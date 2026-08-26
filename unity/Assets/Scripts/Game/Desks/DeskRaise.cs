using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — THE COMPANY · "the raise" (twin of desk_raise.gd). W2 lane: L-OWN.
    /// THE QUESTION THIS DESK ANSWERS: "who would fund us next, and at what true price?"
    /// No shipped ancestor: the stub renders the question as its hero and an
    /// honest pen note until the lane lands.
    /// </summary>
    public static class DeskRaise
    {
        public const string Question = "who would fund us next, and at what true price?";

        public static string[] HeroSummary(GameState s)
        {
            return new[] { "the raise", "radar -> conversations -> terms -> wired" };
        }

        public static void Draw(BinderScreen b)
        {
            DeskKit.UnderConstruction(b, "the raise", Question, "the fundraising pipeline — every instrument with its true character, term sheets compared for their real price; a raise costs founder time");
        }

        public static void Handle(BinderScreen b, string id) { }
    }
}
