class_name EraTransitionScreen
extends Control
## THE MOVE (LANE-WIRING) — the beat between eras, told as a page of the log book.
##
## Not a card overlay: a leaf turns and the next page of the founder's own book is
## the move. Per the 60 Seconds! model the page carries one hand-lettered title,
## one short prompt, and then STATE IS DRAWN — rent as banknotes, the staff cap as
## chairs, what you earned as a pinned medal. The corner arrows are painted into
## the scene art; the only chrome is the week plate and the standard CTA.
##
## Safe zones (owner law): week plate 24,14 430x52 · CTA 560,930 420x76 · every
## drawn element inside the page itself, y200-800.

signal done

const PALETTE := {
	"cream": Color("F2EAD3"), "ink": Color("1E1E1E"), "coral": Color("E86A5C"),
	"yellow": Color("F4B942"), "sage": Color("8FA582"), "blue": Color("6E8CA0"),
	"faded": Color(0.42, 0.40, 0.36),
}
## What you earn the right to put on the wall, one era at a time.
const MEMORABILIA := {
	"coworking": "a CAMP pennant for the wall",
	"office": "your name on a glass door",
	"floor": "a foosball table nobody asked for",
	"hq": "a lobby big enough to echo",
}
## The facing page inside journal_page/scene.png, measured off the art.
const PAGE := Rect2(340, 296, 890, 424)

var to_era := ""
var from_era := ""
var reason := ""
var moving_up := true
var week := 1
var new_rent := 0
var old_rent := 0
var new_cap := 0
var old_cap := 0
var era_label := ""

var _font: Font
var _hand: Font
var _armed := false
var _page: Control

func setup(p: Dictionary) -> void:
	to_era = String(p.get("to", "coworking"))
	from_era = String(p.get("from", "garage"))
	reason = String(p.get("reason", ""))
	moving_up = bool(p.get("up", true))
	week = int(p.get("week", 1))
	new_rent = int(p.get("new_rent", 0))
	old_rent = int(p.get("old_rent", 0))
	new_cap = int(p.get("new_cap", 0))
	old_cap = int(p.get("old_cap", 0))
	era_label = String(p.get("era_label", to_era))

func _ready() -> void:
	_font = load("res://assets/fonts/Baloo2-Bold.ttf")
	_hand = load("res://assets/fonts/PatrickHand-Regular.ttf")
	size = Vector2(1536, 1024)

	_backdrop()
	_page = Control.new()
	_page.position = Vector2.ZERO
	_page.size = Vector2(1536, 1024)
	_page.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_page.pivot_offset = PAGE.get_center()
	_page.rotation = 0.008   # sit with the book, which is not square to the camera
	add_child(_page)

	var y := _write_head()
	y = _row_rent(y)
	y = _row_desks(y)
	_row_earned(y)

	_week_plate()
	_cta()
	_turn_the_leaf()
	await get_tree().create_timer(1.0).timeout
	_armed = true

## The room with the book open on the bench. If the art is missing the page still
## reads: paper on a dark ground rather than a blank screen.
func _backdrop() -> void:
	var ground := ColorRect.new()
	ground.color = Color(0.16, 0.15, 0.14)
	ground.size = Vector2(1536, 1024)
	ground.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(ground)
	if SceneRoomPicker.has_scene("journal_page"):
		var room := SceneRoom.new()
		room.size = Vector2(1536, 1024)
		add_child(room)
		room.load_scene("journal_page")
	else:
		var paper := ColorRect.new()
		paper.color = PALETTE["cream"]
		paper.position = PAGE.position - Vector2(30, 40)
		paper.size = PAGE.size + Vector2(60, 90)
		paper.mouse_filter = Control.MOUSE_FILTER_IGNORE
		add_child(paper)
	# a demotion is the same book in worse light
	if not moving_up:
		var cold := ColorRect.new()
		cold.color = Color(0.10, 0.12, 0.20, 0.0)
		cold.size = Vector2(1536, 1024)
		cold.mouse_filter = Control.MOUSE_FILTER_IGNORE
		add_child(cold)
		create_tween().tween_property(cold, "color:a", 0.34, 0.8)

## Title and the one prompt under it. Returns the y to keep writing from.
func _write_head() -> float:
	var title := _ink(era_label.to_upper(), 70, PALETTE["ink"] if moving_up else PALETTE["coral"], _hand)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.position = Vector2(PAGE.position.x, PAGE.position.y - 2)
	title.size = Vector2(PAGE.size.x, 96)

	# the rule sits UNDER the title, never through it
	var rule := _Ink.new()
	rule.kind = "rule"
	rule.col = PALETTE["coral"]
	rule.position = Vector2(PAGE.position.x + PAGE.size.x * 0.5 - 150, PAGE.position.y + 88)
	rule.size = Vector2(300, 14)
	_page.add_child(rule)

	var line := reason
	if line == "":
		line = "you outgrew the last place" if moving_up else "you could not carry the rent"
	line = line.substr(0, 1).to_upper() + line.substr(1)   # a sentence, not a headline
	var sub := _ink(("We moved up. %s." % line) if moving_up
			else ("We packed the boxes. %s." % line), 30, PALETTE["faded"], _hand)
	sub.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	sub.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	sub.position = Vector2(PAGE.position.x + 40, PAGE.position.y + 108)
	sub.size = Vector2(PAGE.size.x - 80, 48)
	return PAGE.position.y + 160

## Rent, drawn: one note for what the last place cost, and the multiple you now
## owe every week beside it in red.
func _row_rent(y: float) -> float:
	var label := _ink("what it costs now...", 27, PALETTE["blue"], _hand)
	label.position = Vector2(PAGE.position.x + 26, y)
	label.size = Vector2(420, 38)
	var mult := 1
	if moving_up and old_rent > 0:
		mult = clampi(int(round(float(new_rent) / float(old_rent))), 2, 7)
	elif not moving_up and new_rent > 0:
		mult = clampi(int(round(float(old_rent) / float(new_rent))), 2, 7)
	var x := PAGE.position.x + 30.0
	for i in mult:
		var bill := _Ink.new()
		bill.kind = "bill"
		# moving up: the first note is what you paid, the rest is the new weight
		bill.col = PALETTE["ink"] if (i == 0) else (PALETTE["coral"] if moving_up else PALETTE["faded"])
		bill.solid = (i == 0) if moving_up else (i < mult - 1)
		bill.jitter = float(i) * 0.9
		bill.position = Vector2(x, y + 36)
		bill.size = Vector2(62, 40)
		_page.add_child(bill)
		_pop(bill, 0.55 + i * 0.05)
		x += 70
	var fig := _ink("$%s / week" % _fmt(new_rent), 34, PALETTE["ink"], _hand)
	fig.position = Vector2(x + 16, y + 32)
	fig.size = Vector2(300, 50)
	return y + 88

## The staff cap, drawn: chairs you already filled in ink, the new ones in red.
func _row_desks(y: float) -> float:
	var label := _ink("desks we can fill...", 27, PALETTE["blue"], _hand)
	label.position = Vector2(PAGE.position.x + 26, y)
	label.size = Vector2(420, 34)
	var drawn := new_cap if moving_up else old_cap
	var shown := mini(drawn, 12)
	var x := PAGE.position.x + 30.0
	for i in shown:
		var ch := _Ink.new()
		ch.kind = "chair"
		var kept := i < new_cap
		ch.col = PALETTE["ink"] if (kept if not moving_up else i < old_cap) else PALETTE["coral"]
		ch.solid = i < mini(old_cap, new_cap)
		ch.lost = not moving_up and not kept   # a desk you no longer get to fill
		ch.jitter = float(i) * 1.3
		ch.position = Vector2(x, y + 34)
		ch.size = Vector2(46, 48)
		_page.add_child(ch)
		_pop(ch, 0.7 + i * 0.04)
		x += 52
	var tail := "%d desks" % new_cap
	if drawn > shown:
		tail = "%d desks (+%d off the page)" % [new_cap, drawn - shown]
	elif not moving_up:
		tail = "%d desks — %d gone" % [new_cap, old_cap - new_cap]
	var fig := _ink(tail, 34, PALETTE["ink"], _hand)
	fig.position = Vector2(x + 16, y + 38)
	fig.size = Vector2(380, 50)
	return y + 90

## The one thing you take away from the move: pinned to the page like a keepsake.
func _row_earned(y: float) -> void:
	var icon := _Ink.new()
	icon.kind = "medal" if moving_up else "box"
	icon.col = PALETTE["sage"] if moving_up else PALETTE["coral"]
	icon.solid = true
	icon.position = Vector2(PAGE.position.x + 34, y)
	icon.size = Vector2(52, 54)
	_page.add_child(icon)
	_pop(icon, 1.0)
	var text := String(MEMORABILIA.get(to_era, "a door that locks")) if moving_up \
		else "morale −25. The boxes came back out."
	var cap := _ink(text, 32, PALETTE["ink"] if moving_up else PALETTE["coral"], _hand)
	cap.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	cap.position = Vector2(PAGE.position.x + 100, y + 6)
	cap.size = Vector2(PAGE.size.x - 150, 72)

## The week plate — same anchor as the run's HUD so the screens read as one game.
func _week_plate() -> void:
	var plate := Panel.new()
	plate.position = Vector2(24, 14)
	plate.size = Vector2(430, 52)
	plate.rotation = -0.004
	var st := StyleBoxFlat.new()
	st.bg_color = PALETTE["cream"]
	st.border_color = PALETTE["ink"]
	st.set_border_width_all(3)
	st.set_corner_radius_all(10)
	plate.add_theme_stylebox_override("panel", st)
	add_child(plate)
	var l := _ink("WEEK %d · %s" % [week, "MOVING UP" if moving_up else "MOVING OUT"],
		28, PALETTE["ink"], _font, plate)
	l.position = Vector2(16, 4)
	l.size = Vector2(400, 44)

## The page's own affordance sits at the game's standard CTA anchor, but wearing
## paper and ink rather than a UI button.
func _cta() -> void:
	var cta := Button.new()
	cta.text = ("SETTLE IN  →" if moving_up else "PACK THE BOXES  →")
	cta.position = Vector2(560, 930)
	cta.size = Vector2(420, 76)
	cta.pivot_offset = Vector2(210, 38)
	cta.add_theme_font_override("font", _font)
	cta.add_theme_font_size_override("font_size", 32)
	cta.add_theme_color_override("font_color", PALETTE["ink"])
	cta.add_theme_color_override("font_hover_color", PALETTE["ink"])
	var st := StyleBoxFlat.new()
	st.bg_color = PALETTE["cream"]
	st.border_color = PALETTE["ink"]
	st.set_border_width_all(4)
	st.set_corner_radius_all(14)
	cta.add_theme_stylebox_override("normal", st)
	var hov := st.duplicate() as StyleBoxFlat
	hov.bg_color = PALETTE["yellow"]
	cta.add_theme_stylebox_override("hover", hov)
	cta.add_theme_stylebox_override("pressed", hov)
	cta.pressed.connect(_finish)
	add_child(cta)
	var pulse := create_tween().set_loops()
	pulse.tween_property(cta, "scale", Vector2(1.03, 1.03), 0.7).set_trans(Tween.TRANS_SINE)
	pulse.tween_property(cta, "scale", Vector2.ONE, 0.7).set_trans(Tween.TRANS_SINE)

## A leaf sweeps left off the page and the move is written underneath it.
func _turn_the_leaf() -> void:
	_page.modulate.a = 0.0
	var leaf := ColorRect.new()
	leaf.color = Color(0.93, 0.90, 0.80)
	leaf.position = PAGE.position - Vector2(34, 44)
	leaf.size = PAGE.size + Vector2(64, 96)
	leaf.pivot_offset = Vector2(0, leaf.size.y * 0.5)
	leaf.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(leaf)
	var sfx := AudioStreamPlayer.new()
	sfx.stream = load("res://assets/sfx/card_flip.wav")
	add_child(sfx)
	sfx.play()
	var tw := create_tween()
	tw.tween_property(leaf, "scale", Vector2(0.02, 1.0), 0.45).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
	tw.parallel().tween_property(leaf, "rotation", -0.05, 0.45)
	tw.tween_callback(leaf.queue_free)
	tw.parallel().tween_property(_page, "modulate:a", 1.0, 0.3)
	tw.tween_callback(func():
		var s2 := AudioStreamPlayer.new()
		s2.stream = load("res://assets/sfx/%s.wav" % ("win" if moving_up else "deposit"))
		add_child(s2)
		s2.play())

func _pop(node: Control, delay: float) -> void:
	node.modulate.a = 0.0
	var tw := create_tween()
	tw.tween_interval(delay)
	tw.tween_property(node, "modulate:a", 1.0, 0.16)

func _ink(text: String, px: int, col: Color, font: Font, parent: Node = null) -> Label:
	var l := Label.new()
	l.add_theme_font_override("font", font)
	l.add_theme_font_size_override("font_size", px)
	l.add_theme_color_override("font_color", col)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	l.text = text
	(parent if parent != null else _page).add_child(l)
	return l

func _fmt(v: int) -> String:
	var t := str(absi(v))
	var out := ""
	while t.length() > 3:
		out = "," + t.substr(t.length() - 3) + out
		t = t.substr(0, t.length() - 3)
	return ("-" if v < 0 else "") + t + out

func _finish() -> void:
	if not _armed:
		return
	_armed = false
	done.emit()
	queue_free()

func _unhandled_input(event: InputEvent) -> void:
	if _armed and event is InputEventKey and event.pressed:
		accept_event()
		_finish()

## Everything on this page is drawn in pen, not typeset: banknotes for rent,
## chairs for the staff cap, a medal for what you earned, a box for what you lost.
class _Ink extends Control:
	var kind := "bill"
	var col := Color("1E1E1E")
	var solid := false
	var lost := false
	var jitter := 0.0

	func _ready() -> void:
		mouse_filter = Control.MOUSE_FILTER_IGNORE
		pivot_offset = size * 0.5
		rotation = sin(jitter) * 0.05   # nothing hand-drawn is square

	func _draw() -> void:
		var w := size.x
		var h := size.y
		var fill := Color(col, 0.18)
		if lost:
			modulate.a = 0.55
		match kind:
			"rule":
				var pts := PackedVector2Array()
				for i in 22:
					var t := float(i) / 21.0
					pts.append(Vector2(t * w, h * 0.5 + sin(t * 7.0 + jitter) * 2.4))
				draw_polyline(pts, col, 5.0)
			"bill":
				var r := Rect2(2, h * 0.14, w - 4, h * 0.70)
				if solid:
					draw_rect(r, fill, true)
				draw_rect(r, col, false, 2.6)
				draw_arc(Vector2(w * 0.5, h * 0.49), h * 0.17, 0, TAU, 20, col, 2.4)
				draw_line(Vector2(w * 0.12, h * 0.49), Vector2(w * 0.26, h * 0.49), col, 2.0)
				draw_line(Vector2(w * 0.74, h * 0.49), Vector2(w * 0.88, h * 0.49), col, 2.0)
			"chair":
				draw_line(Vector2(w * 0.26, h * 0.08), Vector2(w * 0.26, h * 0.56), col, 2.8)
				draw_line(Vector2(w * 0.26, h * 0.08), Vector2(w * 0.66, h * 0.14), col, 2.8)
				var seat := PackedVector2Array([
					Vector2(w * 0.16, h * 0.56), Vector2(w * 0.86, h * 0.56),
					Vector2(w * 0.76, h * 0.68), Vector2(w * 0.24, h * 0.68),
					Vector2(w * 0.16, h * 0.56)])
				if solid:
					draw_colored_polygon(seat, fill)
				draw_polyline(seat, col, 2.6)
				draw_line(Vector2(w * 0.28, h * 0.68), Vector2(w * 0.22, h * 0.94), col, 2.4)
				draw_line(Vector2(w * 0.74, h * 0.68), Vector2(w * 0.82, h * 0.94), col, 2.4)
				if lost:
					draw_line(Vector2(w * 0.10, h * 0.14), Vector2(w * 0.92, h * 0.90), Color("1E1E1E"), 3.0)
					draw_line(Vector2(w * 0.92, h * 0.14), Vector2(w * 0.10, h * 0.90), Color("1E1E1E"), 3.0)
			"medal":
				draw_line(Vector2(w * 0.34, h * 0.60), Vector2(w * 0.24, h * 0.96), col, 2.8)
				draw_line(Vector2(w * 0.66, h * 0.60), Vector2(w * 0.76, h * 0.96), col, 2.8)
				draw_circle(Vector2(w * 0.5, h * 0.40), h * 0.28, fill)
				draw_arc(Vector2(w * 0.5, h * 0.40), h * 0.28, 0, TAU, 28, col, 3.0)
				draw_arc(Vector2(w * 0.5, h * 0.40), h * 0.13, 0, TAU, 18, Color(col, 0.75), 2.2)
			"box":
				var b := Rect2(w * 0.08, h * 0.36, w * 0.84, h * 0.54)
				draw_rect(b, fill, true)
				draw_rect(b, col, false, 2.8)
				draw_line(Vector2(w * 0.08, h * 0.36), Vector2(w * 0.5, h * 0.16), col, 2.6)
				draw_line(Vector2(w * 0.92, h * 0.36), Vector2(w * 0.5, h * 0.16), col, 2.6)
				draw_line(Vector2(w * 0.5, h * 0.36), Vector2(w * 0.5, h * 0.90), Color(col, 0.6), 2.2)
