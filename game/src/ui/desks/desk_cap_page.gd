class_name DeskCapPage
extends RefCounted
## DESK — THE COMPANY · "cap table". W2 lane: L-OWN.
## THE QUESTION THIS DESK ANSWERS: "who owns what and what's the company worth?"
## The stub EMBEDS the shipped DeskCap desk so nothing regresses while the
## lane reworks this page to its locked pick (docs/design/DECISIONS.md).
## The shared components live in game/src/ui/components.gd (DeskKit).

const QUESTION := "who owns what and what's the company worth?"

## The group overview's card reads this: the page's hero, one number + one
## sentence (DECISIONS: the quartet card IS the page's hero verbatim).
static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "you own %.0f%%" % _s.founder_pct, "line": "the slices, the dilution story, the waterfall"}

static func draw(b) -> void:
	DeskCap.draw(b)
	DeskKit.hero_question(b, QUESTION)

static func handle(b, id: String) -> void:
	DeskCap.handle(b, id)
