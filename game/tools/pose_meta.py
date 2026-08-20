#!/usr/bin/env python3
"""THE POSE LIBRARY — data, prompts, keying, eye extraction and quality gates.

This is library B of docs/BLANK_SCENES_ARCHITECTURE.md: 21 characters x 25 canonical
pose ids, each a single figure on flat magenta, keyed to alpha, cropped to content, and
shipped with `{eyes, anchor, w, h}` so the runtime can blink and anchor it for free.

WHY THE DATA LIVES HERE AND THE DRIVER LIVES NEXT DOOR: gen_poses.py is a scheduler —
concurrency, resume, re-roll budget. Everything that decides what a sprite IS (who, what
body position, what counts as an acceptable image) is here, so a pose can be re-worded
and re-rolled without touching the machinery, and so the quality gate can be run over an
already-generated library without generating anything.

Three facts drive every prompt:

1. THE CHARACTER LAW IS ABSOLUTE. Identical ink-black bean blobs, two blank white oval
   eyes (left bigger), one cowlick spike, thin stick limbs, cream sneakers with one lace
   untied, no mouth, no nose, no ears, no pupils, NO CLOTHING. Characters differ ONLY by
   the props they hold or stand beside. A "cardigan" character therefore CARRIES the
   cardigan over an arm; it never wears it.

2. THE SCENE PROVIDES THE FURNITURE. A seated pose shows no chair, a lying pose shows no
   bed, a typing pose shows no keyboard. The sprite composites onto a scene that already
   drew those. So every non-standing pose carries an explicit "the seat is invisible"
   clause AND a furniture negative, because a model asked to draw a seated figure will
   draw the chair unless it is told twice.

3. SHADOW IS A GROUNDING DEVICE, NOT DECORATION. A standing figure needs a soft
   elliptical contact shadow or it floats (one of the four causes of the owner's earlier
   rejection of pasted sprites). A seated figure must NOT have one: the furniture it
   sits on grounds it, and a shadow under a character sitting on a chair reads as a
   second, wrong floor.

PROPS COME IN THREE FORMS per character, chosen by how busy the pose's hands are. A
character whose identity is "an open laptop held in both hands" is unrecognisable in
sit_couch_headinhands unless the laptop goes somewhere. So each character declares what
it looks like with both hands free, with one hand free, and with no hands free (the prop
set down on the ground beside it, or worn).
"""
import json, math, os

GAME = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
POSES_DIR = f"{GAME}/assets/poses"

# ---------------------------------------------------------------------------
# THE 21 CHARACTERS
# ---------------------------------------------------------------------------
# `ref` is the identity anchor handed to the seedream edit endpoint.
#   - the 9 cast already have an approved canonical sprite in assets/scenes, and the
#     MAGENTA ORIGINAL (scene.png) is used rather than the keyed sprite.png: a keyed PNG
#     is composited onto some unknown colour by the upload/edit path, and this cast is a
#     solid black silhouette, so a black composite would erase the very thing the
#     reference exists to carry. The magenta original also teaches the background
#     convention and the contact-shadow look in the same image.
#   - the 12 externals have no canonical yet. `ref` is None and gen_poses.py generates
#     assets/poses/<id>/_canonical.png first, from a cast reference, then uses it.
CAST_PROPS_NOTE = "the SAME character as in the reference image: identical body, identical eyes, identical props"

CHARACTERS = {
    # ---- the 9 cast: props copied verbatim from the approved batch_l/batch_n prompts,
    # so a pose sprite and the canonical sprite are recognisably the same character.
    "hacker": dict(
        label="the HACKER founder",
        ref="cast_hacker_fine/scene",
        held="an open laptop held in both hands, its screen glowing faintly",
        one="an open laptop held in one hand like a tray, its screen glowing faintly",
        ground="a closed laptop lying flat on the ground right beside the character"),
    "founder_hustler": dict(
        label="the HUSTLER founder",
        ref="cast_founder_hustler_fine/scene",
        held="a phone held to the side of the head as if mid-call, and a takeaway coffee cup in the other hand",
        one="a phone held to the side of the head as if mid-call",
        ground="a phone and a takeaway coffee cup standing on the ground right beside the character"),
    "founder_pm": dict(
        label="the EX-FAANG PM founder",
        ref="cast_founder_pm_fine/scene",
        held="an identity badge on a lanyard around the neck and a small fan of yellow sticky notes held in one hand",
        one="an identity badge on a lanyard around the neck",
        ground="an identity badge on a lanyard around the neck, with a few yellow sticky notes on the ground beside the character"),
    "founder_consultant": dict(
        label="the EX-CONSULTANT founder",
        ref="cast_founder_consultant_fine/scene",
        held="a small wheeled roller suitcase held by its handle and a laser pointer in the other hand",
        one="a small wheeled roller suitcase held by its handle",
        ground="a small wheeled roller suitcase standing upright on the ground right beside the character, a laser pointer lying next to it"),
    "cofd_sales": dict(
        label="the SALES cofounder",
        ref="cast_cofd_sales_fine/scene",
        held="a telephone headset worn over the head and a signed paper contract held out in one hand",
        one="a telephone headset worn over the head",
        ground="a telephone headset worn over the head, with a curled paper contract on the ground beside the character"),
    "cofd_business": dict(
        label="the BUSINESS cofounder",
        ref="cast_cofd_business_fine/scene",
        held="an open laptop held in both hands showing a small rising line chart, and a tiny tie",
        one="an open laptop held in one hand showing a small rising line chart, and a tiny tie",
        ground="a tiny tie, and a closed laptop lying on the ground right beside the character"),
    "cofd_tech": dict(
        label="the TECH cofounder",
        ref="cast_cofd_tech_fine/scene",
        held="a soldering iron held in one hand with a thin wisp of smoke, and a mug in the other hand",
        one="a soldering iron held in one hand with a thin wisp of smoke",
        ground="a soldering iron and a mug resting on the ground right beside the character"),
    "cofd_hustler": dict(
        label="the HUSTLER cofounder",
        ref="cast_cofd_hustler_fine/scene",
        held="a phone held to the side of the head as if mid-call, and a takeaway coffee cup in the other hand",
        one="a takeaway coffee cup held in one hand",
        ground="a phone and a takeaway coffee cup on the ground right beside the character"),
    "cofd_idea": dict(
        label="THE IDEA FRIEND cofounder, who is visibly doing nothing useful",
        ref="cast_cofd_idea_fine/scene",
        held="a tall smoothie with a straw held in one hand and completely empty other hand",
        one="a tall smoothie with a straw held in one hand",
        ground="a tall smoothie with a straw standing on the ground right beside the character"),

    # ---- the 12 externals. No canonical exists; one is generated first (see
    # canonical_prompt below) and then anchors all 25 of that character's poses.
    # NOTE ON CLOTHING: two of these are described in the design as "cardigan"
    # characters. The character law forbids clothing outright, so the cardigan is a
    # CARRIED prop draped over an arm, never worn. Identity by props only, no exception.
    "vc_investor": dict(
        label="the VC INVESTOR",
        ref=None,
        held="a slim dark leather briefcase held in one hand and a small white business card held out in the other",
        one="a slim dark leather briefcase held in one hand",
        ground="a slim dark leather briefcase standing on the ground right beside the character with a small white business card lying on top of it"),
    "angel_investor": dict(
        label="the ANGEL INVESTOR",
        ref=None,
        held="a soft sage-green cardigan draped over one arm and a tall smoothie cup with a straw held in the other hand",
        one="a soft sage-green cardigan draped over one arm",
        ground="a folded sage-green cardigan and a tall smoothie cup with a straw on the ground right beside the character"),
    "employee": dict(
        label="the EMPLOYEE",
        ref=None,
        held="an identity badge on a plain lanyard around the neck and an open laptop covered in small coloured sticker shapes held in both hands",
        one="an identity badge on a plain lanyard around the neck",
        ground="an identity badge on a plain lanyard around the neck, and a closed laptop covered in small coloured sticker shapes on the ground beside the character"),
    "press_reporter": dict(
        label="the PRESS REPORTER",
        ref=None,
        held="a chunky handheld microphone held out in one hand and a small flip notepad in the other",
        one="a chunky handheld microphone held out in one hand",
        ground="a chunky handheld microphone and a small flip notepad lying on the ground right beside the character"),
    "customer_suit": dict(
        label="the CORPORATE CUSTOMER",
        ref=None,
        held="a boxy briefcase held in one hand and a chunky wristwatch on the other thin arm",
        one="a boxy briefcase held in one hand",
        ground="a boxy briefcase standing on the ground right beside the character, and a chunky wristwatch on one thin arm"),
    "customer_casual": dict(
        label="the EVERYDAY CUSTOMER",
        ref=None,
        held="a canvas tote bag hanging from one hand with a rolled-up magazine poking out of it",
        one="a canvas tote bag hanging from one hand",
        ground="a canvas tote bag slumped on the ground right beside the character with a rolled-up magazine poking out of it"),
    "lawyer": dict(
        label="the LAWYER",
        ref=None,
        held="a thick bulging file folder of papers held under one arm with loose pages sticking out, and a fountain pen in the other hand",
        one="a thick bulging file folder of papers held under one arm with loose pages sticking out",
        ground="a thick bulging file folder of papers on the ground right beside the character with loose pages sticking out of it"),
    "landlord": dict(
        label="the LANDLORD",
        ref=None,
        held="a big ring of jangling keys held up in one hand and a rolled paper lease in the other",
        one="a big ring of jangling keys held up in one hand",
        ground="a big ring of keys and a rolled paper lease lying on the ground right beside the character"),
    "parent": dict(
        label="the FOUNDER'S PARENT",
        ref=None,
        held="a soft cream cardigan draped over one arm and a small framed photograph held in the other hand",
        one="a small framed photograph held in one hand",
        ground="a folded cream cardigan with a small framed photograph resting on it on the ground beside the character"),
    "partner": dict(
        label="the FOUNDER'S PARTNER",
        ref=None,
        held="two takeaway coffee cups, one held in each hand",
        one="a takeaway coffee cup held in one hand",
        ground="two takeaway coffee cups standing on the ground right beside the character"),
    "yc_partner": dict(
        label="the ACCELERATOR PARTNER",
        ref=None,
        held="a bright coral-orange lanyard badge around the neck and a cream clipboard held in one hand",
        one="a bright coral-orange lanyard badge around the neck",
        ground="a bright coral-orange lanyard badge around the neck, and a cream clipboard lying on the ground beside the character"),
    "official": dict(
        label="the GOVERNMENT OFFICIAL",
        ref=None,
        held="a wooden-handled rubber stamp raised in one hand and a single cream form sheet held in the other",
        one="a wooden-handled rubber stamp raised in one hand",
        ground="a wooden-handled rubber stamp and a cream form sheet lying on the ground right beside the character"),
}

EXTERNALS = [c for c, v in CHARACTERS.items() if v["ref"] is None]
CAST = [c for c, v in CHARACTERS.items() if v["ref"] is not None]

# ---------------------------------------------------------------------------
# THE 25 CANONICAL POSE IDS
# ---------------------------------------------------------------------------
# The architecture doc calls these "the 24" because its table counts
# stand_handshake_L / _R as one cell. They are two sprites: a handshake is the one pose
# the engine CANNOT produce by flipping, because flipping mirrors the props too — the
# left-hand partner and the right-hand partner have to be generated separately.
#
# hands: "free" both hands available | "one" one hand free | "none" both occupied.
#        This picks which of the character's three prop forms goes in the prompt.
# face:  the single generated facing. The engine flips for the other side.
# shadow: a soft elliptical contact shadow under the sneakers. Standing/walking only.
# anchor: "seat" for sit_*/lie_*/sleep_desk, "feet" for everything else.
POSES = [
    dict(id="sit_desk_typing", anchor="seat", shadow=False, geometry="desk", seat_frac=0.45, hands="none", face="left",
         posture="SEATED at seated height on a chair that is NOT drawn: the bottom is low as if resting on an "
                 "invisible seat, the thighs horizontal and the shins vertical so hip-knee-foot makes a Z shape, "
                 "both sneakers flat on the ground, the torso leaning forward from the hips, and both thin arms "
                 "reaching forward and slightly down to the LEFT with the two hands level with each other at the "
                 "same height, fingers spread, as if typing on a keyboard that is not shown"),
    dict(id="sit_desk_slumped", anchor="seat", shadow=False, geometry="desk", seat_frac=0.45, hands="none", face="left",
         posture="SEATED at seated height on a chair that is NOT drawn, thighs horizontal, shins vertical, both "
                 "sneakers flat on the ground — and collapsed forward in defeat: the torso folded down over the "
                 "knees, the head hanging low, the cowlick spike bent over limp, one thin arm dangling straight "
                 "down toward the ground and the other draped loosely across the lap"),
    dict(id="sit_couch_relaxed", anchor="seat", shadow=False, geometry="seat", seat_frac=0.45, hands="free", face="left",
         posture="SEATED low and reclining on a couch that is NOT drawn: the bottom very low, the whole body "
                 "leaning well back, both legs stretched out forward to the LEFT with the sneakers resting on "
                 "their heels, and both thin arms spread wide and loose as if resting along the top of an "
                 "invisible backrest"),
    dict(id="sit_couch_headinhands", anchor="seat", shadow=False, geometry="seat", seat_frac=0.45, hands="none", face="left",
         posture="SEATED low on a couch that is NOT drawn, knees apart and bent, both elbows planted on the "
                 "knees, the head dropped low and held in both hands — the hands cup the SIDES of the head so "
                 "that both blank white eyes stay completely visible between them — the cowlick spike bent over "
                 "limp with despair"),
    dict(id="sit_audience_neutral", anchor="seat", shadow=False, geometry="seat", seat_frac=0.45, hands="free", face="left",
         posture="SEATED upright on a chair that is NOT drawn: knees together, thighs horizontal, shins vertical, "
                 "both sneakers flat on the ground, back straight, both hands resting flat on the lap, watching "
                 "something off-frame to the LEFT attentively"),
    dict(id="sit_audience_clapping", anchor="seat", shadow=False, geometry="seat", seat_frac=0.45, hands="none", face="left",
         posture="SEATED upright on a chair that is NOT drawn, knees together, both sneakers flat on the ground — "
                 "and applauding: both thin arms bent up in front of the chest with the two hands together "
                 "mid-clap, a few small ink motion ticks flicking out around the hands"),
    dict(id="sit_bed", anchor="seat", shadow=False, geometry="seat", seat_frac=0.45, hands="free", face="left",
         posture="SEATED on the edge of a bed that is NOT drawn: perched on an invisible low edge with both legs "
                 "hanging straight down and the sneakers dangling just clear of the ground, shoulders rounded "
                 "forward, both hands resting on the invisible edge beside the hips"),
    dict(id="lie_hospital", anchor="seat", shadow=False, geometry="lie", seat_frac=0.16, hands="none", face="left",
         posture="LYING FLAT ON ITS BACK on a bed that is NOT drawn: the whole bean body horizontal and level "
                 "across the frame, the head at the LEFT with its cowlick spike pointing left, the sneakers at "
                 "the RIGHT, both thin arms straight down along the sides of the body, both legs straight, and "
                 "the two blank white oval eyes on the upper side of the head looking straight up at the viewer"),
    dict(id="sleep_desk", anchor="seat", shadow=False, geometry="desk", seat_frac=0.45, hands="none", face="left",
         posture="SEATED at seated height on a chair that is NOT drawn and ASLEEP face-down on a desk that is NOT "
                 "drawn: the torso folded forward and down, both thin arms stretched out flat and level in front "
                 "at chest height as if lying along an invisible desktop, the head resting sideways on one arm so "
                 "both eyes are still fully visible, and the eyes still drawn as two blank white ovals — never "
                 "closed, never drawn as lines"),
    dict(id="stand_neutral", anchor="feet", shadow=True, hands="free", face="left",
         posture="STANDING upright and alert on both feet with a slight forward lean, weight even on both "
                 "sneakers, both thin arms relaxed at the sides"),
    dict(id="stand_phone", anchor="feet", shadow=True, hands="one", face="left",
         posture="STANDING upright, one thin arm bent up so the hand holds a small dark phone flat against the "
                 "side of the head as if mid-call, the other arm gesturing a little out from the body"),
    dict(id="stand_present_pointer", anchor="feet", shadow=True, hands="one", face="left",
         posture="STANDING upright and turned slightly away, one thin arm raised up and forward to the LEFT "
                 "holding a slim ink pointer stick angled up at something off-frame, the other arm down at the "
                 "side. There is NO board, NO whiteboard, NO easel and NO screen drawn anywhere in the image"),
    dict(id="stand_mic", anchor="feet", shadow=True, hands="one", face="left",
         posture="STANDING upright and speaking, one thin arm bent up holding a small handheld microphone — a "
                 "short cylinder with a round grey head — just below the eyes, the other arm out in a small "
                 "speaking gesture. No microphone stand and no cable"),
    dict(id="stand_handshake_L", anchor="feet", shadow=True, hands="one", face="right",
         posture="STANDING upright and turned to face the RIGHT side of the frame, the near thin arm extended "
                 "straight forward to the RIGHT at chest height with the hand open and flat, ready to shake a "
                 "hand that is NOT drawn, the other arm at the side. Only ONE character is in the image; there is "
                 "no second creature and no other hand"),
    dict(id="stand_handshake_R", anchor="feet", shadow=True, hands="one", face="left",
         posture="STANDING upright and turned to face the LEFT side of the frame, the near thin arm extended "
                 "straight forward to the LEFT at chest height with the hand open and flat, ready to shake a "
                 "hand that is NOT drawn, the other arm at the side. Only ONE character is in the image; there is "
                 "no second creature and no other hand"),
    dict(id="stand_carrybox", anchor="feet", shadow=True, hands="none", face="left",
         posture="STANDING and leaning back a little under a weight, both thin arms wrapped around a plain cream "
                 "cardboard box with ink outlines held in front of the belly, the box tilted slightly, both "
                 "sneakers planted flat"),
    dict(id="stand_wave_celebrate", anchor="feet", shadow=True, hands="none", face="left",
         posture="STANDING in celebration with both thin arms thrown up high above the head, one hand open and "
                 "waving, the body arched back a little in delight, both sneakers still flat on the ground, a few "
                 "small ink joy-ticks radiating out around the head"),
    dict(id="stand_armscrossed", anchor="feet", shadow=True, hands="none", face="left",
         posture="STANDING squared up and sceptical, both thin arms folded across the front of the bean one over "
                 "the other, the weight shifted onto one leg, the head tilted back a fraction"),
    dict(id="stand_reading_paper", anchor="feet", shadow=True, hands="none", face="left",
         posture="STANDING still and reading, both hands holding a single loose sheet of cream paper up in front "
                 "at chest height, the head tilted down toward it. The sheet of paper is COMPLETELY BLANK — no "
                 "writing, no letters, no numbers, no lines on it"),
    dict(id="stand_writing_clipboard", anchor="feet", shadow=True, hands="none", face="left",
         posture="STANDING still and writing, one thin arm cradling a cream clipboard against the body at chest "
                 "height, the other hand holding a slim ink pen down on the clipboard mid-stroke. The clipboard "
                 "sheet is COMPLETELY BLANK — no writing, no letters, no numbers on it"),
    dict(id="stand_point_accuse", anchor="feet", shadow=True, hands="one", face="left",
         posture="STANDING and leaning forward hard in accusation, one thin arm shot straight out to the LEFT at "
                 "shoulder height with a single finger pointing at something off-frame, the other arm swung back "
                 "behind the body, the front sneaker planted forward"),
    dict(id="stand_slumped", anchor="feet", shadow=True, hands="free", face="left",
         posture="STANDING but sagging with exhaustion: the shoulders dropped, the whole bean drooping forward, "
                 "the cowlick spike bent over limp, both thin arms hanging heavy and straight down, the head low"),
    dict(id="stand_coffee", anchor="feet", shadow=True, hands="one", face="left",
         posture="STANDING upright and calm, one hand holding a small takeaway coffee cup up near the chest, the "
                 "other thin arm relaxed at the side"),
    dict(id="walk_stride", anchor="feet", shadow=True, hands="one", face="left",
         posture="MID-STRIDE, walking to the LEFT seen from the side: the legs scissored wide apart with the "
                 "leading sneaker heel-down on the ground and the trailing sneaker up on its toe behind, both "
                 "thin arms swinging in opposition, the body leaning forward into the walk"),
    dict(id="crouch_pack", anchor="feet", shadow=True, hands="none", face="left",
         posture="CROUCHED right down on the heels: the knees bent all the way up and apart, the bottom close to "
                 "the ground, both sneakers flat on the ground, the torso leaning forward, and both thin arms "
                 "reaching down and forward to the LEFT as if packing things into a box on the ground"),
]
POSE_BY_ID = {p["id"]: p for p in POSES}

CHARACTER_LAW = {
    "body": "one solid ink-black bean-shaped blob, unbroken silhouette",
    "top": "exactly one ink cowlick spike",
    "eyes": "exactly two blank white ovals, the left slightly bigger, COMPLETELY blank — no pupils, "
            "no irises, no dots, no eyelids, no eyebrows",
    "face": "no mouth, no nose, no ears",
    "limbs": "thin black stick arms and legs",
    "feet": "tiny cream-white sneakers, one lace untied",
    "clothing": "NONE — no shirt, no hoodie, no jacket, no hat; the body is a bare solid black silhouette",
}
PALETTE = ["#1E1E1E", "#E86A5C", "#F4B942", "#8FA582", "#6E8CA0", "#F2EAD3", "#FFFFFF"]

BACKGROUND = ("completely flat uniform pure magenta #FF00FF filling the whole frame, absolutely empty — no floor, "
              "no wall, no horizon line, no gradient, no texture, no objects, no second figure")
# THE CONTACT SHADOW IS BAKED, NOT PROMPTED — and this is a measured decision, not a
# shortcut. A standing pose must carry a soft elliptical contact shadow or it floats,
# which is one of the four named causes of the owner's earlier rejection of pasted
# sprites. Asking the model for it fails twice over at this frame size:
#   - it is inconsistent. Three standing pilots asked for the shadow; all three came back
#     with a shadow too small to register (soft-alpha 0.002-0.035 against the 0.48-0.68
#     the approved 2048-wide cast sprites score). Rejecting on that would have re-rolled
#     essentially every one of the ~325 standing sprites twice — six hundred wasted
#     generations to still not have a shadow.
#   - what it does draw is a DARKENED MAGENTA wash, not a grey one, so keying eats most
#     of it and leaves a violet stain where it was. That is the exact halo the whole
#     dominance-keying method exists to avoid.
# So every prompt asks for clean magenta underneath, and bake_shadow() draws one
# identical, correctly coloured, correctly placed ellipse under the sneakers of every
# standing pose. 325 sprites that ground the same way beats 325 that each guess.
SHADOW_NO = ("NONE — there is no shadow anywhere under or around the character; the magenta beneath it is "
             "completely clean and unbroken")
NO_FURNITURE = ("NO furniture of any kind is drawn anywhere in the image — no chair, no stool, no bench, no "
                "couch, no sofa, no desk, no table, no bed, no mattress, no pillow, no cushion, no keyboard, no "
                "monitor and no floor. The character rests on empty magenta and the seat it uses is invisible")
NO_FURNITURE_STAND = ("NO furniture and NO scenery of any kind is drawn anywhere in the image — no chair, no "
                      "desk, no table, no wall, no floor line. Only the character, its own props and its contact "
                      "shadow are on the magenta")

# CANONICAL SEATED GEOMETRY (coordinator finding, measured on the assembly pilot).
# Standing poses port into any scene on a foot anchor; seated ones do not, because chair
# geometry varies room to room and a seated sprite anchored at its feet breaks the moment
# the chair is a different height. The fix is to stop treating a seated sprite as a body
# and start treating it as a body PLUS a known seat line: if every seated pose in the
# library puts its seat line at the same fraction of its own height, the assembler can
# anchor at the chair's seat instead of at the floor and the pose lands correctly on any
# chair. The numbers are stated relative to THE FIGURE, not the frame, because the sprite
# is cropped to its bounding box before it ships — 45 percent of a 1024px render is a
# different line from 45 percent of the crop.
SEAT_FRAC = 0.45
GEOMETRY_SEAT = (
    "CANONICAL SEATED GEOMETRY — obey these proportions exactly, they are what let this sprite drop onto any "
    "chair in any room: draw the character in TRUE SIDE PROFILE facing LEFT, not three-quarters and not "
    "front-on; the thighs are exactly HORIZONTAL and the shins exactly VERTICAL; the SEAT LINE — the underside "
    "of the bottom, which is also the top of the horizontal thighs — sits at 45 percent of the FIGURE's total "
    "height, measured upward from the soles of the sneakers; the sneakers rest flat on the ground at the very "
    "bottom of the figure.")
GEOMETRY_DESK = (
    " The HANDS are at desk height: 55 percent of the figure's total height above the soles, level with each "
    "other, reaching forward to the LEFT over a desk surface that is NOT drawn.")
GEOMETRY_LIE = (
    "CANONICAL LYING GEOMETRY — the whole body is horizontal and level across the frame in TRUE SIDE VIEW, the "
    "back and the backs of the legs resting along one straight support line at the very bottom of the figure, "
    "the head to the LEFT and the sneakers to the RIGHT, so the sprite can be laid on any bed or trolley by its "
    "underside.")

BASE_NEG = ("no text, no lettering, no words, no numbers, no logos, no pupils, no irises, no dots in the eyes, "
            "no mouth, no nose, no ears, no eyebrows, no clothing, no shirt, no hoodie, no jacket, no hat, "
            "no background scenery, no floor, no wall, no second character, no speech bubble, no frame, no border")


def _props(char, hands):
    c = CHARACTERS[char]
    return c[{"free": "held", "one": "one", "none": "ground"}[hands]]


def pose_prompt(char, pose_id, fix=""):
    """The generation prompt for one pose sprite. JSON on purpose: scene_pipeline's
    variant() skips its scene STYLE block when the prompt starts with '{', and that
    block's 3:2 composition rules and cream-paper palette are wrong for one figure on
    magenta. This is the same prompt shape that produced the approved cast.

    `fix` is what the previous attempt got wrong, in the model's own terms. A blind
    re-roll of the identical prompt tends to reproduce the identical defect, so a
    rejected sprite is regenerated with the defect named."""
    p = POSE_BY_ID[pose_id]
    c = CHARACTERS[char]
    standing = p["anchor"] == "feet"
    body = {}
    if fix:
        body["correction"] = ("A previous attempt was REJECTED for this reason, so fix it explicitly this "
                              "time: " + fix)
    body.update({
        "task": "single character sprite for chroma-key compositing into a game scene",
        "subject": f"one creature, {c['label']} — {CAST_PROPS_NOTE}",
        "posture": p["posture"],
        "character_law": CHARACTER_LAW,
        "identifying_props": _props(char, p["hands"]),
        "framing": "exactly one single figure, centred in the frame, the whole body and both sneakers visible, "
                   "filling about 75 percent of the frame height, nothing cropped by the frame edge",
        "geometry": {"desk": GEOMETRY_SEAT + GEOMETRY_DESK, "seat": GEOMETRY_SEAT,
                     "lie": GEOMETRY_LIE}.get(p.get("geometry"), "the character stands on the ground"),
        "furniture": NO_FURNITURE_STAND if standing else NO_FURNITURE,
        "contact_shadow": SHADOW_NO,          # always: bake_shadow() puts the real one in
        "background": BACKGROUND,
        "style": "flat hand-drawn cartoon, wobbly felt-pen ink outlines, flat fills, no gradients, matching the "
                 "reference image's line quality and character exactly",
        "palette": PALETTE,
        "negative": BASE_NEG + ", no shadow, no drop shadow, no cast shadow, no shading on the ground",
    })
    return json.dumps(body)


def canonical_prompt(char, fix=""):
    """The first sprite of an external character: plain standing, both hands full of the
    props that ARE its identity. Everything downstream references this one image, so it
    is generated alone, verified, and only then used.

    TWO references, deliberately, and from two DIFFERENT cast members. One reference and
    the model copies its props along with its body — an external anchored only on the
    hacker comes back holding a laptop. Two references that share a body but disagree
    about props leave only the body to copy, which is exactly the part that must carry
    over."""
    c = CHARACTERS[char]
    body = {}
    if fix:
        body["correction"] = ("A previous attempt was REJECTED for this reason, so fix it explicitly this "
                              "time: " + fix)
    body.update({
        "task": "single character sprite for chroma-key compositing into a game scene",
        "subject": f"one creature, {c['label']}. It has the EXACT same body, eyes, cowlick and sneakers as the "
                   f"creatures in the reference images — the ONLY difference is the props it carries",
        "posture": "STANDING upright and alert on both feet with a slight forward lean, weight even on both "
                   "sneakers, facing slightly to the LEFT",
        "character_law": CHARACTER_LAW,
        "identifying_props": c["held"],
        "framing": "exactly one single figure, centred in the frame, the whole body and both sneakers visible, "
                   "filling about 75 percent of the frame height, nothing cropped by the frame edge",
        "furniture": NO_FURNITURE_STAND,
        "contact_shadow": SHADOW_NO,
        "background": BACKGROUND,
        "style": "flat hand-drawn cartoon, wobbly felt-pen ink outlines, flat fills, no gradients, matching the "
                 "reference images' line quality exactly",
        "palette": PALETTE,
        "negative": BASE_NEG + ", no shadow, no drop shadow, no shading on the ground. Do NOT copy the props "
                              "from the reference images: no laptop, no telephone headset, no paper contract "
                              "unless it is listed in identifying_props above",
    })
    return json.dumps(body)


# The two style anchors every external character is born from — different props, same law.
CANONICAL_REFS = ["cast_hacker_fine/scene", "cast_cofd_sales_fine/scene"]


# ---------------------------------------------------------------------------
# KEYING — magenta dominance, never Euclidean distance
# ---------------------------------------------------------------------------
BG_ABOVE, OPAQUE_BELOW = 90, 40


def _dominance(r, g, b):
    """min(R-G, B-G), clamped to 0..255 — how magenta-dominant a pixel is.

    Distance to magenta is the obvious metric and it is measurably wrong on this
    palette: a mid-grey sits ~209-215 away and coral ~196, both inside any usable
    feather band, so distance keying EATS THE CONTACT SHADOW and leaves a violet halo
    where it was. Dominance keys on what actually makes the background background — red
    and blue high while green is low — which no grey (R=G=B, so 0) and no coral (blue
    low, so negative) can fake. Measured on a real pose render: the figure scores 0-15,
    the magenta 192-223, and only ~2000 antialiased pixels land in between."""
    from PIL import ImageMath
    def expr(a):
        rg = a["float"](a["r"]) - a["float"](a["g"])
        bg = a["float"](a["b"]) - a["float"](a["g"])
        return a["convert"](a["max"](a["min"](rg, bg), 0.0), "L")
    if hasattr(ImageMath, "lambda_eval"):
        return ImageMath.lambda_eval(expr, r=r, g=g, b=b)
    return ImageMath.eval("convert(max(min(float(r) - float(g), float(b) - float(g)), 0), 'L')", r=r, g=g, b=b)


def key_sprite(src_path, dst_path, shadow=False):
    """magenta -> alpha, de-fringe, de-stain, crop to content, bake the contact shadow.

    Returns the finished RGBA sprite. `shadow` is the pose's own flag: True bakes the
    grounding ellipse, False leaves the sprite clean for furniture to ground."""
    from PIL import Image, ImageMath
    im = Image.open(src_path).convert("RGB")
    r, g, b = im.split()
    dom = _dominance(r, g, b)
    alpha = dom.point(lambda d: 0 if d > BG_ABOVE else (255 if d < OPAQUE_BELOW else
                      int(round((BG_ABOVE - d) * 255 / (BG_ABOVE - OPAQUE_BELOW)))))
    # De-fringe only the feather band: pixels kept there still carry magenta spill, which
    # reads as a violet halo over a cream wall. Clamp red and blue to green+30 — G+30
    # rather than G so a yellow prop's antialiased rim stays yellow instead of turning
    # olive, while a magenta rim (R,B far above G) is still pulled back to ink.
    def clamp(ch, headroom):
        if hasattr(ImageMath, "lambda_eval"):
            return ImageMath.lambda_eval(
                lambda a: a["convert"](a["min"](a["float"](a["c"]), a["float"](a["g"]) + a["h"]), "L"),
                c=ch, g=g, h=float(headroom))
        return ImageMath.eval("convert(min(float(c), float(g)+%f), 'L')" % headroom, c=ch, g=g)
    spill = alpha.point(lambda a: 255 if 0 < a < 255 else 0)
    r2 = Image.composite(clamp(r, 30), r, spill)
    b2 = Image.composite(clamp(b, 30), b, spill)
    # DE-STAIN. The feather clamp handles the one-pixel rim, but a wash the model painted
    # over the magenta (a stray shadow, a soft glow) can land BELOW the feather band and
    # survive fully opaque while still visibly purple — measured at rgb(48,24,48) under a
    # pilot's sneakers. Anything still magenta-leaning after keying is not part of this
    # palette by definition, so its red and blue are pulled level with its green: the
    # purple goes, the value stays, and an ink line stays an ink line.
    stain = dom.point(lambda d: 255 if 12 < d <= BG_ABOVE else 0)
    r2 = Image.composite(clamp(r2, 0), r2, stain)
    b2 = Image.composite(clamp(b2, 0), b2, stain)
    out = Image.merge("RGBA", (r2, g, b2, alpha))
    bbox = alpha.getbbox()
    if bbox:
        out = out.crop(bbox)
    if shadow:
        out = bake_shadow(out)
    if dst_path:
        out.save(dst_path)
    return out


def bake_shadow(img, opacity=96, squash=0.24):
    """Draw one soft elliptical contact shadow under the sneakers and return the result.

    Placement is read off the sprite rather than guessed: the lowest band of solid pixels
    is the shoes, its horizontal span sets the ellipse width, and the ellipse is nudged
    down and to the RIGHT because every room in this game is lit from the upper left —
    the same light direction the approved cast sprites were generated under, so a pose
    dropped into a scene beside them agrees about where the sun is.

    The shadow is warm near-black at partial alpha rather than a flat grey fill, so it
    multiplies believably over a cream floor, a night-blue floor or a coral one."""
    from PIL import Image, ImageDraw, ImageFilter
    W, H = img.size
    px = img.load()
    # FIND THE GROUND LINE, do not assume it is the bottom row. The crop is the alpha
    # bounding box, and a faint wash the model painted despite being told not to can
    # extend that box well below the shoes while carrying almost no alpha — measured on
    # stand_point_accuse, whose bottom 7 percent held ZERO solid pixels, so the shadow
    # silently did not get drawn at all. Walk up from the bottom to the lowest row that
    # actually contains the character and put the shadow there.
    ground = None
    for y in range(H - 1, -1, -1):
        if sum(1 for x in range(0, W, 2) if px[x, y][3] > 200) >= 2:
            ground = y
            break
    if ground is None:
        return img
    band = max(3, int(H * 0.07))
    xs = [x for y in range(max(0, ground - band), ground + 1) for x in range(W) if px[x, y][3] > 200]
    if not xs:
        return img
    fx0, fx1 = min(xs), max(xs)
    fw = max(12, fx1 - fx0)
    # A MARGIN, NOT A MULTIPLIER. Scaling the ellipse by a factor of the foot span looks
    # right for a standing figure and absurd for walk_stride, whose feet are half a body
    # apart — 1.85x turned a 671px sprite into a 1287px one that was mostly shadow. A
    # fixed margin tied to the character's height widens the pool by the same believable
    # amount whether the feet are together or scissored.
    ew = fw + max(int(H * 0.15), int(fw * 0.28))
    eh = max(8, int(ew * squash))
    ecx = (fx0 + fx1) // 2 + int(fw * 0.10)          # light upper-left -> shadow to the right
    ecy = ground - int(eh * 0.22)
    pad_x = max(0, max(ecx + ew // 2 - (W - 1), -(ecx - ew // 2)))
    pad_b = max(0, ecy + eh // 2 + 3 - (H - 1))
    W2, H2 = W + 2 * pad_x, H + pad_b
    layer = Image.new("L", (W2, H2), 0)
    ImageDraw.Draw(layer).ellipse(
        [pad_x + ecx - ew // 2, ecy - eh // 2, pad_x + ecx + ew // 2, ecy + eh // 2], fill=255)
    layer = layer.filter(ImageFilter.GaussianBlur(max(2.0, eh * 0.30)))
    # Clamp the blur's long tail to zero. A Gaussian leaves alpha 1-5 spreading a fifth
    # of the sprite's height above the ellipse; that is invisible on a busy floor and
    # bands into a visible rectangle on a flat one, which is the "grey box around the
    # character" defect this whole library is trying not to reproduce.
    layer = layer.point(lambda a: 0 if a * opacity < 6 * 255 else int(a * opacity / 255.0))
    out = Image.new("RGBA", (W2, H2), (0, 0, 0, 0))
    out.paste(Image.new("RGBA", (W2, H2), (34, 30, 28, 255)), (0, 0), layer)
    out.alpha_composite(img, (pad_x, 0))
    bb = out.getbbox()
    return out.crop(bb) if bb else out


# ---------------------------------------------------------------------------
# EYE EXTRACTION — the enclosed-white-blob method from tools/find_cast.py
# ---------------------------------------------------------------------------
# find_cast.find() answers "how many characters are in this image" and is used below as
# an independent cross-check. It does not return the individual eye positions, which is
# what a blinking runtime needs, so the same walk is repeated here to return coordinates.
# Both of its hard-won fixes are carried over verbatim in behaviour:
#   1. NEVER ring-test from the eye's centre. The test walks outward from the four
#      MIDPOINTS OF THE BLOB'S EDGE; starting at the centre crosses the blob itself.
#   2. TOLERATE THE GREY BLUR BAND. Downscaling smears the white/ink boundary into greys;
#      aborting at the first non-white pixel found 1 eye out of 6. Walk up to 8 cells
#      outward and accept ink anywhere along the way.
import find_cast  # noqa: E402  (imported for the cross-check, and to keep one source of truth)


def _eye_blobs(px, w, h, ink_max=70, eye_min=210):
    ink = [[(px[x, y][0] < ink_max and px[x, y][1] < ink_max and px[x, y][2] < ink_max)
            for y in range(h)] for x in range(w)]
    white = [[(px[x, y][0] > eye_min and px[x, y][1] > eye_min and px[x, y][2] > eye_min)
              for y in range(h)] for x in range(w)]
    seen = [[False] * h for _ in range(w)]
    eyes = []
    for y in range(h):
        for x in range(w):
            if not white[x][y] or seen[x][y]:
                continue
            st = [(x, y)]; seen[x][y] = True; cells = []
            touches_edge = False
            while st:
                cx, cy = st.pop(); cells.append((cx, cy))
                if cx in (0, w - 1) or cy in (0, h - 1):
                    touches_edge = True
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = cx + dx, cy + dy
                    if 0 <= nx < w and 0 <= ny < h and white[nx][ny] and not seen[nx][ny]:
                        seen[nx][ny] = True; st.append((nx, ny))
            if touches_edge or not (3 <= len(cells) <= 400):
                continue
            xs = [c[0] for c in cells]; ys = [c[1] for c in cells]
            x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
            enclosed = 0
            for (sx, sy, dx, dy) in ((x0, (y0 + y1) // 2, -1, 0), (x1, (y0 + y1) // 2, 1, 0),
                                     ((x0 + x1) // 2, y0, 0, -1), ((x0 + x1) // 2, y1, 0, 1)):
                for step in range(1, 8):
                    nx, ny = sx + dx * step, sy + dy * step
                    if not (0 <= nx < w and 0 <= ny < h):
                        break
                    if ink[nx][ny]:
                        enclosed += 1; break
            if enclosed >= 3:
                eyes.append(((x0 + x1) / 2.0, (y0 + y1) / 2.0, len(cells), (x0, y0, x1, y1)))
    return eyes


def _ink_depth(px, w, h, blob, ink_max=70):
    """How THICK the ink is around a white blob, in downscaled cells.

    This is the test that separates an eye from a sneaker, and it was needed the moment
    the library met a pose the cast sprites never struck. In lie_hospital the figure is
    horizontal and its two cream sneakers fill the right of the frame; every white patch
    inside those shoes is a small blob fully enclosed by the shoe's own ink outline, so
    enclosure alone called them eyes — seven "faces" in a frame with one character, and a
    correct sprite thrown away for it.

    An eye sits inside the bean, so ink extends for tens of cells in every direction. A
    sneaker's highlight sits inside a drawn outline one to three cells thick with the
    world beyond it. Measured across three poses: real eyes score a median 6 to 27,
    sneaker and lace whites score 0 to 1."""
    x0, y0, x1, y1 = blob[3]
    mx, my = (x0 + x1) // 2, (y0 + y1) // 2
    ths = []
    for sx, sy, dx, dy in ((x0, my, -1, 0), (x1, my, 1, 0), (mx, y0, 0, -1), (mx, y1, 0, 1),
                           (x0, y0, -1, -1), (x1, y0, 1, -1), (x0, y1, -1, 1), (x1, y1, 1, 1)):
        t, started = 0, False
        for step in range(1, 40):
            nx, ny = sx + dx * step, sy + dy * step
            if not (0 <= nx < w and 0 <= ny < h):
                break
            hit = (px[nx, ny][0] < ink_max and px[nx, ny][1] < ink_max and px[nx, ny][2] < ink_max)
            if hit:
                started = True; t += 1
            elif started:
                break
            elif step > 10:            # tolerate the grey blur band, then give up
                break
        ths.append(t)
    ths.sort()
    return ths[len(ths) // 2]


def _pick_pair(blobs, depths, h=None):
    """The two eyes among the surviving candidates, or None.

    Once the shallow blobs are gone there are usually exactly two left and they ARE the
    eyes — which is what rescues lie_hospital, whose head is rotated a quarter turn so
    its eyes are stacked vertically and fail find_cast's level test outright. Only when
    three or more survive does the level-and-distance pairing have to arbitrate, and then
    the biggest believable pair wins, because eyes are the largest white on this
    character by construction."""
    keep = [(b, d) for b, d in zip(blobs, depths) if d >= 3]
    if len(keep) < 2:
        return None
    # Second line of defence, after ink depth: eyes are in the head and the head is never
    # in the bottom quarter of the figure, whereas cream sneakers always are. Applied
    # only when it still leaves a pair, so it can never break lie_hospital — whose head
    # is at the LEFT of a wide short sprite, not the top of a tall one.
    if h:
        upper = [k for k in keep if k[0][1] <= h * 0.72]
        if len(upper) >= 2:
            keep = upper
    if len(keep) == 2:
        (a, _), (b, _) = keep
        ratio = a[2] / max(b[2], 1)
        return (a, b) if 0.15 < ratio < 6.5 else None
    best = None
    for i in range(len(keep)):
        for j in range(i + 1, len(keep)):
            a, b = keep[i][0], keep[j][0]
            d = ((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2) ** 0.5
            ratio = a[2] / max(b[2], 1)
            if d < 30 and 0.15 < ratio < 6.5:
                score = a[2] + b[2] - d          # big blobs, close together
                if best is None or score > best[0]:
                    best = (score, a, b)
    return (best[1], best[2]) if best else None


def extract_eyes(img):
    """(eyes, blob_count, candidate_count). eyes is [[x,y],[x,y]] in the sprite's own
    full-resolution pixels, or [] when no clean pair was found.

    SCALE MATTERS AND IS NOT find_cast's DEFAULT. find_cast works on 1536x1024 SCENES at
    scale 4, where a character is a couple of hundred pixels tall. A pose sprite is the
    character alone filling ~1000px, so at scale 4 its eyes sit ~30 cells apart and the
    d<22 pairing test rejects them. Measured over the nine approved cast sprites, a scale
    that reduces the sprite to ~100 cells tall pairs 9 of 9; the ladder below tries that
    first and then loosens either way."""
    from PIL import Image
    W, H = img.size
    nblobs = ncand = 0
    for target in (100, 118, 86, 132):
        scale = max(2, int(round(H / float(target))))
        sw, sh = max(8, W // scale), max(8, H // scale)
        # composite over mid grey: transparent pixels keep their magenta RGB after keying,
        # and grey is neither ink nor white so it cannot be mistaken for either.
        flat = Image.new("RGB", img.size, (128, 128, 128))
        flat.paste(img, (0, 0), img)
        sm = flat.resize((sw, sh), Image.LANCZOS)
        px = sm.load()
        blobs = _eye_blobs(px, sw, sh)
        depths = [_ink_depth(px, sw, sh, b) for b in blobs]
        nblobs = max(nblobs, len(blobs))
        ncand = max(ncand, sum(1 for d in depths if d >= 3))
        got = _pick_pair(blobs, depths, sh)
        if got:
            pts = sorted([(got[0][0] * scale, got[0][1] * scale),
                          (got[1][0] * scale, got[1][1] * scale)])
            return [[int(round(x)), int(round(y))] for x, y in pts], nblobs, ncand
    return [], nblobs, ncand


# ---------------------------------------------------------------------------
# QUALITY GATE
# ---------------------------------------------------------------------------
# What the automated gate can and cannot see, stated honestly: it catches the mechanical
# failures (magenta fringe, off-palette colour, no character, a pupil inside an eye, a
# missing or spurious contact shadow, a degenerate crop). It cannot reliably see a drawn
# chair. So gen_poses.py also renders one contact sheet per character for a human read —
# 21 images instead of 525 — which is where clothing, chairs and mouths are caught.
_PAL_RGB = [(30, 30, 30), (232, 106, 92), (244, 185, 66), (143, 165, 130),
            (110, 140, 160), (242, 234, 211), (255, 255, 255)]


def _near_palette(r, g, b, tol=62):
    if abs(r - g) < 26 and abs(g - b) < 26 and abs(r - b) < 26:
        return True                       # any grey: ink, shadow, mic head, antialiasing
    for pr, pg, pb in _PAL_RGB:
        if abs(r - pr) + abs(g - pg) + abs(b - pb) < tol * 2:
            return True
    return False


def shadow_mass(img):
    """How much soft contact shadow sits under the figure, as a fraction of the bottom
    band's area.

    IT LOOKS AT ALPHA, NOT COLOUR, and that is the whole trick. A contact shadow on this
    library is never an opaque grey ellipse: baked, it is near-black ink at ~35 percent
    alpha; model-drawn on magenta, it is a darkened magenta that keying turns into a soft
    alpha gradient. A colour test finds neither — measured, greyness in the bottom band
    scored 0.003 on approved cast sprites whose shadow is plainly visible, and 0.000 on a
    freshly baked one. Partial alpha finds both, and separates hard: 0.52-0.84 for a
    sprite with a shadow against 0.005 for one without."""
    px = img.load(); W, H = img.size
    y0 = int(H * 0.85)
    soft = tot = 0
    for y in range(y0, H, 2):
        for x in range(0, W, 2):
            tot += 1
            if 18 < px[x, y][3] < 245:
                soft += 1
    return (soft / float(tot)) if tot else 0.0


def qc(img, pose):
    """Returns (ok, stats, reasons). `pose` is a POSES entry."""
    W, H = img.size
    reasons = []
    if H < 200 or W < 80 or W > 3 * H:
        reasons.append(f"degenerate crop {W}x{H}")
        return False, {"w": W, "h": H}, reasons
    # sample on a grid: 525 sprites x ~1M pixels is not worth a full pass
    step = max(1, int(math.sqrt(W * H / 90000.0)))
    px = img.load()
    opaque = fringe = ink = offpal = 0
    for y in range(0, H, step):
        for x in range(0, W, step):
            r, g, b, a = px[x, y]
            if a < 200:
                continue
            opaque += 1
            if min(r - g, b - g) > 30:
                fringe += 1
            if r < 70 and g < 70 and b < 70:
                ink += 1
            if not _near_palette(r, g, b):
                offpal += 1
    if not opaque:
        return False, {"w": W, "h": H}, ["empty after keying"]
    st = {"w": W, "h": H, "opaque": opaque,
          "fringe": round(fringe / opaque, 5),
          "ink": round(ink / opaque, 4),
          "offpal": round(offpal / opaque, 4),
          "shadow": round(shadow_mass(img), 4)}
    if st["fringe"] > 0.012:
        reasons.append(f"magenta fringe {st['fringe']:.3f}")
    if st["ink"] < 0.28:
        reasons.append(f"body not a solid ink mass (ink {st['ink']:.2f})")
    if st["offpal"] > 0.035:
        reasons.append(f"off-palette colour {st['offpal']:.3f} (clothing?)")
    # The standing case audits bake_shadow(), not the model: if this ever fires, the
    # ellipse failed to find the shoes. The seated case still watches the model, which
    # occasionally paints a wash under a chair it was told not to draw.
    if pose["shadow"] and st["shadow"] < 0.25:
        reasons.append(f"contact shadow missing after bake (soft-alpha {st['shadow']:.3f})")
    if not pose["shadow"] and st["shadow"] > 0.22:
        reasons.append(f"shadow under a pose the furniture grounds ({st['shadow']:.3f})")
    return (not reasons), st, reasons


def pupil_check(img, eyes):
    """A blank oval has nothing dark in its middle. Sample the inner 45% of each eye's
    neighbourhood at full resolution; a drawn pupil or iris shows up as a dark cluster."""
    if not eyes:
        return True, 0.0
    px = img.load(); W, H = img.size
    worst = 0.0
    for ex, ey in eyes:
        rad = max(3, int(min(W, H) * 0.012))
        dark = tot = 0
        for y in range(max(0, ey - rad), min(H, ey + rad + 1)):
            for x in range(max(0, ex - rad), min(W, ex + rad + 1)):
                r, g, b, a = px[x, y]
                if a < 200:
                    continue
                tot += 1
                if r < 90 and g < 90 and b < 90:
                    dark += 1
        if tot:
            worst = max(worst, dark / tot)
    return worst < 0.34, round(worst, 3)


def ground_px(img):
    """The ground contact point in sprite pixels: the horizontal centre of the lowest
    slice of opaque pixels, at the bottom of the crop. For a standing pose that is the
    middle of the contact shadow; for a seated pose it is where the sneakers rest."""
    px = img.load(); W, H = img.size
    band = max(2, int(H * 0.06))
    xs = [x for y in range(H - band, H) for x in range(0, W, 2) if px[x, y][3] > 120]
    return [int(sum(xs) / len(xs)) if xs else W // 2, H - 1]


def seat_px(img, frac):
    """The seat contact point: where the underside of the bottom meets the chair.

    The prompt puts the seat line at a known fraction of the figure's height, so the row
    is arithmetic; only the column has to be read off the image. It is the horizontal
    centre of the body mass ON that row, which for a side-profile figure is the middle of
    the part actually resting on the seat — not the middle of the bounding box, which the
    reaching arms and the props drag sideways."""
    px = img.load(); W, H = img.size
    y = max(0, min(H - 1, int(round(H * (1.0 - frac)))))
    band = max(2, int(H * 0.04))
    xs = [x for yy in range(max(0, y - band), min(H, y + band + 1)) for x in range(0, W, 2)
          if px[x, yy][3] > 160]
    return [int(sum(xs) / len(xs)) if xs else W // 2, y]


def anchor_px(img, pose=None):
    """The point the assembler pins to a slot. Feet poses pin at the floor; seated and
    lying poses pin at the seat line, which is the whole reason the canonical geometry
    exists — a seated sprite pinned at its feet breaks on any chair but the one it was
    imagined on."""
    if pose and pose.get("anchor") == "seat":
        return seat_px(img, pose.get("seat_frac", SEAT_FRAC))
    return ground_px(img)


def write_meta(path, img, pose, eyes, extra=None):
    m = {"eyes": eyes, "anchor": pose["anchor"], "w": img.size[0], "h": img.size[1]}
    m.update({"pose": pose["id"], "face": pose["face"], "shadow": pose["shadow"],
              "anchor_px": anchor_px(img, pose), "ground_px": ground_px(img)})
    if pose["anchor"] == "seat":
        m["seat_frac"] = pose.get("seat_frac", SEAT_FRAC)
    if extra:
        m.update(extra)
    with open(path, "w") as f:
        json.dump(m, f, separators=(",", ":"), sort_keys=True)
    return m


if __name__ == "__main__":
    import sys
    if len(sys.argv) > 1 and sys.argv[1] == "prompt":
        print(pose_prompt(sys.argv[2], sys.argv[3]))
    elif len(sys.argv) > 1 and sys.argv[1] == "canonical":
        print(canonical_prompt(sys.argv[2]))
    else:
        print(f"{len(CHARACTERS)} characters ({len(CAST)} cast + {len(EXTERNALS)} externals) "
              f"x {len(POSES)} poses = {len(CHARACTERS) * len(POSES)} sprites")
