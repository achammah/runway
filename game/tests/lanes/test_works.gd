extends RefCounted
## LANE SUITE — the works (DAG2 W1 stub pins). Spec: docs/design/DAG2.md.
##
## The stub contract, pinned: the hooks are wired, the three new P&L lanes are
## pre-registered at zero, the identity still balances with them registered,
## and the site tags on existing records ride a tick untouched.
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/WorksTests.cs. Same checks, same order.

static func _state() -> GameState:
	var s := GameState.new()
	s.sim_seed = 4242
	s.week = 12
	s.cash = 60_000
	s.traction = 30
	s.product = 50
	s.morale = 70
	s.hype = 30
	s.biz_what = "Service"
	s.biz_who = "Consumer"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	return s

static func run(ok: Callable) -> void:
	# ── 1 · the stub speaks when spoken to, and says nothing
	var s0 := _state()
	ok.call(SimWorks.attention(s0).is_empty() and SimWorks.directives(s0).is_empty(),
		"works: the stub raises no attention and speaks no directives")

	# ── 2 · the pre-registered relief lane stays zero through a tick
	var s1 := _state()
	SimEngine.weekly_tick(s1)
	var pnl: Dictionary = s1.get_meta("pnl", {})
	ok.call(int(pnl.get("relief", -1)) == 0,
		"works: the neutral tick books no relief spend")

	# ── 3 · all three DAG2 lanes are pre-registered in the record, at zero.
	# `get(key, -1)`: a MISSING key reads -1, so this fails if a lane was
	# silently dropped from the record, not only if it billed.
	ok.call(int(pnl.get("recruit_ads", -1)) == 0 and int(pnl.get("relief", -1)) == 0
		and int(pnl.get("site_rent", -1)) == 0,
		"works: the three new pnl lanes are pre-registered at zero")

	# ── 4 · the identity the twin suite pins still balances with them inside
	ok.call(int(pnl.get("net", 1)) == int(pnl.get("revenue", 0)) - int(pnl.get("burn", 0))
		- int(pnl.get("liabilities_wk", 0)) - int(pnl.get("interest", 0))
		- int(pnl.get("tax", 0)),
		"works: the pnl identity holds with the new lanes registered")

	# ── 5 · a site tag on a person rides the tick untouched (the divisions
	# lane will read these; nothing may eat them meanwhile)
	var s2 := _state()
	s2.employees = [{"name": "June Park", "role": "therapist", "salary": 1_500,
		"burnout": 10, "quirk": "", "skill": 4, "hired_week": 3, "site": "site_lyon"}]
	SimEngine.weekly_tick(s2)
	ok.call(s2.employees.size() == 1
		and String((s2.employees[0] as Dictionary).get("site", "")) == "site_lyon",
		"works: a site tag on an employee survives the tick")
