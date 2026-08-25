extends RefCounted
## LANE SUITE — board. Spec: docs/design/08-board-mna.md §12 (twin test pins).
##
## Six pins, one per mechanic that would be silently wrong if it drifted:
##   1 the pool shuffle's exact arithmetic (and that it only exists at office+)
##   2 the review is deterministic and re-arms on the same cadence
##   3 the stage ladder gates the strikes, the coach and the reprice
##   4 warmth reads the governance record, in the right direction
##   5 the lifeline offer, the no-shop clock and the cooldown after a lapse
##   6 the IPO window is weather — it opens and it shuts, with a reason
##
## `tests/sim_engine_test.gd` calls run() after the engine's own checks and hands
## over `ok`, the same assert the whole suite uses.
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/BoardTests.cs. Same checks, same order, same
## logic — the two engines do not share PRNG internals, so never pin a draw
## across them, only behaviour.

## A run with a live company in it, at whatever era the pin needs.
static func _state(era: String = "office") -> GameState:
	var s := GameState.new()
	s.sim_seed = 4242
	s.week = 20
	s.era = era
	s.cash = 250_000
	s.traction = 60
	s.product = 55
	s.morale = 70
	s.hype = 40
	s.founder_pct = 100.0
	s.biz_what = "Software"
	s.biz_who = "SMB"
	s.theta = SimEngine.default_theta(s.biz_what, s.biz_who)
	return s

## A board mid-quarter, its review landing exactly this week.
static func _with_board(s: GameState, target: int, strikes := 0, goodwill := 0) -> void:
	s.board = {"target_growth_pct": 35.0, "base_revenue": target, "target_revenue": target,
		"review_week": s.week, "strikes": strikes, "goodwill": goodwill}

static func _rep() -> Dictionary:
	return {"lines": [], "fired_clocks": [], "expired": [], "events": []}

static func _commitment(s: GameState, name: String) -> Dictionary:
	for c in s.commitments:
		if String((c as Dictionary).get("name", "")) == name:
			return c
	return {}

static func run(ok: Callable) -> void:
	# ── 1 · ROUND CLOSE DOES THE FULL SHUFFLE ───────────────────────────────
	# The pool is written PRE-money, so it dilutes only the existing side, and
	# the investor's slice then dilutes everyone including the pool.
	var s1 := _state("office")
	SimEngine.apply_round(s1, 100_000, 20.0)
	SimBoard.on_round_closed(s1, 100_000, 20.0)
	ok.call(absf(s1.founder_pct - 72.0) < 0.001,
		"pool shuffle: founder 100 x0.9 pool x0.8 investor = 72 (got %.3f)" % s1.founder_pct)
	ok.call(absf(s1.option_pool_pct - 8.0) < 0.001,
		"pool shuffle: a 10%% pool diluted by the round = 8 (got %.3f)" % s1.option_pool_pct)
	ok.call(s1.board_seats_investor == 1 and int(s1.board.review_week) == s1.week + 12,
		"a priced round seats one investor and dates the first review 12 wks out")
	ok.call(float(s1.board.target_growth_pct) >= 10.0 and float(s1.board.target_growth_pct) <= 60.0
			and int(s1.board.target_revenue) >= int(SimBoard.ERA_REV_FLOOR["office"]),
		"the covenant is clamped 10-60%/qtr and never asks for growth on nothing")
	# and BELOW office there is no pool at all — an angel does not paper an ESOP
	var s1b := _state("coworking")
	SimEngine.apply_round(s1b, 50_000, 20.0)
	SimBoard.on_round_closed(s1b, 50_000, 20.0)
	ok.call(absf(s1b.founder_pct - 80.0) < 0.001 and absf(s1b.option_pool_pct) < 0.001,
		"no pool below office: the founder keeps the shuffle's 10 points")

	# ── 2 · THE REVIEW IS DETERMINISTIC AND RE-ARMS ─────────────────────────
	var a := _state("office")
	var b := _state("office")
	for st in [a, b]:
		(st as GameState).set_meta("pnl", {"revenue": 400})
		_with_board(st, 900)
	var ra := _rep()
	var rb := _rep()
	SimBoard.tick_post(a, ra)
	SimBoard.tick_post(b, rb)
	ok.call(int(a.board.strikes) == int(b.board.strikes)
			and int(a.board.goodwill) == int(b.board.goodwill)
			and ra["lines"] == rb["lines"] and ra["events"] == rb["events"],
		"two identical states review to identical strikes, goodwill and receipts")
	ok.call(int(a.board.review_week) == a.week + 12,
		"the review re-arms exactly 12 weeks out, whichever way it went")
	ok.call(int(a.board.target_revenue) > int(a.board.base_revenue),
		"the re-armed bar sits above the base it was set from")

	# ── 3 · THE STAGE LADDER GATES THE STRIKES ──────────────────────────────
	# garage: an angel has expectations, not covenants — pressure, no record
	var g := _state("garage")
	g.set_meta("pnl", {"revenue": 10})
	_with_board(g, 500)
	SimBoard.tick_post(g, _rep())
	ok.call(int(g.board.strikes) == 0 and SimEngine.has_status(g, "investor_pressure"),
		"a garage miss installs investor_pressure and puts nothing on a record")
	# office, miss twice: the coach a board sends before it does worse
	var o := _state("office")
	o.employees = [{"name": "dev", "role": "engineer", "salary": 12_000, "burnout": 10}]
	o.set_meta("pnl", {"revenue": 100})
	_with_board(o, 5_000)
	SimBoard.tick_post(o, _rep())
	o.week += 12
	o.board["review_week"] = o.week
	SimBoard.tick_post(o, _rep())
	var coach := _commitment(o, "the executive coach the board sent")
	ok.call(int(o.board.strikes) == 2 and not coach.is_empty()
			and int(coach.get("cash_wk", 0)) <= -250 and int(coach.get("cash_wk", 0)) >= -2500,
		"strike two sends a CEO coach billing $250-$2500/wk, by name")
	# a third miss reprices every future round, and the clamp holds under repeats
	var fm0 := float(o.theta.get("funding_mult", 1.0))
	o.week += 12
	o.board["review_week"] = o.week
	SimBoard.tick_post(o, _rep())
	ok.call(absf(float(o.theta.get("funding_mult", 1.0)) - fm0 * 0.8) < 0.0001
			and o.has_flag("down_round_threat"),
		"strike three reprices the company x0.8 and flags the down round")
	# a beat drops the record back to two strikes, so the next miss re-reaches
	# three and reprices again — that is what "repeated" means here
	for i in 6:
		o.week += 12
		o.board["review_week"] = o.week
		o.set_meta("pnl", {"revenue": 900_000})
		SimBoard.tick_post(o, _rep())
		o.week += 12
		o.board["review_week"] = o.week
		o.set_meta("pnl", {"revenue": 100})
		SimBoard.tick_post(o, _rep())
	ok.call(absf(float(o.theta.get("funding_mult", 1.0)) - SimBoard.FUNDING_MULT_FLOOR) < 0.0001,
		"repeated strike threes converge on the 0.5 floor, never to zero")
	# and a beat pays: the record improves and the room warms for four weeks
	var w := _state("office")
	w.set_meta("pnl", {"revenue": 9_000})
	_with_board(w, 5_000, 1, 0)
	SimBoard.tick_post(w, _rep())
	ok.call(int(w.board.goodwill) == 1 and int(w.board.strikes) == 0
			and SimEngine.has_status(w, "board_delight"),
		"a met covenant burns a strike, banks goodwill and installs board_delight")

	# ── 4 · WARMTH READS THE RECORD ─────────────────────────────────────────
	var clean := _state("office")
	var loved := _state("office")
	var hated := _state("office")
	_with_board(clean, 500, 0, 0)
	_with_board(loved, 500, 0, 3)
	_with_board(hated, 500, 3, 0)
	ok.call(absf(SimBoard.warmth_delta(loved) - 6.0) < 0.001
			and absf(SimBoard.warmth_delta(hated) + 7.5) < 0.001,
		"three clean quarters are worth +6 points of ask; three strikes cost 7.5")
	ok.call(SimBoard.warmth_delta(loved) > SimBoard.warmth_delta(clean)
			and SimBoard.warmth_delta(clean) > SimBoard.warmth_delta(hated)
			and absf(SimBoard.warmth_delta(_state("office"))) < 0.001,
		"warmth orders loved > clean > struck, and a boardless run has no record")

	# ── 5 · LIFELINE, THE NO-SHOP AND THE COOLDOWN ──────────────────────────
	var d := _state("garage")
	d.week = 8
	d.cash = 400
	d.weeks_in_red = 2
	var d2 := _state("garage")
	d2.week = 8
	d2.cash = 400
	d2.weeks_in_red = 2
	SimBoard.tick_post(d, _rep())
	SimBoard.tick_post(d2, _rep())
	ok.call(not d.mna.is_empty() and String(d.mna.why) == "lifeline"
			and float(d.mna.premium) >= 0.3 and float(d.mna.premium) <= 0.5
			and int(d.mna.expires_week) == d.week + 2,
		"a dying company with something worth taking gets a 0.3-0.5x lifeline on a 2-wk no-shop")
	ok.call(String(d.mna.buyer) == String(d2.mna.buyer) and int(d.mna.price) == int(d2.mna.price),
		"the same seed and week price the same offer from the same buyer")
	var morale_before := d.morale
	d.week = 11
	var lapse := _rep()
	SimBoard.tick_post(d, lapse)
	ok.call(d.mna.is_empty() and d.morale < morale_before and d.mna_last_week == 11,
		"an unsigned lifeline lapses: the offer dies and the team heard the number")
	var quiet := true
	for i in 9:
		d.week += 1
		SimBoard.tick_post(d, _rep())
		if not d.mna.is_empty():
			quiet = false
	ok.call(quiet, "corp dev does not re-approach for the whole 10-week cooldown")

	# ── 6 · THE WINDOW IS WEATHER ───────────────────────────────────────────
	var h := _state("hq")
	h.traction = 120
	h.rounds_raised.append("seed")
	h.rounds_raised.append("series_a")
	h.macro_season = "boom"
	SimBoard.tick_post(h, _rep())
	ok.call(h.has_flag("ipo_window"),
		"clean covenants + a hundred believers + a market that's buying opens the window")
	h.macro_season = "winter"
	h.week += 1
	var shut := _rep()
	SimBoard.tick_post(h, shut)
	var said := false
	for l in shut["lines"]:
		if String(l).begins_with("the IPO window closed — winter came"):
			said = true
	ok.call(not h.has_flag("ipo_window") and said,
		"winter shuts the window, and the receipt says which weather did it")
