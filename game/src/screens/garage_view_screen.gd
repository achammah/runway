class_name GarageViewScreen
extends Control
## THE GARAGE VIEW — the 60 Seconds! principle: the room IS the save file.
## Everything you own is visibly IN the room. The money is a physical pile.
## Product progress is the whiteboard. Users are the wall chart. Your crew sits
## here, and their mood is drawn on them. The room itself decays with morale.
## Decisions happen in THE JOURNAL: a paper book with the week's event, choices
## gated by what you actually have, and a line to WRITE YOUR OWN MOVE — which
## the Simulation Engine adjudicates.

signal done(result: Dictionary)

const PALETTE := {
	"cream": Color("F2EAD3"), "ink": Color("1E1E1E"), "coral": Color("E86A5C"),
	"yellow": Color("F4B942"), "sage": Color("8FA582"), "blue": Color("6E8CA0"),
}
const GV := "res://assets/sprites/gv/"

var state: GameState
var content: ContentDb
var rng: SeededRng
var record: RunRecord
var generator: EventGenerator

var _font: Font
var _font_d: Font
var _room: Control
var _spots: Dictionary = {}       # key -> TextureRect (living room objects)
var _crew_nodes: Array = []
var _money_label: Label
var _users_label: Label
var _hud_label: Label
var _journal: Control
var _j_page: Control
var _page_body: Control            # every page element is a child of the tilted sheet
var _free_text: Dictionary = {}    # page index -> what the player has written there
var _week_sheet := 0               # 0 = what your move caused, 1 = what is left
## The capture harness in main.gd reaches for the old two-page frames; both now
## alias the single live page so it keeps driving the book without changes.
var _j_left: Control
var _j_right: Control
var _open_btn: Button
var _current_event: Dictionary = {}
var _week_log: Array[String] = []
var _last_cash := 0
var _adjudicating := false
var _sfx := {}
var _over := false
var _page_i := 0
var _pending_choice: Dictionary = {}      # chosen listed option (not yet applied)
var _pending_free: Dictionary = {}        # adjudicated free move (not yet applied)
var _pending_people: Dictionary = {}      # cf index -> "pay" | "shares" | "equip"
var _last_outcome: Dictionary = {}        # what last week's locked decision caused
var _departures: Array[String] = []       # people who left this week (book lines)
var _pending_work: Dictionary = {}        # dept -> {kind:"preset"/"free", id/text}
var _red_vignette: ColorRect
var _note_layer: Control
var _scene_mode := false
var _scene_layout: Dictionary = {}
var _scene_cuts: Dictionary = {}      # name -> TextureRect
var _scene_id := "garage"             # which painted room we are standing in
var _room_bg: TextureRect             # its base plate, swapped when the era turns
var _money_tag: Panel

# where each ownable thing lives in the room (position, height)
const FUN_FACTS := [
	"FUN FACT — Slack began as the chat tool inside a failed video game called Glitch.",
	"FUN FACT — Instagram pivoted from Burbn, a check-in app with too many features.",
	"FUN FACT — YouTube launched as a video dating site. Nobody dated.",
	"FUN FACT — Twitter came out of Odeo, a podcast platform made obsolete by Apple.",
	"FUN FACT — Shopify was a snowboard shop that liked its own checkout better than its boards.",
	"FUN FACT — Netflix mailed DVDs for a decade before the pivot that ate television.",
	"FUN FACT — Nintendo made playing cards for 80 years before video games.",
	"FUN FACT — Nokia started as a paper mill. Then rubber boots. Then phones.",
]

const WORK_DEPTS := ["PRODUCT", "MARKETING", "SALES"]
const DEPT_META := {
	"PRODUCT": {"icon": "itm_laptop", "why": "ships the thing customers pay for"},
	"MARKETING": {"icon": "gv/chart_1", "why": "makes strangers find out you exist"},
	"SALES": {"icon": "itm_savings_jar", "why": "turns interest into money"},
}
const WORK_PRESETS := {
	"PRODUCT": [
		{"id": "sprint", "label": "heads-down sprint", "note": "+product, tiring"},
		{"id": "polish", "label": "polish & bugfix", "note": "steady, keeps spirits up"},
	],
	"MARKETING": [
		{"id": "post_log", "label": "post the build log", "note": "+hype, people might notice"},
		{"id": "outreach", "label": "cold outreach ×50", "note": "grind for users"},
	],
	"SALES": [
		{"id": "demos", "label": "run demo calls", "note": "needs a product worth showing"},
		{"id": "chase", "label": "chase the invoices", "note": "money you are owed"},
	],
}


const ITEM_SPOTS := {
	"itm_laptop": [Vector2(390, 545), 110.0],
	"itm_dads_server": [Vector2(1040, 700), 210.0],
	"itm_houseplant": [Vector2(1252, 420), 110.0],
	"itm_guitar": [Vector2(120, 620), 240.0],
	"itm_savings_jar": [Vector2(1345, 430), 95.0],
	"itm_energy_drinks": [Vector2(545, 555), 85.0],
	"itm_paddle": [Vector2(742, 505), 62.0],
	"itm_hoodie": [Vector2(50, 560), 120.0],
	"itm_textbook": [Vector2(640, 575), 75.0],
	"itm_goodwill": [Vector2(455, 236), 72.0],
	"itm_dignity": [Vector2(1090, 250), 62.0],
	"itm_idea_napkin": [Vector2(668, 560), 58.0],
	"itm_roommate": [Vector2(250, 872), 110.0],
	"itm_bus_pass": [Vector2(600, 560), 0.0],
	"itm_gym_card": [Vector2(600, 560), 0.0],
}

func setup(p_state: GameState, p_content: ContentDb, p_rng: SeededRng, p_record: RunRecord, p_gen: EventGenerator) -> void:
	state = p_state
	content = p_content
	rng = p_rng
	record = p_record
	generator = p_gen

func _ready() -> void:
	_font = load("res://assets/fonts/PatrickHand-Regular.ttf")
	_font_d = load("res://assets/fonts/Baloo2-Bold.ttf")
	_last_cash = state.cash
	for n in ["card_flip", "cash", "death", "win", "tick", "deposit"]:
		var pl := AudioStreamPlayer.new()
		pl.stream = load("res://assets/sfx/%s.wav" % n)
		add_child(pl)
		_sfx[n] = pl
	set_anchors_preset(Control.PRESET_FULL_RECT)

	_room = Control.new()
	_room.set_anchors_preset(Control.PRESET_FULL_RECT)
	_room.size = Vector2(1536, 1024)
	add_child(_room)
	# SCENE-FIRST: one composed painting, decomposed into living layers.
	_scene_id = SceneRoomPicker.scene_id_for(state)
	_load_scene_layout()
	# flags → texture → deferred size, in that order (setting size first lets the
	# texture's minimum size win and the room renders inset with grey bands).
	var bg := TextureRect.new()
	bg.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	bg.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	bg.texture = _scene_base_tex()
	bg.set_deferred("size", Vector2(1536, 1024))
	_room.add_child(bg)
	_room_bg = bg
	if _scene_mode:
		for cut_name in _scene_cut_order():
			_scene_cut(String(cut_name))
		_scene_hover_notes()

	# living objects (classic path only builds sprites; scene mode reuses the tag/labels)
	if not _scene_mode:
		_spot("money", GV + "money_1.png", Vector2(70, 760), 180.0)
	var tag := Panel.new()
	tag.position = Vector2(268, 714) if _scene_mode else Vector2(64, 700)
	tag.size = Vector2(180, 48)
	var tst := StyleBoxFlat.new()
	tst.bg_color = PALETTE["cream"]
	tst.border_color = PALETTE["ink"]
	tst.set_border_width_all(3)
	tst.set_corner_radius_all(8)
	tag.add_theme_stylebox_override("panel", tst)
	tag.rotation = -0.02
	_room.add_child(tag)
	_money_tag = tag
	_money_label = _mk_dlabel("$0", 29, PALETTE["ink"])
	_money_label.position = Vector2(14, 4)
	tag.add_child(_money_label)
	if not _scene_mode:
		_spot("board", GV + "board_1.png", Vector2(200, 270), 210.0)
		_spot("chart", GV + "chart_1.png", Vector2(952, 300), 150.0)
	_users_label = _mk_dlabel("", 22, Color(PALETTE["ink"], 0.85))
	_users_label.position = Vector2(996, 412)
	_users_label.visible = false   # the chart tiers ARE the display; numbers live in the journal
	_room.add_child(_users_label)
	# cap-table paper: mini donut pinned to the wall (scene mode: pinned off the whiteboard frame)
	var cap_paper := Panel.new()
	cap_paper.position = Vector2(646, 88) if _scene_mode else Vector2(1164, 306)
	cap_paper.size = Vector2(118, 138)
	var st := StyleBoxFlat.new()
	st.bg_color = PALETTE["cream"]
	st.border_color = PALETTE["ink"]
	st.set_border_width_all(3)
	st.set_corner_radius_all(4)
	cap_paper.add_theme_stylebox_override("panel", st)
	cap_paper.rotation = 0.045
	_room.add_child(cap_paper)
	var cap_pin := Panel.new()
	cap_pin.position = Vector2(52, -5)
	cap_pin.size = Vector2(14, 14)
	var pin_st := StyleBoxFlat.new()
	pin_st.bg_color = PALETTE["yellow"]
	pin_st.border_color = PALETTE["ink"]
	pin_st.set_border_width_all(2)
	pin_st.set_corner_radius_all(7)
	cap_pin.add_theme_stylebox_override("panel", pin_st)
	cap_paper.add_child(cap_pin)
	var cap_lbl := _mk_label("%.0f%%\nyours" % state.founder_pct, 24, PALETTE["ink"])
	cap_lbl.position = Vector2(28, 34)
	cap_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	cap_paper.add_child(cap_lbl)

	# decay + badge spots (hidden until earned); scene mode repositions onto the painting
	if _scene_mode:
		_spot("decay_trash", GV + "decay_trash.png", Vector2(486, 800), 110.0, false)
		_spot("decay_flies", GV + "decay_flies.png", Vector2(1030, 640), 74.0, false)
		_spot("decay_graffiti", GV + "decay_graffiti.png", Vector2(284, 132), 110.0, false)
		_spot("badge_camp", GV + "badge_camp.png", Vector2(742, 104), 100.0, false)
		_spot("badge_launched", GV + "badge_launched.png", Vector2(852, 88), 118.0, false)
	else:
		_spot("decay_trash", GV + "decay_trash.png", Vector2(320, 850), 120.0, false)
		_spot("decay_pizza", GV + "decay_pizza.png", Vector2(430, 830), 140.0, false)
		_spot("decay_flies", GV + "decay_flies.png", Vector2(700, 560), 80.0, false)
		_spot("decay_graffiti", GV + "decay_graffiti.png", Vector2(1000, 490), 140.0, false)
		_spot("badge_camp", GV + "badge_camp.png", Vector2(520, 250), 110.0, false)
		_spot("badge_launched", GV + "badge_launched.png", Vector2(640, 240), 130.0, false)
		# items in the room (classic only — the composed painting already contains its props)
		for id in ITEM_SPOTS:
			var spec: Array = ITEM_SPOTS[id]
			if float(spec[1]) <= 0.0:
				continue
			_spot("item_" + id, "res://assets/sprites/%s.png" % id, spec[0], float(spec[1]), false)

	# the crew, present and breathing
	_build_crew()

	# HUD
	var hud_plate := Panel.new()
	hud_plate.position = Vector2(24, 14)
	hud_plate.size = Vector2(430, 52)
	var hst := StyleBoxFlat.new()
	hst.bg_color = PALETTE["cream"]
	hst.border_color = PALETTE["ink"]
	hst.set_border_width_all(3)
	hst.set_corner_radius_all(10)
	hud_plate.add_theme_stylebox_override("panel", hst)
	hud_plate.rotation = -0.004
	add_child(hud_plate)
	_hud_label = _mk_dlabel("", 29, PALETTE["ink"])
	_hud_label.position = Vector2(16, 6)
	hud_plate.add_child(_hud_label)
	_open_btn = Button.new()
	_open_btn.text = "OPEN THE JOURNAL"
	_open_btn.position = Vector2(560, 930)
	_open_btn.size = Vector2(420, 76)
	_open_btn.pivot_offset = Vector2(210, 38)
	_style_button(_open_btn, PALETTE["yellow"], 30)
	_open_btn.pressed.connect(_open_journal)
	add_child(_open_btn)

	_red_vignette = ColorRect.new()
	_red_vignette.color = Color(0.85, 0.3, 0.25, 0.0)
	_red_vignette.set_anchors_preset(Control.PRESET_FULL_RECT)
	_red_vignette.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_red_vignette)
	_note_layer = Control.new()
	_note_layer.set_anchors_preset(Control.PRESET_FULL_RECT)
	_note_layer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_note_layer)
	_journal = Control.new()
	_journal.set_anchors_preset(Control.PRESET_FULL_RECT)
	_journal.visible = false
	add_child(_journal)

	_sync_room(true)
	_start_week()

func _process(_delta: float) -> void:
	var t := Time.get_ticks_msec() / 1000.0
	if _open_btn and _open_btn.visible:
		var p := 1.0 + sin(t * 3.2) * 0.03
		_open_btn.scale = Vector2(p, p)
	# the room itself panics about money: pulsing red edges when starving
	if _red_vignette:
		if state.weeks_in_red > 0:
			_red_vignette.color.a = 0.10 + 0.08 * sin(t * 2.4)
		elif state.cash < state.burn_per_week() * 2:
			_red_vignette.color.a = 0.05 + 0.03 * sin(t * 1.6)
		else:
			_red_vignette.color.a = 0.0

# ---------- room construction helpers ----------

func _pos(l: Label, p: Vector2) -> Label:
	l.position = p
	return l

func _mk_dlabel(text: String, size: int, col: Color) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_override("font", _font_d)
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", col)
	return l

func _mk_label(text: String, size: int, col: Color) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_override("font", _font)
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", col)
	return l

func _style_button(b: Button, col: Color, fsize: int) -> void:
	b.add_theme_font_override("font", _font_d)
	b.add_theme_font_size_override("font_size", fsize)
	b.add_theme_color_override("font_color", PALETTE["ink"])
	b.add_theme_color_override("font_disabled_color", Color(PALETTE["ink"], 0.4))
	var st := StyleBoxFlat.new()
	st.bg_color = Color.WHITE
	st.border_color = PALETTE["ink"]
	st.set_border_width_all(4)
	st.set_corner_radius_all(14)
	st.content_margin_left = 14
	st.content_margin_right = 14
	b.add_theme_stylebox_override("normal", st)
	var sh := st.duplicate()
	sh.bg_color = col
	b.add_theme_stylebox_override("hover", sh)
	b.add_theme_stylebox_override("pressed", sh)

func _spot(key: String, path: String, pos: Vector2, height: float, visible_now: bool = true) -> void:
	var tr := TextureRect.new()
	tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	tr.position = pos
	var sz := Vector2(height, height)
	if ResourceLoader.exists(path):
		var tex: Texture2D = load(path)
		tr.texture = tex
		sz = Vector2(height * tex.get_width() / tex.get_height(), height)
	tr.size = sz
	tr.set_deferred("size", sz)
	tr.visible = visible_now
	_room.add_child(tr)
	_spots[key] = tr
	if key.begins_with("item_"):
		var item_id := key.substr(5)
		tr.mouse_filter = Control.MOUSE_FILTER_STOP
		tr.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		tr.gui_input.connect(func(ev):
			if ev is InputEventMouseButton and ev.pressed:
				_item_note(item_id, tr))
		tr.mouse_entered.connect(func():
			var t := create_tween()
			t.tween_property(tr, "scale", Vector2(1.06, 1.06), 0.08))
		tr.mouse_exited.connect(func():
			var t := create_tween()
			t.tween_property(tr, "scale", Vector2.ONE, 0.1))

## Spots differ between the layered scene and the sprite fallback, and the era
## swap can move us between them after registration — so never assume a key.
func _show_spot(key: String, vis: bool) -> void:
	if _spots.has(key):
		var tr: TextureRect = _spots[key]
		if is_instance_valid(tr):
			tr.visible = vis

func _set_spot_tex(key: String, path: String) -> void:
	var tr: TextureRect = _spots.get(key)
	if tr and ResourceLoader.exists(path):
		var tex: Texture2D = load(path)
		var h := tr.size.y
		tr.texture = tex
		tr.set_deferred("size", Vector2(h * tex.get_width() / tex.get_height(), h))

## T6 — click a thing, get the paper note saying what it's for.
func _item_note(item_id: String, anchor: Control) -> void:
	for c in _note_layer.get_children():
		c.queue_free()
	var def: Dictionary = content.items.get(item_id, {})
	var note := Panel.new()
	var st := StyleBoxFlat.new()
	st.bg_color = PALETTE["cream"]
	st.border_color = PALETTE["ink"]
	st.set_border_width_all(3)
	st.set_corner_radius_all(6)
	st.shadow_color = Color(0, 0, 0, 0.25)
	st.shadow_size = 6
	note.add_theme_stylebox_override("panel", st)
	var blurb := String(def.get("blurb", ""))
	var blurb_h := _font.get_multiline_string_size(blurb, HORIZONTAL_ALIGNMENT_LEFT, 272.0, 24).y
	var note_h := 46.0 + blurb_h + 14.0
	note.custom_minimum_size = Vector2(300, note_h)
	note.set_deferred("size", Vector2(300, note_h))
	var pos := anchor.position + Vector2(anchor.size.x * 0.5 - 150, -note_h - 10.0)
	note.position = Vector2(clampf(pos.x, 12, 1224), maxf(12, pos.y))
	note.rotation = 0.015
	_note_layer.add_child(note)
	var nm := _mk_label(String(def.get("name", item_id)), 24, PALETTE["ink"])
	nm.position = Vector2(14, 6)
	note.add_child(nm)
	var bl := _mk_label(blurb, 24, Color(PALETTE["ink"], 0.8))
	bl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	bl.custom_minimum_size = Vector2(272, 0)
	bl.position = Vector2(14, 40)
	bl.set_deferred("size", Vector2(272, blurb_h))
	note.add_child(bl)
	_sfx["tick"].play()
	note.modulate.a = 0.0
	note.pivot_offset = Vector2(150, note_h)
	var tw := create_tween()
	tw.tween_property(note, "modulate:a", 1.0, 0.12)
	tw.parallel().tween_property(note, "scale", Vector2.ONE, 0.14).from(Vector2(0.9, 0.9)).set_trans(Tween.TRANS_BACK)
	tw.tween_interval(2.6)
	tw.tween_property(note, "modulate:a", 0.0, 0.35)
	tw.tween_callback(note.queue_free)

## Every era paints its own layer names, so the order is derived, not hardcoded:
## the garage's tuned back→front order first, then anything else biggest-first
## (a bigger cutout sits further back). `room_dup` is a copy of the whole room.
const GARAGE_ORDER := ["whiteboard", "chart", "crew_headphones", "crew_vest", "founder", "money", "pizza", "mug"]

func _scene_cut_order() -> Array:
	var ordered: Array = []
	for k in GARAGE_ORDER:
		if _scene_layout.has(k):
			ordered.append(k)
	var rest: Array = []
	for k in _scene_layout.keys():
		if not ordered.has(k) and String(k) != "room_dup":
			rest.append(k)
	rest.sort_custom(func(x, y):
		var sx: Dictionary = _scene_layout.get(x, {})
		var sy: Dictionary = _scene_layout.get(y, {})
		return float(sx.get("w", 0)) * float(sx.get("h", 0)) > float(sy.get("w", 0)) * float(sy.get("h", 0)))
	ordered.append_array(rest)
	return ordered

func _scene_base_tex() -> Texture2D:
	for cand in ["res://assets/scenes/%s/room_bg.png" % _scene_id,
			"res://assets/scenes/%s/scene.png" % _scene_id,
			"res://assets/scenes/garage/room_bg.png", "res://assets/env/garage.png"]:
		if ResourceLoader.exists(cand):
			return load(cand)
	return null

## Layers only come with the scenes that shipped a layout; the rest render as a
## single painted plate and fall back to the sprite path for state objects.
func _load_scene_layout() -> void:
	_scene_layout = {}
	_scene_mode = false
	var lp := "res://assets/scenes/%s/layout.json" % _scene_id
	if not FileAccess.file_exists(lp):
		return
	if not (ResourceLoader.exists("res://assets/scenes/%s/room_bg.png" % _scene_id) or ResourceLoader.exists("res://assets/scenes/%s/scene.png" % _scene_id)):
		return
	var parsed = JSON.parse_string(FileAccess.get_file_as_string(lp))
	if parsed is Dictionary and not parsed.is_empty():
		_scene_layout = parsed
		_scene_mode = true

func _scene_cut(cut_name: String) -> void:
	var spec: Dictionary = _scene_layout.get(cut_name, {})
	var path := "res://assets/scenes/%s/%s.png" % [_scene_id, cut_name]
	if spec.is_empty() or not ResourceLoader.exists(path):
		return
	var tr := TextureRect.new()
	tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	tr.texture = load(path)
	tr.position = Vector2(float(spec.get("x", 0)), float(spec.get("y", 0)))
	var sz := Vector2(float(spec.get("w", 64)), float(spec.get("h", 64)))
	tr.size = sz
	tr.set_deferred("size", sz)
	tr.pivot_offset = Vector2(sz.x / 2.0, sz.y)   # feet-anchored for bobs/scales
	_room.add_child(tr)
	_scene_cuts[cut_name] = tr

const SCENE_NOTES := {
	"money": ["The Runway", "This is not a metaphor. It is the money. Watch it."],
	"whiteboard": ["The Plan", "Product progress lives here. It fills as you ship."],
	"chart": ["User Growth", "Pinned where everyone can see it. No pressure."],
	"founder": ["You", "Morale in the shoulders. Watch the cowlick."],
	"crew_headphones": ["The Builder", "Loyalty drains weekly. Gestures refill it."],
	"crew_vest": ["The Talker", "Currently pointing at the chart. Someone has to."],
	"pizza": ["Dinner, Historically", "Appears when morale slips. It never lies."],
	"mug": ["Coffee", "The real fuel. The jar was a decoy."],
}

func _scene_hover_notes() -> void:
	for key in _scene_cuts:
		var tr: TextureRect = _scene_cuts[key]
		tr.mouse_filter = Control.MOUSE_FILTER_STOP
		tr.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		var k: String = key
		tr.mouse_entered.connect(func():
			var t := create_tween()
			t.tween_property(tr, "scale", Vector2(1.03, 1.03), 0.08))
		tr.mouse_exited.connect(func():
			var t := create_tween()
			t.tween_property(tr, "scale", Vector2.ONE, 0.1))
		tr.gui_input.connect(func(ev):
			if ev is InputEventMouseButton and ev.pressed:
				_scene_note(k, tr))

func _scene_note(key: String, anchor: Control) -> void:
	var spec: Array = SCENE_NOTES.get(key, ["?", ""])
	for c in _note_layer.get_children():
		c.queue_free()
	var note := Panel.new()
	var st2 := StyleBoxFlat.new()
	st2.bg_color = PALETTE["cream"]
	st2.border_color = PALETTE["ink"]
	st2.set_border_width_all(3)
	st2.set_corner_radius_all(6)
	st2.shadow_color = Color(0, 0, 0, 0.25)
	st2.shadow_size = 6
	note.add_theme_stylebox_override("panel", st2)
	note.size = Vector2(300, 104)
	var pos := anchor.position + Vector2(anchor.size.x * 0.5 - 150, -114)
	note.position = Vector2(clampf(pos.x, 12, 1224), maxf(12, pos.y))
	note.rotation = 0.015
	_note_layer.add_child(note)
	var nm := _mk_dlabel(String(spec[0]), 25, PALETTE["ink"])
	nm.position = Vector2(14, 6)
	note.add_child(nm)
	var bl := _mk_label(String(spec[1]), 22, Color(PALETTE["ink"], 0.8))
	bl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	bl.custom_minimum_size = Vector2(272, 0)
	bl.position = Vector2(14, 40)
	note.add_child(bl)
	bl.set_deferred("size", Vector2(272, 0))
	_sfx["tick"].play()
	note.modulate.a = 0.0
	note.pivot_offset = Vector2(150, 104)
	var tw := create_tween()
	tw.tween_property(note, "modulate:a", 1.0, 0.12)
	tw.parallel().tween_property(note, "scale", Vector2.ONE, 0.14).from(Vector2(0.9, 0.9)).set_trans(Tween.TRANS_BACK)
	tw.tween_interval(2.6)
	tw.tween_property(note, "modulate:a", 0.0, 0.35)
	tw.tween_callback(note.queue_free)

## Scene-mode living state: mood/droop on the painted crew, money pile scale.
func _refresh_scene_crew() -> void:
	var low := state.morale <= 35
	var happy := state.morale >= 70
	for key in ["founder", "crew_headphones", "crew_vest"]:
		var tr: TextureRect = _scene_cuts.get(key)
		if tr == null:
			continue
		var droop := 0.0
		var tint := Color(1, 1, 1, 1)
		if low:
			droop = 0.04
			tint = Color(0.92, 0.92, 0.95, 1)
		elif happy:
			droop = -0.01
		var tw := create_tween()
		tw.tween_property(tr, "rotation", droop, 0.5)
		tr.modulate = tint
	var cf_n := state.cofounders.size()
	if _scene_cuts.has("crew_headphones"):
		_scene_cuts["crew_headphones"].visible = cf_n >= 1
	if _scene_cuts.has("crew_vest"):
		_scene_cuts["crew_vest"].visible = cf_n >= 2

func _build_crew() -> void:
	if _scene_mode:
		_refresh_scene_crew()
		return
	for n in _crew_nodes:
		if is_instance_valid(n):
			n.queue_free()
	_crew_nodes.clear()
	var xs := [612, 800, 964, 1120, 470]
	# the founder
	var f := TextureRect.new()
	var fp := "res://assets/sprites/%s.png" % ("chr_arch_" + state.archetype_id)
	if ResourceLoader.exists(fp):
		f.texture = load(fp)
	f.size = Vector2(190, 190)
	f.position = Vector2(xs[0], 628)
	f.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	f.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	f.pivot_offset = Vector2(95, 190)
	_room.add_child(f)
	_crew_nodes.append(f)
	_idle_bob(f, 0.0)
	# cofounders with visible mood
	# SALES / BUSINESS / TECH / HUSTLER, tolerant of the older role spellings.
	var slugs := {"tech": "technical", "technical": "technical", "business": "business",
		"sales": "business", "hustler": "idea", "the idea friend": "idea", "design": "design"}
	for i in state.cofounders.size():
		var cf: Dictionary = state.cofounders[i]
		var mood := "neutral"
		if state.morale >= 70:
			mood = "happy"
		elif state.morale <= 35 or state.has_flag("trap_underpaid_cofounder"):
			mood = "resentful"
		var c := TextureRect.new()
		var cp := "res://assets/sprites/cf_%s_%s.png" % [slugs.get(String(cf.get("role", "Technical")), "technical"), mood]
		if ResourceLoader.exists(cp):
			c.texture = load(cp)
		c.size = Vector2(170, 170)
		c.position = Vector2(xs[(i + 1) % xs.size()], 648)
		c.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		c.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		c.pivot_offset = Vector2(85, 170)
		_room.add_child(c)
		_crew_nodes.append(c)
		_idle_bob(c, 0.4 + i * 0.3)

func _idle_bob(node: Control, phase: float) -> void:
	var tw := create_tween().set_loops()
	tw.tween_interval(phase)
	tw.tween_property(node, "scale", Vector2(1.005, 0.985), 1.1).set_trans(Tween.TRANS_SINE)
	tw.tween_property(node, "scale", Vector2.ONE, 1.1).set_trans(Tween.TRANS_SINE)

## The room reflects the state. Called after every change.
## Called on every state change — this is where the room stops being the garage.
func _refresh_scene() -> void:
	if state == null or _room_bg == null:
		return
	var want := SceneRoomPicker.scene_id_for(state)
	if want == _scene_id:
		return
	_scene_id = want
	_load_scene_layout()
	for key in _scene_cuts:
		var old: TextureRect = _scene_cuts[key]
		if is_instance_valid(old):
			old.queue_free()
	_scene_cuts.clear()
	_room_bg.texture = _scene_base_tex()
	if _scene_mode:
		for cut_name in _scene_cut_order():
			_scene_cut(String(cut_name))
	_refresh_scene_crew()

func _sync_room(instant: bool = false) -> void:
	_refresh_scene()
	_hud_label.text = "%s  ·  WEEK %d" % [state.company_name.to_upper(), state.week]
	# money pile grows/shrinks
	var mtier := 1
	if state.cash > 30000: mtier = 4
	elif state.cash > 12000: mtier = 3
	elif state.cash > 3000: mtier = 2
	if _scene_mode:
		var mc: TextureRect = _scene_cuts.get("money")
		if mc:
			var msc: float = [0.7, 0.85, 1.0, 1.14][mtier - 1]
			var mt := create_tween()
			mt.tween_property(mc, "scale", Vector2(msc, msc), 0.4).set_trans(Tween.TRANS_BACK)
	else:
		_set_spot_tex("money", GV + "money_%d.png" % mtier)
	_money_label.text = "$%s" % _fmt(state.cash)
	if state.cash < state.burn_per_week() * 2:
		_money_label.add_theme_color_override("font_color", PALETTE["coral"])
	if not instant and state.cash != _last_cash:
		_sfx["cash"].play()
		var ml := _money_label
		ml.add_theme_color_override("font_color", PALETTE["sage"] if state.cash > _last_cash else PALETTE["coral"])
		var t := create_tween()
		t.tween_interval(0.9)
		t.tween_callback(func(): ml.add_theme_color_override("font_color", PALETTE["ink"]))
	_last_cash = state.cash
	if not _scene_mode:
		# whiteboard = product
		_set_spot_tex("board", GV + "board_%d.png" % clampi(1 + state.product / 26, 1, 4))
		# wall chart = traction
		var ctier := 1
		if state.traction > 60: ctier = 4
		elif state.traction > 20: ctier = 3
		elif state.traction > 4: ctier = 2
		_set_spot_tex("chart", GV + "chart_%d.png" % ctier)
		_users_label.text = "%d user%s" % [state.traction, "" if state.traction == 1 else "s"]
		# items appear when owned
		for id in ITEM_SPOTS:
			var tr: TextureRect = _spots.get("item_" + id)
			if tr:
				tr.visible = state.has_item(id)
	# decay tracks morale; badges track flags
	if _scene_mode:
		var pz: TextureRect = _scene_cuts.get("pizza")
		if pz:
			pz.visible = state.morale < 45
	else:
		_show_spot("decay_pizza", state.morale < 30)
	_show_spot("decay_trash", state.morale < 45)
	_show_spot("decay_flies", state.morale < 22)
	_show_spot("decay_graffiti", state.morale < 15)
	_show_spot("badge_camp", state.has_flag("camp_alum"))
	_show_spot("badge_launched", state.has_flag("first_user"))
	# crew moods refresh
	_build_crew()

func _fmt(v: int) -> String:
	var t := str(absi(v))
	var out := ""
	while t.length() > 3:
		out = "," + t.substr(t.length() - 3) + out
		t = t.substr(0, t.length() - 3)
	return ("-" if v < 0 else "") + t + out

# ---------- weekly loop ----------

func _start_week() -> void:
	state.week += 1
	_week_log.clear()
	_departures.clear()
	_pending_choice = {}
	_pending_free = {}
	_pending_people = {}
	_page_i = 0
	# the people consumable: loyalty drains every week; empty = they walk
	for cf in state.cofounders.duplicate():
		if not cf.has("loyalty"):
			cf["loyalty"] = 70
		cf["loyalty"] = int(cf["loyalty"]) - 6
		if int(cf["loyalty"]) <= 0:
			state.cofounders.erase(cf)
			var who := "%s cofounder" % String(cf.get("role", "?"))
			if bool(cf.get("vesting", true)):
				state.founder_pct += float(cf.get("equity_diluted", cf.get("equity", 0))) * 0.75
				_departures.append("%s walked. The cliff clawed back most of their shares." % who)
			else:
				_departures.append("%s walked — WITH every share. No vesting. The classic." % who)
			state.morale = maxi(0, state.morale - 10)
			_week_log.append("%s left: −10 morale" % who)
			record.log_event(state.week, {"id": "departure", "title": "%s quit" % who}, "loyalty ran dry", [])
	var burn := state.burn_per_week()
	state.cash -= burn
	_week_log.append("rent + ramen%s: −$%s" % ["" if state.employees.is_empty() else " + payroll", _fmt(burn)])
	if state.has_method("weekly_staff_tick"):
		for sline in state.weekly_staff_tick():
			_week_log.append(String(sline))
	if state.cash < 0 and state.employees.size() > 0 and state.has_method("note_missed_payroll"):
		state.note_missed_payroll()
		_week_log.append("payroll missed. they noticed.")
	var passive_build := 2 + int(state.competences.get("build", 3))
	if state.has_item("itm_laptop"):
		passive_build += 2
	if state.has_item("itm_dads_server"):
		passive_build += 1
	state.product = clampi(state.product + passive_build, 0, 100)
	_week_log.append("shipped: +%d product" % passive_build)
	if state.product >= 40:
		var gained := 1 + int(int(state.competences.get("sell", 3)) / 2.0) + int(state.hype / 20.0)
		state.traction += gained
		_week_log.append("new users: +%d" % gained)
	var grit_heal := int(int(state.competences.get("grit", 3)) / 3.0)
	state.morale += grit_heal
	if state.has_item("itm_houseplant"):
		state.morale += 1
		_week_log.append("the plant listened: +1 morale")
	if state.structure_id == "solo":
		state.morale -= 1
		_week_log.append("the 2am dread, alone: −1 morale")
	# T12 — money IS the food: you starve over weeks, not instantly
	if state.cash < 0:
		state.weeks_in_red += 1
		state.morale = maxi(0, state.morale - 6)
		_week_log.append("IN THE RED — week %d of 3. Payroll is a promise now." % state.weeks_in_red)
		for cf in state.cofounders:
			cf["loyalty"] = int(cf.get("loyalty", 70)) - 10
		if state.weeks_in_red >= 3:
			_die("Ramen Zero — three weeks without money. The runway ended, week %d." % state.week)
			return
	else:
		if state.weeks_in_red > 0:
			_week_log.append("back in the black. everyone exhales.")
		state.weeks_in_red = 0
	state.clampi_meters()
	if state.has_method("demote") and (state.has_flag("payroll_crisis") or state.has_flag("down_round")):
		var dwhy := "missed payroll twice" if state.has_flag("payroll_crisis") else "down round"
		var dres: Dictionary = state.demote(dwhy)
		state.flags.erase("down_round")
		if bool(dres.get("changed", false)):
			_departures.append("MOVED DOWN — %s. The boxes barely fit the shame." % dwhy)
	if state.has_method("advance_era_if_ready"):
		var up: Dictionary = state.advance_era_if_ready()
		if bool(up.get("changed", false)):
			_departures.append("MOVED UP — %s → %s. Bigger room, bigger rent." % [String(up.get("from", "")), String(up.get("to", ""))])
			_sfx["win"].play()
	for tb in state.timebombs.duplicate():
		tb["weeks_left"] -= 1
		if tb["weeks_left"] <= 0:
			state.timebombs.erase(tb)
			var bomb: Dictionary = content.events.get(tb["event"], {})
			if not bomb.is_empty():
				_current_event = bomb
				_after_week_setup()
				return
	_current_event = generator.next_card(state, content, rng)
	_after_week_setup()

func _after_week_setup() -> void:
	_sync_room()
	generator.prefetch(state)
	_open_btn.visible = true
	_open_btn.text = "OPEN THE JOURNAL — WEEK %d AWAITS" % state.week

# ---------- the journal ----------

## ─────────────────────────────────────────────────────────────────────────
## THE LOG BOOK — every page is built through JournalPage (MAIN-owned), which
## owns the geometry, the type and the ruling. This lane only decides WHAT is
## on each page: the situation, then what the player can do about it.
## ─────────────────────────────────────────────────────────────────────────
var _jp: JournalPage

## Fit a row to the space the zone actually has left, and to how many things share
## it. Width matters as much as height: a caption box wider than the column step
## overlaps its neighbour, which is what clipped "Enterprise" to "Enterpris".
func _row_cell(zone: String, n: int, reserve: float = 0.0) -> Vector2:
	var free: float = _jp.room_left(zone) - reserve
	var sp := _jp.span_at(_jp.zone_bottom(zone) - 60.0)
	var avail: float = maxf(sp.y - sp.x, 320.0)
	return Vector2(clampf(avail / float(maxi(n, 1)) - 10.0, 90.0, 190.0),
		clampf(free - 10.0, 84.0, 152.0))

func _tex(path: String) -> Texture2D:
	var p := path if path.begins_with("res://") else "res://assets/sprites/%s.png" % path
	return load(p) if ResourceLoader.exists(p) else null

func _open_journal() -> void:
	_open_btn.visible = false
	for c in _journal.get_children():
		c.queue_free()
	_journal.visible = true
	_sfx["card_flip"].play()
	var dim := ColorRect.new()
	dim.color = Color(0.06, 0.05, 0.05, 0.30)
	dim.size = Vector2(1536, 1024)
	_journal.add_child(dim)
	dim.gui_input.connect(func(ev):
		if ev is InputEventMouseButton and ev.pressed and not _adjudicating:
			_close_journal())
	_j_page = Control.new()
	_j_page.set_anchors_preset(Control.PRESET_FULL_RECT)
	_j_page.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_journal.add_child(_j_page)
	_j_left = _j_page
	_j_right = _j_page
	_show_spread()

func _show_spread() -> void:
	for c in _j_page.get_children():
		_j_page.remove_child(c)
		c.queue_free()
	_sfx["card_flip"].play()
	_refresh_scene()
	var pg := JournalPage.new()
	_jp = pg
	_j_page.add_child(pg)
	pg.build("WEEK %d" % state.week, _scene_id)
	pg.prev_page.connect(func():
		if _page_i == 0 and _week_sheet == 1:
			_week_sheet = 0
			_show_spread()
			return
		if _page_i == 0:
			_close_journal()
			return
		_page_i -= 1
		_show_spread())
	pg.next_page.connect(func():
		if _page_i == 0 and _week_sheet == 0:
			_week_sheet = 1
			_show_spread()
			return
		_week_sheet = 0
		_page_i = mini(_page_i + 1, 4)
		_show_spread())
	pg.written.connect(func(t): _free_text[_page_i] = t)
	match _page_i:
		0:
			_page_consequences()
		1:
			_page_people()
		2:
			_page_work()
		3:
			_page_situation()
		4:
			_page_decision()
	pg.arrows(true, _page_i < 4)

## ── the week: WHAT YOUR LAST MOVE CAUSED, then what is left ──────────────
## The owner, after playing: "we don't have actual text output for week N about
## the CONSEQUENCES of week N-1 given choices of user... no sense of progression".
## So the week OPENS with the story and only reaches the numbers on a second
## sheet: what you said, how the world read it, what happened, the sting, then
## the measurable result. A bare status page is a defect.
const EFFECT_ICONS := {
	"cash": "gv/money_2", "product": "itm_laptop", "traction": "cf_business_happy",
	"morale": "itm_goodwill", "hype": "gv/chart_1", "founder": "itm_idea_napkin",
	"everyone": "itm_idea_napkin", "hired": "cf_technical_happy", "round": "gv/money_2",
}

## "cash -900" / "product +8" → a drawing with a signed number under it.
func _effect_chip(entry: String, i: int) -> Dictionary:
	var parts := entry.strip_edges().split(" ", false)
	if parts.is_empty():
		return {}
	var noun := String(parts[0]).to_lower()
	var val := String(parts[1]) if parts.size() > 1 else ""
	var caption := entry.strip_edges()
	if noun == "cash" and val != "":
		caption = "%s$%s" % ["+" if not val.begins_with("-") else "-", val.lstrip("+-")]
	elif val != "":
		caption = "%s %s" % [val, ("customers" if noun == "traction" else noun)]
	return {"id": "fx%d" % i, "tex": _tex(String(EFFECT_ICONS.get(noun, "itm_goodwill"))), "text": caption}

func _page_consequences() -> void:
	if _week_sheet == 1:
		_week_state()
		return
	var said := String(_last_outcome.get("said", "")).strip_edges()
	var heard := String(_last_outcome.get("heard", "")).strip_edges()
	var narration := String(_last_outcome.get("narration", "")).strip_edges()
	var reality := String(_last_outcome.get("reality", "")).strip_edges()
	if said == "" and narration == "":
		# an honest line, never numbers with no story behind them
		_jp.line("You made no move last week. The week passed anyway, and it still cost you.")
		_week_state()
		return
	if said != "":
		_jp.line("You said: \"%s\"" % said)
	if heard != "":
		_jp.line("They heard: %s" % heard)
	if narration != "":
		_jp.line(narration, false, "body" if _jp.room_left("body") > 100.0 else "ending")
	if reality != "":
		_jp.line(reality, true, "body" if _jp.room_left("body") > 60.0 else "ending")
	var chips: Array = []
	var dec_log: Array = _last_outcome.get("dec_log", [])
	for k in dec_log.size():
		var c := _effect_chip(String(dec_log[k]), k)
		if not c.is_empty():
			chips.append(c)
	if chips.is_empty():
		if _jp.room_left("ending") > 60.0:
			_jp.line("Nothing measurable moved.", true, "ending")
	elif _jp.room_left("ending") > 150.0:
		_jp.line("What it cost you:", false, "ending")
		_jp.icon_row(chips.slice(0, 4), _row_cell("ending", mini(chips.size(), 4)))

## Sheet two: the state, once the story has been told.
func _week_state() -> void:
	var net := state.burn_per_week()
	var weeks := 999 if net <= 0 else maxi(0, int(floor(float(state.cash) / float(net))))
	if net > 0:
		_jp.line("$%s goes out every week. That is %d weeks of it left." % [_fmt(net), weeks])
	else:
		_jp.line("You are making money. $%s a week comes in." % _fmt(absi(net)))
	var jars: Array = []
	for i in 6:
		jars.append({"id": "w%d" % i, "tex": _tex("itm_savings_jar"), "text": ""})
	var row := _jp.icon_row(jars, _row_cell("body", jars.size()), "body")
	var lit := clampi(weeks, 0, 6)
	for i in row.get_child_count():
		var slot: Control = row.get_child(i)
		slot.mouse_filter = Control.MOUSE_FILTER_IGNORE
		slot.modulate = Color(1, 1, 1, 1.0 if i < lit else 0.45)
	_jp.line("v0.%d on the board  ·  %d customers" % [state.product, state.traction], true, "ending")

## ── the crew: who is here, and what you hand one of them ─────────────────
func _page_people() -> void:
	var slugs := {"tech": "technical", "technical": "technical", "business": "business",
		"sales": "business", "hustler": "idea", "the idea friend": "idea", "design": "design"}
	var faces: Array = []
	faces.append({"id": "you", "tex": _tex("chr_arch_%s" % state.archetype_id), "text": "you"})
	for i in state.cofounders.size():
		var cf: Dictionary = state.cofounders[i]
		if not cf.has("loyalty"):
			cf["loyalty"] = 70
		var loy := int(cf["loyalty"])
		var mood := "happy" if loy > 70 else ("neutral" if loy > 30 else "resentful")
		var slug: String = slugs.get(String(cf.get("role", "Technical")).to_lower(), "technical")
		faces.append({"id": "cf%d" % i, "tex": _tex("cf_%s_%s" % [slug, mood]),
			"text": String(cf.get("role", "?")).to_lower()})
	for e in state.employees:
		var bs := GameState.burnout_state(int(e.get("burnout", 0)))
		faces.append({"id": "emp", "tex": _tex("cf_technical_%s" % ("resentful" if bs in ["cooked", "gone"] else ("neutral" if bs == "frayed" else "happy"))),
			"text": String(e.get("name", "hire")).to_lower()})
	if faces.size() > 4:
		faces = faces.slice(0, 4)
	_jp.line("Who is still here.")
	var frow := _jp.icon_row(faces, _row_cell("body", faces.size()), "body")
	if state.cofounders.is_empty():
		_jp.line("Nobody else yet. The plant cannot hold equity.", true)
		return
	# who you are tipping, and with what — both are pen circles
	var picked := [0]
	_jp.choice_made.connect(func(id: String):
		if id.begins_with("cf"):
			picked[0] = int(id.substr(2))
		elif id.begins_with("g:"):
			_pending_people[picked[0]] = id.substr(2)
			_sfx["cash"].play())
	var gifts := [
		{"id": "g:pay", "tex": _tex("itm_savings_jar"), "text": "a bonus"},
		{"id": "g:shares", "tex": _tex("itm_idea_napkin"), "text": "a slice"},
		{"id": "g:equip", "tex": _tex("itm_laptop"), "text": "new gear"},
	]
	_jp.line("Give one of them something this week.", false, "ending")
	_jp.icon_row(gifts, _row_cell("ending", gifts.size(), 132.0))
	var gte := _jp.write_field()
	gte.text = String(_free_text.get(1, ""))
	_wire_free(gte)

## ── the work: where the week is pointed ──────────────────────────────────
func _page_work() -> void:
	_jp.line("Point the week at something. One move each.")
	var moves: Array = []
	for dept in WORK_DEPTS:
		for pr in WORK_PRESETS[dept]:
			var pid := String(pr["id"])
			moves.append({"id": "%s|%s" % [dept, pid], "tex": _tex(String(WORK_ICONS.get(pid, "itm_laptop"))),
				"text": String(WORK_SHORT.get(pid, pr["label"]))})
	_jp.icon_row(moves.slice(0, 3), _row_cell("body", 3), "body")
	_jp.icon_row(moves.slice(3, 6), _row_cell("ending", 3, 132.0))
	_jp.choice_made.connect(func(id: String):
		if not "|" in id:
			return
		var parts := id.split("|")
		_pending_work[parts[0]] = {"kind": "preset", "id": parts[1]}
		_sfx["cash"].play())
	var open_dept := "PRODUCT"
	for d in WORK_DEPTS:
		if not _pending_work.has(d):
			open_dept = String(d)
			break
	var te := _jp.write_field("...or write what %s actually does" % open_dept.to_lower())
	te.text = String(_free_text.get(2, ""))
	te.gui_input.connect(func(ev):
		if ev is InputEventKey and ev.pressed and ev.keycode == KEY_ENTER and not ev.shift_pressed:
			te.accept_event()
			var t := te.text.strip_edges()
			if t != "":
				_pending_work[open_dept] = {"kind": "free", "text": t}
				_sfx["cash"].play())

## ── what happened: the situation, then what you do about it ──────────────
func _page_situation() -> void:
	if _current_event.is_empty():
		_jp.line("Nothing came for you this week. That is not the same as safe.")
		var te0 := _jp.write_field()
		te0.text = String(_free_text.get(3, ""))
		_wire_free(te0)
		return
	_jp.line(String(_current_event.get("body", "")))
	if _adjudicating:
		_jp.line("the world considers...", true)
		return
	var te := _jp.write_field()
	te.text = String(_free_text.get(3, ""))
	_wire_free(te)

## ── the decision: circle one, or write your own ──────────────────────────
func _page_decision() -> void:
	if _current_event.is_empty():
		_jp.line("Nothing to decide. Lock the week and let it run.")
		_lock_button()
		return
	var opts: Array = []
	for i in _current_event.get("choices", []).size():
		var choice: Dictionary = _current_event["choices"][i]
		var locked := _choice_lock_reason(choice)
		var label := String(choice.get("label", "..."))
		if locked != "":
			label += " (%s)" % locked
		opts.append({"id": "c%d" % i, "text": label, "locked": locked != ""})
	_jp.choice_made.connect(func(id: String):
		if not id.begins_with("c"):
			return
		var idx := int(id.substr(1))
		var ch: Dictionary = _current_event["choices"][idx]
		if _choice_lock_reason(ch) != "":
			return
		_pending_free = {}
		_pending_choice = ch
		_sfx["cash"].play()
		_lock_button())
	_jp.line(String(_current_event.get("title", "")) + " — what do you do?")
	_jp.icon_row(opts, _row_cell("ending", opts.size(), 150.0))
	var te := _jp.write_field()
	te.text = String(_free_text.get(4, ""))
	_wire_free(te)
	_lock_button()

## Enter commits a written move to the world for adjudication.
func _wire_free(te: TextEdit) -> void:
	te.gui_input.connect(func(ev):
		if ev is InputEventKey and ev.pressed and ev.keycode == KEY_ENTER and not ev.shift_pressed:
			te.accept_event()
			var t := te.text.strip_edges()
			if t != "":
				_free_move(t))

func _choice_lock_reason(choice: Dictionary) -> String:
	if choice.has("needs_item") and not state.has_item(String(choice["needs_item"])):
		var nm := String(content.items.get(String(choice["needs_item"]), {}).get("name", choice["needs_item"]))
		return "needs " + nm
	if choice.has("needs_role"):
		var found := false
		for cf in state.cofounders:
			if String(cf.get("role", "")) == String(choice["needs_role"]):
				found = true
		if not found:
			return "needs a %s cofounder" % String(choice["needs_role"]).to_lower()
	if choice.has("needs_cash") and state.cash < int(choice["needs_cash"]):
		return "needs $%s" % _fmt(int(choice["needs_cash"]))
	return ""

## The commit lives in the controls zone with the arrows, written not chromed.
func _lock_button() -> void:
	if _jp == null or not is_instance_valid(_jp):
		return
	for c in _jp.space.get_children():
		if c.has_meta("lock"):
			c.queue_free()
	var ready := (not _pending_choice.is_empty()) or (not _pending_free.is_empty()) or _current_event.is_empty()
	var b := Button.new()
	b.set_meta("lock", true)
	b.text = "lock the week" if ready else "...decide first"
	b.add_theme_font_override("font", _font)
	b.add_theme_font_size_override("font_size", 34)
	b.add_theme_color_override("font_color", PALETTE["coral"] if ready else Color(PALETTE["ink"], 0.35))
	b.add_theme_color_override("font_hover_color", PALETTE["coral"])
	b.add_theme_color_override("font_disabled_color", Color(PALETTE["ink"], 0.32))
	for stn in ["normal", "hover", "pressed", "focus", "disabled"]:
		b.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	b.disabled = not ready
	var ly := _jp.zone_bottom("ending") + 6.0
	var sp := _jp.span_at(ly)
	b.position = Vector2(sp.x + (sp.y - sp.x) * 0.5 - 170.0, ly)
	b.custom_minimum_size = Vector2(340, 48)
	b.set_deferred("size", Vector2(340, 48))
	b.pressed.connect(_lock_week)
	_jp.space.add_child(b)

## ── THE PIVOT — two questions on one sheet, then the price ───────────────
const PIVOT_WHAT := [["Software", "itm_laptop"], ["Hardware", "itm_dads_server"], ["Market", "gv/money_2"]]
const PIVOT_WHO := [["Enterprise", "cf_business_neutral"], ["SMB", "chr_arch_hustler"], ["Consumer", "chr_arch_dropout"]]
const PIVOT_KEEP := [["fight for them", "cf_business_happy"], ["clean break", "cf_business_resentful"], ["never mind", "itm_idea_napkin"]]

var _pivot_layer: Control
var _pivot_step := 0
var _pivot_what := ""
var _pivot_who := ""
var _pivot_keep := ""

func _open_pivot() -> void:
	_pivot_step = 0
	_pivot_what = String(state.biz_what)
	_pivot_who = String(state.biz_who)
	_pivot_keep = ""
	_pivot_layer = Control.new()
	_pivot_layer.set_anchors_preset(Control.PRESET_FULL_RECT)
	_journal.add_child(_pivot_layer)
	_show_pivot()

func _pivot_cost() -> int:
	return 500 + 30 * state.traction + (500 if state.funding_id == "angel" else 0)

## THREE SHEETS, one question each. The ENDING zone holds ~250px; a title, two
## questions, two rows, a write field and a summary is roughly four times that,
## which is what put icons on top of words and ran the last line off the paper.
## The shell is built to SPLIT rather than shrink, so it splits.
func _show_pivot() -> void:
	if _pivot_layer == null or not is_instance_valid(_pivot_layer):
		return
	for c in _pivot_layer.get_children():
		_pivot_layer.remove_child(c)
		c.queue_free()
	var pg := JournalPage.new()
	_jp = pg
	_pivot_layer.add_child(pg)
	pg.build("THE PIVOT", _scene_id)
	pg.prev_page.connect(func():
		if _pivot_step == 0:
			_pivot_layer.queue_free()
			_pivot_layer = null
			return
		_pivot_step -= 1
		_show_pivot())
	pg.next_page.connect(_pivot_forward)
	match _pivot_step:
		0:
			pg.line("The idea is not working. What are you building now?")
			pg.choice_made.connect(func(id: String):
				if id.begins_with("wt:"):
					_pivot_what = id.substr(3)
					_sfx["cash"].play())
			var what_items: Array = []
			for o in PIVOT_WHAT:
				what_items.append({"id": "wt:" + String(o[0]), "tex": _tex(String(o[1])), "text": String(o[0])})
			pg.icon_row(what_items, _row_cell("ending", what_items.size()))
		1:
			pg.line("Who is it for?")
			pg.choice_made.connect(func(id: String):
				if id.begins_with("wo:"):
					_pivot_who = id.substr(3)
					_sfx["cash"].play())
			var who_items: Array = []
			for o2 in PIVOT_WHO:
				who_items.append({"id": "wo:" + String(o2[0]), "tex": _tex(String(o2[1])), "text": String(o2[0])})
			pg.icon_row(who_items, _row_cell("ending", who_items.size()))
		_:
			pg.line("%s for %s — \"%s\"" % [_pivot_what.to_lower(), _pivot_who.to_lower(), state.company_idea])
			pg.line("$%s to rebrand and rebuild. The product drops to v0.%d." % [_fmt(_pivot_cost()), maxi(1, int(state.product * 0.4) / 10)])
			pg.line("The angel leaves a voice memo." if state.funding_id == "angel" else "Nobody to answer to but you.", true)
			pg.choice_made.connect(func(id: String):
				if id.begins_with("k:"):
					_pivot_keep = id.substr(2)
					_sfx["cash"].play())
			var keep_items: Array = []
			for k in PIVOT_KEEP:
				keep_items.append({"id": "k:" + String(k[0]), "tex": _tex(String(k[1])), "text": String(k[0])})
			pg.line("And the %d customers you have?" % state.traction, false, "ending")
			pg.icon_row(keep_items, _row_cell("ending", keep_items.size()))
	pg.arrows(true, true)

func _pivot_forward() -> void:
	if _pivot_step < 2:
		_pivot_step += 1
		_show_pivot()
		return
	if _pivot_keep == "":
		return
	if _pivot_keep == "never mind":
		_pivot_layer.queue_free()
		_pivot_layer = null
		return
	var kept := 0
	if _pivot_keep == "fight for them":
		kept = int(state.traction * (0.3 + 0.05 * int(state.competences.get("grit", 3))))
	var lay := _pivot_layer
	_pivot_layer = null
	_do_pivot(lay, state.company_idea, _pivot_what, _pivot_who, _pivot_cost(), kept, _pivot_keep == "fight for them")

const WORK_SHORT := {
	"sprint": "sprint", "polish": "polish",
	"post_log": "build log", "outreach": "outreach",
	"demos": "demos", "chase": "invoices",
}

const WORK_ICONS := {
	"sprint": "itm_laptop", "polish": "itm_idea_napkin",
	"post_log": "gv/chart_1", "outreach": "itm_bus_pass",
	"demos": "env_poster", "chase": "itm_savings_jar",
}

## Everything applies at once: gestures, work, then the decision. Then the week turns.
func _lock_week() -> void:
	if _adjudicating:
		return
	var free_depts: Array = []
	for dept in _pending_work:
		var w: Dictionary = _pending_work[dept]
		if w.get("kind", "") == "free" and String(w.get("text", "")).strip_edges() != "":
			free_depts.append(dept)
	if free_depts.is_empty():
		_apply_lock({})
		return
	_adjudicating = true
	_show_spread()
	var results: Dictionary = {}
	var remaining: Array = [free_depts.size()]
	for dept in free_depts:
		var text := String(_pending_work[dept]["text"])
		var synth := {"title": "The week's %s initiative" % String(dept).to_lower(),
			"body": "Instead of a preset, the founder commits this week's %s effort to a plan of their own." % String(dept).to_lower()}
		generator.adjudicate(state, synth, text, func(result: Dictionary):
			results[dept] = result
			remaining[0] -= 1
			if remaining[0] <= 0:
				_adjudicating = false
				_apply_lock(results))

func _apply_lock(work_results: Dictionary) -> void:
	var outcome_log: Array = []
	# the gestures you made to the people who stayed
	for i in _pending_people:
		if i < 0 or i >= state.cofounders.size():
			continue
		var cf: Dictionary = state.cofounders[i]
		match String(_pending_people[i]):
			"pay":
				if state.cash >= 500:
					state.cash -= 500
					cf["loyalty"] = mini(100, int(cf.get("loyalty", 70)) + 15)
					outcome_log.append("bonus to %s: -$500" % String(cf.get("role", "?")))
				else:
					outcome_log.append("bonus to %s: the account said no" % String(cf.get("role", "?")))
			"shares":
				state.founder_pct = maxf(0.0, float(state.founder_pct) - 2.0)
				cf["loyalty"] = mini(100, int(cf.get("loyalty", 70)) + 25)
				outcome_log.append("+2%% of the company to %s" % String(cf.get("role", "?")))
			"equip":
				if state.cash >= 300:
					state.cash -= 300
					cf["loyalty"] = mini(100, int(cf.get("loyalty", 70)) + 10)
					outcome_log.append("new gear for %s: -$300" % String(cf.get("role", "?")))
		state.log_action("gesture to %s: %s" % [String(cf.get("role", "?")), String(_pending_people[i])])
	# the week's work: presets are deterministic, free plans were adjudicated
	for dept in _pending_work:
		var w: Dictionary = _pending_work[dept]
		if w.get("kind", "") == "preset":
			match String(w.get("id", "")):
				"sprint":
					state.product = clampi(state.product + 4, 0, 100)
					state.morale = maxi(0, state.morale - 2)
					outcome_log.append("product sprint: +4 product, -2 morale")
				"polish":
					state.product = clampi(state.product + 2, 0, 100)
					state.morale = mini(100, state.morale + 2)
					outcome_log.append("polish week: +2 product, +2 morale")
				"post_log":
					state.hype = mini(100, state.hype + 3)
					if not state.flags.has("posted_publicly"):
						state.flags.append("posted_publicly")
					outcome_log.append("build log posted: +3 hype")
				"outreach":
					state.traction += 1
					state.morale = maxi(0, state.morale - 2)
					outcome_log.append("cold outreach: +1 user, -2 morale")
				"demos":
					if state.product >= 30:
						state.traction += 2
						if not state.flags.has("did_demos"):
							state.flags.append("did_demos")
						outcome_log.append("demo calls: +2 users")
					else:
						state.morale = maxi(0, state.morale - 2)
						outcome_log.append("demo calls: they saw the bugs. -2 morale")
				"chase":
					if state.traction > 0:
						state.cash += 300
						if not state.flags.has("chased_invoices"):
							state.flags.append("chased_invoices")
						outcome_log.append("invoices chased: +$300")
					else:
						outcome_log.append("invoices chased: there are no invoices")
			state.log_action("%s: %s" % [String(dept).to_lower(), String(w.get("id", ""))])
		elif w.get("kind", "") == "free":
			var res: Dictionary = work_results.get(dept, {})
			if res.is_empty():
				outcome_log.append("%s plan: the fax machine ate it (no effect)" % String(dept).to_lower())
			else:
				var wlog := EffectOps.apply_all(res.get("effects", []), state)
				var v := String(res.get("verdict", ""))
				outcome_log.append("%s plan [%s]: %s" % [String(dept).to_lower(), v.to_upper(), String(res.get("narration", "")).left(90)])
				for l2 in wlog:
					outcome_log.append("   " + l2)
				state.log_action("%s initiative (%s): %s" % [String(dept).to_lower(), v, String(w.get("text", "")).left(60)])
	# the decision itself: a written move that the world judged, or a listed one
	var title := String(_current_event.get("title", "a quiet week"))
	if not _pending_free.is_empty():
		var log := EffectOps.apply_all(_pending_free.get("effects", []), state)
		for l in log:
			outcome_log.append(l)
		record.log_event(state.week, _current_event, "[wrote] " + String(_pending_free.get("player_text", "")), log)
		state.log_action("event '%s' — wrote: %s (%s)" % [title, String(_pending_free.get("player_text", "")).left(60), String(_pending_free.get("verdict", ""))])
		_last_outcome = {"title": title, "verdict": String(_pending_free.get("verdict", "")),
			"said": String(_pending_free.get("player_text", "")),
			"heard": String(_pending_free.get("interpreted_as", "")),
			"narration": String(_pending_free.get("narration", "")),
			"reality": String(_pending_free.get("reality_check", "")),
			"dec_log": log, "log": outcome_log}
	elif not _pending_choice.is_empty():
		var log2 := EffectOps.apply_all(_pending_choice.get("effects", []), state)
		for l2 in log2:
			outcome_log.append(l2)
		record.log_event(state.week, _current_event, String(_pending_choice.get("label", "")), log2)
		state.log_action("event '%s' — chose: %s" % [title, String(_pending_choice.get("label", ""))])
		_last_outcome = {"title": title, "verdict": "",
			"said": String(_pending_choice.get("label", "")), "heard": "",
			"narration": String(_pending_choice.get("outcome", "")),
			"reality": "", "dec_log": log2, "log": outcome_log}
	else:
		_last_outcome = {"title": title, "verdict": "", "said": "", "heard": "",
			"narration": "", "reality": "", "dec_log": [], "log": outcome_log}
	_pending_choice = {}
	_pending_free = {}
	_pending_people.clear()
	_pending_work.clear()
	_free_text.clear()
	state.clampi_meters()
	_sync_room()
	if state.morale <= 0:
		_die("Founder Flatline — morale hit zero in week %d." % state.week)
		return
	if state.product >= 60 and state.traction >= 10 and not state.has_flag("act1_cleared"):
		record.log_event(state.week, {"id": "milestone", "title": "MVP + first users"}, "era gate reached", [])
		_sfx["win"].play()
		_over = true
		done.emit({"victory": true})
		return
	_next_week()

## THE WRITTEN MOVE goes to the world to be judged; the verdict comes back as
## interpreted_as / reality_check / effects and waits, pending, until the lock.
func _free_move(text: String) -> void:
	text = text.strip_edges()
	if text == "" or _adjudicating:
		return
	_adjudicating = true
	_pending_choice = {}
	_show_spread()
	generator.adjudicate(state, _current_event, text, func(result: Dictionary):
		_adjudicating = false
		if result.is_empty():
			_pending_free = {}
			if _journal.visible and _page_i == 3:
				_show_spread()
			return
		result["player_text"] = text
		result["reality"] = result.get("reality_check", "")
		_pending_free = result
		_sfx["deposit"].play()
		if _journal.visible and _page_i == 3:
			_show_spread())

func _do_pivot(layer: Control, new_idea: String, new_what: String, new_who: String, cost: int, kept: int, fought: bool) -> void:
	layer.queue_free()
	var old_idea := state.company_idea
	state.company_idea = new_idea.strip_edges() if new_idea.strip_edges() != "" else state.company_idea
	state.biz_what = new_what
	state.biz_who = new_who
	state.cash -= cost
	state.product = maxi(10, int(state.product * 0.4))
	state.pivots += 1
	var log: Array[String] = ["rebrand + rebuild: −$%s" % _fmt(cost), "product knocked back to v0.%d" % maxi(1, state.product / 10)]
	if fought:
		var lost := state.traction - kept
		state.traction = kept
		state.morale = maxi(0, state.morale - 8)
		log.append("%d customers convinced to stay, %d walked" % [kept, lost])
		log.append("the convincing tour: −8 morale")
	else:
		if state.traction > 0:
			log.append("%d customers released into the wild" % state.traction)
		state.traction = 0
		state.morale = maxi(0, state.morale - 4)
		state.hype = mini(100, state.hype + 3)
		log.append("clean story: +3 hype, −4 morale")
	if state.funding_id == "angel":
		state.hype = maxi(0, state.hype - 2)
		log.append("the angel's voice memo: −2 hype")
	if not state.flags.has("pivoted"):
		state.flags.append("pivoted")
	record.log_event(state.week, {"id": "pivot", "title": "THE PIVOT: %s → %s" % [old_idea, state.company_idea]}, "pivot #%d" % state.pivots, log)
	_week_log.append_array(log)
	_sync_room()
	_sfx["deposit"].play()
	if state.cash < 0:
		_die("Pivoted Into The Ground — the rebrand cost everything, week %d." % state.week)
		return
	# re-render the journal to show the new reality
	_open_journal()

func _close_journal() -> void:
	_journal.visible = false
	_open_btn.visible = true

func _next_week() -> void:
	if _over:
		return
	_journal.visible = false
	_open_btn.visible = false
	# the dread beat: lights out, a tick, then the new week reveals itself
	var dark := ColorRect.new()
	dark.color = Color(0.05, 0.05, 0.06, 0.0)
	dark.size = Vector2(1536, 1024)
	add_child(dark)
	var fact := _mk_label(FUN_FACTS[rng.randi_range(0, FUN_FACTS.size() - 1)], 22, Color(PALETTE["cream"], 0.0))
	fact.position = Vector2(240, 490)
	fact.size = Vector2(1060, 60)
	fact.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	fact.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	add_child(fact)
	_sfx["tick"].play()
	var tw := create_tween()
	tw.tween_property(dark, "color:a", 1.0, 0.35)
	tw.parallel().tween_property(fact, "modulate:a", 1.0, 0.4)
	tw.tween_interval(1.1)
	tw.tween_property(fact, "modulate:a", 0.0, 0.3)
	tw.tween_callback(fact.queue_free)
	tw.tween_callback(func(): _start_week())
	tw.tween_property(dark, "color:a", 0.0, 0.5)
	tw.tween_callback(dark.queue_free)

func _die(cause: String) -> void:
	state.dead = true
	state.death_cause = cause
	record.log_death(state.week, cause)
	_sfx["death"].play()
	_over = true
	await get_tree().create_timer(1.0).timeout
	done.emit({"death": cause})
