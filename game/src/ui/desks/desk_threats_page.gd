class_name DeskThreatsPage
extends RefCounted
## DESK — THE COMPANY · "threats", the command center. W2 lane: L-COMPANY.
## THE QUESTION THIS DESK ANSWERS: "what could kill us?"
## Spec: docs/design/12-binder-rework-2.md § threats + 11-binder-rework.md.
##
##   HERO      the loudest attention item BIG with its desk named, the count
##             of the rest under it
##   THE LIST  every attention row: severity dot · plain-words label · the
##             desk word as a pressable jump (focus_desk). Loudest first.
##             NO FOLDING: if the list is long, that IS the message.
##   THE SPILLOVER  the clocks, the conditions and the standing costs stay —
##             compressed under the list, the old page's strips.
##
## This is the SAME list behind every bang in the game (the engine's
## attention registry): the tab marks, the garage badge, the pre-roll review
## and this page can never disagree.

const QUESTION := "what could kill us?"

static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	var rows := SimEngine.attention_items(s)
	if rows.is_empty():
		return {"big": "nothing is shouting", "line": "that never lasts"}
	var top: Dictionary = rows[0]
	return {"big": "%d live" % rows.size(),
		"line": "%s — %s" % [String(top.get("label", "")), String(top.get("desk", ""))]}

static func draw(b) -> void:
	var s: GameState = b.state
	var rows := SimEngine.attention_items(s)
	var y := 6.0

	# HERO — the loudest item BIG, its desk named, the count of the rest
	if rows.is_empty():
		y = DeskKit.hero_band(b, "nothing is shouting",
			"that never lasts — the clocks below keep ticking", DeskKit.INK)
	else:
		var top: Dictionary = rows[0]
		y = DeskKit.hero_band(b,
			"%s — %s" % [String(top.get("label", "")), String(top.get("desk", ""))],
			("%d more on the list, loudest first" % (rows.size() - 1))
				if rows.size() > 1 else "the only thing shouting this week",
			DeskKit.ALERT if int(top.get("severity", 1)) >= 3 else DeskKit.INK)

	# THE LIST — every row, no folding: a long list IS the message
	for it in rows:
		var itd: Dictionary = it
		DeskKit.sev_dot(b, DeskKit.X_ID, y + 6.0, int(itd.get("severity", 1)))
		b.label(String(itd.get("label", "")), Vector2(DeskKit.X_ID + 36.0, y),
			28, DeskKit.PEN if int(itd.get("severity", 1)) >= 3
			else Color(DeskKit.INK, 0.85), 800.0)
		var dsk := String(itd.get("desk", ""))
		DeskKit.word(b, dsk + " ->", Vector2(DeskKit.X_ID + 900.0, y - 4.0),
			func() -> void: b.focus_desk(dsk), DeskKit.STATUS, DeskKit.PEN, 220.0)
		y += 46.0
	y += 10.0

	# THE SPILLOVER — the clocks, the weather, the standing costs (compressed)
	if s.clocks.is_empty() and s.statuses.is_empty() and s.commitments.is_empty() \
			and rows.is_empty():
		b.label("nothing ticking. that never lasts.", Vector2(DeskKit.X_ID, y), 30,
			Color(DeskKit.INK, 0.6))
		y += 44.0
	if not (s.clocks.is_empty() and s.statuses.is_empty() and s.commitments.is_empty()):
		y = DeskKit.pen_rule(b, y + 4.0)
		for c in s.clocks:
			if y > 780.0:
				break
			var cd: Dictionary = c
			b.clock(Vector2(DeskKit.X_ID, y + 2.0), 26.0)
			b.label("in %d wks: %s" % [int(cd.get("weeks_left", 0)),
				String(cd.get("consequence", ""))],
				Vector2(DeskKit.X_ID + 36.0, y), DeskKit.DETAIL, DeskKit.PEN, 1060.0)
			y += 36.0
		for st in s.statuses:
			if y > 780.0:
				break
			var sd: Dictionary = st
			var kind := String(SimEngine.STATUS.get(String(sd.get("name", "")), {})
				.get("kind", "condition"))
			b.label("%s %s — %d wks left" % ["helping:" if kind == "buff" else "hurting:",
				String(sd.get("name", "")).replace("_", " "), int(sd.get("weeks_left", 0))],
				Vector2(DeskKit.X_ID + 36.0, y), DeskKit.DETAIL,
				DeskKit.SAGE if kind == "buff" else DeskKit.PEN, 1060.0)
			y += 36.0
		for cm in s.commitments:
			if y > 780.0:
				break
			var cmd: Dictionary = cm
			b.label("standing: %s — $%d/wk for %d more wks" % [String(cmd.get("name", "")),
				absi(int(cmd.get("cash_wk", 0))), int(cmd.get("weeks_left", 0))],
				Vector2(DeskKit.X_ID + 36.0, y), DeskKit.DETAIL, DeskKit.BLUE, 1060.0)
			y += 36.0

	DeskKit.footer(b, {
		"computed": "%d rows live · the loudest is what the tab wears" % rows.size(),
		"rules": "this same list is what THE PRE-ROLL REVIEW reads before any dice — "
			+ "fix them, or roll and live with it · every row names the desk that owns the fix",
		"y": 820.0, "rules_y": 852.0})

## A press inside this desk: rows jump to the desk that owns the fix — a
## focus_desk on the binder, never a mutation here.
static func handle(b, id: String) -> void:
	if id.begins_with("go:"):
		b.focus_desk(id.substr(3))
