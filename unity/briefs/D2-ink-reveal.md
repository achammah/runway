# D2 — Ink-reveal (the room paints itself)
Checklist: D2a-c. When a generated room lands, it paints in with brush
strokes instead of fading.
BUILD: NEW `Effects/InkReveal.cs` + `GarageScreen.InkReveal.cs` (partial
hook Apply(RawImage roomImage, Texture2D newRoom)). Technique: a full-screen
mask texture built from 10-14 procedurally drawn brush-stroke rects/arcs
(reuse DrawnUI's rasteriser style), revealed stroke-by-stroke over ≤1.2s by
animating a cutoff in a tiny shader (one-property shader: _Cutoff vs a
per-pixel stroke-index texture) — or, if a custom shader is a compile risk,
CPU path: 12 pre-composited progressive PNbuffers at 128px mask res upscaled.
Click = snap to done. VERIFY: film 6 frames to scratchpad; strokes read as
painting, not wipe. 100% = feels like the world being drawn; skippable;
kill-switch clean.
