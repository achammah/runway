class_name BookIntroScreen
extends Control
## THE FIRST PAGE OF THE BOOK (owner, replacing the old world-reveal sheet):
## the run opens the way a business memoir opens — the founder's own first
## entry, written that night, scrollable — and below it, FIELD NOTES: what
## the founder THINKS they know (working assumptions, not the hidden truth).
## SETTLE IN closes the book and the game begins; the entry never replays.

signal done

const CREAM := Color("F2EAD3")
const INK := Color("1E1E1E")
const PEN := Color("E86A5C")
const BLUE := Color("6E8CA0")
const HAND := "res://assets/fonts/PatrickHand-Regular.ttf"

var state: GameState
var _font: Font
var _scroll: ScrollContainer
var _col: VBoxContainer
var _entry_lbl: Label
var _waiting := true
var _notes_nodes: Array = []   # field notes, revealed only once the entry lands
var _t := 0.0
var _sbar: Control

func setup(p_state: GameState) -> void:
	state = p_state

## The founding entry arrives whenever the prefetch lands — before or after
## this screen opened. Until then the page says it is being written.
func feed_entry(text: String) -> void:
	_waiting = false
	if _entry_lbl != null and is_instance_valid(_entry_lbl):
		_entry_lbl.text = text
		_entry_lbl.add_theme_color_override("font_color", INK)
	for n in _notes_nodes:
		if is_instance_valid(n):
			n.visible = true

func _ready() -> void:
	_font = load(HAND)
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	set_process(true)
	var bg := ColorRect.new()
	bg.color = Color("22262B")
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(bg)
	var sheet := _Sheet.new()
	sheet.position = Vector2(168, 42)
	sheet.set_deferred("size", Vector2(1200, 916))
	sheet.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(sheet)

	var title := Label.new()
	title.add_theme_font_override("font", _font)
	title.add_theme_font_size_override("font_size", 42)
	title.add_theme_color_override("font_color", INK)
	title.text = "%s — a founder's logbook" % state.company_name.to_upper()
	title.position = Vector2(220, 76)
	title.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(title)
	var sub := Label.new()
	sub.add_theme_font_override("font", _font)
	sub.add_theme_font_size_override("font_size", 26)
	sub.add_theme_color_override("font_color", Color(INK, 0.55))
	sub.text = "entry one — the night the lease was signed"
	sub.position = Vector2(222, 132)
	sub.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(sub)

	_scroll = ScrollContainer.new()
	_scroll.position = Vector2(220, 182)
	_scroll.set_deferred("size", Vector2(1080, 660))
	_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	_scroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_SHOW_NEVER
	_scroll.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_scroll)
	_col = VBoxContainer.new()
	_col.add_theme_constant_override("separation", 18)
	_col.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_scroll.add_child(_col)

	_entry_lbl = _mk("the first entry is being written…", 30, Color(INK, 0.45))
	_col.add_child(_entry_lbl)

	# ── FIELD NOTES: the working assumptions, plainly marked as such.
	# They stay HIDDEN until the entry lands (owner: notes floating under a
	# "being written…" placeholder read as noise) — feed_entry reveals them.
	_notes_nodes.append(_rule())
	_col.add_child(_notes_nodes[-1])
	_notes_nodes.append(_mk("FIELD NOTES — what I think I know", 32, PEN))
	_col.add_child(_notes_nodes[-1])
	var th := state.theta
	var b_tam := int(state.beliefs.get("tam", th.get("tam", 0)))
	var b_life := int(state.beliefs.get("lifetime_wk", th.get("lifetime_wk", 40)))
	var mline := String(state.get_meta("market_line", ""))
	_notes_nodes.append(_mk("the market, as far as I can tell: ~%s people who might buy this. A customer probably stays ≈ %d weeks. All of this is a guess I will be correcting for months.%s" % [
		_fmt(b_tam), b_life, ("  (" + mline + ")") if mline != "" else ""], 27, Color(INK, 0.8)))
	_col.add_child(_notes_nodes[-1])
	_notes_nodes.append(_mk("the money in town:", 29, BLUE))
	_col.add_child(_notes_nodes[-1])
	for inv in state.investors:
		var d: Dictionary = inv
		_notes_nodes.append(_mk("%s — %s. \"%s\"" % [String(d.get("name", "?")),
			String(d.get("archetype", "")), String(d.get("thesis", ""))], 25, Color(INK, 0.7)))
		_col.add_child(_notes_nodes[-1])
	_notes_nodes.append(_mk("already selling to my customers:", 29, BLUE))
	_col.add_child(_notes_nodes[-1])
	for rv in state.rivals:
		var r: Dictionary = rv
		var what := String(r.get("what", ""))
		_notes_nodes.append(_mk("%s — looks %s%s" % [String(r.get("name", "?")),
			SimEngine._fuzz(float(r.get("strength", 20.0))),
			(". " + what) if what != "" else ""], 25, Color(INK, 0.7)))
		_col.add_child(_notes_nodes[-1])
	_notes_nodes.append(_mk("everything above is honest. none of it is verified.", 24, Color(INK, 0.45)))
	_col.add_child(_notes_nodes[-1])
	for n in _notes_nodes:
		n.visible = false

	# drawn scrollbar
	_sbar = _ScrollInk.new()
	_sbar.position = Vector2(1316, 182)
	_sbar.set_deferred("size", Vector2(18, 660))
	_sbar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_sbar)

	var go := Button.new()
	go.flat = true
	go.text = "SETTLE IN  →"
	go.add_theme_font_override("font", _font)
	go.add_theme_font_size_override("font_size", 40)
	go.add_theme_color_override("font_color", PEN)
	for stn in ["normal", "hover", "pressed", "focus"]:
		go.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	go.position = Vector2(1020, 878)
	go.set_deferred("size", Vector2(320, 64))
	go.pressed.connect(func() -> void:
		done.emit())
	add_child(go)

	gui_input.connect(func(ev: InputEvent) -> void:
		if ev is InputEventPanGesture:
			_scroll.scroll_vertical += int((ev as InputEventPanGesture).delta.y * 14.0)
		elif ev is InputEventMouseButton and ev.pressed:
			if (ev as InputEventMouseButton).button_index == MOUSE_BUTTON_WHEEL_UP:
				_scroll.scroll_vertical -= 64
			elif (ev as InputEventMouseButton).button_index == MOUSE_BUTTON_WHEEL_DOWN:
				_scroll.scroll_vertical += 64)

	modulate.a = 0.0
	create_tween().tween_property(self, "modulate:a", 1.0, 0.4)

func _process(delta: float) -> void:
	_t += delta
	if _waiting and _entry_lbl != null:
		_entry_lbl.modulate.a = 0.7 + 0.3 * sin(_t * 2.2)
	if _sbar != null and is_instance_valid(_sbar) and _scroll != null:
		var maxs := maxf(_col.size.y - _scroll.size.y, 0.0)
		_sbar.visible = maxs > 8.0
		_sbar.set("frac", clampf(float(_scroll.scroll_vertical) / maxf(maxs, 1.0), 0.0, 1.0))
		_sbar.set("thumb_frac", clampf(_scroll.size.y / maxf(_col.size.y, 1.0), 0.12, 1.0))
		_sbar.queue_redraw()

func _mk(t: String, sz: int, col: Color) -> Label:
	var l := Label.new()
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.add_theme_font_override("font", _font)
	l.add_theme_font_size_override("font_size", sz)
	l.add_theme_color_override("font_color", col)
	l.custom_minimum_size = Vector2(1060, 0)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	l.text = t
	return l

func _rule() -> Control:
	var r := Control.new()
	r.custom_minimum_size = Vector2(1060, 26)
	r.draw.connect(func() -> void:
		var rng := RandomNumberGenerator.new()
		rng.seed = 5
		var pts := PackedVector2Array()
		for i in 33:
			pts.append(Vector2(1060.0 * float(i) / 32.0, 13.0 + rng.randf_range(-1.5, 1.5)))
		r.draw_polyline(pts, Color(INK, 0.35), 3.0, true))
	return r

func _fmt(n: int) -> String:
	if n >= 1_000_000:
		return "%.1fM" % (float(n) / 1_000_000.0)
	if n >= 1_000:
		return "%dk" % int(n / 1000.0)
	return str(n)

class _ScrollInk:
	extends Control
	var frac := 0.0
	var thumb_frac := 0.3
	func _draw() -> void:
		draw_line(Vector2(size.x * 0.5, 4), Vector2(size.x * 0.5, size.y - 4),
				Color(0.12, 0.12, 0.12, 0.18), 3.0)
		var th := maxf(size.y * thumb_frac, 34.0)
		var ty := 4.0 + (size.y - 8.0 - th) * frac
		var rng := RandomNumberGenerator.new()
		rng.seed = 7
		var pts := PackedVector2Array()
		for i in 9:
			pts.append(Vector2(size.x * 0.5 + rng.randf_range(-1.4, 1.4), ty + th * float(i) / 8.0))
		draw_polyline(pts, Color("E86A5C", 0.85), 6.0, true)

class _Sheet:
	extends Control
	func _draw() -> void:
		draw_rect(Rect2(8, 12, size.x, size.y), Color(0, 0, 0, 0.3))
		draw_rect(Rect2(0, 0, size.x, size.y), BookIntroScreen.CREAM)
		var rng := RandomNumberGenerator.new()
		rng.seed = 3
		var pts := PackedVector2Array()
		var corners := [Vector2(3, 3), Vector2(size.x - 3, 3), Vector2(size.x - 3, size.y - 3), Vector2(3, size.y - 3)]
		for i in 4:
			var a: Vector2 = corners[i]
			var b: Vector2 = corners[(i + 1) % 4]
			for k in 16:
				pts.append(a.lerp(b, float(k) / 16.0) + Vector2(rng.randf_range(-2, 2), rng.randf_range(-2, 2)))
		pts.append(pts[0])
		draw_polyline(pts, BookIntroScreen.INK, 4.0, true)
