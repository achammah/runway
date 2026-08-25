class_name SimFactory
extends RefCounted
## LANE 09 — HARDWARE PRODUCTION (build, stock, machines). Spec: docs/design/09-hardware.md
##
## Bonopoly's loop scaled to a garage: you must BUILD what you sell. Every
## mechanic here is a scaled-down textbook manufacturing model, named in its own
## comment — rough-cut capacity planning, periodic-review base-stock, inventory
## holding cost, Wright's experience curve, make-vs-buy premium, constant-hazard
## reliability, lost-sales fill rate. Zero LLM: the equipment catalog is
## authored, every number is engine-owned.
##
## THE ACTIVE GUARD, first line of every entry point: a run that is not Hardware
## never allocates the state, never draws a die, never writes a lane, never
## files a row. On those runs the tick is arithmetically what it was before this
## file had a body — that absence is a tested state (pin 4).
##
## The spine calls, in tick order (docs/design/00-spine.md §1, HOOKS.md):
##   tick_pre   tick §7h — PRODUCE FIRST, before adoption can spend the shelf
##   tick_money the money section — write ONLY the P&L lanes this subsystem owns
##   tick_post  after the week's record is written and can be read back
## and outside the tick: directives() feeds the DM block, attention() feeds
## every bang in the game through SimEngine.attention_items.

# ── THE CONSTANTS (docs/design/09-hardware.md §3, §5, §6, §7, §8) ─────────────
## Rough-cut capacity: one ops hire is 5..15 units/wk by skill, 10 at neutral.
const HW_OPS_UNITS := 10.0
## AUTO is a periodic-review base-stock policy: review period R = 1 week,
## order-up-to level S = 4 weeks of the smoothed forecast.
const HW_AUTO_COVER_WK := 4.0
const HW_AUTO_DEMAND_FLOOR := 2.0
const HW_AUTO_CASH_SHARE := 0.25       ## AUTO never spends more than a quarter of cash
const HW_DEMAND_ALPHA := 0.3           ## exponential smoothing on true weekly demand
## Empty shelves push people out: churn ×(1 + 0.35×(1 − fill rate)).
const HW_STARVE_CHURN := 0.35
## Wright's law on cumulative units BUILT — the linear-in-log approximation,
## −11.5 points per 10× ≈ −3.5%/doubling, floored at the purchased-BOM share.
const HW_LEARN_RATE := 0.115
const HW_LEARN_FLOOR := 0.65
## Inventory holding cost: 2%/wk of unit cost ≈ 104%/yr, the obsolescence-heavy
## end of the 20-30%/yr durable-goods rule, compressed to bite inside a run.
const HW_CARRY_RATE := 0.02
const HW_CARRY_MIN := 0.10             ## cheap gadgets still need shelves
## Overstock is money asleep: more than 8 weeks of cover, and more than 20 units.
const HW_OVERSTOCK_COVER := 8.0
const HW_OVERSTOCK_MIN := 20
## Constant-hazard (exponential) reliability: memoryless failure at MTBF ≈ 50
## machine-weeks, MTTR floored at one tick, repair priced at a month of that
## machine's preventive-maintenance budget.
const HW_BREAK_P := 0.02
const HW_BREAK_CAP := 0.15
const HW_REPAIR_X := 4.0
## The secondary market takes half (docs/design/DECISIONS.md #4).
const HW_RESALE_PCT := 0.5
const HW_FLEET_MAX := 12
## What a customer buys again in a week at unit cadence — 0.2 = one unit per
## five weeks, the same cadence the catalog bills them at.
const HW_UNIT_CADENCE := 0.2
## No lane floods the journal (docs/design/00-spine.md §11).
const HW_LINE_CAP := 4

## THE AUTHORED EQUIPMENT CATALOG. Capacity is bought in LUMPS (stepwise
## expansion — nobody sells 3% of a reflow oven); upkeep is ≈1.6%/wk of price,
## the maintenance-budget rule of thumb at this compression; and $ per unit of
## capacity improves with scale, which is economies of scale in capex. The LLM
## never touches this table: era gates, save-compat and twin parity all need it
## typed and stable. Ascending capacity IS the ladder the buy row walks.
const HW_EQUIPMENT := [
	{"id": "jig", "name": "Assembly Jig", "era": "garage",
		"price": 900, "capacity_add": 6.0, "upkeep_wk": 15.0},
	{"id": "pick_place", "name": "Benchtop Pick-and-Place", "era": "coworking",
		"price": 3_500, "capacity_add": 18.0, "upkeep_wk": 60.0},
	{"id": "reflow", "name": "Reflow Oven Line", "era": "coworking",
		"price": 12_000, "capacity_add": 45.0, "upkeep_wk": 180.0},
	{"id": "cnc", "name": "CNC Cell", "era": "office",
		"price": 45_000, "capacity_add": 140.0, "upkeep_wk": 600.0},
	{"id": "line", "name": "Assembly Line", "era": "floor",
		"price": 180_000, "capacity_add": 450.0, "upkeep_wk": 2_200.0},
	{"id": "lightsout", "name": "Lights-Out Cell", "era": "hq",
		"price": 700_000, "capacity_add": 1_500.0, "upkeep_wk": 7_000.0},
]

## MAKE VS BUY, era-laddered. A contract manufacturer's quote is your marginal
## cost plus THEIR margin, overhead and transaction costs. Relationship and
## committed volume narrow the premium and widen the ceiling — the era IS the
## relationship maturity, so this needs no state of its own.
##   coworking  a local jobber takes small overflow at spot rates
##   office     a real CM relationship
##   floor/hq   supplier contract terms: committed volume prices it down
const HW_SUB_CAP_X := {"coworking": 1.0, "office": 3.0, "floor": 3.0, "hq": 3.0}
const HW_SUB_MULT := {"coworking": 1.6, "office": 1.6, "floor": 1.45, "hq": 1.35}

# ── STATE ────────────────────────────────────────────────────────────────────
## True only on the runs this whole file is allowed to touch.
static func active(state: GameState) -> bool:
	return state.biz_what == "Hardware"

static func _defaults() -> Dictionary:
	return {"stock": 0, "capacity_base": 6.0, "equipment": [],
		"production_target": -1, "produced_total": 0,
		"subcontract_on": false, "demand_ema": 0.0}

## THE ONLY PLACE ALLOCATION HAPPENS. Callers have already checked `active`.
static func hw_state(state: GameState) -> Dictionary:
	if state.hardware.is_empty():
		state.hardware = _defaults()
	return state.hardware

## The read-only twin: hands back the same shape without ever writing state, so
## a desk repaint or an attention scan can never seed a run into existence.
static func hw_view(state: GameState) -> Dictionary:
	return state.hardware if not state.hardware.is_empty() else _defaults()

## THE WEEK'S WORKING BLOCK — transient display + bookkeeping data for the week
## just simulated, on the same contract as `pnl` (docs/design/09-hardware.md §1:
## durable state is a FIELD, per-week display data MAY be meta).
static func week_block(state: GameState) -> Dictionary:
	if not state.has_meta("hw"):
		return {}
	var w: Dictionary = state.get_meta("hw", {})
	return w

# ── THE FLAGSHIP BINDING (what a "unit" is) ──────────────────────────────────
## Production builds the FLAGSHIP: the first offer billed per unit. A pure
## selector, never a stored index — `remove_offer` shifts the array and a stored
## one would dangle. −1 only on a run with no catalog at all.
static func flagship_index(state: GameState) -> int:
	for i in state.offers.size():
		var od: Dictionary = state.offers[i]
		if is_equal_approx(SimEngine.offer_cadence(String(od.get("unit", ""))), HW_UNIT_CADENCE):
			return i
	return 0 if state.offers.size() > 0 else -1

static func flagship(state: GameState) -> Dictionary:
	var i := flagship_index(state)
	if i < 0:
		return {}
	var od: Dictionary = state.offers[i]
	return od

static func flagship_name(state: GameState) -> String:
	var f := flagship(state)
	return String(f.get("name", "the first unit")) if not f.is_empty() else "the first unit"

## The production cost basis: Σ the flagship's variable cost lines, catalog-owned
## and catalog-clamped. A legacy run with no offers falls back to the theta arpu
## the rest of the engine bills on (0.35 margin share at unit cadence).
static func unit_cost(state: GameState) -> float:
	var f := flagship(state)
	if f.is_empty():
		return maxf(1.75 * float(state.theta.get("arpu_wk", 5.0)), 0.0)
	return maxf(float(f.get("unit_cost", 0.0)), 0.0)

## What one unit actually invoices for — the founder's price, or the going rate
## while unpriced. Used only to un-bill units that never shipped.
static func billed_price(state: GameState) -> float:
	var f := flagship(state)
	if f.is_empty():
		return maxf(float(state.theta.get("arpu_wk", 5.0)) / HW_UNIT_CADENCE, 0.0)
	return SimEngine.offer_billed_price(f, SimEngine.street_fair_mult(state))

# ── THE LEARNING CURVE (Wright's law, on units BUILT) ────────────────────────
## Unit cost falls a fixed fraction per DOUBLING of cumulative output. Ours is
## the linear-in-log approximation of C(N) = C₁·N^−b, deliberately gentler than
## aerospace's 80-85% curves because a garage builds one product out of bought
## parts — and floored at 0.65 because learning compresses labor, assembly and
## yield, never the purchased BOM. The floor IS the material share.
##
## THE OTHER CURVE IS NOT THIS ONE (docs/design/00-spine.md §13): the catalog's
## `served_total` curve discounts SERVING; this one discounts BUILDING. Neither
## reads the other, and subcontracted units earn neither.
static func learning(state: GameState) -> float:
	return _learning_of(int(hw_view(state).get("produced_total", 0)))

static func _learning_of(made: int) -> float:
	if made <= 1:
		return 1.0
	return maxf(1.0 - HW_LEARN_RATE * (log(float(made)) / log(10.0)), HW_LEARN_FLOOR)

## The discount as whole percent — what the strip prints and what the milestone
## receipt fires on (one line per new whole point, never weekly spam).
static func _learn_step(made: int) -> int:
	return int(floor((1.0 - _learning_of(made)) * 100.0))

static func learning_pct(state: GameState) -> int:
	return _learn_step(int(hw_view(state).get("produced_total", 0)))

# ── CAPACITY (rough-cut capacity planning) ───────────────────────────────────
## Available output = rated machine capacity + direct labor, in units per
## period. ONE aggregate resource pool, no routings: a weekly tick and a single
## flagship SKU need exactly one honest number, units/wk. `down_i` is the index
## of this week's broken machine, which contributes nothing.
##
## Ops heads are read-only coordination with 02: labor owns the roster, we only
## ask whether a role says "ops" and how skilled they are (default 3 = neutral,
## which is exact parity for a roster with no skill field yet).
static func capacity(state: GameState, down_i: int = -1) -> float:
	var hw := hw_view(state)
	var cap := maxf(float(hw.get("capacity_base", 6.0)), 0.0)
	var eq: Array = hw.get("equipment", [])
	for i in eq.size():
		if i == down_i:
			continue
		cap += maxf(float((eq[i] as Dictionary).get("capacity_add", 0.0)), 0.0)
	for e in state.employees:
		if String((e as Dictionary).get("role", "")).contains("ops"):
			var skill := clampi(int((e as Dictionary).get("skill", 3)), 1, 5)
			cap += HW_OPS_UNITS * (1.0 + 0.25 * float(skill - 3))
	return maxf(cap, 0.0)

## THE WEEK'S BUILD ORDER: the founder's stepper, or AUTO's base-stock policy.
## World-clamped here and only here — the desk is never trusted.
static func target_now(state: GameState, cap: float, unit_cost_eff: float) -> int:
	var hw := hw_view(state)
	var ceiling := maxi(int(floor(cap)), 0)
	var tgt := int(hw.get("production_target", -1))
	if tgt >= 0:
		# the manual stepper is uncapped by cash on purpose — going red is a
		# choice, and the reaper already prices it
		return clampi(tgt, 0, ceiling)
	# AUTO: order up to 4 weeks of the smoothed forecast, minus what is already
	# on the shelf, and never spend more than a quarter of the cash on one week.
	var cover := HW_AUTO_COVER_WK * maxf(float(hw.get("demand_ema", 0.0)), HW_AUTO_DEMAND_FLOOR)
	var want := clampi(int(round(cover)) - int(hw.get("stock", 0)), 0, ceiling)
	var affordable := int(floor(HW_AUTO_CASH_SHARE * maxf(float(state.cash), 0.0)
			/ maxf(unit_cost_eff, 0.01)))
	return maxi(mini(want, affordable), 0)

## The stepper's write path: clamped at the boundary, AUTO is −1.
static func set_target(state: GameState, v: int) -> void:
	if not active(state):
		return
	var hw := hw_state(state)
	if v < 0:
		hw["production_target"] = -1
		return
	hw["production_target"] = clampi(v, 0, maxi(int(floor(capacity(state))), 0))

# ── MAKE VS BUY ──────────────────────────────────────────────────────────────
static func sub_unlocked(state: GameState) -> bool:
	return state.era_index() >= 1

static func sub_mult(era: String) -> float:
	return float(HW_SUB_MULT.get(era, 1.6))

## A jobber will not book unlimited line time for a small client: the ceiling is
## a multiple of YOUR OWN footprint, so equipment stays the growth spine and the
## toggle only ever buys slack.
static func sub_cap_units(state: GameState, cap: float) -> int:
	if not sub_unlocked(state):
		return 0
	return maxi(int(floor(float(HW_SUB_CAP_X.get(state.era, 0.0)) * maxf(cap, 0.0))), 0)

static func toggle_subcontract(state: GameState) -> void:
	if not active(state) or not sub_unlocked(state):
		return
	var hw := hw_state(state)
	hw["subcontract_on"] = not bool(hw.get("subcontract_on", false))

# ── CARRYING, FILL, OVERSTOCK ────────────────────────────────────────────────
## What one unit costs to sit on a shelf for one week: capital tied up, storage,
## insurance, shrinkage and — the big one for a gadget — obsolescence.
static func carrying_rate(state: GameState) -> float:
	return maxf(HW_CARRY_RATE * unit_cost(state), HW_CARRY_MIN)

## Empty shelves are a retention problem, not just a sales one.
static func starve_churn_mult(state: GameState) -> float:
	var fill := float(week_block(state).get("fill", 1.0))
	return 1.0 + HW_STARVE_CHURN * (1.0 - clampf(fill, 0.0, 1.0))

static func overstock(state: GameState) -> bool:
	if not active(state) or state.hardware.is_empty():
		return false
	var hw: Dictionary = state.hardware
	var stock := int(hw.get("stock", 0))
	return float(stock) > HW_OVERSTOCK_COVER * maxf(float(hw.get("demand_ema", 0.0)), 1.0) \
			and stock > HW_OVERSTOCK_MIN

## What the books over-billed this week: repeat buyers who found empty shelves
## were never handed a unit, so nobody may invoice them for one (owner's law
## #196). Deducted through the working money record, which the spine reads back.
static func unserved_billing(state: GameState) -> float:
	if not active(state):
		return 0.0
	var w := week_block(state)
	var fill := clampf(float(w.get("fill", 1.0)), 0.0, 1.0)
	return maxf(billed_price(state) * HW_UNIT_CADENCE
			* float(w.get("demand_base", 0.0)) * (1.0 - fill), 0.0)

# ── THE EQUIPMENT CATALOG ────────────────────────────────────────────────────
static func catalog_entry(id: String) -> Dictionary:
	for e in HW_EQUIPMENT:
		var ed: Dictionary = e
		if String(ed.get("id", "")) == id:
			return ed
	return {}

static func _era_ok(state: GameState, entry: Dictionary) -> bool:
	return GameState.ERAS.find(state.era) >= GameState.ERAS.find(String(entry.get("era", "garage")))

## Every refusal in one place, each with the sentence the desk prints where the
## button would have been — a gate that hides itself teaches nothing.
static func can_buy(state: GameState, id: String) -> Dictionary:
	if not active(state):
		return {"ok": false, "why": "hardware runs only"}
	var e := catalog_entry(id)
	if e.is_empty():
		return {"ok": false, "why": "no such machine"}
	if not _era_ok(state, e):
		return {"ok": false, "why": "the %s era unlocks it" % String(e.get("era", "garage"))}
	var price := int(e.get("price", 0))
	if price > SimEngine.era_spend_cap(state.era):
		return {"ok": false, "why": "past what a %s can sign for" % state.era}
	var owned: Array = hw_view(state).get("equipment", [])
	if owned.size() >= HW_FLEET_MAX:
		return {"ok": false, "why": "the floor holds %d machines" % HW_FLEET_MAX}
	if state.cash < price:
		return {"ok": false, "why": "$%d short" % (price - state.cash)}
	return {"ok": true, "why": ""}

## One-off cash out; the machine's capacity and upkeep are DENORMALIZED at
## purchase so a later catalog rebalance never rewrites an asset you own.
static func buy_equipment(state: GameState, id: String) -> Dictionary:
	var verdict := can_buy(state, id)
	if not bool(verdict.get("ok", false)):
		return verdict
	var e := catalog_entry(id)
	var hw := hw_state(state)
	state.cash -= int(e.get("price", 0))
	(hw["equipment"] as Array).append({
		"id": id, "name": String(e.get("name", id)),
		"capacity_add": float(e.get("capacity_add", 0.0)),
		"upkeep_wk": float(e.get("upkeep_wk", 0.0)),
		"bought_week": state.week})
	state.log_action("BOUGHT %s ($%d, +%d units/wk, $%d/wk upkeep)" % [
		String(e.get("name", id)), int(e.get("price", 0)),
		int(e.get("capacity_add", 0)), int(e.get("upkeep_wk", 0))])
	return {"ok": true, "why": ""}

## Half of what it cost — the real secondhand haircut (DECISIONS.md #4). CAPEX
## is forgiving and costly at the same time: the way out exists, and it bills.
static func resale_value(id: String) -> int:
	var e := catalog_entry(id)
	return int(float(e.get("price", 0)) * HW_RESALE_PCT) if not e.is_empty() else 0

static func can_sell(state: GameState, idx: int) -> Dictionary:
	if not active(state):
		return {"ok": false, "why": "hardware runs only"}
	var eq: Array = hw_view(state).get("equipment", [])
	if idx < 0 or idx >= eq.size():
		return {"ok": false, "why": "no machine there"}
	if catalog_entry(String((eq[idx] as Dictionary).get("id", ""))).is_empty():
		return {"ok": false, "why": "no buyer for that one"}
	return {"ok": true, "why": ""}

static func sell_equipment(state: GameState, idx: int) -> Dictionary:
	var verdict := can_sell(state, idx)
	if not bool(verdict.get("ok", false)):
		return verdict
	var hw := hw_state(state)
	var eq: Array = hw["equipment"]
	var m: Dictionary = eq[idx]
	var id := String(m.get("id", ""))
	var back := resale_value(id)
	var paid := int(catalog_entry(id).get("price", 0))
	eq.remove_at(idx)
	state.cash += back
	state.log_action("SOLD %s for $%d (half of $%d — the secondhand haircut)" % [
		String(m.get("name", id)), back, paid])
	return {"ok": true, "why": "", "back": back, "paid": paid,
		"name": String(m.get("name", id))}

## What the desk's buy row shows: the priciest machine this week can actually
## sign for, and the next rung of the ladder above it — dimmed, wearing the
## engine's own refusal, so the gate is visible instead of missing.
static func buy_row(state: GameState) -> Array:
	if not active(state):
		return []
	var legal: Array = []
	for i in HW_EQUIPMENT.size():
		if _era_ok(state, HW_EQUIPMENT[i]):
			legal.append(i)
	if legal.is_empty():
		return []
	var pick := int(legal[0])
	for i in legal:
		if bool(can_buy(state, String((HW_EQUIPMENT[int(i)] as Dictionary).get("id", ""))).get("ok", false)):
			pick = int(i)
	var out: Array = [_buy_cell(state, pick)]
	if pick + 1 < HW_EQUIPMENT.size():
		out.append(_buy_cell(state, pick + 1))
	return out

static func _buy_cell(state: GameState, i: int) -> Dictionary:
	var e: Dictionary = HW_EQUIPMENT[i]
	var v := can_buy(state, String(e.get("id", "")))
	return {"entry": e, "ok": bool(v.get("ok", false)), "why": String(v.get("why", ""))}

# ═════════════════════════ THE WEEKLY TICK ═══════════════════════════════════

## Tick §7h: breakdown roll → capacity → build target → produce (learning curve).
## PRODUCE FIRST — stock must exist before §8 is allowed to sell it. Without it
## week one would lose its launch: the shelf would be empty before any decision
## existed. The draw order is FIXED (replay-exact): ① breakdown randf,
## ② the picked machine, and only then arithmetic.
static func tick_pre(state: GameState, rep: Dictionary) -> void:
	if not active(state):
		return
	var hw := hw_state(state)
	var eq: Array = hw["equipment"]
	var r := SimEngine.rng_for(state, SimEngine.SALT_HW_BREAKDOWN)
	var down_i := -1
	var down_name := ""
	var repair := 0.0
	if eq.size() > 0 and r.randf() < minf(HW_BREAK_P * float(eq.size()), HW_BREAK_CAP):
		down_i = r.randi_range(0, eq.size() - 1)
		var m: Dictionary = eq[down_i]
		down_name = String(m.get("name", "a machine"))
		# corrective repair, priced at about a month of that machine's
		# preventive-maintenance budget. One week down, then it runs again —
		# MTTR floored at one tick keeps the repair queue at zero state.
		repair = HW_REPAIR_X * float(m.get("upkeep_wk", 0.0))
	var cap := capacity(state, down_i)
	var made_before := int(hw["produced_total"])
	var uc_eff := unit_cost(state) * learning(state)
	var built := target_now(state, cap, uc_eff)
	hw["stock"] = int(hw["stock"]) + built
	hw["produced_total"] = made_before + built
	var util := 0.0 if cap <= 0.0 else clampf(float(built) / cap, 0.0, 1.0)
	# the week's working block: everything §8, §9 and the strip read back
	var w := {
		"week": state.week, "built": built, "capacity": cap,
		"utilization": util, "unit_cost_eff": uc_eff,
		"down_name": down_name, "down_i": down_i, "repair": repair,
		"sub_units": 0, "lost_adds": 0, "fill": 1.0, "sold": 0, "served": 0,
		"demand_base": float(state.traction), "demand_units": 0.0,
		"shelf": int(hw["stock"]),
		"stock_end": int(hw["stock"]), "carrying": 0.0, "upkeep": 0.0,
		"walked": 0,
		"learn_step": _learn_step(int(hw["produced_total"])),
		"learn_step_up": _learn_step(int(hw["produced_total"])) > _learn_step(made_before),
	}
	state.set_meta("hw", w)
	rep["hw"] = w

## THE STOCK SEAM (tick §8, after the go-to-market clamp — you cannot sell from
## a shelf faster than the team can close, and you cannot sell at all from a
## shelf that is empty). Off Hardware this hands `adds` straight back and draws
## nothing, so demand stays stock-free exactly as it is today.
##
## Lost-sales retail, not backorders: consumer hardware simply does not queue.
## Unmet demand is gone, receipted, and it pushes the people it disappointed out.
static func clamp_adds(state: GameState, _rep: Dictionary, adds: float) -> float:
	if not active(state):
		return adds
	var hw := hw_state(state)
	var w := week_block(state)
	var r := SimEngine.rng_for(state, SimEngine.SALT_HW_REPURCHASE)
	var A := float(state.traction)
	# EXISTING CUSTOMERS COME BACK: at unit cadence a customer buys again about
	# every five weeks. The seeded remainder keeps a 0.4-unit week real.
	var u_exist := _seeded_int(A * HW_UNIT_CADENCE, r)
	var served := mini(u_exist, int(hw["stock"]))
	hw["stock"] = int(hw["stock"]) - served
	var unserved_exist := u_exist - served
	var adds_raw := maxf(adds, 0.0)
	var short_adds := maxi(int(ceil(adds_raw - float(hw["stock"]))), 0)
	# MAKE VS BUY: made-to-order overflow. Sub units serve the people already
	# waiting first, then new customers; they NEVER enter stock, never bill
	# carrying, and teach the bench nothing (no produced_total, no learning).
	var sub_units := 0
	var sub_to_adds := 0
	if bool(hw.get("subcontract_on", false)) and sub_unlocked(state):
		var want := unserved_exist + short_adds
		sub_units = mini(want, sub_cap_units(state, float(w.get("capacity", capacity(state)))))
		sub_units = maxi(sub_units, 0)
		var to_exist := mini(sub_units, unserved_exist)
		served += to_exist
		sub_to_adds = sub_units - to_exist
	adds = minf(adds_raw, float(hw["stock"]) + float(sub_to_adds))
	var lost := maxi(int(round(adds_raw - adds)), 0)
	# a new customer's first unit ships at signup; the books keep billing the
	# catalog's smoothed ARPU-week (§12: divergence ≤ 1 unit/wk by construction)
	var off_shelf := mini(int(round(minf(adds, float(hw["stock"])))), int(hw["stock"]))
	hw["stock"] = int(hw["stock"]) - off_shelf
	# THE FORECAST the base-stock policy orders against: plain exponential
	# smoothing over TRUE demand — what people wanted, not what we managed to
	# hand over. Forecasting on served units would starve a starving factory.
	hw["demand_ema"] = (1.0 - HW_DEMAND_ALPHA) * float(hw.get("demand_ema", 0.0)) \
			+ HW_DEMAND_ALPHA * (float(u_exist) + adds_raw)
	w["sub_units"] = sub_units
	w["served"] = served
	w["sold"] = off_shelf + served + sub_to_adds
	w["lost_adds"] = lost
	w["fill"] = 1.0 if u_exist == 0 else clampf(float(served) / float(u_exist), 0.0, 1.0)
	w["demand_units"] = float(u_exist) + adds_raw
	w["demand_base"] = A
	w["stock_end"] = int(hw["stock"])
	return adds

## Godot's own seeded-remainder idiom: a 0.4-unit week is a REAL week, and
## int(round()) would erase it forever.
static func _seeded_int(x: float, r: RandomNumberGenerator) -> int:
	var v := maxf(x, 0.0)
	var whole := int(floor(v))
	if r.randf() < v - float(whole):
		whole += 1
	return whole

## The money section. Four lanes, all of them joining burn: what the bench built,
## what the contract manufacturer charged, what the fleet costs to keep, and what
## the shelf costs to hold.
static func tick_money(state: GameState, _rep: Dictionary, m: Dictionary) -> void:
	if not active(state):
		return
	var hw := hw_state(state)
	var w := week_block(state)
	var built := int(w.get("built", 0))
	var uc_eff := float(w.get("unit_cost_eff", unit_cost(state) * learning(state)))
	m["production"] = float(m.get("production", 0.0)) + float(built) * uc_eff
	# the sub's price is the sub's price: no learning discount rides it
	var sub_cost := float(int(w.get("sub_units", 0))) * sub_mult(state.era) * unit_cost(state)
	m["subcontract"] = float(m.get("subcontract", 0.0)) + sub_cost
	# FIXED COST: idle machines still cost, and a broken one costs more
	var upkeep := 0.0
	for e in (hw["equipment"] as Array):
		upkeep += maxf(float((e as Dictionary).get("upkeep_wk", 0.0)), 0.0)
	var repair := float(w.get("repair", 0.0))
	m["equip_upkeep"] = float(m.get("equip_upkeep", 0.0)) + upkeep + repair
	# only units that actually sit into next week bill: what was built and sold
	# this week never paid rent on a shelf
	var stock_end := int(hw["stock"])
	var carry := float(stock_end) * carrying_rate(state)
	m["carrying"] = float(m.get("carrying", 0.0)) + carry
	# HONEST BILLING: a repeat buyer who found the shelf empty was never handed
	# a unit, so nobody invoices them for one (owner's law #196). The catalog
	# bills a smoothed ARPU-week; this takes back the share that never shipped.
	var lost_billing := minf(unserved_billing(state), maxf(float(m.get("revenue", 0.0)), 0.0))
	m["revenue"] = float(m.get("revenue", 0.0)) - lost_billing
	w["upkeep"] = upkeep
	w["carrying"] = carry
	w["stock_end"] = stock_end
	w["sub_cost"] = sub_cost
	w["lost_billing"] = lost_billing
	w["production"] = float(built) * uc_eff

## After the record is written: the closed week's consequences. Empty shelves
## push people out (the churn the fill rate earned), then the receipts — every
## one of them naming its WHY in the same clause as its number.
static func tick_post(state: GameState, rep: Dictionary) -> void:
	if not active(state):
		return
	var w := week_block(state)
	if w.is_empty():
		return
	var fill := clampf(float(w.get("fill", 1.0)), 0.0, 1.0)
	# ×(1 + 0.35×(1 − fill)) on the week's churn: a repeat buyer who found the
	# shelf empty is a customer with a reason to leave.
	var walked := 0
	if fill < 1.0:
		walked = maxi(int(round(float(int(rep.get("churn", 0))) * (starve_churn_mult(state) - 1.0))), 0)
		walked = mini(walked, state.traction)
		if walked > 0:
			state.traction = maxi(state.traction - walked, 0)
	w["walked"] = walked
	_receipts(state, rep, w)

## THE JOURNAL, capped at four lines a week so no lane floods the page
## (docs/design/00-spine.md §11). Loudest first: the decisions the founder has
## to make outrank the bookkeeping that explains them.
static func _receipts(state: GameState, rep: Dictionary, w: Dictionary) -> void:
	var lines: Array[String] = []
	var lost := int(w.get("lost_adds", 0))
	if lost > 0:
		lines.append("STOCKOUT — %d sales lost (demand %d, shelf %d): add capacity or subcontract" % [
			lost, int(round(float(w.get("demand_units", 0.0)))), int(w.get("shelf", 0))])
		# a founder retells their first stockout for years — a BIG beat, once
		if not state.has_flag("first_stockout"):
			state.set_flag("first_stockout")
			rep["events"].append("THE FIRST STOCKOUT — %d sales walked off an empty shelf" % lost)
	var walked := int(w.get("walked", 0))
	if walked > 0:
		lines.append("−%d customers walked (fill rate %d%% — repeat buyers found empty shelves)" % [
			walked, int(round(float(w.get("fill", 1.0)) * 100.0))])
	var unbilled := float(w.get("lost_billing", 0.0))
	if unbilled >= 1.0:
		lines.append("unserved repeat buyers: −$%d (nobody is invoiced for a unit that never shipped)"
			% int(round(unbilled)))
	var built := int(w.get("built", 0))
	if built > 0:
		lines.append("built %d units at $%.2f each (utilization %d%% — idle capacity still bills upkeep)" % [
			built, float(w.get("unit_cost_eff", 0.0)),
			int(round(float(w.get("utilization", 0.0)) * 100.0))])
	var sub_units := int(w.get("sub_units", 0))
	if sub_units > 0:
		lines.append("make vs buy: subcontracted %d units −$%d (%.2f× unit cost — their margin, your sale, none of your learning)" % [
			sub_units, int(round(float(w.get("sub_cost", 0.0)))), sub_mult(state.era)])
	var down_name := String(w.get("down_name", ""))
	if down_name != "":
		lines.append("machine down: %s (repair −$%d — one week idle, then it runs again)" % [
			down_name, int(round(float(w.get("repair", 0.0))))])
	var carry := float(w.get("carrying", 0.0))
	if carry >= 1.0:
		lines.append("carrying %d units: −$%d (2%%/wk of unit cost — money parked on shelves)" % [
			int(w.get("stock_end", 0)), int(round(carry))])
	if bool(w.get("learn_step_up", false)) and int(w.get("learn_step", 0)) > 0:
		lines.append("learning curve: unit cost −%d%% (%s built — practice makes cheaper)" % [
			int(w.get("learn_step", 0)), _commas(int(hw_view(state).get("produced_total", 0)))])
	for i in mini(lines.size(), HW_LINE_CAP):
		rep["lines"].append(lines[i])

static func _commas(n: int) -> String:
	var s := str(absi(n))
	var out := ""
	while s.length() > 3:
		out = "," + s.substr(s.length() - 3) + out
		s = s.substr(0, s.length() - 3)
	return ("-" if n < 0 else "") + s + out

## DM context lines, section 13 of the DIRECTIVES block (docs/design/00-spine.md
## §5). The narrator gets to describe factory pain and is never handed a number
## it could have invented.
static func directives(state: GameState) -> Array[String]:
	var out: Array[String] = []
	if not active(state) or state.hardware.is_empty():
		return out
	var w := week_block(state)
	out.append("- Stock: %d units (made %d, sold %d last week)." % [
		int(state.hardware.get("stock", 0)), int(w.get("built", 0)), int(w.get("sold", 0))])
	if int(w.get("lost_adds", 0)) > 0:
		out.append("- STOCKOUT: demand outran stock (%d sales lost, fill %d%%)." % [
			int(w.get("lost_adds", 0)), int(round(float(w.get("fill", 1.0)) * 100.0))])
	return out

## Attention rows — the product desk. Labels are ≤40 characters because the
## garage ticker prints them verbatim, and they name the problem in the term the
## player is here to learn.
static func attention(state: GameState) -> Array:
	var rows: Array = []
	if not active(state) or state.hardware.is_empty():
		return rows
	var w := week_block(state)
	if int(w.get("lost_adds", 0)) > 0:
		rows.append({"desk": "product", "key": "stockout", "severity": 3,
			"label": "stockout — %d sales lost" % int(w.get("lost_adds", 0))})
	if overstock(state):
		rows.append({"desk": "product", "key": "overstock", "severity": 2,
			"label": "overstock — cash parked on shelves"})
	var down_name := String(w.get("down_name", ""))
	if down_name != "":
		rows.append({"desk": "product", "key": "machine_down", "severity": 2,
			"label": "machine down: %s" % down_name.left(26)})
	return rows
