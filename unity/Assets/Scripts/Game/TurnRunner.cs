using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Runway.App;
using Runway.Core;
using Runway.Llm;

namespace Runway.Game
{
    /// <summary>
    /// THE GENERATIVE WEEK — main.gd's turn, ported whole.
    ///
    ///   the player writes a move  ─> the DM answers ONCE with both halves of the week:
    ///                                the consequence text AND the scene to build
    ///   the player locks the week ─> THE ART STARTS THIS INSTANT
    ///                             ─> the reading beat opens over the room and the wait
    ///                                is spent on the most interesting thing in the game
    ///   the scene lands           ─> the room opens
    ///
    /// WHAT HAPPENS WHEN THE NETWORK DOES NOT COOPERATE — the whole point of the rules
    /// below, because a render failure must cost a picture and never a turn:
    ///   · the DM is off or errors   → no scene facets, no beat: the week runs untouched
    ///   · the compose fails         → the PREVIOUS room stays and the week continues
    ///   · the render hangs          → the beat closes at HoldCeiling regardless
    ///   · it lands after that       → it becomes the room when the player is back in it
    ///   · the run ends mid-render   → the turn is cancelled and nothing can reach the screen
    /// Nothing on this path waits on a network call without a deadline.
    ///
    /// It lives on Boot's own object, not on a screen, because it has to outlive every
    /// screen swap the week performs.
    /// </summary>
    public sealed class TurnRunner : MonoBehaviour
    {
        /// The beat never holds a reader longer than this, whatever the render is doing.
        public const float HoldCeiling = 150f;

        static TurnRunner _instance;

        public static TurnRunner Get()
        {
            if (_instance != null) return _instance;
            var boot = Boot.Instance;
            if (boot == null) return null;
            _instance = boot.gameObject.AddComponent<TurnRunner>();
            return _instance;
        }

        /// The room currently on stage, when there is one. The turn hands it the week's
        /// painting instead of throwing a picture the player paid for away.
        public static GarageScreen Room;

        public bool TurnBusy { get; private set; }
        public string ScenePath { get; private set; }
        public float SceneProgress { get; private set; }

        bool _sceneDone;
        int _turnSeq;
        string _sceneHeadline = "";
        string _lastStageSig = "";
        ReadingBeat _beat;
        DiceRoll _cup;
        RectTransform _sceneLayer;
        bool _wired;

        RunDriver Driver { get { return RunDriver.Current; } }
        GameState State { get { return RunDriver.Current != null ? RunDriver.Current.State : null; } }

        void Awake() { Wire(); }

        void Wire()
        {
            if (_wired) return;
            var d = Boot.Instance != null ? Boot.Instance.Director : null;
            if (d == null) return;
            _wired = true;
            d.Progress += OnSceneProgress;
            d.Ready += OnSceneReady;
            d.Failed += OnSceneFailed;
        }

        // ══ the curtain and the cup ════════════════════════════════════════════

        /// The lock answers INSTANTLY: the theater curtain drops the moment the week
        /// commits, the world thinks behind it, and it rises on the reading beat. The
        /// CEREMONY OUTRANKS THE CURTAIN — a curtain raised above the cup played the
        /// whole roll invisibly, so it only claims the top once the die has settled.
        public void DropCurtain(string line)
        {
            var boot = Boot.Instance;
            if (boot == null || boot.Curtain == null) return;
            StartCoroutine(DropRoutine(line));
        }

        IEnumerator DropRoutine(string line)
        {
            var boot = Boot.Instance;
            Curtain c = boot.Curtain;
            if (_cup != null)
            {
                _cup.transform.SetAsLastSibling();
                while (_cup != null && !_cup.Settled) yield return null;
                yield return new WaitForSecondsRealtime(0.15f);
            }
            c.transform.SetAsLastSibling();
            c.ConsideringLine = string.IsNullOrEmpty(line)
                ? "the world considers your week…" : line;
            yield return c.Close();
            // THE HANG FAILSAFE. 40s only catches a truly dead network; a week still
            // mid-adjudication keeps the curtain down however long it takes.
            float waited = 0f;
            while (waited < 40f) { waited += Time.unscaledDeltaTime; yield return null; }
            bool thinking = Room != null && Room.Adjudicating;
            if (!TurnBusy && !thinking && c.IsShut)
            {
                Debug.LogWarning("RUNWAY! curtain failsafe: nothing arrived in 40s, opening");
                yield return c.Open();
            }
        }

        public void RaiseCurtain()
        {
            var boot = Boot.Instance;
            if (boot == null || boot.Curtain == null) return;
            if (boot.Curtain.IsShut || boot.Curtain.Shut > 0.01f)
                StartCoroutine(boot.Curtain.Open());
        }

        /// The table roll: the pre-rendered cup-and-die clip for the rolled number
        /// plays ON the room, then the curtain falls. The DM is already thinking while
        /// the die tumbles, so the ceremony costs no extra wait.
        public void ShowDie(int n)
        {
            if (_cup != null) return;
            var boot = Boot.Instance;
            if (boot == null) return;
            _cup = DiceRoll.Create(boot.TopLayer, n);
            _cup.Finished += () =>
            {
                if (_cup != null) Destroy(_cup.gameObject);
                _cup = null;
            };
        }

        // ══ the turn ═══════════════════════════════════════════════════════════

        public void BeginTurn(JObject dm)
        {
            if (dm == null) return;
            Wire();
            StartCoroutine(TurnRoutine(dm));
        }

        IEnumerator TurnRoutine(JObject dm)
        {
            var boot = Boot.Instance;
            var scene = dm["scene"] as JObject;
            string narration = ContentDb.Str(dm, "narration").Trim();
            bool wantArt = boot != null && boot.ArtEnabled && scene != null && scene.Count > 0;

            Debug.Log(string.Format("TURN wk{0:00}: beat opens · narration {1} chars · art {2} · place {3}",
                State != null ? State.Week : -1, narration.Length,
                wantArt ? "ON" : "off", ContentDb.Str(scene, "place", "-")));
            if (narration.Length == 0 && !wantArt) yield break;

            TurnBusy = true;
            if (Room != null) Room.WorldBusy = true;
            int seq = _turnSeq;
            ScenePath = "";
            _sceneDone = false;
            SceneProgress = 0f;
            _sceneHeadline = ContentDb.Str(dm, "headline");

            // CHANGE-BEATS: a fresh image is generated when the WEEK'S STAGE changes —
            // new place, new condition. A quiet week in the same room keeps its scene.
            string stageSig = string.Format("wk{0}|{1}|{2}",
                State != null ? State.Week : 0,
                ContentDb.Str(scene, "novel_place", ContentDb.Str(scene, "place")),
                ContentDb.Str(scene, "condition", "steady"));
            if (wantArt && stageSig == _lastStageSig && _sceneLayer == null) wantArt = false;

            if (!wantArt)
            {
                _sceneDone = true;      // nothing is coming; the beat is pure reading
            }
            else
            {
                _lastStageSig = stageSig;
                string outName = string.Format("run{0}_wk{1:00}",
                    Driver != null && Driver.Record != null ? Driver.Record.SeedValue : 0,
                    State != null ? State.Week : 0);
                boot.Director.MakeSceneV2(scene, Driver.CastRoster(dm["cast"] as JArray),
                    new string[0], ContentDb.Str(scene, "beat"), outName, Driver.CompanyCtx());
            }

            // BOOK-READ TURNS (the founding, read on the book screen) skip the beat
            // entirely: the words WERE the screen; only the art still lands.
            if (ContentDb.Flag(dm, "book_read"))
            {
                RaiseCurtain();
                bool painting = boot.Director.WarmStatus == PaintStatus.Painting;
                if (painting && Room != null) Room.SetPainting(true);
                TurnBusy = false;
                if (Room != null) Room.WorldBusy = false;
                StartCoroutine(SilentWatch(seq, dm));      // the wait watches from the side
                if (Driver != null) Driver.CheckExit();
                yield break;
            }

            // ── the reading beat ──────────────────────────────────────────────
            var beat = ReadingBeat.Create(boot.TopLayer);
            _beat = beat;
            int wkp = ContentDb.Int(dm, "week_played", State != null ? State.Week : 1);
            beat.Begin(wkp <= 0 ? "DAY ONE" : "WEEK " + wkp);
            beat.Say("", ContentDb.Str(dm, "headline"));
            if (ContentDb.Str(dm, "player_text").Length > 0)
            {
                beat.Say("You said", ContentDb.Str(dm, "player_text"));
                beat.Say("They heard", ContentDb.Str(dm, "interpreted_as"));
            }
            // THE JUDGEMENT: the settled die meets its DC before anything else — the
            // player watches the number they rolled become the week they got.
            var rollD = dm["roll"] as JObject;
            var diceD = dm["dice"] as JObject;
            int usedD20 = ContentDb.Int(diceD, "used", 0);
            if (usedD20 > 0 && rollD != null && rollD.Count > 0)
            {
                string stt = ContentDb.Str(diceD, "stat", "grit");
                int mod = ContentDb.Int(diceD, "mod", 0);
                string mode = ContentDb.Str(diceD, "mode");
                int dc = ContentDb.Int(rollD, "dc", 10);
                int total = usedD20 + mod;
                string band = SimEngine.MarginBand(total, dc);
                string plain = "It lands.";
                if (band == "brilliant") plain = "It lands beautifully.";
                else if (band == "risky") plain = "It half-lands: something gives.";
                else if (band == "backfired") plain = "It goes wrong.";
                beat.Say("", string.Format(
                    "The die came up {0}. Your {1} adds {2}{3} — total {4}, and this needed {5}. {6}{7}",
                    usedD20, stt, mod >= 0 ? "+" : "−", Mathf.Abs(mod), total, dc, plain,
                    mode.Length > 0 ? "  ·  " + mode : ""));
            }
            beat.Say("", ContentDb.Str(dm, "narration"));
            beat.Say("", ContentDb.Str(dm, "reality_check"));

            // THE Z-ORDER OF THE TURN: beat under curtain under cup.
            if (boot.Curtain != null && boot.Curtain.IsShut)
            {
                boot.Curtain.transform.SetAsLastSibling();
                if (_cup != null)
                {
                    _cup.transform.SetAsLastSibling();
                    while (_cup != null && !_cup.Settled) yield return null;
                }
                yield return new WaitForSecondsRealtime(usedD20 > 0 ? 0.9f : 0.65f);
            }
            RaiseCurtain();
            // D4: a backfired week flinches the frame, once, after the sweep.
            // Impulse.Verdict is a no-op for every other band.
            yield return new WaitForSecondsRealtime(0.55f);
            Runway.Effects.Impulse.Verdict(ContentDb.Str(dm, "verdict"));

            // THE BOUNDED WAIT. The pen tracks the real render; the deadline is what
            // makes a hung request impossible to be stranded by.
            float deadline = Time.realtimeSinceStartup + HoldCeiling;
            while (!_sceneDone && Time.realtimeSinceStartup < deadline && seq == _turnSeq)
            {
                beat.Report(SceneProgress);
                yield return null;
            }
            if (seq != _turnSeq) yield break;      // the run ended under us
            yield return beat.Finish();            // drains the reading, then fades
            _beat = null;
            if (seq != _turnSeq) yield break;
            TurnBusy = false;
            if (Room != null) Room.WorldBusy = false;
            if (ScenePath.Length > 0) OpenScene(ScenePath, ContentDb.Str(dm, "headline"));
            if (Driver != null) Driver.CheckExit();
        }

        /// THE SILENT PATH NEVER BLOCKS: the world is released immediately and this
        /// watches the paint from the side, opening the room whenever it lands.
        IEnumerator SilentWatch(int seq, JObject dm)
        {
            float deadline = Time.realtimeSinceStartup + HoldCeiling;
            while (!_sceneDone && Time.realtimeSinceStartup < deadline && seq == _turnSeq)
                yield return null;
            if (seq != _turnSeq) yield break;
            if (Room != null) Room.SetPainting(false);
            if (ScenePath.Length > 0) OpenScene(ScenePath, ContentDb.Str(dm, "headline"));
        }

        // ══ the director's answers ═════════════════════════════════════════════

        /// Progress never runs backwards: a pen that goes back down reads as a bug.
        void OnSceneProgress(float f)
        {
            SceneProgress = Mathf.Max(SceneProgress, Mathf.Clamp01(f));
        }

        void OnSceneReady(string path)
        {
            if (Driver != null) Driver.NotifyPaintSettled();
            Debug.Log("TURN art landed: " + path);
            ScenePath = path;
            _sceneDone = true;
            if (!TurnBusy) LateScene(path);
        }

        /// A FAILED RENDER IS A COSMETIC LOSS. The previous room stays, the week
        /// continues, and the only trace is a line in the log.
        void OnSceneFailed(string reason)
        {
            if (Driver != null) Driver.NotifyPaintSettled();
            Debug.Log("TURN art FAILED: " + reason + " — keeping the previous room");
            ScenePath = "";
            _sceneDone = true;
        }

        /// The render came in after the beat closed. Handing it to the room is never an
        /// interruption — it just becomes the room, even behind an open book.
        void LateScene(string path)
        {
            if (Room == null || State == null || State.Dead) return;
            if (Room.AdoptComposed(path, false)) return;
            if (_sceneLayer != null) return;
            if (Room.JournalOpen)
            {
                Debug.Log("RUNWAY! scene arrived late while the book was open — dropped");
                return;
            }
            OpenScene(path, _sceneHeadline);
        }

        /// NOTHING IN FLIGHT SURVIVES THE END OF A RUN.
        public void CancelTurn()
        {
            _turnSeq++;
            TurnBusy = false;
            if (Room != null) Room.WorldBusy = false;
            ScenePath = "";
            _sceneHeadline = "";
            _sceneDone = true;
            if (Boot.Instance != null && Boot.Instance.Director != null)
                Boot.Instance.Director.CancelTurn();
            if (_beat != null) { _beat.Dismiss(); _beat = null; }
            CloseScene();
        }

        // ══ the room you are looking at ════════════════════════════════════════

        /// THE BOUNDARY: the room screen owns what the room looks like, so the week's
        /// scene is handed to it and BECOMES the room. The overlay below is the
        /// fallback for anywhere else.
        public void OpenScene(string path, string headline)
        {
            if (Room != null && Room.AdoptComposed(path, false)) return;
            var boot = Boot.Instance;
            if (boot == null) return;
            CloseScene();
            var layer = DrawnUI.FullRect(boot.TopLayer, "scene");
            _sceneLayer = layer;
            var back = DrawnUI.FullFill(layer, "back", new Color(0.06f, 0.05f, 0.07f, 1f), true);
            back.raycastTarget = true;
            var shot = layer.gameObject.AddComponent<UnityEngine.UI.Button>();
            shot.transition = UnityEngine.UI.Selectable.Transition.None;
            shot.targetGraphic = back;
            shot.onClick.AddListener(CloseScene);

            var picRt = DrawnUI.FullRect(layer, "picture");
            var pic = picRt.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            pic.raycastTarget = false;
            pic.enabled = false;
            Boot.Instance.StartCoroutine(SheetLoop.LoadTexture(RunwayPaths.FileUrl(path), tex =>
            {
                if (pic == null || tex == null) return;
                pic.texture = tex;
                pic.enabled = true;
            }));

            if (!string.IsNullOrEmpty(headline) && headline.Trim().Length > 0)
                DrawnUI.HandLabel(layer, headline, RunwayPaths.StageWidth * 0.12f, 34f, 44f,
                    DrawnUI.Cream, RunwayPaths.StageWidth * 0.76f,
                    TMPro.TextAlignmentOptions.Top);
            DrawnUI.HandLabel(layer, "click anywhere to get on with the week",
                RunwayPaths.StageWidth * 0.12f, RunwayPaths.StageHeight - 74f, 26f,
                DrawnUI.WithAlpha(DrawnUI.Cream, 0.55f), RunwayPaths.StageWidth * 0.76f,
                TMPro.TextAlignmentOptions.Top);

            var g = DrawnUI.Group(layer);
            g.alpha = 0f;
            StartCoroutine(DrawnUI.FadeTo(g, 1f, 0.35f));
        }

        public void CloseScene()
        {
            if (_sceneLayer == null) return;
            RectTransform l = _sceneLayer;
            _sceneLayer = null;
            StartCoroutine(FadeAndDrop(l));
        }

        IEnumerator FadeAndDrop(RectTransform l)
        {
            var g = DrawnUI.Group(l);
            yield return DrawnUI.FadeTo(g, 0f, 0.25f);
            if (l != null) Destroy(l.gameObject);
        }
    }
}
