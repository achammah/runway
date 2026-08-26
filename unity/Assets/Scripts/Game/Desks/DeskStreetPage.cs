using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — THE COMPANY · "the street" (twin of desk_street_page.gd). W2 lane: L-COMPANY.
    /// THE QUESTION THIS DESK ANSWERS: "what is the world doing to us?"
    /// The stub EMBEDS the shipped DeskStreet desk so nothing regresses while the
    /// lane reworks this page to its locked pick.
    /// </summary>
    public static class DeskStreetPage
    {
        public const string Question = "what is the world doing to us?";

        /// The group overview's card: the page's hero, one number + one line.
        public static string[] HeroSummary(GameState s)
        {
            return new[] { "the street", "the weather, the rivals, the investors\u0027 mood" };
        }

        public static void Draw(BinderScreen b)
        {
            DeskStreet.Draw(b);
            DeskKit.HeroQuestion(b, Question);
        }

        public static void Handle(BinderScreen b, string id)
        {
            DeskStreet.Handle(b, id);
        }
    }
}
