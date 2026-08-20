#!/usr/bin/env python3
"""RUNWAY! — the background library correctness audit.

WHAT THIS PROVES. The library is 516 pre-generated empty rooms. The DM never names
a file: it emits five facets and `SceneDirector.resolve()` walks a fixed drop ladder
until something matches. Three things can go wrong silently, and all three are
invisible at runtime because a wrong room still renders:

  1. a manifest entry points at a png that is not on disk    -> blank turn
  2. a png on disk is unreachable by any facet combination   -> money burnt
  3. resolve() answers a family/place it does not hold       -> BROKEN FICTION

(3) is the one that matters. `family` and `place` carry the story. A founder who has
retreated to a childhood bedroom must never be shown a coworking floor because the
lighting facet happened to match. A miss is the CORRECT answer there — the director
generates a new room. So `miss: true` is a pass, not a failure, and this tool asserts
it rather than tolerating it.

THIS IS A MIRROR, NOT THE ENGINE. `resolve()` below is a hand port of the GDScript in
src/llm/scene_director.gd. The two can drift; every place they could is listed in
MIRROR_DRIFT_RISKS at the bottom of this file, and `--check-mirror` re-reads the
GDScript and fails if the pieces this port depends on (DROP_ORDER, the ladder shape,
the comparison) no longer read the way they did when it was written.

  python3 tools/verify_library.py                 # the full audit, lean output
  python3 tools/verify_library.py --verbose       # every failing case, not a count
  python3 tools/verify_library.py --write-index   # (re)write assets/backgrounds/index.json
  python3 tools/verify_library.py --check-mirror  # only the drift check against the .gd

Exit code is 0 only when every check passes.
"""
import argparse
import difflib
import json
import os
import re
import sys
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
GAME = os.path.dirname(HERE)
BG = os.path.join(GAME, "assets", "backgrounds")
MANIFEST = os.path.join(BG, "manifest.json")
INDEX = os.path.join(BG, "index.json")
DIRECTOR = os.path.join(GAME, "src", "llm", "scene_director.gd")
ADJUDICATOR = os.path.join(GAME, "data", "prompts", "adjudicator.txt")

# The five facets the DM emits. main.gd builds exactly these keys and no others
# (see _begin_turn), which matters: resolve() compares EVERY key in `want`, so a
# sixth key nothing in the manifest carries would make every lookup a miss.
FACETS = ["family", "place", "time", "condition", "framing"]

# ---------------------------------------------------------------------------
# THE MIRROR. Ported line for line from scene_director.gd:64-82.
# ---------------------------------------------------------------------------

# scene_director.gd:38 — const DROP_ORDER := ["framing", "time", "condition"]
# Framing first because it matters least; the two that carry the story are
# deliberately absent, so the ladder can never trade a place away for a light.
DROP_ORDER = ["framing", "time", "condition"]


def resolve(entries, want):
    """Mirror of SceneDirector.resolve(). Returns {id, dropped, miss}.

    Faithfulness notes, each one a place a lazy port would diverge:
      * the erase is CUMULATIVE — `have` is mutated across ladder rungs, so rung 3
        is matching on family+place alone, not on family+place+framing+condition.
      * the scan is linear over the manifest IN FILE ORDER and FIRST MATCH WINS.
        Manifest order is therefore load-bearing; see the ambiguity check.
      * the comparison is String(e.get(k, "")) != String(have[k]) — a key the entry
        does not carry compares as "", it does not count as a wildcard.
      * an empty `have` matches the first entry vacuously. That is the engine's
        behaviour and the port keeps it; see FINDING in the report.
    """
    if not entries:
        return {"id": "", "dropped": "empty_library", "miss": True}
    have = dict(want)
    for drop in [""] + DROP_ORDER:
        if drop != "":
            have.pop(drop, None)
        for e in entries:
            ok = True
            for k in have:
                if str(e.get(k, "")) != str(have[k]):
                    ok = False
                    break
            if ok:
                return {"id": str(e.get("id", "")),
                        "dropped": (drop if drop != "" else "exact"),
                        "miss": False}
    return {"id": "", "dropped": "no_fit", "miss": True}


def file_for(entry_id):
    """Manifest ids are `family/place/time_condition_framing`; the pngs are flat,
    with `__` where the id has `/`. The engine's _url_for() does NOT do this
    substitution — that is finding #1."""
    return entry_id.replace("/", "__") + ".png"


def index_key(want):
    """The flat lookup key. Identical in shape to a manifest id, so an exact hit is
    a plain dict get on the id the DM's facets spell out."""
    return "%s/%s/%s_%s_%s" % (want["family"], want["place"], want["time"],
                               want["condition"], want["framing"])


# ---------------------------------------------------------------------------
# loading
# ---------------------------------------------------------------------------

def load_manifest():
    with open(MANIFEST) as f:
        d = json.load(f)
    if not isinstance(d, list):
        raise SystemExit("manifest.json is not an Array — the engine would load nothing")
    return d


def load_disk():
    return sorted(n for n in os.listdir(BG) if n.endswith(".png"))


def taxonomy_places():
    """family -> {place}. Read from the taxonomy module when it imports cleanly,
    otherwise derived from the manifest. The manifest is what the ENGINE loads, so
    it is the authority here; the taxonomy is cross-checked against it."""
    sys.path.insert(0, HERE)
    try:
        import backgrounds_taxonomy as tx
        return {fam: {p[0] for p in rows} for fam, rows in tx.PLACES.items()}
    except Exception:
        return None


# ---------------------------------------------------------------------------
# the checks
# ---------------------------------------------------------------------------

class Report:
    def __init__(self, verbose):
        self.verbose = verbose
        self.lines = []
        self.failures = []
        self.notes = []
        self.numbers = {}

    def n(self, key, value):
        self.numbers[key] = value

    def say(self, s=""):
        self.lines.append(s)

    def fail(self, title, items):
        self.failures.append((title, items))

    def detail(self, items, cap=None):
        cap = cap if cap is not None else (10 ** 9 if self.verbose else 12)
        for it in items[:cap]:
            print("      %s" % it)
        if len(items) > cap:
            print("      ... and %d more (--verbose for all)" % (len(items) - cap))

    def note(self, s):
        self.notes.append(s)


def check_holes(entries, disk, rep):
    """1. Every manifest entry resolves to a file that exists on disk."""
    have = set(disk)
    holes = [e["id"] for e in entries if file_for(e["id"]) not in have]
    rep.n("manifest_entries", len(entries))
    rep.n("holes", len(holes))
    if holes:
        rep.fail("manifest ids with no png on disk", holes)
    return holes


def check_url_for(entries, disk, rep):
    """1b. The engine's OWN path builder, not the corrected one. _url_for() returns
    res://assets/backgrounds/<id>.png with the id's slashes intact, and the pngs are
    flat with `__`. If this count is non-zero the runtime cannot open a single room
    even though every file is present."""
    have = set(disk)
    broken = [e["id"] for e in entries if (e["id"] + ".png") not in have]
    rep.n("engine_url_for_broken", len(broken))
    if broken:
        rep.fail("_url_for() paths that do not exist on disk (slash vs __)", broken)

    # SECOND HALF OF THE SAME PROBLEM. _url_for() prefers entry["url"] and only falls
    # back to a res:// path. compose() posts whatever it gets straight into the
    # remote edit's `images` array, and _cast_url in main.gd proves the shape that
    # call needs: it discards anything that does not begin with "http". So a library
    # room needs a hosted url, and a res:// path is not one.
    hosted = [e["id"] for e in entries if str(e.get("url", "")).startswith("http")]
    rep.n("entries_with_hosted_url", len(hosted))
    if len(hosted) != len(entries):
        rep.fail("manifest entries with no hosted url (compose() posts the path to a "
                 "remote model, which cannot read res://)",
                 ["%d of %d entries carry no `url`" % (len(entries) - len(hosted), len(entries))])
    return broken


def check_unreachable(entries, disk, rep):
    """2. Every png on disk is reachable from some facet combination.

    An id is reachable iff it is the FIRST entry carrying its 5-facet tuple. A drop
    only widens the match set, and the widened set's first match is at or before the
    exact set's first match — so no entry shadowed at the exact rung can ever be
    returned at a later one. Files with no manifest entry at all are unreachable by
    definition: resolve() only ever answers with an id it read from the manifest."""
    first_by_tuple = {}
    for e in entries:
        t = tuple(str(e.get(f, "")) for f in FACETS)
        first_by_tuple.setdefault(t, e["id"])
    reachable_files = {file_for(i) for i in first_by_tuple.values()}
    orphans = [n for n in disk if n not in {file_for(e["id"]) for e in entries}]
    shadowed = [n for n in disk if n not in reachable_files and n not in orphans]
    rep.n("png_on_disk", len(disk))
    rep.n("unreachable_orphan", len(orphans))
    rep.n("unreachable_shadowed", len(shadowed))
    if orphans:
        rep.fail("pngs on disk with no manifest entry (unreachable, generation wasted)", orphans)
    if shadowed:
        rep.fail("pngs shadowed by an earlier identical-facet entry (never returned)", shadowed)
    return orphans, shadowed


def check_exact(entries, rep):
    """3. Exact-match rate. Every manifest entry, queried with its own facets, must
    come back as itself with dropped == "exact"."""
    bad = []
    for e in entries:
        want = {f: str(e.get(f, "")) for f in FACETS}
        got = resolve(entries, want)
        if got["id"] != e["id"] or got["dropped"] != "exact" or got["miss"]:
            bad.append("%s -> id=%s dropped=%s miss=%s"
                       % (e["id"], got["id"] or "<none>", got["dropped"], got["miss"]))
    rep.n("exact_tested", len(entries))
    rep.n("exact_ok", len(entries) - len(bad))
    if bad:
        rep.fail("entries that do not resolve to themselves exactly", bad)
    return bad


def check_drops(entries, rep):
    """4. Drop behaviour. For every (family, place) the library holds, every one of
    the 3x3x2 = 18 time/condition/framing combinations the DM may freely emit must
    land on a real room IN THAT SAME PLACE — never a miss, never a neighbour's room.

    This is the whole promise of the ladder: a room at the wrong hour is still the
    right room."""
    times = sorted({str(e["time"]) for e in entries})
    conds = sorted({str(e["condition"]) for e in entries})
    frames = sorted({str(e["framing"]) for e in entries})
    pairs = sorted({(str(e["family"]), str(e["place"])) for e in entries})
    by_id = {e["id"]: e for e in entries}

    misses, wrong_place, ladder = [], [], Counter()
    for fam, place in pairs:
        for t in times:
            for c in conds:
                for fr in frames:
                    want = {"family": fam, "place": place, "time": t,
                            "condition": c, "framing": fr}
                    got = resolve(entries, want)
                    ladder[got["dropped"]] += 1
                    if got["miss"]:
                        misses.append("%s/%s %s_%s_%s -> MISS" % (fam, place, t, c, fr))
                        continue
                    e = by_id.get(got["id"], {})
                    if str(e.get("family")) != fam or str(e.get("place")) != place:
                        wrong_place.append("%s/%s %s_%s_%s -> %s  (WRONG PLACE)"
                                           % (fam, place, t, c, fr, got["id"]))
    total = len(pairs) * len(times) * len(conds) * len(frames)
    rep.n("drop_tested", total)
    rep.n("drop_pairs", len(pairs))
    rep.n("drop_miss", len(misses))
    rep.n("drop_wrong_place", len(wrong_place))
    rep.n("drop_ladder", dict(ladder))
    if misses:
        rep.fail("a place the library HOLDS that missed on some time/condition/framing", misses)
    if wrong_place:
        rep.fail("a drop that walked out of the requested family/place", wrong_place)
    return misses, wrong_place


# Family/place pairs a DM might plausibly emit that the library does not hold. Each
# must come back miss=true with an EMPTY id. Three shapes are covered on purpose:
# an invented place in a real family, a real place under the WRONG family (the
# adjudicator's own era table gets this wrong today), and a wholly invented family.
MISS_PROBES = [
    # invented place, real family — the ordinary novel situation
    ("body_mind", "dentist_office"),
    ("transit", "ferry_deck"),
    ("customer", "abattoir_floor"),
    ("institutional", "embassy_queue"),
    ("money", "sovereign_fund_office"),
    ("home_retreat", "sisters_spare_room"),
    ("social", "stag_do"),
    ("endings", "bankruptcy_hearing"),
    ("scrappy_workspace", "bike_shed"),
    ("legit_workspace", "satellite_office"),
    # a REAL place under the WRONG family — the dangerous one. If family were
    # droppable this would silently return the right room and hide the prompt bug.
    ("scrappy_workspace", "coworking_hotdesk"),
    ("home_retreat", "garage"),
    ("legit_workspace", "vc_office"),
    ("money", "hq_skyline"),
    ("body_mind", "childhood_bedroom"),
    # invented family entirely
    ("outdoors", "rooftop"),
    ("legal", "courtroom"),
    ("", "small_office"),
    # the near-miss typo class: one character off a real id
    ("home_retreat", "childhood_room"),
    ("legit_workspace", "small_offices"),
]


def check_miss(entries, rep):
    """5. Miss behaviour. The most important test in the file.

    A miss must be honest: miss=true AND id="". Returning any id here would put the
    founder in a room the story never sent them to, and the caller would take it —
    main.gd only checks `miss` to decide whether to generate."""
    bad = []
    for fam, place in MISS_PROBES:
        for t, c, fr in [("day", "steady", "wide"), ("small_hours", "in_the_red", "medium"),
                         ("night", "thriving", "wide")]:
            got = resolve(entries, {"family": fam, "place": place, "time": t,
                                    "condition": c, "framing": fr})
            if not got["miss"] or got["id"] != "":
                bad.append("%s/%s %s_%s_%s -> id=%s dropped=%s miss=%s  (SILENT WRONG ROOM)"
                           % (fam, place, t, c, fr, got["id"] or "<none>",
                              got["dropped"], got["miss"]))
    rep.n("miss_probes", len(MISS_PROBES) * 3)
    rep.n("miss_probes_wrong", len(bad))
    if bad:
        rep.fail("family/place NOT in the library that did not report a miss", bad)
    return bad


def check_degenerate(entries, rep):
    """5b. The degenerate wants. resolve() compares only the keys present in `want`,
    so a want with no keys matches the first entry vacuously and comes back
    miss=false, "exact". main.gd never builds one (it always fills all five, with
    place defaulting to the empty slug), but any other caller can, and the answer
    would be a confident wrong room rather than a miss."""
    notes = []
    got = resolve(entries, {})
    if not got["miss"]:
        notes.append("resolve({}) -> id=%s dropped=%s miss=false  (vacuous match, not a miss)"
                     % (got["id"], got["dropped"]))
    got = resolve(entries, {"family": "home_retreat", "place": ""})
    if not got["miss"]:
        notes.append("resolve(family only, empty place) -> id=%s miss=false" % got["id"])
    got = resolve(entries, {f: "" for f in FACETS})
    if not got["miss"]:
        notes.append("resolve(all facets empty) -> id=%s miss=false" % got["id"])
    # a sixth key nothing in the manifest carries makes every lookup a miss
    got = resolve(entries, dict({f: v for f, v in zip(FACETS, ["scrappy_workspace", "garage",
                                                              "day", "steady", "wide"])},
                                novel_place="a tower"))
    if got["miss"]:
        notes.append("resolve(exact facets + one extra key) -> MISS  "
                     "(any sixth facet the manifest lacks poisons every lookup)")
    rep.n("degenerate_notes", len(notes))
    return notes


def check_ambiguity(entries, rep):
    """6. Ambiguity. Two entries carrying the same 5 facets means the first wins
    arbitrarily and the second is dead weight. Ambiguity at the DROP rungs is by
    design (that is what a drop is), so it is counted, not failed."""
    by_tuple = defaultdict(list)
    for e in entries:
        by_tuple[tuple(str(e.get(f, "")) for f in FACETS)].append(e["id"])
    dupes = ["%s  <- %s" % (" ".join(t), ", ".join(ids))
             for t, ids in sorted(by_tuple.items()) if len(ids) > 1]
    dupe_ids = [i for i in (e["id"] for e in entries)]
    id_dupes = [i for i, c in Counter(dupe_ids).items() if c > 1]
    rep.n("exact_ambiguity", len(dupes))
    rep.n("duplicate_ids", len(id_dupes))
    if dupes:
        rep.fail("two entries share an identical facet set (first wins arbitrarily)", dupes)
    if id_dupes:
        rep.fail("the same id appears twice in the manifest", id_dupes)

    # informational: how wide is the arbitrary choice at each drop rung
    rungs = {}
    for depth, dropped in enumerate(DROP_ORDER, start=1):
        keys = FACETS[:]
        for d in DROP_ORDER[:depth]:
            keys.remove(d)
        groups = defaultdict(set)
        for e in entries:
            groups[tuple(str(e.get(f, "")) for f in keys)].add(e["id"])
        rungs["after dropping " + "+".join(DROP_ORDER[:depth])] = \
            max((len(v) for v in groups.values()), default=0)
    rep.n("drop_rung_max_candidates", rungs)
    return dupes, id_dupes


# ---------------------------------------------------------------------------
# the DM's view of the world
# ---------------------------------------------------------------------------

# Backticked snake_case tokens in the prompt that are NOT places: state fields,
# facet values, era names, item ids, flags. Anything backticked that is not here and
# not a real place id gets reported, so the list is the audit's own allowlist and
# every addition to it is a deliberate statement that the token is not a place.
NOT_A_PLACE = set("""
family place time condition framing novel_place era scene cast beat mood role doing
run_state player_move player_text interpreted_as narration reality_check said
recent_actions weeks_in_the_red runway_weeks cash weekly_burn morale hype product
traction valuation founder_pct pivots_so_far product_version business_model
funding_path founder_archetype company_name company_does employees customers
competences cofounders items flags commitment equity vesting build sell raise
recruit grit seed_value week effects verdict outcome title body id url generated
day night small_hours thriving steady in_the_red wide medium
garage coworking office floor hq
exit_taken _over
""".split())

FACET_VALUE_FIELDS = {"time": None, "condition": None, "framing": None}


def check_adjudicator(entries, rep):
    """Cross-check the DM's view of the world against the library.

    A place id the model is told it may pick, that the library does not hold, is a
    blank turn: the model emits it with novel_place empty (because it believes the
    place is on the list), resolve() misses, and _generate_background gets an empty
    description. `childhood_room` for `childhood_bedroom` was exactly this."""
    if not os.path.exists(ADJUDICATOR):
        rep.fail("adjudicator prompt missing", [ADJUDICATOR])
        return
    text = open(ADJUDICATOR).read()
    lines = text.split("\n")

    real = defaultdict(set)
    for e in entries:
        real[str(e["family"])].add(str(e["place"]))
    all_places = {p for ps in real.values() for p in ps}
    all_families = set(real)

    # -- the declared table -------------------------------------------------
    declared = {}
    for ln in lines:
        m = re.match(r"^\|\s*`(\w+)`\s*\|(.+)\|\s*$", ln)
        if m and m.group(1) in all_families | {"outdoors", "legal"}:
            declared[m.group(1)] = re.findall(r"`([a-z0-9_]+)`", m.group(2))
    table_unknown, table_missing, table_dupes = [], [], []
    seen_declared = set()
    for fam, places in declared.items():
        for p in places:
            if p in seen_declared:
                table_dupes.append("%s/%s listed twice in the table" % (fam, p))
            seen_declared.add(p)
            if p not in real.get(fam, set()):
                near = difflib.get_close_matches(p, sorted(all_places), n=1, cutoff=0.7)
                owner = next((f for f, ps in real.items() if p in ps), None)
                why = ("belongs to family `%s`, not `%s`" % (owner, fam) if owner else
                       ("no such place; nearest is `%s`" % near[0] if near else "no such place"))
                table_unknown.append("table says `%s`/`%s` — %s" % (fam, p, why))
    for fam, places in real.items():
        for p in sorted(places):
            if p not in declared.get(fam, []):
                table_missing.append("library holds %s/%s but the table never offers it" % (fam, p))
    for fam in sorted(all_families - set(declared)):
        table_missing.append("library family `%s` is absent from the table entirely" % fam)

    rep.n("prompt_table_places", sum(len(v) for v in declared.values()))
    rep.n("prompt_table_bad", len(table_unknown))
    rep.n("prompt_table_missing", len(table_missing))
    if table_unknown:
        rep.fail("BACKGROUND LIBRARY table entries that do not exist as declared", table_unknown)
    if table_missing:
        rep.fail("library rooms the prompt never offers the model", table_missing)
    if table_dupes:
        rep.fail("places listed under two families in the table", table_dupes)

    # -- the declared count -------------------------------------------------
    count_claims = []
    for ln in lines:
        m = re.search(r"(\d+)\s+places in\s+(\d+)\s+families", ln)
        if m:
            claimed_p, claimed_f = int(m.group(1)), int(m.group(2))
            if claimed_p != len(all_places) or claimed_f != len(all_families):
                count_claims.append("prompt claims %d places in %d families; library holds %d in %d"
                                    % (claimed_p, claimed_f, len(all_places), len(all_families)))
    if count_claims:
        rep.fail("the prompt's own headline count disagrees with the library", count_claims)

    # -- every backticked token anywhere in the prompt -----------------------
    # Recall over precision: everything snake_case-ish that is not a known field and
    # not a real place gets surfaced, with a near-miss hint when it looks like a typo.
    unknown = defaultdict(list)
    for i, ln in enumerate(lines, start=1):
        for tok in re.findall(r"`([a-z][a-z0-9_]{2,})`", ln):
            if tok in all_places or tok in all_families or tok in NOT_A_PLACE:
                continue
            unknown[tok].append(i)
    suspects = []
    for tok, where in sorted(unknown.items()):
        near = difflib.get_close_matches(tok, sorted(all_places), n=2, cutoff=0.72)
        if near:
            suspects.append("`%s` (lines %s) — not a place; nearest real ids: %s"
                            % (tok, ",".join(map(str, where[:4])), ", ".join("`%s`" % n for n in near)))
    rep.n("prompt_unknown_tokens", len(unknown))
    rep.n("prompt_typo_suspects", len(suspects))
    if suspects:
        rep.fail("backticked tokens that look like a mistyped place id", suspects)
    # The allowlist is the evidence, so the residue is printed rather than hidden:
    # every one of these has to be readable as "not a place" by a human.
    rep.note("backticked tokens that are neither a place, a family, nor an allowlisted "
             "field (%d, all must read as non-places): %s"
             % (len(unknown), ", ".join(sorted(unknown))))

    # -- family/place pairings stated in prose -------------------------------
    # The era table and the waterfall name a family and a place on the same line.
    # A pair the library does not hold is a guaranteed miss with an empty
    # novel_place: the model believes the place is on the list, so it does not fill
    # the description, and the generator gets nothing to draw.
    pair_bad = []
    for i, ln in enumerate(lines, start=1):
        if ln.strip().startswith("| `family`") or "place ids" in ln:
            continue
        fams = [f for f in all_families if "`%s`" % f in ln]
        places = [p for p in all_places if "`%s`" % p in ln]
        if not fams or not places:
            continue
        for p in places:
            owner = next(f for f, ps in real.items() if p in ps)
            if owner not in fams and len(fams) >= 1:
                pair_bad.append("line %d pairs family %s with `%s`, which lives in `%s`: %s"
                                % (i, "/".join("`%s`" % f for f in fams), p, owner,
                                   ln.strip()[:150]))
    # A table row that names a family AND a place in the same cell is stating a
    # pairing the model will copy verbatim. Two families in that cell is not a
    # choice, it is a coin flip, and one side of it misses.
    ambiguous = []
    for i, ln in enumerate(lines, start=1):
        if not ln.strip().startswith("|"):
            continue
        for cell in ln.strip().strip("|").split("|"):
            fams = [f for f in all_families if "`%s`" % f in cell]
            places = [p for p in all_places if "`%s`" % p in cell]
            if not places or len(fams) < 2:
                continue
            for p in places:
                owner = next(f for f, ps in real.items() if p in ps)
                ambiguous.append(
                    "line %d offers `%s` under %s in one cell; only `%s` holds it, so the "
                    "other reading is a miss: %s"
                    % (i, p, "/".join("`%s`" % f for f in fams), owner, cell.strip()[:110]))
    rep.n("prompt_pair_ambiguous", len(ambiguous))
    if ambiguous:
        rep.fail("table cells that pair one place with more than one family", ambiguous)

    # The worked examples are what the model imitates hardest, so their JSON facet
    # literals are checked as strictly as the table.
    json_bad = []
    for i, ln in enumerate(lines, start=1):
        m = re.search(r'"place"\s*:\s*"([^"]*)"', ln)
        if not m or m.group(1) == "":
            continue
        p = m.group(1)
        fam = None
        for j in range(max(0, i - 6), min(len(lines), i + 6)):
            fm = re.search(r'"family"\s*:\s*"([^"]*)"', lines[j])
            if fm:
                fam = fm.group(1)
                break
        if p not in all_places:
            json_bad.append("line %d: example emits place \"%s\", which is not in the library" % (i, p))
        elif fam is not None and p not in real.get(fam, set()):
            json_bad.append("line %d: example pairs family \"%s\" with place \"%s\" (which lives in `%s`)"
                            % (i, fam, p, next(f for f, ps in real.items() if p in ps)))
    for i, ln in enumerate(lines, start=1):
        for facet in ("time", "condition", "framing"):
            m = re.search(r'"%s"\s*:\s*"([^"]*)"' % facet, ln)
            if m and m.group(1) not in {str(e[facet]) for e in entries}:
                json_bad.append("line %d: example emits %s \"%s\", which no room carries"
                                % (i, facet, m.group(1)))
    rep.n("prompt_json_example_bad", len(json_bad))
    if json_bad:
        rep.fail("worked-example JSON that names something the library does not hold", json_bad)

    rep.n("prompt_pair_mismatch", len(pair_bad))
    if pair_bad:
        rep.fail("family/place pairs the prompt states that the library does not hold", pair_bad)

    # -- facet values --------------------------------------------------------
    val_bad = []
    for facet in ("time", "condition", "framing"):
        real_vals = {str(e[facet]) for e in entries}
        # the field's own bullet in "The other scene fields"
        for ln in lines:
            if ln.strip().startswith("- **`%s`**" % facet):
                for tok in re.findall(r"`([a-z_]+)`", ln):
                    if tok in (facet, "condition", "place") or tok in all_places:
                        continue
                    if tok not in real_vals and tok not in NOT_A_PLACE - real_vals:
                        val_bad.append("`%s` offered as a %s value; library has %s"
                                       % (tok, facet, sorted(real_vals)))
    rep.n("prompt_facet_value_bad", len(val_bad))
    if val_bad:
        rep.fail("facet values the prompt offers that the library does not carry", val_bad)


# ---------------------------------------------------------------------------
# the index the runtime reads
# ---------------------------------------------------------------------------

def build_index(entries):
    """Every facet combination the DM can emit for a place the library holds, mapped
    to the png the ladder would land on. Flat, one level, string -> string.

    WHY EVERY COMBINATION AND NOT JUST THE 516. The DM chooses time, condition and
    framing freely — 3x3x2 per place — so the id it spells out is very often not a
    file. Pre-resolving the ladder here means the engine does one dict lookup and
    never scans 516 entries or reimplements the drop order. And it makes a miss
    unambiguous: a key that is absent is a place the library does not hold, which is
    exactly the situation that must generate a new room.

    The keys are manifest-id shaped (`family/place/time_condition_framing`) so an
    exact hit is index[entry.id] and gen_backgrounds.py's id->filename contract is a
    strict subset of this file."""
    times = sorted({str(e["time"]) for e in entries})
    conds = sorted({str(e["condition"]) for e in entries})
    frames = sorted({str(e["framing"]) for e in entries})
    pairs = sorted({(str(e["family"]), str(e["place"])) for e in entries})
    out = {}
    for fam, place in pairs:
        for t in times:
            for c in conds:
                for fr in frames:
                    want = {"family": fam, "place": place, "time": t,
                            "condition": c, "framing": fr}
                    got = resolve(entries, want)
                    if not got["miss"]:
                        out[index_key(want)] = file_for(got["id"])
    return dict(sorted(out.items()))


def check_index(entries, rep, write):
    idx = build_index(entries)
    disk = set(load_disk())
    bad = [k for k, v in idx.items() if v not in disk]
    exact_ids = {e["id"] for e in entries}
    missing_exact = [i for i in sorted(exact_ids) if i not in idx]
    # the index must be as honest as resolve(): a key that IS present for a place the
    # library does not hold would be a silent wrong room baked into a data file.
    leaks = []
    for fam, place in MISS_PROBES:
        for t, c, fr in [("day", "steady", "wide"), ("small_hours", "in_the_red", "medium"),
                         ("night", "thriving", "wide")]:
            k = index_key({"family": fam, "place": place, "time": t,
                           "condition": c, "framing": fr})
            if k in idx:
                leaks.append("%s -> %s" % (k, idx[k]))
    # every png must be pointed at by at least one key, or it is unreachable
    unref = sorted(set(load_disk()) - set(idx.values()))
    rep.n("index_miss_probe_leaks", len(leaks))
    rep.n("index_files_referenced", len(set(idx.values())))
    rep.n("index_files_unreferenced", len(unref))
    if leaks:
        rep.fail("index keys for a family/place the library does not hold", leaks)
    if unref:
        rep.fail("pngs no index key ever points at", unref)
    rep.n("index_keys", len(idx))
    rep.n("index_broken_values", len(bad))
    rep.n("index_missing_exact_ids", len(missing_exact))
    if bad:
        rep.fail("index entries pointing at a png that is not on disk", bad)
    if missing_exact:
        rep.fail("manifest ids absent from the index", missing_exact)
    if write:
        tmp = INDEX + ".tmp"
        with open(tmp, "w") as f:
            json.dump(idx, f, indent=0, sort_keys=True)
            f.write("\n")
        os.replace(tmp, INDEX)
        rep.n("index_written_bytes", os.path.getsize(INDEX))
    elif os.path.exists(INDEX):
        try:
            on_disk = json.load(open(INDEX))
            rep.n("index_on_disk_keys", len(on_disk))
            stale = [k for k, v in idx.items() if on_disk.get(k) != v]
            rep.n("index_stale_keys", len(stale))
        except Exception as ex:
            rep.fail("index.json on disk is unreadable", [str(ex)])
    return idx


# ---------------------------------------------------------------------------
# mirror drift
# ---------------------------------------------------------------------------

MIRROR_DRIFT_RISKS = [
    "DROP_ORDER — this port hardcodes ['framing','time','condition']. Reorder it in "
    "the .gd and every drop-rung result here becomes fiction.",
    "The erase is cumulative in the .gd (`have` is mutated in the loop). If it ever "
    "becomes a fresh copy per rung, rung 3 would match on family+place+condition and "
    "some of the misses this tool asserts would start returning rooms.",
    "First-match-wins over manifest FILE ORDER. Reordering manifest.json changes "
    "which sibling a drop lands on without changing any count in this report.",
    "String(e.get(k,\"\")) treats a missing key as \"\", not as a wildcard. If that "
    "ever becomes a `has()` check, a want carrying a key the manifest lacks would "
    "start matching instead of missing.",
    "_url_for() builds the file path, and this tool tests BOTH the id->file mapping "
    "the library actually uses and the one _url_for() computes. They disagree today.",
    "resolve() with an empty or partial `want` matches vacuously. main.gd is the only "
    "caller that guards this, by always filling all five facets.",
]


def check_mirror(rep):
    """Re-read the GDScript and fail if the pieces this port depends on have moved."""
    if not os.path.exists(DIRECTOR):
        rep.fail("scene_director.gd not found — the mirror cannot be checked", [DIRECTOR])
        return
    src = open(DIRECTOR).read()
    problems = []
    m = re.search(r'const DROP_ORDER\s*:=\s*\[([^\]]*)\]', src)
    if not m:
        problems.append("DROP_ORDER not found in scene_director.gd")
    else:
        got = re.findall(r'"([a-z_]+)"', m.group(1))
        if got != DROP_ORDER:
            problems.append("DROP_ORDER is %s in the .gd but %s in this mirror" % (got, DROP_ORDER))
    for needle, why in [
        ('var order: Array = [""] + DROP_ORDER', "the ladder no longer starts with an exact rung"),
        ("have.erase(drop)", "the cumulative erase is gone"),
        ('String(e.get(k, "")) != String(have[k])', "the comparison changed"),
        ('"miss": true', "the miss contract changed"),
        ('res://assets/backgrounds/%s.png', "_url_for() changed shape"),
    ]:
        if needle not in src:
            problems.append("%s  (looked for: %s)" % (why, needle))
    rep.n("mirror_problems", len(problems))
    if problems:
        rep.fail("the mirror has drifted from scene_director.gd", problems)


# ---------------------------------------------------------------------------

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--verbose", action="store_true")
    ap.add_argument("--write-index", action="store_true")
    ap.add_argument("--check-mirror", action="store_true")
    args = ap.parse_args()

    rep = Report(args.verbose)
    check_mirror(rep)
    if args.check_mirror:
        emit(rep)
        return 1 if rep.failures else 0

    entries = load_manifest()
    disk = load_disk()

    check_holes(entries, disk, rep)
    check_url_for(entries, disk, rep)
    check_unreachable(entries, disk, rep)
    check_exact(entries, rep)
    check_drops(entries, rep)
    check_miss(entries, rep)
    notes = check_degenerate(entries, rep)
    check_ambiguity(entries, rep)
    check_index(entries, rep, args.write_index)
    check_adjudicator(entries, rep)

    # taxonomy vs manifest — the manifest is generated from it, so they must agree
    tx = taxonomy_places()
    if tx is not None:
        real = defaultdict(set)
        for e in entries:
            real[str(e["family"])].add(str(e["place"]))
        drift = []
        for fam in sorted(set(tx) | set(real)):
            only_tx = sorted(tx.get(fam, set()) - real.get(fam, set()))
            only_mf = sorted(real.get(fam, set()) - tx.get(fam, set()))
            if only_tx:
                drift.append("%s: in taxonomy, not in manifest: %s" % (fam, only_tx))
            if only_mf:
                drift.append("%s: in manifest, not in taxonomy: %s" % (fam, only_mf))
        rep.n("taxonomy_drift", len(drift))
        if drift:
            rep.fail("taxonomy and manifest disagree about which places exist", drift)

    for nt in notes:
        rep.note(nt)

    emit(rep)
    return 1 if rep.failures else 0


def emit(rep):
    print("=" * 72)
    print("RUNWAY! background library audit — a MIRROR of SceneDirector.resolve()")
    print("=" * 72)
    for k in sorted(rep.numbers):
        print("  %-28s %s" % (k, rep.numbers[k]))
    print()
    for line in rep.lines:
        print(line)
    if rep.notes:
        print("NOTES (latent hazards and evidence, not failures against today's caller):")
        for nt in rep.notes:
            print("   - %s" % nt)
        print()
    if not rep.failures:
        print("ALL CHECKS PASS")
        return
    print("FAILURES: %d" % len(rep.failures))
    for title, items in rep.failures:
        print("  [%d] %s" % (len(items), title))
        rep.detail(items)
    print()
    print("MIRROR DRIFT RISKS (this file is a port, not the engine):")
    for r in MIRROR_DRIFT_RISKS:
        print("  - %s" % r)


if __name__ == "__main__":
    sys.exit(main())
