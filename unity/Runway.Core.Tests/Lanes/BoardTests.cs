using System;
using System.Collections.Generic;
using Runway.Core;

namespace Runway.CoreTests
{
    /// <summary>
    /// LANE SUITE — board. Spec: docs/design/08-board-mna.md section 12 (twin pins).
    ///
    /// Six pins, one per mechanic that would be silently wrong if it drifted:
    ///   1 the pool shuffle's exact arithmetic (and that it only exists at office+)
    ///   2 the review is deterministic and re-arms on the same cadence
    ///   3 the stage ladder gates the strikes, the coach and the reprice
    ///   4 warmth reads the governance record, in the right direction
    ///   5 the lifeline offer, the no-shop clock and the cooldown after a lapse
    ///   6 the IPO window is weather — it opens and it shuts, with a reason
    ///
    /// Program.cs calls Run after the engine's own checks and hands over `ok`.
    ///
    /// The porting law: a check lands FIRST in game/tests/lanes/test_board.gd,
    /// then here in the same order. Same checks, same order, same logic — the two
    /// engines do not share PRNG internals, so nothing here pins a draw across
    /// them, only behaviour.
    /// </summary>
    public static class BoardTests
    {
        /// <summary>A run with a live company in it, at whatever era the pin needs.</summary>
        static GameState St(string era)
        {
            var s = new GameState();
            s.SimSeed = 4242;
            s.Week = 20;
            s.Era = era;
            s.Cash = 250000;
            s.Traction = 60;
            s.Product = 55;
            s.Morale = 70;
            s.Hype = 40;
            s.FounderPct = 100.0;
            s.BizWhat = "Software";
            s.BizWho = "SMB";
            s.Theta = SimEngine.DefaultTheta(s.BizWhat, s.BizWho);
            return s;
        }

        /// <summary>A board mid-quarter, its review landing exactly this week.</summary>
        static void WithBoard(GameState s, int target, int strikes = 0, int goodwill = 0)
        {
            s.Board = new BoardState
            {
                TargetGrowthPct = 35.0, BaseRevenue = target, TargetRevenue = target,
                ReviewWeek = s.Week, Strikes = strikes, Goodwill = goodwill,
            };
        }

        static WeeklyReport Rep()
        {
            return new WeeklyReport();
        }

        static Commitment Find(GameState s, string name)
        {
            foreach (Commitment c in s.Commitments)
            {
                if (c.Name == name) return c;
            }
            return null;
        }

        static bool SameLines(List<string> a, List<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        public static void Run(Action<bool, string> ok)
        {
            // ── 1 · ROUND CLOSE DOES THE FULL SHUFFLE ───────────────────────
            // The pool is written PRE-money, so it dilutes only the existing
            // side, and the investor's slice then dilutes everyone including it.
            GameState s1 = St("office");
            SimEngine.ApplyRound(s1, 100000, 20.0);
            SimBoard.OnRoundClosed(s1, 100000, 20.0);
            ok(Gd.Absf(s1.FounderPct - 72.0) < 0.001,
                string.Format("pool shuffle: founder 100 x0.9 pool x0.8 investor = 72 (got {0:0.000})", s1.FounderPct));
            ok(Gd.Absf(s1.OptionPoolPct - 8.0) < 0.001,
                string.Format("pool shuffle: a 10% pool diluted by the round = 8 (got {0:0.000})", s1.OptionPoolPct));
            ok(s1.BoardSeatsInvestor == 1 && s1.Board.ReviewWeek == s1.Week + 12,
                "a priced round seats one investor and dates the first review 12 wks out");
            ok(s1.Board.TargetGrowthPct >= 10.0 && s1.Board.TargetGrowthPct <= 60.0
                && s1.Board.TargetRevenue >= SimBoard.ERA_REV_FLOOR["office"],
                "the covenant is clamped 10-60%/qtr and never asks for growth on nothing");
            // and BELOW office there is no pool at all
            GameState s1b = St("coworking");
            SimEngine.ApplyRound(s1b, 50000, 20.0);
            SimBoard.OnRoundClosed(s1b, 50000, 20.0);
            ok(Gd.Absf(s1b.FounderPct - 80.0) < 0.001 && Gd.Absf(s1b.OptionPoolPct) < 0.001,
                "no pool below office: the founder keeps the shuffle's 10 points");

            // ── 2 · THE REVIEW IS DETERMINISTIC AND RE-ARMS ─────────────────
            GameState a = St("office");
            GameState b = St("office");
            a.LastPnl = new Pnl { Revenue = 400 };
            b.LastPnl = new Pnl { Revenue = 400 };
            WithBoard(a, 900);
            WithBoard(b, 900);
            WeeklyReport ra = Rep();
            WeeklyReport rb = Rep();
            SimBoard.TickPost(a, ra);
            SimBoard.TickPost(b, rb);
            ok(a.Board.Strikes == b.Board.Strikes && a.Board.Goodwill == b.Board.Goodwill
                && SameLines(ra.Lines, rb.Lines) && SameLines(ra.Events, rb.Events),
                "two identical states review to identical strikes, goodwill and receipts");
            ok(a.Board.ReviewWeek == a.Week + 12,
                "the review re-arms exactly 12 weeks out, whichever way it went");
            ok(a.Board.TargetRevenue > a.Board.BaseRevenue,
                "the re-armed bar sits above the base it was set from");

            // ── 3 · THE STAGE LADDER GATES THE STRIKES ──────────────────────
            GameState g = St("garage");
            g.LastPnl = new Pnl { Revenue = 10 };
            WithBoard(g, 500);
            SimBoard.TickPost(g, Rep());
            ok(g.Board.Strikes == 0 && SimEngine.HasStatus(g, "investor_pressure"),
                "a garage miss installs investor_pressure and puts nothing on a record");
            // office, miss twice: the coach a board sends before it does worse
            GameState o = St("office");
            o.Employees.Add(new Employee { Name = "dev", Role = "engineer", Salary = 12000, Burnout = 10 });
            o.LastPnl = new Pnl { Revenue = 100 };
            WithBoard(o, 5000);
            SimBoard.TickPost(o, Rep());
            o.Week += 12;
            o.Board.ReviewWeek = o.Week;
            SimBoard.TickPost(o, Rep());
            Commitment coach = Find(o, "the executive coach the board sent");
            ok(o.Board.Strikes == 2 && coach != null
                && coach.CashWk <= -250 && coach.CashWk >= -2500,
                "strike two sends a CEO coach billing $250-$2500/wk, by name");
            // a third miss reprices every future round, and the clamp holds
            double fm0 = o.Theta.FundingMult;
            o.Week += 12;
            o.Board.ReviewWeek = o.Week;
            SimBoard.TickPost(o, Rep());
            ok(Gd.Absf(o.Theta.FundingMult - fm0 * 0.8) < 0.0001 && o.HasFlag("down_round_threat"),
                "strike three reprices the company x0.8 and flags the down round");
            // a beat drops the record back to two strikes, so the next miss
            // re-reaches three and reprices again — that is what "repeated" means
            for (int i = 0; i < 6; i++)
            {
                o.Week += 12;
                o.Board.ReviewWeek = o.Week;
                o.LastPnl = new Pnl { Revenue = 900000 };
                SimBoard.TickPost(o, Rep());
                o.Week += 12;
                o.Board.ReviewWeek = o.Week;
                o.LastPnl = new Pnl { Revenue = 100 };
                SimBoard.TickPost(o, Rep());
            }
            ok(Gd.Absf(o.Theta.FundingMult - SimBoard.FundingMultFloor) < 0.0001,
                "repeated strike threes converge on the 0.5 floor, never to zero");
            // and a beat pays: the record improves and the room warms
            GameState w = St("office");
            w.LastPnl = new Pnl { Revenue = 9000 };
            WithBoard(w, 5000, 1, 0);
            SimBoard.TickPost(w, Rep());
            ok(w.Board.Goodwill == 1 && w.Board.Strikes == 0
                && SimEngine.HasStatus(w, "board_delight"),
                "a met covenant burns a strike, banks goodwill and installs board_delight");

            // ── 4 · WARMTH READS THE RECORD ─────────────────────────────────
            GameState clean = St("office");
            GameState loved = St("office");
            GameState hated = St("office");
            WithBoard(clean, 500, 0, 0);
            WithBoard(loved, 500, 0, 3);
            WithBoard(hated, 500, 3, 0);
            ok(Gd.Absf(SimBoard.WarmthDelta(loved) - 6.0) < 0.001
                && Gd.Absf(SimBoard.WarmthDelta(hated) + 7.5) < 0.001,
                "three clean quarters are worth +6 points of ask; three strikes cost 7.5");
            ok(SimBoard.WarmthDelta(loved) > SimBoard.WarmthDelta(clean)
                && SimBoard.WarmthDelta(clean) > SimBoard.WarmthDelta(hated)
                && Gd.Absf(SimBoard.WarmthDelta(St("office"))) < 0.001,
                "warmth orders loved > clean > struck, and a boardless run has no record");

            // ── 5 · LIFELINE, THE NO-SHOP AND THE COOLDOWN ──────────────────
            GameState d = St("garage");
            d.Week = 8;
            d.Cash = 400;
            d.WeeksInRed = 2;
            GameState d2 = St("garage");
            d2.Week = 8;
            d2.Cash = 400;
            d2.WeeksInRed = 2;
            SimBoard.TickPost(d, Rep());
            SimBoard.TickPost(d2, Rep());
            ok(d.Mna != null && d.Mna.Why == "lifeline"
                && d.Mna.Premium >= 0.3 && d.Mna.Premium <= 0.5
                && d.Mna.ExpiresWeek == d.Week + 2,
                "a dying company with something worth taking gets a 0.3-0.5x lifeline on a 2-wk no-shop");
            ok(d.Mna.Buyer == d2.Mna.Buyer && d.Mna.Price == d2.Mna.Price,
                "the same seed and week price the same offer from the same buyer");
            int moraleBefore = d.Morale;
            d.Week = 11;
            WeeklyReport lapse = Rep();
            SimBoard.TickPost(d, lapse);
            ok(d.Mna == null && d.Morale < moraleBefore && d.MnaLastWeek == 11,
                "an unsigned lifeline lapses: the offer dies and the team heard the number");
            bool quiet = true;
            for (int i = 0; i < 9; i++)
            {
                d.Week += 1;
                SimBoard.TickPost(d, Rep());
                if (d.Mna != null) quiet = false;
            }
            ok(quiet, "corp dev does not re-approach for the whole 10-week cooldown");

            // ── 6 · THE WINDOW IS WEATHER ───────────────────────────────────
            GameState h = St("hq");
            h.Traction = 120;
            h.RoundsRaised.Add("seed");
            h.RoundsRaised.Add("series_a");
            h.MacroSeason = "boom";
            SimBoard.TickPost(h, Rep());
            ok(h.HasFlag("ipo_window"),
                "clean covenants + a hundred believers + a market that's buying opens the window");
            h.MacroSeason = "winter";
            h.Week += 1;
            WeeklyReport shut = Rep();
            SimBoard.TickPost(h, shut);
            bool said = false;
            foreach (string l in shut.Lines)
            {
                if (l.StartsWith("the IPO window closed — winter came")) said = true;
            }
            ok(!h.HasFlag("ipo_window") && said,
                "winter shuts the window, and the receipt says which weather did it");
        }
    }
}
