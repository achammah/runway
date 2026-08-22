# D3 — Beat text (ink-settle typewriter)
Checklist: D3a-c. The beat's words arrive like ink settling.
BUILD: NEW `Screens/BeatScreen.TextFx.cs` (partial; the beat screen class
name is in the N1 report — check Game/ for the loading/beat screen) exposing
Apply(TMP_Text): per-character reveal 40 chars/s, each char fades in with a
2px downward settle (TMP_Text mesh vertex animation via TMP_TextInfo), click
reveals all. Verdict words (BRILLIANT/IT LANDS/MIXED/BACKFIRES set) get a
one-time 1.06→1.0 scale settle. Inline die glyph: build a TMP_SpriteAsset at
runtime from Assets/Art/dice cell (die face N) and swap "die came up N" to
include <sprite>; if runtime sprite-asset creation is risky, ledger it and
ship the glyph as a positioned RawImage inline fallback. VERIFY: film 4
frames + final; save to scratchpad. 100% = reading feels authored; no GC
per frame (cache meshInfo); kill-switch clean.
