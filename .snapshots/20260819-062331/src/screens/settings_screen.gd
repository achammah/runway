class_name SettingsScreen
extends Control
## Pause / settings overlay (S19): volume, the Simulation Engine key status, and
## the promise the PRD makes — the game works fully without a key. Opened with
## ESC from anywhere; owned by MAIN (main.gd instantiates).

signal closed

const PALETTE := {
	"cream": Color("F2EAD3"), "ink": Color("1E1E1E"), "coral": Color("E86A5C"),
	"yellow": Color("F4B942"), "sage": Color("8FA582"), "blue": Color("6E8CA0"),
}

var llm_enabled := false
var llm_label := ""
var _font: Font
var _hand: Font

func setup(p_llm_enabled: bool, p_llm_label: String) -> void:
	llm_enabled = p_llm_enabled
	llm_label = p_llm_label

func _ready() -> void:
	_font = load("res://assets/fonts/Baloo2-Bold.ttf")
	_hand = load("res://assets/fonts/PatrickHand-Regular.ttf")
	set_anchors_preset(Control.PRESET_FULL_RECT)
	var dim := ColorRect.new()
	dim.color = Color(0.07, 0.08, 0.1, 0.78)
	dim.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(dim)
	dim.gui_input.connect(func(ev):
		if ev is InputEventMouseButton and ev.pressed:
			_close())

	var panel := Panel.new()
	panel.position = Vector2(468, 232)
	panel.size = Vector2(600, 560)
	panel.pivot_offset = Vector2(300, 280)
	var st := StyleBoxFlat.new()
	st.bg_color = Color(0.09, 0.09, 0.09, 0.97)
	st.border_color = PALETTE["cream"]
	st.set_border_width_all(4)
	st.set_corner_radius_all(18)
	panel.add_theme_stylebox_override("panel", st)
	add_child(panel)
	panel.scale = Vector2(0.92, 0.92)
	var tw := create_tween()
	tw.tween_property(panel, "scale", Vector2.ONE, 0.16).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)

	var title := _label(panel, "PAUSED", 46, PALETTE["yellow"], Vector2(40, 24))
	title.add_theme_font_override("font", _font)
	var rule := ColorRect.new()
	rule.color = PALETTE["coral"]
	rule.position = Vector2(44, 84)
	rule.size = Vector2(120, 5)
	panel.add_child(rule)

	# volume
	_label(panel, "MUSIC", 26, PALETTE["blue"], Vector2(44, 120)).add_theme_font_override("font", _font)
	var mus := HSlider.new()
	mus.min_value = -40.0
	mus.max_value = 0.0
	mus.value = AudioServer.get_bus_volume_db(0)
	mus.position = Vector2(180, 122)
	mus.size = Vector2(360, 30)
	mus.value_changed.connect(func(v): AudioServer.set_bus_volume_db(0, v))
	panel.add_child(mus)

	# simulation engine status
	_label(panel, "THE SIMULATION ENGINE", 26, PALETTE["blue"], Vector2(44, 190)).add_theme_font_override("font", _font)
	var status_text := ("● LIVE — %s\nEvents and free-written moves are adjudicated by the model." % llm_label) if llm_enabled else "○ OFF — no key found.\nThe game plays fully with its authored deck.\nAdd OPENAI_API_KEY to game/.env to turn the world on."
	var stl := _label(panel, status_text, 24, PALETTE["sage"] if llm_enabled else Color(PALETTE["cream"], 0.8), Vector2(44, 228))
	stl.add_theme_font_override("font", _hand)
	stl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	stl.custom_minimum_size = Vector2(512, 0)
	stl.set_deferred("size", Vector2(512, 0))

	# credits line
	var cr := _label(panel, "RUNWAY! — you don't build a startup. you survive one.", 22, Color(PALETTE["cream"], 0.55), Vector2(44, 370))
	cr.add_theme_font_override("font", _hand)
	cr.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	cr.custom_minimum_size = Vector2(512, 0)
	cr.set_deferred("size", Vector2(512, 0))

	var resume := Button.new()
	resume.text = "BACK TO THE GRIND  →"
	resume.position = Vector2(120, 452)
	resume.size = Vector2(360, 70)
	resume.add_theme_font_override("font", _font)
	resume.add_theme_font_size_override("font_size", 28)
	resume.add_theme_color_override("font_color", PALETTE["ink"])
	var bs := StyleBoxFlat.new()
	bs.bg_color = Color.WHITE
	bs.border_color = PALETTE["ink"]
	bs.set_border_width_all(4)
	bs.set_corner_radius_all(14)
	resume.add_theme_stylebox_override("normal", bs)
	var bh := bs.duplicate()
	bh.bg_color = PALETTE["coral"]
	resume.add_theme_stylebox_override("hover", bh)
	resume.add_theme_stylebox_override("pressed", bh)
	resume.pressed.connect(_close)
	panel.add_child(resume)

func _label(parent: Control, text: String, size: int, col: Color, pos: Vector2) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", col)
	l.position = pos
	parent.add_child(l)
	return l

func _close() -> void:
	closed.emit()
	queue_free()

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
		accept_event()
		_close()
