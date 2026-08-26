class_name DeskStreetPage
extends RefCounted
## DESK — THE COMPANY · "the street". W2 lane: L-COMPANY.
## THE QUESTION THIS DESK ANSWERS: "what is the world doing to us?"
## The stub EMBEDS the shipped DeskStreet desk so nothing regresses while the
## lane reworks this page to its locked pick (docs/design/DECISIONS.md).
## The shared components live in game/src/ui/components.gd (DeskKit).

const QUESTION := "what is the world doing to us?"

## The group overview's card reads this: the page's hero, one number + one
## sentence (DECISIONS: the quartet card IS the page's hero verbatim).
static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "the street", "line": "the weather, the rivals, the investors' mood"}

static func draw(b) -> void:
	DeskStreet.draw(b)
	DeskKit.hero_question(b, QUESTION)

static func handle(b, id: String) -> void:
	DeskStreet.handle(b, id)
