using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `product` tab: THE ROADMAP BOARD. Spec: docs/design/07-roadmap.md section 8
    ///
    /// BinderScreen dispatches the tab body here and passes ITSELF, so this file
    /// draws through the binder's own helpers and never reaches into the sheet
    /// directly.
    ///
    /// WHAT THE PAGE IS: index cards pinned in a column. Each card is a thing the
    /// team could build, with its price in R&amp;D-WEEKS and its odds printed like
    /// the DM saying them across the table. One press points the team at it; the
    /// money the ledger already spends on rnd turns into progress, not polish.
    ///
    /// THE FOUR LESSONS, named where their number first appears:
    ///   CAPACITY            the header prints what a week of this org is worth
    ///   OPPORTUNITY COST    the footer, and the fact that committed weeks ship
    ///                       no base quality
    ///   TECH-DEBT INTEREST  the jar line prints the velocity it is costing
    ///   LAUNCH RISK         every card's odds line, and the ship receipt
    ///
    /// SHIP IS A BUTTON (DECISIONS.md #2): the dice roll AT the press, behind the
    /// pre-roll review — the same card the journal shows before the weekly LOCK
    /// IN, built from the same engine list, mirrored here because a roll is a
    /// roll wherever it happens.
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_product.gd draw the same
    /// rows at the same coordinates.
    /// </summary>
    public static class DeskProduct
    {
        const float CardY = 140f;        // the first card
        const float CardPitch = 118f;    // board-card density (10-interface-language 2.4)
        const int CardCap = 3;           // the era ladder never opens a fourth
        const int HwCardCap = 2;         // Hardware: THE BENCH takes the bottom band
        const float BarX = 720f;
        const float BarW = 270f;
        const float ActX = 1000f;        // the house control column (DeskKit.XMinus)

        /// <summary>Draw the roadmap board: capacity, bet cards, progress, READY.</summary>
        public static void Draw(BinderScreen b)
        {
            GameState st = b.State;
            // the board is paper: it exists from the first open, not the first tick
            SimRoadmap.EnsureBoard(st);
            switch (Mode(b))
            {
                case "preroll": PrerollCard(b); return;
                case "shipped": ShipCard(b); return;
            }
            Head(b, st);
            bool hardware = st.BizWhat == "Hardware";
            List<Bet> cards = VisibleCards(st, hardware);
            float y = CardY;
            for (int i = 0; i < cards.Count; i++)
            {
                y = BetCard(b, cards[i], y, !hardware);
            }
            int hidden = SimRoadmap.Unshipped(st).Count - cards.Count;
            if (hidden > 0) DeskKit.More(b, DeskKit.XId, y, hidden, "wait for a free slot");
            if (hardware)
            {
                // THE BENCH rides the bottom band on Hardware runs (see DrawBench).
                DrawBench(b);
                return;
            }
            Footer(b, st);
        }

        /// <summary>The head: what the product IS, and what the debt is charging for it.</summary>
        static void Head(BinderScreen b, GameState st)
        {
            b.Icon("product", 10f, 6f);
            b.L("v0." + st.Product, 100f, 10f, DeskKit.HeroSize);
            // THE DEBT JAR, shrunk to (300,10) 64x84: a faint ground, a coral level,
            // a 4px ink outline round the whole height and a heavier line across the
            // lip. Without the outline the level floats and the jar is not a jar.
            DrawnUI.Fill(b.Content, "jarback", DrawnUI.WithAlpha(DrawnUI.Ink, 0.04f),
                306f, 20f, 52f, 70f);
            float fill = Mathf.Clamp01((float)st.TechDebt / 100f);
            DrawnUI.Fill(b.Content, "jarfill", DrawnUI.WithAlpha(DrawnUI.Coral, 0.55f),
                308f, 20f + 68f * (1f - fill), 48f, 68f * fill);
            b.JarEdge(306f, 20f, 52f, 70f, 4f);
            DrawnUI.Fill(b.Content, "jarlip", DrawnUI.Ink, 302f, 17.5f, 60f, 5f);
            // ONE LINE, THREE COSTS: debt bills an outage roll, a build penalty, and
            // — new this wave — interest on every hour the team works.
            int outage = Gd.ToInt(Gd.Maxf((st.TechDebt - 40.0) / 250.0, 0.0) * 100.0);
            int interest = Gd.RoundToInt((1.0 - SimRoadmap.DebtDrag(st)) * 100.0);
            b.L(string.Format(CultureInfo.InvariantCulture,
                "debt {0} · outage ≈ {1}%/wk · TECH-DEBT INTEREST: −{2}% velocity",
                Gd.ToInt(st.TechDebt), outage, interest), 390f, 16f, 25f,
                st.TechDebt >= 40.0 && BenchCoral(st) < 2
                    ? DrawnUI.Coral : DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f), 760f);
            // CAPACITY, by name and in the industry's own unit: one team, so many
            // person-weeks a week, whatever the org happens to be made of.
            b.L(string.Format(CultureInfo.InvariantCulture,
                "the roadmap — one team, {0} R&D-wks/wk of capacity",
                Gd.F(SimRoadmap.CapacityPool(st), 1)), 10f, 100f, 32f);
        }

        /// <summary>Which cards this run can see. Non-Hardware: every candidate, then
        /// the standing row. Hardware: two cards only — work in flight first, then
        /// the standing maintenance choice, because THE BENCH owns the rest.</summary>
        static List<Bet> VisibleCards(GameState st, bool hardware)
        {
            List<Bet> board = SimRoadmap.BoardBets(st);
            Bet hardening = SimRoadmap.HardeningBet(st);
            var outp = new List<Bet>();
            if (!hardware)
            {
                for (int i = 0; i < board.Count && i < CardCap; i++) outp.Add(board[i]);
                if (hardening != null) outp.Add(hardening);
                return outp;
            }
            var rest = new List<Bet>();
            for (int i = 0; i < board.Count; i++)
            {
                if (board[i].Committed || board[i].Ready) outp.Add(board[i]);
                else rest.Add(board[i]);
            }
            if (hardening != null) outp.Add(hardening);
            outp.AddRange(rest);
            if (outp.Count > HwCardCap) outp.RemoveRange(HwCardCap, outp.Count - HwCardCap);
            return outp;
        }

        /// <summary>ONE CARD (10-interface-language 2.4, board-card density): what it
        /// is, whose voice it is in, what it costs and what the dice think of it —
        /// then the state block on the right, which is the only thing that changes
        /// between the three states a bet can be in.</summary>
        static float BetCard(BinderScreen b, Bet bet, float y, bool separator)
        {
            GameState st = b.State;
            bool standing = separator && bet.Id == SimRoadmap.HARDENING_ID;
            if (standing)
            {
                b.L("—— standing ——", DeskKit.XId, y, 20f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.4f));
                y += 28f;
            }
            var row = new DeskKit.CardRow
            {
                Name = string.Format(CultureInfo.InvariantCulture, "{0} · {1}, ambition {2}",
                    (bet.Name ?? "").ToUpperInvariant(), bet.Kind, bet.Ambition),
                Flavor = bet.Desc,
                Dense = string.Format(CultureInfo.InvariantCulture,
                    "{0} R&D-wks · LAUNCH RISK: clean ship ~{1}% (DC {2} vs build)",
                    Wks(bet.CostRndWeeks), SimRoadmap.ShipOddsPct(st, bet), SimRoadmap.BetDc(bet)),
                Pitch = CardPitch,
            };
            if (bet.Ready)
            {
                ReadyBlock(b, bet, y);
            }
            else if (bet.Committed)
            {
                ProgressBlock(b, bet, y);
            }
            else if (SimRoadmap.CommittedBets(st).Count >= SimRoadmap.WipCap(st))
            {
                // A CAP THAT BITES SAYS SO where the action was: the team is busy,
                // and the WIP number is the lesson.
                row.Actions.Add(new DeskKit.CardAction
                {
                    Reason = string.Format(CultureInfo.InvariantCulture,
                        "at capacity ({0}/{1})",
                        SimRoadmap.CommittedBets(st).Count, SimRoadmap.WipCap(st)),
                });
            }
            else
            {
                CommitBlock(b, bet, y);
            }
            return DeskKit.Card(b, y, row);
        }

        /// <summary>THE ALLOCATION ACT, behind the two-tap arm (10-interface-language
        /// 2.9): the first press prints what the week costs, the second points the
        /// team. The price is not money — it is the base quality this week's rnd
        /// money will now never buy, which is the lesson the desk exists to teach.</summary>
        static void CommitBlock(BinderScreen b, Bet bet, float y)
        {
            GameState st = b.State;
            string id = bet.Id;
            object cur;
            bool armed = b.Desk.TryGetValue("armed", out cur) && cur != null
                         && cur.ToString() == "on:" + id;
            if (armed)
            {
                b.L("rnd money buys weeks, not polish", BarX, y + 12f, 22f, DrawnUI.Coral, 280f);
            }
            DeskKit.Arm(b, "on:" + id, "point the team ->", "sure?", ActX, y + 4f,
                () => SimRoadmap.CommitBet(st, id), 160f, DeskKit.Detail);
        }

        /// <summary>COMMITTED: the money is visibly going somewhere, with an honest
        /// ETA — and a way back out that quotes its own price first (standing down
        /// costs a quarter of the build, DECISIONS.md).</summary>
        static void ProgressBlock(BinderScreen b, Bet bet, float y)
        {
            GameState st = b.State;
            double cost = Gd.Maxf(bet.CostRndWeeks, 0.001);
            float fill = Mathf.Clamp01((float)(bet.Progress / cost));
            DrawnUI.Fill(b.Content, "betback", DrawnUI.WithAlpha(DrawnUI.Ink, 0.04f),
                BarX, y + 10f, BarW, 34f);
            DrawnUI.Fill(b.Content, "betfill", DrawnUI.WithAlpha(DrawnUI.Sage, 0.6f),
                BarX + 2f, y + 12f, (BarW - 4f) * fill, 30f);
            b.JarEdge(BarX, y + 10f, BarW, 34f, 4f);
            string id = bet.Id;
            object cur;
            bool armed = b.Desk.TryGetValue("armed", out cur) && cur != null
                         && cur.ToString() == "down:" + id;
            if (armed)
            {
                b.L("25% of the build is lost", BarX, y + 48f, 22f, DrawnUI.Coral, 280f);
            }
            else
            {
                int eta = SimRoadmap.EtaWeeks(st, bet);
                b.L(string.Format(CultureInfo.InvariantCulture, "{0}% · {1}",
                    SimRoadmap.ProgressPct(bet), eta > 0
                        ? string.Format(CultureInfo.InvariantCulture, "ships in ~{0} wks", eta)
                        : "no capacity — this never finishes"),
                    BarX, y + 48f, 22f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 280f);
            }
            DeskKit.Arm(b, "down:" + id, "stand down", "sure?", ActX, y + 4f,
                () => SimRoadmap.UncommitBet(st, id), 160f, DeskKit.Detail);
        }

        /// <summary>READY: the held breath. The dice have not rolled — the founder
        /// can still pay debt down, stack advantage, and only then press.</summary>
        static void ReadyBlock(BinderScreen b, Bet bet, float y)
        {
            GameState st = b.State;
            b.L("READY — the dice are yours", BarX, y + 6f, 27f, DrawnUI.Coral, 280f);
            int left = SimRoadmap.StallLeft(st, bet);
            b.L(left > 0
                    ? string.Format(CultureInfo.InvariantCulture,
                        "it slips out on its own in {0} wks", left)
                    : "it slips out on its own this week",
                BarX, y + 44f, 21f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 280f);
            string id = bet.Id;
            Button btn = null;
            // THE PRESS OWNS ITS WHOLE BEAT — the review first if anything is open,
            // then the stroke, then the dice (disarms:false keeps the rebuild from
            // freeing the very button the stroke draws under).
            btn = DeskKit.Word(b, "SHIP IT ->", ActX, y + 4f, () =>
            {
                b.Desk.Remove("armed");
                if (PrerollRows(st).Count > 0)
                {
                    b.Desk["mode"] = "preroll";
                    b.Desk["bet"] = id;
                    b.Refresh();
                    return;
                }
                DeskKit.SignStroke(b, btn, "SHIP IT ->", ActX, y + 4f, () => Fire(b, id));
            }, DeskKit.Status, DrawnUI.Ink, 160f, false);
        }

        /// <summary>THE PRE-ROLL REVIEW (DECISIONS.md #2), on this desk. The engine
        /// decides what counts as outstanding; this page only reads it — minus the
        /// row that IS this press, because "a bet is built" is not a reason to stop
        /// a founder from shipping it.</summary>
        static List<AttentionItem> PrerollRows(GameState st)
        {
            var outp = new List<AttentionItem>();
            foreach (AttentionItem it in SimEngine.PrerollItems(st))
            {
                if (it.Key == "bet_ready") continue;
                outp.Add(it);
            }
            return outp;
        }

        static void PrerollCard(BinderScreen b)
        {
            GameState st = b.State;
            string id = Desk(b, "bet");
            Bet bet = SimRoadmap.BetById(st, id);
            if (bet == null) { b.Desk.Clear(); Draw(b); return; }
            List<AttentionItem> rows = PrerollRows(st);
            var read = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (i >= 5)
                {
                    read.Add(string.Format(CultureInfo.InvariantCulture,
                        "…and {0} more, on the threats page.", rows.Count - i));
                    break;
                }
                read.Add(string.Format(CultureInfo.InvariantCulture, "{0}{1} — {2}",
                    rows[i].Severity >= 3 ? "! " : "", rows[i].Desk, rows[i].Label));
            }
            string toDesk = rows.Count > 0 ? rows[0].Desk : "";
            DeskKit.Review(b, new DeskKit.ReviewCard
            {
                Banner = string.Format(CultureInfo.InvariantCulture,
                    "before the dice: '{0}' ships on a d20 vs DC {1}", bet.Name, SimRoadmap.BetDc(bet)),
                Read = read,
                Verdict = "fix them, or roll and live with it.",
                Note = string.Format(CultureInfo.InvariantCulture,
                    "clean ship ~{0}% at build {1} — debt, focus and flow all move that number",
                    SimRoadmap.ShipOddsPct(st, bet), st.Competence("build")),
                Confirm = "roll anyway",
                Cancel = "go fix it",
                OnConfirm = () => Fire(b, id),
                OnCancel = () =>
                {
                    b.Desk.Clear();
                    if (!string.IsNullOrEmpty(toDesk)) b.FocusDesk(toDesk);
                },
            });
        }

        /// <summary>The dice, at the press. The engine rolls, the state changes, and
        /// the card that comes back is the receipt.</summary>
        static void Fire(BinderScreen b, string id)
        {
            SimRoadmap.ShipResult res = SimRoadmap.ShipReady(b.State, id);
            b.Desk.Clear();
            if (res != null)
            {
                b.Desk["mode"] = "shipped";
                b.Desk["ship"] = res;
            }
            b.Refresh();
        }

        /// <summary>THE RECEIPT: the die, the DC, the band, and every delta with its
        /// cause — the same strings the journal prints when a launch slips out.</summary>
        static void ShipCard(BinderScreen b)
        {
            object stored;
            b.Desk.TryGetValue("ship", out stored);
            var res = stored as SimRoadmap.ShipResult;
            if (res == null) { b.Desk.Clear(); Draw(b); return; }
            DeskKit.Back(b, "back to the board", () => b.Desk.Clear());
            float y = 90f;
            b.L(res.Event, DeskKit.XId, y, DeskKit.TitleSize,
                res.Band == "brilliant" || res.Band == "fine" ? DrawnUI.Sage : DrawnUI.Coral,
                1100f);
            y += 64f;
            y = DeskKit.Rule(b, y);
            for (int i = 0; i < res.Lines.Count; i++)
            {
                var l = b.L(res.Lines[i], DeskKit.XId, y, DeskKit.Status,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.85f), 1100f);
                y += Mathf.Max(BinderScreen.Height(l), 32f) + 6f;
            }
            DeskKit.Footer(b, string.Format(CultureInfo.InvariantCulture,
                "the dice were {0}{1} against DC {2} — margin {3}", res.D20,
                res.Mod.ToString("+0;-0;+0", CultureInfo.InvariantCulture), res.Dc,
                res.Total - res.Dc),
                "LAUNCH RISK: scope widens the spread. Ambition 3 pays double and misses twice as often — preparation is the only thing that moves the odds.",
                "");
        }

        /// <summary>THE DESK STATES ITS OWN LAWS — computed from this run's numbers
        /// in blue, the standing rules in ink, and a warning that outranks both.</summary>
        static void Footer(BinderScreen b, GameState st)
        {
            double pool = SimRoadmap.CapacityPool(st);
            int n = SimRoadmap.CommittedBets(st).Count;
            int interest = Gd.RoundToInt((1.0 - SimRoadmap.DebtDrag(st)) * 100.0);
            string computed = string.Format(CultureInfo.InvariantCulture,
                "this week buys {0} R&D-wks", Gd.F(pool, 2));
            if (interest > 0)
            {
                computed += string.Format(CultureInfo.InvariantCulture,
                    " · debt is eating {0}% of it", interest);
            }
            if (n > 1)
            {
                computed += string.Format(CultureInfo.InvariantCulture,
                    " · {0} bets split it {1} each", n, Gd.F(pool / n, 2));
            }
            else if (n == 1) { computed += " · all of it on one bet"; }
            else { computed += " · nothing committed, so it polishes base quality"; }
            string warning = SimRoadmap.AnyBetReady(st)
                ? "a bet is built and waiting — ship it, or it slips out on its own" : "";
            DeskKit.Footer(b,
                computed,
                "OPPORTUNITY COST: rnd money buys R&D-weeks while a bet is committed, +1 quality per $1,200 when it is not · parallel bets share one team",
                warning);
        }

        /// <summary>R&amp;D-weeks read like the estimates they are: 2.5, 3, 10.</summary>
        static string Wks(double v)
        {
            return Gd.Absf(v - Gd.Round(v)) > 0.05 ? Gd.F(v, 1)
                : Gd.ToInt(Gd.Round(v)).ToString(CultureInfo.InvariantCulture);
        }

        static string Mode(BinderScreen b) { return Desk(b, "mode"); }

        static string Desk(BinderScreen b, string key)
        {
            object v;
            return b.Desk.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }

        /// <summary>A press inside this desk. Every control on this page carries its
        /// own closure (the kit's own idiom), so the id router stays empty here by
        /// design — it is kept because BinderScreen.DeskPress names this desk.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }

        /// <summary>
        /// THE BENCH belongs to the hardware lane and is drawn inside this desk on
        /// Hardware runs only. The band is ruled in 00-spine section 11 (y470-740)
        /// — on Hardware runs 07 caps its bet cards at 2 and yields the footer.
        /// </summary>
        public static void DrawBench(BinderScreen b)
        {
            DeskFactory.DrawBench(b);
        }

        /// SPINE RULING (coral budget): when the bench already spends two coral
        /// lines, the standing debt meter yields its color.
        static int BenchCoral(GameState st)
        {
            int n = 0;
            foreach (var r in SimEngine.AttentionItems(st))
                if (r.Key == "stockout" || r.Key == "overstock" || r.Key == "machine_down") n++;
            return n;
        }

    }
}
