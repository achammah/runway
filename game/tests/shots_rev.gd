extends SceneTree
## REV DESK SHOTS — the four REVENUE desks (L-REV), every audience variant and
## every fold state that renders differently: offers = THE RATE CARD (SMB /
## Consumer units-wk / Enterprise named-accounts / the war line / the fold),
## customers = THE SCOREBOARD (cohort bars / biggest-5 / logo grid / the fog),
## in motion = THE TRIPTYCH (river×sources / hot list / stage board / slim /
## focused column), growth = THE MARKET GARDEN (verdict flips by audience /
## the era ceiling pinned).
##
## Run: RUNWAY_STRESS_DIR=<dir> godot --headless --path game --script tests/shots_rev.gd
## Files land as <surface>_godot.png so the Unity twin can sit beside them.
## The probe wants raw pages: tour_enabled=false (the UI spine's probe seam).

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

## Point the binder at a desk by its NEW name, seed desk-local state, rebuild.
func _page(b: Binder, id: String, st: Dictionary = {}) -> void:
	b.open_page(id)
	for k in st:
		b.desk[k] = st[k]
	if not st.is_empty():
		b.refresh()

# ═══════════════════════════════ fixtures ════════════════════════════════════

## A live company at the office era — the base every scenario bends.
func _mid(era := "office", who := "SMB") -> GameState:
	var s := GameState.new()
	s.sim_seed = 99
	s.week = 34
	s.era = era
	s.cash = 48_600
	s.traction = 124
	s.product = 62
	s.morale = 58
	s.hype = 44
	s.founder_name = "Lena Voss"
	s.company_name = "Mossflow"
	s.biz_what = "Software"
	s.biz_who = who
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	s.analytics_level = 3
	s.set_flag("launched")
	s.price_mult = 1.0
	s.budgets = {"ads": 2000, "content": 500, "referrals": 250, "outbound": 1000,
		"sales": 1000, "care": 500, "rnd": 2000, "office": 500}
	s.content_equity = 0.62
	s.offers = [
		{"name": "pocket synth", "unit": "per unit", "fair_price": 20.0,
			"elasticity": 2.4, "unit_cost": 9.0, "price": 18.0, "price_set": true,
			"weight": 1.0, "fixed_wk": 40.0,
			"cost_lines": [{"label": "components", "amount": 6.0},
				{"label": "packing + delivery", "amount": 3.0}],
			"fixed_lines": [{"label": "bench rental", "amount": 40.0}]},
		{"name": "calibration kit", "unit": "per kit", "fair_price": 60.0,
			"elasticity": 1.8, "unit_cost": 22.0, "price": 0.0, "weight": 0.6,
			"fixed_wk": 15.0,
			"cost_lines": [{"label": "reference cell", "amount": 14.0},
				{"label": "courier", "amount": 8.0}],
			"fixed_lines": [{"label": "calibration lab", "amount": 15.0}]},
		{"name": "support retainer", "unit": "per month", "fair_price": 240.0,
			"elasticity": 1.2, "unit_cost": 90.0, "price": 260.0, "price_set": true,
			"weight": 0.4, "fixed_wk": 60.0,
			"cost_lines": [{"label": "on-call hours", "amount": 90.0}],
			"fixed_lines": [{"label": "helpdesk seat", "amount": 60.0}]},
	]
	s.beliefs = {"tam": 100_000.0, "lifetime_wk": 40.0}
	s.set_meta("pnl", {"revenue": 3_420, "cogs": 760, "rent": 3_000, "payroll": 3_950,
		"infra": 220, "marketing": 3_750, "sales": 1_000, "care": 500, "rnd": 2_000,
		"office": 500, "burn": 16_715, "net": -13_624, "learning": 0.89})
	s.set_meta("unit_econ", {"arpu": 27.6, "cac": 310, "ltv": 1_104, "payback_wk": 11})
	for w in range(1, 34):
		s.metric_history.append({"wk": w, "cash": 60_000 - w * 400,
			"customers": int(pow(w, 1.5)), "morale": 74 - w, "revenue": 40 * w,
			"burn": 900 + 40 * w, "hype": 20 + (w % 17),
			"net": 40 * w - (900 + 40 * w)})
	return s

## The funnel read-out, whole — every key the four desks read.
func _with_funnel(s: GameState) -> void:
	s.set_meta("funnel", {"wk": float(s.week), "reach_total": 1_240.0, "leads_total": 96.0,
		"adds": 11.0, "conv": 0.077, "close_rate": 0.78, "gtm_cap": 14.0,
		"blended_cac": 310.0, "demand": 14.1, "equity": s.content_equity,
		"equity_before": s.content_equity, "ref_gain": 0.4, "happy": 0.4,
		"organic": 2.0, "wom": 3.0,
		"spend_ads": 2_000.0, "reach_ads": 620.0, "leads_ads": 48.0, "signed_ads": 3.4, "cac_ads": 588.0,
		"spend_content": 500.0, "reach_content": 400.0, "leads_content": 31.0, "signed_content": 1.8, "cac_content": 278.0,
		"spend_referrals": 250.0, "reach_referrals": 0.0, "leads_referrals": 0.0, "signed_referrals": 0.5, "cac_referrals": 500.0,
		"spend_outbound": 1_000.0, "reach_outbound": 220.0, "leads_outbound": 17.0, "signed_outbound": 0.3, "cac_outbound": 0.0,
		"spend_total": 3_750.0, "ob_closers": 1.7, "era_eff": 1.0, "team_mult": 1.0,
		"price_demand": 1.0, "launched": 1.0})
	var prev := (s.get_meta("funnel", {}) as Dictionary).duplicate(true)
	prev["adds"] = 8.0
	prev["wk"] = float(s.week - 1)
	s.set_meta("funnel_prev", prev)

## The river's origin history — the last five snapshots carry the split (the
## coordinator package's shape); the three before stay bare, so the ghost
## rendering is photographed too.
func _with_river(s: GameState) -> void:
	var n := s.metric_history.size()
	for i in range(maxi(n - 5, 0), n):
		var m: Dictionary = s.metric_history[i]
		var wk := int(m.get("wk", 0))
		m["adds"] = 6.0 + float(wk % 5)
		m["adds_chan"] = 3.0 + float(wk % 3)
		m["adds_wom"] = 2.0 + float(wk % 2)
		m["adds_org"] = float(m.get("adds", 0.0)) - float(m.get("adds_chan", 0.0)) \
			- float(m.get("adds_wom", 0.0))

## Named accounts on the board (SMB names arrive with a later engine wave; the
## desk reads them generically — the probe photographs both worlds).
func _leads(n: int) -> Array:
	var names := ["Hotel Lys", "Café Verde", "Ferry Books", "Studio Nord", "Vanta Systems",
		"Meridian Logistics", "Quill Health", "Bexley Foods", "Corvid Freight", "Ashby & Sons",
		"Fernbay Group", "Lattice Dynamics"]
	var stages := ["meeting", "pilot", "procurement", "contract"]
	var out: Array = []
	for i in n:
		out.append({"name": names[i % names.size()], "flavor": "" if i % 3 != 0 else "a family firm with a new CFO",
			"seats": 3 + (i * 7) % 60, "stage": stages[i % 4], "age_weeks": 1 + i % 6,
			"heat": [88, 62, 14, 55, 91, 40, 71, 25][i % 8]})
	return out

# ═════════════════════════════════ the run ═══════════════════════════════════

func _go() -> void:
	await process_frame
	var d := OS.get_environment("RUNWAY_STRESS_DIR")
	if d != "":
		_dir = d

	await _offers()
	await _customers()
	await _in_motion()
	await _growth()

	print("REV DESK SHOTS: %d captures -> %s" % [_shots.size(), _dir])
	quit(0)

# ── offers = THE RATE CARD ───────────────────────────────────────────────────
func _offers() -> void:
	var s := _mid()
	_with_funnel(s)
	var b := await _open(s)
	_page(b, "offers")
	await _shot("offers_rate_card_smb")

	# CONSUMER: units/wk rides every row
	var sc := _mid("office", "Consumer")
	_with_funnel(sc)
	var bc := await _open(sc)
	_page(bc, "offers")
	await _shot("offers_rate_card_consumer")

	# ENTERPRISE: the named-accounts per-seat line under the table
	var se := _mid("office", "Enterprise")
	se.logos = [{"name": "Quill Health", "seats": 12, "since_wk": 24, "renewal_wk": 37},
		{"name": "Fernbay Group", "seats": 9, "since_wk": 20, "renewal_wk": 72}]
	_with_funnel(se)
	var be := await _open(se)
	_page(be, "offers")
	await _shot("offers_rate_card_enterprise")

	# THE PRICE WAR: the street's reference moves and the sheet says so
	var sw := _mid()
	SimEngine.add_status(sw, "price_war", 3)
	_with_funnel(sw)
	var bw := await _open(sw)
	_page(bw, "offers")
	await _shot("offers_rate_card_war")

	# THE FOLD: eight offers — the healthy crowd folds, the unpriced stay up
	var sf := _mid("hq")
	for i in 5:
		sf.offers.append({"name": "service tier %d" % (i + 1), "unit": "per month",
			"fair_price": 100.0 + 40.0 * i, "elasticity": 1.6,
			"unit_cost": 30.0 + 10.0 * i, "price": 110.0 + 44.0 * i, "price_set": true,
			"weight": 0.4, "fixed_wk": 20.0})
	_with_funnel(sf)
	var bf := await _open(sf)
	_page(bf, "offers")
	await _shot("offers_rate_card_fold")
	_page(bf, "offers", {"mode": "all"})
	await _shot("offers_rate_card_all")

# ── customers = THE SCOREBOARD ───────────────────────────────────────────────
func _customers() -> void:
	# CONSUMER: cohort retention bars
	var s := _mid("office", "Consumer")
	_with_funnel(s)
	var b := await _open(s)
	_page(b, "customers")
	await _shot("customers_scoreboard_consumer")

	# SMB: the biggest-5 strip, honestly unnamed today
	var sm := _mid()
	_with_funnel(sm)
	var bm := await _open(sm)
	_page(bm, "customers")
	await _shot("customers_scoreboard_smb")

	# ENTERPRISE: the logo grid with seats + renewal clocks
	var se := _mid("floor", "Enterprise")
	se.logos = [{"name": "Quill Health", "seats": 12, "since_wk": 24, "renewal_wk": 37},
		{"name": "Fernbay Group", "seats": 9, "since_wk": 20, "renewal_wk": 72},
		{"name": "Meridian Logistics", "seats": 40, "since_wk": 28, "renewal_wk": 36},
		{"name": "Corvid Freight", "seats": 22, "since_wk": 30, "renewal_wk": 60}]
	_with_funnel(se)
	var be := await _open(se)
	_page(be, "customers")
	await _shot("customers_scoreboard_enterprise")

	# THE FOG: analytics 0 — "?" shapes, never absence
	var sf := _mid("garage")
	sf.analytics_level = 0
	var bf := await _open(sf)
	_page(bf, "customers")
	await _shot("customers_scoreboard_fog")

# ── in motion = THE TRIPTYCH ─────────────────────────────────────────────────
func _in_motion() -> void:
	# CONSUMER: the river (5 known weeks + 3 ghosts), sources, taste test
	var s := _mid("office", "Consumer")
	_with_funnel(s)
	_with_river(s)
	var b := await _open(s)
	_page(b, "in motion")
	await _shot("inmotion_consumer_river")
	_page(b, "in motion", {"mode": "sources"})
	await _shot("inmotion_consumer_sources")

	# SMB: the hot list with named accounts on the board
	var sm := _mid()
	sm.leads = _leads(7)
	_with_funnel(sm)
	var bm := await _open(sm)
	_page(bm, "in motion")
	await _shot("inmotion_smb_hotlist")

	# SMB: the crowd alone — no shop worth a dinner yet
	var sq := _mid()
	_with_funnel(sq)
	var bq := await _open(sq)
	_page(bq, "in motion")
	await _shot("inmotion_smb_crowd")

	# ENTERPRISE: the stage board, dying deal in red
	var se := _mid("office", "Enterprise")
	se.leads = [
		{"name": "Vanta Systems", "flavor": "the security team found you first",
			"seats": 6, "stage": "meeting", "age_weeks": 1, "heat": 62},
		{"name": "Ashby & Sons", "flavor": "a family firm with a new CFO",
			"seats": 9, "stage": "meeting", "age_weeks": 5, "heat": 14},
		{"name": "Meridian Logistics", "flavor": "forty depots, one spreadsheet",
			"seats": 40, "stage": "pilot", "age_weeks": 3, "heat": 88},
		{"name": "Corvid Freight", "flavor": "", "seats": 22, "stage": "procurement",
			"age_weeks": 6, "heat": 55},
	]
	se.pipe_units = 12.0
	se.pipe_stats = {"signed": 4, "lost": 7, "cycle_sum": 28, "seats_signed": 21,
		"spend": 6_500.0, "first_wk": 8}
	_with_funnel(se)
	var be := await _open(se)
	_page(be, "in motion")
	await _shot("inmotion_enterprise_board")
	_page(be, "in motion", {"mode": "col:meeting"})
	await _shot("inmotion_enterprise_column")

	# ENTERPRISE, CROWDED: past ~8 live deals the cards go slim
	var sl := _mid("floor", "Enterprise")
	sl.leads = _leads(10)
	sl.pipe_stats = {"signed": 9, "lost": 6, "cycle_sum": 45, "seats_signed": 88,
		"spend": 22_000.0, "first_wk": 8}
	_with_funnel(sl)
	var bl := await _open(sl)
	_page(bl, "in motion")
	await _shot("inmotion_enterprise_slim")

# ── growth = THE MARKET GARDEN ───────────────────────────────────────────────
func _growth() -> void:
	# SMB: the balanced world
	var s := _mid()
	_with_funnel(s)
	var b := await _open(s)
	_page(b, "growth")
	await _shot("growth_garden_smb")

	# CONSUMER: outbound reads "nobody answers a cold call" — from the data
	var sc := _mid("office", "Consumer")
	_with_funnel(sc)
	var bc := await _open(sc)
	_page(bc, "growth")
	await _shot("growth_garden_consumer")

	# ENTERPRISE: ads read "a drop in the ocean" — from the data
	var se := _mid("office", "Enterprise")
	_with_funnel(se)
	var be := await _open(se)
	_page(be, "growth")
	await _shot("growth_garden_enterprise")

	# THE CEILING: a garage mix pinned at the era cap, the meter full
	var sg := _mid("garage")
	sg.budgets = {"ads": 4_000, "content": 1_000, "referrals": 500, "outbound": 500,
		"sales": 250, "care": 250, "rnd": 250, "office": 250}
	var bg := await _open(sg)
	_page(bg, "growth")
	await _shot("growth_garden_ceiling")

	# GENERATED TOPICS: the world's own vocabulary dresses the plots
	var st := _mid()
	st.topics = {"growth": {
		"ads": {"name": "the flyer runs", "line": "posters up tonight, gone by rain — reach while the ink is wet"},
		"content": {"name": "the recipe book", "line": "every massage guide written keeps booking while you sleep"},
		"referrals": {"name": "the regulars' word", "line": "happy backs tell other backs — only if the work is good"},
		"outbound": {"name": "the hotel calls", "line": "ring every concierge on the list — costly, certain, slow"}}}
	_with_funnel(st)
	var bt := await _open(st)
	_page(bt, "growth")
	await _shot("growth_garden_topics")
