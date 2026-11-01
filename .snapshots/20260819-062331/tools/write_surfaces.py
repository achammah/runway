#!/usr/bin/env python3
"""Declare a scene's writable faces in its layout.json.

Per DIEGETIC STATE in the brief, x,y is the top-left of the WRITABLE FACE, not
of the object: the whiteboard's frame, the clipboard's clip and the sticky's
curled corner are all excluded, then the face is inset ~8% so writing never
touches a drawn edge. So rects here are given as the FACE bounds measured off
the art, and the 8% inset is applied here rather than by hand.

`rot` is read off the object's own lean in the art — a surface that leans with
text that does not is worse than neither. `lines` is 2 by default; only a big
whiteboard holds 3.

Merges into layout.json, which also carries crew marks and occluders.
"""
import json, os, sys

GAME = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
# The brief specifies ~8%, sized for where TEXT may safely sit. But clear_surfaces
# floods this same rect, so an 8% inset provably leaves the outer 8% of the old
# scribble behind as a ring — the first office pass left a column of dots, a
# sticky note and several dashes stranded between the fill and the frame. 3% still
# keeps writing clear of the drawn edge while the flood reaches the whole face.
INSET = 0.03


def declare(room, faces):
    """faces: name -> (face_x, face_y, face_w, face_h, rot, lines[, align])"""
    path = f"{GAME}/assets/scenes/{room}/layout.json"
    layout = json.load(open(path)) if os.path.exists(path) else {}
    out = {}
    for name, spec in faces.items():
        x, y, w, h, rot, lines = spec[:6]
        dx, dy = round(w * INSET), round(h * INSET)
        row = {"x": round(x + dx), "y": round(y + dy),
               "w": round(w - 2 * dx), "h": round(h - 2 * dy),
               "rot": rot, "lines": lines}
        if len(spec) > 6:
            row["align"] = spec[6]
        out[name] = row
    layout["write_surfaces"] = out
    json.dump(layout, open(path, "w"), indent=1)
    print(f"{room}: {len(out)} write_surfaces -> " + ", ".join(out))


# Face bounds measured off each scene's own art at 1536x1024.
ROOMS = {
    # the only genuinely writable face in this room; flat-on, so no lean
    "stage_office": {"whiteboard": (425, 355, 260, 150, 0.0, 3, "center")},
    # the kanban board face; clearing it takes the pinned cards with it, which is
    # the point — an empty board at a young startup reads fine and it is the only
    # face in this room big enough to hold readable handwriting
    "stage_coworking": {"wallchart": (816, 318, 296, 180, 0.0, 3, "center")},
    # the framed chart leans: its top edge rises to the right at about -0.084 rad,
    # measured off the art, so writing has to lean with it
    "stage_hq": {"wallchart": (1222, 225, 175, 145, -0.08, 2, "center")},
    # flip-chart on its stand, flat-on; smaller than the office board so 2 lines
    "stage_garage": {"whiteboard": (902, 327, 145, 197, 0.0, 3, "center")},
    # this room has no large board at all — its wall is a mosaic of small pinned
    # sheets, so these are the two biggest faces it actually owns. Both are
    # undersized for two lines of 26px type; flagged to the coordinator.
    "stage_floor": {"wallchart": (547, 255, 55, 47, 0.0, 2, "center"),
                    "ledger":    (492, 314, 41, 53, 0.0, 2, "center")},
}

if __name__ == "__main__":
    for room in (sys.argv[1:] or ROOMS):
        declare(room, ROOMS[room])
