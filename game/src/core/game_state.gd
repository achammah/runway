class_name GameState
extends RefCounted
## The whole run state. Meters per PRD §3.3. Era ladder per Dossier §2:
## garage → coworking → office → floor → hq, each with its own rent and staff cap.

var week: int = 1
var era: String = "garage"
var archetype_id: String = ""
var archetype_name: String = ""
var competences: Dictionary = {"build": 3, "sell": 3, "raise": 3, "recruit": 3, "grit": 3}
## THE SIX HIDDEN TRAITS (D&D character depth, owner: "REAL impact on decisions
## and the world"). Competences are what the founder DOES and get rolled; these
## are what the founder IS and are never rolled — they bend the dice and the
## terms behind the scene. Authored per archetype in data/archetypes.json, bent
## by what is in the bag, read by SimEngine.roll_context and generate_offers.
var traits: Dictionary = {"charisma": 3, "luck": 3, "network": 3,
	"focus": 3, "credibility": 3, "stamina": 3}
var structure_id: String = "solo"
var company_name: String = "Untitled Inc"
var company_idea: String = ""
var biz_what: String = "Software"     # Software | Hardware | Marketplace | Service
var biz_who: String = "Consumer"      # Enterprise | SMB | Consumer
var funding_id: String = "bootstrap"  # bootstrap | fnf | angel
var pivots: int = 0
var last_outcome: Dictionary = {}     # last week's story, so a resumed run remembers
var ceremony_payout: int = 0          # the finale's multiplied figure; the book honors it
var run_history: Array = []           # every week: {wk, said, verdict, roll, fx} — the DM's memory

# ── SimEngine state (docs/DND_STARTUP_PLAN.md) ──────────────────────────────
var sim_seed: int = 0                 # per-run seed for the engine's salted streams
var theta: Dictionary = {}            # the world constants, generated from the pitch
var statuses: Array = []              # [{name, weeks_left}] from SimEngine.STATUS
var clocks: Array = []                # [{weeks_left, consequence}] deadlines that FIRE
var commitments: Array = []           # [{name, cash_wk, weeks_left}] recurring deltas
var pipeline: Array = []              # hires onboarding: [{name, role, salary, weeks_in}]
var price_mult: float = 1.0           # 0.5..2.0, elasticity applies
var marketing_budget: int = 0
# THE LEDGER (owner: manual weekly spend on the business's real levers).
# Set in the Binder; consumed and applied by SimEngine.weekly_tick. The four
# acquisition channels (ads/content/referrals/outbound) replaced the single
# `marketing` key; SimEngine.migrate_budgets folds a legacy save's `marketing`
# into `ads` on load and at every tick start, so an old run spends identically.
var budgets: Dictionary = {"ads": 0, "content": 0, "referrals": 0, "outbound": 0,
	"sales": 0, "care": 0, "rnd": 0, "office": 0}
# WORKING ASSUMPTIONS (owner: nobody knows their LTV on day one): what the
# founder BELIEVES about the market. Starts wrong, converges toward theta as
# analytics, customers and R&D teach the truth. The binder shows THESE.
var beliefs: Dictionary = {}
# WHAT WE SELL (owner: pricing is a real decision): 1-3 offers born with the
# world. {name, unit, fair_price, elasticity, unit_cost, price (0 = NOT ON
# SALE), weight}. Revenue exists only for priced offers, through the demand
# curve — a $500 massage sells to almost nobody.
var offers: Array = []         # $ per week
var analytics_level: int = 0          # 0..3 — the binder's fog of war
var tech_debt: float = 10.0
var fatigue: float = 20.0
var exhaustion: int = 0               # 0..6 graded burnout track
var loan_principal: int = 0           # 18%/wk bridge loan
var market_trend: float = 1.0
var last_growth: float = 0.0          # weekly revenue growth, for the valuation multiple
var rivals: Array = []                # [{name, strength, tactics[], secret}]
var investors: Array = []             # world bible: [{name, archetype, thesis, bond, flaw, coords, secret}]
var xp: int = 0
var level: int = 1
var traits_tally: Dictionary = {}     # trait -> count, for the archetype epilogue
var xp_spent: int = 0                 # stat points already circled

## Founder archetypes (research repo: startup-simulator) — matched on the trait
## tally with the (-score, -coverage, name) tie-break that stops one spammed
## trait from handing a broad archetype the win.
const FOUNDER_ARCHETYPES := [
	{"name": "The Visionary", "keys": ["long_term", "intuition_driven", "risk_taker", "independent"],
	 "line": "You saw a future. Whether anyone else lived there was always a detail."},
	{"name": "The Operator", "keys": ["long_term", "data_driven", "risk_averse", "quality_focused", "delegator"],
	 "line": "The spreadsheet loved you back. That is rarer than it sounds."},
	{"name": "The Fundraiser", "keys": ["short_term", "speed_focused", "collaborative", "diplomatic", "risk_taker"],
	 "line": "You could sell a bridge to the river. The company was sometimes the bridge."},
	{"name": "The Product Builder", "keys": ["long_term", "quality_focused", "hands_on", "collaborative"],
	 "line": "You built the thing. Then rebuilt it. The market was an afterthought you got to eventually."},
	{"name": "The Firefighter", "keys": ["short_term", "speed_focused", "hands_on", "risk_taker", "independent"],
	 "line": "Every week was an emergency and you were magnificent in exactly that weather."},
	{"name": "The People-First Leader", "keys": ["collaborative", "diplomatic", "risk_averse", "long_term"],
	 "line": "The team would follow you anywhere. Occasionally somewhere profitable."},
]

func founder_archetype() -> Dictionary:
	var best: Dictionary = {}
	var best_score := -1.0
	var best_cov := -1.0
	for a in FOUNDER_ARCHETYPES:
		var score := 0
		var matched := 0
		for k in (a["keys"] as Array):
			var c := int(traits_tally.get(String(k), 0))
			if c > 0:
				matched += 1
				score += c
		var cov := float(matched) / maxf(float((a["keys"] as Array).size()), 1.0)
		if score > best_score or (score == best_score and cov > best_cov):
			best = a
			best_score = float(score)
			best_cov = cov
	return best if best_score > 0.0 else FOUNDER_ARCHETYPES[4]
var story_so_far: String = ""         # the DM's compacted memory, ≤500 words, engine-capped
var metric_history: Array = []        # weekly snapshots for the binder's hand-drawn charts
var played_events: Array = []         # recent event titles, so the world never repeats itself
var weeks_in_red: int = 0                 # money IS the food — 3 weeks starved = dead
var history: Array = []                   # {week:int, entry:String} — everything the player did
var founder_name: String = ""   # the player's own name, written at the draft
var cofounders: Array = []   # {role, commitment, equity, vesting, name}
var employees: Array = []    # {name, role, salary, burnout 0-100, quirk}
var cash: int = 0
var product: int = 0        # 0-100 era gate
var traction: int = 0       # users
var morale: int = 60        # 0-100
var hype: int = 0           # 0-100
var founder_pct: float = 100.0
var board_seats_founder: int = 2
var board_seats_investor: int = 0
var rounds_raised: Array[String] = []  # "pre-seed" | "seed" | "series_a" | "series_b"
var missed_payrolls: int = 0
var exit_value: int = 0                # set when an acquisition is accepted
var items: Array[String] = []          # item ids banked from scrambles
var flags: Array[String] = []
var timebombs: Array = []              # {weeks_left:int, event:String}
var future_weights: Array[String] = [] # event ids boosted by weight_future
var arcs: Array = []                   # Tier-3 run-director storylines (empty w/o LLM key)
var dead: bool = false
var death_cause: String = ""

# ── SUBSYSTEM STATE (docs/design/00-spine.md §8) ────────────────────────────
## Every field below is additive with a default, so a pre-wave save loads at
## the default and an old build ignores the key: SaveSystem.VERSION stays 2.
## Durable state is a FIELD, never Object metadata — Godot metas are not in the
## save whitelist, so a meta silently resets on load (that is what broke the
## learning curve before `served_total` moved here).

# 01 catalog — cumulative customer-weeks served; drives the learning curve
var served_total: int = 0
# 02 labor market
var open_roles: Array = []       # [{role, offered_salary, opened_week, seats}]
var applicants: Array = []       # [{name, role, skill, ask, quirk, one_liner, applied_week, source}]
var recruiters: int = 0          # 0..2, floor era up
var severance_due: int = 0       # the firing invoice, booked by the NEXT tick's money section
# 04 funnel channels — the content channel's compounding stock
var content_equity: float = 0.0
# 05 enterprise pipeline
var leads: Array = []            # [{name, flavor, seats, stage, age_weeks, heat}]
var logos: Array = []            # [{name, seats, since_wk, renewal_wk}]
var pipe_units: float = 0.0      # seats of interest not yet attached to a lead
var pipe_churn_acc: float = 0.0  # fractional account-churn accumulator
var pipe_stats: Dictionary = {}  # {signed, lost, cycle_sum, seats_signed, spend, first_wk}
# 06 finance — structured notes (the legacy shark `loan_principal` still stands)
var loans: Array = []            # [{kind, principal, balance, rate_wk, term_wk, taken_week, pay_wk, missed}]
var tax_loss_carry: int = 0      # loss carryforward sheltering later profit
var last_round_amount: int = 0
var receivables: Array = []      # net-30 invoicing, floor era up
# 07 roadmap bets
var bets: Array = []             # [{id, name, desc, kind, ambition, cost_rnd_weeks, progress, ...}]
var platform_level: int = 0      # 0..4 — shipped platform bets compound velocity
# 08 board + M&A
var board: Dictionary = {}       # {target_growth_pct, target_revenue, base_revenue, review_week, strikes, goodwill}
var mna: Dictionary = {}         # {} or {buyer, price, why, premium, expires_week}
var mna_last_week: int = -99     # cooldown anchor (offer generation OR lapse)
var option_pool_pct: float = 0.0 # ESOP slice of the cap table
var founder_banked: int = 0      # secondary proceeds — the founder's, run over or not
var macro_season: String = "steady"   # "winter" | "steady" | "boom"; written by macro only
# 09 hardware production ({} on every non-Hardware run; seeded lazily)
var hardware: Dictionary = {}    # {stock, capacity_base, equipment, production_target, produced_total, subcontract_on, demand_ema}

# ── DAG2 W1 — THE BINDER REWORK'S DURABLE FIELDS (docs/design/DAG2.md §W1,
# docs/design/DECISIONS.md). Same law as above: every field is additive with a
# safe default so a pre-DAG2 save loads at the default and SaveSystem.VERSION
# stays 2. The W1 spine plants the FIELDS; the W2 lanes fill the LOGIC — until
# they land, nothing writes these and nothing reads them.
#
# divisions & sites (W2 L-DIVWORKS). A site is a ROOF the company operates
# under; divisions are never generated — born only from real ops (open_site).
var sites: Array = []            # [{id, name, rent_wk, wage_mult, learning_count, demand_weight, opened_wk}]
# THE PRICE BOOK (DECISIONS.md): the structural price schedule generated once
# at run start (and again only at a nature-changing pivot), LLM-proposed inside
# engine bands, engine-clamped. Empty until world-gen fills it. Keys:
# open_site_pack, relocation_fee, machine_shipping, lease_break_weeks,
# contract_notice_wks, refinance_break_fee, freelance_rate, subcontract_rate,
# account_fire_penalty.
var price_book: Dictionary = {}
# Generated-at-birth vocabulary (growth plots, spend rooms, works terms) —
# dressing only, the engine's numbers never live here. Empty is a valid book.
var topics: Dictionary = {}
# THE ORG SPEND BOOK (DECISIONS.md): generated lines fitted to THIS business;
# each bucket ∈ the four engine levers, engine math untouched (lever = Σ lines).
var spend_book: Array = []       # [{name, buys, amt, bucket, contract_notice, division}]
# ── the ownership cluster (W2 L-OWN)
var esop: Dictionary = {}        # {pool_pct, granted: [{emp_id, pct, vest_start_wk}]} — {} = no pool born yet
var instruments: Array = []      # [{kind: safe|note|priced|bridge, holder, amount, cap, discount,
                                 #   rate, maturity_wk, pct, prefs, protective, drag_threshold, signed_wk}]
var raise_state: Dictionary = {} # {stages: [], interest_score, active, founder_time_tax} — {} = no raise opened
var recruitment: Dictionary = {} # {roles: [], candidates: [], offers_out: []} — {} = nothing advertised
# ── the features pipeline behind WHAT WE MAKE (W2 L-MAKE)
var features: Array = []         # [{id, name, job: pull|keep|charge|plumbing, family,
                                 #   solidity: solid|creaky|breaking, keep_wk, unit_cost_add,
                                 #   product_id, born_wk, measured}]
# THE OFFER — the momentary buyout desk (W2 L-OWN). {} = nothing on the table;
# a live offer extends the board lane's M&A offers with structure
# {cash, stock+lockup, earnout+controller, retention} plus the fine-print flags.
var buyout_offer: Dictionary = {}

const RAMEN_PER_WEEK := 500    # founder personal burn, Dossier §10
## The founder's day-one bank, granted at run birth (archetype bonus, funding
## and banked scramble value ride on top). The draft screen has PROMISED this
## number since the first commit while the birth code granted only the riders,
## so a bootstrap founder started on $1,500 couch cushions against a $650/wk
## garage burn — 2.3 weeks of runway, unwinnable by arithmetic. One constant,
## read by the draft display AND the birth grant, so they can never disagree.
const START_CASH := 8000

const ERAS: Array[String] = ["garage", "coworking", "office", "floor", "hq"]
const ERA_NAMES := {
	"garage": "The Garage",
	"coworking": "Desk 47, WorkNest",
	"office": "The First Office",
	"floor": "The Startup Floor",
	"hq": "Headquarters",
}
const ERA_RENT := {"garage": 150, "coworking": 600, "office": 3000, "floor": 12000, "hq": 45000}
## What one customer-week is worth, by era (garage users are freebies and favors;
## HQ customers are contracts). Revenue only flows once something shipped.
const ERA_REV_PER_CUSTOMER := {"garage": 4, "coworking": 12, "office": 40, "floor": 100, "hq": 310}
const ERA_STAFF_CAP := {"garage": 2, "coworking": 4, "office": 9, "floor": 20, "hq": 40}
## Valuation floor per era, Dossier §10 (milestones are worth more than meters).
const ERA_VALUATION_BASE := {"garage": 50000, "coworking": 400000, "office": 2000000,
	"floor": 12000000, "hq": 60000000}

func era_index() -> int:
	return maxi(0, ERAS.find(era))

func era_display_name() -> String:
	return String(ERA_NAMES.get(era, era.capitalize()))

func staff_cap() -> int:
	return int(ERA_STAFF_CAP.get(era, 2))

func can_hire() -> bool:
	return employees.size() < staff_cap()

func revenue_per_week() -> int:
	if not (has_flag("launched") or has_flag("first_revenue")):
		return 0
	var rate := float(ERA_REV_PER_CUSTOMER.get(era, 4))
	if has_flag("premium_pricing"):
		rate *= 1.25
	elif has_flag("cheap_pricing"):
		rate *= 0.8
	return int(traction * rate)

## NET weekly cash movement: rent + ramen + payroll − revenue. Can go negative
## (a profitable company) — that's the point of the whole game.
func burn_per_week() -> int:
	var salaries := 0
	for e in employees:
		salaries += int(e.get("salary", 0))
	return int(ERA_RENT.get(era, 150)) + RAMEN_PER_WEEK + salaries - revenue_per_week()

func has_item(id: String) -> bool:
	return items.has(id)

# ── the six traits ──────────────────────────────────────────────────────────
const TRAIT_NAMES: Array[String] = ["charisma", "luck", "network", "focus",
	"credibility", "stamina"]

## Every item's trait modifiers, read once from the same JSON the shelf reads.
## The engine has to be able to ask "what does this bag do to luck" without a
## ContentDb in the room — headless tests, the draft screen and the weekly tick
## all ask, and none of them share a loader.
static var _item_traits: Dictionary = {}
static var _item_traits_read := false

static func item_trait_table() -> Dictionary:
	if _item_traits_read:
		return _item_traits
	_item_traits_read = true
	var parsed = JSON.parse_string(FileAccess.get_file_as_string("res://data/items.json"))
	if parsed is Dictionary:
		for it in (parsed as Dictionary).get("items", []):
			var mods: Dictionary = (it as Dictionary).get("trait_mods", {})
			if not mods.is_empty():
				_item_traits[String((it as Dictionary).get("id", ""))] = mods
	return _item_traits

## What the bag alone does to a trait — the number the loadout line prints.
func item_trait_delta(name: String) -> int:
	var tbl := item_trait_table()
	var d := 0
	for id in items:
		d += int((tbl.get(String(id), {}) as Dictionary).get(name, 0))
	return d

## THE ONE READING anything is allowed to use: archetype base + what you packed,
## clamped to the 1..5 the whole game speaks. Nothing else may add to a trait,
## so a screen and the engine can never disagree about who you are.
func trait_level(name: String) -> int:
	return clampi(int(traits.get(name, 3)) + item_trait_delta(name), 1, 5)

## All six, resolved. The sheet the DM is handed and the card prints.
func trait_sheet() -> Dictionary:
	var out := {}
	for t in TRAIT_NAMES:
		out[t] = trait_level(t)
	return out

func has_flag(f: String) -> bool:
	return flags.has(f)

func set_flag(f: String) -> void:
	if f != "" and not flags.has(f):
		flags.append(f)

func clampi_meters() -> void:
	product = clampi(product, 0, 100)
	morale = clampi(morale, 0, 100)
	hype = clampi(hype, 0, 100)
	traction = maxi(traction, 0)
	founder_pct = clampf(founder_pct, 0.0, 100.0)

## Everything the player does goes here; the LLM engine reads it back.
func log_action(entry: String) -> void:
	history.append({"week": week, "entry": entry})
	while history.size() > 40:
		history.pop_front()

func recent_actions(n: int = 14) -> Array:
	var out: Array = []
	for h in history.slice(maxi(0, history.size() - n)):
		out.append("wk%d: %s" % [int(h["week"]), String(h["entry"])])
	return out

# ── Cap table ──────────────────────────────────────────────────────────────
## A new investor taking X% dilutes EVERYONE pro-rata (founder and cofounders alike).
func dilute_all(investor_pct: float) -> void:
	var keep := 1.0 - clampf(investor_pct, 0.0, 45.0) / 100.0
	founder_pct *= keep
	for c in cofounders:
		c["equity"] = float(c.get("equity", 0.0)) * keep
	if founder_pct < 50.0:
		set_flag("lost_majority")
	if founder_pct < 25.0:
		set_flag("employee_of_own_company")

# ── Valuation / payout ─────────────────────────────────────────────────────
## Era milestones dominate; meters modulate. Monotonic in era, product, traction, hype.
func valuation() -> int:
	var base := float(ERA_VALUATION_BASE.get(era, 50000))
	var mult := 0.5 + product / 100.0 + traction / 50.0 + hype / 200.0
	return int(base * mult)

func payout_today() -> int:
	if exit_value > 0:
		return int(exit_value * founder_pct / 100.0)
	return int(valuation() * founder_pct / 100.0)

# ── Employees (Dossier: burnout ladder fine→frayed→cooked→gone) ────────────
static func burnout_state(b: int) -> String:
	if b >= 100: return "gone"
	if b >= 70: return "cooked"
	if b >= 40: return "frayed"
	return "fine"

## Weekly staff upkeep. Returns human-readable log lines (screens print them).
## Burnout climbs faster when morale is low or the company is starving.
func weekly_staff_tick() -> Array[String]:
	var lines: Array[String] = []
	# solvency is the mood: payroll clearing on time slowly restores the room,
	# and an actually-profitable company restores it faster and higher
	if cash > 0 and weeks_in_red == 0:
		var target := 70 if burn_per_week() < 0 else 60
		var lift := 4 if burn_per_week() < 0 else 2
		if morale < target:
			morale = mini(morale + lift, target)
	var rate := 2
	if morale < 60: rate = 5
	if morale < 40: rate = 9
	if cash < 0: rate += 4
	for e in employees.duplicate():
		var before := burnout_state(int(e.get("burnout", 0)))
		e["burnout"] = clampi(int(e.get("burnout", 0)) + rate - (3 if morale >= 75 else 0), 0, 100)
		var after := burnout_state(int(e["burnout"]))
		if after != before and after == "cooked":
			set_flag("staff_cooked")
			lines.append("%s is running on fumes." % String(e.get("name", "Someone")))
		if after == "gone":
			employees.erase(e)
			morale = clampi(morale - 8, 0, 100)
			set_flag("staff_quit")
			lines.append("%s quit. The chair is still warm." % String(e.get("name", "Someone")))
	return lines

## Payroll miss: call when cash went negative on payday. Two misses = demotion risk.
func note_missed_payroll() -> void:
	missed_payrolls += 1
	set_flag("missed_payroll")
	if missed_payrolls >= 2:
		set_flag("payroll_crisis")

# ── Era ladder ─────────────────────────────────────────────────────────────
## Checks the Dossier §2 gates. Returns {changed, from, to, reason} — screens
## react (scene swap, memorabilia); this only mutates state.
func advance_era_if_ready() -> Dictionary:
	var from := era
	var to := ""
	var reason := ""
	match era:
		"garage":
			if product >= 60 and (traction >= 5 or has_flag("first_revenue")):
				to = "coworking"; reason = "something works and someone noticed"
		"coworking":
			# the SAME cushion law as office→floor: promotion into 5x rent
			# was bankrupting healthy companies at week 21 (C5 audit D5)
			if has_flag("launched") and traction >= 25 and cash >= 6 * int(ERA_RENT["office"]):
				to = "office"; reason = "launched, and the numbers kept moving"
		"office":
			# no moving into rent you can't pay — the deadly jumps need a cushion
			if has_flag("pmf") and has_flag("seed_raised") and cash >= 6 * int(ERA_RENT["floor"]):
				to = "floor"; reason = "product-market fit with money behind it"
		"floor":
			if has_flag("series_a") and traction >= 100 and cash >= 6 * int(ERA_RENT["hq"]):
				to = "hq"; reason = "Series A and a hundred believers"
	if to == "":
		return {"changed": false}
	era = to
	morale = clampi(morale + 10, 0, 100)
	set_flag("moved_up_" + to)
	log_action("MOVED UP: %s → %s (%s)" % [from, to, reason])
	return {"changed": true, "from": from, "to": to, "reason": reason}

## Demotion (missed payroll ×2 or a down round). Moving down hurts.
func demote(reason: String) -> Dictionary:
	var idx := era_index()
	if idx <= 0:
		return {"changed": false}
	var from := era
	era = ERAS[idx - 1]
	morale = clampi(morale - 25, 0, 100)
	missed_payrolls = 0
	flags.erase("payroll_crisis")
	set_flag("moved_down_" + era)
	log_action("MOVED DOWN: %s → %s (%s)" % [from, era, reason])
	return {"changed": true, "from": from, "to": era, "reason": reason}

## The run director's beat directives that apply to the CURRENT era.
## One line per active beat: "KIND [actors]: directive". Empty when no arcs.
func active_arc_directives() -> Array[String]:
	var out: Array[String] = []
	for a in arcs:
		for b in a.get("beats", []):
			if b is Dictionary and String(b.get("era", "")) == era:
				var actors: PackedStringArray = []
				for n in a.get("actors", []):
					actors.append(String(n))
				out.append("%s [%s]: %s" % [String(a.get("kind", "arc")).to_upper(),
					", ".join(actors), String(b.get("directive", ""))])
	return out

## Compact digest for the LLM layer (PRD §7: run-state digest, stable field order).
## The DM's memory: every week verbatim for the recent past, compressed further
## back — decisions, verdicts, rolls and consequences compound across a run.
func history_digest() -> Array:
	var out: Array = []
	var n := run_history.size()
	for i in n:
		var h: Dictionary = run_history[i]
		if i >= n - 12:
			out.append(h)
		else:
			out.append({"wk": h.get("wk", 0), "said": String(h.get("said", "")).left(40),
				"verdict": h.get("verdict", "")})
	return out

func to_digest() -> Dictionary:
	var staff: Array = []
	for e in employees:
		staff.append("%s (%s, burnout: %s)" % [e.get("name", "?"), e.get("role", "?"),
			burnout_state(int(e.get("burnout", 0)))])
	var d := {
		"week": week,
		"era": era,
		"era_name": era_display_name(),
		"company_name": company_name,
		"founder_name": founder_name,
		"company_does": company_idea,
		"business_model": "%s for %s" % [biz_what, biz_who],
		"funding_path": "bootstrapped" if funding_id == "bootstrap" else "outside money taken (%s)" % funding_id,
		"rounds_raised": rounds_raised.duplicate(),
		"employees": 1 + cofounders.size() + employees.size(),
		"staff": staff,
		"staff_cap": staff_cap(),
		"customers": traction,
		"product_version": "v0.%d" % maxi(1, product / 10),
		"pivots_so_far": pivots,
		"weeks_in_the_red": weeks_in_red,
		"recent_actions": recent_actions(),
		"founder_archetype": archetype_name,
		"competences": competences,
		"traits": trait_sheet(),
		"cofounders": cofounders,
		"cash": cash,
		"weekly_burn": burn_per_week(),
		"weekly_revenue": revenue_per_week(),
		"valuation": valuation(),
		"board": "%d founder seats, %d investor seats" % [board_seats_founder, board_seats_investor],
		# 08 — present only when live, so the DM never narrates a board that
		# does not exist or a deal that is not on the table.
		"board_review": ("covenant $%d/wk by wk %d · now $%d/wk · strikes %d · goodwill %d" % [
			int(board.get("target_revenue", 0)), int(board.get("review_week", 0)),
			int((get_meta("pnl", {}) as Dictionary).get("revenue", 0)),
			int(board.get("strikes", 0)), int(board.get("goodwill", 0))]) if not board.is_empty() else "",
		"acquisition_offer": ("%s at $%d — the no-shop ends wk %d" % [
			String(mna.get("buyer", "")), int(mna.get("price", 0)),
			int(mna.get("expires_week", 0))]) if not mna.is_empty() else "",
		"ipo_window": has_flag("ipo_window"),
		"product": product,
		"traction": traction,
		"morale": morale,
		"hype": hype,
		"founder_pct": founder_pct,
		"items": items.duplicate(),
		"flags": flags.duplicate(),
	}
	# 05 — the named pipeline rides the digest (empty off Enterprise)
	var pipe := SimPipeline.digest_rows(self)
	for pk in pipe:
		d[pk] = pipe[pk]
	return d
