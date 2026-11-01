class_name ScrambleScreen
extends Node2D
## Act 0 — The Leap. Real-time apartment scramble: WASD to move, SPACE/E to grab,
## carry capacity 4, drag items to the glowing DOOR zone before the timer dies.
## Ends with the "what you got / what you left" tableau (the clip moment).

signal done(result: Dictionary)   # {banked: [ids], left: [ids]}

const PALETTE := {
	"cream": Color("F2EAD3"), "ink": Color("1E1E1E"), "coral": Color("E86A5C"),
	"yellow": Color("F4B942"), "sage": Color("8FA582"), "blue": Color("6E8CA0"),
}
const DURATION := 60.0
const CAPACITY := 4
const SPEED := 320.0
const GRAB_RADIUS := 56.0

var rng: SeededRng
var item_defs: Array = []

var _player: Node2D
var _time_left := DURATION
var _carrying: Array = []       # item defs in hand
var _floor_items: Array = []    # {def, node}
var _banked: Array = []         # item defs deposited
var _door: Rect2
var _hud: Label
var _hint: Label
var _over := false

func setup(p_rng: SeededRng, p_items: Array) -> void:
	rng = p_rng
	item_defs = p_items

func _ready() -> void:
	var vp := get_viewport_rect().size
	# apartment floor
	var floor_rect := ColorRect.new()
	floor_rect.color = PALETTE["cream"]
	floor_rect.size = vp
	add_child(floor_rect)
	# door / deposit zone on the right edge
	_door = Rect2(vp.x - 140, vp.y * 0.35, 140, vp.y * 0.3)
	var door_rect := ColorRect.new()
	door_rect.color = PALETTE["yellow"]
	door_rect.position = _door.position
	door_rect.size = _door.size
	add_child(door_rect)
	var door_label := _label("DOOR →", 28, PALETTE["ink"])
	door_label.position = _door.position + Vector2(18, -40)
	add_child(door_label)
	# player: black blob placeholder (body + mismatched eyes + sneakers)
	_player = _make_player()
	_player.position = Vector2(vp.x * 0.15, vp.y * 0.5)
	add_child(_player)
	# scatter items with seeded jitter
	for def in item_defs:
		var node := _make_item_node(def)
		node.position = Vector2(
			rng.randi_range(int(vp.x * 0.08), int(vp.x * 0.72)),
			rng.randi_range(int(vp.y * 0.15), int(vp.y * 0.85)))
		add_child(node)
		_floor_items.append({"def": def, "node": node})
	# HUD
	_hud = _label("", 30, PALETTE["ink"])
	_hud.position = Vector2(24, 16)
	add_child(_hud)
	_hint = _label("WASD move · SPACE grab · drop at the DOOR", 22, PALETTE["blue"])
	_hint.position = Vector2(24, vp.y - 48)
	add_child(_hint)

func _process(delta: float) -> void:
	if _over:
		return
	_time_left -= delta
	if _time_left <= 0.0:
		_finish()
		return
	# movement
	var dir := Vector2.ZERO
	if Input.is_key_pressed(KEY_W) or Input.is_key_pressed(KEY_UP): dir.y -= 1
	if Input.is_key_pressed(KEY_S) or Input.is_key_pressed(KEY_DOWN): dir.y += 1
	if Input.is_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT): dir.x -= 1
	if Input.is_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT): dir.x += 1
	var slow := 1.0 - 0.08 * _carry_cost()   # heavy hands, slow feet
	_player.position += dir.normalized() * SPEED * slow * delta
	var vp := get_viewport_rect().size
	_player.position = _player.position.clamp(Vector2(20, 20), vp - Vector2(20, 20))
	# deposit when touching the door
	if _door.has_point(_player.position) and not _carrying.is_empty():
		for def in _carrying:
			_banked.append(def)
		_carrying.clear()
	# carried items trail the player
	var hud_carry := []
	for def in _carrying:
		hud_carry.append(String(def["name"]))
	_hud.text = "⏱ %02d   bag %d/%d   %s" % [int(ceil(_time_left)), _carry_cost(), CAPACITY, ", ".join(hud_carry)]
	if _time_left < 10.0:
		_hud.add_theme_color_override("font_color", PALETTE["coral"])

func _unhandled_input(event: InputEvent) -> void:
	if _over or not (event is InputEventKey and event.pressed):
		return
	if event.keycode == KEY_SPACE or event.keycode == KEY_E:
		_try_grab()

func _try_grab() -> void:
	var best := -1
	var best_d := GRAB_RADIUS
	for i in _floor_items.size():
		var d: float = _player.position.distance_to(_floor_items[i]["node"].position)
		if d < best_d:
			best_d = d
			best = i
	if best < 0:
		return
	var def: Dictionary = _floor_items[best]["def"]
	if _carry_cost() + int(def.get("carry_cost", 1)) > CAPACITY:
		_hint.text = "Hands full! Drop at the DOOR first."
		return
	_carrying.append(def)
	_floor_items[best]["node"].queue_free()
	_floor_items.remove_at(best)

func _carry_cost() -> int:
	var c := 0
	for def in _carrying:
		c += int(def.get("carry_cost", 1))
	return c

func _finish() -> void:
	_over = true
	# anything still in hand at the buzzer counts if you're AT the door, else dropped
	if _door.has_point(_player.position):
		for def in _carrying:
			_banked.append(def)
		_carrying.clear()
	var banked_ids: Array = []
	var left_ids: Array = []
	for def in _banked:
		banked_ids.append(String(def["id"]))
	for it in _floor_items:
		left_ids.append(String(it["def"]["id"]))
	for def in _carrying:
		left_ids.append(String(def["id"]))
	_show_tableau(banked_ids, left_ids)

func _show_tableau(banked_ids: Array, left_ids: Array) -> void:
	var vp := get_viewport_rect().size
	var panel := ColorRect.new()
	panel.color = PALETTE["ink"]
	panel.size = Vector2(vp.x * 0.6, vp.y * 0.6)
	panel.position = (vp - panel.size) / 2.0
	add_child(panel)
	var got_names: Array = []
	var left_names: Array = []
	for it in item_defs:
		if banked_ids.has(it["id"]): got_names.append(String(it["name"]))
		elif left_ids.has(it["id"]): left_names.append(String(it["name"]))
	var txt := _label("WHAT YOU GOT\n%s\n\nWHAT YOU LEFT\n%s\n\n[press any key]" % [
		"· " + "\n· ".join(got_names) if not got_names.is_empty() else "· nothing. bold.",
		"· " + "\n· ".join(left_names) if not left_names.is_empty() else "· nothing!"],
		26, PALETTE["cream"])
	txt.position = panel.position + Vector2(36, 28)
	add_child(txt)
	set_process_unhandled_input(false)
	# let the buzzer moment breathe, then wait for a fresh keypress
	await get_tree().create_timer(0.8).timeout
	while Input.is_anything_pressed():
		await get_tree().process_frame
	while not Input.is_anything_pressed():
		await get_tree().process_frame
	done.emit({"banked": banked_ids, "left": left_ids})

func _make_player() -> Node2D:
	var p := Node2D.new()
	var body := ColorRect.new()
	body.color = PALETTE["ink"]
	body.size = Vector2(34, 44)
	body.position = Vector2(-17, -30)
	body.rotation = 0.12   # the permanent forward lean
	p.add_child(body)
	var eye_l := ColorRect.new()
	eye_l.color = Color.WHITE
	eye_l.size = Vector2(9, 11)   # left eye a touch bigger
	eye_l.position = Vector2(-9, -22)
	p.add_child(eye_l)
	var eye_r := ColorRect.new()
	eye_r.color = Color.WHITE
	eye_r.size = Vector2(7, 9)
	eye_r.position = Vector2(4, -21)
	p.add_child(eye_r)
	var cowlick := ColorRect.new()
	cowlick.color = PALETTE["ink"]
	cowlick.size = Vector2(4, 12)
	cowlick.position = Vector2(-2, -42)
	cowlick.rotation = -0.3
	p.add_child(cowlick)
	for x in [-12, 4]:
		var shoe := ColorRect.new()
		shoe.color = PALETTE["cream"]
		shoe.size = Vector2(12, 6)
		shoe.position = Vector2(x, 12)
		p.add_child(shoe)
	return p

func _make_item_node(def: Dictionary) -> Node2D:
	var n := Node2D.new()
	var box := ColorRect.new()
	var col: Color = PALETTE["sage"]
	if def.get("tags", []).has("liquid"): col = PALETTE["yellow"]
	elif def.get("tags", []).has("tech"): col = PALETTE["blue"]
	elif def.get("tags", []).has("vice"): col = PALETTE["coral"]
	box.color = col
	var s := 30 + 8 * int(def.get("carry_cost", 1))
	box.size = Vector2(s, s)
	box.position = Vector2(-s / 2.0, -s / 2.0)
	n.add_child(box)
	var lab := _label(String(def["name"]), 15, PALETTE["ink"])
	lab.position = Vector2(-s / 2.0 - 10, s / 2.0 + 2)
	n.add_child(lab)
	return n

func _label(text: String, size: int, color: Color) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", color)
	return l
