# RUNWAY! — Art Direction & Complete Asset Manifest (v1.0)
*Everything to draw, with specs, naming, counts, and priority tiers. Totals: **P0 vertical slice ≈ 85 images · P1 MVP ≈ 300 · P2 v1.0 ≈ 640+**. The style is chosen to make this volume feasible for one artist.*

## 1. Style spec (locked)
- Thick, uneven hand-drawn ink outline (3–6px at working res); flat fills; NO gradients or soft shadows; hatching allowed for shading.
- Palette: paper cream `#F2EAD3` (bg), ink black `#1E1E1E`, sage green `#8FA582`, coral accent `#E86A5C`, muted blue `#6E8CA0` (rare, UI/water/tech), 2 grays. Max 7 colors on screen.
- Consistent top-left light; characters ~4 heads tall, big hands, dot-or-line eyes (emotion via eyebrows + posture).
- Wobble aesthetic: 2-frame "boiling line" alternates acceptable in place of full animation.
- **Deliverable format:** PNG, transparent bg (except full backgrounds), sRGB. Work at 2× listed target sizes. Source files layered (PSD/Procreate), flattened export.
- **Naming convention:** `cat_subject_variant_state.png` all lowercase snake_case → `chr_hacker_walk_02.png`, `arn_garage_bg.png`, `itm_laptop.png`, `card_rust_rewrite.png`, `end_foosball_growth.png`, `ui_meter_runway.png`.

## 2. Characters — sprites (target 512px height @2×)
**Per-character animation set (the "standard rig", 14 frames):** idle ×2 (boil pair) · walk ×4 · carry-light ×2 · carry-heavy ×2 · panic-run ×2 · grab ×1 · collapse/exhausted ×1.
**Per-character portrait set (768px, bust, 6):** neutral · happy · stressed · cooked (burnout) · furious · devastated.
| Set | Characters | Sprites | Portraits | Priority |
|---|---|---|---|---|
| Founder archetypes | Hacker, Hustler, Dropout, Ex-FAANG | 4×14=56 | 4×6=24 | P0: Hacker only (20) · P1: all |
| Cofounder/staff pool | 12 distinct characters (mix roles/genders/vibes) | 12×14=168 | 12×6=72 | P0: 2 chars · P1: 6 · P2: 12 |
| Burnout overlays | eye-bags, coffee-jitter lines, "plant friend" | 6 accessory overlays | — | P1 |
**NPC portraits only (no walk rigs; 768px, 2 emotions each):** 8 investor archetypes, landlord, journalist, Camp partner, IRS agent, lawyer, rival founder, acquirer exec, celebrity angel = 16×2 = **32** (P1: 8 · P2: all).
*Character subtotal: ~360 images (P0 ≈ 26).*

## 3. Arenas & environments
**Full backgrounds (2560×1440 @2×, layered: back wall / floor / deposit zone):**
apartment (Act 0) · garage · coworking · first office · startup floor · HQ · NYSE finale · "moving out" gloomy variants ×2 = **9 backgrounds** (P0: apartment+garage · P1: +coworking · P2: rest).
**Furniture & upgrade sprites (item-placed, ~300–600px):** 12 upgrades × 5 eras + 15 generic props (desks, chairs, boxes, plants, cables) = **75** (P0: 10 · P1: 35 · P2: 75). Each upgrade needs 1 sprite + optional "broken/on-fire" variant for crises: +20 variants (P2).
**Crisis dressing:** fire/smoke overlay set (4), water leak (2), police tape (1), moving boxes (3) = **10** (P1).
*Environment subtotal: ~114.*

## 4. Items (grabbables) — 256–384px, chunky silhouette-first
90 items at v1.0. Each = 1 sprite; 12 "heavy" items also need a 2-hand carry pose accounted in character rigs (no extra item art). Categories & counts: liquid/money 8 · tech 18 · social/sentimental 12 · vice/morale 14 · documents 10 · food/coffee 8 · industry-specific packs (5 industries × 4) 20.
Priorities: **P0 the starter 15** (laptop, savings jar, idea napkin, roommate-token, energy drinks, goodwill, dignity, gym card, paddle, dad's server, hoodie, bus pass, textbook, guitar, houseplant) · P1: 40 · P2: 90.
*Item subtotal: 90.*

## 5. Event card illustrations (800×600 @2×)
The biggest single category. **Composition system to control volume:** each card = 1 of 30 reusable *scene plates* (garage desk night, coworking couches, boardroom, doorway, server closet, stage, rooftop, hospital...) + character portrait overlay + 1 prop accent. Only "signature" events get bespoke full illustrations.
- Scene plates: **30** (P0: 8 · P1: 18 · P2: 30)
- Bespoke signature cards: **40** (P0: 5 · P1: 20 · P2: 40)
- Prop accents (small, reusable): **25** (P1)
- The Door: door frame + 16 visitor silhouettes/reveals: **17** (P0: 4 · P1: 10 · P2: 17)
*(LLM-generated events auto-select scene plate + portrait + prop via tags — zero new art per generated event.)*
*Card subtotal: 112.*

## 6. Ending cards (1200×900 @2×, poster-style with title lettering)
Illustrated death/exit cards, the collectible gallery + share images. Hand-lettered title baked in. **P0: 8 · P1(MVP): 20 · P2(launch): 60** · post-launch to 120. Plus gallery locked-card back ×1, card frame ×3 rarities.
*Ending subtotal: 64 at v1.0.*

## 7. UI kit (SVG-like crisp PNGs, drawn style)
Meters: runway ticker, cap-table donut, product bar, traction curve, hype flame, morale face, burnout thermometer (7 × 3 states = 21) · buttons & frames (12) · event card frame + choice buttons (6) · negotiation slider + walk-risk dial (5) · week/turn banner, act title plates ×7 (8) · scramble HUD: timer, capacity slots, deposit arrow (6) · autopsy diagram nodes/edges/skull (8) · Twitch overlay: vote bars, chat badge, tally frame (6) · menus/logo/wordmark ×2/settings icons (14) · cursor set (3).
*UI subtotal: ~89 (P0: 30 · P1: 60 · P2: 89).*

## 8. VFX & misc
Sweat drops, panic lines, zzz, coffee steam, money burst, equity-slice animation frames, confetti (IPO), smoke loop, "sold!" stamp, rain-of-termsheets = **~25 small sheets** (P0: 6 · P1: 15 · P2: 25). Steam capsule art set (header/capsule/library, 5 sizes) + 10 screenshot frames + trailer endcard = **16** (P1–P2, marketing).

## 9. Production notes for the artist (you)
- Draw silhouette-first; every item must read at 64px on a compressed stream.
- Batch by category (all items one week, all portraits the next) to keep line weight consistent; make a 1-page style sheet from the first 5 approved images and pin it.
- Export both @2× and @1×; keep a single `palette.ase` swatch file as law.
- Integration handshake: drop PNGs matching the naming convention into `/assets/incoming/`; an import script (TODO 2.9) auto-slices, registers to the content database, and hot-reloads in-engine — you should see your art in-game within 60 seconds of export.

## 10. Roll-up
| Category | P0 slice | P1 MVP | P2 v1.0 |
|---|---|---|---|
| Characters & portraits | 26 | 160 | 360 |
| Arenas & furniture | 14 | 55 | 114 |
| Items | 15 | 40 | 90 |
| Event cards system | 17 | 60 | 112 |
| Ending cards | 8 | 20 | 64 |
| UI | 30 | 60 | 89 |
| VFX & marketing | 6 | 20 | 41 |
| **Total** | **≈116** | **≈415** | **≈870** |
*(Totals exceed the §heading estimate once variants counted — the composition systems in §5 are what keep this feasible. Cut order if needed: staff pool 12→8, endings 60→40, scene plates carry more weight.)*
