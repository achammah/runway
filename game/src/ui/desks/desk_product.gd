class_name DeskProduct
extends RefCounted
## DESK — the binder's `product` tab: THE ROADMAP BOARD. Spec: docs/design/07-roadmap.md §8
##
## `binder.gd` dispatches the tab body here and passes ITSELF, so this file draws
## through the binder's own helpers and never reaches into the sheet directly.
##
## WHAT THE PAGE IS: index cards pinned in a column. Each card is a thing the
## team could build, with its price in R&D-WEEKS and its odds printed like the
## DM saying them across the table. One press points the team at it; the money
## the ledger already spends on rnd turns into progress instead of polish.
##
## THE FOUR LESSONS, named where their number first appears (§13):
##   CAPACITY            the header prints what a week of this org is worth
##   OPPORTUNITY COST    the footer, and the fact that committed weeks ship no
##                       base quality
##   TECH-DEBT INTEREST  the jar line prints the velocity it is costing
##   LAUNCH RISK         every card's odds line, and the ship receipt
##
## SHIP IS A BUTTON (docs/design/DECISIONS.md #2): the dice roll AT the press,
## behind the pre-roll review — the same card the journal shows before the
## weekly LOCK IN, built from the same engine list, mirrored here because a roll
## is a roll wherever it happens.
##
## THE BENCH (09) owns y470-740 on Hardware runs (docs/design/00-spine.md §11):
## the cards cap at two there, the standing row joins the stack and the footer
## yields.

const CARD_Y := 140.0        ## the first card
const CARD_PITCH := 118.0    ## board-card density (10-interface-language §2.4)
const CARD_CAP := 3          ## the era ladder never opens a fourth
const HW_CARD_CAP := 2       ## Hardware: THE BENCH takes the bottom band
const BAR_X := 720.0
const BAR_W := 270.0
const ACT_X := 1000.0        ## the house control column (DeskKit.X_MINUS)

## Draw the roadmap board: capacity, bet cards, progress, READY. `b` is the
## Binder itself (untyped to keep the two files free of a cyclic class
## dependency).
static func draw(b) -> void:
	var state: GameState = b.state
	# the board is paper: it exists from the first open, not from the first tick
	SimRoadmap.ensure_board(state)
	match String(b.desk.get("mode", "")):
		"preroll":
			_preroll_card(b)
			return
		"shipped":
			_ship_card(b)
			return
	_head(b, state)
	var hardware := String(state.biz_what) == "Hardware"
	var cards := _visible_cards(state, hardware)
	var y := CARD_Y
	for bet in cards:
		y = _bet_card(b, bet, y, not hardware)
	var hidden := SimRoadmap.unshipped(state).size() - cards.size()
	if hidden > 0:
		DeskKit.more(b, Vector2(DeskKit.X_ID, y), hidden, "wait for a free slot")
	if hardware:
		# THE BENCH rides the bottom band on Hardware runs (see draw_bench).
		draw_bench(b)
		return
	_footer(b, state)

## The head: what the product IS, and what the debt is charging for it.
static func _head(b, state: GameState) -> void:
	b.icon("product", Vector2(10, 6))
	b.label("v0.%d" % state.product, Vector2(100, 10), DeskKit.HERO)
	b.debt_jar(state.tech_debt / 100.0, Vector2(300, 10), Vector2(64, 84))
	# ONE LINE, THREE COSTS: debt bills an outage roll, a build penalty, and —
	# new this wave — interest on every hour the team works (§6).
	var outage := int(maxf((state.tech_debt - 40.0) / 250.0, 0.0) * 100.0)
	var interest := int(round((1.0 - SimRoadmap.debt_drag(state)) * 100.0))
	b.label("debt %d · outage ≈ %d%%/wk · TECH-DEBT INTEREST: −%d%% velocity"
		% [int(state.tech_debt), outage, interest], Vector2(390, 16), 25,
		Binder.PEN if state.tech_debt >= 40.0 else Color(Binder.INK, 0.75), 760.0)
	# CAPACITY, by name and in the industry's own unit: one team, so many
	# person-weeks a week, whatever the org happens to be made of.
	b.label("the roadmap — one team, %.1f R&D-wks/wk of capacity"
		% SimRoadmap.capacity_pool(state), Vector2(10, 100), 32)

## Which cards this run can see. Non-Hardware: every candidate, then the
## standing row. Hardware: two cards only — work in flight first, then the
## standing maintenance choice, because THE BENCH owns the rest of the sheet.
static func _visible_cards(state: GameState, hardware: bool) -> Array:
	var board := SimRoadmap.board_bets(state)
	var hardening := SimRoadmap.hardening_bet(state)
	if not hardware:
		var out: Array = board.slice(0, CARD_CAP)
		if not hardening.is_empty():
			out.append(hardening)
		return out
	var live: Array = []
	var rest: Array = []
	for bet in board:
		var bd: Dictionary = bet
		if bool(bd.get("committed", false)) or bool(bd.get("ready", false)):
			live.append(bd)
		else:
			rest.append(bd)
	if not hardening.is_empty():
		live.append(hardening)
	for r in rest:
		live.append(r)
	return live.slice(0, HW_CARD_CAP)

## ONE CARD (10-interface-language §2.4, board-card density): what it is, whose
## voice it is in, what it costs and what the dice think of it — then the state
## block on the right, which is the only thing that changes between the three
## states a bet can be in.
static func _bet_card(b, bet: Dictionary, y: float, separator: bool) -> float:
	var state: GameState = b.state
	var standing := separator and String(bet.get("id", "")) == SimRoadmap.HARDENING_ID
	if standing:
		b.label("── standing ──", Vector2(DeskKit.X_ID, y), 20, Color(Binder.INK, 0.4))
		y += 28.0
	var cost := float(bet.get("cost_rnd_weeks", 0.0))
	var dense := "%s R&D-wks · LAUNCH RISK: clean ship ~%d%% (DC %d vs build)" % [
		_wks(cost), SimRoadmap.ship_odds_pct(state, bet), SimRoadmap.bet_dc(bet)]
	var cfg := {
		"name": "%s · %s, ambition %d" % [String(bet.get("name", "")).to_upper(),
			String(bet.get("kind", "")), int(bet.get("ambition", 1))],
		"flavor": String(bet.get("desc", "")),
		"dense": dense,
		"pitch": CARD_PITCH,
		"actions": [],
	}
	if bool(bet.get("ready", false)):
		_ready_block(b, bet, y)
	elif bool(bet.get("committed", false)):
		_progress_block(b, bet, y)
	elif SimRoadmap.committed_bets(state).size() >= SimRoadmap.wip_cap(state):
		# A CAP THAT BITES SAYS SO where the action was (§2.4): the team is
		# busy, and the WIP number is the lesson.
		cfg["actions"] = [{"reason": "at capacity (%d/%d)" % [
			SimRoadmap.committed_bets(state).size(), SimRoadmap.wip_cap(state)]}]
	else:
		_commit_block(b, bet, y)
	return DeskKit.card(b, y, cfg)

## THE ALLOCATION ACT, behind the two-tap arm (10-interface-language §2.9): the
## first press prints what the week costs, the second points the team. The price
## is not money — it is the base quality this week's rnd money will now never
## buy, which is exactly the lesson the desk exists to teach.
static func _commit_block(b, bet: Dictionary, y: float) -> void:
	var state: GameState = b.state
	var id := String(bet.get("id", ""))
	if String(b.desk.get("armed", "")) == "on:" + id:
		b.label("rnd money buys weeks, not polish", Vector2(BAR_X, y + 12.0), 22,
			Binder.PEN, 280.0)
	DeskKit.arm(b, "on:" + id, "point the team →", "sure?", Vector2(ACT_X, y + 4.0),
		func() -> void: SimRoadmap.commit_bet(state, id), 160.0, DeskKit.DETAIL)

## COMMITTED: the money is visibly going somewhere, with an honest ETA — and a
## way back out that quotes its own price first (standing down costs a quarter
## of the build, docs/design/DECISIONS.md).
static func _progress_block(b, bet: Dictionary, y: float) -> void:
	var state: GameState = b.state
	var cost := maxf(float(bet.get("cost_rnd_weeks", 1.0)), 0.001)
	var bar := _BetBar.new()
	bar.fill = clampf(float(bet.get("progress", 0.0)) / cost, 0.0, 1.0)
	bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	bar.position = Vector2(BAR_X, y + 10.0)
	bar.set_deferred("size", Vector2(BAR_W, 34.0))
	b.pane().add_child(bar)
	var id := String(bet.get("id", ""))
	var armed := String(b.desk.get("armed", "")) == "down:" + id
	if armed:
		b.label("25% of the build is lost", Vector2(BAR_X, y + 48.0), 22,
			Binder.PEN, 280.0)
	else:
		var eta := SimRoadmap.eta_weeks(state, bet)
		b.label("%d%% · %s" % [SimRoadmap.progress_pct(bet),
			("ships in ~%d wks" % eta) if eta > 0 else "no capacity — this never finishes"],
			Vector2(BAR_X, y + 48.0), 22, Color(Binder.INK, 0.7), 280.0)
	DeskKit.arm(b, "down:" + id, "stand down", "sure?", Vector2(ACT_X, y + 4.0),
		func() -> void: SimRoadmap.uncommit_bet(state, id), 160.0, DeskKit.DETAIL)

## READY: the held breath. The dice have not rolled — the founder can still pay
## debt down, stack advantage, and only then press.
static func _ready_block(b, bet: Dictionary, y: float) -> void:
	var state: GameState = b.state
	b.label("READY — the dice are yours", Vector2(BAR_X, y + 6.0), 27, Binder.PEN, 280.0)
	var left := SimRoadmap.stall_left(state, bet)
	b.label(("it slips out on its own in %d wks" % left) if left > 0
		else "it slips out on its own this week", Vector2(BAR_X, y + 44.0), 21,
		Color(Binder.INK, 0.55), 280.0)
	var id := String(bet.get("id", ""))
	var btn: Button = DeskKit.word(b, "SHIP IT →", Vector2(ACT_X, y + 4.0), Callable(),
		DeskKit.STATUS, Binder.INK, 160.0)
	# THE PRESS OWNS ITS WHOLE BEAT — the review first if anything is open, then
	# the stroke, then the dice. Letting `word` rebuild the pane would free the
	# very button the stroke draws under.
	btn.pressed.connect(func() -> void:
		b.desk.erase("armed")
		if not _preroll_rows(state).is_empty():
			b.desk["mode"] = "preroll"
			b.desk["bet"] = id
			b.refresh()
			return
		DeskKit.sign_stroke(b, btn, func() -> void: _fire(b, id)))

## THE PRE-ROLL REVIEW (docs/design/DECISIONS.md #2), on this desk. The engine
## decides what counts as outstanding; this page only reads it — minus the row
## that IS this press, because "a bet is built" is not a reason to stop a
## founder from shipping it.
static func _preroll_rows(state: GameState) -> Array:
	var out: Array = []
	for it in SimEngine.preroll_items(state):
		if String((it as Dictionary).get("key", "")) == "bet_ready":
			continue
		out.append(it)
	return out

static func _preroll_card(b) -> void:
	var state: GameState = b.state
	var id := String(b.desk.get("bet", ""))
	var bet := SimRoadmap.bet_by_id(state, id)
	if bet.is_empty():
		# the bet went out from under the card (a slip, a load): no dead ends —
		# fall back to the board rather than draw a review of nothing
		b.desk.clear()
		draw(b)
		return
	var rows := _preroll_rows(state)
	var read: Array = []
	var shown := 0
	for it in rows:
		if shown >= 5:
			read.append("…and %d more, on the threats page." % (rows.size() - shown))
			break
		var itd: Dictionary = it
		read.append("%s%s — %s" % ["! " if int(itd.get("severity", 2)) >= 3 else "",
			String(itd.get("desk", "")), String(itd.get("label", ""))])
		shown += 1
	var to_desk := String((rows[0] as Dictionary).get("desk", "")) if not rows.is_empty() else ""
	DeskKit.review(b, {
		"banner": "before the dice: '%s' ships on a d20 vs DC %d" % [
			String(bet.get("name", "")), SimRoadmap.bet_dc(bet)],
		"read": read,
		"verdict": "fix them, or roll and live with it.",
		"note": "clean ship ~%d%% at build %d — debt, focus and flow all move that number"
			% [SimRoadmap.ship_odds_pct(state, bet), int(state.competences.get("build", 3))],
		"confirm": "roll anyway",
		"cancel": "go fix it",
		"on_confirm": func() -> void: _fire(b, id),
		"on_cancel": func() -> void:
			b.desk.clear()
			if to_desk != "":
				b.focus_desk(to_desk),
	})

## The dice, at the press. The engine rolls, the state changes, and the card
## that comes back is the receipt.
static func _fire(b, id: String) -> void:
	var state: GameState = b.state
	var res := SimRoadmap.ship_ready(state, id)
	b.desk.clear()
	if not res.is_empty():
		b.desk["mode"] = "shipped"
		b.desk["ship"] = res
	b.refresh()

## THE RECEIPT: the die, the DC, the band, and every delta with its cause. The
## same strings the journal prints when a launch slips out on its own.
static func _ship_card(b) -> void:
	var res: Dictionary = b.desk.get("ship", {})
	if res.is_empty():
		b.desk.clear()
		draw(b)
		return
	DeskKit.back(b, "◂ back to the board", func() -> void:
		b.desk.clear())
	var y := 90.0
	b.label(String(res.get("event", "")), Vector2(DeskKit.X_ID, y), DeskKit.TITLE,
		Binder.SAGE if String(res.get("band", "")) in ["brilliant", "fine"] else Binder.PEN, 1100.0)
	y += 64.0
	y = DeskKit.rule(b, y)
	var receipts: Array = res.get("lines", [])
	for l in receipts:
		var t := String(l)
		b.label(t, Vector2(DeskKit.X_ID, y), DeskKit.STATUS, Color(Binder.INK, 0.85), 1100.0)
		y += maxf(b.wrap_h(t, DeskKit.STATUS, 1100.0), 32.0) + 6.0
	DeskKit.footer(b, {
		"computed": "the dice were %d%+d against DC %d — margin %d" % [
			int(res.get("d20", 0)), int(res.get("mod", 0)), int(res.get("dc", 0)),
			int(res.get("total", 0)) - int(res.get("dc", 0))],
		"rules": "LAUNCH RISK: scope widens the spread. Ambition 3 pays double and misses twice as often — preparation is the only thing that moves the odds.",
	})

## THE DESK STATES ITS OWN LAWS — computed from this run's numbers in blue,
## the standing rules in ink, and a warning that outranks both when one fires.
static func _footer(b, state: GameState) -> void:
	var pool := SimRoadmap.capacity_pool(state)
	var n := SimRoadmap.committed_bets(state).size()
	var interest := int(round((1.0 - SimRoadmap.debt_drag(state)) * 100.0))
	var computed := "this week buys %.2f R&D-wks" % pool
	if interest > 0:
		computed += " · debt is eating %d%% of it" % interest
	if n > 1:
		computed += " · %d bets split it %.2f each" % [n, pool / float(n)]
	elif n == 1:
		computed += " · all of it on one bet"
	else:
		computed += " · nothing committed, so it polishes base quality"
	var warning := ""
	if SimRoadmap.any_bet_ready(state):
		warning = "a bet is built and waiting — ship it, or it slips out on its own"
	DeskKit.footer(b, {
		"computed": computed,
		"rules": "OPPORTUNITY COST: rnd money buys R&D-weeks while a bet is committed, +1 quality per $1,200 when it is not · parallel bets share one team",
		"warning": warning,
	})

## R&D-weeks read like the estimates they are: 2.5, 3, 10.
static func _wks(v: float) -> String:
	return ("%.1f" % v) if absf(v - round(v)) > 0.05 else str(int(round(v)))

## A press inside this desk. Every control on this page carries its own closure
## (the kit's own idiom), so the id router stays empty here by design — it is
## kept because `binder.desk_press` names this desk in its match.
static func handle(_b, _id: String) -> void:
	pass

## THE BENCH belongs to the hardware lane and is drawn inside this desk on
## Hardware runs only. The call site is planted; the band is ruled in
## docs/design/00-spine.md §11 (y470-740) — on Hardware runs 07 caps its bet
## cards at 2 and yields the footer line to make room.
static func draw_bench(b) -> void:
	DeskFactory.draw_bench(b)

## THE VESSEL for a build in flight (10-interface-language §2.10): a sage level
## under an ink edge — the `_DebtJar` construction, laid on its side. No
## animation: progress is a fact, not a performance.
class _BetBar:
	extends Control
	var fill := 0.0
	func _draw() -> void:
		var w := size.x
		var h := size.y
		draw_rect(Rect2(0, 0, w, h), Color(Binder.INK, 0.04))
		draw_rect(Rect2(2, 2, (w - 4.0) * clampf(fill, 0.0, 1.0), h - 4.0),
			Color(Binder.SAGE, 0.6))
		draw_rect(Rect2(0, 0, w, h), Binder.INK, false, 4.0)
