class_name DeskHistory
extends RefCounted
## DESK — THE LOG · "history". W2 lane: L-COMPANY.
## THE QUESTION THIS DESK ANSWERS: "how did we get here?"
## No shipped ancestor: the stub renders the question as its hero and an
## honest pen note until the lane lands (docs/design/DECISIONS.md).

const QUESTION := "how did we get here?"

static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "the ledger of weeks", "line": "a row per week, receipts behind each"}

static func draw(b) -> void:
	DeskKit.under_construction(b, "the ledger of weeks", QUESTION, "the run's own ledger — wk, cash, net, customers, the headline; folded momentary tabs file here as flagged rows")

static func handle(_b, _id: String) -> void:
	pass
