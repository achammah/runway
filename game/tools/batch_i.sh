#!/bin/bash
# BATCH I: the real gaps in SceneRoomPicker's id set — scenes the picker can
# return that have NO anim/ dir and would render as a dead still behind the
# logbook page. Calm ambient motion only: the page sits on top, so nothing may
# read as movement in the centre of frame.
cd "/Users/assem/Documents/Doc-Assem/Claude Code/runway/game" || exit 1
P="python3 tools/scene_pipeline.py"

CALM="Bring this hand-drawn scene alive as a calm seamless loop, keeping the artwork EXACTLY as drawn. Camera completely static. Nothing enters or leaves the frame, no object changes shape or size, no lettering, chart or diagram redraws itself, and the flat 2D hand-drawn style is preserved perfectly. The motion is ambient and gentle only — the creatures breathe and shift their weight very slightly and their cowlick spikes sway a little. Keep the CENTRE of the frame especially still: no large or fast movement there."

$P animate office_thriving      "$CALM Dust drifts slowly through the light and the creatures keep working at what they are already doing." > /tmp/bi_offthr.log 2>&1 &
$P animate launch_day           "$CALM The creatures keep celebrating in place with small bobbing movements and any confetti drifts slowly downward." > /tmp/bi_launch.log 2>&1 &
$P animate pivot_night          "$CALM The lamp and screen light flicker almost imperceptibly and dust drifts slowly through the beam." > /tmp/bi_pivot.log 2>&1 &
$P animate hackathon_night      "$CALM Screen light flickers faintly across the creatures as they keep typing, and steam rises slowly from a mug." > /tmp/bi_hack.log 2>&1 &
$P animate first_customer_call  "$CALM The creatures hold their poses and only breathe and shift their weight; any steam or dust drifts slowly." > /tmp/bi_first.log 2>&1 &

wait
echo "BATCH I DONE"
