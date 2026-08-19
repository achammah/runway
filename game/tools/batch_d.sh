#!/bin/bash
# BATCH D: era scene re-frames. Stronger, literal rail/HUD language — the soft
# "recompose" wording did not clear shelves out of the rails.
cd "/Users/assem/Documents/Doc-Assem/Claude Code/runway/game" || exit 1
P="python3 tools/scene_pipeline.py"

R="Same room, same characters, same objects, same palette, same hand-drawn style as the reference — this is a RE-FRAMING only. Obey these literally: (1) the leftmost eighth and the rightmost eighth of the image must be PLAIN EMPTY WALL in flat colour with NOTHING drawn on them — no shelves, no doors, no posters, no plants, no furniture, no cables. (2) The top tenth of the image must be plain empty wall or ceiling with nothing hanging into it, and the upper-left corner especially must be flat empty colour for a small label plate. (3) The bottom seventh must be plain empty floor, and the middle third of that bottom strip must be completely clear — no props, no feet, no shadows. (4) Move every character and every important object into the middle band of the picture, pushed toward the left-centre and right-centre, leaving the centre of the frame readable. Nothing may touch the outer margin."

$P variant garage_v4            "$R" --ref garage/scene            > /tmp/bd_garage.log 2>&1 &
$P variant garage_thriving_v3   "$R" --ref garage_thriving_v2/scene > /tmp/bd_gthr.log 2>&1 &
$P variant garage_night_solo_v4 "$R" --ref garage_night_solo/scene  > /tmp/bd_gnight.log 2>&1 &
$P variant coworking_steady_v2  "$R" --ref coworking_steady/scene   > /tmp/bd_cw.log 2>&1 &
$P variant office_steady_v2     "$R" --ref office_steady/scene      > /tmp/bd_off.log 2>&1 &
$P variant floor_steady_v2      "$R" --ref floor_steady/scene       > /tmp/bd_flr.log 2>&1 &
$P variant hq_steady_v2         "$R" --ref hq_steady/scene          > /tmp/bd_hq.log 2>&1 &
$P variant nasdaq_bell_v2       "$R" --ref nasdaq_bell/scene        > /tmp/bd_nas.log 2>&1 &

wait
echo "BATCH D DONE"
