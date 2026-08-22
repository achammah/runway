#if !RUNWAY_FX_UFLOW_OFF && !RUNWAY_FX_USHOTS_OFF
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.Core;
using Runway.Game;

namespace Runway.App
{
    /// <summary>
    /// THE HARNESS'S HANDS AND EYES — every reach-in `UnityFlow` makes into the shipped
    /// screens, every assert it can state, and the guard that gives the owner their
    /// save slots back.
    ///
    /// WHY REFLECTION AT ALL. The Godot harnesses drive the game through its own
    /// private members — `gv._page_i = 1`, `gv._show_spread()`, `_beat.set("_proceed",
    /// true)`, `d._toggle_bag(...)`. GDScript allows that; C# does not, and this lane
    /// may not edit a shipped file to add a seam. So the same entry points are reached
    /// with `UnityShotsPoke`, which logs a POKE MISS the moment a member is renamed —
    /// and `UnityFlow.Report` turns every miss into a failure, because a probe that
    /// quietly measures the wrong thing is worse than one that stops.
    ///
    /// WHERE THE PUBLIC DOOR EXISTS, THE PUBLIC DOOR IS USED. The draft's model
    /// (`Cofounders`, `Bag`, `SelFund`, `BizWhat`, `RefreshCapLine`, `TransitionTo`,
    /// `DoLaunch`), the room (`OpenJournal`, `ComposedPath`, `JournalOpen`,
    /// `CurrentEvent`), the week (`WeekCommit.Written`, `.Clarify`, `.AnswerClarify`,
    /// `.CommitFromText`) and the page (`JournalPage.SetWritten`) are all public, so
    /// the pokes below are only the handful of members that are not.
    /// </summary>
    public sealed partial class UnityFlow
    {
        // ── the walls the walk will not cross ──────────────────────────────────
        /// Boot's own holds are 25s worldgen + 160s words + 240s paint.
        const float BirthCap = 480f;
        const float PaintCap = 260f;
        const float ClarifyCap = 120f;
        const float DieCap = 120f;
        const float BeatOpenCap = 180f;
        /// TurnRunner.HoldCeiling is 150s; the beat then drains and fades.
        const float BeatCap = 260f;
        const float TurnCap = 180f;

        /// BookIntroScreen's own placeholder — the string that means "no entry yet".
        const string BookPlaceholder = "the first entry is being written…";

        static readonly string[] BagPicks =
        {
            "itm_laptop", "itm_savings_jar", "itm_houseplant", "itm_guitar",
        };

        // ══ the verdict ════════════════════════════════════════════════════════

        int _pass;
        int _fail;
        readonly List<string> _failed = new List<string>();
        readonly Dictionary<string, int> _shotN = new Dictionary<string, int>();
        readonly List<string> _seenBodies = new List<string>();
        readonly List<string> _seenTitles = new List<string>();

        void Pass(string check)
        {
            _pass++;
            Debug.Log("UFLOW pass " + check);
        }

        void Fail(string check, string detail)
        {
            _fail++;
            _failed.Add(check);
            Debug.LogError("UFLOW FAIL " + check + ": " + detail);
        }

        void Report(float secs)
        {
            // a picture nobody can read is not evidence: the shutter's own findings
            // become failures here rather than a line nobody counts
            for (int i = 0; i < UnityShotsCamera.Flat.Count; i++)
                Fail("shot_flat", UnityShotsCamera.Flat[i]
                     + " is one flat colour — no screen was in that frame");
            for (int i = 0; i < UnityShotsCamera.Failed.Count; i++)
                Fail("shot_failed", UnityShotsCamera.Failed[i] + " was never written");
            for (int i = 0; i < UnityShotsPoke.Misses.Count; i++)
                Fail("poke_miss", UnityShotsPoke.Misses[i]);
            for (int i = 0; i < UnityFlowReach.Misses.Count; i++)
                Fail("poke_miss", UnityFlowReach.Misses[i]);

            Debug.Log(string.Format("UFLOW walked {0:0}s · {1} shots -> {2}",
                secs, UnityShotsCamera.Written.Count, _dir));
            if (UnityShotsCamera.WrongSize.Count > 0)
                Debug.LogWarning(string.Format(
                    "UFLOW {0} shot(s) are not {1}x{2} — run the player with -screen-width {1} "
                    + "-screen-height {2} -screen-fullscreen 0", UnityShotsCamera.WrongSize.Count,
                    UnityShotsCamera.TwinWidth, UnityShotsCamera.TwinHeight));
            if (_failed.Count > 0)
                Debug.LogError("UFLOW failed checks: " + string.Join(" · ", _failed.ToArray()));
            Debug.Log(string.Format("UFLOW DONE pass={0} fail={1}", _pass, _fail));
        }

        static void Leave(int code)
        {
#if UNITY_EDITOR
            Debug.Log("UFLOW leaving play mode (exit code would be " + code + ")");
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(code);
#endif
        }

        // ══ the shutter ════════════════════════════════════════════════════════

        /// UnityShots' shutter, unchanged: WaitForEndOfFrame, grab, write, and judge the
        /// luminance spread so a black rectangle can never pass for a screen.
        IEnumerator Shoot(string tag, string name)
        {
            int n;
            _shotN.TryGetValue(tag, out n);
            n += 1;
            _shotN[tag] = n;
            yield return UnityShotsCamera.Shoot(_dir,
                string.Format("{0}_{1:00}_{2}", tag, n, name));
        }

        /// The Godot window is 1536x1024 and every reference PNG is that size.
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
                Debug.LogWarning("UFLOW could not size the window (" + e.Message + ").");
            }
        }

        // ══ waiting ════════════════════════════════════════════════════════════

        /// Poll a condition to a wall. Never throws out of a screen that went away
        /// mid-check — a torn-down screen reads as "not yet", and the wall ends it.
        IEnumerator Until(Func<bool> cond, float capSecs, string what)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < capSecs)
            {
                bool ok = false;
                try { ok = cond(); }
                catch (Exception e)
                {
                    Debug.LogWarning("UFLOW: checking " + what + " threw " + e.Message);
                }
                if (ok) yield break;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Debug.LogWarning(string.Format("UFLOW waited {0:0}s for {1} and gave up",
                                           capSecs, what));
        }

        // ══ where the run is ═══════════════════════════════════════════════════

        static GameState St
        {
            get { return RunDriver.Current != null ? RunDriver.Current.State : null; }
        }

        static bool RoomAlive()
        {
            Boot b = Boot.Instance;
            GameState s = St;
            return b != null && b.State == AppState.Garage
                   && GarageScreen.Room != null && !GarageScreen.Room.Over
                   && s != null && !s.Dead;
        }

        static bool TurnBusyNow()
        {
            TurnRunner r = TurnRunner.Get();
            return r != null && r.TurnBusy;
        }

        /// POKE — `GarageScreen._paintRibbon`. The ribbon is the room SAYING it is
        /// unfinished; there is no public way to ask, and "authored room + ribbon" is
        /// half of what C1 accepts.
        static bool RibbonUp()
        {
            GarageScreen r = GarageScreen.Room;
            if (r == null) return false;
            var t = UnityFlowReach.Field(r, "_paintRibbon") as TextMeshProUGUI;
            return t != null;
        }

        static bool RoomComposed()
        {
            GarageScreen r = GarageScreen.Room;
            return r != null && !string.IsNullOrEmpty(r.ComposedPath);
        }

        // ══ the journal's model ════════════════════════════════════════════════

        /// POKE — `GarageScreen._spreads`. The book is built and owned inside the room;
        /// nothing public hands it out, and every week check needs it.
        static JournalSpreads Spreads(GarageScreen room)
        {
            if (room == null) return null;
            return UnityShotsPoke.GetField(room, "_spreads") as JournalSpreads;
        }

        /// POKE — `JournalSpreads._commit`. `WeekCommit` itself is public: one hop and
        /// the rest of the week is driven through its own public door.
        static WeekCommit Commit(GarageScreen room)
        {
            JournalSpreads sp = Spreads(room);
            if (sp == null) return null;
            return UnityShotsPoke.GetField(sp, "_commit") as WeekCommit;
        }

        /// POKE — `JournalSpreads._jp`. The live page; it is REPLACED on every spread
        /// turn and on every clarify redraw, so it is always re-read, never cached.
        static JournalPage Page(GarageScreen room)
        {
            JournalSpreads sp = Spreads(room);
            if (sp == null) return null;
            return UnityShotsPoke.GetField(sp, "_jp") as JournalPage;
        }

        /// POKE — `JournalSpreads._pageI` (read).
        static int PageIndex(JournalSpreads sp)
        {
            if (sp == null) return -1;
            object v = UnityShotsPoke.GetField(sp, "_pageI");
            return v is int ? (int)v : -1;
        }

        /// A player turns the page with the drawn arrow. The forward arrow is the one
        /// further right; the left one CLOSES the book on spread 0, so the wrong pick
        /// would end the week rather than open it.
        IEnumerator TurnToDecisionSpread(GarageScreen room)
        {
            JournalSpreads sp = Spreads(room);
            JournalPage page = Page(room);
            if (page != null && page.Space != null)
            {
                RectTransform best = null;
                for (int i = 0; i < page.Space.childCount; i++)
                {
                    var c = page.Space.GetChild(i) as RectTransform;
                    if (c == null || c.name != "arrow") continue;
                    if (best == null || c.anchoredPosition.x > best.anchoredPosition.x) best = c;
                }
                if (best != null)
                {
                    var b = best.GetComponent<Button>();
                    if (b != null)
                    {
                        b.onClick.Invoke();
                        yield return new WaitForSecondsRealtime(1.0f);
                    }
                }
            }
            if (PageIndex(sp) != 1)
            {
                // POKE — the .gd twin: `gv._page_i = 1; gv._show_spread()`. Only ever
                // reached when the drawn arrow could not be found or did not take.
                UnityShotsPoke.SetField(sp, "_pageI", 1);
                UnityShotsPoke.Call(sp, "ShowSpread");
                yield return new WaitForSecondsRealtime(0.7f);
            }
            if (PageIndex(sp) != 1)
                Fail("c2_decision_page", "the journal would not turn to THE WEEK AHEAD");
        }

        /// POKE — `WeekCommit._lockBtn`. Pressing the real button plays the real
        /// ceremony (the pen strike, the scrap burst) and then commits, which is what a
        /// player's press does; `CommitFromText()` alone would skip the ceremony.
        void PressLock(WeekCommit commit)
        {
            if (commit == null) return;
            var btn = UnityShotsPoke.GetField(commit, "_lockBtn") as Button;
            if (btn != null)
            {
                if (!btn.interactable)
                    Debug.LogWarning("UFLOW: the lock row is not ready (it reads "
                                     + "'...decide first') — pressing it anyway");
                btn.onClick.Invoke();
                return;
            }
            Debug.LogWarning("UFLOW: no lock row on the page — committing straight, "
                             + "the twin of gv._commit_from_text()");
            commit.CommitFromText();
        }

        /// POKE — `WeekCommit._pendingDice`. THE ENGINE'S number, read at the press and
        /// before the DM has answered, so C6 compares the die that was cast with the die
        /// that was shown rather than two copies of the same field.
        static JObject PendingDice(WeekCommit c)
        {
            if (c == null) return null;
            return UnityFlowReach.Field(c, "_pendingDice") as JObject;   // frame-polled
        }

        static JObject LastDice()
        {
            RunDriver d = RunDriver.Current;
            JObject o = d != null ? d.LastOutcome : null;
            JObject dm = o != null ? o["dm"] as JObject : null;
            return dm != null ? dm["dice"] as JObject : null;
        }

        // ══ the clarify chips ══════════════════════════════════════════════════

        /// Tap the world's own answer chip, because a price chip is the ONLY path that
        /// writes the price into `GameState.Offers` — answering in prose would leave the
        /// engine unpriced and C3's second half unproved.
        IEnumerator AnswerClarify(GarageScreen room, string kind, string tag, int week)
        {
            yield return new WaitForSecondsRealtime(0.7f);   // Redraw() rebuilt the page
            JournalPage page = Page(room);
            WeekCommit commit = Commit(room);
            if (page == null || commit == null)
            {
                Fail("c3_answer", "the clarify page could not be reached to answer it");
                yield break;
            }
            string prefix = kind == "price" ? "prc:" : (kind == "amount" ? "clr:" : "");
            bool tapped = false;
            if (prefix.Length > 0) tapped = PressChip(page, prefix, 1);
            if (!tapped)
            {
                if (prefix.Length > 0)
                    Fail("c3_chips", "the world asked a '" + kind + "' question and put no "
                         + prefix + " chips on the page to answer it with");
                commit.AnswerClarify(kind == "amount"
                    ? "budget: $1,500"
                    : "we price it at the street price and say so out loud");
            }
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shoot(tag, "w" + Two(week) + "_clarify_answered");
        }

        /// POKE — `JournalPage._rowIds`. `IconRow` registers the ids it laid out against
        /// the row it laid them in; that map is the only way to tell the price chips from
        /// the term sheets, the level-up stats and the delta strip on the same page.
        static bool PressChip(JournalPage page, string idPrefix, int which)
        {
            var map = UnityShotsPoke.GetField(page, "_rowIds") as IDictionary;
            if (map == null) return false;
            // Keys/indexer, never `foreach (DictionaryEntry …)`: a Dictionary<K,V> reached
            // through IDictionary yields boxed KeyValuePairs from IEnumerable.GetEnumerator,
            // and the DictionaryEntry cast throws at runtime.
            var keys = new List<object>();
            foreach (object k in map.Keys) keys.Add(k);
            for (int ki = 0; ki < keys.Count; ki++)
            {
                var row = keys[ki] as RectTransform;
                var ids = map[keys[ki]] as IList;
                if (row == null || ids == null || ids.Count == 0) continue;
                int first = -1;
                for (int i = 0; i < ids.Count; i++)
                {
                    string id = ids[i] as string;
                    if (id != null && id.StartsWith(idPrefix, StringComparison.Ordinal))
                    {
                        first = i;
                        break;
                    }
                }
                if (first < 0) continue;
                int pick = Mathf.Clamp(which, first, ids.Count - 1);
                if (pick >= row.childCount) pick = row.childCount - 1;
                if (pick < 0) continue;
                var slot = row.GetChild(pick);
                var btn = slot != null ? slot.GetComponent<Button>() : null;
                if (btn == null) continue;
                Debug.Log("UFLOW taps chip " + (ids[pick] as string));
                btn.onClick.Invoke();
                return true;
            }
            return false;
        }

        // ══ the reading beat ═══════════════════════════════════════════════════

        static ReadingBeat BeatNow()
        {
            Boot b = Boot.Instance;
            if (b == null || b.TopLayer == null) return null;
            return b.TopLayer.GetComponentInChildren<ReadingBeat>(true);
        }

        static DiceRoll CupNow()
        {
            Boot b = Boot.Instance;
            if (b == null || b.TopLayer == null) return null;
            return b.TopLayer.GetComponentInChildren<DiceRoll>(true);
        }

        /// READ THE BEAT LIKE A PLAYER, then look up.
        /// POKE — `ReadingBeat.SkipReading()` (the click that catches the text up) and
        /// `ReadingBeat._proceed` (the click that closes it). The .gd twin is
        /// `_beat.set("_proceed", true)` on a loop; `_proceed` is set to false again
        /// inside `Finish()`, which is exactly why this keeps setting it.
        IEnumerator DrainBeat(float cap)
        {
            if (BeatNow() == null) yield break;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < cap)
            {
                ReadingBeat b = BeatNow();
                if (b == null) yield break;
                UnityFlowReach.Call(b, "SkipReading");            // polled, not one-shot
                UnityFlowReach.SetField(b, "_proceed", true);
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Debug.LogWarning(string.Format(
                "UFLOW: the reading beat would not close in {0:0}s", cap));
        }

        /// POKE — `ReadingBeat._bodies`. The judgement sentence the player actually
        /// reads ("The die came up 14.") is one of these labels; it is the SHOWN number,
        /// independent of the cup.
        static int DieFromBeat(ReadingBeat beat)
        {
            if (beat == null) return -1;
            var list = UnityShotsPoke.GetField(beat, "_bodies") as IList;
            if (list == null) return -1;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i] as TextMeshProUGUI;
                if (t == null) continue;
                int n = DieInSentence(t.text);
                if (n > 0) return n;
            }
            return -1;
        }

        static int DieInSentence(string s)
        {
            const string lead = "The die came up ";
            if (string.IsNullOrEmpty(s)) return -1;
            int i = s.IndexOf(lead, StringComparison.Ordinal);
            if (i < 0) return -1;
            return FirstInt(s, i + lead.Length);
        }

        /// POKE — `DiceRoll._loop`, then `SheetLoop._inflight` and `SheetLoop._sheet`.
        /// The cup plays `dice/roll_NN.png`, so the sheet it asked for IS the number the
        /// player watched settle. Two readings because the dice sheets are NOT baked
        /// into `Resources/Sheets` (only birth/curtain/howto are), so the streamed
        /// texture carries no name and only the in-flight path names the file — which is
        /// why this is frame-polled from the moment the die is cast. If neither answers,
        /// the beat's judgement sentence carries C6 instead.
        static int CupNumber()
        {
            DiceRoll cup = CupNow();
            if (cup == null) return -1;
            var loop = UnityFlowReach.Field(cup, "_loop") as SheetLoop;   // frame-polled
            if (loop == null) return -1;
            var inflight = UnityFlowReach.Field(loop, "_inflight") as string;
            int n = RollNumber(inflight);
            if (n > 0) return n;
            var sheet = UnityFlowReach.Field(loop, "_sheet") as Texture2D;
            return sheet == null ? -1 : RollNumber(sheet.name);
        }

        /// "dice/roll_07.png" or "roll_07" -> 7.
        static int RollNumber(string s)
        {
            if (string.IsNullOrEmpty(s)) return -1;
            int u = s.LastIndexOf('_');
            if (u < 0) return -1;
            return FirstInt(s, u + 1);
        }

        void CompareDice(int week, int engine, int cup, int beat)
        {
            Debug.Log(string.Format("UFLOW wk{0} die · engine={1} · cup={2} · beat={3}",
                week, engine, cup < 0 ? "unreadable" : cup.ToString(),
                beat < 0 ? "unreadable" : beat.ToString()));
            if (engine <= 0)
            {
                Fail("c6_engine_die", "no d20 was cast at the press for week " + week
                     + " — the roll is not final at the commit");
                return;
            }
            if (cup < 0 && beat < 0)
            {
                Fail("c6_die_shown", "week " + week + ": the engine rolled " + engine
                     + " and neither the cup's sheet nor the beat's judgement line could be "
                     + "read, so the number the player SAW is unverifiable");
                return;
            }
            if (cup >= 0)
            {
                if (cup == engine) Pass("c6_die_cup");
                else Fail("c6_die_cup", string.Format(
                    "week {0}: the cup played roll_{1:00} and the engine rolled {2}",
                    week, cup, engine));
            }
            if (beat >= 0)
            {
                if (beat == engine) Pass("c6_die_beat");
                else Fail("c6_die_beat", string.Format(
                    "week {0}: the beat told the player the die came up {1} and the engine "
                    + "rolled {2}", week, beat, engine));
            }
        }

        // ══ C7 — the card ══════════════════════════════════════════════════════

        void CheckEventCard(GarageScreen room, int week)
        {
            JObject ev = room != null ? room.CurrentEvent : null;
            string title = ContentDb.Str(ev, "title").Trim();
            string body = ContentDb.Str(ev, "body").Trim();
            Debug.Log(string.Format("UFLOW wk{0} card: '{1}' — {2}", week, title, Left(body, 140)));

            if (body.Length == 0)
            {
                Fail("c7_event_body", "week " + week
                     + " opened with no situation at all — the card has an empty body");
            }
            else
            {
                Pass("c7_event_body");
                if (_seenBodies.Contains(body))
                    Fail("c7_no_repeat", "week " + week + " dealt a body this run has already "
                         + "shown: '" + Left(body, 100) + "'");
                else Pass("c7_no_repeat");
                _seenBodies.Add(body);
            }
            if (title.Length > 0)
            {
                if (_seenTitles.Contains(title))
                    Fail("c7_no_repeat_title", "week " + week + " repeats the lead '"
                         + title + "'");
                else Pass("c7_no_repeat_title");
                _seenTitles.Add(title);
            }
        }

        // ══ C4 — the money law ═════════════════════════════════════════════════

        /// WHAT THIS CAN AND CANNOT SEE. `State.Cash` moves three times inside one lock:
        /// the weekly tick (rent, payroll, revenue), the DM's own ops, and the milestone
        /// bookkeeping — and only the middle one is what C4 is about. So the EXACTNESS
        /// claim is measured on the engine's own receipt line, which is written by the
        /// `spend` op itself ("spent $1500 on …" / "the bank stopped it at $X (wanted
        /// $Y)"), and the whole arithmetic is printed beside it so a human can settle
        /// anything the harness cannot.
        void CheckMoney(string move, int cashAtPress, string era)
        {
            int written = AmountIn(move);
            RunDriver drv = RunDriver.Current;
            JObject outcome = drv != null ? drv.LastOutcome : null;
            JObject dm = outcome != null ? outcome["dm"] as JObject : null;
            var effects = dm != null ? dm["effects"] as JArray : null;
            var decLog = outcome != null ? outcome["dec_log"] as JArray : null;
            int cashNow = St != null ? St.Cash : cashAtPress;
            int cap = SimEngine.EraSpendCap(era);

            int asked = -1;
            int cashOps = 0;
            if (effects != null)
                foreach (JToken t in effects)
                {
                    var e = t as JObject;
                    string op = ContentDb.Str(e, "op");
                    if (op == "spend") asked = ContentDb.Int(e, "v", 0);
                    else if (op == "cash_delta") cashOps += ContentDb.Int(e, "v", 0);
                    else if (op == "take_loan") cashOps += ContentDb.Int(e, "v", 0);
                }

            int receipt = -1;
            bool clamped = false;
            if (decLog != null)
                foreach (JToken t in decLog)
                {
                    string line = t != null ? t.ToString() : "";
                    if (line.StartsWith("spent $", StringComparison.Ordinal))
                        receipt = FirstInt(line, 7);
                    if (line.IndexOf("the bank stopped it at", StringComparison.Ordinal) >= 0)
                        clamped = true;
                }

            Debug.Log(string.Format(
                "UFLOW money · written ${0} · DM spend ${1} · other cash ops {2} · receipt ${3}"
                + "{4} · era cap ${5} · cash ${6} -> ${7}",
                written < 0 ? "-" : written.ToString(), asked < 0 ? "-" : asked.ToString(),
                cashOps, receipt < 0 ? "-" : receipt.ToString(), clamped ? " (CLAMPED)" : "",
                cap, cashAtPress, cashNow));

            if (written <= 0) return;   // this week's move named no number; nothing to prove

            // the world's own tick moved cash too (rent, payroll, revenue); its receipts
            // are printed here so the arithmetic above can be settled by hand
            var tick = outcome != null ? outcome["log"] as JArray : null;
            if (tick != null)
                foreach (JToken t in tick) Debug.Log("UFLOW   tick: " + t);

            if (asked < 0 && receipt < 0 && cashOps == 0)
            {
                Fail("c4_spend_lands", string.Format(
                    "the move named ${0} and the week produced no spend op, no spend receipt "
                    + "and no cash op at all — the written money never reached the ledger",
                    written));
                return;
            }

            // the DM must name the number the founder wrote
            if (asked == written || Mathf.Abs(cashOps) == written) Pass("c4_written_amount");
            else Fail("c4_written_amount", string.Format(
                "the founder wrote ${0} and the world spent ${1} (other cash ops {2})",
                written, asked < 0 ? 0 : asked, cashOps));

            // the engine's own law: want = clamp(v, 0, era cap); can = min(want, cash)
            int lawful = Mathf.Min(asked < 0 ? 0 : asked, cap);
            if (receipt >= 0)
            {
                if (receipt == lawful) Pass("c4_debits_exactly");
                else if (clamped && receipt < lawful) Pass("c4_debits_exactly");
                else Fail("c4_debits_exactly", string.Format(
                    "the receipt says ${0}; the era cap ({1}, ${2}) and the ask (${3}) make "
                    + "${4} the lawful debit, and no clamp line was printed",
                    receipt, era, cap, asked < 0 ? 0 : asked, lawful));

                if (asked > cap)
                {
                    if (receipt <= cap) Pass("c4_era_clamp");
                    else Fail("c4_era_clamp", string.Format(
                        "the {0} spend cap is ${1} and ${2} left the drawer", era, cap, receipt));
                }
            }
            else if (asked > 0)
            {
                Fail("c4_debits_exactly", "a spend op for $" + asked
                     + " produced no 'spent $…' receipt in the week's decision log");
            }

            if (cashNow != cashAtPress) Pass("c4_cash_moved");
            else Fail("c4_cash_moved", string.Format(
                "cash is still exactly ${0} after a ${1} week — not even rent came out",
                cashNow, written));
        }

        // ══ the week read back ═════════════════════════════════════════════════

        void CheckWasPage(int week)
        {
            RunDriver drv = RunDriver.Current;
            JObject outcome = drv != null ? drv.LastOutcome : null;
            string narration = ContentDb.Str(outcome, "narration").Trim();
            string verdict = ContentDb.Str(outcome, "verdict").Trim();
            var decLog = outcome != null ? outcome["dec_log"] as JArray : null;
            int receipts = decLog != null ? decLog.Count : 0;
            Debug.Log(string.Format("UFLOW wk{0} outcome · verdict '{1}' · narration {2} chars "
                                    + "· {3} receipt(s)", week, verdict, narration.Length, receipts));
            if (narration.Length == 0 && receipts == 0)
                Fail("c2_effects", "week " + week + " came back with neither narration nor a "
                     + "single effect receipt — the lock produced nothing to read");
            else Pass("c2_effects");
            if (decLog != null)
                foreach (JToken t in decLog) Debug.Log("UFLOW   receipt: " + t);
        }

        // ══ the book ═══════════════════════════════════════════════════════════

        /// POKE — `BookIntroScreen._entry`. The page's own label is the only place the
        /// entry exists once it has landed; `BookShowedEntry` says an entry arrived,
        /// this says WHAT is on the paper, which is what C1 is about.
        static string EntryText(Boot app)
        {
            var book = app != null ? app.CurrentScreen as BookIntroScreen : null;
            if (book == null) return "";
            var t = UnityShotsPoke.GetField(book, "_entry") as TextMeshProUGUI;
            if (t == null) return "";
            string s = (t.text ?? "").Trim();
            if (s == BookPlaceholder) return "";
            return s;
        }

        static bool EntryLanded(Boot app)
        {
            RunDriver d = RunDriver.Current;
            if (d != null && d.BookShowedEntry) return true;
            return EntryText(app).Length > 0;
        }

        // ══ the draft's seven pages ════════════════════════════════════════════

        /// POKE — `FounderDraftScreen._sign` then `DraftSignPage._founderEdit`. Setting
        /// `FounderName` alone would leave the DEALT name on the paper; the page's own
        /// `SetValue` types it, and its `Changed` handler writes the model exactly as a
        /// keystroke would.
        void TypeFounderName(FounderDraftScreen d, string name)
        {
            d.FounderName = name;
            object sign = UnityShotsPoke.GetField(d, "_sign");
            if (sign == null) return;
            var edit = UnityShotsPoke.GetField(sign, "_founderEdit") as PaperInput;
            if (edit != null) edit.SetValue(name);
        }

        /// POKE — `FounderDraftScreen._name` then `DraftNamePage._nameEdit/_ideaEdit`.
        void TypePitch(FounderDraftScreen d, string company, string idea)
        {
            d.CompanyName = company;
            d.CompanyIdea = idea;
            object page = UnityShotsPoke.GetField(d, "_name");
            if (page == null) return;
            var nameEdit = UnityShotsPoke.GetField(page, "_nameEdit") as PaperInput;
            var ideaEdit = UnityShotsPoke.GetField(page, "_ideaEdit") as PaperInput;
            if (nameEdit != null) nameEdit.SetValue(company);
            if (ideaEdit != null) ideaEdit.SetValue(idea);
        }

        /// POKE — `FounderDraftScreen._shape` then `DraftShapePage.Restyle()`. The model
        /// fields are public; only the "which card is circled" refresh is not.
        void PickShape(FounderDraftScreen d, string what, string who)
        {
            d.BizWhat = what;
            d.BizWho = who;
            object shape = UnityShotsPoke.GetField(d, "_shape");
            if (shape != null) UnityShotsPoke.Call(shape, "Restyle");
        }

        void PickFunding(FounderDraftScreen d, int index)
        {
            if (d.Fundings == null || d.Fundings.Count == 0)
            {
                Fail("c1_draft_money", "no funding cards on the shelf — the launch door "
                     + "cannot open");
                return;
            }
            int k = Mathf.Clamp(index, 0, d.Fundings.Count - 1);
            d.SelFund = d.Fundings[k] as JObject;   // the card's own onClick, both lines
            d.RefreshCapLine();
        }

        /// POKE — `DraftBagPage.Toggle(id, cost)`. The twin of `d._toggle_bag(iid, 1,
        /// d._bag_btns[iid])`, and the same call UnityShots makes for traits_bag.
        void PackTheBag(FounderDraftScreen d)
        {
            DraftBagPage bag = d.BagPage;
            if (bag == null)
            {
                Fail("c1_draft_bag", "page 6 did not come up as a DraftBagPage");
                return;
            }
            for (int i = 0; i < BagPicks.Length; i++)
            {
                string id = BagPicks[i];
                if (d.Deck == null || d.Deck.Item(id) == null)
                {
                    Debug.LogWarning("UFLOW: no shelf tile " + id + " for this trade — skipped");
                    continue;
                }
                UnityShotsPoke.Call(bag, "Toggle", id, d.Deck.CarryCost(id));
            }
            if (d.Bag.Count == 0)
                Fail("c1_draft_bag", "nothing was packed — the shelf refused every tile");
            else Pass("c1_draft_bag");
        }

        // ══ the title's slot table ═════════════════════════════════════════════

        /// The slot cards are named `slot1`..`slot3` and each one carries the Button
        /// that answers NEW GAME. Pressing it is a real click, not a poke.
        static bool PressSlotCard(AppScreen title, int slot)
        {
            if (title == null || title.Rect == null) return false;
            Transform t = FindDeep(title.Rect, "slot" + slot);
            if (t == null) return false;
            var b = t.GetComponent<Button>();
            if (b == null) return false;
            b.onClick.Invoke();
            return true;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDeep(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        // ══ reading the written move ═══════════════════════════════════════════

        static int UnpricedOffers(GameState st)
        {
            if (st == null || st.Offers == null) return 0;
            int n = 0;
            for (int i = 0; i < st.Offers.Count; i++)
                if (st.Offers[i] != null && st.Offers[i].Price <= 0.0) n++;
            return n;
        }

        static bool Sells(string move)
        {
            string low = (move ?? "").ToLower();
            return low.Contains("sell") || low.Contains("customer") || low.Contains("paying");
        }

        static bool Spends(string move)
        {
            string low = (move ?? "").ToLower();
            return low.Contains("ads") || low.Contains("spend") || low.Contains("marketing")
                   || low.Contains("budget") || low.Contains("buy");
        }

        static bool HasAmount(string move) { return AmountIn(move) > 0; }
        static bool HasNoAmount(string move) { return !HasAmount(move); }

        /// The first "$1,500" in a sentence, as 1500. -1 when there is no number.
        static int AmountIn(string s)
        {
            if (string.IsNullOrEmpty(s)) return -1;
            int i = s.IndexOf('$');
            if (i < 0) return -1;
            return FirstInt(s, i + 1);
        }

        /// Digits from `at`, commas skipped. -1 when there are none.
        static int FirstInt(string s, int at)
        {
            if (string.IsNullOrEmpty(s) || at < 0 || at >= s.Length) return -1;
            var sb = new StringBuilder();
            for (int k = at; k < s.Length && sb.Length < 9; k++)
            {
                char c = s[k];
                if (c >= '0' && c <= '9') sb.Append(c);
                else if (c == ',' && sb.Length > 0) continue;
                else break;
            }
            int v;
            return sb.Length > 0 && int.TryParse(sb.ToString(), out v) ? v : -1;
        }

        static string Left(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " / ");
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }

        static string Two(int n) { return n < 10 ? "0" + n : n.ToString(); }

        /// A file-name-safe scrap of a status line ("painting the room" -> "painting_the_room").
        static string Slug(string s)
        {
            if (string.IsNullOrEmpty(s)) return "none";
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length && sb.Length < 28; i++)
            {
                char c = char.ToLowerInvariant(s[i]);
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
            }
            string outp = sb.ToString().Trim('_');
            return outp.Length == 0 ? "none" : outp;
        }
    }

    /// <summary>
    /// THE POLLED REACH-INS — the same reflection `UnityShotsPoke` does, with one
    /// difference that matters here and not there: a MISS IS RECORDED ONCE.
    ///
    /// UnityShots pokes a member a handful of times, so a rename is a handful of lines.
    /// This lane FRAME-POLLS four of them — `WeekCommit._pendingDice` while the die is
    /// being cast, the cup's sheet while it loads, the room's paint ribbon while it
    /// paints, `ReadingBeat._proceed` while the week is read — which is thousands of
    /// reads per run. Through UnityShotsPoke a single renamed field would bury the log
    /// under seven thousand identical errors and every one of them would be counted as
    /// a failure. So the polled reads live here: the FieldInfo is resolved once per
    /// type+member, the miss is shouted once, and `UnityFlow.Report` counts it once.
    /// Everything one-shot still goes through UnityShotsPoke, whose miss list this
    /// report also carries.
    /// </summary>
    public static class UnityFlowReach
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static
                                 | BindingFlags.Public | BindingFlags.NonPublic
                                 | BindingFlags.DeclaredOnly;

        static readonly Dictionary<string, FieldInfo> _fields = new Dictionary<string, FieldInfo>();
        static readonly Dictionary<string, MethodInfo> _methods = new Dictionary<string, MethodInfo>();

        /// Every member this run could not reach, once each.
        public static readonly List<string> Misses = new List<string>();

        public static object Field(object target, string name)
        {
            FieldInfo f = FieldOf(target, name);
            if (f == null) return null;
            try { return f.GetValue(f.IsStatic ? null : target); }
            catch (Exception) { return null; }
        }

        public static void SetField(object target, string name, object value)
        {
            FieldInfo f = FieldOf(target, name);
            if (f == null) return;
            try { f.SetValue(f.IsStatic ? null : target, value); }
            catch (Exception) { /* a field that will not take the value already missed */ }
        }

        public static void Call(object target, string name)
        {
            if (target == null) return;
            Type t = target.GetType();
            string key = t.FullName + "." + name + "()";
            MethodInfo m;
            if (!_methods.TryGetValue(key, out m))
            {
                m = MethodOf(t, name);
                _methods[key] = m;
                if (m == null) Miss(key);
            }
            if (m == null) return;
            try { m.Invoke(m.IsStatic ? null : target, null); }
            catch (Exception) { /* the screen went away under the poll; the wall ends it */ }
        }

        static FieldInfo FieldOf(object target, string name)
        {
            if (target == null) return null;
            Type t = target.GetType();
            string key = t.FullName + "." + name;
            FieldInfo f;
            if (_fields.TryGetValue(key, out f)) return f;
            Type walk = t;
            while (walk != null)
            {
                f = walk.GetField(name, Any);
                if (f != null) break;
                walk = walk.BaseType;
            }
            _fields[key] = f;
            if (f == null) Miss(key);
            return f;
        }

        static MethodInfo MethodOf(Type t, string name)
        {
            while (t != null)
            {
                MethodInfo[] all = t.GetMethods(Any);
                for (int i = 0; i < all.Length; i++)
                    if (all[i].Name == name && all[i].GetParameters().Length == 0) return all[i];
                t = t.BaseType;
            }
            return null;
        }

        static void Miss(string key)
        {
            Misses.Add(key);
            Debug.LogError("UFLOW POKE MISS: " + key
                           + " — the member this harness polls has been renamed or removed");
        }
    }

    /// <summary>
    /// BUG-15, IN UNITY. The Godot note is exact: "a test run must not delete the game
    /// the owner has in progress". This walk deliberately does NOT put Boot into harness
    /// mode, which means two shipped behaviours now point at the owner's desk —
    /// `Driver.ClearRun()` on the NEW GAME card, and `SaveIfWeekTurned()` once a week.
    ///
    /// So all three slots are copied before the first frame, to memory AND to a file in
    /// the shot directory (a crash then still leaves the owner a copy), and put back at
    /// the end and again on the quit path. A slot that did not exist is DELETED on
    /// restore rather than left holding the harness's company. The how-to mark is
    /// treated the same way, exactly as howto_shot.gd treats its own.
    /// </summary>
    public static class UnityFlowGuard
    {
        const string HowToMark = "seen_howto_v2.unity";

        static readonly string[] _slots = new string[SaveSlots.SlotCount];
        static int _activeSlot = 1;
        static bool _hadHowTo;
        static bool _taken;
        static bool _restored;

        public static void BackUp(string dir)
        {
            if (_taken) return;
            _taken = true;
            _activeSlot = SaveSlots.ActiveSlot;
            for (int i = 0; i < _slots.Length; i++)
            {
                string path = SaveSlots.Path(i + 1);
                _slots[i] = RunwayPaths.ReadAllTextOrEmpty(path);
                if (_slots[i].Length == 0) continue;
                string copy = Path.Combine(dir, string.Format("_slot{0}.backup.json", i + 1));
                if (!RunwayPaths.WriteAllText(copy, _slots[i]))
                    Debug.LogError("UFLOW could not park a copy of slot " + (i + 1)
                                   + " at " + copy + " — its run is only held in memory now.");
            }
            _hadHowTo = Exists(RunwayPaths.User(HowToMark));
            Debug.Log(string.Format(
                "UFLOW guard: slots [{0}] copied · active slot {1} · how-to mark {2}",
                Occupancy(), _activeSlot, _hadHowTo ? "present" : "absent"));
        }

        public static void Restore()
        {
            if (!_taken || _restored) return;
            _restored = true;
            for (int i = 0; i < _slots.Length; i++)
            {
                string path = SaveSlots.Path(i + 1);
                if (_slots[i].Length > 0)
                {
                    if (!RunwayPaths.WriteAllText(path, _slots[i]))
                        Debug.LogError("UFLOW COULD NOT RESTORE slot " + (i + 1)
                                       + " — its saved run is gone. A copy is in the shot "
                                       + "directory as _slot" + (i + 1) + ".backup.json.");
                }
                else
                {
                    Delete(path);   // it was an empty desk before the walk; leave it empty
                }
            }
            SaveSlots.ActiveSlot = _activeSlot;
            string mark = RunwayPaths.User(HowToMark);
            if (_hadHowTo) RunwayPaths.WriteAllText(mark, "1");
            else Delete(mark);
            Debug.Log("UFLOW guard: the desk is back as it was found.");
        }

        static string Occupancy()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _slots.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(_slots[i].Length > 0 ? "full" : "empty");
            }
            return sb.ToString();
        }

        static bool Exists(string path)
        {
            try { return File.Exists(path); }
            catch (Exception) { return false; }
        }

        static void Delete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception e)
            {
                Debug.LogWarning("UFLOW cannot remove " + path + ": " + e.Message);
            }
        }
    }
}
#endif
