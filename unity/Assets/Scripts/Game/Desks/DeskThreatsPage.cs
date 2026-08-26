using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — THE COMPANY · "threats" (twin of desk_threats_page.gd). W2 lane: L-COMPANY.
    /// THE QUESTION THIS DESK ANSWERS: "what could kill us?"
    /// The stub EMBEDS the shipped DeskThreats desk so nothing regresses while the
    /// lane reworks this page to its locked pick.
    /// </summary>
    public static class DeskThreatsPage
    {
        public const string Question = "what could kill us?";

        /// The group overview's card: the page's hero, one number + one line.
        public static string[] HeroSummary(GameState s)
        {
            return new[] { "the list", "the command center — loudest first" };
        }

        public static void Draw(BinderScreen b)
        {
            DeskThreats.Draw(b);
            DeskKit.HeroQuestion(b, Question);
        }

        public static void Handle(BinderScreen b, string id)
        {
            DeskThreats.Handle(b, id);
        }
    }
}
