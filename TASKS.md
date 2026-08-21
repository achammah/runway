# RUNWAY! — MASTER TASK LIST (autonomous build to a full AAA game)

Owner check-in file. Every task has an owner lane, a status, and a Definition of Done (DoD).
Status legend: ☐ todo · ◐ in progress · ✔ done (owner-lane claim) · ✅ verified by main review · ✖ dropped (reason logged)
Last updated: 2026-08-20 (the journal wave)

## THE JOURNAL WAVE (2026-08-20) — the core mechanic, rebuilt and verified
- ✅ J1 Ink reveal: pages write themselves behind a travelling pen (real pen glyph), skippable, harness-instant via RUNWAY_INSTANT_PAGES
- ✅ J2 Drawn selection: the pen DRAWS the circle (0.24s), neighbours quiet on an ease; scribble sfx
- ✅ J3 Physical page turn: sheet lifts opaque, room breathes 80ms, new sheet lands, reveal starts on landing; old spreads stay dry ink
- ✅ J4 Paper fence: answer space reserved first, captions cap 3 lines, rows compress, pull-up cascade, prompt degrade — 0 overruns in worst-case stress
- ✅ J5 Lock ceremony: pen underline (under the words), lock_week latch sfx, one beat, then the week
- ✅ J6 Verdict margin marks: star (brilliant) / double strike (backfired), only those two
- ✅ J7 Pen sound: procedural paper scratch under writing, generated in-repo
- ✅ J8 Films: reveal probe + turn film + lock film harnesses; every animation READ frame by frame
- ✅ J9 Fullrun soak: 30 weeks, five eras, ended alive — zero journal warnings, zero script errors
- ☐ J10 people-page composition polish (portrait sizing/dead paper with small crews) — cosmetic
- SCENES MODE: FULLY GENERATIVE (owner pivot executed) — GPT medium per staged beat, contract status surfaces inked, opening + moving-day scenes, change-beat cadence; static patch rooms remain the fallback floor
- SUPERSEDED: the 700-scene pre-generation batch (fully-generative replaced it); taxonomy v4 lives on as the DM's staging vocabulary

## THE AAA NIGHT (2026-08-21) — D&D-of-startups, tracked live
Status: ☐ todo · ◐ in progress · ✔ done+tested
- ✔ N1 SimEngine core: Theta, hostile tick, status catalog, clocks, pipeline, funding, signals
- ✔ N2 GameState fields + save/load for all engine state (incl. metric_history)
- ✔ N3 sim_engine_test.gd — 38 hermetic checks PASS
- ✔ N4 balance_sim.gd — 5 strategies × 50 wks; GTM capacity clamp + morale baseline landed from its tables
- ✔ N5 WorldGen: Markov names (pronounceability-filtered), 3 investors (archetype/coords/trait/bond/flaw/secret), 2 rivals; deterministic per seed. (LLM Theta enrichment: deferred — defaults per WHAT×WHO are live; the DM already stages from the pitch)
- ✔ N6 world ticks FIRST each lock; extended ops executed via typed catalog; receipts on the page — verified live (enterprise_pilot 4wk, +$700 with why)
- ✔ N7 dice PAIR + per-stat adv/dis map sent; ENGINE selects the die post-verdict; margin bands stamp curtain + beat; nat-20/nat-1 lines on the week page
- ✔ N8 context sandwich (signals + bible + capped memory + last-3-verbatim + directives); memory required + hard-capped — verified live (546-char memory, traits tagged)
- ✔ N9 sentinel (premise/catalog/round-coherence) + one echo-retry + sanitize
- ✔ N10 milestone XP + level-up pen-circle on the page + archetype epilogue on the last page
- ✔ N11 THE BINDER: 7 tabs photographed (vitals/customers+fog/product/crew/cap/street/threats), TAB/B + drawn doorway button
- ✔ N12 telegraph under the pen · term-sheet cards (fundraising_open flow) · nat stamps
- ✔ N13 20×1080p ceremony sheets rendered + wired + filmed (cup → tumble → number)
- ◐ N14 world-map reveal page SHOT-VERIFIED and wired; stat point-buy + cofounder chemistry needle deferred (level-up system covers stat agency; chemistry needs cofounder coords — next round)
- ◐ N15 final 75-wk soak running; live paid week clean; docs current

## THE LIVE-PLAY + AUTONOMY ROUND (2026-08-20 night)
- ✅ K1 Two-spread 60-second week (read the world's reply → one clean written move), no options before the pen
- ✅ K2 One-press commit: write → adjudicate → apply → beat; payload rides the outcome (race-proof)
- ✅ K3 Theater curtain on lock (drawn drapes, fabric whoosh, scalloped valance, breathing considering line, 12s failsafe)
- ✅ K4 Beat writes itself with paper scratch; click-to-skip; skip never skips the render
- ✅ K5 Delta strip shows the week's CHANGES (+/−) with morale battery; crew strip in doodles; DM headline on the page
- ✅ K6 Journal doodle icon set ×20 (generated + layer-decomposed, bean-blob law)
- ✅ K7 Standing context + never-trimmed 'So — what do you do?'; DM staging vocabulary = the whole world (novel places first-class)
- ✅ K8 Typed ink rides a scroll-tracking coral ruling; two-slot field floor + descender headroom (instrumented, filmed)
- ✅ K9 Moving-day + founding-day generated scenes; era rooms never open empty
- ✅ K10 Resume remembers last week's story (state.last_outcome persisted)
- ✅ K11 Service as fourth WHAT everywhere
- ✅ K12 ~330 lines of dead five-spread code purged; shot harness photographs the real states; screens.md updated
- ✅ K13 Soak hang root-caused (freed screen mid-era-move killed the harness coroutine) and fixed — 41 live weeks clean
- ◐ K14 Balance: one move carries the week (ranges retuned ±3000/±15 + generosity clause) — pace-check soak running

## 0. Standing constraints (every lane inherits these)
- PIPELINE ORDER (owner doctrine, 2026-08-18): 1) generate ASSETS (text→image) · 2) generate SCENES (image→image with refs = the consistency method) · 3) ANIMATE scenes (image→video, 4s, first==last). CRITICAL LESSON: i2v only moves what the still already depicts — if the animation needs a mechanism (a treadmill burning away, smoke rising, a crowd reacting), the STILL must show that mechanism (destruction gradients, mid-action poses); fix at step 2, never by prompting harder at step 3.
- Art: hand-drawn wobbly felt-pen ink, flat fills, no gradients. Palette ONLY cream #F2EAD3 · ink #1E1E1E · coral #E86A5C · yellow #F4B942 · sage #8FA582 · blue #6E8CA0 · white. Character = blob v2 (cowlick, mismatched oval eyes, tiny cream sneakers, lean; archetypes differ by props only).
- Assets: SCENE-FIRST — generate full composed scenes, decompose with Seedream (permanent nexus asset URL!), place layers by template match; sprites only where recomposition is truly needed. Middleware + keys documented in PROJECT_LOG §3.
- Typography: Baloo 2 Bold for headers/buttons/big numbers; Patrick Hand ONLY for diegetic journal handwriting. Couch-distance sizes: nothing under 24px on a 1536×1024 canvas; body 26–30; titles 42+.
- Godot trap: configure autowrap/expand BEFORE size; assign texture LAST or set_deferred size. Never place a TextureRect at a size before its texture fit is configured.
- Layout truth: text must sit INSIDE measured art (journal pages are trapezoids: left paper x≈173→122, right ≈768→1361..1415, art renders 1340×814 at (98,93)).
- Verification: a change is NOT done until its autopilot PNG has been READ by the lane. `RUNWAY_SHOT=<dir> godot --path game` captures 21 states. Reviewer must score typography and placement explicitly and hold to "award-winning indie game, not a SaaS form".
- Never pkill the owner's game window. Never touch game/.env. Smoke suite green before any lane claims done.
- Music (all delivered in `music/`): loops share 96 BPM / F–Dm; wire with bar-aligned crossfades (2.5s bar).

## 1. Foundation lane (main) — enables all others
- ✔ F1 Loop-point + wire all music (MusicManager: 5 loops with bar-aligned loop_end, 2.5s crossfades, title/selection/garage/in-the-red/last-page state map, whistle stem ≥75 morale / hum ≥55, generated stings replace win/death sfx + lock_week/pivot installed): 5 loops (bar-aligned seamless cuts already computed: title 62.5s, selection 30s, garage 20s, in-the-red 42.5s, last-page 30s), 4 stings, 2 stems; AudioStreamWAV loop points; crossfade manager; in-the-red swap on cash<0; stems as morale-driven overlays.
- ✔ F2 Autopilot covers 21 states; map at game/docs/screens.md with per-PNG owner lanes (era screens to be added with F4) per era; per-lane shot dirs; a `screens.md` map of every screen → state → PNG.
- ✔ F3 Reviewer brief v2 — baked into game/docs/LANE_BRIEF.md (typography/placement/integration/game-feel sub-scores, refute-first, owner-bar language); every lane instructed to use it verbatim (owner's bar baked in): scores /10 with typography + placement sub-scores, refutes before reporting, must flag "assembled/not organic", "small text", "SaaS-form" explicitly; blind to code.
- ◐ F4 Era system (delegated to LANE-CONTENT with gates/rents/caps/demotion spec): garage → coworking → first office → startup floor → HQ; era gates from the dossier; era-specific room scene + memorabilia; demotion flow.
- ✔ F5 Scene-first placement pipeline — game/tools/scene_pipeline.py (generate/decompose/place, auto style block, permanent URLs, multi-scale match, scenes_index.json) as a reusable script (generate → asset upload → decompose → template-match → layout.json → GDScript loader).

## Lane roster (live)
- LANE-GARAGE → S6-S9 (garage_view_screen.gd exclusive)
- LANE-FLOW → S1-S5, S10 (title/draft/autopsy exclusive)
- LANE-SCENES → A3/A4/A8 Wave 1 (14 hero scenes) then Wave 2
- LANE-CONTENT → C1-C6 + F4 (data/core/llm exclusive)
- MAIN → F-lane, integration, reviews of lane claims, TASKS.md

## 2. Screen lanes (INDEPENDENT agents, same brief, own shot dir; each: screenshot → improve → reviewer → iterate until ≥9/10, then main review)
- ☐ S1 TITLE (living video loop; typography lockup; press-any-key; music) 
- ☐ S2 FOUNDER SELECT (stage, hero animation, stat sheet, dock, transitions)
- ☐ S3 NAME + SHAPE (ceremony, idea machine, WHAT/WHO cards)
- ☐ S4 CREW + RECRUIT MODAL (heist cards, donut, traps, cast art states)
- ☐ S5 FIRST MONEY + PACK YOUR BAG
- ☐ S6 GARAGE ROOM (scene-first rebuild from the composed scene: crew in-room by state, money pile, board, chart, decay, badges, item notes, in-the-red vignette)
- ☐ S7 JOURNAL — CONSEQUENCES + PEOPLE spreads (paper geometry, ruling, portraits, pip bars, gestures)
- ☐ S8 JOURNAL — WORK + SITUATION + DECISION spreads (assignment desk, options gated by reality, write-your-own, lock)
- ☐ S9 PIVOT panel + week-turn dread beat + fun facts
- ☐ S10 THE LAST PAGE (autopsy) + RUN IT BACK
- ☐ S11 COWORKING era room + memorabilia (CAMP pennant, demo-day)
- ☐ S12 FIRST OFFICE era room (seed raised, hires, server closet)
- ☐ S13 STARTUP FLOOR era room (Series A, foosball, layoffs)
- ☐ S14 HQ era room (pre-IPO, board coup eve)
- ✔ S15 IPO FINALE + acquisition ceremony (LANE-WIRING, verified in-run)
- ☐ S16 YC/"Combinator Camp" branch screens (application, interview, demo day stage)
- ☐ S17 Investor/deal screens (negotiation slider, term sheets, cap-table evolution)
- ☐ S18 Deaths gallery + collectible ending cards (60 at launch target; start with 12)
- ✔ S19 Settings/pause overlay (MAIN): ESC anywhere — music volume, Simulation Engine status with works-without-a-key copy, resume

## 3. Content + systems lanes
- ☐ C1 Authored events to 60 (garage 25 · coworking 15 · office 10 · floor 5 · HQ 5) with needs_item/needs_role gates and foreshadow chains
- ☐ C2 Employees system: hire/fire, salaries, burnout ladder with visible states, per-era staff caps; hires appear IN the room scenes
- ☐ C3 Funding rounds: seed/A/B with dilution math, board seats, control ladder (<50% board deck, <25% employee ending)
- ☐ C4 Acquisition offers cadence (bimodal session valve) + accept = bank score
- ☐ C5 Valuation model + payout; score = founder% × exit; leaderboard-ready run record
- ☐ C6 LLM: Tier-3 run director (arcs: rival/press/inner-circle) injected into Tier-2; adjudicator prompt v2 with era + employees context; opportunity generation referencing history
- ✔ C7 COMPLETE incl. daily seed (MAIN): D on title = date-seeded run, generator.disabled (authored-only determinism), [DAILY] tag in profile; save/resume/profile as before: SaveSystem — weekly autosave, resume ongoing run from title (60S!-style one-run-at-a-time, death clears), profile with run history + endings seen + best payout; daily seed still open
- ✔ C8 Balance DONE (LANE-CONTENT, 7 sim iterations): exits 20% · deaths 52.5% · median death wk19 · all runs leave garage; structural fix = real recurring revenue (traction × era value, net burn) + promotion cash guard + solvency morale drift: smoke sim to first-death week 14–20, death distribution ≈ dossier §10

## 4. Asset production lanes (scene-first; each delivers layers + layout + integration)
- ☐ A1 Founder wardrobe by era (garage hoodie → coworking tee → office shirt → floor blazer → HQ suit) ×5 archetypes, idle loops via video pipeline
- ☐ A2 Employee cast (12 distinct staff, 3 mood states each) placed in era rooms
- ☐ A3 Era rooms ×4 (coworking, office, floor, HQ) as composed scenes + decay variants + memorabilia (CAMP pennant, LAUNCHED poster, first dollar, press clipping, Series A tombstone, unicorn horn)
- ☐ A4 NASDAQ bell scene + roadshow stage + YC demo-day stage + investor portraits (8 archetypes ×2 emotions)
- ☐ A5 Ending cards (12) + gallery frame + locked-card back
- ☐ A6 UI kit pass: meters, buttons, frames in one consistent hand-drawn family (replace remaining default-styled controls)
- ☐ A7 SFX set (25) synthesized/curated to the palette; VFX (confetti, sweat, zzz, money burst)
- ✔ A9 CONSISTENCY METHOD VALIDATED (golden-hour garage test: same room, same objects, perfect relight via seedream-5.0-pro/edit; default engine of scene_pipeline.py variant) (owner, 2026-08-18): **structured JSON prompt + reference images = new on-model image**. The Nano Banana JSON spec (prompts/image_prompt_gen.md schema: scene, subjects with consistency_rule, palette, technical_rules) describes WHAT to draw; referenceImages (permanent nexus-asset URLs of the APPROVED character cutouts / base room) pin WHO and WHERE so characters stay identical across hundreds of setups. Implemented as `scene_pipeline.py variant <new_id> <prompt-or-json> --ref <url|scene_id/layer> …` calling /edit-image-openai. Canonical refs registry: game/assets/scenes/refs.json (approved character + room asset URLs). Applies to A1 wardrobe, A2 staff moods, A3 era/decay variants, A8 state matrix.
- ◐ A10 PER-SCENE LOOP ANIMATIONS: seedance-2.5 engine LIVE and validated — title reanimated (fire + intact lettering, deployed to assets/title/video/) and garage hero loop shipped (typing/soldering/steam, one 48f cycle); owner budget 20-50 loops, validate >100; LANE-SCENES producing the rest
- ☐ A8 THE SETUP LIBRARY (owner mandate: HUNDREDS of composed scenes so every branch/prompt has art). Matrix = era (garage/coworking/office/floor/HQ) × company state (thriving/steady/starving/dead) × crew size (solo/2/3/5+) × moment (working/celebrating/crisis/night/pivot/layoff/demo/board-meeting) + specials (YC interview, YC demo day, NASDAQ bell, press ambush, due diligence, moving day up, moving day down, acquihire signing, server fire, launch day). Produced in priority waves: W1 hero scenes (20, high quality), W2 state variants (60, medium), W3 long tail (120+, medium). Each scene: generated → decomposed → layout.json → registered in scenes_index.json so game code and LLM events can reference setups by id.

## 5. Integration + ship
- ✔ I1 Wire eras end-to-end (LANE-WIRING): CRITICAL FIX — game was ending at first era promotion (victory=chapter break now); runs reach garage→coworking→office→floor→hq; SceneRoomPicker + era-transition-as-log-book-page + IPO/acquisition finale on nasdaq_bell (verified in real runs: IPO $137.9M, acquisition $3.55M); queued era moves no longer dropped; RUNWAY_LANEWIRE fast harness. GAP: room still hardcodes garage art → relayed to LANE-JOURNAL: garage → … → IPO or death; scenes swap; memorabilia accrues; wardrobe swaps
- ✔ I2 Full-run autopilot: RUNWAY_FULLRUN reaches hq in ~27 weeks with per-era captures; RUNWAY_LANEWIRE renders era/ending beats in seconds: RUNWAY_FULLRUN mode plays real weeks through draft→journal→lock (7 weeks green in test v2); death/era-up runs pending lane completion (title → founding → 10 weeks → era-up → … → last page) green + 21+ PNG review set per era
- ☐ I3 Main review of every lane's claim; send-backs logged in TASKS.md; PROJECT_LOG milestone entry
- ☐ I4 Final report to owner: per-screen scores, what shipped, what's parked, how to play

- ✔ S18 Deaths gallery (MAIN): corkboard polaroid wall, 12-slot catalog matched against profile endings_seen, ×N repeat badges, locked ?-cards, G on title (proper button = LANE-FLOW followup); in autopilot as 01b_gallery
- ✅ S3/S5/S10 MAIN-verified from lane r3 captures (shape = real select screen: coral rings + checks, full-color unpicked cards; autopsy stamp + estate verified; right autopsy page under-filled at short runs — noted for a later pass). S1/S2/S4 spot checks pending. Lane claim: 8.5-9 (selection rings, rubber stamp, type raises, TRAPS autosize; 2 findings refuted by pixel probe; consultant-frame clip + baked PRESS ANY KEY deferred as asset-level) — MAIN verification pending

## CAST + SCENE MATRIX (owner-locked 2026-08-18)

**Founder archetypes: 4** — HACKER, HUSTLER, EX-FAANG PM, EX-CONSULTANT. THE DROPOUT IS CUT.
**Cofounder types: 5** — SALES, BUSINESS, TECH, HUSTLER (competences) + THE IDEA FRIEND (covers
nothing, pure YC trap, owner chose to keep it). Max 4 cofounders, NEVER two of the same type.
Matrix = 31 cofounder sets x 4 archetypes = 124 crews x 5 eras x 3 moods = **1860 room states**.

**THE RULE THAT MAKES IT TRACTABLE: the loop belongs to the ROOM, not the CAST.**
Baking 1860 loops is impossible; baking 1860 scenes is 62 hours. Instead:
- era stages are generated EMPTY and only the empty stage is animated (ambient life only:
  dust, lamp flicker, fan, monitor glow — nothing implying a person moved)
- the cast is STATIC sprites composited at crew marks; `SceneRoom` already draws layout.json
  layers ON TOP of anim frames, so no engine change was needed
- character life is free in-engine: breathe() + blink + idle sway, and it reacts to state
- mood (steady / in-the-red / thriving) is an in-engine grade + prop swaps, never a new loop

**VIDEO BUDGET: 8 new loops total** — 5 empty era stages, 1 empty character-select stage
(the 4 archetype sprites composite on top, so the five per-archetype select loops are CANCELLED),
2 special stages (nasdaq_bell, yc_stage). Nothing else animates without owner sign-off.

**Static generation (budget fine per owner):** 15 empty era stages (5 eras x 3 moods),
12 founder sprites (4 x 3 moods), 15 cofounder sprites (5 x 3 moods) = 42 assets -> all 1860 states.

**Craft rules so composites read organic, not assembled** (owner's standing defect):
1. crew marks carry per-mark SCALE — marks further back are smaller; uniform scale looks pasted
2. foreground occluders (desk front / monitor back) drawn OVER characters so they sit IN the room
3. every sprite carries a baked soft contact shadow matching that room's light direction
4. sprites generated with `variant --ref <era_room>/room_bg` so lighting and palette match
5. fixed seating order SALES, BUSINESS, TECH, HUSTLER, IDEA FRIEND — never random per frame

- [ ] A11 5 empty era stages x 3 moods (LANE-SCENES)
- [ ] A12 4 founder archetype sprites x 3 moods, on magenta (LANE-SCENES)
- [ ] A13 5 cofounder type sprites x 3 moods, on magenta (LANE-SCENES)
- [ ] A14 crew marks + foreground occluders in every era layout.json (LANE-SCENES)
- [ ] A15 8 stage loops (LANE-SCENES)
- [ ] C9 draft enforces max 4 cofounders AND no duplicate types (LANE-FLOW)
- [x] C10 archetypes.json: dropout removed, structure ladder extended to 4 cofounders (MAIN)

## ASSET INTEGRITY (2026-08-18)

`urllib.request.urlretrieve` writes a partial file and returns SUCCESS when a connection drops,
so two half-downloaded scenes (`nasdaq_bell_gc`, `hq_steady_gc` — one cut at exactly 1048576 bytes)
sat in the repo looking like valid PNGs. All five download sites in `tools/scene_pipeline.py` now
use a verified `_fetch()` (Content-Length + PNG signature + IEND + decode + 3 retries), and
`python3 tools/scene_pipeline.py verify` scans every scene PNG and exits 1 on damage.
Lanes run it after every batch and paste the line in their round log.

- [x] F6 harden every pipeline download + add `verify` gate (MAIN)
- [ ] A16 regenerate the 2 quarantined truncated scenes (LANE-SCENES)

## Owner feedback ledger (append; each item becomes tasks above)
- 2026-08-18 PM: "log book VERY FAR from award winning" → LANE-GARAGE re-scoped to JOURNAL-FIRST (perfect wrapping, page titles, structure, integration; may extract journal_view.gd). "hundreds of setups get small loop animations / video loops" → LANE-SCENES i2v mandate per hero scene + SceneRoom auto-plays anim/ frames (done, MAIN).
- 2026-08-18: "assembled/not organic" garage → S6 scene-first (in progress). "All text small" → §0 type floor + S7/S8/S10. "Reviewer/redesigner should have caught it" → F3.
