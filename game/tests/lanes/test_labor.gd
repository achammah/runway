extends RefCounted
## LANE SUITE — labor. Spec: docs/design/02-labor-market.md §9 (the six twin pins).
##
## `tests/sim_engine_test.gd` calls run() after the engine's own checks and hands
## over `ok`, the same assert the whole suite uses: ok.call(condition, "what this
## pins").
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/LaborTests.cs. Same checks, same order, same
## logic — the two engines do not share PRNG internals, so nothing below pins a
## draw across them, only behaviour.

## The lane's own fixture: a coworking company, because the market does not open
## until there is a company to open it for.
static func _state(seed_v: int = 42) -> GameState:
	var s := GameState.new()
	s.sim_seed = seed_v
	s.week = 5
	s.era = "coworking"
	s.cash = 200_000
	s.traction = 40
	s.product = 50
	s.morale = 60
	s.hype = 0
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	return s

static func _tick(s: GameState, weeks: int) -> Array:
	var lines: Array = []
	for _i in weeks:
		s.week += 1
		var rep := SimEngine.weekly_tick(s)
		for l in rep.get("lines", []):
			lines.append(String(l))
		for e in rep.get("events", []):
			lines.append(String(e))
	return lines

static func _joined(lines: Array) -> String:
	return "\n".join(PackedStringArray(lines))

static func run(ok: Callable) -> void:
	# ── PIN 1: THE FLOOR, AND DETERMINISM ────────────────────────────────────
	# Below 80% of the market rate nobody applies at all — the reservation wage
	# is a cliff, not a slope, and the desk has to be able to teach that.
	var mk_eng := SimLabor.market_salary("engineer", "coworking")
	var quiet := _state()
	ok.call(SimLabor.open_role(quiet, "engineer", int(float(mk_eng) * 0.7)),
		"a role opens at 0.70x market")
	_tick(quiet, 4)
	ok.call(quiet.applicants.is_empty(),
		"nobody applies below the 0.8x reservation wage (4 wks of silence)")

	var loud := _state()
	loud.hype = 40
	loud.morale = 70
	SimLabor.open_role(loud, "engineer", mk_eng)
	_tick(loud, 3)
	ok.call(loud.applicants.size() >= 1, "an advert AT the market rate draws people")
	var twin := _state()
	twin.hype = 40
	twin.morale = 70
	SimLabor.open_role(twin, "engineer", mk_eng)
	_tick(twin, 3)
	var same := twin.applicants.size() == loud.applicants.size()
	if same:
		for i in loud.applicants.size():
			var a: Dictionary = loud.applicants[i]
			var b: Dictionary = twin.applicants[i]
			if String(a.get("name", "")) != String(b.get("name", "")) \
					or int(a.get("skill", 0)) != int(b.get("skill", 0)) \
					or int(a.get("ask", 0)) != int(b.get("ask", 0)):
				same = false
				break
	ok.call(same, "the same seed draws the same people, name for name")

	# ── PIN 2: ASK BOUNDS + THE DECAY WINDOW ─────────────────────────────────
	# Morale stays under 70 here on purpose: the referral discount is a real
	# 0.95x on the first arrival of a week, and it would sit outside the band
	# this pin exists to police.
	var flood := _state()
	flood.hype = 80
	SimLabor.open_role(flood, "engineer", int(float(mk_eng) * 1.5))
	_tick(flood, 3)
	ok.call(flood.applicants.size() >= 6, "an advert at 1.5x market floods the desk")
	var bounded := true
	for a2 in flood.applicants:
		var ad: Dictionary = a2
		var sk := int(ad.get("skill", 0))
		if sk < 1 or sk > 5:
			bounded = false
			break
		var base := float(mk_eng) * float(SimLabor.SKILL_ASK[sk])
		var ask := float(int(ad.get("ask", 0)))
		if ask < base * 0.90 - 5.0 or ask > base * 1.15 + 5.0:
			bounded = false
			break
	ok.call(bounded, "every ask sits on the skill curve within its noise band")

	var patient := _state()
	var w0 := patient.week
	patient.applicants.append({"name": "Halden Rook", "role": "engineer", "skill": 3,
		"ask": mk_eng, "quirk": "waits", "one_liner": "", "applied_week": w0,
		"source": "inbound"})
	_tick(patient, 1)
	var here_1 := _has_applicant(patient, "Halden Rook")
	_tick(patient, 1)
	ok.call(here_1 and _has_applicant(patient, "Halden Rook"),
		"two weeks of grace: a fresh candidate never evaporates")
	var gone_lines := _tick(patient, 3)
	ok.call(not _has_applicant(patient, "Halden Rook"),
		"the offer shelf-life is hard: gone by week five, whatever the seed")
	ok.call(_joined(gone_lines).contains("Halden Rook"),
		"and the desk printed why they stopped waiting")

	# ── PIN 3: THE ERA GATES ─────────────────────────────────────────────────
	ok.call(SimLabor.severance_weeks("garage", 10) == 1
		and SimLabor.severance_weeks("coworking", 10) == 2,
		"severance: a garage handshake is 1 wk, a coworking exit 2")
	ok.call(SimLabor.severance_weeks("office", 10) == 2
		and SimLabor.severance_weeks("office", 30) == 3
		and SimLabor.severance_weeks("office", 100) == 4
		and SimLabor.severance_weeks("hq", 10) == 3,
		"severance bands by tenure at office, and the hq floor is 3")
	ok.call(not SimLabor.role_unlocked("manager", "coworking")
		and SimLabor.role_unlocked("manager", "floor"),
		"managers unlock with the floor they manage")
	ok.call(not SimLabor.role_unlocked("designer", "garage")
		and SimLabor.role_unlocked("designer", "coworking"),
		"specialists unlock when there is a company to specialise in")

	var thin := _state()
	thin.era = "floor"
	for _i in 12:
		thin.employees.append({"name": "IC", "role": "engineer", "salary": 2600,
			"burnout": 10, "skill": 3, "hired_week": 1})
	ok.call(absf(SimLabor.span_mult(thin) - 0.5) < 0.001,
		"12 heads and no manager: the floor runs at the 50% floor")
	for _i in 2:
		thin.employees.append({"name": "Mgr", "role": "manager", "salary": 3000,
			"burnout": 10, "skill": 3, "hired_week": 1})
	ok.call(absf(SimLabor.span_mult(thin) - 1.0) < 0.001,
		"two managers cover twelve reports: span is whole again")
	thin.era = "office"
	ok.call(absf(SimLabor.span_mult(thin) - 1.0) < 0.001,
		"below the floor era the founder manages everyone: span is always 1.0")

	var lean := _office_twin(0)
	var fed := _office_twin(1_000)
	_tick(lean, 4)
	_tick(fed, 4)
	ok.call(lean.morale < fed.morale,
		"a real office expects benefits: the unfunded twin's morale is lower")

	# ── PIN 4: THE HIRE FLOW, AND SKILL PAYS ─────────────────────────────────
	var hiring := _state()
	SimLabor.open_role(hiring, "engineer", mk_eng)
	hiring.applicants.append({"name": "Mara Voss", "role": "engineer", "skill": 5,
		"ask": 2_400, "quirk": "negotiates via long silences", "one_liner": "",
		"applied_week": hiring.week, "source": "inbound"})
	var hired: Dictionary = SimLabor.hire_applicant(hiring, 0)
	ok.call(not hired.is_empty() and hiring.pipeline.size() == 1
		and int((hiring.pipeline[0] as Dictionary).get("salary", 0)) == 2_400
		and hiring.open_roles.is_empty(),
		"the ASK is the contract, and the last seat closes the role")
	hiring.week += 1
	var rep1 := SimEngine.weekly_tick(hiring)
	ok.call(hiring.employees.is_empty() and int(rep1.get("burn", 0)) >= 2_400,
		"onboarding is paid before it is productive")
	hiring.week += 1
	SimEngine.weekly_tick(hiring)
	ok.call(hiring.employees.size() == 1
		and int((hiring.employees[0] as Dictionary).get("skill", 0)) == 5
		and int((hiring.employees[0] as Dictionary).get("hired_week", -1)) == hiring.week,
		"the graduate carries the skill that was hired, and a tenure clock")

	var closers := _sales_twin(5)
	var duds := _sales_twin(1)
	closers.week += 1
	duds.week += 1
	var rep_hi := SimEngine.weekly_tick(closers)
	var rep_lo := SimEngine.weekly_tick(duds)
	ok.call(int(rep_hi.get("adds", 0)) > int(rep_lo.get("adds", 0)),
		"two closers land more than two duds: skill IS the capacity")

	# ── PIN 5: THE FIRE RECEIPT ──────────────────────────────────────────────
	var boss := _fire_twin()
	var morale_before := boss.morale
	var slip: Dictionary = SimLabor.fire_employee(boss, 0)
	ok.call(boss.employees.is_empty() and int(slip.get("severance", 0)) == 3_000
		and boss.morale == morale_before - 8,
		"letting go: 2 wks of $1,500 owed, and the room takes the -8")
	var control := _fire_twin()
	control.employees.remove_at(0)          # the same roster, no invoice
	boss.week += 1
	control.week += 1
	SimEngine.weekly_tick(boss)
	SimEngine.weekly_tick(control)
	var pnl: Dictionary = boss.get_meta("pnl", {})
	var plain: Dictionary = control.get_meta("pnl", {})
	ok.call(int(pnl.get("severance", 0)) == 3_000
		and int(pnl.get("burn", 0)) >= int(plain.get("burn", 0)) + 3_000,
		"the invoice lands on NEXT week's books, in its own P&L lane")
	ok.call(int(pnl.get("net", 0)) == int(pnl.get("revenue", 0)) - int(pnl.get("burn", 0))
		- int(pnl.get("liabilities_wk", 0)) - int(pnl.get("interest", 0))
		- int(pnl.get("tax", 0)),
		"and the P&L identity still holds the week severance is paid")

	# ── PIN 6: THE UNDERPAY LADDER, AND THE POACH QUERY ──────────────────────
	var stiffed := _state()
	stiffed.morale = 70
	stiffed.employees.append({"name": "Nico Bell", "role": "engineer", "salary": 750,
		"burnout": 10, "skill": 3, "hired_week": 1})
	stiffed.week += 1
	SimEngine.weekly_tick(stiffed)
	ok.call(bool((stiffed.employees[0] as Dictionary).get("wants_raise", false)),
		"paid half of fair: the ask is immediate, not a dice roll")
	var mark: Dictionary = SimLabor.poach_target(stiffed)
	ok.call(not mark.is_empty() and String(mark.get("name", "")) == "Nico Bell"
		and float(mark.get("gap_pct", 0.0)) >= 25.0,
		"a raider bids for exactly the person paid furthest under their worth")
	var quit_lines := _tick(stiffed, 3)
	ok.call(stiffed.employees.is_empty() and _joined(quit_lines).contains("resigned"),
		"three weeks of being ignored ends in a resignation, with the ratio")

	var paid := _state()
	paid.employees.append({"name": "Priya Ines", "role": "engineer", "salary": 1_350,
		"burnout": 10, "skill": 3, "hired_week": 1})
	ok.call(SimLabor.poach_target(paid).is_empty(),
		"nobody bids for a person already paid near the market")

	# ── THE SEAMS OTHER LANES CALL ───────────────────────────────────────────
	# The dressing plumbing and the poach handoff are cross-lane contracts, and
	# an untested contract between two lanes is the one that breaks.
	var seam := _seam_state()
	var payload: Dictionary = SimLabor.dressing_payload(seam)
	ok.call((payload.get("candidates", []) as Array).size() == 2
		and (payload.get("taken_names", []) as Array).has("Priya Voss"),
		"the dressing payload carries THIS week's arrivals, and the names already taken")
	ok.call(SimLabor.dress_applicants_rows(seam, [
			{"name": "Mara Voss", "quirk": "negotiates via long silences", "one_liner": "I fix what you broke."},
			{"name": "Bo Halloway", "quirk": "sends follow-ups at 5am sharp", "one_liner": "Available, alarmingly."}]) == 2
		and String((seam.applicants[0] as Dictionary).get("name", "")) == "Mara Voss"
		and int((seam.applicants[0] as Dictionary).get("ask", 0)) == 1_500
		and int((seam.applicants[0] as Dictionary).get("skill", 0)) == 3,
		"a dressing reply changes the words and never a number")
	ok.call(SimLabor.dress_applicants_rows(seam, [{"name": "Only One", "quirk": "", "one_liner": ""}]) == 0
		and SimLabor.dress_applicants_rows(seam, [
			{"name": "Priya Voss", "quirk": "", "one_liner": ""},
			{"name": "Someone", "quirk": "", "one_liner": ""}]) == 0
		and String((seam.applicants[0] as Dictionary).get("name", "")) == "Mara Voss",
		"a reply that miscounts or steals a name is discarded whole; the pool stands")
	var mark2: Dictionary = SimLabor.poach_target(seam)
	ok.call(int(mark2.get("index", -1)) == 0 and int(mark2.get("market_salary", 0)) == 1_875
		and float(mark2.get("pay_gap", 0.0)) >= 0.2,
		"the poach target answers in the shape the rivals lane asks for")
	SimLabor.poach_failed(seam, 0, "Vantage")
	ok.call(bool((seam.employees[0] as Dictionary).get("wants_raise", false))
		and int((seam.employees[0] as Dictionary).get("asked_week", -1)) == seam.week - 2
		and int(seam.get_meta("poach_wk", -1)) == seam.week,
		"a FAILED poach hardens the ask: two weeks already gone off the clock")
	ok.call(SimLabor.grant_raise(seam, 0, 1_875) == 1_875
		and not bool((seam.employees[0] as Dictionary).get("wants_raise", false))
		and SimLabor.poach_target(seam).is_empty(),
		"paying fair clears the ask and the raider loses interest in the same breath")
	var morale_was := seam.morale
	SimLabor.poach_lands(seam, 0, "Vantage")
	ok.call(seam.employees.is_empty() and seam.morale == morale_was - 6
		and seam.severance_due == 0,
		"a landed poach costs the head and the room — but never severance, they left")

	# THE HANDOFF THE RIVALS LANE ACTUALLY USES: it resolves its own poach at
	# tick §6a and leaves a marker; the next §3b reads it and the ask hardens.
	var courted := _state()
	courted.morale = 70
	courted.employees.append({"name": "Ivo Marsh", "role": "engineer", "salary": 900,
		"burnout": 10, "skill": 3, "hired_week": 1})
	courted.set_meta("poach_failed_wk", courted.week)
	courted.set_meta("poach_failed_name", "Ivo Marsh")
	courted.week += 1
	var courted_rep := SimEngine.weekly_tick(courted)
	# Either they are asking now or they already walked on the hardened clock —
	# the same lesson, and which one it is belongs to the dice, not to this pin.
	ok.call((courted.employees.is_empty()
			or bool((courted.employees[0] as Dictionary).get("wants_raise", false)))
		and int(courted.get_meta("poach_failed_wk", 0)) < 0
		and _joined(courted_rep.get("events", [])).contains("came back with a number"),
		"a failed poach the rivals lane resolved still starts counter-offer season, once")

## An underpaid star, two candidates who arrived this week and one who did not —
## everything the dressing call and the poach handoff have to get right.
static func _seam_state() -> GameState:
	var s := _state(7)
	s.week = 9
	s.company_name = "Pivotflow"
	s.founder_name = "Lena Voss"
	s.employees.append({"name": "Priya Voss", "role": "engineer", "salary": 700,
		"burnout": 10, "skill": 4, "hired_week": 2})
	s.applicants = [
		{"name": "Pool One", "role": "engineer", "skill": 3, "ask": 1_500, "quirk": "q1",
			"one_liner": "", "applied_week": 9, "source": "inbound"},
		{"name": "Pool Two", "role": "engineer", "skill": 5, "ask": 2_400, "quirk": "q2",
			"one_liner": "", "applied_week": 9, "source": "referral"},
		{"name": "Old Hand", "role": "sales", "skill": 2, "ask": 1_000, "quirk": "q3",
			"one_liner": "", "applied_week": 6, "source": "inbound"}]
	return s

static func _has_applicant(s: GameState, nm: String) -> bool:
	for a in s.applicants:
		if String((a as Dictionary).get("name", "")) == nm:
			return true
	return false

## Three office-era staff paid exactly the market rate, so nothing but the
## benefits expectation can move morale between the twins.
static func _office_twin(office_budget: int) -> GameState:
	var s := _state()
	s.era = "office"
	s.budgets["office"] = office_budget
	for _i in 3:
		s.employees.append({"name": "Staff", "role": "engineer", "salary": 2_000,
			"burnout": 10, "skill": 3, "hired_week": 1})
	return s

## A launched company with a real market in front of it, so demand is well past
## what two sellers can close and the gtm clamp is what decides the week.
static func _sales_twin(skill: int) -> GameState:
	var s := _state()
	s.set_flag("launched")
	s.traction = 600
	s.theta["tam"] = 5_000_000.0
	s.hype = 40
	var pay := int(round(float(SimLabor.market_salary("sales", "coworking"))
		* float(SimLabor.SKILL_ASK[skill])))
	for _i in 2:
		s.employees.append({"name": "Seller", "role": "sales", "salary": pay,
			"burnout": 10, "skill": skill, "hired_week": 1})
	return s

## One office-era engineer on $1,500 with ten weeks behind them: the 2-week band.
static func _fire_twin() -> GameState:
	var s := _state()
	s.era = "office"
	s.morale = 70
	s.employees.append({"name": "Ivo Marsh", "role": "engineer", "salary": 1_500,
		"burnout": 10, "skill": 3, "hired_week": s.week - 10})
	return s
