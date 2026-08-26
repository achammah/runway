using System.Collections.Generic;
using System.Globalization;

namespace Runway.Core
{
    /// <summary>
    /// LANE — THE WORKS (per-type capacity, the unit ticket, relief valves).
    /// Spec: docs/design/DECISIONS.md (the factory → THE WORKS + SCALE LADDER)
    /// + docs/design/DAG2.md (L-DIVWORKS) + mockups 10/11/12.
    ///
    /// Same four questions in every business, in its own units:
    ///   Service      crew bookable hours (people × slots, site-aware) ·
    ///                relief = freelancers, priced per unit
    ///   Software     the care team is the ceiling; past it nothing walks
    ///                away — replies slip and churn bites (DEGRADATION) ·
    ///                relief = cloud burst, capped (the queue's human half
    ///                doesn't burst)
    ///   Hardware     the machines — SimFactory's molecule, REUSED not
    ///                forked; this lane only reads
    ///   Marketplace  the seller pool is the factory (supply proxy off
    ///                traction, lagged by growth) · relief = recruit-supply
    ///
    /// THE GAP IS PRICED HONESTLY: service/marketplace lose UN-BILLED revenue
    /// (deducted through the money record like the factory's lost billing);
    /// software pays in churn (TickPost, the factory's `walked` idiom);
    /// hardware already pays both through SimFactory.
    ///
    /// THE RELIEF VALVES are standing levers (DM op `set_relief`). Per-valve
    /// semantics of x: freelance = units/wk (0..60, billed at
    /// price_book.freelance_rate each) · subcontract = 1/0 (the factory's own
    /// toggle) · burst = extra seats of ceiling (0..4000) · recruit_supply =
    /// $/wk (0..2000, ≈ one seller per $35 feeding ≈2.5 orders/wk).
    /// DURABLE HOME: state.Flags as "works_relief:&lt;cat&gt;:&lt;int&gt;".
    ///
    /// The spine calls: TickPre §7i (the week's dice drawn and parked),
    /// TickMoney (the serving math on SETTLED traction; owns ONLY m.Relief +
    /// the un-billing against m.Revenue), TickPost (software's churn tax +
    /// receipts), Directives, Attention.
    ///
    /// SALTS: SALT_WORKS_CAPACITY (160) capacity jitter · SALT_WORKS_RELIEF
    /// (161) freelancer availability · SALT_WORKS_REMAINDER (162) seeded
    /// remainders. Draw order per week FIXED: jitter, avail, then remainders.
    ///
    /// TWIN LAW: game/src/core/lanes/sim_works.gd carries the same logic in
    /// the same order.
    /// </summary>
    public static class SimWorks
    {
        // ── CAPACITY (service) ─────────────────────────────────────────────
        public const double SLOTS_BASE = 24.0;
        public const double SLOTS_SKILL = 2.0;
        public const double FOUNDER_SLOTS = 26.0;
        // ── CEILING (software) ─────────────────────────────────────────────
        public const double SW_FREE_SEATS = 400.0;
        public const double SW_SEAT_COST = 2.6;
        public const double SW_DEGRADE_RATE = 0.004;
        public const double SW_OVER_SPAN = 0.25;
        public const double SW_BURST_CAP = 0.6;
        // ── SUPPLY (marketplace) ───────────────────────────────────────────
        public const double MK_SELLER_RATIO = 0.42;
        public const double MK_SELLER_FEED = 2.5;
        public const double MK_SELLER_COST = 35.0;
        public const double MK_LAG_K = 2.0;
        // ── the week's dice ────────────────────────────────────────────────
        public const double CAP_JITTER = 0.04;
        public const double RELIEF_AVAIL_LO = 0.7;
        // ── lever clamps ───────────────────────────────────────────────────
        public const int FREELANCE_MAX = 60;
        public const int BURST_MAX = 4000;
        public const int RECRUIT_MAX = 2000;
        const int LINE_CAP = 4;
        /// Retiring a product: exactly half migrate, the rest churn.
        public const double RETIRE_MIGRATE = 0.5;

        // ═════════════════════════ ACTIVE &amp; VOCAB ══════════════════════════

        /// <summary>The works reads the catalog for its ticket: no offers, no
        /// works math (a legacy run keeps its old arithmetic).</summary>
        public static bool Active(GameState state)
        {
            return state.Offers.Count > 0;
        }

        /// <summary>This business's native words (topics.works, with a hand
        /// for the older works_terms key), else the type defaults.</summary>
        public static Dictionary<string, string> Vocab(GameState state)
        {
            var d = new Dictionary<string, object>();
            object t;
            if (state.Topics != null
                && (state.Topics.TryGetValue("works", out t)
                    || state.Topics.TryGetValue("works_terms", out t)))
            {
                var td = t as Dictionary<string, object>;
                if (td != null) d = td;
                else
                {
                    var jt = t as Newtonsoft.Json.Linq.JObject;
                    if (jt != null) d = jt.ToObject<Dictionary<string, object>>();
                }
            }
            string unit = "unit", cap = "capacity", relief = "outside help";
            switch (state.BizWhat)
            {
                case "Service": unit = "session"; cap = "bookable hours"; relief = "freelancers"; break;
                case "Software": unit = "seat"; cap = "the care team"; relief = "cloud burst"; break;
                case "Hardware": unit = "unit"; cap = "the machines"; relief = "the subcontract shop"; break;
                case "Marketplace": unit = "order"; cap = "the seller pool"; relief = "recruited supply"; break;
            }
            return new Dictionary<string, string>
            {
                { "unit_word", DS(d, "unit_word", unit) },
                { "capacity_word", DS(d, "capacity_word", cap) },
                { "relief_word", DS(d, "relief_word", relief) },
            };
        }

        static string DS(Dictionary<string, object> d, string key, string dflt)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v != null)
            {
                string s = v.ToString();
                if (s.Length > 0) return s;
            }
            return dflt;
        }

        /// <summary>Read a number out of a string-keyed block, Godot-style.</summary>
        public static double Num(Dictionary<string, object> d, string key, double dflt = 0.0)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v != null)
            {
                try { return System.Convert.ToDouble(v, CultureInfo.InvariantCulture); }
                catch { return dflt; }
            }
            return dflt;
        }

        // ═════════════════ THE RELIEF LEVERS (durable) ═════════════════════

        public static int ReliefGet(GameState state, string cat)
        {
            if (cat == "subcontract")
                return state.Hardware != null && state.Hardware.SubcontractOn ? 1 : 0;
            string prefix = "works_relief:" + cat + ":";
            for (int i = 0; i < state.Flags.Count; i++)
                if (state.Flags[i].StartsWith(prefix))
                {
                    int v;
                    if (int.TryParse(state.Flags[i].Substring(prefix.Length), out v)) return v;
                    return 0;
                }
            return 0;
        }

        /// <summary>THE ONE WRITE — clamped here, whoever asks.</summary>
        public static int ReliefSet(GameState state, string cat, int x)
        {
            int v;
            switch (cat)
            {
                case "freelance": v = Gd.Clampi(x, 0, FREELANCE_MAX); break;
                case "burst": v = Gd.Clampi(x, 0, BURST_MAX); break;
                case "recruit_supply":
                    v = Gd.Clampi(x, 0, Gd.Mini(RECRUIT_MAX, SimEngine.EraSpendCap(state.Era) / 4));
                    break;
                case "subcontract":
                    if (SimFactory.Active(state) && SimFactory.SubUnlocked(state))
                    {
                        bool want = x > 0;
                        if (state.Hardware.SubcontractOn != want)
                            SimFactory.ToggleSubcontract(state);
                        return want ? 1 : 0;
                    }
                    return 0;
                default: return 0;
            }
            string prefix = "works_relief:" + cat + ":";
            for (int i = state.Flags.Count - 1; i >= 0; i--)
                if (state.Flags[i].StartsWith(prefix)) state.Flags.RemoveAt(i);
            if (v > 0) state.Flags.Add(prefix + v);
            return v;
        }

        /// <summary>The desk's stepper ladders, per valve.</summary>
        public static int[] ReliefSteps(string cat)
        {
            switch (cat)
            {
                case "freelance": return new[] { 0, 2, 4, 6, 8, 10, 14, 20, 30, 45, 60 };
                case "burst": return new[] { 0, 100, 200, 400, 800, 1600, 3000, 4000 };
                case "recruit_supply": return new[] { 0, 100, 200, 300, 500, 800, 1200, 2000 };
            }
            return new[] { 0, 1 };
        }

        // ═════════════════ THE NATIVE ARITHMETIC ═══════════════════════════

        /// <summary>What the market wants this week, in native units: every
        /// billing offer's customers × cadence (the "wanted" column).</summary>
        public static double DemandUnits(GameState state)
        {
            double total = 0.0;
            double fm = SimEngine.StreetFairMult(state);
            for (int i = 0; i < state.Offers.Count; i++)
            {
                Offer od = state.Offers[i];
                if (SimEngine.OfferBilledPrice(od, fm) <= 0.0) continue;
                total += state.Traction * od.Weight * SimEngine.OfferCadence(od.Unit);
            }
            return total;
        }

        /// <summary>What one native unit bills — revenue over units, so the
        /// un-billing and the books can never disagree.</summary>
        public static double RevPerUnit(GameState state)
        {
            double units = DemandUnits(state);
            if (units <= 0.0) return 0.0;
            double arpu = SimEngine.OffersArpu(state);
            if (arpu < 0.0) return 0.0;
            return state.Traction * arpu / units;
        }

        /// <summary>The catalog's variable cost per unit, volume-blended.</summary>
        public static double BaseUnitCost(GameState state)
        {
            double units = 0.0;
            double cost = 0.0;
            double fm = SimEngine.StreetFairMult(state);
            for (int i = 0; i < state.Offers.Count; i++)
            {
                Offer od = state.Offers[i];
                if (SimEngine.OfferBilledPrice(od, fm) <= 0.0) continue;
                double u = od.Weight * SimEngine.OfferCadence(od.Unit);
                units += u;
                cost += u * od.UnitCost;
            }
            return units > 0.0 ? cost / units : 0.0;
        }

        /// <summary>What the feature inventory adds to serving one unit of a
        /// product — the works' half of WHAT WE MAKE's cost footer. A thin
        /// seam over the features lane's own read (the product's features +
        /// the shared plumbing); the works never re-derives what L-MAKE owns.</summary>
        public static double FeatureCostAdd(GameState state, string productId)
        {
            return Gd.Maxf(SimFeatures.UnitCostTotal(state, productId ?? ""), 0.0);
        }

        /// <summary>THE CREW'S HANDS, site-aware. Serving roles are everyone
        /// but the sellers and the marketers (a manager runs the floor at half
        /// a hand). A person mid-ramp or onboarding gives zero.</summary>
        public static double CapacityOfSite(GameState state, string site)
        {
            if (state.BizWhat != "Service") return 0.0;
            double slots = 0.0;
            if (site == "" || state.Sites.Count == 0) slots += FOUNDER_SLOTS;
            for (int i = 0; i < state.Employees.Count; i++)
            {
                Employee ed = state.Employees[i];
                if ((ed.Site ?? "") != site) continue;
                string role = ed.Role ?? "";
                if (role.Contains("sales") || role.Contains("marketing")) continue;
                if (SimDivisions.MarkedUntil(state, "works_ramp", ed.Name ?? "") > state.Week - 1)
                    continue;
                int skill = Gd.Clampi(ed.Skill, 1, 5);
                double hand = SLOTS_BASE + SLOTS_SKILL * (skill - 3);
                if (role.Contains("manager")) hand *= 0.5;
                slots += hand;
            }
            return slots;
        }

        public static double ServiceCapacity(GameState state)
        {
            double total = CapacityOfSite(state, "");
            for (int i = 0; i < state.Sites.Count; i++)
                total += CapacityOfSite(state, state.Sites[i].Id ?? "");
            return total;
        }

        /// <summary>THE SOFTWARE CEILING — free seats + care-effective dollars
        /// at $2.60 a seat.</summary>
        public static double SoftwareCeiling(GameState state)
        {
            return SW_FREE_SEATS + SimLabor.CareEff(state, state.Budgets.Care) / SW_SEAT_COST;
        }

        /// <summary>THE SELLER POOL — other people's shops, lagged by your own
        /// growth: fast growth starves the shelves.</summary>
        public static double MarketplaceSupply(GameState state)
        {
            double lag = 1.0 / (1.0 + MK_LAG_K * Gd.Clampf(state.LastGrowth, 0.0, 0.5));
            double sellers = System.Math.Ceiling(state.Traction * MK_SELLER_RATIO);
            return sellers * MK_SELLER_FEED * lag;
        }

        public static int SellerPool(GameState state)
        {
            return (int)System.Math.Ceiling(state.Traction * MK_SELLER_RATIO);
        }

        /// <summary>THE UNIT TICKET — the offer's own generated cost lines
        /// (learning at the total, never per line) + the features' adds.</summary>
        public static Dictionary<string, object> UnitTicket(GameState state, int offerI)
        {
            if (offerI < 0 || offerI >= state.Offers.Count)
                return new Dictionary<string, object>();
            Offer od = state.Offers[offerI];
            double lc = SimEngine.LearningCurve(state);
            var lines = new List<Dictionary<string, object>>();
            if (od.CostLines != null && od.CostLines.Count > 0)
            {
                for (int i = 0; i < od.CostLines.Count; i++)
                    lines.Add(new Dictionary<string, object>
                        { { "label", od.CostLines[i].Label }, { "amount", od.CostLines[i].Amount } });
            }
            else
            {
                lines.Add(new Dictionary<string, object>
                    { { "label", "cost of one" }, { "amount", od.UnitCost } });
            }
            double feat = FeatureCostAdd(state, od.ProductId ?? "");
            if (feat > 0.005)
                lines.Add(new Dictionary<string, object>
                    { { "label", "the features' share" }, { "amount", feat } });
            double cost = od.UnitCost * lc + feat;
            double sells = SimEngine.OfferBilledPrice(od, SimEngine.StreetFairMult(state));
            return new Dictionary<string, object>
            {
                { "lines", lines }, { "cost_each", cost }, { "sells", sells },
                { "margin", sells - cost }, { "lc", lc },
            };
        }

        // ═════════════════ THE WEEK'S PLAN (pure) ══════════════════════════

        /// <summary>The whole works, one honest map — the tick computes it
        /// with the week's own dice; the desk recomputes live (quiet dice).</summary>
        public static Dictionary<string, object> WeekPlan(GameState state, double jitter, double avail)
        {
            Dictionary<string, string> v = Vocab(state);
            var w = new Dictionary<string, object>
            {
                { "type", state.BizWhat }, { "unit_word", v["unit_word"] },
                { "demand_units", 0.0 }, { "capacity_units", 0.0 },
                { "relief_cap_units", 0.0 }, { "relief_used", 0.0 },
                { "relief_spend", 0.0 }, { "served_units", 0.0 },
                { "walk_units", 0.0 }, { "unbilled", 0.0 }, { "rev_per_unit", 0.0 },
                { "ceiling", 0.0 }, { "over", 0.0 }, { "sellers", 0 },
                { "jitter", jitter }, { "avail", avail },
            };
            if (!Active(state)) return w;
            double units = DemandUnits(state);
            w["demand_units"] = units;
            w["rev_per_unit"] = RevPerUnit(state);
            switch (state.BizWhat)
            {
                case "Service":
                {
                    double cap = ServiceCapacity(state) * jitter;
                    w["capacity_units"] = cap;
                    double fee = SimDivisions.Pb(state, "freelance_rate");
                    double capUnits = ReliefGet(state, "freelance") * avail;
                    w["relief_cap_units"] = capUnits;
                    double gap = Gd.Maxf(units - cap, 0.0);
                    double used = Gd.Minf(gap, capUnits);
                    w["relief_used"] = used;
                    w["relief_spend"] = used * fee;
                    w["served_units"] = Gd.Minf(units, cap + used);
                    w["walk_units"] = Gd.Maxf(units - Num(w, "served_units"), 0.0);
                    w["unbilled"] = Num(w, "walk_units") * Num(w, "rev_per_unit");
                    break;
                }
                case "Software":
                {
                    double ceiling = SoftwareCeiling(state);
                    double seats = state.Traction;
                    double burstSeats = ReliefGet(state, "burst");
                    w["ceiling"] = ceiling + burstSeats;
                    w["capacity_units"] = ceiling + burstSeats;
                    double over = Gd.Maxf(seats - ceiling, 0.0);
                    // burst closes at most 60% of the RAW overload — the
                    // queue's human half doesn't burst
                    double burstUsed = Gd.Minf(Gd.Minf(burstSeats, over), over * SW_BURST_CAP);
                    double rate = Gd.Maxf(0.4 * BaseUnitCost(state) * SimEngine.LearningCurve(state), 0.3);
                    w["relief_cap_units"] = burstSeats;
                    w["relief_used"] = burstUsed;
                    w["relief_spend"] = burstUsed * rate;
                    w["over"] = Gd.Maxf(over - burstUsed, 0.0);
                    w["served_units"] = seats;
                    w["demand_units"] = seats;
                    break;
                }
                case "Marketplace":
                {
                    double feed = MarketplaceSupply(state) * jitter;
                    w["sellers"] = SellerPool(state);
                    w["capacity_units"] = feed;
                    double push = ReliefGet(state, "recruit_supply");
                    double pushedUnits = push / MK_SELLER_COST * MK_SELLER_FEED;
                    w["relief_cap_units"] = pushedUnits;
                    double gap2 = Gd.Maxf(units - feed, 0.0);
                    double used2 = Gd.Minf(gap2, pushedUnits);
                    w["relief_used"] = used2;
                    w["relief_spend"] = push > 0.0 ? push : 0.0;   // the push spends whole
                    w["served_units"] = Gd.Minf(units, feed + used2);
                    w["walk_units"] = Gd.Maxf(units - Num(w, "served_units"), 0.0);
                    w["unbilled"] = Num(w, "walk_units") * Num(w, "rev_per_unit");
                    break;
                }
                case "Hardware":
                {
                    // the factory owns the whole molecule — the works only reads
                    Dictionary<string, object> hw = SimFactory.WeekBlock(state);
                    w["capacity_units"] = Num(hw, "capacity", SimFactory.Capacity(state));
                    w["demand_units"] = Num(hw, "demand_units", units);
                    w["served_units"] = Num(hw, "sold");
                    w["walk_units"] = Num(hw, "lost_adds");
                    w["relief_used"] = Num(hw, "sub_units");
                    w["relief_spend"] = 0.0;   // billed by the factory's own lane
                    break;
                }
            }
            return w;
        }

        /// <summary>LAST WEEK'S WORKS, whole — recomputed live (quiet dice)
        /// before the first tick and after a load.</summary>
        public static Dictionary<string, object> WeekView(GameState state)
        {
            var w = state.GetMeta("works") as Dictionary<string, object>;
            if (w != null && w.Count > 0) return w;
            return WeekPlan(state, 1.0, 1.0);
        }

        // ═══════════════ THE SPINE'S ENTRY POINTS ══════════════════════════

        /// <summary>Tick §7i — the week's dice are drawn HERE (order fixed:
        /// ① capacity jitter ② freelancer availability) and parked; the
        /// serving math waits for TickMoney, where traction has settled.</summary>
        public static void TickPre(GameState state, WeeklyReport rep)
        {
            if (!Active(state))
            {
                state.SetMeta("works", new Dictionary<string, object>());
                return;
            }
            double jitter = 1.0;
            double avail = 1.0;
            if (state.BizWhat == "Service" || state.BizWhat == "Marketplace")
            {
                jitter = 1.0 + (SimEngine.RngForSalt(state, SimEngine.SALT_WORKS_CAPACITY).Randf() * 2.0 - 1.0) * CAP_JITTER;
                avail = RELIEF_AVAIL_LO + SimEngine.RngForSalt(state, SimEngine.SALT_WORKS_RELIEF).Randf() * (1.0 - RELIEF_AVAIL_LO);
            }
            state.SetMeta("works_dice", new Dictionary<string, object>
                { { "jitter", jitter }, { "avail", avail } });
        }

        /// <summary>The money section — traction settled, the works serves the
        /// week now. Owns ONLY m.Relief; the un-billing rides m.Revenue the
        /// same way the factory's lost billing does.</summary>
        public static void TickMoney(GameState state, WeeklyReport rep, MoneyWork m)
        {
            if (!Active(state)) return;
            var dice = state.GetMeta("works_dice") as Dictionary<string, object>
                ?? new Dictionary<string, object>();
            Dictionary<string, object> w = WeekPlan(state, Num(dice, "jitter", 1.0), Num(dice, "avail", 1.0));
            state.SetMeta("works", w);
            state.Meta.Remove("works_dice");
            if (state.BizWhat == "Hardware") return;   // the factory books its own
            double relief = Num(w, "relief_spend");
            if (relief >= 1.0) m.Relief += relief;
            double unbilled = Gd.Minf(Num(w, "unbilled"), Gd.Maxf(m.Revenue, 0.0));
            if (unbilled >= 1.0)
            {
                m.Revenue -= unbilled;
                w["unbilled"] = unbilled;
            }
        }

        /// <summary>After the record: software's overload collects its churn
        /// (traction × 0.4%/wk × how far past the ceiling, seeded remainder),
        /// then the receipts, loudest first.</summary>
        public static void TickPost(GameState state, WeeklyReport rep)
        {
            if (!Active(state)) return;
            Dictionary<string, object> w = WeekView(state);
            var lines = new List<string>();
            Dictionary<string, string> vw = Vocab(state);
            string unitWord = vw["unit_word"];
            if (state.BizWhat == "Software")
            {
                double over = Num(w, "over");
                if (over > 0.0)
                {
                    double ceiling = Gd.Maxf(Num(w, "ceiling", 1.0), 1.0);
                    double overFrac = Gd.Clampf(over / (ceiling * SW_OVER_SPAN), 0.0, 1.0);
                    double exact = state.Traction * SW_DEGRADE_RATE * overFrac;
                    int walked = (int)System.Math.Floor(exact);
                    if (SimEngine.RngForSalt(state, SimEngine.SALT_WORKS_REMAINDER).Randf()
                        < exact - System.Math.Floor(exact))
                        walked += 1;
                    walked = Gd.Mini(walked, state.Traction);
                    if (walked > 0)
                    {
                        state.Traction = Gd.Maxi(state.Traction - walked, 0);
                        w["degrade_walked"] = walked;
                        lines.Add(string.Format(
                            "past the ceiling nothing walks away — replies slip instead: −{0} churned to the queue ({1} {2}s over)",
                            walked, Gd.RoundToInt(over), unitWord));
                    }
                }
                else if (Num(w, "relief_used") > 0.0)
                {
                    lines.Add(string.Format(
                        "cloud burst held the line: {0} {1}s served over the care ceiling",
                        Gd.RoundToInt(Num(w, "relief_used")), unitWord));
                }
            }
            double walk = Num(w, "walk_units");
            if (walk >= 1.0 && Num(w, "unbilled") >= 1.0)
                lines.Add(string.Format(
                    "{0} {1}s turned away — ${2}/wk walks (hands for {3} of {4})",
                    Gd.RoundToInt(walk), unitWord, Gd.RoundToInt(Num(w, "unbilled")),
                    Gd.RoundToInt(Num(w, "served_units")), Gd.RoundToInt(Num(w, "demand_units"))));
            double used = Num(w, "relief_used");
            if (used >= 1.0 && state.BizWhat != "Software" && state.BizWhat != "Hardware")
                lines.Add(string.Format(
                    "{0} served {1} {2}s: −${3} — dearer each, but dearer beats turned away",
                    vw["relief_word"], Gd.RoundToInt(used), unitWord,
                    Gd.RoundToInt(Num(w, "relief_spend"))));
            if (used > 0.0 && used >= Num(w, "relief_cap_units") - 0.01 && walk >= 1.0)
                lines.Add("the relief valve is full open and it still wasn't enough");
            for (int i = 0; i < lines.Count && i < LINE_CAP; i++)
                rep.Lines.Add(lines[i]);
        }

        /// <summary>DM context — the works in one line, native units.</summary>
        public static List<string> Directives(GameState state)
        {
            var outp = new List<string>();
            if (!Active(state)) return outp;
            Dictionary<string, object> w = WeekView(state);
            Dictionary<string, string> vw = Vocab(state);
            if (state.BizWhat == "Software")
            {
                outp.Add(string.Format("- The works: {0} {1}s live under a ceiling of {2} ({3}).",
                    Gd.RoundToInt(Num(w, "served_units")), vw["unit_word"],
                    Gd.RoundToInt(Num(w, "ceiling")), vw["capacity_word"]));
            }
            else
            {
                double walk = Num(w, "walk_units");
                outp.Add(string.Format("- The works: {0} {1}s wanted, capacity for {2}{3}.",
                    Gd.RoundToInt(Num(w, "demand_units")), vw["unit_word"],
                    Gd.RoundToInt(Num(w, "capacity_units")),
                    walk >= 1.0 ? " — " + Gd.RoundToInt(walk) + " walked" : ""));
            }
            return outp;
        }

        /// <summary>Attention — the works desk: money walking or churn biting
        /// is worth a stop; a saturated valve says the fix is structural; a
        /// moved machine offline names its roof.</summary>
        public static List<AttentionItem> Attention(GameState state)
        {
            var rows = new List<AttentionItem>();
            if (!Active(state)) return rows;
            Dictionary<string, object> w = WeekView(state);
            double unbilled = Num(w, "unbilled");
            if (unbilled >= 1.0)
                rows.Add(new AttentionItem { Desk = "the works", Key = "works_gap",
                    Severity = 2, Label = "$" + Gd.RoundToInt(unbilled) + "/wk walks — capacity short" });
            if ((int)Num(w, "degrade_walked") > 0)
                rows.Add(new AttentionItem { Desk = "the works", Key = "works_degrade",
                    Severity = 2, Label = "past the ceiling — churn is the queue" });
            double used = Num(w, "relief_used");
            if (used > 0.0 && used >= Num(w, "relief_cap_units") - 0.01
                && Num(w, "walk_units") >= 1.0)
                rows.Add(new AttentionItem { Desk = "the works", Key = "relief_full",
                    Severity = 2, Label = "relief valve full open — still short" });
            if (state.Hardware != null && state.Hardware.Equipment != null)
            {
                for (int i = 0; i < state.Hardware.Equipment.Count; i++)
                {
                    EquipmentItem md = state.Hardware.Equipment[i];
                    if (!string.IsNullOrEmpty(md.Site)
                        && SimDivisions.MarkedUntil(state, "works_off", md.Name ?? "") > state.Week - 1)
                    {
                        string nm = md.Name ?? "a machine";
                        rows.Add(new AttentionItem { Desk = "the works", Key = "machine_moving",
                            Severity = 2,
                            Label = (nm.Length > 24 ? nm.Substring(0, 24) : nm) + " offline — mid-move" });
                    }
                }
            }
            return rows;
        }

        // ═══════════ THE MUTATION-LAW EXECUTORS (this lane's) ══════════════

        /// <summary>REFINANCE — swap a CURRENT bank note for a new quote at
        /// today's standing. A distressed note or a locked book refuses.</summary>
        public static Dictionary<string, object> RefinanceQuote(GameState state, int idx, int term)
        {
            if (idx < 0 || idx >= state.Loans.Count) return new Dictionary<string, object>();
            Loan note = state.Loans[idx];
            if ((note.Kind ?? "") != "bank" || note.Balance <= 0)
                return new Dictionary<string, object>();
            if (note.Missed >= 1 || SimBank.CreditLocked(state))
                return new Dictionary<string, object>();
            int[] terms = SimBank.TermOptions(state, "bank");
            int t = System.Array.IndexOf(terms, term) >= 0 ? term : terms[0];
            double rate = SimBank.BankRateWk(state);
            int fee = Gd.RoundToInt(SimDivisions.Pb(state, "refinance_break_fee"));
            int bal = note.Balance;
            return new Dictionary<string, object>
            {
                { "old_rate", note.RateWk }, { "new_rate", rate }, { "fee", fee },
                { "balance", bal }, { "term", t }, { "old_pay", note.PayWk },
                { "new_pay", SimBank.LoanPaymentWk(bal, rate, t) },
            };
        }

        public static Dictionary<string, object> RefinanceNote(GameState state, int idx, int term)
        {
            Dictionary<string, object> q = RefinanceQuote(state, idx, term);
            if (q.Count == 0)
                return new Dictionary<string, object> { { "ok", false },
                    { "why", "only a current bank note refinances" } };
            int fee = System.Convert.ToInt32(q["fee"], CultureInfo.InvariantCulture);
            if (state.Cash < fee)
                return new Dictionary<string, object> { { "ok", false },
                    { "why", "$" + (fee - state.Cash) + " short of the break fee" } };
            Loan note = state.Loans[idx];
            state.Cash -= fee;
            note.RateWk = System.Convert.ToDouble(q["new_rate"], CultureInfo.InvariantCulture);
            note.TermWk = System.Convert.ToInt32(q["term"], CultureInfo.InvariantCulture);
            note.TakenWeek = state.Week;
            note.PayWk = System.Convert.ToInt32(q["new_pay"], CultureInfo.InvariantCulture);
            state.LogAction(string.Format(
                "REFINANCED the bank note: {0:F1}%→{1:F1}%/wk, break fee ${2}, ${3}/wk now",
                Num(q, "old_rate") * 100.0, Num(q, "new_rate") * 100.0, fee, note.PayWk));
            return new Dictionary<string, object> { { "ok", true }, { "why", "" }, { "quote", q } };
        }

        /// <summary>FIRE AN ACCOUNT — the contract penalty bills, the revenue
        /// dies, and the street hears it (the typed rival_fud cloud).</summary>
        public static Dictionary<string, object> FireAccount(GameState state, string name = "")
        {
            if (state.Traction <= 0)
                return new Dictionary<string, object> { { "ok", false }, { "why", "no accounts to fire" } };
            int penalty = Gd.RoundToInt(SimDivisions.Pb(state, "account_fire_penalty"));
            int seats = 1;
            string who = (name ?? "").Trim();
            if (state.BizWho == "Enterprise" && state.Logos.Count > 0)
            {
                int hitI = 0;
                for (int i = 0; i < state.Logos.Count; i++)
                    if (who.Length > 0 && (state.Logos[i].Name ?? "")
                        .ToLowerInvariant().Contains(who.ToLowerInvariant()))
                    {
                        hitI = i;
                        break;
                    }
                Logo logo = state.Logos[hitI];
                who = logo.Name ?? "an account";
                seats = Gd.Maxi(logo.Seats, 1);
                state.Logos.RemoveAt(hitI);
            }
            else if (who.Length == 0)
            {
                who = "the account";
            }
            state.Cash -= penalty;
            state.Traction = Gd.Maxi(state.Traction - seats, 0);
            state.Hype = Gd.Clampi(state.Hype - 2, 0, 100);
            SimEngine.AddStatus(state, "rival_fud", 2);
            state.LogAction(string.Format(
                "FIRED {0}: −${1} penalty, {2} customer{3} gone — the street heard",
                who, penalty, seats, seats > 1 ? "s" : ""));
            return new Dictionary<string, object> { { "ok", true }, { "why", "" },
                { "penalty", penalty }, { "seats", seats }, { "who", who } };
        }

        /// <summary>RETIRE A PRODUCT — its offers retire with it; exactly half
        /// its customers migrate, the rest churn; its features die with the
        /// codebase they lived in.</summary>
        public static Dictionary<string, object> RetireProduct(GameState state, string productId)
        {
            if (SimDivisions.ProductsCount(state) < 2)
                return new Dictionary<string, object> { { "ok", false },
                    { "why", "the only product cannot retire — that is a pivot" } };
            double weightAll = 0.0;
            double weightGone = 0.0;
            var names = new List<string>();
            for (int i = 0; i < state.Offers.Count; i++)
            {
                Offer od = state.Offers[i];
                weightAll += od.Weight;
                if ((od.ProductId ?? "") == (productId ?? ""))
                {
                    weightGone += od.Weight;
                    names.Add(od.Name);
                }
            }
            if (names.Count == 0)
                return new Dictionary<string, object> { { "ok", false },
                    { "why", "no product called '" + productId + "'" } };
            double share = weightGone / Gd.Maxf(weightAll, 0.001);
            int cust = Gd.RoundToInt(state.Traction * share);
            int churned = (int)System.Math.Floor(cust * (1.0 - RETIRE_MIGRATE));
            for (int i = state.Offers.Count - 1; i >= 0; i--)
                if ((state.Offers[i].ProductId ?? "") == (productId ?? ""))
                    state.Offers.RemoveAt(i);
            for (int j = state.Features.Count - 1; j >= 0; j--)
                if ((state.Features[j].ProductId ?? "") == (productId ?? ""))
                    state.Features.RemoveAt(j);
            state.Traction = Gd.Maxi(state.Traction - churned, 0);
            state.LogAction(string.Format(
                "RETIRED {0}: {1} off the shelf, {2} migrated, {3} churned",
                productId, string.Join(", ", names), cust - churned, churned));
            return new Dictionary<string, object> { { "ok", true }, { "why", "" },
                { "offers", names }, { "migrated", cust - churned }, { "churned", churned } };
        }

        // ═══════════ THE DM OP DOORS (WeekCommit arms call these) ══════════

        static string DStr(Dictionary<string, object> d, string key, string dflt = "")
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v != null) return v.ToString();
            return dflt;
        }

        static int DInt(Dictionary<string, object> d, string key, int dflt = 0)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v != null)
            {
                try { return System.Convert.ToInt32(v, CultureInfo.InvariantCulture); }
                catch { return dflt; }
            }
            return dflt;
        }

        /// <summary>{op:"set_relief", cat:&lt;valve&gt;, x:&lt;per-valve
        /// semantics&gt;, weeks:1}</summary>
        public static string OpSetRelief(GameState state, Dictionary<string, object> d)
        {
            string cat = DStr(d, "cat");
            if (cat != "freelance" && cat != "subcontract" && cat != "burst"
                && cat != "recruit_supply")
                return "no relief valve called '" + cat + "'";
            int x = DInt(d, "x", DInt(d, "v"));
            if (cat == "subcontract" && !(SimFactory.Active(state) && SimFactory.SubUnlocked(state)))
                return "no outside shop for this business yet";
            int v = ReliefSet(state, cat, x);
            switch (cat)
            {
                case "freelance":
                    return string.Format("freelancers booked up to {0}/wk (at ${1} each)",
                        v, Gd.RoundToInt(SimDivisions.Pb(state, "freelance_rate")));
                case "burst":
                    return "cloud burst provisioned: +" + v + " seats of ceiling";
                case "recruit_supply":
                    return string.Format("seller recruitment push: ${0}/wk (≈{1} new sellers)",
                        v, Gd.RoundToInt(v / MK_SELLER_COST));
            }
            return "the subcontract shop is " + (v > 0 ? "ON" : "OFF");
        }

        public static string OpRefinanceNote(GameState state, Dictionary<string, object> d)
        {
            int idx = DInt(d, "old_id", DInt(d, "v"));
            int term = DInt(d, "weeks", 12);
            Dictionary<string, object> res = RefinanceNote(state, idx, term);
            if (!(bool)res["ok"]) return (string)res["why"];
            var q = (Dictionary<string, object>)res["quote"];
            return string.Format("REFINANCED: {0:F1}%→{1:F1}%/wk over {2} wks, break fee ${3}",
                Num(q, "old_rate") * 100.0, Num(q, "new_rate") * 100.0,
                DInt(q, "term"), DInt(q, "fee"));
        }

        public static string OpFireAccount(GameState state, Dictionary<string, object> d)
        {
            Dictionary<string, object> res = FireAccount(state, DStr(d, "cat"));
            if (!(bool)res["ok"]) return (string)res["why"];
            return string.Format("FIRED {0}: −${1} penalty, the revenue dies, the street heard",
                res["who"], res["penalty"]);
        }

        public static string OpRetireProduct(GameState state, Dictionary<string, object> d)
        {
            Dictionary<string, object> res = RetireProduct(state, DStr(d, "cat", DStr(d, "v")));
            if (!(bool)res["ok"]) return (string)res["why"];
            return string.Format("RETIRED the product: {0} customers migrated, {1} churned with it",
                res["migrated"], res["churned"]);
        }
    }
}
