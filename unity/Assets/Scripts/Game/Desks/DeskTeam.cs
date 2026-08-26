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
    /// person rows → function groups with subtotals (askers surface first) →
    /// business units that open to their functions (display recursion). The
    /// OPEN SEAT points at recruitment; asks are answered ON the row via
    /// SimLabor.GrantRaise behind a receipt-priced arm; granted rows carry
    /// the vesting mini-bar (208/52 fallback formula).
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
            return new[] { s.Employees.Count + " people",
                "$" + GameUi.Money(SimLabor.PayrollWk(s)) + "/wk on payroll" };
        }

        public static void Draw(BinderScreen b)
        {
            GameState state = b.State;
            int payroll = SimLabor.PayrollWk(state);
            int n = state.Employees.Count;
            int rung = SimSpendBook.TeamRung(n);

            // ── the hero
            string big = n + " people";
            b.L(big, SheetX, 6f, DeskKit.HeroSize, DrawnUI.Ink, 420f);
            float bw = DrawnUI.MeasureWidth(big, DeskKit.HeroSize);
            b.L("· $" + GameUi.Money(payroll) + " a week", SheetX + bw + 14f, 22f,
                DeskKit.Row, Ink(0.7f), 360f);
            b.L("payroll is the biggest bill in the building — and the easiest to grow carelessly.",
                SheetX, 62f, DeskKit.Detail, Ink(0.6f), 720f);
            if (state.Applicants.Count > 0)
            {
                DeskKit.ClockChip(b, 848f, 10f, state.Applicants.Count + " applicant"
                    + (state.Applicants.Count == 1 ? "" : "s") + " waiting");
                DeskKit.Word(b, "recruitment ▸", 848f, 38f, () => b.FocusDesk("recruitment"),
                    DeskKit.Law, Ink(0.6f), 200f);
            }
            var meta = b.L("morale " + state.Morale + " · all figures $/week · rung " + rung,
                SheetX, 74f, DeskKit.Law, Ink(0.42f), SheetW);
            meta.alignment = TMPro.TextAlignmentOptions.TopRight;

            // ── the sheet
            var sheet = DeskKit.LedgerSheet(b, SheetX, YSheet, SheetW, new List<DeskKit.LedgerCol>
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
            foreach (OpenRole r in state.OpenRoles)
            {
                string role = r.Role ?? "engineer";
                int waiting = SimLabor.WaitingFor(state, role);
                DeskKit.LedgerRow(b, sheet, new[] { role + " — open seat", "advertised",
                    "advert $" + GameUi.Money(r.OfferedSalary),
                    "≈" + SimLabor.ArrivalRate(state, r).ToString("0.0") + "/wk",
                    waiting + " waiting → recruitment ▸" },
                    new DeskKit.LedgerRowCfg { Dim = true, OnPress = () => b.FocusDesk("recruitment") });
            }
            if (state.Employees.Count == 0 && state.Pipeline.Count == 0 && state.OpenRoles.Count == 0)
                DeskKit.LedgerRow(b, sheet, new[] { "nobody yet", "the founder does everything",
                    "", "$0", "hiring starts at recruitment ▸" },
                    new DeskKit.LedgerRowCfg { Dim = true, OnPress = () => b.FocusDesk("recruitment") });
            DeskKit.LedgerTotal(b, sheet, "total payroll", "$" + GameUi.Money(payroll));
            DeskKit.LedgerMemo(b, sheet, "fully loaded",
                "≈$" + GameUi.Money(SimLabor.LoadedPayrollWk(state)),
                "with the roof's share · severance always owed");
            DeskKit.LedgerEnd(b, sheet);

            DeskKit.Footer(b,
                "a person costs more than their pay — the roof, the seats and the office share ride every head",
                "asks answered late become resignations · at 10 the rows group by function · at hundreds, by business unit — same sheet, folded",
                "", YFoot, YRules);
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
            if (asking)
            {
                // the coral ask, answered ON the row via the existing raise op
                int fair = SimLabor.FairPay(state, e);
                int idx = i;
                DeskKit.Arm(b, "raise_" + i, "wants market pay — answer $" + GameUi.Money(fair),
                    "pay $" + GameUi.Money(fair) + "/wk — sure?", noteX, rowY + 4f,
                    () => SimLabor.GrantRaise(state, idx, fair), 300f, 19f);
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
