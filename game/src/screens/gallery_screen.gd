class_name GalleryScreen
extends Control
## S18 — THE WALL OF DEATHS. Every ending you've earned, pinned like polaroids
## on the garage corkboard; locked slots stay as empty outlines with a "?".
## Failure is content: the gallery is the collection the player grinds for.
## Owned by MAIN. Opened with G on the title screen (proper button = LANE-FLOW).

signal closed

const PALETTE := {
	"cream": Color("F2EAD3"), "ink": Color("1E1E1E"), "coral": Color("E86A5C"),
	"yellow": Color("F4B942"), "sage": Color("8FA582"), "blue": Color("6E8CA0"),
	"night": Color("2C3238"), "cork": Color("A8845C"),
}
## The launch catalog: named endings the causes map onto (grows toward 60).
const CATALOG := [
	{"id": "ramen_zero", "name": "RAMEN ZERO", "match": "Ramen Zero", "flavor": "the money ran out. the noodles remember."},
	{"id": "flatline", "name": "FOUNDER FLATLINE", "match": "Founder Flatline", "flavor": "morale hit zero. the plant got custody."},
	{"id": "pivot_ground", "name": "PIVOTED INTO THE GROUND", "match": "Pivoted Into The Ground", "flavor": "the rebrand cost everything. great logo though."},
	{"id": "sold", "name": "SOLD THE DREAM", "match": "acquired", "flavor": "you shook the hand. the hand won."},
	{"id": "mvp_alive", "name": "THE MVP LIVED", "match": "SURVIVED", "flavor": "it worked. nobody was more surprised than you."},
	{"id": "walked_shares", "name": "THE GHOST EQUITY", "match": "WITH every share", "flavor": "they left. the shares didn't."},
	{"id": "board_coup", "name": "FIRED FROM YOUR OWN COMPANY", "match": "board", "flavor": "the seats you sold voted you off them."},
	{"id": "payroll", "name": "PAYROLL FRIDAY", "match": "payroll", "flavor": "everyone got paid except the future."},
	{"id": "hype_cliff", "name": "THE HYPE CLIFF", "match": "hype", "flavor": "the internet moved on mid-sentence."},
	{"id": "cofounder_gone", "name": "EVERYONE FOLLOWED DAVE", "match": "quit", "flavor": "one resignation. then the stampede."},
	{"id": "servers", "name": "THE SERVER SMELLED LIKE TOAST", "match": "fire", "flavor": "dad's server gave its life for the cause."},
	{"id": "unknown", "name": "A NEW WAY TO DIE", "match": "", "flavor": "congratulations. this one's yours."},
]

var _font: Font
var _hand: Font
var _armed := false

func _ready() -> void:
	_font = load("res://assets/fonts/Baloo2-Bold.ttf")
	_hand = load("res://assets/fonts/PatrickHand-Regular.ttf")
	set_anchors_preset(Control.PRESET_FULL_RECT)
	var bg := ColorRect.new()
	bg.color = PALETTE["night"]
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(bg)
	# corkboard
	var cork := Panel.new()
	cork.position = Vector2(88, 130)
	cork.size = Vector2(1360, 780)
	var cst := StyleBoxFlat.new()
	cst.bg_color = PALETTE["cork"]
	cst.border_color = Color("6E5334")
	cst.set_border_width_all(14)
	cst.set_corner_radius_all(6)
	cork.add_theme_stylebox_override("panel", cst)
	add_child(cork)

	var profile := SaveSystem.load_profile()
	var seen: Array = profile.get("endings_seen", [])
	var runs: Array = profile.get("runs", [])

	var title := _mk(self, "THE WALL OF DEATHS", 54, PALETTE["cream"], _font)
	title.position = Vector2(96, 40)
	var unlocked := 0
	for c in CATALOG:
		if _earned(c, seen):
			unlocked += 1
	var sub := _mk(self, "%d of %d endings collected  ·  %d runs  ·  best payout $%s" % [unlocked, CATALOG.size(), runs.size(), _fmt(int(profile.get("best_payout", 0)))], 26, Color(PALETTE["cream"], 0.75), _hand)
	sub.position = Vector2(100, 104)

	# polaroid grid 4×3
	var rng := RandomNumberGenerator.new()
	rng.seed = 11
	for i in CATALOG.size():
		var c: Dictionary = CATALOG[i]
		var earned := _earned(c, seen)
		var card := Panel.new()
		card.position = Vector2(150 + (i % 4) * 320, 180 + (i / 4) * 240)
		card.size = Vector2(270, 200)
		card.rotation = rng.randf_range(-0.035, 0.035)
		card.pivot_offset = Vector2(135, 100)
		var st := StyleBoxFlat.new()
		st.bg_color = PALETTE["cream"] if earned else Color(PALETTE["cream"], 0.16)
		st.border_color = PALETTE["ink"] if earned else Color(PALETTE["cream"], 0.25)
		st.set_border_width_all(3)
		st.set_corner_radius_all(4)
		st.shadow_color = Color(0, 0, 0, 0.3 if earned else 0.0)
		st.shadow_size = 8
		st.shadow_offset = Vector2(4, 6)
		card.add_theme_stylebox_override("panel", st)
		add_child(card)
		# pin
		var pin := _mk(card, "📌", 22, PALETTE["coral"], _hand)
		pin.position = Vector2(122, -12)
		if earned:
			var nm := _mk(card, String(c["name"]), 24, PALETTE["ink"], _font)
			nm.position = Vector2(16, 18)
			nm.custom_minimum_size = Vector2(238, 0)
			nm.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
			nm.set_deferred("size", Vector2(238, 0))
			var fl := _mk(card, String(c["flavor"]), 21, Color(PALETTE["ink"], 0.75), _hand)
			fl.position = Vector2(16, 96)
			fl.custom_minimum_size = Vector2(238, 0)
			fl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
			fl.set_deferred("size", Vector2(238, 0))
			# count of runs that ended this way
			var n := 0
			for r in runs:
				if _cause_matches(c, String(r.get("cause", ""))):
					n += 1
			if n > 1:
				var badge := _mk(card, "×%d" % n, 22, PALETTE["coral"], _font)
				badge.position = Vector2(224, 160)
		else:
			var q := _mk(card, "?", 84, Color(PALETTE["cream"], 0.30), _font)
			q.position = Vector2(112, 46)
			var hint := _mk(card, "undiscovered", 19, Color(PALETTE["cream"], 0.30), _hand)
			hint.position = Vector2(84, 152)
		if earned:
			card.mouse_filter = Control.MOUSE_FILTER_STOP
			card.mouse_entered.connect(func():
				var t := create_tween()
				t.tween_property(card, "scale", Vector2(1.05, 1.05), 0.09).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT))
			card.mouse_exited.connect(func():
				var t := create_tween()
				t.tween_property(card, "scale", Vector2.ONE, 0.1))

	var hint := _mk(self, "any key to close", 22, Color(PALETTE["cream"], 0.5), _hand)
	hint.position = Vector2(680, 972)
	_arm()

func _earned(c: Dictionary, seen: Array) -> bool:
	for s in seen:
		if _cause_matches(c, String(s)):
			return true
	return false

func _cause_matches(c: Dictionary, cause: String) -> bool:
	var m := String(c["match"])
	if m == "":
		return false
	return cause.findn(m) != -1

func _mk(parent: Node, text: String, size: int, col: Color, font: Font) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_override("font", font)
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", col)
	parent.add_child(l)
	return l

func _fmt(v: int) -> String:
	var t := str(absi(v))
	var out := ""
	while t.length() > 3:
		out = "," + t.substr(t.length() - 3) + out
		t = t.substr(0, t.length() - 3)
	return ("-" if v < 0 else "") + t + out

func _arm() -> void:
	await get_tree().create_timer(0.4).timeout
	_armed = true

func _unhandled_input(event: InputEvent) -> void:
	if _armed and ((event is InputEventKey and event.pressed) or (event is InputEventMouseButton and event.pressed)):
		accept_event()
		closed.emit()
		queue_free()
