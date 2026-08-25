using System.Collections.Generic;

namespace Runway.Core
{
    /// <summary>
    /// LANE 03 — THE STREET (rivals + macro weather). Spec: docs/design/03-rivals-macro.md
    ///
    /// THE STUB the engine spine planted. Every entry point below is a no-op,
    /// and the weekly tick is arithmetically identical while they stay that way
    /// — that is what lets this lane be written against a live engine without
    /// touching a shared file. Fill the bodies here; the call sites exist.
    ///
    /// The spine calls, in tick order (00-spine section 1, HOOKS.md):
    ///   TickPre    tick 6a/6b — rivals act, then the weather turns
    ///   TickMoney  the money section — write ONLY the P&amp;L lanes this lane owns
    ///   TickPost   after the week's record is written and can be read back
    /// and outside the tick: Directives feeds the DM block, Attention feeds
    /// every bang in the game through SimEngine.AttentionItems.
    ///
    /// TWIN LAW: this file and game/src/core/lanes/sim_street.gd carry the same logic in
    /// the same order. The engines do NOT share PRNG internals, so parity means
    /// same checks and same behaviour, never a byte-equal draw.
    /// </summary>
    public static class SimStreet
    {
        // ── THE TWO SEAMS THE SPINE LEFT OPEN ────────────────────────────
        /// <summary>
        /// Flip these when TickPre takes the job over. While they are false the
        /// engine runs its LEGACY blocks — the salt-6 strength ratchet and the
        /// plain salt-7 mood walk — so the world behaves as it always has.
        ///
        /// Owning the macro walk does NOT mean a new stream: draw the SAME
        /// single salt-7 number (SimEngine.RngForSalt(state, SimEngine.SALT_TREND))
        /// and mean-revert it, or every downstream lane's dice shift with you.
        /// </summary>
        public const bool OwnsRivals = false;
        public const bool OwnsMacro = false;

        /// <summary>Tick 6a then 6b, in that order inside this one call: per-rival upkeep, weekly action pick (salt 30), poach (31), hq disruptor (32), then the macro walk and shock roll (80). Both run BEFORE the market so a price cut or a launch shapes THIS week's demand.</summary>
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

        /// <summary>After the record is written: street bookkeeping that reads the closed week.</summary>
        public static void TickPost(GameState state, WeeklyReport rep)
        {
        }

        /// <summary>DM context lines, section 7 and 8 of the DIRECTIVES block.
        /// Return plain strings; the spine orders them and caps the block.</summary>
        public static List<string> Directives(GameState state)
        {
            return new List<string>();
        }

        /// <summary>
        /// Attention rows — the street desk (a beat the founder would retell).
        /// Each row carries desk, key, severity and a label of 40 characters or
        /// less: the garage ticker prints it verbatim (00-spine section 4).
        /// </summary>
        public static List<AttentionItem> Attention(GameState state)
        {
            return new List<AttentionItem>();
        }
    }
}
