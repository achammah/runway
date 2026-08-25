using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `customers` tab. Spec: docs/design/04-funnel-channels.md
    ///
    /// BinderScreen dispatches the tab body here and passes ITSELF, so this file
    /// draws through the binder's own helpers and never reaches into the sheet
    /// directly.
    ///
    /// WHAT IS HERE NOW is today's shipped fog-of-war customer page, moved
    /// verbatim off the binder so the lane REPLACES a working baseline instead of
    /// a blank file. The lane's job (04): the funnel READS at their analytics
    /// gates on this branch only — the spend controls live on the ledger, and the
    /// Enterprise branch belongs wholesale to the pipeline lane below.
    ///
    /// The bar every surface ships at (00-spine section 11): readable first pass
    /// by a tired player; concepts named in real business terms with a teaching
    /// line where a number first appears; no dead ends and every state leavable;
    /// drawn in the game's hand, never a SaaS panel. The shared components live
    /// in Game/DeskKit.cs — use them, never fork them.
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_customers.gd draw the same
    /// rows at the same coordinates.
    /// </summary>
    public static class DeskCustomers
    {
        /// <summary>Draw the funnel reads at their analytics gates (non-Enterprise branch).</summary>
        public static void Draw(BinderScreen b)
        {
            GameState st = b.State;
            // THE BRANCH IS THE PLANTED SEAM: an Enterprise run's customers page is
            // the pipeline's stage board, drawn by its own lane, and NEITHER LANE
            // EDITS THE OTHER'S FILE. The pipeline says when it owns the page
            // (OwnsPage) — until it does, this branch never fires and the funnel
            // page below is what every run gets, exactly as it does today.
            if (DeskPipeline.OwnsPage(b)) { DrawEnterprise(b); return; }

            b.Icon("customers", 10f, 6f);
            if (st.AnalyticsLevel <= 0)
            {
                b.L(st.Traction + " customers, give or take.", 100f, 10f, 46f);
                b.L("Traffic seems… decent? Someone signed up on Tuesday. The numbers live in a "
                    + "notebook you lost.", 10f, 110f, 30f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f));
                b.L("(invest in analytics to see the funnel)", 10f, 210f, 26f, DrawnUI.Coral);
                return;
            }
            b.L(st.Traction + " customers", 100f, 10f, 46f);
            b.L("customers, weekly:", 10f, 100f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            b.Spark("customers", 10f, 132f, 1120f, 200f, DrawnUI.Sage);
            double tam = st.Beliefs != null && st.Beliefs.Tam > 0.0
                ? st.Beliefs.Tam : (st.Theta != null ? st.Theta.Tam : 100000.0);
            double life = st.Beliefs != null && st.Beliefs.LifetimeWk > 0.0
                ? st.Beliefs.LifetimeWk : (st.Theta != null ? st.Theta.LifetimeWk : 40.0);
            b.L(string.Format(
                "market, as you believe it: ~{0} buyers ({1:0.0}% reached) · a customer stays ≈ {2} wks",
                GameUi.Money(Gd.ToInt(tam)), st.Traction / Gd.Maxf(tam, 1.0) * 100.0, Gd.ToInt(life)),
                10f, 356f, 27f);
            b.L("working assumptions — they sharpen as you learn", 10f, 392f, 22f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f));
            if (st.AnalyticsLevel >= 2)
            {
                // the second analytics level BUYS the CAC read — dropping it left the
                // upgrade paying for a line the player already had
                double mk = st.MarketingBudget;
                string cac = mk <= 0.0 ? "∞"
                    : "$" + Gd.ToInt(mk / Gd.Maxf(1.0, mk / 900.0));
                b.L(string.Format("price ×{0:0.00} · marketing ${1}/wk · CAC roughly {2}",
                    st.PriceMult, GameUi.Money(st.MarketingBudget), cac), 10f, 404f, 28f);
                b.L(string.Format("lifetime ≈ {0} wks at v0.{1} quality",
                    Gd.ToInt(life * (0.4 + st.Product / 100.0 * 1.2)), st.Product), 10f, 448f, 28f);
            }
            if (st.AnalyticsLevel >= 3)
                b.L("the funnel is fully lit: organic + word-of-mouth + paid, all measured. "
                    + "You are the analytics now.", 10f, 500f, 26f, DrawnUI.Sage);
        }

        /// <summary>A press inside this desk. `id` is whatever Draw registered.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }

        /// <summary>
        /// THE ENTERPRISE BRANCH belongs to the pipeline lane, drawn inside this
        /// desk. The call site is planted so neither lane edits the other's file.
        /// </summary>
        public static void DrawEnterprise(BinderScreen b)
        {
            DeskPipeline.DrawBoard(b);
        }
    }
}
