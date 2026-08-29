using System;
using System.Collections.Generic;
using Runway.Core;

namespace Runway.CoreTests
{
    /// <summary>
    /// LANE SUITE — factory. Spec: docs/design/09-hardware.md 14 (the twin pins).
    ///
    /// Program.cs calls Run after the engine's own checks and hands over `ok`,
    /// the same assert the whole suite uses.
    ///
    /// The porting law: every check below landed FIRST in
    /// game/tests/lanes/test_factory.gd and is repeated here in the same order,
    /// with the same logic. The two engines do not share PRNG internals, so
    /// nothing here pins a draw across them, only behaviour.
    /// </summary>
    public static class FactoryTests
    {
        /// <summary>A live Hardware run with a priced flagship: $100 per unit, $20 of parts.</summary>
        static GameState Hw(string era = "garage")
        {
            var s = new GameState();
            s.SimSeed = 42;
            s.Week = 5;
            s.Cash = 50000;
            s.Traction = 40;
            s.Product = 50;
            s.Morale = 70;
            s.Hype = 40;
            s.BizWhat = "Hardware";
            s.BizWho = "Consumer";
            s.Era = era;
            s.Theta = SimEngine.DefaultTheta(s.BizWhat, s.BizWho);
            SimEngine.AddOffer(s, "Pocket Synth", "per unit", 100.0, 20.0, 2.0, 1.0);
            s.Offers[0].Price = 100.0;
            s.Offers[0].PriceSet = true;
            s.SetFlag("launched");
            return s;
        }

        static Pnl P(GameState s)
        {
            return s.LastPnl ?? new Pnl();
        }

        public static void Run(Action<bool, string> ok)
        {
            // ── THE CAPACITY WEIGHT (owner: a sale eats the hours it takes).
            // capacity 2.0 doubles demand units; absent = 1.0 (old saves hold).
            var cw = new GameState { BizWhat = "Service", BizWho = "SMB", Traction = 10 };
            cw.Offers = new List<Offer> { new Offer { Name = "flat", Unit = "per session",
                Weight = 1.0, FairPrice = 40.0, Price = 30.0, UnitCost = 5.0 } };
            double flat = SimWorks.DemandUnits(cw);
            cw.Offers[0].CapacityPerUnit = 2.0;
            ok(Gd.Absf(SimWorks.DemandUnits(cw) - flat * 2.0) < 0.0001,
                "capacity 2.0 doubles the slots a sale eats; absent reads 1.0");
            // ── PIN 1 — STOCKOUT CAPS ADDS. You cannot sell what you did not
            // build: demand exists, the shelf is empty, and every new customer is
            // lost sales (consumer hardware does not backorder). Empty shelves
            // also push the people who came back out: fill 0 → churn x1.35 exactly.
            GameState s1 = Hw();
            HardwareState hw1 = SimFactory.HwState(s1);
            hw1.CapacityBase = 0.0;
            hw1.ProductionTarget = 0;
            hw1.Stock = 0;
            int t1 = s1.Traction;
            WeeklyReport r1 = SimEngine.WeeklyTick(s1);
            Dictionary<string, object> w1 = SimFactory.WeekBlock(s1);
            ok(r1.Adds == 0, "stockout: an empty shelf lands zero customers");
            ok(Convert.ToInt32(w1["lost_adds"]) > 0, "stockout: the lost sales are receipted, not silent");
            ok(s1.Traction <= t1, "stockout: traction never rises off an empty shelf");
            ok(Math.Abs(Convert.ToDouble(w1["fill"])) < 0.0001
               && Math.Abs(SimFactory.StarveChurnMult(s1) - 1.35) < 0.0001,
                "stockout: fill rate 0 makes churn exactly x1.35");

            // ── PIN 2 — CARRYING BILLS. 50 units of a $20 unit at 2%/wk = $20,
            // and it joins burn like every other dollar.
            GameState s2 = Hw();
            HardwareState hw2 = SimFactory.HwState(s2);
            hw2.CapacityBase = 0.0;
            hw2.ProductionTarget = 0;
            hw2.Stock = 50;
            s2.Traction = 0;
            s2.Flags.Remove("launched");
            SimEngine.WeeklyTick(s2);
            GameState s2b = Hw();
            HardwareState hw2b = SimFactory.HwState(s2b);
            hw2b.CapacityBase = 0.0;
            hw2b.ProductionTarget = 0;
            hw2b.Stock = 0;
            s2b.Traction = 0;
            s2b.Flags.Remove("launched");
            SimEngine.WeeklyTick(s2b);
            ok(P(s2).Carrying == 20, "carrying: 50 units x 2% of $20 = $20 a week on the shelf");
            ok(P(s2).Burn - P(s2b).Burn == 20, "carrying: the shelf's rent is inside burn, to the dollar");

            // ── PIN 3 — MAKE VS BUY. The jobber's ceiling is a multiple of YOUR
            // OWN footprint and the premium is the era's; no learning rides it,
            // and a subcontracted unit never touches the shelf.
            ok(SimFactory.SubCapUnits(Hw("office"), 5.0) == 15
               && SimFactory.SubCapUnits(Hw("coworking"), 5.0) == 5
               && SimFactory.SubCapUnits(Hw("garage"), 5.0) == 0,
                "make vs buy: the ceiling is 3x footprint at office, 1x at coworking, shut in the garage");
            ok(Math.Abs(SimFactory.SubMult("coworking") - 1.6) < 0.0001
               && Math.Abs(SimFactory.SubMult("floor") - 1.45) < 0.0001
               && Math.Abs(SimFactory.SubMult("hq") - 1.35) < 0.0001,
                "make vs buy: committed volume prices the premium down 1.6x → 1.45x → 1.35x");
            GameState s3 = Hw("office");
            HardwareState hw3 = SimFactory.HwState(s3);
            hw3.CapacityBase = 5.0;
            hw3.ProductionTarget = 0;
            hw3.Stock = 0;
            hw3.SubcontractOn = true;
            hw3.ProducedTotal = 1000;      // a deep learning curve the CM does NOT get
            SimEngine.WeeklyTick(s3);
            ok(Convert.ToInt32(SimFactory.WeekBlock(s3)["sub_units"]) == 15
               && P(s3).Subcontract == 480,
                "make vs buy: 15 units at 1.6x $20 = $480, with no learning discount");
            ok(s3.Hardware.Stock == 0, "make vs buy: made-to-order units never enter stock");

            // ── PIN 4 — A NON-HARDWARE RUN IS UNTOUCHED. No state, no lane, no
            // line, no roll, no row: the absence is the test.
            var s4 = new GameState();
            s4.SimSeed = 42;
            s4.Week = 5;
            s4.Cash = 50000;
            s4.Traction = 40;
            s4.Product = 50;
            s4.BizWhat = "Software";
            s4.BizWho = "SMB";
            s4.Theta = SimEngine.DefaultTheta(s4.BizWhat, s4.BizWho);
            WeeklyReport r4 = SimEngine.WeeklyTick(s4);
            Pnl p4 = P(s4);
            bool hwWords = false;
            foreach (string l in r4.Lines)
            {
                if (l.Contains("STOCKOUT") || l.Contains("carrying ") || l.Contains("built ")
                    || l.Contains("make vs buy") || l.Contains("machine down")) hwWords = true;
            }
            ok(s4.Hardware == null && SimFactory.WeekBlock(s4).Count == 0,
                "off Hardware: the factory state is never allocated");
            ok(p4.Production == 0 && p4.Subcontract == 0 && p4.EquipUpkeep == 0 && p4.Carrying == 0,
                "off Hardware: none of the four factory lanes carry a dollar");
            ok(!hwWords && SimFactory.Attention(s4).Count == 0
               && SimFactory.Directives(s4).Count == 0
               && Math.Abs(SimFactory.ClampAdds(s4, new WeeklyReport(), 7.5) - 7.5) < 0.0001,
                "off Hardware: no receipt, no bang, no directive, and demand is stock-free");

            // ── PIN 5 — DETERMINISM. The salt-110 breakdown stream and the
            // salt-111 repurchase remainder replay exactly, six weeks running.
            GameState s5a = Hw("office");
            GameState s5b = Hw("office");
            foreach (GameState st in new[] { s5a, s5b })
            {
                HardwareState h = SimFactory.HwState(st);
                h.CapacityBase = 40.0;
                for (int i = 0; i < 8; i++)
                {
                    h.Equipment.Add(new EquipmentItem { Id = "jig", Name = "Assembly Jig",
                        CapacityAdd = 6.0, UpkeepWk = 15.0, BoughtWeek = 1 });
                }
            }
            bool same = true;
            for (int i = 0; i < 6; i++)
            {
                s5a.Week += 1;
                s5b.Week += 1;
                SimEngine.WeeklyTick(s5a);
                SimEngine.WeeklyTick(s5b);
                Pnl pa = P(s5a);
                Pnl pb = P(s5b);
                if (s5a.Hardware.Stock != s5b.Hardware.Stock
                    || s5a.Hardware.ProducedTotal != s5b.Hardware.ProducedTotal
                    || pa.Production != pb.Production || pa.Subcontract != pb.Subcontract
                    || pa.EquipUpkeep != pb.EquipUpkeep || pa.Carrying != pb.Carrying) same = false;
            }
            ok(same, "determinism: same seed and week rebuild the same shelf and the same four lanes");

            // ── PIN 6 — THE LEARNING CURVE AND THE ERA GATE. Wright's law on
            // units BUILT: 10 made = 1 - 0.115*log10(10) = 0.885, and it is what
            // production actually pays. A garage cannot sign for a CNC cell.
            GameState s6 = Hw();
            HardwareState hw6 = SimFactory.HwState(s6);
            hw6.CapacityBase = 100.0;
            hw6.ProductionTarget = 20;
            hw6.ProducedTotal = 10;
            ok(Math.Abs(SimFactory.Learning(s6) - 0.885) < 0.0001,
                "learning curve: 10 units built takes 11.5% off the next one");
            int cash6 = s6.Cash;
            SimFactory.Verdict gate = SimFactory.BuyEquipment(s6, "cnc");
            ok(!gate.Ok && s6.Cash == cash6 && gate.Why.Contains("office"),
                "era gate: a garage is refused a CNC cell, and the refusal says why");
            SimEngine.WeeklyTick(s6);
            ok(P(s6).Production == 354,
                "learning curve: 20 units at $20 x 0.885 = $354 of production, not $400");

            // ── THE WAY BACK OUT (docs/design/DECISIONS.md #4): equipment sells
            // back at half price — CAPEX is forgiving, and costly.
            GameState s7 = Hw();
            double base7 = SimFactory.Capacity(s7);
            ok(SimFactory.BuyEquipment(s7, "jig").Ok && s7.Cash == 49100
               && Math.Abs(SimFactory.Capacity(s7) - (base7 + 6.0)) < 0.0001,
                "a jig costs $900 and puts 6 units a week on the bench");
            SimFactory.Verdict sold = SimFactory.SellEquipment(s7, 0);
            ok(sold.Ok && s7.Cash == 49550 && Math.Abs(SimFactory.Capacity(s7) - base7) < 0.0001
               && sold.Back == 450,
                "resale: the secondhand market pays half, and the capacity leaves with it");

            // ── THE FLOOR HOLDS TWELVE. Capacity is bought in lumps, not infinitely.
            GameState s8 = Hw();
            s8.Cash = 1000000;
            for (int i = 0; i < 13; i++) SimFactory.BuyEquipment(s8, "jig");
            ok(SimFactory.HwView(s8).Equipment.Count == 12,
                "the fleet caps at 12 machines, and the 13th is refused with a reason");

            // ── AUTO IS A BASE-STOCK POLICY: order up to four weeks of the
            // smoothed forecast, minus the shelf, and never spend a quarter of
            // the cash at once.
            GameState s9 = Hw();
            HardwareState hw9 = SimFactory.HwState(s9);
            hw9.CapacityBase = 100.0;
            hw9.DemandEma = 10.0;
            hw9.Stock = 0;
            ok(SimFactory.TargetNow(s9, 100.0, 20.0) == 40,
                "AUTO: four weeks of cover on a 10/wk forecast is a 40-unit order");
            hw9.Stock = 35;
            ok(SimFactory.TargetNow(s9, 100.0, 20.0) == 5,
                "AUTO: what is already on the shelf comes off the order");
            s9.Cash = 200;
            hw9.Stock = 0;
            ok(SimFactory.TargetNow(s9, 100.0, 20.0) == 2,
                "AUTO: a quarter of $200 at $20 a unit is two units, and no more");
        }
    }
}
