class_name DeskRaise
extends RefCounted
## DESK — THE COMPANY · "the raise". W2 lane: L-OWN.
## THE QUESTION THIS DESK ANSWERS: "who would fund us next, and at what true price?"
## No shipped ancestor: the stub renders the question as its hero and an
## honest pen note until the lane lands (docs/design/DECISIONS.md).

const QUESTION := "who would fund us next, and at what true price?"

static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "the raise", "line": "radar -> conversations -> terms -> wired"}

static func draw(b) -> void:
	DeskKit.under_construction(b, "the raise", QUESTION, "the fundraising pipeline — every instrument with its true character, term sheets compared for their real price; a raise costs founder time")

static func handle(_b, _id: String) -> void:
	pass
