using System;
using System.Collections.Generic;
using System.Globalization;

namespace Runway.Core
{
    /// <summary>
    /// LANE 09 — HARDWARE PRODUCTION (build, stock, machines). Spec: docs/design/09-hardware.md
    ///
    /// Bonopoly's loop scaled to a garage: you must BUILD what you sell. Every
    /// mechanic here is a scaled-down textbook manufacturing model, named in its
    /// own comment — rough-cut capacity planning, periodic-review base-stock,
    /// inventory holding cost, Wright's experience curve, make-vs-buy premium,
    /// constant-hazard reliability, lost-sales fill rate. Zero LLM: the
    /// equipment catalog is authored, every number is engine-owned.
    ///
    /// THE ACTIVE GUARD, first line of every entry point: a run that is not
    /// Hardware never allocates the state, never draws a die, never writes a
    /// lane, never files a row. On those runs the tick is arithmetically what it
    /// was before this file had a body — that absence is a tested state (pin 4).
    ///
    /// The spine calls, in tick order (00-spine section 1, HOOKS.md):
    ///   TickPre    tick 7h — PRODUCE FIRST, before adoption can spend the shelf
    ///   TickMoney  the money section — write ONLY the P&amp;L lanes this lane owns
    ///   TickPost   after the week's record is written and can be read back
    /// and outside the tick: Directives feeds the DM block, Attention feeds
    /// every bang in the game through SimEngine.AttentionItems.
    ///
    /// TWIN LAW: this file and game/src/core/lanes/sim_factory.gd carry the same
    /// logic in the same order. The engines do NOT share PRNG internals, so
    /// parity means same checks and same behaviour, never a byte-equal draw.
    /// </summary>
    public static class SimFactory
    {
        // ── THE CONSTANTS (docs/design/09-hardware.md 3, 5, 6, 7, 8) ─────────
        /// <summary>Rough-cut capacity: an ops hire is 5..15 units/wk by skill, 10 at neutral.</summary>
        public const double HwOpsUnits = 10.0;
        /// <summary>AUTO is a periodic-review base-stock policy: review period R = 1
        /// week, order-up-to level S = 4 weeks of the smoothed forecast.</summary>
        public const double HwAutoCoverWk = 4.0;
        public const double HwAutoDemandFloor = 2.0;
        /// <summary>AUTO never spends more than a quarter of the cash in one week.</summary>
        public const double HwAutoCashShare = 0.25;
        /// <summary>Exponential smoothing on true weekly demand.</summary>
        public const double HwDemandAlpha = 0.3;
        /// <summary>Empty shelves push people out: churn x(1 + 0.35x(1 - fill rate)).</summary>
        public const double HwStarveChurn = 0.35;
        /// <summary>Wright's law on cumulative units BUILT — the linear-in-log
        /// approximation, -11.5 points per 10x, about -3.5% per doubling,
        /// floored at the purchased-BOM share.</summary>
        public const double HwLearnRate = 0.115;
        public const double HwLearnFloor = 0.65;
        /// <summary>Inventory holding cost: 2%/wk of unit cost, about 104%/yr — the
        /// obsolescence-heavy end of the 20-30%/yr durable-goods rule, compressed
        /// so it bites inside a run.</summary>
        public const double HwCarryRate = 0.02;
        public const double HwCarryMin = 0.10;
        /// <summary>Overstock is money asleep: more than 8 weeks of cover, and more than 20 units.</summary>
        public const double HwOverstockCover = 8.0;
        public const int HwOverstockMin = 20;
        /// <summary>Constant-hazard (exponential) reliability: memoryless failure at
        /// MTBF about 50 machine-weeks, MTTR floored at one tick, repair priced at
        /// a month of that machine's preventive-maintenance budget.</summary>
        public const double HwBreakP = 0.02;
        public const double HwBreakCap = 0.15;
        public const double HwRepairX = 4.0;
        /// <summary>The secondary market takes half (docs/design/DECISIONS.md #4).</summary>
        public const double HwResalePct = 0.5;
        public const int HwFleetMax = 12;
        /// <summary>What a customer buys again in a week at unit cadence — 0.2 is one
        /// unit per five weeks, the same cadence the catalog bills them at.</summary>
        public const double HwUnitCadence = 0.2;
        /// <summary>No lane floods the journal (docs/design/00-spine.md 11).</summary>
        public const int HwLineCap = 4;

        /// <summary>One row of the authored equipment catalog.</summary>
        public sealed class Machine
        {
            public string Id;
            public string Name;
            public string Era;
            public int Price;
            public double CapacityAdd;
            public double UpkeepWk;
        }

        /// <summary>
        /// THE AUTHORED EQUIPMENT CATALOG. Capacity is bought in LUMPS (stepwise
        /// expansion — nobody sells 3% of a reflow oven); upkeep is about 1.6%/wk
        /// of price, the maintenance-budget rule of thumb at this compression;
        /// and $ per unit of capacity improves with scale, which is economies of
        /// scale in capex. The LLM never touches this table: era gates,
        /// save-compat and twin parity all need it typed and stable. Ascending
        /// capacity IS the ladder the buy row walks.
        /// </summary>
        public static readonly Machine[] HwEquipment =
        {
            new Machine { Id = "jig", Name = "Assembly Jig", Era = "garage",
                Price = 900, CapacityAdd = 6.0, UpkeepWk = 15.0 },
            new Machine { Id = "pick_place", Name = "Benchtop Pick-and-Place", Era = "coworking",
                Price = 3500, CapacityAdd = 18.0, UpkeepWk = 60.0 },
            new Machine { Id = "reflow", Name = "Reflow Oven Line", Era = "coworking",
                Price = 12000, CapacityAdd = 45.0, UpkeepWk = 180.0 },
            new Machine { Id = "cnc", Name = "CNC Cell", Era = "office",
                Price = 45000, CapacityAdd = 140.0, UpkeepWk = 600.0 },
            new Machine { Id = "line", Name = "Assembly Line", Era = "floor",
                Price = 180000, CapacityAdd = 450.0, UpkeepWk = 2200.0 },
            new Machine { Id = "lightsout", Name = "Lights-Out Cell", Era = "hq",
                Price = 700000, CapacityAdd = 1500.0, UpkeepWk = 7000.0 },
        };

        /// <summary>
        /// MAKE VS BUY, era-laddered. A contract manufacturer's quote is your
        /// marginal cost plus THEIR margin, overhead and transaction costs.
        /// Relationship and committed volume narrow the premium and widen the
        /// ceiling — the era IS the relationship maturity, so this needs no state
        /// of its own. coworking: a local jobber at spot rates. office: a real CM
        /// relationship. floor/hq: supplier contract terms, volume priced in.
        /// </summary>
        public static readonly Dictionary<string, double> HwSubCapX = new Dictionary<string, double>
        {
            { "coworking", 1.0 }, { "office", 3.0 }, { "floor", 3.0 }, { "hq", 3.0 },
        };

        public static readonly Dictionary<string, double> HwSubMult = new Dictionary<string, double>
        {
            { "coworking", 1.6 }, { "office", 1.6 }, { "floor", 1.45 }, { "hq", 1.35 },
        };

        /// <summary>The refusal a desk prints where the button would have been.</summary>
        public sealed class Verdict
        {
            public bool Ok;
            public string Why = "";
            public int Back;      // resale only: what the secondary market paid
            public int Paid;      // resale only: what it cost new
            public string Name = "";
        }

        /// <summary>One cell of the desk's buy row: a catalog machine, and whether this week can sign for it.</summary>
        public sealed class BuyCell
        {
            public Machine Entry;
            public bool Ok;
            public string Why = "";
        }

        // ── STATE ────────────────────────────────────────────────────────────
        /// <summary>True only on the runs this whole file is allowed to touch.</summary>
        public static bool Active(GameState state)
        {
            return state != null && state.BizWhat == "Hardware";
        }

        /// <summary>THE ONLY PLACE ALLOCATION HAPPENS. Callers have already checked Active.</summary>
        public static HardwareState HwState(GameState state)
        {
            if (state.Hardware == null) state.Hardware = new HardwareState();
            if (state.Hardware.Equipment == null) state.Hardware.Equipment = new List<EquipmentItem>();
            return state.Hardware;
        }

        /// <summary>
        /// The read-only twin: hands back the same shape without ever writing
        /// state, so a desk repaint or an attention scan can never seed a run
        /// into existence.
        /// </summary>
        public static HardwareState HwView(GameState state)
        {
            if (state == null || state.Hardware == null) return new HardwareState();
            if (state.Hardware.Equipment == null) state.Hardware.Equipment = new List<EquipmentItem>();
            return state.Hardware;
        }

        /// <summary>
        /// THE WEEK'S WORKING BLOCK — transient display and bookkeeping data for
        /// the week just simulated, on the same contract as the pnl record
        /// (docs/design/09-hardware.md 1: durable state is a FIELD, per-week
        /// display data MAY be meta). A plain string-keyed dictionary, exactly
        /// the Godot twin's shape, so a round-tripped save reads back cleanly.
        /// </summary>
        public static Dictionary<string, object> WeekBlock(GameState state)
        {
            if (state == null) return new Dictionary<string, object>();
            var w = state.GetMeta("hw", null) as Dictionary<string, object>;
            return w ?? new Dictionary<string, object>();
        }

        static double WD(Dictionary<string, object> w, string k, double dflt)
        {
            object v;
            if (w == null || !w.TryGetValue(k, out v) || v == null) return dflt;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); }
            catch (Exception) { return dflt; }
        }

        static int WI(Dictionary<string, object> w, string k, int dflt)
        {
            object v;
            if (w == null || !w.TryGetValue(k, out v) || v == null) return dflt;
            try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); }
            catch (Exception) { return dflt; }
        }

        static string WS(Dictionary<string, object> w, string k, string dflt)
        {
            object v;
            if (w == null || !w.TryGetValue(k, out v) || v == null) return dflt;
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        static bool WB(Dictionary<string, object> w, string k, bool dflt)
        {
            object v;
            if (w == null || !w.TryGetValue(k, out v) || v == null) return dflt;
            try { return Convert.ToBoolean(v, CultureInfo.InvariantCulture); }
            catch (Exception) { return dflt; }
        }

        // ── THE FLAGSHIP BINDING (what a "unit" is) ──────────────────────────
        /// <summary>
        /// Production builds the FLAGSHIP: the first offer billed per unit. A
        /// pure selector, never a stored index — RemoveOffer shifts the list and
        /// a stored one would dangle. -1 only on a run with no catalog at all.
        /// </summary>
        public static int FlagshipIndex(GameState state)
        {
            if (state == null || state.Offers == null) return -1;
            for (int i = 0; i < state.Offers.Count; i++)
            {
                if (Math.Abs(SimEngine.OfferCadence(state.Offers[i].Unit ?? "") - HwUnitCadence) < 1e-9)
                    return i;
            }
            return state.Offers.Count > 0 ? 0 : -1;
        }

        public static Offer Flagship(GameState state)
        {
            int i = FlagshipIndex(state);
            return i < 0 ? null : state.Offers[i];
        }

        public static string FlagshipName(GameState state)
        {
            Offer f = Flagship(state);
            return f == null || string.IsNullOrEmpty(f.Name) ? "the first unit" : f.Name;
        }

        /// <summary>
        /// The production cost basis: the sum of the flagship's variable cost
        /// lines, catalog-owned and catalog-clamped. A legacy run with no offers
        /// falls back to the theta arpu the rest of the engine bills on (the 0.35
        /// margin share at unit cadence).
        /// </summary>
        public static double UnitCost(GameState state)
        {
            Offer f = Flagship(state);
            if (f == null) return Gd.Maxf(1.75 * ThetaArpu(state), 0.0);
            return Gd.Maxf(f.UnitCost, 0.0);
        }

        static double ThetaArpu(GameState state)
        {
            return state != null && state.Theta != null ? state.Theta.ArpuWk : 5.0;
        }

        /// <summary>
        /// What one unit actually invoices for — the founder's price, or the
        /// going rate while unpriced. Used only to un-bill units that never shipped.
        /// </summary>
        public static double BilledPrice(GameState state)
        {
            Offer f = Flagship(state);
            if (f == null) return Gd.Maxf(ThetaArpu(state) / HwUnitCadence, 0.0);
            return SimEngine.OfferBilledPrice(f, SimEngine.StreetFairMult(state));
        }

        // ── THE LEARNING CURVE (Wright's law, on units BUILT) ────────────────
        /// <summary>
        /// Unit cost falls a fixed fraction per DOUBLING of cumulative output.
        /// Ours is the linear-in-log approximation of C(N) = C1*N^-b, gentler
        /// than aerospace's 80-85% curves on purpose because a garage builds one
        /// product out of bought parts — and floored at 0.65 because learning
        /// compresses labor, assembly and yield, never the purchased BOM. The
        /// floor IS the material share.
        ///
        /// THE OTHER CURVE IS NOT THIS ONE (docs/design/00-spine.md 13): the
        /// catalog's ServedTotal curve discounts SERVING; this one discounts
        /// BUILDING. Neither reads the other, and subcontracted units earn neither.
        /// </summary>
        public static double Learning(GameState state)
        {
            return LearningOf(HwView(state).ProducedTotal);
        }

        public static double LearningOf(int made)
        {
            if (made <= 1) return 1.0;
            return Gd.Maxf(1.0 - HwLearnRate * (Math.Log(made) / Math.Log(10.0)), HwLearnFloor);
        }

        /// <summary>
        /// The discount as whole percent — what the strip prints and what the
        /// milestone receipt fires on (one line per new whole point, never spam).
        /// </summary>
        static int LearnStep(int made)
        {
            return (int)Math.Floor((1.0 - LearningOf(made)) * 100.0);
        }

        public static int LearningPct(GameState state)
        {
            return LearnStep(HwView(state).ProducedTotal);
        }

        // ── CAPACITY (rough-cut capacity planning) ───────────────────────────
        /// <summary>
        /// Available output = rated machine capacity + direct labor, in units per
        /// period. ONE aggregate resource pool, no routings: a weekly tick and a
        /// single flagship SKU need exactly one honest number, units/wk.
        /// `downIndex` is this week's broken machine, which contributes nothing.
        ///
        /// Ops heads are read-only coordination with 02: labor owns the roster,
        /// we only ask whether a role says "ops" and how skilled they are
        /// (default 3 = neutral, exact parity for a roster with no skill yet).
        /// </summary>
        public static double Capacity(GameState state, int downIndex = -1)
        {
            HardwareState hw = HwView(state);
            double cap = Gd.Maxf(hw.CapacityBase, 0.0);
            for (int i = 0; i < hw.Equipment.Count; i++)
            {
                if (i == downIndex) continue;
                cap += Gd.Maxf(hw.Equipment[i].CapacityAdd, 0.0);
            }
            if (state != null && state.Employees != null)
            {
                foreach (Employee e in state.Employees)
                {
                    if ((e.Role ?? "").Contains("ops"))
                        cap += HwOpsUnits * (1.0 + 0.25 * (Gd.Clampi(e.Skill, 1, 5) - 3));
                }
            }
            return Gd.Maxf(cap, 0.0);
        }

        /// <summary>
        /// THE WEEK'S BUILD ORDER: the founder's stepper, or AUTO's base-stock
        /// policy. World-clamped here and only here — the desk is never trusted.
        /// </summary>
        public static int TargetNow(GameState state, double cap, double unitCostEff)
        {
            HardwareState hw = HwView(state);
            int ceiling = Gd.Maxi((int)Math.Floor(cap), 0);
            if (hw.ProductionTarget >= 0)
            {
                // the manual stepper is uncapped by cash on purpose — going red
                // is a choice, and the reaper already prices it
                return Gd.Clampi(hw.ProductionTarget, 0, ceiling);
            }
            // AUTO: order up to 4 weeks of the smoothed forecast, minus what is
            // already on the shelf, and never a quarter of the cash in one week.
            double cover = HwAutoCoverWk * Gd.Maxf(hw.DemandEma, HwAutoDemandFloor);
            int want = Gd.Clampi(Gd.RoundToInt(cover) - hw.Stock, 0, ceiling);
            int affordable = (int)Math.Floor(HwAutoCashShare * Gd.Maxf(state.Cash, 0.0)
                / Gd.Maxf(unitCostEff, 0.01));
            return Gd.Maxi(Gd.Mini(want, affordable), 0);
        }

        /// <summary>The stepper's write path: clamped at the boundary, AUTO is -1.</summary>
        public static void SetTarget(GameState state, int v)
        {
            if (!Active(state)) return;
            HardwareState hw = HwState(state);
            if (v < 0) { hw.ProductionTarget = -1; return; }
            hw.ProductionTarget = Gd.Clampi(v, 0, Gd.Maxi((int)Math.Floor(Capacity(state)), 0));
        }

        // ── MAKE VS BUY ──────────────────────────────────────────────────────
        public static bool SubUnlocked(GameState state)
        {
            return state != null && state.EraIndex() >= 1;
        }

        public static double SubMult(string era)
        {
            double v;
            return HwSubMult.TryGetValue(era ?? "", out v) ? v : 1.6;
        }

        /// <summary>
        /// A jobber will not book unlimited line time for a small client: the
        /// ceiling is a multiple of YOUR OWN footprint, so equipment stays the
        /// growth spine and the toggle only ever buys slack.
        /// </summary>
        public static int SubCapUnits(GameState state, double cap)
        {
            if (!SubUnlocked(state)) return 0;
            double x;
            if (!HwSubCapX.TryGetValue(state.Era ?? "", out x)) x = 0.0;
            return Gd.Maxi((int)Math.Floor(x * Gd.Maxf(cap, 0.0)), 0);
        }

        public static void ToggleSubcontract(GameState state)
        {
            if (!Active(state) || !SubUnlocked(state)) return;
            HardwareState hw = HwState(state);
            hw.SubcontractOn = !hw.SubcontractOn;
        }

        // ── CARRYING, FILL, OVERSTOCK ────────────────────────────────────────
        /// <summary>
        /// What one unit costs to sit on a shelf for one week: capital tied up,
        /// storage, insurance, shrinkage and — the big one for a gadget —
        /// obsolescence.
        /// </summary>
        public static double CarryingRate(GameState state)
        {
            return Gd.Maxf(HwCarryRate * UnitCost(state), HwCarryMin);
        }

        /// <summary>Empty shelves are a retention problem, not just a sales one.</summary>
        public static double StarveChurnMult(GameState state)
        {
            double fill = Gd.Clampf(WD(WeekBlock(state), "fill", 1.0), 0.0, 1.0);
            return 1.0 + HwStarveChurn * (1.0 - fill);
        }

        public static bool Overstock(GameState state)
        {
            if (!Active(state) || state.Hardware == null) return false;
            int stock = state.Hardware.Stock;
            return stock > HwOverstockCover * Gd.Maxf(state.Hardware.DemandEma, 1.0)
                   && stock > HwOverstockMin;
        }

        /// <summary>
        /// What the books over-billed this week: repeat buyers who found empty
        /// shelves were never handed a unit, so nobody may invoice them for one
        /// (owner's law #196). Deducted through the working money record, which
        /// the spine reads back.
        /// </summary>
        public static double UnservedBilling(GameState state)
        {
            if (!Active(state)) return 0.0;
            Dictionary<string, object> w = WeekBlock(state);
            double fill = Gd.Clampf(WD(w, "fill", 1.0), 0.0, 1.0);
            return Gd.Maxf(BilledPrice(state) * HwUnitCadence * WD(w, "demand_base", 0.0)
                * (1.0 - fill), 0.0);
        }

        // ── THE EQUIPMENT CATALOG ────────────────────────────────────────────
        public static Machine CatalogEntry(string id)
        {
            for (int i = 0; i < HwEquipment.Length; i++)
                if (HwEquipment[i].Id == id) return HwEquipment[i];
            return null;
        }

        static bool EraOk(GameState state, Machine e)
        {
            return GameState.ERAS.IndexOf(state.Era) >= GameState.ERAS.IndexOf(e.Era);
        }

        /// <summary>
        /// Every refusal in one place, each with the sentence the desk prints
        /// where the button would have been — a gate that hides itself teaches
        /// nothing.
        /// </summary>
        public static Verdict CanBuy(GameState state, string id)
        {
            if (!Active(state)) return new Verdict { Ok = false, Why = "hardware runs only" };
            Machine e = CatalogEntry(id);
            if (e == null) return new Verdict { Ok = false, Why = "no such machine" };
            if (!EraOk(state, e))
                return new Verdict { Ok = false, Why = "the " + e.Era + " era unlocks it" };
            if (e.Price > SimEngine.EraSpendCap(state.Era))
                return new Verdict { Ok = false, Why = "past what a " + state.Era + " can sign for" };
            if (HwView(state).Equipment.Count >= HwFleetMax)
                return new Verdict { Ok = false, Why = "the floor holds " + HwFleetMax + " machines" };
            if (state.Cash < e.Price)
                return new Verdict { Ok = false, Why = "$" + (e.Price - state.Cash) + " short" };
            return new Verdict { Ok = true };
        }

        /// <summary>
        /// One-off cash out; the machine's capacity and upkeep are DENORMALIZED
        /// at purchase so a later catalog rebalance never rewrites an asset you own.
        /// </summary>
        public static Verdict BuyEquipment(GameState state, string id)
        {
            Verdict v = CanBuy(state, id);
            if (!v.Ok) return v;
            Machine e = CatalogEntry(id);
            HardwareState hw = HwState(state);
            state.Cash -= e.Price;
            hw.Equipment.Add(new EquipmentItem
            {
                Id = id, Name = e.Name, CapacityAdd = e.CapacityAdd,
                UpkeepWk = e.UpkeepWk, BoughtWeek = state.Week,
            });
            state.LogAction(string.Format(CultureInfo.InvariantCulture,
                "BOUGHT {0} (${1}, +{2} units/wk, ${3}/wk upkeep)",
                e.Name, e.Price, (int)e.CapacityAdd, (int)e.UpkeepWk));
            return new Verdict { Ok = true, Name = e.Name };
        }

        /// <summary>
        /// Half of what it cost — the real secondhand haircut (DECISIONS.md #4).
        /// CAPEX is forgiving and costly at the same time: the way out exists,
        /// and it bills.
        /// </summary>
        public static int ResaleValue(string id)
        {
            Machine e = CatalogEntry(id);
            return e == null ? 0 : (int)(e.Price * HwResalePct);
        }

        public static Verdict CanSell(GameState state, int idx)
        {
            if (!Active(state)) return new Verdict { Ok = false, Why = "hardware runs only" };
            List<EquipmentItem> eq = HwView(state).Equipment;
            if (idx < 0 || idx >= eq.Count) return new Verdict { Ok = false, Why = "no machine there" };
            if (CatalogEntry(eq[idx].Id) == null)
                return new Verdict { Ok = false, Why = "no buyer for that one" };
            return new Verdict { Ok = true };
        }

        public static Verdict SellEquipment(GameState state, int idx)
        {
            Verdict v = CanSell(state, idx);
            if (!v.Ok) return v;
            HardwareState hw = HwState(state);
            EquipmentItem m = hw.Equipment[idx];
            int back = ResaleValue(m.Id);
            Machine cat = CatalogEntry(m.Id);
            int paid = cat == null ? 0 : cat.Price;
            hw.Equipment.RemoveAt(idx);
            state.Cash += back;
            state.LogAction(string.Format(CultureInfo.InvariantCulture,
                "SOLD {0} for ${1} (half of ${2} — the secondhand haircut)", m.Name, back, paid));
            return new Verdict { Ok = true, Back = back, Paid = paid, Name = m.Name };
        }

        /// <summary>
        /// What the desk's buy row shows: the priciest machine this week can
        /// actually sign for, and the next rung of the ladder above it — dimmed,
        /// wearing the engine's own refusal, so the gate is visible instead of
        /// missing.
        /// </summary>
        public static List<BuyCell> BuyRow(GameState state)
        {
            var outRows = new List<BuyCell>();
            if (!Active(state)) return outRows;
            var legal = new List<int>();
            for (int i = 0; i < HwEquipment.Length; i++)
                if (EraOk(state, HwEquipment[i])) legal.Add(i);
            if (legal.Count == 0) return outRows;
            int pick = legal[0];
            for (int i = 0; i < legal.Count; i++)
                if (CanBuy(state, HwEquipment[legal[i]].Id).Ok) pick = legal[i];
            outRows.Add(BuyCellFor(state, pick));
            if (pick + 1 < HwEquipment.Length) outRows.Add(BuyCellFor(state, pick + 1));
            return outRows;
        }

        static BuyCell BuyCellFor(GameState state, int i)
        {
            Machine e = HwEquipment[i];
            Verdict v = CanBuy(state, e.Id);
            return new BuyCell { Entry = e, Ok = v.Ok, Why = v.Why };
        }

        // ═══════════════════════ THE WEEKLY TICK ═════════════════════════════

        /// <summary>
        /// Tick 7h: breakdown roll, capacity, build target, produce (learning
        /// curve). PRODUCE FIRST — stock must exist before section 8 is allowed
        /// to sell it. Without it week one would lose its launch: the shelf would
        /// be empty before any decision existed. The draw order is FIXED
        /// (replay-exact): breakdown randf, then the picked machine, then arithmetic.
        /// </summary>
        public static void TickPre(GameState state, WeeklyReport rep)
        {
            if (!Active(state)) return;
            HardwareState hw = HwState(state);
            Rng r = SimEngine.RngForSalt(state, SimEngine.SALT_HW_BREAKDOWN);
            int downIndex = -1;
            string downName = "";
            double repair = 0.0;
            if (hw.Equipment.Count > 0
                && r.Randf() < Gd.Minf(HwBreakP * hw.Equipment.Count, HwBreakCap))
            {
                downIndex = r.RandiRange(0, hw.Equipment.Count - 1);
                EquipmentItem m = hw.Equipment[downIndex];
                downName = m.Name;
                // corrective repair, priced at about a month of that machine's
                // preventive-maintenance budget. One week down, then it runs
                // again — MTTR of one tick keeps the repair queue at zero state.
                repair = HwRepairX * m.UpkeepWk;
            }
            double cap = Capacity(state, downIndex);
            int madeBefore = hw.ProducedTotal;
            double ucEff = UnitCost(state) * Learning(state);
            int built = TargetNow(state, cap, ucEff);
            hw.Stock += built;
            hw.ProducedTotal = madeBefore + built;
            double util = cap <= 0.0 ? 0.0 : Gd.Clampf(built / cap, 0.0, 1.0);
            // the week's working block: everything 8, 9 and the strip read back
            var w = new Dictionary<string, object>
            {
                { "week", state.Week }, { "built", built }, { "capacity", cap },
                { "utilization", util }, { "unit_cost_eff", ucEff },
                { "down_name", downName }, { "down_i", downIndex }, { "repair", repair },
                { "sub_units", 0 }, { "lost_adds", 0 }, { "fill", 1.0 },
                { "sold", 0 }, { "served", 0 },
                { "demand_base", (double)state.Traction }, { "demand_units", 0.0 },
                { "shelf", hw.Stock }, { "stock_end", hw.Stock },
                { "carrying", 0.0 }, { "upkeep", 0.0 }, { "walked", 0 },
                { "learn_step", LearnStep(hw.ProducedTotal) },
                { "learn_step_up", LearnStep(hw.ProducedTotal) > LearnStep(madeBefore) },
            };
            state.SetMeta("hw", w);
        }

        /// <summary>
        /// THE STOCK SEAM (tick 8, after the go-to-market clamp — you cannot sell
        /// from a shelf faster than the team can close, and you cannot sell at
        /// all from a shelf that is empty). Off Hardware this hands `adds`
        /// straight back and draws nothing, so demand stays stock-free.
        ///
        /// Lost-sales retail, not backorders: consumer hardware does not queue.
        /// Unmet demand is gone, receipted, and it pushes the people it
        /// disappointed out.
        /// </summary>
        public static double ClampAdds(GameState state, WeeklyReport rep, double adds)
        {
            if (!Active(state)) return adds;
            HardwareState hw = HwState(state);
            Dictionary<string, object> w = WeekBlock(state);
            Rng r = SimEngine.RngForSalt(state, SimEngine.SALT_HW_REPURCHASE);
            double A = state.Traction;
            // EXISTING CUSTOMERS COME BACK: at unit cadence a customer buys again
            // about every five weeks. The seeded remainder keeps a 0.4-unit week real.
            int uExist = SeededInt(A * HwUnitCadence, r);
            int served = Gd.Mini(uExist, hw.Stock);
            hw.Stock -= served;
            int unservedExist = uExist - served;
            double addsRaw = Gd.Maxf(adds, 0.0);
            int shortAdds = Gd.Maxi((int)Math.Ceiling(addsRaw - hw.Stock), 0);
            // MAKE VS BUY: made-to-order overflow. Sub units serve the people
            // already waiting first, then new customers; they NEVER enter stock,
            // never bill carrying, and teach the bench nothing (no ProducedTotal,
            // no learning).
            int subUnits = 0;
            int subToAdds = 0;
            if (hw.SubcontractOn && SubUnlocked(state))
            {
                int want = unservedExist + shortAdds;
                subUnits = Gd.Maxi(Gd.Mini(want, SubCapUnits(state, WD(w, "capacity", Capacity(state)))), 0);
                int toExist = Gd.Mini(subUnits, unservedExist);
                served += toExist;
                subToAdds = subUnits - toExist;
            }
            adds = Gd.Minf(addsRaw, hw.Stock + (double)subToAdds);
            int lost = Gd.Maxi(Gd.RoundToInt(addsRaw - adds), 0);
            // a new customer's first unit ships at signup; the books keep billing
            // the catalog's smoothed ARPU-week (12: divergence is under a unit a week)
            int offShelf = Gd.Mini(Gd.RoundToInt(Gd.Minf(adds, hw.Stock)), hw.Stock);
            hw.Stock -= offShelf;
            // THE FORECAST the base-stock policy orders against: plain
            // exponential smoothing over TRUE demand — what people wanted, not
            // what we managed to hand over. Forecasting on served units would
            // starve a starving factory.
            hw.DemandEma = (1.0 - HwDemandAlpha) * hw.DemandEma
                + HwDemandAlpha * (uExist + addsRaw);
            w["sub_units"] = subUnits;
            w["served"] = served;
            w["sold"] = offShelf + served + subToAdds;
            w["lost_adds"] = lost;
            w["fill"] = uExist == 0 ? 1.0 : Gd.Clampf((double)served / uExist, 0.0, 1.0);
            w["demand_units"] = uExist + addsRaw;
            w["demand_base"] = A;
            w["stock_end"] = hw.Stock;
            return adds;
        }

        /// <summary>
        /// Godot's own seeded-remainder idiom: a 0.4-unit week is a REAL week,
        /// and rounding would erase it forever.
        /// </summary>
        static int SeededInt(double x, Rng r)
        {
            double v = Gd.Maxf(x, 0.0);
            int whole = (int)Math.Floor(v);
            if (r.Randf() < v - whole) whole += 1;
            return whole;
        }

        /// <summary>
        /// The money section. Four lanes, all of them joining burn: what the
        /// bench built, what the contract manufacturer charged, what the fleet
        /// costs to keep, and what the shelf costs to hold.
        /// </summary>
        public static void TickMoney(GameState state, WeeklyReport rep, MoneyWork m)
        {
            if (!Active(state)) return;
            HardwareState hw = HwState(state);
            Dictionary<string, object> w = WeekBlock(state);
            int built = WI(w, "built", 0);
            double ucEff = WD(w, "unit_cost_eff", UnitCost(state) * Learning(state));
            m.Production += built * ucEff;
            // the sub's price is the sub's price: no learning discount rides it
            double subCost = WI(w, "sub_units", 0) * SubMult(state.Era) * UnitCost(state);
            m.Subcontract += subCost;
            // FIXED COST: idle machines still cost, and a broken one costs more
            double upkeep = 0.0;
            for (int i = 0; i < hw.Equipment.Count; i++)
                upkeep += Gd.Maxf(hw.Equipment[i].UpkeepWk, 0.0);
            double repair = WD(w, "repair", 0.0);
            m.EquipUpkeep += upkeep + repair;
            // only units that actually sit into next week bill: what was built
            // and sold this week never paid rent on a shelf
            int stockEnd = hw.Stock;
            double carry = stockEnd * CarryingRate(state);
            m.Carrying += carry;
            // HONEST BILLING: a repeat buyer who found the shelf empty was never
            // handed a unit, so nobody invoices them for one (owner's law #196).
            // The catalog bills a smoothed ARPU-week; this takes back the share
            // that never shipped.
            double lostBilling = Gd.Minf(UnservedBilling(state), Gd.Maxf(m.Revenue, 0.0));
            m.Revenue -= lostBilling;
            w["upkeep"] = upkeep;
            w["carrying"] = carry;
            w["stock_end"] = stockEnd;
            w["sub_cost"] = subCost;
            w["lost_billing"] = lostBilling;
            w["production"] = built * ucEff;
        }

        /// <summary>
        /// After the record is written: the closed week's consequences. Empty
        /// shelves push people out (the churn the fill rate earned), then the
        /// receipts — every one of them naming its WHY in the same clause as its
        /// number.
        /// </summary>
        public static void TickPost(GameState state, WeeklyReport rep)
        {
            if (!Active(state)) return;
            Dictionary<string, object> w = WeekBlock(state);
            if (w.Count == 0) return;
            double fill = Gd.Clampf(WD(w, "fill", 1.0), 0.0, 1.0);
            // x(1 + 0.35x(1 - fill)) on the week's churn: a repeat buyer who
            // found the shelf empty is a customer with a reason to leave.
            int walked = 0;
            if (fill < 1.0)
            {
                walked = Gd.Maxi(Gd.RoundToInt(rep.Churn * (StarveChurnMult(state) - 1.0)), 0);
                walked = Gd.Mini(walked, state.Traction);
                if (walked > 0) state.Traction = Gd.Maxi(state.Traction - walked, 0);
            }
            w["walked"] = walked;
            Receipts(state, rep, w);
        }

        /// <summary>
        /// THE JOURNAL, capped at four lines a week so no lane floods the page
        /// (docs/design/00-spine.md 11). Loudest first: the decisions the founder
        /// has to make outrank the bookkeeping that explains them.
        /// </summary>
        static void Receipts(GameState state, WeeklyReport rep, Dictionary<string, object> w)
        {
            var lines = new List<string>();
            int lost = WI(w, "lost_adds", 0);
            if (lost > 0)
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "STOCKOUT — {0} sales lost (demand {1}, shelf {2}): add capacity or subcontract",
                    lost, Gd.RoundToInt(WD(w, "demand_units", 0.0)), WI(w, "shelf", 0)));
                // a founder retells their first stockout for years — a BIG beat, once
                if (!state.HasFlag("first_stockout"))
                {
                    state.SetFlag("first_stockout");
                    rep.Events.Add(string.Format(CultureInfo.InvariantCulture,
                        "THE FIRST STOCKOUT — {0} sales walked off an empty shelf", lost));
                }
            }
            int walked = WI(w, "walked", 0);
            if (walked > 0)
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "−{0} customers walked (fill rate {1}% — repeat buyers found empty shelves)",
                    walked, Gd.RoundToInt(WD(w, "fill", 1.0) * 100.0)));
            }
            double unbilled = WD(w, "lost_billing", 0.0);
            if (unbilled >= 1.0)
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "unserved repeat buyers: −${0} (nobody is invoiced for a unit that never shipped)",
                    Gd.RoundToInt(unbilled)));
            }
            int built = WI(w, "built", 0);
            if (built > 0)
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "built {0} units at ${1} each (utilization {2}% — idle capacity still bills upkeep)",
                    built, WD(w, "unit_cost_eff", 0.0).ToString("F2", CultureInfo.InvariantCulture),
                    Gd.RoundToInt(WD(w, "utilization", 0.0) * 100.0)));
            }
            int subUnits = WI(w, "sub_units", 0);
            if (subUnits > 0)
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "make vs buy: subcontracted {0} units −${1} ({2}× unit cost — their margin, your sale, none of your learning)",
                    subUnits, Gd.RoundToInt(WD(w, "sub_cost", 0.0)),
                    SubMult(state.Era).ToString("0.##", CultureInfo.InvariantCulture)));
            }
            string downName = WS(w, "down_name", "");
            if (!string.IsNullOrEmpty(downName))
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "machine down: {0} (repair −${1} — one week idle, then it runs again)",
                    downName, Gd.RoundToInt(WD(w, "repair", 0.0))));
            }
            double carry = WD(w, "carrying", 0.0);
            if (carry >= 1.0)
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "carrying {0} units: −${1} (2%/wk of unit cost — money parked on shelves)",
                    WI(w, "stock_end", 0), Gd.RoundToInt(carry)));
            }
            if (WB(w, "learn_step_up", false) && WI(w, "learn_step", 0) > 0)
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "learning curve: unit cost −{0}% ({1} built — practice makes cheaper)",
                    WI(w, "learn_step", 0),
                    HwView(state).ProducedTotal.ToString("N0", CultureInfo.InvariantCulture)));
            }
            for (int i = 0; i < Gd.Mini(lines.Count, HwLineCap); i++) rep.Lines.Add(lines[i]);
        }

        /// <summary>
        /// DM context lines, section 13 of the DIRECTIVES block
        /// (docs/design/00-spine.md 5). The narrator gets to describe factory
        /// pain and is never handed a number it could have invented.
        /// </summary>
        public static List<string> Directives(GameState state)
        {
            var outLines = new List<string>();
            if (!Active(state) || state.Hardware == null) return outLines;
            Dictionary<string, object> w = WeekBlock(state);
            outLines.Add(string.Format(CultureInfo.InvariantCulture,
                "- Stock: {0} units (made {1}, sold {2} last week).",
                state.Hardware.Stock, WI(w, "built", 0), WI(w, "sold", 0)));
            if (WI(w, "lost_adds", 0) > 0)
            {
                outLines.Add(string.Format(CultureInfo.InvariantCulture,
                    "- STOCKOUT: demand outran stock ({0} sales lost, fill {1}%).",
                    WI(w, "lost_adds", 0), Gd.RoundToInt(WD(w, "fill", 1.0) * 100.0)));
            }
            return outLines;
        }

        /// <summary>
        /// Attention rows — the product desk. Labels are 40 characters or less
        /// because the garage ticker prints them verbatim, and they name the
        /// problem in the term the player is here to learn.
        /// </summary>
        public static List<AttentionItem> Attention(GameState state)
        {
            var rows = new List<AttentionItem>();
            if (!Active(state) || state.Hardware == null) return rows;
            Dictionary<string, object> w = WeekBlock(state);
            if (WI(w, "lost_adds", 0) > 0)
            {
                rows.Add(new AttentionItem { Desk = "product", Key = "stockout", Severity = 3,
                    Label = "stockout — " + WI(w, "lost_adds", 0) + " sales lost" });
            }
            if (Overstock(state))
            {
                rows.Add(new AttentionItem { Desk = "product", Key = "overstock", Severity = 2,
                    Label = "overstock — cash parked on shelves" });
            }
            string downName = WS(w, "down_name", "");
            if (!string.IsNullOrEmpty(downName))
            {
                rows.Add(new AttentionItem { Desk = "product", Key = "machine_down", Severity = 2,
                    Label = "machine down: " + Gd.Left(downName, 26) });
            }
            return rows;
        }
    }
}
