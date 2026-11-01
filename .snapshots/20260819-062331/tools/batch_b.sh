#!/bin/bash
# BATCH B: select stages re-framed (camera pulled back, CTA zone cleared) + garage rail fixes
cd "/Users/assem/Documents/Doc-Assem/Claude Code/runway/game" || exit 1
P="python3 tools/scene_pipeline.py"

FRAME="IMPORTANT FRAMING CHANGE: pull the camera far back so the character is SMALL in the frame and occupies only the middle band, roughly from 25 percent to 72 percent of the image height, centered horizontally. The spotlight cone and its elliptical floor pool must END well above the bottom of the image: the lowest quarter of the picture is plain dark empty stage floor, no light pool, no props, no shadows, no part of the character. The top tenth is plain dark empty air above the cone. Keep the far left and far right edges as plain dark curtain with no detail."
KEEP="Keep the character design EXACTLY as in the reference: small solid ink-black bean-shaped body, one ink cowlick spike, two blank white oval eyes with the left slightly bigger, thin black stick limbs, tiny cream sneakers. Same palette, same flat hand-drawn style, no text anywhere."

$P variant select_hacker_v2     "Same dark theater stage and same character doing the same thing as the reference. $FRAME $KEEP" --ref select_hacker/scene     > /tmp/bb_hack.log 2>&1 &
$P variant select_hustler_v2    "Same dark theater stage and same character doing the same thing as the reference. $FRAME $KEEP" --ref select_hustler/scene    > /tmp/bb_hust.log 2>&1 &
$P variant select_dropout_v2    "Same dark theater stage and same character doing the same thing as the reference. $FRAME $KEEP" --ref select_dropout/scene    > /tmp/bb_drop.log 2>&1 &
$P variant select_exfaang_v2    "Same dark theater stage and same character doing the same thing as the reference. $FRAME $KEEP" --ref select_exfaang/scene    > /tmp/bb_exf.log 2>&1 &
$P variant select_consultant_v2 "Same dark theater stage and same character doing the same thing as the reference. $FRAME $KEEP" --ref select_consultant/scene > /tmp/bb_cons.log 2>&1 &

# empty ceremony stage (no character) — same family
$P generate select_stage_empty "An EMPTY dark theater stage for a video game menu screen: deep muted-blue night background, heavy dark curtains hanging at the far left and far right edges, one soft pale-cream spotlight cone coming down from above and landing as a gentle elliptical pool of light on the dark stage floor in the middle of the frame. Nobody is standing in it. A few tiny dust motes drift in the beam. The spotlight pool ends well above the bottom of the image; the lowest quarter is plain dark empty stage floor and the top tenth is plain dark empty air. No characters, no props, no text." > /tmp/bb_empty.log 2>&1 &

# garage rail fixes
$P variant garage_v3 "Same room, same characters, same objects, same palette and style as the reference. Only fix the framing: clear the rightmost eighth of the image to plain calm wall with nothing on it, and keep the leftmost eighth plain too. Everything important stays in the middle band. Nothing touches the frame edges." --ref garage_v2/scene > /tmp/bb_g3.log 2>&1 &
$P variant garage_night_solo_v3 "Same room, same character, same objects, same palette and style as the reference. Only fix the framing: clear the leftmost eighth AND the rightmost eighth of the image to plain calm wall with nothing on it, and keep the upper-left corner low-detail for a small label plate. Everything important stays in the middle band. Nothing touches the frame edges." --ref garage_night_solo_v2/scene > /tmp/bb_gn3.log 2>&1 &

wait
echo "BATCH B DONE"
