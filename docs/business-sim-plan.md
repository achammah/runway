# RUNWAY! — the true business simulation plan

The target: every subsystem a real business has, playable from the binder,
on one repeating pattern:

> **Deterministic core** (engine-owned, seeded, twin-tested) →
> **LLM dressing** (batch calls that name/describe entities only when they
> exist — never decide numbers) →
> **A desk in the binder** (world-clamped controls, no free-money edits) →
> **Feeds** (the weekly P&L, the DM's context, the bang system).

The DM stays the storyteller; the engine stays the accountant. Nothing
below breaks that law.

---

## 1. THE CATALOG (offers & unit economics) — desk: `pricing`
**Engine**: offers carry itemized `cost_lines` (variable, per unit) and
`fixed_lines` (weekly overheads); `unit_cost = Σ variable` (≤90% of fair),
`fixed_wk = Σ fixed`; catalog overhead is a P&L lane; learning curve cuts
variable costs with cumulative volume; add/remove offers world-clamped;
new offers arrive unpriced (bill at going rate until priced).
**LLM**: from the founder's plain-words description → name, unit, fair
price, elasticity, weight, and the itemized cost list (variable + fixed).
Keyless fallback: audience-scaled defaults with generic lines.
**Desk**: per-offer expand → price stepper, every cost line on −/+
steppers (world-clamped per line), margin/unit, weekly contribution;
"+ sell something new" write-in → review card → confirm; drop (×) per
offer.
**Feeds**: pnl.offer_fixed lane; DM sees the catalog; bang on unpriced.

## 2. THE LABOR MARKET (HR) — desk: `crew`
**Engine (the owner's design)**: every role has a market salary (role ×
era). The founder opens roles with an offered salary (clamped stepper).
Weekly candidate arrivals are seeded-deterministic: 0–10 per open role,
drawn from an attractiveness score = offered/market salary + hype +
morale + era polish; candidates sit in an applicant pipeline and decay
(take other jobs in 2–3 weeks); skill 1–5 drives productivity; hiring
enters the existing onboarding pipeline; firing costs severance (2–4 wks
salary) + a morale hit; underpaid stars are poachable (see rivals).
**LLM**: ONE batch call per week, only on weeks with arrivals, dressing
all new candidates at once (names, quirks, one-liners). Keyless: seeded
name/quirk pools.
**Desk**: open/close role + salary stepper vs market line; applicant
cards (skill, ask, quirk) → hire; per-employee card (salary, burnout,
effect) → raise / let go; payroll total.
**Feeds**: payroll in P&L (exists); bang when applicants wait or someone
is about to walk; DM sees roster changes.

## 3. LIVING RIVALS — desk: `the street`
**Engine**: each rival = {strength, cash vigor, focus, price posture,
hype}. A seeded weekly action from a state-weighted table: price cut
(market fair-price pressure), feature launch (relative quality drag),
marketing blitz (adoption pressure), poach attempt (targets your best
underpaid employee — ties into HR), stumble/scandal (they weaken), rare
acquisition sniff (see M&A). All effects deterministic and receipted.
**LLM**: none weekly. BIG beats (launch, scandal, acquisition sniff) are
injected into the DM's event context so the story weaves them — riding
the existing adjudication call, zero extra calls.
**Desk**: the street becomes a weekly action log per rival + their
posture; threats tab keeps the danger read.
**Feeds**: adoption/churn/fair-price pressure into the tick; bang on a
poach attempt or a rival launch.

## 4. THE FUNNEL (marketing & sales channels) — desk: `customers`
**Engine**: the single marketing lever splits into a channel mix — paid
ads / content / referrals / outbound — each with its own CAC curve and
saturation; funnel made explicit: reach → leads → conversion (quality +
price + GTM capacity) → customers; analytics levels keep gating what you
SEE (fog of war stays).
**LLM**: none — pure math.
**Desk**: 4 channel sliders with live CAC per channel; funnel bars at
analytics ≥1; cohort retention read at analytics ≥2.
**Feeds**: replaces the blended marketing effect inside the tick; P&L
lever lanes split per channel.

## 5. ENTERPRISE PIPELINE (named leads) — desk: `customers` (Enterprise runs)
**Engine**: for Enterprise audiences, adds arrive as named accounts with
stages (meeting → pilot → contract), each stage a seeded weekly advance
probability, pushed by moves/sales capacity; a signed logo = many seats
(weight).
**LLM**: batch-name new leads when they spawn (one call, rare).
**Desk**: pipeline board with stage chips.
**Feeds**: Enterprise adds flow through stages instead of raw counts.

## 6. THE FINANCE DESK — desk: `the ledger`
**Engine**: loans get honest terms priced by health (rate = f(runway,
revenue, era) — replaces the flat 18%/wk for deliberate borrowing;
the story op keeps its shark rate), take/repay controls; break-even line
(customers needed at current prices); 4-week cash forecast at current
settings; net history alongside the customer sparkline. Simple profit
tax (~20%) on profitable weeks from `office` era up.
**LLM**: none.
**Desk**: borrow/repay steppers + terms preview; forecast strip; the
existing P&L block stays the centerpiece.
**Feeds**: interest lane in P&L; bang when a repayment cliff nears.

## 7. PRODUCT ROADMAP (feature bets) — desk: `product`
**Engine**: 2–3 candidate feature bets sit on the board; each = R&D weeks
+ a payoff distribution (quality, retention, new segment) resolved with
the house dice when shipped; tech debt drag visible; hardening sprint as
a standing bet.
**LLM**: feature card names/descriptions, generated once per era or when
a slot opens (rare batch call).
**Desk**: bet cards with cost/odds; commit R&D to one; ship ceremony in
the journal.
**Feeds**: R&D lever gains a target; DM narrates shipped bets.

## 8. MACRO & SEASONS — desk: `the street` (banner)
**Engine**: readable market cycles over the trend walk + rare shocks
(funding winter: valuations ×0.6, term sheets scarcer; boom: warmer).
Seeded, pre-announced one week ahead ("the street smells winter").
**LLM**: none; authored banner lines.
**Feeds**: valuation, adoption, term-sheet generation.

## 9. THE BOARD (post-raise) — desk: `cap table`
**Engine**: after a round closes, expectations exist: a growth target
every 12 weeks; miss → investor_pressure status + colder next round;
beat → warmth. An investor-update move improves warmth (DM narrates).
**LLM**: none new (board beats ride event context).
**Desk**: cap table shows the target and the countdown.
**Feeds**: statuses, warmth, valuation.

## 10. M&A / EXIT PATHS — journal events
**Engine**: rare, valuation-priced acquisition offers (from a strong
rival or a strategic) with a hard clock; acqui-hire floor when dying;
accept = run ends with that exit (score = your % × price).
**LLM**: the DM narrates the courtship (rides existing calls).
**Feeds**: the finale.

## 11. HARDWARE PRODUCTION (Hardware runs only)
**Engine**: capacity = base + ops staff + equipment assets; produce to
stock; stockouts cap adds; unsold stock carries carrying cost — the
Bonopoly loop, scoped to Hardware `biz_what`.
**Desk**: a production strip on `product`.
**Status**: candidate for a later wave — only if the owner wants it now.

---

## Sequencing (each wave lands complete in BOTH engines + tests before the next)
- **Wave A**: 1 Catalog (finish — engine half done) + 6 Finance desk.
- **Wave B**: 2 Labor market + the poach hook stub.
- **Wave C**: 3 Living rivals + 8 Macro (they share the street).
- **Wave D**: 4 Funnel + 5 Enterprise pipeline (they share customers).
- **Wave E**: 7 Roadmap + 9 Board + 10 M&A.
- **Wave F** (if wanted): 11 Hardware production.

Standing rules for every wave: engine first with twin-suite pins, then
Godot desk, then Unity desk, prompts byte-synced, bangs + coach lines
updated, compile/parse/suite gates before commit.
