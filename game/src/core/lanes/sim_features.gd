class_name SimFeatures
extends RefCounted
## LANE — THE FEATURE INVENTORY behind WHAT WE MAKE. Spec:
## docs/design/DECISIONS.md (PRODUCT desk — corrected understanding, THE
## KANBAN WALL, its scale ladder) + docs/design/DAG2.md + mockups 16/17.
##
## WHAT THE LANE OWNS (DAG2 W2 L-MAKE):
##   · the inventory — state.features, born at world gen (L-GEN) or seeded
##     from the offers on a keyless/old save, grown by LANDED ROADMAP BETS
##   · keep-costs — features are never free; the weekly sum is the product's
##     upkeep. THE MONEY IS PACKAGE-GATED: the `feature_keep` P&L lane does
##     not exist in the fixed money record yet, so tick_money stays inert and
##     the coordinator's package activates the one billing line (see the seam
##     comment in tick_money). Until then keep_total() is display truth only.
##   · solidity — solid | creaky | breaking, the debt jar's per-feature FACE.
##     THE ONE-TAX LAW: the jar (state.tech_debt) keeps every mechanical
##     consequence it already has (SimRoadmap.debt_drag velocity, the outage
##     roll, the build disadvantage). Creaks NEVER tax anything themselves —
##     they are the jar made pointable. The weekly reconcile maps jar level ↔
##     creak load:  target = ceil((debt − 40) / 15)  (0 at or below 40, the
##     jar's own free line), load = creaky×1 + breaking×2, one transition per
##     week toward target, plumbing first (the debt concentrates in the
##     plumbing). The desk prints the tax as (1 − debt_drag) — the jar's own
##     number, displayed through the creaks, applied exactly once.
##   · promised-vs-measured — a landed feature's `measured` stays 0 (unknown)
##     for MEASURE_WEEKS, then settles at the landing's ACTUAL payoff units
##     (the exact engine delta the dice granted: BET_PAYOFF[amb][band]) times
##     a salted market spread of 0.75..1.25 (SALT_FEAT_MEASURED). The promise
##     the card advertised is the "fine"-band payoff, recovered from the bet
##     record while it lives in the 8-launch history; when history forgot,
##     the fallback base is 4 (the matrix's low-mid landing).
##   · THE SHELF — 3..5 candidate ideas, regenerated deterministically per
##     (seed, week) on SALT_FEAT_SHELF from era + business type + the wall's
##     gaps (a missing job draws its candidate first; a creak draws a rebuild
##     bet that kills it). Priced by the engine's own tables (weeks ×
##     RND_PER_WEEK) — the same price-book-ish band the roadmap already
##     charges. Committing one materializes a REAL bet through the roadmap's
##     own door.
##   · THE NEXT QUEUE — chosen-but-waiting bets. Storage is the bet record's
##     own committed_week, NEGATIVE: a queued bet carries committed_week =
##     −position (its save key unchanged, both engines; the field is only
##     ever read by the roadmap for READY bets, which a queued bet is not).
##     Each tick the queue head commits itself while WIP slots are free.
##
## LANDED BETS → FEATURES, the mapping (documented, tested):
##   kind quality   → job "charge"  (a better thing lets us charge)
##   kind retention → job "keep"    (keeps them)
##   kind reach     → job "pull"    (brings them in)
##   kind platform  → job "plumbing"
##   kind debt      → NO new feature — the rebuild HEALS the worst creak on
##                    landing (breaking → creaky → solid, one full step to
##                    solid for the target), the same week the jar pays down.
##   band backfired → nothing shipped worth keeping: no feature.
##   band risky     → shipped in a hurry: the feature is born CREAKY (the
##                    +debt the band already charged is the same story).
##   keep_wk = KEEP_ERA[era] × ambition (banded); unit_cost_add = keep_wk/20
##   (a documented 5% of upkeep leaks into every unit served — SimWorks reads
##   it additively for the ticket; no seam needed).
##
## THE LANDING SEAM: LANDING_SEAM_LIVE is false until the coordinator's
## package plants the on_bet_landed() call inside SimRoadmap.ship_bet and
## flips it — then a press births the feature in the same beat. Until then
## tick_pre polls this week's landings (dedup-guarded), one tick behind at
## worst.
##
## The spine calls, in tick order (docs/design/HOOKS.md):
##   tick_pre   tick §7f — landings join the inventory, the queue advances
##   tick_money the money section — INERT until the feature_keep package
##   tick_post  after the record — measured settles, solidity reconciles
## Outside the tick: directives() feeds the DM block, attention() feeds every
## bang through SimEngine.attention_items.
##
## SALTS (docs/design/00-spine.md §3), this lane's decade only:
##   SALT_FEAT_SHELF 140 — shelf candidate refresh (pure re-draw per week)
##   SALT_FEAT_CREAK 141 — the weekly solidity transition pick
##   SALT_FEAT_MEASURED 142 — the measured payoff's market spread
##
## TWIN LAW: this file and unity/Assets/Scripts/Core/Lanes/SimFeatures.cs
## carry the same logic in the same order.

# ═══════════════════════════════ CONSTANTS ═══════════════════════════════════

## The package flips this to true when the roadmap's ship_bet gains the
## on_bet_landed call; the polling fallback stands down the same commit.
const LANDING_SEAM_LIVE := false

## Weeks between a landing and its measured verdict.
const MEASURE_WEEKS := 4

## keep_wk = KEEP_ERA[era] × ambition — the banded upkeep a landing signs for.
const KEEP_ERA := {"garage": 3, "coworking": 4, "office": 5, "floor": 7, "hq": 9}

## One creak per this many debt points over the jar's free line (40).
const CREAK_STEP := 15.0

## The measured spread the market puts on a landing's payoff.
const MEASURE_SPREAD_LO := 0.75
const MEASURE_SPREAD_HI := 1.25
## When the 8-launch history forgot the source bet: the low-mid landing.
const MEASURE_FALLBACK_UNITS := 4

## The shelf's size band.
const SHELF_MIN := 3
const SHELF_MAX := 5

## bet kind → feature job, and back (the documented mapping).
const KIND_TO_JOB := {"quality": "charge", "retention": "keep",
	"reach": "pull", "platform": "plumbing"}
const JOB_TO_KIND := {"charge": "quality", "keep": "retention",
	"pull": "reach", "plumbing": "platform"}
## The job said in the wall's own words (shelf cards, receipts, directives).
const JOB_WORDS := {"pull": "brings them in", "keep": "keeps them",
	"charge": "lets us charge", "plumbing": "the plumbing"}

## THE KEYLESS SHELF POOLS, per business type — the same idea library the
## mockups draw from; vocabulary only, every number comes from the tables.
const SHELF_POOL := {
	"Software": [
		{"name": "white-label", "job": "charge"},
		{"name": "group scheduling", "job": "pull"},
		{"name": "SMS pack", "job": "keep"},
		{"name": "calendar sync", "job": "keep"},
		{"name": "analytics pack", "job": "keep"},
		{"name": "the referral loop", "job": "pull"},
		{"name": "the exports pack", "job": "charge"},
		{"name": "team spaces", "job": "pull"},
	],
	"Service": [
		{"name": "home visits", "job": "pull"},
		{"name": "corporate packages", "job": "charge"},
		{"name": "the gift card", "job": "pull"},
		{"name": "memberships", "job": "keep"},
		{"name": "the loyalty card", "job": "keep"},
		{"name": "the premium hour", "job": "charge"},
		{"name": "the referral card", "job": "pull"},
		{"name": "the seasonal line", "job": "keep"},
	],
	"Hardware": [
		{"name": "the pro bundle", "job": "charge"},
		{"name": "the accessory line", "job": "pull"},
		{"name": "the rugged build", "job": "keep"},
		{"name": "the companion app", "job": "keep"},
		{"name": "spare-parts program", "job": "pull"},
		{"name": "the limited edition", "job": "charge"},
		{"name": "quick-swap parts", "job": "keep"},
		{"name": "the starter kit", "job": "pull"},
	],
	"Marketplace": [
		{"name": "subscriptions", "job": "keep"},
		{"name": "the gift registry", "job": "pull"},
		{"name": "B2B invoicing", "job": "charge"},
		{"name": "bulk orders", "job": "charge"},
		{"name": "seller analytics", "job": "keep"},
		{"name": "buyer protection", "job": "keep"},
		{"name": "the weekly digest", "job": "pull"},
		{"name": "same-day courier", "job": "charge"},
	],
}

# ═══════════════════════════ THE SPINE'S ENTRY POINTS ═══════════════════════

## Tick §7f. The wall settles before anything reads it: a keyless/old save
## seeds its minimal inventory, this week's landings join (until the roadmap
## seam goes live), and the NEXT queue takes any freed slot.
static func tick_pre(state: GameState, rep: Dictionary) -> void:
	seed_defaults(state)
	if not LANDING_SEAM_LIVE:
		_poll_landings(state, rep)
	_run_queue(state, rep)

## The money section. INERT BY DESIGN until the coordinator's feature_keep
## package lands: the working money record's keys are fixed in sim_engine.gd
## and no feature lane exists yet, so billing here would either vanish (a key
## nothing sums) or break the twin. keep_total() below is the display truth.
static func tick_money(_state: GameState, _rep: Dictionary, _m: Dictionary) -> void:
	# ── FEATURE_KEEP SEAM (coordinator package): when the money record gains
	# the "feature_keep" lane, this hook bills it, in one line:
	#   _m["feature_keep"] = float(_m.get("feature_keep", 0.0)) + float(keep_total(_state))
	#   _rep["lines"].append("product upkeep: $%d/wk keeps %d features alive"
	#       % [keep_total(_state), _state.features.size()])
	pass

## After the record is written: landings settle their measured verdict, and
## the solidity ledger reconciles against the jar.
static func tick_post(state: GameState, rep: Dictionary) -> void:
	_measure(state, rep)
	_reconcile_solidity(state, rep)

## DM context lines (the spine caps the block).
static func directives(state: GameState) -> Array[String]:
	var out: Array[String] = []
	var creaks := creak_count(state)
	if creaks > 0:
		var tax := creak_tax_pct(state)
		var worst := worst_creak_name(state)
		if tax > 0:
			out.append("- The wall creaks at '%s' (%d creaky) — build speed −%d%%. A rebuild bet kills a creak."
				% [worst, creaks, tax])
		else:
			out.append("- '%s' shipped hot and creaks — a rebuild bet firms it up." % worst)
	for f in state.features:
		var fd: Dictionary = f
		if int(fd.get("born_wk", 0)) == state.week and int(fd.get("born_wk", 0)) > 0:
			out.append("- NEW ON THE WALL: '%s' joined what we make (%s)." % [
				String(fd.get("name", "")),
				String(JOB_WORDS.get(String(fd.get("job", "")), "plumbing"))])
			break
	return out

## Attention rows {desk, key, severity, label} — the desk names the new page
## directly (the binder's alias map passes unknown desks through verbatim).
static func attention(state: GameState) -> Array:
	var rows: Array = []
	var breaking := breaking_count(state)
	if breaking > 0:
		# ≤40 chars: 24 of template + a 16-char name
		rows.append({"desk": "what we make", "key": "feature_breaking", "severity": 3,
			"label": "'%s' is breaking — rebuild" % worst_creak_name(state).substr(0, 16)})
	var creaks := creak_count(state)
	if creaks > 0 and breaking == 0:
		var tax := creak_tax_pct(state)
		if tax > 0:
			rows.append({"desk": "what we make", "key": "creak_tax", "severity": 2,
				"label": "%d creak%s — build speed −%d%%" % [creaks,
					"" if creaks == 1 else "s", tax]})
		else:
			rows.append({"desk": "what we make", "key": "creak_tax", "severity": 2,
				"label": "%d creak%s on the wall — rebuild" % [creaks,
					"" if creaks == 1 else "s"]})
	var keep := keep_total(state)
	var pnl: Dictionary = state.get_meta("pnl", {})
	var revenue := int(pnl.get("revenue", 0))
	if keep >= 50 and revenue > 0 and keep * 4 >= revenue:
		rows.append({"desk": "what we make", "key": "keep_spike", "severity": 2,
			"label": "keep $%d/wk eats %d%% of revenue" % [keep,
				int(float(keep) * 100.0 / float(revenue))]})
	return rows

# ═════════════════════════ BIRTH & THE DEFAULT SET ═══════════════════════════

## A run with no generated inventory (an old save, a keyless world) still has
## a wall: a minimal set derived from what it already sells. born_wk 0 marks
## a birth feature — never measured (there was no promise). L-GEN writes the
## real generated set at run start under the same law.
static func seed_defaults(state: GameState) -> void:
	if not state.features.is_empty():
		return
	if state.offers.is_empty() and state.traction <= 0 and state.product <= 0:
		return   # a truly blank state (the draft) keeps an empty wall
	var base := int(KEEP_ERA.get(state.era, 3))
	var flagship := "what we sell"
	if not state.offers.is_empty():
		flagship = String((state.offers[0] as Dictionary).get("name", flagship))
	state.features = [
		{"id": "ft_seed_pull", "name": "the front door", "job": "pull",
			"family": "", "solidity": "solid", "keep_wk": base,
			"unit_cost_add": float(base) / 20.0, "product_id": "",
			"born_wk": 0, "measured": 0.0},
		{"id": "ft_seed_core", "name": flagship.substr(0, 28), "job": "keep",
			"family": "", "solidity": "solid", "keep_wk": base * 2,
			"unit_cost_add": float(base * 2) / 20.0, "product_id": "",
			"born_wk": 0, "measured": 0.0},
		{"id": "ft_seed_plumb", "name": "the plumbing", "job": "plumbing",
			"family": "", "solidity": "solid", "keep_wk": base * 2,
			"unit_cost_add": 0.0, "product_id": "",
			"born_wk": 0, "measured": 0.0},
	]

## THE LANDING, one door for both routes (the tick's poll today, the roadmap
## seam after the package): a shipped bet becomes inventory, a shipped
## rebuild heals. Idempotent for births (name + born_wk guard); heals are
## guarded by the caller's window. Returns the receipt lines it wrote.
static func on_bet_landed(state: GameState, bet: Dictionary, rep: Dictionary) -> void:
	var band := String(bet.get("band", ""))
	if band == "backfired":
		return
	var kind := String(bet.get("kind", ""))
	if kind == "debt":
		_heal_worst(state, rep, String(bet.get("name", "")))
		return
	if not KIND_TO_JOB.has(kind):
		return
	var name := String(bet.get("name", ""))
	var born := int(bet.get("shipped_week", state.week))
	for f in state.features:
		var fd: Dictionary = f
		if String(fd.get("name", "")) == name and int(fd.get("born_wk", -1)) == born:
			return   # already on the wall
	var amb := clampi(int(bet.get("ambition", 1)), 1, 3)
	var keep := int(KEEP_ERA.get(state.era, 3)) * amb
	var n := 0
	for f2 in state.features:
		if int((f2 as Dictionary).get("born_wk", -1)) == born:
			n += 1
	var creaky := band == "risky"
	state.features.append({
		"id": "ft_w%d_%d" % [born, n + 1],
		"name": name.substr(0, 28),
		"job": String(KIND_TO_JOB.get(kind, "plumbing")),
		"family": "",
		"solidity": "creaky" if creaky else "solid",
		"keep_wk": keep,
		"unit_cost_add": float(keep) / 20.0,
		"product_id": "",
		"born_wk": born,
		"measured": 0.0,
	})
	if rep.has("lines"):
		rep["lines"].append("the wall grows: '%s' joins what we make (%s) — keep $%d/wk" % [
			name, String(JOB_WORDS.get(String(KIND_TO_JOB.get(kind, "plumbing")), "")), keep])
		if creaky:
			rep["lines"].append("  → shipped in a hurry: it starts life CREAKY")

## The polling fallback: this week's landings, one tick behind a desk press
## at worst. Births carry their own dedup; heals only fire on bets shipped
## THIS week (a landing is healed exactly once).
static func _poll_landings(state: GameState, rep: Dictionary) -> void:
	for b in state.bets:
		var bd: Dictionary = b
		if not bool(bd.get("shipped", false)):
			continue
		var wk := int(bd.get("shipped_week", 0))
		if String(bd.get("kind", "")) == "debt":
			if wk == state.week:
				on_bet_landed(state, bd, rep)
		elif wk >= state.week - 1:
			on_bet_landed(state, bd, rep)

## A rebuild kills the creak on landing: the worst feature (breaking first,
## then creaky; the plumbing first inside a class) goes SOLID the same week
## the jar pays down.
static func _heal_worst(state: GameState, rep: Dictionary, why: String) -> void:
	var target := _worst_creak(state)
	if target.is_empty():
		return
	target["solidity"] = "solid"
	if rep.has("lines"):
		rep["lines"].append("the rebuild lands ('%s'): '%s' is SOLID again" % [
			why, String(target.get("name", ""))])

# ═══════════════════════════ THE MEASURED VERDICT ════════════════════════════

## After MEASURE_WEEKS the world answers: measured = the landing's ACTUAL
## payoff units (the engine delta the dice granted, recovered from the bet
## record) × a salted 0.75..1.25 market spread, snapped to 0.1 with the same
## explicit formula in both engines. 0 stays the "not yet" sentinel; birth
## features (born_wk 0) are never measured — nothing was promised.
static func _measure(state: GameState, rep: Dictionary) -> void:
	var r: RandomNumberGenerator = null
	for f in state.features:
		var fd: Dictionary = f
		var born := int(fd.get("born_wk", 0))
		if born < 2 or float(fd.get("measured", 0.0)) != 0.0:
			continue
		if state.week - born < MEASURE_WEEKS:
			continue
		if r == null:
			r = SimEngine.rng_for(state, SimEngine.SALT_FEAT_MEASURED)
		var base := MEASURE_FALLBACK_UNITS
		var promised := 0
		var src := _source_bet(state, String(fd.get("name", "")), born)
		if not src.is_empty():
			var amb := clampi(int(src.get("ambition", 1)), 1, 3)
			var bi := maxi(SimRoadmap.BANDS.find(String(src.get("band", "fine"))), 0)
			base = int(SimRoadmap.BET_PAYOFF[amb - 1][bi])
			promised = int(SimRoadmap.BET_PAYOFF[amb - 1][1])
		var spread := r.randf_range(MEASURE_SPREAD_LO, MEASURE_SPREAD_HI)
		var measured: float = floor(maxf(float(base) * spread, 0.1) / 0.1 + 0.5) * 0.1
		fd["measured"] = measured
		if rep.has("lines"):
			if promised > 0:
				rep["lines"].append("measured: '%s' promised +%d, the market says +%.1f" % [
					String(fd.get("name", "")), promised, measured])
			else:
				rep["lines"].append("measured: '%s' settles at +%.1f" % [
					String(fd.get("name", "")), measured])

## The source bet while the 8-launch history still holds it.
static func _source_bet(state: GameState, name: String, born: int) -> Dictionary:
	for b in state.bets:
		var bd: Dictionary = b
		if bool(bd.get("shipped", false)) and int(bd.get("shipped_week", -1)) == born \
				and String(bd.get("name", "")) == name:
			return bd
	return {}

## The promise a landed feature's card advertised (the "fine" payoff), while
## history remembers. 0 = history forgot; the desk prints measured alone.
static func promised_units(state: GameState, feature: Dictionary) -> int:
	var src := _source_bet(state, String(feature.get("name", "")),
		int(feature.get("born_wk", -1)))
	if src.is_empty():
		return 0
	var amb := clampi(int(src.get("ambition", 1)), 1, 3)
	return int(SimRoadmap.BET_PAYOFF[amb - 1][1])

# ═══════════════════════ SOLIDITY — THE JAR'S FACE ═══════════════════════════

## The jar level the wall should show: 0 creaks at or under the free line,
## one more per CREAK_STEP points above it.
static func expected_creak_load(debt: float) -> int:
	if debt <= SimRoadmap.DEBT_FREE:
		return 0
	return int(ceil((debt - SimRoadmap.DEBT_FREE) / CREAK_STEP))

## creaky counts 1, breaking counts 2 — the load the reconcile steers.
static func creak_load(state: GameState) -> int:
	var load := 0
	for f in state.features:
		match String((f as Dictionary).get("solidity", "solid")):
			"creaky": load += 1
			"breaking": load += 2
	return load

## ONE transition a week toward the jar's truth. Worsening picks a solid
## feature on SALT_FEAT_CREAK (plumbing first — the debt concentrates in the
## plumbing); with no solid feature left, the oldest creak breaks. Healing
## un-breaks first, then firms the oldest creak. The tax itself lives in
## SimRoadmap.debt_drag and NOWHERE here — one jar, one tax, many faces.
static func _reconcile_solidity(state: GameState, rep: Dictionary) -> void:
	if state.features.is_empty():
		return
	var target := expected_creak_load(state.tech_debt)
	var load := creak_load(state)
	if load < target:
		var pool: Array = []
		for f in state.features:
			var fd: Dictionary = f
			if String(fd.get("solidity", "solid")) == "solid" \
					and String(fd.get("job", "")) == "plumbing":
				pool.append(fd)
		if pool.is_empty():
			for f2 in state.features:
				if String((f2 as Dictionary).get("solidity", "solid")) == "solid":
					pool.append(f2)
		if not pool.is_empty():
			var r := SimEngine.rng_for(state, SimEngine.SALT_FEAT_CREAK)
			var pick: Dictionary = pool[r.randi_range(0, pool.size() - 1)]
			pick["solidity"] = "creaky"
			if rep.has("lines"):
				rep["lines"].append("the debt shows its face: '%s' starts creaking (debt %d)" % [
					String(pick.get("name", "")), int(state.tech_debt)])
		else:
			for f3 in state.features:
				var fd3: Dictionary = f3
				if String(fd3.get("solidity", "")) == "creaky":
					fd3["solidity"] = "breaking"
					if rep.has("lines"):
						rep["lines"].append("'%s' is BREAKING — the debt is collecting (debt %d)" % [
							String(fd3.get("name", "")), int(state.tech_debt)])
					break
	elif load > target:
		var healed := false
		for f4 in state.features:
			var fd4: Dictionary = f4
			if String(fd4.get("solidity", "")) == "breaking":
				fd4["solidity"] = "creaky"
				if rep.has("lines"):
					rep["lines"].append("'%s' steps back from the edge — still creaky" % [
						String(fd4.get("name", ""))])
				healed = true
				break
		if not healed:
			for f5 in state.features:
				var fd5: Dictionary = f5
				if String(fd5.get("solidity", "")) == "creaky":
					fd5["solidity"] = "solid"
					if rep.has("lines"):
						rep["lines"].append("the codebase breathes: '%s' firms up" % [
							String(fd5.get("name", ""))])
					break

## breaking first, then creaky; the plumbing first inside a class — the
## rebuild's target and the attention row's name.
static func _worst_creak(state: GameState) -> Dictionary:
	for solidity in ["breaking", "creaky"]:
		for plumb in [true, false]:
			for f in state.features:
				var fd: Dictionary = f
				if String(fd.get("solidity", "")) != solidity:
					continue
				if (String(fd.get("job", "")) == "plumbing") != plumb:
					continue
				return fd
	return {}

# ═══════════════════════════ PURE READS (the desk) ═══════════════════════════

static func keep_total(state: GameState) -> int:
	var total := 0
	for f in state.features:
		total += int((f as Dictionary).get("keep_wk", 0))
	return total

## The build spend the wall's footer prints: while a bet is committed the
## whole rnd lever buys weeks (SimRoadmap routes it), so that IS the number.
static func build_total(state: GameState) -> int:
	if SimRoadmap.committed_bets(state).is_empty():
		return 0
	return int(state.budgets.get("rnd", 0))

## Per-unit impact on the works' ticket: the flagship's own features plus the
## shared plumbing every product stands on. SimWorks reads this additively.
static func unit_cost_total(state: GameState, product_id: String = "") -> float:
	var total := 0.0
	for f in state.features:
		var fd: Dictionary = f
		var pid := String(fd.get("product_id", ""))
		if pid == "" or pid == product_id:
			total += float(fd.get("unit_cost_add", 0.0))
	return total

static func creak_count(state: GameState) -> int:
	var n := 0
	for f in state.features:
		if String((f as Dictionary).get("solidity", "solid")) != "solid":
			n += 1
	return n

static func breaking_count(state: GameState) -> int:
	var n := 0
	for f in state.features:
		if String((f as Dictionary).get("solidity", "")) == "breaking":
			n += 1
	return n

static func worst_creak_name(state: GameState) -> String:
	var w := _worst_creak(state)
	return String(w.get("name", "")) if not w.is_empty() else ""

## THE ONE TAX, displayed: the jar's own velocity interest, said in percent.
## The creaks are its face; nothing here applies a second one.
static func creak_tax_pct(state: GameState) -> int:
	return int(round((1.0 - SimRoadmap.debt_drag(state)) * 100.0))

## Distinct non-flagship product ids on the wall (rung 3 fires at ≥1: the
## flagship plus a named product = many things).
static func product_ids(state: GameState) -> Array:
	var out: Array = []
	for f in state.features:
		var pid := String((f as Dictionary).get("product_id", ""))
		if pid != "" and not out.has(pid):
			out.append(pid)
	for o in state.offers:
		var pid2 := String((o as Dictionary).get("product_id", ""))
		if pid2 != "" and not out.has(pid2):
			out.append(pid2)
	return out

# ═══════════════════════════════ THE SHELF ═══════════════════════════════════

## 3..5 priced candidates, re-drawn deterministically per (seed, week) on
## SALT_FEAT_SHELF: the wall's gap jobs draw their candidates first, a creak
## draws the rebuild that kills it, the era draws the rest from the business
## type's own pool. Cost/odds come from the roadmap's authored tables — the
## world prices it, the same way it prices everything.
static func shelf_candidates(state: GameState) -> Array:
	var out: Array = []
	var r := SimEngine.rng_for(state, SimEngine.SALT_FEAT_SHELF)
	var pool: Array = SHELF_POOL.get(state.biz_what, SHELF_POOL["Software"])
	var taken: Array = []
	for f in state.features:
		taken.append(String((f as Dictionary).get("name", "")))
	for b in state.bets:
		taken.append(String((b as Dictionary).get("name", "")))
	var n := SHELF_MIN + (1 if state.era_index() >= 2 else 0)
	# 1 ── the rebuild, when the wall creaks (it kills the creak on landing)
	if creak_count(state) > 0:
		var worst := worst_creak_name(state)
		out.append(_candidate(state, r, "rebuild: %s" % worst.substr(0, 18), "debt", 1))
		n = mini(n + 1, SHELF_MAX)
	# 2 ── the gaps draw first: a job nobody on the wall does
	var jobs_live: Array = []
	for f2 in state.features:
		var j := String((f2 as Dictionary).get("job", ""))
		if not jobs_live.has(j):
			jobs_live.append(j)
	for gap in ["pull", "keep", "charge"]:
		if out.size() >= n:
			break
		if jobs_live.has(gap):
			continue
		var pick := _draw_from_pool(r, pool, taken, gap)
		if not pick.is_empty():
			taken.append(String(pick.get("name", "")))
			out.append(_candidate(state, r, String(pick.get("name", "")),
				String(JOB_TO_KIND.get(gap, "quality")), 0))
	# 3 ── the rest of the shelf from the pool, any job
	while out.size() < n:
		var pick2 := _draw_from_pool(r, pool, taken, "")
		if pick2.is_empty():
			break
		taken.append(String(pick2.get("name", "")))
		out.append(_candidate(state, r, String(pick2.get("name", "")),
			String(JOB_TO_KIND.get(String(pick2.get("job", "keep")), "retention")), 0))
	return out

## One shelf candidate, priced by the engine's own tables. ambition 0 = draw
## one inside the era's cap.
static func _candidate(state: GameState, r: RandomNumberGenerator, name: String,
		kind: String, amb_fixed: int) -> Dictionary:
	var amb := amb_fixed
	if amb <= 0:
		amb = r.randi_range(1, SimRoadmap.ambition_cap(state))
	var cost := SimRoadmap.bet_cost(kind, amb)
	var dc := SimRoadmap.PLATFORM_DC if kind == "platform" \
		else int(SimRoadmap.DC_BY_AMBITION[clampi(amb, 1, 3) - 1])
	var mod := int(state.competences.get("build", 3)) - 3
	var need := clampi(dc - mod, 2, 20)
	var job := "plumbing" if kind == "debt" else String(KIND_TO_JOB.get(kind, "plumbing"))
	return {
		"id": "shelf_w%d_%d" % [state.week, r.randi_range(1000, 9999)],
		"name": name.substr(0, 28),
		"kind": kind, "ambition": amb,
		"job": job,
		"job_words": "kills a creak" if kind == "debt" \
			else String(JOB_WORDS.get(job, "")),
		"cost_rnd_weeks": cost,
		"cost_usd": int(cost * SimRoadmap.RND_PER_WEEK),
		"weeks": int(ceil(cost)),
		"odds_pct": int(round(float(21 - need) / 20.0 * 100.0)),
	}

static func _draw_from_pool(r: RandomNumberGenerator, pool: Array, taken: Array,
		job: String) -> Dictionary:
	var eligible: Array = []
	for c in pool:
		var cd: Dictionary = c
		if taken.has(String(cd.get("name", ""))):
			continue
		if job != "" and String(cd.get("job", "")) != job:
			continue
		eligible.append(cd)
	if eligible.is_empty():
		return {}
	return eligible[r.randi_range(0, eligible.size() - 1)]

## COMMIT A SHELF IDEA: materialize a real bet through the roadmap's own
## door — the team takes it on if a WIP slot is free, else it joins NEXT.
## Returns "committed", "queued" or "" (unknown id / duplicate name).
static func commit_shelf(state: GameState, shelf_id: String) -> String:
	var cand := {}
	for c in shelf_candidates(state):
		if String((c as Dictionary).get("id", "")) == shelf_id:
			cand = c
			break
	if cand.is_empty():
		return ""
	var name := String(cand.get("name", ""))
	for b in state.bets:
		var bd: Dictionary = b
		if String(bd.get("name", "")) == name and not bool(bd.get("shipped", false)):
			return ""   # already on the board
	var n := 1
	for b2 in state.bets:
		if String((b2 as Dictionary).get("id", "")).begins_with("featbet_w%d_" % state.week):
			n += 1
	var kind := String(cand.get("kind", "quality"))
	var bet := {
		"id": "featbet_w%d_%d" % [state.week, n],
		"name": name,
		"desc": "from the shelf — %s" % String(cand.get("job_words", "")),
		"kind": kind,
		"ambition": int(cand.get("ambition", 1)),
		"cost_rnd_weeks": float(cand.get("cost_rnd_weeks", 3.0)),
		"progress": 0.0, "committed": false, "committed_week": 0,
		"ready": false, "shipped": false, "shipped_week": 0,
		"band": "", "era": state.era,
	}
	state.bets.append(bet)
	if SimRoadmap.committed_bets(state).size() < SimRoadmap.wip_cap(state):
		SimRoadmap.commit_bet(state, String(bet.get("id", "")))
		return "committed"
	enqueue_bet(state, String(bet.get("id", "")))
	return "queued"

# ═══════════════════════════ THE NEXT QUEUE ══════════════════════════════════
## Storage: a queued bet's committed_week is NEGATIVE, −position. The field's
## save key is unchanged in both engines; the roadmap only ever reads it on
## READY bets (the stall clock), which a queued bet is not. An era change
## drops uncommitted candidates and their queue seats with them — candidates
## are paper, never paid work.

## The queue, in order.
static func queued_bets(state: GameState) -> Array:
	var out: Array = []
	for b in state.bets:
		var bd: Dictionary = b
		if int(bd.get("committed_week", 0)) < 0 and not bool(bd.get("committed", false)) \
				and not bool(bd.get("ready", false)) and not bool(bd.get("shipped", false)):
			out.append(bd)
	out.sort_custom(func(a: Dictionary, b2: Dictionary) -> bool:
		return -int(a.get("committed_week", 0)) < -int(b2.get("committed_week", 0)))
	return out

## Choose a board candidate for NEXT. Refuses work in flight.
static func enqueue_bet(state: GameState, id: String) -> bool:
	var bet := SimRoadmap.bet_by_id(state, id)
	if bet.is_empty() or bool(bet.get("committed", false)) or bool(bet.get("ready", false)) \
			or bool(bet.get("shipped", false)) or int(bet.get("committed_week", 0)) < 0:
		return false
	var deepest := 0
	for q in queued_bets(state):
		deepest = maxi(deepest, -int((q as Dictionary).get("committed_week", 0)))
	bet["committed_week"] = -(deepest + 1)
	state.log_action("what we make: queued '%s' for NEXT" % String(bet.get("name", "")))
	return true

## Back to the shelf.
static func dequeue_bet(state: GameState, id: String) -> bool:
	var bet := SimRoadmap.bet_by_id(state, id)
	if bet.is_empty() or int(bet.get("committed_week", 0)) >= 0:
		return false
	bet["committed_week"] = 0
	return true

## Reorder: dir −1 = sooner, +1 = later. Swaps seats with the neighbour.
static func queue_move(state: GameState, id: String, dir: int) -> bool:
	var q := queued_bets(state)
	for i in q.size():
		if String((q[i] as Dictionary).get("id", "")) == id:
			var j := i + (1 if dir > 0 else -1)
			if j < 0 or j >= q.size():
				return false
			var a: Dictionary = q[i]
			var b: Dictionary = q[j]
			var tmp := int(a.get("committed_week", 0))
			a["committed_week"] = int(b.get("committed_week", 0))
			b["committed_week"] = tmp
			return true
	return false

## The queue takes any freed slot, in order, through the roadmap's own
## commit (its WIP arithmetic is the law; the sentinel resets first so the
## stall clock starts honest).
static func _run_queue(state: GameState, rep: Dictionary) -> void:
	var q := queued_bets(state)
	for bet in q:
		if SimRoadmap.committed_bets(state).size() >= SimRoadmap.wip_cap(state):
			break
		var bd: Dictionary = bet
		var seat := int(bd.get("committed_week", 0))
		bd["committed_week"] = 0
		if SimRoadmap.commit_bet(state, String(bd.get("id", ""))):
			if rep.has("lines"):
				rep["lines"].append("the queue moves: the team takes up '%s'" % [
					String(bd.get("name", ""))])
		else:
			bd["committed_week"] = seat
			break
