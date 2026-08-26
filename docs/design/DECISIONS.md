# Owner arbitrations — binding for implementation

## Asked and answered (2026-08-25)

1. **THE BANK is approved**: 10th binder tab. Borrowing controls, terms
   preview, notes list, full grouped P&L statement live there; the ledger
   keeps levers + the compact weekly P&L. Tab pitch tightens, ring synced.
2. **Shipping a bet is a ritual with a pre-flight**: a SHIP button in the
   binder rolls the dice at the press (house law). A ready bet unshipped
   for 3 weeks ships itself ("launches slip out on their own").
   **Generalized owner requirement — THE PRE-ROLL REVIEW**: before ANY
   dice roll (the weekly LOCK IN included), the game surfaces every
   outstanding attention item (unpriced offers, ready-unshipped bets,
   waiting applicants, repayment cliffs…) as a clear review card built
   from `attention_items(state)`, with two exits: **go back and fix**
   (routes to the right desk) or **roll anyway** (proceeds). Zero items →
   no card. This is a spine-level component; implement once, reuse at
   every roll site.
3. **No mercy cap**: stacked downturns compound (winter × price war ×
   blitz). Every piece receipts and explains itself; capping would
   falsify the lesson.
4. **Machine resale at 50%**: equipment sells back at half price — the
   real secondary-market haircut. CAPEX is forgiving but costly.

## Standing recommendations (owner-notified; proceeding unless overruled)

- **Catalog**: era shelf caps 2/3/5/8/8 + Σweight ≤ 6.0; the flagship
  birth offer carries one starter fixed line (~$15×audience "the tools
  that make it"); dropping an offer is instant behind the two-tap arm.
- **Labor**: multi-seat requisitions stay hq-only; severance is ALWAYS
  owed (a DM "for-cause" firing never waives it — the world charges you
  anyway); era polish stays in attractiveness (a coworking desk
  out-attracts a garage at equal pay).
- **Funnel**: the era spend cap clamps the channel SUM; no pre-launch
  waitlist burst; DM channel categories deferred (one `marketing` cat,
  engine splits by mix).
- **Pipeline**: legacy mid-run Enterprise saves keep their past unnamed
  (no cosmetic backfill); `push_lead` rides the widened `cat` string
  (executors guard); the renewal calendar also surfaces one line on the
  cap table (the board reads it).
- **Finance**: venture debt takes the 0.25% warrant nibble at signing;
  tax bills weekly with NOL carryforward; the garage stays shark-only
  (no friends-and-family note).
- **Roadmap**: standing down a committed bet decays its progress −25%
  (context-switching priced); reach payoffs on Enterprise runs also add
  +2 temporary gtm_cap (coordinated with the pipeline).
- **Board/M&A**: lifeline exits keep the style bonuses, kicker swaps to
  "THE SOFT LANDING"; banked secondary cash joins the finale hero
  count-up with its own autopsy line.
- **Rivals**: a FAILED poach starts counter-offer dynamics (the target's
  ask rises — labor implements); a freed rival slot re-opens disruptor
  spawns below hq.
- **Hardware**: multi-SKU factories and the Service-capacity reuse of the
  production molecule are later waves, recorded not built.

## Wave A fix list (bugs found by the design corpus — not choices)

1. `price_offer` missing from both engines' allowed-ops validators (any
   DM reply using it is nuked).
2. The C# twin lacks the catalog cost-lines / `offer_fixed` engine half
   (Godot has it — committed asymmetry).
3. `served_total` meta does not survive saves (learning curve resets on
   load).
4. `price_offer`'s unmatched-name fallback creates offers bypassing every
   clamp — route it through `add_offer`.

## Binder rework — owner picks (2026-08-26, choose-by-letter rounds)

Frame and system (locked):

1. **THE RING BINDER frame**: kraft cover + rotated sticker, drawn
   concentric rings on the kraft2 ringbar, side rail of divider groups
   with colored index tabs poking left. Closed divider = kraft with stack
   shadow + live total/count; open = paper with its pages fanned.
2. **Smooth opening**: ~0.3s kraft→paper ease; pages fan with ~40ms
   stagger; the sheet slides out from under its tab.
3. **Alarm-red attention**: #D93425 filled tab + white "!", climbing onto
   the closed divider header AND its index tab; sev3 pulses at 12fps,
   sev2 still. Coral stays money-out; red means act.
4. **Taxonomy — 15 desks in 4 groups**: REVENUE (offers, customers, in
   motion, growth) / COSTS (spend, team, bills, the bank, the factory) /
   THE COMPANY (product, cap table, the street, threats) / THE LOG (this
   week, history, events).
5. **Group overview = THE DASHBOARD QUARTET**: divider-header press opens
   an overview whose cards are each page's hero and ARE the buttons.
6. **First-open tour**: 6 steps (4 groups fanned with one-liners + red
   demo + handover); click advances, Esc skips, once per install,
   replayable from how-to.
7. **AUDIENCE DOCTRINE (binder-wide)**: wherever a page's truth differs
   by customer type, it renders a NATIVE variant per audience (Consumer /
   SMB / Enterprise) — never one stretched layout. Locked earlier picks
   (Rate Card, Scoreboard, Quartet) get their audience-density variants
   at implementation.
8. **COLLAPSE LAW (binder-wide)**: every growing list ships its designed
   full/compact/counted ladder — the page never crowds, and the items
   closest to money are never the ones hidden.

Per-desk picks so far:

- **offers = THE RATE CARD** (columned card of what we sell: price,
  serve, margin, verdict word; steppers on the row).
- **customers = THE SCOREBOARD** (count + net + kept%, spark washes,
  audience-density variants).
- **in motion** (audience-native triptych):
  - Consumer = **THE RIVER × THE SOURCES** (C2+C1 mix): hero (joiners/wk
    + measured word-of-mouth factor) → cohort river, last 8 weeks of
    joiner bars colored by origin → two cards below: WHERE THEY COME
    FROM (this week's ranked sources, top 4 + "+N more" expand) and THE
    TASTE TEST (tried → stayed % with the "ads can't move this" note).
  - SMB = **THE HOT LIST**: top 5 named rows ranked by revenue-if-landed
    × closeness; rank 1 is the week's journal move; "the other N" is one
    honest pressable row that expands to the full scrolling list.
  - Enterprise = **THE STAGE BOARD**: rep kanban, columns narrowing like
    the funnel, dying deal in red. Collapse ladder: ≤3 cards per column
    + "+N" chip; column-header press opens the focused single-column
    list; past ~8 live deals cards compress to slim rows.
- **growth = THE MARKET GARDEN (E)** — four plots, steppers, yield
  lines, era cap on the sum, verdict chips — with TWO GENERATED LAYERS
  (owner, 2026-08-26):
  1. **Generated topics**: the four channel metaphors are LLM-generated
     once per run from the business idea + audience (dressing-only). The
     engine's four characters are preserved verbatim in whatever world
     the DM invents — ads = instant and saturating, content = a stock
     that compounds funded and rots starved, referrals = an NPS-gated
     multiplier, outbound = quota knocking. The LLM fits vocabulary,
     never numbers. A fallback topic library keyed by audience ships in
     content_db (garden set = the default).
  2. **Generated illustrations**: each plot's small illustration is
     generated through the scene painter's v2 pipeline (Seedream
     text-to-image, the style-locked palette prompt, cached in user://
     like gen_scenes_v2, inflight-coalesced). The drawn SVG instruments
     remain the instant placeholder and the permanent fallback when no
     key is present or generation fails. Engine numbers, verdicts and
     steppers never wait on an image.
  3. **Generation happens ONCE, at run start** (owner: per-turn would be
     wasteful — the topics follow the nature of the business, which does
     not change weekly). Cached for the whole run. The single other
     legitimate generation point is a PIVOT that changes the business's
     nature (see THE PIVOT below) — the topics and paintings regenerate
     because the business itself did.

## THE PIVOT — owner-specified mechanic (2026-08-26)

THE COMPANY group gains a fifth page: **pivot** (the binder is now 16
desks). Pivoting is the classic startup escape hatch: the money in the
bank survives, and what you burn depends on the axis you pivot along.
Debts and obligations survive both pivots — the bank does not forget.

1. **Audience pivot (change customer type — e.g. SMB → Consumer)**:
   customers → 0, and ALL market-side learning dies — named deals and
   leads, channel learning, content equity (the well drains), word-of-
   mouth base, market beliefs re-fog. What survives: the product (as
   built), the cash, the debts. Default interpretation to confirm at
   implementation: the TEAM also survives (employees are contracts, not
   traction) — "everything to zero" reads as market-side zeroing.
2. **Product pivot (same audience, new product)**: customers take a
   uniform random 50–100% loss; ALL product advances die (quality,
   version, roadmap bets — tech debt clears with the codebase it lived
   in); what survives: channel/marketing/sales learning (content
   equity, CAC learning, the relationships), the cash, the debts.
   Open detail for the pivot design round: in-flight Enterprise deals
   are relationships for a product that no longer exists — default:
   they survive as named leads knocked back to the earliest stage.

Desk law: pivot is rare, deliberate and dangerous — the page must show
a full PREVIEW of exactly what dies (N customers, the well's $X,
N named deals, v0.62 → v0.1) before a two-tap arm; the DM narrates the
pivot week; the garden/rooms topics and illustrations regenerate. The
pivot desk gets its own five-way design round with THE COMPANY group.

## Binder rework picks, continued

- **spend = THE ORG LEDGER (C)** — with the generated-book law (owner,
  2026-08-26: "depends from one business to another — beware it can be
  much longer"):
  1. **The org spend book is generated once at run start** from the
     business idea: line items fitted to THIS company (a restaurant gets
     front-of-house training, the test kitchen, staff meals; a dev-tools
     company gets sales engineering, docs, on-call). Each generated line
     carries {name, one-line "buys", suggested $, bucket}, where bucket
     ∈ the four ENGINE levers — closing (sales), retention (care),
     building (rnd), people (office). The DM invents rows, never math.
  2. **Engine math is untouched**: each engine lever's value = the SUM
     of its lines. Effects (closing +%, churn, quality pace, morale)
     compute per bucket exactly as today. Lines are durable FIELDS on
     the state (saves drop metas), adjusted by per-line steppers.
  3. **Length law (the collapse ladder applied)**: rows group under the
     four buckets with subtotal + effect rows always visible; a bucket
     collapses to its subtotal and opens to its lines; hero + weekly
     total stay pinned while the sheet scrolls. Default book when no
     key: the bare four lines (sales/care/rnd/office).
- **team = THE PAYROLL LEDGER (B)** — with a THREE-RUNG ladder (owner:
  must aggregate per business unit as the company grows, up to hundreds
  of employees):
  1. **≤9 people (garage→office)**: flat person rows — who, role, skill
     pips, $/wk, note (asks/counter-offers in coral, answered on the
     row). The open seat is an honest row with the advert stepper and
     the red waiting chip.
  2. **10–40 (floor→hq)**: rows group by FUNCTION with subtotals
     (SALES ×12 — $9,600/wk ▸); a group opens to its people; askers
     surface to the top of the page regardless of grouping.
  3. **Hundreds (beyond hq — later eras/IPO scale)**: aggregation by
     BUSINESS UNIT — unit rows carry headcount, payroll/wk, average $,
     asks count, a morale mini-face; a unit opens to its teams, a team
     to its people. Unit NAMES are generated once at run start from the
     nature of the business (a restaurant chain: kitchens / front of
     house / supply; a SaaS: engineering / GTM / success / G&A), each
     mapping onto engine functions for effects. Same grouped-row
     component recursed — one primitive, three rungs.
  - **Engine implication (recorded, later wave)**: past ~40 heads the
    labor lane keeps NAMED entities only for leads, askers and recent
    hires, and pools the crowd as per-unit aggregates (headcount +
    payroll + churn rates). The UI grammar ships ready for it; today's
    era cap (40) runs entirely on rungs 1–2.
- **bills = THE BILLS LEDGER (B)** — upgraded by owner ruling into
  **THE LEDGER SHEET primitive** (owner: "like an actual excel sheet…
  total at the bottom… think more accounting"), shared by every ledger
  desk (spend, team, bills, the bank's BOOKS mode):
  1. **Paper book-keeping grammar**: fine vertical column rules,
     horizontal row rules, faint row numbers, a header band of small-
     caps column labels with the unit stated once ("all figures
     $/week"), tabular numerals, money right-aligned in a faintly
     sage-tinted amount column (the green ledger band).
  2. **Accounting rules law**: SECTION subtotal rows carry a single
     rule above (label italic, e.g. "subtotal — the flat"); the GRAND
     TOTAL sits at the bottom under a DOUBLE RULE — the biggest number
     on the sheet after the hero, and it must equal the hero's number.
  3. **Sections fold** (collapse law): a section collapses to its
     subtotal row; the total row never scrolls away.
  4. Bills columns: who we pay · for what · kind (flat/scales) · $/wk ·
     trend note. Sections THE FLAT and THE SCALING with subtotals, then
     TOTAL, then an accounting memo line comparing bills to revenue
     ("the Monday floor eats 1.9× revenue").
  5. Spend and team re-render on the same primitive (spend: bucket
     sections with generated lines, per-bucket subtotal + effect, total
     $/wk; team: people rows, payroll TOTAL double-ruled, fully-loaded
     memo). The rate card (offers) keeps its card form — it is a menu,
     not a book.
  6. **Stepper law (owner, 2026-08-26)**: editable lines carry a
     dedicated ADJUST column immediately right of the amount band, with
     two SEPARATE drawn buttons (− and +, each its own wobbly square,
     visible gap) — never a joined "−+" chip. The amount column stays
     pure right-aligned numerals. Obligation rows (bills) carry no
     controls; only chosen spends do.
- **the bank = THE MEETING** (didactic rework A; the first five-way was
  rejected as "unclear and not didactic" — the fix that won is a
  DIDACTIC SPINE of four numbered zones, each with its lesson written
  into the zone header, read top-to-bottom like an appointment at the
  branch):
  1. YOUR STANDING — the rate DERIVED line by line (era base + runway
     worry − revenue reassurance = the rate), never asserted; credit
     line stated. The rate is the bank's opinion, repriced as the books
     change.
  2. WHAT YOU OWE — each loan anatomized: paid-off progress bar, and
     the Monday payment split into "pays the debt down" vs "the bank's
     fee". The interest-only note's bar never moves — that IS the
     lesson (amortizing vs interest-only).
  3. NEW MONEY — borrow/term steppers beside a paper RECEIPT printing
     the full truth before SIGN: $/Monday, total handed back, THE PRICE
     OF THE MONEY, and "shorter term → smaller price, heavier Mondays".
  4. IF A MONDAY IS MISSED — the miss ladder drawn as three climbing
     stairs, the third red (the note is CALLED).
  Bank dress (letterhead, receipt paper, signature), plain words.
  BOOKS mode = the full statement on the ledger sheet.
  **Pattern promoted**: the NUMBERED DIDACTIC ZONE (badge number +
  small-caps title + one-line lesson in the header) is a reusable
  primitive for every concept-heavy desk (the factory, cap table,
  pivot).
