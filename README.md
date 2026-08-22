# RUNWAY!

*You don't build a startup. You survive one.*

A satirical startup-survival roguelike played as a founder's logbook: you
write your week's move in the journal, a d20 rolls the moment you commit,
and an AI dungeon master adjudicates what the world does to you. Every
market, rival, investor, consequence and painted room is generated for
your run only. Hand-drawn ink-and-paper look throughout.

## Two engines, one game

| | Godot (original) | Unity (port) |
|---|---|---|
| Where | `game/` | `unity/` |
| Version | Godot **4.7.1** | Unity **6000.0.82f1** (Apple Silicon) |
| Build | `bash tools/build_dmg.sh` → `dist/RUNWAY.dmg` | `Unity -batchmode -quit -projectPath unity -executeMethod Runway.Build.BuildMac` → `unity/build/mac/RUNWAY!.app` |
| Compile check | — | `bash tools/unity_compile.sh` |

Both read the same data (`game/data/` prompts, archetypes, items, events —
the Unity build stages them at build time) and share the same deterministic
engine math (73-check parity suite: `unity/Runway.Core.Tests`).

## Keys

The world generates on the player's own OpenAI key, pasted once at the
in-game keys desk (stored in the OS user folder, never in the repo). No
key = the full game on the authored deck, zero network. Dev builds read
`game/.env` (`cp` your own; never commit it — the ignore rules refuse it).

## The art is not in the repo

`game/assets/` (5.4GB of generated scenes, poses, films) and
`unity/Assets/Art/` are local-only by design: the repo carries code, data
and the hand-authored registries (`layout.json`, `refs.json`, poses/patch
JSON), not pixels. A fresh clone builds and runs with drawn fallbacks;
the full look needs the art folders from a machine that has them. The
generation pipeline (`tools/scene_pipeline.py`) documents how the art was
made but requires API keys and session paths to re-run.

## Harnesses (env-gated, in both builds)

- Godot: `RUNWAY_FIRSTFLOW=<dir>`, `RUNWAY_FULLRUN=<dir>`, shot harnesses
  under `game/tests/*.gd` (`RUNWAY_STRESS_DIR=<dir>`).
- Unity: `RUNWAY_USHOTS=<dir>` (23 screenshot twins), `RUNWAY_UPERF=<dir>`
  (+`RUNWAY_UPERF_SOAK=1`) perf tables, `RUNWAY_UFLOW=<dir>` (live
  first-flow + week-loop probe, needs a key), `RUNWAY_FX_<LANE>=0`
  runtime kill-switches (BOIL, REVEAL, TEXT, IMPULSE, PARTICLES, GLOWS,
  MIX, MUSIC, SFX).

Build stamps: both builds print and draw `date · sha` (title corner);
verify a shipped build is the build you think it is before filing a bug.

## Repo map

- `game/` — the Godot game (`src/`, `data/`, `tests/`, `assets/` local-only)
- `unity/` — the Unity port (`Assets/Scripts/{Core,App,Screens,Game,LLM,Audio,Effects}`)
- `tools/` — build + compile tooling
- `docs/` — repo plan and working documents
- `PROJECT_LOG.md` — append-only build log
