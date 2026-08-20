class_name LoadingScreen
extends Control
## THE BEAT BETWEEN WEEKS — owned by MAIN. Shown while the next scene is generated.
##
## THE DESIGN, in the owner's words: "have ALL the text of the situation coming in
## the loading screen so people can read it while we actually generate the full
## situation... then after we're done, it opens the scene to see what's happening."
##
## So this is not a loading screen with a spinner. It is the READING beat. The week's
## consequences arrive as text within a few seconds of the lock; the scene takes ~40-90s
## to render. Reading the consequences takes 30-60s. So the wait is spent doing the most
## interesting thing in the game — finding out what your decision actually did — and the
## room opens when you look up.
##
## Rules it holds itself to:
##  - The text is the content, not decoration. It is paced, not dumped: lines arrive as
##    if being written, so reading and generating finish together.
##  - It NEVER blocks on the image. If the render is slow the player can still read on;
##    if the render fails the game continues with the previous room.
##  - The player is never held once they are done reading AND the art is ready.
##  - No percentage. A pen stroke advances with real reported progress.
##
## Usage:
##     var l := LoadingScreen.new()
##     l.begin("WEEK 7")
##     add_child(l)
##     l.say("You said", written_move)
##     l.say("They heard", interpreted_as)
##     l.say("What happened", narration)
##     l.say("", reality_check)
##     l.report(0.5)                     # optional real progress
##     await l.finish()                  # waits for the reader, then opens the scene

signal closed

const HAND := "res://assets/fonts/PatrickHand-Regular.ttf"
const INK := Color("1E1E1E")
const PEN := Color("E86A5C")
const CREAM := Color("F2EAD3")
const SIZE_TITLE := 56
const SIZE_BODY := 34
const SIZE_LABEL := 26
## Below this a screen reads as a glitch. Above it, a reader has time to settle.
const MIN_LIFE := 2.0
## Roughly how fast a person reads this kind of prose, in characters per second.
## Used only to PACE the reveal, never to hold the player once they are done.
const READ_CPS := 22.0
## The card, and the band at its foot that text may never enter.
const CARD_H := 868.0
const FOOTER_H := 96.0
const TEXT_H := CARD_H - 126.0 - FOOTER_H

var _font: Font
var _card: Control
var _col: VBoxContainer
var _bar: _PenLine
var _scroll: ScrollContainer
var _t := 0.0
var _target := 0.0
var _done := false
var _reveal_queue: Array = []
var _next_reveal_at := 0.0
var _scratch: AudioStreamPlayer
var _writing := 0                 ## blocks currently writing; scratch while > 0
var _bodies: Array = []           ## every body label, so a skip can finish them all
var _ready_hint: Label

func begin(week_label: String) -> void:
	_font = load(HAND)
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP    # nothing behind this is ready to touch

	var veil := ColorRect.new()
	veil.color = Color(0.06, 0.05, 0.07, 0.90)
	veil.set_anchors_preset(Control.PRESET_FULL_RECT)
	veil.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(veil)

	_card = Control.new()
	_card.position = Vector2(228, 78)
	_card.set_deferred("size", Vector2(1080, 868))
	_card.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_card)

	var paper := _Paper.new()
	paper.mouse_filter = Control.MOUSE_FILTER_IGNORE
	paper.set_deferred("size", Vector2(1080, 868))
	_card.add_child(paper)

	var t := _mk(week_label, SIZE_TITLE, INK, HORIZONTAL_ALIGNMENT_CENTER)
	t.position = Vector2(0, 40)
	t.custom_minimum_size = Vector2(1080, 0)
	t.set_deferred("size", Vector2(1080, 0))
	_card.add_child(t)

	# THE TEXT MUST NEVER REACH THE FOOTER. Laid out free, six beats overran the card
	# and the progress stroke printed straight through a paragraph — the strikethrough
	# bug class. So the column is clipped inside a scroller with a reserved footer band
	# below it, and it scrolls itself as beats arrive.
	_scroll = ScrollContainer.new()
	_scroll.position = Vector2(96, 126)
	_scroll.set_deferred("size", Vector2(888, TEXT_H))
	_scroll.clip_contents = true
	_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	_scroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_SHOW_NEVER
	_scroll.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_card.add_child(_scroll)

	_col = VBoxContainer.new()
	_col.custom_minimum_size = Vector2(888, 0)
	_col.add_theme_constant_override("separation", 22)
	_col.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_scroll.add_child(_col)

	_bar = _PenLine.new()
	_bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_bar.position = Vector2(160, CARD_H - FOOTER_H + 22.0)
	_bar.set_deferred("size", Vector2(760, 26))
	_card.add_child(_bar)

	_ready_hint = _mk("the week is still developing... (click to catch up)", SIZE_LABEL, Color(INK, 0.4), HORIZONTAL_ALIGNMENT_CENTER)
	_ready_hint.position = Vector2(0, CARD_H - FOOTER_H + 54.0)
	_ready_hint.custom_minimum_size = Vector2(1080, 0)
	_ready_hint.set_deferred("size", Vector2(1080, 0))
	_card.add_child(_ready_hint)

	_card.modulate.a = 0.0
	create_tween().tween_property(_card, "modulate:a", 1.0, 0.24)
	set_process(true)
	# A READER IS NEVER HELD (the journal's own law, now the beat's too): one
	# click lands every remaining line instantly. The art keeps its own time —
	# skipping the words never skips the render.
	mouse_filter = Control.MOUSE_FILTER_STOP
	gui_input.connect(func(ev: InputEvent) -> void:
		if ev is InputEventMouseButton and ev.pressed:
			_skip_reading())

## Queue one beat of the consequence chain. `label` is the small blue-grey lead-in
## ("You said", "They heard", "What happened"); pass "" for a bare paragraph.
## Beats are revealed in order, paced to reading speed, so the page writes itself.
func say(label: String, body: String) -> void:
	if body.strip_edges() == "":
		return
	_reveal_queue.append({"label": label, "body": body})

## Real progress on the art, 0..1. Optional — without it the stroke creeps and waits.
func report(p: float) -> void:
	_target = clampf(p, 0.0, 1.0)

## Wait until the reader has had the whole text AND the art is done, then open the
## scene. Never returns before MIN_LIFE, never holds a reader who is finished.
func finish() -> void:
	_target = 1.0
	while _t < MIN_LIFE or not _reveal_queue.is_empty() or _bar.amount < 0.995:
		await get_tree().process_frame
	_ready_hint.text = "look up"
	_ready_hint.add_theme_color_override("font_color", PEN)
	await get_tree().create_timer(0.55).timeout
	_done = true
	var tw := create_tween()
	tw.tween_property(self, "modulate:a", 0.0, 0.30)
	await tw.finished
	closed.emit()
	queue_free()

func _process(delta: float) -> void:
	if _done:
		return
	_t += delta
	# reveal the next beat when the previous one has had time to be read
	if not _reveal_queue.is_empty() and _t >= _next_reveal_at:
		var beat: Dictionary = _reveal_queue.pop_front()
		_reveal(beat)
		_next_reveal_at = _t + clampf(String(beat["body"]).length() / READ_CPS, 1.2, 9.0)
	var goal: float = _target if _target > 0.0 else minf(0.92, _t / 60.0)
	_bar.amount = move_toward(_bar.amount, goal, delta * 0.5)
	_bar.queue_redraw()

func _reveal(beat: Dictionary) -> void:
	var block := VBoxContainer.new()
	block.add_theme_constant_override("separation", 4)
	block.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_col.add_child(block)
	var lbl := String(beat["label"])
	if lbl != "":
		var l := _mk(lbl, SIZE_LABEL, Color(INK, 0.45), HORIZONTAL_ALIGNMENT_LEFT)
		block.add_child(l)
	var b := _mk(String(beat["body"]), SIZE_BODY, INK, HORIZONTAL_ALIGNMENT_LEFT)
	b.custom_minimum_size = Vector2(888, 0)
	block.add_child(b)
	# WRITTEN, not faded: the beat is the week being put down in ink, so each
	# block writes itself in at a hand's pace with the paper scratching under it.
	b.visible_ratio = 0.0
	_bodies.append(b)
	var secs: float = clampf(String(beat["body"]).length() / 95.0, 0.3, 6.5)
	_scratch_on()
	var tw := create_tween()
	tw.tween_property(b, "visible_ratio", 1.0, secs)
	tw.tween_callback(_scratch_off)
	# keep the newest beat in view: the reader follows the pen down the page
	await get_tree().process_frame
	await get_tree().process_frame
	if is_instance_valid(_scroll):
		# snap the scroll to a WHOLE line: stopping mid-line leaves the top row of
		# glyphs half-clipped ("says sne aoes noT wanT" in the capture), which reads
		# as a rendering bug rather than a scroll position
		var line_h := float(SIZE_BODY) * 1.5
		var to: float = maxf(0.0, _col.size.y - _scroll.size.y)
		to = ceilf(to / line_h) * line_h
		var st := create_tween()
		st.tween_property(_scroll, "scroll_vertical", int(to), 0.45).set_trans(Tween.TRANS_SINE)

## Everything lands NOW: queued beats spawn, writing lines complete, the clock
## considers the reading done. Only the render (the bar) keeps its own pace.
func _skip_reading() -> void:
	while not _reveal_queue.is_empty():
		_reveal(_reveal_queue.pop_front())
	for b in _bodies:
		if is_instance_valid(b):
			(b as Label).visible_ratio = 1.0
	_writing = 0
	if _scratch != null and _scratch.playing:
		_scratch.stop()
	_t = maxf(_t, MIN_LIFE)
	_next_reveal_at = 0.0

func _scratch_on() -> void:
	_writing += 1
	if _scratch == null and FileAccess.file_exists("res://assets/sfx/pen_scratch.wav"):
		var st: AudioStream = load("res://assets/sfx/pen_scratch.wav")
		if st is AudioStreamWAV:
			(st as AudioStreamWAV).loop_mode = AudioStreamWAV.LOOP_FORWARD
		_scratch = AudioStreamPlayer.new()
		_scratch.stream = st
		_scratch.volume_db = -16.0
		add_child(_scratch)
	if _scratch != null and not _scratch.playing:
		_scratch.play()

func _scratch_off() -> void:
	_writing = maxi(_writing - 1, 0)
	if _writing == 0 and _scratch != null and _scratch.playing:
		_scratch.stop()

func _mk(text: String, sz: int, col: Color, align: int) -> Label:
	var l := Label.new()
	# flags before size, always
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.horizontal_alignment = align
	l.add_theme_font_override("font", _font)
	l.add_theme_font_size_override("font_size", sz)
	l.add_theme_color_override("font_color", col)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	l.text = text
	return l

class _Paper:
	extends Control
	func _draw() -> void:
		var w := size.x
		var h := size.y
		draw_rect(Rect2(9, 11, w, h), Color(0, 0, 0, 0.24))
		draw_rect(Rect2(0, 0, w, h), LoadingScreen.CREAM)
		# faint ruling, so the text reads as written on paper
		var y := 132.0
		while y < h - 90.0:
			draw_line(Vector2(84, y), Vector2(w - 84, y), Color("8FA582", 0.30), 1.5)
			y += 44.0
		# one continuous wobble around the perimeter: per-edge wobble spikes the corners
		var rng := RandomNumberGenerator.new()
		rng.seed = 7
		var pts := PackedVector2Array()
		var corners := [Vector2(4, 4), Vector2(w - 4, 4), Vector2(w - 4, h - 4), Vector2(4, h - 4)]
		for i in 4:
			var a: Vector2 = corners[i]
			var b: Vector2 = corners[(i + 1) % 4]
			var n := Vector2(b.y - a.y, a.x - b.x).normalized()
			for k in 20:
				pts.append(a.lerp(b, float(k) / 20.0) + n * rng.randf_range(-1.7, 1.7))
		pts.append(pts[0])
		draw_polyline(pts, LoadingScreen.INK, 3.0, true)

class _PenLine:
	extends Control
	var amount := 0.0
	func _draw() -> void:
		var rng := RandomNumberGenerator.new()
		rng.seed = 3
		var full := PackedVector2Array()
		for i in 61:
			var t := float(i) / 60.0
			full.append(Vector2(size.x * t, size.y * 0.5 + rng.randf_range(-2.2, 2.2)))
		draw_polyline(full, Color(LoadingScreen.INK, 0.13), 3.0, true)
		if amount <= 0.01:
			return
		var upto := PackedVector2Array()
		for p in full:
			if p.x <= size.x * amount:
				upto.append(p)
		if upto.size() >= 2:
			draw_polyline(upto, LoadingScreen.PEN, 5.0, true)
			draw_circle(upto[upto.size() - 1], 5.0, LoadingScreen.INK)
