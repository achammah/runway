# Integration hookups (N14) — collected as lanes land
Applied all at once at integration; each lane proven OFF-able first (D8).

## D2 ink-reveal — ACCEPTED (frames eyeballed: strokes, not a wipe)
- Hookup: GarageScreen.cs:362-364 inside AdoptComposed load callback —
  replace the 3-line fade (Group/alpha/FadeTo) with `GarageInk.Apply(_composed, tex);`
  (HideDrawnRoom(true) stays; 357-358 optional to remove — Apply sets both).
- Kill-switch: RUNWAY_FX_REVEAL=0 → InkReveal.Instant ≡ old three lines. Verified 3/3 by the lane.
- Note: GarageScreen is `sealed`, not partial — lane shipped static `GarageInk` seam
  instead; optionally add `partial` at integration (one word, line 31).
- Evidence: scratchpad/d2/ steps 0-10; pacing re-spaced to even %-per-frame
  (91→0% cream, ~8%/frame); 0.18s deaf window so the beat-closing click can't skip.
