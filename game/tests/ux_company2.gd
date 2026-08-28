extends SceneTree
## UX PROBE — B-COMPANY2 (DAG3 Wave B): the street · threats · pivot · the
## offer, photographed across the plan's matrix — week-1 / zero states (S1),
## live states with the DO lane (S3), one receipt popover open (S4), red
## states with the ask strip (S2) — plus the street's seen-store pen circles
## (S5) against a pre-seeded last-open file.
##
## Run WINDOWED (shots come out black headless):
##   RUNWAY_STRESS_DIR=<dir> godot --path game --script tests/ux_company2.gd
## Files land as <surface>_godot.png. Raw pages: tour_enabled=false.

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

func _open(s: GameState) -> Binder:
	if _b != null and is_instance_valid(_b):
		_b.queue_free()
		await process_frame
	var b := Binder.new()
	b.tour_enabled = false
	b.setup(s, null)
	root.add_child(b)
	b.size = Vector2(1536, 1024)
	await create_timer(0.30).timeout
	_b = b
	return b

func _page(b: Binder, id: String, st: Dictionary = {}) -> void:
	b.open_page(id)
	for k in st:
		b.desk[k] = st[k]
	if not st.is_empty():
		b.refresh()

# ═══════════════════════════════ fixtures ════════════════════════════════════

## Three rivals the street can rank — one came at YOU (sniffing).
func _rivals() -> Array:
	return [
		{"name": "Vanta Systems", "strength": 62.0, "vigor": 66.0,
			"price_posture": 0.90, "focus": "price", "hype": 40.0,
			"last_action": "price_cut", "sniffing": 0, "cooldowns": {},
			"tactics": ["price", "hype"],
			"log": ["wk31: shipped a feature", "wk33: cut prices"]},
		{"name": "Fernbay Group", "strength": 48.0, "vigor": 58.0,
			"price_posture": 1.02, "focus": "growth", "hype": 26.0,
			"last_action": "sniff", "sniffing": 1, "cooldowns": {},
			"tactics": ["growth"],
			"log": ["wk30: poached a designer", "wk34: asked around about you"]},
		{"name": "Quill Health", "strength": 34.0, "vigor": 44.0,
			"price_posture": 1.00, "focus": "product", "hype": 18.0,
			"last_action": "quiet", "sniffing": 0, "cooldowns": {},
			"tactics": ["product"],
			"log": ["wk29: banked cash and said nothing"]},
	]

## Week 1 — the first thing a player ever sees: a seeded world, quiet books.
func _w1(seed_v: int) -> GameState:
	var s := GameState.new()
	s.sim_seed = seed_v
	s.week = 1
	s.era = "garage"
	s.cash = 12_000
	s.founder_name = "Lena Voss"
	s.company_name = "Mossflow"
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	s.offers = [{"name": "pocket synth", "unit": "per unit", "fair_price": 20.0,
		"elasticity": 2.4, "unit_cost": 9.0, "price": 0.0, "weight": 1.0}]
	for r in _rivals():
		var rd: Dictionary = (r as Dictionary).duplicate(true)
		rd["log"] = []
		rd["last_action"] = ""
		rd["sniffing"] = 0
		s.rivals.append(rd)
	return s

## Week 34 — a live company: rivals with rap sheets, a warm book, real books.
func _live(seed_v: int) -> GameState:
	var s := GameState.new()
	s.sim_seed = seed_v
	s.week = 34
	s.era = "office"
	s.cash = 48_600
	s.traction = 124
	s.product = 62
	s.morale = 58
	s.founder_name = "Lena Voss"
	s.company_name = "Mossflow"
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	s.set_flag("launched")
	s.rivals = _rivals()
	s.investors = [{"name": "Hazel Fund"}, {"name": "Bright & Slow"}]
	s.raise_state = {"interest_score": 30.0}
	s.content_equity = 61.7
	s.tech_debt = 57.2
	s.employees = [
		{"name": "Ravi", "role": "engineer", "salary": 900},
		{"name": "Mona", "role": "designer", "salary": 800},
		{"name": "Petra", "role": "sales", "salary": 850},
	]
	s.leads = [
		{"name": "Hotel Lys", "stage": "meeting", "seats": 6, "age_weeks": 2, "heat": 60},
		{"name": "Ferry Books", "stage": "pilot", "seats": 12, "age_weeks": 4, "heat": 82},
		{"name": "Studio Nord", "stage": "meeting", "seats": 4, "age_weeks": 1, "heat": 35},
	]
	s.bets = [{"id": "b1", "name": "the sync engine"}, {"id": "b2", "name": "the api"}]
	s.offers = [{"name": "pocket synth", "unit": "per unit", "fair_price": 20.0,
		"elasticity": 2.4, "unit_cost": 9.0, "price": 18.0, "price_set": true,
		"weight": 1.0}]
	return s

## The live buyout letter THE OFFER decomposes.
func _with_offer(s: GameState, countered := false) -> void:
	s.buyout_offer = {
		"buyer": "Corvex Systems", "headline": 1_200_000,
		"cash": 400_000, "stock": 500_000, "earnout": 300_000,
		"lockup_wks": 48, "retention_wks": 96, "expires_wk": s.week + 3,
		"fishy_flags": ["the earnout's targets are set by their CFO after closing"],
		"earnout_controller": "buyer", "retention_carve": false,
		"countered": countered,
	}

## Yesterday's paper: what the seen store believed at the last open — the live
## state above differs on both keys, so the pen circles both.
func _seed_seen(seed_v: int) -> void:
	var f := FileAccess.open("user://binder_seen_%d.json" % seed_v, FileAccess.WRITE)
	if f != null:
		f.store_string(JSON.stringify({
			"the street/act:Vanta Systems": "wk20: laid low",
			"the street/act:Fernbay Group": "wk30: poached a designer",
			"the street/mood": "quiet — nobody is dialing yet",
		}))
		f.close()

# ═════════════════════════════════ the run ═══════════════════════════════════

func _go() -> void:
	await process_frame
	var d := OS.get_environment("RUNWAY_STRESS_DIR")
	if d != "":
		_dir = d

	await _street()
	await _threats()
	await _pivot()
	await _offer()

	print("UX COMPANY2 SHOTS: %d captures -> %s" % [_shots.size(), _dir])
	quit(0)

# ── the street — S1 zero, week 1, S5 circles, S2 red, S7 drill crumb ─────────
func _street() -> void:
	# S1 — a world with nobody in it (rivals cleared): the designed zero state
	var s0 := _w1(71)
	s0.rivals = []
	var b0 := await _open(s0)
	_page(b0, "the street")
	await _shot("street_zero")

	# week 1 as shipped: seeded rivals, quiet logs, mood "quiet"
	var s1 := _w1(76)
	var b1 := await _open(s1)
	_page(b1, "the street")
	await _shot("street_week1")

	# S5 — the pen circles: last-open file pre-seeded, both keys moved since
	_seed_seen(72)
	var s2 := _live(72)
	var b2 := await _open(s2)
	_page(b2, "the street")
	await _shot("street_live_circles")

	# S7 — the rivals drill wears its breadcrumb
	_page(b2, "the street", {"mode": "rivals"})
	await _shot("street_drill_crumb")

	# S2 — a red street: the beat alert rides the strip, the wire is marked
	var s3 := _live(73)
	s3.set_meta("street_alert", "Vanta Systems cut prices under you")
	s3.set_meta("street_beats", ["Vanta Systems cut prices under you"])
	var b3 := await _open(s3)
	_page(b3, "the street")
	await _shot("street_red_strip")

# ── threats — S1 zero, live list with ages + DO lane ─────────────────────────
func _threats() -> void:
	# S1 — a fully quiet company
	var s0 := GameState.new()
	s0.sim_seed = 61
	s0.week = 2
	s0.founder_name = "Lena Voss"
	s0.company_name = "Mossflow"
	var b0 := await _open(s0)
	_page(b0, "threats")
	await _shot("threats_zero")

	# live: two rows, one 3 weeks old (the age chip), the DO lane's fix-first
	var s1 := _live(62)
	s1.offers[0]["price"] = 0.0
	s1.offers[0].erase("price_set")
	s1.set_meta("pnl", {"revenue": 3_420, "net": -13_624, "burn": 16_715})
	s1.attention_ages = {"pricing/unpriced": 31, "the ledger/losing_week": 34}
	var b1 := await _open(s1)
	_page(b1, "threats")
	await _shot("threats_live_ages_do")

# ── pivot — week 1 doors, KEEP/DIES both doors, a receipt open, armed red ────
func _pivot() -> void:
	# week 1: the doors page IS the designed first state (never bare)
	var s0 := _w1(74)
	var b0 := await _open(s0)
	_page(b0, "pivot")
	await _shot("pivot_week1")

	# the two-column ledger, audience door
	var s1 := _live(64)
	var b1 := await _open(s1)
	_page(b1, "pivot", {"mode": "audience", "chip": "Enterprise"})
	await _shot("pivot_keep_dies_audience")

	# the two-column ledger, product door
	_page(b1, "pivot", {"mode": "product"})
	await _shot("pivot_keep_dies_product")

	# S4 — press a number: the first receipt hit in the preview (190×24 is
	# the ledger receipts' unique hit size) opens its source popover
	var hit := _find_receipt_hit(b1)
	if hit != null:
		hit.emit_signal("pressed")
		await _shot("pivot_receipt_open")
	else:
		print("WARN no receipt hit found — pivot_receipt_open skipped")

	# S2 — armed: the red hero + the strip + the marked disarm
	var s2 := _live(65)
	SimPivot.arm_audience(s2, "Enterprise")
	var b2 := await _open(s2)
	_page(b2, "pivot")
	await _shot("pivot_armed_strip")

## The ledger's receipt hit-zones are the pane's only 190×24 buttons.
func _find_receipt_hit(b: Binder) -> Button:
	var best: Button = null
	for c in b.pane().get_children():
		if c is Button and (c as Button).text == "":
			var sz: Vector2 = (c as Control).size
			if absf(sz.x - 190.0) < 1.0 and absf(sz.y - 24.0) < 1.0:
				if best == null or (c as Control).position.y < best.position.y \
						or ((c as Control).position.y == best.position.y \
						and (c as Control).position.x < best.position.x):
					best = c as Button
	return best

# ── the offer — S1 letter-gone, live letter (strip + DO lane), counter spent ─
func _offer() -> void:
	# S1 — the letter left the table. With no live buyout the gold tab is in
	# no group, so open_page refuses the id — the probe sets the page seam
	# directly (the state is defensive: buyout emptied under an open sheet).
	var s0 := _live(66)
	var b0 := await _open(s0)
	b0.desk.clear()
	b0._page = "the offer"
	b0.refresh()
	await _shot("offer_zero")

	# the live letter: gold tab + clock, the strip, the three-answer DO lane
	var s1 := _live(67)
	_with_offer(s1)
	var b1 := await _open(s1)
	_page(b1, "the offer")
	await _shot("offer_live_strip_do")

	# the spent counter, said plainly — the lane drops to two answers
	var s2 := _live(68)
	_with_offer(s2, true)
	var b2 := await _open(s2)
	_page(b2, "the offer")
	await _shot("offer_counter_spent")
