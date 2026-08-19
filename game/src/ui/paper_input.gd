class_name PaperInput
extends Control
## A PLACE TO WRITE, in the game's own language — owned by MAIN, used by any screen.
##
## The draft screens shipped white rounded rectangles on a dark panel: a web form
## laid over a drawn stage. The owner: "unable to add nice good looking textual
## input". Nothing about a white box belongs in this game.
##
## So this is a torn strip of the same paper the log book is made of, with a printed
## rule to write on and a pen caret. There is no box, no border radius, no fill
## behind the glyphs — the paper IS the field, exactly as in the journal, so a screen
## that asks you to write looks like a screen you write on.
##
## Usage:
##     var f := PaperInput.new()
##     f.setup("THE NAME", "Mossflow", 44)
##     f.position = Vector2(420, 300)
##     f.set_deferred("size", Vector2(600, 132))
##     add_child(f)
##     f.text_submitted.connect(func(t): ...)
##     print(f.value())

signal text_changed(text: String)
signal text_submitted(text: String)

const HAND := "res://assets/fonts/PatrickHand-Regular.ttf"
const INK := Color("1E1E1E")
const PEN := Color("E86A5C")
const CREAM := Color("F2EAD3")
const SAGE := Color("8FA582")

var _font: Font
var _label: Label
var _edit: LineEdit
var _paper: _Strip
var _focused := false

## `label` is the small lead-in above the line; pass "" for none. `size_px` is the
## handwriting size — 44 for a headline field like a company name, 34 for prose.
func setup(label: String, placeholder: String = "", size_px: int = 40) -> void:
	_font = load(HAND)
	mouse_filter = Control.MOUSE_FILTER_PASS

	_paper = _Strip.new()
	_paper.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_paper.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(_paper)

	var top := 6.0
	if label != "":
		_label = Label.new()
		_label.autowrap_mode = TextServer.AUTOWRAP_OFF
		_label.add_theme_font_override("font", _font)
		_label.add_theme_font_size_override("font_size", 24)
		_label.add_theme_color_override("font_color", Color(INK, 0.5))
		_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_label.text = label
		_label.position = Vector2(26, top)
		add_child(_label)
		top += 30.0

	_edit = LineEdit.new()
	# flags before size, always
	_edit.add_theme_font_override("font", _font)
	_edit.add_theme_font_size_override("font_size", size_px)
	_edit.add_theme_color_override("font_color", INK)
	_edit.add_theme_color_override("font_placeholder_color", Color(INK, 0.28))
	_edit.add_theme_color_override("caret_color", PEN)
	_edit.add_theme_color_override("selection_color", Color(PEN, 0.22))
	# every stylebox emptied: a filled box is the thing being removed
	for st in ["normal", "focus", "read_only"]:
		_edit.add_theme_stylebox_override(st, StyleBoxEmpty.new())
	_edit.placeholder_text = placeholder
	_edit.alignment = HORIZONTAL_ALIGNMENT_CENTER
	_edit.position = Vector2(26, top)
	_edit.set_deferred("size", Vector2(size.x - 52.0, size_px * 1.35))
	add_child(_edit)

	_edit.text_changed.connect(func(t: String) -> void: text_changed.emit(t))
	_edit.text_submitted.connect(func(t: String) -> void: text_submitted.emit(t))
	# the rule under the writing thickens on focus, the way a pen presses harder
	_edit.focus_entered.connect(func() -> void:
		_focused = true
		_paper.focused = true
		_paper.queue_redraw())
	_edit.focus_exited.connect(func() -> void:
		_focused = false
		_paper.focused = false
		_paper.queue_redraw())

	resized.connect(_relayout)
	call_deferred("_relayout")

func _relayout() -> void:
	if _edit == null:
		return
	var top: float = 6.0 + (30.0 if _label != null else 0.0)
	_edit.set_deferred("size", Vector2(maxf(size.x - 52.0, 40.0), size.y - top - 18.0))
	if _paper != null:
		_paper.rule_y = size.y - 22.0
		_paper.queue_redraw()

func value() -> String:
	return _edit.text.strip_edges() if _edit != null else ""

func set_value(t: String) -> void:
	if _edit != null:
		_edit.text = t

func grab_write_focus() -> void:
	if _edit != null:
		_edit.call_deferred("grab_focus")

## A torn strip of log-book paper with one printed rule to write along.
class _Strip:
	extends Control
	var focused := false
	var rule_y := 0.0
	func _draw() -> void:
		var w := size.x
		var h := size.y
		draw_rect(Rect2(5, 7, w, h), Color(0, 0, 0, 0.20))
		draw_rect(Rect2(0, 0, w, h), PaperInput.CREAM)
		# one continuous wobble around the perimeter — per-edge wobble spikes corners
		var rng := RandomNumberGenerator.new()
		rng.seed = 11
		var pts := PackedVector2Array()
		var c := [Vector2(3, 3), Vector2(w - 3, 3), Vector2(w - 3, h - 3), Vector2(3, h - 3)]
		for i in 4:
			var a: Vector2 = c[i]
			var b: Vector2 = c[(i + 1) % 4]
			var n := Vector2(b.y - a.y, a.x - b.x).normalized()
			for k in 14:
				pts.append(a.lerp(b, float(k) / 14.0) + n * rng.randf_range(-1.5, 1.5))
		pts.append(pts[0])
		draw_polyline(pts, PaperInput.INK, 3.0, true)
		# the rule you write along, pressed harder while the field has focus
		var y: float = rule_y if rule_y > 0.0 else h - 22.0
		var line := PackedVector2Array()
		var r2 := RandomNumberGenerator.new()
		r2.seed = 5
		for i in 41:
			line.append(Vector2(26.0 + (w - 52.0) * float(i) / 40.0, y + r2.randf_range(-1.2, 1.2)))
		draw_polyline(line, PaperInput.PEN if focused else Color(PaperInput.SAGE, 0.75),
				3.0 if focused else 2.0, true)
