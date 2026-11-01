#!/bin/bash
# BATCH G: animate every scene that passes the corrected zone audit.
cd "/Users/assem/Documents/Doc-Assem/Claude Code/runway/game" || exit 1
P="python3 tools/scene_pipeline.py"

# Shared motion law: static camera, art preserved exactly, nothing enters or leaves.
M="Bring this hand-drawn scene alive as a calm seamless loop, keeping the artwork EXACTLY as drawn. Camera completely static. Nothing enters or leaves the frame, no object changes shape or size, no lettering or diagram redraws itself, and the flat 2D hand-drawn style is preserved perfectly. The creatures breathe and shift their weight very slightly and their cowlick spikes sway a little."

$P animate office_steady_g   "$M The creatures at the desks keep typing, the one at the whiteboard keeps gesturing at it, and the soft glow inside the server cupboard pulses very gently." > /tmp/bg_off.log 2>&1 &
$P animate floor_steady_g    "$M The creatures at the desks keep typing, the two by the foosball table keep talking and one rocks a handle back and forth, and steam drifts slowly from the coffee station." > /tmp/bg_flr.log 2>&1 &
$P animate hq_steady_g       "$M The creatures keep talking in their small groups, and far outside the tall windows the flat city skyline stays completely still." > /tmp/bg_hq.log 2>&1 &
$P animate nasdaq_bell_g     "$M The creature with the raised arm keeps it raised and rocks very slightly, the others keep clapping, and the confetti drifts slowly downward through the air. The ticker wall glows steadily without its numbers redrawing." > /tmp/bg_nas.log 2>&1 &
$P animate garage_thriving_v5 "$M They keep working at what they are already doing and the hanging bulb sways almost imperceptibly." > /tmp/bg_gthr.log 2>&1 &
$P animate garage_night_solo_v5 "$M The lone creature keeps typing, its screen light flickers almost imperceptibly across it, and the hanging bulb sways very slightly." > /tmp/bg_gnight.log 2>&1 &
$P animate garage_starving_v2 "$M They keep at their small tasks, slower and wearier than usual, and dust drifts slowly through the light." > /tmp/bg_gstar.log 2>&1 &
$P animate coworking_steady_v3 "$M The creatures keep working at their desks and one leans back in its chair very slightly." > /tmp/bg_cw.log 2>&1 &

wait
echo "BATCH G DONE"
