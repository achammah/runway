using System;
using System.Collections.Generic;
using System.Globalization;

namespace Runway.Core
{
    /// <summary>
    /// LANE — THE FEATURE INVENTORY behind WHAT WE MAKE. Spec:
    /// docs/design/DECISIONS.md (PRODUCT desk — corrected understanding, THE
    /// KANBAN WALL, its scale ladder) + docs/design/DAG2.md + mockups 16/17.
    ///
    /// WHAT THE LANE OWNS (DAG2 W2 L-MAKE):
    ///   · the inventory — state.Features, born at world gen (L-GEN) or
    ///     seeded from the offers on a keyless/old save, grown by LANDED
    ///     ROADMAP BETS
    ///   · keep-costs — features are never free; the weekly sum is the
    ///     product's upkeep. THE MONEY IS PACKAGE-GATED: the `feature_keep`
    ///     P&amp;L lane does not exist in the fixed money record yet, so
    ///     TickMoney stays inert and the coordinator's package activates the
    ///     one billing line (see the seam comment). Until then KeepTotal()
    ///     is display truth only.
    ///   · solidity — solid | creaky | breaking, the debt jar's per-feature
    ///     FACE. THE ONE-TAX LAW: the jar (state.TechDebt) keeps every
    ///     mechanical consequence it already has (SimRoadmap.DebtDrag
    ///     velocity, the outage roll, the build disadvantage). Creaks NEVER
    ///     tax anything themselves — they are the jar made pointable. The
    ///     weekly reconcile maps jar level ↔ creak load: target =
    ///     ceil((debt − 40) / 15) (0 at or below 40), load = creaky×1 +
    ///     breaking×2, one transition per week toward target, plumbing
    ///     first. The desk prints the tax as (1 − DebtDrag) — the jar's own
    ///     number, displayed through the creaks, applied exactly once.
    ///   · promised-vs-measured — a landed feature's Measured stays 0
    ///     (unknown) for MEASURE_WEEKS, then settles at the landing's ACTUAL
    ///     payoff units (BET_PAYOFF[amb][band], the exact engine delta the
    ///     dice granted) × a salted 0.75..1.25 market spread
    ///     (SALT_FEAT_MEASURED). The promise the card advertised is the
    ///     "fine"-band payoff, recovered from the bet record while the
    ///     8-launch history holds it; fallback base 4 when history forgot.
    ///   · THE SHELF — 3..5 candidate ideas, re-drawn deterministically per
    ///     (seed, week) on SALT_FEAT_SHELF from era + business type + the
    ///     wall's gaps; a creak draws the rebuild that kills it. Priced by
    ///     the engine's own tables (weeks × RND_PER_WEEK). Committing one
    ///     materializes a REAL bet through the roadmap's own door.
    ///   · THE NEXT QUEUE — chosen-but-waiting bets. Storage: the bet's own
    ///     CommittedWeek, NEGATIVE = queued at position −CommittedWeek (same
    ///     save key both engines; the roadmap only reads the field on READY
    ///     bets, which a queued bet is not). Each tick the head commits
    ///     itself while WIP slots are free.
    ///
    /// LANDED BETS → FEATURES (documented, tested): quality→charge,
    /// retention→keep, reach→pull, platform→plumbing; debt → no new feature,
    /// the rebuild HEALS the worst creak on landing; band backfired → no
    /// feature; band risky → born CREAKY. keep_wk = KEEP_ERA[era] ×
    /// ambition; unit_cost_add = keep_wk / 20 (SimWorks reads it additively
    /// for the ticket — no seam needed).
    ///
    /// THE LANDING SEAM: LANDING_SEAM_LIVE is false until the coordinator's
    /// package plants the OnBetLanded() call inside SimRoadmap.ShipBet and
    /// flips it. Until then TickPre polls this week's landings.
    ///
    /// The spine calls, in tick order (docs/design/HOOKS.md):
    ///   TickPre    tick 7f — landings join the inventory, the queue advances
    ///   TickMoney  the money section — INERT until the feature_keep package
    ///   TickPost   after the record — measured settles, solidity reconciles
    ///
    /// SALTS (00-spine section 3), this lane's decade only: SALT_FEAT_SHELF
    /// 140, SALT_FEAT_CREAK 141, SALT_FEAT_MEASURED 142.
    ///
    /// TWIN LAW: this file and game/src/core/lanes/sim_features.gd carry the
    /// same logic in the same order. The C# Rng draws different values than
    /// Godot's for the same key (Rng.cs's documented divergence): the laws
    /// match, the flavour differs.
    /// </summary>
    public static class SimFeatures
    {
        /// <summary>The package flips this when SimRoadmap.ShipBet gains the
        /// OnBetLanded call; the polling fallback stands down the same commit.</summary>
        public const bool LANDING_SEAM_LIVE = true;

        /// <summary>Weeks between a landing and its measured verdict.</summary>
        public const int MEASURE_WEEKS = 4;

        /// <summary>keep_wk = KEEP_ERA[era] × ambition — the banded upkeep.</summary>
        public static readonly Dictionary<string, int> KEEP_ERA = new Dictionary<string, int>
        {
            { "garage", 3 }, { "coworking", 4 }, { "office", 5 },
            { "floor", 7 }, { "hq", 9 },
        };

        /// <summary>One creak per this many debt points over the free line (40).</summary>
        public const double CREAK_STEP = 15.0;

        public const double MEASURE_SPREAD_LO = 0.75;
        public const double MEASURE_SPREAD_HI = 1.25;
        /// <summary>When the 8-launch history forgot the source bet.</summary>
        public const int MEASURE_FALLBACK_UNITS = 4;

        public const int SHELF_MIN = 3;
        public const int SHELF_MAX = 5;

        /// <summary>bet kind → feature job, and back (the documented mapping).</summary>
        public static readonly Dictionary<string, string> KIND_TO_JOB = new Dictionary<string, string>
        {
            { "quality", "charge" }, { "retention", "keep" },
            { "reach", "pull" }, { "platform", "plumbing" },
        };
        public static readonly Dictionary<string, string> JOB_TO_KIND = new Dictionary<string, string>
        {
            { "charge", "quality" }, { "keep", "retention" },
            { "pull", "reach" }, { "plumbing", "platform" },
        };
        /// <summary>The job said in the wall's own words.</summary>
        public static readonly Dictionary<string, string> JOB_WORDS = new Dictionary<string, string>
        {
            { "pull", "brings them in" }, { "keep", "keeps them" },
            { "charge", "lets us charge" }, { "plumbing", "the plumbing" },
        };

        public sealed class ShelfIdea
        {
            public string Name = "";
            public string Job = "keep";
        }

        /// <summary>THE KEYLESS SHELF POOLS, per business type — vocabulary
        /// only; every number comes from the tables.</summary>
        public static readonly Dictionary<string, ShelfIdea[]> SHELF_POOL =
            new Dictionary<string, ShelfIdea[]>
        {
            { "Software", new[]
                {
                    new ShelfIdea { Name = "white-label", Job = "charge" },
                    new ShelfIdea { Name = "group scheduling", Job = "pull" },
                    new ShelfIdea { Name = "SMS pack", Job = "keep" },
                    new ShelfIdea { Name = "calendar sync", Job = "keep" },
                    new ShelfIdea { Name = "analytics pack", Job = "keep" },
                    new ShelfIdea { Name = "the referral loop", Job = "pull" },
                    new ShelfIdea { Name = "the exports pack", Job = "charge" },
                    new ShelfIdea { Name = "team spaces", Job = "pull" },
                } },
            { "Service", new[]
                {
                    new ShelfIdea { Name = "home visits", Job = "pull" },
                    new ShelfIdea { Name = "corporate packages", Job = "charge" },
                    new ShelfIdea { Name = "the gift card", Job = "pull" },
                    new ShelfIdea { Name = "memberships", Job = "keep" },
                    new ShelfIdea { Name = "the loyalty card", Job = "keep" },
                    new ShelfIdea { Name = "the premium hour", Job = "charge" },
                    new ShelfIdea { Name = "the referral card", Job = "pull" },
                    new ShelfIdea { Name = "the seasonal line", Job = "keep" },
                } },
            { "Hardware", new[]
                {
                    new ShelfIdea { Name = "the pro bundle", Job = "charge" },
                    new ShelfIdea { Name = "the accessory line", Job = "pull" },
                    new ShelfIdea { Name = "the rugged build", Job = "keep" },
                    new ShelfIdea { Name = "the companion app", Job = "keep" },
                    new ShelfIdea { Name = "spare-parts program", Job = "pull" },
                    new ShelfIdea { Name = "the limited edition", Job = "charge" },
                    new ShelfIdea { Name = "quick-swap parts", Job = "keep" },
                    new ShelfIdea { Name = "the starter kit", Job = "pull" },
                } },
            { "Marketplace", new[]
                {
                    new ShelfIdea { Name = "subscriptions", Job = "keep" },
                    new ShelfIdea { Name = "the gift registry", Job = "pull" },
                    new ShelfIdea { Name = "B2B invoicing", Job = "charge" },
                    new ShelfIdea { Name = "bulk orders", Job = "charge" },
                    new ShelfIdea { Name = "seller analytics", Job = "keep" },
                    new ShelfIdea { Name = "buyer protection", Job = "keep" },
                    new ShelfIdea { Name = "the weekly digest", Job = "pull" },
                    new ShelfIdea { Name = "same-day courier", Job = "charge" },
                } },
        };

        // ═════════════════════ THE SPINE'S ENTRY POINTS ═════════════════════

        /// <summary>Tick 7f. The wall settles before anything reads it.</summary>
        public static void TickPre(GameState state, WeeklyReport rep)
        {
            SeedDefaults(state);
            if (!LANDING_SEAM_LIVE) PollLandings(state, rep);
            RunQueue(state, rep);
        }

        /// <summary>The money section. INERT BY DESIGN until the coordinator's
        /// feature_keep package lands: the money record's fields are fixed in
        /// MoneyWork and no feature lane exists yet.</summary>
        public static void TickMoney(GameState state, WeeklyReport rep, MoneyWork m)
        {
            m.FeatureKeep += KeepTotal(state);
            if (KeepTotal(state) > 0)
                rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "product upkeep: ${0}/wk keeps {1} features alive",
                    KeepTotal(state), state.Features.Count));
        }

        /// <summary>After the record: measured settles, solidity reconciles.</summary>
        public static void TickPost(GameState state, WeeklyReport rep)
        {
            Measure(state, rep);
            ReconcileSolidity(state, rep);
        }

        /// <summary>DM context lines (the spine caps the block).</summary>
        public static List<string> Directives(GameState state)
        {
            var outp = new List<string>();
            int creaks = CreakCount(state);
            if (creaks > 0)
            {
                int tax = CreakTaxPct(state);
                string worst = WorstCreakName(state);
                if (tax > 0)
                    outp.Add(string.Format(CultureInfo.InvariantCulture,
                        "- The wall creaks at '{0}' ({1} creaky) — build speed −{2}%. A rebuild bet kills a creak.",
                        worst, creaks, tax));
                else
                    outp.Add(string.Format(CultureInfo.InvariantCulture,
                        "- '{0}' shipped hot and creaks — a rebuild bet firms it up.", worst));
            }
            foreach (Feature f in state.Features)
            {
                if (f.BornWk == state.Week && f.BornWk > 0)
                {
                    string job;
                    if (!JOB_WORDS.TryGetValue(f.Job ?? "", out job)) job = "plumbing";
                    outp.Add(string.Format(CultureInfo.InvariantCulture,
                        "- NEW ON THE WALL: '{0}' joined what we make ({1}).", f.Name, job));
                    break;
                }
            }
            return outp;
        }

        /// <summary>Attention rows — the desk names the new page directly
        /// (the binder's alias map passes unknown desks through).</summary>
        public static List<AttentionItem> Attention(GameState state)
        {
            var rows = new List<AttentionItem>();
            int breaking = BreakingCount(state);
            if (breaking > 0)
            {
                // ≤40 chars: 24 of template + a 16-char name
                string nm = WorstCreakName(state);
                if (nm.Length > 16) nm = nm.Substring(0, 16);
                rows.Add(new AttentionItem
                {
                    Desk = "what we make", Key = "feature_breaking", Severity = 3,
                    Control = "rebuild",
                    Label = string.Format(CultureInfo.InvariantCulture,
                        "'{0}' is breaking — rebuild", nm),
                });
            }
            int creaks = CreakCount(state);
            if (creaks > 0 && breaking == 0)
            {
                int tax = CreakTaxPct(state);
                if (tax > 0)
                    rows.Add(new AttentionItem
                    {
                        Desk = "what we make", Key = "creak_tax", Severity = 2,
                        Control = "rebuild",
                        Label = string.Format(CultureInfo.InvariantCulture,
                            "{0} creak{1} — build speed −{2}%", creaks,
                            creaks == 1 ? "" : "s", tax),
                    });
                else
                    rows.Add(new AttentionItem
                    {
                        Desk = "what we make", Key = "creak_tax", Severity = 2,
                        Control = "rebuild",
                        Label = string.Format(CultureInfo.InvariantCulture,
                            "{0} creak{1} on the wall — rebuild", creaks,
                            creaks == 1 ? "" : "s"),
                    });
            }
            int keep = KeepTotal(state);
            int revenue = state.LastPnl != null ? state.LastPnl.Revenue : 0;
            if (keep >= 50 && revenue > 0 && keep * 4 >= revenue)
                rows.Add(new AttentionItem
                {
                    Desk = "what we make", Key = "keep_spike", Severity = 2,
                    Control = "keep_total",
                    Label = string.Format(CultureInfo.InvariantCulture,
                        "keep ${0}/wk eats {1}% of revenue", keep,
                        (int)(keep * 100.0 / revenue)),
                });
            return rows;
        }

        // ═════════════════════ BIRTH & THE DEFAULT SET ═══════════════════════

        /// <summary>A run with no generated inventory still has a wall: a
        /// minimal set derived from what it already sells. BornWk 0 marks a
        /// birth feature — never measured (there was no promise).</summary>
        public static void SeedDefaults(GameState state)
        {
            if (state.Features.Count > 0) return;
            if (state.Offers.Count == 0 && state.Traction <= 0 && state.Product <= 0)
                return;   // a truly blank state (the draft) keeps an empty wall
            int baseKeep;
            if (!KEEP_ERA.TryGetValue(state.Era ?? "", out baseKeep)) baseKeep = 3;
            string flagship = "what we sell";
            if (state.Offers.Count > 0 && !string.IsNullOrEmpty(state.Offers[0].Name))
                flagship = state.Offers[0].Name;
            if (flagship.Length > 28) flagship = flagship.Substring(0, 28);
            state.Features = new List<Feature>
            {
                new Feature { Id = "ft_seed_pull", Name = "the front door", Job = "pull",
                    Family = "", Solidity = "solid", KeepWk = baseKeep,
                    UnitCostAdd = baseKeep / 20.0, ProductId = "", BornWk = 0, Measured = 0.0 },
                new Feature { Id = "ft_seed_core", Name = flagship, Job = "keep",
                    Family = "", Solidity = "solid", KeepWk = baseKeep * 2,
                    UnitCostAdd = baseKeep * 2 / 20.0, ProductId = "", BornWk = 0, Measured = 0.0 },
                new Feature { Id = "ft_seed_plumb", Name = "the plumbing", Job = "plumbing",
                    Family = "", Solidity = "solid", KeepWk = baseKeep * 2,
                    UnitCostAdd = 0.0, ProductId = "", BornWk = 0, Measured = 0.0 },
            };
        }

        /// <summary>THE LANDING, one door for both routes (the tick's poll
        /// today, the roadmap seam after the package). Idempotent for births
        /// (name + born_wk guard); heals are guarded by the caller's window.</summary>
        public static void OnBetLanded(GameState state, Bet bet, WeeklyReport rep)
        {
            if (bet.Band == "backfired") return;
            if (bet.Kind == "debt")
            {
                HealWorst(state, rep, bet.Name ?? "");
                return;
            }
            string job;
            if (!KIND_TO_JOB.TryGetValue(bet.Kind ?? "", out job)) return;
            string name = bet.Name ?? "";
            int born = bet.ShippedWeek;
            foreach (Feature f in state.Features)
                if (f.Name == name && f.BornWk == born)
                    return;   // already on the wall
            int amb = Math.Max(1, Math.Min(3, bet.Ambition));
            int baseKeep;
            if (!KEEP_ERA.TryGetValue(state.Era ?? "", out baseKeep)) baseKeep = 3;
            int keep = baseKeep * amb;
            int n = 0;
            foreach (Feature f2 in state.Features)
                if (f2.BornWk == born) n++;
            bool creaky = bet.Band == "risky";
            string shortName = name.Length > 28 ? name.Substring(0, 28) : name;
            state.Features.Add(new Feature
            {
                Id = string.Format(CultureInfo.InvariantCulture, "ft_w{0}_{1}", born, n + 1),
                Name = shortName,
                Job = job,
                Family = "",
                Solidity = creaky ? "creaky" : "solid",
                KeepWk = keep,
                UnitCostAdd = keep / 20.0,
                ProductId = "",
                BornWk = born,
                Measured = 0.0,
            });
            if (rep != null)
            {
                string jobWords;
                if (!JOB_WORDS.TryGetValue(job, out jobWords)) jobWords = "";
                rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "the wall grows: '{0}' joins what we make ({1}) — keep ${2}/wk",
                    name, jobWords, keep));
                if (creaky)
                    rep.Lines.Add("  → shipped in a hurry: it starts life CREAKY");
            }
        }

        /// <summary>The polling fallback: this week's landings, one tick
        /// behind a desk press at worst. Heals only fire on bets shipped THIS
        /// week (a landing is healed exactly once).</summary>
        static void PollLandings(GameState state, WeeklyReport rep)
        {
            foreach (Bet bd in state.Bets)
            {
                if (!bd.Shipped) continue;
                int wk = bd.ShippedWeek;
                if (bd.Kind == "debt")
                {
                    if (wk == state.Week) OnBetLanded(state, bd, rep);
                }
                else if (wk >= state.Week - 1)
                {
                    OnBetLanded(state, bd, rep);
                }
            }
        }

        /// <summary>A rebuild kills the creak on landing: the worst feature
        /// goes SOLID the same week the jar pays down.</summary>
        static void HealWorst(GameState state, WeeklyReport rep, string why)
        {
            Feature target = WorstCreak(state);
            if (target == null) return;
            target.Solidity = "solid";
            if (rep != null)
                rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "the rebuild lands ('{0}'): '{1}' is SOLID again", why, target.Name));
        }

        // ═════════════════════ THE MEASURED VERDICT ══════════════════════════

        /// <summary>After MEASURE_WEEKS the world answers: measured = the
        /// landing's ACTUAL payoff units × a salted 0.75..1.25 spread, snapped
        /// to 0.1 with the same explicit formula in both engines. 0 stays the
        /// "not yet" sentinel; birth features (BornWk 0) are never measured.</summary>
        static void Measure(GameState state, WeeklyReport rep)
        {
            Rng r = null;
            foreach (Feature f in state.Features)
            {
                int born = f.BornWk;
                if (born < 2 || f.Measured != 0.0) continue;
                if (state.Week - born < MEASURE_WEEKS) continue;
                if (r == null) r = SimEngine.RngForSalt(state, SimEngine.SALT_FEAT_MEASURED);
                int baseUnits = MEASURE_FALLBACK_UNITS;
                int promised = 0;
                Bet src = SourceBet(state, f.Name ?? "", born);
                if (src != null)
                {
                    int amb = Math.Max(1, Math.Min(3, src.Ambition));
                    int bi = Math.Max(0, Array.IndexOf(SimRoadmap.BANDS,
                        string.IsNullOrEmpty(src.Band) ? "fine" : src.Band));
                    baseUnits = SimRoadmap.BET_PAYOFF[amb - 1][bi];
                    promised = SimRoadmap.BET_PAYOFF[amb - 1][1];
                }
                double spread = r.RandfRange(MEASURE_SPREAD_LO, MEASURE_SPREAD_HI);
                double measured = Math.Floor(Math.Max(baseUnits * spread, 0.1) / 0.1 + 0.5) * 0.1;
                f.Measured = measured;
                if (rep != null)
                {
                    if (promised > 0)
                        rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                            "measured: '{0}' promised +{1}, the market says +{2:0.0}",
                            f.Name, promised, measured));
                    else
                        rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                            "measured: '{0}' settles at +{1:0.0}", f.Name, measured));
                }
            }
        }

        /// <summary>The source bet while the 8-launch history still holds it.</summary>
        static Bet SourceBet(GameState state, string name, int born)
        {
            foreach (Bet bd in state.Bets)
                if (bd.Shipped && bd.ShippedWeek == born && bd.Name == name)
                    return bd;
            return null;
        }

        /// <summary>The promise a landed feature's card advertised (the
        /// "fine" payoff), while history remembers. 0 = history forgot.</summary>
        public static int PromisedUnits(GameState state, Feature feature)
        {
            Bet src = SourceBet(state, feature.Name ?? "", feature.BornWk);
            if (src == null) return 0;
            int amb = Math.Max(1, Math.Min(3, src.Ambition));
            return SimRoadmap.BET_PAYOFF[amb - 1][1];
        }

        // ═════════════════ SOLIDITY — THE JAR'S FACE ═════════════════════════

        /// <summary>The jar level the wall should show: 0 creaks at or under
        /// the free line, one more per CREAK_STEP points above it.</summary>
        public static int ExpectedCreakLoad(double debt)
        {
            if (debt <= SimRoadmap.DEBT_FREE) return 0;
            return (int)Math.Ceiling((debt - SimRoadmap.DEBT_FREE) / CREAK_STEP);
        }

        /// <summary>creaky counts 1, breaking counts 2.</summary>
        public static int CreakLoad(GameState state)
        {
            int load = 0;
            foreach (Feature f in state.Features)
            {
                if (f.Solidity == "creaky") load += 1;
                else if (f.Solidity == "breaking") load += 2;
            }
            return load;
        }

        /// <summary>ONE transition a week toward the jar's truth. Worsening
        /// picks a solid feature on SALT_FEAT_CREAK (plumbing first); with no
        /// solid feature left, the oldest creak breaks. Healing un-breaks
        /// first, then firms the oldest creak. The tax itself lives in
        /// SimRoadmap.DebtDrag and NOWHERE here — one jar, one tax.</summary>
        static void ReconcileSolidity(GameState state, WeeklyReport rep)
        {
            if (state.Features.Count == 0) return;
            int target = ExpectedCreakLoad(state.TechDebt);
            int load = CreakLoad(state);
            if (load < target)
            {
                var pool = new List<Feature>();
                foreach (Feature f in state.Features)
                    if (f.Solidity == "solid" && f.Job == "plumbing")
                        pool.Add(f);
                if (pool.Count == 0)
                    foreach (Feature f2 in state.Features)
                        if (f2.Solidity == "solid")
                            pool.Add(f2);
                if (pool.Count > 0)
                {
                    Rng r = SimEngine.RngForSalt(state, SimEngine.SALT_FEAT_CREAK);
                    Feature pick = pool[r.RandiRange(0, pool.Count - 1)];
                    pick.Solidity = "creaky";
                    if (rep != null)
                        rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                            "the debt shows its face: '{0}' starts creaking (debt {1})",
                            pick.Name, (int)state.TechDebt));
                }
                else
                {
                    foreach (Feature f3 in state.Features)
                    {
                        if (f3.Solidity != "creaky") continue;
                        f3.Solidity = "breaking";
                        if (rep != null)
                            rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                                "'{0}' is BREAKING — the debt is collecting (debt {1})",
                                f3.Name, (int)state.TechDebt));
                        break;
                    }
                }
            }
            else if (load > target)
            {
                bool healed = false;
                foreach (Feature f4 in state.Features)
                {
                    if (f4.Solidity != "breaking") continue;
                    f4.Solidity = "creaky";
                    if (rep != null)
                        rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                            "'{0}' steps back from the edge — still creaky", f4.Name));
                    healed = true;
                    break;
                }
                if (!healed)
                {
                    foreach (Feature f5 in state.Features)
                    {
                        if (f5.Solidity != "creaky") continue;
                        f5.Solidity = "solid";
                        if (rep != null)
                            rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                                "the codebase breathes: '{0}' firms up", f5.Name));
                        break;
                    }
                }
            }
        }

        /// <summary>breaking first, then creaky; the plumbing first inside a
        /// class — the rebuild's target and the attention row's name.</summary>
        static Feature WorstCreak(GameState state)
        {
            string[] classes = { "breaking", "creaky" };
            bool[] plumbs = { true, false };
            foreach (string solidity in classes)
                foreach (bool plumb in plumbs)
                    foreach (Feature f in state.Features)
                    {
                        if (f.Solidity != solidity) continue;
                        if ((f.Job == "plumbing") != plumb) continue;
                        return f;
                    }
            return null;
        }

        // ═════════════════ PURE READS (the desk) ════════════════════════════

        public static int KeepTotal(GameState state)
        {
            int total = 0;
            foreach (Feature f in state.Features) total += f.KeepWk;
            return total;
        }

        /// <summary>The build spend the wall's footer prints: while a bet is
        /// committed the whole rnd lever buys weeks, so that IS the number.</summary>
        public static int BuildTotal(GameState state)
        {
            if (SimRoadmap.CommittedBets(state).Count == 0) return 0;
            return state.Budgets.Rnd;
        }

        /// <summary>Per-unit impact on the works' ticket: the flagship's own
        /// features plus the shared plumbing every product stands on.</summary>
        public static double UnitCostTotal(GameState state, string productId = "")
        {
            double total = 0.0;
            foreach (Feature f in state.Features)
            {
                string pid = f.ProductId ?? "";
                if (pid == "" || pid == productId) total += f.UnitCostAdd;
            }
            return total;
        }

        public static int CreakCount(GameState state)
        {
            int n = 0;
            foreach (Feature f in state.Features)
                if (f.Solidity != "solid") n++;
            return n;
        }

        public static int BreakingCount(GameState state)
        {
            int n = 0;
            foreach (Feature f in state.Features)
                if (f.Solidity == "breaking") n++;
            return n;
        }

        public static string WorstCreakName(GameState state)
        {
            Feature w = WorstCreak(state);
            return w != null ? (w.Name ?? "") : "";
        }

        /// <summary>THE ONE TAX, displayed: the jar's own velocity interest in
        /// percent. The creaks are its face; nothing applies a second one.</summary>
        public static int CreakTaxPct(GameState state)
        {
            return (int)Math.Round((1.0 - SimRoadmap.DebtDrag(state)) * 100.0,
                MidpointRounding.AwayFromZero);
        }

        /// <summary>Distinct non-flagship product ids on the wall (rung 3
        /// fires at ≥1: the flagship plus a named product = many things).</summary>
        public static List<string> ProductIds(GameState state)
        {
            var outp = new List<string>();
            foreach (Feature f in state.Features)
            {
                string pid = f.ProductId ?? "";
                if (pid != "" && !outp.Contains(pid)) outp.Add(pid);
            }
            foreach (Offer o in state.Offers)
            {
                string pid2 = o.ProductId ?? "";
                if (pid2 != "" && !outp.Contains(pid2)) outp.Add(pid2);
            }
            return outp;
        }

        // ═════════════════════════ THE SHELF ════════════════════════════════

        public sealed class ShelfCandidate
        {
            public string Id = "";
            public string Name = "";
            public string Kind = "";
            public int Ambition = 1;
            public string Job = "";
            public string JobWords = "";
            public double CostRndWeeks;
            public int CostUsd;
            public int Weeks;
            public int OddsPct;
        }

        /// <summary>3..5 priced candidates, re-drawn deterministically per
        /// (seed, week) on SALT_FEAT_SHELF: gap jobs draw first, a creak draws
        /// the rebuild that kills it, the rest from the type's own pool.</summary>
        public static List<ShelfCandidate> ShelfCandidates(GameState state)
        {
            var outp = new List<ShelfCandidate>();
            Rng r = SimEngine.RngForSalt(state, SimEngine.SALT_FEAT_SHELF);
            ShelfIdea[] pool;
            if (!SHELF_POOL.TryGetValue(state.BizWhat ?? "", out pool))
                pool = SHELF_POOL["Software"];
            var taken = new List<string>();
            foreach (Feature f in state.Features) taken.Add(f.Name ?? "");
            foreach (Bet b in state.Bets) taken.Add(b.Name ?? "");
            int n = SHELF_MIN + (state.EraIndex() >= 2 ? 1 : 0);
            // 1 ── the rebuild, when the wall creaks
            if (CreakCount(state) > 0)
            {
                string worst = WorstCreakName(state);
                if (worst.Length > 18) worst = worst.Substring(0, 18);
                outp.Add(Candidate(state, r,
                    string.Format(CultureInfo.InvariantCulture, "rebuild: {0}", worst),
                    "debt", 1));
                n = Math.Min(n + 1, SHELF_MAX);
            }
            // 2 ── the gaps draw first: a job nobody on the wall does
            var jobsLive = new List<string>();
            foreach (Feature f2 in state.Features)
                if (!jobsLive.Contains(f2.Job ?? "")) jobsLive.Add(f2.Job ?? "");
            string[] gaps = { "pull", "keep", "charge" };
            foreach (string gap in gaps)
            {
                if (outp.Count >= n) break;
                if (jobsLive.Contains(gap)) continue;
                ShelfIdea pick = DrawFromPool(r, pool, taken, gap);
                if (pick != null)
                {
                    taken.Add(pick.Name);
                    string kind;
                    if (!JOB_TO_KIND.TryGetValue(gap, out kind)) kind = "quality";
                    outp.Add(Candidate(state, r, pick.Name, kind, 0));
                }
            }
            // 3 ── the rest of the shelf from the pool, any job
            while (outp.Count < n)
            {
                ShelfIdea pick2 = DrawFromPool(r, pool, taken, "");
                if (pick2 == null) break;
                taken.Add(pick2.Name);
                string kind2;
                if (!JOB_TO_KIND.TryGetValue(pick2.Job ?? "keep", out kind2))
                    kind2 = "retention";
                outp.Add(Candidate(state, r, pick2.Name, kind2, 0));
            }
            return outp;
        }

        /// <summary>One shelf candidate, priced by the engine's own tables.
        /// ambFixed 0 = draw one inside the era's cap.</summary>
        static ShelfCandidate Candidate(GameState state, Rng r, string name,
                                        string kind, int ambFixed)
        {
            int amb = ambFixed;
            if (amb <= 0) amb = r.RandiRange(1, SimRoadmap.AmbitionCap(state));
            double cost = SimRoadmap.BetCost(kind, amb);
            int dc = kind == "platform" ? SimRoadmap.PLATFORM_DC
                : SimRoadmap.DC_BY_AMBITION[Math.Max(1, Math.Min(3, amb)) - 1];
            int mod = state.Competence("build") - 3;
            int need = Math.Max(2, Math.Min(20, dc - mod));
            string job = kind == "debt" ? "plumbing" : KIND_TO_JOB[kind];
            string jobWords;
            if (kind == "debt") jobWords = "kills a creak";
            else if (!JOB_WORDS.TryGetValue(job, out jobWords)) jobWords = "";
            if (name.Length > 28) name = name.Substring(0, 28);
            return new ShelfCandidate
            {
                Id = string.Format(CultureInfo.InvariantCulture, "shelf_w{0}_{1}",
                    state.Week, r.RandiRange(1000, 9999)),
                Name = name,
                Kind = kind,
                Ambition = amb,
                Job = job,
                JobWords = jobWords,
                CostRndWeeks = cost,
                CostUsd = (int)(cost * SimRoadmap.RND_PER_WEEK),
                Weeks = (int)Math.Ceiling(cost),
                OddsPct = (int)Math.Round((21 - need) / 20.0 * 100.0,
                    MidpointRounding.AwayFromZero),
            };
        }

        static ShelfIdea DrawFromPool(Rng r, ShelfIdea[] pool, List<string> taken,
                                      string job)
        {
            var eligible = new List<ShelfIdea>();
            foreach (ShelfIdea c in pool)
            {
                if (taken.Contains(c.Name)) continue;
                if (job != "" && c.Job != job) continue;
                eligible.Add(c);
            }
            if (eligible.Count == 0) return null;
            return eligible[r.RandiRange(0, eligible.Count - 1)];
        }

        /// <summary>COMMIT A SHELF IDEA: materialize a real bet through the
        /// roadmap's own door. Returns "committed", "queued" or "".</summary>
        public static string CommitShelf(GameState state, string shelfId)
        {
            ShelfCandidate cand = null;
            foreach (ShelfCandidate c in ShelfCandidates(state))
                if (c.Id == shelfId) { cand = c; break; }
            if (cand == null) return "";
            foreach (Bet b in state.Bets)
                if (b.Name == cand.Name && !b.Shipped)
                    return "";   // already on the board
            int n = 1;
            string prefix = string.Format(CultureInfo.InvariantCulture,
                "featbet_w{0}_", state.Week);
            foreach (Bet b2 in state.Bets)
                if ((b2.Id ?? "").StartsWith(prefix, StringComparison.Ordinal)) n++;
            var bet = new Bet
            {
                Id = prefix + n.ToString(CultureInfo.InvariantCulture),
                Name = cand.Name,
                Desc = string.Format(CultureInfo.InvariantCulture,
                    "from the shelf — {0}", cand.JobWords),
                Kind = cand.Kind,
                Ambition = cand.Ambition,
                CostRndWeeks = cand.CostRndWeeks,
                Progress = 0.0, Committed = false, CommittedWeek = 0,
                Ready = false, Shipped = false, ShippedWeek = 0,
                Band = "", Era = state.Era,
            };
            state.Bets.Add(bet);
            if (SimRoadmap.CommittedBets(state).Count < SimRoadmap.WipCap(state))
            {
                SimRoadmap.CommitBet(state, bet.Id);
                return "committed";
            }
            EnqueueBet(state, bet.Id);
            return "queued";
        }

        // ═════════════════════ THE NEXT QUEUE ════════════════════════════════
        // Storage: a queued bet's CommittedWeek is NEGATIVE, −position. The
        // save key is unchanged in both engines; the roadmap only reads the
        // field on READY bets, which a queued bet is not. An era change drops
        // uncommitted candidates and their seats — candidates are paper.

        /// <summary>The queue, in order.</summary>
        public static List<Bet> QueuedBets(GameState state)
        {
            var outp = new List<Bet>();
            foreach (Bet bd in state.Bets)
                if (bd.CommittedWeek < 0 && !bd.Committed && !bd.Ready && !bd.Shipped)
                    outp.Add(bd);
            outp.Sort((a, b2) => (-a.CommittedWeek).CompareTo(-b2.CommittedWeek));
            return outp;
        }

        /// <summary>Choose a board candidate for NEXT. Refuses work in flight.</summary>
        public static bool EnqueueBet(GameState state, string id)
        {
            Bet bet = SimRoadmap.BetById(state, id);
            if (bet == null || bet.Committed || bet.Ready || bet.Shipped
                || bet.CommittedWeek < 0)
                return false;
            int deepest = 0;
            foreach (Bet q in QueuedBets(state))
                deepest = Math.Max(deepest, -q.CommittedWeek);
            bet.CommittedWeek = -(deepest + 1);
            state.LogAction(string.Format(CultureInfo.InvariantCulture,
                "what we make: queued '{0}' for NEXT", bet.Name));
            return true;
        }

        /// <summary>Back to the shelf.</summary>
        public static bool DequeueBet(GameState state, string id)
        {
            Bet bet = SimRoadmap.BetById(state, id);
            if (bet == null || bet.CommittedWeek >= 0) return false;
            bet.CommittedWeek = 0;
            return true;
        }

        /// <summary>Reorder: dir −1 = sooner, +1 = later.</summary>
        public static bool QueueMove(GameState state, string id, int dir)
        {
            List<Bet> q = QueuedBets(state);
            for (int i = 0; i < q.Count; i++)
            {
                if (q[i].Id != id) continue;
                int j = i + (dir > 0 ? 1 : -1);
                if (j < 0 || j >= q.Count) return false;
                int tmp = q[i].CommittedWeek;
                q[i].CommittedWeek = q[j].CommittedWeek;
                q[j].CommittedWeek = tmp;
                return true;
            }
            return false;
        }

        /// <summary>The queue takes any freed slot, in order, through the
        /// roadmap's own commit (its WIP arithmetic is the law).</summary>
        static void RunQueue(GameState state, WeeklyReport rep)
        {
            foreach (Bet bd in QueuedBets(state))
            {
                if (SimRoadmap.CommittedBets(state).Count >= SimRoadmap.WipCap(state))
                    break;
                int seat = bd.CommittedWeek;
                bd.CommittedWeek = 0;
                if (SimRoadmap.CommitBet(state, bd.Id))
                {
                    if (rep != null)
                        rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                            "the queue moves: the team takes up '{0}'", bd.Name));
                }
                else
                {
                    bd.CommittedWeek = seat;
                    break;
                }
            }
        }
    }
}
