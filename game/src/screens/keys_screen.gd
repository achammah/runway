class_name KeysScreen
extends Control
## THE KEYS DESK (owner: "we need a screen to set it up"): where the player
## pastes the two API keys the living world runs on. Written to user://keys.env
## (never the project .env). Keyless play stays possible — the world just
## falls back to its authored deck.

signal saved

const CREAM := Color("F2EAD3")
const INK := Color("1E1E1E")
const PEN := Color("E86A5C")

var _openai: PaperInput
var _atlas: PaperInput
var _font: Font

func _ready() -> void:
	_font = load("res://assets/fonts/PatrickHand-Regular.ttf")
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	var bg := ColorRect.new()
	bg.color = Color("22262B")
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(bg)
	_line("THE WORLD NEEDS A NARRATOR", Vector2(430, 150), 52, CREAM)
	_line("two keys make the world alive. no keys = the authored deck only.", Vector2(434, 230), 27, Color(CREAM, 0.7))

	var env := DotEnv.load_env()
	_openai = PaperInput.new()
	_openai.setup("OPENAI API KEY — narration, judgment, and the weekly scene", "sk-…", 26)
	_openai.set_value(String(env.get("OPENAI_API_KEY", "")))
	_openai.position = Vector2(330, 320)
	_openai.set_deferred("size", Vector2(880, 110))
	add_child(_openai)

	_atlas = PaperInput.new()
	_atlas.setup("ATLAS CLOUD KEY (optional) — the room-image fallback ladder", "ac-…", 26)
	_atlas.set_value(String(env.get("ATLAS_CLOUD_API_KEY", env.get("ATLASCLOUD_API_KEY", ""))))
	_atlas.position = Vector2(330, 470)
	_atlas.set_deferred("size", Vector2(880, 110))
	add_child(_atlas)

	_line("keys are stored only on this machine (user data), never in the game folder.",
		Vector2(334, 620), 24, Color(CREAM, 0.55))

	var save := Button.new()
	save.flat = true
	save.text = "SAVE & PLAY  →"
	save.add_theme_font_override("font", _font)
	save.add_theme_font_size_override("font_size", 40)
	save.add_theme_color_override("font_color", PEN)
	for stn in ["normal", "hover", "pressed", "focus"]:
		save.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	save.position = Vector2(1060, 880)
	save.set_deferred("size", Vector2(380, 70))
	save.pressed.connect(_save)
	add_child(save)

	var skip := Button.new()
	skip.flat = true
	skip.text = "play keyless"
	skip.add_theme_font_override("font", _font)
	skip.add_theme_font_size_override("font_size", 28)
	skip.add_theme_color_override("font_color", Color(CREAM, 0.6))
	skip.add_theme_color_override("font_hover_color", PEN)
	for stn in ["normal", "hover", "pressed", "focus"]:
		skip.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	skip.position = Vector2(120, 892)
	skip.set_deferred("size", Vector2(240, 52))
	skip.pressed.connect(func() -> void: saved.emit())
	add_child(skip)

func _save() -> void:
	var lines := PackedStringArray()
	var ok := _openai.value().strip_edges()
	var ak := _atlas.value().strip_edges()
	if ok != "":
		lines.append("OPENAI_API_KEY=" + ok)
	if ak != "":
		lines.append("ATLAS_CLOUD_API_KEY=" + ak)
	var f := FileAccess.open("user://keys.env", FileAccess.WRITE)
	f.store_string("\n".join(lines) + "\n")
	f.close()
	saved.emit()

func _line(t: String, pos: Vector2, sz: int, col: Color) -> void:
	var l := Label.new()
	l.add_theme_font_override("font", _font)
	l.add_theme_font_size_override("font_size", sz)
	l.add_theme_color_override("font_color", col)
	l.position = pos
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(l)
