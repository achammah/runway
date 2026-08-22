using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;

namespace Runway.EditorTools
{
    /// <summary>
    /// The line-boil evidence, shot headlessly. Builds a six-piece drawn kit — a paper
    /// button, a sheet, a coral rule, a standing rule and two rings, one hollow one
    /// filled — sweeps the boil onto it exactly as the integration hookup will, then
    /// holds the shared clock on each of its three drawings and writes one PNG per
    /// drawing. What the three files must show: every inked EDGE creeps, every FILL is
    /// byte-identical, and the line of type does not move at all.
    ///
    /// Run it:
    ///   Unity -batchmode -quit -projectPath unity \
    ///         -executeMethod Runway.EditorTools.BoilShot.Shoot
    /// Output goes to $RUNWAY_BOIL_OUT, or a folder under the system temp dir. Set
    /// RUNWAY_FX_BOIL=0 to shoot the kill-switch proof instead: nothing attaches, the
    /// clock never wakes, and the three frames come out identical.
    /// </summary>
    public static class BoilShot
    {
        const int W = 900;
        const int H = 620;

        // the two places the shot proves a FILL cannot move
        static readonly RectInt SheetFill = new RectInt(452, 92, 320, 110);   // cream, inside the sheet
        static readonly RectInt DiscFill = new RectInt(202, 322, 24, 24);     // solid ink, inside the filled ring

        public static void Shoot()
        {
            string dir = Environment.GetEnvironmentVariable("RUNWAY_BOIL_OUT");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Path.GetTempPath(), "runway-boil");
            Directory.CreateDirectory(dir);

            var log = new StringBuilder();
            Say(log, "kit 900x620 · out " + dir);

            DrawnBoil.ForgetSwitch();
            DrawnBoil.Forget();
            DrawnBoil.KeepReadable = true;   // the shot reads the drawings back
            InkBoilClock.Shutdown();
            Say(log, "RUNWAY_FX_BOIL -> boil " + (DrawnBoil.Enabled ? "ON" : "OFF")
                     + " · amplitude " + DrawnBoil.Amplitude.ToString("0.00") + "px"
                     + " · " + DrawnBoil.Fps.ToString("0") + "fps"
                     + " · " + DrawnBoil.Frames + " drawings");

            GameObject camGo = null, canvasGo = null;
            RenderTexture rt = null;
            try
            {
                Camera cam;
                RectTransform stage;
                Build(dir, out camGo, out canvasGo, out cam, out rt, out stage);

                TMP_Text type = stage.GetComponentInChildren<TMP_Text>(true);

                Canvas.ForceUpdateCanvases();

                var sweepWatch = System.Diagnostics.Stopwatch.StartNew();
                int lit = DrawnBoil.Sweep(stage);
                sweepWatch.Stop();
                Say(log, "swept " + stage.GetComponentsInChildren<Graphic>(true).Length
                         + " graphics -> " + lit + " boiling"
                         + " in " + sweepWatch.Elapsed.TotalMilliseconds.ToString("0.0") + "ms"
                         + " · clock live " + InkBoilClock.LiveCount
                         + " · ticking " + InkBoilClock.Ticking);

                if (type != null)
                    Say(log, "type \"" + type.text + "\" carries InkBoil: "
                             + (type.GetComponent<InkBoil>() != null) + "  (must be False)");

                Transform pool = Find(stage, "spotlight");
                if (pool != null)
                    Say(log, "spotlight (a 104px bake drawn 200px wide) carries InkBoil: "
                             + (pool.GetComponent<InkBoil>() != null) + "  (must be False)");

                MeasureVariants(stage, log);
                MeasureCost(stage, log);

                Texture2D[] shots = Frames(cam, rt, stage, log);
                for (int f = 0; f < shots.Length; f++)
                {
                    string p = Path.Combine(dir, "frame_" + f + ".png");
                    File.WriteAllBytes(p, shots[f].EncodeToPNG());
                    Say(log, "wrote " + p);
                }
                MeasureFrames(shots, dir, log);

                for (int f = 0; f < shots.Length; f++) UnityEngine.Object.DestroyImmediate(shots[f]);
            }
            catch (Exception e)
            {
                Say(log, "FAILED: " + e);
            }
            finally
            {
                InkBoilClock.Shutdown();
                if (rt != null) { RenderTexture.active = null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
                if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
            }

            try { File.WriteAllText(Path.Combine(dir, "measurements.txt"), log.ToString()); }
            catch (Exception) { }
        }

        static void Say(StringBuilder log, string line)
        {
            Debug.Log("BOILSHOT: " + line);
            log.Append(line).Append('\n');
        }

        // ── the kit ────────────────────────────────────────────────────────────

        static void Build(string dir, out GameObject camGo, out GameObject canvasGo,
                          out Camera cam, out RenderTexture rt, out RectTransform stage)
        {
            rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            rt.Create();

            camGo = new GameObject("~boilcam", typeof(Camera));
            camGo.hideFlags = HideFlags.HideAndDontSave;
            cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = DrawnUI.Stage;
            cam.targetTexture = rt;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            canvasGo = new GameObject("~boilcanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.hideFlags = HideFlags.HideAndDontSave;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            var stageGo = new GameObject("stage", typeof(RectTransform));
            stage = stageGo.GetComponent<RectTransform>();
            stage.SetParent(canvasGo.transform, false);
            stage.anchorMin = new Vector2(0f, 1f);
            stage.anchorMax = new Vector2(0f, 1f);
            stage.pivot = new Vector2(0f, 1f);
            stage.sizeDelta = new Vector2(W, H);
            stage.anchoredPosition = Vector2.zero;

            DrawnUI.FullFill(stage, "paper", DrawnUI.Cream);

            // 1 · a paper button — the card every menu wears, ink edge and word
            DrawnUI.PaperButton(stage, "LOCK IT IN", 48f, 40f, 300f, 92f, 40f,
                                DrawnUI.Ink, DrawnUI.Coral, null);

            // 2 · a sheet — the bigger hand, and a large cream fill to hold still
            DrawnUI.PaperCard(stage, new Vector2(420f, 200f), 440f, 40f,
                              DrawnUI.PaperStyle.Sheet, "sheet");

            // 3 · a rule — the coral underline
            DrawnUI.Rule(stage, 48f, 200f, 300f, DrawnUI.Coral, 4f, 4, 1.5f, 21);

            // 4 · a standing rule — the curtain's meeting edge
            Pin(stage, "vrule", 700f, 300f, 11f, 250f,
                DrawnUI.WobbleVLineSprite(240, 4f, 33, 2.5f, 7, 5), DrawnUI.Ink);

            // 5 · a hollow ring — the how-to dot, unfilled
            Pin(stage, "ring", 60f, 300f, 68f, 68f,
                DrawnUI.RingSprite(30f, 4f, 2f, 31, 4, false), DrawnUI.Ink);

            // 6 · a filled ring — a solid interior that must not move a pixel
            Pin(stage, "disc", 180f, 300f, 68f, 68f,
                DrawnUI.RingSprite(30f, 3f, 1.6f, 12, 4, true), DrawnUI.Ink);

            // and the two things that must NOT boil: the line of type, and a bake blown
            // up far past 1:1 (the stage spotlight pool), whose every texture pixel is
            // three screen pixels
            DrawnUI.HandLabel(stage, "TEXT NEVER BOILS", 300f, 310f, 44f, DrawnUI.Ink);
            Image pool = Pin(stage, "spotlight", 60f, 400f, 200f, 200f,
                             DrawnUI.RingSprite(48f, 1f, 0f, 5, 2, true),
                             DrawnUI.WithAlpha(DrawnUI.Yellow, 0.5f));
            pool.name = "spotlight";
        }

        static Transform Find(RectTransform stage, string name)
        {
            Transform[] all = stage.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++) if (all[i].name == name) return all[i];
            return null;
        }

        static Image Pin(RectTransform parent, string name, float x, float y, float w, float h,
                         Sprite sprite, Color color)
        {
            var rt = DrawnUI.Rect(parent, name, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // ── what the redraws themselves say ────────────────────────────────────

        static void MeasureVariants(RectTransform stage, StringBuilder log)
        {
            InkBoil[] all = stage.GetComponentsInChildren<InkBoil>(true);
            float worst = 0f;
            for (int i = 0; i < all.Length; i++)
            {
                InkBoil b = all[i];
                Texture2D f0 = b.FrameAt(0), f1 = b.FrameAt(1), f2 = b.FrameAt(2);
                if (f0 == null || f1 == null || f2 == null) { Say(log, "  " + b.name + ": no redraws"); continue; }
                if (f0.width != f1.width || f0.height != f1.height ||
                    f0.width != f2.width || f0.height != f2.height)
                { Say(log, "  " + b.name + ": SIZE DRIFT — the swap would jump"); continue; }

                Color32[] p0 = f0.GetPixels32(), p1 = f1.GetPixels32(), p2 = f2.GetPixels32();
                int ink = 0;
                for (int k = 0; k < p0.Length; k++) if (p0[k].a != 0) ink++;

                float d01 = Shift(p0, p1, f0.width, f0.height);
                float d12 = Shift(p1, p2, f0.width, f0.height);
                float d20 = Shift(p2, p0, f0.width, f0.height);
                float mx = Mathf.Max(d01, Mathf.Max(d12, d20));
                if (mx > worst) worst = mx;

                Say(log, "  " + b.name + " " + f0.width + "x" + f0.height
                         + " · ink " + ink + "px"
                         + " · alpha changed 0>1 " + Changed(p0, p1) + "px"
                         + ", 1>2 " + Changed(p1, p2) + "px"
                         + " · centroid move " + d01.ToString("0.000") + "/"
                         + d12.ToString("0.000") + "/" + d20.ToString("0.000") + "px");
            }
            Say(log, "worst edge move between any two drawings: " + worst.ToString("0.000")
                     + "px  (ceiling " + DrawnBoil.MaxAmplitude.ToString("0.0") + "px)");
        }

        /// What a boil costs where it costs most: the keys screen's 1140x880 sheet, the
        /// biggest ink bake the game builds. Built off to the side, timed, thrown away —
        /// it never reaches a frame.
        static void MeasureCost(RectTransform stage, StringBuilder log)
        {
            var bakeWatch = System.Diagnostics.Stopwatch.StartNew();
            RectTransform card = DrawnUI.PaperCard(stage, new Vector2(1140f, 880f), 3000f, 0f,
                                                   DrawnUI.PaperStyle.Sheet, "cost-probe");
            bakeWatch.Stop();
            Image edge = null;
            Image[] kids = card.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < kids.Length; i++) if (kids[i].name == "edge") edge = kids[i];
            if (edge == null) { UnityEngine.Object.DestroyImmediate(card.gameObject); return; }

            DrawnBoil.KeepReadable = false;   // measure what SHIPS, not what the shot needs
            var watch = System.Diagnostics.Stopwatch.StartNew();
            InkBoil made = DrawnBoil.Apply(edge, DrawnUI.PaperStyle.Sheet.Seed);
            watch.Stop();
            DrawnBoil.KeepReadable = true;

            Texture2D tex = made != null ? made.FrameAt(0) : null;
            long bytes = tex != null ? (long)tex.width * tex.height * 4L * 2L : 0L;
            Say(log, "cost · the game's biggest bake, a 1140x880 sheet edge"
                     + (tex != null ? " (" + tex.width + "x" + tex.height + ")" : "")
                     + " · DrawnUI rasterises it in " + bakeWatch.Elapsed.TotalMilliseconds.ToString("0.0")
                     + "ms · the boil redraws it twice in " + watch.Elapsed.TotalMilliseconds.ToString("0.0")
                     + "ms · both once, at screen build · " + (bytes / 1024L / 1024L)
                     + "MB of texture added (CPU copy released)"
                     + " · attached " + (made != null));

            UnityEngine.Object.DestroyImmediate(card.gameObject);
        }

        static int Changed(Color32[] a, Color32[] b)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++) if (Mathf.Abs(a[i].a - b[i].a) > 2) n++;
            return n;
        }

        /// Alpha-weighted centroid distance — how far the ink actually travelled.
        static float Shift(Color32[] a, Color32[] b, int w, int h)
        {
            double ax = 0, ay = 0, aw = 0, bx = 0, by = 0, bw = 0;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    byte pa = a[row + x].a, pb = b[row + x].a;
                    if (pa != 0) { ax += x * pa; ay += y * pa; aw += pa; }
                    if (pb != 0) { bx += x * pb; by += y * pb; bw += pb; }
                }
            }
            if (aw <= 0 || bw <= 0) return 0f;
            double dx = ax / aw - bx / bw, dy = ay / aw - by / bw;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        // ── the three frames ───────────────────────────────────────────────────

        static Texture2D[] Frames(Camera cam, RenderTexture rt, RectTransform stage, StringBuilder log)
        {
            Texture2D[] shots = Grab(cam, rt, stage, false);
            if (Differ(shots) == 0 && DrawnBoil.Enabled)
            {
                Say(log, "renderer swap showed no motion on the GPU — retrying on overrideSprite");
                Kill(shots);
                DrawnBoil.SwapMode = DrawnBoil.Swap.Sprite;
                shots = Grab(cam, rt, stage, false);
                DrawnBoil.SwapMode = DrawnBoil.Swap.Renderer;
            }
            if (Differ(shots) == 0 && DrawnBoil.Enabled)
            {
                Say(log, "no GPU device motion — compositing the frames from live component state");
                Kill(shots);
                shots = Grab(cam, rt, stage, true);
            }
            Say(log, "frames captured " + (shots.Length) + " · differing pixels across the strip "
                     + Differ(shots));
            return shots;
        }

        static void Kill(Texture2D[] t)
        {
            if (t == null) return;
            for (int i = 0; i < t.Length; i++) if (t[i] != null) UnityEngine.Object.DestroyImmediate(t[i]);
        }

        static Texture2D[] Grab(Camera cam, RenderTexture rt, RectTransform stage, bool cpu)
        {
            var shots = new Texture2D[DrawnBoil.Frames];
            for (int f = 0; f < DrawnBoil.Frames; f++)
            {
                Canvas.ForceUpdateCanvases();
                InkBoilClock.SetFrame(f);
                shots[f] = cpu ? Composite(stage) : Render(cam, rt);
            }
            return shots;
        }

        static Texture2D Render(Camera cam, RenderTexture rt)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            try
            {
                cam.Render();
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new UnityEngine.Rect(0f, 0f, W, H), 0, 0);
                tex.Apply(false, false);
                RenderTexture.active = prev;
            }
            catch (Exception) { }
            return tex;
        }

        /// The same picture, drawn from what the components are actually showing — the
        /// path that answers when a headless editor has no device to render with.
        static Texture2D Composite(RectTransform stage)
        {
            var px = new Color32[W * H];
            Color32 back = DrawnUI.Cream;
            for (int i = 0; i < px.Length; i++) px[i] = back;

            Graphic[] all = stage.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Image img = all[i] as Image;
                if (img == null || !img.isActiveAndEnabled) continue;

                RectInt box = Box(stage, img.rectTransform);
                InkBoil boil = img.GetComponent<InkBoil>();
                Texture2D tex = boil != null ? boil.Shown : null;
                if (tex == null && img.sprite != null) tex = img.sprite.texture as Texture2D;

                if (tex == null) { Flat(px, box, img.color); continue; }
                Stamp(px, box, tex, img.color);
            }

            var outTex = new Texture2D(W, H, TextureFormat.RGB24, false);
            var flipped = new Color32[px.Length];
            for (int y = 0; y < H; y++) Array.Copy(px, y * W, flipped, (H - 1 - y) * W, W);
            outTex.SetPixels32(flipped);
            outTex.Apply(false, false);
            return outTex;
        }

        static RectInt Box(RectTransform stage, RectTransform child)
        {
            var c = new Vector3[4];
            child.GetWorldCorners(c);
            Vector3 tl = stage.InverseTransformPoint(c[1]);
            Vector3 br = stage.InverseTransformPoint(c[3]);
            int x = Mathf.RoundToInt(tl.x), y = Mathf.RoundToInt(-tl.y);
            return new RectInt(x, y, Mathf.RoundToInt(br.x) - x, Mathf.RoundToInt(-br.y) - y);
        }

        static void Flat(Color32[] px, RectInt box, Color color)
        {
            if (color.a <= 0f) return;
            for (int y = Mathf.Max(box.yMin, 0); y < Mathf.Min(box.yMax, H); y++)
                for (int x = Mathf.Max(box.xMin, 0); x < Mathf.Min(box.xMax, W); x++)
                    Blend(px, y * W + x, color.r, color.g, color.b, color.a);
        }

        static void Stamp(Color32[] px, RectInt box, Texture2D tex, Color tint)
        {
            if (box.width <= 0 || box.height <= 0) return;
            Color32[] src = tex.GetPixels32();
            for (int y = Mathf.Max(box.yMin, 0); y < Mathf.Min(box.yMax, H); y++)
            {
                float v = (y - box.yMin + 0.5f) / box.height;
                int sy = Mathf.Clamp(Mathf.FloorToInt((1f - v) * tex.height), 0, tex.height - 1);
                for (int x = Mathf.Max(box.xMin, 0); x < Mathf.Min(box.xMax, W); x++)
                {
                    float u = (x - box.xMin + 0.5f) / box.width;
                    int sx = Mathf.Clamp(Mathf.FloorToInt(u * tex.width), 0, tex.width - 1);
                    Color32 s = src[sy * tex.width + sx];
                    float a = (s.a / 255f) * tint.a;
                    if (a <= 0f) continue;
                    Blend(px, y * W + x, (s.r / 255f) * tint.r, (s.g / 255f) * tint.g,
                          (s.b / 255f) * tint.b, a);
                }
            }
        }

        static void Blend(Color32[] px, int i, float r, float g, float b, float a)
        {
            Color32 d = px[i];
            px[i] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt((r * 255f) * a + d.r * (1f - a)), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((g * 255f) * a + d.g * (1f - a)), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((b * 255f) * a + d.b * (1f - a)), 0, 255),
                255);
        }

        // ── what the three frames say ──────────────────────────────────────────

        static int Differ(Texture2D[] shots)
        {
            if (shots == null || shots.Length < 2) return 0;
            Color32[] a = shots[0].GetPixels32();
            int worst = 0;
            for (int f = 1; f < shots.Length; f++)
            {
                Color32[] b = shots[f].GetPixels32();
                int n = 0;
                for (int i = 0; i < a.Length; i++)
                    if (Math.Abs(a[i].r - b[i].r) > 2 || Math.Abs(a[i].g - b[i].g) > 2
                        || Math.Abs(a[i].b - b[i].b) > 2) n++;
                if (n > worst) worst = n;
            }
            return worst;
        }

        static void MeasureFrames(Texture2D[] shots, string dir, StringBuilder log)
        {
            Color32[][] p = new Color32[shots.Length][];
            for (int f = 0; f < shots.Length; f++) p[f] = shots[f].GetPixels32();

            Say(log, "edges: frame0>1 " + Diff(p[0], p[1]) + "px moved, 1>2 "
                     + Diff(p[1], p[2]) + "px, 2>0 " + Diff(p[2], p[0]) + "px");
            Say(log, "fill · cream sheet interior " + SheetFill + ": "
                     + Region(p, SheetFill) + " differing px  (must be 0)");
            Say(log, "fill · filled-ring interior " + DiscFill + ": "
                     + Region(p, DiscFill) + " differing px  (must be 0)");

            // one picture of where the motion is: what moved 0>1, 1>2, 2>0 in r/g/b
            var map = new Color32[W * H];
            for (int i = 0; i < map.Length; i++)
                map[i] = new Color32(Amp(p[0][i], p[1][i]), Amp(p[1][i], p[2][i]),
                                     Amp(p[2][i], p[0][i]), 255);
            var mapTex = new Texture2D(W, H, TextureFormat.RGB24, false);
            mapTex.SetPixels32(map);
            mapTex.Apply(false, false);
            string mp = Path.Combine(dir, "edges-diff.png");
            File.WriteAllBytes(mp, mapTex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(mapTex);
            Say(log, "wrote " + mp + "  (black = held still; colour = the pen moved, x8)");
        }

        static byte Amp(Color32 a, Color32 b)
        {
            int d = Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);
            d *= 8;
            return (byte)(d > 255 ? 255 : d);
        }

        static int Diff(Color32[] a, Color32[] b)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++)
                if (Math.Abs(a[i].r - b[i].r) > 2 || Math.Abs(a[i].g - b[i].g) > 2
                    || Math.Abs(a[i].b - b[i].b) > 2) n++;
            return n;
        }

        /// Differing pixels inside one rect, counted across every pair of frames.
        static int Region(Color32[][] p, RectInt box)
        {
            int n = 0;
            for (int y = box.yMin; y < box.yMax; y++)
            {
                int row = (H - 1 - y) * W;   // the shots are stored bottom-up, as PNG wants
                for (int x = box.xMin; x < box.xMax; x++)
                {
                    int i = row + x;
                    for (int f = 1; f < p.Length; f++)
                        if (Math.Abs(p[0][i].r - p[f][i].r) > 2 || Math.Abs(p[0][i].g - p[f][i].g) > 2
                            || Math.Abs(p[0][i].b - p[f][i].b) > 2) { n++; break; }
                }
            }
            return n;
        }
    }
}
