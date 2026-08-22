using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Runway.Llm;

namespace Runway.App
{
    /// <summary>
    /// MAIN — the port of main.gd's boot and its screen flow.
    ///
    /// ONE SCENE, NO PREFABS. The Godot game already constructs every screen in code,
    /// so this build needs no scene file at all: a RuntimeInitializeOnLoadMethod raises
    /// the camera, the canvas and the stage, and the flow takes it from there.
    ///
    /// THE FLOW, exactly as main.gd walks it:
    ///
    ///     boot ─┬─ harness / RUNWAY_FIRSTFLOW ─────────────────────► TITLE
    ///           ├─ no key AND no keys.env ──────────► KEYS ─(saved)─► (re-gate)
    ///           └─ otherwise ───────────────► STUDIO CARD ─(done)──► TITLE
    ///
    ///     TITLE ─┬─ any key (harness) ────────────────────────► start run
    ///            ├─ NEW GAME  → slot table → slot ─► clear ──► start run
    ///            └─ CONTINUE  → slot table → slot ─────────► start run
    ///
    ///     start run ─┬─ a save in the active slot ─────────────────► GARAGE
    ///                ├─ never seen the rules ─► HOW TO ─(got it)──► DRAFT
    ///                └─ otherwise ────────────────────────────────► DRAFT
    ///
    ///     DRAFT ─(launch)─► apply the draft ─► BIRTH ─(bible, ceiling 25s)─►
    ///            BOOK ─(settle in)─► GARAGE ─► cold open (day one)
    ///
    /// THE STAGE IS 1536x1024 AND FIXED. Godot stretches canvas_items with aspect
    /// "keep"; here a fixed-size stage sits centred in the canvas and the camera's
    /// clear colour fills whatever is left, which letterboxes the same way AND lets
    /// every screen keep the original's coordinates unchanged.
    /// </summary>
    public sealed class Boot : MonoBehaviour
    {
        public static Boot Instance { get; private set; }

        /// Set this before the first frame to install the run lane without editing Boot.
        public static IRunDriver PendingDriver;

        // ── the furniture ──────────────────────────────────────────────────────
        public Camera Cam { get; private set; }
        public Canvas UiCanvas { get; private set; }
        public RectTransform Stage { get; private set; }
        public RectTransform ScreenLayer { get; private set; }
        public RectTransform OverlayLayer { get; private set; }
        public RectTransform TopLayer { get; private set; }

        // ── the services ───────────────────────────────────────────────────────
        public LlmClient Llm { get; private set; }
        public EventGenerator Generator { get; private set; }
        public SceneDirector Director { get; private set; }

        IRunDriver _driver;
        public IRunDriver Driver
        {
            get { return _driver ?? (_driver = new NullRunDriver()); }
            set { _driver = value; }
        }

        public AppState State { get; private set; }
        public AppScreen CurrentScreen { get; private set; }
        public Curtain Curtain { get; private set; }

        /// KEY_D on the title: an authored-only, date-seeded run.
        public bool DailyMode { get; private set; }

        // ══ entry point ════════════════════════════════════════════════════════

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (Instance != null) return;
            var go = new GameObject("RUNWAY!");
            DontDestroyOnLoad(go);
            go.AddComponent<Boot>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (PendingDriver != null) { _driver = PendingDriver; PendingDriver = null; }

            // NOTHING IN THIS GAME IS DRAWN FASTER THAN 12fps (project.godot: max_fps=30)
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;

            BuildFurniture();
            BuildServices();
            StartCoroutine(BootFlow());
        }

        void BuildFurniture()
        {
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.transform.SetParent(transform, false);
            camGo.tag = "MainCamera";
            Cam = camGo.GetComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = DrawnUI.Stage;   // project.godot default_clear_color
            Cam.orthographic = true;
            Cam.orthographicSize = RunwayPaths.StageHeight * 0.5f;
            Cam.cullingMask = 0;                   // the canvas is an overlay; nothing 3D

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler),
                                          typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            UiCanvas = canvasGo.GetComponent<Canvas>();
            UiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UiCanvas.pixelPerfect = false;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RunwayPaths.StageWidth, RunwayPaths.StageHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // aspect "keep"
            scaler.referencePixelsPerUnit = 100f;

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem),
                                        typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }

            var stageGo = new GameObject("Stage", typeof(RectTransform));
            Stage = stageGo.GetComponent<RectTransform>();
            Stage.SetParent(UiCanvas.transform, false);
            Stage.anchorMin = new Vector2(0.5f, 0.5f);
            Stage.anchorMax = new Vector2(0.5f, 0.5f);
            Stage.pivot = new Vector2(0.5f, 0.5f);
            Stage.sizeDelta = new Vector2(RunwayPaths.StageWidth, RunwayPaths.StageHeight);
            Stage.anchoredPosition = Vector2.zero;

            ScreenLayer = DrawnUI.FullRect(Stage, "screens");
            OverlayLayer = DrawnUI.FullRect(Stage, "overlays");
            TopLayer = DrawnUI.FullRect(Stage, "top");   // curtain, cup — always last
        }

        void BuildServices()
        {
            var svc = new GameObject("services");
            svc.transform.SetParent(transform, false);

            Llm = svc.AddComponent<LlmClient>();
            Llm.Setup(Env.Load());

            Generator = svc.AddComponent<EventGenerator>();
            Generator.Setup(Llm);

            Director = svc.AddComponent<SceneDirector>();
            Director.Setup();

            Curtain = Runway.App.Curtain.Create(TopLayer);
        }

        // ══ the gate ═══════════════════════════════════════════════════════════

        IEnumerator BootFlow()
        {
            yield return null;   // one frame so the canvas has a size before anything measures

            BuildStamp.PrintOnce();
            Debug.Log(string.Format("RUNWAY! LLM: {0} · art {1} · stage {2}x{3}",
                Llm.Enabled ? (Llm.Provider + "/" + Llm.Model) : "off (authored only)",
                ArtEnabled ? "on" : "off",
                RunwayPaths.StageWidth, RunwayPaths.StageHeight));
            if (_driver == null)
                Debug.LogWarning("RUNWAY! no IRunDriver registered — the flow walks, the run does not. "
                                 + "Set Boot.PendingDriver or Boot.Instance.Driver.");

            // THE STUDIO CARD (owner): a real release opens on its studio, then the
            // title. Harnesses skip straight to work.
            if (Harness || Env.Flag("RUNWAY_FIRSTFLOW"))
            {
                ToTitle();
            }
            else if (!Llm.Enabled && !Env.KeysFileExists)
            {
                Go(AppState.Keys, null, s => { s.Done += _ => OnKeysSaved(); });
            }
            else
            {
                Go(AppState.StudioCard, null, s => { s.Done += _ => ToTitle(); });
            }
        }

        /// keys.env just changed — re-layer the env and re-set up the client, wherever
        /// the keys screen was opened from. Godot reloads the whole scene for this.
        public void NotifyKeysChanged()
        {
            Llm.Setup(Env.Reload());
            Debug.Log("RUNWAY! keys saved · LLM: "
                      + (Llm.Enabled ? (Llm.Provider + "/" + Llm.Model) : "off (authored only)"));
        }

        /// The boot gate's keys screen answered: on to the studio card, then the title.
        void OnKeysSaved()
        {
            Go(AppState.StudioCard, null, s => { s.Done += _ => ToTitle(); });
        }

        // ══ the flow ═══════════════════════════════════════════════════════════

        public void ToTitle()
        {
            Director.CancelTurn();
            var title = Go(AppState.Title, null, s =>
            {
                s.Done += OnTitleChoice;
            });
            if (title == null) return;

            // THE HOUSE OPENS (owner: the title should appear by the curtain parting):
            // the swaying curtain stands shut over the title for a breath, then parts.
            if (!Harness) StartCoroutine(RevealTitle());
        }

        IEnumerator RevealTitle()
        {
            if (Curtain == null) yield break;
            Curtain.ConsideringLine = "";
            Curtain.SnapShut();
            yield return new WaitForSecondsRealtime(1.1f);
            yield return Curtain.Open(0.8f);
        }

        /// The title's three doors. `result` is null for the harness any-key contract,
        /// a TitleChoice for NEW GAME / CONTINUE.
        void OnTitleChoice(object result)
        {
            var choice = result as TitleChoice;
            if (choice != null)
            {
                Driver.SetActiveSlot(choice.Slot);
                if (choice.NewGame) Driver.ClearRun();   // NEW GAME overwrites the desk
            }
            StartRun();
        }

        public void StartRun()
        {
            // one ongoing run at a time (60 Seconds! style): resume it if it exists;
            // death/exit clears it. Autopilot modes always start fresh.
            if (!Harness && !Env.Flag("RUNWAY_FIRSTFLOW") && Driver.HasSavedRun())
            {
                if (Driver.ResumeSavedRun())
                {
                    Go(AppState.Garage);
                    return;
                }
            }
            Driver.BeginFreshRun(DailyMode);

            // FIRST EVER RUN: the rules of the world, once, before anything is chosen
            if (!Harness && !Env.Flag("RUNWAY_FIRSTFLOW") && !Runway.Screens.HowToScreen.Seen)
            {
                Go(AppState.HowTo, null, s => { s.Done += _ => ToDraft(); });
                return;
            }
            ToDraft();
        }

        void ToDraft()
        {
            Go(AppState.Draft, null, s => { s.Done += AfterDraft; });
        }

        /// main.gd's _after_draft: the engine half goes to the driver, the SCREEN half
        /// is this — birth while the bible is written, then the book, then the room.
        void AfterDraft(object draftResult)
        {
            Driver.ApplyDraft(draftResult);
            StartCoroutine(AfterDraftRoutine());
        }

        IEnumerator AfterDraftRoutine()
        {
            if (Env.Flag("RUNWAY_SHOT") || Env.Flag("RUNWAY_FULLRUN"))
            {
                // the SHOT harness photographs screens, not story
                Go(AppState.Garage);
                Driver.ColdOpen();
                yield break;
            }

            // THE BIRTH SCREEN (owner: signing looked dead while the world loaded):
            // the moment the papers are signed, a drawn title card breathes
            // "creating your world…" until the bible lands. No frozen draft page.
            Go(AppState.Birth);
            Driver.EnsureWorldgen();

            // WAIT WITH A CEILING (owner: "I was on this screen for ages"): 25s, then
            // the deterministic skeleton IS the world and the run moves on.
            float waited = 0f;
            while (Driver.WorldgenInFlight && !Driver.WorldgenLanded && waited < 25f)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                waited += 0.25f;
            }

            // The bible lands (or the ceiling fired): apply it, then the book opens on
            // the founder's own entry — fed now if day one already landed.
            string entry = Driver.FinishWorldgen();
            Go(AppState.Book, entry, s =>
            {
                s.Done += _ =>
                {
                    Go(AppState.Garage);
                    Driver.ColdOpen();
                };
            });
        }

        // ══ swapping ═══════════════════════════════════════════════════════════

        /// ORDER MATTERS (owner: "we briefly see the previous image"): the new screen
        /// is added FIRST, then the old one goes — no empty frame where the clear
        /// colour shows through. The new screen fades in over 0.18s so every
        /// transition reads as intended, not as a pop.
        public AppScreen Go(AppState state, object payload = null, Action<AppScreen> wire = null)
        {
            Type t = ScreenRegistry.Resolve(state);
            var go = new GameObject(state.ToString(), typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(ScreenLayer, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            AppScreen screen = null;
            if (t != null) screen = go.AddComponent(t) as AppScreen;
            if (screen == null)
            {
                if (t != null)
                    Debug.LogError("RUNWAY! " + t.Name + " did not come up as an AppScreen.");
                screen = go.AddComponent<MissingScreen>();
            }
            screen.Payload = payload;
            if (wire != null) wire(screen);

            AppScreen previous = CurrentScreen;
            State = state;
            CurrentScreen = screen;
            screen.Build(rt);
            DrawnBoil.Sweep(rt);
            Runway.Game.ArtCache.Sweep();
            screen.StartCoroutine(screen.FadeIn(0.18f));

            if (previous != null) StartCoroutine(RetirePrevious(previous));
            return screen;
        }

        /// one frame of overlap so the swap is seamless, then the old goes
        IEnumerator RetirePrevious(AppScreen previous)
        {
            yield return null;
            if (previous != null && previous.gameObject != null) Destroy(previous.gameObject);
        }

        /// A screen that sits ON TOP of the run — settings, the gallery, an era beat.
        public AppScreen OpenOverlay(AppOverlay overlay, object payload = null,
                                     Action<AppScreen> wire = null)
        {
            Type t = ScreenRegistry.ResolveOverlay(overlay);
            if (t == null)
            {
                Debug.Log("RUNWAY! no " + overlay + " overlay registered yet.");
                return null;
            }
            var go = new GameObject(overlay.ToString(), typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(OverlayLayer, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var screen = go.AddComponent(t) as AppScreen;
            if (screen == null) { Destroy(go); return null; }
            screen.Payload = payload;
            if (wire != null) wire(screen);
            // an overlay closes itself when its door is used — the run behind it stands
            screen.Done += _ => screen.Close();
            screen.Build(rt);
            DrawnBoil.Sweep(rt);
            screen.StartCoroutine(screen.FadeIn(0.18f));
            return screen;
        }

        // ══ global keys (main.gd _unhandled_input) ═════════════════════════════

        bool _settingsOpen;
        bool _galleryOpen;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && !_settingsOpen)
            {
                var s = OpenOverlay(AppOverlay.Settings);
                if (s != null)
                {
                    _settingsOpen = true;
                    s.Done += _ => _settingsOpen = false;
                }
            }
            if (State != AppState.Title) return;
            if (Input.GetKeyDown(KeyCode.D))
            {
                DailyMode = true;
                StartRun();
                return;
            }
            if (Input.GetKeyDown(KeyCode.G) && !_galleryOpen)
            {
                var g = OpenOverlay(AppOverlay.Gallery);
                if (g != null)
                {
                    _galleryOpen = true;
                    g.Done += _ => _galleryOpen = false;
                }
            }
        }

        // ══ switches ═══════════════════════════════════════════════════════════

        static readonly string[] HarnessVars =
        {
            "RUNWAY_SHOT", "RUNWAY_FULLRUN", "RUNWAY_FIRSTFLOW",
            "RUNWAY_LANEWIRE", "RUNWAY_READING", "RUNWAY_TURN",
            "RUNWAY_UPERF",
        };

        /// A capture harness is running: no studio card, no curtain, no spending.
        public bool Harness
        {
            get
            {
                for (int i = 0; i < HarnessVars.Length; i++)
                    if (Env.Flag(HarnessVars[i])) return true;
                return false;
            }
        }

        /// Art costs money and up to three minutes. RUNWAY_NO_ART always wins;
        /// RUNWAY_ART / RUNWAY_TURN_ART are the QA opt-ins that make a harness spend.
        public bool ArtEnabled
        {
            get
            {
                if (Env.Flag("RUNWAY_NO_ART")) return false;
                if (Env.Flag("RUNWAY_ART")) return true;
                if (Env.Flag("RUNWAY_TURN_ART")) return true;
                return !Harness;
            }
        }
    }

    /// What the title hands back when a slot is chosen.
    public sealed class TitleChoice
    {
        public int Slot;
        public bool NewGame;

        public TitleChoice(int slot, bool newGame) { Slot = slot; NewGame = newGame; }
    }
}
