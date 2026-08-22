# D5 — Particles (drawn air)
Checklist: D5a-c. Three systems, all code-built, all in the drawn hand:
1) Motes: ≤40 soft dots drifting in the select/garage light cones.
2) Scraps: LOCK-IN burst — 6-10 paper scraps (tiny cream quads w/ ink edge)
   tumbling with gravity, 0.8s life.
3) Embers: the title's runway fire sheds 8-12 rising embers.
BUILD: NEW `Effects/Motes.cs`, `Effects/Scraps.cs`, `Effects/Embers.cs` using
Unity's ParticleSystem configured from code (no prefabs) + one generated
4-sprite sheet (dot, scrap, ember, blur) via DrawnUI rasteriser. Hooks
reported, not edited. VERIFY: three screenshots to scratchpad; counts within
budget; zero allocations after warmup. 100% = air feels inhabited; nothing
steals attention; kill-switch clean.
