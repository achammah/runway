class_name DeskLedger
extends RefCounted
## DESK — the binder's `the ledger` tab. Spec: docs/design/04-funnel-channels.md §6.1
##
## `binder.gd` dispatches the tab body here and passes ITSELF, so this file draws
## through the binder's own helpers and never reaches into the sheet directly.
##
## EIGHT LEVERS IN TWO BLOCKS. The four acquisition channels ARE the growth
## strategy — one blended lever hid the core decision — so they get their own
## sub-block at a tighter 58px pitch under one MARKETING header, above the
## divider; the org levers keep their fuller 62px rows below it. Every row prints
## what its money is doing RIGHT NOW, out of the engine's own formulas: that is
## house law (10-interface-language §2.1), and for the channels it is also the
## whole lesson — the era discount, the compounding stock, the NPS gate.
##
## THE SHEET NEVER SCROLLS AND NEVER OVERFLOWS. Eight rows, the unit economics,
## the P&L, the bank, the bottom line and a warning all have to live inside
## 760px, so the lower half sits at FIXED slots and exactly one line ever yields
## its slot (the unit-econ line, and only when both warnings fire).

# ── the two blocks ───────────────────────────────────────────────────────────
## [budget key, the word on the page, what the money actually does].
##
## EVERY WHY LINE IS ONE MEASURED LINE. This sheet is a FIXED grid — eight rows
## at 58/62px pitch, then five slots that cannot move — so a why that wraps does
## not push the row down, it writes itself through the NEXT lever's name and, at
## the bottom, straight through the warning. Each string below measures under
## 430px in the hand at its own size (18px channels, 21px org); a longer one is
## a shipped overstrike, not a longer sentence.
const CHANNEL_LEVERS := [
	["ads", "ads", "paid reach — instant, saturates hard; runs only while fed"],
	["content", "content", "the library — slow to build, works while you sleep, rots if starved"],
	["referrals", "referrals", "promoters talking — word of mouth, multiplied; needs care"],
	["outbound", "outbound", "lists and cold calls — buys reach AND closing; born for enterprise"],
]
const ORG_LEVERS := [
	["sales", "sales", "closing — $600/wk buys one more part-time seller"],
	["care", "care", "retention — up to 30% less churn as care nears $3k"],
	["rnd", "rnd", "product — ~+1 quality per $1,200/wk, and debt melts"],
	["office", "office", "the office — food, perks; morale to +3/wk near $2k"],
]
const LEVER_STEPS := [0, 250, 500, 1000, 2000, 4000, 8000]

## ONE COLUMN GRAMMAR down the whole sheet (10-interface-language §1.4):
## identity → money → live effect → controls. Eight rows share it, or the page
## reads as two pages taped together.
const X_VALUE := 455.0
const X_EFFECT := 640.0
const X_MINUS := 1000.0
const X_PLUS := 1064.0

## THE FIXED LOWER HALF, measured against the 760px pane.
const Y_HEADER := 62.0
const Y_CHANNELS := 96.0
const CHANNEL_PITCH := 58.0
const Y_DIVIDER := 333.0
const Y_ORG := 340.0
const ORG_PITCH := 62.0
const Y_UNIT := 592.0
const Y_IN := 626.0
const Y_OUT := 660.0
const Y_BOTTOM := 694.0
const Y_RULES := 734.0

static func draw(b: Binder) -> void:
	var state: GameState = b.state
	b.label("the ledger — where this week's money goes", Vector2(10, 6), 38)

	# ── THE MIX: its total and the blended CAC stay in view while you step it,
	# because the question this block answers is "what does a customer cost".
	var ch_total := 0
	for lv0 in CHANNEL_LEVERS:
		ch_total += int(state.budgets.get(String(lv0[0]), 0))
	var fn := SimFunnel.funnel(state)
	var bl_cac := int(SimFunnel.num(fn, "blended_cac"))
	b.label("MARKETING — the funnel mix · $%s/wk · blended CAC %s" % [b.fmt(ch_total),
		("$%s" % b.fmt(bl_cac)) if bl_cac > 0 else "not yet knowable"],
		Vector2(10, Y_HEADER), 24, Color(Binder.INK, 0.6))
	var y := Y_CHANNELS
	for lv in CHANNEL_LEVERS:
		var cat := String(lv[0])
		var cur := int(state.budgets.get(cat, 0))
		b.label(String(lv[1]).to_upper(), Vector2(10, y), 24)
		b.label(String(lv[2]), Vector2(10, y + 27.0), 18, Color(Binder.INK, 0.6), 430.0)
		b.label("$%s/wk" % b.fmt(cur), Vector2(X_VALUE, y + 2.0), 26, Binder.PEN, 175.0)
		# THE BOUND PRINTS ITS REASON where the effect was (§2.1): a step the
		# world refuses is a lesson about the era, not a dead button.
		var up := channel_step(state, cat, cur, 1)
		var eff := SimFunnel.lever_effect(state, cat)
		if up == cur and cur > 0:
			eff = "the mix is at the era's ceiling"
		b.label(eff, Vector2(X_EFFECT, y + 8.0), 20, Color(Binder.INK, 0.75), 340.0)
		_step(b, "−", Vector2(X_MINUS, y + 2.0), cat, channel_step(state, cat, cur, -1))
		_step(b, "+", Vector2(X_PLUS, y + 2.0), cat, up)
		y += CHANNEL_PITCH
	DeskKit.rule(b, Y_DIVIDER)

	# ── the org levers: same words, same controls, the fuller pitch
	y = Y_ORG
	for lv2 in ORG_LEVERS:
		var cat2 := String(lv2[0])
		var cur2 := int(state.budgets.get(cat2, 0))
		b.label(String(lv2[1]).to_upper(), Vector2(10, y), 28)
		b.label(String(lv2[2]), Vector2(10, y + 32.0), 21, Color(Binder.INK, 0.6), 430.0)
		b.label("$%s/wk" % b.fmt(cur2), Vector2(X_VALUE, y + 4.0), 30, Binder.PEN, 175.0)
		b.label(lever_effect(state, cat2, cur2), Vector2(X_EFFECT, y + 12.0), 20,
			Color(Binder.INK, 0.75), 340.0)
		_step(b, "−", Vector2(X_MINUS, y), cat2, lever_step(state, cur2, -1))
		_step(b, "+", Vector2(X_PLUS, y), cat2, lever_step(state, cur2, 1))
		y += ORG_PITCH

	# ── the math, honestly, at fixed slots
	var rw := SimEngine.runway_weeks(state)
	var warns: Array[String] = []
	if rw <= 4 and rw < 999:
		# NO SYMBOL LEADS THIS LINE. The hand carries no warning sign (nor arrows,
		# triangles or clocks), so a typed one arrives in a borrowed face — or, on
		# another machine, as a box. Coral and the words do the alarming.
		warns.append("this spend kills the company in %d weeks — cut it or earn it" % rw)
	if state.cash < 0:
		warns.append("THE RED: %d of 3 weeks below zero. At three, it's over." % state.weeks_in_red)
	# WARNINGS OUTRANK WISDOM (§2.7), and when BOTH fire the unit-econ line
	# yields its slot to the first — the only line on this sheet that gives way.
	if warns.size() >= 2:
		b.label(warns[0], Vector2(10, Y_UNIT), 24, Binder.PEN, 1100.0)
	else:
		var ue: Dictionary = state.get_meta("unit_econ", {})
		var arpu := float(ue.get("arpu", 0.0))
		var cacv := int(ue.get("cac", 0))
		var ltvv := int(ue.get("ltv", 0))
		var pb := int(ue.get("payback_wk", 0))
		b.label("a customer pays ≈ $%.0f/wk · costs $%s to win (CAC) · is worth $%s over their stay (LTV) · pays back in %s"
			% [arpu, (b.fmt(cacv) if cacv > 0 else "?"), (b.fmt(ltvv) if ltvv > 0 else "?"),
			("%d wks" % pb) if pb > 0 else "—"], Vector2(10, Y_UNIT), 23,
			Color(Binder.INK, 0.75), 1100.0)

	var lever_sum := 0
	for k in state.budgets:
		lever_sum += int(state.budgets[k])
	var pnl: Dictionary = state.get_meta("pnl", {})
	if not pnl.is_empty():
		b.label("last week: in $%s · serving $%s%s" % [b.fmt(int(pnl.get("revenue", 0))),
			b.fmt(int(pnl.get("cogs", 0))),
			("  (learning ×%.2f)" % float(pnl.get("learning", 1.0))) if float(pnl.get("learning", 1.0)) < 0.995 else ""],
			Vector2(10, Y_IN), 24, Binder.BLUE, 1100.0)
		b.label(_out_line(b, pnl), Vector2(10, Y_OUT), 24, Color(Binder.INK, 0.8), 1100.0)
		var net := int(pnl.get("net", 0))
		# BREAK-EVEN (06) closes the bottom line: the number of customers that
		# ends the argument, and how far away it is right now.
		var be := SimBank.break_even_customers(state)
		var be_txt := ""
		if be > 0:
			be_txt = " · break-even %s (%s now)" % [b.fmt(be), b.fmt(state.traction)]
		b.label("THE BOTTOM LINE: %s$%s a week · levers total $%s/wk · runway %s%s" % [
			"+" if net >= 0 else "−", b.fmt(absi(net)), b.fmt(lever_sum),
			("%d weeks" % rw) if rw < 999 else "gaining money", be_txt],
			Vector2(10, Y_BOTTOM), 27, (Binder.SAGE if net >= 0 else Binder.PEN), 1100.0)
	else:
		b.label("levers total $%s/wk · runway %s" % [b.fmt(lever_sum),
			("%d weeks" % rw) if rw < 999 else "gaining money"], Vector2(10, Y_BOTTOM), 27)

	# ── THE COST OF MONEY, on its own line: interest and tax sit OUTSIDE burn
	# (00-spine §2), which is the whole pedagogy — operating profit, then the
	# bank, then the state. The full statement lives on THE BANK.
	var interest := int(pnl.get("interest", 0))
	var tax := int(pnl.get("tax", 0))
	var principal := int(state.get_meta("bank_principal_wk", 0))
	if interest + tax + principal > 0:
		b.label("the bank & the state: interest $%s · principal $%s · tax $%s" % [
			b.fmt(interest), b.fmt(principal), b.fmt(tax)],
			Vector2(600, Y_UNIT + 34.0), 20, Color(Binder.INK, 0.6), 540.0)

	# ── ONE SLOT AT THE FOOT: the loudest warning, else the laws of this world
	if not warns.is_empty():
		b.label(warns[warns.size() - 1], Vector2(10, Y_RULES), 20, Binder.PEN, 1100.0)
	else:
		b.label("the rules of this world: reach saturates · content compounds · only capacity closes · churn is a leaky bucket · three weeks below zero ends it",
			Vector2(10, Y_RULES), 20, Color(Binder.INK, 0.5), 1100.0)

# ── THE COMPACT "out:" LINE ──────────────────────────────────────────────────
## Nine lanes can all bill in one week and the sheet still has ONE line for it
## (00-spine §11). The four standing costs always print; every lane that spent
## something adds its own named tail, in this fixed order, until the line is
## full — and what does not fit says so and points at the desk that keeps the
## full books. A lane that spent nothing renders nothing at all, so a quiet week
## (or a run with no factory) reads exactly as it always did.
const OUT_TAILS := [
	["offer_fixed", "catalog"],       # 01 tools, licenses, storage
	["severance", "severance"],       # 02 the firing invoice
	["recruiting", "recruiting"],     # 02 the recruiter's retainer
	["production", "production"],     # 09 built in house
	["subcontract", "subcontract"],   # 09 someone else's line
	["equip_upkeep", "upkeep"],       # 09 machines do not maintain themselves
	["carrying", "carrying"],         # 09 stock costs money to sit still
	["incident", "unforeseen"],
]
## What one 24px line of the hand holds across the 1100px column. Counted, not
## measured, so both engines break the line in exactly the same place.
const OUT_CHARS := 126

static func _out_line(b: Binder, pnl: Dictionary) -> String:
	var line := "out: rent $%s · payroll $%s · infra $%s · levers $%s" % [
		b.fmt(int(pnl.get("rent", 0))), b.fmt(int(pnl.get("payroll", 0))),
		b.fmt(int(pnl.get("infra", 0))),
		b.fmt(int(pnl.get("marketing", 0)) + int(pnl.get("sales", 0))
			+ int(pnl.get("care", 0)) + int(pnl.get("rnd", 0)) + int(pnl.get("office", 0)))]
	var tails: Array[String] = []
	for t in OUT_TAILS:
		var v := int(pnl.get(String(t[0]), 0))
		if v > 0:
			tails.append(" · %s $%s" % [String(t[1]), b.fmt(v)])
	# the standing commitments are a RATE, not a one-off, so they carry /wk
	var liab := int(pnl.get("liabilities_wk", 0))
	if liab > 0:
		tails.append(" · standing $%s/wk" % b.fmt(liab))
	var shown := 0
	while shown < tails.size():
		var left := tails.size() - shown - 1
		var over := "" if left == 0 else " · +%d lanes — the bank keeps the full books" % left
		if line.length() + tails[shown].length() + over.length() > OUT_CHARS:
			break
		line += tails[shown]
		shown += 1
	if shown < tails.size():
		line += " · +%d lanes — the bank keeps the full books" % (tails.size() - shown)
	return line

## One stepper glyph: a flat ink button that writes the amount the world allows.
static func _step(b: Binder, glyph: String, pos: Vector2, cat: String, to_v: int) -> void:
	var btn := Button.new()
	btn.text = glyph
	btn.position = pos
	btn.size = Vector2(52, 44)
	b.ink_btn(btn)
	btn.pressed.connect(func() -> void:
		b.state.budgets[cat] = to_v
		b.refresh())
	b.pane().add_child(btn)

## THE ERA CAP CLAMPS THE CHANNEL SUM (docs/design/DECISIONS.md — funnel). A step
## up that would push the whole mix past the era's ceiling is refused, and the
## row prints why: clamping per lever would let four channels quadruple what one
## garage is allowed to spend on reach.
static func channel_step(state: GameState, cat: String, cur: int, dir: int) -> int:
	var want := lever_step(state, cur, dir)
	if dir <= 0:
		return want
	var others := 0
	for lv in CHANNEL_LEVERS:
		if String(lv[0]) != cat:
			others += int(state.budgets.get(String(lv[0]), 0))
	if others + want > SimEngine.era_spend_cap(state.era):
		return cur
	return want

## the engine's live math for one lever, in one plain phrase
static func lever_effect(state: GameState, cat: String, v: int) -> String:
	match cat:
		"ads", "content", "referrals", "outbound":
			# the four channels compute their own: the era discount, the
			# compounding stock and the NPS gate all live in the lane
			return SimFunnel.lever_effect(state, cat)
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

static func lever_step(state: GameState, cur: int, dir: int) -> int:
	var idx := 0
	for i in LEVER_STEPS.size():
		if LEVER_STEPS[i] <= cur:
			idx = i
	idx = clampi(idx + dir, 0, LEVER_STEPS.size() - 1)
	return mini(LEVER_STEPS[idx], SimEngine.era_spend_cap(state.era))
