class_name DeskInMotion
extends RefCounted
## DESK — REVENUE · "in motion" = THE TRIPTYCH (DECISIONS: C2×C1 · S1 · E2).
## THE QUESTION THIS DESK ANSWERS: "who is on the way to becoming money?"
##
## AUDIENCE-NATIVE, never stretched (the audience doctrine): the run's world
## decides which page this is —
##   Consumer   = THE RIVER × THE SOURCES: joiners/wk + the measured word-of-
##                mouth factor, the cohort river (weekly joiner bars colored by
##                origin), WHERE THEY COME FROM (top 4 + "+N more"), THE TASTE
##                TEST (tried -> stayed, with the honest "ads can't move this").
##   SMB        = THE HOT LIST: the five worth a dinner ranked by revenue-if-
##                landed × closeness, the crowd as one honest row. Rank 1 is
##                the week's journal move.
##   Enterprise = THE STAGE BOARD: rep kanban, columns narrowing like the
##                funnel, dying deal in red. COLLAPSE LADDER: ≤3 cards + "+N"
##                per column; a column-header press opens the focused list;
##                past ~8 live deals the cards compress to slim rows.
##
## No controls move a deal: pushes ride the journal (`push_lead`) — that
## absence is the lesson. Heat words wear the ramp; clocks inside 2 weeks wear
## the alarm.
##
## THE RIVER'S HISTORY: weekly origin splits ride metric_history snapshots
## (adds/adds_org/adds_wom/adds_chan). Until the engine writes them (see the
## lane's coordinator package) old rows draw as ghost bars — the fog is drawn,
## never faked.
##
## DAG3 (13-binder-ux · in motion): THE PREFILL — a push press composes the
## written move ("push <name>: <the deal's hint line>") straight into the
## journal draft (DeskThisWeek.draft) and a calm chip says where it went;
## rank-1 wears the push in the DO lane, Enterprise cards/rows push per deal,
## and suggestions() hands rank-1 to THIS WEEK's chips. A Consumer source bar
## press opens growth focused on that plot (back pill free). Week one is the
## S1 teaching state, one per audience.

const QUESTION := "who is on the way to becoming money?"

const RIVER_WEEKS := 8
const HOT_SHOW := 5
const BOARD_CARDS := 3
const SLIM_AT := 9          ## past ~8 live deals the cards compress to rows

# ─────────────────────────────── the dispatch ────────────────────────────────

static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	var f := SimFunnel.funnel(s)
	match String(s.biz_who):
		"Enterprise":
			return {"big": "%d deals" % s.leads.size(),
				"line": "≈%d seats in motion — the stage board" % SimPipeline.seats_in_motion(s)}
		"SMB":
			return {"big": "%d in motion" % _smb_in_motion(s, f),
				"line": "≈%d will land — the hot list" % int(round(SimFunnel.num(f, "adds")))}
		_:
			return {"big": "%d joining a week" % int(round(SimFunnel.num(f, "adds"))),
				"line": "the river — joiners by origin, word of mouth measured"}

static func draw(b) -> void:
	var s: GameState = b.state
	if _zero_state(b, s):
		return
	var mode := String(b.desk.get("mode", ""))
	match String(s.biz_who):
		"Enterprise":
			if mode.begins_with("col:"):
				_ent_column_focus(b, s, mode.substr(4))
			else:
				_enterprise(b, s)
		"SMB":
			if mode == "smb_all":
				_smb_all(b, s)
			else:
				_smb(b, s)
		_:
			if mode == "sources":
				_consumer_sources_all(b, s)
			else:
				_consumer(b, s)

static func handle(_b, _id: String) -> void:
	pass

# ═══════════════ S1 · the zero states + THE PREFILL's shared hand ════════════

## S1 — the desk before anything moves is a TEACHING state, one per audience.
static func _zero_state(b, s: GameState) -> bool:
	if not SimFunnel.funnel(s).is_empty():
		return false
	var tend := func() -> void: b.focus_desk("growth", "", "in motion")
	match String(s.biz_who):
		"Enterprise":
			if not (s.leads.is_empty() and s.logos.is_empty()):
				return false
			DeskKit.zero_state(b, {
				"will_show": "the stage board — every buyer has a name",
				"would_line": "deals march meeting → pilot → contract; your written pushes "
					+ "keep them warm, the dice move them",
				"action_label": "tend the garden",
				"action_cb": tend,
				"wakes_hint": "marketing books the first meeting — fund reach and the board fills",
			})
		"SMB":
			if not s.leads.is_empty():
				return false
			DeskKit.zero_state(b, {
				"will_show": "the hot list — the five worth a dinner",
				"would_line": "named shops ranked by revenue-if-landed × closeness, "
					+ "the crowd counted honestly below",
				"action_label": "tend the garden",
				"action_cb": tend,
				"wakes_hint": "names arrive when a shop grows big enough to chase",
			})
		_:
			if not s.metric_history.is_empty():
				return false
			DeskKit.zero_state(b, {
				"will_show": "the river — who joins each week, and where from",
				"would_line": "each locked week lands one bar: bought · friends · walked in — "
					+ "and the word-of-mouth factor, measured",
				"action_label": "tend the garden",
				"action_cb": tend,
				"wakes_hint": "the first bar arrives at your first LOCK IN",
			})
	return true

## The one deal the week's move belongs to: SMB = the hot list's rank 1,
## Enterprise = the hottest deal on the board.
static func _rank1(s: GameState) -> Dictionary:
	if s.leads.is_empty():
		return {}
	if String(s.biz_who) == "Enterprise":
		var order := SimPipeline.leads_by_heat(s)
		return s.leads[order[0]] if not order.is_empty() else {}
	var ranked := _ranked(s)
	return s.leads[int(ranked[0])] if not ranked.is_empty() else {}

## The written move, composed in plain words: the deal's own hint line when
## the world wrote one, else the honest facts.
static func _move_text(lead: Dictionary) -> String:
	var lname := String(lead.get("name", "a prospect"))
	var flavor := String(lead.get("flavor", "")).strip_edges()
	if flavor != "":
		return "push %s: %s" % [lname, flavor]
	return "push %s: %d seats sitting at %s" % [lname,
		int(lead.get("seats", 0)), String(lead.get("stage", "meeting"))]

## THE PREFILL — the binder DRAFTS the decision: the journal's composer
## receives the written move (the player edits freely); nothing else moves.
static func _prefill(b, lead: Dictionary) -> void:
	DeskThisWeek.draft = _move_text(lead)
	b.desk["drafted"] = String(lead.get("name", ""))

## The calm confirmation: a chip saying where the words went — pressing it
## walks there with a back pill; it dies when the draft stops being ours.
static func _drafted_chip(b) -> void:
	var dname := String(b.desk.get("drafted", ""))
	if dname == "" or not DeskThisWeek.draft.begins_with("push " + dname):
		return
	var text := "drafted — see THIS WEEK"
	var tw: float = b.font().get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, 19).x
	DeskKit.chip(b, 1130.0 - tw - 26.0, 70.0, {"text": text, "kind": "person",
		"selected": true, "on_press": func() -> void:
			b.focus_desk("this week", "", "in motion")})

## S3 — THE PREFILL in the one slot: rank-1's push writes the move into the
## journal draft; the chip says where it went (jumping would yank context).
static func _do_push_lane(b, s: GameState) -> void:
	var lead := _rank1(s)
	if lead.is_empty():
		return
	DeskKit.do_lane(b, [{"label": "push — %s" % String(lead.get("name", "a prospect")),
		"tier": "", "cb": func() -> void: _prefill(b, lead)}])

## S5 — the hero against the binder's last open.
static func _hero_arrow(b, val: int, x: float, y: float) -> void:
	var hp: String = b.seen_prev("in motion", "hero")
	if b.seen("in motion", "hero", str(val)) and hp.is_valid_int():
		DeskKit.delta_arrow(b, x, y, float(val), float(hp.to_int()))

## S15 — the desk speaks up: rank-1's push, ready to prefill THE WEEK'S CHIPS.
static func suggestions(state) -> Array:
	var s: GameState = state
	var lead := _rank1(s)
	if lead.is_empty():
		return []
	return [{"label": "push — %s" % String(lead.get("name", "a prospect")),
		"kind": "prefill", "payload": _move_text(lead)}]

## S8 — dormant before launch with nothing named: no river, no board, no list.
static func is_dormant(state) -> bool:
	var s: GameState = state
	return not s.has_flag("launched") and s.leads.is_empty() and s.logos.is_empty()

## S8 — the rail's read, by audience: deals, names, or the week's joiners.
static func micro_status(state) -> String:
	var s: GameState = state
	match String(s.biz_who):
		"Enterprise":
			return ("%d deals" % s.leads.size()) if not s.leads.is_empty() else ""
		"SMB":
			return ("%d named" % s.leads.size()) if not s.leads.is_empty() else ""
		_:
			var f := SimFunnel.funnel(s)
			return ("≈%d/wk" % int(round(SimFunnel.num(f, "adds")))) if not f.is_empty() else ""

# ═══════════════════════════════ CONSUMER ════════════════════════════════════

static func _consumer(b, s: GameState) -> void:
	var f := SimFunnel.funnel(s)
	var adds := SimFunnel.num(f, "adds")
	var prev := SimFunnel.funnel_prev(s)
	var delta := adds - SimFunnel.num(prev, "adds")
	var big := "%d joining a week" % int(round(adds))
	b.label(big, Vector2(DeskKit.X_ID, 6.0), DeskKit.HERO_BIG, DeskKit.INK, 700.0)
	if not prev.is_empty():
		var bx: float = DeskKit.X_ID + b.font().get_string_size(big, HORIZONTAL_ALIGNMENT_LEFT,
			-1, DeskKit.HERO_BIG).x + 24.0
		b.label("%s%d vs last week" % ["+" if delta >= 0.0 else "−", absi(int(round(delta)))],
			Vector2(bx, 22.0), 27, Color("5D7A50") if delta >= 0.0 else DeskKit.PEN, 260.0)
	# the measured word-of-mouth factor — each joiner brings ≈X more
	var wom := SimFunnel.num(f, "wom")
	var factor := wom / maxf(adds - wom, 1.0)
	var wl: Label = b.label("each joiner brings ≈%.1f more" % factor, Vector2(760.0, 10.0),
		27, DeskKit.INK, 370.0)
	wl.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	var ws: Label = b.label("word of mouth, measured", Vector2(760.0, 46.0), 17,
		Color(DeskKit.INK, 0.5), 370.0)
	ws.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	# S2 — red replaces the law line when this desk carries asks
	if not DeskKit.ask_strip(b, "in motion", DeskKit.X_ID, 74.0, 1100.0, "push what's cooling"):
		b.label("consumer — nobody has a name until they pay: the page is rates, sources and word of mouth",
			Vector2(DeskKit.X_ID, 74.0), DeskKit.LAW, Color(DeskKit.INK, 0.5), 1100.0)
	var y := DeskKit.pen_rule(b, 122.0) + 6.0   # R5 - the rule sits under the strip slot (96-118), content at 126+
	y = _river_card(b, s, y)
	_sources_card(b, s, y)
	_taste_card(b, s, y)
	_consumer_foot(b, s)

## THE COHORT RIVER — each bar one week's joiners, stacked by origin.
static func _river_card(b, s: GameState, y: float) -> float:
	var frame := DeskKit.card_frame(b, 10.0, y, 1120.0, 268.0,
		"the weeks flow in — each bar is one week's joiners, colored by where they came from")
	var cx := float(frame.get("content_x", 10.0))
	var cy := float(frame.get("content_y", y))
	var rows := _river_rows(s)
	if rows.is_empty():
		DeskKit.empty(b, Vector2(cx, cy + 8.0),
			"no week on the books yet — the river is measured, not predicted.",
			"lock in a week and the first bar arrives")
		return float(frame.get("bottom", y + 268.0)) + 14.0
	var hi := 1.0
	for r in rows:
		hi = maxf(hi, float((r as Dictionary).get("total", 0.0)))
	var region_h := 126.0
	var cell := (1120.0 - DeskKit.CARD_PAD * 2.0) / float(rows.size())
	for i in rows.size():
		var r2: Dictionary = rows[i]
		var known := bool(r2.get("known", false))
		var total := float(r2.get("total", 0.0))
		var bar_h := maxf(region_h * (total / hi), 8.0) if known else region_h * 0.4
		var bx := cx + float(i) * cell + (cell - 64.0) * 0.5
		var bar := _RiverBar.new()
		bar.known = known
		bar.segs = r2.get("segs", [])
		bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
		bar.position = Vector2(bx, cy + region_h - bar_h)
		bar.set_deferred("size", Vector2(64.0, bar_h))
		b.pane().add_child(bar)
		var nl: Label = b.label(("%d" % int(round(total))) if known else "?",
			Vector2(bx - 20.0, cy + region_h + 6.0), 20,
			DeskKit.INK if i == rows.size() - 1 else Color(DeskKit.INK, 0.6), 104.0)
		nl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		var wl2: Label = b.label("wk %d" % int(r2.get("wk", 0)),
			Vector2(bx - 20.0, cy + region_h + 32.0), 14, Color(DeskKit.INK, 0.4), 104.0)
		wl2.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	b.label("bought", Vector2(cx, cy + region_h + 58.0), 17, DeskKit.BLUE, 120.0)
	b.label("friends", Vector2(cx + 100.0, cy + region_h + 58.0), 17, DeskKit.YELL, 120.0)
	b.label("walked in", Vector2(cx + 210.0, cy + region_h + 58.0), 17, DeskKit.SAGE, 140.0)
	b.label("the river rises when word of mouth does — friends are the middle color",
		Vector2(cx + 400.0, cy + region_h + 58.0), 17, Color(DeskKit.INK, 0.45), 680.0)
	return float(frame.get("bottom", y + 268.0)) + 14.0

## The last 8 weeks' origin rows, oldest first. A row without the origin keys
## (a pre-package save) keeps its week and draws as a ghost.
static func _river_rows(s: GameState) -> Array:
	var out: Array = []
	var n := s.metric_history.size()
	var start := maxi(n - RIVER_WEEKS, 0)
	for i in range(start, n):
		var m: Dictionary = s.metric_history[i]
		if m.has("adds"):
			var org := float(m.get("adds_org", 0.0))
			var wom := float(m.get("adds_wom", 0.0))
			var chan := float(m.get("adds_chan", 0.0))
			out.append({"wk": int(m.get("wk", m.get("week", 0))), "known": true,
				"total": float(m.get("adds", 0.0)), "segs": [chan, wom, org]})
		else:
			out.append({"wk": int(m.get("wk", m.get("week", 0))), "known": false,
				"total": 0.0, "segs": []})
	# the freshest week can always speak: the funnel meta knows it even before
	# the snapshot carries the keys
	var f := SimFunnel.funnel(s)
	if not f.is_empty() and not out.is_empty():
		var last: Dictionary = out[out.size() - 1]
		if not bool(last.get("known", false)) and int(last.get("wk", -1)) == int(SimFunnel.num(f, "wk")):
			var chan2 := SimFunnel.num(f, "signed_ads") + SimFunnel.num(f, "signed_content") \
				+ SimFunnel.num(f, "signed_referrals") + SimFunnel.num(f, "signed_outbound")
			out[out.size() - 1] = {"wk": int(last.get("wk", 0)), "known": true,
				"total": SimFunnel.num(f, "adds"),
				"segs": [chan2, SimFunnel.num(f, "wom"), SimFunnel.num(f, "organic")]}
	return out

## WHERE THEY COME FROM — this week's ranked sources, top 4 + "+N more".
static func _sources_card(b, s: GameState, y: float) -> void:
	var frame := DeskKit.card_frame(b, 10.0, y, 640.0, 236.0,
		"where they come from — this week")
	var cx := float(frame.get("content_x", 10.0))
	var cy := float(frame.get("content_y", y))
	var f := SimFunnel.funnel(s)
	if f.is_empty():
		DeskKit.empty(b, Vector2(cx, cy), "no week on the books yet.", "")
		return
	var src := _sources(s, f)
	var rows: Array = []
	for i in mini(src.size(), 4):
		var d: Dictionary = src[i]
		rows.append({"label": String(d.get("name", "")), "value": float(d.get("v", 0.0)),
			"col": d.get("col", DeskKit.BLUE), "text": "%d" % int(round(float(d.get("v", 0.0))))})
	# the kit's bars are full-pane; inside this card they get the card's width
	var by := cy
	var hi := 1.0
	for r in rows:
		hi = maxf(hi, float((r as Dictionary).get("value", 0.0)))
	# S2b/S7 — a source bar is a door: growth opens focused on that plot,
	# the back pill comes free from the jump's source
	var plot_keys := {"the ads": "ads", "the library": "content",
		"referrals": "referrals", "cold outreach": "outbound"}
	for r2 in rows:
		var rd: Dictionary = r2
		b.label(String(rd.get("label", "")).to_upper(), Vector2(cx, by), 18, DeskKit.INK, 170.0)
		var w := 24.0 + 300.0 * (float(rd.get("value", 0.0)) / hi)
		DeskKit.meter(b, cx + 180.0, by, w, 1.0, rd.get("col", DeskKit.BLUE), String(rd.get("text", "")))
		var pk := String(plot_keys.get(String(rd.get("label", "")), ""))
		if pk != "":
			var hit := DeskKit.word(b, "", Vector2(cx, by - 4.0), func() -> void:
				b.focus_desk("growth", "plot_" + pk, "in motion"), 18, DeskKit.INK, 540.0)
			hit.size = Vector2(540.0, 36.0)
		by += 36.0
	if src.size() > 4:
		DeskKit.word(b, "+%d more ->" % (src.size() - 4), Vector2(cx, by - 4.0), func() -> void:
			b.desk["mode"] = "sources", DeskKit.LAW, Color(DeskKit.INK, 0.6), 200.0)

## The named streams, ranked. Words stay plain; the funnel's numbers carry.
static func _sources(_s: GameState, f: Dictionary) -> Array:
	var src: Array = [
		{"name": "word of mouth", "v": SimFunnel.num(f, "wom"), "col": DeskKit.YELL},
		{"name": "the ads", "v": SimFunnel.num(f, "signed_ads"), "col": DeskKit.BLUE},
		{"name": "the library", "v": SimFunnel.num(f, "signed_content"), "col": DeskKit.BLUE},
		{"name": "referrals", "v": SimFunnel.num(f, "signed_referrals"), "col": DeskKit.YELL},
		{"name": "cold outreach", "v": SimFunnel.num(f, "signed_outbound"), "col": DeskKit.BLUE},
		{"name": "walked in", "v": SimFunnel.num(f, "organic"), "col": DeskKit.SAGE},
	]
	src.sort_custom(func(a: Dictionary, c: Dictionary) -> bool:
		return float(a.get("v", 0.0)) > float(c.get("v", 0.0)))
	return src

## THE TASTE TEST — tried -> stayed, and the note ads can't argue with.
static func _taste_card(b, s: GameState, y: float) -> void:
	var frame := DeskKit.card_frame(b, 666.0, y, 464.0, 236.0, "the taste test")
	var f := SimFunnel.funnel(s)
	if f.is_empty():
		DeskKit.empty(b, Vector2(float(frame.get("content_x", 666.0)),
			float(frame.get("content_y", y))), "no week on the books yet.", "")
		return
	var leads := SimFunnel.num(f, "leads_total")
	var chan := SimFunnel.num(f, "signed_ads") + SimFunnel.num(f, "signed_content") \
		+ SimFunnel.num(f, "signed_referrals") + SimFunnel.num(f, "signed_outbound")
	var stayed := (chan / leads * 100.0) if leads >= 1.0 else 0.0
	DeskKit.money_row(b, frame, "tried it this week", b.fmt(int(round(leads))))
	DeskKit.money_row(b, frame, "stayed to pay",
		("%d%%" % int(round(stayed))) if leads >= 1.0 else "—")
	DeskKit.money_row(b, frame, "a point of staying ≈",
		("+%.1f/wk" % (leads * 0.01)) if leads >= 1.0 else "—", Color("5D7A50"))
	b.label("care and product quality move this number — ads can't",
		Vector2(float(frame.get("content_x", 666.0)),
		float(frame.get("bottom", y)) - 40.0), 17, Color(DeskKit.INK, 0.5), 420.0)

static func _consumer_foot(b, s: GameState) -> void:
	var f := SimFunnel.funnel(s)
	var computed := ""
	if not f.is_empty():
		computed = "the week: bought %d · friends %d · walked in %d = %d joined" % [
			int(round(SimFunnel.num(f, "signed_ads") + SimFunnel.num(f, "signed_content")
			+ SimFunnel.num(f, "signed_referrals") + SimFunnel.num(f, "signed_outbound"))),
			int(round(SimFunnel.num(f, "wom"))), int(round(SimFunnel.num(f, "organic"))),
			int(round(SimFunnel.num(f, "adds")))]
	DeskKit.footer(b, {"computed": computed,
		"rules": "a consumer pipeline is sources × conversion — nobody has a name until they pay",
		"y": 806.0, "rules_y": 840.0})

## The fold opened: every source, one sheet.
static func _consumer_sources_all(b, s: GameState) -> void:
	DeskKit.back(b, "back to the river", func() -> void:
		b.desk["mode"] = "")
	var f := SimFunnel.funnel(s)
	var src := _sources(s, f)
	var rows: Array = []
	for d in src:
		var dd: Dictionary = d
		rows.append({"label": String(dd.get("name", "")), "value": float(dd.get("v", 0.0)),
			"col": dd.get("col", DeskKit.BLUE),
			"text": "%d this week" % int(round(float(dd.get("v", 0.0))))})
	b.label("every source, counted", Vector2(DeskKit.X_ID, 60.0), 24,
		Color(DeskKit.INK, 0.6), 600.0)
	DeskKit.bars(b, Vector2(DeskKit.X_ID, 100.0), rows, 52.0)
	_consumer_foot(b, s)

# ═══════════════════════════════ SMB ═════════════════════════════════════════

static func _smb_in_motion(s: GameState, f: Dictionary) -> int:
	return s.leads.size() + int(round(SimFunnel.num(f, "leads_total")))

static func _smb(b, s: GameState) -> void:
	var f := SimFunnel.funnel(s)
	var big := "%d in motion" % _smb_in_motion(s, f)
	b.label(big, Vector2(DeskKit.X_ID, 6.0), DeskKit.HERO_BIG, DeskKit.INK, 620.0)
	var bx: float = DeskKit.X_ID + b.font().get_string_size(big, HORIZONTAL_ALIGNMENT_LEFT, -1,
		DeskKit.HERO_BIG).x + 24.0
	b.label("· ≈%d will land" % int(round(SimFunnel.num(f, "adds"))), Vector2(bx, 26.0),
		24, Color(DeskKit.INK, 0.6), 260.0)
	var cr: Label = b.label("close rate %d%%" % int(round(SimFunnel.num(f, "close_rate") * 100.0))
		if not f.is_empty() else "close rate ?", Vector2(800.0, 14.0), 27, DeskKit.INK, 330.0)
	cr.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	_hero_arrow(b, _smb_in_motion(s, f), bx + 266.0, 26.0)
	# S2 — red replaces the law line when this desk carries asks
	if not DeskKit.ask_strip(b, "in motion", DeskKit.X_ID, 74.0, 1100.0, "push what's cooling"):
		b.label("SMB — dozens of small shops, a handful worth chasing by name",
			Vector2(DeskKit.X_ID, 74.0), DeskKit.LAW, Color(DeskKit.INK, 0.5), 1100.0)
	var y := DeskKit.pen_rule(b, 122.0) + 6.0   # R5 - the rule sits under the strip slot (96-118), content at 126+
	y = _hot_list(b, s, f, y, HOT_SHOW)
	_do_push_lane(b, s)
	_drafted_chip(b)
	DeskKit.footer(b, {
		"computed": "",
		"rules": "SMB is a hybrid: name the five that deserve a dinner, count the forty that don't · rank 1 is this week's journal move",
		"y": 806.0, "rules_y": 840.0})

## CLOSEST TO MONEY — ranked by revenue-if-landed × closeness.
static func _hot_list(b, s: GameState, f: Dictionary, y: float, show: int) -> float:
	var frame := DeskKit.card_frame(b, 10.0, y, 1120.0,
		DeskKit.CARD_HEAD + maxf(float(mini(_ranked(s).size(), show)), 1.0) * 58.0 + 64.0,
		"closest to money")
	var cy := float(frame.get("content_y", y))
	var ranked := _ranked(s)
	var unit := SimPipeline.unit_rev_wk(s)
	if ranked.is_empty():
		cy = DeskKit.empty(b, Vector2(float(frame.get("content_x", 10.0)), cy),
			"no shop is worth a dinner yet — the crowd moves on its own.",
			"a named account arrives when one grows big enough to chase")
	for n in mini(ranked.size(), show):
		var lead: Dictionary = s.leads[int(ranked[n])]
		var heat := int(lead.get("heat", 0))
		var dies := SimPipeline.weeks_to_cold(heat, SimPipeline.decay_for(s))
		var facts := "%d seats · %s" % [int(lead.get("seats", 0)), String(lead.get("stage", "meeting"))]
		if n == 0:
			facts += " · the move"
		var value := "≈$%s/wk" % b.fmt(int(round(float(lead.get("seats", 0)) * unit)))
		var rn: Label = b.label("%d" % (n + 1), Vector2(float(frame.get("content_x", 10.0)), cy + 4.0),
			22, Color(DeskKit.INK, 0.4), 30.0)
		rn.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		b.label(String(lead.get("name", "a prospect")), Vector2(60.0, cy), DeskKit.ROW, DeskKit.INK, 330.0)
		b.label(facts, Vector2(400.0, cy + 4.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.65), 330.0)
		b.label(SimPipeline.heat_word(heat), Vector2(740.0, cy + 4.0), DeskKit.DETAIL,
			DeskKit.heat_col(SimPipeline.heat_word(heat)), 90.0)
		if dies <= 2:
			DeskKit.clock_chip(b, 830.0, cy + 4.0, "%d wk" % dies)
		var vl: Label = b.label(value, Vector2(920.0, cy), DeskKit.ROW, DeskKit.INK, 190.0)
		vl.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		DeskKit.pen_rule(b, cy + 44.0, 40.0, 1060.0, Color(DeskKit.INK, 0.12), 13 + n)
		cy += 58.0
	# the crowd — one honest row, never hidden math: the named leads past the
	# face-up cap count here too, so face-up + the other N = the hero's number
	var crowd := int(round(SimFunnel.num(f, "leads_total"))) \
		+ maxi(ranked.size() - mini(ranked.size(), show), 0)
	var lands := int(round(SimFunnel.num(f, "adds")))
	b.label("the other %d — small shops moving on their own · ≈%d land weekly" % [crowd, lands],
		Vector2(60.0, cy + 2.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.55), 800.0)
	if ranked.size() > show:
		DeskKit.word(b, "the full list ->", Vector2(920.0, cy - 4.0), func() -> void:
			b.desk["mode"] = "smb_all", DeskKit.LAW, Color(DeskKit.INK, 0.6), 190.0)
	return float(frame.get("bottom", cy)) + 10.0

## revenue-if-landed × closeness: seats × $/seat × (stage reached × heat).
static func _ranked(s: GameState) -> Array:
	var stages := {"meeting": 1.0, "pilot": 2.0, "procurement": 3.0, "contract": 4.0}
	var unit := SimPipeline.unit_rev_wk(s)
	var idx: Array = []
	for i in s.leads.size():
		idx.append(i)
	idx.sort_custom(func(a: int, c: int) -> bool:
		var la: Dictionary = s.leads[a]
		var lc: Dictionary = s.leads[c]
		var sa := float(la.get("seats", 0)) * unit \
			* float(stages.get(String(la.get("stage", "meeting")), 1.0)) \
			* (float(la.get("heat", 0)) + 20.0)
		var sc := float(lc.get("seats", 0)) * unit \
			* float(stages.get(String(lc.get("stage", "meeting")), 1.0)) \
			* (float(lc.get("heat", 0)) + 20.0)
		if not is_equal_approx(sa, sc):
			return sa > sc
		return a < c)
	return idx

## The fold opened: every named account, ranked, one sheet.
static func _smb_all(b, s: GameState) -> void:
	DeskKit.back(b, "back to the hot list", func() -> void:
		b.desk["mode"] = "")
	var f := SimFunnel.funnel(s)
	_hot_list(b, s, f, 64.0, s.leads.size())
	_do_push_lane(b, s)
	_drafted_chip(b)
	DeskKit.footer(b, {"computed": "",
		"rules": "every named account, ranked by revenue-if-landed × closeness",
		"y": 806.0, "rules_y": 840.0})

# ═══════════════════════════════ ENTERPRISE ══════════════════════════════════

static func _enterprise(b, s: GameState) -> void:
	_ent_hero(b, s)
	var y := DeskKit.pen_rule(b, 122.0) + 6.0   # R5 - the rule sits under the strip slot (96-118), content at 126+
	if s.leads.size() >= SLIM_AT:
		_ent_slim(b, s, y)
	else:
		_ent_board(b, s, y)
	_do_push_lane(b, s)
	_drafted_chip(b)
	_ent_foot(b, s)

static func _ent_hero(b, s: GameState) -> void:
	var big := "%d deals" % s.leads.size()
	b.label(big, Vector2(DeskKit.X_ID, 6.0), DeskKit.HERO_BIG, DeskKit.INK, 460.0)
	var bx: float = DeskKit.X_ID + b.font().get_string_size(big, HORIZONTAL_ALIGNMENT_LEFT, -1,
		DeskKit.HERO_BIG).x + 24.0
	b.label("· ≈%d seats in motion" % SimPipeline.seats_in_motion(s), Vector2(bx, 26.0),
		24, Color(DeskKit.INK, 0.6), 320.0)
	var st: Dictionary = s.pipe_stats
	var signed := int(st.get("signed", 0))
	var decided := signed + int(st.get("lost", 0))
	var win := "?" if decided <= 0 else "%d%%" % int(round(100.0 * float(signed) / float(decided)))
	var cycle := "?" if signed <= 0 else "%d" % int(round(float(st.get("cycle_sum", 0)) / float(signed)))
	var wl: Label = b.label("win rate %s · cycle %s wks" % [win, cycle], Vector2(770.0, 10.0),
		27, DeskKit.INK, 360.0)
	wl.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	var seats := 0
	for lg in s.logos:
		seats += int((lg as Dictionary).get("seats", 0))
	var ll: Label = b.label("%d logos · %d seats live" % [s.logos.size(), seats],
		Vector2(770.0, 48.0), 17, Color(DeskKit.INK, 0.5), 360.0)
	ll.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	_hero_arrow(b, s.leads.size(), bx + 328.0, 26.0)
	# S2 — red replaces the law line when this desk carries asks
	if not DeskKit.ask_strip(b, "in motion", DeskKit.X_ID, 74.0, 1100.0, "push what's cooling"):
		b.label("enterprise — every buyer has a name and a dinner budget",
			Vector2(DeskKit.X_ID, 74.0), DeskKit.LAW, Color(DeskKit.INK, 0.5), 1100.0)

static func _ent_stages(s: GameState) -> Array:
	if s.era_index() >= 2:
		return ["meeting", "pilot", "procurement", "contract"]
	return ["meeting", "pilot", "contract"]

## THE STAGE BOARD — columns narrowing like the funnel.
static func _ent_board(b, s: GameState, y: float) -> void:
	var stages := _ent_stages(s)
	var ratios: Array = [1.12, 1.0, 0.9, 0.78] if stages.size() == 4 else [1.12, 1.0, 0.78]
	var gaps := float(stages.size() - 1) * 12.0
	var rsum := 0.0
	for r in ratios:
		rsum += float(r)
	var order := SimPipeline.leads_by_heat(s)
	var decay := SimPipeline.decay_for(s)
	var x := 10.0
	var live := 0
	var col_h := 520.0
	for si in stages.size():
		var stage := String(stages[si])
		var w := (1120.0 - gaps) * float(ratios[si]) / rsum
		var col := DeskKit.wall_column(b, x, y, w, col_h, stage,
			"signed deals join the logos" if stage == "contract" else "")
		# the header is the door to the focused list
		var head_hit := DeskKit.word(b, "", Vector2(x, y), func() -> void:
			b.desk["mode"] = "col:" + stage, DeskKit.LAW, DeskKit.INK, w)
		head_hit.size = Vector2(w, 54.0)
		var here: Array = []
		for i in order:
			if String((s.leads[i] as Dictionary).get("stage", "meeting")) == stage:
				here.append(i)
		live += here.size()
		for n in mini(here.size(), BOARD_CARDS):
			var lead: Dictionary = s.leads[here[n]]
			var heat := int(lead.get("heat", 0))
			var dies := SimPipeline.weeks_to_cold(heat, decay)
			var facts: Array = ["%d seats · %s · wk %d" % [int(lead.get("seats", 0)),
				SimPipeline.heat_word(heat), int(lead.get("age_weeks", 0))]]
			if dies <= 2:
				facts.append("dies in %d wk%s" % [dies, "" if dies == 1 else "s"])
			# THE PREFILL, per deal: the card press drafts the push (no deal moves)
			var card_y := float(col.get("cursor", y))
			DeskKit.wall_card(b, col, {"title": String(lead.get("name", "a prospect")),
				"facts": facts, "ready": dies <= 2,
				"on_press": func() -> void: _prefill(b, lead)})
			# S2b — the first cooling deal's card is the ask's landing switch
			if heat <= 16 and not b.has_control("push_cold"):
				b.mark_control("push_cold", Rect2(float(col.get("content_x", x)), card_y,
					w - 16.0, float(col.get("cursor", y)) - card_y - 10.0))
		if here.size() > BOARD_CARDS:
			b.label("+%d" % (here.size() - BOARD_CARDS),
				Vector2(x + 10.0, float(col.get("cursor", y)) + 2.0), 21,
				Color(DeskKit.INK, 0.5), w - 20.0)
		x += w + 12.0
	if live == 0:
		b.label("no deals on the board yet — marketing books the meetings, and %.0f seats of interest are already waiting in the pool" % s.pipe_units,
			Vector2(DeskKit.X_ID, y + 120.0), DeskKit.STATUS, Color(DeskKit.INK, 0.6), 1100.0)

## Past ~8 live deals the cards compress to slim rows, hottest first.
static func _ent_slim(b, s: GameState, y: float) -> void:
	b.label("the board, compressed — %d live deals, hottest first" % s.leads.size(),
		Vector2(DeskKit.X_ID, y), DeskKit.LAW, Color(DeskKit.INK, 0.45), 900.0)
	y += 30.0
	var decay := SimPipeline.decay_for(s)
	var unit := SimPipeline.unit_rev_wk(s)
	var order := SimPipeline.leads_by_heat(s)
	var shown := 0
	for i in order:
		if shown >= 10:
			break
		var lead: Dictionary = s.leads[i]
		var heat := int(lead.get("heat", 0))
		var dies := SimPipeline.weeks_to_cold(heat, decay)
		var dying := dies <= 2
		var row_y := y
		y = DeskKit.hero_row(b, y, {
			"name": String(lead.get("name", "a prospect")),
			"facts": "%s · %d seats · %s · wk %d%s" % [String(lead.get("stage", "meeting")),
				int(lead.get("seats", 0)), SimPipeline.heat_word(heat),
				int(lead.get("age_weeks", 0)), (" · dies in %d wk" % dies) if dying else ""],
			"value": "≈$%s/wk" % b.fmt(int(round(float(lead.get("seats", 0)) * unit))),
			"col": DeskKit.PEN if dying else DeskKit.INK,
			"sev": 3 if dying else 0,
			"on_press": func() -> void: _prefill(b, lead)})
		if heat <= 16 and not b.has_control("push_cold"):
			b.mark_control("push_cold", Rect2(DeskKit.X_ID, row_y, 1120.0, 44.0))
		shown += 1
	DeskKit.more(b, Vector2(DeskKit.X_ID, y), s.leads.size() - shown, "sit colder below these")

## A column's focused list — the header press opened it.
static func _ent_column_focus(b, s: GameState, stage: String) -> void:
	DeskKit.back(b, "back to the board", func() -> void:
		b.desk["mode"] = "")
	b.label(stage.to_upper(), Vector2(DeskKit.X_ID, 58.0), DeskKit.TITLE, DeskKit.INK, 500.0)
	var y := 120.0
	var decay := SimPipeline.decay_for(s)
	var unit := SimPipeline.unit_rev_wk(s)
	var any := false
	for i in SimPipeline.leads_by_heat(s):
		var lead: Dictionary = s.leads[i]
		if String(lead.get("stage", "meeting")) != stage:
			continue
		any = true
		var heat := int(lead.get("heat", 0))
		var dies := SimPipeline.weeks_to_cold(heat, decay)
		var dying := dies <= 2
		y = DeskKit.hero_row(b, y, {
			"name": String(lead.get("name", "a prospect")),
			"facts": "%d seats · %s · wk %d%s%s" % [int(lead.get("seats", 0)),
				SimPipeline.heat_word(heat), int(lead.get("age_weeks", 0)),
				(" · dies in %d wk" % dies) if dying else "",
				(" · " + String(lead.get("flavor", ""))) if String(lead.get("flavor", "")) != "" else ""],
			"value": "≈$%s/wk" % b.fmt(int(round(float(lead.get("seats", 0)) * unit))),
			"col": DeskKit.PEN if dying else DeskKit.INK,
			"sev": 3 if dying else 0,
			"on_press": func() -> void: _prefill(b, lead)})
	if not any:
		DeskKit.empty(b, Vector2(DeskKit.X_ID, y), "nothing sits at this gate this week.", "")
	_drafted_chip(b)
	_ent_foot(b, s)

static func _ent_foot(b, s: GameState) -> void:
	var st: Dictionary = s.pipe_stats
	var signed := int(st.get("signed", 0))
	var lost := int(st.get("lost", 0))
	var seats := int(st.get("seats_signed", 0))
	var decided := signed + lost
	var win := "?" if decided <= 0 else "%d/%d (%d%%)" % [signed, decided,
		int(round(100.0 * float(signed) / float(decided)))]
	var cycle := "?" if signed <= 0 else "%d" % int(round(float(st.get("cycle_sum", 0)) / float(signed)))
	var cost := "?" if seats <= 0 else "$%d" % int(round(float(st.get("spend", 0.0)) / float(seats)))
	DeskKit.footer(b, {
		"computed": "win rate %s · avg cycle %s wks · cost per signed seat ≈ %s · a seat pays ≈ $%.0f/wk" % [
			win, cycle, cost, SimPipeline.unit_rev_wk(s)],
		"rules": "deals move on written moves — the journal pushes them (a push moves heat, never a stage) · " \
			+ String(SimPipeline.COACH.get(s.era, "")),
		"y": 806.0, "rules_y": 840.0})

# ─────────────────────────────── drawn pieces ────────────────────────────────

## One week of the river: stacked origin segments under one seeded ink edge.
## A ghost week (no origin data yet) keeps its outline and loses its wash.
class _RiverBar:
	extends Control
	var segs: Array = []       ## [bought, friends, walked in] magnitudes
	var known := true
	func _draw() -> void:
		var w := size.x
		var h := size.y
		if known:
			var total := 0.0
			for v in segs:
				total += maxf(float(v), 0.0)
			var cols := [DeskKit.BLUE, DeskKit.YELL, DeskKit.SAGE]
			var yy := h
			for i in mini(segs.size(), 3):
				var frac := maxf(float(segs[i]), 0.0) / maxf(total, 0.001)
				var sh := h * frac
				draw_rect(Rect2(0, yy - sh, w, sh), Color(cols[i], 0.6))
				yy -= sh
		var rng := RandomNumberGenerator.new()
		rng.seed = 29 + int(position.x) % 17
		var pts := PackedVector2Array()
		var corners := [Vector2(1, 1), Vector2(w - 1, 1), Vector2(w - 1, h - 1), Vector2(1, h - 1)]
		for i in 4:
			var a: Vector2 = corners[i]
			var bb: Vector2 = corners[(i + 1) % 4]
			for k in 7:
				pts.append(a.lerp(bb, float(k) / 7.0)
					+ Vector2(rng.randf_range(-1.0, 1.0), rng.randf_range(-1.0, 1.0)))
		pts.append(pts[0])
		draw_polyline(pts, DeskKit.INK if known else Color(DeskKit.INK, 0.35),
			2.4 if known else 2.0, true)
		if not known:
			var f := get_theme_default_font()
			if f != null:
				draw_string(f, Vector2(w * 0.5 - 6.0, h * 0.5 + 8.0), "?",
					HORIZONTAL_ALIGNMENT_LEFT, -1, 22, Color(DeskKit.INK, 0.4))
