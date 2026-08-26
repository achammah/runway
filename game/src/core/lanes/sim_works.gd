class_name SimWorks
extends RefCounted
## LANE — THE WORKS (per-type capacity, the unit ticket, relief valves).
## Spec: docs/design/DECISIONS.md (the factory → THE WORKS + SCALE LADDER) +
## docs/design/DAG2.md (L-DIVWORKS) + mockups 10/11/12.
##
## Same four questions in every business, in its own units: can we serve? ·
## what does one cost? · what makes the capacity? · what's the relief valve?
##   Service      crew bookable hours (people × slots, site-aware, sleeps when
##                they do) · relief = freelancers, priced per unit
##   Software     the care team is the ceiling (care $ + support heads); past
##                it nothing walks away — replies slip and churn bites
##                (DEGRADATION, not lost sales) · relief = cloud burst, capped
##                because the queue's human half doesn't burst
##   Hardware     the machines — the factory lane's molecule, REUSED not
##                forked: capacity, stockouts, un-billing and the subcontract
##                valve all stay SimFactory's; this lane only reads
##   Marketplace  the seller pool is the factory (supply proxy off traction,
##                lagged by growth — fast growth starves the shelves) · relief
##                = a recruit-supply push
##
## THE GAP IS PRICED HONESTLY: service/marketplace lose UN-BILLED revenue
## ("$320 walks away" — deducted through the money record like the factory's
## own lost billing); software pays in churn (tick_post, the factory's
## `walked` idiom, documented multiplier below); hardware already pays both
## through SimFactory.
##
## THE RELIEF VALVES are standing levers (DM op `set_relief`, coordinator
## ruling). Per-valve semantics of x:
##   freelance       x = units/wk the outside hands may serve (0..60); each
##                   unit billed at price_book.freelance_rate dollars
##   subcontract     x = 1/0 — the factory's own make-vs-buy toggle (hardware
##                   only; other types answer "no outside shop")
##   burst           x = extra seats of ceiling bought (0..4000), billed per
##                   seat actually over; relief capped at 60% of the overload
##                   because the queue's human half doesn't burst
##   recruit_supply  x = $/wk of seller recruitment (0..2000); ≈ one new
##                   seller per $35, each feeding ≈2.5 orders/wk
## DURABLE HOME: the levers ride state.flags as "works_relief:<cat>:<int>" —
## the one untyped, save-whole list both engines share (the C# GameState's
## typed records bar new keys; a first-class field is offered to W3).
##
## The spine calls (docs/design/HOOKS.md): tick_pre §7i (the week's dice are
## drawn and parked — capacity jitter, freelancer availability), tick_money
## (the served/walked math runs on SETTLED traction; owns ONLY m["relief"]
## and the un-billing against m["revenue"]), tick_post (software's churn tax
## + the receipts), directives(), attention().
##
## SALTS: SALT_WORKS_CAPACITY (160) capacity jitter · SALT_WORKS_RELIEF (161)
## freelancer availability · SALT_WORKS_REMAINDER (162) seeded remainders for
## fractional native units. Draw order per week is FIXED: jitter, avail, then
## remainders in walk → degrade order.
##
## TWIN LAW: unity/Assets/Scripts/Core/Lanes/SimWorks.cs carries the same
## logic in the same order.

# ── CAPACITY (service) ───────────────────────────────────────────────────────
const SLOTS_BASE := 24.0        ## one serving head's bookable units/wk
const SLOTS_SKILL := 2.0        ## ± per skill point off 3
const FOUNDER_SLOTS := 26.0     ## the founder's own hands, always on the floor
# ── CEILING (software) ───────────────────────────────────────────────────────
const SW_FREE_SEATS := 400.0    ## a young product self-serves this far
const SW_SEAT_COST := 2.6       ## care-effective dollars per seat of ceiling
const SW_DEGRADE_RATE := 0.004  ## 0.4pt of the base churn rate per week at full overload
const SW_OVER_SPAN := 0.25      ## full overload = 25% past the ceiling
const SW_BURST_CAP := 0.6       ## burst closes at most 60% of the overload
# ── SUPPLY (marketplace) ─────────────────────────────────────────────────────
const MK_SELLER_RATIO := 0.42   ## active sellers per buyer at steady state
const MK_SELLER_FEED := 2.5     ## orders one seller feeds per week
const MK_SELLER_COST := 35.0    ## $ of recruitment per new seller
const MK_LAG_K := 2.0           ## fast growth starves supply: feed ÷ (1 + K×growth)
# ── the week's dice ──────────────────────────────────────────────────────────
const CAP_JITTER := 0.04        ## ±4% weekly on human capacity (the breakdown analogue)
const RELIEF_AVAIL_LO := 0.7    ## freelancers answer 70-100% of the ask
# ── lever clamps ─────────────────────────────────────────────────────────────
const FREELANCE_MAX := 60
const BURST_MAX := 4000
const RECRUIT_MAX := 2000
const LINE_CAP := 4             ## no lane floods the journal
## Retiring a product: exactly half its customers migrate to neighbor offers,
## the rest churn — a deterministic split, no die needed.
const RETIRE_MIGRATE := 0.5

# ═════════════════════════════ ACTIVE & VOCAB ════════════════════════════════

## The works reads the catalog for its ticket: no offers, no works math (the
## desk still draws the crew honestly; a legacy run keeps its old arithmetic).
static func active(state: GameState) -> bool:
	return not state.offers.is_empty()

## This business's native words, written once at birth (topics.works — with a
## graceful hand for the older works_terms key), else the type's own defaults.
static func vocab(state: GameState) -> Dictionary:
	var t: Variant = state.topics.get("works", state.topics.get("works_terms", {}))
	var d: Dictionary = t if t is Dictionary else {}
	var unit := "unit"
	var cap := "capacity"
	var relief := "outside help"
	match state.biz_what:
		"Service":
			unit = "session"; cap = "bookable hours"; relief = "freelancers"
		"Software":
			unit = "seat"; cap = "the care team"; relief = "cloud burst"
		"Hardware":
			unit = "unit"; cap = "the machines"; relief = "the subcontract shop"
		"Marketplace":
			unit = "order"; cap = "the seller pool"; relief = "recruited supply"
	return {"unit_word": String(d.get("unit_word", unit)),
		"capacity_word": String(d.get("capacity_word", cap)),
		"relief_word": String(d.get("relief_word", relief))}

# ═════════════════════════ THE RELIEF LEVERS (durable) ═══════════════════════

static func relief_get(state: GameState, cat: String) -> int:
	if cat == "subcontract":
		return 1 if bool(state.hardware.get("subcontract_on", false)) else 0
	var prefix := "works_relief:%s:" % cat
	for f in state.flags:
		if String(f).begins_with(prefix):
			return int(String(f).trim_prefix(prefix))
	return 0

## THE ONE WRITE — clamped here, whoever asks (desk stepper or DM op).
static func relief_set(state: GameState, cat: String, x: int) -> int:
	var v := 0
	match cat:
		"freelance":
			v = clampi(x, 0, FREELANCE_MAX)
		"burst":
			v = clampi(x, 0, BURST_MAX)
		"recruit_supply":
			v = clampi(x, 0, mini(RECRUIT_MAX, SimEngine.era_spend_cap(state.era) / 4))
		"subcontract":
			if SimFactory.active(state) and SimFactory.sub_unlocked(state):
				var want := x > 0
				if bool(state.hardware.get("subcontract_on", false)) != want:
					SimFactory.toggle_subcontract(state)
				return 1 if want else 0
			return 0
		_:
			return 0
	var prefix := "works_relief:%s:" % cat
	for i in range(state.flags.size() - 1, -1, -1):
		if String(state.flags[i]).begins_with(prefix):
			state.flags.remove_at(i)
	if v > 0:
		state.flags.append("%s%d" % [prefix, v])
	return v

## The desk's stepper ladders, per valve — the world's own list.
static func relief_steps(cat: String) -> Array:
	match cat:
		"freelance":
			return [0, 2, 4, 6, 8, 10, 14, 20, 30, 45, 60]
		"burst":
			return [0, 100, 200, 400, 800, 1600, 3000, 4000]
		"recruit_supply":
			return [0, 100, 200, 300, 500, 800, 1200, 2000]
	return [0, 1]

# ═════════════════════════ THE NATIVE ARITHMETIC ═════════════════════════════

## What the market wants this week, in native units: every billing offer's
## customers × cadence — the demand-mix rows summed (the "wanted" column).
static func demand_units(state: GameState) -> float:
	var total := 0.0
	var fm := SimEngine.street_fair_mult(state)
	for o in state.offers:
		var od: Dictionary = o
		if SimEngine.offer_billed_price(od, fm) <= 0.0:
			continue
		total += float(state.traction) * float(od.get("weight", 1.0)) \
			* SimEngine.offer_cadence(String(od.get("unit", "")))
	return total

## What one native unit bills — revenue over units, so the un-billing and the
## books can never disagree about what a walked unit was worth.
static func rev_per_unit(state: GameState) -> float:
	var units := demand_units(state)
	if units <= 0.0:
		return 0.0
	var arpu := SimEngine.offers_arpu(state)
	if arpu < 0.0:
		return 0.0
	return float(state.traction) * arpu / units

## The catalog's variable cost per unit, volume-blended — the ticket's "costs,
## each" before a roof's own rent and wages bend it (divisions' half).
static func base_unit_cost(state: GameState) -> float:
	var units := 0.0
	var cost := 0.0
	var fm := SimEngine.street_fair_mult(state)
	for o in state.offers:
		var od: Dictionary = o
		if SimEngine.offer_billed_price(od, fm) <= 0.0:
			continue
		var u := float(od.get("weight", 1.0)) * SimEngine.offer_cadence(String(od.get("unit", "")))
		units += u
		cost += u * float(od.get("unit_cost", 0.0))
	return (cost / units) if units > 0.0 else 0.0

## What the feature inventory adds to serving one unit of a product — the
## works' half of WHAT WE MAKE's cost footer. A thin seam over the features
## lane's own read (the product's features + the shared plumbing every
## product stands on); the works never re-derives what L-MAKE owns.
static func feature_cost_add(state: GameState, product_id: String) -> float:
	return maxf(SimFeatures.unit_cost_total(state, product_id), 0.0)

## THE CREW'S HANDS, site-aware. Serving roles are everyone but the sellers
## and the marketers (a manager runs the floor at half a hand). A person mid
## ramp (works_ramp marker) or still onboarding gives zero this week.
static func capacity_of_site(state: GameState, site: String) -> float:
	if state.biz_what != "Service":
		return 0.0
	var slots := 0.0
	if site == "" or state.sites.is_empty():
		slots += FOUNDER_SLOTS   # the founder's hands live on the home roof
	for e in state.employees:
		var ed: Dictionary = e
		if String(ed.get("site", "")) != site:
			continue
		var role := String(ed.get("role", ""))
		if role.contains("sales") or role.contains("marketing"):
			continue
		if SimDivisions.marked_until(state, "works_ramp", String(ed.get("name", ""))) > state.week - 1:
			continue
		var skill := clampi(int(ed.get("skill", 3)), 1, 5)
		var hand := SLOTS_BASE + SLOTS_SKILL * float(skill - 3)
		if role.contains("manager"):
			hand *= 0.5
		slots += hand
	return slots

static func service_capacity(state: GameState) -> float:
	var total := capacity_of_site(state, "")
	for s in state.sites:
		total += capacity_of_site(state, String((s as Dictionary).get("id", "")))
	return total

## THE SOFTWARE CEILING — servers scale for pennies; the care team is the real
## ceiling: free seats + care-effective dollars at $2.60 a seat, plus whatever
## burst was provisioned.
static func software_ceiling(state: GameState) -> float:
	return SW_FREE_SEATS + SimLabor.care_eff(state, float(state.budgets.get("care", 0))) / SW_SEAT_COST

## THE SELLER POOL — other people's shops, lagged by your own growth: the
## faster the buyers arrive, the further supply runs behind.
static func marketplace_supply(state: GameState) -> float:
	var lag := 1.0 / (1.0 + MK_LAG_K * clampf(state.last_growth, 0.0, 0.5))
	var sellers := ceilf(float(state.traction) * MK_SELLER_RATIO)
	return sellers * MK_SELLER_FEED * lag

static func seller_pool(state: GameState) -> int:
	return int(ceilf(float(state.traction) * MK_SELLER_RATIO))

## THE UNIT TICKET — the offer's own generated cost lines (learning applied at
## the total, never per line) + the feature inventory's per-unit adds.
## Returns {lines: [{label, amount}], cost_each, sells, margin, lc}.
static func unit_ticket(state: GameState, offer_i: int) -> Dictionary:
	if offer_i < 0 or offer_i >= state.offers.size():
		return {}
	var od: Dictionary = state.offers[offer_i]
	var lc := SimEngine.learning_curve(state)
	var lines: Array = []
	if od.has("cost_lines"):
		for cl in od["cost_lines"]:
			lines.append({"label": String((cl as Dictionary).get("label", "line")),
				"amount": float((cl as Dictionary).get("amount", 0.0))})
	else:
		lines.append({"label": "cost of one", "amount": float(od.get("unit_cost", 0.0))})
	var feat := feature_cost_add(state, String(od.get("product_id", "")))
	if feat > 0.005:
		lines.append({"label": "the features' share", "amount": feat})
	var cost := float(od.get("unit_cost", 0.0)) * lc + feat
	var sells := SimEngine.offer_billed_price(od, SimEngine.street_fair_mult(state))
	return {"lines": lines, "cost_each": cost, "sells": sells,
		"margin": sells - cost, "lc": lc}

# ═════════════════════════ THE WEEK'S PLAN (pure) ════════════════════════════

## The whole works, one honest map — the tick computes it with the week's own
## dice; the desk recomputes it live with quiet dice (jitter 1, avail 1).
static func week_plan(state: GameState, jitter: float, avail: float) -> Dictionary:
	var v := vocab(state)
	var w := {"type": state.biz_what, "unit_word": String(v.get("unit_word", "unit")),
		"demand_units": 0.0, "capacity_units": 0.0, "relief_cap_units": 0.0,
		"relief_used": 0.0, "relief_spend": 0.0, "served_units": 0.0,
		"walk_units": 0.0, "unbilled": 0.0, "rev_per_unit": 0.0,
		"ceiling": 0.0, "over": 0.0, "sellers": 0, "jitter": jitter, "avail": avail}
	if not active(state):
		return w
	var units := demand_units(state)
	w["demand_units"] = units
	w["rev_per_unit"] = rev_per_unit(state)
	match state.biz_what:
		"Service":
			var cap := service_capacity(state) * jitter
			w["capacity_units"] = cap
			var fee := SimDivisions.pb(state, "freelance_rate")
			var cap_units := float(relief_get(state, "freelance")) * avail
			w["relief_cap_units"] = cap_units
			var gap := maxf(units - cap, 0.0)
			var used := minf(gap, cap_units)
			w["relief_used"] = used
			w["relief_spend"] = used * fee
			w["served_units"] = minf(units, cap + used)
			w["walk_units"] = maxf(units - float(w["served_units"]), 0.0)
			w["unbilled"] = float(w["walk_units"]) * float(w["rev_per_unit"])
		"Software":
			var ceiling := software_ceiling(state)
			var seats := float(state.traction)
			var burst_seats := float(relief_get(state, "burst"))
			w["ceiling"] = ceiling + burst_seats
			w["capacity_units"] = ceiling + burst_seats
			var over := maxf(seats - ceiling, 0.0)
			# burst closes at most 60% of the RAW overload — the queue's human
			# half doesn't burst; billed per seat it actually covered
			var burst_used := minf(minf(burst_seats, over), over * SW_BURST_CAP)
			var rate := maxf(0.4 * base_unit_cost(state) * SimEngine.learning_curve(state), 0.3)
			w["relief_cap_units"] = burst_seats
			w["relief_used"] = burst_used
			w["relief_spend"] = burst_used * rate
			w["over"] = maxf(over - burst_used, 0.0)
			w["served_units"] = seats
			w["demand_units"] = seats
		"Marketplace":
			var feed := marketplace_supply(state) * jitter
			w["sellers"] = seller_pool(state)
			w["capacity_units"] = feed
			var push := float(relief_get(state, "recruit_supply"))
			var pushed_units := push / MK_SELLER_COST * MK_SELLER_FEED
			w["relief_cap_units"] = pushed_units
			var gap2 := maxf(units - feed, 0.0)
			var used2 := minf(gap2, pushed_units)
			w["relief_used"] = used2
			w["relief_spend"] = push if push > 0.0 else 0.0   # the push spends whole — it's advertising
			w["served_units"] = minf(units, feed + used2)
			w["walk_units"] = maxf(units - float(w["served_units"]), 0.0)
			w["unbilled"] = float(w["walk_units"]) * float(w["rev_per_unit"])
		"Hardware":
			# the factory owns the whole molecule — the works only reads it
			var hw := SimFactory.week_block(state)
			w["capacity_units"] = float(hw.get("capacity", SimFactory.capacity(state)))
			w["demand_units"] = float(hw.get("demand_units", units))
			w["served_units"] = float(hw.get("sold", 0))
			w["walk_units"] = float(hw.get("lost_adds", 0))
			w["relief_used"] = float(hw.get("sub_units", 0))
			w["relief_spend"] = 0.0   # billed by the factory's own subcontract lane
	return w

## LAST WEEK'S WORKS, whole — {} before the first tick and after a load, which
## the desk answers by recomputing live (quiet dice).
static func week_view(state: GameState) -> Dictionary:
	var w: Variant = state.get_meta("works", {})
	if w is Dictionary and not (w as Dictionary).is_empty():
		return w
	return week_plan(state, 1.0, 1.0)

# ═══════════════════════════ THE SPINE'S ENTRY POINTS ════════════════════════

## Tick §7i — the week's dice are drawn HERE (order fixed: ① capacity jitter
## ② freelancer availability) and parked; the serving math itself waits for
## tick_money, where traction has settled.
static func tick_pre(state: GameState, _rep: Dictionary) -> void:
	if not active(state):
		state.set_meta("works", {})
		return
	var jitter := 1.0
	var avail := 1.0
	if state.biz_what == "Service" or state.biz_what == "Marketplace":
		jitter = 1.0 + (SimEngine.rng_for(state, SimEngine.SALT_WORKS_CAPACITY).randf() * 2.0 - 1.0) * CAP_JITTER
		avail = RELIEF_AVAIL_LO + SimEngine.rng_for(state, SimEngine.SALT_WORKS_RELIEF).randf() * (1.0 - RELIEF_AVAIL_LO)
	state.set_meta("works_dice", {"jitter": jitter, "avail": avail})

## The money section — traction is settled, so the works serves the week now.
## Owns ONLY m["relief"]; the un-billing rides m["revenue"] the same way the
## factory's lost billing does (the record answers back).
static func tick_money(state: GameState, rep: Dictionary, m: Dictionary) -> void:
	if not active(state):
		return
	var dice: Dictionary = state.get_meta("works_dice", {})
	var w := week_plan(state, float(dice.get("jitter", 1.0)), float(dice.get("avail", 1.0)))
	state.set_meta("works", w)
	state.remove_meta("works_dice")
	if state.biz_what == "Hardware":
		return   # the factory books its own money
	var relief := float(w.get("relief_spend", 0.0))
	if relief >= 1.0:
		m["relief"] = float(m.get("relief", 0.0)) + relief
	var unbilled := minf(float(w.get("unbilled", 0.0)), maxf(float(m.get("revenue", 0.0)), 0.0))
	if unbilled >= 1.0:
		m["revenue"] = float(m.get("revenue", 0.0)) - unbilled
		w["unbilled"] = unbilled

## After the record: software's overload collects its churn (the factory's
## `walked` idiom — extra churn = traction × 0.4%/wk × how far past the
## ceiling, seeded remainder), then the receipts, loudest first.
static func tick_post(state: GameState, rep: Dictionary) -> void:
	if not active(state):
		return
	var w := week_view(state)
	var lines: Array[String] = []
	var vw := vocab(state)
	var unit_word := String(vw.get("unit_word", "unit"))
	if state.biz_what == "Software":
		var over := float(w.get("over", 0.0))
		if over > 0.0:
			var ceiling := maxf(float(w.get("ceiling", 1.0)), 1.0)
			var over_frac := clampf(over / (ceiling * SW_OVER_SPAN), 0.0, 1.0)
			var exact := float(state.traction) * SW_DEGRADE_RATE * over_frac
			var walked := int(floor(exact))
			if SimEngine.rng_for(state, SimEngine.SALT_WORKS_REMAINDER).randf() < exact - floor(exact):
				walked += 1
			walked = mini(walked, state.traction)
			if walked > 0:
				state.traction = maxi(state.traction - walked, 0)
				w["degrade_walked"] = walked
				lines.append("past the ceiling nothing walks away — replies slip instead: −%d churned to the queue (%d %ss over)" % [
					walked, int(round(over)), unit_word])
		elif float(w.get("relief_used", 0.0)) > 0.0:
			lines.append("cloud burst held the line: %d %ss served over the care ceiling" % [
				int(round(float(w.get("relief_used", 0.0)))), unit_word])
	var walk := float(w.get("walk_units", 0.0))
	if walk >= 1.0 and float(w.get("unbilled", 0.0)) >= 1.0:
		lines.append("%d %ss turned away — $%d/wk walks (hands for %d of %d)" % [
			int(round(walk)), unit_word, int(round(float(w.get("unbilled", 0.0)))),
			int(round(float(w.get("served_units", 0.0)))), int(round(float(w.get("demand_units", 0.0))))])
	var used := float(w.get("relief_used", 0.0))
	if used >= 1.0 and state.biz_what != "Software" and state.biz_what != "Hardware":
		var rw := String(vw.get("relief_word", "outside help"))
		lines.append("%s served %d %ss: −$%d — dearer each, but dearer beats turned away" % [
			rw, int(round(used)), unit_word, int(round(float(w.get("relief_spend", 0.0))))])
	if used > 0.0 and used >= float(w.get("relief_cap_units", 0.0)) - 0.01 and walk >= 1.0:
		lines.append("the relief valve is full open and it still wasn't enough")
	for i in mini(lines.size(), LINE_CAP):
		rep["lines"].append(lines[i])

## DM context — the works in one line, native units, never a price to invent.
static func directives(state: GameState) -> Array[String]:
	var out: Array[String] = []
	if not active(state):
		return out
	var w := week_view(state)
	var vw := vocab(state)
	if state.biz_what == "Software":
		out.append("- The works: %d %ss live under a ceiling of %d (%s)." % [
			int(round(float(w.get("served_units", 0.0)))), String(vw.get("unit_word", "seat")),
			int(round(float(w.get("ceiling", 0.0)))), String(vw.get("capacity_word", "the care team"))])
	else:
		out.append("- The works: %d %ss wanted, capacity for %d%s." % [
			int(round(float(w.get("demand_units", 0.0)))), String(vw.get("unit_word", "unit")),
			int(round(float(w.get("capacity_units", 0.0)))),
			" — %d walked" % int(round(float(w.get("walk_units", 0.0)))) if float(w.get("walk_units", 0.0)) >= 1.0 else ""])
	return out

## Attention — the works desk: money walking or churn biting is worth a stop;
## a saturated valve says the fix is structural; a moved machine offline names
## its roof (the factory's own row covers the home floor).
static func attention(state: GameState) -> Array:
	var rows: Array = []
	if not active(state):
		return rows
	var w := week_view(state)
	var unbilled := float(w.get("unbilled", 0.0))
	if unbilled >= 1.0:
		rows.append({"desk": "the works", "key": "works_gap", "severity": 2,
			"label": "$%d/wk walks — capacity short" % int(round(unbilled))})
	if int(w.get("degrade_walked", 0)) > 0:
		rows.append({"desk": "the works", "key": "works_degrade", "severity": 2,
			"label": "past the ceiling — churn is the queue"})
	var used := float(w.get("relief_used", 0.0))
	if used > 0.0 and used >= float(w.get("relief_cap_units", 0.0)) - 0.01 \
			and float(w.get("walk_units", 0.0)) >= 1.0:
		rows.append({"desk": "the works", "key": "relief_full", "severity": 2,
			"label": "relief valve full open — still short"})
	for mrec in (state.hardware.get("equipment", []) as Array):
		var md: Dictionary = mrec
		if String(md.get("site", "")) != "" \
				and SimDivisions.marked_until(state, "works_off", String(md.get("name", ""))) > state.week - 1:
			rows.append({"desk": "the works", "key": "machine_moving", "severity": 2,
				"label": "%s offline — mid-move" % String(md.get("name", "a machine")).left(24)})
	return rows

# ═══════════════════ THE MUTATION-LAW EXECUTORS (this lane's) ════════════════

## REFINANCE — swap a CURRENT bank note for a new quote at today's standing.
## The break fee is the price book's; a distressed note (missed ≥ 1) or a
## locked book refuses — refinancing must never launder the miss ladder.
static func refinance_quote(state: GameState, idx: int, term: int) -> Dictionary:
	if idx < 0 or idx >= state.loans.size():
		return {}
	var note: Dictionary = state.loans[idx]
	if String(note.get("kind", "")) != "bank" or int(note.get("balance", 0)) <= 0:
		return {}
	if int(note.get("missed", 0)) >= 1 or SimBank.credit_locked(state):
		return {}
	var terms: Array = SimBank.term_options(state, "bank")
	var t := term if terms.has(term) else int(terms[0])
	var rate := SimBank.bank_rate_wk(state)
	var fee := int(round(SimDivisions.pb(state, "refinance_break_fee")))
	var bal := int(note.get("balance", 0))
	return {"old_rate": float(note.get("rate_wk", 0.0)), "new_rate": rate, "fee": fee,
		"balance": bal, "term": t, "old_pay": int(note.get("pay_wk", 0)),
		"new_pay": SimBank.loan_payment_wk(bal, rate, t)}

static func refinance_note(state: GameState, idx: int, term: int) -> Dictionary:
	var q := refinance_quote(state, idx, term)
	if q.is_empty():
		return {"ok": false, "why": "only a current bank note refinances"}
	if state.cash < int(q.get("fee", 0)):
		return {"ok": false, "why": "$%d short of the break fee" % (int(q.get("fee", 0)) - state.cash)}
	var note: Dictionary = state.loans[idx]
	state.cash -= int(q.get("fee", 0))
	note["rate_wk"] = float(q.get("new_rate", 0.0))
	note["term_wk"] = int(q.get("term", 8))
	note["taken_week"] = state.week
	note["pay_wk"] = int(q.get("new_pay", 0))
	state.log_action("REFINANCED the bank note: %.1f%%→%.1f%%/wk, break fee $%d, $%d/wk now" % [
		float(q.get("old_rate", 0.0)) * 100.0, float(q.get("new_rate", 0.0)) * 100.0,
		int(q.get("fee", 0)), int(q.get("new_pay", 0))])
	return {"ok": true, "why": "", "quote": q}

## FIRE AN ACCOUNT — the contract penalty bills, the revenue dies, and the
## street hears it (the typed rival_fud cloud: word gets around).
static func fire_account(state: GameState, name := "") -> Dictionary:
	if state.traction <= 0:
		return {"ok": false, "why": "no accounts to fire"}
	var penalty := int(round(SimDivisions.pb(state, "account_fire_penalty")))
	var seats := 1
	var who := name.strip_edges()
	if state.biz_who == "Enterprise" and not state.logos.is_empty():
		var hit_i := 0
		for i in state.logos.size():
			if who != "" and String((state.logos[i] as Dictionary).get("name", "")).to_lower().contains(who.to_lower()):
				hit_i = i
				break
		var logo: Dictionary = state.logos[hit_i]
		who = String(logo.get("name", "an account"))
		seats = maxi(int(logo.get("seats", 1)), 1)
		state.logos.remove_at(hit_i)
	elif who == "":
		who = "the account"
	state.cash -= penalty
	state.traction = maxi(state.traction - seats, 0)
	state.hype = clampi(state.hype - 2, 0, 100)
	SimEngine.add_status(state, "rival_fud", 2)
	state.log_action("FIRED %s: −$%d penalty, %d customer%s gone — the street heard" % [
		who, penalty, seats, "s" if seats > 1 else ""])
	return {"ok": true, "why": "", "penalty": penalty, "seats": seats, "who": who}

## RETIRE A PRODUCT — its offers retire with it; exactly half its customers
## migrate to neighbor offers, the rest churn (a deterministic split); its
## features die with the codebase they lived in.
static func retire_product(state: GameState, product_id: String) -> Dictionary:
	if SimDivisions.products_count(state) < 2:
		return {"ok": false, "why": "the only product cannot retire — that is a pivot"}
	var weight_all := 0.0
	var weight_gone := 0.0
	var names: Array = []
	for o in state.offers:
		var od: Dictionary = o
		weight_all += float(od.get("weight", 1.0))
		if String(od.get("product_id", "")) == product_id:
			weight_gone += float(od.get("weight", 1.0))
			names.append(String(od.get("name", "?")))
	if names.is_empty():
		return {"ok": false, "why": "no product called '%s'" % product_id}
	var share := weight_gone / maxf(weight_all, 0.001)
	var cust := int(round(float(state.traction) * share))
	var churned := int(floor(float(cust) * (1.0 - RETIRE_MIGRATE)))
	for i in range(state.offers.size() - 1, -1, -1):
		if String((state.offers[i] as Dictionary).get("product_id", "")) == product_id:
			state.offers.remove_at(i)
	for j in range(state.features.size() - 1, -1, -1):
		if String((state.features[j] as Dictionary).get("product_id", "")) == product_id:
			state.features.remove_at(j)
	state.traction = maxi(state.traction - churned, 0)
	state.log_action("RETIRED %s: %s off the shelf, %d migrated, %d churned" % [
		product_id, ", ".join(PackedStringArray(names)), cust - churned, churned])
	return {"ok": true, "why": "", "offers": names, "migrated": cust - churned,
		"churned": churned}

# ═══════════════════ THE DM OP DOORS (garage executor arms) ══════════════════

## {op:"set_relief", cat:<valve>, x:<per-valve semantics above>, weeks:1}
static func op_set_relief(state: GameState, d: Dictionary) -> String:
	var cat := String(d.get("cat", ""))
	if not ["freelance", "subcontract", "burst", "recruit_supply"].has(cat):
		return "no relief valve called '%s'" % cat
	var x := int(d.get("x", d.get("v", 0)))
	if cat == "subcontract" and not (SimFactory.active(state) and SimFactory.sub_unlocked(state)):
		return "no outside shop for this business yet"
	var v := relief_set(state, cat, x)
	match cat:
		"freelance":
			return "freelancers booked up to %d/wk (at $%d each)" % [v, int(round(SimDivisions.pb(state, "freelance_rate")))]
		"burst":
			return "cloud burst provisioned: +%d seats of ceiling" % v
		"recruit_supply":
			return "seller recruitment push: $%d/wk (≈%d new sellers)" % [v, int(round(float(v) / MK_SELLER_COST))]
	return "the subcontract shop is %s" % ("ON" if v > 0 else "OFF")

static func op_refinance_note(state: GameState, d: Dictionary) -> String:
	var idx := int(d.get("old_id", d.get("v", 0)))
	var term := int(d.get("weeks", 12))
	var res := refinance_note(state, idx, term)
	if not bool(res.get("ok", false)):
		return String(res.get("why", ""))
	var q: Dictionary = res.get("quote", {})
	return "REFINANCED: %.1f%%→%.1f%%/wk over %d wks, break fee $%d" % [
		float(q.get("old_rate", 0.0)) * 100.0, float(q.get("new_rate", 0.0)) * 100.0,
		int(q.get("term", 0)), int(q.get("fee", 0))]

static func op_fire_account(state: GameState, d: Dictionary) -> String:
	var res := fire_account(state, String(d.get("cat", "")))
	if not bool(res.get("ok", false)):
		return String(res.get("why", ""))
	return "FIRED %s: −$%d penalty, the revenue dies, the street heard" % [
		String(res.get("who", "the account")), int(res.get("penalty", 0))]

static func op_retire_product(state: GameState, d: Dictionary) -> String:
	var res := retire_product(state, String(d.get("cat", d.get("v", ""))))
	if not bool(res.get("ok", false)):
		return String(res.get("why", ""))
	return "RETIRED the product: %d customers migrated, %d churned with it" % [
		int(res.get("migrated", 0)), int(res.get("churned", 0))]
