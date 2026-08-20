#!/usr/bin/env python3
"""Programmatic scene-element detection v2 — the $0 animation path.

v1 searched blind by colour and drowned in false positives (59 "plants" = the
wooden bench). v2 exploits two things this project controls:

1. DOMINANCE, not distance: a plant is where GREEN beats red and blue; a lamp bulb
   is warm (r,g both beat blue) AND small AND round AND hanging high; a window is
   where BLUE beats red. The wooden bench beats none of these tests.
2. THE MANIFEST KNOWS THE ROOM. Every scene's generation prompt named its objects
   ("workbench, pegboard, whiteboard, crate, garage door"). Detection only runs for
   classes the scene claims, and keeps the BEST-K candidates per class, K from the
   object list. A detector that knows what it is looking for and how many cannot
   hallucinate a forest onto a bench.

    python3 tools/detect_elements.py <image> [--objects "a lamp, a plant"] [--overlay out.png]
"""
import sys, json
from PIL import Image, ImageDraw

def _components(sm, test, band, w, h, scale):
    px = sm.load()
    mask = [[test(*px[x, y]) for y in range(h)] for x in range(w)]
    seen = [[False] * h for _ in range(w)]
    comps = []
    y0b, y1b = int(band[0] * h), int(band[1] * h)
    for y in range(y0b, y1b):
        for x in range(w):
            if mask[x][y] and not seen[x][y]:
                st = [(x, y)]; seen[x][y] = True; cells = []
                while st:
                    cx, cy = st.pop(); cells.append((cx, cy))
                    for dx, dy in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(-1,-1),(1,-1),(-1,1)):
                        nx, ny = cx + dx, cy + dy
                        if 0 <= nx < w and 0 <= ny < h and mask[nx][ny] and not seen[nx][ny]:
                            seen[nx][ny] = True; st.append((nx, ny))
                xs = [c[0] for c in cells]; ys = [c[1] for c in cells]
                comps.append({"box": [min(xs)*scale, min(ys)*scale, (max(xs)+1)*scale, (max(ys)+1)*scale],
                              "area": len(cells) * scale * scale})
    return comps

CLASSES = {
    # test(r,g,b) -> bool, band(y frac), score(candidate, H) -> float (higher = better)
    # A BULB is saturated warm yellow (cardboard is not), small, roundish, and HANGS:
    # a thin dark cord must rise from its top. The cord test alone killed both false
    # positives (a cardboard box and a yellow clipboard frame) in the garage read.
    "lamp": dict(
        test=lambda r, g, b: r > 215 and g > 160 and b < 120 and (r - b) > 95,
        band=(0.0, 0.6),
        score=lambda c, W, H: (c["area"] if 150 < c["area"] < 3200 else 0)
            * (2.0 if (c["box"][1] < H * 0.5) else 0.2)
            * (1.5 if 0.6 < (c["box"][2]-c["box"][0]) / max(c["box"][3]-c["box"][1], 1) < 1.7 else 0.3),
        needs_cord=True),
    # Foliage splits into many components across dark stems — merge green blobs
    # within a small gap before scoring, so the box covers the whole crown.
    "plant": dict(
        test=lambda r, g, b: g > r + 12 and g > b + 12 and g > 90,
        band=(0.05, 0.95),
        score=lambda c, W, H: c["area"] if c["area"] > 1500 else 0,
        merge_gap=60),
    "window": dict(
        test=lambda r, g, b: b > r + 15 and b > 90 and g > r,
        band=(0.0, 0.85),
        score=lambda c, W, H: c["area"] if c["area"] > 8000 else 0),
    "screen": dict(   # pale glow surfaces (monitors) — near-white with a blue cast
        test=lambda r, g, b: r > 200 and g > 210 and b > 215 and b >= r,
        band=(0.05, 0.7),
        score=lambda c, W, H: c["area"] if 2000 < c["area"] < 400000 else 0),
}

## which classes an object list implies, and how many
def classes_from_objects(objects: str):
    o = objects.lower()
    want = {}
    for key, names in [("lamp", ["lamp", "bulb", "light"]), ("plant", ["plant", "leaves", "foliage", "tree"]),
                       ("window", ["window", "skyline", "glass wall"]), ("screen", ["screen", "monitor", "tv", "laptop"])]:
        n = sum(o.count(nm) for nm in names)
        if n:
            want[key] = min(n, 3)
    return want

def detect(path, objects="", scale=4):
    im = Image.open(path).convert("RGB")
    W, H = im.size
    sm = im.resize((W // scale, H // scale))
    w, h = sm.size
    want = classes_from_objects(objects) if objects else {k: 2 for k in CLASSES}
    out = []
    for cls, k in want.items():
        spec = CLASSES[cls]
        comps = _components(sm, spec["test"], spec["band"], w, h, scale)
        gap = spec.get("merge_gap", 0)
        if gap:
            merged = []
            for c in sorted(comps, key=lambda c: -c["area"]):
                for m in merged:
                    if (c["box"][0] < m["box"][2] + gap and c["box"][2] > m["box"][0] - gap and
                            c["box"][1] < m["box"][3] + gap and c["box"][3] > m["box"][1] - gap):
                        m["box"] = [min(m["box"][0], c["box"][0]), min(m["box"][1], c["box"][1]),
                                    max(m["box"][2], c["box"][2]), max(m["box"][3], c["box"][3])]
                        m["area"] += c["area"]
                        break
                else:
                    merged.append(dict(c))
            comps = merged
        if spec.get("needs_cord"):
            px_full = Image.open(path).convert("RGB").load()
            def has_cord(c):
                cx = (c["box"][0] + c["box"][2]) // 2
                y1 = c["box"][1]
                dark = 0
                for y in range(max(0, y1 - 90), y1):
                    for dx in (-6, -3, 0, 3, 6):
                        x = cx + dx
                        if 0 <= x < W and sum(px_full[x, y]) < 260:
                            dark += 1
                            break
                return dark >= 30
            comps = [c for c in comps if has_cord(c)]
        scored = sorted(((spec["score"](c, W, H), c) for c in comps), key=lambda t: -t[0])
        for s, c in scored[:k]:
            if s > 400:      # score floor: a 24px mug speck scored 158 and slipped through
                out.append({"class": cls, "box": c["box"], "area": c["area"], "score": round(s, 1)})
    return W, H, out

if __name__ == "__main__":
    path = sys.argv[1]
    objects = ""
    if "--objects" in sys.argv:
        objects = sys.argv[sys.argv.index("--objects") + 1]
    W, H, els = detect(path, objects)
    print(json.dumps({"size": [W, H], "elements": els}, indent=1))
    if "--overlay" in sys.argv:
        outp = sys.argv[sys.argv.index("--overlay") + 1]
        im = Image.open(path).convert("RGB")
        d = ImageDraw.Draw(im)
        COL = {"lamp": (255,140,0), "plant": (0,160,60), "window": (30,90,220), "screen": (160,40,200)}
        for e in els:
            d.rectangle(e["box"], outline=COL[e["class"]], width=6)
            d.text((e["box"][0]+4, e["box"][1]-24), f'{e["class"]} {e["score"]}', fill=COL[e["class"]])
        im.save(outp)
