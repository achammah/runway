class_name DeskThisWeek
extends RefCounted
## DESK — THE LOG · "this week", the desk you play from. W2 lane: L-COMPANY.
## THE QUESTION THIS DESK ANSWERS: "what happened, and what's our move?"
## Spec: docs/design/12-binder-rework-2.md § this week. This is the binder's
## default landing tab (the UI spine set that; this file fills the content).
##
##   HERO      week number + era + one plain sentence of the week's situation
##             (pressable — the receipt says the terms behind the verdict)
##   1 THE CARD          the event and its bite (art slot at the left); when
##                       no card is seeded yet, THE OUTCOME VIEW: last week's
##                       headline + its consequence lines, each with a jump
##                       to the desk where that number is edited
##   2 YOUR MOVE         the written-move composer — the SAME input flow the
##                       journal runs (clarify answers append after " — ");
##                       this desk only HOSTS the words, the garage's
##                       existing commit path judges them. Under the rule:
##                       THE WEEK'S CHIPS (13-binder § this week) — every
##                       desk-suggested action, gathered by the kit's
##                       collect_suggestions; prefill chips APPEND to the
##                       draft (never overwrite), jump chips walk there
##   3 ARMED THIS WEEK   the receipt list of changes staged since the last
##                       roll (read from the run's own action log)
##   4 LOCK IN           the die — with THE PRE-ROLL REVIEW intercept
##                       (SimEngine.preroll_items) before any roll. The
##                       PRESS itself lives in the DO lane (S3, one slot on
##                       every desk) wearing the garage's outstanding-count
##                       badge; the zone explains, the lane acts.
##
## THE HOST SEAM: the garage screen owns the real flow (_commit_from_text:
## clarify -> pre-roll -> dice -> adjudicate). It hands this desk the week's
## card and a lock hook; until that package lands the desk shows the card it
## can prove and points the roll at the journal instead of faking one.
##   DeskThisWeek.week_card = {title, line, icon}   (set at week start)
##   DeskThisWeek.lock_hook = Callable(text)        (routes into the flow)
##   DeskThisWeek.draft                             (the composer's words)

const QUESTION := "what happened, and what's our move?"

## The host seam (see above). Static: survives binder open/close, dies with
## the process, never saved — the garage re-seeds it every week.
static var week_card: Dictionary = {}
static var lock_hook: Callable = Callable()
static var draft: String = ""

static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	var line := String(week_card.get("title", ""))
	if line == "":
		line = "the desk you play from"
	return {"big": "week %d" % s.week, "line": line}

static func draw(b) -> void:
	var s: GameState = b.state

	# HERO — the week, the era, the situation in plain words
	var band := SimEngine.health_band(s)
	var hero_line := "%s · %s" % [s.era_display_name(), band.to_lower()]
	var y := DeskKit.hero_band(b, "week %d" % s.week, hero_line, DeskKit.INK)
	# S4 — the hero is pressable: the terms behind the verdict word
	var pnl: Dictionary = s.get_meta("pnl", {})
	var net := int(pnl.get("net", 0))
	DeskKit.press_receipt(b, Rect2(DeskKit.X_ID, 6.0, 560.0, 100.0), "the week's terms", [
		{"label": "cash", "value": "$%s" % b.fmt(s.cash)},
		{"label": "the week's net", "value": "%s$%s" % ["+" if net >= 0 else "−",
			b.fmt(absi(net))], "col": DeskKit.SAGE if net >= 0 else DeskKit.PEN},
		{"label": "runway = cash ÷ net burn", "value": "%d wk" % SimEngine.runway_weeks(s)},
		{"label": "the verdict", "value": band.to_lower()}])
	# S5/R3 — a moved situation line earns the gutter dot (the rect starts at
	# 24 so the dot's 14px gutter lands inside the page edge; the sentence
	# rides at +58 since the slot grid)
	if b.seen("this week", "band", band):
		var lw: float = b.font().get_string_size(hero_line,
			HORIZONTAL_ALIGNMENT_LEFT, -1, DeskKit.ROW).x
		DeskKit.pen_circle(b, Rect2(24.0, 64.0, minf(lw, 900.0), 34.0))

	# 1 · THE CARD — the event and its bite, art slot left; without a seeded
	# card this zone is THE OUTCOME VIEW (13-binder § this week, post-roll):
	# last week's headline + its consequence lines, each naming its desk.
	var z1 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, 120.0, 1, "the card", "")
	var title := String(week_card.get("title", ""))
	var icon := String(week_card.get("icon", ""))
	if icon != "":
		b.icon(icon, Vector2(float(z1.content_x), float(z1.content_y) - 16.0), 64.0)
	var tx := float(z1.content_x) + (80.0 if icon != "" else 0.0)
	if title != "":
		b.label(title, Vector2(tx, float(z1.content_y) - 14.0), DeskKit.ROW,
			DeskKit.INK, 1000.0 - tx)
		b.label(String(week_card.get("line", "")), Vector2(tx, float(z1.content_y) + 20.0),
			DeskKit.DETAIL, Color(DeskKit.INK, 0.65), 1020.0 - tx)
	else:
		var last_t := String(s.last_outcome.get("title", ""))
		b.label(("last week: " + last_t) if last_t != "" else "the first week is a blank page",
			Vector2(tx, float(z1.content_y) - 16.0), DeskKit.ROW,
			Color(DeskKit.INK, 0.75), 1000.0)
		var cons := _consequences(b, s)
		if cons.is_empty():
			b.label("this week's card opens with the journal", Vector2(tx,
				float(z1.content_y) + 16.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.5), 900.0)
		else:
			var cy := float(z1.y) + 64.0
			for ci in mini(cons.size(), 2):
				var cd: Dictionary = cons[ci]
				DeskKit.fit_line(b, String(cd.get("text", "")), Vector2(tx, cy),
					DeskKit.DETAIL, Color(DeskKit.INK, 0.75), 850.0 - (tx - float(z1.content_x)))
				var cdesk := String(cd.get("desk", ""))
				if cdesk != "":
					var jump_cb := func() -> void:
						b.focus_desk(cdesk, "", "this week")
					DeskKit.word(b, cdesk + " ->", Vector2(float(z1.content_x) + 890.0,
						cy - 6.0), jump_cb, DeskKit.DETAIL, DeskKit.PEN, 200.0)
				cy += 28.0
	y = float(z1.bottom) + 12.0

	# 2 · YOUR MOVE — the composer (the same flow; clarify appends after " — "),
	# and under its rule THE WEEK'S CHIPS: what the desks suggest, adopt-only.
	var z2 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, 168.0, 2, "your move",
		"plain words — the world asks a question when a number is missing")
	_move_field(b, float(z2.content_x), float(z2.cursor) - 6.0)
	_chip_strip(b, s, float(z2.content_x), float(z2.cursor) + 46.0)
	y = float(z2.bottom) + 12.0

	# 3 · ARMED THIS WEEK — everything staged since the last roll
	var staged := _staged_rows(s)
	var z3_h := 66.0 + maxf(float(mini(staged.size(), 3)) * 30.0, 28.0) + 10.0
	var z3 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, z3_h, 3, "armed this week",
		"the receipt list — what the week will carry into the roll")
	var ry := float(z3.cursor) - 8.0
	if staged.is_empty():
		b.label("nothing staged since the last roll — steppers, signatures and arranges land here",
			Vector2(float(z3.content_x), ry), DeskKit.DETAIL, Color(DeskKit.INK, 0.5), 1040.0)
	for i in mini(staged.size(), 3):
		var row: Dictionary = staged[i]
		b.label("· " + String(row.get("label", "")), Vector2(float(z3.content_x), ry),
			DeskKit.DETAIL, row.get("col", Color(DeskKit.INK, 0.8)), 1000.0)
		ry += 30.0
	if staged.size() > 3:
		b.label("+%d more in the log" % (staged.size() - 3),
			Vector2(float(z3.content_x), ry), 17, Color(DeskKit.INK, 0.5), 400.0)
	y = float(z3.bottom) + 12.0

	# 4 · LOCK IN — the die, behind THE PRE-ROLL REVIEW. The zone explains and
	# holds the intercept; the PRESS lives in the DO lane below (S3).
	var in_preroll := String(b.desk.get("mode", "")) == "preroll"
	var z4 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, 128.0 if in_preroll else 112.0,
		4, "lock in",
		"the die is cast at the press — the review reads the threats list first")
	var ay := float(z4.cursor) - 6.0
	var outstanding := SimEngine.preroll_items(s)
	if in_preroll:
		var shown := 0
		for it in outstanding:
			if shown >= 2:
				break
			var itd: Dictionary = it
			b.label("%s%s — %s" % ["! " if int(itd.get("severity", 2)) >= 3 else "",
				String(itd.get("desk", "")), String(itd.get("label", ""))],
				Vector2(float(z4.content_x), ay), DeskKit.DETAIL, DeskKit.PEN, 700.0)
			ay += 28.0
			shown += 1
		if outstanding.size() > 2:
			b.label("+%d more on the threats page" % (outstanding.size() - 2),
				Vector2(float(z4.content_x), ay), 17, Color(DeskKit.INK, 0.5), 400.0)
		DeskKit.word(b, "roll anyway", Vector2(float(z4.content_x) + 760.0,
			float(z4.cursor) - 6.0), func() -> void:
				b.desk.erase("mode")
				_fire_lock(b), DeskKit.STATUS, DeskKit.PEN, 200.0)
		DeskKit.word(b, "go fix it", Vector2(float(z4.content_x) + 760.0,
			float(z4.cursor) + 34.0), func() -> void:
				b.desk.erase("mode")
				if not outstanding.is_empty():
					b.jump_to_ask(outstanding[0] as Dictionary, "this week"),
			DeskKit.STATUS, DeskKit.INK, 200.0)
	elif lock_hook.is_valid():
		if outstanding.is_empty():
			b.label("nothing outstanding — the week is ready to roll",
				Vector2(float(z4.content_x), ay), DeskKit.DETAIL,
				Color(DeskKit.INK, 0.65), 900.0)
		else:
			b.label("%d outstanding — the review will stop you once" % outstanding.size(),
				Vector2(float(z4.content_x), ay), DeskKit.DETAIL, DeskKit.PEN, 700.0)
	else:
		# R7 — one line: the outstanding count already rides the LOCK IN badge
		# (the garage idiom below) and the review says it again; the second
		# line here printed through the zone's own border.
		b.label("the journal rolls the week — close the binder (TAB) and press LOCK IN",
			Vector2(float(z4.content_x), ay), DeskKit.DETAIL, Color(DeskKit.INK, 0.55), 900.0)

	# S3 — THE DO LANE: the die and the pen, one slot, above the teaching foot.
	var actions: Array = []
	if lock_hook.is_valid() and not in_preroll:
		var lock_cb := func() -> void:
			if SimEngine.preroll_items(b.state).is_empty():
				_fire_lock(b)
			else:
				b.desk["mode"] = "preroll"
		actions.append({"label": "LOCK IN — roll the week", "cb": lock_cb, "tier": ""})
	var write_cb := func() -> void:
		b.desk["focus_move"] = true
	actions.append({"label": "write the move", "cb": write_cb, "tier": ""})
	DeskKit.do_lane(b, actions)
	# the outstanding-count badge rides the LOCK IN mirror (the garage idiom)
	var n_att := SimEngine.attention_items(s).size()
	if lock_hook.is_valid() and not in_preroll and n_att > 0 and b.has_control("do_0"):
		var r0: Rect2 = b.control_rect("do_0")
		var bd := DeskKit._CountBadge.new()
		bd.count = n_att
		bd.font = b.display_font()
		bd.mouse_filter = Control.MOUSE_FILTER_IGNORE
		bd.position = r0.position + Vector2(r0.size.x - 12.0, -14.0)
		bd.set_deferred("size", Vector2(28.0, 28.0))
		b.pane().add_child(bd)

	DeskKit.footer(b, {
		"computed": "",
		"rules": "one move a week · the DM judges the plan into a DC and the die decides "
			+ "· clarify answers append after \" — \"", "rules_y": 856.0})

## THE OUTCOME VIEW's consequence lines: last week's booked effects, each with
## a best-effort desk. The desk is read from the why-text when it names one
## (any taxonomy id or legacy desk word), else from the op's own ledger:
## cash → the bank, customers → customers, product → what we make,
## morale → team, hype → the street. "" = no jump word.
static func _consequences(b, s: GameState) -> Array:
	var out: Array = []
	for eff in (s.last_outcome.get("dm", {}) as Dictionary).get("effects", []):
		if not eff is Dictionary:
			continue
		var d: Dictionary = eff
		var op := String(d.get("op", ""))
		var why := String(d.get("why", ""))
		if why == "" or op == "set_flag":
			continue
		var v := int(d.get("v", 0))
		var noun := String({"product_delta": "product", "traction_delta": "customers",
			"morale_delta": "morale", "hype_delta": "hype"}.get(op, ""))
		var amt: String = ("+" if v >= 0 else "−") \
			+ (("$" + b.fmt(absi(v))) if op == "cash_delta" else str(absi(v)))
		var text := ("%s %s — %s" % [amt, noun, why]) if noun != "" \
			else ("%s — %s" % [amt, why])
		out.append({"text": text, "desk": _consequence_desk(op, why)})
	return out

static func _consequence_desk(op: String, why: String) -> String:
	var low := why.to_lower()
	for g in Binder.GROUPS:
		for d in (g as Dictionary).get("desks", []):
			if String(d) != "this week" and low.find(String(d)) >= 0:
				return String(d)
	for old in Binder.LEGACY_TO_DESK:
		if low.find(String(old)) >= 0:
			return String(Binder.LEGACY_TO_DESK[old])
	return String({"cash_delta": "the bank", "traction_delta": "customers",
		"product_delta": "what we make", "morale_delta": "team",
		"hype_delta": "the street"}.get(op, ""))

## THE WEEK'S CHIPS (13-binder § this week): every desk-suggested action in
## one pressable strip. Prefill chips APPEND to the draft — never overwrite;
## a standing draft gains the suggestion on its own line. Jump chips walk to
## the suggesting desk (payload = the control to spotlight) and leave a back
## pill. Suggestions are ADOPT-only — pressing a chip never arms anything.
static func _chip_strip(b, s: GameState, x: float, y: float) -> void:
	var rows := DeskKit.collect_suggestions(s, _desk_ids())
	if rows.is_empty():
		b.label("no desk is suggesting a move yet — suggestions land here as chips",
			Vector2(x, y + 6.0), 17, Color(DeskKit.INK, 0.45), 1000.0)
		return
	b.label("suggested:", Vector2(x, y + 6.0), 17, Color(DeskKit.INK, 0.5), 110.0)
	var cx := x + 104.0
	var limit := DeskKit.X_ID + 1120.0 - DeskKit.CARD_PAD
	for i in rows.size():
		var rd: Dictionary = rows[i]
		var jump := String(rd.get("kind", "")) == "jump"
		# generated labels come pre-fit (S6) — the chip draws what it is given
		var cap := DeskKit.fit_text(b, String(rd.get("label", "")), 240.0, 19) \
			+ (" ->" if jump else "")
		var w: float = b.font().get_string_size(cap,
			HORIZONTAL_ALIGNMENT_LEFT, -1, 19).x + 36.0
		if cx + w > limit:
			b.label("+%d more" % (rows.size() - i), Vector2(cx, y + 6.0), 17,
				Color(DeskKit.INK, 0.5), 120.0)
			break
		# payload shapes in the wild: prefill = the draft text (String); jump =
		# a control id String OR {desk, control} (the shipped desks' form) —
		# accept both, never crash on a foreign desk's shape
		var pv: Variant = rd.get("payload", "")
		var jump_desk := String(rd.get("desk", ""))
		var jump_control := ""
		if pv is Dictionary:
			jump_desk = String((pv as Dictionary).get("desk", jump_desk))
			jump_control = String((pv as Dictionary).get("control", ""))
		elif pv is String:
			jump_control = String(pv)
		var prefill_text := String(pv) if pv is String else str(pv)
		var press := (func() -> void:
			b.focus_desk(jump_desk, jump_control, "this week")) if jump \
			else (func() -> void: _adopt_prefill(prefill_text))
		cx = DeskKit.chip(b, cx, y, {"text": cap, "kind": "person", "on_press": press})

## The desk ids the chips sweep — the binder's own GROUPS, never a local list.
static func _desk_ids() -> Array:
	var out: Array = []
	for g in Binder.GROUPS:
		out.append_array((g as Dictionary).get("desks", []))
	return out

## Prefill = APPEND. A chip never eats a half-written move: a standing draft
## keeps its words and the suggestion joins after " — " — the composer's own
## append grammar (the clarify law the footer teaches). A literal newline was
## tried first and the single-line composer renders it as nothing: two moves
## fused into one word — worse than any separator.
static func _adopt_prefill(text: String) -> void:
	if draft.strip_edges() == "":
		draft = text
	else:
		draft += " — " + text

## THE STAGED LIST — the run's own action log, this week's entries, plus the
## standing intents other desks armed (the pivot, an open raise).
static func _staged_rows(s: GameState) -> Array:
	var rows: Array = []
	if not SimPivot.armed(s).is_empty():
		rows.append({"label": "THE PIVOT — armed, fires at this LOCK IN",
			"col": DeskKit.ALERT})
	if s.has_flag("fundraising_open"):
		rows.append({"label": "term sheets on the table — they expire", "col": DeskKit.PEN})
	for h in s.history:
		var hd: Dictionary = h
		if int(hd.get("week", -1)) == s.week:
			rows.append({"label": String(hd.get("entry", ""))})
	return rows

## The composer's paper: a bare LineEdit in the binder's hand. The words live
## in the static draft so closing the binder never eats a half-written move.
## Registers "move_field" (S2b) so jumps can land ON the pen; the DO lane's
## [write the move] sets desk.focus_move and the next draw hands it the caret.
static func _move_field(b, x: float, y: float) -> void:
	var le := LineEdit.new()
	le.add_theme_font_override("font", b.font())
	le.add_theme_font_size_override("font_size", 28)
	le.add_theme_color_override("font_color", DeskKit.INK)
	le.add_theme_color_override("font_placeholder_color", Color(DeskKit.INK, 0.28))
	le.add_theme_color_override("caret_color", DeskKit.PEN)
	for st in ["normal", "focus", "read_only"]:
		le.add_theme_stylebox_override(st, StyleBoxEmpty.new())
	le.placeholder_text = "what do we do this week?"
	le.text = draft
	le.position = Vector2(x, y)
	le.set_deferred("size", Vector2(1060.0, 44.0))
	le.text_changed.connect(func(t: String) -> void:
		draft = t)
	b.pane().add_child(le)
	b.mark_control("move_field", Rect2(x, y, 1060.0, 44.0))
	if bool(b.desk.get("focus_move", false)):
		b.desk.erase("focus_move")
		le.call_deferred("grab_focus")
		le.call_deferred("set_caret_column", draft.length())
		b.spotlight(Rect2(x, y, 1060.0, 44.0))
	DeskKit.pen_rule(b, y + 40.0, x, 1060.0, Color(DeskKit.SAGE, 0.75), 5)

## The roll leaves through the host seam — the garage's own commit path.
static func _fire_lock(b) -> void:
	if lock_hook.is_valid():
		lock_hook.call(draft.strip_edges())

# ── S8/S10 — the rail speaks for the desk (has-method-guarded, never must) ────

## The landing tab never sleeps — the week is always being played.
static func is_dormant(_state) -> bool:
	return false

## The tab's four characters: how much the week already carries into the roll.
static func micro_status(state) -> String:
	var n := _staged_rows(state).size()
	return ("%d armed" % n) if n > 0 else ""

static func handle(b, id: String) -> void:
	match id:
		"lock":
			if SimEngine.preroll_items(b.state).is_empty():
				_fire_lock(b)
			else:
				b.desk["mode"] = "preroll"
		"pre:roll":
			b.desk.erase("mode")
			_fire_lock(b)
		"pre:fix":
			var items := SimEngine.preroll_items(b.state)
			b.desk.erase("mode")
			if not items.is_empty():
				b.focus_desk(String((items[0] as Dictionary).get("desk", "threats")))
