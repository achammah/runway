using System;
using System.Collections.Generic;
using Runway.Core;

namespace Runway.CoreTests
{
    /// <summary>
    /// LANE SUITE — roadmap. Spec: docs/design/07-roadmap.md section 14 (twin pins).
    ///
    /// Six pins, in the spec's order, each pinning a number a player can feel:
    ///   1 the board is deterministic and era-legal
    ///   2 OPPORTUNITY COST — committed weeks ship no base quality, and the pool
    ///     is exactly what the arithmetic says
    ///   3 TECH-DEBT INTEREST — the drag is a formula, not a vibe
    ///   4 the band table, the QA net and the clamps, on scripted dice
    ///   5 READY waits for the founder's press, and slips out on its own at
    ///     three weeks; the standing bet always comes back
    ///   6 the multipliers compose
    ///
    /// The porting law: a check lands FIRST in game/tests/lanes/test_roadmap.gd,
    /// then here in the same order. Same checks, same order, same logic — the two
    /// engines do not share PRNG internals, so never pin a draw across them.
    /// </summary>
    public static class RoadmapTests
    {
        static GameState St()
        {
            var s = new GameState();
            s.SimSeed = 42;
            s.Week = 5;
            s.Cash = 50000;
            s.Traction = 40;
            s.Product = 50;
            s.Morale = 70;
            s.Hype = 40;
            s.BizWhat = "Software";
            s.BizWho = "SMB";
            s.Theta = SimEngine.DefaultTheta(s.BizWhat, s.BizWho);
            return s;
        }

        /// <summary>A bet planted by hand, so a pin never depends on the draw.</summary>
        static Bet MakeBet(string id, string kind, int amb, string era = "garage")
        {
            return new Bet
            {
                Id = id, Name = id.ToUpperInvariant(), Desc = "a thing the team could build",
                Kind = kind, Ambition = amb,
                CostRndWeeks = SimRoadmap.BetCost(kind, amb, id),
                Era = era,
            };
        }

        /// <summary>Scripted dice: the list, then its last value forever.</summary>
        static Func<int> Roller(params int[] vals)
        {
            int i = 0;
            return () =>
            {
                int v = vals[Math.Min(i, vals.Length - 1)];
                i += 1;
                return v;
            };
        }

        static GameState Debt(double v)
        {
            GameState s = St();
            s.TechDebt = v;
            return s;
        }

        public static void Run(Action<bool, string> ok)
        {
            // ── 1 ── DETERMINISM + THE SEED BOARD ────────────────────────────────
            // Two clones of the same week deal the same board, and the garage's board
            // is era-legal: the standing bet, one candidate, nothing over ambition 2
            // and no platform work before there is a floor to put it on.
            GameState a = St();
            a.Week = 1;
            GameState b = St();
            b.Week = 1;
            SimEngine.WeeklyTick(a);
            SimEngine.WeeklyTick(b);
            bool same = a.Bets.Count == b.Bets.Count;
            for (int i = 0; i < a.Bets.Count && same; i++)
            {
                same = a.Bets[i].Id == b.Bets[i].Id && a.Bets[i].Name == b.Bets[i].Name;
            }
            ok(same, "the roadmap board is deterministic (same seed, same week, same cards)");
            ok(SimRoadmap.BoardBets(a).Count == SimRoadmap.Slots(a)
               && SimRoadmap.HardeningBet(a) != null,
                "the garage board is the standing bet plus " + SimRoadmap.Slots(a) + " candidate");
            bool eraLegal = true;
            foreach (Bet bet in SimRoadmap.BoardBets(a))
            {
                if (bet.Ambition > 2 || bet.Kind == "platform") eraLegal = false;
            }
            ok(eraLegal, "the garage never deals ambition 3 or a platform bet");

            // ── 2 ── OPPORTUNITY COST: the same money, one output ────────────────
            // $2,400 of rnd is two R&D-weeks; the garage founder adds a quarter of
            // their own. Committed, that is 2.25 weeks of a bet and ZERO base quality
            // — the spine's drip is handed back. Uncommitted, the legacy path stands.
            GameState c = St();
            c.Budgets.Rnd = 2400;
            c.Bets = new List<Bet> { MakeBet("bet_x", "quality", 2) };
            ok(SimRoadmap.CommitBet(c, "bet_x"), "the team can be pointed at a bet");
            int p0 = c.Product;
            SimEngine.WeeklyTick(c);
            Bet bx = SimRoadmap.BetById(c, "bet_x");
            ok(Gd.Absf(bx.Progress - 2.25) < 0.0001,
                "committed rnd buys exactly 2.25 R&D-wks (2.0 money + 0.25 founder)");
            ok(c.Product == p0, "OPPORTUNITY COST: a committed week ships no base quality");
            GameState u = St();
            u.Budgets.Rnd = 2400;
            u.Bets = new List<Bet> { MakeBet("bet_y", "quality", 2) };
            int up0 = u.Product;
            SimEngine.WeeklyTick(u);
            ok(u.Product >= up0 + 2 && SimRoadmap.BetById(u, "bet_y").Progress == 0.0,
                "uncommitted, the legacy +1-per-$1,200 path runs and no bet moves");

            // ── 3 ── TECH-DEBT INTEREST is a formula ─────────────────────────────
            // drag(40) = 1.0, drag(90) = 0.58333, drag(100) = 0.5 — linear interest
            // on every hour the team works, floored at half speed.
            GameState d10 = St();
            d10.TechDebt = 10.0;
            d10.Budgets.Rnd = 2400;
            d10.Bets = new List<Bet> { MakeBet("bet_d", "quality", 2) };
            SimRoadmap.CommitBet(d10, "bet_d");
            GameState d90 = St();
            d90.TechDebt = 90.0;
            d90.Budgets.Rnd = 2400;
            d90.Bets = new List<Bet> { MakeBet("bet_d", "quality", 2) };
            SimRoadmap.CommitBet(d90, "bet_d");
            double ratio = SimRoadmap.CapacityPool(d90) / SimRoadmap.CapacityPool(d10);
            ok(Gd.Absf(ratio - 0.5833333) < 0.0001,
                "debt 90 vs 10 costs exactly 41.7% of the team's throughput");
            ok(Gd.Absf(SimRoadmap.DebtDrag(d10) - 1.0) < 0.0001
               && Gd.Absf(SimRoadmap.DebtDrag(Debt(100.0)) - 0.5) < 0.0001,
                "the drag is 1.0 under debt 40 and floors at 0.5");

            // ── 4 ── THE BAND TABLE, on scripted dice ────────────────────────────
            // A 20 on an ambition-2 quality bet is brilliant: +11 product, +8 hype.
            // The same bet on a 7 misses DC 11 by four — a backfire in a garage, and
            // only a risky launch once staging and review exist (the QA net, office+).
            GameState win = St();
            Bet wb = MakeBet("bet_w", "quality", 2);
            win.Bets = new List<Bet> { wb };
            SimRoadmap.ShipResult wr = SimRoadmap.ShipBet(win, wb, Roller(20));
            ok(wr.Band == "brilliant" && win.Product == 61 && win.Hype == 48,
                "a 20 vs DC 11 is brilliant: product +11, hype +8");
            GameState bad = St();
            Bet bb = MakeBet("bet_b", "quality", 2);
            bad.Bets = new List<Bet> { bb };
            SimRoadmap.ShipResult br = SimRoadmap.ShipBet(bad, bb, Roller(7));
            ok(br.Band == "backfired" && Gd.Absf(bad.TechDebt - 22.0) < 0.001 && bad.Morale == 64,
                "a 7 vs DC 11 backfires in the garage: debt +12, morale −6");
            GameState qa = St();
            qa.Era = "office";
            Bet qb = MakeBet("bet_q", "quality", 2, "office");
            qa.Bets = new List<Bet> { qb };
            SimRoadmap.ShipResult qr = SimRoadmap.ShipBet(qa, qb, Roller(7));
            ok(qr.Band == "risky" && qr.QaNet && Gd.Absf(qa.TechDebt - 16.0) < 0.001,
                "the QA net softens a miss by four to risky at office+: debt +6, not +12");
            GameState cap = St();
            cap.Product = 98;
            Bet cb = MakeBet("bet_c", "quality", 2);
            cap.Bets = new List<Bet> { cb };
            SimRoadmap.ShipBet(cap, cb, Roller(20));
            ok(cap.Product == 100, "the payoff clamps: product never passes 100");

            // ── 5 ── READY WAITS FOR THE PRESS, then slips out on its own ────────
            // SHIP IS A BUTTON (DECISIONS.md #2): a finished bet sits READY until the
            // founder presses it — for three weeks, and then the world ships it
            // anyway. The standing bet always comes back.
            GameState r = St();
            r.Budgets.Rnd = 1200;
            Bet rb = MakeBet("bet_r", "quality", 2);
            rb.Progress = 4.5;
            r.Bets = new List<Bet> { rb };
            SimRoadmap.CommitBet(r, "bet_r");
            r.Week += 1;
            SimEngine.WeeklyTick(r);
            Bet rr = SimRoadmap.BetById(r, "bet_r");
            ok(rr.Ready && !rr.Shipped && !rr.Committed,
                "a finished bet goes READY, uncommitted, unshipped");
            r.Week += 1;
            SimEngine.WeeklyTick(r);
            ok(SimRoadmap.BetById(r, "bet_r").Ready && !SimRoadmap.BetById(r, "bet_r").Shipped,
                "the world does not ship it for you — the dice wait for the press");
            SimRoadmap.ShipResult pressed = SimRoadmap.ShipReady(r, "bet_r");
            ok(pressed != null && SimRoadmap.BetById(r, "bet_r").Shipped
               && SimRoadmap.BetById(r, "bet_r").Band != "",
                "the press rolls the house dice and the bet ships with a band");
            GameState s3 = St();
            Bet sb = MakeBet("bet_s", "quality", 2);
            sb.Ready = true;
            sb.CommittedWeek = s3.Week - SimRoadmap.STALL_WEEKS;
            s3.Bets = new List<Bet> { sb };
            WeeklyReport s3rep = SimEngine.WeeklyTick(s3);
            bool slipped = false;
            foreach (string l in s3rep.Lines)
            {
                if (l != null && l.StartsWith("nobody pressed ship", StringComparison.Ordinal))
                    slipped = true;
            }
            ok(SimRoadmap.BetById(s3, "bet_s").Shipped && slipped,
                "three weeks unpressed and the launch slips out on its own, with its receipt");
            GameState h = St();
            h.Bets = new List<Bet> { MakeBet(SimRoadmap.HARDENING_ID, "debt", 1) };
            SimRoadmap.ShipBet(h, SimRoadmap.HardeningBet(h), Roller(20));
            h.Week += 1;
            SimEngine.WeeklyTick(h);
            Bet fresh = SimRoadmap.HardeningBet(h);
            ok(fresh != null && fresh.Progress == 0.0,
                "the standing hardening bet is re-seeded the tick after it ships");

            // ── 6 ── THE MULTIPLIERS COMPOSE ─────────────────────────────────────
            // An engineer is 0.25 x skill of real capacity; a shipped platform level
            // multiplies everything that comes after it.
            GameState e0 = St();
            e0.Bets = new List<Bet> { MakeBet("bet_e", "quality", 2) };
            SimRoadmap.CommitBet(e0, "bet_e");
            double bas = SimRoadmap.CapacityPool(e0);
            e0.Employees.Add(new Employee { Name = "Ren", Role = "engineer", Salary = 1200, Skill = 4 });
            ok(Gd.Absf(SimRoadmap.CapacityPool(e0) - (bas + 1.0)) < 0.0001,
                "one engineer at skill 4 adds exactly 1.0 R&D-wk/wk");
            GameState pl = St();
            pl.Budgets.Rnd = 2400;
            pl.Bets = new List<Bet> { MakeBet("bet_p", "quality", 2) };
            SimRoadmap.CommitBet(pl, "bet_p");
            pl.PlatformLevel = 1;
            ok(Gd.Absf(SimRoadmap.CapacityPool(pl) - 2.5875) < 0.0001,
                "a platform level compounds the whole pool: 2.25 x 1.15 = 2.5875");
        }
    }
}
