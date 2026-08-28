extends SceneTree
## B-COSTS2 UX PROBE — bills · the bank · the works (DAG3 Wave B).
## Photographs every tab × {week-1 zero state, live state with the DO lane,
## a press_receipt open, red state with the ask strip} plus this lane's own
## interactions: the bills source jumps landing spotlit, the bank's unlock
## checklist, the works' walked-number receipt and relief marginal quotes.
##
## Run: RUNWAY_STRESS_DIR=<dir> godot --path game --script tests/ux_costs2.gd
##      (shots run WINDOWED — the gate law)
## Parse-only: RUNWAY_PARSE_ONLY=1 godot --headless --path game --script
##      tests/ux_costs2.gd — loads this lane's scripts and quits 0/1.

var _dir := "/tmp"
var _b: Binder = null
var _shots: Array[String] = []

func _init() -> void:
	call_deferred("_go")

# ═══════════════════════════ the shot harness ════════════════════════════════

func _shot(nm: String) -> void:
	await create_timer(0.25).timeout
	await RenderingServer.frame_post_draw
	root.get_viewport().get_texture().get_image().save_png("%s/%s_godot.png" % [_dir, nm])
	_shots.append(nm)
	print("SHOT %s" % nm)

## Open a fresh binder on a state. The old one is freed first: desk-local
## state dies with the node, which is exactly the desks' own contract.
## The binder lands directly on this lane's page (`land`) so a parallel
## lane's in-flight desk never draws under this probe's camera.
func _open(s: GameState, land: String = "bills") -> Binder:
	if _b != null and is_instance_valid(_b):
		_b.queue_free()
		await process_frame
	var b := Binder.new()
	b.tour_enabled = false
	b.setup(s, null)
	b._page = land
	b._open_group = 1   # COSTS
	root.add_child(b)
	b.size = Vector2(1536, 1024)
	await create_timer(0.30).timeout
	_b = b
	return b

## Point the binder at a desk by its NEW name, seed desk-local state, rebuild.
func _page(b: Binder, id: String, st: Dictionary = {}) -> void:
	b.open_page(id)
	for k in st:
		b.desk[k] = st[k]
	if not st.is_empty():
		b.refresh()

# ═══════════════════════════════ fixtures ════════════════════════════════════

## Week 1, garage, nothing signed — the zero states.
func _week1() -> GameState:
	var s := GameState.new()
	s.sim_seed = 41
	s.week = 1
	s.era = "garage"
	s.cash = 8_000
	s.founder_name = "Lena Voss"
	s.company_name = "Mossflow"
	s.biz_what = "Service"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	return s

## A live office-era Service company: offers, notes, sites-free rung 1-2.
func _live() -> GameState:
	var s := GameState.new()
	s.sim_seed = 42
	s.week = 30
	s.era = "office"
	s.cash = 22_500
	s.traction = 61
	s.product = 55
	s.morale = 62
	s.founder_name = "Lena Voss"
	s.company_name = "Mossflow"
	s.biz_what = "Service"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	s.set_flag("launched")
	s.offers = [
		{"name": "deep-tissue hour", "unit": "per session", "fair_price": 60.0,
			"elasticity": 1.8, "unit_cost": 14.0, "price": 62.0, "price_set": true,
			"weight": 1.0, "fixed_wk": 40.0,
			"cost_lines": [{"label": "oils + linen", "amount": 6.0},
				{"label": "room hour", "amount": 8.0}],
			"fixed_lines": [{"label": "booking app", "amount": 40.0}]},
		{"name": "back-office retainer", "unit": "per month", "fair_price": 240.0,
			"elasticity": 1.2, "unit_cost": 90.0, "price": 250.0, "price_set": true,
			"weight": 0.5, "fixed_wk": 25.0,
			"cost_lines": [{"label": "on-call hours", "amount": 90.0}],
			"fixed_lines": [{"label": "helpdesk seat", "amount": 25.0}]},
		{"name": "posture audit", "unit": "per visit", "fair_price": 120.0,
			"elasticity": 1.5, "unit_cost": 30.0, "price": 115.0, "price_set": true,
			"weight": 0.4, "fixed_wk": 10.0,
			"cost_lines": [{"label": "travel + kit", "amount": 30.0}],
			"fixed_lines": [{"label": "assessment forms", "amount": 10.0}]},
	]
	s.employees = [
		{"name": "Ravi Chand", "role": "operations", "skill": 4, "salary": 640,
			"burnout": 20, "site": ""},
		{"name": "Mina Sorel", "role": "care", "skill": 3, "salary": 540,
			"burnout": 15, "site": ""},
	]
	s.loans = [
		{"kind": "bank", "principal": 12_000, "balance": 8_400, "rate_wk": 0.041,
			"pay_wk": 640, "term_wk": 16, "taken_week": 18, "missed": 0},
		{"kind": "venture", "principal": 6_000, "balance": 6_000, "rate_wk": 0.028,
			"pay_wk": 0, "term_wk": 20, "taken_week": 24, "missed": 0},
	]
	s.set_meta("pnl", {"revenue": 3_900, "cogs": 850, "rent": 600, "payroll": 1_180,
		"infra": 120, "marketing": 900, "sales": 300, "care": 250, "rnd": 400,
		"office": 150, "burn": 4_750, "net": -1_450, "tax": 0, "interest": 512,
		"learning": 0.93})
	for w in range(1, 30):
		s.metric_history.append({"wk": w, "cash": 26_000 - w * 130,
			"customers": 2 * w, "morale": 70 - (w % 9), "revenue": 130 * w,
			"burn": 3_000 + 60 * w, "hype": 20 + (w % 11), "net": 130 * w - 3_000})
	return s

## The live state plus demand overflow: the works red, relief half-open.
func _overflow() -> GameState:
	var s := _live()
	s.traction = 140
	SimWorks.relief_set(s, "freelance", 10)
	return s

## The live state with a note two Mondays missed — the credit lock.
func _locked() -> GameState:
	var s := _live()
	s.loans = [
		{"kind": "bank", "principal": 12_000, "balance": 9_900, "rate_wk": 0.061,
			"pay_wk": 640, "term_wk": 16, "taken_week": 18, "missed": 2},
	]
	s.cash = 900
	return s

## Rung-3: three roofs — the empire lineup.
func _empire() -> GameState:
	var s := _live()
	s.week = 52
	s.era = "floor"
	s.cash = 61_000
	s.traction = 260
	s.sites = [
		{"id": "s1", "name": "Lyon", "rent_wk": 420, "wage_mult": 1.0,
			"learning_count": 120, "demand_weight": 1.0, "opened_wk": 38},
		{"id": "s2", "name": "Turin", "rent_wk": 380, "wage_mult": 0.9,
			"learning_count": 40, "demand_weight": 0.8, "opened_wk": 45},
	]
	for i in 4:
		s.employees.append({"name": "Hand %d" % i, "role": "operations", "skill": 3,
			"salary": 560, "burnout": 22, "site": "s1" if i % 2 == 0 else "s2"})
	return s

# ═════════════════════════════════ the run ═══════════════════════════════════

func _go() -> void:
	await process_frame
	if OS.get_environment("RUNWAY_PARSE_ONLY") != "":
		_parse_only()
		return
	var d := OS.get_environment("RUNWAY_STRESS_DIR")
	if d != "":
		_dir = d

	await _bills()
	await _bank()
	await _works()

	print("B-COSTS2 UX SHOTS: %d captures -> %s" % [_shots.size(), _dir])
	quit(0)

## The parse gate: load this lane's scripts; a parse error fails the load.
func _parse_only() -> void:
	var ok := true
	for p in ["res://src/ui/desks/desk_bills.gd", "res://src/ui/desks/desk_bank_page.gd",
			"res://src/ui/desks/desk_works.gd"]:
		var scr: Variant = load(p)
		if scr == null or not (scr as Script).can_instantiate():
			push_error("PARSE FAIL %s" % p)
			ok = false
		else:
			print("PARSE OK %s" % p)
	print("PARSE GATE %s" % ("PASS" if ok else "FAIL"))
	quit(0 if ok else 1)

# ── bills = THE BILLS LEDGER ─────────────────────────────────────────────────
func _bills() -> void:
	# week 1: the sheet as a promise (S1) — the roof already on it
	var b1 := await _open(_week1())
	_page(b1, "bills")
	await _shot("bills_week1_zero")

	# live: rows with jumps, the memo, the hero delta on a second open
	var s := _live()
	var b := await _open(s)
	_page(b, "bills")
	await _shot("bills_live")
	# second open with a moved ratio: the S5 pen circle + arrows
	_b.queue_free()
	await process_frame   # frees + flushes the seen store
	s.traction = 84
	var pnl: Dictionary = s.get_meta("pnl", {})
	pnl["revenue"] = 5_300
	s.set_meta("pnl", pnl)
	var b2 := await _open(s)
	_page(b2, "bills")
	await _shot("bills_live_delta")

	# the interest row's jump: lands on the bank with the note card spotlit
	# and the "back to bills" pill waiting (the row's own closure re-created)
	b2.focus_desk("the bank", "note_0", "bills")
	await _shot("bills_jump_bank_note")

# ── the bank = THE MEETING ───────────────────────────────────────────────────
func _bank() -> void:
	# week 1 garage: dormant tab, the shark teaching (S1/S8)
	var b1 := await _open(_week1(), "the bank")
	_page(b1, "the bank")
	await _shot("bank_week1_zero")

	# live: notes + DO lane [borrow][repay][refinance] + note_<i> controls
	var b := await _open(_live(), "the bank")
	_page(b, "the bank")
	await _shot("bank_live_do_lane")

	# the receipt re-inks on a stepper press: photograph right after the press
	var bb := await _open(_live(), "the bank")
	_page(bb, "the bank")
	await process_frame
	# walk the borrow ladder one notch up through the desk's own state, the
	# same write the + square performs, then refresh — the flick plays
	bb.desk["borrow"] = 15_000
	bb.desk["flick"] = true
	bb.refresh()
	await create_timer(0.06).timeout
	await RenderingServer.frame_post_draw
	root.get_viewport().get_texture().get_image().save_png("%s/bank_receipt_flick_godot.png" % _dir)
	_shots.append("bank_receipt_flick")
	print("SHOT bank_receipt_flick")
	await _shot("bank_receipt_reinked")

	# locked: the unlock CHECKLIST from the real lock state + the ask strip
	var bl := await _open(_locked(), "the bank")
	_page(bl, "the bank")
	await _shot("bank_locked_checklist_red")

# ── the works = THE FOUR-TYPE ENGINE ─────────────────────────────────────────
func _works() -> void:
	# week 1, no offers: the S1 zero state proper
	var b1 := await _open(_week1(), "the works")
	_page(b1, "the works")
	await _shot("works_week1_zero")

	# live house face: DO lane [set relief], the ticket book, capacity band
	var b := await _open(_live(), "the works")
	_page(b, "the works")
	await _shot("works_live_do_lane")

	# overflow: red ask strip + the walked number + its receipt open
	var so := _overflow()
	var bo := await _open(so, "the works")
	_page(bo, "the works")
	await _shot("works_red_ask_strip")
	DeskWorks.open_walked_receipt(bo, so)
	await _shot("works_walked_receipt")

	# the capacity sheet: relief rows quoting "next 10 ≈ $X vs $Y in-house"
	_page(bo, "the works", {"page": "capacity"})
	await _shot("works_relief_marginal")

	# the empire: lineup rows registered as site_<id>, [arrange]/[open a roof]
	var be := await _open(_empire(), "the works")
	_page(be, "the works")
	await _shot("works_empire_do_lane")
	# the site drill: the crumb "Lyon ‹ the works" + back word
	_page(be, "the works", {"row": "s1"})
	await _shot("works_site_drill_crumb")
