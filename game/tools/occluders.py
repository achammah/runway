#!/usr/bin/env python3
"""Build foreground occluder PNGs from decomposed cutouts (A-06).

An occluder is drawn ON TOP of a loop that already contains the same object, so
placement has to be pixel-accurate — a few px off and you get a ghosted double
edge instead of something to stand behind. Hand-measured rects are not that
accurate, which the first pass showed plainly.

But `place()`'s global search (1px steps across 11 scales over the whole frame)
ran over an hour on one room without finishing. So this does a SEEDED LOCAL
search: the authored rect says roughly where and how big, and we only search a
small window around it — coarse pass then a 1px refine. That is a few thousand
comparisons instead of tens of millions.

Two cautions learned the hard way:
  - decomposition does NOT return layers in the order of the numbered prompt, so
    the layer index for each name is discovered by score, not assumed.
  - cutouts predate clear_surfaces, so a cutout of a cleared face still carries
    the old scribble. Never build an occluder from an annotated surface.

  python3 tools/occluders.py stage_garage occ_workbench occ_desk occ_crate
"""
import json, os, sys
from PIL import Image

GAME = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def _score(cut, scene, ox, oy, pts):
    sp = scene.load()
    cp = cut.load()
    W, H = scene.size
    tot = 0
    for px, py in pts:
        x, y = ox + px, oy + py
        if not (0 <= x < W and 0 <= y < H):
            return 1e18
        a, b = cp[px, py], sp[x, y]
        tot += abs(a[0] - b[0]) + abs(a[1] - b[1]) + abs(a[2] - b[2])
    return tot / max(1, len(pts))


def _fit(cut, scene, rect):
    """Search scale and position in a window around the authored rect."""
    best = (1e18, None, None, None)
    base = rect["w"] / cut.width
    for sm in (0.80, 0.90, 1.00, 1.10, 1.20):
        sc = base * sm
        c = cut.resize((max(4, round(cut.width * sc)), max(4, round(cut.height * sc))),
                       Image.LANCZOS)
        cp = c.load()
        pts = [(x, y) for y in range(0, c.height, max(1, c.height // 14))
               for x in range(0, c.width, max(1, c.width // 14)) if cp[x, y][3] > 200]
        if len(pts) < 12:
            continue
        for step, span, seed in ((6, 42, (rect["x"], rect["y"])),):
            ox0, oy0 = seed
            for dy in range(-span, span + 1, step):
                for dx in range(-span, span + 1, step):
                    e = _score(c, scene, ox0 + dx, oy0 + dy, pts)
                    if e < best[0]:
                        best = (e, c, ox0 + dx, oy0 + dy)
    if best[1] is None:
        return best
    # 1px refine around the coarse winner
    e0, c, bx, by = best
    cp = c.load()
    pts = [(x, y) for y in range(0, c.height, max(1, c.height // 20))
           for x in range(0, c.width, max(1, c.width // 20)) if cp[x, y][3] > 200]
    for dy in range(-6, 7):
        for dx in range(-6, 7):
            e = _score(c, scene, bx + dx, by + dy, pts)
            if e < best[0]:
                best = (e, c, bx + dx, by + dy)
    return best


def cut(room, names):
    """Cut each occluder straight out of the scene at its authored rect.

    Matching a decomposed cutout back onto the art turned out to be the wrong
    problem three times over: the global search was too slow to finish, hand
    rects were not pixel-accurate enough to overlay a duplicate, and the seeded
    search rejects every candidate on colour (err 81-136) because the cutouts
    come back inpainted and upscaled, so they no longer match their own scene.

    None of that is necessary. The occluder is drawn over a loop that already
    contains the furniture, and the furniture does not move — so a rectangular
    crop of the scene at the authored rect IS the occluder, aligned perfectly by
    construction. The cast composites at a crew mark, this rect is drawn back on
    top, and whatever of the cast falls inside it goes behind the furniture.

    The rect must hug the furniture: anything else it covers is also redrawn.
    """
    d = f"{GAME}/assets/scenes/{room}"
    layout = json.load(open(f"{d}/layout.json"))
    scene = Image.open(f"{d}/scene.png").convert("RGBA").resize((1536, 1024), Image.LANCZOS)
    for name in names:
        r = layout.get(name)
        if not r:
            print(f"{name}: no authored rect, skipped")
            continue
        x, y, w, h = int(r["x"]), int(r["y"]), int(r["w"]), int(r["h"])
        scene.crop((x, y, x + w, y + h)).save(f"{d}/{name}.png")
        r.update({"placed": True, "z": "front", "source": "scene-crop"})
        r.pop("err", None)
        r.pop("from", None)
        print(f"{name:16s} cut {w}x{h} at ({x},{y}) from scene")
    json.dump(layout, open(f"{d}/layout.json", "w"), indent=1)


def build(room, names):
    d = f"{GAME}/assets/scenes/{room}"
    layout = json.load(open(f"{d}/layout.json"))
    scene = Image.open(f"{d}/scene.png").convert("RGB").resize((1536, 1024), Image.LANCZOS)
    # .endswith(".png") matters: Godot writes a layer_1.png.import sidecar next to
    # every texture, and a startswith-only glob feeds that to PIL and dies.
    layers = sorted(f for f in os.listdir(d)
                    if f.startswith("layer_") and f.endswith(".png")
                    and not f.startswith("layer_0"))
    cuts = {}
    for f in layers:
        im = Image.open(f"{d}/{f}").convert("RGBA")
        bb = im.getbbox()
        cuts[f] = im.crop(bb) if bb else im
    used = set()
    for name in names:
        rect = layout.get(name)
        if not rect:
            print(f"{name}: no authored rect, skipped")
            continue
        # layer order is not the prompt order — pick the cutout that fits best
        cands = []
        for f, cut in cuts.items():
            if f in used:
                continue
            e, c, x, y = _fit(cut, scene, rect)
            if c is not None:
                cands.append((e, f, c, x, y))
        if not cands:
            print(f"{name}: no candidate")
            continue
        e, f, c, x, y = min(cands, key=lambda t: t[0])
        used.add(f)
        c.save(f"{d}/{name}.png")
        rect.update({"x": x, "y": y, "w": c.width, "h": c.height,
                     "err": round(e, 1), "from": f, "placed": e < 60})
        print(f"{name:16s} <- {f} at ({x},{y}) {c.width}x{c.height} "
              f"err {e:.0f} {'OK' if e < 60 else 'REJECTED, too different'}")
    json.dump(layout, open(f"{d}/layout.json", "w"), indent=1)


if __name__ == "__main__":
    cut(sys.argv[1], sys.argv[2:])
