class_name DeskCustomersPage
extends RefCounted
## DESK — REVENUE · "customers" = THE SCOREBOARD (DECISIONS: owner pick E).
## THE QUESTION THIS DESK ANSWERS: "who is coming and staying?"
##
## The hero is the score: the count big, this week's +won/−lost colored, the
## kept% beside them, and the run-long chart as a wash — fourteen weeks of the
## only line that matters. Under it, two cards: THE FUNNEL, SMALL (four
## narrowing mouths) and WHAT ONE IS WORTH (pays / costs to win / stays /
## lifetime), plus the audience-density card (12-binder-rework-2 §Retrofits):
##   Consumer   = cohort retention bars (a class of 100, k weeks later)
##   SMB        = the biggest-5 strip (counts until accounts earn names)
##   Enterprise = the logo grid with seats + renewal clocks (pipeline reads)
##
## ANALYTICS FOG RULES PRESERVED: what the founder cannot see renders as "?"
## shapes, never as absence — an = min(analytics_level, era cap), the funnel
## needs an ≥ 1, the cohort read an ≥ 2, and the era gate says its own name.
## The Enterprise STAGE BOARD lives on "in motion" now — this page keeps the
## score for every audience.

const QUESTION := "who is coming and staying?"

## The two-column card row under the hero.
const CARDS_Y := 330.0
const LEFT_X := 10.0
const LEFT_W := 468.0
const RIGHT_X := 492.0
const RIGHT_W := 638.0

# ─────────────────────────────── the dispatch ────────────────────────────────

## The quartet card IS the page's hero verbatim (DECISIONS).
static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	var sc := _score(s)
	return {"big": "%d customers" % s.traction, "line": String(sc.get("line", ""))}

static func draw(b) -> void:
	var s: GameState = b.state
	var an := SimFunnel.analytics(s)
	var sc := _score(s)
	_hero(b, s, an, sc)
	_funnel_card(b, s, an)
	var y := _worth_card(b, s, an)
	_density_card(b, s, an, y)
	_foot(b, s, an)

static func handle(_b, _id: String) -> void:
	pass

# ─────────────────────────────── the score ───────────────────────────────────

## This week's score, from the funnel and the binder's own history. won = the
## week's arrivals; lost = what the count says walked out; kept% = what a
## class of 100 looks like 12 weeks later (the old cohort read's own math).
static func _score(s: GameState) -> Dictionary:
	var f := SimFunnel.funnel(s)
	var won := -1
	var lost := -1
	if not f.is_empty():
		won = int(round(SimFunnel.num(f, "adds")))
		var n := s.metric_history.size()
		if n >= 2:
			var prev := int((s.metric_history[n - 2] as Dictionary).get("customers", 0))
			lost = maxi(prev + won - s.traction, 0)
	var kept := _kept_pct(s)
	var parts: Array[String] = []
	if won >= 0:
		parts.append("+%d won · −%s lost this week" % [won, str(lost) if lost >= 0 else "?"])
	else:
		parts.append("no week on the books yet")
	if kept >= 0:
		parts.append("kept ≈%d%% after 12 weeks" % kept)
	else:
		parts.append("kept ? — analytics sees who stays")
	return {"won": won, "lost": lost, "kept": kept, "line": " · ".join(parts)}

## Survival of a class of 100 at week 12 — engine terms, no invention. −1 when
## the fog (an < 2) keeps it a "?".
static func _kept_pct(s: GameState) -> int:
	if SimFunnel.analytics(s) < 2:
		return -1
	var th := s.theta
	var residence := maxf(float(th.get("lifetime_wk", 40.0))
		* (0.4 + float(s.product) / 100.0 * 1.2), 2.0)
	return int(round(pow(maxf(1.0 - 1.0 / residence, 0.0), 12.0) * 100.0))

# ─────────────────────────────── the hero ────────────────────────────────────

static func _hero(b, s: GameState, an: int, sc: Dictionary) -> void:
	var big := "%d" % s.traction
	if an <= 0:
		big += ", give or take"
	b.label(big, Vector2(DeskKit.X_ID, 6.0), DeskKit.HERO_BIG, DeskKit.INK, 700.0)
	var bx: float = DeskKit.X_ID + b.font().get_string_size(big, HORIZONTAL_ALIGNMENT_LEFT, -1,
		DeskKit.HERO_BIG).x + 30.0
	b.label("customers", Vector2(bx, 34.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.5), 200.0)
	var won := int(sc.get("won", -1))
	var lost := int(sc.get("lost", -1))
	if won >= 0:
		b.label("+%d won" % won, Vector2(bx + 150.0, 10.0), 27, Color("5D7A50"), 200.0)
		b.label("−%s lost" % (str(lost) if lost >= 0 else "?"), Vector2(bx + 150.0, 44.0),
			27, DeskKit.PEN, 200.0)
	var kept := int(sc.get("kept", -1))
	var kl: Label = b.label("kept ≈%d%%" % kept if kept >= 0 else "kept ?",
		Vector2(830.0, 10.0), 34, DeskKit.SAGE if kept >= 0 else Color(DeskKit.INK, 0.4), 290.0)
	kl.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	var ks: Label = b.label("still here after 12 weeks" if kept >= 0
		else "invest in analytics to see who stays", Vector2(700.0, 52.0), 17,
		Color(DeskKit.INK, 0.5), 420.0)
	ks.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	if an <= 0:
		b.label("Traffic seems… decent? Someone signed up on Tuesday. The numbers live in a notebook you lost.",
			Vector2(DeskKit.X_ID, 84.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 1100.0)
		DeskKit.pen_rule(b, 150.0)
		b.label("the whole run — the chart returns with a notebook that survives (analytics)",
			Vector2(DeskKit.X_ID, 200.0), DeskKit.LAW, Color(DeskKit.INK, 0.4), 1100.0)
		return
	b.label("the whole run", Vector2(DeskKit.X_ID, 84.0), DeskKit.LAW,
		Color(DeskKit.INK, 0.45), 400.0)
	DeskKit.pen_rule(b, 150.0)
	b.spark(b.series("customers"), Vector2(10.0, 166.0), Vector2(1120.0, 148.0), DeskKit.SAGE)

# ─────────────────────────── the funnel, small ───────────────────────────────

## Four narrowing mouths; a fogged stage keeps its mouth and loses its number.
static func _funnel_card(b, s: GameState, an: int) -> void:
	var frame := DeskKit.card_frame(b, LEFT_X, CARDS_Y, LEFT_W, 384.0, "the funnel, small")
	var fx := float(frame.get("content_x", LEFT_X))
	var fy := float(frame.get("content_y", CARDS_Y))
	var f := SimFunnel.funnel(s)
	var known := an >= 1 and not f.is_empty()
	var stages: Array = [
		{"label": "reach", "value_text": b.fmt(int(round(SimFunnel.num(f, "reach_total")))), "known": known},
		{"label": "leads", "value_text": b.fmt(int(round(SimFunnel.num(f, "leads_total")))), "known": known},
		{"label": "signed", "value_text": b.fmt(int(round(SimFunnel.num(f, "adds")))), "known": known},
		{"label": "kept", "value_text": b.fmt(s.traction), "known": true},
	]
	fy = DeskKit.funnel_shape(b, fx, fy, LEFT_W - DeskKit.CARD_PAD * 2.0, stages)
	if not known:
		var why := "invest in analytics to see the funnel" if s.analytics_level <= 0 \
			else ("the funnel is dark here: attribution needs an office, not a garage."
				if an < 1 else "no week on the books yet — the funnel is measured, not predicted.")
		b.label(why, Vector2(fx, fy - 4.0), DeskKit.LAW, DeskKit.PEN if s.analytics_level <= 0
			else Color(DeskKit.INK, 0.5), LEFT_W - 40.0)

# ─────────────────────────── what one is worth ───────────────────────────────

static func _worth_card(b, s: GameState, an: int) -> float:
	var frame := DeskKit.card_frame(b, RIGHT_X, CARDS_Y, RIGHT_W, 196.0, "what one is worth")
	var ue: Dictionary = s.get_meta("unit_econ", {})
	var fogged := an < 1
	var arpu := float(ue.get("arpu", 0.0))
	var cac := int(ue.get("cac", 0))
	var ltv := int(ue.get("ltv", 0))
	var th := s.theta
	var stays := int(s.beliefs.get("lifetime_wk", th.get("lifetime_wk", 40)))
	DeskKit.money_row(b, frame, "pays weekly / costs to win (CAC)",
		"?" if fogged else "$%.0f / %s" % [arpu, ("$%s" % b.fmt(cac)) if cac > 0 else "?"],
		DeskKit.INK if not fogged else Color(DeskKit.INK, 0.4))
	DeskKit.money_row(b, frame, "stays about",
		"?" if fogged else "%d wks" % stays,
		DeskKit.INK if not fogged else Color(DeskKit.INK, 0.4))
	DeskKit.money_row(b, frame, "worth, lifetime (LTV)",
		"?" if fogged else (("$%s" % b.fmt(ltv)) if ltv > 0 else "?"),
		Color("5D7A50") if not fogged and ltv > 0 else Color(DeskKit.INK, 0.4))
	return float(frame.get("bottom", CARDS_Y + 196.0))

# ─────────────────────── the audience-density card ───────────────────────────

static func _density_card(b, s: GameState, an: int, top: float) -> void:
	var y := top + 14.0
	var h := 150.0
	match String(s.biz_who):
		"Enterprise":
			_logo_grid(b, s, y, h)
		"SMB":
			_biggest_five(b, s, y, h)
		_:
			_cohort_bars(b, s, an, y, h)

## CONSUMER: cohort retention bars — a class of 100, k weeks later. The read
## unlocks at analytics 2; below that the bars keep their shape and lose their
## wash (the fog is drawn, not hidden).
static func _cohort_bars(b, s: GameState, an: int, y: float, h: float) -> void:
	var frame := DeskKit.card_frame(b, RIGHT_X, y, RIGHT_W, h, "the cohorts — a class of 100")
	var cx := float(frame.get("content_x", RIGHT_X))
	var cy := float(frame.get("content_y", y)) - 6.0
	var known := an >= 2
	var th := s.theta
	var residence := maxf(float(th.get("lifetime_wk", 40.0))
		* (0.4 + float(s.product) / 100.0 * 1.2), 2.0)
	for k in [4, 8, 12, 16]:
		var frac := pow(maxf(1.0 - 1.0 / residence, 0.0), float(k))
		var note := "wk %d — %s still here" % [k,
			("%d of 100" % int(round(frac * 100.0))) if known else "?"]
		DeskKit.meter(b, cx, cy, 300.0, frac if known else 0.0,
			DeskKit.SAGE if known else Color(DeskKit.INK, 0.2), note)
		cy += 24.0
	if not known:
		b.label("unlocks at analytics 2 — below that nobody counts who stays",
			Vector2(cx + 320.0, y + 56.0), 15, Color(DeskKit.INK, 0.45), 290.0)

## SMB: the biggest-5 strip — named when accounts have names, counted honestly
## until they do (an SMB crowd is small shops; names arrive with the pipeline).
static func _biggest_five(b, s: GameState, y: float, h: float) -> void:
	var frame := DeskKit.card_frame(b, RIGHT_X, y, RIGHT_W, h, "the biggest five")
	var cx := float(frame.get("content_x", RIGHT_X))
	var cy := float(frame.get("content_y", y))
	if s.logos.is_empty():
		var arpu := SimEngine.offers_arpu(s)
		DeskKit.empty(b, Vector2(cx, cy),
			"no account big enough to name yet.",
			"%d small shops pay ≈ $%s/wk each — the biggest earn names as they grow" % [
				s.traction, b.fmt(int(round(maxf(arpu, 0.0))))])
		return
	var idx := _logos_by_seats(s)
	var x := cx
	for n in mini(idx.size(), 5):
		var lg: Dictionary = s.logos[idx[n]]
		x = DeskKit.chip(b, x, cy, {"text": "%s — %d" % [String(lg.get("name", "?")),
			int(lg.get("seats", 0))], "kind": "person"})

## ENTERPRISE: the logo grid — seats on every card, the renewal clock when the
## pipeline says one is close enough to plan around.
static func _logo_grid(b, s: GameState, y: float, h: float) -> void:
	var frame := DeskKit.card_frame(b, RIGHT_X, y, RIGHT_W, h,
		"the logos — %d signed" % s.logos.size())
	var cx := float(frame.get("content_x", RIGHT_X))
	var cy := float(frame.get("content_y", y))
	if s.logos.is_empty():
		DeskKit.empty(b, Vector2(cx, cy),
			"no logos yet — a contract is the only way an enterprise customer arrives.",
			"the board on IN MOTION is where they come from")
		return
	var idx := _logos_by_seats(s)
	var x := cx
	var row := 0
	var shown := 0
	for n in idx.size():
		if row >= 2:
			break
		var lg: Dictionary = s.logos[idx[n]]
		var due := int(lg.get("renewal_wk", 0)) - s.week
		x = DeskKit.chip(b, x, cy, {"text": "%s — %d seats" % [String(lg.get("name", "?")),
			int(lg.get("seats", 0))], "kind": "person"})
		if due > 0 and due <= 4:
			x = DeskKit.clock_chip(b, x, cy + 3.0, "renews %d wk" % due)
		shown += 1
		if x > RIGHT_X + RIGHT_W - 220.0:
			x = cx
			cy += 44.0
			row += 1
	DeskKit.more(b, Vector2(cx, cy + 44.0), idx.size() - shown, "logos hold behind these")

static func _logos_by_seats(s: GameState) -> Array:
	var idx: Array = []
	for i in s.logos.size():
		idx.append(i)
	idx.sort_custom(func(a: int, c: int) -> bool:
		var sa := int((s.logos[a] as Dictionary).get("seats", 0))
		var sc := int((s.logos[c] as Dictionary).get("seats", 0))
		if sa != sc:
			return sa > sc
		return a < c)
	return idx

# ─────────────────────────────── the foot ────────────────────────────────────

static func _foot(b, s: GameState, an: int) -> void:
	var f := SimFunnel.funnel(s)
	var computed := ""
	if an >= 3 and not f.is_empty():
		var best := ""
		var best_cac := 0.0
		for k in SimFunnel.MIX:
			var c := SimFunnel.num(f, "cac_" + k)
			if c > 0.0 and (best == "" or c < best_cac):
				best = k
				best_cac = c
		computed = "the truth: conv %.1f%% · close %d%% · %s" % [
			SimFunnel.num(f, "conv") * 100.0,
			int(round(SimFunnel.num(f, "close_rate") * 100.0)),
			("cheapest customer: %s" % best.to_upper()) if best != ""
				else "no channel has bought a customer yet"]
	var warning := ""
	for k2 in SimFunnel.MIX:
		if SimFunnel.num(f, "cac_" + k2) <= 0.0 \
				and SimFunnel.num(f, "spend_" + k2) >= SimFunnel.BURN_SPEND:
			warning = "%s is BURNING: $%s/wk bought nobody last week — a channel with no CAC has no price" % [
				k2.to_upper(), b.fmt(int(SimFunnel.num(f, "spend_" + k2)))]
			break
	DeskKit.footer(b, {
		"computed": computed,
		"rules": "the rules of this desk: REACH is what money bought · a LEAD is reach that answered · "
			+ "only closing capacity signs them · churn is a leaky bucket, and care patches it",
		"warning": warning,
		"y": 806.0, "rules_y": 840.0,
	})
