#!/bin/bash
# BATCH J: the CAST — 4 cofounder types as chroma-key sprites.
# Technique note: variant() skips the scene STYLE block when the prompt is JSON,
# which is what we want — STYLE's palette and 3:2-game-background composition
# rules are wrong for a sprite on magenta. The character law is carried in the
# JSON itself, and select_exfaang (standing, verified on-model) is the visual
# anchor so all four share one body.
cd "/Users/assem/Documents/Doc-Assem/Claude Code/runway/game" || exit 1
P="python3 tools/scene_pipeline.py"

sprite () {  # $1 = id, $2 = prop description
cat <<JSON
{"task":"single character sprite for chroma-key compositing",
 "subject":"one small creature, standing, full body, facing the viewer, three-quarter angle, slight forward lean",
 "character_law":{"body":"a solid ink-black bean-shaped blob, unbroken silhouette","top":"exactly one ink cowlick spike","eyes":"exactly two blank white ovals, the left slightly bigger, COMPLETELY blank with no pupils, no irises, no dots, no eyelids, no eyebrows","face":"no mouth, no nose, no ears","limbs":"thin black stick arms and legs","feet":"tiny cream-white sneakers, one lace untied","clothing":"none whatsoever — no shirt, no hoodie, no jacket, no hat, no tie"},
 "identifying_props":"$2",
 "framing":"the creature is centred and fills about 78 percent of the frame height, whole body visible, nothing cropped",
 "background":"completely flat uniform pure magenta #FF00FF, absolutely empty, no shadow, no floor, no gradient, no texture, no objects",
 "style":"flat hand-drawn cartoon, wobbly felt-pen ink outlines, flat fills, no gradients",
 "palette":["#1E1E1E","#E86A5C","#F4B942","#8FA582","#6E8CA0","#F2EAD3","#FFFFFF"],
 "negative":"no text, no lettering, no pupils, no mouth, no clothing, no drop shadow, no background scenery, no second character"}
JSON
}

$P variant cast_sales_fine    "$(sprite cast_sales    'a telephone headset worn over the head and a signed paper contract held in one hand')" --ref select_exfaang/scene > /tmp/bj_sales.log 2>&1 &
$P variant cast_business_fine "$(sprite cast_business 'an open laptop held in both hands showing a small rising line chart, and a tiny tie')" --ref select_exfaang/scene > /tmp/bj_biz.log 2>&1 &
$P variant cast_tech_fine     "$(sprite cast_tech     'a soldering iron held in one hand with a thin wisp of smoke, and a mug in the other hand')" --ref select_exfaang/scene > /tmp/bj_tech.log 2>&1 &
$P variant cast_hustler_fine  "$(sprite cast_hustler  'a phone held to the side of the head as if on a call, and a takeaway coffee cup in the other hand')" --ref select_exfaang/scene > /tmp/bj_hustler.log 2>&1 &

wait
echo "BATCH J DONE"
