class_name DeskOffers
extends RefCounted
## DESK — REVENUE · "offers" = THE RATE CARD (DECISIONS: owner pick D).
## THE QUESTION THIS DESK ANSWERS: "what do we sell and what does each sale earn?"
##
## One columned card of what we sell: a row per offer, a column per truth —
## price big, street, serve, margin, the demand verdict as ONE colored word —
## with the two SEPARATE −/+ squares in a dedicated ADJUST column (the stepper
## law) and ▸ UNWRAPPING the row in place: the open offer's whole ledger —
## price dial, the world's stated costs, the cost sprint, weight, the drop —
## draws on this same sheet (owner: all the control here, no separate page).
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
##
## DAG3 (13-binder-ux · offers): the verdict word is a DOOR — press it and the
## street's read opens as a paper card with THE FAIR BAND drawn (the demand
## curve's own thresholds on the price axis, your price dotted); the DO lane
## carries [set price — …] [add an offer]; the pen circles verdicts that moved
## since the last open; the empty shelf is the S1 teaching state.

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
	if mode.begins_with("verdict:"):
		# S4 — the verdict opened: the rate card stays under the paper card,
		# Esc or any press closes the read before anything else (desk-mode pop)
		_rate_card(b)
		_verdict_card(b, b.state, int(mode.substr(8)))
		return
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
	if s.offers.is_empty():
		# S1 — the empty shelf is a TEACHING page, never bare furniture: the
		# promise, the honest subjunctive, the one door, and when it wakes.
		DeskKit.zero_state(b, {
			"will_show": "what you sell, and what each sale earns",
			"would_line": "a row per offer — your price, the street's rate, the cost to serve, "
				+ "the margin, and one word saying how demand reads it",
			"action_label": "+ define a new offer",
			"action_cb": func() -> void: b.desk["mode"] = "write",
			"wakes_hint": "the shelf fills with the bible — an unpriced offer bills at the "
				+ "street's rate until you set your own",
		})
		return
	var h := _hero(s)
	var y := DeskKit.hero_band(b, String(h.get("big", "")), String(h.get("line", "")))
	# S5 — the hero's arrow: what one customer nets, against the last open
	var arpu0 := float(h.get("arpu", -1.0))
	if arpu0 >= 0.0:
		var net := int(round(arpu0 - float(h.get("cogs", 0.0))))
		var prev: String = b.seen_prev("offers", "hero")
		if b.seen("offers", "hero", str(net)) and prev.is_valid_float():
			var bw: float = b.font().get_string_size(String(h.get("big", "")),
				HORIZONTAL_ALIGNMENT_LEFT, -1, DeskKit.HERO_BIG).x
			DeskKit.delta_arrow(b, DeskKit.X_ID + bw + 14.0, 26.0, float(net), float(prev))
	# S2 — red speaks ON the page: the pricing asks in one measured line.
	# R5 — the strip renders in its own slot (96-118); content holds its
	# position whether or not the desk is red — stability beats density.
	DeskKit.ask_strip(b, "offers", DeskKit.X_ID, y, 1100.0, "set the price below")
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
	# THE OPEN OFFER'S CARD (owner: the loose stack read as clutter — the
	# shelf stays whole for context, and the open offer's whole ledger lives
	# in ONE framed card below it, in the kit's own grammar).
	var open_i := int(b.desk.get("open_row", -1))
	var open_vis := open_i >= 0 and open_i < s.offers.size()
	var rows: Array = shown
	var hidden := folded
	if open_vis and shown.size() > 4:
		# a card below needs air: deep shelves fold past four rows, and the
		# open row is always among the four
		rows = shown.slice(0, 4)
		var present := false
		for r in rows:
			if int(r) == open_i:
				present = true
		if not present:
			rows[3] = open_i
		hidden = folded + shown.size() - 4
	var card_h := DeskKit.CARD_HEAD + 30.0 + float(rows.size()) * ROW_H + 26.0
	var frame := DeskKit.card_frame(b, 10.0, y, 1120.0, card_h, "the rate card")
	var cy := float(frame.get("content_y", y)) + 0.0
	cy = _head_row(b, cy)
	for i in rows:
		cy = _row(b, cy, int(i), s, lc, fm)
	y = float(frame.get("bottom", cy)) + 10.0
	if open_vis:
		y = _open_card(b, y, open_i, s, lc, fm)
	if hidden > 0:
		y = DeskKit.fold_row(b, DeskKit.X_ID, y, hidden, "offers, healthy",
			func() -> void: b.desk["mode"] = "all")
	if String(s.biz_who) == "Enterprise":
		y = _named_accounts_line(b, s, y)
	_define_door(b, y + 6.0)
	# a tall page pushes its own lane and foot down and SCROLLS — never overlaps
	var base := maxf(DeskKit.DO_LANE_Y, y + 52.0)
	_do_lane(b, s, base)
	_fire_spot(b)
	_foot(b, s, base + 44.0)

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

## THE OPEN OFFER'S CARD — the whole ledger in one frame, the kit's grammar:
## the price dial, the world's costs in two stated lines, the labor line, and
## ONE lane holding the sprint and the drop. The row's mark closes it.
static func _open_card(b, y: float, i: int, s: GameState, lc: float, fm: float) -> float:
	var o: Dictionary = s.offers[i]
	var era := s.era_index()
	var capl := clampf(float(o.get("capacity_per_unit", 1.0)), 0.1, 40.0)
	var svc := s.biz_what == "Service"
	var clines: Array = o.get("cost_lines", []) if era >= 1 else []
	var flines: Array = o.get("fixed_lines", []) if era >= 1 else []
	var card_h := 250.0 + (52.0 if era >= 2 else 0.0) + (26.0 if svc else 0.0) \
		+ float(clines.size() + flines.size()) * 24.0 \
		+ (24.0 if not clines.is_empty() or not flines.is_empty() else 0.0)
	var frame := DeskKit.card_frame(b, 10.0, y, 1120.0, card_h,
		"the open offer — %s" % String(o.get("name", "?")), false,
		1120.0 - DeskKit.CARD_PAD * 2.0)
	var cx := float(frame.get("content_x", 28.0))
	var cy := float(frame.get("content_y", y))
	# the founder's one dial, on the kit's own stepper
	var steps := DeskCatalog.price_steps(o)
	var cur := float(o.get("price", 0.0))
	var fair := float(o.get("fair_price", 0.0)) * fm
	cy = DeskKit.stepper(b, cy, {
		"name": "price", "x": cx,
		"why": "the going rate is $%s — you name what you charge" % b.fmt(int(round(fair))),
		"value": ("$%s per unit" % b.fmt(int(round(cur)))) if cur > 0.0
			else "unpriced — bills at $%s" % b.fmt(int(round(fair))),
		"effect": "margin $%s/unit" % b.fmt(int(round(SimCatalog.contribution(o, lc, fm)))),
		"x_value": DeskKit.X_VALUE, "pitch": 64.0,
		"at_min": DeskKit.at_min(steps, cur), "at_max": DeskKit.at_max(steps, cur),
		"on_minus": func() -> void: DeskCatalog.price_step(b, i, -1),
		"on_plus": func() -> void: DeskCatalog.price_step(b, i, 1)})
	# the world's costs, stated line by line — never a dial
	var fair0 := maxf(float(o.get("fair_price", 1.0)), 1.0)
	if not clines.is_empty() or not flines.is_empty():
		b.label("what one sale costs — the world set these when the offer was written:",
			Vector2(cx, cy), DeskKit.DETAIL, Color(DeskKit.INK, 0.5), 1060.0)
		cy += 24.0
		for li in clines:
			var ld: Dictionary = li
			b.label("%s — $%s/unit (%d%% of the going rate)" % [
				String(ld.get("label", "line")), b.fmt(int(round(float(ld.get("amount", 0.0))))),
				int(round(float(ld.get("amount", 0.0)) / fair0 * 100.0))],
				Vector2(cx + 18.0, cy), DeskKit.DETAIL, Color(DeskKit.INK, 0.7), 1040.0)
			cy += 24.0
		for fi in flines:
			var fd: Dictionary = fi
			b.label("%s — $%s/wk, sold or not" % [String(fd.get("label", "line")),
				b.fmt(int(round(float(fd.get("amount", 0.0)))))],
				Vector2(cx + 18.0, cy), DeskKit.DETAIL, Color(DeskKit.INK, 0.7), 1040.0)
			cy += 24.0
	b.label("= serve $%s/unit (×%.2f today) · standing tools $%s/wk" % [
		b.fmt(int(round(SimCatalog.served_unit_cost(o, lc)))), lc,
		b.fmt(int(round(float(o.get("fixed_wk", 0.0)))))],
		Vector2(cx, cy), DeskKit.DETAIL, DeskKit.BLUE, 1060.0)
	cy += 28.0
	var be := SimCatalog.break_even(o, lc, fm)
	b.label(("every sale loses $%s at this price" % b.fmt(int(round(-SimCatalog.contribution(o, lc, fm))))) if be < 0
		else "break-even: %d sales/wk pay the standing costs" % be,
		Vector2(cx, cy), DeskKit.DETAIL,
		DeskKit.PEN if be < 0 else Color(DeskKit.INK, 0.55), 1060.0)
	cy += 28.0
	if svc:
		b.label("one %s = %.1f hours of hands · today's crew: ≈ %d/wk before hiring" % [
			String(o.get("unit", "unit")).trim_prefix("per "), capl,
			int(SimWorks.service_capacity(s) / capl) if capl > 0.0 else 0],
			Vector2(cx, cy), DeskKit.DETAIL, Color(DeskKit.INK, 0.55), 1060.0)
		cy += 26.0
	if era >= 2:
		cy = DeskKit.stepper(b, cy, {
			"name": "weight", "x": cx,
			"value": "%.1f of the wallet" % float(o.get("weight", 1.0)),
			"effect": "shelf ∑%.1f of %.1f" % [SimCatalog.shelf_weight(s),
				SimCatalog.SHELF_WEIGHT_CAP],
			"x_value": DeskKit.X_VALUE, "pitch": 52.0,
			"at_min": DeskKit.at_min(DeskCatalog.WEIGHT_STEPS, float(o.get("weight", 1.0))),
			"at_max": float(o.get("weight", 1.0)) >= SimCatalog.MAX_WEIGHT - 0.001,
			"on_minus": func() -> void:
				o["weight"] = clampf(DeskKit.ladder(DeskCatalog.WEIGHT_STEPS,
					float(o.get("weight", 1.0)), -1), SimCatalog.MIN_WEIGHT, SimCatalog.MAX_WEIGHT),
			"on_plus": func() -> void:
				o["weight"] = clampf(DeskKit.ladder(DeskCatalog.WEIGHT_STEPS,
					float(o.get("weight", 1.0)), 1), SimCatalog.MIN_WEIGHT, SimCatalog.MAX_WEIGHT)})
	# ONE lane: the sprint and the drop, side by side at the card's foot
	var nm := String(o.get("name", str(i)))
	var has_sprint := false
	for bv in s.bets:
		var bd: Dictionary = bv
		if String(bd.get("kind", "")) == "cost_down" \
				and String(bd.get("offer", "")) == nm and not bool(bd.get("shipped", false)):
			has_sprint = true
	if has_sprint:
		b.label("a cost sprint is on the roadmap — the team is on it",
			Vector2(cx, cy + 6.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.55), 600.0)
	else:
		DeskKit.arm(b, "sprint_" + nm, "cut the serve cost — a team sprint",
			"3 R&D-weeks of the team — sure?", Vector2(cx, cy + 2.0), func() -> void:
				SimRoadmap.add_cost_down_bet(b.state, nm), 420.0, 20)
	DeskKit.arm(b, "drop_" + nm, "drop this offer ×", "sure? it disappears ×",
		Vector2(790.0, cy + 2.0), func() -> void:
			SimCatalog.remove_offer(s, i)
			s.log_action("DROPPED the offer: %s" % nm)
			b.desk["open_row"] = -1, 300.0, 20)
	return float(frame.get("bottom", y)) + 10.0

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
	b.label(sub, Vector2(COL_NAME_X, y + 27.0), DeskKit.CHIP_S, Color(DeskKit.INK, 0.45),
		COL_NAME_W)
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
	# S4 — the verdict word is a door: press → the street's read, band drawn
	var vword := String(vd.get("word", ""))
	var vbtn := DeskKit.word(b, vword, Vector2(COL_VERDICT_X, y - 4.0), func() -> void:
		b.desk["mode"] = "verdict:%d" % i, 22, vd.get("col", DeskKit.INK), COL_VERDICT_W)
	vbtn.size = Vector2(COL_VERDICT_W, 44.0)
	# S5/R3 — a moved verdict earns the gutter dot (coral when the new word
	# is a losing one); the arbiter keeps the worst row on the pane (R2)
	if b.seen("offers", "vd_" + String(o.get("name", str(i))), vword):
		var vtw: float = minf(b.font().get_string_size(vword,
			HORIZONTAL_ALIGNMENT_LEFT, -1, 22).x, COL_VERDICT_W)
		DeskKit.pen_circle(b, Rect2(COL_VERDICT_X, y + 2.0, vtw, 24.0),
			vd.get("col", DeskKit.INK) == DeskKit.PEN)
	DeskKit.expand(b, Vector2(COL_EXPAND_X, y - 4.0), func() -> void:
		b.desk["open_row"] = -1 if int(b.desk.get("open_row", -1)) == i else i,
		int(b.desk.get("open_row", -1)) == i)
	var steps := DeskCatalog.price_steps(o)
	DeskKit.adjust_pair(b, COL_ADJUST_X, y + 4.0,
		func() -> void: DeskCatalog.price_step(b, i, -1),
		func() -> void: DeskCatalog.price_step(b, i, 1),
		DeskKit.at_min(steps, price), DeskKit.at_max(steps, price))
	# S2b — the row's switch has a name: focus lands on the ADJUST squares
	var adj_rect := Rect2(COL_ADJUST_X - 4.0, y, 96.0, 44.0)
	b.mark_control("adjust_%d" % i, adj_rect)
	if price <= 0.0 and not bool(o.get("price_set", false)) \
			and not b.has_control("set_price"):
		b.mark_control("set_price", adj_rect)
	if SimCatalog.never_pays(o, lc) and not b.has_control("losing_price"):
		b.mark_control("losing_price", adj_rect)
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
	b.mark_control("offer_form", Rect2(DeskKit.X_ID, y, 340.0, 40.0))

## The teaching foot. WARNINGS OUTRANK WISDOM: an offer that loses money on
## every sale outranks the rules line; the drop's migration law rides the rules.
static func _foot(b, s: GameState, base_y: float = 806.0) -> void:
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
		"y": base_y, "rules_y": base_y + 34.0,
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
	var card_h := DeskKit.CARD_HEAD + 30.0 + float(n) * ROW_H + 26.0
	var frame := DeskKit.card_frame(b, 10.0, 64.0, 1120.0, card_h, "the whole shelf — %d offers" % n)
	var cy := float(frame.get("content_y", 64.0))
	cy = _head_row(b, cy)
	for i in n:
		cy = _row(b, cy, i, s, lc, fm)
	var base2 := maxf(DeskKit.DO_LANE_Y, float(frame.get("bottom", cy)) + 16.0)
	_do_lane(b, s, base2)
	_fire_spot(b)
	_foot(b, s, base2 + 44.0)

# ──────────────────── S3 · the DO lane + the focus walk ──────────────────────

## The desk's primary acts in the ONE slot: price the row that needs it (the
## first unpriced, else the flagship), or add to the shelf. The price press
## walks the hand to that row's own ADJUST squares.
static func _do_lane(b, s: GameState, base_y: float = DeskKit.DO_LANE_Y) -> void:
	var t := _price_target(s)
	var actions: Array = []
	if t >= 0:
		var tn := String((s.offers[t] as Dictionary).get("name", "the offer"))
		actions.append({"label": "set price — %s" % tn, "tier": "", "cb": func() -> void:
			if not b.has_control("adjust_%d" % t):
				b.desk["mode"] = "all"
			b.desk["spot"] = "adjust_%d" % t})
	if SimCatalog.shelf_full_line(s) == "":
		actions.append({"label": "add an offer", "tier": "", "cb": func() -> void:
			b.desk["mode"] = "write"})
	DeskKit.do_lane(b, actions, base_y)

## The DO lane's object: the first unpriced offer, else the flagship (the
## heaviest weight on the shelf — the row most of the money walks through).
static func _price_target(s: GameState) -> int:
	if s.offers.is_empty():
		return -1
	for i in s.offers.size():
		var o: Dictionary = s.offers[i]
		if float(o.get("price", 0.0)) <= 0.0 and not bool(o.get("price_set", false)):
			return i
	var best := 0
	for i2 in s.offers.size():
		if float((s.offers[i2] as Dictionary).get("weight", 1.0)) \
				> float((s.offers[best] as Dictionary).get("weight", 1.0)):
			best = i2
	return best

## A DO press asked for a spotlight; the registry filled during THIS draw, so
## the walk fires only after every row has marked its switch.
static func _fire_spot(b) -> void:
	var sid := String(b.desk.get("spot", ""))
	if sid == "":
		return
	b.desk.erase("spot")
	if b.has_control(sid):
		b.spotlight(b.control_rect(sid))

# ─────────────────── S4 · the street's read (the fair band) ──────────────────

## THE VERDICT, OPENED: a paper card saying the street math in receipt lines
## with THE FAIR BAND DRAWN — the price axis, the stretch demand calls fair,
## the street's rate ticked, your price dotted onto it. Any press or Esc
## closes the read before anything else (the desk-mode chain).
static func _verdict_card(b, s: GameState, i: int) -> void:
	if i < 0 or i >= s.offers.size():
		b.desk["mode"] = ""
		return
	var o: Dictionary = s.offers[i]
	var lc := SimEngine.learning_curve(s)
	var fm := SimEngine.street_fair_mult(s)
	var fair := maxf(float(o.get("fair_price", 1.0)) * fm, 0.01)
	var e := maxf(float(o.get("elasticity", 2.0)), 0.05)
	var price := float(o.get("price", 0.0))
	var unpriced := price <= 0.0 and not bool(o.get("price_set", false))
	var billed := fair if unpriced else price
	var vd := _verdict_word(s, o, price, fm, lc)
	var catcher := DeskKit.word(b, "", Vector2(0.0, 0.0), func() -> void:
		b.desk["mode"] = "", DeskKit.DETAIL, DeskKit.INK, 1140.0)
	catcher.size = Vector2(1140.0, 880.0)
	var lines := _street_lines(b, s, o, price, fair, lc, fm, unpriced)
	var ch := 56.0 + 132.0 + float(lines.size()) * 30.0 + 18.0
	var frame := DeskKit.card_frame(b, 290.0, 200.0, 560.0, ch,
		"the street's read — one word, priced")
	var cx := float(frame.get("content_x", 308.0))
	var cy := float(frame.get("content_y", 256.0))
	var band := _FairBand.new()
	band.font = b.font()
	band.fair = fair
	band.lo = fair * pow(1.15, -1.0 / e)
	band.hi = fair * pow(0.85, -1.0 / e)
	band.absurd = fair * pow(0.25, -1.0 / e)
	band.price = billed
	band.pmax = maxf(maxf(band.absurd * 1.15, billed * 1.2), fair * 1.6)
	band.vcol = vd.get("col", DeskKit.INK)
	band.mouse_filter = Control.MOUSE_FILTER_IGNORE
	band.position = Vector2(cx, cy)
	band.set_deferred("size", Vector2(560.0 - DeskKit.CARD_PAD * 2.0, 116.0))
	b.pane().add_child(band)
	var money_x := float(frame.get("money_x", 832.0))
	var ly := cy + 132.0
	for ln in lines:
		var ld: Dictionary = ln
		DeskKit.fit_line(b, String(ld.get("label", "")), Vector2(cx, ly), 19,
			Color(DeskKit.INK, 0.85), 300.0)
		var v: Label = DeskKit.fit_line(b, String(ld.get("value", "")),
			Vector2(cx + 310.0, ly), 19, ld.get("col", DeskKit.INK), money_x - cx - 310.0)
		v.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		ly += 30.0

## The receipt lines: the exact terms the verdict is made of, engine numbers
## only — demand is (price/fair)^−elasticity, clamped at ×2.
static func _street_lines(b, s: GameState, o: Dictionary, price: float, fair: float,
		lc: float, fm: float, unpriced: bool) -> Array:
	var vd := _verdict_word(s, o, price, fm, lc)
	var e := maxf(float(o.get("elasticity", 2.0)), 0.05)
	var dem := SimEngine.offer_demand(o, fair if unpriced else price, fm)
	var lines: Array = [
		{"label": String(o.get("name", "the offer")), "value": String(vd.get("word", "")),
			"col": vd.get("col", DeskKit.INK)},
		{"label": "your price", "value": ("unpriced — bills $%s" % b.fmt(int(round(fair))))
			if unpriced else "$%s" % b.fmt(int(round(price)))},
		{"label": "the street pays", "value": "$%s" % b.fmt(int(round(fair)))},
		{"label": "demand at this price", "value": "×%.2f" % dem},
		{"label": "the fair band", "value": "$%s – $%s" % [
			b.fmt(int(round(fair * pow(1.15, -1.0 / e)))),
			b.fmt(int(round(fair * pow(0.85, -1.0 / e))))]},
	]
	if SimCatalog.never_pays(o, lc):
		lines.append({"label": "every sale loses", "value": "$%s" % b.fmt(int(round(
			-SimCatalog.contribution(o, lc, fm)))), "col": DeskKit.PEN})
	else:
		lines.append({"label": "serve costs / margin", "value": "$%s / $%s" % [
			b.fmt(int(round(SimCatalog.served_unit_cost(o, lc)))),
			b.fmt(int(round(SimCatalog.contribution(o, lc, fm))))]})
	return lines

# ─────────────────────── S8 · the rail's own two reads ───────────────────────

## Pricing never sleeps: the first real decision lives here from week one.
static func is_dormant(_state) -> bool:
	return false

## The rail's four-character read — the shelf's average asking price.
static func micro_status(state) -> String:
	var s: GameState = state
	var sum := 0.0
	var n := 0
	for o in s.offers:
		var p := float((o as Dictionary).get("price", 0.0))
		if p > 0.0:
			sum += p
			n += 1
	if n == 0:
		return ""
	return "$%d avg" % int(round(sum / float(n)))

# ─────────────────────────── the drawn instrument ────────────────────────────

## THE FAIR BAND — the price axis in the desk's own hand: the sage stretch
## demand calls fair, yellow to where it turns absurd, coral past that, the
## street's rate ticked in ink, your price dotted down onto its bead.
class _FairBand:
	extends Control
	var font: Font
	var fair := 1.0
	var lo := 0.8
	var hi := 1.2
	var absurd := 2.0
	var price := 1.0
	var pmax := 2.0
	var vcol := Color.BLACK
	func _draw() -> void:
		var w := size.x
		var ax := size.y - 34.0
		var sc := w / maxf(pmax, 0.01)
		draw_rect(Rect2(lo * sc, ax - 26.0, (hi - lo) * sc, 26.0), Color(DeskKit.SAGE, 0.45))
		draw_rect(Rect2(hi * sc, ax - 26.0, (minf(absurd, pmax) - hi) * sc, 26.0),
			Color(DeskKit.YELL, 0.30))
		if absurd < pmax:
			draw_rect(Rect2(absurd * sc, ax - 26.0, (pmax - absurd) * sc, 26.0),
				Color(DeskKit.PEN, 0.25))
		var rng := RandomNumberGenerator.new()
		rng.seed = 31
		var pts := PackedVector2Array()
		for k in 25:
			pts.append(Vector2(w * float(k) / 24.0, ax + rng.randf_range(-1.2, 1.2)))
		draw_polyline(pts, DeskKit.INK, 2.4, true)
		draw_line(Vector2(fair * sc, ax - 30.0), Vector2(fair * sc, ax + 6.0), DeskKit.INK, 2.2)
		if font != null:
			draw_string(font, Vector2(fair * sc - 34.0, ax + 24.0),
				"street $%d" % int(round(fair)), HORIZONTAL_ALIGNMENT_LEFT, -1, 15,
				Color(DeskKit.INK, 0.6))
			draw_string(font, Vector2(lo * sc + 6.0, ax - 32.0), "fair",
				HORIZONTAL_ALIGNMENT_LEFT, -1, 15, Color(DeskKit.INK, 0.55))
		var px := clampf(price * sc, 0.0, w)
		var yy := 14.0
		while yy < ax - 6.0:
			draw_line(Vector2(px, yy), Vector2(px, minf(yy + 6.0, ax - 6.0)), DeskKit.PEN, 2.4)
			yy += 11.0
		draw_circle(Vector2(px, ax - 13.0), 6.0, vcol)
		draw_arc(Vector2(px, ax - 13.0), 6.0, 0.0, TAU, 12, DeskKit.INK, 2.0)
		if font != null:
			draw_string(font, Vector2(clampf(px - 30.0, 0.0, w - 84.0), 10.0),
				"you: $%d" % int(round(price)), HORIZONTAL_ALIGNMENT_LEFT, -1, 15, DeskKit.PEN)
