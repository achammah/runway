extends SceneTree
## KIT SHOTS — the eight rework primitives, drawn on a blank clipboard so the
## desk agents and QA can SEE what they are composing with before a single desk
## is rewritten. This is the demo bench, not a desk: there is no engine read on
## the page, only the components with plausible numbers in them.
##
## docs/design/11-binder-rework.md §NEW DESKKIT PRIMITIVES is the contract:
##   hero_band · card_frame · money_row · twobar · funnel_shape · meter ·
##   grid2 · sev_dot
##
## Run: RUNWAY_STRESS_DIR=<dir> godot --path . --script tests/kit_shots.gd
## Files land as kit_<sheet>_godot.png so the Unity twin can sit beside them.

var _dir := "/tmp"
var _b: Binder = null
var _shots: Array[String] = []

func _init() -> void:
	call_deferred("_go")

# ═══════════════════════════ the shot harness ════════════════════════════════

func _shot(nm: String) -> void:
	await create_timer(0.25).timeout
	await RenderingServer.frame_post_draw
	root.get_viewport().get_texture().get_image().save_png("%s/%s_godot.png" % [_dir, nm])
	_shots.append(nm)
	print("SHOT %s" % nm)

## The bench's company: a live office-era state so the rail's counts and the
## embedded desks have something true to say.
func _state() -> GameState:
	var s := GameState.new()
	s.sim_seed = 7
	s.week = 21
	s.era = "office"
	s.cash = 18_400
	s.company_name = "Mossflow"
	return s

## A fresh binder, tour held off — the frame, the rail and an empty sheet.
## The kit draws onto it exactly the way a desk would.
func _open() -> Binder:
	if _b != null and is_instance_valid(_b):
		_b.queue_free()
		await process_frame
	var b := Binder.new()
	b.tour_enabled = false
	b.setup(_state())
	root.add_child(b)
	b.size = Vector2(1536, 1024)
	await create_timer(0.35).timeout
	_b = b
	return b

func _blank() -> Binder:
	var b := await _open()
	# blank the pane the landing desk just filled — this bench is its own page
	for c in b.pane().get_children():
		c.queue_free()
	await process_frame
	return b

# ═════════════════════════════════ the sheets ════════════════════════════════

## SHEET ONE — the grammar of a reworked desk, end to end: the hero band with a
## drawn instrument beside it, two paper cards with their money in one column,
## and the teaching foot. This is the picture every desk has to match.
func _grammar() -> void:
	var b := await _blank()
	# the instrument the band reserves room for — the caller's own drawing
	b.debt_jar(0.34, Vector2(DeskKit.X_ID, 8.0), Vector2(84, 104))
	var y := DeskKit.hero_band(b, "$18,400", "alive, losing $218 a week.",
		DeskKit.INK, 6.0, true)
	# a meter riding under the band: the fuse, with its number after it
	y = DeskKit.meter(b, DeskKit.X_ID, y, 300.0, 0.58, DeskKit.SAGE, "runway 14 weeks")
	y += 10.0

	var left := DeskKit.card_frame(b, DeskKit.X_ID, y, 548.0, 250.0, "the company")
	DeskKit.money_row(b, left, "era", "office")
	DeskKit.money_row(b, left, "week", "21")
	DeskKit.money_row(b, left, "crew", "3 heads")
	DeskKit.money_row(b, left, "valuation", "$2,400,000", DeskKit.BLUE)

	# THE CORAL BUDGET HOLDS INSIDE A CARD TOO: the amounts are ink, and the one
	# line that is money bleeding gets the pen. Five coral rows would be five
	# alarms, which is none.
	var right := DeskKit.card_frame(b, DeskKit.X_ID + 572.0, y, 548.0, 250.0,
		"fixed costs", true)
	DeskKit.money_row(b, right, "rent", "$3,000/wk", DeskKit.INK,
		func() -> void: pass, func() -> void: pass)
	DeskKit.money_row(b, right, "payroll", "$3,950/wk")
	DeskKit.money_row(b, right, "infra", "$220/wk")
	DeskKit.money_row(b, right, "the shark", "-$2,232/wk", DeskKit.PEN)

	# A DRAWN SHAPE INSIDE A CARD — Law 4 living inside Law 3, which is what most
	# reworked panes will actually look like.
	var wide := DeskKit.card_frame(b, DeskKit.X_ID, y + 274.0, 1120.0, 200.0,
		"where the money went")
	DeskKit.twobar(b, wide.content_x, wide.content_y, 1064.0,
		"in", "$1,240", [1240.0],
		"out", "$1,458", [820.0, 380.0, 178.0, 80.0],
		DeskKit.SAGE, DeskKit.PEN)

	DeskKit.footer(b, {
		"computed": "CAC $310 · LTV $1,104 · payback 11 wks",
		"rules": "the rules of this bench: every primitive here is kit-owned — a desk "
			+ "spends a y cursor and nothing else",
	})
	await _shot("kit_grammar")

## SHEET TWO — the drawn shapes: the two-bar with its out side segmented by lane,
## the funnel with its bottom stage still fogged, and the severity ramp beside it.
func _shapes() -> void:
	var b := await _blank()
	b.label("the drawn shapes", Vector2(DeskKit.X_ID, 6.0), DeskKit.TITLE)
	var y := 70.0
	y = DeskKit.twobar(b, DeskKit.X_ID, y, 1120.0,
		"in", "$1,240", [1240.0],
		"out", "$1,458", [820.0, 380.0, 178.0, 80.0],
		DeskKit.SAGE, DeskKit.PEN)
	y += 12.0
	DeskKit.funnel_shape(b, DeskKit.X_ID, y, 620.0, [
		{"label": "reach", "value_text": "1,240"},
		{"label": "leads", "value_text": "96"},
		{"label": "signed", "value_text": "11"},
		{"label": "kept", "value_text": "8", "known": false},
	])
	# the ramp rides beside the funnel: the same page, two instruments, no collision
	var sx := 700.0
	b.label("the attention ramp", Vector2(sx, y - 4.0), DeskKit.STATUS,
		Color(DeskKit.INK, 0.6))
	var sy := y + 48.0
	for i in 3:
		DeskKit.sev_dot(b, sx, sy + float(i) * 58.0, i + 1)
		b.label(["a note — the tab wears it, and nothing else does",
			"a warning — the tab, the ticker, and a row on this desk",
			"an alarm — the same pen, simply bigger, at the top of the list"][i],
			Vector2(sx + DeskKit.SEV_BOX + 12.0, sy + float(i) * 58.0 - 2.0),
			DeskKit.DETAIL, DeskKit.INK if i > 0 else Color(DeskKit.INK, 0.7), 400.0)
	DeskKit.footer(b, {
		"computed": "every shape carries its own number — a chart without one is decoration",
		"rules": "the funnel's last mouth is fogged: an earned dashboard is a mechanic, "
			+ "never a missing feature",
	})
	await _shot("kit_shapes")

## SHEET THREE — the levers and the fills: the 2×2 grid at its real width, and
## three meters showing what a drawn fill does that a percentage cannot.
func _levers() -> void:
	var b := await _blank()
	b.label("the levers", Vector2(DeskKit.X_ID, 6.0), DeskKit.TITLE)
	var y := DeskKit.grid2(b, DeskKit.X_ID, 78.0, [
		{"name": "ads", "value": "$2,000/wk", "effect": "reach ×1.92",
			"on_minus": func() -> void: pass, "on_plus": func() -> void: pass},
		{"name": "content", "value": "$500/wk", "effect": "compounding",
			"on_minus": func() -> void: pass, "on_plus": func() -> void: pass},
		{"name": "referrals", "value": "$250/wk", "effect": "cheapest",
			"on_minus": func() -> void: pass, "on_plus": func() -> void: pass, "at_min": true},
		{"name": "outbound", "value": "$1,000/wk", "effect": "burning",
			"on_minus": func() -> void: pass, "on_plus": func() -> void: pass, "at_max": true},
	])
	y += 24.0
	b.label("the drawn fills", Vector2(DeskKit.X_ID, y), DeskKit.STATUS,
		Color(DeskKit.INK, 0.6))
	y += 48.0
	y = DeskKit.meter(b, DeskKit.X_ID, y, 420.0, 0.58, DeskKit.SAGE, "runway 14 weeks")
	y += 18.0
	y = DeskKit.meter(b, DeskKit.X_ID, y, 420.0, 0.62, DeskKit.BLUE,
		"cold-start fix — ships in ~2 wks")
	y += 18.0
	y = DeskKit.meter(b, DeskKit.X_ID, y, 420.0, 0.91, DeskKit.PEN,
		"utilization 91% — the bench is the ceiling")
	# a card beside them, so the two densities can be compared on one sheet
	var f := DeskKit.card_frame(b, 620.0, 320.0, 510.0, 240.0, "what one sale earns")
	DeskKit.money_row(b, f, "street price", "$18.00")
	DeskKit.money_row(b, f, "cost to serve", "-$6.10", DeskKit.PEN)
	DeskKit.money_row(b, f, "fixed, per week", "-$40.00", DeskKit.PEN)
	DeskKit.money_row(b, f, "contribution", "$11.90", DeskKit.SAGE)
	DeskKit.footer(b, {
		"rules": "a lever cell is a stepper with the prose taken out — name, money, "
			+ "one word of consequence, and the two glyphs",
	})
	await _shot("kit_levers")

# ═══════════════ the frame, photographed (DAG2 W1 — UI SPINE) ════════════════

## THE DIEGETIC BINDER OBJECT, fallback face: the drawn mini-binder with the
## name overlaid and the red sticker lit (a threats row is planted). Any real
## cached portrait is parked first so the FALLBACK is what the QA wave sees,
## then put back exactly as found.
func _object_fallback() -> void:
	var parked := ""
	if FileAccess.file_exists(Binder.PORTRAIT_PATH):
		parked = ProjectSettings.globalize_path(Binder.PORTRAIT_PATH) + ".bench_parked"
		DirAccess.rename_absolute(ProjectSettings.globalize_path(Binder.PORTRAIT_PATH), parked)
	await _object_stage(true, "kit_object_fallback")
	if parked != "":
		DirAccess.rename_absolute(parked, ProjectSettings.globalize_path(Binder.PORTRAIT_PATH))

## The portrait face: a stand-in PNG proves the texture slot + the overlay;
## the real art arrives from the generation lane. The stand-in is removed
## after the shot — a user's own cached portrait is never touched.
func _object_portrait() -> void:
	var made := false
	if not FileAccess.file_exists(Binder.PORTRAIT_PATH):
		var img := Image.create_empty(640, 780, false, Image.FORMAT_RGBA8)
		img.fill(Color(0, 0, 0, 0))
		img.fill_rect(Rect2i(60, 60, 500, 660), Color("DDBE8C"))
		img.fill_rect(Rect2i(60, 60, 70, 660), Color("CBA96F"))
		var tabs := [Color("8FA582"), Color("E86A5C"), Color("6E8CA0"), Color("F4B942")]
		for i in 4:
			img.fill_rect(Rect2i(548, 130 + i * 140, 60, 70), tabs[i])
		img.save_png(ProjectSettings.globalize_path(Binder.PORTRAIT_PATH))
		made = true
	await _object_stage(false, "kit_object_portrait")
	if made:
		DirAccess.remove_absolute(ProjectSettings.globalize_path(Binder.PORTRAIT_PATH))

## A quiet room-colored stage with the object at the scene's bottom-left —
## exactly where the room composites it.
func _object_stage(with_alert: bool, shot_name: String) -> void:
	if _b != null and is_instance_valid(_b):
		_b.queue_free()
		_b = null
		await process_frame
	var stage := ColorRect.new()
	stage.color = Color("2A2722")
	stage.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.add_child(stage)
	var s := _state()
	if with_alert:
		s.set_flag("fundraising_open")   # a real sev-3 row lights the sticker
	var obj := Binder.make_object(s, Callable())
	obj.position = Vector2(36, 740)
	stage.add_child(obj)
	await _shot(shot_name)
	stage.queue_free()
	await process_frame

## The rail's states: everything closed · REVENUE fanned · a group overview.
func _frame_states() -> void:
	var b := await _open()
	b._open_group = -1
	b.refresh()
	await _shot("kit_frame_closed")
	b.press_group(0)
	await _shot("kit_frame_revenue")
	b.press_group(1)
	b.press_group(1)   # the open header pressed again = the quartet/sextet
	await _shot("kit_overview_costs")

func _arrange_shell() -> void:
	var b := await _open()
	b.state.employees = [{"name": "June", "salary": 510}, {"name": "Ravi", "salary": 540}]
	b.focus_desk("the works")
	b.desk["mode"] = "arrange"
	b.desk["staged"] = [{"chip": "June", "to": "SHARED / HQ"}]
	b.refresh()
	await _shot("kit_arrange_shell")

func _momentary() -> void:
	var b := await _open()
	b.debug_summon_offer()
	b.focus_desk("cap table")
	await _shot("kit_momentary_rail")
	b.open_page("the offer")
	await _shot("kit_momentary_offer")

func _tour_steps() -> void:
	var b := await _open()
	b._tour = 0
	b._tour_apply()
	b.refresh()
	await _shot("kit_tour_step1")
	b._tour = 4
	b._tour_apply()
	b.refresh()
	await _shot("kit_tour_red_demo")

# ═══════════════ the v2 primitives, photographed ═════════════════════════════

## THE LEDGER SHEET + THE STEPPER LAW: sections, subtotal single rule, TOTAL
## double rule on the card tint, the memo, and the ADJUST column's two
## SEPARATE drawn buttons. Obligation rows carry no controls.
func _ledger() -> void:
	var b := await _blank()
	b.label("the ledger sheet", Vector2(DeskKit.X_ID, 6.0), DeskKit.TITLE)
	var sh := DeskKit.ledger_sheet(b, DeskKit.X_ID, 78.0, 1120.0, {
		"columns": [{"label": "line", "w": 300.0}, {"label": "buys", "w": 280.0},
			{"label": "$/wk", "w": 150.0, "align": "right"}, {"label": "effect", "w": 250.0}],
		"amount": 2, "adjust": true, "unit": "all figures $/week"})
	DeskKit.ledger_section(b, sh, "closing — sales")
	DeskKit.ledger_row(b, sh, ["sales engineering", "demos that land", "$180", ""],
		{"on_minus": func() -> void: pass, "on_plus": func() -> void: pass})
	DeskKit.ledger_row(b, sh, ["the demo rig", "always ready to show", "$120", ""],
		{"on_minus": func() -> void: pass, "on_plus": func() -> void: pass})
	DeskKit.ledger_subtotal(b, sh, "subtotal — closing", "$300", "deals shut +9% faster")
	DeskKit.ledger_section(b, sh, "people — office")
	DeskKit.ledger_row(b, sh, ["the kitchen", "fed people stay", "$220", ""],
		{"on_minus": func() -> void: pass, "on_plus": func() -> void: pass})
	DeskKit.ledger_row(b, sh, ["the rent", "the office-era roof", "$3,000", "obligation"],
		{"dim": true})
	DeskKit.ledger_subtotal(b, sh, "subtotal — people", "$3,220", "morale +1.2/wk")
	DeskKit.ledger_total(b, sh, "total org spend", "$3,520")
	DeskKit.ledger_memo(b, sh, "revenue this week", "$3,840", "the floor eats 0.9× revenue")
	var y := DeskKit.ledger_end(b, sh)
	DeskKit.footer(b, {"y": maxf(y, DeskKit.FOOTER_Y),
		"computed": "single rule = subtotal · double rule = total — the book balances to the hero",
		"rules": "the ADJUST column holds two SEPARATE drawn buttons — the stepper law"})
	await _shot("kit_ledger_sheet")

## THE NUMBERED ZONE + THE WALL: the didactic header, the kanban column, the
## card anatomy with its progress fill, the red READY variant, the fold row.
func _zone_wall() -> void:
	var b := await _blank()
	b.label("the zone + the wall", Vector2(DeskKit.X_ID, 6.0), DeskKit.TITLE)
	var z := DeskKit.zone(b, DeskKit.X_ID, 70.0, 520.0, 250.0, 1, "your standing",
		"the rate is derived line by line, never asserted")
	DeskKit.money_row(b, z, "era base", "6.0%/wk")
	DeskKit.money_row(b, z, "runway worry", "+1.2%", DeskKit.PEN)
	DeskKit.money_row(b, z, "revenue reassurance", "−0.8%", DeskKit.SAGE)
	var c1 := DeskKit.wall_column(b, 570.0, 70.0, 270.0, 500.0, "building",
		"progress + $/wk + odds")
	DeskKit.wall_card(b, c1, {"title": "the cold-start fix",
		"facts": ["$140/wk · 6 in 10"], "progress": 0.62})
	DeskKit.wall_card(b, c1, {"title": "night-mode billing",
		"facts": ["$90/wk · 8 in 10"], "progress": 0.31})
	var c2 := DeskKit.wall_column(b, 856.0, 70.0, 270.0, 500.0, "ready",
		"SHIP rolls the dice")
	DeskKit.wall_card(b, c2, {"title": "the booking suite",
		"facts": ["promised +8 keeps-them"], "ready": true})
	DeskKit.wall_card(b, c2, {"title": "creaky exporter", "facts": ["keep $12/wk"], "sev": 2})
	DeskKit.fold_row(b, DeskKit.X_ID, 380.0, 6, "solid features")
	await _shot("kit_zone_wall")

## TICKETS, OWNERSHIP, FACES: the receipt with its dashed head/foot and double
## price rule, capbars, the dilution story, the plate, hero rows, the folder,
## the chip set and the deadline clock chip.
func _instruments() -> void:
	var b := await _blank()
	b.label("tickets, ownership, faces", Vector2(DeskKit.X_ID, 6.0), DeskKit.TITLE)
	DeskKit.ticket(b, DeskKit.X_ID, 70.0, 420.0, {
		"title": "the pre-move receipt",
		"lines": [{"label": "June → Lyon, now", "value": "$400", "col": DeskKit.PEN},
			{"label": "Paris, during the ramp", "value": "−24 slots/wk"},
			{"label": "Lyon, after the ramp", "value": "+24 slots/wk", "col": DeskKit.SAGE}],
		"total_label": "the price of the move", "total_value": "$400 now",
		"foot": "two-tap confirms · Esc abandons"})
	DeskKit.capbars(b, 500.0, 84.0, 620.0, [
		{"label": "you", "pct": 54.0, "col": DeskKit.PEN, "note": "common"},
		{"label": "the seed round", "pct": 22.0, "col": DeskKit.SAGE, "note": "1x non-part."},
		{"label": "the pool", "pct": 10.0, "col": DeskKit.YELL, "note": "6.2% granted"}])
	DeskKit.dilution_bar(b, 500.0, 250.0, 620.0, [
		{"label": "wk 1 — founded", "pct": 100.0},
		{"label": "wk 18 — the SAFEs", "pct": 82.0, "note": "paper up"},
		{"label": "wk 30 — the pool", "pct": 68.0, "note": "the top-up dilutes YOU"},
		{"label": "wk 44 — the A", "pct": 54.0, "note": "$2.4M paper"}])
	var y := DeskKit.hero_plate(b, DeskKit.X_ID, 430.0, "Mossflow Core", "v0.62",
		"software for SMB — who it's for, in one line")
	y = DeskKit.hero_row(b, y + 8.0, {"name": "Paris studio",
		"facts": "capacity 210 · wanted 260 · $27 a session", "value": "margin $9", "sev": 2})
	y = DeskKit.hero_row(b, y, {"name": "Lyon studio",
		"facts": "capacity 140 · wanted 90 · $31 a session", "value": "margin $5"})
	DeskKit.folder(b, DeskKit.X_ID, y + 6.0, 340.0, "the booking suite",
		"4 features · $18/wk keep")
	var cx := DeskKit.X_ID + 380.0
	cx = DeskKit.chip(b, cx, y + 26.0, {"text": "June", "kind": "person", "selected": true})
	cx = DeskKit.chip(b, cx, y + 26.0, {"text": "the lathe", "kind": "machine"})
	cx = DeskKit.chip(b, cx, y + 26.0, {"text": "on-call rotation", "kind": "spend"})
	DeskKit.clock_chip(b, cx + 10.0, y + 30.0, "3 wks left")
	await _shot("kit_instruments")

# ═════════════════════════════════ the run ═══════════════════════════════════

func _go() -> void:
	await process_frame
	var d := OS.get_environment("RUNWAY_STRESS_DIR")
	if d != "":
		_dir = d
	await _object_fallback()
	await _object_portrait()
	await _frame_states()
	await _arrange_shell()
	await _momentary()
	await _tour_steps()
	await _grammar()
	await _shapes()
	await _levers()
	await _ledger()
	await _zone_wall()
	await _instruments()
	print("KIT SHOTS: %d captures -> %s" % [_shots.size(), _dir])
	quit(0)
