#!/usr/bin/env python3
"""Derive TYPED SLOTS for every blank scene -> assets/backgrounds/slots.json.

A SLOT says where a character can BE in a room and in what body position:

    {"id","pose_class","x","y","h","face","occ","prominence","confidence"}

  x,y   the ANCHOR: the seat point for sitting/lying, the foot point for standing.
  h     the character's STANDING height at that depth. Every pose sprite carries its
        own anchor and its own height ratio, so one number per slot is enough and it
        means the same thing for every pose class. (The architecture example is
        internally consistent with this reading: sit h=300 at y=585 and stand h=360
        at y=760 are the same figure at two depths of one linear depth model.)
  face  "left" | "right" | "any" -- which way the character looks.
  occ   always null. See below.
  confidence  "high" when a LIBRARY pose ports into this slot, "low" when it may not.

WHAT THIS FILE IS FOR, AFTER THE DERIVE PIPELINE
------------------------------------------------
Blank scenes are increasingly DERIVED from populated ones (architecture S7): the room
is generated WITH characters, they are measured, erased, and cut out as resident
poses. Every scene that has been through that has slots measured off real bodies.
These slots are the FALLBACK layer for every scene that has not -- which is all 516
of them today -- and the pilot measured exactly how far each kind can be trusted:

  * STAND slots port cleanly. A standing library pose dropped into a measured stand
    slot read near-identical to the model's own composition. These are the slots the
    runtime trusts everywhere, so they get the effort: every foot point is verified
    against the floor, walked onto it when it misses, and the floor band is swept for
    more of them than the five crew marks provide.
  * SEATED slots do not port -- a foot-anchored body breaks across chair geometries,
    which it did twice on the pilot. They are still emitted where seating is
    unambiguous, marked confidence "low" so the assembler can prefer dropping a
    character to mis-seating one.
  * OCCLUDERS are not authored at all. A resident cut carries a furniture-shaped hole
    and reproduces occlusion for free, so a rect here buys nothing -- while a wrong
    one silently deletes a character, the failure BACKGROUND_INVARIANTS.md measured
    at 4 crew marks in 15.

WHY EVERY NUMBER IS MEASURED AND NOT PROMPTED
---------------------------------------------
docs/BACKGROUND_INVARIANTS.md measured the camera across the library: the ground line
spans 0.449-0.648, 204px on a 1024 canvas. Nothing about the geometry may be assumed.

  * the ground line and the five crew marks come from annotations.json, already
    measured per room by auto_marks.py.
  * the DEPTH MODEL is a least-squares line h(y) through the room's own five marks,
    damped into the range the library actually lives in. It turns any floor y into the
    character height that belongs at that depth, and keeps sitting characters the same
    size as standing ones.
  * FURNITURE is found two ways, because one is not enough. Flat COLOUR REGIONS
    separate a chair from the desk it touches; a SKYLINE cut separates a sofa from the
    side table against it, whatever pattern the sofa is upholstered in.

THE AUTHORING RULES
-------------------
1. STAND ANCHORS go on visibly clear floor: the foot pad and the shins are tested,
   not the whole body column -- standing in front of furniture is normal and reads
   correctly once the character is drawn over the scene.
2. HEIGHT comes from the room's own depth, never a constant, with an office chair
   seat at 0.27h (45cm) and a desk surface at 0.45h (75cm) as the yardsticks that
   turn a piece of furniture into a floor depth.
3. PRECISION OVER RECALL. An under-slotted scene is harmless -- the assembler drops a
   character it cannot place. A wrong slot, feet in mid-air or a seat on a tabletop,
   is the failure that ships. A detector once stood five characters on a conveyor
   belt, so places whose furniture is a hazard (conveyors, treadmills, stacked chairs)
   are marked marks-only and no seat detector runs on them at all.

USAGE
    python3 tools/annotate_slots.py --probe <scene_id> [...]   # overlay PNGs
    python3 tools/annotate_slots.py --run [--shard i/n] [--part f] [--only substr]
    python3 tools/annotate_slots.py --merge part0.json part1.json ...
    python3 tools/annotate_slots.py --stats
    python3 tools/annotate_slots.py --scale-audit 120
"""
import json, math, os, sys, argparse
from collections import deque
from PIL import Image, ImageDraw, ImageFilter, ImageChops

GAME = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BG = f"{GAME}/assets/backgrounds"
ANNOTATIONS = f"{BG}/annotations.json"
SLOTS = f"{BG}/slots.json"

CW, CH = 1536, 1024          # every background in the library
SUB = 2                      # work at half resolution; the edge barrier is full-res
SW, SH = CW // SUB, CH // SUB

# --- the human yardsticks, as fractions of standing height -------------------
SEAT_F = 0.27                # 45cm office-chair seat ~ the knee
DESK_F = 0.45                # 75cm desk surface ~ mid-torso
COUCH_F = 0.23               # a couch cushion sits lower than an office chair
BED_F = 0.19                 # a mattress lower still
TORSO_F = 0.60               # an occluder may never rise above this off the floor

# ---------------------------------------------------------------------------
# PER-PLACE PROFILES: what a room of this kind actually contains.
#
# The library is 516 rooms but only 77 PLACES, and every variant of a place is the
# same room description regenerated -- the furniture KIND is constant even though
# every position, and the camera, is not. So the profile says WHAT to look for and
# the detector only has to find WHERE. That is what keeps precision high: no
# detector ever has to decide, unprompted, whether a long low mass is a couch or a
# conveyor belt.
#
#   seat    pose class for a compact chair-shaped mass, or None
#   soft    pose class for a long low soft mass (couch / bench / bed), or None
#   counter True if a long waist-high surface earns a lean_counter
#   board   True if a large write_surface earns a stand_present
#   Places absent from this table are marks-only by default.
# ---------------------------------------------------------------------------
MARKS_ONLY = dict(seat=None, soft=None, counter=False, board=True)
def P(seat=None, soft=None, counter=False, board=True):
    return dict(seat=seat, soft=soft, counter=counter, board=board)

PLACE_PROFILES = {
    # home_retreat ----------------------------------------------------------
    "parents_livingroom": P(seat="sit_couch", soft="sit_couch"),
    "childhood_bedroom":  P(seat="sit_desk",  soft="sit_bed"),
    "partner_flat":       P(seat="sit_desk"),
    "friends_couch":      P(seat="sit_couch", soft="sit_couch"),
    "own_flat_empty":     P(soft="sit_bed"),
    "car_backseat":       MARKS_ONLY,          # a car interior has no floor to speak of
    "sublet_room":        P(seat="sit_desk",   soft="sit_bed"),
    # scrappy_workspace -----------------------------------------------------
    "garage":             P(seat="sit_desk",  counter=True),
    "basement_office":    P(seat="sit_desk"),
    "old_hangar":         P(seat="sit_desk"),
    "storage_unit":       P(seat="sit_desk"),
    "back_of_shop":       P(seat="sit_desk"),
    "church_hall":        MARKS_ONLY,          # the chairs are STACKED, not sittable
    "university_lab":     P(seat="sit_desk",  counter=True),
    "shipping_container": P(seat="sit_desk"),
    "barn":               P(seat="sit_desk"),
    "attic":              P(seat="sit_desk"),
    "houseboat":          P(seat="sit_desk"),
    # legit_workspace -------------------------------------------------------
    "coworking_hotdesk":  P(seat="sit_desk"),
    "coworking_phonebooth": P(seat="sit_desk"),
    "small_office":       P(seat="sit_desk"),
    "open_floor":         P(seat="sit_desk"),
    "glass_boardroom":    P(seat="sit_desk"),
    "hq_atrium":          P(counter=True),
    "hq_skyline":         P(seat="sit_desk"),
    "server_corner":      MARKS_ONLY,
    # money -----------------------------------------------------------------
    "vc_office":          P(seat="sit_desk"),
    "vc_lobby":           P(seat="sit_audience", counter=True),
    "angel_kitchen":      P(seat="sit_desk"),
    "bank_branch":        P(counter=True),
    "pitch_stage":        P(seat="sit_audience"),
    "demo_day":           P(seat="sit_audience"),
    "family_office":      P(seat="sit_desk"),
    "penthouse_party":    P(soft="sit_couch", counter=True),
    "video_call_wall":    P(seat="sit_desk"),
    # customer --------------------------------------------------------------
    "trade_show_booth":   P(counter=True),
    "client_warehouse":   MARKS_ONLY,
    "retail_floor":       P(counter=True),
    "hospital_ward":      MARKS_ONLY,
    "factory_line":       MARKS_ONLY,          # THE CONVEYOR. never seat anyone here.
    "farm_yard":          MARKS_ONLY,
    "restaurant_kitchen": P(counter=True),
    "construction_site":  MARKS_ONLY,
    "school_classroom":   P(seat="sit_audience"),
    # institutional ---------------------------------------------------------
    "lawyer_office":      P(seat="sit_desk"),
    "accountant_office":  P(seat="sit_desk"),
    "courtroom":          P(seat="sit_audience", soft="sit_audience"),
    "patent_office":      P(counter=True),
    "immigration_office": P(seat="sit_audience"),
    "tax_office":         P(seat="sit_desk"),
    # transit ---------------------------------------------------------------
    "airport_gate":       P(seat="sit_audience", soft="sit_audience"),
    "plane_cabin":        MARKS_ONLY,          # no floor, seats are cut by the frame
    "train_carriage":     P(seat="sit_audience"),
    "rental_car":         MARKS_ONLY,
    "hotel_room":         P(seat="sit_desk",  soft="sit_bed"),
    "motel_room":         P(seat="sit_desk",  soft="sit_bed"),
    "conference_hotel_lobby": P(seat="sit_couch", soft="sit_couch", counter=True),
    # social ----------------------------------------------------------------
    "launch_party":       P(counter=True),
    "industry_mixer":     P(counter=True),
    "wedding_reception":  P(seat="sit_desk"),
    "funeral":            P(soft="sit_audience"),
    "family_dinner":      P(seat="sit_desk"),
    "school_reunion":     P(counter=True),
    # body_mind -------------------------------------------------------------
    "clinic_waiting":     P(seat="sit_audience", soft="sit_audience"),
    "therapist_office":   P(seat="sit_couch",  soft="sit_couch"),
    "hospital_bed":       P(seat="sit_desk",   soft="lie_hospital"),
    "gym_2am":            MARKS_ONLY,          # treadmills. the conveyor lesson again.
    "pharmacy":           P(counter=True),
    # endings ---------------------------------------------------------------
    "nasdaq_bell":        MARKS_ONLY,
    "signing_room":       P(seat="sit_desk"),
    "empty_office_cleared": MARKS_ONLY,
    "returned_laptops":   MARKS_ONLY,
    "liquidation_auction": MARKS_ONLY,         # STACKED chairs, lot stickers
    "press_conference":   P(seat="sit_audience"),
}

# HAND-AUTHORED SLOTS, added after the overlay review to the ones the detectors find.
#
# Only where a detector's miss is systematic and the place is small enough to fix by
# hand. A hospital bed is drawn as a pillow, a blanket, a side rail and a footboard at
# four different heights, so the skyline cut that finds every couch in the library
# shatters it -- and lie_hospital is the one pose class with nowhere else to go. Three
# rooms, measured off the image: the anchor is the hip on the mattress, floor_y the
# castors. h still comes from the room's own depth model, like every other slot.
HAND_AUTHORED = {
    "body_mind/hospital_bed/day_steady_wide": [
        dict(pose="lie_hospital", x=780, y=524, floor_y=720, face="right"),
    ],
    "body_mind/hospital_bed/night_in_the_red_wide": [
        dict(pose="lie_hospital", x=860, y=556, floor_y=780, face="right"),
    ],
    "body_mind/hospital_bed/night_thriving_wide": [
        dict(pose="lie_hospital", x=600, y=580, floor_y=800, face="right"),
    ],
}


# ===========================================================================
# geometry helpers
# ===========================================================================
def fit_depth(marks):
    """Least-squares h(y) through the room's own five crew marks.

    The marks carry a fixed arc of scales, so the LINE they define is the room's
    measured depth gradient: it is the only per-room statement of how big a person
    is at a given floor y, and everything else in this file is derived from it.
    """
    pts = [(m["foot_y"], m["h"]) for m in marks.values() if m.get("h")]
    n = len(pts)
    if n < 2:
        return (0.0, 300.0)
    sx = sum(p[0] for p in pts); sy = sum(p[1] for p in pts)
    sxx = sum(p[0] * p[0] for p in pts); sxy = sum(p[0] * p[1] for p in pts)
    den = n * sxx - sx * sx
    if abs(den) < 1e-6:
        return (0.0, sy / n)
    a = (n * sxy - sx * sy) / den
    b = (sy - a * sx) / n
    if a <= 0.02:                      # degenerate arc -> flat, use the mean
        return (0.0, sy / n)
    # DAMPING. The marks carry a FIXED arc of scales, so the slope this fit returns
    # is really a measure of how wide the mark spread is, not of the room's
    # perspective: across the library it runs 0.00 to 1.10 and the extremes come from
    # rooms whose floor band is shallow enough to squeeze the arc. An implausible
    # slope makes every ratio in this file misjudge, so pull it back to the range the
    # library actually lives in and re-anchor on the marks' own centre, which keeps
    # the marks themselves almost exactly where they were.
    ym, hm = sx / n, sy / n
    if a < 0.30 or a > 0.90:
        a = 0.30 if a < 0.30 else 0.90
        b = hm - a * ym
    return (a, b)


def h_at(depth, y):
    a, b = depth
    return max(120.0, min(760.0, a * y + b))


def floor_from_anchor(depth, y_anchor, frac):
    """A seat/cushion point sits `frac` of a standing height above the floor.

    Solving h = a*(y + frac*h) + b for h gives the depth at the anchor directly,
    instead of guessing the floor first and iterating.
    """
    a, b = depth
    den = 1.0 - frac * a
    h = (a * y_anchor + b) / den if abs(den) > 1e-3 else h_at(depth, y_anchor)
    h = max(120.0, min(760.0, h))
    return y_anchor + frac * h, h


def _geom(r):
    x0, y0, x1, y1 = r["box"]
    r["w"] = (x1 - x0 + 1) * SUB
    r["h"] = (y1 - y0 + 1) * SUB
    r["fill"] = r["n"] * SUB * SUB / float(r["w"] * r["h"])
    r["cx"] = (x0 + x1 + 1) * 0.5 * SUB
    r["top"] = y0 * SUB
    r["bot"] = (y1 + 1) * SUB
    r["left"] = x0 * SUB
    r["right"] = (x1 + 1) * SUB


# ===========================================================================
# image analysis
# ===========================================================================
class Scene:
    """Everything measured off one background, computed once."""

    def __init__(self, scene_id, entry):
        self.id = scene_id
        self.entry = entry
        self.marks = entry.get("marks") or {}
        self.meta = entry.get("meta") or {}
        self.surfaces = entry.get("write_surfaces") or {}
        self.depth = fit_depth(self.marks)
        self.ground = float(self.meta.get("ground_line") or 0.56) * CH

        im = Image.open(f"{BG}/{scene_id.replace('/', '__')}.png").convert("RGB")
        if im.size != (CW, CH):
            im = im.resize((CW, CH), Image.LANCZOS)
        self.im = im

        # --- the full-resolution edge barrier -----------------------------
        # A felt-pen outline is 2px wide. Averaged down to half resolution it can
        # vanish and let a chair leak into the floor -- the exact failure that cost
        # detect_surfaces.py 52 points of recall. So edges are found at FULL
        # resolution and MAX-pooled down, which cannot lose a thin line.
        lum = im.convert("L")
        edge = ImageChops.difference(lum, lum.filter(ImageFilter.MinFilter(3)))
        edge = ImageChops.lighter(edge, ImageChops.difference(lum.filter(ImageFilter.MaxFilter(3)), lum))
        edge = edge.point(lambda v: 255 if v > 26 else 0)
        edge = edge.filter(ImageFilter.MaxFilter(3)).resize((SW, SH), Image.NEAREST)
        self.barrier = edge.load()

        self.small = im.resize((SW, SH), Image.BOX)
        self.px = self.small.load()

        self._background_colours()
        self._segment()
        self._objects()

    # -- wall and floor ----------------------------------------------------
    def _background_colours(self):
        gy = int(self.ground / SUB)
        self.wall = self._modal(0, int(SH * 0.06), SW, max(int(SH * 0.10), gy - int(SH * 0.06)))
        self.floor = self._modal(0, min(SH - 2, gy + int(SH * 0.04)), SW, int(SH * 0.97))

    def _modal(self, x0, y0, x1, y1, step=2):
        px = self.px
        hist = {}
        for y in range(max(0, y0), min(SH, y1), step):
            for x in range(max(0, x0), min(SW, x1), step):
                r, g, b = px[x, y]
                if r + g + b < 150:
                    continue
                hist[(r >> 4, g >> 4, b >> 4)] = hist.get((r >> 4, g >> 4, b >> 4), 0) + 1
        if not hist:
            return (200, 200, 190)
        k = max(hist, key=hist.get)
        # refine: mean of everything inside that bucket
        n = sr = sg = sb = 0
        for y in range(max(0, y0), min(SH, y1), step):
            for x in range(max(0, x0), min(SW, x1), step):
                r, g, b = px[x, y]
                if (r >> 4, g >> 4, b >> 4) == k:
                    sr += r; sg += g; sb += b; n += 1
        return (sr // n, sg // n, sb // n) if n else (200, 200, 190)

    def is_bg(self, c, tol=26):
        for ref in (self.wall, self.floor):
            if abs(c[0] - ref[0]) <= tol and abs(c[1] - ref[1]) <= tol and abs(c[2] - ref[2]) <= tol:
                return True
        return False

    # -- flat colour regions ----------------------------------------------
    def _segment(self, tol=17, min_area=90):
        """Region-grow the half-res image, blocked by the full-res edge barrier.

        Cel-flat art means a chair seat, a desk top and a mattress are each ONE
        near-uniform region. Segmenting on colour rather than on a furniture/not
        mask is what stops a chair merging into the desk it is parked at -- the two
        touch, but they are never the same colour.
        """
        px, bar = self.px, self.barrier
        lab = [-1] * (SW * SH)
        regions = []
        for sy in range(SH):
            base = sy * SW
            for sx in range(SW):
                if lab[base + sx] >= 0 or bar[sx, sy]:
                    continue
                sc = px[sx, sy]
                rid = len(regions)
                lab[base + sx] = rid
                q = deque([(sx, sy)])
                n = 0; x0 = x1 = sx; y0 = y1 = sy; sr = sg = sb = 0
                while q:
                    x, y = q.popleft()
                    c = px[x, y]
                    n += 1; sr += c[0]; sg += c[1]; sb += c[2]
                    if x < x0: x0 = x
                    if x > x1: x1 = x
                    if y < y0: y0 = y
                    if y > y1: y1 = y
                    for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                        if nx < 0 or ny < 0 or nx >= SW or ny >= SH:
                            continue
                        i = ny * SW + nx
                        if lab[i] >= 0 or bar[nx, ny]:
                            continue
                        c2 = px[nx, ny]
                        if abs(c2[0] - sc[0]) <= tol and abs(c2[1] - sc[1]) <= tol and abs(c2[2] - sc[2]) <= tol:
                            lab[i] = rid
                            q.append((nx, ny))
                regions.append(dict(id=rid, n=n, box=(x0, y0, x1, y1),
                                    col=(sr // n, sg // n, sb // n)))
        self.labels = lab
        self.regions = [r for r in regions if r["n"] >= min_area]
        for r in self.regions:
            _geom(r)
            r["bg"] = self.is_bg(r["col"])
        self._cluster()

    def _cluster(self, cdist=40.0, gap=6):
        """Glue same-coloured touching regions into one OBJECT.

        This is the fix for the two ways cel art fragments furniture, and both of
        them produced wrong slots before it existed:

          a filing cabinet is drawn as four stacked DRAWER FACES. Each face's bottom
          edge looks exactly like the front lip of a seat, so a character was seated
          on a drawer. Glued, the cabinet's bottom is the floor and the chair test
          rejects it outright.

          a conference chair is drawn as four horizontal SLATS. No single slat is
          chair-shaped, so an entire boardroom of twelve chairs was invisible.
          Glued, the chair is one compact mass again.
        """
        regs = [r for r in self.regions if not r["bg"]]
        n = len(regs)
        par = list(range(n))

        def find(i):
            while par[i] != i:
                par[i] = par[par[i]]
                i = par[i]
            return i

        for i in range(n):
            a = regs[i]
            for j in range(i + 1, n):
                b = regs[j]
                if a["left"] - gap > b["right"] or b["left"] - gap > a["right"]:
                    continue
                if a["top"] - gap > b["bot"] or b["top"] - gap > a["bot"]:
                    continue
                d = a["col"]; e = b["col"]
                if (d[0] - e[0]) ** 2 + (d[1] - e[1]) ** 2 + (d[2] - e[2]) ** 2 > cdist * cdist:
                    continue
                ra, rb = find(i), find(j)
                if ra != rb:
                    par[ra] = rb

        groups = {}
        for i in range(n):
            groups.setdefault(find(i), []).append(regs[i])
        self.clusters = []
        for members in groups.values():
            x0 = min(m["box"][0] for m in members); y0 = min(m["box"][1] for m in members)
            x1 = max(m["box"][2] for m in members); y1 = max(m["box"][3] for m in members)
            tot = sum(m["n"] for m in members)
            sr = sum(m["col"][0] * m["n"] for m in members) // tot
            sg = sum(m["col"][1] * m["n"] for m in members) // tot
            sb = sum(m["col"][2] * m["n"] for m in members) // tot
            c = dict(id=len(self.clusters), n=tot, box=(x0, y0, x1, y1),
                     col=(sr, sg, sb), bg=False, parts=len(members))
            _geom(c)
            self.clusters.append(c)

    # -- whole furniture pieces, cut out of the skyline ---------------------
    def _objects(self, tol=24, jump=0.055, min_w=0.10):
        """Split the furniture silhouette into PIECES along its skyline.

        Flat-colour regions are the right unit for a CHAIR, whose upholstery has to
        be told apart from the desk it touches. They are the wrong unit for a FLORAL
        SOFA: the pattern shatters it into seven fragments, none couch-shaped, and the
        couch detector saw nothing at all. But one connected mass of "not wall, not
        floor" is no better -- in a furnished room everything touches, and the whole
        wall of furniture came back as a single blob 1536px wide.

        What separates a sofa from the side table against it is the SKYLINE. Read the
        topmost furniture pixel in every column and the room becomes a profile: the
        back of the sofa is a long flat run, the side table a lower one, the plant a
        spike. Cut the profile wherever it steps by more than a fraction of a body
        height and each piece is one item of furniture, whatever colour it is
        patterned in.
        """
        r, g, b = self.small.split()

        def dist(ch, v):
            return ImageChops.difference(ch, Image.new("L", (SW, SH), v))

        def to(col):
            d = ImageChops.lighter(dist(r, col[0]), dist(g, col[1]))
            return ImageChops.lighter(d, dist(b, col[2]))

        m = ImageChops.darker(to(self.wall), to(self.floor))
        m = m.point(lambda v: 255 if v > tol else 0)
        m = m.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.MinFilter(3))
        self.mask = m
        mp = m.load()

        # A skyline must be read from the FLOOR UP, never from the top down: a
        # whiteboard, a window and a framed print are all "not wall, not floor" too,
        # and reading downward makes every piece of furniture appear to reach the
        # picture rail. Walk up from the lowest furniture pixel in the column and stop
        # at the first real break, so only what actually stands on the floor is
        # measured, and the wall above it is left where it belongs.
        y_lo = max(0, int(self.ground / SUB) - int(0.40 * SH))
        y_hi = int(SH * 0.985)
        tops = [None] * SW
        bots = [None] * SW
        cols = [0] * SW
        for x in range(SW):
            bt = None
            for y in range(y_hi, y_lo - 1, -1):
                if mp[x, y]:
                    bt = y
                    break
            if bt is None:
                continue
            t = bt; gap = 0; n = 1
            y = bt - 1
            while y >= 0:
                if mp[x, y]:
                    t = y; gap = 0; n += 1
                else:
                    gap += 1
                    if gap > 3:
                        break
                y -= 1
            tops[x] = t; bots[x] = bt; cols[x] = n
        self.sky_bot = bots
        sm = list(tops)
        for x in range(2, SW - 2):
            w = [t for t in tops[x - 2:x + 3] if t is not None]
            sm[x] = sorted(w)[len(w) // 2] if len(w) >= 3 else tops[x]

        self.objects = self._cut(sm, tops, bots, cols, jump, min_w)
        # A second, much coarser cut. The fine one is right for a chair beside a desk;
        # a BED is a pillow, a blanket, a rail and a footboard whose skyline steps by
        # more than a fine cut tolerates, and it came back as five unusable slivers.
        self.slabs = self._cut(sm, tops, bots, cols, 0.30, 0.30)

    def floor_under(self, x, fallback):
        """The furniture's own floor contact in the column at x.

        A slab's global bottom belongs to whatever bit of it is nearest the camera --
        a crate shoved against the foot of a bed, say -- and taking the depth from
        there makes the character on the bed a head too big. Reading the contact in
        the anchor's own column keeps the depth honest.
        """
        i = max(0, min(SW - 1, int(x / SUB)))
        vals = sorted(b for b in self.sky_bot[max(0, i - 7):i + 8] if b is not None)
        if not vals:
            return float(fallback)
        return float(vals[len(vals) // 4] * SUB)

    def _cut(self, sm, tops, bots, cols, jump, min_w):
        out = []
        cur = None
        for x in range(SW):
            t = sm[x]
            if t is None or bots[x] is None:
                cur = None
                continue
            if cur is not None:
                href = h_at(self.depth, bots[x] * SUB)
                if abs(t - cur["last"]) * SUB > jump * href:
                    cur = None
            t = min(t, bots[x])          # a smoothed skyline may dip below its own base
            if cur is None:
                cur = dict(x0=x, x1=x, t=t, b=bots[x], last=t, n=cols[x])
                out.append(cur)
            else:
                cur["x1"] = x; cur["last"] = t
                cur["t"] = min(cur["t"], t); cur["b"] = max(cur["b"], bots[x])
                cur["n"] += cols[x]
        keep = []
        for o in out:
            o["box"] = (o["x0"], o["t"], o["x1"], o["b"])
            o["bg"] = False; o["parts"] = 1
            _geom(o)
            if o["w"] < min_w * h_at(self.depth, o["bot"]):
                continue
            o["col"] = self.patch(o["cx"], (o["top"] + o["bot"]) * 0.5) or (0, 0, 0)
            keep.append(o)
        return keep

    # -- is this patch of floor clear? -------------------------------------
    def band_clear(self, x, y, h, wfrac=0.20, need=0.72):
        """True when the standing band y-h .. y at x is mostly wall/floor colour.

        Authoring rule 2: a stand anchor goes on visibly clear floor and the whole
        body column has to miss the furniture, not just the feet.
        """
        half = max(4, int(h * wfrac / SUB))
        x0 = max(0, int(x / SUB) - half); x1 = min(SW - 1, int(x / SUB) + half)
        y0 = max(0, int((y - h) / SUB));  y1 = min(SH - 1, int(y / SUB))
        if x1 <= x0 or y1 <= y0:
            return False
        px = self.px
        tot = ok = 0
        for yy in range(y0, y1 + 1, 2):
            for xx in range(x0, x1 + 1, 2):
                tot += 1
                if self.is_bg(px[xx, yy], tol=30):
                    ok += 1
        return tot > 0 and ok / tot >= need

    def patch(self, x, y, k=2):
        """Median-ish colour of a small patch, robust to felt-pen outlines."""
        px = self.px
        xx = max(k, min(SW - k - 1, int(x / SUB)))
        yy = int(y / SUB)
        if yy < k or yy >= SH - k:
            return None
        vals = [px[xx + dx, yy + dy] for dy in range(-k, k + 1) for dx in range(-k, k + 1)]
        vals.sort(key=lambda c: c[0] + c[1] + c[2])
        return vals[len(vals) // 2]

    def is_floor(self, c, tol=32):
        f = self.floor
        return abs(c[0] - f[0]) <= tol and abs(c[1] - f[1]) <= tol and abs(c[2] - f[2]) <= tol

    def floor_row_frac(self, cx, y, half):
        px = self.px
        yy = int(y / SUB)
        if yy < 0 or yy >= SH:
            return 0.0
        x0 = max(0, int((cx - half) / SUB)); x1 = min(SW - 1, int((cx + half) / SUB))
        if x1 <= x0:
            return 0.0
        tot = ok = 0
        for xx in range(x0, x1 + 1):
            tot += 1
            if self.is_floor(px[xx, yy]):
                ok += 1
        return ok / tot

    def floor_contact(self, cx, y_from, h_ref, max_drop=0.55):
        """Where does the furniture above (cx, y_from) actually meet the floor?

        This is the measurement that separates a CHAIR from a CRATE. A chair's
        upholstery stops about a seat-height above its floor contact and you can see
        the floor between its legs; a crate's colour runs all the way down to the
        floor it stands on. Scanning for the floor makes that difference a number.

        Returns (y_floor, blocker). A blocker is a wide mass that hides the floor --
        i.e. a desk standing BETWEEN the camera and this chair, which is precisely
        the case that earns an occluder under authoring rule 1.
        """
        half = max(6.0, 0.055 * h_ref)
        y = y_from + 2
        limit = y_from + max_drop * h_ref
        run = 0
        while y < limit and y < CH - 2:
            if self.floor_row_frac(cx, y, half) >= 0.60:
                run += 1
                if run >= 3:
                    return (y - (run - 1) * SUB, None)
            else:
                run = 0
            y += SUB
        blocker = None
        for r in self.clusters:
            if r["w"] < 0.60 * h_ref:
                continue
            if not (r["left"] - 4 <= cx <= r["right"] + 4):
                continue
            if r["bot"] < y_from + 0.10 * h_ref or r["top"] > y_from + 0.28 * h_ref:
                continue
            if blocker is None or r["top"] < blocker["top"]:
                blocker = r
        return (None, blocker)


# ===========================================================================
# detectors
# ===========================================================================
def contact_run(sc, cx, y):
    """Width of the unbroken non-floor run through (cx, y)."""
    px, yy = sc.px, int(y / SUB)
    if yy < 0 or yy >= SH:
        return 0
    x = max(0, min(SW - 1, int(cx / SUB)))
    if sc.is_floor(px[x, yy]):
        return 0
    l = x
    while l > 0 and not sc.is_floor(px[l - 1, yy]):
        l -= 1
    r = x
    while r < SW - 1 and not sc.is_floor(px[r + 1, yy]):
        r += 1
    return (r - l + 1) * SUB


def rests_on_surface(sc, r, fy, h_ref):
    """Is this mass standing on a SURFACE that runs on well past it either side?

    A monitor, a printer, a plant pot and a stack of boxes are all chair-sized, all
    have a gap of visible floor somewhere below them, and all of them put a character
    sitting on a desk. What gives them away is the desk itself: the material directly
    under the object continues, unchanged, far beyond the object's own width on BOTH
    sides. Under a chair, one side at least is floor, or a cabinet, or nothing alike.

    Sampled in colour rather than from bounding boxes on purpose -- a box carries no
    depth, so the desk BEHIND a chair and the desk UNDER a monitor look identical.
    """
    off = max(r["w"] * 1.2, 0.30 * h_ref)
    drop = fy - r["bot"]
    for t in (0.15, 0.35, 0.55, 0.75):
        y = r["bot"] + t * drop
        mid = sc.patch(r["cx"], y)
        if mid is None or sc.is_floor(mid, 30):
            continue
        hits = 0
        for x in (r["cx"] - off, r["cx"] + off):
            c = sc.patch(x, y)
            if c is not None and abs(c[0] - mid[0]) <= 26 and \
               abs(c[1] - mid[1]) <= 26 and abs(c[2] - mid[2]) <= 26:
                hits += 1
        if hits >= 2:
            return True
    return False


def stands_on_floor(sc, r, fy, h_ref):
    """Does this mass meet the floor on its OWN footprint, or on a tabletop?

    This is what tells a chair from A MONITOR ON A DESK, and it has to be a pixel
    test rather than a bounding-box one, because boxes carry no depth: the desk
    BEHIND a chair and the desk UNDER a monitor overlap the candidate identically.

    Measured at the contact row, a chair's base is a run about as wide as the chair
    with floor on both sides of it. A monitor's "contact" is the front edge of the
    desk, and that run keeps going for the whole width of the desk. So the test is
    the width of the run, against the candidate's own width.
    """
    run = contact_run(sc, r["cx"], fy - max(6.0, 0.05 * h_ref))
    return run <= max(1.9 * r["w"], 1.15 * h_ref)


def detect_seats(sc, pose_class):
    """A chair: a compact upholstery region that stops a seat-height above the floor.

    Everything here is a ratio against the room's own depth model, never a pixel
    constant, because the camera is not holdable (docs/BACKGROUND_INVARIANTS.md).
    The decisive test is the GAP between the bottom of the coloured mass and the
    floor it stands on. A chair has one -- you see between its legs. A crate, a
    filing cabinet, a drawer pedestal and a plinth do not, and those are exactly what
    the first pass seated characters on.
    """
    # An ARMCHAIR is a different animal from an office chair: lower seat, wider body,
    # taller back, and a stub leg instead of a star base. Measured against the crew
    # marks' nominal scale it reads about 0.9 of a body wide, so the office-chair
    # window rejected both armchairs in every therapist's room in the library.
    soft_chair = pose_class == "sit_couch"
    seat_f = COUCH_F if soft_chair else SEAT_F
    w_hi, hh_hi, tall_f, gap_lo = (1.10, 0.95, 1.10, 0.06) if soft_chair \
        else (0.72, 0.66, 0.95, 0.11)
    out = []
    for r in sc.clusters:
        if r["bot"] > CH * 0.955:
            continue
        if sum(r["col"]) / 3.0 > 234:            # a blown-out pale quad is a board
            continue
        if r["fill"] < 0.32:      # a chair silhouette is an L, never a filled box
            continue
        h_ref = h_at(sc.depth, r["bot"] + seat_f * h_at(sc.depth, r["bot"]))
        if not (0.16 * h_ref <= r["w"] <= w_hi * h_ref):
            continue
        if not (0.11 * h_ref <= r["h"] <= hh_hi * h_ref):
            continue
        ar = r["w"] / float(r["h"])
        if not (0.40 <= ar <= 2.30):
            continue

        # No visible floor contact means no measured depth, and a GUESSED depth is
        # exactly how a character ends up floating. Decline the slot.
        fy, _ = sc.floor_contact(r["cx"], r["bot"], h_ref)
        if fy is None:
            continue

        # A slot's depth is where its furniture MEETS THE FLOOR, and no floor exists
        # above the room's own wall/floor junction. The seat point itself is often
        # well above that line -- a chair standing at the back wall has its seat
        # 45cm up -- so the test belongs on the contact, never on the mass.
        if fy < sc.ground - 6:
            continue
        h = h_at(sc.depth, fy)
        gap = fy - r["bot"]
        # THE chair test. An ARMCHAIR needs a lower floor than an office chair: its
        # upholstery runs down to a stub leg, so the gap under it is a third of an
        # office chair's, and the strict window rejected both armchairs in every
        # therapist's room in the library.
        if not (gap_lo * h <= gap <= 0.46 * h):
            continue
        if r["top"] > fy - (seat_f + 0.09) * h:  # no backrest above the seat
            continue
        if r["top"] < fy - tall_f * h:           # taller than a person: not a chair
            continue
        if not stands_on_floor(sc, r, fy, h_ref):
            continue
        if rests_on_surface(sc, r, fy, h_ref):
            continue
        out.append(dict(kind="seat", pose=pose_class, x=r["cx"],
                        y=fy - seat_f * h, h=h, floor_y=fy, reg=r, tucked=None,
                        seat_px=gap))          # measured seat height, for the scale audit
    return out


def detect_soft(sc, pose_class):
    """A long low mass standing on the floor: couch, pew, bench, bed, mattress.

    The SHAPE tests here are all relative to the mass itself, never to the character
    height, and that is deliberate. The crew marks carry a fixed nominal scale, so in
    a close-framed room -- a hotel room, a bedroom -- the furniture is drawn much
    larger than the mark scale implies, and every gate written as a fraction of h
    rejected the bed for being "too tall to be a bed". A couch is recognisable by its
    own proportions: long, low, and solid. Only the character height, which has to
    agree with the stand marks, is read from the depth model.
    """
    bed = pose_class in ("sit_bed", "lie_hospital")
    cush = 0.30 if bed else 0.55        # how far down the mass the sitting surface is
    out = []
    for r in sc.slabs:
        if r["bot"] < sc.ground - 6 or r["bot"] > CH * 0.99:
            continue
        if r["fill"] < 0.45:             # a couch is a solid block, not a frame
            continue
        if r["w"] < 0.11 * CW or r["h"] < 0.07 * CH:
            continue
        ar = r["w"] / float(r["h"])
        if not (1.05 <= ar <= 5.0):      # long and low: that IS the shape
            continue
        y = r["top"] + cush * r["h"]
        fy = sc.floor_under(r["cx"], r["bot"])
        if fy <= y + 8:
            continue
        h = h_at(sc.depth, fy)
        out.append(dict(kind="soft", pose=pose_class, x=r["cx"], y=y,
                        h=h, floor_y=fy, reg=r, tucked=None,
                        area=r["fill"] * r["w"] * r["h"]))
    return out


def detect_counter(sc):
    """A long waist-high surface to lean on: bar, till, pass, reception desk.

    The character stands on the NEAR side, so the counter is its own occluder and the
    foot point goes a step in front of it -- never on top of it, which is the tabletop
    failure wearing a different hat. No fill test: a counter is a top on legs and its
    bounding box is mostly air.
    """
    out = []
    for r in sc.slabs:
        if r["w"] < 0.22 * CW:
            continue
        if r["w"] / float(r["h"]) < 1.6:
            continue
        if r["top"] < sc.ground - 0.30 * CH or r["top"] > sc.ground + 0.10 * CH:
            continue
        fy, h = floor_from_anchor(sc.depth, r["top"], DESK_F)
        if fy < sc.ground + 0.02 * CH or fy > CH * 0.96:
            continue
        if sc.floor_row_frac(r["cx"], fy, 0.13 * h) < 0.55:
            continue                     # the feet must be on visible floor
        out.append(dict(kind="counter", pose="lean_counter", x=r["cx"], y=fy, h=h,
                        floor_y=fy, reg=r, tucked=None, surface_y=r["top"],
                        area=r["w"] * r["h"]))
    return out


def desk_in_front(sc, seat):
    """Rule 1. The FRONT PANEL of a desk standing between the camera and this seat,
    or None when the desk is behind the chair -- which is the common case.

    "In front" is decided by screen y, not by proximity: a surface that reads LOWER
    on screen than the seat point is nearer the camera. When the desk surface reads
    HIGHER, the chair is parked in front of its desk with its back to us and there is
    nothing at all to redraw. Cutting an occluder anyway is what made the pilot's
    characters disappear, and cutting the whole desk mass rather than its front panel
    is the same bug one step later.
    """
    h = seat["h"]
    sx, sy = seat["x"], seat["y"]
    best = None
    if True:
        for r in sc.clusters + sc.objects:
            if r is seat["reg"]:
                continue
            if r["right"] < sx - 0.5 * h or r["left"] > sx + 0.5 * h:
                continue
            if r["w"] < 0.80 * h:                   # a desk is wide
                continue
            if r["top"] <= sy + 0.03 * h:           # surface above the seat -> BEHIND
                continue
            surface = (seat["floor_y"] - r["top"]) / h
            if not (0.28 <= surface <= 0.66):       # not at desk height: not a desk
                continue
            if best is None or r["top"] < best["top"]:
                best = r
    if best is None:
        return None
    # Safety clamp: an occluder may never rise above mid-torso, whatever was found,
    # so it can only ever cover a seated character from the waist down.
    top = max(best["top"], seat["floor_y"] - TORSO_F * h)
    bot = min(CH, max(best["bot"], top + 0.10 * h))
    x0 = max(0.0, sx - 0.42 * h, best["left"] - 2.0)
    x1 = min(float(CW), sx + 0.42 * h, best["right"] + 2.0)
    if x1 - x0 < 0.25 * h or top >= bot - 6:
        return None
    return [int(x0), int(top), int(x1), int(bot)]


def detect_board_stand(sc):
    """One stand_present beside the room's biggest blank board.

    The board is on a wall, so the floor beneath it is the wall/floor junction and the
    presenter's feet go a step forward of the ground line -- never at the board's own
    y, which would post them up the wall.

    Standing in FRONT of furniture is normal and reads correctly once the character is
    composited over the scene, so the test is on the foot pad and the shins, not on
    the whole body column: requiring a clear column rejected nine rooms in ten, all of
    them for the crime of having a sideboard behind the speaker.
    """
    best = None
    for name, s in sc.surfaces.items():
        w, hh = s.get("w", 0), s.get("h", 0)
        if w < 0.075 * CW or hh < 0.05 * CH:
            continue
        if s.get("y", 0) + hh > sc.ground + 0.12 * CH:      # not a wall board
            continue
        area = w * hh
        if best is None or area > best[0]:
            best = (area, name, s)
    if best is None:
        return None
    _, name, s = best
    bx0, bx1 = s["x"], s["x"] + s["w"]
    for depth in (0.10, 0.20, 0.32):
        fy = sc.ground + depth * (CH - sc.ground)
        if fy > CH * 0.94:
            continue
        h = h_at(sc.depth, fy)
        for step in (0.36, 0.55, 0.80, 1.05):
            for side in ("right", "left"):
                x = (bx1 + step * h) if side == "right" else (bx0 - step * h)
                if x < 0.05 * CW or x > 0.95 * CW:
                    continue
                if sc.floor_row_frac(x, fy, 0.14 * h) < 0.60:
                    continue
                if sc.floor_row_frac(x, fy - 0.07 * h, 0.10 * h) < 0.35:
                    continue
                return dict(kind="board", pose="stand_present", x=x, y=fy, h=h,
                            floor_y=fy, face=("left" if side == "right" else "right"),
                            board=name, tucked=None)
    return None


# ===========================================================================
# assembling one scene
# ===========================================================================
BOTTOM_CALM = 0.874          # the UI safe zone auto_marks.py already respects


def foot_ok(sc, x, y, h, strict=0.75):
    """Is (x, y) a foot point on visibly clear floor, with clear shins above it?

    THE test for a stand slot, and the one the runtime leans on hardest: the derive
    pipeline measured that standing library poses port cleanly into a measured stand
    slot, so a stand slot is worth getting exactly right. Standing IN FRONT of
    furniture is fine and reads correctly once the character is drawn over the scene,
    which is why this looks at the floor the feet are on and the shins just above,
    and not at the whole body column -- a full-column test throws away good marks for
    the crime of having a sideboard behind the speaker.
    """
    if y < sc.ground or y > CH * BOTTOM_CALM + 0.06 * CH:
        return False
    if sc.floor_row_frac(x, y, 0.15 * h) < strict:
        return False
    if sc.floor_row_frac(x, y - 0.06 * h, 0.11 * h) < 0.40:
        return False
    return True


def nudge_to_floor(sc, x, y, h):
    """Walk a foot point onto clear floor: a small step is better than a lost slot."""
    if foot_ok(sc, x, y, h, 0.60):
        return (x, y, h)
    for dy in (0, 0.03, -0.03, 0.06, -0.06, 0.10):
        yy = y + dy * CH
        hh = h_at(sc.depth, yy)
        for dx in (0, 0.04, -0.04, 0.08, -0.08, 0.13, -0.13):
            xx = x + dx * CW
            if xx < 0.04 * CW or xx > 0.96 * CW:
                continue
            if foot_ok(sc, xx, yy, hh, 0.70):
                return (xx, yy, hh)
    return None


def prospect_floor(sc, taken, want=3):
    """Extra stand slots on measured open floor, beyond the five crew marks.

    The five marks are laid in one shallow arc by auto_marks.py, so a room with a
    deep clear floor is under-served by them. Since stand slots are what the runtime
    trusts everywhere, sweep the floor band for more of them at two depths, and keep
    only the ones far enough from what is already there to hold a separate body.
    """
    out = []
    lo = sc.ground + 0.10 * (CH * BOTTOM_CALM - sc.ground)
    hi = CH * BOTTOM_CALM
    if hi <= lo:
        return out
    for f in (0.34, 0.70, 0.05):
        y = lo + f * (hi - lo)
        h = h_at(sc.depth, y)
        step = 0.045 * CW
        x = 0.07 * CW
        while x < 0.93 * CW:
            if foot_ok(sc, x, y, h, 0.80):
                near = any(abs(x - t[0]) < 0.42 * min(h, t[2]) and
                           abs(y - t[1]) < 0.45 * min(h, t[2]) for t in taken)
                if not near:
                    out.append(dict(kind="floor", pose="stand", x=x, y=y, h=h,
                                    floor_y=y, occ=None, face=facing(x)))
                    taken.append((x, y, h))
                    if len(out) >= want:
                        return out
                    x += 0.30 * h
                    continue
            x += step
    return out


def facing(x, ref=CW * 0.5):
    return "right" if x < ref - CW * 0.06 else ("left" if x > ref + CW * 0.06 else "any")


def build_slots(sc, place):
    prof = PLACE_PROFILES.get(place, MARKS_ONLY)
    cands = []

    for hand in HAND_AUTHORED.get(sc.id, []):
        cands.append(dict(kind="soft", pose=hand["pose"], x=float(hand["x"]),
                          y=float(hand["y"]), floor_y=float(hand["floor_y"]),
                          h=h_at(sc.depth, hand["floor_y"]), face=hand.get("face"),
                          reg=None, tucked=None, area=10 ** 9, hand=True))

    if prof["seat"]:
        cands += detect_seats(sc, prof["seat"])
    if prof["soft"]:
        cands += detect_soft(sc, prof["soft"])
    if prof["counter"]:
        cands += detect_counter(sc)

    # keep the strongest few; a room does not need eight of anything
    seats = sorted((c for c in cands if c["kind"] == "seat"), key=lambda c: -c["h"])
    soft = sorted((c for c in cands if c["kind"] == "soft"), key=lambda c: -c["area"])
    # A second couch only when it is genuinely another couch. In a living room the
    # runner-up is the TV unit, and it is half the size -- so make it earn its place.
    if len(soft) > 1:
        soft = [soft[0]] + [c for c in soft[1:] if c["area"] >= 0.60 * soft[0]["area"]]
    cnt = sorted((c for c in cands if c["kind"] == "counter"), key=lambda c: -c["area"])
    cands = seats[:6] + soft[:2] + cnt[:1]

    slots = []
    for c in cands:
        occ = None
        if c["kind"] == "seat":
            occ = desk_in_front(sc, c)
        elif c["kind"] == "counter":
            r = c["reg"]
            top = max(r["top"], c["floor_y"] - TORSO_F * c["h"])
            occ = [int(max(0, c["x"] - 0.42 * c["h"])), int(top),
                   int(min(CW, c["x"] + 0.42 * c["h"])), int(min(CH, r["bot"]))]
            if occ[2] - occ[0] < 0.25 * c["h"] or occ[1] >= occ[3] - 6:
                occ = None
        c["occ"] = occ
        c["face"] = c.get("face") or facing(c["x"])
        slots.append(c)

    b = detect_board_stand(sc) if prof["board"] else None
    if b:
        b["occ"] = None
        slots.append(b)

    # The five measured crew marks, as stand slots. These are the slots the runtime
    # trusts most, so each foot point is verified against the floor and walked onto
    # it when it misses, rather than shipped on trust or thrown away.
    order = ["founder_mark", "crew_1", "crew_2", "crew_3", "crew_4"]
    marks = []
    for name in order + [k for k in sc.marks if k not in order]:
        m = sc.marks.get(name)
        if not m:
            continue
        x, y, h = float(m["foot_x"]), float(m["foot_y"]), float(m["h"])
        moved = nudge_to_floor(sc, x, y, h)
        d = dict(kind="mark", pose="stand", x=x, y=y, h=h, floor_y=y,
                 occ=None, face=facing(x), mark=name, on_floor=moved is not None)
        if moved:
            d["x"], d["y"], d["h"] = moved
            d["floor_y"] = moved[1]
            d["face"] = facing(d["x"])
            d["nudged"] = (abs(moved[0] - x) > 1 or abs(moved[1] - y) > 1)
        marks.append(d)

    # ordering: the scene-specific body positions read best, so they lead.
    rank = {"seat": 0, "soft": 0, "counter": 1, "board": 2, "mark": 3, "floor": 4}
    slots.sort(key=lambda c: (rank[c["kind"]], -c["h"]))
    solid = [m for m in marks if m["on_floor"]]
    kept = _dedupe(slots + solid)
    taken = [(k["x"], k["floor_y"], k["h"]) for k in kept]
    kept += prospect_floor(sc, taken)
    kept = settle(sc, kept)
    # Never drop below three. The architecture's fallback contract is that a scene
    # always has the crew marks, so top up from the ones dedupe or the floor test
    # rejected rather than ship a scene the assembler cannot fill.
    if len(kept) < 3:
        have = {(round(k["x"]), round(k["y"])) for k in kept}
        for m in solid + [m for m in marks if not m["on_floor"]]:
            if (round(m["x"]), round(m["y"])) in have:
                continue
            kept.append(m)
            have.add((round(m["x"]), round(m["y"])))
            if len(kept) >= 3:
                break
    return kept


def settle(sc, kept):
    """Re-verify every stand slot at the INTEGER coordinates that will actually ship.

    The detectors work in floats and the file stores ints, and half a pixel is enough
    to change the answer: one presenter in the library passed its floor test at
    y=595.6 and shipped at y=596, which in that room was the dark edge of a workbench
    rather than the floor beside it. Checking the rounded number is the only way the
    check and the shipped slot can agree, so the last word belongs here.
    """
    out = []
    for k in kept:
        if k["pose"] not in PORTABLE:
            out.append(k)
            continue
        x, y, h = float(round(k["x"])), float(round(k["y"])), float(round(k["h"]))
        if sc.floor_row_frac(x, y, 0.15 * h) >= 0.55:
            k["x"], k["y"], k["h"] = x, y, h
            k["floor_y"] = y
            out.append(k)
            continue
        moved = nudge_to_floor(sc, x, y, h)
        if moved is None:
            continue                         # rather no slot than a foot in the air
        x, y, h = (float(round(v)) for v in moved)
        if sc.floor_row_frac(x, y, 0.15 * h) < 0.55:
            continue
        k["x"], k["y"], k["h"] = x, y, h
        k["floor_y"] = y
        k["face"] = facing(x)
        out.append(k)
    return out


def _dedupe(slots):
    """Greedy non-maximum suppression so two characters never land on one another."""
    kept = []
    for s in slots:
        clash = False
        for k in kept:
            if abs(s["x"] - k["x"]) < 0.34 * min(s["h"], k["h"]) and \
               abs(s["floor_y"] - k["floor_y"]) < 0.40 * min(s["h"], k["h"]):
                clash = True
                break
        if not clash:
            kept.append(s)
    return kept


PORTABLE = ("stand", "stand_present")


def emit(scene_id, slots):
    """One slot record per placement.

    Two fields carry a decision rather than a measurement, and both come from what the
    derive pipeline measured on its pilot (docs/BLANK_SCENES_ARCHITECTURE.md S7):

    occ is ALWAYS null. A derived scene's resident pose is cut as the pixel-difference
    against its own blank, so it arrives with a furniture-shaped hole already in it and
    reproduces the occlusion for free. A rect authored here buys nothing, and a wrong
    one silently deletes a character -- the failure BACKGROUND_INVARIANTS.md measured
    at 4 crew marks in 15.

    confidence says whether a LIBRARY pose can be trusted into this slot. Standing and
    walking poses ported cleanly into measured stand slots on the pilot; seated ones
    broke twice, because a foot-anchored body does not port across chair geometries.
    So seats are marked low and the assembler may prefer dropping a character to
    mis-seating one. lean_counter is low for the same reason -- its pose is coupled to
    the height of one particular counter -- even though its foot point is verified
    floor like any stand slot.
    """
    ids = {}
    out = []
    for i, s in enumerate(slots):
        base = {"seat": "seat", "soft": "soft", "counter": "counter",
                "board": "board", "mark": "floor", "floor": "floor"}[s["kind"]]
        pose = s["pose"]
        ids[base] = ids.get(base, 0) + 1
        out.append({
            "id": f"{base}_{ids[base]}",
            "pose_class": pose,
            "x": int(round(s["x"])),
            "y": int(round(s["y"])),
            "h": int(round(s["h"])),
            "face": s["face"],
            "occ": None,
            "prominence": i + 1,
            "confidence": "high" if pose in PORTABLE else "low",
        })
    return out


def scene_place(scene_id):
    parts = scene_id.split("/")
    return parts[1] if len(parts) > 2 else parts[0]


def process(scene_id, ann):
    sc = Scene(scene_id, ann[scene_id])
    slots = build_slots(sc, scene_place(scene_id))
    return sc, emit(scene_id, slots)


# ===========================================================================
# overlay
# ===========================================================================
PALETTE = {"sit_desk": (0, 140, 255), "sit_couch": (0, 200, 120),
           "sit_audience": (255, 160, 0), "sit_bed": (200, 0, 255),
           "lie_hospital": (255, 0, 180), "stand": (255, 40, 40),
           "stand_present": (255, 230, 0), "lean_counter": (0, 220, 220)}


def overlay(scene_id, slots, out_path, scale=0.75):
    im = Image.open(f"{BG}/{scene_id.replace('/', '__')}.png").convert("RGB")
    d = ImageDraw.Draw(im)
    for s in slots:
        c = PALETTE.get(s["pose_class"], (255, 255, 255))
        x, y, h = s["x"], s["y"], s["h"]
        w = h * 0.34
        if s["occ"]:
            d.rectangle(s["occ"], outline=(255, 0, 255), width=5)
        d.rectangle([x - w / 2, y - h, x + w / 2, y], outline=c, width=4)
        d.line([x - 26, y, x + 26, y], fill=c, width=5)
        d.line([x, y - 26, x, y + 26], fill=c, width=5)
        d.ellipse([x - 7, y - 7, x + 7, y + 7], fill=c)
        lab = "%d:%s%s" % (s["prominence"], s["pose_class"],
                           "" if s.get("confidence", "high") == "high" else "?")
        tx, ty = x - w / 2 + 3, max(2, y - h - 22)
        d.rectangle([tx - 2, ty - 2, tx + 9 * len(lab), ty + 16], fill=(0, 0, 0))
        d.text((tx, ty), lab, fill=c)
    if scale != 1.0:
        im = im.resize((int(CW * scale), int(CH * scale)), Image.LANCZOS)
    im.save(out_path)
    return out_path


# ===========================================================================
# driver
# ===========================================================================
def load_ann():
    with open(ANNOTATIONS) as f:
        return json.load(f)


def all_scenes(ann=None):
    """Every background PNG that has been ANNOTATED.

    A scene with no marks has no measured ground line and no depth model, so there is
    nothing to derive a slot from -- newer packs land here before auto_marks.py has
    run over them, and they are skipped rather than guessed at. Re-running this tool
    after their annotations arrive picks them up.
    """
    ids = []
    for fn in sorted(os.listdir(BG)):
        if fn.endswith(".png"):
            sid = fn[:-4].replace("__", "/")
            if ann is None or sid in ann:
                ids.append(sid)
    return ids


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--probe", nargs="*", default=None)
    ap.add_argument("--outdir", default="/tmp/slot_overlays")
    ap.add_argument("--run", action="store_true")
    ap.add_argument("--only", default=None)
    ap.add_argument("--shard", default=None, help="i/n")
    ap.add_argument("--part", default=None, help="write this partial json")
    ap.add_argument("--stats", action="store_true")
    ap.add_argument("--review", nargs="*", default=None,
                    help="render overlays FROM slots.json, i.e. from what actually ships")
    ap.add_argument("--merge", nargs="*", default=None, help="merge part files into slots.json")
    ap.add_argument("--scale-audit", type=int, default=0, help="sample N scenes; compare\
 the height a detected chair implies against the height the crew marks imply")
    ap.add_argument("--audit", action="store_true",
                    help="re-measure every shipped slot against its own scene")
    ap.add_argument("--audit-shard", default=None)
    a = ap.parse_args()

    ann = load_ann()

    if a.audit:
        with open(SLOTS) as f:
            data = json.load(f)
        ids = sorted(data)
        if a.audit_shard:
            i, n = (int(v) for v in a.audit_shard.split("/"))
            ids = [s for k, s in enumerate(ids) if k % n == i]
        rows = []
        for sid in ids:
            try:
                sc = Scene(sid, ann[sid])
            except Exception as e:
                sys.stderr.write("FAIL %s %s\n" % (sid, e))
                continue
            for sl in data[sid]["slots"]:
                if sl["pose_class"] not in PORTABLE:
                    continue
                f1 = sc.floor_row_frac(sl["x"], sl["y"], 0.15 * sl["h"])
                f2 = sc.floor_row_frac(sl["x"], sl["y"] - 0.06 * sl["h"], 0.11 * sl["h"])
                rows.append((round(f1, 3), round(f2, 3), sid, sl["id"], sl["x"], sl["y"]))
        for r in rows:
            print("%.3f %.3f %s %s %d %d" % r)
        return

    if a.review is not None:
        os.makedirs(a.outdir, exist_ok=True)
        with open(SLOTS) as f:
            data = json.load(f)
        for sid in a.review:
            sid = sid.replace("__", "/")
            e = data.get(sid)
            if not e:
                print("MISSING", sid)
                continue
            print(overlay(sid, e["slots"], f"{a.outdir}/{sid.replace('/', '__')}.png"))
            for sl in e["slots"]:
                print("   ", sl)
        return

    if a.scale_audit:
        ids = all_scenes(ann)
        ids = ids[::max(1, len(ids) // a.scale_audit)]
        rs = []
        for sid in ids:
            place = scene_place(sid)
            prof = PLACE_PROFILES.get(place, MARKS_ONLY)
            if prof["seat"] not in ("sit_desk", "sit_audience"):
                continue
            try:
                sc = Scene(sid, ann[sid])
            except Exception:
                continue
            for c in detect_seats(sc, prof["seat"]):
                rs.append((c["seat_px"] / SEAT_F) / c["h"])
        rs.sort()
        if rs:
            print("chairs measured: %d over %d scenes" % (len(rs), len(ids)))
            print("h_from_chair / h_from_marks: p10 %.2f median %.2f p90 %.2f mean %.2f"
                  % (rs[len(rs) // 10], rs[len(rs) // 2], rs[9 * len(rs) // 10],
                     sum(rs) / len(rs)))
        return

    if a.merge is not None:
        out = {}
        for f in a.merge:
            with open(f) as fh:
                out.update(json.load(fh))
        with open(SLOTS, "w") as fh:
            json.dump(out, fh, indent=1, sort_keys=True)
        print("merged", len(out), "->", SLOTS)
        return

    if a.probe is not None:
        os.makedirs(a.outdir, exist_ok=True)
        for sid in a.probe:
            sid = sid.replace("__", "/")
            sc, slots = process(sid, ann)
            p = overlay(sid, slots, f"{a.outdir}/{sid.replace('/', '__')}.png")
            print(sid, "wall", sc.wall, "floor", sc.floor,
                  "depth a=%.3f b=%.0f" % sc.depth, "ground", int(sc.ground),
                  "clusters", len(sc.clusters))
            for s in slots:
                print("   ", s)
            print("   ->", p)
        return

    if a.stats:
        with open(SLOTS) as f:
            data = json.load(f)
        from collections import Counter
        c = Counter()
        for sid, e in data.items():
            for s in e["slots"]:
                c[s["pose_class"]] += 1
        for k, v in c.most_common():
            print("%-16s %5d" % (k, v))
        print("scenes", len(data))
        return

    if a.run:
        ids = all_scenes(ann)
        if a.only:
            ids = [i for i in ids if a.only in i]
        if a.shard:
            i, n = (int(v) for v in a.shard.split("/"))
            ids = [s for k, s in enumerate(ids) if k % n == i]
        out = {}
        path = a.part or SLOTS
        if os.path.exists(path):
            try:
                with open(path) as f:
                    out = json.load(f)
            except Exception:
                out = {}
        done = 0
        for sid in ids:
            if sid in out and not a.only:
                continue
            try:
                sc, slots = process(sid, ann)
                out[sid] = {"slots": slots}
            except Exception as e:
                sys.stderr.write("FAIL %s %s\n" % (sid, e))
                continue
            done += 1
            if done % 25 == 0:
                with open(path, "w") as f:
                    json.dump(out, f, indent=1, sort_keys=True)
                print("  ..%d/%d" % (done, len(ids)), flush=True)
        with open(path, "w") as f:
            json.dump(out, f, indent=1, sort_keys=True)
        print("wrote", path, len(out))
        return

    ap.print_help()


if __name__ == "__main__":
    main()
