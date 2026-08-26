class_name DeskSpend
extends RefCounted
## DESK — COSTS · "spend". W2 lane: L-MONEY.
## THE QUESTION THIS DESK ANSWERS: "where does the money go?"
## The stub EMBEDS the shipped DeskLedger desk so nothing regresses while the
## lane reworks this page to its locked pick (docs/design/DECISIONS.md).
## The shared components live in game/src/ui/components.gd (DeskKit).

const QUESTION := "where does the money go?"

## The group overview's card reads this: the page's hero, one number + one
## sentence (DECISIONS: the quartet card IS the page's hero verbatim).
static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "$%s/wk" % _fmt(int((_s.get_meta("pnl", {}) as Dictionary).get("burn", 0))), "line": "the org ledger — every line sums into a bucket"}

static func draw(b) -> void:
	DeskLedger.draw(b)
	DeskKit.hero_question(b, QUESTION)

static func handle(_b, _id: String) -> void:
	pass   # the shipped ledger routes through closures, not the press router

static func _fmt(n: int) -> String:
	var s := str(absi(n))
	var out := ""
	while s.length() > 3:
		out = "," + s.substr(s.length() - 3) + out
		s = s.substr(0, s.length() - 3)
	return ("-" if n < 0 else "") + s + out
