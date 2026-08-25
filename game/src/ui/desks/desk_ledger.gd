class_name DeskLedger
extends RefCounted
## DESK — the binder's `the ledger` tab. Owner: LANE 04 (funnel channels).
## Extracted verbatim from binder.gd by the coordinator so the ledger has ONE
## writer; lane 04 replaces this body with the 8-lever two-block page
## (docs/design/04-funnel-channels.md §6.1) and owns it from here.

# ── tab 1: THE LEDGER — the levers, the math, the truth about the money ─────
## [budget key, the word on the page, what the money actually does]. The key and
## the word part company for the top lever: the state key migrated to `ads` when
## the four acquisition channels landed, while the founder still calls the whole
## top of the funnel MARKETING until the channels unlock at coworking.
const LEVERS := [
	["ads", "marketing", "reach — more people hear of you; saturates past ~$2k"],
	["sales", "sales", "closing — every $600/wk closes like one more part-time seller"],
	["care", "care", "retention — up to 30% less churn as care approaches $3k"],
	["rnd", "rnd", "product — ships ~+1 quality per $1,200/wk and pays down debt"],
	["office", "office", "the office — food, perks, benefits; morale climbs toward +3/wk by ~$2k"],
]
const LEVER_STEPS := [0, 250, 500, 1000, 2000, 4000, 8000]

static func draw(b: Binder) -> void:
	b.label("the ledger — where this week's money goes", Vector2(10, 6), 38)
	var y := 78.0
	for lv in LEVERS:
		var cat := String(lv[0])
		var cur := int(b.state.budgets.get(cat, 0))
		b.label(String(lv[1]).to_upper(), Vector2(10, y), 28)
		b.label(String(lv[2]), Vector2(10, y + 34), 21, Color(Binder.INK, 0.6))
		b.label("$%s/wk" % b.fmt(cur), Vector2(520, y + 4), 30, Binder.PEN)
		# WHAT THIS MONEY IS DOING RIGHT NOW, from the engine's own formulas —
		# the mechanics visible at the point of the decision
		b.label(_lever_effect(b, cat, cur), Vector2(688, y + 12), 24, Color(Binder.INK, 0.75))
		var minus := Button.new()
		minus.text = "−"
		minus.position = Vector2(1000, y)
		minus.size = Vector2(52, 46)
		b.ink_btn(minus)
		minus.pressed.connect(func() -> void:
			b.state.budgets[cat] = _lever_step(b, cur, -1)
			b.refresh())
		b.pane().add_child(minus)
		var plus := Button.new()
		plus.text = "+"
		plus.position = Vector2(1064, y)
		plus.size = Vector2(52, 46)
		b.ink_btn(plus)
		plus.pressed.connect(func() -> void:
			b.state.budgets[cat] = _lever_step(b, cur, 1)
			b.refresh())
		b.pane().add_child(plus)
		y += 78.0
	# the math, honestly — one running cursor, compact: five levers + the
	# P&L + the warnings all live inside the 760px sheet
	var ue: Dictionary = b.state.get_meta("unit_econ", {})
	var lever_sum := 0
	for k in b.state.budgets:
		lever_sum += int(b.state.budgets[k])
	var rw := SimEngine.runway_weeks(b.state)
	var cy := y + 4.0
	var arpu := float(ue.get("arpu", 0.0))
	var cacv := int(ue.get("cac", 0))
	var ltvv := int(ue.get("ltv", 0))
	var pb := int(ue.get("payback_wk", 0))
	b.label("a customer pays ≈ $%.0f/wk · costs $%s to win (CAC) · is worth $%s over their stay (LTV) · pays back in %s"
		% [arpu, (b.fmt(cacv) if cacv > 0 else "?"), (b.fmt(ltvv) if ltvv > 0 else "?"),
		("%d wks" % pb) if pb > 0 else "—"], Vector2(10, cy), 23, Color(Binder.INK, 0.75), 1100.0)
	cy += 34.0
	# THE WEEK, HONESTLY (owner: a real business sim knows its running cost):
	# the engine's own P&L record, every lane, and the bottom line in ink.
	var pnl: Dictionary = b.state.get_meta("pnl", {})
	if not pnl.is_empty():
		b.label("last week: in $%s · serving $%s%s" % [b.fmt(int(pnl.get("revenue", 0))),
			b.fmt(int(pnl.get("cogs", 0))),
			("  (learning ×%.2f)" % float(pnl.get("learning", 1.0))) if float(pnl.get("learning", 1.0)) < 0.995 else ""],
			Vector2(10, cy), 24, Binder.BLUE, 1100.0)
		cy += 34.0
		b.label("out: rent $%s · payroll $%s · infra $%s · levers $%s%s%s" % [
			b.fmt(int(pnl.get("rent", 0))), b.fmt(int(pnl.get("payroll", 0))),
			b.fmt(int(pnl.get("infra", 0))),
			b.fmt(int(pnl.get("marketing", 0)) + int(pnl.get("sales", 0)) + int(pnl.get("care", 0)) + int(pnl.get("rnd", 0)) + int(pnl.get("office", 0))),
			(" · unforeseen $%s" % b.fmt(int(pnl.get("incident", 0)))) if int(pnl.get("incident", 0)) > 0 else "",
			(" · standing $%s/wk" % b.fmt(int(pnl.get("liabilities_wk", 0)))) if int(pnl.get("liabilities_wk", 0)) > 0 else ""],
			Vector2(10, cy), 24, Color(Binder.INK, 0.8), 1100.0)
		cy += 34.0
		var net := int(pnl.get("net", 0))
		b.label("THE BOTTOM LINE: %s$%s a week · levers total $%s/wk · runway %s" % [
			"+" if net >= 0 else "−", b.fmt(absi(net)), b.fmt(lever_sum),
			("%d weeks" % rw) if rw < 999 else "gaining money"],
			Vector2(10, cy), 27, (Binder.SAGE if net >= 0 else Binder.PEN), 1100.0)
		cy += 40.0
	else:
		b.label("levers total $%s/wk · runway %s" % [b.fmt(lever_sum),
			("%d weeks" % rw) if rw < 999 else "gaining money"], Vector2(10, cy), 27)
		cy += 40.0
	if rw <= 4 and rw < 999:
		b.label("⚠ this spend kills the company in %d weeks — cut it or earn it" % rw,
			Vector2(10, cy), 26, Binder.PEN, 1100.0)
		cy += 36.0
	if b.state.cash < 0:
		b.label("THE RED: %d of 3 weeks below zero. At three, it's over." % b.state.weeks_in_red,
			Vector2(10, cy), 26, Binder.PEN, 1100.0)
		cy += 36.0
	b.label("the rules of this world: reach saturates · only capacity closes · churn is a leaky bucket · debt slows everything · three weeks below zero ends it",
		Vector2(10, cy + 2.0), 20, Color(Binder.INK, 0.5), 1100.0)

## the engine's live math for one lever, in one plain phrase
static func _lever_effect(b: Binder, cat: String, v: int) -> String:
	var th := b.state.theta
	match cat:
		"ads", "content", "referrals", "outbound":
			var mult := 1.0 + 1.4 * (1.0 - exp(-float(v) / float(th.get("cac_sat", 900.0))))
			return "reach ×%.2f" % mult if v > 0 else "no reach bought"
		"sales":
			return "+%.1f closers of capacity" % (float(v) / 600.0) if v > 0 else "founder sells alone"
		"care":
			var cut := 30.0 * (1.0 - exp(-float(v) / 1500.0))
			return "churn −%d%%" % int(round(cut)) if v > 0 else "nobody picks up"
		"rnd":
			return "+%.1f product/wk, debt melts" % (float(v) / 1200.0) if v > 0 else "no extra shipping"
		"office":
			var mg := 3.0 * (1.0 - exp(-float(v) / 800.0))
			return "+%.1f morale/wk" % mg if v > 0 else "instant coffee, cold room"
	return ""

static func _lever_step(b: Binder, cur: int, dir: int) -> int:
	var idx := 0
	for i in LEVER_STEPS.size():
		if LEVER_STEPS[i] <= cur:
			idx = i
	idx = clampi(idx + dir, 0, LEVER_STEPS.size() - 1)
	return mini(LEVER_STEPS[idx], SimEngine.era_spend_cap(b.state.era))

