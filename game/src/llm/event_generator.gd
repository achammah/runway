class_name EventGenerator
extends Node
## Tier-2 generation + free-move adjudication (PRD §7). Prefetches event cards
## in the background; adjudicates the player's own written moves. Both flow
## through the same op whitelist and clamps — the LLM writes flavor, never rules.

var llm: LlmClient
var pool: Array = []
var _pending := false

const SYSTEM_PROMPT := """You write event cards for RUNWAY!, a satirical startup survival game. Voice: dry, specific, wince-funny. Body 60 words max. Choice labels 8 words max. Never real companies or people. Never break the fourth wall. You receive the run state as JSON, including the player's company_name and what it does (company_does) — write events that are SPECIFIC to that business (its customers, its industry's absurdities, its failure modes), and refer to the company by name when natural. Output ONLY a card matching the schema. Effects use ONLY the allowed ops within sane ranges (meter deltas within ±15; cash_delta proportionate to the era in the state — ±2000 garage, ±10k coworking, ±60k office, ±250k floor, ±1M hq). Match the event to the era and its cast: the state carries era_name, staff (named employees with burnout levels), rounds_raised and board — a garage event smells of ramen, an HQ event of lawyers; a Service business bills hours and juggles clients, a Marketplace juggles two sides, Hardware waits on parts; name a staff member when one fits, and never invent people who are not in the state. Choices must be genuine dilemmas — no strictly-correct option. Reference at least one specific item, cofounder, or flag from the state. The state includes recent_actions — the log of what the player actually did each week. USE IT: create continuity and follow-ups. Some weeks, instead of a problem, write an OPPORTUNITY that grows directly out of a recent action (a prospect who saw the marketing post and liked it, a demo attendee who wants an intro, a customer who mentioned them somewhere) — opportunities still carry tradeoffs, never free wins."""

const ADJUDICATE_PROMPT := """You are the world of RUNWAY!, a satirical startup survival game, adjudicating a founder's free-form action during an event. You receive the full run state — company, business_model (what × who), funding_path, employees, customers, product_version, items owned, cofounders with roles and commitment, archetype competences, meters — then the event and the player's written move. Judge it fairly but the world is harsh, and CONTEXT-AWARE: concrete plans that use things the founder ACTUALLY HAS work better; a bootstrapped company can be scrappy but can't outspend problems; a VC-backed one has money but answers for it; enterprise sales are slow and relationship-driven, consumer needs volume and virality, hardware makes everything slower and costlier; part-time cofounders are less available; more customers means more to lose. Vague, magical, or entitled answers backfire with comedy. narration: max 45 words, second person, dry and wince-funny. verdict: brilliant / fine / risky / backfired. effects: 1-3 ops from the whitelist, magnitudes proportionate to the era in the state (cash within ±2000 in the garage, scaling up by era; meters within ±12 always). Staff named in the state are real: leaning on a cooked employee is risky, and plans ignoring an investor board draw friction. Never more than one strongly positive effect unless the plan is genuinely brilliant AND grounded in what the founder actually has."""

var _adjudicate_prompt := ""

func _init(p_llm: LlmClient) -> void:
	llm = p_llm
	# the production adjudicator prompt ships as data; the const is the fallback
	if FileAccess.file_exists("res://data/prompts/adjudicator.txt"):
		_adjudicate_prompt = FileAccess.get_file_as_string("res://data/prompts/adjudicator.txt")
	if _adjudicate_prompt.strip_edges() == "":
		_adjudicate_prompt = ADJUDICATE_PROMPT

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

## User-message composers (also exercised directly by tests/smoke.gd).
func compose_event_user(state: GameState) -> String:
	return "Run state:\n" + JSON.stringify(state.to_digest()) + _arc_block(state) + "\nWrite one new event card for this exact moment."

func compose_adjudicate_user(state: GameState, ev: Dictionary, player_text: String) -> String:
	return "Run state:\n%s%s\n\nEvent: %s — %s\n\nThe player writes their own move instead of picking an option:\n\"%s\"\n\nAdjudicate it." % [
		JSON.stringify(state.to_digest()), _arc_block(state), String(ev.get("title", "")), String(ev.get("body", "")), player_text.substr(0, 300)]

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
func adjudicate(state: GameState, ev: Dictionary, player_text: String, cb: Callable) -> void:
	if not llm.enabled():
		cb.call(keyless_adjudication())
		return
	llm.request_json(_adjudicate_prompt, compose_adjudicate_user(state, ev, player_text), LlmClient.ADJUDICATE_SCHEMA, func(result: Dictionary):
		if result.is_empty() or not _validate_effects(result.get("effects", []), true):
			cb.call({})
		else:
			cb.call(result))

## The invisible seam: generated card if pooled, else authored.
func next_card(state: GameState, content: ContentDb, rng: SeededRng) -> Dictionary:
	if disabled:
		pool.clear()
	if not pool.is_empty():
		return pool.pop_front()
	var eligible := content.eligible_events(state)
	if eligible.is_empty():
		return {}
	for ev in eligible:
		if state.future_weights.has(ev.get("id", "")):
			state.future_weights.erase(ev.get("id", ""))
			return ev
	return rng.weighted_pick(eligible)

const ALLOWED_OPS := ["cash_delta", "product_delta", "traction_delta", "morale_delta", "hype_delta", "set_flag"]

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
