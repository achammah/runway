using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Game;

namespace Runway.EditorTools
{
    /// <summary>
    /// THE SPOTLIT FOUNDER, PHOTOGRAPHED — the evidence for a live-play report of an
    /// empty cone on CHOOSE YOUR FOUNDER: the shadow was on the floor and nobody was
    /// standing on it.
    ///
    /// It builds the select stage's hero exactly as DraftSelectPage does (the same
    /// shadow at 465/742, the same 560x560 holder pivoted on the feet, the same
    /// DraftLoop.Attach) and drives the three orderings the decode queue can put a
    /// first frame in:
    ///
    ///   1 FIRST PLAY        cold cache, no runner at all — the ask must WAIT in the
    ///                       queue, not be answered "absent". Pumped by hand, the
    ///                       founder must appear.
    ///   2 RE-SELECT         the loop is destroyed and rebuilt, then plays the SAME
    ///                       founder with nothing pumped: the picture must come off
    ///                       the cache SYNCHRONOUSLY, inside Play.
    ///   3 DEAD TARGET       a play is issued, the loop is destroyed while the fetch
    ///                       is still queued, and a NEW loop asks for the same founder
    ///                       before the pump runs. The dead callback must not take the
    ///                       picture with it — the new loop must still be standing.
    ///
    /// and two the cache can poison itself with:
    ///
    ///   4 MISSING FILE      a path with nothing on disk stays remembered as absent
    ///   5 TRANSIENT MISS    a path whose file IS there is never remembered as absent
    ///
    /// Then it draws the founder's sheet (stat pips, trait pips, the LOCK IN card) so
    /// the inked pips, the measured rule under each trait name and the draft's own
    /// paper shadow can be looked at rather than asserted about.
    ///
    /// WHAT THE PIXEL COUNT MEANS. Each shot is diffed against a BASELINE frame of the
    /// same stage with the hero's image switched off, inside the hero's 560x560 box.
    /// The floor shadow is in both frames, so it cancels: what is counted is ink that
    /// is there because the founder is there. Under 20,000 of 313,600 is the empty
    /// cone the report is about.
    ///
    /// Runs headless WITH a graphics device (no `-nographics`, exactly like GlowShots):
    ///
    ///   Unity -batchmode -quit -projectPath unity \
    ///         -executeMethod Runway.EditorTools.DraftSelectProbe.Run
    ///
    /// Output goes to $RUNWAY_SELECT_OUT (default /tmp/d-select). Exits 1 on any failed
    /// check, so it is a gate and not only a report.
    ///
    /// WHAT IT CANNOT SEE: nothing here is playing, so DraftLoop's hydrator never runs
    /// (StartCoroutine is inert outside play mode) and only frame 01 is ever on screen
    /// — which is the frame the defect was about. DraftBreath's sway and the walk-on
    /// are not built either; this is the picture path, not the motion.
    /// </summary>
    public static class DraftSelectProbe
    {
        const int W = 1536;
        const int H = 1024;

        // DraftSelectPage's own geometry, transcribed
        const float HeroX = 335f, HeroY = 240f, HeroW = 560f, HeroH = 560f;
        const int InkFloor = 20000;

        static Camera _cam;
        static RenderTexture _rt;
        static RectTransform _stage;
        static GameObject _rig;
        static RectTransform _holder;
        static DraftLoop _hero;
        static Color32[] _baseline;

        static readonly StringBuilder _log = new StringBuilder();
        static int _checks, _fails;

        public static void Run()
        {
            string dir = Environment.GetEnvironmentVariable("RUNWAY_SELECT_OUT");
            if (string.IsNullOrEmpty(dir)) dir = "/tmp/d-select";
            Directory.CreateDirectory(dir);

            Say("RUNWAY! SELECT STAGE · the spotlit founder · "
                + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Say("unity " + Application.unityVersion + " · batchmode=" + Application.isBatchMode
                + " · graphics=" + SystemInfo.graphicsDeviceType);
            Say("art root: " + (RunwayPaths.ArtRoot.Length > 0 ? RunwayPaths.ArtRoot : "(NONE)"));
            Say("Boot.Instance: " + (Boot.Instance == null ? "null (no run — the transient case)" : "live"));
            Say("out: " + dir);
            Say("");

            try
            {
                BuildRig();
                Baseline(dir);
                One(dir);
                Two(dir);
                Three(dir);
                Poison();
                Sheet(dir);
            }
            catch (Exception e)
            {
                _checks++; _fails++;
                Say("FAILED WITH AN EXCEPTION: " + e);
            }
            finally
            {
                Teardown();
            }

            Say("");
            Say("SELECT DONE pass=" + (_checks - _fails) + " fail=" + _fails);
            try { File.WriteAllText(Path.Combine(dir, "measurements.txt"), _log.ToString()); }
            catch (Exception) { }
            EditorApplication.Exit(_fails == 0 ? 0 : 1);
        }

        // ══ the three orderings ════════════════════════════════════════════════

        /// The stage with nobody on it: the frame every other shot is measured against.
        static void Baseline(string dir)
        {
            NewHero();
            Canvas.ForceUpdateCanvases();
            Texture2D shot = Capture(Path.Combine(dir, "0-baseline-empty-cone.png"));
            _baseline = shot.GetPixels32();
            UnityEngine.Object.DestroyImmediate(shot);
            Say("0 · BASELINE — the cone with the shadow and nobody in it. Every count "
                + "below is ink this frame does NOT have.");
            Say("");
        }

        /// 1 · cold cache, no runner: the ask waits, the pump answers it, he appears.
        static void One(string dir)
        {
            const string id = "hacker";
            string path = "sprites/chr_loop_" + id + "_01.png";
            Truth("1 · the file is on disk: " + path, RunwayPaths.ArtExists(path));

            // WHICH CONTRACT IS BEING ASSERTED depends on where the picture lives.
            // A path with an imported mirror under Resources/Art is answered INSIDE
            // Load, on the frame it was asked for, and never sees the queue at all;
            // the streamed contract below still governs gen_scenes and every source
            // whose dimensions a compression pass has not reached yet, and 5 holds
            // it to that.
            bool baked = ArtCache.HasBaked(path);
            _hero.Play(id, "chr_arch_" + id);
            if (baked)
            {
                Say("   " + path + " is BAKED — Resources/Art answers it with no "
                    + "runner, no request and no decode.");
                Truth("1 · a baked founder is UP on the frame he was asked for, with "
                      + "nothing pumped", ArtCache.Peek(path) != null && Showing());
                Truth("1 · and he never joined the queue", ArtCache.Pending == 0);
            }
            else
            {
                Truth("1 · the ask was NOT answered 'absent' with no runner to fetch on "
                      + "(the poison that blanked a founder for a whole session)",
                      !ArtCache.Known(path));
                Truth("1 · nothing is up yet — the fetch is still queued", !Showing());
            }

            int fetched = ArtCache.PumpBlocking();
            Say("   pumped " + fetched + " picture(s) by hand" + Routes());
            Truth("1 · the picture is in the cache now", ArtCache.Known(path)
                  && ArtCache.Peek(path) != null);
            Truth("1 · the loop is showing it", Showing());
            Ink("1-first-play", dir, "1 · FIRST PLAY, cold cache, no runner");
        }

        /// 2 · a re-select: destroy the loop, build a new one, play the same founder.
        /// Nothing is pumped — the cache must answer inside Play.
        static void Two(string dir)
        {
            const string id = "hacker";
            NewHero();
            Truth("2 · the new loop starts blank", !Showing());

            _hero.Play(id, "chr_arch_" + id);
            Truth("2 · the rebuilt loop is showing the founder with NOTHING pumped — "
                  + "the cache answered inside Play", Showing());
            Truth("2 · and it did not need the queue", ArtCache.Pending == 0);
            Ink("2-after-reselect", dir, "2 · RE-SELECT, the same founder on a new loop");
        }

        /// 3 · the dead target: the loop that asked is destroyed while its fetch is
        /// still queued, and its replacement asks for the same founder first.
        static void Three(string dir)
        {
            const string id = "dropout";
            string path = "sprites/chr_loop_" + id + "_01.png";
            Truth("3 · a founder nobody has asked for yet", !ArtCache.Known(path));

            if (ArtCache.HasBaked(path))
            {
                // THE RACE CANNOT HAPPEN TO A BAKED PATH. There is no window between
                // the ask and the answer for a destroyed loop's callback to fall
                // into, because there is no queue.
                _hero.Play(id, "chr_arch_" + id);
                Truth("3 · a baked founder is answered inside Play, so no fetch is "
                      + "left queued for a dead loop to outlive",
                      ArtCache.Pending == 0 && Showing());
                NewHero();
                _hero.Play(id, "chr_arch_" + id);
                Truth("3 · and the replacement loop is standing", Showing());
                Ink("3-dead-target", dir, "3 · DEAD TARGET, the replacement loop takes the frame");
                return;
            }

            _hero.Play(id, "chr_arch_" + id);            // queued, not fetched
            Truth("3 · queued", ArtCache.Pending > 0);
            NewHero();                                    // the loop that asked is gone
            _hero.Play(id, "chr_arch_" + id);             // the new one asks too
            Truth("3 · still one fetch for the two asks", ArtCache.Pending == 1);

            int fetched = ArtCache.PumpBlocking();
            Say("   pumped " + fetched + " picture(s); the dead callback ran first" + Routes());
            Truth("3 · the dead callback did not take the picture with it — "
                  + "the new loop is standing", Showing());
            Ink("3-dead-target", dir, "3 · DEAD TARGET, the replacement loop takes the frame");
        }

        /// 4 and 5 · what may and may not be remembered as absent.
        static void Poison()
        {
            const string ghost = "sprites/chr_loop_nobody_at_all_01.png";
            bool answered = false;
            Texture2D got = null;
            ArtCache.Load(ghost, t => { answered = true; got = t; });
            Truth("4 · a path with nothing on disk answers at once", answered && got == null);
            Truth("4 · and is remembered as absent, so the drawn fallback is permanent",
                  ArtCache.Known(ghost));

            // 5 · the transient case is what phase 1 ran under: no runner, a real file.
            // It is a rule about the STREAMED path, so it needs a path that is still
            // streamed — a baked one is answered inside Load, by design.
            string real = FirstStreamed(new[]
            {
                "sprites/chr_loop_exfaang_01.png",
                "sprites/env_bed.png",
                "sprites/founder.png",
                "title/layers/type_main.png",
            });
            if (real == null)
            {
                Say("5 · skipped — every candidate is baked now. The rule it guards "
                    + "still governs the streamed path, which gen_scenes takes.");
                return;
            }
            Say("5 · held against " + real + ", which still streams");
            bool tooSoon = false;
            ArtCache.Load(real, t => { tooSoon = true; });
            Truth("5 · a file that IS on disk is not answered 'absent' just because "
                  + "there is nothing to fetch on yet", !tooSoon && !ArtCache.Known(real));
            ArtCache.PumpBlocking();
            Truth("5 · and it lands the moment the queue is pumped",
                  ArtCache.Known(real) && ArtCache.Peek(real) != null);
        }

        // ══ the sheet ══════════════════════════════════════════════════════════

        /// The founder's card, so the two ledgers and the paper can be LOOKED at: five
        /// stat rows, six trait rows with their inked pips and measured rules, three of
        /// them carrying a bag delta, and the LOCK IN card in the draft's own paper.
        static void Sheet(string dir)
        {
            var panel = GameUi.PaperSheet(_stage, 936f, 72f, 540f,
                FounderDraftScreen.SheetBottomMax - 72f, 1, 4f, null, "sheet");
            GameUi.Tilt(panel, -0.008f);
            DrawnUI.HandLabel(panel, "THE GARAGE HACKER", 44f, 20f, 46f, DrawnUI.Ink, 470f);

            var stats = new Dictionary<string, int>
            {
                { "build", 5 }, { "sell", 1 }, { "raise", 2 }, { "recruit", 2 }, { "grit", 4 },
            };
            GameUi.StatPips(panel, 44f, 172f, stats,
                            FounderDraftScreen.StatNames, FounderDraftScreen.StatLabels);

            DrawnUI.HandLabel(panel, "HIDDEN TRAITS", 44f, 434f, 22f,
                              DrawnUI.WithAlpha(DrawnUI.Ink, 0.62f));
            var traits = new Dictionary<string, int>();
            var deltas = new Dictionary<string, int>();
            for (int i = 0; i < Runway.Core.GameState.TRAIT_NAMES.Count; i++)
            {
                string t = Runway.Core.GameState.TRAIT_NAMES[i];
                traits[t] = 1 + (i % 5);
                if (i == 0) deltas[t] = 1;
                if (i == 2) deltas[t] = -1;
                if (i == 4) deltas[t] = 2;
            }
            GameUi.TraitPips(panel, 44f, 462f, traits, Runway.Core.GameState.TRAIT_NAMES, deltas);

            DrawnUI.PaperButton(_stage, "LOCK IN  →", 1230f, 880f, 260f, 84f, 36f,
                                DrawnUI.Ink, DrawnUI.CoralDark, null, 1.045f,
                                GameUi.DraftPaper(1230 % 5));
            DrawnUI.PaperButton(_stage, "TITLE-SCREEN CARD", 120f, 880f, 380f, 84f, 30f,
                                DrawnUI.Ink, DrawnUI.CoralDark, null, 1.045f,
                                DrawnUI.PaperStyle.Button);

            Canvas.ForceUpdateCanvases();
            Texture2D shot = Capture(Path.Combine(dir, "4-sheet-and-paper.png"));
            UnityEngine.Object.DestroyImmediate(shot);
            Say("");
            Say("4 · THE SHEET — every trait pip now carries its own ink border (on: "
                + "coral + ink at 1.6; off: ink at 0.07 filled, ink at 0.30 at 1.4) and "
                + "the rule under each name is the width the FONT makes the word, not "
                + "eleven pixels a letter. Three rows are given a bag delta HERE ONLY: "
                + "the widget draws it, and nothing on the draft asks for it — the "
                + "founder is picked five pages before anything is packed, which is "
                + "what the original does too. Bottom of the frame: the draft's LOCK IN "
                + "card beside the title screen's card, so the two shadows can be told "
                + "apart — (7, 9) at 0.18 against (4, 5) at 0.35.");
        }

        // ══ the rig ════════════════════════════════════════════════════════════

        static void BuildRig()
        {
            _rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            _rt.Create();

            _rig = new GameObject("~selectrig");
            _rig.hideFlags = HideFlags.HideAndDontSave;

            var camGo = new GameObject("~selectcam", typeof(Camera));
            camGo.transform.SetParent(_rig.transform, false);
            _cam = camGo.GetComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = 5f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = GameUi.Night;
            _cam.targetTexture = _rt;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            var canvasGo = new GameObject("~selectcanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(_rig.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _cam;
            canvas.planeDistance = 1f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            var stageGo = new GameObject("stage", typeof(RectTransform));
            _stage = stageGo.GetComponent<RectTransform>();
            _stage.SetParent(canvasGo.transform, false);
            _stage.anchorMin = new Vector2(0f, 1f);
            _stage.anchorMax = new Vector2(0f, 1f);
            _stage.pivot = new Vector2(0f, 1f);
            _stage.sizeDelta = new Vector2(W, H);
            _stage.anchoredPosition = Vector2.zero;

            // the night the draft can never fall through to a blank screen behind, and
            // the procedural cone the stage falls back to when env/stage.png is absent
            DrawnUI.FullFill(_stage, "night", GameUi.Night, true);
            for (int i = 0; i < 14; i++)
            {
                float k = i / 13f;
                float cw = Mathf.Lerp(W * 0.16f, W * 0.44f, k);
                DrawnUI.Fill(_stage, "cone", DrawnUI.WithAlpha(DrawnUI.Cream, 0.012f),
                             (W - cw) * 0.5f, H * 0.86f * k, cw, H * 0.86f / 14f + 1f);
            }
            GameUi.Shadow(_stage, 465f, 742f, 300f, 46f);
        }

        /// A fresh hero holder and loop, exactly as DraftSelectPage.Build makes them.
        /// The old one is destroyed FIRST — that is the re-select this probe is about.
        static void NewHero()
        {
            if (_holder != null) UnityEngine.Object.DestroyImmediate(_holder.gameObject);
            _holder = DrawnUI.Rect(_stage, "heroholder", HeroX, HeroY, HeroW, HeroH);
            _holder.pivot = new Vector2(0.5f, 0f);
            _holder.anchoredPosition = new Vector2(HeroX + HeroW * 0.5f, -(HeroY + HeroH));
            _hero = DraftLoop.Attach(_holder, "hero", 0f, 0f, HeroW, HeroH);
        }

        /// The first candidate that is on disk and has NO imported mirror behind it.
        /// Null when every one of them is baked, which is a valid answer and not a
        /// failure: it means the streamed path's remaining tenant is gen_scenes.
        static string FirstStreamed(string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
                if (RunwayPaths.ArtExists(candidates[i]) && !ArtCache.HasBaked(candidates[i]))
                    return candidates[i];
            return null;
        }

        static bool Showing()
        {
            if (_holder == null) return false;
            var img = _holder.GetComponentInChildren<RawImage>(true);
            return img != null && img.enabled && img.texture != null;
        }

        static void Teardown()
        {
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { RenderTexture.active = null; _rt.Release(); UnityEngine.Object.DestroyImmediate(_rt); }
            if (_rig != null) UnityEngine.Object.DestroyImmediate(_rig);
        }

        // ══ the picture and the count ══════════════════════════════════════════

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
            Debug.Log("SELECT SHOT: " + path);
            return shot;
        }

        /// Shoot, count what stands in the hero's box that the empty cone did not have,
        /// and assert it is a founder rather than a gap.
        static void Ink(string name, string dir, string title)
        {
            Texture2D shot = Capture(Path.Combine(dir, name + ".png"));
            int ink = Differs(shot.GetPixels32(), HeroX, HeroY, HeroW, HeroH);
            UnityEngine.Object.DestroyImmediate(shot);
            Say("   " + title + " → " + ink + " ink px in the hero box "
                + "(" + (ink * 100f / (HeroW * HeroH)).ToString("0.0") + "% of "
                + (HeroW * HeroH) + ", floor " + InkFloor + ")");
            Truth(title + " puts a founder in the cone", ink > InkFloor);
        }

        /// Pixels inside a top-left stage box that differ from the empty-cone frame.
        /// The floor shadow is in both, so it cancels out of the count.
        static int Differs(Color32[] shot, float x, float y, float w, float h)
        {
            if (_baseline == null) return 0;
            int x0 = Mathf.Clamp(Mathf.RoundToInt(x), 0, W);
            int x1 = Mathf.Clamp(Mathf.RoundToInt(x + w), 0, W);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(y), 0, H);
            int y1 = Mathf.Clamp(Mathf.RoundToInt(y + h), 0, H);
            int n = 0;
            for (int py = y0; py < y1; py++)
            {
                int row = (H - 1 - py) * W;      // the shots are stored bottom-up
                for (int px = x0; px < x1; px++)
                {
                    int i = row + px;
                    Color32 a = _baseline[i], b = shot[i];
                    if (Math.Abs(a.r - b.r) > 6 || Math.Abs(a.g - b.g) > 6
                        || Math.Abs(a.b - b.b) > 6) n++;
                }
            }
            return n;
        }

        // ══ the paperwork ══════════════════════════════════════════════════════

        /// Which door the pictures came through — the shipped UnityWebRequest one when
        /// anything is pumping the update loop, the editor's own byte read when nothing
        /// is. Everything else on the path is the same either way.
        static string Routes()
        {
            return "  (route: " + ArtCache.BakedRoute + " baked, " + ArtCache.WebRoute
                   + " web, " + ArtCache.DiskRoute + " disk)";
        }

        static void Truth(string what, bool ok)
        {
            _checks++;
            if (!ok) _fails++;
            Say((ok ? "   ok   " : "   FAIL ") + what);
        }

        static void Say(string line)
        {
            Debug.Log("SELECT: " + line);
            _log.Append(line).Append('\n');
        }
    }
}
