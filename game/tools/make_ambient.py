#!/usr/bin/env python3
"""Lift the ambient motion off a room's loop so it can be laid over ANY still of that room.

THE PROBLEM: the pre-built stages breathe — a bulb sways, dust drifts, a monitor
flickers — because they carry a 48-frame loop. A scene composed by seedream is one
still image, so it is dead. The owner: "I love that the background is alive which
feels like we can't do that if we fully generate an image".

THE MEASUREMENT THAT MAKES IT POSSIBLE: only ~0.9% of a loop's pixels ever change,
concentrated in about a dozen places. The motion is LOCALISED, so it is separable
from the room.

SO: store frame_i MINUS frame_0 as an additive delta. Laid over any still of the same
room with additive blending, the delta reproduces exactly the light that moved and
nothing else. Where nothing moves the delta is black and adds nothing. Where the bulb
brightens, it brightens — and it brightens a composited character standing under it
too, which is correct rather than a bug.

    python3 tools/make_ambient.py <scene_id>
"""
import os, sys
from PIL import Image, ImageChops

def build(scene_id, gain=1.0):
    d = f"assets/scenes/{scene_id}/anim"
    if not os.path.isdir(d):
        print(f"{scene_id}: no anim/"); return 0
    fs = sorted(f for f in os.listdir(d) if f.startswith("frame_") and f.endswith(".png"))
    if len(fs) < 8:
        print(f"{scene_id}: only {len(fs)} frames"); return 0
    out = f"assets/scenes/{scene_id}/ambient"
    os.makedirs(out, exist_ok=True)
    base = Image.open(os.path.join(d, fs[0])).convert("RGB")
    kept = 0
    for i, f in enumerate(fs):
        cur = Image.open(os.path.join(d, f)).convert("RGB")
        # only the light that was ADDED. subtract() clamps at zero, so anything that
        # darkened is dropped rather than wrapping around into a bright artefact.
        delta = ImageChops.subtract(cur, base)
        if gain != 1.0:
            delta = delta.point(lambda v: min(255, int(v * gain)))
        delta.save(f"{out}/d_{i:02d}.png")
        kept += 1
    print(f"{scene_id}: {kept} ambient deltas -> {out}")
    return kept

if __name__ == "__main__":
    ids = sys.argv[1:] or [d for d in sorted(os.listdir("assets/scenes"))
                           if os.path.isdir(f"assets/scenes/{d}/anim")]
    total = sum(build(i) for i in ids)
    print(f"total {total} deltas across {len(ids)} scenes")
