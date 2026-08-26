using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — THE COMPANY · "pivot" (twin of desk_pivot.gd). W2 lane: L-COMPANY.
    /// THE QUESTION THIS DESK ANSWERS: "what survives if we change course?"
    /// No shipped ancestor: the stub renders the question as its hero and an
    /// honest pen note until the lane lands.
    /// </summary>
    public static class DeskPivot
    {
        public const string Question = "what survives if we change course?";

        public static string[] HeroSummary(GameState s)
        {
            return new[] { "two doors", "audience pivot · product pivot" };
        }

        public static void Draw(BinderScreen b)
        {
            DeskKit.UnderConstruction(b, "two doors", Question, "the escape hatch — each door lists its exact costs, the preview computes what dies, and the arm wants the word PIVOT typed");
        }

        public static void Handle(BinderScreen b, string id) { }
    }
}
