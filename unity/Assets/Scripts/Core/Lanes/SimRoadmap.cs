using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Runway.Core
{
    /// <summary>
    /// LANE 07 — THE ROADMAP (bets, the ship roll, tech debt). Spec: docs/design/07-roadmap.md
    ///
    /// WHAT THIS DESK TEACHES, by name and with receipts: CAPACITY (one team, so
    /// many R&amp;D-weeks a week), OPPORTUNITY COST (money spent on a bet ships no
    /// base quality), TECH-DEBT INTEREST (every point over 40 taxes throughput),
    /// LAUNCH RISK (the house dice decide a launch, and preparation moves the odds).
    ///
    /// THE ONE LAW: the engine owns every number. A model may dress a card with a
    /// name and a rung of an authored ladder; cost, DC, payoff and duration are
    /// tables in this file and nowhere else.
    ///
    /// THE SPINE CALLS, in tick order (00-spine section 1, HOOKS.md):
    ///   TickPre    7 — stalled READY bets roll themselves out; the hq maintenance
    ///                  tax bills. Ships land BEFORE section 8 reads product.
    ///   TickMoney  9 — the R&amp;D block's own section: while a bet is committed the
    ///                  rnd budget buys WEEKS, not polish (the base drip is
    ///                  reversed here — see RouteRnd), the capacity pool splits
    ///                  across committed bets, and a finished bet goes READY.
    ///   TickPost     — the board refreshes its slots (salt 71).
    ///
    /// SHIP IS A BUTTON (DECISIONS.md #2): a READY bet waits for the founder's
    /// press at the product desk, where the dice roll AT the press behind the
    /// pre-roll review. Three weeks unpressed and it slips out on its own.
    ///
    /// TWIN LAW: this file and game/src/core/lanes/sim_roadmap.gd carry the same
    /// logic in the same order. The engines do NOT share PRNG internals, so
    /// parity means same checks and same behaviour, never a byte-equal draw.
    /// </summary>
    public static class SimRoadmap
    {
        // ───────────────────────── the era ladders (consts) ──────────────────────
        /// <summary>Candidate cards on the board, hardening excluded (it is standing
        /// law). THE SPINE'S ERA LADDER WINS over the spec's own section 2 table
        /// (00-spine section 9: "1 bet slot, 2 slots, 3 + hardening, carry, carry").</summary>
        public static readonly Dictionary<string, int> BET_SLOTS = new Dictionary<string, int>
        {
            { "garage", 1 }, { "coworking", 2 }, { "office", 3 }, { "floor", 3 }, { "hq", 3 },
        };

        /// <summary>How many bets the team may build AT ONCE. The pool splits evenly
        /// across them, so two parallel bets finish later than the same two in
        /// series: the WIP lesson is arithmetic, never a scripted scolding.</summary>
        public static readonly Dictionary<string, int> BET_WIP = new Dictionary<string, int>
        {
            { "garage", 1 }, { "coworking", 1 }, { "office", 2 }, { "floor", 2 }, { "hq", 3 },
        };

        public static readonly Dictionary<string, int> ERA_AMBITION_CAP = new Dictionary<string, int>
        {
            { "garage", 2 }, { "coworking", 3 }, { "office", 3 }, { "floor", 3 }, { "hq", 3 },
        };

        /// <summary>The price and the odds, per rung of ambition — authored, never inferred.</summary>
        public static readonly double[] COST_BY_AMBITION = { 3.0, 5.0, 8.0 };
        public static readonly int[] DC_BY_AMBITION = { 8, 11, 14 };
        public const double HARDENING_COST = 2.5;
        public const double PLATFORM_COST = 10.0;
        public const int PLATFORM_DC = 12;

        /// <summary>BET_PAYOFF[ambition-1][band] — band index: brilliant 0, fine 1,
        /// risky 2, backfired 3. Integers only, so a Godot round() and a C#
        /// banker's round can never disagree about what a launch earned.</summary>
        public static readonly int[][] BET_PAYOFF =
        {
            new[] { 6, 4, 2, 0 }, new[] { 11, 7, 4, 0 }, new[] { 15, 10, 5, 0 },
        };

        public static readonly string[] BANDS = { "brilliant", "fine", "risky", "backfired" };
        public static readonly string[] KINDS = { "quality", "retention", "reach", "debt", "platform" };

        public const double RND_PER_WEEK = 1200.0;   // one loaded engineer-week of money
        public const double ENG_PER_SKILL = 0.25;    // skill 1-5 → 0.25-1.25 wk/wk
        public const double FOUNDER_HANDS_ON = 0.25; // garage + coworking: the founder IS capacity
        public const double PLATFORM_MULT = 0.15;    // x1.15 compounding per shipped level
        public const int PLATFORM_MAX = 4;
        public const double DEBT_FREE = 40.0;        // debt below this is free
        public const double DEBT_SPAN = 120.0;
        public const double DEBT_FLOOR = 0.5;        // -50% velocity at debt 100
        public const int STALL_WEEKS = 3;            // READY and unpressed this long → it slips out
        public const double ABANDON_DECAY = 0.25;    // standing down costs a quarter of the build
        public const int MAINTENANCE_WINDOW = 10;    // hq: weeks of neglect before entropy bills
        public const double MAINTENANCE_DEBT = 0.8;
        public const int QA_NET_ERA = 2;             // office and up: staging truncates the tail
        public const int QA_NET_MARGIN = -4;         // a miss by 3-4 softens to risky, never better
        public const int SHIPPED_KEPT = 8;
        public const int SHIP_DRAWS = 3;
        /// <summary>One tick's worth of scratch: product as it stood before the
        /// engine's R&amp;D block ran (-1 = nothing to reverse). Meta, never saved.</summary>
        public const string PRODUCT_PRE = "roadmap_product_pre";

        public const string HARDENING_ID = "hardening";
        public const string HARDENING_NAME = "Hardening sprint";
        public const string HARDENING_DESC = "No features. Pay the debt down before the debt collects you.";

        /// <summary>One thing the team could chase, before the engine prices it.</summary>
        public sealed class PoolCard
        {
            public string Name;
            public string Desc;
            public string Kind;
            public int Ambition;
        }

        /// <summary>THE KEYLESS POOL — the COMPLETE path, not a fallback. A run with
        /// no model draws these; a run with one gets the same cards wearing this
        /// business's own words (DressBets).</summary>
        public static readonly PoolCard[] BET_POOL =
        {
            new PoolCard { Name = "Onboarding, but humane", Desc = "New users stop rage-quitting the first screen. Mostly.", Kind = "quality", Ambition = 1 },
            new PoolCard { Name = "Annual plans", Desc = "Twelve months upfront, a discount, and a calmer churn chart.", Kind = "retention", Ambition = 1 },
            new PoolCard { Name = "The Referral Loop", Desc = "Users invite users. A button, a bribe, a dream of virality.", Kind = "reach", Ambition = 1 },
            new PoolCard { Name = "Offline mode", Desc = "Works on a plane, in a tunnel, at your uncle's farm. Sync is the hard part.", Kind = "quality", Ambition = 2 },
            new PoolCard { Name = "The Big Integration", Desc = "Plug into the tool your customers already live in. Their IT has questions.", Kind = "reach", Ambition = 2 },
            new PoolCard { Name = "Alerts that matter", Desc = "Fewer notifications, better ones. Customers stop muting you.", Kind = "retention", Ambition = 2 },
            new PoolCard { Name = "The Redesign", Desc = "Everything moves. Half the users hate it loudly, then miss it later.", Kind = "quality", Ambition = 3 },
            new PoolCard { Name = "Mobile, finally", Desc = "The whole thing, on a phone, without weeping. The board keeps asking.", Kind = "reach", Ambition = 3 },
            new PoolCard { Name = "The API platform", Desc = "Everything becomes a building block. Slow now, faster forever.", Kind = "platform", Ambition = 3 },
            new PoolCard { Name = "One-click deploys", Desc = "Shipping stops being a ceremony. The team ships twice as often.", Kind = "platform", Ambition = 3 },
        };

        /// <summary>The band phrase the DM must narrate a launch with — engine-owned
        /// words for an engine-owned outcome.</summary>
        public static readonly Dictionary<string, string> BAND_PHRASE = new Dictionary<string, string>
        {
            { "brilliant", "and the launch sang" },
            { "fine", "and it landed fine" },
            { "risky", "hot, with smoke coming out" },
            { "backfired", "and it faceplanted" },
        };

        /// <summary>THE DRESSING TRIGGER: how many cards the last refresh drew. The
        /// engine never calls a model — it reports that fresh paper exists and the
        /// screen that owns the client decides.</summary>
        public static int LastRefreshed;

        /// <summary>The receipt a launch hands back: the die, the DC, the band, the WHY.</summary>
        public sealed class ShipResult
        {
            public string Band = "";
            public int D20;
            public int Mod;
            public int Dc;
            public int Total;
            public int Units;
            public bool QaNet;
            public string Event = "";
            public List<string> Lines = new List<string>();
        }

        // ═══════════════════════════ THE TICK HOOKS ══════════════════════════════

        /// <summary>Tick 7 — before adoption reads product. A READY bet nobody pressed
        /// for three weeks rolls itself out, and (hq) the maintenance tax bills a
        /// portfolio that has shipped no upkeep in ten weeks.</summary>
        public static void TickPre(GameState state, WeeklyReport rep)
        {
            ShipStalled(state, rep);
            MaintenanceTax(state, rep);
            // the snapshot the R&D branch is measured against (see RouteRnd): after
            // this line the engine's own R&D block is the ONLY writer of Product,
            // which is what makes the reversal exact instead of a re-derivation.
            state.SetMeta(PRODUCT_PRE, state.Product);
        }

        /// <summary>
        /// Tick 9, the R&amp;D block's own section — the spine's base drip has just run.
        ///
        /// THE ROUTING (the spec's DECIDE #1): output SPLITS, it never doubles. While
        /// a bet is committed the rnd money buys R&amp;D-weeks, so the +1-quality-per
        /// -$1,200 drip the engine just applied is reversed here, receipt and all.
        /// Uncommitted, nothing happens and the legacy path stands verbatim. Debt
        /// paydown belongs to the engine's block in BOTH branches.
        /// </summary>
        public static void TickMoney(GameState state, WeeklyReport rep, MoneyWork m)
        {
            List<Bet> live = CommittedBets(state);
            if (live.Count == 0)
            {
                state.SetMeta(PRODUCT_PRE, -1);
                return;
            }
            RouteRnd(state, rep);
            double share = CapacityPool(state) / live.Count;
            foreach (Bet bet in live)
            {
                bet.Progress += share;
                double cost = Gd.Maxf(bet.CostRndWeeks, 0.001);
                if (rep != null)
                {
                    rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "roadmap: '{0}' — {1}% built", bet.Name,
                        Gd.ToInt(Gd.Minf(bet.Progress / cost, 1.0) * 100.0)));
                }
                if (bet.Progress >= bet.CostRndWeeks)
                {
                    bet.Progress = bet.CostRndWeeks;
                    bet.Ready = true;
                    bet.Committed = false;
                    // CommittedWeek doubles as THE STALL CLOCK: the week the team last
                    // touched this bet. It answers "how long has this been sitting
                    // built and unshipped" without a new saved key.
                    bet.CommittedWeek = state.Week;
                    if (rep != null)
                    {
                        rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                            "READY TO SHIP: '{0}' — the dice are yours at the product desk", bet.Name));
                    }
                }
            }
            // NO P&L LANE OF ITS OWN: `m.Rnd` is already on the record and the money
            // still leaves the bank. THAT is the opportunity cost — the same dollars,
            // a different output.
        }

        /// <summary>After the week's record is written: the board refreshes its slots
        /// (salt 71). A bet that shipped this week has already freed its slot, so the
        /// refill lands in the same tick the launch did.</summary>
        public static void TickPost(GameState state, WeeklyReport rep)
        {
            RefreshBets(state, rep);
        }

        /// <summary>DM context lines, section 11 of the DIRECTIVES block. The launch
        /// becomes story through these — no new call, ever.</summary>
        public static List<string> Directives(GameState state)
        {
            var outp = new List<string>();
            List<Bet> ready = ReadyBets(state);
            if (ready.Count > 0)
            {
                outp.Add(string.Format(CultureInfo.InvariantCulture,
                    "- Bet ready to ship: '{0}' (R&D done; shipping rolls the house dice).",
                    ready[0].Name));
            }
            foreach (Bet bet in state.Bets)
            {
                if (!bet.Shipped || bet.ShippedWeek < state.Week - 1) { continue; }
                string phrase;
                if (!BAND_PHRASE.TryGetValue(bet.Band ?? "", out phrase)) { phrase = "and it landed fine"; }
                outp.Add(string.Format(CultureInfo.InvariantCulture,
                    "- SHIPPED: '{0}' went out {1}. The week's story must feel the launch.",
                    bet.Name, phrase));
            }
            return outp;
        }

        /// <summary>Attention rows — the product desk. Labels are 40 characters or
        /// less because the garage ticker prints them verbatim, and they name the
        /// business term, not the state.</summary>
        public static List<AttentionItem> Attention(GameState state)
        {
            var rows = new List<AttentionItem>();
            if (AnyBetReady(state))
            {
                rows.Add(new AttentionItem
                {
                    Desk = "product", Key = "bet_ready", Severity = 2,
                    Control = "ship", Label = "a bet is built — ship it",
                });
            }
            if (state.TechDebt >= 70.0)
            {
                rows.Add(new AttentionItem
                {
                    Desk = "product", Key = "debt_critical", Severity = 2,
                    Control = "rebuild",
                    Label = string.Format(CultureInfo.InvariantCulture,
                        "tech debt {0} — everything builds slow", Gd.ToInt(state.TechDebt)),
                });
            }
            return rows;
        }

        // ═══════════════════════════ THE BOARD (reads) ═══════════════════════════

        public static List<Bet> Unshipped(GameState state)
        {
            var outp = new List<Bet>();
            foreach (Bet b in state.Bets) { if (!b.Shipped) { outp.Add(b); } }
            return outp;
        }

        /// <summary>The candidate cards, hardening excluded (it renders under its own rule).</summary>
        public static List<Bet> BoardBets(GameState state)
        {
            var outp = new List<Bet>();
            foreach (Bet b in Unshipped(state)) { if (b.Id != HARDENING_ID) { outp.Add(b); } }
            return outp;
        }

        public static Bet HardeningBet(GameState state)
        {
            foreach (Bet b in Unshipped(state)) { if (b.Id == HARDENING_ID) { return b; } }
            return null;
        }

        public static Bet BetById(GameState state, string id)
        {
            foreach (Bet b in state.Bets) { if (b.Id == id) { return b; } }
            return null;
        }

        public static List<Bet> CommittedBets(GameState state)
        {
            var outp = new List<Bet>();
            foreach (Bet b in Unshipped(state)) { if (b.Committed) { outp.Add(b); } }
            return outp;
        }

        public static List<Bet> ReadyBets(GameState state)
        {
            var outp = new List<Bet>();
            foreach (Bet b in Unshipped(state)) { if (b.Ready) { outp.Add(b); } }
            return outp;
        }

        public static bool AnyBetReady(GameState state)
        {
            return ReadyBets(state).Count > 0;
        }

        public static List<Bet> ShippedBets(GameState state)
        {
            var outp = new List<Bet>();
            foreach (Bet b in state.Bets) { if (b.Shipped) { outp.Add(b); } }
            return outp;
        }

        public static int WipCap(GameState state)
        {
            int v;
            return BET_WIP.TryGetValue(state.Era ?? "", out v) ? v : 1;
        }

        public static int Slots(GameState state)
        {
            int v;
            return BET_SLOTS.TryGetValue(state.Era ?? "", out v) ? v : 1;
        }

        public static int AmbitionCap(GameState state)
        {
            int v;
            return ERA_AMBITION_CAP.TryGetValue(state.Era ?? "", out v) ? v : 3;
        }

        /// <summary>How long a READY bet has waited for the founder's press.</summary>
        public static int ReadyAge(GameState state, Bet bet)
        {
            return Gd.Maxi(state.Week - bet.CommittedWeek, 0);
        }

        /// <summary>Weeks before a READY bet slips out on its own (0 = it goes this tick).</summary>
        public static int StallLeft(GameState state, Bet bet)
        {
            return Gd.Maxi(STALL_WEEKS - ReadyAge(state, bet), 0);
        }

        // ═══════════════════════ COMMIT — the allocation act ═════════════════════

        /// <summary>Point the team at a bet. Refuses a ready or shipped card, and
        /// refuses at the WIP cap: the desk stands one down explicitly, because
        /// switching costs.</summary>
        public static bool CommitBet(GameState state, string id)
        {
            Bet bet = BetById(state, id);
            if (bet == null || bet.Shipped || bet.Ready) { return false; }
            if (bet.Committed) { return true; }
            if (CommittedBets(state).Count >= WipCap(state)) { return false; }
            bet.Committed = true;
            bet.CommittedWeek = state.Week;
            state.LogAction(string.Format(CultureInfo.InvariantCulture,
                "roadmap: pointed the team at '{0}'", bet.Name));
            return true;
        }

        /// <summary>Stand a bet down. The team carries a quarter of the build out the
        /// door with them (DECISIONS.md — context-switching is priced, not free).</summary>
        public static bool UncommitBet(GameState state, string id)
        {
            Bet bet = BetById(state, id);
            if (bet == null || !bet.Committed) { return false; }
            bet.Committed = false;
            bet.Progress = Gd.Maxf(bet.Progress * (1.0 - ABANDON_DECAY), 0.0);
            state.LogAction(string.Format(CultureInfo.InvariantCulture,
                "roadmap: stood down '{0}' — a quarter of the build went with it", bet.Name));
            return true;
        }

        // ═══════════════════ CAPACITY — one team, priced honestly ════════════════

        /// <summary>
        /// THE WEEKLY CAPACITY POOL, in R&amp;D-weeks. Every term is a real one:
        /// money (the rnd lever at $1,200 the loaded week), engineers (0.25 x skill
        /// each — sub-1.0 is the honest meetings/review tax), the founder while they
        /// still build, the STATUS catalog's velocity_mult, TECH-DEBT INTEREST, and
        /// x1.15 per shipped platform level.
        /// </summary>
        public static double CapacityPool(GameState state)
        {
            double money = state.Budgets.Rnd / RND_PER_WEEK;
            double eng = 0.0;
            foreach (Employee e in state.Employees)
            {
                if ((e.Role ?? "").Contains("engineer"))
                {
                    eng += ENG_PER_SKILL * Gd.Clampi(e.Skill, 1, 5);
                }
            }
            double founder = 0.0;
            if (state.EraIndex() <= 1 && CommittedBets(state).Count > 0)
            {
                founder = FOUNDER_HANDS_ON;
            }
            return (money + eng + founder) * VelocityMult(state) * DebtDrag(state) * PlatformMult(state);
        }

        /// <summary>The first consumer of the STATUS catalog's velocity_mult, dormant
        /// since it was authored: crunch 1.35, burnt_out 0.6, founder_flow 1.15.</summary>
        public static double VelocityMult(GameState state)
        {
            double v = 1.0;
            foreach (Status s in state.Statuses)
            {
                v *= SimEngine.StatusEffect(s.Name).VelocityMult;
            }
            return v;
        }

        /// <summary>TECH-DEBT INTEREST (Cunningham): linear from 1.0 at debt 40 to 0.5 at 100.</summary>
        public static double DebtDrag(GameState state)
        {
            return Gd.Clampf(1.0 - Gd.Maxf(state.TechDebt - DEBT_FREE, 0.0) / DEBT_SPAN,
                DEBT_FLOOR, 1.0);
        }

        public static double PlatformMult(GameState state)
        {
            return 1.0 + PLATFORM_MULT * state.PlatformLevel;
        }

        /// <summary>What one committed bet gets this week (the pool splits evenly —
        /// that is the whole WIP lesson, in one division).</summary>
        public static double WeeklyShare(GameState state)
        {
            int n = CommittedBets(state).Count;
            return n <= 0 ? CapacityPool(state) : CapacityPool(state) / n;
        }

        public static int ProgressPct(Bet bet)
        {
            double cost = Gd.Maxf(bet.CostRndWeeks, 0.001);
            return Gd.ToInt(Gd.Clampf(bet.Progress / cost, 0.0, 1.0) * 100.0);
        }

        /// <summary>Honest ETA in weeks at THIS week's settings, or -1 when the
        /// current spend would never finish it (the desk says so in words).</summary>
        public static int EtaWeeks(GameState state, Bet bet)
        {
            double left = bet.CostRndWeeks - bet.Progress;
            if (left <= 0.0) { return 0; }
            int n = CommittedBets(state).Count;
            double share = CapacityPool(state) / Gd.Maxi(bet.Committed ? n : n + 1, 1);
            if (share <= 0.001) { return -1; }
            return (int)Math.Ceiling(left / share);
        }

        // ═══════════════════════ THE SHIP ROLL (salt 70) ═════════════════════════

        /// <summary>The odds the desk prints before the press — the same numbers the
        /// dice will face, minus luck (luck is felt, never advertised).</summary>
        public static int ShipOddsPct(GameState state, Bet bet)
        {
            int dc = BetDc(bet);
            int mod = state.Competence("build") - 3;
            int need = Gd.Clampi(dc - mod, 2, 20);
            double p = (21 - need) / 20.0;
            RollContext ctx = SimEngine.RollContext(state, "build");
            if (ctx.Advantage) { p = 1.0 - (1.0 - p) * (1.0 - p); }
            else if (ctx.Disadvantage) { p = p * p; }
            return Gd.RoundToInt(p * 100.0);
        }

        public static int BetDc(Bet bet)
        {
            if (bet.Kind == "platform") { return PLATFORM_DC; }
            return DC_BY_AMBITION[Gd.Clampi(bet.Ambition, 1, 3) - 1];
        }

        public static double BetCost(string kind, int ambition, string id = "")
        {
            if (id == HARDENING_ID) { return HARDENING_COST; }
            if (kind == "platform") { return PLATFORM_COST; }
            return COST_BY_AMBITION[Gd.Clampi(ambition, 1, 3) - 1];
        }

        /// <summary>
        /// THE LAUNCH, resolved by the house dice. UI-agnostic on purpose: the desk's
        /// SHIP button, the three-week slip and the twin suites all come through here
        /// with their own roller, so the ceremony can never disagree with the test.
        /// </summary>
        public static ShipResult ShipBet(GameState state, Bet bet, Func<int> roller)
        {
            RollContext ctx = SimEngine.RollD20Ctx(state, "build", roller);
            int dc = BetDc(bet);
            string band = SimEngine.MarginBand(ctx.Total, dc);
            bool qa = false;
            if (state.EraIndex() >= QA_NET_ERA && band == "backfired" && ctx.Total - dc >= QA_NET_MARGIN)
            {
                // THE QA NET: staging and review truncate the tail. They never raise
                // the ceiling — process reduces variance, not mean.
                band = "risky";
                qa = true;
            }
            var lines = new List<string>();
            int units = ApplyPayoff(state, bet, band, lines);
            ApplyBand(state, bet, band, lines);
            if (qa) { lines.Add("  → the QA net caught the worst of it"); }
            bet.Shipped = true;
            bet.Ready = false;
            bet.Committed = false;
            bet.ShippedWeek = state.Week;
            bet.Band = band;
            // W2 L-MAKE seam: the landing joins the wall in the same beat as the dice
            SimFeatures.OnBetLanded(state, bet, null);
            state.ClampiMeters();
            string ev = string.Format(CultureInfo.InvariantCulture,
                "SHIPPED {0}: '{1}' — d20 {2}{3} vs DC {4}", band.ToUpperInvariant(), bet.Name,
                ctx.D20, ctx.Mod.ToString("+0;-0;+0", CultureInfo.InvariantCulture), dc);
            if (band == "backfired" && ctx.Disadvantage)
            {
                // THE BURN ALWAYS EXPLAINS ITSELF: the reason the die was loaded rides
                // the receipt, so a bad week is a lesson and not a mood.
                ev += string.Format(CultureInfo.InvariantCulture, " (disadvantage: {0})",
                    string.Join(", ", ctx.DisReasons.ToArray()));
            }
            return new ShipResult
            {
                Band = band, D20 = ctx.D20, Mod = ctx.Mod, Dc = dc, Total = ctx.Total,
                Units = units, QaNet = qa, Event = ev, Lines = lines,
            };
        }

        /// <summary>THE PRESS (DECISIONS.md #2): the desk's SHIP button lands here and
        /// the house dice pour immediately.</summary>
        public static ShipResult ShipReady(GameState state, string id)
        {
            Bet bet = BetById(state, id);
            if (bet == null || !bet.Ready || bet.Shipped) { return null; }
            ShipResult res = ShipBet(state, bet, HouseRoller(state));
            state.LogAction(string.Format(CultureInfo.InvariantCulture,
                "roadmap: shipped '{0}' — {1} (d20 {2}{3} vs DC {4})", bet.Name, res.Band,
                res.D20, res.Mod.ToString("+0;-0;+0", CultureInfo.InvariantCulture), res.Dc));
            return res;
        }

        /// <summary>The house dice for a launch this week. Deterministic per (seed,
        /// week): the draws already spent by this week's launches are stepped over
        /// first, so two launches in one week never roll the same die twice.</summary>
        public static Func<int> HouseRoller(GameState state)
        {
            Rng r = SimEngine.RngForSalt(state, SimEngine.SALT_ROADMAP_SHIP);
            int done = 0;
            foreach (Bet b in state.Bets)
            {
                if (b.Shipped && b.ShippedWeek == state.Week) { done += 1; }
            }
            for (int i = 0; i < done * SHIP_DRAWS; i++) { r.RandiRange(1, 20); }
            return () => r.RandiRange(1, 20);
        }

        /// <summary>The integer payoff, by kind. Every magnitude is a table lookup; a
        /// status carries its own multiplier from the catalog and ambition buys WEEKS
        /// of it, never a bigger number (the one-typed-catalog law).</summary>
        private static int ApplyPayoff(GameState state, Bet bet, string band, List<string> lines)
        {
            int amb = Gd.Clampi(bet.Ambition, 1, 3);
            int bi = Gd.Maxi(Array.IndexOf(BANDS, band), 0);
            int units = BET_PAYOFF[amb - 1][bi];
            switch (bet.Kind)
            {
                case "quality":
                    if (units > 0)
                    {
                        state.Product = Gd.Mini(state.Product + units, 100);
                        lines.Add(string.Format(CultureInfo.InvariantCulture,
                            "  → product v0.{0} (+{1} quality)", state.Product, units));
                    }
                    break;
                case "retention":
                    if (units > 0)
                    {
                        SimEngine.AddStatus(state, "sticky_release", units);
                        lines.Add(string.Format(CultureInfo.InvariantCulture,
                            "  → customers stick: churn −25% for {0} wks", units));
                    }
                    break;
                case "reach":
                    if (units > 0)
                    {
                        SimEngine.AddStatus(state, "feature_buzz", units);
                        lines.Add(string.Format(CultureInfo.InvariantCulture,
                            "  → word gets out: adoption ×1.3 for {0} wks", units));
                        if (state.BizWho == "Enterprise")
                        {
                            // 05-pipeline reads GtmCapBonus() — a buzz the salespeople
                            // can actually carry (DECISIONS.md, roadmap).
                            lines.Add("  → and the room takes more meetings: +2 GTM capacity while it lasts");
                        }
                    }
                    break;
                case "debt":
                    if (units > 0)
                    {
                        state.TechDebt = Gd.Maxf(state.TechDebt - units * 3.0, 0.0);
                        lines.Add(string.Format(CultureInfo.InvariantCulture,
                            "  → the codebase breathes: debt −{0}", units * 3));
                    }
                    break;
                case "platform":
                    if (units > 0)
                    {
                        state.PlatformLevel = Gd.Mini(state.PlatformLevel + 1, PLATFORM_MAX);
                        lines.Add(string.Format(CultureInfo.InvariantCulture,
                            "  → the platform compounds: all builds ×{0} from here",
                            Gd.F(PlatformMult(state), 2)));
                    }
                    break;
            }
            return units;
        }

        /// <summary>What the launch did to the room, and to the codebase. A refactor is
        /// never punished with debt — that would be absurd.</summary>
        private static void ApplyBand(GameState state, Bet bet, string band, List<string> lines)
        {
            bool gentle = bet.Kind == "debt" || bet.Kind == "platform";
            switch (band)
            {
                case "brilliant":
                    state.Hype = Gd.Clampi(state.Hype + 8, 0, 100);
                    break;
                case "fine":
                    state.Hype = Gd.Clampi(state.Hype + 3, 0, 100);
                    break;
                case "risky":
                    double pen = bet.Kind == "platform" ? 10.0 : (bet.Kind == "debt" ? 0.0 : 6.0);
                    if (pen > 0.0)
                    {
                        state.TechDebt = Gd.Clampf(state.TechDebt + pen, 0.0, 100.0);
                        lines.Add(string.Format(CultureInfo.InvariantCulture,
                            "  → shipped hot: debt +{0}", Gd.ToInt(pen)));
                    }
                    break;
                case "backfired":
                    double dpen = gentle ? 6.0 : 12.0;
                    state.TechDebt = Gd.Clampf(state.TechDebt + dpen, 0.0, 100.0);
                    state.Morale = Gd.Clampi(state.Morale - 6, 0, 100);
                    lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "  → nothing shipped worth keeping: debt +{0}, the room deflates",
                        Gd.ToInt(dpen)));
                    break;
            }
        }

        /// <summary>THE THREE-WEEK SLIP (DECISIONS.md #2): a launch nobody presses goes
        /// out anyway. The world does not wait forever, and the receipt says why.</summary>
        private static void ShipStalled(GameState state, WeeklyReport rep)
        {
            foreach (Bet bet in ReadyBets(state))
            {
                if (ReadyAge(state, bet) < STALL_WEEKS) { continue; }
                ShipResult res = ShipBet(state, bet, HouseRoller(state));
                if (rep != null)
                {
                    rep.Events.Add(res.Event);
                    rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "nobody pressed ship for {0} weeks — '{1}' slipped out on its own",
                        STALL_WEEKS, bet.Name));
                    foreach (string l in res.Lines) { rep.Lines.Add(l); }
                }
                state.LogAction(string.Format(CultureInfo.InvariantCulture,
                    "roadmap: '{0}' slipped out on its own ({1})", bet.Name, res.Band));
            }
        }

        /// <summary>THE HQ MAINTENANCE TAX: a big org pays a standing maintenance share
        /// or it rots. Ten weeks with no upkeep shipped and nothing committed bills
        /// 0.8 debt a week, with the reason attached.</summary>
        private static void MaintenanceTax(GameState state, WeeklyReport rep)
        {
            if (state.Era != "hq") { return; }
            foreach (Bet b in state.Bets)
            {
                if (b.Kind != "debt" && b.Kind != "platform") { continue; }
                if (b.Committed || b.Ready) { return; }
                if (b.Shipped && b.ShippedWeek > state.Week - MAINTENANCE_WINDOW) { return; }
            }
            state.TechDebt = Gd.Clampf(state.TechDebt + MAINTENANCE_DEBT, 0.0, 100.0);
            if (rep != null)
            {
                rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "organizational entropy: debt +{0} (no maintenance shipped in {1} wks)",
                    Gd.F(MAINTENANCE_DEBT, 1), MAINTENANCE_WINDOW));
            }
        }

        /// <summary>OPPORTUNITY COST, made real. The spine's R&amp;D block has already
        /// turned this week's rnd money into base quality; while a bet is committed
        /// that money was spent on WEEKS instead, so the drip is handed back — the
        /// product number and the receipt line both. One team, one throughput.</summary>
        private static void RouteRnd(GameState state, WeeklyReport rep)
        {
            int p0 = Gd.ToInt(state.GetMetaF(PRODUCT_PRE, -1.0));
            state.SetMeta(PRODUCT_PRE, -1);
            if (p0 < 0 || state.Product <= p0) { return; }
            state.Product = p0;
            if (rep == null) { return; }
            for (int i = rep.Lines.Count - 1; i >= 0; i--)
            {
                if (rep.Lines[i] != null && rep.Lines[i].StartsWith("R&D shipped: product v0.",
                        StringComparison.Ordinal))
                {
                    rep.Lines.RemoveAt(i);
                    break;
                }
            }
        }

        // ═════════════════════ THE BOARD REFRESH (salt 71) ═══════════════════════

        /// <summary>Idempotent, every tick: the standing bet exists, stale candidates
        /// go, open slots refill from the pool. Committed work survives an era change
        /// — losing paid work teaches nothing but resentment.</summary>
        public static void RefreshBets(GameState state, WeeklyReport rep)
        {
            LastRefreshed = 0;
            // 1 ── the standing law
            if (HardeningBet(state) == null)
            {
                state.Bets.Add(new Bet
                {
                    Id = HARDENING_ID, Name = HARDENING_NAME, Desc = HARDENING_DESC,
                    Kind = "debt", Ambition = 1, CostRndWeeks = HARDENING_COST,
                    Era = state.Era,
                });
            }
            // 2 ── the era refresh: a stage change resets the roadmap's candidates
            var kept = new List<Bet>();
            foreach (Bet b in state.Bets)
            {
                if (b.Shipped || b.Committed || b.Ready || b.Id == HARDENING_ID || b.Era == state.Era)
                {
                    kept.Add(b);
                }
            }
            state.Bets = kept;
            // 3 ── refill what the era allows
            int openSlots = Slots(state) - BoardBets(state).Count;
            int drawn = 0;
            if (openSlots > 0)
            {
                Rng r = SimEngine.RngForSalt(state, SimEngine.SALT_ROADMAP_SLOTS);
                var recent = new List<string>();
                foreach (Bet s in ShippedBets(state)) { recent.Add(s.Name); }
                for (int n = 0; n < openSlots; n++)
                {
                    List<int> eligible = Eligible(state, recent);
                    if (eligible.Count == 0)
                    {
                        // exclusion (b) drops FIRST — a board with nothing on it teaches
                        // nothing; the era gates (c) and (d) never drop.
                        eligible = Eligible(state, new List<string>());
                    }
                    if (eligible.Count == 0) { break; }
                    PoolCard pick = BET_POOL[eligible[r.RandiRange(0, eligible.Count - 1)]];
                    state.Bets.Add(MakeBet(state, pick, n + 1));
                    drawn += 1;
                }
            }
            // 4 ── history: the last eight launches stay, the rest fall off the board
            int drop = ShippedBets(state).Count - SHIPPED_KEPT;
            if (drop > 0)
            {
                var outp = new List<Bet>();
                foreach (Bet b2 in state.Bets)
                {
                    if (b2.Shipped && drop > 0)
                    {
                        drop -= 1;   // the list is in launch order: the oldest go first
                        continue;
                    }
                    outp.Add(b2);
                }
                state.Bets = outp;
            }
            if (drawn > 0)
            {
                LastRefreshed = drawn;
                if (rep != null)
                {
                    rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0} new bets on the roadmap board", drawn));
                }
            }
        }

        /// <summary>The board with paper on it from the first open, even before the
        /// first tick. Deterministic (salt 71 keyed to the week) and idempotent — it
        /// only fires when there is nothing to look at.</summary>
        public static void EnsureBoard(GameState state)
        {
            if (Unshipped(state).Count == 0) { RefreshBets(state, null); }
        }

        /// <summary>Pool indices this era may draw: nothing already on the board,
        /// nothing in the last eight launches, ambition capped in the garage, platform
        /// work only once there is a floor to put it on.</summary>
        private static List<int> Eligible(GameState state, List<string> recent)
        {
            var onBoard = new List<string>();
            foreach (Bet b in Unshipped(state)) { onBoard.Add(b.Name); }
            var outp = new List<int>();
            for (int i = 0; i < BET_POOL.Length; i++)
            {
                PoolCard c = BET_POOL[i];
                if (onBoard.Contains(c.Name) || recent.Contains(c.Name)) { continue; }
                if (c.Ambition > AmbitionCap(state)) { continue; }
                if (c.Kind == "platform" && state.EraIndex() < 3) { continue; }
                outp.Add(i);
            }
            return outp;
        }

        private static Bet MakeBet(GameState state, PoolCard card, int n)
        {
            int amb = Gd.Clampi(card.Ambition, 1, AmbitionCap(state));
            return new Bet
            {
                Id = string.Format(CultureInfo.InvariantCulture, "bet_w{0}_{1}", state.Week, n),
                Name = Gd.Left(card.Name, 28),
                Desc = Gd.Left(card.Desc, 90),
                Kind = card.Kind,
                Ambition = amb,
                CostRndWeeks = BetCost(card.Kind, amb),
                Era = state.Era,
            };
        }

        // ═══════════════════ THE DRESSING SEAM (LLM value point A) ═══════════════

        /// <summary>What the one batch dressing call is told. Null = nothing to
        /// dress, so the caller never fires. The model sees the business and the
        /// board; it never sees a cost, a DC or a payoff — those are not its
        /// business. JObject because the LLM assembly is pure transport and never
        /// sees a Core type (the same contract SimLabor's dressing uses).</summary>
        public static JObject DressingPayload(GameState state)
        {
            List<Bet> targets = Dressable(state);
            if (targets.Count == 0) { return null; }
            var board = new JArray();
            foreach (Bet b in Unshipped(state)) { board.Add(b.Name ?? ""); }
            var shipped = new JArray();
            foreach (Bet s in ShippedBets(state)) { shipped.Add(s.Name ?? ""); }
            return new JObject
            {
                { "company", new JObject {
                    { "name", state.CompanyName ?? "" }, { "idea", state.CompanyIdea ?? "" },
                    { "what", state.BizWhat ?? "" }, { "who", state.BizWho ?? "" } } },
                { "era", state.EraDisplayName() },
                { "board", board },
                { "recently_shipped", shipped },
                { "slots", targets.Count },
            };
        }

        /// <summary>
        /// The one place a model may touch the roadmap: a fresh candidate's WORDS and
        /// its rung of an authored ladder. Everything with a number attached is
        /// re-priced from the tables, so a slow or hostile reply can only ever change
        /// what a card is CALLED. Committed or started work is untouchable.
        /// </summary>
        public static int DressBets(GameState state, JArray cards)
        {
            if (cards == null) { return 0; }
            List<Bet> targets = Dressable(state);
            int done = 0;
            foreach (JToken tok in cards)
            {
                var card = tok as JObject;
                if (card == null || done >= targets.Count) { break; }
                Bet target = targets[done];
                string kind = RowStr(card, "kind");
                if (Array.IndexOf(KINDS, kind) < 0) { kind = target.Kind; }   // off-enum: keep the authored card
                if (kind == "platform" && state.EraIndex() < 3) { kind = "quality"; }
                int amb = Gd.Clampi(RowInt(card, "ambition", 1), 1, AmbitionCap(state));
                string nm = Gd.Left(RowStr(card, "name"), 28);
                string ds = Gd.Left(RowStr(card, "desc"), 90);
                if (nm.Trim().Length == 0) { continue; }
                target.Name = nm;
                target.Desc = ds;
                target.Kind = kind;
                target.Ambition = amb;
                target.CostRndWeeks = BetCost(kind, amb, target.Id);
                done += 1;
            }
            return done;
        }

        /// <summary>Legacy name from the spec — the same ingestion, one door.</summary>
        public static int ApplyBetDressing(GameState state, JArray cards)
        {
            return DressBets(state, cards);
        }

        static string RowStr(JObject row, string key)
        {
            JToken t = row[key];
            return t == null || t.Type == JTokenType.Null ? "" : (string)t;
        }

        static int RowInt(JObject row, string key, int dflt)
        {
            JToken t = row[key];
            if (t == null || t.Type == JTokenType.Null) { return dflt; }
            try { return (int)t; }
            catch (Exception) { return dflt; }
        }

        /// <summary>The candidates a reply may repaint, in board order: untouched paper only.</summary>
        public static List<Bet> Dressable(GameState state)
        {
            var outp = new List<Bet>();
            foreach (Bet b in BoardBets(state))
            {
                if (b.Progress == 0.0 && !b.Committed && !b.Ready) { outp.Add(b); }
            }
            return outp;
        }

        // ═════════════════════ COORDINATION — what other lanes read ══════════════

        /// <summary>THE COACH'S ONE LINE about this desk (07 INTERFACE DELTA). Null
        /// until there is a board to point at; the garage owns the one-timer, so
        /// this only answers "is there something worth saying, and what is it".</summary>
        public static Dictionary<string, string> CoachChip(GameState state)
        {
            if (Unshipped(state).Count == 0) { return null; }
            return new Dictionary<string, string>
            {
                { "id", "roadmap_live" },
                { "text", "the roadmap is live — point the team at a bet, or the R&D money just polishes what exists." },
            };
        }

        /// <summary>ENTERPRISE REACH (DECISIONS.md, roadmap): a reach launch on an
        /// Enterprise run also buys the salespeople two more meetings a week for as
        /// long as the buzz lasts. Derived from live state — no new saved field, and
        /// it expires exactly when `feature_buzz` does. 04/05 add this to GtmCap.</summary>
        public static double GtmCapBonus(GameState state)
        {
            if (state.BizWho != "Enterprise") { return 0.0; }
            return SimEngine.HasStatus(state, "feature_buzz") ? 2.0 : 0.0;
        }
    }
}
