using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — REVENUE · "customers" (twin of desk_customers_page.gd). W2 lane: L-REV.
    /// THE QUESTION THIS DESK ANSWERS: "who is coming and staying?"
    /// The stub EMBEDS the shipped DeskCustomers desk so nothing regresses while the
    /// lane reworks this page to its locked pick.
    /// </summary>
    public static class DeskCustomersPage
    {
        public const string Question = "who is coming and staying?";

        /// The group overview's card: the page's hero, one number + one line.
        public static string[] HeroSummary(GameState s)
        {
            return new[] { s.Traction + " customers", "the scoreboard — count, net, kept" };
        }

        public static void Draw(BinderScreen b)
        {
            DeskCustomers.Draw(b);
            DeskKit.HeroQuestion(b, Question);
        }

        public static void Handle(BinderScreen b, string id)
        {
            DeskCustomers.Handle(b, id);
        }
    }
}
