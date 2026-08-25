using System;
using System.Collections.Generic;
using System.Globalization;
using Runway.Core;

namespace Runway.CoreTests
{
    /// <summary>
    /// LANE SUITE — pipeline. Spec: docs/design/05-enterprise-pipeline.md section 13.
    ///
    /// Program.cs calls Run after the engine's own checks and hands over `ok`,
    /// the same assert the whole suite uses: ok(condition, "what this pins").
    /// Six checks, one per spec pin.
    ///
    /// The porting law: a check lands FIRST in game/tests/lanes/test_pipeline.gd,
    /// then here in the same order. Same checks, same order, same logic — the two
    /// engines do not share PRNG internals, so never pin a draw across them, only
    /// behaviour. PIN 1 is the single exception and it is exact on purpose:
    /// LeadAdvanceP is closed-form with no RNG in it, so both engines must land on
    /// the same double to 1e-9 or the advance math has diverged.
    /// </summary>
    public static class PipelineTests
    {
        /// <summary>A garage Enterprise run with nothing bought and nothing
        /// priced: sell 3, no hires, budgets 0, product 50, offers empty.</summary>
        private static GameState Ent(int week = 5)
        {
            var s = new GameState();
            s.SimSeed = 42;
            s.Week = week;
            s.Cash = 50000;
            s.Product = 50;
            s.Morale = 70;
            s.Hype = 40;
            s.BizWhat = "Software";
            s.BizWho = "Enterprise";
            s.Theta = SimEngine.DefaultTheta(s.BizWhat, s.BizWho);
            return s;
        }

        private static Lead L(string name, int seats, string stage, int heat, int age = 0)
        {
            return new Lead { Name = name, Flavor = "", Seats = seats,
                Stage = stage, AgeWeeks = age, Heat = heat };
        }

        public static void Run(Action<bool, string> ok)
        {
            // ── PIN 1 — THE ADVANCE MATH IS EXACT, AND IT MOVES THE RIGHT WAY.
            // No RNG anywhere in LeadAdvanceP, so this is the one value both
            // engines can be held to. Longhand: BASE_ADV.meeting 0.45 x capacity
            // clampf(3.9/1.5, 0.5, 1.0) 1.0 x quality (0.6+0.5x0.8) 1.0 x price
            // 1.0 x heat (0.5+0.55) 1.05 x size JanoDown(6, 10, 0.55)
            // 0.8468976844843414.
            GameState s1 = Ent();
            Lead lead = L("Meridian Logistics", 6, "meeting", 55);
            bool exact = Gd.Absf(SimPipeline.LeadAdvanceP(s1, lead, 1) - 0.400159155919) < 1e-9;
            // monotonicity runs at live 3, where the capacity factor is off its
            // ceiling and a sales budget can actually be felt (at live 1 it is
            // already pinned to 1.0)
            double base3 = SimPipeline.LeadAdvanceP(s1, lead, 3);
            GameState sSales = Ent();
            sSales.Budgets.Sales = 4000;
            Lead big = L("Whale Industrial", 60, "meeting", 55);
            Lead hot = L("Meridian Logistics", 6, "meeting", 100);
            bool monotone = SimPipeline.LeadAdvanceP(sSales, lead, 3) > base3
                && SimPipeline.LeadAdvanceP(s1, big, 3) < base3
                && SimPipeline.LeadAdvanceP(s1, hot, 3) > base3;
            ok(exact && monotone, string.Format(CultureInfo.InvariantCulture,
                "advance math is exact ({0}) and monotone in capacity, size and heat",
                Gd.F(SimPipeline.LeadAdvanceP(s1, lead, 1), 12)));

            // ── PIN 2 — DETERMINISM. Two identical Enterprise runs, five weeks
            // each, land on the same board: same names, same stages, same heat,
            // same ages, same pool, same traction, same logos. A seeded stream is
            // the whole contract.
            GameState a = Ent(1);
            GameState b = Ent(1);
            a.Flags.Add("launched");
            b.Flags.Add("launched");
            a.Traction = 30;
            b.Traction = 30;
            for (int w = 0; w < 5; w++)
            {
                a.Week += 1;
                b.Week += 1;
                SimEngine.WeeklyTick(a);
                SimEngine.WeeklyTick(b);
            }
            bool same = a.Leads.Count == b.Leads.Count && a.Logos.Count == b.Logos.Count
                && a.Traction == b.Traction && Gd.Absf(a.PipeUnits - b.PipeUnits) < 1e-9;
            if (same)
            {
                for (int i = 0; i < a.Leads.Count; i++)
                {
                    if (a.Leads[i].Name != b.Leads[i].Name || a.Leads[i].Stage != b.Leads[i].Stage
                        || a.Leads[i].Heat != b.Leads[i].Heat
                        || a.Leads[i].AgeWeeks != b.Leads[i].AgeWeeks)
                    {
                        same = false;
                        break;
                    }
                }
            }
            ok(same, "two identical Enterprise runs replay the same board over 5 weeks");

            // ── PIN 3 — A COLD DEATH REFUNDS THE POOL, EXACTLY. Unlaunched with
            // no traction the market adds nothing, so every unit in the pool
            // afterwards came out of the dead deal. The refund is asserted as
            // CONSERVATION (pool + the seats the refund immediately re-spawned)
            // because spawns run after deaths in the same tick — which is the
            // stronger pin: nothing was invented or lost.
            GameState s3 = Ent();
            s3.Traction = 0;
            s3.PipeUnits = 0.0;
            s3.Leads = new List<Lead> { L("Vanta Systems", 12, "meeting", 8, 4) };
            WeeklyReport r3 = SimEngine.WeeklyTick(s3);
            bool coldLine = false;
            foreach (string l in r3.Lines)
            {
                if (l.StartsWith("gone cold: Vanta Systems", StringComparison.Ordinal)) { coldLine = true; }
            }
            bool refunded = Gd.Absf(s3.PipeUnits + SimPipeline.SeatsInMotion(s3) - 12.0) < 1e-9;
            bool gone = true;
            foreach (Lead ld in s3.Leads) { if (ld.Name == "Vanta Systems") { gone = false; } }
            ok(gone && coldLine && refunded && s3.PipeStats.Lost == 1,
                "a lead that dies cold refunds all 12 seats to the pool (no-decision, not a no)");

            // ── PIN 4 — A CLOSE CONSERVES SEATS. Twelve seats of pipeline become
            // twelve customers, one named logo and one row of PipeStats — never
            // eleven, never thirteen, and never a number the DM chose.
            GameState s4 = Ent();
            s4.Traction = 5;
            s4.Leads = new List<Lead> { L("Quill Health", 12, "contract", 70, 7) };
            var r4 = new WeeklyReport();
            int booked = SimPipeline.CloseLead(s4, 0, r4);
            bool signedLine = false;
            foreach (string l2 in r4.Lines)
            {
                if (l2.Contains("SIGNED") && l2.Contains("Quill Health")) { signedLine = true; }
            }
            ok(booked == 12 && s4.Traction == 17 && s4.Leads.Count == 0
               && s4.Logos.Count == 1 && s4.Logos[0].Seats == 12
               && s4.PipeStats.Signed == 1 && s4.PipeStats.SeatsSigned == 12 && signedLine,
                "a close books 12 seats, one logo and one SIGNED receipt — exactly");

            // ── PIN 5 — ACCOUNTS CHURN WHOLE. Enterprise revenue is
            // contract-shaped: a logo leaves with all of its seats in one week,
            // never a fraction of itself.
            GameState s5 = Ent();
            s5.Flags.Add("launched");
            s5.Traction = 40;
            s5.Logos = new List<Logo> { new Logo { Name = "Fernbay Group", Seats = 40,
                SinceWk = 1, RenewalWk = 0 } };
            s5.PipeChurnAcc = 40.0;
            SimEngine.WeeklyTick(s5);
            ok(s5.Logos.Count == 0 && s5.Traction == 0,
                "a churning account takes all 40 seats with it, in one week, whole");

            // ── PIN 6 — NON-ENTERPRISE IS UNTOUCHED. The pipeline never reaches
            // SMB or Consumer: no leads, no pool, no stats, no directives, no bang
            // — and section 8's own adds/churn lines are still doing the work.
            GameState s6 = Ent();
            s6.BizWho = "SMB";
            s6.Theta = SimEngine.DefaultTheta("Software", "SMB");
            s6.Flags.Add("launched");
            s6.Traction = 40;
            WeeklyReport r6 = SimEngine.WeeklyTick(s6);
            bool classic = false;
            foreach (string l3 in r6.Lines)
            {
                if (l3.Contains("customers (organic")) { classic = true; }
            }
            ok(s6.Leads.Count == 0 && s6.Logos.Count == 0 && s6.PipeUnits == 0.0
               && s6.PipeStats.Signed == 0 && s6.PipeStats.Lost == 0
               && s6.PipeStats.Spend == 0.0 && r6.Adds > 0 && classic
               && SimPipeline.Directives(s6).Count == 0
               && SimPipeline.Attention(s6).Count == 0
               && SimPipeline.PushLead(s6, "anyone", 40) == "",
                "an SMB run never sees the pipeline — adds and churn stay the engine's own");
        }
    }
}
