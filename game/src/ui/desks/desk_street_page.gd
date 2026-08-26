class_name DeskStreetPage
extends RefCounted
## DESK — THE COMPANY · "the street". W2 lane: L-COMPANY.
## THE QUESTION THIS DESK ANSWERS: "what is the world doing to us?"
## Spec: docs/design/12-binder-rework-2.md § the street + 11-binder-rework.md
## (hero = the season banner as drawn weather + one sentence).
##
## Zones, top to bottom (the numbered didactic spine):
##   HERO        the weather answers the question in one second
##   1 THE WEATHER          the drawn season band + what changes THIS week
##   2 THE RIVALS           a card per rival: posture chips, heat dot, the
##                          last-3 record with week stamps; an act that came
##                          at YOU stays face-up; "the other N →" folds
##   3 THE INVESTORS' MOOD  the multiples band + the appetite word — the
##                          raise's radar, fed from here
##   4 TAKEN FROM US        poaches, price wars, disruptors — each row with
##                          its counter-desk jump
##
## The street is a page you READ; its only controls are jumps. Word-maps come
## from SimStreet (once, both engines) — no raw float ever prints.

const QUESTION := "what is the world doing to us?"

## The group overview's card reads this: the page's hero, one number + one
## sentence (DECISIONS: the quartet card IS the page's hero verbatim).
static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	return {"big": _season_big(s), "line": _week_sentence(s)}

# ── the weather's words ───────────────────────────────────────────────────────

static func _season_big(s: GameState) -> String:
	match SimStreet.season(s):
		"winter":
			return "funding winter"
		"boom":
			return "a boom"
	match SimStreet.trend_band(s.market_trend):
		"tailwinds":
			return "tailwinds"
		"headwinds":
			return "headwinds"
	return "a calm street"

## One sentence on what the season changes THIS week — the shock named
## plainly (valuations ×0.6), or the trend's own read.
static func _week_sentence(s: GameState) -> String:
	if SimEngine.has_status(s, "funding_winter"):
		return "checks shrink and terms bite — valuations ×0.6 · %d wks left" \
			% SimStreet.weeks_left(s, "funding_winter")
	if SimEngine.has_status(s, "boom"):
		return "every round oversubscribed — valuations ×1.3 · %d wks left" \
			% SimStreet.weeks_left(s, "boom")
	if SimEngine.has_status(s, "winter_watch"):
		return String(SimStreet.BANNER.get("winter_watch", ""))
	if SimEngine.has_status(s, "boom_watch"):
		return String(SimStreet.BANNER.get("boom_watch", ""))
	return SimStreet.season_read(s.market_trend)

## The rivals ranked by threat: strength high first, and anyone who came at
## YOU this season (poach, price cut, sniff) never sinks below the fold.
static func _ranked_rivals(s: GameState) -> Array:
	var rows := s.rivals.duplicate()
	rows.sort_custom(func(a, b) -> bool:
		return float((a as Dictionary).get("strength", 0.0)) \
			> float((b as Dictionary).get("strength", 0.0)))
	var faced: Array = []
	var calm: Array = []
	for r in rows:
		if _came_at_you(r):
			faced.append(r)
		else:
			calm.append(r)
	return faced + calm

static func _came_at_you(rd: Dictionary) -> bool:
	if int(rd.get("sniffing", 0)) > 0:
		return true
	return String(rd.get("last_action", "")) in ["poach", "price_cut", "sniff"]

## The heat dot: how hard this rival could hurt you right now.
static func _heat(s: GameState, rd: Dictionary) -> int:
	var gap := float(rd.get("strength", 20.0)) - SimStreet.player_power(s)
	if gap > 15.0 or _came_at_you(rd):
		return 3
	return 2 if gap > 0.0 else 1

## The appetite word THE RAISE's radar shares — read off the raise inputs
## (the ownership lane's interest score) and the sky. A word-map, never a float.
static func _appetite(s: GameState) -> String:
	if SimEngine.has_status(s, "funding_winter"):
		return "cold — fewer inbound knocks"
	var rs: Dictionary = s.raise_state
	var score := float(rs.get("interest_score", 0.0))
	if score >= 50.0:
		return "hungry — knocks likely"
	if score >= 25.0:
		return "warm — worth a call"
	if score > 0.0:
		return "curious — traction talks first"
	return "quiet — nobody is dialing yet"

## THE WIRE's rows: what was taken from us, or is being circled — each with
## its counter-desk. Derived from the same records the lanes keep.
static func _wire_rows(s: GameState) -> Array:
	var rows: Array = []
	if SimEngine.has_status(s, "price_war"):
		var down := int(round((1.0 - SimEngine.street_fair_mult(s)) * 100.0))
		rows.append({"label": "price war — the going rate is down %d%% (%d wks left)"
			% [down, SimStreet.weeks_left(s, "price_war")], "desk": "offers"})
	if int(s.get_meta("poach_wk", -1)) == s.week:
		rows.append({"label": "%s was called with a number this week"
			% String(s.get_meta("poach_name", "someone")), "desk": "team"})
	elif int(s.get_meta("poach_failed_wk", -1)) == s.week:
		rows.append({"label": "%s was called — they stayed, this time"
			% String(s.get_meta("poach_failed_name", "someone")), "desk": "team"})
	for r in s.rivals:
		var rd: Dictionary = r
		if int(rd.get("sniffing", 0)) > 0:
			rows.append({"label": "%s is circling — asking your price"
				% String(rd.get("name", "a rival")), "desk": "cap table"})
		elif String(rd.get("focus", "")) == "price" \
				and float(rd.get("price_posture", 1.0)) <= 0.92:
			rows.append({"label": "%s is undercutting from below your price umbrella"
				% String(rd.get("name", "a rival")), "desk": "offers"})
	return rows

# ── the page ──────────────────────────────────────────────────────────────────

static func draw(b) -> void:
	var s: GameState = b.state
	if String(b.desk.get("mode", "")) == "rivals":
		_draw_all_rivals(b, s)
		return

	# HERO — the weather answers the tab's question in one second
	var y := DeskKit.hero_band(b, _season_big(s), _week_sentence(s),
		_season_col(s), 6.0, false)

	# 1 · THE WEATHER — the drawn season band, weeks left on the clock chip
	var z1 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, 92.0, 1, "the weather", "")
	var band_col := _season_col(s)
	DeskKit.meter(b, float(z1.content_x), float(z1.content_y) + 4.0, 560.0, 1.0,
		band_col, SimStreet.season_read(s.market_trend))
	var shock := ""
	for key in ["funding_winter", "boom"]:
		if SimEngine.has_status(s, String(key)):
			shock = String(key)
	if shock != "":
		DeskKit.clock_chip(b, float(z1.content_x) + 950.0, float(z1.content_y) + 2.0,
			"%d wks left" % SimStreet.weeks_left(s, shock))
	y = float(z1.bottom) + 12.0

	# 2 · THE RIVALS — the record is the tell
	var ranked := _ranked_rivals(s)
	var z2_h := 74.0 + float(mini(ranked.size(), 3)) * 80.0 \
		+ (44.0 if ranked.size() > 3 else 0.0) + 6.0
	if ranked.is_empty():
		z2_h = 74.0 + 70.0
	var z2 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, z2_h, 2, "the rivals",
		"read the record, not the vibes — the pattern is the tell")
	var ry := float(z2.cursor)
	if ranked.is_empty():
		DeskKit.empty(b, Vector2(float(z2.content_x), ry),
			"nobody is competing with you this week.",
			"that is rarer, and more temporary, than it feels.")
	for i in mini(ranked.size(), 3):
		var rd: Dictionary = ranked[i]
		DeskKit.sev_dot(b, float(z2.content_x), ry + 2.0, _heat(s, rd))
		b.label(String(rd.get("name", "?")), Vector2(float(z2.content_x) + 34.0, ry - 4.0),
			DeskKit.ROW, DeskKit.INK, 380.0)
		if _came_at_you(rd):
			b.label("→ they came at YOU", Vector2(float(z2.content_x) + 430.0, ry),
				DeskKit.DETAIL, DeskKit.ALERT, 300.0)
		b.label(SimEngine._fuzz(float(rd.get("strength", 20.0))),
			Vector2(float(z2.content_x) + 900.0, ry), DeskKit.DETAIL,
			Color(DeskKit.INK, 0.6), 180.0)
		var cx := float(z2.content_x) + 34.0
		cx = DeskKit.chip(b, cx, ry + 26.0,
			{"text": SimStreet.vigor_word(float(rd.get("vigor", 55.0))), "kind": "person"})
		cx = DeskKit.chip(b, cx, ry + 26.0,
			{"text": SimStreet.posture_word(float(rd.get("price_posture", 1.0))), "kind": "person"})
		cx = DeskKit.chip(b, cx, ry + 26.0,
			{"text": "fights on " + String(rd.get("focus", "growth")), "kind": "person"})
		DeskKit.chip(b, cx, ry + 26.0,
			{"text": SimStreet.hype_word(float(rd.get("hype", 20.0))), "kind": "person"})
		var log_all: Array = rd.get("log", [])
		var trail: Array = log_all.slice(maxi(log_all.size() - 3, 0))
		if not trail.is_empty():
			b.label("  ·  ".join(PackedStringArray(trail)),
				Vector2(float(z2.content_x) + 34.0, ry + 58.0), 17,
				Color(DeskKit.INK, 0.55), 1040.0)
		ry += 80.0
	if ranked.size() > 3:
		DeskKit.fold_row(b, float(z2.content_x), ry, ranked.size() - 3, "rivals",
			func() -> void: b.desk["mode"] = "rivals")
	y = float(z2.bottom) + 12.0

	# 3 · THE INVESTORS' MOOD — the raise's radar reads this
	var z3 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, 108.0, 3, "the investors' mood", "")
	b.label("the street pays ×%.1f the usual  ·  appetite: %s"
		% [SimEngine.shock_val_mult(s), _appetite(s)],
		Vector2(float(z3.content_x), float(z3.content_y)), DeskKit.DETAIL,
		Color(DeskKit.INK, 0.85), 800.0)
	var names := PackedStringArray()
	for inv in s.investors.slice(0, 3):
		names.append(String((inv as Dictionary).get("name", "?")))
	var book := "in the book: " + ", ".join(names) if names.size() > 0 \
		else "no investors in the book yet"
	if s.investors.size() > 3:
		book += "  +%d more" % (s.investors.size() - 3)
	b.label(book, Vector2(float(z3.content_x), float(z3.content_y) + 30.0),
		DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 760.0)
	DeskKit.word(b, "feeds THE RAISE →", Vector2(float(z3.content_x) + 840.0,
		float(z3.content_y) + 12.0), func() -> void: b.focus_desk("the raise"),
		DeskKit.DETAIL, Color(DeskKit.INK, 0.7), 260.0)
	y = float(z3.bottom) + 12.0

	# 4 · TAKEN FROM US / THE WIRE — every row names its counter-desk
	var wire := _wire_rows(s)
	var z4_h := 52.0 + maxf(float(mini(wire.size(), 2)) * 30.0, 28.0) + 6.0
	var z4 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, z4_h, 4, "taken from us", "")
	var wy := float(z4.content_y) - 14.0
	if wire.is_empty():
		b.label("nothing taken this week — the street is only resting.",
			Vector2(float(z4.content_x), wy), DeskKit.DETAIL, Color(DeskKit.INK, 0.55), 1000.0)
	for i2 in mini(wire.size(), 2):
		var row: Dictionary = wire[i2]
		var tail := ("   · +%d more on threats" % (wire.size() - 2)) \
			if i2 == 1 and wire.size() > 2 else ""
		DeskKit.sev_dot(b, float(z4.content_x), wy + 2.0, 2)
		b.label(String(row.get("label", "")) + tail,
			Vector2(float(z4.content_x) + 32.0, wy),
			DeskKit.DETAIL, Color(DeskKit.INK, 0.85), 810.0)
		var dsk := String(row.get("desk", ""))
		DeskKit.word(b, dsk + " →", Vector2(float(z4.content_x) + 880.0, wy - 6.0),
			func() -> void: b.focus_desk(dsk), DeskKit.DETAIL, DeskKit.PEN, 200.0)
		wy += 30.0

	var pressure := 0.0
	for rv in s.rivals:
		pressure += float((rv as Dictionary).get("strength", 0.0))
	pressure = minf(pressure / maxf(float(s.rivals.size()), 1.0) / 100.0 * 0.5, 0.45)
	DeskKit.footer(b, {
		"computed": "rival pressure is shaving %d%% off adoption · the trend multiplies every sale ×%.2f"
			% [int(round(pressure * 100.0)), s.market_trend],
		"rules": "none of this is yours to change from here — the street acts, the desks answer",
		"y": 820.0, "rules_y": 852.0})

## THE FULL LIST — every rival's rap sheet, the old street's own grammar.
static func _draw_all_rivals(b, s: GameState) -> void:
	DeskKit.back(b, "← the street", func() -> void:
		b.desk.erase("mode"))
	var y := 64.0
	for rv in _ranked_rivals(s):
		var rd: Dictionary = rv
		var log_all: Array = rd.get("log", [])
		y = DeskKit.log_block(b, y, {
			"identity": "%s — %s" % [String(rd.get("name", "?")),
				SimEngine._fuzz(float(rd.get("strength", 20.0)))],
			"posture": SimStreet.posture_line(rd),
			"plays": "plays: " + ", ".join(rd.get("tactics", [])),
			"trail": log_all.slice(maxi(log_all.size() - 3, 0)),
		})

static func handle(b, id: String) -> void:
	if id.begins_with("go:"):
		b.focus_desk(id.substr(3))
	elif id == "rivals":
		b.desk["mode"] = "rivals"
	elif id == "back":
		b.desk.erase("mode")

static func _season_col(s: GameState) -> Color:
	match SimStreet.season(s):
		"winter":
			return DeskKit.PEN
		"boom":
			return DeskKit.SAGE
	return DeskKit.BLUE
