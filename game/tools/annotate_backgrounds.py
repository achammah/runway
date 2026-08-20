#!/usr/bin/env python3
"""Annotate the 516-room background library: write surfaces + crew marks.

The library is generated EMPTY on purpose, so every room needs two things before
the game can use it:

  1. write_surfaces — the faces the run's numbers are written onto. The room IS
     the save file: cash in the ledger, product on the whiteboard, customers on
     the wall chart, equity on a sticky, the inventory list on the big board.
  2. marks — where the founder and the crew stand.

NEITHER IS INVENTED HERE. Both come from the detectors a previous lane built and
MEASURED (see docs/BACKGROUND_INVARIANTS.md):

  * faces  -> tools/detect_surfaces.py, recall 90% on faces above the two-line
    size floor, mean IoU 0.64. Its two load-bearing findings are imported with
    it: a 768px working resolution (at 384px a 2px felt-pen outline averages
    away, the face leaks into the wall and recall collapses to 20%) and a size
    cap (a pale flat floor sealed by a dark bottom edge otherwise returns as one
    enormous false "face").
  * marks  -> tools/auto_marks.py, which measures the ground line per room rather
    than assuming it. It cannot be assumed: three pilots asked for 0.62 came back
    0.516 / 0.621 / 0.648, and this library measures 0.406 to 0.859.

Only the I/O differs from those tools (they read assets/scenes/<id>/scene.png and
write layout.json; the library is a flat folder of 1536x1024 PNGs and one
annotations.json). Every threshold, the component fill and the band/arc/scale
maths are imported or copied verbatim so the measured behaviour is preserved.

NO OCCLUDERS. The occluder detector measured 12% recall, and compositing its
proposals over three rooms erased 4 of 15 crew marks outright. A missing founder
is a bug; occlusion is a nicety. Skipped deliberately.

  python3 tools/annotate_backgrounds.py                 # all, resumable
  python3 tools/annotate_backgrounds.py --workers 4
  python3 tools/annotate_backgrounds.py --only <id> --force
  python3 tools/annotate_backgrounds.py --overlay <id> [...]   # verify by eye
"""
import json, os, sys, time
from PIL import Image, ImageFilter, ImageDraw

TOOLS = os.path.dirname(os.path.abspath(__file__))
GAME = os.path.dirname(TOOLS)
BG = f"{GAME}/assets/backgrounds"
OUT = f"{BG}/annotations.json"
PROGRESS = "/tmp/lane_annotate/progress.md"
OVERLAY_DIR = "/tmp/lane_annotate/overlays"
sys.path.insert(0, TOOLS)
import detect_surfaces as ds          # measured face detector: thresholds + flood fill
import auto_marks as am               # measured mark recipe: ground line, floor band

CW, CH = ds.CW, ds.CH                 # 1536x1024, which is also the library's own size
BATCH = 25                            # incremental write cadence
MAX_FACES = 10
SIDE_MARGIN = 0.10                    # a mark whose column sits outside this is half off-frame
FOOT_PUSH = 10                        # px of floor a foot must have above it to count as standing
CHROMA_MAX = 60                       # real faces measured 7-37; a lamp shade ran 144
COOL_MAX = 6                          # real faces run blue BELOW red; a window full of sky ran +17
GROUND_OK = (0.38, 0.74)              # outside this there is no floor band left to stand in
GROUND_REFINE = (0.42, 0.72)          # ...so re-scan for the strongest horizontal in here


# ---------------------------------------------------------------- write surfaces

def detect_faces(img):
    """ds.detect(), with the image handed in instead of read from assets/scenes.

    Body below is ds.detect lines 89-119 verbatim apart from the first line; every
    threshold (INK, GRAD, PALE, FILL, MAX_AREA, MAX_SPAN, the size floor) and the
    flood fill itself come from that module, so a retune there follows through.
    """
    im = img.convert("L")
    if im.size != (CW, CH):
        im = im.resize((CW, CH), Image.LANCZOS)
    h = round(CH * ds.W / CW)
    sm = im.resize((ds.W, h), Image.LANCZOS)
    px = sm.load()
    edge = sm.filter(ImageFilter.FIND_EDGES).load()
    mask = bytearray(ds.W * h)
    for y in range(h):
        row = y * ds.W
        for x in range(ds.W):
            if px[x, y] < ds.INK or edge[x, y] > ds.GRAD:
                mask[row + x] = 1
    found = []
    for (x0, y0, x1, y1), area, border in ds._components(mask, ds.W, h):
        if border:
            continue                                # the wall, the floor, the sky
        bw, bh = (x1 - x0 + 1), (y1 - y0 + 1)
        if bw * ds.K < ds.MIN_W_CANVAS or bh * ds.K < ds.MIN_H_CANVAS:
            continue                                # decoration-scale paper
        if area / float(bw * bh) < ds.FILL:
            continue                                # not a solid quadrilateral
        if (bw * bh) / float(ds.W * h) > ds.MAX_AREA or bw / float(ds.W) > ds.MAX_SPAN:
            continue                                # a wall or floor plane, not a face
        tot = 0
        for yy in range(y0, y1 + 1):
            for xx in range(x0, x1 + 1):
                tot += px[xx, yy]
        if tot / float(bw * bh) < ds.PALE:
            continue                                # dark panel, not a writable face
        found.append((x0 * ds.K, y0 * ds.K, bw * ds.K, bh * ds.K, area * ds.K * ds.K))
    found.sort(key=lambda r: -r[4])
    rects = [(round(x), round(y), round(w), round(hh)) for x, y, w, hh, _ in found[:MAX_FACES]]
    return reject_not_paper(img, rects)


def reject_not_paper(img, rects):
    """Drop the three false positives that reading the overlays turned up.

    The detector works on grey, so anything pale, flat and enclosed qualifies —
    which caught a lamp shade, a window full of sky and a garage door's panels.
    Measured over those rooms, every genuine face is warm off-white paper
    (chroma 7-37, blue below red by 7-37) while the lamp shade ran chroma 144 and
    the sky ran blue ABOVE red by 17. So:

      * saturated -> a lamp, a cushion, a crate. Not paper.
      * cool cast -> a window, a screen, daylight. Not paper.
      * three-plus congruent rects stacked in one column -> panelling: a door, a
        cabinet, a shutter. Paper does not come in a repeating grid.

    Dropping matters more than it looks: a false face steals one of the five
    semantic names off a real one.
    """
    rgb = img.convert("RGB")
    keep = []
    for (x, y, w, h) in rects:
        c = rgb.crop((x, y, x + w, y + h)).resize((16, 16), Image.LANCZOS).load()
        n = 256
        r = sum(c[i, j][0] for j in range(16) for i in range(16)) / n
        g = sum(c[i, j][1] for j in range(16) for i in range(16)) / n
        b = sum(c[i, j][2] for j in range(16) for i in range(16)) / n
        if max(r, g, b) - min(r, g, b) > CHROMA_MAX:
            continue
        if b - r >= COOL_MAX:
            continue
        keep.append((x, y, w, h))
    groups = {}
    for i, (x, y, w, h) in enumerate(keep):
        groups.setdefault((round(x / 12.0), round(w / 24.0)), []).append(i)
    panelled = {i for g in groups.values() if len(g) >= 3 for i in g}
    return [r for i, r in enumerate(keep) if i not in panelled]


def _inset(x, y, w, h):
    """The face minus ds.INSET on every side, so writing never touches a drawn edge."""
    dx, dy = round(w * ds.INSET), round(h * ds.INSET)
    return x + dx, y + dy, w - 2 * dx, h - 2 * dy


def name_faces(rects, ground_y):
    """Give the faces the names the game writes to.

    inventory first and by the brief's rule — the biggest, most portrait-ish face,
    because it holds a variable-length list rather than a headline. Then the wall
    boards, then the flat-lying ledger, then the small square sticky. Anything
    left over keeps a positional name rather than being thrown away.
    """
    faces = [_inset(*r) for r in rects]
    faces = [f for f in faces if f[2] > 0 and f[3] > 0]
    if not faces:
        return {}
    pool = list(range(len(faces)))

    def area(i):
        return faces[i][2] * faces[i][3]

    def portrait(i):
        return faces[i][3] / float(max(1, faces[i][2]))

    def on_wall(i):
        x, y, w, h = faces[i]
        return (y + h) <= ground_y + 0.03 * CH

    def in_ui_zone(i):
        x, y, w, h = faces[i]
        return y + h * 0.5 > 0.86 * CH or y + h * 0.5 < 0.06 * CH

    def take(cands, key, reverse=True):
        cands = [i for i in cands if i in pool]
        if not cands:
            return None
        clean = [i for i in cands if not in_ui_zone(i)] or cands
        best = sorted(clean, key=key, reverse=reverse)[0]
        pool.remove(best)
        return best

    named = {}
    # the biggest, most portrait-ish face: area weighted by how tall-for-its-width it is
    inv = take(pool, lambda i: area(i) * (0.55 + 0.90 * min(portrait(i), 2.2)))
    if inv is not None:
        named["inventory"] = inv
    # a whiteboard is the big landscape board hung on the wall
    wb = take([i for i in pool if faces[i][2] / float(max(1, faces[i][3])) >= 1.15 and on_wall(i)], area)
    if wb is None:
        wb = take(pool, area)
    if wb is not None:
        named["whiteboard"] = wb
    # a wall chart is whatever else is still up on the wall
    wc = take([i for i in pool if on_wall(i)], area)
    if wc is None:
        wc = take(pool, area)
    if wc is not None:
        named["wallchart"] = wc
    # a ledger lies flat on a desk or a counter, below the ground line
    ld = take([i for i in pool if not on_wall(i)], area)
    if ld is None:
        ld = take(pool, area)
    if ld is not None:
        named["ledger"] = ld
    # a sticky is the small squarish one
    st = take([i for i in pool if 0.55 <= portrait(i) <= 1.8], area, reverse=False)
    if st is None:
        st = take(pool, area, reverse=False)
    if st is not None:
        named["sticky"] = st
    for n, i in enumerate(sorted(pool, key=area, reverse=True)):
        named[f"face_{n + 1}"] = i

    out = {}
    for name, i in named.items():
        x, y, w, h = faces[i]
        if name == "inventory":
            lines = max(3, min(6, int(round(h / 70.0))))
        else:
            lines = 3 if h > 150 else 2
        out[name] = {"x": x, "y": y, "w": w, "h": h, "rot": 0.0,
                     "lines": lines, "align": "center"}
    return out


# ---------------------------------------------------------------------- marks

FLOOR_W = 384                         # floor mask working width
FLOOR_TOL = 30                        # per-channel distance that still counts as the floor
FLOOR_BREAK = 3                       # rows of non-floor that end the run walking up
FLOOR_MIN_RUN = 0.05                  # a column with less floor than this cannot be stood in


def floor_mask(im, cols=48):
    """Measure WHERE THE FLOOR ACTUALLY IS, per column, by colour.

    The edge-based ground line answers "where is the strongest horizontal", which
    is the wall/floor junction only when nothing else in the lower frame draws a
    longer straight line. Measured on this library it often is not: a conference
    table edge, a check-in desk top and a conveyor rail all beat the junction, and
    the marks derived from them put feet on the furniture or in mid-air (seen in
    3 of the first 8 rooms read back).

    So the floor is also measured directly. The floor is the big flat fill the
    bottom of the frame is made of, so: take its colour from the bottom band, then
    walk UP each column while the pixel still matches. Where that walk stops is
    the top of the standable floor in that column — which is exactly what a foot
    needs to know, and it is per-column, so it knows about the desk too.

    Returns (floor_top[] in canvas px per column, ground estimate as a fraction,
    ok) where ok is False when the bottom of the frame is not a floor at all.
    """
    w = FLOOR_W
    h = max(1, round(im.height * w / im.width))
    s = im.convert("RGB").resize((w, h), Image.LANCZOS)
    px = s.load()
    hist = {}
    for y in range(int(h * 0.88), h):
        for x in range(int(w * 0.10), int(w * 0.90)):
            r, g_, b = px[x, y]
            k = (r >> 4, g_ >> 4, b >> 4)
            hist[k] = hist.get(k, 0) + 1
    if not hist:
        return [], 0.0, False
    best = max(hist, key=hist.get)
    span = (h - int(h * 0.88)) * (int(w * 0.90) - int(w * 0.10))
    if hist[best] / float(span) < 0.30:        # bottom band is not one flat fill
        return [], 0.0, False
    acc = [0, 0, 0, 0]
    for y in range(int(h * 0.88), h):
        for x in range(int(w * 0.10), int(w * 0.90)):
            r, g_, b = px[x, y]
            if (r >> 4, g_ >> 4, b >> 4) == best:
                acc[0] += r; acc[1] += g_; acc[2] += b; acc[3] += 1
    fr, fg, fb = acc[0] / acc[3], acc[1] / acc[3], acc[2] / acc[3]

    step = w / float(cols)
    tops = []
    for i in range(cols):
        cx = int((i + 0.5) * step)
        top = h
        misses = 0
        for y in range(h - 1, -1, -1):
            r, g_, b = px[cx, y]
            if abs(r - fr) <= FLOOR_TOL and abs(g_ - fg) <= FLOOR_TOL and abs(b - fb) <= FLOOR_TOL:
                top = y
                misses = 0
            else:
                misses += 1
                if misses >= FLOOR_BREAK:
                    break
        tops.append(top / float(h))
    usable = sorted(t for t in tops if (1.0 - t) >= FLOOR_MIN_RUN)
    if len(usable) < cols * 0.25:              # almost no floor visible anywhere
        return [], 0.0, False
    ground = usable[max(0, int(len(usable) * 0.12))]   # the least-occluded columns
    return [round(t * CH) for t in tops], ground, True


def ground_line_in(g, lo, hi):
    """am.ground_line with the search window opened up — same energy, same 384px scan."""
    w = 384
    h = max(1, round(g.height * w / g.width))
    s = g.resize((w, h), Image.LANCZOS)
    px = s.load()
    best, best_y = -1.0, int(h * 0.6)
    for y in range(int(h * lo), min(h - 1, int(h * hi))):
        acc = 0
        for x in range(0, w, 2):
            acc += abs(px[x, y] - px[x, y + 1])
        if acc > best:
            best, best_y = acc, y
    return best_y / float(h)


def measure_ground(im, g):
    """Measure the ground line twice and take the answer feet can actually stand on.

    The edge scan (am.ground_line) finds the strongest horizontal; the colour walk
    (floor_mask) finds the top of the visible floor. Where they agree, either will
    do. Where they disagree it is because the edge scan locked onto a table, a desk
    or a window mullion ABOVE the floor, so the lower of the two on screen — the
    larger fraction — is the one with floor under it. Taking the max is the
    conservative choice by construction: never stand a figure higher than the
    highest pixel of floor the room actually shows.
    """
    edge = am.ground_line(g)
    tops, floor, ok = floor_mask(im)
    src = "edge"
    ground = edge
    if ok and floor > edge:
        ground, src = floor, "floor"
    if not (GROUND_OK[0] <= ground <= GROUND_OK[1]):
        cand = ground_line_in(g, *GROUND_REFINE)
        if ok and floor <= GROUND_OK[1]:
            ground, src = max(cand, floor), "refined+floor"
        else:
            ground, src = cand, "refined"
    return ground, src, tops


def derive_marks(im, g, n=5):
    """auto_marks.derive's recipe: measure, score the floor band, quiet columns, arc, depth.

    From am.derive lines 66-101, with three changes, each one a failure read back
    off an overlay rather than a preference:
      * columns closer to the frame edge than SIDE_MARGIN are not candidates — a
        mark at column 0 puts x at -102, i.e. a founder half out of frame;
      * a column with no visible floor in the standing band is not a candidate —
        "quiet" is not the same as "standable", and a conference tabletop is very
        quiet indeed;
      * a foot that still lands above its own column's floor top is pushed down
        onto the floor, and its depth scale is recomputed from where it ended up.
    """
    ground, src, tops = measure_ground(im, g)
    y_back = min(0.86, ground + 0.03)
    y_front = min(am.BOTTOM_CALM - 0.01, max(y_back + 0.06, ground + 0.20))
    energy = am.free_columns(g, y_back, y_front)
    step = CW // len(energy)
    lo_c = int(len(energy) * SIDE_MARGIN)
    hi_c = len(energy) - lo_c - 1

    def floor_top(i):
        return tops[i] if i < len(tops) else 0

    cands = [i for i in range(lo_c, hi_c + 1)
             if not tops or floor_top(i) <= y_front * CH + FOOT_PUSH]
    if len(cands) < n:
        cands = list(range(lo_c, hi_c + 1))
    order = sorted(cands, key=lambda i: energy[i])
    picked = []
    min_gap = max(2, len(energy) // (n + 2))
    for i in order:
        if all(abs(i - j) >= min_gap for j in picked):
            picked.append(i)
        if len(picked) == n:
            break
    picked.sort()
    if len(picked) < n:                        # very cluttered floor: fall back to even spread
        picked = [lo_c + int((k + 1) * (hi_c - lo_c) / (n + 1)) for k in range(n)]

    marks = {}
    pushed = 0
    names = ["crew_1", "crew_2", "founder_mark", "crew_3", "crew_4"][:n]
    for k, (name, ci) in enumerate(zip(names, picked)):
        t = abs(k - (n - 1) / 2.0) / max(1e-6, (n - 1) / 2.0)      # 0 centre .. 1 edges
        fy = y_back + (y_front - y_back) * (0.30 + 0.70 * t)        # edges come forward
        if tops:
            need = (floor_top(ci) + FOOT_PUSH) / float(CH)
            if need > fy:
                fy = min(am.BOTTOM_CALM - 0.01, need)
                pushed += 1
        scale = am.BACK_SCALE + (am.FRONT_SCALE - am.BACK_SCALE) * \
            ((fy - y_back) / max(1e-6, y_front - y_back))
        scale = max(am.BACK_SCALE, min(am.FRONT_SCALE, scale))
        fx = (ci + 0.5) * step
        h = round(am.NOMINAL_H * scale)
        w = round(200 * scale)
        marks[name] = {"x": round(fx - w / 2), "y": round(fy * CH - h), "w": w, "h": h,
                       "scale": round(scale, 3), "foot_x": round(fx), "foot_y": round(fy * CH),
                       "kind": "crew_mark", "placed": True, "derived": True}
    meta = {"ground_line": round(ground, 4), "ground_source": src,
            "band": [round(y_back, 4), round(y_front, 4)], "feet_pushed": pushed}
    return marks, meta


# ------------------------------------------------------------------ per-room job

def annotate(args):
    bid, fname = args
    try:
        img = Image.open(f"{BG}/{fname}")
        if img.size != (CW, CH):
            img = img.resize((CW, CH), Image.LANCZOS)
        g = img.convert("L")
        rects = detect_faces(img)
        marks, meta = derive_marks(img, g)
        surfaces = name_faces(rects, meta["ground_line"] * CH)
        meta["faces"] = len(surfaces)
        return bid, {"write_surfaces": surfaces, "marks": marks, "meta": meta}, None
    except Exception as e:                                  # a bad file must not kill the run
        return bid, None, f"{type(e).__name__}: {e}"


def _save(doc):
    tmp = OUT + ".tmp"
    with open(tmp, "w") as f:
        json.dump(doc, f, indent=1, sort_keys=True)
    os.replace(tmp, OUT)


def _progress(done, total, started, faces, errs):
    os.makedirs(os.path.dirname(PROGRESS), exist_ok=True)
    per = (time.time() - started) / max(1, done)
    with open(PROGRESS, "w") as f:
        f.write(f"# LANE-ANNOTATE\n\n"
                f"- rooms annotated: {done}/{total}\n"
                f"- mean surfaces/room: {faces / max(1, done):.2f}\n"
                f"- errors: {errs}\n"
                f"- {per:.2f}s/room, eta {per * (total - done) / 60:.1f} min\n"
                f"- updated {time.strftime('%H:%M:%S')}\n")


def run(workers, only=None, force=False, limit=0):
    index = json.load(open(f"{BG}/index.json"))
    doc = {}
    if os.path.exists(OUT) and not force:
        doc = json.load(open(OUT))
    todo = [(k, v) for k, v in sorted(index.items())
            if (only is None or k in only) and (force or k not in doc)]
    if limit:
        todo = todo[:limit]
    total = len(todo)
    print(f"{len(doc)} already annotated, {total} to do, {workers} workers", flush=True)
    started = time.time()
    done = errs = 0
    faces = sum(len(v.get("write_surfaces", {})) for v in doc.values())
    results = []
    if workers > 1:
        from multiprocessing import Pool
        pool = Pool(workers)
        it = pool.imap_unordered(annotate, todo, chunksize=2)
    else:
        pool = None
        it = (annotate(t) for t in todo)
    for bid, row, err in it:
        done += 1
        if err:
            errs += 1
            print(f"ERROR {bid} {err}", flush=True)
        else:
            doc[bid] = row
            faces += len(row["write_surfaces"])
            results.append(bid)
        if done % BATCH == 0 or done == total:
            _save(doc)
            _progress(done, total, started, faces, errs)
            print(f"[{done}/{total}] saved, mean faces {faces / max(1, len(doc)):.2f}", flush=True)
    if pool:
        pool.close(); pool.join()
    _save(doc)
    _progress(done, total, started, faces, errs)
    print(f"done {len(doc)} rooms, {errs} errors, {time.time() - started:.0f}s", flush=True)


# ------------------------------------------------------------------- verify by eye

def overlay(ids):
    """Draw what was detected onto a copy, so it can be LOOKED at rather than trusted."""
    os.makedirs(OVERLAY_DIR, exist_ok=True)
    index = json.load(open(f"{BG}/index.json"))
    doc = json.load(open(OUT))
    for bid in ids:
        row = doc[bid]
        im = Image.open(f"{BG}/{index[bid]}").convert("RGB")
        if im.size != (CW, CH):
            im = im.resize((CW, CH), Image.LANCZOS)
        d = ImageDraw.Draw(im)
        gy = row["meta"]["ground_line"] * CH
        d.line([(0, gy), (CW, gy)], fill=(0, 160, 255), width=3)
        for name, s in row["write_surfaces"].items():
            d.rectangle([s["x"], s["y"], s["x"] + s["w"], s["y"] + s["h"]],
                        outline=(230, 30, 30), width=5)
            d.text((s["x"] + 6, s["y"] + 6), f"{name} L{s['lines']}", fill=(230, 30, 30))
        for name, m in row["marks"].items():
            d.rectangle([m["x"], m["y"], m["x"] + m["w"], m["y"] + m["h"]],
                        outline=(20, 140, 40), width=4)
            fx, fy = m["foot_x"], m["foot_y"]
            d.line([(fx - 26, fy), (fx + 26, fy)], fill=(140, 0, 200), width=6)
            d.line([(fx, fy - 14), (fx, fy + 14)], fill=(140, 0, 200), width=6)
            d.text((m["x"] + 4, m["y"] - 16), f"{name} {m['scale']}", fill=(20, 110, 30))
        out = f"{OVERLAY_DIR}/{bid.replace('/', '__')}.png"
        im.save(out)
        print(out, len(row["write_surfaces"]), "faces", len(row["marks"]), "marks", flush=True)


def validate(workers=4):
    """Check the property the overlays were read for, on every room, not eight.

    A foot is standing if the floor is visible at the top of its own column at or
    above it. Anything else is a figure on a tabletop or in mid-air.
    """
    index = json.load(open(f"{BG}/index.json"))
    doc = json.load(open(OUT))
    seen, jobs = set(), []
    for bid, fn in sorted(index.items()):
        if fn not in seen and bid in doc:
            seen.add(fn)
            jobs.append((bid, fn, doc[bid]["marks"]))
    from multiprocessing import Pool
    with Pool(workers) as p:
        rows = p.map(_validate_one, jobs, chunksize=4)
    feet = sum(r[0] for r in rows)
    good = sum(r[1] for r in rows)
    bad_rooms = [r[2] for r in rows if r[2]]
    print(f"rooms {len(rows)}  feet {feet}  on floor {good} ({good / max(1, feet):.1%})")
    print(f"rooms with any foot off the floor: {len(bad_rooms)}")
    for b in bad_rooms[:15]:
        print("  ", b)


def _validate_one(job):
    bid, fn, marks = job
    im = Image.open(f"{BG}/{fn}").convert("RGB")
    if im.size != (CW, CH):
        im = im.resize((CW, CH), Image.LANCZOS)
    tops, _, ok = floor_mask(im)
    if not ok:
        return len(marks), len(marks), None            # no floor measurable: not counted against
    n = good = 0
    for m in marks.values():
        n += 1
        ci = min(len(tops) - 1, int(m["foot_x"] / (CW / float(len(tops)))))
        near = tops[max(0, ci - 1):ci + 2]
        if m["foot_y"] >= min(near) - 4:
            good += 1
    return n, good, (bid if good < n else None)


if __name__ == "__main__":
    a = sys.argv[1:]
    if "--validate" in a:
        validate()
    elif "--overlay" in a:
        overlay(a[a.index("--overlay") + 1:])
    else:
        w = int(a[a.index("--workers") + 1]) if "--workers" in a else 4
        lim = int(a[a.index("--limit") + 1]) if "--limit" in a else 0
        only = None
        if "--only" in a:
            only = set(a[a.index("--only") + 1:])
        run(w, only=only, force="--force" in a, limit=lim)
