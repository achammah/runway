using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using Runway.Core;

namespace Runway.CoreTests
{
    /// <summary>
    /// SimEngine contract suite — hermetic, no network. The C# twin of
    /// game/tests/sim_engine_test.gd (every _ok ported, in the same order),
    /// followed by the 5-strategy balance run from game/tests/balance_sim.gd.
    ///
    /// Run: dotnet run --project unity/Runway.Core.Tests
    /// </summary>
    public static class Program
    {
        private static int _checks;
        private static bool _failed;
        private static readonly List<string> _failures = new List<string>();

        private static void Ok(bool cond, string msg)
        {
            _checks += 1;
            if (!cond)
            {
                _failed = true;
                _failures.Add("FAIL: " + msg);
            }
        }

        private static string S(object v)
        {
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        private static GameState NewState()
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

        /// <summary>
        /// Core does no IO of its own. Here the host is a console app reading the
        /// same StreamingAssets folder Unity ships.
        /// </summary>
        private static string ResolveStreamingAssets()
        {
            var probes = new List<string>();
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                probes.Add(Path.Combine(dir.FullName, "Assets", "StreamingAssets"));
                dir = dir.Parent;
            }
            var cwd = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (cwd != null)
            {
                probes.Add(Path.Combine(cwd.FullName, "Assets", "StreamingAssets"));
                probes.Add(Path.Combine(cwd.FullName, "unity", "Assets", "StreamingAssets"));
                cwd = cwd.Parent;
            }
            foreach (string p in probes)
            {
                if (File.Exists(Path.Combine(p, "items.json")))
                {
                    return p;
                }
            }
            throw new FileNotFoundException("could not locate Assets/StreamingAssets/items.json");
        }

        public static int Main(string[] args)
        {
            string dataDir = ResolveStreamingAssets();
            CoreFiles.Reader = name =>
            {
                string path = Path.Combine(dataDir, name);
                return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            };

            RunChecks();
            Console.WriteLine();
            RunBalanceSim();
            Console.WriteLine();
            if (Array.IndexOf(args ?? new string[0], "--economy") >= 0)
            {
                EconomyProbe.Run();
                Console.WriteLine();
            }

            if (_failed)
            {
                foreach (string f in _failures)
                {
                    Console.WriteLine(f);
                }
                Console.WriteLine(S(_checks) + " checks run, " + S(_failures.Count) + " failed");
                Console.WriteLine("ENGINE FAIL");
                return 1;
            }
            Console.WriteLine(S(_checks) + " checks held");
            Console.WriteLine("ENGINE PASS");
            return 0;
        }

        private static void RunChecks()
        {
            // ── theta clamps hold against hostile input
            Theta mad = SimEngine.ClampTheta(new Dictionary<string, double>
            {
                { "tam", 1e12 }, { "adopt_p", 9.0 }, { "lifetime_wk", -5 }
            });
            Ok(mad.Tam <= 5000000.0, "tam clamps down");
            Ok(mad.AdoptP <= 0.004, "adopt_p clamps down");
            Ok(mad.LifetimeWk >= 6.0, "lifetime clamps up");

            // ── determinism: same seed+week = identical tick
            GameState a = NewState();
            GameState b = NewState();
            SimEngine.WeeklyTick(a);
            SimEngine.WeeklyTick(b);
            Ok(a.Cash == b.Cash && a.Traction == b.Traction,
                "tick is deterministic for identical state");

            // ── the world grinds: an unlaunched idle company loses money
            GameState s0 = NewState();
            s0.Traction = 0;
            int cash0 = s0.Cash;
            for (int i = 0; i < 4; i++)
            {
                s0.Week += 1;
                SimEngine.WeeklyTick(s0);
            }
            Ok(s0.Cash < cash0, "an idle company burns down (" + S(cash0) + " -> " + S(s0.Cash) + ")");

            // ── churn punishes a bad product; a good one retains
            GameState bad = NewState(); bad.Product = 5;
            GameState good = NewState(); good.Product = 95;
            WeeklyReport rb = SimEngine.WeeklyTick(bad);
            WeeklyReport rg = SimEngine.WeeklyTick(good);
            Ok(rb.Churn > rg.Churn, "worse product churns more (" + S(rb.Churn) + " > " + S(rg.Churn) + ")");

            // ── statuses: catalog-only, install, expire, affect the tick
            GameState st = NewState();
            Ok(!SimEngine.AddStatus(st, "made_up_buff", 3), "unknown status refused");
            Ok(SimEngine.AddStatus(st, "viral_moment", 2), "catalog status installs");
            WeeklyReport boosted = SimEngine.WeeklyTick(st);
            GameState st2 = NewState();
            WeeklyReport plain = SimEngine.WeeklyTick(st2);
            Ok(boosted.Adds > plain.Adds,
                "viral_moment lifts adoption (" + S(boosted.Adds) + " > " + S(plain.Adds) + ")");
            SimEngine.WeeklyTick(st);
            Ok(!SimEngine.HasStatus(st, "viral_moment"), "status expires on schedule");

            // ── clocks fire exactly once, at zero
            GameState ck = NewState();
            SimEngine.AddClock(ck, 2, "the term sheet expires");
            WeeklyReport t1 = SimEngine.WeeklyTick(ck);
            Ok(t1.FiredClocks.Count == 0, "clock silent with a week left");
            WeeklyReport t2 = SimEngine.WeeklyTick(ck);
            Ok(t2.FiredClocks.Contains("the term sheet expires"), "clock fires at zero");
            Ok(ck.Clocks.Count == 0, "fired clock is removed");

            // ── hiring pipeline: paid immediately, productive after onboarding
            GameState hp = NewState();
            hp.Pipeline.Add(new PipelineHire { Name = "Priya", Role = "engineer", Salary = 1500, WeeksIn = 0 });
            WeeklyReport r1 = SimEngine.WeeklyTick(hp);
            Ok(hp.Employees.Count == 0, "week 1: still onboarding");
            Ok(r1.Burn > 1500, "week 1: already on payroll");
            SimEngine.WeeklyTick(hp);
            Ok(hp.Employees.Count == 1, "week 2: productive");

            // ── advantage/disadvantage from state
            GameState av = NewState();
            SimEngine.AddStatus(av, "data_room_ready", 3);
            RollContext ctx = SimEngine.RollContext(av, "raise");
            Ok(ctx.Advantage, "data room grants advantage on raise");
            SimEngine.AddStatus(av, "investor_pressure", 3);
            ctx = SimEngine.RollContext(av, "raise");
            Ok(!ctx.Advantage && !ctx.Disadvantage, "adv + dis cancel to a straight roll");
            av.Exhaustion = 4;
            RollContext gctx = SimEngine.RollContext(av, "grit");
            Ok(gctx.Disadvantage, "exhaustion 4 = disadvantage on grit");

            // ── the 2d20 keep rule
            int[] seq = { 3, 17 };
            int[] i2 = { 0 };
            Func<int> roller = () =>
            {
                int v = seq[i2[0] % 2];
                i2[0] += 1;
                return v;
            };
            RollContext advRoll = SimEngine.RollD20Ctx(av, "raise", roller);   // cancels: straight
            Ok(advRoll.D20 == 3, "straight roll takes the first die");
            i2[0] = 0;
            RollContext g2 = SimEngine.RollD20Ctx(av, "grit", roller);
            Ok(g2.D20 == 3, "disadvantage keeps the WORST of 2d20");

            // ── THE SIX TRAITS: who the founder is, priced.
            // The spreads below mirror data/archetypes.json on purpose — this suite
            // is about the RULES, and a rule that only holds for today's numbers is
            // not one.
            Func<Dictionary<string, int>> exfaang = () => new Dictionary<string, int>
            {
                { "charisma", 3 }, { "luck", 2 }, { "network", 4 },
                { "focus", 3 }, { "credibility", 5 }, { "stamina", 2 }
            };
            Func<Dictionary<string, int>> hacker = () => new Dictionary<string, int>
            {
                { "charisma", 1 }, { "luck", 4 }, { "network", 1 },
                { "focus", 5 }, { "credibility", 2 }, { "stamina", 4 }
            };

            GameState tl = NewState();
            tl.Traits = hacker();
            Ok(tl.TraitLevel("focus") == 5, "trait reads its archetype base");
            tl.Items = new List<string> { "itm_houseplant" };            // +1 focus, -1 network
            Ok(tl.TraitLevel("focus") == 5, "a buff on a maxed trait clamps at 5");
            Ok(tl.TraitLevel("network") == 1, "a nerf on a floored trait clamps at 1");
            tl.Traits = exfaang();
            tl.Items = new List<string> { "itm_headphones" };            // +2 focus, -1 network
            Ok(tl.TraitLevel("focus") == 5 && tl.TraitLevel("network") == 3,
                "item mods add to the base (focus " + S(tl.TraitLevel("focus"))
                + ", network " + S(tl.TraitLevel("network")) + ")");
            Ok(tl.ItemTraitDelta("focus") == 2, "the bag's own swing is readable on its own");

            // the owner's case: the ex-FAANG PM walks into the raise with the doors open
            GameState ex = NewState();
            ex.Traits = exfaang();
            RollContext exRaise = SimEngine.RollContext(ex, "raise");
            Ok(exRaise.Advantage && exRaise.AdvReasons.Count > 0 && exRaise.AdvReasons[0].StartsWith("doors open"),
                "credibility 5 + network 4 = advantage on raise");
            GameState hk = NewState();
            hk.Traits = hacker();
            Ok(!SimEngine.RollContext(hk, "raise").Advantage,
                "credibility 2 + network 1 gets no such door");
            // and the bag can buy the door: 3+3 is six, the ring makes it eight
            GameState ring = NewState();
            Ok(!SimEngine.RollContext(ring, "raise").Advantage, "a plain founder has no door");
            ring.Items = new List<string> { "itm_alumni_ring" };         // +1 network, +1 credibility
            Ok(SimEngine.RollContext(ring, "raise").Advantage, "an item can open the investor doors");

            GameState ch = NewState();
            ch.Traits = new Dictionary<string, int>
            {
                { "charisma", 4 }, { "luck", 3 }, { "network", 3 },
                { "focus", 4 }, { "credibility", 3 }, { "stamina", 2 }
            };
            Ok(SimEngine.RollContext(ch, "sell").Advantage, "charisma 4 = advantage on sell");
            Ok(SimEngine.RollContext(ch, "recruit").Advantage, "charisma 4 = advantage on recruit");
            Ok(SimEngine.RollContext(ch, "build").Advantage, "focus 4 = advantage on build");
            Ok(!SimEngine.RollContext(ch, "grit").Disadvantage,
                "stamina 2 costs nothing while the founder is rested");
            ch.Exhaustion = 3;
            RollContext tired = SimEngine.RollContext(ch, "grit");
            Ok(tired.Disadvantage && tired.DisReasons.Contains("no reserves"),
                "stamina 2 + exhaustion 3 = no reserves on grit");

            // LUCK bends the two extremes, deterministically, through the caller's dice
            GameState lucky = NewState();
            lucky.Traits = hacker();                                     // luck 4
            int[] lseq = { 1, 12, 17 };
            int[] li = { 0 };
            Func<int> lroll = () =>
            {
                int v = lseq[Gd.Mini(li[0], lseq.Length - 1)];
                li[0] += 1;
                return v;
            };
            RollContext luckyRoll = SimEngine.RollD20Ctx(lucky, "sell", lroll);
            Ok(luckyRoll.D20 == 17 && luckyRoll.LuckNote == "luck rerolls the 1",
                "luck 4 rerolls the natural 1 (kept " + S(luckyRoll.D20) + ")");
            GameState plainLuck = NewState();                            // luck 3: the 1 stands
            li[0] = 0;
            Ok(SimEngine.RollD20Ctx(plainLuck, "sell", lroll).D20 == 1,
                "luck 3 leaves the natural 1 exactly where it fell");
            GameState cursed = NewState();
            cursed.Traits = new Dictionary<string, int>
            {
                { "charisma", 4 }, { "luck", 1 }, { "network", 3 },
                { "focus", 3 }, { "credibility", 4 }, { "stamina", 2 }
            };
            int[] cseq = { 20, 4 };
            int[] ci = { 0 };
            Func<int> croll = () =>
            {
                int v = cseq[Gd.Mini(ci[0], cseq.Length - 1)];
                ci[0] += 1;
                return v;
            };
            RollContext cursedRoll = SimEngine.RollD20Ctx(cursed, "grit", croll);
            Ok(cursedRoll.D20 == 19 && cursedRoll.LuckNote == "never quite perfect",
                "luck 1 turns the natural 20 into a 19 (kept " + S(cursedRoll.D20) + ")");

            // the room is warmer for people it already believes: same company, better terms
            GameState warm = NewState();
            warm.Traction = 500;
            warm.LastGrowth = 0.10;
            warm.Investors = new List<Investor> { new Investor { Name = "Fund A", Thesis = "momentum" } };
            warm.Traits = new Dictionary<string, int>
            {
                { "charisma", 3 }, { "luck", 2 }, { "network", 5 },
                { "focus", 3 }, { "credibility", 5 }, { "stamina", 2 }
            };
            GameState cold = NewState();
            cold.Traction = 500;
            cold.LastGrowth = 0.10;
            cold.Investors = warm.Investors;
            cold.Traits = new Dictionary<string, int>
            {
                { "charisma", 3 }, { "luck", 2 }, { "network", 1 },
                { "focus", 3 }, { "credibility", 1 }, { "stamina", 2 }
            };
            List<FundingOffer> warmOffers = SimEngine.GenerateOffers(warm, warm.Investors);
            List<FundingOffer> coldOffers = SimEngine.GenerateOffers(cold, cold.Investors);
            Ok(Gd.Absf(warmOffers[0].Warmth - 8.0) < 0.01,
                "warmth caps at 8% (got " + Gd.F(warmOffers[0].Warmth, 1) + ")");
            Ok(coldOffers[0].Warmth == 0.0, "a cold room discounts nothing");
            Ok(warmOffers[0].EquityPct < coldOffers[0].EquityPct,
                "the same company gives up less equity when the room is warm ("
                + Gd.F(warmOffers[0].EquityPct, 1) + "% < " + Gd.F(coldOffers[0].EquityPct, 1) + "%)");
            Ok(warmOffers[0].EquityPct >= warmOffers[0].FairPct,
                "a warm offer is still never below fair");

            // and every rule the engine runs can say its own name
            List<string> says = SimEngine.TraitEffects(ex);
            bool doorsSaid = false;
            foreach (string line in says)
            {
                if (line.StartsWith("doors open"))
                {
                    doorsSaid = true;
                }
            }
            Ok(doorsSaid, "trait_effects reports the door it opened");
            Ok(SimEngine.TRAIT_RULES.Count == GameState.TRAIT_NAMES.Count,
                "every trait carries the words that explain it");

            // ── margin bands
            Ok(SimEngine.MarginBand(20, 12) == "brilliant", "beat by 5+ = brilliant");
            Ok(SimEngine.MarginBand(12, 12) == "fine", "meet it = fine");
            Ok(SimEngine.MarginBand(10, 12) == "risky", "miss by 1-2 = risky");
            Ok(SimEngine.MarginBand(5, 12) == "backfired", "miss by 3+ = backfired");

            // ── funding: dilution math and the desperation spread
            GameState f = NewState();
            f.Traction = 500;
            f.LastGrowth = 0.10;
            int pre = SimEngine.Valuation(f);
            Ok(pre > f.Cash, "traction + growth beats cash-floor valuation");
            f.Investors = new List<Investor> { new Investor { Name = "Fund A", Thesis = "momentum" } };
            List<FundingOffer> offers = SimEngine.GenerateOffers(f, f.Investors);
            Ok(offers.Count == 3, "three offers");
            foreach (FundingOffer o in offers)
            {
                Ok(o.EquityPct >= o.FairPct, "every offer is priced at or above fair");
            }
            GameState broke = NewState();
            broke.Cash = -100;
            broke.Investors = f.Investors;
            List<FundingOffer> sharky = SimEngine.GenerateOffers(broke, broke.Investors);
            Ok(sharky[0].EquityPct > offers[0].FairPct, "desperation prices against the founder");
            double fp = f.FounderPct;
            SimEngine.ApplyRound(f, 100000, 20.0);
            Ok(Gd.Absf(f.FounderPct - fp * 0.8) < 0.01, "20% round dilutes founder by exactly 20%");
            Ok(f.RoundsRaised.Count == 1 && f.RoundsRaised[0] == "pre-seed",
                "round ladder appends by count");

            // ── commitments recur then expire
            GameState cm = NewState();
            cm.Commitments.Add(new Commitment { Name = "the lease deal", CashWk = -300, WeeksLeft = 2 });
            SimEngine.WeeklyTick(cm);
            Ok(cm.Commitments.Count == 1, "commitment persists mid-term");
            WeeklyReport rc = SimEngine.WeeklyTick(cm);
            Ok(cm.Commitments.Count == 0 && rc.Expired.Contains("the lease deal"),
                "commitment expires and is reported");

            // ── signals speak founder
            Dictionary<string, object> sg = SimEngine.Signals(NewState());
            string health = S(sg["health"]);
            Ok(health.StartsWith("STABLE") || health.StartsWith("WARNING"), "health band renders");
            Ok(sg.ContainsKey("runway_weeks") && sg.ContainsKey("market_phase"), "signals carry the vitals");

            // ── the ledger levers are real money with real effects
            GameState lv = NewState();
            lv.SetFlag("launched");
            lv.Traction = 600;
            WeeklyReport plainR = SimEngine.WeeklyTick(lv);
            GameState lv2 = NewState();
            lv2.SetFlag("launched");
            lv2.Traction = 600;
            lv2.Budgets = new Budgets { Marketing = 0, Sales = 0, Care = 2000, Rnd = 0 };
            WeeklyReport cared = SimEngine.WeeklyTick(lv2);
            Ok(cared.Churn < plainR.Churn,
                "care budget retains (" + S(cared.Churn) + " < " + S(plainR.Churn) + " churn)");
            Ok(cared.Burn >= plainR.Burn + 2000, "care budget is real burn");
            GameState lv3 = NewState();
            lv3.Budgets = new Budgets { Marketing = 0, Sales = 0, Care = 0, Rnd = 2400 };
            int p0 = lv3.Product;
            SimEngine.WeeklyTick(lv3);
            Ok(lv3.Product >= p0 + 2, "rnd budget ships product (+" + S(lv3.Product - p0) + ")");
            GameState lv4 = NewState();
            lv4.Budgets = new Budgets { Marketing = 3000, Sales = 1000, Care = 0, Rnd = 0 };
            lv4.SetFlag("launched");
            lv4.Traction = 40;
            WeeklyReport ue = SimEngine.WeeklyTick(lv4);
            Ok(ue.Cac > 0 && ue.Ltv > 0,
                "unit economics computed (CAC " + S(ue.Cac) + ", LTV " + S(ue.Ltv) + ")");

            // ── beliefs start wrong and converge with analytics
            GameState bl = NewState();
            SimEngine.WeeklyTick(bl);
            double wrong = Gd.Absf(bl.Beliefs.Tam - bl.Theta.Tam);
            bl.AnalyticsLevel = 2;
            bl.Traction = 60;
            for (int i = 0; i < 12; i++)
            {
                bl.Week += 1;
                SimEngine.WeeklyTick(bl);
            }
            double closer = Gd.Absf(bl.Beliefs.Tam - bl.Theta.Tam);
            Ok(closer < wrong * 0.6,
                "beliefs converge toward truth (gap " + S(Gd.ToInt(wrong)) + " -> " + S(Gd.ToInt(closer)) + ")");

            // ── pricing: the demand curve discriminates (no $500 massages)
            var mo = new Offer
            {
                Name = "massage", Unit = "per session", FairPrice = 70.0,
                Elasticity = 2.6, UnitCost = 18.0, Price = 0.0, Weight = 1.0
            };
            Ok(SimEngine.OfferDemand(mo, 70.0) > 0.95 && SimEngine.OfferDemand(mo, 70.0) <= 1.05,
                "fair price = fair demand");
            Ok(SimEngine.OfferDemand(mo, 500.0) < 0.01,
                "a $500 massage sells to ~nobody (" + Gd.F(SimEngine.OfferDemand(mo, 500.0), 4) + ")");
            Ok(SimEngine.OfferDemand(mo, 45.0) > 1.5, "a discount stokes demand");
            Ok(SimEngine.OfferDemand(mo, 0.0) == 0.0, "unpriced = not on sale");
            GameState ps = NewState();
            ps.Traction = 100;
            ps.SetFlag("launched");
            ps.Offers = new List<Offer> { mo.Duplicate() };
            WeeklyReport rUnp = SimEngine.WeeklyTick(ps);
            // LAW OVERRULED by the owner ("10 customers but no money...
            // IMPOSSIBLE"): unpriced no longer earns zero — it bills at the
            // going (fair) rate.
            Ok(rUnp.Revenue > 800, "unpriced offers bill at the going rate (" + S(rUnp.Revenue) + ")");
            GameState ps2 = NewState();
            ps2.Traction = 100;
            ps2.SetFlag("launched");
            Offer mo2 = mo.Duplicate(); mo2.Price = 70.0;
            ps2.Offers = new List<Offer> { mo2 };
            WeeklyReport rFair = SimEngine.WeeklyTick(ps2);
            Ok(rFair.Revenue > 800, "fairly priced sessions pay the rent (" + S(rFair.Revenue) + ")");
            GameState ps3 = NewState();
            ps3.Traction = 100;
            ps3.SetFlag("launched");
            Offer mo3 = mo.Duplicate(); mo3.Price = 500.0;
            ps3.Offers = new List<Offer> { mo3 };
            // THE LAW CHANGED (#196): greed no longer taxes existing spend
            // invisibly — it starves acquisition and bleeds the base. The
            // overpriced pay full freight until they leave.
            Ok(SimEngine.OffersDemandMult(ps3) < 0.15,
                "greed starves adoption (mult " + S2(SimEngine.OffersDemandMult(ps3)) + ")");
            Ok(SimEngine.OffersPricePain(ps3) > 1.5,
                "greed pains retention (pain " + S2(SimEngine.OffersPricePain(ps3)) + ")");
            GameState psFairRun = NewState(); psFairRun.Traction = 40; psFairRun.Cash = 100000;
            psFairRun.Offers = new List<Offer> { mo.Duplicate() };
            GameState psGreedRun = NewState(); psGreedRun.Traction = 40; psGreedRun.Cash = 100000;
            psGreedRun.Offers = new List<Offer> { mo3.Duplicate() };
            for (int wk = 0; wk < 8; wk++) { SimEngine.WeeklyTick(psFairRun); SimEngine.WeeklyTick(psGreedRun); }
            Ok(psGreedRun.Traction < 40 && psGreedRun.Traction <= psFairRun.Traction - 5,
                "greed bleeds the base while fair holds (" + S(psGreedRun.Traction) + " vs " + S(psFairRun.Traction) + ")");
            // THE OWNER'S CASE, PINNED: 16 customers on a $70 weekly-cadence
            // offer read like founder math — hundreds per week, not $200.
            GameState own = NewState();
            own.Traction = 16;
            var ownOffer = new Offer { Name = "standard session", Unit = "per session",
                                       Price = 70.0, FairPrice = 45.0, UnitCost = 18.0, Weight = 1.0 };
            own.Offers = new List<Offer> { ownOffer };
            WeeklyReport rOwn = SimEngine.WeeklyTick(own);
            Ok(rOwn.Revenue >= 900 && rOwn.Revenue <= 1300,
                "16 x $70 session reads like founder math (" + S(rOwn.Revenue) + "/wk)");
            // THE BACKSTOP, PINNED (owner: "10 customers but no money...
            // IMPOSSIBLE"): an unpriced offer bills at the going rate. Zero
            // revenue with customers on the books cannot happen by algorithm.
            GameState np = NewState();
            np.Traction = 10;
            np.Offers = new List<Offer> { new Offer { Name = "consulting session",
                Unit = "per session", Price = 0.0, FairPrice = 70.0,
                UnitCost = 18.0, Weight = 1.0 } };
            WeeklyReport rNp = SimEngine.WeeklyTick(np);
            Ok(rNp.Revenue >= 550 && rNp.Revenue <= 850,
                "10 unpriced customers pay the going rate (" + S(rNp.Revenue) + "/wk)");
            Ok(SimEngine.OffersPricePain(np) == 1.0 && SimEngine.OffersDemandMult(np) >= 0.99,
                "fair billing carries no pain and fair demand");
            // THE OFFICE LANE: perks money buys morale, and it costs real burn.
            GameState ofA = NewState(); ofA.Cash = 100000;
            GameState ofB = NewState(); ofB.Cash = 100000;
            ofB.Budgets.Office = 2000;
            for (int wk2 = 0; wk2 < 8; wk2++) { SimEngine.WeeklyTick(ofA); SimEngine.WeeklyTick(ofB); }
            Ok(ofB.Morale > ofA.Morale,
                "the office lane buys morale (" + S(ofB.Morale) + " vs " + S(ofA.Morale) + ")");
            Ok(ofA.Cash - ofB.Cash >= 8 * 1500,
                "office money is real burn (Δ$" + S(ofA.Cash - ofB.Cash) + " over 8 wks)");
            // THE LEARNING CURVE: serving 1000 customers cheapens serving ~34%.
            GameState lcs = NewState();
            lcs.ServedTotal = 1000;          // a saved FIELD now, not an Object meta
            Ok(SimEngine.LearningCurve(lcs) > 0.6 && SimEngine.LearningCurve(lcs) < 0.7,
                "the learning curve pays at scale (×" + S2(SimEngine.LearningCurve(lcs)) + ")");
            // THE P&L IDENTITY: the binder's record balances to the ledger.
            GameState pns = NewState(); pns.Traction = 10;
            pns.Offers = new List<Offer> { new Offer { Name = "s", Unit = "per session",
                Price = 70.0, FairPrice = 45.0, UnitCost = 18.0, Weight = 1.0 } };
            SimEngine.WeeklyTick(pns);
            Ok(pns.LastPnl != null && pns.LastPnl.Net == pns.LastPnl.Revenue - pns.LastPnl.Burn - pns.LastPnl.LiabilitiesWk,
                "the P&L balances (net " + S(pns.LastPnl != null ? pns.LastPnl.Net : -1) + ")");

            // ── loan compounding punishes
            GameState ln = NewState();
            ln.Cash = 500;
            ln.Traction = 0;
            ln.LoanPrincipal = 10000;
            SimEngine.WeeklyTick(ln);
            Ok(SimBank.DebtTotal(ln) >= 11800 && ln.LoanPrincipal == 0,
               "18%/wk compounds through the migrated note (owe " + S(SimBank.DebtTotal(ln)) + ")");

            // ── WAVE A: the four bugs the design corpus found (DECISIONS.md)
            // 1 — price_offer was in the schema and the executor but not the
            // validator, so every DM reply that priced an offer was thrown away.
            Ok(Array.IndexOf(SimEngine.OP_REGISTRY, "price_offer") >= 0,
                "price_offer survives the ops validator");
            Ok(Array.IndexOf(SimEngine.OP_REGISTRY, "push_lead") >= 0,
                "push_lead is a live op");
            Ok(SimEngine.OP_REGISTRY.Length == 16,
                "the op registry carries " + S(SimEngine.OP_REGISTRY.Length) + " ops");
            // 2 — the catalog cost-lines engine half existed only in Godot
            GameState cl = NewState();
            Offer withLines = SimEngine.AddOffer(cl, "workshop", "per session", 200.0, 0.0, 2.0, 1.0,
                new List<CostLine>
                {
                    new CostLine { Label = "materials", Amount = 30.0 },
                    new CostLine { Label = "room hire", Amount = 20.0 },
                },
                new List<CostLine> { new CostLine { Label = "insurance", Amount = 45.0 } });
            Ok(Gd.Absf(withLines.UnitCost - 50.0) < 0.01,
                "unit cost is the sum of its variable lines (" + S2(withLines.UnitCost) + ")");
            Ok(Gd.Absf(withLines.FixedWk - 45.0) < 0.01,
                "fixed_wk is the sum of its weekly lines (" + S2(withLines.FixedWk) + ")");
            Ok(Gd.Absf(SimEngine.OffersFixedWk(cl) - 45.0) < 0.01,
                "the catalog's weekly overhead reaches the engine");
            // a line above half of fair is clamped, and the total follows it down
            withLines.CostLines[0].Amount = 5000.0;
            SimEngine.SyncOfferCosts(withLines);
            Ok(withLines.UnitCost <= 200.0 * 0.9 + 0.01,
                "an itemised cost sheet still cannot exceed 90% of fair");
            // the deep copy: two offers must never share one line object
            Offer copy = withLines.Duplicate();
            copy.CostLines[0].Amount = 1.0;
            SimEngine.SyncOfferCosts(copy);
            Ok(withLines.CostLines[0].Amount != 1.0, "duplicating an offer deep-copies its cost sheet");
            // the catalog overhead is a real P&L lane, not a silent cost
            GameState fx2 = NewState();
            fx2.Traction = 10;
            SimEngine.AddOffer(fx2, "kit", "per order", 100.0, 20.0, 2.0, 1.0, null,
                new List<CostLine> { new CostLine { Label = "storage", Amount = 120.0 } });
            SimEngine.WeeklyTick(fx2);
            Ok(fx2.LastPnl.OfferFixed == 120, "catalog overheads land in the P&L (" + S(fx2.LastPnl.OfferFixed) + ")");
            // 3 — served_total is a FIELD: the learning curve used to reset on load
            GameState svd = NewState();
            svd.Traction = 25;
            SimEngine.WeeklyTick(svd);
            Ok(svd.ServedTotal >= 25, "served_total accumulates on a real field (" + S(svd.ServedTotal) + ")");

            // ── THE SALT REGISTRY: names, never numbers, and 95 stays burned
            Ok(SimEngine.SALT_LABOR_ARRIVALS == 20 && SimEngine.SALT_RIVAL_ACTION == 30
               && SimEngine.SALT_PIPELINE == 50 && SimEngine.SALT_ROADMAP_SHIP == 70
               && SimEngine.SALT_MACRO_SHOCK == 80 && SimEngine.SALT_MNA == 100
               && SimEngine.SALT_HW_BREAKDOWN == 110,
                "the salt registry matches the spine's table");
            Ok(SimEngine.SALT_BURNED == 95, "salt 95 is burned, not assigned");

            // ── THE STATUS CATALOG's wave additions: installable by name,
            // magnitudes in one place, and the new effect keys stay out of the
            // adoption loop.
            GameState stc = NewState();
            Ok(SimEngine.AddStatus(stc, "price_war", 4) && SimEngine.AddStatus(stc, "board_delight", 3),
                "the wave's statuses install by name");
            Ok(!SimEngine.AddStatus(stc, "made_up_buff", 3), "the catalog still refuses inventions");
            Ok(Gd.Absf(SimEngine.StreetFairMult(stc) - 0.92) < 0.001,
                "a price war drops the going rate (x" + S2(SimEngine.StreetFairMult(stc)) + ")");
            Ok(SimEngine.RollContext(stc, "raise").Advantage,
                "board_delight warms the room for a raise");
            GameState plainc = NewState();
            Ok(SimEngine.StreetFairMult(plainc) == 1.0, "no war, no discount on the street");
            // the price war is DEMAND-side: it never edits the founder's numbers
            GameState warp = NewState();
            warp.Offers = new List<Offer> { new Offer { Name = "s", Unit = "per session",
                Price = 70.0, FairPrice = 70.0, UnitCost = 18.0, Weight = 1.0 } };
            double fairBefore = SimEngine.OffersPricePain(warp);
            SimEngine.AddStatus(warp, "price_war", 4);
            Ok(SimEngine.OffersPricePain(warp) > fairBefore,
                "holding your price through a war reads as expensive ("
                + S2(SimEngine.OffersPricePain(warp)) + " > " + S2(fairBefore) + ")");
            Ok(warp.Offers[0].FairPrice == 70.0,
                "a rival never mutates the founder's own fair price");

            // ── THE P&L IDENTITY v2, both lines, on a week with every lane present
            GameState idn = NewState();
            idn.SetFlag("launched");
            idn.Traction = 120;
            idn.LoanPrincipal = 5000;
            idn.Budgets = new Budgets { Ads = 800, Content = 200, Sales = 400,
                                        Care = 300, Rnd = 600, Office = 250 };
            idn.Offers = new List<Offer> { new Offer { Name = "s", Unit = "per session",
                Price = 40.0, FairPrice = 38.0, UnitCost = 12.0, Weight = 1.0,
                FixedLines = new List<CostLine> { new CostLine { Label = "tools", Amount = 60.0 } },
                FixedWk = 60.0 } };
            idn.Commitments.Add(new Commitment { Name = "the van", CashWk = -150, WeeksLeft = 6 });
            bool sawInterest = false;
            bool sawStanding = false;
            for (int w = 0; w < 8; w++)
            {
                idn.Week += 1;
                SimEngine.WeeklyTick(idn);
                Pnl p = idn.LastPnl;
                int lanesSum = p.Cogs + p.Rent + p.Payroll + p.Infra + p.Marketing
                    + p.Sales + p.Care + p.Rnd + p.Office + p.OfferFixed
                    + p.Severance + p.Recruiting + p.Production + p.Subcontract
                    + p.EquipUpkeep + p.Carrying + p.Incident;
                Ok(Gd.Absi(p.Burn - lanesSum) <= 1,
                    "wk" + S(idn.Week) + " burn is the sum of its operating lanes ("
                    + S(p.Burn) + " vs " + S(lanesSum) + ")");
                Ok(p.Net == p.Revenue - p.Burn - p.LiabilitiesWk - p.Interest - p.Tax,
                    "wk" + S(idn.Week) + " net = revenue - burn - standing - interest - tax");
                if (p.Interest > 0)
                {
                    sawInterest = true;
                    // the whole point of moving interest before the record: burn
                    // is OPERATING spend, and the cost of debt sits outside it
                    Ok(p.Burn < p.Revenue - p.Net,
                        "wk" + S(idn.Week) + " burn excludes the interest that also hit the week");
                }
                if (p.LiabilitiesWk > 0) sawStanding = true;
            }
            Ok(sawInterest, "the loan's interest reaches the ledger instead of vanishing");
            Ok(sawStanding, "the standing-commitments lane reaches the ledger");

            // ── THE ATTENTION REGISTRY: one function behind every bang
            GameState at0 = NewState();
            at0.Offers = new List<Offer>();
            at0.LastPnl = new Pnl { Net = 500 };
            Ok(SimEngine.AttentionItems(at0).Count == 0, "a calm company raises no hands");
            GameState at1 = NewState();
            at1.Offers = new List<Offer> { new Offer { Name = "consulting", Unit = "per session",
                Price = 0.0, FairPrice = 70.0, UnitCost = 18.0, Weight = 1.0 } };
            bool sawUnpriced = false;
            foreach (AttentionItem r in SimEngine.AttentionItems(at1))
            {
                if (r.Key == "unpriced")
                {
                    sawUnpriced = true;
                    Ok(r.Desk == "pricing", "the unpriced row points at the pricing desk");
                    Ok(r.Label.Length <= 40,
                        "a ticker label fits the garage HUD (" + S(r.Label.Length) + " chars)");
                }
            }
            Ok(sawUnpriced, "an offer billing at the going rate raises its hand");
            GameState at2 = NewState();
            at2.SetFlag("fundraising_open");
            at2.LastPnl = new Pnl { Net = -900 };
            List<AttentionItem> rows2 = SimEngine.AttentionItems(at2);
            Ok(rows2.Count >= 2, "losing money and open term sheets both register");
            Ok(rows2[0].Severity >= rows2[rows2.Count - 1].Severity, "the loudest item sorts first");
            Ok(SimEngine.AttentionSeverity(at2, "cap table") == 3, "term sheets are an alarm");
            Ok(SimEngine.AttentionSeverity(at2, "product") == 0, "a quiet desk wears no bang");
            // THE PRE-ROLL REVIEW: the engine half — what stops a roll
            Ok(SimEngine.PrerollItems(at0).Count == 0, "nothing outstanding = no review card");
            List<AttentionItem> pr = SimEngine.PrerollItems(at2);
            Ok(pr.Count > 0, "the review card has something to say before the dice");
            int prMinSev = 3;
            foreach (AttentionItem r2 in pr) prMinSev = Gd.Mini(prMinSev, r2.Severity);
            Ok(prMinSev >= 2, "the review card never stops a roll over a mere note");

            // ── THE DIRECTIVE CAP: the composer truncates, subsystems never do
            var many = new List<string>();
            for (int i = 0; i < 40; i++)
                many.Add("- line " + S(i) + " that runs on for a while to eat the character budget");
            List<string> capped = SimEngine.CapDirectives(many);
            Ok(capped.Count <= SimEngine.DIRECTIVE_MAX_LINES, "the directive block caps at 24 lines");
            int capChars = 0;
            foreach (string l in capped) capChars += l.Length + 1;
            Ok(capChars <= SimEngine.DIRECTIVE_MAX_CHARS, "the directive block caps at 1200 chars");
            Ok(capped[0] == many[0], "priority is the order — line 1 is never dropped");

            // ── THE BUDGET MIGRATION: idempotent, old saves spend identically
            GameState mig = NewState();
            mig.Budgets = new Budgets { Marketing = 900, Sales = 100 };
            SimEngine.MigrateBudgets(mig);
            Ok(mig.Budgets.Ads == 900 && mig.Budgets.Marketing == 0,
                "legacy marketing money becomes paid ads");
            SimEngine.MigrateBudgets(mig);
            Ok(mig.Budgets.Ads == 900, "migrating twice does not double the money");
            Ok(mig.Budgets.Acquisition() == 900, "acquisition spend reads the channels");

            // ── OLD SAVES MUST LOAD (00-spine section 8). The frozen pre-wave
            // fixture: deserialize it, prove every new field sits at its
            // default, then tick four weeks and come out finite and alive.
            string fxTxt = ReadFixture("save_v2_prewave.json");
            Ok(fxTxt.Length > 0, "the frozen fixture is on disk");
            JObject fxDoc = JObject.Parse(fxTxt);
            Ok((int)fxDoc["version"] == 2, "the frozen fixture is a version-2 save");
            GameState oldState = fxDoc["state"].ToObject<GameState>();
            SimEngine.MigrateBudgets(oldState);
            Ok(oldState.Week == 5 && oldState.CompanyName == "Fernwood Supply",
                "the pre-wave run loads (wk " + S(oldState.Week) + ")");
            Ok(oldState.ServedTotal == 0 && oldState.OpenRoles.Count == 0
               && oldState.Applicants.Count == 0 && oldState.Recruiters == 0
               && oldState.Leads.Count == 0 && oldState.Logos.Count == 0
               && oldState.PipeUnits == 0.0 && oldState.Loans.Count == 0
               && oldState.Bets.Count == 0 && oldState.PlatformLevel == 0
               && oldState.Board == null && oldState.Mna == null
               && oldState.Hardware == null && oldState.ContentEquity == 0.0
               && oldState.OptionPoolPct == 0.0 && oldState.FounderBanked == 0
               && oldState.TaxLossCarry == 0 && oldState.MacroSeason == "steady",
                "every new subsystem field loads at its default");
            Ok(oldState.Sites.Count == 0 && oldState.PriceBook.Count == 0
               && oldState.Topics.Count == 0 && oldState.SpendBook.Count == 0
               && oldState.Esop == null && oldState.Instruments.Count == 0
               && oldState.RaiseState == null && oldState.Recruitment == null
               && oldState.Features.Count == 0 && oldState.BuyoutOffer.Count == 0,
                "every DAG2 field loads at its default");
            Ok(oldState.Budgets.Ads == 500 && oldState.Budgets.Marketing == 0,
                "the old save's marketing budget migrated on load");
            for (int wk = 0; wk < 4; wk++)
            {
                oldState.Week += 1;
                WeeklyReport orep = SimEngine.WeeklyTick(oldState);
                Ok(orep != null && orep.Lines != null,
                    "wk" + S(oldState.Week) + " ticks a pre-wave save without error");
            }
            Ok(!double.IsNaN(oldState.Cash) && Gd.Absi(oldState.Cash) < 100000000,
                "four weeks on, the pre-wave run's cash is still a number ($" + S(oldState.Cash) + ")");
            Ok(oldState.LastPnl != null, "a migrated run writes a full P&L record");

            // ── THE ROUND TRIP: a field the serializer forgets is a field that
            // silently stops persisting. The fixture above proves an OLD save
            // still loads; this proves a NEW one survives being written and read
            // back. Both directions or the save format is only half-checked.
            GameState rt = NewState();
            rt.ServedTotal = 4321;
            rt.OpenRoles = new List<OpenRole> { new OpenRole { Role = "engineer",
                OfferedSalary = 1600, OpenedWeek = 3, Seats = 1 } };
            rt.Applicants = new List<Applicant> { new Applicant { Name = "Ade Okafor",
                Role = "engineer", Skill = 4, Ask = 1750 } };
            rt.Recruiters = 1;
            rt.ContentEquity = 12.5;
            rt.Leads = new List<Lead> { new Lead { Name = "Meridian Foods", Seats = 40,
                Stage = "pilot", Heat = 62 } };
            rt.Logos = new List<Logo> { new Logo { Name = "Harbor Group", Seats = 25,
                SinceWk = 9, RenewalWk = 35 } };
            rt.PipeUnits = 17.25;
            rt.PipeChurnAcc = 0.4;
            rt.PipeStats = new PipeStats { Signed = 2, Lost = 1, SeatsSigned = 65 };
            rt.Loans = new List<Loan> { new Loan { Kind = "bank", Principal = 40000,
                Balance = 33500, RateWk = 0.004, TermWk = 52, TakenWeek = 6,
                PayWk = 820, Missed = 0 } };
            rt.TaxLossCarry = 9100;
            rt.LastRoundAmount = 250000;
            rt.Receivables = new List<Commitment> { new Commitment { Name = "Harbor invoice",
                CashWk = 4000, WeeksLeft = 1 } };
            rt.Bets = new List<Bet> { new Bet { Id = "bet_w7_1", Name = "the mobile app",
                Kind = "reach", Ambition = 2, CostRndWeeks = 6.0, Progress = 2.5,
                Committed = true } };
            rt.PlatformLevel = 2;
            rt.Board = new BoardState { TargetRevenue = 8000, ReviewWeek = 24,
                Strikes = 1, Goodwill = 2 };
            rt.Mna = new MnaOffer { Buyer = "Larkspur Depot", Price = 2400000, ExpiresWeek = 30 };
            rt.MnaLastWeek = 22;
            rt.OptionPoolPct = 10.0;
            rt.FounderBanked = 180000;
            rt.MacroSeason = "winter";
            rt.Hardware = new HardwareState { Stock = 48, CapacityBase = 6.0,
                Equipment = new List<EquipmentItem> { new EquipmentItem {
                    Id = "press_1", Name = "the press", CapacityAdd = 4.0,
                    UpkeepWk = 60.0, BoughtWeek = 8, Site = "site_lyon" } },
                ProductionTarget = 12, ProducedTotal = 310, SubcontractOn = true,
                DemandEma = 9.5 };
            // ── DAG2 W1: every new field populated, plus the site/product tags
            // that ride EXISTING records — a tag the save forgets is a division
            // that silently dissolves on load.
            rt.Sites = new List<Site> { new Site { Id = "site_lyon", Name = "Lyon",
                RentWk = 2600, WageMult = 0.92, LearningCount = 140,
                DemandWeight = 0.35, OpenedWk = 9 } };
            rt.PriceBook = new Dictionary<string, object>
            {
                { "open_site_pack", 18000 }, { "relocation_fee", 400 },
                { "machine_shipping", 900 }, { "lease_break_weeks", 8 },
                { "contract_notice_wks", 4 }, { "refinance_break_fee", 350 },
                { "freelance_rate", 65 }, { "subcontract_rate", 30 },
                { "account_fire_penalty", 1200 },
            };
            rt.Topics = new Dictionary<string, object>
            {
                { "growth_plots", new List<string> { "the garden" } },
                { "works_term", "the studio" },
            };
            rt.SpendBook = new List<SpendLine> { new SpendLine { Name = "staff meals",
                Buys = "the kitchen fed", Amt = 220, Bucket = "office",
                ContractNotice = 0, Division = "" } };
            rt.Esop = new Esop { PoolPct = 10.0, Granted = new List<EsopGrant> {
                new EsopGrant { EmpId = "june_park", Pct = 0.4, VestStartWk = 12 } } };
            rt.Instruments = new List<Instrument> { new Instrument { Kind = "safe",
                Holder = "Fern Capital", Amount = 150000, Cap = 4000000,
                Discount = 0.2, Rate = 0.0, MaturityWk = 0, Pct = 0.0,
                Prefs = 0.0, Protective = false, DragThreshold = 0.0, SignedWk = 9 } };
            rt.RaiseState = new RaiseState { Stages = new List<Dictionary<string, object>>(),
                InterestScore = 22.5, Active = true, FounderTimeTax = 0.15 };
            rt.Recruitment = new Recruitment { Roles = new List<Dictionary<string, object>> {
                new Dictionary<string, object> { { "role", "designer" } } } };
            rt.Features = new List<Feature> { new Feature { Id = "ft_booking",
                Name = "online booking", Job = "pull", Family = "",
                Solidity = "solid", KeepWk = 40, UnitCostAdd = 0.0,
                ProductId = "", BornWk = 1, Measured = 0.0 } };
            rt.BuyoutOffer = new Dictionary<string, object>
            {
                { "buyer", "Larkspur Depot" }, { "cash", 1200000 },
            };
            rt.Employees = new List<Employee> { new Employee { Name = "June Park",
                Role = "engineer", Salary = 1500, Burnout = 10, Quirk = "",
                Skill = 4, HiredWeek = 3, Site = "site_lyon" } };
            rt.Offers = new List<Offer> { new Offer { Name = "the massage",
                Unit = "per session", FairPrice = 80.0, UnitCost = 20.0,
                Elasticity = 2.0, Weight = 1.0, Price = 80.0, PriceSet = true,
                ProductId = "prod_flagship" } };
            // JSON is the wire the real save travels on — round-trip through it,
            // exactly as RunSave does, not through a live object reference that
            // would pass no matter what
            JObject rtDoc = JObject.FromObject(rt);
            // the JSON keys are the GODOT save keys, byte-for-byte — that is
            // what makes the two engines' saves the same format and not two
            // formats that happen to agree today
            Ok(rtDoc["served_total"] != null && rtDoc["open_roles"] != null
               && rtDoc["pipe_units"] != null && rtDoc["tax_loss_carry"] != null
               && rtDoc["platform_level"] != null && rtDoc["option_pool_pct"] != null
               && rtDoc["macro_season"] != null && rtDoc["hardware"] != null,
                "the save dict survives JSON under the Godot key names");
            GameState rt2 = rtDoc.ToObject<GameState>();
            Ok(rt2.ServedTotal == 4321, "served_total persists (the learning curve remembers)");
            Ok(rt2.OpenRoles.Count == 1 && rt2.Applicants.Count == 1 && rt2.Recruiters == 1,
                "the labor market persists");
            Ok(Gd.Absf(rt2.ContentEquity - 12.5) < 0.001, "content equity persists");
            Ok(rt2.Leads.Count == 1 && rt2.Logos.Count == 1
               && Gd.Absf(rt2.PipeUnits - 17.25) < 0.001 && rt2.PipeStats.Signed == 2,
                "the pipeline persists");
            Ok(rt2.Loans.Count == 1 && rt2.Loans[0].Balance == 33500
               && rt2.TaxLossCarry == 9100 && rt2.Receivables.Count == 1,
                "the notes, the carryforward and the receivables persist");
            Ok(rt2.Bets.Count == 1 && rt2.PlatformLevel == 2, "the roadmap persists");
            Ok(rt2.Board.ReviewWeek == 24 && rt2.Mna.Buyer == "Larkspur Depot"
               && rt2.MnaLastWeek == 22 && rt2.FounderBanked == 180000
               && Gd.Absf(rt2.OptionPoolPct - 10.0) < 0.001 && rt2.MacroSeason == "winter",
                "the board, the offer and the banked cash persist");
            Ok(rt2.Hardware.Stock == 48 && rt2.Hardware.ProducedTotal == 310,
                "the factory persists");
            Ok(rt2.Sites.Count == 1 && rt2.Sites[0].RentWk == 2600
               && Convert.ToInt32(rt2.PriceBook["open_site_pack"], CultureInfo.InvariantCulture) == 18000,
                "the sites and the price book persist");
            Ok(S(rt2.Topics["works_term"]) == "the studio"
               && rt2.SpendBook.Count == 1 && rt2.SpendBook[0].Amt == 220,
                "the generated books persist (topics, spend book)");
            Ok(Gd.Absf(rt2.Esop.PoolPct - 10.0) < 0.001
               && rt2.Instruments.Count == 1 && rt2.Instruments[0].Cap == 4000000
               && Gd.Absf(rt2.RaiseState.InterestScore - 22.5) < 0.001
               && rt2.Recruitment.Roles.Count == 1,
                "the ownership cluster persists (pool, paper, raise, recruitment)");
            Ok(rt2.Features.Count == 1 && rt2.Features[0].KeepWk == 40
               && S(rt2.BuyoutOffer["buyer"]) == "Larkspur Depot",
                "the feature inventory and the buyout offer persist");
            Ok(rt2.Employees[0].Site == "site_lyon"
               && rt2.Hardware.Equipment[0].Site == "site_lyon"
               && rt2.Offers[0].ProductId == "prod_flagship",
                "the site tags and the product id persist on their records");
            // and the saved run still ticks — a round-tripped state is a LIVE state
            rt2.Week += 1;
            WeeklyReport rtRep = SimEngine.WeeklyTick(rt2);
            Ok(rtRep != null && rtRep.Lines != null, "a round-tripped run ticks without error");

            // ── THE LANES: each suite runs its own pins after the engine's
            Action<bool, string> ok = Ok;
            CatalogTests.Run(ok);
            LaborTests.Run(ok);
            StreetTests.Run(ok);
            FunnelTests.Run(ok);
            PipelineTests.Run(ok);
            BankTests.Run(ok);
            RoadmapTests.Run(ok);
            BoardTests.Run(ok);
            FactoryTests.Run(ok);
            DivisionsTests.Run(ok);
            OwnershipTests.Run(ok);
            FeaturesTests.Run(ok);
            WorksTests.Run(ok);
        }

        /// <summary>
        /// The frozen save fixture, found the same way StreamingAssets is: walk
        /// up from the binary and from the working directory until it turns up,
        /// so the suite runs from anywhere.
        /// </summary>
        private static string ReadFixture(string name)
        {
            var probes = new List<string>();
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                probes.Add(Path.Combine(dir.FullName, "Fixtures", name));
                dir = dir.Parent;
            }
            var cwd = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (cwd != null)
            {
                probes.Add(Path.Combine(cwd.FullName, "Fixtures", name));
                probes.Add(Path.Combine(cwd.FullName, "unity", "Runway.Core.Tests", "Fixtures", name));
                cwd = cwd.Parent;
            }
            foreach (string p in probes)
                if (File.Exists(p)) return File.ReadAllText(p);
            return "";
        }

        static string S2(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

        // ═══════════════════════════ THE BALANCE HARNESS ═════════════════════════
        /// <summary>
        /// Scripted founder strategies through the REAL engine for 50 weeks. No LLM
        /// — strategies apply the same lever/status moves the DM would, so this
        /// calibrates the ECONOMY, not the prose.
        /// </summary>
        private const int WEEKS = 50;

        private static List<KeyValuePair<string, Action<GameState, int>>> Strategies()
        {
            return new List<KeyValuePair<string, Action<GameState, int>>>
            {
                new KeyValuePair<string, Action<GameState, int>>("idle", (s, w) =>
                {
                }),
                new KeyValuePair<string, Action<GameState, int>>("builder", (s, w) =>
                {
                    if (w == 6)
                    {
                        s.SetFlag("launched");
                        s.Product = Gd.Maxi(s.Product, 45);
                    }
                    s.Product = Gd.Mini(s.Product + 3, 100);
                    s.TechDebt = Gd.Maxf(s.TechDebt - 1.0, 0.0);
                    if (w % 8 == 0)
                    {
                        SimEngine.AddStatus(s, "founder_flow", 2);
                    }
                }),
                new KeyValuePair<string, Action<GameState, int>>("seller", (s, w) =>
                {
                    if (w == 4)
                    {
                        s.SetFlag("launched");
                    }
                    s.Product = Gd.Mini(s.Product + 1, 100);
                    s.MarketingBudget = s.Cash > 10000 ? 400 : 0;
                    if (w % 6 == 0)
                    {
                        SimEngine.AddStatus(s, "word_of_mouth", 2);
                    }
                    s.Traction += 2;     // direct founder sales, the written-move analog
                }),
                new KeyValuePair<string, Action<GameState, int>>("balanced", (s, w) =>
                {
                    if (w == 5)
                    {
                        s.SetFlag("launched");
                    }
                    s.Product = Gd.Mini(s.Product + 2, 100);
                    if (w > 8)
                    {
                        s.MarketingBudget = 300;
                    }
                    if (w == 12 && s.Traction >= 20)
                    {
                        List<FundingOffer> offers = SimEngine.GenerateOffers(s, s.Investors);
                        if (offers.Count > 0)
                        {
                            FundingOffer o = offers[0];
                            SimEngine.ApplyRound(s, o.Amount, o.EquityPct);
                            s.SetFlag("seed_raised");
                        }
                    }
                }),
                new KeyValuePair<string, Action<GameState, int>>("reckless", (s, w) =>
                {
                    if (w == 3)
                    {
                        s.SetFlag("launched");
                    }
                    SimEngine.AddStatus(s, "crunch", 2);
                    s.Product = Gd.Mini(s.Product + 4, 100);
                    s.TechDebt = Gd.Minf(s.TechDebt + 3.0, 100.0);
                    s.MarketingBudget = 800;
                    if (s.Cash < 2000 && s.LoanPrincipal == 0)
                    {
                        s.LoanPrincipal = 15000;
                        s.Cash += 15000;
                    }
                }),
            };
        }

        private static void RunBalanceSim()
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-9} {1,6} {2,8} {3,6} {4,6} {5,5} {6,5} {7,4} {8,6}",
                "strategy", "week", "cash", "cust", "morale", "prod", "debt", "exh", "state"));
            foreach (KeyValuePair<string, Action<GameState, int>> kv in Strategies())
            {
                var s = new GameState();
                s.SimSeed = 1234;
                s.Cash = 25000;
                s.Traction = 0;
                s.Product = 20;
                s.BizWhat = "Software";
                s.BizWho = "SMB";
                s.Theta = SimEngine.DefaultTheta(s.BizWhat, s.BizWho);
                s.Investors = new List<Investor> { new Investor { Name = "Fund A", Thesis = "momentum" } };
                int diedAt = 0;
                for (int w = 1; w <= WEEKS; w++)
                {
                    s.Week = w;
                    kv.Value(s, w);
                    SimEngine.WeeklyTick(s);
                    if (s.Cash < -5000 && diedAt == 0)
                    {
                        diedAt = w;
                    }
                    if (w == 10 || w == 25 || w == 50)
                    {
                        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                            "{0,-9} {1,6} {2,8} {3,6} {4,6} {5,5} {6,5} {7,4} {8,6}",
                            kv.Key, w, s.Cash, s.Traction, s.Morale, s.Product,
                            Gd.ToInt(s.TechDebt), s.Exhaustion,
                            diedAt > 0 ? "DEAD@" + S(diedAt) : "alive"));
                    }
                }
            }
            Console.WriteLine("BALANCE SIM DONE");
        }
    }
}
