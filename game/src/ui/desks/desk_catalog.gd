class_name DeskCatalog
extends RefCounted
## DESK — the binder's `pricing` tab. Spec: docs/design/01-catalog.md §7
##
## `binder.gd` dispatches the tab body here and passes ITSELF, so this file draws
## through the binder's own helpers and never reaches into the sheet directly.
## The drawn components come from `ui/components.gd` (DeskKit) — a desk that
## forks one has shipped a second design system.
##
## THE SHOPKEEPER'S SHELF (10-interface-language §5.1). Five states, one sheet:
##
##   LIST     every offer's unit economics, scannable in four seconds
##   DETAIL   one offer's whole ledger: price, every cost line, break-even
##   WRITE    the founder describes something new in plain words
##   WAIT     the street is pricing it (cancellable, callback-guarded)
##   REVIEW   the proposal, adjustable, awaiting the founder's pen
##
## THE OWNER'S REQUIREMENT: nothing an LLM wrote ever enters the books unseen.
## Every road to a new offer ends on the REVIEW card, and the card's confirm is
## the ONLY call to `SimCatalog.add_offer` on this desk.
##
## Desk-local state lives in `b.desk` and dies with the node (HOOKS.md): `mode`
## and `row` are the reserved keys Esc pops, `armed` is the kit's two-tap arm,
## and `pending` / `house` / `text` / `refused` are this desk's own. None of it
## is ever saved — closing the binder discards a proposal mid-air, which is the
## same law from the other side.
##
## PROGRESSIVE DEPTH (01 §5) is the desk GROWING, never options appearing: the
## fine print arrives at coworking, weight and the discount read at office, the
## mini P&L at floor. A locked layer is absent, not greyed.

# ───────────────────────────── the named ladders ─────────────────────────────
## Every stepper walks a named ladder and the engine re-clamps on write; the UI
## is never trusted (10-interface-language §2.1).
const PRICE_MULTS := [0.4, 0.55, 0.7, 0.85, 1.0, 1.15, 1.35, 1.6, 2.0, 2.6, 3.5, 5.0]
const VAR_MULTS := [0.0, 0.02, 0.05, 0.08, 0.12, 0.16, 0.22, 0.30, 0.40, 0.50]
const FIXED_STEPS := [0.0, 5.0, 10.0, 15.0, 25.0, 40.0, 60.0, 90.0, 140.0, 220.0,
	350.0, 550.0, 900.0, 1400.0, 2200.0, 3500.0, 5000.0]
const WEIGHT_STEPS := [0.2, 0.4, 0.6, 0.8, 1.0, 1.3, 1.6, 2.0, 2.5, 3.0]

const ROW_PITCH := 62.0
const ROWS_Y := 84.0
## The last y a row may START at: the growth invitation and the two footer lines
## own everything under it.
const LIST_BOTTOM := 620.0
## The shelf itself never holds more than 8 (SimCatalog.ERA_OFFER_CAP), so this
## cap can only bite on a save that arrived from somewhere else — and then it
## says so rather than hiding a row behind nothing.
const LIST_MAX := 8

# ─────────────────────────────── the dispatch ────────────────────────────────

## Draw the five-state pricing machine. `b` is the Binder itself (untyped to keep
## the two files free of a cyclic class dependency).
static func draw(b) -> void:
	var mode := String(b.desk.get("mode", ""))
	if mode == "":
		# Esc walked out of REVIEW (or the tab was re-entered): a proposal that is
		# no longer on screen is a proposal that no longer exists.
		b.desk.erase("pending")
		b.desk.erase("house")
		b.desk.erase("refused")
		b.desk.erase("short")
	match mode:
		"detail":
			_detail(b)
		"write":
			_write(b)
		"clarify":
			_clarify(b)
		"wait":
			_wait(b)
		"review":
			_review(b)
		_:
			_list(b)

## A press inside this desk. Every control here carries its own closure, so the
## id router stays unused — it exists for desks that prefer it.
static func handle(_b, _id: String) -> void:
	pass

# ─────────────────────────────── 7.1  THE LIST ───────────────────────────────

static func _list(b) -> void:
	var state: GameState = b.state
	DeskKit.title(b, "pricing — what %s sells" % state.company_name)
	if state.offers.is_empty():
		var y0 := DeskKit.empty(b, Vector2(DeskKit.X_ID, 96.0),
			"the world hasn't defined your offers yet — they arrive with the bible.",
			"a company with nothing on the shelf earns nothing: write down what you sell.")
		_new_offer_word(b, y0 + 12.0)
		DeskKit.footer(b, {"rules": "COGS bills only when you sell · fixed bills either way · price − variable = contribution margin"})
		return
	var lc := SimEngine.learning_curve(state)
	var fm := SimEngine.street_fair_mult(state)
	var y := ROWS_Y
	# PRICING DURING A WAR IS THE DECISION THIS TAB EXISTS FOR (03 §5.1): a rival
	# cutting prices moves THE STREET'S reference, not yours, so every verdict
	# below is measured against the lower number and the page says why.
	var war := _war_pct(fm)
	if war > 0:
		b.label("price war: the street's reference is %d%% down — the same price reads dearer this week" % war,
			Vector2(DeskKit.X_ID, 58.0), DeskKit.LAW, DeskKit.PEN, 1100.0)
		y += 26.0
	# ROWS ARE MEASURED, AND THEY STOP WHILE THERE IS STILL PAPER. A price war or
	# a floor-era discount read makes the verdict wrap, which grows the row; eight
	# grown rows do not fit 760px, so the list closes on "+N more" rather than
	# writing the last one over the footer.
	var shown := 0
	for i in mini(state.offers.size(), LIST_MAX):
		if y + ROW_PITCH > LIST_BOTTOM:
			break
		y = _row(b, y, i, lc, fm)
		shown += 1
	y = DeskKit.more(b, Vector2(DeskKit.X_ID, y), state.offers.size() - shown,
		"are on the shelf behind these")
	_new_offer_word(b, y + 8.0)
	_list_footer(b, lc, fm)

## The bottom of the sheet: what the catalog earns, and the laws that made it.
## WARNINGS OUTRANK WISDOM — when a price is losing money on every sale, the
## rules line yields to the lesson (10-interface-language §2.7).
static func _list_footer(b, lc: float, fm: float) -> void:
	var state: GameState = b.state
	var computed := ""
	var rules := "the curve: price at the street's level and demand is fair · discount and demand grows · overprice and it dies fast"
	if state.era_index() >= 1:
		var arpu := SimEngine.offers_arpu(state)
		if arpu >= 0.0:
			var cpc := SimEngine.offers_cogs_per_customer(state)
			computed = "unit economics: ≈ $%.1f ARPU − $%.1f COGS = $%.1f contribution per customer per week  ->  ≈ $%s/wk at %d customers" % [
				arpu, cpc, arpu - cpc, b.fmt(int(round((arpu - cpc) * float(state.traction)))),
				state.traction]
		rules = "COGS bills only when you sell · fixed bills either way · price − variable = contribution margin"
	var warning := ""
	for o in state.offers:
		var od: Dictionary = o
		if SimCatalog.never_pays(od, lc):
			warning = "'%s' never pays for itself — every sale loses $%s" % [
				String(od.get("name", "an offer")),
				b.fmt(int(round(-SimCatalog.contribution(od, lc, fm))))]
			break
	DeskKit.footer(b, {"computed": computed, "rules": rules, "warning": warning})

## THE GROWTH INVITATION, or the honest reason it is shut. A dead button with no
## reason beside it is the one thing a desk may never show.
static func _new_offer_word(b, y: float) -> void:
	var shut := SimCatalog.shelf_full_line(b.state)
	if shut != "":
		b.label(shut, Vector2(DeskKit.X_ID, y + 10.0), DeskKit.DETAIL,
			Color(DeskKit.INK, 0.5), 900.0)
		return
	DeskKit.word(b, "+ sell something new", Vector2(DeskKit.X_ID, y), func() -> void:
		b.desk["mode"] = "write", DeskKit.STATUS, DeskKit.INK, 340.0)

## ONE COLLAPSED ROW (10-interface-language §2.2): the name, the fine print truly
## fine, the verdict where the eye lands, and the controls on the right.
static func _row(b, y: float, i: int, lc: float, fm: float) -> float:
	var state: GameState = b.state
	var o: Dictionary = state.offers[i]
	b.label("%s  ·  %s" % [String(o.get("name", "?")).to_upper(), String(o.get("unit", ""))],
		Vector2(DeskKit.X_ID, y), 28, DeskKit.INK, 400.0)
	b.label(_receipts(b, o, lc, fm), Vector2(DeskKit.X_ID, y + 32.0), 20,
		Color(DeskKit.INK, 0.55), 410.0)
	# THE ROW IS AS TALL AS ITS VERDICT. `about fair (−2% vs street)` and
	# `absurd — ~nobody buys` both wrap the status column, and at a fixed 62px the
	# second line was written into the next offer's own verdict — three rows of
	# status that no longer lined up with the three names beside them.
	var status_h := _status(b, o, y, lc, fm)
	DeskKit.expand(b, Vector2(DeskKit.X_EXPAND, y), func() -> void:
		b.desk["mode"] = "detail"
		b.desk["row"] = i)
	var steps := price_steps(o)
	var cur := float(o.get("price", 0.0))
	_step_btn(b, "−", Vector2(DeskKit.X_MINUS, y), DeskKit.at_min(steps, cur),
		func() -> void: price_step(b, i, -1))
	_step_btn(b, "+", Vector2(DeskKit.X_PLUS, y), DeskKit.at_max(steps, cur),
		func() -> void: price_step(b, i, 1))
	return y + maxf(ROW_PITCH, status_h + 14.0)

## The receipts under a name. The garage sees one number, because at the garage
## price is the only dial; real cost accounting appears at coworking (01 §5).
static func _receipts(b, o: Dictionary, lc: float, fm: float) -> String:
	var served := SimCatalog.served_unit_cost(o, lc)
	if b.state.era_index() < 1:
		return "serve ≈ $%s" % b.fmt(int(round(served)))
	var learned := (" (×%.2f learned)" % lc) if lc < 0.995 else ""
	return "serve ≈ $%s%s · fixed $%s/wk · margin $%s/unit" % [
		b.fmt(int(round(served))), learned,
		b.fmt(int(round(float(o.get("fixed_wk", 0.0))))),
		b.fmt(int(round(SimCatalog.contribution(o, lc, fm))))]

## THE THREE-STATE STATUS COLUMN: a giveaway the founder chose, a price nobody
## named, or the price with its verdict. COLOUR NEVER CARRIES ALONE — every one
## of them says it in words first.
## Returns the MEASURED height of the verdict, so the row can be as tall as what
## the street had to say about the price.
static func _status(b, o: Dictionary, y: float, lc: float, fm: float) -> float:
	var price := float(o.get("price", 0.0))
	var fair := float(o.get("fair_price", 1.0)) * fm
	var text := ""
	var col := DeskKit.INK
	if price <= 0.0 and bool(o.get("price_set", false)):
		text = "FREE ON PURPOSE — pays in users, not dollars"
		col = DeskKit.BLUE
	elif price <= 0.0:
		text = "! billing at the going rate $%s" % b.fmt(int(round(fair)))
		col = DeskKit.PEN
	else:
		text = "$%s  ·  margin $%s/unit  ·  %s" % [b.fmt(int(round(price))),
			b.fmt(int(round(SimCatalog.contribution(o, lc, fm)))), _verdict(b.state, o, price, fm)]
		if SimCatalog.never_pays(o, lc) or SimEngine.offer_demand(o, price, fm) <= 0.25:
			col = DeskKit.PEN
	b.label(text, Vector2(DeskKit.X_VALUE, y + 4.0), 26, col, 480.0)
	return 4.0 + maxf(b.wrap_h(text, 26, 480.0), 34.0)

## What the street makes of this price, in words. Discounting only gets NAMED as
## a strategy at office, where portfolio management unlocks (01 §5).
static func _verdict(state: GameState, o: Dictionary, price: float, fm: float) -> String:
	var dem := SimEngine.offer_demand(o, price, fm)
	var word := "about fair"
	if dem >= 1.15:
		word = "a deal — demand ×%.1f" % dem
	elif dem <= 0.25:
		word = "absurd — ~nobody buys"
	elif dem < 0.85:
		word = "pricey — %d%% of fair demand" % int(dem * 100.0)
	var fair := float(o.get("fair_price", 0.0)) * fm
	if state.era_index() >= 2 and fair > 0.0 and price < fair:
		word += " (−%d%% vs street)" % int(round((1.0 - price / fair) * 100.0))
	return word

## How far a rival's price war has moved the going rate, in whole percent. 0 in a
## quiet week — the street's reference IS the list price until somebody cuts.
static func _war_pct(fm: float) -> int:
	return int(round((1.0 - fm) * 100.0))

# ────────────────────────────── 7.2  THE DETAIL ──────────────────────────────

## One offer, the whole pane — the row's fine print with room to think. Only one
## is ever open, and DETAIL REPLACES the list rather than pushing it down.
static func _detail(b) -> void:
	var state: GameState = b.state
	var i := int(b.desk.get("row", -1))
	if i < 0 or i >= state.offers.size():
		b.desk["mode"] = ""       # the offer was dropped out from under the page
		b.desk.erase("row")
		_list(b)
		return
	var o: Dictionary = state.offers[i]
	var era := state.era_index()
	var lc := SimEngine.learning_curve(state)
	var fm := SimEngine.street_fair_mult(state)
	var nm := String(o.get("name", "an offer"))
	DeskKit.back(b, "back to all offers", func() -> void:
		b.desk["mode"] = ""
		b.desk.erase("row"))
	# DROPPING IS INSTANT BEHIND THE ARM (DECISIONS.md): the lost revenue is the
	# natural cost of the decision; the second tap is the only ceremony it gets.
	DeskKit.arm(b, "drop", "drop this offer ×", "sure? it disappears ×",
		Vector2(880.0, 6.0), func() -> void:
			SimCatalog.remove_offer(state, i)
			state.log_action("DROPPED the offer: %s" % nm)
			b.desk["mode"] = ""
			b.desk.erase("row"), 260.0, 24)
	var y := 58.0
	b.label("%s · %s" % [nm.to_upper(), String(o.get("unit", ""))],
		Vector2(DeskKit.X_ID, y), 32, DeskKit.INK, 860.0)
	y += 34.0
	var learned := (" (learning ×%.2f)" % lc) if lc < 0.995 else ""
	# the going rate is THE STREET'S, and a rival's war moves it: the reference
	# every number below is measured against says so in the same breath (03 §5.1)
	var war := _war_pct(fm)
	b.label("the street charges ≈ $%s%s · a sale costs ≈ $%s to serve%s · fixed $%s/wk" % [
		b.fmt(int(round(float(o.get("fair_price", 0.0)) * fm))),
		(" (price war: −%d%%)" % war) if war > 0 else "",
		b.fmt(int(round(SimCatalog.served_unit_cost(o, lc)))), learned,
		b.fmt(int(round(float(o.get("fixed_wk", 0.0)))))],
		Vector2(DeskKit.X_ID, y), 24, Color(DeskKit.INK, 0.6), 1100.0)
	y += 32.0
	y = _price_row(b, y, o, i, era, lc, fm)
	y = cost_story(b, y, o, era, lc, fm)
	y = sprint_arm(b, y, o)
	if era >= 2:
		y = _weight_row(b, y, o)
	# THE FLOOR UNLOCK, whenever a line still fits on the sheet. Every pitch above
	# was tightened so the densest offer the engine allows — 4 variable lines, 3
	# standing ones, the weight row — still leaves it room; a founder does not lose
	# the era's whole lesson for itemising honestly.
	if era >= 3 and y + 30.0 <= DeskKit.PANE_H:
		y = _mini_pnl(b, y, o, lc, fm)

## THE PRICE ROW — the founder's one strategic dial, with the margin it makes.
static func _price_row(b, y: float, o: Dictionary, i: int, era: int, lc: float,
		fm: float) -> float:
	var steps := price_steps(o)
	var cur := float(o.get("price", 0.0))
	var value := "$%s per unit" % b.fmt(int(round(cur)))
	if cur <= 0.0:
		value = "$0 — free on purpose" if bool(o.get("price_set", false)) \
			else "unpriced — bills at $%s" % b.fmt(int(round(float(o.get("fair_price", 0.0)) * fm)))
	var why := "the going rate is $%s — you name what you charge" % b.fmt(int(round(float(o.get("fair_price", 0.0)) * fm)))
	if era >= 1:
		why = "contribution margin $%s/unit (price − variable cost)" % b.fmt(int(round(SimCatalog.contribution(o, lc, fm))))
	return DeskKit.stepper(b, y, {
		"name": "price", "why": why, "value": value,
		"effect": _verdict(b.state, o, cur, fm) if cur > 0.0 else "not on sale at a named price",
		"x_value": DeskKit.X_VALUE, "pitch": 68.0,
		"at_min": DeskKit.at_min(steps, cur), "at_max": DeskKit.at_max(steps, cur),
		"on_minus": func() -> void: price_step(b, i, -1),
		"on_plus": func() -> void: price_step(b, i, 1)})

## THE SPRINT ARM — the one road to a cheaper serve: a real roadmap bet the
## team builds (R&D capacity, the dice at ship). Two-tap, like every commitment.
static func sprint_arm(b, y: float, o: Dictionary) -> float:
	var nm := String(o.get("name", ""))
	var has := false
	for bv in b.state.bets:
		var bd: Dictionary = bv
		if String(bd.get("kind", "")) == "cost_down" \
				and String(bd.get("offer", "")) == nm and not bool(bd.get("shipped", false)):
			has = true
	if has:
		b.label("a cost sprint for this offer is on the roadmap — the team is on it",
			Vector2(DeskKit.X_ID, y), DeskKit.DETAIL, Color(DeskKit.INK, 0.55), 900.0)
		return y + 30.0
	DeskKit.arm(b, "sprint_" + nm, "start a cost sprint — the team rebuilds it",
		"3 R&D-weeks of the team — sure?", Vector2(DeskKit.X_ID, y), func() -> void:
			SimRoadmap.add_cost_down_bet(b.state, nm),
		560.0, 22)
	return y + 44.0

## THE COST STORY — READ-ONLY (owner: the player never dials a cost; the world
## set this service's costs, stated and explained; cutting them is a BUILD).
## Both the full page and the rate card's open row draw this same block.
static func cost_story(b, y: float, o: Dictionary, era: int, lc: float,
		fm: float = 1.0) -> float:
	var fair := maxf(float(o.get("fair_price", 1.0)), 1.0)
	b.label("what one sale costs — the world set these when the offer was written",
		Vector2(DeskKit.X_ID, y), DeskKit.DETAIL, Color(DeskKit.INK, 0.55), 900.0)
	y += 30.0
	var lines: Array = o.get("cost_lines", [])
	if era >= 1 and not lines.is_empty():
		for li in lines:
			var ld: Dictionary = li
			var amt := float(ld.get("amount", 0.0))
			b.label("%s — $%s (%d%% of the going rate)" % [String(ld.get("label", "line")),
				b.fmt(int(round(amt))), int(round(amt / fair * 100.0))],
				Vector2(40.0, y), DeskKit.DETAIL, Color(DeskKit.INK, 0.7), 900.0)
			y += 26.0
	y = _sum_line(b, y, "= serve cost $%s/unit (served at ×%.2f today) · standing tools $%s/wk" % [
		b.fmt(int(round(float(o.get("unit_cost", 0.0))))), lc,
		b.fmt(int(round(float(o.get("fixed_wk", 0.0)))))], DeskKit.BLUE)
	var be := SimCatalog.break_even(o, lc, fm)
	if be < 0:
		y = _sum_line(b, y, "this price never pays for itself — every sale loses $%s" % [
			b.fmt(int(round(-SimCatalog.contribution(o, lc, fm))))], DeskKit.PEN)
	else:
		y = _sum_line(b, y, "break-even: %d sales/wk pay the standing costs" % be, DeskKit.BLUE)
	var capl := clampf(float(o.get("capacity_per_unit", 1.0)), 0.1, 40.0)
	if b.state.biz_what == "Service" and capl != 1.0:
		var slots2 := SimWorks.service_capacity(b.state)
		b.label("one %s = %.1f hours of hands · today's crew: ≈ %d/wk" % [
			String(o.get("unit", "unit")).trim_prefix("per "), capl,
			int(slots2 / capl) if capl > 0.0 else 0],
			Vector2(DeskKit.X_ID, y), DeskKit.DETAIL, Color(DeskKit.INK, 0.7), 900.0)
		y += 26.0
	b.label("costs only fall when the team rebuilds how this one is made — a cost sprint below",
		Vector2(DeskKit.X_ID, y), DeskKit.LAW, Color(DeskKit.INK, 0.45), 1080.0)
	return y + 26.0

## THE BLUE LINE DOES THE ARITHMETIC OUT LOUD — the patient accountant.
static func _sum_line(b, y: float, text: String, col: Color) -> float:
	b.label(text, Vector2(DeskKit.X_ID + 18.0, y), 22, col, 1080.0)
	return y + maxf(b.wrap_h(text, 22, 1080.0), 26.0) + 6.0

## THE SHELF METER (office+): weight is share-of-wallet, and the wallet is
## finite. The bound prints its own reason, which IS the lesson.
static func _weight_row(b, y: float, o: Dictionary) -> float:
	var state: GameState = b.state
	var cur := float(o.get("weight", 1.0))
	var others := SimCatalog.shelf_weight(state) - cur
	var ceiling := minf(SimCatalog.MAX_WEIGHT, SimCatalog.SHELF_WEIGHT_CAP - others)
	return DeskKit.stepper(b, y, {
		"name": "shelf weight", "why": "the slice of a customer's wallet this one claims",
		"value": "%.1f" % cur,
		# ∑ (U+2211) is IN the hand; Σ (U+03A3, the Greek letter) is not, and the
		# two are a pixel apart to read and a whole typeface apart to draw.
		"effect": "shelf: ∑%.1f of %.1f used" % [SimCatalog.shelf_weight(state), SimCatalog.SHELF_WEIGHT_CAP],
		"bound": "(the shelf is full)" if cur >= ceiling - 0.001 else "",
		"x_value": DeskKit.X_VALUE, "pitch": 66.0,
		"at_min": DeskKit.at_min(WEIGHT_STEPS, cur), "at_max": cur >= ceiling - 0.001,
		"on_minus": func() -> void:
			o["weight"] = clampf(DeskKit.ladder(WEIGHT_STEPS, cur, -1),
				SimCatalog.MIN_WEIGHT, ceiling),
		"on_plus": func() -> void:
			o["weight"] = clampf(DeskKit.ladder(WEIGHT_STEPS, cur, 1),
				SimCatalog.MIN_WEIGHT, ceiling)})

## THE OFFER'S OWN P&L (floor+): a product line reads like a small company.
static func _mini_pnl(b, y: float, o: Dictionary, lc: float, fm: float) -> float:
	var state: GameState = b.state
	var sales := float(state.traction) * float(o.get("weight", 1.0)) \
			* SimEngine.offer_cadence(String(o.get("unit", "")))
	var inc := sales * SimEngine.offer_billed_price(o, fm)
	var variable := sales * SimCatalog.served_unit_cost(o, lc)
	var fixed := float(o.get("fixed_wk", 0.0))
	var text := "this offer, a week at current volume: ≈%d sales -> $%s in − $%s variable − $%s fixed = $%s contribution" % [
		int(round(sales)), b.fmt(int(round(inc))), b.fmt(int(round(variable))),
		b.fmt(int(round(fixed))), b.fmt(int(round(inc - variable - fixed)))]
	b.label(text, Vector2(DeskKit.X_ID, y), 22, DeskKit.BLUE, 1100.0)
	return y + maxf(b.wrap_h(text, 22, 1100.0), 26.0)

# ─────────────────────────────── 7.3  THE WRITE ──────────────────────────────

## THE OFFER FORM (owner: a real interface — name, full description, what it
## bundles, the unit wish, who it is for). Every field survives a rebuild in
## b.desk under f_*; the street reads them all as structured input.
const UNIT_HINTS := ["let the street pick", "per session", "per month", "per order",
	"per unit", "per year", "per hour", "per package", "per kit"]

static func _field(b, y: float, key: String, label: String, placeholder: String,
		height: float, single: bool = false) -> void:
	b.label(label, Vector2(DeskKit.X_ID, y), 18, Color(DeskKit.INK, 0.5), 400.0)
	var te := TextEdit.new()
	te.position = Vector2(DeskKit.X_ID, y + 22.0)
	te.size = Vector2(1140, height)
	te.add_theme_font_override("font", b.font())
	te.add_theme_font_size_override("font_size", 26)
	te.add_theme_color_override("font_color", DeskKit.INK)
	te.add_theme_color_override("font_placeholder_color", Color(DeskKit.INK, 0.30))
	te.add_theme_color_override("caret_color", DeskKit.PEN)
	# THE PAPER IS THE FIELD: no box, no fill — the rule underneath says "write here"
	for stn in ["normal", "focus", "read_only"]:
		te.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	te.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
	te.placeholder_text = placeholder
	te.text = String(b.desk.get(key, ""))
	b.pane().add_child(te)
	DeskKit.rule(b, y + 22.0 + height + 4.0)
	te.text_changed.connect(func() -> void:
		b.desk[key] = te.text.replace("\n", " ") if single else te.text)
	if key == "f_name":
		te.grab_focus()

static func _write(b) -> void:
	# a journal-drafted offer lands here pre-filled, once
	if String(b.desk.get("f_desc", "")) == "" and b.state.offer_draft != "":
		b.desk["f_desc"] = b.state.offer_draft
		b.state.offer_draft = ""
	# THE BINDER'S OWN STATIONERY (owner sign-off: shape C) — a pre-printed
	# sheet, agnostic of the business: the same five blanks price a massage,
	# a seat, a kit or a marketplace listing.
	b.label("NEW OFFER — INTAKE SHEET", Vector2(DeskKit.X_ID, 8.0), 30, DeskKit.INK, 700.0)
	var stamp: Label = b.label("RUNWAY! form 01-C", Vector2(920.0, 12.0), 16,
		Color(DeskKit.INK, 0.45), 200.0)
	stamp.rotation = -0.03
	DeskKit.pen_rule(b, 48.0)
	_field(b, 56.0, "f_name", "offer, called", "its working name — the street may tidy it", 34.0, true)
	_field(b, 128.0, "f_desc", "in plain words, what it is",
		"write it the way you would say it out loud…", 96.0)
	_field(b, 262.0, "f_includes", "a buyer walks away with",
		"the pieces: the work, the time, the materials, the follow-up…", 70.0)
	_field(b, 370.0, "f_audience", "for (optional)",
		"which of your customers this is aimed at", 34.0, true)
	# billed (circle one): every unit on one row, the chosen one in the pen
	b.label("billed (circle one)", Vector2(DeskKit.X_ID, 446.0), 18,
		Color(DeskKit.INK, 0.5), 400.0)
	var chosen := String(b.desk.get("f_unit", UNIT_HINTS[0]))
	var ux := DeskKit.X_ID
	var uy := 470.0
	for u in UNIT_HINTS:
		var uw: float = b.font().get_string_size(String(u),
			HORIZONTAL_ALIGNMENT_LEFT, -1, 20).x + 26.0
		if ux + uw > 1130.0:
			ux = DeskKit.X_ID
			uy += 34.0
		var uv := String(u)
		DeskKit.word(b, uv, Vector2(ux, uy), func() -> void:
			b.desk["f_unit"] = uv, 20,
			DeskKit.PEN if uv == chosen else Color(DeskKit.INK, 0.55), uw)
		if uv == chosen:
			DeskKit.pen_rule(b, uy + 26.0, ux, ux + uw - 20.0)
		ux += uw + 8.0
	var by := uy + 44.0
	DeskKit.word(b, "send it to the street", Vector2(DeskKit.X_ID, by), func() -> void:
		_submit(b), DeskKit.ROW, DeskKit.PEN, 340.0)
	DeskKit.word(b, "never mind", Vector2(380.0, by), func() -> void:
		b.desk["mode"] = ""
		for k in ["f_name", "f_desc", "f_includes", "f_audience", "f_unit", "text", "oq", "oa"]:
			b.desk.erase(k), DeskKit.ROW, Color(DeskKit.INK, 0.7), 200.0)
	if bool(b.desk.get("short", false)):
		b.label("a few words of description at least — the street can't price a shrug",
			Vector2(DeskKit.X_ID, by + 46.0), DeskKit.STATUS, DeskKit.PEN, 900.0)
	DeskKit.footer(b, {"rules": "the street writes the terms — costs are the world's; the price stays yours"})

## THE STREET'S FOLLOW-UP (owner: generated clarification, multiple choice).
## The sheet condenses at the top; each question offers its options as words;
## every answer inked = the pricing fires with the q/a pairs attached.
static func _clarify(b) -> void:
	var oq: Array = b.desk.get("oq", [])
	var oa: Dictionary = b.desk.get("oa", {})
	b.label("THE STREET HAS QUESTIONS", Vector2(DeskKit.X_ID, 8.0), 30, DeskKit.INK, 700.0)
	b.label("\"%s\" — %s" % [String(b.desk.get("f_name", "the new offer")),
		String(b.desk.get("f_desc", "")).substr(0, 90)],
		Vector2(DeskKit.X_ID, 52.0), 20, Color(DeskKit.INK, 0.55), 1100.0)
	DeskKit.pen_rule(b, 84.0)
	var y := 100.0
	for i in oq.size():
		var qd: Dictionary = oq[i]
		b.label(String(qd.get("q", "")), Vector2(DeskKit.X_ID, y), 24, DeskKit.INK, 1100.0)
		y += maxf(b.wrap_h(String(qd.get("q", "")), 24, 1100.0), 30.0) + 6.0
		var picked := String(oa.get(str(i), ""))
		var ox := DeskKit.X_ID + 20.0
		for opt in qd.get("options", []):
			var ov := String(opt)
			var ow: float = b.font().get_string_size(ov,
				HORIZONTAL_ALIGNMENT_LEFT, -1, 22).x + 28.0
			if ox + ow > 1130.0:
				ox = DeskKit.X_ID + 20.0
				y += 36.0
			var qi := i
			DeskKit.word(b, ov, Vector2(ox, y), func() -> void:
				var oa2: Dictionary = b.desk.get("oa", {})
				oa2[str(qi)] = ov
				b.desk["oa"] = oa2, 22,
				DeskKit.PEN if ov == picked else Color(DeskKit.INK, 0.6), ow)
			if ov == picked:
				DeskKit.pen_rule(b, y + 28.0, ox, ox + ow - 20.0)
			ox += ow + 10.0
		y += 48.0
	var all_in := oa.size() >= oq.size() and oq.size() > 0
	if all_in:
		DeskKit.word(b, "that's everything — price it", Vector2(DeskKit.X_ID, y + 6.0),
			func() -> void: _fire_price(b), DeskKit.ROW, DeskKit.PEN, 420.0)
	else:
		b.label("answer each — then the street prices it", Vector2(DeskKit.X_ID, y + 6.0),
			DeskKit.DETAIL, Color(DeskKit.INK, 0.45), 600.0)
	DeskKit.word(b, "never mind", Vector2(500.0, y + 6.0), func() -> void:
		b.desk["mode"] = "write", DeskKit.ROW, Color(DeskKit.INK, 0.7), 200.0)

## The one road out of WRITE. Keyed: the street prices it and the reply lands on
## the review card. Keyless: the house numbers arrive instantly and the card is
## identical, with one dry footnote (01 §8.4).
static func _submit(b) -> void:
	var desc := String(b.desk.get("f_desc", b.desk.get("text", ""))).strip_edges()
	if desc.length() < 3:
		b.desk["short"] = true
		b.refresh()      # Enter has no rebuild of its own; the answer must still land
		return
	b.desk.erase("short")
	var hint := String(b.desk.get("f_unit", ""))
	var fields := {
		"name": String(b.desk.get("f_name", "")).strip_edges().substr(0, 40),
		"description": desc.substr(0, 500),
		"includes": String(b.desk.get("f_includes", "")).strip_edges().substr(0, 300),
		"unit_hint": "" if hint.begins_with("let the street") else hint,
		"audience_note": String(b.desk.get("f_audience", "")).strip_edges().substr(0, 120),
	}
	b.desk["text"] = desc   # the wait card + the house fallback read one line
	b.desk["fields"] = fields
	if _street_is_reachable(b):
		# UNDERSTAND FIRST, PRICE SECOND: the street may ask up to 3
		# multiple-choice questions before it writes the terms
		b.desk["mode"] = "wait"
		b.generator.clarify_offer_intake(b.state, fields, func(cres: Dictionary) -> void:
			if not is_instance_valid(b) or String(b.desk.get("mode", "")) != "wait":
				return
			var qs: Array = cres.get("questions", [])
			if bool(cres.get("ready", true)) or qs.is_empty():
				_fire_price(b)
				return
			b.desk["oq"] = qs
			b.desk["oa"] = {}
			b.desk["mode"] = "clarify"
			b.refresh())
		b.refresh()
		return
	b.desk["pending"] = _proposal(b.state, SimCatalog.draft_terms(b.state, desc))
	b.desk["house"] = true
	b.desk["mode"] = "review"
	b.refresh()

## The priced call, with any clarify answers attached as binding facts.
static func _fire_price(b) -> void:
	var fields: Dictionary = b.desk.get("fields", {})
	var oq: Array = b.desk.get("oq", [])
	var oa: Dictionary = b.desk.get("oa", {})
	if not oq.is_empty():
		var clar: Array = []
		for i in oq.size():
			clar.append({"q": String((oq[i] as Dictionary).get("q", "")),
				"a": String(oa.get(str(i), ""))})
		fields["clarifications"] = clar
	b.desk["mode"] = "wait"
	b.generator.price_offer_idea(b.state, fields, func(res: Dictionary) -> void:
		_land(b, String(b.desk.get("text", "")), res))
	b.refresh()

## THE REPLY COMES BACK. CANCEL IS REAL, so this is the gate rather than the
## hand-off: the desk must still be alive AND still be the desk that asked, or
## the terms land on the floor. A binder closed mid-flight took its desk state
## with it, and no offer ever appears unreviewed.
##
## An empty answer is not a failure state, only a quieter one: the house numbers
## fill the same card, with the one dry footnote (01 §8.4).
static func _land(b, idea: String, res: Dictionary) -> void:
	if not is_instance_valid(b) or String(b.desk.get("mode", "")) != "wait":
		return
	var house := res.is_empty()
	b.desk["pending"] = _proposal(b.state,
		SimCatalog.draft_terms(b.state, idea) if house else res)
	b.desk["house"] = house
	b.desk["mode"] = "review"
	b.refresh()

# ─────────────────────────────── 7.4  THE WAIT ───────────────────────────────

## One breathing line and a cancel word — no spinner, no dots, no progress bar,
## and the subject is always the fiction (10-interface-language §2.12).
##
## CANCEL IS REAL: leaving drops the reply on arrival. Every callback guards the
## node's liveness AND the mode before it touches anything, so a proposal that
## nobody is waiting for lands on the floor instead of on the shelf.
static func _wait(b) -> void:
	DeskKit.title(b, "pricing — what %s sells" % b.state.company_name)
	DeskKit.wait(b, Vector2(DeskKit.X_ID, 96.0), "the street is pricing it…", func() -> void:
		b.desk["mode"] = ""
		b.desk.erase("text"))
	b.label("\"%s\"" % String(b.desk.get("text", "")), Vector2(DeskKit.X_ID, 200.0),
		DeskKit.DETAIL, Color(DeskKit.INK, 0.45), 1100.0)

## THE STREET'S OWN VOICE (01 §8 L1) — one `request_json` on the "clarify" tier,
## `EventGenerator.price_offer_idea`, which maps plain words to plausible market
## terms and concrete cost labels in this business's own vocabulary. That is the
## thing a model does well and a lookup table cannot; every number it returns
## still dies in `add_offer`'s clamps, and the founder still signs the card.
##
## No key, no generator, model down: the road simply is not there, and WRITE goes
## straight to REVIEW on house numbers. Keyless is not a degraded screen — it is
## the same desk with a dry footnote (01 §8.4).
static func _street_is_reachable(b) -> bool:
	var gen = b.generator
	return gen != null and gen.llm != null and gen.llm.enabled()

# ────────────────────────────── 7.5  THE REVIEW ──────────────────────────────

## A PROPOSAL AWAITING THE FOUNDER'S PEN. Same renderer language as DETAIL, three
## changes: the coral banner replaces the way back, the price row becomes a note
## (one decision at a time — the bang walks the founder back here to price it),
## and the bottom carries the two words that end the state.
##
## THE LINES ARE THE ONLY ADJUSTABLE THING. Name, unit, fair price, elasticity and
## weight are the STREET'S read, not the founder's lever — arguing with the market
## about what it pays is not a game mechanic, and the founder's real levers are
## price (later) and costs (now).
static func _review(b) -> void:
	var state: GameState = b.state
	var p: Dictionary = b.desk.get("pending", {})
	if p.is_empty():
		b.desk["mode"] = ""
		_list(b)
		return
	var lc := SimEngine.learning_curve(state)
	# NO WAR MULTIPLIER HERE, on purpose. This card is the street's DURABLE terms
	# — what this audience pays for this kind of thing — and a rival's price cut
	# is a condition on what it bills THIS WEEK, not on what the market is worth.
	# The founder meets that number on the list and the detail card, where the
	# price decision actually happens; folding it into the quote would teach that
	# a transient status changed the market itself.
	var fair := maxf(float(p.get("fair_price", 1.0)), 1.0)
	var era := state.era_index()
	var groups: Array = []
	if era < 1:
		var vcur := float(p.get("unit_cost", 0.0))
		groups.append({"caption": "what one sale costs to serve",
			"lines": [_review_total(p, "serve cost", "$%s/unit" % b.fmt(int(round(vcur))),
				"%d%% of fair" % int(round(vcur / fair * 100.0)),
				var_steps(fair), vcur, true)],
			"sum": "= variable cost $%s/unit · served at ×%.2f today" % [
				b.fmt(int(round(vcur))), lc]})
		var fcur := float(p.get("fixed_wk", 0.0))
		groups.append({"caption": "standing costs — every week, sold or not",
			"lines": [_review_total(p, "tools", "$%s/wk" % b.fmt(int(round(fcur))),
				"billed sold or not", FIXED_STEPS, fcur, false)],
			"sum": _review_fixed_sum(b, p, lc)})
	else:
		groups.append({"caption": "what one sale costs — variable",
			"lines": _review_lines(b, p, p.get("cost_lines", []), fair, lc, true),
			"sum": "= variable cost $%s/unit · served at ×%.2f today" % [
				b.fmt(int(round(float(p.get("unit_cost", 0.0))))), lc]})
		groups.append({"caption": "standing costs — every week, sold or not",
			"lines": _review_lines(b, p, p.get("fixed_lines", []), fair, lc, false),
			"sum": _review_fixed_sum(b, p, lc)})
	DeskKit.review(b, {
		"banner": "THE INTAKE SHEET — the street's terms are in",
		"read": _review_read(b, p, fair),
		"groups": groups,
		"verdict": "" if SimCatalog.break_even(p, lc) >= 0 else
			"this price never pays for itself — every sale loses $%s" % b.fmt(int(round(-SimCatalog.contribution(p, lc)))),
		"note": DeskKit.HOUSE_NOTE if bool(b.desk.get("house", false)) else "",
		"refused": String(b.desk.get("refused", "")),
		"confirm": "put it on the shelf", "cancel": "tear it up",
		"on_confirm": func() -> void: _shelve(b, p),
		"on_cancel": func() -> void:
			b.desk["mode"] = ""
			for k in ["text", "f_name", "f_desc", "f_includes", "f_audience",
					"f_unit", "fields", "oq", "oa"]:
				b.desk.erase(k)})

## THE ONLY CALL TO `add_offer` ON THIS DESK. A raced cap comes back as a printed
## reason in the desk's own voice, never as a silently-dead button.
static func _shelve(b, p: Dictionary) -> void:
	# the reason is read AT THE PRESS, not at the render: a week can pass between
	# the two, and a stale explanation is worse than none
	var shut := SimCatalog.shelf_full_line(b.state)
	var made: Dictionary = SimCatalog.add_offer(b.state, String(p.get("name", "an offer")),
		String(p.get("unit", "per order")), float(p.get("fair_price", 1.0)),
		float(p.get("unit_cost", 0.0)), float(p.get("elasticity", 2.0)),
		float(p.get("weight", 1.0)), p.get("cost_lines", []), p.get("fixed_lines", []))
	if made.is_empty():
		b.desk["refused"] = shut if shut != "" else "the shelf refused it — drop something first"
		return
	b.state.log_action("NEW OFFER shelved: %s (%s) — street $%d" % [
		String(made.get("name", "")), String(made.get("unit", "")),
		int(round(float(made.get("fair_price", 0.0))))])
	b.desk["mode"] = ""
	for k in ["text", "f_name", "f_desc", "f_includes", "f_audience", "f_unit",
			"fields", "oq", "oa"]:
		b.desk.erase(k)

static func _review_lines(b, p: Dictionary, lines: Array, fair: float, lc: float,
		variable: bool) -> Array:
	var out: Array = []
	for l in lines:
		var ld: Dictionary = l
		var steps := var_steps(fair) if variable else FIXED_STEPS
		var amount := float(ld.get("amount", 0.0))
		var effect := "%d%% of fair" % int(round(amount / fair * 100.0))
		if not variable:
			var margin := SimCatalog.contribution(p, lc)
			effect = ("%d sales/wk pays it" % int(ceil(amount / margin))) if margin > 0.0 \
				else "no margin to pay it"
		# READ-ONLY (owner: the world sets an offer's costs — a founder who
		# could dial them would dial them to zero); steps stay for the pitch
		out.append({
			"name": String(ld.get("label", "line")),
			"value": ("$%s" % b.fmt(int(round(amount)))) if variable else ("$%s/wk" % b.fmt(int(round(amount)))),
			"effect": effect, "pitch": 34.0, "static": true})
	return out

## GARAGE TOTALS MODE (01 §5): one stepper for the whole variable sheet, one for
## the whole standing sheet. The lines behind them are scaled proportionally and
## kept, so nothing the street itemised is lost when coworking reveals it.
static func _review_total(_p: Dictionary, nm: String, value: String, effect: String,
		_steps: Array, _cur: float, _variable: bool) -> Dictionary:
	# READ-ONLY (owner: the world's costs, stated — never a founder's dial)
	return {"name": nm, "value": value, "effect": effect, "pitch": 34.0, "static": true}

static func _review_fixed_sum(b, p: Dictionary, lc: float) -> String:
	var be := SimCatalog.break_even(p, lc)
	if be < 0:
		return "= $%s/wk · nothing here pays for it yet" % b.fmt(int(round(float(p.get("fixed_wk", 0.0)))))
	return "= $%s/wk · break-even: %d sales/wk pay for it" % [
		b.fmt(int(round(float(p.get("fixed_wk", 0.0))))), be]

## The review's read block: identity, the world's own line, its visible
## reasoning (owner: stated and explained), then the unpriced law.
static func _review_read(b, p: Dictionary, fair: float) -> Array:
	var read: Array = []
	# YOUR SIDE, condensed — the sheet's words stay visible above the terms
	var yours := String(b.desk.get("f_desc", "")).substr(0, 110)
	if yours != "":
		read.append("you wrote: %s" % yours)
	var oq: Array = b.desk.get("oq", [])
	var oa: Dictionary = b.desk.get("oa", {})
	for i in oq.size():
		if String(oa.get(str(i), "")) != "":
			read.append("— %s  ->  %s" % [String((oq[i] as Dictionary).get("q", "")),
				String(oa.get(str(i), ""))])
	read.append("%s · %s — the street charges ≈ $%s · elasticity %s · weight %.1f" % [
		String(p.get("name", "an offer")).to_upper(), String(p.get("unit", "")),
		b.fmt(int(round(fair))), _elasticity_word(float(p.get("elasticity", 2.0))),
		float(p.get("weight", 1.0))])
	if String(p.get("desc", "")) != "":
		read.append(String(p.get("desc", "")))
	if String(p.get("street_read", "")) != "":
		read.append("the street's read: " + String(p.get("street_read", "")))
	var cap := float(p.get("capacity_per_unit", 1.0))
	if b.state.biz_what == "Service" and cap > 0.0:
		var slots := SimWorks.service_capacity(b.state)
		if slots > 0.0:
			read.append("one %s takes ≈ %.1f hours of hands — today's crew serves ≈ %d/wk before hiring" % [
				String(p.get("unit", "unit")).trim_prefix("per "), cap, int(slots / cap)])
	read.append("arrives unpriced — it bills at the going rate ≈ $%s until you price it" % b.fmt(int(round(fair))))
	return read

## Elasticity in words: how hard demand punishes a price above the going rate.
## A raw engine float never prints (10-interface-language §3.8).
static func _elasticity_word(e: float) -> String:
	if e >= 2.4:
		return "steep"
	if e >= 1.5:
		return "typical"
	return "gentle"

## The proposal, in the shape the whole desk already reads. The street answers in
## the LLM's own vocabulary (`variable_costs` / `fixed_costs_wk`) and the keyless
## draft answers in exactly the same one, so there is ONE road in and one shape
## afterwards — an offer dict, synced, priced at nothing.
static func _proposal(state: GameState, terms: Dictionary) -> Dictionary:
	var p := {
		"name": String(terms.get("name", "an offer")).substr(0, 40),
		"unit": String(terms.get("unit", "per order")),
		"fair_price": clampf(float(terms.get("fair_price", 1.0)), 1.0, 50_000.0),
		"elasticity": clampf(float(terms.get("elasticity", 2.0)), 0.5, 3.0),
		"weight": clampf(float(terms.get("weight", 1.0)), SimCatalog.MIN_WEIGHT,
			minf(SimCatalog.MAX_WEIGHT, maxf(SimCatalog.weight_room(state), SimCatalog.MIN_WEIGHT))),
		"unit_cost": float(terms.get("unit_cost", 0.0)),
		"price": 0.0,
		"desc": String(terms.get("desc", "")).substr(0, 110),
		"street_read": String(terms.get("street_read", "")).substr(0, 140),
		"capacity_per_unit": clampf(float(terms.get("capacity_per_unit", 1.0)), 0.1, 40.0),
	}
	var cl: Array = SimCatalog.sanitize_lines(terms.get("variable_costs", []),
		SimCatalog.MAX_COST_LINES)
	# A MODEL THAT ANSWERED WITH A LUMP SUM still gets a receipt: one honest line
	# beats a total nobody can argue with.
	if cl.is_empty() and float(p["unit_cost"]) > 0.0:
		cl = [{"label": "cost to serve", "amount": float(p["unit_cost"])}]
	if not cl.is_empty():
		p["cost_lines"] = cl
	var fl: Array = SimCatalog.sanitize_lines(terms.get("fixed_costs_wk", []),
		SimCatalog.MAX_FIXED_LINES)
	if not fl.is_empty():
		p["fixed_lines"] = fl
	SimEngine.sync_offer_costs(p)
	return p

# ───────────────────────────── ladders and presses ───────────────────────────

## The price ladder: off sale, then the fair-price multiples. Duplicates are
## dropped so no press is ever a dead press on a cheap offer.
static func price_steps(o: Dictionary) -> Array:
	var fair := maxf(float(o.get("fair_price", 10.0)), 1.0)
	var steps: Array = [0.0]
	for m in PRICE_MULTS:
		steps.append(maxf(round(fair * float(m)), 1.0))
	return _dedupe(steps)

## The variable-line ladder, relative to what the street charges — a cost line is
## only ever meaningful as a share of the price it eats.
static func var_steps(fair: float) -> Array:
	var steps: Array = []
	for m in VAR_MULTS:
		steps.append(round(maxf(fair, 1.0) * float(m)))
	return _dedupe(steps)

static func _dedupe(a: Array) -> Array:
	var out: Array = []
	for v in a:
		if out.is_empty() or absf(float(out[out.size() - 1]) - float(v)) > 0.001:
			out.append(float(v))
	return out

## price steps: the founder's choice, $0 included (a conscious giveaway).
static func price_step(b, oi: int, dir: int) -> void:
	var o: Dictionary = b.state.offers[oi]
	o["price"] = DeskKit.ladder(price_steps(o), float(o.get("price", 0.0)), dir)
	o["price_set"] = true

## The row-inline stepper glyph. The kit's own glyph belongs to its 78px stepper
## row; a 62px list row cannot host one, so the two live side by side — same
## 52×46 target, same dim-at-bound law, same coral hover.
static func _step_btn(b, text: String, pos: Vector2, dead: bool, on_press: Callable) -> void:
	var btn := Button.new()
	btn.text = text
	btn.position = pos
	btn.size = DeskKit.BTN
	b.ink_btn(btn)
	if dead:
		btn.disabled = true
		for k in ["font_color", "font_hover_color", "font_disabled_color"]:
			btn.add_theme_color_override(k, Color(DeskKit.INK, 0.35))
	else:
		btn.pressed.connect(func() -> void:
			b.desk.erase("armed")   # any other control disarms the armed one
			on_press.call()
			b.refresh())
	b.pane().add_child(btn)
