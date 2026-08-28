extends SceneTree
## LOG DESK SHOTS (DAG3 Wave B, lane B-LOG) — the three LOG desks across the
## states 13-binder-ux.md names: this week = THE COCKPIT (week-1 zero · live
## with THE WEEK'S CHIPS + DO lane + badge · the outcome view's desk jumps ·
## the pre-roll intercept (this desk's red) · the hero receipt), history =
## THE RUN'S LEDGER (zero · live with sparkline endpoint dots + ★ filed row ·
## the total receipt · the receipts page the ★ presses through to), events =
## THE MAIL (zero · unread action letters with inline DOs · the answered ✓
## pile · the S5 arrow on a second open).
##
## Run: RUNWAY_STRESS_DIR=<dir> godot --headless --path game --script tests/ux_log.gd
## Files land as <surface>_godot.png. Raw pages: tour_enabled=false.
## Also asserts (CHECK lines) the lane's non-visual statics: prefill append,
## unread_action_count, micro_status — the probe fails loudly on a break.

var _dir := "/tmp"
var _b: Binder = null
var _shots: Array[String] = []
var _fails := 0

func _init() -> void:
	call_deferred("_go")

# ═══════════════════════════ the shot harness ════════════════════════════════

func _shot(nm: String) -> void:
	await create_timer(0.25).timeout
	await RenderingServer.frame_post_draw
	root.get_viewport().get_texture().get_image().save_png("%s/%s_godot.png" % [_dir, nm])
	_shots.append(nm)
	print("SHOT %s" % nm)

func _check(cond: bool, msg: String) -> void:
	if cond:
		print("CHECK ok — %s" % msg)
	else:
		_fails += 1
		printerr("CHECK FAIL — %s" % msg)

## Open a fresh binder on a state; freeing the old one flushes its seen-store.
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

## The host-seam statics persist across scenarios — reset them every fixture.
func _reset_seams() -> void:
	DeskThisWeek.week_card = {}
	DeskThisWeek.lock_hook = Callable()
	DeskThisWeek.draft = ""

## The lane's user:// stores are per-seed; wipe them so every run is hermetic.
func _wipe_stores(seed_v: int) -> void:
	for p in ["user://mail_read_%d.json" % seed_v, "user://binder_seen_%d.json" % seed_v]:
		if FileAccess.file_exists(p):
			DirAccess.remove_absolute(p)

# ═══════════════════════════════ fixtures ════════════════════════════════════

## Week 1, first open ever: the zero states. No offers (no attention), no
## letters (no price book), no history — the desks must still teach.
func _zero(seed_v: int) -> GameState:
	var s := GameState.new()
	s.sim_seed = seed_v
	s.week = 1
	s.cash = 25_000
	s.founder_name = "Lena Voss"
	s.company_name = "Mossflow"
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	s.price_book = {}
	return s

## A live office-era company with every record the LOG reads: history rows,
## receipts, letters of every kind, one unpriced offer (a real attention row
## for the badge), a filed buyout, and last week's outcome effects.
func _live(seed_v: int) -> GameState:
	var s := GameState.new()
	s.sim_seed = seed_v
	s.week = 34
	s.era = "office"
	s.cash = 48_600
	s.traction = 124
	s.product = 62
	s.morale = 58
	s.hype = 44
	s.founder_name = "Lena Voss"
	s.company_name = "Mossflow"
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	s.analytics_level = 3
	s.set_flag("launched")
	s.offers = [
		{"name": "pocket synth", "unit": "per unit", "fair_price": 20.0,
			"elasticity": 2.4, "unit_cost": 9.0, "price": 18.0, "price_set": true,
			"weight": 1.0, "fixed_wk": 40.0},
		{"name": "calibration kit", "unit": "per kit", "fair_price": 60.0,
			"elasticity": 1.8, "unit_cost": 22.0, "price": 0.0, "weight": 0.6,
			"fixed_wk": 15.0},
	]
	s.set_meta("pnl", {"revenue": 3_420, "burn": 16_715, "net": -13_295})
	for w in range(1, 34):
		s.metric_history.append({"wk": w, "cash": 60_000 - w * 400,
			"customers": int(pow(w, 1.5)), "revenue": 40 * w, "burn": 900 + 40 * w,
			"net": 40 * w - (900 + 40 * w), "morale": 74 - w, "hype": 20 + (w % 17)})
	# the action log: an era move (history's sections), an event title, and
	# this week's own staged entries (the cockpit's ARMED list)
	s.history = [
		{"week": 9, "entry": "MOVED UP: garage → coworking (the desk got small)"},
		{"week": 21, "entry": "MOVED UP: coworking → office (the lease signed)"},
		{"week": 20, "entry": "event 'the buyout letter' — someone priced the company"},
		{"week": 34, "entry": "adopted the spend book — +$800/wk to closing"},
		{"week": 34, "entry": "opened a seat — weekend support"},
	]
	# the receipts behind the rows (history's press-through target, wk 20 too)
	s.run_history = [
		{"wk": 20, "said": "answer the buyout with a higher floor", "verdict": "solid",
			"roll": "d20=14", "fx": ["the buyer walked", "morale steadied"]},
		{"wk": 33, "said": "push the calibration kit at the fair", "verdict": "brilliant",
			"roll": "d20=19", "fx": ["+$1,200 — three kits signed", "hype +4"]},
	]
	# the mail, one of every kind
	s.clocks = [{"consequence": "the lease renegotiation lands", "weeks_left": 3}]
	s.applicants = [{"name": "Marta Reyes", "role": "support", "applied_week": 33}]
	s.employees = [{"name": "Ravi Patel", "role": "engineer", "wants_raise": true,
		"asked_week": 32}]
	s.loans = [{"kind": "term", "taken_week": 18, "balance": 9_000, "missed": 0}]
	s.instruments = [{"holder": "Aunt May", "kind": "SAFE", "signed_wk": 6,
		"amount": 25_000}]
	s.rivals = [{"name": "Brightlane", "log": ["wk31: cut prices on the street",
		"wk33: poached a closer"]}]
	# a hot named account — in motion's rank-1 push becomes a PREFILL chip
	s.leads = [{"name": "Café Verde", "flavor": "two rooms, one register",
		"seats": 6, "stage": "pilot", "age_weeks": 2, "heat": 88}]
	s.price_book = {"office": 1}
	s.mna_last_week = 20        # ★ filed: a buyout came and went
	# last week's outcome — the cockpit's consequence lines
	s.last_outcome = {"title": "the fair paid off", "dm": {"effects": [
		{"op": "cash_delta", "v": 1_200, "why": "the pilot invoice cleared"},
		{"op": "morale_delta", "v": -2, "why": "crunch at the works"},
		{"op": "traction_delta", "v": 9, "why": "the street noticed the stand"},
	]}}
	return s

# ═════════════════════════════════ the run ═══════════════════════════════════

func _go() -> void:
	await process_frame
	var d := OS.get_environment("RUNWAY_STRESS_DIR")
	if d != "":
		_dir = d

	_unit_checks()
	await _this_week()
	await _history()
	await _events()

	print("LOG DESK SHOTS: %d captures -> %s · %d check fails"
		% [_shots.size(), _dir, _fails])
	quit(1 if _fails > 0 else 0)

## The lane's non-visual statics, asserted headlessly before any shot.
func _unit_checks() -> void:
	# prefill APPENDS, never overwrites (13-binder § this week); the join is
	# the composer's own " — " grammar (a \n renders as nothing in the field)
	DeskThisWeek.draft = ""
	DeskThisWeek._adopt_prefill("push Café Verde: lead with the pilot numbers")
	_check(DeskThisWeek.draft == "push Café Verde: lead with the pilot numbers",
		"prefill fills an empty draft")
	DeskThisWeek._adopt_prefill("answer Ravi about money")
	_check(DeskThisWeek.draft == "push Café Verde: lead with the pilot numbers"
		+ " — answer Ravi about money", "prefill appends, never overwrites")
	DeskThisWeek.draft = ""
	# the chips sweep the binder's own GROUPS
	_check(DeskThisWeek._desk_ids().size() == 19, "chip sweep covers all 19 desks")
	# collect_suggestions never crashes on quiet desks (S15 contract)
	var sugg := DeskKit.collect_suggestions(_zero(770), DeskThisWeek._desk_ids())
	_check(sugg is Array, "collect_suggestions returns an Array on quiet desks")
	# the rail's number: unread ACTION letters only
	_wipe_stores(776)
	var s := _live(776)
	var n := DeskEvents.unread_action_count(s)
	_check(n >= 2, "unread_action_count sees the ask and the deadline (got %d)" % n)
	var mic := DeskEvents.micro_status(s)
	_check(mic.ends_with("unread"), "events micro_status counts unread (got '%s')" % mic)
	_check(DeskHistory.micro_status(s) == "33 wks", "history micro_status says 33 wks")
	_check(DeskThisWeek.micro_status(s).ends_with("armed"),
		"this week micro_status counts staged rows")
	_check(not DeskEvents.is_dormant(s) and not DeskHistory.is_dormant(s)
		and not DeskThisWeek.is_dormant(s), "the LOG never sleeps")
	_wipe_stores(776)

# ── this week = THE COCKPIT ──────────────────────────────────────────────────
func _this_week() -> void:
	# WEEK 1 — the designed first open: blank-page card, empty chips line,
	# the DO lane with LOCK IN + write the move
	_reset_seams()
	_wipe_stores(771)
	var sz := _zero(771)
	DeskThisWeek.lock_hook = func(t: String) -> void:
		print("LOCK fired: %s" % t)
	var bz := await _open(sz)
	_page(bz, "this week")
	await _shot("log_thisweek_zero_wk1")

	# LIVE — card seeded, staged rows, chips strip (quiet line until other
	# lanes' suggestions land), the badge on the DO lane's LOCK IN
	_reset_seams()
	_wipe_stores(772)
	var sl := _live(772)
	DeskThisWeek.week_card = {"title": "the landlord raises the rent",
		"line": "the office lease resets — pay, argue, or move", "icon": ""}
	DeskThisWeek.lock_hook = func(t: String) -> void:
		print("LOCK fired: %s" % t)
	var bl := await _open(sl)
	_page(bl, "this week")
	await _shot("log_thisweek_live")

	# THE DRAFT — two appended moves in the composer (the append law
	# photographed: both moves visible, separated by the " — " grammar)
	DeskThisWeek.draft = ""
	DeskThisWeek._adopt_prefill("push Café Verde: lead with the pilot numbers")
	DeskThisWeek._adopt_prefill("answer Ravi")
	bl.refresh()
	await _shot("log_thisweek_draft_appended")
	DeskThisWeek.draft = ""

	# THE OUTCOME VIEW — no card seeded: last week's consequence lines with
	# their desk jumps (post-roll, binder opened before the journal reseeds)
	DeskThisWeek.week_card = {}
	bl.refresh()
	await _shot("log_thisweek_outcome")

	# THE HERO RECEIPT — the terms behind the verdict word (kit precedent:
	# the popover opened directly for the photograph)
	var pnl: Dictionary = sl.get_meta("pnl", {})
	bl.popover("the week's terms", [
		{"label": "cash", "value": "$48,600"},
		{"label": "the week's net", "value": "−$13,295", "col": DeskKit.PEN},
		{"label": "runway = cash ÷ net burn", "value": "%d wk" % SimEngine.runway_weeks(sl)},
		{"label": "the verdict", "value": SimEngine.health_band(sl).to_lower()},
	], Vector2(DeskKit.X_ID, 110.0))
	await _shot("log_thisweek_receipt")
	bl.close_popover()

	# THE PRE-ROLL INTERCEPT — this desk's red state: the review names the
	# outstanding asks before the die is allowed to move
	_page(bl, "this week", {"mode": "preroll"})
	await _shot("log_thisweek_preroll_red")
	_reset_seams()

# ── history = THE RUN'S LEDGER ───────────────────────────────────────────────
func _history() -> void:
	# WEEK 1 — the zero state: what the book will hold, the one action
	_wipe_stores(773)
	var sz := _zero(773)
	var bz := await _open(sz)
	_page(bz, "history")
	await _shot("log_history_zero_wk1")

	# LIVE — sparkline endpoint dots (kit-drawn), era sections, the ★ filed
	# row, the double-ruled total
	var sl := _live(774)
	_wipe_stores(774)
	var bl := await _open(sl)
	_page(bl, "history")
	await _shot("log_history_live")

	# THE TOTAL RECEIPT — Σ weekly net, said out loud
	bl.popover("the run so far = Σ weekly net", [
		{"label": "cash at wk 1", "value": "$59,600"},
		{"label": "cash now", "value": "$48,600"},
		{"label": "Σ net across 33 weeks", "value": "−$29,700", "col": DeskKit.PEN},
	], Vector2(DeskKit.X_ID, 560.0))
	await _shot("log_history_receipt")
	bl.close_popover()

	# THE ★ PRESS-THROUGH — the receipts page of the week the buyout was
	# answered (wk 20): the move, the verdict, the die, the effects
	_page(bl, "history", {"mode": "receipts", "wk": 20})
	await _shot("log_history_receipts_page")

# ── events = THE MAIL ────────────────────────────────────────────────────────
func _events() -> void:
	# WEEK 1, empty tray — the zero state teaches the tray
	_wipe_stores(775)
	var sz := _zero(775)
	var bz := await _open(sz)
	_page(bz, "events")
	await _shot("log_events_zero_wk1")

	# LIVE, RED — unread action letters wear the dot and their DO verbs:
	# [answer] on the ask/deadline, [read terms] on the buyout paper
	_wipe_stores(777)
	var sr := _live(777)
	sr.mna = {"buyer": "Northgate Capital", "price": 240_000, "expires_week": 36}
	var br := await _open(sr)
	_page(br, "events")
	await _shot("log_events_live_red")

	# THE ANSWERED PILE — the marks store carries "answered"; the filed rows
	# wear ✓ where the dot stood (wk 32 reopened to show them)
	var marks := {"ask:Ravi Patel:32": "answered", "apply:Marta Reyes:33": true}
	var f := FileAccess.open("user://mail_read_777.json", FileAccess.WRITE)
	f.store_string(JSON.stringify(marks))
	f.close()
	_page(br, "events", {"openwk": 32})
	br.refresh()
	await _shot("log_events_answered_filed")

	# THE S5 ARROW — close (flushes the seen-store), a new letter arrives,
	# reopen: the hero count wears the delta arrow
	br.queue_free()
	await process_frame
	sr.applicants.append({"name": "Jonas Weber", "role": "closer",
		"applied_week": 34})
	var b2 := await _open(sr)
	_page(b2, "events")
	await _shot("log_events_delta_arrow")
	_wipe_stores(777)
