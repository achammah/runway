using System;
using System.Collections.Generic;
using Runway.Core;

namespace Runway.CoreTests
{
    /// <summary>
    /// LANE SUITE — the money desks (DAG2 W2 L-MONEY), the byte-twin of
    /// game/tests/lanes/test_money_desks.gd: same checks, same order, same
    /// messages. Pins the spend book's write-back law, the SUGGEST/ADOPT
    /// ruling, the stop/notice mutation law, and the team desk's display
    /// math (vesting 208/52, the rung ladder). Program.cs registers
    /// MoneyDesksTests.Run(ok) at integration; Runway.MoneyDesks.Tests runs
    /// it standalone meanwhile.
    /// </summary>
    public static class MoneyDesksTests
    {
        static GameState State()
        {
            var s = new GameState
            {
                SimSeed = 42, Week = 5, Cash = 50000, Era = "office",
                BizWhat = "Software", BizWho = "SMB",
            };
            return s;
        }

        static void Booked(GameState s)
        {
            s.SpendBook = new List<SpendLine>
            {
                new SpendLine { Name = "sales engineering", Buys = "demos that land", Amt = 180, Bucket = "sales" },
                new SpendLine { Name = "the demo rig", Buys = "always ready to show", Amt = 120, Bucket = "sales" },
                new SpendLine { Name = "on-call rotation", Buys = "nights answered", Amt = 120, Bucket = "care", ContractNotice = 4 },
                new SpendLine { Name = "the test bench", Buys = "bugs die young", Amt = 150, Bucket = "rnd" },
                new SpendLine { Name = "the kitchen", Buys = "fed people stay", Amt = 220, Bucket = "office" },
            };
        }

        public static void Run(Action<bool, string> ok)
        {
            // ── 1. THE SUGGEST/ADOPT RULING
            GameState s1 = State();
            Booked(s1);
            SimSpendBook.Reconcile(s1);
            ok(s1.Budgets.Sales == 0 && s1.Budgets.Care == 0,
                "a fresh generated book leaves the levers at 0");
            ok(SimSpendBook.BookSuggested(s1) == 790,
                "the suggestions still read whole beside the zero levers");

            // ── 2. ADOPT one line
            GameState s2 = State();
            Booked(s2);
            SimSpendBook.AdoptLine(s2, 0);
            ok(SimSpendBook.LiveOf(s2.SpendBook[0]) == 180 && s2.Budgets.Sales == 180,
                "adopt copies the suggestion into the lever");

            // ── 3. ADOPT the whole book
            GameState s3 = State();
            Booked(s3);
            SimSpendBook.AdoptBook(s3);
            ok(s3.Budgets.Sales == 300 && s3.Budgets.Care == 120
               && s3.Budgets.Rnd == 150 && s3.Budgets.Office == 220,
                "adopt the whole book prices every bucket");

            // ── 4. THE WRITE-BACK LAW
            GameState s4 = State();
            Booked(s4);
            SimSpendBook.AdoptBook(s4);
            SimSpendBook.AdjustLive(s4, 1, 1);   // $120 steps by q(120)=20 → $140
            ok(SimSpendBook.LiveOf(s4.SpendBook[1]) == 140 && s4.Budgets.Sales == 320,
                "the sum IS the lever after a step up");
            SimSpendBook.AdjustLive(s4, 1, -1);
            SimSpendBook.AdjustLive(s4, 1, -1);   // 140 → 120 → 100
            ok(s4.Budgets.Sales == 280,
                "the sum IS the lever after steps down");

            // ── 5. THE ERA CEILING
            GameState s5 = State();
            s5.Era = "garage";
            Booked(s5);
            s5.SpendBook[0].Live = 5990;
            SimSpendBook.Reconcile(s5);
            int before = SimSpendBook.LiveOf(s5.SpendBook[0]);
            SimSpendBook.AdjustLive(s5, 0, 1);
            ok(SimSpendBook.LiveOf(s5.SpendBook[0]) == before && SimSpendBook.AtCap(s5, 0),
                "a step up refuses past the era ceiling");

            // ── 6. The floor
            GameState s6 = State();
            Booked(s6);
            SimSpendBook.AdjustLive(s6, 3, -1);
            ok(SimSpendBook.LiveOf(s6.SpendBook[3]) == 0 && s6.Budgets.Rnd == 0,
                "a step down floors at zero");

            // ── 7. THE LEGACY ABSORB
            GameState s7 = State();
            s7.SpendBook = SimSpendBook.BareBook();
            s7.Budgets.Sales = 1000;
            s7.Budgets.Office = 250;
            SimSpendBook.Reconcile(s7);
            ok(SimSpendBook.LiveOf(s7.SpendBook[0]) == 1000
               && SimSpendBook.LiveOf(s7.SpendBook[3]) == 250 && s7.Budgets.Sales == 1000,
                "the legacy levers land on the first line of their bucket");

            // ── 8. THE MUTATION LAW, stop
            GameState s8 = State();
            Booked(s8);
            SimSpendBook.AdoptBook(s8);
            string verdict = SimSpendBook.StopLine(s8, 0, s8.Week);
            ok(verdict == "stopped" && s8.SpendBook.Count == 4 && s8.Budgets.Sales == 120,
                "a no-notice line stops instantly");

            // ── 9. The contract notice
            GameState s9 = State();
            Booked(s9);
            SimSpendBook.AdoptBook(s9);
            string v9 = SimSpendBook.StopLine(s9, 2, s9.Week);   // care, notice 4
            ok(v9 == "notice" && s9.SpendBook.Count == 5 && s9.Budgets.Care == 120
               && SimSpendBook.NoticeLeft(s9.SpendBook[2], s9.Week + 1) == 3,
                "a contract line bills through its notice");

            // ── 10. The sweep
            int swept = SimSpendBook.SweepLapsed(s9, s9.Week + 4);
            ok(swept == 1 && s9.SpendBook.Count == 4 && s9.Budgets.Care == 0,
                "the notice runs out and the line closes");

            // ── 11. ADD is ink
            GameState s11 = State();
            Booked(s11);
            int idx = SimSpendBook.AddLine(s11, "rnd");
            ok(idx == 5 && s11.Budgets.Rnd == 0 && s11.SpendBook.Count == 6,
                "adding a line is free until raised");

            // ── 12/13. THE VESTING FORMULA
            ok(SimSpendBook.VestedFrac(51, 0) == 0.0,
                "the vesting cliff holds for a year");
            ok(Math.Abs(SimSpendBook.VestedFrac(104, 0) - 0.5) < 0.0001
               && SimSpendBook.VestedFrac(300, 0) == 1.0,
                "vesting runs linear to week 208 and caps");

            // ── 14. THE TEAM LADDER
            ok(SimSpendBook.TeamRung(9) == 1 && SimSpendBook.TeamRung(10) == 2
               && SimSpendBook.TeamRung(40) == 2 && SimSpendBook.TeamRung(41) == 3,
                "the team ladder breaks at ten and forty");

            // ── 15. THE RECEIPT'S ANNUITY
            ok(SimBank.LoanPaymentWk(5000, 0.034, 26) == 293,
                "the receipt's annuity matches the bank's own");
        }
    }
}
