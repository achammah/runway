class_name DeskTeam
extends RefCounted
## DESK — COSTS · "team" = THE PAYROLL LEDGER (DAG2 W2 L-MONEY; DECISIONS:
## team = B with THREE rungs; pixels: docs/design/mockups/06; retrofit: the
## vesting mini-bar per granted row, 12-binder-rework-2 §Retrofits).
##
## THE THREE RUNGS (deterministic counts — SimSpendBook.team_rung):
##   1  ≤9 people   flat person rows — who, role, skill pips, $/wk, note;
##                  coral asks answered ON the row via the existing
##                  counter-offer op (SimLabor.grant_raise), receipt-armed.
##   2  10–40       rows group by FUNCTION with subtotals; a group opens to
##                  its people; ASKERS surface to the top regardless.
##   3  beyond      BUSINESS UNITS — headcount · payroll · avg · asks · a
##                  face from the unit's own burnout; a unit opens to its
##                  function groups (display recursion — engine crowd-pooling
##                  is a recorded later wave). Unit names read state.topics
##                  ("units": [{name, roles[]}]) when the world wrote them.
##
## The OPEN SEAT is an honest row that POINTS at recruitment (▸) — the
## hiring flow lives there now. The vesting mini-bar renders from
## esop.granted with the 208/52 formula (SimSpendBook.vested_frac) until the
## ownership lane's own getter lands.
##
## DAG3 Wave B: S1 zero state (nobody yet), S2 ask strip + spotlit controls
## (raise_first / raise_urgent / go_recruit / open_seat / poached), S3 DO
## lane ([answer ask — name] else [open a seat → recruitment]), S4 the
## payroll-total receipt, S5 the morale ▲/▼ beside the hero's morale read,
## the vesting bar pressing through to the cap table (back pill free), S15
## the ask as a jump suggestion, S8 headcount micro-status.

const QUESTION := "who works here and who's asking?"

const SHEET_X := 10.0
const SHEET_W := 1120.0
const Y_SHEET := 108.0
const Y_FOOT := 806.0
const Y_RULES := 840.0
## An open group shows this many people before the crowd folds.
const GROUP_MAX := 6

## Function groups in the labor lane's own order.
const FUNCTIONS: Array[String] = ["engineer", "designer", "support", "manager", "sales", "ops"]
## The default business units when the topics name none (DECISIONS: a SaaS —
## engineering / GTM / success / G&A), each mapping onto engine functions.
const UNIT_DEFAULTS := [
	{"name": "engineering", "roles": ["engineer", "designer"]},
	{"name": "gtm", "roles": ["sales"]},
	{"name": "success", "roles": ["support"]},
	{"name": "g&a", "roles": ["ops", "manager"]},
]

static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	# onboarding hires are on the payroll the total shows — the count says so
	var big := "%d people" % s.employees.size()
	if s.pipeline.size() > 0:
		big += " +%d onboarding" % s.pipeline.size()
	return {"big": big,
		"line": "$%s/wk on payroll" % _fmt(SimLabor.payroll_wk(s))}

## S8 — the rail's micro-status: the headcount, plain.
static func micro_status(state) -> String:
	var s: GameState = state
	return str(s.employees.size()) if s.employees.size() > 0 else ""

## S8 — the payroll ledger never dims: the founder is always on this page.
static func is_dormant(_state) -> bool:
	return false

## S15 — the loudest object on the desk speaks up: the first ask, as a jump.
static func suggestions(state) -> Array:
	var s: GameState = state
	var fa := _first_asker(s)
	if fa < 0:
		return []
	var e: Dictionary = s.employees[fa]
	return [{"label": "answer the ask — %s wants market pay" % String(e.get("name", "someone")),
		"kind": "jump", "payload": {"desk": "team", "control": "raise_first"}}]

## The engine's own predicates, desk-side: the FIRST asker and the first
## OVERDUE asker (asked ≥2 wks ago — the resignation clock). The attention
## rows' `control` keys land on exactly these marks.
static func _first_asker(state: GameState) -> int:
	for i in state.employees.size():
		if bool((state.employees[i] as Dictionary).get("wants_raise", false)):
			return i
	return -1

static func _first_urgent(state: GameState) -> int:
	for i in state.employees.size():
		var e: Dictionary = state.employees[i]
		if bool(e.get("wants_raise", false)) \
				and state.week - int(e.get("asked_week", state.week)) >= 2:
			return i
	return -1

## S5 — last week's morale, from the metric history the tick already keeps.
static func _morale_prev(state: GameState) -> float:
	for i in range(state.metric_history.size() - 1, -1, -1):
		var m: Dictionary = state.metric_history[i]
		if int(m.get("wk", -1)) != state.week:
			return float(m.get("morale", state.morale))
	return float(state.morale)

static func draw(b) -> void:
	var state: GameState = b.state
	var payroll := SimLabor.payroll_wk(state)
	var n := state.employees.size()
	var rung := SimSpendBook.team_rung(n)

	# ── S1 · the zero state: nobody yet — the page teaches what payroll IS
	if state.employees.is_empty() and state.pipeline.is_empty() and state.open_roles.is_empty():
		var band := SimOwnership.band_for(state, "engineer")
		var zero_go := func() -> void:
			b.focus_desk("recruitment")
		DeskKit.zero_state(b, {
			"will_show": "the payroll ledger — who works here, what they cost, who's asking",
			"would_line": "a first engineer WOULD cost $%s–%s a week — and the roof rides every head" % [
				SimOwnership.money(int(band.get("lo", 0))), SimOwnership.money(int(band.get("hi", 0)))],
			"action_label": "open a seat → recruitment",
			"action_cb": zero_go,
			"wakes_hint": "wakes when the first offer is signed — hiring lives at recruitment",
		})
		return

	# ── the hero — onboarding hires are paid, so the money line names them
	var big: String = "%d people" % n
	b.label(big, Vector2(SHEET_X, 6.0), DeskKit.HERO, DeskKit.INK, 420.0)
	var bw: float = b.font().get_string_size(big, HORIZONTAL_ALIGNMENT_LEFT, -1, DeskKit.HERO).x
	b.label("· $%s a week%s" % [b.fmt(payroll),
		(" · +%d onboarding" % state.pipeline.size()) if state.pipeline.size() > 0 else ""],
		Vector2(SHEET_X + bw + 14.0, 22.0), DeskKit.ROW,
		Color(DeskKit.INK, 0.7), 420.0)
	b.label("payroll is the biggest bill in the building — and the easiest to grow carelessly.",
		Vector2(SHEET_X, 62.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 700.0)
	if state.applicants.size() > 0:
		DeskKit.clock_chip(b, 848.0, 10.0, "%d applicant%s waiting" % [state.applicants.size(),
			"" if state.applicants.size() == 1 else "s"])
	# the door to the hiring flow, always drawn — the red rows land on it
	var go_recruit := func() -> void:
		b.focus_desk("recruitment")
	DeskKit.word(b, "recruitment ▸", Vector2(848.0, 38.0), go_recruit, DeskKit.LAW,
		Color(DeskKit.INK, 0.6), 200.0)
	b.mark_control("go_recruit", Rect2(840.0, 36.0, 216.0, 44.0))
	# S5 — the morale read wears its week-over-week arrow (the meta line lost
	# the sheet's own unit words; the sheet says them once)
	var mtxt := "morale %d · rung %d" % [state.morale, rung]
	var mw: float = b.font().get_string_size(mtxt, HORIZONTAL_ALIGNMENT_LEFT, -1, DeskKit.LAW).x
	var mx := SHEET_X + SHEET_W - mw - 4.0
	b.label(mtxt, Vector2(mx, 64.0), DeskKit.LAW, Color(DeskKit.INK, 0.42), mw + 8.0)
	DeskKit.delta_arrow(b, mx - 24.0, 66.0, float(state.morale), _morale_prev(state))
	# S2a — red speaks on the page; the sheet drops 8px clear of the strip
	var sheet_y := Y_SHEET
	if DeskKit.ask_strip(b, "team", SHEET_X, 86.0, 1000.0, "answer the ask before it walks"):
		sheet_y += 8.0

	# ── the sheet
	var sheet := DeskKit.ledger_sheet(b, SHEET_X, sheet_y, SHEET_W, {
		"columns": [{"label": "who", "w": 210.0}, {"label": "role", "w": 160.0},
			{"label": "skill", "w": 140.0}, {"label": "$/wk", "w": 120.0, "align": "right"},
			{"label": "note", "w": 330.0}],
		"amount": 3, "adjust": false, "unit": "all figures $/week",
	})
	match rung:
		1:
			for i in state.employees.size():
				_person_row(b, sheet, state, i)
		2:
			_askers_first(b, sheet, state)
			_function_groups(b, sheet, state, range(state.employees.size()), "fn")
		3:
			_askers_first(b, sheet, state)
			_unit_rows(b, sheet, state)
	# onboarding hires are on the payroll before they are productive
	for h in state.pipeline:
		var hd: Dictionary = h
		DeskKit.ledger_row(b, sheet, [String(hd.get("name", "a hire")),
			String(hd.get("role", "")), "", "$" + b.fmt(int(hd.get("salary", 0))),
			"onboarding — wk %d of 2" % clampi(int(hd.get("weeks_in", 0)) + 1, 1, 2)], {"dim": true})
	# THE OPEN SEAT — an honest row; the flow lives at recruitment
	var seat_marked := false
	for r in state.open_roles:
		var rd: Dictionary = r
		var role := String(rd.get("role", "engineer"))
		var waiting := SimLabor.waiting_for(state, role)
		var seat_y := float(sheet.get("cursor", 0.0))
		var go := func() -> void:
			b.focus_desk("recruitment")
		# Law 2 — the offered pay rides the money column; the rate is a fact
		DeskKit.ledger_row(b, sheet, ["%s — open seat" % role, "advertised",
			"≈%.1f apply/wk" % SimLabor.arrival_rate(state, rd),
			"$" + b.fmt(int(rd.get("offered_salary", 0))),
			"%d waiting -> recruitment ▸" % waiting], {"dim": true, "on_press": go})
		# S2b — a silent advert's red row lands on its own seat
		if not seat_marked:
			b.mark_control("open_seat", Rect2(SHEET_X, seat_y, SHEET_W * 0.5, DeskKit.LG_ROW_H))
			seat_marked = true
	# S4 — PRESS THE TOTAL: the receipt that decomposes the payroll
	var tot_y := float(sheet.get("cursor", 0.0))
	DeskKit.ledger_total(b, sheet, "total payroll", "$" + b.fmt(payroll))
	b.mark_control("payroll_total", Rect2(SHEET_X, tot_y, SHEET_W, DeskKit.LG_TOT_H))
	DeskKit.press_receipt(b, "payroll_total", "payroll = every signed salary",
		_payroll_lines(b, state))
	DeskKit.ledger_memo(b, sheet, "fully loaded", "≈$" + b.fmt(SimLabor.loaded_payroll_wk(state)),
		"with the roof's share · severance always owed")
	DeskKit.ledger_end(b, sheet)

	# ── S3 · the DO lane: answer the loudest ask, or grow the roster
	var actions: Array = []
	var fa := _first_asker(state)
	if fa >= 0:
		var ea: Dictionary = state.employees[fa]
		var fair := SimLabor.fair_pay(state, ea)
		var fi := fa
		var lane_answer := func() -> void:
			SimLabor.grant_raise(state, fi, fair)
		actions.append({"label": "answer ask — %s · $%s/wk" % [
			String(ea.get("name", "someone")).split(" ")[0], b.fmt(fair)],
			"tier": "two-tap", "cb": lane_answer})
	var lane_seat := func() -> void:
		b.focus_desk("recruitment")
	actions.append({"label": "open a seat → recruitment", "tier": "", "cb": lane_seat})
	DeskKit.do_lane(b, actions)

	# ── the teaching foot
	b.label("a person costs more than their pay — the roof, the seats and the office share ride every head",
		Vector2(SHEET_X, Y_FOOT), DeskKit.LAW, Binder.BLUE, 1100.0)
	b.label("asks answered late become resignations · at 10 the rows group by function · at hundreds, by business unit — same sheet, folded",
		Vector2(SHEET_X, Y_RULES), DeskKit.LAW, Color(DeskKit.INK, 0.5), 1100.0)

## S4 — the payroll receipt's terms: signed salaries, the onboarding share,
## and the loaded truth the memo whispers.
static func _payroll_lines(b, state: GameState) -> Array:
	var emp_sum := 0
	for e in state.employees:
		emp_sum += int((e as Dictionary).get("salary", 0))
	var pipe_sum := 0
	for h in state.pipeline:
		pipe_sum += int((h as Dictionary).get("salary", 0))
	var lines: Array = [{"label": "salaries — %d people" % state.employees.size(),
		"value": "$%s/wk" % b.fmt(emp_sum)}]
	if pipe_sum > 0:
		lines.append({"label": "onboarding — %d hire%s" % [state.pipeline.size(),
			"" if state.pipeline.size() == 1 else "s"], "value": "$%s/wk" % b.fmt(pipe_sum)})
	lines.append({"label": "fully loaded", "value": "≈$%s/wk" % b.fmt(SimLabor.loaded_payroll_wk(state))})
	lines.append({"label": "the law", "value": "severance always owed"})
	return lines

# ── the one person row every rung shares ─────────────────────────────────────

static func _person_row(b, sheet: Dictionary, state: GameState, i: int) -> void:
	var e: Dictionary = state.employees[i]
	var row_y := float(sheet.get("cursor", 0.0))
	var asking := bool(e.get("wants_raise", false))
	DeskKit.ledger_row(b, sheet, [String(e.get("name", "someone")),
		SimLabor.role_of(e), "", "$" + b.fmt(int(e.get("salary", 0))), ""], {})
	# skill pips, drawn into the skill column
	var cols: Array = sheet.get("cols", [])
	var skill_x := float((cols[2] as Dictionary).get("x", 0.0))
	DeskKit.pips(b, Vector2(skill_x, row_y + 13.0), SimLabor.skill_of(e), 5)
	var note_x := float((cols[4] as Dictionary).get("x", 0.0))
	var note_w := float((cols[4] as Dictionary).get("w", 300.0))
	# S2b — a courted colleague's red row lands on their own line
	if int(state.get_meta("poach_wk", -99)) == state.week \
			and String(e.get("name", "")) == String(state.get_meta("poach_name", "")):
		b.mark_control("poached", Rect2(SHEET_X, row_y, SHEET_W * 0.5, DeskKit.LG_ROW_H))
	if asking:
		# the coral ask, answered ON the row via the existing raise op —
		# the arm carries the price first (the receipt path)
		var fair := SimLabor.fair_pay(state, e)
		var idx := i
		var fire := func() -> void:
			SimLabor.grant_raise(state, idx, fair)
		DeskKit.arm(b, "raise_%d" % i, "wants market pay — answer $%s" % b.fmt(fair),
			"pay $%s/wk — sure?" % b.fmt(fair), Vector2(note_x, row_y + 4.0), fire, 300.0, 19)
		# S2b — the red rows land on the arm that answers them
		if i == _first_asker(state):
			b.mark_control("raise_first", Rect2(note_x - 8.0, row_y + 2.0, 316.0,
				DeskKit.LG_ROW_H))
		if i == _first_urgent(state):
			b.mark_control("raise_urgent", Rect2(note_x - 8.0, row_y + 2.0, 316.0,
				DeskKit.LG_ROW_H))
		return
	var grant := SimSpendBook.grant_for(state, String(e.get("name", "")))
	if not grant.is_empty():
		# THE VESTING MINI-BAR (the ESOP thread's team surface)
		var frac := SimSpendBook.vested_frac(state.week, int(grant.get("vest_start_wk", 0)))
		var cliff_wk := int(grant.get("vest_start_wk", 0)) + 52
		var cliff_txt := "cliff passed" if state.week >= cliff_wk else "cliff wk %d" % cliff_wk
		b.label("%.1f%% · %d%% vested · %s" % [float(grant.get("pct", 0.0)), int(round(frac * 100.0)),
			cliff_txt], Vector2(note_x, row_y + 8.0), 18, Color(DeskKit.INK, 0.7), note_w - 84.0)
		DeskKit.meter(b, note_x + note_w - 76.0, row_y + 9.0, 66.0, frac, DeskKit.SAGE)
		# S7 — the bar presses through to the ownership state; the back
		# pill home is free (focus_desk carries the source)
		var vest_jump := func() -> void:
			b.focus_desk("cap table", "", "team")
		var hit: Button = DeskKit.word(b, "", Vector2(note_x, row_y + 2.0), vest_jump,
			18, DeskKit.INK, note_w)
		hit.size = Vector2(note_w, DeskKit.LG_ROW_H - 4.0)
		return
	var quirk := String(e.get("quirk", ""))
	b.label(quirk if quirk != "" else "—", Vector2(note_x, row_y + 8.0), 18,
		Color(DeskKit.INK, 0.5), note_w - 10.0)

## ASKERS SURFACE FIRST (rungs 2–3): the people asking are never folded.
static func _askers_first(b, sheet: Dictionary, state: GameState) -> void:
	var asked := false
	for i in state.employees.size():
		if bool((state.employees[i] as Dictionary).get("wants_raise", false)):
			if not asked:
				DeskKit.ledger_section(b, sheet, "the askers — answer or lose them")
				asked = true
			_person_row(b, sheet, state, i)

## Rung 2: the roster grouped by FUNCTION with subtotals; a group opens to
## its people (askers already surfaced above, so they are skipped here).
static func _function_groups(b, sheet: Dictionary, state: GameState, pool, key_prefix: String) -> void:
	for fn in FUNCTIONS:
		var members: Array = []
		var group_pay := 0
		for i in pool:
			var e: Dictionary = state.employees[int(i)]
			if SimLabor.role_row(SimLabor.role_of(e)) != fn:
				continue
			members.append(int(i))
			group_pay += int(e.get("salary", 0))
		if members.is_empty():
			continue
		var key := "%s_%s" % [key_prefix, fn]
		var open := bool(b.desk.get(key, false))
		var toggle := func() -> void:
			b.desk[key] = not open
		DeskKit.ledger_row(b, sheet, ["%s ×%d" % [fn.to_upper(), members.size()],
			"", "", "$" + b.fmt(group_pay), ("close ▸" if open else "open ▸")],
			{"on_press": toggle})
		if open:
			var shown := 0
			for mi in members:
				if bool((state.employees[int(mi)] as Dictionary).get("wants_raise", false)):
					continue   # already face-up in THE ASKERS
				if shown >= GROUP_MAX:
					DeskKit.ledger_row(b, sheet, ["the other %d" % (members.size() - shown),
						"", "", "", "steady"], {"dim": true})
					break
				_person_row(b, sheet, state, int(mi))
				shown += 1

## Rung 3: BUSINESS UNITS — the same grouped-row component recursed. Unit
## names come from the topics when the world wrote them; the engine keeps no
## per-unit meters yet, so the face reads the unit's own burnout mix.
static func _unit_rows(b, sheet: Dictionary, state: GameState) -> void:
	var units := _units(state)
	var opened := String(b.desk.get("unit_open", ""))
	var others := 0
	var others_pay := 0
	for u in units:
		var ud: Dictionary = u
		var roles: Array = ud.get("roles", [])
		var members: Array = []
		var pay := 0
		var asks := 0
		var burnout := 0
		for i in state.employees.size():
			var e: Dictionary = state.employees[i]
			if not roles.has(SimLabor.role_row(SimLabor.role_of(e))):
				continue
			members.append(i)
			pay += int(e.get("salary", 0))
			burnout += int(e.get("burnout", 0))
			if bool(e.get("wants_raise", false)):
				asks += 1
		if members.is_empty():
			continue
		var face := GameState.burnout_state(burnout / maxi(members.size(), 1))
		var uname := String(ud.get("name", "a unit"))
		var open: bool = opened == uname
		# an open unit folds its siblings to one counted row (the pane never
		# crowds; the askers already surfaced above)
		if opened != "" and not open:
			others += 1
			others_pay += pay
			continue
		var toggle := func() -> void:
			b.desk["unit_open"] = "" if open else uname
		var row_y := float(sheet.get("cursor", 0.0))
		DeskKit.ledger_row(b, sheet, [uname.to_upper(), "%d people" % members.size(), "",
			"$" + b.fmt(pay), "avg $%s · %d asking · %s" % [b.fmt(pay / maxi(members.size(), 1)),
			asks, ("open ▸" if not open else "close ▸")]], {"on_press": toggle})
		var cols: Array = sheet.get("cols", [])
		var skill_x := float((cols[2] as Dictionary).get("x", 0.0))
		b.label(face, Vector2(skill_x, row_y + 8.0), 18,
			Binder.PEN if face != "fine" else Color("5D7A50"), 120.0)
		if open:
			_function_groups(b, sheet, state, members, "u_" + uname)
	if others > 0:
		var back_out := func() -> void:
			b.desk["unit_open"] = ""
		DeskKit.ledger_row(b, sheet, ["the other %d units" % others, "", "",
			"$" + _fmt(others_pay), "close this unit to see them ▸"],
			{"dim": true, "on_press": back_out})

## The units the topics name, validated; else the defaults.
static func _units(state: GameState) -> Array:
	var out: Array = []
	for u in state.topics.get("units", []):
		if not (u is Dictionary):
			continue
		var ud: Dictionary = u
		var roles: Array = []
		for r in ud.get("roles", []):
			if FUNCTIONS.has(String(r)):
				roles.append(String(r))
		if String(ud.get("name", "")) != "" and not roles.is_empty():
			out.append({"name": String(ud.get("name", "")), "roles": roles})
	if out.is_empty():
		return UNIT_DEFAULTS
	# any function no unit claimed falls into the last unit, so nobody vanishes
	var claimed: Array = []
	for u2 in out:
		for r2 in (u2 as Dictionary).get("roles", []):
			claimed.append(r2)
	for fn in FUNCTIONS:
		if not claimed.has(fn):
			((out[out.size() - 1] as Dictionary).get("roles", []) as Array).append(fn)
	return out

static func handle(_b, _id: String) -> void:
	pass   # every control on this sheet carries its own closure

static func _fmt(n: int) -> String:
	var s := str(absi(n))
	var out := ""
	while s.length() > 3:
		out = "," + s.substr(s.length() - 3) + out
		s = s.substr(0, s.length() - 3)
	return ("-" if n < 0 else "") + s + out
