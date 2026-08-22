# RUNWAY! Unity — the hand-off

## What this is
The full game, ported: same engine math (73/73 checks), same prompts
byte-for-byte, same art, same world — plus the add-on wave Godot never had:

- **Living linework** — every ink edge and rule boils at 8fps (never text).
- **The room paints itself in** — brush-stroke reveal on every generated room.
- **Ink-settle text** — the beat writes in per-character; the die stamps into
  the sentence as a red chit; verdict words settle with a punch.
- **Weight** — a backfired week flinches the frame once; the die settling
  punches in 2%. Fine weeks stay still on purpose.
- **Drawn air** — dust in the light cones, paper scraps off the LOCK-IN,
  embers off the title fire. All budgeted, zero GC.
- **Light** — a breathing warm pool under the garage bulb, the laptop's cool
  counterpoint, the select stage's lit beam. In the red, the warmth dies and
  the room goes cold.
- **A mix that knows** — music ducks behind the curtain, muffles under the
  binder, thins when cash goes red. The OST self-drives off game state.

Every effect has a runtime kill-switch (`RUNWAY_FX_*=0`) — the D8 matrix ran
all nine variants: exit 0, 23/23 screens, zero exceptions each.

## How to run
- App: `unity/build/mac/RUNWAY!.app` (stamp bottom-left of the title:
  date · git sha · editor — check it matches what you expect).
- Keys: same desk as Godot — paste once, stored in the user folder.
- Keyless: full game on the authored deck, zero network.

## What to feel for (vs the Godot build)
1. Boot → title: the film starts immediately; the how-to and birth films are
   instant (they ship GPU-ready — Godot pays this at export, Unity now too).
2. Settle in: the room paints in strokes instead of fading.
3. Lock a week: scraps fly, the curtain ducks the music, the die lands with
   weight, a backfire flinches the frame.
4. Open the binder: the world muffles until it closes.
5. Go broke: the room loses its warmth and the music thins.

## The numbers (measured on built apps, this machine)

| | Godot (shipped DMG) | Unity (RUNWAY-Unity.app) |
|---|---|---|
| App size | 2.2 GB | 682 MB |
| Boot -> first paint | ~2-3s | ~4s (2.3s is the mandatory splash; plan: preload during it) |
| Title -> draft | 382-620 ms | instant (screens build in <40ms; worst construction 176ms on title) |
| SETTLE -> room | 297 ms | ~200 ms measured live |
| Films (howto/birth/curtain/dice) | streamed, instant | imported GPU-ready, instant (were 2.8-6.6s streamed) |
| UI repaints | 12-25 redraws/s | 12-24 rebuilds/s (curtain was 2118/s before the fix) |
| Uncapped frame time | n/a (30fps cap) | 2.5-3.6 ms (280-405 fps headroom), capped to 30 in play |
| Steady RAM (alloc) | — | 81-137 MB vs the 400 MB bar |
| Hitches (10-min soak, pre-fix build) | multi-minute freezes reported live | 31 frames >50ms / 17915; worst 890ms — since fixed (decode pump); re-soak pending |
| Fresh per-screen table | — | scratchpad uperf/unity_perf.md (harness-written) |

## Harnesses (all in the app, env-gated)
- `RUNWAY_USHOTS=<dir>` — 23 screenshot twins of the Godot shot harnesses.
- `RUNWAY_UPERF=<dir>` — per-screen perf table; `RUNWAY_UPERF_SOAK=1` the
  10-minute hitch hunt.
- `RUNWAY_FX_<BOIL|REVEAL|TEXT|IMPULSE|PARTICLES|GLOWS|MIX|MUSIC>=0` — kill
  any lane at runtime.

## Known gaps (honest)
- Patch-room/assembled-stage rungs and the era-transition overlay are not
  ported (the move still fires; the room comes from the director or the
  drawn fallback).
- The finale ceremony is the minimal autopsy path.
- SFX cues are not yet wired (the mix and the OST are; the 14 cue files
  ship, each needs its play call + RegisterSource line).
- Godot's binder prints a raw cofounder role int in one line; the port
  prints the word. The port is right; expect that one line to differ.
