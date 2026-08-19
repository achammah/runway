class_name FounderDraftScreen
extends Control
## THE FOUNDER DRAFT — three game screens, not a form.
## 1. CHOOSE YOUR FOUNDER: dark stage, spotlight, the selected founder BIG and
##    animated center-stage, stats panel at the side, roster chips at the bottom,
##    arrow keys or click to switch.
## 2. NAME YOUR STARTUP: arcade-style naming screen.
## 3. THE FOUNDING: the cap-table table — cofounders, money, bag, live donut,
##    and the trap panel wired to the YC founding canon.

signal done(result: Dictionary)

const PALETTE := {
	"cream": Color("F2EAD3"), "ink": Color("1E1E1E"), "coral": Color("E86A5C"),
	"yellow": Color("F4B942"), "sage": Color("8FA582"), "blue": Color("6E8CA0"),
	"night": Color("39434B"),
}
const STAT_NAMES := ["build", "sell", "raise", "recruit", "grit"]
const STAT_LABELS := ["BUILD", "SELL", "RAISE", "RECRUIT", "GRIT"]
const MAX_COFOUNDERS := 4
const ROLES := ["Sales", "Business", "Tech", "Hustler", "The Idea Friend"]
const COMMITMENTS := ["Full-time", "Part-time"]

const NAME_A := ["Snack", "Loop", "Byte", "Nap", "Quill", "Moss", "Pling", "Drift", "Stack", "Fern", "Bolt", "Mono", "Husk", "Pivot", "Blob"]
const NAME_B := ["ly", ".io", "ify", "base", "deck", "nest", "flow", "ora", "ium", "sy", "let", "kit"]
const IDEA_PRE := ["AI", "Blockchain", "Artisanal", "Enterprise", "Vegan", "Quantum", "Subscription", "Voice-first", "B2B", "Peer-to-peer", "Serverless", "Emotional"]
const IDEA_FORM := ["copilot", "marketplace", "superapp", "API", "subscription box", "robot", "assistant", "platform", "dashboard", "wearable"]
const IDEA_FOR := ["compliance", "dog grooming", "funerals", "tax returns", "meal prep", "parking", "therapy", "laundry", "weddings", "napping", "HOAs", "expense reports", "houseplants", "breakups"]

var data: Dictionary = {}
var content_items: Array = []
var _room: SceneRoom
var _surfaces: SceneSurfaces
var _money_strip: Control

var _font: Font
var _font_d: Font
var _pages: Array = []
var _page := 0
var _archs: Array = []
var _sel_i := 0
var _sel_arch: Dictionary = {}
var _sel_fund: Dictionary = {}
var _chips: Array = []
var _hero: TextureRect
var _hero_shadow: Control
var _d_name: Label
var _d_tag: Label
var _d_pips: Control
var _d_cash: Label
var _d_perk: Label
var _cofounders: Array = []
var _cf_list: VBoxContainer
var _add_cf_btn: Button
var _fund_btns: Array = []
var _bag: Array[String] = []
var _bag_btns: Dictionary = {}
var _name_edit: PaperInput
var _idea_edit: PaperInput
var _donut: Control
var _summary: Label
var _launch: Button
var _anim_frames: Dictionary = {}
var _anim_i := 0
var _anim_timer: Timer
var _sfx_click: AudioStreamPlayer
var _hero_base_y := 0.0
var _hero_tween: Tween
var _title_label: Label
var _spot_pool: Control
var _lockin_btn: Button
var _stamping := false
var _crew_row: Control
var _crew_sprites: Array = []
var _recruit_layer: Control
var _money_cards: Array = []
var _money_preview: Label
var _bag_summary: Label
var _slots_label: Label
var _box_anchor: Vector2 = Vector2(1230, 560)
var _bagd_art: TextureRect
var _bagd_name: Label
var _bagd_blurb: Label
var _bagd_cost: Label
var _packed_row: HBoxContainer
var _biz_what := "Software"
var _biz_who := "Consumer"
var _name_witness: TextureRect
var _spinning := false
var _what_chips: Array = []
var _who_chips: Array = []


var _team_stage: Control
var _team_nodes: Array = []
var _founder_mini: TextureRect
var _last_founder_pct := 100.0

func _ready() -> void:
	_font = load("res://assets/fonts/PatrickHand-Regular.ttf")
	_font_d = load("res://assets/fonts/Baloo2-Bold.ttf")
	data = _load_json("res://data/archetypes.json")
	_archs = data.get("archetypes", [])
	_sfx_click = AudioStreamPlayer.new()
	_sfx_click.stream = load("res://assets/sfx/cash.wav")
	add_child(_sfx_click)
	set_anchors_preset(Control.PRESET_FULL_RECT)

	# stage background on every page — can NEVER fall back to a blank screen:
	# night field always, stage art on top if it loads, procedural spotlight if not
	var bg := ColorRect.new()
	bg.color = PALETTE["night"]
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(bg)
	_surfaces = SceneSurfaces.new()
	_surfaces.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_room = SceneRoom.new()
	_room.size = Vector2(1536, 1024)
	_room.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_room)
	if _surfaces.mount(STAGE_SCENE):
		add_child(_surfaces)
	else:
		_surfaces = null
	if not _room.load_scene(STAGE_SCENE):
		_room.queue_free()
		_room = null
		var stage_tex: Texture2D = load("res://assets/env/stage.png") if ResourceLoader.exists("res://assets/env/stage.png") else null
		if stage_tex:
			var stage := TextureRect.new()
			stage.texture = stage_tex
			stage.set_anchors_preset(Control.PRESET_FULL_RECT)
			stage.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
			stage.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
			add_child(stage)
		else:
			var spot := SpotlightFallback.new()
			spot.set_anchors_preset(Control.PRESET_FULL_RECT)
			add_child(spot)
	# preload loop frames (any count — 4 or 48)
	for arch in _archs:
		var aid := String(arch["id"])
		var frames: Array = []
		var i := 1
		while true:
			var p := "res://assets/sprites/chr_loop_%s_%02d.png" % [aid, i]
			if not ResourceLoader.exists(p):
				break
			var tex: Texture2D = load(p)
			if aid == "consultant":
				# the run-video union bbox baked a hard cut line; trim it off
				var at := AtlasTexture.new()
				at.atlas = tex
				at.region = Rect2(16, 2, tex.get_width() - 18, tex.get_height() - 4)
				frames.append(at)
			else:
				frames.append(tex)
			i += 1
		if frames.is_empty():
			var still := "res://assets/sprites/%s.png" % String(arch.get("sprite", ""))
			if ResourceLoader.exists(still):
				frames.append(load(still))
		_anim_frames[aid] = frames

	_pages = [_build_select(), _build_name(), _build_shape_page(), _build_crew_page(), _build_money_page(), _build_bag_page()]
	_show_page(0)
	if not _archs.is_empty():
		_select(0, false)

	_anim_timer = Timer.new()
	_anim_timer.wait_time = 0.32
	_anim_timer.timeout.connect(_tick_anim)
	add_child(_anim_timer)
	_anim_timer.start()

func _fmt_money(v: int) -> String:
	var t := str(absi(v))
	var out := ""
	while t.length() > 3:
		out = "," + t.substr(t.length() - 3) + out
		t = t.substr(0, t.length() - 3)
	return ("-" if v < 0 else "") + t + out

func _load_json(path: String) -> Dictionary:
	var parsed = JSON.parse_string(FileAccess.get_file_as_string(path))
	return parsed if parsed is Dictionary else {}

func _show_page(i: int) -> void:
	_page = i
	for p in _pages.size():
		_pages[p].visible = p == i
	if i == 1 and _name_witness and not _sel_arch.is_empty():
		var sp := "res://assets/sprites/%s.png" % String(_sel_arch.get("sprite", ""))
		if ResourceLoader.exists(sp):
			_name_witness.texture = load(sp)
	if i >= 3:
		_refresh_capline()

# ---------- helpers ----------

func _wrap(l: Label, w: float) -> Label:
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.custom_minimum_size = Vector2(w, 0)
	l.set_deferred("size", Vector2(w, 0))
	return l

func _dlabel(text: String, size: int, color: Color) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_override("font", _font_d)
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", color)
	return l

## Rules are measured and drawn by hand. A hardcoded width underlines half a
## title; a rule set at the cap-height strikes through its own descenders.
func _rule_under(page: Control, text: String, size: int, at: Vector2) -> void:
	var w: float = _font_d.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, size).x
	var r := HandRule.new()
	r.length = w
	r.color = PALETTE["coral"]
	r.size = Vector2(w, 14)
	r.position = at + Vector2(2, size * 1.48)
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	page.add_child(r)

func _ink_outline(l: Label, px: int = 8) -> Label:
	l.add_theme_color_override("font_outline_color", PALETTE["ink"])
	l.add_theme_constant_override("outline_size", px)
	return l

func _label(text: String, size: int, color: Color) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_override("font", _font)
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", color)
	return l

func _style_button(b: Button, col: Color, fsize: int) -> void:
	b.add_theme_font_override("font", _font_d)
	b.add_theme_font_size_override("font_size", fsize)
	b.add_theme_color_override("font_color", PALETTE["ink"])
	b.add_theme_color_override("font_disabled_color", Color(PALETTE["ink"], 0.45))
	var st := StyleBoxFlat.new()
	st.bg_color = PALETTE["cream"]
	st.border_color = PALETTE["ink"]
	st.set_border_width_all(4)
	st.set_corner_radius_all(0)
	st.content_margin_left = 14
	st.content_margin_right = 14
	b.add_theme_stylebox_override("normal", st)
	var sh := st.duplicate()
	sh.bg_color = col
	b.add_theme_stylebox_override("hover", sh)
	b.add_theme_stylebox_override("pressed", sh)
	var sd := st.duplicate()
	sd.bg_color = Color(0.90, 0.88, 0.82)
	sd.border_color = Color(0.6, 0.58, 0.52)
	b.add_theme_stylebox_override("disabled", sd)

func _juice(b: Button) -> void:
	b.pivot_offset = b.size / 2.0
	b.mouse_entered.connect(func():
		var t := create_tween()
		t.tween_property(b, "scale", Vector2(1.045, 1.045), 0.08).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT))
	b.mouse_exited.connect(func():
		var t := create_tween()
		t.tween_property(b, "scale", Vector2.ONE, 0.1))

## strips a card back to bare paper: the fill and the border are drawn, not styled
func _paper_card(b: Button) -> void:
	for st_name in ["normal", "hover", "pressed", "disabled", "focus"]:
		var sb := StyleBoxFlat.new()
		sb.bg_color = Color(0, 0, 0, 0)
		sb.set_border_width_all(0)
		sb.set_corner_radius_all(0)
		b.add_theme_stylebox_override(st_name, sb)
	var e := PaperEdge.new()
	e.name = "edge"
	e.size = b.size
	e.lean = int(b.position.x) % 5
	e.mouse_filter = Control.MOUSE_FILTER_IGNORE
	b.add_child(e)
	b.move_child(e, 0)
	# A Control paints its own content FIRST and its children paint over it, so
	# the opaque paper drawn by the edge blanks the Button's own label. Its text
	# is re-issued as a child ABOVE the paper and the Button's copy is cleared.
	# move_child only orders siblings — it cannot put the edge behind its parent.
	if b.text != "":
		var cap := Label.new()
		cap.name = "cap"
		cap.text = b.text
		cap.add_theme_font_override("font", _font_d)
		var fs := b.get_theme_font_size("font_size")
		cap.add_theme_font_size_override("font_size", fs if fs > 0 else 30)
		cap.add_theme_color_override("font_color", PALETTE["ink"])
		cap.size = b.size
		cap.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		cap.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		cap.mouse_filter = Control.MOUSE_FILTER_IGNORE
		b.add_child(cap)
		b.text = ""

## A paper button's label lives in a child Label, so its text is set here rather
## than on the Button, which no longer draws its own.
func _set_button_text(b: Button, text: String) -> void:
	var cap := b.get_node_or_null("cap")
	if cap is Label:
		cap.text = text
	else:
		b.text = text

func _panel(pos: Vector2, sz: Vector2, bg_col: Color = Color("1E1E1E"), border: Color = Color("F2EAD3")) -> Panel:
	var p := Panel.new()
	p.position = pos
	p.size = sz
	var st := StyleBoxFlat.new()
	st.bg_color = bg_col
	st.border_color = border
	st.set_border_width_all(4)
	st.set_corner_radius_all(16)
	p.add_theme_stylebox_override("panel", st)
	return p

# ---------- SCREEN 1: CHOOSE YOUR FOUNDER ----------

func _build_select() -> Control:
	var page := Control.new()
	page.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(page)

	_title_label = _ink_outline(_dlabel("CHOOSE YOUR FOUNDER", 58, PALETTE["cream"]))
	_title_label.position = Vector2(60, 28)
	_title_label.pivot_offset = Vector2(320, 40)
	page.add_child(_title_label)
	_rule_under(page, "CHOOSE YOUR FOUNDER", 58, Vector2(60, 28))

	# hero: the selected founder, big, in the spotlight
	_hero_shadow = EllipseShadow.new()
	_hero_shadow.position = Vector2(650, 776)
	_hero_shadow.size = Vector2(280, 44)
	page.add_child(_hero_shadow)
	_hero = TextureRect.new()
	_hero.position = Vector2(560, 268)
	_hero.size = Vector2(460, 520)
	_hero.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_hero.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	_hero.pivot_offset = Vector2(230, 520)
	page.add_child(_hero)
	_hero_base_y = _hero.position.y

	# stat sheet: open, borderless, BIG type — readable from the couch
	var panel := Control.new()
	panel.position = Vector2(936, 100)
	panel.size = Vector2(540, minf(740.0, SHEET_BOTTOM_MAX - 100.0))
	panel.rotation = -0.008
	page.add_child(panel)
	var sheet := PaperEdge.new()
	sheet.size = panel.size
	sheet.thick = 4.0
	sheet.lean = 1
	sheet.mouse_filter = Control.MOUSE_FILTER_IGNORE
	panel.add_child(sheet)
	_d_name = _dlabel("", 46, PALETTE["ink"])
	_d_name.position = Vector2(44, 28)
	panel.add_child(_d_name)
	_d_tag = _label("", 27, Color(PALETTE["ink"], 0.9))
	_d_tag.position = Vector2(44, 96)
	_d_tag.size = Vector2(470, 70)
	_d_tag.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	panel.add_child(_d_tag)
	var rule2 := HandRule.new()
	rule2.length = 140.0
	rule2.color = PALETTE["coral"]
	rule2.size = Vector2(140, 14)
	rule2.position = Vector2(44, 168)
	rule2.mouse_filter = Control.MOUSE_FILTER_IGNORE
	panel.add_child(rule2)
	_d_pips = StatPips.new()
	_d_pips.position = Vector2(44, 196)
	_d_pips.size = Vector2(470, 290)
	panel.add_child(_d_pips)
	var cash_cap := _label("IN THE BANK, DAY ONE", 24, Color(PALETTE["ink"], 0.6))
	cash_cap.position = Vector2(44, 506)
	panel.add_child(cash_cap)
	_d_cash = _dlabel("", 42, PALETTE["ink"])
	_d_cash.position = Vector2(44, 534)
	panel.add_child(_d_cash)
	var perk_cap := _label("PERK", 24, Color(PALETTE["ink"], 0.6))
	perk_cap.position = Vector2(44, 610)
	panel.add_child(perk_cap)
	_d_perk = _label("", 28, PALETTE["ink"])
	_d_perk.position = Vector2(44, 640)
	_d_perk.size = Vector2(470, 90)
	_d_perk.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	panel.add_child(_d_perk)

	# roster: fighting-game dock at the bottom
	var row_w := _archs.size() * 134 - 14
	var x0 := (1536 - row_w) / 2.0
	var dock := PaperEdge.new()
	dock.size = Vector2(row_w + 48, 150)
	dock.position = Vector2(x0 - 24, DOCK_BAND_TOP)
	dock.thick = 4.0
	dock.lean = 3
	dock.mouse_filter = Control.MOUSE_FILTER_IGNORE
	page.add_child(dock)
	for i in _archs.size():
		var chip := Button.new()
		chip.position = Vector2(x0 + i * 134, DOCK_BAND_TOP + 14.0)
		chip.size = Vector2(120, 120)
		chip.pivot_offset = Vector2(60, 120)
		_style_button(chip, PALETTE["yellow"], 16)
		var still := "res://assets/sprites/%s.png" % String(_archs[i].get("sprite", ""))
		if ResourceLoader.exists(still):
			chip.icon = load(still)
			chip.expand_icon = true
		chip.pressed.connect(_select.bind(i))
		chip.mouse_entered.connect(func():
			if _chips.find(chip) != _sel_i:
				chip.position.y = DOCK_BAND_TOP + 6.0)
		chip.mouse_exited.connect(func():
			if _chips.find(chip) != _sel_i:
				chip.position.y = DOCK_BAND_TOP + 14.0)
		page.add_child(chip)
		_chips.append(chip)
	# dust motes drifting up through the spotlight beam
	var mote_rng := RandomNumberGenerator.new()
	mote_rng.seed = 12
	for i in 14:
		var mote := ColorRect.new()
		var msz := mote_rng.randf_range(2.0, 4.5)
		mote.size = Vector2(msz, msz)
		mote.color = Color(PALETTE["cream"], 0.0)
		mote.rotation = 0.6
		var mx := mote_rng.randf_range(600.0, 990.0)
		var my := mote_rng.randf_range(300.0, 840.0)
		mote.position = Vector2(mx, my)
		mote.mouse_filter = Control.MOUSE_FILTER_IGNORE
		page.add_child(mote)
		var dur := mote_rng.randf_range(5.0, 9.0)
		var mt := create_tween().set_loops()
		mt.tween_property(mote, "color:a", mote_rng.randf_range(0.18, 0.4), dur * 0.3)
		mt.parallel().tween_property(mote, "position:y", my - mote_rng.randf_range(80.0, 150.0), dur)
		mt.tween_property(mote, "color:a", 0.0, dur * 0.25)
		mt.tween_callback(func(): mote.position = Vector2(mx, my))

	_lockin_btn = Button.new()
	_lockin_btn.text = "LOCK IN  →"
	_lockin_btn.position = Vector2(1230, 880)
	_lockin_btn.size = Vector2(260, 84)
	_lockin_btn.pivot_offset = Vector2(130, 42)
	_style_button(_lockin_btn, PALETTE["coral"], 36)
	_lockin_btn.pressed.connect(_lock_in)
	page.add_child(_lockin_btn)
	return page

func _select(i: int, animate_swap: bool = true) -> void:
	var prev := _sel_i
	_sel_i = wrapi(i, 0, _archs.size())
	_sel_arch = _archs[_sel_i]
	_sfx_click.pitch_scale = 0.9 + 0.08 * _sel_i
	_sfx_click.play()
	for c in _chips.size():
		var chip: Button = _chips[c]
		var selected := c == _sel_i
		chip.modulate = Color.WHITE if selected else Color(0.62, 0.62, 0.62, 1.0)
		var target_y := 842.0 if selected else 862.0
		var ct := create_tween()
		ct.tween_property(chip, "position:y", target_y, 0.12).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	# stats: name stamps in, radar sweeps open
	_d_name.text = String(_sel_arch["name"])
	_d_name.pivot_offset = Vector2(60, 22)
	_d_name.scale = Vector2(1.5, 1.5)
	_d_name.rotation = -0.06
	var nt := create_tween()
	nt.tween_property(_d_name, "scale", Vector2.ONE, 0.16).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	nt.parallel().tween_property(_d_name, "rotation", 0.0, 0.16)
	_d_tag.text = String(_sel_arch["tagline"])
	var pips := _d_pips as StatPips
	pips.set_stats(_sel_arch["stats"])
	pips.progress = 0.0
	var rt := create_tween()
	rt.tween_method(func(v): pips.progress = v; pips.queue_redraw(), 0.0, 1.0, 0.5).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	var cash_total := 8000 + int(_sel_arch.get("start_cash_bonus", 0))
	_d_cash.text = "$%s" % _fmt_money(cash_total)
	_d_cash.add_theme_color_override("font_color", PALETTE["sage"] if cash_total >= 8000 else PALETTE["coral"])
	_d_perk.text = "★ " + String(_sel_arch.get("perk", ""))
	# hero swap: old slides off, new lands with squash
	var frames: Array = _anim_frames.get(String(_sel_arch["id"]), [])
	if _anim_timer:
		_anim_timer.wait_time = clampf(2.0 / maxf(1.0, float(frames.size())), 1.0 / 30.0, 0.5)
	if _hero_tween and _hero_tween.is_valid():
		_hero_tween.kill()
	var from_right := _sel_i > prev or (prev == _archs.size() - 1 and _sel_i == 0)
	if animate_swap:
		_hero_tween = create_tween()
		_hero_tween.tween_property(_hero, "position:x", 560.0 + (-90.0 if from_right else 90.0), 0.1).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
		_hero_tween.parallel().tween_property(_hero, "modulate:a", 0.0, 0.1)
		_hero_tween.tween_callback(func():
			if not frames.is_empty():
				_hero.texture = frames[0]
			_hero.position.x = 560.0 + (110.0 if from_right else -110.0))
		_hero_tween.tween_property(_hero, "position:x", 560.0, 0.14).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
		_hero_tween.parallel().tween_property(_hero, "modulate:a", 1.0, 0.12)
		_hero_tween.tween_property(_hero, "scale", Vector2(1.08, 0.92), 0.07)
		_hero_tween.tween_property(_hero, "scale", Vector2.ONE, 0.12).set_trans(Tween.TRANS_BOUNCE)
	else:
		if not frames.is_empty():
			_hero.texture = frames[0]

func _lock_in() -> void:
	if _sel_arch.is_empty() or _stamping:
		return
	_stamping = true
	var sfx := AudioStreamPlayer.new()
	sfx.stream = load("res://assets/sfx/deposit.wav")
	add_child(sfx)
	sfx.play()
	var stamp := _label("LOCKED IN", 110, PALETTE["coral"])
	stamp.position = Vector2(400, 380)
	stamp.rotation = -0.14
	stamp.pivot_offset = Vector2(330, 60)
	stamp.scale = Vector2(2.6, 2.6)
	stamp.modulate.a = 0.0
	add_child(stamp)
	var tw := create_tween()
	tw.tween_property(stamp, "scale", Vector2.ONE, 0.14).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
	tw.parallel().tween_property(stamp, "modulate:a", 1.0, 0.1)
	tw.tween_interval(0.45)
	tw.tween_callback(func():
		stamp.queue_free()
		_stamping = false
		_transition_to(1))

## Curtain-wipe page transition: two night panels close, page swaps, they open.
func _transition_to(page_i: int) -> void:
	var top := ColorRect.new()
	top.color = PALETTE["ink"]
	top.size = Vector2(1536, 0)
	var bottom := ColorRect.new()
	bottom.color = PALETTE["ink"]
	bottom.size = Vector2(1536, 0)
	bottom.position = Vector2(0, 1024)
	add_child(top)
	add_child(bottom)
	var sfx := AudioStreamPlayer.new()
	sfx.stream = load("res://assets/sfx/card_flip.wav")
	add_child(sfx)
	sfx.play()
	var tw := create_tween()
	tw.set_parallel(true)
	tw.tween_property(top, "size:y", 520.0, 0.22).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
	tw.tween_property(bottom, "position:y", 504.0, 0.22).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
	tw.tween_property(bottom, "size:y", 520.0, 0.22).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
	tw.set_parallel(false)
	tw.tween_callback(func(): _show_page(page_i))
	tw.set_parallel(true)
	tw.tween_property(top, "size:y", 0.0, 0.22).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tw.tween_property(bottom, "position:y", 1024.0, 0.22).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tw.tween_property(bottom, "size:y", 0.0, 0.22).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tw.set_parallel(false)
	tw.tween_callback(func():
		top.queue_free()
		bottom.queue_free()
		sfx.queue_free())

func _process(_delta: float) -> void:
	if _page != 0 or _hero == null:
		return
	var t := Time.get_ticks_msec() / 1000.0
	_hero.rotation = sin(t * 1.1) * 0.02                      # gentle sway
	_hero.position.y = _hero_base_y + sin(t * 2.2) * 4.0      # breath float
	if _hero_shadow:
		_hero_shadow.scale.x = 1.0 - sin(t * 2.2) * 0.03      # shadow answers the float
	if _spot_pool:
		_spot_pool.modulate.a = 0.85 + sin(t * 1.7) * 0.15    # spotlight breathes
	if _title_label:
		_title_label.rotation = sin(t * 0.7) * 0.004
	if _lockin_btn and not _stamping:
		var p := 1.0 + sin(t * 3.0) * 0.02
		_lockin_btn.scale = Vector2(p, p)

func _unhandled_input(event: InputEvent) -> void:
	if _page != 0 or _stamping or not (event is InputEventKey and event.pressed):
		return
	if event.keycode == KEY_LEFT or event.keycode == KEY_A:
		_select(_sel_i - 1)
	elif event.keycode == KEY_RIGHT or event.keycode == KEY_D:
		_select(_sel_i + 1)
	elif event.keycode == KEY_ENTER or event.keycode == KEY_SPACE:
		_lock_in()

func _tick_anim() -> void:
	_anim_i += 1
	if _sel_arch.is_empty() or _hero == null:
		return
	var frames: Array = _anim_frames.get(String(_sel_arch["id"]), [])
	if frames.size() > 1:
		_hero.texture = frames[_anim_i % frames.size()]

# ---------- SCREEN 2: NAME YOUR STARTUP ----------

func _dim(page: Control) -> void:
	var dim := ColorRect.new()
	dim.color = Color(0.11, 0.13, 0.16, 0.84)
	dim.size = Vector2(1536, 1024)
	page.add_child(dim)

func _style_option(ob: OptionButton) -> void:
	ob.add_theme_font_override("font", _font)
	ob.add_theme_font_size_override("font_size", 22)
	ob.add_theme_color_override("font_color", PALETTE["ink"])
	ob.add_theme_color_override("font_hover_color", PALETTE["ink"])
	ob.add_theme_color_override("font_focus_color", PALETTE["ink"])
	var st := StyleBoxFlat.new()
	st.bg_color = Color.WHITE
	st.border_color = PALETTE["ink"]
	st.set_border_width_all(3)
	st.set_corner_radius_all(10)
	st.content_margin_left = 12
	st.content_margin_right = 12
	for state in ["normal", "hover", "pressed", "focus"]:
		ob.add_theme_stylebox_override(state, st)

func _build_name() -> Control:
	var page := Control.new()
	page.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(page)
	_dim(page)

	var title := _ink_outline(_dlabel("NAME YOUR STARTUP", 58, PALETTE["cream"]))
	title.position = Vector2(430, 120)
	page.add_child(title)
	_rule_under(page, "NAME YOUR STARTUP", 58, Vector2(430, 120))

	var witness := TextureRect.new()
	witness.position = Vector2(150, 380)
	witness.size = Vector2(280, 340)
	witness.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	witness.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	page.add_child(witness)
	_name_witness = witness
	var wsh := EllipseShadow.new()
	wsh.position = Vector2(200, 706)
	wsh.size = Vector2(180, 30)
	page.add_child(wsh)

	_name_edit = PaperInput.new()
	_name_edit.setup("THE NAME", "Mossflow", 44)
	_name_edit.position = Vector2(560, 300)
	_name_edit.set_deferred("size", Vector2(660, 132))
	page.add_child(_name_edit)

	_idea_edit = PaperInput.new()
	_idea_edit.setup("WHAT IT DOES — the world will hold you to this",
		"an app that walks your dog, badly", 34)
	_idea_edit.position = Vector2(500, 470)
	_idea_edit.set_deferred("size", Vector2(780, 124))
	page.add_child(_idea_edit)

	var reroll := Button.new()
	reroll.text = "SPIN THE IDEA MACHINE"
	reroll.position = Vector2(690, 636)
	reroll.size = Vector2(400, 68)
	reroll.pivot_offset = Vector2(200, 34)
	_style_button(reroll, PALETTE["yellow"], 26)
	_paper_card(reroll)
	_juice(reroll)
	reroll.pressed.connect(_spin_idea)
	page.add_child(reroll)
	var hintl := _label("or type your own. braver.", 24, Color(PALETTE["cream"], 0.7))
	hintl.position = Vector2(760, 716)
	page.add_child(hintl)
	_reroll_idea()
	_name_edit.grab_write_focus()

	var back := Button.new()
	back.text = "←"
	back.position = Vector2(60, 900)
	back.size = Vector2(90, 70)
	_style_button(back, PALETTE["blue"], 30)
	_paper_card(back)
	back.pressed.connect(func(): _transition_to(0))
	page.add_child(back)
	var next := Button.new()
	next.text = "TO THE FOUNDING  →"
	next.position = Vector2(1150, 890)
	next.size = Vector2(340, 84)
	_style_button(next, PALETTE["coral"], 32)
	_paper_card(next)
	next.pressed.connect(func():
		_sfx_click.play()
		_transition_to(2))
	page.add_child(next)
	return page

func _spin_idea() -> void:
	if _spinning:
		return
	_spinning = true
	_sfx_click.play()
	var tw := create_tween()
	for i in 6:
		tw.tween_callback(_reroll_idea)
		tw.tween_interval(0.07 + i * 0.03)
	tw.tween_callback(func(): _spinning = false)

func _reroll_idea() -> void:
	var r := RandomNumberGenerator.new()
	r.randomize()
	_name_edit.set_value(NAME_A[r.randi_range(0, NAME_A.size() - 1)] + NAME_B[r.randi_range(0, NAME_B.size() - 1)])
	_idea_edit.set_value("%s %s for %s" % [IDEA_PRE[r.randi_range(0, IDEA_PRE.size() - 1)], IDEA_FORM[r.randi_range(0, IDEA_FORM.size() - 1)], IDEA_FOR[r.randi_range(0, IDEA_FOR.size() - 1)]])

# ---------- SCREEN 3: THE FOUNDING ----------

const SHAPE_WHAT := [
	["Software", "itm_laptop", "Ships fast. Scales free. Everyone is doing it, and that is the problem."],
	["Hardware", "itm_dads_server", "Real things for real shelves. Slower, costlier, defensible."],
	["Marketplace", "env_boxes", "You own the middle. Nothing works until both sides show up."],
]
const SHAPE_WHO := [
	["Enterprise", "env_calendar", "Huge contracts, glacial sales. Bring patience and a blazer."],
	["SMB", "itm_textbook", "Thousands of small checks. They churn when the card expires."],
	["Consumer", "env_tv", "Millions of maybes. You need volume, virality, and luck."],
]

func _build_shape_page() -> Control:
	var page := Control.new()
	page.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(page)
	_dim(page)
	var title := _ink_outline(_dlabel("THE SHAPE OF IT", 56, PALETTE["cream"]))
	title.position = Vector2(60, 26)
	page.add_child(title)
	_rule_under(page, "THE SHAPE OF IT", 56, Vector2(60, 26))
	var sub := _label("This shapes every week that follows.", 27, Color(PALETTE["cream"], 0.85))
	sub.position = Vector2(64, 116)
	page.add_child(sub)

	var wl := _dlabel("WHAT", 30, PALETTE["yellow"])
	wl.position = Vector2(64, 172)
	page.add_child(wl)
	for i in SHAPE_WHAT.size():
		var card := _shape_card(SHAPE_WHAT[i], Vector2(64 + i * 470, 226), true)
		page.add_child(card)
		_what_chips.append(card)
	var hl := _dlabel("FOR WHO", 30, PALETTE["yellow"])
	hl.position = Vector2(64, 552)
	page.add_child(hl)
	for i in SHAPE_WHO.size():
		var card := _shape_card(SHAPE_WHO[i], Vector2(64 + i * 470, 606), false)
		page.add_child(card)
		_who_chips.append(card)
	_restyle_chips()

	var back := Button.new()
	back.text = "←"
	back.position = Vector2(48, 940)
	back.size = Vector2(100, 64)
	_style_button(back, PALETTE["blue"], 30)
	_paper_card(back)
	back.pressed.connect(func(): _transition_to(1))
	page.add_child(back)
	var next := Button.new()
	next.text = "NEXT: THE CREW  →"
	next.position = Vector2(1120, 930)
	next.size = Vector2(380, 80)
	_style_button(next, PALETTE["coral"], 32)
	_paper_card(next)
	_juice(next)
	next.pressed.connect(func():
		_sfx_click.play()
		_transition_to(3))
	page.add_child(next)
	return page

func _shape_card(spec: Array, pos: Vector2, is_what: bool) -> Button:
	var card := Button.new()
	card.position = pos
	card.size = Vector2(440, 300)
	card.pivot_offset = Vector2(220, 150)
	_style_button(card, PALETTE["sage"], 20)
	_paper_card(card)
	card.rotation = (0.006 if is_what else -0.005) * (1.0 if pos.x < 500 else (-1.0 if pos.x > 900 else 0.45))
	_juice(card)
	card.pressed.connect(func():
		if is_what:
			_biz_what = String(spec[0])
		else:
			_biz_who = String(spec[0])
		_sfx_click.play()
		_restyle_chips())
	# icon: fixed box at the top, texture assigned LAST so the box size wins
	var icon := TextureRect.new()
	icon.position = Vector2(158, 12)
	icon.size = Vector2(124, 124)
	icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	icon.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(icon)
	var ip := "res://assets/sprites/%s.png" % String(spec[1])
	if ResourceLoader.exists(ip):
		icon.texture = load(ip)
	var nm := _dlabel(String(spec[0]).to_upper(), 30, PALETTE["ink"])
	nm.name = "nm"
	nm.position = Vector2(20, 128)
	nm.size = Vector2(400, 44)
	nm.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	nm.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(nm)
	var ds := _label(String(spec[2]), 27, Color(PALETTE["ink"], 0.88))
	ds.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	ds.custom_minimum_size = Vector2(384, 0)
	ds.position = Vector2(28, 184)
	ds.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	ds.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(ds)
	ds.set_deferred("size", Vector2(384, 0))
	# the picked-state check chip
	var chk := Label.new()
	chk.name = "chk"
	chk.text = "✓"
	chk.add_theme_font_override("font", _font)
	chk.add_theme_font_size_override("font_size", 34)
	chk.add_theme_color_override("font_color", PALETTE["coral"])
	chk.position = Vector2(396, 6)
	chk.visible = false
	card.add_child(chk)
	return card

func _card_state(b: Button, sel: bool) -> void:
	# the picked card is circled in coral pen; the rest stay paper, just quieter
	# an unpicked card is paper left in the shade — lighter and quieter, never
	# a browner, muddier paper than the one that is picked
	# paper is opaque. Fading a card with alpha lets the stage lighting behind it
	# bleed through as a seam across the card face
	b.modulate = Color.WHITE if sel else Color(0.93, 0.92, 0.90, 1.0)
	var edge := b.get_node_or_null("edge")
	if edge is PaperEdge:
		edge.edge = PALETTE["coral"] if sel else PALETTE["ink"]
		edge.thick = 7.0 if sel else 4.0
		edge.queue_redraw()
	# selection marks the card. Growing it changes the layout every time the
	# player looks at a different option, which reads as the grid slipping.
	var tw := create_tween()
	tw.tween_property(b, "scale", Vector2.ONE, 0.12).set_trans(Tween.TRANS_BACK)

func _restyle_chips() -> void:
	for b in _what_chips:
		var sel: bool = String(b.get_node("nm").text) == _biz_what.to_upper()
		_card_state(b, sel)
		b.get_node("chk").visible = sel
	for b in _who_chips:
		var sel2: bool = String(b.get_node("nm").text) == _biz_who.to_upper()
		_card_state(b, sel2)
		b.get_node("chk").visible = sel2

func _build_crew_page() -> Control:
	var page := Control.new()
	page.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(page)
	_dim(page)

	var title := _ink_outline(_dlabel("THE CREW", 56, PALETTE["cream"]))
	title.position = Vector2(60, 26)
	page.add_child(title)
	_rule_under(page, "THE CREW", 56, Vector2(60, 26))
	var sub := _label("Recruit cofounders. Split the company. They will remember the split.", 28, Color(PALETTE["cream"], 0.85))
	sub.position = Vector2(64, 116)
	page.add_child(sub)

	_crew_row = Control.new()
	_crew_row.position = Vector2(40, 200)
	_crew_row.size = Vector2(1140, 500)
	page.add_child(_crew_row)

	_donut = CapTableDonut.new()
	(_donut as CapTableDonut).text_color = PALETTE["cream"]
	_donut.position = Vector2(1230, 170)
	_donut.size = Vector2(260, 260)
	page.add_child(_donut)
	var dcap := _label("the cap table", 24, Color(PALETTE["cream"], 0.6))
	dcap.position = Vector2(1288, 436)
	page.add_child(dcap)

	var back := Button.new()
	back.text = "←"
	back.position = Vector2(48, 930)
	back.size = Vector2(100, 70)
	_style_button(back, PALETTE["blue"], 30)
	_paper_card(back)
	back.pressed.connect(func(): _transition_to(2))
	page.add_child(back)
	var next := Button.new()
	next.text = "NEXT: FIRST MONEY  →"
	next.position = Vector2(1090, 920)
	next.size = Vector2(410, 84)
	_style_button(next, PALETTE["coral"], 32)
	_paper_card(next)
	_juice(next)
	next.pressed.connect(func():
		_sfx_click.play()
		_transition_to(4))
	page.add_child(next)

	# recruit modal lives above everything on this page
	_recruit_layer = Control.new()
	_recruit_layer.set_anchors_preset(Control.PRESET_FULL_RECT)
	_recruit_layer.visible = false
	page.add_child(_recruit_layer)
	return page

const ROLE_INFO := {
	"Sales": {"gives": "Covers SELL — turns strangers into revenue", "slug": "sales"},
	"Business": {"gives": "Covers RAISE and the spreadsheet — makes money appear", "slug": "business"},
	"Tech": {"gives": "Covers BUILD — ships the product", "slug": "tech"},
	"Hustler": {"gives": "Covers GRIT — does whatever nobody else will", "slug": "hustler"},
	"The Idea Friend": {"gives": "Had the idea. That is the whole thing.", "slug": "idea"},
}

## Art for the new roles is still being drawn. Until it lands, a role borrows
## the nearest existing portrait rather than rendering an empty card.
const STAGE_SCENE := "select_stage_empty_v2"
# CHOOSE YOUR FOUNDER reserves two bands so elements cannot drift into each
# other as copy changes: the roster owns everything below DOCK_BAND_TOP, and
# the founder's sheet must end above it. A position that merely happens to be
# free today is how the dock ended up across the card in the first place.
const DOCK_BAND_TOP := 848.0
const SHEET_BOTTOM_MAX := 840.0
const ROLE_ART_FALLBACK := {"sales": "business", "tech": "technical", "hustler": "design"}

## Resolves a cofounder portrait, trying the role's own art first, then its
## stand-in. Returns "" when neither exists so callers can draw a placeholder.
func _cf_art(slug: String, mood: String = "neutral") -> String:
	var direct := "res://assets/sprites/cf_%s_%s.png" % [slug, mood]
	if ResourceLoader.exists(direct):
		return direct
	var alt: String = ROLE_ART_FALLBACK.get(slug, "")
	if alt != "":
		var sub := "res://assets/sprites/cf_%s_%s.png" % [alt, mood]
		if ResourceLoader.exists(sub):
			return sub
	return ""

## A type can be on the cap table once. Two of the same cofounder is not a
## team, it is the same person twice.
func _role_taken(idx: int) -> bool:
	for cf in _cofounders:
		if int(cf["role"]) == idx:
			return true
	return false

func _first_free_role() -> int:
	for i in ROLES.size():
		if not _role_taken(i):
			return i
	return 0

func _open_recruit() -> void:
	for c in _recruit_layer.get_children():
		c.queue_free()
	_recruit_layer.visible = true
	var shade := ColorRect.new()
	shade.color = Color(0.05, 0.05, 0.06, 0.8)
	shade.size = Vector2(1536, 1024)
	_recruit_layer.add_child(shade)
	shade.gui_input.connect(func(ev):
		if ev is InputEventMouseButton and ev.pressed:
			_recruit_layer.visible = false)
	var panel := _panel(Vector2(148, 200), Vector2(1240, 620), Color(0.09, 0.09, 0.09, 0.98), PALETTE["cream"])
	_recruit_layer.add_child(panel)
	var t := _dlabel("WHO DO YOU CALL?", 44, PALETTE["yellow"])
	t.position = Vector2(40, 24)
	panel.add_child(t)
	for i in ROLES.size():
		var role: String = ROLES[i]
		var card := Button.new()
		card.position = Vector2(40 + i * 235, 100)
		card.size = Vector2(220, 420)
		_style_button(card, PALETTE["yellow"], 20)
		_paper_card(card)
		if _role_taken(i):
			card.disabled = true
			card.modulate = Color(0.72, 0.72, 0.70, 1.0)
		card.pressed.connect(func():
			_recruit_layer.visible = false
			_cofounders.append({"role": i, "commitment": 0, "equity": 25.0, "vesting": true, "fresh": true})
			_sfx_click.play()
			_refresh_capline())
		panel.add_child(card)
		var spr := TextureRect.new()
		spr.size = Vector2(160, 190)
		spr.position = Vector2(30, 16)
		spr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		spr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		var sp := _cf_art(String(ROLE_INFO[role]["slug"]))
		if sp != "":
			spr.texture = load(sp)
		card.add_child(spr)
		if sp == "":
			var ph := BlobPlaceholder.new()
			ph.size = spr.size
			ph.position = spr.position
			ph.mouse_filter = Control.MOUSE_FILTER_IGNORE
			card.add_child(ph)
		var nm := _dlabel(role.to_upper(), 25, PALETTE["ink"])
		nm.position = Vector2(10, 218)
		nm.size = Vector2(200, 40)
		nm.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		card.add_child(nm)
		var gv := _label(String(ROLE_INFO[role]["gives"]), 24, PALETTE["ink"])
		gv.position = Vector2(10, 262)
		gv.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		_wrap(gv, 200.0)
		card.add_child(gv)
	# the ask is the same for every role — stating it four times is the screen
	# repeating itself, so it is said once, under the row
	var ask := _label("whoever you call will want ~25% of the company.", 26, PALETTE["coral"])
	ask.position = Vector2(40, 536)
	panel.add_child(ask)
	var cancel := Button.new()
	cancel.text = "☎ nobody. hang up."
	cancel.position = Vector2(920, 545)
	cancel.size = Vector2(280, 52)
	_style_button(cancel, PALETTE["blue"], 24)
	cancel.pressed.connect(func(): _recruit_layer.visible = false)
	panel.add_child(cancel)

func _build_money_page() -> Control:
	var page := Control.new()
	page.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(page)
	_dim(page)
	var title := _ink_outline(_dlabel("FIRST MONEY", 56, PALETTE["cream"]))
	title.position = Vector2(60, 26)
	page.add_child(title)
	_rule_under(page, "FIRST MONEY", 56, Vector2(60, 26))
	var sub := _label("Who pays for week one? Money now costs equity forever.", 28, Color(PALETTE["cream"], 0.85))
	sub.position = Vector2(64, 116)
	page.add_child(sub)

	var display := {
		"bootstrap": {"icon": "itm_savings_jar", "big": "your savings", "cost": "you keep 100%", "cost_col": "sage", "flavor": "Ramen. Focus. Freedom."},
		"fnf": {"icon": "itm_goodwill", "big": "+$15,000", "cost": "−5% · dilutes EVERYONE", "cost_col": "coral", "flavor": "Awkward Thanksgiving if this fails."},
		"angel": {"icon": "itm_dignity", "big": "+$50,000", "cost": "−12% · dilutes EVERYONE", "cost_col": "coral", "flavor": "The angel replies only in voice memos."},
	}
	var funds: Array = data.get("fundings", [])
	for i in funds.size():
		var f: Dictionary = funds[i]
		var d: Dictionary = display.get(String(f["id"]), {})
		var card := Button.new()
		card.position = Vector2(120 + i * 440, 210)
		card.size = Vector2(400, 520)
		card.pivot_offset = Vector2(200, 260)
		_style_button(card, PALETTE["sage"], 22)
		_paper_card(card)
		card.rotation = [0.006, -0.004, 0.005][i % 3]
		card.pressed.connect(_pick_fund.bind(f, card))
		_juice(card)
		page.add_child(card)
		_money_cards.append(card)
		_fund_btns.append(card)
		var icon := TextureRect.new()
		icon.size = Vector2(150, 150)
		icon.position = Vector2(125, 30)
		icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		var ip := "res://assets/sprites/%s.png" % String(d.get("icon", ""))
		if ResourceLoader.exists(ip):
			icon.texture = load(ip)
		card.add_child(icon)
		var nm := _dlabel(String(f["name"]), 30, PALETTE["ink"])
		nm.position = Vector2(20, 196)
		nm.size = Vector2(360, 44)
		nm.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		card.add_child(nm)
		var big := _dlabel(String(d.get("big", "")), 40, PALETTE["ink"])
		big.position = Vector2(20, 252)
		big.size = Vector2(360, 60)
		big.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		card.add_child(big)
		var cost := _label(String(d.get("cost", "")), 32, PALETTE[String(d.get("cost_col", "coral"))])
		cost.position = Vector2(20, 330)
		cost.size = Vector2(360, 44)
		cost.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		card.add_child(cost)
		var fl := _label(String(d.get("flavor", "")), 27, Color(PALETTE["ink"], 0.8))
		fl.position = Vector2(30, 404)
		fl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		_wrap(fl, 340.0)
		card.add_child(fl)

	var strip := PaperEdge.new()
	strip.size = Vector2(880, 66)
	strip.position = Vector2(328, 774)
	strip.thick = 3.0
	strip.lean = 2
	strip.mouse_filter = Control.MOUSE_FILTER_IGNORE
	page.add_child(strip)
	_money_strip = strip
	# once the room declares a ledger, the number is written there instead and
	# the plate that used to carry it is retired
	if _surfaces and _surfaces.has("ledger"):
		strip.visible = false
	_money_preview = _label("", 28, PALETTE["ink"])
	_money_preview.visible = not (_surfaces != null and _surfaces.has("ledger"))
	_money_preview.position = Vector2(328, 788)
	_money_preview.size = Vector2(880, 40)
	_money_preview.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	page.add_child(_money_preview)

	var back := Button.new()
	back.text = "←"
	back.position = Vector2(48, 930)
	back.size = Vector2(100, 70)
	_style_button(back, PALETTE["blue"], 30)
	_paper_card(back)
	back.pressed.connect(func(): _transition_to(3))
	page.add_child(back)
	var next := Button.new()
	next.text = "NEXT: PACK YOUR BAG  →"
	next.position = Vector2(1060, 920)
	next.size = Vector2(440, 84)
	_style_button(next, PALETTE["coral"], 32)
	_paper_card(next)
	_juice(next)
	next.pressed.connect(func():
		if _sel_fund.is_empty():
			return
		_sfx_click.play()
		_transition_to(5))
	page.add_child(next)
	return page

func _build_bag_page() -> Control:
	var page := Control.new()
	page.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(page)
	_dim(page)
	var title := _ink_outline(_dlabel("PACK YOUR BAG", 56, PALETTE["cream"]))
	title.position = Vector2(60, 26)
	page.add_child(title)
	_rule_under(page, "PACK YOUR BAG", 56, Vector2(60, 26))
	var sub := _label("4 slots. Everything else stays in your old life.", 28, Color(PALETTE["cream"], 0.85))
	sub.position = Vector2(64, 116)
	page.add_child(sub)

	# item grid — hover to inspect, click to pack
	var gi := 0
	for def in content_items:
		var ib := Button.new()
		ib.custom_minimum_size = Vector2(112, 112)
		ib.size = Vector2(112, 112)
		ib.position = Vector2(60 + (gi % 5) * 128, 190 + (gi / 5) * 130)
		ib.pivot_offset = Vector2(56, 56)
		var ipath := "res://assets/sprites/%s.png" % String(def["id"])
		if ResourceLoader.exists(ipath):
			ib.icon = load(ipath)
			ib.expand_icon = true
		_style_button(ib, PALETTE["yellow"], 14)
		ib.pressed.connect(_toggle_bag.bind(String(def["id"]), int(def.get("carry_cost", 1)), ib))
		ib.mouse_entered.connect(func():
			_bag_detail(def)
			var t := create_tween()
			t.tween_property(ib, "scale", Vector2(1.07, 1.07), 0.08))
		ib.mouse_exited.connect(func():
			var t := create_tween()
			t.tween_property(ib, "scale", Vector2.ONE, 0.1))
		var packed_mark := _dlabel("✕", 34, PALETTE["coral"])
		packed_mark.name = "packed"
		packed_mark.position = Vector2(8, -6)
		packed_mark.visible = false
		packed_mark.mouse_filter = Control.MOUSE_FILTER_IGNORE
		ib.add_child(packed_mark)
		page.add_child(ib)
		_bag_btns[String(def["id"])] = ib
		if int(def.get("carry_cost", 1)) > 1:
			var pill := Panel.new()
			# the tag sits INSIDE the tile — a badge that hangs off the corner
			# gets clipped by whatever is drawn next to it
			pill.position = ib.position + Vector2(ib.size.x - 104.0, ib.size.y - 40.0)
			pill.size = Vector2(100, 34)
			var pst := StyleBoxFlat.new()
			pst.bg_color = PALETTE["coral"]
			pst.border_color = PALETTE["ink"]
			pst.set_border_width_all(2)
			pst.set_corner_radius_all(12)
			pill.add_theme_stylebox_override("panel", pst)
			pill.mouse_filter = Control.MOUSE_FILTER_IGNORE
			page.add_child(pill)
			var badge := _dlabel("2 SLOTS", 24, Color.WHITE)
			badge.position = Vector2(9, -2)
			badge.mouse_filter = Control.MOUSE_FILTER_IGNORE
			pill.add_child(badge)
		gi += 1

	# detail panel — what the thing is FOR
	var dp := Control.new()
	dp.position = Vector2(730, 180)
	dp.size = Vector2(430, 520)
	dp.rotation = 0.007
	page.add_child(dp)
	var dp_sheet := PaperEdge.new()
	dp_sheet.size = dp.size
	dp_sheet.thick = 4.0
	dp_sheet.lean = 4
	dp_sheet.mouse_filter = Control.MOUSE_FILTER_IGNORE
	dp.add_child(dp_sheet)
	_bagd_art = TextureRect.new()
	_bagd_art.size = Vector2(190, 190)
	_bagd_art.position = Vector2(120, 26)
	_bagd_art.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_bagd_art.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	dp.add_child(_bagd_art)
	_bagd_name = _dlabel("", 34, PALETTE["ink"])
	_bagd_name.position = Vector2(30, 232)
	_bagd_name.size = Vector2(370, 50)
	_bagd_name.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	dp.add_child(_bagd_name)
	var rule := HandRule.new()
	rule.length = 120.0
	rule.color = PALETTE["coral"]
	rule.size = Vector2(120, 14)
	rule.position = Vector2(155, 284)
	rule.mouse_filter = Control.MOUSE_FILTER_IGNORE
	dp.add_child(rule)
	_bagd_blurb = _label("", 27, PALETTE["ink"])
	_bagd_blurb.position = Vector2(36, 310)
	_bagd_blurb.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_wrap(_bagd_blurb, 360.0)
	dp.add_child(_bagd_blurb)
	_bagd_cost = _label("", 24, Color(PALETTE["ink"], 0.6))
	_bagd_cost.position = Vector2(36, 452)
	_bagd_cost.size = Vector2(360, 34)
	_bagd_cost.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	dp.add_child(_bagd_cost)

	# the box + slots
	_slots_label = _dlabel("SLOTS 0/4", 38, PALETTE["cream"])
	_slots_label.position = Vector2(1236, 200)
	page.add_child(_slots_label)
	var box := TextureRect.new()
	if ResourceLoader.exists("res://assets/sprites/env_boxes.png"):
		box.texture = load("res://assets/sprites/env_boxes.png")
	box.size = Vector2(230, 230)
	box.position = Vector2(1210, 260)
	box.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	box.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	page.add_child(box)
	_box_anchor = box.position + Vector2(115, 90)
	_packed_row = HBoxContainer.new()
	_packed_row.position = Vector2(1150, 690)
	_packed_row.add_theme_constant_override("separation", 14)
	page.add_child(_packed_row)

	_bag_summary = _label("", 28, Color(PALETTE["cream"], 0.9))
	_bag_summary.position = Vector2(64, 820)
	_bag_summary.size = Vector2(1000, 100)
	_bag_summary.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	page.add_child(_bag_summary)

	var back := Button.new()
	back.text = "←"
	back.position = Vector2(48, 930)
	back.size = Vector2(100, 70)
	_style_button(back, PALETTE["blue"], 30)
	_paper_card(back)
	back.pressed.connect(func(): _transition_to(4))
	page.add_child(back)
	_launch = Button.new()
	_launch.text = "SIGN & QUIT YOUR JOB  →"
	_launch.position = Vector2(1050, 920)
	_launch.size = Vector2(450, 84)
	_style_button(_launch, PALETTE["coral"], 32)
	_juice(_launch)
	_launch.pressed.connect(_do_launch)
	page.add_child(_launch)
	if not content_items.is_empty():
		_bag_detail(content_items[0])
	return page

func _bag_detail(def: Dictionary) -> void:
	if _bagd_name == null:
		return
	var ipath := "res://assets/sprites/%s.png" % String(def["id"])
	if ResourceLoader.exists(ipath):
		_bagd_art.texture = load(ipath)
	_bagd_name.text = String(def["name"])
	_bagd_blurb.text = String(def.get("blurb", ""))
	var cost := int(def.get("carry_cost", 1))
	_bagd_cost.text = ("takes %d slot%s" % [cost, "s" if cost > 1 else ""]) + ("  ·  PACKED ✓" if _bag.has(String(def["id"])) else "")

func _add_cofounder() -> void:
	if _cofounders.size() >= MAX_COFOUNDERS:
		return
	_open_recruit()
	return

func _add_cofounder_dead() -> void:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 10)
	var cf := {"role": _first_free_role(), "commitment": 0, "equity": 25.0, "vesting": true, "node": row}
	var role := OptionButton.new()
	for r in ROLES:
		role.add_item(r)
	_style_option(role)
	role.custom_minimum_size = Vector2(180, 48)
	role.select(int(cf["role"]))
	for i in ROLES.size():
		if i != int(cf["role"]) and _role_taken(i):
			role.set_item_disabled(i, true)
	role.item_selected.connect(func(i):
		for other in _cofounders:
			if other != cf and int(other["role"]) == i:
				role.select(int(cf["role"]))
				return
		cf["role"] = i
		_refresh_capline())
	row.add_child(role)
	var com := OptionButton.new()
	for c in COMMITMENTS:
		com.add_item(c)
	_style_option(com)
	com.custom_minimum_size = Vector2(126, 48)
	com.item_selected.connect(func(i): cf["commitment"] = i; _refresh_capline())
	row.add_child(com)
	var minus := Button.new()
	minus.text = "−"
	minus.custom_minimum_size = Vector2(44, 48)
	_style_button(minus, PALETTE["coral"], 28)
	row.add_child(minus)
	var eq_label := _label("25%", 30, PALETTE["cream"])
	eq_label.custom_minimum_size = Vector2(62, 48)
	eq_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	row.add_child(eq_label)
	var plus := Button.new()
	plus.text = "+"
	plus.custom_minimum_size = Vector2(44, 48)
	_style_button(plus, PALETTE["sage"], 28)
	row.add_child(plus)
	minus.pressed.connect(func():
		cf["equity"] = maxf(1.0, float(cf["equity"]) - 5.0)
		eq_label.text = "%.0f%%" % cf["equity"]
		_refresh_capline()
		_react_team_member(_cofounders.find(cf), false))
	plus.pressed.connect(func():
		cf["equity"] = minf(60.0, float(cf["equity"]) + 5.0)
		eq_label.text = "%.0f%%" % cf["equity"]
		_refresh_capline()
		_react_team_member(_cofounders.find(cf), true))
	var vest := CheckBox.new()
	vest.text = "vested"
	vest.tooltip_text = "4-year vesting, 1-year cliff. Turning this off is the classic mistake."
	vest.button_pressed = true
	vest.add_theme_font_override("font", _font)
	vest.add_theme_font_size_override("font_size", 22)
	vest.add_theme_color_override("font_color", PALETTE["cream"])
	vest.toggled.connect(func(on): cf["vesting"] = on; _refresh_capline())
	row.add_child(vest)
	var rm := Button.new()
	rm.text = "✕"
	rm.custom_minimum_size = Vector2(44, 48)
	_style_button(rm, PALETTE["coral"], 24)
	rm.pressed.connect(func():
		_cofounders.erase(cf)
		row.queue_free()
		_refresh_capline())
	row.add_child(rm)
	_cofounders.append(cf)
	_cf_list.add_child(row)
	_sfx_click.play()
	_refresh_capline()

func _pick_fund(f: Dictionary, btn: Button) -> void:
	_sel_fund = f
	_sfx_click.play()
	for b in _fund_btns:
		_card_state(b, b == btn)
	if int(f.get("cash", 0)) > 0:
		for i in 4:
			var coin := _label("$", 26, PALETTE["yellow"])
			coin.position = btn.global_position + Vector2(40 + i * 55.0, 20)
			add_child(coin)
			var ct := create_tween()
			ct.tween_property(coin, "position:y", coin.position.y - 70.0 - i * 8.0, 0.5 + i * 0.06).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
			ct.parallel().tween_property(coin, "modulate:a", 0.0, 0.55 + i * 0.06)
			ct.tween_callback(coin.queue_free)
	_refresh_capline()

## Paints an item tile as packed or not across EVERY state, so the change is
## visible under the cursor that caused it.
func _paint_bag_tile(btn: Button, packed: bool) -> void:
	for st_name in ["normal", "hover", "pressed"]:
		var sb = btn.get_theme_stylebox(st_name)
		if sb is StyleBoxFlat:
			sb.bg_color = Color("B9C9AC") if packed else PALETTE["cream"]
			sb.border_color = PALETTE["coral"] if packed else PALETTE["ink"]
			sb.set_border_width_all(6 if packed else 4)
	var mark := btn.get_node_or_null("packed")
	if mark:
		mark.visible = packed

func _toggle_bag(id: String, cost: int, btn: Button) -> void:
	if _bag.has(id):
		_bag.erase(id)
		_paint_bag_tile(btn, false)
	else:
		var used := 0
		for bid in _bag:
			for def in content_items:
				if def["id"] == bid:
					used += int(def.get("carry_cost", 1))
		if used + cost > 4:
			var shake := create_tween()
			for off in [-6.0, 6.0, -4.0, 0.0]:
				shake.tween_property(btn, "position:x", btn.position.x + off, 0.04)
			return
		_bag.append(id)
		_paint_bag_tile(btn, true)
		# the item flies into the box
		if btn.icon:
			var fly := TextureRect.new()
			fly.texture = btn.icon
			fly.size = Vector2(70, 70)
			fly.position = btn.global_position
			add_child(fly)
			var ft := create_tween()
			ft.tween_property(fly, "position", _box_anchor, 0.4).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
			ft.parallel().tween_property(fly, "scale", Vector2(0.3, 0.3), 0.4)
			ft.tween_callback(fly.queue_free)
	_sfx_click.play()
	_refresh_capline()
	if _packed_row:
		for c in _packed_row.get_children():
			c.queue_free()
		for bid in _bag:
			var chip := Button.new()
			chip.flat = true
			var cp := "res://assets/sprites/%s.png" % bid
			if ResourceLoader.exists(cp):
				chip.icon = load(cp)
				chip.expand_icon = true
			chip.custom_minimum_size = Vector2(104, 104)
			chip.tooltip_text = "take it back out"
			chip.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
			var chip_id := String(bid)
			chip.pressed.connect(func():
				var owner_btn: Button = _bag_btns.get(chip_id)
				if owner_btn:
					_toggle_bag(chip_id, 0, owner_btn))
			_packed_row.add_child(chip)

## The YC-canon trap detector.
func _compute_traps() -> Array:
	var traps: Array = []
	var founder_pct := _founder_pct()
	var n := _cofounders.size()
	if n == 0:
		traps.append({"id": "solo", "text": "Solo founder — nobody to split the 2am dread with. Burnout bleed, investors squint."})
	for cf in _cofounders:
		var ft: bool = int(cf["commitment"]) == 0
		var eq: float = float(cf["equity"])
		var role: String = ROLES[int(cf["role"])]
		if ft and role != "The Idea Friend" and eq < 10.0:
			traps.append({"id": "trap_underpaid_cofounder", "text": "A full-time %s at %.0f%% — insulting splits breed resentment. It WILL come up again." % [role.to_lower(), eq]})
		if not ft and eq >= 15.0:
			traps.append({"id": "trap_part_timer_rich", "text": "A part-timer holding %.0f%% — real equity for half presence. Launch week will find them 'at a thing'." % eq})
		if role == "The Idea Friend" and eq >= 10.0:
			traps.append({"id": "trap_idea_tax", "text": "%.0f%% for the idea. Ideas are free; execution is the company. They will have notes." % eq})
		if not bool(cf["vesting"]):
			traps.append({"id": "trap_no_vesting", "text": "No vesting, no cliff — if they walk, the shares walk with them. Forever. The classic."})
	if n >= 3:
		traps.append({"id": "trap_too_many_cooks", "text": "%d cofounders — every decision becomes a senate hearing." % n})
	if founder_pct < 50.0:
		traps.append({"id": "trap_lost_majority", "text": "You hold %.0f%% on day one — everyone else combined outvotes you." % founder_pct})
	if traps.is_empty() and n >= 1 and n <= 2:
		var ok := true
		for cf in _cofounders:
			if int(cf["commitment"]) != 0 or not bool(cf["vesting"]) or float(cf["equity"]) < 15.0:
				ok = false
		if ok:
			traps.append({"id": "healthy_split", "text": "✓ Near-equal, full-time, vested. The essays would be proud. (This pays off.)"})
	return traps

func _dilution() -> float:
	## Investors dilute EVERYONE pro-rata: every founding share is multiplied by this.
	if _sel_fund.is_empty():
		return 1.0
	return 1.0 - float(_sel_fund.get("equity_cost", 0)) / 100.0

func _founder_pct() -> float:
	var pct := 100.0
	for cf in _cofounders:
		pct -= float(cf["equity"])
	return pct * _dilution()

func _refresh_capline() -> void:
	var founder_pct := _founder_pct()
	if _donut:
		var slices: Array = [{"label": "YOU", "pct": maxf(0.0, founder_pct), "color": PALETTE["sage"]}]
		var cf_colors := [PALETTE["blue"], PALETTE["yellow"], Color("A78BBA"), Color("D98E7E")]
		for i in _cofounders.size():
			slices.append({"label": "cf%d" % (i + 1), "pct": float(_cofounders[i]["equity"]) * _dilution(), "color": cf_colors[i % cf_colors.size()]})
		if not _sel_fund.is_empty() and float(_sel_fund.get("equity_cost", 0)) > 0.0:
			slices.append({"label": "investors", "pct": float(_sel_fund["equity_cost"]), "color": PALETTE["coral"]})
		(_donut as CapTableDonut).set_slices(slices, founder_pct)
	var cash := 8000 + (int(_sel_arch.get("start_cash_bonus", 0)) if not _sel_arch.is_empty() else 0) + (int(_sel_fund.get("cash", 0)) if not _sel_fund.is_empty() else 0)
	for bid in _bag:
		for def in content_items:
			if def["id"] == bid:
				cash += int(def.get("cash_value", 0))
	if _surfaces and _surfaces.has("ledger"):
		_surfaces.write("ledger", "IN THE BANK", "~$%s" % _fmt_money(cash))
	if _surfaces and _surfaces.has("sticky"):
		_surfaces.write("sticky", "YOU KEEP", "%.0f%%" % founder_pct)
	if _money_preview:
		_money_preview.text = "You'd keep %.0f%% of %s · ~$%s in the bank on day one" % [founder_pct, _name_edit.value() if _name_edit else "the company", _fmt_money(cash)] if not _sel_fund.is_empty() else "pick one — the donut remembers forever"
	if _slots_label:
		var used := 0
		for bid in _bag:
			for def in content_items:
				if def["id"] == bid:
					used += int(def.get("carry_cost", 1))
		_slots_label.text = "SLOTS %d/4" % used
	if _bag_summary:
		var n_cf: int = _cofounders.size()
		_bag_summary.text = "%s · %s · %d %s · you keep %.0f%% · ~$%s day one" % [
			_name_edit.value() if _name_edit else "?", String(_sel_arch.get("name", "?")),
			n_cf, "cofounder" if n_cf == 1 else "cofounders", founder_pct, _fmt_money(cash)]
	if _launch:
		var blocked := ""
		if _sel_arch.is_empty():
			blocked = "PICK A FOUNDER FIRST"
		elif _sel_fund.is_empty():
			blocked = "PICK YOUR FIRST MONEY"
		elif founder_pct <= 5.0:
			blocked = "YOU KEPT TOO LITTLE — TAKE EQUITY BACK"
		_launch.disabled = blocked != ""
		_set_button_text(_launch, blocked if blocked != "" else "SIGN & QUIT YOUR JOB  →")
	_rebuild_crew(founder_pct)

const ROLE_SLUGS := {"Sales": "sales", "Business": "business", "Tech": "tech", "Hustler": "hustler", "The Idea Friend": "idea"}

func _crew_card_bg(warn: bool = false) -> StyleBoxFlat:
	var st := StyleBoxFlat.new()
	st.bg_color = Color(1, 1, 1, 0.04)
	st.border_color = Color(Color("F2EAD3"), 0.25) if not warn else Color("E86A5C")
	st.set_border_width_all(2)
	st.set_corner_radius_all(14)
	return st

func _rebuild_crew(founder_pct: float) -> void:
	if _crew_row == null:
		return
	for c in _crew_row.get_children():
		c.queue_free()
	_crew_sprites.clear()
	var n := _cofounders.size()
	var slots := n + 1 + (1 if n < MAX_COFOUNDERS else 0)
	var cw := 230.0
	var gap := 18.0
	var x0 := (_crew_row.size.x - (slots * cw + (slots - 1) * gap)) / 2.0
	# YOU card
	var you := Panel.new()
	you.position = Vector2(x0, 0)
	you.size = Vector2(cw, 490)
	you.add_theme_stylebox_override("panel", _crew_card_bg())
	_crew_row.add_child(you)
	var yspr := TextureRect.new()
	yspr.size = Vector2(180, 180)
	yspr.position = Vector2(25, 18)
	yspr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	yspr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	yspr.pivot_offset = Vector2(90, 180)
	var still := "res://assets/sprites/%s.png" % String(_sel_arch.get("sprite", ""))
	if ResourceLoader.exists(still):
		yspr.texture = load(still)
	if founder_pct < 50.0:
		yspr.rotation = 0.12
		yspr.modulate = Color(0.85, 0.85, 0.85)
	you.add_child(yspr)
	var ylab := _dlabel("YOU · CEO", 27, PALETTE["yellow"])
	ylab.position = Vector2(0, 214)
	ylab.size = Vector2(cw, 40)
	ylab.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	you.add_child(ylab)
	var ypct := _dlabel("%.0f%%" % founder_pct, 62, PALETTE["sage"] if founder_pct >= 50.0 else PALETTE["coral"])
	ypct.position = Vector2(0, 262)
	ypct.size = Vector2(cw, 90)
	ypct.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	you.add_child(ypct)
	var ysub := _label("your slice", 24, Color(PALETTE["cream"], 0.6))
	ysub.position = Vector2(0, 360)
	ysub.size = Vector2(cw, 32)
	ysub.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	you.add_child(ysub)
	if founder_pct < 50.0:
		var warn := _label("OUTVOTED!", 28, PALETTE["coral"])
		warn.position = Vector2(0, 420)
		warn.size = Vector2(cw, 36)
		warn.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		you.add_child(warn)
	# cofounder cards
	for i in n:
		var cf: Dictionary = _cofounders[i]
		var card := Panel.new()
		card.position = Vector2(x0 + (i + 1) * (cw + gap), 0)
		card.size = Vector2(cw, 490)
		var no_vest: bool = not bool(cf["vesting"])
		card.add_theme_stylebox_override("panel", _crew_card_bg(no_vest))
		_crew_row.add_child(card)
		var slug: String = ROLE_SLUGS.get(ROLES[int(cf["role"])], "technical")
		var st := _cf_state(cf, n)
		var spr := TextureRect.new()
		spr.size = Vector2(170, 170)
		spr.position = Vector2(30, 14)
		spr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		spr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		spr.pivot_offset = Vector2(85, 170)
		var spath := _cf_art(slug, st)
		if spath != "":
			spr.texture = load(spath)
		card.add_child(spr)
		if spath == "":
			var ph := BlobPlaceholder.new()
			ph.size = spr.size
			ph.position = spr.position
			ph.mouse_filter = Control.MOUSE_FILTER_IGNORE
			card.add_child(ph)
		_crew_sprites.append(spr)
		if cf.get("fresh", false):
			cf["fresh"] = false
			spr.position.y = -80
			spr.modulate.a = 0.0
			var drop := create_tween()
			drop.tween_property(spr, "modulate:a", 1.0, 0.1)
			drop.parallel().tween_property(spr, "position:y", 14.0, 0.32).set_trans(Tween.TRANS_BOUNCE).set_ease(Tween.EASE_OUT)
		var rm := Button.new()
		rm.text = "✕"
		rm.size = Vector2(40, 40)
		rm.position = Vector2(cw - 48, 8)
		_style_button(rm, PALETTE["coral"], 20)
		rm.tooltip_text = "Part ways (before it gets ugly)"
		rm.pressed.connect(func():
			_cofounders.erase(cf)
			_sfx_click.play()
			_refresh_capline())
		card.add_child(rm)
		var rname := _label(ROLES[int(cf["role"])].to_upper(), 26, PALETTE["cream"])
		rname.position = Vector2(0, 196)
		rname.size = Vector2(cw, 36)
		rname.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		card.add_child(rname)
		var com := Button.new()
		var ft: bool = int(cf["commitment"]) == 0
		com.text = "FULL-TIME" if ft else "PART-TIME ⚠"
		com.size = Vector2(cw - 40, 44)
		com.position = Vector2(20, 238)
		_style_button(com, PALETTE["sage"] if ft else PALETTE["yellow"], 24)
		if not ft:
			com.add_theme_color_override("font_color", PALETTE["coral"])
		com.tooltip_text = "Click to toggle. Part-timers with real equity flake at the worst time."
		com.pressed.connect(func():
			cf["commitment"] = 1 - int(cf["commitment"])
			_sfx_click.play()
			_refresh_capline())
		card.add_child(com)
		var minus := Button.new()
		minus.text = "−"
		minus.size = Vector2(48, 56)
		minus.position = Vector2(16, 296)
		_style_button(minus, PALETTE["coral"], 30)
		minus.pressed.connect(func():
			cf["equity"] = maxf(1.0, float(cf["equity"]) - 5.0)
			_refresh_capline()
			_react_crew(_cofounders.find(cf), false))
		card.add_child(minus)
		var eq := _dlabel("%.0f%%" % float(cf["equity"]), 46, PALETTE["cream"])
		eq.position = Vector2(64, 292)
		eq.size = Vector2(cw - 128, 64)
		eq.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		card.add_child(eq)
		var plus := Button.new()
		plus.text = "+"
		plus.size = Vector2(48, 56)
		plus.position = Vector2(cw - 64, 296)
		_style_button(plus, PALETTE["sage"], 30)
		plus.pressed.connect(func():
			cf["equity"] = minf(60.0, float(cf["equity"]) + 5.0)
			_refresh_capline()
			_react_crew(_cofounders.find(cf), true))
		card.add_child(plus)
		var vest := Button.new()
		vest.text = "VESTED ✓" if not no_vest else "NO VESTING ⚠"
		vest.size = Vector2(cw - 40, 44)
		vest.position = Vector2(20, 366)
		_style_button(vest, PALETTE["sage"] if not no_vest else PALETTE["coral"], 24)
		if no_vest:
			vest.add_theme_color_override("font_color", PALETTE["coral"])
		vest.tooltip_text = "4-year vesting, 1-year cliff. Turning this off is the classic mistake."
		vest.pressed.connect(func():
			cf["vesting"] = not bool(cf["vesting"])
			_sfx_click.play()
			_refresh_capline())
		card.add_child(vest)
		var mood := _label({"happy": "☀ thrilled", "neutral": "steady", "resentful": "⛈ resentful…"}[st], 27,
			{"happy": PALETTE["sage"], "neutral": Color(PALETTE["cream"], 0.85), "resentful": PALETTE["coral"]}[st])
		mood.position = Vector2(0, 434)
		mood.size = Vector2(cw, 36)
		mood.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		card.add_child(mood)
	# recruit slot
	if n < MAX_COFOUNDERS:
		var slot := Button.new()
		slot.position = Vector2(x0 + (n + 1) * (cw + gap), 0)
		slot.size = Vector2(cw, 490)
		slot.text = "☎\n+ RECRUIT"
		slot.add_theme_font_override("font", _font)
		slot.add_theme_font_size_override("font_size", 38)
		slot.add_theme_color_override("font_color", Color(PALETTE["cream"], 0.9))
		var sb := _crew_card_bg()
		sb.bg_color = Color(1, 1, 1, 0.02)
		slot.add_theme_stylebox_override("normal", sb)
		var sbh := _crew_card_bg()
		sbh.bg_color = Color(1, 1, 1, 0.08)
		slot.add_theme_stylebox_override("hover", sbh)
		slot.add_theme_stylebox_override("pressed", sbh)
		slot.pressed.connect(_add_cofounder)
		_crew_row.add_child(slot)

func _react_crew(i: int, positive: bool) -> void:
	if i < 0 or i >= _crew_sprites.size():
		return
	var node: TextureRect = _crew_sprites[i]
	if not is_instance_valid(node):
		return
	var tw := create_tween()
	if positive:
		tw.tween_property(node, "position:y", -8.0, 0.1).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
		tw.tween_property(node, "position:y", 12.0, 0.18).set_trans(Tween.TRANS_BOUNCE)
	else:
		for off in [-6.0, 6.0, -4.0, 0.0]:
			tw.tween_property(node, "rotation", off * 0.02, 0.05)

func _cf_state(cf: Dictionary, n: int) -> String:
	var eq := float(cf["equity"])
	var fair := 100.0 / float(n + 1)
	var ft: bool = int(cf["commitment"]) == 0
	var role: String = ROLES[int(cf["role"])]
	if (ft and role != "The Idea Friend" and eq < 10.0) or eq <= fair * 0.45:
		return "resentful"
	if eq >= fair * 1.15:
		return "happy"
	return "neutral"

func _render_team(founder_pct: float) -> void:
	_rebuild_crew(founder_pct)
	return

	if _team_stage == null:
		return
	# founder mini: your own archetype, drooping once you lose the majority
	if _founder_mini and not _sel_arch.is_empty():
		var still := "res://assets/sprites/%s.png" % String(_sel_arch.get("sprite", ""))
		if ResourceLoader.exists(still) and _founder_mini.texture == null:
			_founder_mini.texture = load(still)
		var droop := 0.14 if founder_pct < 50.0 else 0.0
		var ft := create_tween()
		ft.tween_property(_founder_mini, "rotation", droop, 0.3)
		_founder_mini.modulate = Color(1, 1, 1, 1) if founder_pct >= 50.0 else Color(0.85, 0.85, 0.85, 1)
	if founder_pct < _last_founder_pct - 0.5 and _founder_mini:
		var dots := _label("…", 30, PALETTE["cream"])
		dots.position = _founder_mini.position + Vector2(84, -24)
		_team_stage.add_child(dots)
		var dt := create_tween()
		dt.tween_property(dots, "position:y", dots.position.y - 30.0, 0.9)
		dt.parallel().tween_property(dots, "modulate:a", 0.0, 0.9)
		dt.tween_callback(dots.queue_free)
	_last_founder_pct = founder_pct
	# cofounder cast: create/update/remove to match the row list
	while _team_nodes.size() > _cofounders.size():
		var gone: TextureRect = _team_nodes.pop_back()
		var gt := create_tween()
		gt.tween_property(gone, "modulate:a", 0.0, 0.18)
		gt.parallel().tween_property(gone, "position:y", gone.position.y + 40.0, 0.18)
		gt.tween_callback(gone.queue_free)
	for i in _cofounders.size():
		var cf: Dictionary = _cofounders[i]
		var slug: String = ROLE_SLUGS.get(ROLES[int(cf["role"])], "technical")
		var st := _cf_state(cf, _cofounders.size())
		var path := _cf_art(slug, st)
		if i >= _team_nodes.size():
			var node := TextureRect.new()
			node.size = Vector2(104, 104)
			node.position = Vector2(132 + i * 78, -60)
			node.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
			node.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
			node.pivot_offset = Vector2(52, 104)
			node.modulate.a = 0.0
			if ResourceLoader.exists(path):
				node.texture = load(path)
			_team_stage.add_child(node)
			_team_nodes.append(node)
			var drop := create_tween()
			drop.tween_property(node, "modulate:a", 1.0, 0.1)
			drop.parallel().tween_property(node, "position:y", 252.0, 0.28).set_trans(Tween.TRANS_BOUNCE).set_ease(Tween.EASE_OUT)
			drop.tween_property(node, "scale", Vector2(1.12, 0.88), 0.07)
			drop.tween_property(node, "scale", Vector2.ONE, 0.12)
		else:
			var node2: TextureRect = _team_nodes[i]
			if ResourceLoader.exists(path) and (node2.texture == null or node2.texture.resource_path != path):
				node2.texture = load(path)

func _react_team_member(i: int, positive: bool) -> void:
	if i < 0 or i >= _team_nodes.size():
		return
	var node: TextureRect = _team_nodes[i]
	var tw := create_tween()
	if positive:
		tw.tween_property(node, "position:y", 232.0, 0.1).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
		tw.tween_property(node, "position:y", 252.0, 0.16).set_trans(Tween.TRANS_BOUNCE)
	else:
		for off in [-6.0, 6.0, -4.0, 0.0]:
			tw.tween_property(node, "rotation", off * 0.02, 0.05)

func _do_launch() -> void:
	var cfs: Array = []
	for cf in _cofounders:
		cfs.append({"role": ROLES[int(cf["role"])], "commitment": COMMITMENTS[int(cf["commitment"])], "equity": float(cf["equity"]), "vesting": bool(cf["vesting"])})
	var trap_ids: Array[String] = []
	for t in _compute_traps():
		if not trap_ids.has(String(t["id"])):
			trap_ids.append(String(t["id"]))
	done.emit({
		"archetype": _sel_arch,
		"cofounders": cfs,
		"funding": _sel_fund,
		"company_name": _name_edit.value(),
		"company_idea": _idea_edit.value(),
		"biz_what": _biz_what,
		"biz_who": _biz_who,
		"items": _bag.duplicate(),
		"traps": trap_ids,
	})


class BlobPlaceholder:
	extends Control
	## stands in for a portrait that has not been drawn yet, in the same ink as
	## everything else, so a missing file never leaves a hole in the layout
	func _draw() -> void:
		var ink := Color("1E1E1E", 0.30)
		var c := Vector2(size.x * 0.5, size.y * 0.56)
		var r := minf(size.x, size.y) * 0.30
		var pts := PackedVector2Array()
		for i in 40:
			var t := i / 39.0 * TAU
			pts.append(c + Vector2(cos(t) * r * 0.82, sin(t) * r * 1.15))
		pts.append(pts[0])
		draw_polyline(pts, ink, 3.0)
		draw_line(c + Vector2(-r * 0.5, r * 1.2), c + Vector2(-r * 0.5, r * 1.7), ink, 3.0)
		draw_line(c + Vector2(r * 0.5, r * 1.2), c + Vector2(r * 0.5, r * 1.7), ink, 3.0)

class HandRule:
	extends Control
	## a rule drawn with a pen: it wobbles and it clears the descenders
	var length := 200.0
	var color := Color("E86A5C")
	func _draw() -> void:
		var pts := PackedVector2Array()
		for i in 30:
			var t := i / 29.0
			pts.append(Vector2(t * length, 6.0 + sin(t * 5.0) * 1.8 + sin(t * 14.0) * 0.8))
		draw_polyline(pts, color, 6.0)

class PaperEdge:
	extends Control
	## a card cut from the same paper as the journal: a real shadow, a cream
	## body, and an inked border that was drawn by a hand, not a border-radius
	var edge := Color("1E1E1E")
	var thick := 4.0
	var lean := 0
	func _draw() -> void:
		var w := size.x
		var h := size.y
		draw_rect(Rect2(7.0, 9.0, w, h), Color(0, 0, 0, 0.18))
		draw_rect(Rect2(0, 0, w, h), Color("F2EAD3"))
		# the wobble walks the whole perimeter as one continuous stroke — wobbling
		# each edge on its own splits the corners into spikes
		var inset := thick * 0.5 + 3.0
		var corners := [Vector2(inset, inset), Vector2(w - inset, inset),
			Vector2(w - inset, h - inset), Vector2(inset, h - inset)]
		var per := 2.0 * (w + h)
		var pts := PackedVector2Array()
		var acc := 0.0
		for i in 4:
			var a: Vector2 = corners[i]
			var b: Vector2 = corners[(i + 1) % 4]
			var seg := a.distance_to(b)
			var n := Vector2(b.y - a.y, a.x - b.x).normalized()
			for k in 13:
				var t := k / 12.0
				var u := (acc + seg * t) / per * TAU
				pts.append(a.lerp(b, t) + n * (sin(u * 3.0 + lean) * 2.1 + sin(u * 7.0 + lean) * 1.0))
			acc += seg
		pts.append(pts[0])
		draw_polyline(pts, edge, thick, true)

class RadarChart:
	extends Control
	var stats: Dictionary = {}
	var progress := 1.0   # 0..1 sweep-in animation
	var line_color := Color("1E1E1E")
	var fill_color := Color(Color("E86A5C"), 0.45)
	var edge_color := Color("E86A5C")
	var label_color := Color(Color("1E1E1E"), 0.7)

	func set_stats(s: Dictionary) -> void:
		stats = s
		queue_redraw()

	func _ready() -> void:
		queue_redraw()

	func _draw() -> void:
		var center := size / 2.0
		var radius := minf(size.x, size.y) / 2.0 - 20.0
		var n := FounderDraftScreen.STAT_NAMES.size()
		for ring in [0.33, 0.66, 1.0]:
			var pts := PackedVector2Array()
			for i in n + 1:
				var ang := -PI / 2.0 + TAU * i / n
				pts.append(center + Vector2(cos(ang), sin(ang)) * radius * ring)
			draw_polyline(pts, Color(line_color, 0.25), 1.5)
		var font: Font = load("res://assets/fonts/PatrickHand-Regular.ttf")
		for i in n:
			var ang := -PI / 2.0 + TAU * i / n
			var tip := center + Vector2(cos(ang), sin(ang)) * radius
			draw_line(center, tip, Color(line_color, 0.25), 1.5)
			var lpos := center + Vector2(cos(ang), sin(ang)) * (radius + 14.0)
			draw_string(font, lpos + Vector2(-20, 5), FounderDraftScreen.STAT_LABELS[i], HORIZONTAL_ALIGNMENT_CENTER, 48, 14, label_color)
		var poly := PackedVector2Array()
		for i in n:
			var ang := -PI / 2.0 + TAU * i / n
			var v := float(stats.get(FounderDraftScreen.STAT_NAMES[i], 0)) / 5.0 * clampf(progress, 0.02, 1.0)
			poly.append(center + Vector2(cos(ang), sin(ang)) * radius * v)
		draw_colored_polygon(poly, fill_color)
		poly.append(poly[0])
		draw_polyline(poly, edge_color, 3.5)


class StatPips:
	extends Control
	## Five chunky pip rows — the stat display a player reads in half a second.
	var stats: Dictionary = {}
	var progress := 1.0

	func set_stats(v: Dictionary) -> void:
		stats = v
		queue_redraw()

	func _draw() -> void:
		var font: Font = load("res://assets/fonts/Baloo2-Bold.ttf")
		var n := FounderDraftScreen.STAT_NAMES.size()
		for i in n:
			var y := i * 58.0
			draw_string(font, Vector2(0, y + 34), FounderDraftScreen.STAT_LABELS[i], HORIZONTAL_ALIGNMENT_LEFT, 150, 27, Color("1E1E1E"))
			var v := int(stats.get(FounderDraftScreen.STAT_NAMES[i], 0))
			var revealed := float(v) * clampf(progress, 0.0, 1.0)
			for pp in 5:
				var r := Rect2(160 + pp * 62, y + 8, 50, 30)
				if pp < int(round(revealed)):
					draw_rect(r, Color("E86A5C"))
					draw_rect(r, Color("1E1E1E"), false, 2.5)
				else:
					draw_rect(r, Color(Color("1E1E1E"), 0.06))
					draw_rect(r, Color(Color("1E1E1E"), 0.32), false, 2.0)


class EllipseShadow:
	extends Control
	func _draw() -> void:
		var pts := PackedVector2Array()
		var c := size / 2.0
		for i in 40:
			var a := TAU * i / 40.0
			pts.append(c + Vector2(cos(a) * c.x, sin(a) * c.y))
		draw_colored_polygon(pts, Color(0.04, 0.04, 0.04, 0.35))


class EllipseGlow:
	extends Control
	func _draw() -> void:
		var c := size / 2.0
		for layer in [[1.0, 0.10], [0.8, 0.10], [0.6, 0.12]]:
			var pts := PackedVector2Array()
			for i in 48:
				var a := TAU * i / 48.0
				pts.append(c + Vector2(cos(a) * c.x * layer[0], sin(a) * c.y * layer[0]))
			draw_colored_polygon(pts, Color(Color("F2EAD3"), layer[1]))


class SpotlightFallback:
	extends Control
	## Procedural stage if the painted backdrop is missing: cone + pool + floor line.
	func _draw() -> void:
		var w := size.x
		var h := size.y
		draw_rect(Rect2(0, h * 0.78, w, h * 0.22), Color("2C343B"))
		draw_line(Vector2(0, h * 0.78), Vector2(w, h * 0.78), Color(0.05, 0.05, 0.05), 3.0)
		var cone := PackedVector2Array([
			Vector2(w * 0.42, 0), Vector2(w * 0.58, 0),
			Vector2(w * 0.72, h * 0.86), Vector2(w * 0.28, h * 0.86),
		])
		draw_colored_polygon(cone, Color(Color("F2EAD3"), 0.14))
		var pool := PackedVector2Array()
		var c := Vector2(w * 0.5, h * 0.86)
		for i in 48:
			var a := TAU * i / 48.0
			pool.append(c + Vector2(cos(a) * w * 0.23, sin(a) * h * 0.05))
		draw_colored_polygon(pool, Color(Color("F2EAD3"), 0.20))


class CapTableDonut:
	extends Control
	var slices: Array = []          # displayed (animated) slices
	var founder_pct := 100.0        # displayed (animated) center number
	var text_color := Color("1E1E1E")
	var _anim: Tween

	func set_slices(target: Array, target_founder: float) -> void:
		# never teleport: sweep the slices and count the number
		if _anim and _anim.is_valid():
			_anim.kill()
		var from_slices := slices.duplicate(true)
		var from_founder := founder_pct
		# align slice counts for interpolation
		while from_slices.size() < target.size():
			from_slices.append({"pct": 0.0, "color": target[from_slices.size()]["color"]})
		while target.size() < from_slices.size():
			target.append({"pct": 0.0, "color": from_slices[target.size()]["color"]})
		_anim = create_tween()
		_anim.tween_method(func(t: float):
			var mix: Array = []
			for i in target.size():
				mix.append({
					"pct": lerpf(float(from_slices[i]["pct"]), float(target[i]["pct"]), t),
					"color": target[i]["color"],
				})
			slices = mix
			founder_pct = lerpf(from_founder, target_founder, t)
			queue_redraw(), 0.0, 1.0, 0.45).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)

	func _draw() -> void:
		var center := size / 2.0
		var r_out := minf(size.x, size.y) / 2.0 - 4.0
		var r_in := r_out * 0.55
		var start := -PI / 2.0
		var use := slices if not slices.is_empty() else [{"pct": 100.0, "color": Color("8FA582")}]
		for s in use:
			var sweep: float = TAU * clampf(float(s["pct"]), 0.0, 100.0) / 100.0
			var steps: int = maxi(6, int(sweep / 0.08))
			var pts := PackedVector2Array()
			for i in steps + 1:
				var a := start + sweep * i / steps
				pts.append(center + Vector2(cos(a), sin(a)) * r_out)
			for i in steps + 1:
				var a := start + sweep - sweep * i / steps
				pts.append(center + Vector2(cos(a), sin(a)) * r_in)
			draw_colored_polygon(pts, s["color"])
			start += sweep
		var font: Font = load("res://assets/fonts/Baloo2-Bold.ttf")
		draw_string(font, center + Vector2(-42, 4), "%.0f%%" % founder_pct, HORIZONTAL_ALIGNMENT_CENTER, 90, 34, text_color)
		draw_string(font, center + Vector2(-32, 32), "yours", HORIZONTAL_ALIGNMENT_CENTER, 64, 18, Color(text_color, 0.7))
