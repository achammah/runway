extends SceneTree
## B-REV UX PROBE (DAG3 Wave B) — the four REVENUE desks wearing the nine
## systems: per tab a week-1 S1 zero state, a live state with the DO lane, an
## opened press-receipt (the drawn fair band / curve cards, the cohort
## popover), and the red ask-strip state. Plus THE PREFILL's drafted chip, the
## suggest-mode ADOPT rows, the cap pulse, and the sources→growth focus jump.
##
## Run (WINDOWED — headless captures come back empty):
##   RUNWAY_STRESS_DIR=<dir> godot --path game --script tests/ux_rev.gd
## Files land as <surface>_godot.png. Modeled on shots_rev.gd.

var _dir := "/tmp"
var _b: Binder = null
var _shots: Array[String] = []

# ═══════════════════════════ the shot harness ════════════════════════════════

func _shot(nm: String) -> void:
	await create_timer(0.25).timeout
	await RenderingServer.frame_post_draw
	root.get_viewport().get_texture().get_image().save_png("%s/%s_godot.png" % [_dir, nm])
	_shots.append(nm)
	print("SHOT %s" % nm)

## Free the live binder and WAIT for its exit-flush — a seed written before
## the old binder leaves the tree gets clobbered by its own stale merge.
func _close() -> void:
	if _b != null and is_instance_valid(_b):
		_b.queue_free()
		await process_frame
	_b = null

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

## Seed the S5 seen-store with LAST-OPEN values so arrows and circles draw
## (first sighting is silent by law). Overwrites; re-seed before every shot
## that wants deltas — a freed binder flushes current values over it.
func _seed_seen(seed_v: int, entries: Dictionary) -> void:
	var f := FileAccess.open("user://binder_seen_%d.json" % seed_v, FileAccess.WRITE)
	if f != null:
		f.store_string(JSON.stringify(entries))
		f.close()

func _clear_seen(seed_v: int) -> void:
	if FileAccess.file_exists("user://binder_seen_%d.json" % seed_v):
		DirAccess.remove_absolute(ProjectSettings.globalize_path(
			"user://binder_seen_%d.json" % seed_v))

# ═══════════════════════════════ fixtures ════════════════════════════════════

## A live company at the office era — the shots_rev base, verbatim.
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
			"weight": 1.0, "fixed_wk": 40.0},
		{"name": "calibration kit", "unit": "per kit", "fair_price": 60.0,
			"elasticity": 1.8, "unit_cost": 22.0, "price": 0.0, "weight": 0.6,
			"fixed_wk": 15.0},
		{"name": "support retainer", "unit": "per month", "fair_price": 240.0,
			"elasticity": 1.2, "unit_cost": 90.0, "price": 260.0, "price_set": true,
			"weight": 0.4, "fixed_wk": 60.0},
	]
	s.beliefs = {"tam": 100_000.0, "lifetime_wk": 40.0}
	s.set_meta("pnl", {"revenue": 3_420, "net": -13_624})
	s.set_meta("unit_econ", {"arpu": 27.6, "cac": 310, "ltv": 1_104, "payback_wk": 11})
	for w in range(1, 34):
		s.metric_history.append({"wk": w, "cash": 60_000 - w * 400,
			"customers": int(pow(w, 1.5)), "morale": 74 - w, "revenue": 40 * w,
			"burn": 900 + 40 * w, "hype": 20 + (w % 17),
			"net": 40 * w - (900 + 40 * w)})
	return s

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

## The bare week-one company — every S1 zero state starts here.
func _week1(who := "SMB") -> GameState:
	var s := GameState.new()
	s.sim_seed = 99
	s.week = 1
	s.era = "garage"
	s.cash = 12_000
	s.traction = 0
	s.founder_name = "Lena Voss"
	s.company_name = "Mossflow"
	s.biz_what = "Software"
	s.biz_who = who
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	s.offers = []
	s.budgets = {}
	s.marketing_budget = 0
	return s

func _leads(n: int) -> Array:
	var names := ["Hotel Lys", "Café Verde", "Ferry Books", "Studio Nord", "Vanta Systems",
		"Meridian Logistics", "Quill Health", "Bexley Foods"]
	var stages := ["meeting", "pilot", "procurement", "contract"]
	var out: Array = []
	for i in n:
		out.append({"name": names[i % names.size()],
			"flavor": "" if i % 3 != 0 else "a family firm with a new CFO",
			"seats": 3 + (i * 7) % 60, "stage": stages[i % 4], "age_weeks": 1 + i % 6,
			"heat": [88, 62, 14, 55, 91, 40, 71, 25][i % 8]})
	return out

# ═════════════════════════════════ the run ═══════════════════════════════════

func _go() -> void:
	await process_frame
	# UNATTENDED WINDOWED RUN: macOS suspends rendering for occluded windows
	# and frame_post_draw never fires — keep the window on top and fronted so
	# the capture loop cannot wedge mid-run.
	DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_ALWAYS_ON_TOP, true)
	DisplayServer.window_move_to_foreground()
	var d := OS.get_environment("RUNWAY_STRESS_DIR")
	if d != "":
		_dir = d

	# RUNWAY_UX_ONLY=offers|customers|in_motion|growth reruns one desk's
	# chunk (the unattended-render stall strikes randomly; small chunks let
	# a wedged run be retried without re-shooting the whole set).
	var only := OS.get_environment("RUNWAY_UX_ONLY")
	if only == "" or only == "offers":
		await _offers()
	if only == "" or only == "customers":
		await _customers()
	if only == "" or only == "in_motion":
		await _in_motion()
	if only == "" or only == "growth":
		await _growth()

	print("B-REV UX SHOTS: %d captures -> %s" % [_shots.size(), _dir])
	quit(0)

func _init() -> void:
	call_deferred("_go")

# ── offers ───────────────────────────────────────────────────────────────────
func _offers() -> void:
	# S1 — the empty shelf teaches
	_clear_seen(99)
	var sz := _week1()
	var bz := await _open(sz)
	_page(bz, "offers")
	await _shot("uxrev_offers_zero")

	# live: DO lane + ask strip (unpriced) + S5 circle/arrow off the seeded store
	await _close()
	_seed_seen(99, {"offers/hero": "5", "offers/vd_pocket synth": "fair"})
	var s := _mid()
	_with_funnel(s)
	var b := await _open(s)
	_page(b, "offers")
	await _shot("uxrev_offers_live_do")

	# S4 — the verdict opened: the street's read with THE FAIR BAND drawn
	_seed_seen(99, {})
	var sr := _mid()
	_with_funnel(sr)
	var br := await _open(sr)
	_page(br, "offers", {"mode": "verdict:0"})
	await _shot("uxrev_offers_receipt_band")

	# red: unpriced AND a price below its serve cost — both asks on the strip
	var sred := _mid()
	sred.offers[2]["price"] = 60.0
	_with_funnel(sred)
	var bred := await _open(sred)
	_page(bred, "offers")
	await _shot("uxrev_offers_red_strip")

# ── customers ────────────────────────────────────────────────────────────────
func _customers() -> void:
	_clear_seen(99)
	var sz := _week1()
	var bz := await _open(sz)
	_page(bz, "customers")
	await _shot("uxrev_customers_zero")

	# live: kept% wears the receipt underdot; won/lost wear seeded arrows
	await _close()
	_seed_seen(99, {"customers/won": "8", "customers/lost": "2"})
	var s := _mid()
	_with_funnel(s)
	var b := await _open(s)
	_page(b, "customers")
	await _shot("uxrev_customers_live")

	# S4 — the cohort receipt open (the exact lines the kept% press opens)
	var sr := _mid()
	_with_funnel(sr)
	var br := await _open(sr)
	_page(br, "customers")
	br.popover("kept — a class of 100", DeskCustomersPage._cohort_lines(sr),
		Vector2(700.0, 60.0))
	await _shot("uxrev_customers_receipt_cohort")

	# red: the fog line sells analytics AND the rotting library asks on the
	# strip (content unfunded at equity 0.62 — the engine's own predicate)
	_seed_seen(99, {})
	var sf := _mid()
	sf.analytics_level = 0
	sf.budgets["content"] = 0
	_with_funnel(sf)
	var bf := await _open(sf)
	_page(bf, "customers")
	await _shot("uxrev_customers_red_fog")

# ── in motion ────────────────────────────────────────────────────────────────
func _in_motion() -> void:
	_clear_seen(99)
	var szc := _week1("Consumer")
	var bzc := await _open(szc)
	_page(bzc, "in motion")
	await _shot("uxrev_inmotion_zero_consumer")

	var szs := _week1("SMB")
	var bzs := await _open(szs)
	_page(bzs, "in motion")
	await _shot("uxrev_inmotion_zero_smb")

	var sze := _week1("Enterprise")
	var bze := await _open(sze)
	_page(bze, "in motion")
	await _shot("uxrev_inmotion_zero_ent")

	# SMB live: the hot list with [push — rank 1] in the DO lane
	DeskThisWeek.draft = ""
	var s := _mid()
	s.leads = _leads(7)
	_with_funnel(s)
	var b := await _open(s)
	_page(b, "in motion")
	await _shot("uxrev_inmotion_smb_do")

	# THE PREFILL landed: the draft carries the move, the chip says where
	var rank1: Dictionary = s.leads[int(DeskInMotion._ranked(s)[0])]
	DeskThisWeek.draft = DeskInMotion._move_text(rank1)
	b.desk["drafted"] = String(rank1.get("name", ""))
	b.refresh()
	await _shot("uxrev_inmotion_prefill_chip")

	# Enterprise: per-deal push cards; the cooling deal is the named switch
	DeskThisWeek.draft = ""
	var se := _mid("office", "Enterprise")
	se.leads = [
		{"name": "Vanta Systems", "flavor": "the security team found you first",
			"seats": 6, "stage": "meeting", "age_weeks": 1, "heat": 62},
		{"name": "Ashby & Sons", "flavor": "a family firm with a new CFO",
			"seats": 9, "stage": "meeting", "age_weeks": 5, "heat": 14},
		{"name": "Meridian Logistics", "flavor": "forty depots, one spreadsheet",
			"seats": 40, "stage": "pilot", "age_weeks": 3, "heat": 88},
	]
	se.pipe_stats = {"signed": 4, "lost": 7, "cycle_sum": 28, "seats_signed": 21,
		"spend": 6_500.0, "first_wk": 8}
	_with_funnel(se)
	var be := await _open(se)
	_page(be, "in motion")
	if be.has_control("push_cold"):
		be.spotlight(be.control_rect("push_cold"))
	await _shot("uxrev_inmotion_ent_do_spot")

# ── growth ───────────────────────────────────────────────────────────────────
func _growth() -> void:
	_clear_seen(99)
	var sz := _week1()
	var bz := await _open(sz)
	_page(bz, "growth")
	await _shot("uxrev_growth_zero")

	# live: verdict doors + [balance the mix — suggest] in the DO lane
	await _close()
	_seed_seen(99, {"growth/hero": "4"})
	var s := _mid()
	_with_funnel(s)
	var b := await _open(s)
	_page(b, "growth")
	await _shot("uxrev_growth_live_do")

	# S4 — the verdict opened: the curve drawn, spend dotted, knee ticked
	_seed_seen(99, {})
	var sc := _mid()
	_with_funnel(sc)
	var bc := await _open(sc)
	_page(bc, "growth", {"mode": "curve:ads"})
	await _shot("uxrev_growth_curve_ads")

	# suggest mode: the even-marginal ADOPT rows (nothing applies itself)
	var ss := _mid()
	_with_funnel(ss)
	var bs := await _open(ss)
	_page(bs, "growth", {"mode": "suggest"})
	await _shot("uxrev_growth_suggest_adopt")

	# the ceiling made felt: a refused press pulses the meter
	var sg := _mid("garage")
	sg.budgets = {"ads": 4_000, "content": 1_000, "referrals": 500, "outbound": 500}
	_with_funnel(sg)
	var bg := await _open(sg)
	_page(bg, "growth", {"cap_pulse": true})
	await _shot("uxrev_growth_cap_pulse")

	# S2b/S7 — a consumer source bar's jump: growth focused on the plot,
	# the spotlight on it and the back pill waiting at the rail's foot
	var sj := _mid("office", "Consumer")
	_with_funnel(sj)
	var bj := await _open(sj)
	_page(bj, "in motion")
	bj.focus_desk("growth", "plot_content", "in motion")
	await _shot("uxrev_growth_focus_from_sources")
