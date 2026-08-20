class_name GameState
extends RefCounted
## The whole run state. Meters per PRD §3.3. Era ladder per Dossier §2:
## garage → coworking → office → floor → hq, each with its own rent and staff cap.

var week: int = 1
var era: String = "garage"
var archetype_id: String = ""
var archetype_name: String = ""
var competences: Dictionary = {"build": 3, "sell": 3, "raise": 3, "recruit": 3, "grit": 3}
var structure_id: String = "solo"
var company_name: String = "Untitled Inc"
var company_idea: String = ""
var biz_what: String = "Software"     # Software | Hardware | Marketplace | Service
var biz_who: String = "Consumer"      # Enterprise | SMB | Consumer
var funding_id: String = "bootstrap"  # bootstrap | fnf | angel
var pivots: int = 0
var last_outcome: Dictionary = {}     # last week's story, so a resumed run remembers
var weeks_in_red: int = 0                 # money IS the food — 3 weeks starved = dead
var history: Array = []                   # {week:int, entry:String} — everything the player did
var cofounders: Array = []   # {role, commitment, equity, vesting}
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

const RAMEN_PER_WEEK := 500    # founder personal burn, Dossier §10

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
			if has_flag("launched") and traction >= 25:
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
func to_digest() -> Dictionary:
	var staff: Array = []
	for e in employees:
		staff.append("%s (%s, burnout: %s)" % [e.get("name", "?"), e.get("role", "?"),
			burnout_state(int(e.get("burnout", 0)))])
	return {
		"week": week,
		"era": era,
		"era_name": era_display_name(),
		"company_name": company_name,
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
		"cofounders": cofounders,
		"cash": cash,
		"weekly_burn": burn_per_week(),
		"weekly_revenue": revenue_per_week(),
		"valuation": valuation(),
		"board": "%d founder seats, %d investor seats" % [board_seats_founder, board_seats_investor],
		"product": product,
		"traction": traction,
		"morale": morale,
		"hype": hype,
		"founder_pct": founder_pct,
		"items": items.duplicate(),
		"flags": flags.duplicate(),
	}
