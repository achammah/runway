using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Runway.Core
{
    /// <summary>
    /// LANE 08 — THE BOARD &amp; M&amp;A (covenants, offers, the exit). Spec: docs/design/08-board-mna.md
    ///
    /// One file because it is one arc: the check you took installs a room that
    /// measures you, the record you build in that room prices the next check,
    /// and the same record decides whether anyone ever offers to buy the whole
    /// thing.
    ///
    ///   THE BOARD is a plan of record. A priced round writes a growth covenant
    ///   and a quarterly review date; the review is DETERMINISTIC — revenue
    ///   against a bar, no dice — and it walks the real intervention ladder: a
    ///   hard meeting, then the coach a board sends before it does worse, then
    ///   the reprice that is a down round waiting to happen. Governance HARDENS
    ///   with the company: an angel's handshake in the garage, audit-committee
    ///   cadence at hq.
    ///
    ///   M&amp;A is a courtship on a clock. Offers arrive on their own dice (salt
    ///   100), priced as a premium on YOUR standalone valuation, and every one
    ///   of them dies in two weeks unless it is signed. Writing any other move
    ///   IS walking away. At hq the IPO window opens as the alternative exit —
    ///   weather, not a decision: clean covenants in a market that is buying.
    ///
    /// The spine calls, in tick order (00-spine section 1, HOOKS.md):
    ///   TickPre    tick 9 — nothing: this lane reads a week already closed
    ///   TickMoney  the money section — this lane owns NO P&amp;L lane (the coach
    ///              the board sends bills through Commitments, like any
    ///              standing cost)
    ///   TickPost   9c the board review, then 9d M&amp;A lapse to generation to
    ///              the IPO window, both reading the revenue and valuation this
    ///              week just posted
    /// and outside the tick: Directives feeds the DM block, Attention feeds
    /// every bang in the game through SimEngine.AttentionItems, and the two
    /// journal seams draw the offer and take the signature.
    ///
    /// SALT (00-spine section 3): 100, M&amp;A arrival + premium. ONE stream, drawn
    /// in the fixed trigger order below. The review has no salt at all: a
    /// covenant is arithmetic, and a board that rolled dice would teach nothing.
    ///
    /// TWIN LAW: this file and game/src/core/lanes/sim_board.gd carry the same
    /// logic in the same order. The engines do NOT share PRNG internals, so
    /// parity means same checks and same behaviour, never a byte-equal draw.
    /// </summary>
    public static class SimBoard
    {
        // ── the covenant's constants ────────────────────────────────────────
        /// <summary>A pre-revenue raise still gets a concrete bar: the floor a
        /// board would plan from at each stage.</summary>
        public static readonly Dictionary<string, int> ERA_REV_FLOOR = new Dictionary<string, int>
        {
            { "garage", 40 }, { "coworking", 120 }, { "office", 500 },
            { "floor", 2000 }, { "hq", 8000 },
        };

        /// <summary>Growth persistence decay: big bases grow slower and real
        /// boards plan for exactly that.</summary>
        public static readonly Dictionary<string, double> ERA_TARGET_MULT = new Dictionary<string, double>
        {
            { "garage", 1.0 }, { "coworking", 1.0 }, { "office", 0.9 },
            { "floor", 0.8 }, { "hq", 0.65 },
        };

        public const int ReviewCadence = 12;      // 12 weeks is the quarterly board meeting
        public const int GoodwillCap = 3;
        public const int CoachWeeks = 6;
        public const int CoachMin = 250;
        public const int CoachMax = 2500;
        public const double FundingMultFloor = 0.5;
        /// Weeks after a signing or a review that a founder sale is on the table.
        public const int SecondaryWindow = 4;

        // ── M&A's constants ─────────────────────────────────────────────────
        public const int NoShopWeeks = 2;         // the exploding LOI, at game scale
        public const int MnaCooldown = 10;        // corp dev does not re-approach next week
        public const int MnaFirstWeek = 6;
        public const int SniffLapse = 8;
        public const int MinPrice = 10000;

        /// <summary>The one-shot valuation bands a strategic notices you crossing.</summary>
        static readonly long[] BandValues = { 50000000L, 10000000L, 2000000L };
        static readonly string[] BandFlags = { "mna_band_50m", "mna_band_10m", "mna_band_2m" };

        /// <summary>
        /// THE ARM (10-interface-language section 2.9). Selling the company is
        /// the heaviest act in the game, so the card takes two taps — and the
        /// journal seam has no screen-local bool to hold the first one. The key
        /// is (seed, week, card): a different run, week or offer can never
        /// inherit an arm, and nothing durable is invented to store it.
        /// </summary>
        static string _armedKey = "";

        // ═════════════════════ THE SPINE'S ENTRY POINTS ═════════════════════
        /// <summary>Tick 9, before the money. Nothing: a covenant is measured
        /// against a week that has closed, and an offer is priced off a
        /// valuation not computed yet. Both wait for TickPost, deliberately.</summary>
        public static void TickPre(GameState state, WeeklyReport rep)
        {
        }

        /// <summary>
        /// The money section. This lane writes no P&amp;L lane of its own: the
        /// executive coach the board sends is a standing cost like any other and
        /// bills through Commitments, so it shows up in the ledger under its own
        /// name with no new column to explain.
        /// </summary>
        public static void TickMoney(GameState state, WeeklyReport rep, MoneyWork m)
        {
        }

        /// <summary>After the record is written: 9c the board review against the
        /// covenant (deterministic — no dice, no salt), then 9d M&amp;A offers and
        /// the IPO window (salt 100), priced off the growth this week posted.</summary>
        public static void TickPost(GameState state, WeeklyReport rep)
        {
            // A signed exit ends the run on the next week change. Nothing may
            // reprice the company between the signature and the ceremony.
            if (state.ExitValue > 0 || state.Dead) return;
            Review(state, rep);
            Mna(state, rep);
            IpoWindow(state, rep);
        }

        // ═══════════════════ section 2 STAGE — what exists when ═════════════
        /// <summary>Governance grows when the company does, read LIVE at every
        /// review and every signing: 0 garage, 1 coworking, 2 office, 3 floor,
        /// 4 hq.</summary>
        public static int BoardStage(GameState state)
        {
            return state.EraIndex();
        }

        /// <summary>The option pool a term sheet asks for at this stage, in
        /// points written PRE-money. Nothing below office — an angel does not
        /// paper an ESOP; later rounds top up rather than create, so hq is half.</summary>
        public static double PoolAskPct(GameState state)
        {
            int stage = BoardStage(state);
            if (stage == 2 || stage == 3) return 10.0;
            return stage >= 4 ? 5.0 : 0.0;
        }

        /// <summary>The strike ceiling this stage can reach: no ladder in the
        /// garage, two rungs at coworking, the full three from office up.</summary>
        public static int StrikeCap(GameState state)
        {
            int stage = BoardStage(state);
            if (stage <= 0) return 0;
            return stage == 1 ? 2 : 3;
        }

        /// <summary>
        /// THE COVENANT, percent per quarter. Round by round it is T2D3: a
        /// seed/A company tripling ARR yearly compounds at ~31%/quarter, and
        /// each round resets the bar higher (30/35/40/45). The era multiplier is
        /// growth-persistence decay; the season is the board re-forecasting to
        /// the climate — a winter board asks for less, not for nothing.
        /// </summary>
        public static double BoardTargetPct(GameState state)
        {
            int rounds = state.RoundsRaised != null ? state.RoundsRaised.Count : 0;
            double baseP = 25.0 + 5.0 * Gd.Mini(rounds, 4);
            double eraM;
            if (!ERA_TARGET_MULT.TryGetValue(state.Era ?? "", out eraM)) eraM = 1.0;
            double macM = 1.0;
            if (state.MacroSeason == "winter") macM = 0.7;
            else if (state.MacroSeason == "boom") macM = 1.2;
            return Gd.Snappedf(Gd.Clampf(baseP * eraM * macM, 10.0, 60.0), 1.0);
        }

        /// <summary>
        /// THE GOVERNANCE RECORD, in points off (or onto) the next round's
        /// equity ask. A clean record IS lower perceived risk and a smaller ask;
        /// missed plans are a risk premium. SimEngine.WarmthPct adds this to
        /// trait warmth and clamps the sum to [0, 12].
        /// </summary>
        public static double WarmthDelta(GameState state)
        {
            if (state.Board == null) return 0.0;
            return 2.0 * state.Board.Goodwill - 2.5 * state.Board.Strikes;
        }

        /// <summary>Weeks until the next board review; -1 when there is no board.</summary>
        public static int BoardReviewIn(GameState state)
        {
            if (state.Board == null) return -1;
            return Gd.Maxi(state.Board.ReviewWeek - state.Week, 0);
        }

        // ═══════ section 3 ROUND CLOSE — seats, the pool shuffle, covenant ═══
        /// <summary>
        /// SEAM (coordinator-planted): fires at the signature, both signing
        /// sites, immediately after SimEngine.ApplyRound took the investor's
        /// slice.
        ///
        /// THE POOL SHUFFLE, the standard term-sheet move: the option pool is
        /// written into the PRE-money, so its dilution lands on the founders'
        /// side and not on the investor's. ApplyRound has already multiplied the
        /// existing side by invKeep; multiplying by poolKeep here lands on
        /// exactly founder x poolKeep x invKeep, because multiplication commutes
        /// and the pool is never granted retroactively. The pool ITSELF is
        /// created pre-money and then diluted with everyone else — that is the
        /// (old x keep + new) x keep below, and it is the whole lesson: the
        /// slice comes out of you.
        /// </summary>
        public static void OnRoundClosed(GameState state, int amount, double pct)
        {
            double pool = Gd.Clampf(PoolAskPct(state), 0.0, 15.0);
            double poolKeep = 1.0 - pool / 100.0;
            double invKeep = 1.0 - Gd.Clampf(pct, 0.0, 100.0) / 100.0;
            if (pool > 0.0)
            {
                state.FounderPct = Gd.Maxf(state.FounderPct * poolKeep, 1.0);
                foreach (Cofounder cf in state.Cofounders)
                {
                    cf.EquityDiluted = (cf.EquityDiluted ?? cf.Equity) * poolKeep;
                }
            }
            state.OptionPoolPct = Gd.Clampf(
                (state.OptionPoolPct * poolKeep + pool) * invKeep, 0.0, 100.0);

            // SEATS. A first priced round buys a seat; from the floor up, a
            // third round buys a second one. Three is the ceiling — past that
            // the founder is outvoted on their own cap table.
            int stage = BoardStage(state);
            if (stage >= 1)
            {
                int earned = 1 + ((state.RoundsRaised.Count >= 3 && stage >= 3) ? 1 : 0);
                state.BoardSeatsInvestor = Gd.Clampi(
                    Gd.Maxi(state.BoardSeatsInvestor, earned), 0, 3);
            }

            // THE PLAN OF RECORD. The bar is set from the week's actual revenue
            // or the era's floor, whichever is higher, so a pre-revenue raise
            // still owes a concrete number. A new round INHERITS the record — a
            // fresh board does not forgive the last one's strikes.
            int revenue = state.LastPnl != null ? state.LastPnl.Revenue : 0;
            int eraFloor;
            if (!ERA_REV_FLOOR.TryGetValue(state.Era ?? "", out eraFloor)) eraFloor = 40;
            int baseRev = Gd.Maxi(revenue, eraFloor);
            double targetPct = BoardTargetPct(state);
            var prev = state.Board;
            state.Board = new BoardState
            {
                TargetGrowthPct = targetPct,
                BaseRevenue = baseRev,
                TargetRevenue = Gd.ToInt(baseRev * (1.0 + targetPct / 100.0)),
                ReviewWeek = state.Week + ReviewCadence,
                Strikes = prev != null ? prev.Strikes : 0,
                Goodwill = prev != null ? prev.Goodwill : 0,
            };
            // THE FORMATION RECEIPT: the obligations taken with the check enter
            // the written record in the same breath as the check itself.
            if (stage == 0)
                state.LogAction(string.Format(
                    "the angel shook on it: {0}%/quarter is the number you said out loud — talk again wk {1}",
                    Gd.ToInt(targetPct), state.Board.ReviewWeek));
            else
                state.LogAction(string.Format(
                    "a board now sits between you and the company: {0} investor seat(s) · growth covenant {1}%/quarter · first review wk {2}",
                    state.BoardSeatsInvestor, Gd.ToInt(targetPct), state.Board.ReviewWeek));
            if (pool > 0.0)
                state.LogAction(string.Format(
                    "the pool shuffle: a {0}% option pool written PRE-money — the dilution came out of your side, not theirs",
                    Gd.ToInt(pool)));
        }

        // ═════════════════ section 4 THE REVIEW (deterministic) ═════════════
        /// <summary>Revenue against a bar, no dice. Fires the week the review
        /// lands and re-arms for the next quarter whichever way it went — a
        /// board that stops measuring you is not a board.</summary>
        static void Review(GameState state, WeeklyReport rep)
        {
            if (state.Board == null) return;
            BoardState b = state.Board;
            if (state.Week < b.ReviewWeek) return;
            int stage = BoardStage(state);
            int measured = state.LastPnl != null ? state.LastPnl.Revenue : 0;
            int target = b.TargetRevenue;

            // THE UPDATE YOU SENT. The adjudicator graded a written move weeks
            // ago and left a flag; the ENGINE converts it to goodwill here, so
            // the LLM never touched a board number.
            if (state.HasFlag("investor_update_sent"))
            {
                b.Goodwill = Gd.Mini(b.Goodwill + 1, GoodwillCap);
                state.Flags.Remove("investor_update_sent");
                rep.Lines.Add("the update you sent bought patience — the room read it (+goodwill)");
            }

            if (measured >= target)
            {
                b.Strikes = Gd.Maxi(b.Strikes - 1, 0);
                b.Goodwill = Gd.Mini(b.Goodwill + 1, GoodwillCap);
                SimEngine.AddStatus(state, "board_delight", 4);
                if (stage == 0)
                    rep.Lines.Add(string.Format(
                        "the angel checked in — the numbers spoke for you: ${0}/wk against the ${1} you talked about. A quarter like that is cheap capital later (board_delight, 4 wks)",
                        measured, target));
                else
                    rep.Lines.Add(string.Format(
                        "BOARD REVIEW — COVENANT MET: ${0}/wk against the ${1} bar. A clean quarter is cheap capital later (board_delight, 4 wks)",
                        measured, target));
            }
            else if (stage == 0)
            {
                // No board exists yet. An angel has expectations, not covenants:
                // the week is awkward and nothing goes on a record that does not
                // exist.
                SimEngine.AddStatus(state, "investor_pressure", 3);
                rep.Lines.Add(string.Format(
                    "the angel checked in — ${0}/wk against the ${1} you talked about. Awkward calls all week (investor_pressure, 3 wks)",
                    measured, target));
            }
            else
            {
                int before = b.Strikes;
                int after = Gd.Mini(before + 1, StrikeCap(state));
                b.Strikes = after;
                SimEngine.AddStatus(state, "investor_pressure", 4);
                rep.Lines.Add(string.Format(
                    "BOARD REVIEW — COVENANT MISSED: ${0}/wk against the ${1} bar. Strike {2} (investor_pressure, 4 wks)",
                    measured, target, after));
                if (stage >= 3) state.Hype = Gd.Clampi(state.Hype - 2, 0, 100);
                // THE LADDER, on the rung it just reached — never re-fired for
                // standing still on it. Boards hire the CEO a coach before
                // anything harsher.
                if (before < 2 && after >= 2)
                {
                    int payroll = 0;
                    foreach (Employee e in state.Employees) payroll += e.Salary;
                    foreach (PipelineHire h in state.Pipeline) payroll += h.Salary;
                    int coachWk = Gd.Clampi(Gd.ToInt(payroll * 0.05), CoachMin, CoachMax);
                    state.Commitments.Add(new Commitment
                    {
                        Name = "the executive coach the board sent",
                        CashWk = -coachWk,
                        WeeksLeft = CoachWeeks,
                    });
                    rep.Events.Add(string.Format(
                        "STRIKE TWO — the board sent a CEO coach: ${0}/wk for six weeks. This is what boards do before they do worse",
                        coachWk));
                    if (stage >= 4) state.Hype = Gd.Clampi(state.Hype - 5, 0, 100);
                }
                if (before < 3 && after >= 3 && stage >= 2)
                {
                    if (state.Theta != null)
                        state.Theta.FundingMult = Gd.Maxf(state.Theta.FundingMult * 0.8, FundingMultFloor);
                    state.SetFlag("down_round_threat");
                    rep.Events.Add("STRIKE THREE — the board reprices you: every future round now values the company 20% lower. That is a down round waiting to happen");
                }
            }

            // RE-ARM, both ways. The next quarter's bar is set from what you
            // actually did, with era, round count and season all re-read live.
            int floorNow;
            if (!ERA_REV_FLOOR.TryGetValue(state.Era ?? "", out floorNow)) floorNow = 40;
            b.BaseRevenue = Gd.Maxi(measured, floorNow);
            b.TargetGrowthPct = BoardTargetPct(state);
            b.TargetRevenue = Gd.ToInt(b.BaseRevenue * (1.0 + b.TargetGrowthPct / 100.0));
            b.ReviewWeek = state.Week + ReviewCadence;
        }

        // ═════════════ section 6 M&A — lapse, then the courtship ════════════
        static void Mna(GameState state, WeeklyReport rep)
        {
            // LAPSE FIRST. LOIs die by lapse, and a leaked number destabilizes a
            // team while a public suitor validates the market — both, same week.
            if (state.Mna != null && state.Week > state.Mna.ExpiresWeek)
            {
                bool lifeline = state.Mna.Why == "lifeline";
                int mor = lifeline ? 5 : 3;
                state.Morale = Gd.Clampi(state.Morale - mor, 0, 100);
                state.Hype = Gd.Clampi(state.Hype + 2, 0, 100);
                state.Mna = null;
                state.MnaLastWeek = state.Week;
                _armedKey = "";
                ClearSniff(state);
                rep.Lines.Add(string.Format(
                    "the no-shop lapsed — the offer is off the table. The team heard the number (−{0} morale); so did the street (+2 hype)",
                    mor));
                return;                      // one M&A beat a week, always
            }
            if (state.Mna != null) return;   // a live offer blocks every other approach
            // INTEREST GOES COLD. A rival that asked about you and never wrote a
            // sheet stops asking; the street's flag comes down with it.
            LapseSniff(state);
            if (state.Week < MnaFirstWeek || state.Week < state.MnaLastWeek + MnaCooldown) return;

            // THE TRIGGER LADDER — first hit wins, drawn in this order forever.
            Rng r = SimEngine.RngForSalt(state, SimEngine.SALT_MNA);
            int v = SimEngine.Valuation(state);
            Rival strong = StrongestRival(state);
            Rival sniffer = SniffingRival(state);
            string why = "";
            double prem = 0.0;
            string buyer = "";

            // 1 · THE LIFELINE. Distressed acqui-hire economics: they are
            // pricing the team and the shutdown avoided, not the business. It is
            // the floor, so it never rolls.
            if ((state.WeeksInRed >= 2 || SimEngine.RunwayWeeks(state) <= 2)
                && (state.Traction >= 5 || state.Product >= 30))
            {
                why = "lifeline";
                prem = 0.3 + r.Randf() * 0.2;
                buyer = BuyerOr(strong, 55.0, "a quiet strategic");
            }
            // 2 · THE RIVAL. A consolidator buying a competitor — sometimes
            // lowballing a wounded one (0.9x). A rival that already asked about
            // your price (the street's sniff) is mid-courtship, so it writes far
            // more often.
            else if (sniffer != null && r.Randf() < 0.45)
            {
                why = "rival";
                prem = 0.9 + r.Randf() * 0.4;
                buyer = sniffer.Name ?? "a rival";
            }
            else if (strong != null && strong.Strength >= 70.0 && r.Randf() < 0.20)
            {
                why = "rival";
                prem = 0.9 + r.Randf() * 0.4;
                buyer = strong.Name ?? "a rival";
            }
            // 3 · THE BOOM. Frothy-market multiple expansion: the same company
            // is worth more this quarter because money is cheap.
            else if (state.MacroSeason == "boom" && v >= 500000 && r.Randf() < 0.15)
            {
                why = "boom";
                prem = 1.2 + r.Randf() * 0.6;
                buyer = "a strategic riding the market";
            }
            else
            {
                // 4 · THE MILESTONE. Crossing $2M / $10M / $50M puts you on a
                // list. One shot per band: the flag is stamped at the approach.
                string band = "";
                for (int i = 0; i < BandValues.Length; i++)
                {
                    if (v >= BandValues[i] && !state.HasFlag(BandFlags[i]))
                    {
                        band = BandFlags[i];
                        break;
                    }
                }
                if (band.Length > 0 && r.Randf() < 0.35)
                {
                    why = "milestone";
                    prem = 1.0 + r.Randf() * 0.5;
                    buyer = BuyerOr(strong, 55.0, "a strategic who has been watching");
                    state.SetFlag(band);
                }
            }
            if (why.Length == 0) return;

            state.Mna = new MnaOffer
            {
                Buyer = buyer,
                Why = why,
                Premium = Gd.Snappedf(prem, 0.01),
                Price = Gd.Maxi(Gd.ToInt(v * prem), MinPrice),
                ExpiresWeek = state.Week + NoShopWeeks,
            };
            state.MnaLastWeek = state.Week;
            _armedKey = "";
            if (why == "rival") ClearSniff(state);   // the courtship became a sheet
            rep.Events.Add(string.Format(
                "AN OFFER FOR THE COMPANY: {0} puts ${1} on the table — a {2}% {3} on your ${4} standalone value. {5} The no-shop clock runs {6} weeks",
                buyer, Money(state.Mna.Price), Gd.RoundToInt(Gd.Absf(prem - 1.0) * 100.0),
                PremiumLabel(why, prem), Money(v), PremiumWhy(why, prem), NoShopWeeks));
        }

        /// <summary>What the premium IS, in the term a banker would use. Kept to
        /// a NOUN so it drops into the receipt's sentence frame unbroken.</summary>
        public static string PremiumLabel(string why, double prem)
        {
            if (why == "lifeline") return "acqui-hire discount";
            if (prem >= 1.0) return "strategic premium";
            return "consolidator's discount";
        }

        /// <summary>And why that number is that number — its own sentence,
        /// because a receipt that names a mechanism has to say what the
        /// mechanism means in the same breath.</summary>
        public static string PremiumWhy(string why, double prem)
        {
            if (why == "lifeline")
                return "They are pricing the team and the shutdown avoided, not the business.";
            if (prem >= 1.0)
                return "That is what control of your customers is worth to somebody else.";
            return "A consolidator buys a wounded competitor cheap.";
        }

        // ═══════════════ section 6 THE IPO WINDOW (hq only) ═════════════════
        /// <summary>Weather, not a decision. It opens on clean governance in a
        /// receptive market and shuts in winters — and the reason it shut is the
        /// lesson.</summary>
        static void IpoWindow(GameState state, WeeklyReport rep)
        {
            int strikes = state.Board != null ? state.Board.Strikes : 0;
            bool openNow = state.Era == "hq" && state.Traction >= 100
                && state.RoundsRaised.Count >= 2 && strikes == 0
                && state.MacroSeason != "winter";
            if (openNow && !state.HasFlag("ipo_window"))
            {
                state.SetFlag("ipo_window");
                rep.Events.Add("THE IPO WINDOW IS OPEN — clean covenants, a hundred believers, and a market that's buying. The bell is a journal card while it lasts");
            }
            else if (!openNow && state.HasFlag("ipo_window"))
            {
                state.Flags.Remove("ipo_window");
                _armedKey = "";
                string reason = "the numbers slipped";
                if (state.MacroSeason == "winter") reason = "winter came";
                else if (strikes > 0) reason = "the board's strikes";
                rep.Lines.Add("the IPO window closed — " + reason);
            }
        }

        /// <summary>What the bell would price the company at. Computed at the
        /// signature, never stored: an IPO pop is a market condition on the day.</summary>
        public static int IpoPrice(GameState state)
        {
            return Gd.ToInt(SimEngine.Valuation(state) * (state.MacroSeason == "boom" ? 1.35 : 1.1));
        }

        // ═════════ section 7 THE JOURNAL SEAMS — draw, then sign ════════════
        /// <summary>
        /// SEAM: a journal offer block mirroring the term-sheet idiom. Null = no
        /// card. The journal draws the title and the cards and routes every tap
        /// back through JournalPick; consequences live HERE.
        ///
        /// One block, in priority order — an exit clock outranks a bell, and a
        /// bell outranks taking money off the table.
        /// </summary>
        public static JObject JournalOffer(GameState state)
        {
            if (state.ExitValue > 0 || state.Dead) return null;
            if (state.Mna != null)
            {
                int price = state.Mna.Price;
                int slice = Gd.ToInt(price * state.FounderPct / 100.0);
                int left = Gd.Maxi(state.Mna.ExpiresWeek - state.Week, 0);
                bool armed = _armedKey == ArmKey(state, "mna:accept:0");
                string tag = (state.Mna.Buyer ?? "a buyer").Split(' ')[0];
                if (tag.Length > 9) tag = tag.Substring(0, 9);
                return Block(string.Format(
                    "SOMEONE WANTS TO BUY THE COMPANY: ${0} all-in · your {1}% = ${2} · the no-shop ends in {3} wk — or write anything else and let it lapse. Selling ends the run, so the card takes two taps.",
                    Money(price), Gd.ToInt(state.FounderPct), Money(slice), left),
                    "mna:accept:0",
                    armed ? "SELL — tap again" : tag + "  " + MoneyShort(price));
            }
            if (state.HasFlag("ipo_window"))
            {
                int bell = IpoPrice(state);
                int bslice = Gd.ToInt(bell * state.FounderPct / 100.0);
                bool barmed = _armedKey == ArmKey(state, "ipo:accept:0");
                return Block(string.Format(
                    "THE BELL IS THERE TO RING: an IPO prices the company at ${0} — your {1}% = ${2}. Windows close. Ringing it ends the run, so the card takes two taps.",
                    Money(bell), Gd.ToInt(state.FounderPct), Money(bslice)),
                    "ipo:accept:0",
                    barmed ? "RING IT — tap again" : "RING THE BELL  " + MoneyShort(bell));
            }
            int bank = SecondaryBank(state);
            if (bank > 0)
            {
                return Block(string.Format(
                    "THE BOARD WILL LET YOU TAKE SOME OFF THE TABLE: sell 5 points of YOUR OWN stake at a 15% discount to the round price — ${0} banked, yours whatever happens to the company.",
                    Money(bank)),
                    "sec:0", "secondary " + MoneyShort(bank));
            }
            return null;
        }

        /// <summary>The signature. Returns the receipt the journal logs; "" is
        /// ignored entirely, which is what makes the first tap of a two-tap arm
        /// silent and harmless.</summary>
        public static string JournalPick(GameState state, string id)
        {
            if (state.ExitValue > 0 || state.Dead) return "";
            if (id == "mna:accept:0")
            {
                if (state.Mna == null) return "";
                string key = ArmKey(state, id);
                if (_armedKey != key)
                {
                    _armedKey = key;             // tap one arms; the caption re-reads
                    return "";
                }
                MnaOffer mo = state.Mna;
                state.ExitValue = mo.Price;
                state.SetFlag("acquired_exit");
                // A fire sale keeps its style multipliers (DECISIONS.md) — only
                // the name of the chip changes, because "SOLD AT THE TOP" is a
                // lie at 0.4x standalone. The finale reads this flag.
                if (mo.Why == "lifeline") state.SetFlag("soft_landing");
                state.Mna = null;
                _armedKey = "";
                return string.Format("SOLD to {0} for ${1} ({2}) — your {3}% pays ${4}",
                    mo.Buyer, Money(state.ExitValue), mo.Why, Gd.ToInt(state.FounderPct),
                    Money(Gd.ToInt(state.ExitValue * state.FounderPct / 100.0)));
            }
            if (id == "ipo:accept:0")
            {
                if (!state.HasFlag("ipo_window")) return "";
                string ikey = ArmKey(state, id);
                if (_armedKey != ikey)
                {
                    _armedKey = ikey;
                    return "";
                }
                state.ExitValue = IpoPrice(state);
                state.Flags.Remove("ipo_window");
                _armedKey = "";
                return string.Format("FILED. Priced at ${0} — your {1}% pays ${2}",
                    Money(state.ExitValue), Gd.ToInt(state.FounderPct),
                    Money(Gd.ToInt(state.ExitValue * state.FounderPct / 100.0)));
            }
            if (id == "sec:0")
            {
                // NOT armed: a secondary is expensive, not irreversible, and the
                // price is printed on the card before the tap (2.9's own test).
                int bank = SecondaryBank(state);
                if (bank <= 0) return "";
                state.FounderPct = Gd.Maxf(state.FounderPct - 5.0, 1.0);
                state.FounderBanked += bank;
                state.SetFlag(SecondaryFlag(state));
                return string.Format(
                    "SECONDARY: sold 5 points of YOUR OWN stake at a 15% discount to the round price — ${0} banked, yours whatever happens to the company",
                    Money(bank));
            }
            return "";
        }

        /// <summary>What a secondary would bank right now, 0 when the door is
        /// shut. Five points of the company at a 15% discount, because
        /// secondaries price below the primary; the goodwill gate is board
        /// consent, which only trusted founders get, and one per round because
        /// the board signs a share purchase, not a tap.</summary>
        public static int SecondaryBank(GameState state)
        {
            if (BoardStage(state) < 3 || state.Board == null) return 0;
            if (state.Board.Goodwill < 2) return 0;
            if (state.HasFlag(SecondaryFlag(state))) return 0;
            if (state.FounderPct <= 6.0) return 0;
            // ONLY WHILE THE PAPERS ARE OUT. A board consents to a founder sale
            // at the table — the weeks right after a round closes or a review
            // lands — not on a random Tuesday. ReviewWeek is stamped week + 12
            // at both, so this is the window after either, and it keeps the card
            // from squatting on the page.
            if (state.Board.ReviewWeek - state.Week < ReviewCadence - SecondaryWindow) return 0;
            return Gd.Maxi(Gd.ToInt(SimEngine.Valuation(state) * 0.05 * 0.85), 0);
        }

        /// <summary>One secondary per round closed — the board signs a share
        /// purchase, not a tap. A flag, not a board field, so the two engines
        /// carry it identically.</summary>
        public static string SecondaryFlag(GameState state)
        {
            return "secondary_r" + state.RoundsRaised.Count;
        }

        // ═════════ DIRECTIVES — what the DM is told, and told not to ════════
        /// <summary>Sections 12 (board) and 14 (M&amp;A) of the DIRECTIVES block.
        /// The DM gives the boardroom a face and the courtship a dinner; it never
        /// decides an outcome, and the lines say so where the temptation is
        /// strongest.</summary>
        public static List<string> Directives(GameState state)
        {
            var outp = new List<string>();
            if (state.Board != null)
            {
                int due = BoardReviewIn(state);
                int target = state.Board.TargetRevenue;
                int nowRev = state.LastPnl != null ? state.LastPnl.Revenue : 0;
                if (due <= 0)
                    outp.Add(string.Format(
                        "- BOARD REVIEW THIS WEEK: the covenant is ${0}/wk revenue; the company sits at ${1}/wk. The boardroom is part of this week's story.",
                        target, nowRev));
                else if (due == 1)
                    outp.Add(string.Format(
                        "- The board reviews NEXT week: covenant ${0}/wk, now ${1}/wk. The founder can feel it.",
                        target, nowRev));
                if (state.Board.Strikes >= 2)
                    outp.Add("- The board is one missed review from repricing the company. The coach's sessions are on the calendar.");
            }
            if (state.Mna != null)
                outp.Add(string.Format(
                    "- AN ACQUISITION OFFER IS ON THE TABLE: {0} at ${1}, no-shop ends week {2}. Weave the courtship; only the journal card signs — never close or kill the deal yourself.",
                    state.Mna.Buyer, Money(state.Mna.Price), state.Mna.ExpiresWeek));
            if (state.HasFlag("ipo_window"))
                outp.Add("- THE IPO WINDOW IS OPEN. Bankers circle; the bell is the founder's to ring in the journal, never yours.");
            return outp;
        }

        /// <summary>ENGINE SIGNALS (08 section 9) — the two facts the DM needs
        /// stated flatly, so its narration can never contradict the ledger.
        /// Empty string when there is nothing to say.</summary>
        public static string SignalLine(GameState state)
        {
            if (state.Board == null) return "no board — nobody to answer to";
            int nowRev = state.LastPnl != null ? state.LastPnl.Revenue : 0;
            return string.Format(
                "review wk{0} (in {1}): covenant ${2}/wk · now ${3}/wk · strikes {4} · goodwill {5}",
                state.Board.ReviewWeek, BoardReviewIn(state), state.Board.TargetRevenue,
                nowRev, state.Board.Strikes, state.Board.Goodwill);
        }

        public static string MnaLine(GameState state)
        {
            if (state.Mna == null) return "";
            return string.Format("offer on the table: {0} at ${1} — no-shop ends wk{2}",
                state.Mna.Buyer, state.Mna.Price, state.Mna.ExpiresWeek);
        }

        /// <summary>
        /// ATTENTION ROWS (00-spine section 4) — the single list behind every
        /// bang, the garage ticker and the pre-roll review. Every row here is a
        /// time-boxed cap-table decision: a clock the founder must not roll past.
        /// Labels are 40 characters or less because the ticker prints them.
        /// </summary>
        public static List<AttentionItem> Attention(GameState state)
        {
            var rows = new List<AttentionItem>();
            if (state.Mna != null)
                rows.Add(Row("mna_offer", 3, "offer on the table — no-shop wk " + state.Mna.ExpiresWeek));
            if (state.HasFlag("ipo_window") && state.Mna == null)
                rows.Add(Row("ipo_window", 2, "the IPO window is open — windows close"));
            if (state.Board != null)
            {
                int due = BoardReviewIn(state);
                // The ticker prints this verbatim in 40 characters, so the bar
                // is written short: a comma train would cost it its words.
                if (due <= 1)
                    rows.Add(Row("board_review", 2, string.Format("board review {0} — bar {1}/wk",
                        due <= 0 ? "this week" : "next week", MoneyShort(state.Board.TargetRevenue))));
                if (state.Board.Strikes >= 2)
                    rows.Add(Row("board_strikes", 3,
                        "strike " + state.Board.Strikes + " — a reprice is the next rung"));
            }
            if (SecondaryBank(state) > 0)
                rows.Add(Row("secondary", 1, "the board will let you take some off"));
            return rows;
        }

        // ───────────────────────────── small hands ──────────────────────────
        static AttentionItem Row(string key, int severity, string label)
        {
            return new AttentionItem
            {
                Desk = "cap table",
                Key = key,
                Severity = severity,
                Label = label.Length > 40 ? label.Substring(0, 40) : label,
            };
        }

        static JObject Block(string title, string id, string text)
        {
            return new JObject
            {
                ["title"] = title,
                ["cards"] = new JArray { new JObject { ["id"] = id, ["text"] = text } },
            };
        }

        /// <summary>The lane's money hand, so a receipt, a card and the desk read
        /// the same number the same way.</summary>
        public static string Money(int n)
        {
            string s = Gd.Absi(n).ToString();
            string outp = "";
            while (s.Length > 3)
            {
                outp = "," + s.Substring(s.Length - 3) + outp;
                s = s.Substring(0, s.Length - 3);
            }
            return (n < 0 ? "-" : "") + s + outp;
        }

        /// <summary>A card caption has room for four characters and a unit,
        /// never a comma train.</summary>
        public static string MoneyShort(int n)
        {
            double v = Gd.Absf(n);
            string sign = n < 0 ? "-" : "";
            if (v >= 1000000000.0) return sign + "$" + Gd.F(v / 1000000000.0, 1) + "B";
            if (v >= 1000000.0) return sign + "$" + Gd.F(v / 1000000.0, 1) + "M";
            if (v >= 1000.0) return sign + "$" + Gd.F(v / 1000.0, 0) + "k";
            return sign + "$" + Gd.ToInt(v);
        }

        /// <summary>THIS run, THIS week, THIS card. The instance identity (not
        /// the seed) is what makes a replayed seed unable to inherit an arm: a
        /// new run is a new object.</summary>
        static string ArmKey(GameState state, string id)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(state)
                + ":" + state.Week + ":" + id;
        }

        static Rival StrongestRival(GameState state)
        {
            Rival best = null;
            foreach (Rival rv in state.Rivals)
            {
                if (best == null || rv.Strength > best.Strength) best = rv;
            }
            return best;
        }

        /// <summary>The rival the street says is asking about your price
        /// (03 section 5.7's handoff).</summary>
        static Rival SniffingRival(GameState state)
        {
            if (!state.HasFlag("acquisition_sniff")) return null;
            foreach (Rival rv in state.Rivals)
            {
                if (rv.Sniffing > 0) return rv;
            }
            return null;
        }

        static void ClearSniff(GameState state)
        {
            foreach (Rival rv in state.Rivals) rv.Sniffing = 0;
            state.Flags.Remove("acquisition_sniff");
        }

        static void LapseSniff(GameState state)
        {
            if (!state.HasFlag("acquisition_sniff")) return;
            bool live = false;
            foreach (Rival rv in state.Rivals)
            {
                if (rv.Sniffing <= 0) continue;
                if (state.Week - rv.Sniffing >= SniffLapse) rv.Sniffing = 0;
                else live = true;
            }
            if (!live) state.Flags.Remove("acquisition_sniff");
        }

        static string BuyerOr(Rival rival, double floorStrength, string fallback)
        {
            if (rival != null && rival.Strength >= floorStrength) return rival.Name ?? fallback;
            return fallback;
        }
    }
}
