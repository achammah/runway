using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// THE FIRST-OPEN TOUR (twin of desk_tour.gd; DECISIONS #6): six steps —
    /// the four groups fanned with one-liners, the red demo, the handover.
    /// Click advances · Esc skips · once per install · replayable from the
    /// how-to screen. The binder forces the rail's state per step; this file
    /// draws the sheet side.
    /// </summary>
    public static class DeskTour
    {
        static readonly string[][] Steps =
        {
            new[] { "REVENUE", "the money coming in: what you sell, who buys it, who is "
                + "on the way, and what makes them come. The sage tab." },
            new[] { "COSTS", "the money going out: your chosen spend, the payroll, the "
                + "bills that arrive anyway, the bank, and the works that deliver it "
                + "all. The coral tab." },
            new[] { "THE COMPANY", "the thing itself: what you make, who owns it, who "
                + "might fund it, the street outside, and the door marked pivot. The "
                + "blue tab." },
            new[] { "THE LOG", "time: this week (the desk you play from), the run's "
                + "history, and the mail. You land here every week. The yellow tab." },
            new[] { "WHEN A TAB TURNS RED", "red means ACT — a page needs you, and its "
                + "red climbs onto the divider so a closed group can still call for "
                + "help. Coral is just money out; red is a fire." },
            new[] { "IT'S YOUR BINDER NOW", "press a divider to open its group; press an "
                + "open divider's header for the group at a glance; Esc always walks "
                + "you back out. Replay this tour any time from the how-to screen." },
        };

        public static void Draw(BinderScreen b, int step)
        {
            string[] s = Steps[Mathf.Clamp(step, 0, Steps.Length - 1)];
            b.L("the binder, in six flips", DeskKit.XId, 10f, DeskKit.Detail,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), 600f);
            float y = 140f;
            b.L((step + 1) + " / " + Steps.Length, DeskKit.XId, y, DeskKit.Detail,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.4f), 200f);
            y += 44f;
            b.L(s[0], DeskKit.XId, y, DeskKit.HeroSize,
                step == 4 ? DeskKit.Alert : DrawnUI.Ink, 1080f);
            y += 84f;
            b.L(s[1], DeskKit.XId, y, DeskKit.Row, DrawnUI.WithAlpha(DrawnUI.Ink, 0.75f),
                980f);
            y += 120f;
            if (step == 4)
                b.L("the red page tab on the rail and the red dot on its divider are the demo",
                    DeskKit.XId, y, DeskKit.Status, DeskKit.Alert, 900f);
            b.L("click to continue · Esc skips the tour", DeskKit.XId, 700f, DeskKit.Law,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 800f);
            var hit = DeskKit.Word(b, "", 0f, 0f, b.TourAdvance, DeskKit.Detail,
                                   DrawnUI.Ink, DeskKit.PaneW);
            hit.GetComponent<RectTransform>().sizeDelta = new Vector2(DeskKit.PaneW, 760f);
        }

        public static void Handle(BinderScreen b, string id) { }
    }
}
