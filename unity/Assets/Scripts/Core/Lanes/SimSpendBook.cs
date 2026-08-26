using System;
using System.Collections.Generic;

namespace Runway.Core
{
    /// <summary>
    /// LANE HELPER — THE ORG SPEND BOOK's state math + the money desks' shared
    /// display reads (DAG2 W2 L-MONEY). PURE and rng-free: no tick seams, no
    /// salts — the money desks call these, and the twin suites pin them
    /// (unity/Runway.Core.Tests/Lanes/MoneyDesksTests.cs ·
    /// game/tests/lanes/test_money_desks.gd).
    ///
    /// THE BOOK IS THE LEVER: each org bucket's engine value = the SUM of its
    /// lines' LIVE spend; this file only keeps state.Budgets equal to that
    /// sum. The generated `amt` is a SUGGESTION (coordinator ruling): levers
    /// start at 0 and the player ADOPTS through the receipt path — never
    /// auto-seeded, so week-1 economics match a tree without this file.
    ///
    /// Line schema: name/buys/amt/bucket/contract_notice/division (the
    /// spine's) + Live and StopWk as NULLABLES — both engines write these
    /// keys only once a line is touched (byte-identical save keys).
    ///
    /// TWIN: game/src/core/lanes/sim_spend_book.gd — same order, same math.
    /// </summary>
    public static class SimSpendBook
    {
        public static readonly string[] Buckets = { "sales", "care", "rnd", "office" };

        /// The section words the sheet prints — bucket → "closing — sales" etc.
        public static string BucketWord(string bucket)
        {
            switch (bucket)
            {
                case "sales": return "closing — sales";
                case "care": return "retention — care";
                case "rnd": return "building — r&d";
                default: return "people — office";
            }
        }

        /// The add door closes here (birth writes at most 10 rows).
        public const int BookCap = 12;

        // ═══════════════════════ the book itself ═══════════════════════════

        /// The bare four-line book — world-gen's own default, duplicated here
        /// so an old save that predates the birth book still opens a sheet.
        public static List<SpendLine> BareBook()
        {
            return new List<SpendLine>
            {
                new SpendLine { Name = "sales", Buys = "closing what is already in the pipe", Amt = 0, Bucket = "sales" },
                new SpendLine { Name = "care", Buys = "keeping the customers we have", Amt = 0, Bucket = "care" },
                new SpendLine { Name = "r&d", Buys = "building the thing", Amt = 0, Bucket = "rnd" },
                new SpendLine { Name = "office", Buys = "the room and the people in it", Amt = 0, Bucket = "office" },
            };
        }

        public static void EnsureBook(GameState state)
        {
            if (state.SpendBook == null) state.SpendBook = new List<SpendLine>();
            if (state.SpendBook.Count == 0) state.SpendBook = BareBook();
        }

        /// The line's REAL weekly spend. Null until the desk touches it.
        public static int LiveOf(SpendLine line) { return line.Live ?? 0; }

        /// A contract line the player stopped: it bills through its notice.
        public static bool IsStopping(SpendLine line) { return line.StopWk.HasValue; }

        /// The indices of a bucket's lines, in book order.
        public static List<int> LinesOf(GameState state, string bucket)
        {
            var outI = new List<int>();
            for (int i = 0; i < state.SpendBook.Count; i++)
                if ((state.SpendBook[i].Bucket ?? "office") == bucket) outI.Add(i);
            return outI;
        }

        public static int BucketLive(GameState state, string bucket)
        {
            int total = 0;
            foreach (int i in LinesOf(state, bucket)) total += LiveOf(state.SpendBook[i]);
            return total;
        }

        public static int BucketSuggested(GameState state, string bucket)
        {
            int total = 0;
            foreach (int i in LinesOf(state, bucket)) total += state.SpendBook[i].Amt;
            return total;
        }

        public static int BookLive(GameState state)
        {
            int total = 0;
            foreach (string b in Buckets) total += BucketLive(state, b);
            return total;
        }

        public static int BookSuggested(GameState state)
        {
            int total = 0;
            foreach (string b in Buckets) total += BucketSuggested(state, b);
            return total;
        }

        static int OrgGet(GameState state, string bucket)
        {
            switch (bucket)
            {
                case "sales": return state.Budgets.Sales;
                case "care": return state.Budgets.Care;
                case "rnd": return state.Budgets.Rnd;
                default: return state.Budgets.Office;
            }
        }

        static void OrgSet(GameState state, string bucket, int v)
        {
            switch (bucket)
            {
                case "sales": state.Budgets.Sales = v; break;
                case "care": state.Budgets.Care = v; break;
                case "rnd": state.Budgets.Rnd = v; break;
                default: state.Budgets.Office = v; break;
            }
        }

        // ═══════════════════ the one write-back law ═════════════════════════

        /// <summary>
        /// THE SUM IS THE LEVER: Budgets[bucket] := Σ live of the bucket's
        /// lines, after every mutation and at the top of the spend desk's
        /// draw. THE LEGACY ABSORB, once: a pre-book save (levers set, no
        /// Live keys anywhere) lands its levers on the FIRST line of each
        /// bucket; a fresh generated book (no Live keys, levers 0) is left
        /// unstamped. Returns true when anything changed.
        /// </summary>
        public static bool Reconcile(GameState state)
        {
            EnsureBook(state);
            bool changed = false;
            bool anyLive = false;
            foreach (SpendLine l in state.SpendBook)
                if (l.Live.HasValue) { anyLive = true; break; }
            if (!anyLive)
            {
                int org = 0;
                foreach (string b in Buckets) org += OrgGet(state, b);
                if (org > 0)
                {
                    foreach (string b2 in Buckets)
                    {
                        List<int> idxs = LinesOf(state, b2);
                        if (idxs.Count == 0)
                        {
                            state.SpendBook.Add(new SpendLine { Name = b2, Buys = "", Amt = 0, Bucket = b2, Live = 0 });
                            idxs = LinesOf(state, b2);
                        }
                        state.SpendBook[idxs[0]].Live = OrgGet(state, b2);
                    }
                    foreach (SpendLine l2 in state.SpendBook)
                        if (!l2.Live.HasValue) l2.Live = 0;
                    changed = true;
                }
            }
            foreach (string b3 in Buckets)
            {
                int want = BucketLive(state, b3);
                if (OrgGet(state, b3) != want) { OrgSet(state, b3, want); changed = true; }
            }
            return changed;
        }

        // ═══════════════════════ the line steppers ══════════════════════════

        /// The per-line quantum: small lines move in small steps.
        public static int StepQ(int amt)
        {
            if (amt < 200) return 20;
            if (amt < 1000) return 50;
            if (amt < 2000) return 100;
            return 250;
        }

        /// <summary>One press of a line's − or +. Down floors at $0; up is
        /// REFUSED when the bucket would pass the era's spend ceiling.</summary>
        public static int AdjustLive(GameState state, int i, int dir)
        {
            if (i < 0 || i >= state.SpendBook.Count) return 0;
            SpendLine line = state.SpendBook[i];
            if (IsStopping(line)) return LiveOf(line);
            int cur = LiveOf(line);
            int next;
            if (dir < 0) next = Math.Max(cur - StepQ(cur), 0);
            else
            {
                next = cur + StepQ(cur);
                int cap = SimEngine.EraSpendCap(state.Era);
                if (BucketLive(state, line.Bucket ?? "office") - cur + next > cap) return cur;
            }
            line.Live = next;
            Reconcile(state);
            return next;
        }

        /// Whether one more + on this line would be refused by the ceiling.
        public static bool AtCap(GameState state, int i)
        {
            if (i < 0 || i >= state.SpendBook.Count) return true;
            SpendLine line = state.SpendBook[i];
            int cur = LiveOf(line);
            int cap = SimEngine.EraSpendCap(state.Era);
            return BucketLive(state, line.Bucket ?? "office") - cur + (cur + StepQ(cur)) > cap;
        }

        // ═══════════════════ adopt — the suggestion path ════════════════════

        /// ADOPT one suggested line: live := amt, clamped by the era ceiling.
        /// The desk fires this behind the receipt + two-tap.
        public static int AdoptLine(GameState state, int i)
        {
            if (i < 0 || i >= state.SpendBook.Count) return 0;
            SpendLine line = state.SpendBook[i];
            if (IsStopping(line)) return LiveOf(line);
            int sugg = line.Amt;
            if (sugg <= 0) return LiveOf(line);
            int cap = SimEngine.EraSpendCap(state.Era);
            int room = cap - (BucketLive(state, line.Bucket ?? "office") - LiveOf(line));
            line.Live = Gd.Clampi(sugg, 0, Math.Max(room, 0));
            Reconcile(state);
            return LiveOf(line);
        }

        /// ADOPT the whole suggested book — one arm at the sheet top.
        public static int AdoptBook(GameState state)
        {
            for (int i = 0; i < state.SpendBook.Count; i++) AdoptLine(state, i);
            return BookLive(state);
        }

        // ═══════════════ add and stop — the mutation law ════════════════════

        /// ADD a line into a bucket (free — it bills only when raised).
        /// Returns the new index, or -1 when full or the bucket unknown.
        public static int AddLine(GameState state, string bucket)
        {
            EnsureBook(state);
            if (Array.IndexOf(Buckets, bucket) < 0 || state.SpendBook.Count >= BookCap) return -1;
            state.SpendBook.Add(new SpendLine
            {
                Name = "a new line", Buys = "name it with a written move",
                Amt = 0, Bucket = bucket, Live = 0,
            });
            return state.SpendBook.Count - 1;
        }

        /// <summary>STOP a line. No notice → removed now ("stopped"). A
        /// contract line starts its notice clock ("notice") and keeps billing
        /// until it runs out — obligations survive removal.</summary>
        public static string StopLine(GameState state, int i, int week)
        {
            if (i < 0 || i >= state.SpendBook.Count) return "";
            SpendLine line = state.SpendBook[i];
            int notice = line.ContractNotice;
            if (notice <= 0)
            {
                state.SpendBook.RemoveAt(i);
                Reconcile(state);
                return "stopped";
            }
            if (!line.StopWk.HasValue) line.StopWk = week;
            return "notice";
        }

        /// Weeks a stopping line still bills. -1 = the line is not stopping.
        public static int NoticeLeft(SpendLine line, int week)
        {
            if (!line.StopWk.HasValue) return -1;
            return Math.Max(line.ContractNotice - (week - line.StopWk.Value), 0);
        }

        /// Drop every stopping line whose notice ran out (the desk sweeps at
        /// draw — deterministic in both engines). Returns how many closed.
        public static int SweepLapsed(GameState state, int week)
        {
            var kept = new List<SpendLine>();
            int dropped = 0;
            foreach (SpendLine l in state.SpendBook)
            {
                if (l.StopWk.HasValue && NoticeLeft(l, week) <= 0) dropped += 1;
                else kept.Add(l);
            }
            if (dropped > 0)
            {
                state.SpendBook = kept;
                Reconcile(state);
            }
            return dropped;
        }

        // ═══════════ shared display reads for the money desks ═══════════════

        /// TEAM's ladder rung — deterministic counts: ≤9 flat person rows ·
        /// 10–40 function groups · beyond that, business units.
        public static int TeamRung(int n)
        {
            if (n <= 9) return 1;
            if (n <= 40) return 2;
            return 3;
        }

        /// The ESOP vesting fraction at `week` for a grant vesting since
        /// `vestStartWk`: 208-week vest, 52-week cliff (the fallback formula
        /// the team desk renders until the ownership lane's getter lands).
        public static double VestedFrac(int week, int vestStartWk)
        {
            int weeksIn = Math.Max(week - vestStartWk, 0);
            if (weeksIn < 52) return 0.0;
            return Math.Min(weeksIn / 208.0, 1.0);
        }

        /// The grant on a person, matched by name (grants carry emp_id; a
        /// person's only stable identity today is their name). Null = none.
        public static EsopGrant GrantFor(GameState state, string empName)
        {
            if (state.Esop == null || state.Esop.Granted == null) return null;
            foreach (EsopGrant g in state.Esop.Granted)
                if ((g.EmpId ?? "") == empName) return g;
            return null;
        }
    }
}
