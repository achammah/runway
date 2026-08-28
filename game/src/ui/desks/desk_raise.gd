class_name DeskRaise
extends RefCounted
## DESK — THE COMPANY · "the raise" (the fundraising PIPELINE). W2 lane:
## L-OWN, per DECISIONS (THE OWNERSHIP CLUSTER §2) and mockup 18 page 2.
##
## THE PAGE: hero (offers on the table · investors in motion · the
## founder-time banner while active) -> WHO'S IN MOTION (four pipeline
## columns: radar -> conversations -> terms -> wired) -> THE COMPARISON (two
## term sheets side by side, true dilution NOW and AT THE NEXT ROUND, the
## SAFE-stack warning — the desk's biggest teaching moment) -> EVERY WAY
## MONEY COMES IN (six instruments, six characters). Signing runs through
## the receipt-shaped ticket + the two-tap arm; a no-shop freezes the pens.
##
## Numbers all come from SimOwnership/SimEngine — this file draws, never
## computes cap math of its own (a desk that recomputes a rule drifts).
##
## DAG3 (13-binder-ux): every data-room doubt presses into the weak desk it
## names (S2 focus); the DO lane says [pitch — name] [sign — best terms]
## [walk away] — "no" as pressable as "yes", S9 two-tap, the walk taking the
## stage machine's own "passed" exit (the expiry's path, founder-initiated);
## the founder-time banner quotes the velocity cost in numbers straight from
## the raise_distraction catalog entry; zone 3's glossary re-lays as the
## standard foot so the DO lane keeps its one anchor; zero state (S1), ask
## strip (S2a), the offers count wearing the delta arrow (S5).

const QUESTION := "who would fund us next, and at what true price?"

## S8 — the raise sleeps through the garage: no paper signed, no raise opened,
## nothing to pipeline. The tab stays on the map (the map is the curriculum)
## at 60%; it wakes the week the era turns or money moves.
static func is_dormant(state) -> bool:
	var s: GameState = state
	return s.era == "garage" and s.instruments.is_empty() \
		and not bool(s.raise_state.get("active", false))

## S10 — the tab's four-character read. Quiet while dormant; awake, the
## interest score is the desk in one glance.
static func micro_status(state) -> String:
	var s: GameState = state
	if is_dormant(s):
		return ""
	var terms := _terms_count(s)
	if terms > 0:
		return "%d offer%s" % [terms, "" if terms == 1 else "s"]
	return "%.0f/100" % float(s.raise_state.get("interest_score", 0.0))

static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	var terms := _terms_count(s)
	var motion := _in_motion_count(s)
	if terms + motion == 0:
		return {"big": "the raise", "line": "interest %.0f/100 — inbound comes to traction, not to wishes"
			% float(s.raise_state.get("interest_score", 0.0))}
	return {"big": "%d offer%s on the table" % [terms, "" if terms == 1 else "s"],
		"line": "%d investor%s in motion — the buyer buys a piece of you" % [motion,
			"" if motion == 1 else "s"]}

static func _terms_count(s: GameState) -> int:
	return _stages(s, "terms").size()

static func _in_motion_count(s: GameState) -> int:
	return _stages(s, "radar").size() + _stages(s, "conversations").size() + _stages(s, "terms").size()

static func _stages(s: GameState, stage: String) -> Array:
	var out: Array = []
	for st in s.raise_state.get("stages", []):
		if String((st as Dictionary).get("stage", "")) == stage:
			out.append(st)
	return out

static func draw(b) -> void:
	var state: GameState = b.state
	# S1 — an untouched pipeline opens on the designed first week
	if _in_motion_count(state) == 0 and state.instruments.is_empty() \
			and not bool(state.raise_state.get("active", false)) \
			and _stages(state, "wired").is_empty() and _stages(state, "passed").is_empty():
		_zero(b, state)
		return
	var terms := _stages(state, "terms")
	var has_vig := _pitch_vignette(b)
	# the hero keeps to its own lane: when the vignette rides the header
	# (x600..732, left of the corner banner at x740) the big line wears its
	# compact form and the sentence trims to the lane — three lanes, no
	# collisions; with no image the full wording keeps the whole band
	var big := "%d offer%s on the table · %d in motion" % [terms.size(),
		"" if terms.size() == 1 else "s", _in_motion_count(state)]
	var sentence := "raising is a pipeline, like customers — except the buyer buys a piece of you."
	if has_vig:
		big = _fit(b, "%d offer%s · %d in motion" % [terms.size(),
			"" if terms.size() == 1 else "s", _in_motion_count(state)],
			560.0, DeskKit.HERO_BIG)
		sentence = _fit(b, sentence, 560.0, DeskKit.ROW)
	var y := DeskKit.hero_band(b, big, sentence)
	# S5 — the offers count wears the arrow when it moved since the last open
	var prev_offers: String = b.seen_prev("the raise", "offers")
	b.seen("the raise", "offers", str(terms.size()))
	if prev_offers != "":
		var big_w: float = b.font().get_string_size(big, HORIZONTAL_ALIGNMENT_LEFT, -1,
			DeskKit.HERO_BIG).x
		DeskKit.delta_arrow(b, 10.0 + minf(big_w, 560.0) + 14.0, 30.0,
			float(terms.size()), prev_offers.to_float())
	# the founder-time banner — fundraising is never free
	if bool(state.raise_state.get("active", false)):
		var t1: Label = b.label("the raise eats ≈%.0f%% of your week"
			% (float(state.raise_state.get("founder_time_tax", 0.3)) * 100.0),
			Vector2(740.0, 10.0), DeskKit.DETAIL, DeskKit.PEN, 380.0)
		t1.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		var t2: Label = b.label("the shop slows while you pitch — that is real",
			Vector2(740.0, 40.0), 18, Color(DeskKit.INK, 0.5), 380.0)
		t2.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		# THE VELOCITY COST IN NUMBERS — straight from the status catalog, the
		# one place the magnitude lives (the desk quotes, never recomputes)
		var vmult := float((SimEngine.STATUS.get("raise_distraction", {}) as Dictionary)
			.get("velocity_mult", 1.0))
		if vmult < 1.0:
			var t2b: Label = b.label("build speed ≈ −%d%% this week (velocity ×%.2f)"
				% [int(round((1.0 - vmult) * 100.0)), vmult],
				Vector2(740.0, 64.0), 18, DeskKit.PEN, 380.0)
			t2b.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	else:
		var t3: Label = b.label("investor interest %.0f/100"
			% float(state.raise_state.get("interest_score", 0.0)),
			Vector2(740.0, 10.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.7), 380.0)
		t3.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		# S4 — the score presses into what feeds it (inputs, not arithmetic)
		DeskKit.press_receipt(b, Rect2(740.0, 8.0, 380.0, 30.0), "what interest reads", [
			{"label": "the era", "value": state.era},
			{"label": "traction", "value": str(state.traction)},
			{"label": "hype", "value": str(state.hype)},
			{"label": "growth last week", "value": "%+.1f%%" % (state.last_growth * 100.0)},
			{"label": "the season", "value": state.macro_season},
			{"label": "reads", "value": "%.0f/100"
				% float(state.raise_state.get("interest_score", 0.0)), "col": DeskKit.SAGE}])
	# S2 — red speaks on the page: the pipeline's asks in one red line
	DeskKit.ask_strip(b, "the raise", 10.0, 100.0, 720.0, "sign or walk — below")

	# ── zone 1 · WHO'S IN MOTION — four pipeline columns
	var z1 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, 262.0, 1, "who's in motion",
		"inbound comes to traction, not to wishes — outbound is yours to spend time on")
	var col_w := 268.0
	var cx := float(z1.content_x) - 6.0
	var col_h := 176.0
	# THE COLLAPSE LAW per column: one card face-up (the loudest), the crowd
	# folds to an honest count — a pipeline column never spills its box.
	var c1 := DeskKit.wall_column(b, cx, z1.cursor, col_w, col_h, "on the radar", "")
	var radar := _stages(state, "radar")
	var outbound := SimOwnership.outbound_targets(state)
	if not radar.is_empty():
		var sd: Dictionary = radar[0]
		var nm := String(sd.get("name", "?"))
		DeskKit.wall_card(b, c1, {"title": nm,
			"facts": ["inbound — saw the growth" if bool(sd.get("inbound", false)) else "outbound — your list"],
			"on_press": func() -> void:
				SimOwnership.op_pitch_investor(b.state, nm)})
		_fold_note(b, c1, radar.size() - 1, col_w)
	elif not outbound.is_empty():
		DeskKit.wall_card(b, c1, {"title": "%d outbound target%s" % [outbound.size(),
			"" if outbound.size() == 1 else "s"],
			"facts": ["pitch one — costs a week's focus"],
			"on_press": func() -> void:
				SimOwnership.op_pitch_investor(b.state)})
	var c2 := DeskKit.wall_column(b, cx + col_w + 16.0, z1.cursor, col_w, col_h, "conversations", "")
	var convs := _stages(state, "conversations")
	if not convs.is_empty():
		var sd2: Dictionary = convs[0]
		var doubt := String(sd2.get("doubt", ""))
		var conv_cfg := {"title": String(sd2.get("name", "?")),
			"facts": ["asked for real numbers"] if doubt == "" else ["noticed: %s" % doubt],
			"sev": 0 if doubt == "" else 2}
		if doubt != "":
			# S2 — the doubt names its weak desk; the card walks you there
			conv_cfg["on_press"] = func() -> void:
				b.focus_desk(_doubt_desk(doubt), "", "the raise")
		DeskKit.wall_card(b, c2, conv_cfg)
		_fold_note(b, c2, convs.size() - 1, col_w)
	var c3 := DeskKit.wall_column(b, cx + (col_w + 16.0) * 2.0, z1.cursor, col_w, col_h,
		"terms on the table", "")
	if not terms.is_empty():
		var sd3: Dictionary = terms[0]
		var t: Dictionary = sd3.get("terms", {})
		DeskKit.wall_card(b, c3, {"title": String(sd3.get("name", "?")), "ready": true,
			"facts": [_terms_fact(t), "expires wk %d" % int(t.get("expires_wk", 0))]})
		_fold_note(b, c3, terms.size() - 1, col_w)
	var c4 := DeskKit.wall_column(b, cx + (col_w + 16.0) * 3.0, z1.cursor, col_w, col_h,
		"signed & wired", "")
	if not state.instruments.is_empty():
		var idd: Dictionary = state.instruments[state.instruments.size() - 1]
		DeskKit.wall_card(b, c4, {"title": "%s — %s" % [String(idd.get("kind", "?")),
			String(idd.get("holder", "?"))],
			"facts": ["$%s · wk %d" % [SimOwnership.money(int(idd.get("amount", 0))),
				int(idd.get("signed_wk", 0))]]})
		_fold_note(b, c4, state.instruments.size() - 1, col_w)
	if _in_motion_count(state) == 0 and state.instruments.is_empty():
		# the authored empty line sits BELOW the four boxes, never across them
		DeskKit.empty(b, Vector2(z1.content_x + 8.0, z1.cursor + col_h + 8.0),
			"", "nobody is knocking yet — traction is the doorbell", true)
	# S2b — the engine rows' named switches: the columns they mean
	b.mark_control("pitch", Rect2(cx, float(z1.cursor), col_w, col_h))
	b.mark_control("sign_terms", Rect2(cx + (col_w + 16.0) * 2.0, float(z1.cursor),
		col_w, col_h))
	b.mark_control("instruments", Rect2(cx + (col_w + 16.0) * 3.0, float(z1.cursor),
		col_w, col_h))
	y += 262.0 + 10.0

	# ── zone 2 · THE COMPARISON — two sheets never say their true price
	var z2 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, 348.0, 2, "the comparison",
		"two term sheets never say their true price — this card does")
	if terms.is_empty():
		DeskKit.empty(b, Vector2(z2.content_x, z2.cursor + 8.0),
			"no terms on the table.",
			"conversations become sheets when the data room holds — growth, margin, runway", true)
		# S2 — THE DATA ROOM'S DOUBTS, each pressable into the weak desk it
		# names (the doubts already speak in page names)
		var doubts: Array = SimOwnership.data_room(state).get("doubts", [])
		if not doubts.is_empty():
			b.label("the data room's doubts — each names its weak page:",
				Vector2(z2.content_x, float(z2.cursor) + 100.0), 18,
				Color(DeskKit.INK, 0.6), 1070.0)
			var dx := float(z2.content_x)
			var dn := 0
			for d in doubts:
				if dn >= 3:
					break
				var ds := String(d)
				DeskKit.word(b, _fit(b, "! %s ->" % ds, 330.0, 19),
					Vector2(dx, float(z2.cursor) + 132.0), func() -> void:
						b.focus_desk(_doubt_desk(ds), "", "the raise"),
					19, DeskKit.PEN, 340.0)
				dx += 356.0
				dn += 1
	else:
		_comparison_ticket(b, state, z2.content_x, z2.cursor, terms[0])
		if terms.size() >= 2:
			_comparison_ticket(b, state, z2.content_x + 375.0, z2.cursor, terms[1])
		else:
			b.label("one sheet is not a comparison — a second set of terms teaches the price of the first.",
				Vector2(z2.content_x + 385.0, z2.cursor + 10.0), DeskKit.DETAIL,
				Color(DeskKit.INK, 0.55), 330.0)
		var stack := SimOwnership.stack_dilution_at(state, float(SimEngine.valuation(state)))
		if stack > 0.0:
			b.label("THE SAFE STACK: with the old paper, ≈%.0f%% converts AT ONCE at the next priced round. Deferred is not free." % stack,
				Vector2(z2.content_x + 762.0, z2.cursor + 6.0), 19, DeskKit.PEN, 340.0)
		b.label("participating preferred would be flagged here in red — predatory.",
			Vector2(z2.content_x + 762.0, z2.cursor + 130.0), 17, Color(DeskKit.INK, 0.45), 340.0)
		# S2 — doubts that still stand while sheets sit: pressable, right rail
		var doubts2: Array = SimOwnership.data_room(state).get("doubts", [])
		var dy := float(z2.cursor) + 164.0
		var dn2 := 0
		for d2 in doubts2:
			if dn2 >= 2:
				break
			var ds2 := String(d2)
			DeskKit.word(b, _fit(b, "! %s ->" % ds2, 330.0, 17),
				Vector2(z2.content_x + 762.0, dy), func() -> void:
					b.focus_desk(_doubt_desk(ds2), "", "the raise"),
				17, DeskKit.PEN, 340.0)
			dy += 42.0
			dn2 += 1
	y += 348.0 + 10.0

	# ── the foot · EVERY WAY MONEY COMES IN + the desk law. Zone 3's glossary
	# re-laid as the standard money-desk foot so the DO lane keeps its one
	# anchor (762) clear — same words, no zone box.
	DeskKit.fit_line(b, "angel check · SAFE · convertible note · priced round · bridge · venture debt (-> the bank) · secondary — six characters, from a friend's check to selling your own slice",
		Vector2(DeskKit.X_ID, 812.0), DeskKit.LAW, Color(DeskKit.INK, 0.7), 1100.0)
	DeskKit.fit_line(b, _costline(state), Vector2(DeskKit.X_ID, 846.0),
		DeskKit.LAW, DeskKit.BLUE, 1100.0)
	# S3 — the DO lane: pitch, sign, or walk — "no" as pressable as "yes"
	_do_lane(b, state)

## S1 — the designed first week: the pipeline as a promise, the odds said
## honestly (the engine's own constants, never re-derived), the one action.
static func _zero(b, state: GameState) -> void:
	var score := float(state.raise_state.get("interest_score", 0.0))
	var act := ""
	var target := ""
	var outbound := SimOwnership.outbound_targets(state)
	if not outbound.is_empty():
		target = String(outbound[0])
		act = "pitch %s — costs a week's focus" % target.substr(0, 18)
	DeskKit.zero_state(b, {
		"will_show": "THE PIPELINE — radar -> conversations -> terms -> wired",
		"would_line": "at interest %.0f/100 an inbound knock WOULD land ≈%.0f%% of weeks — inbound comes to traction, not to wishes" % [
			score, score * SimOwnership.KNOCK_P_MAX],
		"action_label": act,
		"action_cb": func() -> void:
			SimOwnership.op_pitch_investor(state, target),
		"wakes_hint": "wakes at coworking, or the week the growth gets noticed — raising is optional, and never free",
	})

## S3 — the DO lane: [pitch — name] [sign — best terms] [walk away].
static func _do_lane(b, state: GameState) -> void:
	var actions: Array = []
	var pitch_name := ""
	var radar := _stages(state, "radar")
	if not radar.is_empty():
		pitch_name = String((radar[0] as Dictionary).get("name", ""))
	else:
		var outbound := SimOwnership.outbound_targets(state)
		if not outbound.is_empty():
			pitch_name = String(outbound[0])
	if pitch_name != "":
		var pn := pitch_name
		actions.append({"label": "pitch — %s" % pn.substr(0, 16), "tier": "",
			"cb": func() -> void:
				SimOwnership.op_pitch_investor(b.state, pn)})
	var terms := _stages(state, "terms")
	if not terms.is_empty() and SimOwnership.no_shop_until(state) <= state.week:
		var best := _best_terms(terms)
		var bn := String(best.get("name", ""))
		actions.append({"label": "sign — %s" % bn.substr(0, 14), "tier": "two-tap",
			"cb": func() -> void:
				SimOwnership.op_sign_instrument(b.state, bn)})
		actions.append({"label": "walk away", "tier": "two-tap",
			"cb": func() -> void:
				_walk_away(b.state)})
	if not actions.is_empty():
		DeskKit.do_lane(b, actions)

## "Best" is the biggest check on the table — the plainest tie-break.
static func _best_terms(terms: Array) -> Dictionary:
	var best: Dictionary = terms[0]
	for t in terms:
		var td: Dictionary = t
		if int((td.get("terms", {}) as Dictionary).get("amount", 0)) \
				> int((best.get("terms", {}) as Dictionary).get("amount", 0)):
			best = td
	return best

## WALK AWAY — declining is a real move, not neglect: every open sheet takes
## the stage machine's own "passed" exit (the expiry path's transition,
## founder-initiated). No invented penalties — hesitation is already priced.
static func _walk_away(state: GameState) -> void:
	var n := 0
	for st in state.raise_state.get("stages", []):
		var sd: Dictionary = st
		if String(sd.get("stage", "")) == "terms":
			sd["stage"] = "passed"
			n += 1
	if n > 0:
		state.log_action("the raise: walked away from %d term sheet%s — the best deal is sometimes none"
			% [n, "" if n == 1 else "s"])

## The weak desk a data-room doubt names (the doubts speak in page words).
static func _doubt_desk(doubt: String) -> String:
	if doubt.contains("growth"):
		return "growth"
	if doubt.contains("runway"):
		return "the bank"
	if doubt.contains("margin"):
		return "spend"
	if doubt.contains("product"):
		return "what we make"
	if doubt.contains("customer"):
		return "customers"
	return "this week"

## One term sheet, priced honestly, with its SIGN arm under it.
static func _comparison_ticket(b, state: GameState, x: float, y: float, entry: Dictionary) -> void:
	var t: Dictionary = entry.get("terms", {})
	var nm := String(entry.get("name", "?"))
	var kind := String(t.get("kind", "safe"))
	var lines: Array = [{"label": "money now", "value": "$%s" % SimOwnership.money(int(t.get("amount", 0)))}]
	if kind == "priced":
		var topup := float(t.get("pool_topup_pct", 0.0))
		lines.append({"label": "dilution today", "value": "%.1f%%%s" % [float(t.get("pct", 0.0)),
			(" + %.0f%% pool" % topup) if topup > 0.0 else ""], "col": DeskKit.PEN})
		lines.append({"label": "preferences", "value":
			("participating — PREDATORY" if bool(t.get("participating", false)) else "1× non-participating · fair"),
			"col": DeskKit.PEN if bool(t.get("participating", false)) else DeskKit.INK})
		lines.append({"label": "board · control", "value": "1 seat + no-shop %d wks" % int(t.get("no_shop_wks", 4)),
			"col": DeskKit.PEN})
	else:
		var pseudo := {"kind": kind, "amount": int(t.get("amount", 0)), "cap": int(t.get("cap", 0)),
			"discount": float(t.get("discount", 0.0)), "rate": float(t.get("rate", 0.0)),
			"signed_wk": state.week, "pct": 0.0}
		lines.append({"label": "dilution today", "value": "0%", "col": DeskKit.SAGE})
		lines.append({"label": "dilution at the next round", "value": "≈%.1f%%"
			% SimOwnership.convert_pct_at(pseudo, float(SimEngine.valuation(state)), state.week),
			"col": DeskKit.PEN})
		if kind == "note" or kind == "bridge":
			lines.append({"label": "the fuse", "value": "matures wk %d" % int(t.get("maturity_wk", 0)),
				"col": DeskKit.PEN})
		else:
			lines.append({"label": "board · control", "value": "none"})
	var character := String({"safe": "fast money, deferred pain", "note": "a fuse under fast money",
		"bridge": "insiders keeping you alive", "priced": "real partner, real price"}.get(kind, ""))
	var end_y := DeskKit.ticket(b, x, y, 360.0, {"title": "%s — the %s" % [nm, kind],
		"lines": lines, "total_label": "character", "total_value": character,
		"total_col": DeskKit.INK})
	if SimOwnership.no_shop_until(state) > state.week:
		b.label("no-shop holds until wk %d — the pens are down" % SimOwnership.no_shop_until(state),
			Vector2(x, end_y - 8.0), 17, Color(DeskKit.INK, 0.45), 360.0)
	else:
		DeskKit.arm(b, "sign_%s" % nm, _fit(b, "SIGN %s" % nm.to_upper(), 330.0),
			"press again — the cap table redraws", Vector2(x, end_y - 10.0), func() -> void:
				SimOwnership.op_sign_instrument(b.state, nm), 350.0, DeskKit.DETAIL)

## LONG-TEXT LAW: a caption is measured and trimmed to its lane, never left
## to run under a neighbour.
static func _fit(b, s: String, w: float, size: int = DeskKit.DETAIL) -> String:
	if b.font().get_string_size(s, HORIZONTAL_ALIGNMENT_LEFT, -1, size).x <= w:
		return s
	var t := s
	while t.length() > 1 and b.font().get_string_size(t + "…",
			HORIZONTAL_ALIGNMENT_LEFT, -1, size).x > w:
		t = t.substr(0, t.length() - 1)
	return t.strip_edges() + "…"

## THE PITCH ILLUSTRATION (DECISIONS § THE THREE BINDER ILLUSTRATIONS): a
## generated vignette at user://illus_pitch.png rides the header BEHIND the
## hero when it exists; the plain header IS the fallback — numbers never wait.
static func _pitch_vignette(b) -> bool:
	if not FileAccess.file_exists("user://illus_pitch.png"):
		return false
	var img := Image.new()
	if img.load(ProjectSettings.globalize_path("user://illus_pitch.png")) != OK:
		return false
	var tr := TextureRect.new()
	tr.texture = ImageTexture.create_from_image(img)
	tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	tr.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tr.modulate = Color(1, 1, 1, 0.9)
	tr.position = Vector2(600.0, 2.0)
	tr.set_deferred("size", Vector2(132.0, 132.0))
	b.pane().add_child(tr)
	return true

static func _fold_note(b, col: Dictionary, n: int, col_w: float) -> void:
	if n > 0:
		b.label("+%d more wait behind this one" % n, Vector2(float(col.content_x) + 2.0,
			float(col.y) + float(col.h) - 26.0), 16, Color(DeskKit.INK, 0.5), col_w - 20.0)

static func _terms_fact(t: Dictionary) -> String:
	match String(t.get("kind", "")):
		"safe":
			return "SAFE $%s · cap %s" % [SimOwnership.money_short(int(t.get("amount", 0))).lstrip("$"),
				SimOwnership.money_short(int(t.get("cap", 0)))]
		"note":
			return "note $%s · fuse wk %d" % [SimOwnership.money_short(int(t.get("amount", 0))).lstrip("$"),
				int(t.get("maturity_wk", 0))]
		"bridge":
			return "bridge $%s — insiders" % SimOwnership.money_short(int(t.get("amount", 0))).lstrip("$")
	return "%s at %s pre" % [SimOwnership.money_short(int(t.get("amount", 0))),
		SimOwnership.money_short(int(t.get("valuation", 0)))]

static func _costline(state: GameState) -> String:
	if SimOwnership.no_shop_until(state) > state.week:
		return "no-shop honored: other terms freeze until wk %d" % SimOwnership.no_shop_until(state)
	if not _stages(state, "terms").is_empty():
		return "signing -> the cap table redraws + covenants arm · walking away is allowed — the best deal is sometimes none"
	return "the data room reads YOUR binder — weak pages become named doubts"

static func handle(_b, _id: String) -> void:
	pass
