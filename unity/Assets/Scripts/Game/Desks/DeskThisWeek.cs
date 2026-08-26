using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — THE LOG · "this week" (twin of desk_this_week.gd). W2 lane: L-COMPANY.
    /// THE QUESTION THIS DESK ANSWERS: "what happened, and what\u0027s our move?"
    /// The stub EMBEDS the shipped DeskVitals desk so nothing regresses while the
    /// lane reworks this page to its locked pick.
    /// </summary>
    public static class DeskThisWeek
    {
        public const string Question = "what happened, and what\u0027s our move?";

        /// The group overview's card: the page's hero, one number + one line.
        public static string[] HeroSummary(GameState s)
        {
            return new[] { "week " + s.Week, "the desk you play from" };
        }

        public static void Draw(BinderScreen b)
        {
            DeskVitals.Draw(b);
            DeskKit.HeroQuestion(b, Question);
        }

        public static void Handle(BinderScreen b, string id)
        {
            DeskVitals.Handle(b, id);
        }
    }
}
