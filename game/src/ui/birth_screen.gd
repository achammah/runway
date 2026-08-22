class_name BirthScreen
extends Control
## THE BIRTH SCREEN (owner: "a proper first loading screen — RUNWAY! and
## creating your world"). Shown the instant the founding papers are signed,
## while the world bible is generated. Drawn, breathing, never chrome.

const CREAM := Color("F2EAD3")
const INK := Color("1E1E1E")
const PEN := Color("E86A5C")

var _t := 0.0
var status_line := "creating your world"   # main flips this per phase
var _font: Font
## The unpacking loop (seedance-baked sheet, 5x8 grid of 1024x576 frames).
## Missing sheet = the drawn fallback below keeps the screen alive.
var _loop_tex: Texture2D
const L_COLS := 5
const L_FRAMES := 40
const L_FPS := 12.0
## THE ARRIVAL (owner: "the animation just drops in with no transition"). Plays
## ONCE before the loop: the room is empty and taped shut, the founder walks in
## from the left, pulls the tape, kneels. Its last frame IS the loop's frame 0,
## so the hand-over is a straight cut with nothing to see.
## Sheet: 24 frames of 1024x576 in a 5x5 grid — the 25th cell is unused.
var _intro_tex: Texture2D
const I_COLS := 5
const I_FRAMES := 24
## the first beat holds on the still so the fade-in lands on a calm frame
const I_HOLD := 0.3
enum { PHASE_INTRO, PHASE_LOOP }
var _phase := PHASE_INTRO
var _loop_t0 := 0.0   ## when the loop's own clock starts, so it opens on frame 0
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
	if ResourceLoader.exists("res://assets/title/birth_intro.png"):
		_intro_tex = load("res://assets/title/birth_intro.png")
	if _intro_tex == null:
		_phase = PHASE_LOOP   # no arrival on disk: the loop is the whole screen
	if ResourceLoader.exists("res://assets/title/layers/type_main.png"):
		_type_tex = load("res://assets/title/layers/type_main.png")
	# the screen comes up out of the dark, never pops. main's swap fades too; this
	# is the screen's own, so it holds wherever it is shown from.
	self_modulate.a = 0.0
	create_tween().tween_property(self, "self_modulate:a", 1.0, 0.35)
	set_process(true)

## ONE REPAINT PER BAKED FRAME. The screen is 12fps art with a bobbing logotype
## and a breathing line over it; drawn on every displayed frame, four repaints
## in five re-blitted the identical cell of a 5120x4608 sheet. The bob and the
## breath ride the same clock as the art they sit on, which is what they look
## like anyway. -1 so the very first frame always lands.
var _fr := -1

func _process(delta: float) -> void:
	_t += delta
	# the arrival runs once, then the loop takes the frame over from its own zero
	if _phase == PHASE_INTRO and _loop_tex != null \
			and _t >= I_HOLD + float(I_FRAMES) / L_FPS:
		_phase = PHASE_LOOP
		_loop_t0 = _t
		# THE ARRIVAL IS SPENT. Its sheet is 59MB that will never be drawn
		# again, and this screen stays up until the whole world is written.
		_intro_tex = null
	# the sheet clocks are all this clock with a constant offset, so one tick is
	# one new cell — and the line keeps breathing even where no sheet shipped
	var fr := int(_t * L_FPS)
	if fr == _fr:
		return
	_fr = fr
	queue_redraw()

## one baked frame, cover-scaled and center-cropped — never stretched
func _blit(tex: Texture2D, fr: int, cols: int) -> void:
	var src := Rect2(float(fr % cols) * 1024.0, float(fr / cols) * 576.0, 1024.0, 576.0)
	var s := maxf(size.x / 1024.0, size.y / 576.0)
	var dw := 1024.0 * s
	var dh := 576.0 * s
	draw_texture_rect_region(tex,
		Rect2(Vector2((size.x - dw) * 0.5, (size.y - dh) * 0.5), Vector2(dw, dh)), src)

func _draw() -> void:
	var w := size.x
	var h := size.y
	var art := _loop_tex != null or _intro_tex != null
	draw_rect(Rect2(Vector2.ZERO, size), Color("F2EAD3") if art else Color("22262B"))
	if not art:
		# the spotlight the run is born under (drawn fallback only)
		draw_circle(Vector2(w * 0.5, h * 0.55), minf(w, h) * 0.42, Color(1, 1, 1, 0.035))
	# THE ART FILLS THE FRAME (owner: "should feel the whole frame like title") —
	# no card, no border. The arrival first, then the loop from its own zero.
	if _phase == PHASE_INTRO and _intro_tex != null:
		_blit(_intro_tex, mini(int(maxf(_t - I_HOLD, 0.0) * L_FPS), I_FRAMES - 1), I_COLS)
	elif _loop_tex != null:
		_blit(_loop_tex, int((_t - _loop_t0) * L_FPS) % L_FRAMES, L_COLS)
	# RUNWAY! — the painted logotype itself, bobbing. it is ink art, so it only
	# reads over the cream loop; the dark fallback card keeps the drawn title.
	var bob := sin(_t * 1.4) * 4.0
	if _type_tex != null and art:
		var lw := w * 0.62
		var lh := lw * float(_type_tex.get_height()) / float(_type_tex.get_width())
		draw_texture_rect(_type_tex, Rect2((w - lw) * 0.5, h * 0.12 + bob, lw, lh), false)
	else:
		var title := "RUNWAY"
		var tsz := 132
		var ts := _font.get_string_size(title, HORIZONTAL_ALIGNMENT_LEFT, -1, tsz)
		var tx := (w - ts.x - 54.0) * 0.5
		var ty := (h * 0.17 if art else h * 0.42) + bob
		var main_col := INK if art else CREAM
		draw_string(_font, Vector2(tx, ty), title, HORIZONTAL_ALIGNMENT_LEFT, -1, tsz, main_col)
		draw_string(_font, Vector2(tx + ts.x + 10.0, ty), "!", HORIZONTAL_ALIGNMENT_LEFT, -1, tsz, PEN)
	# the runway strip, drawn in dashes that crawl forward
	if not art:
		var ry := h * 0.55
		var dash_w := 46.0
		var off := fmod(_t * 60.0, dash_w * 2.0)
		for i in 18:
			var x := w * 0.18 + float(i) * dash_w * 2.0 - off
			if x > w * 0.2 and x + dash_w < w * 0.82:
				draw_rect(Rect2(x, ry, dash_w, 6), Color(CREAM, 0.25))
	# creating your world… — a cream ring behind, ink in front. a single offset
	# copy only ghosted the glyphs; the ring is what carries it over the boxes.
	var msg := status_line
	var dots := ".".repeat(1 + int(fmod(_t * 1.6, 3.0)))
	var msz := _font.get_string_size(msg + "...", HORIZONTAL_ALIGNMENT_LEFT, -1, 34)
	var a := 0.82 + 0.18 * sin(_t * 2.4)   # breathes, never dims to unreadable
	var at := Vector2((w - msz.x) * 0.5, h * 0.90 if art else h * 0.66)
	if art:
		for o in [Vector2(-2, 0), Vector2(2, 0), Vector2(0, -2), Vector2(0, 2)]:
			draw_string(_font, at + o, msg + dots, HORIZONTAL_ALIGNMENT_LEFT, -1, 34,
					Color(CREAM, a))
	draw_string(_font, at, msg + dots, HORIZONTAL_ALIGNMENT_LEFT, -1, 34,
			Color(INK, a) if art else Color(CREAM, a))
