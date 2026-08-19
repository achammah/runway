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

## 3. TASKS

### Content and systems
- [ ] C-01 Week page opens with the consequence chain (BUG-01)
- [ ] C-02 Quiet weeks get an honest line, never a bare status page
- [ ] C-03 Enforce max 4 cofounders **and** no duplicate types — done in the picker, verify in a run
- [x] C-04 Cast locked: 4 archetypes (dropout cut), 5 cofounder types incl. THE IDEA FRIEND as a pure trap
- [x] C-05 Structure ladder extended to four cofounders (0/30/45/55/62% equity)

### The journal
- [x] J-01 `JournalPage` shell owns geometry, two type sizes, four zones, and `ask()`
- [x] J-02 Text sits on the printed rules (17 rules, 44.9px pitch, two independent measurements agreed)
- [x] J-03 Pivot split into three sheets
- [ ] J-04 Migrate THE PEOPLE and THE WORK fully onto the shell
- [ ] J-05 Zones cascade so an overrun never lands on the next zone (BUG-03)
- [ ] J-06 Room behind the page uses `SceneRoomPicker` so it changes with the era

### Scenes and art
- [x] A-01 8 empty-stage loops
- [x] A-02 27 cast sprites with contact shadows
- [x] A-03 25 crew marks with per-mark scale + 20 occluders
- [x] A-04 `write_surfaces` annotated and cleared on 5 stages
- [x] A-05 Composite blank surface sprites so each room has 4–5 usable faces (BUG-11)
- [x] A-06 Garage occluders done, and all 20 across 5 rooms — a matched cutout was the wrong problem: the occluder is drawn over a loop that already contains the furniture, and the furniture does not move, so a crop of the scene at the authored rect aligns perfectly by construction
- [x] A-07 Inventory board on every scene, largest face, 3+ lines (portrait corkboard, 4 lines; hq/nasdaq/yc reuse the face already annotated there)

### Engine and tooling
- [x] T-01 Verified downloads + `verify` gate
- [x] T-02 `clear_surfaces` wipes declared faces across base image and all 48 anim frames
- [x] T-03 `SceneSurfaces` writes state onto drawn objects
- [x] T-04 `play.sh`, `snapshot.sh`
- [ ] T-05 `SceneRoom` per-layer animation, so a layer can loop while the room base stays still

---

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
