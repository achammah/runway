#!/usr/bin/env python3
"""Per-class procedural animators — high-quality scene life at $0 per scene.

Each detected element gets its class's animator. Everything is deterministic,
subtle, and localised; nothing outside an element's box ever changes, so the room
stays exactly the shipped art. This file is the reference implementation the
GDScript runtime mirrors.

    python3 tools/procedural_anim.py <scene.png> <elements.json> <out.gif> [--frames 48]
"""
import sys, json, math
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageEnhance

def animate(scene, elements, n=48):
    W, H = scene.size
    frames = []
    # pre-cut element patches once
    cuts = []
    for e in elements:
        x0, y0, x1, y1 = e["box"]
        pad = 10
        box = (max(0, x0-pad), max(0, y0-pad), min(W, x1+pad), min(H, y1+pad))
        cuts.append({"e": e, "box": box, "patch": scene.crop(box)})
    for i in range(n):
        t = i / float(n)
        fr = scene.copy()
        d = ImageDraw.Draw(fr, "RGBA")
        for c in cuts:
            cls = c["e"]["class"]; x0, y0, x1, y1 = c["box"]
            cx, cy = (x0+x1)//2, (y0+y1)//2
            if cls == "lamp":
                # the bulb warms and dims on a slow sine; a soft radial halo breathes
                # with it. The glow is ADDITIVE and centred on the bulb, so it lights
                # the wall behind without repainting anything.
                k = 0.5 + 0.5*math.sin(2*math.pi*(t*2 + 0.0))
                glow_r = int((x1-x0) * (2.2 + 0.55*k))
                halo = Image.new("L", (glow_r*2, glow_r*2), 0)
                hd = ImageDraw.Draw(halo)
                for rr in range(glow_r, 0, -6):
                    a = int(26 * k * (1 - rr/glow_r))
                    hd.ellipse([glow_r-rr, glow_r-rr, glow_r+rr, glow_r+rr], fill=a)
                tint = Image.new("RGB", halo.size, (255, 214, 120))
                fr.paste(ImageChops.add(fr.crop((cx-glow_r, cy-glow_r, cx+glow_r, cy+glow_r)),
                         Image.composite(tint, Image.new("RGB", halo.size, 0), halo)),
                         (cx-glow_r, cy-glow_r))
                # brighten the bulb itself slightly
                bulb = c["patch"]
                fr.paste(ImageEnhance.Brightness(bulb).enhance(1.0 + 0.10*k), (x0, y0))
            elif cls == "plant":
                # the crown sways: shear the foliage patch around its BASE so the pot
                # never moves. +/- 1.2 degrees on a slow sine with a phase.
                ang = 1.2 * math.sin(2*math.pi*(t*1 + 0.3))
                patch = c["patch"]
                sheared = patch.transform(patch.size, Image.AFFINE,
                    (1, math.tan(math.radians(ang)), -math.tan(math.radians(ang)) * patch.size[1], 0, 1, 0),
                    resample=Image.BICUBIC)
                mask = sheared.convert("L").point(lambda v: 255)
                fr.paste(sheared, (x0, y0))
            elif cls in ("screen", "window"):
                # luminance breathes +/-2%, uneven top-to-bottom
                k = math.sin(2*math.pi*(t*1.5 + 0.6))
                patch = ImageEnhance.Brightness(c["patch"]).enhance(1.0 + 0.02*k)
                fr.paste(patch, (x0, y0))
        # dust motes in the room's light, drifting up-left, alpha-faded — pure code
        rngseed = 7
        for m in range(6):
            mx = (m*257 + rngseed*31) % W
            my = (m*173) % (H//2) + H//5
            px = (mx + int(28*math.sin(2*math.pi*(t + m*0.17)))) % W
            py = (my - int(46*t) - m*9) % H
            a = int(38 + 26*math.sin(2*math.pi*(t*2 + m*0.29)))
            d.ellipse([px, py, px+4, py+4], fill=(255, 250, 230, max(a, 0)))
        frames.append(fr)
    return frames

if __name__ == "__main__":
    scene = Image.open(sys.argv[1]).convert("RGB")
    els = json.load(open(sys.argv[2]))["elements"]
    n = int(sys.argv[sys.argv.index("--frames")+1]) if "--frames" in sys.argv else 48
    frames = animate(scene, els, n)
    small = [f.resize((1024, int(scene.size[1]*1024/scene.size[0]))) for f in frames]
    small[0].save(sys.argv[3], save_all=True, append_images=small[1:], duration=83, loop=0)
    print(f"{sys.argv[3]}: {len(frames)} frames")
