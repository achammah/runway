# RUNWAY! QA report

Run by LANE-QA against `871e49d`. Godot 4.7.1.stable, macOS arm64.
Read only: no game code, data, prompts or assets were touched.

All three harnesses were run and **every one of the 45 PNGs was opened and read**, not sampled.

Concurrency note: another lane was writing while this ran. `HEAD` never moved off `871e49d`, so every capture below is against that commit, but the working tree drifted underneath: `src/llm/scene_director.gd` and eight `assets/scenes/*/layout.json` files gained object placement blocks (`money`, `product`, `trophy`, `plant`, each with `placed` and `err`). Those edits do **not** touch the `surfaces` line counts, so nothing in this report is invalidated by them, but a re-run of the harnesses will not be byte identical to this one.

| harness | exit | PNGs | SCRIPT ERROR | terminating line |
|---|---|---|---|---|
| `RUNWAY_SHOT` | 0 | 23 | **0** | `AUTOPILOT DONE` |
| `RUNWAY_FULLRUN` | 0 | 17 | **0** | `FULLRUN DONE: weeks=40 era=hq cash=2888943 dead=false shots=17 eras=garage, coworking, office, floor, hq` |
| `RUNWAY_LANEWIRE` | 0 | 5 | **0** | `LANEWIRE SHOTS DONE` |

`PASS` and zero `SCRIPT ERROR` appear in the same output for all three. That gate is genuinely green.

## Answers to the seven questions

**Does the game boot and play?** Yes. Zero `SCRIPT ERROR` in all three logs, all three harnesses self exited 0, and each printed its own done line in the same stream that carried no errors. Verified.

**Is anyone in the room?** **No. Nobody, in any room, in any capture.** This is the single worst finding and it is the owner's standing complaint. See blocker 1.

**Can the player write?** The field is present and visible on every decision page that was photographed, quiet week included, and it survives a page rebuild. But on the decision page specifically, pressing Enter produces no visible response and never enables the lock. See blocker 2.

**Does text stay on the paper?** Mostly, but not always. Two hard failures: `The Move` prints `(+28 off the page)` past the book edge and onto the garage shelf behind it, and the hq wall board clips `+1 more`. No text on text was found in any of the 45 captures, and no clipped word other than `+1 more`. The `nothing under 24px` rule does **fail**, but on the room surfaces rather than on paper: labels floor at 16px and values at 22px. See blockers 4 and 9.

**Does the run end?** Yes. 40 weeks, no loop, no stall, a real ending. Final line: `FULLRUN DONE: weeks=40 era=hq cash=2888943 dead=false shots=17 eras=garage, coworking, office, floor, hq`, and `final_alive_wk40.png` shows `WEEK 40 . THE BELL / YOU RANG THE BELL. / $3,707,382`.

**Do the eras change the room?** **Yes, confirmed on pixels.** Five visually distinct rooms: garage (pegboard, tools, roller door), coworking (long shared tables, phone booth, brick pillar), office (two private desks, server rack, street door), floor (two desk banks, foosball, poster wall), hq (boardroom, city glass, stage, sofa). `docs/screens.md` still carries a `Known gap` section saying the room never changes. That section is stale and should be deleted.

**Any placeholder or debug text on a player facing screen?** None found. Source grep for `TODO`, `FIXME`, `placeholder`, `lorem`, `DEBUG:`, `TBD` returns only code identifiers (`placeholder_text`, `class BlobPlaceholder`), never rendered strings.

## Log census

| pattern | shot | full | wire |
|---|---|---|---|
| `SCRIPT ERROR` | 0 | 0 | 0 |
| `Infinite loop` | 0 | 0 | 0 |
| `Invalid access` | 0 | 0 | 0 |
| `Nonexistent function` | 0 | 0 | 0 |
| `zone overran` (push_warning) | 8 | **110** | 0 |
| `non-equal opposite anchors` | 8 | 13 | 1 |
| `Condition "!is_inside_tree()"` | 1 | 0 | 0 |
| leaked ObjectDB / resource at exit | 0 | 0 | 2 / 1 |

Top offender by a wide margin, from `full.log`:

| count | call site | magnitude |
|---|---|---|
| 108 | `garage_view_screen.gd:1420` into `journal_page.gd:284` (`icon_row` on the decision page) | 511, 558, 562, 605, 609 against a 443 zone |
| 2 | `journal_page.gd:212` / `:425` (`line`) | 823 against 801 |

108 of the 110 overruns are the decision page, on essentially every week of a 40 week run. The `871e49d` commit message claims `zones cascade so text stops landing on text`. The cascade does save the render, and I found no visible text on text in any capture, but the zone accounting is still wrong on every single week.

## Screen verdicts

### `RUNWAY_SHOT` (23)

| screen | verdict | evidence |
|---|---|---|
| `01_title` | PASS | Title, subtitle, `PRESS ANY KEY` all clear; the blob runs the burning runway; nothing crowds the edges. |
| `01b_gallery` | PASS (nit) | `4 of 12 endings collected . 50 runs . best payout $417,648` reads correctly, 4 cards revealed and 8 masked. Nit: the subtitle baseline sits on the corkboard top edge so the comma in `417,648` is nicked. |
| `02_select` | PASS | `THE HACKER` card, five stat rows, `$8,000`, perk line, four chips with the first ringed in coral. All legible. |
| `03_select_consultant` | **DEFECT** | The card still says `THE HACKER` and the coral ring is still on chip 1. The consultant is never selected. Blocker 11. |
| `04_name` | PASS (nit) | Both writable fields present and filled (`Blobium`, `Enterprise API for meal prep`). Nit: the stage is so dark the founder at left is barely readable. |
| `04b_shape` | PASS | Six cards, two ticked, every body line inside its card, no wrap collisions. |
| `05_crew` | PASS | Cap table sums (65 + 30 + 5 = 100), the trap cofounder is flagged in red on three axes (`PART-TIME`, `NO VESTING`, `resentful...`). |
| `06_recruit` | DEFECT (minor) | Five candidate cards. The recruited two are struck with a full opacity coral X drawn **over** the body copy, so `Covers SELL ... revenue` and `Covers GRIT ... will` are hard to read. The intent (crossed off, `ON BOARD`) is clear, the execution costs legibility. |
| `07_money` | PASS | Three funding cards, dilution in coral, preview line `You'd keep 57% of Blobium . ~$58,000 in the bank on day one`. |
| `08_bag` | PASS | 15 items, three ringed, `IN THE BAG . 3/4` matches, `2 slots` tags on the bulky items, detail panel correct. |
| `09_journal` | DEFECT | Reads `WEEK 2` on a brand new run and opens with `You made no move last week. The week passed anyway, and it still cost you.` Blocker 6. |
| `09b_consequences_real` | PASS (nit) | Verdict, narration and the faint reality line all render inside the paper, no clipping. Nit: two blank rules between the title and the first line leave a visible hole. |
| `10_garage` | **DEFECT** | Furniture, whiteboard, wall chart, money tag, sticky, door. Zero people. Blocker 1. |
| `10b_room_item_note` | PASS (room aside) | Item note panel renders cleanly with three lines inside its border. Room still empty. |
| `10c_room_in_the_red` | PASS (room aside) | `-$300` in red, the whole frame washes pink, the journal button tints with it. The state reads. Room still empty. |
| `11_people` | **DEFECT** | Portraits render at roughly 30px and the gift icons at roughly 10px. `a bonus`, `a slice`, `new gear` are captions under specks. Blocker 3. |
| `12_work` | **DEFECT** | Same. `sprint`, `polish`, `build log` at roughly 30px; `outreach`, `demos`, `invoices` are 6px to 10px dots. Blocker 3. |
| `12b_situation` | PASS | The quiet week situation page: `Nothing came for you this week. That is not the same as safe.` plus the write prompt, the pen nib, the caret and the guide rules. The field is unmistakably there. |
| `12c_situation_event` | PASS | Three line event body wraps inside the paper, no clipping, write field below it. |
| `13_decision` | **DEFECT** | The write field is present and correct. The two choices are bare centred sentences with no icon, no border and no visible pen ring; `Stay solo. Stay / whole.` breaks mid phrase. Blockers 7 and 8. |
| `13b_decision_ready` | DEFECT (minor) | Only difference from `13_decision` is the button flipping from `...decide first` to `lock the week`. The pending choice is not circled, so `ready to lock` is communicated by four words of grey to coral and nothing else. |
| `13c_pivot` | PASS | Sheet 1 of 3: question plus three properly sized icons. `Market` is the real constant, not a clip. `docs/screens.md` calls this screen `axes, costs, fun fact`; it is one question per sheet and the costs land on sheet 3. Doc is stale, screen is fine. |
| `14_autopsy` | DEFECT (minor) | Cause, estate and a two beat causal chain all render at readable size. But the chain reads `wk 0 founded Blobium` and `wk 2 The Roommate Talk`, so week 1 is missing from the player's own history. Blocker 6. |

### `RUNWAY_FULLRUN` (17)

| screen | verdict | evidence |
|---|---|---|
| `era_garage_wk03` | PASS (room aside) | Garage art, `$61,700`, `v0.18`, `62%`. |
| `era_coworking_wk10` | DEFECT (timing) | Mean frame luminance 61.6 against 184 to 195 for every other era shot. The whole frame including the HUD is under a heavy dark overlay. Blocker 12. |
| `era_office_wk23` | PASS (nit) | Office art, distinct from garage and coworking. Nit: the wall reads `PRODUCT v0.100`. |
| `era_floor_wk35` | PASS (room aside) | Open floor, twelve desks, foosball, poster wall, `328` users, `$230,043`. Twelve empty chairs. |
| `era_hq_wk39` | **DEFECT** | hq art is excellent and the company name plus week are written onto the glass. But the wall board clips `+1 more` against its frame. Blocker 9. |
| `move_coworking_wk09` | DEFECT (minor) | `DESK 47, WORKNEST`, cost row, 4 desks, perk line: all readable. The entire right hand page is blank and the title and desk row straddle the book gutter. HUD says `WEEK 8`, filename says `wk09`. |
| `move_office_wk23` | DEFECT (minor) | Same shape. `9 desks` row crosses the gutter. HUD `WEEK 22`, filename `wk23`. |
| `move_floor_wk35` | **DEFECT** | `20 desks (+8 off the page)` runs past the page edge; `page)` lands on the wooden shelf behind the book. Blocker 4. |
| `move_hq_wk39` | **DEFECT** | `40 desks (+28 off the page)`; `page)` prints across the page edge onto the blue cabinet, dark ink on dark blue. Blocker 4. |
| `wk05_garage` | PASS (room aside) | Decision page, three options, no collisions, write field and `lock the week` both present. |
| `wk10_coworking` | PASS (room aside) | Two options wrapping to two lines each, no overlap, coworking room correct behind. |
| `wk15_coworking` | PASS (room aside) | Luminance 160.6, identical to every other journal open frame. The coworking room is not inherently dark. |
| `wk20_coworking` | PASS (room aside) | Luminance 160.7. Clean. |
| `wk25_office` | PASS (room aside) | Office room behind the paper. Clean. |
| `wk30_office` | PASS (room aside) | Worst case caption wrap: both options run to three lines. This is the 609 against 443 overrun and it still does **not** collide with the write prompt below. The cascade holds. |
| `wk35_floor` | PASS (room aside) | Floor room behind the paper. Clean. |
| `final_alive_wk40` | PASS | The run really ends. `WEEK 40 . THE BELL`, `$3,707,382`, `43% of the company x $6,870,000 market cap`, two multiplier chips, `THE LAST PAGE`. And note: **this screen has five characters in it.** The cast composes fine here. |

### `RUNWAY_LANEWIRE` (5)

| screen | verdict | evidence |
|---|---|---|
| `move_up_coworking` | PASS (minor) | Same as the fullrun move screen. Right page blank, title on the gutter. |
| `move_up_office` | PASS (minor) | Same. `9 desks`, `your name on a glass door`. |
| `move_down_coworking` | PASS | The best screen in the build. Cold light, coral struck title, five desks crossed out, `4 desks - 5 gone`, `morale -25. The boxes came back out.`, and the button changes to `PACK THE BOXES`. The demotion lands. |
| `finale_ipo` | PASS (nit) | Five multiplier chips fit. Arithmetic checks exactly: 57% x $42,000,000 x 2 x 1.25 x 1.15 x 1.1 x 1.1 = $83,281,275. Nit: confetti draws over the payout digits and over `NO OUTSIDE MONEY`. |
| `finale_acquisition` | **DEFECT** | Identical set to the IPO: the bell, the ticker wall, the trading floor. The player who was acquired is shown ringing the IPO bell. Blocker 5. |

## Blockers, worst first

### 1. The room is empty, in every single capture. `src/screens/garage_view_screen.gd:836`
Pixel confirmed in all 12 room captures across garage, coworking, office, floor and hq. Furniture, numbers on the walls, no people. Never the founder, never the two cofounders that were just recruited two screens earlier.

`_build_crew()` returns on its first line when `_scene_mode` is true (`:837`), because the people are meant to arrive painted into a model composed room instead of pasted on top. `_scene_mode` is set true at `:450` whenever `SceneRoom.load_scene()` succeeds, and 516 rooms ship, so it always succeeds. The composition that was supposed to replace the sprites is gated behind `main.gd:884 _art_enabled()`, which returns false for **any** harness env var (`:891`). Boot line confirms: `RUNWAY! scene library: 516 rooms . art off`.

So the fallback is disabled by a flag that is only ever true when the replacement is also disabled. Anyone running art off, which includes every harness, every `RUNWAY_NO_ART=1` run and every keyless player, gets an empty room forever. `final_alive_wk40.png` proves the cast art itself is fine.

Not verified: whether the art on path actually paints people into the room. That costs money and roughly three minutes a render, and the harnesses force it off by design, so I could not photograph it and will not guess.

### 2. The written move on the decision page never lights the lock. `src/screens/garage_view_screen.gd`, `_free_move`
Code verified, not pixel verified: no harness types into the field, so this needs a human at the keyboard to confirm.

`_free_move` re-renders the page only under `if _journal.visible and _page_i == 3`. Page 3 is the situation page. The decision page is page 4. So on the decision page: Enter fires, `_show_spread()` runs once with `_adjudicating` true (and `_page_decision` has no adjudicating state, unlike `_page_situation:1381`, so the page looks unchanged), the verdict comes back, `_pending_free` is set, and the page is never rebuilt. `_lock_button()` at `:1457` still reads `...decide first`.

From the player's chair: you write your move on the game's headline page, press Enter, and nothing happens. Recoverable only by paging away and back. The free written move is the whole pitch of this game.

### 3. Journal icons render 10px to 30px. `src/journal/journal_page.gd:237` and `:263`
```
:237   cell = Vector2(cap_w, cell.y - 46.0 + cap_h + 8.0)
:263   ic.set_deferred("size", Vector2(cap_w, cell.y - cap_h - 8.0))
```
Substituting one into the other, the icon height is always `incoming cell.y - 46.0`. Callers pass 76 for the top rows and 56 for the bottom rows, so icons come out at 30px and **10px**.

Pixel confirmed on `11_people` (`a bonus`, `a slice`, `new gear` are captions under 10px specks) and `12_work` (`outreach`, `demos`, `invoices` likewise). The same stray speck shows up above the money row on every Move screen and beside the runway jars on the week state sheet. Contrast `13c_pivot`, which passes a larger nominal height and gets properly sized icons.

The same expression is the overrun engine: `cap_h` grows with every wrapped caption and drags `cell.y` with it, which is how a 443px zone gets a 609px row.

### 4. Text runs off the page and prints onto the room. `src/screens/era_transition_screen.gd`
Cropped and confirmed at 3x on both `move_hq_wk39.png` and `move_floor_wk35.png`. `40 desks (+28 off the page)` and `20 desks (+8 off the page)`: the trailing `page)` crosses the book's right edge and is drawn on top of the garage's wooden shelf and blue cabinet. Dark ink on dark blue, effectively unreadable.

This is the exact failure mode the build has been chasing. It reproduces on the two largest eras, so any run that reaches floor or hq hits it.

### 5. The acquisition ending shows the IPO bell. `src/screens/finale_screen.gd:62`
```
room.load_scene("nasdaq_bell" if SceneRoomPicker.has_scene("nasdaq_bell") else "hq_steady")
```
No branch on the ending. `finale_acquisition.png` and `finale_ipo.png` are the same trading floor, the same bell, the same photographers. Only the headline word differs (`SOLD.` against `YOU RANG THE BELL.`) and the label under the number (`sale price` against `market cap`). Getting bought out and being shown yourself ringing the opening bell reads as a bug on the screen the player will screenshot.

### 6. Week 1 does not exist. `src/screens/garage_view_screen.gd:281` and `:971`
`GameState.week` initialises to 1 (`game_state.gd:6`). `_ready` calls `_start_week()` at `:281`, and `_start_week()` opens with `state.week += 1` at `:971`. So the first week a player ever sees is week 2.

Pixel confirmed everywhere: the HUD reads `BLOBIUM . WEEK 2`, the button reads `OPEN THE JOURNAL - WEEK 2 AWAITS`, the first journal page reads `WEEK 2`, and the autopsy chain reads `wk 0 founded Blobium` then `wk 2 The Roommate Talk`. On top of that, the very first journal greets a brand new founder with `You made no move last week. The week passed anyway, and it still cost you.` (`garage_view_screen.gd:1232`), which is an accusation about a week the player was never given.

### 7. The decision options have no affordance, and a locked option is silent. `src/screens/garage_view_screen.gd:1400` and `1408`, `src/journal/journal_page.gd:245`
The option dictionaries are built without a `tex` key, and `icon_row` only draws an icon `if it.has("tex")`, so each option is an invisible box with a caption at the bottom. On screen they are two floating sentences with nothing to say they are pressable.

Worse, `:1407` computes `"locked": locked != ""` and `icon_row` never reads it. A locked option looks exactly like an available one, and the click handler at `:1413` returns early with no sound, no mark and no message. A player will click a locked option, get nothing, and conclude the game is broken.

### 8. 108 zone overruns per run on the decision page. `src/journal/journal_page.gd:237`
Same root cause as blocker 3, listed separately because it is the loudest signal in the logs and because the current commit claims to have fixed it. It did not. Honest qualifier: I found **no** visible text on text in any of the 45 captures, and `wk30_office.png` shows the worst case (both options wrapping to three lines, 609 against 443) still resolving cleanly. The cascade is absorbing it. But the page is one long caption away from spilling, and shipping a build that pushes 108 warnings per run leaves no signal left to notice a real one.

### 9. The hq wall board clips its last line, and room surface type goes under 24px. `src/ui/scene_surfaces.gd:75`, called from `src/screens/garage_view_screen.gd:758`
Cropped at 3x from `era_hq_wk39.png`: the `IN THE BAG` board reads `Laptop / Savings Jar / Houseplant / +1 more`, and `+1 more` is sliced by the board's black frame. Same family as the old `CUSTOMERS` to `CUSTOME` clip.

`write()` derives everything from the face's declared line count:
```
:99    var per := rect.size.y / float(max(lines, 1))
:100   var lab_sz := int(clampf(per * 0.42, 16.0, 26.0))
:101   var val_sz := int(clampf(per * 0.78, 22.0, 54.0))
:102   while val_sz > 22 and ...x > rect.size.x * 0.94: val_sz -= 2
:112   v.set_deferred("size", Vector2(rect.size.x, rect.size.y - per * 0.5))
```
The shrink loop only ever answers **width**. Nothing measures the wrapped height, so a value with more lines than the face declares overflows `rect.size.y - per * 0.5` and the frame slices it. That is the `+1 more` clip: the hq inventory face declares fewer lines than a four item bag needs.

The same three lines are the answer to the 24px question. `lab_sz` floors at **16px** and `val_sz` floors at **22px**, both under the 24px minimum, and both are wall text the player is meant to read from across the room. Visible on every room capture in the small caps `IN THE BANK`, `USERS`, `PRODUCT`, `IN THE BAG` labels.

### 10. A keyless player cannot use the core mechanic at all. `src/llm/event_generator.gd:121`
```
if not llm.enabled():
    cb.call({})
    return
```
`llm_client.gd:181` requires both a provider and a non empty key. With no key the written move silently returns nothing: no verdict, no effect, no feedback, and the lock button never enables from writing. `.env.example` promises `The game runs fully without any key`. It does not; it runs without its headline mechanic.

Not the owner's own problem right now: `game/.env` holds a real 164 character key, so this bites anyone the build is handed to, not the machine it was built on.

### 11. The QA gate photographs a screen it is not on, and cannot photograph the real room. `src/main.gd:354` and `:884`
Two gate defects, called out because this project's stated failure mode is a gate printing green over a broken screen.

`main.gd:354` calls `d._select(4)`. `data/archetypes.json` holds exactly four archetypes, and `founder_draft_screen.gd:601` wraps with `wrapi(i, 0, _archs.size())`, so `4` becomes `0`. The file named `03_select_consultant.png` is a photograph of the hacker. The hero swap path has never been captured. The game's own wiring is correct (`chip.pressed.connect(_select.bind(i))` for i in 0 to 3), so this is purely a harness lie. Fix is `d._select(3)`.

`main.gd:884` `_art_enabled()` returns false for any of `RUNWAY_SHOT`, `RUNWAY_FULLRUN`, `RUNWAY_LANEWIRE`, `RUNWAY_READING`, `RUNWAY_TURN`. Every screenshot this project reviews is therefore of a code path that a keyed player never sees, and specifically of the path where the room is empty. The gate is structurally incapable of showing the room the owner actually plays.

### 12. One era capture came out three times too dark. `src/screens/garage_view_screen.gd:1802` (`_next_week` scrim) or the fullrun capture timing
`era_coworking_wk10.png` measures 61.6 mean luminance. Every other era shot measures 184 to 195, and every journal open shot measures 155 to 161. The whole frame including the HUD sits under a heavy overlay, which is the shape of the `_next_week()` dark `ColorRect` caught mid tween. Most likely a harness timing race on the era beat rather than a game bug, but it happened on one of four era shots and if a player can land on it the scrim is not being cleared.

## Nits, not blockers

- `06_recruit`: the coral strike X is drawn at full opacity over the card body, costing legibility on two of five cards.
- `04_name`: the stage is dark enough that the founder is hard to make out.
- `01b_gallery`: the subtitle's descenders and the comma in `$417,648` are nicked by the corkboard edge.
- `era_office_wk23`: the whiteboard reads `v0.100`. `"v0.%d" % state.product` needs a cap or a rollover to `v1.0`.
- `finale_ipo` and `finale_acquisition`: confetti draws above the payout digits and above the chip labels, so `NO OUTSIDE MONEY` reads as `NO OUTS DE MONEY`.
- The Move screens leave the entire right hand page blank and centre the title and the desk row across the book gutter.
- Move filenames are off by one against the HUD (`move_office_wk23.png` shows `WEEK 22`).
- `final_alive_wk40` reads `43% x $6,870,000 = $3,707,382`. The arithmetic is right; the displayed percentage is rounded. Showing one decimal would stop it reading as a mistake.
- `docs/screens.md` is stale in four places: the `Known gap` section (eras do change the room now), `06_recruit` says 4 candidates and there are 5, `13c_pivot` is described as one page and is three, `finale_ipo` says 4 multipliers and shows 5.

## Overall verdict

**Playable end to end, and not yet good enough to hand over.**

The engine is sound. Three harnesses, zero script errors, a real 40 week run that reaches all five eras and terminates in a genuine ending, five distinct rooms, and a demotion screen (`move_down_coworking`) that is genuinely excellent. Nothing here crashes, hangs or soft locks.

What is wrong is what the player looks at. The rooms are unpopulated in every capture I have, which is the complaint that started this. The one page the whole game is built around gives no response when you use it as intended. The icons on the two pages where you spend your week are ten pixels tall. And on the two biggest eras, the words fall off the paper onto the furniture.

Fix blockers 1, 2 and 3 and this is a game. Ship it as it stands and the first run reads as an empty room where the typing does nothing.
