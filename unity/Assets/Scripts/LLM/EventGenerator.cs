using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Runway.App;

namespace Runway.Llm
{
    /// <summary>
    /// Tier-2 generation + free-move adjudication — event_generator.gd, ported.
    /// Prefetches event cards in the background; adjudicates the player's own written
    /// moves. Both flow through the same op whitelist and clamps — the LLM writes
    /// flavour, never rules.
    ///
    /// Every composer below is the original's, string for string: THE CONTEXT SANDWICH
    /// (world bible -> compacted memory -> recent weeks verbatim -> numeric state +
    /// engine signals -> the dice -> directives) is what makes a week read as a
    /// continuation instead of a fresh prompt, and re-wording any of it is a change to
    /// the game.
    /// </summary>
    public sealed class EventGenerator : MonoBehaviour
    {
        public LlmClient Llm;

        /// The generated cards waiting to be dealt.
        public readonly List<JObject> Pool = new List<JObject>();

        /// daily seeded runs: authored-only determinism
        public bool Disabled;

        bool _pending;
        string _adjudicatePrompt = "";
        string _clarifyPrompt = "";

        public const string SYSTEM_PROMPT = @"You write event cards for RUNWAY!, a satirical startup survival game. Voice: dry, specific, wince-funny. Body 60 words max. Choice labels 8 words max. Never real companies or people. Never break the fourth wall. The title is a PLAIN statement of the situation in at most 7 words — 'The pilot customer wants a discount', never a mood-phrase riddle like 'Inner Calm, Concrete Floor'. If the user message lists PEOPLE ALREADY ON STAGE RECENTLY, none of them may appear in this card. You receive the run state as JSON, including the player's company_name and what it does (company_does) — write events that are SPECIFIC to that business (its customers, its industry's absurdities, its failure modes), and refer to the company by name when natural. Output ONLY a card matching the schema. Effects use ONLY the allowed ops within sane ranges (meter deltas within ±15; cash_delta proportionate to the era in the state — ±2000 garage, ±10k coworking, ±60k office, ±250k floor, ±1M hq). Match the event to the era and its cast: the state carries era_name, staff (named employees with burnout levels), rounds_raised and board — a garage event smells of ramen, an HQ event of lawyers; a Service business bills hours and juggles clients, a Marketplace juggles two sides, Hardware waits on parts; name a staff member when one fits, and never invent people who are not in the state. Choices must be genuine dilemmas — no strictly-correct option. Reference at least one specific item, cofounder, or flag from the state. The state includes recent_actions — the log of what the player actually did each week. USE IT: create continuity and follow-ups. Some weeks, instead of a problem, write an OPPORTUNITY that grows directly out of a recent action (a prospect who saw the marketing post and liked it, a demo attendee who wants an intro, a customer who mentioned them somewhere) — opportunities still carry tradeoffs, never free wins.";

        public const string ADJUDICATE_PROMPT = @"You are the world of RUNWAY!, a satirical startup survival game, adjudicating a founder's free-form action during an event. You receive the full run state — company, business_model (what × who), funding_path, employees, customers, product_version, items owned, cofounders with roles and commitment, archetype competences, meters — then the event and the player's written move. Judge it fairly but the world is harsh, and CONTEXT-AWARE: concrete plans that use things the founder ACTUALLY HAS work better; a bootstrapped company can be scrappy but can't outspend problems; a VC-backed one has money but answers for it; enterprise sales are slow and relationship-driven, consumer needs volume and virality, hardware makes everything slower and costlier; part-time cofounders are less available; more customers means more to lose. Vague, magical, or entitled answers backfire with comedy. narration: 210-290 words in 4-6 short second-person paragraphs — read while the art renders (~70s). PLAIN FIRST: simple declaratives a tired reader follows first pass; at most one wry line per two paragraphs; no riddle headlines. verdict: brilliant / fine / risky / backfired. effects: 1-3 ops from the whitelist, magnitudes proportionate to the era in the state (cash within ±3000 in the garage, scaling up by era; meters within ±15 always). The player makes ONE move per week — your effects carry seven days of work, so a sound grounded plan earns the generous end of the range. MILESTONES: when the written week genuinely constitutes it, set the gating flag via set_flag — first_revenue, launched, pmf, seed_raised, series_a (max one per week; pair a closed round with its cash). THE ROLL: the user message carries d20=N and competences; pick the governing stat, mod=stat-3, judge DC 6-16 by boldness, and narrate what total EARNED (beat by 5+ brilliant / 0+ fine / -1..-2 risky-mixed / -3- backfired); output roll={stat,dc}. Every effect includes ""why"": its concrete in-world cause (<=10 words). Staff named in the state are real: leaning on a cooked employee is risky, and plans ignoring an investor board draw friction. Never more than one strongly positive effect unless the plan is genuinely brilliant AND grounded in what the founder actually has.";

        /// Run-start world generation: everything is ABOUT this company. Deterministic
        /// WorldGen remains the keyless fallback and the shape both paths share.
        public const string WORLDGEN_PROMPT = @"You are the world-builder for RUNWAY!, a satirical startup survival game. Given the company (its pitch, what it sells, to whom), invent THE WORLD IT WAS BORN INTO — specific to this exact business, never generic. market: honest intuitive numbers (how many real buyers exist for THIS product; how many weeks such a customer stays before churning) and a dry one-liner about this market's mood. investors: three funds/angels that would plausibly circle THIS space, each mapped to one archetype from the enum; thesis in their own voice ABOUT THIS MARKET (never the words 'growth is the only truth' or any stock phrase); a concrete trait, a bond connecting them to this founder's world, a flaw, and a SECRET the founder must never be told directly. rivals: two companies already competing for these exact customers — name (pronounceable, no real companies), what they do in one line, how strong they look, and three tactics they actually use in this market.
The same birth also writes the company's own binder. identity: one_liner — what the company is in plain dry words (never the pitch's own adjectives); who_for — who it is actually for. growth_topics: the four growth channels dressed as four plots fitted to THIS business — you invent each plot's name and one_line in the business's own vocabulary, but each channel's CHARACTER must survive verbatim in your wording, because the engine behaves exactly this way: ads is INSTANT AND SATURATING (works the day it is watered, stops the day it is not, each extra dollar buys a little less); content is A STOCK THAT COMPOUNDS funded and ROTS starved; referrals is a MULTIPLIER GATED on how much customers actually like the thing; outbound is QUOTA KNOCKING (so many doors a week per person knocking). Each channel ALSO carries buys — what the money CONCRETELY buys for THIS business, named as the real-world mechanism a founder would recognise (for referrals: the actual deal, e.g. ""referral cards: a free session for every friend who books""; for ads: the actual placement; for content: the actual artifact; for outbound: who knocks on what) — and why: one dry line of reasoning a founder can read for why this mechanism fits THIS business and ITS customers, never generic marketing advice. You fit vocabulary, never numbers. works_terms: this business's native units — unit_word (what ONE sold thing is called: a session, a seat, a unit, an order), capacity_word (what the capacity is made of: bookable hours, headroom, machine slots, active sellers), relief_word (what the overflow relief valve is called: freelancers, burst capacity, the subcontract shop, recruited supply). spend_book: 6-10 organisational spend lines fitted to THIS company (a restaurant gets front-of-house training and staff meals; a dev-tools company gets docs and on-call) — name, buys (one dry line on what the money actually buys), amt in dollars per week at garage scale (most lines 20-250), bucket must be one of sales (closing), care (retention), rnd (building), office (people & the room), contract_notice = weeks of notice if stopping this line is a CONTRACT (0 means stoppable instantly; only 1-3 lines should be contracts). You invent rows, never math. price_book: the structural price schedule for THIS business, every value INSIDE its band — open_site_pack 6000-40000 (opening a second roof: deposit + fit-out + first hires, about 18000 for an ordinary business), relocation_fee 100-1500 (moving one person between roofs), machine_shipping 150-4000 (moving one machine, a week offline), lease_break_weeks 4-16 (breaking a lease costs this many weeks of rent), contract_notice_wks 2-12 (the default notice on contract spend lines), refinance_break_fee 100-2000 (swapping an old note for a new quote), freelance_rate 15-300 (one overflow unit served by an outside hand, in dollars), subcontract_rate 10-250 (one unit made by the outside shop, in dollars), account_fire_penalty 200-5000 (firing a customer breaks a contract). Fit the values to the business — a massage studio's roofs and freelancers are cheap, a hardware plant's are not. birth_features: 3-6 named parts the product or service is MADE OF on day one (for a service: the 50-minute protocol, online booking; for hardware: capabilities and components; for a marketplace: escrow, ratings, search) — job is what the part does for the business: pull (brings them in), keep (keeps them), charge (lets us charge more), plumbing (nothing visible, everything stands on it — include at least one); keep_wk = what it costs per week to keep alive in dollars (features are never free; 10-60 is ordinary at garage scale); unit_cost_add = what it adds to serving ONE unit in dollars (0 for most, small for the rest).
Every thesis, one-liner, buys and what-they-do is a COMPLETE sentence or phrase that ends before the limit — never a thought cut mid-word. Dry, wince-funny, PG-13, no real companies or people. Every string you output is plain printable ASCII: when a natural phrase runs past a length limit, cut it at a word boundary — never swap in a shorter symbol or non-Latin character to fit.";

        /// Tier-3: the RUN DIRECTOR (PRD §7).
        public const string DIRECTOR_PROMPT = @"You are the RUN DIRECTOR for RUNWAY!, a satirical startup survival game. Once per era, you design the run's narrative arcs — the recurring storylines that make this run feel authored instead of drawn from a deck: a named rival company with a strategy, a recurring journalist with an angle, a slow-burn cofounder or investor storyline. Rules: arcs grow out of THIS company (its name, what it does, its business model, its recent actions); invent names — never real companies or people; write 2-3 arcs, each with beats for the CURRENT era and eras after it (never past eras); every beat directive is one concrete, self-standing instruction to a downstream event writer who sees ONLY the directive — name the actors and what happens next; escalation_rule says when the arc intensifies or pays off. If arcs already exist, evolve them: carry actors forward, never drop a thread without a payoff beat. Dry, wince-funny, PG-13.";

        public static readonly string[] ARC_KINDS =
        {
            "rival", "press", "cofounder", "investor", "customer",
        };

        /// GameState.ERAS — the only eras an arc beat may name.
        public static readonly string[] ERAS =
        {
            "garage", "coworking", "office", "floor", "hq",
        };

        /// THE OP REGISTRY, validator half (00-spine section 7). This list, the
        /// schema enum in LlmClient.cs and the executor in WeekCommit.cs are ONE
        /// list at three sites; a twin test pins them equal. `price_offer` was in
        /// the schema and the executor but missing HERE, so any DM reply that
        /// priced an offer was rejected wholesale — the bug this pin prevents.
        /// Mirrors SimEngine.OP_REGISTRY, copied rather than referenced because
        /// this lane deliberately holds no reference to a Runway.Core type.
        public static readonly string[] ALLOWED_OPS =
        {
            "cash_delta", "product_delta", "traction_delta", "morale_delta", "hype_delta",
            "set_flag", "status", "clock", "set_price", "price_offer", "set_marketing",
            "hire", "take_loan", "spend", "set_budget", "push_lead",
            "open_site", "close_site", "reassign_employee", "move_machine",
            "tag_offer", "tag_spend_line", "refinance_note", "fire_account",
            "retire_product", "pivot_audience", "pivot_product",
            "pitch_investor", "sign_instrument", "send_offer", "set_relief", "draft_offer",
        };

        public void Setup(LlmClient llm)
        {
            Llm = llm;
            // the production adjudicator prompt ships as data; the const is the fallback
            _adjudicatePrompt = RunwayPaths.ReadAllTextOrEmpty(
                RunwayPaths.Streaming("prompts/adjudicator.txt"));
            if (_adjudicatePrompt.Trim().Length == 0) _adjudicatePrompt = ADJUDICATE_PROMPT;
            _clarifyPrompt = RunwayPaths.ReadAllTextOrEmpty(
                RunwayPaths.Streaming("prompts/clarify.txt"));
        }

        bool Live { get { return Llm != null && Llm.Enabled; } }

        // ══ world ══════════════════════════════════════════════════════════════

        public void GenerateWorld(RunSnapshot s, Action<JObject> cb)
        {
            if (!Live)
            {
                if (cb != null) cb(null);
                return;
            }
            string user = string.Format(
                "The company:\n{0}\nPitch: {1}\nSells {2} to {3}.\nInvent its world.",
                s.CompanyName, s.CompanyIdea, s.BizWhat, s.BizWho);
            Llm.RequestJson(WORLDGEN_PROMPT, user, LlmClient.WorldSchema,
                result => { if (cb != null) cb(result); },
                // the birth blocks (spend book, price book, features) roughly
                // double the reply — 1400 truncated it on the anthropic path
                new LlmOptions { MaxTokens = 3200 });
        }

        // ══ Tier-3: the run director ═══════════════════════════════════════════

        /// One higher-quality call at run start + each era transition. Hands VALIDATED
        /// arcs to cb (an empty array on reject); the run lane stores them on state.
        public void GenerateArcs(RunSnapshot s, Action<JArray> cb)
        {
            if (Disabled || !Live)
            {
                if (cb != null) cb(new JArray());
                return;
            }
            var sb = new StringBuilder();
            sb.Append("Run state:\n").Append(Json(s.Digest));
            if (s.Arcs != null && s.Arcs.Count > 0)
                sb.Append("\n\nExisting arcs (evolve these, keep continuity):\n").Append(Json(s.Arcs));
            sb.Append("\n\nCurrent era: ").Append(s.Era).Append(". Write this run's arcs.");
            Llm.RequestJson(DIRECTOR_PROMPT, sb.ToString(), LlmClient.ArcSchema,
                result =>
                {
                    JArray clean = ValidateArcs(result);
                    if (cb != null) cb(clean);
                },
                new LlmOptions { Director = true, MaxTokens = 1600 });
        }

        /// Validates director output. Returns [] on any reject — one bad beat drops the
        /// whole reply, because a half-applied arc is a thread with no payoff.
        public static JArray ValidateArcs(JObject data)
        {
            var empty = new JArray();
            if (data == null) return empty;
            var arcs = data["arcs"] as JArray;
            if (arcs == null || arcs.Count == 0) return empty;
            var outArcs = new JArray();
            foreach (JToken t in arcs)
            {
                var a = t as JObject;
                if (a == null) return empty;
                if (Array.IndexOf(ARC_KINDS, Str(a, "kind")) < 0) return empty;
                if (Str(a, "arc_id").Trim().Length == 0) return empty;
                if (Str(a, "premise").Trim().Length == 0) return empty;
                var beats = a["beats"] as JArray;
                if (beats == null || beats.Count == 0) return empty;
                foreach (JToken bt in beats)
                {
                    var b = bt as JObject;
                    if (b == null) return empty;
                    if (Array.IndexOf(ERAS, Str(b, "era")) < 0) return empty;
                    string d = Str(b, "directive");
                    if (d.Trim().Length == 0 || d.Length > 220) return empty;
                }
                if (outArcs.Count < 3) outArcs.Add(a);
            }
            return outArcs;
        }

        /// Injection block for Tier-2 + adjudicator user messages. Empty without arcs.
        static string ArcBlock(RunSnapshot s)
        {
            if (s.ArcDirectives == null || s.ArcDirectives.Length == 0) return "";
            return "\n\nACTIVE NARRATIVE DIRECTIVES (this run's authored storylines — weave ONE in when it fits, never force all):\n- "
                   + string.Join("\n- ", s.ArcDirectives);
        }

        // ══ composers ══════════════════════════════════════════════════════════

        /// Proper names seen in the recent past — "Nico Sorel" looping week after week
        /// is the exact failure this hunts. Crude on purpose: a filter, not a parser.
        public static string[] RecentNames(string[] playedEvents, int back = 4)
        {
            var outNames = new List<string>();
            if (playedEvents == null) return outNames.ToArray();
            int from = Mathf.Max(playedEvents.Length - back, 0);
            var rex = new Regex("[A-Z][a-z]+ [A-Z][a-z]+");
            for (int i = from; i < playedEvents.Length; i++)
            {
                foreach (Match hit in rex.Matches(playedEvents[i] ?? ""))
                {
                    if (!outNames.Contains(hit.Value)) outNames.Add(hit.Value);
                }
            }
            return outNames.ToArray();
        }

        public string ComposeEventUser(RunSnapshot s)
        {
            string noRepeat = "";
            if (s.PlayedEvents != null && s.PlayedEvents.Length > 0)
                noRepeat = "\nALREADY PLAYED (never repeat these situations, characters, or their obvious sequels back-to-back): "
                           + Json(new JArray(s.PlayedEvents));
            string[] names = RecentNames(s.PlayedEvents);
            if (names.Length > 0)
                noRepeat += string.Format(
                    "\nPEOPLE ALREADY ON STAGE RECENTLY: {0}. Do NOT lead with any of them again this week — bring in someone NEW (a different customer, a stranger, a rival's person), or let the world itself be the event.",
                    string.Join(", ", names));
            return "Run state:\n" + Json(s.Digest) + ArcBlock(s) + noRepeat
                   + "\nWrite one new event card for this exact moment.";
        }

        /// THE CONTEXT SANDWICH (plan C1): world bible -> compacted memory -> recent
        /// weeks verbatim -> numeric state + engine signals -> the dice -> directives.
        public string ComposeAdjudicateUser(RunSnapshot s, JObject ev, string playerText,
                                            JObject dice)
        {
            var parts = new List<string>();
            parts.Add("Run state:\n" + Json(s.Digest));
            parts.Add("\nENGINE SIGNALS (ground truth this week — narrate FROM these):\n"
                      + Json(s.Signals));
            if (s.HasBible)
                parts.Add("\nTHE WORLD (fixed cast — keep names and voices consistent):\n"
                          + s.BibleDigest);
            if (!string.IsNullOrEmpty(s.StorySoFar))
                parts.Add("\nTHE STORY SO FAR (your own compacted memory):\n" + s.StorySoFar);
            if (s.RunHistory != null && s.RunHistory.Count > 0)
            {
                var recent = new JArray();
                for (int i = Mathf.Max(s.RunHistory.Count - 3, 0); i < s.RunHistory.Count; i++)
                    recent.Add(s.RunHistory[i]);
                parts.Add("\nRECENT WEEKS VERBATIM:\n" + Json(recent));
            }
            parts.Add(ArcBlock(s));
            if (dice != null && dice.Count > 0)
            {
                string modeLine = Str(dice, "mode");
                int mod = Int(dice, "mod", 0);
                int used = Int(dice, "used", 10);
                parts.Add(string.Format(
                    "\nTHE DIE IS ALREADY ON THE TABLE: the founder pressed, the cup poured. "
                    + "Rolled {0} and {1}{2}; the kept die is {3}. The governing stat is {4} (mod {5}) — "
                    + "fixed by the table, NOT yours to change; output roll.stat exactly as given. "
                    + "Set the DC from the PLAN'S difficulty alone, as if you had not seen the die "
                    + "(floors: routine 6-8, solid 9-11, bold 12-14, wild 15-16), then narrate what "
                    + "total {6} earned against it.",
                    Int(dice, "a", 10), Int(dice, "b", 10),
                    modeLine.Length > 0 ? " — " + modeLine : "",
                    used, Str(dice, "stat", "grit"),
                    (mod >= 0 ? "+" : "-") + Mathf.Abs(mod),
                    used + mod));
            }
            // WHO THE FOUNDER IS, 1-5, never rolled: the room already reacted to these
            // before anyone picked up a die. Narrate as if they were simply true.
            parts.Add("\nTRAITS (fixed): " + Json(s.TraitSheet));
            string directives = Directives(s);
            if (directives != "")
                parts.Add("\nDIRECTIVES (non-negotiable this week):\n" + directives);
            parts.Add(string.Format("\nEvent: {0} — {1}", Str(ev, "title"), Str(ev, "body")));
            parts.Add(string.Format("\nThe player writes their own move:\n\"{0}\"\n\nAdjudicate it.",
                Left(playerText, 300)));
            return string.Join("\n", parts.ToArray());
        }

        /// Deterministic, prescriptive, computed from state — the register LLM GMs obey.
        public static string Directives(RunSnapshot s)
        {
            var outLines = new List<string>();
            if (s.RunwayWeeks <= 3)
                outLines.Add(string.Format(
                    "- Runway is {0} weeks. The world MUST escalate; nothing is routine.", s.RunwayWeeks));
            if (s.Exhaustion >= 4)
                outLines.Add(string.Format(
                    "- The founder is exhausted ({0}/6). It shows in everything.", s.Exhaustion));
            if (s.Clocks != null)
            {
                foreach (RunSnapshot.ClockRow c in s.Clocks)
                {
                    if (c.WeeksLeft <= 2)
                        outLines.Add(string.Format("- A deadline looms ({0} wks): {1}. Reference it.",
                            c.WeeksLeft, c.Consequence));
                }
            }
            if (s.TechDebt >= 70f)
                outLines.Add(string.Format("- Tech debt is {0}. The cracks are visible to customers.",
                    (int)s.TechDebt));
            // THE CATALOG IS ALWAYS ON THE DESK (owner: the world must know
            // what we sell and at how much): every offer, one line each.
            if (s.Offers != null)
            {
                foreach (RunSnapshot.OfferRow o in s.Offers)
                {
                    string onm = string.IsNullOrEmpty(o.Name) ? "an offer" : o.Name;
                    string ounit = string.IsNullOrEmpty(o.Unit) ? "per order" : o.Unit;
                    if (o.Price > 0f)
                        outLines.Add(string.Format("- On sale: '{0}' at ${1} {2} (costs ~${3} a sale to serve).", onm, (int)o.Price, ounit, (int)System.Math.Round(o.ServeCost)));
                    else if (o.PriceSet)
                        outLines.Add(string.Format("- '{0}' is FREE ON PURPOSE (the founder chose $0) — it pays in users, not dollars.", onm));
                    else if (o.FairPrice > 0f)
                        outLines.Add(string.Format("- '{0}' has no set price: it bills at the going rate (~${1} {2}) until the founder names one. Use price_offer when the move prices it.", onm, (int)o.FairPrice, ounit));
                    else
                        outLines.Add(string.Format("- '{0}' has NO PRICE and no going rate: it earns $0. If the plan sells, the week must confront this.", onm));
                }
            }
            // ── SECTIONS 6-14: the nine subsystems, in the spine's fixed order.
            // The run lane fills LaneDirectives from SimEngine.LaneDirectives so
            // this file never references a Core type; lanes never touch it either.
            if (s.LaneDirectives != null)
                foreach (string ld in s.LaneDirectives)
                    if (!string.IsNullOrEmpty(ld)) outLines.Add(ld);
            // THE TOKEN BUDGET GUARD: 24 lines / 1200 chars, priority IS the
            // order, and the COMPOSER truncates — never the subsystems, so no
            // lane can starve another by writing more.
            var capped = new List<string>();
            int chars = 0;
            foreach (string l in outLines)
            {
                if (capped.Count >= 24) break;
                if (chars + l.Length + 1 > 1200 && capped.Count > 0) break;
                capped.Add(l);
                chars += l.Length + 1;
            }
            return string.Join("\n", capped.ToArray());
        }

        // ══ prefetch ═══════════════════════════════════════════════════════════

        public void Prefetch(RunSnapshot s)
        {
            if (Disabled || !Live || _pending || Pool.Count >= 3) return;
            _pending = true;
            Llm.RequestJson(SYSTEM_PROMPT, ComposeEventUser(s), LlmClient.EventSchema, OnCard);
        }

        void OnCard(JObject card)
        {
            _pending = false;
            if (card == null || card.Count == 0) return;
            if (ValidateCard(card))
            {
                card["tier"] = "generated";
                card["id"] = "gen_" + (long)(Time.realtimeSinceStartup * 1000f);
                Pool.Add(card);
            }
            else
            {
                Debug.LogWarning("generated card rejected by validator");
            }
        }

        /// THE INVISIBLE SEAM, pool half: a generated card if one is pooled and it is
        /// not a near-duplicate of a recent week. Null means "deal an authored card" —
        /// the content deck is the run lane's, so the fall-through lives there.
        public JObject TakeGeneratedCard(RunSnapshot s)
        {
            if (Disabled) Pool.Clear();
            string[] recent = RecentNames(s.PlayedEvents);
            while (Pool.Count > 0)
            {
                JObject cand = Pool[0];
                Pool.RemoveAt(0);
                string ct = Str(cand, "title");
                bool dup = false;
                if (s.PlayedEvents != null)
                {
                    int from = Mathf.Max(s.PlayedEvents.Length - 4, 0);
                    for (int i = from; i < s.PlayedEvents.Length; i++)
                    {
                        if (Similarity(s.PlayedEvents[i] ?? "", ct) > 0.6f) { dup = true; break; }
                    }
                }
                if (!dup)
                {
                    // a returning lead character IS a repeat, whatever the title says
                    string blob = ct + " " + Str(cand, "body");
                    for (int i = 0; i < recent.Length; i++)
                    {
                        if (blob.Contains(recent[i])) { dup = true; break; }
                    }
                }
                if (!dup) return cand;
                Debug.LogWarning("event pool: dropped near-duplicate '" + ct + "'");
            }
            return null;
        }

        // ══ adjudication ═══════════════════════════════════════════════════════

        /// WHAT THE WORLD SAYS WHEN THERE IS NO WORLD TO ASK. With no key the written
        /// move used to hand back nothing and the page did not so much as blink, which
        /// reads as broken rather than as unconfigured. So the world answers in its own
        /// voice instead — it hears the move, writes it down, and changes NOTHING.
        ///
        /// `effects` is empty ON PURPOSE and must stay empty. A stub that paid out would
        /// be the game inventing a judgement it never made.
        public static JObject KeylessAdjudication()
        {
            return new JObject
            {
                ["interpreted_as"] = "you write it down",
                ["narration"] = "The world takes note. Nothing changes yet — the phone stays quiet.",
                ["verdict"] = "fine",
                ["effects"] = new JArray(),
            };
        }

        /// Adjudicate the player's own written move. cb gets the validated verdict, the
        /// keyless stub when there is no key at all, or null when a live call came back
        /// empty or failed its validator.
        public void Adjudicate(RunSnapshot s, JObject ev, string playerText,
                               Action<JObject> cb, JObject dice = null, string tier = "assess")
        {
            if (!Live)
            {
                if (cb != null) cb(KeylessAdjudication());
                return;
            }
            var opts = new LlmOptions { Tier = tier };
            string user = ComposeAdjudicateUser(s, ev ?? new JObject(), playerText, dice);
            Llm.RequestJson(_adjudicatePrompt, user, LlmClient.AdjudicateSchema, result =>
            {
                if (result == null || result.Count == 0
                    || !ValidateEffects(result["effects"], true))
                {
                    // a PAYING player's transport failure deserves one more
                    // try before the caller falls back (A18 #16)
                    Llm.RequestJson(_adjudicatePrompt, user, LlmClient.AdjudicateSchema, again =>
                    {
                        if (again == null || again.Count == 0
                            || !ValidateEffects(again["effects"], true))
                        {
                            Debug.Log("DM adjudication failed twice (transport) — authored fallback");
                            if (cb != null) cb(null);
                            return;
                        }
                        Sanitize(s, again);
                        if (cb != null) cb(again);
                    }, opts);
                    return;
                }
                // THE SENTINEL (plan C3): deterministic post-checks. One retry with the
                // errors echoed, then proceed with the sanitized reply — never deadlock.
                List<string> faults = Sentinel(s, result);
                if (faults.Count == 0)
                {
                    if (cb != null) cb(result);
                    return;
                }
                Debug.LogWarning("DM sentinel: " + string.Join("; ", faults.ToArray()));
                string retryUser = user + "\n\nYOUR PREVIOUS REPLY WAS REJECTED FOR: "
                                   + string.Join("; ", faults.ToArray())
                                   + "\nFix ONLY these and answer again.";
                Llm.RequestJson(_adjudicatePrompt, retryUser, LlmClient.AdjudicateSchema, second =>
                {
                    JObject final = second;
                    if (final == null || final.Count == 0 || !ValidateEffects(final["effects"], true))
                        final = result;            // the first reply, sanitized below
                    Sanitize(s, final);
                    if (cb != null) cb(final);
                }, opts);
            }, opts);
        }

        /// THE CLARIFY PRE-PASS (owner: terra assesses, luna clarifies): one cheap call
        /// before the dice — does this move need ONE follow-up question?
        const string CANDIDATES_PROMPT = "You dress job applicants for RUNWAY!, a satirical startup survival game. The engine already decided every number — each candidate's role, skill 1-5 and weekly ask are FIXED and not yours. For each candidate, in the given order, invent ONLY: name (a plausible human full name, never a real person), quirk (one dry, specific habit, <=60 chars), one_liner (how they'd pitch themselves in one wince-funny sentence, <=90 chars). Match the texture to this company, its era and its business. Skill 5 reads impressive with one red flag; skill 1 reads earnest and alarming. A candidate with source \"referral\" knows someone on the team — let it show. Never state the numbers. No name may repeat a name in taken_names. Exactly one entry per candidate, same order. Output ONLY the schema.";

        /// ONE batch dressing call on weeks with arrivals (02 §8.1): pure
        /// transport — this assembly never sees Core types. The caller builds
        /// the payload from SimLabor and lands the reply back on it.
        public void DressApplicants(JObject payload, Action<JObject> cb)
        {
            if (payload == null || payload.Count == 0 || Llm == null || !Llm.Enabled)
            {
                if (cb != null) cb(null);
                return;
            }
            Llm.RequestJson(CANDIDATES_PROMPT,
                payload.ToString(Newtonsoft.Json.Formatting.None) + "\nDress them.",
                LlmClient.CandidatesSchema, res =>
                {
                    if (cb != null) cb(res);
                }, new LlmOptions { Tier = "clarify" });
        }

        const string LEAD_PROMPT = "You name enterprise prospects for RUNWAY!, a satirical startup survival game. You receive the player's company (name, idea, what × who) and N new prospects that just took a first meeting, each with a size band. Invent N fictional companies that would plausibly BUY from this exact business — sector-appropriate, pronounceable, never real companies or people. one_liner: who they are and why they're suddenly shopping, dry, wince-funny, a complete sentence. Return exactly N leads in the order given. Never output numbers, seat counts, or stages.";

        /// ONE batch naming call on weeks with spawns (05 §10): pure transport —
        /// the caller builds the payload from SimPipeline and lands the reply.
        public void DressLeads(JObject payload, Action<JObject> cb)
        {
            if (payload == null || payload.Count == 0 || Llm == null || !Llm.Enabled)
            {
                if (cb != null) cb(null);
                return;
            }
            Llm.RequestJson(LEAD_PROMPT,
                payload.ToString(Newtonsoft.Json.Formatting.None) + "\nName them.",
                LlmClient.LeadSchema, res => { if (cb != null) cb(res); },
                new LlmOptions { Tier = "clarify" });
        }

        const string BETS_PROMPT = "You name feature bets for RUNWAY!, a satirical startup survival game. Given the company, its era, what already shipped and what sits on the board, write N candidate feature bets SPECIFIC to this exact business. name: <=28 chars, plain product-speak a PM would write on a card. desc: <=90 chars, dry and wince-funny — what it is and who it is for. kind: quality (the product gets better for everyone), retention (existing customers stay longer), reach (new people get a reason to show up), platform (infrastructure that makes all future building faster — only natural for a company with real scale). ambition: 1 small and safe, 2 a real feature, 3 the big swing. Cover at least two different kinds across the batch. Never numbers, never metric promises, never real companies or people, never a bet already on the board or recently shipped. Exactly `slots` entries. Output ONLY the schema.";

        /// ONE batch dressing call on weeks the board drew fresh paper (07 §10):
        /// pure transport — caller builds the payload and lands the reply.
        public void DressBets(JObject payload, Action<JObject> cb)
        {
            if (payload == null || payload.Count == 0 || Llm == null || !Llm.Enabled)
            {
                if (cb != null) cb(null);
                return;
            }
            Llm.RequestJson(BETS_PROMPT,
                payload.ToString(Newtonsoft.Json.Formatting.None) + "\nName them.",
                LlmClient.BetsSchema, res => { if (cb != null) cb(res); },
                new LlmOptions { Tier = "clarify" });
        }

        const string OFFER_PROMPT = "You itemize and price a new product or service for a startup-survival business simulator. You receive the company (what kind, for whom, its idea, its stage) and the founder's plain-words description of something new they want to sell. Output realistic market terms as strict JSON:\n- name: a short clean name for the offer, taken from the founder's words (<=40 chars)\n- unit: the billing unit — one of \"per session\", \"per month\", \"per order\", \"per unit\", \"per year\", \"per hour\", \"per package\", \"per kit\"\n- fair_price: what this audience typically pays per unit at the going market rate, in USD (Consumer offers are cheap, Enterprise expensive)\n- elasticity: how hard demand punishes overpricing — 0.8 luxury/inelastic, ~2.0 typical, 2.6 commodity\n- weight: how much of an average customer's weekly spend lands on this offer (1.0 typical, 0.5 side item, 2.0 flagship)\n- variable_costs: 1-4 itemized costs paid EVERY TIME one unit is sold or served (materials, packaging, compute, payment fees, a worker's hour). Concrete labels (<=24 chars) in this business's own vocabulary, never generic. Amounts in USD per unit; their SUM should land at 15-60% of fair_price — a plausible gross margin for this kind of business.\n- fixed_costs_wk: 0-3 weekly standing costs this offer adds whether or not anything sells (a tool subscription, a license, storage, a rented machine). USD per week, scaled to the company's stage.\nNever invent revenue, discounts, or advice. Strict JSON only. No prose.";

        /// ONE pricing call on a founder write-in (01 §8 L1): pure transport —
        /// the caller builds the payload from the desk and lands the reply on
        /// the review card. Nothing here decides a number; add_offer's clamps do.
        public void PriceOfferIdea(JObject payload, Action<JObject> cb)
        {
            if (payload == null || payload.Count == 0 || Llm == null || !Llm.Enabled)
            {
                if (cb != null) cb(null);
                return;
            }
            Llm.RequestJson(OFFER_PROMPT,
                payload.ToString(Newtonsoft.Json.Formatting.None) + "\nPrice it.",
                LlmClient.OfferSchema, res => { if (cb != null) cb(res); },
                new LlmOptions { Tier = "clarify" });
        }

        public void Clarify(RunSnapshot s, JObject ev, string move, Action<JObject> cb)
        {
            if (!Live)
            {
                if (cb != null) cb(null);
                return;
            }
            if (_clarifyPrompt.Length == 0)
                _clarifyPrompt = RunwayPaths.ReadAllTextOrEmpty(
                    RunwayPaths.Streaming("prompts/clarify.txt"));

            var offers = new JArray();
            if (s.Offers != null)
            {
                foreach (RunSnapshot.OfferRow o in s.Offers)
                    offers.Add(new JObject { ["name"] = o.Name ?? "",
                        ["priced"] = o.Price > 0f || o.PriceSet,
                        ["price"] = o.Price, ["unit"] = o.Unit ?? "" });
            }
            var user = new JObject
            {
                ["run_state"] = new JObject
                {
                    ["cash"] = s.Cash,
                    ["week"] = s.Week,
                    ["era"] = s.Era,
                    ["customers"] = s.Traction,
                    ["crew"] = new JArray(s.Crew ?? new string[0]),
                    ["items"] = new JArray(s.Items ?? new string[0]),
                    ["budgets"] = s.Budgets ?? new JObject(),
                    // the roofs, by name: with ≥2 of them a physical hire/buy
                    // that names none is a real gap (the "for which roof?" rule)
                    ["sites"] = new JArray(s.SiteNames ?? new string[0]),
                    ["offers"] = offers,
                },
                ["event_card"] = new JObject
                {
                    ["title"] = Str(ev, "title"),
                    ["body"] = Left(Str(ev, "body"), 160),
                },
                ["move"] = Left(move, 300),
            };
            Llm.RequestJson(_clarifyPrompt, Json(user), LlmClient.ClarifySchema,
                res => { if (cb != null) cb(res); }, LlmOptions.Clarify);
        }

        // ══ the deterministic checks ═══════════════════════════════════════════

        /// Deterministic continuity checks: hallucinated cast, premise drift, empty
        /// milestones.
        public List<string> Sentinel(RunSnapshot s, JObject res)
        {
            var faults = new List<string>();
            // 1 — the known-NPC roster the narration is checked against
            var known = new List<string>();
            if (s.InvestorNames != null) known.AddRange(s.InvestorNames);
            if (s.RivalNames != null) known.AddRange(s.RivalNames);
            if (s.LeadNames != null) known.AddRange(s.LeadNames);
            if (s.LogoNames != null) known.AddRange(s.LogoNames);
            string narration = Str(res, "narration");

            // premise guard: money the narration spends must exist (order-of-magnitude)
            int spendGuess = 0;
            var effects = res["effects"] as JArray;
            if (effects != null)
            {
                foreach (JToken eff in effects)
                {
                    var d = eff as JObject;
                    if (d == null) continue;
                    if (Str(d, "op") == "cash_delta") spendGuess = Int(d, "v", 0);
                }
            }
            if (spendGuess < 0 && s.Cash + spendGuess < -8000)
                faults.Add(string.Format("the move spends ${0} the company does not have (cash ${1})",
                    -spendGuess, s.Cash));

            // unknown status names die silently in the executor; flag them for a fix
            if (effects != null)
            {
                foreach (JToken eff2 in effects)
                {
                    var d = eff2 as JObject;
                    if (d == null) continue;
                    if (Str(d, "op") == "status" && !KnownStatus(s, Str(d, "v")))
                        faults.Add(string.Format("unknown status '{0}' — pick from the fixed catalog",
                            Str(d, "v")));
                }
            }

            // a raise-verdict week that grants seed money must set the flag (and vice versa)
            string lower = narration.ToLowerInvariant();
            bool saysRound = lower.Contains("term sheet signed") || lower.Contains("round closes")
                             || lower.Contains("wire hits");
            bool setsRound = false;
            if (effects != null)
            {
                foreach (JToken eff3 in effects)
                {
                    var d = eff3 as JObject;
                    if (d == null) continue;
                    if (Str(d, "op") == "set_flag" && Str(d, "v").Contains("raised")) setsRound = true;
                }
            }
            if (saysRound && !setsRound)
                faults.Add("the narration closes a round but no *_raised flag is set");
            return faults;
        }

        /// What survives even a failed retry: strip ops the engine would refuse anyway.
        public void Sanitize(RunSnapshot s, JObject res)
        {
            if (res == null) return;
            var ok = new JArray();
            var effects = res["effects"] as JArray;
            if (effects != null)
            {
                foreach (JToken eff in effects)
                {
                    var d = eff as JObject;
                    if (d == null) continue;
                    if (Str(d, "op") == "status" && !KnownStatus(s, Str(d, "v"))) continue;
                    ok.Add(d);
                }
            }
            res["effects"] = ok;
        }

        /// An empty catalogue means the run lane has not handed one over: trust the DM
        /// rather than reject every status it names.
        static bool KnownStatus(RunSnapshot s, string status)
        {
            if (s.StatusCatalog == null || s.StatusCatalog.Length == 0) return true;
            return Array.IndexOf(s.StatusCatalog, status) >= 0;
        }

        public bool ValidateEffects(JToken effects, bool allowEmpty = false)
        {
            var arr = effects as JArray;
            if (arr == null) return false;
            if (arr.Count == 0) return allowEmpty;
            foreach (JToken eff in arr)
            {
                var d = eff as JObject;
                if (d == null) return false;
                if (Array.IndexOf(ALLOWED_OPS, Str(d, "op")) < 0) return false;
            }
            return true;
        }

        public bool ValidateCard(JObject card)
        {
            if (card == null) return false;
            if (card["title"] == null || card["body"] == null || card["choices"] == null) return false;
            if (Str(card, "title").Length > 60 || Str(card, "body").Length > 500) return false;
            var choices = card["choices"] as JArray;
            if (choices == null || choices.Count < 2 || choices.Count > 4) return false;
            foreach (JToken ch in choices)
            {
                var c = ch as JObject;
                if (c == null || c["label"] == null || c["effects"] == null) return false;
                if (Str(c, "label").Length > 60) return false;
                if (!ValidateEffects(c["effects"])) return false;
            }
            return true;
        }

        // ══ helpers ════════════════════════════════════════════════════════════

        /// JSON.stringify() — compact, the shape every prompt in this game was tuned on.
        public static string Json(JToken t)
        {
            if (t == null) return "{}";
            return t.ToString(Formatting.None);
        }

        public static string Str(JObject o, string key, string fallback = "")
        {
            if (o == null) return fallback;
            JToken t = o[key];
            return t == null || t.Type == JTokenType.Null ? fallback : t.ToString();
        }

        public static int Int(JObject o, string key, int fallback)
        {
            if (o == null) return fallback;
            JToken t = o[key];
            if (t == null || t.Type == JTokenType.Null) return fallback;
            double d;
            if (double.TryParse(t.ToString(), System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out d))
                return (int)d;
            return fallback;
        }

        public static string Left(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= n ? s : s.Substring(0, n);
        }

        /// Godot's String.similarity(): the share of shared character bigrams.
        public static float Similarity(string a, string b)
        {
            if (a == b) return 1f;
            if (a.Length < 2 || b.Length < 2) return 0f;
            var bigramsA = new List<string>();
            for (int i = 0; i < a.Length - 1; i++) bigramsA.Add(a.Substring(i, 2));
            var bigramsB = new List<string>();
            for (int i = 0; i < b.Length - 1; i++) bigramsB.Add(b.Substring(i, 2));
            int hits = 0;
            foreach (string g in bigramsA)
            {
                int at = bigramsB.IndexOf(g);
                if (at >= 0) { bigramsB.RemoveAt(at); hits++; }
            }
            return (float)(hits * 2) / (bigramsA.Count + bigramsB.Count + hits);
        }
    }
}
