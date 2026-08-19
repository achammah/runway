#!/usr/bin/env python3
"""Detect writable faces in a scene, so the 516-library costs nothing per room.

A writable face is a PALE, FLAT region ENCLOSED BY DRAWN INK — a whiteboard
inside its frame, a sheet inside its outline, a clipboard's paper, a corkboard
inside its moulding. The wall is also pale and flat, and is the thing that must
NOT be returned; what separates them is enclosure. The wall reaches the image
border, a face does not.

Method (PIL only, no numpy):
  1. work at 384px wide
  2. ink mask = dark pixels (the felt-pen outlines)
  3. flood-fill the non-ink regions into components
  4. keep components that are enclosed, pale, rectangular and big enough to hold
     two lines of 26px type on the 1536x1024 canvas

The size floor is the point of the whole exercise: the reason the stages had one
usable face each is that everything else was drawn at decoration scale. A
detector that returns 23x27px kanban cards has not helped anyone.

  python3 tools/detect_surfaces.py stage_garage
  python3 tools/detect_surfaces.py --eval          # score against hand-authored truth
"""
import json, os, sys
from PIL import Image, ImageFilter

GAME = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCENES = f"{GAME}/assets/scenes"
CW, CH = 1536, 1024
# 384px was too coarse: a 2px felt-pen outline averages away at that scale, the
# face leaks into the wall through the gap, and the merged component touches the
# image border and is thrown out. Recall was 20% for exactly this reason. The
# barrier is also gradient-aware now, so an anti-aliased line still seals.
W = 768                      # working width
K = CW / W                   # working px -> canvas px
INK = 150                    # below this is a drawn line
GRAD = 26                    # or this much local contrast — closes soft outlines
PALE = 176                   # a writable face is at least this bright
MIN_H_CANVAS = 58            # two lines of 26px type, with padding
MIN_W_CANVAS = 66
FILL = 0.72                  # component must fill this much of its own bbox
# The floor is also pale, flat and rectangular, and in the garage a dark line
# along the very bottom sealed it so it never touched the border — it came back
# as one enormous "face". No writable surface is a sixth of the frame.
MAX_AREA = 0.13              # fraction of the canvas
MAX_SPAN = 0.66              # nor does one span two thirds of the width
INSET = 0.03                 # same inset the clearing uses


def _components(mask, w, h):
    """Iterative flood fill; returns (bbox, area, touches_border) per component."""
    seen = bytearray(w * h)
    out = []
    for sy in range(h):
        base = sy * w
        for sx in range(w):
            i = base + sx
            if seen[i] or mask[i]:
                continue
            stack = [i]
            seen[i] = 1
            x0 = x1 = sx
            y0 = y1 = sy
            area = 0
            border = False
            while stack:
                j = stack.pop()
                jy, jx = divmod(j, w)
                area += 1
                if jx < x0: x0 = jx
                if jx > x1: x1 = jx
                if jy < y0: y0 = jy
                if jy > y1: y1 = jy
                if jx == 0 or jy == 0 or jx == w - 1 or jy == h - 1:
                    border = True
                if jx > 0 and not seen[j - 1] and not mask[j - 1]:
                    seen[j - 1] = 1; stack.append(j - 1)
                if jx < w - 1 and not seen[j + 1] and not mask[j + 1]:
                    seen[j + 1] = 1; stack.append(j + 1)
                if jy > 0 and not seen[j - w] and not mask[j - w]:
                    seen[j - w] = 1; stack.append(j - w)
                if jy < h - 1 and not seen[j + w] and not mask[j + w]:
                    seen[j + w] = 1; stack.append(j + w)
            out.append(((x0, y0, x1, y1), area, border))
    return out


def detect(sid):
    im = Image.open(f"{SCENES}/{sid}/scene.png").convert("L").resize((CW, CH), Image.LANCZOS)
    h = round(CH * W / CW)
    sm = im.resize((W, h), Image.LANCZOS)
    px = sm.load()
    edge = sm.filter(ImageFilter.FIND_EDGES).load()
    mask = bytearray(W * h)
    for y in range(h):
        row = y * W
        for x in range(W):
            if px[x, y] < INK or edge[x, y] > GRAD:
                mask[row + x] = 1
    found = []
    for (x0, y0, x1, y1), area, border in _components(mask, W, h):
        if border:
            continue                               # the wall, the floor, the sky
        bw, bh = (x1 - x0 + 1), (y1 - y0 + 1)
        if bw * K < MIN_W_CANVAS or bh * K < MIN_H_CANVAS:
            continue                               # decoration-scale paper
        if area / float(bw * bh) < FILL:
            continue                               # not a solid quadrilateral
        if (bw * bh) / float(W * h) > MAX_AREA or bw / float(W) > MAX_SPAN:
            continue                               # a wall or floor plane, not a face
        tot = 0
        for yy in range(y0, y1 + 1):
            for xx in range(x0, x1 + 1):
                tot += px[xx, yy]
        if tot / float(bw * bh) < PALE:
            continue                               # dark panel, not a writable face
        found.append((x0 * K, y0 * K, bw * K, bh * K, area * K * K))
    found.sort(key=lambda r: -r[4])
    return [(round(x), round(y), round(w), round(hh)) for x, y, w, hh, _ in found]


def as_faces(rects):
    out = {}
    for i, (x, y, w, h) in enumerate(rects):
        dx, dy = round(w * INSET), round(h * INSET)
        out[f"face_{i+1}"] = {"x": x + dx, "y": y + dy, "w": w - 2 * dx, "h": h - 2 * dy,
                              "rot": 0.0, "lines": 3 if h > 150 else 2, "align": "center"}
    return out


def _iou(a, b):
    ax, ay, aw, ah = a
    bx, by, bw, bh = b
    ix = max(0, min(ax + aw, bx + bw) - max(ax, bx))
    iy = max(0, min(ay + ah, by + bh) - max(ay, by))
    inter = ix * iy
    return inter / float(aw * ah + bw * bh - inter) if inter else 0.0


def evaluate():
    """Score against the faces I annotated by hand — real ground truth."""
    stages = sorted(d for d in os.listdir(SCENES)
                    if d.startswith("stage_") and os.path.exists(f"{SCENES}/{d}/layout.json"))
    tot_gt = hits = 0
    ious = []
    extra = 0
    for sid in stages:
        gt = json.load(open(f"{SCENES}/{sid}/layout.json")).get("write_surfaces", {})
        det = as_faces(detect(sid))
        det_rects = [(v["x"], v["y"], v["w"], v["h"]) for v in det.values()]
        matched = set()
        for name, v in gt.items():
            tot_gt += 1
            g = (v["x"], v["y"], v["w"], v["h"])
            best, bi = 0.0, -1
            for i, d in enumerate(det_rects):
                s = _iou(g, d)
                if s > best:
                    best, bi = s, i
            ious.append(best)
            if best >= 0.5:
                hits += 1
                matched.add(bi)
            print(f"  {sid:<18} {name:<12} IoU {best:.2f} {'HIT' if best >= 0.5 else 'MISS'}")
        extra += len(det_rects) - len(matched)
    print(f"\nrecall {hits}/{tot_gt} = {hits/max(1,tot_gt):.0%}   "
          f"mean IoU {sum(ious)/max(1,len(ious)):.2f}   unmatched detections {extra}")


if __name__ == "__main__":
    if "--eval" in sys.argv:
        evaluate()
    else:
        for s in sys.argv[1:]:
            print(s, json.dumps(as_faces(detect(s)), indent=1))
