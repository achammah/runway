# RUNWAY! — Product Requirements Document (v1.0)

**Working title:** RUNWAY! *(alternatives: Crash & Burn, Ramen Profitable, Term Sheet)*
**Platform:** macOS first (Apple Silicon + Intel universal binary), Windows/Steam Deck fast-follow
**Genre:** Startup survival roguelike — timed panic phases + turn-based consequence management + light tycoon layer
**Tone:** Dark comedy, HBO Silicon Valley-adjacent, satirical but authored by someone who has actually lived it
**One-liner:** *You don't build a startup. You survive one.*

---

## 1. Vision & Design Pillars

The player takes a company from "quitting your job tonight" to IPO — or, far more often, to one of 120+ collectible, named, illustrated deaths. Runs last 20 minutes (early crash, early acquihire) to several hours (the full unicorn arc), shaped by a risk-it-or-bank-it structure.

**Pillar 1 — Panic, then consequences.** The signature loop alternates a real-time timed SCRAMBLE (grab what you can in 60–90 seconds) with a turn-based GRIND (week-by-week survival where what you grabbed determines what's possible). Nothing enters the game that doesn't feed one of these two phases.

**Pillar 2 — Every death is traceable.** No unfair RNG deaths. Every catastrophe is foreshadowed and causally linked to a player choice. The end-of-run Autopsy screen proves it.

**Pillar 3 — Failure is content.** Crash-and-burn endings are collectible, named, illustrated, and shareable. Players chase deaths on purpose.

**Pillar 4 — The office is the progress bar.** Growth is rendered spatially: Garage → Coworking → First Office → Startup Floor → HQ. The office is simultaneously the tycoon canvas, the survival shelter, and the scramble arena.

**Pillar 5 — Your score is YOUR equity.** Score = founder's remaining stake × exit value. Keeping 80% of a $100M exit beats holding 3% of a unicorn. The cap table is score, danger meter, and thesis.

**Pillar 6 — Watchable by design.** Every system must be legible at 480p Twitch compression; every failure must make a 15-second clip; chat must have levers to pull.

**Pillar 7 — Infinite via hybrid content.** A hand-authored spine guarantees quality and offline play; an LLM generation layer (Claude API, schema-constrained) makes every run literally novel. See §7.

## 2. Target Audience & References

- Primary: roguelike/management players (Slay the Spire, FTL, 60 Seconds!, Reigns, Papers Please audiences), 20–40, Steam-native.
- Secondary: tech/startup workers who will recognize themselves; streamers and their audiences.
- Comparable loops: **60 Seconds!** (scramble→survive, item-driven events, dark comedy), **Game Dev Tycoon** (office-era growth fantasy), **Slay the Spire** (branching act map, risk banking), **Reigns** (fast readable decisions).

## 3. Game Structure

### 3.1 The run: Acts and eras
| Act | Era / Arena | Business stage | Typical duration |
|---|---|---|---|
| 0 | Your apartment (night you quit) | The Leap — opening Scramble | 90 sec |
| 1 | Garage | Idea → MVP | 15–25 min |
| 2 | Coworking space | Launch → first revenue; YC branch | 20–30 min |
| 3 | First real office | PMF → Seed/Series A | 25–40 min |
| 4 | Startup floor | Scale → Series B/C | 30–50 min |
| 5 | HQ | Pre-IPO | 30–50 min |
| Finale | NYSE floor | IPO roadshow + bell-ringing Scramble | 10 min set-piece |

- Act transitions are gated by milestones (product, traction, funding) and each opens with a **transition Scramble** in the new arena (move-in day, demo day, due-diligence scramble).
- **Demotion is real:** a down round or catastrophic event can kick the company back an era (with a "moving out" scramble). Heartbreaking, hilarious, streamable.
- **Acquisition offers** appear at milestone beats and after visible traction spikes: bank score and end the run, or decline and raise stakes. This creates the intended bimodal session-length curve.

### 3.2 The three layers
1. **SCRAMBLE (real-time, 60–90s):** top-down/2.5-side view of the arena; move, grab, carry (capacity-limited), deposit. Item spawns partially randomized. Two scramble types: *transition scrambles* (scheduled) and *crisis scrambles* (triggered by run state: server fire, landlord lockout, press ambush, dawn raid before the board meeting).
2. **GRIND (turn-based weeks):** each week = ration cash/coffee, assign staff to tracks (Build / Sell / Raise / Recruit / Rest), resolve 1–3 events (cards with illustrated art + 2–4 choices), answer the Door (knock mechanic: investor / landlord / journalist / IRS / YC partner / rival), watch meters.
3. **TYCOON (between weeks):** buy/sell upgrades and furniture for the current era (each has a Grind stat effect, unlocks/blocks events, AND physically exists in scramble arenas). Every upgrade raises burn rate. Deliberately thin: ~12 meaningful upgrades per era, not a furniture catalog.

### 3.3 Core resources & meters
- **Cash / Runway** (the "food"): weekly burn = rent (era) + salaries + upgrades + founder ramen budget. Zero = death (unless rescued by an event).
- **Cap table / Founder %** (score + danger): every deal costs equity. Board control thresholds: <50% unlocks "board can fire you" event deck; <25% you're effectively an employee ending-track.
- **Valuation** (grows via milestones/traction; sets deal terms).
- **Product** (0–100 per era gate) and **Traction** (users/revenue; unlocks offers).
- **Morale** (company-wide) and per-character **Burnout** (0–100 with visible comedic behavior stages; see Dossier §5).
- **Hype** (press/market attention: brings offers AND scrutiny).

### 3.4 Endings
- **IPO finale:** roadshow gauntlet (rapid-fire investor Q&A events) → bell-ringing scramble set piece → score screen.
- **Death catalog:** 120+ named illustrated ending cards at full release (60 at launch), e.g. "Died of: Foosball-Led Growth", "Acquihired For Parts", "The CFO Was A Ghost", "Rugged By Your Own Cofounder". Collection gallery ("Deaths discovered: 47/120").
- **Autopsy screen** after every run: causal chain visualization from death back to origin choices ("this traces back to the ping-pong table, week 1"). Exportable/shareable image.

## 4. Characters
- **Founder archetypes (pick 1 at run start, more unlock via meta):** The Hacker, The Hustler, The Dropout, The Ex-FAANG PM. Each modifies scramble stats (speed/capacity), grind bonuses, starting items, and unique events.
- **Cofounders & staff:** systemic objects with visible stats (competence per track, salary/equity ask, loyalty) and **hidden traits** that reveal over time (the "great on paper, toxic in month 6" cofounder is a design pillar). Staff are physically present in scramble arenas (in a fire: grab the servers or push Dave out first?).
- **Recurring NPCs:** investors (8 archetypes with hidden temperaments used by the negotiation system), landlord, journalist, YC partner, rival founder (LLM-driven nemesis arc, §7), lawyer, IRS agent.
- **Negotiation micro-mechanic:** every deal (cofounder equity, term sheets, hires, acquisitions) uses one push-your-luck slider: drag their ask down, each notch raises walk-away risk, modified by traction, competing offers, and hidden temperament. One interaction to learn, used everywhere, built for chat backseat-driving.

## 5. Meta-progression & replayability systems
- Unlocks: founder archetypes, industries (SaaS, consumer, crypto, deeptech, AI — each reskins events/items), starting scenarios, cursed modifiers (ascension-style difficulty ladder).
- **Procedural idea generator** at run start ("Tinder for compliance software") — slot-machine moment; idea tags bias the event decks.
- **Daily seeded run** (authored-content-only for determinism) with leaderboard.
- Run history / legacy log; optional "reputation follows you" modifier.

## 6. Content system (authored spine)
- Events are **data-driven cards**: JSON with requirements (items, era, meters, flags, idea tags), choices, effect ops from a bounded vocabulary, foreshadow links, and art reference. Full schema in Dossier §6.
- Launch targets: **90 items**, **150 authored events**, **60 endings**, 5 eras × 12 upgrades.
- The **item→event dependency graph is the replayability budget** and gets its own authoring tool + coverage tests (no orphan items, no unreachable events).

## 7. LLM dynamic content layer ("The Simulation Engine")
Goal: every run literally novel, virtually infinite branching — without sacrificing balance, tone, offline play, or determinism.

**Architecture — three tiers:**
- **Tier 1 (authored spine):** the 150 hand-written events. Always available, offline-safe, used exclusively for daily seeded runs.
- **Tier 2 (generated events):** at runtime, Claude generates novel event cards *within the exact same JSON schema as authored events*. The prompt receives a compact **run-state digest** (era, meters, items held, staff roster + revealed traits, recent event history, idea tags, active arcs) and must output: situation text, 2–4 choices, and effects **chosen only from the whitelisted effect-op vocabulary with clamped numeric ranges**. The local rules engine validates and can reject/regenerate; rejected or offline → seamless fallback to Tier 1.
- **Tier 3 (run director):** at run start (and act transitions), a single higher-quality call generates the run's **narrative arcs**: a rival company with a name and strategy, a recurring journalist with an agenda, a slow-burn cofounder storyline — expressed as arc objects that bias/inject Tier 2 generations across the whole run. This is what makes runs feel *authored*, not random.

**Key technical decisions (details + prompts in Dossier §7):**
- Claude API with **structured outputs** (`output_config.format` JSON schema, GA) so responses are grammar-constrained to the event schema — no parsing failures by construction.
- Model split: Haiku-class for Tier 2 volume (cheap, fast), Sonnet-class for Tier 3 direction.
- **Latency hiding:** generation is prefetched asynchronously during scrambles and between weeks into a per-run event pool; the player never waits on a request.
- **Safety rails:** effect whitelist + numeric clamps (LLM writes flavor and picks effects; it cannot invent mechanics), balance linting (net-EV bounds per era), content-tone system prompt, profanity/IP filters, "no real company/person names" rule with local blocklist.
- **Determinism:** LLM content disabled for daily runs and leaderboards; normal runs log every generated card to the run record so autopsies/replays are exact.
- **Key management:** MVP = player-provided API key (settings pane, stored in macOS Keychain) with graceful authored-only mode without one; v1.0 decision point = optional developer-run proxy with monthly generation allowance (cost model in Dossier §7.6).

## 8. Streamer & Twitch features
- **Chat integration (Twitch EventSub/IRC):** chat votes on term sheets and Door decisions; chat names the company; "The Market" mode: aggregate chat sentiment decides launch-week virality; chaos vouchers (channel points trigger crisis scrambles).
- Spectator-legible UI: oversized meters, cap-table donut always visible, minimal body text, event cards readable at 480p.
- Clip engineering: deaths, negotiations, and scramble finishes all resolve with a ≤15s dramatic beat; autopsy and ending cards are shareable images.
- Daily seed race + archetype challenges as recurring streamer formats.

## 9. Presentation
- **Art:** hand-drawn ink-outline style per reference — thick uneven line, flat fills, paper-cream background, sage green + ink black + one coral accent; minimal frame animation (style is a cost feature: scales to 600+ images cheaply). Full spec + complete asset manifest in `03_ASSET_MANIFEST.md`.
- **Audio:** comedy lives in sound — muffled chaos, ticking-runway heartbeat, era-specific office ambience, a "term sheet signing" sting. Adaptive intensity in scrambles.
- **UX:** zero-tutorial onboarding (Act 0 apartment scramble IS the tutorial), one-hand playable grind phase, full mouse-only support, remappable keys, colorblind-safe meter design.

## 10. Technology
- **Engine: Godot 4.x** (2D-first, tiny export size, excellent data-driven workflows, clean macOS export). Unity acceptable fallback if physics feel demands it — decide at end of prototype phase.
- macOS specifics: universal binary, codesigning + notarization, Steam Mac depot; App Store optional later.
- Systems: deterministic sim core (seeded RNG, replayable), JSON content pipeline with hot-reload, run-record logging (drives autopsy + bug reports), local telemetry with opt-in upload, cloud saves via Steam.
- LLM client: async HTTP wrapper, request budget/rate limiting, response cache, offline detection.

## 11. Scope tiers
- **Vertical slice (validate the loop):** Act 0 + Act 1 only; 1 archetype; 15 items; 25 authored events; 8 endings; no LLM, no meta, placeholder art.
- **MVP / demo (Steam Next Fest):** Acts 0–2; 2 archetypes; 40 items; 70 events; 20 endings; Tier-2 LLM behind a flag; basic chat voting.
- **v1.0 launch:** all acts + IPO finale; 4 archetypes; 3 industries; 90 items; 150 authored events + full LLM tiers; 60 endings; meta unlocks; daily seed; full Twitch suite.
- **Post-launch:** more industries, endings to 120+, workshop/modding (the JSON content format is mod-ready by design), Windows port.

## 12. Success metrics
- Session curve is **bimodal** (peaks ~25 min and ~2 hr) — this is the core design KPI.
- Demo: >40% of players finish a full Act-1 run; median 2+ runs per session.
- Launch: D7 retention >20%; 30%+ of players discover 10+ endings in month 1; ≥50 streamer broadcasts in launch month; wishlist→purchase >15%.
- LLM layer: <2% generation rejects surfaced to players; zero waiting states attributable to API latency.

## 13. Top risks & mitigations
1. **Scramble→Grind causality feels arbitrary** → paper-prototype before any code (first item in the TODO); fairness/traceability rules are a design constitution.
2. **Genre stack bloat** (tycoon layer grows) → Pillar 1 cut rule, 12-upgrade cap per era.
3. **LLM content is bland or breaks balance** → bounded effect vocabulary, balance linter, arcs from Tier 3, aggressive curation of authored few-shot examples; ship-gate: blind test where players can't reliably tell authored from generated.
4. **Event pool feels repetitive** (the 60 Seconds! criticism) → combo-checking events, LLM tier, per-run arc variety, coverage analytics.
5. **Solo/small-team art volume** → the chosen style is deliberately cheap; asset manifest is priority-tiered; event art reuse system (scene + character overlay composition).
6. **Mac-only launch limits streamer reach** → Mac-first but Steam/Windows fast-follow within 3 months; most streamers are on Windows.
