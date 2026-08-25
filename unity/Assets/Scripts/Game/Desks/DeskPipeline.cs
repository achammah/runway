namespace Runway.Game
{
    /// <summary>
    /// DESK — the binder's `customers` tab, Enterprise branch.
    /// Spec: docs/design/05-enterprise-pipeline.md
    ///
    /// THE STUB the spine planted. DeskCustomers dispatches the Enterprise page
    /// here and passes the BinderScreen ITSELF, so this file draws through the
    /// binder's own helpers and never reaches into the sheet directly. Empty
    /// until the lane fills it.
    ///
    /// THE HANDOVER IS THIS LANE'S CALL: OwnsPage answers false while the board
    /// is a stub, so an Enterprise run keeps today's customer page until this
    /// file can draw something better. Flip it to `b.State.BizWho == "Enterprise"`
    /// in the same commit that fills DrawBoard — that one line is the whole
    /// takeover, and nobody has to touch DeskCustomers.cs for it.
    ///
    /// The bar every surface ships at (00-spine section 11): readable first pass
    /// by a tired player; concepts named in real business terms with a teaching
    /// line where a number first appears; no dead ends and every state leavable;
    /// drawn in the game's hand, never a SaaS panel. The shared components live
    /// in Game/DeskKit.cs — the stage board is DeskKit.Board().
    ///
    /// TWIN LAW: this file and game/src/ui/desks/desk_pipeline.gd draw the same
    /// rows at the same coordinates.
    /// </summary>
    public static class DeskPipeline
    {
        /// <summary>
        /// Does the pipeline own the customers page on this run? False until the
        /// board is real: an empty page is a worse Enterprise desk than today's
        /// funnel read.
        /// </summary>
        public static bool OwnsPage(BinderScreen b)
        {
            return false;
        }

        /// <summary>Draw the stage board, lead chips, signed-logos strip and teaching footer.</summary>
        public static void Draw(BinderScreen b)
        {
        }

        /// <summary>A press inside this desk. `id` is whatever Draw registered.</summary>
        public static void Handle(BinderScreen b, string id)
        {
        }

        /// <summary>Drawn INSIDE the customers desk on Enterprise runs.</summary>
        public static void DrawBoard(BinderScreen b)
        {
        }
    }
}
