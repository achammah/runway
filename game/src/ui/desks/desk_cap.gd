class_name DeskCap
extends RefCounted
## DESK — the binder's `cap table` tab. Spec: docs/design/08-board-mna.md §8/§10
##
## `binder.gd` dispatches the tab body here and passes ITSELF, so this file draws
## through the binder's own helpers and never reaches into the sheet directly.
##
## THE PAGE IS A SCORECARD THE FOUNDER KEEPS ON THEMSELF. The wheel says who owns
## what — and the fourth slice makes the option pool a drawn wound, carved out of
## your side by the pool shuffle. Under it, the room you answer to: the covenant
## with a countdown, the strike track as pen marks, and one authored line naming
## the governance you have actually grown into. Above all of it, when a clock is
## running, the offer banner.
##
## No board, no rows: a company that never took a check has nothing under the
## wheel, and that clean page IS the bootstrap flex.
##
## NOTHING ON THIS PAGE SIGNS ANYTHING. Selling the company and ringing the bell
## are journal acts (two-tap, docs/design/10-interface-language.md §2.9) — the
## desk shows the clock and says where the pen is. That split is deliberate: the
## binder is where you read, the book is where you commit.
##
## The bar every surface ships at (docs/design/00-spine.md §11): readable first
## pass by a tired player; concepts named in real business terms with a teaching
## line where a number first appears; no dead ends and every state leavable;
## drawn in the game's hand, never a SaaS panel. The shared components live in
## game/src/ui/components.gd (DeskKit) — use them, never fork them.

## THE HAND HAS NO SYMBOL FONT. `✗` (U+2717) and `⚡` (U+26A1) are both absent
## from PatrickHand — a typed one arrives as a tofu box — so a strike is `×`
## (U+00D7, present) and the banner leads with the game's own alarm mark, the
## same `!` the tab bangs and the term-sheet banner already wear. The same law
## rules every other desk: the hand carries no arrows, triangles, clocks or box
## rules either, and the whole binder writes `->` the way a pen does.
const MARK_STRIKE := "×"
const MARK_EMPTY := "·"

## The governance vocabulary, by era: what a board IS at this size. This is the
## scale-progressive lesson in one line — the page teaches that governance
## hardens as the company grows, not that it arrives all at once.
const STAGE_LINE := [
	"no board — an angel and a handshake",
	"1 investor seat — expectations, lightly held",
	"a real board: covenants + the pool shuffle",
	"%s — politics, leaks, secondaries",
	"exit-grade governance — clean quarters open windows",
]

## THE ROWS UNDER THE WHEEL GET THE WHOLE PAGE. The spec drew them at w=470 to
## stay clear of the rounds column at x=540 — but that column is bounded: its
## last row sits at 60 + 44×rounds + 216, and the ladder caps at six rounds, so
## it can never reach below y=540. Everything from y=568 down is free paper, and
## a covenant sentence wrapped to three lines inside 470px collided with the
## stage line every time. One line each, at full width.
const ROW_W := 1100.0

## Draw the option-pool slice, covenant and strikes, the offer/window banner. `b`
## is the Binder itself (untyped to keep the two files free of a cyclic class
## dependency).
static func draw(b) -> void:
	var state: GameState = b.state
	var founder := state.founder_pct
	var cof := 0.0
	for cf in state.cofounders:
		cof += float((cf as Dictionary).get("equity_diluted", (cf as Dictionary).get("equity", 0)))
	# FOUR SLICES. The pool sits between the cofounders and the investors because
	# that is where it came from in the shuffle: written pre-money, out of the
	# founding side, before the investor's slice diluted everyone including it.
	var pool := state.option_pool_pct
	var inv := maxf(100.0 - founder - cof - pool, 0.0)
	b.pie([
		{"pct": founder, "col": Binder.PEN, "label": "you %.0f%%" % founder},
		{"pct": cof, "col": Binder.BLUE, "label": "cofounders %.0f%%" % cof},
		# THE WHEEL'S OWN TAG IS SHORT ON PURPOSE. This slice sits at nine
		# o'clock, and there is only ~90px of paper left of the wheel there: the
		# long form ran either off the sheet or back over its own wedge. The full
		# term is named where it is taught, on the raise line below.
		{"pct": pool, "col": Binder.YELL, "label": "pool %.0f%%" % pool},
		{"pct": inv, "col": Binder.SAGE, "label": "investors %.0f%%" % inv},
	], Vector2(40, 30), 430.0)
	var y := 60.0
	b.label("rounds:", Vector2(540, 30), 32)
	if state.rounds_raised.is_empty():
		# 620px, not 560: at 560 this exact sentence wrapped by two pixels and its
		# second line landed on the valuation.
		b.label("none yet. every point of the company is still on this table.",
			Vector2(540, y + 20), 27, Color(Binder.INK, 0.7), 620.0)
	# FOUR ROUNDS, then the count. The ladder allows six, and rounds five and six
	# pushed the raise line down into the offer banner.
	var shown_rounds := 0
	for r in state.rounds_raised:
		if shown_rounds >= 4:
			b.label("+%d more closed" % (state.rounds_raised.size() - shown_rounds),
				Vector2(540, y + 20), 24, Color(Binder.INK, 0.6), 560.0)
			y += 44.0
			break
		# THE ROUND'S NAME AS A HUMAN SAYS IT: the engine's id carries an
		# underscore, and a raw id is never what a page prints (§3.8).
		b.label("· %s — closed" % String(r).replace("_", " "), Vector2(540, y + 20), 28,
			Binder.INK, 560.0)
		y += 44.0
		shown_rounds += 1
	var val := SimEngine.valuation(state)
	b.label("valuation $%s" % b.fmt(val), Vector2(540, y + 80), 30)
	b.label("your slice today: $%s" % b.fmt(int(float(val) * state.founder_pct / 100.0)),
		Vector2(540, y + 128), 30, Binder.PEN)
	if state.founder_banked > 0:
		# Banked money is not on the wheel and never will be — it already left.
		b.label("banked already: $%s (yours whatever happens)" % b.fmt(state.founder_banked),
			Vector2(540, y + 168), 24, Color(Binder.INK, 0.7), 620.0)
	# WHAT THE NEXT ROUND WOULD COST, with the terms named. Pre-money is what the
	# company is worth before the check; the check makes the post; their slice is
	# check ÷ post. Naming it here is the one place the math is free to teach.
	if val > 0:
		var ask := int(float(val) * 0.10)
		var fair_pct := float(ask) / float(val + ask) * 100.0
		var warm := SimEngine.warmth_pct(state)
		# THE CREDIT CYCLE PRICES THIS LINE (03 §7.3). A winter widens the
		# spread and shrinks the pre-money at the same time; a boom does both
		# the other way. Both numbers are already the engine's — the caption
		# only says which weather moved them, because raise TIMING against the
		# cycle is the decision a founder makes on this row.
		var asked := fair_pct * 1.3 * (1.0 - warm / 100.0) * SimEngine.shock_spread_mult(state)
		var pool_ask := SimBoard.pool_ask_pct(state)
		# THE ARROWS ARE WRITTEN, NOT TYPED: `->` is in the hand, U+2192 is not, and
		# a borrowed arrow is a second typeface inside a one-hand binder.
		b.label("raise ~$%s now: pre-money $%s%s -> post $%s — they'd ask ≈ %.0f%%%s · your %.0f%% -> ≈ %.0f%%%s" % [
			b.fmt(ask), b.fmt(val), _shock_note(state), b.fmt(val + ask), asked,
			(" (%.0f%% off — they know you)" % warm) if warm > 0.0 else "",
			state.founder_pct, state.founder_pct * (1.0 - asked / 100.0),
			(" · plus a ~%d%% OPTION POOL written pre-money" % int(pool_ask)) if pool_ask > 0.0 else ""],
			Vector2(540, y + 216), 24, Color(Binder.INK, 0.7), 620.0)
	if state.has_flag("fundraising_open"):
		b.label("! TERM SHEETS ARE ON THE TABLE — sign in the journal before they expire",
			Vector2(40, 480), 27, Binder.PEN, 1100.0)
	# THE BANNER IS A CURSOR, NOT A SLOT. It used to be drawn at a fixed 520 and
	# the board block at a fixed 568, so the week a buyer's name or a nine-figure
	# price pushed the banner onto a second line, that line was written straight
	# through "the board:". The banner hands back where it ended; 568 stays the
	# floor, so a one-line banner leaves the whole page exactly where it was.
	var y_board := _board_block(b, state, maxf(_banner(b, state), 568.0))
	# THE RENEWAL CALENDAR (05, DECISIONS.md): the board reads the book of
	# business too. The pipeline lane writes one line here when it has one;
	# nothing is drawn while the slot is empty.
	var renewal := String(state.get_meta("cap_renewal_line", ""))
	if renewal != "" and y_board + 30.0 <= 760.0:
		b.label(renewal, Vector2(40, y_board), 24, Color(Binder.INK, 0.7), ROW_W)

## Which weather is on the term sheet, or "" in ordinary money. The multiple is
## read live off the status catalog, so a rebalance can never make this line lie.
static func _shock_note(state: GameState) -> String:
	if SimEngine.has_status(state, "funding_winter"):
		return " (winter: valuations %.1f×)" % SimEngine.shock_val_mult(state)
	if SimEngine.has_status(state, "boom"):
		return " (boom: %.1f×)" % SimEngine.shock_val_mult(state)
	return ""

## THE CLOCK, above the board block. An offer or an open window is time-boxed and
## has to be visible without opening the book — the banner says what is on the
## table and where the pen is, and nothing else. Returns the y it ENDED at, so
## the block below can start under it however long the buyer's name turns out.
static func _banner(b, state: GameState) -> float:
	var text := ""
	if not state.mna.is_empty():
		var mo: Dictionary = state.mna
		var price := int(mo.get("price", 0))
		var left := maxi(int(mo.get("expires_week", 0)) - state.week, 0)
		text = "! ON THE TABLE: %s $%s (%.2f×) · your slice $%s · %d wk%s to sign, in the journal" % [
			String(mo.get("buyer", "a buyer")), b.fmt(price), float(mo.get("premium", 1.0)),
			b.fmt(int(float(price) * state.founder_pct / 100.0)), left,
			"" if left == 1 else "s"]
	elif state.has_flag("ipo_window"):
		text = "! THE IPO WINDOW IS OPEN — $%s at the bell · your slice $%s · the bell is in the journal" % [
			b.fmt(SimBoard.ipo_price(state)),
			b.fmt(int(float(SimBoard.ipo_price(state)) * state.founder_pct / 100.0))]
	if text == "":
		return 0.0
	b.label(text, Vector2(40, 520), 27, Binder.PEN, 1100.0)
	return 520.0 + maxf(b.wrap_h(text, 27, 1100.0), 34.0) + 14.0

## THE ROOM YOU ANSWER TO. Absent entirely until a round closes — the empty page
## is the bootstrap flex, and printing "no covenant" would be four words saying
## nothing.
static func _board_block(b, state: GameState, y0: float) -> float:
	if state.board.is_empty():
		return y0
	var bd: Dictionary = state.board
	var stage := SimBoard.board_stage(state)
	b.label("the angel:" if stage == 0 else "the board:", Vector2(40, y0), 32)

	var pnl: Dictionary = state.get_meta("pnl", {})
	var now_rev := int(pnl.get("revenue", 0))
	var target := int(bd.get("target_revenue", 0))
	var due := SimBoard.board_review_in(state)
	# The garage has no covenant to breach — it has a number you said out loud.
	b.label("%s: $%s/wk by wk %d — now $%s/wk · %s" % [
		"the number you said" if stage == 0 else "growth covenant",
		b.fmt(target), int(bd.get("review_week", 0)), b.fmt(now_rev),
		("this week" if due <= 0 else ("1 wk left" if due == 1 else "%d wks left" % due))],
		Vector2(40, y0 + 42.0), 28, Binder.INK, ROW_W)

	# THE MISS LADDER AS A VISIBLE TRACK. Marks, not a number: a founder should
	# be able to see how many rungs are left without doing arithmetic.
	var strikes := int(bd.get("strikes", 0))
	var goodwill := int(bd.get("goodwill", 0))
	var cap := maxi(SimBoard.strike_cap(state), strikes)
	var track := ""
	for i in cap:
		track += MARK_STRIKE if i < strikes else MARK_EMPTY
		if i < cap - 1:
			track += " "
	var room := "professional"
	if strikes >= 2:
		room = "ice"
	elif goodwill >= 2:
		room = "warm"
	if stage == 0:
		b.label("no strikes — an angel has expectations, not covenants · goodwill %d/3" % goodwill,
			Vector2(40, y0 + 86.0), 28, Binder.SAGE, ROW_W)
	else:
		# THE COUNT RIDES THE TRACK. The empty rung is a middle dot and so is the
		# line's own separator, so "strikes × × · · · goodwill" read as one run of
		# marks; the parenthesis says where the track ends and gives the anchor
		# every judgement number owes (§3.4).
		b.label("strikes %s (%d of %d) · goodwill %d/3 · the room is %s" % [
			track, strikes, cap, goodwill, room],
			Vector2(40, y0 + 86.0), 28, Binder.PEN if strikes > 0 else Binder.SAGE, ROW_W)

	var line := String(STAGE_LINE[clampi(stage, 0, STAGE_LINE.size() - 1)])
	if line.contains("%s"):
		var seats := state.board_seats_investor
		line = line % ("1 investor seat" if seats == 1 else "%d investor seats" % seats)
	b.label(line, Vector2(40, y0 + 130.0), 24, Color(Binder.INK, 0.7), ROW_W)
	return y0 + 174.0

## A press inside this desk. Nothing on the cap table commits: the signature
## lives in the journal, where a run-ending act gets its two-tap arm.
static func handle(_b, _id: String) -> void:
	pass
