extends SceneTree
## B-COSTS1 UX SHOTS — the three COSTS desks this lane owns (spend · team ·
## recruitment), photographed in the DAG3 Wave-B states: the week-1 zero
## state (S1), the live page with the DO lane (S3), one receipt popover open
## (S4), the red page with the ask strip clear of the sheet (S2), plus the
## spend two-open delta (S5 circles + hero arrow) and the recruit composer
## after a stepper press (the odds ticket's animated end state).
##
## Run: RUNWAY_STRESS_DIR=<dir> godot --headless --path game --script tests/ux_costs1.gd
## Files land as <surface>_godot.png. The probe wants raw pages:
## tour_enabled=false (the UI spine's probe seam).

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
## state dies with the node, and the seen store flushes on exit — which is
## exactly what the S5 two-open shot needs.
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

## Press the invisible receipt hit registered over a marked control: find the
## empty-text Button parked exactly on the control's own corner and fire it.
func _press_receipt(b: Binder, id: String) -> bool:
	if not b.has_control(id):
		print("NO CONTROL %s" % id)
		return false
	var r: Rect2 = b.control_rect(id)
	for c in b.pane().get_children():
		if c is Button and (c as Button).text == "" \
				and ((c as Button).position - r.position).length() < 3.0:
			(c as Button).pressed.emit()
			return true
	print("NO HIT BUTTON AT %s" % str(r.position))
	return false

# ═══════════════════════════════ fixtures ════════════════════════════════════

## The bones every scenario shares. Week 1 at the garage IS the zero state.
func _base(week: int = 34, era: String = "office") -> GameState:
	var s := GameState.new()
	s.sim_seed = 77
	s.week = week
	s.era = era
	s.cash = 21_400
	s.traction = 62
	s.product = 55
	s.morale = 58
	s.hype = 30
	s.founder_name = "Lena Voss"
	s.company_name = "Mossflow"
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	s.set_flag("launched")
	for w in range(1, week):
		s.metric_history.append({"wk": w, "cash": 30_000 - w * 240,
			"customers": w * 2, "morale": mini(40 + w, 100), "revenue": 30 * w,
			"burn": 700 + 20 * w, "hype": 20, "net": 30 * w - (700 + 20 * w)})
	return s

## A working org book: one adopted line, two pending suggestions, one live
## line with no suggestion — the adopt arms, the whole-book arm, the DO lane
## and the S15 suggestion all have something to say.
func _spend_live() -> GameState:
	var s := _base()
	s.spend_book = [
		{"name": "field sales", "buys": "closing what is already in the pipe",
			"amt": 600, "bucket": "sales", "contract_notice": 0, "division": "", "live": 300},
		{"name": "support desk", "buys": "keeping the customers we have",
			"amt": 500, "bucket": "care", "contract_notice": 0, "division": "", "live": 500},
		{"name": "contract dev", "buys": "building the thing faster",
			"amt": 900, "bucket": "rnd", "contract_notice": 4, "division": "", "live": 0},
		{"name": "the room", "buys": "the room and the people in it",
			"amt": 0, "bucket": "office", "contract_notice": 0, "division": "", "live": 200},
	]
	SimSpendBook.reconcile(s)
	return s

## A losing week: the spine's own sev-2 row lands on this desk ("the ledger"
## aliases onto spend) and the strip must clear the sheet by 8px.
func _spend_red() -> GameState:
	var s := _spend_live()
	s.set_meta("pnl", {"revenue": 400, "burn": 2_900, "net": -2_500})
	return s

## A small roster: a vesting grant (the cap-table jump), a pipeline hire, an
## open seat, applicants waiting — every note-column state on one sheet.
func _team_live() -> GameState:
	var s := _base()
	s.employees = [
		{"name": "June Park", "role": "engineer", "salary": 300, "burnout": 20,
			"quirk": "ships at night", "skill": 4},
		{"name": "Milo Renner", "role": "sales", "salary": 260, "burnout": 35,
			"quirk": "knows every café owner", "skill": 3},
		{"name": "Sofia Brandt", "role": "support", "salary": 210, "burnout": 15,
			"quirk": "answers before the phone rings", "skill": 3},
	]
	s.esop = {"pool_pct": 10.0, "granted": [
		{"emp_id": "June Park", "pct": 1.2, "vest_start_wk": -26}]}
	s.pipeline = [{"name": "Ravi Iyer", "role": "support", "salary": 210, "weeks_in": 0}]
	s.open_roles = [{"role": "designer", "offered_salary": 260, "opened_week": 30}]
	s.applicants = [{"name": "Nora Lindt", "role": "designer"},
		{"name": "Pavel Ostrov", "role": "designer"}]
	return s

## Two askers, one overdue: wants_raise (sev 2) + quit_risk (sev 3) both ride
## the strip; the arms wear the marks the red rows land on.
func _team_red() -> GameState:
	var s := _team_live()
	(s.employees[1] as Dictionary)["wants_raise"] = true
	(s.employees[1] as Dictionary)["asked_week"] = s.week
	(s.employees[2] as Dictionary)["wants_raise"] = true
	(s.employees[2] as Dictionary)["asked_week"] = s.week - 3
	return s

## A seat advertised, one candidate interviewed (the composer opens on her),
## one applied — the DO lane carries [send the offer — Priya].
func _recruit_live() -> GameState:
	var s := _base()
	s.esop = {"pool_pct": 10.0, "granted": []}
	s.recruitment = {
		"roles": [{"id": "role_engineer_30", "seat": "engineer", "band_lo": 240,
			"band_hi": 320, "advert_wk": 40, "opened_wk": 30}],
		"candidates": [
			{"id": "c1", "name": "Priya Sharma", "stage": "interviewed", "ask": 280,
				"profile": "mercenary", "role_id": "role_engineer_30"},
			{"id": "c2", "name": "Tomas Vidal", "stage": "applied", "ask": 250,
				"profile": "missionary", "role_id": "role_engineer_30"},
		],
		"offers_out": [],
	}
	return s

## An offer out and expiring: the ownership lane's sev-2 row lands here; the
## clock chip and the offer card carry the same countdown.
func _recruit_red() -> GameState:
	var s := _recruit_live()
	(s.recruitment["candidates"] as Array).append({"id": "c3", "name": "Ana Ruiz",
		"stage": "offer", "ask": 300, "profile": "missionary", "role_id": "role_engineer_30"})
	s.recruitment["offers_out"] = [{"candidate_id": "c3", "cash_wk": 300,
		"options_pct": 0.5, "expires_wk": s.week + 1}]
	return s

# ═════════════════════════════════ the run ═══════════════════════════════════

func _go() -> void:
	await process_frame
	var d := OS.get_environment("RUNWAY_STRESS_DIR")
	if d != "":
		_dir = d
	# the seen store persists per seed across runs — a stale file would eat
	# the S5 shot, so the probe starts clean
	DirAccess.remove_absolute(OS.get_user_data_dir().path_join("binder_seen_77.json"))

	await _zero_states()
	await _spend()
	await _team()
	await _recruit()

	print("B-COSTS1 UX SHOTS: %d captures -> %s" % [_shots.size(), _dir])
	quit(0)

# ── S1 · week 1: every tab of this lane opens on a designed teaching state ───
func _zero_states() -> void:
	var s := _base(1, "garage")
	s.metric_history.clear()
	var b := await _open(s)
	_page(b, "spend")
	await _shot("costs1_spend_zero")
	_page(b, "team")
	await _shot("costs1_team_zero")
	_page(b, "recruitment")
	await _shot("costs1_recruit_zero")

# ── spend: DO lane · subtotal receipt with the marginal · red · the delta ────
func _spend() -> void:
	var s := _spend_live()
	var b := await _open(s)
	_page(b, "spend")
	await _shot("costs1_spend_live_do_lane")
	if _press_receipt(b, "sub_sales"):
		await _shot("costs1_spend_receipt_marginal")
		b.refresh()
	# the two-open delta: leave (flush), adopt a line, come back — the pen
	# circles the moved rows and the hero wears its arrow
	SimSpendBook.adopt_line(s, 0)
	var b2 := await _open(s)
	_page(b2, "spend")
	await _shot("costs1_spend_delta_circles")

	var sr := _spend_red()
	var br := await _open(sr)
	_page(br, "spend")
	await _shot("costs1_spend_red_strip")

# ── team: DO lane + vesting bar · payroll receipt · red askers ───────────────
func _team() -> void:
	var s := _team_live()
	var b := await _open(s)
	_page(b, "team")
	await _shot("costs1_team_live_do_lane")
	if _press_receipt(b, "payroll_total"):
		await _shot("costs1_team_receipt_payroll")
		b.refresh()

	var sr := _team_red()
	var br := await _open(sr)
	_page(br, "team")
	await _shot("costs1_team_red_strip")

# ── recruitment: DO lane + composer · hero receipt · red offer · the tick ────
func _recruit() -> void:
	var s := _recruit_live()
	var b := await _open(s)
	_page(b, "recruitment")
	await _shot("costs1_recruit_live_do_lane")
	if _press_receipt(b, "pipeline_hero"):
		await _shot("costs1_recruit_receipt_pipeline")
		b.refresh()
	# the stepper beat: raise the cash mix — the odds ticket re-inks and the
	# number ticks to its new value (the shot holds the settled end state)
	_page(b, "recruitment", {"cand": "c1", "cash": 340})
	await create_timer(0.35).timeout
	await _shot("costs1_recruit_composer_stepped")

	var sr := _recruit_red()
	var br := await _open(sr)
	_page(br, "recruitment")
	await _shot("costs1_recruit_red_strip")
