class_name StudioCard
extends Control
## THE STUDIO CARD — "ASSEM STUDIO" fades in and out before the title, the way
## a real release opens. Click skips. Drawn: ink stage, cream lettering, one
## coral underline that draws itself while the name holds.

signal done

const CREAM := Color("F2EAD3")
const PEN := Color("E86A5C")

var _t := 0.0
var _font: Font
var _finished := false

const HOLD := 2.6      ## total life: fade in 0.7, hold, fade out 0.6

func _init() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP

func _ready() -> void:
	_font = load("res://assets/fonts/PatrickHand-Regular.ttf")
	set_process(true)
	gui_input.connect(func(ev: InputEvent) -> void:
		if ev is InputEventMouseButton and ev.pressed:
			_finish())

func _finish() -> void:
	if _finished:
		return
	_finished = true
	done.emit()
	queue_free()

func _process(delta: float) -> void:
	_t += delta
	if _t >= HOLD:
		_finish()
		return
	queue_redraw()

func _draw() -> void:
	var w := size.x
	var h := size.y
	draw_rect(Rect2(Vector2.ZERO, size), Color("22262B"))
	var a := 1.0
	if _t < 0.7:
		a = _t / 0.7
	elif _t > HOLD - 0.6:
		a = maxf((HOLD - _t) / 0.6, 0.0)
	var name := "ASSEM STUDIO"
	var sz := 76
	var ts := _font.get_string_size(name, HORIZONTAL_ALIGNMENT_LEFT, -1, sz)
	draw_string(_font, Vector2((w - ts.x) * 0.5, h * 0.5), name,
			HORIZONTAL_ALIGNMENT_LEFT, -1, sz, Color(CREAM, a))
	# the underline draws itself during the hold
	var prog := clampf((_t - 0.6) / 0.5, 0.0, 1.0)
	if prog > 0.02:
		var x0 := (w - ts.x) * 0.5
		var pts := PackedVector2Array()
		var rng := RandomNumberGenerator.new()
		rng.seed = 11
		for i in 17:
			var fx := float(i) / 16.0
			if fx > prog:
				break
			pts.append(Vector2(x0 + ts.x * fx, h * 0.5 + 22.0 + rng.randf_range(-2.0, 2.0)))
		if pts.size() >= 2:
			draw_polyline(pts, Color(PEN, a), 5.0, true)
	var sub := "presents"
	var ss := _font.get_string_size(sub, HORIZONTAL_ALIGNMENT_LEFT, -1, 30)
	draw_string(_font, Vector2((w - ss.x) * 0.5, h * 0.5 + 74.0), sub,
			HORIZONTAL_ALIGNMENT_LEFT, -1, 30, Color(CREAM, a * 0.55))
