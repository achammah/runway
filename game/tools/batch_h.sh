#!/bin/bash
# BATCH H: character-fidelity lock. Six rooms passed the zone audit but drew the
# crew off-model (pupils, mouths, eyebrows, hoodies). Room is kept from ref 1,
# the crew is redrawn to match ref 2 (floor_steady_g, verified on-model).
cd "/Users/assem/Documents/Doc-Assem/Claude Code/runway/game" || exit 1
P="python3 tools/scene_pipeline.py"

FIX="Keep the room in the FIRST reference image EXACTLY as it is: same layout, same furniture, same objects, same props, same colours, same lighting, same composition, nothing moved and nothing added or removed. ONE change only: redraw every creature so it matches the character design in the SECOND reference image. Each creature is a small SOLID INK-BLACK bean-shaped blob with one ink cowlick spike, thin black stick limbs and tiny cream sneakers. Its ONLY facial features are two blank white oval eyes, the left slightly bigger. Remove every pupil, dot, iris, eyelid, eyebrow, mouth and nose. Remove ALL clothing — no hoodies, no shirts, no jackets, no hats — so each body is an unbroken solid black silhouette. Keep every creature in the same place, at the same size, in the same pose, doing the same thing."

$P variant hq_steady_gc        "$FIX" --ref hq_steady_g/scene        --ref floor_steady_g/scene > /tmp/bh_hq.log 2>&1 &
$P variant nasdaq_bell_gc      "$FIX" --ref nasdaq_bell_g/scene      --ref floor_steady_g/scene > /tmp/bh_nas.log 2>&1 &
$P variant coworking_steady_gc "$FIX Also remove the drawn white picture-frame border around the artwork so the scene fills the whole frame edge to edge." --ref coworking_steady_v3/scene --ref floor_steady_g/scene > /tmp/bh_cw.log 2>&1 &
$P variant garage_starving_gc  "$FIX" --ref garage_starving_v2/scene --ref floor_steady_g/scene > /tmp/bh_gstar.log 2>&1 &
$P variant garage_thriving_gc  "$FIX" --ref garage_thriving_v5/scene --ref floor_steady_g/scene > /tmp/bh_gthr.log 2>&1 &
$P variant garage_night_gc     "$FIX" --ref garage_night_solo_v5/scene --ref floor_steady_g/scene > /tmp/bh_gnight.log 2>&1 &

wait
echo "BATCH H DONE"
