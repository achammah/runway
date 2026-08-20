#!/usr/bin/env python3
"""RUNWAY! per-scene animation factory — turn ONE scene into a hand-animated loop.

The spot-patch model (BLANK_SCENES_ARCHITECTURE.md §8) gives a scene a blank plate
and one patch per (spot, character). This tool adds the motion, in three layers that
run at different speeds so the result reads as acting rather than as a screensaver:

  1. AMBIENT   scene-level light, generated ONCE on the blank (seedance i2v, 4s,
               first==last) and stored as additive deltas, spatially GATED to the
               boxes the scene's animation script says may change.
  2. ACTING    a second frame per character (one replace-edit each: "everything
               identical except <one pose change>"), cut into an f2 patch that is
               guaranteed to differ from f1 only where the edit touched the
               character it was for.
  3. LIFE      procedural, exactly loop-periodic: 1-2px bob, blinks on auto-found
               eyes, and per-scene props (here: the laser dot's tremor).

Every command is per-scene and driven by one config json, so regenerating another
scene is: write its animation script -> write its config -> run these commands.

Usage
  craft_scene.py deltas   <config.json>   mp4 -> frames -> seam/locality report -> gated deltas
  craft_scene.py cut      <config.json>   f2 sources -> f2 patches (+ leak report)
  craft_scene.py eyes     <config.json>   auto-find eyes in every patch
  craft_scene.py render   <config.json>   the 47 composited frames
  craft_scene.py encode   <config.json>   GIF @1024w + MP4 @1536w
  craft_scene.py contact  <config.json>   frames 0/12/24/36 of each loop, for judging
"""
import json, os, sys, subprocess, math
import numpy as np
from PIL import Image, ImageFilter

# ---------------------------------------------------------------- helpers

def cfg_load(p):
    c = json.load(open(p))
    c["_dir"] = os.path.dirname(os.path.abspath(p))
    return c

def arr(path):
    return np.asarray(Image.open(path).convert("RGB")).astype(np.int16)

def rgba(path):
    return np.asarray(Image.open(path).convert("RGBA")).astype(np.int16)

def save(a, path):
    Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).save(path)

def sh(cmd, **kw):
    return subprocess.run(cmd, capture_output=True, text=True, **kw)

def _label(mask):
    """Connected components (4-neighbour), no scipy. Returns (labels, count)."""
    h, w = mask.shape
    lab = np.zeros((h, w), np.int32)
    cur = 0
    idx = np.argwhere(mask)
    seen = mask.copy()
    for y0, x0 in idx:
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

def _erode(mask, r):
    if r <= 0:
        return mask
    im = Image.fromarray((mask * 255).astype(np.uint8))
    return np.asarray(im.filter(ImageFilter.MinFilter(2 * r + 1))) > 127

def _open(mask, r):
    """Break the hair-thin bridges that merge an eye into whatever else is white in the
    patch — here, the slab of presentation screen the presenter's spot box also contains."""
    return _dilate(_erode(mask, r), r)

def _dilate(mask, r):
    if r <= 0:
        return mask
    im = Image.fromarray((mask * 255).astype(np.uint8))
    im = im.filter(ImageFilter.MaxFilter(2 * r + 1))
    return np.asarray(im) > 127

def _feather_box(shape, box, feather):
    """A soft 0..1 gate for one box, cosine-tapered over `feather` px inside the edge."""
    h, w = shape
    x0, y0, x1, y1 = box
    g = np.zeros((h, w), np.float32)
    x0c, y0c = max(0, x0), max(0, y0)
    x1c, y1c = min(w, x1), min(h, y1)
    if x1c <= x0c or y1c <= y0c:
        return g
    g[y0c:y1c, x0c:x1c] = 1.0
    if feather > 0:
        g = np.asarray(Image.fromarray((g * 255).astype(np.uint8))
                       .filter(ImageFilter.GaussianBlur(feather / 2.0))).astype(np.float32) / 255.0
    return g

# ---------------------------------------------------------------- 1. ambient

def cmd_deltas(c):
    """mp4 -> 48 frames -> pick the best seam window -> measure -> gated additive deltas."""
    a = c["ambient"]
    W, H = c["size"]
    work = os.path.join(c["_dir"], a["work"])
    fdir = os.path.join(work, "frames")
    ddir = os.path.join(work, "deltas")
    for d in (fdir, ddir):
        os.makedirs(d, exist_ok=True)
    for f in os.listdir(fdir):
        os.remove(os.path.join(fdir, f))
    sh(["ffmpeg", "-hide_banner", "-v", "error", "-y", "-i", a["mp4"],
        "-vf", f"fps={a.get('fps',12)},scale={W}:{H}:flags=lanczos",
        f"{fdir}/f_%03d.png"])
    names = sorted(os.listdir(fdir))
    assert len(names) >= 8, f"only {len(names)} frames extracted"
    F = np.stack([arr(f"{fdir}/{n}") for n in names]).astype(np.float32)
    n = len(F)

    # SEAM: first==last was requested of the model, but it drifts the global exposure in
    # the opening frames. Search for the contiguous window whose ends match best.
    def seam(i, j):
        return float(np.abs(F[i] - F[j]).mean())
    best, span = None, a.get("frames", 47)
    for i in range(0, min(a.get("max_drop", 8), n - span)):
        j = i + span
        if j >= n:
            break
        s = seam(i, j - 1)
        if best is None or s < best[0]:
            best = (s, i, j)
    if best is None:
        best = (seam(0, n - 2), 0, n - 1)
    seam_val, i0, i1 = best
    used = F[i0:i1][:span]
    n_used = len(used)

    base = used[0]
    d = used - base[None]                       # signed
    pos = np.clip(d, 0, None)                   # additive contract: the engine only adds
    clip_loss = float(np.clip(-d, 0, None).mean())

    # GATE: the animation script's must-not-move list, enforced mechanically.
    # `gain` is the art direction: the model has no idea which of the scene's light
    # events should lead. Here it handed us a +32-mean sunburst in the window and
    # nothing at all on the screen, so the window is scaled down and the screen's
    # breath is synthesised below from the script instead.
    gate = np.zeros((H, W), np.float32)
    for g in a.get("gate", []):
        gate = np.maximum(gate, _feather_box((H, W), g["box"], g.get("feather", 30))
                          * g.get("gain", 1.0))
    if not a.get("gate"):
        gate[:] = 1.0
    posg = pos * gate[None, :, :, None]

    # SYNTH: script rows the model under-delivered, drawn procedurally. Every layer is
    # non-negative (the engine only adds) and exactly zero at i=0 and i=n-1, so it
    # cannot break the seam no matter what it does in between.
    base_lum = base.mean(axis=2)
    for s in a.get("synth", []):
        x0, y0, x1, y1 = s["box"]
        m = np.zeros((H, W), np.float32)
        sel = np.zeros((H, W), bool)
        sel[max(0,y0):y1, max(0,x0):x1] = True
        if s.get("mask", "bright") == "bright":
            sel &= (base_lum > s.get("bright_min", 200))
        m[sel] = 1.0
        if s.get("blur", 6):
            m = np.asarray(Image.fromarray((m * 255).astype(np.uint8))
                           .filter(ImageFilter.GaussianBlur(s.get("blur", 6)))).astype(np.float32) / 255.0
        k = s.get("cycles", 2)
        for i in range(n_used):
            t = i / n_used
            env = 0.5 - 0.5 * math.cos(2 * math.pi * t)          # 0 at both ends
            if s["type"] == "breath":
                amt = s["amp"] * (0.5 - 0.5 * math.cos(2 * math.pi * k * t))
                posg[i] += (m * amt)[..., None]
            elif s["type"] == "ripple":
                xx = np.arange(W, dtype=np.float32)[None, :]
                wave = 0.5 + 0.5 * np.sin(2 * math.pi * (k * t + xx / s.get("wavelength", 500.0)))
                posg[i] += (m * wave * (s["amp"] * env))[..., None]

    def locality(stack, thr=6):
        m = (stack.max(axis=3) > thr)
        return float(m.mean(axis=(1, 2)).max() * 100), m.any(axis=0)

    loc_raw, _ = locality(pos)
    loc_gate, movemask = locality(posg)
    seam_after = float(np.abs((base + posg[0]) - (base + posg[-1])).mean())

    inside = 0.0
    if a.get("gate"):
        allow = np.zeros((H, W), bool)
        for g in list(a["gate"]) + list(a.get("synth", [])):
            x0, y0, x1, y1 = g["box"]
            allow[max(0,y0):y1, max(0,x0):x1] = True
        tot = movemask.sum()
        inside = float((movemask & allow).sum() / tot * 100) if tot else 100.0

    for k in range(n_used):
        save(posg[k], f"{ddir}/d_{k:03d}.png")

    rep = {"frames_extracted": n, "window": [i0, i1], "frames_used": n_used,
           "seam_mean_abs_diff_raw": round(seam_val, 3),
           "seam_after_gate": round(seam_after, 3),
           "clamp_loss_mean": round(clip_loss, 3),
           "moving_pixels_pct_raw": round(loc_raw, 2),
           "moving_pixels_pct_gated": round(loc_gate, 2),
           "gated_motion_inside_allowed_boxes_pct": round(inside, 2),
           "peak_delta": int(posg.max()),
           "per_box_peak": {g["name"]: int(pos[:, max(0,g["box"][1]):g["box"][3],
                                                max(0,g["box"][0]):g["box"][2]].max())
                            for g in a.get("gate", [])}}
    json.dump(rep, open(f"{work}/ambient_report.json", "w"), indent=1)
    print(json.dumps(rep, indent=1))

# ---------------------------------------------------------------- 2. acting

def _register(f1_alpha, sub_f2_rgb, sub_blank, thresh=20, band=0.6):
    """The edit model likes to redraw a character a few percent larger. On a two-frame
    flip that is a POP, not a beat, and no prompt reliably stops it — measured here over
    two rolls of the same edit: +12.7% and +12.6% silhouette area. So correct it instead.

    The transform is estimated ONLY from the band of the body the edit did not touch (the
    bottom: legs, shoes, chair contact), using each frame's own silhouette against the
    blank, so the pose change itself cannot bias the estimate. f2 is then warped back onto
    f1's frame before it is cut. Returns (warped_rgb, scale, dx, dy)."""
    h, w = f1_alpha.shape
    y0 = int(h * band)
    a1 = f1_alpha[y0:]
    a2 = np.abs(sub_f2_rgb - sub_blank).max(axis=2)[y0:] > thresh
    n1, n2 = int(a1.sum()), int(a2.sum())
    if n1 < 500 or n2 < 500:
        return sub_f2_rgb, 1.0, 0.0, 0.0
    sc = math.sqrt(n1 / n2)                      # <1 when f2 was drawn too big
    ys1, xs1 = np.nonzero(a1); ys2, xs2 = np.nonzero(a2)
    cx1, cy1 = xs1.mean(), ys1.mean() + y0
    cx2, cy2 = xs2.mean(), ys2.mean() + y0
    # output(x,y) samples input at c2 + (p - c1)/sc  ->  PIL AFFINE maps out->in
    inv = 1.0 / sc
    im = Image.fromarray(np.clip(sub_f2_rgb, 0, 255).astype(np.uint8))
    warped = im.transform((w, h), Image.AFFINE,
                          (inv, 0.0, cx2 - inv * cx1,
                           0.0, inv, cy2 - inv * cy1), resample=Image.BICUBIC)
    return np.asarray(warped).astype(np.int16), sc, cx1 - cx2, cy1 - cy2

def cmd_cut(c):
    """f2 source -> f2 patch. Two guarantees, both measured:
       LEAK  the edit's changes vs the populated source must sit inside the spot box.
       BOND  f2 may differ from f1 only in components attached to that character
             (or to a prop colour the scene declares, e.g. the laser's coral)."""
    W, H = c["size"]
    blank = arr(c["blank"])
    pop = arr(c["populated"])
    work = os.path.join(c["_dir"], c["work"])
    os.makedirs(f"{work}/patches", exist_ok=True)
    reports = {}
    for name, p in c["patches"].items():
        if not p.get("f2_source"):
            continue
        spot = c["spots"][p["spot"]]
        x0, y0, x1, y1 = spot
        f2 = arr(p["f2_source"])
        # a rendition edited from its own populated render (e.g. the hacker swap) is
        # leak-checked against THAT render, not against the scene's default populated one
        base_src = arr(p["f2_base"]) if p.get("f2_base") else pop
        assert f2.shape == base_src.shape, f"{name}: size mismatch {f2.shape}"

        # --- LEAK: where did the edit actually change the picture?
        chg = np.abs(f2 - base_src).max(axis=2) > c.get("edit_thresh", 26)
        lab, k = _label(chg)
        keep = np.zeros_like(chg)
        sizes = np.bincount(lab.ravel())
        for i in range(1, k + 1):
            if sizes[i] >= c.get("min_component", 120):
                keep |= (lab == i)
        # A declared prop may legitimately reach past the spot edge (this scene: the
        # laser beam lands on a chart 45px outside the presenter's box). Score the leak
        # on the body, and report the prop's overspill separately instead of failing it.
        propc = np.zeros_like(keep)
        for col in c.get("prop_colours", []):
            r, g, b = col["rgb"]; tol = col.get("tol", 60)
            propc |= ((np.abs(f2[..., 0] - r) < tol) & (np.abs(f2[..., 1] - g) < tol)
                      & (np.abs(f2[..., 2] - b) < tol))
        insidebox = np.zeros_like(keep)
        insidebox[y0:y1, x0:x1] = True
        body = keep & ~propc
        tot = int(body.sum())
        in_pct = float((body & insidebox).sum() / tot * 100) if tot else 100.0
        prop_out = int((keep & propc & ~insidebox).sum())

        # --- BOND: build f2's patch, then keep only the parts bonded to this character.
        # Geometry comes from the PATCH (offset + its own size), not from the spot box:
        # a cut patch is trimmed to its content and can be shorter than its region.
        f1p = rgba(p["f1"])
        a1 = f1p[..., 3] > 0
        px, py = p["offset"]
        ph, pw = a1.shape
        sub_blank = blank[py:py + ph, px:px + pw]
        sub_f2 = f2[py:py + ph, px:px + pw]
        reg = None
        if p.get("register"):
            sub_f2, rs, rtx, rty = _register(a1, sub_f2, sub_blank,
                                             c.get("patch_thresh", 20),
                                             p["register"].get("band", 0.6))
            reg = {"scale": round(rs, 4), "dx": round(rtx, 1), "dy": round(rty, 1)}
        a2 = np.abs(sub_f2 - sub_blank).max(axis=2) > c.get("patch_thresh", 20)
        diff12 = (a1 != a2) | (a1 & a2 & (np.abs(sub_f2 - f1p[..., :3]).max(axis=2) > c.get("patch_thresh", 20)))
        if p.get("bond_box"):
            bb = np.zeros_like(diff12)
            for (bx0, by0, bx1, by1) in p["bond_box"]:
                bb[by0:by1, bx0:bx1] = True
            diff12 &= bb
        lab2, k2 = _label(diff12)
        anchor = _dilate(a1, c.get("bond_radius", 6))
        prop = np.zeros_like(diff12)
        for col in c.get("prop_colours", []):
            r, g, b = col["rgb"]; tol = col.get("tol", 60)
            prop |= ((np.abs(sub_f2[..., 0] - r) < tol) & (np.abs(sub_f2[..., 1] - g) < tol)
                     & (np.abs(sub_f2[..., 2] - b) < tol))
        bonded = np.zeros_like(diff12)
        dropped = 0
        s2 = np.bincount(lab2.ravel())
        for i in range(1, k2 + 1):
            comp = (lab2 == i)
            if s2[i] < c.get("min_component", 120):
                continue
            if (comp & anchor).any() or (comp & prop).mean() > 0.25:
                bonded |= comp
            else:
                dropped += int(s2[i])

        # Grow the bonded region before applying it. Without this the anti-aliased ring
        # around a feature that MOVED (the VC's eye) falls just outside the mask and
        # survives from f1 — a ghost outline of the old eye, clearly visible on the flip.
        moved_core = int(bonded.sum())      # the real change, before the fringe grow
        bonded = _dilate(bonded, c.get("bond_grow", 4))
        out = f1p.copy()
        # inside the bonded region f2's own cut wins (colour AND alpha)
        out[..., 3] = np.where(bonded, np.where(a2, 255, 0), f1p[..., 3])
        for ch in range(3):
            out[..., ch] = np.where(bonded, sub_f2[..., ch], f1p[..., ch])
        op = f"{work}/patches/{name}__f2.png"
        Image.fromarray(out.astype(np.uint8)).save(op)

        moved = moved_core
        reports[name] = {"leak_changed_px": tot, "inside_spot_pct": round(in_pct, 2),
                         "prop_px_outside_spot": prop_out, "register": reg,
                         "f2_moved_px": moved,
                         "f2_moved_pct_of_patch": round(moved / (a1.size) * 100, 2),
                         "unbonded_px_dropped": dropped,
                         "verdict": ("OK" if in_pct >= c.get("leak_min_pct", 90)
                                     and moved >= c.get("move_min_px", 800)
                                     and moved / a1.size * 100 <= c.get("move_max_pct", 12)
                                     else "REROLL")}
        print(name, json.dumps(reports[name]))
    json.dump(reports, open(f"{work}/acting_report.json", "w"), indent=1)

# ---------------------------------------------------------------- repair

def cmd_repair(c):
    """Fill the holes a difference cut punches in a character.

    diff(populated, blank) drops every pixel where the character happens to match what
    was behind it. In this scene that silently deleted the presenter's EYES — white ovals
    standing in front of the white presentation screen — leaving alpha 0 and RGB 0 where
    the face should be. It looks correct only for as long as the patch is composited back
    over the very blank it was cut from, and it means the face cannot blink.

    A transparent region that does not touch the patch border is interior: make it opaque
    and take its colour from the render the patch was cut from. Where the hole was real
    occlusion (furniture in front of a leg) this repaints the furniture, which is what the
    populated render shows there anyway — so the composite is unchanged and the patch
    becomes self-contained."""
    work = os.path.join(c["_dir"], c["work"])
    out = f"{work}/patches"
    os.makedirs(out, exist_ok=True)
    rep = {}
    for name, p in c["patches"].items():
        for key, srcimg in (("f1", p.get("f1_base") or c["populated"]),
                            ("f2", p.get("f2_source"))):
            path = p.get(key)
            if not path or not os.path.exists(path) or not srcimg:
                continue
            a = rgba(path)
            src = arr(srcimg)
            px, py = p["offset"]
            ph, pw = a.shape[:2]
            sub = src[py:py + ph, px:px + pw]
            holes = a[..., 3] == 0
            lab, k = _label(holes)
            border = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]])))
            fill = np.zeros_like(holes)
            for i in range(1, k + 1):
                if i not in border:
                    fill |= (lab == i)
            n = int(fill.sum())
            if n:
                a[..., :3] = np.where(fill[..., None], sub, a[..., :3])
                a[..., 3] = np.where(fill, 255, a[..., 3])
            op = f"{out}/{name}__{key}_repaired.png"
            Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).save(op)
            rep[f"{name}.{key}"] = {"holes_filled_px": n, "out": op}
            print(f"{name}.{key}: filled {n} px -> {op}")
    json.dump(rep, open(f"{work}/repair_report.json", "w"), indent=1)

def cmd_clean(c):
    """Drop everything in a patch that is not part of its character.

    A spot box is a rectangle, so a patch also carries whatever else fell inside it: the
    hairline where the render noise disagreed about the framed picture's edge, a slab of
    the neighbour's shoe, half a briefcase. None of it belongs to the character, and all
    of it SHIMMERS the moment the patch breathes — that is what the motion map showed
    moving on the wall art and the empty chair. Keep components attached to the body (the
    contact shadow keeps the roller case attached, correctly) plus any explicit keep_box,
    and drop the rest."""
    work = os.path.join(c["_dir"], c["work"])
    out = f"{work}/patches"
    rep = {}
    for name, p in c["patches"].items():
        for key in ("f1", "f2"):
            path = p.get(key)
            if not path or not os.path.exists(path):
                continue
            a = rgba(path)
            al = a[..., 3] > 0
            ink = al & (a[..., :3].mean(axis=2) < 70)
            li, ki = _label(ink)
            if ki == 0:
                continue
            si = np.bincount(li.ravel()); si[0] = 0
            body = _dilate(li == si.argmax(), c.get("attach_radius", 6))
            keep_box = np.zeros(al.shape, bool)
            for (bx0, by0, bx1, by1) in p.get("keep_boxes", []):
                keep_box[by0:by1, bx0:bx1] = True
            lab, k = _label(al)
            s = np.bincount(lab.ravel()); s[0] = 0
            keep = np.zeros(al.shape, bool)
            dropped = 0
            for i in range(1, k + 1):
                if not s[i]:
                    continue
                comp = (lab == i)
                if (comp & body).any() or (comp & keep_box).any():
                    keep |= comp
                else:
                    dropped += int(s[i])
            # drop_boxes: a region the patch must NOT own. Used where something static
            # is fused to the character by its own contact shadow — this scene's roller
            # case (its raised handle crossed the bob line and stretched) and the empty
            # chair beside the presenter. Whatever is dropped is either already in the
            # blank or is re-supplied as a static prop.
            for (bx0, by0, bx1, by1) in p.get("drop_boxes", []):
                a[by0:by1, bx0:bx1, 3] = 0
            a[..., 3] = np.where(keep, a[..., 3], 0)
            op = f"{out}/{name}__{key}_clean.png"
            Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).save(op)
            rep[f"{name}.{key}"] = {"dropped_px": dropped, "kept_px": int(keep.sum()), "out": op}
            print(f"{name}.{key}: dropped {dropped} px of non-character debris")
    json.dump(rep, open(f"{work}/clean_report.json", "w"), indent=1)

# ---------------------------------------------------------------- eyes

def cmd_eyes(c):
    """Blank white oval eyes: near-white blobs in the upper part of the patch body."""
    work = os.path.join(c["_dir"], c["work"])
    out = {}
    targets = []
    for name, p in c["patches"].items():
        targets.append((name, p["f1"]))
        if p.get("f2") and os.path.exists(p["f2"]):
            targets.append((name + "#f2", p["f2"]))
    for name, src in targets:
        if c["patches"].get(name.split("#")[0], {}).get("eye_boxes"):
            out[name] = c["patches"][name.split("#")[0]]["eye_boxes"]
            print(name, "eyes (from config):", out[name])
            continue
        a = rgba(src)
        al = a[..., 3] > 0
        # 1. the BODY, not the patch: the biggest ink-black component. A spot patch also
        #    carries whatever else fell inside its box — for this scene, a slab of the
        #    white presentation screen and a slice of the neighbour's face, both of which
        #    a plain "white blob in the top half" rule happily reported as eyes.
        ink = al & (a[..., :3].mean(axis=2) < 70)
        lab, k = _label(ink)
        if k == 0:
            continue
        sizes = np.bincount(lab.ravel()); sizes[0] = 0
        body = (lab == sizes.argmax())
        bys = np.argwhere(body)
        btop, bbot = bys[:, 0].min(), bys[:, 0].max()
        bh = bbot - btop
        head = np.zeros_like(body)
        head[btop:btop + int(bh * c.get("eye_zone", 0.45))] = True
        # 2. white blobs INSIDE the head, and ringed by the body
        white = _open(al & head & (a[..., :3].min(axis=2) > 190), c.get("eye_open", 2))
        lab2, k2 = _label(white)
        s2 = np.bincount(lab2.ravel())
        eyes = []
        for i in range(1, k2 + 1):
            area = int(s2[i])
            if not (c.get("eye_min", 150) <= area <= max(600, c.get("eye_max_frac", 0.05) * body.sum())):
                continue
            comp = (lab2 == i)
            ys2 = np.argwhere(comp)
            y0, x0 = ys2.min(axis=0); y1, x1 = ys2.max(axis=0) + 1
            w, h = int(x1 - x0), int(y1 - y0)
            if w < 8 or h < 8 or not (0.45 <= w / h <= 2.2):
                continue
            halo = _dilate(comp, 5) & ~_dilate(comp, 1)
            if body[halo].mean() < 0.75:          # must sit ON the body, not beside it
                continue
            eyes.append([int(x0), int(y0), int(x1), int(y1), area])
        eyes.sort(key=lambda e: -e[4])
        eyes = eyes[:2]
        if len(eyes) == 2:                        # a pair sits at nearly the same height
            cy = [(e[1] + e[3]) / 2 for e in eyes]
            if abs(cy[0] - cy[1]) > 0.30 * bh:
                eyes = eyes[:1]
        out[name] = eyes
        print(name, "eyes:", eyes)
    json.dump(out, open(f"{work}/eyes.json", "w"), indent=1)

# ---------------------------------------------------------------- 3. render

def _eye_blob(patch, box):
    """The eye itself — the white oval plus its ink ring — rather than the box around it.
    Erasing a BOX left visible rectangles wherever the box also covered the silhouette
    edge, which is exactly where an eye sits on a head drawn in three-quarter view."""
    x0, y0, x1, y1 = box[:4]
    sel = np.zeros(patch.shape[:2], bool)
    sel[y0:y1, x0:x1] = True
    blob = sel & (patch[..., 3] > 0) & (patch[..., :3].mean(axis=2) > 150)
    if blob.sum() < 40:
        blob = sel
    return _dilate(blob, 5)

def _fill_colour(patch, region, ink):
    """The body ink actually painted AROUND a region — sampled from a ring that excludes
    both the white of the feature and the near-black of any outline, so the erase reads as
    body, not as a patch of a different black."""
    ring = _dilate(region, 9) & ~_dilate(region, 4)
    lum = patch[..., :3].mean(axis=2)
    m = ring & (patch[..., 3] > 0) & (lum > 8) & (lum < 70)
    if m.sum() < 60:
        m = ring & (patch[..., 3] > 0) & (lum < 90)
    if m.sum() < 20:
        return np.array(ink, np.int16)
    return np.median(patch[..., :3][m], axis=0).astype(np.int16)

def _eye_shift(patch, eyes, ink, dx, dy):
    """An eye dart. The blank white ovals sit on a flat ink body, so they can be lifted
    and set down a few px with no seam at all — the cheapest honest acting in this style,
    and the one beat that survives when a replace-edit cannot be trusted."""
    a = patch.copy()
    H, W = patch.shape[:2]
    for e in eyes:
        blob = _eye_blob(patch, e)
        col = _fill_colour(patch, blob, ink)
        src = patch[..., :3].copy()
        a[..., :3] = np.where(blob[..., None], col, a[..., :3])
        moved = np.zeros_like(blob)
        ys, xs = np.nonzero(blob)
        ty, tx = ys + dy, xs + dx
        ok = (ty >= 0) & (ty < H) & (tx >= 0) & (tx < W)
        moved[ty[ok], tx[ok]] = True
        a[..., :3] = np.where(moved[..., None], _shift(src, dx, dy), a[..., :3])
    return a

def _bob(patch, dy, anchor=None, band=6):
    """A breathing shift that keeps the character's feet on the floor.

    Offsetting the WHOLE patch also lifts everything else its spot box happened to
    contain — this scene's roller case, briefcase and chair bases all shimmered in the
    motion map. So the shift is full above `anchor`, ramps to zero over `band` rows, and
    is zero below: the torso and head breathe, the ground contact does not."""
    if dy == 0:
        return patch
    H = patch.shape[0]
    if anchor is None:
        return np.roll(patch, dy, axis=0)
    out = patch.copy()
    for y in range(H):
        if y < anchor:
            v = dy
        elif y < anchor + band:
            v = int(round(dy * (1.0 - (y - anchor) / band)))
        else:
            v = 0
        sy = y - v
        if v and 0 <= sy < H:
            out[y] = patch[sy]
    return out

def _shift(img, dx, dy):
    out = np.roll(np.roll(img, dy, axis=0), dx, axis=1)
    return out

def _slide(patch, spec, dx, dy):
    """Lift a bright object that sits on the body (this scene: the smoothie cup) and set
    it down a few px away. The vacated footprint is filled with the body's own ink."""
    x0, y0, x1, y1 = spec["box"]
    H, W = patch.shape[:2]
    sel = np.zeros((H, W), bool)
    sel[y0:y1, x0:x1] = True
    obj = sel & (patch[..., 3] > 0) & (patch[..., :3].mean(axis=2) > spec.get("bright_min", 75))
    lab, k = _label(obj)
    if k == 0:
        return patch
    sizes = np.bincount(lab.ravel()); sizes[0] = 0
    island = _dilate(lab == sizes.argmax(), spec.get("grow", 2))
    ys = np.argwhere(island)
    if not len(ys):
        return patch
    iy0, ix0 = ys.min(axis=0); iy1, ix1 = ys.max(axis=0) + 1
    a = patch.copy()
    col = _fill_colour(patch, island, spec.get("ink", [24, 22, 23]))
    a[..., :3] = np.where(island[..., None], col, a[..., :3])
    src = patch[iy0:iy1, ix0:ix1]
    msk = island[iy0:iy1, ix0:ix1]
    ty0, tx0 = int(iy0 + dy), int(ix0 + dx)
    ty1, tx1 = ty0 + (iy1 - iy0), tx0 + (ix1 - ix0)
    if ty0 < 0 or tx0 < 0 or ty1 > H or tx1 > W:
        return patch
    tgt = a[ty0:ty1, tx0:tx1]
    tgt[..., :3] = np.where(msk[..., None], src[..., :3], tgt[..., :3])
    tgt[..., 3] = np.where(msk, 255, tgt[..., 3])
    return a

def _blink(patch, eyes, ink):
    """Blob-style blink: the white oval closes to a short horizontal dash."""
    a = patch.copy()
    for e in eyes:
        x0, y0, x1, y1 = e[:4]
        blob = _eye_blob(patch, e)
        a[..., :3] = np.where(blob[..., None], _fill_colour(patch, blob, ink), a[..., :3])
        mid = (y0 + y1) // 2
        inset = max(1, int((x1 - x0) * 0.18))
        a[mid - 1:mid + 1, x0 + inset:x1 - inset, :3] = 242
    return a

def _paste(base, patch, ox, oy):
    h, w = patch.shape[:2]
    H, W = base.shape[:2]
    x0, y0 = max(0, ox), max(0, oy)
    x1, y1 = min(W, ox + w), min(H, oy + h)
    if x1 <= x0 or y1 <= y0:
        return
    sub = patch[y0 - oy:y1 - oy, x0 - ox:x1 - ox]
    al = (sub[..., 3:4].astype(np.float32) / 255.0)
    base[y0:y1, x0:x1] = base[y0:y1, x0:x1] * (1 - al) + sub[..., :3] * al

def _tremor(i, n, harmonics=(3, 5, 7), seed=0.0):
    """Exactly loop-periodic pseudo-tremor: integer harmonics of the loop only."""
    t = 2 * math.pi * i / n
    v = 0.0
    for j, k in enumerate(harmonics):
        v += math.sin(k * t + seed + j * 1.7) / (j + 1)
    return v / sum(1 / (j + 1) for j in range(len(harmonics)))

def _laser(base, spec, i, n):
    """The scene's own prop, and the reason this scene needed a script rather than a
    recipe. Two jobs:
      * repair — a spot box that ends before the beam does leaves the laser cut off in
        mid-air, so the beam is re-drawn from `from` (the cut edge) out to the chart;
      * act — the dot never sits still. It slides a few px along the beam and a hair
        across it, on integer harmonics of the loop so the tremor wraps exactly."""
    tip = np.array(spec["tip"], float)
    d = np.array(spec["dir"], float)
    d /= (np.linalg.norm(d) + 1e-6)
    perp = np.array([-d[1], d[0]])
    t = spec.get("along", 3.0) * (_tremor(i, n, (3, 5, 7)) * 0.5 + 0.5)
    q = spec.get("perp", 1.0) * _tremor(i, n, (5, 8, 11), seed=1.1)
    c = tip + d * t + perp * q
    col = np.array(spec.get("rgb", [232, 106, 92]), np.float32)
    core = np.array(spec.get("core_rgb", [255, 214, 208]), np.float32)
    H, W = base.shape[:2]
    start = np.array(spec.get("from", tip), float)
    seg = np.linalg.norm(c - start)
    steps = int(max(2, seg))
    for k in range(steps + 1):
        pt = start + (c - start) * (k / steps)
        _disc(base, pt, spec.get("stub_r", 2.0), col, W, H)
    _disc(base, c, spec.get("dot_r", 4.0), col, W, H)
    _disc(base, c, spec.get("dot_r", 4.0) * 0.34, core, W, H)

def _disc(base, pt, r, col, W, H):
    x, y = pt
    x0, x1 = int(max(0, x - r - 1)), int(min(W, x + r + 2))
    y0, y1 = int(max(0, y - r - 1)), int(min(H, y + r + 2))
    if x1 <= x0 or y1 <= y0:
        return
    yy, xx = np.mgrid[y0:y1, x0:x1]
    dd = np.sqrt((xx - x) ** 2 + (yy - y) ** 2)
    a = np.clip(r - dd + 0.5, 0, 1)[..., None]
    base[y0:y1, x0:x1] = base[y0:y1, x0:x1] * (1 - a) + col[None, None, :] * a

def _on(i, spans):
    for a, b in spans:
        if a <= i <= b:
            return True
    return False

def cut_prop(c, pr):
    """A prop that straddles its spot's edge (this scene: the consultant's briefcase, cut
    in half at x=760) gets its own static patch, cut the same way a character patch is."""
    blank = arr(c["blank"])
    src = arr(pr["source"])
    x0, y0, x1, y1 = pr["box"]
    sb, ss = blank[y0:y1, x0:x1], src[y0:y1, x0:x1]
    m = np.abs(ss - sb).max(axis=2) > pr.get("thresh", 40)
    lab, k = _label(m)
    sizes = np.bincount(lab.ravel())
    keep = np.zeros_like(m)
    for i in range(1, k + 1):
        if sizes[i] >= pr.get("min_component", 500):
            keep |= (lab == i)
    keep = _dilate(keep, 1)
    out = np.zeros((y1 - y0, x1 - x0, 4), np.uint8)
    out[..., :3] = ss
    out[..., 3] = np.where(keep, 255, 0)
    return out

def cmd_render(c, only=None):
    W, H = c["size"]
    work = os.path.join(c["_dir"], c["work"])
    blank = arr(c["blank"]).astype(np.float32)
    props = {}
    for pr in c.get("props", []):
        props[pr["name"]] = (cut_prop(c, pr).astype(np.int16), pr["box"][0], pr["box"][1])
    ddir = os.path.join(c["_dir"], c["ambient"]["work"], "deltas")
    dn = sorted(os.listdir(ddir))
    N = len(dn)
    eyes = json.load(open(f"{work}/eyes.json"))
    cache = {}

    def pget(path):
        if path not in cache:
            cache[path] = rgba(path)
        return cache[path]

    for loop in c["loops"]:
        if only and loop["name"] != only:
            continue
        odir = f"{work}/render/{loop['name']}"
        os.makedirs(odir, exist_ok=True)
        for f in os.listdir(odir):
            os.remove(os.path.join(odir, f))
        for i in range(N):
            base = blank + arr(f"{ddir}/{dn[i]}").astype(np.float32)
            for spot, sel in loop["spots"].items():
                if not sel:
                    continue
                p = c["patches"][sel]
                sc = c["schedule"][sel]
                use2 = bool(p.get("f2")) and _on(i, sc.get("f2_spans", []))
                pa = pget(p["f2"] if use2 else p["f1"]).copy()
                ev = eyes.get(sel + "#f2" if use2 else sel, eyes.get(sel, []))
                ink = c.get("ink", [30, 30, 30])
                if ev and _on(i, sc.get("blink_frames", [])):
                    pa = _blink(pa, ev, ink)
                elif ev and sc.get("look"):
                    for lk in sc["look"]:
                        if _on(i, lk["spans"]):
                            pa = _eye_shift(pa, ev, ink, lk.get("dx", 0), lk.get("dy", 0))
                            break
                for sl in (sc.get("slide", []) if not use2 else []):
                    if _on(i, sl["spans"]):
                        # ease in and out of the hold so the object does not teleport
                        a0, b0 = sl["spans"][0]
                        for (a0, b0) in sl["spans"]:
                            if a0 <= i <= b0:
                                break
                        span = max(1, b0 - a0)
                        t = (i - a0) / span
                        ramp = min(1.0, min(t, 1 - t) / sl.get("ease", 0.25)) if span > 2 else 1.0
                        pa = _slide(pa, sl, int(round(sl.get("dx", 0) * ramp)),
                                    int(round(sl.get("dy", 0) * ramp)))
                # NOTE: `tap` shifts a box of pixels. It is only safe where the box is
                # entirely interior to a flat body — on this scene's VC it is not (the
                # table edge crosses the crossed arms), and it streaked. Left in, unused.
                if sc.get("tap") and not use2:
                    for (bx0, by0, bx1, by1) in sc["tap"]["boxes"]:
                        amp = sc["tap"].get("amp", 3)
                        ph = sc["tap"]["frames"]
                        near = min(min(abs(i - f), abs(i - f + N), abs(i - f - N)) for f in ph)
                        lift = int(round(amp * max(0.0, 1 - near / 3.0)))
                        if lift:
                            reg = pa[by0:by1, bx0:bx1].copy()
                            pa[by0:by1, bx0:bx1] = np.roll(reg, -lift, axis=0)
                bob = sc.get("bob", {})
                dy = int(round(bob.get("amp", 1.5) *
                               math.sin(2 * math.pi * (i / N + bob.get("phase", 0.0)))))
                if dy:
                    pa = _bob(pa, dy, bob.get("anchor"), bob.get("band", 6))
                _paste(base, pa, p["offset"][0], p["offset"][1])
                if sc.get("laser") and not use2:
                    _laser(base, sc["laser"], i, N)
                elif sc.get("laser_f2") and use2:
                    _laser(base, sc["laser_f2"], i, N)
            for nm in loop.get("props", []):
                pa, px, py = props[nm]
                _paste(base, pa, px, py)
            save(base, f"{odir}/{i:03d}.png")
        print(f"rendered {loop['name']}: {N} frames -> {odir}")

# ---------------------------------------------------------------- encode / judge

def cmd_encode(c):
    work = os.path.join(c["_dir"], c["work"])
    out = os.path.expanduser(c["out_dir"])
    os.makedirs(out, exist_ok=True)
    fps = c["ambient"].get("fps", 12)
    for loop in c["loops"]:
        d = f"{work}/render/{loop['name']}"
        mp4 = f"{out}/{loop['file']}.mp4"
        gif = f"{out}/{loop['file']}.gif"
        r = sh(["ffmpeg", "-hide_banner", "-v", "error", "-y", "-framerate", str(fps),
                "-i", f"{d}/%03d.png", "-vf", "scale=1536:-2:flags=lanczos",
                "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "18", mp4])
        pal = f"{work}/pal_{loop['name']}.png"
        sh(["ffmpeg", "-hide_banner", "-v", "error", "-y", "-i", f"{d}/%03d.png",
            "-vf", "scale=1024:-2:flags=lanczos,palettegen=max_colors=128", pal])
        sh(["ffmpeg", "-hide_banner", "-v", "error", "-y", "-framerate", str(fps),
            "-i", f"{d}/%03d.png", "-i", pal,
            "-lavfi", "scale=1024:-2:flags=lanczos[x];[x][1:v]paletteuse=dither=bayer:bayer_scale=3",
            "-loop", "0", gif])
        print(loop["file"], os.path.getsize(mp4) // 1024, "KB mp4;",
              os.path.getsize(gif) // 1024, "KB gif", r.stderr.strip()[:120])

def cmd_contact(c):
    """Frames 0/12/24/36 side by side, for reading the loop as an animator would."""
    work = os.path.join(c["_dir"], c["work"])
    for loop in c["loops"]:
        d = f"{work}/render/{loop['name']}"
        picks = c.get("contact_frames", [0, 12, 24, 36])
        ims = [Image.open(f"{d}/{i:03d}.png") for i in picks]
        w, h = ims[0].size
        s = c.get("contact_scale", 0.44)
        tw, th = int(w * s), int(h * s)
        sheet = Image.new("RGB", (tw * 2, th * 2), "white")
        for k, im in enumerate(ims):
            sheet.paste(im.resize((tw, th)), ((k % 2) * tw, (k // 2) * th))
        sheet.save(f"{work}/contact_{loop['name']}.png")
        print("contact", f"{work}/contact_{loop['name']}.png")

# ---------------------------------------------------------------- main

if __name__ == "__main__":
    cmd, path = sys.argv[1], sys.argv[2]
    c = cfg_load(path)
    {"deltas": cmd_deltas, "cut": cmd_cut, "eyes": cmd_eyes, "repair": cmd_repair, "clean": cmd_clean,
     "render": cmd_render, "encode": cmd_encode, "contact": cmd_contact}[cmd](c)
