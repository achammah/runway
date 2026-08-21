class_name TitleScreen
extends Control
## The living title, v3 — every component is REAL frame animation:
## 8-frame fire loop, 48-frame founder run cycle (from video), 3-frame
## boiling-line typography, sun boil, fluttering papers, plus one-shot
## spawners: smoke puffs and dollar bills that peel off the runway and burn.
## Falls back per-component to the static layer when frames are missing.

signal done                       ## legacy any-key (harnesses): straight to a fresh run
signal start_new(slot: int)      ## the NEW GAME choice, with its slot
signal continue_slot(slot: int)  ## resume this saved slot

const L := "res://assets/title/layers/"
const A := "res://assets/title/anim/"

var _armed := false
var _t := 0.0
var _root: Control
var _players: Array = []   # {node, frames, fps, t, mode}
var _founder: TextureRect
var _papers: Array = []
var _press_node: TextureRect
var _embers: Array = []
var _jump_cooldown := 5.0
var _smoke_cooldown := 2.0
var _bill_cooldown := 7.0
var _jumping := false
var _founder_base := Vector2(555, 285)

func _frames(prefix: String) -> Array:
	var out: Array = []
	var i := 1
	while true:
		var p := "%s%s_%02d.png" % [A, prefix, i]
		if not ResourceLoader.exists(p):
			break
		out.append(load(p))
		i += 1
	return out

## FIRST FRAME NOW, THE REST WHILE THE TITLE BREATHES (owner: launch was
## SUPER SLOW — 48 full-screen frames loaded synchronously before the first
## pixel). The player array grows in place; the loop plays whatever exists.
func _video_frames() -> Array:
	var out: Array = []
	if ResourceLoader.exists("res://assets/title/video/frame_01.png"):
		out.append(load("res://assets/title/video/frame_01.png"))
	return out

func _stream_video_frames(into: Array) -> void:
	var i := into.size() + 1
	if not ResourceLoader.exists("res://assets/title/video/frame_%02d.png" % i):
		return
	var ht := Timer.new()
	ht.wait_time = 0.04
	ht.timeout.connect(func() -> void:
		for n in 3:
			var p := "res://assets/title/video/frame_%02d.png" % (into.size() + 1)
			if not ResourceLoader.exists(p):
				ht.queue_free()
				return
			into.append(load(p)))
	add_child(ht)
	ht.start()

func _ready() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	# primary: the full-scene loop video as frames — one coherent living painting
	var vframes := _video_frames()
	if not vframes.is_empty():
		_root = Control.new()
		_root.size = Vector2(1536, 1024)
		_root.pivot_offset = Vector2(768, 512)
		add_child(_root)
		var vr := TextureRect.new()
		vr.texture = vframes[0]
		vr.size = Vector2(1536, 1024)
		vr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		vr.stretch_mode = TextureRect.STRETCH_SCALE
		_root.add_child(vr)
		_players.append({"node": vr, "frames": vframes, "fps": 12.0, "t": 0.0, "mode": "loop"})
		_stream_video_frames(vframes)
		_arm()
		return
	if not ResourceLoader.exists(L + "base.png"):
		var tex := TextureRect.new()
		tex.set_anchors_preset(Control.PRESET_FULL_RECT)
		tex.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		tex.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		var art := load("res://assets/title/title_screen.png")
		if art:
			tex.texture = art
		add_child(tex)
		_arm()
		return
	_root = Control.new()
	_root.size = Vector2(1536, 1024)
	_root.pivot_offset = Vector2(768, 512)
	add_child(_root)

	var base := TextureRect.new()
	base.texture = load(L + "base.png")
	base.size = Vector2(1536, 1024)
	base.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	base.stretch_mode = TextureRect.STRETCH_SCALE
	_root.add_child(base)

	_anim_or_layer("sun", "sun.png", Vector2(1310, 42), Vector2(150, 150), 5.0)
	_anim_or_layer("fire", "fire.png", Vector2(-20, 390), Vector2(750, 456), 10.0)

	var paper_frames := _frames("paper")
	for i in 2:
		var paper := _make_rect(Vector2(150 + i * 60, 300 - i * 40), Vector2(300, 205))
		if not paper_frames.is_empty():
			paper.texture = paper_frames[0]
			_players.append({"node": paper, "frames": paper_frames, "fps": 8.0 + i * 2.0, "t": float(i) * 0.3, "mode": "loop"})
		elif ResourceLoader.exists(L + "papers.png"):
			paper.texture = load(L + "papers.png")
		paper.pivot_offset = Vector2(150, 100)
		if i == 1:
			paper.scale = Vector2(0.6, 0.6)
			paper.modulate.a = 0.85
		_papers.append(paper)

	var run_frames := _frames("run")
	_founder = _make_rect(_founder_base, Vector2(430, 430))
	_founder.pivot_offset = Vector2(215, 430)
	if not run_frames.is_empty():
		_founder.texture = run_frames[0]
		_players.append({"node": _founder, "frames": run_frames, "fps": 24.0, "t": 0.0, "mode": "loop"})
	elif ResourceLoader.exists(L + "founder.png"):
		_founder.texture = load(L + "founder.png")

	var type_frames := _frames("type_main")
	if not type_frames.is_empty():
		# the boiling type block already includes PRESS ANY KEY — one node, no duplicate
		var ty := _make_rect(Vector2(280, 40), Vector2(980, 330))
		ty.texture = type_frames[0]
		_players.append({"node": ty, "frames": type_frames, "fps": 6.0, "t": 0.0, "mode": "loop"})
		_press_node = null
	else:
		_anim_or_layer("type_main", "type_main.png", Vector2(300, 42), Vector2(930, 253), 6.0)
		_press_node = _anim_or_layer("type_press", "type_press.png", Vector2(618, 940), Vector2(300, 40), 6.0)

	# embers
	var rng := RandomNumberGenerator.new()
	rng.seed = 5
	for i in 10:
		var e := ColorRect.new()
		var sz := rng.randf_range(4.0, 9.0)
		e.size = Vector2(sz, sz)
		e.rotation = 0.7
		e.color = Color("E86A5C") if i % 3 != 0 else Color("F4B942")
		e.modulate.a = 0.0
		var ex := rng.randf_range(60.0, 600.0)
		var ey := rng.randf_range(430.0, 690.0)
		e.position = Vector2(ex, ey)
		_root.add_child(e)
		var dur := rng.randf_range(1.6, 3.2)
		var tw := create_tween().set_loops()
		tw.tween_interval(rng.randf_range(0.05, 2.0))   # never 0: a looping tween with a zero-length step spins
		tw.tween_property(e, "modulate:a", rng.randf_range(0.5, 0.9), dur * 0.2)
		tw.parallel().tween_property(e, "position:y", ey - rng.randf_range(90.0, 190.0), dur)
		tw.parallel().tween_property(e, "position:x", ex + rng.randf_range(-40.0, 40.0), dur)
		tw.tween_property(e, "modulate:a", 0.0, dur * 0.3)
		tw.tween_callback(func(): e.position = Vector2(ex, ey))
	_arm()

func _make_rect(pos: Vector2, sz: Vector2) -> TextureRect:
	var tr := TextureRect.new()
	tr.position = pos
	tr.size = sz
	tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	_root.add_child(tr)
	return tr

func _anim_or_layer(prefix: String, fallback: String, pos: Vector2, sz: Vector2, fps: float) -> TextureRect:
	var tr := _make_rect(pos, sz)
	var frames := _frames(prefix)
	if not frames.is_empty():
		tr.texture = frames[0]
		_players.append({"node": tr, "frames": frames, "fps": fps, "t": 0.0, "mode": "loop"})
	elif ResourceLoader.exists(L + fallback):
		tr.texture = load(L + fallback)
	return tr

func _spawn_oneshot(prefix: String, pos: Vector2, sz: Vector2, fps: float, rise: float = 0.0) -> void:
	var frames := _frames(prefix)
	if frames.is_empty():
		return
	var tr := _make_rect(pos, sz)
	tr.texture = frames[0]
	var entry := {"node": tr, "frames": frames, "fps": fps, "t": 0.0, "mode": "once"}
	_players.append(entry)
	if rise > 0.0:
		var tw := create_tween()
		tw.tween_property(tr, "position:y", pos.y - rise, frames.size() / fps)

func _arm() -> void:
	if get_node_or_null("stamp") == null:
		var st := Label.new()
		st.name = "stamp"
		st.text = _build_stamp()
		st.add_theme_font_override("font", load("res://assets/fonts/PatrickHand-Regular.ttf"))
		st.add_theme_font_size_override("font_size", 18)
		st.add_theme_color_override("font_color", Color(0.12, 0.12, 0.12, 0.4))
		st.position = Vector2(16, 996)
		st.mouse_filter = Control.MOUSE_FILTER_IGNORE
		add_child(st)
	await get_tree().create_timer(0.4).timeout
	_armed = true

func _process(delta: float) -> void:
	if _root == null:
		return
	_t += delta
	# frame players
	var dead: Array = []
	for pl in _players:
		pl["t"] += delta
		var idx := int(pl["t"] * float(pl["fps"]))
		var frames: Array = pl["frames"]
		if pl["mode"] == "once" and idx >= frames.size():
			(pl["node"] as TextureRect).queue_free()
			dead.append(pl)
			continue
		(pl["node"] as TextureRect).texture = frames[idx % frames.size()]
	for d in dead:
		_players.erase(d)
	# cinematic breathe
	var breathe := 1.0 + 0.012 * sin(_t * 0.5)
	_root.scale = Vector2(breathe, breathe)
	# founder: the run frames carry the cycle; jumps on top (layered mode only)
	if _founder and not _jumping:
		_jump_cooldown -= delta
		if _jump_cooldown <= 0.0:
			_do_jump()
	# papers drift paths
	for i in _papers.size():
		var p: TextureRect = _papers[i]
		var ph := _t * (0.8 + i * 0.3) + i * 2.0
		p.position.y = (300 - i * 40) + sin(ph) * 16.0
		p.position.x = (150 + i * 60) + cos(ph * 0.7) * 10.0
	# press-any-key pulse on top of its boil
	if _press_node:
		_press_node.modulate.a = 0.5 + 0.5 * (0.5 + 0.5 * sin(_t * 2.6))
	# one-shot spawners: smoke from the fire, bills peeling off the runway to burn
	_smoke_cooldown -= delta
	if _smoke_cooldown <= 0.0:
		_smoke_cooldown = randf_range(1.4, 3.0)
		_spawn_oneshot("smoke", Vector2(randf_range(80.0, 520.0), randf_range(380.0, 460.0)), Vector2(90, 90), 7.0, 120.0)
	_bill_cooldown -= delta
	if _bill_cooldown <= 0.0:
		_bill_cooldown = randf_range(5.0, 9.0)
		_spawn_oneshot("bill", Vector2(randf_range(620.0, 900.0), randf_range(730.0, 790.0)), Vector2(110, 110), 6.0, 30.0)

func _do_jump() -> void:
	_jumping = true
	_jump_cooldown = randf_range(4.0, 8.0)
	var tw := create_tween()
	tw.tween_property(_founder, "scale", Vector2(1.1, 0.88), 0.1)
	tw.tween_property(_founder, "position:y", _founder_base.y - 110.0, 0.3).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tw.parallel().tween_property(_founder, "scale", Vector2(0.95, 1.06), 0.3)
	tw.tween_property(_founder, "position:y", _founder_base.y, 0.28).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
	tw.tween_property(_founder, "scale", Vector2(1.12, 0.86), 0.08)
	tw.tween_property(_founder, "scale", Vector2.ONE, 0.14).set_trans(Tween.TRANS_BOUNCE)
	tw.tween_callback(func(): _jumping = false)

## which build am I actually running — the question that cost a whole session
func _build_stamp() -> String:
	if FileAccess.file_exists("res://build_stamp.txt"):
		return FileAccess.get_file_as_string("res://build_stamp.txt").strip_edges()
	return "dev"

func _unhandled_input(event: InputEvent) -> void:
	if not _armed:
		return
	if (event is InputEventKey and event.pressed) or (event is InputEventMouseButton and event.pressed):
		_armed = false
		# harnesses keep the old any-key contract; a person gets the menu
		if OS.get_environment("RUNWAY_SHOT") != "" or OS.get_environment("RUNWAY_FULLRUN") != "" \
				or OS.get_environment("RUNWAY_FIRSTFLOW") != "" or OS.get_environment("RUNWAY_LANEWIRE") != "" \
				or OS.get_environment("RUNWAY_READING") != "" or OS.get_environment("RUNWAY_TURN") != "":
			done.emit()
			return
		_show_menu()

# ── THE MENU (owner: two buttons that ease in after the key press) ───────────
const CREAM_M := Color("F2EAD3")
const INK_M := Color("1E1E1E")
const PEN_M := Color("E86A5C")
var _menu: Control

func _show_menu() -> void:
	if _press_node != null and is_instance_valid(_press_node):
		var ptw := create_tween()
		ptw.tween_property(_press_node, "modulate:a", 0.0, 0.25)
	_menu = Control.new()
	_menu.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(_menu)
	_build_menu_buttons()

func _build_menu_buttons() -> void:
	var slots := SaveSystem.list_slots()
	var any_save := false
	for s in slots:
		if bool((s as Dictionary).get("exists", false)):
			any_save = true
	var hand: Font = load("res://assets/fonts/PatrickHand-Regular.ttf")
	var mk_btn := func(txt: String, y: float, delay: float) -> Button:
		var b := Button.new()
		b.flat = true
		b.text = txt
		b.add_theme_font_override("font", hand)
		b.add_theme_font_size_override("font_size", 40)
		b.add_theme_color_override("font_color", INK_M)
		b.add_theme_color_override("font_hover_color", PEN_M)
		for stn in ["normal", "hover", "pressed", "focus"]:
			b.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
		b.position = Vector2(594, y + 26.0)
		b.set_deferred("size", Vector2(360, 72))
		# a REAL paper button (owner: "not buttons so unclear"): cream card,
		# wobbled ink border, the word re-issued above the paper
		var card := _PaperBtn.new()
		card.name = "edge"
		card.set_deferred("size", Vector2(360, 72))
		card.mouse_filter = Control.MOUSE_FILTER_IGNORE
		b.add_child(card)
		b.move_child(card, 0)
		# the paper paints OVER the Button's own label: re-issue the word above it
		var word := Label.new()
		word.text = txt
		b.text = ""
		word.add_theme_font_override("font", hand)
		word.add_theme_font_size_override("font_size", 40)
		word.add_theme_color_override("font_color", INK_M)
		word.set_anchors_preset(Control.PRESET_FULL_RECT)
		word.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		word.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		word.mouse_filter = Control.MOUSE_FILTER_IGNORE
		b.add_child(word)
		b.modulate.a = 0.0
		b.pivot_offset = Vector2(180, 36)
		b.mouse_entered.connect(func() -> void:
			var ht := create_tween()
			ht.tween_property(b, "scale", Vector2(1.045, 1.045), 0.08))
		b.mouse_exited.connect(func() -> void:
			var ht := create_tween()
			ht.tween_property(b, "scale", Vector2.ONE, 0.1))
		_menu.add_child(b)
		var tw := create_tween()
		tw.tween_interval(delay)
		tw.tween_property(b, "modulate:a", 1.0, 0.3)
		tw.parallel().tween_property(b, "position:y", y, 0.34).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
		return b
	# the rules, always one click away (and the versioned flag means the new
	# video tutorial shows once even for veterans of the old sheet)
	var how := Button.new()
	how.flat = true
	how.text = "how it works"
	how.add_theme_font_override("font", hand)
	how.add_theme_font_size_override("font_size", 24)
	how.add_theme_color_override("font_color", Color(CREAM_M, 0.6))
	how.add_theme_color_override("font_hover_color", PEN_M)
	for stn in ["normal", "hover", "pressed", "focus"]:
		how.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	how.position = Vector2(1310, 962)
	how.set_deferred("size", Vector2(200, 44))
	how.pressed.connect(func() -> void:
		var ht2 := HowToScreen.new()
		add_child(ht2)
		ht2.size = get_viewport().get_visible_rect().size
		ht2.done.connect(func() -> void:
			if is_instance_valid(ht2):
				ht2.queue_free()))
	_menu.add_child(how)
	# the key is never locked away: reopen the desk anytime, save reloads
	var keyb := Button.new()
	keyb.flat = true
	keyb.text = "api key"
	keyb.add_theme_font_override("font", hand)
	keyb.add_theme_font_size_override("font_size", 24)
	keyb.add_theme_color_override("font_color", Color(CREAM_M, 0.6))
	keyb.add_theme_color_override("font_hover_color", PEN_M)
	for stn in ["normal", "hover", "pressed", "focus"]:
		keyb.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	keyb.position = Vector2(1140, 962)
	keyb.set_deferred("size", Vector2(150, 44))
	keyb.pressed.connect(func() -> void:
		var kd := KeysScreen.new()
		add_child(kd)
		kd.size = get_viewport().get_visible_rect().size
		kd.saved.connect(func() -> void:
			get_tree().reload_current_scene()))
	_menu.add_child(keyb)
	var ng: Button = mk_btn.call("NEW GAME", 694.0, 0.05)
	ng.pressed.connect(func() -> void: _pick_slot(true))
	if any_save:
		var ct: Button = mk_btn.call("CONTINUE", 790.0, 0.18)
		ct.pressed.connect(func() -> void: _pick_slot(false))
	else:
		# no saves: NEW GAME is the only door; a second key press walks through it
		pass

## The slot panel: three drawn cards with company · week · last played.
## new_mode: clicking an empty card starts there (occupied = overwrite it);
## continue mode: only occupied cards respond.
## THE SLOT TABLE, its own full screen (owner): the title art dims away and
## three big paper dossiers sit on the stage — company, founder, week, when.
func _pick_slot(new_mode: bool) -> void:
	for c in _menu.get_children():
		c.queue_free()
	var hand: Font = load("res://assets/fonts/PatrickHand-Regular.ttf")
	var veil := ColorRect.new()
	veil.color = Color("22262B", 0.0)
	veil.set_anchors_preset(Control.PRESET_FULL_RECT)
	_menu.add_child(veil)
	create_tween().tween_property(veil, "color:a", 0.94, 0.3)
	if _press_node != null and is_instance_valid(_press_node):
		_press_node.visible = false   # the boiling type ghosts through the veil
	var slots := SaveSystem.list_slots()
	var title := Label.new()
	title.add_theme_font_override("font", hand)
	title.add_theme_font_size_override("font_size", 52)
	title.add_theme_color_override("font_color", CREAM_M)
	title.text = "YOUR COMPANIES" if not new_mode else "WHERE DOES THIS ONE LIVE?"
	title.position = Vector2(120, 96)
	title.modulate.a = 0.0
	_menu.add_child(title)
	create_tween().tween_property(title, "modulate:a", 1.0, 0.35)
	var sub := Label.new()
	sub.add_theme_font_override("font", hand)
	sub.add_theme_font_size_override("font_size", 27)
	sub.add_theme_color_override("font_color", Color(CREAM_M, 0.65))
	sub.text = "pick one to continue" if not new_mode else "a slot with a company in it gets overwritten"
	sub.position = Vector2(124, 172)
	_menu.add_child(sub)
	for i in slots.size():
		var s: Dictionary = slots[i]
		var exists := bool(s.get("exists", false))
		var slot_n := int(s.get("slot", i + 1))
		var card := Button.new()
		card.flat = true
		for stn in ["normal", "hover", "pressed", "focus"]:
			card.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
		card.position = Vector2(190, 250.0 + float(i) * 226.0 + 26.0)
		card.set_deferred("size", Vector2(1160, 196))
		card.pivot_offset = Vector2(580, 98)
		var paper := _PaperBtn.new()
		paper.set_deferred("size", Vector2(1160, 196))
		paper.mouse_filter = Control.MOUSE_FILTER_IGNORE
		card.add_child(paper)
		var head := Label.new()
		head.add_theme_font_override("font", hand)
		head.add_theme_font_size_override("font_size", 44)
		head.add_theme_color_override("font_color", INK_M if exists else Color(INK_M, 0.42))
		head.text = String(s.get("company", "?")) if exists else "empty desk"
		head.position = Vector2(44, 30)
		head.mouse_filter = Control.MOUSE_FILTER_IGNORE
		card.add_child(head)
		var det := Label.new()
		det.add_theme_font_override("font", hand)
		det.add_theme_font_size_override("font_size", 28)
		det.add_theme_color_override("font_color", Color(INK_M, 0.65))
		det.text = ("%s · week %d · last played %s%s" % [String(s.get("founder", "")),
			int(s.get("week", 0)), _ago(int(s.get("ts", 0))),
			"   — overwrites" if (exists and new_mode) else ""]) if exists \
			else ("start here" if new_mode else "nothing yet")
		det.position = Vector2(46, 108)
		det.mouse_filter = Control.MOUSE_FILTER_IGNORE
		card.add_child(det)
		var tag := Label.new()
		tag.add_theme_font_override("font", hand)
		tag.add_theme_font_size_override("font_size", 30)
		tag.add_theme_color_override("font_color", PEN_M)
		tag.text = "slot %d" % slot_n
		tag.position = Vector2(1020, 30)
		tag.mouse_filter = Control.MOUSE_FILTER_IGNORE
		card.add_child(tag)
		if exists or new_mode:
			card.mouse_entered.connect(func() -> void:
				var ht := create_tween()
				ht.tween_property(card, "scale", Vector2(1.02, 1.02), 0.08))
			card.mouse_exited.connect(func() -> void:
				var ht := create_tween()
				ht.tween_property(card, "scale", Vector2.ONE, 0.1))
		card.modulate.a = 0.0
		card.pressed.connect(func() -> void:
			if new_mode:
				start_new.emit(slot_n)
			elif exists:
				continue_slot.emit(slot_n))
		_menu.add_child(card)
		var tw := create_tween()
		tw.tween_interval(0.07 * float(i))
		tw.tween_property(card, "modulate:a", 1.0, 0.3)
		tw.parallel().tween_property(card, "position:y", 250.0 + float(i) * 226.0, 0.32) \
				.set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	var back := Button.new()
	back.flat = true
	back.text = "←  back"
	back.add_theme_font_override("font", hand)
	back.add_theme_font_size_override("font_size", 30)
	back.add_theme_color_override("font_color", Color(CREAM_M, 0.8))
	back.add_theme_color_override("font_hover_color", PEN_M)
	for stn in ["normal", "hover", "pressed", "focus"]:
		back.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	back.position = Vector2(110, 930)
	back.set_deferred("size", Vector2(200, 56))
	back.pressed.connect(func() -> void:
		for c in _menu.get_children():
			c.queue_free()
		_show_menu_buttons())
	_menu.add_child(back)

## the two entry buttons, reusable after "back"
func _show_menu_buttons() -> void:
	_build_menu_buttons()

## Cream paper + wobbled ink border drawn behind a flat Button's word.
class _PaperBtn:
	extends Control
	func _draw() -> void:
		draw_rect(Rect2(Vector2(4, 5), size), Color(0, 0, 0, 0.35))
		draw_rect(Rect2(Vector2.ZERO, size), Color("F2EAD3"))
		var rng := RandomNumberGenerator.new()
		rng.seed = int(position.x) + int(size.x)
		var pts := PackedVector2Array()
		var cs := [Vector2(2, 2), Vector2(size.x - 2, 2), size - Vector2(2, 2), Vector2(2, size.y - 2)]
		for i in 4:
			var a: Vector2 = cs[i]
			var b: Vector2 = cs[(i + 1) % 4]
			for k in 10:
				pts.append(a.lerp(b, float(k) / 10.0) + Vector2(rng.randf_range(-1.6, 1.6), rng.randf_range(-1.6, 1.6)))
		pts.append(pts[0])
		draw_polyline(pts, Color("1E1E1E"), 3.5, true)

static func _ago(ts: int) -> String:
	if ts <= 0:
		return "a while ago"
	var d := int(Time.get_unix_time_from_system()) - ts
	if d < 3600:
		return "%d min ago" % maxi(d / 60, 1)
	if d < 86400:
		return "%d h ago" % (d / 3600)
	return "%d days ago" % (d / 86400)
