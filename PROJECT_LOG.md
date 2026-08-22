# RUNWAY! — Project Log & Decisions

Living document. Every locked decision, built artifact, and open thread lives here so any session can pick up the project cold. Update this file whenever a decision is made or reversed.

Last updated: 2026-08-17

---

## 1. What this project is

Startup-survival roguelike per `initial_info/` (01_PRD, 02_GAME_DESIGN_DOSSIER, 03_ASSET_MANIFEST, 04_MASTER_TODO). One-liner: *You don't build a startup. You survive one.* Core loop: real-time SCRAMBLE (grab items, 60–90s) alternating with turn-based GRIND (week-by-week survival), thin tycoon layer, office eras as progress bar, score = founder % × exit value, 120+ collectible illustrated deaths.

## 2. Locked decisions (chronological)

| # | Decision | Detail | Status |
|---|---|---|---|
| D1 | Build order: style first | Lock 1–2 great images before any code (user override of TODO's paper-prototype-first) | DONE |
| D2 | Engine: Godot 4 from day one | No web prototype; GDScript; sim core still engine-agnostic in design | ACTIVE |
| D3 | Art style locked | Wobbly felt-pen ink line + flat fills, no gradients/soft shadows, on paper cream. Xiaohei-descended, adapted for game (colorful, cream bg instead of white) | LOCKED |
| D4 | 7-color palette locked | Cream `#F2EAD3` (bg) · Ink `#1E1E1E` · Coral `#E86A5C` · Yellow `#F4B942` · Sage `#8FA582` · Blue `#6E8CA0` · White `#FFFFFF` (eyes/highlights only) | LOCKED |
| D5 | Title screen concept locked | `prompts/concept_01_title_screen.json` — founder sprinting down a runway of burning dollar bills, laptop + houseplant, hand-lettered RUNWAY! Kept because it animates well. 5 alternative concepts and 5 alternative art styles were explored and REJECTED (files kept in `prompts/` for archive; deletion pending user call) | LOCKED |
| D6 | Character: "blob v2" | User rejected 5 replacement characters ("keep the black blob, tweak it"). Base = solid ink-black bean + 4 ownable marks: (1) single cowlick spike — droops with burnout, stands at milestones; (2) permanent forward lean — perpetually mid-hustle; (3) mismatched white oval eyes, left bigger; (4) tiny cream sneakers, one lace untied. Blank deadpan face always: no mouth/eyebrows/emoji. Archetypes differ ONLY by props + one accessory (+2 written exceptions: Dropout's lean reads as slouch; Ex-FAANG PM stands straight, both laces tied) | LOCKED |
| D7 | Prompt format | All image prompts follow the Nano Banana JSON spec in `image_prompt_gen.md`: `{reasoning, prompt}` with full schema. Character block carries a `consistency_rule` reused verbatim across assets | LOCKED |
| D8 | Image generation route | Nexus external tool **GPT Image 2** (org `nexus-organisation`), not local API calls. Middleware NexusGPT/nano-banana on Railway handles OpenAI call + S3 upload | LIVE |
| D9 | Layer decomposition route | Direct AtlasCloud registration (user chose simpler path over extending middleware; quality validated, fallback not needed) | LIVE |
| D10 | Batch asset strategy | Generate multi-sprite sheets with GPT Image 2 (single-call style coherence), decompose with Seedream into transparent per-sprite layers. Caps: 12–16 characters/2K sheet (512px spec), 20–30 only for small items; labeled grid with gutters; highest-value use = 14-frame character rig as one sheet. **PARKED until core game runs** (user, 2026-08-17) | PARKED |

## 3. Built artifacts

### Prompts (`prompts/`)
- `concept_01_title_screen.json` — CANONICAL title screen, blob v2 applied. Rendered successfully → `~/Downloads/runway-title-screen-concept01.png` (1536×1024, high quality).
- `concept_02_founder_archetypes.json` — CANONICAL 4-archetype lineup (Hacker / Hustler / Dropout / Ex-FAANG PM), blob v2 applied. Not yet rendered.
- Archive (rejected, kept for reference): `title_A/B/C_*.json` (concept variants), `title_style_A/B/C_*.json` (style variants), `char_A–E_*.json` (character candidates).

### Nexus org tools (profile `nexus-organisation`, org `org_3F7cXucWtHdRlohFvvxAeeiGxLj`)
| Tool | ID | Endpoint | Auth | Actions |
|---|---|---|---|---|
| GPT Image 2 | `9deb3d93-dd48-472e-9512-06cbf65bb5c9` | `https://nano-banana-production-e03b.up.railway.app` | service_http, custom header `x-openai-api-key` | `generateImageOpenAI`, `editImageOpenAI` |
| Layer Decomposition (Seedream) | `c7d7f5af-600e-4c3f-9224-0a83383eaced` | `https://api.atlascloud.ai` | service_http, bearer | `startLayerDecomposition`, `getPrediction` |

Specs: GPT Image 2 spec lives in the nano-banana repo (`openapi-gpt-image-2.yaml`); decomposition spec at `openapi-layer-decomposition.yaml` in this repo. Keep both — Nexus does not return stored specs.

Usage notes:
- Image gen: pass the prompt object of a prompt JSON **stringified** as `prompt`; `quality: high`, `size: 1536x1024`; takes ~2 min → run backgrounded. Result is a signed S3 URL, **expires in 1 h — download immediately**.
- Decomposition: model `bytedance/seedream-v5.0-pro/layer-decomposition`; numbered element list in `prompt` works well; async job ~90 s, poll every 3–5 s; outputs[0] = base image with removed elements inpainted, rest = transparent PNG cutouts cropped to content (~2K upscale). Atlas URLs not permanent — download immediately.
- Validation run: title screen → 7 perfect layers in `~/Downloads/runway-title-layers/` (base, sun, runway strip, fire, papers, character, typography).

## 4. Current phase — CORE GAME (started 2026-08-17)

Goal: the important parts of the game running in Godot 4 before any more art production.

| # | Decision | Detail | Status |
|---|---|---|---|
| D11 | LLM layer is live in-game code from day one | User: "as it will be live part of game we need it integrated in the code". Runtime Simulation Engine in GDScript, key from gitignored `game/.env` (`.env.example` documents it). Provider-agnostic: OpenAI (`response_format: json_schema`) or Anthropic (`output_config` structured outputs), auto-detected from which key is set. Tier-2 generated cards share the authored card schema, pass a local validator (op whitelist + clamps + lengths), prefetch between weeks, invisible fallback to authored pool. Currently on the OpenAI test key + `gpt-5-mini` | LIVE |

| D12 | 2.5D paper-diorama, not full 3D | User found the placeholder build "garbage" and asked for full 3D; challenged (2D economics + pipeline + stream readability), user chose the middle path: hand-drawn flats at real depths in a Godot 3D scene, springy chase camera, billboarded sprites. Art style + generation pipeline fully survive | LOCKED |
| D13 | Demo sprites generated via the pipeline | Sheets on flat magenta `#FF00FF` + deterministic grid-slice + chroma-key (GPT Image 2 rejects `background:transparent`). Produced: 15 item sprites, 8 founder pose frames (idle boil-pair, run pair, carry pair, grab, collapse), 8 furniture flats, apartment wall, floorboard tile, garage backdrop. Slicer lives inline in session; sheets in scratchpad `gen/` | DONE |

Milestone 1.5 — 2.5D + real art + juice (2026-08-17): scramble rebuilt as `scramble3d_screen.gd` (Node3D diorama: textured floor/back wall, furniture flats, glowing door with real door sprite, Sprite3D player with pose-frame animation + accel/friction movement + hop-bob + lean + squash-on-grab + camera spring/shake + heartbeat vignette + synthesized SFX). Grind restyled: Patrick Hand font (fetched from fonts.gstatic — GitHub raw serves LFS pointers, not TTFs), garage backdrop, ink-bordered card with flip-in, styled buttons, meter chips, cash ticker, floating deltas, death/win stings. SFX synthesized procedurally to `assets/sfx/*.wav` by a python script (no external audio assets).

Milestone 1.6 — dense diorama (2026-08-17, user: "need MANY more sprites, and better setup"): 31 more sprites generated/sliced (15 clutter props, 8 wall-dressing pieces, 8 kitchen fixtures + floor decals). Scene now data-driven: `mounted` (flat on the back wall: window, poster, clock, shelf, calendar, whiteboard, fairy lights, mirror), `standing` (26 billboarded flats: furniture + kitchen row + scattered clutter), `decals` (rug, welcome mat, paper scatters flat on the floor), light pools (lamp glow, window moonlight), doors mounted on both side walls. Countdown intro ("YOU QUIT YOUR JOB TONIGHT. 3·2·1·RUN!") gates the timer. Player z clamped in front of the furniture row. 46 sprites total in `assets/sprites/`.

Milestone 2 — THE FOUNDER DRAFT (2026-08-17, user rework of run start): title → **Founder Draft screen** → scramble → grind. One screen, three picks, live cap-table donut: (1) archetype — 5 cards (Hacker/Hustler/Dropout/Ex-FAANG PM/**Ex-Consultant**, user addition) each with a custom-drawn radar chart over Build/Sell/Raise/Recruit/Grit, starting-cash modifier, scramble stats (speed/carry), perk; (2) structure — solo (100%, weekly morale bleed) / 1 cofounder (−30%, covers weakest stat) / 2 cofounders (−45%, covers two); (3) first money — bootstrap / F&F (+$15k/5%) / angel (+$50k/12%). Objective line on screen: "Payout = YOUR % × exit value. Optimise it." Data in `data/archetypes.json`; screen `founder_draft_screen.gd` (RadarChart + CapTableDonut are custom `_draw` controls). Sim wiring: Build drives weekly product, Sell drives user growth, Grit restores morale, solo bleeds morale, archetype speed/carry applied in scramble, `payout_today()` (rough valuation) shown on the autopsy. 5 archetype portrait sprites generated (`chr_arch_*`). Design guardrails: setup stays <60s; scramble kept after draft (signature loop + tutorial; archetype-modified).

Milestone 3 — FOUNDING SIMULATOR (2026-08-17, user rework): the timed Act 0 scramble is CUT from the run flow (scramble code kept for future crisis scrambles); run start is now a two-page untimed setup. Page 1 "WHO QUITS THEIR JOB TONIGHT?": 5 archetype cards with ANIMATED 4-frame idle loops (`chr_loop_<id>_01..04`, generated as 2x2 sheets — Hacker types/sips, Hustler paces two phones, Dropout rocks the skateboard, PM does the sticky-note ritual, Consultant laser-points), plus company identity: name + "what it does" text inputs with a 🎰 reroll idea generator (local combinator, no real brands). Company name/idea flow into the LLM digest and Tier-2 system prompt so generated events are business-specific; grind header shows the company. Page 2 "THE FOUNDING": free-form cap-table builder — add up to 4 cofounders, each with role (Technical/Business/Design/The Idea Friend), commitment (full/part-time), per-head equity (+/- 5%), vesting toggle; live multi-slice donut; "TRAPS YOU ARE ARMING" panel encoding YC founding canon: full-timer under 10% → resentment; part-timer ≥15% → flake; Idea Friend ≥10% → idea tax; NO VESTING → shares walk with them; ≥3 cofounders → senate hearings; <50% day one → outvoted; near-equal+full-time+vested → healthy_split bonus. Traps become flags at launch; `data/events/founding_traps.json` (9 events) delivers the consequences — incl. the vested vs unvested walkout fork (cliff clawback vs ghost equity forever). New `clear_flag` op. "PACK YOUR BAG" replaces the scramble's item choice (pick 4 slots from the 15 items, cash values apply). Roles patch competences (Technical→Build 4, Business→Sell 4/Raise 3, Design→+1 Build/Sell 3, Idea Friend→nothing — that's the joke; part-time = half patch). 19 events total now. Future thread (user): more cofounder/upgrade sprites + animations as systems grow.

Milestone 4 — CHARACTER SELECT AS A GAME SCREEN + VIDEO-LOOP FOUNDERS (2026-08-18): user demanded final-product feel ("not a SaaS website") and video-based animation. (a) Founder idles now come from Seedance image-to-video (`bytedance/seedance-2.0-fast/image-to-video`, first_frame=last_frame=portrait for seamless loops, 4s 720p) → ffmpeg 12fps extraction → 48 chroma-keyed frames per founder with union-bbox registration (`process_video_frames.py`) → the game's frame loader (runtime plays frames because this ffmpeg lacks a theora encoder for Godot-native video; visually identical). 240 frames live. (b) Select screen rebuilt as a stage: generated theater backdrop (`assets/env/stage.png` — curtains/spotlight/stars), hero founder center-stage with sway/breath float/elliptical shadow, hero swap slide+squash transitions, radar sweep-in, name stamp-in, fighting-game dock with raised selected chip + hover lift, twinkling stars, breathing spotlight pool, pulsing LOCK IN, "LOCKED IN" stamp, curtain-wipe page transitions, arrow-key/ENTER controls. Arcade "NAME YOUR STARTUP" page; founding page dimmed + opaque ink panels + styled dropdowns. (c) CRITICAL ENGINE FIX: Controls parented to a plain Node get no layout pass — full-rect-anchored children collapse to 0×0 (why backgrounds were invisibly cream); `main._swap` now sizes swapped screens to the viewport explicitly. (d) DEV LOOP: `RUNWAY_SHOT=<dir> godot --path game` runs an autopilot that walks title→select→name→founding, saves viewport PNGs, quits — screenshot-driven iteration without macOS screen-recording permissions; used it to find/fix: cone bleeding through translucent panels, SaaS-default dropdowns, star-over-donut, clipped hints, detached shadow, unreadable disabled buttons.

Milestone 5 — AWARD-BAR PASS (2026-08-18, standing directive: "every screen must be the award-winning version of itself; think heist crew, not spreadsheet"): (a) LIVING TITLE — key art rebuilt from its decomposed layers, all animated: flickering money-fire + rising embers, founder run-cycle with random jumps + squash landings, tumbling papers, idling sun, jiggling hand-lettered title, pulsing PRESS ANY KEY, slow cinematic breathe; graceful fallback to flat art. Typography layer auto-split (title block vs press-any-key) by alpha-gap scan. (b) SELECT readability — radar chart KILLED (designer-brain) for chunky stat pips that fill on selection; money as plain words ("$33,000 / IN THE BANK, DAY ONE"); PERK labeled; open borderless panel, type sizes ~doubled; speed/bag line removed (scramble cut). (c) FOUNDING = heist-crew assembly — dropdown rows KILLED; crew of standing character cards: YOU·CEO card (your slice huge, droops + "outvoted!" under 50%), per-cofounder cards with role cycler ‹›, FULL/PART-TIME toggle, − % +, VESTED✓/NO VESTING⚠ badge (card border goes coral), mood line (thrilled/fine/resentful…), drop-in animation, bounce/shake reactions to equity changes, dashed RECRUIT slot. Cast art: `cf_<role>_<state>.png` = 4 roles × 3 states (resentful = drooped cowlick over eyes + scribble storm cloud). (d) SOUND — synthesized lo-fi menu loop (78 BPM, Fmaj7-Am7-Dm7-Gm7, vinyl crackle, swing hats; `assets/music/menu_loop.wav`, seamless AudioStreamWAV loop) plays through title+draft, fades into the run. (e) Ambience: dust motes in the beam, twinkling stars, breathing spotlight, hover-juice helper, animated donut sweeps + counting center. (f) Engine layout landmine documented in Milestone 4 hit again → all screens sized via _swap. NEXT SCREENS OWED THE SAME BAR: grind (in-world desk framing, styled events), autopsy (shareable card), money-button copy layout.

Milestone 6 — TITLE = FULL-SCENE LOOP VIDEO + FOUNDING FLOW SPLIT + REAL DILUTION (2026-08-18): (a) Title pivot per user: per-component sprite regeneration proved HAZARDOUS (regenerated fire ≠ baked fire, run-cycle founder lost registration/overlapped text) → title is now ONE Seedance i2v loop of the ORIGINAL key art (first=last frame, 1080p-SR, "keep the artwork EXACTLY as drawn"), extracted to 48 full-screen frames played at 12fps in-engine (no theora codec available; frames VRAM-compress fine). Whole scene moves coherently: flames, running founder, papers, boiling lettering. Layered-sprite title kept as fallback path only. LESSON (logged as doctrine): animate a finished composition with video; use per-component sprites only for elements that must be interactive or recomposed. (b) Founding split into three full screens with the select-screen readability rules — THE CREW (heist-crew cards, recruit via "WHO DO YOU CALL?" modal with 4 labeled candidates, role fixed at recruit — no cryptic cyclers), FIRST MONEY (3 giant cards: icon, +$ amount, "−X% · dilutes EVERYONE", flavor; live preview sentence "You'd keep 53% of Mono.io · ~$83,000 day one"), PACK YOUR BAG (big item grid, items fly into the moving box, SLOTS n/4, summary strip). (c) CAP-TABLE MATH FIXED per user: founding splits 100% among founders; investors dilute EVERYONE pro-rata (founder AND cofounders × (1−X)); `equity_diluted` stored per cofounder; donut/preview/traps all use diluted numbers. Title-animation sprite sheets (fire loop, boil type, sun, paper, smoke, bill-burn, 48-frame run cycle) remain in `assets/title/anim/` — reusable for in-GAME effects (crisis fires, burning money on upkeep).

Milestone 7 — THE GARAGE VIEW + JOURNAL + LIVE DECISION ENGINE (2026-08-18, user's 60 Seconds! constitution — 7 principles logged): (a) Grind replaced by `garage_view_screen.gd`: the room IS the save file — every owned item visibly placed (ITEM_SPOTS map), money as a physical growing/shrinking pile with $ label, product = 4-state whiteboard, users = 4-state wall chart, cap-table paper pinned on wall, crew present with mood-state sprites and idle breathing, room decays with morale (trash→pizza→flies→graffiti thresholds), era badges (CAMP pennant, LAUNCHED poster) appear on flags. (b) THE JOURNAL: 60S!-style paper book overlay (regenerated with BLANK pages after ruled-lines readability complaint; large type pass) — left page: week deltas (colored), company numbers, THE PRODUCT block (idea · what×who · v0.x · customers · "consider a pivot…"), crew moods; right page: the event with choices GATED by reality (needs_item/needs_role/needs_cash → locked with reason) + WRITE YOUR OWN MOVE. (c) Decision engine on **gpt-5.6-terra** (verified live; -sol/-luna also available): free moves adjudicated with full context (crew, archetype, items, money, customers, funding path, business model, employees) → {interpreted_as, reality_check, narration, verdict, effects} — hallucinated resources are denied and called out in a visible journal note; schema-constrained both ends, engine clamps again. Live probe: "the identical-poodle incident". (d) Business axes at naming (Software/Hardware/Marketplace × Enterprise/SMB/Consumer) feed digest + adjudication. (e) THE PIVOT: journal action — new idea + axes, cost = $500+30×customer (+angel tax), product knocked to 40%, choose fight-for-customers (keep ~30%+grit·5%, −8 morale) vs clean break (+3 hype), all logged; real-pivot FUN FACTS (Slack/Instagram/YouTube/Twitter/Shopify/Netflix/Nintendo/Nokia) shown in pivot panel AND during the week-turn dread beat. (f) Dread beat: page turn → lights out + fun fact → new week reveals the room. Adjudicator prompt being authored via Nexus Prompt Assistant (thread c71e5a14, ai-task mode) for embed — verdict table (risky=high-variance), 4-factor grounding check (resource-real hard gate), belt-and-suspenders clamps.

## PROCESS RULE (2026-08-18, after two "look how shitty" catches): a screen change is NOT done until its autopilot screenshot has been READ — actually inspected, not counted. Same-class engine trap logged twice: Godot Control minimum-size lock — TextureRect.texture or Label.text set before size/expand/autowrap locks min size to content; ALWAYS configure expand/autowrap first, assign content last (or set_deferred size). Both label helpers and _shape_card fixed; THE PEOPLE rebuilt character-first (portraits + mood states + pip bars + real gesture buttons with costs/affordability/selected states).

## TASK BACKLOG (user comments 2026-08-18, tracked to completion; autonomous session ~2h authorized)
- [x] T1 WHAT / FOR WHO get their OWN full selection screen (chips off the naming page)
- [x] T2 Journal free-text input paper-native (underline style, not a grey box)
- [x] T3 Adjudication output overlap/readability + PG-13 voice guard in prompt
- [x] T4 Room-first rhythm: open on the garage, player chooses to open the book
- [x] T5 Autopsy = THE LAST PAGE: journal on a dark table, DIED/ALIVE stamp slams in with sting, the estate (payout slice big), causal chain reveals line-by-line on the right page, RUN IT BACK pulses
- [x] T6 Room items clickable: hover-scale + click spawns a fading paper note with the item's name and blurb
- [x] T7 Journal = flippable spreads, back-and-forth nav, decisions LOCK once at the end
- [x] T8 People economy: loyalty consumable drains weekly; gestures feed it; empty = they LEAVE (vesting decides the shares); departures written into the book
- [x] T9 SCENE-FIRST ASSETS validated end-to-end: pilot scene (3 crew working in the garage) generated → Seedream decomposed into 8 clean layers incl. inpainted background; layers stored at `game/assets/scenes/pilot/`; NOTE: decomposition needs a PERMANENT image URL (nexus asset), signed URLs expire mid-flow. 50+ scene batch = next production run (scene list to be designed per screen/state)
- [x] T10 Title video v2 live: money runway is a treadmill actively burning/charring at the trailing edge, founder sprints on it; 48 frames @12fps in-engine
- [x] T11 gpt-5.6-terra + PA-authored 35KB adjudicator prompt as data (full context, reality_check, interpreted_as)
- [x] T12 MONEY IS THE FOOD: 3-week starvation arc (IN THE RED n/3 in book + journal warnings, morale −6/wk + loyalty −10/wk while starving, death at 3); pulsing red room vignette when cash < 2×burn, coral money label; recovery line when back in the black
- [x] T13 THE WORK spread (journal p3 of 5): PRODUCT/MARKETING/SALES each with 2 presets + own-plan free text; presets deterministic (sprint/polish/post log/outreach/demos/chase invoices — some set chain flags); free plans adjudicated by the engine IN PARALLEL at lock; everything applies together at LOCK THE WEEK
- [x] T14 Consequence chains live: marketing/sales presets set flags → authored opportunity events fire (Someone Liked The Post → DM them / peacock publicly; The Demo Had A Fan; The Invoice Answers Back → shelved feature list arms a churn timebomb); generator also instructed to write opportunities out of recent_actions
- [x] T15 Full action history: state.log_action() records every locked decision, written move (with verdict), gesture, work action, pivot; digest carries recent_actions (last 14) + weeks_in_the_red; both event generation and adjudication read it
- [x] T16 Autopilot walks all 15 screens green (title→select→name→shape→crew→recruit→money→bag→garage→journal p1/p2/p3/decision→forced death→last page); autopsy shot timing fixed

Milestone 8 — THE FULL WEEK LOOP (2026-08-18, autonomous session): journal is now 5 spreads (CONSEQUENCES with last week's verdict/narration/reality-check + this week's arithmetic; THE PEOPLE with loyalty bars + one gesture each; THE WORK departments; THE SITUATION full-bleed; THE DECISION with reality-gated options + written move), page dots + names, everything pending until LOCK THE WEEK, dread beat with fun fact between weeks. ENGINE BUG FOUND & FIXED (was the recurring "text overlaps/off the page" complaint): a Godot Label's size set pre-add_child while autowrap off locks min-width to full text width — fix = autowrap first, custom_minimum_size, then set_deferred("size") after add_child; applied to both journal and autopsy label helpers. Loyalty drain −6/wk (−10 extra starving); walkers take their shares if unvested, cliff claws back 75% if vested; depatures written into the book. Room: red starvation vignette, items hover+click notes, morale-decay props (validated visually: sad-coin money pile, WHY? board, graffiti over the chart at morale 1). Smoke suite still green (30 runs). NEXT UP (not started): 50+ scene-batch production for scene-first screens; grind-era Act 2+; per-scene crew-status art by morale tier; T14 deeper (generated opportunities referencing specific history entries observed but needs live-key playtest).

Milestone 1 — DONE (2026-08-17). Godot 4.7.1 installed via brew. Playable loop: title (concept_01 art) → Act 0 apartment scramble (WASD + SPACE grab, capacity 4, 60s, deposit at DOOR, end tableau) → garage grind (weekly upkeep/burn, event cards with choices, timebombs, milestone gate product≥60 + 10 users) → autopsy (causal chain, generated cards marked ✦) → title. Sim core: SeededRng, GameState, ContentDb, EffectOps (12 ops, clamped), RunRecord. Content: 15 starter items + 10 authored garage events. Headless smoke test `game/tests/smoke.gd` (run: `godot --headless --path game --script tests/smoke.gd`): 30 simulated runs → 26 deaths / 4 victories under random play, clamp + validator assertions pass.

Project layout: `game/` — `Main.tscn` + `src/main.gd` (scene flow, all UI built programmatically, no hand-authored scenes), `src/core/*` (sim), `src/llm/*` (dotenv, client, generator), `src/screens/*`, `data/items.json`, `data/events/*.json`, `assets/title/title_screen.png`. Run: `godot --path game`.

Next (master TODO critical path): author to 25 events · Door mechanic (4 visitors) · negotiation slider (cofounder deal) · burnout ladder · foreshadow rules on deaths (every death ≥2 beats) · garage upgrade catalog (tycoon window) · balance pass (first-death week target 14–20; smoke sim is the dial) · then slice gate 2.11 (external playtests).

## 5. Conventions
- Asset naming: `cat_subject_variant_state.png` snake_case (manifest §1); import handshake `/assets/incoming/` planned (TODO 1.9/2.9).
- Content = data-driven JSON cards sharing one schema for authored + LLM tiers (Dossier §6.1); effect ops are a bounded whitelist with clamps; no op kills directly.
- Every fan-out choice (styles, characters, concepts) gets rendered/authored as comparable candidates and the user picks; the winner's spec becomes law in this file.

## 6. Open threads
- Rejected prompt files in `prompts/` — delete or keep? (user call pending)
- OpenAI + AtlasCloud keys currently in tool auth are **test keys** provided in chat — rotate before anything public.
- `concept_02_founder_archetypes.json` not yet rendered — render after core game milestone, as first check that blob v2 consistency holds across a multi-character sheet.
- LLM event-generation layer (PRD §7) untouched — comes after authored spine works.

## 2026-08-20 — Scenes are STATIC IMAGES for now (owner decision)
Owner reviewed the animation attempts end to end and called it: static stills.
Evidence trail (all in ~/Downloads/runway-scene-3-gpt-ladder/): 07 procedural-only
rejected ("all shit" — broken blink placement, imperceptible motion); 08 seedance
on the POPULATED scene restages it (camera breathes, props morph) — documented
negative result; 09 one-video-on-blank + composited characters was technically
clean (landmarks locked at (0,0), verified blinks) but still not good enough.
Change: PatchScene mounts NO ambient and starts NO life by default; the whole
life layer stays built + tested behind RUNWAY_LIFE=1 (patch_scene.gd
life_enabled(); suite exercises life explicitly and proves the static default —
44 checks, PASS; real-scene shot verified static and clean).
700-scene rollout implication: images only (~$400 at GPT medium), no video cost.

## 2026-08-20 — Generative-scene pivot PARKED at the mapping stage (owner redirect)
Owner approved fully-generative scenes at GPT medium (one image per beat, cache +
edit-for-cast-change), then redirected: the JOURNAL is the core mechanic and gets
the time now; repo cleanup done alongside. So the pivot is parked with the map in
hand, nothing built yet. The map, for whoever resumes it:
- The DM is the adjudicator call: data/prompts/adjudicator.txt +
  ADJUDICATE_SCHEMA in src/llm/llm_client.gd (one call = narration + headline +
  scene facets + cast). Scene vocabulary there is still taxonomy v1; v4 vocabulary
  lives in ~/Downloads/RUNWAY_scene_taxonomy_v4.md and would replace it.
- The swap point is src/llm/scene_director.gd make_scene(): today resolve(library)
  + seedream compose; becomes ONE GPT-medium instruction-JSON call (the proven
  ladder protocol) + per-(run,place,cast) cache. Library + PatchScene stay as the
  fallback floor. Opening scene inputs all exist on GameState: company_idea,
  cofounders, funding_id, cash (type + cofounder + pitch + capital).
- Measured: GPT medium ~55s/$0.07 (fits the narration reading beat), high ~140s.

## 2026-08-20 (night) — The 60-second week + fully-generative scenes SHIPPED
The owner's redesign, live end to end and soak-proven:
- JOURNAL: two spreads. Spread 0 = the world's reply (DM narration first, margin
  verdict, headline on paper, delta strip with the week's CHANGES, crew doodles).
  Spread 1 = standing context + situation + "So — what do you do?" + one clean
  writing area (ghost prompt, scroll-tracking coral ruling, two-slot floor).
  ONE press commits: write → adjudicate → apply → beat. Payload rides the
  outcome dict (same-frame-race proof). ~330 lines of five-spread code removed.
- THEATER: lock drops a drawn curtain (fabric whoosh, scalloped valance,
  breathing "the world considers your week…", 12s failsafe); the beat WRITES
  itself with paper scratch and is click-skippable; skip never skips a render.
- SCENES: fully generative (GPT medium via middleware `referenceImages`),
  instruction JSON + character refs + frame law with CONTRACT STATUS SURFACES
  (blank whiteboard UL, pinned sheet UR) that the game inks with cash/runway/
  customers/payroll/equity. Founding-day and moving-day scenes generated with
  the crew. Change-beat cadence + exact-repeat cache. Library rooms = fallback.
  The DM's staging vocabulary opened to the whole world (novel places are
  first-class — it staged a therapy clinic and an industry mixer unprompted).
- ICONS: 20 journal doodles generated + layer-decomposed (bean-blob crew law).
- SOAK: two silent hangs root-caused to ONE freed-instance touch of the game
  screen during an era move killing the harness coroutine; guarded. 41 live
  weeks clean, zero journal warnings. Balance retuned for one-move weeks
  (±3000/±15 + "your effects carry seven days"); pace soak in flight.
- Resume remembers last week's story (state.last_outcome). Service is the
  fourth WHAT. HUD money chip yields to inked walls.

## 2026-08-21 (the AAA night) — D&D-of-startups SHIPPED end to end
Research: 15 repos (6 business sims, 9 D&D/AI-DM) read by agent fleet; plan in
game/docs/DND_STARTUP_PLAN.md. Built and verified in one night:
- SimEngine (deterministic weekly world: Bass adoption gated by launch/quality/
  GTM capacity, churn by product lifetime, elasticity, CAC saturation, morale
  baseline + burnout cliff, exhaustion 0-6, tech debt + outage rolls, rivals,
  market walk, funding module with desperation pricing, typed STATUS catalog,
  clocks, commitments, onboarding pipeline) — 38 checks + balance tables.
- WorldGen bible (Markov-named investors with archetype/coords/trait/bond/flaw/
  secret; rivals with tactic decks) + world-map reveal screen.
- DM contract v2: signals-grounded context sandwich, deterministic directives,
  dice PAIR with per-stat adv/dis (engine selects the die), extended ops
  (status/clock/price/marketing/hire/loan), required traits + hard-capped
  memory, sentinel + premise guard with echo-retry.
- The D&D layer on the page: level-up pen circles, archetype epilogue, nat-20/
  nat-1 lines, telegraph under the pen, term-sheet signing cards.
- THE BINDER: 7-tab drawn dashboard with fog-of-war analytics.
- The ceremony: 20 pre-rendered 1080p seedance cup-and-die clips (GPT stills
  first/last frame), spritesheeted, played on the true engine-selected die.
All filmed; live paid week verified; keyless soaks green.

## 2026-08-21 — The transition rebuilt (owner's live-play storm)
The owner played and filed the storm: unclear/esoteric text, log book cut and
repeating the beat, dice everywhere and mismatched with the video, the same
question with the same person week after week, no new images. The fullrun
probe harness reproduced the structural half live:
- A week could commit UNDER the previous week's open beat: dice rolled and
  numbers applied while the beat and its art were silently swallowed. Now a
  commit gate (_world_busy) holds the lock from beat-open to beat-close, and
  the founding holds it too while day one is still being written.
- The founding beat played TWICE (unstamped dm re-consumed by _poll_turn) —
  the owner's "goes back to Week 1". Stamped; titled DAY ONE.
- An already-shut curtain outranked the cup, so the whole ceremony could play
  invisibly — "sometimes no video dice roll". The ceremony now always claims
  the top; the curtain takes it only after the die settles.
- The curtain's own drawn die + stamp deleted entirely: ONE die on screen,
  the pre-rendered cup clip on its own opaque felt screen, always the
  engine's exact number; the beat explains it in plain sentences.
- The curtain failsafe (12s) was lifting mid-adjudication onto a stale page;
  now 40s and never while a verdict is in flight.
Repeats: played-event memory records cast names; a name filter blocks pool
cards re-leading with recent people; both prompts forbid it; plain-title law.
Log book: line_fitted shrinks the hand before it ever cuts; special pages
print the ask whole; the level row never wraps. Fresh scene every staged
week; art landings/failures and the week's headline/journal_note/narration
now print for live verification. Suites green; 3-week paid probe filmed the
exact spec sequence. Commit 35def8f.

## 2026-08-21b — The casino dice line + the everything sweep
The owner validated a casino-red d20 master (solid red facets, white pips,
no in-facet decoration); 20 stills were edited from it (only the number
changes), seedance rendered 4s 720p clips (cup slam -> small die tumbles
out -> camera zooms into the number), fal veed stripped the cream card
(the earlier matte ate cream die faces — red die makes removal trivial),
and 20 RGBA sheets landed in-game. Contact sheets in ~/Downloads. The
ceremony player draws the drawing straight on the felt.
The autonomous sweep walked every screen: was-page one-line elision fixed
(two-line floor), shot harness skips the founding curtain, autopsy names
the founder, world reveal flows with LLM verbosity (new shot harness),
Binder got a 7-tab harness that immediately caught a Variant crash in the
crew tab (now named + str()-safe). Deep 8-week probe: art every week,
dedup firing, five straight dice keeps correct, prose graded clean.

## 2026-08-21c — The eight-report batch + the 3-hour window
Owner streamed #125-#132 then left for 3 hours with "improve all of that
autonomously and go beyond". Shipped: the name as its own first screen; a
29-item trade-aware shelf (rebuilt on bag entry); the Birth screen; founding
prefetched during the reveal; the founding rewritten as a first-person
journal de bord — the instruction block co-authored with the Nexus prompt
assistant (per the owner's explicit instruction), verified live, then
pinned against example-copying; measured text flow on the reveal AND the
binder street tab (schema caps raised so quotes stop cutting mid-word);
honest ▼ + drawn scrollbar on the beat; the was-page back to one hand;
scene ink tilted to each surface's measured edge and capped at its bottom;
day-one curtain line. Diagnosed a triple "hang" to a stale smoke assertion
(15 items) idling the tree. 14 new item sprites generated in-style (white
keyed to alpha). Suites green; probe 6 confirmed the founding voice.

## 2026-08-21d — The 4-hour window: agents in parallel, everything graded
Owner left for 4 hours ("until the whole list is 100%"). Four Opus agents
ran in parallel with the main line: (1) curtain sway loop — generated,
baked, integrated with drawn fallback, plus a real finding (per-frame font
load starves a lone canvas item); (2) ship diet — 6.65GB→2.77GB .app by
discovering Godot re-encodes at import (webp mirrors + VRAM-lossy .import
sidecars, 4,945 files, PSNR-verified, all PNGs intact); (3) birth loop
rebuild after owner rejection — square source was the squish, real
logotype texture, on-model mascot via refs, seam RMS 30→1.3 (same fix
then applied to the curtain bake); (4) tutorial video pages (in flight).
Main line: full-screen slot TABLE, real paper menu buttons (word above
paper), keys desk redesigned OpenAI-only with the generative pitch (label
bug: Labels without .text render nothing; never prefill keys in
cleartext), RW! icon in the title hand, 2.6GB styled DMG, dead eras
purged (391MB + 60s refs out of the public repo), hero/witness
entrances, journal close motion, dice whoosh, ledger live lever math from
tick formulas, money law + clarify live-probed (spent $1500 exact,
era-sane $300 for the amountless move, narration carried spend continuity
across weeks), boot latency 1440→520ms (144 sync frame loads → streamed).

## 2026-08-21e — Window 4: the DMG playthrough answered
Owner played the shipped DMG and filed 16 reports. All closed:
P0 — the week's art was dying of ONE middleware 502 (no retries; now 3
attempts with backoff and per-attempt traces); luna leaked a Cyrillic
token onto the clarify page (sanitized, one Latin line) and the answer
field collided with the lock row (no lock row while the world waits); the
binder ring circled thin air (old 7-tab geometry); launch slowness was 48
full-screen title frames (68MB) loading before the first pixel (frame 1
instant, rest streamed — 1440→382ms to draft); the birth wait now
prefetches the bible during the bag page with a 25s skeleton ceiling.
Agents (3, parallel): idle sets normalized to one baseline/height with a
premultiplied-alpha pass (plus two real bugs found: the hero jumped 225px
off the spotlight on every archetype switch — THE 'cropped characters';
and the shadow floated); the D&D layer — six hidden traits with pips and
click-for-rule on the card, engine-real effects (doors-open advantage,
luck rerolling nat-1s, warmth discounting term sheets), 42 items with
exact trait_mods and a live in-play line (engine suite 43→66 checks);
the birth intro chain (walk in → open the box → seamless into the loop,
seam RMS 5.6). Inline: title revealed by the parting sway curtain, binder
rises, keys/name mascots idle, crew-line baseline compensated, 13 new
item sprites generated. All screenshots verified by eye; suites green.

## 2026-08-22 — The founder loops earn their gate; the game stops burning
Idle loops v2 (agent + follow-through): a calibrated perceptual gate
(blur-scale RMS over the still's alpha, eyes scored separately — raw RMS
is registration noise on flat line art) accepted three regenerated loops
(hacker/exfaang/consultant: median error ~5x lower, eye error 15-35x,
seams 24-36 -> ~1) and correctly rejected every walk-continuing hustler.
Root cause: the only mid-stride pose. A planted-feet source still got the
eyes right (13.7 vs 81) and its best clip was shipped on an eyeball
override (the flagged max is breathing bulge, on-model). Perf pass
(agent): repaints 145-289/s -> 12-25/s via frame-index gating + 12fps
breathe quantisation, spent sheets freed (curtain open 110->20MB), 30fps
cap; CPU -45% on the comparable screen; perf_probe.gd is the permanent
harness with the occlusion/spike-gauge traps documented. Plus: the
select stage REGENERATED with the beam on the founder (48 fresh dust
frames), the export renderer finally reads user://keys.env (why every
shipped room stayed default), paint starts at the signature, clickable
stat rows, key re-ask + api-key link, top-down book.

## 2026-08-22 — #175: the empty logbook, killed at the root (three layers)
The book opened on "the first entry is being written…" and nothing ever
came (owner had to quit at the start). Root: the founding request WEDGED
past its own 90s timeout — HTTPRequest.timeout proven asleep on macOS for
the second time (render ladder was the first) — so the retry never ran.
Fixes: (1) every text request now races a hard scene-tree watchdog
(50s founding/clarify, 100s assess) that cancels the wedge and hands the
caller its failure path — proven with a black-hole socket + the soft clock
disabled; (2) the birth loop now holds until the words AND the paint are
both done (owner's law) — 3 watchdogged attempts ≈150s, then the ENGINE
writes day one itself from facts it owns (lease, cash, prices, promise) so
the page can never be empty; (3) the probe's own screenshot caught a latent
order bug the change would have shipped: feeding the book before _ready()
dropped the text on a null label and re-locked the gate — entries now
buffer and land through the same door. FIRSTFLOW fault probe re-run
end-to-end against the black hole; watchdog_probe.gd is the permanent
harness. RUNWAY_LLM_URL / RUNWAY_LLM_NO_SOFT are the fault-injection keys.

## 2026-08-22 — the twelve-lane day (Unity port hardening)
Live play surfaced five defects (#176-#180); two measured-comparison
agents produced dossiers of ~20 more (wrong display typeface everywhere,
baseline math, scroll-thumb teleport, cap-pie geometry, tofu glyphs);
the A-tail audit found nine shipped defects incl. saves corrupting on
every CONTINUE (Newtonsoft appending into populated defaults) and a
first-run dead-end that could hold SETTLE IN forever. Twelve parallel
lanes fixed all of it: typography (Baloo2 lives, the TMP fallback chain
was silently dead at a null slot, 25 glyphs baked), charts Godot-exact,
the founder back in the spotlight (four compounding causes), films and
cup sheets shipped GPU-ready, the dice roll over the darkened page, 14
SFX cues wired from a Godot-evidence cue map, the OST self-driving into
the new mix, #175 parity ported to Unity, repo integrity (a clone can
now OPEN the project; README; silent-drop ignore trap closed). LLMUnity
assessed: GO post-ship for keyless local clarify/worldgen, NO for
adjudication. Kill-switch matrix 9/9 before the wave; full re-verify
rides the next build.

## 2026-08-22 (night) — the ship gate: RUNWAY-Unity at 92.6%
Final build 2026-08-22 22:37 · eb21166 (682MB vs the 2.2GB DMG). The
23-pair side-by-side verdict pass drove a final fix wave: the painted
theater returned to the select stage (the two 57%-diff screens died of
one cause — the missing scene mount plus counterpart-less additive
glows), loop clocks run continuously like Godot's, the bag's box is
alpha-trimmed, birth holds its 0.3s and rings its line with a real
halo. The live keyed probe played four generated weeks: 45/47 checks,
dice agreement engine=cup=beat proven, and its one real finding — the
authored deck re-dealing on a dry pool — is fixed in BOTH engines.
Perf on the final build: 12-26 rebuilds/s per screen, 270-308fps
uncapped at 3.2-3.7ms, gc 0.00/s, floors returning. And the cast is
REBORN: five consistent founders (master IS frame one, native
transparency, feet seated in the painted pool, ~$12 of image credit),
installed in both engines. Remaining honest gaps live in the checklist
at their true percentages: C5 pricing-economics measurement, C8 paint
fault-injection, the re-soak, P8/P10/P11 cosmetics, the streamed-art
migration plan (docs/config-plan.md) and the LLMUnity local-narrator
dossier (unity/briefs/) as the two post-ship lanes.
