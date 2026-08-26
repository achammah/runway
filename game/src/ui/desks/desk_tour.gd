class_name DeskTour
extends RefCounted
## THE FIRST-OPEN TOUR (DECISIONS #6): six steps — the four groups fanned with
## one-liners, the red demo, the handover. The binder forces the rail's state
## per step (the group in question fans open; step five paints a demo red);
## this file draws the sheet side: the step's words and the click-to-advance.
## Click advances · Esc skips · once per install (user:// flag) · replayable
## from the how-to screen.

const STEPS := [
	{"title": "REVENUE", "line": "the money coming in: what you sell, who buys it, " +
		"who is on the way, and what makes them come. The sage tab."},
	{"title": "COSTS", "line": "the money going out: your chosen spend, the payroll, " +
		"the bills that arrive anyway, the bank, and the works that deliver it all. " +
		"The coral tab."},
	{"title": "THE COMPANY", "line": "the thing itself: what you make, who owns it, " +
		"who might fund it, the street outside, and the door marked pivot. The blue tab."},
	{"title": "THE LOG", "line": "time: this week (the desk you play from), the run's " +
		"history, and the mail. You land here every week. The yellow tab."},
	{"title": "WHEN A TAB TURNS RED", "line": "red means ACT — a page needs you, and " +
		"its red climbs onto the divider so a closed group can still call for help. " +
		"Coral is just money out; red is a fire."},
	{"title": "IT'S YOUR BINDER NOW", "line": "press a divider to open its group; " +
		"press an open divider's header for the group at a glance; Esc always walks " +
		"you back out. Replay this tour any time from the how-to screen."},
]

static func draw(b, step: int) -> void:
	var s: Dictionary = STEPS[clampi(step, 0, STEPS.size() - 1)]
	# the step's own paper: quiet sheet, big words, one line
	b.label("the binder, in six flips", Vector2(DeskKit.X_ID, 10.0), DeskKit.DETAIL,
		Color(DeskKit.INK, 0.45), 600.0)
	var y := 140.0
	b.label("%d / %d" % [step + 1, STEPS.size()], Vector2(DeskKit.X_ID, y),
		DeskKit.DETAIL, Color(DeskKit.INK, 0.4), 200.0)
	y += 44.0
	b.label(String(s.get("title", "")), Vector2(DeskKit.X_ID, y), DeskKit.HERO,
		DeskKit.ALERT if step == 4 else DeskKit.INK, 1080.0)
	y += 84.0
	var line := String(s.get("line", ""))
	b.label(line, Vector2(DeskKit.X_ID, y), DeskKit.ROW, Color(DeskKit.INK, 0.75), 980.0)
	y += b.wrap_h(line, DeskKit.ROW, 980.0) + 30.0
	if step == 4:
		# the demo red: the rail is wearing one right now — point at it
		b.label("the red page tab on the rail and the red dot on its divider are the demo",
			Vector2(DeskKit.X_ID, y), DeskKit.STATUS, DeskKit.ALERT, 900.0)
		y += 50.0
	b.label("click to continue · Esc skips the tour", Vector2(DeskKit.X_ID, 700.0),
		DeskKit.LAW, Color(DeskKit.INK, 0.5), 800.0)
	# the whole sheet advances the tour — one press, no hunting
	var hit := DeskKit.word(b, "", Vector2(0.0, 0.0), func() -> void:
		b.tour_advance(), DeskKit.DETAIL, DeskKit.INK, DeskKit.PANE_W)
	hit.size = Vector2(DeskKit.PANE_W, 760.0)

static func handle(_b, _id: String) -> void:
	pass
