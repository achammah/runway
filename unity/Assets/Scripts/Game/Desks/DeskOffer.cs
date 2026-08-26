using System.Collections.Generic;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// THE OFFER — the first MOMENTARY desk (twin of desk_offer.gd; DECISIONS
    /// §4): a gold tab slides into THE COMPANY when a buyout offer lands and
    /// folds into HISTORY when answered. Four zones, the didactic spine. THIS
    /// IS THE SHELL: placeholder numbers; resolving folds the tab (real);
    /// nothing books. W2 lane: L-OWN.
    /// </summary>
    public static class DeskOffer
    {
        public const string Question = "should we take their money?";

        public static string[] HeroSummary(GameState s)
        {
            return new[] { "an offer", "cash vs stock vs earnout — read the small lines" };
        }

        public static void Draw(BinderScreen b)
        {
            float y = DeskKit.HeroBand(b, "they want to buy the company",
                Question + " — the clock on the tab is real.", DrawnUI.Ink, 6f, false);
            DeskKit.CardBox z1 = DeskKit.Zone(b, DeskKit.XId, y, 548f, 210f, 1,
                "what's on the table",
                "the headline price decomposed — cash today vs their paper vs maybe-money");
            DeskKit.MoneyRow(b, z1, "cash today", "$—");
            DeskKit.MoneyRow(b, z1, "acquirer stock (lockup)", "$—");
            DeskKit.MoneyRow(b, z1, "earnout (their targets)", "$—");
            DeskKit.CardBox z2 = DeskKit.Zone(b, DeskKit.XId + 572f, y, 548f, 210f, 2,
                "who gets what",
                "the waterfall applied to THIS number — the bank first, preferences next");
            DeskKit.MoneyRow(b, z2, "the bank", "$—");
            DeskKit.MoneyRow(b, z2, "preferences", "$—");
            DeskKit.MoneyRow(b, z2, "your take", "$—", DrawnUI.Sage);
            y += 234f;
            DeskKit.CardBox z3 = DeskKit.Zone(b, DeskKit.XId, y, 548f, 190f, 3,
                "the fine print, read aloud",
                "some offers are fishy on purpose — each flag named in red");
            b.L("· the shell holds the reading lamp — flags land with the ownership lane",
                z3.ContentX, z3.Cursor, DeskKit.Detail,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 500f);
            DeskKit.CardBox z4 = DeskKit.Zone(b, DeskKit.XId + 572f, y, 548f, 190f, 4,
                "who can say no",
                "what was SIGNED at the raise decided your exit freedom years early");
            b.L("· protective provisions · drag-along · the board — resolved from the instruments",
                z4.ContentX, z4.Cursor, DeskKit.Detail,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 500f);
            y += 214f;
            DeskKit.Arm(b, "offer_accept", "ACCEPT", "press again — the company sells",
                DeskKit.XId, y, () => Resolve(b), 260f);
            DeskKit.Word(b, "NEGOTIATE (one counter)", DeskKit.XId + 300f, y,
                () => Resolve(b), DeskKit.Status, DrawnUI.Ink, 320f);
            DeskKit.Word(b, "DECLINE", DeskKit.XId + 660f, y, () => Resolve(b),
                DeskKit.Status, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 200f);
            DeskKit.Footer(b, "resolving folds this gold tab into HISTORY",
                "momentary desks: summoned by their event, gone when answered", "");
        }

        static void Resolve(BinderScreen b)
        {
            b.ResolveMomentary("the offer");
        }

        public static void Handle(BinderScreen b, string id)
        {
            if (id == "resolve") Resolve(b);
        }
    }
}
