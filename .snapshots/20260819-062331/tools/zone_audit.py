#!/usr/bin/env python3
"""UI SAFE-ZONE audit for RUNWAY! scenes (owner law 2026-08-18).

Every scene is a stage the UI sits on. Scenes are mixed resolution (1536 and
2048 wide), so every image is normalized to a fixed working width FIRST —
otherwise edge energies are not comparable between scenes.

The edge map is computed ONCE on the whole (replicate-padded) image and only
then sliced into zones. Filtering each crop separately is wrong: PIL leaves the
outer 1px ring of a 3x3 filter unfiltered, so a thin crop's mean is dominated by
its own border brightness — a blank cream HUD patch (57x9 px, 25% ring) scored
~59 on emptiness alone.

Verdict rule (coordinator spec):
  a zone is CLEAR if its ABSOLUTE edge energy is under the calibrated threshold.
  Ratio-to-scene-median is only a tiebreaker for zones just over the line.
  A quiet zone always passes, however quiet the rest of the scene is.

Usage:
  python3 tools/zone_audit.py                # all scenes
  python3 tools/zone_audit.py id1 id2 ...    # specific scenes
"""
import os, sys, statistics
from PIL import Image, ImageFilter, ImageStat

GAME = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCENES = f"{GAME}/assets/scenes"
WORK_W = 192  # every scene normalized to this width before measuring

ZONES = {
    "TOP":    (0.000, 0.000, 1.000, 0.098),
    "HUD":    (0.000, 0.000, 0.300, 0.070),
    "BOTTOM": (0.000, 0.874, 1.000, 1.000),
    "CTA":    (0.333, 0.874, 0.667, 1.000),
    "L_RAIL": (0.000, 0.098, 0.078, 0.874),
    "R_RAIL": (0.924, 0.098, 1.000, 0.874),
}
# Calibrated on knowns: good = select_stage_empty / select_exfaang_v2 /
# garage_starving_v2 / journal_page ; bad = coworking_thriving (HUD clutter),
# hq_steady (busy top+CTA), garage_v3 (shelf in right rail).
# Calm bands must be near-blank: knowns land <=12.7 good vs >=19 bad.
# Rails only bar CRITICAL SUBJECT MATTER, so a wall/floor junction crossing them
# is fine: knowns land <=35 good vs >=39.5 bad (garage_v3's shelf = 47.1).
LIMITS = {"TOP": 15, "HUD": 15, "BOTTOM": 15, "CTA": 15, "L_RAIL": 38, "R_RAIL": 38}
TIEBREAK = 1.15  # zone within 15% over the limit passes if quieter than scene median


def _edges(path):
    """Normalized whole-image edge map, no crop-border artifacts."""
    im = Image.open(path).convert("L")
    h = max(1, round(im.height * WORK_W / im.width))
    im = im.resize((WORK_W, h), Image.LANCZOS)
    W, H = im.size
    pad = Image.new("L", (W + 2, H + 2))
    pad.paste(im, (1, 1))
    pad.paste(im.crop((0, 0, W, 1)), (1, 0))
    pad.paste(im.crop((0, H - 1, W, H)), (1, H + 1))
    pad.paste(im.crop((0, 0, 1, H)), (0, 1))
    pad.paste(im.crop((W - 1, 0, W, H)), (W + 1, 1))
    for px, py, sx, sy in ((0, 0, 0, 0), (W + 1, 0, W - 1, 0),
                           (0, H + 1, 0, H - 1), (W + 1, H + 1, W - 1, H - 1)):
        pad.putpixel((px, py), im.getpixel((sx, sy)))
    return pad.filter(ImageFilter.FIND_EDGES).crop((1, 1, W + 1, H + 1))


def busy(img):
    if img.width < 1 or img.height < 1:
        return 0.0
    return ImageStat.Stat(img).mean[0]


def measure(scene_id):
    path = f"{SCENES}/{scene_id}/scene.png"
    if not os.path.exists(path):
        return None
    em = _edges(path)
    W, H = em.size
    vals = {n: busy(em.crop((int(x0 * W), int(y0 * H), int(x1 * W), int(y1 * H))))
            for n, (x0, y0, x1, y1) in ZONES.items()}
    med = statistics.median([busy(em.crop((bx * W // 8, by * H // 6,
                                          (bx + 1) * W // 8, (by + 1) * H // 6)))
                             for by in range(6) for bx in range(8)]) or 1e-6
    fails = []
    for n, v in vals.items():
        if v <= LIMITS[n]:
            continue
        if v <= LIMITS[n] * TIEBREAK and v < med:   # tiebreaker: quieter than typical
            continue
        fails.append(n)
    return {"id": scene_id, "vals": vals, "med": med, "fails": fails}


def all_ids():
    return sorted(d for d in os.listdir(SCENES)
                  if os.path.isdir(f"{SCENES}/{d}") and not d.startswith("_")
                  and os.path.exists(f"{SCENES}/{d}/scene.png"))


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    ids = args or all_ids()
    print(f"{'SCENE':<28}" + "".join(f"{z:>8}" for z in ZONES) + f"{'med':>7}   VERDICT")
    npass = 0
    for sid in ids:
        r = measure(sid)
        if r is None:
            print(f"{sid:<28}  (no scene.png)")
            continue
        cells = "".join(f"{r['vals'][z]:>8.1f}" for z in ZONES)
        ok = not r["fails"]
        npass += ok
        print(f"{sid:<28}{cells}{r['med']:>7.1f}   {'PASS' if ok else 'FAIL:' + ','.join(r['fails'])}")
    print(f"\n{npass}/{len(ids)} pass   limits " +
          " ".join(f"{k}<={v}" for k, v in LIMITS.items()))


if __name__ == "__main__":
    main()
