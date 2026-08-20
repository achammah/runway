class_name AutopsyScreen
extends JournalPage
## THE LAST PAGE — ONE portrait sheet of the founder's log book, laid over the live
## room, which keeps playing behind it.
##
## Everything on the sheet is written by the same hand at one of two sizes: the
## title, one line naming how the run ended, the payout on its own line, then the
## causal chain as a short column of small drawn icons with one line each, and the
## way onward inked into the bottom corner. Nothing else exists.
##
## THE GEOMETRY IS THE SHELL'S NOW. This page used to carry a private copy of the
## paper quad, the printed ruling and the layout maths, and the rest of the book
## drifted away from it. It inherits JournalPage instead, so the sheet, the rules
## every baseline lands on, and the four zones every page in the book shares are
## defined once. What stays here is only what the shell has no answer for: WHICH
## words go down, the compression that shortens a beat until the column fits the
## paper, the drawn evidence beside each beat, and the swept arrow in the corner.

signal done

# a SHORT column: how it started, then the last moves before the end
const CHAIN_MAX := 4
const ICON_PX := 58.0
const ICON_COL := 74.0        # what the drawn evidence reserves beside a beat
# The BODY zone holds two written facts, the cause and the payout, and the shell
# always leaves one printed rule between two blocks — so the cause gets two rules
# and is compressed into them rather than pushing the payout into the chain.
const CAUSE_LINES := 2
# The bottom-right corner peels up off the sheet. Walking the flap's drawn edge row
# by row and mapping it into sheet space: it bites in to x 481 at y 710 and x 468 by
# y 758, so the exit row has to stop well short of the writable right edge. An
# earlier guess of a 28px inset put the arrow's tip on the fold.
const CURL_X := 441.0
const NIGHT := Color("2C3238")

var headline := ""
var record: RunRecord
var state: GameState

var _armed := false
var _ink: Array = []          # everything written, in the order it was written
var _indent := 0.0            # see span_at(): the icon column, while the chain writes

func setup(p_headline: String, p_record: RunRecord, p_state: GameState = null) -> void:
	headline = p_headline
	record = p_record
	state = p_state

func _ready() -> void:
	# a backstop under the room: if a scene ever fails to mount, the page still sits
	# on night rather than on whatever the viewport was last cleared to
	var night := ColorRect.new()
	night.color = NIGHT
	night.set_anchors_preset(Control.PRESET_FULL_RECT)
	night.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(night)

	# the room behind the page is the LIVE room, still playing its ambient loop —
	# the last page is written in the place the run actually happened
	build("THE LAST PAGE", SceneRoomPicker.scene_id_for(state) if state else "garage_steady")

	_write_body()
	_build_chain()
	_build_exit()

	_ink = space.get_children()
	_land()
	_reveal()
	_arm()

# ------------------------------------------------------------------- the sheet
## The drawn page settles onto the room instead of appearing on it. The shell owns
## the TextureRect (it is `space`'s parent), so this only animates what it built.
func _land() -> void:
	var sheet := space.get_parent() as Control
	if sheet == null:
		return
	sheet.modulate.a = 0.0
	sheet.scale = Vector2(0.985, 0.985)
	var bt := create_tween()
	bt.tween_property(sheet, "modulate:a", 1.0, 0.3)
	bt.parallel().tween_property(sheet, "scale", Vector2.ONE, 0.45) \
		.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)

## The chain is the one place on this page where the writing does not start at the
## left margin: each beat is set beside its drawn evidence. The shell asks for the
## writable span at every line it lays down, so answering with the icon column
## already taken out is all an indented block needs — the wrapping, the snapping to
## the printed rules and the placement all stay the shell's.
func span_at(y: float) -> Vector2:
	var sp := super.span_at(y)
	return Vector2(sp.x + _indent, sp.y)

# ------------------------------------------------------------------ the writing
func _write_body() -> void:
	line(_fit(_compress(headline.split("\n")[0]), CAUSE_LINES))
	line("you walked away with $%s" % _fmt(state.payout_today() if state else 0))

## The chain: one drawn icon and one handwritten line per beat, filling the ENDING
## zone and nothing beyond it. Beats are measured in PRINTED RULES, because that is
## what the paper is actually divided into — a beat costs the rules its line wraps
## to, plus the one blank rule the shell leaves before the next block.
func _build_chain() -> void:
	var pitch := rule_pitch()
	# THE COLUMN OPENS ONE RULE BELOW THE LAST WRITTEN LINE, not at the zone's first
	# rule. The body can fill its zone right down to the boundary — a two-rule cause
	# plus the payout does — and the ending zone begins on the very next rule, so the
	# chain printed straight under the payout with no clear rule between them and the
	# first icon crowded the payout's descenders. Every other pair of blocks on this
	# page is separated by one blank rule; the zone seam has to obey the same rule.
	_cursor["ending"] = maxf(_snap(float(_cursor.get("ending", 0.0))),
		_snap(float(_cursor.get("body", 0.0))))
	_indent = ICON_COL
	var sp := span_at(0.0)
	var w: float = sp.y - sp.x
	for r in _chain_rows(_rules_left(pitch), w):
		var n := int(r["rules"])
		if _rules_left(pitch) < n:
			break
		var top := _snap(float(_cursor.get("ending", 0.0)))
		# the evidence is centred on the whole beat's ink, not on its first line
		var ink_h: float = float(n - 1) * pitch + _font.get_ascent(SIZE_BODY)
		_icon(int(r["kind"]), top + ink_h * 0.5)
		line(String(r["text"]), false, "ending")
	_indent = 0.0

## Printed rules the ending zone can still take, counted from where the next block
## would actually start — the cursor carries a trailing gap that the snap eats.
func _rules_left(pitch: float) -> int:
	var top := _snap(float(_cursor.get("ending", 0.0)))
	return int(floor((zone_bottom("ending") - top) / pitch + 0.02))

func _icon(kind: int, mid: float) -> void:
	var ic := ChainIcon.new()
	ic.kind = kind
	ic.custom_minimum_size = Vector2(ICON_PX, ICON_PX)
	ic.mouse_filter = Control.MOUSE_FILTER_IGNORE
	ic.position = Vector2(MARGIN_X, mid - ICON_PX * 0.5)
	ic.set_deferred("size", Vector2(ICON_PX, ICON_PX))
	space.add_child(ic)

## RUN IT BACK: the way onward, inked into the bottom corner on the last rule the
## paper carries, clear of the curl. It is the only thing here a player can press,
## so it is a drawn mark and not chrome. The shell's arrows() is for a book you can
## leaf through; this page has one way out and it is back to the start.
func _build_exit() -> void:
	var pitch := rule_pitch()
	var y: float = zone_bottom("controls") - pitch
	var right: float = min(span_at(y).y, CURL_X)
	var arrow_w := 104.0
	var arrow_h := 48.0
	var ax: float = right - arrow_w
	var text := "run it back"
	var tw: float = _font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, SIZE_BODY).x
	var tx: float = ax - 18.0 - tw
	# written by the shell's own placer, so the corner mark is the same hand at the
	# same body size as everything above it — it is simply not a full-width block
	_place_l(text, SIZE_BODY, INK, Vector2(tx, tx + tw + 10.0), y, pitch,
		HORIZONTAL_ALIGNMENT_LEFT)
	var lab := space.get_child(space.get_child_count() - 1) as Control
	var mid: float = y + _font.get_ascent(SIZE_BODY) * 0.5

	var arrow := NextArrow.new()
	arrow.custom_minimum_size = Vector2(arrow_w, arrow_h)
	arrow.mouse_filter = Control.MOUSE_FILTER_IGNORE
	arrow.position = Vector2(ax, mid - arrow_h * 0.5)
	arrow.set_deferred("size", Vector2(arrow_w, arrow_h))
	space.add_child(arrow)

	var hit_size := Vector2(right - tx + 14.0, pitch + 24.0)
	var hit := Control.new()
	hit.custom_minimum_size = hit_size
	hit.mouse_filter = Control.MOUSE_FILTER_STOP
	hit.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	hit.position = Vector2(tx - 10.0, mid - hit_size.y * 0.5)
	hit.set_deferred("size", hit_size)
	space.add_child(hit)
	hit.gui_input.connect(func(ev):
		if ev is InputEventMouseButton and ev.pressed and _armed:
			done.emit())
	# the line leans toward the arrow under the cursor — the only thing on this page
	# that answers back, so it has to answer
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
## Every beat starts at its full length and is shortened a word at a time, longest
## beat first, only for as long as the column overflows — so a thin run keeps its
## detail and a fat one still lands on the paper.
func _chain_rows(budget: int, w: float) -> Array:
	var raw: Array = record.causal_lines() if record else []
	var key := _compress(headline.split("\n")[0]).to_lower()
	var beats: Array = []
	for l in raw:
		var e := _split(String(l))
		var t: String = String(e["text"]).to_lower()
		# the death line already IS the cause line at the top of the page
		if t.length() > 10 and (key.contains(t) or t.contains("died:")):
			continue
		beats.append(e)
	if beats.is_empty() or budget < 1:
		return []

	for keep in [CHAIN_MAX, 3, 2, 1]:
		var rows: Array = []
		for e in _pick(beats, keep):
			var txt := String(e["draw"])
			rows.append({"text": txt, "rules": _wrapped(txt, w), "kind": e["kind"]})
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
			rows[worst]["rules"] = _wrapped(shorter, w)
		if _column_rules(rows) <= budget:
			return rows
	return []

## rules a column of beats needs, counting the one blank rule between two beats
func _column_rules(rows: Array) -> int:
	var t := 0
	for r in rows:
		t += int(r["rules"])
	return t + maxi(0, rows.size() - 1)

## How many lines the shell will break this text into at width `w`. It wraps
## greedily, word by word, so counting any other way — a character estimate, a
## multiline measure — can disagree by a line, and that line is the one that walks
## off the bottom of the zone.
func _wrapped(text: String, w: float) -> int:
	var n := 1
	var cur := ""
	for word in text.split(" ", false):
		var trial: String = String(word) if cur == "" else cur + " " + String(word)
		if _font.get_string_size(trial, HORIZONTAL_ALIGNMENT_LEFT, -1, SIZE_BODY).x <= w \
				or cur == "":
			cur = trial
		else:
			n += 1
			cur = String(word)
	return n

## how it started, then the last moves before the end
func _pick(beats: Array, keep: int) -> Array:
	if beats.size() <= keep:
		return beats
	if keep <= 1:
		return [beats[beats.size() - 1]]
	var out: Array = [beats[0]]
	out.append_array(beats.slice(beats.size() - (keep - 1)))
	return out

## Trim a line, whole words at a time, until it fits `max_lines` at the full
## writable width — a tired hand writes short, and nothing on a physical page
## trails off into an ellipsis.
func _fit(text: String, max_lines: int) -> String:
	var sp := span_at(0.0)
	var w: float = sp.y - sp.x
	var guard := 0
	while _wrapped(text, w) > max_lines and guard < 60:
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

## "Week 3 — The Roommate Talk → PITCH THEM" → "wk 3  the roommate talk: pitch them"
func _split(l: String) -> Dictionary:
	var tag := ""
	var text := l
	var dash := l.find(" — ")
	if dash > 0 and dash < 22:
		var head := l.substr(0, dash)
		text = l.substr(dash + 3)
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
