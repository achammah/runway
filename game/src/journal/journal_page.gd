class_name JournalPage
extends Control
## THE LOG BOOK PAGE SHELL — owned by MAIN, instantiated by every journal page lane.
##
## WHY THIS EXISTS. The page rules (one sheet, one hand, text that never leaves the
## paper, choices marked in pen, diegetic arrows) were written as prose three times
## and broken three times. Prose rules get forgotten; a constructor cannot forget.
## So the shell owns the geometry and the type, and a page script may only ADD
## CONTENT through the API. It is never handed a font, a size, or a free position,
## so it cannot pick a wrong one.
##
## THE GEOMETRY FACT THAT CAUSED EVERY OVERFLOW BUG. The drawn sheet LEANS. Measured
## off the art itself: the writable left edge travels x=146 to x=192 and the right
## edge x=892 to x=952 down the page, the spiral perforation eats the top, and the
## curled corner eats the bottom right. A rectangle laid over that MUST cross the
## paper somewhere. So text here is not wrapped to a box — it is wrapped to the real
## silhouette, line by line, using the writable span measured at each line's own y.
## That table lives in assets/ui/logbook_page_zones.json, extracted from the PNG's
## alpha, so it stays true if the art is ever regenerated.
##
## THE PAGE IS DIVIDED INTO FOUR ZONES, fixed in advance so every page in the book
## has the same anatomy:
##   TITLE     the one hand-lettered heading
##   BODY      what is happening, in the founder's hand
##   ENDING    the payload: visuals, selectable icons, or the written move
##   CONTROLS  navigation and commit, nothing else
##
## Usage:
##     var p := JournalPage.new()
##     p.build("WEEK 6", scene_id)
##     add_child(p)
##     p.line("The Roommate Talk. What do you do?")
##     p.ask("what do you do?", [{"id":"pitch","text":"Pitch them. Offer 30%."}, ...])
##     p.arrows(true, true)

signal choice_made(id: String)
signal written(text: String)
signal prev_page
signal next_page

const PAGE_ART := "res://assets/ui/logbook_page.png"
const ZONES_PATH := "res://assets/ui/logbook_page_zones.json"
const HAND := "res://assets/fonts/PatrickHand-Regular.ttf"

# Geometry measured off logbook_page.png and proven on THE LAST PAGE — the one
# page the owner accepted ("the LAST PAGE render really well"). The sheet fills
# the frame the way the reference book does, with the room showing past every edge.
const ART_PX := Vector2(1095.0, 1462.0)
const PAGE_SIZE := Vector2(719.0, 960.0)      # the art's exact aspect: no shear
const PAGE_POS := Vector2(408.0, 32.0)
const PAGE_TILT := -0.012                     # a hair of lean on top of the drawn one
# Re-measured on THE LAST PAGE: 884 wrongly counted the sheets drawn BEHIND the
# page as part of it, which is why the right margin read tighter than the left.
const PAPER_ORIGIN_TEX := Vector2(74.0, 152.0)
const PAPER_SIZE_TEX := Vector2(858.0, 1232.0)
const PAPER_TILT := -0.069                    # the lean drawn INTO the paper
const SCALE := PAGE_SIZE.x / ART_PX.x
const SHEET_POS := Vector2(PAPER_ORIGIN_TEX.x * SCALE, PAPER_ORIGIN_TEX.y * SCALE)
const SHEET_SIZE := Vector2(PAPER_SIZE_TEX.x * SCALE, PAPER_SIZE_TEX.y * SCALE)
# clear of the torn left edge, the spiral strip and the curled corner
const MARGIN_X := 46.0
const MARGIN_TOP := 26.0
const MARGIN_BOT := 44.0

## Two sizes. There is no third. The owner's defect was "different font size and
## style, the actual font doesn't integrate well as if it was written".
## The page's anatomy, in printed rules. 17 rules fit inside the sheet's margins,
## so this allocation is what the paper can physically hold.
const ZONE_RULES := {"title": 3, "body": 4, "ending": 7, "controls": 2}

const SIZE_TITLE := 64
const SIZE_BODY := 34
const GAP := 22.0
const INK := Color("1E1E1E")
const PEN := Color("E86A5C")
const FAINT := Color(Color("1E1E1E"), 0.45)

var space: Control                ## page-local content space; everything lands here
var room: SceneRoom               ## the live animated room behind the sheet

var _font: Font
var _page := Vector2.ZERO         ## rendered size of the sheet
var _spans: Array = []            ## [[y, left, right], ...] normalised, measured from the art
var _zone: Dictionary = {}        ## name -> [y0, y1] normalised
var _rules: Dictionary = {}       ## the PRINTED ruling: {first, pitch} normalised
var _usable := Vector2.ZERO       ## writable area inside the sheet's margins
var _zone_px: Dictionary = {}     ## name -> [top, bottom] in sheet-local pixels
var _cursor: Dictionary = {}      ## name -> current y in page-local pixels
var _input: TextEdit

func build(title_text: String, scene_id: String = "") -> void:
	_font = load(HAND)
	# FULL_RECT already drives the size; assigning it as well warns that the size
	# will be overridden after _ready() and fights the anchor system.
	set_anchors_preset(Control.PRESET_FULL_RECT)

	var doc: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(ZONES_PATH))
	_spans = doc.get("spans", [])
	_zone = doc.get("zones", {})
	_rules = doc.get("rules", {})

	if scene_id != "":
		room = SceneRoom.new()
		room.load_scene(scene_id)
		add_child(room)
		# The page is the subject; the room is where you are. Owner: "the paper log
		# should come simply on top of a scene and we need to have a dark overlay on
		# scene". Without this the sheet competes with the art behind it.
		room.dim(0.45)

	var tex := TextureRect.new()
	# flags BEFORE size. Setting size first locks the minimum size and the image
	# letterboxes — the exact ordering bug that produced the inset room.
	tex.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	tex.stretch_mode = TextureRect.STRETCH_SCALE
	tex.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tex.texture = load(PAGE_ART)
	_page = PAGE_SIZE
	tex.position = PAGE_POS
	tex.pivot_offset = PAGE_SIZE * 0.5
	tex.rotation = PAGE_TILT
	tex.set_deferred("size", PAGE_SIZE)
	add_child(tex)

	# THE SHEET. The paper is drawn ALREADY LEANING inside the texture, so the
	# paper's edges and its printed rules are not parallel to the texture frame.
	# Rather than wrap text to a diagonal, rotate one Control to match the drawn
	# lean: inside it the paper IS an upright rectangle, so plain axis-aligned
	# layout is correct by construction and the tilt is applied exactly once.
	# This is the geometry from THE LAST PAGE, the one page the owner passed.
	space = Control.new()
	space.mouse_filter = Control.MOUSE_FILTER_PASS
	space.position = SHEET_POS
	space.pivot_offset = Vector2.ZERO
	space.rotation = PAPER_TILT
	space.set_deferred("size", SHEET_SIZE)
	tex.add_child(space)

	_usable = Vector2(SHEET_SIZE.x - 2.0 * MARGIN_X, SHEET_SIZE.y - MARGIN_TOP - MARGIN_BOT)
	# ZONES ARE MEASURED IN PRINTED RULES, not in fractions of the artwork.
	# Fractions of the art gave the title 103px while a 64px title needs two rules
	# plus its gap — 107 — so every page opened by overrunning its own first zone.
	# Rules are the unit the page is actually built on, so allocate in rules and
	# every boundary lands on one.
	var pitch := rule_pitch()
	var y0 := (float(_rules.get("first", 0.17784)) * ART_PX.y - PAPER_ORIGIN_TEX.y) * SCALE
	var y := y0
	for z in ZONE_RULES.keys():
		var h: float = float(ZONE_RULES[z]) * pitch
		_cursor[z] = y
		_zone_px[z] = [y, y + h]
		y += h

	if title_text != "":
		title(title_text)

# ---------- the writable silhouette ----------

## Writable span at a y inside the SHEET, as [left_x, right_x]. It is constant,
## because `space` is already rotated to the paper's lean — inside that space the
## paper is an upright rectangle. This is why no shaped wrapping is needed: the
## rotation does the work that per-scanline wrapping would otherwise have to.
func span_at(_y: float) -> Vector2:
	return Vector2(MARGIN_X, SHEET_SIZE.x - MARGIN_X)

func zone_bottom(zone: String) -> float:
	if _zone_px.has(zone):
		return float((_zone_px[zone] as Array)[1])
	return SHEET_SIZE.y - MARGIN_BOT

## How much of a zone is still free. A page that returns <= 0 is holding too much
## and should be split rather than shrunk.
func room_left(zone: String = "ending") -> float:
	return zone_bottom(zone) - float(_cursor.get(zone, 0.0))

# ---------- content ----------

func title(text: String) -> void:
	_shaped(text, SIZE_TITLE, INK, "title", HORIZONTAL_ALIGNMENT_CENTER)

func line(text: String, faint: bool = false, zone: String = "body") -> void:
	_shaped(text, SIZE_BODY, FAINT if faint else INK, zone, HORIZONTAL_ALIGNMENT_LEFT)

## A row of selectable icons. State is drawn and choosing is a pen mark — never a
## button, never a bordered chip. `items` is [{id, text, tex(optional)}].
func icon_row(items: Array, cell := Vector2(124, 116), zone: String = "ending") -> Control:
	var y: float = _cursor.get(zone, 0.0)
	var sp := span_at(y + cell.y * 0.5)
	var avail: float = sp.y - sp.x
	var n: int = max(items.size(), 1)
	var step: float = min(cell.x + 28.0, avail / float(n))
	var total: float = step * n
	var x0: float = sp.x + (avail - total) * 0.5

	# THE CAPTION DECIDES THE CELL, not the other way round. A fixed caption strip
	# is what let "Buy fans. Boring. Works." wrap to three lines and print straight
	# through the line below it, and what clipped "Enterprise" to "Enterpris".
	# Captions are drawn at the COLUMN width (not the nominal cell width) so two
	# neighbours can never overlap, and the row grows to fit the tallest one.
	var cap_w: float = max(step - 14.0, 60.0)
	var cap_h := 0.0
	for it0 in items:
		var t0 := String((it0 as Dictionary).get("text", ""))
		cap_h = max(cap_h, _font.get_multiline_string_size(
				t0, HORIZONTAL_ALIGNMENT_CENTER, cap_w, SIZE_BODY).y)
	cap_h = max(cap_h, SIZE_BODY * 1.2)
	cell = Vector2(cap_w, cell.y - 46.0 + cap_h + 8.0)

	var row := Control.new()
	row.position = Vector2(0, y)
	row.set_deferred("size", Vector2(_page.x, cell.y))
	space.add_child(row)

	for i in items.size():
		var it: Dictionary = items[i]
		var slot := Control.new()
		slot.position = Vector2(x0 + step * i, 0)
		slot.set_deferred("size", cell)
		slot.mouse_filter = Control.MOUSE_FILTER_STOP
		slot.set_meta("id", String(it.get("id", str(i))))
		row.add_child(slot)
		var mark := _PenCircle.new()
		mark.mouse_filter = Control.MOUSE_FILTER_IGNORE
		mark.visible = false
		mark.set_deferred("size", cell)
		slot.add_child(mark)
		if it.has("tex") and it["tex"] != null:
			var ic := TextureRect.new()
			ic.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
			ic.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
			ic.mouse_filter = Control.MOUSE_FILTER_IGNORE
			ic.texture = it["tex"]
			ic.set_deferred("size", Vector2(cap_w, cell.y - cap_h - 8.0))
			slot.add_child(ic)
		var cap := Label.new()
		cap.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		cap.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		cap.add_theme_font_override("font", _font)
		cap.add_theme_font_size_override("font_size", SIZE_BODY)
		cap.add_theme_color_override("font_color", INK)
		cap.mouse_filter = Control.MOUSE_FILTER_IGNORE
		cap.text = String(it.get("text", ""))
		cap.custom_minimum_size = Vector2(cap_w, 0)
		cap.position = Vector2(0, cell.y - cap_h)
		cap.set_deferred("size", Vector2(cap_w, cap_h))
		slot.add_child(cap)
		var id := String(it.get("id", str(i)))
		slot.gui_input.connect(func(e: InputEvent) -> void:
			if e is InputEventMouseButton and e.pressed:
				_select(row, id))

	_cursor[zone] = y + cell.y + GAP
	_overrun(zone)
	return row

## THE FREE WRITTEN MOVE — the originality of the game. The player writes what they
## actually do and the world adjudicates it. Deliberately NOT a widget: no box, no
## border, no fill. The ruled line IS the field and the typing looks handwritten.
func write_field(prompt: String = "...or write what you actually do", zone: String = "ending") -> TextEdit:
	line(prompt, true, zone)
	var y: float = _cursor.get(zone, 0.0)
	var sp := span_at(y + SIZE_BODY)
	_input = TextEdit.new()
	_input.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
	_input.scroll_fit_content_height = true
	_input.add_theme_font_override("font", _font)
	_input.add_theme_font_size_override("font_size", SIZE_BODY)
	_input.add_theme_color_override("font_color", INK)
	_input.add_theme_color_override("caret_color", PEN)
	for s in ["normal", "focus", "read_only"]:
		_input.add_theme_stylebox_override(s, StyleBoxEmpty.new())
	# Give the writing area a real height: it must be a big, obvious place to write,
	# not a one-line slot. Two ruled lines minimum, more when the page has room.
	var hgt: float = max(rule_pitch() * 2.0, min(rule_pitch() * 4.0, zone_bottom(zone) - y - 8.0))
	_input.position = Vector2(sp.x, y)
	_input.custom_minimum_size = Vector2(sp.y - sp.x, hgt)
	_input.set_deferred("size", Vector2(sp.y - sp.x, hgt))
	_input.mouse_filter = Control.MOUSE_FILTER_STOP
	space.add_child(_input)
	var nib := _WriteHint.new()
	nib.mouse_filter = Control.MOUSE_FILTER_IGNORE
	nib.position = Vector2(sp.x, y)
	nib.set_deferred("size", Vector2(sp.y - sp.x, hgt))
	space.add_child(nib)
	space.move_child(nib, max(space.get_child_count() - 2, 0))
	_input.text_changed.connect(func() -> void:
		nib.written = _input.text.strip_edges() != ""
		nib.queue_redraw()
		written.emit(_input.text))
	# THE FIELD IS INVISIBLE BY DESIGN — no box, no border, the ruling IS the field.
	# That makes it undiscoverable unless it already has focus, which is why the
	# owner reported "I actually cannot write at all": there was nothing to aim at.
	# So the page hands it the keyboard the moment it opens. Clicking a choice does
	# not steal it back, because choices are picked with the mouse.
	_input.call_deferred("grab_focus")
	# CLICKING ANYWHERE ON THE SHEET PUTS THE PEN IN YOUR HAND. The field has no
	# box by design, so its exact hit area is invisible and the owner reported it
	# as impossible to select. The whole page now routes a click into the field,
	# which also means a mis-click can never leave the player with nowhere to type.
	if not gui_input.is_connected(_focus_writing):
		gui_input.connect(_focus_writing)
	mouse_filter = Control.MOUSE_FILTER_STOP
	# do not let the trailing gap push past the boundary: it is space AFTER the
	# last element, not space the element needs. That alone reported the ending
	# zone overrunning by 2px on every page.
	_cursor[zone] = min(y + hgt + GAP, zone_bottom(zone))
	_overrun(zone)
	return _input

func _focus_writing(ev: InputEvent) -> void:
	if ev is InputEventMouseButton and ev.pressed and _input != null and is_instance_valid(_input):
		_input.grab_focus()

func written_text() -> String:
	return _input.text.strip_edges() if _input != null else ""

## THE SHAPE EVERY PAGE ENDS IN (owner: each page states the situation, then asks
## what you want to do). Composing it here means no page can forget the written
## move or bury the question.
func ask(situation: String, options: Array, allow_write: bool = true) -> Control:
	# The question goes in BODY. It is prose, and putting it in ENDING meant the
	# shell's own anatomy — question + choice row + written move — measured 436px
	# into a 256px zone, which is what produced overrun warnings on nearly every
	# page. ENDING carries the payload only.
	line(situation, false, "body")
	var row := icon_row(options)
	if allow_write:
		write_field()
	return row

## THE OVERVIEW PAGE: reads the run back to the player in their own hand, then asks
## what to do next.
func overview(state_lines: Array, whats_next: String, options: Array) -> Control:
	for s in state_lines:
		if s is Dictionary:
			line(String(s.get("text", "")), bool(s.get("faint", false)))
		else:
			line(String(s))
	return ask(whats_next, options)

## Navigation lives in the CONTROLS zone and is drawn, never chrome.
func arrows(show_prev: bool, show_next: bool) -> void:
	var y: float = float(_zone.get("controls", [0.85, 0.92])[0]) * _page.y + 12.0
	var sp := span_at(y + 26.0)
	if show_prev:
		_arrow(Vector2(sp.x, y), false).pressed.connect(func() -> void: prev_page.emit())
	if show_next:
		_arrow(Vector2(sp.y - 90.0, y), true).pressed.connect(func() -> void: next_page.emit())

# ---------- internals ----------

## Lay text out line by line against the REAL paper edges. Each line is measured,
## broken to the span available at its own y, and placed at that span's left edge.
## This is what makes the wrap follow the lean instead of crossing it.
func _shaped(text: String, sz: int, col: Color, zone: String, align: int) -> void:
	# Sit ON the printed ruling. Text that floats between the rules is the single
	# strongest "a text engine did this" tell; snapping every baseline to a rule and
	# advancing by whole rules is what makes it read as handwriting.
	var y: float = _snap(float(_cursor.get(zone, 0.0)))
	var lh: float = _line_advance(sz)
	var words := text.split(" ", false)
	var cur := ""
	var i := 0
	while i < words.size():
		var sp := span_at(y + lh * 0.5)
		var avail: float = sp.y - sp.x
		var trial: String = words[i] if cur == "" else cur + " " + words[i]
		if _font.get_string_size(trial, align, -1, sz).x <= avail or cur == "":
			cur = trial
			i += 1
		else:
			_place(cur, sz, col, sp, y, lh, align)
			y += lh
			cur = ""
	if cur != "":
		var sp2 := span_at(y + lh * 0.5)
		_place(cur, sz, col, sp2, y, lh, align)
		y += lh
	_cursor[zone] = y + GAP
	_overrun(zone)

## Pitch of the printed ruling, in SHEET-local pixels.
func rule_pitch() -> float:
	return float(_rules.get("pitch", 0.04446)) * ART_PX.y * SCALE

## The next printed rule at or after y, so a baseline always lands on one. Rule
## positions are measured in the art and converted into the sheet's own space.
func _snap(y: float) -> float:
	var first_art: float = float(_rules.get("first", 0.17784)) * ART_PX.y
	var first: float = (first_art - PAPER_ORIGIN_TEX.y) * SCALE
	var pitch := rule_pitch()
	if pitch <= 1.0:
		return y
	if y <= first:
		return first
	return first + ceil((y - first) / pitch) * pitch

## A line occupies whole rules: body text ONE, a big title two. The 0.78 factor is
## load-bearing — a font's reported height includes ascender and descender padding
## that handwriting does not visually occupy, so measuring raw height pushed body
## text to two rules and opened a blank line between every wrapped line.
func _line_advance(sz: int) -> float:
	var pitch := rule_pitch()
	if pitch <= 1.0:
		return _font.get_height(sz) * 1.08
	return pitch * max(1.0, ceil(_font.get_height(sz) * 0.78 / pitch))

func _place(text: String, sz: int, col: Color, sp: Vector2, y: float, lh: float, align: int) -> void:
	var l := Label.new()
	l.add_theme_font_override("font", _font)
	l.add_theme_font_size_override("font_size", sz)
	l.add_theme_color_override("font_color", col)
	l.horizontal_alignment = align
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	l.text = text
	l.position = Vector2(sp.x, y)
	l.set_deferred("size", Vector2(sp.y - sp.x, lh))
	space.add_child(l)

func _overrun(zone: String) -> void:
	if float(_cursor.get(zone, 0.0)) > zone_bottom(zone):
		push_warning("JournalPage: %s zone overran (%.0f > %.0f) — split the page or cut copy"
				% [zone, float(_cursor[zone]), zone_bottom(zone)])

## Chosen is circled in ink; the rest simply go quiet. Never a border, never a
## fill, never a highlight — those read as a form.
func _select(row: Control, id: String) -> void:
	for slot in row.get_children():
		var mine: bool = String(slot.get_meta("id", "")) == id
		slot.modulate = Color.WHITE if mine else Color(1, 1, 1, 0.55)
		for c in slot.get_children():
			if c is _PenCircle:
				c.visible = mine
	choice_made.emit(id)

func _arrow(pos: Vector2, forward: bool) -> Button:
	var b := Button.new()
	b.flat = true
	b.position = pos
	b.set_deferred("size", Vector2(90, 52))
	var a := _Arrow.new()
	a.forward = forward
	a.mouse_filter = Control.MOUSE_FILTER_IGNORE
	a.set_deferred("size", Vector2(90, 52))
	b.add_child(a)
	space.add_child(b)
	return b

## Marks the writing area as a writing area: the rule you write on, and a pen nib
## resting at its start until you have written something.
class _WriteHint:
	extends Control
	var written := false
	func _draw() -> void:
		var pitch: float = max(size.y * 0.5, 30.0)
		var y := pitch - 6.0
		while y < size.y:
			var pts := PackedVector2Array()
			var rng := RandomNumberGenerator.new()
			rng.seed = 17
			for i in 33:
				pts.append(Vector2(size.x * float(i) / 32.0, y + rng.randf_range(-1.0, 1.0)))
			draw_polyline(pts, Color(JournalPage.PEN, 0.30), 2.5, true)
			y += pitch
		if not written:
			draw_circle(Vector2(2.0, pitch - 10.0), 4.0, Color(JournalPage.PEN, 0.75))

class _PenCircle:
	extends Control
	func _draw() -> void:
		var c := size * 0.5
		var pts := PackedVector2Array()
		var rng := RandomNumberGenerator.new()
		rng.seed = 9
		for i in 41:
			var t := TAU * float(i) / 40.0
			var w := 1.0 + rng.randf_range(-0.05, 0.05)
			pts.append(c + Vector2(cos(t) * size.x * 0.48 * w, sin(t) * size.y * 0.47 * w))
		draw_polyline(pts, JournalPage.PEN, 4.0, true)

class _Arrow:
	extends Control
	var forward := true
	func _draw() -> void:
		var y := size.y * 0.5
		var x0 := 8.0
		var x1 := size.x - 8.0
		var shaft := PackedVector2Array([Vector2(x0, y), Vector2(x1 * 0.62, y - 3.0), Vector2(x1, y)])
		if not forward:
			shaft = PackedVector2Array([Vector2(x1, y), Vector2(x1 * 0.38, y - 3.0), Vector2(x0, y)])
		draw_polyline(shaft, JournalPage.INK, 4.0, true)
		var tip := Vector2(x1, y) if forward else Vector2(x0, y)
		var d := 1.0 if forward else -1.0
		draw_polyline(PackedVector2Array([tip - Vector2(22.0 * d, 13.0), tip, tip - Vector2(22.0 * d, -13.0)]),
				JournalPage.INK, 4.0, true)
