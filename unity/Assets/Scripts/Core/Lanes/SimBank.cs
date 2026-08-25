using System.Collections.Generic;

namespace Runway.Core
{
    /// <summary>
    /// LANE 06 — THE BANK & THE STATE (credit, interest, tax). Spec: docs/design/06-finance.md
    ///
    /// THE STUB the engine spine planted. Every entry point below is a no-op,
    /// and the weekly tick is arithmetically identical while they stay that way
    /// — that is what lets this lane be written against a live engine without
    /// touching a shared file. Fill the bodies here; the call sites exist.
    ///
    /// The spine calls, in tick order (00-spine section 1, HOOKS.md):
    ///   TickPre    tick 9 — notes settle before the money is assembled
    ///   TickMoney  the money section — write ONLY the P&amp;L lanes this lane owns
    ///   TickPost   after the week's record is written and can be read back
    /// and outside the tick: Directives feeds the DM block, Attention feeds
    /// every bang in the game through SimEngine.AttentionItems.
    ///
    /// TWIN LAW: this file and game/src/core/lanes/sim_bank.gd carry the same logic in
    /// the same order. The engines do NOT share PRNG internals, so parity means
    /// same checks and same behaviour, never a byte-equal draw.
    /// </summary>
    public static class SimBank
    {
        // ── THE DEBT SEAM ────────────────────────────────────────────────
        /// <summary>
        /// While this is false the engine runs the LEGACY shark note: 18%/wk
        /// compounded into principal, auto-repaid above $2,000, its interest
        /// booked to the P&amp;L's interest lane. Flip it and this lane owns every
        /// note — the structured Loans list, honest risk-priced rates,
        /// amortization, the miss ladder.
        /// </summary>
        public const bool OwnsDebt = false;

        /// <summary>
        /// THE STATE, charged last. Tax can only be computed once every other
        /// lane has closed, because it is levied on what is LEFT: revenue minus
        /// burn minus standing liabilities minus interest. `m` is the working
        /// record, already complete except for this. Return 0 and the week is
        /// untaxed, which is the garage's truth.
        /// </summary>
        public static int TaxWk(GameState state, MoneyWork m)
        {
            return 0;
        }

        /// <summary>Tick 9, before the money is assembled: migrate legacy notes, accrue the week's schedule, run the miss ladder.</summary>
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

        /// <summary>After the record is written: anything reading the finished record (forecast, break-even).</summary>
        public static void TickPost(GameState state, WeeklyReport rep)
        {
        }

        /// <summary>DM context lines, section 10 of the DIRECTIVES block.
        /// Return plain strings; the spine orders them and caps the block.</summary>
        public static List<string> Directives(GameState state)
        {
            return new List<string>();
        }

        /// <summary>
        /// Attention rows — the bank (debt distress, the first tax week, break-even).
        /// Each row carries desk, key, severity and a label of 40 characters or
        /// less: the garage ticker prints it verbatim (00-spine section 4).
        /// </summary>
        public static List<AttentionItem> Attention(GameState state)
        {
            return new List<AttentionItem>();
        }
    }
}
