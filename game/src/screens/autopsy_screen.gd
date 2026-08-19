class_name AutopsyScreen
extends Control
## THE LAST PAGE — ONE portrait sheet of the founder's log book, tilted a few
## degrees and laid over the live room, which keeps playing behind it.
##
## Everything on the sheet is written by the same hand at one of two sizes:
## the title, one line naming how the run ended, the payout on its own line,
## then the causal chain as a short column of small drawn icons with one line
## each, and the way onward inked into the bottom corner. Nothing else exists.
##
## THE GEOMETRY THAT MATTERS: logbook_page.png draws the sheet already tilted
## INSIDE the image (about -4 degrees) with its own soft shadow baked in. So the
## paper's edges and its printed rules are NOT parallel to the texture's frame.
## All writing therefore lives in one Control (_sheet) that is rotated to match
## the drawn paper and sized to it — every element is laid out in that sheet's
## simple axis-aligned local coordinates, and the tilt is applied exactly once,
## by the parent. Nothing is ever positioned in screen space.

signal done

const PALETTE := {
	"cream": Color("F2EAD3"), "ink": Color("1E1E1E"), "coral": Color("E86A5C"),
	"yellow": Color("F4B942"), "sage": Color("8FA582"), "blue": Color("6E8CA0"),
	"night": Color("2C3238"),
}

# ONE hand, TWO sizes. There is no third size and no second typeface.
const T_TITLE := 64
const T_BODY := 34

const PAGE_ART := "res://assets/ui/logbook_page.png"
const ART_PX := Vector2(1095.0, 1462.0)
# the sheet fills the frame the way the reference book does, with the room
# still showing past every edge
const PAGE_SIZE := Vector2(719.0, 960.0)      # exactly the art's aspect: no shear
const PAGE_POS := Vector2(408.0, 32.0)
const PAGE_TILT := -0.012                     # a hair of lean on top of the drawn one

# the paper quad, measured off logbook_page.png by walking its drawn edges row by
# row (origin = its top-left corner, extent along its OWN axes). The first pass
# read the stacked sheets behind as part of the page and came out 26px too wide,
# which pushed the right margin in tighter than the left.
const PAPER_ORIGIN_TEX := Vector2(74.0, 152.0)
const PAPER_SIZE_TEX := Vector2(858.0, 1232.0)
const PAPER_TILT := -0.069

const SCALE := PAGE_SIZE.x / ART_PX.x
const SHEET_POS := Vector2(PAPER_ORIGIN_TEX.x * SCALE, PAPER_ORIGIN_TEX.y * SCALE)
const SHEET_SIZE := Vector2(PAPER_SIZE_TEX.x * SCALE, PAPER_SIZE_TEX.y * SCALE)

# margins inside the paper: clear of the torn left edge and of the curled corner
const MARGIN_X := 70.0
# The bottom-right corner peels up off the sheet. Walking the flap's drawn edge
# row by row and mapping it into sheet space: it bites in to x 481 at y 710 and
# x 468 by y 758, so the exit row on the last rule has to stop well short of the
# column edge. An earlier guess of 28 put the arrow's tip on the fold.
const CURL_INSET := 52.0
const TEXT_W := SHEET_SIZE.x - 2.0 * MARGIN_X
const ICON_COL := 74.0
const ICON_PX := 58.0
const CHAIN_W := TEXT_W - ICON_COL
const CHAIN_MAX := 4

# THE RULING. The page is printed with 16 rules; writing that floats between them
# is the loudest "this is a text box on a paper texture" tell there is. Measured
# off the PNG (pitch 65.6 texture px, first rule at texture y 270 on the x=300
# column) and carried into the sheet's own space, where they are exactly level.
# Every baseline in this file lands on one of these and nowhere else.
const RULE_0 := 87.9
const RULE_PITCH := 43.08
const RULE_FIRST := 1          # rule 0 sits under the torn top edge
const RULE_LAST := 15          # the last rule the paper carries; the exit rides it
# A font reports a height that includes ascender and descender padding the ink
# never occupies. Dividing the reported height by the pitch therefore claims body
# text needs two rules and opens a blank line between wrapped lines; 0.78 of it is
# what the writing actually covers, which gives body one rule and a title two.
const INK_FILL := 0.78

var headline := ""
var record: RunRecord
var state: GameState
var _font: Font
var _armed := false
var _ink: Array = []          # everything written, for the reveal
var _page: Control
var _sheet: Control
var _last_written: Label      # the Label _write just laid down, for callers that animate it

func setup(p_headline: String, p_record: RunRecord, p_state: GameState = null) -> void:
	headline = p_headline
	record = p_record
	state = p_state

func _ready() -> void:
	_font = load("res://assets/fonts/PatrickHand-Regular.ttf")
	set_anchors_preset(Control.PRESET_FULL_RECT)
	var bg := ColorRect.new()
	bg.color = PALETTE["night"]
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(bg)

	# the room behind the page is the LIVE room, still playing its ambient loop —
	# the last page is written in the place the run actually happened
	var room := SceneRoom.new()
	room.size = Vector2(1536, 1024)
	room.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(room)
	room.load_scene(SceneRoomPicker.scene_id_for(state) if state else "garage_steady")
	var shade := ColorRect.new()
	shade.color = Color(0, 0, 0, 0.3)
	shade.set_anchors_preset(Control.PRESET_FULL_RECT)
	shade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(shade)

	# ONE portrait sheet on top of the room. The art carries its own soft shadow,
	# so nothing is ever drawn behind it.
	_page = Control.new()
	_page.position = PAGE_POS
	_page.size = PAGE_SIZE
	_page.pivot_offset = PAGE_SIZE * 0.5
	_page.rotation = PAGE_TILT
	_page.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_page)
	if ResourceLoader.exists(PAGE_ART):
		var art := TextureRect.new()
		art.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		art.stretch_mode = TextureRect.STRETCH_SCALE
		art.mouse_filter = Control.MOUSE_FILTER_IGNORE
		art.custom_minimum_size = PAGE_SIZE
		art.texture = load(PAGE_ART)
		art.set_deferred("size", PAGE_SIZE)
		_page.add_child(art)
	else:
		var fallback := ColorRect.new()
		fallback.color = PALETTE["cream"]
		fallback.mouse_filter = Control.MOUSE_FILTER_IGNORE
		fallback.set_deferred("size", PAGE_SIZE)
		_page.add_child(fallback)

	# the writing frame: rotated onto the drawn paper, sized to it. Every line,
	# icon and mark below is a child of this and nothing else.
	_sheet = Control.new()
	_sheet.position = SHEET_POS
	_sheet.size = SHEET_SIZE
	_sheet.pivot_offset = Vector2.ZERO
	_sheet.rotation = PAPER_TILT
	_sheet.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_page.add_child(_sheet)

	_page.modulate.a = 0.0
	_page.scale = Vector2(0.985, 0.985)
	var bt := create_tween()
	bt.tween_property(_page, "modulate:a", 1.0, 0.3)
	bt.parallel().tween_property(_page, "scale", Vector2.ONE, 0.45) \
		.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)

	_build_page()
	_reveal()
	_arm()

# ------------------------------------------------------------------ measuring
## The ONLY legal way to advance y in this file: ask the font how tall the
## wrapped text actually is. Character-count estimates shipped an overlap twice.
func _h(text: String, size: int, width: float) -> float:
	return _font.get_multiline_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, width, size).y

func _line_h(size: int) -> float:
	return _font.get_multiline_string_size("Hg", HORIZONTAL_ALIGNMENT_LEFT, 4000.0, size).y

func _rule_y(k: int) -> float:
	return RULE_0 + RULE_PITCH * float(k)

## how many rules one written line of this size covers
func _span(size: int) -> int:
	return maxi(1, int(ceil(_line_h(size) * INK_FILL / RULE_PITCH)))

## how many lines a block wraps to at this width
func _lines(text: String, size: int, w: float) -> int:
	return maxi(1, int(round(_h(text, size, w) / maxf(1.0, _line_h(size)))))

## Write one block with its FIRST baseline resting on rule `k`. Wrapped lines
## advance by exactly one ruling (or two, for the title), so every line in the
## block lands on a printed rule. Returns the number of rules it consumed.
func _write(text: String, size: int, k: int, x: float, w: float,
		centred := false) -> int:
	var span := _span(size)
	var lines := _lines(text, size, w)
	var h := RULE_PITCH * float(span * lines)
	var l := Label.new()
	l.text = text
	l.add_theme_font_override("font", _font)
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", PALETTE["ink"])
	# the leading IS the ruling
	l.add_theme_constant_override("line_spacing",
		int(round(RULE_PITCH * float(span) - _font.get_height(size))))
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	if centred:
		l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	l.custom_minimum_size = Vector2(w, h)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_sheet.add_child(l)
	l.position = Vector2(x, _rule_y(k) - _font.get_ascent(size))
	l.set_deferred("size", Vector2(w, h))
	_ink.append(l)
	_last_written = l
	return span * lines

# -------------------------------------------------------------------- content
## Laid out in two passes: measure everything first, then place it. The second
## pass is what lets the leftover height be shared back out as leading instead of
## piling up as dead paper under the last line.
func _build_page() -> void:
	var title := "THE LAST PAGE"
	var cause := _fit(_compress(headline.split("\n")[0]), T_BODY, TEXT_W, 3)
	var payout := "you walked away with $%s" % _fmt(state.payout_today() if state else 0)

	var r_title := _span(T_TITLE) * _lines(title, T_TITLE, TEXT_W)
	var r_cause := _span(T_BODY) * _lines(cause, T_BODY, TEXT_W)
	var r_payout := _span(T_BODY)
	var chain_last := RULE_LAST - 2          # one clear rule above the exit

	# blank rules between the three blocks; whatever the chain does not need is
	# shared back out into them so a thin run still reads as a composed page
	var gap: PackedInt32Array = [1, 1, 1]
	var k0: int = RULE_FIRST + r_title + r_cause + r_payout + gap[0] + gap[1] + gap[2]
	var budget: int = chain_last - k0 + 1
	var rows := _chain_rows(budget)
	var slack: int = budget - _column_rules(rows)
	var i := 0
	while slack > 0 and i < 6:
		if gap[i % 3] < 2:
			gap[i % 3] += 1
			slack -= 1
		i += 1

	var k := RULE_FIRST
	k += _write(title, T_TITLE, k, MARGIN_X, TEXT_W, true) + gap[0]
	k += _write(cause, T_BODY, k, MARGIN_X, TEXT_W) + gap[1]
	k += _write(payout, T_BODY, k, MARGIN_X, TEXT_W) + gap[2]

	for r in rows:
		var n := int(r["rules"])
		if k + n - 1 > chain_last:
			break
		# the evidence is centred on the whole beat, not on its first line
		var mid := (_rule_y(k) + _rule_y(k + n - 1)) * 0.5 - 13.0
		var icon := ChainIcon.new()
		icon.kind = int(r["kind"])
		icon.custom_minimum_size = Vector2(ICON_PX, ICON_PX)
		icon.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_sheet.add_child(icon)
		icon.position = Vector2(MARGIN_X - 6.0, mid - ICON_PX * 0.5)
		icon.set_deferred("size", Vector2(ICON_PX, ICON_PX))
		_ink.append(icon)
		_write(String(r["text"]), T_BODY, k, MARGIN_X + ICON_COL, CHAIN_W)
		k += n + 1

	# the way onward, inked into the bottom corner clear of the curl
	_build_exit(RULE_LAST)

## The chain: one drawn icon and one handwritten line per beat. Every beat starts
## at its full length and is shortened a word at a time, longest first, only for
## as long as the column overflows — so a thin run keeps its detail and a fat one
## still lands on the paper.
func _chain_rows(budget: int) -> Array:
	var raw: Array = record.causal_lines() if record else []
	var key := _compress(headline.split("\n")[0]).to_lower()
	var beats: Array = []
	for line in raw:
		var e := _split(String(line))
		var t: String = String(e["text"]).to_lower()
		# the death line already IS the cause line at the top of the page
		if t.length() > 10 and (key.contains(t) or t.contains("died:")):
			continue
		beats.append(e)
	if beats.is_empty() or budget < 1:
		return []

	# a SHORT column: how it started, then the last moves before the end
	for keep in [CHAIN_MAX, 3, 2, 1]:
		var rows: Array = []
		for e in _pick(beats, keep):
			var txt := String(e["draw"])
			rows.append({
				"text": txt, "rules": _lines(txt, T_BODY, CHAIN_W), "kind": e["kind"],
			})
		var guard := 0
		while _column_rules(rows) > budget and guard < 200:
			guard += 1
			var worst := 0
			for i in rows.size():
				if int(rows[i]["rules"]) > int(rows[worst]["rules"]):
					worst = i
			var shorter := _drop_word(String(rows[worst]["text"]))
			if shorter == String(rows[worst]["text"]):
				break
			rows[worst]["text"] = shorter
			rows[worst]["rules"] = _lines(shorter, T_BODY, CHAIN_W)
		if _column_rules(rows) <= budget:
			return rows
	return []

## rules a column of beats needs, counting one blank rule between beats
func _column_rules(rows: Array) -> int:
	var t := 0
	for r in rows:
		t += int(r["rules"])
	return t + maxi(0, rows.size() - 1)

## RUN IT BACK: the arrow in the bottom corner, riding the paper's last rule.
func _build_exit(k: int) -> void:
	var label := "run it back"
	var tw: float = _font.get_string_size(label, HORIZONTAL_ALIGNMENT_LEFT, -1, T_BODY).x
	var right := MARGIN_X + TEXT_W - CURL_INSET
	var arrow_w := 104.0
	var arrow_h := 48.0
	var ax := right - arrow_w
	var tx := ax - 18.0 - tw
	_write(label, T_BODY, k, tx, tw + 10.0)
	var lab := _last_written
	var y := _rule_y(k) - 13.0        # the middle of the ink on that rule

	var arrow := NextArrow.new()
	arrow.custom_minimum_size = Vector2(arrow_w, arrow_h)
	arrow.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_sheet.add_child(arrow)
	arrow.position = Vector2(ax, y - arrow_h * 0.5)
	arrow.set_deferred("size", Vector2(arrow_w, arrow_h))
	_ink.append(arrow)

	var hit_size := Vector2(right - tx + 14.0, RULE_PITCH + 24.0)
	var hit := Control.new()
	hit.custom_minimum_size = hit_size
	hit.mouse_filter = Control.MOUSE_FILTER_STOP
	hit.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	_sheet.add_child(hit)
	hit.position = Vector2(tx - 10.0, y - hit_size.y * 0.5)
	hit.set_deferred("size", hit_size)
	hit.gui_input.connect(func(ev):
		if ev is InputEventMouseButton and ev.pressed and _armed:
			done.emit())
	# the line leans toward the arrow under the cursor — the only thing on this
	# page that answers back, so it has to answer
	hit.mouse_entered.connect(func():
		create_tween().tween_property(lab, "position:x", tx + 7.0, 0.16) \
			.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT))
	hit.mouse_exited.connect(func():
		create_tween().tween_property(lab, "position:x", tx, 0.2) \
			.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT))

	var pulse := create_tween().set_loops()
	pulse.tween_property(arrow, "position:x", ax + 7.0, 0.85).set_trans(Tween.TRANS_SINE)
	pulse.tween_property(arrow, "position:x", ax, 0.85).set_trans(Tween.TRANS_SINE)

# ---------------------------------------------------------------------- words
## how it started, then the last moves before the end
func _pick(beats: Array, keep: int) -> Array:
	if beats.size() <= keep:
		return beats
	if keep <= 1:
		return [beats[beats.size() - 1]]
	var out: Array = [beats[0]]
	out.append_array(beats.slice(beats.size() - (keep - 1)))
	return out

## Trim a line, whole words at a time, until it fits `max_lines` — a tired hand
## writes short, and nothing on a physical page trails off into an ellipsis.
func _fit(text: String, size: int, w: float, max_lines: int) -> String:
	var cap := _line_h(size) * max_lines + 2.0
	var guard := 0
	while _h(text, size, w) > cap and guard < 60:
		guard += 1
		var shorter := _drop_word(text)
		if shorter == text:
			break
		text = shorter
	return text

## Lose the last word — and then any word it was holding up. A hand-written line
## that stops on "…founded Blobium: the" reads as a rendering bug, so trailing
## articles, prepositions, bare numbers and punctuation go with it.
const TRAILING_JUNK := ["the", "a", "an", "and", "of", "with", "to", "for", "in",
	"on", "at", "his", "her", "their", "our", "my", "your", "into", "from", "by"]

func _drop_word(text: String) -> String:
	var words := text.split(" ")
	if words.size() <= 4:
		return text
	words.remove_at(words.size() - 1)
	var guard := 0
	while words.size() > 4 and guard < 12:
		guard += 1
		var last := String(words[words.size() - 1]).strip_edges()
		while last.ends_with(",") or last.ends_with(":") or last.ends_with(";") \
				or last.ends_with("."):
			last = last.substr(0, last.length() - 1)
		if last == "" or last.to_lower() in TRAILING_JUNK or last.is_valid_int():
			words.remove_at(words.size() - 1)
			continue
		words[words.size() - 1] = last
		break
	return " ".join(words).strip_edges()

## "Week 3 — The Roommate Talk → PITCH THEM" → "wk 3  the roommate talk — pitch them"
func _split(line: String) -> Dictionary:
	var tag := ""
	var text := line
	var dash := line.find(" — ")
	if dash > 0 and dash < 22:
		var head := line.substr(0, dash)
		text = line.substr(dash + 3)
		if head.begins_with("Week "):
			tag = "wk " + head.substr(5)
		elif head.begins_with("Night"):
			tag = "night 0"
		else:
			tag = head.to_lower()
	text = _compress(text)
	return {
		"tag": tag, "text": text, "kind": _kind(text),
		"draw": ("%s  %s" % [tag, text]) if tag != "" else text,
	}

## The run record writes for a log file; the page is written by a tired founder.
func _compress(text: String) -> String:
	text = text.replace("\"", "").replace(" *", "").strip_edges()
	text = text.replace("The Founding of ", "founded ")
	text = text.replace("cofounder(s)", "cofounders")
	var arrow := text.find(" → ")
	if arrow > 0:
		var head := text.substr(0, arrow)
		var tail := text.substr(arrow + 3)
		# a SHOUTED choice reads as a form field; lowercase it back into prose
		var caps := 0
		for c in tail:
			if c == c.to_upper() and c != c.to_lower():
				caps += 1
		if caps > tail.length() * 0.35:
			tail = tail.to_lower()
		tail = tail.replace(" · ", ", ")
		var parts := tail.split(", ")
		if parts.size() > 2:
			tail = ", ".join([parts[0], parts[1]])
		text = head + ": " + tail
	# a felt pen writes a colon, not a typesetter's em dash
	return text.replace(" — ", ": ").replace(" – ", ": ").strip_edges()

## the evidence beside each beat is DRAWN, never a glyph
func _kind(text: String) -> int:
	var t := text.to_lower()
	if t.contains("founded") or t.contains("grabbed") or t.contains("left behind") \
			or t.contains("leap"):
		return 4
	if t.contains("money") or t.contains("cash") or t.contains("payroll") \
			or t.contains("ramen") or t.contains("angel") or t.contains("raise") \
			or t.contains("seed") or t.contains("broke") or t.contains("burn"):
		return 0
	if t.contains("quit") or t.contains("walked") or t.contains("flatline") \
			or t.contains("morale") or t.contains("burnt") or t.contains("burnout") \
			or t.contains("fired") or t.contains("layoff") or t.contains("gone"):
		return 1
	if t.contains("product") or t.contains("ship") or t.contains("build") \
			or t.contains("mvp") or t.contains("launch") or t.contains("demo") \
			or t.contains("server") or t.contains("bug") or t.contains("code"):
		return 2
	if t.contains("user") or t.contains("customer") or t.contains("churn") \
			or t.contains("growth") or t.contains("press") or t.contains("sign") \
			or t.contains("talk") or t.contains("roommate") or t.contains("cofounder") \
			or t.contains("hire") or t.contains("crew") or t.contains("team") \
			or t.contains("partner") or t.contains("equity"):
		return 3
	return 5

# --------------------------------------------------------------------- pieces
func _reveal() -> void:
	for n in _ink:
		n.modulate.a = 0.0
	var tw := create_tween()
	tw.tween_interval(0.3)
	for n in _ink:
		tw.tween_property(n, "modulate:a", 1.0, 0.05)

func _fmt(v: int) -> String:
	var t := str(absi(v))
	var out := ""
	while t.length() > 3:
		out = "," + t.substr(t.length() - 3) + out
		t = t.substr(0, t.length() - 3)
	return ("-" if v < 0 else "") + t + out

class ChainIcon:
	extends Control
	## the state is DRAWN, not written: what was in the room at that beat
	## Drawn at 58x58: a flat-filled object in the palette, outlined in a wobbly
	## felt pen. Perfect circles and uniform strokes read as an icon font sitting
	## next to handwriting, which is the one thing this page cannot afford.
	const INK := Color("1E1E1E")
	var kind := 5

	## a hand cannot draw a true circle
	func _blob(c: Vector2, r: Vector2, fill: Color, w := 3.0, phase := 0.0) -> void:
		var pts := PackedVector2Array()
		for i in 29:
			var t := i / 28.0 * TAU
			var k := 1.0 + sin(t * 3.0 + phase) * 0.035 + sin(t * 7.0 + phase) * 0.018
			pts.append(c + Vector2(cos(t) * r.x, sin(t) * r.y) * k)
		if fill.a > 0.0:
			draw_colored_polygon(pts, fill)
		pts.append(pts[0])
		draw_polyline(pts, INK, w, true)

	## a hand cannot draw a true rectangle either
	func _slab(rect: Rect2, fill: Color, w := 3.0, phase := 0.0) -> void:
		var c := [rect.position, Vector2(rect.end.x, rect.position.y), rect.end,
			Vector2(rect.position.x, rect.end.y)]
		var pts := PackedVector2Array()
		for i in 4:
			var p0: Vector2 = c[i]
			var p1: Vector2 = c[(i + 1) % 4]
			var n := Vector2(p1.y - p0.y, p0.x - p1.x).normalized()
			for j in 7:
				var t := j / 6.0
				pts.append(p0.lerp(p1, t) + n * sin((i + t) * 2.7 + phase) * 1.1)
		if fill.a > 0.0:
			draw_colored_polygon(pts, fill)
		pts.append(pts[0])
		draw_polyline(pts, INK, w, true)

	func _draw() -> void:
		var cream := Color("F2EAD3")
		match kind:
			0:  # the money that ran out
				for i in 3:
					var y := 34.0 - i * 12.0
					_slab(Rect2(5, y, 47, 13), Color("8FA582"), 3.0, i * 1.9)
			1:  # somebody went — the face is scribbled out in pen
				_blob(Vector2(29, 27), Vector2(21, 22), cream, 3.2)
				draw_circle(Vector2(22, 21), 2.8, INK)
				draw_circle(Vector2(36, 21), 2.8, INK)
				draw_line(Vector2(7, 44), Vector2(51, 10), INK, 4.2)
				draw_line(Vector2(7, 11), Vector2(51, 45), INK, 4.2)
			2:  # the product that flatlined — a screen with a dead line on it
				_slab(Rect2(7, 8, 44, 31), cream, 3.2)
				draw_line(Vector2(13, 29), Vector2(45, 29), Color("6E8CA0"), 3.6)
				draw_line(Vector2(3, 46), Vector2(55, 46), INK, 3.6)
			3:  # the people in it — two faces, shoulder to shoulder, like the
				# crew row in the reference book
				for i in 2:
					var cx := 18.0 + i * 22.0
					_blob(Vector2(cx, 29), Vector2(13, 16), cream, 3.2, i * 1.7)
					draw_circle(Vector2(cx - 5, 25), 2.6, INK)
					draw_circle(Vector2(cx + 5, 25), 2.6, INK)
			4:  # night zero — the bulb over the bench
				_blob(Vector2(29, 23), Vector2(16, 17), Color("F4B942"), 3.2)
				draw_line(Vector2(21, 41), Vector2(37, 41), INK, 3.2)
				draw_line(Vector2(23, 48), Vector2(35, 48), INK, 3.2)
				draw_line(Vector2(6, 8), Vector2(13, 14), INK, 2.6)
				draw_line(Vector2(52, 8), Vector2(45, 14), INK, 2.6)
			_:  # the mug that got it through the week
				_slab(Rect2(8, 18, 29, 31), Color("E86A5C"), 3.2)
				draw_arc(Vector2(38, 30), 10.0, -PI * 0.55, PI * 0.55, 18, INK, 3.0)
				draw_line(Vector2(15, 12), Vector2(15, 3), INK, 2.6)
				draw_line(Vector2(27, 12), Vector2(27, 2), INK, 2.6)

class NextArrow:
	extends Control
	## The onward mark. It is the only thing on this page a player can press, so
	## it is drawn fat and swept with a solid head, the way the reference book's
	## corner arrows are — not a thin chevron that reads as UI chrome.
	func _draw() -> void:
		var ink := Color("1E1E1E")
		var y := size.y * 0.5
		var tip := Vector2(size.x - 3, y)
		# a swept shaft, thickening toward the head
		var shaft := PackedVector2Array()
		var back := PackedVector2Array()
		for i in 13:
			var t := i / 12.0
			var x := lerpf(3.0, size.x - 26.0, t)
			var dy := sin(t * PI) * -2.6
			var w := lerpf(2.2, 5.0, t)
			shaft.append(Vector2(x, y + dy - w))
			back.append(Vector2(x, y + dy + w))
		back.reverse()
		shaft.append_array(back)
		draw_colored_polygon(shaft, ink)
		# a solid head
		draw_colored_polygon(PackedVector2Array([
			tip, Vector2(size.x - 30.0, y - 15.0), Vector2(size.x - 24.0, y),
			Vector2(size.x - 32.0, y + 15.0)]), ink)

func _arm() -> void:
	await get_tree().create_timer(1.2).timeout
	_armed = true

func _unhandled_input(event: InputEvent) -> void:
	if _armed and event is InputEventKey and event.pressed:
		_armed = false
		done.emit()
