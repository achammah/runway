class_name SimSpendBook
extends RefCounted
## LANE HELPER — THE ORG SPEND BOOK's state math + the money desks' shared
## display reads (DAG2 W2 L-MONEY). PURE and rng-free: no tick seams, no
## salts, nothing here rolls dice — the money desks call these, and the twin
## suites pin them (game/tests/lanes/test_money_desks.gd ·
## unity/Runway.Core.Tests/Lanes/MoneyDesksTests.cs).
##
## THE BOOK IS THE LEVER (DECISIONS: spend = THE ORG LEDGER): each org
## bucket's engine value = the SUM of its lines' LIVE spend, and the engine's
## own budget math is untouched — this file only keeps state.budgets equal to
## that sum. The generated `amt` is a SUGGESTION (coordinator ruling): levers
## start at 0 and the player ADOPTS a suggestion through the receipt path.
## Never auto-seeded — week-1 economics match a tree without this file.
##
## Line schema (durable dictionary rows on state.spend_book):
##   name / buys / amt / bucket / contract_notice / division — the spine's.
##   live    (int, key absent until the desk touches the line) — the REAL $/wk.
##   stop_wk (int, key absent until a contract line is stopped) — the notice
##           clock; the line keeps billing until the notice runs out
##           (obligations survive removal — the mutation law).
## The C# twin's SpendLine carries `live`/`stop_wk` as nullables, so BOTH
## engines write these keys only once a line is touched — byte-identical
## save keys either way.
##
## TWIN: unity/Assets/Scripts/Core/Lanes/SimSpendBook.cs — same order, same math.

const BUCKETS: Array[String] = ["sales", "care", "rnd", "office"]
## The section words the sheet prints — bucket → "CLOSING — sales" etc.
const BUCKET_WORDS := {"sales": "closing — sales", "care": "retention — care",
	"rnd": "building — r&d", "office": "people — office"}
## The add door closes here (birth writes at most 10 rows).
const BOOK_CAP := 12

# ═══════════════════════════ the book itself ═════════════════════════════════

## The bare four-line book — world_gen's own default, duplicated here so an
## old save that predates the birth book still opens a playable sheet.
static func bare_book() -> Array:
	return [
		{"name": "sales", "buys": "closing what is already in the pipe", "amt": 0, "bucket": "sales", "contract_notice": 0, "division": ""},
		{"name": "care", "buys": "keeping the customers we have", "amt": 0, "bucket": "care", "contract_notice": 0, "division": ""},
		{"name": "r&d", "buys": "building the thing", "amt": 0, "bucket": "rnd", "contract_notice": 0, "division": ""},
		{"name": "office", "buys": "the room and the people in it", "amt": 0, "bucket": "office", "contract_notice": 0, "division": ""},
	]

static func ensure_book(state: GameState) -> void:
	if state.spend_book.is_empty():
		state.spend_book = bare_book()

## The line's REAL weekly spend. The key is absent until the desk touches it.
static func live_of(line: Dictionary) -> int:
	return int(line.get("live", 0))

## A contract line the player stopped: it bills through its notice.
static func is_stopping(line: Dictionary) -> bool:
	return line.has("stop_wk")

## The indices of a bucket's lines, in book order.
static func lines_of(state: GameState, bucket: String) -> Array:
	var out: Array = []
	for i in state.spend_book.size():
		if String((state.spend_book[i] as Dictionary).get("bucket", "office")) == bucket:
			out.append(i)
	return out

static func bucket_live(state: GameState, bucket: String) -> int:
	var total := 0
	for i in lines_of(state, bucket):
		total += live_of(state.spend_book[int(i)])
	return total

static func bucket_suggested(state: GameState, bucket: String) -> int:
	var total := 0
	for i in lines_of(state, bucket):
		total += int((state.spend_book[int(i)] as Dictionary).get("amt", 0))
	return total

static func book_live(state: GameState) -> int:
	var total := 0
	for b in BUCKETS:
		total += bucket_live(state, b)
	return total

static func book_suggested(state: GameState) -> int:
	var total := 0
	for b in BUCKETS:
		total += bucket_suggested(state, b)
	return total

# ═══════════════════════ the one write-back law ══════════════════════════════

## THE SUM IS THE LEVER. Called after every mutation (and at the top of the
## spend desk's draw): state.budgets[bucket] := Σ live of that bucket's lines.
##
## THE LEGACY ABSORB, once: a pre-book save where the old ledger set the org
## levers has budgets > 0 and a book with no `live` keys at all. The levers
## are the truth there — they land on the FIRST line of their bucket (a bare
## line is created if the bucket has none), so the book and the levers agree
## without inventing a dollar. A fresh generated book (no live keys, levers 0)
## is left unstamped: suggestions stay suggestions and the levers stay 0.
## Returns true when anything changed.
static func reconcile(state: GameState) -> bool:
	ensure_book(state)
	var changed := false
	var any_live := false
	for l in state.spend_book:
		if (l as Dictionary).has("live"):
			any_live = true
			break
	if not any_live:
		var org := 0
		for b in BUCKETS:
			org += int(state.budgets.get(b, 0))
		if org > 0:
			for b2 in BUCKETS:
				var idxs := lines_of(state, b2)
				if idxs.is_empty():
					state.spend_book.append({"name": b2, "buys": "", "amt": 0,
						"bucket": b2, "contract_notice": 0, "division": "", "live": 0})
					idxs = lines_of(state, b2)
				var first: Dictionary = state.spend_book[int(idxs[0])]
				first["live"] = int(state.budgets.get(b2, 0))
			for l2 in state.spend_book:
				var ld: Dictionary = l2
				if not ld.has("live"):
					ld["live"] = 0
			changed = true
	for b3 in BUCKETS:
		var want := bucket_live(state, b3)
		if int(state.budgets.get(b3, 0)) != want:
			state.budgets[b3] = want
			changed = true
	return changed

# ═══════════════════════════ the line steppers ═══════════════════════════════

## The per-line quantum: small lines move in small steps.
static func step_q(amt: int) -> int:
	if amt < 200:
		return 20
	if amt < 1000:
		return 50
	if amt < 2000:
		return 100
	return 250

## One press of a line's − or +. Down floors at $0; up is REFUSED when the
## bucket would pass the era's spend ceiling (the same cap the old ledger's
## org levers obeyed — the desk prints why). Returns the line's live after.
static func adjust_live(state: GameState, i: int, dir: int) -> int:
	if i < 0 or i >= state.spend_book.size():
		return 0
	var line: Dictionary = state.spend_book[i]
	if is_stopping(line):
		return live_of(line)
	var cur := live_of(line)
	var next := cur
	if dir < 0:
		next = maxi(cur - step_q(cur), 0)
	else:
		next = cur + step_q(cur)
		var cap := SimEngine.era_spend_cap(state.era)
		if bucket_live(state, String(line.get("bucket", "office"))) - cur + next > cap:
			return cur
	line["live"] = next
	reconcile(state)
	return next

## Whether one more + on this line would be refused by the era ceiling.
static func at_cap(state: GameState, i: int) -> bool:
	if i < 0 or i >= state.spend_book.size():
		return true
	var line: Dictionary = state.spend_book[i]
	var cur := live_of(line)
	var cap := SimEngine.era_spend_cap(state.era)
	return bucket_live(state, String(line.get("bucket", "office"))) - cur + (cur + step_q(cur)) > cap

# ═══════════════════════ adopt — the suggestion path ═════════════════════════

## ADOPT one suggested line (coordinator ruling): live := amt, clamped so the
## bucket never passes the era ceiling. The desk fires this behind the
## receipt + two-tap. Returns the line's live after.
static func adopt_line(state: GameState, i: int) -> int:
	if i < 0 or i >= state.spend_book.size():
		return 0
	var line: Dictionary = state.spend_book[i]
	if is_stopping(line):
		return live_of(line)
	var sugg := int(line.get("amt", 0))
	if sugg <= 0:
		return live_of(line)
	var cap := SimEngine.era_spend_cap(state.era)
	var room := cap - (bucket_live(state, String(line.get("bucket", "office"))) - live_of(line))
	line["live"] = clampi(sugg, 0, maxi(room, 0))
	reconcile(state)
	return live_of(line)

## ADOPT the whole suggested book — one arm at the sheet top. Returns the
## book's live total after.
static func adopt_book(state: GameState) -> int:
	for i in state.spend_book.size():
		adopt_line(state, i)
	return book_live(state)

# ═══════════════════════ add and stop — the mutation law ═════════════════════

## ADD a line into a bucket (free to add — it bills only when raised).
## Returns the new index, or -1 when the book is full or the bucket unknown.
static func add_line(state: GameState, bucket: String) -> int:
	ensure_book(state)
	if not BUCKETS.has(bucket) or state.spend_book.size() >= BOOK_CAP:
		return -1
	state.spend_book.append({"name": "a new line", "buys": "name it with a written move",
		"amt": 0, "bucket": bucket, "contract_notice": 0, "division": "", "live": 0})
	return state.spend_book.size() - 1

## STOP a line. No notice → removed now ("stopped"). A contract line starts
## its notice clock instead ("notice") and keeps billing until it runs out —
## obligations survive removal. Idempotent on an already-stopping line.
static func stop_line(state: GameState, i: int, week: int) -> String:
	if i < 0 or i >= state.spend_book.size():
		return ""
	var line: Dictionary = state.spend_book[i]
	var notice := int(line.get("contract_notice", 0))
	if notice <= 0:
		state.spend_book.remove_at(i)
		reconcile(state)
		return "stopped"
	if not line.has("stop_wk"):
		line["stop_wk"] = week
	return "notice"

## Weeks a stopping line still bills. -1 = the line is not stopping.
static func notice_left(line: Dictionary, week: int) -> int:
	if not line.has("stop_wk"):
		return -1
	return maxi(int(line.get("contract_notice", 0)) - (week - int(line.get("stop_wk", week))), 0)

## Drop every stopping line whose notice ran out (the desk sweeps at draw —
## deterministic in both engines). Returns how many closed.
static func sweep_lapsed(state: GameState, week: int) -> int:
	var kept: Array = []
	var dropped := 0
	for l in state.spend_book:
		var ld: Dictionary = l
		if ld.has("stop_wk") and notice_left(ld, week) <= 0:
			dropped += 1
		else:
			kept.append(ld)
	if dropped > 0:
		state.spend_book = kept
		reconcile(state)
	return dropped

# ═══════════════ shared display reads for the money desks ════════════════════

## TEAM's ladder rung — deterministic counts (DECISIONS): ≤9 flat person
## rows · 10–40 function groups · beyond that, business units.
static func team_rung(n: int) -> int:
	if n <= 9:
		return 1
	if n <= 40:
		return 2
	return 3

## The ESOP vesting fraction at `week` for a grant that started vesting at
## `vest_start_wk`: 208-week vest, 52-week cliff (DECISIONS — the fallback
## formula the team desk renders until the ownership lane's getter lands).
static func vested_frac(week: int, vest_start_wk: int) -> float:
	var weeks_in := maxi(week - vest_start_wk, 0)
	if weeks_in < 52:
		return 0.0
	return minf(float(weeks_in) / 208.0, 1.0)

## The grant on a person, matched by name (grants carry emp_id; employees'
## only stable identity today is their name). {} = no grant.
static func grant_for(state: GameState, emp_name: String) -> Dictionary:
	for g in state.esop.get("granted", []):
		if String((g as Dictionary).get("emp_id", "")) == emp_name:
			return g
	return {}
