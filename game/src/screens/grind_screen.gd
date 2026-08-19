class_name GrindScreen
extends Control
## Turn-based GRIND, styled as the game: ink-outlined event cards that flip in,
## hand-drawn font, floating stat deltas, a cash ticker, and sound. Weekly
## sequence (Dossier §3.2, slice-simplified): upkeep → timebombs → card → meters.

signal done(result: Dictionary)   # {death: cause} or {victory: true}

const PALETTE := {
	"cream": Color("F2EAD3"), "ink": Color("1E1E1E"), "coral": Color("E86A5C"),
	"yellow": Color("F4B942"), "sage": Color("8FA582"), "blue": Color("6E8CA0"),
}

var state: GameState
var content: ContentDb
var rng: SeededRng
var record: RunRecord
var generator: EventGenerator

var _font: Font
var _meters_box: HBoxContainer
var _meter_labels := {}
var _shown_cash := 0
var _week_label: Label
var _card_panel: Panel
var _card_title: Label
var _card_body: Label
var _choices_box: VBoxContainer
var _next_btn: Button
var _tier_dot: Label
var _sfx := {}

func setup(p_state: GameState, p_content: ContentDb, p_rng: SeededRng, p_record: RunRecord, p_gen: EventGenerator) -> void:
	state = p_state
	content = p_content
	rng = p_rng
	record = p_record
	generator = p_gen

func _ready() -> void:
	_font = load("res://assets/fonts/PatrickHand-Regular.ttf")
	_shown_cash = state.cash
	for n in ["card_flip", "cash", "death", "win"]:
		var p := AudioStreamPlayer.new()
		p.stream = load("res://assets/sfx/%s.wav" % n)
		add_child(p)
		_sfx[n] = p

	set_anchors_preset(Control.PRESET_FULL_RECT)
	var bg := ColorRect.new()
	bg.color = PALETTE["cream"]
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(bg)
	if ResourceLoader.exists("res://assets/env/garage.png"):
		var scene_bg := TextureRect.new()
		scene_bg.texture = load("res://assets/env/garage.png")
		scene_bg.set_anchors_preset(Control.PRESET_FULL_RECT)
		scene_bg.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		scene_bg.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
		scene_bg.modulate = Color(1, 1, 1, 0.35)
		scene_bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
		add_child(scene_bg)

	_week_label = _label("WEEK 1", 36, PALETTE["ink"])
	_week_label.position = Vector2(40, 26)
	add_child(_week_label)

	_meters_box = HBoxContainer.new()
	_meters_box.position = Vector2(560, 26)
	_meters_box.add_theme_constant_override("separation", 18)
	add_child(_meters_box)
	for m in [["cash", PALETTE["sage"]], ["product", PALETTE["blue"]], ["users", PALETTE["yellow"]], ["morale", PALETTE["coral"]], ["hype", PALETTE["coral"]], ["equity", PALETTE["ink"]]]:
		var chip := PanelContainer.new()
		var st := StyleBoxFlat.new()
		st.bg_color = Color.WHITE
		st.border_color = m[1]
		st.set_border_width_all(3)
		st.set_corner_radius_all(12)
		st.content_margin_left = 14
		st.content_margin_right = 14
		st.content_margin_top = 4
		st.content_margin_bottom = 4
		chip.add_theme_stylebox_override("panel", st)
		var l := _label("", 26, PALETTE["ink"])
		chip.add_child(l)
		_meters_box.add_child(chip)
		_meter_labels[m[0]] = l

	_card_panel = Panel.new()
	_card_panel.position = Vector2(268, 150)
	_card_panel.size = Vector2(1000, 640)
	_card_panel.pivot_offset = Vector2(500, 320)
	var style := StyleBoxFlat.new()
	style.bg_color = Color.WHITE
	style.border_color = PALETTE["ink"]
	style.set_border_width_all(5)
	style.set_corner_radius_all(18)
	style.shadow_color = Color(0.12, 0.12, 0.12, 0.18)
	style.shadow_size = 12
	style.shadow_offset = Vector2(6, 8)
	_card_panel.add_theme_stylebox_override("panel", style)
	add_child(_card_panel)

	_tier_dot = _label("", 22, PALETTE["blue"])
	_tier_dot.position = Vector2(950, 14)
	_card_panel.add_child(_tier_dot)

	_card_title = _label("", 46, PALETTE["ink"])
	_card_title.position = Vector2(44, 28)
	_card_title.size = Vector2(910, 64)
	_card_panel.add_child(_card_title)

	var rule := ColorRect.new()
	rule.color = PALETTE["coral"]
	rule.position = Vector2(44, 96)
	rule.size = Vector2(180, 4)
	_card_panel.add_child(rule)

	_card_body = _label("", 30, PALETTE["ink"])
	_card_body.position = Vector2(44, 116)
	_card_body.size = Vector2(910, 210)
	_card_body.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_card_panel.add_child(_card_body)

	_choices_box = VBoxContainer.new()
	_choices_box.position = Vector2(44, 340)
	_choices_box.size = Vector2(910, 270)
	_choices_box.add_theme_constant_override("separation", 16)
	_card_panel.add_child(_choices_box)

	_next_btn = Button.new()
	_next_btn.text = "NEXT WEEK  →"
	_next_btn.position = Vector2(1160, 880)
	_next_btn.size = Vector2(300, 84)
	_style_button(_next_btn, PALETTE["yellow"], 34)
	_next_btn.pressed.connect(_start_week)
	add_child(_next_btn)

	_refresh_meters(true)
	_show_no_card("You made it out with your stuff.\nThe garage era begins. Hit NEXT WEEK.")
	generator.prefetch(state)

func _label(text: String, size: int, color: Color) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_override("font", _font)
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", color)
	return l

func _style_button(b: Button, col: Color, fsize: int) -> void:
	b.add_theme_font_override("font", _font)
	b.add_theme_font_size_override("font_size", fsize)
	b.add_theme_color_override("font_color", PALETTE["ink"])
	b.add_theme_color_override("font_hover_color", PALETTE["ink"])
	var st := StyleBoxFlat.new()
	st.bg_color = Color.WHITE
	st.border_color = PALETTE["ink"]
	st.set_border_width_all(4)
	st.set_corner_radius_all(14)
	st.content_margin_left = 18
	st.content_margin_right = 18
	b.add_theme_stylebox_override("normal", st)
	var sh := st.duplicate()
	sh.bg_color = col
	b.add_theme_stylebox_override("hover", sh)
	var sp := st.duplicate()
	sp.bg_color = col.darkened(0.1)
	b.add_theme_stylebox_override("pressed", sp)
	var sd := st.duplicate()
	sd.bg_color = Color(0.93, 0.9, 0.83)
	sd.border_color = Color(0.6, 0.58, 0.52)
	b.add_theme_stylebox_override("disabled", sd)

func _refresh_meters(instant: bool = false) -> void:
	_week_label.text = "%s · WEEK %d" % [state.company_name.to_upper(), state.week]
	if instant:
		_shown_cash = state.cash
	else:
		var tw := create_tween()
		tw.tween_method(func(v): _shown_cash = int(v); _set_cash_text(), float(_shown_cash), float(state.cash), 0.5)
		if state.cash != _shown_cash:
			_sfx["cash"].play()
	_set_cash_text()
	_meter_labels["product"].text = "🛠 %d" % state.product
	_meter_labels["users"].text = "📈 %d" % state.traction
	_meter_labels["morale"].text = "😊 %d" % state.morale
	_meter_labels["hype"].text = "🔥 %d" % state.hype
	_meter_labels["equity"].text = "🥧 %.0f%%" % state.founder_pct

func _set_cash_text() -> void:
	_meter_labels["cash"].text = "💰 $%d  (−$%d/wk)" % [_shown_cash, state.burn_per_week()]

func _float_delta(text: String, col: Color, idx: int) -> void:
	var l := _label(text, 32, col)
	l.position = Vector2(1290, 200 + idx * 46)
	add_child(l)
	var tw := create_tween()
	tw.tween_property(l, "position:y", l.position.y - 70.0, 1.1).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tw.parallel().tween_property(l, "modulate:a", 0.0, 1.1)
	tw.tween_callback(l.queue_free)

func _start_week() -> void:
	_next_btn.disabled = true
	state.week += 1
	state.cash -= state.burn_per_week()
	# competences drive the passive economy: Build ships, Sell converts, Grit endures
	var passive_build := 2 + int(state.competences.get("build", 3))
	if state.has_item("itm_laptop"):
		passive_build += 2
	if state.has_item("itm_dads_server"):
		passive_build += 1
	state.product = clampi(state.product + passive_build, 0, 100)
	if state.product >= 40:
		state.traction += 1 + int(int(state.competences.get("sell", 3)) / 2.0) + int(state.hype / 20.0)
	state.morale += int(int(state.competences.get("grit", 3)) / 3.0)
	if state.structure_id == "solo":
		state.morale -= 1   # nobody to split the 2am dread with
	state.clampi_meters()
	_refresh_meters()
	if state.cash < 0:
		_die("Ramen Zero — the money ran out in week %d." % state.week)
		return
	for tb in state.timebombs.duplicate():
		tb["weeks_left"] -= 1
		if tb["weeks_left"] <= 0:
			state.timebombs.erase(tb)
			var ev: Dictionary = content.events.get(tb["event"], {})
			if not ev.is_empty():
				_present_card(ev)
				return
	var card := generator.next_card(state, content, rng)
	if card.is_empty():
		_show_no_card("A quiet week. Suspiciously quiet.")
	else:
		_present_card(card)

func _present_card(ev: Dictionary) -> void:
	_card_title.text = String(ev.get("title", "?"))
	_card_body.text = String(ev.get("body", ""))
	_tier_dot.text = "✦" if ev.get("tier", "authored") == "generated" else ""
	for c in _choices_box.get_children():
		c.queue_free()
	for choice in ev.get("choices", []):
		var b := Button.new()
		b.text = String(choice.get("label", "…"))
		_style_button(b, PALETTE["sage"], 30)
		b.custom_minimum_size = Vector2(0, 62)
		b.pressed.connect(_on_choice.bind(ev, choice))
		_choices_box.add_child(b)
	# flip-in
	_sfx["card_flip"].play()
	_card_panel.scale = Vector2(0.02, 1.0)
	var tw := create_tween()
	tw.tween_property(_card_panel, "scale", Vector2(1.06, 1.0), 0.16).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tw.tween_property(_card_panel, "scale", Vector2.ONE, 0.1)

func _on_choice(ev: Dictionary, choice: Dictionary) -> void:
	var log := EffectOps.apply_all(choice.get("effects", []), state)
	record.log_event(state.week, ev, String(choice.get("label", "")), log)
	var i := 0
	for line in log:
		var col: Color = PALETTE["sage"]
		if line.begins_with("cash -") or line.contains("-"):
			col = PALETTE["coral"]
		_float_delta(line, col, i)
		i += 1
	_refresh_meters()
	if state.morale <= 0:
		_die("Founder Flatline — morale hit zero in week %d." % state.week)
		return
	if state.product >= 60 and state.traction >= 10:
		record.log_event(state.week, {"id": "milestone", "title": "MVP + first users"}, "era gate reached", [])
		_sfx["win"].play()
		await get_tree().create_timer(0.9).timeout
		done.emit({"victory": true})
		return
	for c in _choices_box.get_children():
		if c is Button:
			c.disabled = true
	_next_btn.disabled = false
	generator.prefetch(state)

func _show_no_card(msg: String) -> void:
	_card_title.text = ""
	_tier_dot.text = ""
	_card_body.text = msg
	for c in _choices_box.get_children():
		c.queue_free()
	_next_btn.disabled = false

func _die(cause: String) -> void:
	state.dead = true
	state.death_cause = cause
	record.log_death(state.week, cause)
	_sfx["death"].play()
	await get_tree().create_timer(1.0).timeout
	done.emit({"death": cause})
