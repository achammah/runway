class_name DeskBankPage
extends RefCounted
## DESK — COSTS · "the bank". W2 lane: L-MONEY.
## THE QUESTION THIS DESK ANSWERS: "what do we owe and can we borrow?"
## The stub EMBEDS the shipped DeskBank desk so nothing regresses while the
## lane reworks this page to its locked pick (docs/design/DECISIONS.md).
## The shared components live in game/src/ui/components.gd (DeskKit).

const QUESTION := "what do we owe and can we borrow?"

## The group overview's card reads this: the page's hero, one number + one
## sentence (DECISIONS: the quartet card IS the page's hero verbatim).
static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "debt $%s" % _fmt(SimBank.debt_total(_s)), "line": "the meeting — four numbered zones"}

static func draw(b) -> void:
	DeskBank.draw(b)
	DeskKit.hero_question(b, QUESTION)

static func handle(b, id: String) -> void:
	DeskBank.handle(b, id)

static func _fmt(n: int) -> String:
	var s := str(absi(n))
	var out := ""
	while s.length() > 3:
		out = "," + s.substr(s.length() - 3) + out
		s = s.substr(0, s.length() - 3)
	return ("-" if n < 0 else "") + s + out
