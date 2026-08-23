using System;
using System.Collections.Generic;
using System.Globalization;
using Runway.Core;

namespace Runway.CoreTests
{
    /// <summary>
    /// THE ECONOMY PROBES — the revenue law (#196) driven for 50 weeks through the
    /// REAL engine, once per trade shape WorldGen ships, at a spread of price ratios.
    ///
    /// This is a READ-ONLY instrument: it prints tables and verdicts, it never
    /// asserts, so the contract suite's check count stays exactly what the GDScript
    /// twin runs. Balance is arbitrated by a human off these numbers.
    ///
    /// The scripted founder is deliberately plain: builds +2 product a week,
    /// launches in week 5, spends $500/wk on marketing while there is cash, hires
    /// nobody, raises nothing. Whatever the table shows is the OFFER's doing.
    /// </summary>
    public static class EconomyProbe
    {
        private const int WEEKS = 50;
        private const int START_CASH = 25000;
        private const int MARKETING_WK = 500;
        private const long SEED = 1234;

        private sealed class Shape
        {
            public string Label;
            public string What;
            public string Who;
            public Shape(string label, string what, string who)
            {
                Label = label; What = what; Who = who;
            }
        }

        /// <summary>Everything a run is allowed to bend, so a counterfactual is one field.</summary>
        private sealed class Knobs
        {
            public bool EraLadder = true;
            public int Marketing = MARKETING_WK;
            public int StartTraction;
            public string RelabelUnitOf;   // offer name whose unit is swapped (cadence counterfactual)
            public string RelabelUnitTo;
            public double PriceScale = 1.0; // scales fair_price AND unit_cost together (margin held)
            public string CapWho;           // BizWho swapped AFTER worldgen: isolates the GTM cap_scale
            public double TamScale = 1.0;   // blows the market up so sub-unit weekly adds become readable
        }

        private sealed class Result
        {
            public string Shape;
            public string Who;
            public double Ratio;
            public int Customers;
            public double ArpuCust;
            public int Revenue;
            public int Cogs;
            public int Burn;
            public int EndCash;
            public int MinCash;
            public int MaxDelta;
            public int MaxDeltaGarage;
            public string Era;
            public int DiedAt;
            public double GrossMargin;
            public int NetWk;
            public double AvgAdds;
            public int CapBoundWeeks;      // weeks the GTM clamp, not demand, set the adds
            public string EraWeeks = "";   // when the ladder promoted, and into what rent
        }

        private static readonly Shape[] SHAPES =
        {
            new Shape("Service", "Service", "SMB"),
            new Shape("SaaS", "Software", "SMB"),
            new Shape("Hardware", "Hardware", "Consumer"),
            new Shape("Market", "Marketplace", "Consumer"),
        };

        private static readonly double[] RATIOS = { 0.6, 1.0, 1.5, 3.0 };

        public static void Run()
        {
            Console.WriteLine("═══ ECONOMY PROBE — the revenue law over 50 weeks ═══");
            Console.WriteLine("scripted founder: product +2/wk, launch wk5, $" + MARKETING_WK
                + "/wk marketing while cash > $5k, no hires, no raise. Seed " + SEED + ".");
            Console.WriteLine();

            StaticOfferTable();
            Console.WriteLine();

            Console.WriteLine("PANEL A — 50 weeks, era ladder ON (rent scales as the shipped loop scales it)");
            List<Result> a = Sweep(SHAPES, RATIOS, new Knobs { EraLadder = true });
            PrintTable(a);
            Console.WriteLine();

            Console.WriteLine("PANEL B — same runs, era FROZEN in the garage ($150/wk rent for 50 weeks:");
            Console.WriteLine("          the cheapest possible world, so a money printer has nowhere to hide)");
            List<Result> b = Sweep(SHAPES, RATIOS, new Knobs { EraLadder = false });
            PrintTable(b);
            Console.WriteLine();

            Console.WriteLine("PANEL C — fair price only, swept across who the buyer is");
            Shape[] whoShapes = WhoSweep();
            List<Result> c = Sweep(whoShapes, new[] { 1.0 }, new Knobs { EraLadder = true });
            PrintTable(c);
            Console.WriteLine("          ... and the same twelve with the era ladder frozen, to separate");
            Console.WriteLine("          'died of the price' from 'died of the rent it was promoted into':");
            List<Result> cFrozen = Sweep(whoShapes, new[] { 1.0 }, new Knobs { EraLadder = false });
            PrintTable(cFrozen);
            Console.WriteLine();

            PriceCurve();
            Console.WriteLine();

            CadenceCounterfactual();
            Console.WriteLine();

            ColdStart();
            Console.WriteLine();

            PrinterLevers();
            Console.WriteLine();

            LadderTrap();
            Console.WriteLine();

            DashboardDrift();
            Console.WriteLine();

            ParityDrift();
            Console.WriteLine();

            Console.WriteLine("═══ VERDICTS against founder sense ═══");
            Judge("PANEL A (era ladder on)", a);
            Judge("PANEL B (garage frozen)", b);
            Console.WriteLine();
            Console.WriteLine("ECONOMY PROBE DONE");
        }

        private static Shape[] WhoSweep()
        {
            var list = new List<Shape>();
            foreach (Shape s in SHAPES)
            {
                foreach (string who in new[] { "Consumer", "SMB", "Enterprise" })
                {
                    list.Add(new Shape(s.Label, s.What, who));
                }
            }
            return list.ToArray();
        }

        // ── the law with no simulation at all: what one customer is worth per week ──
        private static void StaticOfferTable()
        {
            Console.WriteLine("PANEL D — the law itself, per shipped offer at FAIR price (no simulation)");
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-9} {1,-17} {2,-15} {3,6} {4,8} {5,8} {6,10} {7,10} {8,6} {9,9}",
                "shape", "offer", "unit", "cad", "fair$", "cost$", "rev/cus/wk", "cog/cus/wk", "gm%", "loss<"));
            foreach (Shape sh in SHAPES)
            {
                GameState s = Build(sh, 1.0, new Knobs());
                double arpu = SimEngine.OffersArpu(s);
                double cogs = SimEngine.OffersCogsPerCustomer(s);
                foreach (Offer o in s.Offers)
                {
                    double cad = SimEngine.OfferCadence(o.Unit);
                    double rev = o.Weight * o.Price * cad;
                    double cog = o.Weight * o.UnitCost * cad;
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0,-9} {1,-17} {2,-15} {3,6} {4,8} {5,8} {6,10} {7,10} {8,6} {9,9}",
                        sh.Label, Trunc(o.Name, 17), Trunc(o.Unit, 15), F(cad, 2),
                        F(o.FairPrice, 0), F(o.UnitCost, 0), F(rev, 2), F(cog, 2),
                        rev > 0.0 ? F((rev - cog) / rev * 100.0, 0) : "-",
                        F(o.UnitCost / Gd.Maxf(o.FairPrice, 0.01), 2) + "x"));
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0,-9} {1,-17} {2,-15} {3,6} {4,8} {5,8} {6,10} {7,10} {8,6} {9,9}",
                    "", "  = per customer", "", "", "", "", F(arpu, 2), F(cogs, 2),
                    arpu > 0.0 ? F((arpu - cogs) / arpu * 100.0, 0) : "-",
                    F(cogs / Gd.Maxf(arpu, 0.01), 2) + "x"));
            }
            Console.WriteLine("          loss< = the price ratio below which cogs eats the whole invoice.");
        }

        // ── the shape of the price decision: is fair ever the right answer? ──
        private static void PriceCurve()
        {
            Console.WriteLine("PANEL E — the price curve: end cash after 50 weeks at every ratio (era ladder ON).");
            Console.WriteLine("          If the peak is not at 1.0x, fair price is not a decision — it is a mistake.");
            var ratios = new List<double>();
            for (double r = 0.3; r <= 2.01; r += 0.1)
            {
                ratios.Add(Math.Round(r, 2));
            }
            foreach (Shape sh in SHAPES)
            {
                double bestCashRatio = 0.0;
                int bestCash = int.MinValue;
                double bestCustRatio = 0.0;
                int bestCust = -1;
                var cells = new List<string>();
                foreach (double r in ratios)
                {
                    Result res = RunOne(sh, r, new Knobs { EraLadder = true });
                    if (res.EndCash > bestCash) { bestCash = res.EndCash; bestCashRatio = r; }
                    if (res.Customers > bestCust) { bestCust = res.Customers; bestCustRatio = r; }
                    cells.Add(F(r, 1) + "x=" + Money(res.EndCash));
                }
                Console.WriteLine(sh.Label + " (" + sh.Who + "): best cash @ " + F(bestCashRatio, 1)
                    + "x ($" + bestCash + ")  ·  most customers @ " + F(bestCustRatio, 1)
                    + "x (" + bestCust + ")");
                for (int i = 0; i < cells.Count; i += 6)
                {
                    var line = new List<string>();
                    for (int j = i; j < Gd.Mini(i + 6, cells.Count); j++)
                    {
                        line.Add(cells[j].PadRight(14));
                    }
                    Console.WriteLine("    " + string.Join("", line));
                }
            }
        }

        // ── the cadence lever, measured without touching the engine: relabel the unit ──
        private static void CadenceCounterfactual()
        {
            Console.WriteLine("PANEL F — cadence counterfactual. OfferCadence() reads the unit STRING, so");
            Console.WriteLine("          relabelling a durable good measures what a slower cadence would do.");
            var hw = new Shape("Hardware", "Hardware", "Consumer");
            var svc = new Shape("Service", "Service", "Consumer");
            var rows = new List<Result>();
            rows.Add(Tag(RunOne(hw, 1.0, new Knobs()), "unit(0.20) SHIPPED"));
            rows.Add(Tag(RunOne(hw, 1.0, new Knobs { RelabelUnitOf = "the device", RelabelUnitTo = "per month" }),
                "device->month(.25)"));
            rows.Add(Tag(RunOne(hw, 1.0, new Knobs { RelabelUnitOf = "the device", RelabelUnitTo = "per year" }),
                "device->year(.02)"));
            rows.Add(Tag(RunOne(svc, 1.0, new Knobs()), "session(1.0) SHIPPED"));
            rows.Add(Tag(RunOne(svc, 1.0, new Knobs { RelabelUnitOf = "standard session", RelabelUnitTo = "per month" }),
                "session->month(.25)"));
            PrintTable(rows);
        }

        private static Result Tag(Result r, string label)
        {
            r.Who = label;
            return r;
        }

        // ── can a slow market ever land its first customer? ──
        private static void ColdStart()
        {
            Console.WriteLine("PANEL G — cold start. Traction is an int and net adds are ROUNDED, so a market");
            Console.WriteLine("          that yields under half a customer a week yields none of them, forever.");
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-9} {1,-11} {2,9} {3,10} {4,7} {5,9} {6,9}",
                "shape", "who", "marketing", "startCust", "endCus", "avgAdds", "endCash"));
            foreach (Shape sh in SHAPES)
            {
                foreach (string who in new[] { "Enterprise", "SMB" })
                {
                    foreach (int mk in new[] { 500, 6000 })
                    {
                        foreach (int t0 in new[] { 0, 5 })
                        {
                            if (who == "SMB" && (mk != 500 || t0 != 0))
                            {
                                continue;   // SMB is the control: one row is enough
                            }
                            var probe = new Shape(sh.Label, sh.What, who);
                            Result r = RunOne(probe, 1.0,
                                new Knobs { EraLadder = true, Marketing = mk, StartTraction = t0 });
                            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                "{0,-9} {1,-11} {2,9} {3,10} {4,7} {5,9} {6,9}",
                                sh.Label, who, mk, t0, r.Customers, F(r.AvgAdds, 2), r.EndCash));
                        }
                    }
                }
            }
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("          The same Enterprise worlds with the market blown up, so the sub-unit");
            Console.WriteLine("          weekly adds the rounding throws away become readable. Adds scale with");
            Console.WriteLine("          the untapped market, so avgAdds / scale is the real per-week rate.");
            Console.WriteLine("          Enterprise GTM cap is (1.5 + 0.8*sell3 + 500/400) * 1.0 = 5.15/wk, so");
            Console.WriteLine("          only rows well under 5.15 are demand-bound and therefore honest.");
            foreach (double scale in new[] { 4.0, 10.0, 100.0 })
            {
                foreach (Shape sh in SHAPES)
                {
                    var probe = new Shape(sh.Label, sh.What, "Enterprise");
                    Result r = RunOne(probe, 1.0, new Knobs { EraLadder = true, TamScale = scale });
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0,-9} {1,-11} TAM x{2,-5} {3,10} {4,7} {5,9} {6,9}   real adds/wk ~ {7}{8}",
                        sh.Label, "Enterprise", F(scale, 0), 0, r.Customers, F(r.AvgAdds, 2), r.EndCash,
                        F(r.AvgAdds / scale, 3), r.CapBoundWeeks > 5 ? "  (CAP-BOUND, ignore)" : ""));
                }
            }
        }

        // ── which constant actually prints the money? two candidates, measured ──
        private static void PrinterLevers()
        {
            Console.WriteLine("PANEL H — the printer's two candidate constants, each moved ALONE at fair price.");
            Console.WriteLine("          (i) the GTM cap_scale: Consumer theta and Consumer offers kept, only");
            Console.WriteLine("              the closing-capacity multiplier swapped (Consumer 40 / SMB 3 / other 1).");
            Console.WriteLine("          (ii) who-scaled offer prices: fair_price AND unit_cost scaled together,");
            Console.WriteLine("               the pattern default_offers already uses for Software but not for");
            Console.WriteLine("               Service / Hardware / Marketplace.");
            var rows = new List<Result>();
            foreach (Shape sh in new[]
            {
                new Shape("Service", "Service", "Consumer"),
                new Shape("Hardware", "Hardware", "Consumer"),
                new Shape("Market", "Marketplace", "Consumer"),
            })
            {
                rows.Add(Tag(RunOne(sh, 1.0, new Knobs()), "SHIPPED (cap 40)"));
                rows.Add(Tag(RunOne(sh, 1.0, new Knobs { CapWho = "SMB" }), "cap_scale 40->3"));
                rows.Add(Tag(RunOne(sh, 1.0, new Knobs { CapWho = "Enterprise" }), "cap_scale 40->1"));
                rows.Add(Tag(RunOne(sh, 1.0, new Knobs { PriceScale = 0.25 }), "price+cost x0.25"));
                rows.Add(Tag(RunOne(sh, 1.0, new Knobs { PriceScale = 0.25, CapWho = "SMB" }), "both"));
            }
            PrintTable(rows);
        }

        // ── the promotion that bankrupts a healthy company ──
        private static void LadderTrap()
        {
            Console.WriteLine("PANEL I — the era ladder's promotions, with the rent it moved into and the");
            Console.WriteLine("          revenue that week. coworking->office is the only 5x rent jump with no");
            Console.WriteLine("          cash-cushion gate (office->floor has one, and says why in a comment).");
            foreach (Shape sh in new[]
            {
                new Shape("Market", "Marketplace", "SMB"),
                new Shape("SaaS", "Software", "SMB"),
                new Shape("Service", "Service", "SMB"),
            })
            {
                foreach (double ratio in new[] { 1.0, 1.5 })
                {
                    Result on = RunOne(sh, ratio, new Knobs { EraLadder = true });
                    Result off = RunOne(sh, ratio, new Knobs { EraLadder = false });
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0,-9} {1,-4} ladder ON: {2,9} {3,-9} | frozen: {4,9} {5,-7} | {6}",
                        sh.Label, F(ratio, 1) + "x", on.EndCash,
                        on.DiedAt > 0 ? "DEAD@" + on.DiedAt : "alive", off.EndCash,
                        off.DiedAt > 0 ? "DEAD@" + off.DiedAt : "alive", on.EraWeeks));
                }
            }
        }

        // ── the ledger books offers; three other readouts still book theta ──
        private static void DashboardDrift()
        {
            Console.WriteLine("PANEL J — what the LEDGER books vs what the READOUTS say. Revenue moved to");
            Console.WriteLine("          offers in #196, but unit_econ arpu/ltv/payback, valuation() and");
            Console.WriteLine("          runway_weeks() still compute theta_arpu * price_mult. Same drift in");
            Console.WriteLine("          both engines, so this is not a parity break — it is a shared one.");
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-9} {1,-11} {2,10} {3,10} {4,7} {5,11} {6,11} {7,7} {8,9} {9,9}",
                "shape", "who", "realArpu", "shownArpu", "off-by", "realRev/wk", "shownRev/wk",
                "cust", "shownRnwy", "realRnwy"));
            foreach (Shape sh in SHAPES)
            {
                GameState s = Build(sh, 1.0, new Knobs());
                WeeklyReport rep = null;
                for (int w = 1; w <= WEEKS; w++)
                {
                    s.Week = w;
                    if (w == 5) { s.SetFlag("launched"); }
                    s.Product = Gd.Mini(s.Product + 2, 100);
                    s.MarketingBudget = s.Cash > 5000 ? MARKETING_WK : 0;
                    rep = SimEngine.WeeklyTick(s);
                    s.AdvanceEraIfReady();
                }
                double realArpu = SimEngine.OffersArpu(s);
                double shownArpu = s.Theta.ArpuWk * s.PriceMult;
                double shownRev = s.Traction * shownArpu;
                int shownRunway = SimEngine.RunwayWeeks(s);
                int netWk = rep.Revenue - rep.Burn;
                string realRunway = netWk >= 0 ? "inf" : ((int)Math.Floor(s.Cash / (double)(-netWk))).ToString(
                    CultureInfo.InvariantCulture);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0,-9} {1,-11} {2,10} {3,10} {4,7} {5,11} {6,11} {7,7} {8,9} {9,9}",
                    sh.Label, sh.Who, F(realArpu, 2), F(shownArpu, 2),
                    F(realArpu / Gd.Maxf(shownArpu, 0.01), 1) + "x", rep.Revenue,
                    Gd.RoundToInt(shownRev), s.Traction, shownRunway, realRunway));
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "          ... the binder's panel says LTV ${0}, payback {1} wks, valuation ${2}",
                    rep.Ltv, rep.PaybackWk, SimEngine.Valuation(s)));
            }
        }

        // ── the one place the two engines can disagree ──
        private static void ParityDrift()
        {
            Console.WriteLine("PANEL K — the offer law's only semantic gap between the engines. An offer");
            Console.WriteLine("          carrying a price but NO fair_price key (a legacy or hand-written save):");
            var s = new GameState();
            s.Offers = new List<Offer>
            {
                new Offer { Name = "orphan", Unit = "per session", Price = 70.0, Weight = 1.0 },
                // FairPrice deliberately left at its declared default
            };
            Console.WriteLine("          C#      OffersPricePain -> " + F(SimEngine.OffersPricePain(s), 2)
                + "   (Offer.FairPrice defaults to 1.0, so the ratio reads 70.0)");
            Console.WriteLine("          GDScript offers_price_pain -> 1.00   (fair_price defaults to the");
            Console.WriteLine("                   offer's OWN price: `od.get(\"fair_price\", price)`, ratio 1.0)");
            Console.WriteLine("          Same input, maximum divergence: capped pain vs no pain at all.");
            Console.WriteLine("          Unreachable through WorldGen (it always writes fair_price) and through");
            Console.WriteLine("          the binder (it only writes price) — a save-file / LLM-patch hazard only.");
        }

        // ───────────────────────────── the machinery ─────────────────────────────
        private static List<Result> Sweep(Shape[] shapes, double[] ratios, Knobs k)
        {
            var outp = new List<Result>();
            foreach (Shape sh in shapes)
            {
                foreach (double ratio in ratios)
                {
                    outp.Add(RunOne(sh, ratio, k));
                }
            }
            return outp;
        }

        /// <summary>The world exactly as WorldGen ships it for this seed, priced at ratio x fair.</summary>
        private static GameState Build(Shape sh, double ratio, Knobs k)
        {
            var s = new GameState();
            s.SimSeed = SEED;
            s.Cash = START_CASH;
            s.Traction = k.StartTraction;
            s.Product = 20;
            s.Era = "garage";
            s.BizWhat = sh.What;
            s.BizWho = sh.Who;
            s.Theta = SimEngine.DefaultTheta(sh.What, sh.Who);
            if (k.TamScale != 1.0)
            {
                s.Theta.Tam = Gd.Clampf(s.Theta.Tam * k.TamScale, 2000.0, 5000000.0);
            }
            WorldGen.Build(s);                       // investors, rivals AND the shipped offers
            foreach (Offer o in s.Offers)
            {
                o.FairPrice = Math.Round(o.FairPrice * k.PriceScale, 2);
                o.UnitCost = Math.Round(o.UnitCost * k.PriceScale, 2);
                o.Price = Math.Round(o.FairPrice * ratio, 2);
                if (!string.IsNullOrEmpty(k.RelabelUnitOf) && o.Name == k.RelabelUnitOf)
                {
                    o.Unit = k.RelabelUnitTo;
                }
            }
            if (!string.IsNullOrEmpty(k.CapWho))
            {
                s.BizWho = k.CapWho;   // theta and offers stay Consumer's; only the GTM clamp moves
            }
            return s;
        }

        private static Result RunOne(Shape sh, double ratio, Knobs k)
        {
            GameState s = Build(sh, ratio, k);
            double capScale = s.BizWho == "SMB" ? 3.0 : (s.BizWho == "Consumer" ? 40.0 : 1.0);
            var r = new Result
            {
                Shape = sh.Label, Who = sh.Who, Ratio = ratio,
                MinCash = s.Cash, MaxDelta = 0, MaxDeltaGarage = 0, DiedAt = 0,
            };
            WeeklyReport rep = null;
            double addsTotal = 0.0;
            for (int w = 1; w <= WEEKS; w++)
            {
                s.Week = w;
                if (w == 5)
                {
                    s.SetFlag("launched");
                }
                s.Product = Gd.Mini(s.Product + 2, 100);
                s.MarketingBudget = s.Cash > 5000 ? k.Marketing : 0;

                // the GTM clamp as the engine computes it, so we can tell a demand-bound
                // week (the market said no) from a cap-bound one (we could not close it)
                double gtmCap = (1.5 + 0.8 * s.Competence("sell") + s.MarketingBudget / 400.0) * capScale;

                int before = s.Cash;
                string eraBefore = s.Era;
                rep = SimEngine.WeeklyTick(s);
                if (k.EraLadder)
                {
                    GameState.EraMove up = s.AdvanceEraIfReady();
                    if (up.Changed)
                    {
                        int newRent;
                        GameState.ERA_RENT.TryGetValue(s.Era, out newRent);
                        r.EraWeeks += (r.EraWeeks.Length > 0 ? " " : "") + s.Era + "@w" + w
                            + "(rent " + newRent + ", rev " + rep.Revenue + ")";
                    }
                }
                if (rep.Adds == Gd.RoundToInt(gtmCap) && rep.Adds > 0)
                {
                    r.CapBoundWeeks += 1;
                }
                addsTotal += rep.Adds;
                int delta = s.Cash - before;
                if (delta > r.MaxDelta) { r.MaxDelta = delta; }
                if (eraBefore == "garage" && delta > r.MaxDeltaGarage) { r.MaxDeltaGarage = delta; }
                if (s.Cash < r.MinCash) { r.MinCash = s.Cash; }
                if (s.Cash < -5000 && r.DiedAt == 0) { r.DiedAt = w; }
            }
            r.Customers = s.Traction;
            r.ArpuCust = SimEngine.OffersArpu(s);
            r.Revenue = rep.Revenue;
            r.Cogs = Gd.RoundToInt(s.Traction * SimEngine.OffersCogsPerCustomer(s));
            r.Burn = rep.Burn;
            r.EndCash = s.Cash;
            r.Era = s.Era;
            r.GrossMargin = r.Revenue > 0 ? (double)(r.Revenue - r.Cogs) / r.Revenue : 0.0;
            r.NetWk = r.Revenue - r.Burn;
            r.AvgAdds = addsTotal / WEEKS;
            return r;
        }

        private static void PrintTable(List<Result> rows)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-9} {1,-19} {2,5} {3,6} {4,8} {5,9} {6,8} {7,5} {8,9} {9,10} {10,9} {11,4} {12,-10} {13,-8}",
                "shape", "who", "ratio", "cust", "$/cust", "rev/wk", "cogs/wk", "gm%",
                "net/wk", "endCash", "maxD/wk", "cap", "era", "state"));
            foreach (Result r in rows)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0,-9} {1,-19} {2,5} {3,6} {4,8} {5,9} {6,8} {7,5} {8,9} {9,10} {10,9} {11,4} {12,-10} {13,-8}",
                    r.Shape, Trunc(r.Who, 19), F(r.Ratio, 1) + "x", r.Customers, F(r.ArpuCust, 2),
                    r.Revenue, r.Cogs, F(r.GrossMargin * 100.0, 0), r.NetWk, r.EndCash,
                    r.MaxDelta, r.CapBoundWeeks, r.Era, r.DiedAt > 0 ? "DEAD@" + r.DiedAt : "alive"));
            }
            Console.WriteLine("          cap = weeks the GTM clamp (not the market) set the week's adds.");
        }

        // ───────────────────────────── the judgement ─────────────────────────────
        private static void Judge(string panel, List<Result> rows)
        {
            Console.WriteLine();
            Console.WriteLine("-- " + panel + " --");
            var byShape = new Dictionary<string, Dictionary<double, Result>>();
            var order = new List<string>();
            foreach (Result r in rows)
            {
                if (!byShape.ContainsKey(r.Shape))
                {
                    byShape[r.Shape] = new Dictionary<double, Result>();
                    order.Add(r.Shape);
                }
                byShape[r.Shape][r.Ratio] = r;
            }
            foreach (string shape in order)
            {
                Dictionary<double, Result> m = byShape[shape];
                if (!m.ContainsKey(1.0))
                {
                    continue;
                }
                Result fair = m[1.0];
                var notes = new List<string>();

                if (fair.MaxDeltaGarage > 50000)
                {
                    notes.Add("(a) PRINTER: +$" + fair.MaxDeltaGarage + "/wk peak while still in the garage");
                }
                else if (fair.MaxDelta > 50000)
                {
                    notes.Add("(a) late printer: +$" + fair.MaxDelta + "/wk peak (post-garage)");
                }
                else
                {
                    notes.Add("(a) no printer: peak +$" + fair.MaxDelta + "/wk — holds");
                }

                if (fair.Revenue > 0 && fair.Cogs > fair.Revenue)
                {
                    notes.Add("COGS > REVENUE at fair price ($" + fair.Cogs + " vs $" + fair.Revenue + ")");
                }
                else
                {
                    notes.Add("cogs sanity: $" + fair.Cogs + " cogs on $" + fair.Revenue
                        + " revenue at fair — holds");
                }

                if (m.ContainsKey(1.5))
                {
                    Result up = m[1.5];
                    if (up.EndCash > fair.EndCash && up.Customers >= fair.Customers)
                    {
                        notes.Add("(b) 1.5x STRICTLY DOMINATES fair (cash $" + up.EndCash + " > $" + fair.EndCash
                            + " AND customers " + up.Customers + " >= " + fair.Customers + ")");
                    }
                    else if (up.EndCash > fair.EndCash)
                    {
                        notes.Add("(b) 1.5x wins cash ($" + up.EndCash + " vs $" + fair.EndCash + ") and pays "
                            + (fair.Customers - up.Customers) + " customers for it — a real tradeoff");
                    }
                    else
                    {
                        notes.Add("(b) 1.5x is DOMINATED, not a tradeoff: cash $" + up.EndCash + " vs $"
                            + fair.EndCash + " AND customers " + up.Customers + " vs " + fair.Customers
                            + (up.DiedAt > 0 ? " (DEAD@" + up.DiedAt + ")" : ""));
                    }
                }

                if (m.ContainsKey(3.0))
                {
                    Result greed = m[3.0];
                    if (greed.EndCash >= fair.EndCash)
                    {
                        notes.Add("(c) 3x DOES NOT LOSE: cash $" + greed.EndCash + " >= fair $" + fair.EndCash);
                    }
                    else
                    {
                        notes.Add("(c) 3x loses ($" + greed.EndCash + " vs $" + fair.EndCash + ", "
                            + greed.Customers + " vs " + fair.Customers + " customers) — holds");
                    }
                }

                if (m.ContainsKey(0.6))
                {
                    Result cheap = m[0.6];
                    notes.Add("(d) 0.6x margin " + F(cheap.GrossMargin * 100.0, 0) + "% vs fair "
                        + F(fair.GrossMargin * 100.0, 0) + "% — margin strains"
                        + (cheap.EndCash > fair.EndCash
                            ? ", BUT cash $" + cheap.EndCash + " > fair $" + fair.EndCash + ": UNDERCUT DOMINATES"
                            : ", and cash $" + cheap.EndCash + " < fair $" + fair.EndCash + " — holds"));
                }

                Console.WriteLine(shape + ":");
                foreach (string n in notes)
                {
                    Console.WriteLine("    " + n);
                }
            }
        }

        private static string Money(int v)
        {
            if (Math.Abs(v) >= 1000000) { return F(v / 1000000.0, 2) + "M"; }
            if (Math.Abs(v) >= 1000) { return F(v / 1000.0, 1) + "k"; }
            return v.ToString(CultureInfo.InvariantCulture);
        }

        private static string F(double v, int dp)
        {
            return v.ToString("F" + dp.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        private static string Trunc(string s, int n)
        {
            s = s ?? "";
            return s.Length <= n ? s : s.Substring(0, n);
        }
    }
}
