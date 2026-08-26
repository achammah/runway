class_name DeskOverview
extends RefCounted
## THE DASHBOARD QUARTET (DECISIONS #5, mockup 03): pressing an OPEN divider's
## header opens the group overview — a grid of cards, one per page, each card
## the page's hero (one number + one sentence + red state), and the card IS
## the button to the page. COSTS' quartet grew into a sextet; same card
## grammar, the grid wraps (the collapse law's overview face).

const CARD_W := 548.0
const CARD_H := 208.0
const GAP := 24.0

static func draw(b, gi: int) -> void:
	var g: Dictionary = Binder.GROUPS[gi]
	var sev: Dictionary = b.desk_severities()
	b.label(String(g.get("name", "")).to_upper() + " — the group at a glance",
		Vector2(DeskKit.X_ID, 6.0), DeskKit.TITLE)
	b.label("a card is its page's hero — press it to open the page",
		Vector2(DeskKit.X_ID, 52.0), DeskKit.LAW, Color(DeskKit.INK, 0.5), 800.0)
	var desks: Array = g.get("desks", [])
	var y := 96.0
	for i in desks.size():
		var id := String(desks[i])
		var cx := DeskKit.X_ID + float(i % 2) * (CARD_W + GAP)
		var cy := y + float(i / 2) * (CARD_H + GAP)
		_card(b, cx, cy, id, summary_for(b, id), int(sev.get(id, 0)))

## One quartet card: hero number, hero sentence, the red state, the chevron.
static func _card(b, x: float, y: float, id: String, s: Dictionary, severity: int) -> void:
	var f := DeskKit.card_frame(b, x, y, CARD_W, CARD_H, id)
	if severity > 0:
		DeskKit.sev_dot(b, x + CARD_W - 78.0, y + 16.0, severity)
	b.label(String(s.get("big", "—")), Vector2(f.content_x, f.content_y + 6.0),
		DeskKit.HERO_BIG, DeskKit.ALERT if severity >= 2 else DeskKit.INK,
		CARD_W - DeskKit.CARD_PAD * 2.0)
	b.label(String(s.get("line", "")), Vector2(f.content_x, f.content_y + 78.0),
		DeskKit.DETAIL, Color(DeskKit.INK, 0.65), CARD_W - DeskKit.CARD_PAD * 2.0)
	if severity > 0:
		b.label("needs you — the red climbed here from the page",
			Vector2(f.content_x, y + CARD_H - 44.0), DeskKit.LAW, DeskKit.ALERT,
			CARD_W - DeskKit.CARD_PAD * 2.0)
	var did := id
	var hit := DeskKit.word(b, "", Vector2(x, y), func() -> void:
		b.open_page(did), DeskKit.DETAIL, DeskKit.INK, CARD_W)
	hit.size = Vector2(CARD_W, CARD_H)

## The hero each stub declares, routed by desk id — the overview's one feed.
static func summary_for(b, id: String) -> Dictionary:
	var s: GameState = b.state
	match id:
		"offers": return DeskOffers.hero_summary(s)
		"customers": return DeskCustomersPage.hero_summary(s)
		"in motion": return DeskInMotion.hero_summary(s)
		"growth": return DeskGrowth.hero_summary(s)
		"spend": return DeskSpend.hero_summary(s)
		"team": return DeskTeam.hero_summary(s)
		"recruitment": return DeskRecruit.hero_summary(s)
		"bills": return DeskBills.hero_summary(s)
		"the bank": return DeskBankPage.hero_summary(s)
		"the works": return DeskWorks.hero_summary(s)
		"what we make": return DeskMake.hero_summary(s)
		"cap table": return DeskCapPage.hero_summary(s)
		"the raise": return DeskRaise.hero_summary(s)
		"the street": return DeskStreetPage.hero_summary(s)
		"threats": return DeskThreatsPage.hero_summary(s)
		"pivot": return DeskPivot.hero_summary(s)
		"this week": return DeskThisWeek.hero_summary(s)
		"history": return DeskHistory.hero_summary(s)
		"events": return DeskEvents.hero_summary(s)
		"the offer": return DeskOffer.hero_summary(s)
	return {"big": "—", "line": ""}

static func handle(_b, _id: String) -> void:
	pass
