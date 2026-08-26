class_name Binder
extends Control
## THE RING BINDER — the founder's dashboard as a real object (docs/design/
## DECISIONS.md § "Binder rework — owner picks", mockups/00 pick A). Never a
## SaaS panel: a kraft cover, drawn rings, a side rail of divider groups with
## colored index tabs poking left, and the open group fanning its pages.
##
## THE FRAME OWNS: the binder body, the rail, the alarm-red climb, the group
## overviews, the first-open tour, the momentary tab slot and the cover state.
## IT OWNS NO PAGE: every desk lives in its own file under ui/desks/ and is
## handed this node (docs/design/HOOKS.md), so a subsystem grows its page
## without ever opening this file — and one page has exactly one writer.
##
## FOG OF WAR: precision follows state.analytics_level (0-3) — unchanged.
## ESC CONTRACT: Esc pops desk states (armed → mode), then the overview, then
## closes the binder; TAB/B always close. The tour eats Esc as "skip".
##
## Usage:  var b := Binder.new(); b.setup(state); add_child(b)
##         b.closed.connect(...)  — TAB/B/Esc or the close corner dismisses it.

signal closed

const CREAM := Color("F2EAD3")
const PAPER2 := Color("F6F0DE")
const CARD := Color("EFE6CE")
const KRAFT := Color("DDBE8C")
const KRAFT2 := Color("CBA96F")
const INK := Color("1E1E1E")
const PEN := Color("E86A5C")
const SAGE := Color("8FA582")
const YELL := Color("F4B942")
const BLUE := Color("6E8CA0")
const ALERT := Color("D93425")
const HAND := "res://assets/fonts/PatrickHand-Regular.ttf"
const DISPLAY := "res://assets/fonts/Baloo2-Bold.ttf"

## THE TAXONOMY (DECISIONS: 18 desks in 4 groups). Group order is rail order;
## desk order is page order; THE LOG's "this week" is the default landing desk.
const GROUPS := [
	{"name": "REVENUE", "col": SAGE,
		"desks": ["offers", "customers", "in motion", "growth"]},
	{"name": "COSTS", "col": PEN,
		"desks": ["spend", "team", "recruitment", "bills", "the bank", "the works"]},
	{"name": "THE COMPANY", "col": BLUE,
		"desks": ["what we make", "cap table", "the raise", "the street", "threats", "pivot"]},
	{"name": "THE LOG", "col": YELL,
		"desks": ["this week", "history", "events"]},
]

## THE OLD TEN TABS, kept as the LEGACY ORDER: the engine's attention registry,
## the garage's focus calls and the shot harnesses all still speak these names,
## and `_tab` (poked by tests) still indexes this list. The alias map turns any
## old name into its new desk.
const TABS := ["vitals", "the ledger", "the bank", "pricing", "customers",
	"product", "crew", "cap table", "the street", "threats"]
const LEGACY_TO_DESK := {
	"vitals": "this week", "the ledger": "spend", "the bank": "the bank",
	"pricing": "offers", "customers": "customers", "product": "what we make",
	"crew": "team", "cap table": "cap table", "the street": "the street",
	"threats": "threats", "pipeline": "in motion", "factory": "the works",
	"catalog": "offers", "bank": "the bank", "cap": "cap table",
	"street": "the street", "ledger": "spend",
}

## THE FRAME GEOMETRY (mockups/00 variant A, scaled into the 1536×1024 view).
## cover 54 + ringbar 46 + rail ~196 + sheet — the sheet's content pane keeps
## the binder-wide 1160 width, so every shipped desk lands unchanged.
const FRAME_POS := Vector2(16, 28)
const FRAME_SIZE := Vector2(1504, 968)
const COVER_W := 54.0
const RING_W := 46.0
const STACK_X := 100.0            ## cover + ringbar
const RAIL_X := 124.0             ## divider boxes start here (frame-local)
const RAIL_BOX_W := 182.0         ## a divider box; its index tab pokes 16 left
const SHEET_RULE_X := 318.0       ## the sheet's left rule
const CONTENT_POS := Vector2(344, 36)
const CONTENT_SIZE := Vector2(1160, 880)
## The content pane the desks draw into — unchanged widths, taller pane.
const PANE_W := 1160.0
## Legacy tab-row constants (retired furniture, kept so old readers stay sane).
const TAB_X0 := 24.0
const TAB_PITCH := 120.0
const TAB_W := 118.0
const TAB_H := 44.0

## THE BINDER PORTRAIT (DECISIONS § THE BINDER PORTRAIT, owner-corrected):
## the portrait is a DIEGETIC OBJECT on the room painting, bottom-left — it
## replaces the binder doorway button. The PNG is cached by the generation
## lane; the drawn mini-binder (same silhouette) is the instant placeholder
## and permanent fallback. The label is BLANK in the image — the company name
## is overlaid in the hand inside LABEL_RECT (fractions of the object's rect,
## so the generation lane can tune placement). Below LABEL_MIN_PX the name is
## omitted rather than rendered illegibly.
const PORTRAIT_PATH := "user://binder_portrait.png"
const LABEL_RECT := Rect2(0.26, 0.42, 0.48, 0.13)
const LABEL_FONT_SIZE := 46
const LABEL_MIN_PX := 10
const TOUR_FLAG := "user://seen_binder_tour"

var state: GameState
var generator: EventGenerator = null   # the street's pricing road (01 WAIT state)
## DESK-LOCAL STATE, one visit long (docs/design/10-interface-language.md §4.8):
## a desk's page mode, its expanded row, its armed control. Never saved, cleared
## on every page change, dead with this node.
var desk := {}
var _font: Font
var _font_d: Font
## Navigation: one open group, one active page, and the transient layers.
var _open_group := 3               ## THE LOG opens first — "this week" lands
var _page := "this week"
var _overview := -1                ## ≥0 = that group's overview covers the sheet
var _tour := -1                    ## ≥0 = the tour owns the binder
var _momentary: Array = []         ## [{id, group, label, wks}] — gold tabs
## LEGACY SHIM: tests and old callers poke `_tab` (an index into TABS) and call
## refresh. The shim maps it onto the new navigation without clearing the desk
## dict the harness just seeded.
var _tab := -1
var _legacy_applied := -1
## The shot benches drive the binder's states by hand; this keeps the tour
## from hijacking their sheets (a real install never touches it).
var tour_enabled := true
var _frame: Control
var _rail: Control
var _content: Control
var _close_btn: Button
var _tour_demo_red := false

func setup(p_state: GameState, p_gen: EventGenerator = null) -> void:
	generator = p_gen
	state = p_state

func _ready() -> void:
	_font = load(HAND)
	_font_d = load(DISPLAY)
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	var dim := ColorRect.new()
	dim.color = Color(0.05, 0.05, 0.06, 0.55)
	dim.set_anchors_preset(Control.PRESET_FULL_RECT)
	dim.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(dim)

	_frame = _FrameBoard.new()
	_frame.position = FRAME_POS
	_frame.set_deferred("size", FRAME_SIZE)
	_frame.mouse_filter = Control.MOUSE_FILTER_STOP
	add_child(_frame)

	_rail = Control.new()
	_rail.position = Vector2.ZERO
	_rail.set_deferred("size", FRAME_SIZE)
	_rail.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_frame.add_child(_rail)

	_content = Control.new()
	_content.position = CONTENT_POS
	_content.set_deferred("size", CONTENT_SIZE)
	_content.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_frame.add_child(_content)

	_close_btn = Button.new()
	_close_btn.flat = true
	_close_btn.text = "×"
	_close_btn.add_theme_font_override("font", _font)
	_close_btn.add_theme_font_size_override("font_size", 46)
	_close_btn.add_theme_color_override("font_color", PEN)
	for stn in ["normal", "hover", "pressed", "focus"]:
		_close_btn.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	_close_btn.position = Vector2(FRAME_SIZE.x - 60.0, 2.0)
	_close_btn.set_deferred("size", Vector2(52, 52))
	_close_btn.pressed.connect(func() -> void: _dismiss())
	_frame.add_child(_close_btn)

	gui_input.connect(func(ev: InputEvent) -> void:
		if ev is InputEventMouseButton and ev.pressed:
			_dismiss())
	_refresh()

## ESC POPS BEFORE IT CLOSES (docs/design/10-interface-language.md §4.2): the
## tour eats Esc as "skip"; then a desk's armed control, then its mode, then an
## open overview, and only from a page's base state does Esc shut the binder.
## TAB and B always shut it — the same keys that opened it.
func _unhandled_key_input(ev: InputEvent) -> void:
	if ev is InputEventKey and ev.pressed and ev.keycode in [KEY_ESCAPE, KEY_TAB, KEY_B]:
		accept_event()
		if _tour >= 0:
			if ev.keycode == KEY_ESCAPE:
				_tour_finish()
				return
			_tour_finish()
			_dismiss()
			return
		if ev.keycode == KEY_ESCAPE and _desk_pop():
			return
		if ev.keycode == KEY_ESCAPE and _overview >= 0:
			_overview = -1
			_refresh()
			return
		_dismiss()

## One step back inside the current desk. True = something was popped, so the
## press is spent and the binder stays open. The arrange shell lives in
## desk["mode"], so Esc abandoning a staged change is this same pop.
func _desk_pop() -> bool:
	if desk.has("armed"):
		desk.erase("armed")
		_refresh()
		return true
	if String(desk.get("mode", "")) != "":
		desk["mode"] = ""
		desk.erase("row")
		desk.erase("chip")
		desk.erase("staged")
		_refresh()
		return true
	return false

func _dismiss() -> void:
	closed.emit()
	queue_free()

# ─────────────────────────────── navigation ──────────────────────────────────

## Open the binder ON a desk — old names and new names both land. The pre-roll
## review's "go fix it" arrives with the attention row's own (old) desk word.
func focus_desk(desk_name: String) -> void:
	var id := String(LEGACY_TO_DESK.get(desk_name, desk_name))
	if _find_group(id) < 0:
		return
	desk.clear()
	open_page(id)

## The page press: sets the active sheet, clears desk-local state, keeps the
## group. Also the overview's card press and the momentary tab press.
func open_page(id: String) -> void:
	var gi := _find_group(id)
	if gi < 0:
		return
	if _page != id:
		desk.clear()
	_overview = -1
	if gi != _open_group:
		_open_group = gi
	_page = id
	if _content != null:
		_refresh()
		_slide_sheet()

## The divider-header press. Closed → the group opens (kraft→paper ease, pages
## fan). Open → THE DASHBOARD QUARTET: the group's overview covers the sheet.
func press_group(gi: int) -> void:
	if _tour >= 0:
		return
	if gi == _open_group:
		_overview = gi if _overview != gi else -1
		_refresh()
		return
	_open_group = gi
	_overview = -1
	var desks: Array = (GROUPS[gi] as Dictionary).get("desks", [])
	_page = String(desks[0]) if desks.size() > 0 else _page
	desk.clear()
	_refresh()
	_animate_open(gi)
	_slide_sheet()

func _find_group(id: String) -> int:
	for gi in GROUPS.size():
		if ((GROUPS[gi] as Dictionary).get("desks", []) as Array).has(id):
			return gi
	for m in _momentary:
		if String((m as Dictionary).get("id", "")) == id:
			return int((m as Dictionary).get("group", 2))
	return -1

# ────────────────────────────── momentary tabs ───────────────────────────────

## THE MOMENTARY TAB SLOT (DECISIONS §4 THE OFFER): a gold page tab any desk
## can summon into a group; it folds away when resolved. `group_name` is the
## rail name ("THE COMPANY"); `wks` feeds the deadline clock chip.
func summon_momentary(id: String, group_name: String, label: String, wks: int) -> void:
	for m in _momentary:
		if String((m as Dictionary).get("id", "")) == id:
			return
	var gi := 2
	for i in GROUPS.size():
		if String((GROUPS[i] as Dictionary).get("name", "")) == group_name:
			gi = i
	_momentary.append({"id": id, "group": gi, "label": label, "wks": wks})
	if _content != null:
		_refresh()

func resolve_momentary(id: String) -> void:
	for i in range(_momentary.size() - 1, -1, -1):
		if String((_momentary[i] as Dictionary).get("id", "")) == id:
			_momentary.remove_at(i)
	if _page == id:
		var desks: Array = (GROUPS[_open_group] as Dictionary).get("desks", [])
		_page = String(desks[0]) if desks.size() > 0 else "this week"
		desk.clear()
	if _content != null:
		_refresh()

## The debug summon — the shell's first client until the board lane wires the
## real buyout event: THE OFFER slides into THE COMPANY with a 3-week clock.
func debug_summon_offer() -> void:
	summon_momentary("the offer", "THE COMPANY", "THE OFFER", 3)

# ─────────────────────────────── composition ─────────────────────────────────

func _refresh() -> void:
	# LEGACY SHIM: a harness that poked `_tab` gets the old tab's new desk,
	# with the desk dict it seeded left exactly as found.
	if _tab >= 0 and _tab < TABS.size():
		var want := String(LEGACY_TO_DESK.get(TABS[_tab], "this week"))
		if _tab != _legacy_applied or want != _page:
			_overview = -1
			_tour = -1
			var gi := _find_group(want)
			if gi >= 0:
				_open_group = gi
				_page = want
		_legacy_applied = _tab
	# the first open of an install: the tour
	if tour_enabled and _tour < 0 and not FileAccess.file_exists(TOUR_FLAG) \
			and _legacy_applied < 0:
		_tour = 0
		_tour_apply()
	for c in _content.get_children():
		c.queue_free()
	for c2 in _rail.get_children():
		c2.queue_free()
	_frame.queue_redraw()
	# THE OFFER's gold tab follows the buyout folder (L-OWN): summoned while
	# an offer is on the table, folded away when it leaves. resolve is guarded
	# on presence — resolve_momentary refreshes unconditionally and would loop.
	if state != null:
		if not state.buyout_offer.is_empty():
			summon_momentary("the offer", "THE COMPANY", "THE OFFER",
				maxi(int(state.buyout_offer.get("expires_wk", 0)) - state.week, 0))
		elif _find_group("the offer") >= 0:
			resolve_momentary("the offer")
	_build_rail()
	# THE SHEET: tour > overview > the page's own desk
	if _tour >= 0:
		DeskTour.draw(self, _tour)
		return
	if _overview >= 0:
		DeskOverview.draw(self, _overview)
		return
	_dispatch(_page)

## THE DESK DISPATCH (docs/design/HOOKS.md): every page drawn by its own file.
func _dispatch(id: String) -> void:
	match id:
		"offers": DeskOffers.draw(self)
		"customers": DeskCustomersPage.draw(self)
		"in motion": DeskInMotion.draw(self)
		"growth": DeskGrowth.draw(self)
		"spend": DeskSpend.draw(self)
		"team": DeskTeam.draw(self)
		"recruitment": DeskRecruit.draw(self)
		"bills": DeskBills.draw(self)
		"the bank": DeskBankPage.draw(self)
		"the works": DeskWorks.draw(self)
		"what we make": DeskMake.draw(self)
		"cap table": DeskCapPage.draw(self)
		"the raise": DeskRaise.draw(self)
		"the street": DeskStreetPage.draw(self)
		"threats": DeskThreatsPage.draw(self)
		"pivot": DeskPivot.draw(self)
		"this week": DeskThisWeek.draw(self)
		"history": DeskHistory.draw(self)
		"events": DeskEvents.draw(self)
		"the offer": DeskOffer.draw(self)
		_: DeskThisWeek.draw(self)

## THE PRESS ROUTER: id-dispatch for desks that prefer it. Old names keep
## working (the shipped desks' controls are embedded unchanged); new pages
## answer under their own names.
func desk_press(desk_name: String, id: String) -> void:
	match desk_name:
		"vitals": DeskVitals.handle(self, id)
		"threats": DeskThreats.handle(self, id)
		"catalog": DeskCatalog.handle(self, id)
		"crew": DeskCrew.handle(self, id)
		"street": DeskStreet.handle(self, id)
		"customers": DeskCustomers.handle(self, id)
		"pipeline": DeskPipeline.handle(self, id)
		"bank": DeskBank.handle(self, id)
		"product": DeskProduct.handle(self, id)
		"factory": DeskFactory.handle(self, id)
		"cap": DeskCap.handle(self, id)
		"works": DeskWorks.handle(self, id)
		"arrange": DeskArrange.handle(self, id)
		"offer": DeskOffer.handle(self, id)
		"overview": DeskOverview.handle(self, id)
	_refresh()

# ─────────────────────────────── the rail ────────────────────────────────────

## The severity every desk wears, from the ONE list behind every mark: the
## engine's attention registry, its old desk words aliased onto the new pages.
func desk_severities() -> Dictionary:
	var worst := {}
	for it in SimEngine.attention_items(state):
		var old := String((it as Dictionary).get("desk", ""))
		var id := String(LEGACY_TO_DESK.get(old, old))
		worst[id] = maxi(int(worst.get(id, 0)), int((it as Dictionary).get("severity", 1)))
	if _tour_demo_red:
		worst["threats"] = 3
	return worst

func _build_rail() -> void:
	var sev := desk_severities()
	var y := 24.0
	for gi in GROUPS.size():
		var g: Dictionary = GROUPS[gi]
		var desks: Array = g.get("desks", [])
		var moms: Array = []
		for m in _momentary:
			if int((m as Dictionary).get("group", -1)) == gi:
				moms.append(m)
		var open := gi == _open_group
		var g_sev := 0
		for d in desks:
			g_sev = maxi(g_sev, int(sev.get(String(d), 0)))
		var box_h := 48.0
		if open:
			box_h = 52.0 + float(desks.size() + moms.size()) * 40.0 + 12.0
		var div := _Divider.new()
		div.col = g.get("col", SAGE)
		div.open_t = 1.0 if open else 0.0
		div.sev = 0 if open else g_sev
		div.font = _font
		div.mouse_filter = Control.MOUSE_FILTER_IGNORE
		div.position = Vector2(RAIL_X, y)
		div.set_deferred("size", Vector2(RAIL_BOX_W, box_h))
		_rail.add_child(div)
		# the header press: open the group, or open its overview
		var head := Button.new()
		head.flat = true
		head.text = ""
		head.position = Vector2(RAIL_X, y)
		head.set_deferred("size", Vector2(RAIL_BOX_W, 48.0))
		for stn in ["normal", "hover", "pressed", "focus"]:
			head.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
		var gidx := gi
		head.pressed.connect(func() -> void: press_group(gidx))
		_rail.add_child(head)
		var name_l := Label.new()
		name_l.text = String(g.get("name", ""))
		name_l.add_theme_font_override("font", _font)
		name_l.add_theme_font_size_override("font_size", 19)
		name_l.add_theme_color_override("font_color", INK)
		name_l.position = Vector2(RAIL_X + 12.0, y + 10.0)
		# full-width, single line — the bang chip rides the box CORNER instead
		# (the narrow lane wrapped "THE COMPANY"; owner screenshot)
		name_l.custom_minimum_size = Vector2(RAIL_BOX_W - 24.0, 0)
		name_l.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_rail.add_child(name_l)
		# the live count/total on the header (closed carries it; open keeps it)
		var cnt := Label.new()
		cnt.text = _group_count(gi)
		cnt.add_theme_font_override("font", _font)
		cnt.add_theme_font_size_override("font_size", 15)
		cnt.add_theme_color_override("font_color", Color(INK, 0.5))
		cnt.position = Vector2(RAIL_X + RAIL_BOX_W - 66.0, y + 14.0)
		cnt.custom_minimum_size = Vector2(58.0, 0)
		cnt.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		cnt.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_rail.add_child(cnt)
		# a closed divider with attention: the red bang chip climbs onto it
		if not open and g_sev > 0:
			var chip := _BangChip.new()
			chip.pulse = g_sev >= 3
			chip.font = _font_d
			chip.mouse_filter = Control.MOUSE_FILTER_IGNORE
			# the corner-badge slot: clear of the name line entirely
			chip.position = Vector2(RAIL_X + RAIL_BOX_W - 26.0, y - 6.0)
			chip.set_deferred("size", Vector2(22.0, 22.0))
			_rail.add_child(chip)
		if open:
			var py := y + 52.0
			for d2 in desks:
				var did := String(d2)
				_page_tab(did, py, int(sev.get(did, 0)), false, "")
				py += 40.0
			for m2 in moms:
				var md: Dictionary = m2
				_page_tab(String(md.get("id", "")), py, 0, true,
					"%d wks" % int(md.get("wks", 0)))
				py += 40.0
		y += box_h + 12.0

## One page tab in the fan. Red-filled with the white bang when its desk has
## attention; gold with the deadline clock when momentary.
func _page_tab(id: String, y: float, severity: int, gold: bool, clock_text: String) -> void:
	var tab := _PageTab.new()
	tab.text_v = id
	tab.active = id == _page and _overview < 0
	tab.sev = severity
	tab.gold = gold
	tab.clock_text = clock_text
	tab.font = _font
	tab.font_d = _font_d
	tab.position = Vector2(RAIL_X + 8.0, y)
	tab.set_deferred("size", Vector2(RAIL_BOX_W - 16.0, 36.0))
	var did := id
	tab.pressed.connect(func() -> void: open_page(did))
	_rail.add_child(tab)

## The live figure a closed divider carries: money for the money groups, the
## week for the log — the binder read without opening it.
func _group_count(gi: int) -> String:
	var pnl: Dictionary = state.get_meta("pnl", {})
	match gi:
		0:
			var rev := int(pnl.get("revenue", 0))
			return ("$%s/wk" % _fmt(rev)) if rev > 0 else "—"
		1:
			var burn := int(pnl.get("burn", 0))
			return ("$%s/wk" % _fmt(burn)) if burn > 0 else "—"
		2:
			return "6"
		3:
			return "wk %d" % state.week
	return ""

## ~0.3s kraft→paper ease + the pages fanning with ~40ms stagger.
func _animate_open(gi: int) -> void:
	# EVERY TWEEN RIDES ITS OWN NODE: a refresh mid-animation frees the rail,
	# and a tween created on the binder would then call into freed captures
	# ("Lambda capture was freed" — caught by the first kit_shots run). A
	# node-bound tween dies with its node instead.
	var idx := 0
	for c in _rail.get_children():
		if c is _Divider:
			if idx == gi:
				var div := c as _Divider
				div.open_t = 0.0
				var tw := div.create_tween()
				tw.tween_method(func(t: float) -> void:
					div.open_t = t
					div.queue_redraw(), 0.0, 1.0, 0.3).set_ease(Tween.EASE_OUT)
			idx += 1
	var fan := 0
	for c2 in _rail.get_children():
		if c2 is _PageTab:
			var tabc := c2 as _PageTab
			tabc.modulate.a = 0.0
			var from_x := tabc.position.x - 14.0
			var to_x := tabc.position.x
			tabc.position.x = from_x
			var tw2 := tabc.create_tween()
			tw2.tween_interval(0.04 * float(fan))
			tw2.tween_property(tabc, "modulate:a", 1.0, 0.12)
			var tw3 := tabc.create_tween()
			tw3.tween_interval(0.04 * float(fan))
			tw3.tween_property(tabc, "position:x", to_x, 0.12) \
				.set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
			fan += 1

## The active sheet slides out from under its tab.
func _slide_sheet() -> void:
	if _content == null:
		return
	_content.position = CONTENT_POS + Vector2(-26.0, 0)
	_content.modulate.a = 0.35
	var tw := create_tween()
	tw.set_parallel(true)
	tw.tween_property(_content, "position", CONTENT_POS, 0.22) \
		.set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	tw.tween_property(_content, "modulate:a", 1.0, 0.18)

# ────────────────────────── the binder, as an object ─────────────────────────

## The portrait texture, or null — never awaited: the drawn mini-binder is the
## instant placeholder and the permanent fallback.
static func portrait_texture() -> Texture2D:
	if not FileAccess.file_exists(PORTRAIT_PATH):
		return null
	var img := Image.new()
	if img.load(ProjectSettings.globalize_path(PORTRAIT_PATH)) != OK:
		return null
	return ImageTexture.create_from_image(img)

## THE DIEGETIC BINDER (DECISIONS § THE BINDER PORTRAIT, corrected): the
## object that sits ON the room painting at the scene's bottom-left and
## REPLACES the binder doorway button. Portrait when cached, the drawn
## mini-binder (same silhouette) otherwise; the company name overlaid on the
## label in the hand (omitted below LABEL_MIN_PX rather than rendered
## illegibly); slight lift/tilt on hover; press opens the binder; and when
## the company has attention items the red "!" sticker appears — the red
## system reaching the scene, fed by the SAME attention list as the tabs.
static func make_object(p_state: GameState, on_open: Callable,
		obj_size := Vector2(210, 250)) -> Control:
	var root := _BinderObject.new()
	root.state = p_state
	root.on_open = on_open
	root.set_deferred("size", obj_size)
	root.pivot_offset = obj_size * 0.5
	var tex := portrait_texture()
	if tex != null:
		var tr := TextureRect.new()
		tr.texture = tex
		tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		tr.set_deferred("size", obj_size)
		tr.mouse_filter = Control.MOUSE_FILTER_IGNORE
		root.add_child(tr)
	else:
		var art := _CoverArt.new()
		art.set_deferred("size", obj_size)
		art.mouse_filter = Control.MOUSE_FILTER_IGNORE
		root.add_child(art)
	# the label illustration (or the monogram) sits above the overlaid name —
	# DECISIONS § THE THREE BINDER ILLUSTRATIONS: the mark is a vignette,
	# generated textless; initials on a drawn chip stand in until it exists.
	var logo_h := obj_size.y * LABEL_RECT.size.y * 1.35
	var logo_pos := Vector2(obj_size.x * LABEL_RECT.position.x,
		obj_size.y * LABEL_RECT.position.y - logo_h - 2.0)
	if FileAccess.file_exists(PortraitClient.LOGO_PATH):
		var limg := Image.new()
		if limg.load(PortraitClient.LOGO_PATH) == OK:
			var ltr := TextureRect.new()
			ltr.texture = ImageTexture.create_from_image(limg)
			ltr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
			ltr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
			ltr.position = logo_pos
			ltr.set_deferred("size", Vector2(obj_size.x * LABEL_RECT.size.x, logo_h))
			ltr.mouse_filter = Control.MOUSE_FILTER_IGNORE
			root.add_child(ltr)
	elif p_state != null and String(p_state.company_name) != "":
		var initials := ""
		for wpart in String(p_state.company_name).split(" ", false):
			if initials.length() < 2 and wpart.length() > 0:
				initials += wpart.substr(0, 1).to_upper()
		var mono := Label.new()
		mono.text = initials
		mono.add_theme_font_override("font", load(DISPLAY) as Font)
		mono.add_theme_font_size_override("font_size", int(logo_h * 0.7))
		mono.add_theme_color_override("font_color", INK)
		mono.position = logo_pos
		mono.custom_minimum_size = Vector2(obj_size.x * LABEL_RECT.size.x, logo_h)
		mono.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		mono.mouse_filter = Control.MOUSE_FILTER_IGNORE
		root.add_child(mono)
	# the name, overlaid — the image is generated with a BLANK label
	var f: Font = load(HAND)
	var company := String(p_state.company_name) if p_state != null else ""
	if company == "":
		company = "the company"
	var sz := LABEL_FONT_SIZE
	while sz > LABEL_MIN_PX and f.get_string_size(company,
			HORIZONTAL_ALIGNMENT_LEFT, -1, sz).x > obj_size.x * LABEL_RECT.size.x - 10.0:
		sz -= 1
	if sz >= LABEL_MIN_PX:
		var lab := Label.new()
		lab.text = company
		lab.add_theme_font_override("font", f)
		lab.add_theme_font_size_override("font_size", sz)
		lab.add_theme_color_override("font_color", INK)
		lab.position = Vector2(obj_size.x * LABEL_RECT.position.x,
			obj_size.y * LABEL_RECT.position.y)
		lab.custom_minimum_size = Vector2(obj_size.x * LABEL_RECT.size.x,
			obj_size.y * LABEL_RECT.size.y)
		lab.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		lab.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		lab.mouse_filter = Control.MOUSE_FILTER_IGNORE
		root.add_child(lab)
	# the red "!" sticker — hidden until the attention feed says otherwise
	var sticker := _BangChip.new()
	sticker.font = load(DISPLAY)
	sticker.mouse_filter = Control.MOUSE_FILTER_IGNORE
	sticker.position = Vector2(obj_size.x - 40.0, 8.0)
	sticker.set_deferred("size", Vector2(30.0, 30.0))
	sticker.visible = false
	root.add_child(sticker)
	root.sticker = sticker
	# the press — the whole object is the doorway
	var press := Button.new()
	press.flat = true
	press.text = ""
	press.set_anchors_preset(Control.PRESET_FULL_RECT)
	for stn in ["normal", "hover", "pressed", "focus"]:
		press.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	press.pressed.connect(func() -> void:
		if on_open.is_valid():
			on_open.call())
	press.mouse_entered.connect(func() -> void: root.hover(true))
	press.mouse_exited.connect(func() -> void: root.hover(false))
	root.add_child(press)
	root.refresh_attention()
	return root

## The object's own body: the hover lift, and the 1s attention poll that keeps
## the sticker honest without asking the room screen to wire anything.
class _BinderObject:
	extends Control
	var state: GameState
	var on_open: Callable
	var sticker: Control = null
	var _poll := 0.0
	func hover(on: bool) -> void:
		var tw := create_tween()
		tw.set_parallel(true)
		tw.tween_property(self, "scale", Vector2(1.05, 1.05) if on else Vector2.ONE, 0.14) \
			.set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
		tw.tween_property(self, "rotation", -0.02 if on else 0.0, 0.14)
	func _process(dt: float) -> void:
		_poll += dt
		if _poll < 1.0:
			return
		_poll = 0.0
		refresh_attention()
	func refresh_attention() -> void:
		if sticker == null or state == null:
			return
		var worst := 0
		for it in SimEngine.attention_items(state):
			worst = maxi(worst, int((it as Dictionary).get("severity", 1)))
		sticker.visible = worst > 0
		sticker.set("pulse", worst >= 3)

# ─────────────────────────────── the tour ────────────────────────────────────

## THE FIRST-OPEN TOUR (DECISIONS #6): six steps — the four groups fanned with
## one-liners, the red demo, the handover. Click advances, Esc skips, once per
## install; the how-to screen can replay it by clearing the flag.
func tour_advance() -> void:
	_tour += 1
	if _tour > 5:
		_tour_finish()
		return
	_tour_apply()
	_refresh()

func _tour_apply() -> void:
	_tour_demo_red = _tour == 4
	if _tour >= 0 and _tour <= 3:
		_open_group = _tour
		var desks: Array = (GROUPS[_tour] as Dictionary).get("desks", [])
		_page = String(desks[0]) if desks.size() > 0 else _page
	elif _tour == 4:
		_open_group = 2
		_page = "threats"
	else:
		_open_group = 3
		_page = "this week"

func _tour_finish() -> void:
	_tour = -1
	_tour_demo_red = false
	var f := FileAccess.open(TOUR_FLAG, FileAccess.WRITE)
	if f != null:
		f.store_string("1")
		f.close()
	_open_group = 3
	_page = "this week"
	desk.clear()
	_refresh()

static func tour_seen() -> bool:
	return FileAccess.file_exists(TOUR_FLAG)

## The how-to screen's replay: clear the mark; the next binder open tours.
static func reset_tour() -> void:
	if FileAccess.file_exists(TOUR_FLAG):
		DirAccess.remove_absolute(ProjectSettings.globalize_path(TOUR_FLAG))

# ───────────────────────── what a desk may touch ─────────────────────────────
## The public half of this node: the drawing hand every desk file and the
## shared component kit draw through — unchanged, so every shipped desk lands.

func pane() -> Control:
	return _content

func font() -> Font:
	return _font

func display_font() -> Font:
	return _font_d

func label(text: String, pos: Vector2, sz: int = 30, col: Color = INK, w: float = 1100.0) -> Label:
	return _label(text, pos, sz, col, w)

func ink_btn(btn: Button) -> void:
	_ink_btn(btn)

func icon(name: String, pos: Vector2, side: float = 72.0) -> void:
	_icon(name, pos, side)

func spark(series_v: Array, pos: Vector2, size_v: Vector2, col: Color) -> void:
	_spark(series_v, pos, size_v, col)

func series(key: String) -> Array:
	return _series(key)

func fmt(n: int) -> String:
	return _fmt(n)

func wrap_h(text: String, sz: int, w: float) -> float:
	return _wrap_h(text, sz, w)

func refresh() -> void:
	_refresh()

## THE VESSEL: a jar with a level — product's tech debt, and any "how full is it"
## read a desk needs. Ink outline round the whole height or it is not a jar.
func debt_jar(fill: float, pos: Vector2, size_v: Vector2) -> void:
	var jar := _DebtJar.new()
	jar.fill = fill
	jar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	jar.position = pos
	jar.set_deferred("size", size_v)
	_content.add_child(jar)

## THE FACE: a wobbled coral clock with two ink hands — the leading mark on any
## deadline line (§2.10).
func clock(pos: Vector2, side: float = 30.0) -> void:
	var face := _Clock.new()
	face.mouse_filter = Control.MOUSE_FILTER_IGNORE
	face.position = pos
	face.set_deferred("size", Vector2(side, side))
	_content.add_child(face)

## THE WHEEL: slices at 0.75α under a 4px ink rim, names hung round the arc's
## middle. `slices` = [{pct, col, label}].
func pie(slices: Array, pos: Vector2, side: float) -> void:
	var p := _Pie.new()
	p.slices = slices
	p.font = _font
	p.mouse_filter = Control.MOUSE_FILTER_IGNORE
	p.position = pos
	p.set_deferred("size", Vector2(side, side))
	_content.add_child(p)

func _label(text: String, pos: Vector2, sz: int = 30, col: Color = INK, w: float = 1100.0) -> Label:
	var l := Label.new()
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.add_theme_font_override("font", _font)
	l.add_theme_font_size_override("font_size", sz)
	l.add_theme_color_override("font_color", col)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	l.text = text
	l.position = pos
	l.custom_minimum_size = Vector2(w, 0)
	_content.add_child(l)
	return l

func _icon(name: String, pos: Vector2, side: float = 72.0) -> void:
	var p := "res://assets/journal_icons/%s.png" % name
	if not ResourceLoader.exists(p):
		return
	var tr := TextureRect.new()
	tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	tr.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tr.texture = load(p)
	tr.position = pos
	tr.set_deferred("size", Vector2(side, side))
	_content.add_child(tr)

func _spark(series_v: Array, pos: Vector2, size_v: Vector2, col: Color) -> void:
	var sp := _Spark.new()
	sp.series = series_v
	sp.col = col
	sp.mouse_filter = Control.MOUSE_FILTER_IGNORE
	sp.position = pos
	sp.set_deferred("size", size_v)
	_content.add_child(sp)

func _series(key: String) -> Array:
	var out: Array = []
	for m in state.metric_history:
		out.append(float((m as Dictionary).get(key, 0)))
	return out

func _fmt(n: int) -> String:
	var s := str(absi(n))
	var out := ""
	while s.length() > 3:
		out = "," + s.substr(s.length() - 3) + out
		s = s.substr(0, s.length() - 3)
	return ("-" if n < 0 else "") + s + out

func _ink_btn(btn: Button) -> void:
	btn.flat = true
	btn.add_theme_font_override("font", _font)
	btn.add_theme_font_size_override("font_size", 40)
	btn.add_theme_color_override("font_color", INK)
	btn.add_theme_color_override("font_hover_color", PEN)
	for stn in ["normal", "hover", "pressed", "focus"]:
		btn.add_theme_stylebox_override(stn, StyleBoxEmpty.new())

## Wrapped text is MEASURED, never assumed one line.
func _wrap_h(text: String, sz: int, w: float) -> float:
	return _font.get_multiline_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, w, sz).y

# ─────────────────────────────── drawn pieces ───────────────────────────────

## THE BINDER BODY (mockups/00 A): kraft cover with the rotated sticker, the
## kraft2 ringbar with drawn concentric rings, the paper stack with its punched
## margin, and the sheet's left rule. The rail and pages are child controls.
class _FrameBoard:
	extends Control
	func _draw() -> void:
		var w := size.x
		var h := size.y
		var rng := RandomNumberGenerator.new()
		rng.seed = 7
		# the thrown shadow
		draw_rect(Rect2(10, 14, w, h), Color(0, 0, 0, 0.25))
		# the paper stack (everything right of the ringbar)
		draw_rect(Rect2(Binder.STACK_X, 0, w - Binder.STACK_X, h), Binder.CREAM)
		# the kraft cover
		draw_rect(Rect2(0, 0, Binder.COVER_W, h), Binder.KRAFT)
		for i in 24:
			var sx := float(i) * 9.0 - 20.0
			draw_line(Vector2(sx, 0), Vector2(sx + h * 0.12, h), Color(Binder.INK, 0.05), 3.0)
		# the ringbar
		draw_rect(Rect2(Binder.COVER_W, 0, Binder.RING_W, h), Binder.KRAFT2)
		for r in 3:
			var cy := h * (0.25 + 0.25 * float(r))
			var cx := Binder.COVER_W + Binder.RING_W * 0.5
			var ring := PackedVector2Array()
			for k in 25:
				var t := TAU * float(k) / 24.0
				ring.append(Vector2(cx + cos(t) * 17.0, cy + sin(t) * 17.0)
					+ Vector2(rng.randf_range(-0.6, 0.6), rng.randf_range(-0.6, 0.6)))
			draw_polyline(ring, Binder.INK, 3.4, true)
			var ring2 := PackedVector2Array()
			for k2 in 21:
				var t2 := TAU * float(k2) / 20.0
				ring2.append(Vector2(cx + cos(t2) * 11.0, cy + sin(t2) * 11.0))
			draw_polyline(ring2, Color(Binder.INK, 0.45), 2.6, true)
		# the wobbled outer edge round the whole object
		var pts := PackedVector2Array()
		var corners := [Vector2(3, 3), Vector2(w - 3, 3), Vector2(w - 3, h - 3), Vector2(3, h - 3)]
		for i2 in 4:
			var a: Vector2 = corners[i2]
			var b: Vector2 = corners[(i2 + 1) % 4]
			for k3 in 20:
				pts.append(a.lerp(b, float(k3) / 20.0)
					+ Vector2(rng.randf_range(-2, 2), rng.randf_range(-2, 2)))
		pts.append(pts[0])
		draw_polyline(pts, Binder.INK, 4.0, true)
		# the cover's rotated sticker
		draw_set_transform(Vector2(Binder.COVER_W * 0.5, 120.0), -PI / 2.0, Vector2.ONE)
		draw_rect(Rect2(-64, -14, 128, 28), Binder.PAPER2)
		draw_rect(Rect2(-64, -14, 128, 28), Binder.INK, false, 2.2)
		var f: Font = load(Binder.HAND)
		draw_string(f, Vector2(-56, 7), "the binder", HORIZONTAL_ALIGNMENT_LEFT, 116, 17,
			Binder.INK)
		draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)
		# the punched margin rule
		var dy := 12.0
		while dy < h - 12.0:
			draw_line(Vector2(Binder.STACK_X + 7.0, dy), Vector2(Binder.STACK_X + 7.0,
				minf(dy + 9.0, h - 12.0)), Color(Binder.INK, 0.25), 2.0)
			dy += 16.0
		# the sheet's left rule
		draw_line(Vector2(Binder.SHEET_RULE_X, 14.0), Vector2(Binder.SHEET_RULE_X, h - 14.0),
			Color(Binder.INK, 0.25), 2.4)

## A divider group on the rail: closed = kraft card with the stack shadow;
## open = paper with its header rule. open_t eases kraft→paper in ~0.3s. The
## colored index tab pokes LEFT of the box; a red group paints it ALERT.
class _Divider:
	extends Control
	var col := Binder.SAGE
	var open_t := 0.0
	var sev := 0
	var font: Font
	func _process(_dt: float) -> void:
		if sev >= 3:
			queue_redraw()   # the 12fps pulse below quantizes the clock itself
	func _draw() -> void:
		var w := size.x
		var h := size.y
		var rng := RandomNumberGenerator.new()
		rng.seed = 11 + int(position.y)
		# closed: the kraft stack shadow (two offset cards under this one)
		if open_t < 0.5:
			draw_rect(Rect2(2, h - 2.0, w - 4.0, 4.0), Binder.KRAFT2)
			draw_rect(Rect2(4, h + 1.0, w - 8.0, 4.0), Color(Binder.INK, 0.35))
		var body := Binder.KRAFT.lerp(Binder.CREAM, clampf(open_t, 0.0, 1.0))
		draw_rect(Rect2(0, 0, w, h), body)
		# the index tab, poking left — ALERT red climbs onto it when the group
		# carries attention (sev3 pulses at ~12fps, sev2 holds still)
		var tab_col := col
		if sev > 0:
			tab_col = Binder.ALERT
			if sev >= 3:
				var t := floorf(Time.get_ticks_msec() / 1000.0 * 12.0) / 12.0
				tab_col = Binder.ALERT if fmod(t, 0.33) > 0.13 else Color(Binder.ALERT, 0.45)
		draw_rect(Rect2(-16, 4, 15, h - 8.0), tab_col)
		draw_rect(Rect2(-16, 4, 15, h - 8.0), Binder.INK, false, 2.6)
		# the box's own wobbled edge
		var pts := PackedVector2Array()
		var corners := [Vector2(1, 1), Vector2(w - 1, 1), Vector2(w - 1, h - 1), Vector2(1, h - 1)]
		for i in 4:
			var a: Vector2 = corners[i]
			var b: Vector2 = corners[(i + 1) % 4]
			for k in 10:
				pts.append(a.lerp(b, float(k) / 10.0)
					+ Vector2(rng.randf_range(-1.0, 1.0), rng.randf_range(-1.0, 1.0)))
		pts.append(pts[0])
		draw_polyline(pts, Binder.INK, 2.6, true)
		# the open group's header rule, fading in with the paper
		if open_t > 0.6:
			draw_line(Vector2(8, 46), Vector2(w - 8, 46),
				Color(Binder.INK, 0.25 * open_t), 2.0)

## One page tab in the fan: a quiet row normally; the active page in a paper2
## box; a desk with attention RED-FILLED with the white bang; a momentary desk
## GOLD in the display hand with the deadline clock chip, leaning slightly.
class _PageTab:
	extends Button
	var text_v := ""
	var active := false
	var sev := 0
	var gold := false
	var clock_text := ""
	var font: Font
	var font_d: Font
	func _init() -> void:
		flat = true
		text = ""
		for stn in ["normal", "hover", "pressed", "focus"]:
			add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	func _process(_dt: float) -> void:
		if sev >= 3:
			queue_redraw()
	func _draw() -> void:
		var w := size.x
		var h := size.y
		if gold:
			draw_set_transform(Vector2(w * 0.5, h * 0.5), -0.03, Vector2.ONE)
			draw_rect(Rect2(-w * 0.5 + 1, -h * 0.5 + 1, w - 2, h - 2), Binder.YELL)
			draw_rect(Rect2(-w * 0.5 + 1, -h * 0.5 + 1, w - 2, h - 2), Binder.INK, false, 2.4)
			if font_d != null:
				draw_string(font_d, Vector2(-w * 0.5 + 8, 7), text_v,
					HORIZONTAL_ALIGNMENT_LEFT, w - 60.0, 16, Binder.INK)
			draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)
			if clock_text != "" and font != null:
				draw_rect(Rect2(w - 52, 6, 48, 22), Binder.ALERT)
				draw_rect(Rect2(w - 52, 6, 48, 22), Binder.INK, false, 2.0)
				draw_string(font, Vector2(w - 47, 23), clock_text,
					HORIZONTAL_ALIGNMENT_LEFT, 44, 14, Color.WHITE)
			return
		var red := sev > 0
		var fill := Binder.ALERT
		if red and sev >= 3:
			var t := floorf(Time.get_ticks_msec() / 1000.0 * 12.0) / 12.0
			fill = Binder.ALERT if fmod(t, 0.33) > 0.13 else Color(Binder.ALERT, 0.5)
		if red:
			draw_rect(Rect2(1, 1, w - 2, h - 2), fill)
			draw_rect(Rect2(1, 1, w - 2, h - 2), Binder.INK, false, 2.4)
		elif active:
			draw_rect(Rect2(2.0, 2.0, w - 2.0, h - 2.0), Color(0, 0, 0, 0.18))
			draw_rect(Rect2(0, 0, w - 2.0, h - 2.0), Binder.PAPER2)
			draw_rect(Rect2(0, 0, w - 2.0, h - 2.0), Binder.INK, false, 2.4)
		if font != null:
			var col := Color.WHITE if red else Binder.INK
			draw_string(font, Vector2(9, h - 11), text_v, HORIZONTAL_ALIGNMENT_LEFT,
				w - (44.0 if red else 18.0), 19, col)
			if red:
				draw_string(font, Vector2(w - 24, h - 10), "!", HORIZONTAL_ALIGNMENT_LEFT,
					20, 22, Color.WHITE)

## The red bang chip a CLOSED divider wears when a page inside it needs the
## founder: a drawn alert circle with the white bang (mockup 03's .abang).
class _BangChip:
	extends Control
	var pulse := false
	var font: Font
	func _process(_dt: float) -> void:
		if pulse:
			queue_redraw()
	func _draw() -> void:
		var c := size * 0.5
		var col := Binder.ALERT
		if pulse:
			var t := floorf(Time.get_ticks_msec() / 1000.0 * 12.0) / 12.0
			col = Binder.ALERT if fmod(t, 0.33) > 0.13 else Color(Binder.ALERT, 0.45)
		var rng := RandomNumberGenerator.new()
		rng.seed = 13
		var pts := PackedVector2Array()
		for i in 19:
			var t2 := TAU * float(i) / 18.0
			pts.append(c + Vector2(cos(t2), sin(t2)) * (size.x * 0.5 - 1.0
				+ rng.randf_range(-0.6, 0.6)))
		draw_colored_polygon(pts, col)
		pts.append(pts[0])
		draw_polyline(pts, Binder.INK, 2.2, true)
		if font != null:
			draw_string(font, Vector2(c.x - 4.0, c.y + 7.0), "!",
				HORIZONTAL_ALIGNMENT_LEFT, 12, 17, Color.WHITE)

## THE DRAWN COVER — the portrait's instant placeholder and permanent
## fallback: a chunky kraft binder, four index tabs in the group colors,
## papers poking out untidily, and the taped label the name lands on.
class _CoverArt:
	extends Control
	func _draw() -> void:
		var w := size.x
		var h := size.y
		var rng := RandomNumberGenerator.new()
		rng.seed = 29
		# untidy papers poking out of the top
		for p in 4:
			var px := w * 0.18 + float(p) * w * 0.16 + rng.randf_range(-8.0, 8.0)
			var tilt := rng.randf_range(-0.12, 0.12)
			draw_set_transform(Vector2(px, h * 0.06), tilt, Vector2.ONE)
			draw_rect(Rect2(0, -18, w * 0.16, 36), Binder.PAPER2)
			draw_rect(Rect2(0, -18, w * 0.16, 36), Binder.INK, false, 2.2)
			draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)
		# the body
		var body := Rect2(w * 0.06, h * 0.07, w * 0.82, h * 0.86)
		draw_rect(Rect2(body.position + Vector2(9, 12), body.size), Color(0, 0, 0, 0.22))
		draw_rect(body, Binder.KRAFT)
		for s in 30:
			var sx := body.position.x + float(s) * 14.0 - 30.0
			draw_line(Vector2(sx, body.position.y), Vector2(sx + body.size.y * 0.1,
				body.end.y), Color(Binder.INK, 0.05), 3.0)
		# the spine band + rings
		draw_rect(Rect2(body.position.x, body.position.y, w * 0.11, body.size.y), Binder.KRAFT2)
		for r in 3:
			var cy := body.position.y + body.size.y * (0.22 + 0.28 * float(r))
			var cx := body.position.x + w * 0.055
			var ring := PackedVector2Array()
			for k in 23:
				var t := TAU * float(k) / 22.0
				ring.append(Vector2(cx + cos(t) * 15.0, cy + sin(t) * 15.0)
					+ Vector2(rng.randf_range(-0.7, 0.7), rng.randf_range(-0.7, 0.7)))
			draw_polyline(ring, Binder.INK, 3.2, true)
		# four thick index tabs, right edge, the group colors
		var cols := [Binder.SAGE, Binder.PEN, Binder.BLUE, Binder.YELL]
		for i in 4:
			var ty := body.position.y + body.size.y * (0.14 + 0.2 * float(i))
			draw_rect(Rect2(body.end.x - 4, ty, w * 0.09, h * 0.09), cols[i])
			draw_rect(Rect2(body.end.x - 4, ty, w * 0.09, h * 0.09), Binder.INK, false, 2.6)
		# the wobbled edge round the body
		var pts := PackedVector2Array()
		var corners := [body.position, Vector2(body.end.x, body.position.y), body.end,
			Vector2(body.position.x, body.end.y)]
		for i2 in 4:
			var a: Vector2 = corners[i2]
			var b: Vector2 = corners[(i2 + 1) % 4]
			for k2 in 16:
				pts.append(a.lerp(b, float(k2) / 16.0)
					+ Vector2(rng.randf_range(-2.0, 2.0), rng.randf_range(-2.0, 2.0)))
		pts.append(pts[0])
		draw_polyline(pts, Binder.INK, 4.0, true)
		# the taped label — BLANK: the caller overlays the name (LABEL_RECT)
		var lab := Rect2(w * Binder.LABEL_RECT.position.x, h * Binder.LABEL_RECT.position.y,
			w * Binder.LABEL_RECT.size.x, h * Binder.LABEL_RECT.size.y)
		draw_rect(Rect2(lab.position + Vector2(3, 4), lab.size), Color(0, 0, 0, 0.18))
		draw_rect(lab, Binder.PAPER2)
		draw_rect(lab, Binder.INK, false, 2.6)
		# the tape corners
		for tc in [Vector2(lab.position.x - 12, lab.position.y - 8),
				Vector2(lab.end.x - 24, lab.end.y - 10)]:
			draw_set_transform(tc, 0.6, Vector2.ONE)
			draw_rect(Rect2(0, 0, 44, 18), Color(1, 1, 1, 0.45))
			draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)

class _Spark:
	extends Control
	var series: Array = []
	var col := Color("6E8CA0")
	func _draw() -> void:
		draw_rect(Rect2(Vector2.ZERO, size), Color(0, 0, 0, 0.03))
		if series.size() < 2:
			var f: Font = load(Binder.HAND)
			draw_string(f, Vector2(12, size.y * 0.55), "not enough weeks on record yet",
				HORIZONTAL_ALIGNMENT_LEFT, -1, 24, Color(Binder.INK, 0.4))
			return
		var lo := 1e18
		var hi := -1e18
		for v in series:
			lo = minf(lo, float(v))
			hi = maxf(hi, float(v))
		if hi - lo < 1.0:
			hi = lo + 1.0
		var pts := PackedVector2Array()
		var rng := RandomNumberGenerator.new()
		rng.seed = 13
		for i in series.size():
			var x := 8.0 + (size.x - 16.0) * float(i) / float(series.size() - 1)
			var y := size.y - 10.0 - (size.y - 24.0) * (float(series[i]) - lo) / (hi - lo)
			pts.append(Vector2(x, y + rng.randf_range(-1.0, 1.0)))
		draw_polyline(pts, col, 4.0, true)
		draw_circle(pts[pts.size() - 1], 6.0, Binder.PEN)
		var f2: Font = load(Binder.HAND)
		draw_string(f2, Vector2(8, 22), _fmt_s(hi), HORIZONTAL_ALIGNMENT_LEFT, -1, 20, Color(Binder.INK, 0.45))
		draw_string(f2, Vector2(8, size.y - 4), _fmt_s(lo), HORIZONTAL_ALIGNMENT_LEFT, -1, 20, Color(Binder.INK, 0.45))
	func _fmt_s(v: float) -> String:
		if absf(v) >= 1_000_000.0:
			return "%.1fM" % (v / 1_000_000.0)
		if absf(v) >= 1_000.0:
			return "%.0fk" % (v / 1_000.0)
		return "%.0f" % v

## THE CLOCK (10-interface-language §2.10): a wobbled coral face and two ink
## hands — transcribed from the twin in DrawnChart.Clock so the two engines
## draw the same face.
class _Clock:
	extends Control
	func _draw() -> void:
		var side := minf(size.x, size.y)
		var c := side * 0.5
		var r := c - 3.0
		var rng := RandomNumberGenerator.new()
		rng.seed = 11
		var ring := PackedVector2Array()
		for i in 21:
			var t := TAU * float(i) / 20.0
			var rr := r + rng.randf_range(-0.7, 0.7)
			ring.append(Vector2(c + cos(t) * rr, c + sin(t) * rr))
		draw_polyline(ring, Binder.PEN, side * 0.085, true)
		var hw := maxf(side * 0.07, 1.6)
		draw_line(Vector2(c, c), Vector2(c, c - r * 0.72), Binder.INK, hw, true)
		draw_line(Vector2(c, c), Vector2(c + r * 0.42, c + r * 0.42), Binder.INK, hw, true)

class _DebtJar:
	extends Control
	var fill := 0.3
	func _draw() -> void:
		var w := size.x
		var h := size.y
		draw_rect(Rect2(6, 10, w - 12, h - 14), Color(0, 0, 0, 0.04))
		var lv := clampf(fill, 0.0, 1.0)
		draw_rect(Rect2(8, 10 + (h - 16) * (1.0 - lv), w - 16, (h - 16) * lv),
			Color(Binder.PEN, 0.55))
		draw_rect(Rect2(6, 10, w - 12, h - 14), Binder.INK, false, 4.0)
		draw_line(Vector2(2, 10), Vector2(w - 2, 10), Binder.INK, 5.0)

class _Pie:
	extends Control
	var slices: Array = []
	var font: Font
	func _draw() -> void:
		var c := size * 0.5
		var r := minf(size.x, size.y) * 0.38
		var a0 := -PI / 2.0
		for s in slices:
			var d: Dictionary = s
			var frac := clampf(float(d.get("pct", 0.0)) / 100.0, 0.0, 1.0)
			if frac <= 0.001:
				continue
			var a1 := a0 + TAU * frac
			var pts := PackedVector2Array([c])
			var steps := maxi(int(frac * 48.0), 2)
			for i in steps + 1:
				var t := a0 + (a1 - a0) * float(i) / float(steps)
				pts.append(c + Vector2(cos(t), sin(t)) * r)
			draw_colored_polygon(pts, Color(d.get("col", Binder.SAGE), 0.75))
			a0 = a1
		draw_arc(c, r, 0, TAU, 64, Binder.INK, 4.0, true)
		# LABELS HUNG ROUND THE WHEEL, never over it — the word hangs OUTWARD:
		# it starts at the point when the slice faces right, ends at it when the
		# slice faces left, and centres only near top and bottom.
		a0 = -PI / 2.0
		for s2 in slices:
			var d2: Dictionary = s2
			var frac2 := clampf(float(d2.get("pct", 0.0)) / 100.0, 0.0, 1.0)
			if frac2 <= 0.01:
				continue
			var mid := a0 + TAU * frac2 * 0.5
			var dir := Vector2(cos(mid), sin(mid))
			var p := c + dir * (r + 30.0)
			var txt := String(d2.get("label", ""))
			var tw := font.get_string_size(txt, HORIZONTAL_ALIGNMENT_LEFT, -1, 24).x
			var anchor := (1.0 - dir.x) * 0.5
			var lx: float = clampf(p.x - tw * anchor, -position.x,
				Binder.PANE_W - position.x - tw)
			draw_string(font, Vector2(lx, p.y + 8.0), txt,
				HORIZONTAL_ALIGNMENT_LEFT, -1, 24, Binder.INK)
			a0 += TAU * frac2
