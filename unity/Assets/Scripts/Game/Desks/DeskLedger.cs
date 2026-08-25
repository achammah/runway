using System;
using System.Collections.Generic;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// DESK — the binder's `the ledger` tab. Owner: LANE 04 (funnel channels).
    /// Extracted verbatim from BinderScreen by the coordinator so the ledger
    /// has ONE writer; lane 04 replaces this body with the 8-lever two-block
    /// page (docs/design/04-funnel-channels.md §6.1) and owns it from here.
    public static class DeskLedger
    {
        static readonly string[][] Levers =
        {
            new[] { "ads", "marketing", "reach — more people hear of you; saturates past ~$2k" },
            new[] { "sales", "sales", "closing — every $600/wk closes like one more part-time seller" },
            new[] { "care", "care", "retention — up to 30% less churn as care approaches $3k" },
            new[] { "rnd", "rnd", "product — ships ~+1 quality per $1,200/wk and pays down debt" },
            new[] { "office", "office", "the office — food, perks, benefits; morale climbs toward +3/wk by ~$2k" },
        };

        public static void Draw(BinderScreen b)
        {
            b.L("the ledger — where this week's money goes", 10f, 6f, 38f);
            float y = 78f;
            for (int i = 0; i < Levers.Length; i++)
            {
                string cat = Levers[i][0];
                int cur = b.Budget(cat);
                b.L(Levers[i][1].ToUpper(), 10f, y, 28f);
                b.L(Levers[i][2], 10f, y + 34f, 21f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
                b.L("$" + GameUi.Money(cur) + "/wk", 520f, y + 4f, 30f, DrawnUI.Coral, 200f);
                // WHAT THIS MONEY IS DOING RIGHT NOW, from the engine's own formulas
                b.L(LeverEffect(b, cat, cur), 688f, y + 12f, 24f,
                  DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f), 300f);
                string c = cat;
                int at = cur;
                GameUi.InkWord(b.Content, "−", 1000f, y, 52f, 46f, 40f, DrawnUI.Ink, () =>
                {
                    b.SetBudget(c, b.Step(at, -1));
                    b.Refresh();
                });
                GameUi.InkWord(b.Content, "+", 1064f, y, 52f, 46f, 40f, DrawnUI.Ink, () =>
                {
                    b.SetBudget(c, b.Step(at, 1));
                    b.Refresh();
                });
                y += 78f;
            }
            // the math, honestly — one running cursor, compact: five levers +
            // the P&L + the warnings all live inside the 760px sheet
            int leverSum = b.State.Budgets.Sum();
            int rw = SimEngine.RunwayWeeks(b.State);
            float cy = y + 4f;
            double arpu = b.UnitEcon("arpu");
            int cac = Gd.ToInt(b.UnitEcon("cac"));
            int ltv = Gd.ToInt(b.UnitEcon("ltv"));
            int pb = Gd.ToInt(b.UnitEcon("payback_wk"));
            b.L(string.Format(
                "a customer pays ≈ ${0:0}/wk · costs ${1} to win (CAC) · is worth ${2} over their stay (LTV) · pays back in {3}",
                arpu, cac > 0 ? GameUi.Money(cac) : "?",
                ltv > 0 ? GameUi.Money(ltv) : "?", pb > 0 ? pb + " wks" : "—"),
                10f, cy, 23f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f), 1100f);
            cy += 34f;
            // THE WEEK, HONESTLY (owner: a real business sim knows its running
            // cost): the engine's own P&L record, every lane, the bottom line.
            Pnl pnl = b.State.LastPnl;
            if (pnl != null)
            {
                b.L(string.Format("last week: in ${0} · serving ${1}{2}",
                    GameUi.Money(pnl.Revenue), GameUi.Money(pnl.Cogs),
                    pnl.Learning < 0.995
                        ? string.Format("  (learning ×{0:0.00})", pnl.Learning) : ""),
                    10f, cy, 24f, DrawnUI.Blue, 1100f);
                cy += 34f;
                b.L(string.Format("out: rent ${0} · payroll ${1} · infra ${2} · levers ${3}{4}{5}",
                    GameUi.Money(pnl.Rent), GameUi.Money(pnl.Payroll), GameUi.Money(pnl.Infra),
                    GameUi.Money(pnl.Marketing + pnl.Sales + pnl.Care + pnl.Rnd + pnl.Office),
                    pnl.Incident > 0 ? " · unforeseen $" + GameUi.Money(pnl.Incident) : "",
                    pnl.LiabilitiesWk > 0 ? " · standing $" + GameUi.Money(pnl.LiabilitiesWk) + "/wk" : ""),
                    10f, cy, 24f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f), 1100f);
                cy += 34f;
                b.L(string.Format("THE BOTTOM LINE: {0}${1} a week · levers total ${2}/wk · runway {3}",
                    pnl.Net >= 0 ? "+" : "−", GameUi.Money(Math.Abs(pnl.Net)),
                    GameUi.Money(leverSum), rw < 999 ? rw + " weeks" : "gaining money"),
                    10f, cy, 27f, pnl.Net >= 0 ? DrawnUI.Sage : DrawnUI.Coral, 1100f);
                cy += 40f;
            }
            else
            {
                b.L(string.Format("levers total ${0}/wk · runway {1}", GameUi.Money(leverSum),
                    rw < 999 ? rw + " weeks" : "gaining money"), 10f, cy, 27f);
                cy += 40f;
            }
            if (rw <= 4 && rw < 999)
            {
                b.L(string.Format("⚠ this spend kills the company in {0} weeks — cut it or earn it", rw),
                  10f, cy, 26f, DrawnUI.Coral, 1100f);
                cy += 36f;
            }
            if (b.State.Cash < 0)
            {
                b.L(string.Format("THE RED: {0} of 3 weeks below zero. At three, it's over.",
                    b.State.WeeksInRed), 10f, cy, 26f, DrawnUI.Coral, 1100f);
                cy += 36f;
            }
            b.L("the rules of this world: reach saturates · only capacity closes · churn is a "
              + "leaky bucket · debt slows everything · three weeks below zero ends it",
              10f, cy + 2f, 20f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f));
        }

        /// the engine's live math for one lever, in one plain phrase
        static string LeverEffect(BinderScreen b, string cat, int v)
        {
            double sat = b.State.Theta != null ? b.State.Theta.CacSat : 900.0;
            switch (cat)
            {
                case "ads":
                case "content":
                case "referrals":
                case "outbound":
                    return v > 0
                        ? string.Format("reach ×{0:0.00}",
                            1.0 + 1.4 * (1.0 - Mathf.Exp(-(float)(v / sat))))
                        : "no reach bought";
                case "sales":
                    return v > 0 ? string.Format("+{0:0.0} closers of capacity", v / 600f)
                                 : "founder sells alone";
                case "care":
                    return v > 0 ? string.Format("churn −{0}%",
                        Mathf.RoundToInt(30f * (1f - Mathf.Exp(-v / 1500f)))) : "nobody picks up";
                case "rnd":
                    return v > 0 ? string.Format("+{0:0.0} product/wk, debt melts", v / 1200f)
                                 : "no extra shipping";
                case "office":
                    return v > 0 ? string.Format("+{0:0.0} morale/wk",
                        3.0 * (1.0 - Mathf.Exp(-v / 800f))) : "instant coffee, cold room";
            }
            return "";
        }
    }
}
