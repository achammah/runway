class_name DeskThreatsPage
extends RefCounted
## DESK — THE COMPANY · "threats". W2 lane: L-COMPANY.
## THE QUESTION THIS DESK ANSWERS: "what could kill us?"
## The stub EMBEDS the shipped DeskThreats desk so nothing regresses while the
## lane reworks this page to its locked pick (docs/design/DECISIONS.md).
## The shared components live in game/src/ui/components.gd (DeskKit).

const QUESTION := "what could kill us?"

## The group overview's card reads this: the page's hero, one number + one
## sentence (DECISIONS: the quartet card IS the page's hero verbatim).
static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "%d live" % SimEngine.attention_items(_s).size(), "line": "the command center — loudest first"}

static func draw(b) -> void:
	DeskThreats.draw(b)
	DeskKit.hero_question(b, QUESTION)

static func handle(b, id: String) -> void:
	DeskThreats.handle(b, id)
