class_name BirthScreen
extends Control
## THE BIRTH SCREEN (owner: "a proper first loading screen — RUNWAY! and
## creating your world"). Shown the instant the founding papers are signed,
## while the world bible is generated. Drawn, breathing, never chrome.

const CREAM := Color("F2EAD3")
const INK := Color("1E1E1E")
const PEN := Color("E86A5C")

var _t := 0.0
var _font: Font
## The unpacking loop (seedance-baked sheet, 5x8 grid of 1024x576 frames).
## Missing sheet = the drawn fallback below keeps the screen alive.
var _loop_tex: Texture2D
const L_COLS := 5
const L_FRAMES := 40
const L_FPS := 12.0
## The real painted logotype. A font can only ever approximate it, so the screen
## draws the art; the drawn title below is the fallback, not the intent.
var _type_tex: Texture2D

func _init() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP   # nothing behind is ready

func _ready() -> void:
	_font = load("res://assets/fonts/PatrickHand-Regular.ttf")
	if ResourceLoader.exists("res://assets/title/birth_loop.png"):
		_loop_tex = load("res://assets/title/birth_loop.png")
	if ResourceLoader.exists("res://assets/title/layers/type_main.png"):
		_type_tex = load("res://assets/title/layers/type_main.png")
	set_process(true)

func _process(delta: float) -> void:
	_t += delta
	queue_redraw()

func _draw() -> void:
	var w := size.x
	var h := size.y
	draw_rect(Rect2(Vector2.ZERO, size), Color("F2EAD3") if _loop_tex != null else Color("22262B"))
	if _loop_tex == null:
		# the spotlight the run is born under (drawn fallback only)
		draw_circle(Vector2(w * 0.5, h * 0.55), minf(w, h) * 0.42, Color(1, 1, 1, 0.035))
	if _loop_tex != null:
		# THE UNPACKING LOOP FILLS THE FRAME (owner: "should feel the whole
		# frame like title") — cover-scaled, center-cropped, no card, no border
		var fr := int(_t * L_FPS) % L_FRAMES
		var src := Rect2(float(fr % L_COLS) * 1024.0, float(fr / L_COLS) * 576.0, 1024.0, 576.0)
		var s := maxf(w / 1024.0, h / 576.0)
		var dw := 1024.0 * s
		var dh := 576.0 * s
		draw_texture_rect_region(_loop_tex,
			Rect2(Vector2((w - dw) * 0.5, (h - dh) * 0.5), Vector2(dw, dh)), src)
	# RUNWAY! — the painted logotype itself, bobbing. it is ink art, so it only
	# reads over the cream loop; the dark fallback card keeps the drawn title.
	var bob := sin(_t * 1.4) * 4.0
	if _type_tex != null and _loop_tex != null:
		var lw := w * 0.62
		var lh := lw * float(_type_tex.get_height()) / float(_type_tex.get_width())
		draw_texture_rect(_type_tex, Rect2((w - lw) * 0.5, h * 0.12 + bob, lw, lh), false)
	else:
		var title := "RUNWAY"
		var tsz := 132
		var ts := _font.get_string_size(title, HORIZONTAL_ALIGNMENT_LEFT, -1, tsz)
		var tx := (w - ts.x - 54.0) * 0.5
		var ty := (h * 0.17 if _loop_tex != null else h * 0.42) + bob
		var main_col := INK if _loop_tex != null else CREAM
		draw_string(_font, Vector2(tx, ty), title, HORIZONTAL_ALIGNMENT_LEFT, -1, tsz, main_col)
		draw_string(_font, Vector2(tx + ts.x + 10.0, ty), "!", HORIZONTAL_ALIGNMENT_LEFT, -1, tsz, PEN)
	# the runway strip, drawn in dashes that crawl forward
	if _loop_tex == null:
		var ry := h * 0.55
		var dash_w := 46.0
		var off := fmod(_t * 60.0, dash_w * 2.0)
		for i in 18:
			var x := w * 0.18 + float(i) * dash_w * 2.0 - off
			if x > w * 0.2 and x + dash_w < w * 0.82:
				draw_rect(Rect2(x, ry, dash_w, 6), Color(CREAM, 0.25))
	# creating your world… — cream behind, ink in front, so it survives the art
	var msg := "creating your world"
	var dots := ".".repeat(1 + int(fmod(_t * 1.6, 3.0)))
	var msz := _font.get_string_size(msg + "...", HORIZONTAL_ALIGNMENT_LEFT, -1, 34)
	var a := 0.7 + 0.3 * sin(_t * 2.4)
	var at := Vector2((w - msz.x) * 0.5, h * 0.90 if _loop_tex != null else h * 0.66)
	if _loop_tex != null:
		draw_string(_font, at + Vector2(2, 2), msg + dots, HORIZONTAL_ALIGNMENT_LEFT, -1, 34,
				Color(CREAM, a * 0.9))
	draw_string(_font, at, msg + dots, HORIZONTAL_ALIGNMENT_LEFT, -1, 34,
			Color(INK, a * 0.85) if _loop_tex != null else Color(CREAM, a))
