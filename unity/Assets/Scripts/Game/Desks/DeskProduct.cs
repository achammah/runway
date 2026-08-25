using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `product` tab. Spec: docs/design/07-roadmap.md
    ///
    /// BinderScreen dispatches the tab body here and passes ITSELF, so this file
    /// draws through the binder's own helpers and never reaches into the sheet
    /// directly.
    ///
    /// WHAT IS HERE NOW is today's shipped product page, moved verbatim off the
    /// binder so the lane REPLACES a working baseline instead of a blank file.
    /// The lane's job (07): the debt jar shrinks to (300,10) 64x84 with its
    /// triple-cost caption, the debt spark goes, the hype spark moves to vitals,
    /// and the roadmap board takes the sheet — capacity header, bet cards at
    /// 118px pitch (uncommitted / committed+progress / READY), hardening row,
    /// footer.
    ///
    /// The bar every surface ships at (00-spine section 11): readable first pass
    /// by a tired player; concepts named in real business terms with a teaching
    /// line where a number first appears; no dead ends and every state leavable;
    /// drawn in the game's hand, never a SaaS panel. The shared components live
    /// in Game/DeskKit.cs — use them, never fork them.
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_product.gd draw the same
    /// rows at the same coordinates.
    /// </summary>
    public static class DeskProduct
    {
        /// <summary>Draw the roadmap board: capacity, bet cards, progress, READY.</summary>
        public static void Draw(BinderScreen b)
        {
            GameState st = b.State;
            b.Icon("product", 10f, 6f);
            b.L("v0." + st.Product, 100f, 10f, 46f);
            b.L("tech debt:", 10f, 110f, 28f);
            // THE DEBT JAR — `_DebtJar._draw` at position (160, 92), size 90×110. It is
            // a VESSEL: a faint ground, a coral level, a 4px ink outline round the whole
            // height and a heavier line across the lip. Without the outline the level
            // floats and the jar is not a jar. Every number below is the .gd's own,
            // with the jar's (160, 92) already added in.
            DrawnUI.Fill(b.Content, "jarback", DrawnUI.WithAlpha(DrawnUI.Ink, 0.04f),
                166f, 102f, 78f, 96f);
            float fill = Mathf.Clamp01((float)st.TechDebt / 100f);
            // the level rides h-16 = 94, not the ground's 96
            DrawnUI.Fill(b.Content, "jarfill", DrawnUI.WithAlpha(DrawnUI.Coral, 0.55f),
                168f, 102f + 94f * (1f - fill), 74f, 94f * fill);
            b.JarEdge(166f, 102f, 78f, 96f, 4f);
            // draw_line((2, 10) → (w-2, 10), INK, 5.0): a stroke CENTRED on the lip
            DrawnUI.Fill(b.Content, "jarlip", DrawnUI.Ink, 162f, 99.5f, 86f, 5f);
            double risk = Gd.Maxf((st.TechDebt - 40.0) / 250.0, 0.0) * 100.0;
            b.L(string.Format("outage odds ≈ {0}% weekly", Gd.ToInt(risk)), 290f, 120f, 28f,
                risk > 10.0 ? DrawnUI.Coral : DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f));
            b.L("debt, weekly:", 10f, 236f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            b.Spark("debt", 10f, 268f, 1120f, 170f, DrawnUI.Coral);
            b.L("hype:", 10f, 470f, 28f);
            b.Spark("hype", 120f, 452f, 1010f, 130f, DrawnUI.Yellow);
            // THE BENCH rides the bottom band on Hardware runs (see DrawBench).
            if (st.BizWhat == "Hardware") DrawBench(b);
        }

        /// <summary>A press inside this desk. `id` is whatever Draw registered.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }

        /// <summary>
        /// THE BENCH belongs to the hardware lane and is drawn inside this desk
        /// on Hardware runs only. The band is ruled in 00-spine section 11
        /// (y470-740) — on Hardware runs 07 caps its bet cards at 2 and yields
        /// the footer line to make room.
        /// </summary>
        public static void DrawBench(BinderScreen b)
        {
            DeskFactory.DrawBench(b);
        }
    }
}
