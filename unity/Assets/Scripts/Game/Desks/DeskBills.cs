using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — COSTS · "bills" = THE BILLS LEDGER (twin of desk_bills.gd; DAG2
    /// W2 L-MONEY). OBLIGATIONS, NOT CHOICES: rows are engine truth (the roof
    /// by era + sites, the payroll sum, the catalog's fixed lines, serving
    /// COGS, the notes' interest, the taxman) and carry NO adjust buttons —
    /// you change a bill by changing its source. The TREND column teaches;
    /// the memo compares the Monday floor to revenue; the TOTAL double-rules
    /// and equals the hero. Standing commitments render too — obligations
    /// survive removal.
    /// </summary>
    public static class DeskBills
    {
        public const string Question = "what must be paid every Monday?";

        const float SheetX = 10f;
        const float SheetW = 1120f;
        const float YSheet = 108f;
        const float YFoot = 806f;
        const float YRules = 840f;
        const int ToolsMax = 5;

        sealed class BillRow
        {
            public string Who = "";
            public string What = "";
            public string Kind = "";
            public int Amt;
            public string Note = "";
            public Color? NoteCol;
            public string Press = "";
            public bool Dim;
            public bool SkipSum;
        }

        public static string[] HeroSummary(GameState s)
        {
            int total = Sum(FlatRows(s, false)) + Sum(ScalingRows(s));
            return new[] { "$" + GameUi.Money(total) + "/Mon", "the flat vs the scaling" };
        }

        public static void Draw(BinderScreen b)
        {
            GameState state = b.State;
            bool toolsOpen = DBool(b, "tools_open");
            List<BillRow> flat = FlatRows(state, toolsOpen);
            List<BillRow> scaling = ScalingRows(state);
            int flatSum = Sum(flat);
            int scalingSum = Sum(scaling);
            int total = flatSum + scalingSum;

            // ── the hero: the Monday floor, which the double-ruled TOTAL equals
            string big = "$" + GameUi.Money(total);
            b.L(big, SheetX, 6f, DeskKit.HeroSize, DrawnUI.Ink, 460f);
            float bw = DrawnUI.MeasureWidth(big, DeskKit.HeroSize);
            b.L("every Monday, before you choose anything", SheetX + bw + 16f, 22f,
                DeskKit.Row, Ink(0.7f), 560f);
            b.L("the flat moves when you move; the scaling moves when the business does.",
                SheetX, 62f, DeskKit.Detail, Ink(0.6f), 760f);
            var meta = b.L("week " + state.Week + " · " + state.Era + " era", SheetX, 10f,
                DeskKit.Law, Ink(0.42f), SheetW);
            meta.alignment = TMPro.TextAlignmentOptions.TopRight;

            // ── the sheet (no ADJUST column: obligations aren't adjustable)
            var sheet = DeskKit.LedgerSheet(b, SheetX, YSheet, SheetW, new List<DeskKit.LedgerCol>
            {
                new DeskKit.LedgerCol { Label = "who we pay", W = 250f },
                new DeskKit.LedgerCol { Label = "for what", W = 300f },
                new DeskKit.LedgerCol { Label = "kind", W = 90f, Align = "center" },
                new DeskKit.LedgerCol { Label = "$/wk", W = 130f, Align = "right" },
                new DeskKit.LedgerCol { Label = "trend", W = 300f },
            }, 3, false, "all figures $/week");
            DeskKit.LedgerSection(b, sheet, "the flat");
            foreach (BillRow r in flat) Row(b, sheet, r);
            DeskKit.LedgerSubtotal(b, sheet, "subtotal — the flat", "$" + GameUi.Money(flatSum));
            DeskKit.LedgerSection(b, sheet, "the scaling");
            foreach (BillRow r in scaling) Row(b, sheet, r);
            DeskKit.LedgerSubtotal(b, sheet, "subtotal — the scaling", "$" + GameUi.Money(scalingSum));
            DeskKit.LedgerTotal(b, sheet, "total bills", "$" + GameUi.Money(total));
            int revenue = state.LastPnl != null ? state.LastPnl.Revenue : 0;
            if (revenue > 0)
            {
                double ratio = (double)total / revenue;
                string memoNote = ratio >= 1.0
                    ? "the Monday floor eats " + ratio.ToString("0.0") + "× revenue"
                    : "revenue covers the floor ×" + (1.0 / Math.Max(ratio, 0.01)).ToString("0.0")
                      + " — the machine feeds itself";
                DeskKit.LedgerMemo(b, sheet, "revenue last week", "$" + GameUi.Money(revenue), memoNote);
            }
            else
                DeskKit.LedgerMemo(b, sheet, "revenue last week", "$0",
                    "no revenue yet — the floor waits for nobody");
            DeskKit.LedgerEnd(b, sheet);

            DeskKit.Footer(b,
                "bills are obligations — you change a bill by changing its source: the roof, the roster, the catalog, the debt",
                "single rule = subtotal · double rule = total — the book always balances to the hero · severance and notice periods survive removal",
                "", YFoot, YRules);
        }

        static void Row(BinderScreen b, DeskKit.LedgerBox sheet, BillRow r)
        {
            var cfg = new DeskKit.LedgerRowCfg { Dim = r.Dim };
            if (r.Press != "")
            {
                string target = r.Press;
                cfg.OnPress = () =>
                {
                    if (target == "tools") b.Desk["tools_open"] = !DBool(b, "tools_open");
                    else b.FocusDesk(target);
                };
            }
            float rowY = sheet.Cursor;
            // a colored trend renders ONLY as the overlay — the cell stays
            // empty so the two never double-print
            DeskKit.LedgerRow(b, sheet, new[] { r.Who, r.What, r.Kind,
                "$" + GameUi.Money(r.Amt), r.NoteCol.HasValue ? "" : r.Note }, cfg);
            if (r.NoteCol.HasValue)
            {
                DeskKit.LedgerCol tcol = sheet.Cols[4];
                b.L(r.Note, tcol.X, rowY + 8f, 18f, r.NoteCol.Value, tcol.W - 10f);
            }
        }

        // ── the rows, from engine truth ──────────────────────────────────────

        static List<BillRow> FlatRows(GameState state, bool toolsOpen)
        {
            var rows = new List<BillRow>();
            int eraRent = GameState.ERA_RENT.ContainsKey(state.Era) ? GameState.ERA_RENT[state.Era] : 150;
            rows.Add(new BillRow { Who = NameOf(state, "landlord", "the landlord"),
                What = "the " + state.Era + "-era roof", Kind = "flat", Amt = eraRent,
                Note = RentTrend(state) });
            if (state.Sites != null)
                foreach (Site s in state.Sites)
                    rows.Add(new BillRow { Who = string.IsNullOrEmpty(s.Name) ? "a second roof" : s.Name,
                        What = "a roof of its own", Kind = "flat", Amt = s.RentWk,
                        Note = "opened wk " + s.OpenedWk });
            int payroll = SimLabor.PayrollWk(state);
            int heads = state.Employees.Count + state.Pipeline.Count;
            rows.Add(new BillRow { Who = "the payroll", What = heads + " people -> team",
                Kind = "flat", Amt = payroll, Note = PayrollTrend(state), Press = "team" });
            List<BillRow> toolLines = ToolLines(state);
            int tools = (int)Math.Round(SimEngine.OffersFixedWk(state));
            if (tools > 0 || toolLines.Count > 0)
            {
                rows.Add(new BillRow { Who = "the tools",
                    What = toolLines.Count + " lines — the catalog's fixed costs", Kind = "flat",
                    Amt = tools, Note = "grows with the catalog", Press = "tools" });
                if (toolsOpen)
                {
                    int shown = 0;
                    foreach (BillRow t in toolLines)
                    {
                        if (shown >= ToolsMax)
                        {
                            rows.Add(new BillRow { Who = "",
                                What = "+" + (toolLines.Count - shown) + " more tool lines",
                                Dim = true, SkipSum = true });
                            break;
                        }
                        t.Dim = true;
                        t.SkipSum = true;
                        rows.Add(t);
                        shown += 1;
                    }
                }
            }
            int standing = 0;
            int standingWks = 0;
            foreach (Commitment c in state.Commitments)
            {
                if (c.WeeksLeft <= 0) continue;
                standing += Math.Abs(Math.Min(c.CashWk, 0));
                standingWks = Math.Max(standingWks, c.WeeksLeft);
            }
            if (standing > 0)
                rows.Add(new BillRow { Who = "the standing costs",
                    What = "what nobody planned, on a plan", Kind = "flat", Amt = standing,
                    Note = "runs out within " + standingWks + " wks" });
            return rows;
        }

        static List<BillRow> ScalingRows(GameState state)
        {
            var rows = new List<BillRow>();
            double cogsPc = SimEngine.OffersCogsPerCustomer(state);
            int serving = (int)Math.Round(cogsPc * state.Traction);
            bool marginSafe = SimBank.ContributionMargin(state) > 0.0;
            rows.Add(new BillRow { Who = "serving customers",
                What = "≈$" + cogsPc.ToString("0") + " × " + state.Traction + ", every week",
                Kind = "scales", Amt = serving,
                Note = marginSafe ? "margin-safe at your prices" : "each one serves at a loss",
                NoteCol = marginSafe ? DrawnUI.Hex("5D7A50") : DrawnUI.Coral });
            int interest = 0;
            bool amortizing = false;
            bool onlyFee = false;
            if (state.LoanPrincipal > 0)
            {
                interest += (int)Math.Ceiling(state.LoanPrincipal * SimBank.SharkRate);
                onlyFee = true;
            }
            foreach (Loan l in state.Loans)
            {
                if (l.Balance <= 0) continue;
                interest += (int)Math.Ceiling(l.Balance * l.RateWk);
                if (l.Kind == "bank") amortizing = true;
                else onlyFee = true;
            }
            if (interest > 0)
            {
                string note = "falls as you repay";
                Color ncol = DrawnUI.Hex("5D7A50");
                if (!amortizing && onlyFee)
                {
                    note = "never falls on its own — interest only";
                    ncol = DrawnUI.Coral;
                }
                rows.Add(new BillRow { Who = NameOf(state, "bank", "the bank"),
                    What = "interest on $" + GameUi.Money(SimBank.DebtTotal(state)) + " -> the bank",
                    Kind = "scales", Amt = interest, Note = note, NoteCol = ncol, Press = "the bank" });
            }
            int tax = state.LastPnl != null ? state.LastPnl.Tax : 0;
            string taxNote;
            if (state.EraIndex() < SimBank.TaxEra)
                taxNote = "waiting — below the radar until the office era";
            else if (state.TaxLossCarry > 0)
                taxNote = "losses banked: $" + GameUi.Money(state.TaxLossCarry) + " shelter profit";
            else
                taxNote = "20% of profit, after interest";
            rows.Add(new BillRow { Who = "the taxman", What = "on profit — never on revenue",
                Kind = "scales", Amt = tax, Note = taxNote });
            return rows;
        }

        /// Every offer's fixed lines, flattened with their offer's name.
        static List<BillRow> ToolLines(GameState state)
        {
            var outRows = new List<BillRow>();
            foreach (Offer o in state.Offers)
            {
                if (o.FixedLines == null) continue;
                foreach (CostLine fl in o.FixedLines)
                    outRows.Add(new BillRow { Who = "· " + (fl.Label ?? "a tool"),
                        What = o.Name ?? "", Kind = "flat",
                        Amt = (int)Math.Round(fl.Amount) });
            }
            return outRows;
        }

        static string RentTrend(GameState state)
        {
            int idx = state.EraIndex();
            if (idx >= GameState.ERAS.Count - 1) return "the last roof on the ladder";
            string nextEra = GameState.ERAS[idx + 1];
            int cur = Math.Max(GameState.ERA_RENT.ContainsKey(state.Era) ? GameState.ERA_RENT[state.Era] : 150, 1);
            int nxt = GameState.ERA_RENT.ContainsKey(nextEra) ? GameState.ERA_RENT[nextEra] : cur;
            return "jumps ×" + (int)Math.Round((double)nxt / cur) + " at the " + nextEra + " era";
        }

        static string PayrollTrend(GameState state)
        {
            int pending = 0;
            foreach (OpenRole r in state.OpenRoles)
                pending += r.OfferedSalary * Math.Max(r.Seats, 1);
            if (pending > 0)
                return "+$" + GameUi.Money(pending) + " if you fill the seat"
                       + (state.OpenRoles.Count == 1 ? "" : "s");
            return "moves only when you hire";
        }

        /// A section's honest sum — breakdown rows (SkipSum) never join it.
        static int Sum(List<BillRow> rows)
        {
            int total = 0;
            foreach (BillRow r in rows)
                if (!r.SkipSum) total += r.Amt;
            return total;
        }

        /// The world's own counterparty name when the topics carry one, else
        /// plain. Tolerates both live dictionaries and a loaded save's JObject.
        static string NameOf(GameState state, string key, string fallback)
        {
            if (state.Topics == null) return fallback;
            object direct;
            if (state.Topics.TryGetValue(key, out direct))
            {
                string d = AsString(direct);
                if (d != "") return d;
            }
            object names;
            if (state.Topics.TryGetValue("names", out names))
            {
                var dict = names as IDictionary<string, object>;
                if (dict != null)
                {
                    object v;
                    if (dict.TryGetValue(key, out v))
                    {
                        string s = AsString(v);
                        if (s != "") return s;
                    }
                }
                var jo = names as JObject;
                if (jo != null)
                {
                    JToken t = jo[key];
                    if (t != null && t.Type == JTokenType.String && (string)t != "")
                        return (string)t;
                }
            }
            return fallback;
        }

        static string AsString(object v)
        {
            if (v is string) return (string)v;
            var t = v as JValue;
            if (t != null && t.Type == JTokenType.String) return (string)t.Value;
            return "";
        }

        static bool DBool(BinderScreen b, string key)
        {
            object v;
            return b.Desk.TryGetValue(key, out v) && v is bool && (bool)v;
        }

        static Color Ink(float a) { return DrawnUI.WithAlpha(DrawnUI.Ink, a); }

        public static void Handle(BinderScreen b, string id)
        {
            // rows route through closures; obligations carry no controls
        }
    }
}
