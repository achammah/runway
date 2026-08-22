# RUNWAY! Unity — the road to 100% (master plan v2)

## WHAT 100% MEANS (the award bar)
100% is not "ported". 100% is an indie game a festival jury remembers:
- **LOOK**: one coherent hand-drawn world. No static pixel anywhere a player
  stares — linework boils, paper breathes, rooms paint themselves in. Every
  screen could be a poster; every transition is an authored moment, never a cut.
- **FEEL**: every input answers within 100ms with motion + ink + sound layered
  and restrained. Dice have weight. Money hurts. The die settling is a held
  breath. Nothing pops; everything arrives.
- **TRUTH**: the world is the same world as the Godot build — same prompts,
  same engine math (73-check parity), same art — so the comparison is honest.
- **PERFORMANCE**: 30fps locked with zero hitches >50ms across a 10-minute
  session; boot→title <2s warm; settle→room <300ms; steady RAM <400MB with
  sheets resident, floors recovered on screen exit; every number in a table
  beside Godot's measured numbers, equal or better, misses explained and fixed.
- **ROBUSTNESS**: every failure state is authored — keyless, offline, slow
  upstream, hung request, empty save — no raw defaults, no freezes, loud logs.

Per-item format: `[ %] id — Build · Verify · 100% =`
A % moves only on evidence: a run, a screenshot/film I eyeballed, a number.

---

## THE DAG (lanes, dependencies, agents)

```
N0 Core+Framework+LLM (done)
 └─ N1 Game screens+RunDriver (agent, in flight)
     └─ N2 COMPILE LOOP (me)
         └─ N3 BOOT WALK green (me)
             ├─ N4 Parity harness (AGENT-P0) ──► N5a/N5b/N5c parity-fix lanes (AGENTS, per screen group)
             ├─ N6 Perf harness + baseline (AGENT-PERF)
             ├─ D1 Line-boil shader (AGENT-D1)      [own files: Shaders/, DrawnUI.Boil.cs partial]
             ├─ D2 Ink-reveal (AGENT-D2)            [GarageScreen.InkReveal.cs partial + Effects/]
             ├─ D3 TMP beat animation (AGENT-D3)    [BeatScreen.TextFx.cs partial]
             ├─ D4 Impulse/shake (AGENT-D4)         [Effects/Impulse.cs — NO Cinemachine dep, hand-rolled]
             ├─ D5 Particles (AGENT-D5)             [Effects/Motes.cs, Effects/Scraps.cs, Effects/Embers.cs]
             ├─ D6 Soft-light sprites (AGENT-D6)    [Effects/GlowSprites.cs — NOT URP: additive drawn glows fit the style and avoid a pipeline migration]
             └─ D7 Audio mixer (AGENT-D7)           [Audio/RunwayMixer.cs + .mixer asset]
                 └─ N14 INTEGRATION (me): hookups, conflict sweep, full B re-run
                     ├─ N15 Perf table final (me, needs N6)
                     └─ N16 Build .app + stamp (me) ─► N17 SHIP
```

**Parallelism contract** (what makes the lanes independent): every feature
lane ships ONLY new files plus C# `partial class` extensions of existing
screens — no lane edits a shared file. Each lane exposes one static
`Apply(...)` entry point and a `RUNWAY_FX_<NAME>` scripting-define kill-switch.
I do the one-line hookups at N14. A lane that needs a shared-file change
writes the request into its report instead of making it.

**Agent brief template** (used for every lane): context files to read →
exact deliverable files → the Godot reference behavior → the verification
the agent must run itself (screenshot/film + where to save it) → the ledger
(COMPILE-RISKS.md) duty → no-git/no-game/-edits rules → report format.

**My verification at integration**: compile clean → boot walk → B-suite
probes → eyeball every lane's evidence myself → the D-lane kill-switches
each toggled once to prove isolation → perf re-run (no lane may cost >1ms
frame time; measured).

---

## A — PORT FOUNDATION
- [100%] A1 Engine parity: 73/73 dotnet checks · re-run each Core touch · 100% = suite green forever
- [100%] A2 Balance cross-check vs Godot table (column-identical off-draw) · rerun on Core change
- [100%] A3 Schemas deep-compared to llm_client.gd · re-diff on schema change
- [100%] A4 Prompts byte-exact from event_generator.gd · re-diff on prompt change
- [100%] A5 Framework (Boot/DrawnUI/SheetLoop/Curtain/PaperInput/Env/Slots/Build)
- [ 90%] A6 Shell screens written (Studio/Title/Keys/HowTo) · verify = compile + walk + parity shots
- [ 60%] A7 Game screens + RunDriver (agent) · verify = its report + my compile
- [  0%] A8 First compile: harvest → fix → zero errors · 100% = `-batchmode -quit` exit 0, log clean
- [  0%] A9 Console hygiene: zero errors AND zero warnings from our code at boot · 100% = clean log budget enforced
- [  0%] A10 Boot walk in-editor: title→draft→birth→book→garage, no exceptions · 100% = scripted walk exits green
- [  0%] A11 The stamp: date+sha baked at build, drawn on title corner, printed at boot (twin of Godot's)
- [  0%] A12 Session logging: every FOUNDING/WARM/paint/clarify/settle line prints like Godot's (grep-parity list)
- [  0%] A13 Watchdog parity: request hang → cancelled+retried (fault-injection test with a black-hole URL)
- [  0%] A14 Keyless mode: full boot→draft→garage on authored deck, zero network calls (probe with network denied)
- [  0%] A15 Save round-trip: state JSON out == state JSON in (diff empty) after a 3-week run
- [  0%] A16 Slots: 3 slots, meta lines, overwrite labeling, continue restores (probe)
- [  0%] A17 Key desk: writes keys.env, reload brings LLM up (probe)
- [  0%] A18 Error states authored: LLM fail → retry once → plain-words line on screen (never raw)

## B — VISUAL PARITY (screen by screen; each: Build twin-shot state · Verify side-by-side vs the Godot shot · 100% = differences zero or accepted in writing here)
- [  0%] B1 Harness: UnityShots.cs — same states as new_screens_shot/select_shot/binder_shot/howto_shot/birth_shot/traits_shot, saved to scratchpad
- [  0%] B2 Studio card (fade timings, underline draw)
- [  0%] B3 Title: film loop playing, stamp corner
- [  0%] B4 Title menu: paper buttons ease-in, ghost text absent
- [  0%] B5 Slot table: 3 dossiers, ago-times, back
- [  0%] B6 Keys desk: pitch copy, mascot idle, no cleartext
- [  0%] B7 HowTo p1/p2/p3: loops un-squished, dots, captions
- [  0%] B8 Draft sign page (deal-another works)
- [  0%] B9 Draft select: spotlight ON founder, idle loop playing, traits block, stat-click tip open
- [  0%] B10 Draft name: witness idles, inputs
- [  0%] B11 Draft shape page
- [  0%] B12 Draft crew: named cards, redeal, capline
- [  0%] B13 Draft money page
- [  0%] B14 Draft bag: categories, scroll+drawn bar, detail card with trait mods, loadout+in-play lines
- [  0%] B15 Birth: intro→loop seamless, phase lines
- [  0%] B16 Book: entry, field notes after entry, gates (door absent until entry+paint), drawn scrollbar
- [  0%] B17 Garage room: composed painting adopted, ribbon during paint, HUD chips
- [  0%] B18 Journal was-page: headline, diary line, receipts, strips
- [  0%] B19 Journal decision page: event body, ask line, telegraph formula line
- [  0%] B20 Clarify on page: question line, chips (amount + price kinds)
- [  0%] B21 Dice: full-height sheet play over darkening page, engine number == shown number (5 rolls)
- [  0%] B22 Beat: title, judgement sentence, scroll + ▼ honesty, look-up close
- [  0%] B23 Curtain: sway loop, considering line, sweep
- [  0%] B24 Binder ×9 tabs incl. ledger effects + pricing verdicts (twin of binder_shot with offers)
- [  0%] B25 Autopsy-minimal: summary + back
- [  0%] B26 Transition sweep: film every swap (fade, journal rise/drop, binder rise) — no pops anywhere

## C — BEHAVIOR PARITY (live, paid where needed)
- [  0%] C1 First-flow probe: sign→world→day-one written→gate→settle→PAINTED room, full trace
- [  0%] C2 Week loop ×3 weeks: event→move→clarify(silent)→dice→beat→effects→was-page→binder deltas
- [  0%] C3 Clarify ask-paths: amountless ad move asks amount; unpriced sell asks price and SETS the engine price
- [  0%] C4 Money law: written $1,500 spend debits exactly; era-clamp; THE RED countdown appears at cash<0
- [  0%] C5 Pricing: $500 session ≈ zero adds/revenue in play; fair price pays (numbers from the tick log)
- [  0%] C6 Traits at the table: doors-open advantage visible in telegraph; luck reroll observed in logs (seeded)
- [  0%] C7 No-repeat: 4 weeks, no repeated leads/titles (log check)
- [  0%] C8 Paint resilience: black-hole render URL → ladder retries → authored room + ribbon, game never blocks (fault injection)

## D — THE ADD-ON WAVES (each: Build · Verify by film/shot · 100% = the award bar for that effect; kill-switch proven)
- [  0%] D1a Line-boil: screen-space or per-material wobble shader (2-frame 8fps boil) on ink textures
- [  0%] D1b Boil coverage: DrawnUI borders, rules, rings, buttons — one material path
- [  0%] D1c Boil restraint: text glyphs EXCLUDED (readability), amplitude ≤1.5px, film shows edges-only motion
- [  0%] D2a Ink-reveal: brush-stroke mask sequence (drawn strokes texture set)
- [  0%] D2b Reveal wired to composed-room adoption (and to weekly repaints)
- [  0%] D2c Reveal film: 12 frames, room paints in stroke by stroke, ≤1.2s, skippable
- [  0%] D3a Beat typewriter: per-char ink-settle (alpha+2px drop, 40 chars/s, click = all)
- [  0%] D3b Inline die glyph in the judgement sentence (TMP sprite asset from dice art)
- [  0%] D3c Verdict emphasis: BRILLIANT/BACKFIRED words get one-time scale-settle (film)
- [  0%] D4a Impulse kit: hand-rolled spring camera offset (no package dep)
- [  0%] D4b Wired: backfired week = 6px 250ms shake; die settle = 2% punch-in 120ms (film both)
- [  0%] D4c Restraint pass: fine/brilliant weeks = NO shake (film proves absence)
- [  0%] D5a Spotlight dust motes (select+garage bulb): ≤40 particles, drawn dot sprite
- [  0%] D5b LOCK-IN paper scraps burst (6-10 scraps, 0.8s, gravity)
- [  0%] D5c Title embers off the burning runway (matches Godot title's fire line)
- [  0%] D6a Soft-light sprites (NOT URP): additive radial glows — garage bulb warm pool, laptop glow
- [  0%] D6b In-the-red dimming: room multiply layer eases to 0.85 + cold tint (shot at cash<0)
- [  0%] D6c Select spotlight glow matches the regenerated stage art (shot)
- [  0%] D7a Mixer asset: Music/SFX/World groups, 3 snapshots (normal, curtained, red)
- [  0%] D7b Wired: curtain duck −6dB 0.3s; binder muffle LPF; red-week thin filter (state-log verify + listen note)
- [  0%] D8 Kill-switch matrix: each D lane toggled off compiles+boots clean (7 runs)
- [  0%] D9 INTEGRATION: hookups merged, no shared-file conflicts, B-suite re-run green, each lane ≤1ms frame cost (measured)

## E — PERFORMANCE (twin harness, published table)
- [  0%] E1 UnityPerf.cs: per-screen 3s averages — frame ms, draw calls, RAM, canvas rebuilds — same screens as perf_probe.gd
- [  0%] E2 Canvas hygiene: animated elements on their own canvases; rebuild storms = 0 outside animation frames
- [  0%] E3 30fps cap + vsync policy set; frame pacing verified (no 2-frame stutters in film)
- [  0%] E4 Texture residency: sheets released on screen exit (RAM floor returns; numbers)
- [  0%] E5 Hitch hunt: 10-minute scripted session, zero >50ms spikes (Profiler capture)
- [  0%] E6 THE TABLE (in this file): boot→title, title→draft, settle→room, steady CPU/RAM per screen, app size — Unity vs Godot measured (Godot: draft 382-620ms, settle 297ms, redraws 12-25/s)
- [  0%] E7 Every row equal-or-better or fixed; misses explained in writing
- [  0%] E8 App size ≤ Godot 2.77GB (same staged art)

## F — SHIP
- [  0%] F1 Build.BuildMac batch build succeeds headless
- [  0%] F2 Stamp verified INSIDE the app bundle (twin of the pck check)
- [  0%] F3 Launch from Finder on this Mac: full first-flow played by me in the BUILT app (not editor)
- [  0%] F4 RUNWAY-Unity.app + side-by-side hand-off note (what to feel for, the perf table)
- [  0%] F5 Repo synced: all lanes committed, this file at 100

DECIDED: monorepo; game/data+prompts single source (staged at build).
DEFERRED: WebGL full-game; URP migration (soft-light sprites chosen instead —
style-truer and zero pipeline risk); Addressables (paired with WebGL later).
