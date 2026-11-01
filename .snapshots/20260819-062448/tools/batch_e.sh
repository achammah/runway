#!/bin/bash
# BATCH E: dense rooms REGENERATED from scratch (variant --ref cannot restructure
# a busy room; the STYLE block now carries the safe-zone law) + targeted micro-fixes.
cd "/Users/assem/Documents/Doc-Assem/Claude Code/runway/game" || exit 1
P="python3 tools/scene_pipeline.py"

STAGE="Compose it as a game background: a wide calm empty band of plain wall across the very top, a wide calm empty band of plain floor across the very bottom with its middle completely clear, plain empty wall down the far left and far right edges, and ALL the furniture, characters and objects gathered in the middle band of the picture and pushed toward the left-centre and right-centre so the centre stays readable. The upper-left corner is flat empty wall."
CREW="The characters are small solid ink-black bean-shaped creatures, each with one ink cowlick spike, two blank white oval eyes with the left slightly bigger, thin black stick limbs and tiny cream sneakers."

$P generate office_steady_g "The inside of a startup's first small real office, seen straight on from across the room. A glass entrance door on one side, a couple of desks with monitors, a server cupboard standing slightly ajar with a soft glow inside, a whiteboard with ink diagrams, a small plant. Four of the creatures work at the desks and one stands by the whiteboard. $CREW $STAGE" > /tmp/be_off.log 2>&1 &
$P generate floor_steady_g "The inside of a busy open-plan startup floor, seen straight on from across the room. Two neat rows of desks with monitors running back into the middle distance, a foosball table off to one side, a wall of small charts and sticky notes, a coffee station. Eight of the creatures work at the desks and two talk beside the foosball table. $CREW $STAGE" > /tmp/be_flr.log 2>&1 &
$P generate hq_steady_g "The inside of a modern startup headquarters, seen straight on. Tall windows along the back showing a calm flat city skyline, a small stage with a microphone stand for all-hands meetings, a long meeting table, a big framed chart on the wall. Six of the creatures work and talk in small groups. $CREW $STAGE" > /tmp/be_hq.log 2>&1 &
$P generate nasdaq_bell_g "A stock-exchange opening-bell ceremony seen straight on: a raised podium with a big brass bell on a stand, a huge flat ticker wall of scrolling numbers behind it, confetti in the air. Five of the creatures stand on the podium, one with its stick arm raised to strike the bell, the others clapping. $CREW $STAGE" > /tmp/be_nas.log 2>&1 &
$P generate garage_steady_g "The inside of a cluttered suburban garage being used as a startup office, seen straight on from across the room. A workbench with a pegboard of tools, a whiteboard on a stand covered in ink diagrams, a metal shelf with paint cans, a wooden crate with a small stack of banded cash on it, a hanging bulb, a houseplant, a pizza box on the floor. Three of the creatures work: one typing at a laptop, one soldering at the bench, one pointing at a pinned paper chart. $CREW $STAGE" > /tmp/be_gar.log 2>&1 &

# targeted micro-fixes (single-zone failures)
$P variant garage_thriving_v4 "Same scene, same characters, same objects, same palette and style as the reference. ONE change only: the leftmost eighth of the image becomes plain empty wall in flat colour with absolutely nothing drawn on it — move or remove whatever currently sits there. Everything else identical." --ref garage_thriving_v3/scene > /tmp/be_gthr.log 2>&1 &
$P variant garage_night_solo_v5 "Same scene, same character, same objects, same palette and style as the reference. ONE change only: the upper-left corner and the top strip of the image become flat empty wall with absolutely nothing drawn on them — no shelf, no poster, no cable, no window frame. Everything else identical." --ref garage_night_solo_v4/scene > /tmp/be_gnight.log 2>&1 &
$P variant coworking_steady_v3 "Same scene, same characters, same objects, same palette and style as the reference. ONE change only: the upper-left corner and the top strip of the image become flat empty wall with absolutely nothing drawn on them. Everything else identical." --ref coworking_steady_v2/scene > /tmp/be_cw.log 2>&1 &

wait
echo "BATCH E DONE"
