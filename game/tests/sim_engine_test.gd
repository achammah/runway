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

	# ── the day-one bank: the draft's promise IS the engine's grant
	_ok(GameState.START_CASH == 8000, "day-one bank pinned at $8,000")

	# ── the service consumer floor: a consumer session is a real price
	var sc_rng := RandomNumberGenerator.new()
	sc_rng.seed = 7
	var sc0: Dictionary = WorldGen.default_offers("Service", "Consumer", sc_rng)[0]
	_ok(WorldGen.SERVICE_CONSUMER_AUD == 0.4 and float(sc0["fair_price"]) >= 18.0
		and float(sc0["fair_price"]) <= 34.0,
		"service consumer floor: sessions bill in the 0.4 band")

	# ── the works un-bills BOTH halves of a walked unit (W4: a walked unit
	# takes its serving cost with it, by exactly the walked share)
	var wu := GameState.new()
	wu.sim_seed = 99
	wu.week = 6
	wu.biz_what = "Service"
	wu.biz_who = "SMB"
	wu.theta = SimEngine.default_theta("Service", "SMB")
	wu.offers = [{"name": "session", "unit": "per session", "fair_price": 60.0,
		"elasticity": 2.6, "unit_cost": 20.0, "price": 60.0, "price_set": true, "weight": 1.0}]
	wu.set_flag("launched")
	wu.traction = 200   # 200 sessions wanted vs the founder's ~26 hands
	var wm := {"revenue": 12000.0, "cogs": 4000.0, "relief": 0.0}
	SimWorks.tick_pre(wu, {})
	SimWorks.tick_money(wu, {}, wm)
	var wrec: Dictionary = wu.get_meta("works", {})
	var wshare := float(wrec.get("walk_units", 0.0)) / maxf(float(wrec.get("demand_units", 1.0)), 0.001)
	_ok(float(wm["cogs"]) < 4000.0 and float(wm["revenue"]) < 12000.0
		and absf((4000.0 - float(wm["cogs"])) - 4000.0 * wshare) < 1.0,
		"the works un-bills serving costs by exactly the walked share")

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
	lcs.served_total = 1000          # a saved FIELD now, not an Object meta
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
	_ok(SimBank.debt_total(ln) >= 11_800 and ln.loan_principal == 0,
		"18%%/wk compounds through the migrated note (owe %d)" % SimBank.debt_total(ln))

	# ── WAVE A: the four bugs the design corpus found (docs/design/DECISIONS.md)
	# 1 — price_offer was in the schema and the executor but not the validator,
	# so every DM reply that priced an offer was thrown away whole.
	_ok(EventGenerator.ALLOWED_OPS.has("price_offer"),
		"price_offer survives the ops validator")
	_ok(EventGenerator.ALLOWED_OPS.has("push_lead"), "push_lead is a live op")
	# the op list is ONE list at three sites — pin them equal, per engine
	var schema_ops: Array = LlmClient.ADJUDICATE_SCHEMA["properties"]["effects"]["items"]["properties"]["op"]["enum"]
	_ok(schema_ops.size() == EventGenerator.ALLOWED_OPS.size(),
		"schema enum and validator carry the same %d ops" % schema_ops.size())
	var ops_match := true
	for op_name in schema_ops:
		if not EventGenerator.ALLOWED_OPS.has(String(op_name)):
			ops_match = false
	_ok(ops_match, "every schema op is an allowed op")
	# 2 — the catalog cost-lines engine half (Godot had it, the C# twin did not)
	var cl := _state()
	var with_lines := SimEngine.add_offer(cl, "workshop", "per session", 200.0, 0.0, 2.0, 1.0,
		[{"label": "materials", "amount": 30.0}, {"label": "room hire", "amount": 20.0}],
		[{"label": "insurance", "amount": 45.0}])
	_ok(absf(float(with_lines.get("unit_cost", 0.0)) - 50.0) < 0.01,
		"unit cost is the sum of its variable lines (%.2f)" % float(with_lines.get("unit_cost", 0.0)))
	_ok(absf(float(with_lines.get("fixed_wk", 0.0)) - 45.0) < 0.01,
		"fixed_wk is the sum of its weekly lines (%.2f)" % float(with_lines.get("fixed_wk", 0.0)))
	_ok(absf(SimEngine.offers_fixed_wk(cl) - 45.0) < 0.01,
		"the catalog's weekly overhead reaches the engine")
	# a line above half of fair is clamped, and the total follows it down
	(with_lines["cost_lines"][0] as Dictionary)["amount"] = 5000.0
	SimEngine.sync_offer_costs(with_lines)
	_ok(float(with_lines.get("unit_cost", 0.0)) <= 200.0 * 0.9 + 0.01,
		"an itemised cost sheet still cannot exceed 90%% of fair")
	# the deep copy: two offers must never share one line object
	var copy: Dictionary = with_lines.duplicate(true)
	(copy["cost_lines"][0] as Dictionary)["amount"] = 1.0
	_ok(float((with_lines["cost_lines"][0] as Dictionary).get("amount", 0.0)) != 1.0,
		"duplicating an offer deep-copies its cost sheet")
	# the catalog overhead is a real P&L lane, not a silent cost
	var fx2 := _state()
	fx2.traction = 10
	SimEngine.add_offer(fx2, "kit", "per order", 100.0, 20.0, 2.0, 1.0, [],
		[{"label": "storage", "amount": 120.0}])
	SimEngine.weekly_tick(fx2)
	var fx2_pnl: Dictionary = fx2.get_meta("pnl", {})
	_ok(int(fx2_pnl.get("offer_fixed", 0)) == 120,
		"catalog overheads land in the P&L (%d)" % int(fx2_pnl.get("offer_fixed", 0)))
	# 3 — served_total is a FIELD: the learning curve used to reset on load
	var svd := _state()
	svd.traction = 25
	SimEngine.weekly_tick(svd)
	_ok(svd.served_total >= 25, "served_total accumulates on a real field (%d)" % svd.served_total)

	# ── THE SALT REGISTRY: names, never numbers, and 95 stays burned
	_ok(SimEngine.SALT_LABOR_ARRIVALS == 20 and SimEngine.SALT_RIVAL_ACTION == 30
		and SimEngine.SALT_PIPELINE == 50 and SimEngine.SALT_ROADMAP_SHIP == 70
		and SimEngine.SALT_MACRO_SHOCK == 80 and SimEngine.SALT_MNA == 100
		and SimEngine.SALT_HW_BREAKDOWN == 110,
		"the salt registry matches the spine's table")
	_ok(SimEngine.SALT_BURNED == 95, "salt 95 is burned, not assigned")

	# ── THE STATUS CATALOG's wave additions: installable by name, magnitudes
	# in one place, and the new effect keys stay out of the adoption loop.
	var stc := _state()
	_ok(SimEngine.add_status(stc, "price_war", 4) and SimEngine.add_status(stc, "board_delight", 3),
		"the wave's statuses install by name")
	_ok(not SimEngine.add_status(stc, "made_up_buff", 3), "the catalog still refuses inventions")
	_ok(absf(SimEngine.street_fair_mult(stc) - 0.92) < 0.001,
		"a price war drops the going rate (×%.2f)" % SimEngine.street_fair_mult(stc))
	_ok(bool(SimEngine.roll_context(stc, "raise").advantage),
		"board_delight warms the room for a raise")
	var plainc := _state()
	_ok(SimEngine.street_fair_mult(plainc) == 1.0, "no war, no discount on the street")
	# the price war is DEMAND-side: it never edits the founder's own numbers
	var warp := _state()
	warp.offers = [{"name": "s", "unit": "per session", "price": 70.0,
		"fair_price": 70.0, "unit_cost": 18.0, "weight": 1.0}]
	var fair_before := SimEngine.offers_price_pain(warp)
	SimEngine.add_status(warp, "price_war", 4)
	_ok(SimEngine.offers_price_pain(warp) > fair_before,
		"holding your price through a war reads as expensive (%.2f > %.2f)" % [
		SimEngine.offers_price_pain(warp), fair_before])
	_ok(float((warp.offers[0] as Dictionary).get("fair_price", 0.0)) == 70.0,
		"a rival never mutates the founder's own fair price")

	# ── THE P&L IDENTITY v2, both lines, on a week with every lane present
	var idn := _state()
	idn.set_flag("launched")
	idn.traction = 120
	idn.loan_principal = 5_000
	idn.budgets = {"ads": 800, "content": 200, "sales": 400, "care": 300, "rnd": 600, "office": 250}
	idn.offers = [{"name": "s", "unit": "per session", "price": 40.0,
		"fair_price": 38.0, "unit_cost": 12.0, "weight": 1.0,
		"fixed_lines": [{"label": "tools", "amount": 60.0}], "fixed_wk": 60.0}]
	idn.commitments.append({"name": "the van", "cash_wk": -150, "weeks_left": 6})
	var saw_interest := false
	var saw_standing := false
	for _w in 8:
		idn.week += 1
		SimEngine.weekly_tick(idn)
		var p: Dictionary = idn.get_meta("pnl", {})
		var lanes_sum := int(p.get("cogs", 0)) + int(p.get("rent", 0)) + int(p.get("payroll", 0)) \
			+ int(p.get("infra", 0)) + int(p.get("marketing", 0)) + int(p.get("sales", 0)) \
			+ int(p.get("care", 0)) + int(p.get("rnd", 0)) + int(p.get("office", 0)) \
			+ int(p.get("offer_fixed", 0)) + int(p.get("severance", 0)) + int(p.get("recruiting", 0)) \
			+ int(p.get("production", 0)) + int(p.get("subcontract", 0)) \
			+ int(p.get("equip_upkeep", 0)) + int(p.get("carrying", 0)) + int(p.get("incident", 0)) \
			+ int(p.get("recruit_ads", 0)) + int(p.get("relief", 0)) \
			+ int(p.get("site_rent", 0)) + int(p.get("feature_keep", 0))
		_ok(absi(int(p.get("burn", 0)) - lanes_sum) <= 1,
			"wk%d burn is the sum of its operating lanes (%d vs %d)" % [idn.week, int(p.get("burn", 0)), lanes_sum])
		_ok(int(p.get("net", 0)) == int(p.get("revenue", 0)) - int(p.get("burn", 0))
			- int(p.get("liabilities_wk", 0)) - int(p.get("interest", 0)) - int(p.get("tax", 0)),
			"wk%d net = revenue − burn − standing − interest − tax" % idn.week)
		if int(p.get("interest", 0)) > 0:
			saw_interest = true
			# the whole point of moving interest before the record: burn is
			# OPERATING spend, and the cost of debt sits outside it
			_ok(int(p.get("burn", 0)) < int(p.get("revenue", 0)) - int(p.get("net", 0)),
				"wk%d burn excludes the interest that also hit the week" % idn.week)
		if int(p.get("liabilities_wk", 0)) > 0:
			saw_standing = true
	_ok(saw_interest, "the loan's interest reaches the ledger instead of vanishing")
	_ok(saw_standing, "the standing-commitments lane reaches the ledger")

	# ── THE ATTENTION REGISTRY: one function behind every bang
	var at0 := _state()
	at0.offers = []
	at0.set_meta("pnl", {"net": 500})
	_ok(SimEngine.attention_items(at0).is_empty(), "a calm company raises no hands")
	var at1 := _state()
	at1.offers = [{"name": "consulting", "unit": "per session", "price": 0.0,
		"fair_price": 70.0, "unit_cost": 18.0, "weight": 1.0}]
	var rows := SimEngine.attention_items(at1)
	var saw_unpriced := false
	for r in rows:
		if String((r as Dictionary).get("key", "")) == "unpriced":
			saw_unpriced = true
			_ok(String((r as Dictionary).get("desk", "")) == "pricing",
				"the unpriced row points at the pricing desk")
			_ok(String((r as Dictionary).get("label", "")).length() <= 40,
				"a ticker label fits the garage HUD (%d chars)" % String((r as Dictionary).get("label", "")).length())
	_ok(saw_unpriced, "an offer billing at the going rate raises its hand")
	var at2 := _state()
	at2.set_flag("fundraising_open")
	at2.set_meta("pnl", {"net": -900})
	var rows2 := SimEngine.attention_items(at2)
	_ok(rows2.size() >= 2, "losing money and open term sheets both register")
	_ok(int((rows2[0] as Dictionary).get("severity", 0)) >= int((rows2[rows2.size() - 1] as Dictionary).get("severity", 0)),
		"the loudest item sorts first")
	_ok(SimEngine.attention_severity(at2, "the raise") == 3, "term sheets are an alarm")
	_ok(SimEngine.attention_severity(at2, "product") == 0, "a quiet desk wears no bang")
	# THE PRE-ROLL REVIEW: the engine half — what is worth stopping a roll for
	_ok(SimEngine.preroll_items(at0).is_empty(), "nothing outstanding = no review card")
	var pr := SimEngine.preroll_items(at2)
	_ok(pr.size() > 0, "the review card has something to say before the dice")
	var pr_min_sev := 3
	for r2 in pr:
		pr_min_sev = mini(pr_min_sev, int((r2 as Dictionary).get("severity", 1)))
	_ok(pr_min_sev >= 2, "the review card never stops a roll over a mere note")

	# ── THE DIRECTIVE CAP: the composer truncates, the subsystems never do
	var many: Array = []
	for i in 40:
		many.append("- line %d that runs on for a while to eat the character budget" % i)
	var capped := SimEngine.cap_directives(many)
	_ok(capped.size() <= SimEngine.DIRECTIVE_MAX_LINES, "the directive block caps at 24 lines")
	var cap_chars := 0
	for l in capped:
		cap_chars += String(l).length() + 1
	_ok(cap_chars <= SimEngine.DIRECTIVE_MAX_CHARS, "the directive block caps at 1200 chars")
	_ok(String(capped[0]) == String(many[0]), "priority is the order — line 1 is never dropped")

	# ── THE BUDGET MIGRATION: idempotent, and an old save spends identically
	var mig := _state()
	mig.budgets = {"marketing": 900, "sales": 100}
	SimEngine.migrate_budgets(mig)
	_ok(int(mig.budgets.get("ads", 0)) == 900 and not mig.budgets.has("marketing"),
		"legacy marketing money becomes paid ads")
	SimEngine.migrate_budgets(mig)
	_ok(int(mig.budgets.get("ads", 0)) == 900, "migrating twice does not double the money")
	_ok(int(mig.budgets.get("outbound", -1)) == 0, "the missing channels arrive at zero")

	# ── OLD SAVES MUST LOAD (docs/design/00-spine.md §8). The frozen pre-wave
	# fixture: load it through the REAL loader, prove every new field sits at
	# its default, then tick four weeks and come out finite and alive.
	var fx = JSON.parse_string(FileAccess.get_file_as_string("res://tests/fixtures/save_v2_prewave.json"))
	_ok(fx is Dictionary and int((fx as Dictionary).get("version", 0)) == 2,
		"the frozen fixture is a version-2 save")
	var old_state := SaveSystem.state_from_dict((fx as Dictionary).get("state", {}))
	_ok(old_state.week == 5 and old_state.company_name == "Fernwood Supply",
		"the pre-wave run loads (wk %d)" % old_state.week)
	_ok(old_state.served_total == 0 and old_state.open_roles.is_empty()
		and old_state.applicants.is_empty() and old_state.recruiters == 0
		and old_state.leads.is_empty() and old_state.logos.is_empty()
		and old_state.pipe_units == 0.0 and old_state.loans.is_empty()
		and old_state.bets.is_empty() and old_state.platform_level == 0
		and old_state.board.is_empty() and old_state.mna.is_empty()
		and old_state.hardware.is_empty() and old_state.content_equity == 0.0
		and old_state.option_pool_pct == 0.0 and old_state.founder_banked == 0
		and old_state.tax_loss_carry == 0 and old_state.macro_season == "steady",
		"every new subsystem field loads at its default")
	_ok(old_state.sites.is_empty() and old_state.price_book.is_empty()
		and old_state.topics.is_empty() and old_state.spend_book.is_empty()
		and old_state.esop.is_empty() and old_state.instruments.is_empty()
		and old_state.raise_state.is_empty() and old_state.recruitment.is_empty()
		and old_state.features.is_empty() and old_state.buyout_offer.is_empty(),
		"every DAG2 field loads at its default")
	_ok(int(old_state.budgets.get("ads", 0)) == 500 and not old_state.budgets.has("marketing"),
		"the old save's marketing budget migrated on load")
	for _wk in 4:
		old_state.week += 1
		var orep := SimEngine.weekly_tick(old_state)
		_ok(orep.has("lines"), "wk%d ticks a pre-wave save without error" % old_state.week)
	_ok(is_finite(float(old_state.cash)) and absi(old_state.cash) < 100_000_000,
		"four weeks on, the pre-wave run's cash is still a number ($%d)" % old_state.cash)
	_ok(not old_state.get_meta("pnl", {}).is_empty(), "a migrated run writes a full P&L record")

	# ── THE ROUND TRIP: a field the save dict forgets is a field that silently
	# stops persisting. The fixture above proves an OLD save still loads; this
	# proves a NEW one survives being written and read back. Both directions or
	# the save format is only half-checked.
	var rt := _state()
	rt.served_total = 4_321
	rt.open_roles = [{"role": "engineer", "offered_salary": 1600, "opened_week": 3, "seats": 1}]
	rt.applicants = [{"name": "Ade Okafor", "role": "engineer", "skill": 4, "ask": 1750}]
	rt.recruiters = 1
	rt.content_equity = 12.5
	rt.leads = [{"name": "Meridian Foods", "seats": 40, "stage": "pilot", "heat": 62}]
	rt.logos = [{"name": "Harbor Group", "seats": 25, "since_wk": 9, "renewal_wk": 35}]
	rt.pipe_units = 17.25
	rt.pipe_churn_acc = 0.4
	rt.pipe_stats = {"signed": 2, "lost": 1, "seats_signed": 65}
	rt.loans = [{"kind": "bank", "principal": 40_000, "balance": 33_500,
		"rate_wk": 0.004, "term_wk": 52, "taken_week": 6, "pay_wk": 820, "missed": 0}]
	rt.tax_loss_carry = 9_100
	rt.last_round_amount = 250_000
	rt.receivables = [{"name": "Harbor invoice", "cash_wk": 4_000, "weeks_left": 1}]
	rt.bets = [{"id": "bet_w7_1", "name": "the mobile app", "kind": "reach",
		"ambition": 2, "cost_rnd_weeks": 6.0, "progress": 2.5, "committed": true}]
	rt.platform_level = 2
	rt.board = {"target_revenue": 8_000, "review_week": 24, "strikes": 1, "goodwill": 2}
	rt.mna = {"buyer": "Larkspur Depot", "price": 2_400_000, "expires_week": 30}
	rt.mna_last_week = 22
	rt.option_pool_pct = 10.0
	rt.founder_banked = 180_000
	rt.macro_season = "winter"
	rt.hardware = {"stock": 48, "capacity_base": 6.0, "equipment": [
		{"id": "press_1", "name": "the press", "capacity_add": 4.0,
			"upkeep_wk": 60.0, "bought_week": 8, "site": "site_lyon"}],
		"production_target": 12, "produced_total": 310, "subcontract_on": true,
		"demand_ema": 9.5}
	# ── DAG2 W1: every new field populated, plus the site/product tags that
	# ride EXISTING records — a tag the save forgets is a division that
	# silently dissolves on load.
	rt.sites = [{"id": "site_lyon", "name": "Lyon", "rent_wk": 2_600,
		"wage_mult": 0.92, "learning_count": 140, "demand_weight": 0.35,
		"opened_wk": 9}]
	rt.price_book = {"open_site_pack": 18_000, "relocation_fee": 400,
		"machine_shipping": 900, "lease_break_weeks": 8,
		"contract_notice_wks": 4, "refinance_break_fee": 350,
		"freelance_rate": 65, "subcontract_rate": 30,
		"account_fire_penalty": 1_200}
	rt.topics = {"growth_plots": ["the garden"], "works_term": "the studio"}
	rt.spend_book = [{"name": "staff meals", "buys": "the kitchen fed",
		"amt": 220, "bucket": "office", "contract_notice": 0, "division": ""}]
	rt.esop = {"pool_pct": 10.0, "granted": [
		{"emp_id": "june_park", "pct": 0.4, "vest_start_wk": 12}]}
	rt.instruments = [{"kind": "safe", "holder": "Fern Capital",
		"amount": 150_000, "cap": 4_000_000, "discount": 0.2, "rate": 0.0,
		"maturity_wk": 0, "pct": 0.0, "prefs": 0.0, "protective": false,
		"drag_threshold": 0.0, "signed_wk": 9}]
	rt.raise_state = {"stages": [], "interest_score": 22.5, "active": true,
		"founder_time_tax": 0.15}
	rt.recruitment = {"roles": [{"role": "designer"}], "candidates": [],
		"offers_out": []}
	rt.features = [{"id": "ft_booking", "name": "online booking", "job": "pull",
		"family": "", "solidity": "solid", "keep_wk": 40, "unit_cost_add": 0.0,
		"product_id": "", "born_wk": 1, "measured": 0.0}]
	rt.buyout_offer = {"buyer": "Larkspur Depot", "cash": 1_200_000}
	rt.attention_ages = {"pricing/unpriced": 3}
	rt.employees = [{"name": "June Park", "role": "engineer", "salary": 1_500,
		"burnout": 10, "quirk": "", "skill": 4, "hired_week": 3,
		"site": "site_lyon"}]
	rt.offers = [{"name": "the massage", "unit": "per session",
		"fair_price": 80.0, "unit_cost": 20.0, "elasticity": 2.0,
		"weight": 1.0, "price": 80.0, "price_set": true,
		"product_id": "prod_flagship"}]
	# JSON is the wire the real save travels on — round-trip through it, not
	# through a live object reference that would pass no matter what
	var rt_doc = JSON.parse_string(JSON.stringify(SaveSystem.state_to_dict(rt)))
	_ok(rt_doc is Dictionary, "the save dict survives JSON")
	var rt2 := SaveSystem.state_from_dict(rt_doc as Dictionary)
	_ok(rt2.served_total == 4_321, "served_total persists (the learning curve remembers)")
	_ok(rt2.open_roles.size() == 1 and rt2.applicants.size() == 1 and rt2.recruiters == 1,
		"the labor market persists")
	_ok(absf(rt2.content_equity - 12.5) < 0.001, "content equity persists")
	_ok(rt2.leads.size() == 1 and rt2.logos.size() == 1
		and absf(rt2.pipe_units - 17.25) < 0.001 and int(rt2.pipe_stats.get("signed", 0)) == 2,
		"the pipeline persists")
	_ok(rt2.loans.size() == 1 and int((rt2.loans[0] as Dictionary).get("balance", 0)) == 33_500
		and rt2.tax_loss_carry == 9_100 and rt2.receivables.size() == 1,
		"the notes, the carryforward and the receivables persist")
	_ok(rt2.bets.size() == 1 and rt2.platform_level == 2, "the roadmap persists")
	_ok(int(rt2.board.get("review_week", 0)) == 24 and String(rt2.mna.get("buyer", "")) == "Larkspur Depot"
		and rt2.mna_last_week == 22 and rt2.founder_banked == 180_000
		and absf(rt2.option_pool_pct - 10.0) < 0.001 and rt2.macro_season == "winter",
		"the board, the offer and the banked cash persist")
	_ok(int(rt2.hardware.get("stock", 0)) == 48 and int(rt2.hardware.get("produced_total", 0)) == 310,
		"the factory persists")
	_ok(rt2.sites.size() == 1
		and int((rt2.sites[0] as Dictionary).get("rent_wk", 0)) == 2_600
		and int(rt2.price_book.get("open_site_pack", 0)) == 18_000,
		"the sites and the price book persist")
	_ok(String(rt2.topics.get("works_term", "")) == "the studio"
		and rt2.spend_book.size() == 1
		and int((rt2.spend_book[0] as Dictionary).get("amt", 0)) == 220,
		"the generated books persist (topics, spend book)")
	_ok(absf(float(rt2.esop.get("pool_pct", 0.0)) - 10.0) < 0.001
		and rt2.instruments.size() == 1
		and int((rt2.instruments[0] as Dictionary).get("cap", 0)) == 4_000_000
		and absf(float(rt2.raise_state.get("interest_score", 0.0)) - 22.5) < 0.001
		and (rt2.recruitment.get("roles", []) as Array).size() == 1,
		"the ownership cluster persists (pool, paper, raise, recruitment)")
	_ok(rt2.features.size() == 1
		and int((rt2.features[0] as Dictionary).get("keep_wk", 0)) == 40
		and String(rt2.buyout_offer.get("buyer", "")) == "Larkspur Depot",
		"the feature inventory and the buyout offer persist")
	_ok(String((rt2.employees[0] as Dictionary).get("site", "")) == "site_lyon"
		and String(((rt2.hardware.get("equipment", []) as Array)[0] as Dictionary).get("site", "")) == "site_lyon"
		and String((rt2.offers[0] as Dictionary).get("product_id", "")) == "prod_flagship",
		"the site tags and the product id persist on their records")
	_ok(int(rt2.attention_ages.get("pricing/unpriced", 0)) == 3,
		"the attention ages persist a save round-trip")
	# and the saved run still ticks — a round-tripped state is a LIVE state
	rt2.week += 1
	var rt_rep := SimEngine.weekly_tick(rt2)
	_ok(rt_rep.has("lines"), "a round-tripped run ticks without error")

	# ── THE ATTENTION AGES (DAG3): a row's first-seen week is recorded at the
	# tick, holds while the row stands, and vanishes with the resolved row;
	# attention_items stamps every row with since_wk and a control key.
	var ag := _state()
	ag.set_flag("fundraising_open")   # a stable sev-3 registry row
	SimEngine.weekly_tick(ag)
	var born := int(ag.attention_ages.get("the raise/term_sheets", -1))
	_ok(born == ag.week, "a new attention row is stamped with its first week")
	ag.week += 1
	SimEngine.weekly_tick(ag)
	_ok(int(ag.attention_ages.get("the raise/term_sheets", -1)) == born
		and ag.week - born == 1,
		"a stable attention item ages by 1 across two ticks")
	var aged_row := {}
	for r_ag in SimEngine.attention_items(ag):
		if String((r_ag as Dictionary).get("key", "")) == "term_sheets":
			aged_row = r_ag
	_ok(int(aged_row.get("since_wk", -1)) == born
		and String(aged_row.get("control", "?")) == "sign_terms",
		"attention rows carry since_wk and a control key")
	ag.flags.erase("fundraising_open")
	ag.week += 1
	SimEngine.weekly_tick(ag)
	_ok(not ag.attention_ages.has("the raise/term_sheets"),
		"a resolved attention item's key drops from the ages")

	# ── THE LANES: each suite runs its own pins after the engine's
	for lane_suite in [preload("res://tests/lanes/test_catalog.gd"),
			preload("res://tests/lanes/test_labor.gd"),
			preload("res://tests/lanes/test_street.gd"),
			preload("res://tests/lanes/test_funnel.gd"),
			preload("res://tests/lanes/test_pipeline.gd"),
			preload("res://tests/lanes/test_bank.gd"),
			preload("res://tests/lanes/test_roadmap.gd"),
			preload("res://tests/lanes/test_board.gd"),
			preload("res://tests/lanes/test_factory.gd"),
			preload("res://tests/lanes/test_divisions.gd"),
			preload("res://tests/lanes/test_money_desks.gd"),
			preload("res://tests/lanes/test_ownership.gd"),
			preload("res://tests/lanes/test_pivot.gd"),
			preload("res://tests/lanes/test_features.gd"),
			preload("res://tests/lanes/test_works.gd")]:
		lane_suite.run(_ok)

	if _failed:
		print("SIM ENGINE FAIL")
		quit(1)
		return
	print("%d checks held" % _checks)
	print("SIM ENGINE PASS")
	quit(0)
