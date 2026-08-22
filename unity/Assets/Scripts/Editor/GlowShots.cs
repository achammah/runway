using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Effects;

namespace Runway.EditorTools
{
    /// <summary>
    /// D6 EVIDENCE — the soft-light lane, photographed.
    ///
    /// Runs headless with a real graphics device (`-batchmode -quit`, NO
    /// `-nographics`) so the additive material is actually rasterised rather than
    /// asserted about:
    ///
    ///   Unity -batchmode -quit -projectPath unity \
    ///         -executeMethod Runway.EditorTools.GlowShots.Shoot
    ///
    /// Output folder comes from RUNWAY_SHOT_DIR (default /tmp/d6). Five frames:
    ///   1 garage-normal   a dark room, warm bulb pool + cool laptop glow
    ///   2 garage-red      the same room with SetRed(true) settled
    ///   3 select-beam     the real env/stage.png with the beam glow over it
    ///   4 garage-drawn    the room as it actually ships (cream wall), lit
    ///   5 killswitch-off  RUNWAY_FX_GLOWS=0 — the same call, nothing built
    ///
    /// It also PRINTS the numbers, because "warm" and "cold" are measurable: the
    /// mean red-minus-blue of the lit wall under the bulb, normal versus red.
    ///
    /// The room here is a STAND-IN, not GarageScreen: the real room needs a run, a
    /// driver and streamed art. It carries the room's real geometry (the object
    /// spots transcribed from GarageScreen) and the real object drawings off disk,
    /// which is what the light has to land on to be judged.
    /// </summary>
    public static class GlowShots
    {
        const int W = 1536;
        const int H = 1024;

        static Camera _cam;
        static RenderTexture _rt;
        static RectTransform _stage;
        static GameObject _rig;
        static readonly StringBuilder _report = new StringBuilder();

        public static void Shoot()
        {
            string dir = Environment.GetEnvironmentVariable("RUNWAY_SHOT_DIR");
            if (string.IsNullOrEmpty(dir)) dir = "/tmp/d6";
            Directory.CreateDirectory(dir);
            Debug.Log("D6 SHOTS: writing to " + dir);

            try
            {
                BuildRig();

                // ── 1. the dark room, lit ──────────────────────────────────────
                GlowSprites.SetRed(false);
                RectTransform room = Room(true);
                var glows = GlowSprites.Apply(room, GlowScene.Garage);
                Settle(2.0f);
                Caption("D6a — a dark room, lit: warm bulb pool (breathing) + cool laptop glow");
                Texture2D normal = Capture(Path.Combine(dir, "d6-garage-normal.png"));
                Probe("normal", normal);
                Breath(room);

                // ── 2. the same room, in the red ───────────────────────────────
                GlowSprites.SetRed(true);
                Settle(2.0f);
                Caption("D6b — the same room, SetRed(true): multiply toward 0.85 + cold tint");
                Texture2D red = Capture(Path.Combine(dir, "d6-garage-red.png"));
                Probe("red", red);
                _report.AppendLine("  red overlay eased to " +
                    (glows != null ? glows.RedAmount.ToString("0.00") : "?") +
                    "   glows built: " + (glows != null ? glows.Count : 0));
                Compare(normal, red);
                GlowSprites.SetRed(false);
                Clear(room);

                // ── 3. the select stage ────────────────────────────────────────
                RectTransform stage = SelectStage();
                GlowSprites.Apply(stage, GlowScene.SelectStage);
                Settle(1.0f);
                Caption("D6c — the drawn stage cone, with the beam glow matched to it");
                Capture(Path.Combine(dir, "d6-select-beam.png"));
                Clear(stage);

                // ── 4. the room as it actually ships: a CREAM wall ─────────────
                RectTransform cream = Room(false);
                GlowSprites.Apply(cream, GlowScene.Garage);
                Settle(1.0f);
                Caption("the shipped drawn room (cream wall), same glow set — restraint on a bright room");
                Capture(Path.Combine(dir, "d6-garage-drawn-cream.png"));
                Clear(cream);

                // ── 5. the kill-switch ─────────────────────────────────────────
                Environment.SetEnvironmentVariable("RUNWAY_FX_GLOWS", "0");
                GlowSprites.ForgetSwitch();
                RectTransform off = Room(true);
                Caption("RUNWAY_FX_GLOWS=0 — the same two calls, nothing built");
                var none = GlowSprites.Apply(off, GlowScene.Garage);
                Settle(0.5f);
                Capture(Path.Combine(dir, "d6-garage-killswitch-off.png"));
                _report.AppendLine("  kill-switch off → Apply returned " +
                                   (none == null ? "null (nothing installed)" : "A RIG — WRONG") +
                                   ", MakeGlow live=" +
                                   GlowSprites.MakeGlow(off, new Vector2(0f, 0f), 10f,
                                                        GlowTint.Warm, 1f).Live);
                Clear(off);
                Environment.SetEnvironmentVariable("RUNWAY_FX_GLOWS", null);
                GlowSprites.ForgetSwitch();

                Debug.Log("D6 SHOTS — measured\n" + _report);
            }
            catch (Exception e)
            {
                Debug.LogError("D6 SHOTS failed: " + e);
            }
            finally
            {
                TearDown();
            }
        }

        // ══ the capture rig ════════════════════════════════════════════════════

        static void BuildRig()
        {
            _rig = new GameObject("D6ShotRig");

            var camGo = new GameObject("shotcam", typeof(Camera));
            camGo.transform.SetParent(_rig.transform, false);
            _cam = camGo.GetComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = DrawnUI.Stage;      // the game's own clear colour
            _cam.orthographic = true;
            _cam.orthographicSize = H * 0.5f;
            _cam.nearClipPlane = 0.3f;
            _cam.farClipPlane = 5000f;
            _cam.transform.position = new Vector3(0f, 0f, -500f);

            _rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            _rt.antiAliasing = 1;
            _rt.Create();
            _cam.targetTexture = _rt;

            var canvasGo = new GameObject("shotcanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(_rig.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _cam;
            canvas.planeDistance = 500f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var stageGo = new GameObject("Stage", typeof(RectTransform));
            _stage = stageGo.GetComponent<RectTransform>();
            _stage.SetParent(canvasGo.transform, false);
            _stage.anchorMin = new Vector2(0.5f, 0.5f);
            _stage.anchorMax = new Vector2(0.5f, 0.5f);
            _stage.pivot = new Vector2(0.5f, 0.5f);
            _stage.sizeDelta = new Vector2(W, H);
            _stage.anchoredPosition = Vector2.zero;
        }

        static void TearDown()
        {
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); UnityEngine.Object.DestroyImmediate(_rt); _rt = null; }
            if (_rig != null) { UnityEngine.Object.DestroyImmediate(_rig); _rig = null; }
        }

        static void Clear(RectTransform rt)
        {
            if (rt != null) UnityEngine.Object.DestroyImmediate(rt.gameObject);
        }

        /// No game loop in the editor, so the rigs are driven by hand — in 1/12s
        /// steps, the beat the room actually breathes on.
        static void Settle(float secs)
        {
            int steps = Mathf.CeilToInt(secs * GlowSprites.BreathFps);
            for (int i = 0; i < steps; i++) GlowSprites.StepAll(1f / GlowSprites.BreathFps);
        }

        static Texture2D Capture(string path)
        {
            Canvas.ForceUpdateCanvases();
            _cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var shot = new Texture2D(W, H, TextureFormat.RGBA32, false);
            shot.ReadPixels(new UnityEngine.Rect(0f, 0f, W, H), 0, 0);
            shot.Apply(false, false);
            RenderTexture.active = prev;
            File.WriteAllBytes(path, shot.EncodeToPNG());
            Debug.Log("D6 SHOT: " + path);
            return shot;
        }

        // ══ the rooms ══════════════════════════════════════════════════════════

        /// THE ROOM, WITH ITS REAL GEOMETRY. Every coordinate is transcribed from
        /// GarageScreen: the floor at 0.72 of the stage, the money pile at (70,760),
        /// the whiteboard at (200,270), the wall chart at (952,300), the laptop at
        /// (390,545) — and the laptop rect carries the room's own object name, so
        /// the glow's follow path is the one that runs.
        static RectTransform Room(bool dark)
        {
            var room = DrawnUI.FullRect(_stage, "room");
            Color wall = dark ? new Color(0.135f, 0.128f, 0.122f) : DrawnUI.Cream;
            Color floor = dark ? new Color(0.10f, 0.105f, 0.118f)
                               : DrawnUI.WithAlpha(DrawnUI.Sage, 0.22f);
            DrawnUI.FullFill(room, "wall", wall);
            DrawnUI.Fill(room, "floor", floor, 0f, H * 0.72f, W, H * 0.28f);
            DrawnUI.Rule(room, 0f, H * 0.72f - 4f, W,
                         DrawnUI.WithAlpha(dark ? new Color(0.04f, 0.04f, 0.05f) : DrawnUI.Ink, 0.6f),
                         4f, 4, 2.2f, 61);

            // unlit objects sit dark; the light is what brings them up
            float tint = dark ? 0.42f : 1f;
            Pic(room, "money", "gv/money_2.png", 70f, 760f, 288f, 180f, tint);
            Pic(room, "board", "gv/board_2.png", 200f, 270f, 336f, 210f, tint);
            Pic(room, "chart", "gv/chart_2.png", 952f, 300f, 240f, 150f, tint);
            Pic(room, "item_itm_laptop", "itm_laptop.png", 390f, 545f, 176f, 110f, tint);
            Pic(room, "item_itm_houseplant", "itm_houseplant.png", 1252f, 420f, 176f, 110f, tint);
            Pic(room, "founder", "chr_arch_hacker.png", 600f, 617f, 214f, 214f, tint);
            return room;
        }

        /// CHOOSE YOUR FOUNDER, as the screen builds it: the night field, the painted
        /// stage over it, and the founder standing in the drawn cone.
        static RectTransform SelectStage()
        {
            var page = DrawnUI.FullRect(_stage, "select");
            DrawnUI.FullFill(page, "night", GameUiNight);
            var stageRt = DrawnUI.FullRect(page, "stage");
            var img = stageRt.gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            Texture2D tex = Art("env/stage.png");
            if (tex != null) img.texture = tex; else img.enabled = false;
            return page;
        }

        /// GameUi.Night, without dragging the run lane into an editor script.
        static readonly Color GameUiNight = DrawnUI.Hex("39434B");

        static TMPro.TextMeshProUGUI _caption;
        static Image _captionBar;

        /// The caption lives OUTSIDE the room, so the light never falls on it and the
        /// red multiply never dims it — a label about the shot, not part of it.
        static void Caption(string text)
        {
            try
            {
                if (_caption == null)
                {
                    _captionBar = DrawnUI.Fill(_stage, "captionbar", new Color(0f, 0f, 0f, 0.62f),
                                               0f, H - 52f, W, 52f);
                    _caption = DrawnUI.HandLabel(_stage, text, 26f, H - 46f, 26f,
                                                 DrawnUI.WithAlpha(DrawnUI.Cream, 0.92f), 1460f);
                }
                _caption.text = text;
                if (_captionBar != null) _captionBar.transform.SetAsLastSibling();
                _caption.transform.SetAsLastSibling();
                _caption.ForceMeshUpdate();
            }
            catch (Exception e)
            {
                Debug.Log("D6 SHOTS: caption skipped (" + e.Message + ")");
            }
        }

        /// One drawing off the disk, aspect-fitted the way GameUi.Picture fits it.
        static void Pic(RectTransform parent, string name, string rel,
                        float x, float y, float w, float h, float tint)
        {
            Texture2D tex = Art("sprites/" + rel);
            if (tex == null) return;
            var rt = DrawnUI.Rect(parent, name, x, y, w, h);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            img.color = new Color(tint, tint, tint, 1f);
            float ar = (float)tex.width / Mathf.Max(tex.height, 1);
            float dw = w;
            float dh = dw / ar;
            if (dh > h) { dh = h; dw = dh * ar; }
            rt.sizeDelta = new Vector2(dw, dh);
            DrawnUI.SetTopLeft(rt, x + (w - dw) * 0.5f, y + (h - dh) * 0.5f);
        }

        static Texture2D Art(string relative)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/" + relative);
        }

        // ══ the numbers ════════════════════════════════════════════════════════

        static readonly Dictionary<string, Color> _probes = new Dictionary<string, Color>();

        /// Warmth is measurable: mean red minus mean blue over a patch. Boxes are in
        /// the room's own top-left coordinates.
        static void Probe(string label, Texture2D shot)
        {
            Color bulb = Mean(shot, 660f, 240f, 200f, 160f);
            Color lap = Mean(shot, 400f, 540f, 160f, 120f);
            Color dark = Mean(shot, 30f, 30f, 140f, 120f);
            _probes[label + ".bulb"] = bulb;
            _probes[label + ".laptop"] = lap;
            _probes[label + ".corner"] = dark;
            _report.AppendLine(label + ":");
            _report.AppendLine("  bulb wall   " + Say(bulb));
            _report.AppendLine("  laptop      " + Say(lap));
            _report.AppendLine("  dark corner " + Say(dark));
        }

        /// A still cannot show a breath, so the breath is measured instead: the bulb's
        /// own scale sampled twice a second across one full 4s cycle.
        static void Breath(RectTransform room)
        {
            var bulb = room.Find("glow_layer/glow_warm") as RectTransform;
            if (bulb == null) { _report.AppendLine("breath: no bulb found"); return; }
            float min = float.MaxValue;
            float max = float.MinValue;
            var line = new StringBuilder("  scale:");
            for (int i = 0; i <= 8; i++)
            {
                float s = bulb.localScale.x;
                min = Mathf.Min(min, s);
                max = Mathf.Max(max, s);
                line.Append(string.Format(" {0:0.000}", s));
                Settle(0.5f);
            }
            _report.AppendLine("breath (bulb, 0.5s apart across one 4s cycle):");
            _report.AppendLine(line.ToString());
            _report.AppendLine(string.Format("  swing {0:0.000} → {1:0.000}  = ±{2:0.0}%",
                                             min, max, (max - min) * 0.5f * 100f));
        }

        static void Compare(Texture2D normal, Texture2D red)
        {
            Color a = _probes["normal.bulb"];
            Color b = _probes["red.bulb"];
            float wa = a.r - a.b;
            float wb = b.r - b.b;
            _report.AppendLine("VERDICT:");
            _report.AppendLine(string.Format(
                "  warmth under the bulb  normal {0:0.000} → red {1:0.000}   ({2:0}% of the warmth gone)",
                wa, wb, wa <= 0f ? 0f : (1f - wb / wa) * 100f));
            float la = _probes["normal.bulb"].grayscale;
            float lb = _probes["red.bulb"].grayscale;
            _report.AppendLine(string.Format(
                "  brightness under the bulb  normal {0:0.000} → red {1:0.000}   ({2:0}% dimmer)",
                la, lb, la <= 0f ? 0f : (1f - lb / la) * 100f));
            Color ca = _probes["normal.corner"];
            Color cb = _probes["red.corner"];
            _report.AppendLine(string.Format(
                "  unlit corner  normal r-b {0:0.000} → red r-b {1:0.000}  (cold means this goes NEGATIVE)",
                ca.r - ca.b, cb.r - cb.b));
        }

        static string Say(Color c)
        {
            return string.Format("rgb {0:0.000} {1:0.000} {2:0.000}   warmth(r-b) {3:+0.000;-0.000}",
                                 c.r, c.g, c.b, c.r - c.b);
        }

        /// Mean colour of a box given in top-left stage coordinates. ReadPixels hands
        /// back a bottom-left image, so the row is flipped exactly once, here.
        static Color Mean(Texture2D shot, float x, float y, float w, float h)
        {
            int x0 = Mathf.Clamp(Mathf.RoundToInt(x), 0, W - 1);
            int x1 = Mathf.Clamp(Mathf.RoundToInt(x + w), 0, W);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(H - (y + h)), 0, H - 1);
            int y1 = Mathf.Clamp(Mathf.RoundToInt(H - y), 0, H);
            double r = 0, g = 0, b = 0;
            int n = 0;
            Color[] px = shot.GetPixels(x0, y0, Mathf.Max(x1 - x0, 1), Mathf.Max(y1 - y0, 1));
            for (int i = 0; i < px.Length; i++) { r += px[i].r; g += px[i].g; b += px[i].b; n++; }
            if (n == 0) return Color.black;
            return new Color((float)(r / n), (float)(g / n), (float)(b / n), 1f);
        }
    }
}
