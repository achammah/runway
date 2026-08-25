namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `crew` tab. Spec: docs/design/02-labor-market.md
    ///
    /// THE STUB the spine planted. BinderScreen dispatches the tab body here and
    /// passes ITSELF, so this file draws through the binder's own helpers and
    /// never reaches into the sheet directly. Empty until the lane fills it.
    ///
    /// The bar every surface ships at (00-spine section 11): readable first pass
    /// by a tired player; concepts named in real business terms with a teaching
    /// line where a number first appears; no dead ends and every state leavable;
    /// drawn in the game's hand, never a SaaS panel. The shared components live
    /// in Game/DeskKit.cs — use them, never fork them.
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_crew.gd draw the same rows at the
    /// same coordinates.
    /// </summary>
    public static class DeskCrew
    {
        /// <summary>Draw the roster/hiring toggle: employee rows, open roles, applicant cards.</summary>
        public static void Draw(BinderScreen b)
        {
        }

        /// <summary>A press inside this desk. `id` is whatever Draw registered.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }
    }
}
