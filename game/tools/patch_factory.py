#!/usr/bin/env python3
"""RUNWAY! spot-patch factory — the five era rooms, built as swappable scenes.

BLANK_SCENES_ARCHITECTURE.md §8: a scene's spots are REGIONS WITH RENDITIONS, and
every rendition is cut from a native full-scene render of that same scene. Nothing is
ever pasted in from outside, so integration is correct by construction.

The pipeline, per era room (garage / coworking / office / floor / hq):

  A populated   the era's room WITH its working crew, rendered natively
                (seedream edit; the room itself and each character's canonical
                sprite go in as reference images, so the place and the identities
                both hold)
  B blank       the same render with every character erased — the plate
  C swaps       the founder spot re-cast for the other three archetypes, one
                replace-edit each, everything else identical
  D patches     per (spot, character): the pixel-difference against the blank
                inside that spot's region, carrying its own contact shadow and a
                furniture-shaped hole wherever the scene occluded the body
  E ambient     one 4s seedance loop of the BLANK -> additive light deltas

Spot regions are MEASURED, never authored. A character is the only solid INK-BLACK
mass in this palette, so `ink AND changed-against-the-blank` is the set of body
pixels and nothing else; each body's region then grows to whatever changed beside
it — sneakers on a pale floor, a held laptop, a contact shadow — and the founder's
region is widened again to hold all four of its renditions, so a rolling suitcase
that only the consultant brings is never sliced in half.

Usage
  patch_factory.py refs                      upload the identity references (once)
  patch_factory.py plan  <era>               print the jobs this era needs
  patch_factory.py push  <era>               upload the populated render, before
                                             the jobs that reference it fan out
  patch_factory.py edit  <era> <name>        one seedream edit (resumable: skips
                                             a file that already decodes)
  patch_factory.py empty <era>               eye-in-ink test on the blank
  patch_factory.py spots <era>               measure the spot regions
  patch_factory.py swapcheck <era>           report how far each re-cast reached
  patch_factory.py sheet <era>               the four renditions side by side —
                                             the read that actually gates a swap
  patch_factory.py patches <era>             cut every patch + patches.json
  patch_factory.py eyes <era>                find the eyes in every patch
  patch_factory.py assemble <era> <out> <spot>=<who> ...
  patch_factory.py ambient <era>             seedance loop of the blank
  patch_factory.py deltas <era>              loop -> seam measurement -> deltas
  patch_factory.py verify                    every PNG decodes, every table ships
  patch_factory.py report <era>              one line per stage, for the log

Order for a fresh scene, parallel across eras at every step:
  refs -> edit <era> populated -> push -> edit blank + the three swaps -> spots
  -> patches -> eyes -> ambient -> deltas -> assemble and LOOK -> verify
"""
import base64, io, json, math, os, subprocess, sys, time, urllib.request

import numpy as np
from PIL import Image, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from scene_pipeline import STYLE, STYLE_EMPTY, ATLAS, UA, _key, _post_json, _fetch, _permanent_url

GAME = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ROOT = f"{GAME}/assets/patch_scenes"
REFS = f"{ROOT}/_refs.json"
SIZE = "2048*1360"

# The proven cut numbers (v2.1 derive pipeline, re-measured on the vc_pitch scene).
THRESH = 26          # a pixel differs from the blank
MAXF = 5             # MaxFilter: close the gaps a flat-fill style leaves inside a body
MINF = 3             # MinFilter: pull the fringe back off the furniture
MIN_COMPONENT = 900  # at 2048x1360 anything smaller is render noise, not a character

# ---------------------------------------------------------------- the five rooms

# Identity references. A cast sprite is keyed (half its pixels are transparent) and an
# ink-black character flattened onto black by an uploader would be an invisible
# reference, so every sprite is composited onto the palette's paper cream first.
CAST = {
    "hacker":     "scenes/cast_hacker_fine/sprite.png",
    "hustler":    "scenes/cast_founder_hustler_fine/sprite.png",
    "pm":         "scenes/cast_founder_pm_fine/sprite.png",
    "consultant": "scenes/cast_founder_consultant_fine/sprite.png",
    "cofd_tech":  "scenes/cast_cofd_tech_fine/sprite.png",
    "cofd_sales": "scenes/cast_cofd_sales_fine/sprite.png",
    "cofd_business": "scenes/cast_cofd_business_fine/sprite.png",
    "employee":   "poses/employee/_canonical.png",
}
ROOMS = {
    "garage":    "scenes/stage_garage/scene.png",
    "coworking": "scenes/stage_coworking/scene.png",
    "office":    "scenes/stage_office/scene.png",
    "floor":     "scenes/stage_floor/scene.png",
    "hq":        "scenes/stage_hq/scene.png",
}

# Props are the ONLY thing that tells two characters apart (character law), so the
# swap prompts name them and nothing else about the body.
# Phrased so they fit ANY body position. "a laser pointer in the raised hand" told a
# seated founder to stand up, and the model solved the contradiction by handing the
# pointer to whoever in the room was already standing — the office scene's failure.
FOUNDER_PROPS = {
    "hacker":     "an open glowing yellow laptop and a small screwdriver",
    "hustler":    "a dark smartphone held in one stick hand and a coral takeaway coffee cup with a lid, "
                  "either in the other hand or set down right next to them",
    "pm":         "a cream clipboard in one stick hand and a slim coral pen or laser pointer in the other",
    "consultant": "a slim coral laser pointer in one stick hand and a sage-green rolling suitcase with its "
                  "handle up, standing on the floor right beside them",
}

# Each era: the room, the crew, and the spots left-to-right. `founder` names the spot
# that gets re-cast; `who` is the identity reference each spot's character is built from.
ERAS = {
    "garage": {
        "room": "garage",
        "founder": "desk",
        "spots": [
            {"id": "bench", "who": "cofd_tech",
             "doing": "standing at the long wooden workbench on the LEFT, leaning over it, "
                      "soldering a small circuit board with a thin soldering iron, a curl of smoke rising"},
            {"id": "desk", "who": "hacker",
             "doing": "sitting on the blue office chair at the small desk in the MIDDLE of the room, "
                      "leaning toward the open laptop and typing on it with both stick hands"},
        ],
        "scene": "a two-car garage converted into a first office: a pegboard of tools over a long wooden "
                 "workbench on the left, a bare bulb on a cord, a small desk with a laptop in the middle, "
                 "a rolling whiteboard, a plant, a shelf of cardboard boxes and a closed roller door on the right",
    },
    "coworking": {
        "room": "coworking",
        "founder": "hotdesk",
        "spots": [
            {"id": "hotdesk", "who": "hacker",
             "doing": "sitting on one of the sage-green chairs at the long hot-desk table on the LEFT, "
                      "leaning in over the open laptop on the table and typing on it"},
            {"id": "phone", "who": "cofd_sales",
             "doing": "standing in the RIGHT half of the room beside the second long table, weight on one "
                      "leg, one stick arm raised holding a phone up beside the head, mid-call"},
        ],
        "scene": "a shared coworking floor: two long light-wood hot-desk tables with sage-green chairs, a "
                 "grey soundproof phone booth, an exposed brick column, pendant lamps, a green board on the "
                 "wall, plants and a small coffee counter on the right",
    },
    "office": {
        "room": "office",
        "founder": "desk_left",
        "spots": [
            {"id": "desk_left", "who": "hacker",
             "doing": "sitting on the black task chair at the LEFT desk, turned toward the monitor, "
                      "both stick hands on the keyboard, typing"},
            {"id": "board", "who": "cofd_business",
             "doing": "standing in the MIDDLE of the room just left of the server rack, one stick arm "
                      "raised toward the whiteboard, holding a marker"},
            {"id": "desk_right", "who": "cofd_tech",
             "doing": "sitting on the black task chair at the RIGHT desk, leaning back slightly, "
                      "one hand on the mouse, looking at the monitor"},
        ],
        "scene": "a small first real office: a glass door to the street on the left, a desk with a monitor "
                 "on each side of the room, a big blank whiteboard, a black server rack blinking in the "
                 "middle, a shelf of folders, a plant and a safe",
    },
    "floor": {
        "room": "floor",
        "founder": "presenter",
        "spots": [
            {"id": "seat_left", "who": "employee",
             "doing": "sitting on one of the coral task chairs at the LEFT bank of desks, swivelled away "
                      "from the monitors to face the middle of the room, watching"},
            {"id": "presenter", "who": "hacker",
             "doing": "standing in the MIDDLE of the room in front of the wall of pinned charts, "
                      "turned three-quarters toward the camera, one stick arm raised and open, presenting"},
            {"id": "seat_right", "who": "employee",
             "doing": "sitting on one of the coral task chairs at the RIGHT bank of desks, swivelled away "
                      "from the monitors to face the middle of the room, watching"},
        ],
        "scene": "a startup open floor: two long banks of desks with monitors and coral task chairs facing "
                 "each other, a foosball table in the middle, a wall of pinned paper charts and sticky "
                 "notes, a bookshelf, a snack cabinet, plants and a blue door on the right",
    },
    "hq": {
        "room": "hq",
        "founder": "window",
        # the two at the table sit a chair apart and the chair's own ink outline bridges
        # them into one blob; a 3px opening cuts that bridge
        "open": 3,
        "spots": [
            {"id": "table_1", "who": "cofd_business",
             "doing": "sitting on one of the yellow chairs on the FAR SIDE of the long red conference "
                      "table on the LEFT, facing the camera, both hands resting on the table"},
            {"id": "table_2", "who": "cofd_sales",
             "doing": "sitting on the next yellow chair along the far side of the same red conference "
                      "table, turned slightly toward the first one, one hand raised mid-sentence"},
            {"id": "window", "who": "hacker",
             "doing": "standing at the floor-to-ceiling glass in the MIDDLE-RIGHT of the room, seen from "
                      "behind at three-quarters, hands at their sides, looking out over the skyline"},
        ],
        "scene": "a skyline HQ floor: a long red conference table with yellow chairs on the left, "
                 "floor-to-ceiling windows over a pale city skyline, a small low stage with a microphone, "
                 "a coral couch on a green rug, a big blank whiteboard and tall plants",
    },
}

# ---------------------------------------------------------------- small helpers

def era_dir(era, *sub):
    d = os.path.join(ROOT, era, *sub)
    os.makedirs(d, exist_ok=True)
    return d

def arr(path):
    return np.asarray(Image.open(path).convert("RGB")).astype(np.int16)

def rgba(path):
    return np.asarray(Image.open(path).convert("RGBA")).astype(np.int16)

def intact(path):
    """A resumable step must be able to tell a finished file from a half one."""
    if not os.path.exists(path) or os.path.getsize(path) < 4096:
        return False
    try:
        data = open(path, "rb").read()
        if data[:8] != b"\x89PNG\r\n\x1a\n" or data[-8:-4] != b"IEND":
            return False
        Image.open(io.BytesIO(data)).load()
        return True
    except Exception:
        return False

# Morphology by distance transform. PIL's MaxFilter is a sliding window, so a radius-55
# grow is a 111x111 pass over 2.8M pixels and the five-scene sweep ran past the watchdog;
# an EDT is one linear pass whatever the radius. It also gives a round structuring
# element rather than a square one, which is what a body outline actually wants.
def _dilate(m, r):
    if r <= 0 or not m.any():
        return m
    if _ndi is not None:
        return _ndi.distance_transform_edt(~m) <= r
    im = Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(2 * r + 1))
    return np.asarray(im) > 127

def _erode(m, r):
    if r <= 0 or not m.any():
        return m
    if _ndi is not None:
        return _ndi.distance_transform_edt(m) > r
    im = Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(2 * r + 1))
    return np.asarray(im) > 127

try:
    from scipy import ndimage as _ndi
except ImportError:
    _ndi = None

def _label(mask):
    """Connected components, 4-neighbour. A 2048x1360 change mask holds well over a
    million set pixels and the pure-Python flood fill took minutes per call, which
    turned a five-scene sweep into a watchdog kill — so scipy does it when present."""
    if _ndi is not None:
        lab, cur = _ndi.label(mask)
        return lab.astype(np.int32), int(cur)
    h, w = mask.shape
    lab = np.zeros((h, w), np.int32)
    cur = 0
    seen = mask.copy()
    for y0, x0 in np.argwhere(mask):
        if not seen[y0, x0]:
            continue
        cur += 1
        stack = [(y0, x0)]
        seen[y0, x0] = False
        while stack:
            y, x = stack.pop()
            lab[y, x] = cur
            if y > 0 and seen[y - 1, x]:
                seen[y - 1, x] = False; stack.append((y - 1, x))
            if y < h - 1 and seen[y + 1, x]:
                seen[y + 1, x] = False; stack.append((y + 1, x))
            if x > 0 and seen[y, x - 1]:
                seen[y, x - 1] = False; stack.append((y, x - 1))
            if x < w - 1 and seen[y, x + 1]:
                seen[y, x + 1] = False; stack.append((y, x + 1))
    return lab, cur

def _boxes_of(lab, k):
    """Per-label bounding boxes and areas in one pass."""
    if _ndi is not None and k:
        objs = _ndi.find_objects(lab)
        sizes = np.bincount(lab.ravel(), minlength=k + 1)
        out = {}
        for i, sl in enumerate(objs, start=1):
            if sl is None:
                continue
            out[i] = ([int(sl[1].start), int(sl[0].start), int(sl[1].stop), int(sl[0].stop)],
                      int(sizes[i]))
        return out
    out = {}
    for i in range(1, k + 1):
        ys, xs = np.nonzero(lab == i)
        if len(ys):
            out[i] = ([int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1], len(ys))
    return out

def change_mask(a, b, thresh=THRESH):
    """The proven cut: threshold, MaxFilter to close a flat body's interior, MinFilter
    to pull the fringe back off the furniture."""
    m = np.abs(a - b).max(axis=2) > thresh
    m = _dilate(m, MAXF // 2)
    m = _erode(m, MINF // 2)
    return m

def keep_big(mask, min_px=MIN_COMPONENT):
    lab, k = _label(mask)
    if k == 0:
        return mask, []
    info = _boxes_of(lab, k)
    good = np.zeros(k + 1, bool)
    comps = []
    for i, (box, area) in info.items():
        if area >= min_px:
            good[i] = True
            comps.append({"box": box, "area": area})
    return good[lab], comps

# ---------------------------------------------------------------- references

def _refs_load():
    return json.load(open(REFS)) if os.path.exists(REFS) else {}

def _refs_put(k, v):
    os.makedirs(ROOT, exist_ok=True)
    have = _refs_load(); have[k] = v
    json.dump(have, open(REFS, "w"), indent=1)
    return v

def ref_url(key):
    have = _refs_load()
    if key in have:
        return have[key]
    raise SystemExit(f"reference not uploaded yet: {key} — run `patch_factory.py refs`")

def cmd_refs():
    """Flatten every keyed sprite onto paper cream and upload it once."""
    flat = era_dir("_refs")
    todo = {}
    for name, rel in list(CAST.items()) + [(f"room_{k}", v) for k, v in ROOMS.items()]:
        src = f"{GAME}/assets/{rel}"
        assert os.path.exists(src), src
        out = f"{flat}/{name}.png"
        if not intact(out):
            im = Image.open(src).convert("RGBA")
            bg = Image.new("RGBA", im.size, (242, 234, 211, 255))
            bg.alpha_composite(im)
            bg.convert("RGB").save(out)
        todo[name] = out
    have = _refs_load()
    for name, path in todo.items():
        if name in have:
            print(f"  {name}: cached")
            continue
        url = _permanent_url(path)
        if not url:
            print(f"  {name}: UPLOAD FAILED")
            continue
        _refs_put(name, url)
        print(f"  {name}: {url[:80]}")

# ---------------------------------------------------------------- A/B/C: the edits

def _crew_block(era):
    e = ERAS[era]
    rows = []
    for i, s in enumerate(e["spots"], start=1):
        rows.append(f"{i}. one creature {s['doing']}.")
    return " ".join(rows)

def prompts(era):
    """Every job this era needs: name -> (prompt, [ref keys])."""
    e = ERAS[era]
    room = f"room_{e['room']}"
    n = len(e["spots"])
    ident = " ".join(
        f"The creature in position {i} must match reference image {i + 1} exactly in body shape, "
        f"eyes and props." for i in range(1, n + 1))
    populated = (
        f"Redraw reference image 1 — {e['scene']} — as the SAME room, same camera, same furniture in the "
        f"same places, same colours and same light, but now with {n} creature{'s' if n > 1 else ''} working "
        f"in it, drawn INTO the room natively so their scale, perspective, contact shadows and the way the "
        f"furniture passes in front of them are all correct: {_crew_block(era)} "
        f"{ident} "
        "The creatures must be spread apart and must not overlap each other. "
        "Every whiteboard, wall chart, clipboard, sticky note and board in the room stays exactly where it "
        "is and stays COMPLETELY BLANK — no writing, no numbers, no diagrams drawn on any of them. "
        "Keep the top tenth of the image and the bottom seventh calm and empty as in the reference. ")
    jobs = {"populated": (populated, [room] + [s["who"] for s in e["spots"]])}

    jobs["blank"] = (
        "Redraw reference image 1 with EVERY creature removed. Remove each creature completely, together "
        "with everything they were holding or touching — laptops in their hands, phones, cups, clipboards, "
        "pointers, soldering irons, markers, suitcases — and their contact shadows on the floor and on the "
        "furniture. Where a creature was, draw what is behind them: the chair, the desk, the wall, the "
        "floor. EVERYTHING ELSE IS IDENTICAL, pixel for pixel: the same camera, the same furniture in the "
        "same places, the same colours, the same light, the same shadows of the furniture, the same blank "
        "boards and papers, the same plants. Do not move, resize, restyle or redraw any object. "
        "The result is the same room, unoccupied and waiting. ",
        ["populated"])

    fspot = next(s for s in e["spots"] if s["id"] == e["founder"])
    # naming the creatures that must NOT change, one by one, is what stops the edit from
    # handing the new prop to whichever creature happens to suit it better
    hold = " ".join(f"The creature {s['doing']} MUST NOT CHANGE AT ALL — same body, same props "
                    f"({'a marker' if s['id'] == 'board' else 'whatever it already holds'}), same place, "
                    "same pose. It is not the one being replaced."
                    for s in e["spots"] if s["id"] != e["founder"])
    for who in ("hustler", "pm", "consultant"):
        jobs[f"swap_{who}"] = (
            "Change EXACTLY ONE creature in reference image 1 and nothing else in the whole picture. "
            f"THE ONE TO CHANGE, and the only one: the creature {fspot['doing']}. That creature is "
            "replaced by a different creature in the SAME place, at the same size, in the same body "
            "position, under the same light, matching reference image 2 exactly in body shape and eyes. "
            f"What changes about it is only what it has: it now has {FOUNDER_PROPS[who]}, and everything "
            "the previous creature was holding or using is gone. "
            f"{hold} "
            "The room is UNTOUCHED: same camera, same furniture in the same places, same colours, same "
            "light, same plants, same blank boards and papers with nothing written on them. Do not add "
            "any creature, do not remove any creature, do not move any creature.",
            ["populated", who])
    return jobs

def seedream_edit(prompt, ref_urls, out_path, tries=3):
    key = _key("atlas-key.txt")
    last = ""
    for attempt in range(1, tries + 1):
        try:
            r = _post_json(f"{ATLAS}/api/v1/model/generateImage",
                           {"model": "bytedance/seedream-v5.0-pro/edit",
                            "prompt": prompt, "images": ref_urls,
                            "size": SIZE, "output_format": "png",
                            "thinking": "enabled", "prompt_optimization_mode": "standard",
                            "enable_base64_output": False},
                           {"Authorization": f"Bearer {key}"})
            jid = r["data"]["id"]
            for _ in range(120):
                time.sleep(4)
                req = urllib.request.Request(f"{ATLAS}/api/v1/model/prediction/{jid}",
                                             headers={"Authorization": f"Bearer {key}", **UA})
                st = json.load(urllib.request.urlopen(req, timeout=60))["data"]
                if st["status"] in ("completed", "succeeded"):
                    _fetch((st["outputs"] or [""])[0], out_path)
                    return True
                if st["status"] == "failed":
                    raise IOError("model failed: %s" % str(st.get("error"))[:200])
            raise IOError("timed out")
        except Exception as ex:
            last = f"{type(ex).__name__}: {ex}"
            print(f"  attempt {attempt}/{tries}: {last}", flush=True)
            if attempt < tries:
                time.sleep(6 * attempt)
    raise SystemExit(f"edit failed: {last}")

def cmd_push(era):
    """Upload this era's populated render ONCE, before the jobs that reference it fan
    out. _refs_put is a read-modify-write of one shared file: four workers uploading the
    same picture concurrently would each write the whole dict back and all but the last
    would be lost — the failure that silently dropped 18 of 27 refs on the pose lane."""
    p = f"{era_dir(era)}/populated.png"
    assert intact(p), f"{era}: no populated.png"
    k = f"{era}__populated"
    have = _refs_load()
    if k in have:
        print(f"{era}: populated already uploaded")
        return
    url = _permanent_url(p)
    assert url, f"{era}: upload failed"
    _refs_put(k, url)
    print(f"{era}: {url[:80]}")

def cmd_edit(era, name, force=False):
    """One generation. Resumable by construction: a file that already decodes is kept."""
    jobs = prompts(era)
    assert name in jobs, f"{name} not in {list(jobs)}"
    out = {"populated": f"{era_dir(era)}/populated.png",
           "blank": f"{era_dir(era)}/blank.png"}.get(name, f"{era_dir(era, 'src')}/{name}.png")
    if intact(out) and not force:
        print(f"{era}/{name}: already on disk, skipped")
        return
    prompt, refs = jobs[name]
    urls = []
    for r in refs:
        if r == "populated":
            p = f"{era_dir(era)}/populated.png"
            assert intact(p), f"{era}: populated.png must exist before {name}"
            k = f"{era}__populated"
            urls.append(_refs_load().get(k) or _refs_put(k, _permanent_url(p)))
        else:
            urls.append(ref_url(r))
    assert all(urls), f"{era}/{name}: a reference URL is missing"
    print(f"{era}/{name}: {len(urls)} refs, generating...", flush=True)
    seedream_edit(prompt + " " + (STYLE_EMPTY if name == "blank" else STYLE), urls, out)
    print(f"{era}/{name}: saved {out}", flush=True)

# ---------------------------------------------------------------- the eye-in-ink test

def eye_blobs(img, mask=None, min_area=200, max_area=9000):
    """Blank white ovals sitting INSIDE an ink-black body. This is both the emptiness
    test on a blank plate (an occupied 'empty' room doubles the cast — the known
    failure) and the blink data on a patch."""
    a = np.asarray(Image.open(img).convert("RGBA")).astype(np.int16) if isinstance(img, str) else img
    sel = np.ones(a.shape[:2], bool) if mask is None else mask
    opaque = (a[..., 3] > 0) if a.shape[2] == 4 else np.ones(a.shape[:2], bool)
    ink = sel & opaque & (a[..., :3].mean(axis=2) < 70)
    white = sel & opaque & (a[..., :3].min(axis=2) > 190)
    white = _dilate(_erode(white, 2), 2)
    lab, k = _label(white)
    sizes = np.bincount(lab.ravel())
    found = []
    for i in range(1, k + 1):
        area = int(sizes[i])
        if not (min_area <= area <= max_area):
            continue
        comp = (lab == i)
        ys, xs = np.nonzero(comp)
        x0, y0, x1, y1 = int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1
        w, h = x1 - x0, y1 - y0
        if w < 10 or h < 10 or not (0.45 <= w / h <= 2.2):
            continue
        halo = _dilate(comp, 6) & ~_dilate(comp, 2)
        if ink[halo].mean() < 0.80:      # must be RINGED by body ink, not merely pale
            continue
        found.append([x0, y0, x1, y1, area])
    found.sort(key=lambda e: -e[4])
    return found

def cmd_empty(era):
    """A blank plate must contain no eyes anywhere, and must not disagree with the
    populated render outside the crew's own regions."""
    d = era_dir(era)
    eyes = eye_blobs(f"{d}/blank.png")
    pop, bl = arr(f"{d}/populated.png"), arr(f"{d}/blank.png")
    m = change_mask(pop, bl)
    keep, comps = keep_big(m)
    rep = {"eyes_found_in_blank": eyes,
           "verdict": "EMPTY" if not eyes else "OCCUPIED — RE-ROLL",
           "changed_components": len(comps),
           "changed_pct": round(float(keep.mean()) * 100, 2),
           "component_boxes": [c["box"] for c in comps],
           "component_areas": [c["area"] for c in comps]}
    json.dump(rep, open(f"{d}/empty_report.json", "w"), indent=1)
    print(json.dumps(rep))
    return 0 if not eyes else 1

# ---------------------------------------------------------------- spots

def _merge_pass(boxes, areas, gap, cond=None):
    changed = True
    while changed:
        changed = False
        for i in range(len(boxes)):
            for j in range(i + 1, len(boxes)):
                a, b = boxes[i], boxes[j]
                if not (a[0] - gap < b[2] and b[0] - gap < a[2]
                        and a[1] - gap < b[3] and b[1] - gap < a[3]):
                    continue
                if cond and not cond(areas[i], areas[j]):
                    continue
                boxes[i] = [min(a[0], b[0]), min(a[1], b[1]), max(a[2], b[2]), max(a[3], b[3])]
                areas[i] += areas[j]
                del boxes[j]; del areas[j]
                changed = True
                break
            if changed:
                break
    return boxes, areas

def _merge_boxes(comps, gap=24, reach=130):
    """A character cuts into several components — a desk crosses the body, a held prop
    sits a few px off the hand, a contact shadow detaches. Merge in two passes so two
    characters sitting a chair apart stay two spots:
      1. anything within `gap` px (touching: body, limbs, shadow, held prop);
      2. then absorb the leftover SMALL fragments into a big neighbour within `reach`.
    A big-to-big merge is never allowed in pass 2, which is what keeps a pair apart."""
    boxes = [c["box"] for c in comps]
    areas = [c["area"] for c in comps]
    boxes, areas = _merge_pass(boxes, areas, gap)
    if areas:
        big = max(areas)
        boxes, areas = _merge_pass(boxes, areas, reach,
                                   cond=lambda x, y: min(x, y) < 0.22 * big)
    order = sorted(range(len(boxes)), key=lambda i: boxes[i][0])
    return [boxes[i] for i in order], [areas[i] for i in order]

SEED_THRESH = 110      # a solid ink body against a pale room; paper grain never reaches it
SEED_MIN = 9000        # a character at 2048x1360
SEED_ERODE = 6         # thickness: what survives is a BLOB, not a re-drawn outline
SEED_CORE = 1500
SEED_REACH = 45        # how far from its core a body's limbs and props may be claimed

INK = 70            # a character is a SOLID ink-black blob; nothing else in the palette is
BODY_MIN = 6000     # the smallest ink body worth calling a character at 2048x1360
BODY_REACH = 55     # how far from the body its sneakers, held props and shadow may sit
PROP_REACH = 130    # ...and how far a prop set down beside it may stand (a rolling suitcase)

def character_regions(pop, blank, gap=10, open_r=0):
    """The crew's regions in a populated render, measured against its blank plate.

    Two facts about this style do the work. A character is the only SOLID INK-BLACK mass
    in the palette, and the blank is the same room with the crew gone — so
    `ink AND changed` is the set of character-body pixels and nothing else. Re-drawn
    furniture outlines are thin and mostly not ink-black over their whole width; a
    yellow chair standing between two seated characters is not ink at all, which is what
    keeps a pair that sits a chair apart measuring as TWO spots instead of one blob.

    Around each body the region then grows to whatever changed nearby: cream sneakers on
    a pale floor, a yellow laptop, a coral cup, the contact shadow."""
    ink = pop.mean(axis=2) < INK
    chg = change_mask(pop, blank, 60)
    raw = chg & ink
    # `open_r` breaks the hair-thin bridges that join two characters sitting a chair
    # apart: the chair's own black OUTLINE is ink, and it counts as changed because a
    # character stood in front of it, which merged the HQ pair into one spot. Opt-in per
    # scene, because the same opening severs a character at a thin waist or a stick leg —
    # it cut the floor scene's seated employee into a torso and a pair of legs.
    if open_r:
        raw = _dilate(_erode(raw, open_r), open_r) & raw
    bodies, comps = keep_big(raw, BODY_MIN)
    if not comps:
        return [], [], []
    # merge BODIES, then grow — never the other way round. Growing first and merging
    # after put the garage's two characters in one region: each body's box, grown by the
    # reach that catches its sneakers, overlapped the other's.
    bboxes, bareas = _merge_boxes(comps, gap=gap, reach=90)
    lab, k = _label(bodies)
    info = _boxes_of(lab, k)
    regions = []
    for bx in bboxes:
        sel = np.zeros(bodies.shape, bool)
        for i, (box, area) in info.items():
            if box[0] < bx[2] and bx[0] < box[2] and box[1] < bx[3] and bx[1] < box[3]:
                sel |= (lab == i)
        near = chg & _dilate(sel, BODY_REACH)
        ys, xs = np.nonzero(near)
        regions.append([int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1])
    return regions, bareas, bboxes

def solid_regions(a, b, thresh=SEED_THRESH):
    """Where does picture `b` differ from `a` BECAUSE OF A CHARACTER?

    Every edit re-renders the whole room, so a plain threshold lights up on each
    re-drawn outline and every speckle of paper grain — 7-11% of the frame here, in a
    dozen scattered components, which measured the founder's 'region' as the whole
    picture. What separates a character from a jittered outline is not size, it is
    THICKNESS: erode the change mask and a 3px line disappears while a bean-shaped body
    barely shrinks. Components with a surviving core are the characters; each one's box
    is taken near that core so a chain of drifted furniture edges the component happens
    to touch cannot stretch it across the room."""
    m = change_mask(a, b, thresh)
    lab, k = _label(m)
    if k == 0:
        return [], []
    core = _erode(m, SEED_ERODE)
    sizes = np.bincount(lab.ravel(), minlength=k + 1); sizes[0] = 0
    csz = np.bincount(lab[core], minlength=k + 1); csz[0] = 0
    csz[0] = 0
    solid = np.zeros(k + 1, bool)
    for i in range(1, k + 1):
        solid[i] = sizes[i] >= SEED_MIN and csz[i] >= SEED_CORE
    if not solid.any():
        return [], []
    # the box is taken NEAR the core, so a chain of drifted furniture outlines that the
    # component happens to touch cannot stretch a character's region across the room
    near = solid[lab] & _dilate(core & solid[lab], SEED_REACH)
    lab2, k2 = _label(near)
    info = _boxes_of(lab2, k2)
    comps = [{"box": bx, "area": ar} for bx, ar in info.values() if ar >= SEED_CORE]
    return _merge_boxes(comps)

def measure_regions(a, b, gap=24):
    return solid_regions(a, b)

def _silhouette_change(pop, swap, blank, boxes):
    """Did the character in this region get re-cast? Measured as 1 - IoU of its ink body
    before and after, which is the only score that separates a re-cast from a re-render:
    an untouched character's body comes back within a pixel of itself (IoU ~0.97), while
    room jitter — thin outlines and paper grain — is not ink-and-changed at all and so
    never enters the score."""
    def body(img):
        return (img.mean(axis=2) < INK) & (np.abs(img - blank).max(axis=2) > 60)
    b1, b2 = body(pop), body(swap)
    out = []
    for x0, y0, x1, y1 in boxes:
        a, b = b1[y0:y1, x0:x1], b2[y0:y1, x0:x1]
        u = float((a | b).sum())
        out.append(round(1.0 - (float((a & b).sum()) / u if u else 1.0), 4))
    return out

def cmd_spots(era, pad=16):
    e = ERAS[era]
    d = era_dir(era)
    pop, bl = arr(f"{d}/populated.png"), arr(f"{d}/blank.png")
    H, W = pop.shape[:2]
    boxes, areas, bboxes = character_regions(pop, bl, open_r=e.get("open", 0))
    names = [s["id"] for s in e["spots"]]
    if len(boxes) != len(names):
        print(f"  ! {era}: {len(boxes)} bodies measured for {len(names)} spots "
              f"(areas {areas}) — keeping the largest {len(names)} in x-order")
        idx = sorted(sorted(range(len(boxes)), key=lambda i: -areas[i])[:len(names)],
                     key=lambda i: boxes[i][0])
        boxes = [boxes[i] for i in idx]; bboxes = [bboxes[i] for i in idx]
    # THE FOUNDER IS IDENTIFIED, NOT ASSUMED: it is the region the three re-cast edits
    # actually re-drew. If that disagrees with the declared spot the scene is wrong and
    # says so, rather than cutting three patches of somebody else.
    # THE FOUNDER'S REGION MUST HOLD ALL FOUR RENDITIONS. It is measured on the populated
    # render, where the founder is the hacker and has a laptop — but the consultant
    # arrives with a rolling suitcase on the floor beside them and the hustler with a cup
    # set down next to them. A region sized to the hacker slices those in half, and a
    # prop cut in half reads as a rendering bug in every assembly.
    fi = names.index(e["founder"])
    scores = []
    for who in ("hustler", "pm", "consultant"):
        p = f"{era_dir(era,'src')}/swap_{who}.png"
        if not intact(p):
            continue
        sw = arr(p)
        scores.append(_silhouette_change(pop, sw, bl, boxes))
        sb, _, _ = character_regions(sw, bl, open_r=e.get("open", 0))
        f0 = boxes[fi]
        over = [(min(f0[2], b[2]) - max(f0[0], b[0])) * (min(f0[3], b[3]) - max(f0[1], b[1]))
                for b in sb]
        if over and max(over) > 0:
            b = sb[int(np.argmax(over))]
            # clamped: a swap render sometimes measures two characters as one blob, and
            # an unclamped union then stretched the founder's region across the whole
            # frame and swallowed the neighbour. A prop set down beside a character is
            # never more than ~140px away.
            g = 140
            boxes[fi] = [max(min(f0[0], b[0]), f0[0] - g), max(min(f0[1], b[1]), f0[1] - g),
                         min(max(f0[2], b[2]), f0[2] + g), min(max(f0[3], b[3]), f0[3] + g)]
    mean = [float(np.mean([s[i] for s in scores])) for i in range(len(boxes))] if scores else []
    hit = names[int(np.argmax(mean))] if mean else e["founder"]
    if mean:
        print(f"  body change per region {dict(zip(names, [round(m,4) for m in mean]))} "
              f"-> founder is '{hit}', declared '{e['founder']}'")
    spots = {}
    for n, b in zip(names, boxes):
        x0, y0, x1, y1 = b
        spots[n] = [max(0, x0 - pad), max(0, y0 - pad), min(W, x1 + pad), min(H, y1 + pad)]
    out = {"scene": era, "size": [W, H], "founder_spot": e["founder"],
           "spots": spots, "bodies": dict(zip(names, bboxes)),
           "cast": {s["id"]: [s["who"]] + (["hustler", "pm", "consultant"]
                                           if s["id"] == e["founder"] else [])
                    for s in e["spots"]},
           "measured_bodies": len(areas), "expected_spots": len(names),
           "body_change_per_region": dict(zip(names, [round(m, 4) for m in mean])),
           "founder_by_measurement": hit}
    json.dump(out, open(f"{d}/spots.json", "w"), indent=1)
    print(json.dumps({"spots": spots, "measured": len(areas), "expected": len(names)}))

def cmd_swapcheck(era):
    """A re-cast may only change the founder — measured, then READ.

    Be honest about what a number can prove here. Seedream's edit re-renders the whole
    picture, so 6-11% of the frame differs before anyone has been re-cast: every outline
    lands a pixel or two off and the paper grain is redrawn. Three scores were tried
    against that floor — changed pixels, thick silhouette change, newly occupied pixels,
    new saturated prop pixels — and all four eras that a human read as PERFECT scored in
    the same band as the one era that was broken. The reason is structural: a re-cast
    swaps one solid ink-black bean for another of the same size, so the real change is a
    few hundred prop pixels sitting inside a frame where tens of thousands of pixels
    moved for free.

    So this reports rather than judges. `founder_body_iou_change` is the trustworthy
    column — the founder's own silhouette must actually have changed — and the four-panel
    contact sheet next to it is the gate. That read is what caught the office scene
    handing the laser pointer to the wrong creature."""
    d = era_dir(era)
    sp = json.load(open(f"{d}/spots.json"))
    names = list(sp["spots"].keys())
    boxes = [sp["spots"][n] for n in names]
    f = sp["founder_spot"]
    pop = arr(f"{d}/populated.png")
    bl = arr(f"{d}/blank.png")
    ink_room = np.ones(pop.shape[:2], bool)
    for x0, y0, x1, y1 in boxes:
        ink_room[y0:y1, x0:x1] = False
    rep = {}
    for who in ("hustler", "pm", "consultant"):
        p = f"{era_dir(era,'src')}/swap_{who}.png"
        if not intact(p):
            rep[who] = {"verdict": "MISSING"}
            continue
        sw = arr(p)
        per = _silhouette_change(pop, sw, bl, boxes)
        # THE PROP TEST. Props are the only thing that tells two characters apart, so a
        # re-cast IS a prop change, and a broken re-cast is a prop handed to the wrong
        # creature. Props are strongly coloured against a near-neutral room, so scoring
        # only saturated pixels that are new against BOTH the populated render and the
        # blank plate skips the whole-frame linework jitter that swamps a plain
        # difference (every edit re-renders the room; 6-11% of the frame moves before
        # anyone has been re-cast).
        def sat(img):
            return (img.max(axis=2) - img.min(axis=2)) > 55
        newprop = _erode(_dilate(sat(sw) & (np.abs(sw - pop).max(axis=2) > 70)
                                 & (np.abs(sw - bl).max(axis=2) > 70), 2), 3)
        tot = int(newprop.sum())
        inside = np.zeros_like(newprop)
        x0, y0, x1, y1 = sp["spots"][f]
        inside[y0:y1, x0:x1] = True
        pct = round(float((newprop & inside).sum()) / tot * 100, 2) if tot else 0.0
        _, stray = keep_big(newprop & ~inside, 900)
        byname = dict(zip(names, per))
        rep[who] = {"founder_body_iou_change": byname[f],
                    "other_bodies_iou_change": {n: v for n, v in byname.items() if n != f},
                    "new_prop_px_inside_founder_pct": pct, "new_prop_px": tot,
                    "new_prop_blobs_outside_founder": [c["box"] for c in stray][:5],
                    "founder_was_recast": byname[f] >= 0.08}
        print(who, json.dumps(rep[who]), flush=True)
    json.dump(rep, open(f"{d}/swap_report.json", "w"), indent=1)
    return 0 if all(v.get("founder_was_recast") for v in rep.values()) else 1

def cmd_sheet(era):
    """The four renditions of a scene side by side — the read that gates a swap."""
    d = era_dir(era)
    ims = [Image.open(f"{d}/populated.png")]
    for w in ("hustler", "pm", "consultant"):
        p = f"{d}/src/swap_{w}.png"
        if intact(p):
            ims.append(Image.open(p))
    sheet = Image.new("RGB", (700, 465 * len(ims)), "white")
    for i, im in enumerate(ims):
        sheet.paste(im.resize((700, 465)), (0, 465 * i))
    out = f"{era_dir(era, 'assembly')}/_swap_sheet.png"
    sheet.save(out)
    print(out)

# ---------------------------------------------------------------- D: patches

def _cut(source, blank, box, body_box=None, other_bodies=(), min_px=MIN_COMPONENT):
    x0, y0, x1, y1 = box
    src = source[y0:y1, x0:x1]
    bl = blank[y0:y1, x0:x1]
    m = change_mask(src, bl)
    keep, comps = keep_big(m, min_px)
    if not comps:
        return None, None
    # THE BODY THIS PATCH IS FOR. Two spots' regions can overlap once each is grown to
    # hold its props, so "the biggest ink blob in the crop" is not good enough — it can
    # be the neighbour. The spot's measured body box picks the right one.
    ink = keep & (src.mean(axis=2) < INK)
    lab_i, ki = _label(ink)
    if ki:
        si = np.bincount(lab_i.ravel()); si[0] = 0
        pick = int(si.argmax())
        if body_box:
            bx0, by0, bx1, by1 = (body_box[0] - x0, body_box[1] - y0,
                                  body_box[2] - x0, body_box[3] - y0)
            win = np.zeros(ink.shape, bool)
            win[max(0, by0):max(0, by1), max(0, bx0):max(0, bx1)] = True
            hits = np.bincount(lab_i[win], minlength=ki + 1); hits[0] = 0
            if hits.max() > 0:
                pick = int(hits.argmax())
        core = (lab_i == pick)
        body = _dilate(core, 8)
        body_area = int(si[pick])
        reach = _dilate(core, PROP_REACH)
        lab, k = _label(keep)
        s = np.bincount(lab.ravel()); s[0] = 0
        final = np.zeros_like(keep)
        for i in range(1, k + 1):
            if not s[i]:
                continue
            comp = (lab == i)
            # attached to the body, or big enough to BE a body part the furniture cut off
            # (a desk crossing a seated character splits it in two — dropping the legs is
            # worse than keeping a stray), or a prop of its own standing near it
            if (comp & body).any() or (s[i] >= 0.06 * body_area and (comp & reach).any()):
                final |= comp
        # BOUNDED to the character's own halo. A founder rendition is cut from a swap
        # render, which drifted from the blank TWICE — once into the swap, once into the
        # erase — so far more of its room disagrees, and any of that touching an elbow
        # dragged a wall line, a desk and a chair into the patch: the coworking hustler
        # came out 293k px against the 107k its resident twin measures, with 39k pixels of
        # hole-fill flooding a region enclosed by unrelated debris.
        keep = final & reach
        # NO NEIGHBOURS. A grown region overlaps the spot next door, and whatever of that
        # character rides along inside this patch appears even when the game assembles
        # this spot filled and that one EMPTY — the office assembly showed a disembodied
        # arm still holding its marker at the whiteboard. So nothing inside another
        # spot's region survives unless it is within arm's length of THIS body. Cutting
        # on the neighbour's ink alone was not enough: it left their yellow clipboard.
        forbid = np.zeros(src.shape[:2], bool)
        for ob in other_bodies:
            fx0, fy0, fx1, fy1 = ob[0] - x0, ob[1] - y0, ob[2] - x0, ob[3] - y0
            forbid[max(0, fy0):max(0, fy1), max(0, fx0):max(0, fx1)] = True
        # protected: this spot's own measured BODY box, padded. Protecting `dilate(core,
        # 45)` instead was not enough — a founder's head reaching within 45px of the
        # boundary re-opened the neighbour's whole column, and the garage kept a black
        # sliver of the soldering cofounder in the founder's patch.
        if body_box:
            mine = np.zeros(src.shape[:2], bool)
            g = 25
            mine[max(0, body_box[1] - y0 - g):max(0, body_box[3] - y0 + g),
                 max(0, body_box[0] - x0 - g):max(0, body_box[2] - x0 + g)] = True
            forbid &= ~mine
        keep &= ~forbid
        # and a detached fragment sitting in somebody else's region is theirs, not ours
        lab2, k2 = _label(keep)
        if k2:
            s2 = np.bincount(lab2.ravel()); s2[0] = 0
            attached = np.zeros(k2 + 1, bool)
            for i in np.unique(lab2[body]):
                if i:
                    attached[i] = True
            drop = np.zeros(k2 + 1, bool)
            for i in range(1, k2 + 1):
                if s2[i] and not attached[i] and (lab2 == i)[forbid].any():
                    drop[i] = True
            keep &= ~drop[lab2]
    else:
        forbid = np.zeros(src.shape[:2], bool)
    ys, xs = np.nonzero(keep)
    if not len(ys):
        return None, None
    cx0, cy0, cx1, cy1 = int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1
    sub = keep[cy0:cy1, cx0:cx1]
    rgb = src[cy0:cy1, cx0:cx1]
    # INTERIOR HOLES: a difference cut drops every pixel where the character matched what
    # was behind it — which silently deletes white eyes in front of a white board. Any
    # transparent island that does not touch the patch edge is interior: make it opaque
    # and take its colour from the render the patch was cut from.
    holes = ~sub
    lab_h, kh = _label(holes)
    border = set(np.unique(np.concatenate([lab_h[0], lab_h[-1], lab_h[:, 0], lab_h[:, -1]])))
    sub_forbid = forbid[cy0:cy1, cx0:cx1]
    filled = 0
    for i in range(1, kh + 1):
        if i in border:
            continue
        comp = (lab_h == i)
        if (comp & sub_forbid).any():
            continue
        sub |= comp
        filled += int(comp.sum())
    out = np.zeros((cy1 - cy0, cx1 - cx0, 4), np.uint8)
    out[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    out[..., 3] = np.where(sub, 255, 0)
    meta = {"offset": [x0 + cx0, y0 + cy0], "size": [cx1 - cx0, cy1 - cy0],
            "px": int(sub.sum()), "holes_filled": filled}
    return out, meta

def cmd_patches(era):
    d = era_dir(era)
    sp = json.load(open(f"{d}/spots.json"))
    blank = arr(f"{d}/blank.png")
    pop = arr(f"{d}/populated.png")
    pdir = era_dir(era, "patches")
    resident = {s["id"]: s["who"] for s in ERAS[era]["spots"]}
    out = {}
    for spot, box in sp["spots"].items():
        for who in sp["cast"][spot]:
            # the resident cast is cut from the populated render; a re-cast founder from
            # the swap render that produced them
            src = pop if who == resident[spot] else arr(f"{era_dir(era,'src')}/swap_{who}.png")
            img, meta = _cut(src, blank, box, sp.get("bodies", {}).get(spot),
                             [b for n, b in sp["spots"].items() if n != spot])
            if img is None:
                print(f"  {spot}__{who}: NOTHING CUT")
                continue
            p = f"{pdir}/{spot}__{who}.png"
            Image.fromarray(img).save(p)
            meta["spot"] = spot; meta["who"] = who
            out[f"{spot}__{who}"] = meta
            print(f"  {spot}__{who}: {meta['size']} at {meta['offset']} "
                  f"({meta['px']} px, {meta['holes_filled']} holes filled)", flush=True)
    json.dump(out, open(f"{d}/patches.json", "w"), indent=1)
    print(f"{era}: {len(out)} patches")

def cmd_eyes(era):
    d = era_dir(era)
    pat = json.load(open(f"{d}/patches.json"))
    out = {}
    for name, meta in pat.items():
        a = rgba(f"{era_dir(era,'patches')}/{name}.png")
        opaque = a[..., 3] > 0
        ink = opaque & (a[..., :3].mean(axis=2) < 70)
        lab, k = _label(ink)
        if k == 0:
            out[name] = []
            continue
        sizes = np.bincount(lab.ravel()); sizes[0] = 0
        body = (lab == sizes.argmax())
        ys = np.argwhere(body)
        top, bot = int(ys[:, 0].min()), int(ys[:, 0].max())
        head = np.zeros(a.shape[:2], bool)
        head[top:top + int((bot - top) * 0.50)] = True       # the top half of the BODY
        eyes = eye_blobs(a, mask=head, max_area=max(900, int(0.05 * body.sum())))
        out[name] = eyes[:2]
        if len(out[name]) == 2:      # a pair sits at nearly the same height
            cy = [(e[1] + e[3]) / 2 for e in out[name]]
            if abs(cy[0] - cy[1]) > 0.30 * (bot - top):
                out[name] = out[name][:1]
        print(f"  {name}: {len(out[name])} eye(s) {out[name]}", flush=True)
    json.dump(out, open(f"{d}/eyes.json", "w"), indent=1)

# ---------------------------------------------------------------- assembly

def cmd_assemble(era, outname, pairs):
    d = era_dir(era)
    pat = json.load(open(f"{d}/patches.json"))
    base = np.asarray(Image.open(f"{d}/blank.png").convert("RGB")).astype(np.float32)
    H, W = base.shape[:2]
    for spot, who in pairs:
        if who in ("", "empty", "none"):
            continue
        name = f"{spot}__{who}"
        assert name in pat, f"no patch {name}"
        p = rgba(f"{era_dir(era,'patches')}/{name}.png")
        ox, oy = pat[name]["offset"]
        h, w = p.shape[:2]
        x0, y0, x1, y1 = max(0, ox), max(0, oy), min(W, ox + w), min(H, oy + h)
        sub = p[y0 - oy:y1 - oy, x0 - ox:x1 - ox]
        al = sub[..., 3:4].astype(np.float32) / 255.0
        base[y0:y1, x0:x1] = base[y0:y1, x0:x1] * (1 - al) + sub[..., :3] * al
    outp = f"{era_dir(era,'assembly')}/{outname}.png"
    Image.fromarray(np.clip(base, 0, 255).astype(np.uint8)).save(outp)
    print(outp)

# ---------------------------------------------------------------- E: ambient

def cmd_ambient(era, tries=3):
    d = era_dir(era)
    mp4 = f"{d}/src/ambient.mp4"
    os.makedirs(f"{d}/src", exist_ok=True)
    if os.path.exists(mp4) and os.path.getsize(mp4) > 100_000:
        print(f"{era}: ambient.mp4 already on disk")
        return
    script = json.load(open(f"{d}/ambient.json"))
    small = f"{d}/src/_i2v.png"
    subprocess.run(["sips", "-Z", "1024", f"{d}/blank.png", "--out", small], capture_output=True)
    data = "data:image/png;base64," + base64.b64encode(open(small, "rb").read()).decode()
    key = _key("atlas-key.txt")
    last = ""
    for attempt in range(1, tries + 1):
        try:
            r = _post_json(f"{ATLAS}/api/v1/model/generateVideo",
                           {"model": "bytedance/seedance-2.5/image-to-video",
                            "prompt": script["motion_prompt"], "image": data, "last_image": data,
                            "duration": 4, "resolution": "720p", "ratio": "adaptive",
                            "generate_audio": False, "watermark": False, "output_format": "mp4"},
                           {"Authorization": f"Bearer {key}"})
            jid = r["data"]["id"]
            for _ in range(150):
                time.sleep(5)
                req = urllib.request.Request(f"{ATLAS}/api/v1/model/prediction/{jid}",
                                             headers={"Authorization": f"Bearer {key}", **UA})
                st = json.load(urllib.request.urlopen(req, timeout=60))["data"]
                if st["status"] in ("completed", "succeeded"):
                    _fetch((st["outputs"] or [""])[0], mp4)
                    print(f"{era}: ambient.mp4 saved", flush=True)
                    return
                if st["status"] == "failed":
                    raise IOError("model failed: %s" % str(st.get("error"))[:200])
            raise IOError("timed out")
        except Exception as ex:
            last = f"{type(ex).__name__}: {ex}"
            print(f"  attempt {attempt}/{tries}: {last}", flush=True)
            if attempt < tries:
                time.sleep(8 * attempt)
    raise SystemExit(f"{era}: ambient failed: {last}")

def cmd_deltas(era, span=None, max_drop=10):
    """mp4 -> 48 frames at 12fps -> measure the loop seam and drop the drifted opening
    frames -> additive deltas, gated to the boxes the scene's script says may change."""
    d = era_dir(era)
    script = json.load(open(f"{d}/ambient.json"))
    W, H = Image.open(f"{d}/blank.png").size
    fdir = era_dir(era, "src", "frames")
    for f in os.listdir(fdir):
        os.remove(os.path.join(fdir, f))
    subprocess.run(["ffmpeg", "-hide_banner", "-v", "error", "-y", "-i", f"{d}/src/ambient.mp4",
                    "-vf", f"fps=12,scale={W}:{H}:flags=lanczos", f"{fdir}/f_%03d.png"], check=True)
    names = sorted(os.listdir(fdir))
    F = np.stack([arr(f"{fdir}/{n}") for n in names]).astype(np.float32)
    n = len(F)
    span = span or min(47, n - 1)
    best = None
    for i in range(0, min(max_drop, n - span)):
        j = i + span
        if j > n:
            break
        s = float(np.abs(F[i] - F[j - 1]).mean())
        if best is None or s < best[0]:
            best = (s, i, j)
    if best is None:
        best = (float(np.abs(F[0] - F[-2]).mean()), 0, n - 1)
    seam, i0, i1 = best
    seam_raw = float(np.abs(F[0] - F[n - 1]).mean())
    used = F[i0:i1][:span]
    base = used[0]
    pos = np.clip(used - base[None], 0, None)         # additive contract: only ever adds
    clip_loss = float(np.clip(base[None] - used, 0, None).mean())
    gate = np.zeros((H, W), np.float32)
    boxes = script.get("gate", [])
    if boxes:
        for g in boxes:
            x0, y0, x1, y1 = g["box"]
            m = np.zeros((H, W), np.float32)
            m[max(0, y0):y1, max(0, x0):x1] = 1.0
            m = np.asarray(Image.fromarray((m * 255).astype(np.uint8))
                           .filter(ImageFilter.GaussianBlur(g.get("feather", 30) / 2.0))
                           ).astype(np.float32) / 255.0
            gate = np.maximum(gate, m * g.get("gain", 1.0))
    else:
        gate[:] = 1.0
    posg = pos * gate[None, :, :, None]
    # SOFT KNEE. Ambient is LIGHT, and a light delta lives around 5-20; a peak of 242
    # means the model moved a drawn thing — a leaf swinging clear of the wall, a bulb
    # redrawn — and adding 242 to a pale room blows it to white and leaves a ghost of the
    # thing in its old place. A hard clamp would flat-top the gentle light too, so the
    # curve is exponential: under ~15 it is within a percent of the identity, and it
    # asymptotes at the cap however hard the model pushed.
    cap = float(script.get("delta_cap", 40))
    peak_before = int(posg.max())
    posg = cap * (1.0 - np.exp(-posg / cap))
    ddir = era_dir(era, "ambient")
    for f in os.listdir(ddir):
        os.remove(os.path.join(ddir, f))
    for k in range(len(posg)):
        Image.fromarray(np.clip(posg[k], 0, 255).astype(np.uint8)).save(f"{ddir}/d_{k:02d}.png")
    moving = float(((posg.max(axis=3) > 6).mean(axis=(1, 2)).max()) * 100)
    rep = {"frames_extracted": n, "window": [i0, i1], "frames_kept": len(posg),
           "dropped_drifted_frames": i0,
           "seam_first_to_last_raw": round(seam_raw, 3),
           "seam_after_window": round(seam, 3),
           "seam_after_gate": round(float(np.abs(posg[0] - posg[-1]).mean()), 3),
           "clamp_loss_mean": round(clip_loss, 3),
           "moving_pixels_pct": round(moving, 2),
           "peak_delta_raw": peak_before, "peak_delta": int(posg.max()),
           "per_box_peak": {g["name"]: int(posg[:, max(0, g["box"][1]):g["box"][3],
                                                max(0, g["box"][0]):g["box"][2]].max())
                            for g in boxes}}
    script["report"] = rep
    json.dump(script, open(f"{d}/ambient.json", "w"), indent=1)
    print(json.dumps(rep))

# ---------------------------------------------------------------- report

def cmd_verify():
    """Every PNG under patch_scenes must be a complete, decodable image.

    scene_pipeline's verify walks assets/scenes; this is the same gate over the tree
    this tool writes. A half-downloaded PNG decodes as a normal file to Godot's importer
    and only shows up as a cut-off room in the game, which is why it runs as a gate
    rather than by eye."""
    bad, total = [], 0
    for dirpath, _, files in os.walk(ROOT):
        for f in sorted(files):
            if not f.lower().endswith(".png"):
                continue
            p = os.path.join(dirpath, f)
            total += 1
            if not intact(p):
                bad.append((os.path.relpath(p, ROOT), os.path.getsize(p)))
    print("verify: %d PNGs scanned, %d intact, %d DAMAGED" % (total, total - len(bad), len(bad)))
    for rel, n in bad:
        print("  DAMAGED %-58s %8d bytes" % (rel, n))
    # and the tables every scene must ship complete
    for era in ERAS:
        d = era_dir(era)
        miss = [f for f in ("populated.png", "blank.png", "spots.json", "patches.json",
                            "eyes.json", "ambient.json") if not os.path.exists(f"{d}/{f}")]
        sp = json.load(open(f"{d}/spots.json")) if os.path.exists(f"{d}/spots.json") else {"cast": {}}
        want = sum(len(v) for v in sp.get("cast", {}).values())
        have = len([f for f in os.listdir(f"{d}/patches")]) if os.path.isdir(f"{d}/patches") else 0
        deltas = len(os.listdir(f"{d}/ambient")) if os.path.isdir(f"{d}/ambient") else 0
        print("  %-10s patches %d/%d  deltas %d  %s"
              % (era, have, want, deltas, ("MISSING " + ",".join(miss)) if miss else "tables complete"))
    return 1 if bad else 0

def cmd_report(era):
    d = era_dir(era)
    def n(p):
        return len([f for f in os.listdir(p) if f.endswith(".png")]) if os.path.isdir(p) else 0
    r = {"era": era,
         "populated": intact(f"{d}/populated.png"), "blank": intact(f"{d}/blank.png"),
         "swaps": [w for w in ("hustler", "pm", "consultant")
                   if intact(f"{d}/src/swap_{w}.png")],
         "patches": n(f"{d}/patches"), "ambient_deltas": n(f"{d}/ambient")}
    for f in ("empty_report.json", "swap_report.json"):
        if os.path.exists(f"{d}/{f}"):
            r[f[:-5]] = json.load(open(f"{d}/{f}"))
    print(json.dumps(r))

# ---------------------------------------------------------------- main

if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "refs":
        cmd_refs()
    elif cmd == "plan":
        for k, (p, r) in prompts(sys.argv[2]).items():
            print(f"{k:16s} refs={r}")
    elif cmd == "push":
        cmd_push(sys.argv[2])
    elif cmd == "edit":
        cmd_edit(sys.argv[2], sys.argv[3], "--force" in sys.argv)
    elif cmd == "empty":
        sys.exit(cmd_empty(sys.argv[2]))
    elif cmd == "spots":
        cmd_spots(sys.argv[2])
    elif cmd == "swapcheck":
        sys.exit(cmd_swapcheck(sys.argv[2]))
    elif cmd == "sheet":
        cmd_sheet(sys.argv[2])
    elif cmd == "patches":
        cmd_patches(sys.argv[2])
    elif cmd == "eyes":
        cmd_eyes(sys.argv[2])
    elif cmd == "assemble":
        cmd_assemble(sys.argv[2], sys.argv[3],
                     [tuple(a.split("=", 1)) for a in sys.argv[4:]])
    elif cmd == "ambient":
        cmd_ambient(sys.argv[2])
    elif cmd == "deltas":
        cmd_deltas(sys.argv[2])
    elif cmd == "verify":
        sys.exit(cmd_verify())
    elif cmd == "report":
        cmd_report(sys.argv[2])
    else:
        raise SystemExit(__doc__)
