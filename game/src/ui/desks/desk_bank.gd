class_name DeskBank
extends RefCounted
## DESK — the binder's `the bank` tab, the tenth. Spec: docs/design/06-finance.md
## Approved in docs/design/DECISIONS.md #1 and ruled in docs/design/00-spine.md
## §11: the ledger keeps the levers and the compact weekly P&L; the quote, the
## borrowing controls, the notes, the forecast, the sparklines, the tax block
## and the FULL grouped statement live HERE, at full width.
##
## `binder.gd` dispatches the tab body here and passes ITSELF, so this file draws
## through the binder's own helpers and never reaches into the sheet directly.
##
## A BANKER'S LETTER, not a form (docs/design/10-interface-language.md §5.2):
## the quote reads as a sentence about YOU with its reasons in the parenthesis,
## the preview does the amortization out loud before the pen touches paper, and
## SIGN THE NOTE carries the commit stroke so signing feels like signing. Notes
## stack like filed letters; the shark's line is one cold clause.
##
## TWO PAGE MODES behind one pen word, the crew idiom (§11): `""` is THE DESK
## (borrow, sign, repay, forecast) and `"books"` is THE BOOKS (the grouped
## statement, the sparklines, the tax block, the break-even arithmetic). Esc
## pops "books" back to the desk before it closes the binder — the shared
## contract in binder.gd, driven by the reserved `b.desk["mode"]` key.

# ── THE SHEET'S OWN GRID (mirrored byte-for-byte in DeskBank.cs) ─────────────
const Y_QUOTE := 78.0
const Y_COST := 116.0
const Y_RULE1 := 150.0
const Y_BORROW := 168.0
const Y_TERM := 230.0
const Y_PREVIEW := 294.0
const Y_SIGN := 330.0
const Y_RULE2 := 388.0
const Y_NOTES := 404.0
const NOTE_PITCH := 58.0
const NOTES_MAX := 3
const Y_FORECAST := 646.0
const X_TOGGLE := 860.0
const X_REPAY := 960.0
const X_SPARK := 600.0
## THE BOOKS' LAST LINE never crosses into the teaching footer at 700.
const Y_BOTTOM_MAX := 654.0

## Draw the desk. `b` is the Binder itself (untyped to keep the two files free
## of a cyclic class dependency).
static func draw(b) -> void:
	var state: GameState = b.state
	# THE ACK PATTERN (docs/design/00-spine.md §4/§11): a milestone bang is a
	# tap on the shoulder, not a permanent badge — looking at the desk answers it.
	if state.has_flag("tax_noticed"):
		state.set_flag("tax_seen")
	if state.has_flag("broke_even"):
		state.set_flag("broke_even_seen")
	if String(b.desk.get("mode", "")) == "books":
		_draw_books(b, state)
		return
	_draw_desk(b, state)

# ══════════════════════════ THE DESK ════════════════════════════════════════

static func _draw_desk(b, state: GameState) -> void:
	DeskKit.title(b, "the bank — money, debt, and the taxman")
	# THE PAGE TOGGLE. Words, never glyphs: the hand font carries no geometric
	# shapes at all, so a typed arrow arrives in somebody else's face (HOOKS.md).
	var to_books := func() -> void:
		b.desk["mode"] = "books"
	DeskKit.word(b, "the full books", Vector2(X_TOGGLE, 16.0), to_books,
		DeskKit.STATUS, Color(Binder.INK, 0.75), 260.0)
	var y := Y_QUOTE
	if state.era_index() < 1:
		y = _garage_block(b, state)
	else:
		y = _quote_block(b, state)
	y = _notes_block(b, state, y)
	if state.era_index() >= 1:
		# THE FORECAST IS A FLOOR, NOT A SLOT. Y_FORECAST is where it sits on an
		# ordinary sheet; on a floor-era sheet the venture block and a fourth filed
		# note push the notes down, and a fixed slot is a line drawn through them.
		_forecast_line(b, state, maxf(Y_FORECAST, y + 8.0))
	_desk_footer(b, state)

## NO BANK ANSWERS A GARAGE (docs/design/00-spine.md §9). The gate is TAUGHT,
## never greyed out: the player learns why credit exists at each stage, and the
## shark is the garage's whole lesson about the price of desperate money.
static func _garage_block(b, state: GameState) -> float:
	var y := Y_QUOTE
	b.label("no bank answers a garage — only the shark does.",
		Vector2(DeskKit.X_ID, y), DeskKit.STATUS, Color(Binder.INK, 0.75), 1100.0)
	y += 38.0
	b.label("a desk somewhere other than your kitchen is what puts you on their radar — banks lend against books, and a garage has none.",
		Vector2(DeskKit.X_ID, y), DeskKit.DETAIL, Color(Binder.INK, 0.5), 1100.0)
	y += 40.0
	b.label("money costs/wk: shark 18%  ·  equity: forever",
		Vector2(DeskKit.X_ID, y), DeskKit.DETAIL, Color(Binder.INK, 0.75), 1100.0)
	return DeskKit.rule(b, y + 40.0)

## THE BANKER SIZING YOU UP. Every number in the quote is a pure read of the
## books, so the reasons in the parenthesis ARE the price (§4.1).
static func _quote_block(b, state: GameState) -> float:
	var rate := SimBank.bank_rate_wk(state)
	var rw := SimEngine.runway_weeks(state)
	b.label("quotes %.1f%%/wk against your books (runway %s · growth %s%d%% · %s era)" % [
		rate * 100.0, ("%d wks" % rw) if rw < 999 else "gaining money",
		"+" if state.last_growth >= 0.0 else "−", int(round(absf(state.last_growth) * 100.0)),
		state.era], Vector2(DeskKit.X_ID, Y_QUOTE), DeskKit.STATUS, Binder.INK, 1100.0)
	# THE COST OF CAPITAL, all three prices side by side — the one comparison a
	# founder never makes early enough.
	b.label("money costs/wk: bank %.1f%%  ·  shark 18%%  ·  equity: forever" % (rate * 100.0),
		Vector2(DeskKit.X_ID, Y_COST), DeskKit.DETAIL, Color(Binder.INK, 0.75), 1100.0)
	DeskKit.rule(b, Y_RULE1)
	var headroom := SimBank.borrow_headroom(state)
	var terms: Array = SimBank.term_options(state, "bank")
	var borrow := int(b.desk.get("borrow", mini(10_000, maxi(headroom, SimBank.MIN_DRAW))))
	borrow = clampi(borrow, SimBank.MIN_DRAW, maxi(headroom, SimBank.MIN_DRAW))
	var term := int(b.desk.get("term", int(terms[mini(1, terms.size() - 1)])))
	if not terms.has(term):
		term = int(terms[0])
	# where the block ACTUALLY ended: the venture note is the one line below the
	# sign row, and the rule under it follows the ink rather than a constant
	var block_end := Y_RULE2
	var locked := SimBank.credit_locked(state)
	var no_line := headroom < SimBank.MIN_DRAW
	var dead := locked or no_line
	var floor_amt := maxi(headroom, SimBank.MIN_DRAW)
	var borrow_down := func() -> void:
		b.desk["borrow"] = clampi(int(DeskKit.ladder(SimBank.BORROW_STEPS, float(borrow), -1)),
			SimBank.MIN_DRAW, floor_amt)
	var borrow_up := func() -> void:
		b.desk["borrow"] = clampi(int(DeskKit.ladder(SimBank.BORROW_STEPS, float(borrow), 1)),
			SimBank.MIN_DRAW, floor_amt)
	var term_down := func() -> void:
		b.desk["term"] = int(DeskKit.ladder(terms, float(term), -1))
	var term_up := func() -> void:
		b.desk["term"] = int(DeskKit.ladder(terms, float(term), 1))
	DeskKit.stepper(b, Y_BORROW, {
		"name": "borrow", "why": "the draw — cash today, rented at the rate above",
		"value": "$%s" % b.fmt(borrow),
		"bound": "(all the line allows)" if borrow >= headroom and not dead else "",
		"effect": ("%d%% of a $%s line" % [int(round(float(borrow) / maxf(float(headroom), 1.0) * 100.0)), b.fmt(headroom)]) if not dead else "",
		"disabled": dead, "pitch": 62.0,
		"at_min": borrow <= SimBank.MIN_DRAW,
		"at_max": borrow >= headroom,
		"on_minus": borrow_down,
		"on_plus": borrow_up,
	})
	# THE WHY IS ONE MEASURED LINE. This sheet is a fixed grid (Y_* above), so a
	# why that wraps writes its second line straight through the preview.
	DeskKit.stepper(b, Y_TERM, {
		"name": "over", "why": "the term — longer weeks, smaller payment, more interest",
		"value": "%d weeks" % term,
		"effect": ("%d payments" % term) if not dead else "",
		"disabled": dead, "pitch": 62.0,
		"at_min": term <= int(terms[0]),
		"at_max": term >= int(terms[terms.size() - 1]),
		"on_minus": term_down,
		"on_plus": term_up,
	})
	# THE AMORTIZATION LESSON, DONE OUT LOUD before the pen moves: what a week
	# costs, what the whole note costs, and the difference between the two.
	var pay := SimBank.loan_payment_wk(borrow, rate, term)
	var all_in := pay * term
	if dead:
		b.label("no terms to preview until the bank answers.",
			Vector2(DeskKit.X_ID, Y_PREVIEW), DeskKit.DETAIL, Color(Binder.INK, 0.5), 1100.0)
	else:
		# THE ARITHMETIC MARK IS THE HOUSE'S OWN `=` (the blue sum lines everywhere
		# else on this desk). The hand carries no arrow at all, so a typed one
		# arrives in a borrowed face here and as a box on a machine without it.
		b.label("= $%s/wk  ·  ≈$%s all-in ($%s interest — that is what the time costs)" % [
			b.fmt(pay), b.fmt(all_in), b.fmt(maxi(all_in - borrow, 0))],
			Vector2(DeskKit.X_ID, Y_PREVIEW), DeskKit.STATUS, Binder.BLUE, 1100.0)
	if locked:
		b.label("the bank won't answer — clear the collectors first.",
			Vector2(DeskKit.X_ID, Y_SIGN + 8.0), DeskKit.STATUS, Binder.PEN, 1100.0)
	elif no_line:
		b.label("no revenue, no line — a bank lends against what customers already pay you.",
			Vector2(DeskKit.X_ID, Y_SIGN + 8.0), DeskKit.STATUS, Color(Binder.INK, 0.6), 1100.0)
	else:
		# THE SIGNATURE BEAT (§1.6.4): the stroke draws under the words, and only
		# then do the books change. The most consequential click in this desk
		# must never feel like a menu.
		var btn := DeskKit.word(b, "[ SIGN THE NOTE ]", Vector2(DeskKit.X_ID, Y_SIGN),
			Callable(), DeskKit.ROW, Binder.INK, 420.0)
		btn.pressed.connect(func() -> void:
			DeskKit.sign_stroke(b, btn, func() -> void:
				SimBank.sign_note(state, "bank", borrow, term)
				b.desk.erase("borrow")
				b.refresh()))
		# VENTURE DEBT rides the same block once a round has closed (floor+).
		var vcap := mini(SimBank.venture_cap(state), headroom)
		if vcap >= SimBank.MIN_DRAW:
			var vrate := SimBank.venture_rate_wk(state)
			var vbtn := DeskKit.word(b, "[ take venture debt ]", Vector2(460.0, Y_SIGN),
				Callable(), DeskKit.STATUS, Color(Binder.INK, 0.8), 380.0)
			vbtn.pressed.connect(func() -> void:
				DeskKit.sign_stroke(b, vbtn, func() -> void:
					SimBank.sign_note(state, "venture", vcap, SimBank.TERMS_VENTURE[0])
					b.refresh()))
			# ONE MEASURED LINE at 690px, clear of the 46px word above it — and the
			# rule below moves down to meet it. Wrapped and fixed, this line was
			# drawn through both the button and the divider, and its tail landed in
			# WHAT YOU OWE.
			var vy := Y_SIGN + 48.0
			b.label("$%s at %.1f%%/wk · interest-only · balloon in %d wks · %.2f%% in warrants" % [
				b.fmt(vcap), vrate * 100.0, int(SimBank.TERMS_VENTURE[0]), SimBank.WARRANT_PCT],
				Vector2(460.0, vy), DeskKit.LAW, Color(Binder.INK, 0.5), 690.0)
			block_end = maxf(block_end, vy + 34.0)
	return DeskKit.rule(b, block_end)

## THE FILED LETTERS. Each note carries what it costs, what is left, and how
## long — the cliff visible per note, never as one blended debt number.
static func _notes_block(b, state: GameState, y: float) -> float:
	b.label("WHAT YOU OWE", Vector2(DeskKit.X_ID, y), DeskKit.DETAIL, Color(Binder.INK, 0.6), 400.0)
	y += 30.0
	if state.loans.is_empty() and state.loan_principal <= 0:
		return DeskKit.empty(b, Vector2(DeskKit.X_ID, y),
			"you owe nobody anything. rare, and worth noticing.",
			"debt buys time, and time is the only thing a runway is made of.")
	var shown := 0
	# a legacy shark that has not met a tick yet still reads as debt (§3)
	if state.loan_principal > 0:
		b.label("THE SHARK — $%s (18%%/wk, it feeds first)" % b.fmt(state.loan_principal),
			Vector2(DeskKit.X_ID, y), DeskKit.ROW, Binder.PEN, 900.0)
		b.label("$%s/wk in interest alone — it takes everything above $2,000 the week you have it" % [
			b.fmt(int(ceil(float(state.loan_principal) * SimBank.SHARK_RATE)))],
			Vector2(DeskKit.X_ID, y + 34.0), DeskKit.DETAIL, Color(Binder.INK, 0.65), 900.0)
		y += NOTE_PITCH
		shown += 1
	for i in state.loans.size():
		if shown >= NOTES_MAX:
			break
		var note: Dictionary = state.loans[i]
		var bal := int(note.get("balance", 0))
		if bal <= 0:
			continue
		var rate := float(note.get("rate_wk", 0.0))
		var kind := String(note.get("kind", "shark"))
		var missed := int(note.get("missed", 0))
		var head := ""
		var sub := ""
		match kind:
			"shark":
				head = "THE SHARK — $%s (18%%/wk, it feeds first)" % b.fmt(bal)
				sub = "$%s/wk in interest alone — it takes everything above $2,000" % b.fmt(int(ceil(float(bal) * rate)))
			"venture":
				var to_balloon := maxi(int(note.get("taken_week", 0)) + int(note.get("term_wk", 0)) - state.week, 0)
				head = "venture note — $%s owed" % b.fmt(bal)
				sub = "interest-only $%s/wk · %.1f%%/wk · balloon $%s in %d wks" % [
					b.fmt(int(ceil(float(bal) * rate))), rate * 100.0, b.fmt(bal), to_balloon]
			_:
				var left := SimBank.note_weeks_left(bal, rate, int(note.get("pay_wk", 0)))
				head = "bank note — $%s left" % b.fmt(bal)
				sub = "$%s/wk · %.1f%%/wk · %s" % [b.fmt(int(note.get("pay_wk", 0))), rate * 100.0,
					("%d wks" % left) if left >= 0 else "no end at this payment"]
		if missed > 0:
			sub += " · missed %d" % missed
		b.label(head, Vector2(DeskKit.X_ID, y), DeskKit.ROW,
			Binder.PEN if (kind == "shark" or missed > 0) else Binder.INK, 900.0)
		b.label(sub, Vector2(DeskKit.X_ID, y + 34.0), DeskKit.DETAIL, Color(Binder.INK, 0.65), 900.0)
		# REPAY IS TWO-TAP because it books an immediate, irreversible cash cost:
		# the armed caption is where the invoice gets quoted (§2.9's money voice).
		var idx := i
		var quote := mini(state.cash - GameState.RAMEN_PER_WEEK, bal)
		if quote > 0:
			var fire := func() -> void:
				SimBank.repay_note(state, idx)
			DeskKit.arm(b, "repay_%d" % idx, "repay", "$%s now — sure?" % b.fmt(quote),
				Vector2(X_REPAY, y), fire, 200.0)
		else:
			b.label("nothing spare", Vector2(X_REPAY, y + 8.0), DeskKit.DETAIL,
				Color(Binder.INK, 0.35), 200.0)
		y += NOTE_PITCH
		shown += 1
	var live := 0
	for l in state.loans:
		if int((l as Dictionary).get("balance", 0)) > 0:
			live += 1
	if state.loan_principal > 0:
		live += 1
	return DeskKit.more(b, Vector2(DeskKit.X_ID, y + 2.0), live - shown, "notes are filed behind these")

## THE FP&A STRIP: what the plan does to the bank account, before surprises.
static func _forecast_line(b, state: GameState, y: float) -> void:
	var rows: Array = SimBank.forecast_cash(state, SimBank.FORECAST_WEEKS)
	# the teaching footer owns 700 down: a forecast with no room yields to it
	if rows.is_empty() or y + 34.0 > DeskKit.FOOTER_Y - 6.0:
		return
	var parts: Array = []
	var below := false
	for r in rows:
		var c := int((r as Dictionary).get("cash", 0))
		if c < 0:
			below = true
		parts.append(("−$%.1fk" % (absf(float(c)) / 1000.0)) if c < 0 else ("$%.1fk" % (float(c) / 1000.0)))
	b.label("the next %d weeks, as planned: %s (before surprises)" % [
		rows.size(), " -> ".join(PackedStringArray(parts))],
		Vector2(DeskKit.X_ID, y), DeskKit.STATUS,
		Binder.PEN if below else Color(Binder.INK, 0.8), 1100.0)

## THE DESK STATES ITS OWN LAWS, and the warning outranks them when one fires.
static func _desk_footer(b, state: GameState) -> void:
	var be := SimBank.break_even_customers(state)
	var computed := ""
	if be > 0:
		computed = "break-even: %d customers at these prices — %d on the books · each one contributes $%.1f/wk" % [
			be, state.traction, SimBank.contribution_margin(state)]
	else:
		computed = "no count breaks even — each customer costs $%.2f more than they pay" % absf(SimBank.contribution_margin(state))
	var warning := ""
	if SimBank.credit_locked(state):
		warning = "a note is in default: the collectors are calling and investors do check your credit"
	elif SimBank.debt_service_wk(state) > 0 and state.cash < 2 * SimBank.debt_service_wk(state):
		warning = "the repayment cliff: $%s a week is due and there is $%s in the bank" % [
			b.fmt(SimBank.debt_service_wk(state)), b.fmt(state.cash)]
	DeskKit.footer(b, {
		"computed": computed,
		"warning": warning,
		"rules": "the rules of this desk: a LOAN is rented money — you pay for the time, not "
			+ "the amount · INTEREST bills every week, sold or not · the taxman takes his cut "
			+ "of profit, never of revenue · repaying early is the only discount there is",
	})

# ══════════════════════════ THE BOOKS ═══════════════════════════════════════

## THE FULL GROUPED STATEMENT (docs/design/00-spine.md §2 display split):
## IN -> COST OF SERVING -> KEEPING THE LIGHTS ON -> THE LEVERS -> THE UNPLANNED ->
## THE BANK & THE STATE -> THE BOTTOM LINE. Grouped, because an income statement
## that is one flat list of lanes teaches nothing about which costs are which.
static func _draw_books(b, state: GameState) -> void:
	DeskKit.title(b, "the books — last week, line by line")
	var to_desk := func() -> void:
		b.desk["mode"] = ""
	DeskKit.back(b, "back to the bank", to_desk, Vector2(X_TOGGLE, 16.0))
	var pnl: Dictionary = state.get_meta("pnl", {})
	if pnl.is_empty():
		DeskKit.empty(b, Vector2(DeskKit.X_ID, Y_QUOTE),
			"no week has closed yet — the books open after the first LOCK IN.",
			"a P&L is a record of what happened, and nothing has.")
		return
	var y := Y_QUOTE
	y = _group(b, y, "IN", ["revenue $%s" % b.fmt(int(pnl.get("revenue", 0)))], Binder.BLUE)
	var learn := float(pnl.get("learning", 1.0))
	var serving: Array = ["cogs $%s%s" % [b.fmt(int(pnl.get("cogs", 0))),
		("  (learning ×%.2f — scale earns its margin)" % learn) if learn < 0.995 else ""]]
	# HARDWARE PAYS FOR ITS PARTS AT THE BENCH, not at the sale (09's ruling), so
	# the build lanes belong beside cogs rather than in a lane of their own. Both
	# keys are absent off a Hardware run, so this line simply does not exist there.
	var built := _some(b, pnl, [["production", "built in-house"], ["subcontract", "bought outside"]])
	if built != "":
		serving.append(built + " — hardware is paid at the bench, not at the sale")
	y = _group(b, y, "COST OF SERVING", serving, Binder.INK)
	var lights: Array = ["rent $%s · payroll $%s · infra $%s" % [
		b.fmt(int(pnl.get("rent", 0))), b.fmt(int(pnl.get("payroll", 0))), b.fmt(int(pnl.get("infra", 0)))]]
	var extra := _some(b, pnl, [["offer_fixed", "catalog"], ["equip_upkeep", "upkeep"], ["carrying", "carrying"]])
	if extra != "":
		lights.append(extra)
	y = _group(b, y, "KEEPING THE LIGHTS ON", lights, Binder.INK)
	y = _group(b, y, "THE LEVERS", ["marketing $%s · sales $%s · care $%s · rnd $%s · office $%s" % [
		b.fmt(int(pnl.get("marketing", 0))), b.fmt(int(pnl.get("sales", 0))),
		b.fmt(int(pnl.get("care", 0))), b.fmt(int(pnl.get("rnd", 0))),
		b.fmt(int(pnl.get("office", 0)))]], Binder.INK)
	var unplanned := _some(b, pnl, [["incident", "the unforeseen"], ["severance", "severance"],
		["recruiting", "recruiting"], ["liabilities_wk", "standing"]])
	if unplanned != "":
		y = _group(b, y, "THE UNPLANNED", [unplanned], Binder.INK)
	var principal := int(state.get_meta("bank_principal_wk", 0))
	y = _group(b, y, "THE BANK & THE STATE", ["interest $%s · principal $%s · tax $%s" % [
		b.fmt(int(pnl.get("interest", 0))), b.fmt(principal), b.fmt(int(pnl.get("tax", 0)))]], Binder.PEN)
	var net := int(pnl.get("net", 0))
	var be := SimBank.break_even_customers(state)
	# THE BOTTOM LINE GETS THE WHOLE WIDTH and a ceiling. Nine lanes can all bill
	# in one week: the groups above are a measured cursor, so on a busy week `y`
	# arrives near the footer, and at 560px this line wrapped into the desk laws.
	# The right column ends well above Y_BOTTOM_MAX, so the full width is free.
	b.label("THE BOTTOM LINE: %s$%s a week  ·  %s" % ["+" if net >= 0 else "−", b.fmt(absi(net)),
		("break-even %d customers (%d now)" % [be, state.traction]) if be > 0 else "no count breaks even"],
		Vector2(DeskKit.X_ID, minf(y + 4.0, Y_BOTTOM_MAX)), DeskKit.ROW,
		Binder.SAGE if net >= 0 else Binder.PEN, 1100.0)
	# ── the right column: the two series the bank itself prices you on
	var sy := DeskKit.spark(b, _net_series(state), Vector2(X_SPARK, Y_QUOTE),
		Vector2(540.0, 64.0), Binder.SAGE, "net, weekly:")
	sy = DeskKit.spark(b, b.series("revenue"), Vector2(X_SPARK, sy + 10.0),
		Vector2(540.0, 64.0), Binder.BLUE, "revenue, weekly:")
	sy += 14.0
	b.label("THE TAXMAN", Vector2(X_SPARK, sy), DeskKit.DETAIL, Color(Binder.INK, 0.6), 540.0)
	sy += 30.0
	var ebt := int(pnl.get("revenue", 0)) - int(pnl.get("burn", 0)) \
			- int(pnl.get("liabilities_wk", 0)) - int(pnl.get("interest", 0))
	if state.era_index() < SimBank.TAX_ERA:
		sy = _tax_line(b, sy, "nothing yet — profit is taxed from the office era up. Cash-basis and below the radar until then.")
	else:
		# SIGN OUTSIDE THE DOLLAR (10-interface-language §1.3): a loss-making week
		# reads −$13,804, never $-13,804.
		sy = _tax_line(b, sy, "20%% of EBT — earnings after interest, before tax. Last week's EBT: %s -> tax $%s" % [
			_signed(b, ebt), b.fmt(int(pnl.get("tax", 0)))])
	if state.tax_loss_carry > 0:
		sy = _tax_line(b, sy, "losses carried forward: $%s — they shelter the next profits before the taxman sees them" % b.fmt(state.tax_loss_carry))
	if state.receivables.size() > 0:
		var owed := 0
		for r in state.receivables:
			owed += int((r as Dictionary).get("cash_wk", 0))
		sy = _tax_line(b, sy, "net-30 float: $%s invoiced and not yet in the bank — profit is not cash" % b.fmt(owed))
	DeskKit.footer(b, {
		"computed": "burn is OPERATING spend only · interest and tax sit outside it, which is why the bottom line is smaller than in − out",
		"rules": "read it top to bottom: what came in, what serving cost, what the lights cost, "
			+ "what you chose to spend, what nobody planned, what the bank and the state took",
	})

## ONE LINE OF THE TAXMAN BLOCK, cursor-advanced by MEASURED height. A fixed
## 56px step wrote the loss-carryforward line straight through the second line of
## the EBT sentence the week the numbers got long enough to wrap.
static func _tax_line(b, sy: float, text: String) -> float:
	b.label(text, Vector2(X_SPARK, sy), DeskKit.DETAIL, Color(Binder.INK, 0.7), 540.0)
	return sy + maxf(b.wrap_h(text, DeskKit.DETAIL, 540.0), 28.0) + 14.0

## Money with the sign OUTSIDE the dollar (§1.3): −$300, never $-300.
static func _signed(b, v: int) -> String:
	return ("−$%s" % b.fmt(absi(v))) if v < 0 else ("$%s" % b.fmt(v))

## One captioned group of the statement. Returns the y it ended at.
static func _group(b, y: float, caption: String, lines: Array, col: Color) -> float:
	b.label(caption, Vector2(DeskKit.X_ID, y), DeskKit.DETAIL, Color(Binder.INK, 0.6), 540.0)
	y += 28.0
	for l in lines:
		b.label(String(l), Vector2(DeskKit.X_ID + 18.0, y), DeskKit.STATUS, col, 540.0)
		y += maxf(b.wrap_h(String(l), DeskKit.STATUS, 540.0), 30.0) + 4.0
	return y + 8.0

## The lanes that only exist some weeks, joined — and nothing at all when they
## are all zero, because a statement full of $0 rows teaches nothing.
static func _some(b, pnl: Dictionary, keys: Array) -> String:
	var parts: Array = []
	for kv in keys:
		var v := int(pnl.get(String((kv as Array)[0]), 0))
		if v != 0:
			parts.append("%s $%s" % [String((kv as Array)[1]), b.fmt(v)])
	return " · ".join(PackedStringArray(parts))

## The net series, with the honest fallback: a pre-finance history row has no
## `net` at all, so it reads as revenue − burn — close enough for history and
## exact from the week this lane landed (§3).
static func _net_series(state: GameState) -> Array:
	var out: Array = []
	for m in state.metric_history:
		var md: Dictionary = m
		if md.has("net"):
			out.append(float(md["net"]))
		else:
			out.append(float(int(md.get("revenue", 0)) - int(md.get("burn", 0))))
	return out

## A press inside this desk. Every control here carries its own closure, so
## nothing routes through the id dispatcher — the hook stays for the router.
static func handle(_b, _id: String) -> void:
	pass
