using System;
using System.Collections.Generic;
using System.Globalization;

namespace Runway.Core
{
    /// <summary>
    /// LANE 01 — THE CATALOG (offers, prices, itemized costs). Spec: docs/design/01-catalog.md
    ///
    /// THE LAW, unchanged: the ENGINE owns every number (every line, every total,
    /// every clamp); the DM owns sentences; the LLM proposes terms that are ALWAYS
    /// shown for adjustment and only enter the books through this lane's clamped
    /// door after the founder confirms.
    ///
    /// NORTH STAR: this subsystem teaches unit economics by their real names —
    /// COGS, fixed vs variable cost, contribution margin, break-even — with
    /// receipts that say WHY a number moved.
    ///
    /// WHAT LIVES WHERE. SimEngine already owns the arithmetic half: the cost-line
    /// sync (F1), the catalog overhead sum (F2), COGS per customer (F3), the
    /// learning curve (F4), demand/pain/arpu (F5), and the scalar clamps inside
    /// AddOffer. THIS FILE is the lane's own half, and it is exactly three things:
    ///
    ///   1. THE DOOR — AddOffer here is the only entry the desks and the DM should
    ///      use, because the ERA SHELF CAP and the SUM-OF-WEIGHT BUDGET live at the
    ///      door, not in the engine's scalar clamps (D1: the arpu exploit is closed
    ///      structurally, by an engine clamp, never by UI politeness).
    ///   2. PROPOSAL TIME — the keyless draft (F7 v2), era-scaled tooling and a
    ///      seeded jitter on salt 11, so two keyless runs never sell the identical
    ///      workshop at the identical price and a replay still lands on the same one.
    ///   3. THE PEDAGOGY — contribution margin, break-even, and the one lesson a
    ///      founder must not miss (a price under its own variable cost), computed
    ///      once here so the desk, the receipts and the attention row can never
    ///      disagree about the number or the words.
    ///
    /// The spine calls, in tick order (00-spine section 1, HOOKS.md):
    ///   TickPre    tick 8 — the shelf invariant, before the market reads weights
    ///   TickMoney  the money section — the catalog's receipts, in their real names
    ///   TickPost   after the week's record is written and can be read back
    /// and outside the tick: Directives feeds the DM block, Attention feeds every
    /// bang in the game through SimEngine.AttentionItems.
    ///
    /// TWIN LAW: this file and game/src/core/lanes/sim_catalog.gd carry the same
    /// logic in the same order. The engines do NOT share PRNG internals, so parity
    /// means same checks and same behaviour, never a byte-equal draw.
    /// </summary>
    public static class SimCatalog
    {
        // ── the constants ──────────────────────────────────────────────────────

        /// THE SHELF (DECISIONS.md, standing recommendation): how many offers a
        /// company of this stage can actually keep on the shelf, and how much of
        /// one customer's finite weekly wallet the whole catalog may claim.
        public static readonly Dictionary<string, int> EraOfferCap =
            new Dictionary<string, int>
            {
                { "garage", 2 }, { "coworking", 3 }, { "office", 5 },
                { "floor", 8 }, { "hq", 8 },
            };

        /// THE TOOLING A STAGE IS QUOTED (D2): era pressure enters at PROPOSAL time
        /// only. A floor-era founder is QUOTED heavier tooling; the quoted number
        /// then stays the number until they step it by hand. A receipt that
        /// silently grows with promotion would be a hidden multiplier, which is the
        /// one sin this whole subsystem exists to refuse.
        public static readonly Dictionary<string, double> EraToolScale =
            new Dictionary<string, double>
            {
                { "garage", 1.0 }, { "coworking", 1.4 }, { "office", 2.2 },
                { "floor", 4.0 }, { "hq", 7.0 },
            };

        /// Sum of every offer's weight, whole catalog. A customer's weekly budget
        /// is finite, so a spammed catalog cannot mint arpu (D1, share-of-wallet).
        public const double ShelfWeightCap = 6.0;
        public const double MinWeight = 0.2;
        public const double MaxWeight = 3.0;

        /// 1-4 variable lines, 0-3 fixed lines: past that the fine print stops
        /// being fine print and the DETAIL card stops fitting the sheet.
        public const int MaxCostLines = 4;
        public const int MaxFixedLines = 3;
        public const int MaxLabel = 24;

        // ── the shelf ──────────────────────────────────────────────────────────

        /// <summary>How many offers this stage holds. Era demotion never deletes an
        /// offer — the cap gates the door, and a company that fell back to the
        /// garage keeps the five things it was selling.</summary>
        public static int OfferCap(GameState state)
        {
            int cap;
            return EraOfferCap.TryGetValue(state.Era ?? "", out cap) ? cap : 2;
        }

        /// <summary>Sum of weight across the catalog — the slice of a customer's
        /// wallet already spoken for.</summary>
        public static double ShelfWeight(GameState state)
        {
            if (state.Offers == null) return 0.0;
            double total = 0.0;
            foreach (Offer o in state.Offers) total += o.Weight;
            return total;
        }

        /// <summary>What is left of the wallet, never below zero.</summary>
        public static double WeightRoom(GameState state)
        {
            return Gd.Maxf(ShelfWeightCap - ShelfWeight(state), 0.0);
        }

        /// <summary>True when nothing more can be shelved — either the stage is out
        /// of slots or there is no wallet left for even a minimum-weight offer.</summary>
        public static bool ShelfFull(GameState state)
        {
            int count = state.Offers == null ? 0 : state.Offers.Count;
            return count >= OfferCap(state) || WeightRoom(state) < MinWeight;
        }

        /// <summary>Why the door is shut, in the desk's own voice. Empty while it
        /// is open.</summary>
        public static string ShelfFullLine(GameState state)
        {
            int count = state.Offers == null ? 0 : state.Offers.Count;
            if (count >= OfferCap(state))
                return "the shelf is full at this stage — drop something first";
            if (WeightRoom(state) < MinWeight)
                return "the catalog already claims a whole customer's wallet — drop something first";
            return "";
        }

        // ── the door into the books (F6) ───────────────────────────────────────

        /// <summary>
        /// THE ONLY DOOR. Every path that puts an offer on the shelf comes through
        /// here: the review card, a DM price_offer naming something that does not
        /// exist, a future op. Order of operations is F6's, and it is deliberate:
        ///   1. REFUSE first, so a full shelf costs nothing and says so;
        ///   2. narrow the weight to what the wallet has left BEFORE the engine's
        ///      own [0.2, 3.0] clamp sees it — the lane door narrows, the engine
        ///      floor holds;
        ///   3. sanitize the lines (count, label, numeric) so SyncOfferCosts is
        ///      handed receipts it can believe;
        ///   4. hand the whole thing to SimEngine.AddOffer, which owns the scalar
        ///      clamps and the sync. Nothing here re-implements a clamp the engine
        ///      already has.
        /// Returns the new offer, or null when the shelf refused it.
        /// </summary>
        public static Offer AddOffer(GameState state, string name, string unit,
                                     double fair, double cost, double elasticity,
                                     double weight,
                                     List<CostLine> costLines = null,
                                     List<CostLine> fixedLines = null)
        {
            int count = state.Offers == null ? 0 : state.Offers.Count;
            if (count >= OfferCap(state)) return null;
            double room = WeightRoom(state);
            if (room < MinWeight) return null;
            double w = Gd.Clampf(weight, MinWeight, Gd.Minf(MaxWeight, room));
            return SimEngine.AddOffer(state, name, unit, fair, cost, elasticity, w,
                SanitizeLines(costLines, MaxCostLines),
                SanitizeLines(fixedLines, MaxFixedLines));
        }

        /// <summary>Dropping is instant behind the desk's two-tap arm
        /// (DECISIONS.md): the revenue consequence is the natural cost, and a
        /// wind-down would only teach the founder to fear the shelf.</summary>
        public static bool RemoveOffer(GameState state, int idx)
        {
            return SimEngine.RemoveOffer(state, idx);
        }

        /// <summary>
        /// Receipts the engine can believe: at most `cap` of them (extras drop from
        /// the tail), a stripped label of 24 characters that is never blank, and a
        /// number where a number belongs. The AMOUNTS are not clamped here —
        /// SyncOfferCosts owns that, against the fair price, and owning it twice is
        /// how two clamps start disagreeing.
        /// </summary>
        public static List<CostLine> SanitizeLines(List<CostLine> lines, int cap)
        {
            var outp = new List<CostLine>();
            if (lines == null) return outp;
            foreach (CostLine l in lines)
            {
                if (outp.Count >= cap) break;
                if (l == null) continue;
                string label = (l.Label ?? "").Trim();
                if (label.Length > MaxLabel) label = label.Substring(0, MaxLabel);
                if (label.Length == 0) label = "line";
                double amount = double.IsNaN(l.Amount) || double.IsInfinity(l.Amount)
                    ? 0.0 : l.Amount;
                outp.Add(new CostLine { Label = label, Amount = amount });
            }
            return outp;
        }

        // ── proposal time: the draft (F7) ──────────────────────────────────────

        /// <summary>What this audience pays, relative to an SMB. Consumer pays a
        /// quarter, Enterprise four times — and the costs scale with the price, so
        /// margin holds.</summary>
        public static double AudienceScale(string who)
        {
            if (who == "Consumer") return 0.25;
            if (who == "Enterprise") return 4.0;
            return 1.0;
        }

        public static double ToolScale(string era)
        {
            double s;
            return EraToolScale.TryGetValue(era ?? "", out s) ? s : 1.0;
        }

        /// <summary>
        /// THE KEYLESS DRAFT v2 (F7). Cost-plus estimation at roughly a 65% gross
        /// margin, era-scaled tooling, and one seeded jitter so the house numbers
        /// are not a fixed price list. Returns the same shape the model returns —
        /// the itemized cost sheet — so the review card has exactly one road to read.
        ///
        /// THE ONE DRAW COMES FIRST and it is the only draw: same (seed, week)
        /// gives the same draft, so a replay shelves the identical offer. Salt 11
        /// is this lane's, and this is its only draw-site (00-spine section 3).
        /// </summary>
        public static Offer DraftTerms(GameState state, string idea)
        {
            double jitter = SimEngine.RngForSalt(state, SimEngine.SALT_CATALOG_JITTER)
                                     .RandfRange(0.8, 1.3);
            // the engine's own draft supplies the name and sniffs the billing unit
            // out of the founder's words; this lane re-prices it and itemizes the
            // cost sheet.
            Offer terms = SimEngine.DraftOfferTerms(state, idea);
            double aud = AudienceScale(state.BizWho);
            double fair = Gd.Maxf(Gd.Round(40.0 * aud * jitter), 1.0);
            double materials = Gd.Round(fair * 0.20);
            double labor = Gd.Round(fair * 0.15);
            terms.FairPrice = fair;
            terms.UnitCost = materials + labor;
            terms.Elasticity = 2.0;
            terms.Weight = 1.0;
            // GENERIC ON PURPOSE (L2): a keyed run answers with this business's own
            // vocabulary ("cold-chain packaging", "a barista's hour"), so the house
            // labels must read visibly plainer than the street's.
            terms.CostLines = new List<CostLine>
            {
                new CostLine { Label = "materials & delivery", Amount = materials },
                new CostLine { Label = "labor share", Amount = labor },
            };
            terms.FixedLines = new List<CostLine>
            {
                new CostLine
                {
                    Label = "tools & subscriptions",
                    Amount = Gd.Round(15.0 * aud * ToolScale(state.Era)),
                },
            };
            terms.FixedWk = terms.FixedLines[0].Amount;
            return terms;
        }

        // ── the pedagogy, computed once ────────────────────────────────────────

        /// <summary>What one sale costs to serve TODAY — the founder's stepped line
        /// amounts, times the one learning factor, applied at the total and never
        /// per line (F4: the stepped numbers are receipts and must stay exactly what
        /// the founder set).</summary>
        public static double ServedUnitCost(Offer offer, double lc)
        {
            return offer == null ? 0.0 : offer.UnitCost * lc;
        }

        /// <summary>
        /// CONTRIBUTION MARGIN — price minus variable cost, per unit. The number
        /// that decides whether volume is a business or a hobby.
        ///
        /// `fairMult` is THE STREET'S price, not yours (03 section 5.1): while a
        /// rival's price war runs, an UNPRICED offer follows the going rate down,
        /// so its margin is genuinely thinner that week. A named price ignores it —
        /// the founder's number is the founder's number.
        /// </summary>
        public static double Contribution(Offer offer, double lc, double fairMult = 1.0)
        {
            if (offer == null) return 0.0;
            return SimEngine.OfferBilledPrice(offer, fairMult) - ServedUnitCost(offer, lc);
        }

        /// <summary>BREAK-EVEN — how many sales a week pay for this offer's standing
        /// tools. −1 when the price never pays for itself, because there is no such
        /// number and printing a big one instead of the lesson would be the kinder
        /// lie.</summary>
        public static int BreakEven(Offer offer, double lc, double fairMult = 1.0)
        {
            double margin = Contribution(offer, lc, fairMult);
            if (margin <= 0.0) return -1;
            return (int)Math.Ceiling(offer.FixedWk / margin);
        }

        /// <summary>THE ONE MISTAKE A FOUNDER MUST NOT MISS: every sale loses money.
        /// A conscious $0 giveaway is NOT this — it is a strategy the founder chose,
        /// priced at zero on purpose, and the desk says so in blue. This is a named
        /// price that sits under its own variable cost.</summary>
        public static bool NeverPays(Offer offer, double lc)
        {
            if (offer == null || offer.Price <= 0.0) return false;
            return offer.Price <= ServedUnitCost(offer, lc);
        }

        // ── the tick ───────────────────────────────────────────────────────────

        /// <summary>
        /// Tick 8, before adoption: THE SHELF INVARIANT.
        ///
        /// AddOffer guards the door, but state can arrive by other roads — a world
        /// bible, a hand-edited save, a legacy run from before the cap existed. The
        /// sum of weight is what turns customers into revenue (D1: weight stays
        /// ABSOLUTE in arpu), so if it is ever allowed past 6.0 the catalog mints
        /// money. It is trimmed here, every week, in both engines — and it RECEIPTS
        /// when it bites, because a clamp that moves a number in silence is exactly
        /// the hidden multiplier this subsystem refuses to contain.
        /// </summary>
        public static void TickPre(GameState state, WeeklyReport rep)
        {
            if (state.Offers == null || state.Offers.Count == 0) return;
            double total = ShelfWeight(state);
            if (total <= ShelfWeightCap + 0.001) return;
            double k = ShelfWeightCap / total;
            foreach (Offer o in state.Offers)
                o.Weight = Gd.Clampf(o.Weight * k, MinWeight, MaxWeight);
            rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                "the shelf only holds so much: catalog weights trimmed to Σ{0:0.0} — one customer's weekly wallet is finite",
                ShelfWeight(state)));
        }

        /// <summary>
        /// The money section. The catalog's P&amp;L lanes (Cogs, OfferFixed) are
        /// already assembled by the engine above this call — this lane does not
        /// write a second number over them. What it DOES own is the RECEIPTS: the
        /// engine's working lines name the money, and the lane names the CONCEPT,
        /// which is the whole pedagogy contract (section 6: COGS, fixed vs
        /// variable, billed sold or not).
        ///
        /// The upgrade happens in place, matched by the engine's own prefix, so
        /// nothing ever prints twice. If the engine's wording ever changes, the
        /// match simply misses and its line stands as written — a lane may sharpen
        /// the spine's voice, never shout over it.
        /// </summary>
        public static void TickMoney(GameState state, WeeklyReport rep, MoneyWork m)
        {
            int cogs = Gd.RoundToInt(m.Cogs);
            if (cogs >= 1)
            {
                // the same learning factor the week's record carries, so the
                // journal and the ledger can never disagree about what the curve did
                double lc = SimEngine.LearningCurve(state);
                string learned = lc < 0.995
                    ? string.Format(CultureInfo.InvariantCulture, ", learning ×{0:0.00}", lc)
                    : "";
                Reword(rep.Lines, "cost of serving customers: $",
                    string.Format(CultureInfo.InvariantCulture,
                        "COGS ${0} — serving {1} customers (variable cost × volume{2})",
                        cogs, state.Traction, learned));
            }
            int fixedWk = Gd.RoundToInt(m.OfferFixed);
            if (fixedWk >= 1)
            {
                Reword(rep.Lines, "catalog overheads: $",
                    string.Format(CultureInfo.InvariantCulture,
                        "fixed costs — the catalog's standing tools: ${0}/wk (billed sold or not)",
                        fixedWk));
            }
        }

        /// Replace the last line carrying `prefix`. Silent when there is none.
        static void Reword(List<string> lines, string prefix, string replacement)
        {
            if (lines == null) return;
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                if (lines[i] != null && lines[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    lines[i] = replacement;
                    return;
                }
            }
        }

        /// <summary>The catalog's books close inside the money section — nothing
        /// here needs the finished week, and a hook that does nothing costs the
        /// tick nothing.</summary>
        public static void TickPost(GameState state, WeeklyReport rep)
        {
        }

        /// <summary>
        /// DM context lines, section 5 of the DIRECTIVES block (00-spine section 5).
        ///
        /// The composer already prints WHAT is on sale and at what price, one line
        /// per offer. What it cannot say — and what makes the difference between a
        /// narrator inventing costs and one reading them — is what a sale COSTS and
        /// what the shelf carries whether or not anything sells. Two lines,
        /// aggregate on purpose: the per-offer serve cost belongs on the composer's
        /// own on-sale line, and printing it again here would be the same fact
        /// twice in a 24-line budget.
        /// </summary>
        public static List<string> Directives(GameState state)
        {
            var outp = new List<string>();
            if (state.Offers == null || state.Offers.Count == 0) return outp;
            double serve = SimEngine.OffersCogsPerCustomer(state);
            if (serve >= 1.0)
                outp.Add(string.Format(CultureInfo.InvariantCulture,
                    "- Serving one customer costs ~${0}/wk (COGS — variable cost, it bills only when they buy).",
                    Gd.RoundToInt(serve)));
            double fixedWk = SimEngine.OffersFixedWk(state);
            if (fixedWk >= 1.0)
                outp.Add(string.Format(CultureInfo.InvariantCulture,
                    "- The catalog carries ${0}/wk of standing tool costs, sold or not.",
                    Gd.RoundToInt(fixedWk)));
            return outp;
        }

        /// <summary>
        /// Attention rows — the pricing desk (00-spine section 4).
        ///
        /// The `unpriced` row of the registry is filed by the spine itself and is
        /// NOT repeated here: one condition, one row, or the ticker starts
        /// stuttering.
        ///
        /// What this lane adds is the row the spec calls the one lesson a founder
        /// must not miss (section 6): a NAMED price sitting under its own variable
        /// cost, which loses money on every single sale and does it more the better
        /// the marketing works. It is a warn, so it reaches the pre-roll review and
        /// stops the dice — losing money per unit is worth one more look before a
        /// week is spent scaling it.
        /// </summary>
        public static List<AttentionItem> Attention(GameState state)
        {
            var rows = new List<AttentionItem>();
            if (state.Offers == null || state.Offers.Count == 0) return rows;
            double lc = SimEngine.LearningCurve(state);
            foreach (Offer o in state.Offers)
            {
                if (!NeverPays(o, lc)) continue;
                rows.Add(new AttentionItem
                {
                    Desk = "pricing",
                    Key = "losing_price",
                    Severity = 2,
                    Label = "a price below its variable cost",
                    Control = "losing_price",
                });
                break;
            }
            return rows;
        }
    }
}
