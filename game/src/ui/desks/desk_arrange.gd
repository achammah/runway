class_name DeskArrange
extends RefCounted
## THE ARRANGE MODE SHELL (DECISIONS: the works' WRITE view; mockup 14).
## One neutral assignment layout regardless of the read face: divisions as
## labeled BINS (+ the SHARED/HQ bin + the dashed ghost), elements as CHIPS
## (people, machines, spend lines). TWO PRESSES, NO DRAG: press a chip, press
## its new home; the staged change prints a PRE-MOVE RECEIPT; CONFIRM is a
## two-tap; Esc abandons the whole staged change (the desk-mode pop).
##
## THIS IS THE SHELL: the bins, chips, staging and receipt are real; the
## PRICES on the receipt are placeholders and the confirm books NOTHING —
## L-DIVWORKS wires reassign_employee/move_machine/tag_spend_line and the
## price book here. The interaction contract is what this file locks.

const BIN_W := 260.0
const BIN_H := 190.0

static func draw(b) -> void:
	var state: GameState = b.state
	DeskKit.back(b, "back to the works", func() -> void:
		b.desk["mode"] = ""
		b.desk.erase("chip")
		b.desk.erase("staged"))
	b.label("ARRANGE — press a thing, then press its new home", Vector2(220.0, 8.0),
		DeskKit.STATUS, Color(DeskKit.INK, 0.6), 760.0)
	# ── the bins row: this run's divisions (stub: one roof), SHARED/HQ, ghost
	var y := 64.0
	var bins := ["HQ — the roof", "SHARED / HQ"]
	var bx := DeskKit.X_ID
	for i in bins.size():
		var bin_name: String = bins[i]
		DeskKit.bin(b, bx, y, BIN_W, BIN_H, {
			"title": bin_name,
			"note": "everything lives here today" if i == 0 else
				"what has no single roof — allocated vs direct IS the lesson",
			"on_press": func() -> void: _press_bin(b, bin_name),
		})
		bx += BIN_W + 22.0
	DeskKit.bin(b, bx, y, BIN_W, BIN_H, {"ghost": true, "on_press": func() -> void:
		b.desk["staged_note"] = "a new roof opens through the open_site door — the lease quote, capex and hire pack arrive as one priced receipt"})
	y += BIN_H + 26.0
	# ── the chips: people (real), machines (real when the works has them),
	# spend lines (the org book's rows — stub: the four engine levers)
	b.label("THE PIECES", Vector2(DeskKit.X_ID, y), DeskKit.DETAIL,
		Color(DeskKit.INK, 0.6), 300.0)
	y += 36.0
	var cx := DeskKit.X_ID
	var picked := String(b.desk.get("chip", ""))
	for e in state.employees:
		var nm := String((e as Dictionary).get("name", "someone"))
		cx = DeskKit.chip(b, cx, y, {"text": nm, "kind": "person",
			"selected": picked == nm,
			"on_press": func() -> void: _press_chip(b, nm)})
		if cx > 900.0:
			cx = DeskKit.X_ID
			y += 46.0
	if state.employees.is_empty():
		b.label("nobody on payroll yet — chips appear as the company does",
			Vector2(DeskKit.X_ID, y), DeskKit.DETAIL, Color(DeskKit.INK, 0.5), 700.0)
	y += 52.0
	cx = DeskKit.X_ID
	for lever in ["sales", "care", "rnd", "office"]:
		var ln := String(lever)
		cx = DeskKit.chip(b, cx, y, {"text": ln + " $%d/wk" % int(state.budgets.get(ln, 0)),
			"kind": "spend", "selected": picked == ln,
			"on_press": func() -> void: _press_chip(b, ln)})
	y += 58.0
	# THE LOCKED STRIP: bound elements never move by hand — the player learns
	# which costs follow which objects.
	b.label("bound to their objects (never move by hand): rent → its roof · " +
		"serving costs → their offer · interest → its note", Vector2(DeskKit.X_ID, y),
		DeskKit.LAW, Color(DeskKit.INK, 0.5), 1080.0)
	y += 44.0
	# ── the staged-change receipt panel
	var staged: Array = b.desk.get("staged", [])
	if staged.is_empty():
		var note := String(b.desk.get("staged_note", ""))
		if note != "":
			b.label(note, Vector2(DeskKit.X_ID, y), DeskKit.DETAIL, DeskKit.BLUE, 1080.0)
		DeskKit.footer(b, {
			"computed": "ink is free · brick is priced · obligations survive removal",
			"rules": "two presses stage a move; nothing is booked until the receipt is confirmed",
		})
		return
	var lines: Array = []
	for m in staged:
		var md: Dictionary = m
		lines.append({"label": "%s → %s" % [String(md.get("chip", "")), String(md.get("to", ""))],
			"value": "$400 now · 1 wk ramp", "col": DeskKit.PEN})
	ticket_end(b, y, lines, staged.size())

static func ticket_end(b, y: float, lines: Array, n: int) -> void:
	var end_y := DeskKit.ticket(b, DeskKit.X_ID, y, 560.0, {
		"title": "the staged change — nothing is booked yet",
		"lines": lines,
		"total_label": "the price of the move",
		"total_value": "$%d now" % (n * 400),
		"foot": "placeholder pricing — the price book wires in with the divisions lane",
	})
	DeskKit.arm(b, "arrange_confirm", "CONFIRM the change", "press again — $%d books now" % (n * 400),
		Vector2(620.0, y + 30.0), func() -> void:
			b.desk.erase("staged")
			b.desk["staged_note"] = "the shell confirmed — the real ops land with L-DIVWORKS",
		360.0)
	DeskKit.word(b, "tear it up", Vector2(620.0, y + 84.0), func() -> void:
		b.desk.erase("staged"), DeskKit.STATUS, Color(DeskKit.INK, 0.7), 240.0)
	DeskKit.footer(b, {"y": maxf(end_y, 700.0),
		"computed": "Esc abandons the whole staged change — the mode pop is the abandon",
		"rules": ""})

static func _press_chip(b, nm: String) -> void:
	if String(b.desk.get("chip", "")) == nm:
		b.desk.erase("chip")
	else:
		b.desk["chip"] = nm

static func _press_bin(b, bin_name: String) -> void:
	var picked := String(b.desk.get("chip", ""))
	if picked == "":
		return
	var staged: Array = b.desk.get("staged", [])
	staged.append({"chip": picked, "to": bin_name})
	b.desk["staged"] = staged
	b.desk.erase("chip")

static func handle(b, id: String) -> void:
	if id == "leave":
		b.desk["mode"] = ""
