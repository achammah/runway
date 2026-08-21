class_name KeysScreen
extends Control
## THE ONE KEY (owner: OpenAI only, and SELL the why): the first-boot screen
## where the player hands the game its narrator. One drawn sheet, the pitch in
## plain words, one paste line, one button. Written to user://keys.env — never
## the project folder. "play keyless" stays: the authored deck still works.

signal saved

const CREAM := Color("F2EAD3")
const INK := Color("1E1E1E")
const PEN := Color("E86A5C")
const SAGE := Color("8FA582")

var _openai: PaperInput
var _font: Font
var _t := 0.0

func _ready() -> void:
	_font = load("res://assets/fonts/PatrickHand-Regular.ttf")
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	set_process(true)
	var bg := ColorRect.new()
	bg.color = Color("22262B")
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(bg)
	var sheet := _Sheet.new()
	sheet.position = Vector2(198, 60)
	sheet.set_deferred("size", Vector2(1140, 880))
	sheet.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(sheet)

	# the mascot vouches for the ask
	var mascot := TextureRect.new()
	if ResourceLoader.exists("res://assets/title/layers/founder.png"):
		mascot.texture = load("res://assets/title/layers/founder.png")
	mascot.position = Vector2(1040, 116)
	mascot.set_deferred("size", Vector2(240, 240))
	mascot.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	mascot.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	mascot.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(mascot)

	_ink("ONE KEY MAKES THE WORLD ALIVE", Vector2(250, 128), 48, INK)
	_rule(Vector2(252, 204), 560.0)
	_wrap(("RUNWAY! is a fully generative survival game. There is no script: "
		+ "your market, your rivals, your investors, every week's consequences "
		+ "and every picture of your office are invented on the spot, for this "
		+ "run only. Nobody else will ever play your company."),
		Vector2(252, 234), 30, Color(INK, 0.85), 740.0)
	_wrap(("The narrator behind all of that is OpenAI's model, and it works "
		+ "for you, on your own key."),
		Vector2(252, 420), 30, Color(INK, 0.85), 740.0)

	_openai = PaperInput.new()
	_openai.setup("PASTE YOUR OPENAI API KEY", "sk-…", 28)
	_openai.position = Vector2(252, 528)
	_openai.set_deferred("size", Vector2(1030, 112))
	add_child(_openai)

	_wrap("· stored only on this machine, in your user folder — never in the game, never sent anywhere but OpenAI",
		Vector2(256, 668), 24, Color(INK, 0.55), 1000.0)
	_wrap("· a typical evening of play costs about a coffee in API credit",
		Vector2(256, 708), 24, Color(INK, 0.55), 1000.0)
	_wrap("· get one at platform.openai.com → API keys", Vector2(256, 748), 24, Color(INK, 0.55), 1000.0)

	var save := Button.new()
	save.flat = true
	save.text = "BRING THE WORLD TO LIFE  →"
	save.add_theme_font_override("font", _font)
	save.add_theme_font_size_override("font_size", 38)
	save.add_theme_color_override("font_color", PEN)
	save.add_theme_color_override("font_hover_color", INK)
	for stn in ["normal", "hover", "pressed", "focus"]:
		save.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	save.position = Vector2(690, 830)
	save.set_deferred("size", Vector2(600, 70))
	save.pressed.connect(_save)
	add_child(save)

	var skip := Button.new()
	skip.flat = true
	skip.text = "play without — authored world only"
	skip.add_theme_font_override("font", _font)
	skip.add_theme_font_size_override("font_size", 24)
	skip.add_theme_color_override("font_color", Color(INK, 0.5))
	skip.add_theme_color_override("font_hover_color", PEN)
	for stn in ["normal", "hover", "pressed", "focus"]:
		skip.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	skip.position = Vector2(252, 840)
	skip.set_deferred("size", Vector2(420, 52))
	skip.pressed.connect(func() -> void:
		var f := FileAccess.open("user://keys.env", FileAccess.WRITE)
		f.store_string("# keyless by choice\n")
		f.close()
		saved.emit())
	add_child(skip)

	modulate.a = 0.0
	create_tween().tween_property(self, "modulate:a", 1.0, 0.35)

func _process(delta: float) -> void:
	_t += delta

func _save() -> void:
	var ok := _openai.value().strip_edges()
	if ok == "":
		_openai.setup("PASTE YOUR OPENAI API KEY — it looks like sk-…", "sk-…", 28)
		return
	var lines := PackedStringArray(["OPENAI_API_KEY=" + ok])
	# an Atlas key already present in a dev .env keeps working; never asked for
	var f := FileAccess.open("user://keys.env", FileAccess.WRITE)
	f.store_string("\n".join(lines) + "\n")
	f.close()
	saved.emit()

func _ink(t: String, pos: Vector2, sz: int, col: Color) -> void:
	var l := Label.new()
	l.text = t
	l.add_theme_font_override("font", _font)
	l.add_theme_font_size_override("font_size", sz)
	l.add_theme_color_override("font_color", col)
	l.position = pos
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(l)

func _wrap(t: String, pos: Vector2, sz: int, col: Color, w: float) -> void:
	var l := Label.new()
	l.text = t
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.custom_minimum_size = Vector2(w, 0)
	l.add_theme_font_override("font", _font)
	l.add_theme_font_size_override("font_size", sz)
	l.add_theme_color_override("font_color", col)
	l.position = pos
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(l)

func _rule(pos: Vector2, length: float) -> void:
	var r := Control.new()
	r.position = pos
	r.custom_minimum_size = Vector2(length, 12)
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	r.draw.connect(func() -> void:
		var rng := RandomNumberGenerator.new()
		rng.seed = 4
		var pts := PackedVector2Array()
		for i in 21:
			pts.append(Vector2(length * float(i) / 20.0, 5.0 + rng.randf_range(-1.5, 1.5)))
		r.draw_polyline(pts, PEN, 4.0, true))
	add_child(r)

class _Sheet:
	extends Control
	func _draw() -> void:
		draw_rect(Rect2(8, 12, size.x, size.y), Color(0, 0, 0, 0.3))
		draw_rect(Rect2(0, 0, size.x, size.y), KeysScreen.CREAM)
		var rng := RandomNumberGenerator.new()
		rng.seed = 6
		var pts := PackedVector2Array()
		var cs := [Vector2(3, 3), Vector2(size.x - 3, 3), size - Vector2(3, 3), Vector2(3, size.y - 3)]
		for i in 4:
			var a: Vector2 = cs[i]
			var b: Vector2 = cs[(i + 1) % 4]
			for k in 16:
				pts.append(a.lerp(b, float(k) / 16.0) + Vector2(rng.randf_range(-2, 2), rng.randf_range(-2, 2)))
		pts.append(pts[0])
		draw_polyline(pts, KeysScreen.INK, 4.0, true)
