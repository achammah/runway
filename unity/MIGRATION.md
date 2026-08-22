# RUNWAY! — Unity migration (alternative build)

Goal: a faithful Unity 6 (URP-less, built-in 2D) port of the Godot game for an
owner-run side-by-side. Same assets, same prompts, same engine math, same
drawn look. The Godot build remains the primary until the owner decides.

## Architecture (mirrors the Godot original)
- Runway.Core (Assets/Scripts/Core): PURE C# — no UnityEngine. SimEngine,
  WorldGen, GameState, Rng. Deterministic; the seeded RNG builds the SAME
  "seed:week:salt" key strings the GDScript builds, then runs them through a
  64-bit FNV-1a into a xorshift64* (Godot's hash() and PCG32 are engine
  internals and cannot be reproduced outside Godot). Streams are therefore
  identically STRUCTURED and fully deterministic, but numerically different
  from the Godot build: same laws, different draws. Core does no file IO —
  the host supplies a Func<string,string> reader (Unity: StreamingAssets).
  Verified by the console test runner in Runway.Core.Tests (dotnet) replicating
  all 73 GDScript engine checks plus the 5-strategy balance run.
  Run: export PATH="$HOME/.dotnet:$PATH"; dotnet run --project unity/Runway.Core.Tests
- App (Assets/Scripts/App): bootstrap (Main.cs builds everything from code —
  the Godot game already constructs all UI programmatically, so the port
  keeps that: ONE scene, code-built screens), DrawnUI framework (cream
  paper cards, wobbled ink borders as runtime meshes/textures, Patrick Hand
  via TMP), Env (user keys file), BuildStamp.
- Screens (Assets/Scripts/Screens): StudioCard, Title (film frames), Keys,
  HowTo (sheet loops), Draft (7 pages), BookIntro, Birth, Garage, Journal,
  DiceRoll, Curtain, Binder (9 tabs incl. pricing/ledger).
- LLM (Assets/Scripts/LLM): LlmClient (UnityWebRequest, structured outputs,
  two-tier terra/luna), EventGenerator (context sandwich, clarify, sentinel),
  SceneDirector (middleware calls + watchdogs + warm renders + paint gate).

## Assets
Copied from game/: fonts, sprites, dice sheets, title layers/video frames,
howto/birth/curtain sheets, journal icons, sfx, music. PNG policy matches the
main repo: generated art stays untracked.

## Build
macOS universal via Unity CLI batchmode once the editor + license exist:
  "$UNITY" -batchmode -quit -projectPath unity -buildTarget OSXUniversal \
     -executeMethod Runway.Build.BuildMac
License note: Unity Personal requires one interactive Unity Hub sign-in on
this machine (owner action) before CLI builds run.
