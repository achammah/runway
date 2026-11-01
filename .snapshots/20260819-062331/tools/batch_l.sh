#!/bin/bash
# BATCH L: THE CAST — chroma-key sprites, generated FROM a room so the lighting
# and palette match what they stand in.
#
# Two technique notes:
#  - variant() skips the scene STYLE block when the prompt is JSON. That is what
#    we want: STYLE's "palette ONLY / paper cream" and its 3:2-game-background
#    composition rules are wrong for a single figure on magenta.
#  - the room goes in as --ref so the sprite inherits its light direction. The
#    marks put light upper-left, so the contact shadow is baked down-and-right.
#
# Moods: fine / burnt / gone. "gone" draws NO creature — the reference shows
# absence as what fills the space instead, so it is the abandoned prop alone.
cd "/Users/assem/Documents/Doc-Assem/Claude Code/runway/game" || exit 1
P="python3 tools/scene_pipeline.py"
REF="stage_garage/scene"

sprite () {  # $1 subject-line, $2 props, $3 posture, $4 extra-negative
cat <<JSON
{"task":"single character sprite for chroma-key compositing into a game scene",
 "subject":"$1",
 "posture":"$3",
 "character_law":{"body":"one solid ink-black bean-shaped blob, unbroken silhouette","top":"exactly one ink cowlick spike","eyes":"exactly two blank white ovals, the left slightly bigger, COMPLETELY blank — no pupils, no irises, no dots, no eyelids, no eyebrows","face":"no mouth, no nose, no ears","limbs":"thin black stick arms and legs","feet":"tiny cream-white sneakers, one lace untied","clothing":"NONE — no shirt, no hoodie, no jacket, no hat, no tie; the body is bare solid black"},
 "identifying_props":"$2",
 "framing":"one single figure, centred, whole body visible, filling about 78 percent of the frame height, nothing cropped",
 "contact_shadow":"a small soft dark-grey elliptical shadow directly under the feet, offset slightly down and to the RIGHT because the light comes from the upper left; the shadow touches the feet and fades out, and it sits ON the magenta",
 "background":"completely flat uniform pure magenta #FF00FF, absolutely empty — no floor, no wall, no gradient, no texture, no objects, no second figure",
 "style":"flat hand-drawn cartoon, wobbly felt-pen ink outlines, flat fills, no gradients, matching the reference image's line quality and lighting",
 "palette":["#1E1E1E","#E86A5C","#F4B942","#8FA582","#6E8CA0","#F2EAD3","#FFFFFF"],
 "negative":"no text, no lettering, no pupils, no mouth, no clothing, no background scenery, no second character, no drop shadow other than the contact shadow$4"}
JSON
}

FINE="standing upright and alert, slight forward lean, weight on both feet"
BURNT="visibly exhausted — slumped forward, shoulders sagging, head drooping, the cowlick spike itself bent over limp, arms hanging heavy"

$P variant cast_hacker_fine "$(sprite 'one creature, the HACKER founder' 'an open laptop held in both hands, its screen glowing faintly' "$FINE" '')" --ref $REF > /tmp/bl_hf.log 2>&1 &
$P variant cast_hacker_burnt "$(sprite 'one creature, the HACKER founder, burnt out' 'an open laptop held low and loose in one hand, screen dark' "$BURNT" '')" --ref $REF > /tmp/bl_hb.log 2>&1 &
$P variant cast_hacker_gone "$(sprite 'NO creature at all — only the abandoned belongings of someone who has left' 'a closed laptop lying flat on the ground with a cold coffee mug beside it, and a small settled puff of dust' 'nothing is standing; the objects rest on the ground' ', absolutely no creature, no blob, no figure, no body, no eyes, no sneakers, nobody')" --ref $REF > /tmp/bl_hg.log 2>&1 &

wait
echo "BATCH L PILOT DONE"
python3 tools/scene_pipeline.py verify | tail -1
