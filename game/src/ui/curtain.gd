class_name Curtain
extends Control
## THE THEATER CURTAIN (owner ask: "an animation to show things are moving, like
## the curtain of a theater"). Locking the week drops it INSTANTLY — the click
## always answers — and it rises on whatever the world prepared behind it: the
## reading beat, the new room, the next act.
##
## Drawn, never chrome: two coral drapes with wobbly ink edges and a scalloped
## valance, in the game's own felt-pen hand.
##
## Usage (MAIN owns one):
##     var c := Curtain.new()
##     add_child(c)                # topmost
##     await c.close()             # sweeps shut in 0.45s
##     ... build what is behind ...
##     await c.open()              # sweeps apart in 0.55s

signal closed
signal opened

const COR := Color("E86A5C")
const DARK := Color("C9503F")
const INK := Color("1E1E1E")

var _t := 0.0          ## 0 = fully open (offstage), 1 = fully shut
var _tw: Tween

func _init() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	set_anchors_preset(Control.PRESET_FULL_RECT)
	visible = false

func close(secs: float = 0.45) -> void:
	visible = true
	# shut curtains swallow clicks so nothing behind them can be pressed mid-swap
	mouse_filter = Control.MOUSE_FILTER_STOP
	if _tw != null and _tw.is_valid():
		_tw.kill()
	_tw = create_tween()
	_tw.tween_method(_step, _t, 1.0, secs).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	_tw.tween_callback(func() -> void: closed.emit())
	await _tw.finished

func open(secs: float = 0.55) -> void:
	if _tw != null and _tw.is_valid():
		_tw.kill()
	_tw = create_tween()
	_tw.tween_method(_step, _t, 0.0, secs).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_IN_OUT)
	_tw.tween_callback(func() -> void:
		visible = false
		mouse_filter = Control.MOUSE_FILTER_IGNORE
		opened.emit())
	await _tw.finished

func _step(v: float) -> void:
	_t = v
	queue_redraw()

func _draw() -> void:
	if _t <= 0.001:
		return
	var w := size.x
	var h := size.y
	var half: float = w * 0.5 * _t
	var rng := RandomNumberGenerator.new()
	rng.seed = 41
	for side in 2:
		var panel_w: float = half + 14.0
		var x0: float = -14.0 if side == 0 else w - panel_w + 14.0
		# the drape body
		draw_rect(Rect2(x0, 0, panel_w, h), COR)
		# fold shading: four darker vertical swags per panel
		for f in 4:
			var fx: float = x0 + panel_w * (0.18 + 0.22 * float(f))
			draw_rect(Rect2(fx, 0, panel_w * 0.055, h), Color(DARK, 0.55))
		# the wobbly INK edge on the meeting side
		var ex: float = x0 + panel_w if side == 0 else x0
		var pts := PackedVector2Array()
		for i in 33:
			pts.append(Vector2(ex + rng.randf_range(-2.5, 2.5), h * float(i) / 32.0))
		draw_polyline(pts, INK, 4.0, true)
	# the valance: a scalloped strip across the top, always full width while shut
	var va := Color(DARK, minf(_t * 2.0, 1.0))
	draw_rect(Rect2(0, 0, w, 46), va)
	for s in 12:
		var cx: float = w * (float(s) + 0.5) / 12.0
		draw_circle(Vector2(cx, 46), w / 24.0, va)
	if _t > 0.15:
		var line := PackedVector2Array()
		for i in 49:
			var lx: float = w * float(i) / 48.0
			var ly: float = 46.0 + absf(sin(float(i) * PI / 4.0)) * (w / 24.0) - 2.0
			line.append(Vector2(lx, ly + rng.randf_range(-1.5, 1.5)))
		draw_polyline(line, INK, 3.0, true)
