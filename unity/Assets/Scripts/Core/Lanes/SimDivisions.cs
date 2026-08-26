using System.Collections.Generic;

namespace Runway.Core
{
    /// <summary>
    /// LANE — DIVISIONS &amp; SITES. Spec: docs/design/DECISIONS.md (THE DIVISION
    /// MECHANIC, the works scale ladder, ARRANGE MODE) + docs/design/DAG2.md.
    ///
    /// STUB (DAG2 W1). The engine spine plants the entry points; W2 L-DIVWORKS
    /// fills the logic. Until then every hook is a no-op and the tick's
    /// arithmetic is byte-identical to a tree without this file.
    ///
    /// What lands here (W2): site records (open_site / close_site / edit_site
    /// with price-book costs), relocation + machine shipping, per-site demand
    /// weights, wage multipliers and learning counts, the SHARED/HQ row,
    /// group-by aggregators over records the engine already keeps
    /// (employee.site, machine.site, offer.product_id), and the deterministic
    /// rung rule — sites &gt;= 2 empire · offers &gt;= 3 house · else boutique.
    /// Divisions are NEVER generated: born only from real ops; the LLM names,
    /// never numbers.
    ///
    /// The spine calls, in tick order (docs/design/HOOKS.md):
    ///   TickPre    tick 6c — sites settle (ramps, weights) before the market
    ///              splits demand by site
    ///   TickMoney  the money section — this lane owns the `site_rent` P&amp;L
    ///              lane (per-site rents beside the era's own roof), zero
    ///              until filled
    ///   TickPost   after the record — site flags read the finished week
    /// and outside the tick: Directives feeds the DM block, Attention feeds
    /// every bang in the game through SimEngine.AttentionItems.
    ///
    /// SALTS (00-spine section 3): the 130-139 decade is this lane's. Burned
    /// so far: SALT_DIV_SITES (130), SALT_DIV_NAMES (131).
    ///
    /// TWIN LAW: this file and game/src/core/lanes/sim_divisions.gd carry the
    /// same logic in the same order.
    /// </summary>
    public static class SimDivisions
    {
        /// <summary>Tick 6c. Neutral: no sites move until the lane lands.</summary>
        public static void TickPre(GameState state, WeeklyReport rep)
        {
        }

        /// <summary>The money section. Will write ONLY m.SiteRent; neutral until filled.</summary>
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
