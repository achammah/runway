class_name DeskTeam
extends RefCounted
## DESK — COSTS · "team". W2 lane: L-MONEY.
## THE QUESTION THIS DESK ANSWERS: "who works here and who's asking?"
## The stub EMBEDS the shipped DeskCrew desk so nothing regresses while the
## lane reworks this page to its locked pick (docs/design/DECISIONS.md).
## The shared components live in game/src/ui/components.gd (DeskKit).

const QUESTION := "who works here and who's asking?"

## The group overview's card reads this: the page's hero, one number + one
## sentence (DECISIONS: the quartet card IS the page's hero verbatim).
static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "%d people" % _s.employees.size(), "line": "the payroll ledger, three rungs"}

static func draw(b) -> void:
	DeskCrew.draw(b)
	DeskKit.hero_question(b, QUESTION)

static func handle(b, id: String) -> void:
	DeskCrew.handle(b, id)
