using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — THE COMPANY · "what we make" (twin of desk_make.gd). W2 lane: L-MAKE.
    /// THE QUESTION THIS DESK ANSWERS: "what are we making, and how solid is it?"
    /// The stub EMBEDS the shipped DeskProduct desk so nothing regresses while the
    /// lane reworks this page to its locked pick.
    /// </summary>
    public static class DeskMake
    {
        public const string Question = "what are we making, and how solid is it?";

        /// The group overview's card: the page's hero, one number + one line.
        public static string[] HeroSummary(GameState s)
        {
            return new[] { "v0." + s.Product, "the kanban wall — shelf to live" };
        }

        public static void Draw(BinderScreen b)
        {
            DeskProduct.Draw(b);
            DeskKit.HeroQuestion(b, Question);
        }

        public static void Handle(BinderScreen b, string id)
        {
            DeskProduct.Handle(b, id);
        }
    }
}
