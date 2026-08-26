using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — REVENUE · "in motion" (twin of desk_in_motion.gd). W2 lane: L-REV.
    /// THE QUESTION THIS DESK ANSWERS: "who is on the way to becoming money?"
    /// The stub EMBEDS the shipped DeskPipeline desk so nothing regresses while the
    /// lane reworks this page to its locked pick.
    /// </summary>
    public static class DeskInMotion
    {
        public const string Question = "who is on the way to becoming money?";

        /// The group overview's card: the page's hero, one number + one line.
        public static string[] HeroSummary(GameState s)
        {
            return new[] { "the board", "river, hot list or stage board — by audience" };
        }

        public static void Draw(BinderScreen b)
        {
            DeskPipeline.Draw(b);
            DeskKit.HeroQuestion(b, Question);
        }

        public static void Handle(BinderScreen b, string id)
        {
            DeskPipeline.Handle(b, id);
        }
    }
}
