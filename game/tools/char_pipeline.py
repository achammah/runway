#!/usr/bin/env python3
"""THE CHARACTER PIPELINE — one master illustration per archetype, then its idle loop.

    export ART_KEY_FILE=/path/to/openai-key.txt        # the key NEVER lives in this file
    python3 tools/char_pipeline.py master  <id> [--quality high] [--tries 3]
    python3 tools/char_pipeline.py frames  <id> [--only 3,7] [--workers 4] [--tries 2]
    python3 tools/char_pipeline.py adopt   <id> [--only 7,8] [--try 1]   # re-gate, no API
    python3 tools/char_pipeline.py sheet   <id>
    python3 tools/char_pipeline.py preview <id> [--fps 6]                # animated GIF
    python3 tools/char_pipeline.py finish  <id> [--size 368]
    python3 tools/char_pipeline.py audit   <id|--all>          # gates only, no API
    python3 tools/char_pipeline.py run     <id>                # master + frames + finish + sheet

Outputs under OUT/<id>/  (OUT defaults to the session scratchpad — this tool NEVER
writes into game/assets, a human copies the approved set across):
    _master_1024.png     the bible image, full res, transparent
    _ref_url.txt         the public URL the frame edits anchor on
    _raw/frame_NN.png    the untouched 1024 render of each frame
    frames/frame_NN.png  aligned + gated 1024 frame
    chr_arch_<id>.png    the still, downscaled to the shipping size
    chr_loop_<id>_NN.png 12 frames, downscaled to the shipping size
    _contact.png         the contact sheet the eye judges identity on
    _report.json         every gate measurement for every image

WHY IT IS BUILT THIS WAY
------------------------
1. THE STILL AND THE LOOP MUST BE THE SAME DRAWING. The art this replaces has a
   cross-legged hacker in chr_arch_hacker.png and a differently-shaped hunched one in
   chr_loop_hacker_*.png, so the character changes the instant the page starts breathing.
   Here frame 01 IS the master, and frames 02-12 are edits of it. Identity is structural,
   not hoped for.

2. TRANSPARENCY IS NATIVE, NOT KEYED. The middleware forwards `background: "transparent"`
   to the image model on both the generate and the edit call, and the PNG comes back RGBA
   with a clean zero-alpha field (measured: 1030211 of 1048576 px at alpha 0, corners
   (0,0,0,0), no halo). That removes the whole chroma-key stage — and with it the violet
   stain the magenta route leaves on pale props, which is visible today on the shipped
   consultant's and hustler's shoelaces.

3. THE REFERENCE IS A URL, NOT A PAYLOAD. `referenceImages` takes public URLs. A
   base64 data: URI of a 1024 PNG is ~460KB and the middleware answers 413 Payload Too
   Large, so the master is uploaded once via `nexus asset upload` and its URL is cached
   in _ref_url.txt. A transparent PNG survives that round trip intact.

4. NOTHING MAY BE CUT OFF. The shipped ex-FAANG still has a roller suitcase sliced in
   half by the right edge. Asking for a margin is not enough, so every image is measured:
   the alpha bounding box must clear every edge by MARGIN_REJECT, and a failure is
   re-rolled with the defect named in the next prompt rather than re-rolled blind.
"""
import json, os, re, sys, threading, time, urllib.error, urllib.request

TOOLS = os.path.dirname(os.path.abspath(__file__))
GAME = os.path.dirname(TOOLS)
SCRATCH = "/private/tmp/claude-501/-Users-assem-Documents-Doc-Assem-Claude-Code-runway/46461c38-41e8-4daa-aa34-0dc94af8f9ef/scratchpad"
OUT = os.environ.get("ART_OUT", f"{SCRATCH}/art-char/pilot")

BASE = "https://nano-banana-production-e03b.up.railway.app"
GEN, EDIT = BASE + "/generate-image-openai", BASE + "/edit-image-openai"
UA = {"User-Agent": "curl/8.7.1"}          # the storage WAF 403s urllib's default UA

GEN_SIZE = "1024x1024"                     # width and height must both divide by 16
SHIP_SIZE = 368                            # what the game ships today (chr_arch/chr_loop)
NFRAMES = 12                               # 12 keyframes; DraftLoop divides its 2s loop
                                           # by whatever count exists, so 12 is a 6fps
                                           # boil and Godot's 0.09s timer an 11fps one
MARGIN_REJECT = 0.04                       # ink inside 4% of any edge = cut off, re-roll
MARGIN_WANT = 0.08                         # under 8% = warned about, not rejected
SPECK_FRAC = 0.004                         # an ink island under 0.4% of the total ink is
                                           # litter, not a prop (the shipped dropout loop
                                           # carries a stray blue tick in all 48 frames)
BREAKER = 3                                # consecutive endpoint failures before we stop
                                           # and report rather than burn credit
IDENTITY_MIN = 0.75                        # alpha-mask IoU against the master, after the
                                           # feet are aligned. CALIBRATED, not guessed: on
                                           # the hacker pilot, frames the eye reads as the
                                           # same drawing score 0.83-0.94, and the two the
                                           # eye also accepts (the blink pair, where the
                                           # bean narrows a little) score 0.79. A 0.80 line
                                           # therefore rejected two good frames and cost
                                           # two re-rolls to reject them again. Below ~0.7
                                           # the drawing has genuinely re-posed.


def key():
    """The key is read from the file named by ART_KEY_FILE and never stored, logged or
    echoed. Nothing in this repo may contain it."""
    p = os.environ.get("ART_KEY_FILE")
    if not p or not os.path.exists(p):
        raise SystemExit("set ART_KEY_FILE to the path of a file holding the OpenAI key")
    return open(p).read().strip()


# ---------------------------------------------------------------------------
# THE BIBLE — one law, five casts of props
# ---------------------------------------------------------------------------
# The law is the game's own, carried over verbatim in substance from the character law
# every other sprite in this project obeys, so a regenerated founder still belongs beside
# the crew and the scenes. Archetypes differ ONLY by the props they hold or sit beside.
LAW = (
    "THE CHARACTER (obey every word exactly): one small creature whose body is a single "
    "SOLID INK-BLACK bean-shaped blob with an unbroken silhouette and a thick wobbly "
    "felt-pen ink outline. The bean is TALL — clearly taller than it is wide, about three "
    "units tall to two units wide — with a rounded top and a slight forward lean, and it "
    "has no separate head: the head and the body are one single bean. Exactly ONE thin ink "
    "cowlick spike curls up from the top of the bean. Its only facial features are two "
    "blank white oval eyes, the left one slightly bigger — the eyes are SMALL, each about "
    "one fifth as wide as the bean, standing upright as narrow ovals, set close together "
    "high up in the TOP THIRD of the bean. The eyes are COMPLETELY blank: no pupils, no "
    "irises, no dots, no eyelids, no eyebrows. It has NO mouth, NO nose and NO ears. Thin "
    "black stick arms and thin black stick legs, no thicker than the cowlick. Tiny "
    "cream-white sneakers with thick ink outlines and one lace untied and curling. It "
    "wears NO clothing of any kind — no shirt, no hoodie, no jacket, no coat, no blazer, "
    "no hat, no cap — its body is a bare unbroken solid black silhouette. Different "
    "characters are told apart ONLY by the props they hold or sit beside, never by their "
    "bodies, their faces or any clothing.")

STYLE = (
    "STYLE: flat hand-drawn cartoon, wobbly felt-pen ink outlines of even weight, flat "
    "fills, no gradients, no shading, no hatching, no texture, no drop shadow, no contact "
    "shadow, no ground line drawn. "
    "PALETTE — use these colours and no others: ink black #1E1E1E, coral #E86A5C, sunny "
    "yellow #F4B942, sage green #8FA582, muted blue #6E8CA0, paper cream #F2EAD3, white.")

BACKGROUND = (
    "BACKGROUND: completely transparent and completely empty — no floor, no wall, no "
    "horizon line, no colour, no panel, no card, no frame, no border, no vignette.")

FRAMING = (
    "FRAMING (must be obeyed): exactly ONE figure in the image, centred horizontally, "
    "resting on an invisible ground line that is NOT drawn. The entire drawing — the "
    "character AND every one of its props — sits fully inside the frame with a generous "
    "empty margin: no part of it, not the cowlick spike, not a sneaker, not a can, not a "
    "corner of a laptop or a bag, comes within one tenth of any edge of the image. "
    "Nothing is cropped, clipped or cut off by the frame. The drawing fills about 80 "
    "percent of the image height and is vertically centred.")

NEGATIVE = (
    "NEGATIVE — none of these may appear: no text, no lettering, no words, no numbers, no "
    "logos, no watermark, no signature, no pupils, no mouth, no nose, no ears, no "
    "eyebrows, no clothing, no second character, no speech bubble, no thought bubble, no "
    "furniture, no chair, no desk, no table, no background scenery, no motion lines, no "
    "sparkles, no stray marks or specks anywhere in the empty space.")

CHARACTERS = {
    # props transcribed from the art the game ships today, so a regenerated founder is
    # recognisably the same person the player already met
    "hacker": dict(
        title="THE HACKER",
        props="THE POSE AND PROPS — this is THE HACKER: the creature sits cross-legged on the "
              "ground, leaning forward over an open laptop balanced on its crossed legs, both "
              "thin stick hands resting on the keyboard. The laptop is muted blue #6E8CA0 with "
              "a plain pale screen and nothing drawn on the screen. Standing on the ground "
              "beside its left knee is a small tidy group of three sunny-yellow #F4B942 energy "
              "drink cans, each with one small black lightning bolt on it. Nothing else is in "
              "the image."),
    "hustler": dict(
        title="THE HUSTLER",
        props="THE POSE AND PROPS — this is THE HUSTLER: the creature strides forward mid-step, "
              "one thin stick arm bent to press a muted-blue #6E8CA0 phone against the side of "
              "its head, the other arm held straight out to the side holding a second "
              "muted-blue phone at arm's length. A single narrow coral #E86A5C necktie hangs "
              "down the front of its body, tied at the neck. Nothing else is in the image."),
    "exfaang": dict(
        title="THE EX-FAANG PM",
        props="THE POSE AND PROPS — this is THE EX-FAANG PM: the creature stands upright and "
              "square on both sneakers. A thin cream lanyard cord hangs around its neck with a "
              "small muted-blue #6E8CA0 identity badge at the bottom of it. One thin stick hand "
              "holds a cream takeaway coffee cup with a lid; the other is raised holding up one "
              "single sunny-yellow #F4B942 square sticky note, completely blank. Nothing else "
              "is in the image."),
    "consultant": dict(
        title="THE EX-CONSULTANT",
        props="THE POSE AND PROPS — this is THE EX-CONSULTANT: the creature stands with one thin "
              "stick arm raised, holding a small dark laser pointer that emits one short thin "
              "coral #E86A5C beam up and to the side. Under its other arm it clamps a thick "
              "sage-green #8FA582 ring binder. Standing upright on the ground beside its "
              "sneakers is a small cream wheeled roller suitcase with its handle up. Nothing "
              "else is in the image."),
    "dropout": dict(
        title="THE DROPOUT",
        props="THE POSE AND PROPS — this is THE DROPOUT: the creature stands with one sneaker "
              "resting on top of a small coral #E86A5C skateboard on the ground, the other "
              "sneaker flat on the ground beside it. A sunny-yellow #F4B942 backpack hangs from "
              "one shoulder with a rolled white diploma scroll sticking out of the top of it. "
              "Nothing else is in the image."),
}

# THE 12 BEATS OF ONE BREATH. Each is generated independently from the master, so the
# cycle is described as an absolute phase rather than "a bit more than last frame" —
# there is no last frame to compare against. Frame 01 is the master itself, unedited, so
# the still and the first frame of the loop can never disagree.
BEATS = {
    1:  None,   # the master, verbatim
    2:  "the body is a hair TALLER and narrower, as if starting to breathe in, and leans a "
        "tiny amount to its own left",
    3:  "the body is slightly TALLER and narrower still, near the top of a breath in, "
        "leaning a tiny amount to its own left",
    4:  "the body is at its TALLEST and narrowest, at the very top of the breath in, and "
        "the cowlick spike tips a hair backwards",
    5:  "the body has just started to settle back down from the top of the breath, barely "
        "taller than at rest",
    6:  "the body has settled back to its resting height and the weight has shifted a tiny "
        "amount onto the other side",
    7:  "THE EYES ARE CLOSED — the two white ovals are replaced by two short blank white "
        "horizontal slits in the same places, the same width as the ovals were. The body is "
        "a hair SHORTER and wider, breathing out",
    8:  "THE EYES ARE HALF CLOSED — the two white ovals are squashed to about half their "
        "usual height, still in the same places. The body is at its SHORTEST and widest, at "
        "the bottom of the breath out",
    9:  "the eyes are fully open again and the body is barely shorter than at rest, just "
        "starting to rise",
    10: "the body has risen back to its resting height and leans a tiny amount to its own "
        "right",
    11: "the body is a hair taller than at rest and the cowlick spike tips a hair forwards",
    12: "the body is back at its exact resting height and shape, a breath away from the "
        "pose of the reference image",
}


def master_prompt(cid, fix=""):
    c = CHARACTERS[cid]
    parts = []
    if fix:
        # A blind re-roll of the same words reproduces the same defect. The gate that
        # rejected the last attempt says what was wrong, in the model's own terms, and
        # that sentence leads the next prompt.
        parts.append("A previous attempt was REJECTED for this reason — fix it explicitly "
                     "this time: " + fix)
    parts += [
        "A single character illustration for a video game character-select screen, drawn "
        "on a completely transparent background.",
        LAW, c["props"], FRAMING, STYLE, BACKGROUND, NEGATIVE,
    ]
    return " ".join(parts)


def frame_prompt(cid, n, fix=""):
    """The edit that holds identity. Everything that must NOT change is enumerated, the
    one thing that changes is stated as an absolute, and the size of the change is
    bounded — because an unbounded 'idle motion' edit re-poses the whole character."""
    c = CHARACTERS[cid]
    parts = []
    if fix:
        parts.append("The previous attempt at this frame was REJECTED for this reason — fix "
                     "it explicitly this time: " + fix)
    parts += [
        "FRAME %d OF %d of a subtle idle animation cycle." % (n, NFRAMES),
        "This is the SAME EXACT CHARACTER as the reference image, redrawn identically: the "
        "identical body shape and proportions, the identical two blank white oval eyes with "
        "the left slightly bigger, the identical single cowlick spike, the identical thin "
        "stick limbs, the identical cream sneakers, the identical props in the identical "
        "places, at the identical size, in the identical position in the frame, in the "
        "identical palette, with the identical line weight, on a transparent background.",
        "The ONLY change from the reference image is this one tiny motion: " +
        BEATS[n] + ".",
        "The change is EXTREMELY SMALL: no part of the drawing moves more than about three "
        "percent of the image width. Everything else is motionless. The sneakers stay "
        "planted in exactly the same spot, the props stay in exactly the same spot, and the "
        "character stays at exactly the same place and scale in the frame.",
        "Do NOT re-pose, re-frame, re-crop, resize, restyle or recolour the character. Do "
        "NOT add anything and do NOT remove anything. Do NOT zoom in or out.",
        c["props"], FRAMING, STYLE, BACKGROUND, NEGATIVE,
    ]
    return " ".join(parts)


# ---------------------------------------------------------------------------
# transport
# ---------------------------------------------------------------------------
class Transient(Exception):
    """The endpoint or the network misbehaved. Free to retry; never costs a re-roll."""


_state = {"streak": 0, "calls": 0, "t0": time.time()}
_lock = threading.Lock()


def _post(url, body):
    req = urllib.request.Request(url, json.dumps(body).encode(),
                                 {"Content-Type": "application/json", **UA,
                                  "x-openai-api-key": key()})
    try:
        r = json.load(urllib.request.urlopen(req, timeout=600))
    except urllib.error.HTTPError as e:
        detail = e.read()[:400].decode(errors="replace")
        if e.code in (408, 409, 425, 429, 500, 502, 503, 504):
            raise Transient("HTTP %d %s" % (e.code, detail))
        raise RuntimeError("HTTP %d %s" % (e.code, detail))
    except Exception as e:
        raise Transient("%s: %s" % (type(e).__name__, str(e)[:200]))
    if "imageUrl" not in r:
        raise RuntimeError("no imageUrl in response: %s" % list(r.keys()))
    with _lock:
        _state["calls"] += 1
    return r["imageUrl"]


def _download(url, path, tries=3):
    """Verified download — a partial body that decodes is worse than a failure, because
    it looks fine on disk and only shows up as a torn sprite in the game."""
    import io
    from PIL import Image
    last = ""
    for attempt in range(1, tries + 1):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=600) as r:
                declared = r.headers.get("Content-Length")
                data = r.read()
            if declared is not None and len(data) != int(declared):
                raise IOError("short read %d/%s" % (len(data), declared))
            if data[:8] != b"\x89PNG\r\n\x1a\n":
                raise IOError("not a PNG")
            if data[-8:-4] != b"IEND":
                raise IOError("truncated PNG (no IEND)")
            Image.open(io.BytesIO(data)).load()
            open(path, "wb").write(data)
            return path
        except Exception as e:
            last = "%s: %s" % (type(e).__name__, str(e)[:120])
            if attempt < tries:
                time.sleep(3 * attempt)
    raise Transient("download failed after %d tries: %s" % (tries, last))


def _call(kind, prompt, ref=None, quality="high"):
    """One image, with free retries for transient failures and a run-wide breaker so a
    dead endpoint stops the run instead of spending the whole budget on 502s."""
    body = {"prompt": prompt, "size": GEN_SIZE, "quality": quality,
            "output_format": "png", "background": "transparent"}
    if kind == "edit":
        body["referenceImages"] = [ref]
    url = EDIT if kind == "edit" else GEN
    for attempt in range(1, 5):
        try:
            out = _post(url, body)
            with _lock:
                _state["streak"] = 0
            return out
        except Transient as e:
            with _lock:
                _state["streak"] += 1
                streak = _state["streak"]
            print("    transient %d/4 (streak %d): %s" % (attempt, streak, e))
            if streak >= BREAKER:
                raise SystemExit("STOPPING: %d consecutive endpoint failures — reporting "
                                 "rather than burning credit. Last: %s" % (streak, e))
            time.sleep(4 * attempt)
    raise Transient("gave up after 4 transient failures")


def _upload(path):
    """A public URL for the master. The middleware answers 413 to a base64 data: URI of a
    1024 PNG, so the reference has to be hosted."""
    import subprocess
    out = subprocess.run(["nexus", "--timeout", "300", "asset", "upload", path, "--json"],
                         capture_output=True, text=True)
    try:
        j = json.loads(out.stdout)
    except json.JSONDecodeError:
        raise RuntimeError("asset upload gave no JSON: %s" % out.stdout[:200])
    url = j.get("url") or j.get("data", {}).get("url")
    if not url:
        raise RuntimeError("asset upload returned no url")
    return url


# ---------------------------------------------------------------------------
# the gates — measured, never eyeballed
# ---------------------------------------------------------------------------
def measure(path):
    """Everything the gates need from one image, in one pass."""
    import numpy as np
    from PIL import Image
    im = Image.open(path).convert("RGBA")
    W, H = im.size
    a = np.array(im.getchannel("A"))
    ink = a > 16
    m = {"path": path, "w": W, "h": H}
    if not ink.any():
        m.update(empty=True, margin=0.0, verdict="EMPTY")
        return m
    ys, xs = np.where(ink)
    l, r, t, b = int(xs.min()), int(xs.max()), int(ys.min()), int(ys.max())
    m["bbox"] = [l, t, r, b]
    m["margins"] = {"l": l / W, "t": t / H, "r": (W - 1 - r) / W, "b": (H - 1 - b) / H}
    m["margin"] = min(m["margins"].values())
    m["fill_h"] = (b - t + 1) / H
    m["fill_w"] = (r - l + 1) / W
    # transparency health: the four corners must be empty and the soft rim must stay a rim
    m["corners"] = [int(a[0, 0]), int(a[0, W - 1]), int(a[H - 1, 0]), int(a[H - 1, W - 1])]
    m["alpha0_frac"] = float((a == 0).mean())
    soft = ((a > 16) & (a < 235)).sum()
    m["soft_frac"] = float(soft / max(1, int(ink.sum())))
    # ink islands: a prop is a big island, litter is a small one
    from scipy import ndimage
    lab, n = ndimage.label(ink)
    sizes = np.bincount(lab.ravel())[1:]
    tot = float(sizes.sum())
    m["islands"] = sorted((round(float(s) / tot, 4) for s in sizes), reverse=True)[:8]
    m["specks"] = int((sizes / tot < SPECK_FRAC).sum())
    m["speck_frac"] = float(sizes[sizes / tot < SPECK_FRAC].sum() / tot) if n else 0.0
    # palette drift: how much of the ink is a colour this game does not own
    rgb = np.array(im.convert("RGB"), dtype=np.int16)[ink]
    pal = np.array([[30, 30, 30], [232, 106, 92], [244, 185, 66], [143, 165, 130],
                    [110, 140, 160], [242, 234, 211], [255, 255, 255], [0, 0, 0]], dtype=np.int16)
    d = np.abs(rgb[:, None, :] - pal[None, :, :]).sum(axis=2).min(axis=1)
    m["offpalette_frac"] = float((d > 150).mean())
    return m


def verdict(m, want_margin=MARGIN_REJECT):
    """Why an image is rejected, phrased so it can be pasted into the next prompt."""
    if m.get("empty"):
        return "the image came back empty"
    if m["margin"] < want_margin:
        side = min(m["margins"], key=m["margins"].get)
        name = {"l": "left", "t": "top", "r": "right", "b": "bottom"}[side]
        return ("part of the drawing is cut off by the %s edge of the frame — it reaches to "
                "within %.1f percent of that edge. Draw the whole character and all of its "
                "props smaller and further from every edge, with at least a tenth of the "
                "image as empty margin on all four sides" % (name, 100 * m["margin"]))
    if max(m["corners"]) > 8:
        return ("the background is not transparent — there is colour filling the corners of "
                "the image. The background must be fully transparent and empty")
    if m["speck_frac"] > 0.02 or m["specks"] > 3:
        return ("there are stray marks or specks floating in the empty space around the "
                "character. Draw only the character and its named props, on a clean empty "
                "transparent background")
    if m["offpalette_frac"] > 0.06:
        return ("colours outside the allowed palette were used. Use only ink black #1E1E1E, "
                "coral #E86A5C, sunny yellow #F4B942, sage green #8FA582, muted blue "
                "#6E8CA0, paper cream #F2EAD3 and white")
    return ""


def identity(master_path, frame_path):
    """A cheap numeric second opinion on 'is this the same drawing'. The eye judges the
    contact sheet; this catches the frame that quietly grew, moved or re-posed."""
    import numpy as np
    from PIL import Image
    A = np.array(Image.open(master_path).convert("RGBA").getchannel("A")) > 16
    B = np.array(Image.open(frame_path).convert("RGBA").getchannel("A")) > 16
    inter = float((A & B).sum())
    union = float((A | B).sum())
    return {"iou": round(inter / max(union, 1.0), 4),
            "area_ratio": round(float(B.sum()) / max(float(A.sum()), 1.0), 4)}


def align(master_path, frame_path, out_path):
    """Plant the feet. Each frame is generated independently, so its figure can sit a few
    pixels off from the master's and the loop hops. The anchor is the FEET, not the
    centroid: an idle is supposed to shift its weight, so aligning on the whole mass would
    subtract the very motion being drawn. The bottom of the ink is the soles, and the
    horizontal centre of the lowest band is where the character stands."""
    import numpy as np
    from PIL import Image

    def foot(p):
        a = np.array(Image.open(p).convert("RGBA").getchannel("A")) > 16
        ys, xs = np.where(a)
        b = int(ys.max())
        band = a[max(0, b - max(4, (b - int(ys.min())) // 12)): b + 1]
        bys, bxs = np.where(band)
        return float(bxs.mean()), b

    mx, mb = foot(master_path)
    fx, fb = foot(frame_path)
    dx, dy = int(round(mx - fx)), int(round(mb - fb))
    im = Image.open(frame_path).convert("RGBA")
    if dx or dy:
        moved = Image.new("RGBA", im.size, (0, 0, 0, 0))
        moved.paste(im, (dx, dy))
        im = moved
    im.save(out_path)
    return {"dx": dx, "dy": dy}


def despeck(path):
    """Erase ink islands too small to be a prop. The shipped dropout loop carries a stray
    blue tick in the empty space of all 48 frames; a prop is never 0.4% of the drawing."""
    import numpy as np
    from scipy import ndimage
    from PIL import Image
    im = Image.open(path).convert("RGBA")
    a = np.array(im)
    ink = a[:, :, 3] > 16
    lab, n = ndimage.label(ink)
    if n <= 1:
        return 0
    sizes = np.bincount(lab.ravel())[1:]
    tot = float(sizes.sum())
    kill = [i + 1 for i, s in enumerate(sizes) if s / tot < SPECK_FRAC]
    if not kill:
        return 0
    mask = np.isin(lab, kill)
    a[mask] = 0
    Image.fromarray(a).save(path)
    return len(kill)


# ---------------------------------------------------------------------------
# stages
# ---------------------------------------------------------------------------
def cdir(cid):
    d = f"{OUT}/{cid}"
    os.makedirs(f"{d}/_raw", exist_ok=True)
    os.makedirs(f"{d}/frames", exist_ok=True)
    return d


def _report(cid, key_, value):
    d = cdir(cid)
    p = f"{d}/_report.json"
    with _lock:
        rep = json.load(open(p)) if os.path.exists(p) else {}
        rep[key_] = value
        json.dump(rep, open(p, "w"), indent=1, sort_keys=True)


def master(cid, quality="high", tries=3, force=False):
    d = cdir(cid)
    dst = f"{d}/_master_1024.png"
    if os.path.exists(dst) and not force:
        print("%s: master exists, skipping (--force to redo)" % cid)
        return dst
    fix = ""
    for attempt in range(1, tries + 1):
        print("%s: master attempt %d/%d" % (cid, attempt, tries))
        url = _call("gen", master_prompt(cid, fix), quality=quality)
        raw = f"{d}/_raw/master_try{attempt}.png"
        _download(url, raw)
        despeck(raw)
        m = measure(raw)
        why = verdict(m)
        print("   margin %.1f%% fill %.0f%%h islands %s offpal %.1f%% -> %s"
              % (100 * m["margin"], 100 * m["fill_h"], m["islands"][:4],
                 100 * m["offpalette_frac"], why or "PASS"))
        _report(cid, "master_try%d" % attempt, {**m, "verdict": why or "PASS"})
        if not why:
            import shutil
            shutil.copyfile(raw, dst)
            if m["margin"] < MARGIN_WANT:
                print("   NOTE: margin %.1f%% is under the %.0f%% target but clears the "
                      "reject line" % (100 * m["margin"], 100 * MARGIN_WANT))
            print("%s: master saved" % cid)
            return dst
        fix = why
    raise SystemExit("%s: master failed %d attempts, last defect: %s" % (cid, tries, fix))


def ref_url(cid, force=False):
    d = cdir(cid)
    p = f"{d}/_ref_url.txt"
    if os.path.exists(p) and not force:
        return open(p).read().strip()
    url = _upload(f"{d}/_master_1024.png")
    open(p, "w").write(url)
    return url


def frames(cid, only=None, quality="high", tries=2, workers=4):
    import shutil
    d = cdir(cid)
    mp = f"{d}/_master_1024.png"
    if not os.path.exists(mp):
        raise SystemExit("%s: no master yet — run `master %s` first" % (cid, cid))
    # frame 01 IS the master: the still and the loop can then never disagree
    shutil.copyfile(mp, f"{d}/frames/frame_01.png")
    url = ref_url(cid)
    todo = [n for n in range(2, NFRAMES + 1)
            if (only is None or n in only) and
            (only is not None or not os.path.exists(f"{d}/frames/frame_%02d.png" % n))]
    if not todo:
        print("%s: all frames present" % cid)
        return
    print("%s: generating frames %s" % (cid, todo))
    results = {}

    def one(n):
        fix = ""
        for attempt in range(1, tries + 1):
            u = _call("edit", frame_prompt(cid, n, fix), ref=url, quality=quality)
            raw = f"{d}/_raw/frame_%02d_try%d.png" % (n, attempt)
            _download(u, raw)
            despeck(raw)
            aligned = f"{d}/frames/frame_%02d.png" % n
            adj = align(mp, raw, aligned)
            m = measure(aligned)
            why = verdict(m)
            ident = identity(mp, aligned)
            rec = {**m, "align": adj, **ident, "verdict": why or "PASS", "attempt": attempt}
            if not why and ident["iou"] < IDENTITY_MIN:
                why = ("the character was re-drawn too differently from the reference — keep "
                       "the identical body shape, size and position and change only the one "
                       "tiny motion named")
                rec["verdict"] = why
            print("   f%02d try%d margin %.1f%% iou %.3f area %.3f shift(%+d,%+d) -> %s"
                  % (n, attempt, 100 * m["margin"], ident["iou"], ident["area_ratio"],
                     adj["dx"], adj["dy"], why or "PASS"))
            _report(cid, "frame_%02d" % n, rec)
            if not why:
                return
            fix = why
            os.remove(aligned)
        results[n] = "FAILED"

    threads = []
    for n in todo:
        t = threading.Thread(target=one, args=(n,))
        t.start()
        threads.append(t)
        while sum(1 for x in threads if x.is_alive()) >= workers:
            time.sleep(0.4)
    for t in threads:
        t.join()
    missing = [n for n in range(1, NFRAMES + 1)
               if not os.path.exists(f"{d}/frames/frame_%02d.png" % n)]
    print("%s: frames done, %d missing %s" % (cid, len(missing), missing))


def adopt(cid, only=None, which=1):
    """Re-gate a render that is already on disk — no API call.

    An image that arrived intact is never paid for twice. When a threshold moves, when the
    alignment changes, or when the eye overrules a numeric gate, the raw 1024 render in
    _raw/ is re-aligned, re-measured and promoted from local files. This is the same
    discipline the pose library uses to re-key hundreds of sprites for free."""
    d = cdir(cid)
    mp = f"{d}/_master_1024.png"
    done = 0
    for n in range(2, NFRAMES + 1):
        if only is not None and n not in only:
            continue
        raw = f"{d}/_raw/frame_%02d_try%d.png" % (n, which)
        if not os.path.exists(raw):
            print("   f%02d: no _raw try%d to adopt" % (n, which))
            continue
        dst = f"{d}/frames/frame_%02d.png" % n
        adj = align(mp, raw, dst)
        m = measure(dst)
        why = verdict(m)
        ident = identity(mp, dst)
        if not why and ident["iou"] < IDENTITY_MIN:
            why = "re-drawn too differently from the reference"
        print("   f%02d adopt try%d margin %.1f%% iou %.3f area %.3f shift(%+d,%+d) -> %s"
              % (n, which, 100 * m["margin"], ident["iou"], ident["area_ratio"],
                 adj["dx"], adj["dy"], why or "PASS"))
        _report(cid, "frame_%02d" % n, {**m, "align": adj, **ident,
                                        "verdict": why or "PASS", "adopted_try": which})
        if why:
            os.remove(dst)
        else:
            done += 1
    print("%s: adopted %d frame(s) with no API call" % (cid, done))


def finish(cid, size=SHIP_SIZE):
    """Downscale to what the game ships and write the shipping filenames. LANCZOS on a
    premultiplied copy: straight LANCZOS on an unpremultiplied RGBA drags the colour of
    fully transparent pixels into the soft rim and rings a black sprite with grey."""
    import numpy as np
    from PIL import Image
    d = cdir(cid)
    out = []
    for n in range(1, NFRAMES + 1):
        src = f"{d}/frames/frame_%02d.png" % n
        if not os.path.exists(src):
            continue
        a = np.array(Image.open(src).convert("RGBA"), dtype=np.float32)
        al = a[:, :, 3:4] / 255.0
        a[:, :, :3] *= al
        small = Image.fromarray(a.astype(np.uint8)).resize((size, size), Image.LANCZOS)
        s = np.array(small, dtype=np.float32)
        al2 = np.clip(s[:, :, 3:4] / 255.0, 1e-4, 1.0)
        s[:, :, :3] = np.clip(s[:, :, :3] / al2, 0, 255)
        fin = Image.fromarray(s.astype(np.uint8))
        p = f"{d}/chr_loop_{cid}_%02d.png" % n
        fin.save(p)
        out.append(p)
        if n == 1:
            fin.save(f"{d}/chr_arch_{cid}.png")
    print("%s: wrote chr_arch_%s.png + %d chr_loop frames at %dx%d"
          % (cid, cid, len(out), size, size))
    return out


def sheet(cid, cell=300):
    """The contact sheet the eye judges. Frames on a light card and the master repeated in
    the last cell, so a drift between frame 01 and frame 12 is a glance, not a diff."""
    from PIL import Image, ImageDraw
    d = cdir(cid)
    cols, rows = 4, 4
    W = cols * cell
    H = rows * cell + 34
    sh = Image.new("RGB", (W, H), (246, 243, 235))
    dr = ImageDraw.Draw(sh)
    dr.text((10, 10), "%s — 12 idle frames + master, alpha on cream" % cid, fill=(30, 30, 30))
    slots = [("frame_%02d" % n, f"{d}/frames/frame_%02d.png" % n) for n in range(1, NFRAMES + 1)]
    slots += [("MASTER", f"{d}/_master_1024.png"),
              ("f01 vs f12", None), ("checker f07", None), ("checker f01", None)]
    for i, (label, p) in enumerate(slots):
        x, y = (i % cols) * cell, 34 + (i // cols) * cell
        tile = Image.new("RGBA", (cell, cell), (255, 255, 255, 255))
        if p and os.path.exists(p):
            tile.alpha_composite(Image.open(p).convert("RGBA").resize((cell, cell), Image.LANCZOS))
        elif label == "f01 vs f12":
            # the loop's seam: frame 01 in ink, frame 12 in coral, over each other
            a = f"{d}/frames/frame_01.png"
            b = f"{d}/frames/frame_%02d.png" % NFRAMES
            if os.path.exists(a) and os.path.exists(b):
                import numpy as np
                A = np.array(Image.open(a).convert("RGBA").resize((cell, cell), Image.LANCZOS))
                B = np.array(Image.open(b).convert("RGBA").resize((cell, cell), Image.LANCZOS))
                t = np.full((cell, cell, 3), 255, dtype=np.uint8)
                t[A[:, :, 3] > 40] = [30, 30, 30]
                m = B[:, :, 3] > 40
                t[m] = (t[m] * 0.45 + np.array([232, 106, 92]) * 0.55).astype(np.uint8)
                tile = Image.fromarray(t).convert("RGBA")
        elif label.startswith("checker"):
            # a real transparency check: any halo shows against a mid-grey checkerboard
            fn = f"{d}/frames/frame_%s.png" % label.split()[-1][1:]
            ch = Image.new("RGBA", (cell, cell))
            for yy in range(0, cell, 20):
                for xx in range(0, cell, 20):
                    c = 205 if ((xx // 20 + yy // 20) % 2) else 150
                    ch.paste((c, c, c, 255), (xx, yy, min(xx + 20, cell), min(yy + 20, cell)))
            if os.path.exists(fn):
                ch.alpha_composite(Image.open(fn).convert("RGBA").resize((cell, cell), Image.LANCZOS))
            tile = ch
        sh.paste(tile.convert("RGB"), (x, y))
        dr.rectangle([x, y, x + cell - 1, y + cell - 1], outline=(210, 205, 195))
        dr.text((x + 6, y + 6), label, fill=(120, 115, 105))
    p = f"{d}/_contact.png"
    sh.save(p)
    print("%s: contact sheet -> %s" % (cid, p))
    return p


def preview(cid, fps=6, size=480):
    """An animated GIF of the loop at the rate the game actually plays it, over the select
    stage. A contact sheet proves identity; only motion proves the loop reads as breathing
    and not as a jitter, and Unity runs 12 frames over its 2s LoopSeconds — 6fps."""
    from PIL import Image
    d = cdir(cid)
    stage = f"{GAME}/../unity/Assets/Art/env/stage.png"
    if os.path.exists(stage):
        s = Image.open(stage).convert("RGBA")
        # the hero box is a 560 square on a 1080-tall page: crop the stage the same way
        s = s.resize((int(size * 1.6), int(size * 1.6 * s.size[1] / s.size[0])), Image.LANCZOS)
        bx = (s.size[0] - size) // 2
        by = int(s.size[1] * 0.30)
        back = s.crop((bx, by, bx + size, by + size))
    else:
        back = Image.new("RGBA", (size, size), (242, 234, 211, 255))
    out = []
    for n in range(1, NFRAMES + 1):
        p = f"{d}/chr_loop_{cid}_%02d.png" % n
        if not os.path.exists(p):
            continue
        f = back.copy()
        f.alpha_composite(Image.open(p).convert("RGBA").resize((size, size), Image.LANCZOS))
        out.append(f.convert("P", palette=Image.ADAPTIVE, colors=128))
    if not out:
        raise SystemExit("%s: no shipping frames — run finish first" % cid)
    p = f"{d}/_loop_{fps}fps.gif"
    out[0].save(p, save_all=True, append_images=out[1:], duration=int(1000 / fps), loop=0,
                disposal=2)
    print("%s: %d-frame loop at %dfps -> %s" % (cid, len(out), fps, p))
    return p


def audit(cid):
    d = cdir(cid)
    rows = []
    for n in range(1, NFRAMES + 1):
        p = f"{d}/frames/frame_%02d.png" % n
        if not os.path.exists(p):
            rows.append((("f%02d" % n), None))
            continue
        m = measure(p)
        i = identity(f"{d}/_master_1024.png", p)
        rows.append((("f%02d" % n), {**m, **i}))
    print("%-5s %7s %7s %6s %6s %7s %7s %s" %
          ("frame", "margin", "fillH", "iou", "area", "soft%", "offpal", "verdict"))
    ok = 0
    for name, m in rows:
        if m is None:
            print("%-5s  MISSING" % name)
            continue
        v = verdict(m) or "PASS"
        ok += v == "PASS"
        print("%-5s %6.1f%% %6.1f%% %6.3f %6.3f %6.1f%% %6.1f%% %s"
              % (name, 100 * m["margin"], 100 * m["fill_h"], m["iou"], m["area_ratio"],
                 100 * m["soft_frac"], 100 * m["offpalette_frac"], v))
    print("%s: %d/%d frames pass every gate" % (cid, ok, NFRAMES))
    return ok


def run(cid, quality="high"):
    master(cid, quality=quality)
    frames(cid, quality=quality)
    finish(cid)
    sheet(cid)
    preview(cid)
    audit(cid)
    print("calls made: %d in %.0fs" % (_state["calls"], time.time() - _state["t0"]))


if __name__ == "__main__":
    cmd = sys.argv[1]
    args = sys.argv[2:]
    cid = args[0] if args and not args[0].startswith("-") else None
    opt = {}
    for i, a in enumerate(args):
        if a.startswith("--"):
            opt[a[2:]] = args[i + 1] if i + 1 < len(args) and not args[i + 1].startswith("--") else True
    only = [int(x) for x in re.split(r"[,\s]+", opt["only"])] if "only" in opt else None
    q = opt.get("quality", "high")
    if cmd == "master":
        master(cid, quality=q, tries=int(opt.get("tries", 3)), force="force" in opt)
    elif cmd == "frames":
        frames(cid, only=only, quality=q, tries=int(opt.get("tries", 2)),
               workers=int(opt.get("workers", 4)))
    elif cmd == "adopt":
        adopt(cid, only=only, which=int(opt.get("try", 1)))
    elif cmd == "finish":
        finish(cid, int(opt.get("size", SHIP_SIZE)))
    elif cmd == "sheet":
        sheet(cid)
    elif cmd == "preview":
        preview(cid, int(opt.get("fps", 6)))
    elif cmd == "audit":
        for c in (CHARACTERS if cid is None else [cid]):
            audit(c)
    elif cmd == "run":
        run(cid, quality=q)
    else:
        raise SystemExit(__doc__)
