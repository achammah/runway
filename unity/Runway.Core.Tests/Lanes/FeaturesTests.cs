using System;
using System.Collections.Generic;
using Runway.Core;

namespace Runway.CoreTests
{
    /// <summary>
    /// LANE SUITE — the feature inventory (DAG2 W2 L-MAKE). Spec:
    /// docs/design/DECISIONS.md (PRODUCT desk corrected + THE KANBAN WALL) +
    /// docs/design/DAG2.md + the L-MAKE brief.
    ///
    /// What these pins hold: births (the seeded wall, landed bets, kind→job,
    /// era×ambition keep, risky-born-creaky, the backfired null, dedup, the
    /// rebuild heal) · measured (unknown four weeks, then actual payoff ×
    /// salted spread, deterministic) · solidity (the jar's face: plumbing
    /// first, converge on ceil((debt−40)/15), heal on paydown, the ONE-TAX
    /// law) · the shelf (3..5 priced deterministic ideas, gaps first, the
    /// rebuild, era caps) · the NEXT queue (commit-or-queue, freed slot,
    /// reorder, dequeue) · money inert until the feature_keep package.
    ///
    /// The porting law: a check lands FIRST in
    /// game/tests/lanes/test_features.gd, then here in the same order.
    /// Same checks, same order, byte-identical messages. Checks that would
    /// pin a specific DRAW pin the LAW instead (this Rng diverges from
    /// Godot's in values by design).
    /// </summary>
    public static class FeaturesTests
    {
        static GameState St()
        {
            var s = new GameState();
            s.SimSeed = 4242;
            s.Week = 12;
            s.Cash = 60000;
            s.Traction = 30;
            s.Product = 50;
            s.Morale = 70;
            s.Hype = 30;
            s.BizWhat = "Software";
            s.BizWho = "Consumer";
            s.Theta = SimEngine.DefaultTheta(s.BizWhat, s.BizWho);
            return s;
        }

        static void Populate(GameState s)
        {
            s.Features = new List<Feature>
            {
                new Feature { Id = "ft_booking", Name = "online booking",
                    Job = "pull", Family = "", Solidity = "solid", KeepWk = 40,
                    UnitCostAdd = 0.0, ProductId = "", BornWk = 0, Measured = 0.0 },
                new Feature { Id = "ft_pipes", Name = "the data plumbing",
                    Job = "plumbing", Family = "", Solidity = "creaky", KeepWk = 25,
                    UnitCostAdd = 0.5, ProductId = "", BornWk = 0, Measured = 0.0 },
            };
            s.TechDebt = 50.0;   // target load 1 — the fixture's creak is jar-stable
        }

        /// <summary>A bet already built, waiting for the dice.</summary>
        static Bet MkBet(GameState s, string kind, int amb, string name)
        {
            double cost = SimRoadmap.BetCost(kind, amb);
            var bet = new Bet
            {
                Id = "tb_" + kind + "_" + s.Bets.Count, Name = name, Desc = "",
                Kind = kind, Ambition = amb, CostRndWeeks = cost, Progress = cost,
                Committed = false, CommittedWeek = s.Week, Ready = true,
                Shipped = false, ShippedWeek = 0, Band = "", Era = s.Era,
            };
            s.Bets.Add(bet);
            return bet;
        }

        public static void Run(Action<bool, string> ok)
        {
            // ── 1 · the field exists, at a safe default
            GameState s0 = St();
            ok(s0.Features.Count == 0,
                "features: a fresh state ships no feature inventory");

            // ── 2 · a truly blank state (the draft) seeds nothing
            var blank = new GameState();
            blank.SimSeed = 7;
            blank.Traction = 0;
            blank.Product = 0;
            SimFeatures.SeedDefaults(blank);
            ok(blank.Features.Count == 0,
                "features: the blank draft state seeds no wall");

            // ── 3/4 · an old save's minimal wall, derived from its offers
            GameState sw = St();
            SimEngine.AddOffer(sw, "the massage hour", "per session", 40.0, 10.0, 2.0, 1.0);
            SimFeatures.SeedDefaults(sw);
            ok(sw.Features.Count == 3
                && sw.Features[1].Name == "the massage hour"
                && sw.Features[0].BornWk == 0,
                "features: an old save seeds its minimal wall from the offers");
            var jobs = new List<string>();
            foreach (Feature f in sw.Features) jobs.Add(f.Job);
            ok(jobs.Contains("pull") && jobs.Contains("keep") && jobs.Contains("plumbing"),
                "features: the seeded wall covers pull, keep and the plumbing");

            // ── 5 · NEUTRALITY until the feature_keep package: no money moves
            GameState ctrl = St();
            ctrl.TechDebt = 50.0;
            GameState full = St();
            Populate(full);
            SimEngine.WeeklyTick(ctrl);
            SimEngine.WeeklyTick(full);
            ok(ctrl.Cash - full.Cash == SimFeatures.KeepTotal(full) - SimFeatures.KeepTotal(ctrl)
                && ctrl.Traction == full.Traction && ctrl.Product == full.Product,
                "features: keep-costs bill into burn, dollar for dollar");

            // ── 6 · the records ride the tick untouched (jar-stable fixture)
            ok(full.Features[0].KeepWk == 40 && full.Features[1].Solidity == "creaky",
                "features: the inventory survives the tick untouched");

            // ── 7/8 · a landed bet joins the wall: kind→job, era×ambition keep
            GameState sl = St();
            Bet b7 = MkBet(sl, "reach", 2, "group scheduling");
            SimRoadmap.ShipBet(sl, b7, () => 20);
            SimFeatures.TickPre(sl, new WeeklyReport());
            Feature f7 = sl.Features.Count > 0 ? sl.Features[sl.Features.Count - 1] : null;
            ok(sl.Features.Count >= 1 && f7 != null && f7.Job == "pull"
                && f7.Name == "group scheduling"
                && f7.BornWk == sl.Week
                && f7.Measured == 0.0
                && f7.Solidity == "solid",
                "features: a landed reach bet joins the wall as brings-them-in");
            ok(f7 != null && f7.KeepWk == 6 && Math.Abs(f7.UnitCostAdd - 0.3) < 0.0001,
                "features: a landing prices its keep from era and ambition");

            // ── 9 · a backfired launch ships nothing worth keeping
            GameState sb = St();
            Bet b9 = MkBet(sb, "reach", 1, "the referral loop");
            SimRoadmap.ShipBet(sb, b9, () => 1);
            SimFeatures.TickPre(sb, new WeeklyReport());
            ok(sb.Features.Count == 3,   // only the seeded defaults, no landing
                "features: a backfired launch ships nothing worth keeping");

            // ── 10 · a risky ship is born creaky (shipped in a hurry)
            GameState sr = St();
            Bet b10 = MkBet(sr, "retention", 1, "SMS pack");
            SimRoadmap.ShipBet(sr, b10, () => 7);
            SimFeatures.TickPre(sr, new WeeklyReport());
            Feature f10 = sr.Features[sr.Features.Count - 1];
            ok(f10.Solidity == "creaky" && f10.Job == "keep",
                "features: a risky ship is born creaky");

            // ── 11 · the landing is born once, not twice
            int n11 = sr.Features.Count;
            SimFeatures.TickPre(sr, new WeeklyReport());
            ok(sr.Features.Count == n11,
                "features: the landing is born once, not twice");

            // ── 12 · a rebuild landing makes the worst creak solid again
            GameState sh = St();
            Populate(sh);
            Bet b12 = MkBet(sh, "debt", 1, "Hardening sprint");
            SimRoadmap.ShipBet(sh, b12, () => 15);
            SimFeatures.TickPre(sh, new WeeklyReport());
            ok(sh.Features[1].Solidity == "solid",
                "features: a rebuild landing makes the worst creak solid again");

            // ── 13-16 · promised vs measured: four quiet weeks, then the verdict
            GameState sm = St();
            Bet b13 = MkBet(sm, "reach", 1, "the referral loop");
            SimRoadmap.ShipBet(sm, b13, () => 10);   // fine → 4 units
            SimFeatures.TickPre(sm, new WeeklyReport());
            Feature fm = sm.Features[sm.Features.Count - 1];
            for (int i = 0; i < 3; i++)
            {
                sm.Week += 1;
                SimFeatures.TickPost(sm, new WeeklyReport());
            }
            ok(fm.Measured == 0.0,
                "features: measured stays unknown until the fourth week");
            sm.Week += 1;
            SimFeatures.TickPost(sm, new WeeklyReport());
            double m13 = fm.Measured;
            ok(m13 >= 2.95 && m13 <= 5.05,
                "features: the market answers inside the promised spread");
            GameState sm2 = St();
            Bet b14 = MkBet(sm2, "reach", 1, "the referral loop");
            SimRoadmap.ShipBet(sm2, b14, () => 10);
            SimFeatures.TickPre(sm2, new WeeklyReport());
            for (int i2 = 0; i2 < 4; i2++)
            {
                sm2.Week += 1;
                SimFeatures.TickPost(sm2, new WeeklyReport());
            }
            ok(Math.Abs(sm2.Features[sm2.Features.Count - 1].Measured - m13) < 0.0001,
                "features: the measured verdict is deterministic");
            ok(SimFeatures.PromisedUnits(sm, fm) == 4,
                "features: the promise is recovered from the launch history");

            // ── 17-20 · the jar's face: plumbing first, converge, stop, heal
            GameState sj = St();
            sj.TechDebt = 70.0;   // target load: ceil(30/15) = 2
            sj.Features = new List<Feature>();
            for (int i3 = 0; i3 < 5; i3++)
                sj.Features.Add(new Feature
                {
                    Id = "ft_s" + i3, Name = "solid thing " + i3, Job = "keep",
                    Family = "", Solidity = "solid", KeepWk = 5,
                    UnitCostAdd = 0.0, ProductId = "", BornWk = 0, Measured = 0.0,
                });
            sj.Features.Add(new Feature
            {
                Id = "ft_plumb", Name = "the billing core", Job = "plumbing",
                Family = "", Solidity = "solid", KeepWk = 9,
                UnitCostAdd = 0.0, ProductId = "", BornWk = 0, Measured = 0.0,
            });
            SimFeatures.TickPost(sj, new WeeklyReport());
            Feature plumb = sj.Features[5];
            ok(plumb.Solidity == "creaky" && SimFeatures.CreakLoad(sj) == 1,
                "features: the debt creaks the plumbing first");
            SimFeatures.TickPost(sj, new WeeklyReport());
            ok(SimFeatures.CreakLoad(sj) == 2
                && SimFeatures.ExpectedCreakLoad(sj.TechDebt) == 2,
                "features: the jar's level becomes the wall's creak count");
            SimFeatures.TickPost(sj, new WeeklyReport());
            ok(SimFeatures.CreakLoad(sj) == 2,
                "features: the creaks stop at the jar's level");
            sj.TechDebt = 10.0;
            SimFeatures.TickPost(sj, new WeeklyReport());
            SimFeatures.TickPost(sj, new WeeklyReport());
            ok(SimFeatures.CreakLoad(sj) == 0,
                "features: paying the jar down heals the wall");

            // ── 21 · THE ONE-TAX LAW: the jar's drag is the only velocity tax
            GameState st1 = St();
            st1.TechDebt = 70.0;
            GameState st2 = St();
            st2.TechDebt = 70.0;
            Populate(st2);
            st2.TechDebt = 70.0;
            ok(Math.Abs(SimRoadmap.CapacityPool(st1) - SimRoadmap.CapacityPool(st2)) < 0.0001
                && SimFeatures.CreakTaxPct(st1) == (int)Math.Round(
                    (1.0 - SimRoadmap.DebtDrag(st1)) * 100.0, MidpointRounding.AwayFromZero),
                "features: creaks never tax twice — the jar's drag is the only tax");

            // ── 22-26 · the shelf: priced, deterministic, gap-first, era-capped
            GameState ss = St();
            SimEngine.AddOffer(ss, "the planner", "per month", 30.0, 8.0, 2.0, 1.0);
            SimFeatures.SeedDefaults(ss);
            List<SimFeatures.ShelfCandidate> shelf = SimFeatures.ShelfCandidates(ss);
            bool priced = shelf.Count > 0;
            foreach (SimFeatures.ShelfCandidate c in shelf)
                if (c.CostUsd <= 0 || c.Weeks < 1 || c.OddsPct < 5 || c.OddsPct > 95)
                    priced = false;
            ok(shelf.Count >= 3 && shelf.Count <= 5 && priced,
                "features: the shelf holds three to five priced ideas");
            List<SimFeatures.ShelfCandidate> shelf2 = SimFeatures.ShelfCandidates(ss);
            bool same = shelf.Count == shelf2.Count;
            for (int i4 = 0; i4 < shelf.Count; i4++)
                if (same && shelf[i4].Id != shelf2[i4].Id) same = false;
            ok(same,
                "features: the shelf re-draws the same paper within a week");
            GameState sc = St();
            Populate(sc);
            List<SimFeatures.ShelfCandidate> shelf3 = SimFeatures.ShelfCandidates(sc);
            bool hasRebuild = false;
            foreach (SimFeatures.ShelfCandidate c3 in shelf3)
                if (c3.Kind == "debt") hasRebuild = true;
            ok(hasRebuild,
                "features: a creaky wall puts a rebuild on the shelf");
            bool hasCharge = false;
            foreach (SimFeatures.ShelfCandidate c4 in shelf)
                if (c4.Job == "charge") hasCharge = true;
            ok(hasCharge,
                "features: the shelf fills the wall's missing jobs first");
            bool capped = true;
            foreach (SimFeatures.ShelfCandidate c5 in shelf)
                if (c5.Ambition > SimRoadmap.AmbitionCap(ss)) capped = false;
            ok(capped,
                "features: shelf ambitions respect the era's cap");

            // ── 27-29 · commit or queue, the freed slot, reorder + dequeue
            GameState sq = St();
            SimEngine.AddOffer(sq, "the planner", "per month", 30.0, 8.0, 2.0, 1.0);
            SimFeatures.SeedDefaults(sq);
            List<SimFeatures.ShelfCandidate> cands = SimFeatures.ShelfCandidates(sq);
            string r1 = SimFeatures.CommitShelf(sq, cands[0].Id);
            List<SimFeatures.ShelfCandidate> cands2 = SimFeatures.ShelfCandidates(sq);
            string r2 = SimFeatures.CommitShelf(sq, cands2[0].Id);
            ok(r1 == "committed" && r2 == "queued"
                && SimRoadmap.CommittedBets(sq).Count == 1
                && SimFeatures.QueuedBets(sq).Count == 1,
                "features: committing the shelf points the team or queues");
            string committedId = SimRoadmap.CommittedBets(sq)[0].Id;
            SimRoadmap.UncommitBet(sq, committedId);
            SimFeatures.TickPre(sq, new WeeklyReport());
            ok(SimRoadmap.CommittedBets(sq).Count == 1
                && SimFeatures.QueuedBets(sq).Count == 0,
                "features: the queue takes the freed slot in order");
            GameState so = St();
            so.Era = "office";
            SimRoadmap.EnsureBoard(so);
            List<Bet> board = SimRoadmap.BoardBets(so);
            string qa = board[0].Id;
            string qb = board[1].Id;
            SimFeatures.EnqueueBet(so, qa);
            SimFeatures.EnqueueBet(so, qb);
            SimFeatures.QueueMove(so, qb, -1);
            List<Bet> qAfter = SimFeatures.QueuedBets(so);
            bool reordered = qAfter[0].Id == qb;
            SimFeatures.DequeueBet(so, qa);
            Bet qaBet = SimRoadmap.BetById(so, qa);
            ok(reordered && SimFeatures.QueuedBets(so).Count == 1
                && qaBet.CommittedWeek == 0,
                "features: the queue reorders and returns to the shelf");

            // ── 30 · attention: the creaks named inside the ticker's 40 characters
            GameState sa = St();
            Populate(sa);
            List<AttentionItem> rows = SimFeatures.Attention(sa);
            AttentionItem creakRow = rows.Count > 0 ? rows[0] : new AttentionItem();
            sa.Features[1].Solidity = "breaking";
            List<AttentionItem> rows2 = SimFeatures.Attention(sa);
            AttentionItem breakRow = rows2.Count > 0 ? rows2[0] : new AttentionItem();
            ok(creakRow.Key == "creak_tax" && creakRow.Severity == 2
                && creakRow.Label.Length <= 40
                && breakRow.Key == "feature_breaking" && breakRow.Severity == 3
                && breakRow.Label.Length <= 40
                && creakRow.Desk == "what we make",
                "features: attention names the creaks inside 40 characters");

            // ── 31 · keep_total is pure arithmetic over the wall
            ok(SimFeatures.KeepTotal(sa) == 65,
                "features: keep_total is the sum of the wall's keep lines");

            // ── 32 · the DM hears the creak, and only the creak
            GameState sd = St();
            Populate(sd);
            List<string> dd = SimFeatures.Directives(sd);
            GameState quiet = St();
            SimEngine.AddOffer(quiet, "the planner", "per month", 30.0, 8.0, 2.0, 1.0);
            SimFeatures.SeedDefaults(quiet);
            ok(dd.Count >= 1 && dd[0].Contains("creak")
                && SimFeatures.Directives(quiet).Count == 0,
                "features: the DM hears the creak, and only the creak");
        }
    }
}
