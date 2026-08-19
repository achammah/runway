#!/usr/bin/env python3
"""Write founder/crew marks and foreground occluders into a room's layout.json.

The cast is not painted into the room — it composites on top as sprites, so each
room needs anchors. From the 60 Seconds! crew shots: the survivors sit in a
shallow ARC at visibly different depths, they overlap casually, and every one of
them is partly hidden behind a table or crate. So a mark carries its own scale
(further back = smaller, never uniform), and each room declares foreground
pieces that draw OVER the cast.

The pair shot settles the subset question: with two survivors instead of four,
the remaining two keep their exact positions and the empty stools simply stay.
Marks are FIXED — a missing cofounder leaves a gap, the row never re-centres.

SceneRoom reads layout.json as {name: {x, y, w, h, placed}} and positions a
TextureRect at x,y sized w,h; a row whose <name>.png is missing is skipped, so
marks sit here harmlessly until sprites land. x,y is the sprite box top-left,
derived from the foot anchor, because the cast stands ON the mark.
"""
import json, os, sys

GAME = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NOMINAL_W, NOMINAL_H = 200, 300   # a creature at scale 1.0 on the 1536x1024 canvas


def marks_for(room, entries, occluders=(), canvas=(1536, 1024)):
    """entries: (name, foot_x, foot_y, scale). occluders: (name, x, y, w, h)."""
    path = f"{GAME}/assets/scenes/{room}/layout.json"
    layout = {}
    if os.path.exists(path):
        layout = json.load(open(path))
    for name, fx, fy, scale in entries:
        w, h = round(NOMINAL_W * scale), round(NOMINAL_H * scale)
        layout[name] = {
            "x": round(fx - w / 2), "y": round(fy - h), "w": w, "h": h,
            "scale": round(scale, 3), "foot_x": round(fx), "foot_y": round(fy),
            "kind": "crew_mark", "placed": True,
        }
    for name, x, y, w, h in occluders:
        layout[name] = {
            "x": x, "y": y, "w": w, "h": h,
            "kind": "occluder", "z": "front", "placed": True,
            "note": "draws OVER the cast — the cast stands behind it",
        }
    json.dump(layout, open(path, "w"), indent=1)
    n_m = sum(1 for r in layout.values() if r.get("kind") == "crew_mark")
    n_o = sum(1 for r in layout.values() if r.get("kind") == "occluder")
    print(f"{room}: {n_m} marks, {n_o} occluders")


# Foot anchors read off each room's own art: crew stand on open floor, and the
# marks that sit behind a desk or crate are deliberately shallower so that piece
# cuts across the legs the way the reference shots do.
ROOMS = {
    "stage_garage": (
        # arc across the middle band; founder behind the desk, crew_4 behind the crate
        [("crew_1",       330, 706, 1.05),
         ("founder_mark", 700, 658, 0.99),
         ("crew_2",       520, 692, 1.02),
         ("crew_3",       960, 672, 0.96),
         ("crew_4",      1215, 666, 0.94)],
        [("occ_workbench", 102, 490, 393, 152),
         ("occ_desk",      546, 487, 307, 155),
         ("occ_crate",    1118, 589, 230, 112)],
    ),
    "stage_coworking": (
        # founder front-centre and closest; crew_1/crew_4 tucked behind the long desks
        [("crew_1",       222, 734, 1.06),
         ("founder_mark", 597, 768, 1.09),
         ("crew_2",       734, 666, 0.98),
         ("crew_3",       973, 649, 0.96),
         ("crew_4",      1297, 717, 1.03)],
        [("occ_desk_left",   17, 512, 427, 205),
         ("occ_booth",      452, 299, 171, 290),
         ("occ_column",     700, 205,  77, 384),
         ("occ_desk_right",1041, 512, 427, 205),
         ("occ_coffee",    1280, 469, 205,  94)],
    ),
    "stage_office": (
        # crew_4 stands behind the right-hand desk so it cuts across the legs
        [("crew_1",       300, 740, 1.05),
         ("founder_mark", 560, 705, 1.00),
         ("crew_2",       800, 685, 0.97),
         ("crew_3",      1060, 672, 0.95),
         ("crew_4",      1400, 662, 0.94)],
        [("occ_desk_left",  205, 563, 230, 120),
         ("occ_server",     789, 307, 150, 359),
         ("occ_cabinet",   1221, 504,  85, 145),
         ("occ_desk_right",1315, 546, 204, 137)],
    ),
    "stage_floor": (
        # the desk banks recede hard here, so the depth spread is the widest
        [("crew_1",       307, 666, 1.02),
         ("founder_mark", 597, 717, 1.06),
         ("crew_2",       802, 615, 0.95),
         ("crew_3",      1041, 597, 0.93),
         ("crew_4",      1297, 674, 1.01)],
        [("occ_desk_left",    0, 480, 341, 250),
         ("occ_foosball",   683, 427, 256, 136),
         ("occ_coffee",     990, 410, 145, 105),
         ("occ_desk_right",1195, 470, 341, 265)],
    ),
    # Moment stages the picker can return (bell, yc_demo_day). The crew stand UP
    # on the platform, so those marks are smaller — the platform is further from
    # camera than the floor in front of it.
    "stage_nasdaq": (
        [("crew_1",       400, 524, 0.92),
         ("founder_mark", 560, 520, 0.90),   # beside the bell, the one ringing it
         ("crew_2",       930, 520, 0.88),
         ("crew_3",      1090, 524, 0.90),
         ("crew_4",       250, 600, 1.00)],  # down on the floor, off the dais
        [("occ_dais", 285, 505, 944, 195)],
    ),
    "stage_yc": (
        [("founder_mark", 520, 578, 0.90),   # at the microphone
         ("crew_1",       700, 578, 0.88),
         ("crew_2",       880, 578, 0.88),
         ("crew_3",       330, 700, 1.05),   # in the seating, front of house
         ("crew_4",      1200, 700, 1.05)],
        [("occ_stage_front", 259, 575, 882, 145),
         ("occ_chairs_left",  60, 641, 420, 139),
         ("occ_chairs_right", 990, 641, 470, 139)],
    ),
    "stage_hq": (
        # crew_2 stands up on the little stage — the deepest, smallest mark
        [("crew_1",       256, 700, 1.04),
         ("founder_mark", 666, 734, 1.07),
         ("crew_2",       930, 529, 0.90),
         ("crew_3",      1178, 589, 0.94),
         ("crew_4",      1348, 572, 0.93)],
        [("occ_table",      34, 452, 495, 231),
         ("occ_stage",     777, 444, 333,  68),
         ("occ_low_table",1119, 500, 137,  51),
         ("occ_couch",    1194, 418, 171, 128)],
    ),
}

if __name__ == "__main__":
    for room in (sys.argv[1:] or ROOMS):
        e, o = ROOMS[room]
        marks_for(room, e, o)
