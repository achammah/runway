extends RefCounted
## LANE SUITE — the money desks (DAG2 W2 L-MONEY). Pins the spend book's
## write-back law (the sum IS the lever), the SUGGEST/ADOPT ruling (levers
## start at 0; adoption is explicit), the stop/notice mutation law, and the
## display math the team desk renders (vesting 208/52, the rung ladder).
##
## `tests/sim_engine_test.gd` calls run() after the engine's own checks and
## hands over `ok`, the same assert the whole suite uses. Standalone:
## `godot --headless --path . --script tests/money_suite.gd` runs exactly
## these checks with an honest exit code.
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/MoneyDesksTests.cs — same checks, same
## order, byte-identical messages. Nothing here rolls dice.

static func _state() -> GameState:
	var s := GameState.new()
	s.sim_seed = 42
	s.week = 5
	s.cash = 50_000
	s.era = "office"
	s.biz_what = "Software"
	s.biz_who = "SMB"
	return s

## A generated-looking book: two suggested lines per bucket family the tests
## poke, in the schema world-gen writes (amt = the suggestion, no live key).
static func _booked(s: GameState) -> void:
	s.spend_book = [
		{"name": "sales engineering", "buys": "demos that land", "amt": 180, "bucket": "sales", "contract_notice": 0, "division": ""},
		{"name": "the demo rig", "buys": "always ready to show", "amt": 120, "bucket": "sales", "contract_notice": 0, "division": ""},
		{"name": "on-call rotation", "buys": "nights answered", "amt": 120, "bucket": "care", "contract_notice": 4, "division": ""},
		{"name": "the test bench", "buys": "bugs die young", "amt": 150, "bucket": "rnd", "contract_notice": 0, "division": ""},
		{"name": "the kitchen", "buys": "fed people stay", "amt": 220, "bucket": "office", "contract_notice": 0, "division": ""},
	]

static func run(ok: Callable) -> void:
	# ── 1. THE SUGGEST/ADOPT RULING: a fresh generated book proposes, the
	# levers stay 0 until the player adopts — week 1 spends nothing.
	var s1 := _state()
	_booked(s1)
	SimSpendBook.reconcile(s1)
	ok.call(int(s1.budgets.get("sales", -1)) == 0 and int(s1.budgets.get("care", -1)) == 0,
		"a fresh generated book leaves the levers at 0")
	ok.call(SimSpendBook.book_suggested(s1) == 790,
		"the suggestions still read whole beside the zero levers")

	# ── 2. ADOPT one line: live := amt, and the bucket lever follows.
	var s2 := _state()
	_booked(s2)
	SimSpendBook.adopt_line(s2, 0)
	ok.call(SimSpendBook.live_of(s2.spend_book[0]) == 180
		and int(s2.budgets.get("sales", 0)) == 180,
		"adopt copies the suggestion into the lever")

	# ── 3. ADOPT the whole book: every bucket prices at its suggested sum.
	var s3 := _state()
	_booked(s3)
	SimSpendBook.adopt_book(s3)
	ok.call(int(s3.budgets.get("sales", 0)) == 300 and int(s3.budgets.get("care", 0)) == 120
		and int(s3.budgets.get("rnd", 0)) == 150 and int(s3.budgets.get("office", 0)) == 220,
		"adopt the whole book prices every bucket")

	# ── 4. THE WRITE-BACK LAW: a stepper press keeps lever == Σ live.
	var s4 := _state()
	_booked(s4)
	SimSpendBook.adopt_book(s4)
	SimSpendBook.adjust_live(s4, 1, 1)   # $120 steps by q(120)=20 → $140
	ok.call(SimSpendBook.live_of(s4.spend_book[1]) == 140
		and int(s4.budgets.get("sales", 0)) == 320,
		"the sum IS the lever after a step up")
	SimSpendBook.adjust_live(s4, 1, -1)
	SimSpendBook.adjust_live(s4, 1, -1)   # 140 → 120 → 100
	ok.call(int(s4.budgets.get("sales", 0)) == 280,
		"the sum IS the lever after steps down")

	# ── 5. THE ERA CEILING: a + past the garage cap is refused, and the
	# refusal is visible to the desk through at_cap.
	var s5 := _state()
	s5.era = "garage"
	_booked(s5)
	s5.spend_book[0]["live"] = 5_990
	SimSpendBook.reconcile(s5)
	var before := SimSpendBook.live_of(s5.spend_book[0])
	SimSpendBook.adjust_live(s5, 0, 1)
	ok.call(SimSpendBook.live_of(s5.spend_book[0]) == before and SimSpendBook.at_cap(s5, 0),
		"a step up refuses past the era ceiling")

	# ── 6. The floor: a line steps down to $0 and no further.
	var s6 := _state()
	_booked(s6)
	SimSpendBook.adjust_live(s6, 3, -1)
	ok.call(SimSpendBook.live_of(s6.spend_book[3]) == 0
		and int(s6.budgets.get("rnd", 0)) == 0,
		"a step down floors at zero")

	# ── 7. THE LEGACY ABSORB: a pre-book save's levers land on the first
	# line of their bucket — the book agrees without inventing a dollar.
	var s7 := _state()
	s7.spend_book = SimSpendBook.bare_book()
	s7.budgets["sales"] = 1_000
	s7.budgets["office"] = 250
	SimSpendBook.reconcile(s7)
	ok.call(SimSpendBook.live_of(s7.spend_book[0]) == 1_000
		and SimSpendBook.live_of(s7.spend_book[3]) == 250
		and int(s7.budgets.get("sales", 0)) == 1_000,
		"the legacy levers land on the first line of their bucket")

	# ── 8. THE MUTATION LAW, stop: no notice → gone now, the lever falls.
	var s8 := _state()
	_booked(s8)
	SimSpendBook.adopt_book(s8)
	var verdict := SimSpendBook.stop_line(s8, 0, s8.week)
	ok.call(verdict == "stopped" and s8.spend_book.size() == 4
		and int(s8.budgets.get("sales", 0)) == 120,
		"a no-notice line stops instantly")

	# ── 9. A contract line bills through its notice: it stays in the book,
	# the lever keeps carrying it, and the countdown reads honestly.
	var s9 := _state()
	_booked(s9)
	SimSpendBook.adopt_book(s9)
	var v9 := SimSpendBook.stop_line(s9, 2, s9.week)   # care, notice 4
	ok.call(v9 == "notice" and s9.spend_book.size() == 5
		and int(s9.budgets.get("care", 0)) == 120
		and SimSpendBook.notice_left(s9.spend_book[2], s9.week + 1) == 3,
		"a contract line bills through its notice")

	# ── 10. The notice runs out: the sweep closes the line, the lever falls.
	var swept := SimSpendBook.sweep_lapsed(s9, s9.week + 4)
	ok.call(swept == 1 and s9.spend_book.size() == 4
		and int(s9.budgets.get("care", 0)) == 0,
		"the notice runs out and the line closes")

	# ── 11. ADD is ink: free until raised, refused only when the book is full.
	var s11 := _state()
	_booked(s11)
	var idx := SimSpendBook.add_line(s11, "rnd")
	ok.call(idx == 5 and int(s11.budgets.get("rnd", 0)) == 0
		and s11.spend_book.size() == 6,
		"adding a line is free until raised")

	# ── 12/13. THE VESTING FORMULA (208-wk vest, 52-wk cliff) the team desk
	# renders until the ownership getter lands.
	ok.call(SimSpendBook.vested_frac(51, 0) == 0.0,
		"the vesting cliff holds for a year")
	ok.call(absf(SimSpendBook.vested_frac(104, 0) - 0.5) < 0.0001
		and SimSpendBook.vested_frac(300, 0) == 1.0,
		"vesting runs linear to week 208 and caps")

	# ── 14. THE TEAM LADDER's deterministic rungs.
	ok.call(SimSpendBook.team_rung(9) == 1 and SimSpendBook.team_rung(10) == 2
		and SimSpendBook.team_rung(40) == 2 and SimSpendBook.team_rung(41) == 3,
		"the team ladder breaks at ten and forty")

	# ── 15. THE RECEIPT'S ANNUITY is the bank's own level payment: borrow
	# $5,000 over 26 weeks at 3.4%/wk → $293 every Monday (hand-checkable:
	# 5000·0.034 / (1 − 1.034⁻²⁶) = 292.7…, ceilinged).
	ok.call(SimBank.loan_payment_wk(5_000, 0.034, 26) == 293,
		"the receipt's annuity matches the bank's own")
