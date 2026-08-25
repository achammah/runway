extends RefCounted
## LANE SUITE — roadmap. Spec: docs/design/07-roadmap.md §14 (twin test pins).
##
## Six pins, in the spec's order, each pinning a number a player can feel:
##   1 the board is deterministic and era-legal
##   2 OPPORTUNITY COST — committed weeks ship no base quality, and the pool is
##     exactly what the arithmetic says
##   3 TECH-DEBT INTEREST — the drag is a formula, not a vibe
##   4 the band table, the QA net and the clamps, on scripted dice
##   5 READY waits for the founder's press, and slips out on its own at three
##     weeks; the standing bet always comes back
##   6 the multipliers compose
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/RoadmapTests.cs. Same checks, same order, same
## logic — the two engines do not share PRNG internals, so never pin a draw
## across them, only behaviour.

static func _st() -> GameState:
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

## A bet planted by hand, so a pin never depends on which card the draw dealt.
static func _bet(id: String, kind: String, amb: int, era: String = "garage") -> Dictionary:
	return {"id": id, "name": id.to_upper(), "desc": "a thing the team could build",
		"kind": kind, "ambition": amb,
		"cost_rnd_weeks": SimRoadmap.bet_cost(kind, amb, id),
		"progress": 0.0, "committed": false, "committed_week": 0,
		"ready": false, "shipped": false, "shipped_week": 0, "band": "", "era": era}

## Scripted dice: the list, then its last value forever.
static func _roller(vals: Array) -> Callable:
	var i := [0]
	return func() -> int:
		var v := int(vals[mini(int(i[0]), vals.size() - 1)])
		i[0] = int(i[0]) + 1
		return v

static func run(ok: Callable) -> void:
	# ── 1 ── DETERMINISM + THE SEED BOARD ────────────────────────────────────
	# Two clones of the same week deal the same board, and the garage's board is
	# era-legal: the standing bet, one candidate, nothing over ambition 2 and no
	# platform work before there is a floor to put it on.
	var a := _st()
	a.week = 1
	var b := _st()
	b.week = 1
	SimEngine.weekly_tick(a)
	SimEngine.weekly_tick(b)
	var same := a.bets.size() == b.bets.size()
	for i in a.bets.size():
		if not same:
			break
		same = String((a.bets[i] as Dictionary).get("id", "")) == String((b.bets[i] as Dictionary).get("id", "")) \
			and String((a.bets[i] as Dictionary).get("name", "")) == String((b.bets[i] as Dictionary).get("name", ""))
	ok.call(same, "the roadmap board is deterministic (same seed, same week, same cards)")
	ok.call(SimRoadmap.board_bets(a).size() == SimRoadmap.slots(a)
		and not SimRoadmap.hardening_bet(a).is_empty(),
		"the garage board is the standing bet plus %d candidate" % SimRoadmap.slots(a))
	var era_legal := true
	for bet in SimRoadmap.board_bets(a):
		var bd: Dictionary = bet
		if int(bd.get("ambition", 1)) > 2 or String(bd.get("kind", "")) == "platform":
			era_legal = false
	ok.call(era_legal, "the garage never deals ambition 3 or a platform bet")

	# ── 2 ── OPPORTUNITY COST: the same money, one output ────────────────────
	# $2,400 of rnd is two R&D-weeks; the garage founder adds a quarter of their
	# own. Committed, that is 2.25 weeks of a bet and ZERO base quality — the
	# spine's drip is handed back. Uncommitted, the legacy path is untouched.
	var c := _st()
	c.budgets["rnd"] = 2_400
	c.bets = [_bet("bet_x", "quality", 2)]
	ok.call(SimRoadmap.commit_bet(c, "bet_x"), "the team can be pointed at a bet")
	var p0 := c.product
	SimEngine.weekly_tick(c)
	var bx := SimRoadmap.bet_by_id(c, "bet_x")
	ok.call(absf(float(bx.get("progress", 0.0)) - 2.25) < 0.0001,
		"committed rnd buys exactly 2.25 R&D-wks (2.0 money + 0.25 founder)")
	ok.call(c.product == p0, "OPPORTUNITY COST: a committed week ships no base quality")
	var u := _st()
	u.budgets["rnd"] = 2_400
	u.bets = [_bet("bet_y", "quality", 2)]
	var up0 := u.product
	SimEngine.weekly_tick(u)
	ok.call(u.product >= up0 + 2 and float(SimRoadmap.bet_by_id(u, "bet_y").get("progress", 0.0)) == 0.0,
		"uncommitted, the legacy +1-per-$1,200 path runs and no bet moves")

	# ── 3 ── TECH-DEBT INTEREST is a formula ─────────────────────────────────
	# drag(40) = 1.0, drag(90) = 0.58333, drag(100) = 0.5 — linear interest on
	# every hour the team works, floored at half speed.
	var d10 := _st()
	d10.tech_debt = 10.0
	d10.budgets["rnd"] = 2_400
	d10.bets = [_bet("bet_d", "quality", 2)]
	SimRoadmap.commit_bet(d10, "bet_d")
	var d90 := _st()
	d90.tech_debt = 90.0
	d90.budgets["rnd"] = 2_400
	d90.bets = [_bet("bet_d", "quality", 2)]
	SimRoadmap.commit_bet(d90, "bet_d")
	var ratio := SimRoadmap.capacity_pool(d90) / SimRoadmap.capacity_pool(d10)
	ok.call(absf(ratio - 0.5833333) < 0.0001,
		"debt 90 vs 10 costs exactly 41.7% of the team's throughput")
	ok.call(absf(SimRoadmap.debt_drag(d10) - 1.0) < 0.0001
		and absf(SimRoadmap.debt_drag(_debt(100.0)) - 0.5) < 0.0001,
		"the drag is 1.0 under debt 40 and floors at 0.5")

	# ── 4 ── THE BAND TABLE, on scripted dice ────────────────────────────────
	# A 20 on an ambition-2 quality bet is brilliant: +11 product, +8 hype. The
	# same bet on a 7 misses DC 11 by four — a backfire in a garage, and only a
	# risky launch once staging and review exist (the QA net, office+).
	var win := _st()
	var wb := _bet("bet_w", "quality", 2)
	win.bets = [wb]
	var wr := SimRoadmap.ship_bet(win, wb, _roller([20]))
	ok.call(String(wr.get("band", "")) == "brilliant" and win.product == 61 and win.hype == 48,
		"a 20 vs DC 11 is brilliant: product +11, hype +8")
	var bad := _st()
	var bb := _bet("bet_b", "quality", 2)
	bad.bets = [bb]
	var br := SimRoadmap.ship_bet(bad, bb, _roller([7]))
	ok.call(String(br.get("band", "")) == "backfired" and absf(bad.tech_debt - 22.0) < 0.001
		and bad.morale == 64,
		"a 7 vs DC 11 backfires in the garage: debt +12, morale −6")
	var qa := _st()
	qa.era = "office"
	var qb := _bet("bet_q", "quality", 2, "office")
	qa.bets = [qb]
	var qr := SimRoadmap.ship_bet(qa, qb, _roller([7]))
	ok.call(String(qr.get("band", "")) == "risky" and bool(qr.get("qa_net", false))
		and absf(qa.tech_debt - 16.0) < 0.001,
		"the QA net softens a miss by four to risky at office+: debt +6, not +12")
	var cap := _st()
	cap.product = 98
	var cb := _bet("bet_c", "quality", 2)
	cap.bets = [cb]
	SimRoadmap.ship_bet(cap, cb, _roller([20]))
	ok.call(cap.product == 100, "the payoff clamps: product never passes 100")

	# ── 5 ── READY WAITS FOR THE PRESS, then slips out on its own ────────────
	# SHIP IS A BUTTON (docs/design/DECISIONS.md #2): a finished bet sits READY
	# until the founder presses it — for three weeks, and then the world ships
	# it anyway. The standing bet always comes back.
	var r := _st()
	r.budgets["rnd"] = 1_200
	var rb := _bet("bet_r", "quality", 2)
	rb["progress"] = 4.5
	r.bets = [rb]
	SimRoadmap.commit_bet(r, "bet_r")
	r.week += 1
	SimEngine.weekly_tick(r)
	var rr := SimRoadmap.bet_by_id(r, "bet_r")
	ok.call(bool(rr.get("ready", false)) and not bool(rr.get("shipped", false))
		and not bool(rr.get("committed", false)),
		"a finished bet goes READY, uncommitted, unshipped")
	r.week += 1
	SimEngine.weekly_tick(r)
	ok.call(bool(SimRoadmap.bet_by_id(r, "bet_r").get("ready", false))
		and not bool(SimRoadmap.bet_by_id(r, "bet_r").get("shipped", false)),
		"the world does not ship it for you — the dice wait for the press")
	var pressed := SimRoadmap.ship_ready(r, "bet_r")
	ok.call(not pressed.is_empty() and bool(SimRoadmap.bet_by_id(r, "bet_r").get("shipped", false))
		and String(SimRoadmap.bet_by_id(r, "bet_r").get("band", "")) != "",
		"the press rolls the house dice and the bet ships with a band")
	var s3 := _st()
	var sb := _bet("bet_s", "quality", 2)
	sb["ready"] = true
	sb["committed_week"] = s3.week - SimRoadmap.STALL_WEEKS
	s3.bets = [sb]
	var s3rep := SimEngine.weekly_tick(s3)
	var slipped := false
	var s3lines: Array = s3rep.get("lines", [])
	for l in s3lines:
		if String(l).begins_with("nobody pressed ship"):
			slipped = true
	ok.call(bool(SimRoadmap.bet_by_id(s3, "bet_s").get("shipped", false)) and slipped,
		"three weeks unpressed and the launch slips out on its own, with its receipt")
	var h := _st()
	h.bets = [_bet(SimRoadmap.HARDENING_ID, "debt", 1)]
	var hb := SimRoadmap.hardening_bet(h)
	SimRoadmap.ship_bet(h, hb, _roller([20]))
	h.week += 1
	SimEngine.weekly_tick(h)
	var fresh := SimRoadmap.hardening_bet(h)
	ok.call(not fresh.is_empty() and float(fresh.get("progress", 0.0)) == 0.0,
		"the standing hardening bet is re-seeded the tick after it ships")

	# ── 6 ── THE MULTIPLIERS COMPOSE ─────────────────────────────────────────
	# An engineer is 0.25 x skill of real capacity; a shipped platform level
	# multiplies everything that comes after it.
	var e0 := _st()
	e0.bets = [_bet("bet_e", "quality", 2)]
	SimRoadmap.commit_bet(e0, "bet_e")
	var base := SimRoadmap.capacity_pool(e0)
	e0.employees.append({"name": "Ren", "role": "engineer", "salary": 1_200, "skill": 4})
	ok.call(absf(SimRoadmap.capacity_pool(e0) - (base + 1.0)) < 0.0001,
		"one engineer at skill 4 adds exactly 1.0 R&D-wk/wk")
	var pl := _st()
	pl.budgets["rnd"] = 2_400
	pl.bets = [_bet("bet_p", "quality", 2)]
	SimRoadmap.commit_bet(pl, "bet_p")
	pl.platform_level = 1
	ok.call(absf(SimRoadmap.capacity_pool(pl) - 2.5875) < 0.0001,
		"a platform level compounds the whole pool: 2.25 x 1.15 = 2.5875")

static func _debt(v: float) -> GameState:
	var s := _st()
	s.tech_debt = v
	return s
