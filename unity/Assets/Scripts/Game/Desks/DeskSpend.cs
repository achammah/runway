using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — COSTS · "spend" (twin of desk_spend.gd). W2 lane: L-MONEY.
    /// THE QUESTION THIS DESK ANSWERS: "where does the money go?"
    /// The stub EMBEDS the shipped DeskLedger desk so nothing regresses while the
    /// lane reworks this page to its locked pick.
    /// </summary>
    public static class DeskSpend
    {
        public const string Question = "where does the money go?";

        /// The group overview's card: the page's hero, one number + one line.
        public static string[] HeroSummary(GameState s)
        {
            return new[] { "the org ledger", "every line sums into a bucket" };
        }

        public static void Draw(BinderScreen b)
        {
            DeskLedger.Draw(b);
            DeskKit.HeroQuestion(b, Question);
        }

        public static void Handle(BinderScreen b, string id)
        {
            // the shipped ledger routes through closures, not the press router
        }
    }
}
