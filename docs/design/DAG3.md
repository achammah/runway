# DAG 3 — the binder UX plan, implemented

Authority: `13-binder-ux.md` (the plan; owner-approved verbatim) +
`DECISIONS.md` + the standing laws. Twin law, exit-code gates, windowed
shot harnesses, own-test-files rule — all as DAG 2 ran them.

## Wave A — THE KIT SPINE (one agent; owns ALL shared files this wave)

Files: game/src/ui/components.gd, binder.gd, garage_view_screen.gd,
game/src/core/sim_engine.gd + game_state.gd + save_system.gd (one new
field), Unity twins (DeskKit.V2.cs, BinderScreen.cs, GarageScreen.cs,
SimEngine.cs, GameState.cs), kit/tutorial probes.

Delivers, per the plan's Part 1:
1. `zero_state(...)` component (S1) — will-show + would-line + one
   action + wakes-hint, in the kit's paper grammar.
2. `ask_strip(b, desk_id)` (S2a) — the generalized spend pattern:
   this desk's attention rows + verb, red ink, one line.
3. **focus_control** (S2b): desks register control rects during draw
   (`b.mark_control(id, rect)`); `focus_desk(desk, control_id)` lands
   with a 2s coach-style spotlight on the control. Attention rows gain
   an optional `control` key (engine pass-through).
4. `do_lane(b, actions[{label, cb, tier}])` (S3) — one slot, one
   grammar, above the teaching foot.
5. `press_receipt(b, target, title, lines)` (S4) — the popover.
6. The delta layer (S5): `delta_arrow`, `pen_circle`, and the per-run
   seen-store (user://binder_seen_<seed>, the events read-marks
   precedent) with a changed(desk, key, value) API.
7. `fit_line` / `fit_par` (S6) + migrate the kit's own generated-text
   sites.
8. Keyboard + back stack (S7): 1-4 groups, arrows walk pages, Enter
   arms focus, Esc law untouched; back pills on cross-desk jumps;
   breadcrumbs for rung drills.
9. Era dim (S8): desks expose `is_dormant(state)` (default false),
   rail dims at 60% + the page wakes-hint slot.
10. Arm family styling (S9): one family, the tier said on the control.
11. Rail micro-status: desks expose `micro_status(state)` (default "");
    rail renders right-aligned per tab.
12. Quartet cards gain delta + ask lines (frame-owned).
13. Pre-roll items deep-link via focus_control; LOCK IN wears the
    outstanding count badge (garage screen).
14. ENGINE: `attention_ages` durable field (key -> first-seen week,
    maintained at tick, attached to rows as `since_wk`; stale keys
    dropped); `control` key pass-through. Save round-trip + twin pins.
15. The suggestion interface (for B-LOG): desks expose
    `suggestions(state) -> [{label, kind: prefill|jump, payload}]`
    (default []); the kit defines the shape, this-week consumes it.

## Wave B — SIX GROUP LANES (parallel; desk files disjoint)

Each lane: its tabs' Part-2 entries, using only Wave A primitives; own
test/probe files; shared-file changes returned as packages.
- B-REV: offers, customers, in motion (THE PREFILL), growth.
- B-COSTS1: spend (ask_strip refactor), team, recruitment.
- B-COSTS2: bills, the bank, the works (+lineup [arrange] entry).
- B-COMPANY1: what we make, cap table (THE VALUATION SLIDER), the
  raise.
- B-COMPANY2: the street, threats (focus_control + ages), pivot
  (KEEP/DIES two-column), the offer momentary.
- B-LOG: this week (THE WEEK'S CHIPS via the suggestion interface),
  history (light), events (inline DOs).

## Wave C — PROOF AND SHIP

Zero-state probe (19 desks at week 1) · delta probe (two-open diff) ·
press-map probe (every DO lane + popover) · full lockstep suites ·
smoke in band · one scripted live-play pass · PROJECT_LOG + memory ·
both DMGs recut, byte-scanned, stamps verified.
