class_name DeskBills
extends RefCounted
## DESK — COSTS · "bills" = THE BILLS LEDGER (DAG2 W2 L-MONEY; DECISIONS:
## bills = B on THE LEDGER SHEET; pixels: docs/design/mockups/06).
##
## OBLIGATIONS, NOT CHOICES: rows are engine truth (the roof by era + sites,
## the payroll sum, the catalog's fixed lines, serving COGS, the notes'
## interest, the taxman) and carry NO adjust buttons — you change a bill by
## changing its source. The TREND column teaches what moves each line; the
## memo compares the Monday floor to revenue. Sections THE FLAT / THE
## SCALING, single-ruled subtotals, the TOTAL double-ruled and equal to the
## hero. Standing commitments render too — obligations survive removal.
##
## Counterparty names come from state.topics when the world wrote them
## (topics.names.landlord / topics.names.bank), else plain words.

const QUESTION := "what must be paid every Monday?"

const SHEET_X := 10.0
const SHEET_W := 1120.0
const Y_SHEET := 108.0
const Y_FOOT := 806.0
const Y_RULES := 840.0
## Expanded tool lines cap here; the rest fold into "+N more".
const TOOLS_MAX := 5

static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	var total := _sum(_flat_rows(s, false)) + _sum(_scaling_rows(s))
	return {"big": "$%s/Mon" % _fmt(total), "line": "the flat vs the scaling"}

static func draw(b) -> void:
	var state: GameState = b.state
	var flat := _flat_rows(state, bool(b.desk.get("tools_open", false)))
	var scaling := _scaling_rows(state)
	var flat_sum := _sum(flat)
	var scaling_sum := _sum(scaling)
	var total := flat_sum + scaling_sum

	# ── the hero: the Monday floor, which the double-ruled TOTAL equals
	var big: String = "$" + b.fmt(total)
	b.label(big, Vector2(SHEET_X, 6.0), DeskKit.HERO, DeskKit.INK, 460.0)
	var bw: float = b.font().get_string_size(big, HORIZONTAL_ALIGNMENT_LEFT, -1, DeskKit.HERO).x
	b.label("every Monday, before you choose anything", Vector2(SHEET_X + bw + 16.0, 22.0),
		DeskKit.ROW, Color(DeskKit.INK, 0.7), 560.0)
	b.label("the flat moves when you move; the scaling moves when the business does.",
		Vector2(SHEET_X, 62.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 760.0)
	var meta: Label = b.label("week %d · %s era" % [state.week, state.era],
		Vector2(SHEET_X, 10.0), DeskKit.LAW, Color(DeskKit.INK, 0.42), SHEET_W)
	meta.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT

	# ── the sheet (no ADJUST column: obligations aren't adjustable)
	var sheet := DeskKit.ledger_sheet(b, SHEET_X, Y_SHEET, SHEET_W, {
		"columns": [{"label": "who we pay", "w": 250.0}, {"label": "for what", "w": 300.0},
			{"label": "kind", "w": 90.0, "align": "center"},
			{"label": "$/wk", "w": 130.0, "align": "right"}, {"label": "trend", "w": 300.0}],
		"amount": 3, "adjust": false, "unit": "all figures $/week",
	})
	DeskKit.ledger_section(b, sheet, "the flat")
	for r in flat:
		_row(b, sheet, r)
	DeskKit.ledger_subtotal(b, sheet, "subtotal — the flat", "$" + b.fmt(flat_sum))
	DeskKit.ledger_section(b, sheet, "the scaling")
	for r2 in scaling:
		_row(b, sheet, r2)
	DeskKit.ledger_subtotal(b, sheet, "subtotal — the scaling", "$" + b.fmt(scaling_sum))
	DeskKit.ledger_total(b, sheet, "total bills", "$" + b.fmt(total))
	var pnl: Dictionary = state.get_meta("pnl", {})
	var revenue := int(pnl.get("revenue", 0))
	if revenue > 0:
		var ratio := float(total) / float(revenue)
		var memo_note := ""
		if ratio >= 1.0:
			memo_note = "the Monday floor eats %.1f× revenue" % ratio
		else:
			memo_note = "revenue covers the floor ×%.1f — the machine feeds itself" % (1.0 / maxf(ratio, 0.01))
		DeskKit.ledger_memo(b, sheet, "revenue last week", "$" + b.fmt(revenue), memo_note)
	else:
		DeskKit.ledger_memo(b, sheet, "revenue last week", "$0",
			"no revenue yet — the floor waits for nobody")
	DeskKit.ledger_end(b, sheet)

	# ── the teaching foot
	b.label("bills are obligations — you change a bill by changing its source: the roof, the roster, the catalog, the debt",
		Vector2(SHEET_X, Y_FOOT), DeskKit.LAW, Binder.BLUE, 1100.0)
	b.label("single rule = subtotal · double rule = total — the book always balances to the hero · severance and notice periods survive removal",
		Vector2(SHEET_X, Y_RULES), DeskKit.LAW, Color(DeskKit.INK, 0.5), 1100.0)

## One bill row + its optional press-through and coral trend.
static func _row(b, sheet: Dictionary, r: Dictionary) -> void:
	var cfg := {}
	if r.has("press"):
		var target := String(r.get("press", ""))
		var go := func() -> void:
			if target == "tools":
				b.desk["tools_open"] = not bool(b.desk.get("tools_open", false))
			else:
				b.focus_desk(target)
		cfg["on_press"] = go
	if bool(r.get("dim", false)):
		cfg["dim"] = true
	var row_y := float(sheet.get("cursor", 0.0))
	# a colored trend renders ONLY as the overlay — the cell stays empty so
	# the two never double-print
	var cell_note := "" if r.has("note_col") else String(r.get("note", ""))
	DeskKit.ledger_row(b, sheet, [String(r.get("who", "")), String(r.get("what", "")),
		String(r.get("kind", "")), "$" + b.fmt(int(r.get("amt", 0))), cell_note], cfg)
	if r.has("note_col"):
		var cols: Array = sheet.get("cols", [])
		var tcol: Dictionary = cols[4]
		b.label(String(r.get("note", "")), Vector2(float(tcol.get("x", 0.0)), row_y + 8.0),
			18, r.get("note_col"), float(tcol.get("w", 200.0)) - 10.0)

# ── the rows, from engine truth ──────────────────────────────────────────────

static func _flat_rows(state: GameState, tools_open: bool) -> Array:
	var rows: Array = []
	var era_rent := int(GameState.ERA_RENT.get(state.era, 150))
	rows.append({"who": _name_of(state, "landlord", "the landlord"),
		"what": "the %s-era roof" % state.era, "kind": "flat", "amt": era_rent,
		"note": _rent_trend(state)})
	for s in state.sites:
		var sd: Dictionary = s
		rows.append({"who": String(sd.get("name", "a second roof")),
			"what": "a roof of its own", "kind": "flat", "amt": int(sd.get("rent_wk", 0)),
			"note": "opened wk %d" % int(sd.get("opened_wk", 0))})
	var payroll := SimLabor.payroll_wk(state)
	var heads := state.employees.size() + state.pipeline.size()
	rows.append({"who": "the payroll", "what": "%d people -> team" % heads, "kind": "flat",
		"amt": payroll, "note": _payroll_trend(state), "press": "team"})
	var tool_lines := _tool_lines(state)
	var tools := int(round(SimEngine.offers_fixed_wk(state)))
	if tools > 0 or not tool_lines.is_empty():
		rows.append({"who": "the tools", "what": "%d lines — the catalog's fixed costs" % tool_lines.size(),
			"kind": "flat", "amt": tools, "note": "grows with the catalog", "press": "tools"})
		if tools_open:
			var shown := 0
			for t in tool_lines:
				if shown >= TOOLS_MAX:
					rows.append({"who": "", "what": "+%d more tool lines" % (tool_lines.size() - shown),
						"kind": "", "amt": 0, "note": "", "dim": true, "skip_sum": true})
					break
				var td: Dictionary = t
				rows.append({"who": "· " + String(td.get("label", "a tool")),
					"what": String(td.get("offer", "")), "kind": "flat",
					"amt": int(round(float(td.get("amount", 0.0)))), "note": "", "dim": true,
					"skip_sum": true})
				shown += 1
	var standing := 0
	var standing_wks := 0
	for c in state.commitments:
		var cd: Dictionary = c
		if int(cd.get("weeks_left", 0)) <= 0:
			continue
		standing += absi(mini(int(cd.get("cash_wk", 0)), 0))
		standing_wks = maxi(standing_wks, int(cd.get("weeks_left", 0)))
	if standing > 0:
		rows.append({"who": "the standing costs", "what": "what nobody planned, on a plan",
			"kind": "flat", "amt": standing, "note": "runs out within %d wks" % standing_wks})
	return rows

static func _scaling_rows(state: GameState) -> Array:
	var rows: Array = []
	var cogs_pc := SimEngine.offers_cogs_per_customer(state)
	var serving := int(round(cogs_pc * float(state.traction)))
	var margin_safe := SimBank.contribution_margin(state) > 0.0
	rows.append({"who": "serving customers", "what": "≈$%.0f × %d, every week" % [cogs_pc, state.traction],
		"kind": "scales", "amt": serving,
		"note": "margin-safe at your prices" if margin_safe else "each one serves at a loss",
		"note_col": Color("5D7A50") if margin_safe else Binder.PEN})
	var interest := 0
	var amortizing := false
	var only_fee := false
	if state.loan_principal > 0:
		interest += int(ceil(float(state.loan_principal) * SimBank.SHARK_RATE))
		only_fee = true
	for l in state.loans:
		var ld: Dictionary = l
		var bal := int(ld.get("balance", 0))
		if bal <= 0:
			continue
		interest += int(ceil(float(bal) * float(ld.get("rate_wk", 0.0))))
		if String(ld.get("kind", "")) == "bank":
			amortizing = true
		else:
			only_fee = true
	if interest > 0:
		var note := "falls as you repay"
		var ncol := Color("5D7A50")
		if not amortizing and only_fee:
			note = "never falls on its own — interest only"
			ncol = Binder.PEN
		rows.append({"who": _name_of(state, "bank", "the bank"),
			"what": "interest on $%s -> the bank" % _fmt(SimBank.debt_total(state)),
			"kind": "scales", "amt": interest, "note": note, "note_col": ncol,
			"press": "the bank"})
	var pnl: Dictionary = state.get_meta("pnl", {})
	var tax := int(pnl.get("tax", 0))
	var tax_note := ""
	if state.era_index() < SimBank.TAX_ERA:
		tax_note = "waiting — below the radar until the office era"
	elif state.tax_loss_carry > 0:
		tax_note = "losses banked: $%s shelter profit" % _fmt(state.tax_loss_carry)
	else:
		tax_note = "20% of profit, after interest"
	rows.append({"who": "the taxman", "what": "on profit — never on revenue",
		"kind": "scales", "amt": tax, "note": tax_note})
	return rows

## Every offer's fixed lines, flattened with their offer's name.
static func _tool_lines(state: GameState) -> Array:
	var out: Array = []
	for o in state.offers:
		var od: Dictionary = o
		for fl in od.get("fixed_lines", []):
			var fd: Dictionary = fl
			out.append({"label": String(fd.get("label", "a tool")),
				"offer": String(od.get("name", "")), "amount": float(fd.get("amount", 0.0))})
	return out

static func _rent_trend(state: GameState) -> String:
	var idx := state.era_index()
	if idx >= GameState.ERAS.size() - 1:
		return "the last roof on the ladder"
	var next_era := String(GameState.ERAS[idx + 1])
	var cur := maxi(int(GameState.ERA_RENT.get(state.era, 150)), 1)
	var nxt := int(GameState.ERA_RENT.get(next_era, cur))
	return "jumps ×%d at the %s era" % [int(round(float(nxt) / float(cur))), next_era]

static func _payroll_trend(state: GameState) -> String:
	var pending := 0
	for r in state.open_roles:
		pending += int((r as Dictionary).get("offered_salary", 0)) * maxi(int((r as Dictionary).get("seats", 1)), 1)
	if pending > 0:
		return "+$%s if you fill the seat%s" % [_fmt(pending), "" if state.open_roles.size() == 1 else "s"]
	return "moves only when you hire"

## A section's honest sum — breakdown rows (skip_sum) never join it.
static func _sum(rows: Array) -> int:
	var total := 0
	for r in rows:
		if not bool((r as Dictionary).get("skip_sum", false)):
			total += int((r as Dictionary).get("amt", 0))
	return total

## The world's own counterparty name when the topics carry one, else plain.
static func _name_of(state: GameState, key: String, fallback: String) -> String:
	var direct := String(state.topics.get(key, ""))
	if direct != "":
		return direct
	var names: Dictionary = state.topics.get("names", {})
	var nested := String(names.get(key, ""))
	return nested if nested != "" else fallback

static func handle(_b, _id: String) -> void:
	pass   # rows route through closures; obligations carry no controls

static func _fmt(n: int) -> String:
	var s := str(absi(n))
	var out := ""
	while s.length() > 3:
		out = "," + s.substr(s.length() - 3) + out
		s = s.substr(0, s.length() - 3)
	return ("-" if n < 0 else "") + s + out
