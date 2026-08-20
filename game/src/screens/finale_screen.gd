class_name FinaleScreen
extends Control
## THE EXIT (LANE-WIRING) — the score ceremony when a run ends in money instead
## of ash: an acquisition signed, or the bell rung.
##
## Pillar 5 made literal: your score is YOUR equity. The hero number is the
## founder's slice, and the bonuses name WHY it is that big — kept the company,
## took no outside money, sold at the top.
##
## Safe zones: kicker plate 24,14 · hero centered · CTA center-bottom 560,930.

signal done

const PALETTE := {
	"cream": Color("F2EAD3"), "ink": Color("1E1E1E"), "coral": Color("E86A5C"),
	"yellow": Color("F4B942"), "sage": Color("8FA582"), "blue": Color("6E8CA0"),
}

var state: GameState
var kind := "acquisition"   # acquisition | ipo
var _font: Font
var _hand: Font
var _armed := false
var final_payout := 0
var bonuses: Array = []

func setup(p_state: GameState, p_kind: String = "acquisition") -> void:
	state = p_state
	kind = p_kind

## Score = founder% × exit value, then the style multipliers the dossier asks for.
func _score() -> void:
	var base := state.payout_today()
	var mult := 1.0
	bonuses.clear()
	if state.funding_id == "bootstrap" and state.rounds_raised.is_empty():
		mult *= 2.0
		bonuses.append(["NO OUTSIDE MONEY", "×2", PALETTE["sage"]])
	if state.founder_pct >= 50.0:
		mult *= 1.25
		bonuses.append(["STILL YOURS", "×1.25", PALETTE["sage"]])
	if state.hype >= 60:
		mult *= 1.15
		bonuses.append(["SOLD AT THE TOP", "×1.15", PALETTE["yellow"]])
	if state.cofounders.is_empty():
		mult *= 1.1
		bonuses.append(["SOLO THE WHOLE WAY", "×1.1", PALETTE["blue"]])
	if state.pivots >= 2:
		mult *= 1.1
		bonuses.append(["PIVOTED AND LIVED", "×1.1", PALETTE["coral"]])
	final_payout = int(base * mult)

func _ready() -> void:
	_font = load("res://assets/fonts/Baloo2-Bold.ttf")
	_hand = load("res://assets/fonts/PatrickHand-Regular.ttf")
	size = Vector2(1536, 1024)
	_score()

	_ceremony_room()

	var scrim := ColorRect.new()
	scrim.color = Color(0.05, 0.05, 0.07, 0.0)
	scrim.size = Vector2(1536, 1024)
	scrim.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(scrim)
	# short: the ceremony's settled state is the one worth looking at, so get there
	create_tween().tween_property(scrim, "color:a", 0.52, 0.3)

	# a trading floor or a boardroom is busy art either way; the ceremony block
	# carries its own contrast rather than dimming the whole room
	var band := _Band.new()
	band.position = Vector2(0, 120)
	band.size = Vector2(1536, 460)
	add_child(band)

	var plate := Panel.new()
	plate.position = Vector2(24, 14)
	plate.size = Vector2(430, 52)
	var pst := StyleBoxFlat.new()
	pst.bg_color = PALETTE["cream"]
	pst.border_color = PALETTE["ink"]
	pst.set_border_width_all(3)
	pst.set_corner_radius_all(10)
	plate.add_theme_stylebox_override("panel", pst)
	plate.rotation = -0.004
	add_child(plate)
	var kick := _lbl(plate, "WEEK %d · %s" % [state.week, "THE EXIT" if kind == "acquisition" else "THE BELL"], 28, PALETTE["ink"], _font)
	kick.position = Vector2(16, 6)

	var title := _lbl(self, "SOLD." if kind == "acquisition" else "YOU RANG THE BELL.", 76, PALETTE["yellow"], _font)
	title.size = Vector2(1536, 96)
	title.position = Vector2(0, 168)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.pivot_offset = Vector2(768, 48)
	title.scale = Vector2(1.6, 1.6)
	title.modulate.a = 0.0
	var tt := create_tween()
	tt.tween_interval(0.2)
	tt.tween_property(title, "modulate:a", 1.0, 0.1)
	tt.parallel().tween_property(title, "scale", Vector2.ONE, 0.2).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tt.tween_callback(func():
		var sfx := AudioStreamPlayer.new()
		sfx.stream = load("res://assets/sfx/win.wav")
		add_child(sfx)
		sfx.play()
		_confetti())

	# THE hero number — the founder's slice, counted up
	var hero := _lbl(self, "$0", 120, PALETTE["cream"], _font)
	hero.size = Vector2(1536, 150)
	hero.position = Vector2(0, 300)
	hero.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	var count := create_tween()
	count.tween_interval(0.4)
	count.tween_method(func(v: float): hero.text = "$" + _fmt(int(v)), 0.0, float(final_payout), 0.9).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)

	var caption := _lbl(self, "your slice of %s" % state.company_name, 32, PALETTE["cream"], _hand)
	_shadow(caption)
	caption.size = Vector2(1536, 42)
	caption.position = Vector2(0, 452)
	caption.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER

	var math_line := _lbl(self, "%.0f%% of the company  ×  $%s %s" % [
		state.founder_pct, _fmt(state.exit_value if state.exit_value > 0 else state.valuation()),
		"sale price" if kind == "acquisition" else "market cap"], 30, Color(PALETTE["cream"], 0.88), _hand)
	_shadow(math_line)
	math_line.size = Vector2(1536, 40)
	math_line.position = Vector2(0, 500)
	math_line.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER

	# style bonuses — the WHY behind the number
	if bonuses.is_empty():
		bonuses.append(["TOOK THE MONEY", "×1", Color(PALETTE["cream"], 0.7)])
	var rows := bonuses.size()
	# never silently drop a multiplier: the chips shrink to fit instead
	var chip_w := 330.0 if rows <= 4 else 280.0
	var gap := 22.0 if rows <= 4 else 16.0
	var total := rows * chip_w + (rows - 1) * gap
	var x0 := (1536.0 - total) / 2.0
	for i in rows:
		var b: Array = bonuses[i]
		var card := Panel.new()
		card.position = Vector2(x0 + i * (chip_w + gap), 600)
		card.size = Vector2(chip_w, 116)
		card.pivot_offset = Vector2(chip_w / 2.0, 58)
		var cst := StyleBoxFlat.new()
		cst.bg_color = Color(0.09, 0.09, 0.09, 0.9)
		cst.border_color = b[2]
		cst.set_border_width_all(3)
		cst.set_corner_radius_all(14)
		card.add_theme_stylebox_override("panel", cst)
		add_child(card)
		var nm := _lbl(card, String(b[0]), 24, b[2], _font)
		nm.size = Vector2(chip_w, 30)
		nm.position = Vector2(0, 16)
		nm.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		var mv := _lbl(card, String(b[1]), 40, PALETTE["cream"], _font)
		mv.size = Vector2(chip_w, 52)
		mv.position = Vector2(0, 54)
		mv.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		card.modulate.a = 0.0
		card.scale = Vector2(0.9, 0.9)
		var ct := create_tween()
		ct.tween_interval(0.8 + i * 0.1)
		ct.tween_property(card, "modulate:a", 1.0, 0.12)
		ct.parallel().tween_property(card, "scale", Vector2.ONE, 0.18).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)

	var cta := Button.new()
	cta.text = "THE LAST PAGE  →"
	cta.position = Vector2(560, 930)
	cta.size = Vector2(420, 76)
	cta.pivot_offset = Vector2(210, 38)
	cta.add_theme_font_override("font", _font)
	cta.add_theme_font_size_override("font_size", 32)
	cta.add_theme_color_override("font_color", PALETTE["ink"])
	var bs := StyleBoxFlat.new()
	bs.bg_color = Color.WHITE
	bs.border_color = PALETTE["ink"]
	bs.set_border_width_all(4)
	bs.set_corner_radius_all(14)
	cta.add_theme_stylebox_override("normal", bs)
	var bh := bs.duplicate()
	bh.bg_color = PALETTE["coral"]
	cta.add_theme_stylebox_override("hover", bh)
	cta.add_theme_stylebox_override("pressed", bh)
	cta.pressed.connect(_finish)
	add_child(cta)
	var pulse := create_tween().set_loops()
	pulse.tween_property(cta, "scale", Vector2(1.03, 1.03), 0.7).set_trans(Tween.TRANS_SINE)
	pulse.tween_property(cta, "scale", Vector2.ONE, 0.7).set_trans(Tween.TRANS_SINE)
	# the whole ceremony has settled by ~1.3s, so the guard against a stray keypress
	# skipping it does not need to outlast the animation it is guarding
	await get_tree().create_timer(1.4).timeout
	_armed = true

## THE ROOM THE ENDING ACTUALLY HAPPENED IN.
##
## An acquisition is a signature at a boardroom table. The bell, the ticker wall,
## the trading floor and the photographers belong to an IPO and to nothing else —
## showing a founder who was bought out ringing the opening bell reads as a bug on
## the one screen they are going to screenshot. So the exit kind picks the room.
##
## Every rung is checked against the disk before it is taken: a composed signing
## scene if the art lane has made one, the painted signing plate that ships today
## if not, and the hq room as the floor. A file that is missing degrades one rung
## instead of leaving the ceremony blank.
func _ceremony_room() -> void:
	var room := SceneRoom.new()
	room.size = Vector2(1536, 1024)
	if kind != "acquisition":
		add_child(room)
		room.load_scene("nasdaq_bell" if SceneRoomPicker.has_scene("nasdaq_bell") else "hq_steady")
		return
	for id in ["signing_room", "stage_signing", "signing_room_day"]:
		if SceneRoomPicker.has_scene(id):
			add_child(room)
			room.load_scene(id)
			return
	room.queue_free()
	for f in ["endings__signing_room__day_thriving_wide",
			"endings__signing_room__day_steady_wide",
			"endings__signing_room__night_thriving_wide",
			"endings__signing_room__night_steady_wide"]:
		var path := "res://assets/backgrounds/%s.png" % f
		if ResourceLoader.exists(path):
			var plate := TextureRect.new()
			plate.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
			plate.stretch_mode = TextureRect.STRETCH_SCALE
			plate.mouse_filter = Control.MOUSE_FILTER_IGNORE
			plate.texture = load(path)
			plate.size = Vector2(1536, 1024)
			plate.set_deferred("size", Vector2(1536, 1024))
			add_child(plate)
			return
	var fallback := SceneRoom.new()
	fallback.size = Vector2(1536, 1024)
	add_child(fallback)
	fallback.load_scene("hq_steady" if SceneRoomPicker.has_scene("hq_steady") else "stage_hq")

## Hand-drawn confetti: flat palette rectangles, no gradients.
func _confetti() -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = 42
	var cols := [PALETTE["coral"], PALETTE["yellow"], PALETTE["sage"], PALETTE["blue"], PALETTE["cream"]]
	for i in 46:
		var bit := ColorRect.new()
		bit.color = cols[i % cols.size()]
		var w := rng.randf_range(9.0, 20.0)
		bit.size = Vector2(w, w * rng.randf_range(0.4, 0.8))
		bit.position = Vector2(rng.randf_range(60, 1476), rng.randf_range(-260, -20))
		bit.rotation = rng.randf_range(-0.8, 0.8)
		bit.mouse_filter = Control.MOUSE_FILTER_IGNORE
		add_child(bit)
		var fall := rng.randf_range(2.2, 4.4)
		var tw := create_tween()
		tw.tween_interval(rng.randf_range(0.0, 1.1))
		tw.tween_property(bit, "position:y", 1120.0, fall).set_trans(Tween.TRANS_SINE)
		tw.parallel().tween_property(bit, "position:x", bit.position.x + rng.randf_range(-90, 90), fall)
		tw.parallel().tween_property(bit, "rotation", bit.rotation + rng.randf_range(-3.0, 3.0), fall)
		tw.tween_callback(bit.queue_free)

func _lbl(parent: Node, text: String, size: int, col: Color, font: Font) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_override("font", font)
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", col)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(l)
	return l

func _fmt(v: int) -> String:
	var t := str(absi(v))
	var out := ""
	while t.length() > 3:
		out = "," + t.substr(t.length() - 3) + out
		t = t.substr(0, t.length() - 3)
	return ("-" if v < 0 else "") + t + out

## Ink under a light word so it holds over any frame of the loop beneath it.
func _shadow(l: Label) -> void:
	l.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.75))
	l.add_theme_constant_override("shadow_offset_x", 2)
	l.add_theme_constant_override("shadow_offset_y", 3)
	l.add_theme_constant_override("shadow_outline_size", 6)

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


## A vertical wash that is darkest through the middle of the ceremony block and
## fades to nothing at both edges, so it never reads as a bar laid on the scene.
class _Band extends Control:
	func _ready() -> void:
		mouse_filter = Control.MOUSE_FILTER_IGNORE

	func _draw() -> void:
		var strips := 40
		var h := size.y / float(strips)
		for i in strips:
			var t := float(i) / float(strips - 1)
			var a := sin(t * PI) * 0.5
			draw_rect(Rect2(0, i * h, size.x, h + 1.0), Color(0.03, 0.03, 0.05, a), true)
