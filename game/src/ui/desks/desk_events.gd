class_name DeskEvents
extends RefCounted
## DESK — THE LOG · "events". W2 lane: L-COMPANY.
## THE QUESTION THIS DESK ANSWERS: "what has the world sent us?"
## No shipped ancestor: the stub renders the question as its hero and an
## honest pen note until the lane lands (docs/design/DECISIONS.md).

const QUESTION := "what has the world sent us?"

static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "the mail", "line": "letters and notices, newest first"}

static func draw(b) -> void:
	DeskKit.under_construction(b, "the mail", QUESTION, "the inbox stream — investor knocks, employee asks, covenant warnings; unread bold, each with its desk jump")

static func handle(_b, _id: String) -> void:
	pass
