# Integration hookups (N14) — collected as lanes land
Applied all at once at integration; each lane proven OFF-able first (D8).

## D2 ink-reveal — ACCEPTED (frames eyeballed: strokes, not a wipe)
- Hookup: GarageScreen.cs:362-364 inside AdoptComposed load callback —
  replace the 3-line fade (Group/alpha/FadeTo) with `GarageInk.Apply(_composed, tex);`
  (HideDrawnRoom(true) stays; 357-358 optional to remove — Apply sets both).
- Kill-switch: RUNWAY_FX_REVEAL=0 → InkReveal.Instant ≡ old three lines. Verified 3/3 by the lane.
- Note: GarageScreen is `sealed`, not partial — lane shipped static `GarageInk` seam
  instead; optionally add `partial` at integration (one word, line 31).
- Evidence: scratchpad/d2/ steps 0-10; pacing re-spaced to even %-per-frame
  (91→0% cream, ~8%/frame); 0.18s deaf window so the beat-closing click can't skip.

## D7 audio mix — ACCEPTED (322 assertions, evidence file spot-read)
- Hookups (6 lines, 3 files): Curtain.cs Close:159 + Open:167 + SnapShut:184 →
  SetState curtained/normal/curtained; BinderScreen.cs:59 → "binder", OnDestroy:64 →
  "normal"; GarageScreen.cs:471 (breath) → SetRed(State.Cash<0) (idempotent).
- Registration seam for future audio: RegisterSource(src, "music"|"sfx"|"world")
  beside each AddComponent<AudioSource>. No Install() needed.
- Kill-switch RUNWAY_FX_MIX=0 verified (no filter, base volume held).
- GAP FOUND: nothing plays audio yet — MusicManager/sfx cues unported (ledger C6).
  NEW WORK ITEM: port music loops + 14 sfx cues, register each with the mix.
- Manifest: com.unity.modules.particlesystem ADDED by me (D5 needs it; a missing
  module was failing the shared compile gate for every lane).

## D4 impulse — ACCEPTED (curves + zero-alloc verified in two independent runs)
- Hookup A (backfired shake): TurnRunner.cs TurnRoutine after line 256 RaiseCurtain():
  `yield return new WaitForSecondsRealtime(0.55f);`
  `Runway.Effects.Impulse.Verdict(ContentDb.Str(dm, "verdict"));`
  (ordering trap documented: at line 237 the curtain still masks it; without the
  wait the 250ms shake hides under the 450ms sweep. Line 199 = day-one path, gets nothing.)
- Hookup B (die punch): DiceRoll.cs HoldThenFinish() line 106 first statement:
  `Runway.Effects.Impulse.DieSettled();` (skip path deliberately unpunched)
- Optional: Impulse.Install() in Boot.BootFlow (log line only).
- Restraint law in the file: Verdict() no-ops for every band except backfired.
- Kill-switch RUNWAY_FX_IMPULSE (+ SetEnabled for the D8 matrix). Return-to-rest EXACT.
- Constraints ledgered: never Punch below 1.0 (letterbox reveal); Impulse owns the
  Stage transform (rest pose captured at idle→active edge).

## PERF harness — ACCEPTED (real findings, my fix list below)
- Hookup: add "RUNWAY_UPERF" to Boot.HarnessVars (Boot.cs:430), one line.
- FIX-1 (E2 headline): Curtain.Update rewrites TMP text/colour/position every tick
  → 2118 rebuilds/s while shut (target ≈12). Move writes onto the 12fps baked clock.
- FIX-2 (E4): ArtCache holds every sprite forever → steady 704MB vs the 400MB bar.
  Needs LRU/scene-exit eviction while keeping sheets' Release() semantics.
- FIX-3 (E5): draft chr_loop hydrator stalls main thread up to 889ms → async/sliced.
- INVESTIGATE-4 DOWNGRADED: sheet loops verified ALIVE in the built player (keys
  mascot region differs across 1.3s captures) — blame silence was a batchmode
  artifact. Recheck per-screen during B-suite side-by-sides; ledger N16 has repro.
- Batchmode numbers: fps/frame-ms/draws are BLIND (no present) — re-run against the
  built .app for E6; rebuild/s, tex MB, alloc, gc/s, soak pacing are valid now.

## D3 beat text — ACCEPTED (frames eyeballed: inline die chit + mid-settle chars)
- Hookup (one line): ReadingBeat.cs:243 →
  `float secs = ReadingBeatText.Apply(b, Mathf.Clamp(body.Length / 95f, 0.3f, 6.5f));`
  (line 244 WriteIn stays — it keeps _draining/skip/click; Apply must run first.)
- Kill-switch RUNWAY_FX_TEXT=0 → byte-identical text, Apply returns caller's number.
- Sealed-class wall again (ReadingBeat) → static seam, optional `partial` word later.
- FLAG: manifest LIES — Unity 6 resolves builtin TMP 5.0.0/uGUI 2.0.0 over the
  3.0.9/1.0.0 request; make the manifest tell the truth at integration.
- Note: capitalized verdicts are dead words in BOTH engines (faithful); the punch
  rides the plain sentence. `beat.Say("", band)` would surface them if ever wanted.

## MusicManager (my port, gate pending)
- Zero hookups: self-installs BeforeSceneLoad, polls Boot.Instance 4x/s with
  main.gd's exact mapping (title/selection/garage/in_the_red/last_page,
  whistle≥75 hum≥55), bar-trimmed seamless loops, 2.5s dB crossfades,
  registers with RunwayMix, silent under harness envs, RUNWAY_FX_MUSIC=0 kill.

## D6 glows — ACCEPTED (normal vs red frames eyeballed: temperature is real)
- Hookups (3 lines): GarageScreen.cs after :146 BuildHud() →
  `Runway.Effects.GlowSprites.Apply(_room, Runway.Effects.GlowScene.Garage);`
  GarageScreen.cs Update() before :477 (inside 12fps block) →
  `Runway.Effects.GlowSprites.SetRed(State.Cash < 0);`
  FounderDraftScreen.cs after :142 (before pages) →
  `Runway.Effects.GlowSprites.Apply(Rect, Runway.Effects.GlowScene.SelectStage);`
- Laptop glow self-tracks item_itm_laptop — no hookup needed.
- Shader Resources/Shaders/RunwayGlow (one file, additive+multiply via blend props);
  stripped→graceful alpha fallback. Kill RUNWAY_FX_GLOWS=0 verified.
- N14 eyes: D6-B9 two red layers (my cold multiply + existing warm _redVignette
  stack — vignette alpha at GarageScreen.cs:477-484 is the knob if muddy);
  D6-B3 sibling pinning means D5 motes added later draw UNDER the glow — reorder
  at integration if motes should live in the light.

## D1 line-boil — ACCEPTED (edges-diff eyeballed: outlines only, interiors+text black)
- Required hookups (4 lines): DrawnUI.cs AddInkEdge before :310 + Rule before :328 →
  `DrawnBoil.Apply(img, st.Seed);` / `DrawnBoil.Apply(img, seed);`
  Boot.cs Go after :350 + OpenOverlay after :387 → `DrawnBoil.Sweep(rt);`
- Optional sweeps (post-Build ink): Curtain.Panel :113, DiceRoll.BuildParts :49,
  ReadingBeat.Begin :70, BinderScreen.BuildParts :66, JournalPage.MarginMark :528.
- Motion bounded ≤1.5px BY CONSTRUCTION (same bake through a smooth 3-state field,
  |v|=|w|=|v−w|); fills structurally identical; worst measured edge move 0.919px.
- Kill RUNWAY_FX_BOIL=0 → byte-identical renders (md5-proven).
- N15 DECISION FOR ME: giant sheets (1140x880 keys/howto) — bake 41ms + boil 42ms
  doubles an already-over-bar build frame; DrawnBoil.MaxPixels at 1<<19 exempts
  exactly those two. Decide with E-numbers at integration.

## D5 particles — ACCEPTED (scraps + motes frames eyeballed: in the hand, restrained)
- Hookups (4 lines): DraftSelectPage.cs:63 after page rect → Motes.DraftSpotlight(_page);
  GarageScreen.cs:145 after BuildRoom() → Motes.GarageBulb(_room);
  WeekCommit.cs:171 after fillAmount=1 → Scraps.Burst(_lockRow);
  TitleScreen.cs:65 before stamp label → Embers.TitleFire(_root);
- Hand-rolled sim (no ParticleSystem): UI-geometry rendering — REQUIRED under
  ScreenSpaceOverlay (a Renderer would draw invisibly behind the canvas).
  Manifest module reverted; nothing references it.
- Budgets measured: motes 0.031ms, embers 0.015ms, 0 B/frame GC; burst counts vary 6-10.
- CROSS-LANE LESSON (ledgered): a code-added Graphic can come up with NULL
  canvasRenderer and silently never draw — Mount adds it by hand.

## P0 parity harness — ACCEPTED (23/23 twins, exit 0, reproduced twice)
- Zero hookups (BeforeSceneLoad env injection). Invoke:
  RUNWAY_USHOTS=<dir> RUNWAY!.app/Contents/MacOS/RUNWAY! -screen-width 1536 -screen-height 1024 -screen-fullscreen 0
- FOUR SHIPPED-FILE BUGS FOUND (mine to fix):
  P0-F1 SheetLoop.PlaySheet delivers NO frame in a player build (howto/birth/curtain
        films EMPTY after 10s; PlaySequence path fine) — explains the perf lane's
        "no sheet repaint" too. FIX FIRST.
  P0-F2 untextured SheetLoop draws an opaque WHITE rect (Awake sets color=white).
  P0-F3 title film first frame > 2.1s (blank in strict twin, ok at +2.5s).
  P0-F4 FounderDraftScreen.OnBuild calls _select.Select(0,false) on an INACTIVE page
        → "Coroutine couldn't be started" → default founder idle never plays.
- Fixture divergences (accepted): Godot binder prints raw cofounder role int (its own
  bug); metric_history product/week fields uncharted in both.

## My perf fixes (post-lane)
- Curtain 12fps line quantizer: DONE (committed).
- ArtCache one-decode-per-frame pump: DONE (committed).
- ArtCache.Sweep(280MB, 45s age guard): code in; HOOKUP → one line in Boot.Go
  after the old screen is torn down: `Runway.Game.ArtCache.Sweep();`
- SheetLoop OnEnable deferral + textured-only visibility, DraftLoop deferral: DONE.

## A-TAIL fixes applied (all in unowned files) + deferrals
- APPLIED: RunSave ObjectCreationHandling.Replace (saves no longer corrupt on
  CONTINUE), GetMetaF int fix, SaveSlots state-less guard, Env whitespace trim,
  empty-founding fallthrough, empty-verdict stand-in, paid-transport retry with
  its own line, founding tier=founding (50s watchdog), keyless warm skip,
  LLM parse witnesses x2, MusicManager FileUrl (space-in-path).
- DEFERRED (owned files): BookIntroScreen #11 — Update() must POLL WarmPaint
  when _holdingPaint (first-run PaintSettled has no subscriber; door can hold
  forever) → apply when FIX-CHARTS lands. KeysScreen privacy copy vs middleware
  ("never sent anywhere but OpenAI" is false while the render middleware takes
  the key header) → reword when FIX-TYPE lands.
- SFX: 21 free-file sites WIRED (garage/journal/curtain/commit/reveal/beat/
  autopsy). Draft sites (7-14, 19) + DiceRoll 17 wait for FIX-SELECT/FLOW.
  Nav-factory click decision: ADOPT (house doctrine: every input answers).
- Cleanup owed: a fixture save (Driftdeck, slot 2) may sit in the real user
  folder from a lane's live run — verify + remove.

## B-VERDICT wave applied (P1-P7, P9)
P1 painted theater mounted (scene.png staged local; SelectStage glows deleted —
no Godot counterpart), P2 loop clock continuous (once-mode still starts at 0),
P3 bag box uv-trimmed to Godot's 325x460, P4 bag labels TopLeft, P5 birth halo
as TMP outline (pill dead), P6 I_HOLD 0.3s, P7 keys copy one-line, P9 book col
1080. DEFERRED: P8 binder block-height formula (±2-4px at breaks), P10 title
film phase (load-rate nondeterminism, cosmetic), P11 tab tilt, mote count tune
toward Godot's 14.
