using System.Collections.Generic;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `pricing` tab. Spec: docs/design/01-catalog.md
    ///
    /// BinderScreen dispatches the tab body here and passes ITSELF, so this file
    /// draws through the binder's own helpers and never reaches into the sheet
    /// directly.
    ///
    /// WHAT IS HERE NOW is today's shipped pricing page, moved verbatim off the
    /// binder so the lane REPLACES a working baseline instead of a blank file.
    /// The lane's job (01 section 7) is the five-state machine: LIST, DETAIL(i),
    /// WRITE-IN, REVIEW and the write-in arrival — every state reachable and
    /// leavable, "back to all offers" everywhere, desk-local state in `b.Desk`.
    ///
    /// The bar every surface ships at (00-spine section 11): readable first pass
    /// by a tired player; concepts named in real business terms with a teaching
    /// line where a number first appears; no dead ends and every state leavable;
    /// drawn in the game's hand, never a SaaS panel. The shared components live
    /// in Game/DeskKit.cs — use them, never fork them.
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_catalog.gd draw the same
    /// rows at the same coordinates.
    /// </summary>
    public static class DeskCatalog
    {
        /// <summary>Draw the five-state pricing machine: LIST, DETAIL, WRITE-IN, REVIEW.</summary>
        public static void Draw(BinderScreen b)
        {
            GameState st = b.State;
            b.L("pricing — what " + st.CompanyName + " sells", 10f, 6f, 36f);
            if (st.Offers.Count == 0)
            {
                b.L("the world hasn't defined your offers yet — they arrive with the bible.",
                    10f, 90f, 28f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f));
                return;
            }
            float y = 84f;
            for (int i = 0; i < st.Offers.Count; i++)
            {
                Offer o = st.Offers[i];
                b.L((o.Name ?? "?").ToUpper() + "  ·  " + (o.Unit ?? ""), 10f, y, 30f);
                double ucEff = o.UnitCost * SimEngine.LearningCurve(st);
                b.L(string.Format("the street charges ≈ ${0}  ·  costs you ≈ ${1} to serve",
                    GameUi.Money(Gd.ToInt(o.FairPrice)), GameUi.Money(Gd.RoundToInt(ucEff))),
                    10f, y + 38f, 23f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 600f);
                if (o.Price <= 0.0 && o.PriceSet)
                    b.L("FREE ON PURPOSE — pays in users, not dollars", 430f, y + 6f, 27f,
                        DrawnUI.Blue, 540f);
                else if (o.Price <= 0.0)
                    b.L("! no price set — billing at the going rate $" + GameUi.Money(Gd.ToInt(o.FairPrice)),
                        430f, y + 6f, 27f, DrawnUI.Coral, 540f);
                else
                {
                    double dem = SimEngine.OfferDemand(o, o.Price);
                    string verdict = dem > 0.85 && dem < 1.15 ? "about fair"
                        : (dem >= 1.15 ? string.Format("a deal — demand ×{0:0.0}", dem)
                        : (dem > 0.25 ? string.Format("pricey — {0}% of fair demand", (int)(dem * 100.0))
                        : "absurd — ~nobody buys"));
                    b.L(string.Format("${0}  ·  margin ${1}/unit  ·  {2}", GameUi.Money(Gd.ToInt(o.Price)),
                        GameUi.Money(Gd.RoundToInt(o.Price - ucEff)), verdict),
                        430f, y + 6f, 28f, dem > 0.25 ? DrawnUI.Ink : DrawnUI.Coral, 540f);
                }
                Offer captured = o;
                GameUi.InkWord(b.Content, "−", 1000f, y, 52f, 46f, 40f, DrawnUI.Ink, () =>
                {
                    PriceStep(captured, -1);
                    b.Refresh();
                });
                GameUi.InkWord(b.Content, "+", 1064f, y, 52f, 46f, 40f, DrawnUI.Ink, () =>
                {
                    PriceStep(captured, 1);
                    b.Refresh();
                });
                y += 104f;
            }
            double arpu2 = SimEngine.OffersArpu(st);
            if (arpu2 >= 0.0)
            {
                double cpc = SimEngine.OffersCogsPerCustomer(st);
                b.L(string.Format(
                    "all offers together: ≈ ${0:0.0} in − ${1:0.0} serving = ${2:0.0} margin per customer per week  →  ≈ ${3}/wk at {4} customers",
                    arpu2, cpc, arpu2 - cpc,
                    GameUi.Money(Gd.ToInt((arpu2 - cpc) * st.Traction)), st.Traction),
                    10f, y + 10f, 26f, DrawnUI.Blue, 1100f);
            }
            b.L("the curve: price at the street's level and demand is fair · discount and demand "
                + "grows · overprice and it dies fast", 10f, y + 56f, 22f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f));
        }

        /// <summary>price steps: a sensible ladder around the fair price (0 = off sale)</summary>
        public static void PriceStep(Offer o, int dir)
        {
            double fair = o.FairPrice > 0.0 ? o.FairPrice : 10.0;
            var steps = new List<double> { 0.0 };
            double[] mults = { 0.4, 0.55, 0.7, 0.85, 1.0, 1.15, 1.35, 1.6, 2.0, 2.6, 3.5, 5.0 };
            for (int i = 0; i < mults.Length; i++)
                steps.Add(Gd.Maxf(Gd.Round(fair * mults[i]), 1.0));
            int idx = 0;
            for (int i = 0; i < steps.Count; i++) if (steps[i] <= o.Price) idx = i;
            idx = Gd.Clampi(idx + dir, 0, steps.Count - 1);
            o.Price = steps[idx];
            o.PriceSet = true;   // the founder chose this — $0 included (a conscious giveaway)
        }

        /// <summary>A press inside this desk. `id` is whatever Draw registered.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }
    }
}
