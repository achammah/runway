# RUNWAY! — Master TODO (start → launch)
*Sequenced by dependency. ☐ = task. Bold gates = don't proceed until passed. Assumes 1 dev + 1 artist (you, possibly both). Rough calendar for that team size in brackets.*

## PHASE 0 — Design validation & pre-production [2–3 wks]
☐ 0.1 Write the one-page design constitution (7 pillars + the cut rule) and pin it
☐ 0.2 Build economy spreadsheet: burn/salaries/valuations/dilution across all 5 eras; simulate bootstrap vs blitz columns to a plausible IPO and 3 death curves
☐ 0.3 Paper prototype: 15 starter items, 25 event cards (index cards), 1 era; play 5 runs solo + 3 with a friend
☐ 0.4 **GATE: causality test** — players spontaneously say "that happened because I grabbed/skipped X" ≥3 times per run; if not, redesign event requirements before ANY code
☐ 0.5 Tune session valve on paper: insert acquihire offer beats; verify someone banks early at least once
☐ 0.6 Write tone bible v1 + 10 sample events in final voice; test read-aloud time <20s each
☐ 0.7 Lock working title; check trademark/steam name collisions
☐ 0.8 Decide engine: Godot 4 default; 1-day physics feel spike (grab/carry/drop) in Godot vs Unity; commit
☐ 0.9 Set up repo, project structure, CI (headless test runner), asset pipeline folders
☐ 0.10 Draw 5 style-test images (1 character, 1 arena corner, 2 items, 1 card); lock style sheet + palette.ase

## PHASE 1 — Core tech foundations [3–4 wks]
☐ 1.1 Deterministic sim core: seeded RNG service, tick/week state machine, full run-record logging (every event, choice, roll)
☐ 1.2 Content database: JSON loaders for items/events/upgrades/characters with schema validation on load + hot-reload
☐ 1.3 Save system: run save + profile save (meta), versioned, cloud-save-safe
☐ 1.4 Scene flow: title → run setup → scramble → grind → tycoon → transitions → ending → autopsy
☐ 1.5 Scramble controller: movement, grab/drop, carry capacity, heavy items, timer, deposit zone, end tableau
☐ 1.6 Grind controller: weekly sequence (upkeep→assignments→events→door→meters), event card UI with 2–4 choices
☐ 1.7 Effect-op interpreter: all ~40 ops with clamps; unit tests per op
☐ 1.8 Meter/HUD framework with placeholder art
☐ 1.9 Asset import script: /assets/incoming/ auto-register + hot-reload (artist handshake)
☐ 1.10 Debug console: set meters, force events, skip weeks, seed control

## PHASE 2 — VERTICAL SLICE: Act 0 + Act 1 [4–6 wks]
☐ 2.1 Author content: 15 items, 25 events, 8 endings, garage upgrade catalog (12), 1 archetype (Hacker), 1 cofounder candidate — all in final JSON
☐ 2.2 Build apartment scramble arena (Act 0) with item spawns + jitter
☐ 2.3 Build garage arena + 2 crisis scrambles (laptop dies, parents reclaim garage)
☐ 2.4 Implement negotiation slider micro-game (cofounder equity deal as first use)
☐ 2.5 Implement Door mechanic with 4 visitors
☐ 2.6 Implement burnout ladder + 2 comedic behaviors
☐ 2.7 Implement foreshadow/timebomb system + autopsy screen v1 (causal chain list, ugly is fine)
☐ 2.8 Wire P0 art set (~116 images) replacing placeholders
☐ 2.9 First-time UX: Act 0 as silent tutorial (no text popups; verb discovery by design)
☐ 2.10 Playtest ×10 externally (record screens)
☐ 2.11 **GATE: slice test** — ≥60% finish a run, ≥50% immediately restart, players retell their death as a story. Iterate 2.x until passed
☐ 2.12 Balance pass 1 from spreadsheet + telemetry hooks (local CSV)

## PHASE 3 — Content tooling & production line [2–3 wks, parallel with 4]
☐ 3.1 Event editor (simple web or in-engine): form → schema-valid JSON, requirement pickers, effect dropdowns with clamp display
☐ 3.2 Dependency-graph analyzer: orphan items, unreachable events, foreshadow-chain integrity, coverage report (§6.5 rules as automated checks)
☐ 3.3 Balance linter: per-choice EV bounds by era; run as CI on all content
☐ 3.4 Batch playtest simulator: headless bot plays 1,000 runs/seed-sweep; output death distribution, week-of-death histogram, event repetition rate
☐ 3.5 Writer's pipeline doc: how to go from joke → card in 10 min

## PHASE 4 — LLM Simulation Engine [3–4 wks]
☐ 4.1 Run-state digest serializer (≤600 tokens, stable field order)
☐ 4.2 API client: async, retry, rate budget, offline detection, response cache
☐ 4.3 Structured-output integration: event JSON schema via output_config.format; verify against docs.claude.com current API shape + pricing
☐ 4.4 Tier-2 prompt: system prompt + 3 rotating few-shot authored cards + arc directives; temperature/model config
☐ 4.5 Validator pipeline: clamps, whitelist, balance lint, requirement sanity, tone/blocklist filter, dedup vs run history
☐ 4.6 Prefetcher: background pool of 6–10 validated cards, refill during scrambles/between weeks; instrumented so UI never waits
☐ 4.7 Tier-3 Director: arc schema, run-start + act-transition calls, arc→directive injection into Tier 2
☐ 4.8 Fallback seams: no-key mode, API-down mode, reject-overflow mode — all invisible to player
☐ 4.9 Key management: settings pane, macOS Keychain storage, test-connection button
☐ 4.10 Telemetry: reject rate, latency, cost per run
☐ 4.11 **GATE: blind test** — 10 players play mixed runs; they cannot reliably flag generated cards (<60% detection) AND rate them equally fun. Iterate prompts/few-shots until passed
☐ 4.12 Determinism guard: LLM disabled on daily-seed mode; generated cards logged to run record

## PHASE 5 — Full content build-out to MVP then v1.0 [6–10 wks, overlaps everything]
☐ 5.1 Acts 2 content: coworking arena, Camp branch + demo-day scramble set piece, 45 more events
☐ 5.2 MVP content targets: 40 items, 70 events, 20 endings, 2 archetypes, chat voting v1 → **Steam Next Fest demo build**
☐ 5.3 Acts 3–5: three arenas + upgrade catalogs, Seed/A/B/C raise chains with board seats + control ladder, demotion flow + moving-out scramble
☐ 5.4 IPO finale: roadshow gauntlet + bell scramble + score ceremony
☐ 5.5 Staff system full: hiring pipeline, 12 staff pool, hidden traits, layoff mechanic
☐ 5.6 Industries ×3 (SaaS/consumer/AI): item packs, deck reskins, death variants; idea generator with tags
☐ 5.7 Author to v1.0 targets: 90 items, 150 events, 60 endings; run 3.4 simulator after every batch
☐ 5.8 Meta-progression: unlock tree, endings gallery, run history, daily seed mode + local leaderboard (Steam leaderboard if feasible)
☐ 5.9 Autopsy v2: visual causal graph + shareable PNG export

## PHASE 6 — Streamer suite [2–3 wks]
☐ 6.1 Twitch OAuth connect + EventSub/IRC client
☐ 6.2 Vote moments (negotiations, Door, naming) with 20s tally UI
☐ 6.3 "The Market" launch-week sentiment mechanic
☐ 6.4 Channel-point redemption hooks (configurable) + chat-credit in autopsy
☐ 6.5 Streamer mode toggle: bigger fonts, overlay-safe margins, panic-button to pause chat effects
☐ 6.6 Test live with 2–3 mid-size streamers under NDA; iterate

## PHASE 7 — Art completion & audio [parallel, 6–10 wks artist-time]
☐ 7.1 Execute asset manifest P1 then P2 per priorities (03_ASSET_MANIFEST.md §10)
☐ 7.2 Audio: 25-SFX core set, era ambience beds ×5, scramble music with final-10s escalation, grind lo-fi loop ×3, stings (term sheet, death, offer, bell)
☐ 7.3 Juice pass: screen shake, meter pops, equity-slice animation, confetti, card flips
☐ 7.4 Accessibility: colorblind check on meters, remappable keys, timer-extend assist option, text size option, reduced-flash mode

## PHASE 8 — QA, balance, hardening [3–4 wks]
☐ 8.1 Closed beta (50–100 players, Steam playtest); telemetry opt-in upload
☐ 8.2 Balance to targets: bimodal session curve, death distribution, first-death week — tune via 0.2 spreadsheet + 3.4 simulator + real data
☐ 8.3 Full run-record replay tool for bug repro; fix determinism drifts
☐ 8.4 Perf pass on Mac: Apple Silicon + Intel, low-end MacBook Air target 60fps
☐ 8.5 Save migration tests; crash reporting (sentry-like) wired
☐ 8.6 Localization decision (EFIGS later? at minimum: string table extraction now)

## PHASE 9 — Ship [3–4 wks, overlaps 8]
☐ 9.1 Steamworks setup: app, depots (mac first, windows branch), achievements (map to endings gallery), cloud saves, leaderboards
☐ 9.2 macOS: codesign + notarize pipeline in CI; universal binary; Gatekeeper test on clean machine
☐ 9.3 Steam page: capsule art, 10 screenshots, 60–90s trailer (structure: scramble panic → death montage → "You don't build a startup. You survive one."), demo build up
☐ 9.4 Marketing beats: devlog thread (build-in-public — your startup audience IS the target market), Next Fest, streamer key list (100 curated, management-sim + variety streamers), press kit with ending-card gallery, launch discount plan
☐ 9.5 Legal/ops: LLC/eula/privacy policy (LLM data flows disclosed!), Twitch dev app review, Anthropic usage-policy compliance check for the proxy decision
☐ 9.6 Launch-day runbook: hotfix branch, telemetry dashboard, community Discord
☐ 9.7 **LAUNCH**

## POST-LAUNCH backlog (pre-seeded)
☐ P.1 Windows/Steam Deck port (verified) ☐ P.2 Endings 60→120 ☐ P.3 Industries 4–5 (crypto, deeptech) ☐ P.4 Dev-proxy LLM service if key-adoption data justifies ☐ P.5 Mod support: open the JSON content format + workshop ☐ P.6 60 Parsecs-style expansion hook: "RUNWAY! 2: Web3 Boogaloo" (kidding) (mostly)

---
### Critical path summary
0.3→0.4 paper causality gate → 1.x foundations → 2.x slice gate → (3.x tools ∥ 4.x LLM) → 5.2 MVP demo → 5.x full content ∥ 6.x ∥ 7.x → 8.x beta → 9.7 launch.
**Realistic total for 1–2 people: 9–14 months to v1.0; MVP demo at month 4–6.** The two moments the whole project can die: gate 0.4 (causality on paper) and gate 2.11 (slice fun). Everything else is execution.
