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
