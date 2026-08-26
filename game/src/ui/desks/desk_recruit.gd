class_name DeskRecruit
extends RefCounted
## DESK — COSTS · "recruitment". W2 lane: L-OWN.
## THE QUESTION THIS DESK ANSWERS: "who are we hiring, and will they say yes?"
## No shipped ancestor: the stub renders the question as its hero and an
## honest pen note until the lane lands (docs/design/DECISIONS.md).

const QUESTION := "who are we hiring, and will they say yes?"

static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "hiring", "line": "roles, candidates, offers out"}

static func draw(b) -> void:
	DeskKit.under_construction(b, "hiring", QUESTION, "open roles, the candidates pipeline and the offer composer — salary + options from the pool; acceptance moves with the mix")

static func handle(_b, _id: String) -> void:
	pass
