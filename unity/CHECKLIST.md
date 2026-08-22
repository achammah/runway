# RUNWAY! Unity — the road to 100%

Goal: the Unity build LOOKS the same as Godot, plays the same world, is
MEASURABLY more performant, and is BETTER through the add-on waves.
Percentages are honest and only move on verified evidence (a run, a
screenshot I eyeballed, a number). WebGL is explicitly deferred.

## A — PORT (the game exists)
- [100%] A1 Engine: Runway.Core pure C#, 73/73 parity checks (dotnet), balance cross-check column-identical
- [100%] A2 Framework: Boot flow, DrawnUI ink kit, SheetLoop, Curtain, PaperInput, Env/saves, Build.BuildMac
- [100%] A3 LLM layer: schemas deep-compared identical; prompt composers byte-exact; SceneDirector ladder+watchdogs+warm gate
- [ 90%] A4 Shell screens: StudioCard/Title/Keys/HowTo written; verified only by inspection until first compile
- [ 60%] A5 Game screens + RunDriver: draft lane written; book/birth/garage/journal/dice/binder/autopsy in flight (agent resumed)
- [  0%] A6 FIRST COMPILE: editor error-harvest → fix loop → zero errors (license ✔, editor ✔)
- [  0%] A7 First boot in-editor: flow walks title→draft→birth→book→garage without exceptions

## B — PARITY (it looks and plays the same)
- [  0%] B1 Screenshot harness (Unity twin of new_screens_shot/select_shot/binder_shot/howto_shot): same states, same coordinates
- [  0%] B2 Side-by-side eyeball: title, keys, howto pages, all 7 draft pages, book, birth, garage, journal spreads, dice, beat, binder tabs — each Unity shot compared against the Godot shot, differences listed and fixed or accepted in writing
- [  0%] B3 Live first-flow probe (paid): founding → warm paint → gate → settle → PAINTED room, traced in the log like Godot's
- [  0%] B4 Week-loop probe: event → written move → clarify (incl. price ask) → dice sheet plays the engine's number → beat → effects land via Core → was-page → binder reflects it
- [  0%] B5 Save/continue probe: quit mid-run, CONTINUE restores identically (state JSON diff)
- [  0%] B6 Keyless probe: no key → authored deck only, no crashes, no network

## C — PERFORMANCE (it is measurably better)
- [  0%] C1 Unity perf harness (twin of perf_probe.gd): per-screen redraw-equivalent (SetVerticesDirty count or Profiler draw calls), RAM, CPU ms
- [  0%] C2 The comparison table, published in this file: boot-to-title, title→draft, settle click, steady-state CPU/RAM per screen, app size — Unity vs the measured Godot numbers (Godot: boot 382-620ms to draft, settle 297ms, 12-25 redraws/s, ~30fps cap)
- [  0%] C3 Every Unity number ≥ as good as Godot's, or the miss is explained and fixed
- [  0%] C4 App size with art staged: target ≤ Godot's 2.77GB (same assets; Addressables deferred with WebGL)

## D — THE ADD-ON WAVES (it is better) — parallel lanes after A6
- [  0%] D1 Line-boil shader: one screen-space wobble over all linework (8fps boil), toggleable; verified by two shots 1/8s apart differing on edges only
- [  0%] D2 Ink-reveal: the generated room paints in via animated SpriteMask brush strokes; filmed frames verified
- [  0%] D3 TMP per-character beats: ink-settle typewriter on the beat + inline die glyph; shot verified
- [  0%] D4 Cinemachine impulse: shake on backfired, punch-in on die settle; amplitude tasteful (filmed)
- [  0%] D5 Particles: spotlight dust, LOCK-IN paper scraps, title embers; shots verified
- [  0%] D6 2D lights: select spotlight as Light2D, garage bulb warmth, in-the-red dimming; shots verified
- [  0%] D7 Audio mixer snapshots: curtain duck, binder muffle, red-week low-pass; verified via mixer-state logs + listen note
- [  0%] D8 Integration pass (me): lanes merged, no visual conflicts, full B-suite re-run green

## E — SHIP
- [  0%] E1 RUNWAY-Unity.app batch-built, stamped like Godot (date+sha inside the bundle), launches from Finder
- [  0%] E2 Owner hand-off note: side-by-side table + what to feel for
- [  0%] E3 Repo synced: every lane committed, tracker at 100

DECIDED: monorepo (unity/ in this repo; game/data + prompts stay the single
source of truth, staged into the build). WebGL: full-game target, DEFERRED.
