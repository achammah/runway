using System.Collections.Generic;

namespace Runway.Core
{
    /// <summary>
    /// LANE 09 — HARDWARE PRODUCTION (build, stock, machines). Spec: docs/design/09-hardware.md
    ///
    /// THE STUB the engine spine planted. Every entry point below is a no-op,
    /// and the weekly tick is arithmetically identical while they stay that way
    /// — that is what lets this lane be written against a live engine without
    /// touching a shared file. Fill the bodies here; the call sites exist.
    ///
    /// The spine calls, in tick order (00-spine section 1, HOOKS.md):
    ///   TickPre    tick 7h — PRODUCE FIRST, before adoption can spend the shelf
    ///   TickMoney  the money section — write ONLY the P&amp;L lanes this lane owns
    ///   TickPost   after the week's record is written and can be read back
    /// and outside the tick: Directives feeds the DM block, Attention feeds
    /// every bang in the game through SimEngine.AttentionItems.
    ///
    /// TWIN LAW: this file and game/src/core/lanes/sim_factory.gd carry the same logic in
    /// the same order. The engines do NOT share PRNG internals, so parity means
    /// same checks and same behaviour, never a byte-equal draw.
    /// </summary>
    public static class SimFactory
    {
        // ── THE STOCK SEAM ───────────────────────────────────────────────
        /// <summary>
        /// You cannot sell what you did not build. The engine hands over the
        /// week's demand AFTER the go-to-market clamp; clamp it to the shelf,
        /// decrement the shelf, and receipt the stockout. Off Hardware, hand
        /// `adds` straight back and demand stays stock-free as it is today.
        /// </summary>
        public static double ClampAdds(GameState state, WeeklyReport rep, double adds)
        {
            return adds;
        }

        /// <summary>Tick 7h: build target, produce (learning curve), breakdown roll (salt 110). PRODUCE FIRST — stock must exist before section 8 is allowed to sell it.</summary>
        public static void TickPre(GameState state, WeeklyReport rep)
        {
        }

        /// <summary>
        /// The money section. `m` is the working P&amp;L record — one field per lane
        /// of 00-spine section 2. Write only what this subsystem owns; the spine
        /// sums burn and writes the record whole.
        /// </summary>
        public static void TickMoney(GameState state, WeeklyReport rep, MoneyWork m)
        {
        }

        /// <summary>After the record is written: stockout and overstock bookkeeping for the closed week.</summary>
        public static void TickPost(GameState state, WeeklyReport rep)
        {
        }

        /// <summary>DM context lines, section 13 of the DIRECTIVES block.
        /// Return plain strings; the spine orders them and caps the block.</summary>
        public static List<string> Directives(GameState state)
        {
            return new List<string>();
        }

        /// <summary>
        /// Attention rows — the product desk (stockout, overstock, a machine down).
        /// Each row carries desk, key, severity and a label of 40 characters or
        /// less: the garage ticker prints it verbatim (00-spine section 4).
        /// </summary>
        public static List<AttentionItem> Attention(GameState state)
        {
            return new List<AttentionItem>();
        }
    }
}
