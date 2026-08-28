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
    ///
    /// DAG3 (13-binder-ux): every row press jumps to its SOURCE with a back
    /// pill (rent/roofs -> the works, interest -> that note's card at the
    /// bank, serving -> the works' ticket, payroll -> team); the eats-N.N×
    /// memo wears the S5 pen when the ratio moved since the binder last
    /// opened; the hero carries its delta. No DO lane and no ask strip BY
    /// DESIGN: bills are obligations — the sources hold the switches.
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
            public string Ctl = "";
            public bool Dim;
            public bool SkipSum;
        }

        /// S8 — bills never sleep: the roof and the ramen bill from week 1,
        /// which IS the lesson. The tab never dims.
        public static bool IsDormant(GameState s) { return false; }

        /// S10 — the rail's four-character read: the Monday floor.
        public static string MicroStatus(GameState s)
        {
            int total = Sum(FlatRows(s, false)) + Sum(ScalingRows(s));
            if (total >= 1000) return "$" + (total / 1000.0).ToString("0.0") + "k";
            return "$" + total;
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
            Pnl pnl = state.LastPnl;

            // ── the hero: the Monday floor, which the double-ruled TOTAL equals
            string big = "$" + GameUi.Money(total);
            b.L(big, SheetX, 6f, DeskKit.HeroSize, DrawnUI.Ink, 460f);
            float bw = DrawnUI.MeasureWidth(big, DeskKit.HeroSize);
            // S5 — which way the floor moved since the binder was last open
            // (the stored value read BEFORE recording this open's — the law)
            string prevTotal = b.SeenPrev("bills", "total");
            b.Seen("bills", "total", total.ToString());
            float capX = SheetX + bw + 16f;
            int prevTotalI;
            if (prevTotal != "" && int.TryParse(prevTotal, out prevTotalI) && prevTotalI != total)
            {
                DeskKit.DeltaArrow(b, SheetX + bw + 12f, 26f, total, prevTotalI);
                capX += 26f;
            }
            b.L("every Monday, before you choose anything", capX, 22f,
                DeskKit.Row, Ink(0.7f), 560f);
            // S1 — before the first Monday has struck, the sheet is a promise
            // and says so in the honest subjunctive
            string subline = pnl == null
                ? "no Monday has struck yet — this is what one would take, before a single sale."
                : "the flat moves when you move; the scaling moves when the business does.";
            b.L(subline, SheetX, 62f, DeskKit.Detail, Ink(0.6f), 760f);
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
            int revenue = pnl != null ? pnl.Revenue : 0;
            if (revenue > 0)
            {
                double ratio = (double)total / revenue;
                string memoNote = ratio >= 1.0
                    ? "the Monday floor eats " + ratio.ToString("0.0") + "× revenue"
                    : "revenue covers the floor ×" + (1.0 / Math.Max(ratio, 0.01)).ToString("0.0")
                      + " — the machine feeds itself";
                float memoY = sheet.Cursor;
                DeskKit.LedgerMemo(b, sheet, "revenue last week", "$" + GameUi.Money(revenue), memoNote);
                // S5 — the memo wears the pen when the ratio moved since the
                // last open: the circle marks the news, the arrow the way
                string prevRatio = b.SeenPrev("bills", "eats_ratio");
                bool moved = b.Seen("bills", "eats_ratio",
                    ratio.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                double prevR;
                if (moved && double.TryParse(prevRatio,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out prevR))
                {
                    DeskKit.PenCircle(b, new Rect(SheetX + 48f, memoY + 4f, 560f, 32f));
                    DeskKit.DeltaArrow(b, SheetX + 16f, memoY + 12f, (float)ratio, (float)prevR);
                }
            }
            else
                DeskKit.LedgerMemo(b, sheet, "revenue last week", "$0",
                    "the floor waits for nobody");
            DeskKit.LedgerEnd(b, sheet);

            DeskKit.Footer(b,
                "bills are obligations — you change a bill by changing its source: the roof, the roster, the catalog, the debt",
                "single rule = subtotal · double rule = total — the book always balances to the hero · severance and notice periods survive removal",
                "", YFoot, YRules);
        }

        /// A row that names a SOURCE desk jumps there with a back pill (S7)
        /// and, when it names the switch too, lands spotlit (S2b).
        static void Row(BinderScreen b, DeskKit.LedgerBox sheet, BillRow r)
        {
            var cfg = new DeskKit.LedgerRowCfg { Dim = r.Dim };
            if (r.Press != "")
            {
                string target = r.Press;
                string control = r.Ctl;
                cfg.OnPress = () =>
                {
                    if (target == "tools") b.Desk["tools_open"] = !DBool(b, "tools_open");
                    else b.FocusDesk(target, control, "bills");
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
                Note = RentTrend(state), Press = "the works", Ctl = "capacity" });
            if (state.Sites != null)
                foreach (Site s in state.Sites)
                    rows.Add(new BillRow { Who = string.IsNullOrEmpty(s.Name) ? "a second roof" : s.Name,
                        What = "a roof of its own", Kind = "flat", Amt = s.RentWk,
                        Note = "opened wk " + s.OpenedWk,
                        Press = "the works", Ctl = "site_" + (s.Id ?? "") });
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
                NoteCol = marginSafe ? DrawnUI.Hex("5D7A50") : DrawnUI.Coral,
                Press = "the works", Ctl = "ticket" });
            int interest = 0;
            bool amortizing = false;
            bool onlyFee = false;
            // the dearest live note is the card the interest row lands on
            // ("note_<i>", the bank's control ids; the legacy shark = note_-1)
            int worstIdx = -1;
            double worstRate = 0.0;
            if (state.LoanPrincipal > 0)
            {
                interest += (int)Math.Ceiling(state.LoanPrincipal * SimBank.SharkRate);
                onlyFee = true;
                worstRate = SimBank.SharkRate;
            }
            for (int i = 0; i < state.Loans.Count; i++)
            {
                Loan l = state.Loans[i];
                if (l.Balance <= 0) continue;
                interest += (int)Math.Ceiling(l.Balance * l.RateWk);
                if (l.RateWk >= worstRate) { worstRate = l.RateWk; worstIdx = i; }
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
                    Kind = "scales", Amt = interest, Note = note, NoteCol = ncol,
                    Press = "the bank", Ctl = "note_" + worstIdx });
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
        /// One line per OFFER at the amount the bank actually bills (fixed_wk)
        /// — the label names the offer's first tool (+N more when it has
        /// several). THE ACCOUNTING RULES LAW: the fold's sublines always sum
        /// to their parent row, even when fixed_lines drifted from fixed_wk.
        static List<BillRow> ToolLines(GameState state)
        {
            var outRows = new List<BillRow>();
            foreach (Offer o in state.Offers)
            {
                int amt = (int)Math.Round(Math.Max(0.0, Math.Min(o.FixedWk, 10_000.0)));
                if (amt <= 0) continue;
                string label = "the tools";
                if (o.FixedLines != null && o.FixedLines.Count > 0)
                {
                    label = o.FixedLines[0].Label ?? "a tool";
                    if (o.FixedLines.Count > 1)
                        label += " +" + (o.FixedLines.Count - 1) + " more";
                }
                outRows.Add(new BillRow { Who = "· " + label,
                    What = o.Name ?? "", Kind = "flat", Amt = amt });
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
