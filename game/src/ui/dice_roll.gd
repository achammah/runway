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
	# the table arrives with a breath of curtain-whoosh under the rattle
	if FileAccess.file_exists("res://assets/sfx/curtain.wav"):
		var wh := AudioStreamPlayer.new()
		wh.stream = load("res://assets/sfx/curtain.wav")
		wh.volume_db = -14.0
		wh.pitch_scale = 1.25
		add_child(wh)
		wh.play()
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
	modulate.a = 0.0
	var tw := create_tween()
	tw.tween_property(self, "modulate:a", 1.0, 0.25)
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
	# ITS OWN SCREEN (owner: "the video dice roll ... on its own screen"): an
	# opaque felt table so nothing — page, beat, room — can bleed through the roll
	draw_rect(Rect2(Vector2.ZERO, size), Color(0.11, 0.095, 0.08, 1.0))
	draw_circle(size * 0.5, minf(size.x, size.y) * 0.58, Color(0.145, 0.125, 0.10, 1.0))
	# FULL HEIGHT (owner: "fill screen in height so we avoid video cropping"):
	# the clip is square, so height IS the constraint — use all of it
	var side := size.y
	var pos := Vector2((size.x - side) * 0.5, (size.y - side) * 0.5)
	var src := Rect2(float(_frame % COLS) * CELL, float(_frame / COLS) * CELL, CELL, CELL)
	# the sheets carry ALPHA (owner: background-removed clips): the cup and die
	# sit straight on the felt, no card, no frame — just the drawing and the light
	draw_texture_rect_region(_tex, Rect2(pos, Vector2(side, side)), src)
