# RUNWAY! repo optimisation and reorganisation plan

Assessment only. Nothing in this document has been applied. Every command block is written to be
read and approved first, then run by the owner.

Repo: `https://github.com/achammah/runway` (public), branch `main`, 215 commits, no tags,
`main` and `origin/main` identical at `78e06b3`.

---

## 0. The five numbers that matter

| Measure | Value | How it was obtained |
|---|---:|---|
| Working copy on disk | 23 GB | `du -sh .` |
| Local `.git` | 192 MB | `du -sh .git` |
| **What a fresh clone actually downloads** | **25.91 MiB** | `git clone --no-local --bare` into a scratch dir, then `count-objects -vH` |
| Files tracked at HEAD | 926 files, 19.77 MB | `git ls-tree -r -l HEAD` |
| Unreachable garbage in `.git` | **91.54 MB in 142 blobs** | `git fsck --unreachable` |

The single most useful fact in this document: **the repo is not heavy, the local `.git` is.**
166 MB of the 192 MB `.git` never leaves this machine. It is reclaimed by one safe command that
rewrites no history and changes nothing on the remote.

---

## 1. WEIGHT

### 1.1 Disk by top level

| Path | Size | Tracked? |
|---|---:|---|
| `game/` | 17 GB | partly (818 files) |
| `dist/` | 4.5 GB | no (ignored) |
| `unity/` | 1.6 GB | partly (91 files) |
| `.git/` | 192 MB | n/a |
| `music/` | 61 MB | no (ignored) |
| `.snapshots/` | 25 MB | no (ignored) |
| everything else | under 200 KB | yes |

Inside `game/`: `assets/` 9.6 GB, `.godot/` 7.4 GB, `docs/` 5.2 MB, `stage_new_loop.mp4` 2.4 MB,
`runway.icns` 1.8 MB, `src/` 1.1 MB, `tools/` 620 KB, `tests/` 224 KB, `data/` 188 KB.

Inside `unity/`: `Assets/` 717 MB, `build/` 482 MB, `Library/` 460 MB, `Runway.Core.Tests/` 1.3 MB.

Inside `dist/`: `RUNWAY!.app` 2.3 GB, `RUNWAY.dmg` 2.2 GB, `dmg_bg_raw.png` 1.1 MB, `dmg_bg.png` 392 KB.

The 23 GB working copy is correct and by design. `game/assets` and `game/.godot` are generated art
and engine cache; `dist/` and `unity/build` are build output. None of it is in git.

### 1.2 Git object accounting

| Class | Objects | Raw bytes |
|---|---:|---:|
| Reachable blobs (all history, deduped) | 1410 | 52.56 MB |
| Reachable trees | 1013 | 0.44 MB |
| Reachable commits | 215 | 0.21 MB |
| **Unreachable blobs** | **142** | **91.54 MB** |

The 142 unreachable blobs are all image data: 53 PNGs at 1536x1024 (background art), 48 at 334x297
(journal icons), and 41 assorted, plus 3 UTF-8 text blobs. They were staged with `git add` before
the art ignore rules landed, then reset. **They are in no commit and no reflog entry.** They inflate
the local `.git` only; the measured 25.91 MiB clone proves they never reach anyone else.

### 1.3 The 20 largest blobs in history, and whether HEAD still needs them

Sizes below are **pack disk bytes**, not raw, so they sum against the 25.91 MiB clone directly.

| Pack MB | State | Path |
|---:|---|---|
| 5.01 | AT HEAD | `game/docs/refs/pilot_composed_hangar.png` |
| 1.84 | AT HEAD | `game/runway.icns` |
| 1.77 | DEAD | `game/runway.icns` (superseded revision) |
| 1.15 | DEAD | `game/docs/refs/_superseded/60s_reference_01.png` |
| 1.10 | DEAD | `game/docs/refs/_superseded/60s_room_survivor_supplies.png` |
| 0.95 | DEAD | `game/docs/refs/_superseded/60s_room_alt_01.png` |
| 0.94 | DEAD | `game/docs/refs/_superseded/60s_room_alt_02.png` |
| 0.88 | DEAD | `game/docs/refs/_superseded/60s_logbook_alt.png` |
| 0.85 | DEAD | `game/docs/refs/_superseded/60s_logbook_event_page.png` |
| 0.81 | AT HEAD | `game/icon_1024.png` |
| 0.75 | DEAD | `game/icon_1024.png` (superseded revision) |
| 0.70 | DEAD | `game/docs/refs/_superseded/60s_room_death_state_tallies.png` |
| 0.54 | DEAD | `game/docs/refs/60s_logbook_ration_page.jpg` |
| 0.42 | DEAD | `game/docs/refs/60s_room_crew_four.jpg` |
| 0.42 | DEAD | `game/docs/refs/60s_logbook_event_binary_choice.jpg` |
| 0.41 | DEAD | `game/docs/refs/60s_room_crew_costumes.jpg` |
| 0.40 | DEAD | `game/docs/refs/60s_room_crew_five.jpg` |
| 0.38 | DEAD | `game/docs/refs/60s_room_crew_three.jpg` |
| 0.38 | DEAD | `game/docs/refs/60s_room_crew_pair.jpg` |
| 0.37 | DEAD | `game/docs/refs/60s_room_death_skeleton.jpg` |

No `dist/*.dmg`, no `*.wav`, no `*.mp4` has ever been committed. The ignore rules held from the
first commit onward. The only heavy history is deleted reference art.

### 1.4 Clone weight by path prefix

| Pack MB | Share of clone | Prefix |
|---:|---:|---|
| 16.31 | 63% | `game/docs/refs` |
| 3.61 | 14% | `game/runway.icns` |
| 2.90 | 11% | `game/assets` |
| 1.56 | 6% | `game/icon_1024.png` |
| 1.19 | 5% | `game/src` |
| under 0.5 each | 1% | everything else |

**Four binary paths are 94% of the clone.** All actual source code, data and documentation together
are roughly 1.5 MB.

---

## 2. TRACKED VERSUS POLICY

The stated policy is: code and data tracked, generated and large art local only. The repo violates
it in both directions, and the second direction is far more serious than the first.

### 2.1 Direction one: tracked, but heavy or misplaced

| Item | Size at HEAD | Problem |
|---|---:|---|
| `game/docs/refs/pilot_composed_hangar.png` | 5.02 MB | The largest tracked file in the repo. It is harness stub art (`game/src/main.gd:349` loads it as the offline turn background) stored in a documentation directory. Art, in `docs/`, tracked. |
| `game/runway.icns` | 1.84 MB | Legitimate build icon, but two revisions live in history for 3.61 MB. |
| `game/icon_1024.png` | 0.81 MB | Same pattern, 1.56 MB across two revisions. |
| `unity/Assets/Resources/Fonts/Baloo2-Bold.ttf` + `PatrickHand-Regular.ttf` | 557 KB | Byte identical (verified by sha256) to `game/assets/fonts/*.ttf`, which are also tracked. The same bytes are stored twice. |
| `game/tools/shoot_autopsy.gd.uid` | 20 B | Tracked orphan. Its parent `shoot_autopsy.gd` exists neither in git nor on disk. |

### 2.2 Direction two: a fresh clone cannot build

This is the headline finding. `git clone` today gives you a Godot game that boots and a Unity port
that **cannot even be opened**.

283 files are untracked and **not one of them is matched by any ignore rule.** They were simply never
added, almost certainly because `game/docs/LANE_BRIEF.md` forbids `git add -A` and mandates naming
files by hand.

| Missing from git | Count | Consequence |
|---|---:|---|
| `unity/ProjectSettings/*` | 21 | Unity Hub does not recognise the folder as a project. `ProjectVersion.txt` is the only place the required editor version `6000.0.82f1` is written down, and it is not in git. **First and hardest failure.** |
| `unity/Assets/**/*.meta` | 175 untracked of 1261 on disk, **0 tracked** | Unity regenerates every `.meta` with a fresh GUID. Every serialized reference, script assignment and build-settings entry breaks by definition. |
| `unity/Assets/**/*.cs` | 26 of 97 | Does not compile. Tracked code references the missing types directly: `ContentDb`, `DraftResult`, `RunRecord`, `EffectOps`, `JournalPage`, `JournalSpreads`, `GameUi`, `RunSave`, `Sfx`, `BirthScreen`, `DrawnChart` and more. |
| `unity/Assets/Scenes/Main.unity` | 1 | No boot scene. |
| `unity/Assets/StreamingAssets/*` | 13 | Byte identical copy of `game/data/` (verified). Unity reads game content from here. |
| `unity/Assets/TextMesh Pro/*` | 81 | Vendored TMP package assets. |
| `unity/Packages/packages-lock.json` | 1 | Package resolution unpinned. |
| `unity/Runway.ATail.Tests/*` | 3 | A whole test project. |
| `unity/HANDOFF.md` | 1 | The newest and most summary level Unity doc, and the only untracked `.md` in the repo. |
| `game/assets/scenes/refs.json` | 1 | The canonical character and room asset URL registry. Read by `game/src/main.gd:1360` and `garage_view_screen.gd:805` as `CAST_REFS`, and required by `scene_pipeline.py variant` (`scene_pipeline.py:283`). Hand curated, not regenerable. |
| `game/assets/poses/_refs.json`, `_report.json`, `game/assets/patch_scenes/_refs.json` | 3 | Same class. |
| `game/tests/{binder_shot,watchdog_probe,world_reveal_shot}.gd.uid` | 3 | The other 58 `.uid` files are tracked. Inconsistent. |
| **A README, anywhere** | 0 | There is no `README.md` at any level. `git ls-files | grep -i readme` returns one file: `unity/briefs/README.md`, an internal lane index. No prerequisites, no engine versions, no clone to run path. |

### 2.3 The 578 file silent-drop trap

`git ls-files -i -c --exclude-standard` returns **578 tracked files that a `.gitignore` rule also
matches.**

| Group | Count | Caught by |
|---|---:|---|
| `game/assets/poses/<21 chars>/*.json` | 525 | `.gitignore:10 game/assets/*` |
| `game/assets/patch_scenes/<5 eras>/*.json` | 30 | `.gitignore:10` |
| `game/assets/journal_icons/*.png` | 20 | `.gitignore:10` |
| `game/assets/backgrounds/{annotations,index,slots}.json` | 3 | `.gitignore:14` |

The re-inclusion chain was built with real care for `scenes/*/layout.json` (lines 21 to 26) and the
comment above it argues the case precisely: hand authored data that "cannot be recovered by rerunning
anything". **That same argument applies to all 578 files, and the rule was never extended to them.**
They survive only because they were added before the ignore rule landed.

Existing files keep syncing, because git tracks a file regardless of ignore rules once it is in the
index. The danger is prospective: **any new pose JSON, any new journal icon, any new patch scene
room, any new backgrounds index is invisible to `git add`, `git status` and `git add -A`.** No error,
no warning. Combined with the `LANE_BRIEF.md` rule to add files by name, a lane that authors a new
pose set and runs `git add game/assets/poses/newchar/` gets silence and loses the work.

This is a live data loss risk, not tidiness.

### 2.4 The reproducibility promise does not hold

`.gitignore` lines 6 to 8 justify excluding 5.4 GB of art on the grounds that "all of it is
reproducible from tools/scene_pipeline.py". Three problems:

1. The path in that comment is wrong. The file is `game/tools/scene_pipeline.py`.
2. `game/tools/scene_pipeline.py:30` reads:
   `SCRATCH = "/private/tmp/claude-501/-Users-assem-Documents-Doc-Assem-Claude-Code-runway/46461c38-41e8-4daa-aa34-0dc94af8f9ef/scratchpad"`
   and `:99` is `return open(f"{SCRATCH}/{name}").read().strip()`. **API keys are read from a
   hardcoded, session scoped scratchpad path baked into committed source.** There is no environment
   variable fallback and no CLI flag. On any other machine, and in any later session on this machine,
   this is an immediate `FileNotFoundError`. `gen_backgrounds.py:40` and `patch_factory.py:59` import
   `_key` from it, so they inherit the same break.
3. It depends on a private hosted middleware (`nano-banana-production-e03b.up.railway.app`) plus paid
   Seedream and GPT Image endpoints.

So the art is not reproducible by anyone but the author, in the original session. The ignore policy is
still the right call, but the README must say plainly that the art is not in the repo and not
recoverable from it, rather than implying a rerun restores it.

### 2.5 What actually breaks first, in order

**Godot (`./play.sh`): it works.** No hard crash, no blank boot.

1. Engine version gate. `game/project.godot:10` declares feature `"4.7"`. Anyone on 4.5 or 4.6 gets a
   newer engine dialog. The version appears nowhere else in git.
2. Full reimport on first open. `*.import` is ignored and zero are tracked, but the clone contains
   only 23 importable assets, so this takes seconds. `play.sh` already runs `--import` first.
3. `game/src/core/music_manager.gd:31` and `:47` call `load()` on `res://assets/music/*.wav` with no
   `ResourceLoader.exists` guard. Seven red `Failed loading resource` errors. The game is silent but
   runs. This also breaks the `LANE_BRIEF.md` gate that `grep -cE "SCRIPT ERROR"` must be 0.
4. Boots to `KeysScreen`, which is fully code drawn and renders correctly using the tracked
   `PatrickHand-Regular.ttf`. Offers "play without".
5. `TitleScreen` is a black screen. `title_screen.gd:85` misses `assets/title/base.png`, falls back to
   `assets/title/title_screen.png` at `:90`, gets `null`, and the `if art:` guard skips assignment.
   `assets/title/` has zero tracked files. Input still works, so the player must press a key on faith.
   This is the moment it looks broken.
6. Draft and garage screens run artless via well guarded `ResourceLoader.exists` fallbacks. The
   journal survives fully intact: all 20 icons and `ui/logbook_page.png` are tracked.
7. `SceneRoom` refuses all 44 scenes. The 44 hand authored `layout.json` files, which the whole ignore
   chain exists to preserve, are present with nothing to lay out.

**Unity: hard fail at step zero.** Cannot open (2.2, row 1), cannot compile (row 3), no scene, no meta
discipline. The art degrade path (`EnsureSheets`, `EnsureStreamingArt`, `RunwayPaths.ArtRoot`) is
correctly built and warns cleanly, and is the one part of the Unity story that works as intended.

---

## 3. STRUCTURE

### 3.1 Current top level

```
runway/
├── .gitignore                       25 path rules encoding the current layout
├── .DS_Store                        ignored
├── .snapshots/                      ignored, 25 MB, 2 snapshots from 2026-08-19
├── dist/                            ignored, 4.5 GB, but holds 2 build INPUTS
├── game/                            Godot project, 17 GB
│   ├── project.godot, Main.tscn, export_presets.cfg
│   ├── icon.png, icon_1024.png, runway.icns
│   ├── .env (ignored), .env.example, build_stamp.txt (untracked)
│   ├── src/  data/  tests/  assets/
│   ├── tools/                       26 .py pipeline tools + 17 batch_*.sh + 1 orphan .uid
│   ├── docs/                        7 .md (100 KB) + refs/ (5.1 MB, one PNG + 27 orphan stubs)
│   ├── .godot/ (ignored), stage_new_loop.mp4 (ignored)
├── initial_info/                    4 founding charter docs, superseded
├── music/                           ignored, 61 MB, master WAVs
├── prompts/                         art and music generation prompt archive
├── tools/                           build_dmg.sh, unity_compile.sh
├── unity/                           Unity project, 1.6 GB
│   ├── Assets/  ProjectSettings/  Packages/  Runway.Core.Tests/  Runway.ATail.Tests/
│   ├── Library/ build/ Logs/ UserSettings/   ignored
│   ├── briefs/                      10 lane briefs
│   └── CHECKLIST.md COMPILE-RISKS.md HANDOFF.md INTEGRATION-HOOKUPS.md MIGRATION.md
├── image_prompt_gen.md              90 KB, the Nano Banana JSON spec (locked decision D7)
├── openapi-layer-decomposition.yaml
├── play.sh                          player entry point
├── snapshot.sh                      pre git safety net, now superseded
├── PROJECT_LOG.md                   append only ledger
├── STATUS.md                        live dashboard
└── TASKS.md                         live task board
```

### 3.2 Naming: keep `game/`, do not rename to `godot/`

The repo now holds two engine trees, and `game/` versus `unity/` is genuinely ambiguous: both are the
game. `godot/` versus `unity/` would read better. **Recommendation: do not do it.**

Measured cost of the rename:

| Site | Count |
|---|---:|
| `.gitignore` rules naming `game/` | 20 |
| `game/tools/batch_*.sh` hardcoded absolute `cd` | 17 |
| `snapshot.sh` lines 34, 41, 43 | 3 |
| `play.sh:10`, `tools/build_dmg.sh:7` | 2 |
| Documentation references across `PROJECT_LOG.md`, `STATUS.md`, `TASKS.md`, `game/docs/*`, `unity/*` | roughly 50 |
| **Total** | **roughly 90 sites** |

Nothing inside the Godot project is affected: every `res://` path and `export_presets.cfg` is project
internal, and the 19 Python tools anchor on `__file__`. So the rename touches roughly 90 references
and changes zero behaviour. It buys a clearer word.

The real problem is ambiguity, and ambiguity is cured by a README sentence, not by 90 edits. Do that
instead. Revisit only if Unity ever becomes the shipping engine and Godot is retired, at which point
the rename should ride along with that larger change.

### 3.3 Proposed structure

```
runway/
├── README.md                        NEW. engines + versions + run + honest art policy
├── .gitignore                       re-inclusion chain extended to cover all hand authored data
├── STATUS.md                        stays at root, live
├── TASKS.md                         stays at root, live
├── PROJECT_LOG.md                   stays at root, append only, content untouched
├── play.sh                          stays at root, the one command a player runs
│
├── docs/
│   ├── repo-plan.md                 this file
│   ├── design/
│   │   ├── DND_STARTUP_PLAN.md              from game/docs/
│   │   ├── BLANK_SCENES_ARCHITECTURE.md     from game/docs/
│   │   ├── GENERATIVE_ARCHITECTURE.md       from game/docs/
│   │   └── charter/                         from initial_info/, marked superseded
│   │       ├── 01_PRD.md  02_GAME_DESIGN_DOSSIER.md
│   │       └── 03_ASSET_MANIFEST.md  04_MASTER_TODO.md
│   ├── godot/
│   │   ├── LANE_BRIEF.md  BACKGROUND_INVARIANTS.md  screens.md  QA_REPORT.md
│   ├── unity/
│   │   ├── MIGRATION.md  CHECKLIST.md  COMPILE-RISKS.md
│   │   ├── HANDOFF.md  INTEGRATION-HOOKUPS.md
│   │   └── briefs/                          10 files
│   └── art-prompts/
│       ├── image_prompt_gen.md              from root
│       ├── openapi-layer-decomposition.yaml from root
│       ├── concept_01_title_screen.json  concept_02_founder_archetypes.json
│       ├── music_ost.json
│       └── music/                           11 per track prompt JSONs
│
├── game/                            Godot project, name unchanged
│   ├── project.godot  Main.tscn  export_presets.cfg
│   ├── icon.png  icon_1024.png  runway.icns  .env.example
│   ├── src/  data/  assets/
│   ├── tests/
│   │   └── fixtures/pilot_composed_hangar.png    from game/docs/refs/
│   └── tools/
│       ├── *.py                     26 pipeline tools, unchanged location
│       └── batches/                 17 batch_*.sh, historical records
│
├── unity/                           Unity project, name unchanged
│   ├── Assets/  ProjectSettings/  Packages/
│   ├── Runway.Core.Tests/  Runway.ATail.Tests/
│   └── (all .md moved to docs/unity/)
│
├── tools/
│   ├── build_dmg.sh  unity_compile.sh
│   └── dmg/                         dmg_bg.png + dmg_bg_raw.png, out of dist/
│
└── music-masters/                   ignored. renamed from music/ to end the ambiguity
                                     with game/assets/music (the deployed copy)
```

Removed entirely: `.snapshots/`, `snapshot.sh`, `initial_info/` (moved), `prompts/` (moved),
`game/docs/` (moved), `game/docs/refs/_superseded/`.

### 3.4 Every proposed move, with its exact reference fix list

Move M1 through M8. Each row names the files and lines that must change in the same commit.

---

**M1. `game/docs/*.md` to `docs/godot/` and `docs/design/`**

| Must change | Why |
|---|---|
| `snapshot.sh:40` | `for d in src data docs tools tests` copies `game/docs`. Moot if snapshot.sh is retired (M8). |
| `game/docs/BACKGROUND_INVARIANTS.md:15,52,108,133,197` | self references to `tools/...` meaning `game/tools/` |
| `game/docs/GENERATIVE_ARCHITECTURE.md:76` | reference to `tools/scene_pipeline.py` |
| `game/docs/BLANK_SCENES_ARCHITECTURE.md:64` | reference to `tools/find_cast.py` |
| `game/docs/DND_STARTUP_PLAN.md:95` | references `tools/balance_sim.py`, **which does not exist** (it is `game/tests/balance_sim.gd`). Fix while moving. |
| `game/docs/LANE_BRIEF.md:6` | hardcodes the absolute machine path `/Users/assem/Documents/Doc-Assem/Claude Code/runway/game`. Replace with a repo relative instruction. |
| `game/docs/LANE_BRIEF.md:51,53` | contain the full session scratchpad key paths. Same dead path as `scene_pipeline.py:30`. |
| `game/docs/screens.md:3,29,46` | `godot --path game` stays correct, no change needed |
| `STATUS.md:126`, `TASKS.md:71,174,176` | point at `tools/...` and `game/tools/scene_pipeline.py` |
| `unity/briefs/P0-parity-harness.md:4` | `read game/tests/new_screens_shot.gd` stays correct |

Side benefit: `game/docs/` currently sits **inside the Godot project**, so Godot imports it and
generated 27 `.import` stubs there. Moving the docs out stops that permanently.

---

**M2. `game/docs/refs/pilot_composed_hangar.png` to `game/tests/fixtures/`**

| Must change | Why |
|---|---|
| `game/src/main.gd:349` | `_begin_turn(dm, "" if live else "res://docs/refs/pilot_composed_hangar.png")` becomes `res://tests/fixtures/pilot_composed_hangar.png`. This is the **only** code reference into `game/docs/`. |

It is harness stub art, so `game/tests/fixtures/` is where it belongs. This keeps it tracked and keeps
behaviour identical. See stage 3 for the option of removing its 5.01 MB from history entirely.

---

**M3. `game/docs/refs/_superseded/` and the 27 orphan `.import` stubs: delete**

Nothing to fix. `_superseded/` contains 7 files, all of them `.import` stubs for images that no longer
exist. 20 more orphan stubs sit in `refs/` itself. All 27 are untracked (`*.import` is ignored), so
this is a pure local disk deletion. Three more orphan stubs live at
`game/assets/title/anim/type_press_{01,02,03}.png.import`.

---

**M4. `unity/*.md` and `unity/briefs/` to `docs/unity/`**

| Must change | Why |
|---|---|
| `unity/briefs/README.md:3,4,8,9,10` | lane contract self references, including `bash tools/unity_compile.sh` which stays correct from repo root |
| `unity/MIGRATION.md:18,36,42` | `dotnet run --project unity/Runway.Core.Tests` and `-projectPath unity` stay correct |
| `unity/CHECKLIST.md:56,164,170` | self references |
| `unity/HANDOFF.md:25` | `unity/build/mac/RUNWAY!.app` stays correct |
| `unity/COMPILE-RISKS.md:294,618` | self references |
| Eight `.cs` doc comments citing `-projectPath unity` | `Build.cs:15`, `Editor/{BoilShot,GlowShots,ParticleShots,ImpulseProbe,InkRevealFilm,MixProbe,BeatTextFxShots}.cs`. Unaffected by this move; listed so they are not disturbed. |

`unity/HANDOFF.md` must be `git add`ed as part of this move, since it is currently untracked.

Note on `COMPILE-RISKS.md`: 127 KB, 987 lines, 48 sections. It is append only in structure but a live
contract in use (`unity/briefs/README.md` requires every lane to read **and extend** it). Its opening
premise, "No Unity editor exists on this machine yet, so nothing here has been compiled", is now stale
because `tools/unity_compile.sh` runs a real headless editor. It is a good candidate for a later split
into live conventions versus per lane archive, but that is an editorial task, not a repo move, and is
out of scope here.

---

**M5. `initial_info/` to `docs/design/charter/`**

| Must change | Why |
|---|---|
| `PROJECT_LOG.md:11` | cites `initial_info/` as the project origin. **Do not edit.** `PROJECT_LOG.md` is append only. The new path is recorded in the README and in a new charter index instead. |
| `initial_info/*` file modes | all four are mode 600. Normalise to 644. |

These four documents are the founding charter and are materially superseded. Spot checks:

| Claim | Reality | Verdict |
|---|---|---|
| `01_PRD.md:19` Pillar 1: the signature loop alternates timed SCRAMBLE with turn based GRIND | The scramble was cut (`PROJECT_LOG.md:65`). `game/src/screens/scramble_screen.gd` and `scramble3d_screen.gd` are referenced by nothing in `game/`. | stale, and it surfaces 27 KB of dead source |
| `03_ASSET_MANIFEST.md:9` naming `arn_*`, `itm_*`, `card_*` | No such prefixes exist. Real art is directory organised. | stale |
| `03_ASSET_MANIFEST.md` premise: 640 hand drawn images for one artist | Replaced by the generative pipeline: 516 rooms, 21 characters times 25 poses | wholly superseded |
| `02_GAME_DESIGN_DOSSIER.md:12` era table | `game/src/core/sim_engine.gd:635` has exactly `garage, coworking, office, floor, hq` | accurate, the era spine survived |
| `04_MASTER_TODO.md` Phase 0 checkboxes | All decided long ago, every box still unticked | abandoned |

Move them under `charter/` with a one line index stating they are the 2026-08-17 founding documents,
superseded where they conflict with `PROJECT_LOG.md`.

---

**M6. `prompts/`, `image_prompt_gen.md`, `openapi-layer-decomposition.yaml` to `docs/art-prompts/`**

| Must change | Why |
|---|---|
| `TASKS.md:119` | already says `prompts/image_prompt_gen.md` while the file is at root. **This move fixes an existing stale reference rather than creating one.** Update to `docs/art-prompts/image_prompt_gen.md`. |
| `PROJECT_LOG.md:23` | locked decision D7 cites `image_prompt_gen.md` by bare filename. Append only, do not edit. The bare filename stays findable. |
| `game/docs/LANE_BRIEF.md:9` | forbids lanes touching `prompts/`. Update the path in the moved copy. |

This also resolves a real naming collision: root `prompts/` (a dead generation archive) versus
`game/data/prompts/` (**live**, loaded at runtime by `game/src/llm/event_generator.gd:20,21,274`).

`image_prompt_gen.md` is **not** an orphan. It is the Nano Banana JSON spec that locked decision D7
mandates for all image prompts.

---

**M7. `dist/dmg_bg.png` and `dmg_bg_raw.png` to `tools/dmg/`, and track them**

| Must change | Why |
|---|---|
| `tools/build_dmg.sh:12` | `BG="$DIST/dmg_bg.png"` becomes `BG="$ROOT/tools/dmg/dmg_bg.png"` |

These are build **inputs** living inside a gitignored build **output** directory, so a fresh clone
cannot rebuild the DMG background. This is a straight policy inversion. 1.6 MB, and `dmg_bg.png` at
392 KB is the only one the script actually reads.

---

**M8. Retire `snapshot.sh` and delete `.snapshots/`**

| Must change | Why |
|---|---|
| `STATUS.md:6-8` | mentions `snapshot.sh` alongside `play.sh` |
| `.gitignore:38,39` | `.snapshots/` and `restored-*/` rules become dead |

`snapshot.sh`'s own header states why it exists: "No git, by the owner's standing rule ... game/ has
never been under version control". That is no longer true; `STATUS.md:14` says "Git now covers this
too". The mechanism it substitutes for now exists, with 215 commits. The two snapshots on disk are
from 2026-08-19 and cost 25 MB.

This one is a judgement call for the owner, not a defect. Keeping a belt and braces snapshotter is a
defensible choice. If it stays, at minimum correct the header text.

---

**M9. `game/tools/batch_*.sh` to `game/tools/batches/`**

| Must change | Why |
|---|---|
| all 17 files, one line each | `cd "/Users/assem/Documents/Doc-Assem/Claude Code/runway/game"` becomes a self locating `cd "$(dirname "${BASH_SOURCE[0]}")/../.."`. They are **already broken** for any other checkout path, so this is a repair, not a regression. |
| file modes | 5 are 755, 12 are 644. Normalise. |

These are dated one shot generation records (A garage quartet, B select re-frames, ... Q the
generative library pilot), not reusable tools. Moving them one level down separates 17 historical
records from the 26 live Python tools.

**Do not merge `game/tools/` into root `tools/`.** They are different things: root `tools/` is build
and release, `game/tools/` is art generation anchored on `__file__` resolving to `game/`. Merging
would break 19 Python anchors and `make_ambient.py`, which hard requires cwd `game/`.

---

## 4. HYGIENE

| Finding | Detail | Action |
|---|---|---|
| `.DS_Store` | 11 on disk, **0 tracked**. The ignore rule works. | delete locally, no git change |
| `*.log` `*.tmp` `*.bak` `*.orig` `*.rej` `*~` `*.swp` | **zero repo wide** | none |
| Orphan `.import` stubs | 30, all untracked | delete (M3) |
| Tracked orphan | `game/tools/shoot_autopsy.gd.uid`, parent gone | `git rm` |
| Missing `.uid` | 3 in `game/tests/`, other 58 tracked | `git add` |
| Dead source | `game/src/screens/scramble_screen.gd` (7.7 KB) and `scramble3d_screen.gd` (19.2 KB) referenced by nothing | owner call: delete or annotate |
| File modes | 5 tracked files are mode 600 (`scramble3d_screen.gd`, all 4 `initial_info/*`); `tools/build_dmg.sh` is 644 while `unity_compile.sh` is 755; batch scripts split 5/12 | normalise |
| `game/build_stamp.txt` | untracked, **not ignored**, shows as `??` in every `git status`. Generated by `tools/build_dmg.sh:18`. | add to `.gitignore` |
| Empty dirs / zero byte files | `unity/Logs` (ignored), one MSBuild marker in `obj/` (ignored) | none |
| Scratch named files | zero genuine hits repo wide | none |
| `.gitignore:7` | comment says `tools/scene_pipeline.py`; the file is `game/tools/scene_pipeline.py` | fix comment |
| `.gitignore:61` | `unity/Builds/` does not match the real output `unity/build/mac` (line 67 covers it) | remove dead rule |
| `music/` duplication | 61 MB masters at root, byte identical 52 MB deployed copy at `game/assets/music/`. Both ignored, so zero git weight, but 113 MB of local disk and an ambiguous source of truth. | rename root to `music-masters/` and update `.gitignore:27` |
| `PROJECT_LOG.md` at root | Correct. Append only ledger, 40 KB, cited throughout. Its header says "Last updated: 2026-08-17" while mtime is 2026-08-22. | **leave content untouched**, as instructed |
| README accuracy | There is no README. `STATUS.md:6-8` is the closest thing and it is accurate as far as it goes. | write one (stage 1) |

---

## 5. HISTORY WEIGHT

### 5.1 What a rewrite would actually buy

Clone today: **25.91 MiB**.

| Candidate | Pack MB | Removable without touching HEAD? |
|---|---:|---|
| `game/docs/refs` dead revisions (19 blobs) | 11.30 | yes |
| `game/runway.icns` superseded revision | 1.77 | yes |
| `game/icon_1024.png` superseded revision | 0.75 | yes |
| `game/icon.png` superseded revision | 0.02 | yes |
| **Subtotal, history only** | **13.84** | **53% of the clone** |
| `game/docs/refs/pilot_composed_hangar.png` at HEAD | 5.01 | only if also dropped from HEAD |
| **Subtotal, including the HEAD copy** | **18.85** | **73% of the clone** |

| Scenario | Resulting clone | Reduction |
|---|---:|---:|
| Do nothing | 25.91 MiB | 0 |
| Drop dead history only | roughly 12.1 MiB | 53% |
| Also drop the 5 MB pilot PNG from HEAD | roughly 7.1 MiB | 73% |

### 5.2 Recommendation

**Do not rewrite history. Recommendation only, owner decision, and the recommendation is no.**

Reasons:

1. The absolute saving is 14 to 19 MB. A 26 MiB clone is not a problem anyone has.
2. `git gc --prune=now` reclaims **166 MB locally**, which is 10 times larger, at zero risk, with no
   force push and no history change. Take that win and stop.
3. This is a **public** repo. `git filter-repo` rewrites every commit SHA from the first one onward,
   requires `git push --force`, and permanently breaks every existing clone, fork, and any URL that
   pins a commit. `PROJECT_LOG.md` and `TASKS.md` cite commit SHAs (`game/docs/QA_REPORT.md` is pinned
   to commit `871e49d`), and those citations would all go dangling.
4. `git-filter-repo` is not installed on this machine (nor `bfg`), so the pass would also need a new
   dependency.

If the owner decides the clone size matters later, the correct sequencing is: do stages 1 and 2 first,
let them settle, then rewrite once, in a single pass, from a fresh mirror clone, with the whole team
notified. A stage 3 script is provided below for that case, marked destructive.

---

## 6. STAGED MIGRATION PLAN

### Stage 1: zero risk

No file moves. Nothing that changes a path any script or doc refers to. Everything here is either a
pure local reclaim, an addition, or an ignore rule fix.

| Step | What | Effect |
|---|---|---|
| 1.1 | `git gc --prune=now` | **local `.git` 192 MB to roughly 26 MB.** No history change, no remote change. |
| 1.2 | Delete 30 orphan `.import` stubs, `game/docs/refs/_superseded/`, 11 `.DS_Store`, `.snapshots/` | roughly 25 MB local, all untracked |
| 1.3 | Extend the `.gitignore` re-inclusion chain | closes the 578 file silent drop trap |
| 1.4 | `git add` the 4 untracked hand authored registries and 3 `.uid` files | protects irreplaceable data |
| 1.5 | `git add` the missing Unity tree: `ProjectSettings/`, all `.meta`, the 26 `.cs`, `Main.unity`, `StreamingAssets/`, `TextMesh Pro/`, `packages-lock.json`, `Runway.ATail.Tests/`, `HANDOFF.md` | **makes the Unity port buildable from a clone at all** |
| 1.6 | `git rm --cached` the tracked orphan `shoot_autopsy.gd.uid` | removes a dead file |
| 1.7 | Add `game/build_stamp.txt` to `.gitignore`; fix the wrong path in the `.gitignore:7` comment; drop the dead `unity/Builds/` rule | cleans `git status` |
| 1.8 | Write `README.md` | Godot 4.7, Unity 6000.0.82f1, `./play.sh`, `cp game/.env.example game/.env`, and an honest statement that the art is not in the repo and not regenerable from it |
| 1.9 | Normalise file modes | consistency |

Stage 1 adds roughly 1.4 MB to the clone (the Unity tree) and is worth every byte.

### Stage 2: moves

M1 through M9 from section 3.4. Each move lands with its reference fixes **in the same commit**, so
no commit is ever in a broken intermediate state. Recommended commit order: M3, M7, M2, M1, M4, M5,
M6, M9, M8.

Two prerequisites that are independently worth doing and should land before or with stage 2:

- **`game/tools/scene_pipeline.py:30`**: replace the hardcoded scratchpad `SCRATCH` with environment
  variables (`OPENAI_API_KEY`, `ATLAS_API_KEY`), keeping the scratchpad as a fallback. Fixes the
  pipeline for every future session, not just other machines. `gen_backgrounds.py:40` and
  `patch_factory.py:59` inherit the fix.
- **`game/docs/LANE_BRIEF.md:6`**: replace the absolute machine path with a repo relative instruction.

### Stage 3: history rewrite

Owner decision. Recommendation is **no**. See section 5.2. The script below is written for
completeness and is marked destructive.

---

## 7. COMMAND SCRIPTS, FOR REVIEW ONLY

**None of these have been run.** Read them, adjust, then run them yourself.
Run every stage from the repo root.

### Stage 1 script

```bash
#!/bin/bash
# RUNWAY! repo cleanup, stage 1. Zero risk: no moves, no history change, no force push.
set -e
cd "$(git rev-parse --show-toplevel)"

# --- 1.1 reclaim the local .git. 192 MB -> ~26 MB. Nothing leaves this machine. ---
git count-objects -vH                    # before
git gc --prune=now
git count-objects -vH                    # after

# --- 1.2 delete untracked dead weight (all of this is ignored or orphaned) ---
find game/docs/refs game/assets/title/anim -name '*.import' \
  ! -name 'pilot_composed_hangar.png.import' -delete
rm -rf game/docs/refs/_superseded
find . -name .DS_Store -not -path './.git/*' -delete
rm -rf .snapshots

# --- 1.3 close the 578 file silent drop trap ---
# Appended to .gitignore, immediately after the existing scenes block (line 26).
# Verified: these rules re-include EXACTLY the currently tracked set, no more.
cat >> .gitignore <<'IGNORE'

# Hand authored data inside the ignored art trees. Same argument as layout.json
# above: these are measured and curated by hand and cannot be regenerated. Without
# these rules a NEW pose, icon or patch scene is silently invisible to git add.
!game/assets/journal_icons/
!game/assets/poses/
game/assets/poses/*
!game/assets/poses/*.json
!game/assets/poses/*/
game/assets/poses/*/*
!game/assets/poses/*/*.json
!game/assets/patch_scenes/
game/assets/patch_scenes/*
!game/assets/patch_scenes/*.json
!game/assets/patch_scenes/*/
game/assets/patch_scenes/*/*
!game/assets/patch_scenes/*/*.json
!game/assets/scenes/*.json
IGNORE
# widen backgrounds from manifest.json alone to every json (4 files)
#   FIND:    !game/assets/backgrounds/manifest.json
#   REPLACE: !game/assets/backgrounds/*.json

# verify the rules changed nothing that was already tracked, and caught the 4 registries
git ls-files -i -c --exclude-standard | wc -l     # expect 0 (was 578)
git status --short game/assets | head -20         # expect the 4 registries as ??

# --- 1.4 protect the hand authored registries ---
git add game/assets/scenes/refs.json \
        game/assets/poses/_refs.json game/assets/poses/_report.json \
        game/assets/patch_scenes/_refs.json
git add game/tests/binder_shot.gd.uid \
        game/tests/watchdog_probe.gd.uid \
        game/tests/world_reveal_shot.gd.uid

# --- 1.5 make the Unity port buildable from a clone ---
# 283 untracked files, NONE of them ignored. They were simply never added.
git add unity/ProjectSettings/
git add unity/Packages/packages-lock.json
git add unity/Assets/Scenes/
git add unity/Assets/Scripts/
git add unity/Assets/Resources/
git add unity/Assets/StreamingAssets/
git add "unity/Assets/TextMesh Pro/"
git add unity/Assets/*.meta
git add unity/Runway.ATail.Tests/
git add unity/HANDOFF.md unity/briefs/LOCAL-NARRATOR-dossier.md
# confirm nothing is left behind
git ls-files --others --exclude-standard | grep '^unity/' || echo "unity fully staged"
# confirm the ignored art is still ignored
git status --short unity/Assets/Art unity/Assets/Resources/Sheets 2>/dev/null \
  || echo "art still local only, correct"

# --- 1.6 the tracked orphan ---
git rm --cached game/tools/shoot_autopsy.gd.uid
rm -f game/tools/shoot_autopsy.gd.uid

# --- 1.7 .gitignore corrections (apply by hand, exact strings) ---
#   FIND:    # tools/scene_pipeline.py. Excluded so that a lane's PR shows the code and data it
#   REPLACE: # game/tools/scene_pipeline.py. Excluded so that a lane's PR shows the code and data it
#
#   FIND:    unity/Builds/
#   REPLACE: (delete the line: the real output is unity/build/, covered below)
#
#   ADD under "# build output":
#     game/build_stamp.txt

# --- 1.9 normalise modes ---
chmod 644 game/src/screens/scramble3d_screen.gd initial_info/*.md
chmod 755 tools/build_dmg.sh game/tools/batch_*.sh
git update-index --chmod=+x tools/build_dmg.sh

# --- verify ---
git status --short --untracked-files=no
git ls-files -i -c --exclude-standard | wc -l    # expect 0
```

Then write `README.md` (step 1.8) and commit.

### Stage 2 script

```bash
#!/bin/bash
# RUNWAY! repo cleanup, stage 2. Moves. Each block is ONE commit: move + reference fixes together.
set -e
cd "$(git rev-parse --show-toplevel)"
mkdir -p docs/design/charter docs/godot docs/unity docs/art-prompts tools/dmg

# ---- M7: build inputs out of the ignored build output dir ----
git mv --force dist/dmg_bg.png dist/dmg_bg_raw.png tools/dmg/ 2>/dev/null \
  || { mv dist/dmg_bg.png dist/dmg_bg_raw.png tools/dmg/ && git add tools/dmg/; }
#   tools/build_dmg.sh:12
#   FIND:    BG="$DIST/dmg_bg.png"
#   REPLACE: BG="$ROOT/tools/dmg/dmg_bg.png"
git commit -m "DMG background sources move out of the ignored dist/ tree"

# ---- M2: the harness stub PNG becomes a test fixture ----
mkdir -p game/tests/fixtures
git mv game/docs/refs/pilot_composed_hangar.png game/tests/fixtures/
rm -f game/docs/refs/pilot_composed_hangar.png.import
#   game/src/main.gd:349  (the ONLY code reference into game/docs/)
#   FIND:    _begin_turn(dm, "" if live else "res://docs/refs/pilot_composed_hangar.png")
#   REPLACE: _begin_turn(dm, "" if live else "res://tests/fixtures/pilot_composed_hangar.png")
godot --headless --path game --import >/dev/null 2>&1 || true
godot --headless --path game --script tests/smoke.gd     # must print SMOKE PASS
git commit -am "The turn harness stub becomes a test fixture, not a doc"

# ---- M1: Godot docs leave the Godot project ----
git mv game/docs/LANE_BRIEF.md game/docs/BACKGROUND_INVARIANTS.md \
       game/docs/screens.md game/docs/QA_REPORT.md docs/godot/
git mv game/docs/DND_STARTUP_PLAN.md game/docs/BLANK_SCENES_ARCHITECTURE.md \
       game/docs/GENERATIVE_ARCHITECTURE.md docs/design/
rmdir game/docs/refs game/docs 2>/dev/null || true
#   docs/godot/LANE_BRIEF.md:6
#   FIND:    - Godot project: `/Users/assem/Documents/Doc-Assem/Claude Code/runway/game`. Run
#   REPLACE: - Godot project: `game/` at the repo root. Run
#   docs/design/DND_STARTUP_PLAN.md:95
#   FIND:    `tools/balance_sim.py`
#   REPLACE: `game/tests/balance_sim.gd`
#   Then sweep tools/ -> game/tools/ in:
#     docs/godot/BACKGROUND_INVARIANTS.md:15,52,108,133,197
#     docs/design/GENERATIVE_ARCHITECTURE.md:76
#     docs/design/BLANK_SCENES_ARCHITECTURE.md:64
#     STATUS.md:126   TASKS.md:71,174,176
grep -rn "game/docs" --include=*.md --include=*.gd --include=*.sh . || echo "no stale game/docs refs"
git commit -am "Docs leave the Godot project so the engine stops importing them"

# ---- M4: Unity docs ----
git mv unity/MIGRATION.md unity/CHECKLIST.md unity/COMPILE-RISKS.md \
       unity/HANDOFF.md unity/INTEGRATION-HOOKUPS.md docs/unity/
git mv unity/briefs docs/unity/briefs
grep -rn "unity/CHECKLIST\|unity/COMPILE-RISKS\|unity/briefs\|unity/HANDOFF\|unity/MIGRATION" \
  --include=*.md --include=*.cs . || echo "no stale unity doc refs"
git commit -am "Unity lane docs join the docs tree"

# ---- M5 + M6: charter and art prompts ----
git mv initial_info/*.md docs/design/charter/ && rmdir initial_info
git mv image_prompt_gen.md openapi-layer-decomposition.yaml docs/art-prompts/
git mv prompts/*.json docs/art-prompts/ && git mv prompts/music docs/art-prompts/music
rm -f prompts/.DS_Store && rmdir prompts 2>/dev/null || true
#   TASKS.md:119  (already cites a path that does not exist: this FIXES it)
#   FIND:    prompts/image_prompt_gen.md schema
#   REPLACE: docs/art-prompts/image_prompt_gen.md schema
#   docs/godot/LANE_BRIEF.md:9
#   FIND:    `prompts/`, `music/`
#   REPLACE: `docs/art-prompts/`, `music-masters/`
#   PROJECT_LOG.md:11,23 cite initial_info/ and image_prompt_gen.md. DO NOT EDIT: append only.
git commit -am "The founding charter and the art prompt archive get a docs home"

# ---- M9: batch records separate from live tools, and stop hardcoding one machine ----
mkdir -p game/tools/batches && git mv game/tools/batch_*.sh game/tools/batches/
#   In each of the 17 files, ONE line:
#   FIND:    cd "/Users/assem/Documents/Doc-Assem/Claude Code/runway/game" || exit 1
#   REPLACE: cd "$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)" || exit 1
git commit -am "The 17 batch records stop hardcoding one machine's path"

# ---- M8: retire the pre git safety net (owner call) ----
git rm snapshot.sh
#   STATUS.md:6-8  drop the snapshot.sh sentence
#   .gitignore     drop .snapshots/ and restored-*/
git commit -am "Git replaces the pre git snapshotter it was built to substitute for"

# ---- music masters rename (both sides ignored, zero git impact) ----
mv music music-masters
#   .gitignore:27
#   FIND:    music/
#   REPLACE: music-masters/
git commit -am "Master audio is named apart from the deployed copy"

# ---- full verification ----
godot --headless --path game --import >/dev/null 2>&1
godot --headless --path game --script tests/smoke.gd      # SMOKE PASS
bash tools/unity_compile.sh                                # must stay clean
./play.sh --log                                            # then: grep -cE "SCRIPT ERROR" /tmp/runway_play.log
```

### Stage 3 script: DESTRUCTIVE, owner decision only

**Do not run this without deciding to accept every consequence below.**

- Rewrites all 215 commit SHAs. Every SHA cited in `PROJECT_LOG.md`, `TASKS.md` and
  `docs/godot/QA_REPORT.md` (pinned to `871e49d`) goes dangling.
- Requires `git push --force` to a **public** repo. Every existing clone and fork breaks.
- Requires installing `git-filter-repo` (not present on this machine).
- Buys 14 to 19 MB. The recommendation in section 5.2 is not to do this.

```bash
#!/bin/bash
# RUNWAY! stage 3. DESTRUCTIVE HISTORY REWRITE. Read section 5.2 first.
set -e

# Work on a fresh mirror, never on the live working copy.
brew install git-filter-repo
cd /tmp && rm -rf runway-rewrite
git clone --mirror https://github.com/achammah/runway.git runway-rewrite
cd runway-rewrite
git count-objects -vH | grep size-pack          # baseline: ~25.91 MiB

# Option A: drop DEAD history only. Keeps the pilot PNG at HEAD. Expect ~12.1 MiB.
git filter-repo --invert-paths \
  --path-glob 'game/docs/refs/_superseded/*' \
  --path-glob 'game/docs/refs/60s_*'

# Option B (instead of A): also drop the 5.01 MB pilot PNG from every commit.
# Only valid if stage 2 M2 already re-pointed game/src/main.gd:349, or you accept
# the harness losing its offline stub. Expect ~7.1 MiB.
# git filter-repo --invert-paths --path game/docs/refs

git count-objects -vH | grep size-pack          # confirm the win before pushing

# POINT OF NO RETURN. Everything below is irreversible for every clone and fork.
# git push --force --mirror https://github.com/achammah/runway.git
```

---

## 8. Top five recommendations, ranked

1. **`git gc --prune=now`.** Reclaims 166 MB locally, 91.54 MB of it unreachable art blobs from a
   staging that was reset. Zero risk, no history change, nothing leaves the machine. Ten times larger
   than anything a history rewrite would buy.
2. **Commit the Unity project.** 283 untracked files, none of them ignored: 21 `ProjectSettings`, 26
   `.cs`, 175 `.meta`, the only scene. Today `git clone` yields a Unity project that cannot be opened,
   let alone compiled. This is the biggest gap between what the repo claims to be and what it is.
3. **Close the 578 file silent drop trap.** Extend the re-inclusion chain to `poses/`,
   `patch_scenes/`, `journal_icons/` and `backgrounds/*.json`. The next hand authored pose set is
   currently invisible to `git add` with no warning. This is a live data loss risk.
4. **Write a README.** Godot 4.7, Unity 6000.0.82f1, `./play.sh`, `cp game/.env.example game/.env`,
   and an honest line that the art is not in the repo and cannot be regenerated from it. The engine
   versions exist nowhere in git except one untracked file.
5. **Do not rewrite history, and do not rename `game/` to `godot/`.** The rewrite buys 14 to 19 MB at
   the cost of force pushing a public repo and dangling every cited SHA. The rename touches roughly 90
   references and changes no behaviour. Both are motion mistaken for progress; a README sentence cures
   the ambiguity the rename was for.

### Runners up, worth doing in the same pass

- Fix `game/tools/scene_pipeline.py:30`. API keys read from a hardcoded session scratchpad path make
  the committed art pipeline unrunnable for anyone, including this machine in a later session.
- Guard `game/src/core/music_manager.gd:31,47` with `ResourceLoader.exists`. Seven boot errors on a
  fresh clone, and it restores the `SCRIPT ERROR == 0` gate that `LANE_BRIEF.md` depends on.
- Remove the absolute machine path from `docs/godot/LANE_BRIEF.md:6`, which every lane reads.
