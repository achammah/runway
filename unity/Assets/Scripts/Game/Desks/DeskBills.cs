using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — COSTS · "bills" (twin of desk_bills.gd). W2 lane: L-MONEY.
    /// THE QUESTION THIS DESK ANSWERS: "what must be paid every Monday?"
    /// No shipped ancestor: the stub renders the question as its hero and an
    /// honest pen note until the lane lands.
    /// </summary>
    public static class DeskBills
    {
        public const string Question = "what must be paid every Monday?";

        public static string[] HeroSummary(GameState s)
        {
            return new[] { "the floor", "the flat vs the scaling" };
        }

        public static void Draw(BinderScreen b)
        {
            DeskKit.UnderConstruction(b, "the floor", Question, "the bills ledger splits out of the old ledger here — until it lands, the whole book reads under spend");
        }

        public static void Handle(BinderScreen b, string id) { }
    }
}
