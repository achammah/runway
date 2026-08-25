using System.Collections.Generic;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `cap table` tab. Spec: docs/design/08-board-mna.md
    ///
    /// BinderScreen dispatches the tab body here and passes ITSELF, so this file
    /// draws through the binder's own helpers and never reaches into the sheet
    /// directly.
    ///
    /// WHAT IS HERE NOW is today's shipped cap table — the wheel, the rounds, the
    /// valuation, the dilution preview — moved verbatim off the binder so the
    /// lane REPLACES a working baseline instead of a blank file. The lane's job
    /// (08): the fourth slice (the option pool, YELL) so dilution is a drawn
    /// wound, the covenant and strikes record, the era stage line, the
    /// offer/window banner.
    ///
    /// The bar every surface ships at (00-spine section 11): readable first pass
    /// by a tired player; concepts named in real business terms with a teaching
    /// line where a number first appears; no dead ends and every state leavable;
    /// drawn in the game's hand, never a SaaS panel. The shared components live
    /// in Game/DeskKit.cs — use them, never fork them.
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_cap.gd draw the same rows
    /// at the same coordinates.
    /// </summary>
    public static class DeskCap
    {
        const int PieSide = 430;      // `pie.set_deferred("size", Vector2(430, 430))`
        const float PieX = 40f;
        const float PieY = 30f;

        /// <summary>Draw the option-pool slice, covenant and strikes, the offer/window banner.</summary>
        public static void Draw(BinderScreen b)
        {
            GameState st = b.State;
            double founder = st.FounderPct;
            double cof = 0.0;
            for (int i = 0; i < st.Cofounders.Count; i++)
                cof += st.Cofounders[i].EquityDiluted.HasValue
                    ? st.Cofounders[i].EquityDiluted.Value : st.Cofounders[i].Equity;
            double investors = Gd.Maxf(100.0 - founder - cof, 0.0);
            // THE WHEEL IS 430 WIDE AND ITS INK IS AT 0.38 OF THAT. A 340 box put the
            // centre at (210, 200) where the original has it at (255, 245), and every
            // label hung off it inherited the error.
            var pcts = new[] { (float)founder, (float)cof, (float)investors };
            var cols = new[] { DrawnUI.Coral, DrawnUI.Blue, DrawnUI.Sage };
            var names = new[] {
                string.Format("you {0:0}%", founder),
                string.Format("cofounders {0:0}%", cof),
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
            b.L("valuation $" + GameUi.Money(SimEngine.Valuation(st)), 540f, y + 80f, 30f,
                DrawnUI.Ink, 560f);
            b.L("your slice today: $" + GameUi.Money(
                Gd.ToInt(SimEngine.Valuation(st) * st.FounderPct / 100.0)),
                540f, y + 128f, 30f, DrawnUI.Coral, 560f);
            // what the NEXT round would cost, so dilution is never a surprise
            int val = SimEngine.Valuation(st);
            if (val > 0)
            {
                int ask = (int)(val * 0.10);
                double fairPct = (double)ask / (val + ask) * 100.0;
                double warm = SimEngine.WarmthPct(st);
                double asked = fairPct * 1.3 * (1.0 - warm / 100.0);
                b.L(string.Format(
                    "raise ~${0} now → investors ask ≈ {1:0}%{2} · your {3:0}% would become ≈ {4:0}%",
                    GameUi.Money(ask), asked,
                    warm > 0.0 ? string.Format(" ({0:0}% off — they know you)", warm) : "",
                    st.FounderPct, st.FounderPct * (1.0 - asked / 100.0)),
                    540f, y + 186f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 620f);
            }
            if (st.HasFlag("fundraising_open"))
                b.L("! TERM SHEETS ARE ON THE TABLE — sign in the journal before they expire",
                    40f, 480f, 27f, DrawnUI.Coral, 1100f);
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

        /// <summary>A press inside this desk. `id` is whatever Draw registered.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }
    }
}
