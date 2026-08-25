using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Runway.Core;

namespace Runway.CoreTests
{
    /// <summary>
    /// LANE SUITE — street. Spec: docs/design/03-rivals-macro.md section 13 (the twin pins).
    ///
    /// Program.cs calls Run after the engine's own checks and hands over `ok`,
    /// the same assert the whole suite uses: ok(condition, "what it pins").
    ///
    /// The porting law: a check lands FIRST in game/tests/lanes/test_street.gd,
    /// then here in the same order. Same checks, same order, same logic — the two
    /// engines do not share PRNG internals, so nothing here pins a draw across
    /// them, only behaviour. Where a pin needs a particular die, it SEARCHES the
    /// seeds for one instead of hardcoding it.
    /// </summary>
    public static class StreetTests
    {
        private static Rival RivalA()
        {
            return new Rival
            {
                Name = "Vantage", Strength = 48.0,
                Tactics = new List<string> { "undercut pricing" },
                WeeksSinceMove = 0, Secret = "", Vigor = 60.0, Hype = 25.0,
                Focus = "price", PricePosture = 1.0, LastAction = "",
                Log = new List<string>(), Cooldowns = new Dictionary<string, int>(), Sniffing = 0,
            };
        }

        private static Rival RivalB()
        {
            return new Rival
            {
                Name = "Northgate", Strength = 33.0,
                Tactics = new List<string> { "shipped a clone feature" },
                WeeksSinceMove = 0, Secret = "quietly running out of money",
                Vigor = 45.0, Hype = 18.0, Focus = "product", PricePosture = 1.0,
                LastAction = "", Log = new List<string>(),
                Cooldowns = new Dictionary<string, int>(), Sniffing = 0,
            };
        }

        private static GameState St(long seed, string era)
        {
            var s = new GameState();
            s.SimSeed = seed;
            s.Week = 5;
            s.Era = era;
            s.Cash = 80000;
            s.Traction = 120;
            s.Product = 45;
            s.Morale = 70;
            s.Hype = 35;
            s.BizWhat = "Software";
            s.BizWho = "SMB";
            s.Theta = SimEngine.DefaultTheta(s.BizWhat, s.BizWho);
            s.Offers = new List<Offer> { new Offer { Name = "the plan", Unit = "per month",
                Price = 40.0, FairPrice = 40.0, Elasticity = 2.2, UnitCost = 9.0, Weight = 1.0 } };
            s.SetFlag("launched");
            s.Rivals = new List<Rival> { RivalA(), RivalB() };
            return s;
        }

        public static void Run(Action<bool, string> ok)
        {
            PinDeterminism(ok);
            PinEraGate(ok);
            PinCooldownLaw(ok);
            PinPoach(ok);
            PinPriceWar(ok);
            PinMacro(ok);
        }

        // ── 1 · DETERMINISM ──────────────────────────────────────────────────
        /// <summary>The whole lane is dice, and dice the player cannot replay are not
        /// fair. Two identical states, ten weeks each, must land on the same street.</summary>
        private static void PinDeterminism(Action<bool, string> ok)
        {
            GameState a = St(42, "office");
            GameState b = St(42, "office");
            for (int i = 0; i < 10; i++)
            {
                SimEngine.WeeklyTick(a);
                SimEngine.WeeklyTick(b);
                a.Week += 1;
                b.Week += 1;
            }
            ok(JsonConvert.SerializeObject(a.Rivals) == JsonConvert.SerializeObject(b.Rivals),
                "ten weeks of rivals replay identically from one seed");
            ok(Math.Abs(a.MarketTrend - b.MarketTrend) < 1e-12,
                "the mean-reverting trend replays identically");
            ok(JsonConvert.SerializeObject(a.Statuses) == JsonConvert.SerializeObject(b.Statuses),
                "the statuses the street installed replay identically");
        }

        // ── 2 · THE ERA GATE ─────────────────────────────────────────────────
        /// <summary>A garage is beneath notice. Rivals still live their lives — that is
        /// the lesson — but nothing they do lands on a company nobody has heard of.</summary>
        private static void PinEraGate(Action<bool, string> ok)
        {
            GameState g = St(7, "garage");
            g.Employees = new List<Employee> { new Employee { Name = "Mara Voss",
                Role = "engineer", Salary = 900, Burnout = 10 } };
            string before = JsonConvert.SerializeObject(g.Rivals);
            bool touched = false;
            for (int i = 0; i < 30; i++)
            {
                SimEngine.WeeklyTick(g);
                foreach (Status s in g.Statuses)
                {
                    if (s.Name == "price_war" || s.Name == "outshipped"
                        || s.Name == "rival_fud" || s.Name == "rival_stumbled")
                    {
                        touched = true;
                    }
                }
                g.Week += 1;
            }
            ok(!touched, "a garage never eats a rival status: nobody is answering you yet");
            // the roster itself is the labor lane's to move (resignations, reviews);
            // what this pins is that NO POACH ever rang — the phone-call meta is
            // only ever written here, so its absence is the honest headcount claim
            ok((int)g.GetMetaF("poach_wk", -1.0) == -1,
                "nobody poaches from a company the street cannot see");
            ok(JsonConvert.SerializeObject(g.Rivals) != before,
                "the street lives without you: rivals still move at the garage");
        }

        // ── 3 · THE COOLDOWN LAW ─────────────────────────────────────────────
        /// <summary>Competitive response has a lag. Two hundred worlds, half a year
        /// each: no rival ever repeats a move inside its own response time, and
        /// the street stays mostly quiet — conduct is punctuation, not the sentence.</summary>
        private static void PinCooldownLaw(Action<bool, string> ok)
        {
            int violations = 0;
            int fires = 0;
            int quiets = 0;
            for (int s = 0; s < 200; s++)
            {
                GameState st = St(1000 + s, "office");
                var last = new Dictionary<string, int>();
                for (int w = 0; w < 26; w++)
                {
                    SimEngine.WeeklyTick(st);
                    for (int i = 0; i < st.Rivals.Count; i++)
                    {
                        Rival rd = st.Rivals[i];
                        string act = rd.LastAction ?? "";
                        if (act.Length == 0) { continue; }
                        if (act == "quiet") { quiets++; continue; }
                        fires++;
                        string key = i.ToString(CultureInfo.InvariantCulture) + "|" + act;
                        int cd;
                        if (!SimStreet.COOLDOWNS.TryGetValue(act, out cd)) { cd = 0; }
                        if (act == "price_cut" && rd.Focus == "price") { cd = 3; }
                        int seen;
                        if (last.TryGetValue(key, out seen) && st.Week - seen < cd) { violations++; }
                        last[key] = st.Week;
                    }
                    st.Week += 1;
                }
            }
            ok(violations == 0, string.Format(CultureInfo.InvariantCulture,
                "no rival repeats a move inside its cooldown ({0} breaches)", violations));
            double quietShare = quiets / Gd.Maxf(quiets + fires, 1.0);
            ok(quietShare >= 0.20 && quietShare <= 0.70, string.Format(CultureInfo.InvariantCulture,
                "the street is mostly quiet but never asleep (quiet share {0:0.00})", quietShare));
        }

        // ── 4 · THE POACH ────────────────────────────────────────────────────
        /// <summary>Pay-gap arbitrage, priced exactly. The target comes from the labor
        /// lane's interface; the suite hands over a stubbed one so the resolution
        /// can be pinned before that desk exists.</summary>
        private static void PinPoach(Action<bool, string> ok)
        {
            ok(Math.Abs(SimStreet.PoachOdds(0.6, 80.0) - 0.70) < 1e-9,
                "a 60% pay gap and a full war chest still caps at 0.70 — money does not always win");
            ok(Math.Abs(SimStreet.PoachOdds(0.15, 50.0) - 0.15) < 1e-9,
                "the curve is anchored at a 15% gap on an average war chest");
            ok(Math.Abs(SimStreet.PoachOdds(0.40, 80.0) - 0.54) < 1e-9,
                "a 40% gap with money behind it is better than a coin flip");
            // {salary 900, market 2250} -> pay_gap 0.6, exactly as the labor query reports it
            var target = new Dictionary<string, object> { { "index", 0 }, { "name", "Mara Voss" },
                { "salary", 900 }, { "market_salary", 2250 }, { "pay_gap", 0.6 } };
            int winSeed = SeedWhere(true, 0.70);
            int loseSeed = SeedWhere(false, 0.70);
            ok(winSeed >= 0 && loseSeed >= 0, "the salt-31 stream has both outcomes to pin");

            GameState s1 = St(winSeed, "office");
            s1.Employees = new List<Employee> { new Employee { Name = "Mara Voss",
                Role = "engineer", Salary = 900, Burnout = 10 } };
            s1.Morale = 70;
            var rep1 = new WeeklyReport();
            Rival rd1 = s1.Rivals[0];
            rd1.Vigor = 80.0;
            double strBefore = rd1.Strength;
            bool landed = SimStreet.ResolvePoach(s1, rep1, new List<string>(), rd1, 1, target);
            ok(landed && s1.Employees.Count == 0, "the number won: they are off the roster this week");
            ok(s1.Morale == 64, "the team feels it: morale −6");
            ok(Math.Abs(rd1.Strength - (strBefore + 2.0)) < 1e-9,
                "the rival banks the hire: strength +2");
            bool named = false;
            foreach (string l in rep1.Events)
            {
                if (l.Contains("Mara Voss")) { named = true; }
            }
            ok(named, "the receipt names the person who left");
            ok((int)s1.GetMetaF("poach_wk", -1.0) == s1.Week,
                "the crew desk is handed the week the phone rang");

            GameState s2 = St(loseSeed, "office");
            s2.Employees = new List<Employee> { new Employee { Name = "Mara Voss",
                Role = "engineer", Salary = 900, Burnout = 10 } };
            var rep2 = new WeeklyReport();
            Rival rd2 = s2.Rivals[0];
            rd2.Vigor = 80.0;
            bool landed2 = SimStreet.ResolvePoach(s2, rep2, new List<string>(), rd2, 1, target);
            ok(!landed2 && s2.Employees.Count == 1, "a lost recruiting battle costs no headcount");
            ok((int)s2.GetMetaF("poach_failed_wk", -1.0) == s2.Week,
                "a failed poach opens the counter-offer season for the labor desk");
        }

        /// <summary>The first seed whose salt-31 draw lands on the wanted side of `p` — a
        /// pinned die found by search, because the two engines' PRNGs differ by design.</summary>
        private static int SeedWhere(bool wantWin, double p)
        {
            for (int s = 1; s < 400; s++)
            {
                var probe = new GameState();
                probe.SimSeed = s;
                probe.Week = 5;
                double d = SimEngine.RngForSalt(probe, SimEngine.SALT_RIVAL_POACH).Randf();
                if ((d < p) == wantWin) { return s; }
            }
            return -1;
        }

        // ── 5 · THE PRICE WAR ────────────────────────────────────────────────
        /// <summary>Bertrand undercutting, in one number: the street's reference price
        /// drops, so holding your list price through a war reads as expensive.</summary>
        private static void PinPriceWar(Action<bool, string> ok)
        {
            GameState s = St(11, "office");
            SimEngine.AddStatus(s, "price_war", 3);
            ok(Math.Abs(SimEngine.StreetFairMult(s) - 0.92) < 1e-12,
                "a price war knocks 8% off the going rate");
            double want = Math.Pow(1.0 / 0.92, -2.2);
            ok(Math.Abs(SimEngine.OffersDemandMult(s) - want) < 1e-9, string.Format(
                CultureInfo.InvariantCulture,
                "an offer held at its old list price loses demand at its own elasticity ({0:0.0000})", want));
            s.Statuses = new List<Status>();
            ok(Math.Abs(SimEngine.StreetFairMult(s) - 1.0) < 1e-12
                && Math.Abs(SimEngine.OffersDemandMult(s) - 1.0) < 1e-9,
                "the war ends and the reference price mean-reverts to fair");
        }

        // ── 6 · THE MACRO ────────────────────────────────────────────────────
        /// <summary>One stylised business cycle plus rare credit shocks. The cycle is a
        /// pure function of seed and week; the shocks reprice every valuation and
        /// term sheet at once, which is the whole lesson about raise timing.</summary>
        private static void PinMacro(Action<bool, string> ok)
        {
            GameState s = St(4242, "office");
            s.Week = 10;
            double want = 1.0 + 0.12 * Math.Sin(2.0 * Math.PI * 40.0 / 52.0);   // phase = 4242 % 52 = 30
            ok(Math.Abs(SimStreet.CycleTarget(s) - want) < 1e-9,
                "the season is a pure function of seed and week (phase 30)");
            ok(SimStreet.TrendBand(1.12) == "tailwinds" && SimStreet.TrendBand(0.88) == "headwinds"
                && SimStreet.TrendBand(1.0) == "calm", "the banner reads the trend in words");

            GameState bas = St(4242, "office");
            bas.Week = 10;
            int vBase = SimEngine.Valuation(bas);
            List<FundingOffer> oBase = SimEngine.GenerateOffers(bas, bas.Investors);
            GameState win = St(4242, "office");
            win.Week = 10;
            SimEngine.AddStatus(win, "funding_winter", 8);
            ok(Math.Abs(SimEngine.ShockValMult(win) - 0.6) < 1e-12
                && Math.Abs(SimEngine.ShockAmtMult(win) - 0.7) < 1e-12
                && Math.Abs(SimEngine.ShockSpreadMult(win) - 1.25) < 1e-12,
                "a funding winter reprices valuations 0.6x, checks 0.7x, equity asks 1.25x");
            ok(Math.Abs(SimEngine.Valuation(win) - vBase * 0.6) <= 1.0,
                "the winter's 0.6x lands on the valuation before the int cast");
            List<FundingOffer> oWin = SimEngine.GenerateOffers(win, win.Investors);
            ok(Math.Abs(oWin[0].Amount - oBase[0].Amount * 0.6 * 0.7) <= 2.0,
                "a winter's checks come in smaller on the same salt-9 draws");
            // THE PRICE OF MONEY, not the size of the bite: a winter shrinks the
            // check faster (0.42x) than it widens the ask (1.25x), so the absolute
            // equity percentage FALLS while every dollar costs far more of the
            // company. That ratio is the raise-timing lesson, and it is what this pins.
            double perWin = oWin[0].EquityPct / Gd.Maxf(oWin[0].Amount, 1.0);
            double perBase = oBase[0].EquityPct / Gd.Maxf(oBase[0].Amount, 1.0);
            ok(perWin > perBase, string.Format(CultureInfo.InvariantCulture,
                "a winter charges more of the company per dollar raised ({0:0.00}x)", perWin / perBase));
            GameState boom = St(4242, "office");
            boom.Week = 10;
            SimEngine.AddStatus(boom, "boom", 8);
            ok(SimEngine.Valuation(boom) > vBase && SimEngine.ShockSpreadMult(boom) < 1.0,
                "a boom mirrors it upward: richer valuations, gentler terms");
            ok(SimStreet.Season(win) == "winter" && SimStreet.Season(boom) == "boom"
                && SimStreet.Season(bas) == "steady",
                "the persisted season word is what the M&A desk reads");
        }
    }
}
