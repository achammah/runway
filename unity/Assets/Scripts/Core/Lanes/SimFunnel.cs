using System.Collections.Generic;

namespace Runway.Core
{
    /// <summary>
    /// LANE 04 — THE FUNNEL (four acquisition channels). Spec: docs/design/04-funnel-channels.md
    ///
    /// THE STUB the engine spine planted. Every entry point below is a no-op,
    /// and the weekly tick is arithmetically identical while they stay that way
    /// — that is what lets this lane be written against a live engine without
    /// touching a shared file. Fill the bodies here; the call sites exist.
    ///
    /// The spine calls, in tick order (00-spine section 1, HOOKS.md):
    ///   TickPre    tick 8 — channel stocks settle before reach is read
    ///   TickMoney  the money section — write ONLY the P&amp;L lanes this lane owns
    ///   TickPost   after the week's record is written and can be read back
    /// and outside the tick: Directives feeds the DM block, Attention feeds
    /// every bang in the game through SimEngine.AttentionItems.
    ///
    /// TWIN LAW: this file and game/src/core/lanes/sim_funnel.gd carry the same logic in
    /// the same order. The engines do NOT share PRNG internals, so parity means
    /// same checks and same behaviour, never a byte-equal draw.
    /// </summary>
    public static class SimFunnel
    {
        // ── THE REACH SEAM ───────────────────────────────────────────────
        /// <summary>
        /// THE ENGINE'S QUESTION: how much reach did this week's acquisition
        /// spend buy? `dflt` is the blended lever the engine would use on its
        /// own — hand it back unchanged and adoption is byte-identical. Replace
        /// it with the four-channel term (ads saturate, content compounds,
        /// referrals amplify, outbound is quota math) and the whole funnel
        /// lights up without the engine changing a line.
        /// </summary>
        public static double ReachMult(GameState state, double spend, double dflt)
        {
            return dflt;
        }

        /// <summary>
        /// The DM's set_budget with the founder-language cat "marketing" lands
        /// here: the narrator says "put $2k into marketing" and the ENGINE
        /// decides which channels that means, splitting by the mix the player
        /// already curated. The stub does the spine's simple ruling — legacy
        /// marketing IS paid ads.
        /// </summary>
        public static void SetMarketing(GameState state, int amount)
        {
            if (state.Budgets == null) state.Budgets = new Budgets();
            state.Budgets.Ads = amount;
        }

        /// <summary>Tick 8, before adoption: content equity compounds or rots, referral and outbound stocks settle — everything ReachMult is about to read.</summary>
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

        /// <summary>After the record is written: attribution bookkeeping for the closed week.</summary>
        public static void TickPost(GameState state, WeeklyReport rep)
        {
        }

        /// <summary>DM context lines, section (no numbered slot; rides after the street) of the DIRECTIVES block.
        /// Return plain strings; the spine orders them and caps the block.</summary>
        public static List<string> Directives(GameState state)
        {
            return new List<string>();
        }

        /// <summary>
        /// Attention rows — the ledger (a channel burning money for nothing).
        /// Each row carries desk, key, severity and a label of 40 characters or
        /// less: the garage ticker prints it verbatim (00-spine section 4).
        /// </summary>
        public static List<AttentionItem> Attention(GameState state)
        {
            return new List<AttentionItem>();
        }
    }
}
