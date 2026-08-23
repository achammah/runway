using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace Runway.Core
{
    /// <summary>
    /// THE WORLD CONSTANTS, generated once per run from the pitch. GDScript keeps
    /// these in a loose Dictionary; here they are named fields with the same JSON
    /// keys, plus a string indexer so ClampTheta can walk them exactly as the
    /// original walks THETA_CLAMPS.
    /// </summary>
    public sealed class Theta
    {
        [JsonProperty("tam")] public double Tam = 120000.0;                 // buyers in the whole market
        [JsonProperty("adopt_p")] public double AdoptP = 0.00025;           // weekly independent adoption fraction
        [JsonProperty("adopt_ic")] public double AdoptIc = 0.06;            // word-of-mouth contact*conversion
        [JsonProperty("lifetime_wk")] public double LifetimeWk = 40.0;      // customer residence at product=50
        [JsonProperty("arpu_wk")] public double ArpuWk = 5.0;               // $ per customer per week at price 1.0
        [JsonProperty("cac_sat")] public double CacSat = 8000.0;            // marketing saturation $ per week
        [JsonProperty("rival_strength")] public double RivalStrength = 20.0;
        [JsonProperty("trend_vol")] public double TrendVol = 0.02;          // market mood volatility per week
        [JsonProperty("burn_mult")] public double BurnMult = 1.0;           // difficulty
        [JsonProperty("churn_mult")] public double ChurnMult = 1.0;
        [JsonProperty("funding_mult")] public double FundingMult = 1.0;

        public Theta Duplicate()
        {
            return (Theta)MemberwiseClone();
        }

        public double this[string key]
        {
            get
            {
                switch (key)
                {
                    case "tam": return Tam;
                    case "adopt_p": return AdoptP;
                    case "adopt_ic": return AdoptIc;
                    case "lifetime_wk": return LifetimeWk;
                    case "arpu_wk": return ArpuWk;
                    case "cac_sat": return CacSat;
                    case "rival_strength": return RivalStrength;
                    case "trend_vol": return TrendVol;
                    case "burn_mult": return BurnMult;
                    case "churn_mult": return ChurnMult;
                    case "funding_mult": return FundingMult;
                    default: throw new ArgumentException("unknown theta key: " + key);
                }
            }
            set
            {
                switch (key)
                {
                    case "tam": Tam = value; break;
                    case "adopt_p": AdoptP = value; break;
                    case "adopt_ic": AdoptIc = value; break;
                    case "lifetime_wk": LifetimeWk = value; break;
                    case "arpu_wk": ArpuWk = value; break;
                    case "cac_sat": CacSat = value; break;
                    case "rival_strength": RivalStrength = value; break;
                    case "trend_vol": TrendVol = value; break;
                    case "burn_mult": BurnMult = value; break;
                    case "churn_mult": ChurnMult = value; break;
                    case "funding_mult": FundingMult = value; break;
                    default: throw new ArgumentException("unknown theta key: " + key);
                }
            }
        }
    }

    /// <summary>
    /// One entry of the status catalog. Anything the catalog leaves out reads as
    /// the neutral default, exactly as GDScript's Dictionary.get(key, 1.0) does.
    /// </summary>
    public sealed class StatusDef
    {
        [JsonProperty("adopt_mult")] public double AdoptMult = 1.0;
        [JsonProperty("churn_mult")] public double ChurnMult = 1.0;
        [JsonProperty("arpu_mult")] public double ArpuMult = 1.0;
        [JsonProperty("velocity_mult")] public double VelocityMult = 1.0;
        [JsonProperty("hype_wk")] public double HypeWk;
        [JsonProperty("fatigue_wk")] public double FatigueWk;
        [JsonProperty("morale_wk")] public double MoraleWk;
        [JsonProperty("adv")] public string Adv = "";
        [JsonProperty("dis")] public string Dis = "";
        [JsonProperty("kind")] public string Kind = "";
    }

    /// <summary>The week's REPORT: every delta with its why, so the journal prints receipts.</summary>
    public sealed class WeeklyReport
    {
        [JsonProperty("lines")] public List<string> Lines = new List<string>();
        [JsonProperty("fired_clocks")] public List<string> FiredClocks = new List<string>();
        [JsonProperty("expired")] public List<string> Expired = new List<string>();
        [JsonProperty("events")] public List<string> Events = new List<string>();
        [JsonProperty("adds")] public int Adds;
        [JsonProperty("churn")] public int Churn;
        [JsonProperty("revenue")] public int Revenue;
        [JsonProperty("burn")] public int Burn;
        [JsonProperty("cac")] public int Cac;
        [JsonProperty("ltv")] public int Ltv;
        [JsonProperty("payback_wk")] public int PaybackWk;
    }

    /// <summary>Advantage/disadvantage and the dice that came out of it.</summary>
    public sealed class RollContext
    {
        [JsonProperty("advantage")] public bool Advantage;
        [JsonProperty("disadvantage")] public bool Disadvantage;
        [JsonProperty("adv_reasons")] public List<string> AdvReasons = new List<string>();
        [JsonProperty("dis_reasons")] public List<string> DisReasons = new List<string>();
        [JsonProperty("rolls")] public List<int> Rolls = new List<int>();
        [JsonProperty("a")] public int A;
        [JsonProperty("b")] public int B;
        [JsonProperty("luck_note")] public string LuckNote = "";
        [JsonProperty("d20")] public int D20;
        [JsonProperty("mod")] public int Mod;
        [JsonProperty("total")] public int Total;
    }

    /// <summary>One term sheet on the table.</summary>
    public sealed class FundingOffer
    {
        [JsonProperty("investor")] public string Investor = "?";
        [JsonProperty("amount")] public int Amount;
        [JsonProperty("equity_pct")] public double EquityPct;
        [JsonProperty("fair_pct")] public double FairPct;
        [JsonProperty("warmth")] public double Warmth;
        [JsonProperty("thesis")] public string Thesis = "";
    }

    /// <summary>
    /// THE DETERMINISTIC BUSINESS ENGINE.
    ///
    /// The one law: the ENGINE owns every number, the DM owns every sentence, a
    /// narrow typed schema is the only bridge. This file is the world that grinds
    /// the company down by default — burn, churn, fatigue, debt, rivals — so that
    /// doing nothing is slow death and the weekly written move is how the founder
    /// pushes back.
    ///
    /// Formula provenance:
    ///   Bass adoption + churn-by-quality      — BSL system-dynamics library
    ///   elasticity / CAC / funding math       — Ventiqra
    ///   buff slots / staffing balance         — TeamDay business-tycoon
    ///   burnout cliff / resignation roll      — Ventiqra morale module
    ///   market demography                     — opendnd Dominia
    ///
    /// Everything externally settable passes a clamp. Every stochastic subsystem
    /// rolls on its own salted stream keyed (seed, week), so a run replays exactly.
    /// </summary>
    public static class SimEngine
    {
        // ───────────────────────── THETA: the world constants ────────────────────
        public static readonly List<string> THETA_KEYS = new List<string>
        {
            "tam", "adopt_p", "adopt_ic", "lifetime_wk", "arpu_wk", "cac_sat",
            "rival_strength", "trend_vol", "burn_mult", "churn_mult", "funding_mult"
        };

        public static readonly Dictionary<string, double[]> THETA_CLAMPS = new Dictionary<string, double[]>
        {
            { "tam", new[] { 2000.0, 5000000.0 } },
            { "adopt_p", new[] { 0.00005, 0.004 } },
            { "adopt_ic", new[] { 0.05, 0.9 } },
            { "lifetime_wk", new[] { 6.0, 200.0 } },
            { "arpu_wk", new[] { 0.5, 5000.0 } },
            { "cac_sat", new[] { 500.0, 100000.0 } },
            { "rival_strength", new[] { 5.0, 60.0 } },
            { "trend_vol", new[] { 0.005, 0.05 } },
            { "burn_mult", new[] { 0.6, 1.8 } },
            { "churn_mult", new[] { 0.5, 1.8 } },
            { "funding_mult", new[] { 0.5, 1.5 } },
        };

        public static Theta DefaultTheta(string what, string who)
        {
            var t = new Theta
            {
                Tam = 120000.0, AdoptP = 0.00025, AdoptIc = 0.06,
                LifetimeWk = 40.0, ArpuWk = 5.0, CacSat = 8000.0,
                RivalStrength = 20.0, TrendVol = 0.02,
                BurnMult = 1.0, ChurnMult = 1.0, FundingMult = 1.0,
            };
            switch (who)
            {
                case "Enterprise":
                    t.Tam = 4000.0; t.ArpuWk = 400.0; t.AdoptP = 0.00018;
                    t.AdoptIc = 0.02; t.LifetimeWk = 90.0;
                    break;
                case "SMB":
                    t.Tam = 60000.0; t.ArpuWk = 14.0; t.LifetimeWk = 50.0;
                    break;
                case "Consumer":
                    t.Tam = 900000.0; t.ArpuWk = 0.9; t.AdoptIc = 0.15;
                    t.LifetimeWk = 22.0;
                    break;
            }
            switch (what)
            {
                case "Hardware":
                    t.ArpuWk *= 2.2; t.AdoptP *= 0.6; t.LifetimeWk *= 1.4;
                    break;
                case "Marketplace":
                    t.AdoptIc *= 1.3; t.ArpuWk *= 0.5;
                    break;
                case "Service":
                    t.AdoptP *= 1.5; t.ArpuWk *= 1.8; t.Tam *= 0.3;
                    break;
            }
            return ClampTheta(t);
        }

        public static Theta ClampTheta(Theta t)
        {
            Theta outp = t.Duplicate();
            foreach (string k in THETA_KEYS)
            {
                double[] c = THETA_CLAMPS[k];
                outp[k] = Gd.Clampf(outp[k], c[0], c[1]);
            }
            return outp;
        }

        /// <summary>
        /// The hostile-input path: a PARTIAL set of world constants (an LLM's, a
        /// save file's). Anything missing falls to the midpoint of its clamp
        /// window before clamping, exactly as GDScript's
        /// out.get(k, (c[0] + c[1]) * 0.5) does.
        /// </summary>
        public static Theta ClampTheta(IDictionary<string, double> partial)
        {
            var outp = new Theta();
            foreach (string k in THETA_KEYS)
            {
                double[] c = THETA_CLAMPS[k];
                double v;
                if (partial == null || !partial.TryGetValue(k, out v))
                {
                    v = (c[0] + c[1]) * 0.5;
                }
                outp[k] = Gd.Clampf(v, c[0], c[1]);
            }
            return outp;
        }

        // ───────────────────────── the status catalog ────────────────────────────
        /// <summary>
        /// Conditions and buffs are ONE typed catalog: the DM (or the engine
        /// itself) installs a status BY NAME with a duration; the magnitudes live
        /// HERE, so the LLM can never invent an untyped modifier. adv/dis grant
        /// advantage or disadvantage on the named stat while active.
        /// </summary>
        public static readonly Dictionary<string, StatusDef> STATUS = new Dictionary<string, StatusDef>
        {
            { "press_surge",       new StatusDef { AdoptMult = 1.6, HypeWk = 4.0, Kind = "buff" } },
            { "press_darling",     new StatusDef { AdoptMult = 1.25, Adv = "sell", Kind = "buff" } },
            { "word_of_mouth",     new StatusDef { AdoptMult = 1.35, Kind = "buff" } },
            { "viral_moment",      new StatusDef { AdoptMult = 2.2, Kind = "buff" } },
            { "enterprise_pilot",  new StatusDef { ArpuMult = 1.3, Kind = "buff" } },
            { "crunch",            new StatusDef { VelocityMult = 1.35, FatigueWk = 9.0, Kind = "buff" } },
            { "investor_pressure", new StatusDef { MoraleWk = -2.0, Dis = "raise", Kind = "condition" } },
            { "burnt_out",         new StatusDef { VelocityMult = 0.6, Dis = "grit", Kind = "condition" } },
            { "press_backlash",    new StatusDef { AdoptMult = 0.6, HypeWk = -6.0, Kind = "condition" } },
            { "outage_fallout",    new StatusDef { ChurnMult = 1.6, Dis = "sell", Kind = "condition" } },
            { "churn_spiral",      new StatusDef { ChurnMult = 1.4, Kind = "condition" } },
            { "lawsuit_cloud",     new StatusDef { Dis = "raise", MoraleWk = -1.0, Kind = "condition" } },
            { "talent_magnet",     new StatusDef { Adv = "recruit", Kind = "buff" } },
            { "data_room_ready",   new StatusDef { Adv = "raise", Kind = "buff" } },
            { "founder_flow",      new StatusDef { Adv = "build", VelocityMult = 1.15, Kind = "buff" } },
            { "market_tailwind",   new StatusDef { AdoptMult = 1.3, Kind = "buff" } },
            { "market_headwind",   new StatusDef { AdoptMult = 0.7, Kind = "condition" } },
            { "rival_fud",         new StatusDef { AdoptMult = 0.8, Dis = "sell", Kind = "condition" } },
        };

        private static readonly StatusDef NO_STATUS = new StatusDef();

        /// <summary>The catalog entry, or the neutral default for a name the catalog never heard of.</summary>
        public static StatusDef StatusEffect(string name)
        {
            StatusDef d;
            return STATUS.TryGetValue(name ?? "", out d) ? d : NO_STATUS;
        }

        // ─────────────────── seeded per-subsystem randomness ─────────────────────
        private static Rng RngFor(GameState state, int salt)
        {
            return Rng.Salted(state.SimSeed, state.Week, salt);
        }

        // ───────────────────────── lookup curves (BSL) ───────────────────────────
        /// <summary>Janoschek falling curve: 1.0 at x=0 down to floor_v as x grows, knee at x_ref.</summary>
        public static double JanoDown(double x, double xRef, double floorV = 0.25)
        {
            if (x <= 0.0)
            {
                return 1.0;
            }
            double k = 0.6931 / Gd.Maxf(xRef, 0.001);   // ln2: halfway to the floor at x_ref
            return floorV + (1.0 - floorV) * Math.Exp(-k * x);
        }

        // ═══════════════════════════ THE WEEKLY TICK ═════════════════════════════
        /// <summary>The hostile world, in order.</summary>
        public static WeeklyReport WeeklyTick(GameState state)
        {
            var rep = new WeeklyReport();
            Theta th = state.Theta;
            if (th == null)
            {
                th = DefaultTheta(state.BizWhat, state.BizWho);
                state.Theta = th;
            }

            // 1 ── clocks: deadlines fire deterministically
            var keptClocks = new List<Clock>();
            foreach (Clock cd in state.Clocks)
            {
                cd.WeeksLeft = cd.WeeksLeft - 1;
                if (cd.WeeksLeft <= 0)
                {
                    rep.FiredClocks.Add(cd.Consequence ?? "a deadline passes");
                }
                else
                {
                    keptClocks.Add(cd);
                }
            }
            state.Clocks = keptClocks;

            // 2 ── statuses decrement, expire
            var keptStatus = new List<Status>();
            foreach (Status sd in state.Statuses)
            {
                sd.WeeksLeft = sd.WeeksLeft - 1;
                if (sd.WeeksLeft <= 0)
                {
                    rep.Expired.Add(sd.Name ?? "");
                }
                else
                {
                    keptStatus.Add(sd);
                }
            }
            state.Statuses = keptStatus;

            // 3 ── the hiring pipeline advances: cohort 0 onboards, then productive
            if (state.Pipeline.Count > 0)
            {
                var grads = new List<PipelineHire>();
                var still = new List<PipelineHire>();
                foreach (PipelineHire hd in state.Pipeline)
                {
                    hd.WeeksIn = hd.WeeksIn + 1;
                    if (hd.WeeksIn >= 2)
                    {
                        grads.Add(hd);
                    }
                    else
                    {
                        still.Add(hd);
                    }
                }
                state.Pipeline = still;
                foreach (PipelineHire g in grads)
                {
                    state.Employees.Add(new Employee
                    {
                        Name = g.Name ?? "hire",
                        Role = g.Role ?? "engineer",
                        Salary = g.Salary,
                        Burnout = 10,
                        Quirk = g.Quirk ?? "",
                    });
                    rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0} finished onboarding — productive now", g.Name ?? "a hire"));
                }
            }

            // 4 ── fatigue and morale drift (the slow tax)
            bool crunching = HasStatus(state, "crunch");
            double targetFatigue = crunching ? 65.0 : 20.0;
            state.Fatigue += (targetFatigue - state.Fatigue) / 4.0;
            // morale drifts toward a lived-in 50 (up when battered, down when coasting);
            // statuses, red ink and events push around that baseline
            double moraleWk = (50.0 - state.Morale) / 6.0;
            foreach (Status s2 in state.Statuses)
            {
                moraleWk += StatusEffect(s2.Name).MoraleWk;
            }
            if (state.Cash < 0)
            {
                moraleWk -= 3.0;
            }
            state.Morale = Gd.Clampi(Gd.ToInt(state.Morale + moraleWk), 0, 100);
            // burnout cliff: below 30 someone may walk — best people first
            if (state.Morale < 30 && state.Employees.Count > 0)
            {
                Rng r4 = RngFor(state, 4);
                if (r4.Randf() < 0.6 * (31 - state.Morale) / 31.0)
                {
                    int bestI = 0;
                    for (int i = 0; i < state.Employees.Count; i++)
                    {
                        if (state.Employees[i].Salary > state.Employees[bestI].Salary)
                        {
                            bestI = i;
                        }
                    }
                    Employee quit = state.Employees[bestI];
                    state.Employees.RemoveAt(bestI);
                    rep.Events.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0} quit (morale {1}): the good ones leave first", quit.Name ?? "someone", state.Morale));
                }
            }
            // exhaustion track 0-6 rises with fatigue, falls with rest
            if (state.Fatigue > 55.0)
            {
                state.Exhaustion = Gd.Mini(state.Exhaustion + 1, 6);
            }
            else if (state.Fatigue < 30.0 && state.Exhaustion > 0)
            {
                state.Exhaustion -= 1;
            }
            if (state.Exhaustion >= 4 && !HasStatus(state, "burnt_out"))
            {
                AddStatus(state, "burnt_out", 3);
                rep.Events.Add(string.Format(CultureInfo.InvariantCulture,
                    "the founder is burnt out (exhaustion {0})", state.Exhaustion));
            }

            // 5 ── tech debt: decays product if nobody builds; outage roll
            int eng = 0;
            foreach (Employee e in state.Employees)
            {
                if ((e.Role ?? "").Contains("engineer"))
                {
                    eng += 1;
                }
            }
            if (eng == 0 && state.Competence("build") < 4)
            {
                state.TechDebt = Gd.Minf(state.TechDebt + 1.5, 100.0);
            }
            Rng r5 = RngFor(state, 5);
            if (state.TechDebt > 40.0 && r5.Randf() < (state.TechDebt - 40.0) / 250.0)
            {
                AddStatus(state, "outage_fallout", 2);
                rep.Events.Add(string.Format(CultureInfo.InvariantCulture,
                    "OUTAGE — the debt collected (debt {0})", Gd.ToInt(state.TechDebt)));
            }

            // 6 ── rivals ratchet up; occasional launch
            Rng r6 = RngFor(state, 6);
            foreach (Rival rd in state.Rivals)
            {
                rd.Strength = Gd.Minf(rd.Strength + r6.RandfRange(0.0, 1.2), 95.0);
                rd.WeeksSinceMove = rd.WeeksSinceMove + 1;
                if (rd.WeeksSinceMove >= 5 && r6.Randf() < 0.4)
                {
                    rd.WeeksSinceMove = 0;
                    rd.Strength = Gd.Minf(rd.Strength + 4.0, 95.0);
                    List<string> tactics = (rd.Tactics != null && rd.Tactics.Count > 0)
                        ? rd.Tactics : new List<string> { "shipped something loud" };
                    string move = tactics[(int)(r6.Randi() % (uint)tactics.Count)];
                    rep.Events.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0} made a move — {1}", rd.Name ?? "a rival", move));
                }
            }
            double pressure = 0.0;
            foreach (Rival rv2 in state.Rivals)
            {
                pressure += rv2.Strength;
            }
            pressure = Gd.Minf(pressure / Gd.Maxf(state.Rivals.Count, 1.0) / 100.0 * 0.5, 0.45);

            // 7 ── market mood random walk
            Rng r7 = RngFor(state, 7);
            state.MarketTrend = Gd.Clampf(state.MarketTrend + r7.RandfRange(-1.0, 1.0) * th.TrendVol, 0.5, 1.5);

            // 8 ── adoption and churn (Bass + quality residence)
            double A = state.Traction;
            double N = th.Tam;
            double P = Gd.Maxf(N - A, 0.0);
            double hypeMult = 0.6 + state.Hype / 100.0 * 0.9;
            // THE LEVERS: four weekly budgets the player sets in the Binder's ledger.
            // Every dollar is real: it leaves cash in section 9, and it does exactly this —
            //   marketing -> reach (diminishing via cac_sat), sales -> closing capacity,
            //   care -> retention, rnd -> product quality and debt paydown.
            Budgets bud = state.Budgets;
            double bMk = bud.Marketing + state.MarketingBudget;
            double bSales = bud.Sales;
            double bCare = bud.Care;
            double bRnd = bud.Rnd;
            double mkBudget = bMk;
            double mkMult = 1.0 + 1.4 * (1.0 - Math.Exp(-mkBudget / th.CacSat));
            double statusAdopt = 1.0;
            double statusChurn = 1.0;
            double statusArpu = 1.0;
            foreach (Status s3 in state.Statuses)
            {
                StatusDef eff = StatusEffect(s3.Name);
                statusAdopt *= eff.AdoptMult;
                statusChurn *= eff.ChurnMult;
                statusArpu *= eff.ArpuMult;
            }
            // NOTHING SELLS ITSELF BEFORE LAUNCH: organic adoption requires the launch;
            // an unlaunched product only grows by word of mouth of the few it has (half
            // rate) and whatever the founder's written moves win directly.
            bool launched = state.HasFlag("launched");
            double qualityGate = 0.2 + state.Product / 100.0 * 0.8;
            double pEff = th.AdoptP * hypeMult * mkMult * statusAdopt
                * state.MarketTrend * (1.0 - pressure) * qualityGate
                * (launched ? 1.0 : 0.0);
            double wom = th.AdoptIc * A * P / Gd.Maxf(N, 1.0) * statusAdopt
                * (1.0 - pressure) * qualityGate * (launched ? 1.0 : 0.5);
            double priceDemand = Math.Pow(Gd.Maxf(state.PriceMult, 0.1), -1.5);
            double offerMult = OffersDemandMult(state);
            if (offerMult >= 0.0)
            {
                // offers exist: THEY are the price signal. Nothing on sale still lets
                // people sign up out of interest (half rate) — but nobody pays.
                priceDemand = offerMult == 0.0 ? 0.5 : offerMult;
            }
            double adds = (pEff * P + wom) * Gd.Clampf(priceDemand, 0.1, 3.0);
            // THE GTM CAPACITY CLAMP: demand is not closing. A tiny team can only land
            // what its go-to-market can actually handle — founder sell-stat, sales
            // hires, and marketing reach set the weekly ceiling.
            int salesHeads = 0;
            foreach (Employee e3 in state.Employees)
            {
                if ((e3.Role ?? "").Contains("sales"))
                {
                    salesHeads += 1;
                }
            }
            double capScale = 1.0;
            switch (state.BizWho)
            {
                case "SMB": capScale = 3.0; break;
                case "Consumer": capScale = 40.0; break;
            }
            // a sales budget hires fractional closing power (an SDR-hour equivalent)
            double gtmCap = (1.5 + 0.8 * state.Competence("sell")
                + 3.0 * salesHeads + mkBudget / 400.0 + bSales / 600.0) * capScale;
            adds = Gd.Minf(adds, gtmCap);
            double residence = th.LifetimeWk * (0.4 + state.Product / 100.0 * 1.2);
            // customer care keeps people: churn eases toward -30% as care approaches ~$3k/wk
            double careMult = 1.0 - 0.30 * (1.0 - Math.Exp(-bCare / 1500.0));
            double churn = A / Gd.Maxf(residence, 2.0) * th.ChurnMult * statusChurn * careMult;
            // pricing pain lands on RETENTION, never on invisible spend-shrink
            churn *= OffersPricePain(state);
            // a market of 0.3 adds/wk is a REAL market: rounding erased
            // Enterprise forever — the seeded remainder keeps it (C5 D4)
            double netF = adds - churn;
            int net = (int)Math.Floor(Math.Abs(netF)) * (netF >= 0.0 ? 1 : -1);
            if (RngFor(state, 91).Randf() < Math.Abs(netF) - Math.Floor(Math.Abs(netF)))
                net += netF >= 0.0 ? 1 : -1;
            state.Traction = Gd.Maxi(state.Traction + net, 0);
            rep.Adds = Gd.RoundToInt(adds);
            rep.Churn = Gd.RoundToInt(churn);
            if (adds >= 1.0)
            {
                rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "+{0} customers (organic {1} · word of mouth {2})",
                    Gd.RoundToInt(adds), Gd.RoundToInt(pEff * P * priceDemand), Gd.RoundToInt(wom * priceDemand)));
            }
            if (churn >= 1.0)
            {
                rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "−{0} churned (lifetime {1} wks at v0.{2})",
                    Gd.RoundToInt(churn), Gd.RoundToInt(residence), state.Product));
            }

            // 9 ── money: revenue, burn, loan
            double arpuOff = OffersArpu(state);
            double revenue = 0.0;
            if (arpuOff >= 0.0)
            {
                revenue = state.Traction * arpuOff * statusArpu;
                if (arpuOff == 0.0 && state.Traction > 0)
                {
                    rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "NOTHING IS ON SALE — {0} customers, $0 revenue. Set prices in THE BINDER.", state.Traction));
                }
            }
            else
            {
                revenue = state.Traction * th.ArpuWk * state.PriceMult * statusArpu;
            }
            int payroll = 0;
            foreach (Employee e2 in state.Employees)
            {
                payroll += e2.Salary;
            }
            foreach (PipelineHire h2 in state.Pipeline)
            {
                payroll += h2.Salary;          // paid before productive
            }
            int rent;
            if (!GameState.ERA_RENT.TryGetValue(state.Era, out rent))
            {
                rent = 150;
            }
            int infra = 50 + Gd.ToInt(state.Traction * 0.05);
            // R&D: a real budget ships real product — +1 quality per ~$1200/wk (seeded
            // remainder), and it pays down tech debt as it goes
            if (bRnd > 0.0)
            {
                double qualityGain = bRnd / 1200.0;
                int whole = (int)Math.Floor(qualityGain);
                if (RngFor(state, 77).Randf() < qualityGain - whole)
                {
                    whole += 1;
                }
                if (whole > 0)
                {
                    state.Product = Gd.Mini(state.Product + whole, 100);
                    rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "R&D shipped: product v0.{0}", state.Product));
                }
                state.TechDebt = Gd.Maxf(state.TechDebt - bRnd / 1500.0, 0.0);
            }
            double cogs = 0.0;
            if (arpuOff >= 0.0)
            {
                cogs = state.Traction * OffersCogsPerCustomer(state);
                if (cogs >= 1.0)
                    rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "cost of serving customers: ${0}", Gd.RoundToInt(cogs)));
            }
            int burn = Gd.ToInt(((double)(rent + payroll + infra) + mkBudget + bSales + bCare + bRnd) * th.BurnMult + cogs);
            state.Cash += Gd.RoundToInt(revenue) - burn;
            if (state.GetMetaF("prev_revenue", 0.0) > 1.0)
            {
                double prev = state.GetMetaF("prev_revenue", 0.0);
                state.LastGrowth = Gd.Clampf((revenue - prev) / prev, -0.5, 0.5);
            }
            state.SetMeta("prev_revenue", revenue);
            rep.Revenue = Gd.RoundToInt(revenue);
            rep.Burn = burn;
            string leverTxt = "";
            if (bSales + bCare + bRnd > 0.0)
            {
                leverTxt = string.Format(CultureInfo.InvariantCulture, " · sales {0} · care {1} · rnd {2}",
                    Gd.ToInt(bSales), Gd.ToInt(bCare), Gd.ToInt(bRnd));
            }
            rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                "${0} in · ${1} out (rent {2} · payroll {3} · infra {4} · marketing {5}{6})",
                Gd.RoundToInt(revenue), burn, rent, payroll, infra, Gd.ToInt(mkBudget), leverTxt));
            // ── UNIT ECONOMICS, computed honestly every week (the simulator SHOWS its
            // math): CAC from what acquisition actually cost / who actually arrived;
            // LTV from residence x margin-per-week; payback in weeks.
            double arpuReal = OffersArpu(state);
            double arpu = (arpuReal >= 0.0 ? arpuReal : th.ArpuWk * state.PriceMult) * statusArpu;
            double newAdds = Gd.Maxf(adds, 0.0);
            rep.Cac = (newAdds >= 0.5 && (bMk + bSales) > 0.0) ? Gd.RoundToInt((bMk + bSales) / newAdds) : 0;
            rep.Ltv = Gd.RoundToInt(residence * arpu);
            rep.PaybackWk = rep.Cac > 0 ? (int)Math.Ceiling(rep.Cac / Gd.Maxf(arpu, 0.01)) : 0;
            state.SetMeta("unit_econ", new Dictionary<string, object>
            {
                { "arpu", arpu }, { "cac", rep.Cac }, { "ltv", rep.Ltv },
                { "payback_wk", rep.PaybackWk }, { "residence", Gd.ToInt(residence) },
            });
            if (state.LoanPrincipal > 0)
            {
                int interest = (int)Math.Ceiling(state.LoanPrincipal * 0.18);
                state.LoanPrincipal += interest;
                rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "the loan compounds: +${0} interest (owe ${1})", interest, state.LoanPrincipal));
                if (state.Cash > 2000)
                {
                    int pay = Gd.Mini(state.Cash - 1500, state.LoanPrincipal);
                    state.Cash -= pay;
                    state.LoanPrincipal -= pay;
                    rep.Lines.Add(string.Format(CultureInfo.InvariantCulture, "auto-repaid ${0} of the loan", pay));
                }
            }

            // 9b ── the founder's working assumptions converge toward the truth.
            // Rate: analytics tooling, real customers, and R&D all teach.
            if (state.Beliefs == null)
            {
                SeedBeliefs(state);
            }
            else
            {
                double k = Gd.Clampf(0.02 + 0.05 * state.AnalyticsLevel
                    + 0.003 * state.Traction + bRnd / 40000.0, 0.0, 0.30);
                state.Beliefs.Tam = state.Beliefs.Tam + (th.Tam - state.Beliefs.Tam) * k;
                state.Beliefs.LifetimeWk = state.Beliefs.LifetimeWk
                    + (th.LifetimeWk - state.Beliefs.LifetimeWk) * k;
            }

            // 10 ── commitments (recurring deltas with duration)
            var keptComm = new List<Commitment>();
            foreach (Commitment cmd in state.Commitments)
            {
                state.Cash += cmd.CashWk;
                cmd.WeeksLeft = cmd.WeeksLeft - 1;
                rep.Lines.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1}${2}",
                    cmd.Name ?? "commitment", cmd.CashWk >= 0 ? "+" : "−", Gd.Absi(cmd.CashWk)));
                if (cmd.WeeksLeft > 0)
                {
                    keptComm.Add(cmd);
                }
                else
                {
                    rep.Expired.Add(cmd.Name ?? "");
                }
            }
            state.Commitments = keptComm;

            // the binder's memory: one snapshot per week, capped
            state.MetricHistory.Add(new MetricSnapshot
            {
                Wk = state.Week, Cash = state.Cash,
                Customers = state.Traction, Revenue = rep.Revenue,
                Burn = rep.Burn, Morale = state.Morale,
                Debt = Gd.ToInt(state.TechDebt), Hype = state.Hype,
            });
            if (state.MetricHistory.Count > 90)
            {
                state.MetricHistory = state.MetricHistory.GetRange(
                    state.MetricHistory.Count - 90, 90);
            }
            state.ClampiMeters();
            return rep;
        }

        // ─────────────────────── status / clock helpers ──────────────────────────
        public static bool AddStatus(GameState state, string name, int weeks)
        {
            if (!STATUS.ContainsKey(name ?? ""))
            {
                return false;
            }
            foreach (Status s in state.Statuses)
            {
                if (s.Name == name)
                {
                    s.WeeksLeft = Gd.Maxi(s.WeeksLeft, weeks);
                    return true;
                }
            }
            state.Statuses.Add(new Status { Name = name, WeeksLeft = Gd.Maxi(weeks, 1) });
            return true;
        }

        public static bool HasStatus(GameState state, string name)
        {
            foreach (Status s in state.Statuses)
            {
                if (s.Name == name)
                {
                    return true;
                }
            }
            return false;
        }

        public static void AddClock(GameState state, int weeks, string consequence)
        {
            state.Clocks.Add(new Clock
            {
                WeeksLeft = Gd.Maxi(weeks, 1),
                Consequence = Gd.Left(consequence, 120),
            });
        }

        // ───────────────────── the D&D resolution layer ──────────────────────────
        public static readonly Dictionary<string, int> DC_FLOORS = new Dictionary<string, int>
        {
            { "routine", 6 }, { "solid", 9 }, { "bold", 12 }, { "wild", 15 }
        };

        // ─────────────────────────── THE SIX TRAITS ──────────────────────────────
        /// <summary>
        /// Competences are rolled; TRAITS are never rolled. They are who the founder
        /// is, and they bend the dice and the terms from behind. The numbers live
        /// HERE, once, in the same table as the words that explain them, so a rule
        /// can never drift from its own description. Thresholds:
        ///   charisma   4+  advantage on sell and recruit
        ///   focus      4+  advantage on build
        ///   cred+net   8+  advantage on raise, and warmer term sheets
        ///   luck       4+  a natural 1 is rerolled once  ·  1  a natural 20 is only 19
        ///   stamina    2-  disadvantage on grit once exhaustion bites
        /// </summary>
        public static readonly Dictionary<string, string> TRAIT_RULES = new Dictionary<string, string>
        {
            { "charisma", "People say yes to you. At 4+ you roll SELL and RECRUIT with advantage: two dice, keep the best." },
            { "luck", "The dice bend. At 4+ a natural 1 is rerolled once. At 1 a natural 20 only ever counts as 19." },
            { "network", "Counted together with CREDIBILITY. At 8+ combined the investor doors open: advantage on RAISE." },
            { "focus", "Deep work. At 4+ you roll BUILD with advantage: two dice, keep the best." },
            { "credibility", "Counted with NETWORK. At 8+ combined you raise with advantage, and offers ask up to 8% less equity." },
            { "stamina", "Reserves. At 2 or less, GRIT rolls go to disadvantage as soon as exhaustion reaches 3." },
        };

        /// <summary>Which trait rules are ON for this founder right now, in the words the screens print.</summary>
        public static List<string> TraitEffects(GameState state)
        {
            var outp = new List<string>();
            int doors = state.TraitLevel("credibility") + state.TraitLevel("network");
            if (doors >= 8)
            {
                outp.Add(string.Format(CultureInfo.InvariantCulture,
                    "doors open (cred+net {0}): advantage on RAISE", doors));
            }
            else if (doors == 7)
            {
                outp.Add("one point from open doors (cred+net 7)");
            }
            if (state.TraitLevel("charisma") >= 4)
            {
                outp.Add("people say yes: advantage on SELL + RECRUIT");
            }
            if (state.TraitLevel("focus") >= 4)
            {
                outp.Add("deep work: advantage on BUILD");
            }
            if (state.TraitLevel("luck") >= 4)
            {
                outp.Add("luck rerolls a natural 1");
            }
            if (state.TraitLevel("luck") <= 1)
            {
                outp.Add("a natural 20 only counts as 19");
            }
            if (state.TraitLevel("stamina") <= 2)
            {
                outp.Add("no reserves: disadvantage on GRIT when tired");
            }
            double warm = WarmthPct(state);
            if (warm > 0.0)
            {
                outp.Add("offers ask " + Gd.F(warm, 0) + "% less equity");
            }
            return outp;
        }

        /// <summary>
        /// Advantage/disadvantage from STATE — items, hires, statuses, exhaustion,
        /// and the six traits the founder never rolls.
        /// </summary>
        public static Runway.Core.RollContext RollContext(GameState state, string stat)
        {
            var adv = new List<string>();
            var dis = new List<string>();
            foreach (Status s in state.Statuses)
            {
                StatusDef eff = StatusEffect(s.Name);
                if (eff.Adv == stat)
                {
                    adv.Add(s.Name ?? "");
                }
                if (eff.Dis == stat)
                {
                    dis.Add(s.Name ?? "");
                }
            }
            if (state.Exhaustion >= 3 && stat == "grit")
            {
                dis.Add(string.Format(CultureInfo.InvariantCulture, "exhaustion {0}", state.Exhaustion));
            }
            if (state.TechDebt > 70.0 && stat == "build")
            {
                dis.Add(string.Format(CultureInfo.InvariantCulture, "tech debt {0}", Gd.ToInt(state.TechDebt)));
            }
            foreach (Employee e in state.Employees)
            {
                string role = e.Role ?? "";
                if (role.Contains("sales") && stat == "sell" && !adv.Contains("sales team"))
                {
                    adv.Add("sales team");
                }
            }
            // WHO YOU ARE, at the table. Same reasons the card promised, word for word.
            if (stat == "raise")
            {
                int doors = state.TraitLevel("credibility") + state.TraitLevel("network");
                if (doors >= 8)
                {
                    adv.Add(string.Format(CultureInfo.InvariantCulture,
                        "doors open (credibility+network {0})", doors));
                }
            }
            if ((stat == "sell" || stat == "recruit") && state.TraitLevel("charisma") >= 4)
            {
                adv.Add("people say yes to you");
            }
            if (stat == "build" && state.TraitLevel("focus") >= 4)
            {
                adv.Add("deep work");
            }
            if (stat == "grit" && state.TraitLevel("stamina") <= 2 && state.Exhaustion >= 3)
            {
                dis.Add("no reserves");
            }
            bool hasA = adv.Count > 0;
            bool hasD = dis.Count > 0;
            return new RollContext
            {
                Advantage = hasA && !hasD,
                Disadvantage = hasD && !hasA,
                AdvReasons = adv,
                DisReasons = dis,
            };
        }

        /// <summary>
        /// The full roll: 1d20, or 2d20 keep best/worst under advantage/disadvantage,
        /// and then LUCK, which only ever touches the two extremes and says so out
        /// loud. Every die comes out of the caller's roller, so a run replays exactly.
        /// </summary>
        public static Runway.Core.RollContext RollD20Ctx(GameState state, string stat, Func<int> rngRoll)
        {
            Runway.Core.RollContext ctx = RollContext(state, stat);
            int a = rngRoll();
            int b = rngRoll();
            int used = a;
            if (ctx.Advantage)
            {
                used = Gd.Maxi(a, b);
            }
            else if (ctx.Disadvantage)
            {
                used = Gd.Mini(a, b);
            }
            ctx.Rolls = (ctx.Advantage || ctx.Disadvantage)
                ? new List<int> { a, b }
                : new List<int> { a };
            // THE LUCKY ARE SPARED THE 1; THE UNLUCKY NEVER GET THE 20.
            int luck = state.TraitLevel("luck");
            string note = "";
            if (used == 1 && luck >= 4)
            {
                used = rngRoll();
                note = "luck rerolls the 1";
            }
            else if (used == 20 && luck <= 1)
            {
                used = 19;
                note = "never quite perfect";
            }
            ctx.A = a;
            ctx.B = b;
            ctx.LuckNote = note;
            ctx.D20 = used;
            ctx.Mod = state.Competence(stat) - 3;
            ctx.Total = used + ctx.Mod;
            return ctx;
        }

        /// <summary>total - dc, banded. The band FORCES the narration frame.</summary>
        public static string MarginBand(int total, int dc)
        {
            int m = total - dc;
            if (m >= 5) { return "brilliant"; }
            if (m >= 0) { return "fine"; }
            if (m >= -2) { return "risky"; }
            return "backfired";
        }

        // ───────────────────────── the funding module ────────────────────────────
        public static int Valuation(GameState state)
        {
            double arpuWk = state.Theta != null ? state.Theta.ArpuWk : 4.0;
            double fundingMult = state.Theta != null ? state.Theta.FundingMult : 1.0;
            double arpuV = OffersArpu(state);
            if (arpuV < 0.0) arpuV = arpuWk * state.PriceMult;
            double arr = state.Traction * arpuV * 52.0;
            double growth = Gd.Clampf(state.LastGrowth, 0.0, 0.4);
            double mult = 8.0 + Gd.Minf(12.0, growth * 60.0);
            return Gd.Maxi(state.Cash, Gd.ToInt(arr * mult * fundingMult));
        }

        /// <summary>
        /// HOW WARM THE ROOM IS, in percent off the equity asked. Credibility and the
        /// phone book are read together: every point over 6 combined is worth about
        /// 2% less dilution, capped at 8%. The same company raises on better terms
        /// because of who is asking.
        /// </summary>
        public static double WarmthPct(GameState state)
        {
            int doors = state.TraitLevel("credibility") + state.TraitLevel("network");
            return Gd.Minf(2.0 * Gd.Maxi(doors - 6, 0), 8.0);
        }

        /// <summary>
        /// Three offers against fair price; desperation prices against you, standing
        /// in the room warms them back.
        /// </summary>
        public static List<FundingOffer> GenerateOffers(GameState state, List<Investor> investors)
        {
            int pre = Valuation(state);
            Rng r = RngFor(state, 9);
            bool desperate = state.Cash < 0 || RunwayWeeks(state) <= 4;
            double warm = WarmthPct(state);
            var outp = new List<FundingOffer>();
            int invCount = investors != null ? investors.Count : 0;
            for (int i = 0; i < 3; i++)
            {
                Investor inv = invCount > 0 ? investors[i % Gd.Maxi(invCount, 1)] : null;
                int amount = Gd.ToInt(pre * r.RandfRange(0.05, 0.15));
                double fair = (double)amount / (pre + amount) * 100.0;
                double spread = r.RandfRange(1.15, 1.6) * (desperate ? 1.35 : 1.0) * (1.0 - warm / 100.0);
                outp.Add(new FundingOffer
                {
                    Investor = inv != null ? (inv.Name ?? "?") : "an angel",
                    Amount = Gd.Maxi(amount, 5000),
                    EquityPct = Gd.Snappedf(Gd.Clampf(fair * spread, 1.0, 45.0), 0.1),
                    FairPct = Gd.Snappedf(fair, 0.1),
                    Warmth = Gd.Snappedf(warm, 0.1),
                    Thesis = inv != null ? (inv.Thesis ?? "") : "",
                });
            }
            return outp;
        }

        public static void ApplyRound(GameState state, int amount, double equityPct)
        {
            state.Cash += amount;
            double keep = 1.0 - equityPct / 100.0;
            state.FounderPct = Gd.Maxf(state.FounderPct * keep, 1.0);
            foreach (Cofounder cf in state.Cofounders)
            {
                cf.EquityDiluted = (cf.EquityDiluted ?? cf.Equity) * keep;
            }
            string[] ladder = { "pre-seed", "seed", "series_a", "series_b", "series_c", "growth" };
            state.RoundsRaised.Add(ladder[Gd.Mini(state.RoundsRaised.Count, ladder.Length - 1)]);
            state.Morale = Gd.Clampi(state.Morale + 5, 0, 100);
        }

        // ───────────────────────── derived signals ───────────────────────────────
        /// <summary>
        /// THE DEMAND CURVE (nobody EVER buys a $500 massage): how much of fair
        /// demand survives at this price. (p/fair)^-elasticity, clamped so a
        /// giveaway can at most triple demand and an absurd price sells ~nothing.
        /// </summary>
        public static double OfferDemand(Offer offer, double price)
        {
            double fair = Gd.Maxf(offer.FairPrice, 0.01);
            if (price <= 0.0)
            {
                return 0.0;   // not on sale
            }
            double e = offer.Elasticity;
            return Gd.Clampf(Math.Pow(price / fair, -e), 0.0, 2.0);
        }

        /// <summary>
        /// Weekly revenue per customer across PRICED offers (0 when nothing is on
        /// sale — an unpriced product earns nothing, however many sign up).
        /// Returns -1 for legacy runs with no offers at all: fall back to theta arpu.
        /// </summary>
        public static double OffersArpu(GameState state)
        {
            if (state.Offers == null || state.Offers.Count == 0)
            {
                return -1.0;
            }
            double total = 0.0;
            foreach (Offer od in state.Offers)
            {
                double price = od.Price;
                if (price <= 0.0)
                {
                    continue;
                }
                // THE OWNER'S LAW (#196): existing customers simply pay their
                // offer's price at its cadence. Demand gates ACQUISITION and
                // pushes CHURN above fair; it never taxes spend invisibly.
                total += od.Weight * price * OfferCadence(od.Unit);
            }
            return total;
        }

        /// <summary>Purchases per week for one customer of this offer — the honest
        /// bridge between "customers x price" and a weekly ledger line.</summary>
        public static double OfferCadence(string unit)
        {
            string u = (unit ?? "").ToLowerInvariant();
            if (u.Contains("session") || u.Contains("order") || u.Contains("hour")) return 1.0;
            if (u.Contains("month") || u.Contains("plan")) return 0.25;
            if (u.Contains("year")) return 0.02;
            if (u.Contains("package") || u.Contains("kit") || u.Contains("unit") || u.Contains("device")) return 0.2;
            return 0.5;
        }

        /// <summary>The weekly cost of serving one customer's purchases — a VISIBLE
        /// cogs line inside burn, never a silent subtraction from revenue.</summary>
        public static double OffersCogsPerCustomer(GameState state)
        {
            if (state.Offers == null || state.Offers.Count == 0) return 0.0;
            double total = 0.0;
            foreach (Offer od in state.Offers)
            {
                if (od.Price <= 0.0) continue;
                total += od.Weight * od.UnitCost * OfferCadence(od.Unit);
            }
            return total;
        }

        /// <summary>Above fair price the invoice reminds people to leave: 1.0 at or
        /// below fair, +0.4 per 100% over fair, capped at 1.6.</summary>
        public static double OffersPricePain(GameState state)
        {
            if (state.Offers == null || state.Offers.Count == 0) return 1.0;
            double num = 0.0, den = 0.0;
            foreach (Offer od in state.Offers)
            {
                if (od.Price <= 0.0) continue;
                double fair = Gd.Maxf(od.FairPrice > 0.0 ? od.FairPrice : od.Price, 1.0);
                num += od.Weight * (od.Price / fair);
                den += od.Weight;
            }
            if (den <= 0.0) return 1.0;
            double ratio = num / den;
            if (ratio <= 1.0) return 1.0;
            return 1.0 + Gd.Minf((ratio - 1.0) * 0.4, 0.6);
        }

        /// <summary>The blended price-demand multiplier adoption feels (1.0 at fair prices).</summary>
        public static double OffersDemandMult(GameState state)
        {
            if (state.Offers == null || state.Offers.Count == 0)
            {
                return -1.0;
            }
            double num = 0.0;
            double den = 0.0;
            foreach (Offer od in state.Offers)
            {
                double wgt = od.Weight;
                den += wgt;
                double price = od.Price;
                num += wgt * (price > 0.0 ? OfferDemand(od, price) : 0.0);
            }
            return den > 0.0 ? Gd.Clampf(num / Gd.Maxf(den, 0.01), 0.0, 3.0) : 0.0;
        }

        /// <summary>First guesses about the market — wrong on purpose, corrected by playing.</summary>
        public static void SeedBeliefs(GameState state)
        {
            Theta th = state.Theta;
            Rng br = RngFor(state, 88);
            state.Beliefs = new Beliefs
            {
                Tam = (th != null ? th.Tam : 100000.0) * br.RandfRange(0.35, 2.6),
                LifetimeWk = (th != null ? th.LifetimeWk : 40.0) * br.RandfRange(0.4, 2.2),
            };
        }

        /// <summary>
        /// What one week may plausibly spend at this stage — the DM's inputs are
        /// clamped here so no narration can invent hq money in a garage.
        /// </summary>
        public static int EraSpendCap(string era)
        {
            switch (era)
            {
                case "garage": return 6000;
                case "coworking": return 25000;
                case "office": return 80000;
                case "floor": return 300000;
                case "hq": return 1200000;
                default: return 6000;
            }
        }

        public static int RunwayWeeks(GameState state)
        {
            double arpuWk = state.Theta != null ? state.Theta.ArpuWk : 4.0;
            double arpuR = OffersArpu(state);
            if (arpuR < 0.0) arpuR = arpuWk * state.PriceMult;
            double revenue = state.Traction * arpuR;
            int payroll = 0;
            foreach (Employee e in state.Employees)
            {
                payroll += e.Salary;
            }
            int leverSum = state.Budgets.Sum();
            int rent;
            if (!GameState.ERA_RENT.TryGetValue(state.Era, out rent))
            {
                rent = 150;
            }
            double burn = (double)(rent + payroll + 50)
                + (double)(state.MarketingBudget + leverSum) - revenue;
            if (burn <= 0.0)
            {
                return 999;
            }
            return Gd.Maxi((int)Math.Floor(state.Cash / burn), 0);
        }

        public static string HealthBand(GameState state)
        {
            int rw = RunwayWeeks(state);
            if (state.Cash < 0)
            {
                return "CRITICAL — in the red";
            }
            if (rw <= 4)
            {
                return string.Format(CultureInfo.InvariantCulture, "CRITICAL — {0} weeks", rw);
            }
            if (rw <= 10)
            {
                return string.Format(CultureInfo.InvariantCulture, "WARNING — {0} weeks", rw);
            }
            return string.Format(CultureInfo.InvariantCulture, "STABLE — {0} weeks", Gd.Mini(rw, 260));
        }

        /// <summary>Everything the DM should know that a founder would feel. Fed every call.</summary>
        public static Dictionary<string, object> Signals(GameState state)
        {
            Theta th = state.Theta;
            double A = state.Traction;
            double N = th != null ? th.Tam : 100000.0;
            string phase = "pre-launch";
            if (A > 0.5 * N)
            {
                phase = "saturating";
            }
            else if (A > 0.1 * N)
            {
                phase = "scaling";
            }
            else if (A > 0.0)
            {
                phase = "early adopters";
            }
            var conds = new List<string>();
            foreach (Status s in state.Statuses)
            {
                conds.Add(string.Format(CultureInfo.InvariantCulture, "{0} ({1}wk)", s.Name, s.WeeksLeft));
            }
            var clocksOut = new List<string>();
            foreach (Clock c in state.Clocks)
            {
                clocksOut.Add(string.Format(CultureInfo.InvariantCulture, "in {0} wks: {1}", c.WeeksLeft, c.Consequence));
            }
            var rivals = new List<string>();
            foreach (Rival r in state.Rivals)
            {
                rivals.Add(string.Format(CultureInfo.InvariantCulture, "{0} ({1})", r.Name ?? "?", Fuzz(r.Strength)));
            }
            return new Dictionary<string, object>
            {
                { "health", HealthBand(state) },
                { "runway_weeks", RunwayWeeks(state) },
                { "market_phase", phase },
                { "market_penetration_pct", Gd.Snappedf(A / N * 100.0, 0.1) },
                { "market_mood", Gd.Snappedf(state.MarketTrend, 0.01) },
                { "price_mult", state.PriceMult },
                { "marketing_weekly", state.MarketingBudget },
                { "tech_debt", Gd.ToInt(state.TechDebt) },
                { "fatigue", Gd.ToInt(state.Fatigue) },
                { "exhaustion", state.Exhaustion },
                { "statuses", conds },
                { "clocks", clocksOut },
                { "loan_owed", state.LoanPrincipal },
                { "valuation", Valuation(state) },
                { "rivals", rivals },
            };
        }

        public static string Fuzz(double strength)
        {
            if (strength >= 70.0) { return "dominant"; }
            if (strength >= 45.0) { return "strong"; }
            if (strength >= 25.0) { return "scrappy"; }
            return "struggling";
        }
    }
}
