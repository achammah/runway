using UnityEngine;
using UnityEngine.UI;
using Runway.Effects;

namespace Runway.Game
{
    /// <summary>
    /// THE ROOM'S SIDE OF THE INK-REVEAL — one call, so the garage never has to know
    /// how the effect works or whether it is switched on.
    ///
    /// GarageScreen.AdoptComposed already is the single seam every painted room comes
    /// through: the first room of a run, every weekly repaint, and the late render
    /// that lands after the beat has closed (TurnRunner.LateScene and
    /// TurnRunner.OpenScene both hand their picture to it). Hooking here therefore
    /// covers all of them at once.
    ///
    /// THE HOOKUP (one line, inside the AdoptComposed load callback):
    ///
    ///     _composed.texture = tex;
    ///     _composed.enabled = true;
    ///     HideDrawnRoom(true);
    ///     GarageInk.Apply(_composed, tex);        // <- replaces the three fade lines
    ///
    /// Apply sets the texture and enables the image itself, so the two lines above it
    /// may stay or go. What it replaces is the CanvasGroup cross-fade:
    ///
    ///     var g = DrawnUI.Group(_composed.rectTransform);
    ///     g.alpha = 0f;
    ///     boot.StartCoroutine(DrawnUI.FadeTo(g, 1f, 0.4f));
    ///
    /// With RUNWAY_FX_REVEAL=0 those exact three lines are what runs again, so the
    /// switch is a true no-op rather than a different quiet behaviour.
    ///
    /// (This is a plain static class rather than a `partial class GarageScreen`
    /// because GarageScreen.cs declares the type without the `partial` modifier and
    /// this lane does not edit shared files. Turning it into a real partial is one
    /// word on GarageScreen.cs line 31 if the owner prefers that at integration.)
    /// </summary>
    public static class GarageInk
    {
        /// PUT THIS ROOM ON THE WALL. Paints it in over one second, or — with the
        /// kill-switch off — swaps it exactly the way the room did before.
        public static void Apply(RawImage roomImage, Texture2D newRoom)
        {
            if (roomImage == null) return;
            if (!InkReveal.Enabled)
            {
                InkReveal.Instant(roomImage, newRoom);
                return;
            }
            InkReveal.Begin(roomImage, newRoom);
        }
    }
}
