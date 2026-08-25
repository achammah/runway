namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `product` tab. Spec: docs/design/09-hardware.md
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
    /// TWIN LAW: this file and game/src/ui/desks/desk_factory.gd draw the same rows at the
    /// same coordinates.
    /// </summary>
    public static class DeskFactory
    {
        /// <summary>Draw THE BENCH strip: stock, capacity, machines, produce steppers.</summary>
        public static void Draw(BinderScreen b)
        {
        }

        /// <summary>A press inside this desk. `id` is whatever Draw registered.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }

        /// <summary>Drawn INSIDE the product desk on Hardware runs.</summary>
        public static void DrawBench(BinderScreen b)
        {
        }
    }
}
