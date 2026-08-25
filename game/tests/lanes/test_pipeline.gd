extends RefCounted
## LANE SUITE — pipeline. Spec: docs/design/05-enterprise-pipeline.md §13.
##
## `tests/sim_engine_test.gd` calls run() after the engine's own checks and hands
## over `ok`, the same assert the whole suite uses: ok.call(condition, "what this
## pins"). Six checks, one per spec pin.
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/PipelineTests.cs. Same checks, same order, same
## logic — the two engines do not share PRNG internals, so never pin a draw
## across them, only behaviour. PIN 1 is the single exception and it is exact on
## purpose: `lead_advance_p` is closed-form with no RNG in it, so both engines
## must land on the same float to 1e-9 or the advance math has diverged.

## A garage Enterprise run with nothing bought and nothing priced: sell 3, no
## hires, budgets 0, product 50, offers empty.
static func _ent(week: int = 5) -> GameState:
	var s := GameState.new()
	s.sim_seed = 42
	s.week = week
	s.cash = 50_000
	s.product = 50
	s.morale = 70
	s.hype = 40
	s.biz_what = "Software"
	s.biz_who = "Enterprise"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	return s

static func run(ok: Callable) -> void:
	# ── PIN 1 — THE ADVANCE MATH IS EXACT, AND IT MOVES THE RIGHT WAY.
	# No RNG anywhere in lead_advance_p, so this is the one value both engines
	# can be held to. Longhand: BASE_ADV.meeting 0.45 × capacity clampf(3.9/1.5,
	# 0.5, 1.0) 1.0 × quality (0.6+0.5×0.8) 1.0 × price 1.0 × heat (0.5+0.55)
	# 1.05 × size jano_down(6, 10, 0.55) 0.8468976844843414.
	var s1 := _ent()
	var lead := {"name": "Meridian Logistics", "flavor": "", "seats": 6,
		"stage": "meeting", "age_weeks": 0, "heat": 55}
	var exact := absf(SimPipeline.lead_advance_p(s1, lead, 1) - 0.400159155919) < 1e-9
	# monotonicity runs at live 3, where the capacity factor is off its ceiling and
	# a sales budget can actually be felt (at live 1 it is already pinned to 1.0)
	var base3 := SimPipeline.lead_advance_p(s1, lead, 3)
	var s_sales := _ent()
	s_sales.budgets["sales"] = 4000
	var big := {"name": "Whale Industrial", "flavor": "", "seats": 60,
		"stage": "meeting", "age_weeks": 0, "heat": 55}
	var hot := {"name": "Meridian Logistics", "flavor": "", "seats": 6,
		"stage": "meeting", "age_weeks": 0, "heat": 100}
	var monotone := SimPipeline.lead_advance_p(s_sales, lead, 3) > base3 \
			and SimPipeline.lead_advance_p(s1, big, 3) < base3 \
			and SimPipeline.lead_advance_p(s1, hot, 3) > base3
	ok.call(exact and monotone,
		"advance math is exact (%.12f) and monotone in capacity, size and heat"
		% SimPipeline.lead_advance_p(s1, lead, 1))

	# ── PIN 2 — DETERMINISM. Two identical Enterprise runs, five weeks each,
	# land on the same board: same names, same stages, same heat, same ages, same
	# pool, same traction, same logos. A seeded stream is the whole contract.
	var a := _ent(1)
	var b := _ent(1)
	a.flags.append("launched")
	b.flags.append("launched")
	a.traction = 30
	b.traction = 30
	for _w in 5:
		a.week += 1
		b.week += 1
		SimEngine.weekly_tick(a)
		SimEngine.weekly_tick(b)
	var same := a.leads.size() == b.leads.size() and a.logos.size() == b.logos.size() \
			and a.traction == b.traction and absf(a.pipe_units - b.pipe_units) < 1e-9
	if same:
		for i in a.leads.size():
			var la: Dictionary = a.leads[i]
			var lb: Dictionary = b.leads[i]
			if String(la.name) != String(lb.name) or String(la.stage) != String(lb.stage) \
					or int(la.heat) != int(lb.heat) or int(la.age_weeks) != int(lb.age_weeks):
				same = false
				break
	ok.call(same, "two identical Enterprise runs replay the same board over 5 weeks")

	# ── PIN 3 — A COLD DEATH REFUNDS THE POOL, EXACTLY. Unlaunched with no
	# traction the market adds nothing, so every unit in the pool afterwards came
	# out of the dead deal. The refund is asserted as CONSERVATION (pool + the
	# seats the refund immediately re-spawned) because spawns run after deaths in
	# the same tick — which is the stronger pin: nothing was invented or lost.
	var s3 := _ent()
	s3.traction = 0
	s3.pipe_units = 0.0
	s3.leads = [{"name": "Vanta Systems", "flavor": "", "seats": 12,
		"stage": "meeting", "age_weeks": 4, "heat": 8}]
	var r3 := SimEngine.weekly_tick(s3)
	var cold_line := false
	for l in r3["lines"]:
		if String(l).begins_with("gone cold: Vanta Systems"):
			cold_line = true
	var refunded := absf(s3.pipe_units + float(SimPipeline.seats_in_motion(s3)) - 12.0) < 1e-9
	var gone := true
	for ld in s3.leads:
		if String((ld as Dictionary).get("name", "")) == "Vanta Systems":
			gone = false
	ok.call(gone and cold_line and refunded and int(s3.pipe_stats.get("lost", 0)) == 1,
		"a lead that dies cold refunds all 12 seats to the pool (no-decision, not a no)")

	# ── PIN 4 — A CLOSE CONSERVES SEATS. Twelve seats of pipeline become twelve
	# customers, one named logo and one row of pipe_stats — never eleven, never
	# thirteen, and never a number the DM chose.
	var s4 := _ent()
	s4.traction = 5
	s4.leads = [{"name": "Quill Health", "flavor": "", "seats": 12,
		"stage": "contract", "age_weeks": 7, "heat": 70}]
	var r4 := {"lines": []}
	var booked := SimPipeline.close_lead(s4, 0, r4)
	var signed_line := false
	for l2 in r4["lines"]:
		if String(l2).contains("SIGNED") and String(l2).contains("Quill Health"):
			signed_line = true
	ok.call(booked == 12 and s4.traction == 17 and s4.leads.is_empty()
			and s4.logos.size() == 1 and int((s4.logos[0] as Dictionary).seats) == 12
			and int(s4.pipe_stats.get("signed", 0)) == 1
			and int(s4.pipe_stats.get("seats_signed", 0)) == 12 and signed_line,
		"a close books 12 seats, one logo and one SIGNED receipt — exactly")

	# ── PIN 5 — ACCOUNTS CHURN WHOLE. Enterprise revenue is contract-shaped: a
	# logo leaves with all of its seats in one week, never a fraction of itself.
	var s5 := _ent()
	s5.flags.append("launched")
	s5.traction = 40
	s5.logos = [{"name": "Fernbay Group", "seats": 40, "since_wk": 1, "renewal_wk": 0}]
	s5.pipe_churn_acc = 40.0
	SimEngine.weekly_tick(s5)
	ok.call(s5.logos.is_empty() and s5.traction == 0,
		"a churning account takes all 40 seats with it, in one week, whole")

	# ── PIN 6 — NON-ENTERPRISE IS UNTOUCHED. The pipeline never reaches SMB or
	# Consumer: no leads, no pool, no stats, no directives, no bang — and §8's own
	# adds/churn lines are still the ones doing the work.
	var s6 := _ent()
	s6.biz_who = "SMB"
	s6.theta = SimEngine.default_theta("Software", "SMB")
	s6.flags.append("launched")
	s6.traction = 40
	var r6 := SimEngine.weekly_tick(s6)
	var classic := false
	for l3 in r6["lines"]:
		if String(l3).contains("customers (organic"):
			classic = true
	ok.call(s6.leads.is_empty() and s6.logos.is_empty() and s6.pipe_units == 0.0
			and s6.pipe_stats.is_empty() and int(r6["adds"]) > 0 and classic
			and SimPipeline.directives(s6).is_empty()
			and SimPipeline.attention(s6).is_empty()
			and SimPipeline.push_lead(s6, "anyone", 40) == "",
		"an SMB run never sees the pipeline — adds and churn stay the engine's own")
