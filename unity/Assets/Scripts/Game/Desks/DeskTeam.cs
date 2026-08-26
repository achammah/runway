using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — COSTS · "team" (twin of desk_team.gd). W2 lane: L-MONEY.
    /// THE QUESTION THIS DESK ANSWERS: "who works here and who\u0027s asking?"
    /// The stub EMBEDS the shipped DeskCrew desk so nothing regresses while the
    /// lane reworks this page to its locked pick.
    /// </summary>
    public static class DeskTeam
    {
        public const string Question = "who works here and who\u0027s asking?";

        /// The group overview's card: the page's hero, one number + one line.
        public static string[] HeroSummary(GameState s)
        {
            return new[] { s.Employees.Count + " people", "the payroll ledger, three rungs" };
        }

        public static void Draw(BinderScreen b)
        {
            DeskCrew.Draw(b);
            DeskKit.HeroQuestion(b, Question);
        }

        public static void Handle(BinderScreen b, string id)
        {
            DeskCrew.Handle(b, id);
        }
    }
}
