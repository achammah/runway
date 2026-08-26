using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `vitals` tab. Spec: docs/design/11-binder-rework.md
    /// section vitals.
    ///
    /// BinderScreen dispatches the tab body here and passes ITSELF, so this file
    /// draws through the binder's own helpers and never reaches into the sheet
    /// directly.
    ///
    /// THE QUESTION THIS DESK ANSWERS: "how are we doing?" — the company's pulse
    /// in one read. Cash first and biggest, the health band under it, then the
    /// money's own shape over time, then the week's ins and outs, the burn, what
    /// the company would fetch if anyone asked, and the heat it is carrying.
    ///
    /// The bar every surface ships at (00-spine section 11): readable first pass
    /// by a tired player; concepts named in real business terms with a teaching
    /// line where a number first appears; no dead ends and every state leavable;
    /// drawn in the game's hand, never a SaaS panel. The shared components live
    /// in Game/DeskKit.cs — use them, never fork them.
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_vitals.gd draw the same
    /// rows at the same coordinates.
    /// </summary>
    public static class DeskVitals
    {
        /// <summary>Draw the pulse.</summary>
        public static void Draw(BinderScreen b)
        {
            GameState st = b.State;
            b.Icon("cash", 10f, 6f);
            b.L("$" + GameUi.Money(st.Cash) + " in the bank", 100f, 10f, 46f);
            b.L(SimEngine.HealthBand(st), 100f, 66f, 30f,
                SimEngine.RunwayWeeks(st) <= 10 ? DrawnUI.Coral : DrawnUI.Sage);
            b.L("cash, drawn weekly:", 10f, 140f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            b.Spark("cash", 10f, 172f, 1120f, 190f, DrawnUI.Blue);
            MetricSnapshot last = st.MetricHistory.Count > 0
                ? st.MetricHistory[st.MetricHistory.Count - 1] : new MetricSnapshot();
            b.L(string.Format("last week: ${0} in · ${1} out",
                GameUi.Money(last.Revenue), GameUi.Money(last.Burn)), 10f, 386f);
            int payroll = 0;
            for (int i = 0; i < st.Employees.Count; i++) payroll += st.Employees[i].Salary;
            int rent;
            if (!GameState.ERA_RENT.TryGetValue(st.Era, out rent)) rent = 150;
            // ONE HONEST DEBT FIGURE across shark, bank and venture notes (06
            // section 9): the single LoanPrincipal field stopped being the whole
            // story the week the structured notes landed.
            int debtOwed = SimBank.DebtTotal(st);
            int noteCount = st.Loans.Count + (st.LoanPrincipal > 0 ? 1 : 0);
            b.L(string.Format("burn: rent ${0} · payroll ${1} · marketing ${2}{3}",
                GameUi.Money(rent), GameUi.Money(payroll),
                GameUi.Money(st.LastPnl != null ? st.LastPnl.Marketing
                             : Gd.ToInt(SimFunnel.SpendTotal(st))),
                debtOwed > 0
                    ? "  ·  DEBT $" + GameUi.Money(debtOwed) + " across " + noteCount
                      + " notes (worst " + Gd.RoundToInt(SimBank.WorstRate(st) * 100.0) + "%/wk)"
                    : ""),
                10f, 432f, 27f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f));
            b.L("valuation, if anyone asked: $" + GameUi.Money(SimEngine.Valuation(st)),
                10f, 486f);
            // THE PRICE LINE OWNS 532–566 AT 27px, so the hype caption cannot start
            // at 556: it was written over the line above it and its own spark's wash
            // was drawn over it in turn. 574 clears both, and the spark still lands
            // inside the 760 pane.
            b.L(string.Format("price ×{0:0.00}  ·  the market is {1}", st.PriceMult,
                st.MarketTrend > 1.05 ? "warm" : (st.MarketTrend < 0.95 ? "cold" : "even")),
                10f, 532f, 27f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f));
            // the hype chart moved here when the roadmap took the product sheet (07)
            b.L("hype:", 10f, 574f, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
            b.Spark("hype", 10f, 606f, 1120f, 120f, DrawnUI.Yellow);
        }

        /// <summary>A press inside this desk. `id` is whatever Draw registered.
        /// Vitals is a page you READ: nothing on it is set from here, every number
        /// on it is somebody else's desk stated plainly.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }
    }
}
