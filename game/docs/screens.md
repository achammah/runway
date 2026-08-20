# RUNWAY! — Screen → State → Capture map (F2)

Autopilot: `rm -rf <dir>; RUNWAY_SHOT=<dir> godot --path game > log 2>&1` (~2.5 min, 21 PNGs, exits itself). SCRIPT ERROR count in the log must be 0.

| PNG | Screen | State shown | Owner lane |
|---|---|---|---|
| 01_title | Title | video loop, press-any-key | LANE-FLOW |
| 02_select | Founder select | default selection (Hacker) | LANE-FLOW |
| 03_select_consultant | Founder select | Ex-Consultant selected (hero swap, pips) | LANE-FLOW |
| 04_name | Name ceremony | witness founder + idea machine | LANE-FLOW |
| 04b_shape | The Shape | WHAT/WHO cards, defaults picked | LANE-FLOW |
| 05_crew | The Crew | 2 cofounders incl. a trap (5% no-vesting idea friend) | LANE-FLOW |
| 06_recruit | Recruit modal | 4 candidates | LANE-FLOW |
| 07_money | First money | angel selected, preview line | LANE-FLOW |
| 08_bag | Pack your bag | 3 items packed, detail panel | LANE-FLOW |
| 09_journal | Journal p1 | week-1 consequences (day one) | LANE-GARAGE |
| 09b_consequences_real | Journal p1 | real adjudicated outcome (verdict/narration/reality) | LANE-GARAGE |
| 10_garage | Garage room | steady state, crew present | LANE-GARAGE |
| 10b_room_item_note | Garage room | item clicked → paper note | LANE-GARAGE |
| 10c_room_in_the_red | Garage room | cash<0: red vignette, red money tag | LANE-GARAGE |
| 11_decision_quiet | Journal p2 | portraits, pips, gestures | LANE-GARAGE |
| 12_decision_event | Journal p3 | assignment desk, presets+free inputs | LANE-GARAGE |
| 12_decision_event (same surface) | Journal p4 | event full-bleed | LANE-GARAGE |
| 13_decision_written | Journal p5 | nothing chosen | LANE-GARAGE |
| 13b_decision_ready | Journal p5 | choice+gesture+work pending (ready to lock) | LANE-GARAGE |
| 13c_pivot | Pivot panel | axes, costs, fun fact | LANE-GARAGE |
| 14_autopsy | The Last Page | forced death, estate, causal chain | LANE-FLOW |

## Full run — `RUNWAY_FULLRUN=<dir> godot --path game`

Plays a REAL run through the real screens (draft picks, weekly journal locks, era
promotions, the ending) instead of walking fixed states. Slower than the autopilot and
its week numbers vary with the seed, so filenames carry the week they were taken.
A run reaches hq in roughly 25-30 weeks. SCRIPT ERROR count in the log must be 0, and
the last line reports the eras reached:

`FULLRUN DONE: weeks=27 era=hq cash=878530 dead=false shots=14 eras=garage, coworking, office, floor, hq`

| PNG | Screen | State shown | Owner lane |
|---|---|---|---|
| wk&lt;NN&gt;_&lt;era&gt; | Garage room | the room every 5th week | LANE-GARAGE |
| move_&lt;era&gt;_wk&lt;NN&gt; | The Move | the era beat as it fires in a run | LANE-WIRING |
| era_&lt;era&gt;_wk&lt;NN&gt; | Garage room | the room the first week each era is standing | LANE-GARAGE |
| final_&lt;alive\|dead&gt;_wk&lt;NN&gt; | The Exit / Last Page | however the run actually ended | LANE-WIRING |

## Era + ending screens — `RUNWAY_LANEWIRE=<dir> godot --path game`

Renders the two beats that a run only reaches after many weeks, against a synthetic
company, so they can be reviewed in seconds. Exits itself, 5 PNGs.

| PNG | Screen | State shown | Owner lane |
|---|---|---|---|
| move_up_coworking | The Move | garage → coworking, rent ×4, 2 desks gained | LANE-WIRING |
| move_up_office | The Move | coworking → office, rent ×5, 5 desks gained | LANE-WIRING |
| move_down_coworking | The Move | demotion: colder light, 5 desks crossed out | LANE-WIRING |
| finale_acquisition | The Exit | SOLD, 5 style multipliers | LANE-WIRING |
| finale_ipo | The Exit | the bell rung, 4 style multipliers | LANE-WIRING |

Not yet in any capture mode: YC screens, investor/deal screens, deaths gallery, settings.

## Capture modes added since
`RUNWAY_TURN=<dir>` renders one full generative turn (reading beat opening, mid-read,
the composed room, and the beat after a dead render). `RUNWAY_TURN_ART=1` makes the
render real (1-3 min, paid). `RUNWAY_READING=<dir>` renders the reading beat alone.

The era rooms DO change with the era (QA-confirmed: five visually distinct sets);
the room is composed by SceneDirector or assembled by SceneStage, with the empty
stage as fallback.
