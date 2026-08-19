#!/bin/bash
# BATCH P: blank writable surfaces as chroma-key sprites (BUG-11 / A-05).
#
# clear_surfaces can blank a drawn surface but cannot create one that was never
# drawn, and the stages predate the WRITING SURFACES clause — their other paper
# is decoration-scale (kanban cards 23x27px) and cannot hold 26px type. So these
# get composited into scene.png and all 48 frames at static wall/desk sites.
#
# They must arrive COMPLETELY BLANK: the whole point is a face to write on.
cd "/Users/assem/Documents/Doc-Assem/Claude Code/runway/game" || exit 1

surface () {  # 1 object, 2 shape-notes
cat <<JSON
{"task":"a single blank object for chroma-key compositing into a game scene",
 "subject":"$1",
 "surface":"its writing face is COMPLETELY BLANK — no letters, no numbers, no words, no drawn chart, no lines, no ruling, no scribble, no logo, no marks of any kind. It is an empty unused surface, pale and clean.",
 "shape":"$2",
 "orientation":"seen almost flat-on, square to the viewer, so writing would sit level on it",
 "framing":"one single object, centred, whole object visible, filling about 85 percent of the frame, nothing cropped",
 "shading":"lit from the upper left with a small soft neutral-grey shadow down and to the right; plain neutral grey, never tinted toward the background colour",
 "background":"completely flat uniform pure magenta #FF00FF, absolutely empty — no wall, no floor, no gradient, no texture, no other objects",
 "style":"flat hand-drawn cartoon, wobbly felt-pen ink outlines, flat fills, no gradients, matching the reference image's line quality",
 "palette":["#1E1E1E","#E86A5C","#F4B942","#8FA582","#6E8CA0","#F2EAD3","#FFFFFF"],
 "negative":"no text, no lettering, no numbers, no handwriting, no printed chart, no graph, no ruled lines, no creature, no person, no hand, no second object"}
JSON
}

P="python3 tools/scene_pipeline.py"
R="--ref stage_garage/scene"

$P variant surf_ledger "$(surface 'a clipboard holding one blank sheet of pale paper, lying flat' 'a wooden or dark clip at the top, the blank sheet below it filling most of the board; landscape-ish')" $R > /tmp/bp_ledger.log 2>&1 &
$P variant surf_sticky "$(surface 'a cluster of three blank square sticky notes overlapping slightly' 'each note a plain flat square of pale yellow, sage and coral, one corner of one note curled; the cluster is wider than it is tall')" $R > /tmp/bp_sticky.log 2>&1 &
$P variant surf_wallchart "$(surface 'a single blank sheet of pale paper pinned to nothing, as if on a wall' 'a plain rectangular sheet with a small pin or tape tab at the top centre; portrait, slightly taller than wide')" $R > /tmp/bp_chart.log 2>&1 &
$P variant surf_inventory "$(surface 'a large empty corkboard in a dark wooden frame, for listing inventory' 'a big plain cork-coloured face inside a simple dark frame, clearly TALLER than it is wide, portrait orientation, large enough to list many items')" $R > /tmp/bp_inv.log 2>&1 &

wait
echo "BATCH P DONE"
python3 tools/chroma_key.py surf_ledger surf_sticky surf_wallchart surf_inventory
python3 tools/scene_pipeline.py verify | tail -1
