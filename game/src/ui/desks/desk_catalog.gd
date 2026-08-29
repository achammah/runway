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

## THE FINE PRINT (coworking+): what one sale costs, line by line, and what the
## week costs whether or not anything sells. The garage gets the same two totals
## on one stepper each — the LINES ARE STILL THE STORED TRUTH underneath, so
## nothing is lost when coworking reveals them (01 §5).
static func _cost_groups(b, y: float, o: Dictionary, era: int, lc: float,
		fm: float = 1.0) -> float:
	var fair := maxf(float(o.get("fair_price", 1.0)), 1.0)
	var totals := era < 1
	b.label("what one sale costs — variable" if not totals else "what one sale costs to serve",
		Vector2(DeskKit.X_ID, y), DeskKit.DETAIL, Color(DeskKit.INK, 0.55), 900.0)
	y += 30.0
	if totals:
		var cur := float(o.get("unit_cost", 0.0))
		var vsteps := var_steps(fair)
		y = DeskKit.stepper(b, y, {
			"name": "serve cost", "value": "$%s/unit" % b.fmt(int(round(cur))),
			"effect": "%d%% of fair" % int(round(cur / fair * 100.0)),
			"x_value": DeskKit.X_VALUE, "pitch": 56.0,
			"at_min": DeskKit.at_min(vsteps, cur), "at_max": DeskKit.at_max(vsteps, cur),
			"on_minus": func() -> void: scale_variable(o, -1),
			"on_plus": func() -> void: scale_variable(o, 1)})
	else:
		var lines: Array = o.get("cost_lines", [])
		if lines.is_empty():
			b.label("this one arrived as a single number — no itemised receipts behind it",
				Vector2(40.0, y), DeskKit.DETAIL, Color(DeskKit.INK, 0.45), 900.0)
			y += 30.0
		for li in lines.size():
			y = _line_row(b, y, o, lines[li], fair, lc, true, fm)
	y = _sum_line(b, y, "= variable cost $%s/unit · served at ×%.2f today" % [
		b.fmt(int(round(float(o.get("unit_cost", 0.0))))), lc], DeskKit.BLUE)
	b.label("standing costs — every week, sold or not", Vector2(DeskKit.X_ID, y),
		DeskKit.DETAIL, Color(DeskKit.INK, 0.55), 900.0)
	y += 30.0
	if totals:
		var curf := float(o.get("fixed_wk", 0.0))
		y = DeskKit.stepper(b, y, {
			"name": "tools", "value": "$%s/wk" % b.fmt(int(round(curf))),
			"effect": "billed sold or not",
			"x_value": DeskKit.X_VALUE, "pitch": 56.0,
			"at_min": DeskKit.at_min(FIXED_STEPS, curf), "at_max": DeskKit.at_max(FIXED_STEPS, curf),
			"on_minus": func() -> void: scale_fixed(o, -1),
			"on_plus": func() -> void: scale_fixed(o, 1)})
	else:
		var flines: Array = o.get("fixed_lines", [])
		if flines.is_empty():
			b.label("nothing standing — this one costs nothing in a week it sells nothing",
				Vector2(40.0, y), DeskKit.DETAIL, Color(DeskKit.INK, 0.45), 900.0)
			y += 30.0
		for fi in flines.size():
			y = _line_row(b, y, o, flines[fi], fair, lc, false, fm)
	# THE LESSON LINE: break-even, or the one mistake a founder must not miss.
	var be := SimCatalog.break_even(o, lc, fm)
	if be < 0:
		return _sum_line(b, y, "= $%s/wk · this price never pays for itself — every sale loses $%s" % [
			b.fmt(int(round(float(o.get("fixed_wk", 0.0))))),
			b.fmt(int(round(-SimCatalog.contribution(o, lc, fm))))], DeskKit.PEN)
	return _sum_line(b, y, "= $%s/wk · break-even: %d sales/wk pay for it" % [
		b.fmt(int(round(float(o.get("fixed_wk", 0.0))))), be], DeskKit.BLUE)

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
	b.label("costs only fall when the team rebuilds how this one is made — a cost sprint below",
		Vector2(DeskKit.X_ID, y), DeskKit.LAW, Color(DeskKit.INK, 0.45), 1080.0)
	return y + 26.0

## THE BLUE LINE DOES THE ARITHMETIC OUT LOUD — the patient accountant.
static func _sum_line(b, y: float, text: String, col: Color) -> float:
	b.label(text, Vector2(DeskKit.X_ID + 18.0, y), 22, col, 1080.0)
	return y + maxf(b.wrap_h(text, 22, 1080.0), 26.0) + 6.0

## One cost line on its own stepper. `variable` walks the fair-relative ladder,
## fixed walks absolute dollars; every press re-syncs, so the totals can never
## drift from the receipts that explain them.
static func _line_row(b, y: float, o: Dictionary, line, fair: float, lc: float,
		variable: bool, fm: float = 1.0) -> float:
	var ld: Dictionary = line
	var amount := float(ld.get("amount", 0.0))
	var steps := var_steps(fair) if variable else FIXED_STEPS
	var effect := "%d%% of fair" % int(round(amount / fair * 100.0))
	if not variable:
		var margin := SimCatalog.contribution(o, lc, fm)
		effect = ("%d sales/wk pays it" % int(ceil(amount / margin))) if margin > 0.0 \
			else "no margin to pay it"
	return DeskKit.stepper(b, y, {
		"name": String(ld.get("label", "line")),
		"value": ("$%s" % b.fmt(int(round(amount)))) if variable else ("$%s/wk" % b.fmt(int(round(amount)))),
		"effect": effect, "x_value": DeskKit.X_VALUE, "pitch": 46.0,
		"at_min": DeskKit.at_min(steps, amount), "at_max": DeskKit.at_max(steps, amount),
		"on_minus": func() -> void:
			ld["amount"] = DeskKit.ladder(steps, float(ld.get("amount", 0.0)), -1)
			SimEngine.sync_offer_costs(o),
		"on_plus": func() -> void:
			ld["amount"] = DeskKit.ladder(steps, float(ld.get("amount", 0.0)), 1)
			SimEngine.sync_offer_costs(o)})

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

static func _write(b) -> void:
	DeskKit.title(b, "what do you want to sell?")
	b.label("plain words — \"a monthly meal-prep box\", \"API access for clinics\", \"a two-hour audit\"",
		Vector2(DeskKit.X_ID, 62.0), 22, Color(DeskKit.INK, 0.55), 1100.0)
	var te := TextEdit.new()
	te.position = Vector2(DeskKit.X_ID, 96.0)
	te.size = Vector2(1140, 150)
	te.add_theme_font_override("font", b.font())
	te.add_theme_font_size_override("font_size", 28)
	te.add_theme_color_override("font_color", DeskKit.INK)
	te.add_theme_color_override("font_placeholder_color", Color(DeskKit.INK, 0.30))
	te.add_theme_color_override("caret_color", DeskKit.PEN)
	# THE PAPER IS THE FIELD: no box, no fill, no chrome — the rule underneath is
	# the only thing that says "write here".
	for stn in ["normal", "focus", "read_only"]:
		te.add_theme_stylebox_override(stn, StyleBoxEmpty.new())
	te.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
	te.placeholder_text = "write it the way you would say it out loud…"
	te.text = String(b.desk.get("text", ""))
	b.pane().add_child(te)
	DeskKit.rule(b, 250.0)
	# the founder's words survive a rebuild; the proposal never does
	te.text_changed.connect(func() -> void: b.desk["text"] = te.text)
	# ENTER SUBMITS, SHIFT+ENTER NEWLINES (the journal's `_wire_free` contract)
	te.gui_input.connect(func(ev: InputEvent) -> void:
		if ev is InputEventKey and ev.pressed and ev.keycode == KEY_ENTER \
				and not ev.shift_pressed:
			te.accept_event()
			b.desk["text"] = te.text
			_submit(b))
	te.grab_focus()
	te.set_caret_line(te.get_line_count() - 1)
	te.set_caret_column(te.get_line(te.get_line_count() - 1).length())
	# THE SUBMIT IS ALWAYS LIVE. A field that rebuilt the pane on every keystroke
	# would take the keyboard away mid-word, so the button cannot appear when the
	# text gets long enough — it presses, and a press with nothing behind it
	# ANSWERS instead of doing nothing.
	DeskKit.word(b, "price it", Vector2(DeskKit.X_ID, 280.0), func() -> void:
		_submit(b), DeskKit.ROW, DeskKit.INK, 220.0)
	DeskKit.word(b, "never mind", Vector2(260.0, 280.0), func() -> void:
		b.desk["mode"] = ""
		b.desk.erase("text"), DeskKit.ROW, Color(DeskKit.INK, 0.7), 200.0)
	if bool(b.desk.get("short", false)):
		b.label("a few words at least — the street can't price a shrug",
			Vector2(DeskKit.X_ID, 340.0), DeskKit.STATUS, DeskKit.PEN, 700.0)
	DeskKit.footer(b, {"rules": "whatever comes back is a PROPOSAL — you adjust every cost line before it reaches the books"})

## The one road out of WRITE. Keyed: the street prices it and the reply lands on
## the review card. Keyless: the house numbers arrive instantly and the card is
## identical, with one dry footnote (01 §8.4).
static func _submit(b) -> void:
	var idea := String(b.desk.get("text", "")).strip_edges()
	if idea.length() < 3:
		b.desk["short"] = true
		b.refresh()      # Enter has no rebuild of its own; the answer must still land
		return
	b.desk.erase("short")
	if _street_is_reachable(b):
		b.desk["mode"] = "wait"
		b.generator.price_offer_idea(b.state, idea, func(res: Dictionary) -> void:
			_land(b, idea, res))
		b.refresh()
		return
	b.desk["pending"] = _proposal(b.state, SimCatalog.draft_terms(b.state, idea))
	b.desk["house"] = true
	b.desk["mode"] = "review"
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
		"banner": "the street's terms — adjust the lines, then shelve it",
		"read": [
			"%s · %s — the street charges ≈ $%s · elasticity %s · weight %.1f" % [
				String(p.get("name", "an offer")).to_upper(), String(p.get("unit", "")),
				b.fmt(int(round(fair))), _elasticity_word(float(p.get("elasticity", 2.0))),
				float(p.get("weight", 1.0))],
			"arrives unpriced — it bills at the going rate ≈ $%s until you price it" % b.fmt(int(round(fair)))],
		"groups": groups,
		"verdict": "" if SimCatalog.break_even(p, lc) >= 0 else
			"this price never pays for itself — every sale loses $%s" % b.fmt(int(round(-SimCatalog.contribution(p, lc)))),
		"note": DeskKit.HOUSE_NOTE if bool(b.desk.get("house", false)) else "",
		"refused": String(b.desk.get("refused", "")),
		"confirm": "put it on the shelf", "cancel": "tear it up",
		"on_confirm": func() -> void: _shelve(b, p),
		"on_cancel": func() -> void:
			b.desk["mode"] = ""
			b.desk.erase("text")})

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
	b.desk.erase("text")

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
		out.append({
			"name": String(ld.get("label", "line")),
			"value": ("$%s" % b.fmt(int(round(amount)))) if variable else ("$%s/wk" % b.fmt(int(round(amount)))),
			"effect": effect, "pitch": 46.0,
			"at_min": DeskKit.at_min(steps, amount), "at_max": DeskKit.at_max(steps, amount),
			"on_minus": func() -> void:
				ld["amount"] = DeskKit.ladder(steps, float(ld.get("amount", 0.0)), -1)
				SimEngine.sync_offer_costs(p),
			"on_plus": func() -> void:
				ld["amount"] = DeskKit.ladder(steps, float(ld.get("amount", 0.0)), 1)
				SimEngine.sync_offer_costs(p)})
	return out

## GARAGE TOTALS MODE (01 §5): one stepper for the whole variable sheet, one for
## the whole standing sheet. The lines behind them are scaled proportionally and
## kept, so nothing the street itemised is lost when coworking reveals it.
static func _review_total(p: Dictionary, nm: String, value: String, effect: String,
		steps: Array, cur: float, variable: bool) -> Dictionary:
	return {"name": nm, "value": value, "effect": effect, "pitch": 52.0,
		"at_min": DeskKit.at_min(steps, cur), "at_max": DeskKit.at_max(steps, cur),
		"on_minus": func() -> void: _scale_total(p, variable, -1),
		"on_plus": func() -> void: _scale_total(p, variable, 1)}

static func _scale_total(o: Dictionary, variable: bool, dir: int) -> void:
	if variable:
		scale_variable(o, dir)
	else:
		scale_fixed(o, dir)

static func _review_fixed_sum(b, p: Dictionary, lc: float) -> String:
	var be := SimCatalog.break_even(p, lc)
	if be < 0:
		return "= $%s/wk · nothing here pays for it yet" % b.fmt(int(round(float(p.get("fixed_wk", 0.0)))))
	return "= $%s/wk · break-even: %d sales/wk pay for it" % [
		b.fmt(int(round(float(p.get("fixed_wk", 0.0))))), be]

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

## Step the VARIABLE TOTAL and let the lines follow proportionally (garage totals
## mode). A sheet with no lines yet gets the total as its one line, so the next
## era still has receipts to reveal.
static func scale_variable(o: Dictionary, dir: int) -> void:
	var fair := maxf(float(o.get("fair_price", 1.0)), 1.0)
	var cur := float(o.get("unit_cost", 0.0))
	var target := DeskKit.ladder(var_steps(fair), cur, dir)
	var lines: Array = o.get("cost_lines", [])
	if lines.is_empty():
		o["cost_lines"] = [{"label": "cost to serve", "amount": target}]
	elif cur <= 0.001:
		var each := target / float(lines.size())
		for l in lines:
			(l as Dictionary)["amount"] = each
	else:
		var k := target / cur
		for l2 in lines:
			var ld: Dictionary = l2
			ld["amount"] = float(ld.get("amount", 0.0)) * k
	SimEngine.sync_offer_costs(o)

## The same move on the standing sheet.
static func scale_fixed(o: Dictionary, dir: int) -> void:
	var cur := float(o.get("fixed_wk", 0.0))
	var target := DeskKit.ladder(FIXED_STEPS, cur, dir)
	var lines: Array = o.get("fixed_lines", [])
	if lines.is_empty():
		o["fixed_lines"] = [{"label": "tools & subscriptions", "amount": target}]
	elif cur <= 0.001:
		var each := target / float(lines.size())
		for l in lines:
			(l as Dictionary)["amount"] = each
	else:
		var k := target / cur
		for l2 in lines:
			var ld: Dictionary = l2
			ld["amount"] = float(ld.get("amount", 0.0)) * k
	SimEngine.sync_offer_costs(o)

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
