# D6 — Soft-light glows (NOT URP)
Checklist: D6a-c. Light as the game draws it: additive radial glows.
BUILD: NEW `Effects/GlowSprites.cs`: generated radial-gradient textures
(warm, cool) + additive Image layers: garage bulb pool (warm, breathes ±3%
over 4s), laptop glow on the founder desk, select-stage beam glow matched to
the regenerated stage art. In-the-red: a multiply overlay easing to 0.85
with a cold tint while cash<0 (reads state via the run driver snapshot —
report the hook). VERIFY: shots of garage normal vs red, select with glow;
save to scratchpad. 100% = rooms have temperature; red weeks FEEL cold;
kill-switch clean.
