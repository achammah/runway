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
const WORLDGEN_PROMPT := """You are the world-builder for RUNWAY!, a satirical startup survival game. Given the company (its pitch, what it sells, to whom), invent THE WORLD IT WAS BORN INTO — specific to this exact business, never generic. market: honest intuitive numbers (how many real buyers exist for THIS product; how many weeks such a customer stays before churning) and a dry one-liner about this market's mood. investors: three funds/angels that would plausibly circle THIS space, each mapped to one archetype from the enum; thesis in their own voice ABOUT THIS MARKET (never the words 'growth is the only truth' or any stock phrase); a concrete trait, a bond connecting them to this founder's world, a flaw, and a SECRET the founder must never be told directly. rivals: two companies already competing for these exact customers — name (pronounceable, no real companies), what they do in one line, how strong they look, and three tactics they actually use in this market. Dry, wince-funny, PG-13, no real companies or people."""

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
		{"max_tokens": 1400})

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
		parts.append(("\nTHE DICE ARE CAST: two d20s rolled: %d and %d. Competences: %s. "
			+ "Advantage/disadvantage BY STAT (from items, hires, conditions): %s. "
			+ "Pick the governing stat; the engine will use the HIGHER die under advantage, "
			+ "the LOWER under disadvantage, the FIRST otherwise, add (stat - 3), and compare "
			+ "to your DC. Set the DC honestly (floors: routine 6-8, solid 9-11, bold 12-14, "
			+ "wild 15-16) and narrate the outcome the FINAL total earns.") % [
			int(dice.get("a", 10)), int(dice.get("b", 10)),
			JSON.stringify(state.competences), JSON.stringify(dice.get("adv_map", {}))])
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
	return "\n".join(out)

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
func adjudicate(state: GameState, ev: Dictionary, player_text: String, cb: Callable, dice: Dictionary = {}) -> void:
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
				cb.call(final)))

## Deterministic continuity checks: hallucinated cast, premise drift, empty milestones.
func _sentinel(state: GameState, res: Dictionary) -> Array:
	var faults: Array = []
	# 1 — unknown named NPC: every capitalized fund/rival mention must exist
	var known := PackedStringArray()
	for inv in state.investors:
		known.append(String((inv as Dictionary).get("name", "")))
	for rv in state.rivals:
		known.append(String((rv as Dictionary).get("name", "")))
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
	for ev in eligible:
		if state.future_weights.has(ev.get("id", "")):
			state.future_weights.erase(ev.get("id", ""))
			return ev
	return rng.weighted_pick(eligible)

const ALLOWED_OPS := ["cash_delta", "product_delta", "traction_delta", "morale_delta", "hype_delta", "set_flag", "status", "clock", "set_price", "set_marketing", "hire", "take_loan"]

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
