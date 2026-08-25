using System.Collections.Generic;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `cap table` tab. Spec: docs/design/08-board-mna.md sections 8/10
    ///
    /// BinderScreen dispatches the tab body here and passes ITSELF, so this file
    /// draws through the binder's own helpers and never reaches into the sheet
    /// directly.
    ///
    /// THE PAGE IS A SCORECARD THE FOUNDER KEEPS ON THEMSELF. The wheel says who
    /// owns what — and the fourth slice makes the option pool a drawn wound,
    /// carved out of your side by the pool shuffle. Under it, the room you answer
    /// to: the covenant with a countdown, the strike track as pen marks, and one
    /// authored line naming the governance you have actually grown into. Above
    /// all of it, when a clock is running, the offer banner.
    ///
    /// No board, no rows: a company that never took a check has nothing under the
    /// wheel, and that clean page IS the bootstrap flex.
    ///
    /// NOTHING ON THIS PAGE SIGNS ANYTHING. Selling the company and ringing the
    /// bell are journal acts (two-tap, 10-interface-language section 2.9) — the
    /// desk shows the clock and says where the pen is.
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_cap.gd draw the same rows
    /// at the same coordinates.
    /// </summary>
    public static class DeskCap
    {
        const int PieSide = 430;      // `pie.set_deferred("size", Vector2(430, 430))`
        const float PieX = 40f;
        const float PieY = 30f;

        /// THE HAND HAS NO SYMBOL FONT. U+2717 and U+26A1 are both absent from
        /// PatrickHand — a typed one arrives as a tofu box — so a strike is the
        /// multiplication sign and the banner leads with the game's own alarm
        /// mark, the same "!" the tab bangs and the term-sheet banner wear.
        const string MarkStrike = "×";
        const string MarkEmpty = "·";

        /// The governance vocabulary, by era: what a board IS at this size. The
        /// scale-progressive lesson in one line.
        static readonly string[] StageLine =
        {
            "no board — an angel and a handshake",
            "1 investor seat — expectations, lightly held",
            "a real board: covenants + the pool shuffle",
            "{0} — politics, leaks, secondaries",
            "exit-grade governance — clean quarters open windows",
        };

        /// THE ROWS UNDER THE WHEEL GET THE WHOLE PAGE. The spec drew them at
        /// w=470 to stay clear of the rounds column at x=540 — but that column
        /// is bounded: its last row sits at 60 + 44*rounds + 216 and the ladder
        /// caps at six rounds, so it can never reach below y=540. Everything
        /// from y=568 down is free paper, and a covenant sentence wrapped to
        /// three lines inside 470px collided with the stage line every time.
        const float RowW = 1100f;

        /// <summary>Draw the option-pool slice, covenant and strikes, the offer/window banner.</summary>
        public static void Draw(BinderScreen b)
        {
            GameState st = b.State;
            double founder = st.FounderPct;
            double cof = 0.0;
            for (int i = 0; i < st.Cofounders.Count; i++)
                cof += st.Cofounders[i].EquityDiluted.HasValue
                    ? st.Cofounders[i].EquityDiluted.Value : st.Cofounders[i].Equity;
            // FOUR SLICES. The pool sits between the cofounders and the investors
            // because that is where it came from in the shuffle: written
            // pre-money, out of the founding side, before the investor's slice
            // diluted everyone including it.
            double pool = st.OptionPoolPct;
            double investors = Gd.Maxf(100.0 - founder - cof - pool, 0.0);
            // THE WHEEL IS 430 WIDE AND ITS INK IS AT 0.38 OF THAT. A 340 box put the
            // centre at (210, 200) where the original has it at (255, 245), and every
            // label hung off it inherited the error.
            var pcts = new[] { (float)founder, (float)cof, (float)pool, (float)investors };
            var cols = new[] { DrawnUI.Coral, DrawnUI.Blue, DrawnUI.Yellow, DrawnUI.Sage };
            var names = new[] {
                string.Format("you {0:0}%", founder),
                string.Format("cofounders {0:0}%", cof),
                string.Format("option pool {0:0}%", pool),
                string.Format("investors {0:0}%", investors),
            };
            DrawnChart.Mount(b.Content, "pie", DrawnChart.CapPie(pcts, cols, PieSide),
                             PieX, PieY, PieSide, PieSide);
            PieLabels(b, pcts, names);

            float y = 60f;
            b.L("rounds:", 540f, 30f, 32f, DrawnUI.Ink, 560f);
            if (st.RoundsRaised.Count == 0)
                b.L("none yet. every point of the company is still on this table.",
                    540f, y + 20f, 27f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 560f);
            for (int i = 0; i < st.RoundsRaised.Count; i++)
            {
                b.L("· " + st.RoundsRaised[i] + " — closed", 540f, y + 20f, 28f, DrawnUI.Ink, 560f);
                y += 44f;
            }
            int val = SimEngine.Valuation(st);
            b.L("valuation $" + GameUi.Money(val), 540f, y + 80f, 30f, DrawnUI.Ink, 560f);
            b.L("your slice today: $" + GameUi.Money(Gd.ToInt(val * st.FounderPct / 100.0)),
                540f, y + 128f, 30f, DrawnUI.Coral, 560f);
            if (st.FounderBanked > 0)
                // Banked money is not on the wheel and never will be — it left.
                b.L("banked already: $" + GameUi.Money(st.FounderBanked) + " (yours whatever happens)",
                    540f, y + 168f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 620f);
            // WHAT THE NEXT ROUND WOULD COST, with the terms named. Pre-money is
            // what the company is worth before the check; the check makes the
            // post; their slice is check ÷ post.
            if (val > 0)
            {
                int ask = (int)(val * 0.10);
                double fairPct = (double)ask / (val + ask) * 100.0;
                double warm = SimEngine.WarmthPct(st);
                // THE CREDIT CYCLE PRICES THIS LINE (03 section 7.3). A winter
                // widens the spread and shrinks the pre-money at the same time;
                // a boom does both the other way. Both numbers are already the
                // engine's — the caption only says which weather moved them,
                // because raise TIMING against the cycle is decided on this row.
                double asked = fairPct * 1.3 * (1.0 - warm / 100.0) * SimEngine.ShockSpreadMult(st);
                double poolAsk = SimBoard.PoolAskPct(st);
                b.L(string.Format(
                    "raise ~${0} now: pre-money ${1}{2} → post ${3} — they'd ask ≈ {4:0}%{5} · your {6:0}% → ≈ {7:0}%{8}",
                    GameUi.Money(ask), GameUi.Money(val), ShockNote(st), GameUi.Money(val + ask), asked,
                    warm > 0.0 ? string.Format(" ({0:0}% off — they know you)", warm) : "",
                    st.FounderPct, st.FounderPct * (1.0 - asked / 100.0),
                    poolAsk > 0.0 ? string.Format(" · plus a ~{0:0}% pool written pre-money", poolAsk) : ""),
                    540f, y + 216f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 620f);
            }
            if (st.HasFlag("fundraising_open"))
                b.L("! TERM SHEETS ARE ON THE TABLE — sign in the journal before they expire",
                    40f, 480f, 27f, DrawnUI.Coral, 1100f);
            Banner(b, st);
            BoardBlock(b, st);
            // THE RENEWAL CALENDAR (05, DECISIONS.md): the board reads the book
            // of business too. The pipeline lane writes one line here when it has
            // one; nothing is drawn while the slot is empty.
            string renewal = st.GetMeta("cap_renewal_line", "") as string ?? "";
            if (renewal.Length > 0)
                b.L(renewal, 40f, 742f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), RowW);
        }

        /// <summary>Which weather is on the term sheet, or "" in ordinary money.
        /// The multiple is read live off the status catalog, so a rebalance can
        /// never make this line lie.</summary>
        static string ShockNote(GameState st)
        {
            if (SimEngine.HasStatus(st, "funding_winter"))
                return string.Format(" (winter: valuations {0:0.0}×)", SimEngine.ShockValMult(st));
            if (SimEngine.HasStatus(st, "boom"))
                return string.Format(" (boom: {0:0.0}×)", SimEngine.ShockValMult(st));
            return "";
        }

        /// <summary>THE CLOCK, above the board block. An offer or an open window
        /// is time-boxed and has to be visible without opening the book — the
        /// banner says what is on the table and where the pen is.</summary>
        static void Banner(BinderScreen b, GameState st)
        {
            if (st.Mna != null)
            {
                int price = st.Mna.Price;
                int left = Gd.Maxi(st.Mna.ExpiresWeek - st.Week, 0);
                b.L(string.Format(
                    "! ON THE TABLE: {0} — ${1} ({2:0.00}× standalone) · your slice ${3} · no-shop ends in {4} wk. The journal signs.",
                    st.Mna.Buyer, GameUi.Money(price), st.Mna.Premium,
                    GameUi.Money(Gd.ToInt(price * st.FounderPct / 100.0)), left),
                    40f, 520f, 27f, DrawnUI.Coral, 1100f);
            }
            else if (st.HasFlag("ipo_window"))
            {
                int bell = SimBoard.IpoPrice(st);
                b.L(string.Format(
                    "! THE IPO WINDOW IS OPEN — ${0} at the bell, your slice ${1}. The bell is in the journal. Windows close.",
                    GameUi.Money(bell), GameUi.Money(Gd.ToInt(bell * st.FounderPct / 100.0))),
                    40f, 520f, 27f, DrawnUI.Coral, 1100f);
            }
        }

        /// <summary>THE ROOM YOU ANSWER TO. Absent entirely until a round closes
        /// — the empty page is the bootstrap flex.</summary>
        static void BoardBlock(BinderScreen b, GameState st)
        {
            if (st.Board == null) return;
            BoardState bd = st.Board;
            int stage = SimBoard.BoardStage(st);
            b.L(stage == 0 ? "the angel:" : "the board:", 40f, 568f, 32f, DrawnUI.Ink, 470f);

            int nowRev = st.LastPnl != null ? st.LastPnl.Revenue : 0;
            int due = SimBoard.BoardReviewIn(st);
            string when = due <= 0 ? "this week"
                : (due == 1 ? "1 wk left" : due + " wks left");
            // The garage has no covenant to breach — it has a number you said.
            b.L(string.Format("{0}: ${1}/wk by wk {2} — now ${3}/wk · {4}",
                stage == 0 ? "the number you said" : "growth covenant",
                GameUi.Money(bd.TargetRevenue), bd.ReviewWeek, GameUi.Money(nowRev), when),
                40f, 610f, 28f, DrawnUI.Ink, RowW);

            // THE MISS LADDER AS A VISIBLE TRACK. Marks, not a number: a founder
            // should see how many rungs are left without doing arithmetic.
            int strikes = bd.Strikes;
            int goodwill = bd.Goodwill;
            int cap = Gd.Maxi(SimBoard.StrikeCap(st), strikes);
            string track = "";
            for (int i = 0; i < cap; i++)
            {
                track += i < strikes ? MarkStrike : MarkEmpty;
                if (i < cap - 1) track += " ";
            }
            string room = "professional";
            if (strikes >= 2) room = "ice";
            else if (goodwill >= 2) room = "warm";
            if (stage == 0)
                b.L(string.Format(
                    "no strikes — an angel has expectations, not covenants · goodwill {0}/3", goodwill),
                    40f, 654f, 28f, DrawnUI.Sage, RowW);
            else
                b.L(string.Format("strikes {0} · goodwill {1}/3 · the room is {2}", track, goodwill, room),
                    40f, 654f, 28f, strikes > 0 ? DrawnUI.Coral : DrawnUI.Sage, RowW);

            string line = StageLine[Gd.Clampi(stage, 0, StageLine.Length - 1)];
            if (line.Contains("{0}"))
            {
                int seats = st.BoardSeatsInvestor;
                line = string.Format(line, seats == 1 ? "1 investor seat" : seats + " investor seats");
            }
            b.L(line, 40f, 698f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), RowW);
        }

        /// <summary>
        /// THE NAMES GO ROUND THE WHEEL, NOT UNDER IT. `_Pie._draw` walks the slices a
        /// second time and hangs each label at the MIDDLE of its own arc, 40px outside
        /// the ink, all in plain ink — a stacked legend beside the chart is a different
        /// drawing and it stopped saying which colour was whose.
        /// draw_string plants a BASELINE, and the original nudges it by (-46, +8).
        /// </summary>
        static void PieLabels(BinderScreen b, IList<float> pct, IList<string> names)
        {
            const float TwoPi = Mathf.PI * 2f;
            float cx = PieX + PieSide * 0.5f;
            float cy = PieY + PieSide * 0.5f;
            float rr = PieSide * DrawnChart.PieRadiusFrac + 40f;
            float a0 = -Mathf.PI * 0.5f;                 // twelve o'clock
            for (int i = 0; i < pct.Count; i++)
            {
                float frac = Mathf.Clamp(pct[i], 0f, 100f) / 100f;
                if (frac <= 0.01f) continue;             // a sliver gets no name
                float mid = a0 + TwoPi * frac * 0.5f;
                float px = cx + Mathf.Cos(mid) * rr;
                float py = cy + Mathf.Sin(mid) * rr;
                b.L(names[i], px - 46f, py + 8f - 24f * 0.78f, 24f, DrawnUI.Ink, 0f);
                a0 += TwoPi * frac;
            }
        }

        /// <summary>A press inside this desk. Nothing on the cap table commits:
        /// the signature lives in the journal, where a run-ending act gets its
        /// two-tap arm.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }
    }
}
