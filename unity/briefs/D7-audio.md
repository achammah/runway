# D7 — Audio states (the mix breathes)
Checklist: D7a-b. The mix reacts to the game's states.
BUILD: an AudioMixer asset can't be authored headless-safely → build the
equivalent in code: NEW `Audio/RunwayMix.cs` — central gain/filter controller
over the app's AudioSources: Duck(0.5, 300ms) while the curtain is shut,
LowPass (via AudioLowPassFilter component) while the binder is open, a thin
red-week filter (LPF 2400Hz + −3dB) while cash<0. Snapshot pattern: named
states, 0.3s lerps, one Update driver. Hooks reported. VERIFY: state-change
log lines + a scripted run toggling all three (log to scratchpad); note any
inaudible-in-headless caveat honestly. 100% = the game sounds like it knows
what is happening; kill-switch clean.
