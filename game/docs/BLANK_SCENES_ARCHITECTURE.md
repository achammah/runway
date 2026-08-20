# The Blank-Scenes Architecture

The owner's design, and it changes the runtime cost model to zero:

> "Much more thoughtful BLANK SCENES — backgrounds with spots available to add any
> cofounder doing something. All 500+ backgrounds transformed into blank scenes, 500+
> character poses pre-generated that fit the scenes, each blank scene deeply analysed
> and FULLY animated, each character type fully animated. Then the prompt only decides
> blank scene + characters, and the whole thing is assembled on the spot
> programmatically. Zero extra cost and instantaneous."

Everything expensive happens ONCE, offline. The runtime is a dictionary lookup and a
draw call. This document is the structure that makes it work for ANY situation — not
just chairs at desks — and the error-resilience contract.

---

## 1. The three pre-generated libraries

### A. BLANK SCENES (516 now → 1000+ by batches)
```
assets/backgrounds/<id>.png          the empty room (done: 516, verified empty)
assets/backgrounds/annotations.json  write_surfaces + marks (done)
assets/backgrounds/slots.json        NEW: typed slots per scene
assets/backgrounds/ambient/<id>/     NEW: additive light deltas per scene
```

### B. POSE LIBRARY (~500 sprites = 21 characters × 24 canonical poses)
A POSE is a body position + action, on magenta, keyed, with baked contact shadow for
standing poses. Facing is ONE side only; the engine flips. Identity comes from
`variant --ref` against each character's canonical sprite — the proven consistency
method.

The canonical 24, chosen to cover the whole life of a startup:

| sitting | standing | other |
|---|---|---|
| sit_desk_typing | stand_neutral | walk_stride |
| sit_desk_slumped | stand_phone | crouch_pack |
| sit_couch_relaxed | stand_present_pointer | lie_hospital |
| sit_couch_headinhands | stand_mic | sleep_desk |
| sit_audience_neutral | stand_handshake_L / _R | |
| sit_audience_clapping | stand_carrybox | |
| sit_bed | stand_wave_celebrate | |
| | stand_armscrossed | |
| | stand_reading_paper | |
| | stand_writing_clipboard | |
| | stand_point_accuse | |
| | stand_slumped | |
| | stand_coffee | |

**Mood is folded into pose choice, not multiplied against it.** A burnt-out cofounder
IS the one in sit_desk_slumped or sit_couch_headinhands. This kills the ×3 mood
multiplier that would have tripled the library.

THE CHARACTERS (21): the 9 cast (4 founder archetypes + 5 cofounder types) + 12
externals the stories need: vc_investor, angel_investor, employee, press_reporter,
customer_suit, customer_casual, lawyer, landlord, parent, partner, yc_partner,
official. Externals wear the world's props (lanyard, notepad, briefcase) — the
character law still holds: identity by props only.

Each pose ships with meta: `{eyes: [[x,y],[x,y]], anchor: "seat"|"feet", w, h}`.
Eye coordinates are extracted ONCE at import by the proven eye detector
(`tools/find_cast.py` logic), which is what makes blinking free at runtime.

### C. SLOTS (the interaction contract per scene)
```json
"legit_workspace__small_office__day_steady_wide": {
  "slots": [
    {"id":"desk_1","pose_class":"sit_desk","x":175,"y":585,"h":300,"face":"left",
     "occ":[0,470,460,720],"prominence":1},
    {"id":"board","pose_class":"stand_present","x":600,"y":560,"h":330,"face":"left",
     "occ":null,"prominence":2},
    {"id":"floor_1","pose_class":"stand","x":880,"y":760,"h":360,"face":"any",
     "occ":null,"prominence":3}
  ]
}
```
- `x,y` is the ANCHOR (seat point for sitting, foot point for standing), `h` the
  character height at that depth — depth scaling per slot, never uniform.
- `occ` is a rect of the SCENE ITSELF drawn over the character (a crop is aligned by
  construction — proven; no detection needed at runtime).
- `prominence` orders assignment: the founder takes the most prominent fitting slot.

## 2. The runtime (instantaneous, zero cost)

The DM's contract BARELY changes — it already emits `cast: [{who, mood, doing}]`.
The engine does the rest:

1. `doing` (free text) → pose class via a small synonym table
   ("calls investors" → stand_phone; "types all night" → sit_desk_typing;
   "despairs" → sit_couch_headinhands). Unknown verbs → stand_neutral. The DM never
   names a slot or a pose id, so there is NOTHING to hallucinate.
2. Deterministic slot assignment: sort cast by role (founder first), assign each the
   most prominent unoccupied slot whose pose_class matches (mood may override the
   pose within the class: burnt + sit_desk → sit_desk_slumped).
3. Draw: scene.png → for each placement: pose sprite, flipped if facing mismatches,
   scaled to slot.h, anchored at (x,y) → scene's own occ crops drawn back on top →
   ambient delta loop over everything (additive, black adds nothing).
4. Life, all in-engine and free: ambient deltas animate the room; characters breathe
   (scale tween from the anchor), blink (ink drawn over the stored eye coords),
   sway. A burnt character breathes slower.

## 3. Error resilience (the owner's explicit requirement)

Every lookup degrades, none breaks:
- unknown `doing` verb → stand_neutral
- no matching slot free → next-best class (stand fits anywhere); still none → the
  character is dropped from the frame, never mis-posed
- missing pose sprite → the character's canonical standing sprite
- missing ambient deltas → still scene (alive-ness lost, nothing broken)
- missing slots.json → the crew marks from annotations.json as stand slots
- missing scene → resolve() sibling; a true miss → novel-room generation (unchanged)
- every asset verified at import (IEND + decode), the gate that already exists

## 4. Why this reads organic when sprite-pasting did not

The owner rejected pasted sprites once. That failure had four causes, each addressed:
1. grey boxes around characters → magenta-dominance keying (measured fix)
2. floating → baked contact shadows + seat/foot anchoring at slot depth
3. wrong scale → per-slot h from the scene's own geometry
4. standing sprites plopped anywhere → poses DESIGNED for the slot's body position
   (a seated pose on the scene's own chair, occluded by the scene's own desk)
Plus a per-scene grade pass (multiply the pose layer toward the scene's palette) for
night/red rooms.

## 5. Scaling to 1000+ scenes (batches)

The taxonomy is the unit of growth. Next packs, each ~10-40 places ×variants:
- **YC pack**: interview room, group office hours, batch dinner, demo-day backstage,
  demo-day stage, YC lobby, office-hours whiteboard room, alumni party
- **Press & scandal pack**: TV studio desk, podcast booth, congressional hearing,
  courthouse steps, crisis-PR war room, 3am home office mid-twitterstorm
- **Crisis pack**: burning office, flooded garage, police raid, repo men taking the
  furniture, pawn shop, storage auction, eviction notice on the door, ER waiting
- **Milestone pack**: first office keys, ringing the bell, 1M-users cake, wired
  magazine cover shoot, yacht party, casino (gambling the runway), jail cell (fraud
  arc), Shenzhen factory floor, customs seizure, wedding altar missed for a demo
Same pipeline per pack: generate empty → verify empty → slots → ambient → ship.

## 6. The build order

1. PILOT (one scene, three poses, assembled + animated) — judged stringently first
2. L-POSES: the ~500-pose library (Opus lane)
3. L-SLOTS: typed slots for all 516, chair/couch/board detection + overlay review
4. L-ASSEMBLER: SceneStage runtime in GDScript + the activity→pose table
5. L-SCENE-LOOPS: one 4s loop per scene → ambient deltas (480p mini, ~$0.15/scene;
   516 ≈ $77 one-time) — era cores first
6. DM prompt nudge (PA): `doing` should use recognizable activity verbs
7. Batch packs toward 1000+


## 7. THE DERIVE PIPELINE (v2.1 — the integration fix, proven on a pilot)

The owner rejected the first assembled pilot: hand-authored slots on scenes generated
empty read as pasted. The fix inverts the order — **blank scenes are DERIVED from
populated ones**, so integration is correct by construction:

1. Generate the scene WITH characters in it (compose). The model integrates them
   natively: scale, perspective, lighting, occlusion all correct.
2. MEASURE the characters (ink-mass bounding boxes): their foot points and heights
   become the slots.
3. ERASE them (seedream edit: "remove every character, keep everything else
   identical"). The result is the blank scene — same furniture, same light.
4. THE RESIDENT POSE: each character is also CUT OUT as the pixel-difference between
   populated and blank. The cut carries its own contact shadow, and where furniture
   hid the body the cut has a furniture-shaped hole — so compositing it back
   reproduces the occlusion automatically, with zero occluder authoring.

Measured on the pilot (small office, three characters):
- resident poses: pixel-perfect by definition
- PORTABLE poses: standing/walking poses from the library port cleanly into measured
  stand slots (the presenter read near-identical to the model's own composition)
- seated/lying library poses do NOT port on foot-anchoring — chair geometry varies.
  Seated slots therefore use resident poses, or seat-anchored poses matched to a
  canonical chair geometry class.

Cost per derived scene: 2 generations + 1 optional ambient loop (~$0.60 all-in),
still one-time. The deliverable at ~/Downloads/runway-one-scene shows every stage.


## 8. THE SPOT-PATCH MODEL (v3 — the owner's actual design, correcting v2)

The owner, after reviewing an assembly that pasted library sprites at slot
coordinates: "I needed full SCENES in which you had BUILT-IN SPOTS that are either
empty OR with people in it and we can swap empty/character in them and assemble the
scene like that. While what you have done is backgrounds and you just pasted on top
random characters."

The correction: a scene's spots are not coordinates to paste at — they are REGIONS
WITH RENDITIONS. Every rendition is cut from a native full-scene render of that same
scene, so integration is perfect by definition and swapping is choosing, not pasting.

Per scene:
```
scene/
  populated.png        the scene with all spots filled (native render)
  blank.png            the same scene, all spots empty (erase edit)
  spots.json           per spot: region box + available renditions
  patches/
    <spot>__empty.png          (implicit: the blank's pixels)
    <spot>__<character>.png    diff(populated_variant, blank) inside the region
  ambient/d_NN.png     light deltas over the blank
```

Growing a spot's cast: a REPLACE edit of the populated scene ("swap the presenter
for X, everything else identical") -> diff against the blank -> a new patch for the
same spot. One edit per (spot, character) pair, generated offline in batches for the
casts a scene will actually see.

Assembly at runtime: blank + chosen patch per spot + ambient deltas + in-engine life
(breathe/blink on the patch regions, eyes found by the detector). Instant and free,
and every pixel is in-scene.

Where the v2 libraries fit: the 699 blanks stay (they are the erase targets and the
fallback rooms), the slots become the spot region definitions, and the 525-pose
library serves as REFERENCES for replace edits and as distant background extras —
never as pasted foreground characters.
