class_name WorldGen
extends RefCounted
## THE WORLD BIBLE — generated once per run (docs/DND_STARTUP_PLAN.md A4/A8/B5).
##
## Deterministic core: names from a seeded Markov chain (opendnd/Nomina's
## count^1.3 weighting), investors assembled Personae-style (archetype +
## alignment coords + trait + bond + flaw + secret), rivals with tactics.
## The LLM ENRICHES (Theta from the pitch, thesis lines in the world's own
## words) — but a keyless run gets a complete, playable world from here alone.

# ── Markov names (Nomina) ────────────────────────────────────────────────────
const NAME_SEEDS := ["vanta", "loomly", "brightside", "koda", "meridian", "fluxo",
	"harbor", "nimbus", "verdant", "quill", "atlasgo", "pebble", "crestline",
	"sundial", "fernwood", "arclight", "tidepool", "monarch", "juniper", "cobalt",
	"drift", "ember", "willow", "stonefruit", "larkspur", "novabeam", "haven",
	"maple", "cinder", "bluefin", "orchard", "signal", "lumen", "basalt"]
const FUND_SUFFIX := ["Capital", "Ventures", "Partners", "Collective", "Fund", "Syndicate"]

## People are not companies: hires, cofounders and walk-ons draw from these.
const FIRST_NAMES := ["Mara", "Nico", "Priya", "Jonas", "Aiko", "Sam", "Lena",
	"Ravi", "Ines", "Theo", "Dana", "Milo", "Zara", "Owen", "Nadia", "Felix",
	"June", "Marco", "Elif", "Casper", "Rosa", "Ade", "Petra", "Yuki", "Bram"]
const LAST_NAMES := ["Sorel", "Okafor", "Lindgren", "Vance", "Marchetti", "Bakker",
	"Ito", "Novak", "Ferreira", "Duval", "Haddad", "Kowalski", "Mbeki", "Ander",
	"Voss", "Reyes", "Tanaka", "Bergstrom", "Cissé", "Moreau", "Silva", "Grant"]

static func person_name(rng: RandomNumberGenerator) -> String:
	return "%s %s" % [FIRST_NAMES[rng.randi() % FIRST_NAMES.size()],
		LAST_NAMES[rng.randi() % LAST_NAMES.size()]]

static func _chain(rng: RandomNumberGenerator) -> Dictionary:
	var initial: Dictionary = {}
	var trans: Dictionary = {}
	var lens: Array = []
	for nm in NAME_SEEDS:
		lens.append(nm.length())
		var first: String = nm.substr(0, 1)
		initial[first] = int(initial.get(first, 0)) + 1
		for i in range(nm.length() - 1):
			var a: String = nm.substr(i, 1)
			var b: String = nm.substr(i + 1, 1)
			if not trans.has(a):
				trans[a] = {}
			trans[a][b] = int(trans[a].get(b, 0)) + 1
	return {"initial": initial, "trans": trans, "lens": lens, "rng": rng}

static func _pick_weighted(counts: Dictionary, rng: RandomNumberGenerator) -> String:
	var total := 0.0
	for k in counts:
		total += pow(float(counts[k]), 1.3)
	var x := rng.randf() * total
	for k in counts:
		x -= pow(float(counts[k]), 1.3)
		if x <= 0.0:
			return String(k)
	return counts.keys()[0] if counts.size() > 0 else "a"

static func make_name(rng: RandomNumberGenerator) -> String:
	var ch := _chain(rng)
	for _try in 12:
		var ln: int = ch.lens[rng.randi() % (ch.lens as Array).size()]
		var out := _pick_weighted(ch.initial, rng)
		while out.length() < ln:
			var last := out.substr(out.length() - 1, 1)
			if not (ch.trans as Dictionary).has(last):
				break
			out += _pick_weighted(ch.trans[last], rng)
		if _pronounceable(out):
			return out.capitalize()
	return "Fernbay"   # the safety name after twelve unlucky draws

## No three consonants in a row, at least one vowel per 3 letters — names a
## founder could actually say on a podcast.
static func _pronounceable(s: String) -> bool:
	var vowels := "aeiouy"
	var run := 0
	var v_count := 0
	for i in s.length():
		var c := s.substr(i, 1)
		if vowels.contains(c):
			run = 0
			v_count += 1
		else:
			run += 1
			if run >= 3:
				return false
	return v_count * 3 >= s.length()

# ── investor archetypes (Personae-style assembly) ────────────────────────────
## alignment coords: x = founder-friendly(−1)…predatory(+1), y = contrarian(−1)…momentum(+1)
const INVESTOR_ARCHETYPES := [
	{"archetype": "the momentum fund", "coords": [0.3, 0.9],
	 "thesis": "growth is the only truth; everything else is commentary",
	 "tactics": ["pushes for blitz spending", "goes cold the week growth dips"]},
	{"archetype": "the contrarian angel", "coords": [-0.6, -0.8],
	 "thesis": "the best deals look wrong to everyone else",
	 "tactics": ["funds what others passed on", "hates consensus rounds"]},
	{"archetype": "the operator VC", "coords": [-0.3, 0.2],
	 "thesis": "founders who ship beat founders who pitch",
	 "tactics": ["asks for the metrics dashboard", "intros real customers"]},
	{"archetype": "the shark", "coords": [0.9, 0.4],
	 "thesis": "desperation is a pricing signal",
	 "tactics": ["waits until you are broke", "term sheets with teeth"]},
	{"archetype": "the thesis tourist", "coords": [0.1, 0.6],
	 "thesis": "whatever the current wave is, they surfed in last month",
	 "tactics": ["loves the space this quarter", "vanishes next quarter"]},
]
const INVESTOR_TRAITS := ["never blinks in meetings", "answers email at 3am only",
	"quotes their own blog", "keeps a kill list of passed deals",
	"brings a dog to diligence", "speaks entirely in sports metaphors",
	"has one great exit and infinite slides about it"]
const INVESTOR_BONDS := ["led the seed of a company you admire",
	"lost money on a company exactly like yours", "owes your ex-boss a favor",
	"is raising their own fund and needs winners"]
const INVESTOR_FLAWS := ["mistakes confidence for competence",
	"cannot say no in the room, says it by email", "reads only the top line",
	"funds people who remind them of themselves"]
const INVESTOR_SECRETS := ["their fund is nearly out of dry powder",
	"they already backed a competitor quietly", "their LPs are pushing for exits",
	"they decided in the first five minutes"]

const RIVAL_TACTICS := [["undercut pricing", "poached a customer", "shipped a clone feature"],
	["raised a loud round", "hired away talent", "bought ads on your name"],
	["landed a press feature", "announced a partnership", "opened your segment"]]

## What a business of this shape plausibly sells, priced by the market it
## serves — the deterministic skeleton the LLM refines. fair_price is the
## street's reference; elasticity is how hard demand punishes deviation.
static func default_offers(what: String, who: String, rng: RandomNumberGenerator) -> Array:
	# THE AUDIENCE SCALES THE INVOICE (C5 audit D1): only Software priced by
	# `who`, so a Consumer was billed at SMB rates across four thousand
	# customers — a measured +$100k/wk money printer. Consumer pays a quarter,
	# Enterprise four times, costs scale WITH price (margin holds).
	var aud := 0.25 if who == "Consumer" else (4.0 if who == "Enterprise" else 1.0)
	match what:
		"Service":
			return [
				{"name": "standard session", "unit": "per session",
				 "fair_price": float(rng.randi_range(45, 85)) * aud, "elasticity": 2.6,
				 "unit_cost": 18.0 * aud, "price": 0.0, "weight": 0.7},
				# the premium lane is INELASTIC (C5 audit D2): pricing above
				# fair must be a real strategy somewhere, not a cliff
				{"name": "premium package", "unit": "per package",
				 "fair_price": float(rng.randi_range(140, 260)) * aud, "elasticity": 0.8,
				 "unit_cost": 55.0 * aud, "price": 0.0, "weight": 0.3}]
		"Hardware":
			return [
				{"name": "the device", "unit": "per unit",
				 "fair_price": float(rng.randi_range(120, 420)) * aud, "elasticity": 0.9,
				 "unit_cost": float(rng.randi_range(40, 150)) * aud, "price": 0.0, "weight": 0.8},
				{"name": "accessories", "unit": "per kit",
				 "fair_price": float(rng.randi_range(25, 60)) * aud, "elasticity": 2.4,
				 "unit_cost": 9.0 * aud, "price": 0.0, "weight": 0.2}]
		"Marketplace":
			return [
				# dollars per order, and SAYS so (C5 audit D7: a percent was
				# being booked as dollars and 25% read as 3x-fair greed)
				{"name": "platform take, per order", "unit": "per order",
				 "fair_price": float(rng.randi_range(8, 18)) * aud, "elasticity": 3.0,
				 "unit_cost": 1.0 * aud, "price": 0.0, "weight": 1.0}]
		_:
			var base := 12 if who == "Consumer" else (rng.randi_range(29, 79) if who == "SMB" else rng.randi_range(190, 590))
			return [
				{"name": "monthly plan", "unit": "per month",
				 "fair_price": float(base), "elasticity": 2.2 if who != "Enterprise" else 1.5,
				 "unit_cost": 3.0, "price": 0.0, "weight": 0.8},
				{"name": "annual plan", "unit": "per year",
				 "fair_price": float(base) * 10.0, "elasticity": 0.8,
				 "unit_cost": 30.0, "price": 0.0, "weight": 0.2}]

## The complete deterministic bible. `seed` is the run seed.
static func build(state: GameState) -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = hash(str(state.sim_seed) + ":world")
	# investors: three, distinct archetypes
	var picks: Array = INVESTOR_ARCHETYPES.duplicate()
	var invs: Array = []
	for i in 3:
		var a: Dictionary = picks.pop_at(rng.randi() % picks.size())
		invs.append({
			"name": "%s %s" % [make_name(rng), FUND_SUFFIX[rng.randi() % FUND_SUFFIX.size()]],
			"archetype": String(a.archetype),
			"coords": a.coords,
			"thesis": String(a.thesis),
			"trait": INVESTOR_TRAITS[rng.randi() % INVESTOR_TRAITS.size()],
			"bond": INVESTOR_BONDS[rng.randi() % INVESTOR_BONDS.size()],
			"flaw": INVESTOR_FLAWS[rng.randi() % INVESTOR_FLAWS.size()],
			"secret": INVESTOR_SECRETS[rng.randi() % INVESTOR_SECRETS.size()],
			"tactics": a.tactics,
		})
	state.investors = invs
	# rivals: two, born from the same market
	var rivals: Array = []
	for i in 2:
		rivals.append({
			"name": make_name(rng),
			"strength": float(state.theta.get("rival_strength", 20.0)) * rng.randf_range(0.8, 1.3),
			"tactics": RIVAL_TACTICS[rng.randi() % RIVAL_TACTICS.size()],
			"weeks_since_move": 0,
			"secret": "quietly running out of money" if rng.randf() < 0.3 else "",
		})
	state.rivals = rivals
	if state.offers.is_empty():
		state.offers = default_offers(state.biz_what, state.biz_who, rng)
		# DECISIONS.md (catalog): the flagship carries ONE starter fixed line so
		# the catalog-overhead lane is alive from week 1 — the tools were always
		# real; now the ledger says so.
		if not state.offers.is_empty():
			var aud0 := 0.25 if state.biz_who == "Consumer" else (4.0 if state.biz_who == "Enterprise" else 1.0)
			var flag0: Dictionary = state.offers[0]
			flag0["fixed_lines"] = [{"label": "the tools that make it", "amount": 15.0 * aud0}]
			SimEngine.sync_offer_costs(flag0)
	seed_rival_conduct(state, rng)
	# THE BIRTH BOOK'S KEYLESS HALF (DAG2 L-GEN): pure static tables, no rng —
	# added dead last so every existing draw keeps its exact sequence position.
	default_birth(state)

## THE RIVALS' CONDUCT (docs/design/03-rivals-macro.md §1): a war chest, a
## strategic bent, a price posture and a share of voice — what turns a strength
## number into a company that DOES things.
##
## Drawn at the very END of build, after the offers, and never in the middle:
## inserting draws earlier would shift every later investor and offer draw and
## silently break worldgen determinism for every existing seed.
static func seed_rival_conduct(state: GameState, rng: RandomNumberGenerator) -> void:
	for rv in state.rivals:
		var rd: Dictionary = rv
		rd["vigor"] = rng.randf_range(40.0, 70.0)
		rd["hype"] = rng.randf_range(10.0, 40.0)
		rd["focus"] = ["price", "product", "growth"][rng.randi() % 3]
		rd["price_posture"] = 1.0
		rd["last_action"] = ""
		rd["log"] = []
		rd["cooldowns"] = {}
		rd["sniffing"] = 0

## Merge an LLM-generated world onto the deterministic skeleton: names, theses
## and rivals come from the model (born from the pitch); coords and tactics
## decks come from the archetype so the engine math never depends on prose.
static func apply_llm_world(state: GameState, gen: Dictionary) -> bool:
	if gen.is_empty():
		return false
	var market: Dictionary = gen.get("market", {})
	if not market.is_empty():
		var th := state.theta.duplicate()
		th["tam"] = float(market.get("tam_buyers", th.get("tam", 100000.0)))
		th["lifetime_wk"] = float(market.get("customer_patience_weeks", th.get("lifetime_wk", 40.0)))
		state.theta = SimEngine.clamp_theta(th)
		state.set_meta("market_line", String(market.get("one_liner", "")))
	var by_arch := {}
	for a in INVESTOR_ARCHETYPES:
		by_arch[String(a.archetype)] = a
	var invs: Array = []
	for iv in gen.get("investors", []):
		var d: Dictionary = iv
		var arch: Dictionary = by_arch.get(String(d.get("archetype", "")), INVESTOR_ARCHETYPES[2])
		invs.append({
			"name": String(d.get("name", "an investor")).left(40),
			"archetype": String(d.get("archetype", arch.archetype)),
			"coords": arch.coords,
			"thesis": String(d.get("thesis", "")),
			"trait": String(d.get("trait", "")),
			"bond": String(d.get("bond", "")),
			"flaw": String(d.get("flaw", "")),
			"secret": String(d.get("secret", "")),
			"tactics": arch.tactics,
		})
	if invs.size() == 3:
		state.investors = invs
	var str_map := {"struggling": 12.0, "scrappy": 25.0, "strong": 45.0, "dominant": 70.0}
	var rivals: Array = []
	for rv in gen.get("rivals", []):
		var r: Dictionary = rv
		var what_txt := String(r.get("what_they_do", ""))
		if what_txt.length() >= 135 and not what_txt.ends_with("."):
			var wcut := what_txt.rfind(" ")
			if wcut > 40:
				what_txt = what_txt.substr(0, wcut) + "…"
		var rname := String(r.get("name", "a rival")).left(30)
		rivals.append({
			"name": rname,
			"what": what_txt,
			"strength": float(str_map.get(String(r.get("strength", "scrappy")), 25.0)),
			"tactics": r.get("tactics", ["shipped something loud"]),
			"weeks_since_move": 0,
			"secret": "",
			# no rng in scope on the LLM path, so conduct takes its defaults and
			# the bent comes from the name itself — twin-safe, no hash involved
			"vigor": 55.0, "hype": 20.0,
			"focus": ["price", "product", "growth"][rname.length() % 3],
			"price_posture": 1.0, "last_action": "", "log": [],
			"cooldowns": {}, "sniffing": 0,
		})
	if rivals.size() == 2:
		state.rivals = rivals
	# the same call births the binder's own book (clamped; defaults stand
	# wherever the model came back thin)
	apply_birth(state, gen)
	return true

# ══ THE BIRTH BOOK (DAG2 L-GEN, DECISIONS.md) ════════════════════════════════
## Generated-at-birth binder content: identity, the four growth plots, the
## works vocabulary, the org spend book, THE PRICE BOOK, the birth features.
## The LLM proposes inside bands; apply_birth clamps again (LLM proposes,
## engine clamps — the law). default_birth is the DETERMINISTIC fallback: a
## keyless run gets a complete playable book from these tables alone, and
## nothing here draws from the rng (worldgen determinism stays byte-stable).
##
## state.topics shape (the contract every desk reads):
##   {"identity": {"one_liner", "who_for"},
##    "growth":   {"ads"|"content"|"referrals"|"outbound": {"name", "one_line"}},
##    "works":    {"unit_word", "capacity_word", "relief_word"}}

## The garden set — the default plots. Each channel's ENGINE CHARACTER is in
## the wording verbatim: ads instant-and-saturating, content a compounding
## stock that rots starved, referrals NPS-gated, outbound quota knocking.
const GROWTH_DEFAULTS := {
	"ads": {"name": "the paid plot",
		"one_line": "watered, it blooms the same day; unwatered, it dies the same day — and every extra dollar buys a little less"},
	"content": {"name": "the compost bed",
		"one_line": "a stock that compounds while it is fed and rots the month it is starved"},
	"referrals": {"name": "the cutting vine",
		"one_line": "a multiplier gated on how much the regulars actually like the thing"},
	"outbound": {"name": "the knocking rows",
		"one_line": "quota knocking — so many doors a week per person out knocking"},
}

## The works' native units when no key names better ones, by business type.
const WORKS_TERMS_DEFAULTS := {
	"Service": {"unit_word": "session", "capacity_word": "bookable hours", "relief_word": "freelancers"},
	"Hardware": {"unit_word": "unit", "capacity_word": "machine slots", "relief_word": "the subcontract shop"},
	"Marketplace": {"unit_word": "order", "capacity_word": "active sellers", "relief_word": "recruited supply"},
	"Software": {"unit_word": "seat", "capacity_word": "headroom", "relief_word": "burst capacity"},
}

## The bare four-line book (DECISIONS: the default when no key): one honest
## line per engine lever, at the birth budgets (zero — the levers ARE zero).
const SPEND_BOOK_DEFAULT := [
	{"name": "sales", "buys": "closing what is already in the pipe", "amt": 0, "bucket": "sales", "contract_notice": 0, "division": ""},
	{"name": "care", "buys": "keeping the customers we have", "amt": 0, "bucket": "care", "contract_notice": 0, "division": ""},
	{"name": "r&d", "buys": "building the thing", "amt": 0, "bucket": "rnd", "contract_notice": 0, "division": ""},
	{"name": "office", "buys": "the room and the people in it", "amt": 0, "bucket": "office", "contract_notice": 0, "division": ""},
]

## THE PRICE BOOK bands ([lo, hi]) and the mid-band defaults. The schema asks
## inside these same bands; this clamp is the engine's own half of the law.
## Units: flat dollars, except lease_break_weeks (weeks of rent) and
## contract_notice_wks (weeks); freelance_rate and subcontract_rate are
## dollars per overflow unit served/made.
const PRICE_BANDS := {
	"open_site_pack": [6000, 40000], "relocation_fee": [100, 1500],
	"machine_shipping": [150, 4000], "lease_break_weeks": [4, 16],
	"contract_notice_wks": [2, 12], "refinance_break_fee": [100, 2000],
	"freelance_rate": [15, 300], "subcontract_rate": [10, 250],
	"account_fire_penalty": [200, 5000],
}
const PRICE_BOOK_DEFAULT := {
	"open_site_pack": 18000, "relocation_fee": 400, "machine_shipping": 900,
	"lease_break_weeks": 8, "contract_notice_wks": 4, "refinance_break_fee": 350,
	"freelance_rate": 65, "subcontract_rate": 30, "account_fire_penalty": 1200,
}

## Generic birth features by business type: [name, job, keep_wk, unit_cost_add].
## Every set carries the four jobs — the plumbing card is where creak will live.
const FEATURE_DEFAULTS := {
	"Service": [["the signature protocol", "keep", 30, 2.0],
		["online booking", "pull", 20, 0.0],
		["the premium add-on", "charge", 15, 3.0],
		["the back office", "plumbing", 25, 0.0]],
	"Hardware": [["the core device", "keep", 40, 0.0],
		["the companion app", "pull", 25, 0.0],
		["the pro accessory line", "charge", 20, 2.0],
		["the assembly jigs", "plumbing", 30, 0.0]],
	"Marketplace": [["search & matching", "pull", 35, 0.0],
		["ratings & reviews", "keep", 20, 0.0],
		["escrow & payouts", "charge", 25, 1.0],
		["the data plumbing", "plumbing", 30, 0.0]],
	"Software": [["the onboarding door", "pull", 20, 0.0],
		["the daily workflow", "keep", 35, 0.0],
		["the paid tier", "charge", 15, 0.0],
		["the data plumbing", "plumbing", 30, 0.0]],
}

const SPEND_BUCKETS := ["sales", "care", "rnd", "office"]
const FEATURE_JOBS := ["pull", "keep", "charge", "plumbing"]
## Birth caps: per-line and whole-book weekly spend stay garage-sane.
const SPEND_LINE_CAP := 400.0
const SPEND_BOOK_CAP := 900.0

## Install the complete deterministic birth book. Guarded per field so a
## save-loaded or LLM-filled state is never clobbered. No rng in here.
static func default_birth(state: GameState) -> void:
	if state.topics.is_empty():
		state.topics = {
			"identity": {
				"one_liner": (state.company_idea.left(140) if state.company_idea != ""
					else "a small company doing what it says on the door"),
				"who_for": state.biz_who,
			},
			"growth": GROWTH_DEFAULTS.duplicate(true),
			"works": (WORKS_TERMS_DEFAULTS.get(state.biz_what,
				WORKS_TERMS_DEFAULTS["Software"]) as Dictionary).duplicate(true),
		}
	if state.spend_book.is_empty():
		state.spend_book = SPEND_BOOK_DEFAULT.duplicate(true)
	if state.price_book.is_empty():
		state.price_book = PRICE_BOOK_DEFAULT.duplicate(true)
	if state.features.is_empty():
		var rows: Array = []
		var defs: Array = FEATURE_DEFAULTS.get(state.biz_what, FEATURE_DEFAULTS["Software"])
		for i in defs.size():
			var d: Array = defs[i]
			rows.append({"id": "ft_birth_%d" % (i + 1), "name": String(d[0]),
				"job": String(d[1]), "family": "", "solidity": "solid",
				"keep_wk": int(d[2]), "unit_cost_add": float(d[3]),
				"product_id": "", "born_wk": state.week, "measured": 0.0})
		state.features = rows

## Clamp-and-write the LLM's birth blocks over the defaults. Also the PIVOT
## regeneration entry point: a nature-changing pivot calls generate_world and
## hands the fresh gen HERE (not apply_llm_world — the pivot keeps its
## investors and rivals; only the business's own book is reborn).
static func apply_birth(state: GameState, gen: Dictionary) -> bool:
	if gen.is_empty():
		return false
	# ── topics: identity + growth plots + works terms, per-piece fallback
	var identity_in: Dictionary = gen.get("identity", {})
	var growth_in: Dictionary = gen.get("growth_topics", {})
	var works_in: Dictionary = gen.get("works_terms", {})
	var growth := {}
	for ch in ["ads", "content", "referrals", "outbound"]:
		var t: Dictionary = growth_in.get(ch, {})
		var nm := String(t.get("name", "")).strip_edges().left(28)
		var ln := String(t.get("one_line", "")).strip_edges().left(110)
		if nm == "" or ln == "":
			growth[ch] = (GROWTH_DEFAULTS[ch] as Dictionary).duplicate(true)
		else:
			growth[ch] = {"name": nm, "one_line": ln}
	var works_def: Dictionary = WORKS_TERMS_DEFAULTS.get(state.biz_what,
		WORKS_TERMS_DEFAULTS["Software"])
	var one_liner := String(identity_in.get("one_liner", "")).strip_edges().left(140)
	if one_liner == "":
		one_liner = (state.company_idea.left(140) if state.company_idea != ""
			else "a small company doing what it says on the door")
	var who_for := String(identity_in.get("who_for", "")).strip_edges().left(80)
	state.topics = {
		"identity": {"one_liner": one_liner,
			"who_for": (who_for if who_for != "" else state.biz_who)},
		"growth": growth,
		"works": {
			"unit_word": _word(works_in, "unit_word", works_def, 16),
			"capacity_word": _word(works_in, "capacity_word", works_def, 28),
			"relief_word": _word(works_in, "relief_word", works_def, 28),
		},
	}
	# ── the spend book: 4-10 clean rows or the bare four lines
	var book: Array = []
	var total := 0.0
	for row in gen.get("spend_book", []):
		if not (row is Dictionary) or book.size() >= 10:
			continue
		var r: Dictionary = row
		var rname := String(r.get("name", "")).strip_edges().left(28)
		if rname == "":
			continue
		var amt := clampf(float(r.get("amt", 0.0)), 0.0, SPEND_LINE_CAP)
		book.append({"name": rname,
			"buys": String(r.get("buys", "")).strip_edges().left(60),
			"amt": int(round(amt)),
			"bucket": (String(r.get("bucket", "")) if SPEND_BUCKETS.has(String(r.get("bucket", ""))) else "office"),
			"contract_notice": clampi(int(r.get("contract_notice", 0)), 0,
				int(PRICE_BANDS["contract_notice_wks"][1])),
			"division": ""})
		total += amt
	if total > SPEND_BOOK_CAP:
		var scale := SPEND_BOOK_CAP / total
		for r2 in book:
			r2["amt"] = int(round(float(r2["amt"]) * scale))
	state.spend_book = book if book.size() >= 4 else SPEND_BOOK_DEFAULT.duplicate(true)
	# ── the price book: every key inside its band, missing keys at the default
	var pb_in: Dictionary = gen.get("price_book", {})
	var pb := {}
	for key in PRICE_BOOK_DEFAULT:
		var band: Array = PRICE_BANDS[key]
		pb[key] = clampi(int(round(float(pb_in.get(key, PRICE_BOOK_DEFAULT[key])))),
			int(band[0]), int(band[1]))
	state.price_book = pb
	# ── birth features: 3-6 rows, plumbing guaranteed (creak needs a home)
	var feats: Array = []
	# per-unit adds stay a fraction of the flagship's fair price, so a $12
	# Consumer plan can never be handed a $40 serving cost by the model
	var fair := 0.0
	if not state.offers.is_empty():
		fair = float((state.offers[0] as Dictionary).get("fair_price", 0.0))
	var add_cap := 40.0 if fair <= 0.0 else minf(40.0, fair * 0.35)
	for f in gen.get("birth_features", []):
		if not (f is Dictionary) or feats.size() >= 6:
			continue
		var fd: Dictionary = f
		var fname := String(fd.get("name", "")).strip_edges().left(28)
		if fname == "":
			continue
		feats.append({"id": "ft_birth_%d" % (feats.size() + 1), "name": fname,
			"job": (String(fd.get("job", "")) if FEATURE_JOBS.has(String(fd.get("job", ""))) else "keep"),
			"family": "", "solidity": "solid",
			"keep_wk": clampi(int(round(float(fd.get("keep_wk", 0.0)))), 0, 150),
			"unit_cost_add": snappedf(clampf(float(fd.get("unit_cost_add", 0.0)), 0.0, add_cap), 0.01),
			"product_id": "", "born_wk": state.week, "measured": 0.0})
	if feats.size() >= 3:
		var has_plumbing := false
		for f2 in feats:
			if String((f2 as Dictionary).get("job", "")) == "plumbing":
				has_plumbing = true
		if not has_plumbing:
			if feats.size() >= 6:
				feats.pop_back()
			feats.append({"id": "ft_birth_%d" % (feats.size() + 1),
				"name": "the plumbing", "job": "plumbing", "family": "",
				"solidity": "solid", "keep_wk": 25, "unit_cost_add": 0.0,
				"product_id": "", "born_wk": state.week, "measured": 0.0})
		state.features = feats
	# fewer than 3 usable rows: keep whatever book is already installed
	return true

static func _word(src: Dictionary, key: String, fallback: Dictionary, cap: int) -> String:
	var w := String(src.get(key, "")).strip_edges().left(cap)
	return w if w != "" else String(fallback.get(key, ""))

## Investor-founder compatibility: the alignment dot product → a DC nudge on
## raise checks against THIS investor. Friendly-and-aligned = easier ask.
static func investor_dc_mod(investor: Dictionary, founder_coords: Array) -> int:
	var c: Array = investor.get("coords", [0.0, 0.0])
	var dot := float(c[0]) * float(founder_coords[0]) + float(c[1]) * float(founder_coords[1])
	return clampi(int(round(-dot * 3.0)), -3, 3)

## One paragraph the DM receives every call: who exists in this world.
static func bible_digest(state: GameState) -> String:
	var bits := PackedStringArray()
	for inv in state.investors:
		var d: Dictionary = inv
		bits.append("%s (%s): \"%s\" — %s; %s; flaw: %s" % [d.get("name", "?"),
			d.get("archetype", "?"), d.get("thesis", ""), d.get("trait", ""),
			d.get("bond", ""), d.get("flaw", "")])
	for rv in state.rivals:
		var r: Dictionary = rv
		var what := String(r.get("what", ""))
		bits.append("RIVAL %s (%s)%s: plays %s" % [r.get("name", "?"),
			SimEngine._fuzz(float(r.get("strength", 20.0))),
			(" — " + what) if what != "" else "", ", ".join(r.get("tactics", []))])
	return "\n".join(bits)
