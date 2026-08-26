class_name DeskOffer
extends RefCounted
## THE OFFER — the first MOMENTARY desk (DECISIONS §4): when a buyout offer
## lands, a gold tab slides into THE COMPANY and stays until the offer is
## answered or expires, then folds into HISTORY. Four zones, the numbered
## didactic spine. W2 lane: L-OWN (waterfall, powers, fishy structures).
##
## THIS IS THE SHELL: the zones render with placeholder numbers; ACCEPT /
## NEGOTIATE / DECLINE resolve the tab (fold-away is real), book nothing.

const QUESTION := "should we take their money?"

static func hero_summary(state) -> Dictionary:
	var _s: GameState = state
	return {"big": "an offer", "line": "cash vs stock vs earnout — read the small lines"}

static func draw(b) -> void:
	var y := DeskKit.hero_band(b, "they want to buy the company",
		QUESTION + " — the clock on the tab is real.", DeskKit.INK, 6.0, false)
	var z1 := DeskKit.zone(b, DeskKit.X_ID, y, 548.0, 210.0, 1, "what's on the table",
		"the headline price decomposed — cash today vs their paper vs maybe-money")
	DeskKit.money_row(b, z1, "cash today", "$—")
	DeskKit.money_row(b, z1, "acquirer stock (lockup)", "$—")
	DeskKit.money_row(b, z1, "earnout (their targets)", "$—")
	var z2 := DeskKit.zone(b, DeskKit.X_ID + 572.0, y, 548.0, 210.0, 2, "who gets what",
		"the waterfall applied to THIS number — the bank first, preferences next")
	DeskKit.money_row(b, z2, "the bank", "$—")
	DeskKit.money_row(b, z2, "preferences", "$—")
	DeskKit.money_row(b, z2, "your take", "$—", DeskKit.SAGE)
	y += 234.0
	var z3 := DeskKit.zone(b, DeskKit.X_ID, y, 548.0, 190.0, 3, "the fine print, read aloud",
		"some offers are fishy on purpose — each flag named in red")
	b.label("· the shell holds the reading lamp — flags land with the ownership lane",
		Vector2(z3.content_x, z3.cursor), DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 500.0)
	var z4 := DeskKit.zone(b, DeskKit.X_ID + 572.0, y, 548.0, 190.0, 4, "who can say no",
		"what was SIGNED at the raise decided your exit freedom years early")
	b.label("· protective provisions · drag-along · the board — resolved from the instruments",
		Vector2(z4.content_x, z4.cursor), DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 500.0)
	y += 214.0
	DeskKit.arm(b, "offer_accept", "ACCEPT", "press again — the company sells",
		Vector2(DeskKit.X_ID, y), func() -> void: _resolve(b), 260.0)
	DeskKit.word(b, "NEGOTIATE (one counter)", Vector2(DeskKit.X_ID + 300.0, y),
		func() -> void: _resolve(b), DeskKit.STATUS, DeskKit.INK, 320.0)
	DeskKit.word(b, "DECLINE", Vector2(DeskKit.X_ID + 660.0, y), func() -> void:
		_resolve(b), DeskKit.STATUS, Color(DeskKit.INK, 0.7), 200.0)
	DeskKit.footer(b, {
		"computed": "resolving folds this gold tab into HISTORY",
		"rules": "momentary desks: summoned by their event, gone when answered",
	})

static func _resolve(b) -> void:
	b.resolve_momentary("the offer")

static func handle(b, id: String) -> void:
	if id == "resolve":
		_resolve(b)
