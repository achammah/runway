class_name Binder
extends Control
## THE OPERATIONS BINDER — the founder's dashboard, in the game's own hand
## (docs/DND_STARTUP_PLAN.md, UI item 1-7). Never a SaaS panel: a clipboard
## sheet over the dimmed room, seven pen-labelled tabs, doodle icons, charts
## drawn as wobbly polylines.
##
## FOG OF WAR: precision follows state.analytics_level (0-3). At 0 the customer
## page says "traffic seems decent"; invest in analytics (a writable move) and
## the pages sharpen. The dashboard you EARN is a mechanic, not a view.
##
## Usage:  var b := Binder.new(); b.setup(state); add_child(b)
##         b.closed.connect(...)  — TAB/B/Esc or the close corner dismisses it.

signal closed

const CREAM := Color("F2EAD3")
const INK := Color("1E1E1E")
const PEN := Color("E86A5C")
const SAGE := Color("8FA582")
const YELL := Color("F4B942")
const BLUE := Color("6E8CA0")
const HAND := "res://assets/fonts/PatrickHand-Regular.ttf"

const TABS := ["vitals", "the ledger", "pricing", "customers", "product", "crew", "cap table", "the street", "threats"]

var state: GameState
var _font: Font
var _tab := 0
var _sheet: Control
var _content: Control

func setup(p_state: GameState) -> void:
	state = p_state

func _ready() -> void:
	_font = load(HAND)
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	var dim := ColorRect.new()
	dim.color = Color(0.05, 0.05, 0.06, 0.55)
	dim.set_anchors_preset(Control.PRESET_FULL_RECT)
	dim.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(dim)

	_sheet = _Clipboard.new()
	_sheet.position = Vector2(148, 52)
	_sheet.set_deferred("size", Vector2(1240, 920))
	_sheet.mouse_filter = Control.MOUSE_FILTER_STOP
	add_child(_sheet)

	_content = Control.new()
	_content.position = Vector2(40, 118)
	_content.set_deferred("size", Vector2(1160, 760))
	_content.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_sheet.add_child(_content)

	# tabs: pen labels across the top; the active one gets the ring
	for i in TABS.size():
		var b := Button.new()
		b.flat = true
		b.text = TABS[i]
		b.add_theme_font_override("font", _font)
		b.add_theme_font_size_override("font_size", 23)
		b.add_theme_color_override("font_color", INK)
		b.add_theme_color_override("font_hover_color", PEN)
		for stn in ["normal", "hover", "pressed", "focus"]:
			b.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
		b.position = Vector2(24 + i * 133, 54)
		b.set_deferred("size", Vector2(130, 44))
		var idx := i
		b.pressed.connect(func() -> void:
			_tab = idx
			_refresh())
		_sheet.add_child(b)

	var close := Button.new()
	close.flat = true
	close.text = "×"
	close.add_theme_font_override("font", _font)
	close.add_theme_font_size_override("font_size", 46)
	close.add_theme_color_override("font_color", PEN)
	for stn2 in ["normal", "hover", "pressed", "focus"]:
		close.add_theme_stylebox_override(stn2, StyleBoxEmpty.new())
	close.position = Vector2(1180, 8)
	close.set_deferred("size", Vector2(52, 52))
	close.pressed.connect(func() -> void: _dismiss())
	_sheet.add_child(close)

	gui_input.connect(func(ev: InputEvent) -> void:
		if ev is InputEventMouseButton and ev.pressed:
			_dismiss())
	_refresh()

func _input(ev: InputEvent) -> void:
	if ev is InputEventKey and ev.pressed and ev.keycode in [KEY_ESCAPE, KEY_TAB, KEY_B]:
		accept_event()
		_dismiss()

func _dismiss() -> void:
	closed.emit()
	queue_free()

# ─────────────────────────────── composition ────────────────────────────────
func _refresh() -> void:
	for c in _content.get_children():
		c.queue_free()
	(_sheet as _Clipboard).active_tab = _tab
	_sheet.queue_redraw()
	match _tab:
		0: _tab_vitals()
		1: _tab_ledger()
		2: _tab_pricing()
		3: _tab_customers()
		4: _tab_product()
		5: _tab_crew()
		6: _tab_cap()
		7: _tab_street()
		8: _tab_threats()

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

func _spark(series: Array, pos: Vector2, size_v: Vector2, col: Color) -> void:
	var sp := _Spark.new()
	sp.series = series
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

# ── tab 0: vitals ────────────────────────────────────────────────────────────
func _tab_vitals() -> void:
	_icon("cash", Vector2(10, 6))
	_label("$%s in the bank" % _fmt(state.cash), Vector2(100, 10), 46)
	_label(SimEngine.health_band(state), Vector2(100, 66), 30,
		PEN if SimEngine.runway_weeks(state) <= 10 else SAGE)
	_label("cash, drawn weekly:", Vector2(10, 140), 24, Color(INK, 0.6))
	_spark(_series("cash"), Vector2(10, 172), Vector2(1120, 190), BLUE)
	var last: Dictionary = state.metric_history[-1] if state.metric_history.size() > 0 else {}
	_label("last week: $%s in · $%s out" % [_fmt(int(last.get("revenue", 0))), _fmt(int(last.get("burn", 0)))],
		Vector2(10, 386), 30)
	var payroll := 0
	for e in state.employees:
		payroll += int(e.get("salary", 0))
	_label("burn: rent $%s · payroll $%s · marketing $%s%s" % [
		_fmt(int(GameState.ERA_RENT.get(state.era, 150))), _fmt(payroll),
		_fmt(state.marketing_budget),
		("  ·  LOAN OWED $%s (18%%/wk)" % _fmt(state.loan_principal)) if state.loan_principal > 0 else ""],
		Vector2(10, 432), 27, Color(INK, 0.8))
	_label("valuation, if anyone asked: $%s" % _fmt(SimEngine.valuation(state)), Vector2(10, 486), 30)
	_label("price ×%.2f  ·  the market is %s" % [state.price_mult,
		"warm" if state.market_trend > 1.05 else ("cold" if state.market_trend < 0.95 else "even")],
		Vector2(10, 532), 27, Color(INK, 0.8))

# ── tab 1: THE LEDGER — the levers, the math, the truth about the money ─────
const LEVERS := [
	["marketing", "reach — more people hear of you; saturates past ~$2k"],
	["sales", "closing — every $600/wk closes like one more part-time seller"],
	["care", "retention — up to 30% less churn as care approaches $3k"],
	["rnd", "product — ships ~+1 quality per $1,200/wk and pays down debt"],
]
const LEVER_STEPS := [0, 250, 500, 1000, 2000, 4000, 8000]

func _tab_ledger() -> void:
	_label("the ledger — where this week's money goes", Vector2(10, 6), 38)
	var y := 78.0
	for lv in LEVERS:
		var cat := String(lv[0])
		var cur := int(state.budgets.get(cat, 0))
		_label(cat.to_upper(), Vector2(10, y), 30)
		_label(String(lv[1]), Vector2(10, y + 38), 23, Color(INK, 0.6))
		_label("$%s/wk" % _fmt(cur), Vector2(520, y + 6), 32, PEN)
		# WHAT THIS MONEY IS DOING RIGHT NOW, from the engine's own formulas —
		# the mechanics visible at the point of the decision
		_label(_lever_effect(cat, cur), Vector2(688, y + 12), 24, Color(INK, 0.75))
		var minus := Button.new()
		minus.text = "−"
		minus.position = Vector2(1000, y)
		minus.size = Vector2(52, 46)
		_ink_btn(minus)
		minus.pressed.connect(func() -> void:
			state.budgets[cat] = _lever_step(cur, -1)
			_refresh())
		_content.add_child(minus)
		var plus := Button.new()
		plus.text = "+"
		plus.position = Vector2(1064, y)
		plus.size = Vector2(52, 46)
		_ink_btn(plus)
		plus.pressed.connect(func() -> void:
			state.budgets[cat] = _lever_step(cur, 1)
			_refresh())
		_content.add_child(plus)
		y += 92.0
	# the math, honestly
	var ue: Dictionary = state.get_meta("unit_econ", {})
	var lever_sum := 0
	for k in state.budgets:
		lever_sum += int(state.budgets[k])
	var rw := SimEngine.runway_weeks(state)
	y += 6.0
	_label("the math", Vector2(10, y), 30, BLUE)
	var arpu := float(ue.get("arpu", 0.0))
	var cacv := int(ue.get("cac", 0))
	var ltvv := int(ue.get("ltv", 0))
	var pb := int(ue.get("payback_wk", 0))
	_label("a customer pays ≈ $%.0f/wk · costs $%s to win (CAC) · is worth $%s over their stay (LTV) · pays back in %s"
		% [arpu, (_fmt(cacv) if cacv > 0 else "?"), (_fmt(ltvv) if ltvv > 0 else "?"),
		("%d wks" % pb) if pb > 0 else "—"], Vector2(10, y + 40), 25, Color(INK, 0.75), 1100.0)
	_label("levers total $%s/wk · runway %s" % [_fmt(lever_sum),
		("%d weeks" % rw) if rw < 999 else "gaining money"], Vector2(10, y + 108), 28)
	if rw <= 4 and rw < 999:
		_label("⚠ this spend kills the company in %d weeks — cut it or earn it" % rw,
			Vector2(10, y + 148.0), 28, PEN)
	var ry := y + 148.0
	if rw <= 4 and rw < 999:
		ry += 40.0
	if state.cash < 0:
		_label("THE RED: %d of 3 weeks below zero. At three, it's over." % state.weeks_in_red,
			Vector2(10, ry), 28, PEN)
		ry += 44.0
	_label("the rules of this world: reach saturates · only capacity closes · churn is a leaky bucket · debt slows everything · three weeks below zero ends it",
		Vector2(10, ry + 8.0), 22, Color(INK, 0.5), 1100.0)

## the engine's live math for one lever, in one plain phrase
func _lever_effect(cat: String, v: int) -> String:
	var th := state.theta
	match cat:
		"marketing":
			var mult := 1.0 + 1.4 * (1.0 - exp(-float(v) / float(th.get("cac_sat", 900.0))))
			return "reach ×%.2f" % mult if v > 0 else "no reach bought"
		"sales":
			return "+%.1f closers of capacity" % (float(v) / 600.0) if v > 0 else "founder sells alone"
		"care":
			var cut := 30.0 * (1.0 - exp(-float(v) / 1500.0))
			return "churn −%d%%" % int(round(cut)) if v > 0 else "nobody picks up"
		"rnd":
			return "+%.1f product/wk, debt melts" % (float(v) / 1200.0) if v > 0 else "no extra shipping"
	return ""

func _lever_step(cur: int, dir: int) -> int:
	var idx := 0
	for i in LEVER_STEPS.size():
		if LEVER_STEPS[i] <= cur:
			idx = i
	idx = clampi(idx + dir, 0, LEVER_STEPS.size() - 1)
	return mini(LEVER_STEPS[idx], SimEngine.era_spend_cap(state.era))

func _ink_btn(btn: Button) -> void:
	btn.flat = true
	btn.add_theme_font_override("font", _font)
	btn.add_theme_font_size_override("font_size", 40)
	btn.add_theme_color_override("font_color", INK)
	btn.add_theme_color_override("font_hover_color", PEN)
	for stn in ["normal", "hover", "pressed", "focus"]:
		btn.add_theme_stylebox_override(stn, StyleBoxEmpty.new())

# ── tab 2: PRICING — what we sell, at what price, and what the curve says ───
func _tab_pricing() -> void:
	_label("pricing — what %s sells" % state.company_name, Vector2(10, 6), 36)
	if state.offers.is_empty():
		_label("the world hasn't defined your offers yet — they arrive with the bible.",
			Vector2(10, 90), 28, Color(INK, 0.6))
		return
	var y := 84.0
	for oi in state.offers.size():
		var o: Dictionary = state.offers[oi]
		var price := float(o.get("price", 0.0))
		var fair := float(o.get("fair_price", 1.0))
		_label("%s  ·  %s" % [String(o.get("name", "?")).to_upper(), String(o.get("unit", ""))],
			Vector2(10, y), 30)
		_label("the street charges ≈ $%s" % _fmt(int(fair)), Vector2(10, y + 38), 23, Color(INK, 0.55))
		if price <= 0.0:
			_label("NOT ON SALE — set a price or it earns nothing", Vector2(430, y + 6), 27, PEN)
		else:
			var dem := SimEngine.offer_demand(o, price)
			var verdict := "about fair" if dem > 0.85 and dem < 1.15 else \
					("a deal — demand ×%.1f" % dem if dem >= 1.15 else \
					("pricey — %d%% of fair demand" % int(dem * 100.0) if dem > 0.25 else "absurd — ~nobody buys"))
			_label("$%s   ·   %s" % [_fmt(int(price)), verdict], Vector2(430, y + 6), 28,
				INK if dem > 0.25 else PEN)
		var minus := Button.new()
		minus.text = "−"
		minus.position = Vector2(1000, y)
		minus.size = Vector2(52, 46)
		_ink_btn(minus)
		var idx := oi
		minus.pressed.connect(func() -> void:
			_price_step(idx, -1)
			_refresh())
		_content.add_child(minus)
		var plus := Button.new()
		plus.text = "+"
		plus.position = Vector2(1064, y)
		plus.size = Vector2(52, 46)
		_ink_btn(plus)
		plus.pressed.connect(func() -> void:
			_price_step(idx, 1)
			_refresh())
		_content.add_child(plus)
		y += 104.0
	var arpu := SimEngine.offers_arpu(state)
	if arpu >= 0.0:
		_label("all offers together: ≈ $%.1f per customer per week  →  ≈ $%s/wk at %d customers" % [
			arpu, _fmt(int(arpu * float(state.traction))), state.traction],
			Vector2(10, y + 10), 27, BLUE)
	_label("the curve: price at the street's level and demand is fair · discount and demand grows · overprice and it dies fast",
		Vector2(10, y + 56), 22, Color(INK, 0.5), 1100.0)

## price steps: sensible ladder around the fair price (0 = off sale)
func _price_step(oi: int, dir: int) -> void:
	var o: Dictionary = state.offers[oi]
	var fair := float(o.get("fair_price", 10.0))
	var steps: Array = [0.0]
	for m in [0.4, 0.55, 0.7, 0.85, 1.0, 1.15, 1.35, 1.6, 2.0, 2.6, 3.5, 5.0]:
		steps.append(maxf(roundf(fair * m), 1.0))
	var cur := float(o.get("price", 0.0))
	var idx := 0
	for i in steps.size():
		if float(steps[i]) <= cur:
			idx = i
	idx = clampi(idx + dir, 0, steps.size() - 1)
	o["price"] = float(steps[idx])

# ── tab 3: customers (fog of war) ────────────────────────────────────────────
func _tab_customers() -> void:
	_icon("customers", Vector2(10, 6))
	if state.analytics_level <= 0:
		_label("%d customers, give or take." % state.traction, Vector2(100, 10), 46)
		_label("Traffic seems… decent? Someone signed up on Tuesday. The numbers live in a notebook you lost.",
			Vector2(10, 110), 30, Color(INK, 0.7))
		_label("(invest in analytics to see the funnel)", Vector2(10, 210), 26, PEN)
		return
	_label("%d customers" % state.traction, Vector2(100, 10), 46)
	var last: Dictionary = state.metric_history[-1] if state.metric_history.size() > 0 else {}
	_label("customers, weekly:", Vector2(10, 100), 24, Color(INK, 0.6))
	_spark(_series("customers"), Vector2(10, 132), Vector2(1120, 200), SAGE)
	var th := state.theta
	# WORKING ASSUMPTIONS (owner: nobody knows their TAM on day one): the
	# binder shows what the founder BELIEVES; operating and analytics refine it
	var believed_tam := float(state.beliefs.get("tam", th.get("tam", 100000.0)))
	var pen_pct := float(state.traction) / maxf(believed_tam, 1.0) * 100.0
	_label("market, as you believe it: ~%s buyers (%.1f%% reached) · a customer stays ≈ %d wks" % [
		_fmt(int(believed_tam)), pen_pct,
		int(state.beliefs.get("lifetime_wk", th.get("lifetime_wk", 40)))],
		Vector2(10, 356), 27)
	_label("working assumptions — they sharpen as you learn", Vector2(10, 392), 22, Color(INK, 0.5))
	if state.analytics_level >= 2:
		var mk := float(state.marketing_budget)
		var cac := "∞" if mk <= 0.0 else "$%d" % int(mk / maxf(1.0, mk / 900.0))
		_label("price ×%.2f · marketing $%s/wk · CAC roughly %s" % [
			state.price_mult, _fmt(state.marketing_budget), cac], Vector2(10, 404), 28)
		_label("lifetime ≈ %d wks at v0.%d quality" % [
			int(float(th.get("lifetime_wk", 40.0)) * (0.4 + float(state.product) / 100.0 * 1.2)),
			state.product], Vector2(10, 448), 28)
	if state.analytics_level >= 3:
		_label("the funnel is fully lit: organic + word-of-mouth + paid, all measured. You are the analytics now.",
			Vector2(10, 500), 26, SAGE)

# ── tab 2: product ───────────────────────────────────────────────────────────
func _tab_product() -> void:
	_icon("product", Vector2(10, 6))
	_label("v0.%d" % state.product, Vector2(100, 10), 46)
	_label("tech debt:", Vector2(10, 110), 28)
	var jar := _DebtJar.new()
	jar.fill = state.tech_debt / 100.0
	jar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	jar.position = Vector2(160, 92)
	jar.set_deferred("size", Vector2(90, 110))
	_content.add_child(jar)
	var risk := maxf((state.tech_debt - 40.0) / 250.0, 0.0) * 100.0
	_label("outage odds ≈ %d%% weekly" % int(risk), Vector2(290, 120), 28,
		PEN if risk > 10.0 else Color(INK, 0.7))
	_label("debt, weekly:", Vector2(10, 236), 24, Color(INK, 0.6))
	_spark(_series("debt"), Vector2(10, 268), Vector2(1120, 170), PEN)
	_label("hype:", Vector2(10, 470), 28)
	_spark(_series("hype"), Vector2(120, 452), Vector2(1010, 130), YELL)

# ── tab 3: crew ──────────────────────────────────────────────────────────────
func _tab_crew() -> void:
	_icon("you", Vector2(10, 6))
	var who := state.founder_name if state.founder_name != "" else "the founder"
	_label("%s — lvl %d · XP %d/%d spent · exhaustion %d/6" % [who,
		state.level, state.xp_spent, state.xp, state.exhaustion], Vector2(100, 20), 32)
	var stats := PackedStringArray()
	for st_n in ["build", "sell", "raise", "recruit", "grit"]:
		stats.append("%s %d" % [st_n, int(state.competences.get(st_n, 3))])
	_label("  ·  ".join(stats), Vector2(100, 64), 27, Color(INK, 0.8))
	var y := 130.0
	for cf in state.cofounders:
		_icon("cofd_tech", Vector2(10, y))
		var cf_name := str(cf.get("name", "")).strip_edges()
		var cf_role := str(cf.get("role", "?"))   # str(): a role can arrive as an int
		_label("%s%s cofounder · %.0f%% equity · loyalty %d" % [
			(cf_name + " — ") if cf_name != "" else "", cf_role,
			float(cf.get("equity_diluted", cf.get("equity", 0))), int(cf.get("loyalty", 70))],
			Vector2(100, y + 16), 28)
		y += 84.0
	for e in state.employees:
		_icon("employee", Vector2(10, y))
		_label("%s — %s · $%s/wk · burnout %d" % [String(e.get("name", "?")),
			String(e.get("role", "?")), _fmt(int(e.get("salary", 0))), int(e.get("burnout", 0))],
			Vector2(100, y + 16), 28)
		y += 84.0
	for h in state.pipeline:
		_icon("employee", Vector2(10, y))
		_label("%s — %s · ONBOARDING (paid, not yet productive)" % [
			String(h.get("name", "?")), String(h.get("role", "?"))], Vector2(100, y + 16), 28, Color(INK, 0.55))
		y += 84.0
	_label("morale:", Vector2(10, y + 10), 28)
	_spark(_series("morale"), Vector2(120, y - 8), Vector2(1000, 120), SAGE)

# ── tab 4: cap table ─────────────────────────────────────────────────────────
func _tab_cap() -> void:
	var pie := _Pie.new()
	var founder := state.founder_pct
	var cof := 0.0
	for cf in state.cofounders:
		cof += float(cf.get("equity_diluted", cf.get("equity", 0)))
	pie.slices = [
		{"pct": founder, "col": PEN, "label": "you %.0f%%" % founder},
		{"pct": cof, "col": BLUE, "label": "cofounders %.0f%%" % cof},
		{"pct": maxf(100.0 - founder - cof, 0.0), "col": SAGE,
		 "label": "investors %.0f%%" % maxf(100.0 - founder - cof, 0.0)},
	]
	pie.font = _font
	pie.mouse_filter = Control.MOUSE_FILTER_IGNORE
	pie.position = Vector2(40, 30)
	pie.set_deferred("size", Vector2(430, 430))
	_content.add_child(pie)
	var y := 60.0
	_label("rounds:", Vector2(540, 30), 32)
	if state.rounds_raised.is_empty():
		_label("none yet. every point of the company is still on this table.",
			Vector2(540, y + 20), 27, Color(INK, 0.7), 560.0)
	for r in state.rounds_raised:
		_label("· %s — closed" % String(r), Vector2(540, y + 20), 28, INK, 560.0)
		y += 44.0
	_label("valuation $%s" % _fmt(SimEngine.valuation(state)), Vector2(540, y + 80), 30)
	_label("your slice today: $%s" % _fmt(int(SimEngine.valuation(state) * state.founder_pct / 100.0)),
		Vector2(540, y + 128), 30, PEN)

# ── tab 5: the street ────────────────────────────────────────────────────────
## Wrapped text is MEASURED, never assumed one line — fixed steps stacked the
## street on itself the first week a thesis wrapped (owner photo).
func _wrap_h(text: String, sz: int, w: float) -> float:
	return _font.get_multiline_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, w, sz).y

func _tab_street() -> void:
	_label("the street", Vector2(10, 6), 40)
	var y := 80.0
	for rv in state.rivals:
		var r: Dictionary = rv
		_label("%s — %s" % [String(r.get("name", "?")), SimEngine._fuzz(float(r.get("strength", 20.0)))],
			Vector2(10, y), 32)
		var plays := "plays: " + ", ".join(r.get("tactics", []))
		_label(plays, Vector2(30, y + 42), 26, Color(INK, 0.7), 1070.0)
		y += 50.0 + _wrap_h(plays, 26, 1070.0) + 18.0
	_label("the money:", Vector2(10, y + 10), 32)
	y += 64.0
	for inv in state.investors:
		var d: Dictionary = inv
		_label("%s (%s)" % [String(d.get("name", "?")), String(d.get("archetype", ""))],
			Vector2(10, y), 29)
		var quote := "\"%s\"  ·  %s" % [String(d.get("thesis", "")), String(d.get("trait", ""))]
		_label(quote, Vector2(30, y + 38), 25, Color(INK, 0.65), 1070.0)
		y += 44.0 + _wrap_h(quote, 25, 1070.0) + 16.0

# ── tab 6: threats & promises ────────────────────────────────────────────────
func _tab_threats() -> void:
	_label("threats & promises", Vector2(10, 6), 40)
	var y := 80.0
	if state.clocks.is_empty() and state.statuses.is_empty() and state.commitments.is_empty():
		_label("nothing ticking. that never lasts.", Vector2(10, y), 30, Color(INK, 0.6))
	for c in state.clocks:
		var cd: Dictionary = c
		_label("⏰ in %d wks: %s" % [int(cd.get("weeks_left", 0)), String(cd.get("consequence", ""))],
			Vector2(10, y), 30, PEN)
		y += 52.0
	for s in state.statuses:
		var sd: Dictionary = s
		var kind := String(SimEngine.STATUS.get(String(sd.get("name", "")), {}).get("kind", "condition"))
		_label("%s %s — %d wks left" % ["▲" if kind == "buff" else "▼",
			String(sd.get("name", "")).replace("_", " "), int(sd.get("weeks_left", 0))],
			Vector2(10, y), 30, SAGE if kind == "buff" else PEN)
		y += 52.0
	for cm in state.commitments:
		var cmd: Dictionary = cm
		_label("↻ %s: $%d/wk for %d more wks" % [String(cmd.get("name", "")),
			int(cmd.get("cash_wk", 0)), int(cmd.get("weeks_left", 0))], Vector2(10, y), 30, BLUE)
		y += 52.0

# ─────────────────────────────── drawn pieces ───────────────────────────────
class _Clipboard:
	extends Control
	var active_tab := 0
	func _draw() -> void:
		var w := size.x
		var h := size.y
		draw_rect(Rect2(8, 12, w, h), Color(0, 0, 0, 0.25))
		draw_rect(Rect2(0, 0, w, h), Binder.CREAM)
		var rng := RandomNumberGenerator.new()
		rng.seed = 7
		var pts := PackedVector2Array()
		var corners := [Vector2(3, 3), Vector2(w - 3, 3), Vector2(w - 3, h - 3), Vector2(3, h - 3)]
		for i in 4:
			var a: Vector2 = corners[i]
			var b: Vector2 = corners[(i + 1) % 4]
			for k in 18:
				pts.append(a.lerp(b, float(k) / 18.0) + Vector2(rng.randf_range(-2, 2), rng.randf_range(-2, 2)))
		pts.append(pts[0])
		draw_polyline(pts, Binder.INK, 4.0, true)
		# the clip at the top
		draw_rect(Rect2(w * 0.5 - 70, -18, 140, 34), Binder.YELL)
		draw_rect(Rect2(w * 0.5 - 70, -18, 140, 34), Binder.INK, false, 4.0)
		# the pen ring around the active tab
		# 8 tabs at 150px pitch since the ledger arrived — the ring must match
		# the buttons' own row (owner photos: it circled the gap, then thin air)
		var tx := 24.0 + float(active_tab) * 133.0
		var ring := PackedVector2Array()
		for i in 33:
			var t := TAU * float(i) / 32.0
			ring.append(Vector2(tx + 65.0 + cos(t) * 68.0, 76.0 + sin(t) * 26.0)
				+ Vector2(rng.randf_range(-2, 2), rng.randf_range(-2, 2)))
		draw_polyline(ring, Binder.PEN, 3.5, true)
		# a rule under the tab row
		draw_line(Vector2(30, 108), Vector2(w - 30, 108), Color(Binder.INK, 0.25), 2.0)

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
		# labels around the wheel
		a0 = -PI / 2.0
		for s2 in slices:
			var d2: Dictionary = s2
			var frac2 := clampf(float(d2.get("pct", 0.0)) / 100.0, 0.0, 1.0)
			if frac2 <= 0.01:
				continue
			var mid := a0 + TAU * frac2 * 0.5
			var p := c + Vector2(cos(mid), sin(mid)) * (r + 40.0)
			draw_string(font, p - Vector2(46, -8), String(d2.get("label", "")),
				HORIZONTAL_ALIGNMENT_LEFT, -1, 24, Binder.INK)
			a0 += TAU * frac2
