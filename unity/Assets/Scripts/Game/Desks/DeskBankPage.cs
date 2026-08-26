using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — COSTS · "the bank" (twin of desk_bank_page.gd). W2 lane: L-MONEY.
    /// THE QUESTION THIS DESK ANSWERS: "what do we owe and can we borrow?"
    /// The stub EMBEDS the shipped DeskBank desk so nothing regresses while the
    /// lane reworks this page to its locked pick.
    /// </summary>
    public static class DeskBankPage
    {
        public const string Question = "what do we owe and can we borrow?";

        /// The group overview's card: the page's hero, one number + one line.
        public static string[] HeroSummary(GameState s)
        {
            return new[] { "debt $" + GameUi.Money(SimBank.DebtTotal(s)), "the meeting — four numbered zones" };
        }

        public static void Draw(BinderScreen b)
        {
            DeskBank.Draw(b);
            DeskKit.HeroQuestion(b, Question);
        }

        public static void Handle(BinderScreen b, string id)
        {
            DeskBank.Handle(b, id);
        }
    }
}
