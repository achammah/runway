#if !RUNWAY_FX_USHOTS_OFF
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Runway.App
{
    /// <summary>
    /// THE SHUTTER — the twin of the two lines every Godot harness ends on:
    ///
    ///     await RenderingServer.frame_post_draw
    ///     root.get_viewport().get_texture().get_image().save_png(path)
    ///
    /// `frame_post_draw` becomes `WaitForEndOfFrame`, which is the only moment the
    /// back buffer holds a finished frame — including the ScreenSpaceOverlay canvas
    /// every screen in this game is drawn on.
    ///
    /// TWO BACKENDS, ONE PICTURE. `ScreenCapture.CaptureScreenshotAsTexture()` is the
    /// intended path, but `UnityEngine.ScreenCaptureModule` is NOT in this project's
    /// `Packages/manifest.json`, so naming the type directly is a compile error today
    /// (verified: CS0103). It is therefore reached by reflection — present, it is used;
    /// absent, the frame is read straight off the back buffer with `ReadPixels`, which
    /// lives in the always-present core module and produces the same image. Adding
    /// `com.unity.modules.screencapture` to the manifest switches the backend with no
    /// code change; the log line says which one took the picture.
    ///
    /// EVERY PNG IS CHECKED. A screenshot harness that writes 23 black rectangles
    /// still exits green, which is the failure mode this check exists to stop: the
    /// luminance spread of each capture is measured, and a flat one is written anyway
    /// (never lose the evidence) and then reported as SHOT FLAT.
    /// </summary>
    public static class UnityShotsCamera
    {
        /// The Godot viewport every reference shot was taken at.
        public const int TwinWidth = 1536;
        public const int TwinHeight = 1024;

        /// A capture whose brightest and darkest pixels are this close is not a picture.
        public const int FlatSpread = 8;

        public static readonly List<string> Written = new List<string>();
        public static readonly List<string> Flat = new List<string>();
        public static readonly List<string> Failed = new List<string>();
        public static readonly List<string> WrongSize = new List<string>();

        static readonly WaitForEndOfFrame EndOfFrame = new WaitForEndOfFrame();

        static MethodInfo _screenCapture;
        static bool _backendProbed;

        /// One picture: wait for the finished frame, grab it, write it, judge it.
        public static IEnumerator Shoot(string dir, string name)
        {
            yield return EndOfFrame;

            Texture2D tex = null;
            try
            {
                tex = Grab();
            }
            catch (Exception e)
            {
                Fail(name, "the grab threw " + e.Message);
                yield break;
            }
            if (tex == null)
            {
                Fail(name, "the grab came back empty");
                yield break;
            }

            int w = tex.width;
            int h = tex.height;
            string path = Path.Combine(dir, name + ".png");
            bool wrote = false;
            try
            {
                byte[] png = tex.EncodeToPNG();
                if (png != null && png.Length > 0)
                {
                    File.WriteAllBytes(path, png);
                    wrote = true;
                }
                else
                {
                    Fail(name, "EncodeToPNG returned nothing");
                }
            }
            catch (Exception e)
            {
                Fail(name, "could not write " + path + " (" + e.Message + ")");
            }

            int lo = 255;
            int hi = 0;
            if (wrote)
            {
                try { Spread(tex, out lo, out hi); }
                catch (Exception e) { Debug.LogWarning("USHOTS could not read " + name + ": " + e.Message); }
            }

            UnityEngine.Object.Destroy(tex);
            if (!wrote) yield break;

            Written.Add(name);
            Debug.Log(string.Format("SHOT {0} · {1}x{2} · lum {3}..{4} -> {5}",
                                    name, w, h, lo, hi, path));

            // THE SELF-CHECK: an all-black or all-one-colour frame is not a screen.
            if (hi - lo < FlatSpread)
            {
                Flat.Add(name);
                Debug.LogError("SHOT FLAT " + name);
            }
            if (w != TwinWidth || h != TwinHeight)
            {
                WrongSize.Add(name);
                Debug.LogWarning(string.Format(
                    "SHOT SIZE {0} is {1}x{2}, the Godot twin is {3}x{4} — run the player with "
                    + "-screen-width {3} -screen-height {4} -screen-fullscreen 0",
                    name, w, h, TwinWidth, TwinHeight));
            }
        }

        // ── the two backends ───────────────────────────────────────────────────

        static Texture2D Grab()
        {
            if (!_backendProbed)
            {
                _backendProbed = true;
                _screenCapture = FindScreenCapture();
                Debug.Log("USHOTS shutter: " + (_screenCapture != null
                    ? "ScreenCapture.CaptureScreenshotAsTexture()"
                    : "Texture2D.ReadPixels off the back buffer — add com.unity.modules.screencapture "
                      + "to Packages/manifest.json for the native path"));
            }
            if (_screenCapture != null)
            {
                try
                {
                    var shot = _screenCapture.Invoke(null, null) as Texture2D;
                    if (shot != null) return shot;
                    Debug.LogWarning("USHOTS ScreenCapture came back null — falling back to ReadPixels.");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("USHOTS ScreenCapture threw (" + e.Message
                                     + ") — falling back to ReadPixels.");
                }
                _screenCapture = null;
            }
            return ReadBackBuffer();
        }

        static MethodInfo FindScreenCapture()
        {
            string[] names =
            {
                "UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule",
                "UnityEngine.ScreenCapture, UnityEngine",
                "UnityEngine.ScreenCapture, UnityEngine.CoreModule",
            };
            for (int i = 0; i < names.Length; i++)
            {
                Type t;
                try { t = Type.GetType(names[i], false); }
                catch (Exception) { continue; }
                if (t == null) continue;
                MethodInfo m = t.GetMethod("CaptureScreenshotAsTexture",
                                           BindingFlags.Public | BindingFlags.Static,
                                           null, Type.EmptyTypes, null);
                if (m != null) return m;
            }
            return null;
        }

        /// The pre-ScreenCapture way, and the reason this harness runs today: after
        /// WaitForEndOfFrame with no RenderTexture bound, ReadPixels reads the finished
        /// back buffer — overlay canvases and all.
        static Texture2D ReadBackBuffer()
        {
            int w = Mathf.Max(Screen.width, 1);
            int h = Mathf.Max(Screen.height, 1);
            RenderTexture.active = null;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new UnityEngine.Rect(0f, 0f, w, h), 0, 0, false);
            tex.Apply(false);
            return tex;
        }

        // ── the pixel-variance self-check ──────────────────────────────────────

        /// Darkest and brightest luminance over a strided sample of the whole frame.
        /// The stride is prime so it never lands on one column of a grid.
        static void Spread(Texture2D tex, out int lo, out int hi)
        {
            lo = 255;
            hi = 0;
            Color32[] px = tex.GetPixels32();
            if (px == null || px.Length == 0) return;
            int stride = Mathf.Max(px.Length / 200000, 1);
            if (stride % 2 == 0) stride++;          // never in step with the row width
            for (int i = 0; i < px.Length; i += stride)
            {
                Color32 c = px[i];
                int lum = (c.r * 77 + c.g * 150 + c.b * 29) >> 8;
                if (lum < lo) lo = lum;
                if (lum > hi) hi = lum;
            }
        }

        static void Fail(string name, string why)
        {
            Failed.Add(name);
            Debug.LogError("SHOT FAILED " + name + " — " + why);
        }
    }
}
#endif
