# DAG 2 — implementing the binder rework + the new mechanics

Authority: `DECISIONS.md` (owner picks, binding) + `11-binder-rework.md`
+ `12-binder-rework-2.md` + `mockups/` (the pixels). Twin law: identical
math both engines, byte-identical save keys, prompts byte-twins
(cmp-verified). Durable state = FIELDS (Godot saves drop metas). LLM C#
assembly never sees Core types (payload-in/JObject-out). Every gate
checks EXIT CODES directly (`> /tmp/x.out 2>&1; ec=$?`), never piped
tails. Unity batch success marker: `RUNWAY! build Succeeded`.

## Scope in one paragraph

New binder frame (side rail, divider groups, alarm-red, tour, quartet
overviews, momentary tabs, arrange mode) + 18 desks per the corpus +
new engine subsystems: divisions/sites + price book + mutation-law ops;
the works unification (per-type capacity/ticket/relief, 3 rungs);
features/families pipeline behind WHAT WE MAKE; ownership cluster
(ESOP + instruments + raise + recruitment + buyout offers with
waterfall/powers); pivot (two axes); generated-at-birth content
(identity, topics, spend book, price book, feature list) + once-only
illustrations through the scene painter v2 path; DM/clarify prompt
extensions for all new ops. Then gates, QA screenshots, ship.

## Waves

### W1 — SPINES (parallel: 2 agents)

**ENGINE SPINE** owns: `game/src/core/game_state.gd`,
`game/src/core/sim_engine.gd` (hook seams only),
`game/src/core/lanes/sim_divisions.gd` (stub),
`game/src/core/lanes/sim_ownership.gd` (stub),
`game/src/core/lanes/sim_features.gd` (stub),
`game/src/core/lanes/sim_works.gd` (stub), matching C# twins under
`unity/Assets/Scripts/Core/`, twin-suite skeletons, save fixture
extension. Delivers: all new FIELDS (sites[], price_book{}, topics{},
spend_book[], esop{}, instruments[], raise{}, recruitment{},
features[], buyout_offer, founder_time_tax, offer.product_id,
employee.site, machine.site, budget-line division tags), salt-registry
extensions (new blocks: divisions 130s, ownership 120s, features 140s,
recruitment 150s, works 160s — extend the existing registry file, no
collisions), tick-v2 hook wiring for the four new lane modules
(tick_pre/tick_money/tick_post/directives/attention), pnl lane names
for new money flows, save round-trip green with old-save fixture.
Stubs return neutral values so the tree stays green.

**UI SPINE** owns: `game/src/ui/binder.gd` (frame rebuild),
`game/src/ui/components.gd` (DeskKit v2 — resume the parked work in
tree), new files `game/src/ui/desk_*.gd` STUBS for all 18 desks +
overview + tour + arrange + momentary shell, and the Unity twins
(`BinderScreen.cs`, `DeskKit.cs`, `Desk*.cs` stubs). Delivers: the
ring-binder frame (side rail, divider groups with colored index tabs,
closed-kraft/open-paper, counts on closed dividers, alarm-red tab
system climbing to dividers, ~0.3s open ease + 40ms page fan),
quartet/overview component, first-open tour (6 steps, once per
install, replayable), momentary-tab slot (gold, deadline clock),
arrange-mode shell (bins/chips/receipt), DeskKit v2 primitives:
ledger_sheet (sections/subtotal single rule/total double rule/green
amount band/row numbers/ADJUST column with separate − + buttons),
zone (numbered didactic header), wall column+card, ticket/receipt,
dilution bar, meter, capbars, hero plate, folder/hero-row, chips.
Every desk stub renders its hero question so navigation is testable.

### W2 — LANES (parallel: 7 agents; each owns ONLY its files; changes
needed in shared files are returned as exact FIND/REPLACE packages)

- **L-OWN**: `sim_ownership.gd`+cs (ESOP pool/grants/208-wk vesting/
  52-wk cliffs, leavers; instruments safe/note/priced/bridge +
  conversion at priced rounds + SAFE-stack math; investor interest
  score, raise stages, founder-time tax; waterfall executor;
  buyout-offer generator incl. fishy structures + powers checks from
  instrument fields; recruitment: roles/candidates/acceptance model,
  rival counters) + desks `desk_cap.gd`, `desk_raise.gd`,
  `desk_recruit.gd`, `desk_offer.gd` (+cs twins) + team vesting
  mini-bar package. Extends the board lane's term-sheet mechanics —
  does not fork them.
- **L-DIVWORKS**: `sim_divisions.gd`+cs (site records, open/close/
  edit ops with price-book costs, relocation/shipping, per-site
  demand weights + wage mult + learning counts, group-by
  aggregators, SHARED/HQ row, rung thresholds) + `sim_works.gd`+cs
  (per-type capacity: service hours from crew, software ceiling from
  care, hardware machines, marketplace supply proxy; unit ticket from
  catalog cost lines; relief valves incl. recruit-supply/burst;
  learning curves) + `desk_works.gd` (3 rungs, hero rows face,
  slice control) + arrange mode logic on the shell + mutation-law
  ops: refinance_note, fire_account, retire_product, stop-line
  notice periods.
- **L-MAKE**: `sim_features.gd`+cs (birth features from world gen;
  landed bets → features; family tags; keep-costs; solidity/creak
  taxes; per-unit impact; shelf candidates within price-book bands;
  promised-vs-measured) + `desk_make.gd` (the wall: 4 columns + LIVE
  band, rungs 2–3 lineup + shared plumbing).
- **L-REV**: desks offers (rate card + audience variants + define-
  offer door via cost-lines flow), customers (scoreboard variants),
  in motion (triptych: river×sources / hot list / stage board +
  collapse ladders), growth (market garden + generated topics +
  illustration slots + era cap + verdicts).
- **L-MONEY**: desks spend (org ledger on generated spend book),
  team (payroll ledger 3 rungs + asks + vesting bar slot), bills
  (flat/scaling + trend + memo), the bank (the meeting 4 zones +
  refinance row + BOOKS on ledger sheet).
- **L-COMPANY**: desks street, threats (command center + focus_desk),
  pivot (two doors + computed preview + typed arm + regeneration
  triggers) + THE LOG group (this week composer + armed list +
  LOCK IN seam, history ledger + momentary filings, events inbox) +
  overview quartet contents.
- **L-GEN**: world-gen prompt additions (identity, growth/spend/works
  topics, spend book, price book, birth features — one call, banded,
  engine-clamped) + adjudicator ops for every new mechanic (open_site,
  close_site, reassign_employee, move_machine, tag_offer, refinance,
  fire_account, retire_product, pivot_audience, pivot_product,
  pitch_investor, sign_instrument, send_offer/hire ops) + clarify
  "which roof?" rule + illustration generation calls through
  scene_director's v2 text-to-image path (once at run start + pivot;
  cached user://; drawn fallback; numbers never wait). Prompts are
  byte-twins: edit `game/data/prompts/*`, cmp-copy to Unity
  StreamingAssets.

### W3 — INTEGRATION (coordinator)
Apply lanes' shared-file packages; resolve collisions; compile gates
both engines; twin suites lockstep; old-save fixture; FULLRUN smoke.

### W4 — QA LOOP (1 agent + coordinator)
Screenshot sweep of all 18 desks × states × 3 audiences × rungs where
applicable; the 15-check gate + laws (hero-covers test, no dollar in
prose, ≤2 coral stories, separate − + buttons, double-rule totals);
defect list → fixes → re-sweep until clean. Balance sanity pass on new
money flows (ESOP/raise/works don't break win rates grossly).

### W5 — SHIP
PROJECT_LOG entry, memory update, both DMGs rebuilt, byte-scan for the
6 known secrets, stamp verified inside.

## Standing constraints (verbatim)
Never `pkill` the owner's game; never touch `game/.env`; PROJECT_LOG
append-only; commit by explicit path, never `git add -A`; art PNGs
local-only; public repo — no secrets; test keys read from scratchpad
files, never printed/committed; prompts byte-twins via cmp.
