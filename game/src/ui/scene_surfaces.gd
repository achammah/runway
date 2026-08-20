class_name SceneSurfaces
extends Control
## DIEGETIC STATE — owned by MAIN, used by any screen that stands in a room.
##
## The room IS the save file. So the numbers belong to objects in the room: cash is
## written in the ledger, product on the whiteboard, customers on the wall chart,
## equity on a sticky note. They are NOT floating plates laid over the art.
##
## This exists because the alternative was shipping: "57% yours" on a cream plate
## slapped over a sticky note (and clipped by its own plate), "$-300" floating in
## the middle of the garage floor anchored to nothing, and a whiteboard drawn into
## the scene sitting completely empty beside them. The art already had the surfaces.
##
## HOW A SCENE DECLARES ITS SURFACES. In the scene's layout.json, alongside the
## cutout layers, add a "write_surfaces" object. Coordinates are in the scene's own
## 1536x1024 space, matching the layer coordinates already there:
##
##   "write_surfaces": {
##     "whiteboard": {"x":520,"y":195,"w":330,"h":205,"rot":0.0,  "lines":3, "align":"center"},
##     "wallchart":  {"x":950,"y":180,"w":175,"h":215,"rot":0.02, "lines":2},
##     "ledger":     {"x":690,"y":470,"w":150,"h":95, "rot":-0.06,"lines":2},
##     "sticky":     {"x":660,"y":90, "w":95, "h":95, "rot":0.04, "lines":2}
##   }
##
## RULES FOR PLACING THEM (for whoever annotates a scene):
##  - x,y is the TOP-LEFT of the writable face, not of the object. Exclude the
##    whiteboard's frame, the clipboard's clip, the sticky's curled corner.
##  - Inset by ~8% on every side so writing never touches the drawn edge.
##  - rot is radians, matched to how the object leans in the art. Read it off the
##    drawn edge; a surface that leans and text that does not is worse than neither.
##  - lines is how many lines of handwriting the face can hold at a readable size.
##    Two is the safe default; only a big whiteboard holds three.
##  - Never annotate a surface the art drew with writing already on it.
##
## Usage:
##   var s := SceneSurfaces.new()
##   s.mount("garage_steady_g")
##   add_child(s)
##   s.write("whiteboard", "PRODUCT", "v0.9")
##   s.write("ledger", "IN THE BANK", "$8,000")

const HAND := "res://assets/fonts/PatrickHand-Regular.ttf"
const INK := Color("1E1E1E")
const PEN := Color("E86A5C")

## THE FLOOR. Nothing under 24px on the 1536x1024 canvas — and room type is the
## text furthest from the reader in the whole game, the numbers you are meant to
## read from the couch, so it is the LAST text allowed to go small. It used to be
## the first: labels floored at 16 and values at 22, both under the floor, on every
## wall in every room.
##
## So the size stops at 24 and the VALUE gives way instead. A number too long for
## its face is rewritten shorter — "$1,240,000" becomes "$1.24M", a bag list gives
## its "+1 more" up to a "+1" welded onto the line above — and it is measured in
## BOTH directions, because the old fit test only ever asked about width and a face
## that declares three lines will happily be handed four.
const MIN_TYPE := 24

## Matched to Label.AUTOWRAP_WORD_SMART so a measurement here predicts the render
## exactly. BREAK_MANDATORY is the one that matters: without it the "\n" in a bag
## list measures as a single very long line and every fit answer is a lie.
const BRK := TextServer.BREAK_MANDATORY | TextServer.BREAK_WORD_BOUND \
		| TextServer.BREAK_ADAPTIVE | TextServer.BREAK_TRIM_EDGE_SPACES

var surfaces: Dictionary = {}      ## name -> rect/rot/lines from layout.json
var _font: Font
var _slots: Dictionary = {}        ## name -> Control the writing lives in

## Read a scene's declared surfaces. Returns false when the scene has none yet, so
## callers can fall back to their old plates rather than lose the information.
func mount(scene_id: String) -> bool:
	_font = load(HAND)
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	size = Vector2(1536, 1024)
	var path := "res://assets/scenes/%s/layout.json" % scene_id
	if not FileAccess.file_exists(path):
		return false
	var doc = JSON.parse_string(FileAccess.get_file_as_string(path))
	if not (doc is Dictionary):
		return false
	var ws = (doc as Dictionary).get("write_surfaces", {})
	if not (ws is Dictionary) or (ws as Dictionary).is_empty():
		return false
	surfaces = ws
	return true

## A face too small to hold floor-size handwriting is a face this scene DOES NOT HAVE.
## Callers already ask this before every write and keep their own plate when it says no
## (`_money_tag.visible = not s.has("ledger")`), so a 25x28 sticky in some library room
## costs the run its plate for one week instead of costing the number its legibility.
## The old answer was yes to everything, which is how 16px type ended up on a stamp.
## Mount from the BACKGROUND library's annotations instead of a legacy scene's
## layout.json. `facet_id` is slash-form ("scrappy_workspace/garage/day_steady_wide").
## This is what lets the assembled room, its surfaces and its cast swap as ONE unit —
## wiring the assembled scene while the surfaces still pointed at the old stage's
## coordinates put "$82,350" on a bare wall, which is how we learned.
const BG_ANNOTATIONS := "res://assets/backgrounds/annotations.json"
static var _bg_cache: Dictionary = {}

func mount_background(facet_id: String) -> bool:
	_font = load(HAND)
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	size = Vector2(1536, 1024)
	if _bg_cache.is_empty():
		if not FileAccess.file_exists(BG_ANNOTATIONS):
			return false
		var doc = JSON.parse_string(FileAccess.get_file_as_string(BG_ANNOTATIONS))
		if doc is Dictionary:
			_bg_cache = doc
	var entry = _bg_cache.get(facet_id, null)
	if entry == null:
		entry = _bg_cache.get(facet_id.replace("/", "__"), null)
	if not (entry is Dictionary):
		return false
	var ws = (entry as Dictionary).get("write_surfaces", {})
	if not (ws is Dictionary) or (ws as Dictionary).is_empty():
		return false
	surfaces = ws
	return true

func has(name: String) -> bool:
	if not surfaces.has(name):
		return false
	return _holds_type(surfaces[name])

func _holds_type(d: Dictionary) -> bool:
	if _font == null:
		return true
	return float(d.get("h", 0)) >= _font.get_height(MIN_TYPE) \
			and float(d.get("w", 0)) * 0.94 >= float(MIN_TYPE) * 1.6

## Write a label and its value onto a surface, in handwriting, at the surface's own
## lean. Sizes itself to fit the declared face — a value that does not fit is
## REWRITTEN SHORTER rather than shrunk, because nothing here may go under MIN_TYPE
## and nothing here may cross the drawn edge either.
func write(name: String, label: String, value: String, tint: Color = INK) -> void:
	if not has(name):
		return
	var d: Dictionary = surfaces[name]
	var rect := Rect2(float(d.get("x", 0)), float(d.get("y", 0)),
			float(d.get("w", 100)), float(d.get("h", 60)))
	var lines: int = int(d.get("lines", 2))
	var align := HORIZONTAL_ALIGNMENT_CENTER if String(d.get("align", "center")) == "center" \
			else HORIZONTAL_ALIGNMENT_LEFT

	var slot: Control = _slots.get(name)
	if slot == null:
		slot = Control.new()
		slot.mouse_filter = Control.MOUSE_FILTER_IGNORE
		slot.position = rect.position
		slot.pivot_offset = rect.size * 0.5
		slot.rotation = float(d.get("rot", 0.0))
		slot.set_deferred("size", rect.size)
		add_child(slot)
		_slots[name] = slot
	for c in slot.get_children():
		c.queue_free()

	# the label is small and quiet, the value is the thing you read from the couch —
	# and NEITHER goes under MIN_TYPE. The declared line count sets the ambition, the
	# floor sets the limit, and the ink stays 6% clear of every drawn edge.
	var per := rect.size.y / float(maxi(lines, 1))
	var inner := rect.size.x * 0.94
	var line_h := _font.get_height(MIN_TYPE)
	var top := per * 0.15
	# a face with room for ONE line of floor-size handwriting holds the VALUE. The label
	# is the quiet half; the number is the half you are meant to read from the couch.
	if rect.size.y < line_h * 2.0:
		label = ""
	if label != "":
		# a label may take everything except one floor-sized line, which is what the
		# value is owed no matter how the face was annotated
		var lf := _fit(label, Vector2(inner, maxf(rect.size.y - line_h, line_h)),
				int(clampf(per * 0.42, float(MIN_TYPE), 26.0)), align)
		var l := _mk(String(lf["text"]), int(lf["size"]), Color(tint, 0.62), align)
		l.position = Vector2.ZERO
		l.set_deferred("size", Vector2(rect.size.x, float(lf["h"])))
		slot.add_child(l)
		# the value starts under the label's INK, not under its line box. A small-caps
		# label has nothing in its descender band, and on a 127px board that band is
		# 8px — the difference between two items on the wall and one.
		top = float(lf["ink"]) + 3.0
	# THE TOP OFFSET GIVES WAY BEFORE THE TYPE DOES. On a face barely one line tall a
	# 5px breathing gap is the whole difference between a number and an ellipsis, so
	# the value is always owed a full floor-size line at the bottom of the face.
	top = clampf(top, 0.0, maxf(0.0, rect.size.y - line_h))
	var vf := _fit(value, Vector2(inner, rect.size.y - top),
			int(clampf(per * 0.78, float(MIN_TYPE), 54.0)), align)
	var v := _mk(String(vf["text"]), int(vf["size"]), tint, align)
	v.position = Vector2(0, top)
	v.set_deferred("size", Vector2(rect.size.x, rect.size.y - top))
	slot.add_child(v)

## THE FIT. The largest size at or under `want`, never under MIN_TYPE, at which the
## text wraps inside `box` in BOTH directions. When even the floor will not hold it,
## the size stops and the TEXT gives way: `_forms` hands back the next shorter honest
## way to say the same thing and the search starts again on that.
## Returns {text, size, h (rendered height), ink (height down to the last baseline)}.
func _fit(text: String, box: Vector2, want: int, align: int) -> Dictionary:
	for form in _forms(text):
		# ONE MEASURE DECIDES A RUNG. Text only grows with size, so a form that will
		# not fit at the floor cannot fit at any size, and the descending search is
		# only ever run on the rung that already fits — this whole path runs on every
		# room sync and must not shape a thousand strings to do it.
		if not _inside(_measure(form, box.x, MIN_TYPE, align), box):
			continue
		var sz: int = maxi(want, MIN_TYPE)
		var m := _measure(form, box.x, sz, align)
		while sz > MIN_TYPE and not _inside(m, box):
			sz = maxi(MIN_TYPE, sz - 2)
			m = _measure(form, box.x, sz, align)
		return _fitted(form, sz, m)
	# nothing on the ladder fit even at the floor: cut it and MARK the cut, because a
	# marked cut is honest and ink spilling onto the art is the defect this exists to stop
	var cut := _clamp_to_box(text, box, align)
	return _fitted(cut, MIN_TYPE, _measure(cut, box.x, MIN_TYPE, align))

func _inside(m: Vector2, box: Vector2) -> bool:
	return m.y <= box.y + 0.01 and m.x <= box.x + 0.01

## Shaping a string is not cheap and the room rewrites all six of its surfaces every
## time the state moves, with the same words most weeks. The font never changes on an
## instance, so the answers never go stale.
var _measured: Dictionary = {}

func _measure(text: String, w: float, sz: int, align: int) -> Vector2:
	var key := "%.2f|%d|%d|%s" % [w, sz, align, text]
	var hit = _measured.get(key)
	if hit is Vector2:
		return hit
	var m := _font.get_multiline_string_size(text, align, w, sz, -1, BRK)
	if _measured.size() > 512:
		_measured.clear()
	_measured[key] = m
	return m

func _fitted(text: String, sz: int, m: Vector2) -> Dictionary:
	var lh := maxf(_font.get_height(sz), 1.0)
	var n := maxf(1.0, roundf(m.y / lh))
	return {"text": text, "size": sz, "h": m.y, "ink": (n - 1.0) * lh + _font.get_ascent(sz)}

## EVERY HONEST WAY TO WRITE THIS VALUE, longest first. `_fit` walks down it and takes
## the first rung that fits, so the ladder runs cheapest loss first: a number gives up
## digits before a list gives up an item, and a list welds its counter onto the line
## above before it swallows anything.
##   "$1,240,000" → "$1.24M" → "$1.2M" → "$1M"
##   "Laptop / Savings Jar / Houseplant / +1 more"
##       → …/+1  → "Houseplant +1"  → "Laptop / Savings Jar / +2" → "Savings Jar +2" → …
func _forms(value: String) -> Array[String]:
	var out: Array[String] = [value]
	for m in _money_ladder(value):
		if not out.has(m):
			out.append(m)
	if out.size() > 1:
		return out          # money is not a list; its ladder is the whole story
	var parsed := _parse_list(value)
	var names: Array = parsed["names"]
	var extra: int = parsed["extra"]
	if names.size() <= 1 and extra == 0:
		return out
	while names.size() >= 1:
		if extra > 0:
			_add_form(out, _join(names) + "\n+%d" % extra)
			var welded := names.duplicate()
			welded[welded.size() - 1] = String(welded[welded.size() - 1]) + " +%d" % extra
			_add_form(out, _join(welded))
		else:
			_add_form(out, _join(names))
		if names.size() == 1:
			break
		names.remove_at(names.size() - 1)
		extra += 1
	return out

func _add_form(out: Array[String], form: String) -> void:
	if not out.has(form):
		out.append(form)

func _join(rows: Array) -> String:
	var s := ""
	for i in rows.size():
		s += ("\n" if i > 0 else "") + String(rows[i])
	return s

## A list value read for what it is: some named lines and, maybe, a count of the ones
## that did not make the board — whether that count sits on its own line ("+1 more")
## or has already been welded onto the last name ("Houseplant +1").
func _parse_list(value: String) -> Dictionary:
	var names: Array[String] = []
	for r in value.split("\n"):
		names.append(String(r))
	var extra := 0
	var tail := String(names[names.size() - 1]).strip_edges()
	if tail.ends_with(" more"):
		tail = tail.substr(0, tail.length() - 5).strip_edges()
	var at := tail.rfind("+")
	if at >= 0 and tail.substr(at + 1).is_valid_int():
		extra = tail.substr(at + 1).to_int()
		var head := tail.substr(0, at).strip_edges()
		if head == "":
			names.remove_at(names.size() - 1)
		else:
			names[names.size() - 1] = head
	return {"names": names, "extra": extra}

## "$1,240,000" → ["$1.24M", "$1.2M", "$1M"]. Empty for anything that is not money, and
## empty under $10,000 where the plain number is already short enough to read.
func _money_ladder(value: String) -> Array[String]:
	var t := value.strip_edges()
	var sign := ""
	# the draft screen writes "~$58,000" and the ledger writes "-$300"; both are money
	while t.begins_with("-") or t.begins_with("~"):
		sign += t.substr(0, 1)
		t = t.substr(1)
	if not t.begins_with("$"):
		return []
	var digits := t.substr(1).replace(",", "")
	if not digits.is_valid_int():
		return []
	var n := digits.to_int()
	var unit := ""
	var scale := 1.0
	if n >= 1000000000:
		unit = "B"
		scale = 1000000000.0
	elif n >= 1000000:
		unit = "M"
		scale = 1000000.0
	elif n >= 10000:
		unit = "K"
		scale = 1000.0
	else:
		return []
	var out: Array[String] = []
	for dp in [2, 1, 0]:
		var s := String.num(float(n) / scale, dp)
		if s.contains("."):
			s = s.rstrip("0").rstrip(".")
		var form := "%s$%s%s" % [sign, s, unit]
		if not out.has(form):
			out.append(form)
	return out

## LAST RESORT, and it should never be reached by any shipped value: trim at the floor
## size until the text is inside the face, and mark the trim.
func _clamp_to_box(text: String, box: Vector2, align: int) -> String:
	var t := text
	while t.length() > 1 and _measure(t, box.x, MIN_TYPE, align).y > box.y:
		t = t.substr(0, t.length() - 2).strip_edges() + "…"
	return t

## Draw a small tally/plot of N marks on a surface (customers on the wall chart,
## weeks survived scratched on a wall). Drawn, not typeset.
func tally(name: String, count: int, tint: Color = INK) -> void:
	if not surfaces.has(name):
		return
	var d: Dictionary = surfaces[name]
	var rect := Rect2(float(d.get("x", 0)), float(d.get("y", 0)),
			float(d.get("w", 100)), float(d.get("h", 60)))
	var slot: Control = _slots.get(name + "_tally")
	if slot == null:
		slot = Control.new()
		slot.mouse_filter = Control.MOUSE_FILTER_IGNORE
		slot.position = rect.position
		slot.pivot_offset = rect.size * 0.5
		slot.rotation = float(d.get("rot", 0.0))
		slot.set_deferred("size", rect.size)
		add_child(slot)
		_slots[name + "_tally"] = slot
	for c in slot.get_children():
		c.queue_free()
	var t := _Tally.new()
	t.count = count
	t.col = tint
	t.mouse_filter = Control.MOUSE_FILTER_IGNORE
	t.set_deferred("size", rect.size)
	slot.add_child(t)

func _mk(text: String, sz: int, col: Color, align: int) -> Label:
	var l := Label.new()
	# flags before size — setting size first locks the minimum and clips the text
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.horizontal_alignment = align
	l.vertical_alignment = VERTICAL_ALIGNMENT_TOP
	# Label adds 3px between lines by default and get_multiline_string_size does not,
	# so a four-line value renders 9px taller than it measured — which on a face with
	# 8px of slack is the whole difference between fitting and being sliced.
	l.add_theme_constant_override("line_spacing", 0)
	l.add_theme_font_override("font", _font)
	l.add_theme_font_size_override("font_size", sz)
	l.add_theme_color_override("font_color", col)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	l.text = text
	return l

## Chalk/pen tally marks, four upright and a fifth struck through.
class _Tally:
	extends Control
	var count := 0
	var col := Color("1E1E1E")
	func _draw() -> void:
		if count <= 0:
			return
		var per_row := 20
		var cw: float = size.x / float(per_row) * 0.9
		var ch: float = minf(size.y * 0.30, 26.0)
		var i := 0
		var shown: int = mini(count, 60)
		while i < shown:
			var g := i / 5
			var k := i % 5
			var gx: float = (g % 4) * (size.x / 4.0) + 6.0
			var gy: float = float(g / 4) * (ch + 14.0) + 6.0
			if gy + ch > size.y:
				break
			if k < 4:
				var x := gx + k * (cw * 0.22)
				draw_line(Vector2(x, gy), Vector2(x + 2.0, gy + ch), col, 3.0, true)
			else:
				draw_line(Vector2(gx - 3.0, gy + ch * 0.72),
						Vector2(gx + cw * 0.72, gy + ch * 0.18), col, 3.0, true)
			i += 1
