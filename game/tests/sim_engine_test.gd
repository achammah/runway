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

	# ── THE SIX TRAITS: who the founder is, priced
	# The spreads below mirror data/archetypes.json on purpose — this suite is
	# about the RULES, and a rule that only holds for today's numbers is not one.
	var EXFAANG := {"charisma": 3, "luck": 2, "network": 4, "focus": 3,
		"credibility": 5, "stamina": 2}
	var HACKER := {"charisma": 1, "luck": 4, "network": 1, "focus": 5,
		"credibility": 2, "stamina": 4}

	var tl := _state()
	tl.traits = HACKER.duplicate()
	_ok(tl.trait_level("focus") == 5, "trait reads its archetype base")
	tl.items = ["itm_houseplant"]                    # +1 focus, −1 network
	_ok(tl.trait_level("focus") == 5, "a buff on a maxed trait clamps at 5")
	_ok(tl.trait_level("network") == 1, "a nerf on a floored trait clamps at 1")
	tl.traits = EXFAANG.duplicate()
	tl.items = ["itm_headphones"]                    # +2 focus, −1 network
	_ok(tl.trait_level("focus") == 5 and tl.trait_level("network") == 3,
		"item mods add to the base (focus %d, network %d)" % [tl.trait_level("focus"), tl.trait_level("network")])
	_ok(tl.item_trait_delta("focus") == 2, "the bag's own swing is readable on its own")

	# the owner's case: the ex-FAANG PM walks into the raise with the doors open
	var ex := _state()
	ex.traits = EXFAANG.duplicate()
	var ex_raise := SimEngine.roll_context(ex, "raise")
	_ok(bool(ex_raise.advantage) and String(ex_raise.adv_reasons[0]).begins_with("doors open"),
		"credibility 5 + network 4 = advantage on raise")
	var hk := _state()
	hk.traits = HACKER.duplicate()
	_ok(not bool(SimEngine.roll_context(hk, "raise").advantage),
		"credibility 2 + network 1 gets no such door")
	# and the bag can buy the door: 3+3 is six, the ring makes it eight
	var ring := _state()
	_ok(not bool(SimEngine.roll_context(ring, "raise").advantage), "a plain founder has no door")
	ring.items = ["itm_alumni_ring"]                 # +1 network, +1 credibility
	_ok(bool(SimEngine.roll_context(ring, "raise").advantage),
		"an item can open the investor doors")

	var ch := _state()
	ch.traits = {"charisma": 4, "luck": 3, "network": 3, "focus": 4,
		"credibility": 3, "stamina": 2}
	_ok(bool(SimEngine.roll_context(ch, "sell").advantage), "charisma 4 = advantage on sell")
	_ok(bool(SimEngine.roll_context(ch, "recruit").advantage), "charisma 4 = advantage on recruit")
	_ok(bool(SimEngine.roll_context(ch, "build").advantage), "focus 4 = advantage on build")
	_ok(not bool(SimEngine.roll_context(ch, "grit").disadvantage),
		"stamina 2 costs nothing while the founder is rested")
	ch.exhaustion = 3
	var tired := SimEngine.roll_context(ch, "grit")
	_ok(bool(tired.disadvantage) and (tired.dis_reasons as Array).has("no reserves"),
		"stamina 2 + exhaustion 3 = no reserves on grit")

	# LUCK bends the two extremes, deterministically, through the caller's dice
	var lucky := _state()
	lucky.traits = HACKER.duplicate()                # luck 4
	var lseq := [1, 12, 17]
	var li := [0]
	var lroll := func() -> int:
		var v: int = lseq[mini(li[0], lseq.size() - 1)]
		li[0] += 1
		return v
	var lucky_roll := SimEngine.roll_d20_ctx(lucky, "sell", lroll)
	_ok(int(lucky_roll.d20) == 17 and String(lucky_roll.luck_note) == "luck rerolls the 1",
		"luck 4 rerolls the natural 1 (kept %d)" % int(lucky_roll.d20))
	var plain_luck := _state()                       # luck 3: the 1 stands
	li[0] = 0
	_ok(int(SimEngine.roll_d20_ctx(plain_luck, "sell", lroll).d20) == 1,
		"luck 3 leaves the natural 1 exactly where it fell")
	var cursed := _state()
	cursed.traits = {"charisma": 4, "luck": 1, "network": 3, "focus": 3,
		"credibility": 4, "stamina": 2}
	var cseq := [20, 4]
	var ci := [0]
	var croll := func() -> int:
		var v: int = cseq[mini(ci[0], cseq.size() - 1)]
		ci[0] += 1
		return v
	var cursed_roll := SimEngine.roll_d20_ctx(cursed, "grit", croll)
	_ok(int(cursed_roll.d20) == 19 and String(cursed_roll.luck_note) == "never quite perfect",
		"luck 1 turns the natural 20 into a 19 (kept %d)" % int(cursed_roll.d20))

	# the room is warmer for people it already believes: same company, better terms
	var warm := _state()
	warm.traction = 500
	warm.last_growth = 0.10
	warm.investors = [{"name": "Fund A", "thesis": "momentum"}]
	warm.traits = {"charisma": 3, "luck": 2, "network": 5, "focus": 3,
		"credibility": 5, "stamina": 2}
	var cold := _state()
	cold.traction = 500
	cold.last_growth = 0.10
	cold.investors = warm.investors
	cold.traits = {"charisma": 3, "luck": 2, "network": 1, "focus": 3,
		"credibility": 1, "stamina": 2}
	var warm_offers := SimEngine.generate_offers(warm, warm.investors)
	var cold_offers := SimEngine.generate_offers(cold, cold.investors)
	_ok(absf(float((warm_offers[0] as Dictionary).warmth) - 8.0) < 0.01,
		"warmth caps at 8%% (got %.1f)" % float((warm_offers[0] as Dictionary).warmth))
	_ok(float((cold_offers[0] as Dictionary).warmth) == 0.0, "a cold room discounts nothing")
	_ok(float((warm_offers[0] as Dictionary).equity_pct) < float((cold_offers[0] as Dictionary).equity_pct),
		"the same company gives up less equity when the room is warm (%.1f%% < %.1f%%)" % [
			float((warm_offers[0] as Dictionary).equity_pct), float((cold_offers[0] as Dictionary).equity_pct)])
	_ok(float((warm_offers[0] as Dictionary).equity_pct) >= float((warm_offers[0] as Dictionary).fair_pct),
		"a warm offer is still never below fair")

	# and every rule the engine runs can say its own name
	var says := SimEngine.trait_effects(ex)
	var doors_said := false
	for line in says:
		if String(line).begins_with("doors open"):
			doors_said = true
	_ok(doors_said, "trait_effects reports the door it opened")
	_ok(SimEngine.TRAIT_RULES.size() == GameState.TRAIT_NAMES.size(),
		"every trait carries the words that explain it")

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

	# ── pricing: the demand curve discriminates (owner: no $500 massages)
	var mo := {"name": "massage", "unit": "per session", "fair_price": 70.0,
		"elasticity": 2.6, "unit_cost": 18.0, "price": 0.0, "weight": 1.0}
	_ok(SimEngine.offer_demand(mo, 70.0) > 0.95 and SimEngine.offer_demand(mo, 70.0) <= 1.05,
		"fair price = fair demand")
	_ok(SimEngine.offer_demand(mo, 500.0) < 0.01,
		"a $500 massage sells to ~nobody (%.4f)" % SimEngine.offer_demand(mo, 500.0))
	_ok(SimEngine.offer_demand(mo, 45.0) > 1.5, "a discount stokes demand")
	_ok(SimEngine.offer_demand(mo, 0.0) == 0.0, "unpriced = not on sale")
	var ps := _state()
	ps.traction = 100
	ps.set_flag("launched")
	ps.offers = [mo.duplicate()]
	var r_unp := SimEngine.weekly_tick(ps)
	# LAW OVERRULED by the owner ("10 customers but no money... IMPOSSIBLE"):
	# unpriced no longer earns zero — it bills at the going (fair) rate.
	_ok(int(r_unp.revenue) > 800, "unpriced offers bill at the going rate (%d)" % int(r_unp.revenue))
	var ps2 := _state()
	ps2.traction = 100
	ps2.set_flag("launched")
	var mo2 := mo.duplicate(); mo2["price"] = 70.0
	ps2.offers = [mo2]
	var r_fair := SimEngine.weekly_tick(ps2)
	_ok(int(r_fair.revenue) > 800, "fairly priced sessions pay the rent (%d)" % r_fair.revenue)
	var ps3 := _state()
	ps3.traction = 100
	ps3.set_flag("launched")
	var mo3 := mo.duplicate(); mo3["price"] = 500.0
	ps3.offers = [mo3]
	# THE LAW CHANGED (#196): greed starves acquisition and bleeds the base;
	# the overpriced pay full freight until they leave.
	_ok(SimEngine.offers_demand_mult(ps3) < 0.15,
		"greed starves adoption (mult %.2f)" % SimEngine.offers_demand_mult(ps3))
	_ok(SimEngine.offers_price_pain(ps3) > 1.5,
		"greed pains retention (pain %.2f)" % SimEngine.offers_price_pain(ps3))
	var ps_fair_run := _state(); ps_fair_run.traction = 40; ps_fair_run.cash = 100_000
	ps_fair_run.offers = [mo.duplicate()]
	var ps_greed_run := _state(); ps_greed_run.traction = 40; ps_greed_run.cash = 100_000
	ps_greed_run.offers = [mo3.duplicate()]
	for wk in range(8):
		SimEngine.weekly_tick(ps_fair_run)
		SimEngine.weekly_tick(ps_greed_run)
	_ok(ps_greed_run.traction < 40 and ps_greed_run.traction <= ps_fair_run.traction - 5,
		"greed bleeds the base while fair holds (%d vs %d)" % [ps_greed_run.traction, ps_fair_run.traction])
	# THE OWNER'S CASE, PINNED: 16 customers on a $70 weekly-cadence offer
	# read like founder math — hundreds per week, not $200.
	var own := _state()
	own.traction = 16
	own.offers = [{"name": "standard session", "unit": "per session",
		"price": 70.0, "fair_price": 45.0, "unit_cost": 18.0, "weight": 1.0}]
	var r_own := SimEngine.weekly_tick(own)
	_ok(int(r_own.revenue) >= 900 and int(r_own.revenue) <= 1300,
		"16 x $70 session reads like founder math (%d/wk)" % int(r_own.revenue))
	# THE BACKSTOP, PINNED (owner: "10 customers but no money... IMPOSSIBLE"):
	# an unpriced offer bills at the going rate. Zero revenue with customers
	# on the books cannot happen by algorithm.
	var np := _state()
	np.traction = 10
	np.offers = [{"name": "consulting session", "unit": "per session",
		"price": 0.0, "fair_price": 70.0, "unit_cost": 18.0, "weight": 1.0}]
	var r_np := SimEngine.weekly_tick(np)
	_ok(int(r_np.revenue) >= 550 and int(r_np.revenue) <= 850,
		"10 unpriced customers pay the going rate (%d/wk)" % int(r_np.revenue))
	_ok(SimEngine.offers_price_pain(np) == 1.0 and SimEngine.offers_demand_mult(np) >= 0.99,
		"fair billing carries no pain and fair demand")
	# THE OFFICE LANE: perks money buys morale, and it costs real burn.
	var of_a := _state(); of_a.cash = 100_000
	var of_b := _state(); of_b.cash = 100_000
	of_b.budgets["office"] = 2000
	for wk2 in range(8):
		SimEngine.weekly_tick(of_a)
		SimEngine.weekly_tick(of_b)
	_ok(of_b.morale > of_a.morale,
		"the office lane buys morale (%d vs %d)" % [of_b.morale, of_a.morale])
	_ok(of_a.cash - of_b.cash >= 8 * 1500,
		"office money is real burn (Δ$%d over 8 wks)" % (of_a.cash - of_b.cash))
	# THE LEARNING CURVE: serving 1000 customers cheapens serving ~34%.
	var lcs := _state()
	lcs.set_meta("served_total", 1000)
	_ok(SimEngine.learning_curve(lcs) > 0.6 and SimEngine.learning_curve(lcs) < 0.7,
		"the learning curve pays at scale (×%.2f)" % SimEngine.learning_curve(lcs))
	# THE P&L IDENTITY: the binder's record balances to the ledger.
	var pns := _state(); pns.traction = 10
	pns.offers = [{"name": "s", "unit": "per session", "price": 70.0,
		"fair_price": 45.0, "unit_cost": 18.0, "weight": 1.0}]
	SimEngine.weekly_tick(pns)
	var pnl: Dictionary = pns.get_meta("pnl", {})
	_ok(not pnl.is_empty() and int(pnl.net) == int(pnl.revenue) - int(pnl.burn) - int(pnl.liabilities_wk),
		"the P&L balances (net %d = rev %d − burn %d − standing %d)" % [
		int(pnl.get("net", 0)), int(pnl.get("revenue", 0)), int(pnl.get("burn", 0)), int(pnl.get("liabilities_wk", 0))])

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
