#!/bin/bash
# BATCH K: the five EMPTY era stages. The loop belongs to the room, not the cast:
# these are animated with ambient life only, and every character composites on
# top as a static sprite at a crew mark. So nobody may be drawn into them.
# They also become the --ref for every cast sprite, which is how a sprite
# inherits that room's lighting instead of being generated in a vacuum.
cd "/Users/assem/Documents/Doc-Assem/Claude Code/runway/game" || exit 1
P="python3 tools/scene_pipeline.py"

# Repeated hard, because "empty" is the one thing an image model loves to ignore.
EMPTY="THE ROOM IS COMPLETELY EMPTY OF PEOPLE. There is not a single creature, person, figure, character, face or silhouette anywhere in this picture. Nobody is in the room. Do not draw anyone. The chairs are unoccupied and the desks are unattended."
MARKS="Leave four separate patches of clear open floor in the middle band of the room, spread evenly from left to right, where someone could stand — keep those patches unobstructed. The light falls from the upper left, so every object casts a soft shadow down and to the right."

$P generate stage_garage "The inside of a suburban garage used as a startup workspace, seen straight on from across the room. A wooden workbench with a pegboard of tools above it, a desk with a closed laptop and a chair, a whiteboard on a stand with faint ink diagrams, a wooden crate that serves as the money table, a hanging bulb, a houseplant, a stack of cardboard boxes and a closed roller door. $EMPTY $MARKS" > /tmp/bk_garage.log 2>&1 &

$P generate stage_coworking "The inside of a shared coworking hall, seen straight on. Two long communal desks with chairs and closed laptops, a soundproof phone booth, an exposed brick column, a kanban board of small paper cards on the wall, pendant lamps, a coffee station and potted plants. $EMPTY $MARKS" > /tmp/bk_coworking.log 2>&1 &

$P generate stage_office "The inside of a small startup's first real office, seen straight on. A glass front door, two desks with monitors and chairs, a cupboard of server equipment with a soft glow, a whiteboard covered in faint ink boxes and arrows, a shelf, a low cabinet for the money and a big potted plant. $EMPTY $MARKS" > /tmp/bk_office.log 2>&1 &

$P generate stage_floor "The inside of a whole office floor of a growing company, seen straight on. Two rows of desks with monitors and empty chairs, a foosball table, a coffee station, a wall covered in small pinned paper charts, a bookshelf and potted plants. $EMPTY $MARKS" > /tmp/bk_floor.log 2>&1 &

$P generate stage_hq "The inside of a company headquarters, seen straight on. A wall of tall windows showing a flat city skyline, a long meeting table with empty chairs, a small low stage with a microphone stand, a couch and a rug, a framed chart on the wall, a bookshelf and big potted plants. $EMPTY $MARKS" > /tmp/bk_hq.log 2>&1 &

wait
echo "BATCH K DONE"
python3 tools/scene_pipeline.py verify | tail -1
