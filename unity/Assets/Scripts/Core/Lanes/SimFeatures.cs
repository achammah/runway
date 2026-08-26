using System.Collections.Generic;

namespace Runway.Core
{
    /// <summary>
    /// LANE — THE FEATURE INVENTORY behind WHAT WE MAKE. Spec:
    /// docs/design/DECISIONS.md (PRODUCT desk — corrected understanding, THE
    /// KANBAN WALL, its scale ladder) + docs/design/DAG2.md.
    ///
    /// STUB (DAG2 W1). The engine spine plants the entry points; W2 L-MAKE
    /// fills the logic. Until then every hook is a no-op and the tick's
    /// arithmetic is byte-identical to a tree without this file.
    ///
    /// What lands here (W2): birth features from world gen, landed bets
    /// becoming feature records, family tags (ink — free), keep-costs,
    /// solidity states and creak taxes (tech debt made visible PER FEATURE,
    /// concentrated in the plumbing), per-unit impact on the works' ticket,
    /// shelf candidates priced inside price-book bands, and
    /// promised-vs-measured checks on fresh landings.
    ///
    /// The spine calls, in tick order (docs/design/HOOKS.md):
    ///   TickPre    tick 7f — a bet that just landed becomes inventory before
    ///              anything reads the wall
    ///   TickMoney  the money section — feature keep-costs will bill here;
    ///              neutral until filled. No new P&amp;L lane is pre-registered:
    ///              keep-costs are expected to ride existing lanes (rnd /
    ///              offer cost lines) per the L-MAKE design — if the lane
    ///              needs its own column, that is a coordinator package on
    ///              the fixed money record.
    ///   TickPost   after the record — measured-vs-promised reads the
    ///              finished week
    /// and outside the tick: Directives feeds the DM block, Attention feeds
    /// every bang in the game through SimEngine.AttentionItems.
    ///
    /// SALTS (00-spine section 3): the 140-149 decade is this lane's. Burned
    /// so far: SALT_FEAT_SHELF (140), SALT_FEAT_CREAK (141),
    /// SALT_FEAT_MEASURED (142).
    ///
    /// TWIN LAW: this file and game/src/core/lanes/sim_features.gd carry the
    /// same logic in the same order.
    /// </summary>
    public static class SimFeatures
    {
        /// <summary>Tick 7f. Neutral: no bet becomes a feature until the lane lands.</summary>
        public static void TickPre(GameState state, WeeklyReport rep)
        {
        }

        /// <summary>The money section. Neutral until the lane lands.</summary>
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
