using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — REVENUE · "offers" (twin of desk_offers.gd). W2 lane: L-REV.
    /// THE QUESTION THIS DESK ANSWERS: "what do we sell and what does each sale earn?"
    /// The stub EMBEDS the shipped DeskCatalog desk so nothing regresses while the
    /// lane reworks this page to its locked pick.
    /// </summary>
    public static class DeskOffers
    {
        public const string Question = "what do we sell and what does each sale earn?";

        /// The group overview's card: the page's hero, one number + one line.
        public static string[] HeroSummary(GameState s)
        {
            return new[] { s.Offers.Count + " offers", "the rate card — price, serve, margin, verdict" };
        }

        public static void Draw(BinderScreen b)
        {
            DeskCatalog.Draw(b);
            DeskKit.HeroQuestion(b, Question);
        }

        public static void Handle(BinderScreen b, string id)
        {
            DeskCatalog.Handle(b, id);
        }
    }
}
