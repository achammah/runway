class_name DeskGrowth
extends RefCounted
## DESK — REVENUE · "growth". W2 lane: L-REV.
## THE QUESTION THIS DESK ANSWERS: "where does next week's demand come from?"
## No shipped ancestor: the stub renders the question as its hero and an
## honest pen note until the lane lands (docs/design/DECISIONS.md).

const QUESTION := "where does next week's demand come from?"

static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "the garden", "line": "four plots, steppers, yield lines"}

static func draw(b) -> void:
	DeskKit.under_construction(b, "the garden", QUESTION, "the market garden — four plots with generated topics; until it lands, the channel levers still pull from COSTS -> spend")

static func handle(_b, _id: String) -> void:
	pass
