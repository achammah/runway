#!/usr/bin/env python3
"""Derive crew marks for ANY background by measuring it (the 516-library enabler).

Hand-authoring marks worked for seven stages. It does not scale to 516, and the
pilot proved it cannot simply be prompted away either: three backgrounds
generated with an explicit "the floor line sits at 62 percent of image height"
came back at 0.516, 0.621 and 0.648. One obeyed. The seven hand-built stages
span 0.449-0.609 — 164px on a 1024 canvas.

So the invariant is NOT a fixed set of coordinates. It is this RECIPE:

  1. measure the ground line (the wall/floor junction)
  2. find the widest unobstructed span of floor below it
  3. lay five marks across that span in a shallow arc
  4. scale each mark by its depth: a figure standing further back is smaller

Everything the marks need is then a property the background actually has, rather
than a property we hoped it would have.

  python3 tools/auto_marks.py pilot_hangar [...]
"""
import json, os, sys
from PIL import Image, ImageFilter, ImageStat

GAME = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCENES = f"{GAME}/assets/scenes"
CW, CH = 1536, 1024
NOMINAL_H = 300          # a creature at scale 1.0
BACK_SCALE, FRONT_SCALE = 0.82, 1.18
BOTTOM_CALM = 0.874      # UI safe zone: below this must stay clear


def _prep(sid):
    im = Image.open(f"{SCENES}/{sid}/scene.png").convert("RGB").resize((CW, CH), Image.LANCZOS)
    g = im.convert("L")
    return im, g


def ground_line(g):
    """Strongest horizontal edge in the lower middle = the wall/floor junction."""
    w = 384
    h = max(1, round(g.height * w / g.width))
    s = g.resize((w, h), Image.LANCZOS)
    px = s.load()
    best, best_y = -1.0, int(h * 0.6)
    for y in range(int(h * 0.40), int(h * 0.92)):
        acc = 0
        for x in range(0, w, 2):
            acc += abs(px[x, y] - px[x, y + 1])
        if acc > best:
            best, best_y = acc, y
    return best_y / float(h)


def free_columns(g, y0, y1, cols=48):
    """Edge energy per vertical slice of the floor band — low means standable."""
    band = g.crop((0, int(y0 * CH), CW, int(y1 * CH))).filter(ImageFilter.FIND_EDGES)
    out = []
    step = CW // cols
    for i in range(cols):
        c = band.crop((i * step, 0, (i + 1) * step, band.height))
        out.append(ImageStat.Stat(c).mean[0])
    return out


def derive(sid, n=5):
    im, g = _prep(sid)
    ground = ground_line(g)
    # stand between just behind the ground line and the calm bottom band
    y_back = min(0.86, ground + 0.03)
    y_front = min(BOTTOM_CALM - 0.01, max(y_back + 0.06, ground + 0.20))
    energy = free_columns(g, y_back, y_front)
    step = CW // len(energy)

    # pick n columns that are quiet and spread out
    order = sorted(range(len(energy)), key=lambda i: energy[i])
    picked = []
    min_gap = max(2, len(energy) // (n + 2))
    for i in order:
        if all(abs(i - j) >= min_gap for j in picked):
            picked.append(i)
        if len(picked) == n:
            break
    picked.sort()
    if len(picked) < n:                       # very cluttered floor: fall back to even spread
        picked = [int((k + 1) * len(energy) / (n + 1)) for k in range(n)]

    # shallow arc: the middle marks sit slightly further back than the ends
    layout_path = f"{SCENES}/{sid}/layout.json"
    layout = json.load(open(layout_path)) if os.path.exists(layout_path) else {}
    names = ["crew_1", "crew_2", "founder_mark", "crew_3", "crew_4"][:n]
    for k, (name, ci) in enumerate(zip(names, picked)):
        t = abs(k - (n - 1) / 2.0) / max(1e-6, (n - 1) / 2.0)   # 0 centre .. 1 edges
        fy = y_back + (y_front - y_back) * (0.30 + 0.70 * t)     # edges come forward
        scale = BACK_SCALE + (FRONT_SCALE - BACK_SCALE) * ((fy - y_back) / max(1e-6, y_front - y_back))
        fx = (ci + 0.5) * step
        h = round(NOMINAL_H * scale)
        w = round(200 * scale)
        layout[name] = {"x": round(fx - w / 2), "y": round(fy * CH - h), "w": w, "h": h,
                        "scale": round(scale, 3), "foot_x": round(fx), "foot_y": round(fy * CH),
                        "kind": "crew_mark", "placed": True, "derived": True}
    layout["ground_line"] = {"y": round(ground * CH), "fraction": round(ground, 4),
                             "kind": "measurement",
                             "note": "wall/floor junction, measured not assumed"}
    json.dump(layout, open(layout_path, "w"), indent=1)
    print(f"{sid}: ground {ground:.3f} band {y_back:.3f}-{y_front:.3f} -> {n} marks "
          f"scale {min(layout[nm]['scale'] for nm in names):.2f}-{max(layout[nm]['scale'] for nm in names):.2f}")


if __name__ == "__main__":
    for s in sys.argv[1:]:
        derive(s)
