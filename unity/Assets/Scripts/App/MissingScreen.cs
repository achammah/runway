using TMPro;
using UnityEngine;

namespace Runway.App
{
    /// <summary>
    /// A SCREEN THAT HAS NOT BEEN BUILT YET, drawn honestly. The flow visits states the
    /// other lanes still own — the draft, the book, the garage — and a missing one has
    /// to be a visible gap with a way back, never an empty stage that reads as a broken
    /// build. It names the state, says who owns it, and offers the door to the title.
    /// </summary>
    public sealed class MissingScreen : AppScreen
    {
        protected override void OnBuild()
        {
            var boot = Boot.Instance;
            string state = boot != null ? boot.State.ToString() : "?";
            Debug.LogWarning("RUNWAY! no screen registered for " + state
                             + " — showing the placeholder. Register one with "
                             + "ScreenRegistry.Register(AppState." + state + ", typeof(YourScreen)).");

            DrawnUI.FullFill(Rect, "bg", DrawnUI.Stage, true);

            var card = DrawnUI.PaperCard(Rect, new Vector2(880f, 300f), 328f, 362f,
                                         DrawnUI.PaperStyle.Sheet);
            DrawnUI.HandLabel(card, state.ToUpperInvariant(), 56f, 44f, 54f, DrawnUI.Ink);
            DrawnUI.Rule(card, 58f, 118f, 420f, DrawnUI.Pen);
            DrawnUI.HandLabel(card,
                "this screen is not built yet — the flow reached it and stopped here.",
                58f, 146f, 28f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f), 760f);

            DrawnUI.FlatButton(card, "←  back to the title", 58f, 218f, 420f, 56f, 30f,
                               DrawnUI.Pen, DrawnUI.Ink,
                               () => { if (Boot.Instance != null) Boot.Instance.ToTitle(); });
        }
    }
}
