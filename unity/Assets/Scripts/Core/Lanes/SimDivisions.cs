using System.Collections.Generic;
using System.Globalization;

namespace Runway.Core
{
    /// <summary>
    /// LANE — DIVISIONS &amp; SITES. Spec: docs/design/DECISIONS.md (THE DIVISION
    /// MECHANIC, ARRANGE MODE + ARRANGE EDITS THE BINS, THE MUTATION LAW, THE
    /// PRICE BOOK) + docs/design/DAG2.md (L-DIVWORKS).
    ///
    /// THE LAW: divisions are NEVER generated — they are engine state, born
    /// only from real ops (open_site and its mirrors). The LLM names and
    /// dresses; it never invents a division, a capacity or a dollar. Every
    /// dollar already has an address (employee.site, machine.site,
    /// offer.product_id, rent per site, spend_book[].division); a division's
    /// book is a GROUP-BY, never an invention. What genuinely has no address —
    /// the founder, brand marketing, the era's own roof — lands on one honest
    /// SHARED/HQ row, never smeared (allocated vs direct cost IS the lesson).
    ///
    /// ROOF COUNTING: `Sites` holds the roofs OPENED by ops; the era's own
    /// roof (site id "") is the company's first. "Open a second studio in
    /// Lyon" writes the FIRST site record and the company then has TWO roofs —
    /// so the empire rung fires at SiteDivisions() >= 2 (home + opened).
    ///
    /// THE PRICE BOOK is the ONLY source for structural costs; Pb() clamps
    /// every read and answers mid-band defaults so a keyless run plays.
    /// open_site_pack is a FLAT pack; the era scaling on top is engine math.
    ///
    /// DURABLE MARKERS: ramp / offline / bleeding counters ride state.Flags as
    /// "works_ramp:&lt;name&gt;:&lt;wk&gt;" / "works_off:&lt;name&gt;:&lt;wk&gt;" /
    /// "works_red:&lt;site&gt;:&lt;n&gt;" — the one untyped, save-whole list both
    /// engines share. Pruned by TickPre.
    ///
    /// The spine calls: TickPre §6c (ramps settle before the market splits
    /// demand), TickMoney (owns ONLY m.SiteRent), TickPost (per-site learning
    /// + the bleeding flag read the finished week), Directives, Attention.
    ///
    /// SALTS: SALT_DIV_SITES (130) — quotes + the close-site customer split,
    /// keyed (seed, week) so a preview equals its booking all week.
    /// SALT_DIV_NAMES (131) — the keyless site-name pool. Draw order per call
    /// site is FIXED. The two engines do not share PRNG internals: draws pin
    /// BEHAVIOUR (bands, order), never bits.
    ///
    /// TWIN LAW: game/src/core/lanes/sim_divisions.gd carries the same logic
    /// in the same order.
    /// </summary>
    public static class SimDivisions
    {
        // ── THE PRICE BOOK BANDS (lo, hi, mid-band default = L-GEN's own) ──
        static readonly Dictionary<string, double[]> PB_BANDS = new Dictionary<string, double[]>
        {
            { "open_site_pack", new[] { 6000.0, 40000.0, 18000.0 } },
            { "relocation_fee", new[] { 100.0, 1500.0, 400.0 } },
            { "machine_shipping", new[] { 150.0, 4000.0, 900.0 } },
            { "lease_break_weeks", new[] { 4.0, 16.0, 8.0 } },
            { "contract_notice_wks", new[] { 2.0, 12.0, 4.0 } },
            { "refinance_break_fee", new[] { 100.0, 2000.0, 350.0 } },
            { "freelance_rate", new[] { 15.0, 300.0, 60.0 } },
            { "subcontract_rate", new[] { 10.0, 250.0, 45.0 } },
            { "account_fire_penalty", new[] { 200.0, 5000.0, 1200.0 } },
        };

        /// The pack is quoted "by era" (DECISIONS): flat pack × the era scale.
        static readonly Dictionary<string, double> ERA_PACK_MULT = new Dictionary<string, double>
        {
            { "garage", 0.5 }, { "coworking", 0.7 }, { "office", 1.0 },
            { "floor", 2.2 }, { "hq", 5.0 },
        };
        /// A second roof rents at a fraction of the era's own roof, jittered.
        const double SITE_RENT_LO = 0.45;
        const double SITE_RENT_HI = 0.85;
        /// Local wage regions the world quotes from (a site keeps its mult).
        static readonly double[] WAGE_TABLE = { 0.85, 0.92, 1.0, 1.08, 1.15 };
        /// A new roof ramps its local demand on its own curve.
        public const double SITE_OPEN_WEIGHT = 0.15;
        public const double SITE_RAMP_K = 0.10;
        /// Closing: this fraction of the roof's customers transfers (fragile).
        const double CLOSE_TRANSFER_LO = 0.35;
        const double CLOSE_TRANSFER_HI = 0.50;
        /// A site bleeding this many weeks past its ramp grace wears the alarm.
        public const int RED_WEEKS = 3;
        public const int RAMP_GRACE_WK = 8;
        /// Re-leasing prices one week of the NEW rent + a moving week of demand.
        const double RELEASE_DIP = 0.85;
        /// The keyless site-name pool (the DM proposes real names on keyed runs).
        public static readonly string[] NAME_POOL = { "Lyon", "Harbor East", "Northside",
            "Old Mill", "Riverside", "Midtown", "The Annex", "Southgate",
            "Lakeview", "The Depot" };

        // ═════════════════════════ THE PRICE BOOK ═════════════════════════

        /// <summary>THE ONE READ — clamped to the band whatever wrote it;
        /// mid-band default when missing. Keyless runs must play.</summary>
        public static double Pb(GameState state, string key)
        {
            double[] band;
            if (!PB_BANDS.TryGetValue(key, out band)) return 0.0;
            double v = band[2];
            object raw;
            if (state.PriceBook != null && state.PriceBook.TryGetValue(key, out raw)
                && raw != null)
            {
                try { v = System.Convert.ToDouble(raw, CultureInfo.InvariantCulture); }
                catch { v = band[2]; }
            }
            return Gd.Clampf(v, band[0], band[1]);
        }

        /// <summary>The flat generated pack × the era scale, rounded to $50.</summary>
        public static int OpenPackCost(GameState state)
        {
            double mult;
            if (!ERA_PACK_MULT.TryGetValue(state.Era, out mult)) mult = 1.0;
            double pack = Pb(state, "open_site_pack") * mult;
            return Gd.RoundToInt(pack / 50.0) * 50;
        }

        // ═════════════════════════ ROOFS &amp; RUNGS ═══════════════════════════

        public static Site SiteById(GameState state, string id)
        {
            for (int i = 0; i < state.Sites.Count; i++)
                if (state.Sites[i].Id == id) return state.Sites[i];
            return null;
        }

        static List<EquipmentItem> Eq(GameState state)
        {
            return state.Hardware != null && state.Hardware.Equipment != null
                ? state.Hardware.Equipment : new List<EquipmentItem>();
        }

        /// <summary>The era's own roof never empties: the FOUNDER works under
        /// it (their hands are the home roof's floor in the works), so the
        /// home division always exists.</summary>
        public static bool HomeOccupied(GameState state)
        {
            return true;
        }

        /// <summary>Divisions on the site axis: the home roof (while occupied)
        /// + every opened roof. Two roofs = an empire of two studios.</summary>
        public static int SiteDivisions(GameState state)
        {
            return state.Sites.Count + (HomeOccupied(state) ? 1 : 0);
        }

        /// <summary>Distinct products across the catalog ("" = flagship).</summary>
        public static int ProductsCount(GameState state)
        {
            if (state.Offers.Count == 0) return 0;
            var seen = new HashSet<string>();
            for (int i = 0; i < state.Offers.Count; i++)
                seen.Add(state.Offers[i].ProductId ?? "");
            return seen.Count;
        }

        /// <summary>THE RUNG RULE — deterministic counts, no judgment: site
        /// divisions ≥ 2 → 3 · products ≥ 2 → 3 · offers ≥ 3 → 2 · else 1.</summary>
        public static int Rung(GameState state)
        {
            if (SiteDivisions(state) >= 2 || ProductsCount(state) >= 2) return 3;
            if (state.Offers.Count >= 3) return 2;
            return 1;
        }

        /// <summary>"Sliced by" lists ONLY axes with ≥2 populated divisions.</summary>
        public static List<string> SliceAxes(GameState state)
        {
            var outp = new List<string>();
            if (SiteDivisions(state) >= 2) outp.Add("site");
            if (ProductsCount(state) >= 2) outp.Add("product");
            if (state.Offers.Count >= 2) outp.Add("offer");
            return outp;
        }

        public static string DefaultSlice(GameState state)
        {
            if (SiteDivisions(state) >= 2) return "site";
            if (ProductsCount(state) >= 2) return "product";
            return "offer";
        }

        /// <summary>The per-site learning curve — the engine's own Janoschek
        /// shape on the site's OWN count (Lyon $27 vs Geneva $36, mechanical).</summary>
        public static double SiteLc(int count)
        {
            if (count <= 1) return 1.0;
            return Gd.Maxf(1.0 - 0.115 * (System.Math.Log(count) / System.Math.Log(10.0)), 0.65);
        }

        // ═════════════════ QUOTES (pure, week-stable) ══════════════════════

        /// <summary>The open-a-roof quote, drawn on SALT_DIV_SITES/(seed, week)
        /// so the ghost bin's preview and the signed booking carry the SAME
        /// numbers all week. Draw order FIXED: ① name (SALT_DIV_NAMES)
        /// ② rent factor ③ wage region.</summary>
        public static Dictionary<string, object> QuoteSite(GameState state)
        {
            Rng rn = SimEngine.RngForSalt(state, SimEngine.SALT_DIV_NAMES);
            string name = NAME_POOL[(int)(rn.Randi() % (uint)NAME_POOL.Length)];
            var used = new HashSet<string>();
            for (int i = 0; i < state.Sites.Count; i++) used.Add(state.Sites[i].Name);
            int tries = 0;
            while (used.Contains(name) && tries < NAME_POOL.Length)
            {
                name = NAME_POOL[(int)(rn.Randi() % (uint)NAME_POOL.Length)];
                tries += 1;
            }
            Rng r = SimEngine.RngForSalt(state, SimEngine.SALT_DIV_SITES);
            int eraRent;
            if (!GameState.ERA_RENT.TryGetValue(state.Era, out eraRent)) eraRent = 150;
            int rent = Gd.RoundToInt(eraRent * r.RandfRange(SITE_RENT_LO, SITE_RENT_HI) / 10.0) * 10;
            double wage = WAGE_TABLE[(int)(r.Randi() % (uint)WAGE_TABLE.Length)];
            return new Dictionary<string, object>
            {
                { "pack", OpenPackCost(state) }, { "rent_wk", Gd.Maxi(rent, 40) },
                { "wage_mult", wage }, { "name", name },
            };
        }

        /// <summary>The pack decomposed for the receipt — derived, always sums.</summary>
        public static List<Dictionary<string, object>> PackLines(int pack)
        {
            int deposit = Gd.RoundToInt(pack * 0.25);
            int capex = Gd.RoundToInt(pack * 0.40);
            return new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { { "label", "lease deposit" }, { "amount", deposit } },
                new Dictionary<string, object> { { "label", "fit-out & kit" }, { "amount", capex } },
                new Dictionary<string, object> { { "label", "the hire pack" }, { "amount", pack - deposit - capex } },
            };
        }

        // ═══════════════════ THE SITE EXECUTORS ════════════════════════════

        /// <summary>OPEN A ROOF — one op, two doors (the written move and the
        /// arrange ghost bin). Engine is the bouncer: era cap and cash answer
        /// before a record is born. The DM's only role is the name.</summary>
        public static Dictionary<string, object> OpenSite(GameState state, string name = "")
        {
            Dictionary<string, object> q = QuoteSite(state);
            int pack = System.Convert.ToInt32(q["pack"], CultureInfo.InvariantCulture);
            if (pack > SimEngine.EraSpendCap(state.Era))
                return new Dictionary<string, object> { { "ok", false },
                    { "why", "past what a " + state.Era + " can sign for" } };
            if (state.Cash < pack)
                return new Dictionary<string, object> { { "ok", false },
                    { "why", "$" + (pack - state.Cash) + " short of the pack" } };
            int n = 1;
            for (int i = 0; i < state.Sites.Count; i++)
            {
                string sid = state.Sites[i].Id ?? "";
                if (sid.StartsWith("site_"))
                {
                    int idx;
                    if (int.TryParse(sid.Substring(5), out idx)) n = Gd.Maxi(n, idx + 1);
                }
            }
            string nm = (name ?? "").Trim();
            if (nm.Length > 24) nm = nm.Substring(0, 24);
            var site = new Site
            {
                Id = "site_" + n,
                Name = nm.Length > 0 ? nm : (string)q["name"],
                RentWk = System.Convert.ToInt32(q["rent_wk"], CultureInfo.InvariantCulture),
                WageMult = System.Convert.ToDouble(q["wage_mult"], CultureInfo.InvariantCulture),
                LearningCount = 0, DemandWeight = SITE_OPEN_WEIGHT, OpenedWk = state.Week,
            };
            state.Sites.Add(site);
            state.Cash -= pack;
            state.LogAction(string.Format(
                "OPENED {0}: −${1} (deposit + fit-out + hires), rent ${2}/wk, ramp from wk {3}",
                site.Name, pack, site.RentWk, state.Week));
            return new Dictionary<string, object> { { "ok", true }, { "why", "" },
                { "site", site }, { "pack", pack } };
        }

        /// <summary>Rename is INK — free, dressing only.</summary>
        public static bool RenameSite(GameState state, string id, string name)
        {
            Site s = SiteById(state, id);
            string nm = (name ?? "").Trim();
            if (s == null || nm.Length == 0) return false;
            s.Name = nm.Length > 24 ? nm.Substring(0, 24) : nm;
            return true;
        }

        /// <summary>RE-LEASE quote — dir +1 takes a bigger roof (+25% rent),
        /// −1 a smaller (−20%). Fee = one week of the NEW rent.</summary>
        public static Dictionary<string, object> RelaseQuote(GameState state, string id, int dir)
        {
            Site s = SiteById(state, id);
            if (s == null) return new Dictionary<string, object>();
            int rent = s.RentWk;
            int newRent = Gd.RoundToInt(rent * (dir >= 0 ? 1.25 : 0.8) / 10.0) * 10;
            int eraRent;
            if (!GameState.ERA_RENT.TryGetValue(state.Era, out eraRent)) eraRent = 150;
            newRent = Gd.Clampi(newRent, 40, eraRent * 2);
            return new Dictionary<string, object> { { "fee", newRent },
                { "new_rent", newRent }, { "old_rent", rent } };
        }

        public static Dictionary<string, object> EditSite(GameState state, string id, int dir)
        {
            Dictionary<string, object> q = RelaseQuote(state, id, dir);
            if (q.Count == 0)
                return new Dictionary<string, object> { { "ok", false }, { "why", "no such roof" } };
            int fee = System.Convert.ToInt32(q["fee"], CultureInfo.InvariantCulture);
            if (state.Cash < fee)
                return new Dictionary<string, object> { { "ok", false },
                    { "why", "$" + (fee - state.Cash) + " short of the moving week" } };
            Site s = SiteById(state, id);
            state.Cash -= fee;
            s.RentWk = System.Convert.ToInt32(q["new_rent"], CultureInfo.InvariantCulture);
            s.DemandWeight = Gd.Maxf(s.DemandWeight * RELEASE_DIP, 0.05);
            state.LogAction(string.Format(
                "RE-LEASED {0}: rent ${1}→${2}/wk, −${3} and a moving week",
                s.Name, q["old_rent"], q["new_rent"], fee));
            return new Dictionary<string, object> { { "ok", true }, { "why", "" },
                { "fee", fee }, { "new_rent", s.RentWk } };
        }

        // ── the teardown wizard's arithmetic (pure), then its booking ──────

        /// <summary>Everything closing this roof costs and frees, one decision
        /// per element. decisions: {"e:&lt;i&gt;": "go"|"move:&lt;site&gt;",
        /// "m:&lt;j&gt;": "sell"|"move:&lt;site&gt;"}. Returns priced lines + the
        /// derived verdict incl. the payback line.</summary>
        public static Dictionary<string, object> CloseQuote(GameState state, string id,
            Dictionary<string, string> decisions)
        {
            Site s = SiteById(state, id);
            if (s == null) return new Dictionary<string, object>();
            decisions = decisions ?? new Dictionary<string, string>();
            var lines = new List<Dictionary<string, object>>();
            int cashNow = 0;
            int freedWk = 0;
            int moves = 0;
            int gos = 0;
            int reloc = Gd.RoundToInt(Pb(state, "relocation_fee"));
            for (int i = 0; i < state.Employees.Count; i++)
            {
                Employee e = state.Employees[i];
                if ((e.Site ?? "") != id) continue;
                string d;
                if (!decisions.TryGetValue("e:" + i, out d)) d = "go";
                if (d.StartsWith("move:")) { moves += 1; cashNow -= reloc; }
                else
                {
                    gos += 1;
                    int sev = SimLabor.SeveranceFor(state, e);
                    cashNow -= sev;
                    freedWk += e.Salary;
                }
            }
            if (moves > 0)
                lines.Add(new Dictionary<string, object>
                    { { "label", moves + " move (relocation + 1-wk ramp)" }, { "amount", -reloc * moves } });
            if (gos > 0)
            {
                int sevTotal = 0;
                for (int i2 = 0; i2 < state.Employees.Count; i2++)
                {
                    Employee e2 = state.Employees[i2];
                    string d2;
                    if (!decisions.TryGetValue("e:" + i2, out d2)) d2 = "go";
                    if ((e2.Site ?? "") == id && !d2.StartsWith("move:"))
                        sevTotal += SimLabor.SeveranceFor(state, e2);
                }
                lines.Add(new Dictionary<string, object>
                    { { "label", gos + " let go — severance is always owed" }, { "amount", -sevTotal } });
            }
            int ship = Gd.RoundToInt(Pb(state, "machine_shipping"));
            List<EquipmentItem> eq = Eq(state);
            for (int j = 0; j < eq.Count; j++)
            {
                EquipmentItem m = eq[j];
                if ((m.Site ?? "") != id) continue;
                string dm;
                if (!decisions.TryGetValue("m:" + j, out dm)) dm = "sell";
                if (dm.StartsWith("move:"))
                {
                    cashNow -= ship;
                    lines.Add(new Dictionary<string, object>
                        { { "label", m.Name + " moves (a week offline)" }, { "amount", -ship } });
                }
                else
                {
                    int back = SimFactory.ResaleValue(m.Id);
                    cashNow += back;
                    lines.Add(new Dictionary<string, object>
                        { { "label", m.Name + " sold at half" }, { "amount", back } });
                }
            }
            int rent = s.RentWk;
            int brkWeeks = Gd.RoundToInt(Pb(state, "lease_break_weeks"));
            int brk = brkWeeks * rent;
            cashNow -= brk;
            freedWk += rent;
            lines.Add(new Dictionary<string, object>
                { { "label", "the lease, broken mid-term (" + brkWeeks + " wks of rent)" }, { "amount", -brk } });
            // THE CUSTOMERS ARE DECIDED FOR YOU: one salted, week-stable draw.
            double share = DemandShare(state, id);
            int cust = Gd.RoundToInt(state.Traction * share);
            Rng r = SimEngine.RngForSalt(state, SimEngine.SALT_DIV_SITES);
            double transferFrac = Gd.Clampf(
                CLOSE_TRANSFER_LO + (CLOSE_TRANSFER_HI - CLOSE_TRANSFER_LO) * r.Randf(), 0.0, 1.0);
            int kept = (int)System.Math.Floor(cust * transferFrac);
            int lost = cust - kept;
            double revPerCust = SimEngine.OffersArpu(state);
            if (revPerCust < 0.0)
                revPerCust = (state.Theta != null ? state.Theta.ArpuWk : 4.0) * state.PriceMult;
            int lostRevWk = Gd.RoundToInt(lost * revPerCust);
            int siteMarginWk = 0;
            List<Dictionary<string, object>> book = WorksBook(state, "site");
            for (int b = 0; b < book.Count; b++)
                if ((string)book[b]["id"] == id)
                    siteMarginWk = System.Convert.ToInt32(book[b]["net_wk"], CultureInfo.InvariantCulture);
            int netFreed = freedWk - lostRevWk;
            int payback = -1;
            if (cashNow < 0 && netFreed > 0)
                payback = (int)System.Math.Ceiling((double)(-cashNow) / netFreed);
            return new Dictionary<string, object>
            {
                { "lines", lines }, { "net_now", cashNow }, { "freed_wk", freedWk },
                { "lost_rev_wk", lostRevWk }, { "kept", kept }, { "lost", lost },
                { "payback_wk", payback }, { "site_margin_wk", siteMarginWk },
            };
        }

        /// <summary>CLOSE THE ROOF — the composite receipt booked whole.
        /// Obligations survive removal: severance always owed, the lease
        /// penalty bills, the lost customers leave now, the fragile transfers
        /// carry a churn cloud.</summary>
        public static Dictionary<string, object> CloseSite(GameState state, string id,
            Dictionary<string, string> decisions)
        {
            Site s = SiteById(state, id);
            if (s == null)
                return new Dictionary<string, object> { { "ok", false }, { "why", "no such roof" } };
            decisions = decisions ?? new Dictionary<string, string>();
            Dictionary<string, object> q = CloseQuote(state, id, decisions);
            for (int i = state.Employees.Count - 1; i >= 0; i--)
            {
                Employee e = state.Employees[i];
                if ((e.Site ?? "") != id) continue;
                string d;
                if (!decisions.TryGetValue("e:" + i, out d)) d = "go";
                if (d.StartsWith("move:"))
                {
                    string dest = d.Substring(5);
                    state.Cash -= Gd.RoundToInt(Pb(state, "relocation_fee"));
                    e.Site = dest;
                    Mark(state, "works_ramp", e.Name ?? "", state.Week + 1);
                }
                else
                {
                    SimLabor.FireEmployee(state, i);   // books severance_due; ALWAYS owed
                }
            }
            List<EquipmentItem> eq = Eq(state);
            for (int j = eq.Count - 1; j >= 0; j--)
            {
                EquipmentItem m = eq[j];
                if ((m.Site ?? "") != id) continue;
                string dm;
                if (!decisions.TryGetValue("m:" + j, out dm)) dm = "sell";
                if (dm.StartsWith("move:"))
                {
                    state.Cash -= Gd.RoundToInt(Pb(state, "machine_shipping"));
                    m.Site = dm.Substring(5);
                    Mark(state, "works_off", m.Name ?? "", state.Week + 1);
                }
                else
                {
                    SimFactory.SellEquipment(state, j);
                }
            }
            state.Cash -= Gd.RoundToInt(Pb(state, "lease_break_weeks")) * s.RentWk;
            int lost = System.Convert.ToInt32(q["lost"], CultureInfo.InvariantCulture);
            if (lost > 0) state.Traction = Gd.Maxi(state.Traction - lost, 0);
            int kept = System.Convert.ToInt32(q["kept"], CultureInfo.InvariantCulture);
            if (kept > 0) SimEngine.AddStatus(state, "churn_spiral", 2);
            Unmark(state, "works_red", id);   // the dead roof takes its counter
            state.Sites.Remove(s);
            int payback = System.Convert.ToInt32(q["payback_wk"], CultureInfo.InvariantCulture);
            state.LogAction(string.Format(
                "CLOSED {0}: {1} transferred (fragile), {2} lost with the roof — payback ≈{3} wks",
                s.Name, kept, lost, payback >= 0 ? payback.ToString() : "—"));
            return new Dictionary<string, object> { { "ok", true }, { "why", "" }, { "quote", q } };
        }

        // ═════════════════ THE ARRANGE OPS (chips) ═════════════════════════

        public static Dictionary<string, object> ReassignQuote(GameState state, int empI, string toSite)
        {
            if (empI < 0 || empI >= state.Employees.Count) return new Dictionary<string, object>();
            Employee e = state.Employees[empI];
            return new Dictionary<string, object>
            {
                { "fee", Gd.RoundToInt(Pb(state, "relocation_fee")) }, { "name", e.Name ?? "?" },
                { "from", e.Site ?? "" }, { "to", toSite }, { "ramp_wk", 1 },
            };
        }

        /// <summary>MOVE A PERSON — brick: the relocation fee now and a 1-week
        /// ramp at the new roof (zero slots meanwhile).</summary>
        public static Dictionary<string, object> ReassignEmployee(GameState state, int empI, string toSite)
        {
            Dictionary<string, object> q = ReassignQuote(state, empI, toSite);
            if (q.Count == 0)
                return new Dictionary<string, object> { { "ok", false }, { "why", "nobody there" } };
            if (toSite != "" && SiteById(state, toSite) == null)
                return new Dictionary<string, object> { { "ok", false }, { "why", "no such roof" } };
            if ((string)q["from"] == toSite)
                return new Dictionary<string, object> { { "ok", false }, { "why", "already under that roof" } };
            int fee = System.Convert.ToInt32(q["fee"], CultureInfo.InvariantCulture);
            if (state.Cash < fee)
                return new Dictionary<string, object> { { "ok", false },
                    { "why", "$" + (fee - state.Cash) + " short of the relocation" } };
            Employee e = state.Employees[empI];
            state.Cash -= fee;
            e.Site = toSite;
            Mark(state, "works_ramp", e.Name ?? "", state.Week + 1);
            state.LogAction(string.Format("MOVED {0} to {1}: −${2} and a ramp week",
                e.Name, RoofName(state, toSite), fee));
            return new Dictionary<string, object> { { "ok", true }, { "why", "" }, { "fee", fee } };
        }

        public static Dictionary<string, object> MoveQuote(GameState state, int eqI, string toSite)
        {
            List<EquipmentItem> eq = Eq(state);
            if (eqI < 0 || eqI >= eq.Count) return new Dictionary<string, object>();
            EquipmentItem m = eq[eqI];
            return new Dictionary<string, object>
            {
                { "fee", Gd.RoundToInt(Pb(state, "machine_shipping")) }, { "name", m.Name ?? "?" },
                { "from", m.Site ?? "" }, { "to", toSite }, { "off_wk", 1 },
            };
        }

        /// <summary>MOVE A MACHINE — brick: shipping now and a week offline.</summary>
        public static Dictionary<string, object> MoveMachine(GameState state, int eqI, string toSite)
        {
            Dictionary<string, object> q = MoveQuote(state, eqI, toSite);
            if (q.Count == 0)
                return new Dictionary<string, object> { { "ok", false }, { "why", "no machine there" } };
            if (toSite != "" && SiteById(state, toSite) == null)
                return new Dictionary<string, object> { { "ok", false }, { "why", "no such roof" } };
            if ((string)q["from"] == toSite)
                return new Dictionary<string, object> { { "ok", false }, { "why", "already under that roof" } };
            int fee = System.Convert.ToInt32(q["fee"], CultureInfo.InvariantCulture);
            if (state.Cash < fee)
                return new Dictionary<string, object> { { "ok", false },
                    { "why", "$" + (fee - state.Cash) + " short of the shipping" } };
            EquipmentItem m = Eq(state)[eqI];
            state.Cash -= fee;
            m.Site = toSite;
            Mark(state, "works_off", m.Name ?? "", state.Week + 1);
            state.LogAction(string.Format("SHIPPED {0} to {1}: −${2} and a week offline",
                m.Name, RoofName(state, toSite), fee));
            return new Dictionary<string, object> { { "ok", true }, { "why", "" }, { "fee", fee } };
        }

        /// <summary>PAPER IS PAPER — tags are free.</summary>
        public static bool TagOffer(GameState state, int offerI, string productId)
        {
            if (offerI < 0 || offerI >= state.Offers.Count) return false;
            string pid = productId ?? "";
            state.Offers[offerI].ProductId = pid.Length > 24 ? pid.Substring(0, 24) : pid;
            return true;
        }

        public static bool TagSpendLine(GameState state, int lineI, string division)
        {
            if (lineI < 0 || lineI >= state.SpendBook.Count) return false;
            string dv = division ?? "";
            state.SpendBook[lineI].Division = dv.Length > 24 ? dv.Substring(0, 24) : dv;
            return true;
        }

        /// <summary>STOP A SPEND LINE — instantly, unless the book marked it
        /// "contract": the notice period bills through as a commitment.</summary>
        public static Dictionary<string, object> StopSpendLine(GameState state, int lineI)
        {
            if (lineI < 0 || lineI >= state.SpendBook.Count)
                return new Dictionary<string, object> { { "ok", false }, { "why", "no such line" } };
            SpendLine l = state.SpendBook[lineI];
            int notice = l.ContractNotice;
            if (notice > 0)
            {
                notice = Gd.Clampi(notice, 1, Gd.RoundToInt(Pb(state, "contract_notice_wks") * 3.0));
                state.Commitments.Add(new Commitment { Name = "notice: " + l.Name,
                    CashWk = -l.Amt, WeeksLeft = notice });
            }
            state.SpendBook.RemoveAt(lineI);
            state.LogAction("STOPPED " + l.Name + (notice > 0
                ? string.Format(" — contract: {0} wks of notice bill through", notice) : ""));
            return new Dictionary<string, object> { { "ok", true }, { "why", "" },
                { "notice_wks", notice }, { "amt", l.Amt } };
        }

        // ═════════════ THE GROUP-BY BOOKS (pure sums) ══════════════════════

        /// <summary>A roof's share of this week's demand: weights over home
        /// (1.0) + sites.</summary>
        public static double DemandShare(GameState state, string id)
        {
            double total = HomeOccupied(state) ? 1.0 : 0.0;
            for (int i = 0; i < state.Sites.Count; i++)
                total += Gd.Maxf(state.Sites[i].DemandWeight, 0.0);
            if (total <= 0.0) return 0.0;
            if (id == "") return HomeOccupied(state) ? 1.0 / total : 0.0;
            Site s2 = SiteById(state, id);
            return s2 != null ? Gd.Maxf(s2.DemandWeight, 0.0) / total : 0.0;
        }

        public static string RoofName(GameState state, string id)
        {
            if (id == "") return "the home roof";
            Site s = SiteById(state, id);
            return s != null ? s.Name : id;
        }

        /// <summary>THE SLICER. Division rows are GROUP-BYs over records the
        /// engine already keeps; the SHARED/HQ row closes every book.</summary>
        public static List<Dictionary<string, object>> WorksBook(GameState state, string axis)
        {
            var rows = new List<Dictionary<string, object>>();
            if (axis == "site")
            {
                var ids = new List<string>();
                if (HomeOccupied(state)) ids.Add("");
                for (int i = 0; i < state.Sites.Count; i++) ids.Add(state.Sites[i].Id);
                for (int k = 0; k < ids.Count; k++) rows.Add(SiteRow(state, ids[k]));
            }
            else if (axis == "product")
            {
                var seen = new HashSet<string>();
                for (int i = 0; i < state.Offers.Count; i++)
                {
                    string pid = state.Offers[i].ProductId ?? "";
                    if (seen.Add(pid)) rows.Add(ProductRow(state, pid));
                }
            }
            else
            {
                for (int i = 0; i < state.Offers.Count; i++) rows.Add(OfferRow(state, i));
            }
            rows.Add(SharedRow(state));
            return rows;
        }

        internal static Dictionary<string, object> SiteRow(GameState state, string id)
        {
            Site s = SiteById(state, id);
            int heads = 0;
            int payroll = 0;
            for (int i = 0; i < state.Employees.Count; i++)
                if ((state.Employees[i].Site ?? "") == id)
                {
                    heads += 1;
                    payroll += state.Employees[i].Salary;
                }
            int machines = 0;
            List<EquipmentItem> eq = Eq(state);
            for (int j = 0; j < eq.Count; j++)
                if ((eq[j].Site ?? "") == id) machines += 1;
            int spend = 0;
            for (int k = 0; k < state.SpendBook.Count; k++)
                if ((state.SpendBook[k].Division ?? "") == id && id != "")
                    spend += state.SpendBook[k].Amt;
            Dictionary<string, object> w = SimWorks.WeekView(state);
            double share = DemandShare(state, id);
            double wanted = SimWorks.Num(w, "demand_units") * share;
            double slots = SimWorks.CapacityOfSite(state, id);
            double served = (string)w["type"] == "Service"
                ? Gd.Minf(wanted, slots)
                : SimWorks.Num(w, "served_units") * share;
            double vol = Gd.Maxf(served, 0.0);
            double wage = s != null ? s.WageMult : 1.0;
            double lc = s != null ? SiteLc(s.LearningCount) : SimEngine.LearningCurve(state);
            int rent = s != null ? s.RentWk : 0;
            double baseVar = SimWorks.BaseUnitCost(state);
            double unitCost = baseVar * wage * lc + rent / Gd.Maxf(vol, 1.0);
            double revU = SimWorks.Num(w, "rev_per_unit");
            double margin = revU - unitCost;
            int netWk = Gd.RoundToInt(margin * vol) - payroll - spend;
            double util = slots > 0.0 ? Gd.Clampf(wanted / Gd.Maxf(slots, 0.001), 0.0, 1.0) : 0.0;
            int sev = 0;
            if (s != null)
            {
                if (Gd.Maxi(MarkedUntil(state, "works_red", id), 0) >= RED_WEEKS) sev = 3;
                else if (margin < 0.0 && state.Week - s.OpenedWk > RAMP_GRACE_WK) sev = 2;
            }
            string note = "";
            if (s != null && state.Week - s.OpenedWk <= RAMP_GRACE_WK) note = "young — still ramping";
            else if (sev >= 3) note = "fix or close";
            else if (slots > 0.0 && wanted > slots) note = "full — overflow → relief";
            return new Dictionary<string, object>
            {
                { "id", id }, { "name", RoofName(state, id) }, { "kind", "site" },
                { "heads", heads }, { "payroll_wk", payroll }, { "rent_wk", rent },
                { "machines", machines }, { "slots", slots }, { "wanted", wanted },
                { "served", served }, { "vol", vol }, { "unit_cost", unitCost },
                { "margin_each", margin }, { "net_wk", netWk }, { "util", util },
                { "wage_mult", wage }, { "lc", lc }, { "sev", sev }, { "note", note },
            };
        }

        internal static Dictionary<string, object> ProductRow(GameState state, string pid)
        {
            Dictionary<string, object> w = SimWorks.WeekView(state);
            double wanted = 0.0;
            double costU = 0.0;
            double revU = 0.0;
            var names = new List<string>();
            double lc = SimEngine.LearningCurve(state);
            double fm = SimEngine.StreetFairMult(state);
            for (int i = 0; i < state.Offers.Count; i++)
            {
                Offer od = state.Offers[i];
                if ((od.ProductId ?? "") != pid) continue;
                names.Add(od.Name);
                double u = state.Traction * od.Weight * SimEngine.OfferCadence(od.Unit);
                wanted += u;
                costU += u * (od.UnitCost * lc + SimWorks.FeatureCostAdd(state, pid));
                revU += u * SimEngine.OfferBilledPrice(od, fm);
            }
            double vol = Gd.Maxf(wanted, 0.0);
            double unitCost = costU / Gd.Maxf(vol, 0.001);
            double margin = revU / Gd.Maxf(vol, 0.001) - unitCost;
            double served = Gd.Minf(vol, SimWorks.Num(w, "served_units", vol));
            string joined = string.Join(", ", names);
            if (joined.Length > 40) joined = joined.Substring(0, 40);
            return new Dictionary<string, object>
            {
                { "id", pid }, { "name", pid == "" ? "the flagship" : pid }, { "kind", "product" },
                { "heads", 0 }, { "payroll_wk", 0 }, { "rent_wk", 0 }, { "machines", 0 },
                { "slots", 0.0 }, { "wanted", wanted }, { "served", served }, { "vol", vol },
                { "unit_cost", unitCost }, { "margin_each", margin },
                { "net_wk", Gd.RoundToInt(margin * vol) }, { "util", 0.0 },
                { "wage_mult", 1.0 }, { "lc", lc }, { "sev", 0 }, { "note", joined },
            };
        }

        internal static Dictionary<string, object> OfferRow(GameState state, int i)
        {
            Offer od = state.Offers[i];
            double lc = SimEngine.LearningCurve(state);
            double fm = SimEngine.StreetFairMult(state);
            string pid = od.ProductId ?? "";
            double wanted = state.Traction * od.Weight * SimEngine.OfferCadence(od.Unit);
            double unitCost = od.UnitCost * lc + SimWorks.FeatureCostAdd(state, pid);
            double price = SimEngine.OfferBilledPrice(od, fm);
            Dictionary<string, object> w = SimWorks.WeekView(state);
            double fill = 1.0;
            if (SimWorks.Num(w, "demand_units") > 0.0)
                fill = Gd.Clampf(SimWorks.Num(w, "served_units") / SimWorks.Num(w, "demand_units", 1.0), 0.0, 1.0);
            return new Dictionary<string, object>
            {
                { "id", "offer_" + i }, { "name", od.Name }, { "kind", "offer" },
                { "heads", 0 }, { "payroll_wk", 0 }, { "rent_wk", 0 }, { "machines", 0 },
                { "slots", 0.0 }, { "wanted", wanted }, { "served", wanted * fill },
                { "vol", wanted * fill }, { "unit_cost", unitCost },
                { "margin_each", price - unitCost },
                { "net_wk", Gd.RoundToInt((price - unitCost) * wanted * fill) },
                { "util", fill }, { "wage_mult", 1.0 }, { "lc", lc }, { "sev", 0 },
                { "note", "" },
            };
        }

        internal static Dictionary<string, object> SharedRow(GameState state)
        {
            int spend = 0;
            for (int k = 0; k < state.SpendBook.Count; k++)
                if (string.IsNullOrEmpty(state.SpendBook[k].Division))
                    spend += state.SpendBook[k].Amt;
            int brand = state.Budgets.Ads + state.Budgets.Content;
            int hqRent;
            if (!GameState.ERA_RENT.TryGetValue(state.Era, out hqRent)) hqRent = 150;
            int founder = GameState.RAMEN_PER_WEEK;
            return new Dictionary<string, object>
            {
                { "id", "shared" }, { "name", "SHARED / HQ" }, { "kind", "shared" },
                { "heads", 0 }, { "payroll_wk", founder }, { "rent_wk", hqRent },
                { "machines", 0 }, { "slots", 0.0 }, { "wanted", 0.0 }, { "served", 0.0 },
                { "vol", 0.0 }, { "unit_cost", 0.0 }, { "margin_each", 0.0 },
                { "net_wk", -(founder + hqRent + brand + spend) }, { "util", 0.0 },
                { "wage_mult", 1.0 }, { "lc", 1.0 }, { "sev", 0 },
                { "note", "the founder, brand marketing, the era's roof — never smeared" },
            };
        }

        // ═════════ THE DM OP DOORS (WeekCommit arms call these) ════════════

        static string DStr(Dictionary<string, object> d, string key, string dflt = "")
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v != null) return v.ToString();
            return dflt;
        }

        public static string OpOpenSite(GameState state, Dictionary<string, object> d)
        {
            string nm = DStr(d, "cat", DStr(d, "name"));
            Dictionary<string, object> res = OpenSite(state, nm);
            if (!(bool)res["ok"]) return "no new roof: " + res["why"];
            var site = (Site)res["site"];
            return string.Format(
                "OPENED {0}: −${1}, rent ${2}/wk — its demand ramps on its own curve",
                site.Name, res["pack"], site.RentWk);
        }

        public static string OpCloseSite(GameState state, Dictionary<string, object> d)
        {
            string id = SiteIdFrom(state, DStr(d, "cat"));
            if (id == "") return "no roof called '" + DStr(d, "cat") + "' — nothing closed";
            Dictionary<string, object> res = CloseSite(state, id, null);
            if (!(bool)res["ok"]) return (string)res["why"];
            var q = (Dictionary<string, object>)res["quote"];
            return string.Format(
                "CLOSED the roof: {0} customers transferred (fragile), {1} lost, ${2}/wk freed",
                q["kept"], q["lost"], q["freed_wk"]);
        }

        public static string OpReassignEmployee(GameState state, Dictionary<string, object> d)
        {
            string nm = DStr(d, "cat").Trim().ToLowerInvariant();
            string to = SiteIdFrom(state, DStr(d, "v", DStr(d, "site")));
            for (int i = 0; i < state.Employees.Count; i++)
            {
                if (nm.Length == 0) break;
                if (!(state.Employees[i].Name ?? "").ToLowerInvariant().Contains(nm)) continue;
                Dictionary<string, object> res = ReassignEmployee(state, i, to);
                if ((bool)res["ok"])
                    return string.Format("{0} → {1}: −${2} and a ramp week",
                        state.Employees[i].Name, RoofName(state, to), res["fee"]);
                return (string)res["why"];
            }
            return "nobody called '" + DStr(d, "cat") + "' on the payroll";
        }

        public static string OpMoveMachine(GameState state, Dictionary<string, object> d)
        {
            string nm = DStr(d, "cat").Trim().ToLowerInvariant();
            string to = SiteIdFrom(state, DStr(d, "v", DStr(d, "site")));
            List<EquipmentItem> eq = Eq(state);
            for (int j = 0; j < eq.Count; j++)
            {
                if (nm.Length == 0) break;
                if (!(eq[j].Name ?? "").ToLowerInvariant().Contains(nm)) continue;
                Dictionary<string, object> res = MoveMachine(state, j, to);
                if ((bool)res["ok"])
                    return string.Format("{0} shipped to {1}: −${2} and a week offline",
                        eq[j].Name, RoofName(state, to), res["fee"]);
                return (string)res["why"];
            }
            return "no machine called '" + DStr(d, "cat") + "' on the floor";
        }

        public static string OpTagOffer(GameState state, Dictionary<string, object> d)
        {
            string nm = DStr(d, "cat").Trim().ToLowerInvariant();
            for (int i = 0; i < state.Offers.Count; i++)
            {
                if (nm.Length == 0) break;
                if (!(state.Offers[i].Name ?? "").ToLowerInvariant().Contains(nm)) continue;
                string pid = DStr(d, "v");
                TagOffer(state, i, pid);
                return string.Format("{0} filed under {1} — paper is paper, free",
                    state.Offers[i].Name, pid.Length > 0 ? pid : "the flagship");
            }
            return "no offer called '" + DStr(d, "cat") + "' on the shelf";
        }

        public static string OpTagSpendLine(GameState state, Dictionary<string, object> d)
        {
            string nm = DStr(d, "cat").Trim().ToLowerInvariant();
            for (int i = 0; i < state.SpendBook.Count; i++)
            {
                if (nm.Length == 0) break;
                if (!(state.SpendBook[i].Name ?? "").ToLowerInvariant().Contains(nm)) continue;
                string dv = DStr(d, "v");
                TagSpendLine(state, i, dv == "shared" ? "" : SiteIdFrom(state, dv));
                return string.Format("{0} filed under {1} — ink, free",
                    state.SpendBook[i].Name,
                    (dv == "shared" || dv.Length == 0) ? "SHARED/HQ" : dv);
            }
            return "no spend line called '" + DStr(d, "cat") + "' in the book";
        }

        /// <summary>A roof by id OR by (partial) name — the DM speaks names.</summary>
        public static string SiteIdFrom(GameState state, string word)
        {
            string w = (word ?? "").Trim().ToLowerInvariant();
            if (w.Length == 0 || w == "home" || w == "hq") return "";
            for (int i = 0; i < state.Sites.Count; i++)
            {
                Site sd = state.Sites[i];
                if ((sd.Id ?? "").ToLowerInvariant() == w
                    || (sd.Name ?? "").ToLowerInvariant().Contains(w))
                    return sd.Id;
            }
            return "";
        }

        // ═══════════════ THE SPINE'S ENTRY POINTS ══════════════════════════

        /// <summary>Tick §6c — roofs settle before the market splits demand:
        /// ramps climb, expired markers fall off the flags list.</summary>
        public static void TickPre(GameState state, WeeklyReport rep)
        {
            for (int i = 0; i < state.Sites.Count; i++)
            {
                Site sd = state.Sites[i];
                if (sd.DemandWeight < 1.0)
                    sd.DemandWeight = Gd.Minf(
                        sd.DemandWeight + (1.0 - sd.DemandWeight) * SITE_RAMP_K, 1.0);
            }
            PruneMarks(state);
        }

        /// <summary>The money section — owns ONLY `site_rent`: every opened
        /// roof's rent, beside the era's own roof, receipted once.</summary>
        public static void TickMoney(GameState state, WeeklyReport rep, MoneyWork m)
        {
            int rent = 0;
            for (int i = 0; i < state.Sites.Count; i++) rent += state.Sites[i].RentWk;
            if (rent > 0)
            {
                m.SiteRent += rent;
                rep.Lines.Add(string.Format(
                    "site rents: −${0} across {1} roof{2} (beside the era's own)",
                    rent, state.Sites.Count, state.Sites.Count > 1 ? "s" : ""));
            }
        }

        /// <summary>After the record: per-site learning counts grow with the
        /// roofs' own served volume; the bleeding flag reads the week.</summary>
        public static void TickPost(GameState state, WeeklyReport rep)
        {
            if (state.Sites.Count == 0) return;
            Dictionary<string, object> w = SimWorks.WeekView(state);
            double served = SimWorks.Num(w, "served_units");
            for (int i = 0; i < state.Sites.Count; i++)
            {
                Site sd = state.Sites[i];
                string id = sd.Id ?? "";
                double share = DemandShare(state, id);
                sd.LearningCount += Gd.RoundToInt(served * share);
                Dictionary<string, object> row = SiteRow(state, id);
                double margin = SimWorks.Num(row, "margin_each");
                if (margin < 0.0 && state.Week - sd.OpenedWk > RAMP_GRACE_WK)
                    Mark(state, "works_red", id, Gd.Maxi(MarkedUntil(state, "works_red", id), 0) + 1);
                else
                    Unmark(state, "works_red", id);
            }
        }

        /// <summary>DM context: the roofs in one line — facts, never prices.</summary>
        public static List<string> Directives(GameState state)
        {
            var outp = new List<string>();
            if (state.Sites.Count == 0) return outp;
            var bits = new List<string>();
            for (int i = 0; i < state.Sites.Count && bits.Count < 3; i++)
            {
                Site sd = state.Sites[i];
                Dictionary<string, object> row = SiteRow(state, sd.Id ?? "");
                bits.Add(string.Format("{0} (rent ${1}/wk, {2}% used{3})", sd.Name,
                    sd.RentWk, Gd.RoundToInt(SimWorks.Num(row, "util") * 100.0),
                    System.Convert.ToInt32(row["sev"], CultureInfo.InvariantCulture) >= 2
                        ? ", bleeding" : ""));
            }
            outp.Add("- Roofs beside the home one: " + string.Join(", ", bits)
                + ". Hires and machines need a roof named.");
            return outp;
        }

        /// <summary>Attention — the works desk: a roof bleeding past its ramp
        /// is worth stopping the dice for; three red weeks is the alarm.</summary>
        public static List<AttentionItem> Attention(GameState state)
        {
            var rows = new List<AttentionItem>();
            for (int i = 0; i < state.Sites.Count; i++)
            {
                Site sd = state.Sites[i];
                string id = sd.Id ?? "";
                Dictionary<string, object> row = SiteRow(state, id);
                int sev = System.Convert.ToInt32(row["sev"], CultureInfo.InvariantCulture);
                string nm = sd.Name ?? "a roof";
                if (sev >= 3)
                    rows.Add(new AttentionItem { Desk = "the works", Key = "site_bleeds_" + id,
                        Severity = 3,
                        Label = (nm.Length > 20 ? nm.Substring(0, 20) : nm) + " bleeds — fix or close" });
                else if (sev == 2)
                    rows.Add(new AttentionItem { Desk = "the works", Key = "site_neg_" + id,
                        Severity = 2,
                        Label = (nm.Length > 24 ? nm.Substring(0, 24) : nm) + " runs at a loss" });
            }
            return rows;
        }

        // ── the durable markers (flags-encoded; typed twins bar new keys) ──

        public static void Mark(GameState state, string kind, string name, int untilWk)
        {
            string prefix = kind + ":" + name + ":";
            for (int i = state.Flags.Count - 1; i >= 0; i--)
                if (state.Flags[i].StartsWith(prefix)) state.Flags.RemoveAt(i);
            state.Flags.Add(prefix + untilWk);
        }

        public static void Unmark(GameState state, string kind, string name)
        {
            string prefix = kind + ":" + name + ":";
            for (int i = state.Flags.Count - 1; i >= 0; i--)
                if (state.Flags[i].StartsWith(prefix)) state.Flags.RemoveAt(i);
        }

        public static int MarkedUntil(GameState state, string kind, string name)
        {
            string prefix = kind + ":" + name + ":";
            for (int i = 0; i < state.Flags.Count; i++)
                if (state.Flags[i].StartsWith(prefix))
                {
                    int v;
                    if (int.TryParse(state.Flags[i].Substring(prefix.Length), out v)) return v;
                    return -1;
                }
            return -1;
        }

        static void PruneMarks(GameState state)
        {
            for (int i = state.Flags.Count - 1; i >= 0; i--)
            {
                string f = state.Flags[i];
                if (!f.StartsWith("works_ramp:") && !f.StartsWith("works_off:")) continue;
                int cut = f.LastIndexOf(':');
                int until;
                if (cut >= 0 && int.TryParse(f.Substring(cut + 1), out until)
                    && state.Week >= until)
                    state.Flags.RemoveAt(i);
            }
        }
    }
}
