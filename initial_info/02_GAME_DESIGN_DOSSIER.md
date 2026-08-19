# RUNWAY! — Game Design Dossier (v1.0)
*The deep-reference bible. The PRD says what; this says exactly how. All numbers are first-pass tuning values for the economy spreadsheet — expect ±50% after playtesting.*

---

## 1. Tone bible
- Satire with love: HBO Silicon Valley energy, written by someone who lived it. Real founders should wince-laugh.
- Comedy from specificity: "the investor replies only in Loom videos", "the intern deployed to prod from a plane".
- No real company or person names ever (parody archetypes only: "Combinator Camp", "Sandhill Partners", "Goliath Corp").
- Deaths are punchlines, never punishments. The player laughs AT their corpse, then hits Restart.
- Writing format: situation ≤ 60 words, choices ≤ 8 words each. Readable on stream.

## 2. Office eras — full spec
Weekly turn = 1 in-game week. Rent+ops burn is separate from salaries.

| Era | Rent+ops/wk | Staff cap | Scramble arena size | Era gate (to advance) | Signature crisis scrambles |
|---|---|---|---|---|---|
| Garage | $150 | 2 (founders) | 1 screen | MVP built (Product ≥ 60) + first user | Laptop dies; Parents reclaim garage |
| Coworking | $600 | 4 | 1.5 screens | Launch + $1k MRR or 5k users | Hot-desk eviction; Demo-day scramble |
| First Office | $3,000 | 9 | 2 screens | PMF flag + Seed ≥ $1M raised | Landlord lockout; Server-closet fire |
| Startup Floor | $12,000 | 20 | 3 screens | Series A + $100k MRR | Due-diligence scramble; Layoff day |
| HQ | $45,000 | 40 (abstracted pods) | 4 screens | Series B/C + unicorn track | Press ambush; Board-coup eve |

**Upgrades (per-era catalog, 12 max each; format: name — cost — grind effect — event hooks — scramble presence):**
Garage examples: Espresso machine — $300 — burnout decay −20% — unlocks "coffee snob cofounder" events — grabbable in fires. Second-hand server — $800 — Product build +10% — unlocks outage events — heavy (2-hand carry). Whiteboard — $100 — Raise pitch +5% — "FAKE THE NUMBERS?" due-diligence hazard — must be flipped in lawyer scrambles.
Startup Floor examples: Foosball table — $1,200 — Morale +10, Focus −10 — unlocks "Foosball-Led Growth" death track — blocks a corridor in scrambles. Nap pods, kombucha tap, security badge system, "culture wall", on-prem GPU rack (AI industry), etc. Full catalog authored in content phase (TODO 5.3).

**Demotion:** triggered by down round or missed payroll ×2 → "moving out" scramble (grab what survives; capacity halved), era −1, Morale −25, unlocks redemption-arc events.

## 3. Run structure & branching
### 3.1 Route flags (macro branches)
Set by choices; recolor decks, gates, endings:
- **Funding track:** Bootstrap / Angel / Accelerator ("Combinator Camp": −7% equity, +network flag, demo-day set piece) / VC blitz.
- **Market:** B2B grind / consumer viral gamble / enterprise whale-hunting.
- **Integrity axis:** honest ↔ "fake it" (fake-it choices are powerful and plant fraud-track time bombs — always foreshadowed).
- **Industry (run start):** SaaS / Consumer / Crypto / Deeptech / AI — swaps item skins, event decks, and death catalogs.
### 3.2 Weekly turn sequence (Grind)
1. Upkeep: pay burn; ration coffee; apply burnout drift. 2. Assignments: place each character on Build/Sell/Raise/Recruit/Rest. 3. Event resolution: draw 1–3 from eligible pool (see priority rules §6.4). 4. The Door: 40% chance/week, era-weighted visitor. 5. Meter update + foreshadow ticks. 6. Tycoon window (buy/sell upgrades) every 4th week or after windfalls.
### 3.3 Offer cadence (session-length valve)
Acquihire offers: any era, triggered by traction spikes or rival interest; value 0.3–0.8× valuation. Strategic acquisition: era 3+. Every offer is a full negotiation + a "walk away" that permanently raises stakes (Hype +, board pressure +). Declining an offer marks the save: "greed level" cosmetic tracked for the autopsy.

## 4. Cap table & economy model
- Start: founder 100%, valuation $0, cash = sum of grabbed liquid items ($4k–$25k typical).
- Standard dilutions (negotiable ±): cofounder 25–40%; Camp 7%; angel 8–15% (SAFE, $1–3M cap); Seed 15–25%; A 18–22% + board seat; B/C 12–18%.
- Option pool events force 10% carve-outs before priced rounds (a real-world gotcha = a great trap event).
- Valuation = f(Traction, Product, Hype, era) with industry multipliers; recomputed on milestone beats.
- **Score = founder% × exit value**, with style bonuses (no-VC unicorn ×2, "sold at the top" timing bonus, deaths give consolation score = lessons collected).
- Control ladder: ≥50% safe; <50% board-removal deck live; <25% "employee of your own company" ending track armed.

## 5. Characters
### 5.1 Founder archetypes
| Archetype | Scramble | Grind | Unique |
|---|---|---|---|
| The Hacker | slow, +1 carry | Build +25%, Sell −15% | Can fix any tech crisis once/era |
| The Hustler | fast | Sell/Raise +20%, Product decays | Free reroll on one negotiation/era |
| The Dropout | fastest, −1 carry | All +5%, burnout +30% faster | Parents' garage is rent-free |
| The Ex-FAANG PM | normal | Recruit +25%, starts +$15k | Hires demand less equity; "process" debuff events |
### 5.2 Staff object schema
`{name, role, competence{build,sell,raise,recruit}, salary, equity_ask, loyalty, burnout, visible_quirk, hidden_traits[1–2], scramble_sprite, reveal_conditions}`
Hidden trait examples: Secret Genius, Quiet Quitter, Rust Evangelist, Corporate Spy (rival-arc tie-in), Drama Magnet, Actually A Consultant. Traits reveal via events/burnout thresholds — the "month 6" betrayal/delight moments.
### 5.3 Burnout ladder (per character, visible in office)
0–30 fine → 31–60 *frayed*: −10% output, minor quirk animation → 61–85 *cooked*: −30%, comedic behavior events fire (rewrites backend in Rust at 3am; talks to the office plant; replies to investors in haiku) → 86–100 *gone*: quits/rage-deletes/hospitalized (foreshadowed 2+ weeks in advance, always preventable via Rest/upgrades).

## 6. Event system (authored spine)
### 6.1 Event card schema (canonical JSON — shared by authored AND LLM content)
```json
{
  "id": "evt_garage_rust_rewrite",
  "tier": "authored",
  "era": ["garage","coworking"],
  "weight": 3,
  "requires": {"items_any":["itm_whiteboard"], "flags_all":[], "meters":{"burnout_max_gte":61}, "chars_with_trait":["rust_evangelist"]},
  "excludes": {"flags_any":["no_tech_team"]},
  "foreshadowed_by": ["evt_late_night_commits"],
  "art": "card_rust_rewrite",
  "title": "The Great Rewrite",
  "body": "You wake to 4,000 lines of Rust and zero working features. {char} says it's 'basically done'.",
  "choices": [
    {"label":"Let them finish it", "effects":[{"op":"product_delta","v":-15},{"op":"char_burnout_delta","who":"{char}","v":-20},{"op":"set_flag","v":"rust_backend"}], "weight_future":["evt_rust_pays_off"]},
    {"label":"Revert everything", "effects":[{"op":"product_delta","v":5},{"op":"char_loyalty_delta","who":"{char}","v":-25}]},
    {"label":"Ship both. Chaos.", "effects":[{"op":"random_outcome","table":"chaos_ship"}]}
  ]
}
```
### 6.2 Effect-op vocabulary (bounded whitelist — the LLM safety keystone)
~40 ops, each with hard numeric clamps. Categories: meter deltas (`cash_delta` ±$50k by era, `product_delta` ±20, `traction_mult` 0.7–1.5, `hype_delta`, `morale_delta`), character ops (`char_burnout_delta`, `char_loyalty_delta`, `reveal_trait`, `char_leaves`, `add_candidate`), cap-table ops (`equity_delta` clamp ±5% outside negotiations, `valuation_mult` 0.8–1.3), flow ops (`set_flag`, `arm_timebomb{weeks,event}`, `weight_future`, `trigger_scramble{id}`, `spawn_offer{type}`), item ops (`grant_item`, `destroy_item`). **No op can directly kill** — deaths only via armed, foreshadowed timebomb events.
### 6.3 Item schema
`{id, name, tags[liquid|tech|social|vice|sentimental|heavy], carry_cost, spawn:{arenas,prob}, grind_passives[], event_hooks[]}` — launch catalog: 90 items. Starter 15 for the vertical slice: laptop, savings jar ($8k), idea napkin, roommate(=cofounder candidate), energy drinks, girlfriend's goodwill, dignity, gym membership, ping-pong paddle, dad's old server, hoodie of confidence, bus pass, textbook (sellable), guitar (morale/vice), houseplant (burnout confidant).
### 6.4 Deck priority each week
armed timebombs due > arc events (LLM Tier 3) > foreshadow follow-ups > requirement-matching pool by weight (freshness-decayed) > filler. Guarantee: ≥1 of the 1–3 weekly events references something the player owns/did (the causality feeling).
### 6.5 Coverage rules (anti-repetition constitution)
Every item ≥3 event hooks. Every upgrade ≥2. No death without ≥2 foreshadow beats. Authored pool sized so a 3-act run repeats <10% of seen events across two consecutive runs (analytics-verified).

## 7. LLM Simulation Engine — full architecture
### 7.1 Data flow
```
RunState → digest (≤600 tokens) → [Tier 3 @run/act start: Director call]→ arcs[]
Weekly loop: prefetcher keeps pool of 6–10 validated Tier-2 cards
  → prompt(digest + arc directives + 3 few-shot authored cards + schema)
  → Claude structured output (event JSON) → Validator (schema✓ clamp✓ balance-lint✓ tone-filter✓)
  → accept→pool | reject→retry(1)→fallback authored
```
### 7.2 API usage
- Endpoint: Anthropic Messages API with **structured outputs** (`output_config.format` with the event JSON schema; GA — guarantees parseable, schema-valid responses).
- Models: Tier 2 = Haiku-class (fast/cheap, high volume); Tier 3 = Sonnet-class (1–6 calls per run).
- Prefetch during scrambles and between weeks; pool never blocks UI; offline/no-key → authored-only mode, invisible seam.
### 7.3 Tier-2 prompt skeleton (system)
"You write event cards for RUNWAY!, a satirical startup survival game. Voice: dry, specific, wince-funny; ≤60-word bodies; ≤8-word choices; never real companies/people; never break the fourth wall. You receive the run state and active narrative arcs. Output ONLY a card matching the schema. Effects must use ONLY the listed ops within their ranges; choices must be genuine dilemmas (no strictly-correct option); reference at least one specific item, character, or recent choice from the state; plant foreshadowing when arc directives ask for it."
### 7.4 Tier-3 Director output schema (arcs)
`{arc_id, kind:[rival|press|inner_circle|market|regulator], premise, actors[{name,archetype,agenda}], beats[{act, directive, suggested_timebomb}], escalation_rule, resolution_conditions}` — e.g. rival "Zenith Labs, a well-funded clone that ships fast and copies your launches; beats: appears Act2, poaches Act3, forces exit decision Act4." Arcs are injected as directives into every Tier-2 prompt → the run feels authored.
### 7.5 Validation pipeline (local, deterministic)
1. Schema (belt-and-suspenders re-check). 2. Clamps + op whitelist. 3. Balance lint: sum EV of best/worst choice within era bounds; no free lunches, no unwinnable picks. 4. Requirements sanity (references must exist in run state). 5. Tone/content filter: blocklist (real brands/people, slurs), length caps. 6. Dedup vs. run history (embedding or trigram similarity). Reject rate telemetry; >15% rejects → auto-widen few-shot set.
### 7.6 Cost & keys
Per 3-hour max run: ~120 Tier-2 calls (~1k in / 400 out tokens) + 4 Tier-3 ≈ well under $1 at Haiku-class pricing (verify current pricing at docs.claude.com before launch). MVP: player API key (macOS Keychain; settings pane with test button; clear "works fully without a key" copy). v1.0 decision: dev proxy (auth via Steam ticket, monthly allowance, server-side key) — evaluate ops burden vs. adoption data from MVP.
### 7.7 Determinism & fairness
Daily seeded runs + leaderboards: authored deck only. Normal runs: every accepted card is written into the run record (autopsy/replay exactness, bug repro). Generated cards are marked internally, never visibly ("blind test" ship-gate: players can't reliably tell).

## 8. Scramble design
- Controls: WASD/arrows or mouse-hold to move; single grab/drop key; carry capacity 2–4 slots (items have carry_cost; heavy items slow you / need 2 hands = both slots).
- Readability at speed: items outlined + labeled on hover-proximity; deposit zone glows; final-10-seconds heartbeat + desaturation.
- Crisis scrambles are armed by state (per §6.2 `trigger_scramble`) and always foreshadowed ≥1 week ("the server closet smells like burning hair").
- Anti-mastery variance: item spawn jitter, occasional layout mutations (the foosball table you bought IS the obstacle), rare golden items.
- Every scramble ends with a 3-second "what you got / what you left" tableau — the clip moment.

## 9. Endings catalog (structure + launch sample)
Format: `{id, name, art_card, trigger, autopsy_template, rarity, gallery_flavor}`. Categories: Cash deaths (Ramen Zero; Payroll Friday), Team deaths (Cofounder Rage-Quit; Everyone Followed Dave), Board/equity deaths (Fired From Your Own Company; The 2% Founder), Integrity deaths (The Spreadsheet Was Load-Bearing; SEC Speedrun), Vice deaths (Foosball-Led Growth; Conference Circuit Ghost), Market deaths (Goliath Ships Your Roadmap; The Hype Cliff), Exit endings (Acquihired For Parts; Sold At The Top; The Bootstrap King; IPO variants by final founder%: Ring The Bell / Rich But Powerless / The 51% Miracle). Launch: 60 authored; post-launch to 120+.

## 10. Balancing first-pass numbers
- Founder personal burn: $500/wk (ramen) to $1.5k/wk (dignity intact).
- Salaries/wk: junior $1.2k, senior $2.5k, exec $4k (or −50% for +0.5–2% equity).
- Milestone valuations (SaaS baseline): MVP $1M → launch+revenue $4M → PMF $12M → A $40M → B $150M → C $500M → unicorn $1B+ → IPO 1–3× last round (roadshow performance).
- Coffee: 1 unit/person/wk; shortage = burnout +10/wk. Espresso machine halves consumption. Yes, coffee is literally the food-ration mechanic.
- Target death distribution in playtests: 45% cash, 20% team, 15% board, 10% integrity timebombs, 10% other. First-run median death: week 14–20 (Act 1–2). These are tuning dials, not goals.

## 11. Twitch integration spec
- Connect flow: streamer auths via Twitch OAuth in settings; overlays a "CHAT MODE" badge.
- Vote moments: any negotiation (accept/push/walk), Door decisions, company name at run start, idea reroll. 20-second vote windows with on-screen tally bars.
- "The Market" (launch weeks): chat message velocity + sentiment keywords → virality multiplier 0.8–1.4×.
- Channel-point redemptions (configurable): trigger crisis scramble, gift coffee crate, send a troll applicant, rename an NPC.
- All chat effects logged in run record + autopsy credits chat ("This death sponsored by: user Kappa123").
