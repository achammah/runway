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
# Widened at the owner's request to match the reading beat, which he liked. The
# aspect is the art's own, so the sheet cannot shear — it just fills more frame,
# with the room still showing past every edge.
const PAGE_SIZE := Vector2(862.0, 1152.0)
const PAGE_POS := Vector2(337.0, -24.0)   # re-centred for the wider sheet
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
## AN ICON SMALLER THAN THIS IS NOT AN ICON, IT IS A SPECK. Reported as "portraits at
## 30px and gift icons at 10px, captions under specks". The row had been sizing the
## drawing to whatever the caller's cell had left AFTER the caption, so a wrapped
## caption ate the picture. The floor is enforced here, in the shell, by GROWING the
## row instead — no caller can produce an invisible icon again, and the zones below
## cascade down to absorb the extra height.
const ICON_MIN_H := 96.0
## breathing room between a drawing and the words under it
const CAP_GAP := 8.0
const INK := Color("1E1E1E")
const PEN := Color("E86A5C")
const FAINT := Color(Color("1E1E1E"), 0.45)

## THE PAGE IS WRITTEN, NOT PRINTED. Every element used to pop in fully formed,
## which reads as a text engine filling a template. Now the page plays in the way
## a hand fills a sheet: lines appear left to right behind a travelling nib,
## drawings fade up in order, and the ruled writing line arrives last, ready.
## One click anywhere skips the whole performance — a reader is never held.
## Chars per second. Body writes at a quick hand; the big title is LETTERED, so
## it goes slower per glyph. Both exist only to be felt, not waited on: the page
## budget below caps the total and scales everything to fit it.
const WRITE_CPS := 80.0
const TITLE_CPS := 34.0
const REVEAL_BUDGET := 3.6      ## the longest any page may spend arriving
const ICON_IN := 0.22           ## one drawing's fade-up
const ICON_STAGGER := 0.09      ## the beat between neighbours — felt, not implied

var instant := false            ## re-reading an old page: everything is already ink
var backdrop_path := ""         ## a composed week image to stand behind the sheet

var space: Control                ## page-local content space; everything lands here
var room: SceneRoom               ## the live animated room behind the sheet

var _font: Font
var _page := Vector2.ZERO         ## rendered size of the sheet
var _spans: Array = []            ## [[y, left, right], ...] normalised, measured from the art
var _zone: Dictionary = {}        ## name -> [y0, y1] normalised
var _rules: Dictionary = {}       ## the PRINTED ruling: {first, pitch} normalised
var _usable := Vector2.ZERO       ## writable area inside the sheet's margins
var _top_pad := 0.0               ## where the paper first becomes visible on screen
var _paper_bot := 0.0             ## last sheet-local y the player can still see paper at
var _zone_px: Dictionary = {}     ## name -> [top, bottom] in sheet-local pixels
var _cursor: Dictionary = {}      ## name -> current y in page-local pixels
var _input: TextEdit

var _last_block_y := 0.0          ## sheet-local y where the last written block began
var _sheet: TextureRect           ## the drawn paper; the thing a page-turn moves
var _enter_done := true           ## the sheet has landed; the reveal may start
var _tag := ""                    ## the page's title, so a warning names its page
var _seq: Array = []              ## the performance, in the order content was added
var _revealing := false           ## the page is still arriving; a click skips it
var _reveal_queued := false
var _reveal_tw: Tween
var _pen: _PenTip
var _scratch: AudioStreamPlayer   ## paper under the nib, looping while lines write
var _scribble: AudioStreamPlayer  ## one quick loop of ink for the pen circle
var _wrote: Dictionary = {}       ## zone -> content actually landed there

func build(title_text: String, scene_id: String = "") -> void:
	_font = load(HAND)
	# Harnesses photograph pages the instant they compose, so a page that is still
	# writing itself in would fail every wrap/overflow check it is actually passing.
	# The switch is environmental and deterministic: capture rigs set it, players
	# never see it.
	if OS.get_environment("RUNWAY_INSTANT_PAGES") == "1":
		instant = true
	_tag = title_text
	# FULL_RECT already drives the size; assigning it as well warns that the size
	# will be overridden after _ready() and fights the anchor system.
	set_anchors_preset(Control.PRESET_FULL_RECT)

	var doc: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(ZONES_PATH))
	_spans = doc.get("spans", [])
	_zone = doc.get("zones", {})
	_rules = doc.get("rules", {})

	# a generated week image outranks the stock stage as the room behind the paper
	if backdrop_path != "" and FileAccess.file_exists(backdrop_path):
		var bimg := Image.new()
		if bimg.load(backdrop_path) == OK:
			var btr := TextureRect.new()
			btr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
			btr.stretch_mode = TextureRect.STRETCH_SCALE
			btr.mouse_filter = Control.MOUSE_FILTER_IGNORE
			btr.texture = ImageTexture.create_from_image(bimg)
			btr.set_deferred("size", Vector2(1536, 1024))
			add_child(btr)
			var bdim := ColorRect.new()
			bdim.color = Color(0, 0, 0, 0.45)
			bdim.set_deferred("size", Vector2(1536, 1024))
			bdim.mouse_filter = Control.MOUSE_FILTER_IGNORE
			add_child(bdim)
			scene_id = ""
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
	_sheet = tex

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

	# CONTENT LIVES ONLY WHERE THE PAPER IS ACTUALLY ON SCREEN.
	# The sheet deliberately overflows the canvas so it fills the frame the way the
	# reference book does — but the zones were being measured against the WHOLE sheet,
	# so the title rode 24px above the top of the screen and the controls fell 104px
	# below the bottom. Clip the writable band to the visible intersection first.
	var sheet_top: float = PAGE_POS.y + SHEET_POS.y
	var vis_top: float = maxf(0.0, -sheet_top) + MARGIN_TOP
	var vis_bot: float = minf(SHEET_SIZE.y, 1024.0 - sheet_top) - MARGIN_BOT
	_top_pad = vis_top
	_usable = Vector2(SHEET_SIZE.x - 2.0 * MARGIN_X, maxf(vis_bot - vis_top, 120.0))
	_paper_bot = vis_top + _usable.y
	# ZONES ARE MEASURED IN PRINTED RULES, not in fractions of the artwork.
	# Fractions of the art gave the title 103px while a 64px title needs two rules
	# plus its gap — 107 — so every page opened by overrunning its own first zone.
	# Rules are the unit the page is actually built on, so allocate in rules and
	# every boundary lands on one.
	var pitch := rule_pitch()
	var y0 := (float(_rules.get("first", 0.17784)) * ART_PX.y - PAPER_ORIGIN_TEX.y) * SCALE
	var y: float = maxf(y0, _top_pad)
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
	return writable_bottom()

## THE ONE BOUNDARY THAT IS REAL. A zone boundary is a budget and it may be crossed —
## the cascade moves the next zone down and the page still reads. THIS is the edge of
## the paper: past it ink lands on the room behind the book, which is the defect the
## page has been rejected for. Sheet-local, and it is the VISIBLE bottom — the sheet
## deliberately overflows the canvas, so its own last pixel is below the screen.
func writable_bottom() -> float:
	return _paper_bot if _paper_bot > 0.0 else SHEET_SIZE.y - MARGIN_BOT

## ZONES CASCADE — in BOTH directions. Down: content in a zone whose neighbour above
## overran moves down rather than landing on top of it (the choice captions once
## printed straight through the writing prompt). And UP: a zone whose neighbours
## above are still EMPTY floats up to the first free rule — five blank rules between
## the title and the week's text was dead paper that then pushed the writing prompt
## off the bottom, the worst possible trade on a page whose whole point is writing.
const _ZONE_ORDER := ["title", "body", "ending", "controls"]

func _cascade(zone: String) -> void:
	var idx := _ZONE_ORDER.find(zone)
	if idx <= 0:
		return
	var floor_y := 0.0
	for i in idx:
		var above: String = _ZONE_ORDER[i]
		if bool(_wrote.get(above, false)):
			floor_y = maxf(floor_y, float(_cursor.get(above, 0.0)))
	if bool(_wrote.get(zone, false)):
		# the zone is already flowing: it may only ever be pushed DOWN
		_cursor[zone] = _snap(maxf(float(_cursor.get(zone, 0.0)), floor_y))
	elif floor_y > 0.0:
		# first write into this zone: land on the first free rule, up or down
		_cursor[zone] = _snap(maxf(floor_y, _top_pad))
	# nothing written anywhere above: the nominal start stands

## The last two printed rules belong to CONTROLS — the lock line and the page-turn
## arrows — and nothing else may ever reach them. This is the fence that stops a
## worst-case page from striking its own prompt through with the writing rule.
func _hard_floor() -> float:
	return writable_bottom() - 2.0 * rule_pitch()

## How much of a zone is still free. A page that returns <= 0 is holding too much
## and should be split rather than shrunk.
func room_left(zone: String = "ending") -> float:
	return zone_bottom(zone) - float(_cursor.get(zone, 0.0))

## The paper that is REALLY left before the controls fence, after every cascade —
## the number a host must consult before adding a drawing row. room_left() answers
## against the zone's printed budget; this answers against the sheet itself, which
## is the difference between "the plan had room" and "the page has room".
func room_to_fence(zone: String = "ending") -> float:
	_cascade(zone)
	return _hard_floor() - _snap(float(_cursor.get(zone, 0.0)))

# ---------- content ----------

func title(text: String) -> void:
	_shaped(text, SIZE_TITLE, INK, "title", HORIZONTAL_ALIGNMENT_CENTER)

func line(text: String, faint: bool = false, zone: String = "body") -> void:
	_shaped(text, SIZE_BODY, FAINT if faint else INK, zone, HORIZONTAL_ALIGNMENT_LEFT)

## A row of selectable icons. State is drawn and choosing is a pen mark — never a
## button, never a bordered chip. `items` is [{id, text, tex(optional)}].
func icon_row(items: Array, cell := Vector2(124, 116), zone: String = "ending") -> Control:
	_cascade(zone)
	var y: float = _cursor.get(zone, 0.0)
	var sp := span_at(y + cell.y * 0.5)
	var avail: float = sp.y - sp.x
	var n: int = maxi(items.size(), 1)
	var step: float = minf(cell.x + 28.0, avail / float(n))
	var total: float = step * n
	var x0: float = sp.x + (avail - total) * 0.5

	# THE CAPTION DECIDES THE CELL, not the other way round. A fixed caption strip
	# is what let "Buy fans. Boring. Works." wrap to three lines and print straight
	# through the line below it, and what clipped "Enterprise" to "Enterpris".
	# Captions are drawn at the COLUMN width (not the nominal cell width) so two
	# neighbours can never overlap, and the row grows to fit the tallest one —
	# TO A POINT. Three wrapped lines is where a caption stops being a label and
	# starts being the paragraph that shoves the writing field off the sheet, so
	# the shell cuts it there with an ellipsis. (The card schema caps labels at 48
	# chars; only content that already broke its contract can ever be cut.)
	var cap_w: float = maxf(step - 14.0, 60.0)
	var caps := PackedStringArray()
	var cap_h := 0.0
	for it0 in items:
		var t0 := _cap_lines(String((it0 as Dictionary).get("text", "")), cap_w)
		caps.append(t0)
		cap_h = maxf(cap_h, _font.get_multiline_string_size(
				t0, HORIZONTAL_ALIGNMENT_CENTER, cap_w, SIZE_BODY).y)
	cap_h = maxf(cap_h, SIZE_BODY * 1.2)

	# THE PICTURE IS NEVER WHAT IS LEFT OVER. The caller's cell height is the row's
	# BUDGET: the caption is taken out of it first and the drawing gets the rest — but
	# if that rest falls under ICON_MIN_H the row GROWS rather than shrinking the
	# drawing, because a 10px portrait cannot be rescued and a taller row can. Captions
	# stay at SIZE_BODY always; the shell has two type sizes and no third.
	var draws_icon := false
	for it1 in items:
		if (it1 as Dictionary).get("tex") != null:
			draws_icon = true
			break
	# a caption-only row reserves NO picture space — an empty 96px strip above the
	# words is dead paper, and dead paper is what pushes the writing off the sheet
	var icon_h := 0.0
	if draws_icon:
		icon_h = maxf(cell.y - cap_h - CAP_GAP, ICON_MIN_H)
		# THE ROW GIVES BEFORE THE PAGE DOES. When the row's bottom would cross the
		# controls fence, the DRAWINGS compress toward their floor first — a smaller
		# jar is a jar, but a prompt on the room is a broken page.
		var over: float = (y + icon_h + CAP_GAP + cap_h) - _hard_floor()
		if over > 0.0:
			icon_h = maxf(icon_h - over, ICON_MIN_H)
	cell = Vector2(cap_w, icon_h + (CAP_GAP if draws_icon else 0.0) + cap_h)

	var row := Control.new()
	row.position = Vector2(0, y)
	row.set_deferred("size", Vector2(_page.x, cell.y))
	# The full-width band must never swallow a click meant for the page (the skip)
	# or for a slot. IGNORE on the container leaves the slots as the only targets.
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	space.add_child(row)

	for i in items.size():
		var it: Dictionary = items[i]
		var slot := Control.new()
		slot.position = Vector2(x0 + step * i, 0)
		slot.set_deferred("size", cell)
		# An option that has not been inked yet cannot be chosen — and a click while
		# the page is still arriving must SKIP the arrival, which only works if the
		# page itself hears it. The sequencer hands the click back at reveal.
		slot.mouse_filter = Control.MOUSE_FILTER_IGNORE if not instant else Control.MOUSE_FILTER_STOP
		slot.set_meta("id", String(it.get("id", str(i))))
		if not instant:
			slot.modulate = Color(1, 1, 1, 0)
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
			ic.set_deferred("size", Vector2(cap_w, icon_h))
			slot.add_child(ic)
		var cap := Label.new()
		cap.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		cap.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		cap.add_theme_font_override("font", _font)
		cap.add_theme_font_size_override("font_size", SIZE_BODY)
		cap.add_theme_color_override("font_color", INK)
		cap.mouse_filter = Control.MOUSE_FILTER_IGNORE
		cap.text = caps[i]
		cap.custom_minimum_size = Vector2(cap_w, 0)
		cap.position = Vector2(0, cell.y - cap_h)
		cap.set_deferred("size", Vector2(cap_w, cap_h))
		slot.add_child(cap)
		var id := String(it.get("id", str(i)))
		slot.gui_input.connect(func(e: InputEvent) -> void:
			if e is InputEventMouseButton and e.pressed:
				_select(row, id))

	if not instant:
		_enqueue({"kind": "icons", "row": row})
	_cursor[zone] = y + cell.y + GAP
	_wrote[zone] = true
	_overrun(zone)
	return row

## THE FREE WRITTEN MOVE — the originality of the game. The player writes what they
## actually do and the world adjudicates it. Deliberately NOT a widget: no box, no
## border, no fill. The ruled line IS the field and the typing looks handwritten.
func write_field(prompt: String = "...or write what you actually do", zone: String = "ending") -> TextEdit:
	_cascade(zone)
	# When the sheet is nearly spent, the PROMPT is the line that steps aside — the
	# ruled hint and the resting nib already say "write here", and a prompt printed
	# on the page curl said something worse about the whole book. An empty prompt
	# skips the line on purpose: a page with choices above the field needs no words
	# to explain the ruled lines.
	var room_now: float = _hard_floor() - _snap(float(_cursor.get(zone, 0.0)))
	if prompt != "" and room_now >= _line_advance(SIZE_BODY) + rule_pitch() * 2.0:
		line(prompt, true, zone)
	_cascade(zone)
	var y: float = _cursor.get(zone, 0.0)
	var sp := span_at(y + SIZE_BODY)
	_input = TextEdit.new()
	_input.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
	# SCROLL, DO NOT GROW. Growing to fit pushed a long written move straight through
	# the bottom of the page. The field keeps the height the zone allows and scrolls
	# inside it, so the player can write as much as they like and the paper still holds.
	_input.scroll_fit_content_height = false
	_input.scroll_smooth = true
	_input.add_theme_font_override("font", _font)
	_input.add_theme_font_size_override("font_size", SIZE_BODY)
	_input.add_theme_color_override("font_color", INK)
	_input.add_theme_color_override("caret_color", PEN)
	for s in ["normal", "focus", "read_only"]:
		_input.add_theme_stylebox_override(s, StyleBoxEmpty.new())
	# Give the writing area a real height: it must be a big, obvious place to write,
	# not a one-line slot. Two ruled lines minimum, up to five when the page has
	# room — on the decision spread the field IS the page.
	var hgt: float = maxf(rule_pitch() * 2.0, minf(rule_pitch() * 5.0, _hard_floor() - y - 8.0))
	# ...but the CONTROLS FENCE wins over the zone: the field stops at the hard floor
	# so the lock line and the arrows always keep their two rules. It still never
	# drops under 1.2 rules — the written move is the game's core and a zero-height
	# input is the one cut this page may never make; ask() budgets so it cannot come
	# to that, and if a host composes past every budget the field crosses the fence
	# and _overrun says so, which is the honest failure.
	# TWO FULL SLOTS MINIMUM plus descender headroom — but the PAPER EDGE is
	# absolute: on the rare page whose upstream content lands deep, the floor
	# bends to the sheet rather than inking the room. The field scrolls, so a
	# squeezed field still takes any length of writing.
	hgt = minf(hgt, _hard_floor() - y - 8.0)
	hgt = maxf(hgt, rule_pitch() * 2.0) + 12.0
	hgt = minf(hgt, writable_bottom() - y - 2.0)
	hgt = maxf(hgt, rule_pitch() * 1.2)
	_input.position = Vector2(sp.x, y)
	_input.custom_minimum_size = Vector2(sp.y - sp.x, hgt)
	_input.set_deferred("size", Vector2(sp.y - sp.x, hgt))
	_input.mouse_filter = Control.MOUSE_FILTER_STOP
	space.add_child(_input)
	var nib := _WriteHint.new()
	nib.edit = _input
	nib.mouse_filter = Control.MOUSE_FILTER_IGNORE
	nib.position = Vector2(sp.x, y)
	nib.set_deferred("size", Vector2(sp.y - sp.x, hgt))
	space.add_child(nib)
	space.move_child(nib, max(space.get_child_count() - 2, 0))
	# THE CARET BELONGS TO THE WRITER. An earlier version forced it to the last
	# line on every keystroke "to follow the pen down the page" — which meant
	# editing the middle of your own sentence teleported you to its end (reported
	# live: "when finishing the line it goes back to the beginning"). TextEdit
	# already keeps its caret in view; the page only watches the words.
	_input.text_changed.connect(func() -> void:
		nib.written = _input.text.strip_edges() != ""
		nib.queue_redraw()
		written.emit(_input.text))
	# TYPED INK RIDES THE PRINTED RULES. The field's line height is pinned to the
	# page's own rule pitch, so what you write sits on the ruling like every
	# drawn line — and the hint's rules can never strike through your words.
	_input.add_theme_constant_override("line_spacing",
			maxi(int(rule_pitch() - _font.get_height(SIZE_BODY)), 0))
	nib.pitch = rule_pitch()
	nib.ascent = _font.get_ascent(SIZE_BODY)
	# a scrollbar is a piece of software on a sheet of paper: invisible, inert
	var vsb := _input.get_v_scroll_bar()
	if vsb != null:
		vsb.modulate = Color(1, 1, 1, 0)
		vsb.mouse_filter = Control.MOUSE_FILTER_IGNORE
	# ghost handwriting says "this is yours to fill" before the first keystroke
	_input.add_theme_color_override("font_placeholder_color", Color(INK, 0.30))
	# the ruling presses coral while the pen is in your hand — the same signal
	# PaperInput taught the setup screens
	_input.focus_entered.connect(func() -> void:
		nib.focused = true
		nib.queue_redraw())
	_input.focus_exited.connect(func() -> void:
		nib.focused = false
		nib.queue_redraw())
	# THE FIELD IS INVISIBLE BY DESIGN — no box, no border, the ruling IS the field.
	# That makes it undiscoverable unless it already has focus, which is why the
	# owner reported "I actually cannot write at all": there was nothing to aim at.
	# So the page hands it the keyboard the moment it opens — or, when the page is
	# still writing itself in, the moment the ruled line arrives; grabbing focus
	# under a half-written page would let typed ink land above the pen.
	if instant:
		_input.call_deferred("grab_focus")
	else:
		_input.modulate = Color(1, 1, 1, 0)
		nib.modulate = Color(1, 1, 1, 0)
		_enqueue({"kind": "field", "field": _input, "nib": nib})
	# CLICKING ANYWHERE ON THE SHEET PUTS THE PEN IN YOUR HAND. The field has no
	# box by design, so its exact hit area is invisible and the owner reported it
	# as impossible to select. The whole page now routes a click into the field,
	# which also means a mis-click can never leave the player with nowhere to type.
	if not gui_input.is_connected(_focus_writing):
		gui_input.connect(_focus_writing)
	mouse_filter = Control.MOUSE_FILTER_STOP
	# do not let the trailing gap push past the boundary: it is space AFTER the
	# last element, not space the element needs. That alone reported the ending
	# zone overrunning by 2px on every page. The gap is all that may be dropped
	# though — clamping to the boundary outright hid a field that really did end
	# below it, and a hidden overflow is the one the player sees on the room.
	_cursor[zone] = minf(y + hgt + GAP, maxf(zone_bottom(zone), y + hgt))
	_wrote[zone] = true
	_overrun(zone)
	return _input

func _focus_writing(ev: InputEvent) -> void:
	if not (ev is InputEventMouseButton and ev.pressed):
		return
	# The first click on an arriving page finishes the writing; it never also
	# chooses, focuses, or turns anything. The reader asked for the page, not
	# an accidental decision.
	if _revealing:
		_finish_reveal()
		return
	if _input != null and is_instance_valid(_input):
		_input.grab_focus()

# ---------- the page turn: paper moves, the room does not ----------

## The new sheet arrives the way a turned page lands: from the side you are
## heading, a touch rotated, settling with a small overshoot. Only the SHEET
## moves — the room behind is the same room, so it holds still and the book
## feels like an object sitting in a place. dir: +1 forward, -1 back.
func enter_turn(dir: int) -> void:
	if _sheet == null:
		return
	_enter_done = false
	_sheet.rotation = PAGE_TILT + 0.05 * float(dir)
	_sheet.position = PAGE_POS + Vector2(150.0 * float(dir), 18.0)
	_sheet.modulate = Color(1, 1, 1, 0.0)
	# The beat between sheets is the turn. The new paper WAITS 80ms while the old
	# one clears, so for one breath the room alone is on screen — that gap is what
	# makes two drawings read as pages of one book instead of a crossfade.
	var tw := create_tween().set_parallel()
	tw.tween_property(_sheet, "rotation", PAGE_TILT, 0.24) \
			.set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT).set_delay(0.08)
	tw.tween_property(_sheet, "position", PAGE_POS, 0.24) \
			.set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT).set_delay(0.08)
	tw.tween_property(_sheet, "modulate:a", 1.0, 0.12).set_delay(0.08)
	tw.chain().tween_callback(func() -> void:
		_enter_done = true
		if _reveal_queued:
			_play_reveal())

## The old sheet leaves under the new one: its room winks out (the incoming page
## shows the same room, so nothing changes on screen), the paper lifts away, and
## the node frees itself when it is gone. The host puts this page on TOP first,
## so the departure is actually seen.
func exit_turn(dir: int) -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	if room != null and is_instance_valid(room):
		room.visible = false
	if _sheet == null:
		queue_free()
		return
	# It MOVES first and only then vanishes — a page that dissolves in place reads
	# as a crossfade, not a lifted sheet of paper.
	var tw := create_tween().set_parallel()
	tw.tween_property(_sheet, "rotation", PAGE_TILT - 0.08 * float(dir), 0.18) \
			.set_trans(Tween.TRANS_SINE)
	tw.tween_property(_sheet, "position", PAGE_POS + Vector2(-280.0 * float(dir), 44.0), 0.18) \
			.set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN)
	tw.tween_property(_sheet, "modulate:a", 0.0, 0.12).set_delay(0.06)
	tw.chain().tween_callback(queue_free)

# ---------- the performance: the page writes itself in ----------

func _enqueue(item: Dictionary) -> void:
	_seq.append(item)
	if not _reveal_queued:
		_reveal_queued = true
		# Hosts compose a page synchronously right after build(), so one deferred
		# hop lands after the LAST element and the whole page plays as one hand.
		call_deferred("_play_reveal")

func _play_reveal() -> void:
	if _seq.is_empty() or _revealing or not _enter_done:
		return
	_revealing = true
	mouse_filter = Control.MOUSE_FILTER_STOP
	if not gui_input.is_connected(_focus_writing):
		gui_input.connect(_focus_writing)
	_pen = _PenTip.new()
	_pen.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_pen.set_deferred("size", Vector2(40, 42))
	_pen.visible = false
	space.add_child(_pen)
	# The page sounds like a page. The scratch loops only while a LINE is under
	# the pen (_pen_show gates it), quiet enough to sit under the music.
	_scratch = _sfx_player("res://assets/sfx/pen_scratch.wav", -14.0, true)
	_scribble = _sfx_player("res://assets/sfx/pen_scribble.wav", -10.0, false)
	# The budget scales the hand, never the page: a long page writes faster,
	# a short one savours it, and nothing ever takes longer than the budget.
	var total := 0.0
	for it in _seq:
		total += _item_secs(it)
	var speed: float = maxf(total / REVEAL_BUDGET, 1.0)
	_reveal_tw = create_tween()
	for it in _seq:
		var secs: float = _item_secs(it) / speed
		match String(it.get("kind", "")):
			"line":
				var l: Label = it["label"]
				_reveal_tw.tween_callback(_pen_show.bind(true))
				_reveal_tw.tween_method(_write_line.bind(l, int(it["sz"])), 0.0, 1.0, secs)
			"icons":
				var row: Control = it["row"]
				_reveal_tw.tween_callback(_pen_show.bind(false))
				var slots := row.get_children()
				for i in slots.size():
					var s := slots[i] as Control
					# one parallel step, staggered by per-tweener delay: neighbours
					# arrive on a beat, and the step ends when the last one lands
					var tw := _reveal_tw.parallel() if i > 0 else _reveal_tw
					tw.tween_property(s, "modulate:a", 1.0, ICON_IN) \
							.set_delay(ICON_STAGGER * float(i)).set_trans(Tween.TRANS_SINE)
				_reveal_tw.tween_callback(_wake_row.bind(row))
			"field":
				_reveal_tw.tween_callback(_pen_show.bind(false))
				_reveal_tw.tween_property(it["field"] as Control, "modulate:a", 1.0, secs)
				_reveal_tw.parallel().tween_property(it["nib"] as Control, "modulate:a", 1.0, secs)
				_reveal_tw.tween_callback((it["field"] as Control).grab_focus)
			"fade":
				_reveal_tw.tween_property(it["node"] as Control, "modulate:a", 1.0, secs)
				_reveal_tw.tween_callback(_wake_row.bind(it["node"] as Control))
	_reveal_tw.tween_callback(_finish_reveal)

## One written line under the pen: the ink advances and the nib rides its tip.
func _write_line(r: float, l: Label, sz: int) -> void:
	if not is_instance_valid(l):
		return
	l.visible_ratio = r
	if _pen != null and is_instance_valid(_pen):
		var shown := l.text.substr(0, int(ceil(r * l.text.length())))
		var w := _font.get_string_size(shown, HORIZONTAL_ALIGNMENT_LEFT, -1, sz).x
		# the pen's TIP (local 4,38) touches the baseline just past the last glyph
		_pen.position = l.position + Vector2(w + 1.0, _font.get_ascent(sz) * 0.94) \
				- Vector2(4.0, 38.0)

func _pen_show(on: bool) -> void:
	if _pen != null and is_instance_valid(_pen):
		_pen.visible = on
	if _scratch != null and is_instance_valid(_scratch):
		if on and not _scratch.playing:
			_scratch.play()
		elif not on and _scratch.playing:
			_scratch.stop()

func _sfx_player(path: String, db: float, loop: bool) -> AudioStreamPlayer:
	if not FileAccess.file_exists(path):
		return null
	var stream: AudioStream = load(path)
	if stream == null:
		return null
	if loop and stream is AudioStreamWAV:
		(stream as AudioStreamWAV).loop_mode = AudioStreamWAV.LOOP_FORWARD
	var pl := AudioStreamPlayer.new()
	pl.stream = stream
	pl.volume_db = db
	add_child(pl)
	return pl

## An element becomes interactive the moment it is fully ink — never before.
## Choice rows wake their slots and stay transparent themselves; a lone control
## (a page-turn arrow) wakes directly.
func _wake_row(row: Control) -> void:
	var woke_slot := false
	for s in row.get_children():
		if s is Control and (s as Control).has_meta("id"):
			(s as Control).mouse_filter = Control.MOUSE_FILTER_STOP
			woke_slot = true
	if not woke_slot:
		row.mouse_filter = Control.MOUSE_FILTER_STOP

## Everything lands NOW: ink, drawings, the field, the keyboard. Also the only
## exit — the sequence funnels here whether it played out or was skipped.
func _finish_reveal() -> void:
	if _reveal_tw != null and _reveal_tw.is_valid():
		_reveal_tw.kill()
	for it in _seq:
		match String(it.get("kind", "")):
			"line":
				var l: Label = it["label"]
				if is_instance_valid(l):
					l.visible_ratio = 1.0
			"icons":
				var row: Control = it["row"]
				if is_instance_valid(row):
					for s in row.get_children():
						if s is Control:
							(s as Control).modulate.a = 1.0
					_wake_row(row)
			"field":
				var f: Control = it["field"]
				var nb: Control = it["nib"]
				if is_instance_valid(f):
					f.modulate.a = 1.0
					if _revealing:
						f.grab_focus()
				if is_instance_valid(nb):
					nb.modulate.a = 1.0
			"fade":
				var nd: Control = it["node"]
				if is_instance_valid(nd):
					nd.modulate.a = 1.0
					_wake_row(nd)
	_seq.clear()
	_revealing = false
	if _scratch != null and is_instance_valid(_scratch) and _scratch.playing:
		_scratch.stop()
	if _pen != null and is_instance_valid(_pen):
		_pen.queue_free()
		_pen = null

## How long an element deserves at an unhurried hand, before the page budget.
func _item_secs(it: Dictionary) -> float:
	match String(it.get("kind", "")):
		"line":
			var l: Label = it["label"]
			var cps: float = TITLE_CPS if int(it["sz"]) == SIZE_TITLE else WRITE_CPS
			return maxf(float(l.text.length()) / cps, 0.12)
		"icons":
			var row: Control = it["row"]
			return ICON_IN + ICON_STAGGER * float(row.get_child_count())
		"field":
			return 0.22
		"fade":
			return 0.18
	return 0.1

func written_text() -> String:
	return _input.text.strip_edges() if _input != null else ""

## The live field itself, for hosts whose chips write INTO the pen (a tapped
## intent fills the sentence; the player edits or locks). Null before write_field.
func input_field() -> TextEdit:
	return _input if _input != null and is_instance_valid(_input) else null

## THE SHAPE EVERY PAGE ENDS IN (owner: each page states the situation, then asks
## what you want to do). Composing it here means no page can forget the written
## move or bury the question.
##
## THE ANSWER SPACE IS RESERVED FIRST. The old order let a long situation spend
## the whole sheet and leave the choices and the written move to fight over the
## curl of the paper. Now the row and the field are measured BEFORE the prose is
## laid, the prose gets what remains (and is cut to it, mid-story, with an
## ellipsis — a trimmed anecdote is a smaller loss than a missing answer), and
## the page always ends the way the game is played: with room to act.
func ask(situation: String, options: Array, allow_write: bool = true,
		prompt: String = "...or write what you actually do") -> Control:
	_cascade("body")
	var pitch := rule_pitch()
	var start: float = _snap(float(_cursor.get("body", 0.0)))
	var reserve: float = _row_estimate(options) + GAP
	if allow_write:
		# the prompt line (when one is wanted), then two ruled lines of field
		reserve += pitch * 2.0 + GAP
		if prompt != "":
			reserve += _line_advance(SIZE_BODY) + GAP
	var avail: float = _hard_floor() - start - reserve
	var fit: int = maxi(int(floor(avail / _line_advance(SIZE_BODY))), 2)
	var lines := _wrap_lines(situation, SIZE_BODY)
	var told := situation
	if lines.size() > fit:
		var kept := lines.slice(0, fit)
		var lastl := String(kept[fit - 1])
		var cut := lastl.rfind(" ")
		kept[fit - 1] = (lastl.substr(0, cut) if cut > 24 else lastl) + " …"
		told = " ".join(kept)
		push_warning("JournalPage: situation trimmed from %d to %d rules so the answer space survives."
				% [lines.size(), fit])
	line(told, false, "body")
	var row := icon_row(options)
	if allow_write:
		write_field(prompt)
	return row

## What an icon_row of these items WILL measure, computed the same way icon_row
## computes it — the budget and the build can never disagree.
func _row_estimate(items: Array) -> float:
	if items.is_empty():
		return 0.0
	var sp := span_at(0.0)
	var avail: float = sp.y - sp.x
	var n: int = maxi(items.size(), 1)
	var step: float = minf(124.0 + 28.0, avail / float(n))
	var cap_w: float = maxf(step - 14.0, 60.0)
	var cap_h := 0.0
	var draws := false
	for it in items:
		var d: Dictionary = it
		cap_h = maxf(cap_h, _font.get_multiline_string_size(
				_cap_lines(String(d.get("text", "")), cap_w),
				HORIZONTAL_ALIGNMENT_CENTER, cap_w, SIZE_BODY).y)
		if d.get("tex") != null:
			draws = true
	cap_h = maxf(cap_h, SIZE_BODY * 1.2)
	var icon_h: float = maxf(116.0 - cap_h - CAP_GAP, ICON_MIN_H) if draws else 0.0
	return icon_h + (CAP_GAP if draws else 0.0) + cap_h

## THE OVERVIEW PAGE: reads the run back to the player in their own hand, then asks
## what to do next.
func overview(state_lines: Array, whats_next: String, options: Array) -> Control:
	for s in state_lines:
		if s is Dictionary:
			line(String(s.get("text", "")), bool(s.get("faint", false)))
		else:
			line(String(s))
	return ask(whats_next, options)

## A founder annotates their own log. A margin mark beside the line just written:
## a quick star when the world said brilliant, a hard double strike when it said
## backfired. It rides the reveal like everything else.
func margin_mark(kind: String) -> void:
	var m := _MarginMark.new()
	m.kind = kind
	m.mouse_filter = Control.MOUSE_FILTER_IGNORE
	# inside the paper: x=8 sat on the torn edge and the mark came out half-cut
	m.position = Vector2(16.0, _last_block_y - 6.0)
	m.set_deferred("size", Vector2(34, 40))
	space.add_child(m)
	if not instant:
		m.modulate = Color(1, 1, 1, 0)
		_enqueue({"kind": "fade", "node": m})

class _MarginMark:
	extends Control
	var kind := "star"
	func _draw() -> void:
		if kind == "star":
			# a hand star: five strokes through a centre, never lifted quite right
			var c := Vector2(17, 22)
			var rng := RandomNumberGenerator.new()
			rng.seed = 31
			for i in 5:
				var a := TAU * float(i) / 5.0 - PI * 0.5 + rng.randf_range(-0.06, 0.06)
				var r := 13.0 + rng.randf_range(-1.5, 1.5)
				draw_line(c - Vector2(cos(a), sin(a)) * r * 0.3,
						c + Vector2(cos(a), sin(a)) * r, JournalPage.PEN, 3.5, true)
		else:
			# backfired: two hard slashes, the second angrier
			draw_line(Vector2(4, 30), Vector2(30, 12), JournalPage.PEN, 4.0, true)
			draw_line(Vector2(8, 34), Vector2(32, 18), JournalPage.PEN, 4.5, true)

## Navigation lives in the CONTROLS zone and is drawn, never chrome.
func arrows(show_prev: bool, show_next: bool) -> void:
	# Anchored to the VISIBLE paper, not the zone fractions of the whole sheet — the
	# old maths parked the arrows below the sheet's own bottom edge, so no page-turn
	# arrow has ever actually been seen. They sit in the far corners of the last
	# writable band, clear of the centred lock line.
	var y: float = writable_bottom() - 56.0
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
	_cascade(zone)
	# Sit ON the printed ruling. Text that floats between the rules is the single
	# strongest "a text engine did this" tell; snapping every baseline to a rule and
	# advancing by whole rules is what makes it read as handwriting.
	var y: float = _snap(float(_cursor.get(zone, 0.0)))
	var lh: float = _line_advance(sz)
	# Content stops at the fence; only the controls band itself may use it. A line
	# that will not fit is CUT, with an ellipsis on the last line that did — prose
	# is the one thing on this page that can lose a tail and still work. The
	# alternative was the writing prompt printed onto the room, every stress run.
	var fence: float = writable_bottom() if zone == "controls" else _hard_floor()
	_last_block_y = y
	var lines := _wrap_lines(text, sz, align)
	var placed_any := false
	var last: Label = null
	for li in lines.size():
		if y + lh > fence + 0.5 and placed_any:
			if last != null:
				var t := last.text
				var cut := t.rfind(" ")
				last.text = (t.substr(0, cut) if cut > 24 else t) + " …"
			push_warning("JournalPage: %s cut %d line%s at the paper's fence — shorten this copy."
					% [zone, lines.size() - li, "" if lines.size() - li == 1 else "s"])
			break
		var sp := span_at(y + lh * 0.5)
		last = _place_l(lines[li], sz, col, sp, y, lh, align)
		placed_any = true
		y += lh
	_cursor[zone] = y + GAP
	_wrote[zone] = true
	_overrun(zone)

## A caption capped at three wrapped lines, cut with an ellipsis past that.
const CAP_MAX_LINES := 3

func _cap_lines(text: String, w: float) -> String:
	# explicit newlines are the caller's layout and are kept as line breaks
	var out := PackedStringArray()
	for seg in text.split("\n"):
		if out.size() >= CAP_MAX_LINES:
			break
		var words := String(seg).split(" ", false)
		var cur := ""
		for wd in words:
			var trial: String = String(wd) if cur == "" else cur + " " + String(wd)
			if _font.get_string_size(trial, HORIZONTAL_ALIGNMENT_CENTER, -1, SIZE_BODY).x <= w or cur == "":
				cur = trial
			else:
				out.append(cur)
				cur = String(wd)
				if out.size() >= CAP_MAX_LINES:
					break
		if cur != "" and out.size() < CAP_MAX_LINES:
			out.append(cur)
		elif cur != "" and out.size() >= CAP_MAX_LINES:
			out[CAP_MAX_LINES - 1] = String(out[CAP_MAX_LINES - 1]) + " …"
			break
	return "\n".join(out)

## Prose that must leave room for what follows it: trimmed with an ellipsis to
## fit above `reserve` pixels of the fence, never below two rules. The same
## contract ask() gives its situation, offered to any host block.
func line_fitted(text: String, reserve: float, zone: String = "body", faint: bool = false) -> void:
	_cascade(zone)
	var start: float = _snap(float(_cursor.get(zone, 0.0)))
	var avail: float = _hard_floor() - start - reserve
	# SHRINK BEFORE CUTTING (owner: "text is being too much cut, so unclear"):
	# a smaller hand keeps the whole thought; the ellipsis only survives as the
	# final fallback when even 24px cannot hold it.
	for sz in [SIZE_BODY, 30, 27]:
		var fit_s: int = maxi(int(floor(avail / _line_advance(sz))), 1)
		if _wrap_lines(text, sz).size() <= fit_s:
			_shaped(text, sz, FAINT if faint else INK, zone, HORIZONTAL_ALIGNMENT_LEFT)
			return
	# even a starved page keeps TWO small lines — one line cut mid-sentence
	# ("…own …") reads as a rendering bug, not a diary
	var fit: int = maxi(int(floor(avail / _line_advance(27))), 2)
	var lines := _wrap_lines(text, 27)
	var told := text
	if lines.size() > fit:
		var kept := lines.slice(0, fit)
		var lastl := String(kept[fit - 1])
		var cut := lastl.rfind(" ")
		kept[fit - 1] = (lastl.substr(0, cut) if cut > 24 else lastl) + " …"
		told = " ".join(kept)
	_shaped(told, 27, FAINT if faint else INK, zone, HORIZONTAL_ALIGNMENT_LEFT)

## Greedy wrap against the constant writable span — one shared implementation, so
## measuring for a budget and placing for real can never disagree.
func _wrap_lines(text: String, sz: int, align: int = HORIZONTAL_ALIGNMENT_LEFT) -> PackedStringArray:
	var out := PackedStringArray()
	var sp := span_at(0.0)
	var avail: float = sp.y - sp.x
	var words := text.split(" ", false)
	var cur := ""
	for w in words:
		var trial: String = String(w) if cur == "" else cur + " " + String(w)
		if _font.get_string_size(trial, align, -1, sz).x <= avail or cur == "":
			cur = trial
		else:
			out.append(cur)
			cur = String(w)
	if cur != "":
		out.append(cur)
	return out

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
	# THE EPSILON IS LOAD-BEARING. Every zone starts at first + k*pitch, so the
	# division is a whole number in arithmetic and a hair above it in floats — and a
	# hair above sent ceil() to the NEXT rule, throwing away a full 51px line at the
	# top of every zone and at every cascade. That lost rule is why a page with two
	# icon rows pushed the written move off the bottom of the paper. 0.01 of a rule is
	# half a pixel: it can only ever pull a baseline back onto the rule it is already
	# sitting on.
	return first + ceil((y - first) / pitch - 0.01) * pitch

## A line occupies whole rules: body text ONE, a big title two. The 0.78 factor is
## load-bearing — a font's reported height includes ascender and descender padding
## that handwriting does not visually occupy, so measuring raw height pushed body
## text to two rules and opened a blank line between every wrapped line.
func _line_advance(sz: int) -> float:
	var pitch := rule_pitch()
	if pitch <= 1.0:
		return _font.get_height(sz) * 1.08
	return pitch * max(1.0, ceil(_font.get_height(sz) * 0.78 / pitch))

func _place_l(text: String, sz: int, col: Color, sp: Vector2, y: float, lh: float, align: int) -> Label:
	var l := Label.new()
	l.add_theme_font_override("font", _font)
	l.add_theme_font_size_override("font_size", sz)
	l.add_theme_color_override("font_color", col)
	# A centred label re-centres itself around every character it reveals, so the
	# words would slide while being written. Centring is done HERE once — measure,
	# park the label at the centred x, and let it reveal left-to-right like every
	# other written line. The hand starts where the word will start.
	l.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	var x := sp.x
	if align == HORIZONTAL_ALIGNMENT_CENTER:
		var w := _font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, sz).x
		x = sp.x + maxf((sp.y - sp.x - w) * 0.5, 0.0)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	l.text = text
	l.position = Vector2(x, y)
	l.set_deferred("size", Vector2(sp.y - x, lh))
	space.add_child(l)
	if not instant:
		l.visible_ratio = 0.0
		_enqueue({"kind": "line", "label": l, "sz": sz})
	return l

## A ZONE SLIDING DOWN IS THE MECHANISM WORKING, NOT A FAULT. Zones cascade, so a
## zone that passes its nominal rule is simply being pushed by the one above it and
## the page still renders correctly — a full 40-week run proved it, 108 times, with
## no text ever landing on text. Warning on that left no signal to hear a real fault
## with, so the only thing warned about now is the fault the owner actually rejects
## the page for: ink past the bottom edge of the paper, printed onto the room. The
## message says what to cut, because "overran" alone never told anyone what to do.
func _overrun(zone: String) -> void:
	var y: float = float(_cursor.get(zone, 0.0))
	var bot := writable_bottom()
	if y <= bot:
		return
	var over: float = y - bot
	var lines: int = maxi(1, int(ceil(over / maxf(_line_advance(SIZE_BODY), 1.0))))
	push_warning(("JournalPage[%s p%d]: %s ran %.0fpx off the bottom of the paper (%.0f > %.0f) — "
			+ "cut %d line%s of copy from this page, shorten the captions, or move %s onto the next sheet.")
			% [_tag, get_index(), zone, over, y, bot, lines, "" if lines == 1 else "s", zone])
	if OS.get_environment("RUNWAY_PAGE_DEBUG") == "1":
		for c in space.get_children():
			if c is Label:
				print("  PAGE[%s] y=%.0f  %s" % [_tag, (c as Control).position.y,
					String((c as Label).text).left(48).replace("\n", "¶")])
			elif c is Control and c.get_child_count() > 0:
				print("  PAGE[%s] y=%.0f  <row %d items h=%.0f>" % [_tag,
					(c as Control).position.y, c.get_child_count(), (c as Control).size.y])

## Chosen is circled in ink; the rest simply go quiet. Never a border, never a
## fill, never a highlight — those read as a form. And the circle is DRAWN, the
## way a pen actually closes a loop around a word: instant appearance was the
## one tell left that a computer, not a hand, keeps this book.
func _select(row: Control, id: String) -> void:
	for slot in row.get_children():
		var mine: bool = String(slot.get_meta("id", "")) == id
		var tw := create_tween()
		tw.tween_property(slot, "modulate", Color.WHITE if mine else Color(1, 1, 1, 0.55), 0.14)
		for c in slot.get_children():
			if c is _PenCircle:
				var pc := c as _PenCircle
				if mine and not pc.visible:
					pc.visible = true
					pc.progress = 0.0
					if _scribble != null and is_instance_valid(_scribble):
						_scribble.play()
					var ct := create_tween()
					ct.tween_method(func(p: float) -> void:
						pc.progress = p
						pc.queue_redraw(), 0.0, 1.0, 0.24).set_trans(Tween.TRANS_SINE)
				elif not mine:
					pc.visible = false
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
	if not instant:
		b.modulate = Color(1, 1, 1, 0)
		b.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_enqueue({"kind": "fade", "node": b})
	space.add_child(b)
	return b

## Marks the writing area as a writing area: the rules you write on, and a pen nib
## resting at the first one until you have written something. The rules sit at the
## PAGE'S pitch, aligned just under where the field actually draws its baselines,
## so the guide can never strike through the player's own words.
class _WriteHint:
	extends Control
	var written := false
	var focused := false
	var pitch := 48.0
	var ascent := 34.0
	var edit: TextEdit          ## the field whose scroll these rules must ride
	var _last_scroll := -1.0

	func _process(_d: float) -> void:
		# WHEN THE FIELD SCROLLS, THE RULING SCROLLS WITH IT. Static rules read
		# as aligned only until the third line; then every rule struck through a
		# word. The guide is the paper under the words, so it moves as one.
		if edit != null and is_instance_valid(edit):
			var sv := edit.scroll_vertical
			if absf(sv - _last_scroll) > 0.001:
				_last_scroll = sv
				queue_redraw()

	func _draw() -> void:
		var strong := focused or not written
		# one rule under each LINE SLOT, phase-shifted by the field's scroll
		var shift := 0.0
		if edit != null and is_instance_valid(edit):
			shift = fmod(maxf(edit.scroll_vertical, 0.0) * pitch, pitch)
		var y: float = (pitch + 1.0) - shift
		if y < 11.0:
			y += pitch
		while y < size.y + 2.0:
			var pts := PackedVector2Array()
			var rng := RandomNumberGenerator.new()
			rng.seed = 17
			for i in 33:
				pts.append(Vector2(size.x * float(i) / 32.0, y + rng.randf_range(-1.0, 1.0)))
			draw_polyline(pts, Color(JournalPage.PEN, 0.55 if strong else 0.30),
					3.0 if strong else 2.5, true)
			y += maxf(pitch, 24.0)
		if not written:
			draw_circle(Vector2(2.0, pitch - 13.0), 4.5, Color(JournalPage.PEN, 0.9))

## The pen that rides the tip of the ink while the page writes itself. At page
## scale the first draft read as a stray tick, so this is a real slender pen:
## dark barrel at the writing angle, coral cap at the heel, tip on the baseline.
class _PenTip:
	extends Control
	func _draw() -> void:
		draw_line(Vector2(4, 37), Vector2(27, 9), JournalPage.INK, 5.0, true)
		draw_line(Vector2(24, 13), Vector2(31, 4), Color(JournalPage.PEN, 0.95), 6.5, true)
		draw_line(Vector2(4, 37), Vector2(10, 30), Color(0, 0, 0, 0.35), 2.0, true)
		draw_circle(Vector2(3.4, 38.0), 2.6, JournalPage.INK)

class _PenCircle:
	extends Control
	var progress := 1.0
	func _draw() -> void:
		var c := size * 0.5
		# THE LOOP HAS TO GET ROUND WHAT IT IS CIRCLING. It is sized to the cell, and a
		# cell is now a full-size drawing PLUS a caption that may wrap, so cells are tall.
		# A true ellipse pinches in exactly where a tall cell's caption is widest and left
		# the words sticking out of the ink. Squaring the loop off toward the cell's own
		# corners as it gets taller keeps it a hand-drawn pen ring and actually encloses
		# the item. It only ever grows INSIDE the cell, so it still cannot touch a
		# neighbouring cell or reach the edge of the row.
		var n: float = clampf(2.0 + size.y / maxf(size.x, 1.0), 2.0, 4.5)
		var rx: float = size.x * 0.48
		var ry: float = size.y * 0.47
		var pts := PackedVector2Array()
		var rng := RandomNumberGenerator.new()
		rng.seed = 9
		for i in 41:
			var t := TAU * float(i) / 40.0 - PI * 0.5
			var w := 1.0 + rng.randf_range(-0.05, 0.05)
			var cs := cos(t)
			var sn := sin(t)
			pts.append(c + Vector2(
					signf(cs) * pow(absf(cs), 2.0 / n) * rx * w,
					signf(sn) * pow(absf(sn), 2.0 / n) * ry * w))
		# The pen draws the loop rather than stamping it: `progress` is how far
		# around the hand has got. The stroke starts at the top the way a real
		# circling gesture does, and the final frame is byte-identical to the
		# stamp it replaced.
		var upto: int = clampi(int(ceil(progress * 41.0)), 2, 41)
		draw_polyline(pts.slice(0, upto), JournalPage.PEN, 4.0, true)

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
