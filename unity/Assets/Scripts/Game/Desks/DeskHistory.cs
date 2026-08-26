using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — THE LOG · "history" (twin of desk_history.gd). W2 lane: L-COMPANY.
    /// THE QUESTION THIS DESK ANSWERS: "how did we get here?"
    /// No shipped ancestor: the stub renders the question as its hero and an
    /// honest pen note until the lane lands.
    /// </summary>
    public static class DeskHistory
    {
        public const string Question = "how did we get here?";

        public static string[] HeroSummary(GameState s)
        {
            return new[] { "the ledger of weeks", "a row per week, receipts behind each" };
        }

        public static void Draw(BinderScreen b)
        {
            DeskKit.UnderConstruction(b, "the ledger of weeks", Question, "the run\u0027s own ledger — wk, cash, net, customers, the headline; folded momentary tabs file here as flagged rows");
        }

        public static void Handle(BinderScreen b, string id) { }
    }
}
