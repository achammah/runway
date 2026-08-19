#!/bin/bash
# BATCH F: last two single-rail micro-fixes. Everything else that passes the
# corrected audit goes straight to promote -> decompose -> place -> animate.
cd "/Users/assem/Documents/Doc-Assem/Claude Code/runway/game" || exit 1
P="python3 tools/scene_pipeline.py"

$P variant garage_steady_g2 "Same garage, same characters, same objects, same palette and style as the reference. ONE change only: the rightmost eighth of the image becomes plain empty wall in flat colour with absolutely nothing drawn on it — move the shelf and anything else that sits there inward, toward the middle of the picture. Everything else identical." --ref garage_steady_g/scene > /tmp/bf_gar.log 2>&1 &

$P variant garage_thriving_v5 "Same garage, same characters, same objects, same palette and style as the reference. ONE change only: the leftmost eighth of the image becomes plain empty wall in flat colour with absolutely nothing drawn on it — move whatever sits there inward, toward the middle of the picture. Everything else identical." --ref garage_thriving_v4/scene > /tmp/bf_gthr.log 2>&1 &

wait
echo "BATCH F DONE"
