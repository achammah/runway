#!/bin/bash
# BATCH Q: the generative-library PILOT.
#
# Three taxonomy entries deliberately unlike anything the marks were built on:
# a suburban living room, a vast hangar, an airport gate. If crew marks,
# occluders and write_surfaces survive these, they will survive the 516.
#
# Each prompt is the taxonomy's own text PLUS the proposed INVARIANT clause, so
# this doubles as the experiment for the spec: generate with the clause, then
# MEASURE whether the model actually held the floor line. What it cannot hold is
# what has to be enforced after the fact.
cd "/Users/assem/Documents/Doc-Assem/Claude Code/runway/game" || exit 1
P="python3 tools/scene_pipeline.py"

INV="CAMERA AND GROUND (obey exactly): the camera is level at standing eye height, square to the back wall, no tilt and no perspective from above or below. The line where the back wall meets the floor runs straight across the picture at 62 percent of the image height. Everything below that line is open floor, and the bottom 12 percent of the picture is bare empty floor with nothing on it. Every object stands ON the floor with its base between 62 and 88 percent of the image height. Nothing hangs into the top 10 percent of the picture. Keep a clear unobstructed strip of floor across the middle of the room where people could stand."

$P generate pilot_livingroom "a wide establishing shot of the whole room of a suburban living room with a floral sofa, a laptop balanced on a knee. It contains sofa, side table, family photos, laptop. Lit by flat daylight. The place is lived in, ordinary, neither rich nor desperate. EMPTY OF PEOPLE. $INV" > /tmp/bq_living.log 2>&1 &

$P generate pilot_hangar "a wide establishing shot of the whole room of a vast disused aircraft hangar with a tiny desk island in the middle. It contains hangar doors, girders, lone desk cluster, forklift. Lit by flat daylight. The place is lived in, ordinary, neither rich nor desperate. EMPTY OF PEOPLE. $INV" > /tmp/bq_hangar.log 2>&1 &

$P generate pilot_airport "a wide establishing shot of the whole room of an airport gate at an unsociable hour. It contains gate seating, departure screen, window. Lit by warm lamplight against dark windows. The place is lived in, ordinary, neither rich nor desperate. EMPTY OF PEOPLE. $INV" > /tmp/bq_airport.log 2>&1 &

wait
echo "BATCH Q DONE"
python3 tools/scene_pipeline.py verify | tail -1
