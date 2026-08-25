class_name DeskCatalog
extends RefCounted
## DESK — the binder's `pricing` tab. Spec: docs/design/01-catalog.md
##
## `binder.gd` dispatches the tab body here and passes ITSELF, so this file draws
## through the binder's own helpers and never reaches into the sheet directly.
##
## WHAT IS HERE NOW is today's shipped pricing page, moved verbatim off the
## binder so the lane REPLACES a working baseline instead of a blank file. The
## lane's job (01 §7) is the five-state machine: LIST · DETAIL(i) · WRITE-IN ·
## REVIEW · the write-in arrival — every state reachable and leavable, `◂ all
## offers` everywhere, desk-local state in `b.desk` (never saved).
##
## The bar every surface ships at (docs/design/00-spine.md §11): readable first
## pass by a tired player; concepts named in real business terms with a teaching
## line where a number first appears; no dead ends and every state leavable;
## drawn in the game's hand, never a SaaS panel. The shared components live in
## game/src/ui/components.gd (DeskKit) — use them, never fork them.

## Draw the five-state pricing machine: LIST, DETAIL, WRITE-IN, REVIEW. `b` is
## the Binder itself (untyped to keep the two files free of a cyclic class
## dependency).
static func draw(b) -> void:
	var state: GameState = b.state
	b.label("pricing — what %s sells" % state.company_name, Vector2(10, 6), 36)
	if state.offers.is_empty():
		b.label("the world hasn't defined your offers yet — they arrive with the bible.",
			Vector2(10, 90), 28, Color(Binder.INK, 0.6))
		return
	var y := 84.0
	for oi in state.offers.size():
		var o: Dictionary = state.offers[oi]
		var price := float(o.get("price", 0.0))
		var fair := float(o.get("fair_price", 1.0))
		b.label("%s  ·  %s" % [String(o.get("name", "?")).to_upper(), String(o.get("unit", ""))],
			Vector2(10, y), 30)
		var uc_eff := float(o.get("unit_cost", 0.0)) * SimEngine.learning_curve(state)
		b.label("the street charges ≈ $%s  ·  costs you ≈ $%s to serve" % [
			b.fmt(int(fair)), b.fmt(int(round(uc_eff)))], Vector2(10, y + 38), 23,
			Color(Binder.INK, 0.55))
		if price <= 0.0 and bool(o.get("price_set", false)):
			b.label("FREE ON PURPOSE — pays in users, not dollars", Vector2(430, y + 6), 27, Binder.BLUE)
		elif price <= 0.0:
			b.label("! no price set — billing at the going rate $%s" % b.fmt(int(fair)),
				Vector2(430, y + 6), 27, Binder.PEN)
		else:
			var dem := SimEngine.offer_demand(o, price)
			var verdict := "about fair" if dem > 0.85 and dem < 1.15 else \
					("a deal — demand ×%.1f" % dem if dem >= 1.15 else \
					("pricey — %d%% of fair demand" % int(dem * 100.0) if dem > 0.25 else "absurd — ~nobody buys"))
			b.label("$%s  ·  margin $%s/unit  ·  %s" % [b.fmt(int(price)),
				b.fmt(int(round(price - uc_eff))), verdict], Vector2(430, y + 6), 28,
				Binder.INK if dem > 0.25 else Binder.PEN)
		var minus := Button.new()
		minus.text = "−"
		minus.position = Vector2(1000, y)
		minus.size = Vector2(52, 46)
		b.ink_btn(minus)
		var idx := oi
		minus.pressed.connect(func() -> void:
			price_step(b, idx, -1)
			b.refresh())
		b.pane().add_child(minus)
		var plus := Button.new()
		plus.text = "+"
		plus.position = Vector2(1064, y)
		plus.size = Vector2(52, 46)
		b.ink_btn(plus)
		plus.pressed.connect(func() -> void:
			price_step(b, idx, 1)
			b.refresh())
		b.pane().add_child(plus)
		y += 104.0
	var arpu := SimEngine.offers_arpu(state)
	if arpu >= 0.0:
		var cpc := SimEngine.offers_cogs_per_customer(state)
		b.label("all offers together: ≈ $%.1f in − $%.1f serving = $%.1f margin per customer per week  →  ≈ $%s/wk at %d customers" % [
			arpu, cpc, arpu - cpc, b.fmt(int((arpu - cpc) * float(state.traction))), state.traction],
			Vector2(10, y + 10), 26, Binder.BLUE, 1100.0)
	b.label("the curve: price at the street's level and demand is fair · discount and demand grows · overprice and it dies fast",
		Vector2(10, y + 56), 22, Color(Binder.INK, 0.5), 1100.0)

## price steps: sensible ladder around the fair price (0 = off sale)
static func price_step(b, oi: int, dir: int) -> void:
	var o: Dictionary = b.state.offers[oi]
	var fair := float(o.get("fair_price", 10.0))
	var steps: Array = [0.0]
	for m in [0.4, 0.55, 0.7, 0.85, 1.0, 1.15, 1.35, 1.6, 2.0, 2.6, 3.5, 5.0]:
		steps.append(maxf(roundf(fair * m), 1.0))
	var cur := float(o.get("price", 0.0))
	var idx := 0
	for i in steps.size():
		if float(steps[i]) <= cur:
			idx = i
	idx = clampi(idx + dir, 0, steps.size() - 1)
	o["price"] = float(steps[idx])
	o["price_set"] = true   # the founder chose this — $0 included (a conscious giveaway)

## A press inside this desk. `id` is whatever the desk's own draw registered.
static func handle(_b, _id: String) -> void:
	pass
