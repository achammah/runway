using System;
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
    /// THE D2 EVIDENCE. Films the ink-reveal without launching the game: it rebuilds
    /// the garage's own layering (cream wall, the composed picture at sibling index 1,
    /// the sage floor band and the wobbled horizon over it), runs the REAL
    /// InkReveal.Attach/Step, and shoots the canvas through a camera into PNGs.
    ///
    /// Only the clock is substituted — the strokes, the masks, the cover and the
    /// compositing are the shipped ones.
    ///
    ///   Unity -batchmode -quit -projectPath unity \
    ///         -executeMethod Runway.EditorTools.InkRevealFilm.Film
    ///
    /// (WITHOUT -nographics: it needs a real device to render the canvas.)
    ///
    /// RUNWAY_D2_OUT     where the frames land (default: $TMPDIR/runway-d2)
    /// RUNWAY_D2_FRAMES  how many to write, spread across the reveal (default 6)
    /// </summary>
    public static class InkRevealFilm
    {
        const int ShotW = 768;
        const int ShotH = 512;

        static readonly string[] RoomCandidates =
        {
            "Art/env/garage.png",
            "Art/env/stage.png",
            "Art/env/wall.png",
        };

        public static void Film()
        {
            try { Run(); }
            catch (Exception e)
            {
                Debug.LogError("D2 FILM FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        static void Run()
        {
            string outDir = Environment.GetEnvironmentVariable("RUNWAY_D2_OUT");
            if (string.IsNullOrEmpty(outDir))
                outDir = Path.Combine(Path.GetTempPath(), "runway-d2");
            Directory.CreateDirectory(outDir);

            int want = 6;
            string n = Environment.GetEnvironmentVariable("RUNWAY_D2_FRAMES");
            if (!string.IsNullOrEmpty(n)) int.TryParse(n.Trim(), out want);
            want = Mathf.Clamp(want, 2, InkReveal.Frames);

            Texture2D room = LoadRoom();
            Debug.Log("D2 FILM: room texture " + room.width + "x" + room.height
                      + "  ·  out " + outDir + "  ·  " + want + " frames");

            // ── the stage, the camera, and the room's own layering ─────────────
            var camGo = new GameObject("d2-cam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = DrawnUI.Stage;
            cam.orthographic = false;   // the plain configuration a ScreenSpaceCamera
            cam.fieldOfView = 60f;      // canvas is always driven by
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;

            var rt = new RenderTexture(ShotW, ShotH, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            cam.targetTexture = rt;

            var canvasGo = new GameObject("d2-canvas", typeof(RectTransform), typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;

            var canvasRt = canvasGo.GetComponent<RectTransform>();
            RectTransform stage = DrawnUI.Rect(canvasRt, "stage", 0f, 0f,
                RunwayPaths.StageWidth, RunwayPaths.StageHeight);
            stage.localScale = new Vector3((float)ShotW / RunwayPaths.StageWidth,
                                           (float)ShotH / RunwayPaths.StageHeight, 1f);

            // GarageScreen.BuildRoom, in its own order: wall, floor, horizon — then
            // AdoptComposed inserts the picture at sibling index 1, UNDER the floor
            // tint and the horizon rule. Getting that wrong would flatter the effect.
            RectTransform roomRt = DrawnUI.FullRect(stage, "room");
            DrawnUI.FullFill(roomRt, "wall", DrawnUI.Cream, true);
            DrawnUI.Fill(roomRt, "floor", DrawnUI.WithAlpha(DrawnUI.Sage, 0.22f),
                         0f, RunwayPaths.StageHeight * 0.72f,
                         RunwayPaths.StageWidth, RunwayPaths.StageHeight * 0.28f);
            RectTransform horizon = DrawnUI.Rect(roomRt, "horizon", 0f,
                RunwayPaths.StageHeight * 0.72f - 4f, RunwayPaths.StageWidth, 10f);
            var hImg = horizon.gameObject.AddComponent<Image>();
            hImg.sprite = DrawnUI.WobbleLineSprite((int)RunwayPaths.StageWidth, 4f, 61, 2.2f, 21, 4);
            hImg.color = DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f);
            hImg.raycastTarget = false;

            RectTransform composedRt = DrawnUI.FullRect(roomRt, "composed");
            var composed = composedRt.gameObject.AddComponent<RawImage>();
            composed.raycastTarget = false;
            composedRt.SetSiblingIndex(1);

            // ── the reveal, stepped by hand ────────────────────────────────────
            RawImage cover = InkReveal.Attach(composed, room);
            if (cover == null) throw new Exception("InkReveal.Attach returned nothing");

            var shot = new Texture2D(ShotW, ShotH, TextureFormat.RGB24, false);
            // a ScreenSpaceCamera canvas does not know its own rect until it has been
            // rendered once — this throwaway pass is what makes frame 1 laid out
            Shoot(cam, rt, shot);

            var names = new StringBuilder();
            for (int f = 0; f < want; f++)
            {
                int step = want <= 1 ? 0
                    : Mathf.RoundToInt(f * (InkReveal.Frames - 2f) / (want - 1f));
                InkReveal.Step(cover, step);
                Shoot(cam, rt, shot);
                string file = Path.Combine(outDir,
                    string.Format("d2-{0:00}-step{1:00}.png", f + 1, step));
                File.WriteAllBytes(file, shot.EncodeToPNG());
                names.Append("\n    ").Append(file).Append("   step ").Append(step)
                     .Append("/").Append(InkReveal.Frames - 1)
                     .Append("  t=").Append(((step + 1f) / InkReveal.Frames * InkReveal.Seconds)
                        .ToString("0.00")).Append("s  ")
                     .Append(Flat(shot) ? "*** FLAT — THE RENDER FAILED ***" : "ok");
            }
            Debug.Log("D2 FILM: frames" + names);

            Report();
            Checks(composed, room);

            UnityEngine.Object.DestroyImmediate(shot);
            UnityEngine.Object.DestroyImmediate(canvasGo);
            UnityEngine.Object.DestroyImmediate(camGo);
            RenderTexture.active = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(room);
            Debug.Log("D2 FILM OK");
        }

        /// The two things the film cannot show. Both are read off the objects
        /// themselves rather than asserted in prose. No coroutine is started, because
        /// edit mode has no clock to run one on.
        static void Checks(RawImage composed, Texture2D room)
        {
            string was = Environment.GetEnvironmentVariable(InkReveal.Switch);
            var sb = new StringBuilder("D2 FILM: checks");

            Environment.SetEnvironmentVariable(InkReveal.Switch, null);
            sb.Append("\n    kill-switch absent → Enabled=").Append(InkReveal.Enabled)
              .Append(InkReveal.Enabled ? "  ok" : "  *** WRONG ***");
            Environment.SetEnvironmentVariable(InkReveal.Switch, "1");
            sb.Append("\n    kill-switch \"1\"    → Enabled=").Append(InkReveal.Enabled)
              .Append(InkReveal.Enabled ? "  ok" : "  *** WRONG ***");
            Environment.SetEnvironmentVariable(InkReveal.Switch, "0");
            sb.Append("\n    kill-switch \"0\"    → Enabled=").Append(InkReveal.Enabled)
              .Append(InkReveal.Enabled ? "  *** WRONG ***" : "  ok");
            Environment.SetEnvironmentVariable(InkReveal.Switch, was);

            // a repaint landing on a reveal still in flight must not stack covers
            InkReveal.Attach(composed, room);
            InkReveal.Attach(composed, room);
            int covers = 0;
            for (int i = 0; i < composed.transform.childCount; i++)
                if (composed.transform.GetChild(i).name == InkReveal.CoverName) covers++;
            sb.Append("\n    two Attach calls  → covers on the room=").Append(covers)
              .Append(covers == 1 ? "  ok" : "  *** WRONG ***");
            Debug.Log(sb.ToString());
        }

        static void Shoot(Camera cam, RenderTexture rt, Texture2D into)
        {
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            into.ReadPixels(new UnityEngine.Rect(0f, 0f, rt.width, rt.height), 0, 0);
            into.Apply(false, false);
            RenderTexture.active = prev;
        }

        /// A render that never happened is a single flat colour. Say so loudly rather
        /// than shipping six identical rectangles as evidence.
        static bool Flat(Texture2D t)
        {
            Color32[] px = t.GetPixels32();
            byte lo = 255, hi = 0;
            for (int i = 0; i < px.Length; i += 37)
            {
                byte v = px[i].g;
                if (v < lo) lo = v;
                if (v > hi) hi = v;
            }
            return hi - lo < 8;
        }

        /// The numbers behind the film: how much cream is still on the glass at each
        /// of the twelve cuts, and where the last of it is.
        static void Report()
        {
            Texture2D[] masks = InkReveal.Masks();
            var sb = new StringBuilder("D2 FILM: cover remaining per step");
            for (int k = 0; k < masks.Length; k++)
            {
                Color32[] px = masks[k].GetPixels32();
                long sum = 0;
                for (int i = 0; i < px.Length; i++) sum += px[i].a;
                float pct = 100f * sum / (255f * px.Length);
                sb.Append("\n    step ").Append(k.ToString("00")).Append("  ")
                  .Append(pct.ToString("00.0")).Append("% still cream  ")
                  .Append(new string('#', Mathf.RoundToInt(pct / 2f)));
            }
            Debug.Log(sb.ToString());

            // where the last paint lands — a hole map at the second-to-last cut
            Color32[] late = masks[masks.Length - 3].GetPixels32();
            var map = new StringBuilder("D2 FILM: what is left at step "
                                        + (masks.Length - 3) + " (top row first)");
            for (int ry = 0; ry < 16; ry++)
            {
                map.Append("\n    ");
                for (int rx = 0; rx < 48; rx++)
                {
                    int x = rx * InkReveal.MaskW / 48;
                    int y = (15 - ry) * InkReveal.MaskH / 16;
                    map.Append(late[y * InkReveal.MaskW + x].a > 128 ? '#' : '.');
                }
            }
            Debug.Log(map.ToString());
        }

        static Texture2D LoadRoom()
        {
            for (int i = 0; i < RoomCandidates.Length; i++)
            {
                string p = Path.Combine(Application.dataPath, RoomCandidates[i]);
                if (!File.Exists(p)) continue;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(File.ReadAllBytes(p)))
                {
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.filterMode = FilterMode.Bilinear;
                    return tex;
                }
                UnityEngine.Object.DestroyImmediate(tex);
            }
            // no art on disk: a room-shaped test card, so the strokes are still legible
            var card = new Texture2D(384, 256, TextureFormat.RGBA32, false);
            var px = new Color32[384 * 256];
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 384; x++)
                {
                    bool tile = ((x / 32) + (y / 32)) % 2 == 0;
                    px[y * 384 + x] = tile
                        ? new Color32(40, 60, 90, 255)
                        : new Color32(220, 150, 70, 255);
                }
            card.SetPixels32(px);
            card.Apply(false, false);
            return card;
        }
    }
}
