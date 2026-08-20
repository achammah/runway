#!/usr/bin/env python3
"""BUILD THE POSE LIBRARY — 21 characters x 25 canonical poses, keyed and annotated.

    python3 tools/gen_poses.py canon   [--only a,b]              12 external canonicals
    python3 tools/gen_poses.py poses   [--only a,b] [--pose x,y] [--workers 10]
    python3 tools/gen_poses.py rekey   [--only a,b]              re-key + re-meta, no API
    python3 tools/gen_poses.py sheet   [--only a,b]              contact sheet per character
    python3 tools/gen_poses.py report                            what exists, what is missing

Outputs under game/assets/poses/<char>/:
    <pose>.png     keyed, de-fringed, cropped to content
    <pose>.json    {eyes, anchor, w, h, ...}      <- the only part that is committed
    _raw/<pose>.png   the untouched magenta render, so re-keying never costs a generation
    _canonical.png    externals only: the standing sprite every other pose references

THREE THINGS THIS FILE EXISTS TO SURVIVE
----------------------------------------
1. SCALE. ~537 generations at ~45s each. Serial is seven hours; a thread pool of ten is
   forty minutes. Everything chatty goes to a log file and progress lands in
   /tmp/lane_poses/progress.md, because a lane that streams hundreds of lines to its
   caller dies on the watchdog before its images do.

2. RESUME. Every unit checks for a valid finished output first and skips it. A run that
   is interrupted at sprite 300 costs 237 generations to finish, not 537. `rekey` goes
   further: the raw magenta render is kept, so a change to the keying or the metadata
   re-derives the whole library from disk for free.

3. THE DIFFERENCE BETWEEN A BAD IMAGE AND A BAD NETWORK. An earlier run in this project
   burned every retry it had in ninety seconds against a dead DNS name and reported the
   images as unfixable. So transient failures — connection, timeout, 5xx, rate limit, a
   truncated download, an endpoint-side job failure — retry with backoff and NEVER touch
   the per-sprite re-roll budget, which is spent only on an image that arrived intact and
   was judged wrong. A run-wide circuit breaker trips if transients stop being transient.

A REJECTED SPRITE IS NOT RE-ROLLED BLIND. The quality gate returns why it failed and
that sentence goes into the next prompt, because asking the same question the same way
gets the same answer.
"""
import json, os, random, sys, threading, time, urllib.error, urllib.request

TOOLS = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, TOOLS)
import scene_pipeline as sp          # _resolve_ref, _post_json, _fetch, _permanent_url, ATLAS, UA
import pose_meta as pm

GAME = pm.GAME
POSES_DIR = pm.POSES_DIR
LOGDIR = "/tmp/lane_poses"
REFS = f"{POSES_DIR}/_refs.json"
REPORT = f"{POSES_DIR}/_report.json"
MODEL = "bytedance/seedream-v5.0-pro/edit"
REROLLS = 2                      # per sprite, after the first attempt
# Within each anchor group, the poses a story reaches for most often come first.
POSE_RANK = {"stand_neutral": 0, "walk_stride": 1, "stand_phone": 2, "stand_armscrossed": 3,
             "stand_slumped": 4, "stand_present_pointer": 5, "stand_handshake_L": 6,
             "stand_handshake_R": 7, "stand_coffee": 8, "stand_reading_paper": 9,
             "stand_writing_clipboard": 10, "stand_point_accuse": 11, "stand_mic": 12,
             "stand_carrybox": 13, "stand_wave_celebrate": 14, "crouch_pack": 15,
             "sit_desk_typing": 20, "sit_desk_slumped": 21, "sit_couch_headinhands": 22,
             "sit_audience_neutral": 23, "sit_couch_relaxed": 24, "sit_audience_clapping": 25,
             "sit_bed": 26, "sleep_desk": 27, "lie_hospital": 28}
NET_TRIES = 8                    # transient retries, free
BREAKER = 30                     # consecutive transients across the pool before giving up

os.makedirs(LOGDIR, exist_ok=True)
_lock = threading.Lock()
_log_lock = threading.Lock()
_state = {"done": 0, "skipped": 0, "generated": 0, "rejected": 0, "failed": 0,
          "eyes_ok": 0, "eyes_missing": 0, "transients": 0, "streak": 0, "t0": time.time()}
_events = []


class Transient(Exception):
    """The network or the endpoint misbehaved. Free to retry; never costs a re-roll."""


def log(msg):
    with _log_lock:
        with open(f"{LOGDIR}/gen.log", "a") as f:
            f.write("%7.1f %s\n" % (time.time() - _state["t0"], msg))


def event(kind, char, pose, detail):
    with _lock:
        _events.append({"kind": kind, "char": char, "pose": pose, "detail": detail,
                        "t": round(time.time() - _state["t0"], 1)})
    log(f"{kind.upper():9s} {char}/{pose}: {detail}")


# ---------------------------------------------------------------------------
# refs — one shared JSON, merged on write
# ---------------------------------------------------------------------------
# scene_pipeline learned this the hard way: parallel workers that each read the whole
# dict, upload, then write the whole dict back LOST 18 of 27 entries and still reported
# success, because every worker saw its own write land. Merge, never replace.
def _refs_get(key):
    with _lock:
        return (json.load(open(REFS)) if os.path.exists(REFS) else {}).get(key)


def _refs_put(key, url):
    with _lock:
        have = json.load(open(REFS)) if os.path.exists(REFS) else {}
        have[key] = url
        with open(REFS, "w") as f:
            json.dump(have, f, indent=1, sort_keys=True)
    return url


def char_ref(char):
    """The identity anchor for one character: an approved cast scene for the nine, or
    this lane's own generated canonical for the twelve externals."""
    c = pm.CHARACTERS[char]
    if c["ref"]:
        return [sp._resolve_ref(c["ref"])]
    have = _refs_get(char)
    if have:
        return [have]
    path = f"{POSES_DIR}/{char}/_canonical.png"
    if not os.path.exists(path):
        raise RuntimeError(f"{char}: no canonical yet — run `gen_poses.py canon` first")
    url = sp._permanent_url(path)
    if not url:
        raise Transient(f"{char}: canonical upload returned no url")
    return [_refs_put(char, url)]


# ---------------------------------------------------------------------------
# generation
# ---------------------------------------------------------------------------
def _classify(e):
    """Anything that is about the pipe rather than the picture is transient."""
    if isinstance(e, Transient):
        return True
    if isinstance(e, urllib.error.HTTPError):
        return e.code in (408, 409, 425, 429, 500, 502, 503, 504)
    if isinstance(e, (urllib.error.URLError, TimeoutError, ConnectionError, OSError)):
        return True
    return False


def _submit(prompt, refs, size="1024*1024"):
    key = sp._key("atlas-key.txt")
    r = sp._post_json(f"{sp.ATLAS}/api/v1/model/generateImage",
                      {"model": MODEL, "prompt": prompt, "images": refs,
                       "size": size, "output_format": "png",
                       "thinking": "enabled", "prompt_optimization_mode": "standard",
                       "enable_base64_output": False},
                      {"Authorization": f"Bearer {key}"})
    jid = (r.get("data") or {}).get("id")
    if not jid:
        raise Transient(f"no job id in {json.dumps(r)[:160]}")
    for _ in range(90):                       # 90 * 4s = 6 min ceiling per job
        time.sleep(4)
        req = urllib.request.Request(f"{sp.ATLAS}/api/v1/model/prediction/{jid}",
                                     headers={"Authorization": f"Bearer {key}", **sp.UA})
        st = json.load(urllib.request.urlopen(req, timeout=45))["data"]
        if st["status"] in ("completed", "succeeded"):
            outs = st.get("outputs") or []
            if not outs:
                raise Transient("completed with no outputs")
            return outs[0]
        if st["status"] == "failed":
            raise Transient(f"job failed: {str(st.get('error'))[:120]}")
    raise Transient("job never completed")


def generate(prompt, refs, dst):
    """One image on disk, retrying the network for free. Raises after NET_TRIES."""
    last = None
    for attempt in range(1, NET_TRIES + 1):
        try:
            url = _submit(prompt, refs)
            sp._fetch(url, dst, tries=3)
            with _lock:
                _state["streak"] = 0
            return dst
        except Exception as e:
            if not _classify(e):
                raise
            last = f"{type(e).__name__}: {str(e)[:140]}"
            with _lock:
                _state["transients"] += 1
                _state["streak"] += 1
                tripped = _state["streak"] >= BREAKER
            log(f"  transient {attempt}/{NET_TRIES}: {last}")
            if tripped:
                raise SystemExit(f"CIRCUIT BREAKER: {BREAKER} consecutive transient failures "
                                 f"({last}) — the endpoint or the network is down, stopping "
                                 f"before the re-roll budgets are burned on it")
            if attempt < NET_TRIES:
                time.sleep(min(60, 4 * attempt) + random.uniform(0, 3))
    raise Transient(f"gave up after {NET_TRIES} network attempts: {last}")


# ---------------------------------------------------------------------------
# one sprite, end to end
# ---------------------------------------------------------------------------
def _intact(path):
    try:
        if os.path.getsize(path) < 2000:
            return False
        data = open(path, "rb").read()
        return data[:8] == b"\x89PNG\r\n\x1a\n" and data[-8:-4] == b"IEND"
    except OSError:
        return False


def sprite_done(char, pose_id):
    """Resume test. A finished sprite is a decodable PNG plus its metadata."""
    png, js = f"{POSES_DIR}/{char}/{pose_id}.png", f"{POSES_DIR}/{char}/{pose_id}.json"
    if not (os.path.exists(png) and os.path.exists(js)):
        return False
    try:
        data = open(png, "rb").read()
        if data[:8] != b"\x89PNG\r\n\x1a\n" or data[-8:-4] != b"IEND":
            return False
        json.load(open(js))
        return True
    except Exception:
        return False


def finish(char, pose, raw_path):
    """key -> crop -> gate -> eyes -> json. Returns (ok, reasons, meta_or_None)."""
    d = f"{POSES_DIR}/{char}"
    img = pm.key_sprite(raw_path, None, shadow=pose["shadow"])
    ok, st, reasons = pm.qc(img, pose)
    eyes, nblobs, npairs = pm.extract_eyes(img)
    pupils_ok, pupil = pm.pupil_check(img, eyes)
    if not pupils_ok:
        ok = False
        reasons.append(f"pupils or irises drawn in the eyes (dark centre {pupil})")
    # NOT a rejection on candidate count. An early version rejected any sprite whose
    # detector found more than one eye pair, and it threw away a perfectly good
    # lie_hospital because the sneakers filling the right of that frame are white patches
    # enclosed by ink. The ink-depth filter in extract_eyes now settles who is an eye;
    # the count is recorded for the report and nothing more.
    if not ok:
        return False, reasons, st
    img.save(f"{d}/{pose['id']}.png")
    meta = pm.write_meta(f"{d}/{pose['id']}.json", img, pose, eyes,
                         {"char": char, "eye_blobs": nblobs, "eye_candidates": npairs, "qc": st})
    return True, [], meta


def do_sprite(char, pose):
    pid = pose["id"]
    d = f"{POSES_DIR}/{char}"
    os.makedirs(f"{d}/_raw", exist_ok=True)
    if sprite_done(char, pid):
        with _lock:
            _state["skipped"] += 1; _state["done"] += 1
        return
    raw = f"{d}/_raw/{pid}.png"
    refs = char_ref(char)
    fix = ""
    for roll in range(REROLLS + 1):
        try:
            # A raw render already on disk from an interrupted run is re-used on the
            # FIRST pass only: it cost a generation and may key fine. A re-roll must
            # actually re-generate, or the same image would be judged twice. The raw is
            # checked for IEND, not just for size — a run killed mid-write leaves a
            # half-image that decodes as a normal file right up until it doesn't.
            if not (roll == 0 and _intact(raw)):
                generate(pm.pose_prompt(char, pid, fix), refs, raw)
                with _lock:
                    _state["generated"] += 1
            ok, reasons, meta = finish(char, pose, raw)
        except SystemExit:
            raise
        except Exception as e:
            if _classify(e):
                event("neterror", char, pid, f"{type(e).__name__}: {str(e)[:120]}")
                with _lock:
                    _state["failed"] += 1; _state["done"] += 1
                return
            event("error", char, pid, f"{type(e).__name__}: {str(e)[:160]}")
            ok, reasons, meta = False, [f"{type(e).__name__}: {str(e)[:120]}"], None
        if ok:
            with _lock:
                _state["done"] += 1
                if meta["eyes"]:
                    _state["eyes_ok"] += 1
                else:
                    _state["eyes_missing"] += 1
            if not meta["eyes"]:
                event("noeyes", char, pid, f"eye pair not found ({meta['eye_blobs']} blobs)")
            if roll:
                event("recovered", char, pid, f"accepted on re-roll {roll}")
            return
        fix = "; ".join(reasons)
        event("reject", char, pid, f"roll {roll}: {fix}")
        if roll == REROLLS:
            with _lock:
                _state["rejected"] += 1; _state["done"] += 1
            event("skipped", char, pid, f"out of re-rolls, last defect: {fix}")
            return


def do_canonical(char):
    d = f"{POSES_DIR}/{char}"
    os.makedirs(d, exist_ok=True)
    dst = f"{d}/_canonical.png"
    if os.path.exists(dst) and os.path.getsize(dst) > 2000 and _refs_get(char):
        with _lock:
            _state["skipped"] += 1; _state["done"] += 1
        return
    refs = [sp._resolve_ref(r) for r in pm.CANONICAL_REFS]
    stand = pm.POSE_BY_ID["stand_neutral"]
    fix = ""
    for roll in range(REROLLS + 1):
        try:
            generate(pm.canonical_prompt(char, fix), refs, dst)
            with _lock:
                _state["generated"] += 1
            img = pm.key_sprite(dst, None, shadow=True)
            ok, st, reasons = pm.qc(img, stand)
            eyes, nb, npairs = pm.extract_eyes(img)
            pok, pupil = pm.pupil_check(img, eyes)
            if not pok:
                ok = False; reasons.append(f"pupils drawn in the eyes ({pupil})")
            if not eyes:
                ok = False; reasons.append("no eye pair found — the character law is broken")
        except SystemExit:
            raise
        except Exception as e:
            if _classify(e):
                event("neterror", char, "_canonical", f"{type(e).__name__}: {str(e)[:120]}")
                with _lock:
                    _state["failed"] += 1; _state["done"] += 1
                return
            ok, reasons = False, [f"{type(e).__name__}: {str(e)[:120]}"]
        if ok:
            url = sp._permanent_url(dst)
            if not url:
                event("error", char, "_canonical", "asset upload returned no url")
                with _lock:
                    _state["failed"] += 1; _state["done"] += 1
                return
            _refs_put(char, url)
            with _lock:
                _state["done"] += 1
            event("canonical", char, "_canonical", f"accepted {img.size[0]}x{img.size[1]} (roll {roll})")
            return
        fix = "; ".join(reasons)
        event("reject", char, "_canonical", f"roll {roll}: {fix}")
    with _lock:
        _state["rejected"] += 1; _state["done"] += 1


# ---------------------------------------------------------------------------
# progress + report
# ---------------------------------------------------------------------------
def progress(total, phase):
    el = time.time() - _state["t0"]
    rate = _state["done"] / el if el else 0
    left = (total - _state["done"]) / rate if rate else 0
    rows = []
    for c in pm.CHARACTERS:
        n = sum(1 for p in pm.POSES if sprite_done(c, p["id"]))
        rows.append(f"| {c} | {n}/{len(pm.POSES)} |")
    body = [
        f"# LANE-POSES — {phase}", "",
        f"updated {time.strftime('%H:%M:%S')} · elapsed {el/60:.1f} min · eta {left/60:.1f} min", "",
        f"- done **{_state['done']}/{total}** (skipped-as-existing {_state['skipped']})",
        f"- generations spent {_state['generated']}",
        f"- rejected out of budget {_state['rejected']} · network write-offs {_state['failed']}",
        f"- eyes found {_state['eyes_ok']} · eyes missing {_state['eyes_missing']}",
        f"- transient retries {_state['transients']}", "",
        "| character | sprites |", "|---|---|", *rows, "",
        "## last 25 events", "",
    ]
    with _lock:
        tail = _events[-25:]
    body += [f"- `{e['kind']}` {e['char']}/{e['pose']} — {e['detail']}" for e in tail]
    with open(f"{LOGDIR}/progress.md", "w") as f:
        f.write("\n".join(body) + "\n")


def run_pool(units, total, phase, workers):
    """A hand-rolled pool rather than ThreadPoolExecutor.map, so progress can be written
    every 20 completions from whichever thread happens to get there."""
    import queue
    q = queue.Queue()
    for u in units:
        q.put(u)
    fatal = []

    def worker():
        while True:
            try:
                fn, args = q.get_nowait()
            except queue.Empty:
                return
            try:
                fn(*args)
            except SystemExit as e:
                fatal.append(str(e))
                with _lock:
                    while not q.empty():
                        try:
                            q.get_nowait()
                        except queue.Empty:
                            break
                return
            except Exception as e:
                who = args[1]["id"] if len(args) > 1 and isinstance(args[1], dict) else "_canonical"
                event("error", args[0], who, f"{type(e).__name__}: {str(e)[:160]}")
                with _lock:
                    _state["failed"] += 1; _state["done"] += 1
            finally:
                with _lock:
                    tick = _state["done"] % 20 == 0
                if tick:
                    progress(total, phase)

    ts = [threading.Thread(target=worker, daemon=True) for _ in range(workers)]
    for t in ts:
        t.start()
    for t in ts:
        t.join()
    progress(total, phase)
    if fatal:
        print("FATAL:", fatal[0])
        return 1
    return 0


def write_report():
    rep = {"characters": {}, "events": _events,
           "totals": {k: _state[k] for k in ("generated", "skipped", "rejected", "failed",
                                             "eyes_ok", "eyes_missing", "transients")}}
    for c in pm.CHARACTERS:
        have, missing, noeyes = [], [], []
        for p in pm.POSES:
            if sprite_done(c, p["id"]):
                have.append(p["id"])
                try:
                    if not json.load(open(f"{POSES_DIR}/{c}/{p['id']}.json"))["eyes"]:
                        noeyes.append(p["id"])
                except Exception:
                    noeyes.append(p["id"])
            else:
                missing.append(p["id"])
        rep["characters"][c] = {"accepted": len(have), "missing": missing, "eyes_missing": noeyes}
    with open(REPORT, "w") as f:
        json.dump(rep, f, indent=1, sort_keys=True)
    return rep


# ---------------------------------------------------------------------------
# contact sheet — 25 poses of one character on one page, for a human read
# ---------------------------------------------------------------------------
# The automated gate sees fringe, palette, eyes and shadow. It cannot see a drawn chair
# or a subtly wrong body. Twenty-one sheets can be looked at; 525 sprites cannot.
def sheet(char, cell=210):
    from PIL import Image, ImageDraw
    cols, rows = 5, (len(pm.POSES) + 4) // 5
    W, H = cols * cell, rows * (cell + 16)
    out = Image.new("RGB", (W, H), (242, 234, 211))
    dr = ImageDraw.Draw(out)
    for i, p in enumerate(pm.POSES):
        cx, cy = (i % cols) * cell, (i // cols) * (cell + 16)
        path = f"{POSES_DIR}/{char}/{p['id']}.png"
        dr.rectangle([cx, cy, cx + cell - 2, cy + cell + 14], outline=(190, 180, 160))
        if not os.path.exists(path):
            dr.text((cx + 6, cy + cell // 2), "MISSING", fill=(200, 60, 60))
            dr.text((cx + 4, cy + cell + 3), p["id"][:30], fill=(60, 60, 60))
            continue
        im = Image.open(path).convert("RGBA")
        sc = min((cell - 14) / im.width, (cell - 14) / im.height)
        im = im.resize((max(1, int(im.width * sc)), max(1, int(im.height * sc))), Image.LANCZOS)
        ox, oy = cx + (cell - im.width) // 2, cy + (cell - im.height) // 2
        out.paste(im, (ox, oy), im)
        # the stored eye coordinates, drawn back onto the thumbnail. This is what makes
        # the sheet worth reading twice: a coral ring that is not on an eye is a bad
        # extraction, and blinking at runtime draws ink at exactly these points.
        try:
            m = json.load(open(f"{POSES_DIR}/{char}/{p['id']}.json"))
            for ex, ey in m.get("eyes", []):
                px_, py_ = ox + ex * sc, oy + ey * sc
                dr.ellipse([px_ - 4, py_ - 4, px_ + 4, py_ + 4], outline=(232, 106, 92))
        except Exception:
            pass
        dr.text((cx + 4, cy + cell + 3), p["id"][:30], fill=(60, 60, 60))
    path = f"{LOGDIR}/sheet_{char}.png"
    out.save(path)
    return path


def main():
    args = sys.argv[1:]
    cmd = args[0] if args else "report"
    only = None
    poses = pm.POSES
    workers = 10
    for i, a in enumerate(args):
        if a == "--only":
            only = args[i + 1].split(",")
        elif a == "--pose":
            want = args[i + 1].split(",")
            poses = [p for p in pm.POSES if p["id"] in want]
        elif a == "--workers":
            workers = int(args[i + 1])
    chars = only or list(pm.CHARACTERS)

    if cmd == "canon":
        todo = [c for c in chars if c in pm.EXTERNALS]
        units = [(do_canonical, (c,)) for c in todo]
        print(f"canonicals: {len(units)} characters, {workers} workers")
        rc = run_pool(units, len(units), "canonicals", min(workers, len(units) or 1))
        write_report()
        print(f"canonicals done: generated {_state['generated']}, rejected {_state['rejected']}, "
              f"failed {_state['failed']}")
        return rc

    if cmd == "poses":
        # PRIORITY ORDER, and it is not cosmetic. Measured on the assembly pilot: a
        # standing pose ported into a scene reads near-identical to the model's own
        # composition of that room, while a seated pose depends on the chair it lands on.
        # So the feet-anchored poses are generated first, and POSE-MAJOR rather than
        # character-major, so that an interrupted run leaves a library that is complete
        # for every character across the poses it reached rather than complete for a few
        # characters and empty for the rest. A complete standing library beats a partial
        # everything.
        order = sorted(poses, key=lambda q: (q["anchor"] == "seat", POSE_RANK.get(q["id"], 99)))
        units = [(do_sprite, (c, p)) for p in order for c in chars]
        print(f"poses: {len(units)} sprites, {workers} workers")
        rc = run_pool(units, len(units), "poses", workers)
        write_report()
        print(f"poses done: generated {_state['generated']}, skipped {_state['skipped']}, "
              f"rejected {_state['rejected']}, failed {_state['failed']}, "
              f"eyes {_state['eyes_ok']}/{_state['eyes_ok']+_state['eyes_missing']}")
        return rc

    if cmd == "rekey":
        n = ok = 0
        for c in chars:
            for p in poses:
                raw = f"{POSES_DIR}/{c}/_raw/{p['id']}.png"
                if not os.path.exists(raw):
                    continue
                n += 1
                good, reasons, meta = finish(c, p, raw)
                if good:
                    ok += 1
                    if meta["eyes"]:
                        _state["eyes_ok"] += 1
                    else:
                        _state["eyes_missing"] += 1
                else:
                    event("reject", c, p["id"], "rekey: " + "; ".join(reasons))
        write_report()
        print(f"rekey: {ok}/{n} raws passed the gate, eyes "
              f"{_state['eyes_ok']}/{_state['eyes_ok']+_state['eyes_missing']}")
        return 0

    if cmd == "sheet":
        for c in chars:
            print(sheet(c))
        return 0

    rep = write_report()
    tot = sum(v["accepted"] for v in rep["characters"].values())
    print(f"{tot}/{len(pm.CHARACTERS)*len(pm.POSES)} sprites present")
    for c, v in sorted(rep["characters"].items()):
        print(f"  {c:22s} {v['accepted']:2d}/{len(pm.POSES)}  "
              f"eyes-missing {len(v['eyes_missing'])}" + (f"  missing {v['missing']}" if v["missing"] else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main())
