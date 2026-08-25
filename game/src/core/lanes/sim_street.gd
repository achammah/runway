class_name SimStreet
extends RefCounted
## LANE 03 — THE STREET (rivals + macro weather). Spec: docs/design/03-rivals-macro.md
##
## Two systems share one file because they share one page and one lesson: the
## world outside the company acts on it every single week, and none of it is
## about you personally.
##
##   THE RIVALS act on capacity, strategy and relative position — a war chest
##   (`vigor`), a strategic bent (`focus`), a price they hold against the
##   street (`price_posture`) and a share of voice (`hype`). They do not roll a
##   ratchet upward; they SPEND to move, and every move is a named business
##   dynamic with a receipt that says which one.
##
##   THE MACRO is the weather. A season cycle the trend mean-reverts around, and
##   rare credit shocks that reprice every valuation and term sheet at once —
##   announced one week early, because in the real world sentiment turns before
##   the money does.
##
## The spine calls, in tick order (docs/design/00-spine.md §1, HOOKS.md):
##   tick_pre   tick §6a/§6b — rivals act, then the weather turns
##   tick_money the money section — this lane owns NO P&L lane (see below)
##   tick_post  after the week's record is written and can be read back
## and outside the tick: directives() feeds the DM block, attention() feeds
## every bang in the game through SimEngine.attention_items.
##
## SALTS (docs/design/00-spine.md §3): 30 weekly action pick · 31 poach roll ·
## 32 hq disruptor spawn · 80 macro shock. The macro walk RE-DRAWS the frozen
## salt-7 stream — same single number, mean-reverted — so owning it shifts
## nobody else's dice. Salt 6 is a tombstone.

# ── the era attention ladder (§2.3) ─────────────────────────────────────────
## Competitive response is threshold-triggered in the real world: incumbents do
## not answer a challenger nobody has heard of. Macro ignores the ladder — the
## credit cycle prices a garage exactly as gladly as an incumbent.
const ERAS := ["garage", "coworking", "office", "floor", "hq"]

## THE FIXED SCAN ORDER (§2.4). The cumulative scan walks this list, so the
## order is part of the determinism contract: reordering it re-rolls history.
const ACTIONS := ["price_cut", "launch", "blitz", "poach", "stumble", "sniff", "quiet"]

## Response lags, in weeks — a real price move takes a quarter to answer and
## poaching runs recruiting cycles. Also the anti-spam floor.
const COOLDOWNS := {"price_cut": 4, "launch": 5, "blitz": 3, "poach": 6,
	"stumble": 8, "sniff": 12, "quiet": 0}

## What the street tab's action log calls each move.
const LABELS := {"price_cut": "cut prices", "launch": "launched", "blitz": "ad blitz",
	"poach": "poach attempt", "stumble": "stumbled", "sniff": "asking about you",
	"quiet": "quiet"}

const LOG_CAP := 6           ## the rival's own rap sheet, oldest dropped
const BEATS_CAP := 4         ## the DM never gets more than four facts a week
const RIVAL_CAP := 3         ## the street holds three names, no more
const BORN_RIVALS := 2       ## what worldgen births — fewer means a slot came free
const MONEY_SECRET := "quietly running out of money"

# ── the authored one-liner pools (§10) ───────────────────────────────────────
## Receipts teach the dynamic BY NAME — price war, share of voice, execution
## risk — in the game's dry voice, at zero tokens. The pick is the salt-30 d2
## draw, so the same week always tells the same story.
const LINES := {
	"price_cut": [
		"%s cut their price. The street noticed. — a price war buys share with margin",
		"%s went cheaper. The going rate just followed them down.",
		"%s discounted hard. Margin compression is now everyone's problem.",
		"%s put a sale sign in the window. Your list price reads expensive today."],
	"launch": [
		"%s shipped. It's good. Your product got older overnight.",
		"%s launched the thing they teased. Buyers are comparing notes.",
		"%s cut a ribbon on a real feature. The category ladder just moved.",
		"%s shipped loud. Relative quality is the only quality the street sees."],
	"blitz": [
		"%s is everywhere this week. Attention is a zero-sum street.",
		"%s bought the billboard, the podcast, and probably your ad slot.",
		"%s is outspending you on being seen. Share of voice buys share of market.",
		"%s made noise. Your quiet got quieter."],
	"poach_win": [
		"%s called %s with a number. The number won.",
		"%s hired %s away. Underpaying is a bet somebody else collects.",
		"%s made %s an offer the payroll sheet couldn't answer."],
	"poach_lose": [
		"%s called %s with a number. %s stayed — this time.",
		"%s went fishing in your team. Nobody bit. The bait will get bigger.",
		"%s tested a loyalty you haven't been paying for."],
	"stumble": [
		"%s had a very public bad week. Their churn is your word of mouth.",
		"%s broke something customers loved. Doors are open.",
		"%s made the news for the wrong reason. Execution risk collects.",
		"%s stumbled. Overextension always invoices eventually."],
	"sniff": [
		"somebody at %s keeps asking what you'd cost.",
		"%s's corp-dev person knows your numbers a little too well.",
		"a banker mentioned %s and your name in one sentence."],
	"disruptor": [
		"a new name, %s, is doing what you do for less. You remember this trick.",
		"%s just launched under your price umbrella. You built that umbrella.",
		"%s is scrappy, cheap, and pointed at your cheapest customers first."],
}

## THE MACRO BANNER, one authored line per weather state (§10). The desk prints
## it, the journal prints it, and they are the same sentence on purpose.
const BANNER := {
	"winter_watch": "the street smells winter. money gets cold next week",
	"funding_winter": "FUNDING WINTER — checks shrink, terms bite",
	"thaw": "the thaw. the street funds again",
	"boom_watch": "the street smells a boom. money warms next week",
	"boom": "BOOM — everyone's a genius, every round oversubscribed",
	"boom_end": "the boom cooled. everyone pretends they called it",
}

## THE DM's FACTS (§9). Engine-formatted, so the narrator never sees a number it
## could change. Priority order when the week is loud: macro, sniff, poach,
## launch, stumble, then the disruptor's arrival.
const BEAT_P := {"macro": 0, "sniff": 1, "poach": 2, "launch": 3, "stumble": 4,
	"disruptor": 5}

# ═══════════════════════════ TICK §6a + §6b ═══════════════════════════
## The street's whole week, in the spine's order: per-rival upkeep → weekly
## action pick (salt 30) → poach (31) → hq disruptor (32), then the shock roll
## (80), the watch→shock transitions, and the mean-reverting trend walk (7).
##
## RIVALS ACT BEFORE THE MARKET, and that is the point: their triggers read LAST
## week's player state (the price you posted, the hype you had when the week
## opened) while their effects land on THIS week's adoption. Conduct responds
## with a lag; consequences are immediate on announcement.
static func tick_pre(state: GameState, rep: Dictionary) -> void:
	var beats: Array = []          # [{p:int, text:String}] — sorted and capped below
	var moves: Array = []          # DIRECTIVES §7 lines for the moves that get no beat
	var alert := ""                # the attention row's ≤40-char label
	_rivals_week(state, rep, beats, moves)
	alert = _macro_week(state, rep, beats)
	if alert == "":
		alert = _rival_alert(beats)
	# priority, then the order they happened — a stable sort, so two runs of the
	# same week can never disagree about which four facts the DM gets
	var idx := 0
	for b in beats:
		(b as Dictionary)["_i"] = idx
		idx += 1
	beats.sort_custom(func(a: Dictionary, c: Dictionary) -> bool:
		if int(a.get("p", 9)) != int(c.get("p", 9)):
			return int(a.get("p", 9)) < int(c.get("p", 9))
		return int(a.get("_i", 0)) < int(c.get("_i", 0)))
	var lines: Array[String] = []
	for b2 in beats:
		if lines.size() >= BEATS_CAP:
			break
		lines.append(String((b2 as Dictionary).get("text", "")))
	state.set_meta("street_beats", lines)
	state.set_meta("street_moves", moves)
	state.set_meta("street_alert", alert)

## THE MONEY SECTION — and this lane writes NOTHING to it, deliberately.
## Rival and macro effects are demand-side and funding-side: they surface as
## statuses, valuations and term-sheet math. Inventing a P&L lane for someone
## else's price cut would be fake accounting. What it DOES leave here is the
## receipt that explains the dent, sitting beside the numbers it dented.
static func tick_money(state: GameState, rep: Dictionary, _m: Dictionary) -> void:
	if not SimEngine.has_status(state, "price_war"):
		return
	var down := int(round((1.0 - SimEngine.street_fair_mult(state)) * 100.0))
	rep["lines"].append("price war on the street: the going rate is down %d%% (%d wks left)"
		% [down, weeks_left(state, "price_war")])

## Nothing needs the closed week. The street's bookkeeping all happened in §6a.
static func tick_post(_state: GameState, _rep: Dictionary) -> void:
	pass

## DM CONTEXT, sections 7 (rivals) and 8 (macro) of the DIRECTIVES block.
## The big beats are already engine-resolved facts; the move lines cover the two
## kinds of conduct too small to earn a beat (a price cut, an ad blitz) but too
## real for the narrator to contradict.
static func directives(state: GameState) -> Array[String]:
	var out: Array[String] = []
	for m in state.get_meta("street_moves", []):
		out.append(String(m))
	for b in state.get_meta("street_beats", []):
		out.append(String(b))
	return out

## ATTENTION ROWS (docs/design/00-spine.md §4) — the single list behind every
## bang. One row for the week the street moved, and the two standing threats a
## founder must be able to see while they run: a live price war, and someone
## circling. Labels are ≤40 characters because the garage ticker prints them.
static func attention(state: GameState) -> Array:
	var rows: Array = []
	var alert := String(state.get_meta("street_alert", ""))
	if alert != "" and not (state.get_meta("street_beats", []) as Array).is_empty():
		rows.append({"desk": "the street", "key": "street_beat", "severity": 2,
			"label": alert.left(40)})
	if SimEngine.has_status(state, "price_war"):
		var down := int(round((1.0 - SimEngine.street_fair_mult(state)) * 100.0))
		rows.append({"desk": "threats", "key": "price_war", "severity": 2,
			"label": ("price war: going rate −%d%%, %d wks left"
				% [down, weeks_left(state, "price_war")]).left(40)})
	if state.has_flag("acquisition_sniff"):
		var who := ""
		for rv in state.rivals:
			if int((rv as Dictionary).get("sniffing", 0)) > 0:
				who = String((rv as Dictionary).get("name", ""))
				break
		if who != "":
			rows.append({"desk": "threats", "key": "acquisition_sniff", "severity": 2,
				"label": ("%s is circling — asking your price" % who).left(40)})
	return rows

# ═══════════════════════════ §6a THE RIVALS ═══════════════════════════
static func _rivals_week(state: GameState, rep: Dictionary, beats: Array, moves: Array) -> void:
	var lvl := street_level(state)
	var power := player_power(state)
	var greedy := offers_overpriced(state)
	var target: Dictionary = _labor_poach_target(state)
	var r30 := SimEngine.rng_for(state, SimEngine.SALT_RIVAL_ACTION)
	# TWO DRAWS PER RIVAL, ALWAYS, IN ARRAY ORDER. A fixed draw count is what
	# stops one rival's branch from shifting the next rival's dice — the single
	# most fragile invariant in this file.
	for rv in state.rivals:
		var rd: Dictionary = rv
		_upkeep(rd)
		var d1 := r30.randf()
		var d2 := int(r30.randi())
		var act := _pick(state, rd, lvl, power, greedy, target, d1)
		_fire(state, rep, beats, moves, rd, act, lvl, d2, target)
	_disruptor(state, rep, beats, lvl)

## PER-RIVAL UPKEEP — deterministic, no draws, every week, every era. Firms grow
## on cash and attention, not dice: this replaces the old random ratchet with
## state-driven drift across the same ~−0.5..+1.2/wk band. Buzz decays like
## adstock, reserves mean-revert, and discounts erode back toward list price
## once a war ends (the airline fare-war pattern).
##
## The order below IS the spec's order: strength reads the vigor and hype the
## rival went to bed with, before this week's decay and reversion touch them.
static func _upkeep(rd: Dictionary) -> void:
	var cds: Dictionary = rd.get("cooldowns", {})
	for k in cds.keys():
		cds[k] = maxi(int(cds[k]) - 1, 0)
	rd["cooldowns"] = cds
	var vigor := float(rd.get("vigor", 55.0))
	var hype := float(rd.get("hype", 20.0))
	rd["strength"] = clampf(float(rd.get("strength", 20.0))
		+ clampf((vigor - 45.0) / 50.0, -0.5, 0.7) + 0.005 * hype, 5.0, 95.0)
	rd["hype"] = maxf(hype - 4.0, 0.0)
	rd["vigor"] = clampf(vigor + (55.0 - vigor) / 12.0, 0.0, 100.0)
	var posture := float(rd.get("price_posture", 1.0))
	rd["price_posture"] = posture + clampf(1.0 - posture, -0.01, 0.01)

## THE GAP: how far they outmatch you. Product is half the answer, buzz a
## quarter, market share a quarter — and share is normalised against TAM (2% of
## the market is full marks) so an Enterprise run's 30 logos weigh the same as a
## Consumer run's 18,000 users.
static func player_power(state: GameState) -> float:
	var tam := maxf(float(state.theta.get("tam", 50_000.0)), 1.0)
	return clampf(0.5 * float(state.product) + 0.25 * float(state.hype)
		+ 25.0 * clampf(float(state.traction) / (0.02 * tam), 0.0, 1.0), 5.0, 95.0)

## Where the founder sits on the attention ladder: 0 garage … 4 hq.
static func street_level(state: GameState) -> int:
	return maxi(ERAS.find(state.era), 0)

## GREED INVITES UNDERCUTTING — the price umbrella. Pricing 15% or more above
## the street's reference is an open invitation to enter beneath you.
static func offers_overpriced(state: GameState) -> bool:
	for o in state.offers:
		var od: Dictionary = o
		var price := float(od.get("price", 0.0))
		var fair := float(od.get("fair_price", 0.0))
		if price > 0.0 and fair > 0.0 and price >= 1.15 * fair:
			return true
	return false

## THE WEEKLY ACTION TABLE (§2.4): eligibility first (a failed gate is weight
## zero, never a re-roll), then conjectural-variation weights — firms act on
## capacity, strategy and relative position — then ONE cumulative scan over
## d1 × Σw in the fixed order. A trailing rival (gap ≤ 0) ships and poaches
## harder: that is catch-up behaviour, and it is why falling behind is loud.
static func _pick(state: GameState, rd: Dictionary, lvl: int, power: float,
		greedy: bool, target: Dictionary, d1: float) -> String:
	var vigor := float(rd.get("vigor", 55.0))
	var hype := float(rd.get("hype", 20.0))
	var strength := float(rd.get("strength", 20.0))
	var posture := float(rd.get("price_posture", 1.0))
	var focus := String(rd.get("focus", "growth"))
	var gap := strength - power
	var cds: Dictionary = rd.get("cooldowns", {})
	var w := {}
	w["price_cut"] = 0.0
	if lvl >= 2 and vigor >= 25.0 and posture > 0.82 and int(cds.get("price_cut", 0)) == 0:
		w["price_cut"] = 8.0 * (2.0 if focus == "price" else 1.0) \
			* (0.5 if posture <= 0.90 else 1.0) + (6.0 if greedy else 0.0)
	w["launch"] = 0.0
	if vigor >= 30.0 and int(cds.get("launch", 0)) == 0:
		w["launch"] = (10.0 * (2.0 if focus == "product" else 1.0) + (4.0 if gap <= 0.0 else 0.0)) \
			* (1.5 if lvl >= 3 else 1.0)
	w["blitz"] = 0.0
	if vigor >= 30.0 and int(cds.get("blitz", 0)) == 0:
		w["blitz"] = (8.0 * (2.0 if focus == "growth" else 1.0)
			+ (4.0 if float(state.hype) >= hype + 15.0 else 0.0)) * (1.5 if lvl >= 3 else 1.0)
	w["poach"] = 0.0
	if lvl >= 2 and vigor >= 40.0 and not target.is_empty() and int(cds.get("poach", 0)) == 0:
		w["poach"] = 4.0 + (4.0 if gap <= 0.0 else 0.0) + (2.0 if focus == "product" else 0.0)
	w["stumble"] = 0.0
	if int(cds.get("stumble", 0)) == 0:
		w["stumble"] = 4.0 + (6.0 if vigor < 30.0 else 0.0) + (4.0 if hype >= 70.0 else 0.0) \
			+ (6.0 if String(rd.get("secret", "")) == MONEY_SECRET else 0.0)
	w["sniff"] = 0.0
	if lvl >= 3 and strength >= 60.0 and gap >= 10.0 and power >= 35.0 \
			and int(rd.get("sniffing", 0)) == 0 and int(cds.get("sniff", 0)) == 0:
		w["sniff"] = 2.0
	w["quiet"] = 30.0 + (15.0 if vigor < 25.0 else 0.0)
	var total := 0.0
	for a in ACTIONS:
		total += float(w[a])
	var roll := d1 * total
	var acc := 0.0
	for a2 in ACTIONS:
		acc += float(w[a2])
		if roll < acc:
			return String(a2)
	return "quiet"

## What the move actually costs them, does to you, and reads like. Player-facing
## installs are gated by the ladder: below coworking the street plays among
## itself and the founder is simply not worth answering (§2.3).
static func _fire(state: GameState, rep: Dictionary, beats: Array, moves: Array,
		rd: Dictionary, act: String, lvl: int, d2: int, target: Dictionary) -> void:
	var name := String(rd.get("name", "a rival"))
	var focus := String(rd.get("focus", "growth"))
	var seen := lvl >= 1
	match act:
		"price_cut":
			# BERTRAND UNDERCUTTING: the reference price itself erodes, so holding
			# your list price through a war is what reads as expensive.
			rd["price_posture"] = maxf(float(rd.get("price_posture", 1.0)) - 0.06, 0.80)
			rd["vigor"] = float(rd.get("vigor", 55.0)) - 8.0
			SimEngine.add_status(state, "price_war", 5 if focus == "price" else 4)
			if seen:
				rep["events"].append(LINES["price_cut"][d2 % LINES["price_cut"].size()] % name)
				moves.append("%s cut prices ~%d%% this week." % [name,
					int(round((1.0 - float(rd["price_posture"])) * 100.0))])
		"launch":
			# VERTICAL DIFFERENTIATION: their step up is your relative step down.
			# Your product meter is untouched — you lost no code, only appeal.
			rd["strength"] = minf(float(rd.get("strength", 20.0)) + 4.0, 95.0)
			rd["hype"] = minf(float(rd.get("hype", 20.0)) + 15.0, 100.0)
			rd["vigor"] = float(rd.get("vigor", 55.0)) - 12.0
			if seen:
				SimEngine.add_status(state, "outshipped", 3)
				rep["events"].append(LINES["launch"][d2 % LINES["launch"].size()] % name)
				beats.append({"p": BEAT_P["launch"],
					"text": "THE STREET: %s launched for real this week (strength %d). Customers are comparing."
						% [name, int(round(float(rd["strength"])))]})
		"blitz":
			# SHARE OF VOICE BUYS SHARE OF MARKET: attention is zero-sum, and the
			# status is the decaying adstock of their week of noise.
			rd["hype"] = minf(float(rd.get("hype", 20.0)) + 20.0, 100.0)
			rd["vigor"] = float(rd.get("vigor", 55.0)) - 15.0
			if seen:
				SimEngine.add_status(state, "rival_fud", 2)
				rep["events"].append(LINES["blitz"][d2 % LINES["blitz"].size()] % name)
				moves.append("%s is buying every ad slot this week." % name)
		"poach":
			resolve_poach(state, rep, beats, rd, d2, target)
		"stumble":
			# EXECUTION RISK CORRELATES WITH OVEREXTENSION: the loud, thin ones
			# break loudest, and the worldgen secret finally pays off mechanically.
			var broke := String(rd.get("secret", "")) == MONEY_SECRET
			rd["strength"] = maxf(float(rd.get("strength", 20.0)) - (12.0 if broke else 6.0), 5.0)
			rd["vigor"] = maxf(float(rd.get("vigor", 55.0)) - (20.0 if broke else 10.0), 0.0)
			rd["hype"] = float(rd.get("hype", 20.0)) * 0.5
			if seen:
				SimEngine.add_status(state, "rival_stumbled", 2)
				rep["events"].append(LINES["stumble"][d2 % LINES["stumble"].size()] % name)
				beats.append({"p": BEAT_P["stumble"],
					"text": "THE STREET: %s stumbled publicly — their customers are looking around. A door is open this week." % name})
		"sniff":
			# M&A HANDOFF ONLY. This lane prices nothing and spawns no offer: it
			# marks the interest and lets it charge the room until 08 courts it.
			rd["sniffing"] = state.week
			state.set_flag("acquisition_sniff")
			if seen:
				rep["events"].append(LINES["sniff"][d2 % LINES["sniff"].size()] % name)
				beats.append({"p": BEAT_P["sniff"],
					"text": "THE STREET: quiet word is %s is asking around about acquiring the company. Do not resolve it — let it charge the room." % name})
		_:
			# CONSOLIDATION: they bank cash and say nothing. Silence in the log
			# is information too — a quiet rival is a rival reloading.
			rd["vigor"] = minf(float(rd.get("vigor", 55.0)) + 6.0, 100.0)
	rd["vigor"] = clampf(float(rd.get("vigor", 55.0)), 0.0, 100.0)
	rd["last_action"] = act
	if act != "quiet":
		rd["weeks_since_move"] = 0
	var cds: Dictionary = rd.get("cooldowns", {})
	var cd := int(COOLDOWNS.get(act, 0))
	if act == "price_cut" and focus == "price":
		cd = 3
	if cd > 0:
		cds[act] = cd
	rd["cooldowns"] = cds
	var lg: Array = rd.get("log", [])
	lg.append("wk%d: %s" % [state.week, String(LABELS.get(act, act))])
	while lg.size() > LOG_CAP:
		lg.remove_at(0)
	rd["log"] = lg

## PAY-GAP ARBITRAGE (§5.4). Underpaid people answer recruiter calls; the target
## and the wage come from the labor lane, never from here. The attempt costs the
## rival whether or not it lands — recruiting is not free.
##
## Public so a suite can hand it a stubbed target and pin the whole resolution
## without needing a live labor desk. Returns true when the person left.
static func resolve_poach(state: GameState, rep: Dictionary, beats: Array,
		rd: Dictionary, d2: int, target: Dictionary) -> bool:
	var name := String(rd.get("name", "a rival"))
	var war_chest := float(rd.get("vigor", 55.0))   # the budget they had when they made the call
	rd["vigor"] = war_chest - 10.0
	if target.is_empty():
		return false
	var who := String(target.get("name", "someone"))
	var p := poach_odds(float(target.get("pay_gap", 0.0)), war_chest)
	var won := SimEngine.rng_for(state, SimEngine.SALT_RIVAL_POACH).randf() < p
	# THE HANDOFF (docs/design/00-spine.md §4): the crew desk bangs on this, and
	# a failed attempt is where the labor lane's counter-offer season starts.
	state.set_meta("poach_wk", state.week)
	state.set_meta("poach_name", who)
	if won:
		# They leave BEFORE this week's GTM head-count and before payroll — the
		# week you lose someone is the week you feel it, not the week after.
		var i := int(target.get("index", -1))
		if i >= 0 and i < state.employees.size():
			state.employees.remove_at(i)
		state.morale = clampi(state.morale - 6, 0, 100)
		rd["strength"] = minf(float(rd.get("strength", 20.0)) + 2.0, 95.0)
		rep["events"].append(LINES["poach_win"][d2 % LINES["poach_win"].size()] % [name, who])
		beats.append({"p": BEAT_P["poach"],
			"text": "THE STREET: %s tried to poach %s this week — and they left. The team noticed." % [name, who]})
		return true
	else:
		# THE WARNING SHOT: the salary conversation is coming whether or not you
		# start it. 02 reads these and raises the ask (counter-offer dynamics).
		state.set_meta("poach_failed_wk", state.week)
		state.set_meta("poach_failed_name", who)
		var pool: Array = LINES["poach_lose"]
		var pick := d2 % pool.size()
		rep["events"].append(String(pool[pick]) % ([name, who, who] if pick == 0 else [name]))
		beats.append({"p": BEAT_P["poach"],
			"text": "THE STREET: %s tried to poach %s this week — they stayed, this time. The team noticed." % [name, who]})
	return false

## THE ODDS, exact (§5.4). The curve is anchored at a 15% gap on an average war
## chest; a 40% gap with money behind it is better than even. The 0.70 cap is
## the lesson: even flush acquirers lose recruiting battles.
## `vigor` is the war chest they had when they picked up the phone.
##
## WHO IS WORTH CALLING is the labor lane's threshold, not this one's — this
## only prices whoever it hands over, so the two lanes can move that bar without
## touching each other.
static func poach_odds(pay_gap: float, vigor: float) -> float:
	return clampf(0.15 + 1.2 * (pay_gap - 0.15) + 0.003 * (vigor - 50.0), 0.05, 0.70)

# ── the labor interface ──────────────────────────────────────────────────────
## THE POACH TARGET (§5.4): the labor lane names the most underpaid person a
## rival would actually call — {index, name, salary, market_salary, pay_gap} —
## or {} when there is nobody worth calling. Empty zeroes the poach weight: no
## shim, no fake wages, no phantom employee. This lane never invents a salary.
static func _labor_poach_target(state: GameState) -> Dictionary:
	return SimLabor.poach_target(state)

# ── §6 the disruptor ─────────────────────────────────────────────────────────
## LOW-END DISRUPTION (Christensen): incumbents build the price umbrella that
## attackers live under. At hq you ARE the reference price, so a cheap name
## appears beneath you. And when the street loses a company — acquired, dead —
## the vacuum re-opens the spawn at any era (docs/design/DECISIONS.md): markets
## do not stay two-horse races because you got comfortable.
static func _disruptor(state: GameState, rep: Dictionary, beats: Array, lvl: int) -> void:
	var slot_freed := state.rivals.size() < BORN_RIVALS
	if state.rivals.size() >= RIVAL_CAP or not (lvl >= 4 or slot_freed):
		return
	var r32 := SimEngine.rng_for(state, SimEngine.SALT_RIVAL_DISRUPTOR)
	if r32.randf() >= 0.04:      # ~1 per 25 weeks
		return
	var name := WorldGen.make_name(r32)
	state.rivals.append({
		"name": name, "what": "",
		"strength": 12.0 + r32.randf_range(0.0, 8.0),
		"tactics": WorldGen.RIVAL_TACTICS[0],
		"weeks_since_move": 0, "secret": "",
		"vigor": 70.0 + r32.randf_range(0.0, 20.0), "hype": 30.0,
		"focus": "price", "price_posture": 0.90,
		"last_action": "", "log": [], "cooldowns": {}, "sniffing": 0,
	})
	rep["events"].append(LINES["disruptor"][int(r32.randi()) % LINES["disruptor"].size()] % name)
	beats.append({"p": BEAT_P["disruptor"],
		"text": "THE STREET: a new name, %s, is undercutting from below. Incumbents ignore these at their own funeral." % name})

# ═══════════════════════════ §6b THE MACRO ═══════════════════════════
## The weather, in the spine's order: the shock roll (salt 80, one draw ALWAYS),
## the watch→shock transitions, the cooldown tick, and then the trend walk on
## the frozen salt-7 stream. Returns the attention label a macro week deserves,
## or "" when the sky did nothing.
##
## Macro runs at EVERY era. The lesson: markets do not care that you exist, but
## the economy prices you anyway.
static func _macro_week(state: GameState, rep: Dictionary, beats: Array) -> String:
	var alert := ""
	var r80 := SimEngine.rng_for(state, SimEngine.SALT_MACRO_SHOCK)
	var d := r80.randf()                              # drawn every week, fixed count
	# THE SPACING CLOCK ticks first, and `cool` keeps the value the WEEK opened
	# with. A shock ending below re-arms it to a full 20, and because the roll
	# reads this local rather than the meta, the week a winter thaws can never
	# also be the week the next one is announced.
	var cool := int(state.get_meta("shock_cool", 0))
	state.set_meta("shock_cool", maxi(cool - 1, 0))
	var expired: Array = rep.get("expired", [])

	# ── the pre-announcement becomes the thing (sentiment precedes term sheets)
	if expired.has("winter_watch"):
		var dur := r80.randi_range(6, 10)
		SimEngine.add_status(state, "funding_winter", dur)
		rep["events"].append(BANNER["funding_winter"])
		beats.append({"p": BEAT_P["macro"],
			"text": "MACRO: funding winter, %d wks left — valuations 0.6x, rounds smaller and meaner. Money scenes are hostile." % dur})
		alert = "funding winter — raise money later"
	elif expired.has("boom_watch"):
		var dur2 := r80.randi_range(6, 10)
		SimEngine.add_status(state, "boom", dur2)
		rep["events"].append(BANNER["boom"])
		beats.append({"p": BEAT_P["macro"],
			"text": "MACRO: boom, %d wks left — valuations 1.3x, term sheets sweeten. Everyone is a genius this quarter." % dur2})
		alert = "boom — raise while money is warm"
	elif expired.has("funding_winter"):
		cool = 20
		state.set_meta("shock_cool", cool)
		rep["events"].append(BANNER["thaw"])
		beats.append({"p": BEAT_P["macro"], "text": "MACRO: the thaw — the street funds again."})
		alert = "the thaw — the street funds again"
	elif expired.has("boom"):
		cool = 20
		state.set_meta("shock_cool", cool)
		rep["events"].append(BANNER["boom_end"])
		beats.append({"p": BEAT_P["macro"], "text": "MACRO: the boom cooled — the street is ordinary again."})
		alert = "the boom cooled — money is ordinary"

	# ── the roll: rare, pre-announced, and spaced by the cooldown after one ends
	if state.week >= 8 and cool == 0 and not _weather(state):
		if d < 0.010:
			SimEngine.add_status(state, "winter_watch", 1)
			rep["events"].append(BANNER["winter_watch"])
			beats.append({"p": BEAT_P["macro"],
				"text": "MACRO: the street smells a funding winter — from next week valuations compress and term sheets tighten. Investors already talk colder."})
			alert = "winter watch — money cools next week"
		elif d < 0.020:
			SimEngine.add_status(state, "boom_watch", 1)
			rep["events"].append(BANNER["boom_watch"])
			beats.append({"p": BEAT_P["macro"],
				"text": "MACRO: the street smells a boom — from next week money runs warm and careless."})
			alert = "boom watch — money warms next week"
	elif alert == "" and SimEngine.has_status(state, "funding_winter"):
		# a live winter is standing weather: the DM must keep talking cold
		beats.append({"p": BEAT_P["macro"],
			"text": "MACRO: funding winter, %d wks left — valuations 0.6x, rounds smaller and meaner. Money scenes are hostile."
				% weeks_left(state, "funding_winter")})
	elif alert == "" and SimEngine.has_status(state, "boom"):
		beats.append({"p": BEAT_P["macro"],
			"text": "MACRO: boom, %d wks left — valuations 1.3x, term sheets sweeten. Everyone is a genius this quarter."
				% weeks_left(state, "boom")})
	state.macro_season = season(state)

	# ── §7 the trend walk: the SAME single salt-7 draw, mean-reverted around the
	# season cycle. Owning it never shifts another subsystem's dice.
	var r7 := SimEngine.rng_for(state, SimEngine.SALT_TREND)
	state.market_trend = clampf(state.market_trend
		+ (cycle_target(state) - state.market_trend) * 0.15
		+ r7.randf_range(-1.0, 1.0) * float(state.theta.get("trend_vol", 0.02)), 0.5, 1.5)
	var band := trend_band(state.market_trend)
	if band != String(state.get_meta("season_band", "")):
		if String(state.get_meta("season_band", "")) != "":
			rep["lines"].append("the street turned: %s" % band)
		state.set_meta("season_band", band)
	return alert

## THE SEASON CYCLE (§7.1) — a business cycle decomposed to one stylised
## frequency: a 52-week sine the trend is pulled toward, shifted by whatever
## weather is live. Pure function, no storage, no draws: demand has weather, and
## the weather is readable a season ahead.
static func cycle_target(state: GameState) -> float:
	var phase := absi(state.sim_seed) % 52
	var t := 1.0 + 0.12 * sin(TAU * float(state.week + phase) / 52.0)
	if SimEngine.has_status(state, "funding_winter") or SimEngine.has_status(state, "winter_watch"):
		t -= 0.10
	if SimEngine.has_status(state, "boom") or SimEngine.has_status(state, "boom_watch"):
		t += 0.10
	return t

## The banner's read on the trend. Macro deliberately does NOT install
## market_tailwind/market_headwind — the trend already multiplies adoption, and
## counting the weather twice would be a lie the receipts could not explain.
static func trend_band(trend: float) -> String:
	if trend >= 1.10:
		return "tailwinds"
	if trend <= 0.90:
		return "headwinds"
	return "calm"

## The season with its consequence attached — demand has weather, and the desk
## says what this week's weather does to a sale rather than making you infer it.
static func season_read(trend: float) -> String:
	match trend_band(trend):
		"tailwinds":
			return "tailwinds — the street buys"
		"headwinds":
			return "headwinds — wallets closed"
		_:
			return "calm — no help, no headwind"

## The persisted weather word (state.macro_season) — what 08 reads when it
## prices an exit, without parsing the status list.
static func season(state: GameState) -> String:
	if SimEngine.has_status(state, "funding_winter") or SimEngine.has_status(state, "winter_watch"):
		return "winter"
	if SimEngine.has_status(state, "boom") or SimEngine.has_status(state, "boom_watch"):
		return "boom"
	return "steady"

static func _weather(state: GameState) -> bool:
	for n in ["winter_watch", "boom_watch", "funding_winter", "boom"]:
		if SimEngine.has_status(state, String(n)):
			return true
	return false

# ── the desk's word maps (§11) ───────────────────────────────────────────────
## NEVER A RAW FLOAT ON THE PAGE. Reading who is flush and who fights on price
## IS the counterplay, so the words are the interface and they live here, once,
## for both engines' desks.
static func vigor_word(v: float) -> String:
	if v >= 70.0:
		return "flush"
	if v >= 45.0:
		return "steady"
	if v >= 25.0:
		return "tight"
	return "bleeding"

static func posture_word(p: float) -> String:
	if p <= 0.94:
		return "undercutting"
	if p >= 1.06:
		return "premium"
	return "at market"

static func hype_word(h: float) -> String:
	if h >= 60.0:
		return "loud"
	if h >= 30.0:
		return "buzzing"
	return "quiet"

## The four word-reads of a rival, joined — the street tab's second line.
static func posture_line(rd: Dictionary) -> String:
	return "%s  ·  %s  ·  fights on %s  ·  %s" % [
		vigor_word(float(rd.get("vigor", 55.0))),
		posture_word(float(rd.get("price_posture", 1.0))),
		String(rd.get("focus", "growth")),
		hype_word(float(rd.get("hype", 20.0)))]

# ── small shared reads ───────────────────────────────────────────────────────
## How long a status has to run — the desk prints it, the receipts count it down.
static func weeks_left(state: GameState, name: String) -> int:
	for s in state.statuses:
		if String((s as Dictionary).get("name", "")) == name:
			return int((s as Dictionary).get("weeks_left", 0))
	return 0

## The ≤40-char label for a week the rivals moved but the sky did not.
static func _rival_alert(beats: Array) -> String:
	var best := 99
	var label := ""
	for b in beats:
		var p := int((b as Dictionary).get("p", 9))
		if p >= best:
			continue
		var txt := String((b as Dictionary).get("text", ""))
		if txt.contains("acquiring the company"):
			label = "someone is asking what you cost"
		elif txt.contains("poach"):
			label = "a rival is calling your people"
		elif txt.contains("launched for real"):
			label = "a rival shipped — you look older"
		elif txt.contains("stumbled publicly"):
			label = "a rival stumbled — a door is open"
		elif txt.contains("undercutting from below"):
			label = "a cheaper rival just appeared"
		else:
			continue
		best = p
	return label

# ── THE TWO SEAMS THE SPINE LEFT OPEN ────────────────────────────────────────
## Both flipped: tick_pre now owns §6a and §6b. The legacy salt-6 ratchet is
## retired (its number is a tombstone, never reassigned) and the salt-7 walk is
## re-drawn HERE — same single number, same stream, mean-reverting.
const OWNS_RIVALS := true
const OWNS_MACRO := true
