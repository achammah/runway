class_name DeskCap
extends RefCounted
## DESK — the binder's `cap table` tab. Spec: docs/design/08-board-mna.md
##
## `binder.gd` dispatches the tab body here and passes ITSELF, so this file draws
## through the binder's own helpers and never reaches into the sheet directly.
##
## WHAT IS HERE NOW is today's shipped cap table — the wheel, the rounds, the
## valuation, the dilution preview — moved verbatim off the binder so the lane
## REPLACES a working baseline instead of a blank file. The lane's job (08): the
## fourth slice (the option pool, YELL) so dilution is a drawn wound, the
## covenant and strikes record, the era stage line, the offer/window banner.
##
## The bar every surface ships at (docs/design/00-spine.md §11): readable first
## pass by a tired player; concepts named in real business terms with a teaching
## line where a number first appears; no dead ends and every state leavable;
## drawn in the game's hand, never a SaaS panel. The shared components live in
## game/src/ui/components.gd (DeskKit) — use them, never fork them.

## Draw the option-pool slice, covenant and strikes, the offer/window banner. `b`
## is the Binder itself (untyped to keep the two files free of a cyclic class
## dependency).
static func draw(b) -> void:
	var state: GameState = b.state
	var founder := state.founder_pct
	var cof := 0.0
	for cf in state.cofounders:
		cof += float(cf.get("equity_diluted", cf.get("equity", 0)))
	b.pie([
		{"pct": founder, "col": Binder.PEN, "label": "you %.0f%%" % founder},
		{"pct": cof, "col": Binder.BLUE, "label": "cofounders %.0f%%" % cof},
		{"pct": maxf(100.0 - founder - cof, 0.0), "col": Binder.SAGE,
		 "label": "investors %.0f%%" % maxf(100.0 - founder - cof, 0.0)},
	], Vector2(40, 30), 430.0)
	var y := 60.0
	b.label("rounds:", Vector2(540, 30), 32)
	if state.rounds_raised.is_empty():
		b.label("none yet. every point of the company is still on this table.",
			Vector2(540, y + 20), 27, Color(Binder.INK, 0.7), 560.0)
	for r in state.rounds_raised:
		b.label("· %s — closed" % String(r), Vector2(540, y + 20), 28, Binder.INK, 560.0)
		y += 44.0
	b.label("valuation $%s" % b.fmt(SimEngine.valuation(state)), Vector2(540, y + 80), 30)
	b.label("your slice today: $%s" % b.fmt(int(SimEngine.valuation(state) * state.founder_pct / 100.0)),
		Vector2(540, y + 128), 30, Binder.PEN)
	# what the NEXT round would cost, so dilution is never a surprise
	var val := SimEngine.valuation(state)
	if val > 0:
		var ask := int(float(val) * 0.10)
		var fair_pct := float(ask) / float(val + ask) * 100.0
		var warm := SimEngine.warmth_pct(state)
		b.label("raise ~$%s now → investors ask ≈ %.0f%%%s · your %.0f%% would become ≈ %.0f%%" % [
			b.fmt(ask), fair_pct * 1.3 * (1.0 - warm / 100.0),
			(" (%.0f%% off — they know you)" % warm) if warm > 0.0 else "",
			state.founder_pct, state.founder_pct * (1.0 - fair_pct * 1.3 * (1.0 - warm / 100.0) / 100.0)],
			Vector2(540, y + 186), 24, Color(Binder.INK, 0.7), 620.0)
	if state.has_flag("fundraising_open"):
		b.label("! TERM SHEETS ARE ON THE TABLE — sign in the journal before they expire",
			Vector2(40, 480), 27, Binder.PEN, 1100.0)

## A press inside this desk. `id` is whatever the desk's own draw registered.
static func handle(_b, _id: String) -> void:
	pass
