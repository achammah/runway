using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Effects;
using Runway.Game;
using Debug = UnityEngine.Debug;

namespace Runway.EditorTools
{
    /// <summary>
    /// THE POLISH PASS, MEASURED. Three of the four defects in this batch can be
    /// settled without a running game, because the thing that is wrong is arithmetic
    /// or a baked texture rather than a behaviour:
    ///
    ///   P8   the binder street's block height — BinderScreen.Height against Godot's
    ///        `_wrap_h`, on the fixture the binder_7 shot is taken of, block by block
    ///        and then as the cumulative drift down the page
    ///   D5a  the select stage's motes — the old config's live and drawn counts
    ///        against the new one's, against founder_draft_screen.gd's fourteen, with
    ///        a picture of each
    ///   P11  the live tab's pen ring — whether the ellipse is actually tilted, from
    ///        the second moment of its own ink, at every seed the mount could pass
    ///
    /// The fourth (the caret and the selection in a live field) needs play mode,
    /// because TMP_InputField builds its caret only `if (Application.isPlaying)`. That
    /// one is WriteFieldShot.
    ///
    ///   RUNWAY_POLISH_OUT=&lt;dir&gt; /Applications/.../Unity -batchmode -quit \
    ///     -projectPath unity -executeMethod Runway.EditorTools.PolishProbe.Run
    ///
    /// NOT -nographics: the mote shots render.
    /// </summary>
    public static class PolishProbe
    {
        const int W = 1536;
        const int H = 1024;

        static GameObject _root;
        static Camera _cam;
        static RectTransform _stage;
        static StringBuilder _log;

        public static void Run()
        {
            string dir = Environment.GetEnvironmentVariable("RUNWAY_POLISH_OUT");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Path.GetTempPath(), "d-polish");
            Directory.CreateDirectory(dir);
            _log = new StringBuilder();
            Say("polish evidence · out " + dir);
            Say("ground truth: game/src/ui/binder.gd · game/src/screens/founder_draft_screen.gd");
            Say("");

            try { P8Street(); } catch (Exception e) { Say("P8 FAILED: " + e); }
            try { P11Ring(dir); } catch (Exception e) { Say("P11 FAILED: " + e); }
            try { MotesDraft(dir); } catch (Exception e) { Say("D5a FAILED: " + e); }

            Teardown();
            try { File.WriteAllText(Path.Combine(dir, "measurements.txt"), _log.ToString()); }
            catch (Exception) { }
            EditorApplication.Exit(0);
        }

        // ══ P8 · the binder street's block heights ═════════════════════════════

        /// The two lines the street measures, on the PIVOTFLOW fixture binder_7 is
        /// shot of — `plays: ` joined from a rival's tactics at 26px, and an
        /// investor's thesis + trait at 25px, both wrapped to 1070.
        static readonly string[] Plays =
        {
            "plays: sells monthly rain-recovery memberships that renew before anyone "
            + "reads the invoice, gives local employers discounted weekday vouchers, "
            + "filling the quiet hours, offers free prosecco on thursday evenings, "
            + "because hydration",
            "plays: undercuts massage prices with tightly timed 25-minute slots, "
            + "partners with beauty influencers for carefully cropped testimonials, "
            + "runs last-minute WhatsApp deals whenever the steam rooms sit empty",
        };

        static readonly string[] Quotes =
        {
            "\"belgian wellness works when it sells a repeatable three-hour escape to "
            + "people who cannot leave their jobs, not a lifestyle pivot\"  ·  carries "
            + "a stopwatch and notices queue lengths before introductions",
            "\"the interesting part of this market is the ritual: work, rain, traffic, "
            + "then warmth — package the ritual, not the room\"  ·  arrives with a "
            + "photographer and calls everyone part of the narrative",
        };

        static void P8Street()
        {
            Stage(DrawnUI.Cream);
            var shipped = typeof(BinderScreen).GetMethod(
                "Height", BindingFlags.NonPublic | BindingFlags.Static);
            if (shipped == null) { Say("P8: BinderScreen.Height not found by reflection"); return; }

            Say("── P8 · THE STREET'S BLOCK HEIGHTS  (binder.gd _wrap_h, tab 7) ──");
            Say("  godot   N x (ceil(ascent) + ceil(descent))  ·  no leading, whole pixels");
            Say("  was     max(TMP preferredHeight, size x 1.3)");
            Say("  now     BinderScreen.Height, called here by reflection");
            Say("");

            // the same walk _tab_street does: a header line, then a measured block,
            // then a fixed gap — twice over, rivals then investors
            float yWas = 80f, yNow = 80f;
            var rows = new List<string>();
            for (int i = 0; i < Plays.Length; i++)
                rows.Add(Row(shipped, "rival " + (i + 1) + " plays", Plays[i], 26f,
                             50f, 18f, ref yWas, ref yNow));
            yWas += 64f; yNow += 64f;                       // "the money:" and its step
            for (int i = 0; i < Quotes.Length; i++)
                rows.Add(Row(shipped, "investor " + (i + 1) + " quote", Quotes[i], 25f,
                             44f, 16f, ref yWas, ref yNow));

            for (int i = 0; i < rows.Count; i++) Say(rows[i]);
            Say("");
            Say(string.Format("  page ends at   was {0:0.0}   now {1:0.0}   DRIFT {2:+0.0;-0.0;0.0}px",
                              yWas, yNow, yWas - yNow));
            Say("");
        }

        static string Row(MethodInfo shipped, string label, string text, float size,
                          float lead, float trail, ref float yWas, ref float yNow)
        {
            var t = DrawnUI.HandLabel(_stage, text, 30f, 0f, size,
                                      DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 1070f,
                                      TextAlignmentOptions.TopLeft);
            t.ForceMeshUpdate();
            int lines = Mathf.Max(1, t.textInfo.lineCount);
            float was = Mathf.Max(t.preferredHeight, t.fontSize * 1.3f);
            float now = (float)shipped.Invoke(null, new object[] { t });
            float godot = lines * GodotBox(t.font, size);

            float atWas = yWas, atNow = yNow;
            yWas += lead + was + trail;
            yNow += lead + now + trail;
            return string.Format(
                "  {0,-22} {1} lines @{2:0}px   was {3,6:0.0}   now {4,6:0.0}   godot {5,6:0.0}"
                + "   err was {6:+0.0;-0.0;0.0} now {7:+0.0;-0.0;0.0}   y was {8:0.0} now {9:0.0}",
                label, lines, size, was, now, godot, was - godot, now - godot, atWas, atNow);
        }

        /// Godot's own line box, spelled out from the font's metrics rather than
        /// through DrawnUI — an independent check that the shipped method agrees.
        static float GodotBox(TMP_FontAsset f, float size)
        {
            float asc = 1.042f, desc = 0.312f;
            if (f != null && f.faceInfo.pointSize > 0f)
            {
                asc = f.faceInfo.ascentLine / f.faceInfo.pointSize;
                desc = -f.faceInfo.descentLine / f.faceInfo.pointSize;
            }
            return Mathf.Ceil(asc * size) + Mathf.Ceil(desc * size);
        }

        // ══ P11 · is the pen ring actually tilted ══════════════════════════════

        /// The second moment of the ink tells the truth a screenshot argues about: for
        /// an axis-aligned ellipse the covariance is diagonal and the principal axis
        /// is at 0.0°, and rx 68 against ry 26 makes the denominator big, so the
        /// number is well conditioned — a degree here is a degree of real tilt.
        static void P11Ring(string dir)
        {
            Say("── P11 · THE LIVE TAB'S PEN RING  (binder.gd _Clipboard) ──");
            Say("  godot   33 points of (cos t x 68, sin t x 26), jitter ±2 — AXIS ALIGNED");
            Say("  unity   DrawnChart.PenEllipse, same walk, jitter off System.Random(seed)");
            Say("  a tilt can only come from the jitter: nothing rotates the mount.");
            for (int seed = 1; seed <= 9; seed++)
            {
                Sprite sp = DrawnChart.PenEllipse(68f, 26f, 3.5f, 2f, seed, DrawnUI.Coral);
                float ang, wide, tall;
                int ink = Axis(sp.texture, out ang, out wide, out tall);
                Say(string.Format("  seed {0}   tilt {1,6:+0.00;-0.00;0.00}°   ink {2:0}x{3:0}"
                                  + "   {4} px{5}", seed, ang, wide, tall, ink,
                                  seed == 5 ? "   ← MOUNTED TODAY" : ""));
                if (seed == 5) WriteSprite(dir, "ring_seed5.png", sp);
            }
            Say("");
        }

        /// Ink extent and principal-axis angle, in degrees, y DOWN like the screen.
        static int Axis(Texture2D tex, out float degrees, out float wide, out float tall)
        {
            Color32[] px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            double sx = 0, sy = 0, n = 0;
            int x0 = int.MaxValue, x1 = -1, y0 = int.MaxValue, y1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a < 40) continue;
                    sx += x; sy += y; n++;
                    if (x < x0) x0 = x; if (x > x1) x1 = x;
                    if (y < y0) y0 = y; if (y > y1) y1 = y;
                }
            wide = x1 - x0 + 1; tall = y1 - y0 + 1;
            degrees = 0f;
            if (n < 8) return (int)n;
            double mx = sx / n, my = sy / n, cxx = 0, cyy = 0, cxy = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a < 40) continue;
                    double dx = x - mx, dy = y - my;
                    cxx += dx * dx; cyy += dy * dy; cxy += dx * dy;
                }
            // the texture reads bottom-up, so the sign flips once to say it in screen
            // terms: positive is the right-hand end of the ring hanging LOW
            degrees = (float)(-0.5 * Math.Atan2(2.0 * cxy, cxx - cyy) * Mathf.Rad2Deg);
            return (int)n;
        }

        static void WriteSprite(string dir, string name, Sprite sp)
        {
            Texture2D src = sp.texture;
            int w = src.width, h = src.height;
            Color32[] a = src.GetPixels32();
            var outp = new Color32[a.Length];
            Color32 cream = DrawnUI.Cream;
            for (int i = 0; i < a.Length; i++)
            {
                float k = a[i].a / 255f;
                outp[i] = new Color32(
                    (byte)Mathf.RoundToInt(a[i].r * k + cream.r * (1f - k)),
                    (byte)Mathf.RoundToInt(a[i].g * k + cream.g * (1f - k)),
                    (byte)Mathf.RoundToInt(a[i].b * k + cream.b * (1f - k)), 255);
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(outp);
            tex.Apply(false, false);
            File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        // ══ D5a · the select stage's motes ═════════════════════════════════════

        static void MotesDraft(string dir)
        {
            Say("── D5a · MOTES IN THE SELECT CONE  (founder_draft_screen.gd:716-740) ──");
            Say("  godot   14 ColorRects, 2.0..4.5px, cream, a 0.18..0.4, box 600..990 x");
            Say("          300..840, each RISING 80..150px over 5..9s on a looping tween");
            Say("");

            const float w = RunwayPaths.StageWidth;
            const float h = RunwayPaths.StageHeight;
            float wide = w * 0.44f;
            var beam = new Rect((w - wide) * 0.5f, 0f, wide, h * 0.86f);

            // BEFORE is the old DraftSpotlight body, which was the general form with
            // the cream tint — still reachable, so this is the real old air
            Cone();
            Motes before = Motes.Apply(_stage, beam, w * 0.16f, DrawnUI.Cream, 0.14f, 0.38f, 91);
            ReportMotes("was  (general form)", before);
            Shoot(dir, "motes_draft_before.png", before);

            Cone();
            Motes after = Motes.DraftSpotlight(_stage);
            ReportMotes("now  (godot's own)", after);
            Shoot(dir, "motes_draft_after.png", after);
            Say("");
        }

        static void Cone()
        {
            Stage(DrawnUI.Hex("22262B"));
            DrawnUI.Fill(_stage, "floor", DrawnUI.Hex("2C343B"), 0f, H * 0.78f, W, H * 0.22f);
            for (int i = 0; i < 14; i++)
            {
                float k = i / 13f;
                float cw = Mathf.Lerp(W * 0.16f, W * 0.44f, k);
                DrawnUI.Fill(_stage, "cone", DrawnUI.WithAlpha(DrawnUI.Cream, 0.012f),
                             (W - cw) * 0.5f, H * 0.86f * k, cw, H * 0.86f / 14f + 1f);
            }
        }

        static void ReportMotes(string label, Motes m)
        {
            if (m == null) { Say("  " + label + ": nothing built (kill-switch?)"); return; }
            // settle it the way a played minute would, then read what the cone left
            int live = 0, drawn = 0, samples = 0;
            float lo = 999f, hi = 0f;
            for (int i = 0; i < 900; i++)
            {
                m.View.Step(1f / 60f);
                Canvas.ForceUpdateCanvases();
                if (i < 600 || i % 30 != 0) continue;
                live += m.Live; drawn += m.View.Drawn; samples++;
            }
            for (int i = 0; i < m.Live; i++)
            {
                float s = m.Sim.DebugSize(i);
                if (s < lo) lo = s; if (s > hi) hi = s;
            }
            Say(string.Format("  {0}   live {1:0.0}   DRAWN IN THE CONE {2:0.0}"
                              + "   quad {3:0.0}..{4:0.0}px (speck ≈ {5:0.0}..{6:0.0})",
                              label, live / (float)samples, drawn / (float)samples,
                              lo, hi, lo / 3f, hi / 3f));
        }

        // ══ the stand-in stage ═════════════════════════════════════════════════

        static void Say(string line)
        {
            Debug.Log("POLISH: " + line);
            _log.Append(line).Append('\n');
        }

        static void Stage(Color bg)
        {
            Teardown();
            _root = new GameObject("d-polish");
            _root.hideFlags = HideFlags.DontSave;

            var camGo = new GameObject("cam");
            camGo.transform.SetParent(_root.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -10f);
            camGo.transform.localRotation = Quaternion.identity;
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = H * 0.5f;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 100f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = bg;
            _cam.aspect = (float)W / H;

            var canvasGo = new GameObject("canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(_root.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _cam;
            var crt = canvasGo.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(W, H);
            crt.localPosition = Vector3.zero;
            crt.localRotation = Quaternion.identity;
            crt.localScale = Vector3.one;
            _stage = DrawnUI.FullRect(crt, "stage");
        }

        static void Shoot(string dir, string name, Motes m)
        {
            try
            {
                DrawnUI.HandLabel(_stage, name, 26f, 24f, 26f,
                                  DrawnUI.WithAlpha(DrawnUI.Cream, 0.55f));
            }
            catch (Exception) { }
            // a 24-bit depth buffer, so anything stencil-masked in a later shot works
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;
            rt.Create();
            RenderTexture prev = RenderTexture.active;
            _cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            _cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0f, 0f, W, H), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prev;
            _cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log("POLISH SHOT " + name);
        }

        static void Teardown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            _root = null;
            _cam = null;
            _stage = null;
        }
    }
}
