# RUNWAY! — The D&D-of-Startups Plan

The owner's frame, verbatim, is the design law:

> A D&D-style survival game. **The Pacing Clock**: each turn forces a rapid
> decision. **The DM as the World**: an indifferent, brutal environment throwing
> shifting crises. **The Core Goal**: endurance — dwindling inventory, sanity,
> party health — not slaying a villain.

Mapped: runway = food, morale = sanity, crew = party health, the weekly tick =
the clock, the DM = the world. Winning is lasting (and exiting well); the world
grinds you down BY DEFAULT, so doing nothing is slow death.

Everything below synthesizes six business-sim repos and nine D&D/AI-DM repos
(research reports in the session log). The one architecture every serious repo
converged on, stated best by agentrpg: **"Server owns math, agents own story.
Never ask an agent to calculate damage."** Our engine owns every number; the DM
owns every sentence; a narrow typed schema is the only bridge.

---

## PILLAR A — THE DETERMINISTIC SIM ENGINE (`src/core/sim_engine.gd`)

The realistic economy, built from vetted formulas (sources: Ventiqra, TeamDay
business-tycoon, BSL system-dynamics library). All rates are per-week; every
externally-settable number passes a clamp; every subsystem rolls on its own
salted seeded stream so a run is replayable.

### A1. Stocks and the hostile weekly tick
- **Customers**: Bass adoption `new = p·P + i·c·A·P/N` (P = remaining market,
  A = customers, N = TAM) — organic S-curve, saturation walls; hype multiplies
  the advertising term `p`.
- **Churn**: `customers / residence_weeks`, residence driven by product quality
  through a Janoschek lookup — the product meter becomes a survival mechanic.
- **Revenue**: `customers × ARPU × price_mult`; **pricing elasticity**
  `(price/baseline)^-1.5` clamped [0.1, 3.0].
- **Marketing**: saturating conversion `rate = cap·(1 − e^(−budget/sat))`,
  CAC derived and narratable.
- **Burn**: rent (era) + payroll (ALL hires, including the onboarding pipeline
  below) + infra ($ + ¢/customer) + marketing budget.
- **Hiring pipeline**: DelayN(2) — new hires are paid immediately, productive
  after the onboarding delay; attrition is the pipeline's outflow.
- **Morale**: drifts toward a floor; **burnout cliff** below 30 (output
  multiplier + weekly resignation roll — your best people leave first, ESOP
  measurably retains).
- **Fatigue**: crunch accumulates with a lag (`Smooth`), erodes productivity
  via Janoschek — sustained overtime silently taxes everything.
- **Tech debt**: ship-fast accrues debt; debt raises outage risk
  (`(debt−40)/100`) and drags velocity; refactor is a costed, writable move.
- **Hype**: asymmetric smooth — climbs on a 4-week constant, crashes on a
  1-week one.
- **Market**: TAM grows slowly; trend random-walks ±2%/wk clamped [0.5, 1.5];
  the TAM ceiling actually multiplies acquisition (Ventiqra computed this and
  forgot to wire it; we wire it).
- **Rivals**: 2-3 NAMED competitors, strength ratchets up, occasional launches;
  aggregate pressure scalar multiplies acquisition. Rivals have stat blocks
  (Pillar C).

### A2. The funding module (Ventiqra wholesale, correct math)
- Valuation `= max(cash, ARR × multiple)` where the multiple is
  growth-sensitive (`10 + min(10, growth×50)` for product companies — "your
  multiple is your growth story").
- Post-money equity `= amount/(pre+amount)`; founder_pct compounds through
  rounds; hard guard below 10%.
- **Term-sheet generation**: 3 deterministic offers per raise attempt, asked
  equity = fair × uniform(1.15, 1.60), named investors from the world bible,
  desperation-priced (broke founders get shark terms). Negotiate = a roll.
- **Bridge loan**: 18%/week compounding, the doom spiral instrument.

### A3. Buff slots and commitments (the DM's persistence layer)
Typed expiring modifiers `{name, weeks_left, magnitudes}` written by DM ops,
read by the tick through getters; plus recurring deltas with `duration_weeks`
(a lease, a salary cut, a referral program). The HUD lists active buffs.

### A4. World-gen Theta (run start, from the pitch)
One LLM call emits INTUITIVE quantities — "market of ~200k buyers, customer
lifetime ~40 weeks, virality R0 0.8, rival strength strong" — and the engine
converts to rates and clamps everything (the SIR `R0/period` trick: LLMs are
reliable at intuitive numbers, unreliable at coefficients). Plus a difficulty
vector and an archetype bonus vector from WHAT×WHO (service bills hours…).

### A5. Tracked levers (what a founder can WRITE about and the engine holds)
price · marketing budget + channel split · hiring plan (weekly named candidate
slates: role, skill, salary, fee, accept-probability) · quality-vs-speed ·
ESOP pool · refactor · loan · analytics level (fog of war: the weekly report's
precision matches what you've invested in analytics).

### A6. Derived signals (computed each week, fed to the DM)
runway weeks · health band · bass phase (pre/post inflection) · churn driver ·
CAC · staffing-balance gap · fatigue level · debt multiplier · rival pressure ·
deadline countdowns. Gold for narration, and the input to DC-setting.

### A7. Verification
`tests/sim_engine_test.gd` (hermetic unit checks per subsystem) plus
`tools/balance_sim.py` — headless N-week runs under scripted founder
strategies (build-heavy / sales-heavy / balanced / reckless), printing
week-10/25/50 tables before any constant ships.

### A8. World demography (opendnd/Dominia, adapted)
Market generation with real math: segment TAM from clamped normal draws off a
size table; competitor density `= TAM / (supportValue × prosperity / advantage)`
where "advantage" = the market has a resource your category exploits; market
cycle as a prosperity enum multiplying everything; per-run inventory ("this
scene: 3 seed funds, 1 tier-1 VC, 2 tech journalists"). Startup/fund/product
NAMES from a seeded Markov chain (Nomina's `count^1.3` weighting) trained on
curated startup/VC name lists.

## PILLAR B — THE D&D LAYER

- **B1. Rolls & DCs**: client-side seeded d20 (consensus of every repo — no
  LLM ever rolls). DC floor table by move class (routine 6-8 / solid 9-11 /
  bold 12-14 / wild 15-16), LLM adjusts within bounds; margin → outcome tier
  (crit-fail / fail / mixed / success / crit) forced into the narration frame.
- **B2. Advantage/disadvantage**: granted DETERMINISTICALLY by state — the
  right specialist hire, item, or passive gives advantage on matching stats;
  burnout gives disadvantage on grit; heavy debt on build. Rolled as 2d20.
- **B3. Conditions**: implemented as quest-bound's deferred turn callbacks —
  `{name, weeks_remaining, magnitudes, fires_on}` with captured scope and the
  starts-NEXT-week edge case solved; plus a graded **exhaustion track 0-6**
  (opendnd) as the burnout meter with escalating penalties. Typed effects with
  teeth — `burnt_out` (disadvantage grit, morale floor down), `investor_
  pressure` (weekly hype tax until addressed), `press_darling` (advantage
  sell, decays), `crunch` (velocity now, fatigue later). Shown as margin
  doodles on the journal page.
- **B4. Founder sheet v2**: competences as ability scores; XP at milestones
  grants +1 stat picks (leveling); items and named passives per specialist
  hire (legal +15% deal value…); trait tally per move (DM tags 1-3 from a
  14-trait enum) → end-of-run **Founder Archetype** epilogue with satirical
  strengths/growth-areas.
- **B5. NPC stat blocks** (opendnd Personae adapted): investors and rivals as
  typed records `{name, archetype, alignment_coords, traits×2, thesis (keyed
  by alignment axis), bond, flaw, disposition, secret (gm_only),
  tactics_by_state}` — the 5×5 alignment plane (Founder-friendly↔Predatory ×
  Contrarian↔Momentum) gives NUMERIC coords, so investor-founder compatibility
  is a dot product that modifies the DC of every raise check against that
  investor. Behavior tables keep voice stable across stateless calls; rivals
  at low strength go desperate (Dynastia-style dice-gated history generates
  their backstory). Social currencies on the founder sheet: influence,
  credibility, goodwill, press_heat. Theory-of-mind: NPCs only know what they
  plausibly know.
- **B6. Ticking clocks**: `{deadline_week, consequence}` stored in state,
  fired DETERMINISTICALLY when the week arrives, countdown fed to every DM
  call. The survival pacing instrument ("the status quo is unstable —
  standing still has cost").
- **B7. Typed milestones**: engine-owned objective counters (launched, first
  revenue, pmf, rounds) advanced by engine events; a milestone flip triggers
  the big NARRATOR beat; routine weeks stay terse.

## PILLAR C — THE DM CONTRACT (prompt architecture v2)

- **C1. Context sandwich** (convergent across all repos): system prompt →
  world bible (run-start: market, 3 named investors, 2 rivals, each with a
  secret the client stores but never renders) → ≤500-word compacted memory
  (DM-refreshed each call, engine-enforced cap) → last 3 weeks verbatim →
  numeric state + derived signals → **directives block** (deterministic,
  prescriptive, computed from state: "Runway ≤3 weeks: the world MUST
  escalate"; "Rival launched: the market reacts"; "Milestone pmf flipped:
  narrate the beat").
- **C2. Response schema**: narration (outcome-tier forced framing + per-stat
  flavor hints) · roll{stat,dc} · effects[{op,v,why}] EXTENDED with lever ops
  (set_price, set_budget…), buff ops (install {name,weeks,magnitudes}), clock
  ops, condition ops · traits[] · scene · memory_update (the compacted spine).
- **C3. Sentinel linter** (deterministic post-check): unknown NPC referenced,
  effect/metric contradiction, "unexplained knowledge" → one retry with the
  error echoed, then proceed (never deadlock a week).
- **C4. Premise guard** (compare-and-swap): ops carry the base value they
  were computed against; mismatch with actual state → reject and retry once.

## PILLAR D — CEREMONY & PRESENTATION

- **D1. Dice videos** (IN FLIGHT): 20 pre-rendered 1080p seedance clips (cup
  slams → lifts → decorated d20 tumbles → settles on N), spritesheeted,
  played by DiceRoll; engine picks the number. Pilot verified beautiful.
- **D2. Advantage variant**: tint/frame treatment + two-dice clip later.
- **D3. Journal**: character-sheet spread (stats, conditions, passives, XP),
  clock stamps counting down, condition doodles in margins, receipts with
  whys (built), trait marks.
- **D4. Scenes**: already fully generative with trade staging + inked status
  surfaces; NPC stat blocks now feed scene casting.

## THE NIGHT'S EXECUTION ORDER

1. `sim_engine.gd` core tick + unit tests + `balance_sim.py` calibration
2. Theta world-gen + world bible (investors/rivals with secrets) at run start
3. Extended op vocabulary + executor (buffs, clocks, conditions, levers,
   recurring) + journal receipts for all of it
4. DC floor table, margin tiers, advantage/disadvantage, conditions
5. DM prompt v2: sandwich + directives + forced framing + traits + memory_update
6. Sentinel + premise guard + retry hardening
7. Typed milestones + arc beats on flips
8. Founder sheet v2: XP/levels, passives, archetype epilogue
9. NPC stat blocks + behavior tables + theory-of-mind gating
10. Dice sheets wired when the batch lands; ceremony filmed
11. Soaks: keyless 75-week, live multi-week, balance tables re-run
12. Journal UI: sheet spread, clocks, condition doodles; full QA film pass

Cost note for the night: a handful of live probe runs (~$5-10 in DM calls and
scene renders) plus the dice batch already running (~$8 at 1080p).
