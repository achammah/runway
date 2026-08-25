using System.Collections.Generic;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `crew` tab. Spec: docs/design/02-labor-market.md
    ///
    /// BinderScreen dispatches the tab body here and passes ITSELF, so this file
    /// draws through the binder's own helpers and never reaches into the sheet
    /// directly.
    ///
    /// WHAT IS HERE NOW is today's shipped read-only roster, moved verbatim off
    /// the binder so the lane REPLACES a working baseline instead of a blank
    /// file. The lane's job (02): the roster/hiring pen toggle (both halves
    /// cannot share one sheet — the ruling is in 00-spine section 11), grown
    /// roster rows with loaded cost and skill pips, raise and two-tap let-go,
    /// open roles with advert steppers, applicant cards, payroll totals, the
    /// rules footer.
    ///
    /// The bar every surface ships at (00-spine section 11): readable first pass
    /// by a tired player; concepts named in real business terms with a teaching
    /// line where a number first appears; no dead ends and every state leavable;
    /// drawn in the game's hand, never a SaaS panel. The shared components live
    /// in Game/DeskKit.cs — use them, never fork them.
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_crew.gd draw the same rows
    /// at the same coordinates.
    /// </summary>
    public static class DeskCrew
    {
        /// <summary>Draw the roster/hiring toggle: employee rows, open roles, applicant cards.</summary>
        public static void Draw(BinderScreen b)
        {
            GameState st = b.State;
            b.Icon("you", 10f, 6f);
            string who = (st.FounderName ?? "").Length > 0 ? st.FounderName : "the founder";
            b.L(string.Format("{0} — lvl {1} · XP {2}/{3} spent · exhaustion {4}/6", who,
                st.Level, st.XpSpent, st.Xp, st.Exhaustion), 100f, 20f, 32f);
            var stats = new List<string>();
            for (int i = 0; i < FounderDraftScreen.StatNames.Length; i++)
                stats.Add(FounderDraftScreen.StatNames[i] + " "
                          + st.Competence(FounderDraftScreen.StatNames[i]));
            b.L(string.Join("  ·  ", stats.ToArray()), 100f, 64f, 27f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f));
            float y = 130f;
            RunDriver driver = RunDriver.Current;
            for (int i = 0; i < st.Cofounders.Count; i++)
            {
                Cofounder cf = st.Cofounders[i];
                b.Icon("cofd_tech", 10f, y);
                string nm = (cf.Name ?? "").Trim();
                b.L(string.Format("{0}{1} cofounder · {2:0}% equity · loyalty {3}",
                    nm.Length > 0 ? nm + " — " : "", cf.Role,
                    cf.EquityDiluted.HasValue ? cf.EquityDiluted.Value : cf.Equity,
                    driver != null ? driver.Loyalty(i) : 70), 100f, y + 16f, 28f);
                y += 84f;
            }
            for (int i = 0; i < st.Employees.Count; i++)
            {
                Employee e = st.Employees[i];
                b.Icon("employee", 10f, y);
                b.L(string.Format("{0} — {1} · ${2}/wk · burnout {3}", e.Name, e.Role,
                    GameUi.Money(e.Salary), e.Burnout), 100f, y + 16f, 28f);
                y += 84f;
            }
            for (int i = 0; i < st.Pipeline.Count; i++)
            {
                PipelineHire h = st.Pipeline[i];
                b.Icon("employee", 10f, y);
                b.L(string.Format("{0} — {1} · ONBOARDING (paid, not yet productive)", h.Name, h.Role),
                    100f, y + 16f, 28f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f));
                y += 84f;
            }
            b.L("morale:", 10f, y + 10f, 28f);
            b.Spark("morale", 120f, y - 8f, 1000f, 120f, DrawnUI.Sage);
        }

        /// <summary>A press inside this desk. `id` is whatever Draw registered.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }
    }
}
