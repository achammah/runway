using System;
using System.Collections.Generic;
using System.Globalization;

namespace Runway.Core
{
    /// <summary>
    /// LANE — THE OWNERSHIP CLUSTER (ESOP, instruments, the raise, recruitment,
    /// buyout offers). Spec: docs/design/DECISIONS.md (THE OWNERSHIP CLUSTER,
    /// THE ESOP THREAD, THE OFFER) + docs/design/DAG2.md. W2 L-OWN, filled.
    ///
    /// THE MATH THIS MODULE PINS (the GDScript twin's header carries the full
    /// derivations; the two files hold the same formulas in the same order):
    ///
    /// VESTING — 208-wk linear, 52-wk cliff, computed never stored. A leaver
    /// keeps vested (the grant row's pct is cut to the vested figure and its
    /// emp_id gains the "left:" marker — no new save keys), unvested returns
    /// to the pool's free space.
    ///
    /// CONVERSION (post-money SAFE style): at a priced round the WHOLE
    /// unconverted stack converts at pre:
    ///   amount_eff = amount (safe) | amount × (1 + rate × weeks_out) (note)
    ///   eff_val = min(cap when cap&gt;0, pre × (1 − discount) when discount&gt;0)
    ///   pct = clamp(100 × amount_eff / max(eff_val, 1), 0, 35)
    /// Existing holders scale by 1 − Σpct/100; then the priced investor rides
    /// the EXISTING seams (SimEngine.ApplyRound → SimBoard.OnRoundClosed) and
    /// prior instrument pcts take the same pool_keep × inv_keep.
    ///
    /// THE POOL MIRROR — Esop.PoolPct is the lane's source of truth; the
    /// legacy OptionPoolPct stays as a mirror (SimBoard's round-close writes
    /// it); TickPre absorbs any divergence INTO the esop, one-way, idempotent.
    ///
    /// THE RAISE — interest = clamp(8×era + 10×log10(1+traction) + hype/4
    ///   + max(growth,0)×30 + board(6+2×goodwill−2×strikes) + season(±) +
    ///   active(+4), 0, 100); knocks p = score/100 × 0.35 (SALT_OWN_INBOUND).
    /// The data room reads five binder pages; ≥3 healthy → terms
    /// (SALT_OWN_TERMS, engine bands × the cycle's shock multipliers). While
    /// active the founder-time tax is 0.30 and the raise_distraction status
    /// re-arms weekly (catalog entry = coordinator package; AddStatus refuses
    /// unknown names so the arming is a safe no-op until it lands).
    ///
    /// RECRUITMENT — roles/candidates/offers_out are the source of truth; the
    /// legacy open_roles/applicants migrate in every tick start. Arrivals:
    /// lam = clamp(advert/30,0,4) × (0.5 + attractiveness at band mid) ×
    /// 0.6-if-senior, binomial(8, lam/8). Acceptance:
    /// odds = clamp(60 + (cash/ask−1)×216×w_cash + options×20×w_opt + hype/10
    /// + (morale−50)/10, 5, 95); mercenary 1.0/1.0, missionary 0.5/2.0.
    ///
    /// BUYOUT — extends SimBoard's M&amp;A, never forks: the board's mna gets
    /// DRESSED into the structured buyout_offer (SALT_OWN_BUYOUT); when both
    /// are quiet this lane's own arrival (p = 0.02 + 0.0004×traction +
    /// 0.0006×hype + 0.01×era, cap 10%) writes BOTH records so the board's
    /// lapse/cooldown machinery owns the clock. The WATERFALL is pure: debts
    /// → prefs-or-convert (max of amount×prefs vs pct/100 × (price − debts))
    /// → the remainder pro-rata incl. VESTED ESOP (the unallocated pool is
    /// cancelled — it leaves the denominator).
    ///
    /// The spine calls TickPre (§9: migrations, leavers, the drag,
    /// recruitment), TickMoney (ONLY m.RecruitAds), TickPost (interest,
    /// knocks, terms, the buyout). SALTS: 120/121/122 + 150-153.
    ///
    /// TWIN LAW: this file and game/src/core/lanes/sim_ownership.gd carry the
    /// same logic in the same order (behavioural parity; the engines never
    /// share PRNG bytes).
    /// </summary>
    public static class SimOwnership
    {
        public const int VEST_WEEKS = 208;
        public const int CLIFF_WEEKS = 52;
        public const int RADAR_CAP = 5;
        public const double KNOCK_P_MAX = 0.35;
        public const double CONVERT_PCT_CAP = 35.0;
        public const int TERMS_EXPIRE_WKS = 3;
        public const int OFFER_OUT_WKS = 2;
        public const int CANDIDATE_PATIENCE = 5;
        public const double FOUNDER_TIME_TAX = 0.30;
        public const string RAISE_STATUS = "raise_distraction";
        public const double MERCENARY_P = 0.55;
        public static readonly string[] SENIOR_WORDS =
            { "lead", "senior", "head", "chief", "manager", "principal" };

        // ═════════════════════════ small hands ════════════════════════════

        public static string Money(int n)
        {
            string s = Gd.Absi(n).ToString(CultureInfo.InvariantCulture);
            string outp = "";
            while (s.Length > 3)
            {
                outp = "," + s.Substring(s.Length - 3) + outp;
                s = s.Substring(0, s.Length - 3);
            }
            return (n < 0 ? "-" : "") + s + outp;
        }

        public static string MoneyShort(int n)
        {
            double v = Gd.Absf(n);
            string sign = n < 0 ? "-" : "";
            if (v >= 1000000000.0) return sign + "$" + Gd.F(v / 1000000000.0, 1) + "B";
            if (v >= 1000000.0) return sign + "$" + Gd.F(v / 1000000.0, 1) + "M";
            if (v >= 1000.0) return sign + "$" + Gd.F(v / 1000.0, 0) + "k";
            return sign + "$" + ((int)v).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>THE GRANT KEY: "June Park" → "june_park".</summary>
        public static string EmpSlug(string name)
        {
            return (name ?? "").Trim().ToLowerInvariant().Replace(" ", "_");
        }

        static int Round10(double v) { return Gd.RoundToInt(v / 10.0) * 10; }

        // dictionary hands for the free-form rows (stages/candidates/buyout)
        static double D(Dictionary<string, object> d, string k, double dv)
        {
            object v;
            if (d != null && d.TryGetValue(k, out v) && v != null)
            {
                try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); }
                catch { return dv; }
            }
            return dv;
        }

        static int Di(Dictionary<string, object> d, string k, int dv)
        {
            return (int)Math.Round(D(d, k, dv));
        }

        static string Ds(Dictionary<string, object> d, string k, string dv)
        {
            object v;
            if (d != null && d.TryGetValue(k, out v) && v != null) return Convert.ToString(v, CultureInfo.InvariantCulture);
            return dv;
        }

        static bool Db(Dictionary<string, object> d, string k, bool dv)
        {
            object v;
            if (d != null && d.TryGetValue(k, out v) && v != null)
            {
                try { return Convert.ToBoolean(v, CultureInfo.InvariantCulture); }
                catch { return dv; }
            }
            return dv;
        }

        static List<string> Dl(Dictionary<string, object> d, string k)
        {
            object v;
            var outp = new List<string>();
            if (d != null && d.TryGetValue(k, out v) && v is System.Collections.IEnumerable en && !(v is string))
                foreach (object o in en) outp.Add(Convert.ToString(o, CultureInfo.InvariantCulture));
            return outp;
        }

        // ═════════════════════════ THE MIGRATIONS ═════════════════════════

        public static void MigrateOwnership(GameState state)
        {
            // 1 · ESOP: seed once from the legacy field, then absorb the mirror.
            if (state.Esop == null && state.OptionPoolPct > 0.0)
                state.Esop = new Esop { PoolPct = state.OptionPoolPct, Granted = new List<EsopGrant>() };
            if (state.Esop != null)
            {
                if (state.Esop.Granted == null) state.Esop.Granted = new List<EsopGrant>();
                // The mirror rule, both directions: an esop built without the
                // legacy field (fixtures, probes) mirrors OUT; a legacy
                // round-close write (the only other writer) is absorbed IN. A
                // live pool can never reach exactly zero through the shuffle,
                // so the zero test is unambiguous.
                if (state.OptionPoolPct <= 0.0001 && state.Esop.PoolPct > 0.0)
                    state.OptionPoolPct = state.Esop.PoolPct;
                else if (Gd.Absf(state.OptionPoolPct - state.Esop.PoolPct) > 0.0001)
                    state.Esop.PoolPct = state.OptionPoolPct;
            }
            // 2 · RECRUITMENT: the legacy labor market feeds in, drained.
            if (state.OpenRoles.Count > 0 || state.Applicants.Count > 0)
            {
                EnsureRecruitment(state);
                Recruitment rec = state.Recruitment;
                foreach (OpenRole row in state.OpenRoles)
                {
                    string seat = row.Role ?? "engineer";
                    int mk = SimLabor.MarketSalary(seat, state.Era);
                    // The legacy advert was a POSTED WAGE; the new lever is ad
                    // spend. Carry the founder's intent through: a loud advert
                    // stays loud (40 × ratio², clamped).
                    double ratio = Gd.Clampf(row.OfferedSalary / Gd.Maxf(mk, 1.0), 0.5, 2.0);
                    rec.Roles.Add(new Dictionary<string, object>
                    {
                        { "id", "role_" + EmpSlug(seat) + "_" + row.OpenedWeek.ToString(CultureInfo.InvariantCulture) },
                        { "seat", seat },
                        { "band_lo", Round10(mk * 0.85) },
                        { "band_hi", Round10(mk * 1.25) },
                        { "advert_wk", Gd.Clampi(Gd.RoundToInt(40.0 * ratio * ratio), 10, 200) },
                        { "opened_wk", row.OpenedWeek },
                    });
                }
                state.OpenRoles = new List<OpenRole>();
                Rng r151 = SimEngine.RngForSalt(state, SimEngine.SALT_RECRUIT_PROFILE);
                int n = 0;
                foreach (Applicant a in state.Applicants)
                {
                    string role = a.Role ?? "engineer";
                    rec.Candidates.Add(new Dictionary<string, object>
                    {
                        { "id", "cand_" + state.Week.ToString(CultureInfo.InvariantCulture) + "_m" + n.ToString(CultureInfo.InvariantCulture) },
                        { "role_id", RoleIdFor(state, role) },
                        { "name", a.Name ?? "someone" },
                        { "ask", a.Ask },
                        { "profile", r151.Randf() < MERCENARY_P ? "mercenary" : "missionary" },
                        { "skill", Gd.Clampi(a.Skill, 1, 5) },
                        { "stage", "applied" },
                        { "arrived_wk", a.AppliedWeek },
                    });
                    n += 1;
                }
                state.Applicants = new List<Applicant>();
            }
        }

        static void EnsureRecruitment(GameState state)
        {
            if (state.Recruitment == null)
                state.Recruitment = new Recruitment();
            if (state.Recruitment.Roles == null) state.Recruitment.Roles = new List<Dictionary<string, object>>();
            if (state.Recruitment.Candidates == null) state.Recruitment.Candidates = new List<Dictionary<string, object>>();
            if (state.Recruitment.OffersOut == null) state.Recruitment.OffersOut = new List<Dictionary<string, object>>();
        }

        static void EnsureRaise(GameState state)
        {
            if (state.RaiseState == null)
                state.RaiseState = new RaiseState();
            if (state.RaiseState.Stages == null)
                state.RaiseState.Stages = new List<Dictionary<string, object>>();
        }

        static string RoleIdFor(GameState state, string seat)
        {
            if (state.Recruitment == null) return "";
            string want = SimLabor.RoleRow(seat);
            foreach (var r in state.Recruitment.Roles)
                if (SimLabor.RoleRow(Ds(r, "seat", "")) == want)
                    return Ds(r, "id", "");
            return "";
        }

        // ═════════════════════════ THE ESOP THREAD ════════════════════════

        public static double VestFrac(int weeksIn)
        {
            if (weeksIn < CLIFF_WEEKS) return 0.0;
            return Gd.Mini(weeksIn, VEST_WEEKS) / (double)VEST_WEEKS;
        }

        /// <summary>THE TEAM DESK'S GETTER (L-MONEY reads this): vested points
        /// for emp_id by week wk. A leaver's frozen grant is fully theirs.</summary>
        public static double VestedPct(GameState state, string empId, int wk)
        {
            double total = 0.0;
            if (state.Esop == null) return 0.0;
            foreach (EsopGrant g in state.Esop.Granted)
            {
                if (g.EmpId == empId)
                    total += VestFrac(wk - g.VestStartWk) * g.Pct;
                else if (g.EmpId == "left:" + empId)
                    total += g.Pct;
            }
            return total;
        }

        public static double GrantedPct(GameState state, string empId)
        {
            double total = 0.0;
            if (state.Esop == null) return 0.0;
            foreach (EsopGrant g in state.Esop.Granted)
                if (g.EmpId == empId) total += g.Pct;
            return total;
        }

        public static double PoolFree(GameState state)
        {
            if (state.Esop == null) return 0.0;
            double granted = 0.0;
            foreach (EsopGrant g in state.Esop.Granted) granted += g.Pct;
            return Gd.Maxf(state.Esop.PoolPct - granted, 0.0);
        }

        public static string CreatePool(GameState state, double pct)
        {
            if (state.Esop == null)
                state.Esop = new Esop { PoolPct = 0.0, Granted = new List<EsopGrant>() };
            return ExpandPool(state, pct);
        }

        /// <summary>Expansion dilutes every existing holder pro-rata:
        /// keep = 1 − add/100; pool = pool×keep + add; the sum stays 100.</summary>
        public static string ExpandPool(GameState state, double addPct)
        {
            double add = Gd.Clampf(addPct, 0.0, 15.0);
            if (add <= 0.0 || state.Esop == null) return "";
            double keep = 1.0 - add / 100.0;
            state.FounderPct = Gd.Maxf(state.FounderPct * keep, 1.0);
            foreach (Cofounder cf in state.Cofounders)
                cf.EquityDiluted = (cf.EquityDiluted ?? cf.Equity) * keep;
            foreach (Instrument inst in state.Instruments)
                if (inst.Pct > 0.0) inst.Pct *= keep;
            state.Esop.PoolPct = Gd.Clampf(state.Esop.PoolPct * keep + add, 0.0, 100.0);
            state.OptionPoolPct = state.Esop.PoolPct;
            string line = string.Format(CultureInfo.InvariantCulture,
                "the pool grows +{0}% — every holder diluted ×{1} (the slice came out of everyone)",
                Gd.F(add, 1), Gd.F(keep, 3));
            state.LogAction(line);
            return line;
        }

        public static string GrantOptions(GameState state, string empName, double pct)
        {
            if (pct <= 0.0 || state.Esop == null) return "";
            if (PoolFree(state) + 0.0001 < pct) return "";
            state.Esop.Granted.Add(new EsopGrant { EmpId = EmpSlug(empName),
                Pct = Gd.Snappedf(pct, 0.01), VestStartWk = state.Week });
            return string.Format(CultureInfo.InvariantCulture,
                "{0} granted {1}% ({2}-wk vest, {3}-wk cliff)", empName, Gd.F(pct, 2),
                VEST_WEEKS, CLIFF_WEEKS);
        }

        static void CrystallizeLeavers(GameState state, WeeklyReport rep)
        {
            if (state.Esop == null) return;
            var present = new HashSet<string>();
            foreach (Employee e in state.Employees) present.Add(EmpSlug(e.Name));
            foreach (PipelineHire h in state.Pipeline) present.Add(EmpSlug(h.Name));
            foreach (EsopGrant g in state.Esop.Granted)
            {
                string gid = g.EmpId ?? "";
                if (gid.StartsWith("left:") || present.Contains(gid)) continue;
                double vested = VestFrac(state.Week - g.VestStartWk) * g.Pct;
                double returned = g.Pct - vested;
                g.Pct = Gd.Snappedf(vested, 0.001);
                g.EmpId = "left:" + gid;
                if (returned > 0.0005)
                    rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0} left — {1}% unvested returned to the pool, {2}% vested kept",
                        gid.Replace("_", " "), Gd.F(returned, 2), Gd.F(vested, 2)));
            }
        }

        // ═════════════════════════ INSTRUMENTS ════════════════════════════

        public static int AmountDue(Instrument inst, int wk)
        {
            double amt = inst.Amount;
            if (inst.Kind == "note" || inst.Kind == "bridge")
                amt *= 1.0 + inst.Rate * Gd.Maxi(wk - inst.SignedWk, 0);
            return Gd.RoundToInt(amt);
        }

        public static double ConvertPctAt(Instrument inst, double roundPre, int wk)
        {
            double amt = AmountDue(inst, wk);
            double eff = Gd.Maxf(roundPre, 1.0);
            if (inst.Discount > 0.0) eff = roundPre * (1.0 - inst.Discount);
            if (inst.Cap > 0) eff = Gd.Minf(eff, inst.Cap);
            return Gd.Clampf(100.0 * amt / Gd.Maxf(eff, 1.0), 0.0, CONVERT_PCT_CAP);
        }

        static bool Unconverted(Instrument inst)
        {
            return (inst.Kind == "safe" || inst.Kind == "note" || inst.Kind == "bridge")
                && inst.Pct <= 0.0;
        }

        public static double StackDilutionAt(GameState state, double roundPre)
        {
            double total = 0.0;
            foreach (Instrument inst in state.Instruments)
                if (Unconverted(inst)) total += ConvertPctAt(inst, roundPre, state.Week);
            return total;
        }

        public static List<Instrument> MaturedNotes(GameState state)
        {
            var outp = new List<Instrument>();
            foreach (Instrument inst in state.Instruments)
                if (Unconverted(inst) && inst.MaturityWk > 0 && state.Week >= inst.MaturityWk)
                    outp.Add(inst);
            return outp;
        }

        // ═════════════════════════ THE RAISE ═══════════════════════════════

        public static double InterestScoreCalc(GameState state)
        {
            double score = 8.0 * state.EraIndex();
            score += 10.0 * Math.Log10(1.0 + state.Traction);
            score += state.Hype / 4.0;
            score += Gd.Maxf(state.LastGrowth, 0.0) * 30.0;
            if (state.Board != null)
                score += 6.0 + 2.0 * state.Board.Goodwill - 2.0 * state.Board.Strikes;
            if (state.MacroSeason == "boom") score += 8.0;
            else if (state.MacroSeason == "winter") score -= 10.0;
            if (state.RaiseState != null && state.RaiseState.Active) score += 4.0;
            return Gd.Snappedf(Gd.Clampf(score, 0.0, 100.0), 0.1);
        }

        public static Dictionary<string, object> DataRoom(GameState state)
        {
            var doubts = new List<string>();
            int score = 0;
            if (state.LastGrowth > 0.0) score += 1; else doubts.Add("the growth page is flat");
            if (SimEngine.RunwayWeeks(state) >= 10) score += 1; else doubts.Add("the runway page is short");
            if (state.LastPnl != null && state.LastPnl.Net >= 0) score += 1; else doubts.Add("the margin page bleeds");
            if (state.Product >= 50) score += 1; else doubts.Add("the product page is thin");
            if (state.Traction >= 20) score += 1; else doubts.Add("the customer page is quiet");
            return new Dictionary<string, object> { { "score", score }, { "doubts", doubts } };
        }

        static Dictionary<string, object> StageByName(GameState state, string name)
        {
            if (state.RaiseState == null) return null;
            foreach (var st in state.RaiseState.Stages)
                if (Ds(st, "name", "") == name) return st;
            return null;
        }

        public static List<Dictionary<string, object>> StagesIn(GameState state, string stage)
        {
            var outp = new List<Dictionary<string, object>>();
            if (state.RaiseState == null) return outp;
            foreach (var st in state.RaiseState.Stages)
                if (Ds(st, "stage", "") == stage) outp.Add(st);
            return outp;
        }

        public static int NoShopUntil(GameState state)
        {
            int until = 0;
            if (state.RaiseState == null) return 0;
            foreach (var st in state.RaiseState.Stages)
                until = Gd.Maxi(until, Di(st, "no_shop_until", 0));
            return until;
        }

        public static List<string> OutboundTargets(GameState state)
        {
            var seen = new HashSet<string>();
            if (state.RaiseState != null)
                foreach (var st in state.RaiseState.Stages)
                    seen.Add(Ds(st, "name", ""));
            var outp = new List<string>();
            foreach (Investor inv in state.Investors)
                if (!string.IsNullOrEmpty(inv.Name) && !seen.Contains(inv.Name))
                    outp.Add(inv.Name);
            return outp;
        }

        static void RaiseWeekly(GameState state, WeeklyReport rep)
        {
            EnsureRaise(state);
            RaiseState rs = state.RaiseState;
            rs.InterestScore = InterestScoreCalc(state);
            rs.FounderTimeTax = rs.Active ? FOUNDER_TIME_TAX : 0.0;
            Rng r120 = SimEngine.RngForSalt(state, SimEngine.SALT_OWN_INBOUND);
            // 1 · the inbound knock — to traction, not to wishes
            if (StagesIn(state, "radar").Count < RADAR_CAP
                && r120.Randf() < rs.InterestScore / 100.0 * KNOCK_P_MAX)
            {
                string nm;
                List<string> pool = OutboundTargets(state);
                if (pool.Count > 0)
                {
                    nm = pool[(int)(r120.Randi() % (uint)pool.Count)];
                }
                else
                {
                    string[] made = { "Halden Ventures", "R. Osei", "Cormorant Capital",
                        "the fund that emailed twice", "Bright & Motte", "a syndicate off the board's list" };
                    nm = made[(int)(r120.Randi() % (uint)made.Length)];
                }
                if (StageByName(state, nm) == null)
                {
                    rs.Stages.Add(new Dictionary<string, object> { { "name", nm },
                        { "stage", "radar" }, { "inbound", true }, { "arrived_wk", state.Week } });
                    rep.Lines.Add(nm + " knocked — the growth got noticed (ON THE RADAR)");
                }
            }
            // 2 · conversations ripen (≥2 weeks in) — the data room decides
            Rng r121 = SimEngine.RngForSalt(state, SimEngine.SALT_OWN_TERMS);
            foreach (var sd in StagesIn(state, "conversations"))
            {
                if (state.Week - Di(sd, "asked_wk", state.Week) < 2) continue;
                Dictionary<string, object> room = DataRoom(state);
                if (Di(room, "score", 0) >= 3)
                {
                    sd["stage"] = "terms";
                    sd["terms"] = DraftTerms(state, sd, r121);
                    sd["doubt"] = "";
                    var t = (Dictionary<string, object>)sd["terms"];
                    rep.Events.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0} put TERMS ON THE TABLE — {1}, expires wk {2}",
                        Ds(sd, "name", "an investor"), TermsHeadline(t), Di(t, "expires_wk", 0)));
                }
                else
                {
                    var doubts = (List<string>)room["doubts"];
                    sd["doubt"] = doubts.Count > 0 ? doubts[0] : "";
                    if (state.Week - Di(sd, "asked_wk", state.Week) >= 6)
                    {
                        sd["stage"] = "passed";
                        rep.Lines.Add(Ds(sd, "name", "an investor") + " passed — "
                            + (Ds(sd, "doubt", "") != "" ? Ds(sd, "doubt", "") : "the numbers didn't hold"));
                    }
                }
            }
            // 3 · terms expire — walking away happens to you too
            foreach (var sd2 in StagesIn(state, "terms"))
            {
                var t = sd2.ContainsKey("terms") ? (Dictionary<string, object>)sd2["terms"] : null;
                if (t != null && Di(t, "expires_wk", int.MaxValue) < state.Week)
                {
                    sd2["stage"] = "passed";
                    rs.InterestScore = Gd.Maxf(rs.InterestScore - 5.0, 0.0);
                    rep.Lines.Add(Ds(sd2, "name", "an investor")
                        + " pulled their terms — sheets have shelf lives");
                }
            }
            // 4 · the passed pile stays small
            var kept = new List<Dictionary<string, object>>();
            foreach (var sd3 in rs.Stages)
            {
                if (Ds(sd3, "stage", "") == "passed" && state.Week - Di(sd3, "arrived_wk", 0) > 12)
                    continue;
                kept.Add(sd3);
            }
            rs.Stages = kept;
        }

        static Dictionary<string, object> DraftTerms(GameState state,
            Dictionary<string, object> entry, Rng r)
        {
            double val = SimEngine.Valuation(state);
            int era = state.EraIndex();
            double warm = SimEngine.WarmthPct(state);
            bool desperate = state.Cash < 0 || SimEngine.RunwayWeeks(state) <= 4;
            string kind = "priced";
            if (SimEngine.RunwayWeeks(state) < 6 && state.Instruments.Count > 0)
                kind = "bridge";
            else if (era <= 1)
                kind = r.Randf() < 0.65 ? "safe" : "note";
            var t = new Dictionary<string, object> { { "kind", kind },
                { "expires_wk", state.Week + TERMS_EXPIRE_WKS } };
            switch (kind)
            {
                case "safe":
                case "note":
                    t["amount"] = Gd.Maxi((int)(val * r.RandfRange(0.08, 0.18) * SimEngine.ShockAmtMult(state)), 25000);
                    t["cap"] = (int)(val * r.RandfRange(1.2, 2.0) * SimEngine.ShockValMult(state));
                    t["discount"] = Gd.Snappedf(r.RandfRange(0.15, 0.25), 0.01);
                    if (kind == "note")
                    {
                        t["rate"] = Gd.Snappedf(r.RandfRange(0.002, 0.004), 0.0005);
                        t["maturity_wk"] = state.Week + 52;
                    }
                    break;
                case "bridge":
                    t["amount"] = Gd.Maxi((int)(val * r.RandfRange(0.05, 0.10)), 15000);
                    t["rate"] = Gd.Snappedf(r.RandfRange(0.003, 0.005), 0.0005);
                    t["maturity_wk"] = state.Week + 26;
                    t["discount"] = 0.2;
                    t["cap"] = (int)(val * 1.2);
                    break;
                default:
                    double pre = val * r.RandfRange(0.9, 1.3) * SimEngine.ShockValMult(state);
                    pre *= 1.0 + warm / 100.0 * 0.5;
                    if (desperate) pre *= 0.8;
                    t["valuation"] = (int)pre;
                    t["amount"] = Gd.Maxi((int)(pre * r.RandfRange(0.15, 0.25) * SimEngine.ShockAmtMult(state)), 50000);
                    t["pct"] = Gd.Snappedf(100.0 * Di(t, "amount", 0) / Gd.Maxf(pre + Di(t, "amount", 0), 1.0), 0.1);
                    t["prefs"] = 1.0;
                    t["participating"] = r.Randf() < 0.10;
                    t["protective"] = true;
                    t["drag_threshold"] = 60.0;
                    t["board_seat"] = true;
                    t["no_shop_wks"] = 4;
                    t["pool_topup_pct"] = SimBoard.PoolAskPct(state);
                    break;
            }
            return t;
        }

        public static string TermsHeadline(Dictionary<string, object> t)
        {
            switch (Ds(t, "kind", ""))
            {
                case "safe":
                    return "SAFE $" + MoneyShort(Di(t, "amount", 0)).TrimStart('$')
                        + " · cap " + MoneyShort(Di(t, "cap", 0));
                case "note":
                    return "note $" + MoneyShort(Di(t, "amount", 0)).TrimStart('$')
                        + " · matures wk " + Di(t, "maturity_wk", 0).ToString(CultureInfo.InvariantCulture);
                case "bridge":
                    return "bridge $" + MoneyShort(Di(t, "amount", 0)).TrimStart('$') + " from the insiders";
            }
            return "priced: " + MoneyShort(Di(t, "amount", 0)) + " at "
                + MoneyShort(Di(t, "valuation", 0)) + " pre";
        }

        public static string OpPitchInvestor(GameState state, string name = "")
        {
            EnsureRaise(state);
            RaiseState rs = state.RaiseState;
            Dictionary<string, object> target = null;
            if (!string.IsNullOrEmpty(name))
                foreach (var st in rs.Stages)
                    if (Ds(st, "name", "").ToLowerInvariant().Contains(name.ToLowerInvariant()))
                    { target = st; break; }
            if (target == null)
                foreach (var st2 in StagesIn(state, "radar")) { target = st2; break; }
            if (target == null)
            {
                string nm = name ?? "";
                if (nm == "")
                {
                    List<string> pool = OutboundTargets(state);
                    if (pool.Count == 0) return "";
                    nm = pool[0];
                }
                target = new Dictionary<string, object> { { "name", nm }, { "stage", "radar" },
                    { "inbound", false }, { "arrived_wk", state.Week } };
                rs.Stages.Add(target);
            }
            target["stage"] = "conversations";
            target["asked_wk"] = state.Week;
            rs.Active = true;
            rs.FounderTimeTax = FOUNDER_TIME_TAX;
            state.Fatigue = Gd.Minf(state.Fatigue + 6.0, 100.0);
            string line = "pitched " + Ds(target, "name", "an investor")
                + " — they asked for real numbers (the data room reads YOUR binder)";
            state.LogAction(line);
            return line;
        }

        public static string OpSignInstrument(GameState state, string name = "")
        {
            EnsureRaise(state);
            List<Dictionary<string, object>> entries = StagesIn(state, "terms");
            if (entries.Count == 0) return "";
            Dictionary<string, object> entry = null;
            if (!string.IsNullOrEmpty(name))
                foreach (var st in entries)
                    if (Ds(st, "name", "").ToLowerInvariant().Contains(name.ToLowerInvariant()))
                    { entry = st; break; }
            if (entry == null) entry = entries[0];
            if (NoShopUntil(state) > state.Week) return "";
            var t = entry.ContainsKey("terms") ? (Dictionary<string, object>)entry["terms"]
                : new Dictionary<string, object>();
            string holder = Ds(entry, "name", "an investor");
            string kind = Ds(t, "kind", "safe");
            int amount = Di(t, "amount", 0);
            string line;
            if (kind == "priced")
            {
                line = SignPriced(state, holder, t);
            }
            else
            {
                state.Cash += amount;   // ONE-SHOT EVENT CASH — never a weekly lane
                state.Instruments.Add(new Instrument { Kind = kind, Holder = holder,
                    Amount = amount, Cap = Di(t, "cap", 0), Discount = D(t, "discount", 0.0),
                    Rate = D(t, "rate", 0.0), MaturityWk = Di(t, "maturity_wk", 0),
                    Pct = 0.0, Prefs = 0.0, Protective = false, DragThreshold = 0.0,
                    SignedWk = state.Week });
                line = "signed " + holder + "'s " + kind + ": $" + Money(amount)
                    + " wired now — dilution deferred"
                    + (kind != "safe" ? ", matures wk " + Di(t, "maturity_wk", 0).ToString(CultureInfo.InvariantCulture) : "");
            }
            entry["stage"] = "wired";
            entry["wired_wk"] = state.Week;
            if (kind == "priced")
            {
                entry["no_shop_until"] = state.Week + Di(t, "no_shop_wks", 4);
                state.RaiseState.Active = false;
                state.RaiseState.FounderTimeTax = 0.0;
            }
            state.LogAction(line);
            return line;
        }

        static string SignPriced(GameState state, string holder, Dictionary<string, object> t)
        {
            double pre = Gd.Maxf(D(t, "valuation", SimEngine.Valuation(state)), 1.0);
            int amount = Di(t, "amount", 0);
            // 1 · the stack converts, whole, at the round's pre-money
            double convTotal = 0.0;
            var convNotes = new List<string>();
            foreach (Instrument inst in state.Instruments)
            {
                if (!Unconverted(inst)) continue;
                double pctI = ConvertPctAt(inst, pre, state.Week);
                inst.Pct = Gd.Snappedf(pctI, 0.01);
                convTotal += pctI;
                convNotes.Add(inst.Holder + " -> " + Gd.F(pctI, 1) + "%");
            }
            if (convTotal > 0.0)
            {
                double keep = 1.0 - convTotal / 100.0;
                state.FounderPct = Gd.Maxf(state.FounderPct * keep, 1.0);
                foreach (Cofounder cf in state.Cofounders)
                    cf.EquityDiluted = (cf.EquityDiluted ?? cf.Equity) * keep;
                if (state.Esop != null) state.Esop.PoolPct *= keep;
            }
            // 2 · mirror the pool out so the board's shuffle starts from truth
            if (state.Esop != null) state.OptionPoolPct = state.Esop.PoolPct;
            double poolKeep = 1.0 - Gd.Clampf(SimBoard.PoolAskPct(state), 0.0, 15.0) / 100.0;
            double invPct = 100.0 * amount / (pre + amount);
            double invKeep = 1.0 - invPct / 100.0;
            // 3+4 · the existing seams — never forked
            SimEngine.ApplyRound(state, amount, invPct);
            SimBoard.OnRoundClosed(state, amount, invPct);
            // 5 · the paper the seams don't know about scales by the same keeps
            foreach (Instrument inst3 in state.Instruments)
                if (inst3.Pct > 0.0)
                    inst3.Pct = Gd.Snappedf(inst3.Pct * poolKeep * invKeep, 0.01);
            // 6 · absorb the shuffled pool back into the source of truth
            if (state.OptionPoolPct > 0.0 && state.Esop == null)
                state.Esop = new Esop { PoolPct = state.OptionPoolPct, Granted = new List<EsopGrant>() };
            else if (state.Esop != null)
                state.Esop.PoolPct = state.OptionPoolPct;
            // 7 · the new preferred stock on the books. The investor's slice is
            // of the POST company — the pool was written PRE-money, out of the
            // founding side, so their pct never takes the shuffle.
            state.Instruments.Add(new Instrument { Kind = "priced", Holder = holder,
                Amount = amount, Cap = 0, Discount = 0.0, Rate = 0.0, MaturityWk = 0,
                Pct = Gd.Snappedf(invPct, 0.01), Prefs = 1.0,
                Protective = Db(t, "protective", true),
                DragThreshold = D(t, "drag_threshold", 60.0), SignedWk = state.Week });
            string last = state.RoundsRaised.Count > 0
                ? state.RoundsRaised[state.RoundsRaised.Count - 1] : "";
            if (last == "seed") state.SetFlag("seed_raised");
            else if (last == "series_a") state.SetFlag("series_a");
            string convTxt = convNotes.Count > 0
                ? " · the stack converted at once (" + string.Join(" · ", convNotes) + ")" : "";
            return string.Format(CultureInfo.InvariantCulture,
                "PRICED ROUND SIGNED — {0} wires ${1} at ${2} pre -> ≈{3}% preferred, board seat, covenant armed{4}",
                holder, Money(amount), Money((int)pre), Gd.F(invPct, 1), convTxt);
        }

        // ═════════════════════════ RECRUITMENT ════════════════════════════

        public static Dictionary<string, object> BandFor(GameState state, string seat)
        {
            int mk = SimLabor.MarketSalary(seat, state.Era);
            return new Dictionary<string, object> { { "lo", Round10(mk * 0.85) },
                { "hi", Round10(mk * 1.25) } };
        }

        static double SeniorMult(string seat)
        {
            string low = (seat ?? "").ToLowerInvariant();
            foreach (string w in SENIOR_WORDS)
                if (low.Contains(w)) return 0.6;
            return 1.0;
        }

        public static double ArrivalRateR(GameState state, Dictionary<string, object> role)
        {
            double advert = D(role, "advert_wk", 0);
            if (advert <= 0.0) return 0.0;
            string seat = Ds(role, "seat", "engineer");
            double bandMid = (D(role, "band_lo", 0) + D(role, "band_hi", 0)) * 0.5;
            double attract = SimLabor.Attractiveness(state, seat, (int)bandMid);
            return Gd.Clampf(advert / 30.0, 0.0, 4.0) * (0.5 + attract) * SeniorMult(seat);
        }

        public static double AcceptanceOdds(GameState state, Dictionary<string, object> cand,
            int cashWk, double optionsPct)
        {
            double ask = Gd.Maxf(D(cand, "ask", 1), 1.0);
            bool mercenary = Ds(cand, "profile", "mercenary") == "mercenary";
            double wCash = mercenary ? 1.0 : 0.5;
            double wOpt = mercenary ? 1.0 : 2.0;
            double odds = 60.0;
            odds += (cashWk / ask - 1.0) * 216.0 * wCash;
            odds += optionsPct * 20.0 * wOpt;
            odds += state.Hype / 10.0;
            odds += (state.Morale - 50.0) / 10.0;
            return Gd.Clampf(odds, 5.0, 95.0);
        }

        public static Dictionary<string, object> CandById(GameState state, string id)
        {
            if (state.Recruitment == null) return null;
            foreach (var c in state.Recruitment.Candidates)
                if (Ds(c, "id", "") == id) return c;
            return null;
        }

        static Dictionary<string, object> RoleById(GameState state, string id)
        {
            if (state.Recruitment == null) return null;
            foreach (var r in state.Recruitment.Roles)
                if (Ds(r, "id", "") == id) return r;
            return null;
        }

        public static string OpenSeat(GameState state, string seat)
        {
            EnsureRecruitment(state);
            if (!SimLabor.MarketOpen(state.Era) || !SimLabor.RoleUnlocked(seat, state.Era))
                return "";
            if (SimLabor.SeatsLeft(state) <= state.Recruitment.Roles.Count) return "";
            Dictionary<string, object> band = BandFor(state, seat);
            state.Recruitment.Roles.Add(new Dictionary<string, object>
            {
                { "id", "role_" + EmpSlug(seat) + "_" + state.Week.ToString(CultureInfo.InvariantCulture) },
                { "seat", seat }, { "band_lo", Di(band, "lo", 0) }, { "band_hi", Di(band, "hi", 0) },
                { "advert_wk", 40 }, { "opened_wk", state.Week },
            });
            return string.Format(CultureInfo.InvariantCulture,
                "SEAT OPEN: {0} (band ${1}–{2}/wk) — advert $40/wk", seat,
                Money(Di(band, "lo", 0)), Money(Di(band, "hi", 0)));
        }

        public static void CloseSeat(GameState state, string roleId)
        {
            if (state.Recruitment == null) return;
            for (int i = state.Recruitment.Roles.Count - 1; i >= 0; i--)
                if (Ds(state.Recruitment.Roles[i], "id", "") == roleId)
                    state.Recruitment.Roles.RemoveAt(i);
        }

        public static void SetAdvert(GameState state, string roleId, int advert)
        {
            Dictionary<string, object> role = RoleById(state, roleId);
            if (role != null) role["advert_wk"] = Gd.Clampi(advert, 0, 400);
        }

        public static string Interview(GameState state, string candId)
        {
            Dictionary<string, object> cand = CandById(state, candId);
            if (cand == null || Ds(cand, "stage", "") != "applied") return "";
            cand["stage"] = "interviewed";
            state.Fatigue = Gd.Minf(state.Fatigue + 4.0, 100.0);
            return "interviewed " + Ds(cand, "name", "?") + " — " + Ds(cand, "profile", "")
                + ", asks $" + Money(Di(cand, "ask", 0));
        }

        public static string OpSendOffer(GameState state, string candId, int cashWk, double optionsPct)
        {
            Dictionary<string, object> cand = CandById(state, candId);
            if (cand == null) return "";
            string stage = Ds(cand, "stage", "");
            if (stage != "applied" && stage != "interviewed") return "";
            if (SimLabor.SeatsLeft(state) <= 0) return "";
            if (optionsPct > 0.0 && PoolFree(state) + 0.0001 < optionsPct)
                return "";   // empty pool blocks equity offers
            Dictionary<string, object> role = RoleById(state, Ds(cand, "role_id", ""));
            string seatRole = role != null ? Ds(role, "seat", "engineer") : "engineer";
            int cash = SimLabor.ClampSalary(SimLabor.MarketSalary(seatRole, state.Era), cashWk);
            state.Recruitment.OffersOut.Add(new Dictionary<string, object>
            {
                { "candidate_id", Ds(cand, "id", "") }, { "cash_wk", cash },
                { "options_pct", Gd.Snappedf(optionsPct, 0.01) },
                { "expires_wk", state.Week + OFFER_OUT_WKS }, { "sent_wk", state.Week },
            });
            cand["stage"] = "offer";
            string line = string.Format(CultureInfo.InvariantCulture,
                "OFFER OUT to {0}: ${1}/wk{2} — odds ≈{3}%", Ds(cand, "name", "?"),
                Money(cash), optionsPct > 0.0 ? " + " + Gd.F(optionsPct, 1) + "% options" : "",
                Gd.RoundToInt(AcceptanceOdds(state, cand, cash, optionsPct)));
            state.LogAction(line);
            return line;
        }

        static void RecruitWeekly(GameState state, WeeklyReport rep)
        {
            if (state.Recruitment == null) return;
            EnsureRecruitment(state);
            Recruitment rec = state.Recruitment;
            Rng r152 = SimEngine.RngForSalt(state, SimEngine.SALT_RECRUIT_ACCEPT);
            Rng r153 = SimEngine.RngForSalt(state, SimEngine.SALT_RECRUIT_COUNTER);
            // 1 · offers out resolve one week after sending
            List<Dictionary<string, object>> offers = rec.OffersOut;
            for (int i = offers.Count - 1; i >= 0; i--)
            {
                Dictionary<string, object> off = offers[i];
                Dictionary<string, object> cand = CandById(state, Ds(off, "candidate_id", ""));
                if (cand == null) { offers.RemoveAt(i); continue; }
                if (state.Week <= Di(off, "sent_wk", state.Week)) continue;   // she thinks for a week
                double odds = AcceptanceOdds(state, cand, Di(off, "cash_wk", 0),
                    D(off, "options_pct", 0.0));
                if (r152.Randf() * 100.0 < odds)
                {
                    OfferAccepted(state, rep, cand, off);
                    offers.RemoveAt(i);
                    continue;
                }
                bool mercenary = Ds(cand, "profile", "") == "mercenary";
                if (r153.Randf() < (mercenary ? 0.35 : 0.15))
                {
                    cand["stage"] = "lost";
                    rep.Events.Add(Ds(cand, "name", "?")
                        + " took a rival's counter-offer — her profile chases "
                        + (mercenary ? "cash" : "meaning"));
                }
                else
                {
                    cand["stage"] = "interviewed";
                    cand["ask"] = Round10(D(cand, "ask", 0) * 1.05);
                    rep.Lines.Add(Ds(cand, "name", "?") + " declined — the ask hardened to $"
                        + Money(Di(cand, "ask", 0)));
                }
                state.Hype = Gd.Clampi(state.Hype - 1, 0, 100);
                offers.RemoveAt(i);
            }
            // 2 · arrivals per open seat
            Rng r150 = SimEngine.RngForSalt(state, SimEngine.SALT_RECRUIT_ARRIVALS);
            Rng r151 = SimEngine.RngForSalt(state, SimEngine.SALT_RECRUIT_PROFILE);
            int born = 0;
            foreach (var rd in rec.Roles)
            {
                double lam = ArrivalRateR(state, rd);
                if (lam <= 0.0) continue;
                double p = Gd.Minf(lam, 8.0) / 8.0;
                int count = 0;
                for (int k = 0; k < 8; k++) if (r150.Randf() < p) count += 1;
                for (int c = 0; c < count; c++)
                {
                    int skill = DrawSkill(r151);
                    double lo = D(rd, "band_lo", 0);
                    double hi = D(rd, "band_hi", 0);
                    int ask = Round10((lo + (hi - lo) * (skill - 1) / 4.0) * r151.RandfRange(0.95, 1.12));
                    rec.Candidates.Add(new Dictionary<string, object>
                    {
                        { "id", "cand_" + state.Week.ToString(CultureInfo.InvariantCulture) + "_" + born.ToString(CultureInfo.InvariantCulture) },
                        { "role_id", Ds(rd, "id", "") },
                        { "name", FreshName(state, r151) },
                        { "ask", ask },
                        { "profile", r151.Randf() < MERCENARY_P ? "mercenary" : "missionary" },
                        { "skill", skill }, { "stage", "applied" }, { "arrived_wk", state.Week },
                    });
                    born += 1;
                }
                if (count > 0)
                    rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0} applied for {1} (advert ${2}/wk -> ≈{3}/wk)", count,
                        Ds(rd, "seat", "?").ToUpperInvariant(), Di(rd, "advert_wk", 0), Gd.F(lam, 1)));
            }
            // 3 · patience
            List<Dictionary<string, object>> cands = rec.Candidates;
            for (int j = cands.Count - 1; j >= 0; j--)
            {
                Dictionary<string, object> cd = cands[j];
                string stg = Ds(cd, "stage", "");
                if (stg == "applied" || stg == "interviewed")
                {
                    if (state.Week - Di(cd, "arrived_wk", state.Week) >= CANDIDATE_PATIENCE)
                    {
                        if (Di(cd, "skill", 3) >= 4)
                            rep.Events.Add(Ds(cd, "name", "?")
                                + " stopped waiting — the good ones are gone in weeks");
                        cands.RemoveAt(j);
                    }
                }
                else if ((stg == "joined" || stg == "lost")
                    && state.Week - Di(cd, "arrived_wk", 0) > 10)
                {
                    cands.RemoveAt(j);
                }
            }
        }

        static void OfferAccepted(GameState state, WeeklyReport rep,
            Dictionary<string, object> cand, Dictionary<string, object> off)
        {
            if (SimLabor.SeatsLeft(state) <= 0)
            {
                cand["stage"] = "interviewed";
                rep.Lines.Add(Ds(cand, "name", "?")
                    + " said yes but the house is full — the offer lapsed");
                return;
            }
            Dictionary<string, object> role = RoleById(state, Ds(cand, "role_id", ""));
            string seat = role != null ? Ds(role, "seat", "engineer") : "engineer";
            state.Pipeline.Add(new PipelineHire { Name = Ds(cand, "name", "hire"),
                Role = SimLabor.RoleRow(seat), Salary = Di(off, "cash_wk", 1200),
                WeeksIn = 0, Quirk = "", Skill = Gd.Clampi(Di(cand, "skill", 3), 1, 5) });
            cand["stage"] = "joined";
            double opt = D(off, "options_pct", 0.0);
            string grantTxt = "";
            if (opt > 0.0)
            {
                string line = GrantOptions(state, Ds(cand, "name", "hire"), opt);
                grantTxt = line != "" ? " · " + line : " · the pool ran dry — cash-only after all";
            }
            if (role != null) CloseSeat(state, Ds(role, "id", ""));
            rep.Events.Add(Ds(cand, "name", "?") + " SIGNED at $" + Money(Di(off, "cash_wk", 0))
                + "/wk — onboarding" + grantTxt);
        }

        static int DrawSkill(Rng rng)
        {
            double u = rng.Randf();
            double acc = 0.0;
            double[] weights = { 0.15, 0.25, 0.30, 0.20, 0.10 };
            for (int i = 0; i < weights.Length; i++)
            {
                acc += weights[i];
                if (u < acc) return i + 1;
            }
            return 5;
        }

        static string FreshName(GameState state, Rng rng)
        {
            List<string> taken = SimLabor.TakenNames(state);
            string nm = "";
            for (int i = 0; i < 5; i++)
            {
                nm = WorldGen.PersonName(rng);
                if (!taken.Contains(nm)) return nm;
            }
            return nm;
        }

        // ═════════════════════════ BUYOUT OFFERS ══════════════════════════

        static void BuyoutWeekly(GameState state, WeeklyReport rep)
        {
            Rng r122 = SimEngine.RngForSalt(state, SimEngine.SALT_OWN_BUYOUT);
            if (state.BuyoutOffer.Count > 0)
            {
                if (state.Mna == null || (state.Mna.Buyer ?? "") != Ds(state.BuyoutOffer, "buyer", ""))
                {
                    state.LogAction("the buyout folder closed unanswered — "
                        + Ds(state.BuyoutOffer, "buyer", "a buyer") + "'s offer left the table");
                    state.BuyoutOffer = new Dictionary<string, object>();
                }
                return;
            }
            if (state.Mna != null)
            {
                state.BuyoutOffer = DressOffer(state, state.Mna, r122);
                rep.Events.Add("THE OFFER, IN WRITING: "
                    + Ds(state.BuyoutOffer, "headline_line", "the structure is on the desk")
                    + " — read the small lines on THE OFFER desk");
                return;
            }
            if (state.ExitValue > 0 || state.Dead) return;
            if (state.Week < 6 || state.Week < state.MnaLastWeek + 10) return;
            double p = Gd.Clampf(0.02 + 0.0004 * state.Traction + 0.0006 * state.Hype
                + 0.01 * state.EraIndex(), 0.0, 0.10);
            if (r122.Randf() >= p) return;
            int v = SimEngine.Valuation(state);
            double prem = r122.RandfRange(0.9, 1.3);
            Rival strong = StrongestRival(state);
            string buyer = (strong != null && strong.Strength >= 55.0) ? strong.Name
                : "a strategic who has been watching";
            state.Mna = new MnaOffer { Buyer = buyer, Why = "inbound",
                Premium = Gd.Snappedf(prem, 0.01),
                Price = Gd.Maxi((int)(v * prem), 10000),
                ExpiresWeek = state.Week + 2 };
            state.MnaLastWeek = state.Week;
            state.BuyoutOffer = DressOffer(state, state.Mna, r122);
            rep.Events.Add(string.Format(CultureInfo.InvariantCulture,
                "AN OFFER FOR THE COMPANY: {0} puts ${1} on the table — it expires wk {2}. THE OFFER desk has the fine print",
                buyer, Money(state.Mna.Price), state.Mna.ExpiresWeek));
        }

        static Dictionary<string, object> DressOffer(GameState state, MnaOffer mo, Rng r)
        {
            int price = mo.Price;
            string why = mo.Why ?? "";
            double fCash = 0.4, fStock = 0.35;
            switch (why)
            {
                case "lifeline": fCash = 0.8; fStock = 0.2; break;
                case "rival": fCash = 0.5; fStock = 0.3; break;
                case "boom": fCash = 0.3; fStock = 0.5; break;
            }
            fCash = Gd.Clampf(fCash + r.RandfRange(-0.1, 0.1), 0.1, 0.9);
            fStock = Gd.Clampf(fStock + r.RandfRange(-0.1, 0.1), 0.0, 0.9 - fCash + 0.8);
            if (fCash + fStock > 1.0) fStock = 1.0 - fCash;
            int cash = (int)(price * fCash);
            int stock = (int)(price * fStock);
            int earnout = price - cash - stock;
            int lockup = 0;
            if (stock > 0) lockup = r.RandiRange(26, 104);
            string controller = (earnout > 0 && r.Randf() < 0.6) ? "buyer" : "neutral";
            int retention = r.RandiRange(52, 104);
            bool carve = r.Randf() < 0.3;
            var flags = new List<string>();
            if (earnout > 0 && controller == "buyer")
                flags.Add("the earnout's targets are set — and measured — by the buyer");
            if (lockup >= 52)
                flags.Add("$" + MoneyShort(stock).TrimStart('$') + " of the price is their stock, locked "
                    + (lockup / 4).ToString(CultureInfo.InvariantCulture) + " months");
            if (price < (int)(SimEngine.Valuation(state) * 0.8) && mo.ExpiresWeek - state.Week <= 2)
                flags.Add("a low price on a short fuse — expiry pressure is the point");
            if (carve)
                flags.Add("the retention pool is carved from YOUR share, not the buyer's");
            return new Dictionary<string, object>
            {
                { "buyer", mo.Buyer ?? "a buyer" }, { "headline", price },
                { "cash", cash }, { "stock", stock }, { "lockup_wks", lockup },
                { "earnout", earnout }, { "earnout_controller", controller },
                { "retention_wks", retention }, { "retention_carve", carve },
                { "expires_wk", mo.ExpiresWeek }, { "fishy_flags", flags },
                { "why", why }, { "arrived_wk", state.Week }, { "countered", false },
                { "headline_line", (mo.Buyer ?? "a buyer") + " offers $" + Money(price) },
            };
        }

        /// <summary>THE WATERFALL — pure. Debts → prefs-or-convert → the split
        /// incl. vested ESOP (the unallocated pool leaves the denominator).</summary>
        public static Dictionary<string, object> Waterfall(GameState state, int price)
        {
            var rows = new List<Dictionary<string, object>>();
            double pot = price;
            double debts = SimBank.DebtTotal(state);
            double debtsPaid = Gd.Minf(pot, debts);
            pot -= debtsPaid;
            if (debtsPaid > 0.0)
                rows.Add(new Dictionary<string, object> { { "holder", "the bank" },
                    { "take", Gd.RoundToInt(debtsPaid) }, { "note", "debts die first" } });
            double afterDebts = pot;
            double prefTotal = 0.0;
            var choices = new List<Dictionary<string, object>>();
            foreach (Instrument inst in state.Instruments)
            {
                double pct = inst.Pct;
                double convPct = pct;
                if (Unconverted(inst))
                {
                    double basis = Gd.Minf(inst.Cap > 0 ? inst.Cap : price, price);
                    convPct = Gd.Clampf(100.0 * AmountDue(inst, state.Week) / Gd.Maxf(basis, 1.0),
                        0.0, CONVERT_PCT_CAP);
                }
                double prefAmt = 0.0;
                if (inst.Prefs > 0.0) prefAmt = inst.Amount * inst.Prefs;
                else if (Unconverted(inst)) prefAmt = AmountDue(inst, state.Week);
                double convVal = convPct / 100.0 * afterDebts;
                choices.Add(new Dictionary<string, object> { { "inst", inst },
                    { "conv_pct", convPct }, { "pref", prefAmt },
                    { "converts", convVal >= prefAmt } });
                if (convVal < prefAmt) prefTotal += prefAmt;
            }
            prefTotal = Gd.Minf(prefTotal, pot);
            pot -= prefTotal;
            double cof = 0.0;
            foreach (Cofounder cf in state.Cofounders)
                cof += cf.EquityDiluted ?? cf.Equity;
            double esopVested = 0.0;
            if (state.Esop != null)
                foreach (EsopGrant g in state.Esop.Granted)
                {
                    if ((g.EmpId ?? "").StartsWith("left:")) esopVested += g.Pct;
                    else esopVested += VestFrac(state.Week - g.VestStartWk) * g.Pct;
                }
            double convPcts = 0.0;
            foreach (var ch in choices)
                if (Db(ch, "converts", false)) convPcts += D(ch, "conv_pct", 0.0);
            double denom = Gd.Maxf(state.FounderPct + cof + esopVested + convPcts, 0.01);
            foreach (var cd in choices)
            {
                var inst2 = (Instrument)cd["inst"];
                if (Db(cd, "converts", false))
                {
                    double take = pot * D(cd, "conv_pct", 0.0) / denom;
                    rows.Add(new Dictionary<string, object> { { "holder", inst2.Holder },
                        { "take", Gd.RoundToInt(take) },
                        { "note", string.Format(CultureInfo.InvariantCulture,
                            "converts — {0}% beats their {1} (${2}); computed",
                            Gd.F(D(cd, "conv_pct", 0.0), 0),
                            inst2.Prefs > 0.0 ? Gd.F(inst2.Prefs, 0) + "×" : "1×",
                            Money(inst2.Amount)) } });
                }
                else
                {
                    rows.Add(new Dictionary<string, object> { { "holder", inst2.Holder },
                        { "take", Gd.RoundToInt(Gd.Minf(D(cd, "pref", 0.0), prefTotal)) },
                        { "note", "takes the preference — safer than converting" } });
                }
            }
            double esopTake = pot * esopVested / denom;
            if (esopTake >= 1.0)
                rows.Add(new Dictionary<string, object> { { "holder", "the ESOP holders" },
                    { "take", Gd.RoundToInt(esopTake) },
                    { "note", "vested only — your people get paid too" } });
            foreach (Cofounder cf2 in state.Cofounders)
            {
                double cpct = cf2.EquityDiluted ?? cf2.Equity;
                if (cpct > 0.01)
                    rows.Add(new Dictionary<string, object> { { "holder", string.IsNullOrEmpty(cf2.Name) ? "cofounder" : cf2.Name },
                        { "take", Gd.RoundToInt(pot * cpct / denom) }, { "note", "common" } });
            }
            double yourTake = pot * state.FounderPct / denom;
            int breakeven = Gd.RoundToInt(debts + prefTotal);
            return new Dictionary<string, object> { { "rows", rows },
                { "your_take", Gd.RoundToInt(yourTake) }, { "esop_take", Gd.RoundToInt(esopTake) },
                { "debts", Gd.RoundToInt(debtsPaid) }, { "prefs_paid", Gd.RoundToInt(prefTotal) },
                { "breakeven", breakeven } };
        }

        public static Dictionary<string, object> TakeDecomposed(Dictionary<string, object> bo, int yourTake)
        {
            double headline = Gd.Maxf(D(bo, "headline", 1), 1.0);
            int cash = Gd.RoundToInt(yourTake * D(bo, "cash", 0) / headline);
            int stock = Gd.RoundToInt(yourTake * D(bo, "stock", 0) / headline);
            return new Dictionary<string, object> { { "cash", cash }, { "stock", stock },
                { "earnout", yourTake - cash - stock } };
        }

        public static List<Dictionary<string, object>> Powers(GameState state, int price)
        {
            var outp = new List<Dictionary<string, object>>();
            outp.Add(new Dictionary<string, object> { { "who", "you" },
                { "line", "your yes is needed" }, { "blocks", false } });
            Dictionary<string, object> wf = Waterfall(state, price);
            var rows = (List<Dictionary<string, object>>)wf["rows"];
            double prefPctTotal = 0.0;
            double prefPctYes = 0.0;
            foreach (Instrument inst in state.Instruments)
            {
                double pct = inst.Pct;
                if (pct <= 0.0) continue;
                prefPctTotal += pct;
                int take = 0;
                foreach (var row in rows)
                    if (Ds(row, "holder", "") == inst.Holder) take = Di(row, "take", 0);
                bool happy = take >= inst.Amount;
                if (happy) prefPctYes += pct;
                if (inst.Protective)
                    outp.Add(new Dictionary<string, object> { { "who", inst.Holder },
                        { "line", "holds a sale veto (protective provision) — leaning "
                            + (happy ? "yes" : "NO") },
                        { "blocks", !happy } });
            }
            foreach (Instrument inst2 in state.Instruments)
            {
                double thr = inst2.DragThreshold;
                if (thr > 0.0 && prefPctTotal > 0.0)
                {
                    double share = 100.0 * prefPctYes / prefPctTotal;
                    outp.Add(new Dictionary<string, object> { { "who", "drag-along" },
                        { "line", string.Format(CultureInfo.InvariantCulture,
                            "≥{0}% of preferred could force a sale — {1}", Gd.F(thr, 0),
                            share >= thr ? "TRIGGERED at " + Gd.F(share, 0) + "%" : "not triggered here") },
                        { "blocks", false } });
                    break;
                }
            }
            return outp;
        }

        public static string BuyoutAccept(GameState state)
        {
            if (state.BuyoutOffer.Count == 0) return "";
            Dictionary<string, object> bo = state.BuyoutOffer;
            int price = Di(bo, "headline", 0);
            Dictionary<string, object> wf = Waterfall(state, price);
            Dictionary<string, object> dec = TakeDecomposed(bo, Di(wf, "your_take", 0));
            state.ExitValue = price;
            state.SetFlag("acquired_exit");
            if (Ds(bo, "why", "") == "lifeline") state.SetFlag("soft_landing");
            state.SetMeta("exit_take", Di(wf, "your_take", 0));
            string line = string.Format(CultureInfo.InvariantCulture,
                "SOLD to {0} for ${1} — the waterfall pays you ≈${2} (${3} cash + ${4} locked stock + ${5} maybe-earnout)",
                Ds(bo, "buyer", "a buyer"), Money(price), Money(Di(wf, "your_take", 0)),
                Money(Di(dec, "cash", 0)), Money(Di(dec, "stock", 0)), Money(Di(dec, "earnout", 0)));
            state.LogAction(line);
            state.Mna = null;
            state.BuyoutOffer = new Dictionary<string, object>();
            return line;
        }

        public static string BuyoutNegotiate(GameState state)
        {
            if (state.BuyoutOffer.Count == 0 || Db(state.BuyoutOffer, "countered", false))
                return "";
            Dictionary<string, object> bo = state.BuyoutOffer;
            Rng r = SimEngine.RngForSalt(state, SimEngine.SALT_OWN_BUYOUT);
            double lean = 1.0;
            foreach (var p in Powers(state, Di(bo, "headline", 0)))
                if (Db(p, "blocks", false)) lean = 0.95;
            double mult = r.RandfRange(0.95, 1.15) * lean;
            int newPrice = Gd.Maxi((int)(D(bo, "headline", 0) * mult), 10000);
            double scale = newPrice / Gd.Maxf(D(bo, "headline", 1), 1.0);
            bo["headline"] = newPrice;
            bo["cash"] = (int)(D(bo, "cash", 0) * scale);
            bo["stock"] = (int)(D(bo, "stock", 0) * scale);
            bo["earnout"] = newPrice - Di(bo, "cash", 0) - Di(bo, "stock", 0);
            bo["countered"] = true;
            bo["headline_line"] = Ds(bo, "buyer", "a buyer") + " offers $" + Money(newPrice);
            if (state.Mna != null) state.Mna.Price = newPrice;
            string line = string.Format(CultureInfo.InvariantCulture,
                "countered — {0} repriced to ${1} ({2}). One counter is all the room there is.",
                Ds(bo, "buyer", "the buyer"), Money(newPrice), mult >= 1.0 ? "up" : "down");
            state.LogAction(line);
            return line;
        }

        public static string BuyoutDecline(GameState state)
        {
            if (state.BuyoutOffer.Count == 0) return "";
            string buyer = Ds(state.BuyoutOffer, "buyer", "a buyer");
            state.Hype = Gd.Clampi(state.Hype + 2, 0, 100);
            state.Mna = null;
            state.MnaLastWeek = state.Week;
            state.BuyoutOffer = new Dictionary<string, object>();
            string line = "DECLINED " + buyer
                + "'s offer — the street heard you say no (+2 hype). Offers can sour, or come back higher.";
            state.LogAction(line);
            return line;
        }

        static Rival StrongestRival(GameState state)
        {
            Rival best = null;
            foreach (Rival rv in state.Rivals)
                if (best == null || rv.Strength > best.Strength) best = rv;
            return best;
        }

        // ═════════════════════ THE SPINE'S ENTRY POINTS ═══════════════════

        public static void TickPre(GameState state, WeeklyReport rep)
        {
            MigrateOwnership(state);
            CrystallizeLeavers(state, rep);
            if (state.RaiseState != null && state.RaiseState.Active)
            {
                state.RaiseState.FounderTimeTax = FOUNDER_TIME_TAX;
                SimEngine.AddStatus(state, RAISE_STATUS, 2);   // no-op until the catalog entry lands
            }
            else if (state.RaiseState != null)
            {
                state.RaiseState.FounderTimeTax = 0.0;
            }
            RecruitWeekly(state, rep);
        }

        public static void TickMoney(GameState state, WeeklyReport rep, MoneyWork m)
        {
            if (state.Recruitment == null) return;
            double ads = 0.0;
            foreach (var role in state.Recruitment.Roles)
                ads += D(role, "advert_wk", 0);
            if (ads > 0.0)
            {
                m.RecruitAds += ads;
                rep.Lines.Add("role adverts: −$" + ((int)ads).ToString(CultureInfo.InvariantCulture)
                    + "/wk (the seats stay lit)");
            }
        }

        public static void TickPost(GameState state, WeeklyReport rep)
        {
            if (state.Dead) return;
            RaiseWeekly(state, rep);
            BuyoutWeekly(state, rep);
        }

        public static List<string> Directives(GameState state)
        {
            var outp = new List<string>();
            if (state.RaiseState != null && state.RaiseState.Active)
                outp.Add(string.Format(CultureInfo.InvariantCulture,
                    "- THE RAISE is active: {0} in conversations, {1} terms on the table. It eats ~30% of the founder's week — the shop measurably slows.",
                    StagesIn(state, "conversations").Count, StagesIn(state, "terms").Count));
            else if (StagesIn(state, "terms").Count > 0)
                outp.Add("- Terms are on the table at THE RAISE; only sign_instrument signs, never your narration.");
            if (state.BuyoutOffer.Count > 0)
                outp.Add(string.Format(CultureInfo.InvariantCulture,
                    "- The buyout's fine print carries {0} flag(s) the founder can read on THE OFFER desk. The desk answers it, never you.",
                    Dl(state.BuyoutOffer, "fishy_flags").Count));
            if (state.Recruitment != null && state.Recruitment.OffersOut.Count > 0)
            {
                Dictionary<string, object> off = state.Recruitment.OffersOut[0];
                Dictionary<string, object> cand = CandById(state, Ds(off, "candidate_id", ""));
                outp.Add(string.Format(CultureInfo.InvariantCulture,
                    "- A comp offer is out to {0} (${1}/wk{2}), expires wk {3}.",
                    cand != null ? Ds(cand, "name", "a candidate") : "a candidate",
                    Di(off, "cash_wk", 0),
                    D(off, "options_pct", 0.0) > 0.0 ? " + " + Gd.F(D(off, "options_pct", 0.0), 1) + "% options" : "",
                    Di(off, "expires_wk", 0)));
            }
            return outp;
        }

        public static List<AttentionItem> Attention(GameState state)
        {
            var rows = new List<AttentionItem>();
            foreach (Instrument idd in MaturedNotes(state))
                rows.Add(new AttentionItem { Desk = "the raise", Key = "note_matured",
                    Severity = 3,
                    Label = Gd.Left("note matured — $"
                        + MoneyShort(AmountDue(idd, state.Week)).TrimStart('$') + " due or convert", 40) });
            if (state.BuyoutOffer.Count > 0)
            {
                int left = Gd.Maxi(Di(state.BuyoutOffer, "expires_wk", 0) - state.Week, 0);
                rows.Add(new AttentionItem { Desk = "the offer", Key = "buyout_live",
                    Severity = 3,
                    Label = Gd.Left("buyout expires in " + left.ToString(CultureInfo.InvariantCulture)
                        + " wk — answer it", 40), Control = "do_0" });
            }
            foreach (var st in StagesIn(state, "terms"))
            {
                var t = st.ContainsKey("terms") ? (Dictionary<string, object>)st["terms"] : null;
                rows.Add(new AttentionItem { Desk = "the raise", Key = "terms_open",
                    Severity = 2,
                    Label = Gd.Left("terms on the table — expire wk "
                        + (t != null ? Di(t, "expires_wk", 0) : 0).ToString(CultureInfo.InvariantCulture), 40) });
                break;
            }
            if (state.Recruitment != null)
            {
                foreach (var off in state.Recruitment.OffersOut)
                {
                    Dictionary<string, object> cand = CandById(state, Ds(off, "candidate_id", ""));
                    rows.Add(new AttentionItem { Desk = "recruitment", Key = "offer_out",
                        Severity = 2,
                        Label = Gd.Left((cand != null ? Ds(cand, "name", "someone") : "someone")
                            + "'s offer expires in "
                            + Gd.Maxi(Di(off, "expires_wk", 0) - state.Week, 0).ToString(CultureInfo.InvariantCulture)
                            + " wk", 40) });
                    break;
                }
                if (PoolFree(state) <= 0.0001 && state.Esop != null
                    && state.Recruitment.Roles.Count > 0)
                    rows.Add(new AttentionItem { Desk = "cap table", Key = "pool_empty",
                        Severity = 2, Label = "pool empty — no equity offers" });
            }
            if (state.Esop != null)
                foreach (EsopGrant g in state.Esop.Granted)
                {
                    if ((g.EmpId ?? "").StartsWith("left:")) continue;
                    int cliffIn = g.VestStartWk + CLIFF_WEEKS - state.Week;
                    if (cliffIn > 0 && cliffIn <= 4)
                    {
                        rows.Add(new AttentionItem { Desk = "cap table", Key = "cliff_near",
                            Severity = 1,
                            Label = Gd.Left((g.EmpId ?? "").Replace("_", " ") + "'s cliff lands in "
                                + cliffIn.ToString(CultureInfo.InvariantCulture) + " wk", 40) });
                        break;
                    }
                }
            if (NoShopUntil(state) > state.Week)
                rows.Add(new AttentionItem { Desk = "the raise", Key = "no_shop",
                    Severity = 1,
                    Label = Gd.Left("no-shop holds until wk "
                        + NoShopUntil(state).ToString(CultureInfo.InvariantCulture), 40) });
            return rows;
        }
    }
}
