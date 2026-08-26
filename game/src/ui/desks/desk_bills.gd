class_name DeskBills
extends RefCounted
## DESK — COSTS · "bills". W2 lane: L-MONEY.
## THE QUESTION THIS DESK ANSWERS: "what must be paid every Monday?"
## No shipped ancestor: the stub renders the question as its hero and an
## honest pen note until the lane lands (docs/design/DECISIONS.md).

const QUESTION := "what must be paid every Monday?"

static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "the floor", "line": "the flat vs the scaling"}

static func draw(b) -> void:
	DeskKit.under_construction(b, "the floor", QUESTION, "the bills ledger splits out of the old ledger here — until it lands, the whole book reads under spend")

static func handle(_b, _id: String) -> void:
	pass
