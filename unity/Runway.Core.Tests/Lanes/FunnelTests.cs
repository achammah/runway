using System;
using System.Collections.Generic;
using Runway.Core;

namespace Runway.CoreTests
{
    /// <summary>
    /// LANE SUITE — funnel. Spec: docs/design/04-funnel-channels.md (section 8 twin test pins).
    ///
    /// Program.cs calls Run after the engine's own checks and hands over `ok`, the
    /// same assert the whole suite uses: ok(condition, "what this pins").
    ///
    /// The porting law: a check lands FIRST in game/tests/lanes/test_funnel.gd,
    /// then here in the same order. Same checks, same order, same logic — the two
    /// engines do not share PRNG internals, so never pin a draw across them, only
    /// behaviour.
    /// </summary>
    public static class FunnelTests
    {
        /// <summary>The fixture every pin starts from: office era (reach x1.00,
        /// full attribution), launched, no rivals — so a channel number is the
        /// CHANNEL's, not the weather's.</summary>
        static GameState St(string who, int product = 50)
        {
            var s = new GameState
            {
                SimSeed = 42, Week = 5, Era = "office", Cash = 500000,
                Product = product, Morale = 70, Hype = 40,
                BizWhat = "Software", BizWho = who,
            };
            s.Rivals = new List<Rival>();
            s.SetFlag("launched");
            s.Theta = SimEngine.DefaultTheta(s.BizWhat, s.BizWho);
            return s;
        }

        /// <summary>CAPACITY OUT OF THE WAY. Comparing two channels means comparing
        /// what they BROUGHT, so the closing ceiling must never be the thing that
        /// answers: a sell-5 founder, two closers and a real sales budget.</summary>
        static GameState Capacity(GameState s)
        {
            s.Competences["sell"] = 5;
            s.Employees.Add(new Employee { Name = "Rhea", Role = "sales", Salary = 1200, Burnout = 10 });
            s.Employees.Add(new Employee { Name = "Otto", Role = "sales", Salary = 1200, Burnout = 10 });
            s.Budgets.Sales = 20000;
            s.Cash = 5000000;
            return s;
        }

        static double Signed(GameState s, string key)
        {
            return SimFunnel.Num(SimFunnel.Funnel(s), "signed_" + key);
        }

        public static void Run(Action<bool, string> ok)
        {
            // ── PIN 1: baseline, determinism, conservation ────────────────────
            // Nothing funded must leave the world exactly as it was: the seam
            // hands the spine back its own blended value at $0, which is 1.0.
            GameState baseSt = St("SMB");
            baseSt.Traction = 300;
            SimFunnel.TickPre(baseSt, new WeeklyReport());
            ok(Gd.Absf(SimFunnel.ReachMult(baseSt, 0.0, 1.0) - 1.0) < 1e-9,
                "zero channel spend hands the spine back its own reach lever (x1.00)");

            GameState a = St("SMB");
            a.Traction = 300;
            SimEngine.WeeklyTick(a);
            Dictionary<string, double> fa = SimFunnel.Funnel(a);
            ok(SimFunnel.Num(fa, "spend_total") == 0.0
               && SimFunnel.Num(fa, "reach_total") == 0.0
               && SimFunnel.Num(fa, "leads_total") == 0.0
               && SimFunnel.Num(fa, "signed_ads") == 0.0
               && SimFunnel.Num(fa, "signed_referrals") == 0.0
               && a.ContentEquity == 0.0,
                "an unfunded funnel reads zero — no reach, no leads, no equity");

            GameState b = St("SMB");
            b.Traction = 300;
            SimEngine.WeeklyTick(b);
            ok(a.Cash == b.Cash && a.Traction == b.Traction
               && Gd.Absf(a.ContentEquity - b.ContentEquity) < 1e-12,
                "two identical states tick to identical cash, traction and content equity");

            // every arrival is assigned to exactly one source, parts sum to adds
            GameState cons = Capacity(St("Consumer"));
            cons.Traction = 1000;
            cons.Budgets.Ads = 2000;
            cons.Budgets.Content = 1000;
            cons.Budgets.Referrals = 2000;
            cons.Budgets.Outbound = 500;
            cons.Budgets.Care = 1000;
            SimEngine.WeeklyTick(cons);
            Dictionary<string, double> fc = SimFunnel.Funnel(cons);
            double sumAll = SimFunnel.Num(fc, "organic") + SimFunnel.Num(fc, "wom");
            for (int i = 0; i < SimFunnel.Mix.Length; i++)
                sumAll += SimFunnel.Num(fc, "signed_" + SimFunnel.Mix[i]);
            ok(Gd.Absf(sumAll - SimFunnel.Num(fc, "adds")) < 1e-6,
                string.Format("attribution is exact: organic + word of mouth + sum(channels) == adds ({0:0.000000} vs {1:0.000000})",
                    sumAll, SimFunnel.Num(fc, "adds")));

            // ── PIN 2: ads instant, content week-1 weak, the garage discount ──
            GameState sAds = Capacity(St("SMB"));
            sAds.Traction = 300;
            sAds.Budgets.Ads = 2000;
            SimEngine.WeeklyTick(sAds);
            GameState sCon = Capacity(St("SMB"));
            sCon.Traction = 300;
            sCon.Budgets.Content = 2000;
            SimEngine.WeeklyTick(sCon);
            double attAds = Signed(sAds, "ads");
            double attCon = Signed(sCon, "content");
            ok(attAds >= 3.0 * attCon && attAds > 5.0,
                string.Format("week 1, $2k each: ads out-signs content 3:1 ({0:0.0} vs {1:0.0}) — the instant channel",
                    attAds, attCon));

            GameState sGar = Capacity(St("SMB"));
            sGar.Era = "garage";
            sGar.Traction = 300;
            sGar.Budgets.Ads = 2000;
            SimEngine.WeeklyTick(sGar);
            ok(Signed(sGar, "ads") < 0.4 * attAds,
                string.Format("the garage discount: the same $2k of ads buys x0.35 the reach ({0:0.0} vs {1:0.0})",
                    Signed(sGar, "ads"), attAds));

            // ── PIN 3: content beats ads over 12 weeks at equal total spend ───
            GameState armA = Capacity(St("SMB"));
            armA.Traction = 300;
            armA.Budgets.Ads = 2000;
            GameState armC = Capacity(St("SMB"));
            armC.Traction = 300;
            armC.Budgets.Content = 2000;
            double cumA = 0.0, cumC = 0.0, lastA = 0.0, lastC = 0.0;
            for (int i = 0; i < 12; i++)
            {
                armA.Week += 1;
                armC.Week += 1;
                SimEngine.WeeklyTick(armA);
                SimEngine.WeeklyTick(armC);
                lastA = Signed(armA, "ads");
                lastC = Signed(armC, "content");
                cumA += lastA;
                cumC += lastC;
            }
            ok(cumC > cumA,
                string.Format("12 weeks at $2k/wk: the library out-signs the auction ({0:0} vs {1:0})", cumC, cumA));
            ok(lastC >= 1.5 * lastA,
                string.Format("and by week 12 content's weekly rate is 1.5x ads' ({0:0.0} vs {1:0.0})", lastC, lastA));
            ok(armC.ContentEquity > 0.5 && armA.ContentEquity == 0.0,
                string.Format("the funded library compounded to {0}% equity; the unfunded one has none",
                    Gd.RoundToInt(armC.ContentEquity * 100.0)));

            // ── PIN 4: referrals need a product worth vouching for ────────────
            GameState bad = Capacity(St("Consumer", 10));
            bad.Traction = 1000;
            bad.Budgets.Referrals = 2000;
            bad.Budgets.Care = 1000;
            SimEngine.WeeklyTick(bad);
            GameState good = Capacity(St("Consumer", 80));
            good.Traction = 1000;
            good.Budgets.Referrals = 2000;
            good.Budgets.Care = 1000;
            SimEngine.WeeklyTick(good);
            double attBad = Signed(bad, "referrals");
            double attGood = Signed(good, "referrals");
            ok(attBad == 0.0,
                "a v0.10 product has detractors, not promoters — the referral program signs nobody");
            ok(attGood > 5.0 && attGood >= 10.0 * attBad,
                string.Format("at v0.80 with care funded the same $2k amplifies word of mouth ({0:0.0}/wk)", attGood));

            // ── PIN 5: outbound is Enterprise's channel, not Consumer's ───────
            GameState ent = St("Enterprise");
            ent.Traction = 20;
            ent.Budgets.Ads = 2000;
            ent.Budgets.Outbound = 2000;
            SimEngine.WeeklyTick(ent);
            ok(Signed(ent, "outbound") > Signed(ent, "ads"),
                string.Format("Enterprise: cold touch out-signs bought reach ({0:0.00} vs {1:0.00})",
                    Signed(ent, "outbound"), Signed(ent, "ads")));

            GameState capOff = St("Enterprise");
            capOff.Traction = 20;
            GameState capOn = St("Enterprise");
            capOn.Traction = 20;
            capOn.Budgets.Outbound = 2000;
            ok(SimFunnel.GtmCap(capOn) > SimFunnel.GtmCap(capOff) + 5.0,
                string.Format("outbound money is closing capacity too: cap {0:0.0} → {1:0.0}",
                    SimFunnel.GtmCap(capOff), SimFunnel.GtmCap(capOn)));

            GameState conOb = Capacity(St("Consumer"));
            conOb.Traction = 1000;
            conOb.Budgets.Ads = 2000;
            conOb.Budgets.Outbound = 2000;
            SimEngine.WeeklyTick(conOb);
            ok(Signed(conOb, "outbound") < 0.2 * Signed(conOb, "ads"),
                string.Format("Consumer: nobody answers a cold call ({0:0.00} vs {1:0.00} from ads)",
                    Signed(conOb, "outbound"), Signed(conOb, "ads")));

            // ── PIN 6: migration and the DM's one marketing category ──────────
            GameState old = St("SMB");
            old.Traction = 300;
            old.Budgets = new Budgets { Marketing = 2000 };
            SimEngine.WeeklyTick(old);
            ok(old.Budgets.Ads == 2000 && old.Budgets.Marketing == 0
               && old.LastPnl != null && old.LastPnl.Marketing == 2000,
                "a legacy `marketing` budget becomes paid ads and still books as marketing spend");

            // the legacy set_marketing op's own field folds into the SAME ads lane
            GameState byLever = Capacity(St("SMB"));
            byLever.Traction = 300;
            byLever.Budgets.Ads = 2000;
            SimEngine.WeeklyTick(byLever);
            GameState byOp = Capacity(St("SMB"));
            byOp.Traction = 300;
            byOp.MarketingBudget = 2000;
            SimEngine.WeeklyTick(byOp);
            ok(Gd.Absf(SimFunnel.Num(SimFunnel.Funnel(byLever), "spend_ads")
                       - SimFunnel.Num(SimFunnel.Funnel(byOp), "spend_ads")) < 1e-9
               && Gd.Absf(Signed(byLever, "ads") - Signed(byOp, "ads")) < 1e-9,
                "the legacy marketing op buys exactly what the ads lever buys");

            GameState emptyMix = St("SMB");
            SimFunnel.SetMarketing(emptyMix, 2000);
            ok(emptyMix.Budgets.Ads == 2000 && emptyMix.Budgets.Content == 0
               && emptyMix.Budgets.Referrals == 0 && emptyMix.Budgets.Outbound == 0
               && emptyMix.MarketingBudget == 0,
                "the DM's `marketing` on a cold start funds ads — the only channel that pays in week one");

            GameState curated = St("SMB");
            curated.Budgets.Ads = 500;
            curated.Budgets.Content = 1500;
            SimFunnel.SetMarketing(curated, 2000);
            ok(curated.Budgets.Ads == 500 && curated.Budgets.Content == 1500
               && curated.Budgets.Referrals == 0 && curated.Budgets.Outbound == 0,
                "and on a curated mix it splits by that mix — the narrator never overwrites it");
        }
    }
}
