#!/bin/bash
# BATCH C: promote passing select stages -> canonical ids, animate all 6, + journal page turn
cd "/Users/assem/Documents/Doc-Assem/Claude Code/runway/game" || exit 1
P="python3 tools/scene_pipeline.py"

# promote v2 art onto the canonical ids LANE-FLOW will reference
for a in hacker hustler dropout exfaang consultant; do
  cp "assets/scenes/select_${a}_v2/scene.png" "assets/scenes/select_${a}/scene.png"
done
echo "promoted 5 select stages"

MOTION="Bring this hand-drawn character-select stage alive as a calm seamless loop, keeping the artwork EXACTLY as drawn. Only subtle idle motion: the character breathes and shifts its weight very slightly, its cowlick sways a little, and it keeps doing its small action in place. A few dust motes drift slowly down through the spotlight beam and the light flickers almost imperceptibly. The curtains at the edges are still. Camera completely static, nothing enters or leaves the frame, the character stays exactly the same size and position, flat 2D hand-drawn style preserved perfectly."

$P animate select_hacker     "$MOTION The character keeps typing on its laptop." > /tmp/bc_hack.log 2>&1 &
$P animate select_hustler    "$MOTION The character keeps talking into one phone and glancing at the other, its tie swaying." > /tmp/bc_hust.log 2>&1 &
$P animate select_dropout    "$MOTION The skateboard under its foot rocks gently back and forth." > /tmp/bc_drop.log 2>&1 &
$P animate select_exfaang    "$MOTION It sips its coffee and the floating sticky notes flutter very slightly." > /tmp/bc_exf.log 2>&1 &
$P animate select_consultant "$MOTION Its laser pointer sweeps slowly across the invisible chart." > /tmp/bc_cons.log 2>&1 &
$P animate select_stage_empty "Bring this empty hand-drawn theater stage alive as a calm seamless loop, keeping the artwork EXACTLY as drawn. Only the spotlight breathes very gently and dust motes drift slowly down through the beam; the curtains sway almost imperceptibly. Nothing else moves, no characters appear, camera completely static, flat 2D hand-drawn style preserved." > /tmp/bc_empty.log 2>&1 &

# the page-turn companion for the journal
$P variant journal_page_turn "Same log book, same garage, same palette and style as the reference — but caught MID PAGE TURN: one cream page is lifted and arcing across the middle of the frame, curving and slightly translucent where the light passes through it, its far edge sweeping toward the left; underneath it the next blank page is partly revealed. Motion is implied by the curve of the lifted page, not by blur. The page surfaces stay COMPLETELY BLANK — no ruling, no writing, no marks. Same dim out-of-focus garage around the outer edges." --ref journal_page/scene > /tmp/bc_turn.log 2>&1 &

wait
echo "BATCH C DONE"
