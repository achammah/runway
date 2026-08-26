using System;
using System.Collections.Generic;
using System.Globalization;
using Runway.Core;

namespace Runway.CoreTests
{
    /// <summary>
    /// LANE SUITE — the works (L-DIVWORKS live pins). Twin of
    /// game/tests/lanes/test_works.gd — same checks, same order, same
    /// messages; behaviour and bands, never a cross-engine draw.
    /// </summary>
    public static class WorksTests
    {
        static GameState Base(string what, string who)
        {
            var s = new GameState();
            s.SimSeed = 4242;
            s.Week = 12;
            s.Era = "office";
            s.Cash = 60000;
            s.Traction = 90;
            s.Product = 50;
            s.Morale = 70;
            s.Hype = 30;
            s.BizWhat = what;
            s.BizWho = who;
            s.Theta = SimEngine.DefaultTheta(what, who);
            s.SetFlag("launched");
            return s;
        }

        static void Priced(GameState s, string name, string unit, double fair,
            double cost, double weight = 1.0)
        {
            Offer od = SimEngine.AddOffer(s, name, unit, fair, cost, 2.0, weight);
            od.Price = fair;
            od.PriceSet = true;
        }

        static Employee Emp(string name, string role, int salary, int skill, string site)
        {
            return new Employee { Name = name, Role = role, Salary = salary,
                Burnout = 10, Quirk = "", Skill = skill, HiredWeek = 3, Site = site };
        }

        static bool Approx(double a, double b)
        {
            return Math.Abs(a - b) < 0.0001;
        }

        static double N(Dictionary<string, object> d, string k, double dflt = 0.0)
        {
            return SimWorks.Num(d, k, dflt);
        }

        public static void Run(Action<bool, string> ok)
        {
            // ── 1 · no offers, no works
            GameState s0 = Base("Service", "Consumer");
            ok(SimWorks.Attention(s0).Count == 0 && SimWorks.Directives(s0).Count == 0,
                "works: without a catalog the works stays quiet");

            // ── 2 · the neutral (offer-less) tick books no relief
            GameState s1 = Base("Service", "Consumer");
            SimEngine.WeeklyTick(s1);
            Pnl pnl = s1.LastPnl;
            ok(pnl.Relief == 0, "works: the neutral tick books no relief spend");

            // ── 3 · all three DAG2 lanes registered, at zero here
            ok(pnl.RecruitAds == 0 && pnl.Relief == 0 && pnl.SiteRent == 0,
                "works: the three new pnl lanes are pre-registered at zero");

            // ── 4 · the identity still balances
            ok(pnl.Net == pnl.Revenue - pnl.Burn - pnl.LiabilitiesWk - pnl.Interest - pnl.Tax,
                "works: the pnl identity holds with the new lanes registered");

            // ── 5 · a site tag on a person rides the tick untouched
            GameState s2 = Base("Service", "Consumer");
            s2.Employees.Add(Emp("June Park", "therapist", 1500, 4, "site_lyon"));
            SimEngine.WeeklyTick(s2);
            ok(s2.Employees.Count == 1 && s2.Employees[0].Site == "site_lyon",
                "works: a site tag on an employee survives the tick");

            // ── 6 · service capacity is the crew's hands
            GameState s3 = Base("Service", "SMB");
            Priced(s3, "the classic", "per session", 80.0, 31.0);
            ok(Approx(SimWorks.ServiceCapacity(s3), 26.0),
                "works: a solo founder's hands hold 26 slots");
            s3.Employees.Add(Emp("June Park", "therapist", 1500, 4, ""));
            s3.Employees.Add(Emp("Sal Ory", "sales lead", 1200, 4, ""));
            ok(Approx(SimWorks.ServiceCapacity(s3), 26.0 + 26.0),
                "works: a skill-4 hand adds 26 slots and a seller adds none");
            SimDivisions.Mark(s3, "works_ramp", "June Park", s3.Week + 1);
            ok(Approx(SimWorks.ServiceCapacity(s3), 26.0),
                "works: a ramping hand gives zero this week");

            // ── 7 · the service gap is UN-BILLED revenue
            GameState s4 = Base("Service", "SMB");
            Priced(s4, "the classic", "per session", 80.0, 31.0);
            WeeklyReport rep4 = SimEngine.WeeklyTick(s4);
            Pnl pnl4 = s4.LastPnl;
            Dictionary<string, object> w4 = SimWorks.WeekView(s4);
            ok(N(w4, "walk_units") >= 1.0,
                "works: ninety wanted sessions overflow a solo founder's hands");
            ok(pnl4.Revenue < Gd.RoundToInt(s4.Traction * SimEngine.OffersArpu(s4)),
                "works: walked sessions are un-billed — revenue is smaller than customers × price");
            bool said4 = false;
            foreach (string l in rep4.Lines)
                if (l.Contains("turned away")) said4 = true;
            ok(said4, "works: the walk is receipted, not silent");
            ok(pnl4.Net == pnl4.Revenue - pnl4.Burn - pnl4.LiabilitiesWk
               - pnl4.Interest - pnl4.Tax,
                "works: the identity holds while revenue walks");

            // ── 8 · the freelance valve
            GameState s5 = Base("Service", "SMB");
            Priced(s5, "the classic", "per session", 80.0, 31.0);
            s5.PriceBook = new Dictionary<string, object> { { "freelance_rate", 48 } };
            ok(SimWorks.ReliefSet(s5, "freelance", 999) == 60,
                "works: the freelance cap clamps at the engine, not the desk");
            SimWorks.ReliefSet(s5, "freelance", 20);
            SimEngine.WeeklyTick(s5);
            Pnl pnl5 = s5.LastPnl;
            Dictionary<string, object> w5 = SimWorks.WeekView(s5);
            ok(N(w5, "relief_used") >= 1.0
               && pnl5.Relief == Gd.RoundToInt(N(w5, "relief_used") * 48.0),
                "works: freelancers bill per unit served at the price book's rate");
            ok(N(w5, "walk_units", 99.0) < N(SimWorks.WeekView(s4), "walk_units"),
                "works: the valve open, fewer sessions walk than with it closed");

            // ── 9 · software degrades instead of turning away
            GameState s6 = Base("Software", "Consumer");
            Priced(s6, "the plan", "per month", 18.0, 4.0);
            s6.Traction = 3000;   // far over the 400-seat free ceiling at zero care spend
            SimEngine.WeeklyTick(s6);
            Dictionary<string, object> w6 = SimWorks.WeekView(s6);
            ok(N(w6, "over") > 0.0 && (int)N(w6, "degrade_walked") >= 1,
                "works: past the ceiling the queue churns people — degradation, not lost sales");
            ok(N(w6, "unbilled") < 1.0,
                "works: software never un-bills — its gap is churn, not walked revenue");
            GameState s7 = Base("Software", "Consumer");
            Priced(s7, "the plan", "per month", 18.0, 4.0);
            s7.Traction = 120;
            SimEngine.WeeklyTick(s7);
            ok((int)N(SimWorks.WeekView(s7), "degrade_walked") == 0,
                "works: under the ceiling nobody churns to the queue");

            // ── 10 · the marketplace starves on growth; the push feeds it
            GameState s8 = Base("Marketplace", "Consumer");
            Priced(s8, "a matched order", "per order", 9.0, 3.5);
            s8.LastGrowth = 0.30;
            SimEngine.WeeklyTick(s8);
            Dictionary<string, object> w8 = SimWorks.WeekView(s8);
            ok(N(w8, "walk_units") >= 1.0,
                "works: fast growth outruns the seller pool and shelves go empty");
            GameState s9 = Base("Marketplace", "Consumer");
            Priced(s9, "a matched order", "per order", 9.0, 3.5);
            s9.LastGrowth = 0.30;
            SimWorks.ReliefSet(s9, "recruit_supply", 500);
            SimEngine.WeeklyTick(s9);
            ok(N(SimWorks.WeekView(s9), "walk_units", 99.0) < N(w8, "walk_units")
               && s9.LastPnl.Relief == 500,
                "works: the recruit push spends whole and closes part of the gap");

            // ── 11 · hardware stays the factory's
            GameState s10 = Base("Hardware", "Consumer");
            Priced(s10, "Pocket Synth", "per unit", 100.0, 20.0);
            SimEngine.WeeklyTick(s10);
            ok(s10.LastPnl.Relief == 0,
                "works: on hardware the factory owns the molecule — the works books nothing");

            // ── 12 · the unit ticket: cost lines × learning + the features' share
            GameState s11 = Base("Service", "SMB");
            Offer o11 = SimEngine.AddOffer(s11, "the classic", "per session", 80.0, 31.0,
                2.0, 1.0,
                new List<CostLine>
                {
                    new CostLine { Label = "hands, 50 min", Amount = 22.0 },
                    new CostLine { Label = "oils & linens", Amount = 4.0 },
                    new CostLine { Label = "room & laundry", Amount = 5.0 },
                }, new List<CostLine>());
            o11.Price = 80.0;
            o11.PriceSet = true;
            s11.Features.Add(new Feature { Id = "f1", Name = "the loyalty card",
                Job = "keep", Family = "", Solidity = "solid", KeepWk = 12,
                UnitCostAdd = 1.5, ProductId = "", BornWk = 1, Measured = 0.0 });
            Dictionary<string, object> t11 = SimWorks.UnitTicket(s11, 0);
            ok(Approx(N(t11, "cost_each"), 31.0 * SimEngine.LearningCurve(s11) + 1.5),
                "works: the ticket is cost lines × learning at the total, plus the features' share");
            ok(((List<Dictionary<string, object>>)t11["lines"]).Count == 4,
                "works: the ticket itemizes its lines and the features' share rides last");

            // ── 13 · the gap raises the works' own attention
            GameState s12 = Base("Service", "SMB");
            Priced(s12, "the classic", "per session", 80.0, 31.0);
            SimEngine.WeeklyTick(s12);
            bool gapRow = false;
            foreach (AttentionItem r in SimWorks.Attention(s12))
                if (r.Desk == "the works" && r.Key == "works_gap") gapRow = true;
            ok(gapRow, "works: money walking raises a warn on the works desk");

            // ── 14 · retire_product: half migrate, the rest churn
            GameState s13 = Base("Software", "SMB");
            Priced(s13, "Core", "per month", 18.0, 4.0, 1.0);
            Priced(s13, "Legacy API", "per month", 12.0, 9.0, 1.0);
            SimDivisions.TagOffer(s13, 1, "legacy");
            int t13 = s13.Traction;
            Dictionary<string, object> res13 = SimWorks.RetireProduct(s13, "legacy");
            ok((bool)res13["ok"] && s13.Offers.Count == 1
               && t13 - s13.Traction == Convert.ToInt32(res13["churned"], CultureInfo.InvariantCulture)
               && Convert.ToInt32(res13["churned"], CultureInfo.InvariantCulture)
                   == (int)Math.Floor(t13 * 0.5 * 0.5),
                "works: retiring a product churns exactly the un-migrated half of its share");
            ok(!(bool)SimWorks.RetireProduct(s13, "")["ok"],
                "works: the only product cannot retire — that is a pivot");

            // ── 15 · fire_account
            GameState s14 = Base("Service", "SMB");
            Priced(s14, "the classic", "per session", 80.0, 31.0);
            s14.PriceBook = new Dictionary<string, object> { { "account_fire_penalty", 1200 } };
            int cash14 = s14.Cash;
            int t14 = s14.Traction;
            Dictionary<string, object> res14 = SimWorks.FireAccount(s14);
            ok((bool)res14["ok"] && cash14 - s14.Cash == 1200 && t14 - s14.Traction == 1
               && SimEngine.HasStatus(s14, "rival_fud"),
                "works: firing an account bills the penalty, kills the revenue, and the street hears");

            // ── 16 · refinance_note
            GameState s15 = Base("Service", "SMB");
            Priced(s15, "the classic", "per session", 80.0, 31.0);
            s15.PriceBook = new Dictionary<string, object> { { "refinance_break_fee", 350 } };
            s15.Loans.Add(new Loan { Kind = "bank", Principal = 10000, Balance = 8000,
                RateWk = 0.11, TermWk = 12, TakenWeek = 2, PayWk = 1200, Missed = 0 });
            int cash15 = s15.Cash;
            Dictionary<string, object> res15 = SimWorks.RefinanceNote(s15, 0, 12);
            Loan note15 = s15.Loans[0];
            ok((bool)res15["ok"] && cash15 - s15.Cash == 350
               && Approx(note15.RateWk, SimBank.BankRateWk(s15))
               && note15.PayWk == SimBank.LoanPaymentWk(8000, SimBank.BankRateWk(s15), 12),
                "works: refinance swaps to today's standing for the break fee");
            s15.Loans[0].Missed = 2;
            ok(!(bool)SimWorks.RefinanceNote(s15, 0, 12)["ok"],
                "works: a distressed note never refinances — the miss ladder cannot be laundered");
        }
    }
}
