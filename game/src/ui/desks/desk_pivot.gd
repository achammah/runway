class_name DeskPivot
extends RefCounted
## DESK — THE COMPANY · "pivot". W2 lane: L-COMPANY.
## THE QUESTION THIS DESK ANSWERS: "what survives if we change course?"
## No shipped ancestor: the stub renders the question as its hero and an
## honest pen note until the lane lands (docs/design/DECISIONS.md).

const QUESTION := "what survives if we change course?"

static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "two doors", "line": "audience pivot · product pivot"}

static func draw(b) -> void:
	DeskKit.under_construction(b, "two doors", QUESTION, "the escape hatch — each door lists its exact costs, the preview computes what dies, and the arm wants the word PIVOT typed")

static func handle(_b, _id: String) -> void:
	pass
