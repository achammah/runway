class_name DeskCrew
extends RefCounted
## DESK — the binder's `crew` tab. Spec: docs/design/02-labor-market.md
##
## `binder.gd` dispatches the tab body here and passes ITSELF, so this file draws
## through the binder's own helpers and never reaches into the sheet directly.
##
## WHAT IS HERE NOW is today's shipped read-only roster, moved verbatim off the
## binder so the lane REPLACES a working baseline instead of a blank file. The
## lane's job (02): the roster/hiring pen toggle (both halves cannot share one
## sheet — the ruling is in 00-spine §11), grown roster rows with loaded cost and
## skill pips, raise and two-tap let-go, open roles with advert steppers,
## applicant cards, payroll totals, the rules footer.
##
## The bar every surface ships at (docs/design/00-spine.md §11): readable first
## pass by a tired player; concepts named in real business terms with a teaching
## line where a number first appears; no dead ends and every state leavable;
## drawn in the game's hand, never a SaaS panel. The shared components live in
## game/src/ui/components.gd (DeskKit) — use them, never fork them.

## Draw the roster/hiring toggle: employee rows, open roles, applicant cards. `b`
## is the Binder itself (untyped to keep the two files free of a cyclic class
## dependency).
static func draw(b) -> void:
	var state: GameState = b.state
	b.icon("you", Vector2(10, 6))
	var who := state.founder_name if state.founder_name != "" else "the founder"
	b.label("%s — lvl %d · XP %d/%d spent · exhaustion %d/6" % [who,
		state.level, state.xp_spent, state.xp, state.exhaustion], Vector2(100, 20), 32)
	var stats := PackedStringArray()
	for st_n in ["build", "sell", "raise", "recruit", "grit"]:
		stats.append("%s %d" % [st_n, int(state.competences.get(st_n, 3))])
	b.label("  ·  ".join(stats), Vector2(100, 64), 27, Color(Binder.INK, 0.8))
	var y := 130.0
	for cf in state.cofounders:
		b.icon("cofd_tech", Vector2(10, y))
		var cf_name := str(cf.get("name", "")).strip_edges()
		var cf_role := str(cf.get("role", "?"))   # str(): a role can arrive as an int
		b.label("%s%s cofounder · %.0f%% equity · loyalty %d" % [
			(cf_name + " — ") if cf_name != "" else "", cf_role,
			float(cf.get("equity_diluted", cf.get("equity", 0))), int(cf.get("loyalty", 70))],
			Vector2(100, y + 16), 28)
		y += 84.0
	for e in state.employees:
		b.icon("employee", Vector2(10, y))
		b.label("%s — %s · $%s/wk · burnout %d" % [String(e.get("name", "?")),
			String(e.get("role", "?")), b.fmt(int(e.get("salary", 0))), int(e.get("burnout", 0))],
			Vector2(100, y + 16), 28)
		y += 84.0
	for h in state.pipeline:
		b.icon("employee", Vector2(10, y))
		b.label("%s — %s · ONBOARDING (paid, not yet productive)" % [
			String(h.get("name", "?")), String(h.get("role", "?"))], Vector2(100, y + 16), 28,
			Color(Binder.INK, 0.55))
		y += 84.0
	b.label("morale:", Vector2(10, y + 10), 28)
	b.spark(b.series("morale"), Vector2(120, y - 8), Vector2(1000, 120), Binder.SAGE)

## A press inside this desk. `id` is whatever the desk's own draw registered.
static func handle(_b, _id: String) -> void:
	pass
