using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — COSTS · "team" = THE PAYROLL LEDGER (twin of desk_team.gd; DAG2
    /// W2 L-MONEY). Three deterministic rungs (SimSpendBook.TeamRung): flat
    /// person rows -> function groups with subtotals (askers surface first) ->
    /// business units that open to their functions (display recursion). The
    /// OPEN SEAT points at recruitment; asks are answered ON the row via
    /// SimLabor.GrantRaise behind a receipt-priced arm; granted rows carry
    /// the vesting mini-bar (208/52 fallback formula).
    ///
    /// DAG3 Wave B: S1 zero state (nobody yet), S2 ask strip + spotlit
    /// controls (raise_first / raise_urgent / go_recruit / open_seat /
    /// poached), S3 DO lane, S4 the payroll-total receipt, S5 the morale
    /// ▲/▼ beside the hero's morale read, the vesting bar pressing through
    /// to the cap table (back pill free), S15 the ask as a jump suggestion.
    /// </summary>
    public static class DeskTeam
    {
        public const string Question = "who works here and who's asking?";

        const float SheetX = 10f;
        const float SheetW = 1120f;
        const float YSheet = 108f;
        const float YFoot = 806f;
        const float YRules = 840f;
        const int GroupMax = 6;

        static readonly string[] Functions = { "engineer", "designer", "support", "manager", "sales", "ops" };

        sealed class Unit
        {
            public string Name = "";
            public List<string> Roles = new List<string>();
        }

        public static string[] HeroSummary(GameState s)
        {
            // onboarding hires are on the payroll the total shows — say so
            string big = s.Employees.Count + " people";
            if (s.Pipeline.Count > 0) big += " +" + s.Pipeline.Count + " onboarding";
            return new[] { big,
                "$" + GameUi.Money(SimLabor.PayrollWk(s)) + "/wk on payroll" };
        }

        /// S8 — the rail's micro-status: the headcount, plain.
        public static string MicroStatus(GameState s)
        {
            return s.Employees.Count > 0 ? s.Employees.Count.ToString() : "";
        }

        /// S8 — the payroll ledger never dims: the founder is always here.
        public static bool IsDormant(GameState s)
        {
            return false;
        }

        /// S15 — the loudest object on the desk speaks up: the first ask,
        /// as a jump.
        public static List<Dictionary<string, object>> Suggestions(GameState s)
        {
            var rows = new List<Dictionary<string, object>>();
            int fa = FirstAsker(s);
            if (fa < 0) return rows;
            rows.Add(new Dictionary<string, object>
            {
                { "label", "answer the ask — " + (s.Employees[fa].Name ?? "someone")
                    + " wants market pay" },
                { "kind", "jump" },
                { "payload", new Dictionary<string, object>
                    { { "desk", "team" }, { "control", "raise_first" } } },
            });
            return rows;
        }

        /// The engine's own predicates, desk-side: the FIRST asker and the
        /// first OVERDUE asker (asked ≥2 wks ago — the resignation clock).
        static int FirstAsker(GameState state)
        {
            for (int i = 0; i < state.Employees.Count; i++)
                if (state.Employees[i].WantsRaise) return i;
            return -1;
        }

        static int FirstUrgent(GameState state)
        {
            for (int i = 0; i < state.Employees.Count; i++)
            {
                Employee e = state.Employees[i];
                if (e.WantsRaise && state.Week - e.AskedWeek >= 2) return i;
            }
            return -1;
        }

        /// S5 — last week's morale, from the metric history the tick keeps.
        static float MoralePrev(GameState state)
        {
            for (int i = state.MetricHistory.Count - 1; i >= 0; i--)
                if (state.MetricHistory[i].Wk != state.Week)
                    return state.MetricHistory[i].Morale;
            return state.Morale;
        }

        public static void Draw(BinderScreen b)
        {
            GameState state = b.State;
            int payroll = SimLabor.PayrollWk(state);
            int n = state.Employees.Count;
            int rung = SimSpendBook.TeamRung(n);

            // ── S1 · the zero state: nobody yet — the page teaches payroll
            if (state.Employees.Count == 0 && state.Pipeline.Count == 0
                && state.OpenRoles.Count == 0)
            {
                Dictionary<string, object> band = SimOwnership.BandFor(state, "engineer");
                DeskKit.ZeroState(b, new DeskKit.ZeroStateCfg
                {
                    WillShow = "the payroll ledger — who works here, what they cost, who's asking",
                    WouldLine = "a first engineer WOULD cost $"
                        + SimOwnership.Money(Di(band, "lo", 0)) + "–"
                        + SimOwnership.Money(Di(band, "hi", 0))
                        + " a week — and the roof rides every head",
                    ActionLabel = "open a seat → recruitment",
                    ActionCb = () => b.FocusDesk("recruitment"),
                    WakesHint = "wakes when the first offer is signed — hiring lives at recruitment",
                });
                return;
            }

            // ── the hero — onboarding hires are paid, so the money line names them
            string big = n + " people";
            b.L(big, SheetX, 6f, DeskKit.HeroSize, DrawnUI.Ink, 420f);
            float bw = DrawnUI.MeasureWidth(big, DeskKit.HeroSize);
            b.L("· $" + GameUi.Money(payroll) + " a week"
                + (state.Pipeline.Count > 0
                    ? " · +" + state.Pipeline.Count + " onboarding" : ""),
                SheetX + bw + 14f, 22f,
                DeskKit.Row, Ink(0.7f), 420f);
            b.L("payroll is the biggest bill in the building — and the easiest to grow carelessly.",
                SheetX, 62f, DeskKit.Detail, Ink(0.6f), 700f);
            if (state.Applicants.Count > 0)
                DeskKit.ClockChip(b, 848f, 10f, state.Applicants.Count + " applicant"
                    + (state.Applicants.Count == 1 ? "" : "s") + " waiting");
            // the door to the hiring flow, always drawn — the red rows land on it
            DeskKit.Word(b, "recruitment ▸", 848f, 38f, () => b.FocusDesk("recruitment"),
                DeskKit.Law, Ink(0.6f), 200f);
            b.MarkControl("go_recruit", new Rect(840f, 36f, 216f, 44f));
            // S5 — the morale read wears its week-over-week arrow (the meta
            // line lost the sheet's own unit words; the sheet says them once)
            string mtxt = "morale " + state.Morale + " · rung " + rung;
            float mw = DrawnUI.MeasureWidth(mtxt, DeskKit.Law);
            float mx = SheetX + SheetW - mw - 4f;
            b.L(mtxt, mx, 64f, DeskKit.Law, Ink(0.42f), mw + 8f);
            DeskKit.DeltaArrow(b, mx - 24f, 66f, state.Morale, MoralePrev(state));
            // S2a — red speaks on the page; the sheet drops 8px clear
            float sheetY = YSheet;
            if (DeskKit.AskStrip(b, "team", SheetX, 86f, 1000f, "answer the ask before it walks"))
                sheetY += 8f;

            // ── the sheet
            var sheet = DeskKit.LedgerSheet(b, SheetX, sheetY, SheetW, new List<DeskKit.LedgerCol>
            {
                new DeskKit.LedgerCol { Label = "who", W = 210f },
                new DeskKit.LedgerCol { Label = "role", W = 160f },
                new DeskKit.LedgerCol { Label = "skill", W = 140f },
                new DeskKit.LedgerCol { Label = "$/wk", W = 120f, Align = "right" },
                new DeskKit.LedgerCol { Label = "note", W = 330f },
            }, 3, false, "all figures $/week");
            if (rung == 1)
            {
                for (int i = 0; i < state.Employees.Count; i++)
                    PersonRow(b, sheet, state, i);
            }
            else
            {
                AskersFirst(b, sheet, state);
                var pool = new List<int>();
                for (int i = 0; i < state.Employees.Count; i++) pool.Add(i);
                if (rung == 2) FunctionGroups(b, sheet, state, pool, "fn");
                else UnitRows(b, sheet, state);
            }
            // onboarding hires are on the payroll before they are productive
            foreach (PipelineHire h in state.Pipeline)
                DeskKit.LedgerRow(b, sheet, new[] { h.Name ?? "a hire", h.Role ?? "", "",
                    "$" + GameUi.Money(h.Salary),
                    "onboarding — wk " + Mathf.Clamp(h.WeeksIn + 1, 1, 2) + " of 2" },
                    new DeskKit.LedgerRowCfg { Dim = true });
            // THE OPEN SEAT — an honest row; the flow lives at recruitment
            bool seatMarked = false;
            foreach (OpenRole r in state.OpenRoles)
            {
                string role = r.Role ?? "engineer";
                int waiting = SimLabor.WaitingFor(state, role);
                float seatY = sheet.Cursor;
                // Law 2 — the offered pay rides the money column; the rate is a fact
                DeskKit.LedgerRow(b, sheet, new[] { role + " — open seat", "advertised",
                    "≈" + SimLabor.ArrivalRate(state, r).ToString("0.0") + " apply/wk",
                    "$" + GameUi.Money(r.OfferedSalary),
                    waiting + " waiting -> recruitment ▸" },
                    new DeskKit.LedgerRowCfg { Dim = true, OnPress = () => b.FocusDesk("recruitment") });
                // S2b — a silent advert's red row lands on its own seat
                if (!seatMarked)
                {
                    b.MarkControl("open_seat", new Rect(SheetX, seatY, SheetW * 0.5f,
                        DeskKit.LgRowH));
                    seatMarked = true;
                }
            }
            // S4 — PRESS THE TOTAL: the receipt that decomposes the payroll
            float totY = sheet.Cursor;
            DeskKit.LedgerTotal(b, sheet, "total payroll", "$" + GameUi.Money(payroll));
            b.MarkControl("payroll_total", new Rect(SheetX, totY, SheetW, DeskKit.LgTotH));
            DeskKit.PressReceipt(b, "payroll_total", "payroll = every signed salary",
                PayrollLines(state));
            DeskKit.LedgerMemo(b, sheet, "fully loaded",
                "≈$" + GameUi.Money(SimLabor.LoadedPayrollWk(state)),
                "with the roof's share · severance always owed");
            DeskKit.LedgerEnd(b, sheet);

            // ── S3 · the DO lane: answer the loudest ask, or grow the roster
            var actions = new List<DeskKit.DoAction>();
            int fa = FirstAsker(state);
            if (fa >= 0)
            {
                Employee ea = state.Employees[fa];
                int fair = SimLabor.FairPay(state, ea);
                int fi = fa;
                actions.Add(new DeskKit.DoAction
                {
                    Label = "answer ask — " + (ea.Name ?? "someone").Split(' ')[0]
                        + " · $" + GameUi.Money(fair) + "/wk",
                    Tier = "two-tap",
                    Cb = () => SimLabor.GrantRaise(state, fi, fair),
                });
            }
            actions.Add(new DeskKit.DoAction
            {
                Label = "open a seat → recruitment",
                Tier = "",
                Cb = () => b.FocusDesk("recruitment"),
            });
            DeskKit.DoLane(b, actions);

            DeskKit.Footer(b,
                "a person costs more than their pay — the roof, the seats and the office share ride every head",
                "asks answered late become resignations · at 10 the rows group by function · at hundreds, by business unit — same sheet, folded",
                "", YFoot, YRules);
        }

        /// S4 — the payroll receipt's terms: signed salaries, the onboarding
        /// share, and the loaded truth the memo whispers.
        static List<DeskKit.TicketLine> PayrollLines(GameState state)
        {
            int empSum = 0;
            foreach (Employee e in state.Employees) empSum += e.Salary;
            int pipeSum = 0;
            foreach (PipelineHire h in state.Pipeline) pipeSum += h.Salary;
            var lines = new List<DeskKit.TicketLine>
            {
                new DeskKit.TicketLine { Label = "salaries — " + state.Employees.Count + " people",
                    Value = "$" + GameUi.Money(empSum) + "/wk" },
            };
            if (pipeSum > 0)
                lines.Add(new DeskKit.TicketLine
                {
                    Label = "onboarding — " + state.Pipeline.Count + " hire"
                        + (state.Pipeline.Count == 1 ? "" : "s"),
                    Value = "$" + GameUi.Money(pipeSum) + "/wk",
                });
            lines.Add(new DeskKit.TicketLine { Label = "fully loaded",
                Value = "≈$" + GameUi.Money(SimLabor.LoadedPayrollWk(state)) + "/wk" });
            lines.Add(new DeskKit.TicketLine { Label = "the law",
                Value = "severance always owed" });
            return lines;
        }

        // ── the one person row every rung shares ─────────────────────────────

        static void PersonRow(BinderScreen b, DeskKit.LedgerBox sheet, GameState state, int i)
        {
            Employee e = state.Employees[i];
            float rowY = sheet.Cursor;
            bool asking = e.WantsRaise;
            DeskKit.LedgerRow(b, sheet, new[] { e.Name ?? "someone", e.Role ?? "", "",
                "$" + GameUi.Money(e.Salary), "" }, new DeskKit.LedgerRowCfg());
            DeskKit.Pips(b, sheet.Cols[2].X, rowY + 13f, SimLabor.SkillOf(e), 5);
            float noteX = sheet.Cols[4].X;
            float noteW = sheet.Cols[4].W;
            // S2b — a courted colleague's red row lands on their own line
            if (Gd.ToInt(state.GetMetaF("poach_wk", -99.0)) == state.Week
                && (e.Name ?? "") == (state.GetMeta("poach_name", "") as string ?? ""))
                b.MarkControl("poached", new Rect(SheetX, rowY, SheetW * 0.5f, DeskKit.LgRowH));
            if (asking)
            {
                // the coral ask, answered ON the row via the existing raise op
                int fair = SimLabor.FairPay(state, e);
                int idx = i;
                DeskKit.Arm(b, "raise_" + i, "wants market pay — answer $" + GameUi.Money(fair),
                    "pay $" + GameUi.Money(fair) + "/wk — sure?", noteX, rowY + 4f,
                    () => SimLabor.GrantRaise(state, idx, fair), 300f, 19f);
                // S2b — the red rows land on the arm that answers them
                if (i == FirstAsker(state))
                    b.MarkControl("raise_first", new Rect(noteX - 8f, rowY + 2f, 316f,
                        DeskKit.LgRowH));
                if (i == FirstUrgent(state))
                    b.MarkControl("raise_urgent", new Rect(noteX - 8f, rowY + 2f, 316f,
                        DeskKit.LgRowH));
                return;
            }
            EsopGrant grant = SimSpendBook.GrantFor(state, e.Name ?? "");
            if (grant != null)
            {
                // THE VESTING MINI-BAR (the ESOP thread's team surface)
                double frac = SimSpendBook.VestedFrac(state.Week, grant.VestStartWk);
                int cliffWk = grant.VestStartWk + 52;
                string cliffTxt = state.Week >= cliffWk ? "cliff passed" : "cliff wk " + cliffWk;
                b.L(grant.Pct.ToString("0.0") + "% · " + Math.Round(frac * 100.0) + "% vested · "
                    + cliffTxt, noteX, rowY + 8f, 18f, Ink(0.7f), noteW - 84f);
                DeskKit.Meter(b, noteX + noteW - 76f, rowY + 9f, 66f, (float)frac, DrawnUI.Sage);
                // S7 — the bar presses through to the ownership state; the
                // back pill home is free (FocusDesk carries the source)
                var hit = DeskKit.Word(b, "", noteX, rowY + 2f,
                    () => b.FocusDesk("cap table", "pool", "team"), 18f, DrawnUI.Ink, noteW);
                hit.GetComponent<RectTransform>().sizeDelta =
                    new Vector2(noteW, DeskKit.LgRowH - 4f);
                return;
            }
            string quirk = e.Quirk ?? "";
            b.L(quirk != "" ? quirk : "—", noteX, rowY + 8f, 18f, Ink(0.5f), noteW - 10f);
        }

        /// ASKERS SURFACE FIRST (rungs 2–3): the people asking never fold.
        static void AskersFirst(BinderScreen b, DeskKit.LedgerBox sheet, GameState state)
        {
            bool asked = false;
            for (int i = 0; i < state.Employees.Count; i++)
            {
                if (!state.Employees[i].WantsRaise) continue;
                if (!asked)
                {
                    DeskKit.LedgerSection(b, sheet, "the askers — answer or lose them");
                    asked = true;
                }
                PersonRow(b, sheet, state, i);
            }
        }

        /// Rung 2: the roster grouped by FUNCTION with subtotals; a group
        /// opens to its people (askers already surfaced above).
        static void FunctionGroups(BinderScreen b, DeskKit.LedgerBox sheet, GameState state,
                                   List<int> pool, string keyPrefix)
        {
            foreach (string fn in Functions)
            {
                var members = new List<int>();
                int groupPay = 0;
                foreach (int i in pool)
                {
                    Employee e = state.Employees[i];
                    if (SimLabor.RoleRow(e.Role ?? "engineer") != fn) continue;
                    members.Add(i);
                    groupPay += e.Salary;
                }
                if (members.Count == 0) continue;
                string key = keyPrefix + "_" + fn;
                bool open = DBool(b, key);
                DeskKit.LedgerRow(b, sheet, new[] { fn.ToUpper() + " ×" + members.Count, "", "",
                    "$" + GameUi.Money(groupPay), open ? "close ▸" : "open ▸" },
                    new DeskKit.LedgerRowCfg { OnPress = () => { b.Desk[key] = !open; } });
                if (!open) continue;
                int shown = 0;
                foreach (int mi in members)
                {
                    if (state.Employees[mi].WantsRaise) continue;   // face-up in THE ASKERS
                    if (shown >= GroupMax)
                    {
                        DeskKit.LedgerRow(b, sheet, new[] { "the other " + (members.Count - shown),
                            "", "", "", "steady" }, new DeskKit.LedgerRowCfg { Dim = true });
                        break;
                    }
                    PersonRow(b, sheet, state, mi);
                    shown += 1;
                }
            }
        }

        /// Rung 3: BUSINESS UNITS — the grouped-row component recursed; the
        /// face reads the unit's own burnout mix (the engine keeps no
        /// per-unit meters yet).
        static void UnitRows(BinderScreen b, DeskKit.LedgerBox sheet, GameState state)
        {
            string opened = DStr(b, "unit_open");
            int others = 0;
            int othersPay = 0;
            foreach (Unit u in Units(state))
            {
                var members = new List<int>();
                int pay = 0;
                int asks = 0;
                int burnout = 0;
                for (int i = 0; i < state.Employees.Count; i++)
                {
                    Employee e = state.Employees[i];
                    if (!u.Roles.Contains(SimLabor.RoleRow(e.Role ?? "engineer"))) continue;
                    members.Add(i);
                    pay += e.Salary;
                    burnout += e.Burnout;
                    if (e.WantsRaise) asks += 1;
                }
                if (members.Count == 0) continue;
                string face = GameState.BurnoutState(burnout / Math.Max(members.Count, 1));
                string uname = u.Name;
                bool open = opened == uname;
                // an open unit folds its siblings to one counted row
                if (opened != "" && !open)
                {
                    others += 1;
                    othersPay += pay;
                    continue;
                }
                float rowY = sheet.Cursor;
                DeskKit.LedgerRow(b, sheet, new[] { uname.ToUpper(), members.Count + " people", "",
                    "$" + GameUi.Money(pay), "avg $" + GameUi.Money(pay / Math.Max(members.Count, 1))
                    + " · " + asks + " asking · " + (open ? "close ▸" : "open ▸") },
                    new DeskKit.LedgerRowCfg { OnPress = () => { b.Desk["unit_open"] = open ? "" : uname; } });
                b.L(face, sheet.Cols[2].X, rowY + 8f, 18f,
                    face != "fine" ? DrawnUI.Coral : DrawnUI.Hex("5D7A50"), 120f);
                if (open) FunctionGroups(b, sheet, state, members, "u_" + uname);
            }
            if (others > 0)
                DeskKit.LedgerRow(b, sheet, new[] { "the other " + others + " units", "", "",
                    "$" + GameUi.Money(othersPay), "close this unit to see them ▸" },
                    new DeskKit.LedgerRowCfg { Dim = true,
                        OnPress = () => { b.Desk["unit_open"] = ""; } });
        }

        /// The units the topics name (units: [{name, roles[]}]), validated;
        /// else the defaults. Tolerates live dictionaries and JObject/JArray.
        static List<Unit> Units(GameState state)
        {
            var outU = new List<Unit>();
            object raw = null;
            if (state.Topics != null) state.Topics.TryGetValue("units", out raw);
            foreach (object entry in AsList(raw))
            {
                string name = "";
                var roles = new List<string>();
                var d = entry as IDictionary<string, object>;
                if (d != null)
                {
                    object nv;
                    if (d.TryGetValue("name", out nv) && nv is string) name = (string)nv;
                    object rv;
                    if (d.TryGetValue("roles", out rv))
                        foreach (object r in AsList(rv))
                            if (r is string && Array.IndexOf(Functions, (string)r) >= 0)
                                roles.Add((string)r);
                }
                var jo = entry as JObject;
                if (jo != null)
                {
                    name = (string)(jo["name"] ?? "");
                    var ja = jo["roles"] as JArray;
                    if (ja != null)
                        foreach (JToken t in ja)
                            if (t.Type == JTokenType.String && Array.IndexOf(Functions, (string)t) >= 0)
                                roles.Add((string)t);
                }
                if (name != "" && roles.Count > 0) outU.Add(new Unit { Name = name, Roles = roles });
            }
            if (outU.Count == 0)
            {
                outU.Add(new Unit { Name = "engineering", Roles = new List<string> { "engineer", "designer" } });
                outU.Add(new Unit { Name = "gtm", Roles = new List<string> { "sales" } });
                outU.Add(new Unit { Name = "success", Roles = new List<string> { "support" } });
                outU.Add(new Unit { Name = "g&a", Roles = new List<string> { "ops", "manager" } });
                return outU;
            }
            // any function no unit claimed falls into the last unit
            var claimed = new List<string>();
            foreach (Unit u in outU) claimed.AddRange(u.Roles);
            foreach (string fn in Functions)
                if (!claimed.Contains(fn)) outU[outU.Count - 1].Roles.Add(fn);
            return outU;
        }

        static List<object> AsList(object v)
        {
            var outL = new List<object>();
            var l = v as IEnumerable<object>;
            if (l != null) { outL.AddRange(l); return outL; }
            var ja = v as JArray;
            if (ja != null) foreach (JToken t in ja) outL.Add(t);
            return outL;
        }

        static int Di(Dictionary<string, object> d, string k, int dv)
        {
            object v;
            if (d != null && d.TryGetValue(k, out v) && v != null)
            {
                try { return Convert.ToInt32(v); } catch { return dv; }
            }
            return dv;
        }

        static string DStr(BinderScreen b, string key)
        {
            object v;
            return b.Desk.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }

        static bool DBool(BinderScreen b, string key)
        {
            object v;
            return b.Desk.TryGetValue(key, out v) && v is bool && (bool)v;
        }

        static Color Ink(float a) { return DrawnUI.WithAlpha(DrawnUI.Ink, a); }

        public static void Handle(BinderScreen b, string id)
        {
            // every control on this sheet carries its own closure
        }
    }
}
