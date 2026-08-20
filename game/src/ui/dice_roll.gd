class_name DiceRoll
extends Control
## THE TABLE ROLL, pre-rendered (owner design after the 3D attempt failed on
## film): each number 1-20 ships as a spritesheet baked from a seedance video —
## the coral cup rattles, lifts, and a decorated d20 tumbles out and settles on
## the rolled number. The engine's seeded d20 picks WHICH sheet plays; the
## number is the roll, and low numbers already mean bad luck downstream.
##
## Sheets: assets/dice/roll_NN.png — 8x5 grid of 384px frames, 12fps, ~3.3s.
## A missing sheet degrades to a silent skip (the beat still tells the roll).
##
## Usage (MAIN owns it):
##   var dr := DiceRoll.new()
##   add_child(dr)
##   await dr.roll(17)
##   dr.queue_free()

signal settled

const COLS := 8
const ROWS := 5
const FRAMES := 40
const FPS := 12.0
const CELL := 512
const HOLD_LAST := 0.7          ## the settled number is READ, not glimpsed

var _tex: Texture2D
var _frame := 0
var _t := 0.0
var _playing := false
var _rattle: AudioStreamPlayer

func _init() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP   # the ceremony owns its beat

func _ready() -> void:
	if FileAccess.file_exists("res://assets/sfx/dice_rattle.wav"):
		_rattle = AudioStreamPlayer.new()
		_rattle.stream = load("res://assets/sfx/dice_rattle.wav")
		_rattle.volume_db = -6.0
		add_child(_rattle)
	# a click skips to the settled frame — a reader is never held
	gui_input.connect(func(ev: InputEvent) -> void:
		if ev is InputEventMouseButton and ev.pressed and _playing:
			_frame = FRAMES - 1
			_t = 999.0)

func roll(n: int) -> void:
	var path := "res://assets/dice/roll_%02d.png" % clampi(n, 1, 20)
	if not ResourceLoader.exists(path):
		push_warning("DiceRoll: no sheet for %d — ceremony skipped" % n)
		settled.emit()
		return
	_tex = load(path)
	if _tex == null:
		settled.emit()
		return
	_frame = 0
	_t = 0.0
	_playing = true
	if _rattle != null:
		_rattle.play()
	set_process(true)
	queue_redraw()
	await settled

func _process(delta: float) -> void:
	if not _playing:
		return
	_t += delta
	var f := mini(int(_t * FPS), FRAMES - 1)
	if f != _frame:
		_frame = f
		queue_redraw()
	if _frame >= FRAMES - 1 and _t >= float(FRAMES) / FPS + HOLD_LAST:
		_playing = false
		set_process(false)
		settled.emit()

func _draw() -> void:
	if _tex == null:
		return
	# a soft vignette so the tabletop clip reads as ON the room, not a popup
	draw_rect(Rect2(Vector2.ZERO, size), Color(0.08, 0.07, 0.06, 0.45))
	var side := minf(size.x, size.y) * 0.62
	var pos := Vector2((size.x - side) * 0.5, (size.y - side) * 0.5 - 20.0)
	var src := Rect2(float(_frame % COLS) * CELL, float(_frame / COLS) * CELL, CELL, CELL)
	draw_texture_rect_region(_tex, Rect2(pos, Vector2(side, side)), src)
	# the clip's own cream edge blends via a drawn ink frame — a card on the table
	var rng := RandomNumberGenerator.new()
	rng.seed = 27
	var pts := PackedVector2Array()
	var corners := [pos, pos + Vector2(side, 0), pos + Vector2(side, side), pos + Vector2(0, side)]
	for i in 4:
		var a: Vector2 = corners[i]
		var b: Vector2 = corners[(i + 1) % 4]
		for k in 12:
			pts.append(a.lerp(b, float(k) / 12.0) + Vector2(rng.randf_range(-2, 2), rng.randf_range(-2, 2)))
	pts.append(pts[0])
	draw_polyline(pts, Color("1E1E1E"), 5.0, true)
