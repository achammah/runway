class_name DeskOffers
extends RefCounted
## DESK — REVENUE · "offers". W2 lane: L-REV.
## THE QUESTION THIS DESK ANSWERS: "what do we sell and what does each sale earn?"
## The stub EMBEDS the shipped DeskCatalog desk so nothing regresses while the
## lane reworks this page to its locked pick (docs/design/DECISIONS.md).
## The shared components live in game/src/ui/components.gd (DeskKit).

const QUESTION := "what do we sell and what does each sale earn?"

## The group overview's card reads this: the page's hero, one number + one
## sentence (DECISIONS: the quartet card IS the page's hero verbatim).
static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "%d offers" % _s.offers.size(), "line": "the rate card — price, serve, margin, verdict"}

static func draw(b) -> void:
	DeskCatalog.draw(b)
	DeskKit.hero_question(b, QUESTION)

static func handle(b, id: String) -> void:
	DeskCatalog.handle(b, id)
