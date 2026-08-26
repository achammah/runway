using System.Collections.Generic;

namespace Runway.Core
{
    /// <summary>
    /// LANE — THE WORKS (per-type capacity, the unit ticket, relief valves).
    /// Spec: docs/design/DECISIONS.md (the factory to THE WORKS, its SCALE
    /// LADDER) + docs/design/DAG2.md.
    ///
    /// STUB (DAG2 W1). The engine spine plants the entry points; W2
    /// L-DIVWORKS fills the logic. Until then every hook is a no-op and the
    /// tick's arithmetic is byte-identical to a tree without this file.
    ///
    /// What lands here (W2): per-type capacity — service hours from the crew,
    /// software ceiling from care bandwidth, hardware machines (the factory
    /// lane's molecule, reused not forked), marketplace supply proxy; the unit
    /// ticket from the catalog's generated cost lines; relief valves priced
    /// against in-house (freelancers per session, cloud burst, the
    /// subcontract shop, recruit-supply / throttle demand) including
    /// recruit-supply bursts; learning curves per type; and the mutation-law
    /// ops that compose with the price book's contract terms (fire_account,
    /// retire_product, stop-line notice periods).
    ///
    /// The spine calls, in tick order (docs/design/HOOKS.md):
    ///   TickPre    tick 7i — per-type capacity settles AFTER production (7h)
    ///              and before the market reads it
    ///   TickMoney  the money section — this lane owns the `relief` P&amp;L lane
    ///              (freelancers, burst, subcontract relief), zero until
    ///              filled
    ///   TickPost   after the record — utilization and gap costs read the
    ///              finished week
    /// and outside the tick: Directives feeds the DM block, Attention feeds
    /// every bang in the game through SimEngine.AttentionItems.
    ///
    /// SALTS (00-spine section 3): the 160-169 decade is this lane's. Burned
    /// so far: SALT_WORKS_CAPACITY (160), SALT_WORKS_RELIEF (161),
    /// SALT_WORKS_REMAINDER (162).
    ///
    /// TWIN LAW: this file and game/src/core/lanes/sim_works.gd carry the
    /// same logic in the same order.
    /// </summary>
    public static class SimWorks
    {
        /// <summary>Tick 7i. Neutral: capacity is whatever the older lanes computed until this lands.</summary>
        public static void TickPre(GameState state, WeeklyReport rep)
        {
        }

        /// <summary>The money section. Will write ONLY m.Relief; neutral until filled.</summary>
        public static void TickMoney(GameState state, WeeklyReport rep, MoneyWork m)
        {
        }

        /// <summary>After the record is written. Neutral until the lane lands.</summary>
        public static void TickPost(GameState state, WeeklyReport rep)
        {
        }

        /// <summary>DM context lines (the spine caps the block). Empty until the lane lands.</summary>
        public static List<string> Directives(GameState state)
        {
            return new List<string>();
        }

        /// <summary>Attention rows {desk, key, severity, label}. Empty until the lane lands.</summary>
        public static List<AttentionItem> Attention(GameState state)
        {
            return new List<AttentionItem>();
        }
    }
}
