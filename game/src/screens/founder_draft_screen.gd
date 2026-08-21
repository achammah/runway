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
var _fund_btns: Array = []
var _bag: Array[String] = []
var _bag_btns: Dictionary = {}
var _name_edit: PaperInput
var _founder_edit: PaperInput
var _prng := RandomNumberGenerator.new()   # person-name suggestions
var _idea_edit: PaperInput
var _donut: Control
var _launch: Button
var _anim_frames: Dictionary = {}
var _anim_i := 0
var _anim_timer: Timer
var _sfx_click: AudioStreamPlayer
var _hero_base_y := 0.0
var _hero_tween: Tween
var _title_label: Label
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
var _packed_row: VBoxContainer
var _biz_what := "Software"
var _biz_who := "Consumer"
var _name_witness: TextureRect
var _spinning := false
var _what_chips: Array = []
var _who_chips: Array = []
var _crew_body: Control
var _empty_note: Label
var _ink_cache: Dictionary = {}



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
	# THE FIRST FRAME LOADS NOW, THE REST LOAD WHILE YOU LOOK (owner: "weird
	# latency between startup and character selection" — 144 synchronous frame
	# loads was 1.4s of it). Each archetype gets frame 01 immediately; a
	# background hydrator adds ~6 frames per idle frame until the loops are full.
	var pending: Array = []   # [aid, path] in load order
	for arch in _archs:
		var aid := String(arch["id"])
		var frames: Array = []
		var first := "res://assets/sprites/chr_loop_%s_01.png" % aid
		if ResourceLoader.exists(first):
			frames.append(load(first))
			var i := 2
			while ResourceLoader.exists("res://assets/sprites/chr_loop_%s_%02d.png" % [aid, i]):
				pending.append([aid, "res://assets/sprites/chr_loop_%s_%02d.png" % [aid, i]])
				i += 1
		else:
			var still := "res://assets/sprites/%s.png" % String(arch.get("sprite", ""))
			if ResourceLoader.exists(still):
				frames.append(load(still))
		_anim_frames[aid] = frames
	if not pending.is_empty():
		var hydrate := func() -> void:
			var n := 0
			while n < 6 and not pending.is_empty():
				var job: Array = pending.pop_front()
				(_anim_frames[String(job[0])] as Array).append(load(String(job[1])))
				n += 1
		var ht := Timer.new()
		ht.wait_time = 0.05
		ht.timeout.connect(func() -> void:
			hydrate.call()
			if pending.is_empty():
				ht.queue_free())
		add_child(ht)
		ht.start()

	_pages = [_build_sign_page(), _build_select(), _build_name(), _build_shape_page(), _build_crew_page(), _build_money_page(), _build_bag_page()]
	# the shelf depends on the trade chosen on page 3: rebuild it on entry
	_page_shown.connect(func(i: int) -> void:
		if i == 1:
			_hero_entrance()
		if i == 6:
			var old_bag: Control = _pages[6]
			_pages[6] = _build_bag_page()
			_pages[6].visible = true
			old_bag.queue_free())
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

## Object art is decomposed out of generated scenes, and a few sheets came back
## carrying a fragment of the object that stood next to them: a guitar peg under
## the ping-pong paddle, a sliver beside the savings jar, a chip below the old
## server. At cell size those read as dirt on the screen, and they also shrink
## the real object, because an aspect fit has to leave room for them. So an
## object is drawn through its own ink: the alpha is profiled, ink that is
## stranded far from the main mass is cut away, and what is left is served as an
## AtlasTexture — the same trim the consultant's loop frames already use.
## Characters are NOT run through this; their framing is deliberate.
func _clean_tex(path: String) -> Texture2D:
	if _ink_cache.has(path):
		return _ink_cache[path]
	if not ResourceLoader.exists(path):
		_ink_cache[path] = null
		return null
	var tex: Texture2D = load(path)
	var out: Texture2D = tex
	var img: Image = tex.get_image() if tex else null
	if img != null:
		if img.is_compressed():
			img.decompress()
		if img.get_format() != Image.FORMAT_RGBA8:
			img.convert(Image.FORMAT_RGBA8)
		var w := img.get_width()
		var h := img.get_height()
		var data := img.get_data()
		# profiled at every second pixel: the fragments are tens of pixels wide,
		# and a full scan of every item sheet is time the player waits for
		var pw := (w + 1) / 2
		var ph := (h + 1) / 2
		var cols := PackedInt32Array()
		cols.resize(pw)
		var rows := PackedInt32Array()
		rows.resize(ph)
		var y := 0
		while y < h:
			var base := y * w * 4
			var x := 0
			while x < w:
				if data[base + x * 4 + 3] > 24:
					rows[y >> 1] += 1
					cols[x >> 1] += 1
				x += 2
			y += 2
		var vs := _main_span(rows, ph)
		var hs := _main_span(cols, pw)
		var rx := maxi(0, hs.x * 2 - 1)
		var ry := maxi(0, vs.x * 2 - 1)
		var rw := mini(w - rx, (hs.y - hs.x) * 2 + 2)
		var rh := mini(h - ry, (vs.y - vs.x) * 2 + 2)
		if rw > 8 and rh > 8 and (rx > 0 or ry > 0 or rw < w or rh < h):
			var at := AtlasTexture.new()
			at.atlas = tex
			at.region = Rect2(rx, ry, rw, rh)
			out = at
	_ink_cache[path] = out
	return out

## The run of ink to keep along one axis. Ink stranded more than a tenth of the
## sprite away from the main mass is a leftover from a neighbour; ink that is
## merely detached — the sleeping roommate's zZZ, three pixels above his head —
## belongs to the object and stays. Size cannot tell those apart. Distance can,
## so the test is the width of the GAP, with a size guard so a genuinely large
## second mass is never cut.
func _main_span(prof: PackedInt32Array, n: int) -> Vector2i:
	var runs: Array = []
	var st := -1
	var ink := 0
	for i in n:
		if prof[i] > 0:
			if st < 0:
				st = i
				ink = 0
			ink += prof[i]
		elif st >= 0:
			runs.append([st, i, ink])
			st = -1
	if st >= 0:
		runs.append([st, n, ink])
	if runs.is_empty():
		return Vector2i(0, n)
	var best := 0
	for i in runs.size():
		if int(runs[i][2]) > int(runs[best][2]):
			best = i
	var gap_max := int(round(float(n) * 0.10))
	var keep_ink: int = int(runs[best][2]) / 5
	var lo: int = int(runs[best][0])
	var hi: int = int(runs[best][1])
	var b := best - 1
	while b >= 0:
		if lo - int(runs[b][1]) > gap_max and int(runs[b][2]) < keep_ink:
			break
		lo = int(runs[b][0])
		b -= 1
	var f := best + 1
	while f < runs.size():
		if int(runs[f][0]) - hi > gap_max and int(runs[f][2]) < keep_ink:
			break
		hi = int(runs[f][1])
		f += 1
	return Vector2i(lo, hi)

func _show_page(i: int) -> void:
	_page = i
	for p in _pages.size():
		_pages[p].visible = p == i
	_page_shown.emit(i)
	if i == 2 and _name_witness and is_instance_valid(_name_witness):
		var whome := _name_witness.position
		_name_witness.position = whome + Vector2(0, 40)
		_name_witness.modulate.a = 0.0
		var wtw := create_tween().set_parallel(true)
		wtw.tween_property(_name_witness, "position", whome, 0.3).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
		wtw.tween_property(_name_witness, "modulate:a", 1.0, 0.24)
	if i == 2 and _name_witness and not _sel_arch.is_empty():
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
	# NO box. This sets type and colour only. Every fill on these screens is
	# DRAWN — _paper_card lays real paper under the word, _ink_button rings it
	# in pen — so a call that forgets one falls back to bare text on the stage,
	# never to a cream rectangle with a printed border.
	for st_name in ["normal", "hover", "pressed", "disabled", "focus"]:
		b.add_theme_stylebox_override(st_name, StyleBoxEmpty.new())

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
		b.add_theme_stylebox_override(st_name, StyleBoxEmpty.new())
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

## Strips a Button down to its hit box — no fill, no border, no content margins.
## What is left on screen is whatever art the button carries, so the thing you
## click is the OBJECT and not a tile with an object printed on it.
func _bare_button(b: Button) -> void:
	b.add_theme_font_override("font", _font_d)
	b.add_theme_color_override("font_color", PALETTE["ink"])
	for st_name in ["normal", "hover", "pressed", "disabled", "focus"]:
		b.add_theme_stylebox_override(st_name, StyleBoxEmpty.new())

## A control that is a pen MARK rather than a box: a wobbly outline, no fill.
## Used for every toggle and counter that used to be a filled rectangle, and for
## the ring that circles a chosen thing the way the log book circles a choice.
func _ink_button(b: Button, col: Color, fsize: int, ring: bool = false) -> void:
	b.add_theme_font_override("font", _font_d)
	b.add_theme_font_size_override("font_size", fsize)
	for cn in ["font_color", "font_hover_color", "font_pressed_color", "font_focus_color"]:
		b.add_theme_color_override(cn, PALETTE["ink"])
	b.add_theme_color_override("font_disabled_color", Color(PALETTE["ink"], 0.4))
	for st_name in ["normal", "hover", "pressed", "disabled", "focus"]:
		b.add_theme_stylebox_override(st_name, StyleBoxEmpty.new())
	# the Button paints its own text FIRST and children paint after, so an
	# outline-only child rings the word instead of blanking it — no cap needed
	var tag := InkTag.new()
	tag.name = "edge"
	tag.color = col
	tag.shape = 1 if ring else 0
	tag.wobble_seed = int(absf(b.position.x) + absf(b.position.y)) % 9
	tag.set_anchors_preset(Control.PRESET_FULL_RECT)
	tag.mouse_filter = Control.MOUSE_FILTER_IGNORE
	b.add_child(tag)
	# the pen presses harder under the cursor, the same tell PaperInput uses
	b.mouse_entered.connect(func():
		tag.thick = 6.5
		tag.queue_redraw())
	b.mouse_exited.connect(func():
		tag.thick = 3.5
		tag.queue_redraw())

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
	_hero_shadow.position = Vector2(420, 796)
	_hero_shadow.size = Vector2(300, 46)
	page.add_child(_hero_shadow)
	_hero = TextureRect.new()
	_hero.position = Vector2(220, 240)
	_hero.size = Vector2(560, 560)
	_hero.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_hero.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	_hero.pivot_offset = Vector2(280, 560)
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

	# roster: the cast stands ON one sheet of paper. They used to be cream tiles
	# with printed borders laid on that same cream sheet — a plate on a plate,
	# and the unpicked ones turned into grey cards. Now the sheet is the only
	# drawn surface and each founder is an object standing on it, with a contact
	# shadow at the feet; the pick is a coral pen ring, per the log book.
	var row_w := _archs.size() * 142 - 14
	var x0 := (1536 - row_w) / 2.0
	var dock := PaperEdge.new()
	dock.size = Vector2(row_w + 56, 140)
	dock.position = Vector2(x0 - 28, DOCK_BAND_TOP)
	dock.thick = 4.0
	dock.lean = 3
	dock.mouse_filter = Control.MOUSE_FILTER_IGNORE
	page.add_child(dock)
	for i in _archs.size():
		var chip := Button.new()
		chip.position = Vector2(x0 + i * 142, DOCK_BAND_TOP + 12.0)
		chip.size = Vector2(128, 128)
		chip.pivot_offset = Vector2(64, 128)
		_bare_button(chip)
		# the shadow must be a CHILD painted before the portrait: a Button paints
		# its own icon first and children over it, so chip.icon would sit UNDER
		# the shadow. The portrait is a child too, so the stack stays honest.
		var csh := EllipseShadow.new()
		csh.position = Vector2(22, 106)
		csh.size = Vector2(84, 16)
		csh.mouse_filter = Control.MOUSE_FILTER_IGNORE
		chip.add_child(csh)
		var port := TextureRect.new()
		port.name = "port"
		port.size = Vector2(128, 118)
		port.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		port.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		port.mouse_filter = Control.MOUSE_FILTER_IGNORE
		chip.add_child(port)
		var still := "res://assets/sprites/%s.png" % String(_archs[i].get("sprite", ""))
		if ResourceLoader.exists(still):
			port.texture = load(still)
		var ring := InkTag.new()
		ring.name = "ring"
		ring.color = PALETTE["coral"]
		ring.shape = 1
		ring.thick = 5.0
		ring.wobble_seed = i
		ring.position = Vector2(-9, -8)
		ring.size = Vector2(146, 144)
		ring.visible = false
		ring.mouse_filter = Control.MOUSE_FILTER_IGNORE
		chip.add_child(ring)
		chip.pressed.connect(_select.bind(i))
		chip.mouse_entered.connect(func():
			if _chips.find(chip) != _sel_i:
				chip.position.y = DOCK_BAND_TOP + 4.0)
		chip.mouse_exited.connect(func():
			if _chips.find(chip) != _sel_i:
				chip.position.y = DOCK_BAND_TOP + 12.0)
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
	_paper_card(_lockin_btn)
	_lockin_btn.pressed.connect(_lock_in)
	page.add_child(_lockin_btn)
	return page

## the hero walks IN (owner: select needed motion): slide from stage left
## with a settle, shadow fading up under the feet
func _hero_entrance() -> void:
	if _hero == null or not is_instance_valid(_hero):
		return
	var home := _hero.position
	_hero.position = home + Vector2(-90, 0)
	_hero.modulate.a = 0.0
	if _hero_shadow != null and is_instance_valid(_hero_shadow):
		_hero_shadow.modulate.a = 0.0
		var st := create_tween()
		st.tween_interval(0.12)
		st.tween_property(_hero_shadow, "modulate:a", 1.0, 0.22)
	var tw := create_tween().set_parallel(true)
	tw.tween_property(_hero, "position", home, 0.34).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	tw.tween_property(_hero, "modulate:a", 1.0, 0.22)

func _select(i: int, animate_swap: bool = true) -> void:
	var prev := _sel_i
	_sel_i = wrapi(i, 0, _archs.size())
	_sel_arch = _archs[_sel_i]
	_sfx_click.pitch_scale = 0.9 + 0.08 * _sel_i
	_sfx_click.play()
	for c in _chips.size():
		var chip: Button = _chips[c]
		var selected := c == _sel_i
		# only the PORTRAIT mutes, never the ring or the shadow, and it mutes by
		# a light grey multiply at FULL opacity — fading ink toward a cream
		# ground is what washed these out the last time they were touched
		var port := chip.get_node_or_null("port")
		if port:
			port.modulate = Color.WHITE if selected else Color(0.74, 0.74, 0.74, 1.0)
		var ring := chip.get_node_or_null("ring")
		if ring:
			ring.visible = selected
		var target_y := (DOCK_BAND_TOP + 2.0) if selected else (DOCK_BAND_TOP + 12.0)
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
		_transition_to(2))

## Curtain-wipe page transition: two night panels close, page swaps, they open.
## PAGE 0 — WHO IS DOING THIS (owner: the name comes before the character).
## One big write-in, prefilled with a dealt name, redealt on a whim. Everything
## after this — archetype, company, world — happens to THIS person.
func _build_sign_page() -> Control:
	var page := Control.new()
	page.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(page)
	_dim(page)
	var title := _ink_outline(_dlabel("FIRST, YOUR NAME", 58, PALETTE["cream"]))
	title.position = Vector2(470, 200)
	page.add_child(title)
	_rule_under(page, "FIRST, YOUR NAME", 58, Vector2(470, 200))
	var sub := _label("it goes on the lease, the deck, and every apology", 28, Color(PALETTE["cream"], 0.8))
	sub.position = Vector2(478, 296)
	page.add_child(sub)
	_prng.randomize()
	_founder_edit = PaperInput.new()
	_founder_edit.setup("SIGNED", "", 52)
	_founder_edit.set_value(WorldGen.person_name(_prng))
	_founder_edit.position = Vector2(438, 400)
	_founder_edit.set_deferred("size", Vector2(660, 150))
	page.add_child(_founder_edit)
	var redeal := Button.new()
	redeal.text = "DEAL ME ANOTHER"
	redeal.position = Vector2(568, 596)
	redeal.size = Vector2(400, 62)
	redeal.pivot_offset = Vector2(200, 31)
	_style_button(redeal, PALETTE["yellow"], 24)
	_paper_card(redeal)
	_juice(redeal)
	redeal.pressed.connect(func():
		_founder_edit.set_value(WorldGen.person_name(_prng))
		_sfx_click.play())
	page.add_child(redeal)
	var next := Button.new()
	next.text = "CHOOSE YOUR FOUNDER  →"
	next.position = Vector2(1010, 900)
	next.size = Vector2(470, 76)
	next.pivot_offset = Vector2(235, 38)
	_style_button(next, PALETTE["cream"], 30)
	_paper_card(next)
	_juice(next)
	next.pressed.connect(func():
		if _founder_edit.value().strip_edges() == "":
			_founder_edit.set_value(WorldGen.person_name(_prng))
		_sfx_click.play()
		_transition_to(1))
	page.add_child(next)
	return page

signal _page_shown(i: int)

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
	witness.position = Vector2(130, 540)
	witness.size = Vector2(330, 410)
	witness.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	witness.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	page.add_child(witness)
	_name_witness = witness
	var wsh := EllipseShadow.new()
	wsh.position = Vector2(210, 934)
	wsh.size = Vector2(190, 32)
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
	_juice(back)
	back.pressed.connect(func(): _transition_to(1))
	page.add_child(back)
	var next := Button.new()
	next.text = "TO THE FOUNDING  →"
	next.position = Vector2(1150, 890)
	next.size = Vector2(340, 84)
	_style_button(next, PALETTE["coral"], 32)
	_paper_card(next)
	next.pressed.connect(func():
		_sfx_click.play()
		_transition_to(3))
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
	["Service", "itm_idea_napkin", "You ARE the product. Revenue on day one, margins made of hours."],
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
	# four WHATs share the row three used to fill: the cards slim down together
	var wcard: float = 340.0 if SHAPE_WHAT.size() > 3 else 440.0
	var wstep: float = (1536.0 - 128.0 - wcard) / float(maxi(SHAPE_WHAT.size() - 1, 1))
	for i in SHAPE_WHAT.size():
		var card := _shape_card(SHAPE_WHAT[i], Vector2(64 + float(i) * wstep, 226), true, wcard)
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
	_juice(back)
	back.pressed.connect(func(): _transition_to(2))
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
		_transition_to(4))
	page.add_child(next)
	return page

func _shape_card(spec: Array, pos: Vector2, is_what: bool, w: float = 440.0) -> Button:
	var card := Button.new()
	card.position = pos
	card.size = Vector2(w, 300)
	card.pivot_offset = Vector2(w * 0.5, 150)
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
	icon.position = Vector2(w * 0.5 - 62.0, 12)
	icon.size = Vector2(124, 124)
	icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	icon.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(icon)
	icon.texture = _clean_tex("res://assets/sprites/%s.png" % String(spec[1]))
	var nm := _dlabel(String(spec[0]).to_upper(), 30, PALETTE["ink"])
	nm.name = "nm"
	nm.position = Vector2(20, 128)
	nm.size = Vector2(w - 40.0, 44)
	nm.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	nm.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(nm)
	var ds := _label(String(spec[2]), 27 if w >= 440.0 else 24, Color(PALETTE["ink"], 0.88))
	ds.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	ds.custom_minimum_size = Vector2(w - 56.0, 0)
	ds.position = Vector2(28, 184)
	ds.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	ds.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(ds)
	ds.set_deferred("size", Vector2(w - 56.0, 0))
	# the picked-state check chip
	var chk := Label.new()
	chk.name = "chk"
	chk.text = "✓"
	chk.add_theme_font_override("font", _font)
	chk.add_theme_font_size_override("font_size", 34)
	chk.add_theme_color_override("font_color", PALETTE["coral"])
	chk.position = Vector2(w - 44.0, 6)
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

	# everything this page owns hangs off ONE holder, so opening the recruit call
	# can take the whole screen away instead of dimming it. A dim leaves the title
	# and the cap table legible under the modal, and then two headings are on
	# screen at once and the player cannot tell which one they are answering.
	_crew_body = Control.new()
	_crew_body.set_anchors_preset(Control.PRESET_FULL_RECT)
	_crew_body.mouse_filter = Control.MOUSE_FILTER_IGNORE
	page.add_child(_crew_body)

	var title := _ink_outline(_dlabel("THE CREW", 56, PALETTE["cream"]))
	title.position = Vector2(60, 26)
	_crew_body.add_child(title)
	_rule_under(_crew_body, "THE CREW", 56, Vector2(60, 26))
	var sub := _label("Recruit cofounders. Split the company. They will remember the split.", 28, Color(PALETTE["cream"], 0.85))
	sub.position = Vector2(64, 116)
	_crew_body.add_child(sub)

	_crew_row = Control.new()
	_crew_row.position = Vector2(24, 190)
	_crew_row.size = Vector2(1176, 512)
	_crew_body.add_child(_crew_row)

	# the cap table is a chart pinned up beside the crew, not a pie floating in
	# the dark: it is drawn on paper and captioned in the founder's own hand
	var dsheet := PaperEdge.new()
	dsheet.position = Vector2(1214, 190)
	dsheet.size = Vector2(258, 310)
	dsheet.thick = 4.0
	dsheet.lean = 2
	dsheet.rotation = 0.012
	dsheet.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_crew_body.add_child(dsheet)
	_donut = CapTableDonut.new()
	(_donut as CapTableDonut).text_color = PALETTE["ink"]
	_donut.position = Vector2(1240, 206)
	_donut.size = Vector2(210, 210)
	_crew_body.add_child(_donut)
	var dcap := _label("the cap table", 26, Color(PALETTE["ink"], 0.6))
	dcap.position = Vector2(1236, 430)
	dcap.size = Vector2(218, 36)
	dcap.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_crew_body.add_child(dcap)

	var back := Button.new()
	back.text = "←"
	back.position = Vector2(48, 930)
	back.size = Vector2(100, 70)
	_style_button(back, PALETTE["blue"], 30)
	_paper_card(back)
	_juice(back)
	back.pressed.connect(func(): _transition_to(3))
	_crew_body.add_child(back)
	var next := Button.new()
	next.text = "NEXT: FIRST MONEY  →"
	next.position = Vector2(1090, 920)
	next.size = Vector2(410, 84)
	_style_button(next, PALETTE["coral"], 32)
	_paper_card(next)
	_juice(next)
	next.pressed.connect(func():
		_sfx_click.play()
		_transition_to(5))
	_crew_body.add_child(next)

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

## The call takes the screen. Not a dim over the crew — a dim leaves the ghost
## of the cards, the donut and a SECOND title showing through, and the eye cannot
## tell which screen it is on. The crew is hidden outright; what stays behind the
## cards is the same stage every other page in this flow stands on.
func _close_recruit() -> void:
	_recruit_layer.visible = false
	if _crew_body:
		_crew_body.visible = true

func _open_recruit() -> void:
	for c in _recruit_layer.get_children():
		c.queue_free()
	_recruit_layer.visible = true
	if _crew_body:
		_crew_body.visible = false
	var shade := ColorRect.new()
	shade.color = Color(0.05, 0.05, 0.06, 0.55)
	shade.size = Vector2(1536, 1024)
	_recruit_layer.add_child(shade)
	shade.gui_input.connect(func(ev):
		if ev is InputEventMouseButton and ev.pressed:
			_close_recruit())
	# no board behind this. It used to be a dark rounded rectangle with a cream
	# border — a web modal. The dimmed stage IS the modal; the five paper cards
	# lie straight on it, the way the money cards lie on the stage one page on.
	var t := _ink_outline(_dlabel("WHO DO YOU CALL?", 48, PALETTE["yellow"]), 7)
	t.position = Vector2(188, 214)
	_recruit_layer.add_child(t)
	_rule_under(_recruit_layer, "WHO DO YOU CALL?", 48, Vector2(188, 214))
	for i in ROLES.size():
		var role: String = ROLES[i]
		var taken := _role_taken(i)
		var card := Button.new()
		card.position = Vector2(188 + i * 236, 306)
		card.size = Vector2(220, 424)
		card.rotation = [-0.007, 0.005, -0.004, 0.006, -0.005][i % 5]
		card.pivot_offset = Vector2(110, 212)
		_style_button(card, PALETTE["yellow"], 20)
		_paper_card(card)
		if not taken:
			_juice(card)
		card.pressed.connect(func():
			_close_recruit()
			_cofounders.append({"role": i, "commitment": 0, "equity": 25.0, "vesting": true, "fresh": true,
				"name": WorldGen.person_name(_prng)})
			_sfx_click.play()
			_refresh_capline())
		_recruit_layer.add_child(card)
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
		var nm := _dlabel(role.to_upper(), 26, PALETTE["ink"])
		nm.position = Vector2(10, 216)
		nm.size = Vector2(200, 40)
		nm.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		card.add_child(nm)
		var gv := _label(String(ROLE_INFO[role]["gives"]), 25, PALETTE["ink"])
		gv.position = Vector2(10, 262)
		gv.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		_wrap(gv, 200.0)
		card.add_child(gv)
		if taken:
			# already on the cap table: scribbled out in pen, not dimmed. Fading
			# the sheet turned the paper brown, which reads as dirty, not spent.
			card.disabled = true
			spr.modulate = Color(0.62, 0.62, 0.62, 1.0)
			var x := PenCross.new()
			x.size = card.size
			x.mouse_filter = Control.MOUSE_FILTER_IGNORE
			card.add_child(x)
			var got := _dlabel("ON BOARD", 26, PALETTE["coral"])
			got.position = Vector2(10, 372)
			got.size = Vector2(200, 36)
			got.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
			card.add_child(got)
	# the ask is the same for every role — stating it five times is the screen
	# repeating itself, so it is said once, under the row
	# the ask and the way out are ONE line under the row: same band, same type
	# size, the button's right edge on the last card's right edge. They used to
	# sit at two heights and two weights, which is what made the bottom scatter.
	var ask := _label("whoever you call will want ~25% of the company.", 28, PALETTE["coral"])
	ask.position = Vector2(190, 764)
	ask.size = Vector2(760, 62)
	ask.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_recruit_layer.add_child(ask)
	var cancel := Button.new()
	cancel.text = "☎ nobody. hang up."
	cancel.position = Vector2(1032, 764)
	cancel.size = Vector2(320, 62)
	_style_button(cancel, PALETTE["blue"], 28)
	_paper_card(cancel)
	_juice(cancel)
	cancel.pressed.connect(_close_recruit)
	_recruit_layer.add_child(cancel)

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
		"bootstrap": {"icon": "itm_savings_jar", "big": "+$0", "cost": "you keep 100%", "cost_col": "sage", "flavor": "Your savings and nothing else. Ramen. Focus. Freedom."},
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
		icon.texture = _clean_tex("res://assets/sprites/%s.png" % String(d.get("icon", "")))
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
	_juice(back)
	back.pressed.connect(func(): _transition_to(4))
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
		_transition_to(6))
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

	# EVERYTHING YOU OWN — one sheet of paper, your things laid out ON it.
	# They used to be fifteen cream tiles with printed borders: a sticker sheet.
	# The tile was doing one real job, giving dark ink art a light ground on a
	# near-black stage, so that job moved to ONE drawn surface and the items
	# became objects on it, each with a contact shadow and a pen ring when packed.
	var shelf := PaperEdge.new()
	shelf.position = Vector2(44, 166)
	shelf.size = Vector2(676, 566)
	shelf.thick = 4.0
	shelf.lean = 2
	shelf.mouse_filter = Control.MOUSE_FILTER_IGNORE
	page.add_child(shelf)
	var shelf_cap := _label("everything you own", 28, Color(PALETTE["ink"], 0.62))
	shelf_cap.position = Vector2(78, 178)
	page.add_child(shelf_cap)
	var shelf_rule := HandRule.new()
	shelf_rule.length = 604.0
	shelf_rule.color = Color(PALETTE["sage"], 0.8)
	shelf_rule.size = Vector2(604, 14)
	shelf_rule.position = Vector2(80, 214)
	shelf_rule.mouse_filter = Control.MOUSE_FILTER_IGNORE
	page.add_child(shelf_rule)
	# THE SHELF HAS SECTIONS (owner: "maybe topics, like categories"): gear,
	# the pitch, comforts, and — when the trade earns them — your trade's own
	# tools. Items flow left-right under small sage captions.
	var buckets := {"GEAR": [], "THE PITCH": [], "COMFORTS": [], "YOUR TRADE": []}
	for def in content_items:
		var rq_what: Array = (def as Dictionary).get("requires_what", [])
		var rq_who: Array = (def as Dictionary).get("requires_who", [])
		if not rq_what.is_empty() and not rq_what.has(_biz_what):
			continue
		if not rq_who.is_empty() and not rq_who.has(_biz_who):
			continue
		var tags: Array = (def as Dictionary).get("tags", [])
		if not rq_what.is_empty() or not rq_who.is_empty():
			(buckets["YOUR TRADE"] as Array).append(def)
		elif tags.has("morale"):
			(buckets["COMFORTS"] as Array).append(def)
		elif tags.has("sales") or tags.has("marketing"):
			(buckets["THE PITCH"] as Array).append(def)
		else:
			(buckets["GEAR"] as Array).append(def)
	var placed: Array = []   # ["cap", name] or ["item", def]
	for bname in ["GEAR", "THE PITCH", "COMFORTS", "YOUR TRADE"]:
		if (buckets[bname] as Array).is_empty():
			continue
		placed.append(["cap", bname])
		for def in buckets[bname]:
			placed.append(["item", def])
	# THE SHELF SCROLLS (owner: "scrolling or categories" — both): sections
	# flow inside a scroll area; however many items a trade earns, the paper
	# never overflows.
	var shelf_scroll := ScrollContainer.new()
	shelf_scroll.position = Vector2(52, 228)
	shelf_scroll.set_deferred("size", Vector2(660, 484))
	shelf_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	shelf_scroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_SHOW_NEVER
	page.add_child(shelf_scroll)
	var grid := Control.new()
	grid.mouse_filter = Control.MOUSE_FILTER_PASS
	shelf_scroll.add_child(grid)
	# sized after placement (py accumulates); a closure keeps it honest
	var px := 12.0
	var py := 10.0
	var gi := 0
	for entry in placed:
		if String((entry as Array)[0]) == "cap":
			if px > 12.0:
				px = 12.0
				py += 132.0
			var cap := _label(String((entry as Array)[1]).to_lower(), 22, Color(PALETTE["sage"], 0.95))
			cap.position = Vector2(px + 6.0, py - 6.0)
			grid.add_child(cap)
			py += 26.0
			continue
		var def: Dictionary = (entry as Array)[1]
		if px > 12.0 + 5.0 * 104.0:
			px = 12.0
			py += 132.0
		var org := Vector2(px, py)
		px += 104.0
		gi += 1
		var ib := Button.new()
		ib.custom_minimum_size = Vector2(112, 112)
		ib.size = Vector2(112, 112)
		ib.position = org
		ib.pivot_offset = Vector2(56, 100)
		_bare_button(ib)
		ib.pressed.connect(_toggle_bag.bind(String(def["id"]), int(def.get("carry_cost", 1)), ib))
		ib.mouse_entered.connect(func():
			_bag_detail(def)
			var t := create_tween()
			t.tween_property(ib, "scale", Vector2(1.08, 1.08), 0.08))
		ib.mouse_exited.connect(func():
			var t := create_tween()
			t.tween_property(ib, "scale", Vector2.ONE, 0.1))
		# shadow first, art second: a Button paints its own icon BEFORE its
		# children, so an icon here would end up under its own contact shadow
		var ish := EllipseShadow.new()
		ish.position = Vector2(18, 94)
		ish.size = Vector2(76, 14)
		ish.mouse_filter = Control.MOUSE_FILTER_IGNORE
		ib.add_child(ish)
		var art := TextureRect.new()
		art.name = "art"
		art.size = Vector2(112, 104)
		art.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		art.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		art.mouse_filter = Control.MOUSE_FILTER_IGNORE
		ib.add_child(art)
		art.texture = _clean_tex("res://assets/sprites/%s.png" % String(def["id"]))
		var ring := InkTag.new()
		ring.name = "packed"
		ring.color = PALETTE["coral"]
		ring.shape = 1
		ring.thick = 5.0
		ring.wobble_seed = gi
		ring.position = Vector2(-10, -8)
		ring.size = Vector2(132, 128)
		ring.visible = false
		ring.mouse_filter = Control.MOUSE_FILTER_IGNORE
		ib.add_child(ring)
		grid.add_child(ib)
		_bag_btns[String(def["id"])] = ib
		if int(def.get("carry_cost", 1)) > 1:
			# the weight is written next to the thing in pen, not stamped on a
			# rounded badge that covered the art it was describing
			var wt := _label("2 slots", 24, PALETTE["coral"])
			wt.position = org + Vector2(0, 101)
			wt.size = Vector2(112, 30)
			wt.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
			wt.mouse_filter = Control.MOUSE_FILTER_IGNORE
			grid.add_child(wt)

	grid.custom_minimum_size = Vector2(640, py + 132.0)
	if py + 132.0 > 484.0:
		var more := _label("▼ scroll — there's more on the shelf", 22, Color(PALETTE["cream"], 0.75))
		more.position = Vector2(78, 740)
		page.add_child(more)

	# detail panel — what the thing is FOR, cut to the size of what it says. It
	# was a 430x520 sheet holding a name and two lines, which made it the equal of
	# the shelf beside it: three big cream rectangles all shouting at once.
	var dp := Control.new()
	dp.position = Vector2(760, 200)
	dp.size = Vector2(340, 400)
	dp.rotation = 0.007
	page.add_child(dp)
	var dp_sheet := PaperEdge.new()
	dp_sheet.size = dp.size
	dp_sheet.thick = 4.0
	dp_sheet.lean = 4
	dp_sheet.mouse_filter = Control.MOUSE_FILTER_IGNORE
	dp.add_child(dp_sheet)
	_bagd_art = TextureRect.new()
	_bagd_art.size = Vector2(150, 150)
	_bagd_art.position = Vector2(95, 18)
	_bagd_art.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_bagd_art.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	dp.add_child(_bagd_art)
	_bagd_name = _dlabel("", 30, PALETTE["ink"])
	_bagd_name.position = Vector2(10, 176)
	_bagd_name.size = Vector2(320, 44)
	_bagd_name.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	dp.add_child(_bagd_name)
	var rule := HandRule.new()
	rule.length = 110.0
	rule.color = PALETTE["coral"]
	rule.size = Vector2(110, 14)
	rule.position = Vector2(115, 226)
	rule.mouse_filter = Control.MOUSE_FILTER_IGNORE
	dp.add_child(rule)
	_bagd_blurb = _label("", 26, PALETTE["ink"])
	_bagd_blurb.position = Vector2(30, 248)
	_bagd_blurb.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_wrap(_bagd_blurb, 280.0)
	dp.add_child(_bagd_blurb)
	_bagd_cost = _label("", 24, Color(PALETTE["ink"], 0.6))
	_bagd_cost.position = Vector2(16, 356)
	_bagd_cost.size = Vector2(308, 34)
	_bagd_cost.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	dp.add_child(_bagd_cost)

	# WHAT IS PACKED IS WRITTEN ON THE BOX. It used to be a third cream panel the
	# size of the other two, and at 0/4 that panel was a large blank rectangle —
	# the loudest thing on the screen. A moving box already stands here doing
	# nothing, and a shipping label is exactly what a moving box is written on,
	# so the manifest is now stuck to it and the panel is gone.
	var box := TextureRect.new()
	box.size = Vector2(340, 460)
	box.position = Vector2(1140, 126)
	box.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	box.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	box.texture = _clean_tex("res://assets/sprites/env_boxes.png")
	page.add_child(box)
	# the label covers the bottom box and stops where the box stops. Laid across
	# the middle of the stack it left the box's tapering foot showing under it,
	# which read as a paper tail rather than as a box.
	var tag := Control.new()
	tag.position = Vector2(1178, 352)
	tag.size = Vector2(288, 238)
	tag.pivot_offset = tag.size / 2.0
	tag.rotation = -0.018
	page.add_child(tag)
	_box_anchor = tag.position + tag.size / 2.0
	var tag_paper := PaperEdge.new()
	tag_paper.size = tag.size
	tag_paper.thick = 4.0
	tag_paper.lean = 3
	tag_paper.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tag.add_child(tag_paper)
	_slots_label = _dlabel("IN THE BAG · 0/4", 28, PALETTE["ink"])
	_slots_label.position = Vector2(18, 12)
	_slots_label.size = Vector2(252, 38)
	tag.add_child(_slots_label)
	var mrule := HandRule.new()
	mrule.length = 252.0
	mrule.color = Color(PALETTE["sage"], 0.8)
	mrule.size = Vector2(252, 14)
	mrule.position = Vector2(18, 52)
	mrule.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tag.add_child(mrule)
	# four printed rules, one per slot. An empty label was a blank cream field —
	# the loudest thing on the screen when nothing is packed yet. Ruled, the same
	# emptiness reads as a form waiting to be filled in, and the names that arrive
	# sit ON the ruling instead of floating in the middle of it.
	for i in 4:
		var srule := HandRule.new()
		srule.length = 252.0
		srule.color = Color(PALETTE["ink"], 0.15)
		srule.size = Vector2(252, 14)
		srule.position = Vector2(18, 96.0 + i * 36.0)
		srule.mouse_filter = Control.MOUSE_FILTER_IGNORE
		tag.add_child(srule)
	_empty_note = _label("nothing packed yet.", 25, Color(PALETTE["ink"], 0.5))
	_empty_note.position = Vector2(18, 76)
	_empty_note.size = Vector2(252, 36)
	_empty_note.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tag.add_child(_empty_note)
	_packed_row = VBoxContainer.new()
	_packed_row.position = Vector2(18, 72)
	_packed_row.size = Vector2(252, 148)
	_packed_row.add_theme_constant_override("separation", 0)
	tag.add_child(_packed_row)
	_refresh_packed()

	var sum_strip := PaperEdge.new()
	sum_strip.position = Vector2(48, 832)
	sum_strip.size = Vector2(940, 64)
	sum_strip.thick = 3.0
	sum_strip.lean = 4
	sum_strip.mouse_filter = Control.MOUSE_FILTER_IGNORE
	page.add_child(sum_strip)
	_bag_summary = _label("", 28, PALETTE["ink"])
	_bag_summary.position = Vector2(70, 846)
	_bag_summary.size = Vector2(896, 40)
	_bag_summary.autowrap_mode = TextServer.AUTOWRAP_OFF
	page.add_child(_bag_summary)

	var back := Button.new()
	back.text = "←"
	back.position = Vector2(48, 930)
	back.size = Vector2(100, 70)
	_style_button(back, PALETTE["blue"], 30)
	_paper_card(back)
	_juice(back)
	back.pressed.connect(func(): _transition_to(5))
	page.add_child(back)
	_launch = Button.new()
	_launch.text = "SIGN & QUIT YOUR JOB  →"
	_launch.position = Vector2(1050, 920)
	_launch.size = Vector2(450, 84)
	_style_button(_launch, PALETTE["coral"], 32)
	_paper_card(_launch)
	_juice(_launch)
	_launch.pressed.connect(_do_launch)
	page.add_child(_launch)
	if not content_items.is_empty():
		_bag_detail(content_items[0])
	return page

func _bag_detail(def: Dictionary) -> void:
	if _bagd_name == null:
		return
	_bagd_art.texture = _clean_tex("res://assets/sprites/%s.png" % String(def["id"]))
	_bagd_name.text = String(def["name"])
	_bagd_blurb.text = String(def.get("blurb", ""))
	var cost := int(def.get("carry_cost", 1))
	_bagd_cost.text = ("takes %d slot%s" % [cost, "s" if cost > 1 else ""]) + ("  ·  PACKED ✓" if _bag.has(String(def["id"])) else "")

func _add_cofounder() -> void:
	if _cofounders.size() >= MAX_COFOUNDERS:
		return
	_open_recruit()
	return

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

## Marks an item packed or not. Packed is a coral pen ring drawn AROUND the
## object — the log book's own idiom — not a repainted tile, because there is
## no tile any more, and not a red ✕, which reads as "delete this".
func _paint_bag_tile(btn: Button, packed: bool) -> void:
	var mark := btn.get_node_or_null("packed")
	if mark:
		mark.visible = packed
	var art := btn.get_node_or_null("art")
	if art:
		art.modulate = Color.WHITE if packed else Color(0.9, 0.9, 0.9, 1.0)

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
		var flown: Texture2D = null
		var art_node := btn.get_node_or_null("art")
		if art_node is TextureRect:
			flown = (art_node as TextureRect).texture
		if flown:
			var fly := TextureRect.new()
			fly.texture = flown
			fly.size = Vector2(70, 70)
			fly.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
			fly.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
			fly.position = btn.global_position
			add_child(fly)
			var ft := create_tween()
			ft.tween_property(fly, "position", _box_anchor, 0.4).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
			ft.parallel().tween_property(fly, "scale", Vector2(0.3, 0.3), 0.4)
			ft.tween_callback(fly.queue_free)
	_sfx_click.play()
	_refresh_capline()
	_refresh_packed()

## The packing list, written on the shipping label in the founder's own hand:
## a ticked line per thing, clickable to take it back out. Icons came off the
## lines when the list moved onto the box — at label width a 56px thumbnail
## beside every name is fifteen more shapes on a screen already called clogged,
## and the object itself is already on the shelf two hand-spans to the left.
func _refresh_packed() -> void:
	if _packed_row == null:
		return
	for c in _packed_row.get_children():
		c.queue_free()
	if _empty_note:
		_empty_note.visible = _bag.is_empty()
	for bid in _bag:
		var nm := String(bid)
		for d in content_items:
			if String(d["id"]) == String(bid):
				nm = String(d.get("name", bid))
		var line := Button.new()
		line.custom_minimum_size = Vector2(252, 36)
		line.size = Vector2(252, 36)
		_bare_button(line)
		line.tooltip_text = "take it back out"
		line.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		var chip_id := String(bid)
		line.pressed.connect(func():
			var owner_btn: Button = _bag_btns.get(chip_id)
			if owner_btn:
				_toggle_bag(chip_id, 0, owner_btn))
		_packed_row.add_child(line)
		var tick := _label("✓", 26, PALETTE["coral"])
		tick.position = Vector2(0, -2)
		tick.size = Vector2(24, 36)
		tick.mouse_filter = Control.MOUSE_FILTER_IGNORE
		line.add_child(tick)
		var nl := _label(nm, 26, PALETTE["ink"])
		nl.position = Vector2(26, -2)
		nl.size = Vector2(224, 36)
		nl.autowrap_mode = TextServer.AUTOWRAP_OFF
		nl.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
		nl.mouse_filter = Control.MOUSE_FILTER_IGNORE
		line.add_child(nl)

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
		_slots_label.text = "IN THE BAG · %d/4" % used
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

## One founding sheet per person, cut from the same paper as everything else.
## These used to be translucent rounded rectangles with a hairline border —
## the one shape on the whole flow that could only have come from a web app.
func _crew_sheet(parent: Control, at: Vector2, sz: Vector2, warn: bool, lean: int) -> Control:
	var holder := Control.new()
	holder.position = at
	holder.size = sz
	parent.add_child(holder)
	var paper := PaperEdge.new()
	paper.size = sz
	paper.thick = 6.0 if warn else 4.0
	paper.edge = PALETTE["coral"] if warn else PALETTE["ink"]
	paper.lean = lean
	paper.mouse_filter = Control.MOUSE_FILTER_IGNORE
	holder.add_child(paper)
	return holder

func _rebuild_crew(founder_pct: float) -> void:
	if _crew_row == null:
		return
	for c in _crew_row.get_children():
		c.queue_free()
	_crew_sprites.clear()
	var n := _cofounders.size()
	var slots := n + 1 + (1 if n < MAX_COFOUNDERS else 0)
	var gap := 16.0
	var cw := clampf((_crew_row.size.x - (slots - 1) * gap) / float(slots), 170.0, 234.0)
	var ch := 500.0
	var x0 := (_crew_row.size.x - (slots * cw + (slots - 1) * gap)) / 2.0
	# YOU card
	var you := _crew_sheet(_crew_row, Vector2(x0, 0), Vector2(cw, ch), founder_pct < 50.0, 0)
	var yspr := TextureRect.new()
	yspr.size = Vector2(cw - 50.0, 180)
	yspr.position = Vector2(25, 18)
	yspr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	yspr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	yspr.pivot_offset = Vector2((cw - 50.0) / 2.0, 180)
	var still := "res://assets/sprites/%s.png" % String(_sel_arch.get("sprite", ""))
	if ResourceLoader.exists(still):
		yspr.texture = load(still)
	if founder_pct < 50.0:
		yspr.rotation = 0.12
	you.add_child(yspr)
	var yname := "YOU"
	if _founder_edit != null and is_instance_valid(_founder_edit) and _founder_edit.value().strip_edges() != "":
		yname = _founder_edit.value().strip_edges().split(" ")[0].to_upper()
	var ylab := _dlabel("%s · CEO" % yname, 28, PALETTE["ink"])
	ylab.position = Vector2(0, 210)
	ylab.size = Vector2(cw, 40)
	ylab.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	you.add_child(ylab)
	var ypct := _dlabel("%.0f%%" % founder_pct, 64, PALETTE["sage"] if founder_pct >= 50.0 else PALETTE["coral"])
	ypct.position = Vector2(0, 262)
	ypct.size = Vector2(cw, 90)
	ypct.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	you.add_child(ypct)
	var ysub := _label("your slice", 26, Color(PALETTE["ink"], 0.6))
	ysub.position = Vector2(0, 358)
	ysub.size = Vector2(cw, 32)
	ysub.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	you.add_child(ysub)
	var cname := _name_edit.value() if _name_edit else ""
	var cname_lbl := _label(cname if cname != "" else "your company", 30, Color(PALETTE["ink"], 0.8))
	cname_lbl.position = Vector2(10, 404)
	cname_lbl.size = Vector2(cw - 20, 40)
	cname_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	cname_lbl.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	you.add_child(cname_lbl)
	if founder_pct < 50.0:
		var warn := _label("OUTVOTED!", 30, PALETTE["coral"])
		warn.position = Vector2(0, 446)
		warn.size = Vector2(cw, 40)
		warn.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		you.add_child(warn)
	else:
		var owned := _label("is yours to run", 26, Color(PALETTE["ink"], 0.5))
		owned.position = Vector2(0, 448)
		owned.size = Vector2(cw, 36)
		owned.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		you.add_child(owned)
	# cofounder cards
	for i in n:
		var cf: Dictionary = _cofounders[i]
		var no_vest: bool = not bool(cf["vesting"])
		var card := _crew_sheet(_crew_row, Vector2(x0 + (i + 1) * (cw + gap), 0), Vector2(cw, ch), no_vest, i + 1)
		var slug: String = ROLE_SLUGS.get(ROLES[int(cf["role"])], "technical")
		var st := _cf_state(cf, n)
		var spr := TextureRect.new()
		spr.size = Vector2(cw - 60.0, 176)
		spr.position = Vector2(30, 14)
		spr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		spr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		spr.pivot_offset = Vector2((cw - 60.0) / 2.0, 176)
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
		rm.size = Vector2(44, 44)
		rm.position = Vector2(cw - 54, 10)
		_ink_button(rm, PALETTE["coral"], 24, true)
		rm.tooltip_text = "Part ways (before it gets ugly)"
		rm.pressed.connect(func():
			_cofounders.erase(cf)
			_sfx_click.play()
			_refresh_capline())
		card.add_child(rm)
		if String(cf.get("name", "")) == "":
			cf["name"] = WorldGen.person_name(_prng)
		var rname := _dlabel("%s · %s" % [String(cf["name"]).split(" ")[0],
			ROLES[int(cf["role"])].to_upper()], 24, PALETTE["ink"])
		rname.position = Vector2(0, 196)
		rname.size = Vector2(cw, 36)
		rname.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		rname.tooltip_text = "%s — click for another name" % String(cf["name"])
		rname.mouse_filter = Control.MOUSE_FILTER_STOP
		rname.gui_input.connect(func(ev: InputEvent) -> void:
			if ev is InputEventMouseButton and ev.pressed:
				cf["name"] = WorldGen.person_name(_prng)
				rname.text = "%s · %s" % [String(cf["name"]).split(" ")[0],
					ROLES[int(cf["role"])].to_upper()]
				rname.tooltip_text = "%s — click for another name" % String(cf["name"])
				_sfx_click.play())
		card.add_child(rname)
		var com := Button.new()
		var ft: bool = int(cf["commitment"]) == 0
		com.text = "FULL-TIME" if ft else "PART-TIME ⚠"
		com.size = Vector2(cw - 40, 46)
		com.position = Vector2(20, 238)
		_ink_button(com, PALETTE["sage"] if ft else PALETTE["coral"], 24)
		if not ft:
			for cn in ["font_color", "font_hover_color", "font_pressed_color"]:
				com.add_theme_color_override(cn, PALETTE["coral"])
		com.tooltip_text = "Click to toggle. Part-timers with real equity flake at the worst time."
		com.pressed.connect(func():
			cf["commitment"] = 1 - int(cf["commitment"])
			_sfx_click.play()
			_refresh_capline())
		card.add_child(com)
		var minus := Button.new()
		minus.text = "−"
		minus.size = Vector2(52, 56)
		minus.position = Vector2(14, 298)
		_ink_button(minus, PALETTE["coral"], 34, true)
		minus.pressed.connect(func():
			cf["equity"] = maxf(1.0, float(cf["equity"]) - 5.0)
			_refresh_capline()
			_react_crew(_cofounders.find(cf), false))
		card.add_child(minus)
		var eq := _dlabel("%.0f%%" % float(cf["equity"]), 48, PALETTE["ink"])
		eq.position = Vector2(68, 296)
		eq.size = Vector2(cw - 136, 64)
		eq.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		card.add_child(eq)
		var plus := Button.new()
		plus.text = "+"
		plus.size = Vector2(52, 56)
		plus.position = Vector2(cw - 66, 298)
		_ink_button(plus, PALETTE["sage"], 34, true)
		plus.pressed.connect(func():
			cf["equity"] = minf(60.0, float(cf["equity"]) + 5.0)
			_refresh_capline()
			_react_crew(_cofounders.find(cf), true))
		card.add_child(plus)
		var vest := Button.new()
		vest.text = "VESTED ✓" if not no_vest else "NO VESTING ⚠"
		vest.size = Vector2(cw - 40, 46)
		vest.position = Vector2(20, 370)
		_ink_button(vest, PALETTE["sage"] if not no_vest else PALETTE["coral"], 24)
		if no_vest:
			for cn2 in ["font_color", "font_hover_color", "font_pressed_color"]:
				vest.add_theme_color_override(cn2, PALETTE["coral"])
		vest.tooltip_text = "4-year vesting, 1-year cliff. Turning this off is the classic mistake."
		vest.pressed.connect(func():
			cf["vesting"] = not bool(cf["vesting"])
			_sfx_click.play()
			_refresh_capline())
		card.add_child(vest)
		var mood := _label({"happy": "☀ thrilled", "neutral": "steady", "resentful": "⛈ resentful…"}[st], 28,
			{"happy": PALETTE["sage"], "neutral": Color(PALETTE["ink"], 0.7), "resentful": PALETTE["coral"]}[st])
		mood.position = Vector2(0, 438)
		mood.size = Vector2(cw, 40)
		mood.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		card.add_child(mood)
	# the empty chair: a blank founding sheet with nobody's name on it yet
	if n < MAX_COFOUNDERS:
		var slot := Button.new()
		slot.position = Vector2(x0 + (n + 1) * (cw + gap), 0)
		slot.size = Vector2(cw, ch)
		slot.text = ""
		_bare_button(slot)
		slot.pressed.connect(_add_cofounder)
		_crew_row.add_child(slot)
		var spaper := PaperEdge.new()
		spaper.size = slot.size
		spaper.thick = 3.0
		spaper.edge = Color(PALETTE["ink"], 0.5)
		spaper.lean = 4
		spaper.mouse_filter = Control.MOUSE_FILTER_IGNORE
		slot.add_child(spaper)
		var phone := _label("☎", 78, Color(PALETTE["ink"], 0.55))
		phone.position = Vector2(0, 150)
		phone.size = Vector2(cw, 100)
		phone.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		phone.mouse_filter = Control.MOUSE_FILTER_IGNORE
		slot.add_child(phone)
		var scap := _dlabel("+ RECRUIT", 32, PALETTE["ink"])
		scap.position = Vector2(0, 266)
		scap.size = Vector2(cw, 44)
		scap.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		scap.mouse_filter = Control.MOUSE_FILTER_IGNORE
		slot.add_child(scap)
		var shint := _label("an empty chair", 26, Color(PALETTE["ink"], 0.5))
		shint.position = Vector2(0, 316)
		shint.size = Vector2(cw, 36)
		shint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		shint.mouse_filter = Control.MOUSE_FILTER_IGNORE
		slot.add_child(shint)

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

func _do_launch() -> void:
	var cfs: Array = []
	for cf in _cofounders:
		cfs.append({"role": ROLES[int(cf["role"])], "commitment": COMMITMENTS[int(cf["commitment"])], "equity": float(cf["equity"]), "vesting": bool(cf["vesting"]), "name": String(cf.get("name", ""))})
	var trap_ids: Array[String] = []
	for t in _compute_traps():
		if not trap_ids.has(String(t["id"])):
			trap_ids.append(String(t["id"]))
	done.emit({
		"archetype": _sel_arch,
		"cofounders": cfs,
		"funding": _sel_fund,
		"company_name": _name_edit.value(),
		"founder_name": _founder_edit.value().strip_edges() \
			if _founder_edit.value().strip_edges() != "" else WorldGen.person_name(_prng),
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

class InkTag:
	extends Control
	## A pen mark, not a box: a wobbly outline with NO fill and no corner radius.
	## `shape` 0 rings a word, 1 rings an object. This is what replaced every
	## filled rounded rectangle on these screens — a form control fills, a pen
	## only ever draws a line around something that is already there.
	var color := Color("1E1E1E")
	var thick := 3.5
	var shape := 0
	var wobble_seed := 3

	func _ready() -> void:
		resized.connect(queue_redraw)

	func _draw() -> void:
		if size.x < 4.0 or size.y < 4.0:
			return
		var rng := RandomNumberGenerator.new()
		rng.seed = 17 + wobble_seed
		var pts := PackedVector2Array()
		if shape == 1:
			var c := size / 2.0
			for i in 41:
				var a := TAU * float(i) / 40.0
				var k := 1.0 + sin(a * 3.0 + wobble_seed) * 0.03 + sin(a * 7.0) * 0.015
				pts.append(c + Vector2(cos(a) * (c.x - thick) * k, sin(a) * (c.y - thick) * k))
		else:
			var inset := thick * 0.5 + 2.0
			var corners := [Vector2(inset, inset), Vector2(size.x - inset, inset),
				Vector2(size.x - inset, size.y - inset), Vector2(inset, size.y - inset)]
			for i in 4:
				var a: Vector2 = corners[i]
				var b: Vector2 = corners[(i + 1) % 4]
				var n := Vector2(b.y - a.y, a.x - b.x).normalized()
				for k2 in 10:
					pts.append(a.lerp(b, float(k2) / 9.0) + n * rng.randf_range(-1.7, 1.7))
			pts.append(pts[0])
		draw_polyline(pts, color, thick, true)


class PenCross:
	extends Control
	## Scribbled out in pen — how the log book retires something that is gone.
	var color := Color(Color("E86A5C"), 0.78)
	func _draw() -> void:
		var rng := RandomNumberGenerator.new()
		rng.seed = 29
		for pair in [[Vector2(0.08, 0.10), Vector2(0.92, 0.90)], [Vector2(0.92, 0.12), Vector2(0.08, 0.88)]]:
			var a := Vector2(size.x * pair[0].x, size.y * pair[0].y)
			var b := Vector2(size.x * pair[1].x, size.y * pair[1].y)
			var n := Vector2(b.y - a.y, a.x - b.x).normalized()
			var pts := PackedVector2Array()
			for k in 22:
				pts.append(a.lerp(b, float(k) / 21.0) + n * rng.randf_range(-3.5, 3.5))
			draw_polyline(pts, color, 5.0, true)


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
