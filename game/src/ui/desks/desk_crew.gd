class_name DeskCrew
extends RefCounted
## DESK — the binder's `crew` tab. Spec: docs/design/02-labor-market.md §7
##
## `binder.gd` dispatches the tab body here and passes ITSELF, so this file draws
## through the binder's own helpers and never reaches into the sheet directly.
##
## THREE PAGES, ONE DESK (the ruling in docs/design/00-spine.md §11: the roster
## and the hiring half cannot share one 760px sheet):
##   ROSTER  the people you have — what each one costs FULLY LOADED, who is
##           about to walk, and the payroll total underneath
##   PERSON  one person's whole page: pay against the market, the raise stepper,
##           and the let-go arm with the severance invoice in its own caption
##   HIRING  the roles you advertise, the rate you advertise them at, and the
##           people that rate brings in
## The pen toggle in the header moves between roster and hiring; Esc walks
## PERSON → ROSTER before it ever closes the binder (the shared contract).
##
## THE DESK TEACHES: MARKET RATE beside every advert, FULLY-LOADED beside every
## salary, SEVERANCE inside the button that charges it. Nothing here recomputes a
## rule — every number comes from SimLabor, so the desk and the engine can never
## disagree about what a head costs.

const ROW_H := 66.0          ## one person
const CO_H := 56.0           ## one cofounder, or the founder
const PIPE_H := 48.0         ## one hire still onboarding
const BODY_TOP := 92.0
const BODY_BOTTOM := 596.0
const PIPS_X := 770.0

## Draw the crew desk. `b` is the Binder itself (untyped to keep the two files
## free of a cyclic class dependency).
static func draw(b) -> void:
	var mode := String(b.desk.get("mode", ""))
	if mode == "person":
		_page_person(b)
	elif mode == "hiring":
		_page_hiring(b)
	else:
		_page_roster(b)

## A press inside this desk. `id` is whatever the desk's own draw registered.
static func handle(_b, _id: String) -> void:
	pass

# ═════════════════════════════ THE HEADER ════════════════════════════════════

## The desk's name and the pen toggle between its two halves. Both words are
## always live, so the half you are not looking at is never hidden — it is
## simply the dim one.
static func _head(b, title_text: String, mode: String) -> float:
	var y := DeskKit.title(b, title_text)
	DeskKit.word(b, "roster", Vector2(830.0, 14.0), func() -> void:
		b.desk["mode"] = ""
		b.desk.erase("row"), DeskKit.STATUS,
		DeskKit.INK if mode != "hiring" else Color(DeskKit.INK, 0.45), 160.0)
	DeskKit.word(b, "hiring", Vector2(995.0, 14.0), func() -> void:
		b.desk["mode"] = "hiring"
		b.desk.erase("row"), DeskKit.STATUS,
		DeskKit.INK if mode == "hiring" else Color(DeskKit.INK, 0.45), 160.0)
	return y

# ═════════════════════════════ PAGE: ROSTER ══════════════════════════════════

static func _page_roster(b) -> void:
	var state: GameState = b.state
	_head(b, "crew — the people, and what they cost", "roster")
	var y := BODY_TOP
	# THE FOUNDER IS ON THE ROSTER. They are the first head this company ever
	# paid for, and the competences line is what they are actually good at.
	b.icon("you", Vector2(10.0, y - 4.0), 44.0)
	var who := state.founder_name if state.founder_name != "" else "the founder"
	b.label("%s — founder · lvl %d · exhaustion %d/6" % [who, state.level, state.exhaustion],
		Vector2(66.0, y), DeskKit.ROW, DeskKit.INK, 900.0)
	var stats := PackedStringArray()
	for st_n in ["build", "sell", "raise", "recruit", "grit"]:
		stats.append("%s %d" % [st_n, int(state.competences.get(st_n, 3))])
	b.label("  ·  ".join(stats), Vector2(66.0, y + 30.0), DeskKit.DETAIL,
		Color(DeskKit.INK, 0.6), 900.0)
	y += CO_H
	for cf in state.cofounders:
		var cfd: Dictionary = cf
		b.icon("cofd_tech", Vector2(10.0, y - 4.0), 44.0)
		var cf_name := String(cfd.get("name", "")).strip_edges()
		b.label("%s%s cofounder · %.0f%% equity · not on payroll" % [
			(cf_name + " — ") if cf_name != "" else "", _cof_role(cfd.get("role", "")),
			float(cfd.get("equity_diluted", cfd.get("equity", 0)))],
			Vector2(66.0, y + 4.0), DeskKit.ROW, DeskKit.INK, 1000.0)
		y += CO_H
	# HQ GROUPS INTO DEPARTMENTS. At a 40-head cap a flat list is unreadable, and
	# the subtotal is the number a founder at that size actually thinks in.
	if SimLabor.era_idx(state.era) >= 4 and state.employees.size() > 0:
		y = DeskKit.rule(b, y + 2.0)
		for r in SimLabor.ROLE_ORDER:
			var heads := 0
			var cost := 0
			for e in state.employees:
				if SimLabor.role_row(SimLabor.role_of(e as Dictionary)) == r:
					heads += 1
					cost += int((e as Dictionary).get("salary", 0))
			if heads > 0:
				b.label("%s — %d heads · $%s/wk" % [_dept(r), heads, b.fmt(cost)],
					Vector2(10.0, y), DeskKit.STATUS, Color(DeskKit.INK, 0.7), 900.0)
				y += 34.0
		y += 6.0
	if state.employees.is_empty() and state.pipeline.is_empty():
		y = DeskKit.empty(b, Vector2(10.0, y),
			"nobody on the payroll but you. every hire is a weekly bill that starts before the work does.",
			"the hiring half of this desk posts a role — what you advertise against the MARKET RATE decides who answers.")
	# THE LOUDEST PEOPLE SORT TO THE TOP: whoever is asking, then whoever is
	# furthest under market. A quiet roster needs no buttons, so the cap never
	# hides a decision.
	var order := _roster_order(state)
	var room: int = clampi(int((BODY_BOTTOM - y) / ROW_H), 1, DeskKit.LIST_CAP)
	var shown := 0
	for idx in order:
		if shown >= room:
			break
		y = _roster_row(b, state, int(idx), y)
		shown += 1
	if shown < order.size():
		y = DeskKit.more(b, Vector2(10.0, y), order.size() - shown, "on the payroll, all quiet")
	for h in state.pipeline:
		if y > BODY_BOTTOM - PIPE_H:
			break
		var hd: Dictionary = h
		b.icon("employee", Vector2(10.0, y - 6.0), 44.0)
		b.label("%s — %s · ONBOARDING (paid $%s/wk, productive in %d wk)" % [
			String(hd.get("name", "?")), String(hd.get("role", "?")),
			b.fmt(int(hd.get("salary", 0))), maxi(2 - int(hd.get("weeks_in", 0)), 1)],
			Vector2(66.0, y + 2.0), DeskKit.STATUS, Color(DeskKit.INK, 0.55), 1000.0)
		y += PIPE_H
	DeskKit.spark(b, b.series("morale"), Vector2(600.0, 600.0), Vector2(560.0, 78.0),
		DeskKit.SAGE, "morale, drawn weekly:")
	# THE TWO NUMBERS THAT MATTER, side by side: what you think a team costs, and
	# what it actually costs once the room it sits in is counted.
	DeskKit.footer(b, {
		"computed": "payroll $%s/wk  ·  FULLY-LOADED $%s/wk (salary + rent, infra and the office lever, split per head)"
			% [b.fmt(SimLabor.payroll_wk(state)), b.fmt(SimLabor.loaded_payroll_wk(state))],
		"rules": _rules(state),
		"warning": _warning(b, state),
	})

## Whoever needs a decision first: the askers, then the furthest under market.
static func _roster_order(state: GameState) -> Array:
	var rows: Array = []
	for i in state.employees.size():
		var e: Dictionary = state.employees[i]
		var fair := maxf(float(SimLabor.fair_pay(state, e)), 1.0)
		var ratio := float(int(e.get("salary", 0))) / fair
		rows.append({"i": i, "ask": 1 if bool(e.get("wants_raise", false)) else 0,
			"ratio": ratio})
	rows.sort_custom(func(x, y) -> bool:
		var xd: Dictionary = x
		var yd: Dictionary = y
		if int(xd["ask"]) != int(yd["ask"]):
			return int(xd["ask"]) > int(yd["ask"])
		if absf(float(xd["ratio"]) - float(yd["ratio"])) > 0.0001:
			return float(xd["ratio"]) < float(yd["ratio"])
		return int(xd["i"]) < int(yd["i"]))
	var out: Array = []
	for r in rows:
		out.append(int((r as Dictionary)["i"]))
	return out

static func _roster_row(b, state: GameState, idx: int, y: float) -> float:
	var e: Dictionary = state.employees[idx]
	var salary := int(e.get("salary", 0))
	var fair := SimLabor.fair_pay(state, e)
	b.icon("employee", Vector2(10.0, y - 6.0), 44.0)
	b.label("%s — %s · $%s/wk ($%s loaded)" % [
		String(e.get("name", "?")), SimLabor.role_of(e), b.fmt(salary),
		b.fmt(SimLabor.loaded_cost(state, salary))],
		Vector2(66.0, y), DeskKit.ROW, DeskKit.INK, 690.0)
	DeskKit.pips(b, Vector2(PIPS_X, y + 10.0), SimLabor.skill_of(e))
	if bool(e.get("wants_raise", false)):
		# THE INLINE CORAL MARK (§2.8): the subject carries its own warning, with
		# the gap in numbers, so refusing is an informed bet and not an oversight.
		b.label("! wants market pay — $%s now against $%s fair (%.2f×)" % [
			b.fmt(salary), b.fmt(fair), float(salary) / maxf(float(fair), 1.0)],
			Vector2(66.0, y + 32.0), DeskKit.DETAIL, DeskKit.PEN, 690.0)
		DeskKit.word(b, "+10%", Vector2(DeskKit.X_MINUS, y), func() -> void:
			SimLabor.grant_raise(state, idx, int(round(float(salary) * 1.1))),
			DeskKit.STATUS, DeskKit.INK, 140.0)
	else:
		# THE QUIRK IS THE FIRST THING THAT YIELDS. This row is a fixed 66px, so a
		# receipts line that wraps writes its second half through the next person's
		# name — and the person BELOW is the one who disappears. The facts are
		# measured first; the voice joins them only if it fits on the one line.
		# (Burnout carries its scale: a bare 51 is not a number, it is trivia.)
		var facts := "burnout %d/100 · %d wks here · paid %.2f× fair" % [int(e.get("burnout", 0)),
			SimLabor.tenure_of(state, e), float(salary) / maxf(float(fair), 1.0)]
		var quirk := String(e.get("quirk", "")).strip_edges()
		if quirk != "":
			var whole := facts + " · \"%s\"" % quirk
			if b.wrap_h(whole, DeskKit.DETAIL, 690.0) <= 34.0:
				facts = whole
		b.label(facts, Vector2(66.0, y + 32.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.45), 690.0)
	DeskKit.expand(b, Vector2(DeskKit.X_EXPAND, y), func() -> void:
		b.desk["mode"] = "person"
		b.desk["row"] = idx)
	return y + ROW_H

## THE COFOUNDER'S ROLE, AS A WORD. The draft stores the card the founder picked
## as its INDEX (founder_draft_screen `{"role": i}`), so a straight `str()` put
## "Nico Ferreira — 0 cofounder" on the page: a raw engine value, which is the one
## thing §3.8 says never prints. The names are the draft's own five cards; a save
## that already holds a string keeps it.
const COFOUNDER_ROLES := ["sales", "business", "tech", "hustler", "idea"]

static func _cof_role(role) -> String:
	if role is String and String(role).strip_edges() != "":
		return String(role).to_lower()
	var i := int(role) if (role is int or role is float) else -1
	if i >= 0 and i < COFOUNDER_ROLES.size():
		return String(COFOUNDER_ROLES[i])
	return "a"

static func _dept(role: String) -> String:
	match role:
		"engineer": return "ENGINEERING"
		"sales": return "SALES"
		"designer": return "DESIGN"
		"ops": return "OPERATIONS"
		"support": return "SUPPORT"
		"manager": return "MANAGEMENT"
	return role.to_upper()

# ═════════════════════════════ PAGE: PERSON ══════════════════════════════════

static func _page_person(b) -> void:
	var state: GameState = b.state
	var idx := int(b.desk.get("row", -1))
	if idx < 0 or idx >= state.employees.size():
		b.desk["mode"] = ""
		b.desk.erase("row")
		_page_roster(b)
		return
	var e: Dictionary = state.employees[idx]
	DeskKit.back(b, "back to everyone", func() -> void:
		b.desk["mode"] = ""
		b.desk.erase("row"))
	var salary := int(e.get("salary", 0))
	var market := SimLabor.market_salary(SimLabor.role_of(e), state.era)
	var fair := SimLabor.fair_pay(state, e)
	var ratio := float(salary) / maxf(float(fair), 1.0)
	var tenure := SimLabor.tenure_of(state, e)
	b.label("%s — %s" % [String(e.get("name", "?")), SimLabor.role_of(e)],
		Vector2(10.0, 62.0), DeskKit.TITLE, DeskKit.INK, 700.0)
	DeskKit.pips(b, Vector2(740.0, 78.0), SimLabor.skill_of(e))
	b.label("skill %d of 5" % SimLabor.skill_of(e), Vector2(870.0, 70.0),
		DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 260.0)
	var y := 124.0
	# BURNOUT CARRIES ITS SCALE, here as on the roster: a bare 51 is not a number,
	# it is trivia (§3.2 — units always attached).
	b.label("$%s/wk on the payroll  ·  $%s/wk FULLY LOADED  ·  %d wks here  ·  burnout %d/100" % [
		b.fmt(salary), b.fmt(SimLabor.loaded_cost(state, salary)), tenure,
		int(e.get("burnout", 0))], Vector2(10.0, y), DeskKit.STATUS, DeskKit.BLUE, 1100.0)
	y += 40.0
	var anchor := "the MARKET RATE for %s at this stage is $%s/wk. Skill %d asks ×%.2f of it, so FAIR PAY here is $%s/wk." % [
		_role_noun(SimLabor.role_of(e)), b.fmt(market), SimLabor.skill_of(e),
		float(SimLabor.SKILL_ASK.get(SimLabor.skill_of(e), 1.0)), b.fmt(fair)]
	b.label(anchor, Vector2(10.0, y), DeskKit.DETAIL, Color(DeskKit.INK, 0.75), 1100.0)
	y += maxf(b.wrap_h(anchor, DeskKit.DETAIL, 1100.0), 28.0) + 12.0
	# THE CORAL BUDGET IS TWO WARNING LINES A PANE (§1.1), and the footer's own
	# warning already spends one. Once somebody has ASKED, the under-market line is
	# the same fact told twice — the ask IS what being under the band produces —
	# so the ratio yields its coral to the countdown and stays in the stepper's
	# effect string, where the decision is actually made.
	var asking := bool(e.get("wants_raise", false))
	if ratio < 0.85 and not asking:
		var since := int(e.get("underpaid_since", -1))
		b.label("you pay %.2f× fair%s — under 0.85× the asks start, and they compound into resignations."
			% [ratio, (" for %d wks now" % maxi(state.week - since, 0)) if since >= 0 else ""],
			Vector2(10.0, y), DeskKit.STATUS, DeskKit.PEN, 1100.0)
		y += 44.0
	elif not asking:
		b.label("you pay %.2f× fair — at or above the market band, nobody is counting the days."
			% ratio, Vector2(10.0, y), DeskKit.STATUS, Color(DeskKit.INK, 0.75), 1100.0)
		y += 44.0
	if asking:
		var asked := int(e.get("asked_week", state.week))
		var left := maxi(3 - (state.week - asked), 0)
		# THE ONE CORAL LINE CARRIES THE ANCHOR TOO (§3.4): what you pay against
		# fair, and how long that number has left.
		b.label("! they have asked — you pay %.2f× fair. %s" % [ratio,
			"they resign at the end of this week unless the pay moves."
			if left <= 0 else "about %d wk%s of patience left at this number."
			% [left, "" if left == 1 else "s"]],
			Vector2(10.0, y), DeskKit.STATUS, DeskKit.PEN, 1100.0)
		y += 44.0
	y += 10.0
	# THE RAISE, ON THE HOUSE LADDER. The engine re-clamps on write, so the
	# stepper cannot walk anywhere the world does not allow.
	var steps := SimLabor.salary_steps(market, 2.5)
	y = DeskKit.stepper(b, y, {
		"name": "their salary",
		# ONE MEASURED LINE at 480px: the why column is 480 wide and a second line
		# runs past the stepper's own 78px pitch into the let-go arm.
		"why": "clamped to the market band: 0.5× to 2.5× the going rate",
		"value": "$%s/wk" % b.fmt(salary),
		"effect": _raise_effect(salary, fair),
		"at_min": DeskKit.at_min(steps, float(salary)),
		"at_max": DeskKit.at_max(steps, float(salary)),
		"bound": "the band's floor" if DeskKit.at_min(steps, float(salary)) else (
			"the band's ceiling" if DeskKit.at_max(steps, float(salary)) else ""),
		"on_minus": func() -> void:
			SimLabor.grant_raise(state, idx, int(DeskKit.ladder(steps, float(salary), -1))),
		"on_plus": func() -> void:
			SimLabor.grant_raise(state, idx, int(DeskKit.ladder(steps, float(salary), 1))),
	})
	y += 20.0
	# LETTING GO: the invoice is the confirmation. Nothing else on the sheet
	# changes on the first press — the words themselves become the price.
	var owed := SimLabor.severance_for(state, e)
	var weeks := SimLabor.severance_weeks(state.era, tenure)
	DeskKit.arm(b, "letgo_%d" % idx, "let go", "owe $%s severance — sure?" % b.fmt(owed),
		Vector2(10.0, y), func() -> void:
			SimLabor.fire_employee(state, idx)
			b.desk["mode"] = ""
			b.desk.erase("row"), 700.0)
	y += 52.0
	b.label("SEVERANCE at this stage is %d wk%s of salary for %d wks of tenure — $%s, booked to next week's ledger. They leave now; the bill does not."
		% [weeks, "" if weeks == 1 else "s", tenure, b.fmt(owed)],
		Vector2(10.0, y), DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 1100.0)
	DeskKit.footer(b, {
		"computed": "one head here costs $%s/wk fully loaded — %.0f%% more than the salary alone"
			% [b.fmt(SimLabor.loaded_cost(state, salary)),
			(float(SimLabor.loaded_cost(state, salary)) / maxf(float(salary), 1.0) - 1.0) * 100.0],
		"rules": _rules(state),
		"warning": _warning(b, state),
	})

static func _raise_effect(salary: int, fair: int) -> String:
	var ratio := float(salary) / maxf(float(fair), 1.0)
	if ratio >= 0.95:
		return "%.2f× fair — the ask clears at 0.95×" % ratio
	if ratio >= 0.85:
		return "%.2f× fair — inside the band, no asks" % ratio
	if ratio >= 0.60:
		return "%.2f× fair — they may start asking" % ratio
	return "%.2f× fair — insulting; the ask is immediate" % ratio

# ═════════════════════════════ PAGE: HIRING ══════════════════════════════════

static func _page_hiring(b) -> void:
	var state: GameState = b.state
	var y := _head(b, "crew — hiring against the market rate", "hiring")
	if not SimLabor.market_open(state.era):
		# THE ERA GATE, TAUGHT rather than greyed out: a garage does not post
		# jobs, it asks the people it already knows.
		DeskKit.empty(b, Vector2(10.0, y),
			"nobody answers an advert taped to a garage door.",
			"the market opens when you do — a desk somewhere other than your kitchen is what makes a role worth applying to. Until then the people you get are the people you already know.")
		DeskKit.footer(b, {"computed": "", "rules": _rules(state),
			"warning": _warning(b, state)})
		return
	y += 4.0
	if state.open_roles.is_empty():
		y = DeskKit.empty(b, Vector2(10.0, y),
			"nobody is hiring. open a role and the street starts sending people —",
			"the advert against the MARKET RATE decides how many.")
		y += 6.0
	# THE CORAL BUDGET, COUNTED (§1.1: at most two warning lines a pane, §2.8: at
	# most two inline marks). The footer's own warning spends one of the two before
	# the page is drawn, so the role rows are handed what is left; past it the same
	# sentence is still printed, in ink. Three coral lines and the founder stops
	# seeing any of them.
	var budget := {"coral": 2 - (1 if _warning(b, state) != "" else 0)}
	for row in state.open_roles:
		y = _role_row(b, state, row as Dictionary, y, budget)
	y = _open_line(b, state, y)
	y = _recruiter_row(b, state, y)
	y = DeskKit.rule(b, y + 2.0)
	# THE PEOPLE THE ADVERT BROUGHT IN. Six cards, then the truth about the rest.
	if state.applicants.is_empty():
		if not state.open_roles.is_empty():
			y = DeskKit.empty(b, Vector2(10.0, y), "nobody has answered yet.",
				"an advert under 0.8× the market rate draws silence, not bargains.")
	else:
		var room: int = clampi(int((690.0 - y) / ROW_H), 1, DeskKit.LIST_CAP)
		var shown := 0
		for i in state.applicants.size():
			if shown >= room:
				break
			y = _applicant_card(b, state, i, y)
			shown += 1
		if shown < state.applicants.size():
			y = DeskKit.more(b, Vector2(10.0, y), state.applicants.size() - shown)
	DeskKit.footer(b, {"computed": _hiring_computed(b, state), "rules": _rules(state),
		"warning": _warning(b, state)})

static func _role_row(b, state: GameState, row: Dictionary, y: float,
		budget: Dictionary = {}) -> float:
	var role := String(row.get("role", "engineer"))
	var market := SimLabor.market_salary(role, state.era)
	var offered := int(row.get("offered_salary", market))
	var ratio := float(offered) / maxf(float(market), 1.0)
	var waiting := SimLabor.waiting_for(state, role)
	var lam := SimLabor.arrival_rate(state, row)
	var steps := SimLabor.salary_steps(market, 2.0)
	var thin := ratio < 0.8
	var flow := "nobody applies at this rate" if lam < 0.05 else "about %.1f apply/wk" % lam
	DeskKit.stepper(b, y, {
		"name": "%s — advert" % role,
		"value": "$%s/wk" % b.fmt(offered),
		"effect": "market $%s · %.2f× · %s · %d waiting" % [b.fmt(market), ratio, flow, waiting],
		"at_min": DeskKit.at_min(steps, float(offered)),
		"at_max": DeskKit.at_max(steps, float(offered)),
		"pitch": 0.0,
		"on_minus": func() -> void:
			SimLabor.set_role_salary(state, role, int(DeskKit.ladder(steps, float(offered), -1))),
		"on_plus": func() -> void:
			SimLabor.set_role_salary(state, role, int(DeskKit.ladder(steps, float(offered), 1))),
	})
	if thin:
		# THE ENGINE'S OWN READ, printed before the player blames the game for a
		# week of silence: below 0.8× the market rate the applicant flow is ZERO.
		var left := int(budget.get("coral", 2))
		budget["coral"] = left - 1
		b.label("%.2f× market — under 0.8× nobody applies at all." % ratio,
			Vector2(10.0, y + 34.0), DeskKit.DETAIL,
			DeskKit.PEN if left > 0 else Color(DeskKit.INK, 0.6), 460.0)
	else:
		b.label("the advert against the MARKET RATE decides who answers",
			Vector2(10.0, y + 34.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 460.0)
	# AT x480 THIS WORD SAT INSIDE THE ADVERT'S OWN VALUE COLUMN, one line under
	# the coral number, so the row read as a price with a caption rather than a
	# number with a control (§1.4's column grammar: value at 520, effect at 688,
	# steppers at 1000/1064 — every band across the row is already spoken for).
	# The one free paper is under the row's own words, and the pitch grows to hold
	# it rather than borrowing the next row's.
	DeskKit.word(b, "close the role", Vector2(10.0, y + 62.0), func() -> void:
		SimLabor.close_role(state, role), DeskKit.DETAIL, Color(DeskKit.INK, 0.7), 190.0)
	y += 112.0
	if SimLabor.seat_cap(state.era) > 1:
		# HQ: one role row is a requisition batch, so the arrivals keep coming
		# until the seats are filled.
		var seats := int(row.get("seats", 1))
		y = DeskKit.stepper(b, y, {
			"name": "seats on it",
			"why": "one advert, several desks",
			"value": "%d" % seats,
			"effect": "%d hire%s before this role closes itself" % [seats, "" if seats == 1 else "s"],
			"at_min": seats <= 1,
			"at_max": seats >= SimLabor.seat_cap(state.era),
			"pitch": 76.0,
			"on_minus": func() -> void: SimLabor.set_seats(state, role, seats - 1),
			"on_plus": func() -> void: SimLabor.set_seats(state, role, seats + 1),
		})
	return y

## The one trailing line that opens what this era allows — and says plainly when
## there is no desk left to open anything for.
static func _open_line(b, state: GameState, y: float) -> float:
	if SimLabor.seats_left(state) <= state.open_roles.size():
		b.label("no desk left to open a role — this stage seats %d, and every one is spoken for."
			% state.staff_cap(), Vector2(10.0, y), DeskKit.DETAIL,
			Color(DeskKit.INK, 0.55), 1100.0)
		return y + 40.0
	b.label("+ open:", Vector2(10.0, y), DeskKit.STATUS, Color(DeskKit.INK, 0.7), 120.0)
	var x := 130.0
	for r in SimLabor.ROLE_ORDER:
		if not SimLabor.role_unlocked(r, state.era):
			continue
		if not SimLabor.open_role_row(state, r).is_empty():
			continue
		# −5, not −8: a word button centres its text in 46px, so at −8 the role names
		# sat five pixels above the "+ open:" they answer to.
		DeskKit.word(b, r, Vector2(x, y - 5.0), func() -> void:
			SimLabor.open_role(state, r, SimLabor.market_salary(r, state.era)),
			DeskKit.STATUS, DeskKit.INK, 160.0)
		x += 168.0
		if x > 980.0:
			break
	return y + 50.0

static func _recruiter_row(b, state: GameState, y: float) -> float:
	var cap := SimLabor.recruiter_cap(state.era)
	if cap <= 0:
		return y
	var n := SimLabor.recruiters_active(state)
	var steps: Array = []
	for i in cap + 1:
		steps.append(float(i))
	return DeskKit.stepper(b, y, {
		"name": "recruiters on retainer",
		"why": "applicant flow, bought by the week",
		"value": "%d × $%s/wk" % [n, b.fmt(SimLabor.RECRUITER_FEE)],
		"effect": ("applicant flow ×%.2f · −$%s/wk on the books" % [1.0 + 0.75 * float(n),
			b.fmt(SimLabor.RECRUITER_FEE * n)]) if n > 0 else "nobody is working your pipeline",
		"at_min": n <= 0,
		"at_max": n >= cap,
		"pitch": 78.0,
		"on_minus": func() -> void: SimLabor.set_recruiters(state, n - 1),
		"on_plus": func() -> void: SimLabor.set_recruiters(state, n + 1),
	})

static func _applicant_card(b, state: GameState, i: int, y: float) -> float:
	var a: Dictionary = state.applicants[i]
	var role := String(a.get("role", "engineer"))
	var market := SimLabor.market_salary(role, state.era)
	var ask := int(a.get("ask", 0))
	var waiting := state.week - int(a.get("applied_week", state.week))
	var voice := String(a.get("one_liner", "")).strip_edges()
	if voice == "":
		voice = String(a.get("quirk", "")).strip_edges()
	var flavor := "\"%s\"" % voice if voice != "" else "no notes — they let the number speak"
	flavor += " · waiting %d wk" % maxi(waiting, 0)
	if String(a.get("source", "inbound")) == "referral":
		flavor += " · referred by somebody on the team"
	var full := SimLabor.seats_left(state) <= 0
	var acts: Array = [
		{"text": "hire", "reason": "no desk left" if full else "",
		 "on": func() -> void: SimLabor.hire_applicant(state, i)},
		{"text": "pass", "on": func() -> void: SimLabor.reject_applicant(state, i)},
	]
	DeskKit.card(b, y, {
		"name": "%s — %s · asks $%s/wk (market $%s)" % [String(a.get("name", "?")), role,
			b.fmt(ask), b.fmt(market)],
		"pips": SimLabor.skill_of(a), "pips_x": PIPS_X,
		"flavor": flavor, "pitch": ROW_H, "actions": acts,
	})
	return y + ROW_H

static func _hiring_computed(b, state: GameState) -> String:
	if state.applicants.is_empty():
		return ""
	var best := 0
	var best_ask := 0
	for a in state.applicants:
		var ad: Dictionary = a
		if SimLabor.skill_of(ad) > best:
			best = SimLabor.skill_of(ad)
			best_ask = int(ad.get("ask", 0))
	return "%d waiting · best on the desk is skill %d at $%s/wk · a head lands on the payroll 2 wks before it is productive" % [
		state.applicants.size(), best, b.fmt(best_ask)]

# ═════════════════════════════ THE DESK'S LAWS ═══════════════════════════════

## The standing rules, in the terms the player is learning.
static func _rules(_state: GameState) -> String:
	return "the rules of this desk: the MARKET RATE is what the street pays for the role · a head costs more than a salary (FULLY-LOADED) · pay under market and ATTRITION compounds · SEVERANCE is tenure-banded, and grows up with the company"

## WARNINGS OUTRANK WISDOM (§2.7): when the era's own law is being broken, the
## rules line yields to the number that is breaking it.
static func _warning(b, state: GameState) -> String:
	if SimLabor.span_mult(state) < 1.0:
		return "SPAN OF CONTROL: %d heads under %d manager(s) — the floor runs at %d%% until somebody manages it" % [
			state.employees.size() - SimLabor.manager_count(state),
			SimLabor.manager_count(state), int(round(SimLabor.span_mult(state) * 100.0))]
	if SimLabor.benefits_short(state):
		return "BENEFITS: a real office expects $%s/wk on the office lever for %d staff, and you fund $%s — morale pays the difference" % [
			b.fmt(SimLabor.expected_benefits(state)),
			state.employees.size() + state.pipeline.size(),
			b.fmt(int(state.budgets.get("office", 0)))]
	return ""

## The role as a person, so a sentence about one reads like English.
static func _role_noun(role: String) -> String:
	match SimLabor.role_row(role):
		"engineer": return "an engineer"
		"sales": return "a sales head"
		"designer": return "a designer"
		"ops": return "an ops head"
		"support": return "a support head"
		"manager": return "a manager"
	return "a head"
