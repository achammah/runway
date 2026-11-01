#!/bin/bash
# BATCH A: garage quartet safe-zone re-render + 6 select-stage scenes (parallel)
cd "/Users/assem/Documents/Doc-Assem/Claude Code/runway/game" || exit 1
P="python3 tools/scene_pipeline.py"

RECOMPOSE="Same scene, same characters, same objects, same palette and same hand-drawn style — only recompose the framing: make the top tenth of the image plain calm wall or sky with nothing important in it, make the bottom seventh plain calm floor, and keep the center-bottom area (middle third horizontally, lowest quarter vertically) completely empty low-contrast ground. Move every character and key object into the middle band of the image and push the busiest detail toward the left and right thirds. Leave a calm low-detail patch in the upper-left corner. Nothing may touch or cross the outer margin of the frame."

STAGE="A dark theater stage for a video game character-select screen: deep muted-blue night background, heavy dark curtains hanging at the far left and far right edges, one bright pale-cream spotlight cone coming down from above and landing as a soft elliptical pool of light on the dark stage floor. The floor pool sits in the lower-middle of the frame and the character stands in it at mid-height."
KEEP="Keep the reference character design EXACTLY: a small solid ink-black bean-shaped body, one ink cowlick spike on top, two blank white oval eyes with the left slightly bigger, thin black stick limbs, tiny cream sneakers. No text anywhere. Nothing else in the frame besides the stage, curtains, spotlight and this one character."

# --- garage quartet re-render ---
$P variant garage_v2          "$RECOMPOSE" --ref garage/scene          > /tmp/ba_garage.log 2>&1 &
$P variant garage_starving_v2 "$RECOMPOSE" --ref garage_starving/scene > /tmp/ba_gstarv.log 2>&1 &
$P variant garage_thriving_v2 "$RECOMPOSE" --ref garage_thriving/scene > /tmp/ba_gthriv.log 2>&1 &
$P variant garage_night_solo_v2 "$RECOMPOSE" --ref garage_night_solo/scene > /tmp/ba_gnight.log 2>&1 &

# --- select stages ---
$P variant select_hacker "$STAGE The character sits cross-legged in the spotlight pool typing on an open muted-blue laptop balanced on its lap, with three sunny-yellow energy drink cans stacked beside it. $KEEP" --ref _refs/hacker > /tmp/ba_shack.log 2>&1 &
$P variant select_hustler "$STAGE The character stands in the spotlight pool mid-stride wearing a single coral necktie, pressing one muted-blue phone to the side of its head while holding a second phone out at arm's length. $KEEP" --ref _refs/hustler > /tmp/ba_shust.log 2>&1 &
$P variant select_dropout "$STAGE The character stands in the spotlight pool with one foot resting on a small coral skateboard, wearing a backwards sage-green cap with its cowlick poking through and a sunny-yellow backpack with a rolled white diploma sticking out. $KEEP" --ref _refs/dropout > /tmp/ba_sdrop.log 2>&1 &
$P variant select_exfaang "$STAGE The character stands perfectly straight in the spotlight pool wearing a white lanyard with a small muted-blue badge, holding a cream coffee cup in one hand and pressing a sunny-yellow sticky note into the air with the other, where a tidy small grid of yellow sticky notes floats. $KEEP" --ref _refs/exfaang > /tmp/ba_sexf.log 2>&1 &
$P variant select_consultant "$STAGE The character stands in the spotlight pool wearing a muted-blue blazer, holding up a laser pointer toward an invisible chart with one hand and a sage-green binder under the other arm, a tiny cream roller suitcase parked at its side. $KEEP" --ref _refs/consultant > /tmp/ba_scons.log 2>&1 &

wait
echo "BATCH A DONE"
