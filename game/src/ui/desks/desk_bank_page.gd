class_name DeskBankPage
extends RefCounted
## DESK — COSTS · "the bank" = THE MEETING (DAG2 W2 L-MONEY; DECISIONS: the
## bank = didactic rework A, four numbered zones read like an appointment at
## the branch; pixels: docs/design/mockups/08 option A; retrofit: the
## REFINANCE row, 12-binder-rework-2 §Retrofits).
##
##   1 YOUR STANDING — the rate DERIVED from the true risk-pricing inputs
##     (SimBank.bank_rate_wk: 3% base + runway worry + revenue slump −
##     track record, clamped by the era floor and the 12% cap). Never asserted.
##   2 WHAT YOU OWE — each note anatomized: the paid-off bar, the Monday
##     split into "pays the debt down" vs "the bank's fee"; the interest-only
##     bar never moves — that IS the lesson. + THE REFINANCE row (the
##     executor arrives with the works lane — preview renders meanwhile).
##   3 NEW MONEY — borrow/term with SEPARATE −/+ and THE RECEIPT printing
##     the whole truth before SIGN (the existing borrow op).
##   4 IF A MONDAY IS MISSED — the engine's own ladder as three stairs:
##     the balance grows -> repriced + the bank stops answering -> sold to
##     the collectors at the shark's price.
##
## THE COLLAPSE LADDER ON THE ZONES (the pane never scrolls): zones 1–2 are
## always open; zones 3 and 4 share the lower page — one open, the other a
## numbered one-line bar (press to swap). The didactic spine stays readable
## top to bottom either way.
##
## BOOKS mode = the full grouped statement restyled onto THE LEDGER SHEET.
## Two page modes behind one pen word; Esc pops "books" first.
##
## DAG3 (13-binder-ux): THE RECEIPT re-inks on every stepper press (a brief
## alpha dip — the pen going over the numbers again); the locked standing
## renders its unlock as a CHECKLIST read from the real lock state (the
## distressed note's own Mondays); DO lane [borrow] [repay — worst note]
## [refinance] as available; every note card registers "note_<i>" and the
## borrow stepper "borrow" so bills' interest row and the pre-roll land
## spotlit; the ask strip names the red under the hero; the hero wears its
## S5 delta. The DO lane rides the meeting only — the books are a read view.

const QUESTION := "what do we owe and can we borrow?"

const SHEET_X := 10.0
const ZONE_W := 1120.0
const Y_RULES := 844.0
## The meeting shows this many filed notes; the rest are counted.
const NOTES_MAX := 2
## The refinance executor lands with L-DIVWORKS (refinance_note). Until the
## coordinator flips this, the row renders its computed preview disabled.
const REFI_WIRED := true
const CARD_GAP := 18.0
const STAIR_COLS := [Color("F6F0DE"), Color("F2D6B8"), Color("D93425")]
const POS := Color("5D7A50")

## S8 — the bank sleeps through a debtless garage: no bank answers, nothing
## owed, nothing to read. The shark waking (any debt) wakes the tab.
static func is_dormant(state) -> bool:
	var s: GameState = state
	return s.era_index() < 1 and SimBank.debt_total(s) <= 0

## S10 — the rail's four-character read: what is owed.
static func micro_status(state) -> String:
	var s: GameState = state
	var debt := SimBank.debt_total(s)
	if debt <= 0:
		return ""
	if debt >= 1000:
		return "$%.1fk" % (float(debt) / 1000.0)
	return "$%d" % debt

static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	return {"big": "debt $%s" % _fmt(SimBank.debt_total(s)),
		"line": "the bank quotes %.1f%%/wk" % (SimBank.bank_rate_wk(s) * 100.0)}

static func draw(b) -> void:
	var state: GameState = b.state
	# THE ACK PATTERN (kept from the shipped desk): looking answers the bang.
	if state.has_flag("tax_noticed"):
		state.set_flag("tax_seen")
	if state.has_flag("broke_even"):
		state.set_flag("broke_even_seen")
	if String(b.desk.get("mode", "")) == "books":
		_draw_books(b, state)
		return
	_draw_meeting(b, state)

# ══════════════════════════════ THE MEETING ═════════════════════════════════

static func _draw_meeting(b, state: GameState) -> void:
	var debt := SimBank.debt_total(state)
	var service := _monday_out(state)
	var rate := SimBank.bank_rate_wk(state)

	# ── the hero
	var big: String = "we owe $" + b.fmt(debt)
	b.label(big, Vector2(SHEET_X, 6.0), DeskKit.HERO, DeskKit.INK, 560.0)
	var bw: float = b.font().get_string_size(big, HORIZONTAL_ALIGNMENT_LEFT, -1, DeskKit.HERO).x
	# S5 — which way the debt moved since the last open (prev read first)
	var prev_debt: String = b.seen_prev("the bank", "debt")
	b.seen("the bank", "debt", str(debt))
	var cap_x := SHEET_X + bw + 14.0
	if prev_debt != "" and int(prev_debt) != debt:
		DeskKit.delta_arrow(b, SHEET_X + bw + 10.0, 26.0, float(debt), float(prev_debt))
		cap_x += 26.0
	b.label("· $%s leaves every Monday" % b.fmt(service), Vector2(cap_x, 22.0),
		DeskKit.ROW, Color(DeskKit.INK, 0.7), 420.0)
	b.label(_hero_sentence(state), Vector2(SHEET_X, 62.0), DeskKit.DETAIL,
		Color(DeskKit.INK, 0.6), 700.0)
	var opinion: Label = b.label("the bank's opinion of you: %.1f%%/wk — why? see YOUR STANDING"
		% (rate * 100.0), Vector2(SHEET_X, 8.0), DeskKit.LAW, Color(DeskKit.INK, 0.6), ZONE_W)
	opinion.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	var to_books := func() -> void:
		b.desk["mode"] = "books"
	DeskKit.word(b, "BOOKS ▸ the full statement", Vector2(910.0, 34.0), to_books,
		DeskKit.LAW, Color(DeskKit.INK, 0.75), 220.0)
	var clock_txt := _deadline_text(state)
	if clock_txt != "":
		DeskKit.clock_chip(b, 910.0, 68.0, clock_txt)

	# S2a — red speaks on the page: the strip gets its own y under the hero
	# sentence and pushes the zones down only when it drew
	var y := 96.0
	if DeskKit.ask_strip(b, "the bank", SHEET_X, 88.0, 1000.0,
			"find the Monday or repay the note"):
		y = 118.0
	y = _zone_standing(b, state, y, rate)
	y = _zone_owed(b, state, y)
	# zones 3 and 4 share the lower page: one open, the other a bar
	if bool(b.desk.get("zone4", false)):
		y = _zone3_bar(b, state, y, rate)
		y = _zone_stairs(b, state, y)
	else:
		y = _zone_new_money(b, state, y, rate)
		y = _zone4_bar(b, state, y)
	_forecast_strip(b, state, y)
	_do_lane(b, state)
	_foot(b, state)

## S3 — the meeting's primary actions, one slot, as available: borrow opens
## zone 3, repay pays the dearest filed note down (the existing two-tap op),
## refinance fires the swap the tail line quotes (the existing sign op).
static func _do_lane(b, state: GameState) -> void:
	var actions: Array = []
	var garage := state.era_index() < 1
	var locked := SimBank.credit_locked(state)
	var headroom := SimBank.borrow_headroom(state)
	if not garage and not locked and headroom >= SimBank.MIN_DRAW:
		actions.append({"label": "borrow — up to $%s" % b.fmt(headroom),
			"cb": func() -> void: b.desk["zone4"] = false, "tier": ""})
	var worst := _worst_note(state)
	if worst >= 0:
		var quote: int = mini(state.cash - GameState.RAMEN_PER_WEEK,
			int((state.loans[worst] as Dictionary).get("balance", 0)))
		if quote > 0:
			var widx := worst
			actions.append({"label": "repay — the %.1f%% note" %
				(float((state.loans[worst] as Dictionary).get("rate_wk", 0.0)) * 100.0),
				"cb": func() -> void: SimBank.repay_note(state, widx), "tier": "two-tap"})
	var ridx := _refi_note(state)
	if REFI_WIRED and ridx >= 0 and not locked:
		var note: Dictionary = state.loans[ridx]
		if SimBank.bank_rate_wk(state) < float(note.get("rate_wk", 0.0)):
			var rterm := maxi(int(note.get("term_wk", 12)) \
				- (state.week - int(note.get("taken_week", state.week))), 4)
			actions.append({"label": "refinance — today's %.1f%%" %
				(SimBank.bank_rate_wk(state) * 100.0),
				"cb": func() -> void:
					SimWorks.op_refinance_note(state, {"old_id": ridx, "weeks": rterm}),
				"tier": "sign"})
	DeskKit.do_lane(b, actions)

## The dearest live FILED note — the one repay answers first. -1 = none.
static func _worst_note(state: GameState) -> int:
	var best := -1
	var best_rate := -1.0
	for i in state.loans.size():
		var note: Dictionary = state.loans[i]
		if int(note.get("balance", 0)) <= 0:
			continue
		if float(note.get("rate_wk", 0.0)) > best_rate:
			best_rate = float(note.get("rate_wk", 0.0))
			best = i
	return best

## Zone 1 — the rate is an opinion, derived from the engine's own inputs
## (only the terms that actually move it print; the zone is exactly as tall
## as its arithmetic). The garage and the credit lock teach instead.
static func _zone_standing(b, state: GameState, y: float, rate: float) -> float:
	var garage := state.era_index() < 1
	var locked := SimBank.credit_locked(state)
	if garage or locked:
		var hh := 172.0 if locked else 132.0
		var zz := DeskKit.zone(b, SHEET_X, y, ZONE_W, hh, 1, "your standing",
			"— the rate is not a constant; it is what the bank thinks of your books")
		var zx := float(zz.get("content_x", 0.0))
		var zy := float(zz.get("content_y", 0.0))
		if garage:
			b.label("no bank answers a garage — only the shark does, at 18%/wk.",
				Vector2(zx, zy - 4.0), DeskKit.DETAIL, DeskKit.INK, 1060.0)
			b.label("banks lend against books, and a garage has none — a desk somewhere real puts you on their radar.",
				Vector2(zx, zy + 22.0), DeskKit.LAW, Color(DeskKit.INK, 0.6), 1060.0)
		else:
			b.label("the bank stopped answering — a note is in default and the collectors are calling.",
				Vector2(zx, zy - 4.0), DeskKit.DETAIL, Binder.PEN, 1060.0)
			_unlock_checklist(b, state, zx, zy + 26.0)
			b.label("repay the distressed note and the lock lifts — it is derived, never a grudge.",
				Vector2(zx, zy + 62.0), DeskKit.LAW, Color(DeskKit.INK, 0.6), 1060.0)
		return y + hh + 4.0
	# the derivation rows, the engine's own terms (SimBank.bank_rate_wk)
	return _zone_standing_rows(b, state, y, rate)

## The unlock as a CHECKLIST, read from the REAL lock state: the distressed
## note frees itself one covered Monday at a time (note_weeks_left at its own
## payment), so the boxes are its Mondays — done filled, the rest waiting. A
## note with no schedule (sharked, or a payment under water) has one box:
## repay it whole.
static func _unlock_checklist(b, state: GameState, x: float, y: float) -> void:
	var idx := -1
	for i in state.loans.size():
		var note: Dictionary = state.loans[i]
		if int(note.get("missed", 0)) >= 2 and int(note.get("balance", 0)) > 0:
			idx = i
			break
	if idx < 0:
		return
	var nd: Dictionary = state.loans[idx]
	var bal := int(nd.get("balance", 0))
	var left := SimBank.note_weeks_left(bal, float(nd.get("rate_wk", 0.0)),
		int(nd.get("pay_wk", 0)))
	if int(nd.get("pay_wk", 0)) <= 0 or left < 0:
		DeskKit.pips(b, Vector2(x, y + 6.0), 0, 1)
		DeskKit.fit_line(b, "the unlock: repay the collectors in full — $%s, the only door"
			% b.fmt(bal), Vector2(x + 40.0, y + 2.0), DeskKit.DETAIL, DeskKit.INK, 900.0)
		return
	var done := maxi(int(nd.get("term_wk", 0)) - left, 0)
	var total := done + left
	var boxes: int = mini(total, 12)
	DeskKit.pips(b, Vector2(x, y + 6.0),
		int(round(float(done) / float(maxi(total, 1)) * float(boxes))) if total > 12 else done,
		boxes)
	DeskKit.fit_line(b, "the unlock: %d clean Monday%s of %d — $%s each, none missed"
		% [done, "" if done == 1 else "s", total, b.fmt(int(nd.get("pay_wk", 0)))],
		Vector2(x + float(boxes) * 21.0 + 18.0, y + 2.0), DeskKit.DETAIL, DeskKit.INK, 700.0)

static func _zone_standing_rows(b, state: GameState, y: float, rate: float) -> float:
	var rw := SimEngine.runway_weeks(state)
	var health := clampf((12.0 - float(rw)) / 12.0, 0.0, 1.0)
	var slump := clampf(-state.last_growth / 0.25, 0.0, 1.0)
	var era_disc := 0.005 * float(state.era_index())
	var raw := 0.03 + 0.07 * health + 0.02 * slump - era_disc
	var rows: Array = [["every company starts at", "3.0%", Color(DeskKit.INK, 0.85)]]
	if health > 0.0:
		rows.append(["only %d weeks of runway worries them" % rw,
			"+%.1f%%" % (health * 7.0), Binder.PEN])
	if slump > 0.0:
		rows.append(["revenue slipping %d%% worries them"
			% int(round(absf(state.last_growth) * 100.0)), "+%.1f%%" % (slump * 2.0), Binder.PEN])
	if era_disc > 0.0:
		rows.append(["%s-era track record reassures them" % state.era,
			"−%.1f%%" % (era_disc * 100.0), POS])
	var total_label := "your rate — repriced as your books change"
	if rate > raw + 0.0005:
		total_label = "your rate — the small-business floor holds it here"
	elif rate < raw - 0.0005:
		total_label = "your rate — capped; nobody prices above the shark"
	rows.append([total_label, "%.1f%%" % (rate * 100.0), DeskKit.INK])
	var h := 78.0 + float(rows.size()) * 20.0 + 19.0
	var z := DeskKit.zone(b, SHEET_X, y, ZONE_W, h, 1, "your standing",
		"— the rate is not a constant; it is what the bank thinks of your books")
	var cx := float(z.get("content_x", 0.0))
	var ry := float(z.get("content_y", 0.0)) - 6.0
	for i in rows.size():
		var r: Array = rows[i]
		var last := i == rows.size() - 1
		if last:
			ry += 7.0
			DeskKit.pen_rule(b, ry - 3.0, cx, 620.0, Color(DeskKit.INK, 0.6))
		b.label(String(r[0]), Vector2(cx, ry + 2.0), 17,
			(r[2] if last else Color(DeskKit.INK, 0.75)), 530.0)
		var v: Label = b.label(String(r[1]), Vector2(cx, ry + 2.0), 17, r[2], 620.0)
		v.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		ry += 20.0
	b.label("they would lend you up to $%s today — the credit line your books earn"
		% b.fmt(SimBank.borrow_headroom(state)),
		Vector2(cx + 680.0, float(z.get("content_y", 0.0)) + 2.0), DeskKit.LAW,
		Color(DeskKit.INK, 0.6), 370.0)
	return y + h + 4.0

## Zone 2 — every note cut open: the bar you are painting over, the Monday
## dollar split into debt-down vs fee. + the refinance row.
static func _zone_owed(b, state: GameState, y: float) -> float:
	var notes := _live_notes(state)
	var shown: int = mini(notes.size(), NOTES_MAX)
	var card_h := 122.0
	var h := 78.0 + (card_h + 24.0 + 8.0 if shown > 0 else 34.0)
	var z := DeskKit.zone(b, SHEET_X, y, ZONE_W, h, 2, "what you owe",
		"— a loan is a bar you are painting over; watch which loans never shrink")
	var cx := float(z.get("content_x", 0.0))
	var cy := float(z.get("content_y", 0.0))
	if notes.is_empty():
		b.label("you owe nobody anything. rare, and worth noticing — debt buys time, and time is what a runway is made of.",
			Vector2(cx, cy), DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 1060.0)
		return y + h + 4.0
	for k in shown:
		_note_card(b, state, notes[k], cx + float(k) * 552.0, cy - 6.0, 532.0, card_h)
	_tail_line(b, state, notes.size() - shown, cx, cy + card_h + 6.0)
	return y + h + 4.0

## One filed note, anatomized. `n` = {idx, note}.
static func _note_card(b, state: GameState, n: Dictionary, x: float, y: float,
		w: float, h: float) -> void:
	var idx := int(n.get("idx", -1))
	var note: Dictionary = n.get("note", {})
	var kind := String(note.get("kind", "shark"))
	var bal := int(note.get("balance", 0))
	var principal := maxi(int(note.get("principal", bal)), 1)
	var rate := float(note.get("rate_wk", 0.0))
	var interest := int(ceil(float(bal) * rate))
	var title := ""
	var chip := ""
	match kind:
		"bank":
			title = "bank note — term"
			chip = "shrinks as you pay"
		"venture":
			title = "venture note — interest only"
			chip = "never shrinks"
		_:
			title = "the shark — interest only"
			chip = "feeds first"
	var frame := DeskKit.card_frame(b, x, y, w, h, title)
	# S2b — the card is a landing pad: bills' interest row and the pre-roll
	# arrive here spotlit ("note_<i>"; the legacy shark files as note_-1)
	b.mark_control("note_%d" % idx, Rect2(x, y, w, h))
	var cx := float(frame.get("content_x", x))
	var cy := float(frame.get("content_y", y))
	b.label(chip, Vector2(x + w - 176.0, y + 14.0), 15,
		POS if kind == "bank" else Binder.PEN, 162.0)
	var paid := maxi(principal - bal, 0)
	DeskKit.meter(b, cx, cy + 0.0, w - 240.0, float(paid) / float(maxi(principal, bal)),
		DeskKit.SAGE if kind == "bank" else Binder.PEN)
	b.label("paid off $%s" % b.fmt(paid), Vector2(cx, cy + 24.0), 15, Color(DeskKit.INK, 0.55), 200.0)
	b.label("still owe $%s" % b.fmt(bal), Vector2(cx + 210.0, cy + 24.0), 15, DeskKit.INK, 200.0)
	# the Monday split, one compact line — the lesson in the arithmetic
	var split := ""
	match kind:
		"bank":
			var pay: int = mini(int(note.get("pay_wk", 0)), bal + interest)
			var down := maxi(pay - interest, 0)
			var left := SimBank.note_weeks_left(bal, rate, int(note.get("pay_wk", 0)))
			split = "$%s/Mon = $%s down + $%s fee · %s" % [b.fmt(pay), b.fmt(down),
				b.fmt(interest), ("%d left" % left) if left >= 0 else "no end"]
		"venture":
			var to_balloon := maxi(int(note.get("taken_week", 0)) + int(note.get("term_wk", 0)) - state.week, 0)
			split = "$%s/Mon all fee · balloon $%s in %d wks" % [b.fmt(interest), b.fmt(bal), to_balloon]
		_:
			split = "$%s/wk in fees — claws above $%s" % [b.fmt(interest), b.fmt(SimBank.CLAW_TRIGGER)]
	# the attention token leads — a trimmed tail never hides a missed Monday
	if int(note.get("missed", 0)) > 0:
		split = ("missed %d · " % int(note.get("missed", 0))) + split
	# the split line keeps its own lane: it ends before the repay arm and
	# trims with an ellipsis instead of printing under it
	var quote: int = mini(state.cash - GameState.RAMEN_PER_WEEK, bal)
	var has_arm := idx >= 0 and quote > 0
	var sl: Label = b.label(split, Vector2(cx, cy + 42.0), 15,
		Binder.PEN if (kind != "bank" or int(note.get("missed", 0)) > 0)
		else Color(DeskKit.INK, 0.7),
		(w - 36.0 - 212.0) if has_arm else (w - 36.0))
	sl.autowrap_mode = TextServer.AUTOWRAP_OFF
	sl.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	# repay — the existing two-tap op
	if has_arm:
		var fire := func() -> void:
			SimBank.repay_note(state, idx)
		DeskKit.arm(b, "repay_%d" % idx, "repay ▸", "−$%s now — sure?" % b.fmt(quote),
			Vector2(x + w - 216.0, cy + 34.0), fire, 200.0, 17)

## The line under the cards: the count of filed notes + THE REFINANCE row
## (mutation law: swap a note for today's standing, break fee on the old).
static func _tail_line(b, state: GameState, hidden: int, x: float, y: float) -> void:
	var parts: Array[String] = []
	if hidden > 0:
		parts.append("%d more note%s filed" % [hidden, "" if hidden == 1 else "s"])
	var idx := _refi_note(state)
	if idx >= 0:
		var note: Dictionary = state.loans[idx]
		var bal := int(note.get("balance", 0))
		var old_rate := float(note.get("rate_wk", 0.0))
		var new_rate := SimBank.bank_rate_wk(state)
		var fee := int(state.price_book.get("refinance_break_fee", 350))
		if new_rate >= old_rate:
			parts.append("refinance: today's %.1f%% beats nothing against the %.1f%% note"
				% [new_rate * 100.0, old_rate * 100.0])
		else:
			var rem := SimBank.note_weeks_left(bal, old_rate, int(note.get("pay_wk", 0)))
			var new_pay := SimBank.loan_payment_wk(bal, new_rate, maxi(rem, 4))
			parts.append("refinance: swap %.1f%% for %.1f%% — fee $%s · $%s -> $%s/Mon%s" % [
				old_rate * 100.0, new_rate * 100.0, b.fmt(fee),
				b.fmt(int(note.get("pay_wk", 0))), b.fmt(new_pay),
				"" if REFI_WIRED else " · papers arrive with the works wave"])
	if parts.is_empty():
		return
	b.label(" · ".join(parts), Vector2(x, y), DeskKit.LAW, Color(DeskKit.INK, 0.42), 940.0)
	# the executor, behind the same sign-stroke ritual as new money — the
	# preview line above IS the receipt (mutation law)
	var ridx := _refi_note(state)
	if REFI_WIRED and ridx >= 0:
		var refi_note: Dictionary = state.loans[ridx]
		var rterm := maxi(int(refi_note.get("term_wk", 12)) \
			- (state.week - int(refi_note.get("taken_week", state.week))), 4)
		var rsign := DeskKit.word(b, "[ swap it ]", Vector2(x + 950.0, y - 2.0),
			Callable(), DeskKit.LAW, DeskKit.INK, 110.0)
		var fire_refi := func() -> void:
			SimWorks.op_refinance_note(state, {"old_id": ridx, "weeks": rterm})
			b.refresh()
		rsign.pressed.connect(func() -> void:
			DeskKit.sign_stroke(b, rsign, fire_refi))

## Zone 3 — new money: the truth printed before the pen moves.
static func _zone_new_money(b, state: GameState, y: float, rate: float) -> float:
	var locked := SimBank.credit_locked(state)
	var headroom := SimBank.borrow_headroom(state)
	var garage := state.era_index() < 1
	var dead: bool = garage or locked or headroom < SimBank.MIN_DRAW
	var h := 124.0 if dead else 240.0
	var z := DeskKit.zone(b, SHEET_X, y, ZONE_W, h, 3, "new money",
		"— before you sign, the receipt shows what the money truly costs")
	var cx := float(z.get("content_x", 0.0))
	var cy := float(z.get("content_y", 0.0))
	if dead:
		b.label(_dead_reason(garage, locked), Vector2(cx, cy), DeskKit.DETAIL,
			Color(DeskKit.INK, 0.6), 1060.0)
		return y + h + 4.0
	var floor_amt: int = maxi(headroom, SimBank.MIN_DRAW)
	var borrow := clampi(int(b.desk.get("borrow", mini(10_000, floor_amt))), SimBank.MIN_DRAW, floor_amt)
	var terms: Array = SimBank.term_options(state, "bank")
	var term := int(b.desk.get("term", int(terms[mini(1, terms.size() - 1)])))
	if not terms.has(term):
		term = int(terms[0])
	# every stepper press re-inks THE RECEIPT (S4): the flag rides the desk
	# dict through the refresh the squares fire, and the redraw dips its ink
	var borrow_down := func() -> void:
		b.desk["borrow"] = clampi(int(DeskKit.ladder(SimBank.BORROW_STEPS, float(borrow), -1)),
			SimBank.MIN_DRAW, floor_amt)
		b.desk["flick"] = true
	var borrow_up := func() -> void:
		b.desk["borrow"] = clampi(int(DeskKit.ladder(SimBank.BORROW_STEPS, float(borrow), 1)),
			SimBank.MIN_DRAW, floor_amt)
		b.desk["flick"] = true
	var term_down := func() -> void:
		b.desk["term"] = int(DeskKit.ladder(terms, float(term), -1))
		b.desk["flick"] = true
	var term_up := func() -> void:
		b.desk["term"] = int(DeskKit.ladder(terms, float(term), 1))
		b.desk["flick"] = true
	_money_line(b, cx, cy, "borrow", "$" + b.fmt(borrow), borrow_down, borrow_up,
		borrow <= SimBank.MIN_DRAW, borrow >= headroom)
	# S2b — the borrow stepper is a landing pad ("borrow")
	b.mark_control("borrow", Rect2(cx - 8.0, cy - 6.0, 560.0, 46.0))
	_money_line(b, cx, cy + 40.0, "pay it back over", "%d weeks" % term, term_down, term_up,
		term <= int(terms[0]), term >= int(terms[terms.size() - 1]))
	b.label("at your rate  %.1f%%/wk — set by your standing" % (rate * 100.0),
		Vector2(cx, cy + 82.0), DeskKit.LAW, Color(DeskKit.INK, 0.6), 520.0)
	# the venture line, when a round opened that door (existing op)
	var vcap: int = mini(SimBank.venture_cap(state), headroom)
	if vcap >= SimBank.MIN_DRAW:
		var vrate := SimBank.venture_rate_wk(state)
		var vfire := func() -> void:
			SimBank.sign_note(state, "venture", vcap, int(SimBank.TERMS_VENTURE[0]))
		DeskKit.arm(b, "venture", "or venture debt: $%s at %.1f%% — take it ▸" % [b.fmt(vcap), vrate * 100.0],
			"interest-only · balloon in %d wks · %.2f%% warrants — sure?" % [int(SimBank.TERMS_VENTURE[0]), SimBank.WARRANT_PCT],
			Vector2(cx, cy + 108.0), vfire, 520.0, 17)
	# THE RECEIPT, compact: the three numbers and the pen
	var pay := SimBank.loan_payment_wk(borrow, rate, term)
	var all_in := pay * term
	var rx := cx + 620.0
	var rcpt_w := 420.0
	var inked: Array = []
	inked.append(b.label("THE RECEIPT — shorter term: smaller price, heavier Mondays",
		Vector2(rx, cy - 6.0), 15, Color(DeskKit.INK, 0.5), rcpt_w))
	inked.append_array(_rcpt_row(b, rx, cy + 16.0, rcpt_w, "every Monday",
		"$" + b.fmt(pay), DeskKit.INK))
	inked.append_array(_rcpt_row(b, rx, cy + 40.0, rcpt_w, "you will hand back, in all",
		"$" + b.fmt(all_in), DeskKit.INK))
	DeskKit.pen_rule(b, cy + 64.0, rx, rcpt_w, Color(DeskKit.INK, 0.8))
	DeskKit.pen_rule(b, cy + 68.0, rx, rcpt_w, Color(DeskKit.INK, 0.8))
	inked.append_array(_rcpt_row(b, rx, cy + 74.0, rcpt_w, "THE PRICE OF THE MONEY",
		"$" + b.fmt(maxi(all_in - borrow, 0)), Binder.PEN))
	# THE PEN FLICK (S4): a stepper press just rewrote the numbers — the ink
	# dips and settles, so the re-print is FELT, not inferred
	if bool(b.desk.get("flick", false)):
		b.desk.erase("flick")
		var tw: Tween = b.create_tween()
		tw.set_parallel(true)
		for l in inked:
			(l as Label).modulate.a = 0.25
			tw.tween_property(l, "modulate:a", 1.0, 0.18)
	var sign := DeskKit.word(b, "[ SIGN FOR IT ]", Vector2(rx + 100.0, cy + 104.0),
		Callable(), DeskKit.DETAIL, DeskKit.INK, 220.0)
	var fire_sign := func() -> void:
		SimBank.sign_note(state, "bank", borrow, term)
		b.desk.erase("borrow")
		b.refresh()
	sign.pressed.connect(func() -> void:
		DeskKit.sign_stroke(b, sign, fire_sign))
	return y + h + 4.0

## One receipt row; returns its labels so the pen flick can re-ink them.
static func _rcpt_row(b, x: float, y: float, w: float, label_text: String, val: String,
		col: Color) -> Array:
	var l: Label = b.label(label_text, Vector2(x, y), 17, Color(DeskKit.INK, 0.85), w - 120.0)
	var v: Label = b.label(val, Vector2(x, y), 18, col, w)
	v.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	return [l, v]

static func _dead_reason(garage: bool, locked: bool) -> String:
	if locked:
		return "the bank won't quote — clear the collectors first."
	if garage:
		return "no bank answers a garage — the shark (18%/wk) is the only desperate door."
	return "no revenue, no line — a bank lends against what customers already pay you."

static func _money_line(b, x: float, y: float, label_text: String, value: String,
		on_minus, on_plus, at_min: bool, at_max: bool) -> void:
	b.label(label_text, Vector2(x, y + 4.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.85), 200.0)
	b.label(value, Vector2(x + 210.0, y), DeskKit.ROW, DeskKit.INK, 210.0)
	DeskKit.adjust_pair(b, x + 440.0, y + 4.0, on_minus, on_plus, at_min, at_max)

## The folded bars — zones 3 and 4 swap which of them is open.
static func _zone3_bar(b, state: GameState, y: float, rate: float) -> float:
	var locked := SimBank.credit_locked(state)
	var garage := state.era_index() < 1
	var headroom := SimBank.borrow_headroom(state)
	var text := ""
	if garage or locked or headroom < SimBank.MIN_DRAW:
		text = "3 · NEW MONEY — " + _dead_reason(garage, locked).trim_suffix(".") + " ▸"
	else:
		var borrow := clampi(int(b.desk.get("borrow", 10_000)), SimBank.MIN_DRAW,
			maxi(headroom, SimBank.MIN_DRAW))
		var terms: Array = SimBank.term_options(state, "bank")
		var term := int(b.desk.get("term", int(terms[mini(1, terms.size() - 1)])))
		if not terms.has(term):
			term = int(terms[0])
		text = "3 · NEW MONEY — borrow $%s over %d weeks -> $%s every Monday ▸" % [
			b.fmt(borrow), term, b.fmt(SimBank.loan_payment_wk(borrow, rate, term))]
	return _bar(b, y, text, func() -> void: b.desk["zone4"] = false)

## Compact on purpose: this bar shares the lower page with the DO lane (S3,
## right-aligned at 762) — a short door never runs under the lane's buttons;
## the ladder's full story waits inside the opened zone.
static func _zone4_bar(b, _state: GameState, y: float) -> float:
	DeskKit.pen_rule(b, y + 2.0, SHEET_X, ZONE_W, Color(DeskKit.INK, 0.2))
	DeskKit.word(b, "4 · IF A MONDAY IS MISSED — the three stairs ▸",
		Vector2(SHEET_X + 8.0, y + 8.0), func() -> void: b.desk["zone4"] = true,
		19, Color(DeskKit.INK, 0.7), 330.0)
	return y + 42.0

static func _bar(b, y: float, text: String, on_press: Callable) -> float:
	DeskKit.pen_rule(b, y + 2.0, SHEET_X, ZONE_W, Color(DeskKit.INK, 0.2))
	DeskKit.word(b, text, Vector2(SHEET_X + 8.0, y + 8.0), on_press, 19,
		Color(DeskKit.INK, 0.7), ZONE_W - 16.0)
	return y + 42.0

## Zone 4 — the engine's own miss ladder (SimBank._miss), drawn as stairs.
static func _zone_stairs(b, state: GameState, y: float) -> float:
	var h := 182.0
	var z := DeskKit.zone(b, SHEET_X, y, ZONE_W, h, 4, "if a monday is missed",
		"— the ladder is written into every loan; read it before you need it")
	var cx := float(z.get("content_x", 0.0))
	var base := float(z.get("bottom", y + h)) - 12.0
	var sw := (ZONE_W - CARD_GAP * 2.0 - 36.0) / 3.0
	_stair(b, cx, base, sw, 56.0, "1st miss", "the balance grows — unpaid interest joins the debt", 0)
	_stair(b, cx + sw + 6.0, base, sw, 72.0, "2nd miss", "repriced +2%/wk — and the bank stops answering", 1)
	_stair(b, cx + (sw + 6.0) * 2.0, base, sw, 88.0, "3rd miss", "sold to the collectors — 18%/wk, the shark's price", 2)
	var _s := state
	return y + h + 4.0

## One stair: a filled step with an ink edge, climbing left to right. Drawn
## with plain rects so both engines cut the same shape from public parts.
static func _stair(b, x: float, base: float, w: float, h: float, head: String,
		line: String, i: int) -> void:
	var y := base - h
	var bg := ColorRect.new()
	bg.color = STAIR_COLS[i]
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	bg.position = Vector2(x, y)
	bg.set_deferred("size", Vector2(w, h))
	b.pane().add_child(bg)
	for edge in [[Vector2(x, y), Vector2(w, 2.6)], [Vector2(x, y + h - 2.6), Vector2(w, 2.6)],
			[Vector2(x, y), Vector2(2.6, h)], [Vector2(x + w - 2.6, y), Vector2(2.6, h)]]:
		var e := ColorRect.new()
		e.color = DeskKit.INK
		e.mouse_filter = Control.MOUSE_FILTER_IGNORE
		e.position = (edge as Array)[0]
		e.set_deferred("size", (edge as Array)[1])
		b.pane().add_child(e)
	var ink := Color.WHITE if i == 2 else DeskKit.INK
	b.label(head, Vector2(x + 10.0, y + 2.0), 18, ink, w - 20.0)
	b.label(line, Vector2(x + 10.0, y + 24.0), 13,
		Color(1, 1, 1, 0.9) if i == 2 else Color(DeskKit.INK, 0.6), w - 20.0)

## The cash-ahead strip: the forecast's own cells, before surprises.
static func _forecast_strip(b, state: GameState, y: float) -> void:
	var rows: Array = SimBank.forecast_cash(state, SimBank.FORECAST_WEEKS)
	# the strip yields to the DO lane's slot (S3) — in deep stacks it simply
	# waits for a shallower week rather than printing under the buttons
	if rows.is_empty() or y + 50.0 > minf(Y_RULES - 6.0, DeskKit.DO_LANE_Y - 8.0):
		return
	b.label("cash ahead, if nothing changes:", Vector2(SHEET_X, y + 12.0), DeskKit.LAW,
		Color(DeskKit.INK, 0.6), 240.0)
	var x := SHEET_X + 250.0
	for r in rows:
		var rd: Dictionary = r
		var cell := DeskKit.card_frame(b, x, y, 128.0, 48.0, "")
		var _c := cell
		b.label("wk %d" % int(rd.get("wk", 0)), Vector2(x + 10.0, y + 2.0), 14,
			Color(DeskKit.INK, 0.5), 108.0)
		var c := int(rd.get("cash", 0))
		var txt := ("−$%.1fk" % (absf(float(c)) / 1000.0)) if c < 0 else ("$%.1fk" % (float(c) / 1000.0))
		b.label(txt, Vector2(x + 10.0, y + 18.0), 19, Binder.PEN if c < 0 else DeskKit.INK, 108.0)
		x += 140.0
	b.label("before surprises", Vector2(x + 10.0, y + 14.0), DeskKit.LAW, Color(DeskKit.INK, 0.42), 200.0)

## One line at the page foot: the warning outranks the laws.
static func _foot(b, state: GameState) -> void:
	var warning := ""
	if SimBank.credit_locked(state):
		warning = "a note is in default: the collectors are calling and investors do check your credit"
	elif SimBank.debt_service_wk(state) > 0 and state.cash < 2 * SimBank.debt_service_wk(state):
		warning = "the repayment cliff: $%s a week is due and there is $%s in the bank" % [
			_fmt(SimBank.debt_service_wk(state)), _fmt(state.cash)]
	if warning != "":
		b.label(warning, Vector2(SHEET_X, Y_RULES), DeskKit.LAW, Binder.PEN, 1100.0)
	else:
		b.label("a loan is rented money — you pay for the time, not the amount · interest bills sold or not · repaying early is the only discount",
			Vector2(SHEET_X, Y_RULES), DeskKit.LAW, Color(DeskKit.INK, 0.5), 1100.0)

# ══════════════════════════════ THE BOOKS ════════════════════════════════════

## The full statement, restyled onto THE LEDGER SHEET (DECISIONS: BOOKS mode
## = the full statement on the ledger sheet). Three bands — money in, the
## operation, the bank & the state — every nonzero lane a row with its own
## teaching note; the crowd beyond the page's budget folds into one honest
## "smaller lines" row; NET double-ruled = the engine's own identity.
static func _draw_books(b, state: GameState) -> void:
	DeskKit.title(b, "the books — last week, line by line")
	var to_desk := func() -> void:
		b.desk["mode"] = ""
	DeskKit.back(b, "back to the meeting", to_desk, Vector2(880.0, 16.0))
	var pnl: Dictionary = state.get_meta("pnl", {})
	if pnl.is_empty():
		DeskKit.empty(b, Vector2(SHEET_X, 120.0),
			"no week has closed yet — the books open after the first LOCK IN.",
			"a P&L is a record of what happened, and nothing has.")
		return
	var sheet := DeskKit.ledger_sheet(b, SHEET_X, 64.0, ZONE_W, {
		"columns": [{"label": "the week's books", "w": 560.0},
			{"label": "$/wk", "w": 150.0, "align": "right"}, {"label": "note", "w": 330.0}],
		"amount": 1, "adjust": false, "unit": "all figures $/week",
	})
	var revenue := int(pnl.get("revenue", 0))
	DeskKit.ledger_section(b, sheet, "money in")
	DeskKit.ledger_row(b, sheet, ["%d customers paid" % state.traction, "$" + b.fmt(revenue),
		("learning ×%.2f — scale earns its margin" % float(pnl.get("learning", 1.0))) if float(pnl.get("learning", 1.0)) < 0.995 else ""], {})
	# ── the operation: every nonzero lane, oldest teaching first
	var op: Array = []
	_op(op, "serving the customers (cogs)", int(pnl.get("cogs", 0)), "")
	_op(op, "built in-house", int(pnl.get("production", 0)), "hardware pays at the bench")
	_op(op, "bought outside", int(pnl.get("subcontract", 0)), "")
	_op(op, "rent", int(pnl.get("rent", 0)), "")
	_op(op, "site rents", int(pnl.get("site_rent", 0)), "the other roofs")
	_op(op, "payroll", int(pnl.get("payroll", 0)), "")
	_op(op, "infra", int(pnl.get("infra", 0)), "")
	_op(op, "the catalog's tools", int(pnl.get("offer_fixed", 0)), "")
	_op(op, "machine upkeep + carrying", int(pnl.get("equip_upkeep", 0)) + int(pnl.get("carrying", 0)), "")
	_op(op, "marketing — the mix", int(pnl.get("marketing", 0)), "-> growth")
	_op(op, "sales · care · rnd · office", int(pnl.get("sales", 0)) + int(pnl.get("care", 0))
		+ int(pnl.get("rnd", 0)) + int(pnl.get("office", 0)), "-> spend")
	_op(op, "the unforeseen", int(pnl.get("incident", 0)), "nobody planned it")
	_op(op, "severance", int(pnl.get("severance", 0)), "always owed")
	_op(op, "recruiting + adverts", int(pnl.get("recruiting", 0)) + int(pnl.get("recruit_ads", 0)), "")
	_op(op, "relief valves", int(pnl.get("relief", 0)), "overflow served outside")
	# the reconciler: whatever the era's burn multiplier added beyond the
	# rows is printed, never hidden — the book must sum
	var rows_sum := 0
	for r in op:
		rows_sum += int((r as Array)[1])
	var burn := int(pnl.get("burn", 0))
	if burn != rows_sum:
		_op(op, "the world's overhead multiplier", burn - rows_sum, "the era taxes every line")
	_op(op, "standing costs", int(pnl.get("liabilities_wk", 0)), "they run out, slowly")
	DeskKit.ledger_section(b, sheet, "money out — the operation")
	if op.size() > 9:
		var rest := 0
		for j in range(8, op.size()):
			rest += int((op[j] as Array)[1])
		op = op.slice(0, 8)
		op.append(["the smaller lines, together", rest, "each one still on the receipts"])
	for r2 in op:
		var ra: Array = r2
		DeskKit.ledger_row(b, sheet, [String(ra[0]), "$" + b.fmt(int(ra[1])), String(ra[2])], {})
	DeskKit.ledger_section(b, sheet, "the bank & the state")
	if int(pnl.get("interest", 0)) != 0:
		DeskKit.ledger_row(b, sheet, ["interest — the cost of debt",
			"$" + b.fmt(int(pnl.get("interest", 0))), "outside burn, on purpose"], {})
	if int(pnl.get("tax", 0)) != 0:
		DeskKit.ledger_row(b, sheet, ["the taxman", "$" + b.fmt(int(pnl.get("tax", 0))),
			"20% of profit, after interest"], {})
	var out_total := burn + int(pnl.get("liabilities_wk", 0)) + int(pnl.get("interest", 0)) + int(pnl.get("tax", 0))
	DeskKit.ledger_subtotal(b, sheet, "subtotal — out", "$" + b.fmt(out_total))
	var net := int(pnl.get("net", 0))
	DeskKit.ledger_total(b, sheet, "net, the week", ("+$" if net >= 0 else "−$") + b.fmt(absi(net)),
		DeskKit.SAGE if net >= 0 else Binder.PEN)
	var rw := SimEngine.runway_weeks(state)
	DeskKit.ledger_memo(b, sheet, "cash $%s · runway at this net" % b.fmt(state.cash),
		("%d wks" % rw) if rw < 999 else "gaining", "")
	_second_memo(b, sheet, state)
	DeskKit.ledger_end(b, sheet)
	b.label("read it top to bottom: in, the operation, the bank and the state — interest and tax sit outside burn, the real P&L shape",
		Vector2(SHEET_X, Y_RULES), DeskKit.LAW, Color(DeskKit.INK, 0.5), 1100.0)

static func _op(op: Array, label_text: String, amount: int, note: String) -> void:
	if amount != 0:
		op.append([label_text, amount, note])

## The second memo slot, by teaching priority: principal (profit ≠ cash) ->
## NOL shelter -> the net-30 float -> break-even.
static func _second_memo(b, sheet: Dictionary, state: GameState) -> void:
	var principal := int(state.get_meta("bank_principal_wk", 0))
	if principal > 0:
		DeskKit.ledger_memo(b, sheet, "principal paid $%s" % b.fmt(principal), "",
			"a balance-sheet move, not a cost")
		return
	if state.tax_loss_carry > 0:
		DeskKit.ledger_memo(b, sheet, "losses carried forward", "$" + b.fmt(state.tax_loss_carry),
			"they shelter the next profits before the taxman sees them")
		return
	var owed := 0
	for r in state.receivables:
		owed += int((r as Dictionary).get("cash_wk", 0))
	if owed > 0:
		DeskKit.ledger_memo(b, sheet, "net-30 float", "$" + b.fmt(owed),
			"invoiced, not yet in the bank — profit is not cash")
		return
	var be := SimBank.break_even_customers(state)
	if be > 0:
		DeskKit.ledger_memo(b, sheet, "break-even %d customers" % be,
			"%d now" % state.traction, "each contributes $%.1f/wk" % SimBank.contribution_margin(state))

# ── shared reads ─────────────────────────────────────────────────────────────

## Every live note with its index: [{idx, note}] — the legacy shark first.
static func _live_notes(state: GameState) -> Array:
	var out: Array = []
	if state.loan_principal > 0:
		out.append({"idx": -1, "note": {"kind": "shark", "principal": state.loan_principal,
			"balance": state.loan_principal, "rate_wk": SimBank.SHARK_RATE, "pay_wk": 0,
			"term_wk": 0, "taken_week": state.week, "missed": 0}})
	for i in state.loans.size():
		var note: Dictionary = state.loans[i]
		if int(note.get("balance", 0)) > 0:
			out.append({"idx": i, "note": note})
	return out

## The first live amortizing bank note — the refinance candidate. -1 = none.
static func _refi_note(state: GameState) -> int:
	for i in state.loans.size():
		var note: Dictionary = state.loans[i]
		if String(note.get("kind", "")) == "bank" and int(note.get("balance", 0)) > 0:
			return i
	return -1

## What actually leaves on a Monday: level payments + coupons + shark fees.
static func _monday_out(state: GameState) -> int:
	var total := SimBank.debt_service_wk(state)
	if state.loan_principal > 0:
		total += int(ceil(float(state.loan_principal) * SimBank.SHARK_RATE))
	for l in state.loans:
		var ld: Dictionary = l
		if String(ld.get("kind", "")) == "shark" and int(ld.get("balance", 0)) > 0:
			total += int(ceil(float(ld.get("balance", 0)) * float(ld.get("rate_wk", 0.0))))
	return total

static func _hero_sentence(state: GameState) -> String:
	var shrinking := 0
	var frozen := 0
	for n in _live_notes(state):
		if String(((n as Dictionary).get("note", {}) as Dictionary).get("kind", "")) == "bank":
			shrinking += 1
		else:
			frozen += 1
	if shrinking + frozen == 0:
		return "no debt on the books — the credit line below is what the bank would answer."
	if shrinking > 0 and frozen > 0:
		return "some of this shrinks as you pay it; some never will. zone 2 shows which."
	if frozen > 0:
		return "interest-only money: the Mondays buy time, never the debt itself."
	return "amortizing money: every Monday buys a little more of the debt back."

static func _deadline_text(state: GameState) -> String:
	for l in state.loans:
		var ld: Dictionary = l
		if int(ld.get("balance", 0)) <= 0:
			continue
		if String(ld.get("kind", "")) == "venture":
			var to_balloon := int(ld.get("taken_week", 0)) + int(ld.get("term_wk", 0)) - state.week
			if to_balloon <= 3:
				return "balloon due in %d wk%s" % [maxi(to_balloon, 0), "" if to_balloon == 1 else "s"]
		if String(ld.get("kind", "")) == "shark" or int(ld.get("missed", 0)) >= 1:
			return "the shark feeds first"
	if state.loan_principal > 0:
		return "the shark feeds first"
	return ""

static func handle(_b, _id: String) -> void:
	pass   # every control on this desk carries its own closure

static func _fmt(n: int) -> String:
	var s := str(absi(n))
	var out := ""
	while s.length() > 3:
		out = "," + s.substr(s.length() - 3) + out
		s = s.substr(0, s.length() - 3)
	return ("-" if n < 0 else "") + s + out
