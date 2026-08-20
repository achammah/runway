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
var _week_told := 0                # how much of the story sheet one got through
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
var _week_opened := false                 # has THIS screen already opened a week? (see _start_week)
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
var _scene_layout: Dictionary = {}    # the stage's own annotation table (crew marks)
var _scene_id := "garage"             # which painted room we are standing in
var _room_bg: TextureRect             # last-resort plate when no stage will render
var _money_tag: Panel
var _cap_paper: Panel                 # equity plate — fallback for a room with no sticky
var _cap_label: Label
var _room_scene: SceneRoom            # the painted stage AND its cast (MAIN-owned)
var _surfaces: SceneSurfaces          # the room's own writable faces (MAIN-owned)
var _surface_layer: Control           # the z-band the handwriting lives in
var _surf_mode := false
var _surf_aligned := false            # ...and whether the room under it is the stage they were measured on
var _composed: TextureRect            # the model-composed room, when one has arrived
var _composed_path := ""
var _composed_for: Dictionary = {}    # scene_id -> a compose has already been asked for
var _composing := false
var _director: SceneDirector
## THE ASSEMBLED ROOM (docs/BLANK_SCENES_ARCHITECTURE.md). A blank scene out of the
## background library, this run's crew posed into its typed slots, and that scene's
## OWN writable faces. It replaces the old stage whole — see _mount_assembled().
var _stage: SceneStage
var _assembled := false               # is the assembled room the room we stand in?
## THE SPOT-PATCH ROOM (docs/BLANK_SCENES_ARCHITECTURE.md §8) — the rung ABOVE the
## assembled stage. Its blank and its people are all cut from native renders of the
## SAME scene, so nothing in it was ever pasted. See _mount_patch_scene().
var _patch: PatchScene
var _patch_mode := false              # is the patch room the room we stand in?
var _facet_id := ""                   # the library facet the stage was built from
var _mount_key := ""                  # scene id + facet the current room was mounted for
var _cast_key := ""                   # who was in it, so a departure re-poses the room
var _face_of: Dictionary = {}         # bank/product/users/equity/bag -> the drawn face

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
	# the handwriting on the room's surfaces sits in its own band: above the room
	# and its cast, below the HUD and the journal, and it survives an era swap.
	_surface_layer = Control.new()
	_surface_layer.set_anchors_preset(Control.PRESET_FULL_RECT)
	_surface_layer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_surface_layer)
	# SCENE-FIRST: the painted stage, with the cast composited onto its marks.
	_scene_id = SceneRoomPicker.scene_id_for(state)
	_mount_scene()

	# living objects (classic path only builds sprites; scene mode reuses the tag/labels)
	if not _scene_mode and not _assembled and not _patch_mode:
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
	if not _scene_mode and not _assembled and not _patch_mode:
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
	_cap_paper = cap_paper
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
	_cap_label = cap_lbl

	# decay + badge spots (hidden until earned); scene mode repositions onto the painting.
	# An ASSEMBLED room gets NONE of them: the facet it was built from already carries
	# the condition (in_the_red IS the fraying room), and a decay sprite measured for a
	# different stage laid over it is the second garage in the frame.
	if _assembled:
		pass
	elif _scene_mode:
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
	var stub := OS.get_environment(ROOM_STUB_ENV)
	if stub != "":
		adopt_composed(stub, false)
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

## ── THE ASSEMBLED ROOM — WIRED, AND OFF BY DEFAULT ───────────────────
##
## WHAT IT DOES when it is on: the era and the company's condition name a facet in
## the background library ("scrappy_workspace/garage/day_in_the_red_wide").
## SceneStage paints that blank scene and poses this run's crew into its typed slots;
## SceneSurfaces reads the SAME facet's annotations for the faces the numbers are
## written on. It lands as ONE unit — an earlier wiring layered the new stage over
## the old one while the surfaces still wrote at the OLD stage's coordinates, and put
## "$82,350" on a bare wall with two garages in the same frame — so when it mounts,
## the whole old stack (stage plate, layered spots and cutouts, sprite crew,
## old-coordinate surfaces) goes down in the same breath, and if any part of it
## misses, none of it is applied.
##
## WHY IT IS OFF. Posing library sprites onto measured slot coordinates still reads
## as pasting: "the placement is weird — what you have done is backgrounds and you
## just pasted on top random characters" (owner, on an assembly). The room that ships
## is the path below. The model being built instead gives every scene PER-SPOT
## IN-SCENE RENDITIONS — an empty patch and character patches for each spot, all cut
## from native renders of that same scene — so assembly composites the scene's own
## pixels and no foreign sprite ever lands in a room. That model reuses everything
## here: the same facet ids, the same slots as spot definitions, the same surfaces.
## So this stays wired and testable behind RUNWAY_STAGE rather than being deleted.
const STAGE_ENV := "RUNWAY_STAGE"

const ERA_FACETS := {
	"garage": "scrappy_workspace/garage",
	"coworking": "legit_workspace/coworking_hotdesk",
	"office": "legit_workspace/small_office",
	"floor": "legit_workspace/open_floor",
	"hq": "legit_workspace/hq_skyline",
}

## THE ROOM'S CONDITION IS THE COMPANY'S. Money first — an overdraft is the loudest
## fact in a run — then a room that is visibly winning, else the ordinary week. This
## picks the SCENE. Burnt moods pick the POSES instead, and SceneStage folds that in,
## so a mood is never multiplied against a room.
func _condition_for_state() -> String:
	if state == null:
		return "steady"
	if state.cash < 0:
		return "in_the_red"
	if state.morale >= 70 and state.cash > 60000:
		return "thriving"
	return "steady"


## The one switch. Off in every player build and every capture that does not ask for
## it, so the shipped room is the path below this block.
func _stage_enabled() -> bool:
	return OS.get_environment(STAGE_ENV) != ""


## What the current room was mounted for. The CONDITION only belongs in it when the
## assembled path is live — otherwise a room that never changes with the money would
## be torn down and rebuilt every time the balance crossed zero, taking the composed
## picture with it.
func _mount_key_now() -> String:
	var want := SceneRoomPicker.scene_id_for(state) if state != null else _scene_id
	# The patch room turns over on the ERA and nothing else, and the picker resolves
	# several eras to the same fallback id when their art is not on disk — so the era
	# is named here in its own right, or a move to the office would keep the garage.
	# Empty, and therefore inert, for every run that ships no patch scene.
	var patch := ""
	if state != null and PatchScene.exists_for(String(state.era)):
		patch = String(state.era)
	return "%s|%s|%s" % [want, _facet_for_state() if _stage_enabled() else "", patch]


## The facet of the room we are standing in, or "" when the era names no place.
## The per-spot rendition model needs exactly this mapping, so it lives here whether
## or not the assembled path above is switched on.
func _facet_for_state() -> String:
	if state == null:
		return ""
	var place := String(ERA_FACETS.get(String(state.era), ""))
	if place == "":
		return ""
	return "%s/day_%s_wide" % [place, _condition_for_state()]


## crew_cast() speaks the game's role vocabulary ("tech", "sales"); the pose library
## is keyed by character folder ("cofd_tech"). Translate, and mark the founder so
## SceneStage still hands them the most prominent slot in the room.
func _stage_cast() -> Array:
	var out: Array = []
	for c in crew_cast():
		var m: Dictionary = c
		var who := String(m.get("who", ""))
		var art := _pose_char(who, String(m.get("kind", "cofounder")))
		if art == "":
			continue
		out.append({
			"who": art,
			"mood": String(m.get("mood", "fine")),
			"doing": String(m.get("doing", "")),
			"founder": who == "founder",
		})
	return out


func _pose_char(who: String, kind: String) -> String:
	if kind == "employee":
		return "employee"
	var art := ""
	if who == "founder":
		art = String(FOUNDER_DIRS.get(String(state.archetype_id), "cast_hacker"))
	else:
		art = String(COFOUNDER_DIRS.get(who, ""))
	return art.trim_prefix("cast_")


## RUNG 1. Touches nothing and returns false when the room cannot be assembled, so
## the caller falls straight through to the old path.
func _mount_assembled() -> bool:
	if not _stage_enabled():
		return false
	var facet := _facet_for_state()
	if facet == "":
		return false
	var st := SceneStage.new()
	st.name = "assembled"
	_room.add_child(st)
	_room.move_child(st, 0)
	var cast := _stage_cast()
	if not st.build(facet, cast):
		_room.remove_child(st)
		st.queue_free()
		return false
	_stage = st
	_facet_id = facet
	# The faces come from the SAME facet the scene did. A room nobody annotated keeps
	# neither half: writing the bank onto a wall we never measured is precisely the
	# failure this integration exists to prevent.
	if not _mount_stage_surfaces():
		_room.remove_child(st)
		st.queue_free()
		_stage = null
		_facet_id = ""
		return false
	_assembled = true
	_cast_key = JSON.stringify(cast)
	_hide_old_stack()
	print("RUNWAY! room assembled: %s — %s" % [facet, _cast_line()])
	return true


## The handwriting is drawn INTO the stage: over the scene, UNDER the cast. A number
## on a board somebody is standing in front of is hidden by that body, the way it
## would be in the room — never tattooed across their back. A stage rebuild frees
## everything the stage owns, so this runs again after every build.
func _mount_stage_surfaces() -> bool:
	if _stage == null or not is_instance_valid(_stage):
		return false
	var sf := SceneSurfaces.new()
	if not sf.mount_background(_facet_id):
		sf.queue_free()
		return false
	_stage.add_child(sf)
	_stage.move_child(sf, mini(1, _stage.get_child_count() - 1))
	_surfaces = sf
	_surf_mode = true
	return true


## THE SWAP IS ATOMIC, so everything the old room drew goes down together: the stage
## plate, its layered spots and cutouts, the badges, the decay sprites, the sprite
## crew. Cheap and idempotent, so the weekly sync can simply call it.
func _hide_old_stack() -> void:
	for k in _spots:
		var tr: TextureRect = _spots[k]
		if is_instance_valid(tr):
			tr.visible = false
	for n in _crew_nodes:
		if is_instance_valid(n):
			(n as CanvasItem).visible = false
	if _room_scene != null and is_instance_valid(_room_scene):
		_room_scene.visible = false
	if _room_bg != null and is_instance_valid(_room_bg):
		_room_bg.visible = false


## The people in the room are state too: a cofounder walks out, morale burns someone
## down into the slumped pose, a hire arrives. The frame must never disagree with the
## ledger, so the cast is re-posed the moment it stops matching — and the surfaces
## are re-laid with it, because a rebuild frees them.
func _refresh_stage_cast() -> void:
	if not _assembled or _stage == null or not is_instance_valid(_stage):
		return
	var cast := _stage_cast()
	var key := JSON.stringify(cast)
	if key == _cast_key:
		return
	_cast_key = key
	if _stage.build(_facet_id, cast) and _mount_stage_surfaces():
		print("RUNWAY! room re-cast: %s — %s" % [_facet_id, _cast_line()])
		return
	_mount_scene()          # the whole unit re-lands, or the old path takes over


## What is actually standing in the room, for the run log — the one place a capture
## can be checked against without opening the frame.
func _cast_line() -> String:
	if _stage == null or not is_instance_valid(_stage):
		return ""
	var bits := PackedStringArray()
	for p in _stage.placements():
		var d: Dictionary = p
		bits.append("%s %s @%s" % [d.get("who", ""), d.get("pose", ""), d.get("slot_id", "")])
	for q in _stage.dropped():
		bits.append("%s DROPPED" % (q as Dictionary).get("who", ""))
	return String(", ").join(bits)


## THE EMPTY STAGE. This is the FLOOR of the room, never the finished picture:
## the annotated stages are painted with nobody in them, and the people arrive
## composed into the room by the model (see below), not pasted on top of it.
## SceneRoom (MAIN-owned) renders the stage and its animation loop; SceneSurfaces
## (MAIN-owned) owns the writable faces this screen writes the numbers on.
func _mount_scene() -> void:
	_scene_mode = false
	_assembled = false
	_patch_mode = false
	_surf_mode = false
	_surf_aligned = true
	_facet_id = ""
	_cast_key = ""
	_face_of = {}
	for old in [_patch, _stage, _room_scene, _surfaces, _room_bg]:
		if old != null and is_instance_valid(old):
			var par := (old as Node).get_parent()
			if par:
				par.remove_child(old)
			(old as Node).queue_free()
	_patch = null
	_stage = null
	_room_scene = null
	_surfaces = null
	_room_bg = null
	_mount_key = _mount_key_now()
	_load_scene_layout()
	# RUNG 0 — the spot-patch room: this era's own scene, with this run's crew chosen
	# out of that scene's own renditions. Nothing in it was pasted, so it outranks
	# every path below it.
	if _mount_patch_scene():
		return
	# RUNG 1 — the assembled room, with nothing of the old stack left under it.
	if _mount_assembled():
		return
	var sr := SceneRoom.new()
	sr.mouse_filter = Control.MOUSE_FILTER_IGNORE
	sr.size = Vector2(1536, 1024)
	_room.add_child(sr)
	_room.move_child(sr, 0)
	if sr.load_scene(_scene_id):
		_room_scene = sr
		_scene_mode = true
	else:
		_room.remove_child(sr)
		sr.queue_free()
	# a flat plate ONLY when no stage would render — never a blank screen
	if not _scene_mode:
		var bg := TextureRect.new()
		bg.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		bg.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
		bg.texture = _scene_base_tex()
		bg.set_deferred("size", Vector2(1536, 1024))
		_room.add_child(bg)
		_room.move_child(bg, 0)
		_room_bg = bg
	var sf := SceneSurfaces.new()
	if sf.mount(_scene_id):
		_surface_layer.add_child(sf)
		_surfaces = sf
		_surf_mode = true
	else:
		sf.queue_free()

## ── THE ROOM IS COMPOSED, NOT ASSEMBLED ──────────────────────────────────
##
## Pasting character cutouts onto a painted room can never match that room's own
## light, and the owner rejected the result outright: bland characters dropped in,
## each sitting in the grey rectangle its sprite sheet came with, no contact shadow
## that belongs to the floor. It is the project's oldest standing defect —
## "assembled, not organic" — and no amount of placement fixes it.
##
## So the cast is never composited here. The room is COMPOSED BY THE MODEL:
## SceneDirector sends the empty stage plus one reference image per character to
## seedream-v5.0-pro/edit, which paints THIS player's founder and cofounders into
## that room with its lighting and real contact shadows, each partly behind the
## furniture. docs/refs/pilot_composed_hangar.png is the proven result.
##
## THE COMPOSE RUNS ON THE STAGE WE ARE STANDING IN, not on a library background:
## the era stays correct, and the writable faces keep their coordinates, because
## the compose prompt holds the room "EXACTLY as the first image".
##
## IT CANNOT HAPPEN IN A FRAME — 67s with one character, 113s with four. Until a
## composed room arrives the screen shows the last good composed room, and failing
## that the plain empty stage. An empty room is better than a wrong one.
##
## The WEEKLY TURN's scene (lock → DM → compose → the reading beat) belongs to
## main.gd. This composes only an establishing room for a stage that has never
## been composed in this run — week one, and each era change — which is the empty
## first scene the owner opened the game to.
const CAST_REFS := "res://assets/scenes/refs.json"
const FOUNDER_DIRS := {
	"hacker": "cast_hacker", "hustler": "cast_founder_hustler",
	"consultant": "cast_founder_consultant", "exfaang": "cast_founder_pm",
}
const COFOUNDER_DIRS := {
	"sales": "cast_cofd_sales", "business": "cast_cofd_business",
	"tech": "cast_cofd_tech", "idea_friend": "cast_cofd_idea",
	"hustler": "cast_cofd_hustler",
}
## Role strings arrive as the draft's display names and events invent their own,
## so they are normalised to the one vocabulary the cast directories use.
const ROLE_KEYS := {
	"tech": "tech", "technical": "tech", "design": "tech", "engineer": "tech",
	"business": "business", "ops": "business", "sales": "sales",
	"hustler": "hustler", "idea": "idea_friend", "the idea friend": "idea_friend",
}
const ROLE_WORDS := {
	"founder": "founder", "sales": "sales cofounder", "business": "business cofounder",
	"tech": "technical cofounder", "idea_friend": "ideas-guy cofounder",
	"hustler": "hustler cofounder",
}
const MOOD_WORDS := {"burnt": " (burnt out, running on empty)", "gone": " (checked out entirely)"}
## What each one is physically doing, so the model has something to paint rather
## than five identical beans standing in a line.
const DOING := {
	"founder": "at the middle of the room, mid-thought, holding the week's notes",
	"tech": "hunched over a laptop, soldering iron in the other hand",
	"business": "working a clipboard, halfway through a column of numbers",
	"sales": "on a headset mid-call, gesturing at nobody",
	"hustler": "half out of a chair with a coffee, already leaving",
	"idea_friend": "gesturing at the whiteboard, carrying nothing useful",
}
## The harness seam ONLY: stands in for a two-minute render so the composed-room
## path can be photographed without a network call. Empty in a real game.
const ROOM_STUB_ENV := "RUNWAY_ROOM_STUB"

## Art costs money and up to three minutes; a capture harness never spends either.
func _harness() -> bool:
	for v in ["RUNWAY_SHOT", "RUNWAY_FULLRUN", "RUNWAY_LANEWIRE", "RUNWAY_READING", "RUNWAY_TURN"]:
		if OS.get_environment(v) != "":
			return true
	return false

func _role_key(role: String) -> String:
	var k := role.to_lower().strip_edges()
	if ROLE_KEYS.has(k):
		return String(ROLE_KEYS[k])
	for probe in ROLE_KEYS:
		if k.contains(String(probe)):
			return String(ROLE_KEYS[probe])
	return "tech"

## THIS PLAYER'S CREW — never a generic set. The founder's chosen archetype first,
## then every actual cofounder and hire, each carrying the mood their own numbers
## give them. Public, and shaped the way the DM writes its cast, so the weekly
## turn can compose the same people without rebuilding this.
func crew_cast() -> Array:
	var out: Array = []
	if state == null:
		return out
	out.append({"who": "founder", "kind": "founder",
		"mood": "burnt" if (state.morale <= 30 or state.weeks_in_red >= 2) else "fine",
		"doing": String(DOING["founder"])})
	for cf in state.cofounders:
		var loy := int(cf.get("loyalty", 70))
		var sour := loy <= 30 or state.morale <= 20 or state.has_flag("trap_underpaid_cofounder")
		var key := _role_key(String(cf.get("role", "Tech")))
		out.append({"who": key, "kind": "cofounder", "mood": "burnt" if sour else "fine",
			"doing": String(DOING.get(key, "at work"))})
	for e in state.employees:
		var bs := GameState.burnout_state(int(e.get("burnout", 0)))
		var ekey := _role_key(String(e.get("role", "generalist")))
		# `kind` keeps a hire from being drawn as a SECOND copy of the cofounder whose
		# job they share; the pose library has its own employee.
		out.append({"who": ekey, "kind": "employee",
			"mood": "burnt" if bs in ["cooked", "gone"] else "fine",
			"doing": String(DOING.get(ekey, "at work"))})
	# the render is 67s for one character and 113s for four; four is the ceiling
	return out.slice(0, 4)

func _refs() -> Dictionary:
	if not FileAccess.file_exists(CAST_REFS):
		return {}
	var r = JSON.parse_string(FileAccess.get_file_as_string(CAST_REFS))
	return r if r is Dictionary else {}

## A character with no fetchable reference is DROPPED, never substituted: the
## compose prompt numbers its roster against the images it is given, so telling
## the model about someone it was never shown is how a duplicate gets invented.
func _cast_ref(who: String, mood: String, refs: Dictionary) -> String:
	var base := ""
	if who == "founder":
		base = String(FOUNDER_DIRS.get(String(state.archetype_id), "cast_hacker"))
	else:
		base = String(COFOUNDER_DIRS.get(who, ""))
	if base == "":
		return ""
	for m in [mood, "fine"]:
		for layer in ["sprite", "scene"]:
			var u := String(refs.get("%s_%s/%s" % [base, m, layer], ""))
			if u.begins_with("http"):
				return u
	return ""

## The marks are no longer where sprites are pasted — they are what the model is
## told about where people plausibly stand in THIS room.
func _mark_beat() -> String:
	var marks := 0
	for k in _scene_layout:
		var row = _scene_layout[k]
		if row is Dictionary and String((row as Dictionary).get("kind", "")) == "crew_mark":
			marks += 1
	var where := "spread across the working half of the room"
	if marks > 0:
		where = "spread across the %d places people actually work in this room" % marks
	return "an ordinary working week: everyone %s, nobody posing for the camera" % where

## Ask for the establishing room. Fires once per stage per run, never inside a
## turn the week loop is already rendering, and never in a harness.
func _compose_room() -> void:
	if state == null or _composing or _harness() or OS.get_environment("RUNWAY_NO_ART") != "":
		return
	# An assembled room IS the room, and it is instantaneous. A two-minute render of
	# the LEGACY stage would replace it with a picture whose walls are not these
	# walls — every writable face would then be somewhere else.
	if _assembled:
		return
	if _composed_for.has(_scene_id) or _turn_in_flight():
		return
	var refs := _refs()
	var bg := ""
	for key in ["%s/scene" % _scene_id, "%s/room_bg" % _scene_id]:
		var u := String(refs.get(key, ""))
		if u.begins_with("http"):
			bg = u
			break
	if bg == "":
		return          # this stage has no fetchable plate; the empty stage stands
	var cast: Array = []
	var urls: Array = []
	var have_founder := false
	for c in crew_cast():
		var who := String((c as Dictionary).get("who", ""))
		var mood := String((c as Dictionary).get("mood", "fine"))
		var u2 := _cast_ref(who, mood, refs)
		if u2 == "":
			print("RUNWAY! no fetchable reference for %s (%s) — left out of the room" % [who, mood])
			continue
		if who == "founder":
			have_founder = true
		cast.append({"role": String(ROLE_WORDS.get(who, who)) + String(MOOD_WORDS.get(mood, "")),
			"doing": String((c as Dictionary).get("doing", "at work"))})
		urls.append(u2)
	# A ROOM WITHOUT THE PLAYER IN IT IS WORSE THAN AN EMPTY ROOM. The whole point
	# is that it is THIS founder and THIS crew, so if the archetype the player chose
	# has no uploaded reference we do not spend two minutes painting somebody else
	# into their garage — the plain stage stands and the gap says so in the log.
	if cast.is_empty() or not have_founder:
		if not have_founder:
			print("RUNWAY! room compose skipped — no reference for the %s founder" % String(state.archetype_id))
		return
	_composed_for[_scene_id] = true
	_composing = true
	if _director == null:
		_director = SceneDirector.new(get_tree())
		_director.ready.connect(_on_room_composed)
		_director.failed.connect(_on_room_compose_failed)
	_director.compose(bg, urls, cast, _mark_beat(), "room_%s_wk%02d" % [_scene_id, state.week])

func _on_room_composed(path: String) -> void:
	_composing = false
	# composed ON this stage, so the writable faces are still where they were measured
	adopt_composed(path, true)

## A failed compose is a cosmetic loss: the plain stage stands and the week runs on.
func _on_room_compose_failed(reason: String) -> void:
	_composing = false
	# one dead request must not leave a whole era empty, so the next week may ask
	# again — once, because _sync_room only runs when the week turns
	_composed_for.erase(_scene_id)
	print("RUNWAY! room compose skipped (%s) — the empty stage stands" % reason)

## Is the week loop already rendering this week's scene? Read through get() on
## purpose: the property belongs to another lane's file, and if it is ever renamed
## this must fall back to "no", not take the room down with it.
func _turn_in_flight() -> bool:
	var p := get_parent()
	if p == null:
		return false
	var busy = p.get("_turn_busy")
	return busy is bool and busy

## SHOW A COMPOSED ROOM. Public: the week loop hands this the scene its own turn
## produced, and the room becomes that scene instead of throwing it away.
##
## `aligned` says the image was composed on the stage we are standing in, so the
## write surfaces still land on the surfaces they were measured on. A scene
## composed somewhere else is a DIFFERENT room, so the handwriting stands down and
## the plates take over rather than writing the cash total across a stranger's wall.
func adopt_composed(path: String, aligned: bool = false) -> bool:
	var tex: Texture2D = null
	if path.begins_with("res://"):
		if ResourceLoader.exists(path):
			tex = load(path)
	else:
		var img := Image.new()
		if img.load(path) == OK:
			tex = ImageTexture.create_from_image(img)
	if tex == null:
		return false
	if _composed == null or not is_instance_valid(_composed):
		var tr := TextureRect.new()
		tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		# SCALE, not COVERED: the composed frame is 2048x1360 and the room is
		# 1536x1024, and a cover-crop would slide every write surface off its face.
		tr.stretch_mode = TextureRect.STRETCH_SCALE
		tr.mouse_filter = Control.MOUSE_FILTER_IGNORE
		tr.size = Vector2(1536, 1024)
		tr.set_deferred("size", Vector2(1536, 1024))
		_room.add_child(tr)
		_room.move_child(tr, mini(1, _room.get_child_count() - 1))
		_composed = tr
	_composed.texture = tex
	_composed.visible = true
	_composed_path = path
	# An assembled room's faces were measured on the LIBRARY scene, so no composed
	# image can ever be aligned to them.
	_surf_aligned = aligned and not _assembled
	if _patch and is_instance_valid(_patch):
		_patch.visible = false        # the composed turn image wins over every room
	if _stage and is_instance_valid(_stage):
		_stage.visible = false
	if _room_scene and is_instance_valid(_room_scene):
		_room_scene.visible = false
	_write_state_surfaces()
	_composed.modulate.a = 0.0
	var tw := create_tween()
	tw.tween_property(_composed, "modulate:a", 1.0, 0.4)
	return true

## The era turned, or the run moved on: the composed room belongs to the old
## stage, so it stands down and the new empty stage shows through.
func _drop_composed() -> void:
	_composed_path = ""
	_surf_aligned = true
	if _composed and is_instance_valid(_composed):
		_composed.visible = false
	if _patch and is_instance_valid(_patch):
		_patch.visible = true
	if _stage and is_instance_valid(_stage):
		_stage.visible = true
	if _room_scene and is_instance_valid(_room_scene):
		_room_scene.visible = true

## ── THE NUMBERS LIVE ON THE ROOM'S OWN SURFACES ──────────────────────────
## Cash is written in the ledger, product on the whiteboard, customers on the wall
## chart, equity on the sticky note. What the room has no face for keeps its old
## plate — per value, not all-or-nothing, because a stage can annotate one face and
## not another and the state must never simply disappear.
##
## WHICH DRAWN FACE EACH NUMBER GOES ON. The library rooms were not all furnished
## alike — a garage in the red has no ledger on its crate — so each value names the
## faces that suit it, best first, and takes the first one this room actually has and
## nobody has claimed yet. The hand-authored stages were annotated face-for-face, so
## only a library room walks past the first choice.
const FACE_PREF := {
	"bank": ["ledger", "sticky", "whiteboard", "wallchart"],
	"equity": ["sticky", "face_1", "face_2"],
	"users": ["wallchart", "face_1", "face_2"],
	"product": ["whiteboard", "face_2", "face_3"],
	"bag": ["inventory", "face_4"],
}
## Claim order: the money first, then what is still yours, then the meters.
const FACE_ORDER := ["bank", "equity", "users", "product", "bag"]


## The surfaces we may actually write on: mounted, and belonging to the room in
## front of us rather than to some other stage the picture came from.
func _live_surfaces() -> SceneSurfaces:
	if _surf_mode and _surf_aligned and _surfaces != null and is_instance_valid(_surfaces):
		return _surfaces
	return null


func _faces() -> Dictionary:
	var out: Dictionary = {}
	var s := _live_surfaces()
	if s == null:
		return out
	var used: Dictionary = {}
	for key in FACE_ORDER:
		var prefs: Array = FACE_PREF[key]
		for i in prefs.size():
			if i > 0 and not _assembled:
				break
			var face := String(prefs[i])
			if used.has(face) or not s.has(face):
				continue
			used[face] = true
			out[key] = face
			break
	return out


## Is the room under us one whose geometry we never measured — a scene composed
## somewhere else, or a library room the old plates were never positioned on?
func _off_stage() -> bool:
	# The patch blanks are NEW ART, not the annotated backgrounds the old plates were
	# measured on, so their geometry is unknown here too.
	return _showing_foreign_room() or _assembled or _patch_mode


func _write_state_surfaces() -> void:
	var s := _live_surfaces()
	_face_of = _faces()
	if _surface_layer and is_instance_valid(_surface_layer):
		# an assembled room carries its handwriting inside the stage, under the cast
		_surface_layer.visible = s != null and not _assembled
	if s != null:
		if _face_of.has("bank"):
			s.write(String(_face_of["bank"]), "IN THE BANK", _cash_str(),
				PALETTE["coral"] if state.cash < 0 else PALETTE["ink"])
		if _face_of.has("product"):
			s.write(String(_face_of["product"]), "PRODUCT", "v0.%d" % state.product)
		if _face_of.has("users"):
			# the face is ~100px: "CUSTOMERS" measures wider than that and clipped
			# itself to "CUSTOME" on the first capture
			s.write(String(_face_of["users"]), "USERS", str(state.traction))
		if _face_of.has("equity"):
			s.write(String(_face_of["equity"]), "YOURS", "%.0f%%" % state.founder_pct)
		if _face_of.has("bag"):
			s.write(String(_face_of["bag"]), "IN THE BAG", _inventory_text())
		if s.has("glass_wall"):
			s.write("glass_wall", state.company_name.to_upper(), "WEEK %d" % state.week)
	var off := _off_stage()
	if _money_tag and is_instance_valid(_money_tag):
		var txt := _money_text()
		if _money_label and is_instance_valid(_money_label):
			_money_label.text = txt
		_money_tag.visible = txt != ""
		if off:
			# on our own stage the plate sits where the art left room for it; in a room
			# we did not lay out, the only ground we can trust is the calm top strip the
			# composition law keeps clear, and ONE plate carries everything that room
			# has no drawn face for.
			var pw: float = _font_d.get_string_size(txt, HORIZONTAL_ALIGNMENT_LEFT, -1.0, 29).x
			_money_tag.position = Vector2(24, 76)
			_money_tag.set_deferred("size", Vector2(clampf(pw + 32.0, 180.0, 430.0), 48))
		else:
			_money_tag.position = Vector2(268, 714) if _scene_mode else Vector2(64, 700)
			_money_tag.set_deferred("size", Vector2(180, 48))
	if _cap_paper and is_instance_valid(_cap_paper):
		# the pinned equity paper hangs where the hand-authored stages left a nail; in
		# any other room the plate above carries the share instead of it floating.
		_cap_paper.visible = not _face_of.has("equity") and not off
		if _cap_label and is_instance_valid(_cap_label):
			_cap_label.text = "%.0f%%\nyours" % state.founder_pct

## Are we standing in a room composed somewhere other than this stage? Then its
## walls are not our walls: no writable face is where we measured it.
func _showing_foreign_room() -> bool:
	return _composed != null and is_instance_valid(_composed) and _composed.visible and not _surf_aligned

## THE LEFTOVERS LINE. Whatever this room has no drawn face for goes here and
## nowhere else — the bank when there is no ledger, the share when there is no
## sticky — so a number is either written on a real object or on this one plate,
## never floating in the middle of somebody's wall.
func _money_text() -> String:
	if state == null:
		return ""
	var parts := PackedStringArray()
	if not _face_of.has("bank"):
		parts.append(_cash_str())
	if _off_stage() and not _face_of.has("equity"):
		parts.append("%.0f%% yours" % state.founder_pct)
	return String("  ·  ").join(parts)

## An overdraft is written "-$300", not "$-300": the minus belongs to the money,
## not to the digits after the sign.
func _cash_str() -> String:
	if state == null:
		return ""
	return ("-$%s" % _fmt(absi(state.cash))) if state.cash < 0 else ("$%s" % _fmt(state.cash))

## What is in the bag, as a list, on the one surface built to hold a list.
func _inventory_text() -> String:
	var names: Array = []
	for id in state.items:
		var nm := String(content.items.get(String(id), {}).get("name", ""))
		if nm == "":
			nm = String(id).replace("itm_", "").replace("_", " ").capitalize()
		names.append(nm)
	if names.is_empty():
		return "nothing but nerve"
	if names.size() > 3:
		var head: Array = names.slice(0, 3)
		head.append("+%d more" % (names.size() - 3))
		names = head
	var txt := ""
	for i in names.size():
		txt += ("\n" if i > 0 else "") + String(names[i])
	return txt

func _scene_base_tex() -> Texture2D:
	for cand in ["res://assets/scenes/%s/room_bg.png" % _scene_id,
			"res://assets/scenes/%s/scene.png" % _scene_id,
			"res://assets/scenes/garage/room_bg.png", "res://assets/env/garage.png"]:
		if ResourceLoader.exists(cand):
			return load(cand)
	return null

## The stage's own annotation table. Read ONLY for the crew marks — SceneRoom
## does the rendering — so it can no longer decide whether a stage is renderable.
func _load_scene_layout() -> void:
	_scene_layout = {}
	var lp := "res://assets/scenes/%s/layout.json" % _scene_id
	if not FileAccess.file_exists(lp):
		return
	var parsed = JSON.parse_string(FileAccess.get_file_as_string(lp))
	if parsed is Dictionary:
		_scene_layout = parsed

## Sprite crew is the ART-LESS fallback only. In a real room nobody is pasted in:
## the people arrive painted into the composed scene or they do not arrive at all.
func _build_crew() -> void:
	if _scene_mode or _assembled or _patch_mode:
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
	tw.tween_interval(maxf(phase, 0.05))   # never 0: a looping tween with a zero-length step spins
	tw.tween_property(node, "scale", Vector2(1.005, 0.985), 1.1).set_trans(Tween.TRANS_SINE)
	tw.tween_property(node, "scale", Vector2.ONE, 1.1).set_trans(Tween.TRANS_SINE)

## The room reflects the state. Called after every change.
## Called on every state change — this is where the room stops being the garage.
## THE ERA TURNS THE ROOM OVER. A new stage means a new set of marks and a new
## set of writable faces, so the cast and the numbers are both re-laid onto it.
func _refresh_scene() -> void:
	if state == null:
		return
	var want := SceneRoomPicker.scene_id_for(state)
	# The room turns over on the ERA, and — once the assembled path is live — on the
	# CONDITION too: going into the red is a different room, not the same room with a
	# sadder sprite laid over it.
	var key := _mount_key_now()
	if key == _mount_key:
		return
	_scene_id = want
	_drop_composed()
	_mount_scene()
	_write_state_surfaces()

func _sync_room(instant: bool = false) -> void:
	_refresh_scene()
	_hud_label.text = "%s  ·  WEEK %d" % [state.company_name.to_upper(), state.week]
	# money pile grows/shrinks
	var mtier := 1
	if state.cash > 30000: mtier = 4
	elif state.cash > 12000: mtier = 3
	elif state.cash > 3000: mtier = 2
	_refresh_stage_cast()
	_refresh_patch_cast()
	_write_state_surfaces()
	if _assembled or _patch_mode:
		pass    # these rooms draw their own money; there is no pile sprite over them
	elif _scene_mode:
		var mc: TextureRect = _room_scene.get_layer("money") if (_room_scene and is_instance_valid(_room_scene)) else null
		if mc:
			var msc: float = [0.7, 0.85, 1.0, 1.14][mtier - 1]
			var mt := create_tween()
			mt.tween_property(mc, "scale", Vector2(msc, msc), 0.4).set_trans(Tween.TRANS_BACK)
	else:
		_set_spot_tex("money", GV + "money_%d.png" % mtier)
	_money_label.text = _money_text()
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
	if not _scene_mode and not _assembled and not _patch_mode:
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
	# decay tracks morale; badges track flags — but NEVER over an assembled room. The
	# facet it was built from already IS the decay (in_the_red is the fraying garage),
	# and the burnt moods are already in the poses, so the old spot sprites laid over
	# it would be the second garage in the frame.
	if _assembled or _patch_mode:
		_hide_old_stack()
		return
	if _scene_mode:
		var pz: TextureRect = _room_scene.get_layer("pizza") if (_room_scene and is_instance_valid(_room_scene)) else null
		if pz:
			pz.visible = state.morale < 45
	else:
		_show_spot("decay_pizza", state.morale < 30)
	_show_spot("decay_trash", state.morale < 45)
	_show_spot("decay_flies", state.morale < 22)
	_show_spot("decay_graffiti", state.morale < 15)
	_show_spot("badge_camp", state.has_flag("camp_alum"))
	_show_spot("badge_launched", state.has_flag("first_user"))
	_build_crew()
	_compose_room()

func _fmt(v: int) -> String:
	var t := str(absi(v))
	var out := ""
	while t.length() > 3:
		out = "," + t.substr(t.length() - 3) + out
		t = t.substr(0, t.length() - 3)
	return ("-" if v < 0 else "") + t + out

# ---------- weekly loop ----------

func _start_week() -> void:
	# WEEK 1 MUST EXIST. The counter opens at 1 and this used to advance it on the
	# way IN, so the first page anyone ever read said WEEK 2, the button said WEEK 2
	# AWAITS, and the player's own first week was missing from their record — the
	# autopsy jumped straight from "wk 0 founded" to "wk 2". The counter now advances
	# only once a week has actually been PLAYED, so the opening value is week 1.
	# A screen built mid-run still ticks: the act break makes a fresh one, and by
	# then the counter has long left 1 behind.
	if _week_opened or state.week > 1:
		state.week += 1
	_week_opened = true
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
## WHAT `icon_row` DOES WITH THE HEIGHT IT IS HANDED. The number is the row's BUDGET:
## the caption is taken out of it first and the drawing gets the rest, and the shell
## will not let a drawing fall under ICON_MIN_H — under that it grows the row instead.
## So the height a caller names IS the row's height, provided it leaves room for both.
## 76 and 56 did not: they left 30px and 10px of picture under captions bigger than the
## thing they belonged to. 150 leaves a drawing worth looking at on every page here.
const ROW_WANT_H := 150.0

## The written move needs the last rules of the sheet: its faint prompt, the two ruled
## lines the shell guarantees under it, and clear air above the lock line. A row that
## runs past this pushes the field down onto the lock button.
const WRITE_FIELD_RESERVE := 205.0

## The height to ASK a row for so it stops at `limit_y`. Below ICON_MIN_H the number
## stops meaning anything — the shell grows the row back — so that is the floor.
func _row_h(zone: String, limit_y: float, want: float = ROW_WANT_H) -> float:
	var cursor: float = _jp.zone_bottom(zone) - _jp.room_left(zone)
	return clampf(limit_y - cursor - JournalPage.GAP, JournalPage.ICON_MIN_H, want)

## Where a row has to stop for the written move to still fit under it.
func _write_limit() -> float:
	return _jp.zone_bottom("ending") - WRITE_FIELD_RESERVE

## Returns Vector2.ZERO when the row genuinely does not fit; callers skip it.
func _row_fits(zone: String, reserve: float) -> bool:
	return _jp.room_left(zone) - reserve > 56.0

func _row_cell(zone: String, n: int, reserve: float = 0.0) -> Vector2:
	var free: float = _jp.room_left(zone) - reserve
	var sp := _jp.span_at(_jp.zone_bottom(zone) - 60.0)
	var avail: float = maxf(sp.y - sp.x, 320.0)
	# ENDING is only 256px on the real page, and a prompt line plus the written
	# move already claim most of it, so a row here is small by necessity.
	return Vector2(clampf(avail / float(maxi(n, 1)) - 10.0, 90.0, 190.0),
		clampf(free - 10.0, 56.0, 152.0))

## Place a line where there is still room for it. BODY does not cascade into
## ENDING, so a line that overruns BODY draws on top of ENDING's first element
## (the faint write prompt). Choosing the zone up front is the fix.
func _say(text: String, faint: bool = false) -> void:
	_jp.line(text, faint, "body" if _jp.room_left("body") > 152.0 else "ending")

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

## The consequence chain as ordered lines, so it can be poured into the page
## rather than assigned to zones by hand — assigning left a hole between a short
## body line and the ending zone's fixed top.
func _story_lines() -> Array:
	var out: Array = []
	var said := String(_last_outcome.get("said", "")).strip_edges()
	var heard := String(_last_outcome.get("heard", "")).strip_edges()
	var verdict := String(_last_outcome.get("verdict", "")).strip_edges()
	var narration := String(_last_outcome.get("narration", "")).strip_edges()
	var reality := String(_last_outcome.get("reality", "")).strip_edges()
	if said != "":
		out.append(["You said: \"%s\"" % said, false, false])
	if heard != "":
		out.append(["They heard: %s" % heard, false, false])
	if verdict != "":
		out.append(["The world called it %s." % verdict.to_lower(), false, false])
	if narration != "":
		out.append([narration, false, true])
	if reality != "":
		out.append([reality, true, true])
	return out

## Will this line fit in the zone as it stands? A fixed threshold cannot answer
## that — a four-line narration is 175px where a one-liner is 45 — so measure the
## text at the paper's own width and leave a rule's margin for the snap.
func _line_h(zone: String, text: String) -> float:
	var sp := _jp.span_at(_jp.zone_bottom(zone) - 40.0)
	var w: float = maxf(sp.y - sp.x, 300.0) * 0.92
	return _font.get_multiline_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, w, 34).y

func _fits(zone: String, text: String) -> bool:
	return _jp.room_left(zone) > _line_h(zone, text) + 12.0

func _page_consequences() -> void:
	if _week_sheet == 1:
		_week_state()
		return
	var lines := _story_lines()
	if lines.is_empty():
		# an honest line, never numbers with no story behind them — and WEEK ONE HAS
		# NO LAST WEEK. Opening a brand new run by blaming the founder for a move they
		# were never offered is the off-by-one read out loud.
		_jp.line("Week one. Nothing has happened to you yet. After this, everything that does is yours."
			if state.week <= 1
			else "You made no move last week. The week passed anyway, and it still cost you.")
		_week_told = 0
		_week_state()
		return
	# Measured on the page: BODY is 213px and ENDING 256px. The three short lines
	# (~45px each) belong in BODY; the narration alone measures 188px and only
	# fits ENDING. So the split is by KIND of line, not by whatever room is left.
	var i := 0
	while i < lines.size() and not bool(lines[i][2]):
		_jp.line(String(lines[i][0]), bool(lines[i][1]))
		i += 1
	while i < lines.size() and _fits("ending", String(lines[i][0])):
		_jp.line(String(lines[i][0]), bool(lines[i][1]), "ending")
		i += 1
	_week_told = i

## The effect chips this decision produced, drawn where there is room for them.
func _cost_chips(zone: String) -> void:
	var dec_log: Array = _last_outcome.get("dec_log", [])
	if dec_log.is_empty():
		dec_log = _last_outcome.get("log", [])
	var chips: Array = []
	for k in dec_log.size():
		var c := _effect_chip(String(dec_log[k]), k)
		if not c.is_empty():
			chips.append(c)
	if chips.is_empty():
		return
	chips = chips.slice(0, 4)
	if not _row_fits(zone, 60.0):
		return
	_jp.line("What it cost you:", false, zone)
	_jp.icon_row(chips, _row_cell(zone, chips.size(), 0.0), zone)

## Sheet two: the state, once the story has been told.
func _week_state() -> void:
	var rest := _story_lines()
	var r := _week_told
	while r < rest.size() and _fits("body", String(rest[r][0])):
		_jp.line(String(rest[r][0]), bool(rest[r][1]))
		r += 1
	_cost_chips("body")
	var net := state.burn_per_week()
	var weeks := 999 if net <= 0 else maxi(0, int(floor(float(state.cash) / float(net))))
	if net > 0:
		_jp.line("$%s goes out every week. That is %d weeks of it left." % [_fmt(net), weeks], false, "ending")
	else:
		_jp.line("You are making money. $%s a week comes in." % _fmt(absi(net)), false, "ending")
	var jars: Array = []
	for i in 6:
		jars.append({"id": "w%d" % i, "tex": _tex("itm_savings_jar"), "text": ""})
	if not _row_fits("ending", 90.0):
		_jp.line("v0.%d on the board  ·  %d customers" % [state.product, state.traction], true, "ending")
		return
	var row := _jp.icon_row(jars, _row_cell("ending", jars.size(), 90.0), "ending")
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
	# THESE ARE PORTRAITS OF THE PEOPLE IN THE ROOM, not bullet points. At 76 they
	# came out 30px tall, under captions bigger than the faces, so the page read as
	# three words over three specks. BODY cannot hold a full-size row on its own —
	# the shell cascades what overflows into the zone below rather than printing on
	# top of it, which is exactly what that mechanism is for.
	var fcell := _row_cell("body", faces.size())
	var frow: Control = _jp.icon_row(faces, Vector2(fcell.x, ROW_WANT_H), "body")
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
	# A CROWDED SHEET TRADES PICTURES FOR ROOM TO WRITE. With a full crew the
	# faces row is tall, and a second row of drawings under it pushed the written
	# move onto the page curl — the one defect this book may never have. So when
	# the sheet is short of a full drawing row plus two ruled writing lines, the
	# gifts keep their pen circles and captions and give up their pictures.
	var lean: bool = _jp.room_to_fence("ending") < JournalPage.ICON_MIN_H + 210.0
	var gifts := [
		{"id": "g:pay", "tex": null if lean else _tex("itm_savings_jar"), "text": "a bonus"},
		{"id": "g:shares", "tex": null if lean else _tex("itm_idea_napkin"), "text": "a slice"},
		{"id": "g:equip", "tex": null if lean else _tex("itm_laptop"), "text": "new gear"},
	]
	_jp.line("Give one of them something this week.", false, "ending")
	# 56 drew the bonus, the slice and the gear at TEN pixels. This row shares what
	# is left of the sheet with the written move below it, so it takes as much of a
	# drawing as that leaves and never less than a visible one.
	var gcell := _row_cell("ending", gifts.size())
	_jp.icon_row(gifts, Vector2(gcell.x, _row_h("ending", _write_limit())), "ending")
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
	# Same as the crew page: 76 and 56 drew the week's six moves at 30px and 10px, so
	# `outreach`, `demos` and `invoices` were captions under dots. The top row takes a
	# full-size drawing and cascades; the bottom row takes what the written move leaves.
	var tcell := _row_cell("body", 3)
	var bcell := _row_cell("ending", 3)
	var bottom: Array = moves.slice(3, 6)
	_jp.icon_row(moves.slice(0, 3), Vector2(tcell.x, ROW_WANT_H), "body")
	_jp.icon_row(bottom, Vector2(bcell.x, _row_h("ending", _write_limit())), "ending")
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
	var ev_body := String(_current_event.get("body", ""))
	_jp.line(ev_body, false, "body" if _fits("body", ev_body) else "ending")
	if _adjudicating:
		_jp.line("the world considers...", true)
		return
	var te := _jp.write_field()
	te.text = String(_free_text.get(3, ""))
	_wire_free(te)

## ── the decision: circle one, or write your own ──────────────────────────
func _page_decision() -> void:
	if _current_event.is_empty():
		# A QUIET WEEK STILL TAKES A WRITTEN MOVE. Returning here left the decision
		# page with no field at all, so on any week without an event the player
		# could not write anything — and the written move is the whole game.
		_jp.line("Nothing came for you this week. So what do you do with it?")
		var qte := _jp.write_field()
		qte.text = String(_free_text.get(4, ""))
		_wire_free(qte)
		_lock_button()
		return
	var opts: Array = []
	var locks: Array = []
	for i in _current_event.get("choices", []).size():
		var choice: Dictionary = _current_event["choices"][i]
		var locked := _choice_lock_reason(choice)
		var label := String(choice.get("label", "..."))
		# THE REASON IS THE CAPTION'S SECOND LINE, never a parenthetical glued onto
		# the end of the sentence. A locked option has to LOOK locked before it is
		# clicked; a click that silently does nothing reads as a broken game.
		if locked != "":
			label += "\n" + locked
		locks.append(locked != "")
		opts.append({"id": "c%d" % i, "tex": _choice_tex(choice), "text": label,
			"locked": locked != ""})
	_jp.line(String(_current_event.get("title", "")) + " — what do you do?")
	var ocell := _row_cell("body", opts.size())
	var orow: Control = _jp.icon_row(opts, Vector2(ocell.x,
			_row_h("body", _write_limit())), "body")
	_dim_locked(orow, locks)
	_jp.choice_made.connect(func(id: String):
		if not id.begins_with("c"):
			return
		var idx := int(id.substr(1))
		var ch: Dictionary = _current_event["choices"][idx]
		if _choice_lock_reason(ch) != "":
			# picking a locked option must ANSWER — the shell has already drawn a pen
			# ring around it and lifted it out of the dim, so put both back
			_sfx["card_flip"].play()
			_dim_locked(orow, locks)
			return
		_pending_free = {}
		_pending_choice = ch
		_sfx["cash"].play()
		_dim_locked(orow, locks)
		_lock_button())
	# THE PAGE HAS TO ANSWER THE PEN. Pressing Enter re-renders this page with the
	# move already gone to the world, and without this line the page came back
	# character-for-character identical — so the one screen the game is built around
	# looked broken for as long as the verdict took. The situation page has said this
	# since it was written; the decision page, where the field actually is, did not.
	if _adjudicating:
		_jp.line("the world considers your move...", true, "ending")
		_lock_button()
		return
	var te := _jp.write_field()
	te.text = String(_free_text.get(4, ""))
	_wire_free(te)
	_lock_button()

## THE OPTION NEEDS A DRAWING. `icon_row` only draws one when the item carries a
## `tex`, and the listed choices carried none — so the decision page was two floating
## sentences with nothing to say they were pressable. A gated choice shows the very
## thing it is waiting on; otherwise the verb in the label picks the drawing. First
## match wins, and the fallback means no choice is ever iconless.
const CHOICE_ICONS := [
	["itm_savings_jar", ["$", "cash", "pay", "buy", "bank", "price", "charge", "cheap",
		"free", "refund", "money", "raise", "fund", "invoice", "salary", "bill"]],
	["cf_business_neutral", ["hire", "cofounder", "founder", "partner", "offer", "equity",
		"share", "recruit", "team", "call", "meet", "talk", "pitch", "email", "customer"]],
	["itm_laptop", ["ship", "build", "code", "hotfix", "fix", "polish", "rebuild",
		"product", "feature", "push", "demo", "launch"]],
	["gv/chart_1", ["post", "tweet", "announce", "publish", "press", "market", "ads",
		"public", "growth", "screenshot"]],
	["itm_energy_drinks", ["sleep", "rest", "night", "weekend", "ramen", "eat", "food",
		"coffee", "pizza", "window"]],
	["itm_dignity", ["decline", "skip", "refuse", "walk", "quit", "ignore", "silent",
		"stay", "keep", "wait", "solo", "no."]],
]
const CHOICE_ICON_FALLBACK := "itm_idea_napkin"

func _choice_tex(choice: Dictionary) -> Texture2D:
	if choice.has("needs_item"):
		var ti := _tex(String(choice["needs_item"]))
		if ti != null:
			return ti
	if choice.has("needs_role"):
		var tr := _tex("cf_%s_neutral" % String(choice["needs_role"]).to_lower())
		if tr != null:
			return tr
	if choice.has("needs_cash"):
		return _tex("gv/money_2")
	var label := String(choice.get("label", "")).to_lower()
	for pair in CHOICE_ICONS:
		var words: Array = (pair as Array)[1]
		for w in words:
			if label.contains(String(w)):
				var tw := _tex(String((pair as Array)[0]))
				if tw != null:
					return tw
	return _tex(CHOICE_ICON_FALLBACK)

## A LOCKED OPTION MUST READ AS LOCKED. `icon_row` computes the "locked" flag into
## the item dictionary and then never looks at it, and `_select` repaints every
## slot's modulate the moment anything is picked — so the dimming is applied here,
## and re-applied after every pen mark.
func _dim_locked(row: Control, locked: Array) -> void:
	if row == null or not is_instance_valid(row):
		return
	for i in mini(row.get_child_count(), locked.size()):
		if not bool(locked[i]):
			continue
		var slot: Control = row.get_child(i)
		slot.modulate = Color(1, 1, 1, 0.40)   # readable, plainly not yours
		# and never a pen ring around something you cannot have. The drawing and the
		# caption stay; the mark the shell drew on the way past does not.
		for c in slot.get_children():
			if not (c is TextureRect or c is Label):
				c.visible = false

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
	# REDRAW WHATEVER PAGE IS OPEN, never one page index. This waited on `_page_i == 3`,
	# the situation page — but the field the player actually writes their move into is
	# on the DECISION page, page 4. So the verdict came back, `_pending_free` was set,
	# the page was never rebuilt, and the lock button sat on "...decide first" forever:
	# you wrote your move on the page the whole game is built around and nothing
	# happened. The crew page carries a field too. The open page is the one to rebuild.
	generator.adjudicate(state, _current_event, text, func(result: Dictionary):
		_adjudicating = false
		if result.is_empty():
			_pending_free = {}
			if _journal.visible:
				_show_spread()
			return
		result["player_text"] = text
		result["reality"] = result.get("reality_check", "")
		_pending_free = result
		_sfx["deposit"].play()
		if _journal.visible:
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


## ── RUNG 0: THE SPOT-PATCH ROOM ──────────────────────────────────────────
##
## docs/BLANK_SCENES_ARCHITECTURE.md §8. This era's scene ships as a blank plus one
## in-scene rendition per (spot, character), every one of them cut from a NATIVE
## render of that same scene. So the room is assembled by CHOOSING renditions, and
## no foreign sprite is ever laid over a painting it was not lit for — which is the
## defect every path below this one still carries.
##
## NO ENV GATE. Unlike RUNWAY_STAGE, this rung is on by default the moment a scene
## exists on disk, because the factory only ships scenes it has verified: the switch
## IS the directory. RUNWAY_NO_ART still turns it off with everything else.
##
## NO SURFACE MOUNTING THIS ROUND. The patch blanks are new art, not the annotated
## backgrounds SceneSurfaces measured — writing "$82,350" at a stranger's coordinates
## is exactly the failure the assembled path was built to prevent. So _surf_mode
## stays false, _live_surfaces() returns null, and the HUD plates keep the numbers.
##
## Returns false without touching anything when this era ships no scene, so the
## caller falls through to precisely the behaviour it had before this existed.
func _mount_patch_scene() -> bool:
	if state == null:
		return false
	if OS.get_environment("RUNWAY_NO_ART") != "":
		return false
	var era := String(state.era)
	if era == "":
		return false
	var ps := PatchScene.new()
	ps.name = "patch_room"
	ps.mouse_filter = Control.MOUSE_FILTER_IGNORE
	# crew_cast() says "founder" without saying WHICH founder, and the founder's chair
	# ships one rendition per archetype — so the archetype is handed over separately
	# rather than the room quietly seating a stranger in it.
	ps.archetype = String(state.archetype_id)
	_room.add_child(ps)
	_room.move_child(ps, 0)
	var cast := crew_cast()
	if not ps.build(era, cast):
		_room.remove_child(ps)
		ps.queue_free()
		return false
	_patch = ps
	_patch_mode = true
	_cast_key = JSON.stringify(cast)
	# The swap is atomic here too: the old plate, its spots, its cutouts and the
	# sprite crew all go down together, or there are two rooms in the frame.
	_hide_old_stack()
	print("RUNWAY! patch room: %s — %s" % [era, ps.cast_line()])
	return true


## The people in the room are state. A cofounder walks out, a hire arrives, a mood
## burns someone down — the frame must never disagree with the ledger, so the room
## is re-chosen the moment its cast stops matching. Choosing is free (it is a set of
## texture swaps out of the same scene), so this can run every week.
func _refresh_patch_cast() -> void:
	if not _patch_mode or _patch == null or not is_instance_valid(_patch):
		return
	var cast := crew_cast()
	var key := JSON.stringify(cast)
	if key == _cast_key:
		return
	_cast_key = key
	_patch.archetype = String(state.archetype_id)
	if _patch.build(String(state.era), cast):
		print("RUNWAY! patch room re-cast: %s — %s" % [state.era, _patch.cast_line()])
		return
	_mount_scene()          # the room re-lands whole, or the ladder below takes over
