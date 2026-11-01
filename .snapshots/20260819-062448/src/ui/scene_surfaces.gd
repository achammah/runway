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

func has(name: String) -> bool:
	return surfaces.has(name)

## Write a label and its value onto a surface, in handwriting, at the surface's own
## lean. Sizes itself to fit the declared face — a value that does not fit is shrunk
## rather than allowed to cross the drawn edge.
func write(name: String, label: String, value: String, tint: Color = INK) -> void:
	if not surfaces.has(name):
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

	# the label is small and quiet, the value is the thing you read from the couch
	var per := rect.size.y / float(max(lines, 1))
	var lab_sz := int(clampf(per * 0.42, 16.0, 26.0))
	var val_sz := int(clampf(per * 0.78, 22.0, 54.0))
	while val_sz > 22 and _font.get_string_size(value, align, -1, val_sz).x > rect.size.x * 0.94:
		val_sz -= 2

	if label != "":
		var l := _mk(label, lab_sz, Color(tint, 0.62), align)
		l.position = Vector2(0, 0)
		l.set_deferred("size", Vector2(rect.size.x, per * 0.6))
		slot.add_child(l)
	var v := _mk(value, val_sz, tint, align)
	v.position = Vector2(0, per * (0.5 if label != "" else 0.15))
	v.set_deferred("size", Vector2(rect.size.x, rect.size.y - per * 0.5))
	slot.add_child(v)

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
		var ch: float = min(size.y * 0.30, 26.0)
		var i := 0
		var shown: int = min(count, 60)
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
