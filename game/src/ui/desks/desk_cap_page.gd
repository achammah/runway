class_name DeskCapPage
extends RefCounted
## DESK — THE COMPANY · "cap table" (the ownership STATE). W2 lane: L-OWN,
## reworked to the locked pick (DECISIONS: THE OWNERSHIP CLUSTER §1, pixel
## source docs/design/mockups/18-ownership-cluster.html page 1).
##
## THE PAGE: hero (your % + paper worth + "paper, not cash") -> THE SLICES
## (ledger sheet, double-ruled 100%) -> THE DILUTION STORY (shrinking bars per
## ownership event) beside IF SOLD TODAY (the waterfall, reused pure from
## SimOwnership) -> the pool/cliff/covenant costline. The old wheel desk
## (desk_cap.gd) is no longer embedded — it stays in tree as a retirement
## candidate for the coordinator.
##
## THE DILUTION STORY'S MATH: the timeline is reconstructed BACKWARD from the
## current slice — the newest priced instrument's pct is un-scaled through
## keep_inv × keep_pool per round (the same uniform scaling the executor
## applied), so each step's number is honest and the notes carry the lessons
## verbatim. At most five bars; the story never invents history it cannot
## derive.
##
## MUTATION LAW: the one write on this desk is EXPAND THE POOL — a receipt
## first (per-holder dilution preview), then the two-tap arm; Esc abandons.
## Rounds, offers and events move the table everywhere else — never by hand.

const QUESTION := "who owns what and what's the company worth?"

const POOL_STEP := 2.0   ## the expansion door's one honest increment

## The group overview's card reads this: the page's hero, verbatim.
static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	# ONE BASIS with zone 3 (W4 package): the paper worth is the waterfall's
	# own answer at today's valuation — the two numbers can never disagree.
	var wf := SimOwnership.waterfall(s, SimEngine.valuation(s))
	var paper := int(wf.get("your_take", 0))
	return {"big": "you own %.0f%%" % s.founder_pct,
		"line": "≈ $%s on paper — paper, not cash" % SimOwnership.money_short(paper).lstrip("$")}

static func draw(b) -> void:
	var state: GameState = b.state
	if String(b.desk.get("mode", "")) == "pool":
		_draw_pool_page(b, state)
		return
	var val := SimEngine.valuation(state)
	var paper := int(float(val) * state.founder_pct / 100.0)
	# ── the hero answers the question in one second
	var y := DeskKit.hero_band(b,
		"you own %.0f%% · ≈ %s on paper" % [state.founder_pct, SimOwnership.money_short(paper)],
		"paper, not cash — it becomes money only at an exit, after everyone ahead of you.")
	# the company's price + the room, quiet right block over the band
	var pr: Label = b.label("the company priced at $%s" % b.fmt(val),
		Vector2(760.0, 10.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.75), 360.0)
	pr.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	var bl: Label = b.label(_board_line(state), Vector2(760.0, 40.0), 18,
		Color(DeskKit.INK, 0.5), 360.0)
	bl.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT

	# ── zone 1 · THE SLICES — the book of who owns what
	var rows := _slice_rows(state)
	var memo_n := 1 if _stack_count(state) > 0 else 0
	var z1_h := 78.0 + DeskKit.LG_HEAD_H + float(rows.size()) * DeskKit.LG_ROW_H \
		+ float(memo_n) * DeskKit.LG_ROW_H + DeskKit.LG_TOT_H + 22.0
	var z1 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, z1_h, 1, "the slices",
		"every holder, every class, every preference — the book of who owns what")
	if not state.esop.is_empty():
		DeskKit.word(b, "expand the pool +%.0f%%" % POOL_STEP,
			Vector2(z1.content_x + 880.0, float(z1.y) + 8.0), func() -> void:
				b.desk["mode"] = "pool", DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 220.0)
	var sheet := DeskKit.ledger_sheet(b, z1.content_x - 4.0, z1.cursor, 1088.0, {
		"columns": [{"label": "holder", "w": 300.0}, {"label": "instrument", "w": 190.0},
			{"label": "put in", "w": 150.0, "align": "right"},
			{"label": "owns", "w": 130.0, "align": "right"},
			{"label": "preferences", "w": 254.0, "align": "right"}],
		"amount": 3, "unit": ""})
	for r in rows:
		var rd: Dictionary = r
		DeskKit.ledger_row(b, sheet, rd.get("cells", []), rd.get("cfg", {}))
	if memo_n > 0:
		DeskKit.ledger_memo(b, sheet, "%d SAFE/note(s) waiting to convert" % _stack_count(state), "",
			"≈%.1f%% more if it converts" % SimOwnership.stack_dilution_at(state, float(val)))
	DeskKit.ledger_total(b, sheet, "the whole pie", "100%", DeskKit.INK)
	DeskKit.ledger_end(b, sheet)
	y = z1.bottom + 12.0

	# ── zone 2 · THE DILUTION STORY beside zone 3 · IF SOLD TODAY
	var z2 := DeskKit.zone(b, DeskKit.X_ID, y, 646.0, 268.0, 2, "the dilution story",
		"your slice shrinks at events — and can be worth more every time it does")
	var steps := _dilution_steps(state, val)
	if steps.size() >= 2:
		DeskKit.dilution_bar(b, z2.content_x, z2.cursor + 2.0, 610.0, steps)
	else:
		# no dilution EVENTS on the book yet — say what is true today
		DeskKit.empty(b, Vector2(z2.content_x, z2.cursor + 8.0),
			"no rounds on the book yet — you hold %.0f%% today." % state.founder_pct,
			"rounds, pools and conversions will draw themselves here")
	var z3 := DeskKit.zone(b, DeskKit.X_ID + 660.0, y, 470.0, 268.0, 3, "if sold today",
		"the waterfall — who gets paid, in order")
	var wf := SimOwnership.waterfall(state, val)
	DeskKit.money_row(b, z3, "the bank — debts first", "$%s" % b.fmt(int(wf.get("debts", 0))))
	DeskKit.money_row(b, z3, "preferences next", "$%s" % b.fmt(int(wf.get("prefs_paid", 0))))
	DeskKit.money_row(b, z3, "then the split — you'd see", "≈$%s" % b.fmt(int(wf.get("your_take", 0))), DeskKit.SAGE)
	b.label("below ≈$%s the preferences eat everything — the waterfall is the whole lesson"
		% b.fmt(int(wf.get("breakeven", 0))), Vector2(z3.content_x, z3.cursor + 6.0),
		18, Color(DeskKit.INK, 0.55), 430.0)
	y += 268.0 + 10.0

	DeskKit.footer(b, {"computed": _costline(state), "rules":
		"the cap table moves only through rounds, offers and events — never by hand · rounds happen at THE RAISE",
		"y": 812.0, "rules_y": 846.0})

## THE POOL EXPANSION — receipt -> two-tap -> Esc abandons (desk["mode"] pops).
static func _draw_pool_page(b, state: GameState) -> void:
	DeskKit.back(b, "back to the cap table", func() -> void:
		b.desk.erase("mode"))
	var y := 64.0
	y = DeskKit.hero_band(b, "expand the pool",
		"a bigger pool hires better people — and the slice comes out of EVERY holder, you first.",
		DeskKit.INK, y)
	var pool := float(state.esop.get("pool_pct", 0.0))
	var keep := 1.0 - POOL_STEP / 100.0
	var lines: Array = [
		{"label": "the pool today", "value": "%.1f%% (%.1f%% free)" % [pool, SimOwnership.pool_free(state)]},
		{"label": "after the expansion", "value": "%.1f%%" % (pool * keep + POOL_STEP)},
		{"label": "your slice", "value": "%.1f%% -> %.1f%%" % [state.founder_pct, state.founder_pct * keep], "col": DeskKit.PEN},
	]
	for cf in state.cofounders:
		var cfd: Dictionary = cf
		var eq := float(cfd.get("equity_diluted", cfd.get("equity", 0.0)))
		lines.append({"label": String(cfd.get("name", "cofounder")),
			"value": "%.1f%% -> %.1f%%" % [eq, eq * keep]})
	y = DeskKit.ticket(b, DeskKit.X_ID + 40.0, y + 6.0, 560.0, {
		"title": "the expansion, priced", "lines": lines,
		"total_label": "new grants it can fund", "total_value": "+%.0f%% of the company" % POOL_STEP,
		"total_col": DeskKit.SAGE,
		"foot": "ink on paper — no cash moves; the dilution is the price"})
	DeskKit.arm(b, "pool_expand", "SIGN THE EXPANSION", "press again — every holder dilutes",
		Vector2(DeskKit.X_ID + 40.0, y + 8.0), func() -> void:
			SimOwnership.expand_pool(b.state, POOL_STEP)
			b.desk.erase("mode"), 420.0)
	DeskKit.footer(b, {"computed": "grants raise labor-market appeal — comp is a mix, not a number",
		"rules": "Esc abandons — nothing moves until the second tap", "y": 812.0, "rules_y": 846.0})

# ─────────────────────────── the page's own reads ────────────────────────────

static func _board_line(state: GameState) -> String:
	if state.board.is_empty():
		return "no board — nobody to answer to yet"
	var seats := state.board_seats_investor
	var strikes := int(state.board.get("strikes", 0))
	return "board: %d seat%s theirs · %s" % [seats, "" if seats == 1 else "s",
		("covenant met, 0 strikes" if strikes == 0 else "strike %d on the record" % strikes)]

static func _stack_count(state: GameState) -> int:
	var n := 0
	for inst in state.instruments:
		var idd: Dictionary = inst
		var kind := String(idd.get("kind", ""))
		if (kind == "safe" or kind == "note" or kind == "bridge") and float(idd.get("pct", 0.0)) <= 0.0:
			n += 1
	return n

## The ledger's rows: you, cofounders, equity-holding paper, the pool.
static func _slice_rows(state: GameState) -> Array:
	var rows: Array = []
	rows.append({"cells": ["you", "common", "sweat", "%.1f%%" % state.founder_pct, "last in line"],
		"cfg": {"col": DeskKit.INK}})
	for cf in state.cofounders:
		var cfd: Dictionary = cf
		rows.append({"cells": ["%s — cofounder" % String(cfd.get("name", "?")), "common", "sweat",
			"%.1f%%" % float(cfd.get("equity_diluted", cfd.get("equity", 0.0))), "last in line"],
			"cfg": {}})
	for inst in state.instruments:
		var idd: Dictionary = inst
		var pct := float(idd.get("pct", 0.0))
		if pct <= 0.0:
			continue
		var kind := String(idd.get("kind", "priced"))
		var label := "preferred" if kind == "priced" else "converted %s" % kind
		var prefs := float(idd.get("prefs", 0.0))
		rows.append({"cells": [String(idd.get("holder", "?")), label,
			"$%s" % SimOwnership.money(int(idd.get("amount", 0))), "%.1f%%" % pct,
			("%.0f× non-participating" % prefs) if prefs > 0.0 else "converts with common"],
			"cfg": {}})
	if not state.esop.is_empty():
		var pool := float(state.esop.get("pool_pct", 0.0))
		var free := SimOwnership.pool_free(state)
		rows.append({"cells": ["the ESOP pool -> team", "options", "—", "%.1f%%" % pool,
			"%.1f%% granted · %.1f%% free" % [pool - free, free]], "cfg": {}})
	elif state.option_pool_pct > 0.0:
		# a pool promised before the esop book opened — still a slice
		rows.append({"cells": ["the option pool (promised)", "options", "—",
			"%.1f%%" % state.option_pool_pct, "grants start with the esop book"],
			"cfg": {}})
	# THE ACCOUNTING RULES LAW: the named slices + the rest = the whole pie —
	# whatever the book cannot name is still shown, never silently missing
	var named := state.founder_pct
	for cf3 in state.cofounders:
		var cfd3: Dictionary = cf3
		named += float(cfd3.get("equity_diluted", cfd3.get("equity", 0.0)))
	for inst3 in state.instruments:
		named += maxf(float((inst3 as Dictionary).get("pct", 0.0)), 0.0)
	if not state.esop.is_empty():
		named += float(state.esop.get("pool_pct", 0.0))
	elif state.option_pool_pct > 0.0:
		named += state.option_pool_pct
	var rest := 100.0 - named
	if rest >= 0.5:
		rows.append({"cells": ["the rest — smaller holders", "mixed", "—",
			"%.1f%%" % rest, "angels, early paper, rounding"],
			"cfg": {"col": Color(DeskKit.INK, 0.6)}})
	return rows

## THE DILUTION STORY's checkpoints — backward from today (header's math).
static func _dilution_steps(state: GameState, val: int) -> Array:
	var priced: Array = []
	for inst in state.instruments:
		var idd: Dictionary = inst
		if String(idd.get("kind", "")) == "priced" and float(idd.get("pct", 0.0)) > 0.0:
			priced.append(idd)
	priced.sort_custom(func(a, b2) -> bool:
		return int((a as Dictionary).get("signed_wk", 0)) < int((b2 as Dictionary).get("signed_wk", 0)))
	var steps: Array = []
	steps.append({"label": "day 0", "pct": 100.0, "note": "100% · $0"})
	# un-scale through the newest round to find the slice just before it
	var now := state.founder_pct
	var before_round := now
	var pool_keep := 1.0 - clampf(SimBoard.pool_ask_pct(state), 0.0, 15.0) / 100.0
	if not priced.is_empty():
		var newest: Dictionary = priced[priced.size() - 1]
		var inv_keep := 1.0 - float(newest.get("pct", 0.0)) / 100.0
		before_round = clampf(now / maxf(inv_keep * pool_keep, 0.01), 0.0, 100.0)
	if priced.size() > 1:
		steps.append({"label": "+%d earlier" % (priced.size() - 1), "pct": minf(before_round + 8.0, 100.0),
			"note": "smaller each time"})
	if _stack_count(state) > 0 or _any_converted(state):
		steps.append({"label": "the SAFE stack", "pct": before_round,
			"note": "converts later*"})
	if not state.esop.is_empty() and not priced.is_empty():
		steps.append({"label": "the pool", "pct": clampf(before_round * pool_keep, 0.0, 100.0),
			"note": "the top-up dilutes YOU"})
	if not priced.is_empty():
		var newest2: Dictionary = priced[priced.size() - 1]
		steps.append({"label": "wk%d · priced" % int(newest2.get("signed_wk", 0)),
			"pct": now, "note": "bigger pie"})
	var paper := int(float(val) * now / 100.0)
	steps.append({"label": "now", "pct": now, "note": "≈ %s on paper" % SimOwnership.money_short(paper)})
	# a story with only day0+now and no events collapses to the empty state
	if steps.size() == 2 and state.instruments.is_empty() and state.esop.is_empty():
		return []
	return steps

static func _any_converted(state: GameState) -> bool:
	for inst in state.instruments:
		var idd: Dictionary = inst
		var kind := String(idd.get("kind", ""))
		if (kind == "safe" or kind == "note" or kind == "bridge") and float(idd.get("pct", 0.0)) > 0.0:
			return true
	return false

static func _costline(state: GameState) -> String:
	var bits: Array[String] = []
	if not state.esop.is_empty():
		bits.append("the pool has %.1f%% free — recruitment draws from it" % SimOwnership.pool_free(state))
	var cliff := _next_cliff(state)
	if cliff != "":
		bits.append(cliff)
	if not state.board.is_empty():
		bits.append("covenant %s" % ("met" if int(state.board.get("strikes", 0)) == 0 else
			"strike %d" % int(state.board.get("strikes", 0))))
	if bits.is_empty():
		return "no pool, no paper — the clean page IS the bootstrap flex"
	return " · ".join(bits)

static func _next_cliff(state: GameState) -> String:
	var best_in := 1 << 30
	var who := ""
	for g in state.esop.get("granted", []):
		var gd: Dictionary = g
		if String(gd.get("emp_id", "")).begins_with("left:"):
			continue
		var cliff_in := int(gd.get("vest_start_wk", 0)) + SimOwnership.CLIFF_WEEKS - state.week
		if cliff_in > 0 and cliff_in < best_in:
			best_in = cliff_in
			who = String(gd.get("emp_id", "")).replace("_", " ")
	if who == "":
		return ""
	return "next cliff: %s's %d wks in %d wk%s" % [who, SimOwnership.CLIFF_WEEKS, best_in,
		"" if best_in == 1 else "s"]

static func handle(_b, _id: String) -> void:
	pass
