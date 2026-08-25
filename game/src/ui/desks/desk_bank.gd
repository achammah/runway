class_name DeskBank
extends RefCounted
## DESK — the binder's `the bank` tab, the tenth. Spec: docs/design/06-finance.md
## Approved in docs/design/DECISIONS.md #1: the ledger keeps the levers and the
## compact weekly P&L; borrowing, notes, the forecast, the tax block and the
## full grouped statement live HERE, at full width.
##
## `binder.gd` dispatches the tab body here and passes ITSELF, so this file draws
## through the binder's own helpers and never reaches into the sheet directly.
##
## WHAT IS HERE NOW IS A PLACEHOLDER, and the whole body is the lane's to
## replace: the sheet must never be blank paper, so until 06 lands this page
## states what the desk is for and what the company owes today, in the desk's own
## voice. Nothing has been relocated off the ledger yet — moving the loan content
## is 06's job, so that the two halves move in one commit and no week renders
## with the money in neither place.
##
## The bar every surface ships at (docs/design/00-spine.md §11): readable first
## pass by a tired player; concepts named in real business terms with a teaching
## line where a number first appears; no dead ends and every state leavable;
## drawn in the game's hand, never a SaaS panel. The shared components live in
## game/src/ui/components.gd (DeskKit) — the borrow/term steppers are
## DeskKit.stepper(), and SIGN THE NOTE is DeskKit.review().

## Draw the borrow/repay controls, notes list, forecast, tax block, full
## statement. `b` is the Binder itself (untyped to keep the two files free of a
## cyclic class dependency).
static func draw(b) -> void:
	var state: GameState = b.state
	var y := DeskKit.title(b, "the bank — money, debt, and the taxman")
	# WHAT THE COMPANY OWES, TODAY. The shark is the only lender a garage has,
	# and it says so in one cold clause.
	if state.loan_principal > 0:
		b.label("THE SHARK — $%s owed at 18%%/wk, and it feeds first" % b.fmt(state.loan_principal),
			Vector2(DeskKit.X_ID, y), DeskKit.ROW, Binder.PEN, 1100.0)
		y += 40.0
		b.label("that is $%s a week in interest alone, before anything you sell" %
			b.fmt(int(round(float(state.loan_principal) * 0.18))),
			Vector2(DeskKit.X_ID, y), DeskKit.DETAIL, Color(Binder.INK, 0.75), 1100.0)
		y += 44.0
	else:
		y = DeskKit.empty(b, Vector2(DeskKit.X_ID, y),
			"you owe nobody anything. rare, and worth noticing.",
			"debt buys time, and time is the only thing a runway is made of.")
		y += 10.0
	# THE ERA GATE, taught rather than greyed out (docs/design/00-spine.md §9).
	if state.era == "garage":
		b.label("no bank answers a garage — only the shark does.",
			Vector2(DeskKit.X_ID, y), DeskKit.STATUS, Color(Binder.INK, 0.7), 1100.0)
		y += 38.0
		b.label("a desk somewhere other than your kitchen is what puts you on their radar.",
			Vector2(DeskKit.X_ID, y), DeskKit.DETAIL, Color(Binder.INK, 0.5), 1100.0)
	else:
		b.label("the bank returns your calls now — terms, notes and the taxman land on this desk.",
			Vector2(DeskKit.X_ID, y), DeskKit.STATUS, Color(Binder.INK, 0.7), 1100.0)
	DeskKit.footer(b, {
		"rules": "the rules of this desk: a LOAN is rented money — you pay for the "
			+ "time, not the amount · INTEREST bills every week, sold or not · the "
			+ "taxman takes his cut of profit, never of revenue",
	})

## A press inside this desk. `id` is whatever the desk's own draw registered.
static func handle(_b, _id: String) -> void:
	pass
