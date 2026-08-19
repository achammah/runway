class_name TitleScreen
extends Control
## The living title, v3 — every component is REAL frame animation:
## 8-frame fire loop, 48-frame founder run cycle (from video), 3-frame
## boiling-line typography, sun boil, fluttering papers, plus one-shot
## spawners: smoke puffs and dollar bills that peel off the runway and burn.
## Falls back per-component to the static layer when frames are missing.

signal done

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

func _video_frames() -> Array:
	var out: Array = []
	var i := 1
	while true:
		var p := "res://assets/title/video/frame_%02d.png" % i
		if not ResourceLoader.exists(p):
			break
		out.append(load(p))
		i += 1
	return out

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

func _unhandled_input(event: InputEvent) -> void:
	if not _armed:
		return
	if (event is InputEventKey and event.pressed) or (event is InputEventMouseButton and event.pressed):
		_armed = false
		done.emit()
