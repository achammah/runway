using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — COSTS · "recruitment" (twin of desk_recruit.gd). W2 lane: L-OWN.
    /// THE QUESTION THIS DESK ANSWERS: "who are we hiring, and will they say yes?"
    /// No shipped ancestor: the stub renders the question as its hero and an
    /// honest pen note until the lane lands.
    /// </summary>
    public static class DeskRecruit
    {
        public const string Question = "who are we hiring, and will they say yes?";

        public static string[] HeroSummary(GameState s)
        {
            return new[] { "hiring", "roles, candidates, offers out" };
        }

        public static void Draw(BinderScreen b)
        {
            DeskKit.UnderConstruction(b, "hiring", Question, "open roles, the candidates pipeline and the offer composer — salary + options from the pool; acceptance moves with the mix");
        }

        public static void Handle(BinderScreen b, string id) { }
    }
}
