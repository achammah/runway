using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — REVENUE · "growth" (twin of desk_growth.gd). W2 lane: L-REV.
    /// THE QUESTION THIS DESK ANSWERS: "where does next week\u0027s demand come from?"
    /// No shipped ancestor: the stub renders the question as its hero and an
    /// honest pen note until the lane lands.
    /// </summary>
    public static class DeskGrowth
    {
        public const string Question = "where does next week\u0027s demand come from?";

        public static string[] HeroSummary(GameState s)
        {
            return new[] { "the garden", "four plots, steppers, yield lines" };
        }

        public static void Draw(BinderScreen b)
        {
            DeskKit.UnderConstruction(b, "the garden", Question, "the market garden — four plots with generated topics; until it lands, the channel levers still pull from COSTS -> spend");
        }

        public static void Handle(BinderScreen b, string id) { }
    }
}
