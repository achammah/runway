class_name Binder
extends Control
## THE OPERATIONS BINDER — the founder's dashboard, in the game's own hand
## (docs/DND_STARTUP_PLAN.md, UI item 1-7). Never a SaaS panel: a clipboard
## sheet over the dimmed room, ten pen-labelled tabs, doodle icons, charts
## drawn as wobbly polylines.
##
## FOG OF WAR: precision follows state.analytics_level (0-3). At 0 the customer
## page says "traffic seems decent"; invest in analytics (a writable move) and
## the pages sharpen. The dashboard you EARN is a mechanic, not a view.
##
## THE SHEET IS THE FRAME, THE DESKS ARE THE PAGES. Vitals, the ledger and
## threats are drawn here; every other tab body lives in its own file under
## ui/desks/ and is handed this node (docs/design/HOOKS.md), so a subsystem
## grows its page without ever opening this file. Desks draw through the public
## helpers below — `label`, `ink_btn`, `spark`, `fmt`, `wrap_h`, `pane` — and
## share the drawn components in ui/components.gd (DeskKit).
##
## Usage:  var b := Binder.new(); b.setup(state); add_child(b)
##         b.closed.connect(...)  — TAB/B/Esc or the close corner dismisses it.

signal closed

const CREAM := Color("F2EAD3")
const INK := Color("1E1E1E")
const PEN := Color("E86A5C")
const SAGE := Color("8FA582")
const YELL := Color("F4B942")
const BLUE := Color("6E8CA0")
const HAND := "res://assets/fonts/PatrickHand-Regular.ttf"

## TEN TABS AT PITCH 120 (docs/design/00-spine.md §10, DECISIONS.md #1): the
## sheet is 1240 wide, 24 + 10×120 = 1224 fits, buttons are 118×44 and the
## longest label ("the street") measures ≈110px at 23px in the hand. THE PEN
## RING AND THE BANGS READ THESE CONSTANTS TOO — the ring desynced from the
## button row twice when a pitch was re-typed somewhere else.
const TABS := ["vitals", "the ledger", "the bank", "pricing", "customers",
	"product", "crew", "cap table", "the street", "threats"]
const TAB_X0 := 24.0
const TAB_PITCH := 120.0
const TAB_W := 118.0
const TAB_H := 44.0

var state: GameState
var generator: EventGenerator = null   # the street's pricing road (01 WAIT state)
## DESK-LOCAL STATE, one visit long (docs/design/10-interface-language.md §4.8):
## a desk's page mode, its expanded row, its armed control. Never saved, cleared
## on every tab change, dead with this node — so reopening the binder is always
## a clean read of state and no half-finished act survives a close.
var desk := {}
var _bangs := {}   # tab name → the coral ! while that desk needs attention
var _font: Font
var _tab := 0
var _sheet: Control
var _content: Control

func setup(p_state: GameState, p_gen: EventGenerator = null) -> void:
	generator = p_gen
	state = p_state

func _ready() -> void:
	_font = load(HAND)
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	var dim := ColorRect.new()
	dim.color = Color(0.05, 0.05, 0.06, 0.55)
	dim.set_anchors_preset(Control.PRESET_FULL_RECT)
	dim.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(dim)

	_sheet = _Clipboard.new()
	_sheet.position = Vector2(148, 52)
	_sheet.set_deferred("size", Vector2(1240, 920))
	_sheet.mouse_filter = Control.MOUSE_FILTER_STOP
	add_child(_sheet)

	_content = Control.new()
	_content.position = Vector2(40, 118)
	_content.set_deferred("size", Vector2(1160, 760))
	_content.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_sheet.add_child(_content)

	# tabs: pen labels across the top; the active one gets the ring
	for i in TABS.size():
		var b := Button.new()
		b.flat = true
		b.text = TABS[i]
		b.add_theme_font_override("font", _font)
		b.add_theme_font_size_override("font_size", 23)
		b.add_theme_color_override("font_color", INK)
		b.add_theme_color_override("font_hover_color", PEN)
		for stn in ["normal", "hover", "pressed", "focus"]:
			b.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
		b.position = Vector2(TAB_X0 + i * TAB_PITCH, 54)
		b.set_deferred("size", Vector2(TAB_W, TAB_H))
		var idx := i
		b.pressed.connect(func() -> void:
			if _tab != idx:
				desk.clear()   # a desk's page mode dies when you leave the page
			_tab = idx
			_refresh())
		_sheet.add_child(b)
		# THE WARNING BANGS (owner: "! warnings on tab where things are unset").
		# EVERY tab carries one now: the engine's attention registry decides
		# which ones light up (docs/design/00-spine.md §4), so a desk that grows
		# a new warning needs no change here — it files a registry row instead.
		# The bang hangs on the tab's own shoulder, DERIVED from the tab width,
		# so the row and its marks can never drift apart again.
		var bang := Label.new()
		bang.text = "!"
		bang.add_theme_font_override("font", _font)
		bang.add_theme_font_size_override("font_size", 30)
		bang.add_theme_color_override("font_color", PEN)
		bang.position = b.position + Vector2(TAB_W - 27.0, -12.0)
		bang.mouse_filter = Control.MOUSE_FILTER_IGNORE
		bang.visible = false
		_sheet.add_child(bang)
		_bangs[TABS[i]] = bang

	var close := Button.new()
	close.flat = true
	close.text = "×"
	close.add_theme_font_override("font", _font)
	close.add_theme_font_size_override("font_size", 46)
	close.add_theme_color_override("font_color", PEN)
	for stn2 in ["normal", "hover", "pressed", "focus"]:
		close.add_theme_stylebox_override(stn2, StyleBoxEmpty.new())
	close.position = Vector2(1180, 8)
	close.set_deferred("size", Vector2(52, 52))
	close.pressed.connect(func() -> void: _dismiss())
	_sheet.add_child(close)

	gui_input.connect(func(ev: InputEvent) -> void:
		if ev is InputEventMouseButton and ev.pressed:
			_dismiss())
	_refresh()

## ESC POPS BEFORE IT CLOSES (docs/design/10-interface-language.md §4.2): inside
## a desk's state machine Esc walks DETAIL/WRITE/WAIT/REVIEW back to the list and
## disarms an armed control; only from a tab's base state does it shut the binder.
func _unhandled_key_input(ev: InputEvent) -> void:
	if ev is InputEventKey and ev.pressed and ev.keycode in [KEY_ESCAPE, KEY_TAB, KEY_B]:
		accept_event()
		if ev.keycode == KEY_ESCAPE and _desk_pop():
			return
		_dismiss()

## One step back inside the current desk. True = something was popped, so the
## press is spent and the binder stays open.
func _desk_pop() -> bool:
	if desk.has("armed"):
		desk.erase("armed")
		_refresh()
		return true
	if String(desk.get("mode", "")) != "":
		desk["mode"] = ""
		desk.erase("row")
		_refresh()
		return true
	return false

func _dismiss() -> void:
	closed.emit()
	queue_free()

# ─────────────────────────────── composition ────────────────────────────────
func _refresh() -> void:
	# ONE list behind every mark on this sheet: a tab wears the bang of its
	# loudest attention item, and an alarm (3) is coral where a note (1) is ink.
	var worst := {}
	for it in SimEngine.attention_items(state):
		var dsk := String((it as Dictionary).get("desk", ""))
		worst[dsk] = maxi(int(worst.get(dsk, 0)), int((it as Dictionary).get("severity", 1)))
	for tab_name in _bangs:
		var sev := int(worst.get(String(tab_name), 0))
		var lbl := _bangs[tab_name] as Label
		lbl.visible = sev > 0
		lbl.add_theme_color_override("font_color", INK if sev == 1 else PEN)
	for c in _content.get_children():
		c.queue_free()
	(_sheet as _Clipboard).active_tab = _tab
	_sheet.queue_redraw()
	# THE DESK DISPATCH (docs/design/HOOKS.md): a tab a subsystem owns is drawn by
	# its own desk file, handed this node. Vitals, the ledger and threats are the
	# frame's own pages and stay here.
	match _tab:
		0: _tab_vitals()
		1: DeskLedger.draw(self)
		2: DeskBank.draw(self)
		3: DeskCatalog.draw(self)
		4: DeskCustomers.draw(self)
		5: DeskProduct.draw(self)
		6: DeskCrew.draw(self)
		7: DeskCap.draw(self)
		8: DeskStreet.draw(self)
		9: _tab_threats()

## THE PRESS ROUTER: a desk that prefers id-dispatch to closures registers its
## controls against an id and answers in its own `handle()`. The tab rebuilds
## afterwards, so a handler only ever has to write state.
func desk_press(desk_name: String, id: String) -> void:
	match desk_name:
		"catalog": DeskCatalog.handle(self, id)
		"crew": DeskCrew.handle(self, id)
		"street": DeskStreet.handle(self, id)
		"customers": DeskCustomers.handle(self, id)
		"pipeline": DeskPipeline.handle(self, id)
		"bank": DeskBank.handle(self, id)
		"product": DeskProduct.handle(self, id)
		"factory": DeskFactory.handle(self, id)
		"cap": DeskCap.handle(self, id)
	_refresh()

## Open the binder ON a desk. The pre-roll review's "go fix it" lands here with
## the loudest attention row's own desk name, so the founder arrives looking at
## the thing the world stopped them for.
func focus_desk(desk_name: String) -> void:
	var i := TABS.find(desk_name)
	if i < 0:
		return
	desk.clear()
	_tab = i
	if _content != null:
		_refresh()

# ───────────────────────── what a desk may touch ─────────────────────────────
## The public half of this node: the drawing hand every desk file and the shared
## component kit draw through. The private twins below stay for this file's own
## pages — one implementation, two names, no second way to draw a label.

func pane() -> Control:
	return _content

func font() -> Font:
	return _font

func label(text: String, pos: Vector2, sz: int = 30, col: Color = INK, w: float = 1100.0) -> Label:
	return _label(text, pos, sz, col, w)

func ink_btn(btn: Button) -> void:
	_ink_btn(btn)

func icon(name: String, pos: Vector2, side: float = 72.0) -> void:
	_icon(name, pos, side)

func spark(series_v: Array, pos: Vector2, size_v: Vector2, col: Color) -> void:
	_spark(series_v, pos, size_v, col)

func series(key: String) -> Array:
	return _series(key)

func fmt(n: int) -> String:
	return _fmt(n)

func wrap_h(text: String, sz: int, w: float) -> float:
	return _wrap_h(text, sz, w)

func refresh() -> void:
	_refresh()

## THE VESSEL: a jar with a level — product's tech debt, and any "how full is it"
## read a desk needs. Ink outline round the whole height or it is not a jar.
func debt_jar(fill: float, pos: Vector2, size_v: Vector2) -> void:
	var jar := _DebtJar.new()
	jar.fill = fill
	jar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	jar.position = pos
	jar.set_deferred("size", size_v)
	_content.add_child(jar)

## THE WHEEL: slices at 0.75α under a 4px ink rim, names hung round the arc's
## middle. `slices` = [{pct, col, label}]. Radial means share-of-whole, nothing
## else — a percentage that is not a slice of something is a word on a line.
func pie(slices: Array, pos: Vector2, side: float) -> void:
	var p := _Pie.new()
	p.slices = slices
	p.font = _font
	p.mouse_filter = Control.MOUSE_FILTER_IGNORE
	p.position = pos
	p.set_deferred("size", Vector2(side, side))
	_content.add_child(p)

func _label(text: String, pos: Vector2, sz: int = 30, col: Color = INK, w: float = 1100.0) -> Label:
	var l := Label.new()
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.add_theme_font_override("font", _font)
	l.add_theme_font_size_override("font_size", sz)
	l.add_theme_color_override("font_color", col)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	l.text = text
	l.position = pos
	l.custom_minimum_size = Vector2(w, 0)
	_content.add_child(l)
	return l

func _icon(name: String, pos: Vector2, side: float = 72.0) -> void:
	var p := "res://assets/journal_icons/%s.png" % name
	if not ResourceLoader.exists(p):
		return
	var tr := TextureRect.new()
	tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	tr.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tr.texture = load(p)
	tr.position = pos
	tr.set_deferred("size", Vector2(side, side))
	_content.add_child(tr)

func _spark(series_v: Array, pos: Vector2, size_v: Vector2, col: Color) -> void:
	var sp := _Spark.new()
	sp.series = series_v
	sp.col = col
	sp.mouse_filter = Control.MOUSE_FILTER_IGNORE
	sp.position = pos
	sp.set_deferred("size", size_v)
	_content.add_child(sp)

func _series(key: String) -> Array:
	var out: Array = []
	for m in state.metric_history:
		out.append(float((m as Dictionary).get(key, 0)))
	return out

func _fmt(n: int) -> String:
	var s := str(absi(n))
	var out := ""
	while s.length() > 3:
		out = "," + s.substr(s.length() - 3) + out
		s = s.substr(0, s.length() - 3)
	return ("-" if n < 0 else "") + s + out

# ── tab 0: vitals ────────────────────────────────────────────────────────────
func _tab_vitals() -> void:
	_icon("cash", Vector2(10, 6))
	_label("$%s in the bank" % _fmt(state.cash), Vector2(100, 10), 46)
	_label(SimEngine.health_band(state), Vector2(100, 66), 30,
		PEN if SimEngine.runway_weeks(state) <= 10 else SAGE)
	_label("cash, drawn weekly:", Vector2(10, 140), 24, Color(INK, 0.6))
	_spark(_series("cash"), Vector2(10, 172), Vector2(1120, 190), BLUE)
	var last: Dictionary = state.metric_history[-1] if state.metric_history.size() > 0 else {}
	_label("last week: $%s in · $%s out" % [_fmt(int(last.get("revenue", 0))), _fmt(int(last.get("burn", 0)))],
		Vector2(10, 386), 30)
	var payroll := 0
	for e in state.employees:
		payroll += int(e.get("salary", 0))
	# the marketing number is the CHANNEL SUM now: the single legacy field goes
	# stale the moment the ledger's four lanes carry the spend (04 §6.2)
	var mk_pnl: Dictionary = state.get_meta("pnl", {})
	var mk_burn := int(mk_pnl.get("marketing", int(SimFunnel.spend_total(state))))
	# ONE HONEST DEBT FIGURE across shark, bank and venture notes (06 §9): the
	# single `loan_principal` field stopped being the whole story the week the
	# structured notes landed, and a founder must never read a debt-free line
	# with a bank note on the books.
	var debt_owed := SimBank.debt_total(state)
	var note_count := state.loans.size() + (1 if state.loan_principal > 0 else 0)
	_label("burn: rent $%s · payroll $%s · marketing $%s%s" % [
		_fmt(int(GameState.ERA_RENT.get(state.era, 150))), _fmt(payroll),
		_fmt(mk_burn),
		("  ·  DEBT $%s across %d notes (worst %d%%/wk)" % [_fmt(debt_owed), note_count,
			int(round(SimBank.worst_rate(state) * 100.0))]) if debt_owed > 0 else ""],
		Vector2(10, 432), 27, Color(INK, 0.8))
	_label("valuation, if anyone asked: $%s" % _fmt(SimEngine.valuation(state)), Vector2(10, 486), 30)
	# the hype chart moved here when the roadmap took the product sheet (07)
	_label("hype:", Vector2(10, 556), 24, Color(INK, 0.6))
	_spark(_series("hype"), Vector2(10, 580), Vector2(1120, 120), YELL)
	_label("price ×%.2f  ·  the market is %s" % [state.price_mult,
		"warm" if state.market_trend > 1.05 else ("cold" if state.market_trend < 0.95 else "even")],
		Vector2(10, 532), 27, Color(INK, 0.8))

func _ink_btn(btn: Button) -> void:
	btn.flat = true
	btn.add_theme_font_override("font", _font)
	btn.add_theme_font_size_override("font_size", 40)
	btn.add_theme_color_override("font_color", INK)
	btn.add_theme_color_override("font_hover_color", PEN)
	for stn in ["normal", "hover", "pressed", "focus"]:
		btn.add_theme_stylebox_override(stn, StyleBoxEmpty.new())

## Wrapped text is MEASURED, never assumed one line — fixed steps stacked the
## street on itself the first week a thesis wrapped (owner photo).
func _wrap_h(text: String, sz: int, w: float) -> float:
	return _font.get_multiline_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, w, sz).y

# ── tab 9: threats & promises ────────────────────────────────────────────────
func _tab_threats() -> void:
	_label("threats & promises", Vector2(10, 6), 40)
	var y := 80.0
	# WHAT NEEDS A HAND, in one place (docs/design/00-spine.md §4/§11): every
	# attention item at warn or above, loudest first. This is the same list the
	# tab bangs, the garage badge and the pre-roll review read — so a desk that
	# is shouting can never be shouting only somewhere the player is not looking.
	var wants := SimEngine.preroll_items(state)
	if not wants.is_empty():
		var shown := 0
		for it in wants:
			if shown >= 12:
				_label("+%d more — the desks have the details" % (wants.size() - shown),
					Vector2(10, y), 26, Color(INK, 0.6))
				y += 44.0
				break
			var itd: Dictionary = it
			_label("! %s  ·  %s" % [String(itd.get("label", "")), String(itd.get("desk", ""))],
				Vector2(10, y), 28, PEN if int(itd.get("severity", 2)) >= 3 else Color(INK, 0.85))
			y += 44.0
			shown += 1
		y += 12.0
	if state.clocks.is_empty() and state.statuses.is_empty() and state.commitments.is_empty():
		_label("nothing ticking. that never lasts.", Vector2(10, y), 30, Color(INK, 0.6))
	for c in state.clocks:
		var cd: Dictionary = c
		_label("⏰ in %d wks: %s" % [int(cd.get("weeks_left", 0)), String(cd.get("consequence", ""))],
			Vector2(10, y), 30, PEN)
		y += 52.0
	for s in state.statuses:
		var sd: Dictionary = s
		var kind := String(SimEngine.STATUS.get(String(sd.get("name", "")), {}).get("kind", "condition"))
		_label("%s %s — %d wks left" % ["▲" if kind == "buff" else "▼",
			String(sd.get("name", "")).replace("_", " "), int(sd.get("weeks_left", 0))],
			Vector2(10, y), 30, SAGE if kind == "buff" else PEN)
		y += 52.0
	for cm in state.commitments:
		var cmd: Dictionary = cm
		_label("↻ %s: $%d/wk for %d more wks" % [String(cmd.get("name", "")),
			int(cmd.get("cash_wk", 0)), int(cmd.get("weeks_left", 0))], Vector2(10, y), 30, BLUE)
		y += 52.0

# ─────────────────────────────── drawn pieces ───────────────────────────────
class _Clipboard:
	extends Control
	var active_tab := 0
	func _draw() -> void:
		var w := size.x
		var h := size.y
		draw_rect(Rect2(8, 12, w, h), Color(0, 0, 0, 0.25))
		draw_rect(Rect2(0, 0, w, h), Binder.CREAM)
		var rng := RandomNumberGenerator.new()
		rng.seed = 7
		var pts := PackedVector2Array()
		var corners := [Vector2(3, 3), Vector2(w - 3, 3), Vector2(w - 3, h - 3), Vector2(3, h - 3)]
		for i in 4:
			var a: Vector2 = corners[i]
			var b: Vector2 = corners[(i + 1) % 4]
			for k in 18:
				pts.append(a.lerp(b, float(k) / 18.0) + Vector2(rng.randf_range(-2, 2), rng.randf_range(-2, 2)))
		pts.append(pts[0])
		draw_polyline(pts, Binder.INK, 4.0, true)
		# the clip at the top
		draw_rect(Rect2(w * 0.5 - 70, -18, 140, 34), Binder.YELL)
		draw_rect(Rect2(w * 0.5 - 70, -18, 140, 34), Binder.INK, false, 4.0)
		# the pen ring around the active tab
		# TEN tabs at 120px pitch since the bank arrived. THE RING READS THE
		# BUTTON ROW'S OWN CONSTANTS — it circled the gap, then thin air, then
		# (this wave, caught by the tab shot) the tab one along, every time a
		# pitch was re-typed here instead of read from up there.
		var tx := Binder.TAB_X0 + float(active_tab) * Binder.TAB_PITCH + Binder.TAB_W * 0.5
		var ring := PackedVector2Array()
		for i in 33:
			var t := TAU * float(i) / 32.0
			ring.append(Vector2(tx + cos(t) * 62.0, 76.0 + sin(t) * 26.0)
				+ Vector2(rng.randf_range(-2, 2), rng.randf_range(-2, 2)))
		draw_polyline(ring, Binder.PEN, 3.5, true)
		# a rule under the tab row
		draw_line(Vector2(30, 108), Vector2(w - 30, 108), Color(Binder.INK, 0.25), 2.0)

class _Spark:
	extends Control
	var series: Array = []
	var col := Color("6E8CA0")
	func _draw() -> void:
		draw_rect(Rect2(Vector2.ZERO, size), Color(0, 0, 0, 0.03))
		if series.size() < 2:
			var f: Font = load(Binder.HAND)
			draw_string(f, Vector2(12, size.y * 0.55), "not enough weeks on record yet",
				HORIZONTAL_ALIGNMENT_LEFT, -1, 24, Color(Binder.INK, 0.4))
			return
		var lo := 1e18
		var hi := -1e18
		for v in series:
			lo = minf(lo, float(v))
			hi = maxf(hi, float(v))
		if hi - lo < 1.0:
			hi = lo + 1.0
		var pts := PackedVector2Array()
		var rng := RandomNumberGenerator.new()
		rng.seed = 13
		for i in series.size():
			var x := 8.0 + (size.x - 16.0) * float(i) / float(series.size() - 1)
			var y := size.y - 10.0 - (size.y - 24.0) * (float(series[i]) - lo) / (hi - lo)
			pts.append(Vector2(x, y + rng.randf_range(-1.0, 1.0)))
		draw_polyline(pts, col, 4.0, true)
		draw_circle(pts[pts.size() - 1], 6.0, Binder.PEN)
		var f2: Font = load(Binder.HAND)
		draw_string(f2, Vector2(8, 22), _fmt_s(hi), HORIZONTAL_ALIGNMENT_LEFT, -1, 20, Color(Binder.INK, 0.45))
		draw_string(f2, Vector2(8, size.y - 4), _fmt_s(lo), HORIZONTAL_ALIGNMENT_LEFT, -1, 20, Color(Binder.INK, 0.45))
	func _fmt_s(v: float) -> String:
		if absf(v) >= 1_000_000.0:
			return "%.1fM" % (v / 1_000_000.0)
		if absf(v) >= 1_000.0:
			return "%.0fk" % (v / 1_000.0)
		return "%.0f" % v

class _DebtJar:
	extends Control
	var fill := 0.3
	func _draw() -> void:
		var w := size.x
		var h := size.y
		draw_rect(Rect2(6, 10, w - 12, h - 14), Color(0, 0, 0, 0.04))
		var lv := clampf(fill, 0.0, 1.0)
		draw_rect(Rect2(8, 10 + (h - 16) * (1.0 - lv), w - 16, (h - 16) * lv),
			Color(Binder.PEN, 0.55))
		draw_rect(Rect2(6, 10, w - 12, h - 14), Binder.INK, false, 4.0)
		draw_line(Vector2(2, 10), Vector2(w - 2, 10), Binder.INK, 5.0)

class _Pie:
	extends Control
	var slices: Array = []
	var font: Font
	func _draw() -> void:
		var c := size * 0.5
		var r := minf(size.x, size.y) * 0.38
		var a0 := -PI / 2.0
		for s in slices:
			var d: Dictionary = s
			var frac := clampf(float(d.get("pct", 0.0)) / 100.0, 0.0, 1.0)
			if frac <= 0.001:
				continue
			var a1 := a0 + TAU * frac
			var pts := PackedVector2Array([c])
			var steps := maxi(int(frac * 48.0), 2)
			for i in steps + 1:
				var t := a0 + (a1 - a0) * float(i) / float(steps)
				pts.append(c + Vector2(cos(t), sin(t)) * r)
			draw_colored_polygon(pts, Color(d.get("col", Binder.SAGE), 0.75))
			a0 = a1
		draw_arc(c, r, 0, TAU, 64, Binder.INK, 4.0, true)
		# labels around the wheel
		a0 = -PI / 2.0
		for s2 in slices:
			var d2: Dictionary = s2
			var frac2 := clampf(float(d2.get("pct", 0.0)) / 100.0, 0.0, 1.0)
			if frac2 <= 0.01:
				continue
			var mid := a0 + TAU * frac2 * 0.5
			var p := c + Vector2(cos(mid), sin(mid)) * (r + 40.0)
			draw_string(font, p - Vector2(46, -8), String(d2.get("label", "")),
				HORIZONTAL_ALIGNMENT_LEFT, -1, 24, Binder.INK)
			a0 += TAU * frac2
