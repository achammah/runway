using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runway.App
{
    /// The screens main.gd swaps between, in the order a first boot meets them.
    public enum AppState
    {
        None,
        StudioCard,
        Keys,
        Title,
        HowTo,
        Draft,
        Birth,
        Book,
        Garage,
        Finale,
        Autopsy,
    }

    /// Screens that sit ON TOP of the run instead of replacing it.
    public enum AppOverlay
    {
        Settings,
        Gallery,
        EraTransition,
        Reading,
        HowTo,   // "how it works", one click away from the title
        Keys,    // "api key" — the desk is never locked away
    }

    /// <summary>
    /// WHO BUILDS WHICH SCREEN. Boot owns the flow; it does not own the screens the
    /// flow visits. A lane that writes the draft, the book or the garage registers its
    /// class here — from a [RuntimeInitializeOnLoadMethod] in its own file — and the
    /// flow picks it up with no edit to Boot:
    ///
    ///     [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    ///     static void Register()
    ///     {
    ///         ScreenRegistry.Register(AppState.Draft, typeof(FounderDraftScreen));
    ///     }
    ///
    /// A state with nothing registered is NOT a crash: Boot puts a drawn placeholder up
    /// that names the missing screen and offers the way back to the title, and logs it
    /// once. The flow stays walkable while the other lanes are still building.
    /// </summary>
    public static class ScreenRegistry
    {
        static readonly Dictionary<AppState, Type> _screens = new Dictionary<AppState, Type>();
        static readonly Dictionary<AppOverlay, Type> _overlays = new Dictionary<AppOverlay, Type>();

        public static void Register(AppState state, Type screenType)
        {
            if (screenType == null) return;
            if (!typeof(AppScreen).IsAssignableFrom(screenType))
            {
                Debug.LogError("RUNWAY! " + screenType.Name + " is not an AppScreen — "
                               + state + " not registered.");
                return;
            }
            _screens[state] = screenType;
        }

        public static void RegisterOverlay(AppOverlay overlay, Type screenType)
        {
            if (screenType == null) return;
            if (!typeof(AppScreen).IsAssignableFrom(screenType))
            {
                Debug.LogError("RUNWAY! " + screenType.Name + " is not an AppScreen — "
                               + overlay + " not registered.");
                return;
            }
            _overlays[overlay] = screenType;
        }

        public static Type Resolve(AppState state)
        {
            Type t;
            return _screens.TryGetValue(state, out t) ? t : null;
        }

        public static Type ResolveOverlay(AppOverlay overlay)
        {
            Type t;
            return _overlays.TryGetValue(overlay, out t) ? t : null;
        }

        public static bool Has(AppState state) { return _screens.ContainsKey(state); }

        public static bool HasOverlay(AppOverlay overlay) { return _overlays.ContainsKey(overlay); }
    }
}
