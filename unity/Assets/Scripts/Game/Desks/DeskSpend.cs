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
    ///
    /// DAG3 Wave B: S1 zero state (the bare keyless book), S4 subtotal
    /// receipts with the MARGINAL line, S5 hero delta + pen circles on moved
    /// lines, S3 DO lane, S15 adopt-the-book suggestion. The ask strip owns
    /// its own y: when it draws, the sheet drops 8px clear.
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

        /// S8 — the rail's micro-status: the book total, short form.
        public static string MicroStatus(GameState s)
        {
            return SimOwnership.MoneyShort(SimSpendBook.BookLive(s));
        }

        /// S8 — the org ledger never sleeps: the founder's own coffee is a line.
        public static bool IsDormant(GameState s)
        {
            return false;
        }

        /// S15 — the desk's one suggestion: adopt the book when it
        /// out-suggests the live spend. A jump chip — the adopt arm stays
        /// the only mutation door.
        public static List<Dictionary<string, object>> Suggestions(GameState s)
        {
            int live = SimSpendBook.BookLive(s);
            int sugg = SimSpendBook.BookSuggested(s);
            var rows = new List<Dictionary<string, object>>();
            if (sugg > 0 && sugg > live)
                rows.Add(new Dictionary<string, object>
                {
                    { "label", "adopt the book — $" + GameUi.Money(sugg) + "/wk" },
                    { "kind", "jump" },
                    { "payload", new Dictionary<string, object>
                        { { "desk", "spend" }, { "control", "adopt_book" } } },
                });
            return rows;
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

            // ── S1 · the zero state: the bare keyless book — nothing live,
            // nothing suggested — teaches before it opens (the one action
            // reveals the sheet; a generated book's suggestions ARE the
            // designed week-1 state).
            if (total == 0 && suggested == 0 && !b.Desk.ContainsKey("zero_off"))
            {
                DeskKit.ZeroState(b, new DeskKit.ZeroStateCfg
                {
                    WillShow = "the org ledger — closing, retention, building, people",
                    WouldLine = "$600/wk into closing WOULD buy one closer of capacity — $1,200 into building WOULD ship +1 product a week",
                    ActionLabel = "open the book — raise a line",
                    ActionCb = () => { b.Desk["zero_off"] = true; },
                    WakesHint = "lines bill only when you raise them — ink is free",
                });
                return;
            }

            // ── S5 · what changed since the last open: read the store ONCE
            // per visit (a refresh must not eat the news), then circle.
            HashSet<int> circled;
            object co;
            if (!b.Desk.TryGetValue("_circ", out co))
            {
                var set = new HashSet<int>();
                string heroPrev = b.SeenPrev("spend", "book_total");
                b.Seen("spend", "book_total", total.ToString());
                for (int ci = 0; ci < state.SpendBook.Count; ci++)
                {
                    SpendLine cl = state.SpendBook[ci];
                    string ck = "l" + ci + "_" + (cl.Name ?? "");
                    string cv = SimSpendBook.LiveOf(cl) + "|"
                                + (SimSpendBook.IsStopping(cl) ? "s" : "o");
                    string was = b.SeenPrev("spend", ck);
                    if (b.Seen("spend", ck, cv) && was != "") set.Add(ci);
                }
                b.Desk["_circ"] = set;
                b.Desk["_hero_prev"] = heroPrev;
                circled = set;
            }
            else circled = co as HashSet<int> ?? new HashSet<int>();

            // ── the hero: the book's total, which the double-ruled TOTAL equals
            string big = "$" + GameUi.Money(total);
            b.L(big, SheetX, 6f, DeskKit.HeroSize, DrawnUI.Ink, 460f);
            float bw = DrawnUI.MeasureWidth(big, DeskKit.HeroSize);
            object hpv;
            string hp = b.Desk.TryGetValue("_hero_prev", out hpv) && hpv != null
                ? hpv.ToString() : "";
            float hpF;
            if (hp != "" && float.TryParse(hp, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out hpF))
                DeskKit.DeltaArrow(b, SheetX + bw + 18f, 4f, total, hpF);
            b.L("a week feeds the org", SheetX + bw + 16f, 22f, DeskKit.Row,
                Ink(0.7f), 420f);
            b.L("your book, written for YOUR business — every line sums into one of four engine buckets.",
                SheetX, 62f, DeskKit.Detail, Ink(0.6f), 760f);
            // RED MEANS ACT, AND THE PAGE NAMES THE ASK — the kit's ask strip,
            // born on this desk (S2a). R5 — the strip renders in its own slot
            // (96-118) and the sheet holds the content slot whether or not the
            // desk is red: stability beats density.
            float sheetY = DeskKit.ContentY0;
            DeskKit.AskStrip(b, "spend", SheetX, 86f, 1000f, "adopt the book or fund a line");
            if (suggested > 0 && suggested != total)
            {
                DeskKit.Arm(b, "adopt_book",
                    "adopt the suggested book — $" + GameUi.Money(suggested) + "/wk",
                    "start billing $" + GameUi.Money(suggested) + "/wk — sure?", 790f, 56f,
                    () => SimSpendBook.AdoptBook(state), 340f, DeskKit.Detail);
                // S2b — the suggestion chip and the red jump land HERE, spotlit
                b.MarkControl("adopt_book", new Rect(782f, 54f, 356f, 46f));
            }

            // ── the sheet
            var sheet = DeskKit.LedgerSheet(b, SheetX, sheetY, SheetW, new List<DeskKit.LedgerCol>
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
                    // S5 — the pen circles a line that moved since the last
                    // open (adopted, stopped, struck or re-levered)
                    if (circled.Contains(i))
                        DeskKit.PenCircle(b, new Rect(SheetX + 8f, rowY + 3f, 540f,
                            DeskKit.LgRowH - 6f));
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
                // S4 — PRESS THE SUBTOTAL: the effect receipt with the
                // MARGINAL line, the number that teaches diminishing returns
                float subY = sheet.Cursor;
                DeskKit.LedgerSubtotal(b, sheet, "subtotal — " + SubWord(bucket),
                    "$" + GameUi.Money(SimSpendBook.BucketLive(state, bucket)),
                    EffectLine(state, bucket));
                b.MarkControl("sub_" + bucket, new Rect(SheetX, subY, SheetW, DeskKit.LgRowH));
                DeskKit.PressReceipt(b, "sub_" + bucket, ReceiptTitle(bucket),
                    ReceiptLines(state, bucket));
            }
            DeskKit.LedgerTotal(b, sheet, "total org spend", "$" + GameUi.Money(total));
            if (suggested > 0 && suggested != total)
                DeskKit.LedgerMemo(b, sheet, "the book suggests", "$" + GameUi.Money(suggested),
                    "adopt line by line, or the whole book above");
            float yEnd = DeskKit.LedgerEnd(b, sheet);

            // the door draws only when the sheet left it room
            if (yEnd + 44f <= YFoot - 8f) AddDoor(b, state, yEnd + 2f);

            // ── S3 · the DO lane: the desk's primary actions in the kit's one
            // slot (parked while the add door is mid-flow)
            if (DStr(b, "mode") != "add")
            {
                var actions = new List<DeskKit.DoAction>();
                if (suggested > 0 && suggested > total)
                    actions.Add(new DeskKit.DoAction
                    {
                        Label = "adopt the book — $" + GameUi.Money(suggested) + "/wk",
                        Tier = "two-tap",
                        Cb = () => SimSpendBook.AdoptBook(state),
                    });
                if (state.SpendBook.Count < SimSpendBook.BookCap)
                    actions.Add(new DeskKit.DoAction
                    {
                        Label = "add a line",
                        Tier = "",
                        Cb = () => { b.Desk["mode"] = "add"; b.Desk.Remove("staged"); },
                    });
                DeskKit.DoLane(b, actions);
            }

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

        // ── S4 · the subtotal's effect receipt: the terms, then THE MARGINAL
        // line — "next $100 buys…" is the diminishing-returns lesson ─────────

        static string ReceiptTitle(string bucket)
        {
            switch (bucket)
            {
                case "sales": return "closing — what sales spend buys";
                case "care": return "retention — what care spend buys";
                case "rnd": return "building — what R&D spend buys";
                default: return "people — what office spend buys";
            }
        }

        /// The receipt speaks the tick's own curves and derives the marginal
        /// at the CURRENT spend (twin of desk_spend.gd `_receipt_lines`).
        static List<DeskKit.TicketLine> ReceiptLines(GameState state, string bucket)
        {
            double v = SimSpendBook.BucketLive(state, bucket);
            var lines = new List<DeskKit.TicketLine>
            {
                new DeskKit.TicketLine { Label = "live spend",
                    Value = "$" + GameUi.Money((int)v) + "/wk" },
            };
            switch (bucket)
            {
                case "sales":
                    lines.Add(new DeskKit.TicketLine { Label = "buys now",
                        Value = "+" + (v / 600.0).ToString("0.0") + " closers of capacity" });
                    lines.Add(new DeskKit.TicketLine { Label = "next $100 buys",
                        Value = "+" + (100.0 / 600.0).ToString("0.00") + " closers" });
                    lines.Add(new DeskKit.TicketLine { Label = "the curve",
                        Value = "linear — $600 per closer" });
                    break;
                case "care":
                {
                    double cutNow = 30.0 * (1.0 - Math.Exp(-SimLabor.CareEff(state, (int)v) / 1500.0));
                    double cutNext = 30.0 * (1.0 - Math.Exp(-SimLabor.CareEff(state, (int)v + 100) / 1500.0));
                    lines.Add(new DeskKit.TicketLine { Label = "buys now",
                        Value = "churn −" + Math.Round(cutNow) + "%" });
                    lines.Add(new DeskKit.TicketLine { Label = "next $100 buys",
                        Value = "churn −" + (cutNext - cutNow).ToString("0.0") + " pts more" });
                    lines.Add(new DeskKit.TicketLine { Label = "the curve",
                        Value = "diminishing — early dollars cut deepest" });
                    break;
                }
                case "rnd":
                    lines.Add(new DeskKit.TicketLine { Label = "buys now",
                        Value = "+" + (v / 1200.0).ToString("0.00") + " product/wk" });
                    lines.Add(new DeskKit.TicketLine { Label = "next $100 buys",
                        Value = "+" + (100.0 / 1200.0).ToString("0.00") + " product/wk" });
                    lines.Add(new DeskKit.TicketLine { Label = "the curve",
                        Value = "linear · debt pays down while funded" });
                    break;
                default:
                {
                    double mgNow = 3.0 * (1.0 - Math.Exp(-v / 800.0));
                    double mgNext = 3.0 * (1.0 - Math.Exp(-(v + 100.0) / 800.0));
                    lines.Add(new DeskKit.TicketLine { Label = "buys now",
                        Value = "+" + mgNow.ToString("0.0") + " morale/wk" });
                    lines.Add(new DeskKit.TicketLine { Label = "next $100 buys",
                        Value = "+" + (mgNext - mgNow).ToString("0.00") + " morale/wk" });
                    lines.Add(new DeskKit.TicketLine { Label = "the curve",
                        Value = "diminishing — comfort saturates" });
                    break;
                }
            }
            return lines;
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
