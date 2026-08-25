# 02 — THE LABOR MARKET (desk: `crew`)

Wave B of docs/business-sim-plan.md §2. House pattern: engine owns every number,
DM owns every sentence. Salts claimed here: **30, 31, 32, 33, 34** (free — used
elsewhere: 4,5,6,7,9,77,88,91,93).

**Realism bar**: every mechanic carries a one-line *Real analogue* naming the labor-economics
dynamic it mirrors, honestly rescaled to weekly ticks; every deliberate simplification carries a
*Drops* line naming the reality it omits and why that is acceptable. LLM boundaries: §8.
**Teaching bar**: the desk names the real concepts out loud — market rate, fully-loaded cost,
attrition, severance norms, span of control — and every departure prints a receipt that says WHY
in numbers. **Depth bar**: HR complexity unlocks by era (§5) — a garage is a handshake, an HQ is
a system.

---

## 1. State model

### New GameState fields (both engines, default-safe for old saves)

Godot `game_state.gd`:
```gdscript
var open_roles: Array = []   # [{role, offered_salary, opened_week, seats}]
var applicants: Array = []   # [{name, role, skill, ask, quirk, one_liner, applied_week, source}]
var recruiters: int = 0      # 0..2, floor era up (see §5)
```

C# `GameState.cs`:
```csharp
[JsonProperty("open_roles")]  public List<OpenRole>  OpenRoles  = new List<OpenRole>();
[JsonProperty("applicants")]  public List<Applicant> Applicants = new List<Applicant>();
[JsonProperty("recruiters")]  public int Recruiters;

public sealed class OpenRole {
    [JsonProperty("role")]           public string Role = "engineer";
    [JsonProperty("offered_salary")] public int    OfferedSalary = 1200;
    [JsonProperty("opened_week")]    public int    OpenedWeek;
    [JsonProperty("seats")]          public int    Seats = 1;      // >1 only at hq (§5)
}
public sealed class Applicant {
    [JsonProperty("name")]         public string Name = "";
    [JsonProperty("role")]         public string Role = "engineer";
    [JsonProperty("skill")]        public int    Skill = 3;        // 1-5
    [JsonProperty("ask")]          public int    Ask  = 1200;      // $/wk, engine-decided
    [JsonProperty("quirk")]        public string Quirk = "";
    [JsonProperty("one_liner")]    public string OneLiner = "";
    [JsonProperty("applied_week")] public int    AppliedWeek;
    [JsonProperty("source")]       public string Source = "inbound";  // "inbound" | "referral"
}
```

### Extended existing records (additive; every reader uses `.get(k, default)` / field initializers — old saves load unchanged)

- `employees[i]` gains: `skill` (int, **default 3** — exact legacy parity, see §4),
  `hired_week` (int, default **-1** = unknown tenure), `wants_raise` (bool, default false),
  `asked_week` (int, default -1), `underpaid_since` (int, default -1 — the receipt's clock).
  C# `Employee`: `Skill=3; HiredWeek=-1; WantsRaise; AskedWeek=-1; UnderpaidSince=-1;`.
- `pipeline[i]` / `PipelineHire` gains: `skill` (default 3). Graduation (tick §3) copies it:
  `{"skill": int(g.get("skill", 3)), "hired_week": state.week}` onto the new employee.
- pnl gains lanes `severance` and `recruiting` (int, default 0). C# `Pnl`:
  `[JsonProperty("severance")] public int Severance; [JsonProperty("recruiting")] public int Recruiting;`.
- `source`: `"referral"` marks the morale-referral arrival (§2; DM color + the small ask discount).

### The market salary table — $/wk, role × era (engine constant, both engines)

Godot `SimEngine.ROLE_MARKET` / C# `SimEngine.RoleMarket`; accessor
`market_salary(role, era) -> int` / `MarketSalary(string role, string era)` (unknown role → engineer row).

| role       | garage | coworking | office | floor | hq   |
|------------|--------|-----------|--------|-------|------|
| engineer   | 1200   | 1500      | 2000   | 2600  | 3400 |
| sales      | 1000   | 1250      | 1650   | 2150  | 2800 |
| designer   | 1050   | 1300      | 1750   | 2250  | 2950 |
| ops        | 850    | 1050      | 1400   | 1850  | 2400 |
| support    | 700    | 900       | 1150   | 1500  | 1950 |
| manager    | 1450   | 1800      | 2400   | 3000  | 3900 |

Anchor: garage engineer 1200 = the existing `PipelineHire.Salary` default. Role strings are exact —
they satisfy the engine's existing `role.contains("engineer"/"sales")` checks. The manager row exists
for table completeness; the role is LOCKED until `floor` (§5).

*Real analogue:* occupational wage structure (BLS OES-style ordering: engineers > managers-adjacent >
designers > sales > ops > support) × the firm-size wage premium — larger/later-stage employers pay
~25-35% more per size class for the same occupation, which is the ~×1.3-per-era column step.
*Drops:* regional and experience wage distributions collapse into one number per role×era; the
within-role spread is carried by the skill-ask curve (§2), where the desk can show it. Benefits and
equity comp are not salary lines — benefits are modeled as the office-lever expectation (§5, office+),
equity lives in the cap-table subsystem.

### Era-gated pure helpers (single source for engine, desk and tests)

```
role_unlocked(role, era) -> bool      # engineer, sales: always; designer, ops, support: coworking+;
                                      # manager: floor+
severance_weeks(era, tenure_wk) -> int  # garage: 1 (handshake) · coworking: 2 flat ·
                                        # office/floor: 2 if <26wk, 3 if <78, else 4 · hq: 3/3/4
span_mult(state) -> float             # 1.0 below floor era; else clampf(capacity/nonmgr, 0.5, 1.0)
                                      # capacity = 5.0 * (1.0 + Σ(manager_skill)/3.0)   (§4/§5)
fair_pay(state, e) -> int             # market_salary(role, era) × SKILL_ASK[skill]
```

### World-clamps (every settable number)

- `offered_salary`: clamp to `[0.5×market, 2.0×market]` on open/step.
- employee `salary` (raise stepper): clamp to `[0.5×market, 2.5×market]`.
- `skill`: clampi 1..5. `ask`: clamp `[0.5×market, 2.5×market]`. `seats`: clampi 1..5 (1 below hq).
- `recruiters`: clampi 0..(era caps in §5).
- open roles: at most one row per role string; `open_roles.size() ≤ staff_cap() − employees.size() − pipeline.size()` (floor 0).

---

## 2. The arrival formula (tick step 3b, salt 30)

Per open role, in `open_roles` array order, one shared salt-30 stream per week:

```
market = market_salary(role, era)
ratio  = clampf(offered_salary / market, 0.0, 2.0)
if ratio < 0.8:  A = 0.0                      # THE FLOOR: nobody applies below 80% of market
else:            A = clampf(0.35 + 1.1*(ratio − 1.0)
                            + hype/250.0 + (morale − 50)/250.0
                            + 0.06*era_index(), 0.0, 1.0)
λ = A * 6.0
λ *= 4.0 / (4.0 + waiting_this_role)          # CROWDING: 4 already waiting → half rate
λ *= 0.5 if (week − opened_week) ≥ 8 else 1.0 # STALE ROLE: word gets around
λ *= 1.5 if has_status("talent_magnet") else 1.0
λ *= 1.0 + 0.75 * recruiters                  # PAID PIPELINE, floor era up (§5)
count = Σ over 10 draws of (r30.randf() < min(λ,10.0)/10.0)   # Binomial(10, λ/10): 0..10, mean λ
```

Reference points: market pay, hype 0, morale 50, garage → A=0.35, ~2/wk. Advert 1.3× → ~4/wk.
Advert 0.85× → ~1/wk. Hype adds up to +0.40, morale ±0.20, hq era +0.24.

*Real analogue, term by term:* posted-wage vacancy filling — higher posted wages draw more (and
better) applicants (directed-search evidence: Dal Bó–Finan–Rossi 2013); the 0.8× floor is the
**reservation wage** (McCall search theory: below it, workers do not apply at all); Binomial(10, λ/10)
≈ Poisson application arrivals per vacancy-week (JOLTS-style vacancy yield, capped for play);
crowding = matching congestion on one vacancy (Diamond–Mortensen–Pissarides) plus a finite local
candidate pool; the stale penalty = adverse inference from long-open postings (applicants read
"open 2 months" as a red flag); hype/morale/era = employer brand and firm-size visibility (the
Glassdoor effect, priced into application flow rather than wages).
*Drops:* no macro labor-supply cycle (a funding winter should loosen the talent market — §8 Macro
of the master plan may later multiply λ; acceptable to ship without because the hook is one factor);
candidates apply to exactly one role (no cross-role spillover — keeps the desk read one-line-per-role).

**Candidate stats** (salt 31 stream, drawn per candidate in creation order):
```
skill = weighted pick over {1:15%, 2:25%, 3:30%, 4:20%, 5:10%}
       # overpay attracts talent: if ratio ≥ 1.25, reroll a 1-2 result once
ask   = round_to_10( market * SKILL_ASK[skill] * r31.randf_range(0.90, 1.15) )
SKILL_ASK = {1: 0.70, 2: 0.85, 3: 1.00, 4: 1.25, 5: 1.60}
```
*Real analogue:* the reroll is wage-driven selection on quality (the second Dal Bó et al. finding:
higher offers improve the applicant POOL, not just its size). SKILL_ASK spans 0.70–1.60 ≈ 2.3×,
matching real within-occupation p10–p90 wage dispersion (~2–2.5×); the ±noise is idiosyncratic
reservation wages. *Drops:* skill is observed truthfully at application — real screening carries
assessment error (interview validity ~0.3–0.5). Dropped because the chosen tension is
price-vs-quality, not assessment gambling; an "interviews" wave could add a seeded reveal later.

Referral garnish: when morale ≥ 70 and count ≥ 1, the FIRST candidate of the week is
`source="referral"` and its ask is multiplied 0.95.
*Real analogue:* employee-referral channels (~a third of real hires) flow only from engaged staff,
and referred candidates accept slightly less for a known-good shop (compensating differential).

**Applicant decay** (salt 32): `waiting = week − applied_week`. Weeks 1-2 are safe (N=2 grace).
From waiting ≥ 3: weekly `p_gone = 0.20 + 0.06*skill` (skill 5 → 0.50). At waiting ≥ 5 they are
gone for certain (hard cap). Garage era: p_gone −0.05 (§5).
*Real analogue:* candidate off-market decay — recruiting's "the best are gone in ~10 days" curve,
scaled to weekly ticks: strong candidates hold competing offers, so decay is skill-weighted;
the hard cap is offer shelf-life. Departure receipts (§6 feeds) teach it:
`"%s stopped waiting on %s after %d wks (your advert: %.2f× market)"` — skill ≥ 4 also → `rep["events"]`.

---

## 3. Hire / reject / fire / raise flows (engine helpers, twin names)

- `open_role(state, role, offered) -> bool` / `OpenRole(...)` — clamps, refuses dup role, cap breach,
  or a role not yet unlocked (`role_unlocked`, §5); sets `opened_week = week`, `seats = 1`.
- `set_role_salary(state, role, offered)` — clamped re-step.
- `close_role(state, role)` — removes the row; its applicants stay (they decay out).
- `hire_applicant(state, idx) -> Dictionary` / `HireApplicant` — guards `employees.size() + pipeline.size() < staff_cap()`;
  appends `{name, role, salary: **ask**, weeks_in: 0, quirk, skill}` to the EXISTING 2-week pipeline
  (paid at once, productive after tick §3 graduates it — unchanged code path); removes the applicant;
  decrements `seats`, and at 0 **auto-closes the role** (one role = one seat below hq; §5). The advert
  is the magnet, the ask is the contract.
  *Real analogue:* posting wage vs bargained wage — directed search separates the advertised wage
  from the accepted one. *Drops:* the counter-offer round (bilateral Nash bargaining) — a one-press
  desk cannot host a negotiation subgame, and the ask already prices skill; acceptable.
  The 2-week paid-unproductive pipeline is the real onboarding ramp (months in reality) at the
  game's ludic compression — already engine law.
- `reject_applicant(state, idx)` — removes, no penalty.
- `fire_employee(state, idx) -> Dictionary` / `FireEmployee` — returns `{name, severance}`.
  `tenure = week − hired_week` (hired_week −1 → tenure 0). **Severance = salary × severance_weeks(era, tenure)**
  (garage 1 · coworking 2 · office/floor 2/3/4 by tenure · hq 3/3/4 — §5). Employee removed now;
  morale −8 (matches the staff-quit hit); severance accrues to `meta["severance_due"]` and is BOOKED
  next tick (§6) so the P&L identity holds. Desk shows the figure before confirming.
  *Real analogue:* severance norms — the "1-2 weeks per year of service" rule of thumb and statutory
  tenure bands, era-scaled from handshake to package; the −8 morale is layoff survivor syndrome
  (firings measurably depress the remaining team). *Drops:* no for-cause vs no-cause distinction —
  the world always charges (also the anti-exploit against fire-rehire cycling); see open question 2.
- `grant_raise(state, idx, new_salary) -> int` — clamp `[0.5×, 2.5×] market`; when new salary ≥ 0.95×fair_pay,
  clears `wants_raise` + `underpaid_since` and morale +2 (once per grant).
- `fair_pay(state, e)` — note era moves this: every era-up silently underpays veterans.
  *Real analogue:* pay compression — external market drift outruns internal pay until a correction.

**Raise / resignation ladder** (tick step 3b, salt 33), per employee, `ratio = salary / fair_pay`:
- first week `ratio < 0.85` → `underpaid_since = week` (the receipt's clock; cleared when fixed).
- `ratio < 0.60` → `wants_raise = true` immediately (deterministic; insulting pay never waits —
  equity theory: perceived inequity triggers immediate response).
- `0.60 ≤ ratio < 0.85` → weekly `p_ask = 0.15` (garage ×0.5; office+ +0.05 when benefits are
  unfunded — §5); on success `wants_raise = true`, `asked_week = week`.
- while `wants_raise` and still `ratio < 0.85`: weeks 1-2 after `asked_week` roll weekly
  `p_quit = 0.20 + 0.05*skill`; at `week − asked_week ≥ 3` the resignation is CERTAIN (deterministic edge).
  Quit: employee removed, morale −6, receipt →
  `"%s resigned: paid %.2f× market for %d weeks"` (weeks = week − underpaid_since) in `rep["events"]`.
  No severance (they left).
- paying to ≥ 0.85 (or the 0.95 grant threshold via the desk) clears the ladder.

*Real analogue:* the efficiency-wage quit function — quit rates rise as the relative wage w/w_market
falls (Krueger–Summers tradition); skill-weighting = better outside options. The 3-week certainty is
an ultimatum compression of "they are already interviewing", kept deterministic for twin-testability.
Review cycles synchronize asks at office+ (§5) the way real comp-review seasons do.

**Poachability hook** (interface only — the rivals designer owns the caller and the steal receipt):
```gdscript
static func poach_target(state: GameState) -> Dictionary
# {} when nobody qualifies. Qualifies: fair_pay/salary ≥ 1.25.
# Pick: highest skill, tie → biggest gap, tie → lowest index.
# Returns {"index", "name", "role", "skill", "salary", "fair", "gap_pct"}
```
```csharp
public sealed class PoachTarget { public int Index; public string Name; public string Role;
    public int Skill; public int Salary; public int Fair; public double GapPct; }
public static PoachTarget PoachTargetFor(GameState state)   // null when none
```
*Real analogue:* Lazear's raiding model — outside firms bid precisely for high-ability workers paid
below their marginal product; the ≥1.25 gap is the raider's margin.

---

## 4. Skill economics inside the existing tick

`skill` defaults to 3 everywhere, chosen so every formula below is EXACTLY today's math for
legacy saves and DM-conjured hires. From `floor` era, the four POSITIVE output channels are
multiplied by `span_mult(state)` (§5 — managers or the work drags):

- **sales → gtm_cap** (§8): replace `3.0 * sales_heads` with `span_mult × Σ over sales employees of (1.0 × skill)`
  (skill 3 ⇒ 3.0, exact parity; skill 5 closer = 5.0, skill 1 = 1.0).
- **engineers → product + debt** (§9 R&D block): quality gain becomes unconditional:
  `quality_gain = b_rnd/1200.0 + span_mult × Σ(eng_skill)/6.0` (skill-3 engineer = +0.5 product/wk,
  salt-77 remainder idiom unchanged); debt paydown becomes `b_rnd/1500.0 + span_mult × Σ(eng_skill) × 0.10`.
  The eng==0 debt-growth check in §5 of the tick is unchanged.
- **support → retention** (§8): effective care budget `b_care_eff = b_care + span_mult × 500.0 × Σ(support_skill)`
  feeds the existing `1 − 0.30(1 − e^(−b/1500))` curve (skill-3 support ≈ $1,500/wk of care; the 30% cap still caps).
- **designers → adoption polish** (§8): `design_mult = 1.0 + minf(0.03 × span_mult × Σ(designer_skill), 0.30)`
  multiplies both `p_eff` and `wom` (skill-3 designer = ×1.09).
- **ops → the unforeseen** (§9): `incident_cost` and a new standing liability's `cash_wk` are multiplied
  by `maxf(0.4, 1.0 − 0.08 × Σ(ops_skill))` (skill-3 ops = ×0.76; deliberately NOT span-damped —
  firefighting does not need a manager).
- **managers → nothing direct**: their entire output IS `span_mult` (§5).

Payroll (§9) already sums employees + pipeline — no change needed for new hires.

*Real analogue:* within-occupation productivity dispersion — top performers deliver ~2-5× the median
in complex work (Hunter–Schmidt–Judiesch: output SD 20-48% of mean), which is the 1..5 linear spread;
sales heads as closing capacity = quota-carrying rep math; support→churn is the service-profit chain;
designer polish = design quality→conversion evidence; ops = operational maturity cutting unplanned
cost; managers-as-multiplier = span-of-control doctrine (managers produce coordination, not output).
*Drops:* no per-pair team chemistry or skill growth on the job (training/learning curves) — flat
skill keeps the roster readable; a later wave could let the office lever train +1 skill slowly.

---

## 5. SCALING BY STAGE — what unlocks at which era (exact numbers)

The desk grows the way a real company's HR does: informal → priced → benefits → managed → systematized.
All gates read `era_index()` (garage 0 … hq 4) through the §1 helpers; nothing else branches.

| era | unlocked / changed | exact numbers |
|-----|--------------------|---------------|
| **garage** | founder + first scrappy hires. Roles: **engineer, sales only** (`role_unlocked`). Handshake exits. Loyal, cheap, no process. | severance = **1 wk** salary flat · raise-ask `p_ask ×0.5` (no pay benchmarking in a garage) · applicant decay `p_gone −0.05` (scrappy joiners hold fewer offers) · staff cap 2 (exists) |
| **coworking** | real salaries, first attrition. All five base roles unlock. | severance = **2 wks** flat · full ladder (`p_ask 0.15`, full decay) · market column steps ×~1.25 |
| **office** | benefits expectations + formal severance + raise cycles. | benefits: expected office lever = **$250 × (employees+pipeline)/wk**; if `budgets.office` < expected → `morale_wk −1.0` (in tick §4) and `p_ask +0.05`, receipt `"a real office expects benefits: office $%d vs $%d expected"` · severance = tenure-banded **2/3/4 wks** (<26wk / <78wk / 78+) · **review cycle**: every `week % 12 == 0`, ALL employees below 0.85×fair set `wants_raise` at once (shared `asked_week`) |
| **floor** | recruiters + managers. | role **manager** unlocks (market $3,000 floor / $3,900 hq) · **span of control**: `capacity = 5 × (1 + Σ(manager_skill)/3)`, `span_mult = clampf(capacity / non-manager headcount, 0.5, 1.0)` applied per §4; receipt when < 1: `"span of control: %d heads, %d manager(s) — the floor runs at %d%%"` · **recruiter retainer**: 0..**1**, **$1,500/wk**, λ ×1.75, booked to pnl lane `recruiting` |
| **hq** | full departments. | recruiters 0..**2** (λ ×2.5 at 2) · **multi-seat roles**: `seats` steppable 1..5, arrivals keep flowing until seats exhaust (one role row = a requisition batch) · severance floor rises: **3/3/4 wks** · desk groups the roster into departments with subtotals (`"ENGINEERING — 6 heads · $19,800/wk"`) |

*Real analogues:* garage = informal micro-firm employment (no benchmarking, handshake severance,
mission loyalty); office = the benefits expectation threshold (benefits ≈ 30% of comp at established
firms — the office lever IS the benefits budget, $250/head/wk ≈ a modest load) and synchronized comp
reviews (annual cycles, compressed to 12 weeks on the game's week-per-turn scale); floor = span of
control 5-8 direct reports (management doctrine; the founder manages the first 5 personally) and
recruiter economics (a contingency fee ~20% of first-year salary ≈ $1.5k/wk amortized); hq =
requisition-based hiring and department structure.
*Drops:* no middle-management hierarchy (managers of managers) — one layer suffices at a 40-head
cap; no per-role benefits menus — one lever teaches the concept without a benefits sub-screen.

---

## 6. Weekly-tick integration, report lines, P&L

**Order**: new step **3b — the labor market**, immediately after step 3 (pipeline graduation),
before step 4 (morale) — the market reads LAST week's morale/hype, which keeps it independent of
this week's drift. Sub-order inside 3b: (a) arrivals (salt 30/31), (b) applicant decay (salt 32),
(c) review cycle (deterministic, office+, `week % 12 == 0`) then raise asks + resignations (salt 33).
Benefits expectation (§5) is computed in 3b(c) from `budgets` (static within a tick) and its
`morale_wk −1.0` lands in step 4. Resignations land before §9, so a quitter is off this week's payroll.

**Money lanes** (§9): `due = int(meta.get("severance_due", 0))`; if due > 0: `burn += due`,
`pnl["severance"] = due`, receipt `"severance: $%s (%d wks × $%s — tenure %d wks)"`, meta zeroed.
Recruiters: `burn += 1500 × recruiters`, `pnl["recruiting"] = 1500 × recruiters`, line
`"recruiter on retainer: −$%s/wk"`. `pnl` net identity unchanged (both live inside burn).
`runway_weeks` adds `1500 × recruiters` to its burn estimate.

**Report lines** (`rep["lines"]` unless noted) — receipts name the concept and the why:
- `"%d applied for %s (advert $%s vs market $%s)"`
- `"%s stopped waiting on %s after %d wks (your advert: %.2f× market)"` (skill ≥ 4 also → `rep["events"]`)
- `"%s wants a raise: $%d now, market says $%d (%.2f×)"` → `rep["events"]`
- `"%s resigned: paid %.2f× market for %d weeks"` → `rep["events"]`
- `"review week: %d people compare their pay to the market"` (office+, cycle weeks)
- `"a real office expects benefits: office $%d vs $%d expected"` (office+, when unfunded)
- `"span of control: %d heads, %d manager(s) — the floor runs at %d%%"` (floor+, when < 100%)
- `"severance: $%s (%d wks × $%s — tenure %d wks)"` · `"recruiter on retainer: −$%s/wk"`
- `rep["applicants_new"] = N` (int) — the turn runner's trigger for the dressing call (§8).

**DM context** — Godot `EventGenerator._directives` + C# `EventGenerator.Directives` add:
- `"- Hiring: %s advertised at $%d (market $%d) — %d applicants waiting%s."`
  (suffix `", best asks $%d"` when any wait)
- `"- %s (%s) wants a raise; refusing much longer risks a resignation."`
- `"- The office expects benefits: the office lever is $%d for %d staff."` (when unfunded, office+)
C# plumbing: `RunSnapshot` gains `OpenRoleRow[] OpenRoles {Role, Offered, Market, Waiting, BestAsk}`
and `string[] RaiseWanters`; `CoreSnapshot` fills them. `to_digest()` gains
`"hiring": ["ENGINEER open at $1,400 — 3 waiting", "recruiter retained", ...]` in both engines.

---

## 7. The desk — crew tab v2 (Godot `binder.gd::_tab_crew`, Unity `BinderScreen.TabCrew`)

Crew becomes the third WRITING tab (update the BinderScreen header comment that says only
ledger + pricing write). Same coordinates both engines; content area 1160×760; buttons via
`_ink_btn` / DrawnUI twins at the ledger's x-positions (1000/1064). The desk TEACHES: every card
prints the concept name next to its number.

```
y=6    founder header + competences line                      (exists, unchanged)
y=110  ROSTER — cofounder rows as today; per-employee row (66px):
       [icon 10,y]  Name — role · $1,500/wk ($1,940 loaded) · sk ●●●●○ · burnout 32
         loaded = salary + (rent + infra + office lever) / headcount  — computed desk-side,
         headcount = 1 + employees + pipeline; the FULLY-LOADED COST, named in the footer
       wants_raise → coral "! wants $X (market $Y)" inline (PEN)
       [+10%](930,y)  [let go](1024,y)   — let-go button label carries the price:
       first press re-labels to "owe $3,000 severance — sure?" (PEN), second press fires
       hq only: rows grouped by role with department subtotal lines ("ENGINEERING — 6 heads · $19,800/wk")
       pipeline rows (dimmed, ONBOARDING) as today
y+=…   OPEN ROLES — per open role (56px):
       ENGINEER  advert $1,400/wk [−](830)[+](894) · market $1,200 · 3 waiting · [×](1064 close)
       hq: a seats stepper "seats 2" between market and waiting
       under-market advert prints the engine's own read: "0.80× market — expect silence" (PEN)
       floor+: one recruiter row: "recruiter on retainer $1,500/wk — pipeline ×1.75  [hire](1000)/[dismiss](1064)"
       (hq: [−/+] 0..2)
       one trailing line for unopened+unlocked roles: "+ open:  sales  designer  ops  support" (buttons;
       locked roles absent; hidden when the cap is full: "no desk left to open a role")
y+=…   APPLICANTS — per card (66px, at most 6 drawn, then "+N more wait behind these"):
       Mara Voss · ●●●●○ · asks $1,700/wk (market $1,200) · "negotiates via long silences" · waiting 2wk
       [hire](1000)  [pass](1064)   — hire disabled (0.4 alpha) when cap is full
bottom payroll total $X/wk · fully-loaded $Y/wk (left) · morale spark 560px (right)
footer  "the rules of this desk: the MARKET RATE is what the street pays for the role · a head costs
        more than a salary (fully-loaded) · pay under market and ATTRITION compounds · SEVERANCE is
        tenure-banded, and grows up with the company"   (20px, INK 0.5 — the ledger's footer idiom)
```

**Bang conditions** (binder `_bangs` gains `"crew"`, same coral "!" as pricing/ledger):
`applicants.size() > 0` OR any employee `wants_raise` OR any open role with
`week − opened_week ≥ 8` and 0 waiting (stale) OR `span_mult(state) < 1.0`. Skill renders as 5 dots
(●=filled); asks and severance always in the desk's own `_fmt` money hand.

---

## INTERFACE DELTA — every UI change this lane needs (assessable, standalone)

Every row lands in BOTH engines: binder.gd + BinderScreen.cs · garage_view_screen.gd + GarageScreen.cs ·
journal page twins. Positions are binder-content coordinates (1160×760 sheet area); buttons use the
existing ink-button style; coral = the house PEN color.

| # | surface | exists today? | CHANGE / ADD | exactly how (content, controls, position, states) | why the player needs it |
|---|---------|---------------|--------------|---------------------------------------------------|--------------------------|
| 1 | bang — binder tab strip, `crew` | bang mechanism yes (pricing / the ledger / cap table only) | ADD | coral "!" over the crew tab label; visible when: any applicant waiting, OR any employee wants a raise, OR a role open ≥ 8 wks with 0 waiting, OR span_mult < 1.0 | applicants decay and raise-refusals end in resignations — the desk must call when hiring is time-boxed |
| 2 | binder `crew` — roster employee row | yes ("Name — role · $X/wk · burnout N") | CHANGE | becomes "Name — role · $1,500/wk ($1,940 loaded) · sk ●●●●○ · burnout 32"; loaded = salary + (rent + infra + office lever)/headcount; coral inline "! wants $X (market $Y)" when wants_raise | shows the skill you are paying for, the FULLY-LOADED cost of a head (not just salary), and who is about to walk |
| 3 | binder `crew` — raise button | no | ADD | per-employee "+10%" ink button at x=930; steps salary +10% (clamped 0.5–2.5× market); crossing 0.95× fair pay clears the coral "!" and pays +2 morale | fixing underpay is one press; the clamp and the clearing threshold teach the market band |
| 4 | binder `crew` — let-go button | no | ADD | per-employee "let go" at x=1024; first press re-labels to "owe $3,000 severance — sure?" (coral), second press fires; employee leaves now, severance booked next tick | firing costs tenure-banded real money; the confirm shows the invoice before the deed |
| 5 | binder `crew` — department grouping | no | ADD (hq era only) | roster grouped by role with subtotal rows: "ENGINEERING — 6 heads · $19,800/wk" | at a 40-head cap a flat list is unreadable; departments teach structure at scale |
| 6 | binder `crew` — OPEN ROLES section | no | ADD | one 56px row per open role: "ENGINEER  advert $1,400/wk [−](830)[+](894) · market $1,200 · 3 waiting · [×](1064)"; advert stepper clamped 0.5–2.0× market; under-market advert prints coral "0.80× market — expect silence"; trailing line "+ open:  sales  designer  ops  support" (buttons; only era-unlocked roles; replaced by "no desk left to open a role" when staff cap is full) | advert-vs-MARKET-RATE is THE hiring decision; the silence warning explains zero applicants before the player blames the game |
| 7 | binder `crew` — seats stepper | no | ADD (hq era only) | "seats 2" [−][+] inside a role row, 1..5; arrivals keep flowing until seats are filled | batch requisitions at scale, without reopening the same role five times |
| 8 | binder `crew` — recruiter row | no | ADD (floor era up) | "recruiter on retainer $1,500/wk — pipeline ×1.75  [hire](1000)/[dismiss](1064)"; at hq a [−][+] counter 0..2 | applicant flow can be bought; the price sits printed next to the effect |
| 9 | binder `crew` — applicant cards | no | ADD | up to 6 cards, 66px each: "Mara Voss · ●●●●○ · asks $1,700/wk (market $1,200) · \"negotiates via long silences\" · waiting 2wk" + [hire](1000) [pass](1064); hire at 0.4 alpha when cap full; overflow line "+N more wait behind these" | the hire is a priced choice — skill vs ask vs market — with the decay clock visible |
| 10 | binder `crew` — payroll totals | no | ADD | bottom-left line: "payroll $6,200/wk · fully-loaded $8,050/wk" | the roster's total weight on burn, in both the naive number and the honest one |
| 11 | binder `crew` — desk footer rules | no (idiom exists on the ledger) | ADD | one 20px INK-0.5 line: "the rules of this desk: the MARKET RATE is what the street pays for the role · a head costs more than a salary (fully-loaded) · pay under market and ATTRITION compounds · SEVERANCE is tenure-banded, and grows up with the company" | the desk states its own laws — the pedagogy in one standing sentence |
| 12 | binder `crew` — morale spark | yes (full-width bottom) | CHANGE | shrinks to 560px, right half of the bottom row | makes room for the totals line without losing the mood read |
| 13 | binder `the ledger` — "out:" P&L line | yes | CHANGE | appends "· severance $3,000" and "· recruiting $1,500" when > 0 | every labor dollar appears in the same P&L sentence as rent and payroll |
| 14 | journal — arrivals line | no | ADD | "3 applied for ENGINEER (advert $1,400 vs market $1,200)" | weekly proof the advert works, always against the market anchor |
| 15 | journal — applicant-decay line | no | ADD | "Mara Voss stopped waiting on ENGINEER after 3 wks (your advert: 0.85× market)"; skill ≥ 4 departures also print as events | losing a candidate names the cause (waiting × underpay), not just the fact |
| 16 | journal — raise-ask event | no | ADD | "Nico wants a raise: $1,100 now, market says $1,600 (0.69×)" | the ask arrives with the exact gap, so granting or refusing is an informed bet |
| 17 | journal — resignation event | no | ADD | "Nico resigned: paid 0.69× market for 6 weeks" | attrition teaches its WHY: the ratio and how long it was tolerated |
| 18 | journal — review-week line | no | ADD (office era up) | "review week: 3 people compare their pay to the market" (every 12th week) | warns that underpayment surfaces in synchronized cycles, like real comp reviews |
| 19 | journal — benefits line | no | ADD (office era up) | "a real office expects benefits: office $0 vs $750 expected" | explains the morale drain and raise pressure the unfunded office lever causes |
| 20 | journal — span-of-control line | no | ADD (floor era up) | "span of control: 9 heads, 1 manager — the floor runs at 83%" | names why output sagged and that a manager (not more ICs) is the fix |
| 21 | journal — severance receipt | no | ADD | "severance: $3,000 (2 wks × $1,500 — tenure 10 wks)" | the firing invoice itemized: weeks × salary × tenure band |
| 22 | journal — recruiter receipt | no | ADD (floor era up) | "recruiter on retainer: −$1,500/wk" | the standing cost stays visible every week it is paid |
| 23 | garage HUD — room-level binder bang | yes (fires on unpriced offers / net < 0 / fundraising) | CHANGE | OR-in row 1's crew conditions (applicants waiting / wants_raise / stale role / span < 1) | the room's call-to-open-the-binder must fire for crew emergencies too |
| 24 | coach — first-run chip 3 (the binder) | yes (3-chip week-1 tutorial) | CHANGE | append one sentence: "when people apply, the CREW tab is where you hire." | first-run players learn the crew desk exists before the first applicants land |

---

## 8. LLM — dressing implementation + leverage map

### 8.1 The one batch call: candidate dressing

Fired by the turn runner only when `rep["applicants_new"] > 0`. Applicants are BORN keyless
(pool names/quirks, playable at once); the call replaces dressing fields in place when it lands —
`name`, `quirk`, `one_liner` only, order-matched. Cheap lane (`{"tier": "clarify"}`).

`LlmClient.CANDIDATES_SCHEMA` (Godot const + C# `CandidatesSchema` twin):
```
{ "type": "object", "additionalProperties": false, "required": ["candidates"],
  "properties": { "candidates": { "type": "array", "minItems": 1, "maxItems": 10,
    "items": { "type": "object", "additionalProperties": false,
      "required": ["name", "quirk", "one_liner"],
      "properties": {
        "name":      {"type": "string", "maxLength": 40},
        "quirk":     {"type": "string", "maxLength": 60},
        "one_liner": {"type": "string", "maxLength": 90} } } } } }
```

System prompt `CANDIDATES_PROMPT` (event_generator + C# twin, byte-synced):
```
You dress job applicants for RUNWAY!, a satirical startup survival game. The engine
already decided every number — each candidate's role, skill 1-5 and weekly ask are
FIXED and not yours. For each candidate, in the given order, invent ONLY: name (a
plausible human full name, never a real person), quirk (one dry, specific habit,
<=60 chars), one_liner (how they'd pitch themselves in one wince-funny sentence,
<=90 chars). Match the texture to this company, its era and its business. Skill 5
reads impressive with one red flag; skill 1 reads earnest and alarming. A candidate
with source "referral" knows someone on the team — let it show. Never state the
numbers. No name may repeat a name in taken_names. Exactly one entry per candidate,
same order. Output ONLY the schema.
```
User payload:
```
{"company": {"name", "idea", "what", "who", "era"},
 "team": [employee names],
 "taken_names": [every employee/cofounder/applicant name],
 "candidates": [{"role": "engineer", "skill": 4, "ask": 1700, "source": "inbound"}, ...]}
 + "\nDress them."
```
Validator: `candidates.size() == N` and each name non-empty and not in taken_names, else the
whole reply is discarded (pool dressing stands). Numbers in the reply are ignored by construction.

**Keyless pools** (salt 34 stream): names from `WorldGen.person_name(r34)` (C#
`WorldGen.PersonName`), retry ≤5 on collision with roster/applicants; `one_liner = ""` keyless
(the card simply omits the line); quirk from the 24-entry pool, indexed `r34.randi() % 24`:

```
QUIRK_POOL := [
  "brings a mechanical keyboard to interviews", "answers every question with a diagram",
  "left three startups the month before each died", "refers to money only as 'runway'",
  "has strong opinions about fonts", "already uses your product wrong",
  "asks about the pension plan, twice", "codes only between 11pm and 4am",
  "keeps a spreadsheet of past managers' flaws", "quotes their old boss like scripture",
  "negotiates via long silences", "brings homemade cookies to close deals",
  "insists on being called a craftsperson", "hosts a podcast about quitting jobs",
  "writes thank-you notes in fountain pen", "claims to have met your rival's founder",
  "will not work Wednesdays, won't say why", "laughs at their own spreadsheets",
  "alphabetizes the shared fridge", "sends follow-ups at 5am sharp",
  "wears their last employer's company shirt", "describes everything as 'basically shipping'",
  "interviews you back, taking notes", "once returned a signing bonus on principle"]
```

### 8.2 LLM leverage map — where a call earns its place, and where it must not

**Genuinely adds value (the complete list):**
1. **Candidate dressing** (8.1). Trigger: `rep["applicants_new"] > 0`, exactly one batch call for
   ALL of the week's candidates. Never decides: how many arrive, role, skill, ask, decay timing,
   who gets hired. Value: people textured to THIS company and era ("your kombucha-subscription
   startup") — a static pool cannot reference the pitch; referral candidates can gesture at their
   referrer because `team` is in the payload.
2. **Market-beat narration — zero new calls.** Resignations, raise asks, span problems and benefits
   pressure enter the DM's existing adjudication/event context as `_directives` lines (§6); the DM
   turns "resigned: paid 0.70× market for 6 weeks" into a scene. It narrates the receipt; it never
   re-prices it.

**Tempting but wrong (and why):**
1. *LLM-inferred skill/ask from generated "resume" text* — breaks seeded determinism, twin parity
   and replay, and opens a flavor→numbers channel (prompt-injection surface). Numbers come only
   from salts 30-33.
2. *Per-company salary tables* ("what does an engineer cost for THIS startup?") — kills the
   pedagogy: the market-rate anchor must be a stable world constant the player learns across runs;
   a theta-style generated table would hide the pay-vs-market read the desk exists to teach.
3. *LLM-adjudicated raise talks / firing fallout* (model sets morale hits, severance, quit odds) —
   magnitudes are engine law (efficiency-wage ladder, tenure bands). The DM gets the receipt, not
   the pen.
4. *Recurring per-applicant chatter* (weekly follow-up notes, thank-you emails) — call-volume creep
   against the one-batch-per-arrival-week budget; recurring texture is what the authored quirk pool
   is for.
5. *LLM-run interviews that reveal "true" skill* — the moment the model grades an answer it decides
   a number. If an interviews wave ever lands, the ENGINE rolls the reveal (seeded); the LLM only
   voices the candidate.

---

## 9. Twin test pins (add to game/tests/sim_engine_test.gd + unity/Runway.Core.Tests/Program.cs)

1. **The floor, and determinism.** seed 42 wk5 state, `open_role("engineer", int(market*0.7))`
   → 4 ticks → `applicants.size() == 0`. Same base state, advert = market, hype 40, morale 70 →
   after 3 ticks `applicants.size() ≥ 1`; run it twice from identical states → both applicant
   lists match element-wise on (name, skill, ask).
2. **Ask bounds + decay window.** Force ≥ 6 arrivals (advert 1.5× market, hype 80, several ticks):
   every applicant has `1 ≤ skill ≤ 5` and
   `market×SKILL_ASK[skill]×0.90 − 5 ≤ ask ≤ market×SKILL_ASK[skill]×1.15 + 5`. Then insert an
   applicant by hand with `applied_week = W`: still present after ticks W+1 and W+2 (grace); tick
   to W+5 → gone whatever the seed (hard cap), and a rep line mentioned the departure.
3. **Era gates.** (a) `severance_weeks("garage", 10) == 1`, `("coworking", 10) == 2`,
   `("office", 10) == 2`, `("office", 30) == 3`, `("office", 100) == 4`, `("hq", 10) == 3`.
   (b) `role_unlocked("manager", "coworking") == false`, `("manager", "floor") == true`,
   `("designer", "garage") == false`, `("designer", "coworking") == true`.
   (c) floor era, 12 non-managers, 0 managers → `span_mult == 0.5`; add 2 skill-3 managers
   (capacity 15) → `span_mult == 1.0`; any era below floor → `span_mult == 1.0` regardless.
   (d) two office-era twins, 3 staff each, `budgets.office` 0 vs 1000: after 4 ticks the unfunded
   twin's morale is strictly lower.
4. **Hire flow + skill pays.** `hire_applicant` moves the ASK into `pipeline[0].salary` and
   auto-closes the role; tick 1: `employees` empty and `rep.burn` covers the salary; tick 2:
   employee exists with the applicant's skill and `hired_week == week`. Then: two skill-5 sales vs
   two skill-1 sales (launched, traction 600, big TAM) → `rep.adds` strictly greater for the 5s.
5. **Fire receipts.** Office era: fire a $1,500 employee with `hired_week = week − 10` → employee
   gone, morale exactly −8, return `{severance: 3000}`; NEXT tick: `pnl.severance == 3000`,
   `burn ≥ plain_burn + 3000`, and `pnl.net == pnl.revenue − pnl.burn − pnl.liabilities_wk` still holds.
6. **Underpay ladder + poach query.** Employee at `salary = int(fair_pay*0.5)` → after one tick
   `wants_raise == true` (deterministic at ratio < 0.60); `poach_target(state)` returns them with
   `gap_pct ≥ 25`; leave pay untouched 3 more ticks → employee gone and an events line contains
   "resigned"; a twin employee paid `0.9×fair` → `poach_target` returns {} / null.

---

## 10. Engine-improvement suggestions (max 5)

1. `can_hire()` counts employees only — the pipeline can overshoot `staff_cap`. Count
   `employees + pipeline` (the desk in §7 already does).
2. `effect_ops` `"hire"` lets the DM conjure staff from nothing. Route it through the market: hire
   the best waiting applicant of that role when one exists, else spawn at `skill 3, ask = market`,
   clamped — the story stays, the numbers obey.
3. Two half-systems mutate staff: `GameState.weekly_staff_tick` (burnout quits) and engine §4
   (morale quit) plus this ladder. Fold burnout into tick 3b so one suite pins all departures.
4. The §4 morale-quit picks highest salary; re-point it at `poach_target`-style "best skill first" —
   "the good ones leave first" becomes literally true and shares the ladder's receipts.
5. `burnout ≥ 70` ("cooked") could subtract 1 effective skill in §4's formulas — fatigue would
   finally touch output, not just the quit roll.

## 11. Open questions (max 3)

1. Multi-seat requisitions are hq-only (below hq: one role = one seat, auto-close on hire).
   Should `seats` unlock earlier (say floor), or is the ladder-step itself the lesson?
2. Severance is ALWAYS owed (engine-owned, era-banded, no exceptions) — should a DM-narrated
   for-cause firing ever waive it, or is "the world charges you anyway" the joke we keep?
3. Era polish (+0.06/era) means a coworking desk out-attracts a garage at equal pay. Keep, or let
   hype carry all "company signal" so a loud garage can beat a quiet office?
