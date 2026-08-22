# D1 — Line-boil (living ink)
Checklist: D1a-c. The hand-drawn linework must BOIL like animated sketch
frames: every ink border/rule/ring wobbles between 2-3 variants at 8fps.
BUILD: DrawnUI already bakes wobbled ink to cached textures per (size, style,
seed) — extend via NEW file `App/DrawnUI.Boil.cs` (partial): bake 2 extra
variants per key (seed+1, seed+2) and add a lightweight `InkBoil` component
that swaps the Image/RawImage texture among the 3 on a shared 8fps clock
(one static clock, no per-instance Update). Wire-in point (report, don't
edit): DrawnUI's bake return site adds the component when RUNWAY_FX_BOIL set.
CONSTRAINTS: text glyphs NEVER boil; amplitude comes from the existing
wobble (≤1.5px); zero per-frame allocation; shared clock ticks only when any
InkBoil is visible. VERIFY: harness scene with 6 kit pieces, capture 3 frames
1/8s apart → edges differ, fills identical; save strip to scratchpad.
100% = the whole UI reads alive-but-calm; toggling the define kills it clean.
