using System;
using System.Collections.Generic;
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
    /// </summary>
    public static class DeskCapPage
    {
        public const string Question = "who owns what and what's the company worth?";

        public const double PoolStep = 2.0;

        public static string[] HeroSummary(GameState s)
        {
            int paper = Gd.ToInt(SimEngine.Valuation(s) * s.FounderPct / 100.0);
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
            int val = SimEngine.Valuation(state);
            int paper = Gd.ToInt((double)val * state.FounderPct / 100.0);
            float y = DeskKit.HeroBand(b,
                "you own " + state.FounderPct.ToString("0") + "% · ≈ " + SimOwnership.MoneyShort(paper) + " on paper",
                "paper, not cash — it becomes money only at an exit, after everyone ahead of you.");
            TextMeshProUGUI pr = b.L("the company priced at $" + GameUi.Money(val),
                760f, 10f, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f), 360f);
            pr.alignment = TextAlignmentOptions.TopRight;
            TextMeshProUGUI bl = b.L(BoardLine(state), 760f, 40f, 18f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 360f);
            bl.alignment = TextAlignmentOptions.TopRight;

            // ── zone 1 · THE SLICES
            List<string[]> rows = SliceRows(state);
            int memoN = StackCount(state) > 0 ? 1 : 0;
            float z1H = 78f + DeskKit.LgHeadH + rows.Count * DeskKit.LgRowH
                + memoN * DeskKit.LgRowH + DeskKit.LgTotH + 22f;
            DeskKit.CardBox z1 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, z1H, 1, "the slices",
                "every holder, every class, every preference — the book of who owns what");
            if (state.Esop != null)
                DeskKit.Word(b, "expand the pool +" + PoolStep.ToString("0") + "%",
                    z1.ContentX + 880f, z1.Y + 8f, () => { b.Desk["mode"] = "pool"; },
                    DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 220f);
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
                DeskKit.LedgerRow(b, sheet, r);
            if (memoN > 0)
                DeskKit.LedgerMemo(b, sheet,
                    StackCount(state).ToString() + " SAFE/note(s) waiting to convert", "",
                    "≈" + Gd.F(SimOwnership.StackDilutionAt(state, val), 1)
                    + "% more if it converts");
            DeskKit.LedgerTotal(b, sheet, "the whole pie", "100%", DrawnUI.Ink);
            DeskKit.LedgerEnd(b, sheet);
            y = z1.Bottom + 12f;

            // ── zone 2 · THE DILUTION STORY beside zone 3 · IF SOLD TODAY
            DeskKit.CardBox z2 = DeskKit.Zone(b, DeskKit.XId, y, 646f, 268f, 2, "the dilution story",
                "your slice shrinks at events — and can be worth more every time it does");
            List<DeskKit.DilStep> steps = DilutionSteps(state, val);
            if (steps.Count >= 2)
                DeskKit.DilutionBar(b, z2.ContentX, z2.Cursor + 2f, 610f, steps);
            else
                DeskKit.Empty(b, z2.ContentX, z2.Cursor + 8f, "day 0 — you own all of it.",
                    "rounds, pools and conversions will draw themselves here");
            DeskKit.CardBox z3 = DeskKit.Zone(b, DeskKit.XId + 660f, y, 470f, 268f, 3, "if sold today",
                "the waterfall — who gets paid, in order");
            Dictionary<string, object> wf = SimOwnership.Waterfall(state, val);
            DeskKit.MoneyRow(b, z3, "the bank — debts first", "$" + GameUi.Money(WfI(wf, "debts")));
            DeskKit.MoneyRow(b, z3, "preferences next", "$" + GameUi.Money(WfI(wf, "prefs_paid")));
            DeskKit.MoneyRow(b, z3, "then the split — you'd see", "≈$" + GameUi.Money(WfI(wf, "your_take")),
                DrawnUI.Sage);
            b.L("below ≈$" + GameUi.Money(WfI(wf, "breakeven"))
                + " the preferences eat everything — the waterfall is the whole lesson",
                z3.ContentX, z3.Cursor + 6f, 18f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 430f);
            y += 268f + 10f;

            DeskKit.Footer(b, Costline(state),
                "the cap table moves only through rounds, offers and events — never by hand · rounds happen at THE RAISE",
                "", 812f, 846f);
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
