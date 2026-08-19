#!/bin/bash
# BATCH M: the loops. The loop belongs to the ROOM — every character composites
# on top as a static sprite, so NOTHING in these may imply a person is present
# or has just moved. No chair rolling, no door swinging, no page turning, no
# mug being set down. Only weather-of-the-room: dust, light, steam, leaves.
cd "/Users/assem/Documents/Doc-Assem/Claude Code/runway/game" || exit 1
P="python3 tools/scene_pipeline.py"

AMB="Bring this empty room alive as a calm seamless loop, keeping the artwork EXACTLY as drawn. The room is EMPTY and must STAY empty — no creature, person, figure or silhouette ever appears, not even partly, not even entering at the edge. Camera completely static. No object changes shape, size or position, no chair moves, no door opens, nothing is picked up or put down, and no lettering, chart or diagram redraws itself. The flat 2D hand-drawn style is preserved perfectly."

# the two specials do not exist as empty art yet
EMPTY="THE PLACE IS COMPLETELY EMPTY OF PEOPLE. There is not a single creature, person, figure, character, face or silhouette anywhere in this picture. Nobody is present. Do not draw anyone. Leave four separate patches of clear open floor in the middle band where someone could stand. The light falls from the upper left, so every object casts a soft shadow down and to the right."

$P generate stage_nasdaq "A stock-exchange bell platform seen straight on: a raised podium with a big brass ceremonial bell hanging in a frame, bunting draped along the podium front, a large dark display board on the wall behind showing abstract coloured bars and arrows with no readable numbers, potted plants at either side and confetti settled on the floor. $EMPTY" > /tmp/bm_nas.log 2>&1 &

$P generate stage_yc "A startup demo-day stage seen straight on: a low wide stage platform, a standing microphone, a very large blank presentation screen behind it, stage lighting above, a row of empty chairs facing the stage in the foreground and potted plants at the sides. $EMPTY" > /tmp/bm_yc.log 2>&1 &

$P animate stage_garage    "$AMB The hanging bulb sways almost imperceptibly and its light breathes very slightly, dust drifts slowly through the air, and the plant's leaves stir a little." > /tmp/bm_garage.log 2>&1 &
$P animate stage_coworking "$AMB The pendant lamps breathe very slightly, dust drifts slowly through the air, and the plants' leaves stir a little." > /tmp/bm_coworking.log 2>&1 &
$P animate stage_office    "$AMB The small status lights on the server rack blink slowly, dust drifts through the air, and the plant's leaves stir a little." > /tmp/bm_office.log 2>&1 &
$P animate stage_floor     "$AMB Dust drifts slowly through the air, the plants' leaves stir a little, and the monitors glow very faintly." > /tmp/bm_floor.log 2>&1 &
$P animate stage_hq        "$AMB Far outside the tall windows the flat city skyline stays completely still, dust drifts slowly through the air, and the big plants' leaves stir a little." > /tmp/bm_hq.log 2>&1 &

wait
echo "BATCH M DONE"
python3 tools/scene_pipeline.py verify | tail -1
