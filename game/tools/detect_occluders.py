#!/usr/bin/env python3
"""Propose foreground occluders — the furniture the cast should stand behind.

Harder than the surface detector, because "which rectangle to cut" is judgement.
So this is the ask/verify/repair shape rather than a single clever rule: propose
candidates from where furniture actually meets the floor, then reject the weak
ones on evidence.

Method:
  1. measure the ground line (the wall/floor junction)
  2. look at a band straddling it — furniture rises above that line and its base
     sits below it, while bare floor is empty on both sides
  3. score each column of that band for edge energy; contiguous runs of busy
     columns are furniture masses
  4. a candidate's rect spans the run, from the top of its content down to the
     floor line, because an occluder whose bottom edge lands mid-floor slices a
     sprite's contact shadow into a straight grey seam

  python3 tools/detect_occluders.py stage_garage
  python3 tools/detect_occluders.py --eval
"""
import json, os, sys
from PIL import Image, ImageFilter, ImageStat

GAME = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCENES = f"{GAME}/assets/scenes"
CW, CH = 1536, 1024
W = 384
K = CW / W
MIN_W_CANVAS = 74          # narrower than this is a plant stem, not cover
BUSY = 1.55                # a column is furniture at this multiple of the median
FLOOR_PAD = 0.16           # how far below the ground line the rect is carried


def _ground(g):
    w = 384
    h = max(1, round(g.height * w / g.width))
    s = g.resize((w, h), Image.LANCZOS)
    px = s.load()
    best, by = -1.0, int(h * 0.6)
    for y in range(int(h * 0.40), int(h * 0.92)):
        acc = 0
        for x in range(0, w, 2):
            acc += abs(px[x, y] - px[x, y + 1])
        if acc > best:
            best, by = acc, y
    return by / float(h)


def detect(sid):
    im = Image.open(f"{SCENES}/{sid}/scene.png").convert("L").resize((CW, CH), Image.LANCZOS)
    ground = _ground(im)
    h = round(CH * W / CW)
    sm = im.resize((W, h), Image.LANCZOS)
    ed = sm.filter(ImageFilter.FIND_EDGES)
    # FLOOR-ONLY band. Straddling the ground line was wrong: the wall also
    # differs from the floor colour, so every column read as busy (median 0.57
    # against 0.17 for the floor band) and nothing separated. Below the ground
    # line, bare floor is uniform and furniture is not.
    y0 = max(0, int(ground * h))
    y1 = min(h, int((ground + FLOOR_PAD) * h))
    if y1 - y0 < 4:
        return []
    # Edge energy per column was the wrong signal: edges fire on an object's
    # OUTLINE, so a desk's flat top scores as empty and the runs came out 1-2
    # columns wide — the detector proposed nothing at all. Deviation from the
    # floor's own colour fills the whole object instead of tracing it.
    sp = sm.load()
    floor_rows = [sp[x, y] for y in range(int(h * 0.93), h) for x in range(0, W, 3)]
    floor_l = sorted(floor_rows)[len(floor_rows) // 2] if floor_rows else 200
    # The wall is needed too. Scanning UP for the top of a furniture mass while
    # comparing against the FLOOR colour never terminates, because wall differs
    # from floor as much as furniture does — every rect grew to the ceiling
    # (top 384 against a true 563). The top 8% is calm by the safe-zone law, so
    # it is a reliable wall sample.
    wall_rows = [sp[x, y] for y in range(0, max(1, int(h * 0.08))) for x in range(0, W, 3)]
    wall_l = sorted(wall_rows)[len(wall_rows) // 2] if wall_rows else 220
    cols = []
    for x in range(W):
        n = 0
        for y in range(y0, y1):
            if abs(sp[x, y] - floor_l) > 22:
                n += 1
        cols.append(n / float(max(1, y1 - y0)))
    med = sorted(cols)[len(cols) // 2]
    thresh = 0.30
    busy = [c > thresh for c in cols]
    # close single-column gaps so one pale drawer front does not split a desk
    for x in range(1, W - 1):
        if not busy[x] and busy[x - 1] and busy[x + 1]:
            busy[x] = True

    runs, i = [], 0
    while i < W:
        if not busy[i]:
            i += 1
            continue
        j = i
        while j < W and busy[j]:
            j += 1
        if (j - i) * K >= MIN_W_CANVAS:
            runs.append((i, j))
        i = j

    out = []
    for a, b in runs:
        # top of this mass: highest row above the ground line that still differs
        # from the floor across most of the run
        top = y0
        for y in range(max(0, int((ground - 0.42) * h)), y0 + 1):
            n = sum(1 for x in range(a, b) if abs(sp[x, y] - wall_l) > 22)
            if n / float(b - a) > 0.55:
                top = y
                break
        x = round(a * K)
        wpx = round((b - a) * K)
        ytop = round(top * K)
        ybot = round(min(CH - 1, (ground + FLOOR_PAD) * CH))
        out.append((x, ytop, wpx, ybot - ytop))
    return out


def _iou(a, b):
    ax, ay, aw, ah = a
    bx, by, bw, bh = b
    ix = max(0, min(ax + aw, bx + bw) - max(ax, bx))
    iy = max(0, min(ay + ah, by + bh) - max(ay, by))
    inter = ix * iy
    return inter / float(aw * ah + bw * bh - inter) if inter else 0.0


def evaluate():
    stages = sorted(d for d in os.listdir(SCENES)
                    if d.startswith("stage_") and os.path.exists(f"{SCENES}/{d}/layout.json"))
    tot = hits = extra = 0
    ious = []
    for sid in stages:
        lay = json.load(open(f"{SCENES}/{sid}/layout.json"))
        gt = {k: v for k, v in lay.items()
              if isinstance(v, dict) and v.get("kind") == "occluder"}
        det = detect(sid)
        matched = set()
        for name, v in gt.items():
            tot += 1
            g = (v["x"], v["y"], v["w"], v["h"])
            best, bi = 0.0, -1
            for i, d in enumerate(det):
                s = _iou(g, d)
                if s > best:
                    best, bi = s, i
            ious.append(best)
            if best >= 0.5:
                hits += 1
                matched.add(bi)
            print(f"  {sid:<18} {name:<18} IoU {best:.2f} {'HIT' if best >= 0.5 else 'MISS'}")
        extra += len(det) - len(matched)
    print(f"\nrecall {hits}/{tot} = {hits/max(1,tot):.0%}   "
          f"mean IoU {sum(ious)/max(1,len(ious)):.2f}   unmatched proposals {extra}")


if __name__ == "__main__":
    if "--eval" in sys.argv:
        evaluate()
    else:
        for s in sys.argv[1:]:
            print(s, detect(s))
