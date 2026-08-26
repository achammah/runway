using System;
using System.Collections.Generic;
using System.Globalization;
using Runway.Core;

namespace Runway.CoreTests
{
    /// <summary>
    /// LANE SUITE — divisions &amp; sites (L-DIVWORKS live pins). Twin of
    /// game/tests/lanes/test_divisions.gd — same checks, same order, same
    /// messages; behaviour and bands, never a cross-engine draw.
    /// </summary>
    public static class DivisionsTests
    {
        static GameState St()
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
            s.BizWhat = "Service";
            s.BizWho = "SMB";
            s.Theta = SimEngine.DefaultTheta(s.BizWhat, s.BizWho);
            Offer o = SimEngine.AddOffer(s, "the classic session", "per session",
                80.0, 31.0, 2.0, 1.0);
            o.Price = 80.0;
            o.PriceSet = true;
            s.SetFlag("launched");
            return s;
        }

        static void Book(GameState s)
        {
            s.PriceBook = new Dictionary<string, object>
            {
                { "open_site_pack", 18000 }, { "relocation_fee", 400 },
                { "machine_shipping", 900 }, { "lease_break_weeks", 8 },
                { "contract_notice_wks", 4 }, { "refinance_break_fee", 350 },
                { "freelance_rate", 65 }, { "subcontract_rate", 30 },
                { "account_fire_penalty", 1200 },
            };
        }

        static void SiteRec(GameState s, string id, int rent, double wage, int learn,
            double weight)
        {
            s.Sites.Add(new Site { Id = id, Name = Cap(id), RentWk = rent,
                WageMult = wage, LearningCount = learn, DemandWeight = weight,
                OpenedWk = 2 });
        }

        static string Cap(string id)
        {
            return id.Length > 0 ? char.ToUpperInvariant(id[0]) + id.Substring(1) : id;
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
            // ── 1 · the fields exist, at safe defaults
            var s0 = new GameState();
            ok(s0.Sites.Count == 0 && s0.PriceBook.Count == 0
               && s0.Topics.Count == 0 && s0.SpendBook.Count == 0,
                "divisions: a fresh state carries no sites and an empty price book");

            // ── 2 · no sites → no attention, no directives
            ok(SimDivisions.Attention(s0).Count == 0 && SimDivisions.Directives(s0).Count == 0,
                "divisions: no roofs, no attention, no directives");

            // ── 3 · no sites → the site_rent lane stays zero through a tick
            GameState s1 = St();
            SimEngine.WeeklyTick(s1);
            ok(s1.LastPnl != null && s1.LastPnl.SiteRent == 0,
                "divisions: no roofs, no site rent booked");

            // ── 4 · LIVE: an opened roof bills its rent through site_rent
            GameState s2 = St();
            Book(s2);
            SiteRec(s2, "site_lyon", 2600, 0.92, 140, 0.35);
            SimEngine.WeeklyTick(s2);
            Pnl pnl2 = s2.LastPnl;
            ok(pnl2.SiteRent == 2600,
                "divisions: the opened roof bills its $2,600 rent through the site_rent lane");
            ok(pnl2.Net == pnl2.Revenue - pnl2.Burn - pnl2.LiabilitiesWk
               - pnl2.Interest - pnl2.Tax,
                "divisions: the pnl identity holds with site rent billing");

            // ── 5 · the price book clamps every read + mid-band defaults
            GameState s3 = St();
            ok(Approx(SimDivisions.Pb(s3, "relocation_fee"), 400.0),
                "divisions: a missing price-book key reads its mid-band default");
            s3.PriceBook = new Dictionary<string, object>
                { { "relocation_fee", 999999 }, { "lease_break_weeks", -3 } };
            ok(Approx(SimDivisions.Pb(s3, "relocation_fee"), 1500.0)
               && Approx(SimDivisions.Pb(s3, "lease_break_weeks"), 4.0),
                "divisions: price-book reads are clamped to their bands, high and low");

            // ── 6 · quotes are week-stable and open_site books the preview
            GameState s4 = St();
            Book(s4);
            Dictionary<string, object> q1 = SimDivisions.QuoteSite(s4);
            Dictionary<string, object> q2 = SimDivisions.QuoteSite(s4);
            ok(Convert.ToInt32(q1["rent_wk"], CultureInfo.InvariantCulture)
                   == Convert.ToInt32(q2["rent_wk"], CultureInfo.InvariantCulture)
               && Approx(Convert.ToDouble(q1["wage_mult"], CultureInfo.InvariantCulture),
                   Convert.ToDouble(q2["wage_mult"], CultureInfo.InvariantCulture)),
                "divisions: the open-a-roof quote is stable within a week");
            int cashBefore = s4.Cash;
            Dictionary<string, object> res = SimDivisions.OpenSite(s4, "Lyon");
            ok((bool)res["ok"] && s4.Sites.Count == 1
               && cashBefore - s4.Cash == Convert.ToInt32(res["pack"], CultureInfo.InvariantCulture)
               && s4.Sites[0].RentWk == Convert.ToInt32(q1["rent_wk"], CultureInfo.InvariantCulture),
                "divisions: signing books the quoted pack and rent, to the dollar");
            int pack = Convert.ToInt32(res["pack"], CultureInfo.InvariantCulture);
            int lsum = 0;
            foreach (Dictionary<string, object> l in SimDivisions.PackLines(pack))
                lsum += Convert.ToInt32(l["amount"], CultureInfo.InvariantCulture);
            ok(lsum == pack, "divisions: the pack's receipt lines sum exactly to the pack");

            // ── 7 · the engine is the bouncer
            GameState s5 = St();
            Book(s5);
            s5.Cash = 100;
            ok(!(bool)SimDivisions.OpenSite(s5)["ok"],
                "divisions: a pack cash cannot cover refuses with a reason");

            // ── 8 · a young roof ramps its demand on its own curve
            GameState s6 = St();
            Book(s6);
            SiteRec(s6, "site_a", 1000, 1.0, 0, 0.15);
            double wBefore = s6.Sites[0].DemandWeight;
            SimEngine.WeeklyTick(s6);
            ok(s6.Sites[0].DemandWeight > wBefore,
                "divisions: a new roof's demand weight climbs every week");

            // ── 9 · the book is a GROUP-BY with an honest SHARED row
            GameState s7 = St();
            Book(s7);
            SiteRec(s7, "site_lyon", 2600, 0.92, 140, 0.5);
            s7.Employees.Add(Emp("June Park", "therapist", 1500, 4, "site_lyon"));
            s7.Employees.Add(Emp("Ana Reyes", "therapist", 1200, 3, ""));
            s7.Budgets.Ads = 300;
            List<Dictionary<string, object>> book = SimDivisions.WorksBook(s7, "site");
            Dictionary<string, object> lyon = null, home = null, shared = null;
            foreach (Dictionary<string, object> r in book)
            {
                switch ((string)r["id"])
                {
                    case "site_lyon": lyon = r; break;
                    case "": home = r; break;
                    case "shared": shared = r; break;
                }
            }
            ok(Convert.ToInt32(lyon["payroll_wk"], CultureInfo.InvariantCulture) == 1500
               && Convert.ToInt32(home["payroll_wk"], CultureInfo.InvariantCulture) == 1200
               && Convert.ToInt32(lyon["heads"], CultureInfo.InvariantCulture) == 1
               && Convert.ToInt32(home["heads"], CultureInfo.InvariantCulture) == 1,
                "divisions: payroll and heads group by the roof their records carry");
            int eraRent;
            GameState.ERA_RENT.TryGetValue(s7.Era, out eraRent);
            ok(Convert.ToInt32(shared["rent_wk"], CultureInfo.InvariantCulture) == eraRent
               && Convert.ToInt32(shared["net_wk"], CultureInfo.InvariantCulture)
                   <= -(GameState.RAMEN_PER_WEEK + 300),
                "divisions: SHARED/HQ carries the founder, the era roof and brand spend");

            // ── 10 · rungs are deterministic counts; the slicer lists real axes
            GameState s8 = St();
            ok(SimDivisions.Rung(s8) == 1, "divisions: one offer, one roof — the boutique");
            SimEngine.AddOffer(s8, "the deep 90", "per session", 130.0, 52.0, 2.0, 0.8);
            SimEngine.AddOffer(s8, "house calls", "per session", 110.0, 47.0, 2.0, 0.6);
            ok(SimDivisions.Rung(s8) == 2, "divisions: three offers under one roof — the house");
            Book(s8);
            SiteRec(s8, "site_lyon", 2600, 0.92, 140, 0.5);
            ok(SimDivisions.Rung(s8) == 3 && SimDivisions.DefaultSlice(s8) == "site",
                "divisions: a second roof makes the empire, sliced by site");
            ok(SimDivisions.SliceAxes(s8).Contains("site")
               && SimDivisions.SliceAxes(s8).Contains("offer"),
                "divisions: the slicer lists only axes with two or more divisions");

            // ── 11 · moving a person is brick; tags are ink
            GameState s9 = St();
            Book(s9);
            SiteRec(s9, "site_lyon", 2600, 0.92, 140, 0.5);
            s9.Employees.Add(Emp("June Park", "therapist", 1500, 4, ""));
            int cash9 = s9.Cash;
            Dictionary<string, object> mv = SimDivisions.ReassignEmployee(s9, 0, "site_lyon");
            ok((bool)mv["ok"] && cash9 - s9.Cash == 400
               && s9.Employees[0].Site == "site_lyon"
               && SimDivisions.MarkedUntil(s9, "works_ramp", "June Park") == s9.Week + 1,
                "divisions: a person moves for the relocation fee and a marked ramp week");
            int cash9b = s9.Cash;
            SimDivisions.TagOffer(s9, 0, "spa_line");
            s9.SpendBook.Add(new SpendLine { Name = "staff meals", Buys = "the kitchen fed",
                Amt = 220, Bucket = "office", ContractNotice = 0, Division = "" });
            SimDivisions.TagSpendLine(s9, 0, "site_lyon");
            ok(s9.Cash == cash9b && s9.Offers[0].ProductId == "spa_line"
               && s9.SpendBook[0].Division == "site_lyon",
                "divisions: tags are ink — free, and they stick");

            // ── 12 · the teardown: severance always owed, the lease breaks
            GameState s10 = St();
            Book(s10);
            SiteRec(s10, "site_gen", 1000, 1.1, 20, 0.5);
            s10.Employees.Add(Emp("Ines Rol", "therapist", 1000, 3, "site_gen"));
            Dictionary<string, object> q10 = SimDivisions.CloseQuote(s10, "site_gen",
                new Dictionary<string, string>());
            ok(Convert.ToInt32(q10["net_now"], CultureInfo.InvariantCulture) < 0
               && Convert.ToInt32(q10["freed_wk"], CultureInfo.InvariantCulture) >= 1000 + 1000,
                "divisions: the closing quote prices severance and frees rent plus payroll");
            int cash10 = s10.Cash;
            int sevBefore = s10.SeveranceDue;
            Dictionary<string, object> res10 = SimDivisions.CloseSite(s10, "site_gen",
                new Dictionary<string, string>());
            ok((bool)res10["ok"] && s10.Sites.Count == 0 && s10.SeveranceDue > sevBefore
               && cash10 - s10.Cash == 8 * 1000,
                "divisions: closing books the lease break now; severance accrues and is always owed");

            // ── 13 · Lyon ≠ Geneva, mechanically
            GameState s11 = St();
            Book(s11);
            SiteRec(s11, "site_lyon", 1000, 0.92, 4100, 1.0);
            SiteRec(s11, "site_gen", 3600, 1.15, 90, 1.0);
            List<Dictionary<string, object>> book11 = SimDivisions.WorksBook(s11, "site");
            Dictionary<string, object> lyon11 = null, gen11 = null;
            foreach (Dictionary<string, object> r11 in book11)
            {
                switch ((string)r11["id"])
                {
                    case "site_lyon": lyon11 = r11; break;
                    case "site_gen": gen11 = r11; break;
                }
            }
            ok(N(lyon11, "unit_cost", 99.0) < N(gen11, "unit_cost"),
                "divisions: the dearer roof makes a dearer unit — rent, wages and learning, mechanically");

            // ── 14 · stopping a spend line honours the contract
            GameState s12 = St();
            Book(s12);
            s12.SpendBook.Add(new SpendLine { Name = "the answering service", Buys = "phones",
                Amt = 120, Bucket = "care", ContractNotice = 3, Division = "" });
            s12.SpendBook.Add(new SpendLine { Name = "fresh flowers", Buys = "the room",
                Amt = 40, Bucket = "office", ContractNotice = 0, Division = "" });
            Dictionary<string, object> st = SimDivisions.StopSpendLine(s12, 0);
            ok((bool)st["ok"] && Convert.ToInt32(st["notice_wks"], CultureInfo.InvariantCulture) == 3
               && s12.Commitments.Count == 1 && s12.Commitments[0].CashWk == -120,
                "divisions: a contract line's notice bills through as a standing commitment");
            Dictionary<string, object> st2 = SimDivisions.StopSpendLine(s12, 0);
            ok((bool)st2["ok"]
               && Convert.ToInt32(st2["notice_wks"], CultureInfo.InvariantCulture) == 0
               && s12.Commitments.Count == 1,
                "divisions: a non-contract line stops instantly, nothing lingers");
        }
    }
}
