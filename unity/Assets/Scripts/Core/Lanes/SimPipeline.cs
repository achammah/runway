using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Runway.Core
{
    /// <summary>
    /// LANE 05 — THE ENTERPRISE PIPELINE (named leads, logos, renewals). Spec: docs/design/05-enterprise-pipeline.md
    ///
    /// Enterprise customers do not arrive as a coin flip. They are named accounts
    /// that took a meeting, sat in a pilot, survived a security review and signed
    /// a contract — and every seat inside them came out of the SAME demand the
    /// Bass block already generates. That is the whole law of this file:
    ///
    ///   CONSERVATION IS SACRED. Section 8's `adds` lands in a POOL (PipeUnits).
    ///   A spawn DEBITS its seats from the pool; a lead that dies cold REFUNDS
    ///   them. Nothing here invents market and nothing destroys it — the pipeline
    ///   only re-times and re-chunks demand, so the tuned Enterprise curve
    ///   survives untouched and what the founder loses to a dead deal is TIME,
    ///   not the market.
    ///
    ///   THE ENGINE OWNS EVERY NUMBER. The DM's only lever on this board is heat,
    ///   clamped plus or minus 40 (PushLead). Narration never advances a stage,
    ///   never sets a seat count and never signs a contract.
    ///
    ///   NON-ENTERPRISE RUNS ARE BYTE-IDENTICAL. Every entry point below leaves
    ///   on the activation gate, no stream is touched, no field is written. SMB
    ///   and Consumer tick exactly as they did before this file had a body.
    ///
    /// The spine calls, in tick order (00-spine section 1, HOOKS.md):
    ///   TickPre    tick 8 — leads advance before the market is counted
    ///   TickMoney  the money section — write ONLY the P&amp;L lanes this lane owns
    ///   TickPost   after the week's record is written and can be read back
    /// and outside the tick: Directives feeds the DM block, Attention feeds
    /// every bang in the game through SimEngine.AttentionItems.
    ///
    /// WHERE THE WEEK ACTUALLY RESOLVES: AdoptionNet, not TickPre. The pool
    /// inflow is `adds` and the churn regime is `churn`, and neither exists until
    /// section 8 has computed them — so the whole pipeline runs in the adoption
    /// seam, in the spec's fixed draw order, on ONE salt-50 stream.
    ///
    /// TWIN LAW: this file and game/src/core/lanes/sim_pipeline.gd carry the same
    /// logic in the same order. The engines do NOT share PRNG internals, so
    /// parity means same checks and same behaviour, never a byte-equal draw.
    /// </summary>
    public static class SimPipeline
    {
        // ── the activation gate ──────────────────────────────────────────
        /// <summary>Enterprise runs only. Everything here is behind this predicate.</summary>
        public static bool Active(GameState state)
        {
            return state != null && state.BizWho == "Enterprise";
        }

        /// <summary>
        /// THE SECOND GATE, read by the tick itself (section 2.1). Demand
        /// generation and closing capacity are two different jobs in every real
        /// B2B org — marketing books the meetings, an AE moves them through gates.
        /// On Enterprise runs the gtm ceiling moves OUT of section 8's adds and
        /// INTO `capacity` on the stage advance, so the tick steps its own min()
        /// aside and the motion is taxed once instead of twice.
        /// </summary>
        public static bool SkipsGtmCap(GameState state)
        {
            return Active(state);
        }

        // ── caps and constants (spec 1.3; all engine-side, all clamped) ───
        public const int LEAD_CAP = 8;           // live leads max — a real AE runs 10-15 open
                                                 // opportunities; a founder juggling everything, fewer
        public const int SPAWNS_PER_WK = 2;      // keeps the naming batch small and rare
        public const int MIN_SEATS = 3;
        public const double POOL_CAP_FRAC = 0.25;  // x tam — 1000 units at the default Enterprise tam 4000
        public const int HEAT_SPAWN_LO = 50;
        public const int HEAT_SPAWN_HI = 65;
        public const int HEAT_DECAY = 8;         // per week; -1 per sales head at floor/hq, max -3
        public const int HEAT_DECAY_FLOOR = 4;   // account teams slow the rot, they never stop it
        public const int HEAT_ADVANCE = 12;      // momentum on a stage advance — worth
                                                 // ~1.5 weeks at HEAT_DECAY, not three.
                                                 // A gate cleared is real momentum, but it
                                                 // must not be a full refill: at 25 every
                                                 // advance bought back most of the deal's
                                                 // lifespan, so a deal that was moving at
                                                 // all could never die, and no-decision —
                                                 // the thing this subsystem exists to
                                                 // teach — stopped happening.
        public const int PUSH_CLAMP = 40;        // PushLead v clamp
        public const double P_ADV_MIN = 0.05;
        public const double P_ADV_MAX = 0.85;
        public const int PROCUREMENT_SEATS = 20; // the seat count that wakes a buyer's IT department
        public const int RENEW_EVERY = 26;       // weeks (floor/hq): the annual-contract cliff
        public const int MAX_LINES = 6;          // pipeline receipts per week, then "…and N more moved"

        public static readonly Dictionary<string, double> BASE_ADV = new Dictionary<string, double>
        {
            { "meeting", 0.45 }, { "pilot", 0.35 }, { "procurement", 0.35 }, { "contract", 0.40 },
        };

        /// <summary>
        /// THE ERA LADDER (spec section 8) — the same math everywhere, the
        /// CONSTANTS climb, so depth arrives as the company earns it. Startups
        /// sell design-partner pilots before they can sell procurement-grade
        /// contracts.
        /// </summary>
        public static readonly int[][] SEAT_BANDS =
        {
            new[] { 3, 8 }, new[] { 9, 20 }, new[] { 21, 60 }, new[] { 61, 120 },
        };

        public static readonly Dictionary<string, int[]> SEAT_TIERS = new Dictionary<string, int[]>
        {
            { "garage",    new[] { 70, 25, 5, 0 } },
            { "coworking", new[] { 60, 30, 10, 0 } },
            { "office",    new[] { 45, 35, 17, 3 } },
            { "floor",     new[] { 30, 35, 27, 8 } },
            { "hq",        new[] { 20, 30, 35, 15 } },
        };

        /// <summary>The knee of the size penalty: a 40-seat deal crawls for a
        /// garage founder and moves for an hq motion.</summary>
        public static readonly Dictionary<string, double> SIZE_REF = new Dictionary<string, double>
        {
            { "garage", 10.0 }, { "coworking", 16.0 }, { "office", 28.0 },
            { "floor", 45.0 }, { "hq", 70.0 },
        };

        /// <summary>THE KEYLESS NAME PATH (spec section 10). The Markov seeds
        /// already carry Meridian, Vanta and Quill — the world names its own
        /// customers, with or without a key.</summary>
        public static readonly string[] ENT_SUFFIX =
        {
            "Logistics", "Systems", "Group", "Health", "Labs",
            "Industrial", "Financial", "Retail", "Foods", "Media",
        };

        /// <summary>One line per era: what this stage of the company's pipeline can and cannot do.</summary>
        public static readonly Dictionary<string, string> COACH = new Dictionary<string, string>
        {
            { "garage", "design-partner pilots: small deals teach fastest" },
            { "coworking", "first real contracts — the ACV on a receipt is a year of one logo" },
            { "office", "procurement appears on 20+ seat deals — price fairness moves it" },
            { "floor", "renewals every 26 wks — care and quality decide them" },
            { "hq", "renewals every 26 wks — care and quality decide them" },
        };

        /// <summary>The heat ramp in words (spec section 11) — the desk and the DM read one scale.</summary>
        public static string HeatWord(int heat)
        {
            if (heat >= 75) { return "hot"; }
            if (heat >= 50) { return "warm"; }
            if (heat >= 25) { return "cool"; }
            return "cold";
        }

        /// <summary>The pool ceiling: a quarter of the addressable market can be in play at once.</summary>
        public static double PoolCap(GameState state)
        {
            double tam = state.Theta != null ? state.Theta.Tam : 4000.0;
            return POOL_CAP_FRAC * tam;
        }

        /// <summary>Weekly heat decay. Account teams (floor+) hold a deal warm a little longer.</summary>
        public static int DecayFor(GameState state)
        {
            int d = HEAT_DECAY;
            if (state.EraIndex() >= 3) { d -= Gd.Mini(SalesHeads(state), 3); }
            return Gd.Maxi(d, HEAT_DECAY_FLOOR);
        }

        /// <summary>Weeks of silence a lead has left before it dies of no-decision.</summary>
        public static int WeeksToCold(int heat, int decay)
        {
            return (int)Math.Ceiling(heat / (double)Gd.Maxi(decay, 1));
        }

        private static int SalesHeads(GameState state)
        {
            int n = 0;
            foreach (Employee e in state.Employees)
            {
                if ((e.Role ?? "").Contains("sales")) { n += 1; }
            }
            return n;
        }

        /// <summary>
        /// THE CLOSING CAPACITY `C` — the tick's own gtmCap formula at capScale
        /// 1.0, REUSED, not re-invented. Demand generation is marketing's job
        /// (section 2); this is the AE capacity that moves deals through gates.
        /// </summary>
        public static double Capacity(GameState state)
        {
            Budgets bud = state.Budgets;
            double mkBudget = bud.Acquisition() + state.MarketingBudget;
            return 1.5 + 0.8 * state.Competence("sell")
                + 3.0 * SalesHeads(state) + mkBudget / 400.0 + bud.Sales / 600.0;
        }

        // ── section 4 THE STAGE ADVANCE MATH ─────────────────────────────
        /// <summary>
        /// The probability this lead clears its current gate THIS week. Pure,
        /// closed form, no RNG — which is what lets the twin suites pin it to
        /// 1e-9 in both engines. `live` is the number of deals sharing the motion.
        ///
        /// Every factor is a real thing a founder can move:
        ///   capacity  AE capacity is finite and shared — more open deals slows all of them
        ///   quality   pilots convert on product (the tick's quality gate, gentler
        ///             floor: a pilot can limp where adoption cannot)
        ///   price     above-fair pricing stalls in evaluation and procurement
        ///   heat      deal momentum — stale deals slip, sponsored deals move
        ///   size      cycle length grows with deal size, against an era-scaled knee
        /// </summary>
        public static double LeadAdvanceP(GameState state, Lead lead, int live)
        {
            Dictionary<string, double> f = AdvanceFactors(state, lead, live);
            double baseP;
            if (!BASE_ADV.TryGetValue(lead.Stage ?? "meeting", out baseP)) { baseP = 0.40; }
            double p = baseP;
            foreach (string k in FACTOR_ORDER) { p *= f[k]; }
            return Gd.Clampf(p, P_ADV_MIN, P_ADV_MAX);
        }

        private static readonly string[] FACTOR_ORDER = { "capacity", "quality", "price", "heat", "size" };

        /// <summary>The five factors by name, so the receipt can say WHICH one carried the week.</summary>
        public static Dictionary<string, double> AdvanceFactors(GameState state, Lead lead, int live)
        {
            double dm = SimEngine.OffersDemandMult(state);
            double sizeRef;
            if (!SIZE_REF.TryGetValue(state.Era ?? "garage", out sizeRef)) { sizeRef = 10.0; }
            return new Dictionary<string, double>
            {
                // CAPACITY ONLY EVER SLOWS A DEAL. The ceiling is 1.0, not 1.5: a
                // motion with room to spare does not push a buyer through their
                // own stage gate faster than the gate opens — it just stops being
                // the bottleneck. Letting it accelerate made a starved board
                // (0.6-1.6 live deals is normal at Enterprise's demand rate) a
                // permanent x1.5 on every BASE_ADV, which won 93% of deals even
                // untended.
                { "capacity", Gd.Clampf(Capacity(state) / (1.5 * Gd.Maxi(live, 1)), 0.5, 1.0) },
                { "quality", 0.6 + state.Product / 100.0 * 0.8 },
                { "price", Gd.Clampf(dm < 0.0 ? 1.0 : dm, 0.5, 1.3) },
                { "heat", 0.5 + lead.Heat / 100.0 },
                { "size", SimEngine.JanoDown(lead.Seats, sizeRef, 0.55) },
            };
        }

        /// <summary>
        /// The factor farthest above 1.0 — ties break in the listed order —
        /// turned into the sentence the journal prints. A receipt that only says
        /// "it moved" teaches nothing; this one names the lever the founder pulled.
        /// </summary>
        private static string DominantWhy(GameState state, Lead lead, int live)
        {
            Dictionary<string, double> f = AdvanceFactors(state, lead, live);
            string best = "";
            double bestV = 1.0;
            foreach (string k in FACTOR_ORDER)
            {
                if (f[k] > bestV + 1e-9) { bestV = f[k]; best = k; }
            }
            switch (best)
            {
                case "capacity":
                    return string.Format(CultureInfo.InvariantCulture,
                        "the motion had room ({0} live deals)", Gd.Maxi(live, 1));
                case "quality":
                    return string.Format(CultureInfo.InvariantCulture,
                        "the demo held (product v0.{0})", state.Product);
                case "price":
                    return "the price sat at fair";
                case "heat":
                    return "the room stayed warm";
                case "size":
                    return string.Format(CultureInfo.InvariantCulture,
                        "a {0}-seat deal moves fast", lead.Seats);
            }
            return "nobody found a reason to say no";
        }

        /// <summary>
        /// The stage AFTER this one, for this deal, in this era. `procurement`
        /// only exists at office+ on deals of 20 seats or more — a security
        /// review appears exactly when deal size makes a buyer's IT department
        /// wake up. Returns "signed" when the next step is the close itself.
        /// </summary>
        public static string NextStage(GameState state, Lead lead)
        {
            switch (lead.Stage ?? "meeting")
            {
                case "meeting":
                    return "pilot";
                case "pilot":
                    return lead.Seats >= PROCUREMENT_SEATS && state.EraIndex() >= 2
                        ? "procurement" : "contract";
                case "procurement":
                    return "contract";
            }
            return "signed";
        }

        /// <summary>What one seat bills per week — the offers catalog when there
        /// is one, the world's own arpu otherwise.</summary>
        public static double UnitRevWk(GameState state)
        {
            double a = SimEngine.OffersArpu(state);
            if (a >= 0.0) { return a; }
            return (state.Theta != null ? state.Theta.ArpuWk : 400.0) * state.PriceMult;
        }

        // ── the spine's tick hooks ───────────────────────────────────────
        /// <summary>
        /// Tick 8, before adoption. The board cannot move yet: `adds` (the pool
        /// inflow) and `churn` (the regime) are computed further down section 8,
        /// so the whole weekly resolution lives in AdoptionNet below, on one
        /// stream in one fixed order. What happens here is the week's clean slate
        /// for the receipts.
        /// </summary>
        public static void TickPre(GameState state, WeeklyReport rep)
        {
            if (!Active(state)) { return; }
            state.SetMeta("pipe_spawned", new List<string>());
        }

        /// <summary>
        /// The money section. The pipeline books NO new P&amp;L lane — meetings,
        /// travel and demos ride the `sales` lever narratively, and a separate
        /// pipeline line would double-bill the same dollars (spec section 9).
        /// What it does do is remember what acquisition COST, so the desk can
        /// divide it by the seats it actually bought.
        /// </summary>
        public static void TickMoney(GameState state, WeeklyReport rep, MoneyWork m)
        {
            if (!Active(state)) { return; }
            Stats(state).Spend += m.Marketing + m.Sales;
        }

        /// <summary>
        /// After the record is written. Every signed-contract fact — traction,
        /// the logo, the cycle, the signed-this-week marker — is booked at the
        /// close itself, inside the single salt-50 pass, so a replay lands on the
        /// same week.
        ///
        /// What is left is ONE line for another desk. The board (08) plans around
        /// the renewal calendar, so the finished week publishes it to
        /// `cap_renewal_line` and the cap table prints whatever it finds there —
        /// blank hides the line, so a run that is not on annual contracts simply
        /// never shows one. A published string, not a cross-desk call: neither
        /// lane has to know the other exists.
        /// </summary>
        public static void TickPost(GameState state, WeeklyReport rep)
        {
            if (!Active(state)) { return; }
            state.SetMeta("cap_renewal_line", RenewalLine(state));
        }

        /// <summary>
        /// THE RENEWAL CALENDAR, in one line. The next three contracts up for
        /// renewal, soonest first — the board's whole question about enterprise
        /// revenue is "what has to be re-won, and when". "" before `floor`, where
        /// there are no annual contracts to lose yet, and the reading desk hides
        /// the line on "".
        /// </summary>
        public static string RenewalLine(GameState state)
        {
            if (!Active(state) || state.EraIndex() < 3) { return ""; }
            var due = new List<Logo>();
            foreach (Logo lg in state.Logos)
            {
                if (lg.RenewalWk > 0 && lg.RenewalWk - state.Week <= 52) { due.Add(lg); }
            }
            if (due.Count == 0) { return "none inside a year"; }
            due.Sort((a, b) =>
            {
                if (a.RenewalWk != b.RenewalWk) { return a.RenewalWk.CompareTo(b.RenewalWk); }
                return string.CompareOrdinal(a.Name ?? "", b.Name ?? "");
            });
            var parts = new List<string>();
            for (int i = 0; i < Gd.Mini(due.Count, 3); i++)
            {
                parts.Add(string.Format(CultureInfo.InvariantCulture, "{0} ({1} seats, wk {2})",
                    due[i].Name, due[i].Seats, due[i].RenewalWk));
            }
            string outp = string.Join(" · ", parts.ToArray());
            if (due.Count > 3)
            {
                outp += string.Format(CultureInfo.InvariantCulture, " · +{0} more", due.Count - 3);
            }
            return outp;
        }

        // ── THE ADOPTION SEAM — where the whole week resolves ─────────────
        /// <summary>
        /// `dflt` is the engine's seeded-remainder net; hand it back and every
        /// non-Enterprise run is untouched.
        ///
        /// On Enterprise runs this returns 0, and that is not a shrug: the
        /// pipeline has ALREADY moved state.Traction itself, seat by seat,
        /// through named accounts — a close adds its seats, a churned logo takes
        /// all of its seats at once, an expansion adds the seats it grew. There
        /// is no smear left over for the spine to apply, and the salt-91
        /// remainder is simply not consulted.
        ///
        /// THE DRAW ORDER IS THE SPEC (section 9): churn, then age/decay/death,
        /// then advances/closes, then expansion, then spawns. One rng for
        /// mechanics (salt 50), one for names (salt 51), so a name-length draw
        /// can never shift a mechanics roll.
        /// </summary>
        public static int AdoptionNet(GameState state, WeeklyReport rep,
                                      double adds, double churn, int dflt)
        {
            if (!Active(state)) { return dflt; }

            double cap = PoolCap(state);
            // 8a ── POOL INFLOW. Fractional adds become units of unattached
            // interest; no rounding at all, strictly better than a remainder coin.
            state.PipeUnits = Gd.Minf(state.PipeUnits + adds, cap);

            Rng r = SimEngine.RngForSalt(state, SimEngine.SALT_PIPELINE);
            var ctx = new Say();
            PipeStats st = Stats(state);

            ChurnPass(state, rep, ctx, churn, r);
            DecayPass(state, rep, ctx, st, cap);
            AdvancePass(state, rep, ctx, st, r);
            ExpandPass(state, rep, ctx, r);
            SpawnPass(state, rep, ctx, st, r);

            if (ctx.More > 0)
            {
                rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "…and {0} more moved", ctx.More));
            }
            // every seat is already booked to its account — nothing smears
            return 0;
        }

        // ── sections 5 / 6 — churn, renewal cliffs, whole-logo departures ─
        /// <summary>
        /// The tick's churn stays THE churn — same formula, same knobs — but for
        /// Enterprise it lands on ACCOUNTS, not a smear of units. You lose logos,
        /// not fractions of logos, because enterprise revenue is contract-shaped.
        /// </summary>
        private static void ChurnPass(GameState state, WeeklyReport rep, Say ctx,
                                      double churn, Rng r)
        {
            double a = state.Traction;
            int seated = 0;
            foreach (Logo lg in state.Logos) { seated += lg.Seats; }
            // DM side-sales, presets and legacy saves leave units nobody named.
            // They keep the old continuous path: floor + seeded coin, on this
            // lane's own stream.
            int looseUnits = Gd.Maxi(state.Traction - seated, 0);
            double looseShare = a > 0.0 ? looseUnits / a : 0.0;
            double loose = churn * looseShare;
            int n = (int)Math.Floor(loose);
            if (r.Randf() < loose - Math.Floor(loose)) { n += 1; }
            if (n > 0)
            {
                state.Traction = Gd.Maxi(state.Traction - Gd.Mini(n, looseUnits), 0);
            }

            if (state.EraIndex() >= 3)
            {
                RenewalPass(state, rep, ctx, churn, a, r);
                return;
            }

            // BELOW `floor`: the accumulator batches the account share of the
            // churn until it is worth a whole logo, then takes one — never a
            // partial account. The expected units lost per week equal the old
            // formula exactly.
            state.PipeChurnAcc += churn * (1.0 - looseShare);
            if (state.Logos.Count == 0) { return; }
            // the pick is drawn whenever there is anything to pick, so the stream
            // position never depends on the accumulator's value
            int pick = r.RandiRange(0, state.Logos.Count - 1);
            Logo lg2 = state.Logos[pick];
            if (state.PipeChurnAcc < lg2.Seats) { return; }
            state.Traction = Gd.Maxi(state.Traction - lg2.Seats, 0);
            state.PipeChurnAcc -= lg2.Seats;
            state.Logos.RemoveAt(pick);
            ctx.Add(rep, string.Format(CultureInfo.InvariantCulture,
                "−{0} churned — {1} seats leave together (lifetime {2} wks at v0.{3})",
                lg2.Name, lg2.Seats, Gd.Maxi(state.Week - lg2.SinceWk, 0), state.Product));
        }

        /// <summary>
        /// AT `floor` THE RUN GRADUATES TO ANNUAL CONTRACTS. Renewal cliffs
        /// replace the accumulator for logos: revenue stops eroding and starts
        /// arriving in decisions, which is the truth of enterprise revenue.
        ///
        /// pRenew is the spec's formula, algebraically folded onto the churn the
        /// tick already computed:
        ///   churn = A/residence x churnMult x statusChurn x careMult x pricePain
        ///   so (RENEW_EVERY/residence) x (that product of knobs)
        ///      = RENEW_EVERY x churn / A
        /// The cliff is therefore calibrated to the continuous curve BY
        /// CONSTRUCTION — switching regimes cannot bend the churn curve, only
        /// make it cliff-shaped — and no knob has to be re-read (or drift) here.
        /// </summary>
        private static void RenewalPass(GameState state, WeeklyReport rep, Say ctx,
                                        double churn, double a, Rng r)
        {
            // logos signed before the era flipped get their first cliff scheduled now
            foreach (Logo lg in state.Logos)
            {
                if (lg.RenewalWk <= 0) { lg.RenewalWk = state.Week + RENEW_EVERY; }
            }
            double pRenew = 0.98;
            if (a > 0.0) { pRenew = Gd.Clampf(1.0 - RENEW_EVERY * churn / a, 0.50, 0.98); }
            var kept = new List<Logo>();
            foreach (Logo lg2 in state.Logos)
            {
                if (lg2.RenewalWk != state.Week) { kept.Add(lg2); continue; }
                if (r.Randf() < pRenew)
                {
                    lg2.RenewalWk += RENEW_EVERY;
                    kept.Add(lg2);
                    ctx.Add(rep, string.Format(CultureInfo.InvariantCulture,
                        "RENEWED: {0} — the annual contract holds (logo retention {1}%)",
                        lg2.Name, Gd.RoundToInt(pRenew * 100.0)));
                }
                else
                {
                    state.Traction = Gd.Maxi(state.Traction - lg2.Seats, 0);
                    ctx.Add(rep, string.Format(CultureInfo.InvariantCulture,
                        "LOST AT RENEWAL: {0} — {1} seats walk (care and quality decide renewals)",
                        lg2.Name, lg2.Seats));
                }
            }
            state.Logos = kept;
        }

        // ── section 4 — age, heat decay, and death by no-decision ─────────
        /// <summary>
        /// Spawn heat 50-65 at decay 8 means an untouched lead dies in ~6-8
        /// weeks: the "cold after N weeks" rule with N emergent and
        /// player-extendable. A dead lead REFUNDS its seats to the pool — 40-60%
        /// of forecast B2B deals are lost to no decision, and those prospects
        /// stay in-market. What the founder lost is time.
        /// </summary>
        private static void DecayPass(GameState state, WeeklyReport rep, Say ctx,
                                      PipeStats st, double cap)
        {
            int decay = DecayFor(state);
            var kept = new List<Lead>();
            foreach (Lead lead in state.Leads)
            {
                lead.AgeWeeks += 1;
                lead.Heat = Gd.Maxi(lead.Heat - decay, 0);
                if (lead.Heat > 0) { kept.Add(lead); continue; }
                state.PipeUnits = Gd.Minf(state.PipeUnits + lead.Seats, cap);
                st.Lost += 1;
                ctx.Add(rep, string.Format(CultureInfo.InvariantCulture,
                    "gone cold: {0} ({1} seats) — {2} wks of silence; enterprise deals die of no-decision, not a no",
                    lead.Name, lead.Seats, lead.AgeWeeks));
            }
            state.Leads = kept;
        }

        // ── section 4 — the advance rolls, and the close ──────────────────
        /// <summary>
        /// One seeded roll per live lead per week, in array order (the order is
        /// part of the spec). At all-factors near 1 the journey is ~8 weeks
        /// meeting to signed, 11-12 through procurement — which under this game's
        /// compressed clock reads as the real 3-9-month enterprise cycle.
        /// </summary>
        private static void AdvancePass(GameState state, WeeklyReport rep, Say ctx,
                                        PipeStats st, Rng r)
        {
            int live = state.Leads.Count;
            if (live == 0) { return; }
            var kept = new List<Lead>();
            foreach (Lead lead in state.Leads)
            {
                if (r.Randf() >= LeadAdvanceP(state, lead, live)) { kept.Add(lead); continue; }
                string nxt = NextStage(state, lead);
                if (nxt == "signed") { Close(state, rep, ctx, st, lead); continue; }
                string why = DominantWhy(state, lead, live);
                lead.Stage = nxt;
                lead.Heat = Gd.Mini(lead.Heat + HEAT_ADVANCE, 100);
                kept.Add(lead);
                ctx.Add(rep, string.Format(CultureInfo.InvariantCulture,
                    "{0} moved to {1} — {2}", lead.Name, nxt, why));
            }
            state.Leads = kept;
        }

        /// <summary>
        /// THE CLOSE. The seats become customers, the account becomes a logo, and
        /// the receipt names ACV and the sales cycle — the two numbers an
        /// enterprise founder has to learn to say out loud.
        /// </summary>
        private static void Close(GameState state, WeeklyReport rep, Say ctx,
                                  PipeStats st, Lead lead)
        {
            state.Traction = Gd.Maxi(state.Traction + lead.Seats, 0);
            state.Logos.Add(new Logo
            {
                Name = lead.Name, Seats = lead.Seats, SinceWk = state.Week,
                RenewalWk = state.EraIndex() >= 3 ? state.Week + RENEW_EVERY : 0,
            });
            st.Signed += 1;
            st.SeatsSigned += lead.Seats;
            st.CycleSum += lead.AgeWeeks;
            state.SetMeta("pipe_signed_wk", state.Week);
            double unit = UnitRevWk(state);
            ctx.Add(rep, string.Format(CultureInfo.InvariantCulture,
                "SIGNED: {0} — {1} seats · ~${2}/wk (ACV ≈ {3}) · cycle {4} wks",
                lead.Name, lead.Seats, Grp(Gd.RoundToInt(lead.Seats * unit)),
                Acv(lead.Seats * unit * 52.0), lead.AgeWeeks));
        }

        /// <summary>
        /// THE CLOSE, exposed. The twin suites drive this path directly (pin 4)
        /// and the desk's teaching footer is only honest if the close is the one
        /// place seats, logos and stats move together. Returns the seats booked,
        /// 0 for a bad index.
        /// </summary>
        public static int CloseLead(GameState state, int i, WeeklyReport rep)
        {
            if (i < 0 || i >= state.Leads.Count) { return 0; }
            Lead lead = state.Leads[i];
            Close(state, rep, new Say(), Stats(state), lead);
            state.Leads.RemoveAt(i);
            return lead.Seats;
        }

        // ── section 6 — land and expand (floor / hq) ──────────────────────
        /// <summary>
        /// Net revenue retention above 100% is the enterprise growth engine.
        /// Expansion is EARNED by product and care, never played as a move — and
        /// it draws down the same TAM through Bass's own P = N - A, so it is
        /// bounded, not free.
        /// </summary>
        private static void ExpandPass(GameState state, WeeklyReport rep, Say ctx, Rng r)
        {
            if (state.EraIndex() < 3 || state.Logos.Count == 0) { return; }
            double tam = state.Theta != null ? state.Theta.Tam : 4000.0;
            if (state.Traction >= 0.9 * tam) { return; }
            double quality = 0.6 + state.Product / 100.0 * 0.8;
            double careMult = 1.0 - 0.30 * (1.0 - Math.Exp(-state.Budgets.Care / 1500.0));
            double pExpand = 0.05 * quality * (2.0 - careMult);
            foreach (Logo lg in state.Logos)
            {
                if (r.Randf() >= pExpand) { continue; }
                int grow = Gd.Maxi((int)Math.Ceiling(lg.Seats * r.RandfRange(0.15, 0.30)), 2);
                lg.Seats += grow;
                state.Traction += grow;
                ctx.Add(rep, string.Format(CultureInfo.InvariantCulture,
                    "EXPANSION at {0}: +{1} seats — land-and-expand pays", lg.Name, grow));
            }
        }

        // ── section 3 — spawning named leads out of the pool ──────────────
        /// <summary>
        /// A BIG DEAL TAKES TIME TO MATERIALIZE. When the era's tier table draws a
        /// deal the pool cannot fund, the week does NOT shrink it into a design
        /// partner — it HOLDS, and the demand banks. Interest keeps arriving; a
        /// few quiet weeks later the pool is deep enough and the whale walks in
        /// whole.
        ///
        /// That is the honest dynamic and it is what makes the era ladder mean
        /// anything: at hq, where a third of draws are 21-60 seats, the board goes
        /// quiet for a stretch and then lands something that changes the company.
        /// Shrinking every draw to fit would have made SEAT_TIERS and SIZE_REF
        /// decorative — every deal would spawn at the floor of the smallest band
        /// forever.
        ///
        /// Deal sizes are log-normal in the real world; the era tier table
        /// approximates it, and pipeline coverage precedes bookings.
        /// </summary>
        private static void SpawnPass(GameState state, WeeklyReport rep, Say ctx,
                                      PipeStats st, Rng r)
        {
            Rng rn = null;
            int spawned = 0;
            while (spawned < SPAWNS_PER_WK && state.Leads.Count < LEAD_CAP
                   && state.PipeUnits >= MIN_SEATS)
            {
                int[] band = TierDraw(state, r);
                int seats = Gd.Maxi(r.RandiRange(band[0], band[1]), MIN_SEATS);
                // THE HOLD: the demand to fill this deal does not exist yet. Bank
                // the pool and stop the week here — a shrunken whale is a lie
                // about the market, and retrying the draw in-loop would spin until
                // something small came up.
                if (seats > Math.Floor(state.PipeUnits)) { break; }
                state.PipeUnits = Gd.Maxf(state.PipeUnits - seats, 0.0);
                int heat = r.RandiRange(HEAT_SPAWN_LO, HEAT_SPAWN_HI);
                if (rn == null) { rn = SimEngine.RngForSalt(state, SimEngine.SALT_PIPELINE_NAMES); }
                string name = PlaceholderName(state, rn);
                state.Leads.Add(new Lead
                {
                    Name = name, Flavor = "", Seats = seats,
                    Stage = "meeting", AgeWeeks = 0, Heat = heat,
                });
                if (st.FirstWk <= 0) { st.FirstWk = state.Week; }
                SpawnedThisWeek(state).Add(name);
                spawned += 1;
                ctx.Add(rep, string.Format(CultureInfo.InvariantCulture,
                    "pipeline: +{0} enters the calendar ({1} seats, first meeting)", name, seats));
            }
        }

        /// <summary>The era's seat-tier table, drawn seeded. Returns the band [lo, hi].</summary>
        private static int[] TierDraw(GameState state, Rng r)
        {
            int[] w;
            if (!SEAT_TIERS.TryGetValue(state.Era ?? "garage", out w)) { w = SEAT_TIERS["garage"]; }
            double total = 0.0;
            foreach (int x in w) { total += x; }
            if (total <= 0.0) { return SEAT_BANDS[0]; }
            double roll = r.Randf() * total;
            for (int i = 0; i < w.Length; i++)
            {
                roll -= w[i];
                if (roll <= 0.0) { return SEAT_BANDS[i]; }
            }
            return SEAT_BANDS[0];
        }

        /// <summary>
        /// THE KEYLESS NAME, which is also the instant placeholder while an L1
        /// naming call flies. Never a degraded path — the world's own Markov
        /// chain plus a sector suffix, redrawn up to three times on a collision.
        /// </summary>
        private static string PlaceholderName(GameState state, Rng rn)
        {
            HashSet<string> taken = KnownNames(state);
            string name = "";
            for (int attempt = 0; attempt < 3; attempt++)
            {
                name = WorldGen.MakeName(rn) + " " + ENT_SUFFIX[rn.RandiRange(0, ENT_SUFFIX.Length - 1)];
                name = Gd.Left(name, 30);
                if (!taken.Contains(name.ToLowerInvariant())) { return name; }
            }
            return name;
        }

        private static HashSet<string> KnownNames(GameState state)
        {
            var outp = new HashSet<string>();
            foreach (Lead ld in state.Leads) { outp.Add((ld.Name ?? "").ToLowerInvariant()); }
            foreach (Logo lg in state.Logos) { outp.Add((lg.Name ?? "").ToLowerInvariant()); }
            return outp;
        }

        /// <summary>The placeholder names spawned this week, in spawn order — the
        /// list the L1 naming callback aligns its reply against.</summary>
        public static List<string> SpawnedThisWeek(GameState state)
        {
            var have = state.GetMeta("pipe_spawned", null) as List<string>;
            if (have == null)
            {
                have = new List<string>();
                state.SetMeta("pipe_spawned", have);
            }
            return have;
        }

        // ── section 10 L1 — the ONE batch naming call ─────────────────────
        /// <summary>
        /// THE KEYLESS PATH IS THE COMPLETE PATH. Every lead already has a name
        /// the moment it spawns (section 3, salt 51), so this call only ever
        /// replaces WORDS — the board is fully playable before, during and after
        /// it, and a run with no key is not a degraded run.
        ///
        /// Same shape as the labor lane's applicant dressing: a payload the week
        /// something spawned, and a rows lander that refuses a bad reply whole.
        /// Returns an empty dictionary when there is nothing to name — the caller
        /// skips the call.
        /// </summary>
        public static Dictionary<string, object> DressingPayload(GameState state)
        {
            var empty = new Dictionary<string, object>();
            if (!Active(state)) { return empty; }
            List<string> spawned = SpawnedThisWeek(state);
            if (spawned.Count == 0) { return empty; }
            var fresh = new List<Dictionary<string, object>>();
            foreach (Lead lead in state.Leads)
            {
                if (!spawned.Contains(lead.Name ?? "")) { continue; }
                fresh.Add(new Dictionary<string, object>
                {
                    { "placeholder", lead.Name }, { "band", BandWord(lead.Seats) },
                    { "stage", "meeting" },
                });
            }
            if (fresh.Count == 0) { return empty; }
            var taken = new List<string>();
            foreach (string k in KnownNames(state)) { taken.Add(k); }
            foreach (Rival rv in state.Rivals) { taken.Add(rv.Name); }
            foreach (Investor iv in state.Investors) { taken.Add(iv.Name); }
            return new Dictionary<string, object>
            {
                { "company", new Dictionary<string, object> {
                    { "name", state.CompanyName }, { "idea", state.CompanyIdea },
                    { "what", state.BizWhat }, { "who", state.BizWho } } },
                { "era", state.Era }, { "existing_names", taken }, { "new_leads", fresh },
            };
        }

        /// <summary>The size band, as a word the model can write scale into. It is
        /// INPUT for flavor only — the model never returns a number, and seats are
        /// the dice's.</summary>
        private static string BandWord(int seats)
        {
            if (seats >= 61) { return "whale"; }
            if (seats >= 21) { return "large"; }
            if (seats >= 9) { return "mid"; }
            return "small";
        }

        /// <summary>Land a reply: rows of {name, one_liner} in spawn order.
        /// Returns how many leads were dressed; 0 means the reply was discarded
        /// and the placeholders stand — a complete board either way, so nothing is
        /// ever waiting on this.</summary>
        public static int DressLeadsRows(GameState state, JArray rows)
        {
            if (rows == null) { return 0; }
            var names = new List<string>();
            var flavors = new List<string>();
            foreach (JToken tok in rows)
            {
                var row = tok as JObject;
                if (row == null) { return 0; }
                names.Add(RowStr(row, "name"));
                flavors.Add(RowStr(row, "one_liner"));
            }
            return DressLeads(state, names, flavors);
        }

        private static string RowStr(JObject row, string key)
        {
            JToken t = row[key];
            return t == null || t.Type == JTokenType.Null ? "" : t.ToString();
        }

        /// <summary>
        /// The typed core. The caller hands back one name and one one-liner per
        /// lead spawned this week, in the order they spawned; this overwrites Name
        /// and Flavor and NOTHING else. Seats, stage, heat and age are the dice's,
        /// always.
        ///
        /// A count mismatch is refused whole (the placeholders are already good
        /// names), a collision keeps the placeholder, and a save between spawn and
        /// reply simply persists the placeholders.
        /// </summary>
        public static int DressLeads(GameState state, IList<string> names, IList<string> flavors)
        {
            if (!Active(state)) { return 0; }
            List<string> spawned = SpawnedThisWeek(state);
            if (spawned.Count == 0 || names == null || names.Count != spawned.Count) { return 0; }
            int dressed = 0;
            for (int i = 0; i < spawned.Count; i++)
            {
                string want = spawned[i];
                string fresh = Gd.Left((names[i] ?? "").Trim(), 30);
                if (fresh.Length == 0) { continue; }
                foreach (Lead lead in state.Leads)
                {
                    if (lead.Name != want) { continue; }
                    HashSet<string> taken = KnownNames(state);
                    taken.Remove(want.ToLowerInvariant());
                    if (!taken.Contains(fresh.ToLowerInvariant())) { lead.Name = fresh; }
                    if (flavors != null && i < flavors.Count)
                    {
                        lead.Flavor = Gd.Left((flavors[i] ?? "").Trim(), 90);
                    }
                    dressed += 1;
                    break;
                }
            }
            return dressed;
        }

        // ── section 7 THE push_lead OP ────────────────────────────────────
        /// <summary>
        /// The founder writes a move that leans on a deal. Executive engagement
        /// measurably lifts win rates; it does not sign contracts by itself — so
        /// a push moves HEAT and nothing else. It never advances a stage, never
        /// adds traction, and a negative push is legal (a botched demo cools a
        /// deal).
        ///
        /// Returns the receipt line, or "" when no live lead matched — the
        /// executor turns an empty return into the sentinel's "no such lead" line.
        /// </summary>
        public static string PushLead(GameState state, string leadName, int heatDelta)
        {
            if (!Active(state) || state.Leads.Count == 0) { return ""; }
            string want = (leadName ?? "").Trim().ToLowerInvariant();
            if (want.Length == 0) { return ""; }
            int delta = Gd.Clampi(heatDelta, -PUSH_CLAMP, PUSH_CLAMP);
            foreach (Lead lead in state.Leads)
            {
                string have = (lead.Name ?? "").ToLowerInvariant();
                if (have.Length == 0 || !(have.Contains(want) || want.Contains(have))) { continue; }
                lead.Heat = Gd.Clampi(lead.Heat + delta, 0, 100);
                return string.Format(CultureInfo.InvariantCulture,
                    "pushed {0}: heat {1} — the deal reads {2} now",
                    lead.Name, Signed(delta), HeatWord(lead.Heat));
            }
            return "";
        }

        // ── section 11 DM context, and the bangs ──────────────────────────
        /// <summary>
        /// The DM's context lists the board BY NAME, so narration references real
        /// leads and the adjudicator has something true to push. The engine still
        /// owns every number in these lines — they are a read, never a lever.
        /// </summary>
        public static List<string> Directives(GameState state)
        {
            var outp = new List<string>();
            if (!Active(state)) { return outp; }
            if (state.Leads.Count > 0)
            {
                List<int> order = LeadsByHeat(state);
                var parts = new List<string>();
                int decay = DecayFor(state);
                for (int i = 0; i < Gd.Mini(order.Count, 5); i++)
                {
                    Lead lead = state.Leads[order[i]];
                    string word = HeatWord(lead.Heat);
                    int dies = WeeksToCold(lead.Heat, decay);
                    if (dies <= 2)
                    {
                        word += string.Format(CultureInfo.InvariantCulture, " — dies in {0} wk", dies);
                    }
                    parts.Add(string.Format(CultureInfo.InvariantCulture, "{0} ({1}, {2} seats, {3})",
                        lead.Name, lead.Stage, lead.Seats, word));
                }
                string line = "Pipeline: " + string.Join(" · ", parts.ToArray());
                if (order.Count > 5)
                {
                    line += string.Format(CultureInfo.InvariantCulture, " (+{0} more)", order.Count - 5);
                }
                outp.Add(line);
            }
            if (Gd.ToInt(state.GetMetaF("pipe_signed_wk", 0.0)) == state.Week && state.Week > 0)
            {
                Logo last = state.Logos.Count > 0 ? state.Logos[state.Logos.Count - 1] : null;
                outp.Add(string.Format(CultureInfo.InvariantCulture,
                    "SIGNED THIS WEEK: {0} ({1} seats). Let the week feel it.",
                    last != null ? last.Name : "a new logo", last != null ? last.Seats : 0));
            }
            string cold = Coldest(state);
            if (cold.Length > 0)
            {
                outp.Add(string.Format(CultureInfo.InvariantCulture,
                    "A lead is about to go cold: {0}. If the move works a named lead, use push_lead {{cat: the exact lead name, v: heat −40..40}}.",
                    cold));
            }
            outp.Add("Enterprise law: customers arrive ONLY through signed contracts. "
                + "Never grant traction for pipeline work — heat the lead instead.");
            return outp;
        }

        /// <summary>
        /// Attention rows — the customers desk. The bang pulls the player to the
        /// board exactly when a deal is dying or one has just landed, and never
        /// otherwise.
        /// </summary>
        public static List<AttentionItem> Attention(GameState state)
        {
            var rows = new List<AttentionItem>();
            if (!Active(state)) { return rows; }
            int cold = 0;
            foreach (Lead ld in state.Leads) { if (ld.Heat <= 16) { cold += 1; } }
            if (cold == 1)
            {
                rows.Add(new AttentionItem { Desk = "customers", Key = "lead_cold",
                    Severity = 2, Label = "a deal is going cold — push it" });
            }
            else if (cold > 1)
            {
                rows.Add(new AttentionItem { Desk = "customers", Key = "lead_cold", Severity = 2,
                    Label = string.Format(CultureInfo.InvariantCulture,
                        "{0} deals going cold — push them", cold) });
            }
            if (Gd.ToInt(state.GetMetaF("pipe_signed_wk", 0.0)) == state.Week && state.Week > 0)
            {
                rows.Add(new AttentionItem { Desk = "customers", Key = "signed",
                    Severity = 1, Label = "a contract signed — seats booked" });
            }
            return rows;
        }

        // ── reads the desk and the DM share ──────────────────────────────
        /// <summary>
        /// Lead indices ordered hottest first; the array index breaks every tie,
        /// so two reads of one state can never disagree about who is at the top
        /// of the board.
        /// </summary>
        public static List<int> LeadsByHeat(GameState state)
        {
            var idx = new List<int>();
            for (int i = 0; i < state.Leads.Count; i++) { idx.Add(i); }
            idx.Sort((x, y) =>
            {
                int hx = state.Leads[x].Heat;
                int hy = state.Leads[y].Heat;
                if (hx != hy) { return hy.CompareTo(hx); }
                return x.CompareTo(y);
            });
            return idx;
        }

        /// <summary>The name of the coldest lead that is genuinely about to die, or "".</summary>
        private static string Coldest(GameState state)
        {
            int worst = 17;
            string name = "";
            foreach (Lead ld in state.Leads)
            {
                if (ld.Heat <= 16 && ld.Heat < worst) { worst = ld.Heat; name = ld.Name; }
            }
            return name ?? "";
        }

        /// <summary>Live seats sitting on the board — the number the desk's summary prints.</summary>
        public static int SeatsInMotion(GameState state)
        {
            int n = 0;
            foreach (Lead ld in state.Leads) { n += ld.Seats; }
            return n;
        }

        /// <summary>
        /// THE DIGEST'S TWO ENTRIES (section 11), so tier-2 event cards see the
        /// same board the adjudicator does and can write follow-ups about a real
        /// deal by name. Empty off Enterprise — the digest simply gains nothing.
        /// </summary>
        public static Dictionary<string, object> DigestRows(GameState state)
        {
            var outp = new Dictionary<string, object>();
            if (!Active(state)) { return outp; }
            var board = new List<string>();
            foreach (int i in LeadsByHeat(state))
            {
                Lead lead = state.Leads[i];
                board.Add(string.Format(CultureInfo.InvariantCulture, "{0} — {1}, {2} seats, {3}",
                    lead.Name, lead.Stage, lead.Seats, HeatWord(lead.Heat)));
            }
            int seated = 0;
            foreach (Logo lg in state.Logos) { seated += lg.Seats; }
            outp["pipeline"] = board;
            outp["signed_logos"] = string.Format(CultureInfo.InvariantCulture,
                "{0} logos, {1} seats", state.Logos.Count, seated);
            return outp;
        }

        /// <summary>One line for the signals block: the board at a glance.</summary>
        public static string SignalLine(GameState state)
        {
            if (!Active(state)) { return ""; }
            string hottest = "nobody yet";
            List<int> order = LeadsByHeat(state);
            if (order.Count > 0)
            {
                Lead lead = state.Leads[order[0]];
                hottest = string.Format(CultureInfo.InvariantCulture, "{0} ({1}, {2})",
                    lead.Name, lead.Stage, HeatWord(lead.Heat));
            }
            return string.Format(CultureInfo.InvariantCulture,
                "{0} live ({1} seats) · hottest {2} · pool {3} seats",
                state.Leads.Count, SeatsInMotion(state), hottest, Gd.F(state.PipeUnits, 1));
        }

        // ── the running totals the desk divides ──────────────────────────
        /// <summary>PipeStats, never null. An old save can carry one that never
        /// existed; the desk must never divide by a missing struct.</summary>
        public static PipeStats Stats(GameState state)
        {
            if (state.PipeStats == null) { state.PipeStats = new PipeStats(); }
            return state.PipeStats;
        }

        // ── receipts ─────────────────────────────────────────────────────
        /// <summary>Six pipeline lines a week, then the truth about the rest. The
        /// journal is a page, not a log file.</summary>
        private sealed class Say
        {
            public int Said;
            public int More;

            public void Add(WeeklyReport rep, string line)
            {
                if (Said < MAX_LINES) { rep.Lines.Add(line); Said += 1; }
                else { More += 1; }
            }
        }

        /// <summary>A signed delta the way a founder writes one: +20, −40.</summary>
        private static string Signed(int v)
        {
            return v >= 0
                ? "+" + v.ToString(CultureInfo.InvariantCulture)
                : "−" + Gd.Absi(v).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Annual contract value, said the way a salesperson says it.</summary>
        private static string Acv(double v)
        {
            if (v >= 1000000.0) { return "$" + Gd.F(v / 1000000.0, 1) + "M"; }
            if (v >= 1000.0)
            {
                return "$" + Gd.RoundToInt(v / 1000.0).ToString(CultureInfo.InvariantCulture) + "k";
            }
            return "$" + Gd.RoundToInt(v).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Thousands separators, engine-side, so a receipt reads like an invoice.</summary>
        private static string Grp(int n)
        {
            string s = Gd.Absi(n).ToString(CultureInfo.InvariantCulture);
            string outp = "";
            int c = 0;
            for (int i = s.Length - 1; i >= 0; i--)
            {
                outp = s[i] + outp;
                c += 1;
                if (c % 3 == 0 && i > 0) { outp = "," + outp; }
            }
            return (n < 0 ? "−" : "") + outp;
        }
    }
}
