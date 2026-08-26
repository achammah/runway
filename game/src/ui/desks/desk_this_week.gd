class_name DeskThisWeek
extends RefCounted
## DESK — THE LOG · "this week". W2 lane: L-COMPANY.
## THE QUESTION THIS DESK ANSWERS: "what happened, and what's our move?"
## The stub EMBEDS the shipped DeskVitals desk so nothing regresses while the
## lane reworks this page to its locked pick (docs/design/DECISIONS.md).
## The shared components live in game/src/ui/components.gd (DeskKit).

const QUESTION := "what happened, and what's our move?"

## The group overview's card reads this: the page's hero, one number + one
## sentence (DECISIONS: the quartet card IS the page's hero verbatim).
static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "week %d" % _s.week, "line": "the desk you play from"}

static func draw(b) -> void:
	DeskVitals.draw(b)
	DeskKit.hero_question(b, QUESTION)

static func handle(b, id: String) -> void:
	DeskVitals.handle(b, id)
