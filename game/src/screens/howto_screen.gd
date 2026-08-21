class_name HowToScreen
extends Control
## HOW THIS WORLD WORKS (owner: a mechanics screen at first start, with
## illustrations): four drawn panels — the muscles, the die, the verdict,
## the money — each one the REAL rule the engine runs, in plain words.
## Shown once (user://seen_howto), then GOT IT.

signal done

const CREAM := Color("F2EAD3")
const INK := Color("1E1E1E")
const PEN := Color("E86A5C")
const SAGE := Color("8FA582")
const YELL := Color("F4B942")

var _font: Font

func _ready() -> void:
	_font = load("res://assets/fonts/PatrickHand-Regular.ttf")
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	var bg := ColorRect.new()
	bg.color = Color("22262B")
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(bg)
	var art := _Art.new()
	art.set_anchors_preset(Control.PRESET_FULL_RECT)
	art.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(art)

	var go := Button.new()
	go.flat = true
	go.text = "GOT IT — LET'S FOUND SOMETHING  →"
	go.add_theme_font_override("font", _font)
	go.add_theme_font_size_override("font_size", 36)
	go.add_theme_color_override("font_color", PEN)
	for stn in ["normal", "hover", "pressed", "focus"]:
		go.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	go.position = Vector2(820, 928)
	go.set_deferred("size", Vector2(660, 64))
	go.pressed.connect(func() -> void:
		var f := FileAccess.open("user://seen_howto", FileAccess.WRITE)
		f.store_string("1")
		f.close()
		done.emit())
	add_child(go)
	modulate.a = 0.0
	create_tween().tween_property(self, "modulate:a", 1.0, 0.3)

static func seen() -> bool:
	return FileAccess.file_exists("user://seen_howto")

class _Art:
	extends Control
	var _font: Font
	func _ready() -> void:
		_font = load("res://assets/fonts/PatrickHand-Regular.ttf")
	func _txt(t: String, pos: Vector2, sz: int, col: Color = Color("F2EAD3")) -> void:
		draw_string(_font, pos, t, HORIZONTAL_ALIGNMENT_LEFT, -1, sz, col)
	func _wrap(t: String, pos: Vector2, w: float, sz: int, col: Color) -> void:
		draw_multiline_string(_font, pos, t, HORIZONTAL_ALIGNMENT_LEFT, w, sz, -1, col)
	func _panel(r: Rect2) -> void:
		draw_rect(Rect2(r.position + Vector2(6, 8), r.size), Color(0, 0, 0, 0.3))
		draw_rect(r, Color("F2EAD3"))
		var rng := RandomNumberGenerator.new()
		rng.seed = int(r.position.x)
		var pts := PackedVector2Array()
		var cs := [r.position, r.position + Vector2(r.size.x, 0), r.position + r.size, r.position + Vector2(0, r.size.y)]
		for i in 4:
			var a: Vector2 = cs[i]
			var b: Vector2 = cs[(i + 1) % 4]
			for k in 12:
				pts.append(a.lerp(b, float(k) / 12.0) + Vector2(rng.randf_range(-2, 2), rng.randf_range(-2, 2)))
		pts.append(pts[0])
		draw_polyline(pts, Color("1E1E1E"), 4.0, true)
	func _draw() -> void:
		_txt("HOW THIS WORLD WORKS", Vector2(430, 96), 54)
		var y := 150.0
		var panels := [Rect2(90, y, 640, 360), Rect2(806, y, 640, 360),
			Rect2(90, y + 396, 640, 360), Rect2(806, y + 396, 640, 360)]
		for p in panels:
			_panel(p)
		var ink := Color("1E1E1E")
		var dim := Color(ink, 0.7)

		# 1 — the five muscles
		_txt("1 · YOUR FIVE MUSCLES", Vector2(120, y + 52), 32, ink)
		var stats := ["build", "sell", "raise", "recruit", "grit"]
		for i in stats.size():
			var sy := y + 92.0 + float(i) * 44.0
			_txt(String(stats[i]), Vector2(130, sy + 24.0), 26, ink)
			for j in 5:
				var filled := j < (3 if i != 1 else 4)
				var rr := Rect2(250 + j * 44, sy, 36, 26)
				if filled:
					draw_rect(rr, Color("E86A5C"))
				draw_rect(rr, ink, false, 2.0)
		_wrap("every plan is judged through the muscle it uses. levels add to your roll. milestones let you grow one.",
			Vector2(560, y + 110), 150.0, 22, dim)

		# 2 — the die decides
		_txt("2 · THE DIE DECIDES", Vector2(836, y + 52), 32, ink)
		var cx := 950.0
		var cy := y + 190.0
		var hex := PackedVector2Array()
		for i in 7:
			var a2 := TAU * float(i % 6) / 6.0 - PI / 2.0
			hex.append(Vector2(cx, cy) + Vector2(cos(a2), sin(a2)) * 78.0)
		draw_colored_polygon(hex, Color("D9453A"))
		draw_polyline(hex, ink, 5.0, true)
		_txt("17", Vector2(cx - 24, cy + 14), 44, Color("F2EAD3"))
		_wrap("you write the week's move. a d20 rolls the moment you commit — your muscle adds, the world sets the difficulty from how bold the plan is. routine 6-8 · solid 9-11 · bold 12-14 · wild 15-16.",
			Vector2(1070, y + 110), 350.0, 23, dim)

		# 3 — the world answers
		_txt("3 · THE WORLD ANSWERS", Vector2(120, y + 448), 32, ink)
		var bands := [["beat it by 5+", "brilliant", Color("8FA582")],
			["meet it", "it lands", Color("F4B942")],
			["miss by 1-2", "mixed — something gives", Color("E8A05C")],
			["miss by 3+", "it backfires, expensively", Color("E86A5C")]]
		for i in bands.size():
			var by := y + 490.0 + float(i) * 52.0
			draw_rect(Rect2(130, by, 26, 26), bands[i][2])
			draw_rect(Rect2(130, by, 26, 26), ink, false, 2.0)
			_txt(String(bands[i][0]) + " — " + String(bands[i][1]), Vector2(172, by + 21.0), 25, ink)
		_wrap("consequences are real: cash moves, people remember, promises come due.",
			Vector2(130, y + 710), 560.0, 22, dim)

		# 4 — the money is the food
		_txt("4 · MONEY IS THE FOOD", Vector2(836, y + 448), 32, ink)
		for i in 9:
			draw_rect(Rect2(846 + i * 40, y + 500, 26, 7), Color(ink, 0.8 - float(i) * 0.08))
		_wrap("rent, payroll and every budget you set burn weekly. spend on marketing, sales, care and R&D in THE LEDGER (press TAB) — each dollar does one real thing. a customer costs money to win and pays back over their stay. three weeks below zero and it's over.",
			Vector2(846, y + 540), 560.0, 23, dim)
		_txt("the world will teach the rest the hard way.", Vector2(846, y + 730), 22, Color(ink, 0.5))
