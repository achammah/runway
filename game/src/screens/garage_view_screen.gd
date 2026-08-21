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
signal week_committing            # the lock was pressed: drop the curtain NOW
signal week_rolled(d20: int)      # the die is cast; the ceremony shows it tumble

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
var _turn_dir := 0                 # the pending page-turn: +1 forward, -1 back, 0 none
var _binder: Binder                # the operations dashboard, opened with TAB/B
var _lock_ready_last := false      # so typing only rebuilds the lock when readiness flips
var _pending_dice := {}            # {a, b, adv_map} — cast at commit, resolved post-DM
var _world_busy := false           # main holds this true from beat-open to beat-closed
# THE CLARIFY PRE-PASS (owner: luna asks ONE question before the dice when the
# move is missing the number/name/resource that changes the week)
var _clarify := {}                 # {q, kind, base} while a question is on the page
var _clarify_checked := false      # this commit already passed the pre-pass
var _seen_spreads := {}            # "week:page:sheet" -> the ink is already dry
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
	# a resumed run opens on last week's REAL story, not an amnesiac quiet-week
	if _last_outcome.is_empty() and not state.last_outcome.is_empty():
		_last_outcome = state.last_outcome.duplicate(true)

func _unhandled_key_input(ev: InputEvent) -> void:
	if ev is InputEventKey and ev.pressed and ev.keycode in [KEY_TAB, KEY_B]:
		if _binder != null and is_instance_valid(_binder):
			return   # the binder handles its own dismissal
		_open_binder()

func _open_binder() -> void:
	_binder = Binder.new()
	_binder.setup(state)
	add_child(_binder)
	_binder.size = Vector2(1536, 1024)
	_sfx["card_flip"].play()

func _ready() -> void:
	_font = load("res://assets/fonts/PatrickHand-Regular.ttf")
	_font_d = load("res://assets/fonts/Baloo2-Bold.ttf")
	_last_cash = state.cash
	for n in ["card_flip", "cash", "death", "win", "tick", "deposit", "lock_week"]:
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
	# the binder's doorway: a smaller drawn tab beside the journal button
	var bb := Button.new()
	bb.text = "THE BINDER (TAB)"
	bb.position = Vector2(1272, 936)
	bb.size = Vector2(240, 56)
	bb.pivot_offset = Vector2(120, 28)
	_style_button(bb, PALETTE["sage"], 24)
	bb.pressed.connect(_open_binder)
	add_child(bb)

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
## THE STATUS BOARD LIVES IN THE PICTURE (owner design). Every V2-generated
## scene carries two blank surfaces AT CONTRACT POSITIONS — whiteboard in the
## upper-left quarter, pinned sheet in the upper-right — so the game writes the
## week\'s numbers straight onto the room\'s own furniture in the founder\'s hand.
var _contract_ink: Control

## THE SCOUT (owner: "maybe we should have a YOLO to identify boxes that are
## empty"): instead of trusting the contract positions, SCAN the generated image
## for its actual blank surfaces — large bright low-saturation rectangles — and
## write the numbers onto those. Calibrated offline on real renders: brightness
## >207, channel spread <34, solidity >=0.66, at a 192px scan width.
func _scout_blanks(tex: Texture2D) -> Array:
	var img := tex.get_image()
	if img == null:
		return []
	var W := img.get_width()
	var H := img.get_height()
	var sw := 192
	var sh := int(192.0 * float(H) / float(W))
	var small: Image = img.duplicate()
	small.resize(sw, sh, Image.INTERPOLATE_BILINEAR)
	var seen := {}
	var rects: Array = []
	for y0 in sh:
		for x0 in sw:
			var k0 := y0 * sw + x0
			if seen.has(k0):
				continue
			var c0: Color = small.get_pixel(x0, y0)
			if not _blankish(c0):
				continue
			var stack: Array = [Vector2i(x0, y0)]
			seen[k0] = true
			var cells: Array = []
			while not stack.is_empty():
				var p: Vector2i = stack.pop_back()
				cells.append(p)
				for d in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
					var q: Vector2i = p + d
					if q.x < 0 or q.y < 0 or q.x >= sw or q.y >= sh:
						continue
					var kq := q.y * sw + q.x
					if seen.has(kq):
						continue
					if _blankish(small.get_pixel(q.x, q.y)):
						seen[kq] = true
						stack.append(q)
			if cells.size() < 130:
				continue
			var xmin := sw; var xmax := 0; var ymin := sh; var ymax := 0
			for p2 in cells:
				xmin = mini(xmin, (p2 as Vector2i).x); xmax = maxi(xmax, (p2 as Vector2i).x)
				ymin = mini(ymin, (p2 as Vector2i).y); ymax = maxi(ymax, (p2 as Vector2i).y)
			var bw := xmax - xmin + 1
			var bh := ymax - ymin + 1
			if float(cells.size()) / float(bw * bh) < 0.66 or bw < 16 or bh < 11 or bh > bw * 2.2:
				continue
			rects.append({"r": Rect2(float(xmin) * W / sw, float(ymin) * H / sh,
					float(bw) * W / sw, float(bh) * H / sh), "n": cells.size()})
	rects.sort_custom(func(a, b): return int(a["n"]) > int(b["n"]))
	var out: Array = []
	for rr in rects.slice(0, 2):
		# map from image space to the 1536x1024 room space
		var r: Rect2 = rr["r"]
		out.append(Rect2(r.position.x * 1536.0 / W, r.position.y * 1024.0 / H,
				r.size.x * 1536.0 / W, r.size.y * 1024.0 / H))
	return out

## The surface's own lean: scan two columns of the region for the first
## blank pixel from the top — the line between those hits is the edge the
## drawn board actually has, and the ink lies down along it (owner: "there
## should be tilt and all"). Clamped: a wild estimate reads worse than flat.
func _region_tilt(tex: Texture2D, room_r: Rect2) -> float:
	if tex == null:
		return 0.0
	var img := tex.get_image()
	if img == null:
		return 0.0
	var W := img.get_width()
	var H := img.get_height()
	var r := Rect2(room_r.position.x * W / 1536.0, room_r.position.y * H / 1024.0,
			room_r.size.x * W / 1536.0, room_r.size.y * H / 1024.0)
	var xl := int(r.position.x + r.size.x * 0.22)
	var xr := int(r.position.x + r.size.x * 0.78)
	var y_top := maxi(int(r.position.y - r.size.y * 0.35), 0)
	var y_stop := mini(int(r.position.y + r.size.y * 0.7), H - 1)
	var yl := -1
	var yr := -1
	for y in range(y_top, y_stop):
		if yl < 0 and _blankish(img.get_pixel(mini(xl, W - 1), y)):
			yl = y
		if yr < 0 and _blankish(img.get_pixel(mini(xr, W - 1), y)):
			yr = y
		if yl >= 0 and yr >= 0:
			break
	if yl < 0 or yr < 0:
		return 0.0
	return clampf(atan2(float(yr - yl), float(xr - xl)), -0.09, 0.09)

func _blankish(c: Color) -> bool:
	var mx := maxf(c.r, maxf(c.g, c.b))
	var mn := minf(c.r, minf(c.g, c.b))
	return mx > 0.86 and (mx - mn) < 0.09

func _mark_contract_surfaces(on: bool, tex: Texture2D = null) -> void:
	if _contract_ink != null and is_instance_valid(_contract_ink):
		_contract_ink.queue_free()
		_contract_ink = null
	if not on:
		return
	var ink := Control.new()
	ink.mouse_filter = Control.MOUSE_FILTER_IGNORE
	ink.size = Vector2(1536, 1024)
	# scout the REAL blank surfaces; fall back to the contract zones when the
	# picture kept none big enough
	# HONEST SURFACES ONLY (owner): ink lands exclusively on scouted blank
	# regions. One region found -> both blocks share it stacked. None found ->
	# NO ink on the scene at all; the HUD chip and the binder carry the numbers.
	var found: Array = _scout_blanks(tex) if tex != null else []
	if found.is_empty():
		ink.queue_free()
		_contract_ink = null
		return
	var money_r: Rect2 = found[0]
	var sheet_r: Rect2
	var stacked := false
	if found.size() > 1:
		sheet_r = found[1]
		if sheet_r.position.x < money_r.position.x:
			var tmp := money_r
			money_r = sheet_r
			sheet_r = tmp
	else:
		stacked = true
		sheet_r = Rect2(money_r.position + Vector2(money_r.size.x * 0.1, money_r.size.y * 0.55),
				Vector2(money_r.size.x * 0.8, money_r.size.y * 0.4))
		money_r = Rect2(money_r.position + Vector2(money_r.size.x * 0.1, money_r.size.y * 0.08),
				Vector2(money_r.size.x * 0.8, money_r.size.y * 0.45))
	var net := state.burn_per_week()
	var weeks := 999 if net <= 0 else maxi(0, int(floor(float(state.cash) / float(net))))
	var msz := clampi(mini(int(money_r.size.x / 5.5), int(money_r.size.y / 3.2)), 22, 50)
	var wb := [
		["$%s" % _fmt(state.cash), msz, Color("1E1E1E")],
		[("%d weeks left" % weeks) if weeks < 999 else "cash positive", int(msz * 0.62), Color("E86A5C")],
	]
	var money_tilt := _region_tilt(tex, money_r)
	var y := money_r.position.y + money_r.size.y * 0.22
	for row in wb:
		if y + float(row[1]) * 1.2 > money_r.position.y + money_r.size.y:
			break   # the board is full; a spilled line reads as a bug
		var l := Label.new()
		l.add_theme_font_override("font", _font)
		l.add_theme_font_size_override("font_size", int(row[1]))
		l.add_theme_color_override("font_color", row[2])
		l.text = String(row[0])
		l.position = Vector2(money_r.position.x + money_r.size.x * 0.12, y)
		l.rotation = money_tilt
		l.mouse_filter = Control.MOUSE_FILTER_IGNORE
		ink.add_child(l)
		y += float(row[1]) * 1.35
	var ssz := clampi(mini(int(sheet_r.size.x / 9.0), int(sheet_r.size.y / 4.2)), 18, 30)
	var sheet := [
		"%d customers" % state.traction,
		"%d on payroll" % (state.cofounders.size() + state.employees.size()),
		"%d%% yours" % int(state.founder_pct),
	]
	var sheet_tilt := _region_tilt(tex, sheet_r) if not stacked else _region_tilt(tex, money_r)
	var sy := sheet_r.position.y + sheet_r.size.y * 0.18
	for t in sheet:
		if sy + float(ssz) * 1.15 > sheet_r.position.y + sheet_r.size.y:
			break   # drop trailing lines rather than write past the paper
		var l2 := Label.new()
		l2.add_theme_font_override("font", _font)
		l2.add_theme_font_size_override("font_size", ssz)
		l2.add_theme_color_override("font_color", Color("1E1E1E"))
		l2.text = String(t)
		l2.position = Vector2(sheet_r.position.x + sheet_r.size.x * 0.12, sy)
		l2.rotation = sheet_tilt
		l2.mouse_filter = Control.MOUSE_FILTER_IGNORE
		ink.add_child(l2)
		sy += float(ssz) * 1.32
	_room.add_child(ink)
	_contract_ink = ink

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
	_mark_contract_surfaces(path.contains("gen_scenes_v2"), tex)
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
	# a V2 scene already carries the money and the equity ON ITS OWN WALLS (the
	# contract ink) — the chip repeating them was the one redundant UI in shot.
	# But if the scout found NO honest surface, the ink node has no children and
	# the chip keeps the job.
	if _contract_ink != null and is_instance_valid(_contract_ink) 			and _contract_ink.get_child_count() > 0:
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
	# THE PAD COMES UP OFF THE DESK (owner: "a nice normal animation to open"):
	# it rises from below with a slight straightening, the dim fades with it
	_j_page.position.y = 90.0
	_j_page.rotation = 0.012
	_j_page.modulate.a = 0.0
	dim.modulate.a = 0.0
	var otw := create_tween().set_parallel(true)
	otw.tween_property(_j_page, "position:y", 0.0, 0.28).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	otw.tween_property(_j_page, "rotation", 0.0, 0.28).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	otw.tween_property(_j_page, "modulate:a", 1.0, 0.2)
	otw.tween_property(dim, "modulate:a", 1.0, 0.24)

func _show_spread() -> void:
	# THE TURN IS PHYSICAL. On an arrow press the outgoing page keeps its sheet,
	# loses its room (the incoming page shows the same room, so the world holds
	# still), rides ON TOP of the new page and slides away while the new sheet
	# lands from the side you are heading. Any other rebuild swaps instantly.
	var old := _jp
	if old != null and is_instance_valid(old) and _turn_dir != 0:
		old.exit_turn(_turn_dir)
	else:
		for c in _j_page.get_children():
			_j_page.remove_child(c)
			c.queue_free()
	_sfx["card_flip"].play()
	_refresh_scene()
	var pg := JournalPage.new()
	_jp = pg
	_j_page.add_child(pg)
	if old != null and is_instance_valid(old) and _turn_dir != 0:
		_j_page.move_child(old, _j_page.get_child_count() - 1)
	# A page you have already read is DRY INK. Only a spread's first showing
	# performs the writing; turning back (or forward again) opens a written page,
	# because a log book that rewrote itself on every glance would be a screen,
	# not a book.
	var spread_key := "%d:%d" % [state.week, _page_i]
	pg.instant = _seen_spreads.has(spread_key)
	pg.backdrop_path = _composed_path
	var first_open := not _seen_spreads.has(spread_key) and _turn_dir == 0
	_seen_spreads[spread_key] = true
	pg.build("WEEK %d" % state.week, _scene_id)
	if first_open:
		# opening the book is a gesture too: the sheet settles in from the right
		pg.enter_turn(1)
	pg.prev_page.connect(func():
		if _page_i == 0:
			_close_journal()
			return
		_page_i -= 1
		_turn_dir = -1
		_show_spread())
	pg.next_page.connect(func():
		_page_i = mini(_page_i + 1, 1)
		_turn_dir = 1
		_show_spread())
	pg.written.connect(func(t):
		_free_text[_page_i] = t
		var now_ready: bool = String(t).strip_edges() != "" or not _pending_free.is_empty()
		if now_ready != _lock_ready_last and _page_i == 1:
			_lock_button())
	# THE 60-SECOND WEEK (owner redesign): the book holds exactly TWO spreads.
	# Spread 0 — THE WEEK THAT WAS: the world's reply, the deltas, the crew. Read only.
	# Spread 1 — THE WEEK AHEAD: the situation, a few chips, the big written move,
	# and the only commit in the loop. Everything the five old pages asked for in
	# widgets, the founder now simply WRITES, and the world adjudicates.
	match _page_i:
		0:
			_spread_was()
		1:
			_spread_ahead()
	pg.arrows(_page_i > 0, _page_i < 1)
	if _turn_dir != 0:
		pg.enter_turn(_turn_dir)
		_turn_dir = 0

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

func _story_lines() -> Array:
	var out: Array = []
	var said := String(_last_outcome.get("said", "")).strip_edges()
	var heard := String(_last_outcome.get("heard", "")).strip_edges()
	var verdict := String(_last_outcome.get("verdict", "")).strip_edges()
	var narration := String(_last_outcome.get("narration", "")).strip_edges()
	var reality := String(_last_outcome.get("reality", "")).strip_edges()
	# said/heard live on the READING BEAT; the page is the diary and skips them
	if verdict != "":
		out.append(["The world called it %s." % verdict.to_lower(), false, false])
	if narration != "":
		out.append([narration, false, true])
	if reality != "":
		out.append([reality, true, true])
	# THE RECEIPTS (owner: every impact with its reasoning): each effect prints
	# with its why — "+$1,200 — the pilot invoice cleared".
	for eff in _last_outcome.get("dm", {}).get("effects", []):
		var d: Dictionary = eff
		var op := String(d.get("op", ""))
		var why := String(d.get("why", ""))
		if why == "" or op == "set_flag":
			continue
		var v := int(d.get("v", 0))
		var label: String = {"cash_delta": "$", "product_delta": "product ", "traction_delta": "customers ",
			"morale_delta": "morale ", "hype_delta": "hype "}.get(op, "")
		var amt := ("+" if v >= 0 else "−") + (("$" + _fmt(absi(v))) if op == "cash_delta" else str(absi(v)))
		out.append(["   %s %s — %s" % [amt, "" if op == "cash_delta" else String(label).strip_edges(), why], false, true])
	return out

func _spread_was() -> void:
	# THE PAGE IS BUDGETED BACKWARDS: the two strips get ~310px above the fence,
	# the annotations two rules, and the narration is trimmed to whatever remains.
	# The first cut of this page did none of that and the numbers fell off the
	# paper curl — the exact class of defect this book exists to never show.
	var has_crew := _crew_faces().size() > 1
	var strips_h: float = 150.0 + (130.0 if has_crew else 0.0)
	var lines := _story_lines()
	var vt := String(_last_outcome.get("verdict", ""))
	# THE PAGE IS THE DIARY, NOT THE CHAPTER (owner): the beat already read the
	# narration; the log book prints the DM's journal_note — the founder's own
	# scribble — and never replays the beat's text.
	var dmd0: Dictionary = _last_outcome.get("dm", {})
	var note := String(dmd0.get("journal_note", "")).strip_edges()
	if lines.is_empty() and note == "":
		_jp.line("Week one. Nothing has happened to you yet. After this, everything that does is yours."
			if state.week <= 1
			else "A quiet week. The rent noticed it anyway.")
	else:
		var narr := ""
		var shorts: Array = []
		for l in lines:
			if bool((l as Array)[2]) and narr == "":
				narr = String((l as Array)[0])
			else:
				shorts.append(l)
		var short_h: float = 102.0 if shorts.size() > 0 else 0.0
		# the DM titles every week; the log keeps the headline the beat announced
		var hl := String(dmd0.get("headline", ""))
		if hl != "":
			_jp.line(hl)
		if note != "":
			_jp.line_fitted(note, strips_h + short_h + 24.0)
		elif narr != "":
			_jp.line_fitted(narr, strips_h + short_h + 24.0)
			if vt in ["brilliant", "backfired"]:
				_jp.margin_mark("star" if vt == "brilliant" else "cross")
			# nat 20 / nat 1: the die itself gets its stamp, computed the same
			# deterministic way the engine resolved it
			var dmd: Dictionary = _last_outcome.get("dm", {})
			var dice_d: Dictionary = dmd.get("dice", {})
			var roll_d: Dictionary = dmd.get("roll", {})
			if not dice_d.is_empty() and not roll_d.is_empty():
				var stt := String(roll_d.get("stat", "grit"))
				var mode := String((dice_d.get("adv_map", {}) as Dictionary).get(stt, ""))
				var a := int(dice_d.get("a", 10))
				var b := int(dice_d.get("b", 10))
				var used := a
				if mode.begins_with("ADV"):
					used = maxi(a, b)
				elif mode.begins_with("DIS"):
					used = mini(a, b)
				if used == 20:
					_jp.line("Rolled a natural 20. Some weeks the universe pays for lunch.", false)
				elif used == 1:
					_jp.line("Rolled a 1. Everything that could go sideways did.", false)
		# ONE annotation, full ink. Faint text under a printed rule read as
		# struck-through; the margin mark already carries the judgement.
		if not shorts.is_empty():
			_jp.line_fitted(String((shorts[0] as Array)[0]), strips_h)
	_delta_strip()
	_crew_strip()

## The week's numbers as drawings, not sentences: the jar of runway, the build,
## the crowd. Values live in one-line captions; nothing is pressable.
func _delta_strip() -> void:
	if _jp.room_to_fence("ending") < 210.0:
		if _jp.room_to_fence("ending") >= 60.0:
			_jp.line("$%s · v0.%d · %d customers" % [_fmt(state.cash), state.product, state.traction], false, "ending")
		return
	var net := state.burn_per_week()
	var weeks := 999 if net <= 0 else maxi(0, int(floor(float(state.cash) / float(net))))
	var runway_txt := ("%d wks" % weeks) if weeks < 999 else "gaining"
	var chips := [
		{"id": "cash", "tex": _jicon("cash", "itm_savings_jar"),
			"text": "$%s%s\n%s" % [_fmt(state.cash), _chg("cash", state.cash, true), runway_txt]},
		{"id": "prod", "tex": _jicon("product", "itm_laptop"),
			"text": "v0.%d%s" % [state.product, _chg("product", state.product)]},
		{"id": "cust", "tex": _jicon("customers", "gv/chart_1"),
			"text": "%d customers%s" % [state.traction, _chg("traction", state.traction)]},
		{"id": "mood", "tex": _jicon("morale", "itm_energy_drinks"),
			"text": "%s%s" % ["fine" if state.morale > 65 else ("fraying" if state.morale > 35 else "cooked"), _chg("morale", state.morale)]},
	]
	var row := _jp.icon_row(chips, Vector2(124, 116), "ending")
	for slot in row.get_children():
		(slot as Control).mouse_filter = Control.MOUSE_FILTER_IGNORE

## The journal's OWN drawings first (doodles a founder would make), the big
## art's sprites only until those land. The doodle set is generated + decomposed
## offline into assets/journal_icons/.
func _jicon(name: String, fallback: String) -> Texture2D:
	var p := "res://assets/journal_icons/%s.png" % name
	if ResourceLoader.exists(p):
		var t: Texture2D = load(p)
		if t != null:
			return t
	return _tex(fallback)

## " (+3)" / " (-2)" — what this week DID, next to what IS. Blank when unmoved,
## because a zero delta every week is wallpaper.
func _chg(key: String, now: int, money: bool = false) -> String:
	if not _week_prev.has(key):
		return ""
	var d: int = now - int(_week_prev[key])
	if d == 0:
		return ""
	if money:
		return "  (%s$%s)" % ["+" if d > 0 else "-", _fmt(absi(d))]
	return "  (%+d)" % d

## Who is still here, at a glance: small faces, moods drawn on them, no input.
func _crew_strip() -> void:
	var faces := _crew_faces()
	if faces.size() <= 1 or _jp.room_to_fence("ending") < 190.0:
		return
	var row := _jp.icon_row(faces.slice(0, 5), Vector2(110, 100), "ending")
	for slot in row.get_children():
		(slot as Control).mouse_filter = Control.MOUSE_FILTER_IGNORE

func _crew_faces() -> Array:
	var slugs := {"tech": "technical", "technical": "technical", "business": "business",
		"sales": "business", "hustler": "idea", "the idea friend": "idea", "design": "design"}
	var jslug := {"technical": "cofd_tech", "business": "cofd_business",
		"design": "cofd_design", "idea": "cofd_idea"}
	var faces: Array = []
	faces.append({"id": "you", "tex": _jicon("you", "chr_arch_%s" % state.archetype_id), "text": "you"})
	for i in state.cofounders.size():
		var cf: Dictionary = state.cofounders[i]
		if not cf.has("loyalty"):
			cf["loyalty"] = 70
		var loy := int(cf["loyalty"])
		var mood := "happy" if loy > 70 else ("neutral" if loy > 30 else "resentful")
		var slug: String = slugs.get(String(cf.get("role", "Technical")).to_lower(), "technical")
		var role_l := String(cf.get("role", "?")).to_lower()
		var jname: String = jslug.get(slug, "cofd_tech")
		if role_l.contains("sales"):
			jname = "cofd_sales"
		# the doodle carries identity; the mood still comes through the caption
		var cap := role_l if mood == "happy" else role_l + "\n(" + ("uneasy" if mood == "neutral" else "resentful") + ")"
		faces.append({"id": "cf%d" % i, "tex": _jicon(jname, "cf_%s_%s" % [slug, mood]),
			"text": cap})
	for e in state.employees:
		var bs := GameState.burnout_state(int(e.get("burnout", 0)))
		faces.append({"id": "emp", "tex": _jicon("employee", "cf_technical_%s" % ("resentful" if bs in ["cooked", "gone"] else ("neutral" if bs == "frayed" else "happy"))),
			"text": String(e.get("name", "hire")).to_lower()})
	return faces

## ── SPREAD 1 · THE WEEK AHEAD — the situation, then the pen. Nothing else. ──
## Owner redesign: NO options before the player writes. The page states the
## situation and offers one CLEAN, unmistakable writing area. What you write is
## your move; the world adjudicates it; locking is the only button.
func _spread_ahead() -> void:
	# WHERE YOU STAND, one faint line — the ask is meaningless without it
	var net := state.burn_per_week()
	var weeks := 999 if net <= 0 else maxi(0, int(floor(float(state.cash) / float(net))))
	# full ink on purpose — and COMPACT on purpose: this line must never wrap
	var cash_s := ("$%.0fk" % (float(state.cash) / 1000.0)) if absi(state.cash) >= 10_000 else ("$" + _fmt(state.cash))
	_jp.line("%s · %s · %d cust · v0.%d" % [cash_s,
			("%d wks" % weeks) if weeks < 999 else "cash+",
			state.traction, state.product])
	var situation := ""
	if _current_event.is_empty():
		situation = "Nothing came for you this week. The week is yours."
	else:
		situation = String(_current_event.get("title", "")) + " — " + String(_current_event.get("body", ""))
	# THE TERM SHEETS (plan A2/UI-13): when a raise move opened the round, the
	# three offers sit on the decision page as drawn cards — sign one by tapping,
	# or write anything else and let them expire.
	var special_used := false
	var fr_age := state.week - int(state.get_meta("fundraising_week", state.week))
	if state.has_flag("fundraising_open") and fr_age > 2:
		state.flags.erase("fundraising_open")
		_jp.line("the term sheets expired unsigned.", true)
	if state.has_flag("fundraising_open"):
		special_used = true
		var offers := SimEngine.generate_offers(state, state.investors)
		_jp.line("THE TERM SHEETS ARE ON THE TABLE:")
		# one row of three, captions clipped to wrap-proof width: "Harda 15k/24%"
		var cards: Array = []
		for i in offers.size():
			var o: Dictionary = offers[i]
			var tag := String(o.investor).split(" ")[0].left(7)
			cards.append({"id": "ts:%d" % i, "text": "%s %.0fk/%.0f%%" % [
				tag, float(o.amount) / 1000.0, float(o.equity_pct)]})
		_jp.icon_row(cards, Vector2(230, 40), "body")
		_jp.choice_made.connect(func(id: String):
			if not id.begins_with("ts:"):
				return
			var o2: Dictionary = offers[int(id.substr(3))]
			SimEngine.apply_round(state, int(o2.amount), float(o2.equity_pct))
			state.flags.erase("fundraising_open")
			var ladder_flag := "seed_raised" if state.rounds_raised.size() <= 2 else "series_a"
			state.set_flag(ladder_flag)
			state.log_action("signed %s: $%d for %.1f%%" % [o2.investor, int(o2.amount), float(o2.equity_pct)])
			_sfx["win"].play()
			_lock_button())
	# THE LEVEL-UP (plan B4): a banked milestone point is spent HERE, as a pen
	# circle on the stat of your choice — the D&D moment, on paper.
	if state.xp > state.xp_spent and not special_used:
		special_used = true
		_jp.line("★ You leveled — circle the muscle that grew:")
		var stat_items: Array = []
		for st_n in ["build", "sell", "raise", "recruit", "grit"]:
			# "recruit 3" is the one caption that wraps inside a 110px cell; the
			# sheet says "hire", the sheet's id stays canonical
			stat_items.append({"id": "lv:" + st_n,
				"text": "%s %d" % ["hire" if st_n == "recruit" else st_n,
					int(state.competences.get(st_n, 3))]})
		# caption-only cells: the row is one ruled line tall, not a portrait band
		_jp.icon_row(stat_items, Vector2(110, 42), "body")
		_jp.choice_made.connect(func(id: String):
			if id.begins_with("lv:") and state.xp > state.xp_spent:
				var st2 := id.substr(3)
				state.competences[st2] = mini(int(state.competences.get(st2, 3)) + 1, 5)
				state.xp_spent += 1
				state.log_action("leveled %s to %d" % [st2, int(state.competences[st2])])
				_sfx["win"].play())
	# the field gets FOUR rules of reserved paper plus the ASK LINE — except on
	# a term-sheet week, where the cards ARE the question and the prose yields
	if special_used:
		# the cards/level row already ask the question; the prose stays lean and
		# no extra instruction line is spent — the fence math is exact here.
		# On these squeezed pages the ASK must survive whole: the body carries it,
		# the title is the first casualty (owner: "text is being too much cut").
		var ask := String(_current_event.get("body", "")).strip_edges()
		_jp.line_fitted(ask if ask != "" else situation, _jp.rule_pitch() * 2.0 + 72.0)
	else:
		_jp.line_fitted(situation, _jp.rule_pitch() * 4.0 + 60.0)
		_jp.line("So — what do you do?")
	if _adjudicating:
		_jp.line("the world considers your move...", true, "ending")
		_lock_button()
		return
	if not _clarify.is_empty():
		# THE WORLD ASKS FIRST: the question in coral, then chips (amounts) or
		# a plain answer line — answered, the move re-commits with it bound on
		_jp.line(String(_clarify["q"]), false, "ending")
		if String(_clarify["kind"]) == "amount":
			var ccap := SimEngine.era_spend_cap(state.era)
			var copts: Array = []
			for amt in [ccap / 24, ccap / 6, ccap / 2]:
				var a2 := int(round(float(amt) / 50.0) * 50.0)
				copts.append({"id": "clr:%d" % a2, "text": "$%s" % _fmt(a2)})
			_jp.icon_row(copts, Vector2(130, 42), "ending")
			_jp.choice_made.connect(func(id: String) -> void:
				if id.begins_with("clr:"):
					_answer_clarify("budget: $" + _fmt(int(id.substr(4)))))
		var ce := _jp.write_field("", "ending")
		ce.placeholder_text = "answer, then roll…"
		_wire_clarify(ce)
		_lock_button()
		return
	var te := _jp.write_field("", "ending")
	te.placeholder_text = "write what you actually do…"
	te.text = String(_free_text.get(_page_i, ""))
	_wire_free(te)
	_lock_button()

## THE TELEGRAPH (research: decision-matrix pattern): as the founder writes,
## a faint margin note says how the move READS — governing stat guess, the
## modifier, and any advantage/disadvantage their loadout grants. Never the
## odds, never the DC: proof the sheet matters, mystery intact.
const STAT_SNIFF := {
	"build": ["build", "ship", "code", "fix", "refactor", "feature", "prototype", "debug", "product"],
	"sell": ["sell", "demo", "pitch to customer", "close", "customer", "pricing", "price", "door", "outreach", "market"],
	"raise": ["raise", "investor", "term sheet", "fund", "vc", "angel", "round", "pitch deck"],
	"recruit": ["hire", "recruit", "candidate", "interview", "offer letter", "poach", "team up"],
	"grit": ["push through", "all night", "grind", "survive", "hold", "endure", "keep going", "morale"],
}
var _tele: Label

## The move's governing stat, classified from the text — authoritative for the
## roll, previewed by the telegraph. Unclassifiable moves are GRIT: surviving
## the week on will alone is the default startup verb.
func _sniff_stat(t: String) -> String:
	var low := t.to_lower()
	var best := "grit"
	var best_hits := 0
	for st_n in STAT_SNIFF:
		var hits := 0
		for w in STAT_SNIFF[st_n]:
			if low.contains(String(w)):
				hits += 1
		if hits > best_hits:
			best_hits = hits
			best = st_n
	return best

func _telegraph_setup(te: TextEdit) -> void:
	_tele = Label.new()
	_tele.add_theme_font_override("font", _font)
	_tele.add_theme_font_size_override("font_size", 22)
	_tele.add_theme_color_override("font_color", Color(PALETTE["ink"], 0.45))
	_tele.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_tele.position = te.position + Vector2(4, te.size.y + 26.0)
	_jp.space.add_child(_tele)
	_jp.written.connect(func(t: String) -> void: _telegraph_update(t))
	_telegraph_update(te.text)

func _telegraph_update(t: String) -> void:
	if _tele == null or not is_instance_valid(_tele):
		return
	var low := t.to_lower()
	if low.strip_edges().length() < 8:
		_tele.text = ""
		return
	var best := ""
	var best_hits := 0
	for st_n in STAT_SNIFF:
		var hits := 0
		for w in STAT_SNIFF[st_n]:
			if low.contains(String(w)):
				hits += 1
		if hits > best_hits:
			best_hits = hits
			best = st_n
	if best == "":
		_tele.text = ""
		return
	var mod := int(state.competences.get(best, 3)) - 3
	var cx := SimEngine.roll_context(state, best)
	var badge := ""
	if bool(cx.advantage):
		badge = "  ·  advantage (%s)" % ", ".join(cx.adv_reasons)
	elif bool(cx.disadvantage):
		badge = "  ·  disadvantage (%s)" % ", ".join(cx.dis_reasons)
	_tele.text = "reads as a %s move  ·  %s%d%s" % [best.to_upper(),
		"+" if mod >= 0 else "−", absi(mod), badge]

func _wire_free(te: TextEdit) -> void:
	te.gui_input.connect(func(ev):
		if ev is InputEventKey and ev.pressed and ev.keycode == KEY_ENTER and not ev.shift_pressed:
			te.accept_event()
			var t := te.text.strip_edges()
			if t != "":
				_commit_from_text())

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
	# READY = THE FOUNDER WROTE SOMETHING. The written move is the game's whole
	# interface now; a verdict already in hand also counts (Enter path).
	var ready := (not _pending_free.is_empty()) or _jp.written_text() != ""
	_lock_ready_last = ready
	if _adjudicating:
		ready = false
	var b := Button.new()
	b.set_meta("lock", true)
	b.text = ("the dice are out..." if _adjudicating
			else ("ROLL THE WEEK" if ready else "...decide first"))
	b.add_theme_font_override("font", _font)
	b.add_theme_font_size_override("font_size", 34)
	b.add_theme_color_override("font_color", PALETTE["coral"] if ready else Color(PALETTE["ink"], 0.35))
	b.add_theme_color_override("font_hover_color", PALETTE["coral"])
	b.add_theme_color_override("font_disabled_color", Color(PALETTE["ink"], 0.32))
	for stn in ["normal", "hover", "pressed", "focus", "disabled"]:
		b.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	b.disabled = not ready
	# The commit lives in the CONTROLS BAND — the two rules the fence keeps sacred,
	# same band as the arrows. Anchoring to the ending zone's nominal bottom let a
	# cascaded page slide content underneath the lock line.
	var ly := _jp.writable_bottom() - 54.0
	var sp := _jp.span_at(ly)
	b.position = Vector2(sp.x + (sp.y - sp.x) * 0.5 - 170.0, ly)
	b.custom_minimum_size = Vector2(340, 48)
	b.set_deferred("size", Vector2(340, 48))
	b.pressed.connect(_commit_week.bind(b))
	_jp.space.add_child(b)

## THE COMMIT IS A CEREMONY, one beat long: the pen strikes a line under the
## words, the latch clicks, and only then does the week turn. An instant jump
## made the most consequential click in the game feel like a menu.
func _commit_week(b: Button) -> void:
	if _adjudicating:
		return
	if _sfx.has("lock_week"):
		_sfx["lock_week"].play()
	# under the WORDS, not the button's invisible box — the text is centred in it
	var tw2: float = _font.get_string_size(b.text, HORIZONTAL_ALIGNMENT_LEFT, -1, 34).x
	var stroke := _PenStroke.new()
	stroke.mouse_filter = Control.MOUSE_FILTER_IGNORE
	stroke.position = b.position + Vector2((b.size.x - tw2) * 0.5 - 8.0, b.size.y - 8.0)
	stroke.set_deferred("size", Vector2(tw2 + 16.0, 10.0))
	_jp.space.add_child(stroke)
	var tw := create_tween()
	tw.tween_method(func(p: float) -> void:
		stroke.progress = p
		stroke.queue_redraw(), 0.0, 1.0, 0.14)
	tw.tween_interval(0.10)
	tw.tween_callback(_commit_from_text)

## ONE press does the whole thing: the written move goes to the world, the
## verdict comes back, the week applies, the beat opens. The old flow made the
## player lock twice (once to ask, once to accept) — the 60-second week locks once.
func _commit_from_text() -> void:
	# THE COMMIT GATE: one week in the world at a time. While the previous beat
	# is still open (main holds _world_busy), a press must do nothing — the
	# probe caught a week whose dice rolled and numbers applied while its beat
	# and art were silently swallowed.
	if _adjudicating or _world_busy:
		return
	if not _pending_free.is_empty():
		week_committing.emit()
		_lock_week()
		return
	var t := _jp.written_text() if _jp != null and is_instance_valid(_jp) else ""
	if t == "":
		t = String(_free_text.get(1, "")).strip_edges()
	if t == "":
		_lock_week()
		return
	# ── THE PRE-PASS: one luna question when the move hides its number ──
	if not _clarify_checked and generator != null and generator.llm.enabled():
		_adjudicating = true
		_lock_button()
		generator.clarify(state, _current_event, t, func(cq: Dictionary) -> void:
			_adjudicating = false
			_clarify_checked = true
			if bool(cq.get("needs_clarification", false)) and String(cq.get("question", "")) != "":
				var auto := OS.get_environment("RUNWAY_FULLRUN") != "" \
						or OS.get_environment("RUNWAY_FIRSTFLOW") != ""
				if auto:
					_free_text[1] = t + (" — budget: $1,000" if String(cq.get("kind", "")) == "amount"
							else " — whatever is simplest")
					_commit_from_text()
					return
				_clarify = {"q": String(cq.get("question", "")),
					"kind": String(cq.get("kind", "other")), "base": t}
				_show_spread()
				return
			_commit_from_text())
		return
	_clarify_checked = false
	_adjudicating = true
	_lock_button()   # the button itself answers: "the dice are out..."
	state.log_action("wrote: %s" % t.left(80))
	# THE ROLL (owner design: D&D at the heart of the week). The die is cast HERE,
	# before the world speaks — the DM judges the plan into a DC and narrates the
	# outcome this exact number earned. Same plan, different die, different week.
	# THE DIE IS FINAL AT THE PRESS (owner: the roll happens right away). The
	# engine classifies the move's governing stat from the text itself — the
	# same classifier the telegraph showed the player — applies advantage or
	# disadvantage from state, and the cup pours the TRUE number instantly.
	var stat := _sniff_stat(t)
	var cx := SimEngine.roll_context(state, stat)
	var da: int = rng.roll_d20() if rng != null else (randi() % 20 + 1)
	var db: int = rng.roll_d20() if rng != null else (randi() % 20 + 1)
	var used := da
	var mode := ""
	if bool(cx.advantage):
		used = maxi(da, db)
		mode = "advantage (%s)" % ", ".join(cx.adv_reasons)
	elif bool(cx.disadvantage):
		used = mini(da, db)
		mode = "disadvantage (%s)" % ", ".join(cx.dis_reasons)
	_pending_dice = {"a": da, "b": db, "used": used, "stat": stat, "mode": mode,
		"mod": int(state.competences.get(stat, 3)) - 3}
	print("TURN dice used=%d of (%d,%d) stat=%s %s" % [used, da, db, stat, mode])
	week_rolled.emit(used)
	week_committing.emit()
	generator.adjudicate(state, _current_event, t, func(res: Dictionary):
		_adjudicating = false
		var verdict := res
		if verdict.is_empty():
			verdict = EventGenerator.keyless_adjudication()
		verdict["player_text"] = t
		verdict["dice"] = _pending_dice
		verdict["week_played"] = state.week
		_pending_free = verdict
		for ef in verdict.get("effects", []):
			if String((ef as Dictionary).get("op", "")) == "set_flag" \
					and String((ef as Dictionary).get("v", "")) == "fundraising_open":
				state.set_meta("fundraising_week", state.week)
		_lock_week(), _pending_dice)

func _answer_clarify(ans: String) -> void:
	var base := String(_clarify.get("base", ""))
	_clarify = {}
	_free_text[1] = base + " — " + ans
	_clarify_checked = true
	_commit_from_text()

func _wire_clarify(te: TextEdit) -> void:
	te.gui_input.connect(func(ev: InputEvent) -> void:
		if ev is InputEventKey and ev.pressed and \
				((ev as InputEventKey).keycode == KEY_ENTER or (ev as InputEventKey).keycode == KEY_KP_ENTER):
			var ans := te.text.strip_edges()
			if ans != "":
				_answer_clarify(ans)
			te.accept_event())

## One underline in the founder's pen, drawn left to right at commit.
class _PenStroke:
	extends Control
	var progress := 0.0
	func _draw() -> void:
		if progress <= 0.02:
			return
		var pts := PackedVector2Array()
		var rng := RandomNumberGenerator.new()
		rng.seed = 23
		var n: int = maxi(int(progress * 24.0), 2)
		for i in 24:
			var jitter := rng.randf_range(-1.4, 1.4)
			if i < n:
				pts.append(Vector2(size.x * float(i) / 23.0, 5.0 + jitter))
		draw_polyline(pts, Color("E86A5C"), 4.0, true)

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

var _week_prev := {}               # last week's numbers, for the delta strip

## THE EXTENDED EXECUTOR (plan C2): classic meter ops go through EffectOps'
## clamps; engine ops (status/clock/levers/hire/loan) go through SimEngine —
## typed, clamped, catalog-only. Every op returns a receipt line.
func _apply_dm_effects(effects: Array) -> Array:
	var classic: Array = []
	var out: Array = []
	for eff in effects:
		var d: Dictionary = eff
		var op := String(d.get("op", ""))
		var why := String(d.get("why", ""))
		match op:
			"status":
				var nm := String(d.get("v", ""))
				var wk := int(d.get("weeks", 2))
				if SimEngine.add_status(state, nm, wk):
					out.append("status: %s for %d wks — %s" % [nm, wk, why])
			"clock":
				var cons := String(d.get("v", ""))
				var cw := int(d.get("weeks", 3))
				SimEngine.add_clock(state, cw, cons)
				out.append("⏰ clock set (%d wks): %s" % [cw, cons])
			"set_price":
				state.price_mult = clampf(float(d.get("v", 1.0)), 0.5, 2.0)
				out.append("price set to ×%.2f — %s" % [state.price_mult, why])
			"set_marketing":
				state.marketing_budget = clampi(int(d.get("v", 0)), 0, 50_000)
				out.append("marketing $%d/wk — %s" % [state.marketing_budget, why])
			"hire":
				var role := String(d.get("v", "engineer"))
				var rng_n := RandomNumberGenerator.new()
				rng_n.seed = hash(str(state.sim_seed) + str(state.week) + str(state.pipeline.size()))
				var nm2 := WorldGen.person_name(rng_n)   # hires are people, not brands
				var sal: int = {"engineer": 1500, "sales": 1200, "support": 900,
					"designer": 1100, "ops": 1000}.get(role, 1200)
				state.pipeline.append({"name": nm2, "role": role, "salary": sal, "weeks_in": 0})
				out.append("hired a %s ($%d/wk, onboarding) — %s" % [role, sal, why])
			"take_loan":
				var amt := clampi(int(d.get("v", 10_000)), 1_000, 250_000)
				state.loan_principal += amt
				state.cash += amt
				out.append("bridge loan +$%d at 18%%/wk — %s" % [amt, why])
			"spend":
				# THE MONEY LAW, engine side: the DM names the outlay, the ENGINE
				# decides what cash can actually cover. Era-capped; never below
				# zero — an unaffordable plan simply doesn't get its full spend.
				var want_amt := clampi(int(d.get("v", 0)), 0, SimEngine.era_spend_cap(state.era))
				var can := mini(want_amt, maxi(state.cash, 0))
				if can > 0:
					state.cash -= can
					out.append("spent $%d on %s — %s" % [can, String(d.get("cat", "one_off")), why])
				if can < want_amt:
					out.append("the bank stopped it at $%d (wanted $%d) — money you don't have doesn't spend" % [can, want_amt])
			"set_budget":
				var cat := String(d.get("cat", "marketing"))
				if not state.budgets.has(cat):
					cat = "marketing"
				var wk_amt := clampi(int(d.get("v", 0)), 0, SimEngine.era_spend_cap(state.era))
				state.budgets[cat] = wk_amt
				if cat == "marketing":
					state.marketing_budget = 0   # one source of truth once the ledger takes over
				out.append("%s budget set to $%d/wk — %s" % [cat, wk_amt, why])
			_:
				classic.append(d)
	var clog := EffectOps.apply_all(classic, state)
	for l in clog:
		out.append(l)
	print("DM FX: %s" % "; ".join(PackedStringArray(out)))
	return out

func _apply_lock(work_results: Dictionary) -> void:
	_week_prev = {"cash": state.cash, "traction": state.traction,
		"product": state.product, "morale": state.morale}
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
	# ── THE WORLD ACTS FIRST (plan A1): the hostile weekly tick runs before the
	# founder's move lands, and its receipts open the week's ledger.
	var tick := SimEngine.weekly_tick(state)
	for tl in tick.get("lines", []):
		outcome_log.append(String(tl))
	for ev_l in tick.get("events", []):
		outcome_log.append("⚡ " + String(ev_l))
	for fc in tick.get("fired_clocks", []):
		outcome_log.append("⏰ THE DEADLINE HIT: " + String(fc))
		state.log_action("deadline fired: " + String(fc))
	# the decision itself: a written move that the world judged, or a listed one
	var title := String(_current_event.get("title", "a quiet week"))
	if not _pending_free.is_empty():
		var log := _apply_dm_effects(_pending_free.get("effects", []))
		for l in log:
			outcome_log.append(l)
		record.log_event(state.week, _current_event, "[wrote] " + String(_pending_free.get("player_text", "")), log)
		state.log_action("event '%s' — wrote: %s (%s)" % [title, String(_pending_free.get("player_text", "")).left(60), String(_pending_free.get("verdict", ""))])
		_last_outcome = {"title": title, "verdict": String(_pending_free.get("verdict", "")),
			"said": String(_pending_free.get("player_text", "")),
			"heard": String(_pending_free.get("interpreted_as", "")),
			"narration": String(_pending_free.get("narration", "")),
			"reality": String(_pending_free.get("reality_check", "")),
			"dec_log": log, "log": outcome_log,
			# THE FULL DM PAYLOAD RIDES THE OUTCOME. The one-press commit sets the
			# verdict and locks in the same frame, so a per-frame poll upstream can
			# miss the pending dict entirely — the exact silent failure that kept
			# the whole beat-and-render pipeline dark through a real playthrough.
			# The outcome is the durable place; main consumes `dm` exactly once.
			"dm": _pending_free.duplicate(true)}
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
	# the world never asks the same question twice: remember what was played
	var played_title := String(_current_event.get("title", "")).strip_edges()
	if played_title != "":
		# remember the PEOPLE too — "same question, same Nico Sorel every week"
		# was the failure: the title alone can change while the character loops
		var who := PackedStringArray()
		for cm in (_last_outcome.get("dm", {}) as Dictionary).get("cast", []):
			var nm := String((cm as Dictionary).get("name", "")).strip_edges()
			if nm != "" and not who.has(nm):
				who.append(nm)
		if who.size() > 0:
			played_title += " (with %s)" % ", ".join(who)
		state.played_events.append(played_title)
		if state.played_events.size() > 12:
			state.played_events = state.played_events.slice(state.played_events.size() - 12)
	# whatever branch wrote the week, the save remembers it (minus the one-shot dm)
	state.last_outcome = _last_outcome.duplicate(true)
	state.last_outcome.erase("dm")
	# traits tally + the DM's compacted memory (hard-capped) + milestone XP
	var dmres: Dictionary = _last_outcome.get("dm", {})
	for tr in dmres.get("traits", []):
		state.traits_tally[String(tr)] = int(state.traits_tally.get(String(tr), 0)) + 1
	var mem := String(dmres.get("memory", "")).strip_edges()
	print("DM MEMORY (%d chars) · traits %s" % [mem.length(), str(dmres.get("traits", []))])
	if mem != "":
		var words := mem.split(" ", false)
		if words.size() > 130:
			mem = " ".join(words.slice(0, 130)) + "…"
		state.story_so_far = mem
	for fl in ["launched", "first_revenue", "pmf", "seed_raised", "series_a"]:
		if state.has_flag(fl) and not state.has_flag("xp_" + fl):
			state.set_flag("xp_" + fl)
			state.xp += 1
			outcome_log.append("★ MILESTONE: %s — the founder levels (+1 stat to spend)" % fl)
	# ...and the run's memory grows one week: what was said, what the die did,
	# what it cost — the DM reads this back every week from now on
	var fx: Array = []
	for eff in _last_outcome.get("dm", {}).get("effects", []):
		fx.append("%s %s — %s" % [String((eff as Dictionary).get("op", "")),
			str((eff as Dictionary).get("v", "")), String((eff as Dictionary).get("why", ""))])
	var roll_d: Dictionary = _last_outcome.get("dm", {}).get("roll", {})
	state.run_history.append({"wk": state.week, "said": String(_last_outcome.get("said", "")).left(90),
		"heard": String(_last_outcome.get("heard", "")).left(70),
		"verdict": String(_last_outcome.get("verdict", "")),
		"roll": ("d20=%d vs DC %d (%s)" % [int(_last_outcome.get("dm", {}).get("d20", 0)),
			int(roll_d.get("dc", 0)), String(roll_d.get("stat", ""))]) if not roll_d.is_empty() else "",
		"fx": fx})
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
