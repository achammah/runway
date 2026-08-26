class_name DeskPivot
extends RefCounted
## DESK — THE COMPANY · "pivot", the escape hatch. W2 lane: L-COMPANY.
## THE QUESTION THIS DESK ANSWERS: "what survives if we change course?"
## Spec: docs/design/DECISIONS.md § THE PIVOT (mechanics, binding) +
## docs/design/12-binder-rework-2.md § pivot (the four zones).
##
##   1 THE TWO DOORS   audience pivot / product pivot — each door lists its
##                     exact costs in its own words; debts survive BOTH,
##                     said on both. Pressing a door chooses it; the
##                     audience door then asks where you are going.
##   2 THE PREVIEW     computed from live state, never asserted: N customers,
##                     the well's $X, N named deals, v0.62 → v0.1, what
##                     regenerates. The product roll shows its honest RANGE —
##                     the die is cast at the press, not the preview.
##   3 THE WEEK AFTER  what the first post-pivot week looks like.
##   4 THE ARM         the word PIVOT typed + the two-tap. Esc keeps the
##                     company (desk-local state dies with the visit). The
##                     armed pivot resolves at the next LOCK IN
##                     (SimPivot.resolve_armed — the week-turn seam).
##
## Desk-local state: desk["mode"] = "" | "audience" | "product" (the chosen
## door — Esc pops it), desk["chip"] = the target, desk["typed"] = the unlock
## word as written. The ARMED intent itself is DURABLE (SimPivot flags).

const QUESTION := "what survives if we change course?"

static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	var a := SimPivot.armed(s)
	if not a.is_empty():
		return {"big": "ARMED", "line": "the %s pivot fires at the next LOCK IN"
			% String(a.get("kind", ""))}
	return {"big": "two doors",
		"line": "audience pivot · product pivot — the debts survive both"}

## What dies behind each door, in the door's own words (DECISIONS' exact lists).
const AUD_DIES: Array[String] = [
	"customers → 0 — traction starts over",
	"named deals and leads — dead with their market",
	"channel learning + the content well — drained",
	"the market re-fogs — your beliefs reset",
]
const AUD_LIVES := "survives: the product, as built · the team · the cash"
const PROD_DIES: Array[String] = [
	"customers — a 50–100% roll decides who walks",
	"the version → v0.1 — every advance dies",
	"bets and platform die · tech debt clears",
	"named deals knock back to the first meeting",
]
const PROD_LIVES := "survives: channel + sales learning · the well · the cash"
const DEBTS_LINE := "the debts survive. the bank does not forget."

static func draw(b) -> void:
	var s: GameState = b.state
	var a := SimPivot.armed(s)
	if not a.is_empty():
		_draw_armed(b, s, a)
		return

	var door := String(b.desk.get("mode", ""))
	var target := String(b.desk.get("chip", ""))

	# HERO
	var y := DeskKit.hero_band(b, "two doors",
		"the escape hatch — the money survives; what burns depends on the axis",
		DeskKit.INK)

	# 1 · THE TWO DOORS
	var z1 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, 296.0, 1, "the two doors",
		"each door lists its exact costs — pick one and the preview prices it")
	_door(b, s, z1, 0.0, "audience", door, target)
	_door(b, s, z1, 560.0, "product", door, target)
	y = float(z1.bottom) + 12.0

	# 2 · THE PREVIEW — computed, not asserted
	var z2 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, 188.0, 2, "the preview",
		"computed from the live books — nothing here is a guess")
	var py := float(z2.cursor) - 8.0
	if door == "":
		DeskKit.empty(b, Vector2(float(z2.content_x), py),
			"pick a door — the preview prices it against the live books.", "")
	else:
		var pv := SimPivot.preview(s, door)
		var lines := _preview_lines(s, door, pv)
		for ln in lines:
			var l: Dictionary = ln
			b.label(String(l.get("label", "")), Vector2(float(z2.content_x), py),
				DeskKit.DETAIL, Color(DeskKit.INK, 0.85), 700.0)
			var v: Label = b.label(String(l.get("value", "")),
				Vector2(float(z2.content_x), py), DeskKit.DETAIL,
				l.get("col", DeskKit.PEN), float(z2.money_x) - float(z2.content_x))
			v.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
			py += 26.0
	y = float(z2.bottom) + 12.0

	# 3 · THE WEEK AFTER
	var z3 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, 70.0, 3, "the week after", "")
	b.label("demand ramps from zero · the DM narrates the pivot week · new topics "
		+ "and paintings arrive, regenerated — the numbers never wait for art",
		Vector2(float(z3.content_x), float(z3.content_y) - 18.0), DeskKit.DETAIL,
		Color(DeskKit.INK, 0.7), 1070.0)
	y = float(z3.bottom) + 12.0

	# 4 · THE ARM — the typed word, then the two-tap
	var z4 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, 118.0, 4, "the arm",
		"type PIVOT, then press twice — Esc keeps the company")
	var ready := door != "" and (door != "audience" or target != "")
	var typed_ok := String(b.desk.get("typed", "")).strip_edges().to_upper() == "PIVOT"
	var ax := float(z4.content_x)
	var ay := float(z4.cursor) - 8.0
	if not ready:
		b.label("choose the door first" + (" — and where you are going"
			if door == "audience" and target == "" else ""),
			Vector2(ax, ay), DeskKit.DETAIL, Color(DeskKit.INK, 0.45), 700.0)
	else:
		_type_field(b, ax, ay)
		if typed_ok:
			var price := _arm_caption(s, door, target)
			DeskKit.arm(b, "pivot_fire", "arm the pivot — it fires at LOCK IN",
				price, Vector2(ax + 460.0, ay - 6.0), func() -> void:
					if door == "audience":
						SimPivot.arm_audience(b.state, target)
					else:
						SimPivot.arm_product(b.state, target), 620.0)
		else:
			b.label("the word unlocks the arm — deliberate, not accidental",
				Vector2(ax + 460.0, ay + 2.0), DeskKit.DETAIL,
				Color(DeskKit.INK, 0.45), 620.0)

	DeskKit.footer(b, {
		"computed": "",
		"rules": "pivot #%d would be this run's — rare, deliberate, dangerous · %s"
			% [s.pivots + 1, DEBTS_LINE], "rules_y": 856.0})

## One door: its costs in its own words, the survival line, the debts line —
## and, once chosen, the destination chips.
static func _door(b, s: GameState, z: Dictionary, dx: float, kind: String,
		door: String, target: String) -> void:
	var x := float(z.content_x) + dx
	var y := float(z.cursor) - 6.0
	var chosen := door == kind
	var title := "AUDIENCE PIVOT" if kind == "audience" else "PRODUCT PIVOT"
	b.label(title + ("  · chosen" if chosen else ""), Vector2(x, y), DeskKit.ROW,
		DeskKit.PEN if chosen else DeskKit.INK, 520.0)
	var dies: Array = AUD_DIES if kind == "audience" else PROD_DIES
	var ly := y + 38.0
	for d in dies:
		b.label("× " + String(d), Vector2(x, ly), DeskKit.DETAIL,
			Color(DeskKit.INK, 0.8), 530.0)
		ly += 24.0
	b.label(AUD_LIVES if kind == "audience" else PROD_LIVES, Vector2(x, ly),
		DeskKit.DETAIL, DeskKit.SAGE, 530.0)
	ly += 24.0
	b.label(DEBTS_LINE, Vector2(x, ly), DeskKit.DETAIL, DeskKit.PEN, 530.0)
	ly += 28.0
	# the destination chips, once this door is the chosen one
	if chosen:
		var cx := x
		if kind == "audience":
			for who in SimPivot.AUDIENCES:
				if String(who) == s.biz_who:
					continue
				var w := String(who)
				cx = DeskKit.chip(b, cx, ly, {"text": w, "kind": "person",
					"selected": target == w, "on_press": func() -> void:
						b.desk["chip"] = w})
		else:
			cx = DeskKit.chip(b, cx, ly, {"text": "same craft, reborn",
				"kind": "person", "selected": target == "", "on_press": func() -> void:
					b.desk["chip"] = ""})
			for what in SimPivot.CRAFTS:
				if String(what) == s.biz_what:
					continue
				var w2 := String(what)
				cx = DeskKit.chip(b, cx, ly, {"text": w2, "kind": "person",
					"selected": target == w2, "on_press": func() -> void:
						b.desk["chip"] = w2})
	else:
		# the whole half is the door's handle
		var hit := DeskKit.word(b, "", Vector2(x - 6.0, y - 4.0), func() -> void:
			b.desk["mode"] = kind
			b.desk.erase("chip"), DeskKit.DETAIL, DeskKit.INK, 540.0)
		hit.size = Vector2(544.0, 240.0)

## The preview rows: label left, the number right — money in columns.
static func _preview_lines(s: GameState, door: String, pv: Dictionary) -> Array:
	if door == "audience":
		return [
			{"label": "customers walk", "value": "all %d" % int(pv.get("customers_lost", 0))},
			{"label": "the content well drains", "value": "$%d" % int(pv.get("well", 0))},
			{"label": "named deals die", "value": str(int(pv.get("deals_dead", 0)))},
			{"label": "the debts stay owed", "value": "$%d" % int(pv.get("debts", 0)),
				"col": DeskKit.PEN},
		]
	return [
		{"label": "customers at the roll's mercy", "value": "50–100%% of %d"
			% int(pv.get("customers_at_risk", 0))},
		{"label": "the version", "value": "%s → %s" % [String(pv.get("version_from", "")),
			String(pv.get("version_to", ""))]},
		{"label": "bets die on the wall · debt clears · %d deals knock back"
			% int(pv.get("deals_knocked", 0)), "value": "%d bets · −%d debt"
			% [int(pv.get("bets_dead", 0)), int(pv.get("debt_cleared", 0))]},
		{"label": "the debts stay owed", "value": "$%d" % int(pv.get("debts", 0)),
			"col": DeskKit.PEN},
	]

## The armed caption — the second tap carries the full price in coral.
static func _arm_caption(s: GameState, door: String, target: String) -> String:
	if door == "audience":
		return "press again: %d customers → 0, the market dies — %s next" \
			% [s.traction, target]
	return "press again: the roll takes 50–100%% of %d customers" % s.traction

## THE ARMED STATE — the held breath between the arm and the LOCK IN.
static func _draw_armed(b, s: GameState, a: Dictionary) -> void:
	var kind := String(a.get("kind", ""))
	var target := String(a.get("target", ""))
	var y := DeskKit.hero_band(b, "ARMED",
		"the %s pivot fires at the next LOCK IN — disarm keeps the company"
		% kind, DeskKit.ALERT)
	var pv := SimPivot.preview(s, kind)
	var lines: Array = []
	for l in _preview_lines(s, kind, pv):
		var ld: Dictionary = l
		lines.append({"label": String(ld.get("label", "")),
			"value": String(ld.get("value", "")), "col": ld.get("col", DeskKit.INK)})
	y = DeskKit.ticket(b, DeskKit.X_ID, y + 6.0, 720.0, {
		"title": "what fires at LOCK IN" + ((" — toward " + target) if target != "" else ""),
		"lines": lines,
		"total_label": "the price in cash",
		"total_value": "$0 — the price is the traction",
		"foot": "the DM narrates the week it fires · new topics and paintings follow"})
	DeskKit.word(b, "disarm — keep the company", Vector2(DeskKit.X_ID, y + 10.0),
		func() -> void: SimPivot.disarm(b.state), DeskKit.ROW, DeskKit.INK, 520.0)
	DeskKit.footer(b, {
		"computed": "armed pivots read as a sev-3 alarm — the tab wears it until this fires",
		"rules": DEBTS_LINE, "y": 820.0, "rules_y": 852.0})

## The typed-word field: a bare LineEdit in the binder's own hand — the paper
## is the field. Text lives in desk["typed"]; crossing the PIVOT threshold
## refreshes once so the arm appears.
static func _type_field(b, x: float, y: float) -> void:
	var le := LineEdit.new()
	le.add_theme_font_override("font", b.font())
	le.add_theme_font_size_override("font_size", 28)
	le.add_theme_color_override("font_color", DeskKit.INK)
	le.add_theme_color_override("font_placeholder_color", Color(DeskKit.INK, 0.28))
	le.add_theme_color_override("caret_color", DeskKit.PEN)
	for st in ["normal", "focus", "read_only"]:
		le.add_theme_stylebox_override(st, StyleBoxEmpty.new())
	le.placeholder_text = "type PIVOT"
	le.text = String(b.desk.get("typed", ""))
	le.position = Vector2(x, y - 6.0)
	le.set_deferred("size", Vector2(300.0, 44.0))
	le.text_changed.connect(func(t: String) -> void:
		var was_ok := String(b.desk.get("typed", "")).strip_edges().to_upper() == "PIVOT"
		b.desk["typed"] = t
		var now_ok := t.strip_edges().to_upper() == "PIVOT"
		if was_ok != now_ok:
			b.refresh())
	b.pane().add_child(le)
	DeskKit.pen_rule(b, y + 34.0, x, 300.0, Color(DeskKit.PEN, 0.6), 3)

static func handle(b, id: String) -> void:
	match id:
		"door:audience":
			b.desk["mode"] = "audience"
			b.desk.erase("chip")
		"door:product":
			b.desk["mode"] = "product"
			b.desk.erase("chip")
		"disarm":
			SimPivot.disarm(b.state)
