# The hook architecture — how nine lanes plug in without touching shared files

The engine spine (N1a) plants every stub and call site below. Lanes fill
their own files ONLY. File-existence is guaranteed before N2 starts.

## Lane modules (engine logic)

Godot — one static class per lane, planted as a stub:
```
game/src/core/lanes/sim_catalog.gd      class_name SimCatalog
game/src/core/lanes/sim_labor.gd        class_name SimLabor
game/src/core/lanes/sim_street.gd       class_name SimStreet      (rivals+macro)
game/src/core/lanes/sim_funnel.gd       class_name SimFunnel
game/src/core/lanes/sim_pipeline.gd     class_name SimPipeline
game/src/core/lanes/sim_bank.gd         class_name SimBank        (finance)
game/src/core/lanes/sim_roadmap.gd      class_name SimRoadmap
game/src/core/lanes/sim_board.gd        class_name SimBoard       (board+M&A)
game/src/core/lanes/sim_factory.gd      class_name SimFactory     (hardware)
```
Unity twins: `unity/Assets/Scripts/Core/Lanes/Sim<Name>.cs`
(namespace Runway.Core, public static class).

Each stub exposes exactly these entry points (no-op until the lane fills
them; the spine calls them at the tick positions fixed in 00-spine.md §tick):
```
static func tick_pre(state, rep)      # before adoption (street, factory produce, labor arrivals…)
static func tick_money(state, rep, m) # money-section hook; m = the working money dict/struct
                                      # (fields per 00-spine: revenue, burn parts, pnl refs)
static func tick_post(state, rep)     # after money (board review, M&A, bets ship checks…)
static func directives(state) -> Array[String]   # DM context lines (spine caps total)
static func attention(state) -> Array            # rows {desk, key, severity, label}
```
A lane uses the subset it needs; unused stay no-op. The SPINE aggregates
attention() into `attention_items(state)` and directives() into
`_state_lines`/`Directives` in the fixed section order — lanes never touch
event_generator or the aggregator.

## Desks (binder UI)

Godot — one static desk helper per lane, planted as a stub; `binder.gd`
dispatches its tab body to the desk and passes itself:
```
game/src/ui/desks/desk_catalog.gd   class_name DeskCatalog   static func draw(b) / handle(b, id)
game/src/ui/desks/desk_crew.gd      class_name DeskCrew
game/src/ui/desks/desk_street.gd    class_name DeskStreet
game/src/ui/desks/desk_customers.gd class_name DeskCustomers (funnel + pipeline share; funnel owns
                                     the file, pipeline gets desk_pipeline.gd drawn inside it via
                                     DeskPipeline.draw_board(b) — planted call)
game/src/ui/desks/desk_pipeline.gd  class_name DeskPipeline
game/src/ui/desks/desk_bank.gd      class_name DeskBank      (the 10th tab)
game/src/ui/desks/desk_product.gd   class_name DeskProduct   (roadmap owns; factory gets
                                     DeskFactory.draw_bench(b) — planted call)
game/src/ui/desks/desk_factory.gd   class_name DeskFactory
game/src/ui/desks/desk_cap.gd       class_name DeskCap       (board+M&A additions)
```
Unity: `unity/Assets/Scripts/Game/Desks/Desk<Name>.cs` — public static
class with `Draw(BinderScreen b)`.

**The tab row is TEN tabs at pitch 120** (`vitals · the ledger · the bank ·
pricing · customers · product · crew · cap table · the street · threats`).
Tab index, bang position and pen ring all derive from `TAB_X0 / TAB_PITCH /
TAB_W` (Godot) and `TabX0 / TabPitch / TabW` (Unity) — never re-type a pitch.

**Today's pages are already inside the desk files**, moved verbatim off the
binder: pricing, customers (fog-of-war), product, crew, cap table and the
street each render exactly what they rendered before. A lane REPLACES or
EXTENDS a working page; it never starts from blank paper. `the bank` holds a
spine-written placeholder (title + what is owed + the era gate + rules
footer) that 06 replaces wholesale, and nothing has been relocated off the
ledger yet — 06 moves the loan content and the ledger's line in one commit.

### What a desk may call

| Godot (`b` = Binder) | Unity (`b` = BinderScreen) |
|---|---|
| `b.state` | `b.State` |
| `b.pane()` · `b.font()` | `b.Content` |
| `b.label(text, pos, size, col, w)` | `b.L(text, x, y, size, col, w)` |
| `b.ink_btn(button)` | `GameUi.InkWord(b.Content, …)` |
| `b.icon(name, pos, side)` | `b.Icon(name, x, y, side)` |
| `b.spark(series, pos, size, col)` · `b.series(key)` | `b.Spark(key, x, y, w, h, col)` · `b.Series(key)` |
| `b.fmt(n)` | `GameUi.Money(n)` |
| `b.wrap_h(text, size, w)` | `BinderScreen.Height(label)` |
| `b.pie(slices, pos, side)` · `b.debt_jar(fill, pos, size)` | `DrawnChart.CapPie(…)` · `b.JarEdge(…)` |
| `b.refresh()` | `b.Refresh()` |
| `b.desk` (Dictionary) | `b.Desk` (Dictionary<string, object>) |
| `b.desk_press("crew", id)` | `b.DeskPress("crew", id)` |
| `b.focus_desk("the bank")` | `b.FocusDesk("the bank")` |

**Desk-local state lives in `b.desk` / `b.Desk`**: page mode, expanded row,
armed control. It is cleared on every tab change, never saved, and dies with
the node. Two keys are reserved and drive the shared Esc contract — Esc pops
`"armed"`, then `"mode"` (+ `"row"`), and only then closes the binder:
```
b.desk["mode"] = "detail"   b.desk["row"] = 2   b.desk["armed"] = "<control id>"
```

Two branch seams are planted so no lane edits another lane's file:
- `DeskPipeline.owns_page(b)` / `OwnsPage(b)` returns **false** while the
  board is a stub, so an Enterprise run keeps today's customer page. 05
  flips it to `biz_who == "Enterprise"` in the same commit that fills
  `draw_board` / `DrawBoard`.
- `DeskProduct` calls `draw_bench(b)` / `DrawBench(b)` on Hardware runs;
  09 fills `DeskFactory.draw_bench` at the ruled band (00-spine §11,
  y470–740). An empty stub simply draws nothing.

### The shared components (`DeskKit`)

`game/src/ui/components.gd` (`class_name DeskKit`, static funcs) and
`unity/Assets/Scripts/Game/DeskKit.cs` (`public static class DeskKit`). Every
one of these is 10-interface-language §2, already drawn. Lanes USE them,
never fork them. All coordinates and type sizes are constants on the kit
(`X_ID/X_VALUE/X_EFFECT/X_MINUS/X_PLUS`, `HERO/TITLE/ROW/STATUS/DETAIL/LAW`).

| component | Godot | Unity |
|---|---|---|
| desk head | `title(b, text)` · `hero(b, num, cap)` · `rule(b, y)` | `Title` · `HeroLine` · `Rule` |
| world-clamped stepper | `stepper(b, y, cfg)` + `ladder/at_min/at_max` | `Stepper(b, y, StepRow)` + `Ladder/AtMin/AtMax` |
| review card | `review(b, cfg)` | `Review(b, ReviewCard)` |
| two-tap arm | `arm(b, id, plain, armed, pos, on_fire)` | `Arm(b, id, plain, armed, x, y, onFire)` |
| signature beat | `sign_stroke(b, btn, on_done)` | `SignStroke(b, btn, text, x, y, onDone)` |
| word button | `word(b, text, pos, on_press)` | `Word(b, text, x, y, onPress)` |
| expand ▸ / back | `expand(b, pos, on_press)` · `back(b, text, on)` | `Expand` · `Back` |
| card grid + pips | `card(b, y, cfg)` · `pips(b, pos, n)` | `Card(b, y, CardRow)` · `Pips` |
| stage board | `board(b, y, columns, empty_line)` | `Board(b, y, Column[], emptyLine)` |
| action log | `log_block(b, y, cfg)` | `LogBlock(b, y, LogRow)` |
| teaching footer | `footer(b, {computed, rules, warning})` | `Footer(b, computed, rules, warning)` |
| bars / spark | `bars(b, pos, rows)` · `spark(b, series, …)` | `Bars(b, x, y, BarRow[])` · `Spark` |
| empty state / +N more | `empty(b, pos, fact, tell)` · `more(b, pos, n)` | `Empty(b, x, y, fact, tell)` · `More` |
| WAIT + keyless note | `wait(b, pos, phrase, on_cancel)` · `HOUSE_NOTE` | `Wait(b, x, y, phrase, onCancel)` · `HouseNote` |

Rules the kit already enforces, so a lane does not have to: every stepper
prints its live-effect string and dims at a bound with the reason beside it;
the arm carries the price in its coral caption and only one control on a pane
is armed; a confirm that changes the books plays the commit stroke before the
op fires; empty lists print their authored line; the WAIT line breathes on the
12fps clock and offers `cancel`.

**The expand mark and the pips are DRAWN, not typed.** The hand font carries
no geometric shapes (checked: U+25B8, U+25B2, U+25CF are all absent), so a
typed one arrives in a fallback face. Use `DeskKit.expand` / `DeskKit.pips`.

### The pre-roll review

`SimEngine.preroll_items(state)` / `PrerollItems` (severity ≥2) is the engine
half; the card is drawn by the journal's week-ahead spread in both engines
(`_preroll_card` / `PrerollCard`) at the dice gate, with two exits: **go fix
it** (closes the book, opens the binder on the loudest item's desk) and **roll
anyway**. Esc = go fix. A lane adds to that card ONLY by filing an attention
row from `attention(state)` — the desk name in the row is what "go fix it"
routes to, so it must be one of the ten tab names.

## Tests

Twin suites dispatch to per-lane files (planted, initially asserting 0):
```
game/tests/lanes/test_<lane>.gd   static func run(ok: Callable) -> void
unity/Runway.Core.Tests/Lanes/<Lane>Tests.cs  public static void Run(Action<bool,string> ok)
```
The main suites call all nine in order after the existing 82 checks.

## Save / state

New state lives in lane-owned fields planted by N1a on GameState (Godot:
typed vars with defaults; C#: [JsonProperty] with defaults) per each
spec's State section — the spine plants the FIELDS (so saves are stable
from one commit), lanes fill the LOGIC.

## Ops

The spine owns both executors and the op schema. New op cases
(`push_lead`, widened `cat`) are planted by N1a routing into lane calls:
`SimPipeline.push_lead(state, cat, v)` etc.

## Blocked?

If your lane cannot proceed without editing a shared file, STOP and
message the coordinator ("main") with the exact edit you need. The
coordinator arbitrates; shared files have exactly one writer per DAG node.
