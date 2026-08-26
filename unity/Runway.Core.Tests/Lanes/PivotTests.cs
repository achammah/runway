using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Runway.Core;

namespace Runway.CoreTests
{
    /// <summary>
    /// LANE SUITE — the pivot (DAG2 W2 L-COMPANY). Spec:
    /// docs/design/DECISIONS.md § THE PIVOT + 12-binder-rework-2.md § pivot.
    ///
    /// The porting law: a check lands FIRST in game/tests/lanes/test_pivot.gd,
    /// then here in the same order. The two engines do not share PRNG
    /// internals, so the 50–100% roll is pinned per-engine (determinism +
    /// range), never across them.
    /// </summary>
    public static class PivotTests
    {
        static GameState St(long seedV = 4242)
        {
            var s = new GameState();
            s.SimSeed = seedV;
            s.Week = 20;
            s.Era = "office";
            s.Cash = 48000;
            s.Traction = 120;
            s.Product = 62;
            s.Morale = 66;
            s.Hype = 40;
            s.BizWhat = "Software";
            s.BizWho = "SMB";
            s.Theta = SimEngine.DefaultTheta(s.BizWhat, s.BizWho);
            s.SetFlag("launched");
            s.ContentEquity = 1800.0;
            s.TechDebt = 46.0;
            s.ServedTotal = 900;
            s.PlatformLevel = 2;
            s.LoanPrincipal = 9000;
            s.Loans = new List<Loan> { new Loan { Kind = "bank", Principal = 10000,
                Balance = 8200, RateWk = 0.04, TermWk = 8, TakenWeek = 12,
                PayWk = 1480, Missed = 0 } };
            s.Employees = new List<Employee>
            {
                new Employee { Name = "Priya Raman", Role = "engineer", Salary = 1500,
                    Burnout = 20, Skill = 4, HiredWeek = 6, Site = "" },
                new Employee { Name = "Tomas Beck", Role = "sales", Salary = 1100,
                    Burnout = 30, Skill = 3, HiredWeek = 9, Site = "" },
            };
            s.Leads = new List<Lead>
            {
                new Lead { Name = "Meridian Logistics",
                    Flavor = "forty depots, one spreadsheet", Seats = 40,
                    Stage = "pilot", AgeWeeks = 3, Heat = 88 },
                new Lead { Name = "Corvid Freight", Flavor = "", Seats = 22,
                    Stage = "procurement", AgeWeeks = 6, Heat = 55 },
            };
            s.Logos = new List<Logo>
            {
                new Logo { Name = "Quill Health", Seats = 12, SinceWk = 8, RenewalWk = 60 },
                new Logo { Name = "Fernbay Group", Seats = 9, SinceWk = 14, RenewalWk = 66 },
            };
            s.PipeUnits = 12.0;
            s.PipeStats = new PipeStats { Signed = 4, Lost = 7, CycleSum = 28,
                SeatsSigned = 21, Spend = 6500.0, FirstWk = 8 };
            s.Beliefs = new Beliefs { Tam = 90000.0, LifetimeWk = 44.0 };
            s.Bets = new List<Bet> { new Bet { Id = "b1", Name = "Alerts that matter",
                Kind = "retention", Ambition = 2, CostRndWeeks = 6.0, Progress = 3.0,
                Committed = true } };
            s.Features = new List<Feature> { new Feature { Id = "f1",
                Name = "online booking", Job = "pull", Family = "", Solidity = "solid",
                KeepWk = 40, UnitCostAdd = 0.0, ProductId = "", BornWk = 1,
                Measured = 0.0 } };
            return s;
        }

        public static void Run(Action<bool, string> ok)
        {
            PinAudience(ok);
            PinProduct(ok);
            PinDebtsSurvive(ok);
            PinDeterminismAndRange(ok);
            PinArmFlow(ok);
            PinPreviewPure(ok);
            PinRefusals(ok);
        }

        /// <summary>The registration alias the coordinator directive names.</summary>
        public static void RunAll(Action<bool, string> ok) { Run(ok); }

        // ── 1 · THE AUDIENCE PIVOT — the market dies, the shop survives ────
        static void PinAudience(Action<bool, string> ok)
        {
            GameState s = St();
            int cash0 = s.Cash;
            int crew0 = s.Employees.Count;
            PivotReceipt res = SimPivot.PivotAudience(s, "Consumer");
            ok(res.Ok, "pivot: audience executor accepts a real new audience");
            ok(s.Traction == 0, "pivot(audience): customers go to zero");
            ok(s.Leads.Count == 0 && s.Logos.Count == 0 && s.PipeUnits == 0.0,
                "pivot(audience): named deals, logos and loose interest all die");
            ok(s.ContentEquity == 0.0, "pivot(audience): the content well drains");
            ok(s.Beliefs == null, "pivot(audience): market beliefs re-fog");
            ok(s.BizWho == "Consumer" && Gd.Absf(s.Theta.Tam - 900000.0) < 1.0,
                "pivot(audience): the world reprices itself for the new audience");
            ok(s.Product == 62 && s.TechDebt == 46.0 && s.Features.Count == 1
               && s.Bets.Count == 1 && s.ServedTotal == 900,
                "pivot(audience): the product survives as built");
            ok(s.Cash == cash0 && s.Employees.Count == crew0,
                "pivot(audience): the cash and the team survive");
            ok(s.Pivots == 1 && s.HasFlag("pivoted"),
                "pivot(audience): the record notes the pivot");
            ok(res.Lines.Count >= 5,
                "pivot(audience): the receipt speaks in full lines");
        }

        // ── 2 · THE PRODUCT PIVOT — the product dies, the market learning survives
        static void PinProduct(Action<bool, string> ok)
        {
            GameState s = St();
            int cash0 = s.Cash;
            double well0 = s.ContentEquity;
            PivotReceipt res = SimPivot.PivotProduct(s, "");
            ok(res.Ok, "pivot: product executor fires on the same craft");
            int lost = res.LostCustomers;
            ok(lost >= 60 && lost <= 120 && s.Traction == 120 - lost,
                "pivot(product): the roll takes between 50% and 100% of the customers");
            ok(s.Product == 10 && s.Bets.Count == 0 && s.PlatformLevel == 0
               && s.Features.Count == 0,
                "pivot(product): version v0.1, bets, platform and features all die");
            ok(s.TechDebt == 0.0, "pivot(product): tech debt clears with its codebase");
            ok(s.ServedTotal == 0, "pivot(product): serving practice restarts with the product");
            ok(s.Leads.Count == 2 && s.Leads[0].Stage == "meeting" && s.Leads[0].AgeWeeks == 0,
                "pivot(product): named deals survive, knocked back to the first meeting");
            ok(s.ContentEquity == well0 && s.PipeStats != null && s.PipeStats.Signed == 4,
                "pivot(product): the well and the sales learning survive");
            ok(s.Cash == cash0, "pivot(product): the cash survives");
            ok(s.Pivots == 1 && s.HasFlag("pivoted"),
                "pivot(product): the record notes the pivot");
            // the craft may change with the product
            GameState s2 = St();
            PivotReceipt res2 = SimPivot.PivotProduct(s2, "Service");
            ok(res2.Ok && s2.BizWhat == "Service",
                "pivot(product): a new craft lands and the world reprices it");
        }

        // ── 3 · DEBTS SURVIVE BOTH — the bank does not forget ──────────────
        static void PinDebtsSurvive(Action<bool, string> ok)
        {
            GameState a = St();
            SimPivot.PivotAudience(a, "Enterprise");
            ok(a.LoanPrincipal == 9000 && a.Loans.Count == 1 && a.Loans[0].Balance == 8200,
                "pivot(audience): every note on the books survives untouched");
            GameState p = St();
            SimPivot.PivotProduct(p, "");
            ok(p.LoanPrincipal == 9000 && p.Loans.Count == 1,
                "pivot(product): every note on the books survives untouched");
        }

        // ── 4 · DETERMINISM + RANGE — the roll replays, and stays in its band
        static void PinDeterminismAndRange(Action<bool, string> ok)
        {
            GameState a = St(77);
            GameState b = St(77);
            SimPivot.PivotProduct(a, "");
            SimPivot.PivotProduct(b, "");
            ok(a.Traction == b.Traction,
                "pivot: the same seed and week rolls the same loss (replayable)");
            bool seenLow = false;
            bool seenHigh = false;
            for (int sd = 1; sd < 40; sd++)
            {
                GameState s = St(sd);
                PivotReceipt res = SimPivot.PivotProduct(s, "");
                int pct = res.LossPct;
                if (pct < 75) seenLow = true;
                if (pct >= 75) seenHigh = true;
                if (pct < 50 || pct > 100)
                {
                    ok(false, "pivot: a loss roll left the 50–100% band");
                    return;
                }
            }
            ok(seenLow && seenHigh,
                "pivot: the loss roll actually spreads across its band");
        }

        // ── 5 · THE ARM FLOW — flag in, Esc out, LOCK IN resolves ──────────
        static void PinArmFlow(Action<bool, string> ok)
        {
            GameState s = St();
            ok(SimPivot.Armed(s) == null && SimPivot.ResolveArmed(s) == null,
                "pivot: an unarmed company resolves to nothing");
            ok(!SimPivot.ArmAudience(s, "SMB"),
                "pivot: arming toward the audience you already serve is refused");
            ok(SimPivot.ArmAudience(s, "Consumer")
               && SimPivot.Armed(s).Kind == "audience"
               && SimPivot.Armed(s).Target == "Consumer",
                "pivot: the armed flag carries the door and the destination");
            ok(SimPivot.Attention(s).Count == 1 && SimPivot.Attention(s)[0].Severity == 3,
                "pivot: an armed pivot is a sev-3 alarm");
            ok(SimPivot.Directives(s).Count == 1,
                "pivot: an armed pivot briefs the DM");
            SimPivot.Disarm(s);
            ok(SimPivot.Armed(s) == null && s.Pivots == 0,
                "pivot: disarm abandons the whole intent and nothing fired");
            ok(SimPivot.ArmProduct(s, "") && !SimPivot.ArmProduct(s, "Bakery"),
                "pivot: the product arm takes the same craft and refuses a nonsense one");
            PivotReceipt res = SimPivot.ResolveArmed(s);
            ok(res != null && res.Ok && res.Kind == "product"
               && s.Pivots == 1 && SimPivot.Armed(s) == null,
                "pivot: LOCK IN resolves the armed pivot exactly once");
            ok(SimPivot.Attention(s).Count == 0 && SimPivot.Directives(s).Count == 0,
                "pivot: a fired pivot stops shouting");
        }

        // ── 6 · THE PREVIEW is pure — it prices, it never touches ──────────
        static void PinPreviewPure(Action<bool, string> ok)
        {
            GameState s = St();
            string before = JsonConvert.SerializeObject(s);
            PivotPreview pa = SimPivot.Preview(s, "audience");
            PivotPreview pp = SimPivot.Preview(s, "product");
            ok(pa.CustomersLost == 120 && pa.DealsDead == 2 && pa.Well == 1800,
                "pivot: the audience preview prices the live books");
            ok(pp.VersionFrom == "v0.6" && pp.VersionTo == "v0.1" && pp.DebtCleared == 46,
                "pivot: the product preview prices the live books");
            ok(pa.Debts == 17200 && pp.Debts == 17200,
                "pivot: both previews name the debts that survive");
            ok(JsonConvert.SerializeObject(s) == before,
                "pivot: the preview mutates nothing at all");
        }

        // ── 7 · REFUSALS — hostile input bounces with a reason ─────────────
        static void PinRefusals(Action<bool, string> ok)
        {
            GameState s = St();
            PivotReceipt r1 = SimPivot.PivotAudience(s, "SMB");
            PivotReceipt r2 = SimPivot.PivotAudience(s, "Martians");
            PivotReceipt r3 = SimPivot.PivotProduct(s, "Bakery");
            ok(!r1.Ok && !r2.Ok && !r3.Ok && s.Pivots == 0 && s.Traction == 120,
                "pivot: refused pivots change nothing and say why");
        }
    }
}
