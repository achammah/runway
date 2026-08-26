class_name DeskWorks
extends RefCounted
## DESK — COSTS · "the works". W2 lane: L-DIVWORKS.
## THE QUESTION THIS DESK ANSWERS: "can we serve what they want, and what
## does one cost?" — every business has works (DECISIONS: the factory
## broadened; four zones, three rungs, business-type-native units).
## The stub EMBEDS the shipped factory desk so the hardware bench keeps
## working, and opens THE ARRANGE MODE shell the divisions lane fills.

const QUESTION := "can we serve what they want, and what does one cost?"

static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "the works", "line": "capacity, the unit ticket, the relief valves"}

static func draw(b) -> void:
	if String(b.desk.get("mode", "")) == "arrange":
		DeskArrange.draw(b)
		return
	DeskFactory.draw(b)
	# THE WRITE VIEW's door (DECISIONS: ARRANGE MODE): the read faces stay
	# pure display; this word flips the desk to the assignment layout.
	DeskKit.word(b, "arrange →", Vector2(DeskKit.X_ID + 980.0, 6.0), func() -> void:
		b.desk["mode"] = "arrange", DeskKit.STATUS, DeskKit.BLUE, 160.0)
	DeskKit.hero_question(b, QUESTION)

static func handle(b, id: String) -> void:
	if String(b.desk.get("mode", "")) == "arrange":
		DeskArrange.handle(b, id)
		return
	DeskFactory.handle(b, id)
