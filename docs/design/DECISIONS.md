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
  primitive for every concept-heavy desk (the works, cap table, pivot).
- **the factory → THE WORKS** (owner, 2026-08-26: "factory is really
  how you have all your running cost… each massage is kinda a
  factory/product — we need a broader term for all types of business").
  Pick = A, the four-zone Meeting grammar, with BUSINESS-TYPE-NATIVE
  variants — the second variation axis, beside the audience axis:
  the desk answers the same four questions in every business, in that
  business's own units:
  1. CAN WE SERVE? — demand vs capacity vs overflow, in native units
     (massages/wk, seats, units, matched orders). The cost of the gap
     is stated honestly per type: service/hardware/marketplace lose
     un-billed revenue ("$320 walks away"); SOFTWARE degrades instead —
     past the ceiling, service slips and churn bites.
  2. WHAT ONE COSTS — the UNIT TICKET is always the offer's generated
     cost lines (the catalog engine half that already itemizes fixed +
     variable costs): therapist minutes + oils + laundry for a massage;
     hosting + support minutes + billing for a seat; parts + hands +
     wear for a unit; fees + support + supply-acquisition for an order.
     The learning curve teaches per type (practice / automation /
     Wright / ops maturity).
  3. WHAT MAKES THE CAPACITY — the capacity ASSETS, type-native:
     service = people × bookable hours (+ rooms) read from the TEAM;
     software = infra headroom + the care team's ticket bandwidth;
     hardware = the machines (resale at half, as designed);
     marketplace = the active-seller pool + ops staffing (with a
     concentration warning when few sellers carry most supply).
  4. OVERFLOW — the type's own relief valve, always priced against
     in-house: freelancers per session; cloud burst ×price + queue
     (churn cost); the subcontract shop; recruit supply / throttle
     demand behind a waitlist.
  Naming: the tab is **"the works"** in every run. No business sleeps
  the tab — everyone has works.
  Engine honesty: the desk UNIFIES what the engine already computes
  (catalog cost lines, serving COGS, crew, machines, infra line);
  deeper per-type capacity sims (bookable-hours model, ticket
  bandwidth, seller-pool dynamics) are recorded later waves that slot
  into zones 1/3 without changing the page.
  **SCALE LADDER (owner: "not enough scalable — rework")**: the four
  zones never change; their CONTENTS climb three rungs, recursing
  exactly like team's person → function → business unit:
  1. **THE BOUTIQUE** (few offers, named assets, one roof): as first
     designed — named cards, one itemized ticket.
  2. **THE HOUSE** (multiple offers, one roof): zone 1 becomes the
     DEMAND MIX (a mini-ledger: offer · wanted · served · gap) over ONE
     shared capacity pool bar (offers draw from the same hands/machines
     — that is the teaching); zone 2 becomes THE TICKET BOOK on the
     ledger sheet (offer · costs each · sells · margin · volume share;
     pressing a row opens its full itemized ticket); zone 3 groups
     assets with subtotals (HANDS ×9 — 216 slots ▸, ROOMS ×4,
     MACHINES ×3), best-performer named, the crowd counted; zone 4
     lists relief valves as stepper rows (including demand-smoothing:
     off-peak pricing).
  3. **THE EMPIRE** (sites: studios / plants / regions / product
     lines): the page leads with THE SITES LEDGER — site · capacity ·
     wanted · utilization% · unit cost · margin each · flag, TOTAL
     double-ruled; a site row opens to its own rung-2 view (recursion).
     Per-site unit costs teach comparative economics ("Lyon makes a
     session for $27; Geneva for $36 — rent eats the ticket"); site
     flags climb to the tab red. Relief at this rung: open/close a
     site (capex card), rebalance overflow between sites.
  Volume formatting scales with the numbers (82/wk → 8.2k/wk); bars
  normalize; units stay native. Software climbs the same rungs as
  tiers → products → regions; marketplace as categories → regions;
  hardware as SKUs → plants (the deferred multi-SKU factory slots in
  here).
  **THE DIVISION MECHANIC (owner challenge: "how will you
  programmatically decide on each division?")** — divisions are NEVER
  generated; they are engine state:
  1. **Born from decisions.** A division exists only because the player
     built it through a real op: `open_site` (a written move → clarify
     rounds → rent quote + capex + hire pack → a site record),
     shipping a second product (a roadmap bet that lands creates a
     product record; offers carry a product id), entering a segment
     (pipeline/funnel already model audiences). `close_site` is the
     mirror (severance owed, lease broken, resale at half).
  2. **The axes are fixed schema, not free-form**: site · product ·
     offer · segment. The LLM's ONLY role is naming and dressing the
     objects (suggesting "Lyon" for the new studio, awning text) —
     numbers never. "Sliced by ▾" lists only axes with ≥2 populated
     divisions in state.
  3. **Every dollar already has an address.** Division books are
     GROUP-BYs over records the engine already keeps: employee.site,
     machine.site, offer.product, serving COGS per offer, rent per
     site, site wage multiplier. Roll-ups are sums — nothing invented.
     What genuinely has no address (founder, brand marketing, HQ rent)
     lands in an honest **SHARED / HQ row**, never smeared across
     divisions — allocated vs direct costs IS the lesson.
  4. **Per-site demand**: each site carries its own local demand share
     (the funnel splits reach by site weight; a new site ramps on its
     own curve). Site unit costs differ through rent + local wage
     multiplier + its own learning count — which produces the
     "Lyon $27 / Geneva $36" comparison mechanically.
  5. **Rungs are deterministic counts**: sites ≥ 2 → empire (default
     slice = site); else offers ≥ 3 → house (slice = offer); else
     boutique. No judgment calls, no LLM.
  Engine work recorded for implementation: site record + site fields
  on employees/machines + open_site/close_site ops + per-site demand
  weights; product id on offers (set when roadmap ships); the slicer
  itself is pure aggregation over existing pnl lanes.
  **ARRANGE MODE (owner, 2026-08-26: "we need an edit mode of this tab
  to put the right element manually")** — the works' WRITE view:
  1. The read faces (whichever rung-3 face is picked) stay pure
     display; pressing ARRANGE flips the desk to one neutral
     assignment layout regardless of face: divisions as labeled bins
     (+ the SHARED/HQ bin), elements as chips — people, machines/
     rooms, org-book spend lines. Two presses, no drag: press a chip,
     press its new home; Esc cancels (the Esc contract holds).
  2. **Moves are real operations, not bookkeeping**: moving a person
     charges a one-off relocation cost and a 1-week ramp at the new
     site; moving a machine charges shipping and a week offline. The
     PRE-MOVE RECEIPT shows all of it before the two-tap confirm
     ("June → Lyon: $400 now · Paris −24 slots/wk · Lyon +24 after
     ramp"). Pure TAGS are free (offer → product; spend line →
     division/shared) — paper is paper.
  3. **Bound elements never move by hand**: rent belongs to its roof,
     serving COGS to its offer, interest to its note — shown in a
     locked strip so the player learns which costs follow which
     objects.
  4. **New things ask at birth**: with ≥2 sites, hiring/buying asks
     "for which site?" in the clarify pass; DM ops carry the field.
  Engine ops: reassign_employee{site}, move_machine{site},
  tag_offer{product}, tag_spend_line{division|shared} — all
  deterministic, books re-roll as sums.
  **ARRANGE EDITS THE BINS TOO (owner follow-up: "adding/remove/edit
  blocks like LYON — which has consequences")** — full structure
  editing, all through the same receipt + two-tap grammar:
  1. **ADD** — a "+ new" ghost bin sits in arrange mode; pressing it
     enters the SAME open_site flow as the written move (lease quote,
     capex, hire pack, priced receipt). One op, two doors.
  2. **EDIT** — rename is free (dressing). Re-leasing / resizing a
     roof is priced (new rent, a moving week). Demand is never
     editable — it is earned, not set.
  3. **CLOSE** — the teardown wizard: the closing review lists EVERY
     element of the bin with a decision each (people: move — priced —
     or let go — severance ALWAYS owed; machines: move or sell at
     half; lease: broken, penalty), plus the customer consequence
     (this roof's customers partially transfer with a churn hit, the
     rest are lost), all composed into ONE closing receipt with the
     payback line derived ("Geneva loses $210/wk — closing costs
     $6,150 and pays back in 29 weeks"). Two-tap on the total.
  4. **MERGE / SPLIT** — shortcuts that compose the same primitives
     (close + moves / open + moves), one composite receipt.
  5. **PAPER vs BRICK** — the teachable split: PAPER divisions
     (products — groupings of offers) restructure FREE; BRICK
     divisions (sites/plants) always price; SEGMENT divisions are the
     world's, not editable. Chip moves confirm one by one; bin
     operations stage into one composite receipt; Esc abandons the
     whole staged change.
  Engine ops added: open_site (shared with the written-move door),
  close_site{decisions[]}, edit_site{lease}, rename is dressing-only.

## THE MUTATION LAW — binder-wide (owner, 2026-08-26: "we need a
## logic of what happens if things are added/removed/edited on
## customers, bank, etc.")

Every structural mutation on any desk flows through the SAME grammar:
a receipt that prices it first, a two-tap confirm, Esc abandons. Three
consequence classes: **ink is free** (renames, tags, groupings),
**brick is priced** (anything physical: capex, leases, ramps,
relocations), **obligations survive removal** (severance, penalties,
churn — nothing is silently destroyed). Per desk:

| desk | ADD | EDIT | REMOVE |
|---|---|---|---|
| offers | define a new offer → the DM prices its tools (fixed lines) + unit cost → receipt → on the card | price steppers free; redefining re-prices its cost lines | drop behind two-tap; its customers churn or migrate to a neighbor offer |
| customers | never by hand — customers are EARNED (growth, in motion) | — | fire an account: contract penalty, revenue dies, the street hears it |
| segments | entering a new customer type is a priced door — its motion ramps from zero | — | leaving loses its customers (audience-pivot-lite; the pivot desk owns the full version) |
| the bank | borrow = the quote + SIGN | REFINANCE — swap a note for a new quote at today's standing, break fee on the old (new op) | repay early: cash out now, the interest line dies |
| team | hire (asks "which roof?" when sites ≥ 2) | raises / role change (ramp week) / move site via arrange | let go — severance ALWAYS owed |
| the works | open roof: lease + capex + hires, priced | re-lease/resize priced; rename free | close: the teardown wizard, one receipt, payback line |
| product | a new product ships only from a landed bet | reprioritize bets (stand-down −25%) | retire a product: its offers retire with it, customers migrate or churn |
| spend | add a line into a bucket — starts billing $/wk | steppers free | stop a line instantly — unless the book marked it "contract" (notice period bills through) |
| bills | not editable — bills follow the things that create them; you change a bill by changing its source | | |
| cap table | moves only through rounds, offers and events — never by hand | | |

New engine op recorded: refinance_note{old_id, quote} with break fee.
The rate card's "define an offer" door reuses the shipped LLM
cost-lines flow (owner-approved wave 2 mechanic).

**THE PRICE BOOK (owner, 2026-08-26: "the cost of opening etc.
generated at the beginning of the game so the whole path is clear")**
— at RUN START (and again only at a pivot that changes the business's
nature), the world generation emits the complete structural price
schedule fitted to this business, alongside offers/cost lines/topics:
open-a-roof pack (lease deposit + capex + hire pack + ramp weeks, by
era), relocation fee, machine shipping, lease-break formula (N weeks
of rent), which spend-book lines are "contract" and their notice
periods, refinance break fee, freelancer/subcontract/burst overflow
rates, account-fire penalty. Rules:
1. **LLM proposes inside engine bands, engine clamps** — the standing
   law; never unclamped numbers. Stored as durable FIELDS.
2. **The path is visible before the decision**: desks quote the price
   book in advance ("a second roof ≈ $18,000") so expansion can be
   planned, not discovered.
3. **The world-generation prompt (and the PA prompt-assistant pass on
   it) must be updated at implementation** to emit this schedule —
   recorded as an explicit implementation task.

Rung-3 read face: owner moved on without a letter — DEFAULT = B (THE
HERO ROWS, calmest and most scalable, consistent with the ledger-
family picks); one word swaps it to folders or quartet.

## PRODUCT desk — corrected understanding (owner, 2026-08-26: first
## round rejected — "the product tab enables you to see what the
## product IS, the features, planning next features, the whole
## pipeline, and cost of the product — valid for product/service/
## software/marketplace")

The desk is THE PRODUCT'S OWN PAGE, not meters about it. Content spine
(fixed; style being picked from a 10-way round):
1. **IDENTITY** — what the product is in plain words (name + one
   sentence + who it's for + version), generated at birth, rename is
   ink.
2. **THE FEATURES** — the concrete inventory of what it's made of:
   named features, each an ENGINE OBJECT (birth features from world
   gen + landed bets), carrying a contribution class (fixed enums:
   brings-them-in / keeps-them / lets-us-charge / plumbing), a
   solidity state (solid / creaky / breaking — tech debt made visible
   PER FEATURE, concentrated in the plumbing), and a keep-cost $/wk.
   For a service business features are service elements (the 50-min
   protocol, online booking, the loyalty card); for hardware,
   capabilities/components; for a marketplace, platform pieces
   (escrow, ratings, search).
3. **THE PIPELINE** — the whole path visible end-to-end: THE SHELF
   (DM-proposed candidates priced inside price-book bands: $, weeks,
   odds, promised contribution — plus a "define your own" door) →
   NEXT (committed queue, reorderable) → BUILDING (progress, odds,
   $/wk, stand-down burns 25%) → READY (SHIP rolls the dice at the
   press; self-ships in 3 weeks, red clock) → LIVE (joins the
   inventory; promised vs measured checked).
4. **THE COST OF THE PRODUCT** — build $/wk (committed bets) + keep
   $/wk (sum of feature keep-costs; features are never free) + the
   per-unit impact on the works' ticket + the creak line (debt −4%
   build speed until refactored).

**PICK (owner): style 5, THE KANBAN WALL — refined, renamed, proven**:
1. **The tab is renamed "what we make"** — wider than "product": a
   spa makes an experience, a device maker makes the device, a
   marketplace makes the platform. (Alternates offered: "the craft",
   "the thing"; owner can swap with one word.) "Offers" stays the
   COMMERCIAL packaging (prices/units); "what we make" is the
   substance; "the works" is the cost of delivering it.
2. **Two-part page (clearer, cleaner, allowed to be long)**:
   - THE PIPELINE WALL on top — four columns, each with a one-line
     meaning under its header: THE SHELF (priced ideas — $, weeks,
     odds, promised job; press to commit) → NEXT (the committed
     queue, reorderable) → BUILDING (progress + $/wk + odds;
     stand-down burns 25%) → READY (SHIP rolls the dice; self-ships
     in 3 weeks, red clock).
   - LIVE — THE INVENTORY as a full-width band below, grouped by job
     (brings them in / keeps them / lets us charge / plumbing), one
     card per feature: name, solidity dot, keep-cost, measured note
     on recent landings ("promised +8, measured +6"). The creaky
     card is the debt, pointable.
   - The cost footer: build + keep + per-unit impact + creak tax.
3. **One card anatomy everywhere** — pipeline cards carry
   $/weeks/odds/promise; live cards carry solidity/keep/measured;
   same shape, so the wall reads as one system.
4. **Type-proven**: the same wall renders for SaaS, service (the
   experience), hardware (the device), marketplace (the platform) —
   only vocabulary changes, all generated at run start.
5. **SCALE LADDER (owner: "how does it scale?")** — same recursion as
   team and the works:
   - **Rung 1 — one thing, few features** (≤ ~12 live): as rendered,
     every feature a card.
   - **Rung 2 — one thing, many features**: features fold into named
     FAMILIES ("the booking suite" — count, summed keep-cost, worst
     member's solidity; opens to members). Families are INK — free
     tags, regroupable in arrange mode. Inside each job-group the
     fold is attention-first: creaky/breaking cards and fresh
     landings stay face-up, the healthy crowd folds to "the other N
     solid — $X/wk ▸". The shelf column shows its best 4-5 ideas by
     fit + "N more ▸"; BUILDING holds several bars at once (bigger
     r&d = parallel builds).
   - **Rung 3 — many things (products ≥ 2)**: the tab leads with THE
     LINEUP — one hero-row per product (name, version, features live,
     creak state, building count, its cost line); press a product →
     its full wall. A **SHARED PLUMBING band** sits under the lineup
     (billing, identity, data platform — features every product
     stands on; a creak here taxes ALL build speed) — the mirror of
     SHARED/HQ in the works. Red bubbles: feature → family → product
     row → tab.
   Engine: family = a tag field on features (ink); product id already
   exists; nothing new but the tag.

## THE OWNERSHIP CLUSTER (owner, 2026-08-26: cap table = investor
## state + dilution + ESOP surfaced for employees + a recruitment tab
## with offers + inbound/outbound/pitches/SAFEs + ALL instrument types)

The ask splits into THREE pages + one thread. The binder is now 18
desks: COSTS gains **recruitment** (beside team); THE COMPANY gains
**the raise** (beside cap table).

### 1. CAP TABLE — the ownership STATE
- HERO: your slice % and its paper worth ("you own 58% ≈ $1.5M —
  paper, not cash").
- THE SLICES: ledger-sheet of holders — founders, each investor by
  round/instrument, the ESOP pool (granted vs available) — columns:
  holder · instrument/class · invested · % · preferences. Double-ruled
  100% total.
- THE DILUTION STORY: a shrinking-bar timeline of ownership events
  (start 100% → SAFEs convert → pool top-up → priced round), each
  event showing % DOWN but paper value UP — the core dilution lesson.
- THE WATERFALL: "if sold today at $X": the bank first, preferences
  next, then the split — preferences bite below the cap, drawn.
- THE POOL panel: size, granted, available, upcoming vesting cliffs.
- Board & covenants strip stays here (existing mechanics).

### 2. THE RAISE — the fundraising PIPELINE (in motion, for investors)
- Stages: ON THE RADAR (inbound knocks — generated mechanically from
  traction/hype/era/board network as an interest score — plus
  outbound targets you pitch) → CONVERSATIONS (they ask for real
  numbers; the data room readiness reads YOUR binder: growth, margin,
  runway, pipeline — weak pages = investor doubts, named) → TERMS ON
  THE TABLE → SIGNED & WIRED.
- **All instrument types**, each with true semantics: angel check
  (small, fast) · SAFE (post-money, cap + discount — money now,
  dilution LATER; the SAFE STACK warning shows total deferred
  dilution if all convert) · convertible note (SAFE + interest +
  maturity that can come due) · PRICED ROUND (valuation, new share
  class, 1x non-participating standard — participating flagged
  predatory, board seat ask, POOL TOP-UP demanded pre-money — the
  top-up dilutes founders, not the new investor: taught explicitly)
  · bridge (insider note) · venture debt (lives at the bank, warrant
  nibble) · secondary (exists — soft landing).
- THE COMPARISON CARD: two offers side by side — true dilution today
  AND at next round, board seats, preferences, pool demands. The
  desk's biggest teaching moment.
- **The raise costs founder time**: an active raise consumes weekly
  attention (the shop measurably slows) — fundraising is never free.
- No-shop, covenants, strikes: existing board mechanics plug in.

### 3. RECRUITMENT — the hiring pipeline with real offers
- OPEN ROLES: role cards (seat, level, comp band from the price
  book, advert stepper, ≈applicants/wk).
- THE CANDIDATES: per role, applied → interviewed (costs founder
  time) → OFFER OUT → signed/joined (ramp) or lost.
- **THE OFFER COMPOSER** (the ESOP hook): salary $ + options from
  the pool (N% · 4-year vest · 1-year cliff) + title; acceptance
  odds move with the mix AND the candidate's profile (mercenary
  wants cash, missionary takes equity) — comp design taught. Rival
  counter-offers can outbid; declined offers sour the market
  slightly.
- Collapse ladder: many roles → grouped by function (rung 2 team
  grammar).

### THE ESOP THREAD (one mechanic, three surfaces)
- Pool born/expanded at (typically) a priced round's demand or by
  founder op; expansion dilutes existing holders — shown on the
  dilution story.
- Grants: {n%, 208-wk vest, 52-wk cliff}; leavers keep vested,
  unvested returns to the pool.
- Surfaces: cap table (the pool panel) · TEAM (each row gains a
  vesting mini-bar: "0.4% · 31% vested · cliff passed") · recruitment
  (the offer composer draws from the pool; empty pool = no equity
  offers until expanded).
- Attractiveness: grants raise labor-market appeal and allow lower
  cash salary (comp mix is a real lever).

Engine work recorded: ESOP pool + grant records + vesting ticks;
instrument records (safe/note/priced/bridge) with conversion at
priced rounds; investor interest score + raise pipeline state +
founder-time tax; offer records with acceptance model; recruitment
desk state. Board-lane term-sheet/covenant/no-shop mechanics are the
foundation the raise extends.

### 4. THE OFFER — a MOMENTARY desk (owner, 2026-08-26: buyout offers
### create a temporary tab)

When a buyout offer lands, a gold tab slides into THE COMPANY group
and stays only until the offer is answered or expires (then folds
into HISTORY). This is the first of the MOMENTARY DESK pattern (an
IPO window can reuse it later). Contents, four zones:
1. **WHAT'S ON THE TABLE** — the headline price decomposed honestly:
   cash today vs acquirer STOCK (lockup months — their paper, their
   luck) vs EARNOUT (paid only if targets hit — flagged when the
   targets sit under the buyer's control) vs retention handcuffs
   ("you must stay N months").
2. **WHO GETS WHAT** — the waterfall applied to THIS number: the bank
   first, then preferences (or conversion when converting beats the
   1× — computed, shown), then the split incl. the ESOP holders
   ("your people get paid too — vested only"), and YOUR take
   decomposed into cash / locked stock / maybe-earnout.
3. **THE FINE PRINT, READ ALOUD** — the game generates SOME offers
   fishy on purpose (earnout under buyer control, long lockups,
   retention carved from the founders' share, low-ball with expiry
   pressure); the desk names each flag in red. Learning to read the
   small lines IS the lesson.
4. **WHO CAN SAY NO** — the powers, resolved from what was SIGNED at
   the raise: protective provisions (an investor veto can block the
   sale you want), drag-along thresholds (≥N% of preferred accepting
   can force the sale you don't), board composition. Taught
   explicitly: the term sheet decided your exit freedom years early.
Resolution: ACCEPT (two-tap; no-shop freezes the raise) / NEGOTIATE
(one counter, priced by the world) / DECLINE (offers can sour or
return higher; the street hears). Engine: offer records extend the
board lane's M&A offers with structure {cash, stock+lockup, earnout+
controller, retention}, waterfall executor, powers checks
(protective/drag-along fields on instrument records, set at signing).

## THE BINDER PORTRAIT (owner, 2026-08-26: "an actual transparent-
## background illustration of an actual binder with the name on top —
## the big business owner binder, stick-out colors, papers not well
## ordered")

1. **What**: a transparent-background PNG illustration of a chunky,
   well-used ring binder — kraft cover, FOUR thick index tabs
   sticking out in the group colors (sage, coral, blue, yellow),
   papers poking out untidily, a taped paper label on the front. In
   the game's hand-drawn style (flat cartoon, wobbly felt-pen
   outlines, flat fills, game palette, no gradients).
2. **The name is overlaid, not generated**: the illustration is asked
   for with a BLANK label (image models garble text); the engine
   draws the company name onto the label in PatrickHand. Reliable and
   re-nameable for free.
3. **When**: generated ONCE at run start (the generation-once law);
   regenerated only if the company is renamed or a pivot changes its
   nature. Cached in user://; the drawn CSS-style kraft cover (the
   already-locked frame art) is the instant placeholder and permanent
   fallback — the binder never waits on an image.
4. **Where (owner correction)**: the portrait sits ON THE ROOM
   PAINTING, composited at the BOTTOM-LEFT of the scene as a nice
   3D-illustrated object — and it REPLACES the "binder tab" button:
   pressing the binder object opens the binder. Affordances: slight
   lift/tilt on hover; when the binder has attention items, the red
   "!" sticker appears on the object (the red system reaches the
   scene). The scene prompt already keeps the lower-left calm, so
   the composite has a home. Style: soft-shaded 3D-illustrated look
   while staying in the game palette. Fallback: a drawn mini-binder
   (same silhouette) when no portrait exists. The name overlay
   applies at this size too (scaled PatrickHand on the label).
5. **Pipeline split (the "gpt image 2" ruling)**: TRANSPARENT-
   BACKGROUND assets go through the OpenAI images API (the game
   already holds the OpenAI key) — request model "gpt-image-2" and
   fall back to "gpt-image-1" if the API reports no such model;
   `background: transparent`, PNG. Scene paintings (rooms, garden
   plots) stay on the Atlas/Seedream v2 path. One new pure-transport
   client call, same no-Core-types law as the rest of the LLM layer.

## THE THREE BINDER ILLUSTRATIONS (owner, 2026-08-26; refined: "3
## images that are more illustration than logo")

Three DISTINCT transparent-background ILLUSTRATIONS — vignette style
is correct, not a defect; never stark logo marks — generated once at
run start with the portrait (same OpenAI ladder, same client), NO TEXT
in any image (names are always engine-overlaid), regenerated only on
rename/nature-pivot:
1. **THE LABEL illustration** (user://company_logo.png, name kept for
   compat): a small scene of the business itself — sits on the binder
   object's taped label above the overlaid company name.
2. **THE MAKE illustration** (user://illus_make.png): the thing the
   company makes, as an illustrated object/scene — sits on the WHAT
   WE MAKE hero plate.
3. **THE PITCH illustration** (user://illus_pitch.png): the company
   as a venture — an optimistic little scene for THE RAISE desk's
   header.
Fallbacks so identity never waits: label → drawn MONOGRAM (initials
in Baloo on a drawn chip); make → the version plate alone; pitch →
plain header. One getter per surface returns texture-or-fallback.
QA law (owner): generated images are TESTED, not assumed — the QA
wave verifies portrait + all three illustrations (exists, PNG-valid,
alpha true, silhouette non-empty), fires the garden/room generation
live at least once, and captures every fallback rendering in both
engines.

## PROMPT QUALITY PASS (owner: "prompt update WITH PA help")

Before ship, the updated prompts (world-gen birth blocks, adjudicator
structural-ops section, clarify) get a prompt-assistant review pass:
current text + observed failure modes in, improvements applied, then
byte-twin re-copy. Known failure modes to feed it: stray non-Latin
characters near maxLength boundaries, the leaked commentary tail at
the end of adjudicator.txt (delete first), op-choice ambiguity between
set_budget and the structural ops, price-book law adherence.

## LONG-TEXT + SCROLLING QA LAW (owner)

The QA wave tests long content explicitly, in both engines: max-length
generated strings everywhere they render (topics one-liners at 110,
spend lines at 28/60, company names on the label/plates), a 10-line
spend book, rung-2/3 states (40 employees, 12 sites, 34 features),
an 80-week history, a full events inbox — asserting: sheets scroll
while hero + total stay pinned, panes never clip mid-glyph, nothing
overflows its card, fold rows appear at their thresholds, and the
label overlay truncates gracefully.
