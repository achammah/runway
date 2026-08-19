#!/bin/bash
# BATCH N: the rest of the CAST. 9 types x 3 moods = 27; the hacker's three came
# in the pilot, so 24 here. Run in waves of 6 — a dozen concurrent multi-megabyte
# uploads is what tripped the CLI timeout before.
#
# "gone" draws NO creature. The sparse reference shows absence as what fills the
# space instead, so it is the abandoned prop alone, on the ground.
cd "/Users/assem/Documents/Doc-Assem/Claude Code/runway/game" || exit 1
P="python3 tools/scene_pipeline.py"
REF="stage_garage/scene"

FINE="standing upright and alert, slight forward lean, weight on both feet"
BURNT="visibly exhausted — slumped forward, shoulders sagging, head drooping, the cowlick spike itself bent over limp, arms hanging heavy"
NOBODY="nothing is standing; the objects rest on the ground"

sprite () {  # 1 subject, 2 props, 3 posture, 4 extra-negative
cat <<JSON
{"task":"single character sprite for chroma-key compositing into a game scene",
 "subject":"$1",
 "posture":"$3",
 "character_law":{"body":"one solid ink-black bean-shaped blob, unbroken silhouette","top":"exactly one ink cowlick spike","eyes":"exactly two blank white ovals, the left slightly bigger, COMPLETELY blank — no pupils, no irises, no dots, no eyelids, no eyebrows","face":"no mouth, no nose, no ears","limbs":"thin black stick arms and legs","feet":"tiny cream-white sneakers, one lace untied","clothing":"NONE — no shirt, no hoodie, no jacket, no hat; the body is bare solid black. Held or worn PROPS are allowed, clothing is not"},
 "identifying_props":"$2",
 "framing":"one single figure, centred, whole body visible, filling about 78 percent of the frame height, nothing cropped",
 "contact_shadow":"a small soft dark-grey elliptical shadow directly under the feet, offset slightly down and to the RIGHT because the light comes from the upper left; it must be plain neutral grey, never tinted toward the background colour",
 "background":"completely flat uniform pure magenta #FF00FF, absolutely empty — no floor, no wall, no gradient, no texture, no objects, no second figure",
 "style":"flat hand-drawn cartoon, wobbly felt-pen ink outlines, flat fills, no gradients, matching the reference image's line quality and lighting",
 "palette":["#1E1E1E","#E86A5C","#F4B942","#8FA582","#6E8CA0","#F2EAD3","#FFFFFF"],
 "negative":"no text, no lettering, no pupils, no mouth, no clothing, no background scenery, no second character$4"}
JSON
}

NOONE=', absolutely no creature, no blob, no figure, no body, no eyes, no sneakers, nobody'

# id-prefix : label : prop-fine : prop-gone
CAST=(
"founder_hustler|the HUSTLER founder|a phone held to the side of the head as if mid-call, and a takeaway coffee cup in the other hand|a dropped phone lying face down on the ground beside a spilled takeaway coffee cup"
"founder_pm|the EX-FAANG PM founder|an identity badge on a lanyard around the neck and a small fan of yellow sticky notes held in one hand|an abandoned lanyard badge lying on the ground with a few yellow sticky notes scattered around it"
"founder_consultant|the EX-CONSULTANT founder|a small wheeled roller suitcase held by its handle and a laser pointer in the other hand|an upright roller suitcase left standing alone on the ground with a laser pointer lying beside it"
"cofd_sales|the SALES cofounder|a telephone headset worn over the head and a signed paper contract held out in one hand|an abandoned telephone headset on the ground beside a curled signed contract"
"cofd_business|the BUSINESS cofounder|an open laptop held in both hands showing a small rising line chart, and a tiny tie|a closed laptop on the ground with a tiny tie draped over it"
"cofd_tech|the TECH cofounder|a soldering iron held in one hand with a thin wisp of smoke, and a mug in the other hand|a cold soldering iron resting on the ground next to an abandoned mug"
"cofd_hustler|the HUSTLER cofounder|a phone held to the side of the head as if mid-call, and a takeaway coffee cup in the other hand|a dropped phone lying face down on the ground beside a spilled takeaway coffee cup"
"cofd_idea|THE IDEA FRIEND cofounder, who is visibly doing nothing useful|completely empty hands, a fat beanbag to slouch in and a tall smoothie with a straw|an empty sagging beanbag on the ground with a half-finished smoothie left beside it"
)

wave=0
for row in "${CAST[@]}"; do
  IFS='|' read -r id label pfine pgone <<< "$row"
  $P variant "cast_${id}_fine"  "$(sprite "one creature, $label" "$pfine" "$FINE" '')"  --ref $REF > "/tmp/bn_${id}_f.log" 2>&1 &
  $P variant "cast_${id}_burnt" "$(sprite "one creature, $label, burnt out" "$pfine" "$BURNT" '')" --ref $REF > "/tmp/bn_${id}_b.log" 2>&1 &
  $P variant "cast_${id}_gone"  "$(sprite 'NO creature at all — only the abandoned belongings of someone who has left' "$pgone" "$NOBODY" "$NOONE")" --ref $REF > "/tmp/bn_${id}_g.log" 2>&1 &
  wave=$((wave+1))
  if [ $((wave % 2)) -eq 0 ]; then wait; fi
done
wait
echo "BATCH N DONE"
python3 tools/chroma_key.py --all
python3 tools/scene_pipeline.py verify | tail -1
