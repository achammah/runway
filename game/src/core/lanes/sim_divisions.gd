class_name SimDivisions
extends RefCounted
## LANE — DIVISIONS & SITES. Spec: docs/design/DECISIONS.md (THE DIVISION
## MECHANIC, ARRANGE MODE + ARRANGE EDITS THE BINS, THE MUTATION LAW, THE PRICE
## BOOK) + docs/design/DAG2.md (L-DIVWORKS).
##
## THE LAW: divisions are NEVER generated — they are engine state, born only
## from real ops (open_site and its mirrors). The LLM names and dresses; it
## never invents a division, a capacity or a dollar. Every dollar already has
## an address (employee.site, machine.site, offer.product_id, rent per site,
## spend_book[].division); a division's book is a GROUP-BY, never an invention.
## What genuinely has no address — the founder, brand marketing, the era's own
## roof — lands on one honest SHARED/HQ row, never smeared (allocated vs
## direct cost IS the lesson).
##
## ROOF COUNTING: `sites` holds the roofs OPENED by ops; the era's own roof
## (site id "") is the company's first. "Open a second studio in Lyon" writes
## the FIRST site record and the company then has TWO roofs — so the empire
## rung fires at site_divisions() >= 2 (home roof + opened sites), which is
## DECISIONS' own Lyon story read literally.
##
## THE PRICE BOOK is the ONLY source for structural costs. state.price_book is
## guaranteed present after birth (nine flat in-band scalars); `pb()` still
## clamps every read and answers mid-band defaults so a keyless run plays.
## open_site_pack is a FLAT pack; the era scaling on top is engine math here.
##
## DURABLE MARKERS: a reassigned person ramps one week and a moved machine is
## one week offline. Employee/machine records are TYPED in the C# twin, so the
## markers live in state.flags as "works_ramp:<name>:<wk>" / "works_off:<name>:
## <wk>" — a List of strings in both engines, saved whole, pruned by tick_pre.
##
## The spine calls (docs/design/HOOKS.md): tick_pre §6c (ramps settle before
## the market splits demand), tick_money (owns ONLY m["site_rent"]), tick_post
## (per-site learning counts + the bleeding flag read the finished week),
## directives(), attention().
##
## SALTS: SALT_DIV_SITES (130) — quotes + the close-site customer split, keyed
## (seed, week) so a preview equals its booking all week. SALT_DIV_NAMES (131)
## — the keyless site-name pool. Draw order per call site is FIXED.
##
## TWIN LAW: unity/Assets/Scripts/Core/Lanes/SimDivisions.cs carries the same
## logic in the same order.

# ── THE PRICE BOOK BANDS (lo, hi, mid-band default = L-GEN's own bands) ──────
const PB_BANDS := {
	"open_site_pack": [6_000.0, 40_000.0, 18_000.0],
	"relocation_fee": [100.0, 1_500.0, 400.0],
	"machine_shipping": [150.0, 4_000.0, 900.0],
	"lease_break_weeks": [4.0, 16.0, 8.0],
	"contract_notice_wks": [2.0, 12.0, 4.0],
	"refinance_break_fee": [100.0, 2_000.0, 350.0],
	"freelance_rate": [15.0, 300.0, 60.0],
	"subcontract_rate": [10.0, 250.0, 45.0],
	"account_fire_penalty": [200.0, 5_000.0, 1_200.0],
}
## The pack is quoted "by era" (DECISIONS): flat pack × the era's own scale.
const ERA_PACK_MULT := {"garage": 0.5, "coworking": 0.7, "office": 1.0,
	"floor": 2.2, "hq": 5.0}
## A second roof rents at a fraction of the era's own roof, jittered at quote.
const SITE_RENT_LO := 0.45
const SITE_RENT_HI := 0.85
## Local wage regions the world quotes from (a site keeps its mult for life).
const WAGE_TABLE := [0.85, 0.92, 1.0, 1.08, 1.15]
## A new roof ramps its local demand on its own curve: opens at 0.15 weight and
## climbs ~10%/wk of the gap toward 1.0 (≈0.76 by week 12).
const SITE_OPEN_WEIGHT := 0.15
const SITE_RAMP_K := 0.10
## Closing: this fraction of the roof's customers transfers (fragile), drawn
## once on SALT_DIV_SITES; the rest are lost with the roof.
const CLOSE_TRANSFER_LO := 0.35
const CLOSE_TRANSFER_HI := 0.50
## A site bleeding (negative margin) this many weeks past its ramp grace wears
## the alarm ("fix or close").
const RED_WEEKS := 3
const RAMP_GRACE_WK := 8
## Re-leasing prices one week of the NEW rent and costs a moving week of demand.
const RELEASE_DIP := 0.85
## The keyless site-name pool (the DM proposes real names on keyed runs).
const NAME_POOL := ["Lyon", "Harbor East", "Northside", "Old Mill", "Riverside",
	"Midtown", "The Annex", "Southgate", "Lakeview", "The Depot"]

# ═════════════════════════════ THE PRICE BOOK ════════════════════════════════

## THE ONE READ. Clamped to the band whatever wrote it; mid-band default when
## the key is missing — keyless runs must play.
static func pb(state: GameState, key: String) -> float:
	var band: Array = PB_BANDS.get(key, [])
	if band.is_empty():
		return 0.0
	var raw = state.price_book.get(key, band[2])
	var v := float(band[2])
	if raw is float or raw is int:
		v = float(raw)
	return clampf(v, float(band[0]), float(band[1]))

## What opening a roof costs THIS company at THIS stage: the flat generated
## pack × the era scale, rounded to $50 so the receipt reads like a quote.
static func open_pack_cost(state: GameState) -> int:
	var pack := pb(state, "open_site_pack") * float(ERA_PACK_MULT.get(state.era, 1.0))
	return int(round(pack / 50.0)) * 50

# ═════════════════════════════ ROOFS & RUNGS ═════════════════════════════════

static func site_by_id(state: GameState, id: String) -> Dictionary:
	for s in state.sites:
		if String((s as Dictionary).get("id", "")) == id:
			return s
	return {}

## The era's own roof never empties: the FOUNDER works under it (their hands
## are the home roof's floor in the works), so the home division always
## exists. Kept as a function because the books and the bins both ask it.
static func home_occupied(_state: GameState) -> bool:
	return true

## Divisions on the site axis: the home roof (while occupied) + every opened
## roof. Two roofs = an empire of two studios (the Lyon story).
static func site_divisions(state: GameState) -> int:
	return state.sites.size() + (1 if home_occupied(state) else 0)

## Distinct products across the catalog ("" = the flagship).
static func products_count(state: GameState) -> int:
	if state.offers.is_empty():
		return 0
	var seen := {}
	for o in state.offers:
		seen[String((o as Dictionary).get("product_id", ""))] = true
	return seen.size()

## THE RUNG RULE — deterministic counts, no judgment, no model call:
## site divisions ≥ 2 → 3 (empire, sliced by site) · products ≥ 2 → 3 (empire,
## by product) · offers ≥ 3 → 2 (the house) · else 1 (the boutique).
static func rung(state: GameState) -> int:
	if site_divisions(state) >= 2 or products_count(state) >= 2:
		return 3
	if state.offers.size() >= 3:
		return 2
	return 1

## "Sliced by ▾" lists ONLY axes with ≥2 populated divisions in state.
static func slice_axes(state: GameState) -> Array:
	var out: Array = []
	if site_divisions(state) >= 2:
		out.append("site")
	if products_count(state) >= 2:
		out.append("product")
	if state.offers.size() >= 2:
		out.append("offer")
	return out

static func default_slice(state: GameState) -> String:
	if site_divisions(state) >= 2:
		return "site"
	if products_count(state) >= 2:
		return "product"
	return "offer"

## The per-site learning curve — the engine's own Janoschek shape on the
## site's OWN count, which is what makes Lyon $27 and Geneva $36 mechanical.
static func site_lc(count: int) -> float:
	if count <= 1:
		return 1.0
	return maxf(1.0 - 0.115 * (log(float(count)) / log(10.0)), 0.65)

# ═════════════════════════ QUOTES (pure, week-stable) ════════════════════════

## The open-a-roof quote. Drawn on SALT_DIV_SITES/(seed, week) so the ghost
## bin's preview and the signed booking carry the SAME numbers all week.
## Draw order FIXED: ① name index (SALT_DIV_NAMES) ② rent factor ③ wage region.
static func quote_site(state: GameState) -> Dictionary:
	var rn := SimEngine.rng_for(state, SimEngine.SALT_DIV_NAMES)
	var name := String(NAME_POOL[rn.randi() % NAME_POOL.size()])
	var used := {}
	for s in state.sites:
		used[String((s as Dictionary).get("name", ""))] = true
	var tries := 0
	while used.has(name) and tries < NAME_POOL.size():
		name = String(NAME_POOL[rn.randi() % NAME_POOL.size()])
		tries += 1
	var r := SimEngine.rng_for(state, SimEngine.SALT_DIV_SITES)
	var rent := int(round(float(GameState.ERA_RENT.get(state.era, 150)) \
		* r.randf_range(SITE_RENT_LO, SITE_RENT_HI) / 10.0)) * 10
	var wage := float(WAGE_TABLE[r.randi() % WAGE_TABLE.size()])
	return {"pack": open_pack_cost(state), "rent_wk": maxi(rent, 40),
		"wage_mult": wage, "name": name}

## The pack decomposed for the receipt — derived, so the lines always sum.
static func pack_lines(pack: int) -> Array:
	var deposit := int(round(float(pack) * 0.25))
	var capex := int(round(float(pack) * 0.40))
	return [
		{"label": "lease deposit", "amount": deposit},
		{"label": "fit-out & kit", "amount": capex},
		{"label": "the hire pack", "amount": pack - deposit - capex},
	]

# ═══════════════════════════ THE SITE EXECUTORS ══════════════════════════════

## OPEN A ROOF — one op, two doors (the written move and the arrange ghost
## bin). Engine is the bouncer: era cap and cash both answer before a record
## is born. The DM's only role is the name.
static func open_site(state: GameState, name := "") -> Dictionary:
	var q := quote_site(state)
	var pack := int(q.get("pack", 0))
	if pack > SimEngine.era_spend_cap(state.era):
		return {"ok": false, "why": "past what a %s can sign for" % state.era}
	if state.cash < pack:
		return {"ok": false, "why": "$%d short of the pack" % (pack - state.cash)}
	var n := 1
	for s in state.sites:
		var sid := String((s as Dictionary).get("id", ""))
		if sid.begins_with("site_"):
			n = maxi(n, int(sid.trim_prefix("site_")) + 1)
	var site := {"id": "site_%d" % n,
		"name": (name.strip_edges().substr(0, 24) if name.strip_edges() != "" else String(q.get("name", "the new roof"))),
		"rent_wk": int(q.get("rent_wk", 0)), "wage_mult": float(q.get("wage_mult", 1.0)),
		"learning_count": 0, "demand_weight": SITE_OPEN_WEIGHT, "opened_wk": state.week}
	state.sites.append(site)
	state.cash -= pack
	state.log_action("OPENED %s: −$%d (deposit + fit-out + hires), rent $%d/wk, ramp from wk %d" % [
		String(site["name"]), pack, int(site["rent_wk"]), state.week])
	return {"ok": true, "why": "", "site": site, "pack": pack}

## Rename is INK — free, dressing only.
static func rename_site(state: GameState, id: String, name: String) -> bool:
	var s := site_by_id(state, id)
	if s.is_empty() or name.strip_edges() == "":
		return false
	s["name"] = name.strip_edges().substr(0, 24)
	return true

## RE-LEASE — brick: dir +1 takes a bigger roof (+25% rent), −1 a smaller
## (−20%). Fee = one week of the NEW rent, and the moving week dips the roof's
## own demand. Quoted first (pure), booked on confirm.
static func relase_quote(state: GameState, id: String, dir: int) -> Dictionary:
	var s := site_by_id(state, id)
	if s.is_empty():
		return {}
	var rent := int(s.get("rent_wk", 0))
	var new_rent := int(round(float(rent) * (1.25 if dir >= 0 else 0.8) / 10.0)) * 10
	new_rent = clampi(new_rent, 40, int(GameState.ERA_RENT.get(state.era, 150)) * 2)
	return {"fee": new_rent, "new_rent": new_rent, "old_rent": rent}

static func edit_site(state: GameState, id: String, dir: int) -> Dictionary:
	var q := relase_quote(state, id, dir)
	if q.is_empty():
		return {"ok": false, "why": "no such roof"}
	if state.cash < int(q.get("fee", 0)):
		return {"ok": false, "why": "$%d short of the moving week" % (int(q.get("fee", 0)) - state.cash)}
	var s := site_by_id(state, id)
	state.cash -= int(q.get("fee", 0))
	s["rent_wk"] = int(q.get("new_rent", 0))
	s["demand_weight"] = maxf(float(s.get("demand_weight", 1.0)) * RELEASE_DIP, 0.05)
	state.log_action("RE-LEASED %s: rent $%d→$%d/wk, −$%d and a moving week" % [
		String(s.get("name", id)), int(q.get("old_rent", 0)), int(q.get("new_rent", 0)),
		int(q.get("fee", 0))])
	return {"ok": true, "why": "", "fee": int(q.get("fee", 0)), "new_rent": int(q.get("new_rent", 0))}

# ── the teardown wizard's arithmetic (pure) then its booking ─────────────────

## Everything closing this roof costs and frees, one decision per element.
## decisions: {"e:<employee index>": "go"|"move:<site id>",
##             "m:<equipment index>": "sell"|"move:<site id>"}
## Returns priced lines + the derived verdict: net cash now, $/wk freed, the
## revenue that dies, and the payback line ("closing pays back in ≈N weeks").
static func close_quote(state: GameState, id: String, decisions: Dictionary) -> Dictionary:
	var s := site_by_id(state, id)
	if s.is_empty():
		return {}
	var lines: Array = []
	var cash_now := 0
	var freed_wk := 0
	var moves := 0
	var gos := 0
	var reloc := int(round(pb(state, "relocation_fee")))
	for i in state.employees.size():
		var e: Dictionary = state.employees[i]
		if String(e.get("site", "")) != id:
			continue
		var d := String(decisions.get("e:%d" % i, "go"))
		if d.begins_with("move:"):
			moves += 1
			cash_now -= reloc
		else:
			gos += 1
			var sev := SimLabor.severance_for(state, e)
			cash_now -= sev
			freed_wk += int(e.get("salary", 0))
	if moves > 0:
		lines.append({"label": "%d move (relocation + 1-wk ramp)" % moves, "amount": -reloc * moves})
	if gos > 0:
		var sev_total := 0
		for i2 in state.employees.size():
			var e2: Dictionary = state.employees[i2]
			if String(e2.get("site", "")) == id and not String(decisions.get("e:%d" % i2, "go")).begins_with("move:"):
				sev_total += SimLabor.severance_for(state, e2)
		lines.append({"label": "%d let go — severance is always owed" % gos, "amount": -sev_total})
	var ship := int(round(pb(state, "machine_shipping")))
	var eq: Array = state.hardware.get("equipment", [])
	for j in eq.size():
		var m: Dictionary = eq[j]
		if String(m.get("site", "")) != id:
			continue
		var dm := String(decisions.get("m:%d" % j, "sell"))
		if dm.begins_with("move:"):
			cash_now -= ship
			lines.append({"label": "%s moves (a week offline)" % String(m.get("name", "a machine")), "amount": -ship})
		else:
			var back := SimFactory.resale_value(String(m.get("id", "")))
			cash_now += back
			lines.append({"label": "%s sold at half" % String(m.get("name", "a machine")), "amount": back})
	var rent := int(s.get("rent_wk", 0))
	var brk := int(round(pb(state, "lease_break_weeks"))) * rent
	cash_now -= brk
	freed_wk += rent
	lines.append({"label": "the lease, broken mid-term (%d wks of rent)" % int(round(pb(state, "lease_break_weeks"))), "amount": -brk})
	# THE CUSTOMERS ARE DECIDED FOR YOU: a salted, week-stable split — some
	# transfer with churn risk, the rest die with the roof.
	var share := demand_share(state, id)
	var cust := int(round(float(state.traction) * share))
	# ONE draw, week-stable: each helper builds its own rng off the salt key, so
	# the preview and the booking read the same split all week.
	var r := SimEngine.rng_for(state, SimEngine.SALT_DIV_SITES)
	var transfer_frac := clampf(CLOSE_TRANSFER_LO + (CLOSE_TRANSFER_HI - CLOSE_TRANSFER_LO) * r.randf(), 0.0, 1.0)
	var kept := int(floor(float(cust) * transfer_frac))
	var lost := cust - kept
	var rev_per_cust := SimEngine.offers_arpu(state)
	if rev_per_cust < 0.0:
		rev_per_cust = float(state.theta.get("arpu_wk", 4.0)) * state.price_mult
	var lost_rev_wk := int(round(float(lost) * rev_per_cust))
	var site_margin_wk := 0
	for row in works_book(state, "site"):
		if String((row as Dictionary).get("id", "?")) == id:
			site_margin_wk = int((row as Dictionary).get("net_wk", 0))
	var net_freed := freed_wk - lost_rev_wk
	var payback := -1
	if cash_now < 0 and net_freed > 0:
		payback = int(ceil(float(-cash_now) / float(net_freed)))
	return {"lines": lines, "net_now": cash_now, "freed_wk": freed_wk,
		"lost_rev_wk": lost_rev_wk, "kept": kept, "lost": lost,
		"payback_wk": payback, "site_margin_wk": site_margin_wk}

## CLOSE THE ROOF — the composite receipt booked whole. Obligations survive
## removal: severance always owed, the lease penalty bills, the lost customers
## leave now and the fragile transfers carry a churn cloud.
static func close_site(state: GameState, id: String, decisions: Dictionary) -> Dictionary:
	var s := site_by_id(state, id)
	if s.is_empty():
		return {"ok": false, "why": "no such roof"}
	var q := close_quote(state, id, decisions)
	# people: fire from the highest index down so the decision keys hold
	for i in range(state.employees.size() - 1, -1, -1):
		var e: Dictionary = state.employees[i]
		if String(e.get("site", "")) != id:
			continue
		var d := String(decisions.get("e:%d" % i, "go"))
		if d.begins_with("move:"):
			var dest := d.trim_prefix("move:")
			state.cash -= int(round(pb(state, "relocation_fee")))
			e["site"] = dest
			_mark(state, "works_ramp", String(e.get("name", "")), state.week + 1)
		else:
			SimLabor.fire_employee(state, i)   # books severance_due; ALWAYS owed
	var eq: Array = state.hardware.get("equipment", [])
	for j in range(eq.size() - 1, -1, -1):
		var m: Dictionary = eq[j]
		if String(m.get("site", "")) != id:
			continue
		var dm := String(decisions.get("m:%d" % j, "sell"))
		if dm.begins_with("move:"):
			state.cash -= int(round(pb(state, "machine_shipping")))
			m["site"] = dm.trim_prefix("move:")
			_mark(state, "works_off", String(m.get("name", "")), state.week + 1)
		else:
			SimFactory.sell_equipment(state, j)
	state.cash -= int(round(pb(state, "lease_break_weeks"))) * int(s.get("rent_wk", 0))
	var lost := int(q.get("lost", 0))
	if lost > 0:
		state.traction = maxi(state.traction - lost, 0)
	if int(q.get("kept", 0)) > 0:
		SimEngine.add_status(state, "churn_spiral", 2)   # fragile transfers
	_unmark(state, "works_red", id)   # the dead roof takes its counter with it
	state.sites.erase(s)
	state.log_action("CLOSED %s: %d transferred (fragile), %d lost with the roof — payback ≈%s wks" % [
		String(s.get("name", id)), int(q.get("kept", 0)), lost,
		str(int(q.get("payback_wk", -1))) if int(q.get("payback_wk", -1)) >= 0 else "—"])
	return {"ok": true, "why": "", "quote": q}

# ═════════════════════════ THE ARRANGE OPS (chips) ═══════════════════════════

## The move receipts are computed engine-side so the UI shows truth.
static func reassign_quote(state: GameState, emp_i: int, to_site: String) -> Dictionary:
	if emp_i < 0 or emp_i >= state.employees.size():
		return {}
	var e: Dictionary = state.employees[emp_i]
	return {"fee": int(round(pb(state, "relocation_fee"))), "name": String(e.get("name", "?")),
		"from": String(e.get("site", "")), "to": to_site, "ramp_wk": 1}

## MOVE A PERSON — brick: the relocation fee now and a 1-week ramp at the new
## roof (the works counts them at zero slots meanwhile).
static func reassign_employee(state: GameState, emp_i: int, to_site: String) -> Dictionary:
	var q := reassign_quote(state, emp_i, to_site)
	if q.is_empty():
		return {"ok": false, "why": "nobody there"}
	if to_site != "" and site_by_id(state, to_site).is_empty():
		return {"ok": false, "why": "no such roof"}
	if String(q.get("from", "")) == to_site:
		return {"ok": false, "why": "already under that roof"}
	if state.cash < int(q.get("fee", 0)):
		return {"ok": false, "why": "$%d short of the relocation" % (int(q.get("fee", 0)) - state.cash)}
	var e: Dictionary = state.employees[emp_i]
	state.cash -= int(q.get("fee", 0))
	e["site"] = to_site
	_mark(state, "works_ramp", String(e.get("name", "")), state.week + 1)
	state.log_action("MOVED %s to %s: −$%d and a ramp week" % [String(e.get("name", "?")),
		_roof_name(state, to_site), int(q.get("fee", 0))])
	return {"ok": true, "why": "", "fee": int(q.get("fee", 0))}

static func move_quote(state: GameState, eq_i: int, to_site: String) -> Dictionary:
	var eq: Array = state.hardware.get("equipment", [])
	if eq_i < 0 or eq_i >= eq.size():
		return {}
	var m: Dictionary = eq[eq_i]
	return {"fee": int(round(pb(state, "machine_shipping"))), "name": String(m.get("name", "?")),
		"from": String(m.get("site", "")), "to": to_site, "off_wk": 1}

## MOVE A MACHINE — brick: shipping now and a week offline.
static func move_machine(state: GameState, eq_i: int, to_site: String) -> Dictionary:
	var q := move_quote(state, eq_i, to_site)
	if q.is_empty():
		return {"ok": false, "why": "no machine there"}
	if to_site != "" and site_by_id(state, to_site).is_empty():
		return {"ok": false, "why": "no such roof"}
	if String(q.get("from", "")) == to_site:
		return {"ok": false, "why": "already under that roof"}
	if state.cash < int(q.get("fee", 0)):
		return {"ok": false, "why": "$%d short of the shipping" % (int(q.get("fee", 0)) - state.cash)}
	var eq: Array = state.hardware.get("equipment", [])
	var m: Dictionary = eq[eq_i]
	state.cash -= int(q.get("fee", 0))
	m["site"] = to_site
	_mark(state, "works_off", String(m.get("name", "")), state.week + 1)
	state.log_action("SHIPPED %s to %s: −$%d and a week offline" % [String(m.get("name", "?")),
		_roof_name(state, to_site), int(q.get("fee", 0))])
	return {"ok": true, "why": "", "fee": int(q.get("fee", 0))}

## PAPER IS PAPER — tags are free.
static func tag_offer(state: GameState, offer_i: int, product_id: String) -> bool:
	if offer_i < 0 or offer_i >= state.offers.size():
		return false
	(state.offers[offer_i] as Dictionary)["product_id"] = product_id.substr(0, 24)
	return true

static func tag_spend_line(state: GameState, line_i: int, division: String) -> bool:
	if line_i < 0 or line_i >= state.spend_book.size():
		return false
	(state.spend_book[line_i] as Dictionary)["division"] = division.substr(0, 24)
	return true

## STOP A SPEND LINE — instantly, unless the book marked it "contract": the
## notice period bills through as a standing commitment the ledger prints.
static func stop_spend_line(state: GameState, line_i: int) -> Dictionary:
	if line_i < 0 or line_i >= state.spend_book.size():
		return {"ok": false, "why": "no such line"}
	var l: Dictionary = state.spend_book[line_i]
	var notice := int(l.get("contract_notice", 0))
	if notice > 0:
		notice = clampi(notice, 1, int(round(pb(state, "contract_notice_wks")) * 3.0))
		state.commitments.append({"name": "notice: %s" % String(l.get("name", "a line")),
			"cash_wk": -int(l.get("amt", 0)), "weeks_left": notice})
	state.spend_book.remove_at(line_i)
	state.log_action("STOPPED %s%s" % [String(l.get("name", "a spend line")),
		" — contract: %d wks of notice bill through" % notice if notice > 0 else ""])
	return {"ok": true, "why": "", "notice_wks": notice, "amt": int(l.get("amt", 0))}

# ═════════════════════ THE GROUP-BY BOOKS (pure sums) ════════════════════════

## A roof's share of this week's demand: weights over home (1.0) + sites.
static func demand_share(state: GameState, id: String) -> float:
	var total := 1.0 if home_occupied(state) else 0.0
	for s in state.sites:
		total += maxf(float((s as Dictionary).get("demand_weight", 1.0)), 0.0)
	if total <= 0.0:
		return 0.0
	if id == "":
		return (1.0 / total) if home_occupied(state) else 0.0
	var s2 := site_by_id(state, id)
	return maxf(float(s2.get("demand_weight", 0.0)), 0.0) / total if not s2.is_empty() else 0.0

static func _roof_name(state: GameState, id: String) -> String:
	if id == "":
		return "the home roof"
	var s := site_by_id(state, id)
	return String(s.get("name", id)) if not s.is_empty() else id

## THE SLICER. Division rows are GROUP-BYs over records the engine already
## keeps; roll-ups are sums, nothing invented. Rows: {id, name, kind, heads,
## payroll_wk, rent_wk, machines, slots, wanted, served, vol, unit_cost,
## margin_each, net_wk, util, wage_mult, lc, sev, note}. The SHARED/HQ row
## (kind "shared") closes every book: founder ramen + brand marketing + the
## era's own roof + untagged spend lines — never smeared.
static func works_book(state: GameState, axis: String) -> Array:
	var rows: Array = []
	match axis:
		"site":
			var ids: Array = []
			if home_occupied(state):
				ids.append("")
			for s in state.sites:
				ids.append(String((s as Dictionary).get("id", "")))
			for id in ids:
				rows.append(_site_row(state, String(id)))
		"product":
			var seen := {}
			for o in state.offers:
				var pid := String((o as Dictionary).get("product_id", ""))
				if not seen.has(pid):
					seen[pid] = true
					rows.append(_product_row(state, pid))
		_:
			for i in state.offers.size():
				rows.append(_offer_row(state, i))
	rows.append(_shared_row(state))
	return rows

static func _site_row(state: GameState, id: String) -> Dictionary:
	var s := site_by_id(state, id)
	var heads := 0
	var payroll := 0
	for e in state.employees:
		if String((e as Dictionary).get("site", "")) == id:
			heads += 1
			payroll += int((e as Dictionary).get("salary", 0))
	var machines := 0
	for m in (state.hardware.get("equipment", []) as Array):
		if String((m as Dictionary).get("site", "")) == id:
			machines += 1
	var spend := 0
	for l in state.spend_book:
		if String((l as Dictionary).get("division", "")) == id and id != "":
			spend += int((l as Dictionary).get("amt", 0))
	var w := SimWorks.week_view(state)
	var share := demand_share(state, id)
	var wanted := float(w.get("demand_units", 0.0)) * share
	var slots := SimWorks.capacity_of_site(state, id)
	var served := minf(wanted, slots) if String(w.get("type", "")) == "Service" else \
		float(w.get("served_units", 0.0)) * share
	var vol := maxf(served, 0.0)
	var wage := float(s.get("wage_mult", 1.0)) if not s.is_empty() else 1.0
	var lc := site_lc(int(s.get("learning_count", 0))) if not s.is_empty() else SimEngine.learning_curve(state)
	var rent := int(s.get("rent_wk", 0)) if not s.is_empty() else 0
	var base_var := SimWorks.base_unit_cost(state)
	var unit_cost := base_var * wage * lc + (float(rent) / maxf(vol, 1.0))
	var rev_u := float(w.get("rev_per_unit", 0.0))
	var margin := rev_u - unit_cost
	var net_wk := int(round(margin * vol)) - payroll - spend
	var util := clampf(wanted / maxf(slots, 0.001), 0.0, 1.0) if slots > 0.0 else 0.0
	var sev := 0
	if not s.is_empty():
		# the bleeding counter rides the flags list ("works_red:<id>:<n>") —
		# the C# Site record is typed, so the dict may not grow a key
		if maxi(marked_until(state, "works_red", id), 0) >= RED_WEEKS:
			sev = 3
		elif margin < 0.0 and state.week - int(s.get("opened_wk", 0)) > RAMP_GRACE_WK:
			sev = 2
	var note := ""
	if not s.is_empty() and state.week - int(s.get("opened_wk", 0)) <= RAMP_GRACE_WK:
		note = "young — still ramping"
	elif sev >= 3:
		note = "fix or close"
	elif slots > 0.0 and wanted > slots:
		note = "full — overflow → relief"
	return {"id": id, "name": _roof_name(state, id), "kind": "site", "heads": heads,
		"payroll_wk": payroll, "rent_wk": rent, "machines": machines, "slots": slots,
		"wanted": wanted, "served": served, "vol": vol, "unit_cost": unit_cost,
		"margin_each": margin, "net_wk": net_wk, "util": util, "wage_mult": wage,
		"lc": lc, "sev": sev, "note": note}

static func _product_row(state: GameState, pid: String) -> Dictionary:
	var w := SimWorks.week_view(state)
	var wanted := 0.0
	var cost_u := 0.0
	var rev_u := 0.0
	var weight := 0.0
	var names: Array = []
	var lc := SimEngine.learning_curve(state)
	var fm := SimEngine.street_fair_mult(state)
	for o in state.offers:
		var od: Dictionary = o
		if String(od.get("product_id", "")) != pid:
			continue
		names.append(String(od.get("name", "?")))
		var u := float(state.traction) * float(od.get("weight", 1.0)) \
			* SimEngine.offer_cadence(String(od.get("unit", "")))
		wanted += u
		weight += float(od.get("weight", 1.0))
		cost_u += u * (float(od.get("unit_cost", 0.0)) * lc + SimWorks.feature_cost_add(state, pid))
		rev_u += u * SimEngine.offer_billed_price(od, fm)
	var vol := maxf(wanted, 0.0)
	var unit_cost := cost_u / maxf(vol, 0.001)
	var margin := rev_u / maxf(vol, 0.001) - unit_cost
	var served := minf(vol, float(w.get("served_units", vol)))
	var nm := "the flagship" if pid == "" else pid
	return {"id": pid, "name": nm, "kind": "product", "heads": 0, "payroll_wk": 0,
		"rent_wk": 0, "machines": 0, "slots": 0.0, "wanted": wanted, "served": served,
		"vol": vol, "unit_cost": unit_cost, "margin_each": margin,
		"net_wk": int(round(margin * vol)), "util": 0.0, "wage_mult": 1.0, "lc": lc,
		"sev": 0, "note": ", ".join(PackedStringArray(names)).substr(0, 40)}

static func _offer_row(state: GameState, i: int) -> Dictionary:
	var od: Dictionary = state.offers[i]
	var lc := SimEngine.learning_curve(state)
	var fm := SimEngine.street_fair_mult(state)
	var pid := String(od.get("product_id", ""))
	var wanted := float(state.traction) * float(od.get("weight", 1.0)) \
		* SimEngine.offer_cadence(String(od.get("unit", "")))
	var unit_cost := float(od.get("unit_cost", 0.0)) * lc + SimWorks.feature_cost_add(state, pid)
	var price := SimEngine.offer_billed_price(od, fm)
	var w := SimWorks.week_view(state)
	var fill := 1.0
	if float(w.get("demand_units", 0.0)) > 0.0:
		fill = clampf(float(w.get("served_units", 0.0)) / float(w.get("demand_units", 1.0)), 0.0, 1.0)
	return {"id": "offer_%d" % i, "name": String(od.get("name", "?")), "kind": "offer",
		"heads": 0, "payroll_wk": 0, "rent_wk": 0, "machines": 0, "slots": 0.0,
		"wanted": wanted, "served": wanted * fill, "vol": wanted * fill,
		"unit_cost": unit_cost, "margin_each": price - unit_cost,
		"net_wk": int(round((price - unit_cost) * wanted * fill)), "util": fill,
		"wage_mult": 1.0, "lc": lc, "sev": 0, "note": ""}

static func _shared_row(state: GameState) -> Dictionary:
	var spend := 0
	for l in state.spend_book:
		if String((l as Dictionary).get("division", "")) == "":
			spend += int((l as Dictionary).get("amt", 0))
	var brand := int(state.budgets.get("ads", 0)) + int(state.budgets.get("content", 0))
	var hq_rent := int(GameState.ERA_RENT.get(state.era, 150))
	var founder := GameState.RAMEN_PER_WEEK
	return {"id": "shared", "name": "SHARED / HQ", "kind": "shared", "heads": 0,
		"payroll_wk": founder, "rent_wk": hq_rent, "machines": 0, "slots": 0.0,
		"wanted": 0.0, "served": 0.0, "vol": 0.0, "unit_cost": 0.0, "margin_each": 0.0,
		"net_wk": -(founder + hq_rent + brand + spend), "util": 0.0, "wage_mult": 1.0,
		"lc": 1.0, "sev": 0,
		"note": "the founder, brand marketing, the era's roof — never smeared"}

# ═══════════════════ THE DM OP DOORS (garage executor arms) ══════════════════
## One wrapper per DM op, EXACTLY the op's name after `op_`. Each takes the raw
## effect dict, unpacks and clamps through the executors above, and returns the
## receipt line ("" = nothing happened; the world says why in the line itself).

static func op_open_site(state: GameState, d: Dictionary) -> String:
	var res := open_site(state, String(d.get("cat", d.get("name", ""))))
	if not bool(res.get("ok", false)):
		return "no new roof: %s" % String(res.get("why", ""))
	var site: Dictionary = res.get("site", {})
	return "OPENED %s: −$%d, rent $%d/wk — its demand ramps on its own curve" % [
		String(site.get("name", "?")), int(res.get("pack", 0)), int(site.get("rent_wk", 0))]

static func op_close_site(state: GameState, d: Dictionary) -> String:
	var id := _site_id_from(state, String(d.get("cat", "")))
	if id == "":
		return "no roof called '%s' — nothing closed" % String(d.get("cat", ""))
	var res := close_site(state, id, d.get("decisions", {}) if d.get("decisions") is Dictionary else {})
	if not bool(res.get("ok", false)):
		return String(res.get("why", ""))
	var q: Dictionary = res.get("quote", {})
	return "CLOSED the roof: %d customers transferred (fragile), %d lost, $%d/wk freed" % [
		int(q.get("kept", 0)), int(q.get("lost", 0)), int(q.get("freed_wk", 0))]

static func op_reassign_employee(state: GameState, d: Dictionary) -> String:
	var nm := String(d.get("cat", "")).strip_edges().to_lower()
	var to := _site_id_from(state, String(d.get("v", d.get("site", ""))))
	for i in state.employees.size():
		if String((state.employees[i] as Dictionary).get("name", "")).to_lower().contains(nm) and nm != "":
			var res := reassign_employee(state, i, to)
			if bool(res.get("ok", false)):
				return "%s → %s: −$%d and a ramp week" % [
					String((state.employees[i] as Dictionary).get("name", "?")),
					_roof_name(state, to), int(res.get("fee", 0))]
			return String(res.get("why", ""))
	return "nobody called '%s' on the payroll" % String(d.get("cat", ""))

static func op_move_machine(state: GameState, d: Dictionary) -> String:
	var nm := String(d.get("cat", "")).strip_edges().to_lower()
	var to := _site_id_from(state, String(d.get("v", d.get("site", ""))))
	var eq: Array = state.hardware.get("equipment", [])
	for j in eq.size():
		if String((eq[j] as Dictionary).get("name", "")).to_lower().contains(nm) and nm != "":
			var res := move_machine(state, j, to)
			if bool(res.get("ok", false)):
				return "%s shipped to %s: −$%d and a week offline" % [
					String((eq[j] as Dictionary).get("name", "?")), _roof_name(state, to),
					int(res.get("fee", 0))]
			return String(res.get("why", ""))
	return "no machine called '%s' on the floor" % String(d.get("cat", ""))

static func op_tag_offer(state: GameState, d: Dictionary) -> String:
	var nm := String(d.get("cat", "")).strip_edges().to_lower()
	for i in state.offers.size():
		if String((state.offers[i] as Dictionary).get("name", "")).to_lower().contains(nm) and nm != "":
			tag_offer(state, i, String(d.get("v", "")))
			return "%s filed under %s — paper is paper, free" % [
				String((state.offers[i] as Dictionary).get("name", "?")),
				String(d.get("v", "the flagship")) if String(d.get("v", "")) != "" else "the flagship"]
	return "no offer called '%s' on the shelf" % String(d.get("cat", ""))

static func op_tag_spend_line(state: GameState, d: Dictionary) -> String:
	var nm := String(d.get("cat", "")).strip_edges().to_lower()
	for i in state.spend_book.size():
		if String((state.spend_book[i] as Dictionary).get("name", "")).to_lower().contains(nm) and nm != "":
			var dv := String(d.get("v", ""))
			tag_spend_line(state, i, "" if dv == "shared" else _site_id_from(state, dv))
			return "%s filed under %s — ink, free" % [
				String((state.spend_book[i] as Dictionary).get("name", "?")),
				"SHARED/HQ" if (dv == "shared" or dv == "") else dv]
	return "no spend line called '%s' in the book" % String(d.get("cat", ""))

## A roof by id OR by (partial) name — the DM speaks names.
static func _site_id_from(state: GameState, word: String) -> String:
	var w := word.strip_edges().to_lower()
	if w == "" or w == "home" or w == "hq":
		return ""
	for s in state.sites:
		var sd: Dictionary = s
		if String(sd.get("id", "")).to_lower() == w \
				or String(sd.get("name", "")).to_lower().contains(w):
			return String(sd.get("id", ""))
	return ""

# ═══════════════════════════ THE SPINE'S ENTRY POINTS ════════════════════════

## Tick §6c — roofs settle before the market splits demand: ramps climb, and
## the week's expired ramp/offline markers fall off the flags list.
static func tick_pre(state: GameState, _rep: Dictionary) -> void:
	for s in state.sites:
		var sd: Dictionary = s
		var w := float(sd.get("demand_weight", 1.0))
		if w < 1.0:
			sd["demand_weight"] = minf(w + (1.0 - w) * SITE_RAMP_K, 1.0)
	_prune_marks(state)

## The money section — this lane owns ONLY `site_rent`: every opened roof's
## rent, beside the era's own roof, receipted once.
static func tick_money(state: GameState, rep: Dictionary, m: Dictionary) -> void:
	var rent := 0
	for s in state.sites:
		rent += int((s as Dictionary).get("rent_wk", 0))
	if rent > 0:
		m["site_rent"] = float(m.get("site_rent", 0.0)) + float(rent)
		rep["lines"].append("site rents: −$%d across %d roof%s (beside the era's own)" % [
			rent, state.sites.size(), "s" if state.sites.size() > 1 else ""])

## After the record: per-site learning counts grow with the roofs' own served
## volume, and the bleeding flag reads the finished week.
static func tick_post(state: GameState, _rep: Dictionary) -> void:
	if state.sites.is_empty():
		return
	var w := SimWorks.week_view(state)
	var served := float(w.get("served_units", 0.0))
	for s in state.sites:
		var sd: Dictionary = s
		var id := String(sd.get("id", ""))
		var share := demand_share(state, id)
		sd["learning_count"] = int(sd.get("learning_count", 0)) + int(round(served * share))
		var row := _site_row(state, id)
		if float(row.get("margin_each", 0.0)) < 0.0 \
				and state.week - int(sd.get("opened_wk", 0)) > RAMP_GRACE_WK:
			_mark(state, "works_red", id, maxi(marked_until(state, "works_red", id), 0) + 1)
		else:
			_unmark(state, "works_red", id)

## DM context: the roofs in one line — facts, never prices it may invent.
static func directives(state: GameState) -> Array[String]:
	var out: Array[String] = []
	if state.sites.is_empty():
		return out
	var bits: Array = []
	for s in state.sites:
		var sd: Dictionary = s
		var row := _site_row(state, String(sd.get("id", "")))
		bits.append("%s (rent $%d/wk, %d%% used%s)" % [String(sd.get("name", "?")),
			int(sd.get("rent_wk", 0)), int(round(float(row.get("util", 0.0)) * 100.0)),
			", bleeding" if int(row.get("sev", 0)) >= 2 else ""])
		if bits.size() >= 3:
			break
	out.append("- Roofs beside the home one: %s. Hires and machines need a roof named." % ", ".join(PackedStringArray(bits)))
	return out

## Attention — the works desk: a roof bleeding past its ramp is worth stopping
## the dice for; three red weeks is the alarm.
static func attention(state: GameState) -> Array:
	var rows: Array = []
	for s in state.sites:
		var sd: Dictionary = s
		var row := _site_row(state, String(sd.get("id", "")))
		if int(row.get("sev", 0)) >= 3:
			rows.append({"desk": "the works", "key": "site_bleeds_" + String(sd.get("id", "")),
				"severity": 3, "label": "%s bleeds — fix or close" % String(sd.get("name", "a roof")).left(20),
				"control": "site_" + String(sd.get("id", ""))})
		elif int(row.get("sev", 0)) == 2:
			rows.append({"desk": "the works", "key": "site_neg_" + String(sd.get("id", "")),
				"severity": 2, "label": "%s runs at a loss" % String(sd.get("name", "a roof")).left(24),
				"control": "site_" + String(sd.get("id", ""))})
	return rows

# ── the durable markers (flags-encoded; typed twins bar new record keys) ─────

static func _mark(state: GameState, kind: String, name: String, until_wk: int) -> void:
	var prefix := "%s:%s:" % [kind, name]
	for i in range(state.flags.size() - 1, -1, -1):
		if String(state.flags[i]).begins_with(prefix):
			state.flags.remove_at(i)
	state.flags.append("%s%d" % [prefix, until_wk])

static func _unmark(state: GameState, kind: String, name: String) -> void:
	var prefix := "%s:%s:" % [kind, name]
	for i in range(state.flags.size() - 1, -1, -1):
		if String(state.flags[i]).begins_with(prefix):
			state.flags.remove_at(i)

static func marked_until(state: GameState, kind: String, name: String) -> int:
	var prefix := "%s:%s:" % [kind, name]
	for f in state.flags:
		if String(f).begins_with(prefix):
			return int(String(f).trim_prefix(prefix))
	return -1

static func _prune_marks(state: GameState) -> void:
	for i in range(state.flags.size() - 1, -1, -1):
		var f := String(state.flags[i])
		if f.begins_with("works_ramp:") or f.begins_with("works_off:"):
			var until := int(f.substr(f.rfind(":") + 1))
			if state.week >= until:
				state.flags.remove_at(i)
