# RUNWAY! — SCREEN LANE BRIEF (shared by every screen agent)

You own ONE screen (or one tightly related screen group). You take it from its current state to an award-winning indie game screen, autonomously, in review→fix rounds. You do not touch other lanes' screens.

## Repo + tools
- Godot project: `/Users/assem/Documents/Doc-Assem/Claude Code/runway/game`. Run `godot --headless --path . --import > /dev/null 2>&1` after asset changes.
- Capture ALL states: `rm -rf <dir>; RUNWAY_SHOT=<dir> godot --path . > <log> 2>&1` (≈2 min) → PNGs 01_title … 14_autopsy incl. 04b_shape, 09b_consequences_real, 10b_room_item_note, 10c_room_in_the_red, 12b_situation, 13b_decision_ready, 13c_pivot. Use your OWN dir: `/tmp/lane_<yourlane>_rN`. Then `grep -cE "SCRIPT ERROR" <log>` must be 0.
- Smoke: `godot --headless --path . --script tests/smoke.gd` must print SMOKE PASS before you claim a round done.
- NEVER `pkill`; NEVER touch `game/.env`, `prompts/`, `music/`, or another lane's screen script; PROJECT_LOG.md is append-only.
- Patch discipline: python anchored replaces with `assert old in s` on EVERY anchor, one write at the end, re-grep to confirm. Godot trap: configure autowrap/expand BEFORE size; assign texture LAST or `set_deferred("size", …)`.

## The bar (the owner's words, hold to them)
"AAA / award-winning indie game screen, not a SaaS form." "Text is small" is a defect. "Assembled, not organic" is a defect. "Doesn't feel like a video game screen people understand and want to interact with" is a defect. Every element must look INTENDED: sized to the art, integrated into the illustration (text wraps to paper shapes, HUD sits on drawn plates, nothing floats on a void), animated where a real game would animate it (hover, select, transitions, ambient life).

## Constitution
- Art: hand-drawn wobbly felt-pen ink, flat fills, no gradients. Palette ONLY cream #F2EAD3 · ink #1E1E1E · coral #E86A5C · yellow #F4B942 · sage #8FA582 · blue #6E8CA0 · white. Character = blob v2 (cowlick spike, mismatched white oval eyes left bigger, tiny cream sneakers one lace untied, forward lean; archetypes differ by props only).
- Typography: `assets/fonts/Baloo2-Bold.ttf` for headers/buttons/big numbers; `PatrickHand-Regular.ttf` ONLY for diegetic journal handwriting. Floor: nothing under 24px on the 1536×1024 canvas; body 26–30; titles 42+; placeholders ≥ 0.45 alpha ink.
- 60 Seconds! constitution: the room IS the save file (money physically present, product on the whiteboard, users on the wall chart, crew visibly IN the room with mood states, decay with morale, memorabilia by era); decisions in the paper journal (5 flippable spreads, one LOCK); dread beat between weeks; the world adjudicates written moves.
- Journal geometry: art renders 1340×814 at (98,93); left paper x≈173→122 (leans), right ≈768→1361..1415; page frames currently left (206,176) 500×640, right (900,176) 450×640. Keep text inside paper with margins; use the faint ruling.

## Assets — SCENE-FIRST
Generate FULL composed scenes, not sprite kits, whenever a screen needs art. Then decompose and place:
1. Generate: `POST https://nano-banana-production-e03b.up.railway.app/generate-image-openai` header `x-openai-api-key: $(cat /private/tmp/claude-501/-Users-assem-Documents-Doc-Assem-Claude-Code-runway/46461c38-41e8-4daa-aa34-0dc94af8f9ef/scratchpad/openai-key.txt)` JSON `{"prompt","quality":"high","size":"1536x1024","output_format":"png"}` (~2 min; download `imageUrl` immediately, expires 1h). Always append the palette/style block to the prompt. Sprites-on-magenta only for things that MUST recompose. CHROMA-KEY BY MAGENTA DOMINANCE, `min(R-G, B-G)`, NOT by Euclidean distance to magenta: on this palette mid-greys score ~209-215 and coral ~196 by distance, both inside any sane feather band, so distance-keying eats every contact shadow and leaves a purple halo where it was. With dominance, greys score 0 and coral scores negative, so neither can be read as background.
2. Permanent URL for decomposition: `nexus asset upload <png> --json` → `.url` (signed URLs expire mid-job).
3. Decompose: `POST https://api.atlascloud.ai/api/v1/model/generateImage` bearer `$(cat …/scratchpad/atlas-key.txt)` model `bytedance/seedream-v5.0-pro/layer-decomposition`, numbered element list in `prompt`, `size 2K`, `output_format png`; poll `/api/v1/model/prediction/<id>` every 5s; outputs[0]=inpainted base, rest=cutouts (they come UPSCALED and cropped to content).
4. Place: bbox-crop each cutout, template-match it against the ORIGINAL 1536×1024 scene over scales ~0.12–0.40 (cutouts are ~1.6–4× upscaled crops), write `layout.json` {name:{x,y,w,h}}, load in GDScript at those coords. Reference implementation pattern: `assets/scenes/garage/` (in progress by main).
Budget: ≤6 generations per lane unless your brief says otherwise. Log every generation (prompt id, purpose) in your round log.

## Your loop (max 4 rounds)
1. Shoot → READ your screen's PNGs (all states of your screen). Write down every defect you see against the bar.
2. Fix. Re-shoot. READ again — a fix is done only when you SAW it fixed.
3. Spawn a fresh reviewer subagent (general-purpose) blind to code: give it your PNG paths + this brief's "The bar" + "Constitution" sections verbatim; it must Read every PNG, score /10 with sub-scores TYPOGRAPHY, PLACEMENT/COMPOSITION, INTEGRATION (organic vs assembled), GAME-FEEL; refute each defect before reporting; return worst-first findings with screen/element/what/fix-target. It edits nothing.
4. Fix everything ≥medium. Log the round to `/tmp/lane_<yourlane>/rounds.md` (scores, fixed, deferred+why) INCREMENTALLY.
5. Stop at ≥9/10 on all sub-scores from a fresh reviewer, or 4 rounds. Final message: rounds, score trajectory, defects fixed, remaining + why, generations spent, files touched.

## JOURNAL DESIGN SPEC v2 (NUMERIC — owner escalation 2026-08-18 "unprofessional"; adjectives failed, these are LAWS)
Type scale on the 1536×1024 canvas — NOTHING else is permitted in the book:
- Page title: 56px Baloo2, ink, top of page, ONE per page + 6px coral underline bar
- Era/context subtitle under title: 28px Patrick, 60% ink
- Section label: 30px Baloo2, blue, ALL CAPS, max TWO sections per page
- Body/entry line: 34px Patrick, ink (colored only for +/− semantics)
- THE BIG NUMBER: every page has exactly one 72px Baloo2 hero figure (runway weeks, verdict, payout, net burn) — the thing you read from the couch
- Footnote/doodle caption: 24px Patrick 55% ink, max one per page
Layout laws: max 5 text blocks per page total; 24px minimum vertical gap; every line passes through PageSpace (trapezoid+arc warp); no element within the spiral/pencil band (x 820-900) or 30px of any paper edge; underlines are wobbly polylines NEVER straight rects and NEVER through text (the "filed under: lessons" strikethrough bug class); NO DUPLICATE DATA LINES on a page (the double cash line bug class — dedupe before render); sticky notes/doodles anchor to a rule or corner, never float mid-void; plates/HUD must fully contain their text at the LONGEST company name (12 chars + " · WEEK 88").
Self-review gate before claiming any round: read the PNG at 100% and answer in the round log: (1) can I read every line at arm's length? (2) is there exactly one hero number? (3) any duplicate/overlapping/floating element? A yes on 3 or a no on 1-2 = not done.

## SCENE COMPOSITION SAFE ZONES (owner law 2026-08-18 — coherence of art + UI)
Every generated scene is a STAGE that UI is laid on top of. On the 1536×1024 canvas:
- TOP BAND y 0–100: calm wall/sky only. HUD plate lives at (24,14) 430×52 — its patch must be low-detail.
- BOTTOM BAND y 895–1024: calm floor only. Primary CTA (e.g. OPEN THE JOURNAL, 560,930 420×76) sits center-bottom — that middle third must be empty and low-contrast.
- STAGE BAND y 200–800: all characters and state objects (money pile, whiteboard, chart, crew) live here.
- SIDE RAILS x<120 and x>1420: keep clear of critical subject matter (era badges, item notes, vignettes).
- OUTER 4% margin: nothing touches or crosses it.
Enforced automatically in `tools/scene_pipeline.py` STYLE (appended to every generate/variant prompt). When placing UI on a scene, verify against the scene's own art — if a scene violates a zone, regenerate it with `variant --ref` rather than moving the UI.

## DIEGETIC STATE — THE NUMBERS LIVE ON SURFACES IN THE ROOM (owner law 2026-08-19)

The room IS the save file, so the numbers belong to objects in it. Cash is written in
the ledger, product on the whiteboard, customers on the wall chart, equity on a sticky
note. They are NEVER floating plates laid over the art. The owner's words: "every scene
must have available space to write stuff BUILT INTO the space, like a whiteboard, a post
it note, etc. so it is easy to write on them", and "what you add on the scene like money
value in $, current shares, revenue, customers, etc. is tangible".

What this replaced: "57% yours" on a cream plate slapped over a sticky note and clipped
by its own plate, "$-300" floating in the middle of the garage floor anchored to nothing,
and a whiteboard drawn into the same scene sitting completely empty beside them.

GENERATION: `tools/scene_pipeline.py` STYLE now requires every scene to contain at least
FOUR blank writable surfaces — a whiteboard or chalkboard, a wall-pinned chart or sheet,
a clipboard or open ledger lying flat, and a cluster of sticky notes. They must arrive
COMPLETELY BLANK (no letters, numbers or drawn charts), nearly flat-on so writing sits
squarely, large enough for two or three lines, pale-faced against their surroundings, and
spread around the middle band rather than clustered.

ANNOTATION: every scene's `layout.json` declares where they are, in the scene's own
1536x1024 coordinates, alongside the cutout layers:

    "write_surfaces": {
      "whiteboard": {"x":520,"y":195,"w":330,"h":205,"rot":0.0,  "lines":3, "align":"center"},
      "wallchart":  {"x":950,"y":180,"w":175,"h":215,"rot":0.02, "lines":2},
      "ledger":     {"x":690,"y":470,"w":150,"h":95, "rot":-0.06,"lines":2},
      "sticky":     {"x":660,"y":90, "w":95, "h":95, "rot":0.04, "lines":2}
    }

THE INVENTORY BOARD (owner 2026-08-19): every scene also carries an `inventory`
surface — a corkboard, slate or hung clipboard — where the game writes the run's
current inventory as variable text: what is packed, what is owned, what is left.
It is the one surface whose CONTENT LENGTH VARIES most, so annotate it with the
largest face in the room and `lines` 3 or more, and prefer a portrait-ish shape
since it holds a list rather than a headline.

Rules for annotating: x,y is the top-left of the WRITABLE FACE, not of the object —
exclude the whiteboard's frame, the clipboard's clip, the sticky's curled corner. Inset
~8% on every side so writing never touches a drawn edge. `rot` is radians read off the
object's own lean in the art; a surface that leans with text that does not is worse than
neither. `lines` is how many lines of handwriting the face holds at a readable size, two
is the safe default and only a big whiteboard holds three. Never annotate a surface the
art already drew writing on.

RENDERING: `src/ui/scene_surfaces.gd` (SceneSurfaces, MAIN-owned, instantiate never edit).
`mount(scene_id)` returns false when a scene has not been annotated yet, so a screen can
fall back rather than lose the information. `write(name, label, value)` sets handwriting
at the surface's own lean and shrinks the value to fit rather than crossing the edge.
`tally(name, count)` draws struck-through tally marks for counts.

## THE JOURNAL PAGE SHELL — src/journal/journal_page.gd (MAIN-owned, instantiate never edit)

Every journal page is built through `JournalPage`. It owns the geometry and the type so a
page script cannot pick a wrong font, a wrong size, or a position off the paper.

WHY: the drawn sheet LEANS. Measured off the art, the writable left edge travels x=146 to
x=192 and the right edge x=892 to x=952 down the page, the spiral perforation eats the top
and the curled corner eats the bottom right. A rectangle laid over that MUST cross the
paper somewhere, which is what produced every overflow the owner reported. So text is not
wrapped to a box — it is wrapped to the real silhouette, line by line, using the writable
span measured at each line's own y. That table is `assets/ui/logbook_page_zones.json`,
extracted from the PNG's alpha, and regenerating the art regenerates the table.

Text also SNAPS TO THE PRINTED RULES (17 of them, 44.9px pitch at render scale) and each
line advances by whole rules, so it sits on the ruling like handwriting instead of
floating between the lines.

FOUR ZONES, fixed in advance, same anatomy on every page:
  TITLE    0.082-0.199   the one hand-lettered heading
  BODY     0.199-0.466   what is happening, in the founder's hand
  ENDING   0.466-0.800   the payload: visuals, selectable icons, or the written move
  CONTROLS 0.800-0.917   navigation and commit, nothing else

API: `build(title, scene_id)` puts the live animated room behind the sheet. `line(text)`,
`icon_row(items)` (selection is a pen circle, never a button), `write_field()` (the free
written move, on the ruling, no box), `ask(situation, options)` — the shape EVERY page
ends in, per the owner: state the situation then ask what you want to do — `overview()`,
and `arrows(prev, next)`. `room_left(zone)` reports free space; a page that overruns
should be SPLIT, never shrunk.

## THE LOG BOOK REFERENCE (owner-supplied, MANDATORY for every lane touching the book)
READ `docs/refs/60s_logbook_ration_page.jpg` with the Read tool before laying out any book page. Owner: "look how well the text and selection should integrate in a log book. this is much more organic, clear and readable." Other 60 Seconds! reference shots sit beside it in docs/refs/.
What that page actually does — copy this model:
1. ONE facing page at a slight angle fills the frame; previous pages curl behind; the room shows around the edges. Not a flat two-page spread.
2. THREE text elements maximum per page: a huge centered hand-lettered TITLE ("DAY 6" → our "WEEK 6"), and one or two short handwritten prompts ("Time to ration supplies…", "What is left…").
3. STATE IS DRAWN, NOT WRITTEN — rows of icons carry the numbers: their soup cans/water bottles = our runway money-stacks (one per week, spent ones greyed), crew faces in a row (dead = scribbled out in pen), product/customers as icon rows.
4. SELECTION IS A PEN MARK: you place an icon under a face / circle a choice; chosen = circled in ink, unchosen = greyed. No text button lists on the main flow.
5. NAVIGATION IS DIEGETIC: hand-drawn arrows in the page's bottom corners, small page name in a corner. No UI chrome, no dot indicators.
6. Long prose (event body, adjudication) is the exception, in big handwriting, never a wall of text.

### Reference index — docs/refs/ (READ THESE, they are the target)
All refs are now official 1920x1080 press screenshots. The eight earlier files were
screen-captures of a Google Images results page — letterboxed, with the browser's Lens
button and a size badge baked in, and the book cut off at the bottom. They are retired to
`_superseded/`; do not read them.

THE BOOK PAGES:
- `60s_logbook_ration_page_hires.jpg` — THE model page ("DAY 15"): huge hand-lettered title,
  one short prompt, crew faces in a row with the item assigned to each shown UNDER the face,
  supplies as icon rows with spent ones greyed, hand-drawn corner arrows. Copy this.
- `60s_logbook_two_questions.jpg` — TWO stacked questions on ONE page ("What to take outside?"
  then "Who should go outside?"), each answered by its own row of icons. This is the model for
  any screen that asks more than one thing — including THE PIVOT. Note how little text it uses
  and that each question owns a labelled icon row directly beneath it.
- `60s_logbook_event_binary_choice.jpg` — an EVENT page: a handwritten paragraph, then the choice
  as two big drawn marks (a cross and a tick). Prose is allowed here; the choice is still art.
- `60s_logbook_ration_page.jpg` — the original lower-res copy of the model page, kept because it
  is the exact frame the owner pointed at.

THE ROOM AT DIFFERENT CREW SIZES — study these together, they solve our composition problem:
- `60s_room_crew_pair.jpg`, `60s_room_crew_three.jpg`, `60s_room_crew_three_alt.jpg`,
  `60s_room_crew_four.jpg`, `60s_room_crew_five.jpg` — THE SAME ROOM with different numbers of
  survivors. The room art never changes; the cast is composited onto fixed marks along the bench
  and floor, each at its own scale, partly occluded by the table and crates in front of them.
  This is exactly our 124-crew problem and exactly our solution: loop the empty room, place the
  cast as sprites. Match how casually they overlap and how each one sits BEHIND some foreground object.
- `60s_room_crew_sparse.jpg` — the same room nearly empty, with supplies stacked instead. Depletion
  is shown by what is present, not by a number.
- `60s_room_crew_costumes.jpg` — the cast differentiated purely by props and headgear on identical
  bodies. Our character law (props only, never bodies/faces/clothes) is the same idea.
- `60s_room_death_skeleton.jpg` — death as an ART SWAP IN PLACE: the survivor becomes a skeleton on
  the same mark. Our burnt-out and departed states swap the same way.

Rules to steal, beyond the log-book six: the book is a physical object in the room (open it by
clicking it, not a UI button); elapsed time is diegetic (wall tallies, not a HUD counter); crew
death/burnout is an ART SWAP in place; depletion is shown by emptying shelves; screen chrome is
ONE corner icon, nothing else.
