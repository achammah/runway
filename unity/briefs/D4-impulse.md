# D4 — Impulse (weight)
Checklist: D4a-c. Consequence has physical weight; restraint is the law.
BUILD: NEW `Effects/Impulse.cs`: a hand-rolled spring (no packages) offsetting
the Stage RectTransform: Impulse.Shake(px, ms, dir) and Impulse.Punch(scale,
ms). Hooks (report for integration): backfired verdict → Shake(6px, 250ms);
die settled → Punch(1.02, 120ms). NOTHING on fine/brilliant.
VERIFY: harness triggers each once, film 4 frames each; assert stage returns
EXACTLY to rest (position delta 0 after settle). 100% = felt, never noticed;
kill-switch clean.
