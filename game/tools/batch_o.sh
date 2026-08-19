#!/bin/bash
# BATCH O: re-do the three "gone" sprites that drew a person anyway.
# cofd_hustler drew a whole sprawled creature; cofd_business and founder_hustler
# drew ghosted figures whose bodies keyed out into scattered parts. The phrase
# "the abandoned belongings of someone who has left" keeps summoning the someone,
# so this prompt never mentions a person at all — it is a still life of objects.
cd "/Users/assem/Documents/Doc-Assem/Claude Code/runway/game" || exit 1

still () {
cat <<JSON
{"task":"a still life of a few objects lying on the ground, for chroma-key compositing",
 "subject":"ONLY INANIMATE OBJECTS. This image contains no living thing of any kind.",
 "objects":"$1",
 "arrangement":"the objects rest on bare ground, grouped close together, seen from slightly above, each casting a small soft neutral-grey shadow down and to the right",
 "background":"completely flat uniform pure magenta #FF00FF, absolutely empty — no floor, no wall, no gradient, no texture",
 "style":"flat hand-drawn cartoon, wobbly felt-pen ink outlines, flat fills, no gradients",
 "palette":["#1E1E1E","#E86A5C","#F4B942","#8FA582","#6E8CA0","#F2EAD3","#FFFFFF"],
 "negative":"NO creature, NO blob, NO character, NO person, NO figure, NO body, NO silhouette, NO ghost, NO outline of a person, NO eyes, NO cowlick, NO limbs, NO arms, NO legs, NO shoes, NO sneakers, NO face, no text, no lettering"}
JSON
}

PHONE='a phone lying face down on bare ground beside a tipped-over takeaway coffee cup with a small brown spill'

python3 tools/scene_pipeline.py variant cast_cofd_business_gone \
  "$(still 'a closed grey laptop lying flat on bare ground with a tiny coral tie draped across it')" \
  --ref stage_garage/scene > /tmp/bo_biz.log 2>&1 &
python3 tools/scene_pipeline.py variant cast_cofd_hustler_gone \
  "$(still "$PHONE")" --ref stage_garage/scene > /tmp/bo_ch.log 2>&1 &
python3 tools/scene_pipeline.py variant cast_founder_hustler_gone \
  "$(still "$PHONE")" --ref stage_garage/scene > /tmp/bo_fh.log 2>&1 &
wait
python3 tools/chroma_key.py cast_cofd_business_gone cast_cofd_hustler_gone cast_founder_hustler_gone
python3 tools/scene_pipeline.py verify | tail -1
