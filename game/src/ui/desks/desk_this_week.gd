class_name DeskThisWeek
extends RefCounted
## DESK — THE LOG · "this week", the desk you play from. W2 lane: L-COMPANY.
## THE QUESTION THIS DESK ANSWERS: "what happened, and what's our move?"
## Spec: docs/design/12-binder-rework-2.md § this week. This is the binder's
## default landing tab (the UI spine set that; this file fills the content).
##
##   HERO      week number + era + one plain sentence of the week's situation
##   1 THE CARD          the event and its bite (art slot at the left)
##   2 YOUR MOVE         the written-move composer — the SAME input flow the
##                       journal runs (clarify answers append after " — ");
##                       this desk only HOSTS the words, the garage's
##                       existing commit path judges them
##   3 ARMED THIS WEEK   the receipt list of changes staged since the last
##                       roll (read from the run's own action log)
##   4 LOCK IN           the die — with THE PRE-ROLL REVIEW intercept
##                       (SimEngine.preroll_items) before any roll
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
	var y := DeskKit.hero_band(b, "week %d" % s.week,
		"%s · %s" % [s.era_display_name(), SimEngine.health_band(s).to_lower()],
		DeskKit.INK)

	# 1 · THE CARD — the event and its bite, art slot left
	var z1 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, 132.0, 1, "the card", "")
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
			Vector2(tx, float(z1.content_y) - 14.0), DeskKit.ROW,
			Color(DeskKit.INK, 0.75), 1000.0)
		b.label("this week's card opens with the journal", Vector2(tx,
			float(z1.content_y) + 20.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.5), 900.0)
	y = float(z1.bottom) + 12.0

	# 2 · YOUR MOVE — the composer (the same flow; clarify appends after " — ")
	var z2 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, 128.0, 2, "your move",
		"plain words — the world asks a question when a number is missing")
	_move_field(b, float(z2.content_x), float(z2.cursor) - 6.0)
	y = float(z2.bottom) + 12.0

	# 3 · ARMED THIS WEEK — everything staged since the last roll
	var staged := _staged_rows(s)
	var z3_h := 66.0 + maxf(float(mini(staged.size(), 4)) * 30.0, 28.0) + 10.0
	var z3 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, z3_h, 3, "armed this week",
		"the receipt list — what the week will carry into the roll")
	var ry := float(z3.cursor) - 8.0
	if staged.is_empty():
		b.label("nothing staged since the last roll — steppers, signatures and arranges land here",
			Vector2(float(z3.content_x), ry), DeskKit.DETAIL, Color(DeskKit.INK, 0.5), 1040.0)
	for i in mini(staged.size(), 4):
		var row: Dictionary = staged[i]
		b.label("· " + String(row.get("label", "")), Vector2(float(z3.content_x), ry),
			DeskKit.DETAIL, row.get("col", Color(DeskKit.INK, 0.8)), 1000.0)
		ry += 30.0
	if staged.size() > 4:
		b.label("+%d more in the log" % (staged.size() - 4),
			Vector2(float(z3.content_x), ry), 17, Color(DeskKit.INK, 0.5), 400.0)
	y = float(z3.bottom) + 12.0

	# 4 · LOCK IN — the die, behind THE PRE-ROLL REVIEW
	var z4 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, 128.0, 4, "lock in",
		"the die is cast at the press — the review reads the threats list first")
	var ay := float(z4.cursor) - 6.0
	var outstanding := SimEngine.preroll_items(s)
	if String(b.desk.get("mode", "")) == "preroll":
		var shown := 0
		for it in outstanding:
			if shown >= 2:
				break
			var itd: Dictionary = it
			b.label("%s%s — %s" % ["! " if int(itd.get("severity", 2)) >= 3 else "",
				String(itd.get("desk", "")), String(itd.get("label", ""))],
				Vector2(float(z4.content_x), ay), DeskKit.DETAIL, DeskKit.PEN, 700.0)
			ay += 28.0
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
					b.focus_desk(String((outstanding[0] as Dictionary).get("desk", "threats"))),
			DeskKit.STATUS, DeskKit.INK, 200.0)
	elif lock_hook.is_valid():
		DeskKit.word(b, "LOCK IN — roll the week", Vector2(float(z4.content_x), ay),
			func() -> void:
				if SimEngine.preroll_items(b.state).is_empty():
					_fire_lock(b)
				else:
					b.desk["mode"] = "preroll", DeskKit.ROW, DeskKit.INK, 480.0)
		if not outstanding.is_empty():
			b.label("%d outstanding — the review will stop you once" % outstanding.size(),
				Vector2(float(z4.content_x) + 520.0, ay + 6.0), DeskKit.DETAIL,
				DeskKit.PEN, 460.0)
	else:
		b.label("the journal rolls the week — close the binder (TAB) and press LOCK IN",
			Vector2(float(z4.content_x), ay), DeskKit.DETAIL, Color(DeskKit.INK, 0.55), 900.0)
		if not outstanding.is_empty():
			b.label("%d outstanding items wait in the review" % outstanding.size(),
				Vector2(float(z4.content_x), ay + 28.0), DeskKit.DETAIL, DeskKit.PEN, 700.0)

	DeskKit.footer(b, {
		"computed": "",
		"rules": "one move a week · the DM judges the plan into a DC and the die decides "
			+ "· clarify answers append after \" — \"", "rules_y": 856.0})

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
	DeskKit.pen_rule(b, y + 40.0, x, 1060.0, Color(DeskKit.SAGE, 0.75), 5)

## The roll leaves through the host seam — the garage's own commit path.
static func _fire_lock(b) -> void:
	if lock_hook.is_valid():
		lock_hook.call(draft.strip_edges())

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
