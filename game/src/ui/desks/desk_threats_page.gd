class_name DeskThreatsPage
extends RefCounted
## DESK — THE COMPANY · "threats", the command center. W2 lane: L-COMPANY.
## THE QUESTION THIS DESK ANSWERS: "what could kill us?"
## Spec: docs/design/12-binder-rework-2.md § threats + 13-binder-ux.md § threats
## (the command center behaves like a to-do list).
##
##   HERO      the loudest attention item BIG with its desk named, the count
##             of the rest under it
##   THE LIST  every attention row: severity dot · plain-words label · an age
##             chip once a row has stood ≥2 weeks · the desk word as a
##             pressable jump (jump_to_ask: rows that carry a `control` land
##             spotlit on the switch; the rest land on the desk). Ordered
##             severity first, then age — the longest-ignored rises.
##             NO FOLDING: if the list is long, that IS the message.
##   THE SPILLOVER  the clocks, the conditions and the standing costs stay —
##             compressed under the list, the old page's strips.
##   DO LANE   [fix first — top item] — one press walks to the loudest switch.
##
## This is the SAME list behind every bang in the game (the engine's
## attention registry): the tab marks, the garage badge, the pre-roll review
## and this page can never disagree.

const QUESTION := "what could kill us?"

static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	var rows := _ordered(s)
	if rows.is_empty():
		return {"big": "nothing is shouting", "line": "that never lasts"}
	var top: Dictionary = rows[0]
	return {"big": "%d live" % rows.size(),
		"line": "%s — %s" % [String(top.get("label", "")), _desk_word(String(top.get("desk", "")))]}

## The engine's attention registry still speaks the old tab names ("crew",
## "pricing", "the ledger") — the page shows the desk the binder actually
## opens, through the binder's own legacy map.
static func _desk_word(d: String) -> String:
	return String(Binder.LEGACY_TO_DESK.get(d, d))

## THE COMMAND CENTER'S OWN ORDER: severity first, then AGE — between two rows
## shouting equally loud, the one ignored longest rises. The engine's order
## (already severity-first) is kept as the stable tiebreak.
static func _ordered(s: GameState) -> Array:
	var rows := SimEngine.attention_items(s)
	var idx := 0
	for r in rows:
		(r as Dictionary)["_i"] = idx
		idx += 1
	rows.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		var sa := int(a.get("severity", 1))
		var sb := int(b.get("severity", 1))
		if sa != sb:
			return sa > sb
		var aa := int(a.get("since_wk", 0))
		var ab := int(b.get("since_wk", 0))
		if aa != ab:
			return aa < ab
		return int(a.get("_i", 0)) < int(b.get("_i", 0)))
	for r2 in rows:
		(r2 as Dictionary).erase("_i")
	return rows

static func draw(b) -> void:
	var s: GameState = b.state
	var rows := _ordered(s)

	# S1 — a fully quiet company: teach what the page is FOR
	if rows.is_empty() and s.clocks.is_empty() and s.statuses.is_empty() \
			and s.commitments.is_empty():
		DeskKit.zero_state(b, {
			"will_show": "every red mark in the game — one list, loudest first",
			"would_line": "each row WOULD name its ask, wear its age in weeks, and walk you to the switch that fixes it",
			"action_label": "back to this week",
			"action_cb": func() -> void: b.focus_desk("this week"),
			"wakes_hint": "wakes the first time anything goes red — the tab wears the loudest mark",
		})
		return

	var y := 6.0

	# HERO — the loudest item BIG, its desk named, the count of the rest
	if rows.is_empty():
		y = DeskKit.hero_band(b, "nothing is shouting",
			"that never lasts — the clocks below keep ticking", DeskKit.INK)
	else:
		var top: Dictionary = rows[0]
		y = DeskKit.hero_band(b,
			"%s — %s" % [String(top.get("label", "")), _desk_word(String(top.get("desk", "")))],
			("%d more on the list, loudest first" % (rows.size() - 1))
				if rows.size() > 1 else "the only thing shouting this week",
			DeskKit.ALERT if int(top.get("severity", 1)) >= 3 else DeskKit.INK)

	# THE LIST — every row, no folding: a long list IS the message
	for it in rows:
		var itd: Dictionary = it
		var age := s.week - int(itd.get("since_wk", s.week))
		DeskKit.sev_dot(b, DeskKit.X_ID, y + 6.0, int(itd.get("severity", 1)))
		DeskKit.fit_line(b, String(itd.get("label", "")), Vector2(DeskKit.X_ID + 36.0, y),
			28, DeskKit.PEN if int(itd.get("severity", 1)) >= 3
			else Color(DeskKit.INK, 0.85), 750.0 if age >= 2 else 800.0)
		# S5 — the age chip: a row that has stood ≥2 weeks says so
		if age >= 2:
			DeskKit.clock_chip(b, DeskKit.X_ID + 800.0, y + 2.0, "%d wks" % age)
		# S2b — the row itself knows its switch: jump_to_ask reads the row's
		# desk AND its control key, so a filled control lands spotlit; the
		# source leaves the free back pill
		DeskKit.word(b, _desk_word(String(itd.get("desk", ""))) + " ->",
			Vector2(DeskKit.X_ID + 900.0, y - 4.0),
			func() -> void: b.jump_to_ask(itd, "threats"),
			DeskKit.STATUS, DeskKit.PEN, 220.0)
		y += 46.0
	y += 10.0

	# THE SPILLOVER — the clocks, the weather, the standing costs (compressed)
	if not (s.clocks.is_empty() and s.statuses.is_empty() and s.commitments.is_empty()):
		y = DeskKit.pen_rule(b, y + 4.0)
		for c in s.clocks:
			if y > 740.0:
				break
			var cd: Dictionary = c
			b.clock(Vector2(DeskKit.X_ID, y + 2.0), 26.0)
			DeskKit.fit_line(b, "in %d wks: %s" % [int(cd.get("weeks_left", 0)),
				String(cd.get("consequence", ""))],
				Vector2(DeskKit.X_ID + 36.0, y), DeskKit.DETAIL, DeskKit.PEN, 1060.0)
			y += 36.0
		for st in s.statuses:
			if y > 740.0:
				break
			var sd: Dictionary = st
			var kind := String(SimEngine.STATUS.get(String(sd.get("name", "")), {})
				.get("kind", "condition"))
			DeskKit.fit_line(b, "%s %s — %d wks left" % ["helping:" if kind == "buff" else "hurting:",
				String(sd.get("name", "")).replace("_", " "), int(sd.get("weeks_left", 0))],
				Vector2(DeskKit.X_ID + 36.0, y), DeskKit.DETAIL,
				DeskKit.SAGE if kind == "buff" else DeskKit.PEN, 1060.0)
			y += 36.0
		for cm in s.commitments:
			if y > 740.0:
				break
			var cmd: Dictionary = cm
			# Law 2 — the amount rides its own right-aligned column, not the prose
			DeskKit.fit_line(b, "standing: %s — %d more wks" % [String(cmd.get("name", "")),
				int(cmd.get("weeks_left", 0))],
				Vector2(DeskKit.X_ID + 36.0, y), DeskKit.DETAIL, DeskKit.BLUE, 800.0)
			var cv: Label = DeskKit.fit_line(b, "$%s/wk" % b.fmt(absi(int(cmd.get("cash_wk", 0)))),
				Vector2(DeskKit.X_ID + 860.0, y), DeskKit.DETAIL, DeskKit.BLUE, 200.0)
			cv.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
			y += 36.0

	# S3 — the one thing to do here: walk to the loudest switch
	if not rows.is_empty():
		var top_row: Dictionary = rows[0]
		DeskKit.do_lane(b, [{
			"label": "fix first — " + String(top_row.get("label", "")),
			"cb": func() -> void: b.jump_to_ask(top_row, "threats"),
			"tier": ""}])

	DeskKit.footer(b, {
		"computed": "%d rows live · the loudest is what the tab wears" % rows.size(),
		"rules": "this same list is what THE PRE-ROLL REVIEW reads before any dice — "
			+ "fix them, or roll and live with it · every row names the desk that owns the fix",
		"y": 820.0, "rules_y": 852.0})

## A press inside this desk: rows jump to the desk that owns the fix — a
## focus_desk on the binder, never a mutation here.
static func handle(b, id: String) -> void:
	if id.begins_with("go:"):
		b.focus_desk(id.substr(3), "", "threats")

# ── the desk conventions (S8) — the rail reads these ─────────────────────────

static func is_dormant(_state) -> bool:
	return false

## The rail's right-aligned word: how many things are shouting.
static func micro_status(state) -> String:
	var s: GameState = state
	var n := SimEngine.attention_items(s).size()
	return ("%d live" % n) if n > 0 else ""
