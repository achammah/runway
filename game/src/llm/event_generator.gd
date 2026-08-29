class_name EventGenerator
extends Node
## Tier-2 generation + free-move adjudication (PRD §7). Prefetches event cards
## in the background; adjudicates the player's own written moves. Both flow
## through the same op whitelist and clamps — the LLM writes flavor, never rules.

var llm: LlmClient
var pool: Array = []
var _pending := false

const SYSTEM_PROMPT := """You write event cards for RUNWAY!, a satirical startup survival game. Voice: dry, specific, wince-funny. Body 60 words max. Choice labels 8 words max. Never real companies or people. Never break the fourth wall. The title is a PLAIN statement of the situation in at most 7 words — 'The pilot customer wants a discount', never a mood-phrase riddle like 'Inner Calm, Concrete Floor'. If the user message lists PEOPLE ALREADY ON STAGE RECENTLY, none of them may appear in this card. You receive the run state as JSON, including the player's company_name and what it does (company_does) — write events that are SPECIFIC to that business (its customers, its industry's absurdities, its failure modes), and refer to the company by name when natural. Output ONLY a card matching the schema. Effects use ONLY the allowed ops within sane ranges (meter deltas within ±15; cash_delta proportionate to the era in the state — ±2000 garage, ±10k coworking, ±60k office, ±250k floor, ±1M hq). Match the event to the era and its cast: the state carries era_name, staff (named employees with burnout levels), rounds_raised and board — a garage event smells of ramen, an HQ event of lawyers; a Service business bills hours and juggles clients, a Marketplace juggles two sides, Hardware waits on parts; name a staff member when one fits, and never invent people who are not in the state. Choices must be genuine dilemmas — no strictly-correct option. Reference at least one specific item, cofounder, or flag from the state. The state includes recent_actions — the log of what the player actually did each week. USE IT: create continuity and follow-ups. Some weeks, instead of a problem, write an OPPORTUNITY that grows directly out of a recent action (a prospect who saw the marketing post and liked it, a demo attendee who wants an intro, a customer who mentioned them somewhere) — opportunities still carry tradeoffs, never free wins."""

const ADJUDICATE_PROMPT := """You are the world of RUNWAY!, a satirical startup survival game, adjudicating a founder's free-form action during an event. You receive the full run state — company, business_model (what × who), funding_path, employees, customers, product_version, items owned, cofounders with roles and commitment, archetype competences, meters — then the event and the player's written move. Judge it fairly but the world is harsh, and CONTEXT-AWARE: concrete plans that use things the founder ACTUALLY HAS work better; a bootstrapped company can be scrappy but can't outspend problems; a VC-backed one has money but answers for it; enterprise sales are slow and relationship-driven, consumer needs volume and virality, hardware makes everything slower and costlier; part-time cofounders are less available; more customers means more to lose. Vague, magical, or entitled answers backfire with comedy. narration: 210-290 words in 4-6 short second-person paragraphs — read while the art renders (~70s). PLAIN FIRST: simple declaratives a tired reader follows first pass; at most one wry line per two paragraphs; no riddle headlines. verdict: brilliant / fine / risky / backfired. effects: 1-3 ops from the whitelist, magnitudes proportionate to the era in the state (cash within ±3000 in the garage, scaling up by era; meters within ±15 always). The player makes ONE move per week — your effects carry seven days of work, so a sound grounded plan earns the generous end of the range. MILESTONES: when the written week genuinely constitutes it, set the gating flag via set_flag — first_revenue, launched, pmf, seed_raised, series_a (max one per week; pair a closed round with its cash). THE ROLL: the user message carries d20=N and competences; pick the governing stat, mod=stat-3, judge DC 6-16 by boldness, and narrate what total EARNED (beat by 5+ brilliant / 0+ fine / -1..-2 risky-mixed / -3- backfired); output roll={stat,dc}. Every effect includes "why": its concrete in-world cause (<=10 words). Staff named in the state are real: leaning on a cooked employee is risky, and plans ignoring an investor board draw friction. Never more than one strongly positive effect unless the plan is genuinely brilliant AND grounded in what the founder actually has."""

var _adjudicate_prompt := ""

func _init(p_llm: LlmClient) -> void:
	llm = p_llm
	# the production adjudicator prompt ships as data; the const is the fallback
	if FileAccess.file_exists("res://data/prompts/adjudicator.txt"):
		_adjudicate_prompt = FileAccess.get_file_as_string("res://data/prompts/adjudicator.txt")
	if _adjudicate_prompt.strip_edges() == "":
		_adjudicate_prompt = ADJUDICATE_PROMPT

## Run-start world generation: everything is ABOUT this company. Deterministic
## WorldGen remains the keyless fallback and the shape both paths share.
const WORLDGEN_PROMPT := """You are the world-builder for RUNWAY!, a satirical startup survival game. Given the company (its pitch, what it sells, to whom), invent THE WORLD IT WAS BORN INTO — specific to this exact business, never generic. market: honest intuitive numbers (how many real buyers exist for THIS product; how many weeks such a customer stays before churning) and a dry one-liner about this market's mood. investors: three funds/angels that would plausibly circle THIS space, each mapped to one archetype from the enum; thesis in their own voice ABOUT THIS MARKET (never the words 'growth is the only truth' or any stock phrase); a concrete trait, a bond connecting them to this founder's world, a flaw, and a SECRET the founder must never be told directly. rivals: two companies already competing for these exact customers — name (pronounceable, no real companies), what they do in one line, how strong they look, and three tactics they actually use in this market.
The same birth also writes the company's own binder. identity: one_liner — what the company is in plain dry words (never the pitch's own adjectives); who_for — who it is actually for. growth_topics: the four growth channels dressed as four plots fitted to THIS business — you invent each plot's name and one_line in the business's own vocabulary, but each channel's CHARACTER must survive verbatim in your wording, because the engine behaves exactly this way: ads is INSTANT AND SATURATING (works the day it is watered, stops the day it is not, each extra dollar buys a little less); content is A STOCK THAT COMPOUNDS funded and ROTS starved; referrals is a MULTIPLIER GATED on how much customers actually like the thing; outbound is QUOTA KNOCKING (so many doors a week per person knocking). Each channel ALSO carries buys — what the money CONCRETELY buys for THIS business, named as the real-world mechanism a founder would recognise (for referrals: the actual deal, e.g. "referral cards: a free session for every friend who books"; for ads: the actual placement; for content: the actual artifact; for outbound: who knocks on what) — and why: one dry line of reasoning a founder can read for why this mechanism fits THIS business and ITS customers, never generic marketing advice. You fit vocabulary, never numbers. works_terms: this business's native units — unit_word (what ONE sold thing is called: a session, a seat, a unit, an order), capacity_word (what the capacity is made of: bookable hours, headroom, machine slots, active sellers), relief_word (what the overflow relief valve is called: freelancers, burst capacity, the subcontract shop, recruited supply). spend_book: 6-10 organisational spend lines fitted to THIS company (a restaurant gets front-of-house training and staff meals; a dev-tools company gets docs and on-call) — name, buys (one dry line on what the money actually buys), amt in dollars per week at garage scale (most lines 20-250), bucket must be one of sales (closing), care (retention), rnd (building), office (people & the room), contract_notice = weeks of notice if stopping this line is a CONTRACT (0 means stoppable instantly; only 1-3 lines should be contracts). You invent rows, never math. price_book: the structural price schedule for THIS business, every value INSIDE its band — open_site_pack 6000-40000 (opening a second roof: deposit + fit-out + first hires, about 18000 for an ordinary business), relocation_fee 100-1500 (moving one person between roofs), machine_shipping 150-4000 (moving one machine, a week offline), lease_break_weeks 4-16 (breaking a lease costs this many weeks of rent), contract_notice_wks 2-12 (the default notice on contract spend lines), refinance_break_fee 100-2000 (swapping an old note for a new quote), freelance_rate 15-300 (one overflow unit served by an outside hand, in dollars), subcontract_rate 10-250 (one unit made by the outside shop, in dollars), account_fire_penalty 200-5000 (firing a customer breaks a contract). Fit the values to the business — a massage studio's roofs and freelancers are cheap, a hardware plant's are not. birth_features: 3-6 named parts the product or service is MADE OF on day one (for a service: the 50-minute protocol, online booking; for hardware: capabilities and components; for a marketplace: escrow, ratings, search) — job is what the part does for the business: pull (brings them in), keep (keeps them), charge (lets us charge more), plumbing (nothing visible, everything stands on it — include at least one); keep_wk = what it costs per week to keep alive in dollars (features are never free; 10-60 is ordinary at garage scale); unit_cost_add = what it adds to serving ONE unit in dollars (0 for most, small for the rest).
Every thesis, one-liner, buys and what-they-do is a COMPLETE sentence or phrase that ends before the limit — never a thought cut mid-word. Dry, wince-funny, PG-13, no real companies or people. Every string you output is plain printable ASCII: when a natural phrase runs past a length limit, cut it at a word boundary — never swap in a shorter symbol or non-Latin character to fit."""

func generate_world(state: GameState, cb: Callable) -> void:
	if not llm.enabled():
		if cb.is_valid():
			cb.call({})
		return
	var user := "The company:\n%s\nPitch: %s\nSells %s to %s.\nInvent its world." % [
		state.company_name, state.company_idea, state.biz_what, state.biz_who]
	llm.request_json(WORLDGEN_PROMPT, user, LlmClient.WORLD_SCHEMA, func(result: Dictionary):
		if cb.is_valid():
			cb.call(result),
		# the birth blocks (spend book, price book, features) roughly double
		# the reply — 1400 truncated it on the anthropic path
		{"max_tokens": 3200})

# ── Tier-3: the RUN DIRECTOR (PRD §7) ─────────────────────────────────────
const DIRECTOR_PROMPT := """You are the RUN DIRECTOR for RUNWAY!, a satirical startup survival game. Once per era, you design the run's narrative arcs — the recurring storylines that make this run feel authored instead of drawn from a deck: a named rival company with a strategy, a recurring journalist with an angle, a slow-burn cofounder or investor storyline. Rules: arcs grow out of THIS company (its name, what it does, its business model, its recent actions); invent names — never real companies or people; write 2-3 arcs, each with beats for the CURRENT era and eras after it (never past eras); every beat directive is one concrete, self-standing instruction to a downstream event writer who sees ONLY the directive — name the actors and what happens next; escalation_rule says when the arc intensifies or pays off. If arcs already exist, evolve them: carry actors forward, never drop a thread without a payoff beat. Dry, wince-funny, PG-13."""

const ARC_KINDS := ["rival", "press", "cofounder", "investor", "customer"]

## One higher-quality call at run start + each era transition. Stores validated
## arcs on state and hands them to cb. No key (or daily determinism) → no arcs,
## state untouched, zero impact.
func generate_arcs(state: GameState, cb: Callable = Callable()) -> void:
	if disabled or not llm.enabled():
		if cb.is_valid():
				cb.call([])
		return
	var user := "Run state:\n" + JSON.stringify(state.to_digest())
	if not state.arcs.is_empty():
		user += "\n\nExisting arcs (evolve these, keep continuity):\n" + JSON.stringify(state.arcs)
	user += "\n\nCurrent era: %s. Write this run's arcs." % state.era
	llm.request_json(DIRECTOR_PROMPT, user, LlmClient.ARC_SCHEMA, _on_arcs.bind(state, cb),
		{"director": true, "max_tokens": 1600})

func _on_arcs(result: Dictionary, state: GameState, cb: Callable) -> void:
	var arcs := _ingest_arcs(state, result)
	if cb.is_valid():
		if cb.is_valid():
			cb.call(arcs)

## Validates director output and stores it on state. Returns stored arcs ([] on reject).
func _ingest_arcs(state: GameState, data: Dictionary) -> Array:
	var clean := _validate_arcs(data)
	if not clean.is_empty():
		state.arcs = clean
	return clean

func _validate_arcs(data: Dictionary) -> Array:
	var arcs = data.get("arcs", [])
	if not (arcs is Array) or arcs.is_empty():
		return []
	var out: Array = []
	for a in arcs:
		if not (a is Dictionary):
			return []
		if not ARC_KINDS.has(String(a.get("kind", ""))):
			return []
		if String(a.get("arc_id", "")).strip_edges() == "" or String(a.get("premise", "")).strip_edges() == "":
			return []
		var beats = a.get("beats", [])
		if not (beats is Array) or beats.is_empty():
			return []
		for b in beats:
			if not (b is Dictionary):
				return []
			if not GameState.ERAS.has(String(b.get("era", ""))):
				return []
			var d := String(b.get("directive", ""))
			if d.strip_edges() == "" or d.length() > 220:
				return []
		if out.size() < 3:
			out.append(a)
	return out

## Injection block for Tier-2 + adjudicator user messages. Empty without arcs.
func _arc_block(state: GameState) -> String:
	var dirs := state.active_arc_directives()
	if dirs.is_empty():
		return ""
	return "\n\nACTIVE NARRATIVE DIRECTIVES (this run's authored storylines — weave ONE in when it fits, never force all):\n- " + "\n- ".join(dirs)

## Proper names seen in the recent past — "Nico Sorel" looping week after week
## is the exact failure this hunts. Capitalized first-last pairs from the last
## few played entries; crude on purpose, a filter not a parser.
static func recent_names(state: GameState, back: int = 4) -> PackedStringArray:
	var rex := RegEx.new()
	rex.compile("[A-Z][a-z]+ [A-Z][a-z]+")
	var out := PackedStringArray()
	for pt in state.played_events.slice(maxi(state.played_events.size() - back, 0)):
		for hit in rex.search_all(String(pt)):
			var nm := hit.get_string()
			if not out.has(nm):
				out.append(nm)
	return out

## User-message composers (also exercised directly by tests/smoke.gd).
func compose_event_user(state: GameState) -> String:
	var no_repeat := ""
	if not state.played_events.is_empty():
		no_repeat = "\nALREADY PLAYED (never repeat these situations, characters, or their obvious sequels back-to-back): " + JSON.stringify(state.played_events)
	var names := recent_names(state)
	if not names.is_empty():
		no_repeat += "\nPEOPLE ALREADY ON STAGE RECENTLY: %s. Do NOT lead with any of them again this week — bring in someone NEW (a different customer, a stranger, a rival\'s person), or let the world itself be the event." % ", ".join(names)
	return "Run state:\n" + JSON.stringify(state.to_digest()) + _arc_block(state) + no_repeat + "\nWrite one new event card for this exact moment."

## THE CONTEXT SANDWICH (plan C1): world bible -> compacted memory -> recent
## weeks verbatim -> numeric state + engine signals -> the dice -> directives.
func compose_adjudicate_user(state: GameState, ev: Dictionary, player_text: String, dice: Dictionary = {}) -> String:
	var parts := PackedStringArray()
	parts.append("Run state:\n" + JSON.stringify(state.to_digest()))
	parts.append("\nENGINE SIGNALS (ground truth this week — narrate FROM these):\n"
		+ JSON.stringify(SimEngine.signals(state)))
	if not state.investors.is_empty() or not state.rivals.is_empty():
		parts.append("\nTHE WORLD (fixed cast — keep names and voices consistent):\n"
			+ WorldGen.bible_digest(state))
	if state.story_so_far != "":
		parts.append("\nTHE STORY SO FAR (your own compacted memory):\n" + state.story_so_far)
	if not state.run_history.is_empty():
		var recent: Array = state.run_history.slice(maxi(state.run_history.size() - 3, 0))
		parts.append("\nRECENT WEEKS VERBATIM:\n" + JSON.stringify(recent))
	parts.append(_arc_block(state))
	if not dice.is_empty():
		var mode_line := String(dice.get("mode", ""))
		parts.append(("\nTHE DIE IS ALREADY ON THE TABLE: the founder pressed, the cup poured. "
			+ "Rolled %d and %d%s; the kept die is %d. The governing stat is %s (mod %+d) — "
			+ "fixed by the table, NOT yours to change; output roll.stat exactly as given. "
			+ "Set the DC from the PLAN'S difficulty alone, as if you had not seen the die "
			+ "(floors: routine 6-8, solid 9-11, bold 12-14, wild 15-16), then narrate what "
			+ "total %d earned against it.") % [
			int(dice.get("a", 10)), int(dice.get("b", 10)),
			(" — " + mode_line) if mode_line != "" else "",
			int(dice.get("used", 10)), String(dice.get("stat", "grit")),
			int(dice.get("mod", 0)),
			int(dice.get("used", 10)) + int(dice.get("mod", 0))])
	# WHO THE FOUNDER IS, 1-5, never rolled: the room already reacted to these
	# before anyone picked up a die. Narrate as if they were simply true.
	parts.append("\nTRAITS (fixed): " + JSON.stringify(state.trait_sheet()))
	var directives := _directives(state)
	if directives != "":
		parts.append("\nDIRECTIVES (non-negotiable this week):\n" + directives)
	parts.append("\nEvent: %s — %s" % [String(ev.get("title", "")), String(ev.get("body", ""))])
	parts.append("\nThe player writes their own move:\n\"%s\"\n\nAdjudicate it." % player_text.substr(0, 300))
	return "\n".join(parts)

## Deterministic, prescriptive, computed from state — the register LLM GMs obey.
func _directives(state: GameState) -> String:
	var out := PackedStringArray()
	var rw := SimEngine.runway_weeks(state)
	if rw <= 3:
		out.append("- Runway is %d weeks. The world MUST escalate; nothing is routine." % rw)
	if state.exhaustion >= 4:
		out.append("- The founder is exhausted (%d/6). It shows in everything." % state.exhaustion)
	for c in state.clocks:
		if int((c as Dictionary).get("weeks_left", 9)) <= 2:
			out.append("- A deadline looms (%d wks): %s. Reference it." % [
				int((c as Dictionary).get("weeks_left", 0)), String((c as Dictionary).get("consequence", ""))])
	if state.tech_debt >= 70.0:
		out.append("- Tech debt is %d. The cracks are visible to customers." % int(state.tech_debt))
	# THE CATALOG IS ALWAYS ON THE DESK (owner: the world must know what we
	# sell and at how much): every offer, its price and unit, in one line each.
	for o in state.offers:
		var od: Dictionary = o
		var onm := String(od.get("name", "an offer"))
		var oprice := float(od.get("price", 0.0))
		var ounit := String(od.get("unit", "per order"))
		if oprice > 0.0:
			out.append("- On sale: '%s' at $%d %s (costs ~$%d a sale to serve)." % [onm, int(oprice), ounit,
				int(round(float(od.get("unit_cost", 0.0)) * SimEngine.learning_curve(state)))])
		elif bool(od.get("price_set", false)):
			out.append("- '%s' is FREE ON PURPOSE (the founder chose $0) — it pays in users, not dollars." % onm)
		else:
			var ofair := float(od.get("fair_price", 0.0))
			if ofair > 0.0:
				out.append("- '%s' has no set price: it bills at the going rate (~$%d %s) until the founder names one. Use price_offer when the move prices it." % [onm, int(ofair), ounit])
			else:
				out.append("- '%s' has NO PRICE and no going rate: it earns $0. If the plan sells, the week must confront this." % onm)
	# ── SECTIONS 6-14: the nine subsystems, in the spine's fixed order. Lanes
	# return lines and never touch this file; the spine orders them, and the cap
	# truncates the block rather than letting a subsystem starve another by
	# writing more (docs/design/00-spine.md §5).
	for l in SimEngine.lane_directives(state):
		out.append(l)
	return "\n".join(PackedStringArray(SimEngine.cap_directives(out)))

## THE STREET PRICES A NEW OFFER (owner: part of it can be generated by an
## LLM): the founder writes what they sell in plain words; the model answers
## with market terms; SimEngine.add_offer clamps whatever comes back.
const OFFER_PROMPT := """You itemize and price a new product or service for a startup-survival business simulator. You receive the company (what kind, for whom, its idea, its stage) and the founder's plain-words description of something new they want to sell. Output realistic market terms as strict JSON:
- name: a short clean name for the offer, taken from the founder's words (<=40 chars)
- unit: the billing unit — one of "per session", "per month", "per order", "per unit", "per year", "per hour", "per package", "per kit"
- fair_price: what this audience typically pays per unit at the going market rate, in USD (Consumer offers are cheap, Enterprise expensive)
- elasticity: how hard demand punishes overpricing — 0.8 luxury/inelastic, ~2.0 typical, 2.6 commodity
- weight: how much of an average customer's weekly spend lands on this offer (1.0 typical, 0.5 side item, 2.0 flagship)
- variable_costs: 1-4 itemized costs paid EVERY TIME one unit is sold or served (materials, packaging, compute, payment fees, a worker's hour). Concrete labels (<=24 chars) in this business's own vocabulary, never generic. Amounts in USD per unit; their SUM should land at 15-60% of fair_price — a plausible gross margin for this kind of business.
- fixed_costs_wk: 0-3 weekly standing costs this offer adds whether or not anything sells (a tool subscription, a license, storage, a rented machine). USD per week, scaled to the company's stage.
Never invent revenue, discounts, or advice. Strict JSON only. No prose."""

func price_offer_idea(state: GameState, idea: String, cb: Callable) -> void:
	if not llm.enabled():
		cb.call({})
		return
	var user := JSON.stringify({
		"company": {"name": state.company_name, "idea": state.company_idea,
			"what": state.biz_what, "who": state.biz_who, "era": state.era},
		"new_offer": idea.substr(0, 200)})
	llm.request_json(OFFER_PROMPT, user, LlmClient.OFFER_SCHEMA, func(res: Dictionary):
		if cb.is_valid():
			cb.call(res), {"tier": "clarify"})

const CANDIDATES_PROMPT := """You dress job applicants for RUNWAY!, a satirical startup survival game. The engine already decided every number — each candidate's role, skill 1-5 and weekly ask are FIXED and not yours. For each candidate, in the given order, invent ONLY: name (a plausible human full name, never a real person), quirk (one dry, specific habit, <=60 chars), one_liner (how they'd pitch themselves in one wince-funny sentence, <=90 chars). Match the texture to this company, its era and its business. Skill 5 reads impressive with one red flag; skill 1 reads earnest and alarming. A candidate with source "referral" knows someone on the team — let it show. Never state the numbers. No name may repeat a name in taken_names. Exactly one entry per candidate, same order. Output ONLY the schema."""

## ONE batch dressing call on weeks with arrivals (02 §8.1): fire-and-forget —
## the cards are playable before the reply, which only replaces the words.
func dress_applicants(state: GameState, cb: Callable = Callable()) -> void:
	var payload := SimLabor.dressing_payload(state)
	if payload.is_empty() or not llm.enabled():
		if cb.is_valid():
			cb.call(0)
		return
	llm.request_json(CANDIDATES_PROMPT, JSON.stringify(payload) + "\nDress them.",
		LlmClient.CANDIDATES_SCHEMA, func(res: Dictionary):
			var n := SimLabor.dress_applicants_rows(state, res.get("candidates", []))
			if cb.is_valid():
				cb.call(n), {"tier": "clarify"})

const LEAD_PROMPT := "You name enterprise prospects for RUNWAY!, a satirical startup survival game. You receive the player's company (name, idea, what × who) and N new prospects that just took a first meeting, each with a size band. Invent N fictional companies that would plausibly BUY from this exact business — sector-appropriate, pronounceable, never real companies or people. one_liner: who they are and why they're suddenly shopping, dry, wince-funny, a complete sentence. Return exactly N leads in the order given. Never output numbers, seat counts, or stages."

## ONE batch naming call on weeks with spawns (05 §10). Fire-and-forget — the
## board is playable before the reply, which only replaces the words.
func dress_leads(state: GameState, cb: Callable = Callable()) -> void:
	var payload := SimPipeline.dressing_payload(state)
	if payload.is_empty() or not llm.enabled():
		if cb.is_valid():
			cb.call(0)
		return
	llm.request_json(LEAD_PROMPT, JSON.stringify(payload) + "\nName them.",
		LlmClient.LEAD_SCHEMA, func(res: Dictionary):
			var n := SimPipeline.dress_leads_rows(state, res.get("leads", []))
			if cb.is_valid():
				cb.call(n), {"tier": "clarify"})

const BETS_PROMPT := """You name feature bets for RUNWAY!, a satirical startup survival game. Given the company, its era, what already shipped and what sits on the board, write N candidate feature bets SPECIFIC to this exact business. name: <=28 chars, plain product-speak a PM would write on a card. desc: <=90 chars, dry and wince-funny — what it is and who it is for. kind: quality (the product gets better for everyone), retention (existing customers stay longer), reach (new people get a reason to show up), platform (infrastructure that makes all future building faster — only natural for a company with real scale). ambition: 1 small and safe, 2 a real feature, 3 the big swing. Cover at least two different kinds across the batch. Never numbers, never metric promises, never real companies or people, never a bet already on the board or recently shipped. Exactly `slots` entries. Output ONLY the schema."""

## ONE batch dressing call on weeks the board drew fresh paper (07 §10):
## fire-and-forget — the cards are playable before the reply, which only
## replaces the words and the rung.
func dress_bets(state: GameState, cb: Callable = Callable()) -> void:
	var payload := SimRoadmap.dressing_payload(state)
	if payload.is_empty() or not llm.enabled():
		if cb.is_valid():
			cb.call(0)
		return
	llm.request_json(BETS_PROMPT, JSON.stringify(payload) + "\nName them.",
		LlmClient.BETS_SCHEMA, func(res: Dictionary):
			var n := SimRoadmap.dress_bets(state, res.get("bets", []))
			if cb.is_valid():
				cb.call(n), {"tier": "clarify"})

## Background prefetch of generated event cards.
var disabled := false   # daily seeded runs: authored-only determinism

func prefetch(state: GameState) -> void:
	if disabled or not llm.enabled() or _pending or pool.size() >= 3:
		return
	_pending = true
	llm.request_json(SYSTEM_PROMPT, compose_event_user(state), LlmClient.EVENT_SCHEMA, _on_card)

func _on_card(card: Dictionary) -> void:
	_pending = false
	if card.is_empty():
		return
	if _validate_card(card):
		card["tier"] = "generated"
		card["id"] = "gen_%d" % Time.get_ticks_msec()
		pool.append(card)
	else:
		push_warning("generated card rejected by validator")

## WHAT THE WORLD SAYS WHEN THERE IS NO WORLD TO ASK.
## With no key the written move used to hand back {} and the page did not so much as
## blink: no verdict, no line, no lock, no sound. The founder wrote a paragraph into
## the one screen this whole game is built around and the game answered with silence,
## which reads as broken rather than as unconfigured. So the world answers in its own
## voice instead — it hears the move, it writes it down, and it changes NOTHING.
##
## `effects` is empty ON PURPOSE and must stay empty. A stub that paid out would be
## the game inventing a judgement it never made, which is worse than the silence was.
## No headline, no scene and no cast either, so the turn stays a quiet page and never
## starts a render.
static func keyless_adjudication() -> Dictionary:
	return {
		"interpreted_as": "you write it down",
		"narration": "The world takes note. Nothing changes yet — the phone stays quiet.",
		"verdict": "fine",
		"effects": [],
	}

## Adjudicate the player's own written move for an event. cb gets
## {narration, verdict, effects} (validated), the keyless stub above when there is no
## key at all, or {} when a live call came back empty or failed its validator.
func adjudicate(state: GameState, ev: Dictionary, player_text: String, cb: Callable, dice: Dictionary = {}, tier: String = "assess") -> void:
	if not llm.enabled():
		if cb.is_valid():
			cb.call(keyless_adjudication())
		return
	var user := compose_adjudicate_user(state, ev, player_text, dice)
	llm.request_json(_adjudicate_prompt, user, LlmClient.ADJUDICATE_SCHEMA, func(result: Dictionary):
		if result.is_empty() or not _validate_effects(result.get("effects", []), true):
			if cb.is_valid():
				cb.call({})
			return
		# THE SENTINEL (plan C3): deterministic post-checks. One retry with the
		# errors echoed, then proceed with the sanitized reply — never deadlock.
		var faults := _sentinel(state, result)
		if faults.is_empty():
			if cb.is_valid():
				cb.call(result)
			return
		push_warning("DM sentinel: " + "; ".join(PackedStringArray(faults)))
		var retry_user := user + "\n\nYOUR PREVIOUS REPLY WAS REJECTED FOR: " \
			+ "; ".join(PackedStringArray(faults)) + "\nFix ONLY these and answer again."
		llm.request_json(_adjudicate_prompt, retry_user, LlmClient.ADJUDICATE_SCHEMA, func(second: Dictionary):
			var final := second
			if final.is_empty() or not _validate_effects(final.get("effects", []), true):
				final = result            # the first reply, sanitized below
			_sanitize(state, final)
			if cb.is_valid():
				cb.call(final), {"tier": tier}), {"tier": tier})

## THE CLARIFY PRE-PASS (owner: terra assesses, luna clarifies): one cheap
## call before the dice — does this move need ONE follow-up question?
## cb receives {needs_clarification, question, kind} ({} keyless/failed).
var _clarify_prompt := ""

func clarify(state: GameState, ev: Dictionary, move: String, cb: Callable) -> void:
	if not llm.enabled():
		if cb.is_valid():
			cb.call({})
		return
	if _clarify_prompt == "":
		_clarify_prompt = FileAccess.get_file_as_string("res://data/prompts/clarify.txt")
	var user := JSON.stringify({
		"run_state": {"cash": state.cash, "week": state.week, "era": state.era,
			"customers": state.traction, "crew": _crew_names(state),
			"items": state.items, "budgets": state.budgets,
			# the roofs, by name: with ≥2 of them a physical hire/buy that
			# names none is a real gap (the "for which roof?" rule)
			"sites": state.sites.map(func(s): return String((s as Dictionary).get("name", ""))),
			"offers": state.offers.map(func(o): return {
				"name": o.get("name", ""), "priced": float(o.get("price", 0.0)) > 0.0 or bool(o.get("price_set", false)),
				"price": float(o.get("price", 0.0)), "unit": String(o.get("unit", ""))})},
		"event_card": {"title": String(ev.get("title", "")), "body": String(ev.get("body", "")).left(160)},
		"move": move.substr(0, 300)})
	llm.request_json(_clarify_prompt, user, LlmClient.CLARIFY_SCHEMA, func(res: Dictionary):
		if cb.is_valid():
			cb.call(res), {"tier": "clarify"})

static func _crew_names(state: GameState) -> Array:
	var out: Array = []
	if state.founder_name != "":
		out.append(state.founder_name + " (you)")
	for cf in state.cofounders:
		var n := str(cf.get("name", "")).strip_edges()
		if n != "":
			out.append(n)
	for em in state.employees:
		out.append(str(em.get("name", "")))
	return out

## Deterministic continuity checks: hallucinated cast, premise drift, empty milestones.
func _sentinel(state: GameState, res: Dictionary) -> Array:
	var faults: Array = []
	# 1 — unknown named NPC: every capitalized fund/rival mention must exist
	var known := PackedStringArray()
	for inv in state.investors:
		known.append(String((inv as Dictionary).get("name", "")))
	for rv in state.rivals:
		known.append(String((rv as Dictionary).get("name", "")))
	for ld in state.leads:
		known.append(String((ld as Dictionary).get("name", "")))
	for lg in state.logos:
		known.append(String((lg as Dictionary).get("name", "")))
	var narration := String(res.get("narration", ""))
	# premise guard: money the narration spends must exist (order-of-magnitude)
	var spend_guess := 0
	for eff in res.get("effects", []):
		if String((eff as Dictionary).get("op", "")) == "cash_delta":
			spend_guess = int((eff as Dictionary).get("v", 0))
	if spend_guess < 0 and state.cash + spend_guess < -8_000:
		faults.append("the move spends $%d the company does not have (cash $%d)" % [
			-spend_guess, state.cash])
	# unknown status names die silently in the executor; flag them for a fix
	for eff2 in res.get("effects", []):
		var d: Dictionary = eff2
		if String(d.get("op", "")) == "status" and not SimEngine.STATUS.has(String(d.get("v", ""))):
			faults.append("unknown status '%s' — pick from the fixed catalog" % d.get("v", ""))
	# a raise-verdict week that grants seed money must set the flag (and vice versa)
	var says_round := narration.to_lower().contains("term sheet signed") 		or narration.to_lower().contains("round closes") or narration.to_lower().contains("wire hits")
	var sets_round := false
	for eff3 in res.get("effects", []):
		if String((eff3 as Dictionary).get("op", "")) == "set_flag" 				and String((eff3 as Dictionary).get("v", "")).contains("raised"):
			sets_round = true
	if says_round and not sets_round:
		faults.append("the narration closes a round but no *_raised flag is set")
	return faults

## What survives even a failed retry: strip ops the engine would refuse anyway.
func _sanitize(state: GameState, res: Dictionary) -> void:
	var ok: Array = []
	for eff in res.get("effects", []):
		var d: Dictionary = eff
		if String(d.get("op", "")) == "status" and not SimEngine.STATUS.has(String(d.get("v", ""))):
			continue
		ok.append(d)
	res["effects"] = ok

## The invisible seam: generated card if pooled, else authored.
func next_card(state: GameState, content: ContentDb, rng: SeededRng) -> Dictionary:
	if disabled:
		pool.clear()
	while not pool.is_empty():
		var cand: Dictionary = pool.pop_front()
		var ct := String(cand.get("title", ""))
		var dup := false
		for pt in state.played_events.slice(maxi(state.played_events.size() - 4, 0)):
			if String(pt).similarity(ct) > 0.6:
				dup = true
				break
		if not dup:
			# a returning lead character IS a repeat, whatever the title says
			var blob := ct + " " + String(cand.get("body", ""))
			for nm in recent_names(state):
				if blob.contains(String(nm)):
					dup = true
					break
		if not dup:
			return cand
		push_warning("event pool: dropped near-duplicate '%s'" % ct)
	var eligible := content.eligible_events(state)
	if eligible.is_empty():
		return {}
	# NEVER THE SAME CARD TWICE while anything fresh remains (the Unity
	# probe caught the pool-dry re-deal; this draw had the same hole)
	if not state.played_events.is_empty():
		var fresh: Array = []
		for ev in eligible:
			if not state.played_events.has(String(ev.get("title", ""))):
				fresh.append(ev)
		if not fresh.is_empty():
			eligible = fresh
	for ev in eligible:
		if state.future_weights.has(ev.get("id", "")):
			state.future_weights.erase(ev.get("id", ""))
			return ev
	return rng.weighted_pick(eligible)

## THE OP REGISTRY, validator half (docs/design/00-spine.md §7). This list, the
## schema enum in llm_client.gd and the executor in garage_view_screen.gd are
## ONE list at three sites; a twin test pins them equal. `price_offer` was in
## the schema and the executor but missing HERE, so any DM reply that priced an
## offer was rejected wholesale — the bug this list's pin test now prevents.
const ALLOWED_OPS := ["cash_delta", "product_delta", "traction_delta", "morale_delta", "hype_delta", "set_flag", "status", "clock", "set_price", "price_offer", "set_marketing", "hire", "take_loan", "spend", "set_budget", "push_lead", "open_site", "close_site", "reassign_employee", "move_machine", "tag_offer", "tag_spend_line", "refinance_note", "fire_account", "retire_product", "pivot_audience", "pivot_product", "pitch_investor", "sign_instrument", "send_offer", "set_relief"]

func _validate_effects(effects, allow_empty: bool = false) -> bool:
	if not (effects is Array):
		return false
	if effects.is_empty():
		return allow_empty
	for eff in effects:
		if not (eff is Dictionary and ALLOWED_OPS.has(String(eff.get("op", "")))):
			return false
	return true

func _validate_card(card: Dictionary) -> bool:
	if not (card.has("title") and card.has("body") and card.has("choices")):
		return false
	if String(card["title"]).length() > 60 or String(card["body"]).length() > 500:
		return false
	var choices = card["choices"]
	if not (choices is Array) or choices.size() < 2 or choices.size() > 4:
		return false
	for ch in choices:
		if not (ch is Dictionary and ch.has("label") and ch.has("effects")):
			return false
		if String(ch["label"]).length() > 60:
			return false
		if not _validate_effects(ch["effects"]):
			return false
	return true
