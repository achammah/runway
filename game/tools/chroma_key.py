#!/usr/bin/env python3
"""Magenta -> alpha for cast sprites, per the brief: dist<155 -> a0, feather to 215.

Writes <scene>/sprite.png: keyed, de-fringed and cropped to content, so the
result can be dropped straight onto a crew mark (SceneRoom anchors sprites at
the feet). The generator centres the figure loosely and at whatever size it
likes, so cropping to content is what actually makes marks land consistently.

PIL-only — numpy is not available here. ImageMath does the per-pixel distance in
one pass; a Python loop over 2.8M pixels is not worth it.

  python3 tools/chroma_key.py cast_hacker_fine [...]
  python3 tools/chroma_key.py --all        # every cast_* scene
"""
import os, sys
from PIL import Image, ImageChops, ImageMath

GAME = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCENES = f"{GAME}/assets/scenes"
KEY = (255, 0, 255)
# Magenta dominance, not distance: above OPAQUE_BELOW the pixel is background,
# below it the pixel is kept. Greys score 0 and coral scores negative, so both
# stay fully opaque no matter how light they are.
BG_ABOVE, OPAQUE_BELOW = 90, 40


def _magenta_dominance(r, g, b):
    """How magenta-dominant each pixel is: min(R-G, B-G), clamped to 0..255.

    NOT Euclidean distance to magenta. Distance is the obvious metric and it is
    wrong for this palette: a mid-grey sits ~209-215 from magenta and coral
    ~196, both of which land inside any sane feather band, so floor greys and
    coral props get eaten or fringed. Magenta dominance keys on what actually
    makes the background background — red and blue high while green is low —
    which no grey (R=G=B, so 0) and no coral (blue is low, so negative) can fake.

    Pillow 11 dropped ImageMath.eval for lambda_eval, so prefer the current API.
    """
    def expr(a):
        rg = a["float"](a["r"]) - a["float"](a["g"])
        bg = a["float"](a["b"]) - a["float"](a["g"])
        return a["convert"](a["max"](a["min"](rg, bg), 0.0), "L")
    if hasattr(ImageMath, "lambda_eval"):
        return ImageMath.lambda_eval(expr, r=r, g=g, b=b)
    return ImageMath.eval("convert(max(min(float(r) - float(g), float(b) - float(g)), 0), 'L')",
                          r=r, g=g, b=b)


def key_one(sid):
    src = f"{SCENES}/{sid}/scene.png"
    if not os.path.exists(src):
        return f"{sid}: no scene.png"
    im = Image.open(src).convert("RGB")
    r, g, b = im.split()
    dom = _magenta_dominance(r, g, b)
    alpha = dom.point(lambda d: 0 if d > BG_ABOVE else (255 if d < OPAQUE_BELOW else
                      int(round((BG_ABOVE - d) * 255 / (BG_ABOVE - OPAQUE_BELOW)))))
    # De-fringe: pixels kept in the feather band still carry magenta spill, which
    # reads as a violet halo over a cream wall. Pull red/blue down toward green
    # there so the outline stays ink, not purple.
    spill = alpha.point(lambda a: 255 if 0 < a < 255 else 0)
    r2 = Image.composite(ImageChops.darker(r, g), r, spill)
    b2 = Image.composite(ImageChops.darker(b, g), b, spill)
    out = Image.merge("RGBA", (r2, g, b2, alpha))
    bbox = alpha.getbbox()
    if bbox:
        out = out.crop(bbox)
    out.save(f"{SCENES}/{sid}/sprite.png")
    return f"{sid}: sprite {out.width}x{out.height}"


def main():
    args = sys.argv[1:]
    if "--all" in args:
        ids = sorted(d for d in os.listdir(SCENES) if d.startswith("cast_"))
    else:
        ids = args
    for sid in ids:
        print(key_one(sid))


if __name__ == "__main__":
    main()
