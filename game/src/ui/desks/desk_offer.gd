class_name DeskOffer
extends RefCounted
## THE OFFER — the first MOMENTARY desk (DECISIONS §4): a gold tab that
## exists only while a buyout is on the table, four numbered zones, then it
## folds into HISTORY. W2 lane: L-OWN, filled — the shell's placeholder
## numbers are gone; everything on this page is the engine's.
##
## Zone 1 decomposes the headline (a headline is not money) · zone 2 applies
## THE WATERFALL to this exact number (SimOwnership.waterfall, pure) ·
## zone 3 reads the fine print aloud (the fishy flags are computed FIELDS —
## some offers are written fishy on purpose) · zone 4 resolves WHO CAN SAY
## NO from the instruments signed years earlier (protective, drag-along).
##
## Resolution: ACCEPT (two-tap; the run ends through the existing exit
## seam) · NEGOTIATE (one counter — the world reprices once) · DECLINE (the
## street hears). Every resolution folds the gold tab away.

const QUESTION := "should we take their money?"

static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	if s.buyout_offer.is_empty():
		return {"big": "an offer", "line": "cash vs stock vs earnout — read the small lines"}
	return {"big": "%s offers %s" % [String(s.buyout_offer.get("buyer", "a buyer")),
			SimOwnership.money_short(int(s.buyout_offer.get("headline", 0)))],
		"line": "cash vs stock vs earnout — read the small lines"}

static func draw(b) -> void:
	var state: GameState = b.state
	var bo: Dictionary = state.buyout_offer
	if bo.is_empty():
		DeskKit.hero_band(b, "the letter left the table",
			"the offer this tab was summoned for is gone — the tab folds into HISTORY.")
		DeskKit.word(b, "fold the tab away", Vector2(DeskKit.X_ID, 180.0), func() -> void:
			b.resolve_momentary("the offer"), DeskKit.STATUS, DeskKit.INK, 300.0)
		return
	var price := int(bo.get("headline", 0))
	var left := maxi(int(bo.get("expires_wk", 0)) - state.week, 0)
	var y := DeskKit.hero_band(b, "%s offers $%s" % [String(bo.get("buyer", "a buyer")), b.fmt(price)],
		"this desk appeared when the letter did — it leaves when you answer.")
	DeskKit.clock_chip(b, 880.0, 12.0, "expires in %d wk%s" % [left, "" if left == 1 else "s"])
	var nb: Label = b.label("while it lives, the raise is frozen by their no-shop ask",
		Vector2(700.0, 44.0), 18, Color(DeskKit.INK, 0.5), 420.0)
	nb.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT

	# ── zone 1 · WHAT'S ON THE TABLE — the headline, decomposed honestly
	var z1 := DeskKit.zone(b, DeskKit.X_ID, y, 548.0, 312.0, 1, "what's on the table",
		"a headline is not money — read what the $%s is made of" % SimOwnership.money_short(price).lstrip("$"))
	DeskKit.ticket(b, z1.content_x, z1.cursor - 2.0, 500.0, {
		"title": "the $%s, decomposed" % SimOwnership.money_short(price).lstrip("$"),
		"lines": [
			{"label": "cash at closing", "value": "$%s" % b.fmt(int(bo.get("cash", 0)))},
			{"label": "their stock (locked %d months)" % (int(bo.get("lockup_wks", 0)) / 4),
				"value": "$%s" % b.fmt(int(bo.get("stock", 0))), "col": DeskKit.PEN},
			{"label": "earnout (if targets hit)", "value": "$%s" % b.fmt(int(bo.get("earnout", 0))),
				"col": DeskKit.PEN},
		],
		"total_label": "certain today", "total_value": "$%s of $%s" % [b.fmt(int(bo.get("cash", 0))), b.fmt(price)],
		"total_col": DeskKit.INK,
		"foot": "and the handcuffs: you must stay %d months for the stock to vest" % (int(bo.get("retention_wks", 0)) / 4)})

	# ── zone 2 · WHO GETS WHAT — the waterfall applied to THIS number
	var z2 := DeskKit.zone(b, DeskKit.X_ID + 572.0, y, 548.0, 312.0, 2, "who gets what",
		"the waterfall, applied to this exact number — in order")
	var wf := SimOwnership.waterfall(state, price)
	var rows: Array = wf.get("rows", [])
	var shown := 0
	for r in rows:
		if shown >= 3:
			break
		var rd: Dictionary = r
		DeskKit.money_row(b, z2, String(rd.get("holder", "?")), "$%s" % b.fmt(int(rd.get("take", 0))))
		shown += 1
	if rows.size() > 3:
		b.label("+%d more in line" % (rows.size() - 3), Vector2(float(z2.content_x), float(z2.cursor)),
			17, Color(DeskKit.INK, 0.5), 300.0)
		z2["cursor"] = float(z2["cursor"]) + 24.0
	var dec := SimOwnership.take_decomposed(bo, int(wf.get("your_take", 0)))
	DeskKit.money_row(b, z2, "YOU", "≈$%s" % b.fmt(int(wf.get("your_take", 0))), DeskKit.SAGE)
	b.label("= $%s cash + $%s locked stock + $%s maybe" % [b.fmt(int(dec.get("cash", 0))),
		b.fmt(int(dec.get("stock", 0))), b.fmt(int(dec.get("earnout", 0)))],
		Vector2(z2.content_x, z2.cursor + 2.0), 17, Color(DeskKit.INK, 0.6), 500.0)
	y += 312.0 + 10.0

	# ── zone 3 · THE FINE PRINT, READ ALOUD — the flags are the lesson
	var z3 := DeskKit.zone(b, DeskKit.X_ID, y, 548.0, 216.0, 3, "the fine print, read aloud",
		"some offers are written fishy on purpose")
	var fy := float(z3.cursor)
	var flags: Array = bo.get("fishy_flags", [])
	for f in flags:
		if fy > float(z3.bottom) - 36.0:
			break
		var fx := DeskKit.clock_chip(b, float(z3.content_x), fy, "FLAG")
		var fl: Label = b.label(String(f), Vector2(fx + 6.0, fy + 2.0), 16, DeskKit.INK, 508.0)
		fl.autowrap_mode = TextServer.AUTOWRAP_OFF
		fl.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
		fl.custom_minimum_size = Vector2(float(z3.x) + 534.0 - fx, 0)
		fy += 38.0
	for c in _clean_lines(bo):
		if fy > float(z3.bottom) - 36.0:
			break
		b.label("CLEAN", Vector2(float(z3.content_x), fy + 2.0), 16, DeskKit.SAGE, 60.0)
		var cl: Label = b.label(String(c), Vector2(float(z3.content_x) + 66.0, fy + 2.0), 16,
			Color(DeskKit.INK, 0.7), 448.0)
		cl.autowrap_mode = TextServer.AUTOWRAP_OFF
		cl.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
		fy += 38.0
	if flags.is_empty() and _clean_lines(bo).is_empty():
		DeskKit.empty(b, Vector2(z3.content_x, z3.cursor + 6.0),
			"a plain offer — no small lines to trip on.", "")

	# ── zone 4 · WHO CAN SAY NO — the powers were signed at the raise
	var z4 := DeskKit.zone(b, DeskKit.X_ID + 572.0, y, 548.0, 216.0, 4, "who can say no",
		"the powers were signed at the raise, years early")
	var py := float(z4.cursor)
	for p in SimOwnership.powers(state, price):
		if py > z4.bottom - 36.0:
			break
		var pd: Dictionary = p
		b.label(String(pd.get("who", "?")), Vector2(z4.content_x, py), 19, DeskKit.INK, 150.0)
		var col := DeskKit.PEN if bool(pd.get("blocks", false)) else \
			(DeskKit.SAGE if String(pd.get("who", "")) == "you" else Color(DeskKit.INK, 0.7))
		b.label(String(pd.get("line", "")), Vector2(z4.content_x + 156.0, py), 17, col, 360.0)
		py += maxf(b.wrap_h(String(pd.get("line", "")), 17, 360.0), 26.0) + 8.0
	y += 216.0 + 14.0

	# ── the three answers — accept armed, one counter, decline
	DeskKit.arm(b, "offer_accept", "ACCEPT — the two-tap", "press again — the company sells",
		Vector2(DeskKit.X_ID, y), func() -> void:
			SimOwnership.buyout_accept(b.state)
			b.resolve_momentary("the offer"), 300.0)
	if bool(bo.get("countered", false)):
		b.label("one counter is all the room there was", Vector2(DeskKit.X_ID + 330.0, y + 8.0),
			DeskKit.DETAIL, Color(DeskKit.INK, 0.4), 300.0)
	else:
		DeskKit.word(b, "NEGOTIATE — one counter", Vector2(DeskKit.X_ID + 330.0, y), func() -> void:
			SimOwnership.buyout_negotiate(b.state), DeskKit.STATUS, DeskKit.INK, 310.0)
	DeskKit.word(b, "DECLINE", Vector2(DeskKit.X_ID + 680.0, y), func() -> void:
		SimOwnership.buyout_decline(b.state)
		b.resolve_momentary("the offer"), DeskKit.STATUS, Color(DeskKit.PEN, 0.9), 200.0)
	DeskKit.footer(b, {
		"computed": "answered -> this tab folds into HISTORY · declined offers can sour, or come back higher",
		"rules": "the street hears everything",
		"y": 812.0, "rules_y": 846.0})

## The clean rows the desk says out loud beside the flags — what is NOT fishy.
static func _clean_lines(bo: Dictionary) -> Array:
	var out: Array = []
	if not bool(bo.get("retention_carve", false)):
		out.append("the retention pool is carved from the buyer's side, not from your share — this one is fair.")
	if int(bo.get("earnout", 0)) > 0 and String(bo.get("earnout_controller", "")) == "neutral":
		out.append("the earnout's targets are measured by a neutral auditor — as clean as earnouts get.")
	if int(bo.get("lockup_wks", 0)) > 0 and int(bo.get("lockup_wks", 0)) < 52:
		out.append("the stock unlocks inside a year — short, as lockups go.")
	return out

static func _resolve(b) -> void:
	b.resolve_momentary("the offer")

static func handle(b, id: String) -> void:
	if id == "resolve":
		_resolve(b)
