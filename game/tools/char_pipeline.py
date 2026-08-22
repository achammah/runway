#!/usr/bin/env python3
"""THE CHARACTER PIPELINE — one master illustration per archetype, then its idle loop.

    export ART_KEY_FILE=/path/to/openai-key.txt        # the key NEVER lives in this file
    python3 tools/char_pipeline.py master  <id> [--quality high] [--tries 3]
    python3 tools/char_pipeline.py frames  <id> [--only 3,7] [--workers 4] [--tries 2]
    python3 tools/char_pipeline.py adopt   <id> [--only 7,8] [--try 1]   # re-gate, no API
    python3 tools/char_pipeline.py promote <id> --try 4 [--refit]        # a raw becomes the master
    python3 tools/char_pipeline.py sheet   <id>
    python3 tools/char_pipeline.py preview <id> [--fps 6]                # animated GIF
    python3 tools/char_pipeline.py stage   <id> [--frame 1] [--plain]    # feet vs the pool
    python3 tools/char_pipeline.py family  [--ids a,b,c]       # the whole cast, one baseline
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
    _on_stage.png        the founder standing on the real select stage, pool ellipse marked
    _loop_6fps.gif       the loop as Unity plays it, over that same stage
    _report.json         every gate measurement for every image
and one sheet for the whole cast at OUT/_family.png.

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
FOOT_MARGIN = 0.072                        # WHERE THE SOLES SIT. Every master is shifted
                                           # vertically until the bottom of its ink leaves
                                           # exactly this much of the image empty beneath
                                           # it, and every frame is then planted on the
                                           # master's feet — so all five founders stand on
                                           # ONE baseline instead of five. CALIBRATED
                                           # against the screen that shows them:
                                           # DraftSelectPage puts the hero in a 560 box at
                                           # y 240 and paints the contact-shadow ellipse at
                                           # y 742..788, so the pool's near rim is at 10.4%
                                           # of the box and its centre at 6.25%. The pilot
                                           # master drew its soles at 9.3% — grazing the
                                           # rim, the founder standing on the far edge of
                                           # his own light. 7.2% drops him ~2% of the box
                                           # lower: soles well inside the pool and just
                                           # above its centre, which is where a figure
                                           # stands in a ground ellipse.
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
              "on the LOWER HALF of the front of its body. The necktie has NO KNOT: it is one "
              "single narrow flat coral shape that begins at the MIDDLE of the bean's height "
              "— at least one third of the bean's height BELOW the eyes — and hangs straight "
              "down from there towards the bottom of the bean. The whole area between the eyes "
              "and the middle of the bean is empty solid black with absolutely nothing drawn "
              "on it: a knot, a triangle or any coral shape under the eyes reads as a MOUTH, "
              "and this creature has no mouth. Nothing else is in the image."),
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


def foot(p):
    """Where the drawing stands: the row of its lowest ink, and the horizontal centre of
    the lowest band of it — the soles, not the centroid. An idle shifts its weight, so a
    centroid would move with the very motion being drawn, and planting on it would subtract
    the animation."""
    import numpy as np
    from PIL import Image
    a = np.array(Image.open(p).convert("RGBA").getchannel("A")) > 16
    ys, xs = np.where(a)
    b = int(ys.max())
    band = a[max(0, b - max(4, (b - int(ys.min())) // 12)): b + 1]
    bys, bxs = np.where(band)
    return float(bxs.mean()), b


def align(master_path, frame_path, out_path):
    """Plant the feet. Each frame is generated independently, so its figure can sit a few
    pixels off from the master's and the loop hops. The anchor is the FEET, not the
    centroid: an idle is supposed to shift its weight, so aligning on the whole mass would
    subtract the very motion being drawn. The bottom of the ink is the soles, and the
    horizontal centre of the lowest band is where the character stands."""
    from PIL import Image
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


def seat(path, margin=FOOT_MARGIN):
    """Put the drawing on the house baseline. The model composes freely inside the square,
    so one master lands with its soles at 9% of the height and the next at 6%, and the
    select stage — whose contact-shadow ellipse is painted at a FIXED y — then has one
    founder standing in his light and the next hovering above it. Seating is a whole-image
    integer translate, so it costs nothing, loses nothing, and is idempotent: the frames
    align onto the seated master and inherit the same ground for free."""
    import numpy as np
    from PIL import Image
    im = Image.open(path).convert("RGBA")
    W, H = im.size
    a = np.array(im.getchannel("A")) > 16
    if not a.any():
        return 0
    dy = int(round((H - 1 - margin * H) - int(np.where(a)[0].max())))
    if dy:
        moved = Image.new("RGBA", im.size, (0, 0, 0, 0))
        moved.paste(im, (0, dy))
        moved.save(path)
    return dy


def fit(path, want=MARGIN_WANT, floor=FOOT_MARGIN):
    """Shrink a drawing that is simply drawn too big for its frame, about the centre of
    its own feet. The model is asked for 80% fill and sometimes gives 92%, and 92% cannot
    be seated on the house baseline without shoving the cowlick off the top — a defect the
    margin gate then rejects, at the price of a whole re-roll. But 'draw it smaller' is a
    transformation, not a redrawing: the render on disk is already the right character with
    the right props, and scaling it is exactly what the re-roll would have asked for. It is
    applied only where the eye keeps a render the gate refused, it is recorded in the
    report, and the gates are then run again on the result — never silently, never to
    rescue a drawing that is wrong rather than big."""
    import numpy as np
    from PIL import Image
    im = Image.open(path).convert("RGBA")
    W, H = im.size
    a = np.array(im.getchannel("A")) > 16
    if not a.any():
        return 1.0
    ys, xs = np.where(a)
    hi = (int(ys.max()) - int(ys.min()) + 1) / H
    wi = (int(xs.max()) - int(xs.min()) + 1) / W
    s = min(1.0, (1.0 - floor - want) / hi, (1.0 - 2 * want) / wi)
    if s >= 0.999:
        return 1.0
    n = im.resize((int(round(W * s)), int(round(H * s))), Image.LANCZOS)
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    fx, _ = foot(path)
    out.paste(n, (int(round(fx - fx * s)), (H - n.size[1]) // 2))   # feet stay under the body
    out.save(path)
    return round(s, 4)


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


def master(cid, quality="high", tries=3, force=False, fix=""):
    """`fix` lets the EYE name a defect the numbers cannot see — a tie knot that reads as
    a mouth, a bean gone squat — in the same channel the gates use. A blind re-roll of the
    same words reproduces the same drawing, so a rejection is never silent."""
    import shutil
    d = cdir(cid)
    dst = f"{d}/_master_1024.png"
    if os.path.exists(dst):
        if not force:
            print("%s: master exists, skipping (--force to redo)" % cid)
            return dst
        # a rejected master is kept, never overwritten: it is the before-shot of the fix
        v = 1
        while os.path.exists(f"{d}/_master_v{v}.png"):
            v += 1
        shutil.copyfile(dst, f"{d}/_master_v{v}.png")
        print("%s: previous master kept as _master_v%d.png" % (cid, v))
    done = len([f for f in os.listdir(f"{d}/_raw") if f.startswith("master_try")])
    for attempt in range(done + 1, done + tries + 1):
        print("%s: master attempt %d%s" % (cid, attempt, " (fix: %s)" % fix[:60] if fix else ""))
        url = _call("gen", master_prompt(cid, fix), quality=quality)
        raw = f"{d}/_raw/master_try{attempt}.png"
        _download(url, raw)
        despeck(raw)
        # seat BEFORE the gates, never after: the margins that get measured have to be the
        # margins the shipped image actually has, and seating moves them
        dy = seat(raw)
        m = measure(raw)
        why = verdict(m)
        print("   seat %+dpx margin %.1f%% fill %.0f%%h islands %s offpal %.1f%% -> %s"
              % (dy, 100 * m["margin"], 100 * m["fill_h"], m["islands"][:4],
                 100 * m["offpalette_frac"], why or "PASS"))
        _report(cid, "master_try%d" % attempt, {**m, "seat_dy": dy,
                                                "verdict": why or "PASS"})
        if not why:
            shutil.copyfile(raw, dst)
            if m["margin"] < MARGIN_WANT:
                print("   NOTE: margin %.1f%% is under the %.0f%% target but clears the "
                      "reject line" % (100 * m["margin"], 100 * MARGIN_WANT))
            print("%s: master saved" % cid)
            return dst
        fix = why
    raise SystemExit("%s: master failed %d attempts, last defect: %s" % (cid, tries, fix))


def promote(cid, which=1, refit=False):
    """Make an existing raw render the master — no API call. The eye picks which of the
    tries on disk is the character (the numbers cannot see a tie knot that reads as a
    mouth), `--refit` shrinks one that was only ever too big, and the gates then judge the
    result exactly as they judge a fresh render."""
    import shutil
    d = cdir(cid)
    raw = f"{d}/_raw/master_try{which}.png"
    if not os.path.exists(raw):
        raise SystemExit("%s: no _raw/master_try%d.png to promote" % (cid, which))
    dst = f"{d}/_master_1024.png"
    work = f"{d}/_raw/master_try{which}_fit.png"
    shutil.copyfile(raw, work)
    s = fit(work) if refit else 1.0
    dy = seat(work)
    m = measure(work)
    why = verdict(m)
    print("%s: promote try%d  scale %.3f seat %+dpx  margin %.1f%% fill %.0f%%h -> %s"
          % (cid, which, s, dy, 100 * m["margin"], 100 * m["fill_h"], why or "PASS"))
    if why:
        raise SystemExit("%s: try%d still fails the gates after refit: %s" % (cid, which, why))
    if os.path.exists(dst):
        v = 1
        while os.path.exists(f"{d}/_master_v{v}.png"):
            v += 1
        shutil.copyfile(dst, f"{d}/_master_v{v}.png")
    shutil.copyfile(work, dst)
    _report(cid, "master_promoted", {**m, "from_try": which, "scale": s, "seat_dy": dy,
                                     "verdict": "PASS"})
    print("%s: master saved from try%d" % (cid, which))
    return dst


def ref_url(cid, force=False):
    d = cdir(cid)
    p = f"{d}/_ref_url.txt"
    if os.path.exists(p) and not force:
        return open(p).read().strip()
    url = _upload(f"{d}/_master_1024.png")
    open(p, "w").write(url)
    return url


def frames(cid, only=None, quality="high", tries=2, workers=4, fix=""):
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
        why_last = fix          # the eye's own words lead the first try when it re-rolls
        for attempt in range(1, tries + 1):
            u = _call("edit", frame_prompt(cid, n, why_last), ref=url, quality=quality)
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
            why_last = why
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


def adopt(cid, only=None, which=1, eye=False):
    """Re-gate a render that is already on disk — no API call.

    An image that arrived intact is never paid for twice. When a threshold moves, when the
    alignment changes, or when the eye overrules a numeric gate, the raw 1024 render in
    _raw/ is re-aligned, re-measured and promoted from local files. This is the same
    discipline the pose library uses to re-key hundreds of sprites for free."""
    import shutil
    d = cdir(cid)
    mp = f"{d}/_master_1024.png"
    # the master is re-seated first and frame 01 re-cut from it, because every other frame
    # is planted on the master's feet: move the master and the whole loop follows for free
    dy = seat(mp)
    if dy:
        print("   master re-seated %+dpx to the %.1f%% baseline" % (dy, 100 * FOOT_MARGIN))
    shutil.copyfile(mp, f"{d}/frames/frame_01.png")
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
        overruled = ""
        if not why and ident["iou"] < IDENTITY_MIN:
            if eye:
                # THE EYE OVERRULES THE NUMBER, and only this number. IoU is a proxy for
                # "same drawing" and it is unfair to a wide stride: a walking figure's thin
                # splayed legs are a large share of a small mask, so a two-degree change in
                # a shin costs more IoU than a whole bean narrowing does on a standing one.
                # The margin, transparency, speck and palette gates measure the image
                # itself and are never overridable — this one measures a resemblance, which
                # is what an eye is for. Every override is recorded as an override.
                overruled = "identity overruled by eye at iou %.3f" % ident["iou"]
            else:
                why = "re-drawn too differently from the reference"
        print("   f%02d adopt try%d margin %.1f%% iou %.3f area %.3f shift(%+d,%+d) -> %s"
              % (n, which, 100 * m["margin"], ident["iou"], ident["area_ratio"],
                 adj["dx"], adj["dy"], why or overruled or "PASS"))
        _report(cid, "frame_%02d" % n, {**m, "align": adj, **ident, "adopted_try": which,
                                        "verdict": why or "PASS", "override": overruled})
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


# THE SELECT SCREEN, IN ITS OWN NUMBERS. Every value below is read off
# DraftSelectPage.Build() so a preview cannot flatter the art with a kinder layout than
# the game gives it: a 1536x1024 page, the founder in a 560 box at (335,240), and
# GameUi.Shadow's contact ellipse at (465,742) sized 300x46 in ink at 35% alpha.
PAGE = (1536, 1024)
HERO = (335, 240, 560, 560)
POOL = (465, 742, 300, 46)
WINDOW = (315, 245, 600, 600)              # what a preview crops out of that page


def stage_plate(marks=False):
    """The select stage as the player sees it, with the contact-shadow ellipse painted
    where the engine paints it. Compositing a sprite over a bare stage.png answers a
    different question than the one that matters — the founder can look fine on the floor
    and still hover over his own shadow."""
    from PIL import Image, ImageDraw
    # the same file the screen picks, in the same order (FounderDraftScreen.OnBuild), so a
    # preview never flatters the art with a stage the player will not see
    art = os.path.join(GAME, os.pardir, "unity/Assets/Art/env")
    back = None
    for name in ("select_stage_scene.png", "stage.png"):
        if os.path.exists(f"{art}/{name}"):
            back = Image.open(f"{art}/{name}").convert("RGBA").resize(PAGE, Image.LANCZOS)
            break
    if back is None:
        back = Image.new("RGBA", PAGE, (26, 38, 54, 255))
    pool = Image.new("RGBA", PAGE, (0, 0, 0, 0))
    ImageDraw.Draw(pool).ellipse([POOL[0], POOL[1], POOL[0] + POOL[2], POOL[1] + POOL[3]],
                                 fill=(10, 10, 10, 89))
    back.alpha_composite(pool)
    if marks:
        dr = ImageDraw.Draw(back)
        dr.ellipse([POOL[0], POOL[1], POOL[0] + POOL[2], POOL[1] + POOL[3]],
                   outline=(110, 200, 255, 255), width=3)
        dr.line([POOL[0] - 40, POOL[1] + POOL[3] // 2, POOL[0] + POOL[2] + 40,
                 POOL[1] + POOL[3] // 2], fill=(110, 200, 255, 160), width=1)
    return back


def on_stage(img_path, marks=False, size=600):
    """One frame standing on the select stage, cropped to the window around the hero box."""
    from PIL import Image
    plate = stage_plate(marks)
    fig = Image.open(img_path).convert("RGBA").resize((HERO[2], HERO[3]), Image.LANCZOS)
    plate.alpha_composite(fig, (HERO[0], HERO[1]))
    x, y, w, h = WINDOW
    out = plate.crop((x, y, x + w, y + h))
    return out.resize((size, size), Image.LANCZOS) if size != w else out


def stage_still(cid, frame=1, marks=True):
    """The verification image for the baseline: is the founder standing IN the pool?"""
    d = cdir(cid)
    src = f"{d}/chr_loop_{cid}_%02d.png" % frame
    if not os.path.exists(src):
        src = f"{d}/frames/frame_%02d.png" % frame
    p = f"{d}/_on_stage.png"
    on_stage(src, marks=marks).convert("RGB").save(p)
    # where the soles land, in the page's own coordinates, against the pool's own
    import numpy as np
    from PIL import Image
    a = np.array(Image.open(src).convert("RGBA").getchannel("A")) > 16
    ys = np.where(a)[0]
    sole = HERO[1] + HERO[3] * (int(ys.max()) + 1) / a.shape[0]
    top, mid, bot = POOL[1], POOL[1] + POOL[3] / 2.0, POOL[1] + POOL[3]
    where = ("IN THE POOL" if top <= sole <= bot else
             "ABOVE the pool by %.0fpx" % (top - sole) if sole < top else
             "BELOW the pool by %.0fpx" % (sole - bot))
    print("%s: soles at page y=%.0f — pool %d..%d (centre %.0f) -> %s"
          % (cid, sole, top, bot, mid, where))
    print("%s: stage still -> %s" % (cid, p))
    return p


def preview(cid, fps=6, size=480):
    """An animated GIF of the loop at the rate the game actually plays it, over the select
    stage. A contact sheet proves identity; only motion proves the loop reads as breathing
    and not as a jitter, and Unity runs 12 frames over its 1s LoopSeconds — 12 frames is a
    6fps boil, while Godot's 0.09s timer plays the same sheet at 11fps."""
    from PIL import Image
    d = cdir(cid)
    out = []
    for n in range(1, NFRAMES + 1):
        p = f"{d}/chr_loop_{cid}_%02d.png" % n
        if not os.path.exists(p):
            continue
        out.append(on_stage(p, size=size).convert("P", palette=Image.ADAPTIVE, colors=128))
    if not out:
        raise SystemExit("%s: no shipping frames — run finish first" % cid)
    p = f"{d}/_loop_{fps}fps.gif"
    out[0].save(p, save_all=True, append_images=out[1:], duration=int(1000 / fps), loop=0,
                disposal=2)
    print("%s: %d-frame loop at %dfps -> %s" % (cid, len(out), fps, p))
    return p


def family(ids=None, cell=520, out=None):
    """THE WHOLE CAST IN ONE LINE-UP. Each founder is judged alone on a contact sheet — but
    the player meets them as a row, one swapping in for the last inside the same lit box,
    and the only sheet that can catch a founder drawn a size too big or standing a step too
    high is the one that puts all five at the same scale on ONE ground line. The pool under
    each is the select stage's own contact ellipse, scaled to this sheet: a founder whose
    soles miss it here misses it in the game."""
    from PIL import Image, ImageDraw
    ids = ids or [c for c in CHARACTERS if os.path.exists(f"{OUT}/{c}/_master_1024.png")]
    pad = 44
    W, H = cell * len(ids), cell + 96
    sh = Image.new("RGB", (W, H), (246, 243, 235))
    dr = ImageDraw.Draw(sh)
    dr.text((12, 14), "RUNWAY! founders — %s. One scale, one baseline: every figure is its "
                      "own 1024 master dropped into an identical box, so the ground line and "
                      "the pool below are literally the same row for all of them "
                      "(foot margin %.1f%% of the box)."
            % (", ".join(ids), 100 * FOOT_MARGIN), fill=(30, 30, 30))
    box = cell - 2 * pad                       # the shared 'hero box' each founder stands in
    top = 40 + pad
    ground = top + int(box * (1 - FOOT_MARGIN))
    # the stage's own ellipse, in this sheet's units: 300x46 inside a 560 box, centred on
    # the box and on the baseline
    ew, eh = box * 300.0 / 560.0, box * 46.0 / 560.0
    for i, cid in enumerate(ids):
        x = i * cell + pad
        pool = Image.new("RGBA", (int(ew) + 4, int(eh) + 4), (0, 0, 0, 0))
        ImageDraw.Draw(pool).ellipse([2, 2, int(ew), int(eh)], fill=(10, 10, 10, 89))
        sh.paste(Image.alpha_composite(
            Image.new("RGBA", pool.size, (246, 243, 235, 255)), pool).convert("RGB"),
            (int(x + (box - ew) / 2), int(ground - eh / 2)))
        fig = Image.open(f"{OUT}/{cid}/_master_1024.png").convert("RGBA").resize(
            (box, box), Image.LANCZOS)
        tile = Image.new("RGBA", (box, box), (0, 0, 0, 0))
        tile.alpha_composite(fig)
        sh.paste(tile.convert("RGB"), (x, top), tile)
        dr.text((i * cell + 12, 46), CHARACTERS[cid]["title"], fill=(60, 56, 50))
        # the numbers behind "same ground", measured on the shipping image
        src = f"{OUT}/{cid}/chr_arch_{cid}.png"
        if not os.path.exists(src):
            src = f"{OUT}/{cid}/_master_1024.png"
        m = measure(src)
        fx, fb = foot(src)
        n = m["h"]
        dr.text((i * cell + 12, cell + 52),
                "%s   foot margin %.1f%%   fills %.0f%% of height   soles %+.1f%% off centre"
                % (cid, 100 * (n - 1 - fb) / n, 100 * m["fill_h"],
                   100 * (fx - m["w"] / 2.0) / m["w"]), fill=(90, 86, 78))
    dr.line([0, ground, W, ground], fill=(200, 120, 105), width=1)
    p = out or f"{OUT}/_family.png"
    sh.save(p)
    print("family sheet (%d founders) -> %s" % (len(ids), p))
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
    preview(cid, 6)
    preview(cid, 11)
    stage_still(cid)
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
        master(cid, quality=q, tries=int(opt.get("tries", 3)), force="force" in opt,
               fix=opt.get("fix", "") if isinstance(opt.get("fix", ""), str) else "")
    elif cmd == "frames":
        frames(cid, only=only, quality=q, tries=int(opt.get("tries", 2)),
               workers=int(opt.get("workers", 4)),
               fix=opt.get("fix", "") if isinstance(opt.get("fix", ""), str) else "")
    elif cmd == "adopt":
        adopt(cid, only=only, which=int(opt.get("try", 1)), eye="eye" in opt)
    elif cmd == "promote":
        promote(cid, which=int(opt.get("try", 1)), refit="refit" in opt)
    elif cmd == "finish":
        finish(cid, int(opt.get("size", SHIP_SIZE)))
    elif cmd == "sheet":
        sheet(cid)
    elif cmd == "preview":
        preview(cid, int(opt.get("fps", 6)))
    elif cmd == "stage":
        stage_still(cid, int(opt.get("frame", 1)), marks="plain" not in opt)
    elif cmd == "family":
        family([c.strip() for c in opt["ids"].split(",")] if "ids" in opt else None)
    elif cmd == "audit":
        for c in (CHARACTERS if cid is None else [cid]):
            audit(c)
    elif cmd == "run":
        run(cid, quality=q)
    else:
        raise SystemExit(__doc__)
