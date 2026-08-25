using System.Collections.Generic;

namespace Runway.Core
{
    /// <summary>
    /// LANE 07 — THE ROADMAP (bets, the ship roll, tech debt). Spec: docs/design/07-roadmap.md
    ///
    /// THE STUB the engine spine planted. Every entry point below is a no-op,
    /// and the weekly tick is arithmetically identical while they stay that way
    /// — that is what lets this lane be written against a live engine without
    /// touching a shared file. Fill the bodies here; the call sites exist.
    ///
    /// The spine calls, in tick order (00-spine section 1, HOOKS.md):
    ///   TickPre    tick 7 — bets progress and ship before adoption reads product
    ///   TickMoney  the money section — write ONLY the P&amp;L lanes this lane owns
    ///   TickPost   after the week's record is written and can be read back
    /// and outside the tick: Directives feeds the DM block, Attention feeds
    /// every bang in the game through SimEngine.AttentionItems.
    ///
    /// TWIN LAW: this file and game/src/core/lanes/sim_roadmap.gd carry the same logic in
    /// the same order. The engines do NOT share PRNG internals, so parity means
    /// same checks and same behaviour, never a byte-equal draw.
    /// </summary>
    public static class SimRoadmap
    {
        /// <summary>Tick 7: rnd-routed progress ticks, READY bets roll the house dice (salt 70), payoffs land. A shipped bet's quality must exist before section 8 reads product.</summary>
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

        /// <summary>After the record is written: slot refresh and the week-ahead READY notice.</summary>
        public static void TickPost(GameState state, WeeklyReport rep)
        {
        }

        /// <summary>DM context lines, section 11 of the DIRECTIVES block.
        /// Return plain strings; the spine orders them and caps the block.</summary>
        public static List<string> Directives(GameState state)
        {
            return new List<string>();
        }

        /// <summary>
        /// Attention rows — the product desk (a bet ready to ship, debt gone critical).
        /// Each row carries desk, key, severity and a label of 40 characters or
        /// less: the garage ticker prints it verbatim (00-spine section 4).
        /// </summary>
        public static List<AttentionItem> Attention(GameState state)
        {
            return new List<AttentionItem>();
        }
    }
}
