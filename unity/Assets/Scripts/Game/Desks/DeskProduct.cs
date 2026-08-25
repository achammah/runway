namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `product` tab. Spec: docs/design/07-roadmap.md
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
    /// TWIN LAW: this file and game/src/ui/desks/desk_product.gd draw the same rows at the
    /// same coordinates.
    /// </summary>
    public static class DeskProduct
    {
        /// <summary>Draw the roadmap board: capacity, bet cards, progress, READY.</summary>
        public static void Draw(BinderScreen b)
        {
        }

        /// <summary>A press inside this desk. `id` is whatever Draw registered.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }

        /// <summary>
        /// THE BENCH belongs to the hardware lane and is drawn inside this desk
        /// on Hardware runs only. The band is ruled in 00-spine section 11
        /// (y470-740).
        /// </summary>
        public static void DrawBench(BinderScreen b)
        {
            DeskFactory.DrawBench(b);
        }
    }
}
