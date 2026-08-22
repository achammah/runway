# COMPILE-RISK LEDGER — App shell, screens and LLM layer

No Unity editor exists on this machine yet, so nothing here has been compiled. Every
API below is one I was not 100% certain of, with the fallback that was coded so a
wrong guess degrades instead of breaking. Ordered worst-consequence first.

Package baseline this was written against (`Packages/manifest.json`):
`com.unity.textmeshpro 3.0.9`, `com.unity.ugui 1.0.0`, `com.unity.nuget.newtonsoft-json 3.2.1`.

---

## A. Blocking risks — these stop a compile if they are wrong

| # | API | Where | Why uncertain | Fallback coded |
|---|-----|-------|---------------|----------------|
| A1 | `com.unity.textmeshpro` as a separate package | every screen | Unity 6 folded TextMeshPro into `com.unity.ugui 2.0.0` and drops the standalone package. If the editor upgrades the manifest, `TMPro` still resolves (same namespace, same types) but `com.unity.textmeshpro` may be removed from the manifest on first open. | Nothing in the code names the package; all types are `TMPro.*`, which exist in both. If the editor rewrites the manifest, let it. |
| A2 | `TMP_Text.enableWordWrapping` | `DrawnUI.HandLabel`, `PaperInput` | Marked `[Obsolete]` in TMP 3.2+/UGUI 2.0 in favour of `textWrappingMode`. Obsolete is a **warning**, not an error, and the property still functions. | Kept `enableWordWrapping` on purpose: it exists in both 3.0.9 and the newer merged TMP, while `textWrappingMode` does not exist in 3.0.9. If the project is later pinned to UGUI 2.0 only, swap it. |
| A3 | `TMP_InputField.onSubmit` | `PaperInput` | `onSubmit` was added in TMP 2.1; present in 3.x. If it is ever absent the compile breaks. | `onValueChanged` (universally present) carries the `Changed` event, and the keys screen's save button does not depend on submit. Swapping `onSubmit` → `onEndEdit` is a one-line change. |
| A4 | `StandaloneInputModule` | `Boot.BuildFurniture` | Throws at runtime (not compile) if the project's Active Input Handling is set to "Input System Package (New)". `ProjectSettings/` is currently empty, so Unity will generate the default (`activeInputHandler: 0`, legacy), which is what this needs. | Legacy `Input.*` is used everywhere (`Input.anyKeyDown`, `Input.GetKeyDown`). If the owner later adds the Input System package, set Active Input Handling to **Both**. |
| A5 | `UnityEditor.Build.Reporting.BuildReport` / `BuildSummary` | `Build.cs` | Namespace has been stable since 2018 but `Build.cs` is the one file that will not compile in a player build if a guard is wrong. | The **whole file** is inside `#if UNITY_EDITOR`, so a player build never sees it. |
| A6 | `UnityEditor.OSXStandalone.UserBuildSettings.architecture` | `Build.TryUniversalArchitecture` | Lives in an editor **module** assembly whose name has changed between versions; a direct reference can fail to resolve. | Reached entirely through `Type.GetType` + reflection inside `try/catch`. A miss loses the universal slice, never the build. |
| A7 | `Sprite.Create(tex, rect, pivot, ppu, extrude, meshType)` | `DrawnUI.Bake` | The `extrude` parameter is `uint`, and the 6-argument overload must exist. | Passed as `0u` with `SpriteMeshType.FullRect` explicitly, which is the long-stable overload. |
| A8 | `Image.fillMethod` / `fillOrigin` / `fillAmount` with `Image.Type.Filled` | `StudioCard` underline | Long stable, but `fillOrigin` is an `int` fed from `(int)Image.OriginHorizontal.Left`. | If Filled ever misbehaves, the underline can be animated by shrinking `sizeDelta.x` instead — same visual. |

## B. Runtime risks — they compile, but may not do what the Godot original does

| # | Thing | Where | Risk | Fallback coded |
|---|-------|-------|------|----------------|
| B1 | **Runtime TTF loading is impossible in Unity.** `new Font(name)` takes an OS font *name*, not a path. | `DrawnUI.Hand` | Patrick Hand cannot be read off `Assets/Art/fonts/` at runtime. | The `.ttf` was **copied into `Assets/Resources/Fonts/`** (152KB), and `DrawnUI.Hand` walks a ladder: a baked `Fonts/PatrickHand SDF` asset → `Resources.Load<Font>("Fonts/PatrickHand-Regular")` + `TMP_FontAsset.CreateFontAsset(font)` → `TMP_Settings.defaultFontAsset` → null (TMP draws in its own default). `FontBaker.cs` (`#if UNITY_EDITOR`, `[InitializeOnLoad]`) bakes the asset on first editor open so the runtime path is a plain load; every step is in `try/catch` and a failure is a log line. |
| B2 | **TMP Essential Resources may not be imported.** | all text | Without `TMP Settings` in Resources, TMP logs an error and text can render blank. This is an owner action, once: *Window ▸ TextMeshPro ▸ Import TMP Essential Resources*. | Every `t.font = ...` is guarded by `if (Hand != null)`; a null font asset falls back to TMP's own default rather than throwing. |
| B3 | `TMP_FontAsset.CreateFontAsset(Font)` needs the font's binary data | `DrawnUI.Hand`, `FontBaker` | Requires "Include Font Data" on the `.ttf` importer (**on** by default). | `try/catch` around the call; the ladder continues to the TMP default. |
| B4 | **Text metrics are not Godot metrics.** | `DrawnUI.MeasureWidth` (studio card underline, how-to NEXT card width) | `TMP_Text.GetPreferredValues` will not return the same number `Font.get_string_size` did, so two measured widths are *close*, not identical. | Everything else is laid out by explicit coordinates transcribed from the `.gd` files, so only these two widths float. Centred text uses a full-width rect + centre alignment rather than a measured offset, which is exact. |
| B5 | **Baselines vs top-left.** Godot's `draw_string` positions the BASELINE; `Label.position` is top-left. | `DrawnUI.InkString` | The baseline offset is approximated as `0.78 × size`. Text drawn with `draw_string` in the original may sit a few pixels off. | `HandLabel` (top-left, exact) is used everywhere the original used a `Label`; `InkString` is used only where the original used `draw_string`. |
| B6 | `Application.streamingAssetsPath` is not a plain path on every platform | `RunwayPaths` | On macOS it is a real directory, which is all this build targets. | Art is resolved by probing `StreamingAssets/Art` → `Application.dataPath/Art` (the editor's `Assets/Art`) → `../Assets/Art`, and every file is loaded through `UnityWebRequestTexture` with a `file://` URI built by `new Uri(path).AbsoluteUri` — **required**, because the project path contains spaces. |
| B7 | **The art is not copied into StreamingAssets yet** (300MB). | `Build.EnsureStreamingArt` | In the editor the probe finds `Assets/Art` directly, so nothing is duplicated during development. A *player* build needs it in StreamingAssets. | `Build.BuildMac` copies `Assets/Art` → `Assets/StreamingAssets/Art` (skipping files already newer) before `BuildPlayer`. Nothing is duplicated until someone builds. |
| B8 | `UnityWebRequest.Abort()` followed by `Dispose()` | `SceneDirector.MiddlewareCall` | Disposing a request the same frame it is aborted has been reported to be unsafe. | `Abort()` → `yield return null` (one frame) → `Dispose()`. |
| B9 | 5120×4608 textures (94MB each as RGBA32) | `SheetLoop` | Three how-to sheets at once is memory nobody needs — the original notes this. | Loaded with `nonReadable: true`; `HowToScreen` calls `Release()` before loading the next page; `Curtain` borrows its sway on close and gives it back on open; `SheetLoop.Release()` calls `StopAllCoroutines()` first so a load in flight cannot resurrect a released sheet. |
| B10 | `RawImage.uvRect` origin | `SheetLoop.CoverRect` | Unity's UV origin is **bottom-left**; every sheet in this game is laid out **top-left**. Getting this backwards plays the sheet upside down through the rows. | The row flip happens in exactly one place (`CoverRect`) and nowhere else; the ink rasteriser flips once in `DrawnUI.Bake`. If a loop plays its rows in the wrong order, that one line is the cause. |
| B11 | `CanvasScaler.ScreenMatchMode.Expand` vs Godot's `stretch/aspect="keep"` | `Boot` | Unity's scaler cannot letterbox. | A **fixed 1536×1024 `Stage` rect** is centred in the canvas and every screen is a child of it, so the letterbox is the camera's clear colour showing around the stage — and, more importantly, every Godot coordinate transcribes 1:1. |
| B12 | `EventSystem.current` null-check instead of `FindObjectOfType` | `Boot` | `FindObjectOfType` is deprecated in Unity 6 (warning). `EventSystem.current` is only non-null once an EventSystem has enabled — correct here because the boot scene is empty. | If a scene ever ships with its own EventSystem, a duplicate could appear. Harmless (Unity logs it) and one line to fix. |
| B13 | `Time.unscaledDeltaTime` everywhere | all animation | Deliberate, not a risk: `Time.timeScale` is never touched, and menus/curtains must not stop if it ever is. | — |

## C. Faithfulness gaps — knowingly not ported (all are fallback paths)

| # | Original | Status | Why |
|---|----------|--------|-----|
| C1 | `title_screen.gd`'s per-layer fallback (base/sun/fire/papers/founder/type + embers + founder jump) | Not ported | It only runs when `title/video/frame_01.png` is absent, and the 48 film frames ship. `TitleScreen` falls back to the static `title_screen.png` instead. |
| C2 | `howto_screen.gd`'s `_legacy()` four-panel explainer | Not ported | Only reachable when a `howto_N.png` sheet is missing; all three ship. The page keeps its furniture and caption with an empty frame. |
| C3 | `scene_director.gd`'s **v1** path (`resolve`, `compose`, the 516-room Atlas library, `_generate_background`, hosting/upload) | Not ported | It is the `RUNWAY_GPT_SCENES=0` fallback ladder, and `assets/backgrounds/` was never copied into the Unity project, so it could not resolve a room anyway. `MakeSceneV2` is the main path and is complete, including the 3-attempt ladder and both watchdogs. |
| C4 | `event_generator.gd`'s `next_card()` authored fall-through | Half ported | `TakeGeneratedCard()` ports the pool half exactly (dedupe by title similarity + recurring lead character). The authored deck needs a `ContentDb`, which does not exist in `Runway.Core` yet, so the fall-through stays with the run lane. |
| C5 | `main.gd`'s capture harnesses (`_fullrun`, `_firstflow`, `_shoot_turn`, …) | Not ported | Harness scaffolding, not game. The env switches they key off (`RUNWAY_SHOT`, `RUNWAY_FULLRUN`, `RUNWAY_ART`, `RUNWAY_NO_ART`, `RUNWAY_GPT_SCENES`, `RUNWAY_LLM_TIER`, `RUNWAY_FIRSTFLOW`) are all honoured by `Boot.Harness` / `Boot.ArtEnabled` / `Env.Flag`. |
| C6 | `MusicManager` | Not ported | Audio is out of this lane's scope. An `AudioListener` is on the camera and `Boot` marks the three places `music.play(...)` was called. |
| C7 | How-to page dots | Simplified | The active dot is a coral disc with a faint ink ring; the original draws a coral disc plus a *heavier* ink ring. One sprite swap if it matters. |
| C8 | Curtain scallop outlines | Simplified | Filled `CoralDark` discs, no wobbled arc outline over them. They are only visible during the 0.45s sweep — once shut, the baked sway sheet is the whole curtain. |

## D. Verified, not guessed

These were checked mechanically rather than trusted:

- **All five JSON schemas** (`EVENT`, `ADJUDICATE`, `CLARIFY`, `WORLD`, `ARC`) were extracted from
  `LlmClient.cs`, parsed, and deep-compared against the `llm_client.gd` originals: **identical**.
- **All four prompt constants** (`SYSTEM_PROMPT`, `ADJUDICATE_PROMPT`, `WORLDGEN_PROMPT`,
  `DIRECTOR_PROMPT`) are byte-for-byte the `event_generator.gd` strings — they were generated
  into the C# file from the `.gd` source rather than retyped.
- **All three scene laws** (`CharacterLaw`, `StyleLaw`, `FrameLaw`) compared against
  `scene_director.gd` after concatenation: **identical**.
- Brace/paren/bracket balance and cross-type member resolution were swept across all 24 files
  (every `DrawnUI.*`, `RunwayPaths.*`, `Env.*`, `SheetLoop.*`, `SaveSlots.*`, `BuildStamp.*`,
  `EventGenerator.*`, `LlmClient.*`, `ScreenRegistry.*` reference resolves to a declared member).

## E. The seam onto Runway.Core

The composers are pure — they take a `RunSnapshot`, never a Core type — so the LLM
layer can be read and changed without the engine. All of the reaching into the engine
happens in **one file**, `Assets/Scripts/LLM/CoreSnapshot.cs`, through Core's public
API only, and it never writes to state:

```csharp
generator.Adjudicate(CoreSnapshot.From(state), card, move, cb, dice);
```

`CoreSnapshot.From` is the only thing in this lane that will break if Core renames a
member. What it reads, and nothing else:

| Snapshot field | Core call |
|---|---|
| `Digest` | `state.ToDigest()` |
| `Signals` | `SimEngine.Signals(state)` |
| `BibleDigest` | `WorldGen.BibleDigest(state)` |
| `TraitSheet` | `state.TraitSheet()` |
| `ArcDirectives` | `state.ActiveArcDirectives()` |
| `RunwayWeeks` | `SimEngine.RunwayWeeks(state)` |
| `StatusCatalog` | `SimEngine.STATUS.Keys` |
| `Launched` | `state.HasFlag("launched")` |
| the rest | plain fields: `Cash`, `Week`, `Era`, `Traction`, `Exhaustion`, `TechDebt`, `Clocks`, `Offers`, `Items`, `Budgets`, `Investors`, `Rivals`, `StorySoFar`, `RunHistory`, `Arcs`, `PlayedEvents`, `CompanyName`, `CompanyIdea`, `BizWhat`, `BizWho`, `FounderName`, `Cofounders`, `Employees` |

`Boot` itself touches **no** Core type at all. The seed, the state, the record, WorldGen,
SimEngine and the saves all sit behind `IRunDriver` (`Assets/Scripts/App/IRunDriver.cs`),
whose every method names the beat in `main.gd` it stands for. Until an implementation is
registered (`Boot.PendingDriver` or `Boot.Instance.Driver`), `NullRunDriver` keeps the
flow walkable and logs a warning at boot rather than looking like a working game.

## F. Unity conventions this code obeys (so the next lane does too)

1. **One MonoBehaviour per file, named after the file.** Unity will not attach a component
   whose class name does not match its file name. Non-MonoBehaviour helpers (`LlmOptions`,
   `TitleChoice`, `SaveSlotInfo`, `PaintStatus`, `AppState`) share a file with their owner.
2. **No scene, no prefabs.** Everything is built in code from `Boot`'s
   `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`. `Build.cs` creates one empty scene
   because a player build needs something to load.
3. **Screens register themselves**, they are not listed in `Boot`:
   `ScreenRegistry.Register(AppState.Draft, typeof(FounderDraftScreen))` from the screen's own
   `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`. An unregistered state shows
   `MissingScreen` (a drawn placeholder with a way back to the title) and logs once.
4. **`ProjectSettings/` is empty.** Unity will generate defaults on first open. `Build.BuildMac`
   sets `companyName = "Assem Studio"` and `productName = "RUNWAY!"`; until then
   `Application.persistentDataPath` resolves under `DefaultCompany/unity`, which only affects
   the generated-scene cache (keys and saves use `~/Library/Application Support/Runway`).
