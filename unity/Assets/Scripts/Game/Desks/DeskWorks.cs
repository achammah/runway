using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — COSTS · "the works" (twin of desk_works.gd). W2 lane: L-DIVWORKS.
    /// THE QUESTION: "can we serve what they want, and what does one cost?"
    /// The stub EMBEDS the shipped factory desk and opens THE ARRANGE MODE
    /// shell the divisions lane fills.
    /// </summary>
    public static class DeskWorks
    {
        public const string Question = "can we serve what they want, and what does one cost?";

        public static string[] HeroSummary(GameState s)
        {
            return new[] { "the works", "capacity, the unit ticket, the relief valves" };
        }

        public static void Draw(BinderScreen b)
        {
            object mode;
            if (b.Desk.TryGetValue("mode", out mode) && mode != null
                && mode.ToString() == "arrange")
            {
                DeskArrange.Draw(b);
                return;
            }
            DeskFactory.Draw(b);
            DeskKit.Word(b, "arrange →", DeskKit.XId + 980f, 6f,
                () => { b.Desk["mode"] = "arrange"; }, DeskKit.Status, DrawnUI.Blue, 160f);
            DeskKit.HeroQuestion(b, Question);
        }

        public static void Handle(BinderScreen b, string id)
        {
            object mode;
            if (b.Desk.TryGetValue("mode", out mode) && mode != null
                && mode.ToString() == "arrange")
            {
                DeskArrange.Handle(b, id);
                return;
            }
            DeskFactory.Handle(b, id);
        }
    }
}
