class_name DeskCustomersPage
extends RefCounted
## DESK — REVENUE · "customers". W2 lane: L-REV.
## THE QUESTION THIS DESK ANSWERS: "who is coming and staying?"
## The stub EMBEDS the shipped DeskCustomers desk so nothing regresses while the
## lane reworks this page to its locked pick (docs/design/DECISIONS.md).
## The shared components live in game/src/ui/components.gd (DeskKit).

const QUESTION := "who is coming and staying?"

## The group overview's card reads this: the page's hero, one number + one
## sentence (DECISIONS: the quartet card IS the page's hero verbatim).
static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "%d customers" % _s.traction, "line": "the scoreboard — count, net, kept"}

static func draw(b) -> void:
	DeskCustomers.draw(b)
	DeskKit.hero_question(b, QUESTION)

static func handle(b, id: String) -> void:
	DeskCustomers.handle(b, id)
