class_name HowToScreen
extends Control
## HOW THIS WORLD WORKS (owner: "a few pages of looped video to explain the
## mechanics" — the old static four-panel sheet graded 20%): three pages, each
## one a baked seedance loop playing large inside an inked film frame, with the
## REAL rule the engine runs written underneath it in plain words.
## Shown once (user://seen_howto_v2), then GOT IT.

signal done

const CREAM := Color("F2EAD3")
const INK := Color("1E1E1E")
const PEN := Color("E86A5C")
const SAGE := Color("8FA582")
const YELL := Color("F4B942")

## the baked loops: 5x8 grids of 1024x576 frames, one page each
const L_COLS := 5
const L_FRAMES := 40
const L_FPS := 12.0
const CELL_W := 1024.0
const CELL_H := 576.0
const SHEET_R := Rect2(88, 24, 1360, 976)
const FRAME_R := Rect2(198, 116, 1140, 642)

const TITLES := [
	"YOU WRITE. THE DIE DECIDES.",
	"THE WORLD ANSWERS.",
	"MONEY IS THE FOOD."]
const CAPS := [
	"Write your week's move in the journal. A d20 rolls the moment you commit — your five muscles (build, sell, raise, recruit, grit) add to it, the world sets the difficulty.",
	"Beat the difficulty by 5 and it's brilliant. Miss by 3 and it backfires, expensively. Cash moves, people remember, promises come due.",
	"Rent, payroll and every budget burn weekly. Set marketing, sales, care and R&D in THE LEDGER (TAB). A customer costs money to win and pays back over their stay. Three weeks below zero and it's over."]
const LOOPS := [
	"res://assets/title/howto_1.png",
	"res://assets/title/howto_2.png",
	"res://assets/title/howto_3.png"]

var _font: Font
var _art: _Art
var _btn: Button
var _word: Label
var _card: Control
var _page := 0
var _count := 3
var _gone := false

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

	# no loop shipped at all: one page, the old four-panel explainer, never blank
	var any := false
	for p in LOOPS:
		if ResourceLoader.exists(p):
			any = true
	_count = 3 if any else 1

	_art = _Art.new()
	_art.set_anchors_preset(Control.PRESET_FULL_RECT)
	_art.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_art)

	# a REAL paper button (the title screen's card): cream sheet, wobbled ink edge
	_btn = Button.new()
	_btn.flat = true
	for stn in ["normal", "hover", "pressed", "focus"]:
		_btn.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	_card = _PaperBtn.new()
	_card.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_btn.add_child(_card)
	_word = Label.new()
	_word.add_theme_font_override("font", _font)
	_word.add_theme_font_size_override("font_size", 34)
	_word.add_theme_color_override("font_color", PEN)
	_word.set_anchors_preset(Control.PRESET_FULL_RECT)
	_word.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_word.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_word.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_btn.add_child(_word)
	_btn.pressed.connect(_advance)
	add_child(_btn)

	# the whole page is a click target too — nobody hunts for the button
	gui_input.connect(func(ev: InputEvent) -> void:
		if ev is InputEventMouseButton and ev.pressed \
				and (ev as InputEventMouseButton).button_index == MOUSE_BUTTON_LEFT:
			_advance())

	_show(0)
	modulate.a = 0.0
	create_tween().tween_property(self, "modulate:a", 1.0, 0.3)

## ONE REPAINT PER BAKED FRAME (the page is a 12fps loop inside furniture that
## never moves — paper, film edge, sprockets, title, caption, dots are all the
## same drawing every time). Repainting on every displayed frame re-wobbled
## three ink borders and re-laid the caption for a picture nobody could tell
## apart: four repaints in five drew the frame that was already on screen.
var _fr := -1

func _process(delta: float) -> void:
	_art.t += delta
	var fr := int(_art.t * L_FPS) % L_FRAMES
	if fr == _fr:
		return
	_fr = fr
	_art.queue_redraw()

## next page, or the door out on the last one
func _advance() -> void:
	if _page + 1 < _count:
		_show(_page + 1)
	else:
		_finish()

func _finish() -> void:
	if _gone:
		return          # a click landing on the button fires both doors: open it once
	_gone = true
	var f := FileAccess.open("user://seen_howto_v2", FileAccess.WRITE)
	f.store_string("1")
	f.close()
	done.emit()

## page furniture + the loop for it, loaded one at a time: each sheet is a
## 5120x4608 texture and three of them at once is memory nobody needs
func _show(i: int) -> void:
	_page = i
	_art.page = i
	_art.count = _count
	_art.tex = null
	if ResourceLoader.exists(LOOPS[i]):
		_art.tex = load(LOOPS[i])
	_art.queue_redraw()
	var last := _page + 1 >= _count
	var txt := "GOT IT — LET'S FOUND SOMETHING  →" if last else "NEXT  →"
	# the long word rides a notch smaller, or its card crowds the page dots
	var sz := 32 if last else 34
	_word.text = txt
	_word.add_theme_font_size_override("font_size", sz)
	var w := _font.get_string_size(txt, HORIZONTAL_ALIGNMENT_LEFT, -1, sz).x + 68.0
	_btn.position = Vector2(SHEET_R.end.x - 48.0 - w, 918)
	_btn.size = Vector2(w, 70)
	_card.size = Vector2(w, 70)
	_card.queue_redraw()

static func seen() -> bool:
	return FileAccess.file_exists("user://seen_howto_v2")

## Cream paper + wobbled ink border drawn behind the button's word.
class _PaperBtn:
	extends Control
	func _draw() -> void:
		draw_rect(Rect2(Vector2(4, 5), size), Color(0, 0, 0, 0.28))
		draw_rect(Rect2(Vector2.ZERO, size), HowToScreen.CREAM)
		var rng := RandomNumberGenerator.new()
		rng.seed = 12
		var pts := PackedVector2Array()
		var cs := [Vector2(2, 2), Vector2(size.x - 2, 2), size - Vector2(2, 2), Vector2(2, size.y - 2)]
		for i in 4:
			var a: Vector2 = cs[i]
			var b: Vector2 = cs[(i + 1) % 4]
			for k in 10:
				pts.append(a.lerp(b, float(k) / 10.0) + Vector2(rng.randf_range(-1.6, 1.6), rng.randf_range(-1.6, 1.6)))
		pts.append(pts[0])
		draw_polyline(pts, HowToScreen.INK, 3.5, true)

class _Art:
	extends Control
	var page := 0
	var count := 3
	var tex: Texture2D
	var t := 0.0
	var _font: Font

	func _ready() -> void:
		_font = load("res://assets/fonts/PatrickHand-Regular.ttf")

	func _draw() -> void:
		_paper(HowToScreen.SHEET_R)
		var fr := HowToScreen.FRAME_R
		if tex != null:
			_loop(fr)
		else:
			_legacy(fr)          # sheet missing: the old four panels, never a blank frame
		_film(fr)
		var ttl: String = HowToScreen.TITLES[page]
		var tw := _font.get_string_size(ttl, HORIZONTAL_ALIGNMENT_LEFT, -1, 56).x
		draw_string(_font, Vector2(768.0 - tw * 0.5, 90), ttl,
			HORIZONTAL_ALIGNMENT_LEFT, -1, 56, HowToScreen.INK)
		draw_multiline_string(_font, Vector2(238, 812), String(HowToScreen.CAPS[page]),
			HORIZONTAL_ALIGNMENT_CENTER, 1060.0, 30, -1, Color(HowToScreen.INK, 0.85))
		_dots()

	## the loop, cover-cropped in SOURCE space so it fills the frame exactly:
	## never a stretched destination rect, never a letterbox
	func _loop(fr: Rect2) -> void:
		var f := int(t * HowToScreen.L_FPS) % HowToScreen.L_FRAMES
		var want := fr.size.x / fr.size.y
		var sw := HowToScreen.CELL_W
		var sh := HowToScreen.CELL_H
		if sw / sh > want:
			sw = sh * want
		else:
			sh = sw / want
		var src := Rect2(float(f % HowToScreen.L_COLS) * HowToScreen.CELL_W + (HowToScreen.CELL_W - sw) * 0.5,
			float(f / HowToScreen.L_COLS) * HowToScreen.CELL_H + (HowToScreen.CELL_H - sh) * 0.5, sw, sh)
		draw_texture_rect_region(tex, fr, src)

	## cream sheet with a drop shadow and a hand-wobbled ink edge
	func _paper(r: Rect2) -> void:
		draw_rect(Rect2(r.position + Vector2(8, 12), r.size), Color(0, 0, 0, 0.3))
		draw_rect(r, HowToScreen.CREAM)
		_wobble_rect(r.grow(-3.0), HowToScreen.INK, 4.0, 16, 2.0, 6)

	## the film frame: thick ink edge, sprocket ticks down both outer margins —
	## the top and bottom belong to the title and the caption
	func _film(r: Rect2) -> void:
		_wobble_rect(r.grow(4.0), HowToScreen.INK, 6.0, 14, 2.2, 9)
		var holes := int(r.size.y / 78.0)
		for i in holes:
			var y := r.position.y + 14.0 + (r.size.y - 28.0) * float(i) / float(holes - 1)
			draw_rect(Rect2(r.position.x - 31, y - 9, 14, 18), Color(HowToScreen.INK, 0.4))
			draw_rect(Rect2(r.end.x + 17, y - 9, 14, 18), Color(HowToScreen.INK, 0.4))

	## three page dots: the one you are on is filled coral
	func _dots() -> void:
		for i in count:
			var c := Vector2(768.0 + (float(i) - float(count - 1) * 0.5) * 46.0, 952.0)
			if i == page:
				draw_circle(c, 11.0, HowToScreen.PEN)
				_ring(c, 11.0, HowToScreen.INK, 3.0, 20 + i)
			else:
				_ring(c, 9.0, Color(HowToScreen.INK, 0.45), 3.0, 20 + i)

	func _ring(c: Vector2, rad: float, col: Color, w: float, sd: int) -> void:
		var rng := RandomNumberGenerator.new()
		rng.seed = sd
		var pts := PackedVector2Array()
		for i in 17:
			var a := TAU * float(i) / 16.0
			pts.append(c + Vector2(cos(a), sin(a)) * (rad + rng.randf_range(-1.2, 1.2)))
		draw_polyline(pts, col, w, true)

	func _wobble_rect(r: Rect2, col: Color, w: float, steps: int, jit: float, sd: int) -> void:
		var rng := RandomNumberGenerator.new()
		rng.seed = sd
		var pts := PackedVector2Array()
		var cs := [r.position, r.position + Vector2(r.size.x, 0), r.end, r.position + Vector2(0, r.size.y)]
		for i in 4:
			var a: Vector2 = cs[i]
			var b: Vector2 = cs[(i + 1) % 4]
			for k in steps:
				pts.append(a.lerp(b, float(k) / float(steps)) + Vector2(rng.randf_range(-jit, jit), rng.randf_range(-jit, jit)))
		pts.append(pts[0])
		draw_polyline(pts, col, w, true)

	## THE OLD FOUR PANELS, kept as the fallback: if a loop never shipped the
	## rules still get taught. Drawn in the original 1536x1024 hand, fitted.
	func _legacy(r: Rect2) -> void:
		draw_rect(r, HowToScreen.CREAM)
		var sc := minf(r.size.x / 1536.0, r.size.y / 1024.0)
		draw_set_transform(r.position + Vector2((r.size.x - 1536.0 * sc) * 0.5,
			(r.size.y - 1024.0 * sc) * 0.5 - 110.0 * sc), 0.0, Vector2(sc, sc))
		var ink := HowToScreen.INK
		var dim := Color(ink, 0.7)
		var y := 150.0
		for p in [Rect2(90, y, 640, 360), Rect2(806, y, 640, 360),
				Rect2(90, y + 396, 640, 360), Rect2(806, y + 396, 640, 360)]:
			_lpanel(p)
		_txt("1 · YOUR FIVE MUSCLES", Vector2(120, y + 52), 32, ink)
		var stats := ["build", "sell", "raise", "recruit", "grit"]
		for i in stats.size():
			var sy := y + 92.0 + float(i) * 44.0
			_txt(String(stats[i]), Vector2(130, sy + 24.0), 26, ink)
			for j in 5:
				var rr := Rect2(250 + j * 44, sy, 36, 26)
				if j < (3 if i != 1 else 4):
					draw_rect(rr, HowToScreen.PEN)
				draw_rect(rr, ink, false, 2.0)
		_wrap("every plan is judged through the muscle it uses. levels add to your roll.",
			Vector2(560, y + 110), 150.0, 22, dim)
		_txt("2 · THE DIE DECIDES", Vector2(836, y + 52), 32, ink)
		var hex := PackedVector2Array()
		for i in 7:
			var a2 := TAU * float(i % 6) / 6.0 - PI / 2.0
			hex.append(Vector2(950.0, y + 190.0) + Vector2(cos(a2), sin(a2)) * 78.0)
		draw_colored_polygon(hex, Color("D9453A"))
		draw_polyline(hex, ink, 5.0, true)
		_txt("17", Vector2(926, y + 204), 44, HowToScreen.CREAM)
		_wrap("you write the week's move. a d20 rolls the moment you commit — your muscle adds, the world sets the difficulty from how bold the plan is.",
			Vector2(1070, y + 110), 350.0, 23, dim)
		_txt("3 · THE WORLD ANSWERS", Vector2(120, y + 448), 32, ink)
		var bands := [["beat it by 5+", "brilliant", HowToScreen.SAGE],
			["meet it", "it lands", HowToScreen.YELL],
			["miss by 1-2", "mixed — something gives", Color("E8A05C")],
			["miss by 3+", "it backfires, expensively", HowToScreen.PEN]]
		for i in bands.size():
			var by := y + 490.0 + float(i) * 52.0
			draw_rect(Rect2(130, by, 26, 26), bands[i][2])
			draw_rect(Rect2(130, by, 26, 26), ink, false, 2.0)
			_txt(String(bands[i][0]) + " — " + String(bands[i][1]), Vector2(172, by + 21.0), 25, ink)
		_txt("4 · MONEY IS THE FOOD", Vector2(836, y + 448), 32, ink)
		for i in 9:
			draw_rect(Rect2(846 + i * 40, y + 500, 26, 7), Color(ink, 0.8 - float(i) * 0.08))
		_wrap("rent, payroll and every budget you set burn weekly. spend on marketing, sales, care and R&D in THE LEDGER (press TAB). a customer costs money to win and pays back over their stay. three weeks below zero and it's over.",
			Vector2(846, y + 540), 560.0, 23, dim)
		draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)

	func _lpanel(r: Rect2) -> void:
		draw_rect(Rect2(r.position + Vector2(6, 8), r.size), Color(0, 0, 0, 0.22))
		draw_rect(r, HowToScreen.CREAM)
		_wobble_rect(r, HowToScreen.INK, 4.0, 12, 2.0, int(r.position.x))

	func _txt(t2: String, pos: Vector2, sz: int, col: Color) -> void:
		draw_string(_font, pos, t2, HORIZONTAL_ALIGNMENT_LEFT, -1, sz, col)

	func _wrap(t2: String, pos: Vector2, w: float, sz: int, col: Color) -> void:
		draw_multiline_string(_font, pos, t2, HORIZONTAL_ALIGNMENT_LEFT, w, sz, -1, col)
