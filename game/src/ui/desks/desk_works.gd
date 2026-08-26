class_name DeskWorks
extends RefCounted
## DESK — COSTS · "the works". W2 lane: L-DIVWORKS.
## THE QUESTION: "can we serve what they want, and what does one cost?"
##
## Four numbered zones in every business's OWN units (DECISIONS: the factory
## broadened; mockups 10/11/12): 1 CAN WE SERVE? · 2 WHAT ONE COSTS · 3 WHAT
## MAKES THE CAPACITY · 4 THE RELIEF VALVES. The zones never change; their
## CONTENTS climb three rungs — boutique (named things) → house (the demand
## mix + the ticket book on the ledger sheet) → empire (THE LINEUP, hero rows
## per division, face B; press a row and its whole rung-2 works opens).
##
## States (desk dict): mode "arrange" → DeskArrange (the WRITE view) · page
## "capacity" → the assets-and-relief DETAIL sheet (the collapse ladder's
## honest tail: the crowd folds, the valves stay face-up) · row <site id> →
## the empire's opened roof (rung-2 scoped to it) · ticket <i> → the ticket
## book's opened row · slice — the empire's axis.
##
## Money lives in columns; the hero answers the question alone; every gap is
## priced in the works' own receipts (the engine's — this desk only reads).

const QUESTION := "can we serve what they want, and what does one cost?"

static func hero_summary(state) -> Dictionary:
	var s: GameState = state
	if s.offers.is_empty():
		return {"big": "the works", "line": "the desk reads your offers for its ticket — nothing on the shelf yet"}
	var w := SimWorks.week_view(s)
	var vw := SimWorks.vocab(s)
	var unit := String(vw.get("unit_word", "unit"))
	if SimDivisions.rung(s) >= 3:
		var n := SimDivisions.site_divisions(s)
		return {"big": "%d roofs · %d %ss a week" % [maxi(n, SimDivisions.products_count(s)),
			int(round(float(w.get("served_units", 0.0)))), unit],
			"line": "every line keeps its own books — the works is a book of books now"}
	if s.biz_what == "Software":
		return {"big": "%d %ss under a ceiling of %d" % [int(round(float(w.get("served_units", 0.0)))),
			unit, int(round(float(w.get("ceiling", 0.0))))],
			"line": "software scales, support doesn't — the care team is the ceiling"}
	return {"big": "%d %ss wanted · capacity for %d" % [int(round(float(w.get("demand_units", 0.0)))),
		unit, int(round(float(w.get("capacity_units", 0.0))))],
		"line": "a %s you cannot take is revenue that walks out the door" % unit}

# ═════════════════════════════════ DRAW ══════════════════════════════════════

static func draw(b) -> void:
	var s: GameState = b.state
	if String(b.desk.get("mode", "")) == "arrange":
		DeskArrange.draw(b)
		return
	# a popped arrange mode leaves no stale staging behind
	for k in ["teardown", "open_roof", "edit", "staged2", "chip_k"]:
		b.desk.erase(k)
	DeskKit.word(b, "arrange →", Vector2(DeskKit.X_ID + 980.0, 6.0), func() -> void:
		b.desk["mode"] = "arrange", DeskKit.STATUS, DeskKit.BLUE, 160.0)
	if s.offers.is_empty():
		_empty(b, s)
		return
	if String(b.desk.get("page", "")) == "capacity":
		_capacity_sheet(b, s, String(b.desk.get("row", "")))
		return
	var opened := String(b.desk.get("row", ""))
	if SimDivisions.rung(s) >= 3 and opened == "":
		_empire(b, s)
		return
	_house_or_boutique(b, s, opened)

## A desk with nothing still teaches — the works never sleeps, it explains.
static func _empty(b, s: GameState) -> void:
	var y := DeskKit.hero_band(b, "the works", "every business has works — the cost of delivering what you sell", DeskKit.INK, 6.0, false)
	y = DeskKit.empty(b, Vector2(DeskKit.X_ID, y),
		"nothing is on the shelf yet, so there is nothing to serve.",
		"define an offer on the OFFERS desk — its cost lines become the unit ticket here", true)
	if s.biz_what == "Service":
		y += 8.0
		b.label("meanwhile the hands are real: capacity %d slots/wk (the founder + the crew)" % int(round(SimWorks.service_capacity(s))),
			Vector2(DeskKit.X_ID, y), DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 1080.0)
	DeskKit.footer(b, {"y": 806.0, "computed": "the works reads your team for hands and your offers for the ticket",
		"rules": "one desk, every running cost of delivering", "rules_y": 840.0})
	DeskKit.hero_question(b, QUESTION)

# ─────────────────────────── rungs 1-2 (one roof) ────────────────────────────

static func _house_or_boutique(b, s: GameState, opened_site: String) -> void:
	var w := SimWorks.week_view(s)
	var vw := SimWorks.vocab(s)
	var unit := String(vw.get("unit_word", "unit"))
	var scoped := opened_site != ""
	if scoped:
		DeskKit.back(b, "back to the lineup", func() -> void:
			b.desk.erase("row"))
	# ── THE HERO: the tab's question answered in one second
	var demand := float(w.get("demand_units", 0.0))
	var cap := float(w.get("capacity_units", 0.0))
	var served := float(w.get("served_units", 0.0))
	var walk := float(w.get("walk_units", 0.0))
	var big := ""
	var line := ""
	var mrow := _blended_margin(s)
	if scoped:
		var row := SimDivisions.works_book(s, "site").filter(func(r): return String((r as Dictionary).get("id", "?")) == opened_site)
		var rd: Dictionary = row[0] if not row.is_empty() else {}
		demand = float(rd.get("wanted", 0.0))
		cap = float(rd.get("slots", 0.0))
		served = float(rd.get("served", 0.0))
		walk = maxf(demand - served, 0.0)
		big = "%s · %d %ss a week" % [String(rd.get("name", "?")), int(round(served)), unit]
		line = "this roof's own book — rent, hands and learning under one name"
	elif s.biz_what == "Software":
		big = "%d %ss served · room for %d" % [int(round(served)), unit, int(round(float(w.get("ceiling", 0.0))))]
		line = "software doesn't turn people away — it slowly serves everyone worse"
	else:
		big = "%d %ss wanted · %s for %d" % [int(round(demand)), unit, String(vw.get("capacity_word", "capacity")), int(round(cap))]
		line = "each one leaves at its price and costs real hands, rooms and parts"
	var y := DeskKit.hero_band(b, big, line, DeskKit.INK, 6.0 if not scoped else 44.0, false)
	# the hero's right corner: margin each + the gap, money in a column
	var mv: Label = b.label("margin each  " + _money(mrow), Vector2(760.0, 10.0 if not scoped else 48.0), DeskKit.STATUS, DeskKit.INK, 240.0)
	mv.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	if walk >= 1.0:
		var wv: Label = b.label("%d turned away" % int(round(walk)), Vector2(760.0, 44.0 if not scoped else 82.0), DeskKit.DETAIL, DeskKit.PEN, 240.0)
		wv.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	# ── ZONE 1 · CAN WE SERVE?
	var house := SimDivisions.rung(s) >= 2 and not scoped
	if house:
		y = _zone_demand_mix(b, s, y, w, unit)
	else:
		y = _zone_capbars(b, s, y, w, unit, opened_site)
	# ── ZONE 2 · WHAT ONE COSTS
	y = _zone_ticket(b, s, y, house)
	# ── ZONES 3+4, folded to one honest band each (press opens the DETAIL)
	y = _capacity_band(b, s, y, opened_site)
	DeskKit.footer(b, {"y": 806.0,
		"computed": "the works reads your team for hands, your offers for the ticket — one desk, every cost of delivering",
		"rules": "" if walk < 1.0 else "", "rules_y": 840.0,
		"warning": ("$%d/wk walks away — relief valves or hires close it" % int(round(float(w.get("unbilled", 0.0))))) if float(w.get("unbilled", 0.0)) >= 1.0 else ""})
	DeskKit.hero_question(b, QUESTION)

## Zone 1, boutique face: the three drawn bars — they want / we hold / relief.
static func _zone_capbars(b, s: GameState, y: float, w: Dictionary, unit: String, site: String) -> float:
	var vw := SimWorks.vocab(s)
	var rows: Array = []
	var demand := float(w.get("demand_units", 0.0))
	var cap := float(w.get("capacity_units", 0.0))
	var relief := float(w.get("relief_used", 0.0))
	if site != "":
		var book := SimDivisions.works_book(s, "site")
		for r in book:
			if String((r as Dictionary).get("id", "?")) == site:
				demand = float((r as Dictionary).get("wanted", 0.0))
				cap = float((r as Dictionary).get("slots", 0.0))
				relief = 0.0
	var hi := maxf(maxf(demand, cap + relief), 1.0)
	var lesson := "a %s you cannot take is revenue that walks out the door" % unit
	var over := 0.0
	if s.biz_what == "Software":
		lesson = "past the ceiling nothing walks away — replies slip and churn bites"
		over = float(w.get("over", 0.0))
	elif s.biz_what == "Marketplace":
		lesson = "a buyer who finds an empty shelf rarely knocks twice"
	rows.append({"label": "they want", "pct": demand / hi * 100.0, "col": DeskKit.KRAFT2,
		"note": "%d/wk" % int(round(demand))})
	rows.append({"label": String(vw.get("capacity_word", "we hold")).left(18),
		"pct": cap / hi * 100.0,
		"col": DeskKit.PEN if (over > 0.0 or demand > cap + relief + 0.5) else DeskKit.SAGE,
		"note": "%d/wk" % int(round(cap))})
	if relief >= 1.0:
		rows.append({"label": String(vw.get("relief_word", "relief")).left(18),
			"pct": relief / hi * 100.0, "col": DeskKit.BLUE, "note": "+%d/wk" % int(round(relief))})
	var h := 88.0 + float(rows.size()) * 40.0
	var z := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, h, 1, "CAN WE SERVE?", lesson)
	DeskKit.capbars(b, float(z.get("content_x", 0.0)), float(z.get("content_y", 0.0)), 1000.0, rows)
	return y + h + 10.0

## Zone 1, house face: THE DEMAND MIX — offers share one pool; the gap lands
## on whatever you deprioritize. Gap rows stay face-up; the healthy fold.
static func _zone_demand_mix(b, s: GameState, y: float, w: Dictionary, unit: String) -> float:
	var rows := SimDivisions.works_book(s, "offer")
	rows = rows.filter(func(r): return String((r as Dictionary).get("kind", "")) == "offer")
	rows.sort_custom(func(a, bb) -> bool:
		var ga: float = float((a as Dictionary).get("wanted", 0.0)) - float((a as Dictionary).get("served", 0.0))
		var gb: float = float((bb as Dictionary).get("wanted", 0.0)) - float((bb as Dictionary).get("served", 0.0))
		return ga > gb)
	var shown := mini(rows.size(), 4)
	var folded := rows.size() - shown
	var h := 88.0 + 34.0 + float(shown) * 40.0 + 48.0 + (44.0 if folded > 0 else 0.0) + 34.0
	var z := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, h, 1, "CAN WE SERVE? — THE DEMAND MIX",
		"offers share one capacity pool; the gap lands on whatever you deprioritize")
	var sheet := DeskKit.ledger_sheet(b, float(z.get("content_x", 0.0)), float(z.get("content_y", 0.0)), 1070.0, {
		"columns": [{"label": "offer", "w": 420.0}, {"label": "wanted", "w": 170.0, "align": "right"},
			{"label": "served", "w": 170.0, "align": "right"}, {"label": "gap", "w": 170.0, "align": "right"}],
		"amount": 3, "adjust": false, "unit": "all figures %ss/week" % unit})
	var want_t := 0.0
	var served_t := 0.0
	for i in shown:
		var rd: Dictionary = rows[i]
		var gap := float(rd.get("wanted", 0.0)) - float(rd.get("served", 0.0))
		want_t += float(rd.get("wanted", 0.0))
		served_t += float(rd.get("served", 0.0))
		DeskKit.ledger_row(b, sheet, [String(rd.get("name", "?")),
			"%d" % int(round(float(rd.get("wanted", 0.0)))),
			"%d" % int(round(float(rd.get("served", 0.0)))),
			("−%d" % int(round(gap))) if gap >= 0.5 else "—"],
			{"col": DeskKit.PEN if gap >= 0.5 else DeskKit.INK})
	for i2 in range(shown, rows.size()):
		want_t += float((rows[i2] as Dictionary).get("wanted", 0.0))
		served_t += float((rows[i2] as Dictionary).get("served", 0.0))
	var gap_t := want_t - served_t
	DeskKit.ledger_total(b, sheet, "THE WEEK", ("−%d" % int(round(gap_t))) if gap_t >= 0.5 else "0",
		DeskKit.PEN if gap_t >= 0.5 else DeskKit.INK)
	var end_y := DeskKit.ledger_end(b, sheet)
	if folded > 0:
		end_y = DeskKit.fold_row(b, float(z.get("content_x", 0.0)), end_y - 8.0, folded, "offers share the pool too")
	# the one shared pool, drawn: the teaching under the mix
	DeskKit.meter(b, float(z.get("content_x", 0.0)), end_y - 6.0, 560.0,
		clampf(served_t / maxf(want_t, 0.001), 0.0, 1.0), DeskKit.SAGE,
		"the shared pool — %d of %d" % [int(round(served_t)), int(round(want_t))])
	return y + h + 10.0

## Zone 2: the unit ticket (boutique) or THE TICKET BOOK (house).
static func _zone_ticket(b, s: GameState, y: float, house: bool) -> float:
	if not house:
		var t := SimWorks.unit_ticket(s, _flagship_i(s))
		var lines: Array = []
		var raw: Array = t.get("lines", [])
		for i in mini(raw.size(), 3):
			lines.append({"label": String((raw[i] as Dictionary).get("label", "")),
				"value": "$%s" % _m(float((raw[i] as Dictionary).get("amount", 0.0)))})
		if raw.size() > 3:
			var rest := 0.0
			for j in range(3, raw.size()):
				rest += float((raw[j] as Dictionary).get("amount", 0.0))
			lines.append({"label": "everything else", "value": "$%s" % _m(rest)})
		lines.append({"label": "sells for", "value": "$%s" % _m(float(t.get("sells", 0.0)))})
		# the kit ticket's own height formula: head + lines + double-ruled total + foot
		var th := 46.0 + float(lines.size()) * 32.0 + 44.0 + 30.0 + 14.0
		var h := 84.0 + th
		var z := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, h, 2, "WHAT ONE COSTS",
			"practice makes every %s cheaper — the learning curve is real money" % String(SimWorks.vocab(s).get("unit_word", "unit")))
		var lc := float(t.get("lc", 1.0))
		DeskKit.ticket(b, float(z.get("content_x", 0.0)) + 560.0, float(z.get("content_y", 0.0)) - 6.0, 470.0, {
			"title": "ONE %s" % String(s.offers[_flagship_i(s)].get("name", "unit")).to_upper(),
			"lines": lines, "total_label": "margin, each",
			"total_value": _money(float(t.get("margin", 0.0))),
			"total_col": DeskKit.SAGE if float(t.get("margin", 0.0)) >= 0.0 else DeskKit.PEN,
			"foot": "learning ×%.2f — %s served so far" % [lc, _commas(s.served_total)] if lc < 0.995 else "the curve starts once volume does"})
		b.label("costs, each — the offer's own cost lines,\nlearning applied at the total, never per line",
			Vector2(float(z.get("content_x", 0.0)), float(z.get("content_y", 0.0)) + 8.0), DeskKit.DETAIL,
			Color(DeskKit.INK, 0.6), 520.0)
		return y + h + 10.0
	# THE TICKET BOOK: one row per offer; press a row, its ticket opens beside
	var rows := SimDivisions.works_book(s, "offer")
	rows = rows.filter(func(r): return String((r as Dictionary).get("kind", "")) == "offer")
	var shown := mini(rows.size(), 4)
	var folded := rows.size() - shown
	var h2 := 88.0 + 34.0 + float(shown) * 40.0 + 48.0 + (44.0 if folded > 0 else 0.0)
	var z2 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, h2, 2, "WHAT ONE COSTS — THE TICKET BOOK",
		"one row per offer; press a row and its ticket opens itemized")
	var opened := int(b.desk.get("ticket", 0))
	var sheet := DeskKit.ledger_sheet(b, float(z2.get("content_x", 0.0)), float(z2.get("content_y", 0.0)), 620.0, {
		"columns": [{"label": "offer", "w": 250.0}, {"label": "costs each", "w": 120.0, "align": "right"},
			{"label": "margin", "w": 120.0, "align": "right"}],
		"amount": 2, "adjust": false, "unit": ""})
	var blended := 0.0
	var volume := 0.0
	for i in shown:
		var rd: Dictionary = rows[i]
		var idx := int(String(rd.get("id", "offer_0")).trim_prefix("offer_"))
		DeskKit.ledger_row(b, sheet, [String(rd.get("name", "?")) + ("  (open)" if idx == opened else ""),
			"$%s" % _m(float(rd.get("unit_cost", 0.0))),
			_money(float(rd.get("margin_each", 0.0)))],
			{"col": DeskKit.SAGE if float(rd.get("margin_each", 0.0)) >= 0.0 else DeskKit.PEN,
			"on_press": func() -> void: b.desk["ticket"] = idx})
	for r2 in rows:
		blended += float((r2 as Dictionary).get("margin_each", 0.0)) * float((r2 as Dictionary).get("vol", 0.0))
		volume += float((r2 as Dictionary).get("vol", 0.0))
	DeskKit.ledger_total(b, sheet, "BLENDED", _money(blended / maxf(volume, 0.001)),
		DeskKit.SAGE if blended >= 0.0 else DeskKit.PEN)
	var end_y := DeskKit.ledger_end(b, sheet)
	if folded > 0:
		DeskKit.fold_row(b, float(z2.get("content_x", 0.0)), end_y - 10.0, folded, "offers in the book")
	# the opened ticket, beside the book
	var ti := clampi(opened, 0, s.offers.size() - 1)
	var t2 := SimWorks.unit_ticket(s, ti)
	var tl: Array = []
	var raw2: Array = t2.get("lines", [])
	for i3 in mini(raw2.size(), 3):
		tl.append({"label": String((raw2[i3] as Dictionary).get("label", "")),
			"value": "$%s" % _m(float((raw2[i3] as Dictionary).get("amount", 0.0)))})
	tl.append({"label": "sells for", "value": "$%s" % _m(float(t2.get("sells", 0.0)))})
	DeskKit.ticket(b, float(z2.get("content_x", 0.0)) + 660.0, float(z2.get("content_y", 0.0)) - 6.0, 400.0, {
		"title": "%s — OPENED" % String(s.offers[ti].get("name", "?")).to_upper(),
		"lines": tl, "total_label": "margin, each",
		"total_value": _money(float(t2.get("margin", 0.0))),
		"total_col": DeskKit.SAGE if float(t2.get("margin", 0.0)) >= 0.0 else DeskKit.PEN})
	return y + h2 + 10.0

## Zones 3+4 folded to one band: the capacity assets counted, the valves
## named, both pressable — attention is never hidden, the crowd is.
static func _capacity_band(b, s: GameState, y: float, site: String) -> float:
	var vw := SimWorks.vocab(s)
	var w := SimWorks.week_view(s)
	var facts := ""
	match s.biz_what:
		"Service":
			var heads := 0
			for e in s.employees:
				var role := String((e as Dictionary).get("role", ""))
				if not (role.contains("sales") or role.contains("marketing")):
					heads += 1
			facts = "HANDS ×%d — %d slots/wk" % [heads + (1 if s.sites.is_empty() or site == "" else 0),
				int(round(SimWorks.capacity_of_site(s, site) if site != "" else SimWorks.service_capacity(s)))]
		"Software":
			facts = "the care team holds %d seats · servers are not the bottleneck" % int(round(SimWorks.software_ceiling(s)))
		"Marketplace":
			facts = "SELLERS ×%d — feed %d orders/wk" % [SimWorks.seller_pool(s), int(round(float(w.get("capacity_units", 0.0))))]
		"Hardware":
			facts = "MACHINES ×%d — %d units/wk" % [(s.hardware.get("equipment", []) as Array).size(),
				int(round(SimFactory.capacity(s)))]
	var relief_txt := _relief_line(s)
	pen_row(b, y, 3, "WHAT MAKES THE CAPACITY", facts, func() -> void:
		b.desk["page"] = "capacity")
	pen_row(b, y + 52.0, 4, "THE RELIEF VALVES", relief_txt, func() -> void:
		b.desk["page"] = "capacity")
	return y + 108.0

## One line on the valve's standing setting, for the folded band.
static func _relief_line(s: GameState) -> String:
	match s.biz_what:
		"Service":
			var v := SimWorks.relief_get(s, "freelance")
			return "freelancers up to %d/wk — $%d each" % [v, int(round(SimDivisions.pb(s, "freelance_rate")))] if v > 0 \
				else "no freelancers booked — the valve is closed"
		"Software":
			var bv := SimWorks.relief_get(s, "burst")
			return "cloud burst +%d seats provisioned" % bv if bv > 0 \
				else "no burst provisioned — the ceiling is the ceiling"
		"Marketplace":
			var rv := SimWorks.relief_get(s, "recruit_supply")
			return "recruitment push $%d/wk" % rv if rv > 0 \
				else "no recruitment push — supply grows on its own"
	return "the subcontract shop is %s" % ("ON" if SimWorks.relief_get(s, "subcontract") > 0 else "OFF")

static func pen_row(b, y: float, num: int, title: String, facts: String, on_press: Callable) -> void:
	b.label("%d · %s" % [num, title], Vector2(DeskKit.X_ID + 8.0, y), DeskKit.DETAIL, DeskKit.INK, 360.0)
	b.label(facts, Vector2(DeskKit.X_ID + 400.0, y), DeskKit.DETAIL, Color(DeskKit.INK, 0.65), 560.0)
	DeskKit.word(b, "open →", Vector2(DeskKit.X_ID + 980.0, y - 6.0), on_press, DeskKit.DETAIL, DeskKit.BLUE, 130.0)
	DeskKit.pen_rule(b, y + 40.0, DeskKit.X_ID, 1120.0, Color(DeskKit.INK, 0.14), int(y) % 17)

## THE DETAIL SHEET: zones 3+4 full size — the assets grouped like the team,
## and every valve priced against in-house with its own separate − +.
static func _capacity_sheet(b, s: GameState, site: String) -> void:
	DeskKit.back(b, "back to the works", func() -> void:
		b.desk.erase("page"))
	var vw := SimWorks.vocab(s)
	var y := DeskKit.hero_band(b, "what makes the capacity",
		"your capacity is %s — and bought help is dearer per %s, but dearer beats turned away" % [
			String(vw.get("capacity_word", "people and rooms")), String(vw.get("unit_word", "unit"))],
		DeskKit.INK, 44.0, false)
	# ── ZONE 3: the assets, grouped, best named, crowd counted
	var rows: Array = []
	match s.biz_what:
		"Service":
			var by_site := {}
			var best := ""
			var best_skill := -1
			var serving := 0
			for e in s.employees:
				var ed: Dictionary = e
				var role := String(ed.get("role", ""))
				if role.contains("sales") or role.contains("marketing"):
					continue
				serving += 1
				by_site[String(ed.get("site", ""))] = int(by_site.get(String(ed.get("site", "")), 0)) + 1
				if int(ed.get("skill", 3)) > best_skill:
					best_skill = int(ed.get("skill", 3))
					best = String(ed.get("name", ""))
			rows.append(["HANDS" + (" — %s leads" % best if best != "" else " — the founder's own"),
				"×%d" % (serving + 1), "%d slots/wk" % int(round(SimWorks.service_capacity(s))),
				"ramping hands give zero this week"])
			if s.sites.size() > 0:
				rows.append(["ROOFS", "×%d" % SimDivisions.site_divisions(s), "—",
					"each roof holds its own hands"])
		"Software":
			rows.append(["THE SERVERS", "—", "%d seats" % int(round(SimWorks.software_ceiling(s) * 1.5)),
				"not the bottleneck"])
			var care_heads := 0
			for e2 in s.employees:
				if String((e2 as Dictionary).get("role", "")).contains("support"):
					care_heads += 1
			rows.append(["THE CARE TEAM", "×%d" % care_heads,
				"%d seats" % int(round(SimWorks.software_ceiling(s))),
				"THE ceiling — hire or fund care to raise it"])
		"Marketplace":
			rows.append(["THE SELLER POOL", "×%d" % SimWorks.seller_pool(s),
				"%d orders/wk" % int(round(SimWorks.marketplace_supply(s))),
				"grows with the buyers, lags your growth"])
		"Hardware":
			var eq: Array = s.hardware.get("equipment", [])
			for m in eq:
				var md: Dictionary = m
				rows.append([String(md.get("name", "?")),
					"" if String(md.get("site", "")) == "" else SimDivisions._roof_name(s, String(md.get("site", ""))),
					"+%d units/wk" % int(float(md.get("capacity_add", 0.0))),
					"resale ≈ half"])
				if rows.size() >= 4:
					break
			if eq.is_empty():
				rows.append(["THE BENCH", "—", "%d units/wk" % int(round(SimFactory.capacity(s))),
					"hands only — machines live on WHAT WE MAKE"])
	var h3 := 88.0 + 34.0 + float(rows.size()) * 40.0 + 12.0
	var z3 := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, h3, 3, "WHAT MAKES THE CAPACITY",
		"assets group like the team does — best named, the crowd counted")
	var sheet := DeskKit.ledger_sheet(b, float(z3.get("content_x", 0.0)), float(z3.get("content_y", 0.0)), 1070.0, {
		"columns": [{"label": "asset group", "w": 360.0}, {"label": "count", "w": 150.0, "align": "right"},
			{"label": "gives", "w": 200.0, "align": "right"}, {"label": "note", "w": 300.0}],
		"amount": 2, "adjust": false, "unit": ""})
	for r in rows:
		DeskKit.ledger_row(b, sheet, r)
	DeskKit.ledger_end(b, sheet)
	y += h3 + 12.0
	# ── ZONE 4: the valves, each its own SEPARATE − +, priced against in-house
	y = _zone_relief(b, s, y)
	DeskKit.footer(b, {"y": 806.0,
		"computed": "letting hands go is never free — severance is always owed (→ team)",
		"rules": "every valve priced against in-house — dearer each, but dearer beats turned away", "rules_y": 840.0})
	DeskKit.hero_question(b, QUESTION)

static func _zone_relief(b, s: GameState, y: float) -> float:
	var cats: Array = []
	match s.biz_what:
		"Service":
			cats = [["freelance", "freelancers, up to", "/wk", "$%d each vs $%s in-house" % [
				int(round(SimDivisions.pb(s, "freelance_rate"))), _m(SimWorks.base_unit_cost(s))]]]
		"Software":
			cats = [["burst", "cloud burst, plus", " seats", "queue persists — burst closes at most 60%"]]
		"Marketplace":
			cats = [["recruit_supply", "recruitment push", "$/wk", "≈1 seller per $35, each feeds ≈2.5 orders/wk"]]
		"Hardware":
			cats = [["subcontract", "the subcontract shop", "", "×%.2f unit cost — their margin, none of your learning" % SimFactory.sub_mult(s.era)]]
	var h := 88.0 + float(cats.size()) * 52.0 + 8.0
	var z := DeskKit.zone(b, DeskKit.X_ID, y, 1120.0, h, 4, "THE RELIEF VALVES",
		"the valve is a standing lever — it answers this week and every week until you close it")
	var ry := float(z.get("content_y", 0.0))
	for c in cats:
		var cat := String(c[0])
		var v := SimWorks.relief_get(s, cat)
		var steps: Array = SimWorks.relief_steps(cat)
		b.label(String(c[1]), Vector2(float(z.get("content_x", 0.0)), ry), DeskKit.STATUS, DeskKit.INK, 340.0)
		var vv: Label = b.label(("ON" if v > 0 else "OFF") if cat == "subcontract" else "%d%s" % [v, String(c[2])],
			Vector2(float(z.get("content_x", 0.0)) + 350.0, ry), DeskKit.STATUS, DeskKit.PEN, 170.0)
		vv.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		b.label(String(c[3]), Vector2(float(z.get("content_x", 0.0)) + 560.0, ry + 6.0), DeskKit.DETAIL,
			Color(DeskKit.INK, 0.6), 420.0)
		if cat == "subcontract":
			DeskKit.adjust_pair(b, float(z.get("content_x", 0.0)) + 990.0, ry + 4.0,
				func() -> void: SimWorks.relief_set(s, cat, 0),
				func() -> void: SimWorks.relief_set(s, cat, 1),
				v <= 0, v >= 1)
		else:
			DeskKit.adjust_pair(b, float(z.get("content_x", 0.0)) + 990.0, ry + 4.0,
				func() -> void: SimWorks.relief_set(s, cat, int(DeskKit.ladder(steps, float(v), -1))),
				func() -> void: SimWorks.relief_set(s, cat, int(DeskKit.ladder(steps, float(v), 1))),
				DeskKit.at_min(steps, float(v)), DeskKit.at_max(steps, float(v)))
		ry += 52.0
	return y + h + 10.0

# ───────────────────────────── rung 3: THE EMPIRE ────────────────────────────

## THE LINEUP (face B, hero rows): every division one calm row — five facts,
## the red climbing, the ghost door — then the two-ticket lesson of scale.
static func _empire(b, s: GameState) -> void:
	var axes := SimDivisions.slice_axes(s)
	var slice := String(b.desk.get("slice", SimDivisions.default_slice(s)))
	if not axes.has(slice):
		slice = SimDivisions.default_slice(s)
	var book := SimDivisions.works_book(s, slice)
	var divs := book.filter(func(r): return String((r as Dictionary).get("kind", "")) != "shared")
	var shared: Dictionary = book[book.size() - 1]
	var vw := SimWorks.vocab(s)
	var unit := String(vw.get("unit_word", "unit"))
	var w := SimWorks.week_view(s)
	var served_t := float(w.get("served_units", 0.0))
	var y := DeskKit.hero_band(b, "%d %s · %d %ss a week" % [divs.size(),
		_axis_word(s, slice, divs.size()), int(round(served_t)), unit],
		"every line keeps its own books — press one and its whole works opens", DeskKit.INK, 6.0, false)
	# the slice control: only axes with ≥2 divisions exist to be pressed
	if axes.size() > 1:
		var next_axis := String(axes[(axes.find(slice) + 1) % axes.size()])
		DeskKit.word(b, "sliced by %s — slice by %s instead ▸" % [_axis_word(s, slice, 2), _axis_word(s, next_axis, 2)],
			Vector2(DeskKit.X_ID + 640.0, y - 40.0), func() -> void:
				b.desk["slice"] = next_axis, DeskKit.DETAIL, DeskKit.BLUE, 420.0)
	else:
		b.label("sliced by %s" % _axis_word(s, slice, 2), Vector2(DeskKit.X_ID + 760.0, y - 36.0),
			DeskKit.DETAIL, Color(DeskKit.INK, 0.5), 320.0)
	# the rows: worst face-up first is already the book's honesty; keep state
	# order (home first) — red wears its sev dot and the clock line
	var shown := mini(divs.size(), 5)
	for i in shown:
		var rd: Dictionary = divs[i]
		var margin := float(rd.get("margin_each", 0.0))
		var facts := "%d%% used · %d/wk · makes each for $%s" % [
			int(round(float(rd.get("util", 0.0)) * 100.0)), int(round(float(rd.get("vol", 0.0)))),
			_m(float(rd.get("unit_cost", 0.0)))]
		var note := String(rd.get("note", ""))
		if note != "" and facts.length() + note.length() <= 48:
			facts += " · " + note
		var id := String(rd.get("id", ""))
		y = DeskKit.hero_row(b, y, {"name": String(rd.get("name", "?")), "facts": facts,
			"value": _money(margin), "col": DeskKit.SAGE if margin >= 0.0 else DeskKit.PEN,
			"sev": int(rd.get("sev", 0)),
			"on_press": (func() -> void: b.desk["row"] = id) if slice == "site" else Callable()})
	if divs.size() > shown:
		y = DeskKit.fold_row(b, DeskKit.X_ID, y, divs.size() - shown, "healthy lines hold steady")
	# the ghost row — the priced door into a new roof (site axis only)
	if slice == "site":
		DeskKit.word(b, "+ a new roof — the pack quotes ≈$%s (lease · fit-out · hires) ▸" % _m(float(SimDivisions.open_pack_cost(s))),
			Vector2(DeskKit.X_ID, y), func() -> void:
				b.desk["mode"] = "arrange"
				b.desk["open_roof"] = true, DeskKit.DETAIL, DeskKit.BLUE, 720.0)
		y += 46.0
	# THE LESSON OF SCALE: same unit, different books — best vs worst, drawn
	if divs.size() >= 2:
		y = _scale_lesson(b, s, divs, y, unit)
	# the company strip: totals + the honest SHARED row
	b.label("SHARED / HQ — %s" % String(shared.get("note", "")), Vector2(DeskKit.X_ID, y),
		DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 800.0)
	var sv: Label = b.label("−$%s/wk" % _m(-float(shared.get("net_wk", 0))), Vector2(DeskKit.X_ID + 880.0, y - 4.0),
		DeskKit.STATUS, DeskKit.INK, 230.0)
	sv.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	DeskKit.footer(b, {"y": 806.0,
		"computed": "unit economics differ by roof — rent, local wages and each roof's own learning; that is the whole lesson of scale",
		"rules": "press any line and its whole rung-2 works opens for that roof", "rules_y": 840.0})
	DeskKit.hero_question(b, QUESTION)

static func _scale_lesson(b, s: GameState, divs: Array, y: float, unit: String) -> float:
	var best: Dictionary = divs[0]
	var worst: Dictionary = divs[0]
	for r in divs:
		var rd: Dictionary = r
		if float(rd.get("vol", 0.0)) <= 0.0:
			continue
		if float(rd.get("unit_cost", 0.0)) < float(best.get("unit_cost", 1e9)):
			best = rd
		if float(rd.get("unit_cost", 0.0)) > float(worst.get("unit_cost", 0.0)):
			worst = rd
	if best == worst:
		return y
	var diff := float(worst.get("unit_cost", 0.0)) - float(best.get("unit_cost", 0.0))
	DeskKit.ticket(b, DeskKit.X_ID + 40.0, y, 330.0, {
		"title": "%s'S %s" % [String(best.get("name", "?")).to_upper(), unit.to_upper()],
		"lines": [], "total_label": "costs, each",
		"total_value": "$%s" % _m(float(best.get("unit_cost", 0.0))), "total_col": DeskKit.SAGE})
	DeskKit.ticket(b, DeskKit.X_ID + 720.0, y, 330.0, {
		"title": "%s'S %s" % [String(worst.get("name", "?")).to_upper(), unit.to_upper()],
		"lines": [], "total_label": "costs, each",
		"total_value": "$%s" % _m(float(worst.get("unit_cost", 0.0))), "total_col": DeskKit.PEN})
	var mid: Label = b.label("+$%s every %s —\nrent, wages and learning, nothing else" % [_m(diff), unit],
		Vector2(DeskKit.X_ID + 390.0, y + 40.0), DeskKit.DETAIL, Color(DeskKit.INK, 0.6), 320.0)
	mid.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	return y + 156.0

# ─────────────────────────────── the helpers ─────────────────────────────────

static func _flagship_i(s: GameState) -> int:
	if SimFactory.active(s):
		return maxi(SimFactory.flagship_index(s), 0)
	return 0

static func _blended_margin(s: GameState) -> float:
	var rows := SimDivisions.works_book(s, "offer")
	var m := 0.0
	var v := 0.0
	for r in rows:
		if String((r as Dictionary).get("kind", "")) != "offer":
			continue
		m += float((r as Dictionary).get("margin_each", 0.0)) * float((r as Dictionary).get("vol", 0.0))
		v += float((r as Dictionary).get("vol", 0.0))
	return m / maxf(v, 0.001)

static func _axis_word(s: GameState, axis: String, n: int) -> String:
	match axis:
		"site":
			return "roofs" if n != 1 else "roof"
		"product":
			return "products" if n != 1 else "product"
	return "offers" if n != 1 else "offer"

## Signed money for a value column: −$73.54, never $-73.54.
static func _money(v: float) -> String:
	return ("−$%s" % _m(-v)) if v < 0.0 else ("$%s" % _m(v))

## Tabular money: 1240 → 1,240 · 27.5 → 27.50 only when cents matter.
static func _m(v: float) -> String:
	if absf(v - roundf(v)) >= 0.005 and absf(v) < 100.0:
		return "%.2f" % v
	return _commas(int(round(v)))

static func _commas(n: int) -> String:
	var t := str(absi(n))
	var out := ""
	while t.length() > 3:
		out = "," + t.substr(t.length() - 3) + out
		t = t.substr(0, t.length() - 3)
	return ("-" if n < 0 else "") + t + out

static func handle(b, id: String) -> void:
	if String(b.desk.get("mode", "")) == "arrange":
		DeskArrange.handle(b, id)
		return
	if id == "leave":
		b.desk.erase("page")
		b.desk.erase("row")
