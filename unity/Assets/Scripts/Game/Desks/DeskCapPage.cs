using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — THE COMPANY · "cap table" (twin of desk_cap_page.gd). W2 lane:
    /// L-OWN, reworked to the locked pick: hero (your % + paper worth) ->
    /// THE SLICES (ledger sheet, double-ruled 100%) -> THE DILUTION STORY
    /// beside IF SOLD TODAY (SimOwnership.Waterfall, pure) -> the pool door
    /// (receipt + two-tap). The old wheel desk (DeskCap) is no longer
    /// embedded — retirement candidate.
    ///
    /// DAG3 (13-binder-ux): THE VALUATION SLIDER — zone 3 becomes "if sold at
    /// $X", a stepped −/+ pair walking SaleMults × today's price, the
    /// waterfall re-asked LIVE through SimOwnership.Waterfall (pure —
    /// recomputed at every step press, never cached); dilution steps press
    /// into their event receipts (S4); the hero answers with the waterfall's
    /// OWN number and presses into its receipt; ask strip (S2), zero state
    /// (S1), DO lane [expand — the pool] (S3), the slice's delta arrow (S5).
    /// </summary>
    public static class DeskCapPage
    {
        public const string Question = "who owns what and what's the company worth?";

        public const double PoolStep = 2.0;

        /// The slider's named ladder: ~0.2×..3× today's price, 1× centered.
        static readonly double[] SaleMults = { 0.2, 0.35, 0.5, 0.75, 1.0, 1.5, 2.0, 2.5, 3.0 };

        /// S8 — dormant while the book is blank at the garage; the tab stays
        /// on the map (the map is the curriculum) and wakes when paper lands.
        public static bool IsDormant(GameState s)
        {
            return s.Era == "garage" && BookEmpty(s);
        }

        /// S10 — your slice is the tab in one glance.
        public static string MicroStatus(GameState s)
        {
            if (BookEmpty(s)) return "";
            return s.FounderPct.ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        /// A cap book with nothing on it but the founder's own 100%.
        static bool BookEmpty(GameState s)
        {
            return s.Instruments.Count == 0 && s.Esop == null && s.Cofounders.Count == 0
                && s.OptionPoolPct <= 0.0 && s.Board == null;
        }

        public static string[] HeroSummary(GameState s)
        {
            var wfHero = SimOwnership.Waterfall(s, SimEngine.Valuation(s));
            object tkO; wfHero.TryGetValue("your_take", out tkO);
            int paper = tkO != null ? Convert.ToInt32(tkO) : 0;
            return new[] { "you own " + s.FounderPct.ToString("0") + "%",
                "≈ $" + SimOwnership.MoneyShort(paper).TrimStart('$') + " on paper — paper, not cash" };
        }

        static string DMode(BinderScreen b)
        {
            object v;
            return b.Desk.TryGetValue("mode", out v) ? Convert.ToString(v) : "";
        }

        public static void Draw(BinderScreen b)
        {
            GameState state = b.State;
            if (DMode(b) == "pool") { DrawPoolPage(b, state); return; }
            // S1 — a blank book opens on the designed first week
            if (BookEmpty(state)) { Zero(b, state); return; }
            int val = SimEngine.Valuation(state);
            // ONE BASIS with HeroSummary AND zone 3 (the waterfall's own
            // answer at today's price) — the three can never disagree.
            Dictionary<string, object> wf0 = SimOwnership.Waterfall(state, val);
            int paper = WfI(wf0, "your_take");
            string big = "you own " + state.FounderPct.ToString("0") + "% · ≈ "
                + SimOwnership.MoneyShort(paper) + " on paper";
            float y = DeskKit.HeroBand(b, big,
                "paper, not cash — it becomes money only at an exit, after everyone ahead of you.");
            // S5 — the slice wears the arrow when it moved since the last open
            float bigW = DrawnUI.MeasureWidth(big, DeskKit.HeroBig);
            string prevPct = b.SeenPrev("cap table", "pct");
            b.Seen("cap table", "pct", state.FounderPct.ToString("0.0", CultureInfo.InvariantCulture));
            if (prevPct != "")
            {
                float pp;
                float.TryParse(prevPct, NumberStyles.Float, CultureInfo.InvariantCulture, out pp);
                DeskKit.DeltaArrow(b, 10f + bigW + 14f, 30f, (float)state.FounderPct, pp);
            }
            // S4 — the hero presses into the receipt that made its number
            DeskKit.PressReceipt(b, new Rect(10f, 6f, Mathf.Min(bigW + 8f, 720f), 62f),
                "≈ paper, the honest way", new List<DeskKit.TicketLine>
                {
                    new DeskKit.TicketLine { Label = "priced today", Value = "$" + GameUi.Money(val) },
                    new DeskKit.TicketLine { Label = "debts die first",
                        Value = "−$" + GameUi.Money(WfI(wf0, "debts")) },
                    new DeskKit.TicketLine { Label = "preferences next",
                        Value = "−$" + GameUi.Money(WfI(wf0, "prefs_paid")) },
                    new DeskKit.TicketLine { Label = "your " + state.FounderPct.ToString("0") + "% of the split",
                        Value = "≈$" + GameUi.Money(paper), Col = DrawnUI.Sage },
                });
            TextMeshProUGUI pr = b.L("the company priced at $" + GameUi.Money(val),
                760f, 10f, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f), 360f);
            pr.alignment = TextAlignmentOptions.TopRight;
            TextMeshProUGUI bl = b.L(BoardLine(state), 760f, 40f, 18f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 360f);
            bl.alignment = TextAlignmentOptions.TopRight;
            // S2 — red speaks on the page: the book's asks in one red line
            DeskKit.AskStrip(b, "cap table", 10f, 100f, 730f, "expand the pool");

            // ── zone 1 · THE SLICES
            List<string[]> rows = SliceRows(state);
            int memoN = StackCount(state) > 0 ? 1 : 0;
            float z1H = 78f + DeskKit.LgHeadH + rows.Count * DeskKit.LgRowH
                + memoN * DeskKit.LgRowH + DeskKit.LgTotH + 22f;
            DeskKit.CardBox z1 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, z1H, 1, "the slices",
                "every holder, every class, every preference — the book of who owns what");
            if (state.Esop != null)
            {
                DeskKit.Word(b, "expand the pool +" + PoolStep.ToString("0") + "%",
                    z1.ContentX + 880f, z1.Y + 8f, () => { b.Desk["mode"] = "pool"; },
                    DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 220f);
                // S2b — "pool empty" lands here spotlit
                b.MarkControl("expand_pool", new Rect(z1.ContentX + 872f, z1.Y + 4f,
                    244f, 42f));
            }
            var cols = new List<DeskKit.LedgerCol>
            {
                new DeskKit.LedgerCol { Label = "holder", W = 300f },
                new DeskKit.LedgerCol { Label = "instrument", W = 190f },
                new DeskKit.LedgerCol { Label = "put in", W = 150f, Align = "right" },
                new DeskKit.LedgerCol { Label = "owns", W = 130f, Align = "right" },
                new DeskKit.LedgerCol { Label = "preferences", W = 254f, Align = "right" },
            };
            DeskKit.LedgerBox sheet = DeskKit.LedgerSheet(b, z1.ContentX - 4f, z1.Cursor,
                1088f, cols, 3, false, "");
            foreach (string[] r in rows)
            {
                float rowY = sheet.Cursor;
                DeskKit.LedgerRow(b, sheet, r);
                // S2b — the pool's own book line: "esop_row" for the cliff
                // rows, "pool" for cross-desk jumps (team's vesting bar)
                if (r.Length > 0 && ((r[0] ?? "").StartsWith("the ESOP pool", StringComparison.Ordinal)
                    || (r[0] ?? "").StartsWith("the option pool", StringComparison.Ordinal)))
                {
                    var poolRect = new Rect(z1.ContentX - 4f, rowY,
                        1088f, sheet.Cursor - rowY);
                    b.MarkControl("esop_row", poolRect);
                    b.MarkControl("pool", poolRect);
                }
            }
            if (memoN > 0)
                DeskKit.LedgerMemo(b, sheet,
                    StackCount(state).ToString() + " SAFE/note(s) waiting to convert", "",
                    "≈" + Gd.F(SimOwnership.StackDilutionAt(state, val), 1)
                    + "% more if it converts");
            DeskKit.LedgerTotal(b, sheet, "the whole pie", "100%", DrawnUI.Ink);
            DeskKit.LedgerEnd(b, sheet);
            y = z1.Bottom + 12f;

            // ── zone 2 · THE DILUTION STORY beside zone 3 · IF SOLD TODAY.
            // A DEEP BOOK (5+ slice rows) pushes this band toward the
            // teaching foot: the zones shrink to end above it, the bars'
            // note lines retire into their receipts, and the DO lane yields
            // (the z1 header word stays the door).
            bool deep = y > 544f;
            float zh = deep ? Mathf.Min(268f, 806f - y) : 268f;
            DeskKit.CardBox z2 = DeskKit.Zone(b, DeskKit.XId, y, 646f, zh, 2, "the dilution story",
                "your slice shrinks at events — and can be worth more every time it does");
            List<DeskKit.DilStep> steps = DilutionSteps(state, val);
            if (deep)
                foreach (DeskKit.DilStep sd0 in steps) sd0.Note = "";
            if (steps.Count >= 2)
            {
                DeskKit.DilutionBar(b, z2.ContentX, z2.Cursor + 2f, 610f, steps);
                // S4 — every dilution step presses into its event's receipt
                // (rects mirror DilutionBar's cell walk: min(w/n, 190))
                float cell = Mathf.Min(610f / steps.Count, 190f);
                for (int i = 0; i < steps.Count; i++)
                {
                    List<DeskKit.TicketLine> rec = StepReceipt(state, steps[i], val);
                    if (rec.Count == 0) continue;
                    DeskKit.PressReceipt(b, new Rect(z2.ContentX + i * cell,
                        z2.Cursor + 2f, cell - 4f, 184f), steps[i].Label, rec);
                }
            }
            else
                DeskKit.Empty(b, z2.ContentX, z2.Cursor + 8f,
                    "no rounds on the book yet — you hold "
                    + Gd.F(state.FounderPct, 0) + "% today.",
                    "rounds, pools and conversions will draw themselves here");
            // ── zone 3 · THE VALUATION SLIDER — "if sold at $X", LIVE.
            // SimOwnership.Waterfall(state, price) is PURE: re-asked at every
            // step press (the refresh redraw), never cached — the binder's
            // own −/+ grammar walks SaleMults (twin parity with Godot).
            object siObj;
            int si = b.Desk.TryGetValue("sale_i", out siObj) && siObj != null
                ? Convert.ToInt32(siObj) : 4;
            si = Mathf.Clamp(si, 0, SaleMults.Length - 1);
            int price = Math.Max((int)(val * SaleMults[si]), 1);
            DeskKit.CardBox z3 = DeskKit.Zone(b, DeskKit.XId + 660f, y, 470f, zh, 3,
                "if sold at " + SimOwnership.MoneyShort(price) + " · ×"
                + SaleMults[si].ToString("0.##", CultureInfo.InvariantCulture), "");
            float sx = z3.ContentX;
            float sy = z3.Cursor + 6f;
            int siNow = si;
            // the drawn track: nine inked steps, filled to the marker (pips)
            if (si > 0)
                DeskKit.Word(b, "−", sx, sy - 12f, () => { b.Desk["sale_i"] = siNow - 1; },
                    26f, DrawnUI.Ink, 40f);
            else
                b.L("−", sx, sy - 12f, 26f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.25f), 40f);
            DeskKit.Pips(b, sx + 56f, sy, si + 1, SaleMults.Length);
            if (si < SaleMults.Length - 1)
                DeskKit.Word(b, "+", sx + 262f, sy - 12f, () => { b.Desk["sale_i"] = siNow + 1; },
                    26f, DrawnUI.Ink, 40f);
            else
                b.L("+", sx + 262f, sy - 12f, 26f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.25f), 40f);
            b.MarkControl("val_slider", new Rect(sx - 4f, sy - 14f, 434f, 44f));
            Dictionary<string, object> wf = SimOwnership.Waterfall(state, price);
            z3.Cursor = sy + 36f;
            DeskKit.MoneyRow(b, z3, "the bank — debts first", "$" + GameUi.Money(WfI(wf, "debts")));
            DeskKit.MoneyRow(b, z3, "preferences next", "$" + GameUi.Money(WfI(wf, "prefs_paid")));
            DeskKit.MoneyRow(b, z3, "then the split — you'd see", "≈$" + GameUi.Money(WfI(wf, "your_take")),
                DrawnUI.Sage);
            if (!deep)
                DeskKit.FitLine(b, "below ≈$" + GameUi.Money(WfI(wf, "breakeven"))
                    + " the preferences eat everything — walk the price and watch",
                    z3.ContentX, z3.Cursor + 4f, 17f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 430f);

            DeskKit.Footer(b, Costline(state),
                "the cap table moves only through rounds, offers and events — never by hand · rounds happen at THE RAISE",
                "", 812f, 846f);
            // S3 — the desk's one live action, in the one slot every desk
            // keeps (a deep book runs its zones through the anchor — the
            // lane yields; the z1 header word remains the door)
            if (!deep && state.Esop != null)
                DeskKit.DoLane(b, new List<DeskKit.DoAction>
                {
                    new DeskKit.DoAction { Label = "expand — the pool", Tier = "",
                        Cb = () => { b.Desk["mode"] = "pool"; } },
                });
        }

        /// S1 — the designed first week: the whole pie is yours; the page
        /// promises the waterfall lesson before there is anything to draw.
        static void Zero(BinderScreen b, GameState state)
        {
            int val = SimEngine.Valuation(state);
            DeskKit.ZeroState(b, new DeskKit.ZeroStateCfg
            {
                WillShow = "WHO OWNS WHAT — every slice, and who gets paid first at an exit",
                WouldLine = "you hold 100% today — the world prices the company at $"
                    + GameUi.Money(val)
                    + "; a first round WOULD trade ≈15-20% of it for real money",
                ActionLabel = "open the raise ->",
                ActionCb = () => b.FocusDesk("the raise", "", "cap table"),
                WakesHint = "wakes the week the first paper signs — a SAFE, a note, a pool or a round",
            });
        }

        /// S4 — the receipt behind a dilution step, from the instruments/esop
        /// history the step was derived from. Empty = a plain bar.
        static List<DeskKit.TicketLine> StepReceipt(GameState state, DeskKit.DilStep step,
                                                    int val)
        {
            string label = step.Label ?? "";
            var lines = new List<DeskKit.TicketLine>();
            if (label == "day 0")
            {
                lines.Add(new DeskKit.TicketLine { Label = "the founding split", Value = "you 100%" });
                lines.Add(new DeskKit.TicketLine { Label = "raised", Value = "$0" });
                return lines;
            }
            if (label == "the SAFE stack")
            {
                foreach (Instrument idd in state.Instruments)
                    if ((idd.Kind == "safe" || idd.Kind == "note" || idd.Kind == "bridge")
                        && idd.Pct <= 0.0)
                        lines.Add(new DeskKit.TicketLine
                        {
                            Label = idd.Kind + " — " + idd.Holder,
                            Value = "$" + SimOwnership.Money(idd.Amount) + " · cap "
                                + SimOwnership.MoneyShort(idd.Cap),
                        });
                lines.Add(new DeskKit.TicketLine
                {
                    Label = "if priced today",
                    Value = "≈" + Gd.F(SimOwnership.StackDilutionAt(state, val), 1) + "% at once",
                    Col = DrawnUI.Coral,
                });
                return lines;
            }
            if (label == "the pool")
            {
                if (state.Esop == null) return lines;
                lines.Add(new DeskKit.TicketLine { Label = "the pool",
                    Value = Gd.F(state.Esop.PoolPct, 1) + "% of the company" });
                lines.Add(new DeskKit.TicketLine { Label = "free to grant",
                    Value = Gd.F(SimOwnership.PoolFree(state), 1) + "%" });
                lines.Add(new DeskKit.TicketLine { Label = "who paid for it",
                    Value = "every holder — you first", Col = DrawnUI.Coral });
                return lines;
            }
            if (label.StartsWith("wk", StringComparison.Ordinal)
                || label.StartsWith("+", StringComparison.Ordinal))
            {
                foreach (Instrument idd2 in state.Instruments)
                {
                    if (idd2.Kind != "priced" || idd2.Pct <= 0.0) continue;
                    lines.Add(new DeskKit.TicketLine
                    {
                        Label = "wk" + idd2.SignedWk + " — " + idd2.Holder,
                        Value = "$" + SimOwnership.Money(idd2.Amount) + " for "
                            + Gd.F(idd2.Pct, 1) + "%",
                    });
                }
                while (lines.Count > 4) lines.RemoveAt(0);
                return lines;
            }
            if (label == "now")
            {
                lines.Add(new DeskKit.TicketLine { Label = "your slice",
                    Value = Gd.F(state.FounderPct, 1) + "%" });
                lines.Add(new DeskKit.TicketLine { Label = "priced today",
                    Value = "$" + SimOwnership.Money(val) });
                lines.Add(new DeskKit.TicketLine { Label = "on paper",
                    Value = "≈$" + SimOwnership.Money(
                        WfI(SimOwnership.Waterfall(state, val), "your_take"))
                        + " — after the waterfall",
                    Col = DrawnUI.Sage });
                return lines;
            }
            return lines;
        }

        static void DrawPoolPage(BinderScreen b, GameState state)
        {
            DeskKit.Back(b, "back to the cap table", () => { b.Desk.Remove("mode"); });
            float y = 64f;
            y = DeskKit.HeroBand(b, "expand the pool",
                "a bigger pool hires better people — and the slice comes out of EVERY holder, you first.",
                DrawnUI.Ink, y);
            double pool = state.Esop != null ? state.Esop.PoolPct : 0.0;
            double keep = 1.0 - PoolStep / 100.0;
            var lines = new List<DeskKit.TicketLine>
            {
                new DeskKit.TicketLine { Label = "the pool today",
                    Value = Gd.F(pool, 1) + "% (" + Gd.F(SimOwnership.PoolFree(state), 1) + "% free)" },
                new DeskKit.TicketLine { Label = "after the expansion",
                    Value = Gd.F(pool * keep + PoolStep, 1) + "%" },
                new DeskKit.TicketLine { Label = "your slice",
                    Value = Gd.F(state.FounderPct, 1) + "% -> " + Gd.F(state.FounderPct * keep, 1) + "%",
                    Col = DrawnUI.Coral },
            };
            foreach (Cofounder cf in state.Cofounders)
            {
                double eq = cf.EquityDiluted ?? cf.Equity;
                lines.Add(new DeskKit.TicketLine { Label = string.IsNullOrEmpty(cf.Name) ? "cofounder" : cf.Name,
                    Value = Gd.F(eq, 1) + "% -> " + Gd.F(eq * keep, 1) + "%" });
            }
            y = DeskKit.Ticket(b, DeskKit.XId + 40f, y + 6f, 560f, "the expansion, priced",
                lines, "new grants it can fund", "+" + PoolStep.ToString("0") + "% of the company",
                "ink on paper — no cash moves; the dilution is the price", DrawnUI.Sage);
            DeskKit.Arm(b, "pool_expand", "SIGN THE EXPANSION", "press again — every holder dilutes",
                DeskKit.XId + 40f, y + 8f, () =>
                {
                    SimOwnership.ExpandPool(b.State, PoolStep);
                    b.Desk.Remove("mode");
                }, 420f);
            DeskKit.Footer(b, "grants raise labor-market appeal — comp is a mix, not a number",
                "Esc abandons — nothing moves until the second tap", "", 812f, 846f);
        }

        // ───────────────────── the page's own reads ────────────────────────

        static int WfI(Dictionary<string, object> wf, string k)
        {
            object v;
            return wf.TryGetValue(k, out v) && v != null ? Convert.ToInt32(v) : 0;
        }

        static string BoardLine(GameState state)
        {
            if (state.Board == null) return "no board — nobody to answer to yet";
            int seats = state.BoardSeatsInvestor;
            int strikes = state.Board.Strikes;
            return "board: " + seats + " seat" + (seats == 1 ? "" : "s") + " theirs · "
                + (strikes == 0 ? "covenant met, 0 strikes" : "strike " + strikes + " on the record");
        }

        static int StackCount(GameState state)
        {
            int n = 0;
            foreach (Instrument inst in state.Instruments)
                if ((inst.Kind == "safe" || inst.Kind == "note" || inst.Kind == "bridge")
                    && inst.Pct <= 0.0)
                    n += 1;
            return n;
        }

        static List<string[]> SliceRows(GameState state)
        {
            var rows = new List<string[]>();
            rows.Add(new[] { "you", "common", "sweat", Gd.F(state.FounderPct, 1) + "%", "last in line" });
            foreach (Cofounder cf in state.Cofounders)
                rows.Add(new[] { (string.IsNullOrEmpty(cf.Name) ? "?" : cf.Name) + " — cofounder",
                    "common", "sweat", Gd.F(cf.EquityDiluted ?? cf.Equity, 1) + "%", "last in line" });
            foreach (Instrument inst in state.Instruments)
            {
                if (inst.Pct <= 0.0) continue;
                string label = inst.Kind == "priced" ? "preferred" : "converted " + inst.Kind;
                rows.Add(new[] { inst.Holder, label, "$" + SimOwnership.Money(inst.Amount),
                    Gd.F(inst.Pct, 1) + "%",
                    inst.Prefs > 0.0 ? Gd.F(inst.Prefs, 0) + "× non-participating" : "converts with common" });
            }
            if (state.Esop != null)
            {
                double pool = state.Esop.PoolPct;
                double free = SimOwnership.PoolFree(state);
                rows.Add(new[] { "the ESOP pool -> team", "options", "—", Gd.F(pool, 1) + "%",
                    Gd.F(pool - free, 1) + "% granted · " + Gd.F(free, 1) + "% free" });
            }
            else if (state.OptionPoolPct > 0.0)
            {
                // a pool promised before the esop book opened — still a slice
                rows.Add(new[] { "the option pool (promised)", "options", "—",
                    Gd.F(state.OptionPoolPct, 1) + "%", "grants start with the esop book" });
            }
            // THE ACCOUNTING RULES LAW: the named slices + the rest = the whole
            // pie — whatever the book cannot name is still shown
            double named = state.FounderPct;
            foreach (Cofounder cf2 in state.Cofounders)
                named += cf2.EquityDiluted ?? cf2.Equity;
            foreach (Instrument inst2 in state.Instruments)
                named += Math.Max(inst2.Pct, 0.0);
            if (state.Esop != null) named += state.Esop.PoolPct;
            else if (state.OptionPoolPct > 0.0) named += state.OptionPoolPct;
            double rest = 100.0 - named;
            if (rest >= 0.5)
                rows.Add(new[] { "the rest — smaller holders", "mixed", "—",
                    Gd.F(rest, 1) + "%", "angels, early paper, rounding" });
            return rows;
        }

        static List<DeskKit.DilStep> DilutionSteps(GameState state, int val)
        {
            var priced = new List<Instrument>();
            foreach (Instrument inst in state.Instruments)
                if (inst.Kind == "priced" && inst.Pct > 0.0) priced.Add(inst);
            priced.Sort((a, b2) => a.SignedWk.CompareTo(b2.SignedWk));
            var steps = new List<DeskKit.DilStep>();
            steps.Add(new DeskKit.DilStep { Label = "day 0", Pct = 100f, Note = "100% · $0" });
            double now = state.FounderPct;
            double beforeRound = now;
            double poolKeep = 1.0 - Gd.Clampf(SimBoard.PoolAskPct(state), 0.0, 15.0) / 100.0;
            if (priced.Count > 0)
            {
                Instrument newest = priced[priced.Count - 1];
                double invKeep = 1.0 - newest.Pct / 100.0;
                beforeRound = Gd.Clampf(now / Gd.Maxf(invKeep * poolKeep, 0.01), 0.0, 100.0);
            }
            if (priced.Count > 1)
                steps.Add(new DeskKit.DilStep { Label = "+" + (priced.Count - 1) + " earlier",
                    Pct = (float)Gd.Minf(beforeRound + 8.0, 100.0),
                    Note = "smaller each time" });
            if (StackCount(state) > 0 || AnyConverted(state))
                steps.Add(new DeskKit.DilStep { Label = "the SAFE stack", Pct = (float)beforeRound,
                    Note = "converts later*" });
            if (state.Esop != null && priced.Count > 0)
                steps.Add(new DeskKit.DilStep { Label = "the pool",
                    Pct = (float)Gd.Clampf(beforeRound * poolKeep, 0.0, 100.0),
                    Note = "the top-up dilutes YOU" });
            if (priced.Count > 0)
                steps.Add(new DeskKit.DilStep { Label = "wk" + priced[priced.Count - 1].SignedWk + " · priced",
                    Pct = (float)now, Note = "bigger pie" });
            int paper = Gd.ToInt(val * now / 100.0);
            steps.Add(new DeskKit.DilStep { Label = "now", Pct = (float)now,
                Note = "≈ " + SimOwnership.MoneyShort(paper) + " on paper" });
            if (steps.Count == 2 && state.Instruments.Count == 0 && state.Esop == null)
                return new List<DeskKit.DilStep>();
            return steps;
        }

        static bool AnyConverted(GameState state)
        {
            foreach (Instrument inst in state.Instruments)
                if ((inst.Kind == "safe" || inst.Kind == "note" || inst.Kind == "bridge")
                    && inst.Pct > 0.0)
                    return true;
            return false;
        }

        static string Costline(GameState state)
        {
            var bits = new List<string>();
            if (state.Esop != null)
                bits.Add("the pool has " + Gd.F(SimOwnership.PoolFree(state), 1)
                    + "% free — recruitment draws from it");
            string cliff = NextCliff(state);
            if (cliff != "") bits.Add(cliff);
            if (state.Board != null)
                bits.Add("covenant " + (state.Board.Strikes == 0 ? "met" : "strike " + state.Board.Strikes));
            if (bits.Count == 0)
                return "no pool, no paper — the clean page IS the bootstrap flex";
            return string.Join(" · ", bits);
        }

        static string NextCliff(GameState state)
        {
            if (state.Esop == null) return "";
            int bestIn = int.MaxValue;
            string who = "";
            foreach (EsopGrant g in state.Esop.Granted)
            {
                if ((g.EmpId ?? "").StartsWith("left:")) continue;
                int cliffIn = g.VestStartWk + SimOwnership.CLIFF_WEEKS - state.Week;
                if (cliffIn > 0 && cliffIn < bestIn) { bestIn = cliffIn; who = (g.EmpId ?? "").Replace("_", " "); }
            }
            if (who == "") return "";
            return "next cliff: " + who + "'s " + SimOwnership.CLIFF_WEEKS + " wks in " + bestIn
                + " wk" + (bestIn == 1 ? "" : "s");
        }

        public static void Handle(BinderScreen b, string id) { }
    }
}
