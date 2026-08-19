#!/usr/bin/env python3
"""Composite blank writable surfaces into a room, then annotate them (A-05).

`clear_surfaces` can blank a drawn surface but cannot create one, and the stages
predate the WRITING SURFACES clause — their only writable face is the room's one
big board. These sprites add the rest.

Placement is measured, not eyeballed: candidate sites are scored by edge energy
(same idea as zone_audit) and the FLATTEST patch wins, because flat means blank
wall or empty desk. Sites are constrained to the UI stage band, kept out of the
rails and the calm top/bottom, and kept off the crew marks so the cast does not
stand in front of the numbers.

The paste goes into scene.png AND every anim frame at the same coordinates. That
is safe here because these hang on static wall and desk areas and the loops only
move bulbs, dust, leaves and screen glow — nothing behind a paste site.

  python3 tools/place_surfaces.py stage_garage
"""
import json, os, sys
from PIL import Image, ImageFilter, ImageStat

GAME = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCENES = f"{GAME}/assets/scenes"
CANVAS = (1536, 1024)

# target size on canvas, and the writable FACE as a fraction of the sprite
# (excluding the clip, the frame, the tape tab, the curled corner)
# Each surface carries its own y-band, because flatness alone is not enough:
# the flattest patch in a room is usually the middle of the floor, and a
# clipboard hung in mid-air or a sticky cluster lying on the floor reads as
# litter. Wall items go on the wall, the clipboard goes on a furniture top.
SURFACES = {
    "inventory": {"sprite": "surf_inventory", "w": 156, "face": (0.10, 0.08, 0.90, 0.92),
                  "lines": 4, "band": (150, 545)},
    "wallchart": {"sprite": "surf_wallchart", "w": 116, "face": (0.06, 0.13, 0.94, 0.95),
                  "lines": 2, "band": (150, 430)},
    "ledger":    {"sprite": "surf_ledger",    "w": 158, "face": (0.07, 0.19, 0.93, 0.92),
                  "lines": 2, "band": (150, 430)},
    "sticky":    {"sprite": "surf_sticky",    "w": 132, "face": (0.10, 0.12, 0.90, 0.86),
                  "lines": 2, "band": (150, 430)},
}
# horizontal extent: inside the side rails
BAND = (132, 205, 1400, 790)

# Rooms whose wall is not continuous. The hq is a glass box: its middle is a
# window wall onto the skyline, and flatness happily scores clean sky as a
# perfect site — which hung the corkboard in mid-air over the city. Only solid
# wall is eligible there.
SOLID_WALL = {
    "stage_hq": [(126, 330), (1170, 1400)],
}


def _edges(path):
    im = Image.open(path).convert("L").resize(CANVAS, Image.LANCZOS)
    W, H = im.size
    pad = Image.new("L", (W + 2, H + 2))
    pad.paste(im, (1, 1))
    pad.paste(im.crop((0, 0, W, 1)), (1, 0))
    pad.paste(im.crop((0, H - 1, W, H)), (1, H + 1))
    pad.paste(im.crop((0, 0, 1, H)), (0, 1))
    pad.paste(im.crop((W - 1, 0, W, H)), (W + 1, 1))
    return pad.filter(ImageFilter.FIND_EDGES).crop((1, 1, W + 1, H + 1))


def _overlaps(a, b, pad=14):
    return not (a[0] + a[2] + pad <= b[0] or b[0] + b[2] + pad <= a[0] or
                a[1] + a[3] + pad <= b[1] or b[1] + b[3] + pad <= a[1])


def erase(room, rect):
    """Paint a rect out of the still and every frame, using the colour AROUND it.

    Needed because compositing is destructive: a surface pasted at a bad site is
    already in 49 files. Sampling a ring OUTSIDE the rect (not inside, which is
    what clear_surfaces does) fills it with the surrounding wall or floor rather
    than with the mistake's own colour.
    """
    d = f"{SCENES}/{room}"
    x, y, w, h = rect
    targets = [f"{d}/scene.png"] + sorted(
        f"{d}/anim/{f}" for f in os.listdir(f"{d}/anim") if f.endswith(".png"))
    for t in targets:
        im = Image.open(t).convert("RGB")
        ow, oh = im.size
        work = im.resize(CANVAS, Image.LANCZOS) if (ow, oh) != CANVAS else im
        px = work.load()
        ring = []
        for k in range(0, w, 4):
            for yy in (max(0, y - 6), min(CANVAS[1] - 1, y + h + 6)):
                ring.append(px[min(CANVAS[0] - 1, x + k), yy])
        for k in range(0, h, 4):
            for xx in (max(0, x - 6), min(CANVAS[0] - 1, x + w + 6)):
                ring.append(px[xx, min(CANVAS[1] - 1, y + k)])
        ring.sort(key=lambda c: sum(c))
        fill = ring[len(ring) // 2]
        patch = Image.new("RGB", (w, h), fill)
        work.paste(patch, (x, y))
        out = work.resize((ow, oh), Image.LANCZOS) if (ow, oh) != CANVAS else work
        out.save(t)
    print(f"{room}: erased {w}x{h} at ({x},{y}) with rgb{fill} across {len(targets)} image(s)")


def place(room, only=None, preview=False):
    d = f"{SCENES}/{room}"
    layout = json.load(open(f"{d}/layout.json"))
    em = _edges(f"{d}/scene.png")

    # Occluders and existing surfaces are HARD blocks — a surface behind the desk
    # front, or on top of a face another lane already annotated, is simply wrong.
    # Crew marks are a SOFT penalty instead: a board hung high on a wall with a
    # figure standing in front of its lowest edge is normal, and hard-blocking
    # them leaves no legal wall at all (in the garage every gap between crew
    # boxes is 65px wide), which pushed the corkboard onto the pegboard.
    blocked, soft = [], []
    for k, v in layout.items():
        if not isinstance(v, dict):
            continue
        if v.get("kind") == "occluder":
            blocked.append((v["x"], v["y"], v["w"], v["h"]))
        elif v.get("kind") == "crew_mark":
            soft.append((v["x"], v["y"], v["w"], v["h"]))
    for k, v in layout.get("write_surfaces", {}).items():
        blocked.append((v["x"], v["y"], v["w"], v["h"]))

    chosen = {}
    names = only or list(SURFACES)
    for name in names:
        spec = SURFACES[name]
        sp = Image.open(f"{SCENES}/{spec['sprite']}/sprite.png").convert("RGBA")
        w = spec["w"]
        h = max(1, round(sp.height * w / sp.width))
        y0, y1 = spec["band"]
        best = None
        walls = SOLID_WALL.get(room)
        for y in range(y0, max(y0 + 1, y1 - h), 16):
            for x in range(BAND[0], BAND[2] - w, 16):
                if walls and not any(x >= a and x + w <= b for a, b in walls):
                    continue
                rect = (x, y, w, h)
                if any(_overlaps(rect, b) for b in blocked + list(chosen.values())):
                    continue
                # score the patch plus a margin, so it does not butt against detail
                crop = em.crop((max(0, x - 8), max(0, y - 8),
                                min(CANVAS[0], x + w + 8), min(CANVAS[1], y + h + 8)))
                e = ImageStat.Stat(crop).mean[0]
                # soft penalty: how much of the surface a standing figure covers
                for s in soft:
                    ox = max(0, min(x + w, s[0] + s[2]) - max(x, s[0]))
                    oy = max(0, min(y + h, s[1] + s[3]) - max(y, s[1]))
                    e += 26.0 * (ox * oy) / float(w * h)
                if best is None or e < best[0]:
                    best = (e, rect)
        if best is None:
            print(f"{room}: {name} — no free site")
            continue
        chosen[name] = best[1]
        print(f"{room}: {name:10s} at {best[1]} flatness {best[0]:.1f}")

    sprites_pv = {n: Image.open(f"{SCENES}/{SURFACES[n]['sprite']}/sprite.png").convert("RGBA")
                  for n in chosen}
    if preview:
        # never paste into 49 files unchecked — render the sites on a copy first
        pv = Image.open(f"{d}/scene.png").convert("RGBA").resize(CANVAS, Image.LANCZOS)
        for n, (x, y, w, h) in chosen.items():
            pv.alpha_composite(sprites_pv[n].resize((w, h), Image.LANCZOS), (x, y))
        pv.convert("RGB").save(f"/tmp/pv_{room}.png")
        print(f"{room}: PREVIEW ONLY -> /tmp/pv_{room}.png (nothing written)")
        return chosen

    # paste into the still and every frame
    targets = [f"{d}/scene.png"] + sorted(
        f"{d}/anim/{f}" for f in os.listdir(f"{d}/anim") if f.endswith(".png")) \
        if os.path.isdir(f"{d}/anim") else [f"{d}/scene.png"]
    sprites = {n: Image.open(f"{SCENES}/{SURFACES[n]['sprite']}/sprite.png").convert("RGBA")
               for n in chosen}
    for t in targets:
        im = Image.open(t).convert("RGBA")
        ow, oh = im.size
        work = im.resize(CANVAS, Image.LANCZOS) if (ow, oh) != CANVAS else im
        for n, (x, y, w, h) in chosen.items():
            work.alpha_composite(sprites[n].resize((w, h), Image.LANCZOS), (x, y))
        out = work.resize((ow, oh), Image.LANCZOS) if (ow, oh) != CANVAS else work
        out.convert("RGB").save(t)

    ws = layout.setdefault("write_surfaces", {})
    for n, (x, y, w, h) in chosen.items():
        fx0, fy0, fx1, fy1 = SURFACES[n]["face"]
        ws[n] = {"x": round(x + w * fx0), "y": round(y + h * fy0),
                 "w": round(w * (fx1 - fx0)), "h": round(h * (fy1 - fy0)),
                 "rot": 0.0, "lines": SURFACES[n]["lines"], "align": "center"}
    json.dump(layout, open(f"{d}/layout.json", "w"), indent=1)
    print(f"{room}: {len(chosen)} composited into {len(targets)} image(s)")


def place_at(room, name, x, y, w):
    """Composite one surface at an explicit rect, for sites flatness cannot find.

    The hq is the case: it is a glass box, and the only believable places left
    for paper are ON the glass — which flatness rejects as 'not wall' and which
    no automatic rule would choose. Real offices stick notes to glass.
    """
    d = f"{SCENES}/{room}"
    layout = json.load(open(f"{d}/layout.json"))
    sp = Image.open(f"{SCENES}/{SURFACES[name]['sprite']}/sprite.png").convert("RGBA")
    h = max(1, round(sp.height * w / sp.width))
    targets = [f"{d}/scene.png"] + sorted(
        f"{d}/anim/{f}" for f in os.listdir(f"{d}/anim") if f.endswith(".png"))
    piece = sp.resize((w, h), Image.LANCZOS)
    for t in targets:
        im = Image.open(t).convert("RGBA")
        ow, oh = im.size
        work = im.resize(CANVAS, Image.LANCZOS) if (ow, oh) != CANVAS else im
        work.alpha_composite(piece, (x, y))
        out = work.resize((ow, oh), Image.LANCZOS) if (ow, oh) != CANVAS else work
        out.convert("RGB").save(t)
    fx0, fy0, fx1, fy1 = SURFACES[name]["face"]
    layout.setdefault("write_surfaces", {})[name] = {
        "x": round(x + w * fx0), "y": round(y + h * fy0),
        "w": round(w * (fx1 - fx0)), "h": round(h * (fy1 - fy0)),
        "rot": 0.0, "lines": SURFACES[name]["lines"], "align": "center"}
    json.dump(layout, open(f"{d}/layout.json", "w"), indent=1)
    print(f"{room}: {name} forced to ({x},{y}) {w}x{h} across {len(targets)} image(s)")


if __name__ == "__main__":
    args = sys.argv[1:]
    if args[1:2] == ["--at"]:
        place_at(args[0], args[2], int(args[3]), int(args[4]), int(args[5]))
    elif args[1:2] == ["--erase"]:
        erase(args[0], tuple(int(v) for v in args[2:6]))
    else:
        pv = "--preview" in args
        names = [a for a in args[1:] if not a.startswith("--")]
        place(args[0], names or None, preview=pv)
