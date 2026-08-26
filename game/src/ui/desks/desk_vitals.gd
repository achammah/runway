class_name DeskVitals
extends RefCounted
## DESK — the binder's `vitals` tab. Spec: docs/design/11-binder-rework.md §vitals
##
## `binder.gd` dispatches the tab body here and passes ITSELF, so this file draws
## through the binder's own helpers and never reaches into the sheet directly.
##
## THE QUESTION THIS DESK ANSWERS: "how are we doing?" — the company's pulse in
## one read. Cash first and biggest, the health band under it, then the money's
## own shape over time, then the week's ins and outs, the burn, what the company
## would fetch if anyone asked, and the heat it is carrying.
##
## The bar every surface ships at (docs/design/00-spine.md §11): readable first
## pass by a tired player; concepts named in real business terms with a teaching
## line where a number first appears; no dead ends and every state leavable;
## drawn in the game's hand, never a SaaS panel. The shared components live in
## game/src/ui/components.gd (DeskKit) — use them, never fork them.

## Draw the pulse. `b` is the Binder itself (untyped to keep the two files free
## of a cyclic class dependency).
static func draw(b) -> void:
	var state: GameState = b.state
	b.icon("cash", Vector2(10, 6))
	b.label("$%s in the bank" % b.fmt(state.cash), Vector2(100, 10), 46)
	b.label(SimEngine.health_band(state), Vector2(100, 66), 30,
		DeskKit.PEN if SimEngine.runway_weeks(state) <= 10 else DeskKit.SAGE)
	b.label("cash, drawn weekly:", Vector2(10, 140), 24, Color(DeskKit.INK, 0.6))
	b.spark(b.series("cash"), Vector2(10, 172), Vector2(1120, 190), DeskKit.BLUE)
	var last: Dictionary = state.metric_history[-1] if state.metric_history.size() > 0 else {}
	b.label("last week: $%s in · $%s out" % [b.fmt(int(last.get("revenue", 0))),
		b.fmt(int(last.get("burn", 0)))], Vector2(10, 386), 30)
	var payroll := 0
	for e in state.employees:
		payroll += int(e.get("salary", 0))
	# the marketing number is the CHANNEL SUM now: the single legacy field goes
	# stale the moment the ledger's four lanes carry the spend (04 §6.2)
	var mk_pnl: Dictionary = state.get_meta("pnl", {})
	var mk_burn := int(mk_pnl.get("marketing", int(SimFunnel.spend_total(state))))
	# ONE HONEST DEBT FIGURE across shark, bank and venture notes (06 §9): the
	# single `loan_principal` field stopped being the whole story the week the
	# structured notes landed, and a founder must never read a debt-free line
	# with a bank note on the books.
	var debt_owed := SimBank.debt_total(state)
	var note_count := state.loans.size() + (1 if state.loan_principal > 0 else 0)
	b.label("burn: rent $%s · payroll $%s · marketing $%s%s" % [
		b.fmt(int(GameState.ERA_RENT.get(state.era, 150))), b.fmt(payroll),
		b.fmt(mk_burn),
		("  ·  DEBT $%s across %d notes (worst %d%%/wk)" % [b.fmt(debt_owed), note_count,
			int(round(SimBank.worst_rate(state) * 100.0))]) if debt_owed > 0 else ""],
		Vector2(10, 432), 27, Color(DeskKit.INK, 0.8))
	b.label("valuation, if anyone asked: $%s" % b.fmt(SimEngine.valuation(state)),
		Vector2(10, 486), 30)
	# THE PRICE LINE OWNS 532–566 AT 27px, so the hype caption cannot start at 556:
	# it was written over the line above it and its own spark's wash was drawn over
	# it in turn. 574 clears both, and the spark still lands inside the 760 pane.
	b.label("price ×%.2f  ·  the market is %s" % [state.price_mult,
		"warm" if state.market_trend > 1.05 else ("cold" if state.market_trend < 0.95 else "even")],
		Vector2(10, 532), 27, Color(DeskKit.INK, 0.8))
	# the hype chart moved here when the roadmap took the product sheet (07)
	b.label("hype:", Vector2(10, 574), 24, Color(DeskKit.INK, 0.6))
	b.spark(b.series("hype"), Vector2(10, 606), Vector2(1120, 120), DeskKit.YELL)

## A press inside this desk. `id` is whatever the desk's own draw registered.
## Vitals is a page you READ: nothing on it is set from here, every number on it
## is somebody else's desk stated plainly.
static func handle(_b, _id: String) -> void:
	pass
