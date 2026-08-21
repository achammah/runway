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

func _init() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP   # nothing behind is ready

func _ready() -> void:
	_font = load("res://assets/fonts/PatrickHand-Regular.ttf")
	set_process(true)

func _process(delta: float) -> void:
	_t += delta
	queue_redraw()

func _draw() -> void:
	var w := size.x
	var h := size.y
	draw_rect(Rect2(Vector2.ZERO, size), Color("22262B"))
	# the spotlight the run is born under
	draw_circle(Vector2(w * 0.5, h * 0.55), minf(w, h) * 0.42, Color(1, 1, 1, 0.035))
	# RUNWAY! — the title in its own hand, the ! in pen
	var title := "RUNWAY"
	var tsz := 132
	var ts := _font.get_string_size(title, HORIZONTAL_ALIGNMENT_LEFT, -1, tsz)
	var tx := (w - ts.x - 54.0) * 0.5
	var ty := h * 0.42 + sin(_t * 1.4) * 4.0
	draw_string(_font, Vector2(tx, ty), title, HORIZONTAL_ALIGNMENT_LEFT, -1, tsz, CREAM)
	draw_string(_font, Vector2(tx + ts.x + 10.0, ty), "!", HORIZONTAL_ALIGNMENT_LEFT, -1, tsz, PEN)
	# the runway strip, drawn in dashes that crawl forward
	var ry := h * 0.55
	var dash_w := 46.0
	var off := fmod(_t * 60.0, dash_w * 2.0)
	for i in 18:
		var x := w * 0.18 + float(i) * dash_w * 2.0 - off
		if x > w * 0.2 and x + dash_w < w * 0.82:
			draw_rect(Rect2(x, ry, dash_w, 6), Color(CREAM, 0.25))
	# creating your world…
	var msg := "creating your world"
	var dots := ".".repeat(1 + int(fmod(_t * 1.6, 3.0)))
	var msz := _font.get_string_size(msg + "...", HORIZONTAL_ALIGNMENT_LEFT, -1, 40)
	var a := 0.7 + 0.3 * sin(_t * 2.4)
	draw_string(_font, Vector2((w - msz.x) * 0.5, h * 0.66), msg + dots,
			HORIZONTAL_ALIGNMENT_LEFT, -1, 40, Color(CREAM, a))
