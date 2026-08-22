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
		rivals.append({
			"name": String(r.get("name", "a rival")).left(30),
			"what": what_txt,
			"strength": float(str_map.get(String(r.get("strength", "scrappy")), 25.0)),
			"tactics": r.get("tactics", ["shipped something loud"]),
			"weeks_since_move": 0,
			"secret": "",
		})
	if rivals.size() == 2:
		state.rivals = rivals
	return true

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
