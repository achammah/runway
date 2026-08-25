extends RefCounted
## LANE SUITE — bank. Spec: docs/design/06-finance.md §12 (the twin test pins).
##
## `tests/sim_engine_test.gd` calls run() after the engine's own checks and hands
## over `ok`, the same assert the whole suite uses: ok.call(cond, "what it pins").
##
## The porting law: a check lands HERE first, then in the same order in
## unity/Runway.Core.Tests/Lanes/BankTests.cs. Same checks, same order, same
## logic — the two engines do not share PRNG internals, so never pin a draw
## across them, only behaviour. Nothing in this lane rolls dice at all, so every
## number below is arithmetic a reader can check by hand.

## The suite's own fixture, identical to the engine suite's `_state()` so the
## hand arithmetic in the comments below stays checkable: Software/SMB theta
## (arpu 14, tam 60k, burn_mult 1.0), garage era, $50,000 in the bank.
static func _state() -> GameState:
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

static func _note(kind: String, bal: int, rate: float, term: int, wk: int, pay: int) -> Dictionary:
	return {"kind": kind, "principal": bal, "balance": bal, "rate_wk": rate,
		"term_wk": term, "taken_week": wk, "pay_wk": pay, "missed": 0}

static func run(ok: Callable) -> void:
	# ── 1. MIGRATION + THE SHARK LAW. The legacy `loan_principal` becomes a
	# structured shark note at tick time and not one dollar changes: 18%/wk
	# still compounds, the claw still takes everything above walking money.
	var mg := _state()
	mg.cash = 500
	mg.traction = 0
	mg.loan_principal = 10_000
	SimEngine.weekly_tick(mg)
	ok.call(mg.loans.size() == 1 and String((mg.loans[0] as Dictionary).get("kind", "")) == "shark",
		"a legacy loan migrates into one shark note")
	ok.call(mg.loan_principal == 0, "the legacy field empties once it has migrated")
	ok.call(SimBank.debt_total(mg) >= 11_800,
		"18%%/wk still compounds after the migration (owe %d)" % SimBank.debt_total(mg))
	var cw_claw := _state()
	cw_claw.traction = 0
	cw_claw.loan_principal = 5_000
	SimEngine.weekly_tick(cw_claw)
	ok.call(SimBank.debt_total(cw_claw) == 0 and cw_claw.cash < 50_000,
		"the shark's claw still repays out of any cash above $2,000")

	# ── 2. THE CREDIT LADDER. Each era's access is the one the spec's table
	# names, and the desk's quote can never touch the shark's 18%.
	var gar := _state()
	ok.call(SimBank.borrow_headroom(gar) == 0, "no bank answers a garage — headroom is $0")
	var cwk := _state()
	cwk.era = "coworking"
	cwk.traction = 500                       # rev_wk = 500 x 14 = $7,000/wk
	ok.call(SimBank.borrow_headroom(cwk) == 10_000,
		"a coworking micro-line caps at $10,000 (%d)" % SimBank.borrow_headroom(cwk))
	ok.call(absf(SimBank.bank_rate_wk(cwk) - 0.04) < 0.0005,
		"the small-business premium floors the coworking quote at 4.0%/wk")
	var des := _state()
	des.era = "office"
	des.cash = -1_000                        # runway 0 -> health 1.0
	des.last_growth = -0.5                   # a 50%% slump -> slump 1.0
	ok.call(absf(SimBank.bank_rate_wk(des) - 0.11) < 0.0005,
		"desperate office books quote the top of the band, 11.0%%/wk (%.3f)" % SimBank.bank_rate_wk(des))
	ok.call(SimBank.bank_rate_wk(des) < SimBank.SHARK_RATE,
		"the desk's worst quote is still cheaper than the shark")
	var hlt := _state()
	hlt.era = "office"
	hlt.traction = 4_000                     # profitable -> runway 999 -> health 0
	hlt.last_growth = 0.2
	ok.call(absf(SimBank.bank_rate_wk(hlt) - 0.02) < 0.0005,
		"healthy office books quote the 2.0%% floor (%.3f)" % SimBank.bank_rate_wk(hlt))
	var vd := _state()
	vd.era = "floor"
	ok.call(SimBank.venture_cap(vd) == 0, "venture debt is locked until a round has closed")
	vd.last_round_amount = 100_000
	ok.call(SimBank.venture_cap(vd) == 30_000,
		"venture debt sizes at 30%% of the last round (%d)" % SimBank.venture_cap(vd))
	var swp := _state()
	swp.era = "hq"
	swp.traction = 0
	swp.cash = 300_000
	SimEngine.weekly_tick(swp)
	ok.call(int(swp.get_meta("pnl", {}).get("interest", 0)) == -200,
		"hq sweeps 0.1%%/wk on idle cash into the interest lane as income (%d)"
		% int(swp.get_meta("pnl", {}).get("interest", 0)))

	# ── 3. THE ANNUITY. $10,000 at 4%/wk over 8 weeks pays $1,486/wk, closes in
	# exactly 8 ticks with cash to spare, and costs about $1,888 in interest.
	ok.call(SimBank.loan_payment_wk(10_000, 0.04, 8) == 1_486,
		"the level payment is $1,486/wk (%d)" % SimBank.loan_payment_wk(10_000, 0.04, 8))
	var an := _state()
	an.era = "office"
	an.cash = 200_000
	an.traction = 0
	an.loans = [_note("bank", 10_000, 0.04, 8, an.week, 1_486)]
	var interest_sum := 0
	var identity := true
	for _w in 8:
		an.week += 1
		SimEngine.weekly_tick(an)
		var p: Dictionary = an.get_meta("pnl", {})
		interest_sum += int(p.get("interest", 0))
		if int(p.get("net", 0)) != int(p.get("revenue", 0)) - int(p.get("burn", 0)) \
				- int(p.get("liabilities_wk", 0)) - int(p.get("interest", 0)) - int(p.get("tax", 0)):
			identity = false
	ok.call(an.loans.is_empty(), "the note closes in exactly eight payments")
	ok.call(absi(interest_sum - 1_888) <= 40,
		"the eight payments cost about $1,888 in interest (%d)" % interest_sum)
	ok.call(identity, "the P&L identity holds every week the note is being paid")

	# ── 4. THE MISS LADDER. Skipped, never overdrawn: capitalize, reprice,
	# then sell the paper to the collectors — and repaying lifts the lock.
	var ms := _state()
	ms.era = "office"
	ms.cash = 0
	ms.traction = 0
	ms.loans = [_note("bank", 10_000, 0.04, 8, ms.week, 1_486)]
	var ctl := _state()
	ctl.era = "office"
	ctl.cash = 0
	ctl.traction = 0
	ms.week += 1
	ctl.week += 1
	SimEngine.weekly_tick(ms)
	SimEngine.weekly_tick(ctl)
	var n0: Dictionary = ms.loans[0]
	ok.call(int(n0.get("missed", 0)) == 1 and int(n0.get("balance", 0)) == 10_400,
		"a missed payment capitalizes its interest instead of overdrawing you (%d)"
		% int(n0.get("balance", 0)))
	ok.call(ms.morale == ctl.morale - 3, "a missed payment costs three points of morale")
	ms.week += 1
	SimEngine.weekly_tick(ms)
	var n1: Dictionary = ms.loans[0]
	ok.call(absf(float(n1.get("rate_wk", 0.0)) - 0.06) < 0.0005 and SimBank.credit_locked(ms),
		"the second miss reprices the risk +2%% and locks the bank out (%.3f)"
		% float(n1.get("rate_wk", 0.0)))
	ms.week += 1
	SimEngine.weekly_tick(ms)
	var n2: Dictionary = ms.loans[0]
	ok.call(String(n2.get("kind", "")) == "shark" and absf(float(n2.get("rate_wk", 0.0)) - 0.18) < 0.0005,
		"the third miss sells the note to the collectors at 18%/wk")
	ok.call(SimEngine.has_status(ms, "collections_calls"),
		"collections install the status investors can smell")
	ms.cash = 100_000
	SimBank.repay_note(ms, 0)
	ok.call(not SimBank.credit_locked(ms) and ms.loans.is_empty(),
		"repaying the distressed note lifts the credit lock")

	# ── 5. TAX. 20% of EBT — after interest, from the office up, with losses
	# carried forward so one good week inside a bad month is not taxed.
	var tx := _state()
	tx.era = "office"
	var books := {"revenue": 12_000.0, "burn": 9_938, "liabilities_wk": 0, "interest": 0.0}
	var tax_flat := SimBank.tax_wk(tx, books)
	ok.call(tax_flat == 412, "an EBT of $2,062 is taxed $412 (%d)" % tax_flat)
	ok.call(tx.cash == 50_000 - 412, "the tax actually leaves the bank account")
	ok.call(tx.has_flag("tax_noticed"), "the first charge puts the company on the radar")
	ok.call(SimBank.tax_wk(_state(), books) == 0, "identical books in a garage are taxed nothing")
	var cf := _state()
	cf.era = "office"
	SimBank.tax_wk(cf, {"revenue": 0.0, "burn": 1_000, "liabilities_wk": 0, "interest": 0.0})
	ok.call(cf.tax_loss_carry == 1_000, "a losing week banks its loss as a carryforward")
	ok.call(SimBank.tax_wk(cf, {"revenue": 1_000.0, "burn": 0, "liabilities_wk": 0, "interest": 0.0}) == 0
		and cf.tax_loss_carry == 0,
		"the carryforward shelters the next $1,000 of profit and is spent doing it")
	ok.call(SimBank.tax_wk(_office(), {"revenue": 12_000.0, "burn": 9_938,
		"liabilities_wk": 0, "interest": 1_000.0}) == 212,
		"interest is deducted BEFORE the tax — EBT, not operating profit")

	# ── 6. BREAK-EVEN + FORECAST PURITY.
	var be := _state()
	be.era = "coworking"
	be.traction = 0
	be.offers = [{"name": "a session", "unit": "per session", "price": 40.0, "price_set": true,
		"fair_price": 40.0, "unit_cost": 10.0, "elasticity": 2.0, "weight": 1.0}]
	# margin = $40 − ($10 serving + $0.05 infra) = $29.95 · fixed = rent 600 +
	# infra 50 = $650 · 650 / 29.95 = 21.7 → 22 customers
	ok.call(SimBank.break_even_customers(be) == 22,
		"break-even is fixed costs over contribution margin: 22 customers (%d)"
		% SimBank.break_even_customers(be))
	var loss := _state()
	loss.era = "coworking"
	loss.offers = [{"name": "a session", "unit": "per session", "price": 5.0, "price_set": true,
		"fair_price": 40.0, "unit_cost": 10.0, "elasticity": 2.0, "weight": 1.0}]
	ok.call(SimBank.break_even_customers(loss) == -1,
		"no count breaks even when a customer costs more than they pay")
	var fp := _state()
	fp.traction = 0
	fp.cash = 10_000
	var before := JSON.stringify(SaveSystem.state_to_dict(fp))
	var rows: Array = SimBank.forecast_cash(fp, 4)
	var after := JSON.stringify(SaveSystem.state_to_dict(fp))
	ok.call(before == after, "the forecast is pure — it leaves the state byte-identical")
	ok.call(rows.size() == 4, "the forecast runs the four weeks it was asked for")
	# hand math, week 1: no customers, nothing launched, so burn is rent $150 +
	# infra $50 and nothing else. $10,000 − $200 = $9,800, net −$200.
	ok.call(int((rows[0] as Dictionary).get("cash", 0)) == 9_800
		and int((rows[0] as Dictionary).get("net", 0)) == -200,
		"week one of the forecast matches a noise-stripped tick by hand (%d)"
		% int((rows[0] as Dictionary).get("cash", 0)))
	var fl := _state()
	fl.traction = 0
	fl.cash = 10_000
	fl.loans = [_note("bank", 10_000, 0.04, 8, fl.week, 1_486)]
	var frows: Array = SimBank.forecast_cash(fl, 1)
	ok.call(int((frows[0] as Dictionary).get("cash", 0)) == 9_800 - 1_486,
		"the forecast pays the loan schedule it can see (%d)"
		% int((frows[0] as Dictionary).get("cash", 0)))
	var fr := _state()
	fr.era = "floor"
	fr.traction = 0
	fr.cash = 10_000
	fr.receivables = [{"name": "an old invoice", "cash_wk": 4_000, "weeks_left": 1}]
	var rrows: Array = SimBank.forecast_cash(fr, 1)
	ok.call(int((rrows[0] as Dictionary).get("cash", 0)) == 10_000 - 12_050 + 4_000,
		"the forecast lands the net-30 invoices that mature inside it (%d)"
		% int((rrows[0] as Dictionary).get("cash", 0)))
	var sn := _state()
	SimEngine.weekly_tick(sn)
	var last: Dictionary = sn.metric_history[sn.metric_history.size() - 1]
	ok.call(last.has("net"), "every history row now carries the week's net")

## A fresh office-era company — the tax pins need several, and each charge
## mutates the state it is charged against.
static func _office() -> GameState:
	var s := _state()
	s.era = "office"
	return s
