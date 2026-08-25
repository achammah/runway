# The shipping DAG — one push, no phases

```
N0  contracts (this file + HOOKS.md)                      [done, coordinator]
N1a ENGINE SPINE  — one agent, owns ALL shared files      ──┐
N1b UI SPINE      — one agent, after N1a (bank tab shell,   │ sequential
                    pre-roll review, desk dispatch)       ──┘
N2  NINE LANES in parallel — each writes ONLY its own
    lane/desk/test files (stubs planted by N1a/N1b):
      L1 catalog · L2 labor · L3 rivals+macro · L4 funnel
      L5 pipeline · L6 finance+bank · L7 roadmap
      L8 board+M&A · L9 hardware
N4a DM-PROMPTS agent — sole owner of prompt files, after N2
N4b QA LOOP — after N2, iterate until standard (owner mandate):
    extend both shot harnesses to cover EVERY new surface (all 10 binder
    tabs per era-relevant state, the pre-roll review card, review cards,
    applicant/bet/term cards, pipeline board, bench, ticker, coach step 4,
    empty/WAIT/keyless states) → capture screenshots → assess each
    against docs/design/10-interface-language.md's 15-check gate + the
    game's visual bar → fix → re-shoot → repeat until every check passes
    on every surface, BOTH engines. Plus a live keyed probe pass
    (RUNWAY_UFLOW class) across the new desks and a post-wave perf soak.
    No new feature ships below the standard of the existing game.
N5  coordinator — arbitrage, integration seams, full gates
    (parse · compile · twin suites · save fixture · full-run smoke),
    verify N4b's screenshot verdicts myself, rebuild both apps + DMGs,
    log, ship
```

Rules of the DAG:
- **Zero shared-file writes in N2.** The spine plants every call site and
  stub; a lane that believes it must edit a shared file is BLOCKED and
  must message the coordinator instead of improvising.
- Every node lands BOTH engines together, twin-tested, or reports
  blocked. The specs in docs/design/ are the single source of truth;
  DECISIONS.md arbitrations are binding.
- Agents message the coordinator when a contract is ambiguous; the
  coordinator arbitrates and updates HOOKS.md.
```
