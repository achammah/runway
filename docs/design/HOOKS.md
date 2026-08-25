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
class with `Draw(BinderScreen b)`; BinderScreen exposes the helpers desks
need (`b.L(...)`, `b.Content`, `b.State`, stepper/InkWord idioms) — the UI
spine (N1b) makes those public and plants the dispatch.

The UI spine also ships the SHARED COMPONENTS from 10-interface-language:
review card, world-clamped stepper, two-tap arm, teaching footer, card
grid, stage board, action log — in `game/src/ui/components.gd` (static
funcs) and `unity/.../Game/DeskKit.cs`. Lanes USE them, never fork them.

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
