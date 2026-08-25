using System;
using System.Collections.Generic;
using System.Globalization;

namespace Runway.Core
{
    /// <summary>
    /// LANE 04 — THE FUNNEL (four acquisition channels). Spec: docs/design/04-funnel-channels.md
    ///
    /// The old path multiplied organic adoption by one blended reach lever and
    /// hid the funnel. This lane makes the funnel the actual computation:
    ///
    ///   REACH (bought: ads + content + outbound) --,
    ///                                              +--&gt; LEADS (x conv) -&gt; SIGNED
    ///   WALK-INS (organic + word of mouth, --------'      = min(demand, capacity)
    ///     word of mouth amplified by referrals)
    ///
    /// Four real growth dynamics, each named by its real name:
    ///   ads       auction-bought reach — instant, concave in spend, CAC inflates
    ///   content   a STOCK (ContentEquity) that compounds while funded and rots when starved
    ///   referrals promoters amplify word of mouth; below an NPS bar there are none
    ///   outbound  quota math — buys reach AND closing capacity, priced by audience
    ///
    /// Every number is engine arithmetic on state. NO new RNG salts (attribution
    /// is exact division, never a die) and NO LLM calls (spec section 7): the DM
    /// narrates the mix for free through Directives.
    ///
    /// HOW IT REACHES THE ENGINE. The spine owns the weekly tick; this lane owns
    /// three hooks and one seam:
    ///   TickPre    settles the content stock, then computes the WHOLE week's
    ///              funnel and parks it on the state as the `funnel` read-out
    ///   ReachMult  hands the spine the one multiplier that makes its own
    ///              adoption line produce exactly the funnel's number (see Plan)
    ///   TickPost   reconciles the attribution against what actually landed and
    ///              writes the receipts that teach
    /// The invariant a pin asserts every week:
    ///   organic + word of mouth + sum(channels) == adds
    ///
    /// TWIN LAW: this file and game/src/core/lanes/sim_funnel.gd carry the same
    /// logic in the same order. The engines do NOT share PRNG internals, so
    /// parity means same checks and same behaviour, never a byte-equal draw.
    /// </summary>
    public static class SimFunnel
    {
        /// <summary>
        /// THE CHANNEL TABLE (spec section 1.5), per audience, exact. AdsA/ConA
        /// are the reach/week a channel can buy at its ceiling; AdsK scales the
        /// world's own knee (theta.cac_sat) into an ads saturation point; RefA is
        /// the loop's amplitude; ObAud is who answers a cold touch; Conv is the
        /// base lead conversion for all bought reach.
        /// </summary>
        public sealed class Channel
        {
            public double AdsA, AdsK, ConA, ConSat, RefA, RefSat, ObAud, Conv;
        }

        static readonly Dictionary<string, Channel> Channels = new Dictionary<string, Channel>
        {
            { "Consumer", new Channel { AdsA = 2400.0, AdsK = 0.30, ConA = 1600.0, ConSat = 1600.0,
                                        RefA = 2.6, RefSat = 900.0, ObAud = 0.15, Conv = 0.030 } },
            { "SMB", new Channel { AdsA = 320.0, AdsK = 0.40, ConA = 520.0, ConSat = 1600.0,
                                   RefA = 1.8, RefSat = 1200.0, ObAud = 1.0, Conv = 0.080 } },
            { "Enterprise", new Channel { AdsA = 20.0, AdsK = 0.65, ConA = 30.0, ConSat = 2200.0,
                                          RefA = 1.2, RefSat = 1500.0, ObAud = 2.5, Conv = 0.060 } },
        };

        /// <summary>The library ramps 12.5%/wk toward the level its funding supports
        /// (~80% of target in 12 weeks) and decays 7%/wk unfunded (half-life ~9.6 wks).</summary>
        public const double ConRamp = 0.125;
        public const double ConDecay = 0.93;
        /// <summary>Lists and sequences scale linearly with budget — there is always another list.</summary>
        public const double ObReachPerK = 5.0;
        /// <summary>THE ERA LADDER (spec 6.3). A garage has no brand and no pixel
        /// history, so paid reach lands at a third; a name on the door opens doors.</summary>
        static readonly Dictionary<string, double> EraReachEff = new Dictionary<string, double>
        {
            { "garage", 0.35 }, { "coworking", 0.7 }, { "office", 1.0 },
            { "floor", 1.1 }, { "hq", 1.25 },
        };
        /// <summary>Full attribution is an office-era capability: a garage cannot buy a data stack.</summary>
        static readonly Dictionary<string, int> EraAnCap = new Dictionary<string, int>
        {
            { "garage", 1 }, { "coworking", 2 }, { "office", 3 }, { "floor", 3 }, { "hq", 3 },
        };
        /// <summary>Channel teams amplify what money buys, capped so a department is not a cheat.</summary>
        public const double TeamPerHead = 0.12;
        public const int TeamHeadsMax = 5;

        /// <summary>The four acquisition lanes, in the ONE order every reader walks them.</summary>
        public static readonly string[] Mix = { "ads", "content", "referrals", "outbound" };
        /// <summary>A channel this well funded that signs nobody is burning money, not learning.</summary>
        public const double BurnSpend = 500.0;
        public const double BurnSigned = 0.05;
        /// <summary>Below this the product has detractors, not promoters — a
        /// referral program buys silence (spec 1.3).</summary>
        public const double HappyFloor = 0.1;

        // ═════════════════════ THE READ HELPERS ══════════════════════════════
        // Everything the ledger, the customers desk and the pins ask this lane.
        // Pure functions of state: no side effects, safe to call from a redraw.

        /// <summary>The channel constants for this run's audience.</summary>
        public static Channel Of(GameState state)
        {
            Channel c;
            return Channels.TryGetValue(state.BizWho ?? "", out c) ? c : Channels["SMB"];
        }

        /// <summary>One channel's weekly dollars. The legacy set_marketing op's
        /// budget folds into ADS exactly as it folded into the old blended lever.</summary>
        public static double SpendOf(GameState state, string key)
        {
            Budgets b = state.Budgets ?? new Budgets();
            switch (key)
            {
                case "ads": return b.Marketing + b.Ads + state.MarketingBudget;
                case "content": return b.Content;
                case "referrals": return b.Referrals;
                case "outbound": return b.Outbound;
            }
            return 0.0;
        }

        /// <summary>What acquisition costs this week, all four lanes — the P&amp;L's marketing sum.</summary>
        public static double SpendTotal(GameState state)
        {
            double t = 0.0;
            for (int i = 0; i < Mix.Length; i++) t += SpendOf(state, Mix[i]);
            return t;
        }

        public static double EraEff(GameState state)
        {
            double e;
            return EraReachEff.TryGetValue(state.Era ?? "", out e) ? e : 1.0;
        }

        /// <summary>EFFECTIVE ANALYTICS: what the founder can actually see, level clamped by era.</summary>
        public static int Analytics(GameState state)
        {
            int cap;
            if (!EraAnCap.TryGetValue(state.Era ?? "", out cap)) cap = 3;
            return Gd.Mini(state.AnalyticsLevel, cap);
        }

        public static int MkHeads(GameState state)
        {
            int n = 0;
            for (int i = 0; i < state.Employees.Count; i++)
                if ((state.Employees[i].Role ?? "").Contains("marketing")) n += 1;
            return n;
        }

        /// <summary>+12% per marketing head, five heads deep. Live at every era
        /// (0 heads = x1); salaries and era spend caps make it a floor/hq play.</summary>
        public static double TeamMult(GameState state)
        {
            return 1.0 + TeamPerHead * Gd.Mini(MkHeads(state), TeamHeadsMax);
        }

        /// <summary>The very term care already uses on churn, reused here: money
        /// answering the phone is half of whether anyone would vouch for you.</summary>
        public static double CareSoft(GameState state)
        {
            return 1.0 - Math.Exp(-(state.Budgets != null ? state.Budgets.Care : 0) / 1500.0);
        }

        /// <summary>THE NPS GATE. Below v0.25 there are no promoters at all, and a
        /// paid referral program amplifies exactly that silence.</summary>
        public static double Happy(GameState state)
        {
            return Math.Pow(Gd.Maxf((state.Product - 25.0) / 75.0, 0.0), 1.2)
                   * (0.5 + 0.5 * CareSoft(state));
        }

        /// <summary>What the referral program multiplies word of mouth BY (0 = nothing changes).</summary>
        public static double RefGain(GameState state)
        {
            Channel ch = Of(state);
            double b = SpendOf(state, "referrals");
            return ch.RefA * (1.0 - Math.Exp(-b / ch.RefSat)) * Happy(state) * TeamMult(state);
        }

        /// <summary>The auction: the first dollars buy the cheap, well-targeted
        /// audience, and pushing spend climbs the bid landscape. Concave, so CAC
        /// rises on its own.</summary>
        public static double AdsSat(GameState state)
        {
            double sat = state.Theta != null ? state.Theta.CacSat : 8000.0;
            return Gd.Maxf(sat * Of(state).AdsK, 1.0);
        }

        public static double ReachAds(GameState state)
        {
            return Of(state).AdsA * (1.0 - Math.Exp(-SpendOf(state, "ads") / AdsSat(state)))
                   * EraEff(state) * TeamMult(state);
        }

        /// <summary>The library pays at ~zero marginal cost from the stock it has
        /// TODAY. Pass an explicit equity to read a level the state has not reached yet.</summary>
        public static double ReachContent(GameState state, double equity = -1.0)
        {
            double c = equity < 0.0 ? state.ContentEquity : equity;
            return Of(state).ConA * c * EraEff(state) * TeamMult(state);
        }

        /// <summary>The level a given weekly spend funds — the ceiling the ramp climbs toward.</summary>
        public static double ContentTarget(GameState state, double budget = -1.0)
        {
            double b = budget < 0.0 ? SpendOf(state, "content") : budget;
            return 1.0 - Math.Exp(-b / Of(state).ConSat);
        }

        /// <summary>Cold touch is era-neutral: a founder with a list works the same in a garage.</summary>
        public static double ReachOutbound(GameState state)
        {
            return ObReachPerK * SpendOf(state, "outbound") / 1000.0 * Of(state).ObAud;
        }

        /// <summary>Outbound money is also buying an SDR-hour equivalent — closing, not just reach.</summary>
        public static double ObClosers(GameState state)
        {
            return SpendOf(state, "outbound") / 600.0 * Of(state).ObAud;
        }

        // ── THE CAPACITY SEAM ────────────────────────────────────────────────
        /// <summary>
        /// HOW MUCH CLOSING DID ACQUISITION MONEY BUY? Not every dollar buys any.
        /// Ads pull inbound onto the founder's calendar — that is the old blended
        /// lever's own /400 slot, inherited here exactly, so a migrated save keeps
        /// its ceiling. Outbound money IS an SDR hour and buys closing directly,
        /// priced by who answers a cold touch. Content and referral dollars buy
        /// NONE: a library and a promoter make demand, not a person to sign it.
        /// `mkBudget` is the spine's blended total, which this lane no longer needs.
        /// </summary>
        public static double CapReach(GameState state, double mkBudget)
        {
            return SpendOf(state, "ads") / 400.0 + ObClosers(state);
        }

        /// <summary>
        /// THE WEEKLY CEILING, mirrored from the spine's own clamp term for term
        /// so the funnel and the tick can never disagree about what closing
        /// capacity is: founder sell-stat, the sales roster (the labor lane prices
        /// its own people), the sales budget and CapReach above — all scaled by
        /// audience.
        /// </summary>
        public static double GtmCap(GameState state)
        {
            int salesHeads = 0;
            for (int i = 0; i < state.Employees.Count; i++)
                if ((state.Employees[i].Role ?? "").Contains("sales")) salesHeads += 1;
            double capScale = state.BizWho == "SMB" ? 3.0 : (state.BizWho == "Consumer" ? 40.0 : 1.0);
            double bSales = state.Budgets != null ? state.Budgets.Sales : 0;
            return (1.5 + 0.8 * state.Competence("sell")
                    + SimLabor.SalesCapacity(state, 3.0 * salesHeads)
                    + CapReach(state, 0.0) + SimRoadmap.GtmCapBonus(state)
                    + bSales / 600.0) * capScale;
        }

        /// <summary>
        /// LAST WEEK'S FUNNEL, whole — the flat read-out the customers desk draws
        /// from. Empty before the first tick, which is a real state the desk must
        /// print rather than a case it may assume away. Fresh from the tick the
        /// meta is a Dictionary; off disk Newtonsoft hands over a JObject, so this
        /// reads both (the UnitEcon reader pattern).
        /// </summary>
        public static Dictionary<string, double> Funnel(GameState state)
        {
            return Read(state, "funnel");
        }

        /// <summary>The week before it, for the receipts that need a direction (CAC rising).</summary>
        public static Dictionary<string, double> FunnelPrev(GameState state)
        {
            return Read(state, "funnel_prev");
        }

        static Dictionary<string, double> Read(GameState state, string key)
        {
            object box = state.GetMeta(key, null);
            var flat = box as Dictionary<string, double>;
            if (flat != null) return flat;
            var jo = box as Newtonsoft.Json.Linq.JObject;
            var outp = new Dictionary<string, double>();
            if (jo != null)
            {
                foreach (var p in jo)
                {
                    double d;
                    if (double.TryParse((p.Value ?? "").ToString(), NumberStyles.Any,
                                        CultureInfo.InvariantCulture, out d))
                        outp[p.Key] = d;
                }
            }
            return outp;
        }

        public static double Num(Dictionary<string, double> f, string key, double dflt = 0.0)
        {
            double v;
            return f != null && f.TryGetValue(key, out v) ? v : dflt;
        }

        /// <summary>
        /// WHAT THIS MONEY IS DOING RIGHT NOW, in the engine's own formula — the
        /// string the ledger prints beside each channel row (house law: mechanics
        /// visible at the point of decision).
        /// </summary>
        public static string LeverEffect(GameState state, string cat)
        {
            switch (cat)
            {
                case "ads":
                {
                    if (SpendOf(state, "ads") <= 0.0) return "no reach bought";
                    string eraNote = state.Era == "garage" || state.Era == "coworking"
                        ? string.Format(CultureInfo.InvariantCulture, " (era x{0:0.00})", EraEff(state))
                        : "";
                    return string.Format(CultureInfo.InvariantCulture, "reach ~{0}/wk{1}",
                        Gd.RoundToInt(ReachAds(state)), eraNote);
                }
                case "content":
                {
                    double c = state.ContentEquity;
                    if (SpendOf(state, "content") <= 0.0)
                        return c >= 0.005 ? "fading −7%/wk" : "nothing written yet";
                    return string.Format(CultureInfo.InvariantCulture, "equity {0}% → ~{1}/wk",
                        Gd.RoundToInt(c * 100.0), Gd.RoundToInt(ReachContent(state)));
                }
                case "referrals":
                    if (SpendOf(state, "referrals") <= 0.0) return "no program";
                    if (Happy(state) < HappyFloor)
                        return "nobody would vouch yet (v0." + state.Product.ToString(CultureInfo.InvariantCulture) + ")";
                    return string.Format(CultureInfo.InvariantCulture, "word of mouth x{0:0.00}",
                        1.0 + RefGain(state));
                case "outbound":
                    if (SpendOf(state, "outbound") <= 0.0) return "no lists worked";
                    return string.Format(CultureInfo.InvariantCulture, "+{0} reach · +{1:0.0} closing",
                        Gd.RoundToInt(ReachOutbound(state)), ObClosers(state));
            }
            return "";
        }

        // ═════════════════════ THE WEEK'S FUNNEL ═════════════════════════════
        /// <summary>
        /// THE WHOLE COMPUTATION, once, from settled state. Called from TickPre —
        /// after rivals, macro, quality and the content stock have all moved, and
        /// before the spine reads adoption.
        ///
        /// Returns a FLAT map of numbers (no nesting): the same shape both engines
        /// write and both desks read, so a twin can never drift on a key.
        /// </summary>
        static Dictionary<string, double> Plan(GameState state)
        {
            Theta th = state.Theta ?? new Theta();
            Channel ch = Of(state);
            double a = state.Traction;
            double n = Gd.Maxf(th.Tam, 1.0);
            double p = Gd.Maxf(n - a, 0.0);
            bool launched = state.HasFlag("launched");
            double launchF = launched ? 1.0 : 0.0;
            double qualityGate = 0.2 + state.Product / 100.0 * 0.8;
            double hypeMult = 0.6 + state.Hype / 100.0 * 0.9;
            // the same three world terms the spine's own adoption line reads,
            // read the same way — statuses, the settled rival board, the shelf
            double statusAdopt = 1.0;
            for (int i = 0; i < state.Statuses.Count; i++)
                statusAdopt *= SimEngine.StatusEffect(state.Statuses[i].Name).AdoptMult;
            double pressure = 0.0;
            for (int i = 0; i < state.Rivals.Count; i++) pressure += state.Rivals[i].Strength;
            pressure = Gd.Minf(pressure / Gd.Maxf(state.Rivals.Count, 1.0) / 100.0 * 0.5, 0.45);
            double pd = Math.Pow(Gd.Maxf(state.PriceMult, 0.1), -1.5);
            double om = SimEngine.OffersDemandMult(state);
            if (om >= 0.0) pd = om;
            pd = Gd.Clampf(pd, 0.1, 3.0);

            // ── REACH: three bought sources (sections 1.1-1.4) ───────────────
            double bAds = SpendOf(state, "ads");
            double bCon = SpendOf(state, "content");
            double bRef = SpendOf(state, "referrals");
            double bOb = SpendOf(state, "outbound");
            double rAds = ReachAds(state);
            double rCon = ReachContent(state);
            double rOb = ReachOutbound(state);

            // ── LEADS: ONE conversion gate for all bought reach, out of the same
            // terms the organic path uses, plus pool exhaustion and the launch gate
            double avail = p / n;
            double conv = ch.Conv * qualityGate * statusAdopt * state.MarketTrend
                          * (1.0 - pressure) * avail * launchF;
            double lAds = rAds * conv;
            double lCon = rCon * conv;
            double lOb = rOb * conv;
            double leadsPaid = lAds + lCon + lOb;

            // ── WALK-INS: the untouched organic pipeline. organicBase is the
            // spine's own pEff with the reach lever taken out; referrals amplify
            // word of mouth.
            double organicBase = th.AdoptP * hypeMult * statusAdopt * state.MarketTrend
                                 * (1.0 - pressure) * qualityGate * launchF * p;
            double womBase = th.AdoptIc * a * p / n * statusAdopt
                             * (1.0 - pressure) * qualityGate * (launched ? 1.0 : 0.5);
            double gain = RefGain(state);
            // THE ONE THING THE SEAM CANNOT CARRY: the lift rides the spine's
            // organic term, and that term is zero before launch (nothing sells
            // itself yet). A pre-launch referral program buys nothing, and the
            // read-out says nothing.
            bool deliverable = organicBase > 0.0;
            double lift = deliverable ? gain : 0.0;

            // ── SIGNED: price, then the capacity ceiling ─────────────────────
            double organic = organicBase * pd;
            double womAll = womBase * (1.0 + lift) * pd;
            double demand = organic + womAll + leadsPaid * pd;
            // ONE CEILING. GtmCap mirrors the spine's own clamp term for term (its
            // reach half IS this lane's CapReach), so the funnel reads the same
            // ceiling the tick will apply and the clamp lands exactly once — and
            // where the pipeline signs its own way, neither of them applies it.
            double cap = GtmCap(state);
            double adds = SimPipeline.SkipsGtmCap(state) ? demand : Gd.Minf(demand, cap);
            double closeRate = adds / Gd.Maxf(demand, 0.001);

            // ── THE SEAM'S ANSWER. The spine computes (organicBase x mult +
            // womBase) x price and then applies that ceiling itself; solving the
            // first half for `mult` is what makes its line produce this funnel's
            // DEMAND exactly, referral lift and all — and leaves the clamping to
            // the clamp. An unfunded week therefore hands back exactly x1.00 and
            // the tick is arithmetically the week it always was.
            double mkMult = 1.0;
            if (deliverable)
                mkMult = Gd.Maxf((demand / Gd.Maxf(pd, 0.0001) - womBase) / organicBase, 0.0);

            // ── ATTRIBUTION, proportional and exact: every arrival is assigned,
            // and the parts sum to `adds` with no residue (pin 1 asserts it weekly).
            double attAds = lAds * pd * closeRate;
            double attCon = lCon * pd * closeRate;
            double attOb = lOb * pd * closeRate;
            double attRef = womAll * closeRate * (lift / (1.0 + lift));
            double attOrg = organic * closeRate;
            double attWom = womAll * closeRate / (1.0 + lift);

            var f = new Dictionary<string, double>
            {
                { "wk", state.Week },
                { "spend_ads", bAds }, { "spend_content", bCon },
                { "spend_referrals", bRef }, { "spend_outbound", bOb },
                { "spend_total", bAds + bCon + bRef + bOb },
                { "reach_ads", rAds }, { "reach_content", rCon },
                { "reach_referrals", 0.0 }, { "reach_outbound", rOb },
                { "leads_ads", lAds }, { "leads_content", lCon },
                { "leads_referrals", 0.0 }, { "leads_outbound", lOb },
                { "signed_ads", attAds }, { "signed_content", attCon },
                { "signed_referrals", attRef }, { "signed_outbound", attOb },
                { "reach_total", rAds + rCon + rOb },
                { "leads_total", leadsPaid },
                { "conv", conv }, { "close_rate", closeRate },
                { "equity", state.ContentEquity }, { "equity_before", state.ContentEquity },
                { "ref_gain", gain }, { "happy", Happy(state) },
                { "organic", attOrg }, { "wom", attWom },
                { "demand", demand }, { "adds", adds },
                { "gtm_cap", cap },
                { "ob_closers", ObClosers(state) },
                { "era_eff", EraEff(state) }, { "team_mult", TeamMult(state) },
                { "price_demand", pd }, { "launched", launchF },
                { "blended_cac", 0.0 },
                { "_b_sales", state.Budgets != null ? state.Budgets.Sales : 0 },
                { "_mk", mkMult },
            };
            Recac(f);
            return f;
        }

        /// <summary>
        /// CAC is what a customer COST: this channel's dollars over this channel's
        /// arrivals. Recomputed wherever attribution moves, so the two can never
        /// disagree. 0 means "no honest number" — the desk prints the reason.
        /// </summary>
        static void Recac(Dictionary<string, double> f)
        {
            for (int i = 0; i < Mix.Length; i++)
            {
                double sp = Num(f, "spend_" + Mix[i]);
                double got = Num(f, "signed_" + Mix[i]);
                f["cac_" + Mix[i]] = (got >= BurnSigned && sp > 0.0) ? sp / got : 0.0;
            }
            // the blended read the ledger already prints: acquisition + closing
            // spend over the week's arrivals (the spine's own rep.Cac, same meaning)
            double total = Num(f, "spend_total") + Num(f, "_b_sales");
            double gotAll = Gd.Maxf(Num(f, "adds"), 0.0);
            f["blended_cac"] = (gotAll >= 0.5 && total > 0.0) ? Gd.Round(total / gotAll) : 0.0;
        }

        // ═════════════════════ THE SPINE'S HOOKS ═════════════════════════════

        /// <summary>Tick 8, before adoption: the content stock compounds or rots,
        /// then the whole week's funnel is computed and parked for the seam and
        /// the desk to read.</summary>
        public static void TickPre(GameState state, WeeklyReport rep)
        {
            state.SetMeta("funnel_prev", state.GetMeta("funnel", null));
            // THE ONE STOCK IN THIS SUBSYSTEM. Posts and rankings are capital, not
            // spend: funded, the library climbs toward the level its budget
            // supports; starved, it fades. Equity builds even pre-launch (writing
            // before shipping is a real strategy) — only CONVERSION is launch-gated.
            double c0 = state.ContentEquity;
            double bCon = SpendOf(state, "content");
            if (bCon > 0.0)
                state.ContentEquity = Gd.Clampf(c0 + (ContentTarget(state, bCon) - c0) * ConRamp, 0.0, 1.0);
            else
                state.ContentEquity = c0 * ConDecay;
            Dictionary<string, double> f = Plan(state);
            f["equity_before"] = c0;
            // the seam's answer travels with the PLAN, never inside the public
            // read-out: it is an implementation detail of one adoption line
            double mult = Num(f, "_mk", 1.0);
            f.Remove("_mk");
            state.SetMeta("funnel", f);
            state.SetMeta("_funnel_plan", new Dictionary<string, double>
            {
                { "mk_mult", mult }, { "adds", Num(f, "adds") },
            });
        }

        /// <summary>
        /// THE REACH SEAM. `dflt` is the blended lever the engine would use on its
        /// own; the multiplier below makes the spine's adoption line land on this
        /// week's funnel number instead — reach, leads, referral lift and the
        /// capacity ceiling all folded into the one factor its formula exposes.
        /// </summary>
        public static double ReachMult(GameState state, double spend, double dflt)
        {
            var plan = state.GetMeta("_funnel_plan", null) as Dictionary<string, double>;
            if (plan == null || plan.Count == 0) return dflt;
            double v;
            return plan.TryGetValue("mk_mult", out v) ? v : dflt;
        }

        /// <summary>
        /// The money section. Acquisition spend is ONE P&amp;L lane, `marketing`, and
        /// the spine already books it as the four channels summed — the split
        /// lives where the funnel lives (the customers desk and the mix receipt),
        /// so the compact ledger line stays readable and every existing reader of
        /// pnl.marketing keeps working (spec section 4). Nothing to write here.
        /// </summary>
        public static void TickMoney(GameState state, WeeklyReport rep, MoneyWork m)
        {
        }

        /// <summary>After the record is written: the attribution follows what
        /// ACTUALLY landed, then the receipts — each naming the concept and cause.</summary>
        public static void TickPost(GameState state, WeeklyReport rep)
        {
            var plan = state.GetMeta("_funnel_plan", null) as Dictionary<string, double>;
            if (plan == null || plan.Count == 0) return;
            state.Meta.Remove("_funnel_plan");
            Dictionary<string, double> f = Funnel(state);
            if (f.Count == 0) return;
            double planned = Num(f, "adds");
            double actual = rep.Adds;
            // a stock-out or any later clamp can land a smaller week than the
            // funnel planned; attribution is a statement about arrivals, so it
            // follows them
            if (Gd.RoundToInt(planned) != (int)actual && planned > 0.0)
            {
                double k = actual / planned;
                string[] moved = { "signed_ads", "signed_content", "signed_referrals",
                                   "signed_outbound", "organic", "wom" };
                for (int i = 0; i < moved.Length; i++) f[moved[i]] = Num(f, moved[i]) * k;
                f["adds"] = actual;
                f["close_rate"] = actual / Gd.Maxf(Num(f, "demand"), 0.001);
                Recac(f);
                state.SetMeta("funnel", f);
            }
            Receipts(state, rep, f);
        }

        /// <summary>
        /// THREE NUMBERS THAT ADD UP. Rounding each source on its own printed
        /// "+31 customers (organic 9 · word of mouth 11 · channels 12)" — a
        /// receipt a player checks with a finger must balance, so the parts are
        /// apportioned by largest remainder, ties to the earlier source.
        /// </summary>
        static int[] SplitInt(int total, double[] parts)
        {
            var outp = new int[parts.Length];
            double sum = 0.0;
            for (int i = 0; i < parts.Length; i++) sum += Gd.Maxf(parts[i], 0.0);
            if (sum <= 0.0 || total <= 0) return outp;
            var rem = new double[parts.Length];
            int put = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                double exact = Gd.Maxf(parts[i], 0.0) / sum * total;
                outp[i] = (int)Math.Floor(exact);
                put += outp[i];
                rem[i] = exact - outp[i];
            }
            while (put < total)
            {
                int best = -1;
                for (int i = 0; i < parts.Length; i++)
                    if (rem[i] >= 0.0 && (best < 0 || rem[i] > rem[best])) best = i;
                if (best < 0) break;
                outp[best] += 1;
                rem[best] = -1.0;   // each source may take at most one extra unit
                put += 1;
            }
            return outp;
        }

        /// <summary>THE RECEIPTS (spec section 4). Every line names the mechanism
        /// and why it fired.</summary>
        static void Receipts(GameState state, WeeklyReport rep, Dictionary<string, double> f)
        {
            List<string> lines = rep.Lines;
            Dictionary<string, double> prev = FunnelPrev(state);
            double bAds = Num(f, "spend_ads");
            double bCon = Num(f, "spend_content");
            double bRef = Num(f, "spend_referrals");
            double bOb = Num(f, "spend_outbound");
            double adds = Num(f, "adds");

            // 1 ── the week's arrivals gain their third source. The spine printed
            // organic and word of mouth from its own blended line, which now
            // carries the channels inside it; this restates the real split.
            if (adds >= 1.0)
            {
                double chanSum = Num(f, "signed_ads") + Num(f, "signed_content")
                                 + Num(f, "signed_referrals") + Num(f, "signed_outbound");
                int total = rep.Adds;
                int[] split = SplitInt(total, new[] { Num(f, "organic"), Num(f, "wom"), chanSum });
                for (int i = 0; i < lines.Count; i++)
                {
                    string s = lines[i] ?? "";
                    if (s.StartsWith("+") && s.Contains(" customers (organic "))
                    {
                        lines[i] = string.Format(CultureInfo.InvariantCulture,
                            "+{0} customers (organic {1} · word of mouth {2} · channels {3})",
                            total, split[0], split[1], split[2]);
                        break;
                    }
                }
            }

            // 2 ── the mix: spend → customers, per channel, whenever there is a choice
            int funded = 0;
            for (int i = 0; i < Mix.Length; i++)
                if (Num(f, "spend_" + Mix[i]) > 0.0) funded += 1;
            if (funded >= 2)
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "the mix: ads ${0}→{1:0.0} · content ${2}→{3:0.0} (equity {4}%) · referrals ${5}→{6:0.0} · outbound ${7}→{8:0.0}",
                    (int)bAds, Num(f, "signed_ads"), (int)bCon, Num(f, "signed_content"),
                    Gd.RoundToInt(Num(f, "equity") * 100.0), (int)bRef, Num(f, "signed_referrals"),
                    (int)bOb, Num(f, "signed_outbound")));
            }

            // 3 ── saturation, taught at the moment it bites: more money, worse price
            double cacAds = Num(f, "cac_ads");
            double cacWas = Num(prev, "cac_ads");
            if (cacAds > 0.0 && cacWas > 0.0 && bAds >= 1.2 * Num(prev, "spend_ads")
                && cacAds >= 1.25 * cacWas)
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "ads CAC rose to ${0} — the cheap audience is spent (saturation)",
                    Gd.RoundToInt(cacAds)));
            }

            // 4 ── the stock crossing a threshold upward is the only visible sign
            // that a library is compounding
            double cNow = Num(f, "equity");
            double cWas = Num(f, "equity_before");
            double[] gates = { 0.25, 0.5, 0.75 };
            for (int i = 0; i < gates.Length; i++)
            {
                if (cWas < gates[i] && cNow >= gates[i])
                {
                    lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "the library compounds: content reaches {0}/wk now, at $0 marginal",
                        Gd.RoundToInt(Num(f, "reach_content"))));
                    break;
                }
            }

            // 5 ── and rot is the other half of the same lesson
            if (bCon <= 0.0 && cNow >= 0.05)
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "the library goes quiet — content equity fades to {0}%", Gd.RoundToInt(cNow * 100.0)));
            }

            // 6 ── the NPS gate, named instead of silently eating the spend
            if (bRef >= 500.0 && Num(f, "happy") < HappyFloor)
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "a referral program for a product nobody would vouch for (v0.{0}) — promoters first, program second",
                    state.Product));
            }

            // 7 ── demand versus capacity, the funnel's last lesson
            if (Num(f, "close_rate") < 0.9 && Num(f, "demand") >= 1.0)
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "demand outran closing: {0} wanted in, you signed {1} — capacity, not demand, is the bottleneck (sales or outbound)",
                    Gd.RoundToInt(Num(f, "demand")), Gd.RoundToInt(adds)));
            }

            // 8 ── money quietly buying nothing, by name
            for (int i = 0; i < Mix.Length; i++)
            {
                if (Num(f, "spend_" + Mix[i]) >= BurnSpend && Num(f, "signed_" + Mix[i]) < BurnSigned)
                {
                    lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "${0} into {1} found nobody — saturated or mispriced",
                        (int)Num(f, "spend_" + Mix[i]), Mix[i]));
                }
            }

            // 9 ── the classic pre-launch mistake, with its reason
            if (Num(f, "launched") <= 0.0 && bAds + bOb > 0.0)
                lines.Add("reach with nothing to sign — ads and cold calls convert only after launch");

            // 10 ── stage-appropriate acquisition, taught once, the first time paid
            // money goes in early
            if ((state.Era == "garage" || state.Era == "coworking") && bAds + bCon >= 500.0
                && !state.HasFlag("seen_paid_era_note"))
            {
                state.SetFlag("seen_paid_era_note");
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "the garage discount: paid reach x{0:0.00} — no brand, no pixel history. Outbound and word of mouth are the garage channels.",
                    Num(f, "era_eff")));
            }
        }

        /// <summary>DM context. The narrator gets the mix in one line and can never
        /// contradict the ledger, because it IS the ledger's numbers (funnel_mix).</summary>
        public static List<string> Directives(GameState state)
        {
            var outp = new List<string>();
            double total = SpendTotal(state);
            Dictionary<string, double> f = Funnel(state);
            if (total <= 0.0 && f.Count == 0) return outp;
            int cac = Gd.ToInt(Num(f, "blended_cac"));
            outp.Add(string.Format(CultureInfo.InvariantCulture,
                "- The funnel mix: ads ${0} · content ${1} (equity {2}%) · referrals ${3} · outbound ${4} · blended CAC {5}.",
                (int)SpendOf(state, "ads"), (int)SpendOf(state, "content"),
                Gd.RoundToInt(state.ContentEquity * 100.0), (int)SpendOf(state, "referrals"),
                (int)SpendOf(state, "outbound"), cac > 0 ? "$" + cac : "not yet knowable"));
            return outp;
        }

        /// <summary>
        /// Attention rows. Two conditions are worth stopping a founder for: money
        /// that bought nobody (the fix is on the ledger, where the lever is) and a
        /// library left to rot (the read is on the customers desk, where the stock
        /// shows). Labels are 40 characters or less: the ticker prints them verbatim.
        /// </summary>
        public static List<AttentionItem> Attention(GameState state)
        {
            var rows = new List<AttentionItem>();
            Dictionary<string, double> f = Funnel(state);
            if (f.Count > 0)
            {
                for (int i = 0; i < Mix.Length; i++)
                {
                    if (Num(f, "spend_" + Mix[i]) >= BurnSpend && Num(f, "signed_" + Mix[i]) < BurnSigned)
                    {
                        rows.Add(new AttentionItem
                        {
                            Desk = "the ledger", Key = "burning_" + Mix[i], Severity = 2,
                            Label = string.Format(CultureInfo.InvariantCulture,
                                "${0}/wk into {1} finds nobody", (int)Num(f, "spend_" + Mix[i]), Mix[i]),
                        });
                    }
                }
            }
            if (SpendOf(state, "content") <= 0.0 && state.ContentEquity >= 0.3)
            {
                rows.Add(new AttentionItem
                {
                    Desk = "customers", Key = "content_rot", Severity = 1,
                    Label = "the library fades · content unfunded",
                });
            }
            return rows;
        }

        // ── THE DM's ONE MARKETING CATEGORY ──────────────────────────────────
        /// <summary>
        /// The narrator says "put $2k into marketing" and the ENGINE decides which
        /// channels that means, splitting by the mix the player already curated: a
        /// narrator must never silently overwrite a curated mix, and the op schema
        /// stays byte-identical in both prompt files (spec section 5).
        /// </summary>
        public static void SetMarketing(GameState state, int amount)
        {
            if (state.Budgets == null) state.Budgets = new Budgets();
            Budgets b = state.Budgets;
            int mixSum = b.Ads + b.Content + b.Referrals + b.Outbound;
            if (mixSum <= 0)
            {
                // cold start: the instant channel, because nothing else pays in week one
                b.Ads = amount;
            }
            else
            {
                int put = 0;
                int[] cur = { b.Ads, b.Content, b.Referrals, b.Outbound };
                var share = new int[4];
                for (int i = 0; i < 4; i++)   // deterministic order; remainder → ads
                {
                    share[i] = (int)Math.Floor((double)amount * cur[i] / mixSum);
                    put += share[i];
                }
                b.Ads = share[0] + (amount - put);
                b.Content = share[1];
                b.Referrals = share[2];
                b.Outbound = share[3];
            }
            state.MarketingBudget = 0;
        }
    }
}
