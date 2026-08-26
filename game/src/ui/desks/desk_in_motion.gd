class_name DeskInMotion
extends RefCounted
## DESK — REVENUE · "in motion". W2 lane: L-REV.
## THE QUESTION THIS DESK ANSWERS: "who is on the way to becoming money?"
## The stub EMBEDS the shipped DeskPipeline desk so nothing regresses while the
## lane reworks this page to its locked pick (docs/design/DECISIONS.md).
## The shared components live in game/src/ui/components.gd (DeskKit).

const QUESTION := "who is on the way to becoming money?"

## The group overview's card reads this: the page's hero, one number + one
## sentence (DECISIONS: the quartet card IS the page's hero verbatim).
static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "the board", "line": "river, hot list or stage board — by audience"}

static func draw(b) -> void:
	DeskPipeline.draw(b)
	DeskKit.hero_question(b, QUESTION)

static func handle(b, id: String) -> void:
	DeskPipeline.handle(b, id)
