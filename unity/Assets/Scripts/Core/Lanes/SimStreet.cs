using System;
using System.Collections.Generic;
using System.Globalization;

namespace Runway.Core
{
    /// <summary>
    /// LANE 03 — THE STREET (rivals + macro weather). Spec: docs/design/03-rivals-macro.md
    ///
    /// Two systems share one file because they share one page and one lesson:
    /// the world outside the company acts on it every single week, and none of
    /// it is about you personally.
    ///
    ///   THE RIVALS act on capacity, strategy and relative position — a war
    ///   chest (Vigor), a strategic bent (Focus), a price they hold against the
    ///   street (PricePosture) and a share of voice (Hype). They do not roll a
    ///   ratchet upward; they SPEND to move, and every move is a named business
    ///   dynamic with a receipt that says which one.
    ///
    ///   THE MACRO is the weather. A season cycle the trend mean-reverts around,
    ///   and rare credit shocks that reprice every valuation and term sheet at
    ///   once — announced one week early, because in the real world sentiment
    ///   turns before the money does.
    ///
    /// The spine calls, in tick order (00-spine section 1, HOOKS.md):
    ///   TickPre    tick 6a/6b — rivals act, then the weather turns
    ///   TickMoney  the money section — this lane owns NO P&amp;L lane (see below)
    ///   TickPost   after the week's record is written and can be read back
    /// and outside the tick: Directives feeds the DM block, Attention feeds
    /// every bang in the game through SimEngine.AttentionItems.
    ///
    /// SALTS (00-spine section 3): 30 weekly action pick, 31 poach roll, 32 hq
    /// disruptor spawn, 80 macro shock. The macro walk RE-DRAWS the frozen
    /// salt-7 stream — same single number, mean-reverted — so owning it shifts
    /// nobody else's dice. Salt 6 is a tombstone.
    ///
    /// TWIN LAW: this file and game/src/core/lanes/sim_street.gd carry the same
    /// logic in the same order. The engines do NOT share PRNG internals, so
    /// parity means same checks and same behaviour, never a byte-equal draw.
    /// </summary>
    public static class SimStreet
    {
        // ── the era attention ladder (section 2.3) ───────────────────────────
        /// <summary>
        /// Competitive response is threshold-triggered in the real world:
        /// incumbents do not answer a challenger nobody has heard of. Macro
        /// ignores the ladder — the credit cycle prices a garage exactly as
        /// gladly as an incumbent.
        /// </summary>
        public static readonly string[] ERAS = { "garage", "coworking", "office", "floor", "hq" };

        /// <summary>THE FIXED SCAN ORDER (section 2.4). The cumulative scan walks this
        /// list, so the order is part of the determinism contract: reordering it
        /// re-rolls history.</summary>
        public static readonly string[] ACTIONS =
            { "price_cut", "launch", "blitz", "poach", "stumble", "sniff", "quiet" };

        /// <summary>Response lags, in weeks — a real price move takes a quarter to
        /// answer and poaching runs recruiting cycles. Also the anti-spam floor.</summary>
        public static readonly Dictionary<string, int> COOLDOWNS = new Dictionary<string, int>
        {
            { "price_cut", 4 }, { "launch", 5 }, { "blitz", 3 }, { "poach", 6 },
            { "stumble", 8 }, { "sniff", 12 }, { "quiet", 0 },
        };

        /// <summary>What the street tab's action log calls each move.</summary>
        public static readonly Dictionary<string, string> LABELS = new Dictionary<string, string>
        {
            { "price_cut", "cut prices" }, { "launch", "launched" }, { "blitz", "ad blitz" },
            { "poach", "poach attempt" }, { "stumble", "stumbled" },
            { "sniff", "asking about you" }, { "quiet", "quiet" },
        };

        public const int LogCap = 6;        // the rival's own rap sheet, oldest dropped
        public const int BeatsCap = 4;      // the DM never gets more than four facts a week
        public const int RivalCap = 3;      // the street holds three names, no more
        public const int BornRivals = 2;    // what worldgen births — fewer means a slot came free
        public const string MoneySecret = "quietly running out of money";

        // ── the authored one-liner pools (section 10) ────────────────────────
        /// <summary>
        /// Receipts teach the dynamic BY NAME — price war, share of voice,
        /// execution risk — in the game's dry voice, at zero tokens. The pick is
        /// the salt-30 d2 draw, so the same week always tells the same story.
        /// </summary>
        public static readonly Dictionary<string, string[]> LINES = new Dictionary<string, string[]>
        {
            { "price_cut", new[] {
                "{0} cut their price. The street noticed. — a price war buys share with margin",
                "{0} went cheaper. The going rate just followed them down.",
                "{0} discounted hard. Margin compression is now everyone's problem.",
                "{0} put a sale sign in the window. Your list price reads expensive today." } },
            { "launch", new[] {
                "{0} shipped. It's good. Your product got older overnight.",
                "{0} launched the thing they teased. Buyers are comparing notes.",
                "{0} cut a ribbon on a real feature. The category ladder just moved.",
                "{0} shipped loud. Relative quality is the only quality the street sees." } },
            { "blitz", new[] {
                "{0} is everywhere this week. Attention is a zero-sum street.",
                "{0} bought the billboard, the podcast, and probably your ad slot.",
                "{0} is outspending you on being seen. Share of voice buys share of market.",
                "{0} made noise. Your quiet got quieter." } },
            { "poach_win", new[] {
                "{0} called {1} with a number. The number won.",
                "{0} hired {1} away. Underpaying is a bet somebody else collects.",
                "{0} made {1} an offer the payroll sheet couldn't answer." } },
            { "poach_lose", new[] {
                "{0} called {1} with a number. {1} stayed — this time.",
                "{0} went fishing in your team. Nobody bit. The bait will get bigger.",
                "{0} tested a loyalty you haven't been paying for." } },
            { "stumble", new[] {
                "{0} had a very public bad week. Their churn is your word of mouth.",
                "{0} broke something customers loved. Doors are open.",
                "{0} made the news for the wrong reason. Execution risk collects.",
                "{0} stumbled. Overextension always invoices eventually." } },
            { "sniff", new[] {
                "somebody at {0} keeps asking what you'd cost.",
                "{0}'s corp-dev person knows your numbers a little too well.",
                "a banker mentioned {0} and your name in one sentence." } },
            { "disruptor", new[] {
                "a new name, {0}, is doing what you do for less. You remember this trick.",
                "{0} just launched under your price umbrella. You built that umbrella.",
                "{0} is scrappy, cheap, and pointed at your cheapest customers first." } },
        };

        /// <summary>THE MACRO BANNER, one authored line per weather state (section 10).
        /// The desk prints it, the journal prints it, and they are the same
        /// sentence on purpose.</summary>
        public static readonly Dictionary<string, string> BANNER = new Dictionary<string, string>
        {
            { "winter_watch", "the street smells winter. money gets cold next week" },
            { "funding_winter", "FUNDING WINTER — checks shrink, terms bite" },
            { "thaw", "the thaw. the street funds again" },
            { "boom_watch", "the street smells a boom. money warms next week" },
            { "boom", "BOOM — everyone's a genius, every round oversubscribed" },
            { "boom_end", "the boom cooled. everyone pretends they called it" },
        };

        /// <summary>THE DM's FACTS (section 9). Engine-formatted, so the narrator never
        /// sees a number it could change. Priority when the week is loud: macro,
        /// sniff, poach, launch, stumble, then the disruptor's arrival.</summary>
        private const int PMacro = 0, PSniff = 1, PPoach = 2, PLaunch = 3, PStumble = 4, PDisruptor = 5;

        private sealed class Beat
        {
            public int P;
            public string Text = "";
        }

        // ═══════════════════════ TICK 6a + 6b ════════════════════════════════
        /// <summary>
        /// The street's whole week, in the spine's order: per-rival upkeep, the
        /// weekly action pick (salt 30), the poach (31), the hq disruptor (32),
        /// then the shock roll (80), the watch-to-shock transitions, and the
        /// mean-reverting trend walk (7).
        ///
        /// RIVALS ACT BEFORE THE MARKET, and that is the point: their triggers
        /// read LAST week's player state (the price you posted, the hype you had
        /// when the week opened) while their effects land on THIS week's
        /// adoption. Conduct responds with a lag; consequences are immediate on
        /// announcement.
        /// </summary>
        public static void TickPre(GameState state, WeeklyReport rep)
        {
            var beats = new List<Beat>();
            var moves = new List<string>();
            RivalsWeek(state, rep, beats, moves);
            string alert = MacroWeek(state, rep, beats);
            if (alert.Length == 0)
            {
                alert = RivalAlert(beats);
            }
            // priority, then the order they happened — a stable sort, so two runs
            // of the same week can never disagree about which four facts land
            var order = new List<int>();
            for (int i = 0; i < beats.Count; i++) { order.Add(i); }
            order.Sort(delegate (int a, int c)
            {
                if (beats[a].P != beats[c].P) { return beats[a].P.CompareTo(beats[c].P); }
                return a.CompareTo(c);
            });
            var lines = new List<string>();
            for (int i = 0; i < order.Count && lines.Count < BeatsCap; i++)
            {
                lines.Add(beats[order[i]].Text);
            }
            state.SetMeta("street_beats", lines);
            state.SetMeta("street_moves", moves);
            state.SetMeta("street_alert", alert);
        }

        /// <summary>
        /// THE MONEY SECTION — and this lane writes NOTHING to it, deliberately.
        /// Rival and macro effects are demand-side and funding-side: they surface
        /// as statuses, valuations and term-sheet math. Inventing a P&amp;L lane for
        /// someone else's price cut would be fake accounting. What it DOES leave
        /// here is the receipt that explains the dent, sitting beside the numbers
        /// it dented.
        /// </summary>
        public static void TickMoney(GameState state, WeeklyReport rep, MoneyWork m)
        {
            if (!SimEngine.HasStatus(state, "price_war"))
            {
                return;
            }
            int down = Gd.RoundToInt((1.0 - SimEngine.StreetFairMult(state)) * 100.0);
            rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                "price war on the street: the going rate is down {0}% ({1} wks left)",
                down, WeeksLeft(state, "price_war")));
        }

        /// <summary>Nothing needs the closed week. The street's bookkeeping all happened in 6a.</summary>
        public static void TickPost(GameState state, WeeklyReport rep)
        {
        }

        /// <summary>
        /// DM CONTEXT, sections 7 (rivals) and 8 (macro) of the DIRECTIVES block.
        /// The big beats are already engine-resolved facts; the move lines cover
        /// the two kinds of conduct too small to earn a beat (a price cut, an ad
        /// blitz) but too real for the narrator to contradict.
        /// </summary>
        public static List<string> Directives(GameState state)
        {
            var outp = new List<string>();
            outp.AddRange(MetaLines(state, "street_moves"));
            outp.AddRange(MetaLines(state, "street_beats"));
            return outp;
        }

        /// <summary>
        /// ATTENTION ROWS (00-spine section 4) — the single list behind every
        /// bang. One row for the week the street moved, and the two standing
        /// threats a founder must be able to see while they run: a live price
        /// war, and someone circling. Labels are 40 characters or less because
        /// the garage ticker prints them.
        /// </summary>
        public static List<AttentionItem> Attention(GameState state)
        {
            var rows = new List<AttentionItem>();
            string alert = state.GetMeta("street_alert", "") as string ?? "";
            if (alert.Length > 0 && MetaLines(state, "street_beats").Count > 0)
            {
                rows.Add(new AttentionItem { Desk = "the street", Key = "street_beat",
                    Severity = 2, Label = Gd.Left(alert, 40) });
            }
            if (SimEngine.HasStatus(state, "price_war"))
            {
                int down = Gd.RoundToInt((1.0 - SimEngine.StreetFairMult(state)) * 100.0);
                rows.Add(new AttentionItem { Desk = "threats", Key = "price_war", Severity = 2,
                    Label = Gd.Left(string.Format(CultureInfo.InvariantCulture,
                        "price war: going rate −{0}%, {1} wks left", down,
                        WeeksLeft(state, "price_war")), 40) });
            }
            if (state.HasFlag("acquisition_sniff"))
            {
                string who = "";
                foreach (Rival rv in state.Rivals)
                {
                    if (rv.Sniffing > 0) { who = rv.Name ?? ""; break; }
                }
                if (who.Length > 0)
                {
                    rows.Add(new AttentionItem { Desk = "threats", Key = "acquisition_sniff",
                        Severity = 2, Label = Gd.Left(string.Format(CultureInfo.InvariantCulture,
                            "{0} is circling — asking your price", who), 40) });
                }
            }
            return rows;
        }

        // ═══════════════════════ 6a THE RIVALS ═══════════════════════════════
        private static void RivalsWeek(GameState state, WeeklyReport rep, List<Beat> beats,
                                       List<string> moves)
        {
            int lvl = StreetLevel(state);
            double power = PlayerPower(state);
            bool greedy = OffersOverpriced(state);
            Dictionary<string, object> target = LaborPoachTarget(state);
            Rng r30 = SimEngine.RngForSalt(state, SimEngine.SALT_RIVAL_ACTION);
            // TWO DRAWS PER RIVAL, ALWAYS, IN ARRAY ORDER. A fixed draw count is
            // what stops one rival's branch from shifting the next rival's dice —
            // the single most fragile invariant in this file.
            foreach (Rival rd in state.Rivals)
            {
                Upkeep(rd);
                double d1 = r30.Randf();
                int d2 = (int)(r30.Randi() >> 1);
                string act = Pick(state, rd, lvl, power, greedy, target, d1);
                Fire(state, rep, beats, moves, rd, act, lvl, d2, target);
            }
            Disruptor(state, rep, beats, lvl);
        }

        /// <summary>
        /// PER-RIVAL UPKEEP — deterministic, no draws, every week, every era.
        /// Firms grow on cash and attention, not dice: this replaces the old
        /// random ratchet with state-driven drift across the same band. Buzz
        /// decays like adstock, reserves mean-revert, and discounts erode back
        /// toward list price once a war ends (the airline fare-war pattern).
        ///
        /// The order below IS the spec's order: strength reads the vigor and hype
        /// the rival went to bed with, before this week's decay and reversion.
        /// </summary>
        private static void Upkeep(Rival rd)
        {
            var keys = new List<string>(rd.Cooldowns.Keys);
            foreach (string k in keys)
            {
                rd.Cooldowns[k] = Gd.Maxi(rd.Cooldowns[k] - 1, 0);
            }
            double vigor = rd.Vigor;
            double hype = rd.Hype;
            rd.Strength = Gd.Clampf(rd.Strength + Gd.Clampf((vigor - 45.0) / 50.0, -0.5, 0.7)
                + 0.005 * hype, 5.0, 95.0);
            rd.Hype = Gd.Maxf(hype - 4.0, 0.0);
            rd.Vigor = Gd.Clampf(vigor + (55.0 - vigor) / 12.0, 0.0, 100.0);
            rd.PricePosture = rd.PricePosture + Gd.Clampf(1.0 - rd.PricePosture, -0.01, 0.01);
        }

        /// <summary>
        /// THE GAP: how far they outmatch you. Product is half the answer, buzz a
        /// quarter, market share a quarter — and share is normalised against TAM
        /// (2% of the market is full marks) so an Enterprise run's 30 logos weigh
        /// the same as a Consumer run's 18,000 users.
        /// </summary>
        public static double PlayerPower(GameState state)
        {
            double tam = Gd.Maxf(state.Theta != null ? state.Theta.Tam : 50000.0, 1.0);
            return Gd.Clampf(0.5 * state.Product + 0.25 * state.Hype
                + 25.0 * Gd.Clampf(state.Traction / (0.02 * tam), 0.0, 1.0), 5.0, 95.0);
        }

        /// <summary>Where the founder sits on the attention ladder: 0 garage to 4 hq.</summary>
        public static int StreetLevel(GameState state)
        {
            for (int i = 0; i < ERAS.Length; i++)
            {
                if (ERAS[i] == state.Era) { return i; }
            }
            return 0;
        }

        /// <summary>
        /// GREED INVITES UNDERCUTTING — the price umbrella. Pricing 15% or more
        /// above the street's reference is an open invitation to enter beneath you.
        /// </summary>
        public static bool OffersOverpriced(GameState state)
        {
            foreach (Offer o in state.Offers)
            {
                if (o.Price > 0.0 && o.FairPrice > 0.0 && o.Price >= 1.15 * o.FairPrice)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// THE WEEKLY ACTION TABLE (section 2.4): eligibility first (a failed gate
        /// is weight zero, never a re-roll), then conjectural-variation weights —
        /// firms act on capacity, strategy and relative position — then ONE
        /// cumulative scan over d1 x sum(w) in the fixed order. A trailing rival
        /// (gap &lt;= 0) ships and poaches harder: that is catch-up behaviour, and
        /// it is why falling behind is loud.
        /// </summary>
        private static string Pick(GameState state, Rival rd, int lvl, double power,
                                   bool greedy, Dictionary<string, object> target, double d1)
        {
            double vigor = rd.Vigor;
            double hype = rd.Hype;
            double strength = rd.Strength;
            double posture = rd.PricePosture;
            string focus = rd.Focus ?? "growth";
            double gap = strength - power;
            var w = new Dictionary<string, double>();
            w["price_cut"] = 0.0;
            if (lvl >= 2 && vigor >= 25.0 && posture > 0.82 && Cd(rd, "price_cut") == 0)
            {
                w["price_cut"] = 8.0 * (focus == "price" ? 2.0 : 1.0)
                    * (posture <= 0.90 ? 0.5 : 1.0) + (greedy ? 6.0 : 0.0);
            }
            w["launch"] = 0.0;
            if (vigor >= 30.0 && Cd(rd, "launch") == 0)
            {
                w["launch"] = (10.0 * (focus == "product" ? 2.0 : 1.0) + (gap <= 0.0 ? 4.0 : 0.0))
                    * (lvl >= 3 ? 1.5 : 1.0);
            }
            w["blitz"] = 0.0;
            if (vigor >= 30.0 && Cd(rd, "blitz") == 0)
            {
                w["blitz"] = (8.0 * (focus == "growth" ? 2.0 : 1.0)
                    + (state.Hype >= hype + 15.0 ? 4.0 : 0.0)) * (lvl >= 3 ? 1.5 : 1.0);
            }
            w["poach"] = 0.0;
            if (lvl >= 2 && vigor >= 40.0 && target != null && target.Count > 0 && Cd(rd, "poach") == 0)
            {
                w["poach"] = 4.0 + (gap <= 0.0 ? 4.0 : 0.0) + (focus == "product" ? 2.0 : 0.0);
            }
            w["stumble"] = 0.0;
            if (Cd(rd, "stumble") == 0)
            {
                w["stumble"] = 4.0 + (vigor < 30.0 ? 6.0 : 0.0) + (hype >= 70.0 ? 4.0 : 0.0)
                    + (rd.Secret == MoneySecret ? 6.0 : 0.0);
            }
            w["sniff"] = 0.0;
            if (lvl >= 3 && strength >= 60.0 && gap >= 10.0 && power >= 35.0
                && rd.Sniffing == 0 && Cd(rd, "sniff") == 0)
            {
                w["sniff"] = 2.0;
            }
            w["quiet"] = 30.0 + (vigor < 25.0 ? 15.0 : 0.0);
            double total = 0.0;
            for (int i = 0; i < ACTIONS.Length; i++) { total += w[ACTIONS[i]]; }
            double roll = d1 * total;
            double acc = 0.0;
            for (int i = 0; i < ACTIONS.Length; i++)
            {
                acc += w[ACTIONS[i]];
                if (roll < acc) { return ACTIONS[i]; }
            }
            return "quiet";
        }

        private static int Cd(Rival rd, string act)
        {
            int v;
            return rd.Cooldowns.TryGetValue(act, out v) ? v : 0;
        }

        /// <summary>
        /// What the move actually costs them, does to you, and reads like.
        /// Player-facing installs are gated by the ladder: below coworking the
        /// street plays among itself and the founder is simply not worth
        /// answering (section 2.3).
        /// </summary>
        private static void Fire(GameState state, WeeklyReport rep, List<Beat> beats,
                                 List<string> moves, Rival rd, string act, int lvl, int d2,
                                 Dictionary<string, object> target)
        {
            string name = rd.Name ?? "a rival";
            string focus = rd.Focus ?? "growth";
            bool seen = lvl >= 1;
            switch (act)
            {
                case "price_cut":
                    // BERTRAND UNDERCUTTING: the reference price itself erodes, so
                    // holding your list price through a war is what reads expensive.
                    rd.PricePosture = Gd.Maxf(rd.PricePosture - 0.06, 0.80);
                    rd.Vigor = rd.Vigor - 8.0;
                    SimEngine.AddStatus(state, "price_war", focus == "price" ? 5 : 4);
                    if (seen)
                    {
                        rep.Events.Add(Pool("price_cut", d2, name));
                        moves.Add(string.Format(CultureInfo.InvariantCulture,
                            "{0} cut prices ~{1}% this week.", name,
                            Gd.RoundToInt((1.0 - rd.PricePosture) * 100.0)));
                    }
                    break;
                case "launch":
                    // VERTICAL DIFFERENTIATION: their step up is your relative step
                    // down. Your product meter is untouched — you lost no code,
                    // only appeal.
                    rd.Strength = Gd.Minf(rd.Strength + 4.0, 95.0);
                    rd.Hype = Gd.Minf(rd.Hype + 15.0, 100.0);
                    rd.Vigor = rd.Vigor - 12.0;
                    if (seen)
                    {
                        SimEngine.AddStatus(state, "outshipped", 3);
                        rep.Events.Add(Pool("launch", d2, name));
                        beats.Add(new Beat { P = PLaunch, Text = string.Format(CultureInfo.InvariantCulture,
                            "THE STREET: {0} launched for real this week (strength {1}). Customers are comparing.",
                            name, Gd.RoundToInt(rd.Strength)) });
                    }
                    break;
                case "blitz":
                    // SHARE OF VOICE BUYS SHARE OF MARKET: attention is zero-sum,
                    // and the status is the decaying adstock of their week of noise.
                    rd.Hype = Gd.Minf(rd.Hype + 20.0, 100.0);
                    rd.Vigor = rd.Vigor - 15.0;
                    if (seen)
                    {
                        SimEngine.AddStatus(state, "rival_fud", 2);
                        rep.Events.Add(Pool("blitz", d2, name));
                        moves.Add(string.Format(CultureInfo.InvariantCulture,
                            "{0} is buying every ad slot this week.", name));
                    }
                    break;
                case "poach":
                    ResolvePoachInner(state, rep, beats, rd, d2, target);
                    break;
                case "stumble":
                    // EXECUTION RISK CORRELATES WITH OVEREXTENSION: the loud, thin
                    // ones break loudest, and the worldgen secret finally pays off.
                    bool broke = rd.Secret == MoneySecret;
                    rd.Strength = Gd.Maxf(rd.Strength - (broke ? 12.0 : 6.0), 5.0);
                    rd.Vigor = Gd.Maxf(rd.Vigor - (broke ? 20.0 : 10.0), 0.0);
                    rd.Hype = rd.Hype * 0.5;
                    if (seen)
                    {
                        SimEngine.AddStatus(state, "rival_stumbled", 2);
                        rep.Events.Add(Pool("stumble", d2, name));
                        beats.Add(new Beat { P = PStumble, Text = string.Format(CultureInfo.InvariantCulture,
                            "THE STREET: {0} stumbled publicly — their customers are looking around. A door is open this week.",
                            name) });
                    }
                    break;
                case "sniff":
                    // M&A HANDOFF ONLY. This lane prices nothing and spawns no
                    // offer: it marks the interest and lets it charge the room
                    // until 08 courts it.
                    rd.Sniffing = state.Week;
                    state.SetFlag("acquisition_sniff");
                    if (seen)
                    {
                        rep.Events.Add(Pool("sniff", d2, name));
                        beats.Add(new Beat { P = PSniff, Text = string.Format(CultureInfo.InvariantCulture,
                            "THE STREET: quiet word is {0} is asking around about acquiring the company. Do not resolve it — let it charge the room.",
                            name) });
                    }
                    break;
                default:
                    // CONSOLIDATION: they bank cash and say nothing. Silence in the
                    // log is information too — a quiet rival is a rival reloading.
                    rd.Vigor = Gd.Minf(rd.Vigor + 6.0, 100.0);
                    break;
            }
            rd.Vigor = Gd.Clampf(rd.Vigor, 0.0, 100.0);
            rd.LastAction = act;
            if (act != "quiet") { rd.WeeksSinceMove = 0; }
            int cd;
            if (!COOLDOWNS.TryGetValue(act, out cd)) { cd = 0; }
            if (act == "price_cut" && focus == "price") { cd = 3; }
            if (cd > 0) { rd.Cooldowns[act] = cd; }
            string label;
            if (!LABELS.TryGetValue(act, out label)) { label = act; }
            rd.Log.Add(string.Format(CultureInfo.InvariantCulture, "wk{0}: {1}", state.Week, label));
            while (rd.Log.Count > LogCap) { rd.Log.RemoveAt(0); }
        }

        private static string Pool(string key, int d2, string a)
        {
            string[] p = LINES[key];
            return string.Format(CultureInfo.InvariantCulture, p[d2 % p.Length], a);
        }

        /// <summary>
        /// PAY-GAP ARBITRAGE (section 5.4). Underpaid people answer recruiter
        /// calls; the target and the wage come from the labor lane, never from
        /// here. The attempt costs the rival whether or not it lands — recruiting
        /// is not free.
        ///
        /// Public so a suite can hand it a stubbed target and pin the whole
        /// resolution without needing a live labor desk. True = the person left.
        /// </summary>
        public static bool ResolvePoach(GameState state, WeeklyReport rep, List<string> beatTexts,
                                        Rival rd, int d2, Dictionary<string, object> target)
        {
            var beats = new List<Beat>();
            bool won = ResolvePoachInner(state, rep, beats, rd, d2, target);
            if (beatTexts != null)
            {
                foreach (Beat b in beats) { beatTexts.Add(b.Text); }
            }
            return won;
        }

        private static bool ResolvePoachInner(GameState state, WeeklyReport rep, List<Beat> beats,
                                              Rival rd, int d2, Dictionary<string, object> target)
        {
            string name = rd.Name ?? "a rival";
            double warChest = rd.Vigor;      // the budget they had when they made the call
            rd.Vigor = warChest - 10.0;
            if (target == null || target.Count == 0)
            {
                return false;
            }
            string who = Str(target, "name", "someone");
            double p = PoachOdds(Num(target, "pay_gap", 0.0), warChest);
            bool won = SimEngine.RngForSalt(state, SimEngine.SALT_RIVAL_POACH).Randf() < p;
            // THE HANDOFF (00-spine section 4): the crew desk bangs on this, and a
            // failed attempt is where the labor lane's counter-offer season starts.
            state.SetMeta("poach_wk", state.Week);
            state.SetMeta("poach_name", who);
            if (won)
            {
                // They leave BEFORE this week's GTM head-count and before payroll —
                // the week you lose someone is the week you feel it.
                int i = (int)Num(target, "index", -1.0);
                if (i >= 0 && i < state.Employees.Count) { state.Employees.RemoveAt(i); }
                state.Morale = Gd.Clampi(state.Morale - 6, 0, 100);
                rd.Strength = Gd.Minf(rd.Strength + 2.0, 95.0);
                string[] wp = LINES["poach_win"];
                rep.Events.Add(string.Format(CultureInfo.InvariantCulture, wp[d2 % wp.Length], name, who));
                beats.Add(new Beat { P = PPoach, Text = string.Format(CultureInfo.InvariantCulture,
                    "THE STREET: {0} tried to poach {1} this week — and they left. The team noticed.",
                    name, who) });
                return true;
            }
            // THE WARNING SHOT: the salary conversation is coming whether or not you
            // start it. 02 reads these and raises the ask (counter-offer dynamics).
            state.SetMeta("poach_failed_wk", state.Week);
            state.SetMeta("poach_failed_name", who);
            string[] lp = LINES["poach_lose"];
            rep.Events.Add(string.Format(CultureInfo.InvariantCulture, lp[d2 % lp.Length], name, who));
            beats.Add(new Beat { P = PPoach, Text = string.Format(CultureInfo.InvariantCulture,
                "THE STREET: {0} tried to poach {1} this week — they stayed, this time. The team noticed.",
                name, who) });
            return false;
        }

        /// <summary>
        /// THE ODDS, exact (section 5.4). The curve is anchored at a 15% gap on an
        /// average war chest; a 40% gap with money behind it is better than even.
        /// The 0.70 cap is the lesson: even flush acquirers lose recruiting
        /// battles. `vigor` is the war chest they had when they picked up the phone.
        ///
        /// WHO IS WORTH CALLING is the labor lane's threshold, not this one's —
        /// this only prices whoever it hands over, so the two lanes can move that
        /// bar without touching each other.
        /// </summary>
        public static double PoachOdds(double payGap, double vigor)
        {
            return Gd.Clampf(0.15 + 1.2 * (payGap - 0.15) + 0.003 * (vigor - 50.0), 0.05, 0.70);
        }

        // ── the labor interface ──────────────────────────────────────────────
        /// <summary>
        /// THE POACH TARGET (section 5.4): the labor lane names the most underpaid
        /// person a rival would actually call — index, name, salary,
        /// market_salary, pay_gap — or nothing when there is nobody worth calling.
        /// Empty zeroes the poach weight: no shim, no fake wages, no phantom
        /// employee. This lane never invents a salary.
        /// </summary>
        private static Dictionary<string, object> LaborPoachTarget(GameState state)
        {
            return SimLabor.PoachTarget(state);
        }

        // ── section 6 the disruptor ──────────────────────────────────────────
        /// <summary>
        /// LOW-END DISRUPTION (Christensen): incumbents build the price umbrella
        /// that attackers live under. At hq you ARE the reference price, so a
        /// cheap name appears beneath you. And when the street loses a company —
        /// acquired, dead — the vacuum re-opens the spawn at any era
        /// (docs/design/DECISIONS.md): markets do not stay two-horse races
        /// because you got comfortable.
        /// </summary>
        private static void Disruptor(GameState state, WeeklyReport rep, List<Beat> beats, int lvl)
        {
            bool slotFreed = state.Rivals.Count < BornRivals;
            if (state.Rivals.Count >= RivalCap || !(lvl >= 4 || slotFreed))
            {
                return;
            }
            Rng r32 = SimEngine.RngForSalt(state, SimEngine.SALT_RIVAL_DISRUPTOR);
            if (r32.Randf() >= 0.04)      // ~1 per 25 weeks
            {
                return;
            }
            string name = WorldGen.MakeName(r32);
            state.Rivals.Add(new Rival
            {
                Name = name,
                What = "",
                Strength = 12.0 + r32.RandfRange(0.0, 8.0),
                Tactics = new List<string>(WorldGen.RIVAL_TACTICS[0]),
                WeeksSinceMove = 0,
                Secret = "",
                Vigor = 70.0 + r32.RandfRange(0.0, 20.0),
                Hype = 30.0,
                Focus = "price",
                PricePosture = 0.90,
                LastAction = "",
                Log = new List<string>(),
                Cooldowns = new Dictionary<string, int>(),
                Sniffing = 0,
            });
            string[] dp = LINES["disruptor"];
            rep.Events.Add(string.Format(CultureInfo.InvariantCulture,
                dp[(int)(r32.Randi() % (uint)dp.Length)], name));
            beats.Add(new Beat { P = PDisruptor, Text = string.Format(CultureInfo.InvariantCulture,
                "THE STREET: a new name, {0}, is undercutting from below. Incumbents ignore these at their own funeral.",
                name) });
        }

        // ═══════════════════════ 6b THE MACRO ════════════════════════════════
        /// <summary>
        /// The weather, in the spine's order: the shock roll (salt 80, one draw
        /// ALWAYS), the watch-to-shock transitions, the cooldown tick, and then
        /// the trend walk on the frozen salt-7 stream. Returns the attention label
        /// a macro week deserves, or "" when the sky did nothing.
        ///
        /// Macro runs at EVERY era. The lesson: markets do not care that you
        /// exist, but the economy prices you anyway.
        /// </summary>
        private static string MacroWeek(GameState state, WeeklyReport rep, List<Beat> beats)
        {
            string alert = "";
            Rng r80 = SimEngine.RngForSalt(state, SimEngine.SALT_MACRO_SHOCK);
            double d = r80.Randf();                       // drawn every week, fixed count
            // THE SPACING CLOCK ticks first, and `cool` keeps the value the WEEK
            // opened with. A shock ending below re-arms it to a full 20, and
            // because the roll reads this local rather than the meta, the week a
            // winter thaws can never also be the week the next one is announced.
            int cool = (int)state.GetMetaF("shock_cool", 0.0);
            state.SetMeta("shock_cool", Gd.Maxi(cool - 1, 0));

            // ── the pre-announcement becomes the thing (sentiment precedes term sheets)
            if (rep.Expired.Contains("winter_watch"))
            {
                int dur = r80.RandiRange(6, 10);
                SimEngine.AddStatus(state, "funding_winter", dur);
                rep.Events.Add(BANNER["funding_winter"]);
                beats.Add(new Beat { P = PMacro, Text = string.Format(CultureInfo.InvariantCulture,
                    "MACRO: funding winter, {0} wks left — valuations 0.6x, rounds smaller and meaner. Money scenes are hostile.",
                    dur) });
                alert = "funding winter — raise money later";
            }
            else if (rep.Expired.Contains("boom_watch"))
            {
                int dur = r80.RandiRange(6, 10);
                SimEngine.AddStatus(state, "boom", dur);
                rep.Events.Add(BANNER["boom"]);
                beats.Add(new Beat { P = PMacro, Text = string.Format(CultureInfo.InvariantCulture,
                    "MACRO: boom, {0} wks left — valuations 1.3x, term sheets sweeten. Everyone is a genius this quarter.",
                    dur) });
                alert = "boom — raise while money is warm";
            }
            else if (rep.Expired.Contains("funding_winter"))
            {
                cool = 20;
                state.SetMeta("shock_cool", cool);
                rep.Events.Add(BANNER["thaw"]);
                beats.Add(new Beat { P = PMacro, Text = "MACRO: the thaw — the street funds again." });
                alert = "the thaw — the street funds again";
            }
            else if (rep.Expired.Contains("boom"))
            {
                cool = 20;
                state.SetMeta("shock_cool", cool);
                rep.Events.Add(BANNER["boom_end"]);
                beats.Add(new Beat { P = PMacro, Text = "MACRO: the boom cooled — the street is ordinary again." });
                alert = "the boom cooled — money is ordinary";
            }

            // ── the roll: rare, pre-announced, and spaced by the cooldown after one ends
            if (state.Week >= 8 && cool == 0 && !Weather(state))
            {
                if (d < 0.010)
                {
                    SimEngine.AddStatus(state, "winter_watch", 1);
                    rep.Events.Add(BANNER["winter_watch"]);
                    beats.Add(new Beat { P = PMacro, Text =
                        "MACRO: the street smells a funding winter — from next week valuations compress and term sheets tighten. Investors already talk colder." });
                    alert = "winter watch — money cools next week";
                }
                else if (d < 0.020)
                {
                    SimEngine.AddStatus(state, "boom_watch", 1);
                    rep.Events.Add(BANNER["boom_watch"]);
                    beats.Add(new Beat { P = PMacro, Text =
                        "MACRO: the street smells a boom — from next week money runs warm and careless." });
                    alert = "boom watch — money warms next week";
                }
            }
            else if (alert.Length == 0 && SimEngine.HasStatus(state, "funding_winter"))
            {
                // a live winter is standing weather: the DM must keep talking cold
                beats.Add(new Beat { P = PMacro, Text = string.Format(CultureInfo.InvariantCulture,
                    "MACRO: funding winter, {0} wks left — valuations 0.6x, rounds smaller and meaner. Money scenes are hostile.",
                    WeeksLeft(state, "funding_winter")) });
            }
            else if (alert.Length == 0 && SimEngine.HasStatus(state, "boom"))
            {
                beats.Add(new Beat { P = PMacro, Text = string.Format(CultureInfo.InvariantCulture,
                    "MACRO: boom, {0} wks left — valuations 1.3x, term sheets sweeten. Everyone is a genius this quarter.",
                    WeeksLeft(state, "boom")) });
            }
            state.MacroSeason = Season(state);

            // ── section 7 the trend walk: the SAME single salt-7 draw,
            // mean-reverted around the season cycle. Owning it never shifts
            // another subsystem's dice.
            Rng r7 = SimEngine.RngForSalt(state, SimEngine.SALT_TREND);
            double vol = state.Theta != null ? state.Theta.TrendVol : 0.02;
            state.MarketTrend = Gd.Clampf(state.MarketTrend
                + (CycleTarget(state) - state.MarketTrend) * 0.15
                + r7.RandfRange(-1.0, 1.0) * vol, 0.5, 1.5);
            string band = TrendBand(state.MarketTrend);
            string was = state.GetMeta("season_band", "") as string ?? "";
            if (band != was)
            {
                if (was.Length > 0)
                {
                    rep.Lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "the street turned: {0}", band));
                }
                state.SetMeta("season_band", band);
            }
            return alert;
        }

        /// <summary>
        /// THE SEASON CYCLE (section 7.1) — a business cycle decomposed to one
        /// stylised frequency: a 52-week sine the trend is pulled toward, shifted
        /// by whatever weather is live. Pure function, no storage, no draws:
        /// demand has weather, and the weather is readable a season ahead.
        /// </summary>
        public static double CycleTarget(GameState state)
        {
            int phase = (int)(Math.Abs(state.SimSeed) % 52L);
            double t = 1.0 + 0.12 * Math.Sin(2.0 * Math.PI * (state.Week + phase) / 52.0);
            if (SimEngine.HasStatus(state, "funding_winter") || SimEngine.HasStatus(state, "winter_watch"))
            {
                t -= 0.10;
            }
            if (SimEngine.HasStatus(state, "boom") || SimEngine.HasStatus(state, "boom_watch"))
            {
                t += 0.10;
            }
            return t;
        }

        /// <summary>
        /// The banner's read on the trend. Macro deliberately does NOT install
        /// market_tailwind/market_headwind — the trend already multiplies
        /// adoption, and counting the weather twice would be a lie the receipts
        /// could not explain.
        /// </summary>
        public static string TrendBand(double trend)
        {
            if (trend >= 1.10) { return "tailwinds"; }
            if (trend <= 0.90) { return "headwinds"; }
            return "calm";
        }

        /// <summary>The season with its consequence attached — demand has weather, and
        /// the desk says what this week's weather does to a sale rather than
        /// making you infer it.</summary>
        public static string SeasonRead(double trend)
        {
            switch (TrendBand(trend))
            {
                case "tailwinds": return "tailwinds — the street buys";
                case "headwinds": return "headwinds — wallets closed";
                default: return "calm — no help, no headwind";
            }
        }

        /// <summary>The persisted weather word (state.MacroSeason) — what 08 reads when
        /// it prices an exit, without parsing the status list.</summary>
        public static string Season(GameState state)
        {
            if (SimEngine.HasStatus(state, "funding_winter") || SimEngine.HasStatus(state, "winter_watch"))
            {
                return "winter";
            }
            if (SimEngine.HasStatus(state, "boom") || SimEngine.HasStatus(state, "boom_watch"))
            {
                return "boom";
            }
            return "steady";
        }

        private static bool Weather(GameState state)
        {
            return SimEngine.HasStatus(state, "winter_watch") || SimEngine.HasStatus(state, "boom_watch")
                || SimEngine.HasStatus(state, "funding_winter") || SimEngine.HasStatus(state, "boom");
        }

        // ── the desk's word maps (section 11) ────────────────────────────────
        /// <summary>
        /// NEVER A RAW FLOAT ON THE PAGE. Reading who is flush and who fights on
        /// price IS the counterplay, so the words are the interface and they live
        /// here, once, for both engines' desks.
        /// </summary>
        public static string VigorWord(double v)
        {
            if (v >= 70.0) { return "flush"; }
            if (v >= 45.0) { return "steady"; }
            if (v >= 25.0) { return "tight"; }
            return "bleeding";
        }

        public static string PostureWord(double p)
        {
            if (p <= 0.94) { return "undercutting"; }
            if (p >= 1.06) { return "premium"; }
            return "at market";
        }

        public static string HypeWord(double h)
        {
            if (h >= 60.0) { return "loud"; }
            if (h >= 30.0) { return "buzzing"; }
            return "quiet";
        }

        /// <summary>The four word-reads of a rival, joined — the street tab's second line.</summary>
        public static string PostureLine(Rival rd)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}  ·  {1}  ·  fights on {2}  ·  {3}",
                VigorWord(rd.Vigor), PostureWord(rd.PricePosture), rd.Focus ?? "growth",
                HypeWord(rd.Hype));
        }

        // ── small shared reads ───────────────────────────────────────────────
        /// <summary>How long a status has to run — the desk prints it, the receipts
        /// count it down.</summary>
        public static int WeeksLeft(GameState state, string name)
        {
            foreach (Status s in state.Statuses)
            {
                if (s.Name == name) { return s.WeeksLeft; }
            }
            return 0;
        }

        /// <summary>The transient per-week string metas this lane writes, read back safely.</summary>
        public static List<string> MetaLines(GameState state, string key)
        {
            var got = state.GetMeta(key, null) as List<string>;
            return got ?? new List<string>();
        }

        /// <summary>The 40-character label for a week the rivals moved but the sky did not.</summary>
        private static string RivalAlert(List<Beat> beats)
        {
            int best = 99;
            string label = "";
            foreach (Beat b in beats)
            {
                if (b.P >= best) { continue; }
                string txt = b.Text;
                string got;
                if (txt.Contains("acquiring the company")) { got = "someone is asking what you cost"; }
                else if (txt.Contains("poach")) { got = "a rival is calling your people"; }
                else if (txt.Contains("launched for real")) { got = "a rival shipped — you look older"; }
                else if (txt.Contains("stumbled publicly")) { got = "a rival stumbled — a door is open"; }
                else if (txt.Contains("undercutting from below")) { got = "a cheaper rival just appeared"; }
                else { continue; }
                label = got;
                best = b.P;
            }
            return label;
        }

        private static string Str(Dictionary<string, object> d, string k, string dflt)
        {
            object v;
            if (d.TryGetValue(k, out v) && v != null) { return Convert.ToString(v, CultureInfo.InvariantCulture); }
            return dflt;
        }

        private static double Num(Dictionary<string, object> d, string k, double dflt)
        {
            object v;
            if (d.TryGetValue(k, out v) && v != null)
            {
                try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); }
                catch (Exception) { return dflt; }
            }
            return dflt;
        }

        // ── THE TWO SEAMS THE SPINE LEFT OPEN ────────────────────────────────
        /// <summary>
        /// Both flipped: TickPre now owns 6a and 6b. The legacy salt-6 ratchet is
        /// retired (its number is a tombstone, never reassigned) and the salt-7
        /// walk is re-drawn HERE — same single number, same stream, mean-reverting.
        /// </summary>
        public const bool OwnsRivals = true;
        public const bool OwnsMacro = true;
    }
}
