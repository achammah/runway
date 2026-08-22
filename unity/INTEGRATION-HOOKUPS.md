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
- INVESTIGATE-4 (B7/B9/B15): blame sees NO repaint from any sheet-mode SheetLoop
  (birth loop, howto film, curtain sway, dice cup) — loops may be FROZEN on frame 1
  in some modes. Verify visually in the built player; ledger N16 has the repro.
- Batchmode numbers: fps/frame-ms/draws are BLIND (no present) — re-run against the
  built .app for E6; rebuild/s, tex MB, alloc, gc/s, soak pacing are valid now.
