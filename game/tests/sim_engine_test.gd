extends SceneTree
## SimEngine contract suite — hermetic, no network, no files.
## Run: godot --headless --path . --script tests/sim_engine_test.gd

var _checks := 0
var _failed := false

func _ok(cond: bool, msg: String) -> void:
	_checks += 1
	if not cond:
		_failed = true
		push_error("FAIL: " + msg)

func _state() -> GameState:
	var s := GameState.new()
	s.sim_seed = 42
	s.week = 5
	s.cash = 50_000
	s.traction = 40
	s.product = 50
	s.morale = 70
	s.hype = 40
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	return s

func _init() -> void:
	call_deferred("_go")

func _go() -> void:
	await process_frame

	# ── theta clamps hold against hostile input
	var mad := SimEngine.clamp_theta({"tam": 1e12, "adopt_p": 9.0, "lifetime_wk": -5})
	_ok(float(mad.tam) <= 5_000_000.0, "tam clamps down")
	_ok(float(mad.adopt_p) <= 0.004, "adopt_p clamps down")
	_ok(float(mad.lifetime_wk) >= 6.0, "lifetime clamps up")

	# ── determinism: same seed+week = identical tick
	var a := _state()
	var b := _state()
	SimEngine.weekly_tick(a)
	SimEngine.weekly_tick(b)
	_ok(a.cash == b.cash and a.traction == b.traction,
		"tick is deterministic for identical state")

	# ── the world grinds: an unlaunched idle company loses money
	var s0 := _state()
	s0.traction = 0
	var cash0 := s0.cash
	for i in 4:
		s0.week += 1
		SimEngine.weekly_tick(s0)
	_ok(s0.cash < cash0, "an idle company burns down (%d -> %d)" % [cash0, s0.cash])

	# ── churn punishes a bad product; a good one retains
	var bad := _state(); bad.product = 5
	var good := _state(); good.product = 95
	var rb := SimEngine.weekly_tick(bad)
	var rg := SimEngine.weekly_tick(good)
	_ok(int(rb.churn) > int(rg.churn), "worse product churns more (%d > %d)" % [rb.churn, rg.churn])

	# ── statuses: catalog-only, install, expire, affect the tick
	var st := _state()
	_ok(not SimEngine.add_status(st, "made_up_buff", 3), "unknown status refused")
	_ok(SimEngine.add_status(st, "viral_moment", 2), "catalog status installs")
	var boosted := SimEngine.weekly_tick(st)
	var st2 := _state()
	var plain := SimEngine.weekly_tick(st2)
	_ok(int(boosted.adds) > int(plain.adds), "viral_moment lifts adoption (%d > %d)" % [boosted.adds, plain.adds])
	SimEngine.weekly_tick(st)
	_ok(not SimEngine.has_status(st, "viral_moment"), "status expires on schedule")

	# ── clocks fire exactly once, at zero
	var ck := _state()
	SimEngine.add_clock(ck, 2, "the term sheet expires")
	var t1 := SimEngine.weekly_tick(ck)
	_ok((t1.fired_clocks as Array).is_empty(), "clock silent with a week left")
	var t2 := SimEngine.weekly_tick(ck)
	_ok((t2.fired_clocks as Array).has("the term sheet expires"), "clock fires at zero")
	_ok(ck.clocks.is_empty(), "fired clock is removed")

	# ── hiring pipeline: paid immediately, productive after onboarding
	var hp := _state()
	hp.pipeline.append({"name": "Priya", "role": "engineer", "salary": 1500, "weeks_in": 0})
	var r1 := SimEngine.weekly_tick(hp)
	_ok(hp.employees.is_empty(), "week 1: still onboarding")
	_ok(int(r1.burn) > 1500, "week 1: already on payroll")
	SimEngine.weekly_tick(hp)
	_ok(hp.employees.size() == 1, "week 2: productive")

	# ── advantage/disadvantage from state
	var av := _state()
	SimEngine.add_status(av, "data_room_ready", 3)
	var ctx := SimEngine.roll_context(av, "raise")
	_ok(bool(ctx.advantage), "data room grants advantage on raise")
	SimEngine.add_status(av, "investor_pressure", 3)
	ctx = SimEngine.roll_context(av, "raise")
	_ok(not bool(ctx.advantage) and not bool(ctx.disadvantage),
		"adv + dis cancel to a straight roll")
	av.exhaustion = 4
	var gctx := SimEngine.roll_context(av, "grit")
	_ok(bool(gctx.disadvantage), "exhaustion 4 = disadvantage on grit")

	# ── the 2d20 keep rule
	var seq := [3, 17]
	var i2 := [0]
	var roller := func() -> int:
		var v: int = seq[i2[0] % 2]
		i2[0] += 1
		return v
	var adv_roll := SimEngine.roll_d20_ctx(av, "raise", roller)   # cancels: straight
	_ok(int(adv_roll.d20) == 3, "straight roll takes the first die")
	i2[0] = 0
	var g2 := SimEngine.roll_d20_ctx(av, "grit", roller)
	_ok(int(g2.d20) == 3, "disadvantage keeps the WORST of 2d20")

	# ── margin bands
	_ok(SimEngine.margin_band(20, 12) == "brilliant", "beat by 5+ = brilliant")
	_ok(SimEngine.margin_band(12, 12) == "fine", "meet it = fine")
	_ok(SimEngine.margin_band(10, 12) == "risky", "miss by 1-2 = risky")
	_ok(SimEngine.margin_band(5, 12) == "backfired", "miss by 3+ = backfired")

	# ── funding: dilution math and the desperation spread
	var f := _state()
	f.traction = 500
	f.last_growth = 0.10
	var pre := SimEngine.valuation(f)
	_ok(pre > f.cash, "traction + growth beats cash-floor valuation")
	f.investors = [{"name": "Fund A", "thesis": "momentum"}]
	var offers := SimEngine.generate_offers(f, f.investors)
	_ok(offers.size() == 3, "three offers")
	for o in offers:
		_ok(float((o as Dictionary).equity_pct) >= float((o as Dictionary).fair_pct),
			"every offer is priced at or above fair")
	var broke := _state()
	broke.cash = -100
	broke.investors = f.investors
	var sharky := SimEngine.generate_offers(broke, broke.investors)
	_ok(float((sharky[0] as Dictionary).equity_pct) > float((offers[0] as Dictionary).fair_pct),
		"desperation prices against the founder")
	var fp := f.founder_pct
	SimEngine.apply_round(f, 100_000, 20.0)
	_ok(absf(f.founder_pct - fp * 0.8) < 0.01, "20%% round dilutes founder by exactly 20%%")
	_ok(f.rounds_raised.size() == 1 and String(f.rounds_raised[0]) == "pre-seed",
		"round ladder appends by count")

	# ── commitments recur then expire
	var cm := _state()
	cm.commitments.append({"name": "the lease deal", "cash_wk": -300, "weeks_left": 2})
	SimEngine.weekly_tick(cm)
	_ok(cm.commitments.size() == 1, "commitment persists mid-term")
	var rc := SimEngine.weekly_tick(cm)
	_ok(cm.commitments.is_empty() and (rc.expired as Array).has("the lease deal"),
		"commitment expires and is reported")

	# ── signals speak founder
	var sg := SimEngine.signals(_state())
	_ok(String(sg.health).begins_with("STABLE") or String(sg.health).begins_with("WARNING"),
		"health band renders")
	_ok(sg.has("runway_weeks") and sg.has("market_phase"), "signals carry the vitals")

	# ── the ledger levers are real money with real effects
	var lv := _state()
	lv.set_flag("launched")
	lv.traction = 600
	var plain_r := SimEngine.weekly_tick(lv)
	var lv2 := _state()
	lv2.set_flag("launched")
	lv2.traction = 600
	lv2.budgets = {"marketing": 0, "sales": 0, "care": 2000, "rnd": 0}
	var cared := SimEngine.weekly_tick(lv2)
	_ok(int(cared.churn) < int(plain_r.churn),
		"care budget retains (%s < %s churn)" % [str(cared.churn), str(plain_r.churn)])
	_ok(int(cared.burn) >= int(plain_r.burn) + 2000, "care budget is real burn")
	var lv3 := _state()
	lv3.budgets = {"marketing": 0, "sales": 0, "care": 0, "rnd": 2400}
	var p0 := lv3.product
	SimEngine.weekly_tick(lv3)
	_ok(lv3.product >= p0 + 2, "rnd budget ships product (+%d)" % (lv3.product - p0))
	var lv4 := _state()
	lv4.budgets = {"marketing": 3000, "sales": 1000, "care": 0, "rnd": 0}
	lv4.set_flag("launched")
	lv4.traction = 40
	var ue := SimEngine.weekly_tick(lv4)
	_ok(int(ue.get("cac", 0)) > 0 and int(ue.get("ltv", 0)) > 0,
		"unit economics computed (CAC %d, LTV %d)" % [ue.get("cac", 0), ue.get("ltv", 0)])

	# ── beliefs start wrong and converge with analytics
	var bl := _state()
	SimEngine.weekly_tick(bl)
	var wrong: float = absf(float(bl.beliefs["tam"]) - float(bl.theta["tam"]))
	bl.analytics_level = 2
	bl.traction = 60
	for i in 12:
		bl.week += 1
		SimEngine.weekly_tick(bl)
	var closer: float = absf(float(bl.beliefs["tam"]) - float(bl.theta["tam"]))
	_ok(closer < wrong * 0.6,
		"beliefs converge toward truth (gap %d -> %d)" % [int(wrong), int(closer)])

	# ── loan compounding punishes
	var ln := _state()
	ln.cash = 500
	ln.traction = 0
	ln.loan_principal = 10_000
	SimEngine.weekly_tick(ln)
	_ok(ln.loan_principal >= 11_800, "18%%/wk compounds (owe %d)" % ln.loan_principal)

	if _failed:
		print("SIM ENGINE FAIL")
		quit(1)
		return
	print("%d checks held" % _checks)
	print("SIM ENGINE PASS")
	quit(0)
