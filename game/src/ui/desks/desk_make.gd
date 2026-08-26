class_name DeskMake
extends RefCounted
## DESK — THE COMPANY · "what we make". W2 lane: L-MAKE.
## THE QUESTION THIS DESK ANSWERS: "what are we making, and how solid is it?"
## The stub EMBEDS the shipped DeskProduct desk so nothing regresses while the
## lane reworks this page to its locked pick (docs/design/DECISIONS.md).
## The shared components live in game/src/ui/components.gd (DeskKit).

const QUESTION := "what are we making, and how solid is it?"

## The group overview's card reads this: the page's hero, one number + one
## sentence (DECISIONS: the quartet card IS the page's hero verbatim).
static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "v0.%d" % _s.product, "line": "the kanban wall — shelf to live"}

static func draw(b) -> void:
	DeskProduct.draw(b)
	DeskKit.hero_question(b, QUESTION)

static func handle(b, id: String) -> void:
	DeskProduct.handle(b, id)
