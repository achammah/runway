# RUNWAY! — the generative architecture

The concept, in the owner's words: *the first truly generative game of this genre. You have
the situation, you allocate resources and make decisions and can FREE TEXT into the
decisions. Then at each step you have the actual consequences AND the full scene generated
on the spot, with simple animation of the characters, and the full consequences.*

This document is the feasibility assessment and the architecture that follows from it.
Every latency number below was measured on this machine, not estimated.

---

## 1. The measured constraint

One background generation, 1536×1024, same prompt, same style block:

| model | latency | verdict |
|---|---|---|
| google/nano-banana-2-lite | **7.4s** | too low quality |
| microsoft/mai-image-2.5-flash | 16.8s | too low quality |
| google/nano-banana | 23.5s | too low quality |
| **openai GPT Image 2, `quality: low`** | **28.0s** | **CHOSEN — best tradeoff** |
| openai GPT Image 2, `quality: medium` | 48.2s | slower, not enough better |
| bytedance/seedream-v5.0-lite | 91.8s | not the fast model the name suggests |
| bytedance/seedream-v5.0-pro | 106.8s | best obedience, too slow per turn |
| openai GPT Image 2, `quality: high` | 150.0s | reference quality only |

Rejected on API grounds: `krea-2-turbo` and `ideogram/v4/turbo` (price evaluation needs an
`image_size` shape we do not pass), `gpt-image-1.5` and `gpt-image-1-mini` via Atlas (400),
`ERNIE-Image-Turbo` (500). `seedream-v5.0-lite` exists but has **no** `/text-to-image`
suffix — the bare model id is the text-to-image entry point.

One observation worth keeping: at the same prompt, **seedream obeyed "empty of people" and
GPT did not**. GPT drew characters into a room the prompt explicitly asked to be empty.
That matters, because the cast is composited, not generated (§4), so the background must
come back empty. Expect to enforce this after the fact rather than by asking nicely.

**28 seconds is the number the design has to live with.**

---

## 2. Why 28s is enough: generate N+1 during turn N

A turn is not a click. The player reads the consequence chain, looks at the room, allocates
the week, and writes a free-text move. That is **60 to 180 seconds of human time**.

So the scene for week N+1 is generated *while the player is still spending week N*:

```
week N locks
  └─> DM resolves consequences            ~3-6s   (LLM, text)
  └─> DM emits the variable image prompt  (same call, same JSON)
  └─> background generation starts        28s     ── in the background
  player reads the consequence chain      15-40s
  player allocates and writes the move    30-120s
  player locks week N+1
  └─> the background is ALREADY THERE
```

28s fits inside the shortest plausible turn with margin. The generation is never on the
critical path, and the player never waits on a spinner. If a turn does come in faster than
the render, the dread beat between weeks is the natural place to absorb the remainder —
60 Seconds! already uses that beat for tension, so it costs nothing narratively.

**Cost:** GPT Image 2 at `low` is roughly $0.02 per image. A 30-week run is about **$0.60**,
a full 78-week run about **$1.60**. Cost is not the constraint. Latency was, and 28s solves it.

---

## 3. The prompt is fixed + variable

The generation prompt has two halves. The fixed half is the game's identity and never
changes. The variable half is authored by the DM every week from the state and the
player's decision.

### FIXED (already exists as `STYLE` in `tools/scene_pipeline.py`)
- palette, wobbly felt-pen linework, flat fills, no gradients
- UI safe zones: calm top band, calm bottom band, empty centre-bottom for the CTA
- the character law (only matters if a creature slips in; it should not)
- the writing-surfaces requirement: five blank faces including the inventory board
- **camera and framing invariants** — the piece that does not exist yet and is required:
  a consistent floor line, a consistent camera height and distance, and known wall bands.
  Without these, crew marks and occluders authored for one room are invalid for the next
  generated one, and the composited cast will float or sink. LANE-SCENES is deriving these
  from the stages already built.

### VARIABLE (emitted by the DM each week)
- **the room**: era, and how the player's decisions have changed it
- **the state objects**: how much money is physically present, what the product looks like
  now, what evidence of customers exists, how much decay
- **the mood**: thriving / steady / in-the-red
- **the evidence of the last decision** — the most important field. If the player wrote
  "I sleep in the office to ship faster", next week's room has a sleeping bag under the
  desk. This is what makes the game feel like it is listening.

The DM returns the variable half as structured fields, not free prose, so the fixed half
can never be overridden and a hallucinated instruction cannot break the safe zones.

---

## 4. The cast is composited, never generated

This is what makes 28s sufficient. Only the **background** is generated. On top of it:

- 27 cast sprites already exist (4 founder archetypes × 5 cofounder types × fine/burnt/gone)
- 25 crew marks with per-mark scale, and 20 occluders, already authored
- characters animate **in-engine** — breathe, blink, idle sway — which costs nothing and
  reacts to state, so a burnt-out cofounder breathes slower than a fresh one
- `SceneSurfaces` writes the run's numbers onto surfaces drawn into the room

So "the full scene generated on the spot" is: a generated background, dressed with a cast
that already exists, animated for free, and annotated with live state. The expensive part
is the only part that is generated.

**Video is not on the per-turn path.** A 4s loop costs 106s+ and cannot be produced per
week. Ambient loops stay pre-built per era; per-turn life comes from the in-engine tweens.

---

## 5. The DM layer

The LLM stops being an adjudicator and becomes a **dungeon master**. Per turn it holds:

- the run so far: decisions, written moves, consequences, the arc
- **the possible final states**, and how near the company is to each — this is the owner's
  requirement that the prompt "keep in mind potential final state of the game". A DM that
  does not know where the story can end cannot steer toward a meaningful one.
- the era ladder and what each era makes possible
- the traps armed at the draft and which have not yet fired

And per turn it emits, in one structured call:
1. the consequence chain for what just happened — said / heard / verdict / narration /
   reality check / effects
2. the next situation
3. the variable half of the image prompt (§3)
4. its private read of how close the run is to each ending, so the next turn can escalate

The op whitelist and era-scaled clamps stay: the DM narrates freely and mutates state only
through validated effects.

---

## 6. The log becomes free-text-first

If the written move is the product, the page has to stop treating it as an afterthought
below a list of buttons. The owner: *the log must be MUCH BETTER and oriented towards
having full free text.*

- the writing area is the **primary** element of the decision page, not a footnote under
  the presets
- presets become a fallback for a player who does not want to type, drawn smaller
- what you wrote last week is visible in the log as your own handwriting, so the book
  accumulates into a record of your reasoning
- the consequence chain quotes you verbatim, then shows how the world heard it — the gap
  between the two is the joke and the lesson

---

## 7. Risks, honestly

| risk | mitigation |
|---|---|
| Generated rooms drift off-model week to week | reference-locked edits against the era's approved stage, rather than fresh generates |
| The model draws characters into a room that must be empty | measured: GPT does this. Enforce after the fact, as `clear_surfaces` enforces blank surfaces |
| Crew marks invalid on a newly generated room | the framing invariants of §3; a room that violates them is regenerated, not patched |
| A turn shorter than 28s | the dread beat absorbs it; worst case the previous room persists one more week, which is diegetically fine |
| Generation fails mid-run | always fall back to the era's pre-built stage. The game must never block on a network call |
| Cost at scale | $0.60–$1.60 per run at `low`. Fine for a demo; revisit only if this ships broadly |

---

## 8. What this changes about work in flight

- **Stop** producing more fixed stage variants. The stages become the *reference set* that
  teaches the prompt what a RUNWAY! room looks like, not a catalogue to be exhausted.
- **Keep** every cast sprite, crew mark, occluder and write-surface. They are the fixed
  layer that every generated background gets dressed with, and they are what makes a 28s
  budget sufficient.
- **Add** the framing invariants, the DM prompt, and the prefetch pipeline.
