#!/usr/bin/env python3
"""Composite keyed cast sprites onto a room at its crew marks and save a preview.

This is the check that actually matters before producing 27 sprites: it proves
the key left no magenta halo, that the marks land the feet on the floor, and
that the per-mark scales read as depth rather than as random sizing.

  python3 tools/preview_cast.py stage_garage cast_hacker_fine
"""
import json, os, sys
from PIL import Image

GAME = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCENES = f"{GAME}/assets/scenes"
NOMINAL_H = 300   # a creature at scale 1.0 on the 1536x1024 canvas


def preview(room, sprite_id, out="/tmp/cast_preview.png"):
    bg = Image.open(f"{SCENES}/{room}/scene.png").convert("RGBA")
    bg = bg.resize((1536, 1024), Image.LANCZOS)
    sp = Image.open(f"{SCENES}/{sprite_id}/sprite.png").convert("RGBA")
    layout = json.load(open(f"{SCENES}/{room}/layout.json"))
    marks = {k: v for k, v in layout.items() if v.get("kind") == "crew_mark"}
    for name, m in marks.items():
        h = round(NOMINAL_H * m["scale"])
        w = round(sp.width * h / sp.height)
        s = sp.resize((w, h), Image.LANCZOS)
        # marks are foot anchors, and the sprite is cropped to content
        bg.alpha_composite(s, (round(m["foot_x"] - w / 2), round(m["foot_y"] - h)))
    bg.convert("RGB").save(out)
    return f"{room}: {len(marks)} marks previewed -> {out}"


if __name__ == "__main__":
    print(preview(sys.argv[1], sys.argv[2],
                  sys.argv[3] if len(sys.argv) > 3 else "/tmp/cast_preview.png"))
