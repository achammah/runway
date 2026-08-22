#if !RUNWAY_FX_USHOTS_OFF
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Runway.Core;
using Runway.Game;
using Runway.Screens;

namespace Runway.App
{
    /// <summary>
    /// THE TWIN CAMERA — one command, the whole Godot shot set, from the Unity build.
    ///
    ///     RUNWAY_USHOTS=&lt;dir&gt; "build/mac/RUNWAY!.app/Contents/MacOS/RUNWAY!" \
    ///         -screen-width 1536 -screen-height 1024 -screen-fullscreen 0
    ///
    /// Unset the variable and nothing here exists at runtime: no GameObject, no
    /// coroutine, no behaviour change. Set it and the app stops being a game and
    /// becomes a photographer — it builds each state the Godot harnesses photograph,
    /// with the same fixture data, waits the same beats, writes a PNG under the
    /// IDENTICAL filename, and quits.
    ///
    /// WHAT IT PHOTOGRAPHS, and whose harness each shot is the twin of:
    ///
    ///   new_screens_shot.gd  n1_title_menu · n2_slot_panel · n3_howto · n4_keys ·
    ///                        n5_birth_fullframe · n6_book_intro
    ///   select_shot.gd       select_norm_check
    ///   howto_shot.gd        howto_p1 · howto_p2 · howto_p3
    ///   birth_shot.gd        birth_intro_check · birth_loop_check
    ///   traits_shot.gd       traits_card · traits_bag
    ///   binder_shot.gd       binder_0 … binder_8
    ///
    /// NO EDIT TO ANYTHING SHIPPED. The Godot harnesses run in an empty SceneTree; here
    /// the app boots normally and this drives it. Two things make that identical without
    /// a seam in Boot: RUNWAY_SHOT is set from a BeforeSceneLoad hook — one initialize
    /// phase before Boot's own Awake reads it — which is exactly what puts Boot on its
    /// harness path (no studio card, no keys gate, no curtain over the title, no paid
    /// render); and every state is then raised through `Boot.Go`, which destroys the
    /// screen before it, so the stage holds one screen at a time, like the .gd tree.
    ///
    /// THE BEATS ARE THE .GD BEATS. Every wait below is transcribed from the harness it
    /// twins, because a Unity screen slower to hydrate than its Godot twin is a finding
    /// and a longer wait would hide it. `RUNWAY_USHOTS_WARM=&lt;secs&gt;` adds settle before
    /// every capture for the follow-up question only, and says in the log that the set
    /// it produced is not a strict twin.
    ///
    /// IT RUNS WINDOWED, never `-batchmode`. `WaitForEndOfFrame` needs a frame that was
    /// actually drawn — the same reason howto_shot.gd says "windowed — headless renders
    /// nothing". Batch mode is detected and refused with a message rather than hanging.
    /// </summary>
    public sealed class UnityShots : MonoBehaviour
    {
        public const string DirVar = "RUNWAY_USHOTS";

        /// OPT-IN, AND OFF BY DEFAULT: extra seconds of settle before every capture.
        /// The beats below are transcribed from the .gd harnesses and must stay that
        /// way, because a Unity screen that is slower to hydrate than its Godot twin is
        /// a PARITY FINDING and a longer wait would hide it. This exists only to answer
        /// the follow-up question — "is that white rectangle a slow load or missing
        /// art?" — without a rebuild. A run with it set says so in the log and is not a
        /// strict twin.
        public const string WarmVar = "RUNWAY_USHOTS_WARM";

        static string _dir = "";
        static float _warm;

        /// Where the PNGs go. Empty until Install has seen the variable.
        public static string Dir { get { return _dir; } }

        public static bool Armed { get { return _dir.Length > 0; } }

        // ══ the entry point ════════════════════════════════════════════════════

        /// The lane's one Install. BeforeSceneLoad on purpose: Boot's Awake runs an
        /// initialize phase later, so the switches set here are the ones it reads.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install()
        {
            _dir = "";
            string d;
            try { d = Env.Get(DirVar, ""); }
            catch (Exception) { d = ""; }
            if (d == null) return;
            d = d.Trim();
            if (d.Length == 0) return;

            _dir = d;
            _warm = ReadWarm();
            RunwayPaths.TryCreateDir(_dir);
            SetProcessVar("RUNWAY_SHOT", "1");     // Boot.Harness: no card, no gate, no curtain
            SetProcessVar("RUNWAY_NO_ART", "1");   // and nothing this run may bill for
            Debug.Log("USHOTS armed -> " + _dir);
            if (_warm > 0f)
                Debug.LogWarning(string.Format(
                    "USHOTS warm settle {0:0.00}s per shot — NOT A STRICT TWIN of the .gd beats. "
                    + "Unset {1} for the comparable set.", _warm, WarmVar));
        }

        static float ReadWarm()
        {
            string raw;
            try { raw = Env.Get(WarmVar, ""); }
            catch (Exception) { return 0f; }
            if (raw == null || raw.Trim().Length == 0) return 0f;
            float secs;
            if (!float.TryParse(raw.Trim(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out secs))
            {
                Debug.LogWarning("USHOTS: " + WarmVar + "='" + raw + "' is not a number — ignored.");
                return 0f;
            }
            return Mathf.Clamp(secs, 0f, 10f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (!Armed) return;
            var go = new GameObject("RUNWAY! shots");
            DontDestroyOnLoad(go);
            go.AddComponent<UnityShots>();
        }

        static void SetProcessVar(string key, string value)
        {
            try
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    Environment.SetEnvironmentVariable(key, value);
            }
            catch (Exception e)
            {
                Debug.LogWarning("USHOTS could not set " + key + " (" + e.Message
                                 + ") — the harness still drives every screen itself.");
            }
        }

        // ══ the sitting ════════════════════════════════════════════════════════

        /// A PLAYER THAT NEVER GAINS FOCUS STOPS DRAWING, and a frame that is never
        /// drawn means WaitForEndOfFrame never returns — the harness hangs on shot one
        /// with no error at all. That is exactly what a run launched from a terminal
        /// does: the window opens behind the shell and never comes forward. Observed on
        /// the first live run of this file; this line is the whole fix.
        void Awake()
        {
            Application.runInBackground = true;
        }

        void Start() { StartCoroutine(Run()); }

        IEnumerator Run()
        {
            float began = Time.realtimeSinceStartup;

            if (Application.isBatchMode)
            {
                Debug.LogError("USHOTS: -batchmode draws no frame, so WaitForEndOfFrame never "
                               + "returns and every capture would be empty. Run the built player "
                               + "windowed (or the editor in Play mode) instead.");
                Leave(4);
                yield break;
            }

            while (Boot.Instance == null) yield return null;
            Boot app = Boot.Instance;
            SizeTheStage();
            yield return new WaitForSecondsRealtime(0.6f);   // Boot's own gate settles
            HideCurtain(app);

            yield return NewScreensSet(app);   // new_screens_shot.gd
            yield return SelectSet(app);       // select_shot.gd
            yield return HowToSet(app);        // howto_shot.gd
            yield return BirthSet(app);        // birth_shot.gd
            yield return TraitsSet(app);       // traits_shot.gd
            yield return BinderSet(app);       // binder_shot.gd

            Report(Time.realtimeSinceStartup - began);
            Leave(ExitCode());
        }

        // ── new_screens_shot.gd ────────────────────────────────────────────────

        IEnumerator NewScreensSet(Boot app)
        {
            // a fake save so the menu shows CONTINUE and the slot panel has a row
            UnityShotsFixtures.WriteDriftdeckSave();

            AppScreen title = app.Go(AppState.Title);
            yield return new WaitForSecondsRealtime(1.2f);
            UnityShotsPoke.Call(title, "ShowMenu");              // t._show_menu()
            yield return new WaitForSecondsRealtime(0.9f);
            yield return Shoot("n1_title_menu");

            UnityShotsPoke.Call(title, "PickSlot", false);       // t._pick_slot(false)
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shoot("n2_slot_panel");
            UnityShotsFixtures.ClearDriftdeckSave();             // SaveSystem.clear_run()

            app.Go(AppState.HowTo);
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Shoot("n3_howto");

            app.Go(AppState.Keys);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shoot("n4_keys");

            app.Go(AppState.Birth);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shoot("n5_birth_fullframe");

            UnityShotsFixtures.InstallFernora();
            var book = app.Go(AppState.Book) as BookIntroScreen;
            yield return new WaitForSecondsRealtime(0.6f);
            if (book != null) book.FeedEntry(UnityShotsFixtures.FernoraEntry);
            else Debug.LogError("USHOTS: AppState.Book did not come up as a BookIntroScreen.");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shoot("n6_book_intro");
        }

        // ── select_shot.gd ─────────────────────────────────────────────────────

        IEnumerator SelectSet(Boot app)
        {
            var draft = app.Go(AppState.Draft) as FounderDraftScreen;
            if (draft == null)
            {
                Debug.LogError("USHOTS: AppState.Draft did not come up as a FounderDraftScreen "
                               + "— select_norm_check skipped.");
                yield break;
            }
            // the loops hydrate frame by frame — let them finish before picking
            yield return new WaitForSecondsRealtime(1.8f);
            draft.ShowPage(1);
            yield return new WaitForSecondsRealtime(0.6f);
            draft.SelectPage.Select(3, true);                    // draft._select(3)
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shoot("select_norm_check");
        }

        // ── howto_shot.gd ──────────────────────────────────────────────────────

        /// user://seen_howto_v2 — HowToScreen's own mark, left exactly as found.
        const string SeenFile = "seen_howto_v2.unity";

        IEnumerator HowToSet(Boot app)
        {
            string mark = RunwayPaths.User(SeenFile);
            bool had = FileThere(mark);
            if (had) Delete(mark);

            AppScreen ht = app.Go(AppState.HowTo);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shoot("howto_p1");

            UnityShotsPoke.Call(ht, "Advance");                  // ht._btn.pressed.emit()
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shoot("howto_p2");

            UnityShotsPoke.Call(ht, "Advance");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shoot("howto_p3");

            // the last press is under test too: it must write the mark and finish
            UnityShotsPoke.Call(ht, "Advance");
            yield return new WaitForSecondsRealtime(0.3f);
            Debug.Log(string.Format("HOWTO SHOT done={0} seen={1}",
                                    ht != null && ht.Finished, HowToScreen.Seen));

            // leave the mark exactly as found, either way
            if (had) RunwayPaths.WriteAllText(mark, "1");
            else Delete(mark);
        }

        // ── birth_shot.gd ──────────────────────────────────────────────────────

        IEnumerator BirthSet(Boot app)
        {
            app.Go(AppState.Birth);
            yield return new WaitForSecondsRealtime(0.5f);   // mid-arrival, the fade-in landed
            yield return Shoot("birth_intro_check");
            yield return new WaitForSecondsRealtime(4.0f);   // 4.5s in: the arrival is spent
            yield return Shoot("birth_loop_check");
        }

        // ── traits_shot.gd ─────────────────────────────────────────────────────

        static readonly string[] BagPicks = { "itm_crystal_ball", "itm_energy_drinks" };

        IEnumerator TraitsSet(Boot app)
        {
            var d = app.Go(AppState.Draft) as FounderDraftScreen;
            if (d == null)
            {
                Debug.LogError("USHOTS: AppState.Draft did not come up as a FounderDraftScreen "
                               + "— traits_card and traits_bag skipped.");
                yield break;
            }
            yield return new WaitForSecondsRealtime(1.0f);

            // THE EX-FAANG PM: credibility 5 and a phone book full of numbers, which is
            // the owner's whole case for hidden traits printed on one card.
            int exI = ArchetypeIndex(d, "exfaang");
            d.ShowPage(1);
            d.SelectPage.Select(exI, false);
            yield return new WaitForSecondsRealtime(0.8f);
            UnityShotsPoke.Call(d.SelectPage, "ShowTraitTip", "credibility");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shoot("traits_card");

            // THE BAG, two things packed: one that buys luck at the price of standing,
            // one that buys stamina at the price of concentration.
            d.ShowPage(6);
            yield return new WaitForSecondsRealtime(0.7f);
            DraftBagPage bag = d.BagPage;   // page 6 rebuilds itself on entry — read it now
            for (int i = 0; i < BagPicks.Length; i++)
            {
                string id = BagPicks[i];
                if (d.Deck.Item(id) == null) { Debug.LogError("MISSING SHELF TILE: " + id); continue; }
                UnityShotsPoke.Call(bag, "Toggle", id, d.Deck.CarryCost(id));
            }
            JObject ball = d.Deck.Item(BagPicks[0]);
            if (ball != null) UnityShotsPoke.Call(bag, "ShowDetail", ball);
            yield return new WaitForSecondsRealtime(0.9f);
            yield return Shoot("traits_bag");
        }

        static int ArchetypeIndex(FounderDraftScreen d, string id)
        {
            for (int i = 0; i < d.Archetypes.Count; i++)
                if (ContentDb.Str(d.Archetypes[i] as JObject, "id") == id) return i;
            Debug.LogWarning("USHOTS: no archetype '" + id + "' on the shelf — using the first.");
            return 0;
        }

        // ── binder_shot.gd ─────────────────────────────────────────────────────

        IEnumerator BinderSet(Boot app)
        {
            GameState s = UnityShotsFixtures.InstallPivotflow();
            if (s == null) yield break;

            // the .gd adds the binder to an EMPTY tree, so the clear colour is the whole
            // background behind its scrim — clear the stage to photograph the same thing
            ClearStage(app);
            yield return null;

            BinderScreen b = BinderScreen.Open(s);
            if (b == null)
            {
                Debug.LogError("USHOTS: the binder would not open — binder_0..8 skipped.");
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.6f);
            for (int i = 0; i < 9; i++)
            {
                UnityShotsPoke.SetField(b, "_tab", i);           // b.set("_tab", i)
                UnityShotsPoke.Call(b, "Refresh");               // b.call("_refresh")
                yield return new WaitForSecondsRealtime(0.35f);
                yield return Shoot("binder_" + i);
            }
            if (b != null && b.gameObject != null) Destroy(b.gameObject);
        }

        // ══ the plumbing ═══════════════════════════════════════════════════════

        IEnumerator Shoot(string name)
        {
            if (_warm > 0f) yield return new WaitForSecondsRealtime(_warm);
            yield return UnityShotsCamera.Shoot(_dir, name);
        }

        /// The Godot window is 1536x1024 and every reference PNG is that size. A player
        /// launched with -screen-width/-screen-height is already right; this is the
        /// belt for one launched without them. The editor's Game view ignores it, which
        /// the per-shot SHOT SIZE warning then says out loud.
        static void SizeTheStage()
        {
            try
            {
                if (Screen.width != UnityShotsCamera.TwinWidth
                    || Screen.height != UnityShotsCamera.TwinHeight)
                    Screen.SetResolution(UnityShotsCamera.TwinWidth,
                                         UnityShotsCamera.TwinHeight, false);
            }
            catch (Exception e)
            {
                Debug.LogWarning("USHOTS could not size the window (" + e.Message + ").");
            }
        }

        /// Belt for a launch where RUNWAY_SHOT could not be set: the title's curtain
        /// would otherwise stand shut over the first two shots.
        static void HideCurtain(Boot app)
        {
            if (app == null || app.Curtain == null) return;
            GameObject go = app.Curtain.gameObject;
            if (go != null && go.activeSelf) go.SetActive(false);
        }

        static void ClearStage(Boot app)
        {
            Wipe(app != null ? app.ScreenLayer : null);
            Wipe(app != null ? app.OverlayLayer : null);
        }

        static void Wipe(RectTransform layer)
        {
            if (layer == null) return;
            for (int i = layer.childCount - 1; i >= 0; i--)
                Destroy(layer.GetChild(i).gameObject);
        }

        static bool FileThere(string path)
        {
            try { return File.Exists(path); }
            catch (Exception) { return false; }
        }

        static void Delete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception e) { Debug.LogWarning("USHOTS cannot remove " + path + ": " + e.Message); }
        }

        // ══ the verdict ════════════════════════════════════════════════════════

        /// Every filename this harness owes, so a missing one is named rather than
        /// counted. The order is the order they are taken in.
        public static readonly string[] Expected =
        {
            "n1_title_menu", "n2_slot_panel", "n3_howto", "n4_keys",
            "n5_birth_fullframe", "n6_book_intro",
            "select_norm_check",
            "howto_p1", "howto_p2", "howto_p3",
            "birth_intro_check", "birth_loop_check",
            "traits_card", "traits_bag",
            "binder_0", "binder_1", "binder_2", "binder_3", "binder_4",
            "binder_5", "binder_6", "binder_7", "binder_8",
        };

        void Report(float secs)
        {
            var missing = new List<string>();
            for (int i = 0; i < Expected.Length; i++)
                if (!UnityShotsCamera.Written.Contains(Expected[i])) missing.Add(Expected[i]);

            Debug.Log(string.Format(
                "UNITY SHOTS DONE {0}/{1} in {2:0.0}s -> {3}",
                UnityShotsCamera.Written.Count, Expected.Length, secs, _dir));
            Say("missing", missing);
            Say("flat", UnityShotsCamera.Flat);
            Say("failed", UnityShotsCamera.Failed);
            Say("wrong size", UnityShotsCamera.WrongSize);
            Say("poke misses", UnityShotsPoke.Misses);
            if (missing.Count == 0 && UnityShotsCamera.Flat.Count == 0
                && UnityShotsCamera.Failed.Count == 0 && UnityShotsPoke.Misses.Count == 0)
                Debug.Log("UNITY SHOTS CLEAN — every state photographed, nothing flat.");
        }

        static void Say(string label, List<string> rows)
        {
            if (rows == null || rows.Count == 0) return;
            Debug.LogError(string.Format("UNITY SHOTS {0} ({1}): {2}",
                label, rows.Count, string.Join(" · ", rows.ToArray())));
        }

        static int ExitCode()
        {
            if (UnityShotsCamera.Failed.Count > 0) return 2;
            if (UnityShotsCamera.Written.Count < Expected.Length) return 2;
            if (UnityShotsPoke.Misses.Count > 0) return 3;
            if (UnityShotsCamera.Flat.Count > 0) return 3;
            return 0;
        }

        static void Leave(int code)
        {
#if UNITY_EDITOR
            Debug.Log("USHOTS leaving play mode (exit code would be " + code + ")");
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(code);
#endif
        }
    }
}
#endif
