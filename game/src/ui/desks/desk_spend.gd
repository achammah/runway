class_name DeskSpend
extends RefCounted
## DESK — COSTS · "spend" = THE ORG LEDGER on the ledger sheet (DAG2 W2
## L-MONEY; DECISIONS: spend = C, the generated-book law + the stepper law;
## pixels: docs/design/mockups/06 + 07).
##
## THE BOOK IS THE LEVER: sections are the four engine buckets, rows are
## state.spend_book lines, and each bucket's subtotal IS state.budgets[bucket]
## (SimSpendBook keeps the two equal — the write-back law, twin-tested).
##
## THE SUGGESTION PATH (coordinator ruling): the generated `amt` renders as a
## dim suggestion; levers start at 0; ADOPT copies suggestion -> live spend
## through the receipt path (arm carries the price). One "adopt the whole
## book" arm sits at the sheet top. The keyless bare book has no suggestions.
##
## THE MUTATION LAW: adding a line is free (it bills only when raised);
## stopping is instant unless the book marked the line "contract" — then the
## notice bills through and the row renders its countdown.
##
## `binder.gd` dispatches the tab body here and passes ITSELF; everything
## routes through closures, so `handle` stays empty.

const QUESTION := "where does the money go?"

const SHEET_X := 10.0
const SHEET_W := 1120.0
const Y_SHEET := 108.0
## The money desks foot their own lines low on the 880 pane (the kit's fixed
## 700 slots belong to the old 760 pages).
const Y_FOOT := 806.0
const Y_RULES := 840.0
## Books longer than this fold their healthy lines behind the bucket subtotal
## (the collapse law — attention rows never fold).
const FOLD_AT := 6

## LONG-TEXT LAW: a book row is one line tall — a generated buys line (up to
## 60 chars) is measured and trimmed to its column, never wrapped over the
## rule into the row below.
static func _fit(b, s: String, w: float, size: int = 21) -> String:
	if b.font().get_string_size(s, HORIZONTAL_ALIGNMENT_LEFT, -1, size).x <= w:
		return s
	var t := s
	while t.length() > 1 and b.font().get_string_size(t + "…",
			HORIZONTAL_ALIGNMENT_LEFT, -1, size).x > w:
		t = t.substr(0, t.length() - 1)
	return t.strip_edges() + "…"

## The group overview's card reads this: the page's hero verbatim.
static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	var total := 0
	for b in SimSpendBook.BUCKETS:
		total += int(s.budgets.get(b, 0))
	return {"big": "$%s/wk" % _fmt(total), "line": "the org book feeds four levers"}

static func draw(b) -> void:
	var state: GameState = b.state
	var swept := SimSpendBook.sweep_lapsed(state, state.week)
	if swept > 0:
		state.log_action("the notice ran out on %d stopped spend line%s — struck from the book"
			% [swept, "" if swept == 1 else "s"])
	SimSpendBook.reconcile(state)
	var total := SimSpendBook.book_live(state)
	var suggested := SimSpendBook.book_suggested(state)

	# ── the hero: the book's total, which the double-ruled TOTAL must equal
	var big: String = "$" + b.fmt(total)
	b.label(big, Vector2(SHEET_X, 6.0), DeskKit.HERO, DeskKit.INK, 460.0)
	var bw: float = b.font().get_string_size(big, HORIZONTAL_ALIGNMENT_LEFT, -1, DeskKit.HERO).x
	b.label("a week feeds the org", Vector2(SHEET_X + bw + 16.0, 22.0), DeskKit.ROW,
		Color(DeskKit.INK, 0.7), 420.0)
	b.label("your book, written for YOUR business — every line sums into one of four engine buckets.",
		Vector2(SHEET_X, 62.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 760.0)
	# RED MEANS ACT, AND THE PAGE NAMES THE ASK (owner: "exclamation mark but
	# unclear what is asked") — the kit's ask strip, born on this desk (S2a).
	DeskKit.ask_strip(b, "spend", SHEET_X, 86.0, 1000.0, "adopt the book or fund a line")
	# the whole-book adopt arm (the ruling): one press-pair prices the book
	if suggested > 0 and suggested != total:
		var fire_book := func() -> void:
			SimSpendBook.adopt_book(state)
		DeskKit.arm(b, "adopt_book", "adopt the suggested book — $%s/wk" % b.fmt(suggested),
			"start billing $%s/wk — sure?" % b.fmt(suggested), Vector2(790.0, 56.0),
			fire_book, 340.0, DeskKit.DETAIL)

	# ── the sheet
	var sheet := DeskKit.ledger_sheet(b, SHEET_X, Y_SHEET, SHEET_W, {
		"columns": [{"label": "line", "w": 280.0}, {"label": "buys", "w": 230.0},
			{"label": "$/wk", "w": 120.0, "align": "right"}, {"label": "effect", "w": 290.0}],
		"amount": 2, "adjust": true, "unit": "all figures $/week",
	})
	var effect_x := float((sheet["cols"][3] as Dictionary).get("x", 0.0))
	var fold_all: bool = state.spend_book.size() > FOLD_AT
	var open_b := String(b.desk.get("open_b", ""))
	for bucket in SimSpendBook.BUCKETS:
		var idxs := SimSpendBook.lines_of(state, bucket)
		if idxs.is_empty():
			continue
		DeskKit.ledger_section(b, sheet, String(SimSpendBook.BUCKET_WORDS.get(bucket, bucket)))
		var folded := 0
		var folded_live := 0
		var folded_sugg := 0
		for ii in idxs:
			var i := int(ii)
			var line: Dictionary = state.spend_book[i]
			var live := SimSpendBook.live_of(line)
			var sugg := int(line.get("amt", 0))
			var stopping := SimSpendBook.is_stopping(line)
			var pending := sugg > 0 and live != sugg and not stopping
			# the collapse law: on a long book only the pressed-open bucket
			# keeps its crowd; a STOPPING line (a live countdown) never folds,
			# and the whole-book adopt arm covers folded suggestions
			if fold_all and open_b != bucket and not stopping:
				folded += 1
				folded_live += live
				folded_sugg += sugg if pending else 0
				continue
			var row_y := float(sheet["cursor"])
			var idx := i
			var cfg := {}
			if stopping:
				cfg = {"dim": true}
			else:
				var press_minus := func() -> void:
					SimSpendBook.adjust_live(state, idx, -1)
				var press_plus := func() -> void:
					SimSpendBook.adjust_live(state, idx, 1)
				cfg = {"on_minus": press_minus, "on_plus": press_plus,
					"at_min": live <= 0, "at_max": SimSpendBook.at_cap(state, idx)}
			DeskKit.ledger_row(b, sheet, [String(line.get("name", "")),
				_fit(b, String(line.get("buys", "")), 218.0), "$" + b.fmt(live), ""], cfg)
			# the EFFECT cell carries the row's ONE control (mutation law:
			# receipt-priced arm, two taps, Esc disarms)
			if stopping:
				b.label("stops in %d wks — the contract bills through"
					% SimSpendBook.notice_left(line, state.week),
					Vector2(effect_x, row_y + 8.0), 18, Binder.PEN, 286.0)
			elif pending:
				var fire_adopt := func() -> void:
					SimSpendBook.adopt_line(state, idx)
				DeskKit.arm(b, "adopt_%d" % i, "suggested $%s — adopt" % b.fmt(sugg),
					"bills $%s/wk — sure?" % b.fmt(sugg), Vector2(effect_x, row_y + 4.0),
					fire_adopt, 186.0, 19)
				if live == 0:
					var fire_strike := func() -> void:
						SimSpendBook.stop_line(state, idx, state.week)
					DeskKit.arm(b, "strike_%d" % i, "strike", "sure?",
						Vector2(effect_x + 192.0, row_y + 4.0), fire_strike, 90.0, 19)
			else:
				var armed_cap := ""
				if int(line.get("contract_notice", 0)) > 0:
					armed_cap = "bills %d more wks — sure?" % int(line.get("contract_notice", 0))
				else:
					armed_cap = "stops $%s/wk now — sure?" % b.fmt(live)
				var fire_stop := func() -> void:
					SimSpendBook.stop_line(state, idx, state.week)
				DeskKit.arm(b, "stop_%d" % i, "stop the line", armed_cap,
					Vector2(effect_x, row_y + 4.0), fire_stop, 200.0, 19)
		if folded > 0:
			var open_bucket := bucket
			var open_press := func() -> void:
				b.desk["open_b"] = open_bucket
			DeskKit.ledger_row(b, sheet, ["the other %d lines" % folded, "press to open",
				"$" + b.fmt(folded_live),
				("$%s suggested" % b.fmt(folded_sugg)) if folded_sugg > 0 else ""],
				{"dim": true, "on_press": open_press})
		DeskKit.ledger_subtotal(b, sheet, "subtotal — %s" % bucket.replace("rnd", "building").replace("sales", "closing").replace("care", "retention").replace("office", "people"),
			"$" + b.fmt(SimSpendBook.bucket_live(state, bucket)),
			_effect_line(state, bucket))
	DeskKit.ledger_total(b, sheet, "total org spend", "$" + b.fmt(total))
	if suggested > 0 and suggested != total:
		DeskKit.ledger_memo(b, sheet, "the book suggests", "$" + b.fmt(suggested),
			"adopt line by line, or the whole book above")
	var y_end := DeskKit.ledger_end(b, sheet)

	# ── the add-a-line door (ink is free; it bills only when raised) —
	# drawn only when the sheet left it room; a full page keeps its book
	if y_end + 44.0 <= Y_FOOT - 8.0:
		_add_door(b, state, y_end + 2.0)

	# ── the teaching foot, on the money desks' own low slots
	b.label("the subtotals ARE the engine's levers — closing, retention, building, people",
		Vector2(SHEET_X, Y_FOOT), DeskKit.LAW, Binder.BLUE, 1100.0)
	b.label("ink is free · brick is priced · a contract line bills its notice through · the era caps each bucket at $%s/wk" % b.fmt(SimEngine.era_spend_cap(state.era)),
		Vector2(SHEET_X, Y_RULES), DeskKit.LAW, Color(DeskKit.INK, 0.5), 1100.0)

## The bucket's live engine effect, exactly as the tick computes it (§8/§9):
## sales buys closing capacity, care eases churn through care_eff, rnd ships
## quality and melts debt, office lifts morale.
static func _effect_line(state: GameState, bucket: String) -> String:
	var v := SimSpendBook.bucket_live(state, bucket)
	match bucket:
		"sales":
			return "+%.1f closers of capacity" % (float(v) / 600.0) if v > 0 else "founder sells alone"
		"care":
			var cut := 30.0 * (1.0 - exp(-SimLabor.care_eff(state, float(v)) / 1500.0))
			if v > 0:
				return "churn −%d%%" % int(round(cut))
			return ("the support desk alone: churn −%d%%" % int(round(cut))) if cut >= 1.0 else "nobody picks up"
		"rnd":
			return "+%.1f product/wk · debt pays down" % (float(v) / 1200.0) if v > 0 else "no extra shipping"
		"office":
			var mg := 3.0 * (1.0 - exp(-float(v) / 800.0))
			return "+%.1f morale/wk" % mg if v > 0 else "instant coffee, cold room"
	return ""

## The door: press opens the bucket picker; picking stages a receipt; the ADD
## arm books it. Esc abandons (the binder pops `mode` first).
static func _add_door(b, state: GameState, y: float) -> void:
	var full: bool = state.spend_book.size() >= SimSpendBook.BOOK_CAP
	if String(b.desk.get("mode", "")) != "add":
		if full:
			b.label("the book is full — stop a line before adding one",
				Vector2(SHEET_X, y + 6.0), DeskKit.LAW, Color(DeskKit.INK, 0.4), 500.0)
			return
		var open_door := func() -> void:
			b.desk["mode"] = "add"
			b.desk.erase("staged")
		DeskKit.word(b, "+ add a line", Vector2(SHEET_X, y), open_door,
			DeskKit.DETAIL, Color(DeskKit.INK, 0.7), 220.0)
		return
	var staged := String(b.desk.get("staged", ""))
	if staged == "":
		b.label("into which bucket?", Vector2(SHEET_X, y + 4.0), DeskKit.DETAIL,
			Color(DeskKit.INK, 0.7), 220.0)
		var x := SHEET_X + 220.0
		for bucket in SimSpendBook.BUCKETS:
			var pick := bucket
			var pick_press := func() -> void:
				b.desk["staged"] = pick
			DeskKit.word(b, String(SimSpendBook.BUCKET_WORDS.get(bucket, bucket)),
				Vector2(x, y + 4.0), pick_press, DeskKit.DETAIL, DeskKit.INK, 200.0)
			x += 208.0
		return
	b.label("a new line in %s — free to add, $0/wk until you raise it (Esc backs out)"
		% String(SimSpendBook.BUCKET_WORDS.get(staged, staged)),
		Vector2(SHEET_X, y + 4.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.7), 760.0)
	var fire_add := func() -> void:
		SimSpendBook.add_line(state, staged)
		b.desk["mode"] = ""
		b.desk.erase("staged")
	DeskKit.arm(b, "add_line", "ADD THE LINE", "write it into the book — sure?",
		Vector2(SHEET_X + 780.0, y + 2.0), fire_add, 300.0, DeskKit.DETAIL)

static func handle(_b, _id: String) -> void:
	pass   # every control on this sheet carries its own closure

static func _fmt(n: int) -> String:
	var s := str(absi(n))
	var out := ""
	while s.length() > 3:
		out = "," + s.substr(s.length() - 3) + out
		s = s.substr(0, s.length() - 3)
	return ("-" if n < 0 else "") + s + out
