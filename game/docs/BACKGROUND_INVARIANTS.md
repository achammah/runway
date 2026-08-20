# Background invariants — what 516 generated rooms must agree on

For the cast, the crew marks, the occluders and the write_surfaces to stay valid
across a generated library, the backgrounds have to agree on *something*. This
is the measured answer to what that something is, and — more usefully — what it
is **not**.

## The headline: the camera cannot be held by prompt

Three pilot backgrounds were generated with this clause stated explicitly:

> the line where the back wall meets the floor runs straight across the picture
> at 62 percent of the image height

Measured result (`tools/measure_ground.py`):

| background | ground line | asked for | obeyed |
|---|---|---|---|
| pilot_hangar | 0.621 | 0.62 | yes |
| pilot_livingroom | 0.648 | 0.62 | close |
| pilot_airport | 0.516 | 0.62 | **no** |

And the seven hand-built stages, for scale of the problem:

| stage | ground line |
|---|---|
| stage_hq | 0.449 |
| stage_floor | 0.488 |
| stage_coworking | 0.547 |
| stage_garage / stage_yc | 0.574 |
| stage_office / stage_nasdaq | 0.609 |

A spread of 0.449–0.648 is **204px on a 1024 canvas**. A fixed set of crew marks
would put feet through the floor in half the library.

**So do not ship a fixed mark set, and do not spend prompt budget demanding an
exact camera.** One in three obeyed a number stated as plainly as it can be.

## The invariant is a RECIPE, not a coordinate

What every background must contain — and what is then *measured* off it:

1. **A findable ground line.** The wall/floor junction must be the strongest
   horizontal in the lower half of the frame. Rooms with no visible junction
   (pure white void, extreme close-up) break everything downstream.
2. **An unobstructed floor band below it** at least 0.17 of image height deep,
   spanning most of the width, where figures can stand.
3. **Furniture standing ON that floor**, so occluders can be cut from it.
4. **The UI safe zones** (top 10%, bottom 14%, centre-bottom, side rails).
5. **Four blank writable surfaces**, spread rather than clustered.

`tools/auto_marks.py` then derives the marks per background: measure the ground
line, score the floor band for clutter, pick five quiet columns, lay them in a
shallow arc, and scale each by its depth. Verified on all three pilots — a
suburban living room, a disused hangar and an airport gate, none of which the
mark system was designed around — with a real five-character crew composited at
the derived marks. Feet land on the floor and depth reads correctly in all three.

## Holdable by prompt vs enforced after the fact

This is the distinction that matters for the generation run.

### Holdable by prompt — the model reliably obeys

- **Blank writable surfaces.** All three pilots came back with four or more
  (whiteboard, clipboard, pinned sheet, sticky cluster, framed board). The STYLE
  clause works.
- **Flat hand-drawn style and the palette.** Consistent across all pilots.
- **Wide establishing framing**, "the whole room".
- **A large clear floor area**, when asked for plainly.
- **Empty of people** — *conditionally*, see the bug below.

### Enforced after the fact — the model cannot be trusted with these

- **The ground line and camera height.** 1 of 3 obeyed. Measure it
  (`measure_ground.py`) and derive marks from the measurement (`auto_marks.py`).
  Never assume it.
- **Blankness of a writable surface.** The model draws a whiteboard and then
  scribbles a diagram on it. `clear_surfaces` already handles this, and it is
  the model for everything in this list: ask, then verify, then repair in place.
- **UI safe-zone compliance.** `zone_audit.py`. Fresh generation is much better
  than edits here, but it still fails often enough to need the gate.
- **Character fidelity.** Not applicable now that the cast composites as
  sprites — which is exactly why that architecture is the right one. Every room
  that drew its own crew drew them wrong (pupils, mouths, hoodies).
- **File integrity.** `scene_pipeline.py verify`.

### A taxonomy bug found by the pilot

`pilot_livingroom` came back **with a creature sitting on the sofa** despite
`EMPTY OF PEOPLE` in the prompt. The cause is in the taxonomy text itself:

> "a suburban living room with a floral sofa, **a laptop balanced on a knee**"

A knee implies a person, and the description outweighs the instruction. Several
`PLACES` entries describe a room by what someone is doing in it. Those need
rewording to describe the *place* only — "a floral sofa with a laptop left open
on the cushion" — or the library will ship rooms with uninvited occupants that
then double against the composited cast.

## The two detectors, measured against hand-authored ground truth

Scored against the faces and occluders I annotated by hand on the seven stages,
matched by IoU at a 0.5 threshold.

### write_surfaces detector — READY

`tools/detect_surfaces.py --eval`

| metric | value |
|---|---|
| recall | **18/25 = 72%** |
| recall on faces above the two-line size floor | **18/20 = 90%** |
| mean IoU | 0.64 |
| false positives across 7 rooms | 3 |

Five of the seven misses are `sticky` — three overlapping notes, each
individually below the 58px needed for two lines of 26px type. The detector is
being *stricter than I was by hand*, which is the correct direction.

Two things mattered more than any threshold:

- **Working resolution.** At 384px a 2px felt-pen outline averages away, the
  face leaks into the wall through the gap, and the merged region touches the
  image border and is discarded. Recall was **20%** for that reason alone; at
  768px with a gradient-aware barrier it is 72%.
- **A size cap.** The floor is also pale, flat and rectangular, and in the garage
  a dark line along the bottom edge sealed it, so it returned as one enormous
  "face". Capping span at two thirds of the width cut false positives from 9 to 3.

### occluder detector — NOT READY, and its errors are dangerous

`tools/detect_occluders.py --eval`

| metric | value |
|---|---|
| recall | **3/24 = 12%** |
| mean IoU | 0.23 |

Horizontal localisation actually works — proposals matched `occ_desk_left`
(x196-452 against a true x205-435) and `occ_desk_right` closely. What fails is
vertical extent and completeness.

**But the geometric score is not the reason to hold it back.** Because an
occluder is a crop of the scene, an over-tall rect is harmless: it redraws
identical pixels. An over-WIDE one is not. Measured by compositing the cast and
then the proposals over three rooms:

| room | crew marks erased outright |
|---|---|
| stage_office | crew_1 (100% covered) |
| stage_garage | crew_4 (100%) |
| stage_floor | crew_1 (75%), crew_4 (81%) |

**4 of 15 crew marks deleted.** A false positive in the surface detector wastes
an annotation; a false positive here removes a character from the scene. The
failure is silent — the room still looks fine, there is simply one fewer
founder in it.

**Recommendation for the batch: run the surface detector automatically, and do
not auto-apply occluders.** Occlusion is a quality nicety, not a requirement —
the functional test shows a cast standing on open floor reads perfectly well.
Ship the 516 without occluders, and add them later behind a check that no
proposal covers more than about a quarter of any crew mark.

## What is still hand-authored, and what that costs

- **Occluders.** Currently cut from a hand-authored rect. The cut itself is
  cheap and exact (a crop of the scene aligns by construction), but *which*
  rectangle to cut is still a human judgement. For 516 rooms this needs the same
  treatment as marks: detect furniture masses that sit in front of the standable
  band and cut those. Not built yet.
- **write_surfaces rects.** Same: currently measured by eye per room. Detecting
  a pale quadrilateral bounded by a drawn frame is very doable and is the
  natural next tool.

Both are the difference between a library that costs one person-day per room and
one that costs nothing per room.


## THE CHARACTER LAW INVITES CHARACTERS (measured, 2026-08-20)

The shared `STYLE` block ends with ~200 words of CHARACTER LAW describing the crew, so
that a SCENE draws them on-model. Appended to a room prompt whose only counter-instruction
is "EMPTY OF PEOPLE", that description INVITES them in. One of the first two smoke-test
rooms came back with three creatures sitting in it.

Cutting the clause was necessary but NOT sufficient: **roughly 1 room in 12 still arrived
occupied**. This is the same failure class as "a laptop balanced on a knee" summoning a body.

So emptiness is ENFORCED, not requested, exactly as `clear_surfaces` enforces blankness:
threshold the ink, erode the felt-pen outlines so only filled bodies survive, label the
components, and call one a creature only when a blank white oval eye sits fully inside it.
A dark TV has no white holes; a whiteboard has no ink surround. 37 of 516 rooms were caught
and re-rolled on that test alone.

`tools/scene_pipeline.py` now exports `STYLE_EMPTY` for this: palette, UI safe zones and
writing-surface rules kept verbatim, character law cut, emptiness clause in its place.
**Any lane generating a room the cast will be composited into must use `STYLE_EMPTY`, never
`STYLE`** — a person painted into a background doubles against the composited cast, which
is a defect the owner has already photographed once.
