using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `crew` tab. Spec: docs/design/02-labor-market.md section 7
    ///
    /// BinderScreen dispatches the tab body here and passes ITSELF, so this file
    /// draws through the binder's own helpers and never reaches into the sheet
    /// directly.
    ///
    /// THREE PAGES, ONE DESK (the ruling in 00-spine section 11: the roster and
    /// the hiring half cannot share one 760px sheet):
    ///   ROSTER  the people you have — what each one costs FULLY LOADED, who is
    ///           about to walk, and the payroll total underneath
    ///   PERSON  one person's whole page: pay against the market, the raise
    ///           stepper, and the let-go arm with the severance invoice in its
    ///           own caption
    ///   HIRING  the roles you advertise, the rate you advertise them at, and the
    ///           people that rate brings in
    /// The pen toggle in the header moves between roster and hiring; Esc walks
    /// PERSON to ROSTER before it ever closes the binder (the shared contract).
    ///
    /// THE DESK TEACHES: MARKET RATE beside every advert, FULLY-LOADED beside
    /// every salary, SEVERANCE inside the button that charges it. Nothing here
    /// recomputes a rule — every number comes from SimLabor, so the desk and the
    /// engine can never disagree about what a head costs.
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_crew.gd draw the same rows
    /// at the same coordinates.
    /// </summary>
    public static class DeskCrew
    {
        const float RowH = 66f;          // one person
        const float CoH = 56f;           // one cofounder, or the founder
        const float PipeH = 48f;         // one hire still onboarding
        const float BodyTop = 92f;
        const float BodyBottom = 596f;
        const float PipsX = 770f;

        /// <summary>Draw the crew desk.</summary>
        public static void Draw(BinderScreen b)
        {
            string mode = Mode(b);
            if (mode == "person") PagePerson(b);
            else if (mode == "hiring") PageHiring(b);
            else PageRoster(b);
        }

        /// <summary>A press inside this desk. `id` is whatever Draw registered.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }

        static string Mode(BinderScreen b)
        {
            object v;
            if (b.Desk.TryGetValue("mode", out v) && v != null) return v.ToString();
            return "";
        }

        static int Row(BinderScreen b)
        {
            object v;
            if (b.Desk.TryGetValue("row", out v) && v != null)
            {
                int i;
                if (int.TryParse(v.ToString(), out i)) return i;
            }
            return -1;
        }

        // ═══════════════════════════ THE HEADER ══════════════════════════════

        /// <summary>The desk's name and the pen toggle between its two halves.
        /// Both words are always live, so the half you are not looking at is
        /// never hidden — it is simply the dim one.</summary>
        static float Head(BinderScreen b, string titleText, string mode)
        {
            float y = DeskKit.Title(b, titleText);
            DeskKit.Word(b, "roster", 830f, 14f, () =>
            {
                b.Desk["mode"] = "";
                b.Desk.Remove("row");
            }, DeskKit.Status,
                mode != "hiring" ? DrawnUI.Ink : DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), 160f);
            DeskKit.Word(b, "hiring", 995f, 14f, () =>
            {
                b.Desk["mode"] = "hiring";
                b.Desk.Remove("row");
            }, DeskKit.Status,
                mode == "hiring" ? DrawnUI.Ink : DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), 160f);
            return y;
        }

        // ═══════════════════════════ PAGE: ROSTER ════════════════════════════

        static void PageRoster(BinderScreen b)
        {
            GameState st = b.State;
            Head(b, "crew — the people, and what they cost", "roster");
            float y = BodyTop;
            // THE FOUNDER IS ON THE ROSTER. They are the first head this company
            // ever paid for, and the competences line is what they are good at.
            b.Icon("you", 10f, y - 4f, 44f);
            string who = (st.FounderName ?? "").Length > 0 ? st.FounderName : "the founder";
            b.L(string.Format("{0} — founder · lvl {1} · exhaustion {2}/6", who, st.Level,
                st.Exhaustion), 66f, y, DeskKit.Row, DrawnUI.Ink, 900f);
            var stats = new List<string>();
            for (int i = 0; i < FounderDraftScreen.StatNames.Length; i++)
            {
                stats.Add(FounderDraftScreen.StatNames[i] + " "
                          + st.Competence(FounderDraftScreen.StatNames[i]));
            }
            b.L(string.Join("  ·  ", stats.ToArray()), 66f, y + 30f, DeskKit.Detail,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 900f);
            y += CoH;
            RunDriver driver = RunDriver.Current;
            for (int i = 0; i < st.Cofounders.Count; i++)
            {
                Cofounder cf = st.Cofounders[i];
                b.Icon("cofd_tech", 10f, y - 4f, 44f);
                string nm = (cf.Name ?? "").Trim();
                b.L(string.Format("{0}{1} cofounder · {2:0}% equity · not on payroll",
                    nm.Length > 0 ? nm + " — " : "", cf.Role,
                    cf.EquityDiluted.HasValue ? cf.EquityDiluted.Value : cf.Equity),
                    66f, y + 4f, DeskKit.Row, DrawnUI.Ink, 1000f);
                y += CoH;
            }
            // HQ GROUPS INTO DEPARTMENTS. At a 40-head cap a flat list is
            // unreadable, and the subtotal is the number a founder at that size
            // actually thinks in.
            if (SimLabor.EraIdx(st.Era) >= 4 && st.Employees.Count > 0)
            {
                y = DeskKit.Rule(b, y + 2f);
                for (int r = 0; r < SimLabor.RoleOrder.Length; r++)
                {
                    string role = SimLabor.RoleOrder[r];
                    int heads = 0;
                    int cost = 0;
                    for (int i = 0; i < st.Employees.Count; i++)
                    {
                        if (SimLabor.RoleRow(st.Employees[i].Role ?? "") != role) continue;
                        heads += 1;
                        cost += st.Employees[i].Salary;
                    }
                    if (heads <= 0) continue;
                    b.L(string.Format("{0} — {1} heads · ${2}/wk", Dept(role), heads,
                        GameUi.Money(cost)), 10f, y, DeskKit.Status,
                        DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 900f);
                    y += 34f;
                }
                y += 6f;
            }
            if (st.Employees.Count == 0 && st.Pipeline.Count == 0)
            {
                y = DeskKit.Empty(b, 10f, y,
                    "nobody on the payroll but you. every hire is a weekly bill that starts before the work does.",
                    "the hiring half of this desk posts a role — what you advertise against the MARKET RATE decides who answers.");
            }
            // THE LOUDEST PEOPLE SORT TO THE TOP: whoever is asking, then whoever
            // is furthest under market. A quiet roster needs no buttons, so the
            // cap never hides a decision.
            List<int> order = RosterOrder(st);
            int room = Gd.Clampi((int)((BodyBottom - y) / RowH), 1, DeskKit.ListCap);
            int shown = 0;
            for (int i = 0; i < order.Count && shown < room; i++)
            {
                y = RosterRow(b, st, order[i], y);
                shown += 1;
            }
            if (shown < order.Count)
            {
                y = DeskKit.More(b, 10f, y, order.Count - shown, "on the payroll, all quiet");
            }
            for (int i = 0; i < st.Pipeline.Count; i++)
            {
                if (y > BodyBottom - PipeH) break;
                PipelineHire h = st.Pipeline[i];
                b.Icon("employee", 10f, y - 6f, 44f);
                b.L(string.Format("{0} — {1} · ONBOARDING (paid ${2}/wk, productive in {3} wk)",
                    h.Name, h.Role, GameUi.Money(h.Salary), Gd.Maxi(2 - h.WeeksIn, 1)),
                    66f, y + 2f, DeskKit.Status, DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 1000f);
                y += PipeH;
            }
            DeskKit.Spark(b, "morale", 600f, 600f, 560f, 78f, DrawnUI.Sage,
                "morale, drawn weekly:");
            // THE TWO NUMBERS THAT MATTER, side by side: what you think a team
            // costs, and what it actually costs once the room is counted.
            DeskKit.Footer(b, string.Format(
                "payroll ${0}/wk  ·  FULLY-LOADED ${1}/wk (salary + rent, infra and the office lever, split per head)",
                GameUi.Money(SimLabor.PayrollWk(st)), GameUi.Money(SimLabor.LoadedPayrollWk(st))),
                Rules(st), Warning(st));
        }

        /// <summary>Whoever needs a decision first: the askers, then the furthest
        /// under market.</summary>
        static List<int> RosterOrder(GameState st)
        {
            var idx = new List<int>();
            for (int i = 0; i < st.Employees.Count; i++) idx.Add(i);
            idx.Sort((x, y) =>
            {
                Employee ex = st.Employees[x];
                Employee ey = st.Employees[y];
                int ax = ex.WantsRaise ? 1 : 0;
                int ay = ey.WantsRaise ? 1 : 0;
                if (ax != ay) return ay - ax;
                double rx = ex.Salary / Gd.Maxf(SimLabor.FairPay(st, ex), 1.0);
                double ry = ey.Salary / Gd.Maxf(SimLabor.FairPay(st, ey), 1.0);
                if (Gd.Absf(rx - ry) > 0.0001) return rx < ry ? -1 : 1;
                return x - y;
            });
            return idx;
        }

        static float RosterRow(BinderScreen b, GameState st, int idx, float y)
        {
            Employee e = st.Employees[idx];
            int salary = e.Salary;
            int fair = SimLabor.FairPay(st, e);
            b.Icon("employee", 10f, y - 6f, 44f);
            b.L(string.Format("{0} — {1} · ${2}/wk (${3} loaded)", e.Name, e.Role,
                GameUi.Money(salary), GameUi.Money(SimLabor.LoadedCost(st, salary))),
                66f, y, DeskKit.Row, DrawnUI.Ink, 690f);
            DeskKit.Pips(b, PipsX, y + 10f, SimLabor.SkillOf(e));
            if (e.WantsRaise)
            {
                // THE INLINE CORAL MARK (2.8): the subject carries its own
                // warning, with the gap in numbers, so refusing is an informed
                // bet and not an oversight.
                b.L(string.Format("! wants market pay — ${0} now against ${1} fair ({2:0.00}×)",
                    GameUi.Money(salary), GameUi.Money(fair), salary / Gd.Maxf(fair, 1.0)),
                    66f, y + 32f, DeskKit.Detail, DrawnUI.Coral, 690f);
                int captured = idx;
                int pay = salary;
                DeskKit.Word(b, "+10%", DeskKit.XMinus, y,
                    () => SimLabor.GrantRaise(st, captured, Gd.RoundToInt(pay * 1.1)),
                    DeskKit.Status, DrawnUI.Ink, 140f);
            }
            else
            {
                string quirk = (e.Quirk ?? "").Trim();
                string tail = quirk.Length > 0 ? " · \"" + quirk + "\"" : "";
                b.L(string.Format("burnout {0} · {1} wks here · paid {2:0.00}× fair{3}",
                    e.Burnout, SimLabor.TenureOf(st, e), salary / Gd.Maxf(fair, 1.0), tail),
                    66f, y + 32f, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), 690f);
            }
            int open = idx;
            DeskKit.Expand(b, DeskKit.XExpand, y, () =>
            {
                b.Desk["mode"] = "person";
                b.Desk["row"] = open;
            });
            return y + RowH;
        }

        static string Dept(string role)
        {
            switch (role)
            {
                case "engineer": return "ENGINEERING";
                case "sales": return "SALES";
                case "designer": return "DESIGN";
                case "ops": return "OPERATIONS";
                case "support": return "SUPPORT";
                case "manager": return "MANAGEMENT";
            }
            return role.ToUpperInvariant();
        }

        // ═══════════════════════════ PAGE: PERSON ════════════════════════════

        static void PagePerson(BinderScreen b)
        {
            GameState st = b.State;
            int idx = Row(b);
            if (idx < 0 || idx >= st.Employees.Count)
            {
                b.Desk["mode"] = "";
                b.Desk.Remove("row");
                PageRoster(b);
                return;
            }
            Employee e = st.Employees[idx];
            DeskKit.Back(b, "◂ everyone", () =>
            {
                b.Desk["mode"] = "";
                b.Desk.Remove("row");
            });
            int salary = e.Salary;
            int market = SimLabor.MarketSalary(e.Role ?? "", st.Era);
            int fair = SimLabor.FairPay(st, e);
            double ratio = salary / Gd.Maxf(fair, 1.0);
            int tenure = SimLabor.TenureOf(st, e);
            b.L(string.Format("{0} — {1}", e.Name, e.Role), 10f, 62f, DeskKit.TitleSize,
                DrawnUI.Ink, 700f);
            DeskKit.Pips(b, 740f, 78f, SimLabor.SkillOf(e));
            b.L("skill " + SimLabor.SkillOf(e) + " of 5", 870f, 70f, DeskKit.Detail,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 260f);
            float y = 124f;
            b.L(string.Format(
                "${0}/wk on the payroll  ·  ${1}/wk FULLY LOADED  ·  {2} wks here  ·  burnout {3}",
                GameUi.Money(salary), GameUi.Money(SimLabor.LoadedCost(st, salary)), tenure,
                e.Burnout), 10f, y, DeskKit.Status, DrawnUI.Blue, 1100f);
            y += 40f;
            TextMeshProUGUI anchor = b.L(string.Format(
                "the MARKET RATE for {0} at this stage is ${1}/wk. Skill {2} asks ×{3:0.00} of it, so FAIR PAY here is ${4}/wk.",
                RoleNoun(e.Role ?? ""), GameUi.Money(market), SimLabor.SkillOf(e),
                SimLabor.AskMult(SimLabor.SkillOf(e)), GameUi.Money(fair)),
                10f, y, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f), 1100f);
            y += Mathf.Max(BinderScreen.Height(anchor), 28f) + 12f;
            if (ratio < 0.85)
            {
                string howLong = e.UnderpaidSince >= 0
                    ? string.Format(" for {0} wks now", Gd.Maxi(st.Week - e.UnderpaidSince, 0)) : "";
                b.L(string.Format(
                    "you pay {0:0.00}× fair{1} — under 0.85× the asks start, and they compound into resignations.",
                    ratio, howLong), 10f, y, DeskKit.Status, DrawnUI.Coral, 1100f);
            }
            else
            {
                b.L(string.Format(
                    "you pay {0:0.00}× fair — at or above the market band, nobody is counting the days.",
                    ratio), 10f, y, DeskKit.Status, DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f), 1100f);
            }
            y += 44f;
            if (e.WantsRaise)
            {
                int left = Gd.Maxi(3 - (st.Week - e.AskedWeek), 0);
                string clock = left <= 0
                    ? "they resign at the end of this week unless the pay moves."
                    : string.Format("about {0} wk{1} of patience left at this number.",
                        left, left == 1 ? "" : "s");
                b.L("! they have asked. " + clock, 10f, y, DeskKit.Status, DrawnUI.Coral, 1100f);
                y += 44f;
            }
            y += 10f;
            // THE RAISE, ON THE HOUSE LADDER. The engine re-clamps on write, so
            // the stepper cannot walk anywhere the world does not allow.
            List<double> steps = SimLabor.SalarySteps(market, 2.5);
            bool atMin = DeskKit.AtMin(steps, salary);
            bool atMax = DeskKit.AtMax(steps, salary);
            int who = idx;
            y = DeskKit.Stepper(b, y, new DeskKit.StepRow
            {
                Name = "their salary",
                Why = "clamped to the market band: 0.5× to 2.5× the going rate for the role",
                Value = "$" + GameUi.Money(salary) + "/wk",
                Effect = RaiseEffect(salary, fair),
                AtMin = atMin,
                AtMax = atMax,
                Bound = atMin ? "the band's floor" : (atMax ? "the band's ceiling" : ""),
                OnMinus = () => SimLabor.GrantRaise(st, who,
                    (int)DeskKit.Ladder(steps, salary, -1)),
                OnPlus = () => SimLabor.GrantRaise(st, who,
                    (int)DeskKit.Ladder(steps, salary, 1)),
            });
            y += 20f;
            // LETTING GO: the invoice is the confirmation. Nothing else on the
            // sheet changes on the first press — the words become the price.
            int owed = SimLabor.SeveranceFor(st, e);
            int weeks = SimLabor.SeveranceWeeks(st.Era, tenure);
            DeskKit.Arm(b, "letgo_" + idx, "let go",
                "owe $" + GameUi.Money(owed) + " severance — sure?", 10f, y, () =>
                {
                    SimLabor.FireEmployee(st, who);
                    b.Desk["mode"] = "";
                    b.Desk.Remove("row");
                }, 700f);
            y += 52f;
            b.L(string.Format(
                "SEVERANCE at this stage is {0} wk{1} of salary for {2} wks of tenure — ${3}, booked to next week's ledger. They leave now; the bill does not.",
                weeks, weeks == 1 ? "" : "s", tenure, GameUi.Money(owed)),
                10f, y, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 1100f);
            DeskKit.Footer(b, string.Format(
                "one head here costs ${0}/wk fully loaded — {1:0}% more than the salary alone",
                GameUi.Money(SimLabor.LoadedCost(st, salary)),
                (SimLabor.LoadedCost(st, salary) / Gd.Maxf(salary, 1.0) - 1.0) * 100.0),
                Rules(st), Warning(st));
        }

        static string RaiseEffect(int salary, int fair)
        {
            double ratio = salary / Gd.Maxf(fair, 1.0);
            if (ratio >= 0.95)
                return string.Format("{0:0.00}× fair — the ask clears at 0.95×", ratio);
            if (ratio >= 0.85)
                return string.Format("{0:0.00}× fair — inside the band, no asks", ratio);
            if (ratio >= 0.60)
                return string.Format("{0:0.00}× fair — they may start asking", ratio);
            return string.Format("{0:0.00}× fair — insulting; the ask is immediate", ratio);
        }

        // ═══════════════════════════ PAGE: HIRING ════════════════════════════

        static void PageHiring(BinderScreen b)
        {
            GameState st = b.State;
            float y = Head(b, "crew — hiring against the market rate", "hiring");
            if (!SimLabor.MarketOpen(st.Era))
            {
                // THE ERA GATE, TAUGHT rather than greyed out: a garage does not
                // post jobs, it asks the people it already knows.
                DeskKit.Empty(b, 10f, y, "nobody answers an advert taped to a garage door.",
                    "the market opens when you do — a desk somewhere other than your kitchen is what makes a role worth applying to. Until then the people you get are the people you already know.");
                DeskKit.Footer(b, "", Rules(st), Warning(st));
                return;
            }
            y += 4f;
            if (st.OpenRoles.Count == 0)
            {
                y = DeskKit.Empty(b, 10f, y,
                    "nobody is hiring. open a role and the street starts sending people —",
                    "the advert against the MARKET RATE decides how many.");
                y += 6f;
            }
            for (int i = 0; i < st.OpenRoles.Count; i++)
            {
                y = RoleRow(b, st, st.OpenRoles[i], y);
            }
            y = OpenLine(b, st, y);
            y = RecruiterRow(b, st, y);
            y = DeskKit.Rule(b, y + 2f);
            // THE PEOPLE THE ADVERT BROUGHT IN. Six cards, then the rest.
            if (st.Applicants.Count == 0)
            {
                if (st.OpenRoles.Count > 0)
                {
                    y = DeskKit.Empty(b, 10f, y, "nobody has answered yet.",
                        "an advert under 0.8× the market rate draws silence, not bargains.");
                }
            }
            else
            {
                int room = Gd.Clampi((int)((690f - y) / RowH), 1, DeskKit.ListCap);
                int shown = 0;
                for (int i = 0; i < st.Applicants.Count && shown < room; i++)
                {
                    y = ApplicantCard(b, st, i, y);
                    shown += 1;
                }
                if (shown < st.Applicants.Count)
                {
                    y = DeskKit.More(b, 10f, y, st.Applicants.Count - shown);
                }
            }
            DeskKit.Footer(b, HiringComputed(st), Rules(st), Warning(st));
        }

        static float RoleRow(BinderScreen b, GameState st, OpenRole row, float y)
        {
            string role = row.Role ?? "engineer";
            int market = SimLabor.MarketSalary(role, st.Era);
            int offered = row.OfferedSalary;
            double ratio = offered / Gd.Maxf(market, 1.0);
            int waiting = SimLabor.WaitingFor(st, role);
            double lam = SimLabor.ArrivalRate(st, row);
            List<double> steps = SimLabor.SalarySteps(market, 2.0);
            bool thin = ratio < 0.8;
            string flow = lam < 0.05
                ? "nobody applies at this rate"
                : string.Format("about {0:0.0} apply/wk", lam);
            DeskKit.Stepper(b, y, new DeskKit.StepRow
            {
                Name = role + " — advert",
                Value = "$" + GameUi.Money(offered) + "/wk",
                Effect = string.Format("market ${0} · {1:0.00}× · {2} · {3} waiting",
                    GameUi.Money(market), ratio, flow, waiting),
                AtMin = DeskKit.AtMin(steps, offered),
                AtMax = DeskKit.AtMax(steps, offered),
                Pitch = 0f,
                OnMinus = () => SimLabor.SetRoleSalary(st, role,
                    (int)DeskKit.Ladder(steps, offered, -1)),
                OnPlus = () => SimLabor.SetRoleSalary(st, role,
                    (int)DeskKit.Ladder(steps, offered, 1)),
            });
            if (thin)
            {
                // THE ENGINE'S OWN READ, printed before the player blames the
                // game for a week of silence: below 0.8× the market rate the
                // applicant flow is ZERO.
                b.L(string.Format("{0:0.00}× market — under 0.8× nobody applies at all.", ratio),
                    10f, y + 34f, DeskKit.Detail, DrawnUI.Coral, 460f);
            }
            else
            {
                b.L("the advert against the MARKET RATE decides who answers",
                    10f, y + 34f, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 460f);
            }
            DeskKit.Word(b, "close the role", 480f, y + 24f,
                () => SimLabor.CloseRole(st, role), DeskKit.Detail,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 190f);
            y += 92f;
            if (SimLabor.SeatCap(st.Era) > 1)
            {
                // HQ: one role row is a requisition batch, so the arrivals keep
                // coming until the seats are filled.
                int seats = row.Seats;
                y = DeskKit.Stepper(b, y, new DeskKit.StepRow
                {
                    Name = "seats on it",
                    Why = "one advert, several desks",
                    Value = seats.ToString(),
                    Effect = string.Format("{0} hire{1} before this role closes itself",
                        seats, seats == 1 ? "" : "s"),
                    AtMin = seats <= 1,
                    AtMax = seats >= SimLabor.SeatCap(st.Era),
                    Pitch = 76f,
                    OnMinus = () => SimLabor.SetSeats(st, role, seats - 1),
                    OnPlus = () => SimLabor.SetSeats(st, role, seats + 1),
                });
            }
            return y;
        }

        /// <summary>The one trailing line that opens what this era allows — and
        /// says plainly when there is no desk left to open anything for.</summary>
        static float OpenLine(BinderScreen b, GameState st, float y)
        {
            if (SimLabor.SeatsLeft(st) <= st.OpenRoles.Count)
            {
                b.L(string.Format(
                    "no desk left to open a role — this stage seats {0}, and every one is spoken for.",
                    st.StaffCap()), 10f, y, DeskKit.Detail,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 1100f);
                return y + 40f;
            }
            b.L("+ open:", 10f, y, DeskKit.Status, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 120f);
            float x = 130f;
            for (int i = 0; i < SimLabor.RoleOrder.Length; i++)
            {
                string r = SimLabor.RoleOrder[i];
                if (!SimLabor.RoleUnlocked(r, st.Era)) continue;
                if (SimLabor.OpenRoleRow(st, r) != null) continue;
                string role = r;
                DeskKit.Word(b, role, x, y - 8f,
                    () => SimLabor.OpenRoleAt(st, role, SimLabor.MarketSalary(role, st.Era)),
                    DeskKit.Status, DrawnUI.Ink, 160f);
                x += 168f;
                if (x > 980f) break;
            }
            return y + 50f;
        }

        static float RecruiterRow(BinderScreen b, GameState st, float y)
        {
            int cap = SimLabor.RecruiterCap(st.Era);
            if (cap <= 0) return y;
            int n = SimLabor.RecruitersActive(st);
            return DeskKit.Stepper(b, y, new DeskKit.StepRow
            {
                Name = "recruiters on retainer",
                Why = "applicant flow, bought by the week",
                Value = string.Format("{0} × ${1}/wk", n, GameUi.Money(SimLabor.RecruiterFee)),
                Effect = n > 0
                    ? string.Format("applicant flow ×{0:0.00} · −${1}/wk on the books",
                        1.0 + 0.75 * n, GameUi.Money(SimLabor.RecruiterFee * n))
                    : "nobody is working your pipeline",
                AtMin = n <= 0,
                AtMax = n >= cap,
                Pitch = 78f,
                OnMinus = () => SimLabor.SetRecruiters(st, n - 1),
                OnPlus = () => SimLabor.SetRecruiters(st, n + 1),
            });
        }

        static float ApplicantCard(BinderScreen b, GameState st, int i, float y)
        {
            Applicant a = st.Applicants[i];
            string role = a.Role ?? "engineer";
            int market = SimLabor.MarketSalary(role, st.Era);
            int waiting = Gd.Maxi(st.Week - a.AppliedWeek, 0);
            string voice = (a.OneLiner ?? "").Trim();
            if (voice.Length == 0) voice = (a.Quirk ?? "").Trim();
            string flavor = voice.Length > 0
                ? "\"" + voice + "\"" : "no notes — they let the number speak";
            flavor += string.Format(" · waiting {0} wk", waiting);
            if ((a.Source ?? "inbound") == "referral")
                flavor += " · referred by somebody on the team";
            bool full = SimLabor.SeatsLeft(st) <= 0;
            int pick = i;
            var acts = new List<DeskKit.CardAction>
            {
                new DeskKit.CardAction
                {
                    Text = "hire", Reason = full ? "no desk left" : "",
                    On = () => SimLabor.HireApplicant(st, pick),
                },
                new DeskKit.CardAction
                {
                    Text = "pass", On = () => SimLabor.RejectApplicant(st, pick),
                },
            };
            DeskKit.Card(b, y, new DeskKit.CardRow
            {
                Name = string.Format("{0} — {1} · asks ${2}/wk (market ${3})", a.Name, role,
                    GameUi.Money(a.Ask), GameUi.Money(market)),
                Pips = SimLabor.SkillOf(a),
                PipsX = PipsX,
                Flavor = flavor,
                Pitch = RowH,
                Actions = acts,
            });
            return y + RowH;
        }

        static string HiringComputed(GameState st)
        {
            if (st.Applicants.Count == 0) return "";
            int best = 0;
            int bestAsk = 0;
            for (int i = 0; i < st.Applicants.Count; i++)
            {
                if (SimLabor.SkillOf(st.Applicants[i]) <= best) continue;
                best = SimLabor.SkillOf(st.Applicants[i]);
                bestAsk = st.Applicants[i].Ask;
            }
            return string.Format(
                "{0} waiting · best on the desk is skill {1} at ${2}/wk · a head lands on the payroll 2 wks before it is productive",
                st.Applicants.Count, best, GameUi.Money(bestAsk));
        }

        // ═══════════════════════════ THE DESK'S LAWS ═════════════════════════

        /// <summary>The standing rules, in the terms the player is learning.</summary>
        static string Rules(GameState st)
        {
            return "the rules of this desk: the MARKET RATE is what the street pays for the role · a head costs more than a salary (FULLY-LOADED) · pay under market and ATTRITION compounds · SEVERANCE is tenure-banded, and grows up with the company";
        }

        /// <summary>WARNINGS OUTRANK WISDOM (2.7): when the era's own law is being
        /// broken, the rules line yields to the number that is breaking it.</summary>
        static string Warning(GameState st)
        {
            if (SimLabor.SpanMult(st) < 1.0)
            {
                return string.Format(
                    "SPAN OF CONTROL: {0} heads under {1} manager(s) — the floor runs at {2}% until somebody manages it",
                    st.Employees.Count - SimLabor.ManagerCount(st), SimLabor.ManagerCount(st),
                    Gd.RoundToInt(SimLabor.SpanMult(st) * 100.0));
            }
            if (SimLabor.BenefitsShort(st))
            {
                return string.Format(
                    "BENEFITS: a real office expects ${0}/wk on the office lever for {1} staff, and you fund ${2} — morale pays the difference",
                    GameUi.Money(SimLabor.ExpectedBenefits(st)),
                    st.Employees.Count + st.Pipeline.Count, GameUi.Money(st.Budgets.Office));
            }
            return "";
        }

        /// <summary>The role as a person, so a sentence about one reads like
        /// English.</summary>
        static string RoleNoun(string role)
        {
            switch (SimLabor.RoleRow(role))
            {
                case "engineer": return "an engineer";
                case "sales": return "a sales head";
                case "designer": return "a designer";
                case "ops": return "an ops head";
                case "support": return "a support head";
                case "manager": return "a manager";
            }
            return "a head";
        }
    }
}
