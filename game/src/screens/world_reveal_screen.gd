class_name WorldRevealScreen
extends Control
## THE MARKET MAP — shown ONCE, after the papers are signed and before day one
## (plan UI-15): the generated world laid out on one drawn sheet, so the run
## feels authored before week one. The funds' public theses show; their secrets
## stay in the engine.

signal done

const CREAM := Color("F2EAD3")
const INK := Color("1E1E1E")
const PEN := Color("E86A5C")
const SAGE := Color("8FA582")
const BLUE := Color("6E8CA0")
const HAND := "res://assets/fonts/PatrickHand-Regular.ttf"

var state: GameState
var _font: Font

func setup(p_state: GameState) -> void:
	state = p_state

func _ready() -> void:
	_font = load(HAND)
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	var bg := ColorRect.new()
	bg.color = Color("2C3238")
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(bg)

	var sheet := _Sheet.new()
	sheet.position = Vector2(168, 60)
	sheet.set_deferred("size", Vector2(1200, 880))
	sheet.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(sheet)

	_title("THE WORLD %s WAS BORN INTO" % state.company_name.to_upper(), Vector2(220, 96), 44)
	var th := state.theta
	var mline := String(state.get_meta("market_line", ""))
	_line("~%s real buyers · a customer stays ≈ %d weeks%s" % [
		_fmt(int(th.get("tam", 0))), int(th.get("lifetime_wk", 40)),
		("  ·  " + mline) if mline != "" else ""], Vector2(220, 168), 28)

	_title("the money in town", Vector2(220, 240), 34, PEN)
	var y := 292.0
	for inv in state.investors:
		var d: Dictionary = inv
		_line("%s — %s" % [String(d.get("name", "?")), String(d.get("archetype", ""))],
			Vector2(240, y), 29)
		_line("\"%s\"" % String(d.get("thesis", "")), Vector2(268, y + 38), 25, Color(INK, 0.65))
		y += 88.0

	_title("already on the street", Vector2(220, y + 24), 34, BLUE)
	y += 78.0
	for rv in state.rivals:
		var r: Dictionary = rv
		var what := String(r.get("what", ""))
		_line("%s — %s%s" % [String(r.get("name", "?")),
			SimEngine._fuzz(float(r.get("strength", 20.0))),
			("  ·  " + what) if what != "" else ""], Vector2(240, y), 27)
		y += 54.0

	_line("everything above is true. nothing above is the whole truth.",
		Vector2(220, y + 34), 25, Color(INK, 0.5))

	var go := Button.new()
	go.flat = true
	go.text = "SETTLE IN  →"
	go.add_theme_font_override("font", _font)
	go.add_theme_font_size_override("font_size", 40)
	go.add_theme_color_override("font_color", PEN)
	for stn in ["normal", "hover", "pressed", "focus"]:
		go.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	go.position = Vector2(1000, 860)
	go.set_deferred("size", Vector2(320, 70))
	go.pressed.connect(func() -> void:
		done.emit()
		queue_free())
	add_child(go)

	modulate.a = 0.0
	create_tween().tween_property(self, "modulate:a", 1.0, 0.4)

func _title(t: String, pos: Vector2, sz: int, col: Color = INK) -> void:
	_line(t, pos, sz, col)

func _line(t: String, pos: Vector2, sz: int, col: Color = INK) -> void:
	var l := Label.new()
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.add_theme_font_override("font", _font)
	l.add_theme_font_size_override("font_size", sz)
	l.add_theme_color_override("font_color", col)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	l.text = t
	l.position = pos
	l.custom_minimum_size = Vector2(1100, 0)
	add_child(l)

func _fmt(n: int) -> String:
	if n >= 1_000_000:
		return "%.1fM" % (float(n) / 1_000_000.0)
	if n >= 1_000:
		return "%dk" % int(n / 1000.0)
	return str(n)

class _Sheet:
	extends Control
	func _draw() -> void:
		var w := size.x
		var h := size.y
		draw_rect(Rect2(8, 12, w, h), Color(0, 0, 0, 0.3))
		draw_rect(Rect2(0, 0, w, h), WorldRevealScreen.CREAM)
		var rng := RandomNumberGenerator.new()
		rng.seed = 3
		var pts := PackedVector2Array()
		var corners := [Vector2(3, 3), Vector2(w - 3, 3), Vector2(w - 3, h - 3), Vector2(3, h - 3)]
		for i in 4:
			var a: Vector2 = corners[i]
			var b: Vector2 = corners[(i + 1) % 4]
			for k in 16:
				pts.append(a.lerp(b, float(k) / 16.0) + Vector2(rng.randf_range(-2, 2), rng.randf_range(-2, 2)))
		pts.append(pts[0])
		draw_polyline(pts, WorldRevealScreen.INK, 4.0, true)
