#if !RUNWAY_FX_UFLOW_OFF && !RUNWAY_FX_USHOTS_OFF
using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Runway.Core;
using Runway.Game;
using Runway.Llm;

namespace Runway.App
{
    /// <summary>
    /// THE BEHAVIOUR HARNESS — the Unity twin of main.gd's `_firstflow` and `_fullrun`,
    /// and the thing that proves checklist section C.
    ///
    ///     RUNWAY_UFLOW=&lt;dir&gt; RUNWAY_UFLOW_WEEKS=3 \
    ///       "build/mac/RUNWAY!.app/Contents/MacOS/RUNWAY!" \
    ///       -screen-width 1536 -screen-height 1024 -screen-fullscreen 0
    ///
    /// UnityShots photographs SCREENS. This one PLAYS THE GAME: it walks the real
    /// player path — studio card, title, NEW GAME, the slot table, the seven draft
    /// pages, the launch, the birth holds, the book, SETTLE IN, the room — and then
    /// locks weeks in the journal, answers the world's clarifying question, watches
    /// the die, reads the beat, turns the page and opens the binder. Every stage is
    /// photographed and every claim in section C that can be measured from outside
    /// the engine is asserted out loud.
    ///
    /// IT IS NOT A HARNESS AS FAR AS `Boot` IS CONCERNED, AND THAT IS THE POINT.
    /// `RUNWAY_UFLOW` is deliberately NOT in `Boot.HarnessVars`: a run with
    /// `Boot.Harness` true skips the studio card, answers the title with the any-key
    /// contract (so NEW GAME never shows), jumps `AfterDraftRoutine` straight past
    /// BIRTH and BOOK into the garage, and switches art off. Every one of those is a
    /// stage C1 exists to test. So this lane sets NO process variable at all and
    /// drives the shipped screens instead — which means the run behaves like a
    /// player's in one more respect too: it WRITES SAVES. `UnityFlowGuard` takes all
    /// three slots and the how-to mark before the first frame and puts them back at
    /// the end, on the quit path as well (BUG-15: a test run must never eat a company).
    ///
    /// EVERY ASSERT IS A LINE, NEVER AN ABORT. A failed check logs
    /// `UFLOW FAIL &lt;check&gt;: &lt;detail&gt;` and the walk carries on, because one broken
    /// stage must not hide the six behind it. The last line is
    /// `UFLOW DONE pass=&lt;n&gt; fail=&lt;n&gt;` and the exit code IS the fail count.
    ///
    /// IT RUNS WINDOWED, never `-batchmode` — `WaitForEndOfFrame` needs a frame that
    /// was actually drawn, the same reason UnityShots refuses batch mode.
    ///
    /// WHAT IT COSTS. This is the paid probe: worldgen, day one, the founding paint
    /// and one compose per locked week. `RUNWAY_NO_ART=1` still wins over everything
    /// (Boot.ArtEnabled) and turns the run into a text-only rehearsal, which is the
    /// cheap way to check the walk before spending on it.
    /// </summary>
    public sealed partial class UnityFlow : MonoBehaviour
    {
        public const string DirVar = "RUNWAY_UFLOW";
        /// Extra weeks to play after the first flow's own week 1. Default 0.
        public const string WeeksVar = "RUNWAY_UFLOW_WEEKS";
        /// Which save slot the run is played in. Default 3; ALL THREE are restored.
        public const string SlotVar = "RUNWAY_UFLOW_SLOT";

        static string _dir = "";
        static int _weeks;
        static int _slot = 3;

        public static string Dir { get { return _dir; } }
        public static bool Armed { get { return _dir.Length > 0; } }

        // ══ the entry point ════════════════════════════════════════════════════

        /// BeforeSceneLoad for symmetry with UnityShots, and so the slot guard has
        /// taken its copies before Boot's Awake can start a run over them.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install()
        {
            _dir = "";
            string d;
            try { d = Env.Get(DirVar, ""); }
            catch (Exception) { d = ""; }
            if (d == null) return;
            d = d.Trim();
            if (d.Length == 0) return;          // UNARMED: nothing here exists at runtime

            _dir = d;
            _weeks = ReadInt(WeeksVar, 0, 0, 60);
            _slot = ReadInt(SlotVar, 3, 1, SaveSlots.SlotCount);
            RunwayPaths.TryCreateDir(_dir);
            UnityFlowGuard.BackUp(_dir);
            Debug.Log(string.Format("UFLOW armed -> {0} · weeks={1} · slot={2}", _dir, _weeks, _slot));
        }

        static int ReadInt(string key, int fallback, int lo, int hi)
        {
            string raw;
            try { raw = Env.Get(key, ""); }
            catch (Exception) { return fallback; }
            if (raw == null || raw.Trim().Length == 0) return fallback;
            int v;
            if (!int.TryParse(raw.Trim(), out v))
            {
                Debug.LogWarning("UFLOW: " + key + "='" + raw + "' is not a number — using " + fallback);
                return fallback;
            }
            return Mathf.Clamp(v, lo, hi);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (!Armed) return;
            var go = new GameObject("RUNWAY! flow");
            DontDestroyOnLoad(go);
            go.AddComponent<UnityFlow>();
        }

        /// A player that never gains focus stops drawing, and a frame that is never
        /// drawn means WaitForEndOfFrame never returns — the harness would hang on
        /// shot one with no error. Launched from a terminal, that is exactly what
        /// happens. (UnityShots.Awake, same line, same reason.)
        void Awake() { Application.runInBackground = true; }

        void Start() { StartCoroutine(Run()); }

        /// The guard is idempotent, so an Application.Quit that outruns the tail of
        /// Run() still gives the owner their slots back.
        void OnApplicationQuit() { UnityFlowGuard.Restore(); }

        // ══ the walk ═══════════════════════════════════════════════════════════

        IEnumerator Run()
        {
            float began = Time.realtimeSinceStartup;

            if (Application.isBatchMode)
            {
                Debug.LogError("UFLOW: -batchmode draws no frame, so WaitForEndOfFrame never "
                               + "returns and every capture would be empty. Run the built player "
                               + "windowed (or the editor in Play mode) instead.");
                Leave(4);
                yield break;
            }

            while (Boot.Instance == null) yield return null;
            Boot app = Boot.Instance;
            SizeTheStage();
            Debug.Log(string.Format("UFLOW: harness={0} art={1} llm={2} — the run walks the "
                                    + "PLAYER path on purpose (see the file header).",
                app.Harness, app.ArtEnabled,
                app.Llm != null && app.Llm.Enabled ? app.Llm.Provider + "/" + app.Llm.Model : "off"));
            if (app.Llm == null || !app.Llm.Enabled)
                Fail("c0_llm", "no API key is live — day one, the clarify pre-pass and the "
                     + "adjudication all fall back to the authored path, so C1/C3/C7 cannot "
                     + "be proved by this run");

            yield return new WaitForSecondsRealtime(0.8f);   // Boot's own gate settles

            yield return FirstFlow(app);

            for (int w = 0; w < _weeks; w++)
            {
                if (!RoomAlive())
                {
                    Fail("c2_run_alive", string.Format(
                        "the run left the garage before extra week {0} of {1} (state={2}, dead={3})",
                        w + 1, _weeks, app.State, St != null && St.Dead));
                    break;
                }
                string move = Moves[w % Moves.Length];
                yield return PlayWeek("c2", move);
            }

            UnityFlowGuard.Restore();
            Report(Time.realtimeSinceStartup - began);
            Leave(_fail);
        }

        // ══ C1 — the first flow ════════════════════════════════════════════════

        /// The twin of `_firstflow`, plus the two stages the .gd skips because Godot's
        /// harness path skips them: the studio card and the NEW GAME slot table.
        IEnumerator FirstFlow(Boot app)
        {
            // ── the boot gate ─────────────────────────────────────────────────
            yield return Until(() => app.State == AppState.Title
                                     || app.State == AppState.StudioCard
                                     || app.State == AppState.Keys, 30f, "the boot gate");
            if (app.State == AppState.Keys)
            {
                Fail("c1_boot", "the boot gate opened on the KEY DESK — there is no key and no "
                     + "keys.env, so this run cannot prove anything paid");
                yield return Shoot("c1", "boot_keys");
                yield break;
            }
            if (app.State == AppState.StudioCard)
            {
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Shoot("c1", "studio_card");
                app.ToTitle();                       // the card's own door, called straight
            }

            // ── the title, under the curtain that parts ───────────────────────
            yield return Until(() => app.State == AppState.Title, 20f, "the title");
            if (app.State != AppState.Title)
            {
                Fail("c1_title", "the title never came up — the walk cannot start");
                yield break;
            }
            yield return new WaitForSecondsRealtime(2.6f);   // SnapShut 1.1s + Open 0.8s
            yield return Shoot("c1", "title");

            AppScreen title = app.CurrentScreen;
            UnityShotsPoke.Call(title, "ShowMenu");           // Input.anyKeyDown, without a key
            yield return new WaitForSecondsRealtime(1.1f);
            yield return Shoot("c1", "title_menu");

            UnityShotsPoke.Call(title, "PickSlot", true);     // NEW GAME
            yield return new WaitForSecondsRealtime(1.1f);
            yield return Shoot("c1", "slot_table");

            if (!PressSlotCard(title, _slot))
                Fail("c1_new_game", "no card named slot" + _slot + " on the slot table — "
                     + "NEW GAME could not be answered");

            // ── the rules, once, on a first-ever run ──────────────────────────
            yield return Until(() => app.State == AppState.Draft || app.State == AppState.HowTo
                                     || app.State == AppState.Garage, 25f, "NEW GAME to land");
            if (app.State == AppState.Garage)
                Fail("c1_new_game", "NEW GAME resumed a saved run instead of dealing a fresh one");
            if (app.State == AppState.HowTo)
            {
                // the sheet is 3 pages when all three loops ship and ONE when none do,
                // so this pages until the screen itself leaves — poking Advance at the
                // draft would log a miss the run would then count as a failure
                for (int i = 0; i < 4 && app.State == AppState.HowTo; i++)
                {
                    yield return new WaitForSecondsRealtime(0.8f);
                    if (i == 0) yield return Shoot("c1", "howto");
                    if (app.State != AppState.HowTo) break;
                    UnityShotsPoke.Call(app.CurrentScreen, "Advance");
                }
                yield return Until(() => app.State == AppState.Draft, 15f, "the draft after the rules");
            }

            // ── the seven pages ───────────────────────────────────────────────
            yield return Until(() => app.CurrentScreen is FounderDraftScreen, 20f, "the draft screen");
            var d = app.CurrentScreen as FounderDraftScreen;
            if (d == null)
            {
                Fail("c1_draft", "AppState.Draft did not come up as a FounderDraftScreen");
                yield break;
            }
            yield return new WaitForSecondsRealtime(1.8f);   // the loops hydrate frame by frame
            yield return Shoot("c1", "draft_sign");

            TypeFounderName(d, FounderName);
            yield return new WaitForSecondsRealtime(0.5f);

            d.TransitionTo(1);
            yield return new WaitForSecondsRealtime(1.1f);
            d.SelectPage.Select(1, true);                    // d._select(1)
            yield return new WaitForSecondsRealtime(1.3f);
            yield return Shoot("c1", "draft_select");

            d.TransitionTo(2);
            yield return new WaitForSecondsRealtime(1.1f);
            TypePitch(d, CompanyName, CompanyIdea);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shoot("c1", "draft_name");

            d.TransitionTo(3);
            yield return new WaitForSecondsRealtime(1.1f);
            PickShape(d, "Software", "SMB");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shoot("c1", "draft_shape");

            d.TransitionTo(4);
            yield return new WaitForSecondsRealtime(1.1f);
            d.Cofounders.Add(new DraftCofounder                    // d._cofounders.append({...})
            {
                Name = "Mara Quist", Role = "Tech", Commitment = "Full-time",
                Equity = 30.0, Vesting = true,
            });
            d.RefreshCapLine();                                    // d._refresh_capline()
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Shoot("c1", "draft_crew");

            d.TransitionTo(5);
            yield return new WaitForSecondsRealtime(1.1f);
            PickFunding(d, 2);                                     // d._pick_fund(funds[2], ...)
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Shoot("c1", "draft_money");

            d.TransitionTo(6);
            yield return new WaitForSecondsRealtime(1.4f);         // page 6 REBUILDS on entry
            PackTheBag(d);
            yield return new WaitForSecondsRealtime(0.9f);
            yield return Shoot("c1", "draft_bag");

            string blocked = d.BlockedReason();
            if (blocked.Length > 0) Fail("c1_draft_gate", "the launch door is shut: " + blocked);
            else Pass("c1_draft_gate");
            d.DoLaunch();                                          // d._do_launch()

            // ── BIRTH: the world, then the words, then the paint ──────────────
            yield return Until(() => app.State == AppState.Birth || app.State == AppState.Book,
                               25f, "the birth screen");
            if (app.State != AppState.Birth && app.State != AppState.Book)
            {
                Fail("c1_birth", "the launch went to " + app.State + ", not BIRTH");
                yield break;
            }
            yield return new WaitForSecondsRealtime(1.0f);
            if (app.State == AppState.Birth) yield return Shoot("c1", "birth");

            // the birth screen legitimately holds through words AND paint: Boot's own
            // ceilings are 25s + 160s + 240s, so 8 minutes is the honest wall
            string lastStatus = "";
            float t0 = Time.realtimeSinceStartup;
            while (app.State == AppState.Birth && Time.realtimeSinceStartup - t0 < BirthCap)
            {
                var birth = app.CurrentScreen as BirthScreen;
                string s = birth != null ? (birth.StatusLine ?? "") : "";
                if (s != lastStatus)
                {
                    lastStatus = s;
                    Debug.Log(string.Format("UFLOW birth hold: '{0}' at {1:0}s",
                                            s, Time.realtimeSinceStartup - t0));
                    yield return Shoot("c1", "birth_" + Slug(s));
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            if (app.State == AppState.Birth)
            {
                Fail("c1_birth_ceiling", string.Format(
                    "still on BIRTH after {0:0}s — the words/paint ceilings did not release it "
                    + "(last status '{1}')", BirthCap, lastStatus));
                yield break;
            }
            Pass("c1_birth_ceiling");

            // ── BOOK: the entry, then the paint gate ──────────────────────────
            yield return Until(() => app.State == AppState.Book, 30f, "the book");
            if (app.State != AppState.Book)
            {
                Fail("c1_book", "BIRTH handed over to " + app.State + ", not BOOK");
                yield break;
            }
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shoot("c1", "book");

            yield return Until(() => EntryLanded(app), 120f, "day one on the page");
            string entry = EntryText(app);
            if (entry.Length == 0)
                Fail("c1_book_entry", "the book is still showing its placeholder — no entry, "
                     + "live or authored, ever reached the page");
            else if (entry.Length < 80)
                Fail("c1_book_entry", "the entry is only " + entry.Length
                     + " chars: '" + Left(entry, 120) + "'");
            else
            {
                Pass("c1_book_entry");
                Debug.Log("UFLOW day one (" + entry.Length + " chars): " + Left(entry, 160));
            }
            yield return new WaitForSecondsRealtime(2.0f);
            yield return Shoot("c1", "book_entry");

            RunDriver drv = RunDriver.Current;
            yield return Until(() => drv == null || drv.WarmPaint != PaintStatus.Painting,
                               PaintCap, "the paint gate");
            Debug.Log("UFLOW paint gate: " + (drv != null ? drv.WarmPaint.ToString() : "no driver"));
            yield return Shoot("c1", "book_paint_gate");

            // THE REAL DOOR: `(_screen as BookIntroScreen).done.emit()`
            UnityShotsPoke.Call(app.CurrentScreen, "Finish", (object)null);

            // ── the room the decision made ────────────────────────────────────
            yield return Until(() => app.State == AppState.Garage && GarageScreen.Room != null,
                               30f, "the garage");
            if (app.State != AppState.Garage)
            {
                Fail("c1_settle", "SETTLE IN went to " + app.State + ", not the garage");
                yield break;
            }
            yield return new WaitForSecondsRealtime(1.6f);
            yield return Shoot("c1", "garage_settled");

            // C1's room claim only means anything when the run is allowed to paint:
            // under RUNWAY_NO_ART there is deliberately nothing to adopt and nothing to
            // announce, and holding the walk for four minutes to say so would be a lie.
            if (!app.ArtEnabled)
            {
                Debug.LogWarning("UFLOW skip c1_room: art is off (RUNWAY_NO_ART) — this walk "
                                 + "cannot prove the PAINTED room. Re-run without it for C1.");
            }
            else
            {
                yield return Until(() => RoomComposed() || RibbonUp() || !RoomAlive(),
                                   PaintCap, "the painted room");
                if (RoomComposed())
                {
                    Pass("c1_room");
                    Debug.Log("UFLOW room: composed painting adopted -> "
                              + GarageScreen.Room.ComposedPath);
                }
                else if (RibbonUp())
                {
                    Pass("c1_room");
                    Debug.Log("UFLOW room: authored room + '✎ your room is being painted…' ribbon");
                }
                else
                {
                    Fail("c1_room", "no composed painting was adopted and no painting ribbon is "
                         + "up — the player is looking at the stock drawn garage with nothing "
                         + "said about it");
                }
            }
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shoot("c1", "garage_room");

            // week 1 is part of the first flow: journal → move → LOCK IN → dice →
            // beat → was-page → binder
            yield return PlayWeek("c1", FirstMove);
        }

        // ══ C2..C7 — one locked week ═══════════════════════════════════════════

        /// Everything a week does, driven and measured. `tag` prefixes the shots so the
        /// first flow's week reads as c1_* and the extra weeks as c2_*.
        IEnumerator PlayWeek(string tag, string move)
        {
            GameState st = St;
            if (!RoomAlive() || st == null)
            {
                Fail(tag + "_week", "no live room to play a week in");
                yield break;
            }
            int weekBefore = st.Week;
            Debug.Log(string.Format("UFLOW week {0} ({1}) · cash ${2} · move: {3}",
                                    weekBefore, st.Era, st.Cash, move));

            // a beat from the previous week must be finished before this one is written
            yield return DrainBeat(BeatCap);
            yield return Until(() => !TurnBusyNow() && (GarageScreen.Room == null
                                                        || !GarageScreen.Room.WorldBusy),
                               240f, "the world to be free");

            GarageScreen room = GarageScreen.Room;
            if (room == null) { Fail(tag + "_week", "the room went away before the journal"); yield break; }
            if (!room.JournalOpen)
            {
                room.OpenJournal();
                yield return new WaitForSecondsRealtime(1.4f);
            }
            yield return Shoot(tag, "w" + Two(weekBefore) + "_journal");

            // ── C7: this week's card ──────────────────────────────────────────
            CheckEventCard(room, weekBefore);

            // ── the decision spread ───────────────────────────────────────────
            yield return TurnToDecisionSpread(room);
            yield return new WaitForSecondsRealtime(1.9f);       // the page writes itself in
            yield return Shoot(tag, "w" + Two(weekBefore) + "_ahead");

            WeekCommit commit = Commit(room);
            JournalPage page = Page(room);
            if (commit == null || page == null)
            {
                Fail(tag + "_journal", "could not reach the week's commit/page model");
                yield break;
            }
            page.SetWritten(move);          // the input field IS the interface; its
            commit.Written = move;          // onValueChanged carries it, this is the belt
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shoot(tag, "w" + Two(weekBefore) + "_written");

            int unpricedBefore = UnpricedOffers(st);
            int cashAtPress = st.Cash;
            string eraAtPress = st.Era;
            bool wantAmount = HasNoAmount(move) && Spends(move);
            bool wantPrice = Sells(move) && !HasAmount(move) && unpricedBefore > 0;

            PressLock(commit);

            // ── C3: the pre-pass, one question when the move hides its number ─
            yield return Until(() => commit.Clarify != null || PendingDice(commit) != null
                                     || st.Week != weekBefore || !RoomAlive(),
                               ClarifyCap, "the clarify pre-pass");
            if (commit.Clarify != null)
            {
                string kind = ContentDb.Str(commit.Clarify, "kind");
                string q = ContentDb.Str(commit.Clarify, "q");
                Debug.Log("UFLOW clarify asks (" + kind + "): " + q);
                yield return new WaitForSecondsRealtime(1.0f);
                yield return Shoot(tag, "w" + Two(weekBefore) + "_clarify_" + Slug(kind));

                if (q.Trim().Length == 0)
                    Fail("c3_clarify_line", "the clarify UI is up with an empty question line");
                else Pass("c3_clarify_line");

                if (wantPrice)
                {
                    if (kind == "price") Pass("c3_price_ask");
                    else Fail("c3_price_ask", string.Format(
                        "an unpriced offer ({0} of {1}) and a sell move with no price, and the "
                        + "world asked a '{2}' question instead of a price one: '{3}'",
                        unpricedBefore, st.Offers != null ? st.Offers.Count : 0, kind, q));
                }
                if (wantAmount)
                {
                    if (kind == "amount") Pass("c3_amount_ask");
                    else Fail("c3_amount_ask", "an amountless spend move drew a '" + kind
                              + "' question, not an amount one: '" + q + "'");
                }
                yield return AnswerClarify(room, kind, tag, weekBefore);

                // C3's second half: a priced answer must SET THE ENGINE PRICE
                if (kind == "price")
                {
                    int after = UnpricedOffers(St);
                    if (after < unpricedBefore) Pass("c3_price_set");
                    else Fail("c3_price_set", string.Format(
                        "the price question was answered and the engine still has {0} unpriced "
                        + "offer(s) — the chip did not reach GameState.Offers", after));
                }
            }
            else
            {
                if (wantPrice)
                    Fail("c3_price_ask", string.Format(
                        "{0} unpriced offer(s) and a sell move with no price in it, and the "
                        + "pre-pass stayed silent — nothing asked what it costs", unpricedBefore));
                if (wantAmount)
                    Fail("c3_amount_ask", "an amountless spend move and the pre-pass stayed silent");
            }

            // ── C6: the die is final at the press ─────────────────────────────
            JObject dice = null;
            float dieT0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - dieT0 < DieCap)
            {
                dice = PendingDice(commit);
                if (dice != null || st.Week != weekBefore || !RoomAlive()) break;
                yield return null;                       // frame-poll: _pendingDice is short-lived
            }
            int engineDie = dice != null ? ContentDb.Int(dice, "used", 0) : 0;

            int cupDie = -1;
            float cupT0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - cupT0 < 10f && cupDie < 0)
            {
                cupDie = CupNumber();
                if (cupDie < 0) yield return null;
            }
            if (CupNow() != null)
            {
                yield return new WaitForSecondsRealtime(1.4f);
                yield return Shoot(tag, "w" + Two(weekBefore) + "_dice");
            }

            // ── the reading beat ──────────────────────────────────────────────
            yield return Until(() => BeatNow() != null || st.Week != weekBefore || !RoomAlive(),
                               BeatOpenCap, "the reading beat");
            int beatDie = -1;
            ReadingBeat beat = BeatNow();
            if (beat != null)
            {
                Pass(tag + "_beat");
                yield return new WaitForSecondsRealtime(2.2f);
                yield return Shoot(tag, "w" + Two(weekBefore) + "_beat");
                // the judgement sentence is REVEALED on a reading clock — it is the
                // fourth beat in the queue and lands 10-25s in. One click catches the
                // page up, and only then is the shown number on the page to be read.
                ReadingBeat live = BeatNow();
                if (live != null) UnityShotsPoke.Call(live, "SkipReading");
                yield return new WaitForSecondsRealtime(0.7f);
                beatDie = DieFromBeat(BeatNow());
            }
            else
            {
                Fail(tag + "_beat", "the week locked and no reading beat ever opened");
            }

            // engine number, and the two places the player SEES a number
            if (engineDie <= 0 && LastDice() != null) engineDie = ContentDb.Int(LastDice(), "used", 0);
            CompareDice(weekBefore, engineDie, cupDie, beatDie);

            yield return DrainBeat(BeatCap);

            // ── the week turns ────────────────────────────────────────────────
            yield return Until(() => (St != null && St.Week > weekBefore) || !RoomAlive(),
                               TurnCap, "the week to turn");
            if (St != null && St.Week > weekBefore) Pass(tag + "_week_turned");
            else Fail(tag + "_week_turned", string.Format(
                "the week did not advance past {0} (room alive={1})", weekBefore, RoomAlive()));

            yield return new WaitForSecondsRealtime(2.8f);   // the dread beat fades back in

            // ── C4: the money law ─────────────────────────────────────────────
            CheckMoney(move, cashAtPress, eraAtPress);

            // ── the was-page ──────────────────────────────────────────────────
            room = GarageScreen.Room;
            if (RoomAlive() && room != null)
            {
                if (!room.JournalOpen)
                {
                    room.OpenJournal();
                    yield return new WaitForSecondsRealtime(1.6f);
                }
                yield return Shoot(tag, "w" + Two(weekBefore) + "_was_page");
                CheckWasPage(weekBefore);
            }
            else
            {
                Fail(tag + "_was_page", "the run left the garage before the week could be read back");
            }

            // ── the binder ────────────────────────────────────────────────────
            GameState now = St;
            if (now != null)
            {
                BinderScreen b = BinderScreen.Open(now);
                if (b == null) Fail(tag + "_binder", "the binder would not open");
                else
                {
                    Pass(tag + "_binder");
                    yield return new WaitForSecondsRealtime(0.9f);
                    yield return Shoot(tag, "w" + Two(weekBefore) + "_binder");
                    if (b != null && b.gameObject != null) Destroy(b.gameObject);
                    yield return new WaitForSecondsRealtime(0.5f);
                }
            }
        }

        // ══ the scripted weeks ═════════════════════════════════════════════════

        const string FounderName = "Tess Marlow";
        const string CompanyName = "Runwayworks";
        const string CompanyIdea = "voice-first bookkeeping for one-van tradespeople";

        /// Week 1 stays plain on purpose: C1 is about the FLOW reaching a locked week,
        /// not about the clarify branches.
        const string FirstMove =
            "Head down and sprint on the product all week: fix the three bugs that make "
            + "people quit on the first screen.";

        /// The $ move and the sell move come FIRST so a two-week run still covers both
        /// C4 and C3; the amountless one is the fourth, for a longer soak.
        static readonly string[] Moves =
        {
            "Put $1,500 into Google ads aimed at our exact niche this week.",
            "Get out of the building: sell to ten real tradespeople this week and close one paying.",
            "Take the whole week to recruit: interview three engineers and make one offer.",
            "Run some ads.",
        };
    }
}
#endif
