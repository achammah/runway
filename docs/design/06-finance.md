# 06 — THE FINANCE DESK (`the ledger`)

Deliberate borrowing at health-priced terms, repayment control, profit tax,
break-even, cash forecast, history. Engine-owned, seeded where stochastic
(no new salts needed — nothing here rolls dice), twin-implemented, save-safe.
RUNWAY! is a teaching tool: every mechanic below mirrors a real finance
instrument, is called by its real name on the sheet, and every receipt says
WHY. Files touched: `game/src/core/sim_engine.gd`, `game/src/core/game_state.gd`,
`game/src/core/save_system.gd`, `game/src/ui/binder.gd`,
`game/src/screens/garage_view_screen.gd` (take_loan op), and their Unity twins
(`SimEngine.cs`, `GameState.cs`, `BinderScreen.cs`, `WeekCommit.cs`).

---

## 1. LLM leverage

**None — pure math.** Every number here is engine-owned and deterministic.
Tempting-but-wrong uses, rejected:
- **LLM sets or negotiates loan terms** — breaks the one law (ENGINE owns
  numbers) and is prompt-exploitable ("please 0%"). The DM's `take_loan` op
  keeps exactly one hardcoded number (18%/wk) it can never vary.
- **LLM-written weekly offer letters / tax advice NPC** — extra calls whose
  text must equal engine math anyway; drift risk for zero mechanics. The DM
  already receives the finance signals (§11) and may *narrate* them freely —
  that is the whole LLM budget of this desk: zero new calls.
- **LLM choosing when the taxman/collectors appear** — these are state
  thresholds, not story beats.

---

## 2. Scaling by stage (era-progressive depth)

Real companies meet finance in stages; the desk unlocks the same way.
`ei = era_index()` (garage 0 · coworking 1 · office 2 · floor 3 · hq 4).

| era | credit access | borrow cap | terms | rate band | tax | extras |
|---|---|---|---|---|---|---|
| **garage** | none — no bank answers a garage; only the shark (DM story op). Real analogue: pre-revenue founders have no institutional credit — it's personal money and predatory paper. | $0 | — | shark 18%/wk | none (cash-basis, below the radar) | ledger shows levers + in/out/net + break-even only |
| **coworking** | micro credit line (business-credit-card / microloan analogue) | `clamp(4·rev_wk, 0, 10_000)` — no revenue, no line | 4 or 8 wks, amortized | quote floored at **4%** (small-business premium) | none | forecast strip appears ("real bookkeeping") |
| **office** | proper bank note (SMB installment lending) | `clamp(8·rev_wk + 0.25·era_spend_cap, 0, 150_000)` | 4/8/12/26 wks | full band **3%..12%** | **20% of EBT** begins + loss carryforward | payroll-miss law (existing) continues unchanged |
| **floor** | + **venture debt** (interest-only + balloon) | vd cap = `int(0.30 · last_round_amount)`, 0 if never raised | 12 or 26 wks, balloon | bank quote − 1%, floor 2% | 20% EBT | receivables float (§8b); audit-fee entries join the standing-liability table |
| **hq** | full treasury | bank cap ×1.5, clamp 500_000 | all | 2%..12% | 20% EBT | sweep account: idle cash above $100k earns **0.1%/wk** (§8c) |

`rev_wk` = the same revenue expression `runway_weeks` uses
(`traction × offers_arpu`, theta fallback). `last_round_amount`: new int field
set in `apply_round` both engines (old saves: 0 → vd locked until next raise —
correct behavior for free).

---

## 3. State & save-compat

**Loan v2 — structured `loans` array** (a single principal+rate cannot hold a
4%/wk bank note and an 18%/wk shark at once, and amortization needs per-note
terms). Godot: Array of Dictionary (the commitments idiom); Unity: `List<Loan>`.

```
{ "kind": "shark"|"bank"|"venture",   # venture = interest-only + balloon
  "principal": int,                   # original draw (for receipts)
  "balance": int,                     # remaining principal
  "rate_wk": float,                   # frozen at signing
  "term_wk": int,                     # 0 for shark
  "taken_week": int,
  "pay_wk": int,                      # level payment; 0 for shark/venture
  "missed": int }                     # miss ladder §5.4
```

New fields, all default-safe: `GameState.loans: Array = []`,
`tax_loss_carry: int = 0`, `last_round_amount: int = 0`,
`receivables: Array = []` (§8b). Godot `save_system.save_run` adds
`"loans"`, `"tax_loss_carry"`, `"last_round_amount"`, `"receivables"` to the
state dict (loader's `if k in state` picks them up; missing keys keep
defaults). Unity: `[JsonProperty]` fields with those names.

**Migration (legacy saves, `loan_principal > 0`)**: `loan_principal` stays a
field and stays saved. At the top of the loan step in `weekly_tick` (engine is
the only mutator, works for headless and both engines):

```
if loan_principal > 0:
    loans.append({kind:"shark", principal: loan_principal, balance: loan_principal,
                  rate_wk: 0.18, term_wk: 0, taken_week: week, pay_wk: 0, missed: 0})
    loan_principal = 0
```

Pre-migration reads stay correct via the one read helper
`debt_total(state) = Σ loans.balance + loan_principal` (vitals, signals,
desk). The DM op `take_loan` now appends that same shark record directly
(receipt text unchanged).

**MetricSnapshot gains `net`** (checked: fields today are
wk/cash/customers/revenue/burn/morale/debt/hype — revenue exists, net does
not). Godot: `"net": pnl.net` in the snapshot dict; Unity:
`[JsonProperty("net")] public int? Net` (nullable = absent on old rows). The
series reader falls back per row: `net ?? (revenue − burn)` — close enough for
pre-finance history, exact after.

---

## 4. Credit instruments & math

### 4.1 Risk-priced rate — `bank_rate_wk(state) -> float`
Real analogue: SMB lending priced off debt-service coverage and
time-in-business. Runway proxies default probability, revenue slump proxies
coverage, era proxies track record/collateral. No credit-score state is
dropped by this — runway and growth ARE the books here.

```
rw     = runway_weeks(state)                     # 999 when profitable
health = clampf((12.0 - rw) / 12.0, 0.0, 1.0)    # 0 safe .. 1 desperate (cash<0 → rw 0)
slump  = clampf(-last_growth / 0.25, 0.0, 1.0)   # a 25%/wk revenue decline maxes it
rate   = 0.03 + 0.07*health + 0.02*slump - 0.005*era_index()
return clampf(rate, era_floor, 0.12)             # era_floor: 0.04 coworking, else 0.02
```

Bounds check: worst garage-of-the-band case 0.03+0.07+0.02 = 12% exactly —
the desk can never touch the shark's 18%. Best hq case clamps up to 2%.
Healthy garage-era case is moot (no bank in the garage). Deliberately **no
rng**: the quote is a pure function of state, so there is nothing to
reroll-scum and no salt to spend. Venture rate = `max(bank_rate − 0.01, 0.02)`
(venture debt carries a cheaper coupon because real lenders take warrants —
**simplification: warrants dropped**; a loan that nibbles the cap table is a
later wave, noted in open questions).

**The shark stays 18%/wk** (merchant-cash-advance satire, existing pinned
law). Why the split: (a) narrative — the DM's `take_loan` is the 2am
parking-lot fix, always available, no questions, health-blind; (b) schema
safety — the DM op keeps one frozen number; (c) balance — desk credit is
gated by the books (§2 caps), the desperate route stays open and expensive.

### 4.2 Caps — `borrow_headroom(state) -> int`
Per-era cap from §2, minus outstanding **bank+venture** balances. Shark
balances don't count — off-book by nature (the satire writes itself).
Headroom < 1000 → SIGN hidden, the desk says why ("no revenue, no line").

### 4.3 Amortized bank note (level-payment annuity — the standard installment loan)
```
loan_payment_wk(P, r, t) = ceil(P * r / (1 - pow(1+r, -t)))
```
e.g. $10,000 at 4%/wk over 8 wks → $1,486/wk, ≈$11,888 all-in (≈$1,888
interest). **Simplifications dropped**: APR day-count conventions and
origination fees — the weekly tick IS the period, and fees would be sub-$100
noise. Weekly processing per note (insertion order):

```
interest = ceil(balance * rate_wk)
due      = min(pay_wk, balance + interest)          # final payment shrinks
if cash >= due:
    cash -= due; balance = balance + interest - due
    receipt: "the bank's draw: −$1,486 ($329 interest · $1,157 principal) — $6,158 left, 5 wks"
    if balance <= 0: receipt "the bank note is PAID — the folder closes"; drop it
else: miss ladder §4.5 (interest capitalizes: balance += interest)
```
The receipt's interest/principal split is the amortization lesson in one
line: a loan payment is an expense AND a balance-sheet move.

### 4.4 Venture debt (floor+, interest-only + balloon — the real venture-debt shape)
Weekly: `interest = ceil(balance*rate)`; `cash -= interest` (miss ladder if
short). At `week == taken_week + term_wk`: balloon `due = balance`; if cash
covers it, pay and close; else **the workout** (real distressed-refi):
the note converts to an amortized bank note at `min(rate+0.02, 0.12)` over
8 wks, `missed` carries. Sized off the last equity round (30% — the actual
market heuristic), so it's a post-raise instrument by construction.

### 4.5 Miss ladder (default consequences — exact)
A payment cash cannot cover is **skipped, never drawn into the red** (banks
don't overdraw you; rent and payroll already do). Per miss on a note:
`missed += 1`, `morale −3`, unpaid interest capitalizes, receipt
"MISSED the bank ($1,486 due, $612 in hand) — the balance grows".
- `missed == 2`: rate repriced once `+0.02` (cap 0.12), receipt "the bank
  repriced the risk: 6.0%/wk now". Real analogue: covenant breach / default
  interest.
- `missed >= 3`: the note is **sold to the collectors** — kind→"shark",
  rate 0.18, pay_wk 0, plus new catalog status `collections_calls`
  {morale_wk: −1, dis: "raise", kind: "condition"} for 4 wks (both engines'
  STATUS tables). Real analogue: charged-off debt sold to collection agencies;
  investors do check your credit — hence the raise disadvantage.
- **credit lock** is derived, not a flag: `credit_locked(state) = any loan
  with missed >= 2 and balance > 0`. While locked, SIGN is disabled ("the
  bank won't answer — clear the collectors first"). Self-healing: repay the
  distressed note and the lock lifts. Defaulting can't launder it, because a
  sharked note keeps its `missed`.

### 4.6 Early repay
Per-note [repay] button: pays `min(cash − 500, balance)` (the $500
`RAMEN_PER_WEEK` guard — the founder still eats). Repaying principal early
skips all future interest — that IS the prepayment reward; **no penalty**
(simplification: prepayment penalties dropped — rare in small-business notes
and pure friction here). Sharks can be repaid the same way; their auto-claw
(all cash above $1,500 once cash > $2,000) survives verbatim — it's the
shark's character, and the existing 18%-compounding test pin stays green.

---

## 5. Tax (owner-approved: ~20% on positive net, office era up)

Corporate income tax on **EBT** — earnings before tax, **after interest**
(interest deductibility, like a real P&L). Weekly collection ≈ estimated-tax
prepayments compressed to the tick (**simplification: quarterly filing
dropped** — every other lane is weekly and a quarterly clock adds machinery
without teaching more).

```
net_ops = revenue - burn + liab_wk            # today's pnl.net, unchanged inputs
ebt     = net_ops - interest_wk               # interest_wk = Σ interest accrued this week
if era_index() < 2: tax = 0                   # below office: cash-basis, off the radar
elif ebt < 0:       tax_loss_carry += -ebt; tax = 0
else:
    shelter = min(tax_loss_carry, ebt); tax_loss_carry -= shelter
    tax = int(round(0.20 * (ebt - shelter))); cash -= tax
```

**Loss carryforward: yes** — the real NOL carryforward (post-2017 US NOLs
carry forward indefinitely; we drop the 80%-offset limit as needless
arithmetic). Without it, one +$8k week inside a losing month pays tax while
the company bleeds — reads as a bug to any founder. Carry accumulates only
from office up (garage losses are informal — you weren't filing). Receipts:
"the taxman's cut: −$412 (20% of EBT $2,062 — profit after interest)" ·
"old losses shelter $1,300 of profit — no tax on that slice" · first-ever
charge is prefixed "now you're on the radar: " and sets flag `tax_noticed`.

**P&L lanes added**: `interest` (accrued; shark compounding included even
when unpaid — accrual accounting, the receipt says "owe" not "paid"; at hq
the sweep credit nets against it, §8c) and `tax` (cash). New bottom line:

```
pnl.net = net_ops - interest - tax
identity pin: net == revenue - burn - liabilities_wk - interest - tax
```
(`liabilities_wk` stored positive, as today.)

---

## 6. Break-even (textbook CVP / contribution-margin analysis)

`break_even_customers(state) -> int` — shared by desk, bang, and tests:

```
arpu      = (offers_arpu >= 0 ? offers_arpu : theta.arpu_wk * price_mult) * status_arpu
var_pc    = offers_cogs_per_customer + 0.05 * burn_mult      # infra slope rides burn_mult
margin    = arpu - var_pc                                    # contribution margin $/cust/wk
fixed_wk  = (rent + payroll_incl_pipeline + 50 + Σbudgets) * burn_mult
            + offers_fixed_wk + standing_liab + Σ bank/venture debt-service pay_wk
return margin > 0 ? ceil(fixed_wk / margin) : -1
```

Exactly the engine's own burn terms (Godot's `offers_fixed_wk`; Unity's is 0
until the Wave-A catalog half lands there — parity note §13). Debt service
counts as fixed (it is, weekly). **Excluded and named**: incidents (noise, in
the forecast instead) and tax (scales after profit — break-even is pre-tax by
definition). Display: "break-even: **34 customers** at these prices — 16 on
the books" / margin ≤ 0: "no count breaks even — each customer costs $X more
than they pay". First crossing (`traction >= be > 0`): engine sets flag
`broke_even` + receipt "BREAK-EVEN — 34 customers now feed the machine."
Also feeds `runway_weeks`: its burn expression gains `+ Σ pay_wk` (debt
service is real weekly burn; quote-time circularity is causal — the quote
reads runway before the new note exists).

---

## 7. Forecast (the FP&A 13-week cash model, scaled to the game's 4-week attention span)

`forecast_cash(state, weeks=4) -> Array[{wk, cash, net, revenue}]` — **pure**
(operates on locals, never mutates state), **expectation-only** (no rng draw
anywhere). Per projected week, with customers as a float `A`:

- **Included, evolving**: expected adds `min((p_eff·P + wom)·price_demand, gtm_cap)`
  and churn `A/residence·mults` (fractional, no salt-91 remainder); revenue,
  cogs, infra `50+0.05A`; statuses applied only while their `weeks_left ≥ w`
  (expiry is deterministic and knowable); existing commitments for their
  remaining weeks; the full loan schedule (payments assumed made); receivables
  maturities (§8b); tax on projected positive EBT with a local carry copy.
- **Frozen at current values**: market_trend (its walk is rng), rival
  pressure, hype, product/residence (R&D gains are seeded-remainder), payroll,
  prices, budgets, era.
- **Excluded and named**: incidents and new standing liabilities (that's why
  the strip says "before surprises"), DM effects, morale/quit/outage rolls.

Display: one line — "the next 4 weeks, as planned: $8.2k → $6.9k → $5.1k →
$2.8k (before surprises)", coral with the offending figure bolded when any
week goes below zero. Appears from coworking up (§2).

## 8. Stage extras

**8b. Receivables (floor+)** — working-capital float, the DSO/net-30 reality
of enterprise-scale revenue: each week, 25% of revenue books now but lands in
cash 4 weeks later. `receivables.append({amount: int(0.25·revenue), weeks_left: 4})`;
cash gets `revenue − invoiced + matured`. P&L revenue is unchanged (accrual)
— the ledger now teaches **profit ≠ cash** with real numbers; receipt:
"invoiced $2,100 on net-30 — cash when they pay". Forecast and runway include
the maturity schedule. **Simplification: no bad debt** (collections always
arrive) — acceptable until a rivals/churn tie-in wants it.

**8c. Treasury sweep (hq)** — money-market sweep on idle cash:
`sweep = int(0.001 * max(cash − 100_000, 0))` (0.1%/wk ≈ 5%/yr, honest),
credited to cash, netted into the `interest` lane as income; receipt "the
sweep account pays $412 on idle cash". **8d. Audit fees (floor+)**: the
salt-93 standing-liability pick table gains era-gated entries
("the auditors bill by the hour", wk 4, frac 0.05) — being audited is a cost
of scale, not a fraud check (the engine's books cannot be wrong, so there is
nothing to catch — honesty over theater).

---

## 9. Desk layout (`_tab_ledger` / `TabLedger`, 1160×760 content pane)

The sheet is full today; reflow into **two blocks + one cursor stack**
(the `_tab_cap` idiom), same coordinates both engines:

- **y6** title 38px: "the ledger — money, debt, and the taxman".
- **LEFT block x10..560, y64, 5 rows × 62px — THE LEVERS** (their own visual
  block, compacted): row line 1 (26px): `MARKETING  $2,000/wk` (label ink at
  x10, $ coral at x250), − / + ink buttons 46px at x440/x500; row line 2
  (19px, 0.6α): live effect + shortened hint — "reach ×1.92 — saturates ~$2k"
  · "+1.7 closers — $600 ≈ a seller" · "churn −21% — caps at $3k" ·
  "+1.7 product/wk — debt melts" · "+2.8 morale/wk — caps at $2k".
- **LEFT, under levers — HISTORY**: 18px labels "net, weekly:" (y382) and
  "revenue, weekly:" (y472); `_spark` polylines at (10,402,540×64) SAGE and
  (10,492,540×64) BLUE (net series uses the §3 fallback read).
- **RIGHT block x600..1150, y64 — THE BANK** (garage: the block instead shows
  the shark panel + one teaching line "no bank answers a garage — only the
  shark does"):
  - 30px header "the bank"; 22px quote line "quotes **4.0%/wk** against your
    books (runway 22 wks · growth +6% · office era)"; 20px cost-of-capital
    line "money costs/wk: bank 4.0% · shark 18% · equity: forever".
  - borrow row (26px) "borrow $10,000" with −/+ at x940/x1000, steps
    1k/2k/5k/10k/20k/50k/100k clamped to headroom; term row "over 8 weeks",
    −/+ through the era's §2 term list; 22px preview "→ **$1,486/wk** ·
    ≈$11,888 all-in ($1,888 interest)"; `[ SIGN THE NOTE ]` ink button
    (hidden when locked/headroom<1000, replaced by the reason).
  - outstanding notes, ≤3 rows × 52px: 26px "bank note — $8,215 left"
    + `[repay]` ink word at x1030; 19px "…$1,486/wk · 4.0%/wk · 6 wks ·
    missed 1". Shark row: "THE SHARK — $12,400 (18%/wk, it feeds first)".
    Venture row: "venture note — interest-only $312/wk · balloon $15,600 in
    9 wks". Overflow: "…and N more".
- **CURSOR stack, full width from y≈575** (26px steps, conditional lines
  lift): unit-econ line (kept) → pnl "last week: in/serving" (kept) → pnl
  "out: …" (kept) → NEW 22px "the bank & the state: interest $X · principal
  $Y · tax $Z" (only when any ≠ 0) → **THE BOTTOM LINE** 26px now ending
  "· break-even 34 (16 now)" → forecast line (§7) → warnings: when both fire
  they merge to one coral line ("THE RED: wk 2 of 3 — and this spend ends it
  in 3") → the rules line only when no warning is showing. Worst case lands
  ≤760.
- SIGN logs `log_action("signed a bank note: +$10,000 at 4.0%/wk for 8 wks")`
  — the DM's memory sees deliberate debt. Vitals tab line becomes
  "DEBT $%s across %d notes (worst %d%%/wk)" via `debt_total`.

---

## INTERFACE DELTA (assessable, standalone — one row per element; every row lands in BOTH engines)

| surface | exists today? | CHANGE/ADD | exactly how (content, controls, position, states) | why the player needs it |
|---|---|---|---|---|
| binder · ledger · title | yes ("where this week's money goes") | CHANGE | "the ledger — money, debt, and the taxman", 38px, y6 | names the desk's new scope |
| binder · ledger · lever rows | yes (5 full-width rows, 78px, description + effect + −/+) | CHANGE | compacted into a LEFT block x10..560, 5×62px: line 1 `NAME $X/wk` + −/+ at x440/x500; line 2 19px live effect + short hint ("reach ×1.92 — saturates ~$2k") | frees the right half for the bank without losing the live math |
| binder · ledger · net sparkline | no | ADD | "net, weekly:" 18px label y382; wobbly polyline (10,402) 540×64, sage; old history rows draw revenue−burn | shows whether the machine is starting to feed itself |
| binder · ledger · revenue sparkline | no | ADD | "revenue, weekly:" label y472; polyline (10,492) 540×64, blue | growth trend at a glance — the number the bank prices |
| binder · ledger · bank block | no | ADD | RIGHT block x600..1150 from y64: header "the bank"; live quote line "quotes 4.0%/wk against your books (runway 22 wks · growth +6% · office era)"; cost-of-capital line "money costs/wk: bank 4.0% · shark 18% · equity: forever" | deliberate borrowing with the price and its reasons visible before signing |
| binder · ledger · borrow controls | no | ADD | "borrow $10,000" −/+ (steps 1k/2k/5k/10k/20k/50k/100k, clamped to era headroom); "over 8 weeks" −/+ (era term list); preview "→ $1,486/wk · ≈$11,888 all-in ($1,888 interest)"; `[ SIGN THE NOTE ]` ink button | terms preview = the amortization lesson; no surprise debt |
| binder · ledger · bank block states | no | ADD | garage: shark panel + "no bank answers a garage — only the shark does"; headroom<$1k: SIGN hidden + reason ("no revenue, no line"); credit-locked: SIGN hidden + "the bank won't answer — clear the collectors first" | teaches WHY credit exists or doesn't at each stage |
| binder · ledger · outstanding notes list | no | ADD | ≤3 rows ×52px under SIGN: "bank note — $8,215 left" + `[repay]` at x1030; sub-line "$1,486/wk · 4.0%/wk · 6 wks · missed 1"; shark row "THE SHARK — $12,400 (18%/wk, it feeds first)"; venture row "interest-only $312/wk · balloon $15,600 in 9 wks"; overflow "…and N more" | repayment control + the cliff visible per note |
| binder · ledger · P&L "bank & the state" line | no | ADD | cursor stack, 22px, only when ≠0: "the bank & the state: interest $329 · principal $1,157 · tax $412" | separates cost-of-debt and tax from operating burn — the EBT lesson |
| binder · ledger · THE BOTTOM LINE | yes | CHANGE | appends "· break-even 34 (16 now)"; margin ≤0 variant "no count breaks even" | the one number a founder steers by |
| binder · ledger · forecast line | no | ADD | 22px: "the next 4 weeks, as planned: $8.2k → $6.9k → $5.1k → $2.8k (before surprises)"; coral when any week <0; hidden in garage era | cash planning before the cliff, not after |
| binder · ledger · warnings/rules | yes (2 warnings + rules stack) | CHANGE | both warnings merge to one coral line when both fire; rules line shows only when no warning does | keeps the sheet inside 760px worst-case |
| binder · vitals · loan line | yes ("LOAN OWED $X (18%/wk)") | CHANGE | "DEBT $X across N notes (worst Y%/wk)" via `debt_total` | one honest debt figure across shark+bank+venture |
| bang · "the ledger" tab | yes (net<0 only) | CHANGE | OR of: net<0 · debt distress (cash < 2× weekly debt service, a missed note, or a balloon ≤2wks unpayable) · first tax charged (ack flag `tax_seen` set on view) · first break-even crossed (ack `broke_even_seen`) | the coral ! points at cliffs and milestones the week they matter |
| journal · receipts | yes (loan compound/auto-repay lines) | ADD/CHANGE | new formats: "the bank's draw: −$1,486 ($329 interest · $1,157 principal) — $6,158 left, 5 wks" · "MISSED the bank ($1,486 due, $612 in hand)" · "the bank repriced the risk: 6.0%/wk now" · "sold to the collectors — 18%/wk" · "the bank note is PAID" · "now you're on the radar: the taxman's cut: −$412 (20% of EBT $2,062 — profit after interest)" · "old losses shelter $1,300 of profit" · "BREAK-EVEN — 34 customers now feed the machine" · "invoiced $2,100 on net-30 — cash when they pay" (floor+) · "the sweep account pays $412 on idle cash" (hq) · era unlock one-liner "new at coworking: the bank returns your calls (the ledger)" | receipts ARE the curriculum — every draw explains itself |
| journal · DM narration feed | yes (`loan_owed` in signals) | CHANGE | signals add `debt_lines`, `credit`, `tax_wk`, `break_even`; `loan_owed` kept | the DM can tell the debt story without inventing a number |
| garage HUD / room | yes | no change | cash figure already reflects all draws | — |
| coach (first-run) | yes | no change | the desk's own quote/teaching/state lines carry the finance teaching in place | — |

---

## 10. Weekly-tick integration order (exact)

Inside step 9, after `state.cash += round(revenue) − burn` (ops cash first —
the bank draws from what the week left), replacing the current
`loan_principal` block **and moving the pnl write below it**:

1. **9c LOANS**: migrate legacy (§3) → per-note accrue/pay/claw/miss (§4);
   accumulate `interest_wk` (accrued, sweep-netted at hq) + receipts.
2. **9d TAX**: EBT → carryforward → 20% → cash (§5).
3. **9e RECEIVABLES** (floor+): defer 25%, mature the queue (§8b).
4. **pnl write**: + `interest`, `tax` lanes; `net = net_ops − interest − tax`.
5. **9f BREAK-EVEN flag**: first crossing sets `broke_even` + receipt.
6. unit-econ, beliefs, commitments, snapshot — unchanged, except the snapshot
   gains `"net": pnl.net` (it must record the final week, hence last).

**DM feed** (`signals()`): `loan_owed` kept = `debt_total` (prompt compat);
add `debt_lines` (["bank $8,215 at 4.0%/wk — $1,486/wk, 6 wks left",
"shark $12,400 at 18%/wk"]), `credit`: "locked"|"clean", `tax_wk`,
`break_even`. The DM narrates; it never prices.

## 11. Bangs (`the ledger` tab — deterministic state predicates only)

1. `pnl.net < 0` (kept).
2. **Debt distress / repayment cliff**: `Σ pay_wk > 0 and cash < 2·Σ pay_wk`,
   or any note `missed ≥ 1` with balance, or a venture balloon ≤ 2 wks away
   with `cash < balance`.
3. **The taxman arrived**: `tax_noticed` set, `tax_seen` not — the binder
   sets `tax_seen` when the ledger renders (the coach-mark ack pattern).
4. **Break-even, first crossing** (celebratory): `broke_even` set,
   `broke_even_seen` not; same ack.

## 12. Twin test pins (both suites, same fixtures)

1. **Migration + shark law**: `loan_principal=10_000`, tick → one shark note,
   `loan_principal==0`, `debt_total ≥ 11_800` (the existing 18% pin, moved),
   claw still repays above $2,000 cash.
2. **The credit ladder**: garage headroom 0; coworking with $500/wk revenue
   quotes ≥4% and caps ≤$10k; office runway-4 slumping books quote 12.0%
   exactly; floor venture cap is 0 unraised and `0.30·last_round_amount`
   after `apply_round(100k)`; hq cash $300k sweeps $200 into `interest`.
3. **Annuity**: a hand-built bank note ($10k, 4%, 8wk) pays $1,486/wk, closes
   in 8 ticks with ample cash, Σ interest lanes ≈ $1,888 ± $40, final pnl
   identity holds.
4. **Miss ladder**: cash-0 company — 1 miss capitalizes + morale −3; 2nd miss
   reprices +2% and `credit_locked` true; 3rd converts to shark and installs
   `collections_calls`; repayment lifts the lock.
5. **Tax**: office-era week with EBT $2,062 pays $412; identical books in
   garage pay 0; a −$1,000 week then +$1,000 week pays 0 (carryforward);
   interest deducted before tax (EBT, not net_ops).
6. **Break-even + forecast purity**: `break_even_customers` equals hand math
   on a fixed state; `forecast_cash` leaves state byte-identical, includes
   the loan schedule + floor receivables, and week-1 expected cash matches a
   noise-stripped tick by hand; snapshot rows carry `net`.

## 13. Engine-improvement suggestions (≤5)

1. `runway_weeks` ignores cogs/infra-scaling/offer_fixed — refit it on the
   forecast's expectation core so "burn at current settings" has one source.
2. Unity `burn` lacks `offer_fixed` (catalog fixed lines are Godot-only) —
   port with the Wave-A catalog half or the twin P&L pins will drift.
3. `weeks_in_red` lives in the screens (`_start_week`/RunDriver), not the
   engine — move into the tick so headless balance sims die like real runs.
4. Commitments apply cash *after* the pnl that already anticipates them —
   fold into 9 so cash and pnl move in the same block.
5. Valuation ignores debt: subtract `debt_total` (net-debt valuation) so a
   loan can't pump the `maxi(cash, …)` floor.

## 14. Open questions (≤3)

1. **Venture-debt warrants**: real lenders take ~0.1–0.5% warrant coverage;
   we price the coupon cheaper but take no equity. Add a fixed 0.25% cap-table
   nibble at signing, or keep debt equity-free? (Recommend: nibble — one
   `dilute_all(0.25)` call, honest and cheap.)
2. **Tax cadence**: weekly (designed) vs a quarterly clock with a lump
   receipt (more real, more machinery, spikier deaths). Recommend weekly now,
   revisit if the floor/hq game wants treasury planning pressure.
3. **Garage micro-credit**: keep the hard "no bank" rule, or allow one $2k
   "friends-and-family note" at 3% flat for teaching amortization early?
   (Recommend: keep the hard rule — the shark IS the garage lesson.)
