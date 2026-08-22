# Live-play visual defects (owner session, build 0191dab)
Standing rule: no self-launched game windows while the owner is playing.

- VD1 (#176) SELECT: the spotlit founder is MISSING — empty cone, only the
  candidate strip shows. Suspects: hero DraftLoop never fed / drawn behind
  the beam glow / Select(0) path still dead despite the deferral.
- VD2 (#177) BIRTH: "creating your world.." unreadable — the 4 halo copies
  are static while the ink line animates its dots → double-struck smear.
  Fix: ONE label on a soft dark pill (drop the halo stack).
- VD3 (#178) CURTAIN: the considering line floats naked — give it the drawn
  plaque treatment (cream chip + wobble edge) like every other label.
- VD4 (#179) DICE: backdrop is an ugly flat disc on black — Godot dims the
  JOURNAL PAGE behind the roll. Also the cup sheet load was killed (3.4s
  stream) → bake the 20 dice sheets like the films.
- VD5 (#180) GARAGE (drawn room): glow smears — white-yellow blobs on the
  cream wall (additive-over-cream risk D6-B1, possibly the alpha-wash
  fallback if the Resources shader stripped). Clamp alpha on cream rooms,
  pin the pool to the drawn bulb only, verify shader shipped.
