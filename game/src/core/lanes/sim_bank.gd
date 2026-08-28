class_name SimBank
extends RefCounted
## LANE 06 — THE BANK & THE STATE (credit, interest, tax). Spec: docs/design/06-finance.md
##
## Deliberate borrowing at health-priced terms, repayment control, profit tax,
## break-even and a cash forecast. Every mechanic here mirrors a real finance
## instrument, is called by its real name on the sheet, and every receipt says
## WHY — a loan payment is an expense AND a balance-sheet move, and the line
## that prints it says so.
##
## NOTHING HERE ROLLS DICE. The quote is a pure function of the books, so there
## is no salt to spend and nothing to reroll-scum (docs/design/00-spine.md §3
## records 06 as claiming no salt — this file keeps that true).
##
## The spine calls, in tick order (docs/design/00-spine.md §1, HOOKS.md):
##   tick_pre   tick §9 — notes settle before the money is assembled
##   tick_money the money section — the week's schedule, the miss ladder, the sweep
##   tax_wk     the state, charged last, on what is left after interest
##   tick_post  after the record is written — the taxman's receipt, net-30, break-even
## and outside the tick: directives() feeds the DM block, attention() feeds
## every bang in the game through SimEngine.attention_items.

# ── THE DEBT SEAM ────────────────────────────────────────────────────────────
## FLIPPED. This lane owns every note now: the structured `loans` list, honest
## risk-priced rates, level-payment amortization, the miss ladder. The engine's
## legacy shark block (18%/wk compounded, auto-repaid above $2,000) is retired
## by this constant, and a legacy `loan_principal` folds into a shark note the
## first time the week ticks (§3 migration) so no save loses a dollar of debt.
const OWNS_DEBT := true

## THE SHARK NEVER MOVES (existing pinned law): 18%/wk, health-blind, always
## available through the DM's `take_loan`, and it feeds before anyone else.
const SHARK_RATE := 0.18
const RATE_CAP := 0.12          ## the desk can never touch the shark's price
const MIN_DRAW := 1_000         ## below this the paperwork is worth more than the money
const CLAW_TRIGGER := 2_000     ## the shark's auto-claw, unchanged from the legacy block
const CLAW_KEEP := 1_500
const WARRANT_PCT := 0.25       ## docs/design/DECISIONS.md: venture debt nibbles the cap table
const SWEEP_RATE := 0.001       ## 0.1%/wk ≈ 5%/yr on idle cash, hq only
const SWEEP_FLOOR := 100_000
const TAX_RATE := 0.20
const TAX_ERA := 2              ## office — below it the company is cash-basis, off the radar
const RECEIVABLE_FRAC := 0.25   ## net-30: a quarter of revenue books now, lands in 4 weeks
const RECEIVABLE_WK := 4
const FORECAST_WEEKS := 4

## THE LADDERS the desk's steppers walk. The engine re-clamps every write, so
## the UI is never trusted with a bound (docs/design/10-interface-language.md §2.1).
const BORROW_STEPS: Array = [1_000, 2_000, 5_000, 10_000, 20_000, 50_000, 100_000]
const TERMS_EARLY: Array = [4, 8]
const TERMS_FULL: Array = [4, 8, 12, 26]
const TERMS_VENTURE: Array = [12, 26]

## The taxman's receipt cannot be written where the tax is computed: `tax_wk`
## is handed the working record, not the week's report. So the slips wait here
## between the money section and tick_post, which owns the finished record and
## can read the charge back. Cleared at the top of every tick.
static var _slips: Array[String] = []

# ═════════════════════════ READ HELPERS (pure) ══════════════════════════════

## THE ONE DEBT READING anything is allowed to use — vitals, the DM, the desk.
## Sums the structured notes AND a legacy `loan_principal` that has not met a
## tick yet, so a pre-migration save never reads as debt-free.
static func debt_total(state: GameState) -> int:
	var total := state.loan_principal
	for l in state.loans:
		total += int((l as Dictionary).get("balance", 0))
	return total

## The worst rate on the books, as a percent — what the vitals line names.
static func worst_rate(state: GameState) -> float:
	var worst := SHARK_RATE if state.loan_principal > 0 else 0.0
	for l in state.loans:
		if int((l as Dictionary).get("balance", 0)) > 0:
			worst = maxf(worst, float((l as Dictionary).get("rate_wk", 0.0)))
	return worst

## The floor under a quote: a small business pays a small-business premium
## until it has a real balance sheet behind it.
static func era_rate_floor(state: GameState) -> float:
	return 0.04 if state.era == "coworking" else 0.02

## THE RISK-PRICED QUOTE (§4.1). Real analogue: SMB lending priced off
## debt-service coverage and time in business — runway proxies default
## probability, a revenue slump proxies coverage, era proxies track record.
## Simplification drops: credit files, personal guarantees, collateral haircuts
## — runway and growth ARE the books here. Deliberately rng-free.
static func bank_rate_wk(state: GameState) -> float:
	var rw := SimEngine.runway_weeks(state)
	var health := clampf((12.0 - float(rw)) / 12.0, 0.0, 1.0)
	var slump := clampf(-state.last_growth / 0.25, 0.0, 1.0)
	var rate := 0.03 + 0.07 * health + 0.02 * slump - 0.005 * float(state.era_index())
	return clampf(rate, era_rate_floor(state), RATE_CAP)

## Venture debt carries a cheaper coupon because the lender takes warrants
## instead — and here it really does (§4.1 + docs/design/DECISIONS.md).
static func venture_rate_wk(state: GameState) -> float:
	return maxf(bank_rate_wk(state) - 0.01, 0.02)

## The revenue expression `runway_weeks` uses, so the cap and the runway can
## never disagree about how big this company is.
static func rev_wk(state: GameState) -> float:
	var a := SimEngine.offers_arpu(state)
	if a < 0.0:
		a = float(state.theta.get("arpu_wk", 4.0)) * state.price_mult
	return float(state.traction) * a

## WHAT THE BANK WILL LEND AT ALL, before what you already owe (§2). No bank
## answers a garage; a coworking line is sized off revenue alone; from the
## office up the era's own spend cap joins it as a proxy for balance sheet.
static func borrow_cap(state: GameState) -> int:
	var r := rev_wk(state)
	match state.era:
		"coworking":
			return clampi(int(4.0 * r), 0, 10_000)
		"office", "floor":
			return clampi(int(8.0 * r + 0.25 * float(SimEngine.era_spend_cap(state.era))), 0, 150_000)
		"hq":
			return clampi(int((8.0 * r + 0.25 * float(SimEngine.era_spend_cap(state.era))) * 1.5), 0, 500_000)
	return 0

## What is left of the line. Shark balances do not count against it — they are
## off-book by nature, which is most of what makes them a shark (§4.2).
static func borrow_headroom(state: GameState) -> int:
	var used := 0
	for l in state.loans:
		var ld: Dictionary = l
		if String(ld.get("kind", "")) != "shark":
			used += int(ld.get("balance", 0))
	return maxi(borrow_cap(state) - used, 0)

## Venture debt is sized off the last equity round (30% — the market's own
## heuristic), so it is a post-raise instrument by construction. Never raised,
## never available: an old save reads 0 until the next round closes.
static func venture_cap(state: GameState) -> int:
	if state.era_index() < 3:
		return 0
	var used := 0
	for l in state.loans:
		var ld: Dictionary = l
		if String(ld.get("kind", "")) == "venture":
			used += int(ld.get("balance", 0))
	return maxi(int(0.30 * float(state.last_round_amount)) - used, 0)

## The terms this era's paper comes in.
static func term_options(state: GameState, kind: String = "bank") -> Array:
	if kind == "venture":
		return TERMS_VENTURE
	return TERMS_EARLY if state.era_index() <= 1 else TERMS_FULL

## THE LEVEL-PAYMENT ANNUITY — the standard installment loan, and the whole
## amortization lesson in one line. Real analogue: a fixed-payment term note.
## Simplification drops: day-count conventions and origination fees (the weekly
## tick IS the period, and a fee would be sub-$100 noise).
static func loan_payment_wk(principal: int, rate: float, term: int) -> int:
	if principal <= 0:
		return 0
	if term <= 0:
		return principal
	if rate <= 0.0:
		return int(ceil(float(principal) / float(term)))
	return int(ceil(float(principal) * rate / (1.0 - pow(1.0 + rate, -float(term)))))

## How many payments are left at THIS payment and THIS balance — the honest
## count, so a missed week visibly lengthens the note. -1 = the payment no
## longer covers the interest and the note never clears on its own.
static func note_weeks_left(balance: int, rate: float, pay: int) -> int:
	if balance <= 0:
		return 0
	if pay <= 0:
		return -1
	if rate <= 0.0:
		return int(ceil(float(balance) / float(pay)))
	var owed := float(balance) * rate
	if float(pay) <= owed:
		return -1
	return int(ceil(-log(1.0 - float(balance) * rate / float(pay)) / log(1.0 + rate)))

## CREDIT LOCK IS DERIVED, NEVER A FLAG (§4.5): two misses on a live note and
## the bank stops answering. Self-healing — repay the distressed note and the
## lock lifts. Defaulting cannot launder it, because a sharked note keeps its
## `missed` count.
static func credit_locked(state: GameState) -> bool:
	for l in state.loans:
		var ld: Dictionary = l
		if int(ld.get("missed", 0)) >= 2 and int(ld.get("balance", 0)) > 0:
			return true
	return false

## The weekly debt service the books are actually committed to: a bank note's
## level payment, a venture note's coupon. The shark is not a schedule, it is
## a claw, so it stays out of the fixed-cost reading.
static func debt_service_wk(state: GameState) -> int:
	var total := 0
	for l in state.loans:
		var ld: Dictionary = l
		var bal := int(ld.get("balance", 0))
		if bal <= 0:
			continue
		match String(ld.get("kind", "")):
			"bank":
				total += mini(int(ld.get("pay_wk", 0)), bal)
			"venture":
				total += int(ceil(float(bal) * float(ld.get("rate_wk", 0.0))))
	return total

## One live status multiplier — the same product the tick's own §8 loop builds,
## so break-even and the forecast price a week the way the week will be priced.
static func _status_mult(state: GameState, key: String, min_weeks: int = 1) -> float:
	var mult := 1.0
	for s in state.statuses:
		var sd: Dictionary = s
		if int(sd.get("weeks_left", 0)) < min_weeks:
			continue
		mult *= float(SimEngine.STATUS.get(String(sd.get("name", "")), {}).get(key, 1.0))
	return mult

static func _payroll(state: GameState) -> int:
	var p := 0
	for e in state.employees:
		p += int(e.get("salary", 0))
	for h in state.pipeline:
		p += int(h.get("salary", 0))       # paid before productive
	return p

static func _budget_sum(state: GameState) -> float:
	var total := float(state.marketing_budget)
	for k in state.budgets:
		total += float(state.budgets[k])
	return total

static func _standing_liab(state: GameState, min_weeks: int = 1) -> int:
	var owed := 0
	for c in state.commitments:
		var cd: Dictionary = c
		if int(cd.get("weeks_left", 0)) < min_weeks:
			continue
		owed += absi(mini(int(cd.get("cash_wk", 0)), 0))
	return owed

## TEXTBOOK CVP (§6): how many customers the fixed costs need before the
## machine feeds itself. Real analogue: contribution-margin break-even.
## Simplification drops: incidents (noise — they live in the forecast) and tax
## (it scales after profit; break-even is pre-tax by definition).
## -1 = no count breaks even, because each customer costs more than they pay.
static func break_even_customers(state: GameState) -> int:
	var th := state.theta
	var arpu_r := SimEngine.offers_arpu(state)
	if arpu_r < 0.0:
		arpu_r = float(th.get("arpu_wk", 4.0)) * state.price_mult
	var arpu := arpu_r * _status_mult(state, "arpu_mult")
	var burn_mult := float(th.get("burn_mult", 1.0))
	var var_pc := SimEngine.offers_cogs_per_customer(state) + 0.05 * burn_mult
	var margin := arpu - var_pc
	if margin <= 0.0:
		return -1
	var fixed_wk := (float(int(GameState.ERA_RENT.get(state.era, 150)) + _payroll(state) + 50)
			+ _budget_sum(state)) * burn_mult
	fixed_wk += SimEngine.offers_fixed_wk(state)
	fixed_wk += float(_standing_liab(state))
	fixed_wk += float(debt_service_wk(state))
	return int(ceil(fixed_wk / margin))

## What one customer contributes a week after the cost of serving them — the
## number the break-even count divides into. The desk prints it beside the count.
static func contribution_margin(state: GameState) -> float:
	var th := state.theta
	var arpu_r := SimEngine.offers_arpu(state)
	if arpu_r < 0.0:
		arpu_r = float(th.get("arpu_wk", 4.0)) * state.price_mult
	return arpu_r * _status_mult(state, "arpu_mult") - (SimEngine.offers_cogs_per_customer(state)
			+ 0.05 * float(th.get("burn_mult", 1.0)))

# ═══════════════════════ THE DESK'S WRITE PATH ══════════════════════════════

## SIGN A NOTE. The engine is the bouncer (docs/design/10-interface-language.md
## §4.7): the desk asks, this clamps, and a refusal comes back as an empty
## dictionary the desk turns into a printed reason. Returns the note it wrote.
static func sign_note(state: GameState, kind: String, amount: int, term: int) -> Dictionary:
	if credit_locked(state):
		return {}
	var rate := 0.0
	var cap := 0
	var terms: Array = []
	if kind == "venture":
		if state.era_index() < 3:
			return {}
		cap = mini(venture_cap(state), borrow_headroom(state))
		rate = venture_rate_wk(state)
		terms = TERMS_VENTURE
	else:
		kind = "bank"
		if state.era_index() < 1:
			return {}
		cap = borrow_headroom(state)
		rate = bank_rate_wk(state)
		terms = term_options(state, "bank")
	if cap < MIN_DRAW:
		return {}
	var draw := clampi(amount, MIN_DRAW, cap)
	var t := term if terms.has(term) else int(terms[0])
	var note := {
		"kind": kind, "principal": draw, "balance": draw, "rate_wk": rate,
		"term_wk": t, "taken_week": state.week,
		"pay_wk": 0 if kind == "venture" else loan_payment_wk(draw, rate, t),
		"missed": 0,
	}
	state.loans.append(note)
	state.cash += draw
	if kind == "venture":
		# THE WARRANT NIBBLE (docs/design/DECISIONS.md): a cheaper coupon is
		# never free — the lender takes a slice of the company instead.
		state.dilute_all(WARRANT_PCT)
		state.log_action("signed venture debt: +$%d at %.1f%%/wk, interest-only, balloon in %d wks (warrants %.2f%%)"
			% [draw, rate * 100.0, t, WARRANT_PCT])
	else:
		state.log_action("signed a bank note: +$%d at %.1f%%/wk for %d wks ($%d/wk)"
			% [draw, rate * 100.0, t, int(note["pay_wk"])])
	return note

## EARLY REPAY — the prepayment reward is the interest you never pay. No
## penalty (simplification: prepayment penalties dropped; rare in small
## business notes and pure friction here). The $500 ramen guard stands: the
## founder still eats. Returns what was actually paid.
static func repay_note(state: GameState, idx: int) -> int:
	if idx < 0 or idx >= state.loans.size():
		return 0
	var note: Dictionary = state.loans[idx]
	var pay := mini(state.cash - GameState.RAMEN_PER_WEEK, int(note.get("balance", 0)))
	if pay <= 0:
		return 0
	state.cash -= pay
	note["balance"] = int(note.get("balance", 0)) - pay
	state.log_action("repaid $%d of the %s note" % [pay, String(note.get("kind", "bank"))])
	if int(note["balance"]) <= 0:
		state.loans.remove_at(idx)
	return pay

# ═════════════════════════════ THE TICK ═════════════════════════════════════

## Tick §9, before the money is assembled: the legacy note joins the list.
## MIGRATION BY CONSTRUCTION (§3) — the engine is the only mutator, so this
## works for headless runs and both engines with no migrator code and no save
## bump. A $10,000 shark stays a $10,000 shark; only its shape changes.
static func tick_pre(state: GameState, _rep: Dictionary) -> void:
	_slips = []
	if state.loan_principal > 0:
		state.loans.append({"kind": "shark", "principal": state.loan_principal,
			"balance": state.loan_principal, "rate_wk": SHARK_RATE, "term_wk": 0,
			"taken_week": state.week, "pay_wk": 0, "missed": 0})
		state.loan_principal = 0

## The money section (§9c). Every note accrues, pays what it can, and says so.
## Interest is ACCRUED, not paid — a missed week still bills the P&L, because
## that is what accrual accounting means and the receipt says "owe", not "paid".
static func tick_money(state: GameState, rep: Dictionary, m: Dictionary) -> void:
	var kept: Array = []
	# PRINCIPAL IS NOT A P&L LANE — paying down a balance is a balance-sheet
	# move, not an expense, which is exactly why the ledger prints it beside
	# interest and tax rather than inside burn. Carried as a meta because it
	# describes the week, not the company (docs/design/06-finance.md §9).
	var principal_wk := 0
	for l in state.loans:
		var note: Dictionary = l
		var bal := int(note.get("balance", 0))
		if bal <= 0:
			continue
		var rate := float(note.get("rate_wk", 0.0))
		var kind := String(note.get("kind", "shark"))
		var interest := int(ceil(float(bal) * rate))
		m["interest"] = float(m.get("interest", 0.0)) + float(interest)
		if kind == "shark":
			# THE SHARK'S CHARACTER, verbatim from the legacy block: it compounds
			# whether you look or not, and it takes everything above
			# walking-around money the moment there is any.
			bal += interest
			note["balance"] = bal
			rep["lines"].append("the loan compounds: +$%d interest (owe $%d)" % [interest, bal])
			if state.cash > CLAW_TRIGGER:
				var claw := mini(state.cash - CLAW_KEEP, bal)
				state.cash -= claw
				bal -= claw
				principal_wk += claw
				note["balance"] = bal
				rep["lines"].append("auto-repaid $%d of the loan" % claw)
			if bal <= 0:
				rep["lines"].append("the shark is paid off — nothing feeds first any more")
		elif kind == "venture":
			# INTEREST-ONLY, THEN THE BALLOON — the real venture-debt shape.
			var balloon_wk := int(note.get("taken_week", 0)) + int(note.get("term_wk", 0))
			if state.cash >= interest:
				state.cash -= interest
				rep["lines"].append("the venture note takes its coupon: −$%d, interest only — $%d principal still waits" % [interest, bal])
			else:
				_miss(state, rep, note, "venture note", interest, interest)
			bal = int(note.get("balance", 0))
			if state.week >= balloon_wk and bal > 0 and String(note.get("kind", "")) == "venture":
				if state.cash >= bal:
					state.cash -= bal
					principal_wk += bal
					rep["lines"].append("the balloon landed and you covered it: −$%d — the venture note closes" % bal)
					bal = 0
					note["balance"] = 0
				else:
					# THE WORKOUT (real distressed refi): the paper re-papers
					# harder rather than the company dying of a date.
					var wrate := minf(rate + 0.02, RATE_CAP)
					note["kind"] = "bank"
					note["rate_wk"] = wrate
					note["term_wk"] = 8
					note["taken_week"] = state.week
					note["pay_wk"] = loan_payment_wk(bal, wrate, 8)
					rep["lines"].append("the balloon came due with no cash behind it: the note re-papers at %.1f%%/wk over 8 wks — a workout, not a rescue" % (wrate * 100.0))
		else:
			# THE AMORTIZED BANK NOTE. The split in the receipt IS the lesson:
			# part of a payment is rent on the money, part of it is the money.
			var pay_wk := int(note.get("pay_wk", 0))
			var due := mini(pay_wk, bal + interest)
			if pay_wk > 0 and state.cash >= due:
				state.cash -= due
				var principal_paid := due - interest
				principal_wk += maxi(principal_paid, 0)
				bal = bal + interest - due
				note["balance"] = bal
				if bal <= 0:
					rep["lines"].append("the bank's draw: −$%d ($%d interest · $%d principal) — the bank note is PAID, the folder closes" % [due, interest, maxi(principal_paid, 0)])
				else:
					var left := note_weeks_left(bal, rate, pay_wk)
					rep["lines"].append("the bank's draw: −$%d ($%d interest · $%d principal) — $%d left, %s" % [
						due, interest, maxi(principal_paid, 0), bal,
						("%d wks" % left) if left >= 0 else "no end at this payment"])
			else:
				_miss(state, rep, note, "bank", due if pay_wk > 0 else interest, interest)
		if int(note.get("balance", 0)) > 0:
			kept.append(note)
	state.loans = kept
	state.set_meta("bank_principal_wk", principal_wk)
	# THE TREASURY SWEEP (hq): idle cash is not free money sitting still, it is
	# a money-market balance earning its keep. Credited against the interest
	# lane, because that lane is the cost of money in both directions.
	if state.era == "hq" and state.cash > SWEEP_FLOOR:
		var sweep := int(SWEEP_RATE * float(state.cash - SWEEP_FLOOR))
		if sweep > 0:
			state.cash += sweep
			m["interest"] = float(m.get("interest", 0.0)) - float(sweep)
			rep["lines"].append("the sweep account pays $%d on idle cash — money at rest still earns" % sweep)

## THE MISS LADDER (§4.5). A payment cash cannot cover is SKIPPED, never drawn
## into the red — banks do not overdraw you, and rent and payroll already do.
## Real analogues in order: delinquency, default interest after a covenant
## breach, then a charged-off debt sold to a collection agency.
static func _miss(state: GameState, rep: Dictionary, note: Dictionary,
		what: String, due: int, interest: int) -> void:
	note["missed"] = int(note.get("missed", 0)) + 1
	note["balance"] = int(note.get("balance", 0)) + interest     # unpaid interest capitalizes
	state.morale = clampi(state.morale - 3, 0, 100)
	rep["lines"].append("MISSED the %s ($%d due, $%d in hand) — the balance grows" % [
		what, due, maxi(state.cash, 0)])
	var missed := int(note["missed"])
	if missed == 2:
		var repriced := minf(float(note.get("rate_wk", 0.0)) + 0.02, RATE_CAP)
		note["rate_wk"] = repriced
		rep["lines"].append("the bank repriced the risk: %.1f%%/wk now — a covenant breach costs interest" % (repriced * 100.0))
	elif missed >= 3:
		note["kind"] = "shark"
		note["rate_wk"] = SHARK_RATE
		note["pay_wk"] = 0
		note["term_wk"] = 0
		SimEngine.add_status(state, "collections_calls", 4)
		rep["lines"].append("sold to the collectors — 18%/wk now, and investors do check your credit")

## THE STATE, charged last (§5). Corporate income tax on EBT — earnings BEFORE
## tax and AFTER interest, because interest is deductible on a real P&L, and
## that ordering IS the lesson. Real analogue: estimated-tax prepayments.
## Simplification drops: quarterly filing (every other lane is weekly) and the
## 80% NOL offset limit (needless arithmetic for the same lesson).
static func tax_wk(state: GameState, m: Dictionary) -> int:
	if state.era_index() < TAX_ERA:
		return 0                       # below the office: cash-basis, below the radar
	var net_ops := float(m.get("revenue", 0.0)) - float(m.get("burn", 0)) - float(m.get("liabilities_wk", 0))
	var ebt := int(round(net_ops - float(m.get("interest", 0.0))))
	if ebt < 0:
		# THE LOSS CARRYFORWARD: without it one good week inside a losing month
		# pays tax while the company bleeds, which reads as a bug to a founder.
		state.tax_loss_carry += -ebt
		return 0
	var shelter := mini(state.tax_loss_carry, ebt)
	state.tax_loss_carry -= shelter
	var tax := int(round(TAX_RATE * float(ebt - shelter)))
	if shelter > 0:
		_slips.append("old losses shelter $%d of profit — no tax on that slice" % shelter)
	if tax <= 0:
		return 0
	state.cash -= tax
	var line := "the taxman's cut: −$%d (20%% of EBT $%d — profit after interest)" % [tax, ebt - shelter]
	if not state.has_flag("tax_noticed"):
		state.set_flag("tax_noticed")
		line = "now you're on the radar: " + line
	_slips.append(line)
	return tax

## After the record is written (§9e/§9f): the taxman's receipt reads the
## finished week back, the net-30 float moves, and the first break-even
## crossing gets the beat it deserves.
static func tick_post(state: GameState, rep: Dictionary) -> void:
	for s in _slips:
		rep["lines"].append(s)
	_slips = []
	var pnl: Dictionary = state.get_meta("pnl", {})
	# 9e RECEIVABLES (floor+) — working-capital float, the net-30 reality of
	# enterprise-scale revenue. P&L revenue is unchanged (accrual): this desk
	# teaches profit ≠ cash with real numbers.
	# Simplification drops: bad debt — collections always arrive.
	var matured := 0
	var kept: Array = []
	for r in state.receivables:
		var rd: Dictionary = r
		rd["weeks_left"] = int(rd.get("weeks_left", 1)) - 1
		if int(rd["weeks_left"]) <= 0:
			matured += int(rd.get("cash_wk", 0))
		else:
			kept.append(rd)
	state.receivables = kept
	if matured > 0:
		state.cash += matured
		rep["lines"].append("a net-30 invoice cleared: +$%d — the cash finally caught up with the profit" % matured)
	if state.era_index() >= 3:
		var invoiced := int(RECEIVABLE_FRAC * float(pnl.get("revenue", 0)))
		if invoiced > 0:
			state.cash -= invoiced
			state.receivables.append({"name": "invoiced on net-30", "cash_wk": invoiced,
				"weeks_left": RECEIVABLE_WK})
			rep["lines"].append("invoiced $%d on net-30 — booked now, cash in %d weeks" % [invoiced, RECEIVABLE_WK])
	# 9f BREAK-EVEN, the first crossing only — a milestone, not a meter.
	var be := break_even_customers(state)
	if be > 0 and state.traction >= be and not state.has_flag("broke_even"):
		state.set_flag("broke_even")
		rep["events"].append("BREAK-EVEN — %d customers now feed the machine." % be)

# ═════════════════════ THE FORECAST (pure, expectation-only) ════════════════

## THE FP&A 13-WEEK CASH MODEL, scaled to the game's 4-week attention span.
## PURE: it operates on locals and never touches `state`. EXPECTATION-ONLY:
## no draw anywhere, so the strip is a plan, not a prophecy — which is why it
## says "before surprises" on the sheet.
##
## Included and evolving: adds, churn, revenue, cogs, infra, the loan schedule
## (payments assumed made), receivables maturities, tax on projected EBT with a
## local copy of the carryforward, statuses only while they are still alive.
## Frozen: market_trend (its walk is rng), rival pressure, hype, product,
## payroll, prices, budgets, era. Excluded and named: incidents, new standing
## liabilities, DM effects, morale and outage rolls.
static func forecast_cash(state: GameState, weeks: int = FORECAST_WEEKS) -> Array:
	var out: Array = []
	var th := state.theta
	if th.is_empty():
		return out
	var N := float(th.get("tam", 120_000.0))
	var A := float(state.traction)
	var cash := float(state.cash)
	var carry := state.tax_loss_carry
	var era_i := state.era_index()
	# local copies — nothing below may reach a live dictionary
	var notes: Array = []
	for l in state.loans:
		notes.append((l as Dictionary).duplicate(true))
	var recv: Array = []
	for r in state.receivables:
		recv.append((r as Dictionary).duplicate(true))
	# the frozen half of the world
	var pressure := 0.0
	for rv in state.rivals:
		pressure += float((rv as Dictionary).get("strength", 0.0))
	pressure = minf(pressure / maxf(float(state.rivals.size()), 1.0) / 100.0 * 0.5, 0.45)
	var hype_mult := 0.6 + float(state.hype) / 100.0 * 0.9
	var b_mk := float(int(state.budgets.get("ads", 0)) + int(state.budgets.get("content", 0))
			+ int(state.budgets.get("referrals", 0)) + int(state.budgets.get("outbound", 0))
			+ state.marketing_budget)
	var b_sales := float(state.budgets.get("sales", 0))
	var b_care := float(state.budgets.get("care", 0))
	var b_rnd := float(state.budgets.get("rnd", 0))
	var b_office := float(state.budgets.get("office", 0))
	# TYPED, not inferred: the reach term crosses a lane boundary, and a lane
	# that hands back an untyped value must not be able to break this file's parse.
	var mk_mult: float = SimFunnel.reach_mult(state, b_mk,
			1.0 + 1.4 * (1.0 - exp(-b_mk / float(th.get("cac_sat", 8_000.0)))))
	var launched := state.has_flag("launched")
	var quality_gate := 0.2 + float(state.product) / 100.0 * 0.8
	var residence := float(th.get("lifetime_wk", 40.0)) * (0.4 + float(state.product) / 100.0 * 1.2)
	var care_mult := 1.0 - 0.30 * (1.0 - exp(-b_care / 1500.0))
	var price_pain := SimEngine.offers_price_pain(state)
	var price_demand := pow(maxf(state.price_mult, 0.1), -1.5)
	var offer_mult := SimEngine.offers_demand_mult(state)
	if offer_mult >= 0.0:
		price_demand = offer_mult
	price_demand = clampf(price_demand, 0.1, 3.0)
	var sales_heads := 0
	for e in state.employees:
		if String(e.get("role", "")).contains("sales"):
			sales_heads += 1
	var cap_scale := 1.0
	match state.biz_who:
		"SMB": cap_scale = 3.0
		"Consumer": cap_scale = 40.0
	var gtm_cap := (1.5 + 0.8 * float(state.competences.get("sell", 3))
			+ 3.0 * float(sales_heads) + b_mk / 400.0 + b_sales / 600.0) * cap_scale
	var arpu_base := SimEngine.offers_arpu(state)
	if arpu_base < 0.0:
		arpu_base = float(th.get("arpu_wk", 4.0)) * state.price_mult
	var cogs_pc := SimEngine.offers_cogs_per_customer(state)
	var offer_fixed := SimEngine.offers_fixed_wk(state)
	var burn_mult := float(th.get("burn_mult", 1.0))
	var rent := float(int(GameState.ERA_RENT.get(state.era, 150)))
	var payroll := float(_payroll(state))
	var levers := b_mk + b_sales + b_care + b_rnd + b_office
	for w in range(1, maxi(weeks, 1) + 1):
		# STATUSES COUNT ONLY WHILE THEY ARE STILL ALIVE in that week, and the
		# arithmetic is the tick's own: §2 decrements before §8 reads, so a
		# status with `weeks_left` k survives into projected week w iff k > w.
		var s_adopt := _status_mult(state, "adopt_mult", w + 1)
		var s_churn := _status_mult(state, "churn_mult", w + 1)
		var s_arpu := _status_mult(state, "arpu_mult", w + 1)
		var P := maxf(N - A, 0.0)
		var p_eff := float(th.get("adopt_p", 0.00025)) * hype_mult * mk_mult * s_adopt \
				* state.market_trend * (1.0 - pressure) * quality_gate \
				* (1.0 if launched else 0.0)
		var wom := float(th.get("adopt_ic", 0.06)) * A * P / maxf(N, 1.0) * s_adopt \
				* (1.0 - pressure) * quality_gate * (1.0 if launched else 0.5)
		var adds := minf((p_eff * P + wom) * price_demand, gtm_cap)
		var churn := A / maxf(residence, 2.0) * float(th.get("churn_mult", 1.0)) \
				* s_churn * care_mult * price_pain
		A = maxf(A + adds - churn, 0.0)
		var revenue := A * arpu_base * s_arpu
		var cogs := A * cogs_pc
		var infra := 50.0 + A * 0.05
		var burn := (rent + payroll + infra + levers) * burn_mult + cogs + offer_fixed
		cash += revenue - burn
		# the loan schedule, payments assumed made — a forecast that assumed a
		# default would be a threat, not a plan
		var interest := 0.0
		for nn in notes:
			var note: Dictionary = nn
			var bal := int(note.get("balance", 0))
			if bal <= 0:
				continue
			var rate := float(note.get("rate_wk", 0.0))
			var due_int := int(ceil(float(bal) * rate))
			interest += float(due_int)
			var kind := String(note.get("kind", "shark"))
			if kind == "shark":
				bal += due_int
				if cash > float(CLAW_TRIGGER):
					var claw := minf(cash - float(CLAW_KEEP), float(bal))
					cash -= claw
					bal -= int(claw)
			elif kind == "venture":
				cash -= float(due_int)
				if state.week + w >= int(note.get("taken_week", 0)) + int(note.get("term_wk", 0)):
					cash -= float(bal)
					bal = 0
			else:
				var due := mini(int(note.get("pay_wk", 0)), bal + due_int)
				cash -= float(due)
				bal = bal + due_int - due
			note["balance"] = maxi(bal, 0)
		var liab := float(_standing_liab(state, w))
		cash -= liab
		var net_ops := revenue - burn - liab
		var ebt := net_ops - interest
		var tax := 0.0
		if era_i >= TAX_ERA:
			if ebt < 0.0:
				carry += int(round(-ebt))
			else:
				var shelter := minf(float(carry), ebt)
				carry -= int(round(shelter))
				tax = round(TAX_RATE * (ebt - shelter))
				cash -= tax
		# the net-30 float: what clears this week, and what this week defers
		var kept_r: Array = []
		for r2 in recv:
			var rd: Dictionary = r2
			rd["weeks_left"] = int(rd.get("weeks_left", 1)) - 1
			if int(rd["weeks_left"]) <= 0:
				cash += float(rd.get("cash_wk", 0))
			else:
				kept_r.append(rd)
		recv = kept_r
		if era_i >= 3:
			var invoiced := float(int(RECEIVABLE_FRAC * revenue))
			cash -= invoiced
			recv.append({"cash_wk": int(invoiced), "weeks_left": RECEIVABLE_WK})
		if state.era == "hq" and cash > float(SWEEP_FLOOR):
			var sweep := float(int(SWEEP_RATE * (cash - float(SWEEP_FLOOR))))
			cash += sweep
			interest -= sweep
		out.append({"wk": state.week + w, "cash": int(round(cash)),
			"net": int(round(net_ops - interest - tax)), "revenue": int(round(revenue))})
	return out

# ═══════════════════ WHAT THE REST OF THE GAME READS ════════════════════════

## DM context lines, section 10 of the DIRECTIVES block (docs/design/00-spine.md
## §5). The DM narrates the debt; it never prices it.
static func directives(state: GameState) -> Array[String]:
	var out: Array[String] = []
	var shown := 0
	for l in state.loans:
		var ld: Dictionary = l
		var bal := int(ld.get("balance", 0))
		if bal <= 0 or shown >= 2:
			continue
		shown += 1
		var kind := String(ld.get("kind", "bank"))
		if kind == "shark":
			out.append("- Loan: $%d at 18.0%%/wk; the shark feeds before anyone else." % bal)
		elif kind == "venture":
			out.append("- Loan: $%d at %.1f%%/wk; interest only, balloon in %d wks." % [
				bal, float(ld.get("rate_wk", 0.0)) * 100.0,
				maxi(int(ld.get("taken_week", 0)) + int(ld.get("term_wk", 0)) - state.week, 0)])
		else:
			var left := note_weeks_left(bal, float(ld.get("rate_wk", 0.0)), int(ld.get("pay_wk", 0)))
			out.append("- Loan: $%d at %.1f%%/wk; payment $%d due in %d wks." % [
				bal, float(ld.get("rate_wk", 0.0)) * 100.0, int(ld.get("pay_wk", 0)), maxi(left, 0)])
	if credit_locked(state):
		out.append("- The bank has stopped answering: a note is in default and the collectors are calling.")
	if state.has_flag("tax_noticed"):
		var pnl: Dictionary = state.get_meta("pnl", {})
		var ebt := int(pnl.get("revenue", 0)) - int(pnl.get("burn", 0)) \
				- int(pnl.get("liabilities_wk", 0)) - int(pnl.get("interest", 0))
		out.append("- The taxman takes 20%% of profit now (EBT $%d last week)." % ebt)
	return out

## Attention rows — the bank (docs/design/00-spine.md §4). Each label is ≤40
## characters of pedagogy: the garage ticker prints it verbatim.
static func attention(state: GameState) -> Array:
	var rows: Array = []
	var service := debt_service_wk(state)
	var label := ""
	# the switch the red lands on (S2b): the distressed note's own card when
	# one note raised the alarm, else the borrow stepper (the cash-cliff case)
	var ctl := "borrow"
	if service > 0 and state.cash < 2 * service:
		label = "a note payment you cannot cover"
	for i in state.loans.size():
		var ld: Dictionary = state.loans[i]
		if int(ld.get("balance", 0)) <= 0:
			continue
		if int(ld.get("missed", 0)) >= 1:
			label = "missed a note — the balance grows"
			ctl = "note_%d" % i
		if String(ld.get("kind", "")) == "venture":
			var to_balloon := int(ld.get("taken_week", 0)) + int(ld.get("term_wk", 0)) - state.week
			if to_balloon <= 2 and state.cash < int(ld.get("balance", 0)):
				label = "balloon due soon — no cash for it"
				ctl = "note_%d" % i
	if label != "":
		rows.append({"desk": "the bank", "key": "debt_distress", "severity": 3, "label": label,
			"control": ctl})
	if state.has_flag("tax_noticed") and not state.has_flag("tax_seen"):
		rows.append({"desk": "the bank", "key": "first_tax", "severity": 2,
			"label": "the taxman found you — profit is taxed"})
	if state.has_flag("broke_even") and not state.has_flag("broke_even_seen"):
		rows.append({"desk": "the bank", "key": "broke_even", "severity": 1,
			"label": "BREAK-EVEN crossed — see the bank"})
	return rows
