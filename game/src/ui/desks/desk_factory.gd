class_name DeskFactory
extends RefCounted
## DESK — THE BENCH, the hardware strip on the binder's `product` tab.
## Spec: docs/design/09-hardware.md §11 + INTERFACE DELTA · language:
## docs/design/10-interface-language.md §5.8 · band: docs/design/00-spine.md §11.
##
## `binder.gd` dispatches the product tab to DeskProduct, which calls
## `draw_bench(b)` on Hardware runs and passes the Binder ITSELF, so this file
## draws through the binder's own hand and never reaches into the sheet. On
## every other run nothing here runs and the tab is what it was.
##
## THE BAND IS RULED, NOT INVENTED: 07-roadmap owns the tab above; the spine
## gave the bench y470-740 and 07 yields its footer line to make room. Every
## row below is measured into that band, and nothing of ours renders outside it.
##
## The strip is a working vocabulary lesson, not a dashboard: capacity
## UTILIZATION, CARRYING COST, the LEARNING CURVE, MAKE VS BUY and the FILL RATE
## are printed by name on the run's own numbers, in the same clause as the
## number's cause. The engine owns every figure — the desk reads, and every
## write goes back through a SimFactory clamp.

# ── THE BAND (docs/design/00-spine.md §11: y470-740, header + 6 rows) ────────
const Y_RULE := 470.0     ## the band's top edge — the bench is one object
const Y_HEAD := 484.0     ## what we build, and what one costs
const Y_STATUS := 518.0   ## row 1: stock · capacity · utilization · demand
const Y_BUILD := 546.0    ## row 2: the week's build order (stepper + AUTO)
const Y_MACH := 594.0     ## row 3: the fleet, and this week's casualty
const Y_BUY := 624.0      ## row 4: buy a machine · sell one back
const Y_MVB := 670.0      ## row 5: make vs buy, and what it would have saved
const Y_CARRY := 716.0    ## row 6: what the shelf costs to hold

const X_AUTO := 120.0     ## the AUTO word sits in the stepper name's own gutter
const W_AUTO := 300.0
## THE BUY ROW, MEASURED ACROSS: `buy:` 10 · what you can sign for 70–470 · the
## next rung 480–870, dimmed and wearing the engine's own refusal · the sell arm
## 880–1160. At 300px the refusal wrapped and wrote its second line into the
## make-vs-buy row 46px below, so the rung took the whole gap and one step down
## in size instead.
const W_BUY_1 := 400.0
const X_BUY_2 := 480.0
const W_BUY_2 := 390.0
const SZ_BUY_2 := 19
const X_SELL := 880.0

## Draw THE BENCH. `b` is the Binder itself (untyped to keep the two files free
## of a cyclic class dependency).
static func draw(b) -> void:
	draw_bench(b)

## Drawn INSIDE the product desk on Hardware runs (DeskProduct calls this).
static func draw_bench(b) -> void:
	var state: GameState = b.state
	if not SimFactory.active(state):
		return
	var hw := SimFactory.hw_view(state)
	var w := SimFactory.week_block(state)
	var fresh := not w.is_empty()
	DeskKit.rule(b, Y_RULE)
	_head(b, state)
	_status(b, state, hw, w, fresh)
	_build(b, state, hw)
	_machines(b, state, hw, w)
	_buy(b, state, hw)
	_make_vs_buy(b, state, hw, w, fresh)
	_carrying(b, state, hw)

# ── header: what the bench builds, and what one unit costs to build ──────────
static func _head(b, state: GameState) -> void:
	var base := SimFactory.unit_cost(state)
	var eff := base * SimFactory.learning(state)
	var pct := SimFactory.learning_pct(state)
	var head := "THE BENCH — building: %s · $%s/unit" % [
		SimFactory.flagship_name(state).substr(0, 28), _money2(eff)]
	if pct > 0:
		# the learning curve, named where the price of a unit is first shown
		head += " (base $%s, learning curve −%d%%)" % [_money2(base), pct]
	b.label(head, Vector2(DeskKit.X_ID, Y_HEAD), DeskKit.STATUS, DeskKit.INK, 1120.0)

# ── row 1: the four numbers every production decision needs ─────────────────
## UTILIZATION IS NAMED so idle capacity has a word: it reddens below half (you
## are paying upkeep on machines that did nothing) and at the ceiling (the next
## unit of demand has nowhere to go).
static func _status(b, state: GameState, hw: Dictionary, w: Dictionary, fresh: bool) -> void:
	var f: Font = b.font()
	var cap := SimFactory.capacity(state)
	var x := DeskKit.X_ID
	var seg := "stock: %d units · capacity: %d/wk · " % [int(hw.get("stock", 0)), int(cap)]
	b.label(seg, Vector2(x, Y_STATUS), DeskKit.DETAIL, Color(DeskKit.INK, 0.85), 700.0)
	x += f.get_string_size(seg, HORIZONTAL_ALIGNMENT_LEFT, -1, DeskKit.DETAIL).x
	var util_txt := "utilization: —"
	var util_col := Color(DeskKit.INK, 0.5)
	if fresh:
		var util := int(round(float(w.get("utilization", 0.0)) * 100.0))
		util_txt = "utilization: %d%%" % util
		util_col = DeskKit.PEN if (util < 50 or util >= 100) else Color(DeskKit.INK, 0.85)
	b.label(util_txt, Vector2(x, Y_STATUS), DeskKit.DETAIL, util_col, 320.0)
	x += f.get_string_size(util_txt, HORIZONTAL_ALIGNMENT_LEFT, -1, DeskKit.DETAIL).x
	# the forecast AUTO orders against — smoothed true demand, not what shipped
	b.label(" · demand ≈ %d/wk (4-week smoothed forecast)"
		% int(round(float(hw.get("demand_ema", 0.0)))),
		Vector2(x, Y_STATUS), DeskKit.DETAIL, Color(DeskKit.INK, 0.85), 520.0)

# ── row 2: the week's build order ───────────────────────────────────────────
## The core weekly lever, on the house's own stepper: −/+ walk one unit, the
## engine re-clamps to capacity on every write, and AUTO hands the wheel back to
## the base-stock policy that keeps about four weeks of cover on the shelf.
static func _build(b, state: GameState, hw: Dictionary) -> void:
	var cap := SimFactory.capacity(state)
	var ceiling := maxi(int(floor(cap)), 0)
	var eff := SimFactory.unit_cost(state) * SimFactory.learning(state)
	var auto_on := int(hw.get("production_target", -1)) < 0
	var shown := SimFactory.target_now(state, cap, eff)
	DeskKit.stepper(b, Y_BUILD, {
		"name": "build",
		"value": "%d units" % shown,
		"bound": "at capacity" if shown >= ceiling and ceiling > 0 else "",
		"effect": "$%s each = $%s this week" % [_money2(eff), b.fmt(int(round(float(shown) * eff)))],
		"at_min": shown <= 0,
		"at_max": shown >= ceiling,
		"pitch": 0.0,
		"x_value": DeskKit.X_VALUE,
		"on_minus": func() -> void: handle(b, "build_minus"),
		"on_plus": func() -> void: handle(b, "build_plus"),
	})
	# AUTO rides the stepper name's own gutter — it never crowds the −/+ pair
	DeskKit.word(b, ("AUTO (%d) — tap to take over" % shown) if auto_on
			else "set AUTO (4 wks of cover)",
		Vector2(X_AUTO, Y_BUILD), func() -> void: handle(b, "build_auto"),
		DeskKit.DETAIL, DeskKit.PEN if auto_on else DeskKit.INK, W_AUTO)

# ── row 3: the fleet, priced ────────────────────────────────────────────────
## What the machines contribute and what they cost every week, idle or not. The
## week's broken machine is struck through where it stands, so a breakdown is
## visible exactly where it happened.
static func _machines(b, state: GameState, hw: Dictionary, w: Dictionary) -> void:
	var eq: Array = hw.get("equipment", [])
	if eq.is_empty():
		b.label("machines: none — the founder's hands are the whole line (%d/wk, no upkeep)"
			% int(float(hw.get("capacity_base", 6.0))),
			Vector2(DeskKit.X_ID, Y_MACH), DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 1120.0)
		return
	var f: Font = b.font()
	var down_i := int(w.get("down_i", -1))
	var x := DeskKit.X_ID
	var head := "machines: "
	b.label(head, Vector2(x, Y_MACH), DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 200.0)
	x += f.get_string_size(head, HORIZONTAL_ALIGNMENT_LEFT, -1, DeskKit.DETAIL).x
	# group the identical ones, but never fold the broken one into a group
	var order: Array = []
	var groups := {}
	for i in eq.size():
		if i == down_i:
			continue
		var id := String((eq[i] as Dictionary).get("id", ""))
		if not groups.has(id):
			groups[id] = {"n": 0, "row": eq[i]}
			order.append(id)
		(groups[id] as Dictionary)["n"] = int((groups[id] as Dictionary)["n"]) + 1
	if down_i >= 0 and down_i < eq.size():
		var d: Dictionary = eq[down_i]
		var dtxt := "%s DOWN (+0 this week) · " % String(d.get("name", "a machine"))
		b.label(dtxt, Vector2(x, Y_MACH), DeskKit.DETAIL, DeskKit.PEN, 620.0)
		var dw := f.get_string_size(String(d.get("name", "")), HORIZONTAL_ALIGNMENT_LEFT,
			-1, DeskKit.DETAIL).x
		DeskKit.rule(b, Y_MACH + 12.0, x, dw)   # the strike: it did not run
		x += f.get_string_size(dtxt, HORIZONTAL_ALIGNMENT_LEFT, -1, DeskKit.DETAIL).x
	for i in order.size():
		var g: Dictionary = groups[order[i]]
		var row: Dictionary = g["row"]
		var n := int(g["n"])
		var seg := "%s%s (+%d/wk, $%s/wk)%s" % [String(row.get("name", "machine")),
			(" ×%d" % n) if n > 1 else "",
			int(float(row.get("capacity_add", 0.0)) * float(n)),
			b.fmt(int(float(row.get("upkeep_wk", 0.0)) * float(n))),
			" · " if i < order.size() - 1 else ""]
		b.label(seg, Vector2(x, Y_MACH), DeskKit.DETAIL, Color(DeskKit.INK, 0.85), 900.0)
		x += f.get_string_size(seg, HORIZONTAL_ALIGNMENT_LEFT, -1, DeskKit.DETAIL).x
		if x > 1000.0:
			break

# ── row 4: capacity is bought in lumps, and sells back at half ──────────────
## The buy row shows what this week can actually sign for and the next rung
## above it, dimmed with the ENGINE's own refusal — a gate that hides itself
## teaches nothing. The sell word is armed: it books real money, so it prints
## the haircut in coral before it fires.
static func _buy(b, state: GameState, hw: Dictionary) -> void:
	b.label("buy:", Vector2(DeskKit.X_ID, Y_BUY + 10.0), DeskKit.DETAIL,
		Color(DeskKit.INK, 0.6), 60.0)
	var cells := SimFactory.buy_row(state)
	for i in cells.size():
		var c: Dictionary = cells[i]
		var e: Dictionary = c["entry"]
		var id := String(e.get("id", ""))
		if i == 0 and bool(c.get("ok", false)):
			DeskKit.word(b, "%s  $%s  +%d/wk  $%s/wk upkeep" % [String(e.get("name", id)),
					b.fmt(int(e.get("price", 0))), int(e.get("capacity_add", 0)),
					b.fmt(int(e.get("upkeep_wk", 0)))],
				Vector2(70.0, Y_BUY), func() -> void: handle(b, "buy:" + id),
				DeskKit.LAW, DeskKit.INK, W_BUY_1)
			continue
		# dimmed, wearing the engine's own refusal: era gate, era spend cap, or
		# cash short. The rung takes the whole gap to the sell word at its own
		# smaller size — see the constants above for why.
		b.label("%s $%s — %s" % [String(e.get("name", id)), b.fmt(int(e.get("price", 0))),
				String(c.get("why", ""))],
			Vector2(70.0 if i == 0 else X_BUY_2, Y_BUY + 12.0),
			DeskKit.LAW if i == 0 else SZ_BUY_2,
			Color(DeskKit.INK, 0.35), W_BUY_1 if i == 0 else W_BUY_2)
	var eq: Array = hw.get("equipment", [])
	if eq.size() > 0:
		var last: Dictionary = eq[eq.size() - 1]
		var back := SimFactory.resale_value(String(last.get("id", "")))
		# THE ARMED CAPTION HAS TO FIT ITS OWN BOX. A word button never wraps, so
		# the long form ran the machine's name, the price AND the lesson straight off
		# the right edge of the sheet. The invoice is what §2.9 asks the armed label
		# to carry; the haircut is named in the one word `half`, and the machine is
		# the last one on the row above.
		DeskKit.arm(b, "sell_machine", "sell a machine",
			"$%s back (half) — sure?" % b.fmt(back),
			Vector2(X_SELL, Y_BUY), func() -> void: handle(b, "sell_last"),
			280.0, DeskKit.LAW)

# ── row 5: the classic overflow decision, priced ────────────────────────────
## A contract manufacturer's quote is your marginal cost plus THEIR margin. The
## toggle names the trade and carries the era's own multiplier; the fill rate
## beside it is what the toggle would have saved last week.
static func _make_vs_buy(b, state: GameState, hw: Dictionary, w: Dictionary, fresh: bool) -> void:
	if not SimFactory.sub_unlocked(state):
		b.label("make vs buy — a contract manufacturer answers from the coworking era (overflow at 1.6× your unit cost, capped at your own footprint)",
			Vector2(DeskKit.X_ID, Y_MVB + 10.0), DeskKit.LAW, Color(DeskKit.INK, 0.5), 1120.0)
		return
	var on := bool(hw.get("subcontract_on", false))
	DeskKit.word(b, "make vs buy — overflow to a contract mfr at %s×: %s" % [
			String.num(SimFactory.sub_mult(state.era), 2), "ON" if on else "off"],
		Vector2(DeskKit.X_ID, Y_MVB), func() -> void: handle(b, "mvb"),
		DeskKit.STATUS, DeskKit.PEN if on else DeskKit.INK, 660.0)
	var fill_txt := "fill rate — no week on record yet"
	if fresh:
		fill_txt = "fill rate %d%% — repeat buyers served" % int(round(float(w.get("fill", 1.0)) * 100.0))
	b.label(fill_txt, Vector2(690.0, Y_MVB + 10.0), DeskKit.DETAIL,
		Color(DeskKit.INK, 0.75), 460.0)

# ── row 6: money parked on shelves ──────────────────────────────────────────
## The band's teaching line, in the footer's own grammar: BLUE while it is only
## doing the arithmetic out loud, CORAL the moment the arithmetic is a warning.
static func _carrying(b, state: GameState, hw: Dictionary) -> void:
	var stock := int(hw.get("stock", 0))
	var rate := SimFactory.carrying_rate(state)
	var line := "carrying cost: $%s/wk on %d units (2%% of unit cost every week — capital, shelves and obsolescence)" % [
		b.fmt(int(round(float(stock) * rate))), stock]
	var warn := SimFactory.overstock(state)
	if warn:
		# ONE MEASURED LINE at 1120px. The band's last row is at 716 and the pane
		# ends at 760, so a second line falls off the bottom of the sheet: when the
		# warning fires it takes the parenthetical's place rather than following it.
		line = "carrying cost: $%s/wk on %d units — OVERSTOCK: more than 8 weeks of cover is asleep, and %d%%/wk of unit cost bills for every one of them" % [
			b.fmt(int(round(float(stock) * rate))), stock, int(round(rate / maxf(SimFactory.unit_cost(state), 1.0) * 100.0))]
	else:
		var made := int(hw.get("produced_total", 0))
		if made > 0:
			line += " · %s units built" % _commas(made)
	b.label(line, Vector2(DeskKit.X_ID, Y_CARRY), DeskKit.LAW,
		DeskKit.PEN if warn else DeskKit.BLUE, 1120.0)

# ── presses ─────────────────────────────────────────────────────────────────
## A press inside this desk. `id` is whatever the desk's own draw registered.
## Handlers only ever WRITE STATE — the kit rebuilds the pane afterwards — and
## every write lands through a SimFactory clamp, never straight into the dict.
static func handle(b, id: String) -> void:
	var state: GameState = b.state
	if not SimFactory.active(state):
		return
	var hw := SimFactory.hw_view(state)
	var cap := SimFactory.capacity(state)
	var eff := SimFactory.unit_cost(state) * SimFactory.learning(state)
	var shown := SimFactory.target_now(state, cap, eff)
	match id:
		"build_minus":
			SimFactory.set_target(state, shown - 1)
		"build_plus":
			SimFactory.set_target(state, shown + 1)
		"build_auto":
			# no dead ends: AUTO hands the wheel over, and tapping it again
			# takes the wheel back at exactly the number it was about to build
			SimFactory.set_target(state, -1 if int(hw.get("production_target", -1)) >= 0 else shown)
		"mvb":
			SimFactory.toggle_subcontract(state)
		"sell_last":
			SimFactory.sell_equipment(state, (hw.get("equipment", []) as Array).size() - 1)
		_:
			if id.begins_with("buy:"):
				SimFactory.buy_equipment(state, id.substr(4))

# ── the two number formats this strip speaks ────────────────────────────────
static func _money2(v: float) -> String:
	return "%.2f" % v

static func _commas(n: int) -> String:
	var s := str(absi(n))
	var out := ""
	while s.length() > 3:
		out = "," + s.substr(s.length() - 3) + out
		s = s.substr(0, s.length() - 3)
	return ("-" if n < 0 else "") + s + out
