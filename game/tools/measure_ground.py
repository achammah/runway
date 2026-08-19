#!/usr/bin/env python3
"""Measure where a scene's ground line actually sits, and how the marks relate to it.

The generative library makes one question decisive: can ONE set of crew marks,
occluders and write_surfaces be reused across 516 different backgrounds? Only if
the backgrounds agree on where the floor is. This measures whether they do.

The ground line is found as the strongest horizontal edge in the lower-middle of
the frame — the wall/floor junction is the longest uninterrupted horizontal in
almost any room. Reported as a fraction of image height so scenes of different
resolutions are comparable.

  python3 tools/measure_ground.py [id ...]
"""
import json, os, sys
from PIL import Image, ImageFilter

GAME = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCENES = f"{GAME}/assets/scenes"
W = 384  # working width


def ground_line(path):
    im = Image.open(path).convert("L")
    h = max(1, round(im.height * W / im.width))
    im = im.resize((W, h), Image.LANCZOS)
    # horizontal-edge response per row: |row - row_below| summed across x
    px = im.load()
    best, best_y = -1.0, 0
    lo, hi = int(h * 0.40), int(h * 0.92)
    for y in range(lo, hi):
        s = 0
        for x in range(0, W, 2):
            s += abs(px[x, y] - px[x, y + 1])
        if s > best:
            best, best_y = s, y
    return best_y / float(h), best / (W / 2)


def main():
    ids = sys.argv[1:] or sorted(
        d for d in os.listdir(SCENES)
        if d.startswith(("stage_", "pilot_")) and os.path.exists(f"{SCENES}/{d}/scene.png"))
    print(f"{'SCENE':<22}{'ground':>8}{'strength':>10}   feet (fraction of height)")
    for sid in ids:
        p = f"{SCENES}/{sid}/scene.png"
        if not os.path.exists(p):
            continue
        frac, strength = ground_line(p)
        feet = ""
        lp = f"{SCENES}/{sid}/layout.json"
        if os.path.exists(lp):
            lay = json.load(open(lp))
            ys = sorted(v["foot_y"] / 1024.0 for v in lay.values()
                        if isinstance(v, dict) and v.get("kind") == "crew_mark")
            if ys:
                feet = " ".join(f"{v:.3f}" for v in ys)
        print(f"{sid:<22}{frac:>8.3f}{strength:>10.1f}   {feet}")


if __name__ == "__main__":
    main()
