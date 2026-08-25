extends RefCounted
## LANE SUITE — funnel. Spec: docs/design/04-funnel-channels.md (§8 twin test pins).
##
## `tests/sim_engine_test.gd` calls run() after the engine's own checks and hands
## over `ok`, the same assert the whole suite uses: ok.call(cond, "what it pins").
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/FunnelTests.cs. Same checks, same order, same
## logic — the two engines do not share PRNG internals, so never pin a draw
## across them, only behaviour.

## The fixture every pin starts from: office era (reach ×1.00, full attribution),
## launched, no rivals — so a channel number is the CHANNEL's, not the weather's.
static func _st(who: String, product: int = 50) -> GameState:
	var s := GameState.new()
	s.sim_seed = 42
	s.week = 5
	s.era = "office"
	s.cash = 500_000
	s.product = product
	s.morale = 70
	s.hype = 40
	s.biz_what = "Software"
	s.biz_who = who
	s.rivals = []
	s.set_flag("launched")
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	return s

## CAPACITY OUT OF THE WAY. Comparing two channels means comparing what they
## BROUGHT, so the closing ceiling must never be the thing that answers: a
## sell-5 founder, two closers and a real sales budget.
static func _capacity(s: GameState) -> GameState:
	s.competences["sell"] = 5
	s.employees.append({"name": "Rhea", "role": "sales", "salary": 1200, "burnout": 10})
	s.employees.append({"name": "Otto", "role": "sales", "salary": 1200, "burnout": 10})
	s.budgets["sales"] = 20_000
	s.cash = 5_000_000
	return s

static func _signed(s: GameState, key: String) -> float:
	return SimFunnel.num(SimFunnel.funnel(s), "signed_" + key)

static func run(ok: Callable) -> void:
	# ── PIN 1: baseline, determinism, conservation ───────────────────────────
	# Nothing funded must leave the world exactly as it was: the seam hands the
	# spine back its own blended value at $0, which is 1.0.
	var base := _st("SMB")
	base.traction = 300
	SimFunnel.tick_pre(base, {"lines": []})
	ok.call(absf(SimFunnel.reach_mult(base, 0.0, 1.0) - 1.0) < 1e-9,
		"zero channel spend hands the spine back its own reach lever (×1.00)")

	var a := _st("SMB")
	a.traction = 300
	SimEngine.weekly_tick(a)
	var fa := SimFunnel.funnel(a)
	ok.call(SimFunnel.num(fa, "spend_total") == 0.0
		and SimFunnel.num(fa, "reach_total") == 0.0
		and SimFunnel.num(fa, "leads_total") == 0.0
		and SimFunnel.num(fa, "signed_ads") == 0.0
		and SimFunnel.num(fa, "signed_referrals") == 0.0
		and a.content_equity == 0.0,
		"an unfunded funnel reads zero — no reach, no leads, no equity")

	var b := _st("SMB")
	b.traction = 300
	SimEngine.weekly_tick(b)
	ok.call(a.cash == b.cash and a.traction == b.traction
		and absf(a.content_equity - b.content_equity) < 1e-12,
		"two identical states tick to identical cash, traction and content equity")

	# every arrival is assigned to exactly one source, and the parts sum to adds
	var cons := _capacity(_st("Consumer"))
	cons.traction = 1000
	cons.budgets["ads"] = 2000
	cons.budgets["content"] = 1000
	cons.budgets["referrals"] = 2000
	cons.budgets["outbound"] = 500
	cons.budgets["care"] = 1000
	SimEngine.weekly_tick(cons)
	var fc := SimFunnel.funnel(cons)
	var sum_all := SimFunnel.num(fc, "organic") + SimFunnel.num(fc, "wom")
	for k in SimFunnel.MIX:
		sum_all += SimFunnel.num(fc, "signed_" + k)
	ok.call(absf(sum_all - SimFunnel.num(fc, "adds")) < 1e-6,
		"attribution is exact: organic + word of mouth + Σ channels == adds (%.6f vs %.6f)"
			% [sum_all, SimFunnel.num(fc, "adds")])

	# ── PIN 2: ads are instant, content is week-1 weak, the garage discounts both
	var s_ads := _capacity(_st("SMB"))
	s_ads.traction = 300
	s_ads.budgets["ads"] = 2000
	SimEngine.weekly_tick(s_ads)
	var s_con := _capacity(_st("SMB"))
	s_con.traction = 300
	s_con.budgets["content"] = 2000
	SimEngine.weekly_tick(s_con)
	var att_ads := _signed(s_ads, "ads")
	var att_con := _signed(s_con, "content")
	ok.call(att_ads >= 3.0 * att_con and att_ads > 5.0,
		"week 1, $2k each: ads out-signs content 3:1 (%.1f vs %.1f) — the instant channel"
			% [att_ads, att_con])

	var s_gar := _capacity(_st("SMB"))
	s_gar.era = "garage"
	s_gar.traction = 300
	s_gar.budgets["ads"] = 2000
	SimEngine.weekly_tick(s_gar)
	ok.call(_signed(s_gar, "ads") < 0.4 * att_ads,
		"the garage discount: the same $2k of ads buys ×0.35 the reach (%.1f vs %.1f)"
			% [_signed(s_gar, "ads"), att_ads])

	# ── PIN 3: content beats ads over 12 weeks at equal total spend (SMB) ─────
	var arm_a := _capacity(_st("SMB"))
	arm_a.traction = 300
	arm_a.budgets["ads"] = 2000
	var arm_c := _capacity(_st("SMB"))
	arm_c.traction = 300
	arm_c.budgets["content"] = 2000
	var cum_a := 0.0
	var cum_c := 0.0
	var last_a := 0.0
	var last_c := 0.0
	for i in 12:
		arm_a.week += 1
		arm_c.week += 1
		SimEngine.weekly_tick(arm_a)
		SimEngine.weekly_tick(arm_c)
		last_a = _signed(arm_a, "ads")
		last_c = _signed(arm_c, "content")
		cum_a += last_a
		cum_c += last_c
	ok.call(cum_c > cum_a,
		"12 weeks at $2k/wk: the library out-signs the auction (%.0f vs %.0f)" % [cum_c, cum_a])
	ok.call(last_c >= 1.5 * last_a,
		"and by week 12 content's weekly rate is 1.5× ads' (%.1f vs %.1f)" % [last_c, last_a])
	ok.call(arm_c.content_equity > 0.5 and arm_a.content_equity == 0.0,
		"the funded library compounded to %d%% equity; the unfunded one has none"
			% int(round(arm_c.content_equity * 100.0)))

	# ── PIN 4: referrals need a product worth vouching for ───────────────────
	var bad := _capacity(_st("Consumer", 10))
	bad.traction = 1000
	bad.budgets["referrals"] = 2000
	bad.budgets["care"] = 1000
	SimEngine.weekly_tick(bad)
	var good := _capacity(_st("Consumer", 80))
	good.traction = 1000
	good.budgets["referrals"] = 2000
	good.budgets["care"] = 1000
	SimEngine.weekly_tick(good)
	var att_bad := _signed(bad, "referrals")
	var att_good := _signed(good, "referrals")
	ok.call(att_bad == 0.0,
		"a v0.10 product has detractors, not promoters — the referral program signs nobody")
	ok.call(att_good > 5.0 and att_good >= 10.0 * att_bad,
		"at v0.80 with care funded the same $2k amplifies word of mouth (%.1f/wk)" % att_good)

	# ── PIN 5: outbound is Enterprise's channel, not Consumer's ──────────────
	var ent := _st("Enterprise")
	ent.traction = 20
	ent.budgets["ads"] = 2000
	ent.budgets["outbound"] = 2000
	SimEngine.weekly_tick(ent)
	ok.call(_signed(ent, "outbound") > _signed(ent, "ads"),
		"Enterprise: cold touch out-signs bought reach (%.2f vs %.2f)"
			% [_signed(ent, "outbound"), _signed(ent, "ads")])

	var cap_off := _st("Enterprise")
	cap_off.traction = 20
	var cap_on := _st("Enterprise")
	cap_on.traction = 20
	cap_on.budgets["outbound"] = 2000
	ok.call(SimFunnel.gtm_cap(cap_on) > SimFunnel.gtm_cap(cap_off) + 5.0,
		"outbound money is closing capacity too: cap %.1f → %.1f"
			% [SimFunnel.gtm_cap(cap_off), SimFunnel.gtm_cap(cap_on)])

	var con_ob := _capacity(_st("Consumer"))
	con_ob.traction = 1000
	con_ob.budgets["ads"] = 2000
	con_ob.budgets["outbound"] = 2000
	SimEngine.weekly_tick(con_ob)
	ok.call(_signed(con_ob, "outbound") < 0.2 * _signed(con_ob, "ads"),
		"Consumer: nobody answers a cold call (%.2f vs %.2f from ads)"
			% [_signed(con_ob, "outbound"), _signed(con_ob, "ads")])

	# ── PIN 6: migration and the DM's one marketing category ─────────────────
	var old := _st("SMB")
	old.traction = 300
	old.budgets = {"marketing": 2000, "sales": 0, "care": 0, "rnd": 0, "office": 0}
	SimEngine.weekly_tick(old)
	var pnl: Dictionary = old.get_meta("pnl", {})
	ok.call(int(old.budgets.get("ads", 0)) == 2000 and not old.budgets.has("marketing")
		and int(pnl.get("marketing", 0)) == 2000,
		"a legacy `marketing` budget becomes paid ads and still books as marketing spend")

	# the legacy set_marketing op's own field folds into the SAME ads lane
	var by_lever := _capacity(_st("SMB"))
	by_lever.traction = 300
	by_lever.budgets["ads"] = 2000
	SimEngine.weekly_tick(by_lever)
	var by_op := _capacity(_st("SMB"))
	by_op.traction = 300
	by_op.marketing_budget = 2000
	SimEngine.weekly_tick(by_op)
	ok.call(absf(SimFunnel.num(SimFunnel.funnel(by_lever), "spend_ads")
			- SimFunnel.num(SimFunnel.funnel(by_op), "spend_ads")) < 1e-9
		and absf(_signed(by_lever, "ads") - _signed(by_op, "ads")) < 1e-9,
		"the legacy marketing op buys exactly what the ads lever buys")

	var empty_mix := _st("SMB")
	SimFunnel.set_marketing(empty_mix, 2000)
	ok.call(int(empty_mix.budgets.get("ads", 0)) == 2000
		and int(empty_mix.budgets.get("content", 0)) == 0
		and int(empty_mix.budgets.get("referrals", 0)) == 0
		and int(empty_mix.budgets.get("outbound", 0)) == 0
		and empty_mix.marketing_budget == 0,
		"the DM's `marketing` on a cold start funds ads — the only channel that pays in week one")

	var curated := _st("SMB")
	curated.budgets["ads"] = 500
	curated.budgets["content"] = 1500
	SimFunnel.set_marketing(curated, 2000)
	ok.call(int(curated.budgets.get("ads", 0)) == 500
		and int(curated.budgets.get("content", 0)) == 1500
		and int(curated.budgets.get("referrals", 0)) == 0
		and int(curated.budgets.get("outbound", 0)) == 0,
		"and on a curated mix it splits by that mix — the narrator never overwrites it")
