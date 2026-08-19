# RUNWAY! — status, open issues, and the task list

Single source of truth. Updated by MAIN. Every lane reads this before starting a round
and adds a line when it closes something.

**How to play it yourself:** `./play.sh` (add `--fresh` to wipe the saved run and the
gallery, `--log` to tee output to `/tmp/runway_play.log`). Nothing in this repo will ever
kill your window.

**Before any edit that deletes a span of code:** `./snapshot.sh`. This exists because a
lane's splice removed `_lock_week`, `_apply_lock` and `_free_move`, and with no version
control one block of game logic had to be reconstructed from the numbers printed on UI
labels. That must never be the recovery path again. Git now covers this too.

---

## 1. Where the build actually is

| | |
|---|---|
| Compiles | yes, 0 script errors on parse |
| Smoke | SMOKE PASS |
| Authored events | 65 |
| Full run | reaches all five eras: garage → coworking → office → floor → hq |
| Runtime | **37 errors per full run** (see BUG-04) and **an infinite loop** (BUG-05) |
| Scene art | 3,574 PNGs, 0 damaged (`tools/scene_pipeline.py verify`) |
| Loops | 8 empty-stage loops, 48 frames each |
| Cast | 27 sprites: 4 founders × 5 cofounder types × fine / burnt / gone |
| Crew geometry | 25 crew marks + 20 occluders across 5 eras |
| Music | 5 loops + 4 stings + 2 stems, wired |

---

## 2. OPEN ISSUES — playtest, 2026-08-19

Severity: **P0** blocks play · **P1** breaks the core experience · **P2** visible defect · **P3** polish.

| ID | Sev | Issue | Owner | State |
|---|---|---|---|---|
| BUG-01 | P1 | **No consequences of week N-1 on the week N page.** Player picks a move, week turns, page shows numbers with no story connecting them. Owner: "we don't understand any of the choices, it is too weird". The adjudicator already returns `interpreted_as`, `narration`, `reality_check`, `verdict`, `effects` and the page throws them away. | LANE-JOURNAL | **done** (b05b951) — page opens with said / heard / verdict / narration / reality-check / effect chips, state moved to a second sheet. Listed choices have no narration in the content schema: needs an `outcome` field or routing through the adjudicator — decision needed. |
| BUG-02 | P0 | **Cannot type in the free field at all.** Field is invisible by design (the ruling IS the field) so there was nothing to click and it never took focus. | MAIN | **fixed**, needs playtest confirm |
| BUG-03 | P2 | **Faint write prompt renders behind body text.** Long narration overruns the BODY zone into ENDING, where the prompt was already placed. Zones do not cascade. | MAIN | journal side **done** (b05b951): `_say()` picks the zone by remaining room and the write field reserves its space before any icon row, so nothing this lane draws lands on the prompt. Shell-side cascade still open. |
| BUG-04 | P2 | **37 runtime errors per run**: `Invalid access to key 'decay_pizza'`. Read at `garage_view_screen.gd:675`, not always registered by `_spot()` at :216. Needs a `has()` guard. | LANE-JOURNAL | **done** (b05b951) — `_show_spot()` guards with `has()`. Root cause was the era swap flipping `_scene_mode` after registration. Full run logs zero. |
| BUG-05 | P1 | **Infinite loop.** Two distinct causes, both found. (a) A looping tween whose FIRST step had zero duration makes Godot spin (`Infinite loop detected`, tween.cpp) — `breathe()` staggered by `0.35*i` and i is 0 for the first layer. (b) The run could never END: the IPO was gated on the Act-1 victory signal which stops firing after Act One, so at HQ nothing finished the run. Owner played to week 69 with $4M. | MAIN | **fixed** (254a691) — IPO fires when HQ is public-ready, plus a hard 78-week cap; two more looping tweens guarded. |
| BUG-06 | P2 | **Inventory display far too small.** Packed items render as ~30px chips. Owner: "way too small for inventory, needs to be clearer and bigger". | LANE-FLOW | open |
| BUG-07 | P1 | **`_apply_lock` gesture branch was RECONSTRUCTED, not recovered** — rebuilt from values printed on UI labels after a splice deleted it. Bonus (−$500 +15) and shares (−2% equity +25) are inferred. Needs a human read. | MAIN | needs review |
| BUG-08 | P2 | Choice captions wrap to 3 lines and print through the line below; `icon_row` used a fixed caption strip. | MAIN | **fixed**, needs verify |
| BUG-09 | P2 | Body text advanced two ruled lines instead of one, opening a blank line between wrapped lines. | MAIN | **fixed** |
| BUG-10 | P3 | Room still uses floating plates for cash/equity/company name instead of `SceneSurfaces`. | LANE-JOURNAL | next |
| BUG-11 | P2 | **Stages have only ONE usable writing surface each**, not five. Other paper was drawn at decoration scale (coworking kanban cards are 23×27px; two lines of 26px type needs ~60px). | LANE-SCENES | **fixed** — blank surfaces composited into the still + all 48 frames: garage 5, office 5, coworking 4, floor 4. hq is 2: it is a glass box with almost no solid wall |
| BUG-12 | P2 | `clear_surfaces` inset of 8% strands a ring of un-wiped scribble; 3% is correct for clearing while 8% remains right for writing. Contract needs both numbers. | MAIN | open |
| BUG-13 | P3 | Roster dock is the last dark slab on the select screen; should be paper or sit on a surface in the stage. | LANE-FLOW | open |
| BUG-14 | P3 | Warning glyphs survive on PART-TIME / NO VESTING chips. Judgement call: they label a state rather than predict an outcome. **Owner decision needed.** | LANE-FLOW | awaiting owner |
| BUG-15 | P3 | Gallery shows 39 runs / ×37 FOUNDER FLATLINE — autopilot runs polluting the player profile. `./play.sh --fresh` clears it; the harness should write to a separate profile. | MAIN | open |

### Closed this session

| ID | Issue | Fix |
|---|---|---|
| ✔ | Game crashed after the draft: `GarageViewScreen.new()` failed | Two undeclared constants; the other two errors were cascades |
| ✔ | PACK YOUR BAG dead end, could not unpack or advance | `_toggle_bag` wrote only the `normal` stylebox; Godot draws `hover` under the cursor, so the unpack was invisible |
| ✔ | TRAPS ARMED spoiled the lesson | Panel deleted; traps still arm and fire |
| ✔ | Founder select overlaps | Procedural sparkles and a second spotlight were fighting the drawn art; card clipped by its own shadow |
| ✔ | Pivot: icons over words, captions clipped, text off the paper | Split into three sheets; density, not positioning |
| ✔ | Two truncated scene PNGs | `urlretrieve` writes partial files and returns success; all downloads now verified + `verify` gate |
| ✔ | 7 of 8 reference images were cropped browser screenshots | Replaced with 11 official 1920×1080 press shots |
| ✔ | Chroma-key ate every contact shadow | Key by magenta dominance `min(R-G, B-G)`, not Euclidean distance |
| ✔ | Game ended itself at the first era promotion | A victory below hq is a chapter break, not an ending |

---

## 3. TASKS — the DAG now running

**Critical path is L1 (516 backgrounds), ~17 min at measured throughput** (31 img/min,
12-way, no degradation). Everything else is code and fits inside that window.

### WAVE 1 — running now, all parallel, one file each

| Lane | Owns (exclusive) | Tasks |
|---|---|---|
| **L1 BATCH** | `assets/backgrounds/**`, `tools/gen_backgrounds.py` | Generate all 516 EMPTY rooms from the manifest at 12-way. Verified downloads (`_fetch`, never `urlretrieve`). Resumable. `verify` must report 0 damaged. Read 6 samples and confirm nobody is in them. |
| **L2 ROOM** | `src/screens/garage_view_screen.gd` | **Blocker: the room has no people.** Build the crew array from run state and call `SceneRoom.populate()`. Then BUG-10: cash/equity/company onto `SceneSurfaces` instead of floating plates. Then era rooms via `SceneRoomPicker`. |
| **L4 LOOP** | `src/main.gd` | Wire the generative week: lock → DM → `make_scene()` starts immediately → reading beat shows the consequence text → scene opens. Every failure path keeps the previous room. Hard ceiling on the wait. Also BUG-15: harnesses must stop polluting the player profile. |
| **L5 DRAFT** | `src/screens/founder_draft_screen.gd` | The last two plates (item tiles, archetype chips). Audit every remaining `StyleBoxFlat`/`Panel`/`ColorRect`. Confirm no `LineEdit` survives. Re-verify the select-screen overlaps and BUG-06. |
| **L6 LASTPAGE** | `src/screens/autopsy_screen.gd` | Migrate THE LAST PAGE onto the shared `JournalPage` shell so it inherits rule-snapping, the corrected 858px paper quad and the one-rule line advance. Keep its chain-compression, which is better than the shell's. |
| **MAIN** | `src/journal/journal_page.gd`, `src/ui/*`, `STATUS.md`, prompts | DM prompt (done), library injection (done), journal zone overrun, commits, tracker. |

### WAVE 2 — needs L1's output
| Lane | Depends | Tasks |
|---|---|---|
| **L7 ANNOTATE** | L1 | Surface detector over all 516 (90% recall on faces above the size floor), then `clear_surfaces`, then `auto_marks.py` for crew marks. **No occluders**: 12% recall, and an over-wide proposal silently deleted 4 of 15 crew marks in the functional test. |
| **L8 INDEX** | L1 | Build the runtime index `SceneDirector.resolve()` reads; prove every facet combination either resolves or correctly reports a miss. |

### WAVE 3
| **L9 QA** | L1-L8 | Full playthrough, read every capture. Confirm: people in the room, text input works, a scene appears each week, the run can end. Blockers only. |

### Deliberately NOT doing
- **Occluders in this batch** — measured 12% recall; a false positive removes a founder from the room and nothing flags it. Occlusion is a nicety; a missing character is a bug.
- **Per-week video** — 106s+ per loop, impossible per turn. Ambient loops stay pre-built.

## 4. Standing constraints

- **Never** `pkill` — the owner runs the game window.
- **Never** touch `game/.env`. It is gitignored and must stay that way.
- `PROJECT_LOG.md` is append-only.
- The API keys in use are test keys pasted in chat: **rotate before anything is public.**
- One lane per file. Two lanes must never hold the same script.
- Read your own screenshots. A fix is done only when you have SEEN it fixed.

## 5. The bar

"AAA / award-winning indie game screen, not a SaaS form." Small text is a defect.
"Assembled, not organic" is a defect. Every element must look intended: sized to the art,
integrated into the illustration, animated where a real game would animate it.

The one page the owner has passed is **THE LAST PAGE**. Its geometry is now the shared
standard: one sheet rotated to the paper's drawn lean, laid out in that sheet's own
axis-aligned space, baselines snapped to the printed rules.

### Measured zone budgets (LANE-JOURNAL, 2026-08-19) — affects every lane using JournalPage

Probed on a real built page rather than estimated:

| zone | usable height |
|---|---|
| title | 21px left after the heading |
| body | **213px** |
| ending | **256px** |
| controls | 85px |

Costs, measured the same way: a `line()` costs **65px**; an `icon_row()` costs **cell.y + 68**
(46px caption inside the cell, plus the 22px gap); `write_field()` costs **153px**.

So `line + icon_row(90) + write_field` = 65 + 158 + 153 = **376px into a 256px zone**. A question,
a row of choices and the written move CANNOT share ENDING — that is the source of most of the
overrun warnings across the lanes, not per-page copy length. This lane now puts the question and its
icon row in BODY and leaves ENDING to the written move alone, which took its overruns from 21 to 3.
If the intended anatomy is question + choices + written move on one sheet, ENDING needs to be about
twice its current height, or `write_field` needs its own zone.
