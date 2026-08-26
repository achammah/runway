using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — THE COMPANY · "cap table" (twin of desk_cap_page.gd). W2 lane: L-OWN.
    /// THE QUESTION THIS DESK ANSWERS: "who owns what and what\u0027s the company worth?"
    /// The stub EMBEDS the shipped DeskCap desk so nothing regresses while the
    /// lane reworks this page to its locked pick.
    /// </summary>
    public static class DeskCapPage
    {
        public const string Question = "who owns what and what\u0027s the company worth?";

        /// The group overview's card: the page's hero, one number + one line.
        public static string[] HeroSummary(GameState s)
        {
            return new[] { "you own " + s.FounderPct.ToString("0") + "%", "the slices, the dilution story, the waterfall" };
        }

        public static void Draw(BinderScreen b)
        {
            DeskCap.Draw(b);
            DeskKit.HeroQuestion(b, Question);
        }

        public static void Handle(BinderScreen b, string id)
        {
            DeskCap.Handle(b, id);
        }
    }
}
