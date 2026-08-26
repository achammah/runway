using System;
using System.Collections.Generic;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — COSTS · "spend" = THE ORG LEDGER on the ledger sheet (twin of
    /// desk_spend.gd; DAG2 W2 L-MONEY). THE BOOK IS THE LEVER: sections are
    /// the four engine buckets, rows are state.SpendBook lines, each bucket's
    /// subtotal IS the engine lever (SimSpendBook keeps them equal). The
    /// generated amt renders as a dim SUGGESTION; ADOPT copies it into live
    /// spend through the receipt path (coordinator ruling). Adding a line is
    /// free; stopping honors contract_notice — the notice bills through.
    /// </summary>
    public static class DeskSpend
    {
        public const string Question = "where does the money go?";

        const float SheetX = 10f;
        const float SheetW = 1120f;
        const float YSheet = 108f;
        const float YFoot = 806f;
        const float YRules = 840f;
        const int FoldAt = 6;

        public static string[] HeroSummary(GameState s)
        {
            int total = s.Budgets.Sales + s.Budgets.Care + s.Budgets.Rnd + s.Budgets.Office;
            return new[] { "$" + GameUi.Money(total) + "/wk", "the org book feeds four levers" };
        }

        public static void Draw(BinderScreen b)
        {
            GameState state = b.State;
            int swept = SimSpendBook.SweepLapsed(state, state.Week);
            if (swept > 0)
                state.LogAction("the notice ran out on " + swept + " stopped spend line"
                                + (swept == 1 ? "" : "s") + " — struck from the book");
            SimSpendBook.Reconcile(state);
            int total = SimSpendBook.BookLive(state);
            int suggested = SimSpendBook.BookSuggested(state);

            // ── the hero: the book's total, which the double-ruled TOTAL equals
            string big = "$" + GameUi.Money(total);
            b.L(big, SheetX, 6f, DeskKit.HeroSize, DrawnUI.Ink, 460f);
            float bw = DrawnUI.MeasureWidth(big, DeskKit.HeroSize);
            b.L("a week feeds the org", SheetX + bw + 16f, 22f, DeskKit.Row,
                Ink(0.7f), 420f);
            b.L("your book, written for YOUR business — every line sums into one of four engine buckets.",
                SheetX, 62f, DeskKit.Detail, Ink(0.6f), 760f);
            if (suggested > 0 && suggested != total)
                DeskKit.Arm(b, "adopt_book",
                    "adopt the suggested book — $" + GameUi.Money(suggested) + "/wk",
                    "start billing $" + GameUi.Money(suggested) + "/wk — sure?", 790f, 56f,
                    () => SimSpendBook.AdoptBook(state), 340f, DeskKit.Detail);

            // ── the sheet
            var sheet = DeskKit.LedgerSheet(b, SheetX, YSheet, SheetW, new List<DeskKit.LedgerCol>
            {
                new DeskKit.LedgerCol { Label = "line", W = 280f },
                new DeskKit.LedgerCol { Label = "buys", W = 230f },
                new DeskKit.LedgerCol { Label = "$/wk", W = 120f, Align = "right" },
                new DeskKit.LedgerCol { Label = "effect", W = 290f },
            }, 2, true, "all figures $/week");
            float effectX = sheet.Cols[3].X;
            bool foldAll = state.SpendBook.Count > FoldAt;
            string openB = DStr(b, "open_b");
            foreach (string bucket in SimSpendBook.Buckets)
            {
                List<int> idxs = SimSpendBook.LinesOf(state, bucket);
                if (idxs.Count == 0) continue;
                DeskKit.LedgerSection(b, sheet, SimSpendBook.BucketWord(bucket));
                int folded = 0;
                int foldedLive = 0;
                int foldedSugg = 0;
                foreach (int ii in idxs)
                {
                    int i = ii;
                    SpendLine line = state.SpendBook[i];
                    int live = SimSpendBook.LiveOf(line);
                    int sugg = line.Amt;
                    bool stopping = SimSpendBook.IsStopping(line);
                    bool pending = sugg > 0 && live != sugg && !stopping;
                    // the collapse law: a STOPPING line (a live countdown) never
                    // folds; the whole-book adopt arm covers folded suggestions
                    if (foldAll && openB != bucket && !stopping)
                    {
                        folded += 1;
                        foldedLive += live;
                        foldedSugg += pending ? sugg : 0;
                        continue;
                    }
                    float rowY = sheet.Cursor;
                    var cfg = new DeskKit.LedgerRowCfg();
                    if (stopping) cfg.Dim = true;
                    else
                    {
                        cfg.OnMinus = () => SimSpendBook.AdjustLive(state, i, -1);
                        cfg.OnPlus = () => SimSpendBook.AdjustLive(state, i, 1);
                        cfg.AtMin = live <= 0;
                        cfg.AtMax = SimSpendBook.AtCap(state, i);
                    }
                    // LONG-TEXT LAW: a book row is one line tall — a generated
                    // buys line (up to 60 chars) trims to its column, never
                    // wrapping over the rule into the row below
                    DeskKit.LedgerRow(b, sheet, new[] { line.Name ?? "",
                        FitBuys(line.Buys ?? ""),
                        "$" + GameUi.Money(live), "" }, cfg);
                    // the EFFECT cell carries the row's ONE control (mutation
                    // law: receipt-priced arm, two taps, Esc disarms)
                    if (stopping)
                        b.L("stops in " + SimSpendBook.NoticeLeft(line, state.Week)
                            + " wks — the contract bills through", effectX, rowY + 8f, 18f,
                            DrawnUI.Coral, 286f);
                    else if (pending)
                    {
                        DeskKit.Arm(b, "adopt_" + i,
                            "suggested $" + GameUi.Money(sugg) + " — adopt",
                            "bills $" + GameUi.Money(sugg) + "/wk — sure?",
                            effectX, rowY + 4f, () => SimSpendBook.AdoptLine(state, i), 186f, 19f);
                        if (live == 0)
                            DeskKit.Arm(b, "strike_" + i, "strike", "sure?",
                                effectX + 192f, rowY + 4f,
                                () => SimSpendBook.StopLine(state, i, state.Week), 90f, 19f);
                    }
                    else
                    {
                        string armedCap = line.ContractNotice > 0
                            ? "bills " + line.ContractNotice + " more wks — sure?"
                            : "stops $" + GameUi.Money(live) + "/wk now — sure?";
                        DeskKit.Arm(b, "stop_" + i, "stop the line", armedCap,
                            effectX, rowY + 4f,
                            () => SimSpendBook.StopLine(state, i, state.Week), 200f, 19f);
                    }
                }
                if (folded > 0)
                {
                    string openBucket = bucket;
                    DeskKit.LedgerRow(b, sheet, new[] { "the other " + folded + " lines",
                        "press to open", "$" + GameUi.Money(foldedLive),
                        foldedSugg > 0 ? "$" + GameUi.Money(foldedSugg) + " suggested" : "" },
                        new DeskKit.LedgerRowCfg { Dim = true,
                            OnPress = () => { b.Desk["open_b"] = openBucket; } });
                }
                DeskKit.LedgerSubtotal(b, sheet, "subtotal — " + SubWord(bucket),
                    "$" + GameUi.Money(SimSpendBook.BucketLive(state, bucket)),
                    EffectLine(state, bucket));
            }
            DeskKit.LedgerTotal(b, sheet, "total org spend", "$" + GameUi.Money(total));
            if (suggested > 0 && suggested != total)
                DeskKit.LedgerMemo(b, sheet, "the book suggests", "$" + GameUi.Money(suggested),
                    "adopt line by line, or the whole book above");
            float yEnd = DeskKit.LedgerEnd(b, sheet);

            // the door draws only when the sheet left it room
            if (yEnd + 44f <= YFoot - 8f) AddDoor(b, state, yEnd + 2f);

            DeskKit.Footer(b,
                "the subtotals ARE the engine's levers — closing, retention, building, people",
                "ink is free · brick is priced · a contract line bills its notice through · the era caps each bucket at $"
                + GameUi.Money(SimEngine.EraSpendCap(state.Era)) + "/wk", "", YFoot, YRules);
        }

        static string SubWord(string bucket)
        {
            switch (bucket)
            {
                case "sales": return "closing";
                case "care": return "retention";
                case "rnd": return "building";
                default: return "people";
            }
        }

        /// The bucket's live engine effect, exactly as the tick computes it.
        static string EffectLine(GameState state, string bucket)
        {
            int v = SimSpendBook.BucketLive(state, bucket);
            switch (bucket)
            {
                case "sales":
                    return v > 0 ? "+" + (v / 600.0).ToString("0.0") + " closers of capacity"
                                 : "founder sells alone";
                case "care":
                {
                    double cut = 30.0 * (1.0 - Math.Exp(-SimLabor.CareEff(state, v) / 1500.0));
                    if (v > 0) return "churn −" + Math.Round(cut) + "%";
                    return cut >= 1.0 ? "the support desk alone: churn −" + Math.Round(cut) + "%"
                                      : "nobody picks up";
                }
                case "rnd":
                    return v > 0 ? "+" + (v / 1200.0).ToString("0.0") + " product/wk · debt pays down"
                                 : "no extra shipping";
                default:
                {
                    double mg = 3.0 * (1.0 - Math.Exp(-v / 800.0));
                    return v > 0 ? "+" + mg.ToString("0.0") + " morale/wk" : "instant coffee, cold room";
                }
            }
        }

        /// The add-a-line door: bucket picker -> staged receipt -> the ADD arm.
        static void AddDoor(BinderScreen b, GameState state, float y)
        {
            bool full = state.SpendBook.Count >= SimSpendBook.BookCap;
            if (DStr(b, "mode") != "add")
            {
                if (full)
                {
                    b.L("the book is full — stop a line before adding one", SheetX, y + 6f,
                        DeskKit.Law, Ink(0.4f), 500f);
                    return;
                }
                DeskKit.Word(b, "+ add a line", SheetX, y,
                    () => { b.Desk["mode"] = "add"; b.Desk.Remove("staged"); },
                    DeskKit.Detail, Ink(0.7f), 220f);
                return;
            }
            string staged = DStr(b, "staged");
            if (staged == "")
            {
                b.L("into which bucket?", SheetX, y + 4f, DeskKit.Detail, Ink(0.7f), 220f);
                float x = SheetX + 220f;
                foreach (string bucket in SimSpendBook.Buckets)
                {
                    string pick = bucket;
                    DeskKit.Word(b, SimSpendBook.BucketWord(bucket), x, y + 4f,
                        () => { b.Desk["staged"] = pick; }, DeskKit.Detail, DrawnUI.Ink, 200f);
                    x += 208f;
                }
                return;
            }
            b.L("a new line in " + SimSpendBook.BucketWord(staged)
                + " — free to add, $0/wk until you raise it (Esc backs out)", SheetX, y + 4f,
                DeskKit.Detail, Ink(0.7f), 760f);
            DeskKit.Arm(b, "add_line", "ADD THE LINE", "write it into the book — sure?",
                SheetX + 780f, y + 2f, () =>
                {
                    SimSpendBook.AddLine(state, staged);
                    b.Desk["mode"] = "";
                    b.Desk.Remove("staged");
                }, 300f, DeskKit.Detail);
        }

        static Color Ink(float a) { return DrawnUI.WithAlpha(DrawnUI.Ink, a); }

        /// LONG-TEXT LAW: the buys cell is one line — ~30 chars fill its
        /// 218px column at the row hand; longer generated lines trim honestly.
        static string FitBuys(string s)
        {
            return s.Length <= 31 ? s : s.Substring(0, 30).TrimEnd() + "…";
        }

        static string DStr(BinderScreen b, string key)
        {
            object v;
            return b.Desk.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }

        public static void Handle(BinderScreen b, string id)
        {
            // every control on this sheet carries its own closure
        }
    }
}
