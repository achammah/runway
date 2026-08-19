#!/usr/bin/env python3
"""RUNWAY! scene-first pipeline (F5) — one command from prompt to placed layers.

Usage:
  python3 tools/scene_pipeline.py generate <scene_id> "<scene prompt without style block>" [--quality high|medium]
  python3 tools/scene_pipeline.py variant <new_scene_id> "<prompt or Nano-Banana JSON>" --ref <url-or-scene_id/layer> [--ref ...] [--engine seedream|gpt] [--quality high|medium]
      # CONSISTENCY METHOD: JSON/structured prompt + reference images of the
      # approved character/room -> new image, same characters guaranteed.
      # DEFAULT ENGINE: Seedream 5.0 Pro edit (bytedance/seedream-v5.0-pro/edit,
      # same family as our decompositions -> best style match; multi-image refs).
      # --engine gpt uses the GPT Image 2 edit middleware instead.
      # A ref of form "<scene_id>/<layer>" auto-uploads that png as a permanent
      # asset (cached in assets/scenes/refs.json) and passes its URL.
  python3 tools/scene_pipeline.py animate <scene_id> ["<motion prompt>"]
      # Seedance 2.5 i2v loop (EXPENSIVE — hero scenes only): 4s, first frame ==
      # last frame for a seamless loop, no audio, 720p -> anim/frame_NN.png @12fps.
  python3 tools/scene_pipeline.py decompose <scene_id> "<numbered element list>"
  python3 tools/scene_pipeline.py place <scene_id> [name1 name2 ... matching layer_1..N order]

Outputs under game/assets/scenes/<scene_id>/:
  scene.png (original), room_bg.png (inpainted base), <name>.png cutouts, layout.json
Registers the scene in game/assets/scenes/scenes_index.json.

Keys: reads OpenAI key + Atlas key from the session scratchpad (paths below).
The style block and palette are appended automatically to every generation prompt.
"""
import json, os, sys, time, urllib.request, subprocess

GAME = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRATCH = "/private/tmp/claude-501/-Users-assem-Documents-Doc-Assem-Claude-Code-runway/46461c38-41e8-4daa-aa34-0dc94af8f9ef/scratchpad"
MIDDLEWARE = "https://nano-banana-production-e03b.up.railway.app/generate-image-openai"
ATLAS = "https://api.atlascloud.ai"
STYLE = ("Flat hand-drawn cartoon, wobbly felt-pen ink outlines, flat fills, no gradients, no text anywhere. "
         "Palette ONLY: ink black #1E1E1E, coral #E86A5C, sunny yellow #F4B942, sage green #8FA582, "
         "muted blue #6E8CA0, paper cream #F2EAD3, white, soft warm grey walls and floor. "
         # UI SAFE ZONES — every scene is a stage the interface is laid on top of.
         "COMPOSITION RULES (must be obeyed): this is a 3:2 game background that UI overlays on top of. "
         "Keep the TOP 10 percent of the image calm and uncluttered — plain wall, sky or empty space only, "
         "no faces, no key objects, nothing the player must see. Keep the BOTTOM 14 percent calm the same way — "
         "plain floor or ground, no characters' heads, no important props. Keep the CENTER-BOTTOM area "
         "(middle third horizontally, lowest quarter vertically) especially empty and low-contrast: a main "
         "button sits there. Place all characters and important objects inside the middle band of the image, "
         "roughly between 20 and 78 percent of the height, and keep the busiest detail toward the left and "
         "right thirds so the middle stays readable. Leave a calm low-detail patch in the upper-left corner "
         "for a small label plate. No object may touch or cross the outer 4 percent margin of the frame. "
         # WRITING SURFACES — the room IS the save file, so the numbers must be
         # objects in it, not a HUD laid over it. Every scene is generated with
         # blank surfaces the engine later writes cash, shares, revenue and
         # customers onto, which is why they must arrive EMPTY and near flat-on.
         "WRITING SURFACES (must be obeyed): the room must contain at least FIVE blank surfaces that a "
         "person could write on, built naturally into the furniture and walls: a large WHITEBOARD or "
         "chalkboard on a wall, a big sheet of PAPER or a chart pinned to a wall, a CLIPBOARD or open "
         "ledger book lying flat on a desk or crate, and a cluster of two or three square STICKY NOTES "
         "stuck to a wall or a monitor edge, and one more prominent BOARD — a corkboard, a slate, or a clipboard hung on a nail — that reads as the room's inventory list. Each one must be COMPLETELY BLANK: no writing, no letters, "
         "no numbers, no words, no scribbles, no diagrams, no charts drawn on them. They are empty "
         "surfaces waiting to be written on. Draw each one nearly FLAT-ON to the camera with very little "
         "perspective slant so writing can sit on it squarely, make each one large enough to hold two or "
         "three lines of handwriting, and give each a clear pale face (white, cream or pale green) that "
         "contrasts with whatever is behind it. Spread them around the room rather than clustering them, "
         "keep them inside the middle band of the image and toward the left and right thirds, and never "
         "let one cross the calm top or bottom areas. "
         # CHARACTER LAW (blob v2) — a fresh generate has no visual anchor for the crew,
         # so without this every room invents its own creatures: pupils, mouths, clothing.
         # Phrased CONDITIONALLY on purpose: era stages are generated EMPTY, and an
         # unconditional "every creature is..." clause invites creatures into a room
         # that must have nobody in it. The cast composites on top as sprites.
         "THE CHARACTERS (this never requires a creature to appear — but IF one does, it must obey "
         "exactly): every creature is a small SOLID INK-BLACK bean-shaped "
         "blob with one ink cowlick spike on top, thin black stick limbs, tiny cream sneakers with one lace "
         "untied, and a slight forward lean. Its ONLY facial features are two blank white oval eyes, the left "
         "one slightly bigger. The eyes are COMPLETELY BLANK — no pupils, no irises, no dots, no eyelids, no "
         "eyebrows. The creatures have NO mouths, NO noses and NO ears. They wear NO clothing of any kind — no "
         "shirts, no hoodies, no jackets, no hats — their bodies are unbroken solid black silhouettes. "
         "Different characters are told apart ONLY by the props they hold or stand beside, never by their "
         "bodies, faces or clothes.")

def _key(name):
    return open(f"{SCRATCH}/{name}").read().strip()

# Atlas's WAF 403s python-urllib's default User-Agent; a curl UA passes.
UA = {"User-Agent": "curl/8.7.1"}
_opener = urllib.request.build_opener()
_opener.addheaders = [("User-Agent", UA["User-Agent"])]
urllib.request.install_opener(_opener)  # every download goes through _fetch below

def _post_json(url, body, headers):
    req = urllib.request.Request(url, json.dumps(body).encode(), {"Content-Type": "application/json", **UA, **headers})
    return json.load(urllib.request.urlopen(req, timeout=420))

def _fetch(url, path, tries=3):
    """Verified download. NEVER use urlretrieve here: when a connection drops it
    writes the partial body and returns successfully, so a half-image lands on
    disk looking fine. That silently shipped two truncated scenes (one cut at
    exactly 1048576 bytes). Check the length against Content-Length, require a
    complete PNG (signature + IEND), and retry before giving up."""
    import io
    from PIL import Image
    last = ""
    for attempt in range(1, tries + 1):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=420) as r:
                declared = r.headers.get("Content-Length")
                data = r.read()
            if declared is not None and len(data) != int(declared):
                raise IOError("short read %d/%s bytes" % (len(data), declared))
            if path.lower().endswith(".png"):
                if data[:8] != b"\x89PNG\r\n\x1a\n":
                    raise IOError("not a PNG")
                if data[-8:-4] != b"IEND":
                    raise IOError("truncated PNG (no IEND)")
                Image.open(io.BytesIO(data)).load()   # catches a corrupt IDAT stream
            elif not data:
                raise IOError("empty body")
            with open(path, "wb") as f:
                f.write(data)
            return path
        except Exception as e:
            last = "%s: %s" % (type(e).__name__, e)
            print("  fetch attempt %d/%d failed (%s)" % (attempt, tries, last))
            if attempt < tries:
                time.sleep(3 * attempt)
    raise IOError("download failed after %d tries: %s -> %s" % (tries, url[:90], last))

def verify():
    """Scan every produced PNG for truncation. A half-downloaded image decodes as
    a normal file to Godot's importer and only shows up as a cut-off scene in the
    game, so this runs as a gate rather than by eye. Returns a shell exit code."""
    import io
    from PIL import Image
    root = f"{GAME}/assets/scenes"
    bad = []
    total = 0
    for dirpath, _, files in os.walk(root):
        for f in sorted(files):
            if not f.lower().endswith(".png"):
                continue
            p = os.path.join(dirpath, f)
            total += 1
            data = open(p, "rb").read()
            why = ""
            if data[:8] != b"\x89PNG\r\n\x1a\n":
                why = "not a PNG"
            elif data[-8:-4] != b"IEND":
                why = "truncated (no IEND)"
            else:
                try:
                    Image.open(io.BytesIO(data)).load()
                except Exception as e:
                    why = "corrupt: %s" % type(e).__name__
            if why:
                bad.append((os.path.relpath(p, root), len(data), why))
    print("verify: %d PNGs scanned, %d intact, %d DAMAGED" % (total, total - len(bad), len(bad)))
    for rel, n, why in bad:
        print("  DAMAGED %-58s %8d bytes  %s" % (rel, n, why))
    return 1 if bad else 0

def clear_surfaces(sid, feather=5):
    """Make every declared writing surface EMPTY BY CONSTRUCTION.

    The prompt asks for blank whiteboards and blank sticky notes, and the model
    ignores it often enough to matter: shipped garage art came back with a doodle
    on the whiteboard and a line already plotted on the wall chart, so the game
    had nowhere clean to write. Asking harder does not fix a generative model.
    So after the scene exists we WIPE each declared face: sample the surface's own
    colour from a ring just inside its border, flood the interior with it, and
    feather the seam. The drawn frame, clip and curled corner survive because the
    fill is inset; whatever was scribbled inside does not.

    Idempotent, and safe to re-run. Requires the scene's layout.json to declare
    write_surfaces (see docs/LANE_BRIEF.md)."""
    from PIL import Image, ImageFilter
    d = scene_dir(sid)
    lp = f"{d}/layout.json"
    if not os.path.exists(lp):
        print(f"{sid}: no layout.json — nothing to clear")
        return 0
    layout = json.load(open(lp))
    surfaces = layout.get("write_surfaces", {})
    if not surfaces:
        print(f"{sid}: no write_surfaces declared — nothing to clear")
        return 0
    # EVERY rendered image of this room, not just the base. A room that ships as
    # an animation is DISPLAYED from anim/frame_NN.png, so clearing only scene.png
    # would leave the marked-up whiteboard on screen and the wipe invisible. Each
    # frame is sampled independently, so a swaying bulb that relights a board
    # across the loop still gets the right fill per frame.
    targets = [p for p in (f"{d}/room_bg.png", f"{d}/scene.png") if os.path.exists(p)]
    anim = f"{d}/anim"
    if os.path.isdir(anim):
        targets += [f"{anim}/{f}" for f in sorted(os.listdir(anim)) if f.endswith(".png")]
    if not targets:
        print(f"{sid}: no images to clear")
        return 0
    total = 0
    for src in targets:
        total += _clear_one(src, surfaces, feather, verbose=(src == targets[0]))
    print(f"{sid}: cleared {len(surfaces)} surface(s) across {len(targets)} image(s)")
    return total

def _clear_one(src, surfaces, feather, verbose=False):
    from PIL import Image, ImageFilter
    im = Image.open(src).convert("RGBA")
    W, H = im.size
    px = im.load()
    done = 0
    for name, s in surfaces.items():
        sx, sy = W / 1536.0, H / 1024.0
        x, y = int(float(s["x"]) * sx), int(float(s["y"]) * sy)
        w, h = int(float(s["w"]) * sx), int(float(s["h"]) * sy)
        x0, y0 = max(x, 0), max(y, 0)
        x1, y1 = min(x + w, W), min(y + h, H)
        if x1 - x0 < 8 or y1 - y0 < 8:
            if verbose:
                print(f"  {name}: rect too small or off-canvas, skipped")
            continue
        # sample the face's own colour from a ring just inside its edge, so the
        # fill matches this particular board rather than a guessed white
        ring = []
        for t in range(x0 + 2, x1 - 2, 2):
            ring.append(px[t, y0 + 2][:3]); ring.append(px[t, y1 - 3][:3])
        for t in range(y0 + 2, y1 - 2, 2):
            ring.append(px[x0 + 2, t][:3]); ring.append(px[x1 - 3, t][:3])
        ring.sort(key=lambda c: c[0] + c[1] + c[2])
        # upper-median: biased to the PALE part of the ring, because the darker
        # half of the samples is usually the inked frame, not the writable face
        base = ring[int(len(ring) * 0.72)]
        patch = Image.new("RGBA", (x1 - x0, y1 - y0), base + (255,))
        # keep the drawn border: paste inset, then blur only the seam
        inset = 3
        region = (x0 + inset, y0 + inset, x1 - inset, y1 - inset)
        im.paste(patch.crop((inset, inset, patch.size[0] - inset, patch.size[1] - inset)), region)
        blurred = im.crop((x0, y0, x1, y1)).filter(ImageFilter.GaussianBlur(feather * 0.5))
        mask = Image.new("L", (x1 - x0, y1 - y0), 0)
        mp = mask.load()
        for iy in range(y1 - y0):
            for ix in range(x1 - x0):
                e = min(ix, iy, (x1 - x0) - 1 - ix, (y1 - y0) - 1 - iy)
                if inset - feather <= e <= inset + feather:
                    mp[ix, iy] = 200
        im.paste(blurred, (x0, y0), mask)
        done += 1
        if verbose:
            print(f"  {name}: cleared {w}x{h} at ({x},{y}), fill rgb{base}")
    im.save(src)
    return done

def scene_dir(sid):
    d = f"{GAME}/assets/scenes/{sid}"
    os.makedirs(d, exist_ok=True)
    return d

def generate(sid, prompt, quality="high"):
    d = scene_dir(sid)
    r = _post_json(MIDDLEWARE, {"prompt": prompt + " " + STYLE, "quality": quality,
                                "size": "1536x1024", "output_format": "png"},
                   {"x-openai-api-key": _key("openai-key.txt")})
    assert "imageUrl" in r, r
    _fetch(r["imageUrl"], f"{d}/scene.png")
    print(f"{sid}: scene.png saved")

MIDDLEWARE_EDIT = "https://nano-banana-production-e03b.up.railway.app/edit-image-openai"
REFS_PATH = f"{GAME}/assets/scenes/refs.json"

def _refs_cache():
    return json.load(open(REFS_PATH)) if os.path.exists(REFS_PATH) else {}

def _resolve_ref(ref):
    """A ref is a URL, or '<scene_id>/<layer>' pointing at a png under assets/scenes.
    Local refs are uploaded once as permanent assets and cached in refs.json."""
    if ref.startswith("http"):
        return ref
    cache = _refs_cache()
    if ref in cache:
        return cache[ref]
    path = f"{GAME}/assets/scenes/{ref}.png"
    assert os.path.exists(path), f"ref not found: {path}"
    url = _permanent_url(path)
    assert url, f"asset upload failed for {ref}"
    cache[ref] = url
    json.dump(cache, open(REFS_PATH, "w"), indent=1)
    return url

def variant(sid, prompt, refs, quality="high", engine="seedream"):
    """JSON prompt + reference images -> new on-model image (the consistency method)."""
    d = scene_dir(sid)
    urls = [_resolve_ref(r) for r in refs]
    full_prompt = prompt if prompt.lstrip().startswith("{") else prompt + " " + STYLE
    if engine == "seedream":
        key = _key("atlas-key.txt")
        r = _post_json(f"{ATLAS}/api/v1/model/generateImage",
                       {"model": "bytedance/seedream-v5.0-pro/edit",
                        "prompt": full_prompt, "images": urls,
                        "size": "2048*1360", "output_format": "png",
                        "thinking": "enabled", "prompt_optimization_mode": "standard",
                        "enable_base64_output": False},
                       {"Authorization": f"Bearer {key}"})
        jid = r["data"]["id"]
        for _ in range(80):
            time.sleep(4)
            req = urllib.request.Request(f"{ATLAS}/api/v1/model/prediction/{jid}",
                                         headers={"Authorization": f"Bearer {key}", **UA})
            st = json.load(urllib.request.urlopen(req, timeout=30))["data"]
            if st["status"] == "completed":
                _fetch((st["outputs"] or [""])[0], f"{d}/scene.png")
                print(f"{sid}: seedream variant saved ({len(urls)} refs)")
                return
            if st["status"] == "failed":
                raise SystemExit(f"{sid}: seedream edit FAILED: {st.get('error')}")
        raise SystemExit(f"{sid}: seedream edit timed out")
    else:
        body = {"prompt": full_prompt, "referenceImages": urls, "quality": quality,
                "size": "1536x1024", "output_format": "png"}
        r = _post_json(MIDDLEWARE_EDIT, body, {"x-openai-api-key": _key("openai-key.txt")})
        assert "imageUrl" in r, r
        _fetch(r["imageUrl"], f"{d}/scene.png")
        print(f"{sid}: gpt variant saved ({len(urls)} refs)")

DEFAULT_MOTION = ("Bring this hand-drawn scene alive as a calm seamless loop, keeping the artwork "
                  "EXACTLY as drawn: subtle idle motion only — characters breathe and shift their "
                  "weight, small props flicker, steam, or sway, light flickers gently. Camera "
                  "completely static, nothing enters or leaves the frame, flat 2D hand-drawn style "
                  "preserved perfectly.")

def animate(sid, motion=DEFAULT_MOTION):
    """Seedance 2.5 image-to-video loop. Cost discipline (owner): 4s, first==last
    frame, audio off. One call per hero scene."""
    import base64, subprocess as sp
    d = scene_dir(sid)
    src = f"{d}/scene.png"
    assert os.path.exists(src), f"no scene.png for {sid}"
    # keep the request body small: downscale to 1024-wide for the first frame
    small = f"{d}/_i2v_src.png"
    sp.run(["sips", "-Z", "1024", src, "--out", small], capture_output=True)
    data = "data:image/png;base64," + base64.b64encode(open(small, "rb").read()).decode()
    key = _key("atlas-key.txt")
    r = _post_json(f"{ATLAS}/api/v1/model/generateVideo",
                   {"model": "bytedance/seedance-2.5/image-to-video",
                    "prompt": motion, "image": data, "last_image": data,
                    "duration": 4, "resolution": "720p", "ratio": "adaptive",
                    "generate_audio": False, "watermark": False,
                    "output_format": "mp4"},
                   {"Authorization": f"Bearer {key}"})
    jid = r["data"]["id"]
    for _ in range(120):
        time.sleep(5)
        req = urllib.request.Request(f"{ATLAS}/api/v1/model/prediction/{jid}",
                                     headers={"Authorization": f"Bearer {key}", **UA})
        st = json.load(urllib.request.urlopen(req, timeout=30))["data"]
        if st["status"] in ["completed", "succeeded"]:
            mp4 = f"{d}/loop.mp4"
            _fetch((st["outputs"] or [""])[0], mp4)
            os.makedirs(f"{d}/anim", exist_ok=True)
            sp.run(["ffmpeg", "-hide_banner", "-v", "error", "-y", "-i", mp4,
                    "-vf", "fps=12,scale=1536:1024", f"{d}/anim/frame_%02d.png"], check=True)
            # first==last: drop the duplicate closing frame
            frames = sorted(os.listdir(f"{d}/anim"))
            if len(frames) > 1:
                os.remove(f"{d}/anim/{frames[-1]}")
            os.remove(small)
            print(f"{sid}: loop animated, {len(frames)-1} frames")
            return
        if st["status"] == "failed":
            raise SystemExit(f"{sid}: i2v FAILED: {st.get('error')}")
    raise SystemExit(f"{sid}: i2v timed out")

def _permanent_url(path):
    # The CLI's default client-side timeout is 30s, which parallel batches of
    # multi-megabyte scene PNGs blow straight past (CLI_TIMEOUT, no url).
    out = subprocess.run(["nexus", "--timeout", "300", "asset", "upload", path, "--json"],
                         capture_output=True, text=True)
    try:
        j = json.loads(out.stdout)
    except json.JSONDecodeError:
        return None
    return j.get("url") or j.get("data", {}).get("url")

def decompose(sid, elements):
    d = scene_dir(sid)
    url = _permanent_url(f"{d}/scene.png")
    assert url, "asset upload failed"
    key = _key("atlas-key.txt")
    r = _post_json(f"{ATLAS}/api/v1/model/generateImage",
                   {"model": "bytedance/seedream-v5.0-pro/layer-decomposition",
                    "prompt": "Decompose into separate layers: " + elements,
                    "image": url, "size": "2K", "output_format": "png",
                    "enable_sync_mode": False, "enable_base64_output": False},
                   {"Authorization": f"Bearer {key}"})
    jid = r["data"]["id"]
    for _ in range(80):
        time.sleep(5)
        req = urllib.request.Request(f"{ATLAS}/api/v1/model/prediction/{jid}",
                                     headers={"Authorization": f"Bearer {key}", **UA})
        st = json.load(urllib.request.urlopen(req, timeout=30))["data"]
        if st["status"] == "completed":
            outs = st["outputs"] or []
            for i, u in enumerate(outs):
                _fetch(u, f"{d}/layer_{i}.png")
            print(f"{sid}: {len(outs)} layers")
            return
        if st["status"] == "failed":
            raise SystemExit(f"{sid}: decomposition FAILED")
    raise SystemExit(f"{sid}: decomposition timed out")

def place(sid, names):
    from PIL import Image
    d = scene_dir(sid)
    scene = Image.open(f"{d}/scene.png").convert("RGB")
    SW, SH = scene.size
    Image.open(f"{d}/layer_0.png").convert("RGB").resize((SW, SH), Image.LANCZOS).save(f"{d}/room_bg.png")
    ds = 4
    small = scene.resize((SW // ds, SH // ds), Image.LANCZOS)
    ss = small.load(); W2, H2 = small.size
    # MERGE, never replace: layout.json also carries hand-authored rows that no
    # template match can regenerate — the founder/crew marks and the foreground
    # occluder declarations. Starting from {} silently deleted them.
    layout = json.load(open(f"{d}/layout.json")) if os.path.exists(f"{d}/layout.json") else {}
    for idx, name in enumerate(names, start=1):
        src = f"{d}/layer_{idx}.png"
        if not os.path.exists(src):
            print(f"{name}: layer_{idx} missing, skipped"); continue
        cut = Image.open(src).convert("RGBA")
        bb = cut.getbbox()
        if bb: cut = cut.crop(bb)
        best = (1e18, 0, 0, 0.25)
        # cutouts come upscaled ~1.6-4x: search real scales 0.12-0.40
        for sc in [0.12, 0.14, 0.16, 0.18, 0.20, 0.22, 0.25, 0.28, 0.32, 0.36, 0.40]:
            c = cut.resize((max(2, int(cut.width * sc / ds)), max(2, int(cut.height * sc / ds))), Image.LANCZOS)
            cs = c.load(); w2, h2 = c.size
            if w2 >= W2 or h2 >= H2 or w2 < 5 or h2 < 5: continue
            pts = [(x, y) for y in range(0, h2, 2) for x in range(0, w2, 2) if cs[x, y][3] > 220]
            if len(pts) < 15: continue
            for y in range(0, H2 - h2, 1):
                for x in range(0, W2 - w2, 1):
                    err = sum(abs(cs[px, py][0] - ss[x + px, y + py][0])
                              + abs(cs[px, py][1] - ss[x + px, y + py][1])
                              + abs(cs[px, py][2] - ss[x + px, y + py][2]) for px, py in pts) / len(pts)
                    if err < best[0]: best = (err, x * ds, y * ds, sc)
        e, x, y, sc = best
        final = cut.resize((max(1, int(cut.width * sc)), max(1, int(cut.height * sc))), Image.LANCZOS)
        final.save(f"{d}/{name}.png")
        layout[name] = {"x": x, "y": y, "w": final.width, "h": final.height,
                        "err": round(e, 1), "placed": e < 300}
        print(f"{name:18s} ({x},{y}) {final.width}x{final.height} err {e:.0f} {'OK' if e < 300 else 'UNMATCHED'}")
    json.dump(layout, open(f"{d}/layout.json", "w"), indent=1)
    # register
    idx_path = f"{GAME}/assets/scenes/scenes_index.json"
    index = json.load(open(idx_path)) if os.path.exists(idx_path) else {}
    index[sid] = {"layers": list(layout.keys())}
    json.dump(index, open(idx_path, "w"), indent=1)
    print(f"{sid}: layout.json + index updated")

if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "generate":
        generate(sys.argv[2], sys.argv[3], sys.argv[4].split("=")[-1] if len(sys.argv) > 4 else "high")
    elif cmd == "variant":
        args = sys.argv[2:]
        vid, vprompt = args[0], args[1]
        vrefs = [args[i + 1] for i, a in enumerate(args) if a == "--ref"]
        q, eng = "high", "seedream"
        for i, a in enumerate(args):
            if a == "--quality":
                q = args[i + 1]
            elif a == "--engine":
                eng = args[i + 1]
        variant(vid, vprompt, vrefs, q, eng)
    elif cmd == "animate":
        animate(sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else DEFAULT_MOTION)
    elif cmd == "decompose":
        decompose(sys.argv[2], sys.argv[3])
    elif cmd == "place":
        place(sys.argv[2], sys.argv[3:])
    elif cmd == "verify":
        sys.exit(verify())
    elif cmd == "clear_surfaces":
        clear_surfaces(sys.argv[2])
