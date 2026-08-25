using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Runway.Core
{
    /// <summary>
    /// LANE 02 — THE LABOR MARKET (roles, applicants, raises, severance). Spec: docs/design/02-labor-market.md
    ///
    /// THE ONE IDEA: a head is a PRICE, not a slot. What the street pays for a
    /// role (the MARKET RATE) is a world constant the player learns across runs;
    /// the advert against that rate decides who applies, the ask against it
    /// decides what a hire costs, and the salary against it decides who stays.
    /// Every departure prints the ratio that caused it.
    ///
    /// The spine calls, in tick order (00-spine section 1, HOOKS.md):
    ///   TickPre    tick 3b — arrivals (20/21), decay (22), review + ladder (23)
    ///   TickMoney  the money section — severance and the recruiter retainer
    ///   TickPost   after the record is written (this lane needs nothing there)
    /// and outside the tick: Directives feeds the DM block, Attention feeds
    /// every bang in the game through SimEngine.AttentionItems.
    ///
    /// THE PRODUCTIVITY SEAMS (section 4): the tick's own math calls
    /// SalesCapacity, DesignMult, CareEff, RndGain, DebtPaydown and OpsMult.
    /// Every one returns EXACTLY the pre-wave number for a skill-3 roster below
    /// floor era — that parity is what let this lane land on a live engine.
    ///
    /// TWIN LAW: this file and game/src/core/lanes/sim_labor.gd carry the same
    /// logic in the same order. The engines do NOT share PRNG internals, so
    /// parity means same checks and same behaviour, never a byte-equal draw.
    /// </summary>
    public static class SimLabor
    {
        // ─────────────────────── THE MARKET SALARY TABLE ─────────────────────
        /// <summary>
        /// $/wk by role x era. Real analogue: the occupational wage structure
        /// (engineers above designers above sales above ops above support)
        /// multiplied by the firm-size wage premium — a bigger employer pays
        /// ~25-35% more for the same occupation, which is the ~x1.3 step per
        /// column. Anchor: garage engineer 1200 is the engine's own hire-salary
        /// default, so nothing moved under a legacy save. Drops: regional and
        /// experience spreads collapse into one number per role x era; the
        /// within-role spread is carried by the skill-ask curve, where the desk
        /// can show it. Benefits are not salary — they are the office lever.
        /// </summary>
        public static readonly Dictionary<string, Dictionary<string, int>> RoleMarket =
            new Dictionary<string, Dictionary<string, int>>
        {
            { "engineer", new Dictionary<string, int> { {"garage",1200}, {"coworking",1500}, {"office",2000}, {"floor",2600}, {"hq",3400} } },
            { "sales",    new Dictionary<string, int> { {"garage",1000}, {"coworking",1250}, {"office",1650}, {"floor",2150}, {"hq",2800} } },
            { "designer", new Dictionary<string, int> { {"garage",1050}, {"coworking",1300}, {"office",1750}, {"floor",2250}, {"hq",2950} } },
            { "ops",      new Dictionary<string, int> { {"garage", 850}, {"coworking",1050}, {"office",1400}, {"floor",1850}, {"hq",2400} } },
            { "support",  new Dictionary<string, int> { {"garage", 700}, {"coworking", 900}, {"office",1150}, {"floor",1500}, {"hq",1950} } },
            { "manager",  new Dictionary<string, int> { {"garage",1450}, {"coworking",1800}, {"office",2400}, {"floor",3000}, {"hq",3900} } },
        };

        /// <summary>Matched longest-first, so "sales engineer" reads as an
        /// engineer and never as sales — the same substring idiom the tick
        /// already uses for its head counts.</summary>
        public static readonly string[] RoleOrder =
            { "engineer", "designer", "support", "manager", "sales", "ops" };

        /// <summary>What a skill band asks, as a multiple of the market rate.
        /// 0.70..1.60 is about 2.3x, real within-occupation p10-p90 dispersion.</summary>
        public static readonly double[] SkillAsk = { 0.70, 0.85, 1.00, 1.25, 1.60 };
        /// <summary>The applicant quality distribution: 15/25/30/20/10.</summary>
        public static readonly double[] SkillWeights = { 0.15, 0.25, 0.30, 0.20, 0.10 };

        public const int RecruiterFee = 1500;      // $/wk on retainer, floor era up
        public const int BenefitsPerHead = 250;    // $/wk the office lever should carry, office up
        public const double CrowdHalf = 4.0;       // applicants waiting that halve the arrival rate
        public const int StaleWeeks = 8;           // a role open this long reads as a red flag
        public const int GraceWeeks = 2;           // a candidate waits this long for free
        public const int PatienceCap = 5;          // and is gone for certain at this many
        public const int ReviewCycle = 12;         // weeks between synchronised comp reviews

        /// <summary>The keyless quirk pool (salt 24). A dressing reply replaces
        /// these in place; nothing waits on it — the cards are playable the
        /// instant they exist.</summary>
        public static readonly string[] QuirkPool =
        {
            "brings a mechanical keyboard to interviews", "answers every question with a diagram",
            "left three startups the month before each died", "refers to money only as 'runway'",
            "has strong opinions about fonts", "already uses your product wrong",
            "asks about the pension plan, twice", "codes only between 11pm and 4am",
            "keeps a spreadsheet of past managers' flaws", "quotes their old boss like scripture",
            "negotiates via long silences", "brings homemade cookies to close deals",
            "insists on being called a craftsperson", "hosts a podcast about quitting jobs",
            "writes thank-you notes in fountain pen", "claims to have met your rival's founder",
            "will not work Wednesdays, won't say why", "laughs at their own spreadsheets",
            "alphabetizes the shared fridge", "sends follow-ups at 5am sharp",
            "wears their last employer's company shirt", "describes everything as 'basically shipping'",
            "interviews you back, taking notes", "once returned a signing bonus on principle",
        };

        // ═══════════════════════ THE PURE HELPERS ════════════════════════════
        // One source for the engine, the desk and the tests — a rule the desk
        // recomputes for itself is a rule that drifts.

        public static int EraIdx(string era)
        {
            int i = GameState.ERAS.IndexOf(era ?? "");
            return i < 0 ? 0 : i;
        }

        /// <summary>The table row a free-text role string belongs to.</summary>
        public static string RoleRow(string role)
        {
            string low = (role ?? "").ToLowerInvariant();
            for (int i = 0; i < RoleOrder.Length; i++)
            {
                if (low.Contains(RoleOrder[i])) return RoleOrder[i];
            }
            return "engineer";
        }

        /// <summary>What the street pays for this role at this stage of company.</summary>
        public static int MarketSalary(string role, string era)
        {
            Dictionary<string, int> row;
            if (!RoleMarket.TryGetValue(RoleRow(role), out row)) row = RoleMarket["engineer"];
            int v;
            return row.TryGetValue(era ?? "", out v) ? v : row["garage"];
        }

        /// <summary>THE ERA LADDER, roles half (00-spine section 9): engineers
        /// and sellers from the first day; the specialists once there is a
        /// company to specialise in; managers only when there is a floor.</summary>
        public static bool RoleUnlocked(string role, string era)
        {
            switch (RoleRow(role))
            {
                case "engineer":
                case "sales": return true;
                case "designer":
                case "ops":
                case "support": return EraIdx(era) >= 1;
                case "manager": return EraIdx(era) >= 3;
            }
            return false;
        }

        /// <summary>THE MARKET ITSELF opens at coworking: a garage hires the
        /// people it already knows, and nobody answers an advert taped to a
        /// garage door.</summary>
        public static bool MarketOpen(string era)
        {
            return EraIdx(era) >= 1;
        }

        /// <summary>Severance norms, era-banded by tenure: a handshake becomes a
        /// package. Real analogue — the "1-2 weeks per year of service" rule of
        /// thumb and the statutory tenure bands. ALWAYS owed (DECISIONS.md).</summary>
        public static int SeveranceWeeks(string era, int tenureWk)
        {
            switch (era)
            {
                case "garage": return 1;
                case "coworking": return 2;
                case "hq": return tenureWk < 78 ? 3 : 4;
            }
            if (tenureWk < 26) return 2;
            if (tenureWk < 78) return 3;
            return 4;
        }

        /// <summary>How many recruiters this era will keep on retainer.</summary>
        public static int RecruiterCap(string era)
        {
            int i = EraIdx(era);
            if (i >= 4) return 2;
            if (i >= 3) return 1;
            return 0;
        }

        public static int RecruitersActive(GameState state)
        {
            return Gd.Clampi(state.Recruiters, 0, RecruiterCap(state.Era));
        }

        /// <summary>One role, one seat — until hq turns a role into a batch.</summary>
        public static int SeatCap(string era)
        {
            return EraIdx(era) >= 4 ? 5 : 1;
        }

        // ── the roster reads ─────────────────────────────────────────────────

        public static int SkillOf(Employee e) { return Gd.Clampi(e.Skill, 1, 5); }
        public static int SkillOf(Applicant a) { return Gd.Clampi(a.Skill, 1, 5); }

        public static double AskMult(int skill)
        {
            return SkillAsk[Gd.Clampi(skill, 1, 5) - 1];
        }

        /// <summary>Tenure in weeks. An unknown hire week (a legacy save, a
        /// DM-conjured hire) reads as tenure 0 — the world charges the shortest
        /// band, never a guess.</summary>
        public static int TenureOf(GameState state, Employee e)
        {
            if (e.HiredWeek < 0) return 0;
            return Gd.Maxi(state.Week - e.HiredWeek, 0);
        }

        /// <summary>What this person would cost on the open market today. Era
        /// moves it, which is real pay compression: every promotion silently
        /// underpays the veterans.</summary>
        public static int FairPay(GameState state, Employee e)
        {
            return Gd.RoundToInt(MarketSalary(e.Role ?? "", state.Era) * AskMult(SkillOf(e)));
        }

        /// <summary>SPAN OF CONTROL (floor up): five direct reports per manager,
        /// plus the five the founder carries personally. Below the floor era it
        /// is exactly 1.0 — the founder manages everyone, which is what a small
        /// company IS.</summary>
        public static double SpanMult(GameState state)
        {
            if (EraIdx(state.Era) < 3) return 1.0;
            double mgrSkill = 0.0;
            int nonMgr = 0;
            for (int i = 0; i < state.Employees.Count; i++)
            {
                Employee e = state.Employees[i];
                if ((e.Role ?? "").ToLowerInvariant().Contains("manager")) mgrSkill += SkillOf(e);
                else nonMgr += 1;
            }
            if (nonMgr <= 0) return 1.0;
            double capacity = 5.0 * (1.0 + mgrSkill / 3.0);
            return Gd.Clampf(capacity / nonMgr, 0.5, 1.0);
        }

        public static int ManagerCount(GameState state)
        {
            int n = 0;
            for (int i = 0; i < state.Employees.Count; i++)
            {
                if ((state.Employees[i].Role ?? "").ToLowerInvariant().Contains("manager")) n += 1;
            }
            return n;
        }

        /// <summary>Salary plus the standing cost of having a desk at all, split
        /// across every head. THE FULLY-LOADED COST — the number a founder
        /// underestimates exactly once.</summary>
        public static int LoadedCost(GameState state, int salary)
        {
            int heads = 1 + state.Employees.Count + state.Pipeline.Count;
            int rent;
            if (!GameState.ERA_RENT.TryGetValue(state.Era ?? "", out rent)) rent = 150;
            double overhead = rent + 50.0 + state.Traction * 0.05 + state.Budgets.Office;
            return salary + Gd.RoundToInt(overhead / Gd.Maxi(heads, 1));
        }

        public static int PayrollWk(GameState state)
        {
            int total = 0;
            for (int i = 0; i < state.Employees.Count; i++) total += state.Employees[i].Salary;
            for (int i = 0; i < state.Pipeline.Count; i++) total += state.Pipeline[i].Salary;
            return total;
        }

        public static int LoadedPayrollWk(GameState state)
        {
            int total = 0;
            for (int i = 0; i < state.Employees.Count; i++)
                total += LoadedCost(state, state.Employees[i].Salary);
            for (int i = 0; i < state.Pipeline.Count; i++)
                total += LoadedCost(state, state.Pipeline[i].Salary);
            return total;
        }

        /// <summary>What the office lever is expected to carry, office era up.</summary>
        public static int ExpectedBenefits(GameState state)
        {
            if (EraIdx(state.Era) < 2) return 0;
            return BenefitsPerHead * (state.Employees.Count + state.Pipeline.Count);
        }

        public static bool BenefitsShort(GameState state)
        {
            int want = ExpectedBenefits(state);
            return want > 0 && state.Budgets.Office < want;
        }

        /// <summary>The severance invoice for one person — quoted before the
        /// deed, always.</summary>
        public static int SeveranceFor(GameState state, Employee e)
        {
            return e.Salary * SeveranceWeeks(state.Era, TenureOf(state, e));
        }

        // ── the market reads ─────────────────────────────────────────────────

        public static OpenRole OpenRoleRow(GameState state, string role)
        {
            string r = RoleRow(role);
            for (int i = 0; i < state.OpenRoles.Count; i++)
            {
                if ((state.OpenRoles[i].Role ?? "") == r) return state.OpenRoles[i];
            }
            return null;
        }

        public static int WaitingFor(GameState state, string role)
        {
            int n = 0;
            for (int i = 0; i < state.Applicants.Count; i++)
            {
                if ((state.Applicants[i].Role ?? "") == role) n += 1;
            }
            return n;
        }

        /// <summary>THE ATTRACTIVENESS of one advert, 0..1. Real analogue, term
        /// by term: posted wages direct search (a better offer draws more AND
        /// better people); the 0.8x cliff is the reservation wage — below it
        /// nobody applies at all; hype, morale and era are employer brand,
        /// priced into applications instead of into wages.</summary>
        public static double Attractiveness(GameState state, string role, int offered)
        {
            int market = MarketSalary(role, state.Era);
            double ratio = Gd.Clampf(offered / Gd.Maxf(market, 1.0), 0.0, 2.0);
            if (ratio < 0.8) return 0.0;                    // THE FLOOR: silence
            return Gd.Clampf(0.35 + 1.1 * (ratio - 1.0)
                             + state.Hype / 250.0
                             + (state.Morale - 50.0) / 250.0
                             + 0.06 * EraIdx(state.Era), 0.0, 1.0);
        }

        /// <summary>Expected applicants per week for one open role — the number
        /// the desk prints beside the advert, so the price/flow trade is visible
        /// before the press.</summary>
        public static double ArrivalRate(GameState state, OpenRole row)
        {
            string role = row.Role ?? "engineer";
            double lam = Attractiveness(state, role, row.OfferedSalary) * 6.0;
            // CROWDING: one vacancy only absorbs so much attention
            lam *= CrowdHalf / (CrowdHalf + WaitingFor(state, role));
            // THE STALE ROLE: applicants read "open two months" as a warning
            if (state.Week - row.OpenedWeek >= StaleWeeks) lam *= 0.5;
            if (SimEngine.HasStatus(state, "talent_magnet")) lam *= 1.5;
            // THE PAID PIPELINE: one recruiter x1.75, two x2.5
            lam *= 1.0 + 0.75 * RecruitersActive(state);
            return lam;
        }

        /// <summary>Every settable advert passes this — 0.5x to 2.0x market.</summary>
        public static int ClampAdvert(int market, int offered)
        {
            return Gd.Clampi(offered, Gd.RoundToInt(market * 0.5), Gd.RoundToInt(market * 2.0));
        }

        /// <summary>And every settable salary — up to 2.5x, because keeping a
        /// star is allowed to cost more than hiring one.</summary>
        public static int ClampSalary(int market, int salary)
        {
            return Gd.Clampi(salary, Gd.RoundToInt(market * 0.5), Gd.RoundToInt(market * 2.5));
        }

        /// <summary>The named ladder both steppers walk (no sliders here).</summary>
        public static List<double> SalarySteps(int market, double top = 2.0)
        {
            double[] mults = { 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.1, 1.25, 1.4, 1.6, 1.8, 2.0, 2.25, 2.5 };
            var outv = new List<double>();
            for (int i = 0; i < mults.Length; i++)
            {
                if (mults[i] > top + 0.001) break;
                outv.Add(Gd.RoundToInt(market * mults[i] / 10.0) * 10);
            }
            return outv;
        }

        /// <summary>How many more heads this era has desks for. The pipeline
        /// counts: it is already on the payroll (the engine's own CanHire
        /// forgets that — spec section 10.1).</summary>
        public static int SeatsLeft(GameState state)
        {
            return Gd.Maxi(state.StaffCap() - state.Employees.Count - state.Pipeline.Count, 0);
        }

        // ══════════════ HIRE / REJECT / FIRE / RAISE (the desk's API) ════════

        /// <summary>Post an advert. Refuses a duplicate role, a locked role, a
        /// shut market and a full house — the engine is the bouncer, the desk
        /// only prints the reason.</summary>
        public static bool OpenRoleAt(GameState state, string role, int offered)
        {
            string r = RoleRow(role);
            if (!MarketOpen(state.Era) || !RoleUnlocked(r, state.Era)) return false;
            if (OpenRoleRow(state, r) != null) return false;
            if (state.OpenRoles.Count >= SeatsLeft(state)) return false;
            int market = MarketSalary(r, state.Era);
            state.OpenRoles.Add(new OpenRole
            {
                Role = r,
                OfferedSalary = ClampAdvert(market, offered),
                OpenedWeek = state.Week,
                Seats = 1,
            });
            return true;
        }

        public static void SetRoleSalary(GameState state, string role, int offered)
        {
            OpenRole row = OpenRoleRow(state, role);
            if (row == null) return;
            row.OfferedSalary = ClampAdvert(MarketSalary(row.Role, state.Era), offered);
        }

        /// <summary>Close the requisition. The people already waiting stay
        /// waiting — they simply run out of patience like everyone else.</summary>
        public static void CloseRole(GameState state, string role)
        {
            string r = RoleRow(role);
            for (int i = 0; i < state.OpenRoles.Count; i++)
            {
                if ((state.OpenRoles[i].Role ?? "") == r)
                {
                    state.OpenRoles.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>Requisition batching, hq only.</summary>
        public static void SetSeats(GameState state, string role, int seats)
        {
            OpenRole row = OpenRoleRow(state, role);
            if (row == null) return;
            row.Seats = Gd.Clampi(seats, 1, SeatCap(state.Era));
        }

        public static void SetRecruiters(GameState state, int n)
        {
            state.Recruiters = Gd.Clampi(n, 0, RecruiterCap(state.Era));
        }

        /// <summary>THE ADVERT IS THE MAGNET, THE ASK IS THE CONTRACT: a hire is
        /// booked at what the candidate asked, never at what the role
        /// advertised. They join the existing two-week onboarding pipeline —
        /// paid at once, productive when it graduates.</summary>
        public static Dictionary<string, object> HireApplicant(GameState state, int idx)
        {
            if (idx < 0 || idx >= state.Applicants.Count) return null;
            if (SeatsLeft(state) <= 0) return null;
            Applicant a = state.Applicants[idx];
            state.Pipeline.Add(new PipelineHire
            {
                Name = a.Name ?? "a hire",
                Role = a.Role ?? "engineer",
                Salary = a.Ask,
                WeeksIn = 0,
                Quirk = a.Quirk ?? "",
                Skill = SkillOf(a),
            });
            state.Applicants.RemoveAt(idx);
            OpenRole row = OpenRoleRow(state, a.Role ?? "engineer");
            if (row != null)
            {
                row.Seats -= 1;
                if (row.Seats <= 0) CloseRole(state, row.Role);
            }
            return new Dictionary<string, object>
            {
                { "name", a.Name ?? "a hire" }, { "role", a.Role ?? "engineer" },
                { "salary", a.Ask }, { "skill", SkillOf(a) },
            };
        }

        public static void RejectApplicant(GameState state, int idx)
        {
            if (idx >= 0 && idx < state.Applicants.Count) state.Applicants.RemoveAt(idx);
        }

        /// <summary>LETTING SOMEONE GO COSTS REAL MONEY, ALWAYS (DECISIONS.md —
        /// no for-cause waiver; the world charges you anyway, which is also the
        /// anti-exploit against fire-and-rehire cycling). The invoice accrues now
        /// and is BOOKED by next week's money section, so the P&amp;L identity never
        /// has to bend for it. The -8 morale is layoff survivor syndrome:
        /// firings depress the people who stay.</summary>
        public static Dictionary<string, object> FireEmployee(GameState state, int idx)
        {
            if (idx < 0 || idx >= state.Employees.Count) return null;
            Employee e = state.Employees[idx];
            int weeks = SeveranceWeeks(state.Era, TenureOf(state, e));
            int pay = e.Salary;
            int tenure = TenureOf(state, e);
            var outv = new Dictionary<string, object>
            {
                { "name", e.Name ?? "someone" }, { "severance", pay * weeks },
                { "weeks", weeks }, { "salary", pay }, { "tenure", tenure },
            };
            state.Employees.RemoveAt(idx);
            state.SeveranceDue += pay * weeks;
            AddSeveranceNote(state, string.Format(CultureInfo.InvariantCulture,
                "severance: ${0} ({1} wks × ${2} — tenure {3} wks)",
                Money(pay * weeks), weeks, Money(pay), tenure));
            state.Morale = Gd.Clampi(state.Morale - 8, 0, 100);
            return outv;
        }

        /// <summary>A raise, clamped to the market band. Crossing 0.95x fair pay
        /// clears the ask and buys the small morale bump that fixing an
        /// injustice actually buys.</summary>
        public static int GrantRaise(GameState state, int idx, int newSalary)
        {
            if (idx < 0 || idx >= state.Employees.Count) return 0;
            Employee e = state.Employees[idx];
            int paid = ClampSalary(MarketSalary(e.Role ?? "", state.Era), newSalary);
            e.Salary = paid;
            int fair = FairPay(state, e);
            if (paid >= fair * 0.95)
            {
                if (e.WantsRaise)
                {
                    e.WantsRaise = false;
                    e.AskedWeek = -1;
                    state.Morale = Gd.Clampi(state.Morale + 2, 0, 100);
                }
                e.UnderpaidSince = -1;
            }
            return paid;
        }

        // ═══════════════════════ THE POACH INTERFACE ═════════════════════════
        // The rivals lane rolls the poach and owns the steal receipt; this lane
        // owns the roster and the consequences (00-spine section 13).

        /// <summary>Who a raider would bid for: the best person paid furthest
        /// under their worth. Real analogue: Lazear's raiding model — outside
        /// firms bid precisely for high ability paid below marginal product;
        /// 1.25x is the raider's margin. NULL = nobody qualifies.
        /// `market_salary` is what THIS person is worth (their skill at the going
        /// rate) and `pay_gap` = (worth - salary) / worth >= 0.2 is the same test
        /// as >= 1.25x, so a caller may use either.</summary>
        public static Dictionary<string, object> PoachTarget(GameState state)
        {
            int best = -1;
            int bestSkill = -1;
            double bestGap = -1.0;
            for (int i = 0; i < state.Employees.Count; i++)
            {
                Employee e = state.Employees[i];
                int sal = Gd.Maxi(e.Salary, 1);
                double gap = (double)FairPay(state, e) / sal;
                if (gap < 1.25) continue;
                int sk = SkillOf(e);
                if (sk > bestSkill || (sk == bestSkill && gap > bestGap))
                {
                    best = i;
                    bestSkill = sk;
                    bestGap = gap;
                }
            }
            if (best < 0) return null;
            Employee pick = state.Employees[best];
            int salary = Gd.Maxi(pick.Salary, 1);
            int worth = FairPay(state, pick);
            return new Dictionary<string, object>
            {
                { "index", best }, { "name", pick.Name ?? "someone" },
                { "role", pick.Role ?? "engineer" }, { "skill", SkillOf(pick) },
                { "salary", salary }, { "fair", worth }, { "market_salary", worth },
                { "gap_pct", ((double)worth / salary - 1.0) * 100.0 },
                { "pay_gap", (double)(worth - salary) / Gd.Maxi(worth, 1) },
            };
        }

        /// <summary>THE STEAL LANDED. They leave now, no severance (they were not
        /// let go), and the room feels it the way a resignation feels.</summary>
        public static Dictionary<string, object> PoachLands(GameState state, int index, string rival = "a rival")
        {
            if (index < 0 || index >= state.Employees.Count) return null;
            Employee e = state.Employees[index];
            int worth = FairPay(state, e);
            var outv = new Dictionary<string, object>
            {
                { "name", e.Name ?? "someone" }, { "role", e.Role ?? "engineer" },
                { "skill", SkillOf(e) }, { "salary", e.Salary }, { "rival", rival },
                { "line", string.Format(CultureInfo.InvariantCulture,
                    "{0} left for {1}: paid {2:0.00}× market and somebody noticed",
                    e.Name ?? "someone", rival, e.Salary / Gd.Maxf(worth, 1.0)) },
            };
            state.Employees.RemoveAt(index);
            state.Morale = Gd.Clampi(state.Morale - 6, 0, 100);
            MarkPoach(state, rival, (string)outv["name"]);
            return outv;
        }

        /// <summary>THE STEAL FAILED — and that is not the end of it.
        /// COUNTER-OFFER DYNAMICS (DECISIONS.md): being courted teaches a person
        /// exactly what they are worth, so their ask hardens. Mechanically the
        /// resignation clock is already two weeks in, so the ordinary 0.85x
        /// tolerance no longer buys time — only a real raise to fair pay keeps
        /// them, and only if it lands next week.</summary>
        public static Dictionary<string, object> PoachFailed(GameState state, int index, string rival = "a rival")
        {
            if (index < 0 || index >= state.Employees.Count) return null;
            Employee e = state.Employees[index];
            MarkPoach(state, rival, e.Name ?? "someone");
            int worth = FairPay(state, e);
            return new Dictionary<string, object>
            {
                { "index", index }, { "name", e.Name ?? "someone" },
                { "role", e.Role ?? "engineer" }, { "salary", e.Salary },
                { "fair", worth }, { "market_salary", worth }, { "rival", rival },
                { "line", HardenAsk(state, index, rival) },
            };
        }

        /// <summary>The counter-offer itself, in one place: the ask is now hard,
        /// and the clock it runs on is already two weeks in — the ordinary 0.85x
        /// tolerance has stopped buying time. Returns the receipt line, or an
        /// empty string when there is nobody to harden.</summary>
        static string HardenAsk(GameState state, int index, string rival)
        {
            if (index < 0 || index >= state.Employees.Count) return "";
            Employee e = state.Employees[index];
            e.WantsRaise = true;
            e.AskedWeek = state.Week - 2;
            if (e.UnderpaidSince < 0) e.UnderpaidSince = state.Week;
            return string.Format(CultureInfo.InvariantCulture,
                "{0} turned {1} down and came back with a number: ${2}/wk against a market of ${3}",
                e.Name ?? "someone", rival, Money(e.Salary), Money(FairPay(state, e)));
        }

        /// <summary>THE COUNTER-OFFER SEASON (DECISIONS.md). The rivals lane
        /// resolves its own poach at tick 6a — AFTER this section — and leaves a
        /// marker when the call failed. Being courted teaches a person exactly
        /// what they are worth, so the first thing this desk does the following
        /// week is read that marker and harden the ask. The marker is consumed,
        /// so one failed call raises one ask.</summary>
        static void ConsumeCounterOffer(GameState state, WeeklyReport rep)
        {
            if (Gd.ToInt(state.GetMetaF("poach_failed_wk", -1.0)) < 0) return;
            string who = MetaStr(state, "poach_failed_name", "");
            state.SetMeta("poach_failed_wk", -1);
            state.SetMeta("poach_failed_name", "");
            for (int i = 0; i < state.Employees.Count; i++)
            {
                if ((state.Employees[i].Name ?? "") != who) continue;
                string line = HardenAsk(state, i, MetaStr(state, "poach_rival", "a rival"));
                if (line.Length > 0) rep.Events.Add(line);
                return;
            }
        }

        static void MarkPoach(GameState state, string rival, string who)
        {
            state.SetMeta("poach_wk", state.Week);
            state.SetMeta("poach_rival", rival);
            state.SetMeta("poach_name", who);
        }

        // ═══════════════════════════ THE WEEKLY TICK ═════════════════════════

        /// <summary>Tick 3b: arrivals (salt 20/21), applicant decay (22), the
        /// review cycle with raise asks and resignations (23). The roster must be
        /// FINAL here: section 4 reads it for morale and section 9 pays it, so a
        /// quitter is off this week's payroll.</summary>
        public static void TickPre(GameState state, WeeklyReport rep)
        {
            rep.ApplicantsNew = 0;
            Arrivals(state, rep);
            Decay(state, rep);
            Ladder(state, rep);
            // THE FLOOR DRAGS WITHOUT MANAGERS — named, with the number, so the
            // fix reads as "hire a manager" and never as "hire more of everyone".
            double sm = SpanMult(state);
            if (sm < 1.0)
            {
                rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "span of control: {0} heads, {1} manager(s) — the floor runs at {2}%",
                    state.Employees.Count - ManagerCount(state), ManagerCount(state),
                    Gd.RoundToInt(sm * 100.0)));
            }
        }

        /// <summary>The money section. The severance was incurred at the desk and
        /// is booked HERE, one week later, so the week that pays it prints it.</summary>
        public static void TickMoney(GameState state, WeeklyReport rep, MoneyWork m)
        {
            int due = state.SeveranceDue;
            if (due > 0)
            {
                m.Severance += due;
                List<string> notes = SeveranceNotes(state);
                if (notes.Count == 0)
                {
                    rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "severance: ${0} — the invoice for letting someone go", Money(due)));
                }
                else
                {
                    for (int i = 0; i < notes.Count; i++) rep.Lines.Add(notes[i]);
                }
                state.SeveranceDue = 0;
                state.SetMeta("severance_notes", new List<string>());
            }
            int rc = RecruitersActive(state);
            if (rc > 0)
            {
                int cost = RecruiterFee * rc;
                m.Recruiting += cost;
                rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "recruiter on retainer: −${0}/wk (applicant flow ×{1:0.00})",
                    Money(cost), 1.0 + 0.75 * rc));
            }
        }

        /// <summary>Nothing in this lane needs the finished payroll.</summary>
        public static void TickPost(GameState state, WeeklyReport rep)
        {
        }

        // ── 3b(a) ARRIVALS ───────────────────────────────────────────────────
        static void Arrivals(GameState state, WeeklyReport rep)
        {
            if (state.OpenRoles.Count == 0) return;
            Rng r20 = SimEngine.RngForSalt(state, SimEngine.SALT_LABOR_ARRIVALS);
            Rng r21 = SimEngine.RngForSalt(state, SimEngine.SALT_LABOR_STATS);
            Rng r24 = SimEngine.RngForSalt(state, SimEngine.SALT_LABOR_POOLS);
            int born = 0;
            for (int ri = 0; ri < state.OpenRoles.Count; ri++)
            {
                OpenRole row = state.OpenRoles[ri];
                string role = row.Role ?? "engineer";
                int market = MarketSalary(role, state.Era);
                int offered = row.OfferedSalary;
                // Binomial(10, lambda/10): 0..10 arrivals with mean lambda —
                // Poisson-shaped weekly vacancy yield, capped so a runaway advert
                // cannot flood the desk.
                double p = Gd.Minf(ArrivalRate(state, row), 10.0) / 10.0;
                int count = 0;
                for (int i = 0; i < 10; i++)
                {
                    if (r20.Randf() < p) count += 1;
                }
                if (count <= 0) continue;
                double ratio = offered / Gd.Maxf(market, 1.0);
                for (int c = 0; c < count; c++)
                {
                    int sk = DrawSkill(r21, ratio);
                    int ask = ClampSalary(market,
                        Round10(market * AskMult(sk) * r21.RandfRange(0.90, 1.15)));
                    string src = "inbound";
                    // THE REFERRAL: a happy team is a recruiting channel, and a
                    // referred candidate takes a little less to join a shop
                    // somebody vouched for.
                    if (born == 0 && state.Morale >= 70)
                    {
                        src = "referral";
                        ask = Round10(ask * 0.95);
                    }
                    state.Applicants.Add(new Applicant
                    {
                        Name = PoolName(state, r24),
                        Role = role,
                        Skill = sk,
                        Ask = ask,
                        Quirk = QuirkPool[(int)(r24.Randi() % (uint)QuirkPool.Length)],
                        OneLiner = "",
                        AppliedWeek = state.Week,
                        Source = src,
                    });
                    born += 1;
                }
                rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0} applied for {1} (advert ${2} vs market ${3})",
                    count, role.ToUpperInvariant(), Money(offered), Money(market)));
            }
            rep.ApplicantsNew = born;
        }

        /// <summary>Overpay attracts talent: above 1.25x market a weak draw is
        /// rerolled once — a better offer improves the POOL, not only its size.</summary>
        static int DrawSkill(Rng rng, double ratio)
        {
            int sk = WeightedSkill(rng.Randf());
            if (sk <= 2 && ratio >= 1.25) sk = WeightedSkill(rng.Randf());
            return sk;
        }

        static int WeightedSkill(double u)
        {
            double acc = 0.0;
            for (int i = 0; i < SkillWeights.Length; i++)
            {
                acc += SkillWeights[i];
                if (u < acc) return i + 1;
            }
            return 5;
        }

        /// <summary>A name nobody in the building already answers to (5 tries,
        /// then it stands).</summary>
        static string PoolName(GameState state, Rng rng)
        {
            List<string> taken = TakenNames(state);
            string nm = "";
            for (int i = 0; i < 5; i++)
            {
                nm = WorldGen.PersonName(rng);
                if (!taken.Contains(nm)) return nm;
            }
            return nm;
        }

        public static List<string> TakenNames(GameState state)
        {
            var outv = new List<string>();
            if (!string.IsNullOrEmpty(state.FounderName)) outv.Add(state.FounderName);
            for (int i = 0; i < state.Cofounders.Count; i++) outv.Add(state.Cofounders[i].Name ?? "");
            for (int i = 0; i < state.Employees.Count; i++) outv.Add(state.Employees[i].Name ?? "");
            for (int i = 0; i < state.Pipeline.Count; i++) outv.Add(state.Pipeline[i].Name ?? "");
            for (int i = 0; i < state.Applicants.Count; i++) outv.Add(state.Applicants[i].Name ?? "");
            return outv;
        }

        // ── 3b(b) PATIENCE ───────────────────────────────────────────────────
        /// <summary>Candidate off-market decay: two weeks of grace, then a
        /// skill-weighted weekly roll (the good ones are holding competing
        /// offers), then a hard shelf-life. Real analogue: recruiting's "the best
        /// are gone in ten days", on a weekly tick.</summary>
        static void Decay(GameState state, WeeklyReport rep)
        {
            if (state.Applicants.Count == 0) return;
            Rng r22 = SimEngine.RngForSalt(state, SimEngine.SALT_LABOR_PATIENCE);
            var kept = new List<Applicant>();
            for (int i = 0; i < state.Applicants.Count; i++)
            {
                Applicant a = state.Applicants[i];
                int waiting = state.Week - a.AppliedWeek;
                bool gone = false;
                if (waiting >= PatienceCap)
                {
                    gone = true;                              // offer shelf-life, hard
                }
                else if (waiting > GraceWeeks)
                {
                    double p = 0.20 + 0.06 * SkillOf(a);
                    if (state.Era == "garage") p -= 0.05;     // scrappy joiners hold fewer offers
                    gone = r22.Randf() < p;
                }
                if (!gone)
                {
                    kept.Add(a);
                    continue;
                }
                string role = a.Role ?? "engineer";
                OpenRole row = OpenRoleRow(state, role);
                string line;
                if (row == null)
                {
                    line = string.Format(CultureInfo.InvariantCulture,
                        "{0} stopped waiting on {1} after {2} wks (the role closed under them)",
                        a.Name ?? "someone", role.ToUpperInvariant(), waiting);
                }
                else
                {
                    line = string.Format(CultureInfo.InvariantCulture,
                        "{0} stopped waiting on {1} after {2} wks (your advert: {3:0.00}× market)",
                        a.Name ?? "someone", role.ToUpperInvariant(), waiting,
                        row.OfferedSalary / Gd.Maxf(MarketSalary(role, state.Era), 1.0));
                }
                rep.Lines.Add(line);
                if (SkillOf(a) >= 4) rep.Events.Add(line);    // losing a good one is a beat
            }
            state.Applicants = kept;
        }

        // ── 3b(c) THE REVIEW CYCLE, THE ASKS, THE RESIGNATIONS ───────────────
        static void Ladder(GameState state, WeeklyReport rep)
        {
            ConsumeCounterOffer(state, rep);
            bool shortBenefits = BenefitsShort(state);
            if (shortBenefits)
            {
                // A REAL OFFICE EXPECTS BENEFITS: the office lever IS the
                // benefits budget (benefits are about 30% of comp at an
                // established firm). Unfunded, it costs morale and it brings
                // every pay conversation forward.
                rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "a real office expects benefits: office ${0} vs ${1} expected",
                    Money(state.Budgets.Office), Money(ExpectedBenefits(state))));
                state.Morale = Gd.Clampi(state.Morale - 1, 0, 100);
            }
            // THE COMP REVIEW (office up): real companies synchronise pay
            // conversations, which is why underpayment surfaces all at once.
            if (EraIdx(state.Era) >= 2 && state.Week % ReviewCycle == 0)
            {
                int compared = 0;
                for (int i = 0; i < state.Employees.Count; i++)
                {
                    Employee e = state.Employees[i];
                    if (e.Salary < FairPay(state, e) * 0.85)
                    {
                        compared += 1;
                        if (!e.WantsRaise)
                        {
                            e.WantsRaise = true;
                            e.AskedWeek = state.Week;
                        }
                    }
                }
                if (compared > 0)
                {
                    rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "review week: {0} people compare their pay to the market", compared));
                }
            }
            if (state.Employees.Count == 0) return;
            Rng r23 = SimEngine.RngForSalt(state, SimEngine.SALT_LABOR_LADDER);
            var kept = new List<Employee>();
            for (int i = 0; i < state.Employees.Count; i++)
            {
                Employee e = state.Employees[i];
                int fair = FairPay(state, e);
                int salary = e.Salary;
                double ratio = salary / Gd.Maxf(fair, 1.0);
                if (ratio < 0.85)
                {
                    if (e.UnderpaidSince < 0) e.UnderpaidSince = state.Week;   // the clock starts
                }
                else
                {
                    e.UnderpaidSince = -1;
                }
                if (!e.WantsRaise)
                {
                    bool asked = false;
                    if (ratio < 0.60)
                    {
                        // equity theory: insulting pay never waits for a review
                        asked = true;
                    }
                    else if (ratio < 0.85)
                    {
                        double p = 0.15;
                        if (state.Era == "garage") p *= 0.5;    // nobody benchmarks in a garage
                        if (shortBenefits) p += 0.05;
                        asked = r23.Randf() < p;
                    }
                    if (asked)
                    {
                        e.WantsRaise = true;
                        e.AskedWeek = state.Week;
                        rep.Events.Add(string.Format(CultureInfo.InvariantCulture,
                            "{0} wants a raise: ${1} now, market says ${2} ({3:0.00}×)",
                            e.Name ?? "someone", salary, fair, ratio));
                    }
                    kept.Add(e);
                    continue;
                }
                if (ratio >= 0.85)
                {
                    e.WantsRaise = false;                       // paid up: the ladder resets
                    e.AskedWeek = -1;
                    kept.Add(e);
                    continue;
                }
                // THE EFFICIENCY-WAGE QUIT FUNCTION: quit rates rise as the
                // relative wage falls, and the better they are the better their
                // outside option. Three weeks of being ignored is certain — by
                // then they are already interviewing.
                int since = state.Week - e.AskedWeek;
                bool quits = false;
                if (since >= 3) quits = true;
                else if (since >= 1) quits = r23.Randf() < 0.20 + 0.05 * SkillOf(e);
                if (!quits)
                {
                    kept.Add(e);
                    continue;
                }
                int weeks = e.UnderpaidSince >= 0
                    ? Gd.Maxi(state.Week - e.UnderpaidSince, 1) : Gd.Maxi(since, 1);
                rep.Events.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0} resigned: paid {1:0.00}× market for {2} weeks",
                    e.Name ?? "someone", ratio, weeks));
                state.Morale = Gd.Clampi(state.Morale - 6, 0, 100);   // no severance: they left
            }
            state.Employees = kept;
        }

        // ═════════════ section 4 SKILL ECONOMICS — the tick's own math ═══════
        // Within-occupation productivity dispersion is real and large (output SD
        // about 20-48% of the mean in complex work), which is the 1..5 linear
        // spread. Every function below returns EXACTLY the pre-wave number for a
        // skill-3 roster below floor era: `defaultV` is what the tick computed
        // before this lane existed. Drops: no team chemistry and no skill growth
        // on the job — a flat roster stays readable, training is a later wave.

        static double SkillSum(GameState state, string role)
        {
            double total = 0.0;
            for (int i = 0; i < state.Employees.Count; i++)
            {
                if ((state.Employees[i].Role ?? "").ToLowerInvariant().Contains(role))
                    total += SkillOf(state.Employees[i]);
            }
            return total;
        }

        /// <summary>SALES → CLOSING CAPACITY. Quota-carrying rep math: a skill-3
        /// seller is exactly the 3.0 the tick used to hardcode, a closer is 5.0,
        /// a bad hire is 1.0.</summary>
        public static double SalesCapacity(GameState state, double defaultV)
        {
            if (state.Employees.Count == 0) return defaultV;
            return SpanMult(state) * SkillSum(state, "sales");
        }

        /// <summary>DESIGNERS → ADOPTION POLISH. Design quality lifts conversion
        /// and word of mouth; capped at +30%, because polish is not a growth
        /// strategy.</summary>
        public static double DesignMult(GameState state)
        {
            if (state.Employees.Count == 0) return 1.0;
            return 1.0 + Gd.Minf(0.03 * SpanMult(state) * SkillSum(state, "designer"), 0.30);
        }

        /// <summary>SUPPORT → RETENTION. The service-profit chain: a skill-3
        /// support head is worth about $1,500/wk of care budget, and the existing
        /// 30% churn cap still caps.</summary>
        public static double CareEff(GameState state, double bCare)
        {
            if (state.Employees.Count == 0) return bCare;
            return bCare + SpanMult(state) * 500.0 * SkillSum(state, "support");
        }

        /// <summary>ENGINEERS → PRODUCT. A skill-3 engineer ships +0.5 quality a
        /// week with no budget at all, which is what an engineer IS.</summary>
        public static double RndGain(GameState state, double defaultV)
        {
            if (state.Employees.Count == 0) return defaultV;
            return defaultV + SpanMult(state) * SkillSum(state, "engineer") / 6.0;
        }

        /// <summary>ENGINEERS → DEBT. The same hands pay down what the same hands
        /// wrote.</summary>
        public static double DebtPaydown(GameState state, double defaultV)
        {
            if (state.Employees.Count == 0) return defaultV;
            return defaultV + SpanMult(state) * SkillSum(state, "engineer") * 0.10;
        }

        /// <summary>OPS → THE UNFORESEEN. Operational maturity cuts unplanned
        /// cost, floored at -60%. Deliberately NOT span-damped: firefighting does
        /// not need a manager.</summary>
        public static double OpsMult(GameState state)
        {
            if (state.Employees.Count == 0) return 1.0;
            return Gd.Maxf(0.4, 1.0 - 0.08 * SkillSum(state, "ops"));
        }

        // ═══════════════════ DM CONTEXT AND ATTENTION ════════════════════════

        /// <summary>DM context lines, section 6 of the DIRECTIVES block. The DM
        /// narrates these receipts; it never re-prices them.</summary>
        public static List<string> Directives(GameState state)
        {
            var outv = new List<string>();
            for (int ri = 0; ri < state.OpenRoles.Count; ri++)
            {
                OpenRole row = state.OpenRoles[ri];
                string role = row.Role ?? "engineer";
                int waiting = WaitingFor(state, role);
                int bestSkill = 0;
                int bestAsk = 0;
                for (int i = 0; i < state.Applicants.Count; i++)
                {
                    Applicant a = state.Applicants[i];
                    if ((a.Role ?? "") == role && SkillOf(a) > bestSkill)
                    {
                        bestSkill = SkillOf(a);
                        bestAsk = a.Ask;
                    }
                }
                if (waiting > 0)
                {
                    outv.Add(string.Format(CultureInfo.InvariantCulture,
                        "- Hiring: {0} applicants for {1} (best: skill {2}, asks ${3}/wk).",
                        waiting, role, bestSkill, bestAsk));
                }
                else
                {
                    outv.Add(string.Format(CultureInfo.InvariantCulture,
                        "- Hiring: {0} advertised at ${1} (market ${2}) and nobody has applied.",
                        role, row.OfferedSalary, MarketSalary(role, state.Era)));
                }
            }
            if (Gd.ToInt(state.GetMetaF("poach_wk", -99.0)) == state.Week)
            {
                outv.Add(string.Format(CultureInfo.InvariantCulture,
                    "- POACH: {0} is courting {1} (${2}/wk, underpaid).",
                    MetaStr(state, "poach_rival", "a rival"), MetaStr(state, "poach_name", "someone"),
                    PoachedSalary(state)));
            }
            for (int i = 0; i < state.Employees.Count; i++)
            {
                Employee e = state.Employees[i];
                if (e.WantsRaise)
                {
                    outv.Add(string.Format(CultureInfo.InvariantCulture,
                        "- {0} ({1}) wants a raise; refusing much longer risks a resignation.",
                        e.Name ?? "someone", e.Role ?? "engineer"));
                }
            }
            if (BenefitsShort(state))
            {
                outv.Add(string.Format(CultureInfo.InvariantCulture,
                    "- The office expects benefits: the office lever is ${0} for {1} staff.",
                    state.Budgets.Office, state.Employees.Count + state.Pipeline.Count));
            }
            return outv;
        }

        static int PoachedSalary(GameState state)
        {
            string nm = MetaStr(state, "poach_name", "");
            for (int i = 0; i < state.Employees.Count; i++)
            {
                if ((state.Employees[i].Name ?? "") == nm) return state.Employees[i].Salary;
            }
            return 0;
        }

        static string MetaStr(GameState state, string key, string dflt)
        {
            object v = state.GetMeta(key, null);
            if (v == null) return dflt;
            string s = v.ToString();
            return s.Length > 0 ? s : dflt;
        }

        /// <summary>Attention rows — the crew desk. Every label is what the
        /// garage ticker prints verbatim, so it names the business problem and
        /// never the mechanic.</summary>
        public static List<AttentionItem> Attention(GameState state)
        {
            var outv = new List<AttentionItem>();
            if (state.Applicants.Count > 0)
            {
                outv.Add(new AttentionItem { Desk = "crew", Key = "applicants_waiting", Severity = 1,
                    Label = Trunc(string.Format(CultureInfo.InvariantCulture,
                        "{0} waiting on your advert", state.Applicants.Count), 40) });
            }
            string wanter = "";
            string leaving = "";
            for (int i = 0; i < state.Employees.Count; i++)
            {
                Employee e = state.Employees[i];
                if (!e.WantsRaise) continue;
                if (wanter.Length == 0) wanter = e.Name ?? "someone";
                if (leaving.Length == 0 && state.Week - e.AskedWeek >= 2) leaving = e.Name ?? "someone";
            }
            if (wanter.Length > 0)
            {
                outv.Add(new AttentionItem { Desk = "crew", Key = "wants_raise", Severity = 2,
                    Label = Trunc(wanter + " wants market pay", 40) });
            }
            if (leaving.Length > 0)
            {
                outv.Add(new AttentionItem { Desk = "crew", Key = "quit_risk", Severity = 3,
                    Label = Trunc(leaving + " resigns next week unpaid", 40) });
            }
            for (int ri = 0; ri < state.OpenRoles.Count; ri++)
            {
                OpenRole row = state.OpenRoles[ri];
                string role = row.Role ?? "engineer";
                int age = state.Week - row.OpenedWeek;
                if (age >= StaleWeeks && WaitingFor(state, role) == 0)
                {
                    outv.Add(new AttentionItem { Desk = "crew", Key = "silent_role", Severity = 2,
                        Label = Trunc(string.Format(CultureInfo.InvariantCulture,
                            "{0} open {1} wks, nobody applied", role, age), 40) });
                    break;
                }
            }
            if (SpanMult(state) < 1.0)
            {
                outv.Add(new AttentionItem { Desk = "crew", Key = "span_thin", Severity = 2,
                    Label = Trunc(string.Format(CultureInfo.InvariantCulture,
                        "the floor runs at {0}% — too few managers",
                        Gd.RoundToInt(SpanMult(state) * 100.0)), 40) });
            }
            if (Gd.ToInt(state.GetMetaF("poach_wk", -99.0)) == state.Week)
            {
                outv.Add(new AttentionItem { Desk = "crew", Key = "poach_attempt", Severity = 3,
                    Label = Trunc("a rival is courting "
                        + MetaStr(state, "poach_name", "your best"), 40) });
            }
            return outv;
        }

        // ═══════════════ THE LLM DRESSING SEAM (spec section 8.1) ════════════
        // Applicants are BORN playable with pool names and quirks. The one batch
        // call replaces dressing fields IN PLACE when it lands; it never touches
        // a number, and a reply that does not match the week's arrivals is
        // discarded whole. The payload crosses an assembly boundary, so it
        // travels as JSON — the LLM assembly never sees a Core type.

        /// <summary>The user payload for the dressing call, or null when nobody
        /// arrived this week (no arrivals → no call fires).</summary>
        public static JObject DressingPayload(GameState state)
        {
            var fresh = new JArray();
            for (int i = 0; i < state.Applicants.Count; i++)
            {
                Applicant a = state.Applicants[i];
                if (a.AppliedWeek != state.Week) continue;
                fresh.Add(new JObject
                {
                    { "role", a.Role ?? "engineer" }, { "skill", SkillOf(a) },
                    { "ask", a.Ask }, { "source", a.Source ?? "inbound" },
                });
            }
            if (fresh.Count == 0) return null;
            var team = new JArray();
            for (int i = 0; i < state.Employees.Count; i++) team.Add(state.Employees[i].Name ?? "");
            var taken = new JArray();
            List<string> names = TakenNames(state);
            for (int i = 0; i < names.Count; i++) taken.Add(names[i]);
            return new JObject
            {
                { "company", new JObject {
                    { "name", state.CompanyName ?? "" }, { "idea", state.CompanyIdea ?? "" },
                    { "what", state.BizWhat ?? "" }, { "who", state.BizWho ?? "" },
                    { "era", state.Era ?? "garage" } } },
                { "team", team },
                { "taken_names", taken },
                { "candidates", fresh },
            };
        }

        /// <summary>Land a reply. Returns how many candidates were dressed; 0
        /// means the whole reply was discarded and the pool dressing stands —
        /// which is a complete card either way, so nothing is ever waiting.</summary>
        public static int DressApplicants(GameState state, JArray rows)
        {
            var fresh = new List<int>();
            for (int i = 0; i < state.Applicants.Count; i++)
            {
                if (state.Applicants[i].AppliedWeek == state.Week) fresh.Add(i);
            }
            if (rows == null || fresh.Count == 0 || rows.Count != fresh.Count) return 0;
            List<string> taken = TakenNames(state);
            for (int i = 0; i < fresh.Count; i++)
            {
                var row = rows[i] as JObject;
                if (row == null) return 0;
                string nm = RowStr(row, "name");
                if (nm.Length == 0) return 0;
                string own = state.Applicants[fresh[i]].Name ?? "";
                if (nm != own && taken.Contains(nm)) return 0;
            }
            for (int i = 0; i < fresh.Count; i++)
            {
                var row = (JObject)rows[i];
                Applicant a = state.Applicants[fresh[i]];
                a.Name = Trunc(RowStr(row, "name"), 40);
                string q = RowStr(row, "quirk");
                if (q.Length > 0) a.Quirk = Trunc(q, 60);
                a.OneLiner = Trunc(RowStr(row, "one_liner"), 90);
            }
            return fresh.Count;
        }

        static string RowStr(JObject row, string key)
        {
            JToken t = row[key];
            if (t == null || t.Type == JTokenType.Null) return "";
            return (t.ToString() ?? "").Trim();
        }

        // ───────────────────────────── small hands ───────────────────────────

        /// <summary>The severance receipts waiting to be printed by the next
        /// money section. Stored as finished lines so a save round-trip never
        /// loses the arithmetic that produced them.</summary>
        static List<string> SeveranceNotes(GameState state)
        {
            var outv = new List<string>();
            object v = state.GetMeta("severance_notes", null);
            var asList = v as List<string>;
            if (asList != null) return new List<string>(asList);
            var asArr = v as JArray;
            if (asArr != null)
            {
                for (int i = 0; i < asArr.Count; i++) outv.Add(asArr[i].ToString());
            }
            return outv;
        }

        static void AddSeveranceNote(GameState state, string line)
        {
            List<string> notes = SeveranceNotes(state);
            notes.Add(line);
            state.SetMeta("severance_notes", notes);
        }

        static int Round10(double v)
        {
            return Gd.RoundToInt(v / 10.0) * 10;
        }

        static string Trunc(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= n ? s : s.Substring(0, n);
        }

        /// <summary>The desk's money hand, so a receipt and a card read the same
        /// number the same way.</summary>
        public static string Money(int n)
        {
            string s = Gd.Absi(n).ToString(CultureInfo.InvariantCulture);
            string outv = "";
            while (s.Length > 3)
            {
                outv = "," + s.Substring(s.Length - 3) + outv;
                s = s.Substring(0, s.Length - 3);
            }
            return (n < 0 ? "-" : "") + s + outv;
        }
    }
}
