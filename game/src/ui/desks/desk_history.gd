class_name DeskHistory
extends RefCounted
## DESK — THE LOG · "history", the run's own ledger. W2 lane: L-COMPANY.
## THE QUESTION THIS DESK ANSWERS: "how did we get here?"
## Spec: docs/design/12-binder-rework-2.md § history.
##
##   SPARKLINES  cash and customers, side by side, above the book
##   THE BOOK    a ledger-sheet row per week: wk · cash · net · customers ·
##               the headline · receipts -> (opens that week's receipts).
##               Older eras collapse to their net subtotal (the collapse
##               ladder — the recent weeks are the money-nearest rows and
##               never hide); the TOTAL is double-ruled, the run whole.
##   FILINGS     answered momentary tabs (buyout offers come and gone) file
##               here as flagged memo rows.
##
## Sources (all durable, all saved): metric_history rows {wk, cash, net,
## customers…}, run_history rows {wk, said, verdict, roll, fx[]} — the
## receipts — and the action log's era stamps (MOVED UP/DOWN) for sections.

const QUESTION := "how did we get here?"

## The open era shows this many recent weeks face-up before folding.
const FACE_UP := 7
const FACE_UP_ALL := 14

static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	var n := s.metric_history.size()
	if n == 0:
		return {"big": "a blank book", "line": "the first week writes the first row"}
	return {"big": "%d weeks" % n, "line": "the run's own ledger — receipts behind each row"}

## metric_history rows carry "wk" from the engine (older fixtures wrote
## "week"); read both so no book loses its early pages.
static func _wk(row: Dictionary) -> int:
	return int(row.get("wk", row.get("week", 0)))

static func _net(row: Dictionary) -> int:
	if row.has("net"):
		return int(row.get("net", 0))
	return int(row.get("revenue", 0)) - int(row.get("burn", 0))

## The week's headline: the durable title when the run_history row carries
## one, else the event title the action log remembers ("event 'X' — …"),
## else what the founder wrote (run_history.said), else a quiet week.
static func _headline(s: GameState, wk: int) -> String:
	for r0 in s.run_history:
		var rd0: Dictionary = r0
		if int(rd0.get("wk", -1)) == wk and String(rd0.get("title", "")) != "":
			return String(rd0.get("title", ""))
	for h in s.history:
		var hd: Dictionary = h
		if int(hd.get("week", -1)) != wk:
			continue
		var e := String(hd.get("entry", ""))
		var a := e.find("event '")
		if a >= 0:
			var z := e.find("'", a + 7)
			if z > a:
				return e.substr(a + 7, z - a - 7)
	for r in s.run_history:
		var rd: Dictionary = r
		if int(rd.get("wk", -1)) == wk:
			var said := String(rd.get("said", ""))
			if said != "":
				return said
	return "a quiet week"

static func _week_receipts(s: GameState, wk: int) -> Dictionary:
	for r in s.run_history:
		var rd: Dictionary = r
		if int(rd.get("wk", -1)) == wk:
			return rd
	return {}

## Era sections rebuilt from the action log's MOVED stamps: an array of
## {era, from_wk} oldest first, always starting at wk 1.
static func _era_spans(s: GameState) -> Array:
	var moves: Array = []
	for h in s.history:
		var hd: Dictionary = h
		var e := String(hd.get("entry", ""))
		if e.begins_with("MOVED UP:") or e.begins_with("MOVED DOWN:"):
			var arrow := e.find("-> ")
			if arrow >= 0:
				var tail := e.substr(arrow + 2)
				var sp := tail.find(" ")
				moves.append({"era": tail.substr(0, sp) if sp > 0 else tail.strip_edges(),
					"from_wk": int(hd.get("week", 0))})
	var spans: Array = [{"era": (String(moves[0].get("era", s.era)) if false else "the early road")
		if not moves.is_empty() else s.era, "from_wk": 1}]
	for m in moves:
		spans.append(m)
	return spans

static func draw(b) -> void:
	var s: GameState = b.state
	if String(b.desk.get("mode", "")) == "receipts":
		_draw_receipts(b, s, int(b.desk.get("wk", 0)))
		return
	var rows := s.metric_history
	var y := DeskKit.hero_band(b, "%d weeks on the books" % rows.size(),
		"the run's own ledger — a row per week, the receipts behind each",
		DeskKit.INK)
	if rows.is_empty():
		DeskKit.empty(b, Vector2(DeskKit.X_ID, y),
			"the book is blank — the first LOCK IN writes the first row.",
			"play the week; the ledger remembers everything after that.")
		return

	# SPARKLINES — the shape of the run before the rows
	b.label("cash", Vector2(DeskKit.X_ID, y), 20, Color(DeskKit.INK, 0.6), 200.0)
	b.spark(b.series("cash"), Vector2(DeskKit.X_ID, y + 26.0), Vector2(540, 84), DeskKit.BLUE)
	b.label("customers", Vector2(DeskKit.X_ID + 580.0, y), 20, Color(DeskKit.INK, 0.6), 200.0)
	b.spark(b.series("customers"), Vector2(DeskKit.X_ID + 580.0, y + 26.0),
		Vector2(540, 84), DeskKit.SAGE)
	y += 124.0

	# THE BOOK — the ledger sheet, eras folded, the recent weeks face-up
	var all_open := bool(b.desk.get("all", false))
	var face := FACE_UP_ALL if all_open else FACE_UP
	var sheet := DeskKit.ledger_sheet(b, DeskKit.X_ID, y, 1120.0, {
		"columns": [
			{"label": "wk", "w": 64.0},
			{"label": "cash", "w": 150.0, "align": "right"},
			{"label": "net", "w": 132.0, "align": "right"},
			{"label": "customers", "w": 140.0, "align": "right"},
			{"label": "the headline", "w": 400.0},
			{"label": "", "w": 100.0}],
		"amount": 2, "adjust": false, "unit": "cash & net in $, at week's end"})
	var older := rows.size() - face
	if older > 0:
		var sub_net := 0
		for i in older:
			sub_net += _net(rows[i])
		DeskKit.ledger_section(b, sheet, "the road so far — wk %d–%d"
			% [_wk(rows[0]), _wk(rows[older - 1])])
		DeskKit.ledger_subtotal(b, sheet, "subtotal — %d folded weeks" % older,
			"$%s" % b.fmt(sub_net), "open the whole book below")
	var total_net := 0
	for r in rows:
		total_net += _net(r)
	for i2 in range(maxi(older, 0), rows.size()):
		var row: Dictionary = rows[i2]
		var wk := _wk(row)
		var net := _net(row)
		DeskKit.ledger_row(b, sheet, [
			str(wk), "$%s" % b.fmt(int(row.get("cash", 0))),
			"%s$%s" % ["+" if net >= 0 else "−", b.fmt(absi(net))],
			b.fmt(int(row.get("customers", 0))),
			_headline(s, wk).left(44), "receipts ->"],
			{"col": DeskKit.SAGE if net >= 0 else DeskKit.PEN,
				"on_press": _open_receipts(b, wk)})
	DeskKit.ledger_total(b, sheet, "the run so far", "%s$%s"
		% ["+" if total_net >= 0 else "−", b.fmt(absi(total_net))],
		DeskKit.SAGE if total_net >= 0 else DeskKit.PEN)
	# FILINGS — answered momentary tabs file here as flagged rows
	if s.exit_value > 0:
		DeskKit.ledger_memo(b, sheet, "★ filed: the company was sold",
			"$%s" % b.fmt(s.exit_value), "the buyout was accepted")
	elif s.mna.is_empty() and s.mna_last_week > 0:
		DeskKit.ledger_memo(b, sheet, "★ filed: a buyout offer came and went",
			"", "around wk %d — answered or expired" % s.mna_last_week)
	y = DeskKit.ledger_end(b, sheet)
	if older > 0 and not all_open and y <= 800.0:
		DeskKit.fold_row(b, DeskKit.X_ID, y, older, "weeks", func() -> void:
			b.desk["all"] = true)
	elif all_open and rows.size() > FACE_UP_ALL:
		b.label("+%d earlier weeks stay folded in the subtotal"
			% (rows.size() - FACE_UP_ALL), Vector2(DeskKit.X_ID, y + 4.0), 17,
			Color(DeskKit.INK, 0.5), 500.0)

	var first_cash := int((rows[0] as Dictionary).get("cash", 0))
	DeskKit.footer(b, {
		"computed": "cash: $%s at wk %d -> $%s now · %s today%s"
			% [b.fmt(first_cash), _wk(rows[0]), b.fmt(s.cash), s.era_display_name(),
			(" · %d pivots on the record" % s.pivots) if s.pivots > 0 else ""],
		"rules": "a row per week: what the week earned, what it cost, and the receipts "
			+ "behind it · the total must square with the bank",
		"y": 820.0, "rules_y": 852.0})

## The press that opens a week's receipts, bound per-row.
static func _open_receipts(b, wk: int) -> Callable:
	return func() -> void:
		b.desk["mode"] = "receipts"
		b.desk["wk"] = wk

## THE RECEIPTS PAGE — one week, in full: the move, the verdict, the die,
## and every effect the DM's ops booked.
static func _draw_receipts(b, s: GameState, wk: int) -> void:
	DeskKit.back(b, "← the book", func() -> void:
		b.desk.erase("mode")
		b.desk.erase("wk"))
	var y := 64.0
	y = DeskKit.hero_band(b, "week %d — the receipts" % wk,
		_headline(s, wk), DeskKit.INK, y)
	var rd := _week_receipts(s, wk)
	if rd.is_empty():
		DeskKit.empty(b, Vector2(DeskKit.X_ID, y),
			"no receipts survive for this week.",
			"the DM's memory keeps the recent past verbatim and compresses the rest.")
	else:
		var said := String(rd.get("said", ""))
		if said != "":
			b.label("the move: \"%s\"" % said, Vector2(DeskKit.X_ID, y), DeskKit.STATUS,
				Color(DeskKit.INK, 0.85), 1100.0)
			y += maxf(b.wrap_h("the move: \"%s\"" % said, DeskKit.STATUS, 1100.0), 32.0) + 6.0
		var verdict := String(rd.get("verdict", ""))
		var roll := String(rd.get("roll", ""))
		if verdict != "" or roll != "":
			var vr := verdict if roll == "" else (roll if verdict == "" else verdict + " · " + roll)
			b.label(vr, Vector2(DeskKit.X_ID, y), DeskKit.DETAIL, DeskKit.BLUE, 1100.0)
			y += 34.0
		y = DeskKit.pen_rule(b, y + 4.0)
		for fx in rd.get("fx", []):
			b.label("· " + String(fx), Vector2(DeskKit.X_ID, y), DeskKit.DETAIL,
				Color(DeskKit.INK, 0.75), 1100.0)
			y += maxf(b.wrap_h("· " + String(fx), DeskKit.DETAIL, 1100.0), 28.0) + 2.0
	# the week's own numbers close the page
	for m in s.metric_history:
		var row: Dictionary = m
		if _wk(row) == wk:
			var net := _net(row)
			DeskKit.ticket(b, DeskKit.X_ID, y + 8.0, 560.0, {
				"title": "the week, in numbers",
				"lines": [
					{"label": "cash at week's end", "value": "$%s" % b.fmt(int(row.get("cash", 0)))},
					{"label": "the week's net", "value": "%s$%s"
						% ["+" if net >= 0 else "−", b.fmt(absi(net))],
						"col": DeskKit.SAGE if net >= 0 else DeskKit.PEN},
					{"label": "customers", "value": b.fmt(int(row.get("customers", 0)))},
				]})
			break

static func handle(b, id: String) -> void:
	if id.begins_with("wk:"):
		b.desk["mode"] = "receipts"
		b.desk["wk"] = int(id.substr(3))
	elif id == "all":
		b.desk["all"] = true
	elif id == "back":
		b.desk.erase("mode")
		b.desk.erase("wk")
