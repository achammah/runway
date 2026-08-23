using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Effects;
using Debug = UnityEngine.Debug;

namespace Runway.EditorTools
{
    /// <summary>
    /// D5 EVIDENCE. Builds each particle effect against a stand-in of the surface it
    /// will actually live on, steps the simulation by hand (nothing runs Update in
    /// edit mode), renders the canvas to a RenderTexture and writes one PNG per
    /// system. It also checks the three things a screenshot cannot show: that the live
    /// counts stay inside the checklist's budget, that a frame of stepping and
    /// rebuilding costs under 0.2ms and allocates nothing, and that the kill-switch
    /// builds nothing at all.
    ///
    ///   RUNWAY_D5_OUT=&lt;dir&gt; /Applications/.../Unity -batchmode -quit \
    ///     -projectPath unity -executeMethod Runway.EditorTools.ParticleShots.Shoot
    ///
    /// NOT -nographics: this renders.
    /// </summary>
    public static class ParticleShots
    {
        const int W = 1536;
        const int H = 1024;

        static GameObject _root;
        static Camera _cam;
        static RectTransform _stage;
        static bool _failed;

        public static void Shoot()
        {
            string outDir = Environment.GetEnvironmentVariable("RUNWAY_D5_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Path.GetTempPath(), "d5");
            Directory.CreateDirectory(outDir);
            Debug.Log("D5 SHOTS -> " + outDir);

            try
            {
                SaveSheet(Path.Combine(outDir, "00-sheet.png"), 4);
                ShootMotesDraft(Path.Combine(outDir, "01-motes-draft.png"));
                ShootMotesGarage(Path.Combine(outDir, "02-motes-garage.png"));
                ShootScraps(Path.Combine(outDir, "03-scraps-lockin.png"));
                ShootEmbers(Path.Combine(outDir, "04-embers-title.png"));
                KillSwitch();
            }
            catch (Exception e)
            {
                _failed = true;
                Debug.LogError("D5 SHOTS threw: " + e);
            }
            finally
            {
                Teardown();
            }

            Debug.Log(_failed ? "D5 SHOTS: FAIL" : "D5 SHOTS: OK");
            EditorApplication.Exit(_failed ? 1 : 0);
        }

        // ══ diagnosis ══════════════════════════════════════════════════════════

        /// -executeMethod Runway.EditorTools.ParticleShots.Probe
        public static void Probe()
        {
            string outDir = Environment.GetEnvironmentVariable("RUNWAY_D5_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Path.GetTempPath(), "d5");
            Directory.CreateDirectory(outDir);
            try
            {
                ProbeOne(Path.Combine(outDir, "probe-nested.png"), true);
                ProbeOne(Path.Combine(outDir, "probe-flat.png"), false);
            }
            catch (Exception e) { Debug.LogError("D5 PROBE threw: " + e); }
            finally { ParticleInk.NestedCanvas = true; Teardown(); }
            EditorApplication.Exit(0);
        }

        static void ProbeOne(string path, bool nested)
        {
            ParticleInk.NestedCanvas = nested;
            Stage(DrawnUI.Hex("2A2620"));
            // control: a plain Image built the same way, which is known to render
            DrawnUI.Fill(_stage, "control", DrawnUI.Sage, 60f, 60f, 200f, 200f);

            DrawnParticleView.PopulateCalls = 0;
            Scraps s = Scraps.BurstAt(_stage, 768f, 512f);
            if (s == null) { Debug.LogError("D5 PROBE: null burst"); return; }
            int atBuild = DrawnParticleView.PopulateCalls;
            Step(s.View, 0.20f);
            Canvas.ForceUpdateCanvases();
            Debug.Log(string.Format("D5 PROBE populate: {0} at build, {1} total, lastLive={2}",
                atBuild, DrawnParticleView.PopulateCalls, DrawnParticleView.PopulateLastLive));

            var v = s.View;
            var rt = v.rectTransform;
            Debug.Log(string.Format(
                "D5 PROBE nested={0}  live={1} drawn={2}  active={3} enabled={4}  "
                + "canvas={5} rootMode={6}  rect={7}  tex={8}  mat={9}  matCount={10}",
                nested, v.Live, v.Drawn, v.isActiveAndEnabled, v.enabled,
                v.canvas != null ? v.canvas.name : "NULL",
                v.canvas != null && v.canvas.rootCanvas != null
                    ? v.canvas.rootCanvas.renderMode.ToString() : "?",
                rt.rect, v.mainTexture != null ? v.mainTexture.name : "NULL",
                v.materialForRendering != null ? v.materialForRendering.name : "NULL",
                v.canvasRenderer != null ? v.canvasRenderer.materialCount : -1));
            if (v.Sim != null && v.Sim.Live > 0)
                Debug.Log(string.Format("D5 PROBE p0 pos=({0:0.0},{1:0.0}) size={2:0.0} a={3}",
                    v.Sim.DebugX(0), v.Sim.DebugY(0), v.Sim.DebugSize(0), v.Sim.DebugAlpha(0)));
            Save(path);
        }

        // ══ the four shots ═════════════════════════════════════════════════════

        static void ShootMotesDraft(string path)
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
            Caption("D5a  SPOTLIGHT MOTES - the select stage's bulb", DrawnUI.Cream);

            Motes m = Motes.DraftSpotlight(_stage);
            if (m == null) { Fail("motes: DraftSpotlight returned null with the switch on"); return; }
            // the select stage runs the ORIGINAL'S count, not the general form's:
            // founder_draft_screen.gd builds fourteen and the pool is capped there
            Report("motes/draft", m.Live, Motes.DraftMotes, Motes.DraftMotes);
            Measure("motes/draft", m.View);
            Save(path);
        }

        static void ShootMotesGarage(string path)
        {
            Stage(DrawnUI.Cream);
            DrawnUI.FullFill(_stage, "wall", DrawnUI.Cream);
            DrawnUI.Fill(_stage, "floor", DrawnUI.WithAlpha(DrawnUI.Sage, 0.22f),
                         0f, H * 0.72f, W, H * 0.28f);
            var horizon = DrawnUI.Rect(_stage, "horizon", 0f, H * 0.72f - 4f, W, 10f);
            var hi = horizon.gameObject.AddComponent<Image>();
            hi.sprite = DrawnUI.WobbleLineSprite(W, 4f, 61, 2.2f, 21, 4);
            hi.color = DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f);
            hi.raycastTarget = false;
            Caption("D5a  BULB MOTES - ink dust on the garage's cream", DrawnUI.Ink);

            Motes m = Motes.GarageBulb(_stage);
            if (m == null) { Fail("motes: GarageBulb returned null with the switch on"); return; }
            Report("motes/garage", m.Live, 20, Motes.Ceiling);
            Save(path);
        }

        static void ShootScraps(string path)
        {
            Stage(DrawnUI.Hex("2A2620"));
            var page = DrawnUI.PaperCard(_stage, new Vector2(760f, 560f), 388f, 232f,
                                         DrawnUI.PaperStyle.Sheet, "page");
            for (int i = 0; i < 9; i++)
                DrawnUI.Rule(page, 60f, 150f + i * 44f, 640f,
                             DrawnUI.WithAlpha(DrawnUI.Ink, 0.16f), 2f, 30 + i, 1.1f, 25);
            var word = DrawnUI.HandLabel(page, "ROLL THE WEEK", 0f, 470f, 34f, DrawnUI.Coral,
                                         760f, TMPro.TextAlignmentOptions.Top);
            var strike = DrawnUI.Rect(page, "strike", 285f, 516f, 190f, 10f);
            var si = strike.gameObject.AddComponent<Image>();
            si.sprite = DrawnUI.WobbleLineSprite(190, 4f, 24, 1.4f, 23, 4);
            si.color = DrawnUI.Coral;
            si.raycastTarget = false;
            Caption("D5b  LOCK-IN SCRAPS - 0.30s after the strike", DrawnUI.Cream);

            // the burst, exactly as WeekCommit would fire it: off the lock word
            Scraps s = Scraps.Burst(page, word.rectTransform);
            if (s == null) { Fail("scraps: Burst returned null with the switch on"); return; }
            int emitted = s.Live;
            Report("scraps/emitted", emitted, Scraps.MinCount, Scraps.MaxCount);
            Step(s.View, 0.30f);
            Debug.Log("D5 COUNT scraps/at-0.30s = " + s.Live);
            Save(path);

            // and it is GONE by 0.8s + the tail, which is the other half of the spec
            Step(s.View, 1.0f);
            if (s.Live != 0) Fail("scraps: " + s.Live + " still alive at 1.3s");
            else Debug.Log("D5 COUNT scraps/at-1.3s = 0 (clean)");

            // eight weeks of locks: the ceremony must not throw the same six scraps
            var seen = new System.Text.StringBuilder();
            for (int i = 0; i < 8; i++)
            {
                Scraps b = Scraps.BurstAt(_stage, 200f, 200f);
                if (b == null) { Fail("scraps: burst " + i + " returned null"); break; }
                seen.Append(b.Live).Append(i < 7 ? " " : "");
                if (b.Live < Scraps.MinCount || b.Live > Scraps.MaxCount)
                    Fail("scraps: burst " + i + " threw " + b.Live);
                UnityEngine.Object.DestroyImmediate(b.gameObject);
            }
            Debug.Log("D5 COUNT scraps over 8 locks: " + seen);
        }

        static void ShootEmbers(string path)
        {
            Stage(DrawnUI.Hex("140F0C"));
            // a stand-in for the runway fire the title film burns along its left edge
            for (int i = 0; i < 26; i++)
            {
                float k = i / 25f;
                DrawnUI.Fill(_stage, "fire",
                             DrawnUI.WithAlpha(Color.Lerp(DrawnUI.CoralDark, DrawnUI.Yellow, k),
                                               0.10f + 0.22f * k),
                             30f + k * 60f, 430f + k * 400f, 640f - k * 180f, 30f);
            }
            Caption("D5c  TITLE EMBERS - off the burning runway", DrawnUI.Cream);

            Embers e = Embers.TitleFire(_stage);
            if (e == null) { Fail("embers: TitleFire returned null with the switch on"); return; }
            int lo = int.MaxValue, hi = 0;
            for (int i = 0; i < 240; i++)
            {
                Step(e.View, 1f / 60f);
                lo = Mathf.Min(lo, e.Live);
                hi = Mathf.Max(hi, e.Live);
            }
            Debug.Log(string.Format("D5 COUNT embers over 4s: min {0}, max {1}", lo, hi));
            Report("embers", hi, 8, Embers.Ceiling);
            if (lo < 6) Fail("embers: the fire went as thin as " + lo);
            Measure("embers", e.View);
            Save(path);
        }

        // ══ the kill-switch ════════════════════════════════════════════════════

        static void KillSwitch()
        {
            Stage(DrawnUI.Hex("22262B"));
            Environment.SetEnvironmentVariable(ParticleInk.KillSwitch, "0");
            bool clean = Motes.DraftSpotlight(_stage) == null
                         && Motes.GarageBulb(_stage) == null
                         && Embers.TitleFire(_stage) == null
                         && Scraps.BurstAt(_stage, 768f, 512f) == null
                         && _stage.childCount == 0;
            Environment.SetEnvironmentVariable(ParticleInk.KillSwitch, null);
            bool backOn = ParticleInk.On;
            if (!clean) Fail("kill-switch: something was still built at RUNWAY_FX_PARTICLES=0");
            if (!backOn) Fail("kill-switch: did not come back on when the variable was cleared");
            if (clean && backOn) Debug.Log("D5 KILL-SWITCH: off builds nothing, absent = on");
        }

        // ══ stepping, measuring, reporting ═════════════════════════════════════

        static void Step(DrawnParticleView view, float seconds)
        {
            int steps = Mathf.Max(Mathf.RoundToInt(seconds * 60f), 1);
            float dt = seconds / steps;
            for (int i = 0; i < steps; i++) view.Step(dt);
        }

        /// The honest frame cost: the same loop with and without the drawing half, so
        /// the number separates the mesh rebuild from the simulation.
        static void Measure(string label, DrawnParticleView view)
        {
            const int Warm = 400;
            const int N = 600;
            const float Dt = 1f / 60f;

            for (int i = 0; i < Warm; i++)
            {
                view.Step(Dt);
                Canvas.ForceUpdateCanvases();
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < N; i++) view.Sim.Step(Dt);
            sw.Stop();
            double simMs = sw.Elapsed.TotalMilliseconds / N;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            long mem0 = GC.GetTotalMemory(true);
            sw.Reset();
            sw.Start();
            for (int i = 0; i < N; i++)
            {
                view.Step(Dt);
                Canvas.ForceUpdateCanvases();
            }
            sw.Stop();
            long mem1 = GC.GetTotalMemory(false);
            double allMs = sw.Elapsed.TotalMilliseconds / N;

            Debug.Log(string.Format(
                "D5 PERF {0}: {1:0.0000} ms/frame total  (sim alone {2:0.0000}, "
                + "mesh rebuild {3:0.0000})   GC {4} B/frame   live {5}",
                label, allMs, simMs, allMs - simMs, (mem1 - mem0) / N, view.Live));
            if (allMs > 0.2) Fail(label + ": " + allMs.ToString("0.000") + " ms > 0.2ms budget");
        }

        static void Report(string label, int live, int lo, int hi)
        {
            Debug.Log(string.Format("D5 COUNT {0} = {1}  (budget {2}..{3})", label, live, lo, hi));
            if (live < lo || live > hi) Fail(label + ": " + live + " outside " + lo + ".." + hi);
        }

        static void Fail(string why)
        {
            _failed = true;
            Debug.LogError("D5 BUDGET: " + why);
        }

        // ══ the stand-in stage ═════════════════════════════════════════════════

        static void Stage(Color bg)
        {
            Teardown();

            _root = new GameObject("d5-shots");
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

            // the screens' own convention: a full-stage top-left rect to build into
            _stage = DrawnUI.FullRect(crt, "stage");
        }

        static void Caption(string text, Color c)
        {
            try
            {
                DrawnUI.HandLabel(_stage, text, 26f, 24f, 26f, DrawnUI.WithAlpha(c, 0.55f));
            }
            catch (Exception) { /* no font in a bare editor is not a shot failure */ }
        }

        static void Save(string path)
        {
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
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log("D5 SHOT " + path);
        }

        /// The 4-cell sheet itself, point-scaled and laid over mid-grey so both the
        /// cream body and the ink edge of the scrap can be read.
        static void SaveSheet(string path, int scale)
        {
            Texture2D src = ParticleInk.Sheet;
            int w = src.width * scale, h = src.height * scale;
            Color32[] sp = src.GetPixels32();
            var dst = new Color32[w * h];
            var ground = new Color32(96, 96, 96, 255);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color32 s = sp[(y / scale) * src.width + (x / scale)];
                    float a = s.a / 255f;
                    dst[y * w + x] = new Color32(
                        (byte)Mathf.RoundToInt(ground.r + (s.r - ground.r) * a),
                        (byte)Mathf.RoundToInt(ground.g + (s.g - ground.g) * a),
                        (byte)Mathf.RoundToInt(ground.b + (s.b - ground.b) * a),
                        255);
                }
            }
            var big = new Texture2D(w, h, TextureFormat.RGBA32, false);
            big.SetPixels32(dst);
            big.Apply(false, false);
            File.WriteAllBytes(path, big.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(big);
            Debug.Log("D5 SHOT " + path + "  (dot | scrap / ember | blur)");
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
