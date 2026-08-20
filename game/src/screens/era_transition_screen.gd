class_name EraTransitionScreen
extends Control
## THE MOVE (LANE-WIRING) — the beat between eras, told as a page of the log book.
##
## THERE IS ONE BOOK IN THIS GAME AND IT IS THE PORTRAIT NOTEBOOK. This screen used
## to draw its own landscape two-page book lying open on a bench, and it was the last
## page still doing that after every other one moved to the shared sheet. It now
## instantiates JournalPage like the rest: the portrait sheet, the live room of the
## era you are moving INTO showing past its edges, one hand, two sizes, writing that
## sits on the printed rules.
##
## The shell also settles the overflow at the source. The old page laid its own
## labels against a rectangle drawn over paper that is really a trapezoid, so
## "40 desks (+28 off the page)" printed its last word across the garage's blue
## cabinet. Every word here is measured and wrapped by the shell to the sheet's own
## span, so text cannot leave the paper by construction rather than by a clamp
## applied afterwards.
##
## Per the 60 Seconds! model STATE IS STILL DRAWN, not typeset: rent as banknotes,
## the staff cap as chairs, gained in ink and lost struck out. Those two drawings
## are the only thing this screen adds, and they go INSIDE the shell's own rows so
## the sheet keeps the accounting.
##
## THE PAGE'S ANATOMY, in the shell's zones and the rules each one holds:
##   TITLE    the era you are moving into
##   BODY     why you moved, and what you take away from it — one written sentence
##   ENDING   the two drawn rows: what it costs, and how many desks you can fill
##   CONTROLS the commit, written beside the shell's own corner arrow
## The rule counts below are chosen so no zone is ever filled past its last rule,
## because a block that ends ON the boundary still adds its trailing gap and pushes
## the page into an overrun warning.
##
## Chrome is the week plate alone, on the run's standard HUD anchor.

signal done

## Only the DRAWN state carries colour; the writing is the shell's single hand.
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
## BODY holds four printed rules; three of them may be written on, because a block
## that lands on the fourth still adds its gap and overruns the zone.
const BODY_RULES := 3
## The drawn things, sized to the band the shell reserves (ICON_MIN_H) rather than
## to the old landscape spread — the portrait sheet is a third narrower.
const NOTE := Vector2(70, 46)
const NOTE_STEP := 80.0
const CHAIR := Vector2(56, 58)
const CHAIR_STEP := 62.0
## The most chairs worth drawing before the row just means "a lot". The sheet's
## width cuts it further, and the caption owns up to whatever did not get drawn.
const CHAIRS_MAX := 12

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
var _sheet: JournalPage
var _cell_w := 0.0       ## width of a full-span cell inside the sheet's margins
var _band_h := 0.0       ## height of the drawn band the shell reserves above a caption

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

	# THE SHEET, over the room you are moving INTO. It goes into the tree BEFORE it
	# is built: the shell anchors itself full-rect, so it has to have a parent to
	# lay out against.
	_sheet = JournalPage.new()
	add_child(_sheet)
	_sheet.build(era_label.to_upper(), _room_scene_id())
	var sp := _sheet.span_at(0.0)
	_cell_w = maxf(sp.y - sp.x - 14.0, 60.0)   # the shell's own cap_w for one item
	_band_h = JournalPage.ICON_MIN_H
	if not moving_up:
		_cold_light()

	_sheet.line(_body_line())
	_row_rent()
	_row_desks()
	_commit()

	_week_plate()
	_open()
	await get_tree().create_timer(1.0).timeout
	_armed = true

## The room the move lands you in, checked against the disk on the way down so a
## scene the art lane has not produced yet degrades instead of blanking the beat.
func _room_scene_id() -> String:
	for id in [String(SceneRoomPicker.ERA_STAGE.get(to_era, "")),
			String(SceneRoomPicker.ERA_BASE.get(to_era, "")), "stage_garage", "garage"]:
		if id != "" and SceneRoomPicker.has_scene(id):
			return id
	return ""

## A demotion is the same sheet in worse light. The wash goes on the ROOM, under the
## paper, so the writing keeps its contrast while the place goes cold. Short on
## purpose: a slow scrim means the beat spends most of a second underlit, and
## anything photographing the screen on a timer catches it there.
func _cold_light() -> void:
	if _sheet.room == null:
		return
	var cold := ColorRect.new()
	cold.color = Color(0.10, 0.12, 0.20, 0.0)
	cold.size = Vector2(1536, 1024)
	cold.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_sheet.room.add_child(cold)
	create_tween().tween_property(cold, "color:a", 0.34, 0.3)

## Why you moved and what you take away from it, written as one entry in the
## founder's own hand. If the sheet cannot hold both, THE KEEPSAKE GOES FIRST: the
## page can lose the souvenir, and only after that does it start cutting the reason.
## The rent and the desk count are never in this sentence, so neither can be cut.
func _body_line() -> String:
	var why := reason
	if why == "":
		why = "you outgrew the last place" if moving_up else "you could not carry the rent"
	var head := "We moved up. " if moving_up else "We packed the boxes. "
	var keep := _capped(String(MEMORABILIA.get(to_era, "a door that locks"))) if moving_up \
		else "Morale −25, and the boxes came back out"
	var short := "%s%s." % [head, _capped(why)]
	var full := "%s %s." % [short, keep]
	if _rules_of(full) <= BODY_RULES:
		return full
	if _rules_of(short) <= BODY_RULES:
		return short
	return _trim(short, BODY_RULES)

func _capped(s: String) -> String:
	return s if s == "" else s.substr(0, 1).to_upper() + s.substr(1)

## How many printed rules a sentence needs at the sheet's own width, measured with
## the real font — the same call the shell wraps with, not a character count.
func _rules_of(text: String) -> int:
	var one: float = maxf(_hand.get_height(JournalPage.SIZE_BODY), 1.0)
	return int(round(_hand.get_multiline_string_size(
		text, HORIZONTAL_ALIGNMENT_LEFT, maxf(_cell_w, 60.0), JournalPage.SIZE_BODY).y / one))

## Drop trailing words until the sentence fits the rules it is allowed.
func _trim(text: String, rules: int) -> String:
	var out := text
	while out.length() > 12 and _rules_of(out) > rules:
		var cut := out.rstrip(".").rfind(" ")
		if cut <= 0:
			break
		out = out.substr(0, cut) + "."
	return out

## Rent, drawn: one note for what the last place cost, and the multiple you now owe
## every week. The row is a single cell of the shell's own icon row, so the figure
## under it is measured and wrapped to the sheet like every other word on the page.
func _row_rent() -> void:
	var mult := 1
	if moving_up and old_rent > 0:
		mult = clampi(int(round(float(new_rent) / float(old_rent))), 2, 7)
	elif not moving_up and new_rent > 0:
		mult = clampi(int(round(float(old_rent) / float(new_rent))), 2, 7)
	var band := _band("rent", "$%s / week" % _fmt(new_rent), "")
	if band == null:
		return
	var notes: int = clampi(int((_cell_w + 10.0) / NOTE_STEP), 1, mult)
	var x: float = (_cell_w - (float(notes) * NOTE_STEP - 10.0)) * 0.5
	for i in notes:
		var bill := _Ink.new()
		bill.kind = "bill"
		# moving up: the first note is what you paid, the rest is the new weight
		bill.col = PALETTE["ink"] if (i == 0) else (PALETTE["coral"] if moving_up else PALETTE["faded"])
		bill.solid = (i == 0) if moving_up else (i < notes - 1)
		bill.jitter = float(i) * 0.9
		bill.position = Vector2(x, (_band_h - NOTE.y) * 0.5)
		bill.size = NOTE
		band.add_child(bill)
		_pop(bill, 0.24 + i * 0.03)
		x += NOTE_STEP

## The staff cap, drawn: chairs you already filled in ink, the new ones in red, the
## ones you just lost struck through. HOW MANY FIT IS DECIDED BY THE SHEET and the
## caption owns up to the rest — which is why the count can never walk off the page.
func _row_desks() -> void:
	var drawn := new_cap if moving_up else old_cap
	var seats: int = clampi(int((_cell_w + 6.0) / CHAIR_STEP), 1, mini(drawn, CHAIRS_MAX))
	var band := _band("desks", _desk_tail(seats, drawn), "%d desks" % new_cap)
	if band == null:
		return
	var x: float = (_cell_w - (float(seats) * CHAIR_STEP - 6.0)) * 0.5
	for i in seats:
		var ch := _Ink.new()
		ch.kind = "chair"
		var kept := i < new_cap
		ch.col = PALETTE["ink"] if (kept if not moving_up else i < old_cap) else PALETTE["coral"]
		ch.solid = i < mini(old_cap, new_cap)
		ch.lost = not moving_up and not kept   # a desk you no longer get to fill
		ch.jitter = float(i) * 1.3
		ch.position = Vector2(x, (_band_h - CHAIR.y) * 0.5)
		ch.size = CHAIR
		band.add_child(ch)
		_pop(ch, 0.34 + i * 0.022)
		x += CHAIR_STEP

## What the chair row says out loud. The sheet caps how many chairs get drawn, so
## the sentence has to account for the ones that did not.
func _desk_tail(seats: int, drawn: int) -> String:
	if not moving_up:
		return "%d desks — %d gone" % [new_cap, old_cap - new_cap]
	if drawn > seats:
		return "%d desks (+%d off the page)" % [new_cap, drawn - seats]
	return "%d desks" % new_cap

## ONE DRAWN ROW, THE SHELL'S WAY.
##
## The shell only reserves a picture band for a row that declares a picture, and it
## will not size one under ICON_MIN_H — that rule is what stopped this game shipping
## 10px icons, so it is not one to route around. The row therefore declares a plate
## and the plate is empty: the picture on this page is drawn in pen, over the band,
## instead of blitted from a bitmap. The shell still owns the band, the caption, the
## wrapping and the zone accounting.
##
## `fallback` is a shorter caption used if the full one would wrap: a two-line
## caption doubles the row and is what tips the ENDING zone into an overrun.
func _band(id: String, caption: String, fallback: String) -> Control:
	var text := caption
	if fallback != "" and _caption_rules(text) > 1:
		text = fallback
	var row := _sheet.icon_row([{"id": id, "text": text, "tex": _plate()}], Vector2(9999, 0))
	if row == null or row.get_child_count() == 0:
		return null
	var slot := row.get_child(0) as Control
	if slot == null:
		return null
	slot.mouse_filter = Control.MOUSE_FILTER_IGNORE   # nothing on this page is pickable
	return slot

## Captions are centred by the shell, so they are measured centred too.
func _caption_rules(text: String) -> int:
	var one: float = maxf(_hand.get_height(JournalPage.SIZE_BODY), 1.0)
	return int(round(_hand.get_multiline_string_size(
		text, HORIZONTAL_ALIGNMENT_CENTER, maxf(_cell_w, 60.0), JournalPage.SIZE_BODY).y / one))

## An empty plate: it reserves the band and draws nothing, because the ink goes on
## top of it.
func _plate() -> Texture2D:
	var img := Image.create(1, 1, false, Image.FORMAT_RGBA8)
	img.fill(Color(0, 0, 0, 0))
	return ImageTexture.create_from_image(img)

## THE COMMIT, in the CONTROLS zone, as a mark on the paper rather than a button
## floating over the room. It is a row of one: the shell centres the words, circles
## them in pen when they are picked, and reports the pick. The corner arrow is not
## used here — the shell parks it low enough that the page's own curled corner sits
## over it, which is no affordance at all on the one page that has a single choice.
func _commit() -> void:
	_sheet.icon_row([{"id": "go", "text": "SETTLE IN  →" if moving_up else "PACK THE BOXES  →"}],
		Vector2(9999, 0), "controls")
	_sheet.choice_made.connect(func(_id: String) -> void: _finish())

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
	var l := Label.new()
	l.add_theme_font_override("font", _font)
	l.add_theme_font_size_override("font_size", 28)
	l.add_theme_color_override("font_color", PALETTE["ink"])
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	l.text = "WEEK %d · %s" % [week, "MOVING UP" if moving_up else "MOVING OUT"]
	l.position = Vector2(16, 4)
	l.size = Vector2(400, 44)
	plate.add_child(l)

## The sheet arrives over the room the run was just in. FAST ON PURPOSE: the old
## leaf sweep ran 0.45s before a 0.3s fade even started, and the last chair landed at
## 1.3s, so for most of a second and a half the beat was a dark, half-drawn page —
## which is exactly the state anything shooting the screen on a timer catches. Every
## mark is down inside 0.75s now.
func _open() -> void:
	_sheet.modulate.a = 0.0
	var sfx := AudioStreamPlayer.new()
	sfx.stream = load("res://assets/sfx/card_flip.wav")
	add_child(sfx)
	sfx.play()
	var tw := create_tween()
	tw.tween_property(_sheet, "modulate:a", 1.0, 0.22)
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

## The state on this page is drawn in pen, not typeset: banknotes for rent, chairs
## for the staff cap.
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
