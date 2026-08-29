extends SceneTree
## R10 — THE GRADE GATE (docs/design/14-quiet.md): every desk photographed in
## ONE busy fixture (red + changes + suggestions all at once) and graded on
## the quiet law's two counts. A pane FAILS when it carries more than THREE
## attention annotations, or more than ONE alert-red text object.
##
## THE COUNTING RULE (kit-owned, DeskKit.annotation_count / alert_text_count):
##   annotations = the arbiter's rendered marks — the ask strip, the hero
##   delta, the gutter dots — every one tagged `quiet_mark` at draw time.
##   ALERT text = Labels/Buttons whose font_color is the alarm red.
##   Row clock chips are EXEMPT by construction: they are drawn chips (a red
##   polygon under WHITE words), not text labels colored ALERT — they count
##   as data, not commentary.
##
## Run (WINDOWED — headless captures come back empty):
##   RUNWAY_STRESS_DIR=<dir> godot --path game --script tests/quiet_gate.gd
## Parse-only: RUNWAY_PARSE_ONLY=1 godot --headless --path game --script
##   tests/quiet_gate.gd — loads the kit + all 19 desk scripts, quits 0/1.

const DESKS := ["offers", "customers", "in motion", "growth", "spend", "team",
	"recruitment", "bills", "the bank", "the works", "what we make", "cap table",
	"the raise", "the street", "threats", "pivot", "this week", "history", "events"]

const MAX_ANNOTATIONS := 3   ## R2 — the annotation budget
const MAX_ALERT_TEXT := 1    ## R6 — the red singleton

var _dir := "/tmp"
var _b: Binder = null
var _failed := false

func _init() -> void:
	call_deferred("_go")

# ═══════════════════════════ the busy fixture ════════════════════════════════

## One company where everything is happening at once: an unpriced offer, a
## losing week, term sheets on the table, a missed Monday, demand overflowing
## capacity, rivals acting, leads warming — every red the registry can raise,
## plus a seeded seen-store so the delta layer fires on every desk.
func _busy() -> GameState:
	var s := GameState.new()
	s.sim_seed = 77
	s.week = 30
	s.era = "office"
	s.cash = 9_500
	s.traction = 150
	s.product = 55
	s.morale = 48
	s.hype = 40
	s.founder_name = "Lena Voss"
	s.company_name = "Mossflow"
	s.biz_what = "Service"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	s.analytics_level = 3
	s.set_flag("launched")
	s.budgets = {"ads": 1200, "content": 400, "referrals": 200, "outbound": 600,
		"sales": 600, "care": 300, "rnd": 800, "office": 200}
	s.offers = [
		{"name": "deep-tissue hour", "unit": "per session", "fair_price": 60.0,
			"elasticity": 1.8, "unit_cost": 14.0, "price": 62.0, "price_set": true,
			"weight": 1.0, "fixed_wk": 40.0,
			"cost_lines": [{"label": "oils + linen", "amount": 6.0},
				{"label": "room hour", "amount": 8.0}]},
		{"name": "posture audit", "unit": "per visit", "fair_price": 120.0,
			"elasticity": 1.5, "unit_cost": 30.0, "price": 0.0,
			"weight": 0.4, "fixed_wk": 10.0,
			"cost_lines": [{"label": "travel + kit", "amount": 30.0}]},
	]
	s.employees = [
		{"name": "Ravi Chand", "role": "operations", "skill": 4, "salary": 640,
			"burnout": 30, "site": ""},
		{"name": "Mina Sorel", "role": "care", "skill": 3, "salary": 540,
			"burnout": 15, "site": ""},
	]
	s.open_roles = [{"role": "operations", "offered_salary": 560,
		"opened_week": 28, "seats": 1}]
	s.loans = [
		{"kind": "bank", "principal": 12_000, "balance": 9_900, "rate_wk": 0.061,
			"pay_wk": 640, "term_wk": 16, "taken_week": 18, "missed": 2},
	]
	s.leads = [
		{"name": "Café Verde", "flavor": "a six-chair espresso bar", "seats": 6,
			"stage": "warm", "age_weeks": 2, "heat": 0.8},
		{"name": "Nordbahn Gym", "flavor": "a boutique gym", "seats": 4,
			"stage": "cooling", "age_weeks": 5, "heat": 0.3},
	]
	s.rivals = [
		{"name": "KnotWorks", "strength": 64.0, "vigor": 70.0, "price_posture": 0.9,
			"focus": "price", "hype": 40.0,
			"log": ["wk 27: cut prices 10%", "wk 29: poached your therapist"]},
		{"name": "EasePoint", "strength": 38.0, "vigor": 45.0, "price_posture": 1.1,
			"focus": "growth", "hype": 25.0, "log": ["wk 28: opened a second room"]},
	]
	s.investors = [
		{"name": "Bo Lindqvist", "archetype": "operator angel", "thesis": "services",
			"bond": 40, "flaw": "slow", "coords": "Malmö", "secret": ""},
	]
	s.set_flag("fundraising_open")
	for w in range(1, 30):
		s.metric_history.append({"wk": w, "cash": 26_000 - w * 500,
			"customers": 5 * w, "morale": 70 - (w % 9), "revenue": 130 * w,
			"burn": 3_000 + 60 * w, "hype": 20 + (w % 11), "net": 130 * w - 3_000})
	# a losing week on the books — "the ledger" red lands on the spend desk
	s.set_meta("pnl", {"revenue": 3_100, "cogs": 850, "rent": 600, "payroll": 1_180,
		"infra": 120, "marketing": 900, "sales": 300, "care": 250, "rnd": 400,
		"office": 150, "burn": 4_750, "net": -1_650, "tax": 0, "interest": 604,
		"learning": 0.93})
	# two real ticks fill the lane metas (funnel, works book, ages) …
	SimEngine.weekly_tick(s)
	SimEngine.weekly_tick(s)
	# … then the red conditions are re-asserted (a tick pays and expires)
	s.set_flag("fundraising_open")
	if s.loans.is_empty():
		s.loans = [{"kind": "bank", "principal": 12_000, "balance": 9_900,
			"rate_wk": 0.061, "pay_wk": 640, "term_wk": 16, "taken_week": 18,
			"missed": 2}]
	else:
		(s.loans[0] as Dictionary)["missed"] = 2
	s.traction = maxi(s.traction, 150)
	s.cash = maxi(s.cash, 2_000)
	var pnl: Dictionary = s.get_meta("pnl", {})
	if int(pnl.get("net", 0)) >= 0:
		pnl["net"] = -1_650
		s.set_meta("pnl", pnl)
	return s

## LAST-OPEN values that differ from the busy state's own, so the delta layer
## (arrows + gutter dots) fires on every desk that keeps one (ux_rev's seam).
func _seed_seen(s: GameState) -> void:
	var entries := {
		"offers/hero": "5", "offers/vd_deep-tissue hour": "a deal",
		"offers/vd_posture audit": "fair",
		"customers/won": "1", "customers/lost": "0",
		"in motion/hero": "1", "growth/hero": "1",
		"spend/book_total": "900",
		"bills/total": "700", "bills/eats_ratio": "0.40",
		"the bank/debt": "4000", "the works/served": "12",
		"what we make/live": "1", "cap table/pct": "92.0",
		"the raise/offers": "0",
		"the street/mood": "yesterday's word", "the street/act:KnotWorks": "old",
		"the street/act:EasePoint": "old",
		"this week/band": "yesterday's band", "events/unread": "9",
	}
	var f := FileAccess.open("user://binder_seen_%d.json" % s.sim_seed, FileAccess.WRITE)
	if f != null:
		f.store_string(JSON.stringify(entries))
		f.close()

# ═══════════════════════════════ the run ═════════════════════════════════════

func _go() -> void:
	await process_frame
	if OS.get_environment("RUNWAY_PARSE_ONLY") != "":
		_parse_only()
		return
	var d := OS.get_environment("RUNWAY_STRESS_DIR")
	if d != "":
		_dir = d
	# UNATTENDED WINDOWED RUN (the B-REV hardening): macOS suspends rendering
	# for occluded windows and frame_post_draw never fires — keep the window
	# on top and fronted so every await returns.
	DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_ALWAYS_ON_TOP, true)
	DisplayServer.window_move_to_foreground()

	var s := _busy()
	_seed_seen(s)
	var b := Binder.new()
	b.tour_enabled = false
	b.setup(s, null)
	root.add_child(b)
	b.size = Vector2(1536, 1024)
	await create_timer(0.35).timeout
	_b = b

	print("GRADE GATE — desk × annotations (≤%d) × alert text (≤%d)"
		% [MAX_ANNOTATIONS, MAX_ALERT_TEXT])
	for id in DESKS:
		b.open_page(String(id))
		await create_timer(0.25).timeout
		await RenderingServer.frame_post_draw
		var ann := DeskKit.annotation_count(b)
		var reds := DeskKit.alert_text_count(b)
		var ok := ann <= MAX_ANNOTATIONS and reds <= MAX_ALERT_TEXT
		if not ok:
			_failed = true
		var nm := "quiet_" + String(id).replace(" ", "_")
		root.get_viewport().get_texture().get_image().save_png(
			"%s/%s_godot.png" % [_dir, nm])
		print("GRADE %-14s ann=%d red=%d %s" % [String(id), ann, reds,
			"PASS" if ok else "FAIL"])
	# THE OPEN OFFER is its own pane state (owner: the first open-row ship
	# read as clutter and the gate never photographed it) — grade it too
	b.open_page("offers")
	b.desk["open_row"] = 0
	b.refresh()
	await create_timer(0.25).timeout
	await RenderingServer.frame_post_draw
	var ann_o := DeskKit.annotation_count(b)
	var reds_o := DeskKit.alert_text_count(b)
	var ok_o := ann_o <= MAX_ANNOTATIONS and reds_o <= MAX_ALERT_TEXT
	if not ok_o:
		_failed = true
	root.get_viewport().get_texture().get_image().save_png(
		"%s/quiet_offers_open_godot.png" % _dir)
	print("GRADE %-14s ann=%d red=%d %s" % ["offers OPEN", ann_o, reds_o,
		"PASS" if ok_o else "FAIL"])
	print("QUIET GATE %s" % ("FAIL" if _failed else "PASS — 20/20"))
	quit(1 if _failed else 0)

## The parse gate: the kit and every desk this gate grades.
func _parse_only() -> void:
	var ok := true
	var paths := ["res://src/ui/components.gd", "res://src/ui/binder.gd"]
	for id in DESKS:
		var f := String(id).replace(" ", "_")
		match String(id):
			"customers": f = "customers_page"
			"recruitment": f = "recruit"
			"the bank": f = "bank_page"
			"what we make": f = "make"
			"cap table": f = "cap_page"
			"the raise": f = "raise"
			"the street": f = "street_page"
			"threats": f = "threats_page"
			"in motion": f = "in_motion"
			"the works": f = "works"
			"this week": f = "this_week"
		paths.append("res://src/ui/desks/desk_%s.gd" % f)
	for p in paths:
		var scr: Variant = load(String(p))
		if scr == null or not (scr as Script).can_instantiate():
			push_error("PARSE FAIL %s" % String(p))
			ok = false
		else:
			print("PARSE OK %s" % String(p))
	print("PARSE GATE %s" % ("PASS" if ok else "FAIL"))
	quit(0 if ok else 1)
