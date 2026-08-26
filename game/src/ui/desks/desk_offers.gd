class_name DeskOffers
extends RefCounted
## DESK — REVENUE · "offers" = THE RATE CARD (DECISIONS: owner pick D).
## THE QUESTION THIS DESK ANSWERS: "what do we sell and what does each sale earn?"
##
## One columned card of what we sell: a row per offer, a column per truth —
## price big, street, serve, margin, the demand verdict as ONE colored word —
## with the two SEPARATE −/+ squares in a dedicated ADJUST column (the stepper
## law) and ▸ opening the shipped five-state detail machine unchanged.
##
## THE MACHINE UNDERNEATH IS DeskCatalog's: detail / write / wait / review are
## delegated whole — the DEFINE-AN-OFFER door is the same write->wait->review
## road (the DM prices its tools via the cost-lines flow; nothing an LLM wrote
## enters the books unreviewed), and the drop arm lives on the detail sheet
## behind its two-tap. This file owns only the LIST — the rate card itself.
##
## AUDIENCE VARIANTS (12-binder-rework-2 §Retrofits): Consumer rows carry
## units/wk under the name (few offers at volume); Enterprise adds the
## named-account per-seat line under the table, read from the pipeline.
## Fair-price backstop and the "!" unpriced warning are preserved: an unpriced
## offer bills at the street's rate and says so in the pen.

const QUESTION := "what do we sell and what does each sale earn?"

## The rate card's own column grammar (inside the card at x10 w1120, pad 18):
## identity -> price -> street -> serve -> margin -> verdict -> ▸ -> ADJUST.
const COL_NAME_X := 28.0
const COL_NAME_W := 300.0
const COL_PRICE_X := 340.0
const COL_PRICE_W := 130.0
const COL_STREET_X := 480.0
const COL_STREET_W := 105.0
const COL_SERVE_X := 595.0
const COL_SERVE_W := 100.0
const COL_MARGIN_X := 705.0
const COL_MARGIN_W := 115.0
const COL_VERDICT_X := 836.0
const COL_VERDICT_W := 150.0
const COL_EXPAND_X := 986.0
const COL_ADJUST_X := 1028.0
const ROW_H := 52.0
## Six rows face-up, then the fold (collapse law); the shelf itself caps at 8.
const LIST_SHOW := 6

# ─────────────────────────────── the dispatch ────────────────────────────────

## The group overview's card reads this: the page's hero VERBATIM (DECISIONS:
## the quartet card IS the page's hero — one number + one sentence).
static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	var h := _hero(s)
	return {"big": String(h.get("big", "")), "line": String(h.get("line", ""))}

static func draw(b) -> void:
	var mode := String(b.desk.get("mode", ""))
	match mode:
		"detail", "write", "wait", "review":
			# the shipped five-state machine, whole — one writer, one road
			DeskCatalog.draw(b)
		"all":
			_all_offers(b)
		_:
			# Esc walked out of a sub-state: a proposal no longer on screen is a
			# proposal that no longer exists (DeskCatalog's own contract).
			b.desk.erase("pending")
			b.desk.erase("house")
			b.desk.erase("refused")
			b.desk.erase("short")
			_rate_card(b)

static func handle(_b, _id: String) -> void:
	pass

# ─────────────────────────────── the hero ────────────────────────────────────

## What one customer's week earns, across the shelf — the tab's answer.
static func _hero(s: GameState) -> Dictionary:
	var arpu := SimEngine.offers_arpu(s)
	if s.offers.is_empty():
		return {"big": "nothing on the shelf", "arpu": -1.0, "cogs": 0.0,
			"line": "a company with nothing to sell earns nothing — write down what you sell"}
	if arpu < 0.0:
		return {"big": "%d offers, unpriced" % s.offers.size(), "arpu": -1.0, "cogs": 0.0,
			"line": "the shelf bills at the street's rate until you set your own prices"}
	var cogs := SimEngine.offers_cogs_per_customer(s)
	var offers_word := "one offer" if s.offers.size() == 1 else "%d offers" % s.offers.size()
	# at 0 customers the unit story is a PROMISE, not money — say so first
	# (owner: "$23 in but 0 customers" read as real income)
	if s.traction <= 0:
		return {
			"big": "$0/wk — nobody pays yet",
			"line": "one customer's week would earn $%s in · $%s out -> $%s, across %s" % [
				String.num_int64(int(round(arpu))), String.num_int64(int(round(cogs))),
				String.num_int64(int(round(arpu - cogs))), offers_word],
			"arpu": arpu, "cogs": cogs,
		}
	return {
		"big": "$%s in · $%s out -> $%s" % [String.num_int64(int(round(arpu))),
			String.num_int64(int(round(cogs))), String.num_int64(int(round(arpu - cogs)))],
		"line": "what one customer's week earns you, across %s on the shelf" % offers_word,
		"arpu": arpu, "cogs": cogs,
	}

# ─────────────────────────────── the rate card ───────────────────────────────

## A right-aligned cell — every dollar in a column ends on one line (Law 2).
static func _right(b, text: String, pos: Vector2, sz: int, col: Color, w: float) -> void:
	var l: Label = b.label(text, pos, sz, col, w)
	l.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT

static func _rate_card(b) -> void:
	var s: GameState = b.state
	var h := _hero(s)
	var y := DeskKit.hero_band(b, String(h.get("big", "")), String(h.get("line", "")))
	if s.offers.is_empty():
		y = DeskKit.empty(b, Vector2(DeskKit.X_ID, y),
			"the world hasn't defined your offers yet — they arrive with the bible.",
			"a company with nothing on the shelf earns nothing: write down what you sell.")
		_define_door(b, y + 12.0)
		_foot(b, s)
		return
	# the per-customer two-bar: pays against serve — the shape before the digits
	var arpu := float(h.get("arpu", -1.0))
	if arpu >= 0.0:
		var cogs := float(h.get("cogs", 0.0))
		y = DeskKit.twobar(b, DeskKit.X_ID, y, 700.0,
			"pays", "$%s" % b.fmt(int(round(arpu))), [arpu],
			"serve", "$%s" % b.fmt(int(round(cogs))), [maxf(cogs, 0.0)])
		y += 4.0
	var lc := SimEngine.learning_curve(s)
	var fm := SimEngine.street_fair_mult(s)
	# a rival's war moves THE STREET'S reference — every verdict below says so
	var war := int(round((1.0 - fm) * 100.0))
	if war > 0:
		b.label("price war: the street's reference is %d%% down — the same price reads dearer this week" % war,
			Vector2(DeskKit.X_ID, y), DeskKit.LAW, DeskKit.PEN, 1100.0)
		y += 28.0
	var order := _visible_rows(s, lc)
	var shown: Array = order[0]
	var folded := int(order[1])
	var card_h := DeskKit.CARD_HEAD + 30.0 + float(shown.size()) * ROW_H + 10.0
	var frame := DeskKit.card_frame(b, 10.0, y, 1120.0, card_h, "the rate card")
	var cy := float(frame.get("content_y", y)) + 0.0
	cy = _head_row(b, cy)
	for i in shown:
		cy = _row(b, cy, int(i), s, lc, fm)
	y = float(frame.get("bottom", cy)) + 10.0
	if folded > 0:
		y = DeskKit.fold_row(b, DeskKit.X_ID, y, folded, "offers, healthy", func() -> void:
			b.desk["mode"] = "all")
	if String(s.biz_who) == "Enterprise":
		y = _named_accounts_line(b, s, y)
	_define_door(b, y + 6.0)
	_foot(b, s)

## THE COLLAPSE LADDER: six rows face-up, and the ones closest to money are
## never the hidden ones — unpriced or losing offers are promoted into view;
## the healthy crowd folds to "the other N".
static func _visible_rows(s: GameState, lc: float) -> Array:
	var n := s.offers.size()
	if n <= LIST_SHOW:
		var all: Array = []
		for i in n:
			all.append(i)
		return [all, 0]
	var hot: Array = []
	var calm: Array = []
	for i in n:
		var o: Dictionary = s.offers[i]
		var unpriced := float(o.get("price", 0.0)) <= 0.0 and not bool(o.get("price_set", false))
		if unpriced or SimCatalog.never_pays(o, lc):
			hot.append(i)
		else:
			calm.append(i)
	var shown := hot.duplicate()
	for i2 in calm:
		if shown.size() >= LIST_SHOW:
			break
		shown.append(i2)
	shown.sort()
	return [shown, n - shown.size()]

## The small-caps header band — a column per truth, the unit said once.
static func _head_row(b, y: float) -> float:
	var dim := Color(DeskKit.INK, 0.42)
	b.label("OFFER", Vector2(COL_NAME_X, y), 18, dim, COL_NAME_W)
	_right(b, "PRICE", Vector2(COL_PRICE_X, y), 18, dim, COL_PRICE_W)
	_right(b, "STREET", Vector2(COL_STREET_X, y), 18, dim, COL_STREET_W)
	_right(b, "SERVE", Vector2(COL_SERVE_X, y), 18, dim, COL_SERVE_W)
	_right(b, "MARGIN", Vector2(COL_MARGIN_X, y), 18, dim, COL_MARGIN_W)
	b.label("DEMAND", Vector2(COL_VERDICT_X, y), 18, dim, COL_VERDICT_W)
	_right(b, "ADJUST", Vector2(COL_ADJUST_X - 8.0, y), 18, dim, 80.0)
	return DeskKit.pen_rule(b, y + 24.0, COL_NAME_X, 1120.0 - 36.0, Color(DeskKit.INK, 0.25), 7) + 2.0

## One offer, one row, one column per truth.
static func _row(b, y: float, i: int, s: GameState, lc: float, fm: float) -> float:
	var o: Dictionary = s.offers[i]
	b.label(String(o.get("name", "?")).to_upper(), Vector2(COL_NAME_X, y), 24, DeskKit.INK, COL_NAME_W)
	var sub := String(o.get("unit", ""))
	if String(s.biz_who) == "Consumer":
		# Consumer runs price few offers at volume — the row carries units/wk
		var units := float(s.traction) * float(o.get("weight", 1.0)) \
			* SimEngine.offer_cadence(String(o.get("unit", "")))
		sub += " · ≈%d/wk" % int(round(units))
	b.label(sub, Vector2(COL_NAME_X, y + 27.0), 15, Color(DeskKit.INK, 0.45), COL_NAME_W)
	var price := float(o.get("price", 0.0))
	var fair := float(o.get("fair_price", 0.0)) * fm
	var margin := SimCatalog.contribution(o, lc, fm)
	var vd := _verdict_word(s, o, price, fm, lc)
	if price > 0.0:
		_right(b, "$%s" % b.fmt(int(round(price))), Vector2(COL_PRICE_X, y), 26, DeskKit.INK, COL_PRICE_W)
	elif bool(o.get("price_set", false)):
		_right(b, "$0", Vector2(COL_PRICE_X, y), 26, DeskKit.BLUE, COL_PRICE_W)
	else:
		# THE FAIR-PRICE BACKSTOP, preserved: unpriced bills at the going rate
		_right(b, "! $%s" % b.fmt(int(round(fair))), Vector2(COL_PRICE_X, y), 26, DeskKit.PEN, COL_PRICE_W)
	_right(b, "$%s" % b.fmt(int(round(fair))), Vector2(COL_STREET_X, y + 3.0), 21, Color(DeskKit.INK, 0.6), COL_STREET_W)
	_right(b, "$%s" % b.fmt(int(round(SimCatalog.served_unit_cost(o, lc)))),
		Vector2(COL_SERVE_X, y + 3.0), 21, Color(DeskKit.INK, 0.6), COL_SERVE_W)
	_right(b, "$%s" % b.fmt(int(round(margin))), Vector2(COL_MARGIN_X, y + 1.0), 23,
		Color("5D7A50") if margin > 0.0 else DeskKit.PEN, COL_MARGIN_W)
	b.label(String(vd.get("word", "")), Vector2(COL_VERDICT_X, y + 2.0), 22,
		vd.get("col", DeskKit.INK), COL_VERDICT_W)
	DeskKit.expand(b, Vector2(COL_EXPAND_X, y - 4.0), func() -> void:
		b.desk["mode"] = "detail"
		b.desk["row"] = i)
	var steps := DeskCatalog.price_steps(o)
	DeskKit.adjust_pair(b, COL_ADJUST_X, y + 4.0,
		func() -> void: DeskCatalog.price_step(b, i, -1),
		func() -> void: DeskCatalog.price_step(b, i, 1),
		DeskKit.at_min(steps, price), DeskKit.at_max(steps, price))
	DeskKit.pen_rule(b, y + ROW_H - 8.0, COL_NAME_X, 1120.0 - 36.0, Color(DeskKit.INK, 0.12), 11 + i)
	return y + ROW_H

## THE DEMAND VERDICT — one colored word, the heat ramp, never a sentence.
static func _verdict_word(s: GameState, o: Dictionary, price: float, fm: float, lc: float) -> Dictionary:
	if price <= 0.0 and bool(o.get("price_set", false)):
		return {"word": "free on purpose", "col": DeskKit.BLUE}
	if price <= 0.0:
		return {"word": "unpriced", "col": DeskKit.PEN}
	if SimCatalog.never_pays(o, lc):
		return {"word": "loses money", "col": DeskKit.PEN}
	var dem := SimEngine.offer_demand(o, price, fm)
	if dem >= 1.15:
		return {"word": "a deal", "col": DeskKit.SAGE}
	if dem <= 0.25:
		return {"word": "absurd", "col": DeskKit.PEN}
	if dem < 0.85:
		return {"word": "pricey", "col": DeskKit.YELL}
	return {"word": "fair", "col": Color(DeskKit.INK, 0.8)}

## ENTERPRISE RETROFIT: the per-seat + named-account line, fed by the pipeline.
static func _named_accounts_line(b, s: GameState, y: float) -> float:
	var seats := 0
	for lg in s.logos:
		seats += int((lg as Dictionary).get("seats", 0))
	var text := ""
	if s.logos.is_empty():
		text = "named accounts: none signed yet — a contract is the first discount conversation"
	else:
		text = "named accounts: %d logos · %d seats · a seat bills ≈ $%s/wk — discounts live on the signed contracts" % [
			s.logos.size(), seats, b.fmt(int(round(SimPipeline.unit_rev_wk(s))))]
	b.label(text, Vector2(DeskKit.X_ID, y), DeskKit.LAW, Color(DeskKit.INK, 0.6), 1100.0)
	return y + 30.0

## THE DEFINE-AN-OFFER DOOR, or the honest reason it is shut. The road behind
## it is the shipped write->wait->review cost-lines flow (the mutation law's
## receipt: the DM prices its tools, you sign or tear it up).
static func _define_door(b, y: float) -> void:
	var shut := SimCatalog.shelf_full_line(b.state)
	if shut != "":
		b.label(shut, Vector2(DeskKit.X_ID, y + 8.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.5), 900.0)
		return
	DeskKit.word(b, "+ define a new offer", Vector2(DeskKit.X_ID, y), func() -> void:
		b.desk["mode"] = "write", DeskKit.STATUS, DeskKit.INK, 340.0)

## The teaching foot. WARNINGS OUTRANK WISDOM: an offer that loses money on
## every sale outranks the rules line; the drop's migration law rides the rules.
static func _foot(b, s: GameState) -> void:
	var lc := SimEngine.learning_curve(s)
	var fm := SimEngine.street_fair_mult(s)
	var computed := ""
	var arpu := SimEngine.offers_arpu(s)
	if arpu >= 0.0:
		var cpc := SimEngine.offers_cogs_per_customer(s)
		computed = "unit economics: ≈ $%.1f ARPU − $%.1f COGS = $%.1f contribution per customer per week -> ≈ $%s/wk at %d customers" % [
			arpu, cpc, arpu - cpc, b.fmt(int(round((arpu - cpc) * float(s.traction)))), s.traction]
	var warning := ""
	for o in s.offers:
		var od: Dictionary = o
		if SimCatalog.never_pays(od, lc):
			warning = "'%s' never pays for itself — every sale loses $%s" % [
				String(od.get("name", "an offer")),
				b.fmt(int(round(-SimCatalog.contribution(od, lc, fm))))]
			break
	DeskKit.footer(b, {
		"computed": computed,
		"rules": "price at the street's level and demand is fair · dropping an offer (open it, then drop) migrates its customers to the shelf — or churns them",
		"warning": warning,
		"y": 806.0, "rules_y": 840.0,
	})

# ─────────────────────────── the unfolded shelf ──────────────────────────────

## The fold opened: every offer on one sheet, no hero — the crowd, honestly.
static func _all_offers(b) -> void:
	var s: GameState = b.state
	DeskKit.back(b, "back to the rate card", func() -> void:
		b.desk["mode"] = "")
	var lc := SimEngine.learning_curve(s)
	var fm := SimEngine.street_fair_mult(s)
	var n := s.offers.size()
	var card_h := DeskKit.CARD_HEAD + 30.0 + float(n) * ROW_H + 10.0
	var frame := DeskKit.card_frame(b, 10.0, 64.0, 1120.0, card_h, "the whole shelf — %d offers" % n)
	var cy := float(frame.get("content_y", 64.0))
	cy = _head_row(b, cy)
	for i in n:
		cy = _row(b, cy, i, s, lc, fm)
	_foot(b, s)
