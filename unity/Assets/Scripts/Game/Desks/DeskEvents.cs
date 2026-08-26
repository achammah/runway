using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — THE LOG · "events" (twin of desk_events.gd). W2 lane: L-COMPANY.
    /// THE QUESTION THIS DESK ANSWERS: "what has the world sent us?"
    /// No shipped ancestor: the stub renders the question as its hero and an
    /// honest pen note until the lane lands.
    /// </summary>
    public static class DeskEvents
    {
        public const string Question = "what has the world sent us?";

        public static string[] HeroSummary(GameState s)
        {
            return new[] { "the mail", "letters and notices, newest first" };
        }

        public static void Draw(BinderScreen b)
        {
            DeskKit.UnderConstruction(b, "the mail", Question, "the inbox stream — investor knocks, employee asks, covenant warnings; unread bold, each with its desk jump");
        }

        public static void Handle(BinderScreen b, string id) { }
    }
}
