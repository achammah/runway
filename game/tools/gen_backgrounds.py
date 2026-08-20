#!/usr/bin/env python3
"""RUNWAY! — batch generation of the background library (the 516 EMPTY ROOMS).

A background is a ROOM, not a scene. The cast composites on top as sprites, so
anybody painted into a background DOUBLES against the composited crew. Every
prompt here therefore comes straight out of tools/backgrounds_taxonomy.py, which
is the single source of truth for what each room is and ends every line with
EMPTY OF PEOPLE — this file never invents prompt text of its own. The shared
STYLE block (palette, UI safe zones, blank writing surfaces, character law) is
imported from scene_pipeline so the library cannot drift from the hand-built
stages.

  python3 tools/gen_backgrounds.py run [--workers 12] [--limit N] [--only SUBSTR]
  python3 tools/gen_backgrounds.py verify   # truncation gate over assets/backgrounds
  python3 tools/gen_backgrounds.py status   # how many of the manifest exist and pass
  python3 tools/gen_backgrounds.py missing  # ids with no intact file yet

MEASURED SETTINGS (do not "improve" these blind):
  quality=low  is the owner's chosen tradeoff — 28s per image, and the higher
  tiers were judged not worth the extra wall time for a room that is only ever
  a backdrop.
  workers=12   measured at 31 images/min with no degradation: twelve concurrent
  requests finished in the same wall time as four.

RESUMABLE BY CONSTRUCTION. Any id whose png already exists and passes the
truncation check is skipped, so a killed run costs only its in-flight images.
index.json (manifest id -> filename) is rewritten atomically after every single
completion for the same reason.

DOWNLOADS GO THROUGH scene_pipeline._fetch. urllib.request.urlretrieve writes a
partial file and returns SUCCESS when a connection drops — that silently shipped
two truncated scenes earlier in this project. _fetch checks Content-Length,
requires a complete PNG (signature + IEND), decodes the pixels, and retries.
"""
import argparse, io, json, os, subprocess, sys, threading, time
from concurrent.futures import ThreadPoolExecutor, as_completed

TOOLS = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, TOOLS)
from scene_pipeline import STYLE, MIDDLEWARE, _post_json, _fetch, _key, GAME  # noqa: E402

BG = os.path.join(GAME, "assets", "backgrounds")
MANIFEST = os.path.join(BG, "manifest.json")
INDEX = os.path.join(BG, "index.json")
LOG_DIR = "/tmp/lane_batch"
LOG = os.path.join(LOG_DIR, "gen.log")
PROGRESS = os.path.join(LOG_DIR, "progress.md")
PNG_SIG = b"\x89PNG\r\n\x1a\n"

_lock = threading.Lock()

# ---------------------------------------------------------------- the prompt
# A BACKGROUND IS AN EMPTY ROOM. The shared STYLE block ends with the CHARACTER
# LAW: two hundred words describing the crew (ink-black bean, cowlick, blank
# white eyes, untied lace) so that a *scene* draws them on-model. In a background
# that paragraph is the enemy. Measured: two rooms generated with STYLE verbatim,
# one came back with THREE creatures in it — a lovingly detailed description of a
# creature outweighs the three words "EMPTY OF PEOPLE" at the end of the room
# text, exactly the way "a laptop balanced on a knee" outweighed it for the pilot.
# And an occupant here is not a cosmetic flaw: the cast composites on top as
# sprites, so a painted-in creature DOUBLES against the real crew.
#
# So the background style is STYLE with the character law CUT and an emptiness
# clause put in its place. Everything the library must agree on — palette, UI safe
# zones, blank writing surfaces — is inherited unchanged from scene_pipeline, so
# the rooms cannot drift away from the hand-built stages.
_CHARACTER_LAW = "THE CHARACTERS (this never requires a creature to appear"
_EMPTY = ("NOBODY IS IN THIS ROOM (must be obeyed, this is the single most important rule): the room is "
          "COMPLETELY UNOCCUPIED. Draw NO people, NO characters, NO creatures, NO figures, NO blobs, NO "
          "animals, NO silhouettes, NO faces, NO eyes, NO hands, NO limbs, NO body parts and NO shadows or "
          "reflections of a person anywhere in the image. Nothing alive is in the frame. It is an EMPTY "
          "ROOM photographed with everyone gone: the chairs are unoccupied, the desks are unattended, the "
          "doorways are clear. The furniture, the objects and the light tell the whole story on their own.")


def bg_style():
    head = STYLE.split(_CHARACTER_LAW)[0].rstrip()
    assert head != STYLE.rstrip(), "character-law marker not found in STYLE — check scene_pipeline"
    return head + " " + _EMPTY


def filename(mid):
    """manifest id -> flat filename. '/' is not legal in a filename and a flat
    directory keeps the Godot import path trivial."""
    return mid.replace("/", "__") + ".png"


def intact(path):
    """True only for a complete, decodable PNG. Same test as the verify gate: a
    half-downloaded image still imports fine and only shows up as a cut-off room
    in the game, so 'the file exists' is never good enough to skip on."""
    try:
        with open(path, "rb") as f:
            data = f.read()
    except OSError:
        return False
    if data[:8] != PNG_SIG or data[-8:-4] != b"IEND":
        return False
    try:
        from PIL import Image
        Image.open(io.BytesIO(data)).load()
    except Exception:
        return False
    return True


# ------------------------------------------------------- the emptiness gate
# Asking is not enough (see above), so every finished room is MEASURED for
# occupants before it is accepted. The crew is the easiest thing in this style to
# find mechanically: a SOLID ink-black mass with one or two BLANK WHITE OVAL EYES
# inside it. Nothing else in the palette looks like that — a dark TV screen is a
# solid black mass with no white holes, and a whiteboard is a white mass with no
# black surround. So: threshold the ink, erode away the 2px felt-pen outlines so
# only filled bodies survive, label the components, and accept one as a creature
# only when a small pale oval sits fully inside it.
#
# Pure PIL on purpose — this machine has no numpy, and the whole check runs in
# well under a second per image at quarter resolution.

def _label(mask, w, h, min_area):
    """Connected components of a bytearray mask. Returns [(area, x0, y0, x1, y1)]."""
    seen = bytearray(w * h)
    out = []
    for start in range(w * h):
        if not mask[start] or seen[start]:
            continue
        stack, area = [start], 0
        seen[start] = 1
        x0 = x1 = start % w
        y0 = y1 = start // w
        while stack:
            i = stack.pop()
            area += 1
            x, y = i % w, i // w
            if x < x0: x0 = x
            if x > x1: x1 = x
            if y < y0: y0 = y
            if y > y1: y1 = y
            if x > 0 and mask[i - 1] and not seen[i - 1]:
                seen[i - 1] = 1; stack.append(i - 1)
            if x < w - 1 and mask[i + 1] and not seen[i + 1]:
                seen[i + 1] = 1; stack.append(i + 1)
            if y > 0 and mask[i - w] and not seen[i - w]:
                seen[i - w] = 1; stack.append(i - w)
            if y < h - 1 and mask[i + w] and not seen[i + w]:
                seen[i + w] = 1; stack.append(i + w)
        if area >= min_area:
            out.append((area, x0, y0, x1, y1))
    return out


def find_people(path, debug=False):
    """Returns a list of bounding boxes that look like a crew member. Empty list
    means the room is unoccupied."""
    from PIL import Image, ImageFilter
    im = Image.open(path).convert("RGB")
    W = 384
    im = im.resize((W, int(im.height * W / im.width)), Image.LANCZOS)
    w, h = im.size
    px = im.load()

    ink = Image.new("L", (w, h))
    ip = ink.load()
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            ip[x, y] = 255 if (r < 80 and g < 80 and b < 80) else 0
    # erode: a felt-pen outline is ~1px at this scale and disappears; a filled
    # body (a creature is ~25x18 here) survives.
    ink = ink.filter(ImageFilter.MinFilter(3))
    solid = bytearray(ink.tobytes())
    solid = bytearray(1 if v else 0 for v in solid)

    pale = bytearray(w * h)
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            if r > 195 and g > 195 and b > 195:
                pale[y * w + x] = 1

    hits = []
    for area, x0, y0, x1, y1 in _label(solid, w, h, 55):
        bw, bh = x1 - x0 + 1, y1 - y0 + 1
        if bw < 6 or bh < 6:
            continue
        aspect = bw / float(bh)
        fill = area / float(bw * bh)
        if not (0.25 <= aspect <= 2.6) or fill < 0.42:
            continue          # thin ink furniture lines and L-shaped clutter
        # a blank white eye: a small pale island sitting fully inside the mass
        eyes = 0
        sub = bytearray(w * h)
        for y in range(max(y0 - 1, 0), min(y1 + 2, h)):
            for x in range(max(x0 - 1, 0), min(x1 + 2, w)):
                sub[y * w + x] = pale[y * w + x]
        for ea, ex0, ey0, ex1, ey1 in _label(sub, w, h, 4):
            if ea > area * 0.30:
                continue
            if ex0 <= x0 or ey0 <= y0 or ex1 >= x1 or ey1 >= y1:
                continue      # must be enclosed by the ink, not an edge overlap
            ew, eh = ex1 - ex0 + 1, ey1 - ey0 + 1
            if ew > bw * 0.6 or eh > bh * 0.6:
                continue
            if not (0.4 <= ew / float(eh) <= 2.5):
                continue
            eyes += 1
        if eyes >= 1:
            hits.append((x0, y0, x1, y1, area, eyes))
    if debug:
        print(os.path.basename(path), "->", len(hits), "occupant(s)", hits[:6])
    return hits


def entries():
    """Prompts come from the taxonomy, never from here. Pairs id with the
    manifest record so the index can carry the facets the DM resolves on."""
    out = subprocess.run([sys.executable, os.path.join(TOOLS, "backgrounds_taxonomy.py"), "--prompts"],
                         capture_output=True, text=True, check=True).stdout
    prompts = dict(line.split("\t", 1) for line in out.strip().split("\n"))
    manifest = json.load(open(MANIFEST))
    missing = [e["id"] for e in manifest if e["id"] not in prompts]
    assert not missing, "manifest ids with no taxonomy prompt: %s" % missing[:3]
    return [(e, prompts[e["id"]]) for e in manifest]


def _log(line):
    with _lock:
        with open(LOG, "a") as f:
            f.write("%s %s\n" % (time.strftime("%H:%M:%S"), line))


def _write_index(index):
    """Atomic: a crash mid-write must not leave a truncated index behind."""
    tmp = INDEX + ".tmp"
    with open(tmp, "w") as f:
        json.dump(index, f, indent=1, sort_keys=True)
    os.replace(tmp, INDEX)


def generate_one(entry, prompt, quality, tries=4, gate=True):
    """One room, generated and then MEASURED. Returns (id, seconds, attempts,
    rejected) or raises. A room that comes back with somebody in it is thrown
    away and re-rolled: the occupant would double against the composited cast,
    and a re-roll costs 28 seconds."""
    mid = entry["id"]
    path = os.path.join(BG, filename(mid))
    tmp = path[:-4] + ".part.png"          # still .png so _fetch runs its PNG checks
    t0 = time.time()
    last, rejected, netwait = None, 0, 0
    style = bg_style()
    attempt = 0
    while attempt < tries:
        attempt += 1
        try:
            r = _post_json(MIDDLEWARE,
                           {"prompt": prompt + " " + style, "quality": quality,
                            "size": "1536x1024", "output_format": "png"},
                           {"x-openai-api-key": _key("openai-key.txt")})
            url = r.get("imageUrl")
            if not url:
                raise IOError("no imageUrl in response: %s" % str(r)[:200])
            _fetch(url, tmp)               # signed urls expire — download immediately
            if gate:
                who = find_people(tmp)
                if who and attempt < tries:
                    rejected += 1
                    _log("OCCUPIED %s attempt=%d (%d found) — re-rolling" % (mid, attempt, len(who)))
                    continue
                if who:
                    _log("OCCUPIED %s GAVE UP after %d attempts — kept" % (mid, attempt))
            os.replace(tmp, path)
            return mid, time.time() - t0, attempt, rejected
        except Exception as e:
            last = "%s: %s" % (type(e).__name__, str(e)[:180])
            # A DNS/socket failure is the machine's problem, not this room's. A
            # network outage in this window killed a whole run by letting every
            # image burn its four attempts against a dead resolver in ninety
            # seconds. Transient network errors therefore WAIT and do not spend
            # the generation budget; only real generation failures do.
            blip = any(s in last for s in ("URLError", "nodename", "Errno 8", "Errno 54", "Errno 60",
                                           "timed out", "TimeoutError", "RemoteDisconnected",
                                           "IncompleteRead", "HTTP Error 5", "BadStatusLine",
                                           "ConnectionResetError", "Temporary failure"))
            if blip and netwait < 12:
                netwait += 1
                attempt -= 1                       # this one did not count
                _log("NETWAIT %d %s %s" % (netwait, mid, last[:90]))
                time.sleep(min(10 * netwait, 60))
                continue
            _log("RETRY %d/%d %s %s" % (attempt, tries, mid, last))
            if attempt < tries:
                time.sleep(4 * attempt)
    raise IOError("%s failed after %d attempts: %s" % (mid, tries, last))


def write_progress(done, failed, total, skipped, started, workers, quality, recent):
    pct = 100.0 * (done + skipped) / total if total else 0
    elapsed = time.time() - started
    rate = done / (elapsed / 60.0) if elapsed > 5 and done else 0
    left = (total - skipped - done) / rate if rate else 0
    with open(PROGRESS, "w") as f:
        f.write("# LANE-BATCH — background library\n\n")
        f.write("updated %s\n\n" % time.strftime("%Y-%m-%d %H:%M:%S"))
        f.write("- manifest total: %d\n" % total)
        f.write("- already on disk at start (skipped): %d\n" % skipped)
        f.write("- generated this run: %d\n" % done)
        f.write("- failed this run: %d\n" % len(failed))
        f.write("- complete: %d/%d (%.1f%%)\n" % (done + skipped, total, pct))
        f.write("- workers: %d, quality: %s\n" % (workers, quality))
        f.write("- rate: %.1f images/min, elapsed %.1f min, est. remaining %.1f min\n" % (rate, elapsed / 60.0, left))
        if recent:
            f.write("- mean latency of last %d: %.1fs\n" % (len(recent), sum(recent) / len(recent)))
        if failed:
            f.write("\n## failed\n")
            for mid, why in failed[-40:]:
                f.write("- %s — %s\n" % (mid, why))


def run_passes(workers, quality, limit, only, passes):
    """Drive run() until nothing is missing. Each pass skips what is already on
    disk, so an outage that kills a pass costs only its in-flight images. A run
    once ended forty images in because a DNS outage failed every remaining room
    in ninety seconds; one pass is not a run."""
    for p in range(1, passes + 1):
        rc = run(workers, quality, limit, only)
        left = [e["id"] for e, _ in entries()
                if (not only or only in e["id"]) and not intact(os.path.join(BG, filename(e["id"])))]
        print("pass %d/%d finished, %d still missing" % (p, passes, len(left)))
        _log("PASS %d/%d done, missing=%d" % (p, passes, len(left)))
        if not left:
            return 0
        if p < passes:
            time.sleep(30)                 # let a network blip clear before re-trying
    return 1


def run(workers, quality, limit, only):
    os.makedirs(LOG_DIR, exist_ok=True)
    os.makedirs(BG, exist_ok=True)
    all_entries = entries()
    if only:
        all_entries = [(e, p) for e, p in all_entries if only in e["id"]]
    total = len(all_entries)

    index = json.load(open(INDEX)) if os.path.exists(INDEX) else {}
    todo, skipped = [], 0
    for e, p in all_entries:
        path = os.path.join(BG, filename(e["id"]))
        if intact(path):
            index[e["id"]] = filename(e["id"])
            skipped += 1
        else:
            todo.append((e, p))
    _write_index(index)
    if limit:
        todo = todo[:limit]

    print("backgrounds: %d in manifest, %d already intact, %d to generate, %d workers, quality=%s"
          % (total, skipped, len(todo), workers, quality))
    _log("RUN start todo=%d workers=%d quality=%s" % (len(todo), workers, quality))
    started = time.time()
    done, failed, recent, rerolls = 0, [], [], 0
    write_progress(done, failed, total, skipped, started, workers, quality, recent)

    with ThreadPoolExecutor(max_workers=workers) as pool:
        futures = {pool.submit(generate_one, e, p, quality): e["id"] for e, p in todo}
        for fut in as_completed(futures):
            mid = futures[fut]
            try:
                mid, secs, attempts, rejected = fut.result()
                with _lock:
                    index[mid] = filename(mid)
                    _write_index(index)
                done += 1
                rerolls += rejected
                recent.append(secs / max(attempts, 1))
                del recent[:-24]
                _log("OK   %-72s %5.1fs attempt=%d rerolled=%d (%d/%d)" % (mid, secs, attempts, rejected, done, len(todo)))
            except Exception as e:
                failed.append((mid, str(e)[:200]))
                _log("FAIL %s %s" % (mid, str(e)[:200]))
            if (done + len(failed)) % 12 == 0:
                write_progress(done, failed, total, skipped, started, workers, quality, recent)

    write_progress(done, failed, total, skipped, started, workers, quality, recent)
    mins = (time.time() - started) / 60.0
    print("done: %d generated, %d failed, %d re-rolled for occupants, %.1f min (%.1f img/min)"
          % (done, len(failed), rerolls, mins, done / mins if mins else 0))
    _log("RUN end done=%d failed=%d rerolls=%d %.1fmin" % (done, len(failed), rerolls, mins))
    return 1 if failed else 0


def verify():
    """Truncation gate over assets/backgrounds — the same test scene_pipeline.py
    verify runs over assets/scenes, which does not walk this tree."""
    from PIL import Image
    bad, total = [], 0
    for f in sorted(os.listdir(BG)):
        if not f.lower().endswith(".png"):
            continue
        p = os.path.join(BG, f)
        total += 1
        data = open(p, "rb").read()
        why = ""
        if data[:8] != PNG_SIG:
            why = "not a PNG"
        elif data[-8:-4] != b"IEND":
            why = "truncated (no IEND)"
        else:
            try:
                Image.open(io.BytesIO(data)).load()
            except Exception as e:
                why = "corrupt: %s" % type(e).__name__
        if why:
            bad.append((f, len(data), why))
    print("verify backgrounds: %d PNGs scanned, %d intact, %d DAMAGED" % (total, total - len(bad), len(bad)))
    for f, n, why in bad:
        print("  DAMAGED %-64s %8d bytes  %s" % (f, n, why))
    return 1 if bad else 0


def scan():
    """Sweep every finished room for occupants. Prints the ids to re-roll."""
    manifest = json.load(open(MANIFEST))
    bad, total = [], 0
    for e in manifest:
        p = os.path.join(BG, filename(e["id"]))
        if not intact(p):
            continue
        total += 1
        who = find_people(p)
        if who:
            bad.append((e["id"], len(who)))
    print("scan: %d rooms checked, %d EMPTY, %d OCCUPIED" % (total, total - len(bad), len(bad)))
    for mid, n in bad:
        print("  OCCUPIED %s (%d)" % (mid, n))
    return 1 if bad else 0


def status():
    manifest = json.load(open(MANIFEST))
    ok = [e["id"] for e in manifest if intact(os.path.join(BG, filename(e["id"])))]
    print("backgrounds: %d/%d intact on disk" % (len(ok), len(manifest)))
    return 0 if len(ok) == len(manifest) else 1


def missing():
    manifest = json.load(open(MANIFEST))
    gone = [e["id"] for e in manifest if not intact(os.path.join(BG, filename(e["id"])))]
    for mid in gone:
        print(mid)
    return 0 if not gone else 1


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["run", "verify", "status", "missing", "scan"])
    ap.add_argument("--workers", type=int, default=12)
    ap.add_argument("--quality", default="low")
    ap.add_argument("--limit", type=int, default=0)
    ap.add_argument("--only", default="")
    ap.add_argument("--passes", type=int, default=8)
    a = ap.parse_args()
    if a.cmd == "run":
        sys.exit(run_passes(a.workers, a.quality, a.limit, a.only, a.passes))
    sys.exit({"verify": verify, "status": status, "missing": missing, "scan": scan}[a.cmd]())
