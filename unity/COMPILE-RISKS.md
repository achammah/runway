# COMPILE-RISK LEDGER — App shell, screens, LLM layer and the run lane

No Unity editor exists on this machine yet, so nothing here has been compiled. Every
API below is one I was not 100% certain of, with the fallback that was coded so a
wrong guess degrades instead of breaking. Ordered worst-consequence first.

Sections A–F cover the app shell, the screens it shipped with and the LLM layer.
Sections G–K cover **the run lane** (`Assets/Scripts/Game/`): the driver behind
`IRunDriver`, the draft, the birth and book screens, the room, the log book, the
binder and the last page.

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

---

# THE RUN LANE — `Assets/Scripts/Game/`

## G. Blocking risks — these stop a compile if they are wrong

| # | API | Where | Why uncertain | Fallback coded |
|---|-----|-------|---------------|----------------|
| G1 | `TMP_InputField.LineType.MultiLineSubmit` | `JournalPage.WriteField` | It is the one line type where Return raises `onSubmit` instead of inserting a newline — which is exactly the Godot contract (`KEY_ENTER and not shift` commits). Present since TMP 1.x, but it is an enum member and an enum member is a compile error when absent. | Swapping to `MultiLineNewline` is a one-line change; the lock button then becomes the only commit, which is still a complete loop. `PaperInput` already proves `onSubmit` itself resolves. |
| G2 | `TMP_Text.maxVisibleCharacters` | `JournalPage`, `ReadingBeat` | The port of Godot's `Label.visible_ratio` — the "written, not printed" reveal that the whole book depends on. Long-stable, but it is the single API the page performance rides. | Every reveal path also has a `FinishReveal` / `SkipReading` that assigns the full length, and `Instant = true` skips the mechanism entirely. If the property is gone, delete the two write-in loops and every page still composes correctly. |
| G3 | `TMP_Text.lineSpacing` | `JournalPage.WriteField` | Pins the typed ink to the page's own rule pitch so what you write sits on the ruling like every drawn line. | It is cosmetic: without it the typed lines simply float between the printed rules. |
| G4 | `Image.type = Filled` + `fillMethod`/`fillOrigin`/`fillAmount` on a **runtime-baked** sprite | `ReadingBeat` pen stroke, `JournalSpreads` commit stroke | Already flagged as A8 for a sprite from an atlas; here the sprite comes out of `DrawnUI.WobbleLineSprite`, i.e. `Sprite.Create` with `SpriteMeshType.FullRect`. Filled needs a quad, and FullRect gives one. | If Filled misbehaves the same effect is `sizeDelta.x` animation on a masked child — the stroke is 12 lines in one method each. |
| G5 | `RectMask2D` | `JournalPage` write field, `ReadingBeat`, `ShelfScroll`, `BookIntroScreen` | Used instead of `Mask` because it needs no stencil material and no extra draw call. Long-stable in UGUI. | `Mask` + an `Image` on the same object is the drop-in swap. |
| G6 | `Rng.RandiRange(from, to)` treated as INCLUSIVE at both ends | `JournalSpreads` (the d20), `DraftNamePage`, `GarageScreen` fun fact | Ported as Godot's `randi_range`, which is inclusive. If Core's is exclusive at the top, every d20 is a d19 and no name generator ever reaches its last entry. | Verified against `Runway.Core.Rng` before writing; called out here because a silent off-by-one in the die is the worst kind of wrong. |
| G7 | `Newtonsoft` `JObject.ToObject<T>()` for `GameState`, `LlmWorld`, `List<Arc>`, `RunRecord` | `RunSave`, `RunDriver.FinishWorldgen` | Core carries `[JsonProperty]` on every field, so the whole state round-trips as one object rather than a hand-listed dictionary. A type Newtonsoft cannot construct throws at RUNTIME, not compile. | Every call is inside `try/catch`; a save that will not load returns false and the flow falls through to a fresh run exactly as `main.gd` does. |

## H. Runtime risks — they compile, but may not do what the Godot original does

| # | Thing | Where | Risk | Fallback coded |
|---|-------|-------|------|----------------|
| H1 | **`RectMask2D` is axis-aligned; the log book's paper is not.** | `JournalPage.WriteField` | The written-move field sits inside `Space`, which carries the paper's drawn 4° lean, and a 2D rect mask clips to the unrotated bounds. Long text scrolling inside the field is clipped a few pixels early at two corners. | The field is 2–5 rules tall and the clip is generous; the visible symptom is at most a clipped descender on a scrolled line. Removing the mask entirely is also survivable — the field would simply not clip. |
| H2 | **TMP metrics are not Godot metrics, and the page is laid out in RULES.** | `JournalPage.FontHeight` | The zone allocation, `LineAdvance` and every `LineFitted` budget depend on `_font.get_height(sz)`. Unity has no equivalent that is cheap and exact per size, so the hand's line box is approximated ONCE as `size × 1.35`. At 34px that keeps body text on one rule and the 64px title on two, which is the whole contract. | The number lives in one `public static` method. If pages come out with blank rules between lines, that constant is the only thing to change. `_overrun` warns by name whenever ink passes the paper's edge. |
| H3 | **The log book's paper is DRAWN, not photographed.** | `JournalPage` | The Godot page lays out over `assets/ui/logbook_page.png` and reads its writable silhouette from `logbook_page_zones.json`, extracted from that PNG's alpha. Neither ships in this project. | The sheet is drawn with the same hand every other card uses and the silhouette is the rectangle the drawn sheet is — which is what the original's own `span_at()` returns anyway, because it rotates one Control to the drawn lean and works upright inside it. Every measured constant (page size, paper origin, tilt, rule pitch, rule first) is transcribed, so dropping the art back in is a texture swap. |
| H4 | **No cast reference images reach the painter.** | `RunDriver.CastRoster` | `assets/scenes/refs.json` (the permanent https urls for every character sprite) is not in this project, so `castUrls` is always empty and `SceneDirector` takes the **generate** endpoint rather than **edit**. | The roster still NAMES this company's real people and what each is doing, and the generator path paints from that description — so the room is still this founder's room, just not built on their reference sheets. Dropping `refs.json` in and filling `castUrls` is the only change needed. |
| H5 | **The `Finale` ceremony is the last page.** | `RunDriver.AfterGrind` | An IPO or an acquisition earns its own ceremony screen in Godot (`finale_screen.gd`). Here both endings write their headline and go straight to the last page. | `AppState.Finale` is registered to `AutopsyScreen`, so a future finale screen registers over it with no other edit. |
| H6 | **Era transitions are not staged.** | `GarageScreen.StartWeek` | `main.gd` queues an `EraTransitionScreen` overlay per move and re-renders the new room with the crew unpacking in it. Here a move writes its line into the week's departures, which the was-page prints, and the room simply changes. | The move is never LOST — `AdvanceEraIfReady` / `Demote` still run and the ledger still says so. Wiring `AppOverlay.EraTransition` is the whole of the missing work. |
| H7 | **Cofounder loyalty lives on `state.Meta`.** | `RunDriver.Loyalty` / `SetLoyalty` | Godot keeps `loyalty` on the loose cofounder dictionary; Core's `Cofounder` is typed and has no such field, and Core is not this lane's to edit. It is stored as `cf_loyalty_<index>` in the state's own metadata, which saves and loads with everything else. | It is keyed by INDEX, so a cofounder removed from the middle of the list shifts everyone's loyalty by one. The only removal path (a walk-out at zero loyalty) removes the person whose loyalty just hit zero, so the shift lands on people whose value is about to be re-read anyway. Keying by name would be the fix if this ever matters. |
| H8 | **The room has two rungs, not four.** | `GarageScreen` | Godot's room ladder is: spot-patch room → assembled stage → painted scene → sprite fallback. The first two are built on background/pose libraries this project does not carry (`assets/scenes/`, `assets/backgrounds/`), and the owner's standing verdict on the assembled path is "assembled, not organic". | Ported: the composed painting when the director delivers one, and the drawn room (shipped object sprites, decay tracking morale, badges tracking flags) otherwise. `AdoptComposed` is the same public seam `main.gd` calls, so the turn hands over its picture unchanged. |
| H9 | **No writable scene surfaces.** | `GarageScreen` | The Godot room writes the week's numbers onto the room's own furniture (`SceneSurfaces`), and a V2 render is SCANNED for real blank regions to write on. Both need annotation tables this project does not carry. | The numbers live on the drawn plates — the HUD strip, the money tag and the pinned equity paper — which is the Godot fallback for exactly the same case (`_off_stage()`). When a painting arrives the plates move up into the calm top strip the composition law keeps clear. |
| H10 | **The gestures and the department board are gone from the week.** | `JournalSpreads.ApplyLock` | `_apply_lock` in Godot still carries `_pending_people` (bonus / shares / gear) and `_pending_work` (per-department presets and free plans). The two-spread redesign removed every widget that could fill them, so in the shipped Godot build those loops run over empty dictionaries every week. | Not ported. If the widgets ever come back, they come back with their executor. |
| H11 | **The pivot sheet is not ported.** | — | `_open_pivot` is a three-sheet flow reached from a button the current room no longer draws. | Out of the slice. `state.Pivots` and the `pivoted` flag still exist and still read correctly everywhere. |
| H12 | `Input.mouseScrollDelta` for every scroller | `ShelfScroll`, `ReadingBeat` | macOS two-finger scrolling arrives as a pan gesture in Godot and the original handles both; Unity's legacy input reports the trackpad as wheel delta, so one path covers both. | `ShelfScroll` only answers while the pointer is over it, so two scrollers on one page never fight. If the trackpad reports nothing, the shelf still shows its first rows and the beat still writes itself out. |
| H13 | **The pen nib does not ride the ink.** | `JournalPage` | The original draws a real pen at the tip of the line being written. The character-by-character reveal — the part that reads as handwriting — is ported; the nib sprite is not. | Cosmetic. The resting nib at the writing field IS drawn, because it is the affordance that says "write here". |
| H14 | **Escape reaches Boot as well as the binder.** | `BinderScreen` | Boot opens `AppOverlay.Settings` on Escape. No settings screen is registered, so Boot logs one line and returns null. | The binder swallows Tab/B correctly (one frame of deafness stops the opening key from also closing it, and `IsOpen` stops a second copy). If a settings screen is ever registered, the binder should close first. |
| H15 | **`DrawnChart` bakes a texture per distinct value set.** | cap-table donut, binder sparks | A donut is re-baked whenever the split changes, which on the crew page is every button press. Each is ≤340² RGBA. | Cached by a key built from the numbers and colours, so holding a value re-uses the bake. The sparks change once a week. |
| H16 | **The pen ring is a stretched circle.** | `GameUi.PenRing` | Godot's `InkTag(shape=1)` computes an ellipse per cell; here one baked ring is stretched to the cell, so the stroke thins on the long axis. | It is a hand-drawn loop round an object; the distortion reads as the hand, not as a bug. |

## I. How the lane is split, and why

Two files earned a second half rather than growing past the shell's own limit:

- **`JournalSpreads` / `WeekCommit`.** The spreads DRAW the book — what is on the
  week-that-was page and the week-ahead page. `WeekCommit` DRIVES the run — the lock
  line, the clarify pre-pass, the die at the press, the effect executor and everything
  the week does when it turns. The two answer to different things, so they are two files.
- **`JournalPage` / `PageBlocks` / `PageReveal`.** The page shell owns the paper: the
  silhouette, the printed rules, the four zones and the cascade. `PageBlocks` owns the
  only two elements with an internal layout of their own (the icon row and the written
  move). `PageReveal` owns the hand that writes the page in. `PageBlocks` reaches back
  through a deliberately INTERNAL surface — a page host may only ever add content.

`GarageScreen` (770) and `RunDriver` (705) sit a little over the ~700 guide and were
left whole: the room plus its weekly loop is one unit, and so is the driver behind
`IRunDriver`. Splitting either would put one beat of `main.gd` in two files.

## J. What the run lane assumes about the shipped data

- `StreamingAssets/items.json` (42 items), `archetypes.json` (4 archetypes + 3 fundings) and
  `events/*.json` all ship and are read by `ContentDb`. The event folder is enumerated where
  the platform allows it and falls back to the seven known deck names otherwise.
- `ContentDb.InstallCoreReader()` is what makes `Runway.Core` able to read `items.json` at all —
  it is called from `RunDriver`'s constructor AND from `ContentDb.Deck`, so a trait calculation
  can happen before any run has started.
- Art the lane relies on, all present: `dice/roll_01..20.png` (8×5 of 512px),
  `title/birth_intro.png` (5×5 of 1024×576, 24 frames), `title/birth_loop.png` (5×8, 40 frames),
  `title/layers/type_main.png`, `sprites/gv/*` (money/board/chart tiers, decay, badges),
  `sprites/itm_*`, `sprites/cf_*`, `sprites/chr_arch_*`, `sprites/chr_loop_<id>_01..36`,
  `journal_icons/*`, `env/stage.png`. Every one of them degrades to a drawn fallback.

## K. The seam, as the run lane implements it

`Boot` still touches no Core type. `RunDriver` is registered from its own
`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` into `Boot.PendingDriver`, so installing the
run needs no edit to `Boot`:

| `IRunDriver` beat | What the run lane does |
|---|---|
| `ListSlots` / `SetActiveSlot` / `ClearRun` / `HasSavedRun` | `SaveSlots`, unchanged |
| `ResumeSavedRun` | `RunSave.Load` → state + record, rng rebuilt from `seed + week`, card pool cleared |
| `BeginFreshRun` | seed (or the date seed), new `GameState`, new `RunRecord`, `Generator.Disabled = daily` |
| `ApplyDraft` | the whole `_after_draft` engine half: archetype, funding, cap table, competence coverage, traps, bag, the record entry, `WorldGen.Build`, `GenerateArcs` |
| `PrefetchWorld` | the bag-page bible, keyed on `name\|idea` so a re-entry never pays twice |
| `EnsureWorldgen` / `WorldgenInFlight` / `WorldgenLanded` | the birth screen's 25s gate |
| `FinishWorldgen` | `ApplyLlmWorld` + `SeedBeliefs`, then starts day one; returns the entry only if it already landed |
| `ColdOpen` | drops the curtain, consumes (or waits for) day one, and hands it to `TurnRunner` on the `book_read` silent path |
| `CompanyName` | `state.CompanyName` |

Everything else a screen needs — `State`, `Record`, `Rng`, `Deck`, `Loyalty`, `LastOutcome`,
`SaveIfWeekTurned`, `CheckExit`, `AfterGrind` — hangs off `RunDriver.Current`, which is the only
static the screens reach through.

---

# D4 — THE IMPULSE LANE — `Assets/Scripts/Effects/Impulse.cs`

Weight: the hand-rolled critically damped spring that lets the stage take a hit.
Two callers only — a backfired week and a settled die. No packages, no
Cinemachine, no shared file touched. `Assets/Scripts/Editor/ImpulseProbe.cs` is
its evidence and can be run as a gate (`-executeMethod
Runway.EditorTools.ImpulseProbe.Run`, exits 1 on any failed check).

## D4-1. Blocking risks — these stop a compile if they are wrong

| # | API | Where | Why uncertain | Fallback coded |
|---|-----|-------|---------------|----------------|
| D4-1a | `Mathf.Exp` | `Impulse.Advance` | Long stable; named only because it is the ONE call the whole effect's correctness rides on. The closed form for ζ=1 needs a real exponential — there is no cheap substitute that keeps the amplitude honest. | None needed. If it ever went away, `(float)System.Math.Exp` is the same number. |
| D4-1b | `RectTransform.anchoredPosition` / `Transform.localScale` as the offset channels | `Impulse.Apply` | Both are long stable. `anchoredPosition` is the right channel for the Stage specifically because Boot anchors it centre/centre with pivot 0.5, so the value IS an offset from the canvas centre and 0 is genuinely rest. | The effect never reads the target's live pose except once, at rest capture, so any rect with either member works. Binding a different rect is `Impulse.Bind(rt)`. |
| D4-1c | Optional-parameter-free overloads | `Impulse.Shake` | `Shake(px, ms)` and `Shake(px, ms, Vector2)` are written as two methods rather than one with a `Vector2 dir = default(Vector2)`. A struct default-argument is legal C# but it is the kind of thing an older compiler profile argues with, and two methods cost nothing. | — |

## D4-2. Runtime risks — they compile, but may not do what the game wants

| # | Thing | Where | Risk | Fallback coded |
|---|-------|-------|------|----------------|
| D4-2a | **A 6px shake of the Stage can expose 6px of camera clear colour** — on a window whose aspect is EXACTLY 3:2. | `Impulse.Shake` on `Boot.Stage` | `CanvasScaler.ScreenMatchMode.Expand` already letterboxes the 1536x1024 stage on every non-3:2 window (16:10 and 16:9 are both wider), so the shake moves the stage inside a band that is already there and already the same colour. At exactly 3:2 the band is zero-width and one edge shows a 6px strip of `DrawnUI.Stage` for 250ms. | Not coded, deliberately — `Shake` does one thing. If a 3:2 window ever ships, the one-line fix is a cover scale for the shake's life: enough zoom to swallow the displacement is `1 + 2·px/1536` = **1.0078** at 6px, which is imperceptible. Add it inside `Shake`, not at the call site. |
| D4-2b | **`Punch(scale, ms)` with `scale < 1` WOULD reveal the letterbox**, on every window. | `Impulse.Punch` | Scaling the Stage DOWN shrinks it inside the canvas and the camera's clear colour fills the gap on all four sides. Punching IN (>1) can never do this — the stage only grows. | The shipped call is 1.02. Treat `scale < 1` as unsupported on the Stage; bind a child rect first if a shrink is ever wanted. |
| D4-2c | **The rest pose is captured at the idle→active edge.** | `Impulse.Fire` → `CaptureRest` | If anything else is animating the Stage's `anchoredPosition` or `localScale` at the instant an impulse fires, that mid-animation pose becomes "rest" and the stage snaps to it on settle. Nothing animates the Stage today — Boot builds it once and leaves it. | The effect must OWN its target's transform. If a future lane wants to animate the Stage, give Impulse a dedicated child rect with `Impulse.Bind(child)` and animate the parent. |
| D4-2d | **The punch scales everything under the Stage** — screens, overlays, curtain and the dice cup alike. | `Impulse.Apply` | That is the intent (the whole frame takes the weight), but it means the die the punch celebrates is itself scaled by 2%. At 120ms and 2% this reads as the table absorbing the hit. | Binding `Boot.ScreenLayer` instead of the Stage would leave the cup and curtain out of it. One line, at the integrator's discretion. |
| D4-2e | **Eight simultaneous impulses is the ceiling.** | `Impulse.Fire` | A ninth is dropped silently rather than growing an array in the hot path. The game fires at most two at once. | Raise `Slots`; nothing else changes. |
| D4-2f | **The driver is only created in play mode.** | `Impulse.EnsureDriver` | `DontDestroyOnLoad` errors in edit mode, so `Application.isPlaying` gates the whole driver. A `Shake()` from an editor tool therefore arms a shot that nothing pumps. | Intended: `Impulse.Step(dt)` is public, and any harness (including this lane's probe) drives it itself. In a player build the driver always exists. |
| D4-2g | `RUNWAY_FX_IMPULSE` is read through `Env.Get`, not `System.Environment` alone. | `Impulse.Enabled` | So the switch can also be set in `.env` or `keys.env`, not only in the process environment — same layering every other switch in this build uses. A stale value survives until `Impulse.RefreshSwitch()`. | `Impulse.SetEnabled(bool)` forces it without the environment at all — that is the D8 kill-switch matrix's entry point. |
| D4-2h | **Moving the Stage re-batches the canvas** for the frames it moves. | `Impulse.Apply` (E2's concern) | Writing `anchoredPosition`/`localScale` on the canvas root dirties the batch — it does NOT dirty any `Graphic`'s vertices, so this is a re-batch and not a rebuild storm. It happens on at most 8 frames per backfired week and 4 per die. | The effect writes nothing at all while idle: `Step` returns on the first line when no shot is live, and the driver component disables itself on settle, so an untouched frame costs one early-out and zero canvas work. |

## D4-3. Verified, not guessed

Measured by running the shipped `Impulse.cs` itself. `ImpulseProbe.Run` passes
**33/33 headless in the editor**, sampling the curve per frame at 240fps, 60fps
and 30fps. The same source was independently driven outside Unity against a
stand-in for the handful of `UnityEngine` members it touches; the two runs agree
to **4e-06 px at worst**, and every 30fps row is bit-identical:

- **The closed form, not an integrator.** ζ=1 has an exact step
  (`x' = (A + B·h)·e^{-ωh}`, `B = v + ω·x`), and it is used because a sub-stepped
  semi-implicit Euler was **measured bleeding ~15% of the amplitude at 30fps** —
  `Shake(6px)` delivered 4.76px, so the number in the call meant nothing. With the
  closed form the peak is **identical at 240/60/30fps to four decimals**.
- **Shake(6px, 250ms)**: peak 5.987px (of 6.000 asked), 8 frames at 30fps, three
  swings — `+5.987 +4.121 −1.507 −1.943 +0.763 +1.112 +0.175 +0.000`. The shape is
  the Godot original's own bag-refusal shake (`[-6, +6, -4, 0]`,
  `founder_draft_screen.gd:2081`).
- **Punch(1.02, 120ms)**: peak +0.019992 (of 0.020 asked), 4 frames at 30fps —
  `+0.0200 +0.0151 +0.0079 +0.0000`, and it never crosses below rest, because a
  critically damped spring cannot overshoot.
- **Exact rest is exact.** Against a deliberately non-trivial rest pose
  (`pos (13.5, −7.25)`, `scale (0.97, 1.03, 1)`), position and scale deltas after
  settle are **0, bit for bit** — the tail gain brings the offset to a mathematical
  zero over the last 20% of the shot, and the final act assigns the captured rest
  rather than easing toward it.
- **Zero per-frame allocation**: 250,351 `Step()` calls moved the managed heap by
  **0 bytes** with **0 gen-0 collections**.
- **Restraint**: `Verdict("brilliant")`, `Verdict("fine")` and `Verdict("risky")`
  are silent; only `"backfired"` moves the frame. `Verdict(null)` is survivable.
- **Kill-switch**: `RUNWAY_FX_IMPULSE=0` (also `off`/`false`/`no`) fires nothing and
  leaves the target byte-identical; absent and `1` are on.

## D4-4. The seam

`Impulse` reaches into exactly one thing it does not own — `Boot.Instance.Stage`,
read-only, and only when nothing has been bound. Boot is not edited and does not
know the effect exists. `Impulse.Install()` is the optional one-line entry point;
without it the effect installs its driver on the first `Shake`/`Punch`.

The restraint law lives in this file, not at the call sites: `Impulse.Verdict(band)`
is a no-op for every band except `"backfired"`, so the hookup cannot get it wrong
and D4c is provable here rather than at integration.

---

# N6 — THE PERF HARNESS — `Assets/Scripts/App/UnityPerf.cs` + `UnityPerf.Report.cs`

The twin of `game/tests/perf_probe.gd`. It edits nothing: it self-installs from its
own `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` only when `RUNWAY_UPERF=<dir>`
is set, and drives the real screens through Boot's own public doors (`Boot.Go`,
`Boot.Curtain`, `DiceRoll.Create`, `RunDriver`). With the variable unset the two
files cost one string read at boot and nothing else.

## N6-1. Blocking risks — these stop a compile if they are wrong

| # | API | Where | Why uncertain | Fallback coded |
|---|-----|-------|---------------|----------------|
| N1 | `Texture.currentTextureMemory` | `UnityPerf.TextureMb` | A static `ulong` on `UnityEngine.Texture` and the only runtime-side twin of Godot's `RENDER_TEXTURE_MEM_USED`. Present since 2019.3 but it has moved modules before. | The read is inside `try/catch` and returns `-1`, which the report prints as `n/a` rather than as a free screen. Swapping it for `Profiler.GetAllocatedMemoryForGraphicsDriver()` is a one-line change. |
| N2 | `FindObjectsByType<T>(FindObjectsSortMode.None)` | `UnityPerf.LiveObjects` | The Unity 6 replacement for `FindObjectsOfType`, which is deprecated (warning) and would break the zero-warning budget (A9). Its enum argument is what dates it. | `try/catch` → `-1` → `n/a`. |
| N3 | `Profiler.GetTotalAllocatedMemoryLong()` / `GetMonoUsedSizeLong()` | `UnityPerf.LateUpdate` | `UnityEngine.Profiling` is in CoreModule, so it resolves; what is uncertain is the VALUE (see N6-2). | None needed at compile time. |
| N4 | `Canvas.willRenderCanvases` as a subscribable static event | `UnityPerf.Prep` / `OnDestroy` | The engine-side canvas tick. Public since forever; the delegate shape is what could change. | Unsubscribed in `OnDestroy`, so a leak cannot outlive the probe. Losing the column loses `canvas/s` only; `rebuild/s` is the column that matters. |
| N5 | `UnityEditor.SessionState`, `EditorApplication.EnterPlaymode()`, `EditorApplication.Exit`, `Application.isBatchMode` | `UnityPerfEditor`, `UnityPerf.Finish` | All editor-only, all inside `#if UNITY_EDITOR`, so a player build never sees them (the `Build.cs` pattern, A5). | The player path is plain `Application.Quit()`. |

## N6-2. Runtime risks — they compile, but may not measure what they claim

| # | Thing | Where | Risk | Fallback coded |
|---|-------|-------|------|----------------|
| N6 | **The UGUI rebuild queues are found by reflection.** | `UnityPerf.BindUi` | `CanvasUpdateRegistry.instance` is public but `m_LayoutRebuildQueue` / `m_GraphicRebuildQueue` are private, and they are the ONLY honest twin of Godot's `redraw/s`. A UGUI rename silently removes the column. | Found ONCE at first use and then held as plain `IList<ICanvasElement>` — `IndexedSet<T>` is an internal class implementing a public interface, so the cast is legal and the per-frame path has no reflection, no boxing and no allocation to pollute `gc/s`. A miss sets `RebuildBlind` and the whole column prints `n/a` instead of `0.0`. Verified against `com.unity.ugui@8bb446d869cd/Runtime/UGUI/UI/Core/CanvasUpdateRegistry.cs` lines 86–97 and `SpecializedCollections/IndexedSet.cs` line 7. |
| N7 | **The queues are read in `LateUpdate`, and script order inside LateUpdate is undefined.** | `UnityPerf.LateUpdate` | Unity drains the queues in `PostLateUpdate`, after every `LateUpdate`, so this is the last honest read — but a rebuild requested from ANOTHER script's `LateUpdate` that runs after this one lands in the next frame's count instead. | It is a proxy, and the report says so. The error is a one-frame shift, not a lost rebuild. |
| N8 | **`UnityStats.drawCalls` is reached by name, not by reference.** | `UnityPerf.StatInt` | It lives in the editor assembly, so the file has to reach it through `Type.GetType("UnityEditor.UnityStats, UnityEditor")` or it would not compile into a player. Two assembly names are tried. | `-1` → `n/a`. A player build simply has no draw-call column; the Godot `draws` row is then answered from an editor run. |
| N9 | **The Profiler counters read 0 in a non-development player build.** | `alloc MB`, `mono MB` | `ENABLE_PROFILER` is on in the editor and in development builds only. A release `.app` would print a table of zeros for two columns. | Documented in the file's own legend. The editor run and a development build are both real answers; a release build's memory column is not. |
| N10 | **The frame cap makes `frame ms` a lie.** | `UnityPerf.Prep` | Boot pins `targetFrameRate = 30`, so every screen reads ~33.3ms whatever it costs, and a real saving reads as no change — the same trap `perf_probe.gd` calls out. | The per-screen table runs UNCAPPED on purpose (`targetFrameRate = -1`) so frame ms is work and fps is headroom; the soak runs at the shipped 30 because a hitch hunt has to feel what the player feels. `RUNWAY_UPERF_CAP=<n>` overrides both, and the cap in force is stamped into the file. |
| N11 | **Two ways to be blind, and the second one lies convincingly.** | `UnityPerf.Blind` | `-nographics` has no device and every render number is a flat zero. **Batchmode WITH a device is worse**: Metal comes up, textures really are created, the canvas really does update — but no window means nothing is ever presented, nothing paces the loop, and `fps` reads in the THOUSANDS with zero draw calls. Measured: 3,500–9,200 "fps" and `draws 0` on an M4 Pro. | `Blind()` is `Application.isBatchMode || graphicsDeviceType == Null`. It raises a **BLIND RUN** banner naming exactly which columns survive (`rebuild/s`, `tex MB`, `alloc MB`, `mono MB`, `gc/s`, `objects`, texture-upload spikes in `pk ms`) and which do not (`fps`, `frame ms`, `draws`), plus a `BLIND` note on every row. `UnityStats` is not even bound in batchmode, so `draws` reads `n/a` rather than a zero that would read as "free". |
| N12 | **The probe forces Boot's harness switch from the outside.** | `UnityPerf.GoKeyless` | It sets the process variables `RUNWAY_LANEWIRE=1` and `RUNWAY_NO_ART=1` before Boot's `Awake`, because `Env.Get` reads the process environment first. That buys: no studio card, no save writes, no title curtain reveal fighting the curtain row, and a scene director that never renders. If `HarnessVars` is ever re-ordered or `RUNWAY_LANEWIRE` retired, the probe measures the studio card instead of the title. | One line at integration removes the guess: add `"RUNWAY_UPERF"` to `Boot.HarnessVars`. Until then the forced variable is set in exactly one method with a comment naming why. |
| N13 | **`Environment.SetEnvironmentVariable` does not survive an editor domain reload.** | `UnityPerfEditor.Launch` | Entering play mode reloads the domain, and the folder set by a menu click would be gone by the time `Install()` runs. | The folder is stamped into `UnityEditor.SessionState`, which does survive, and `OutDir()` reads the env first and the session second. |
| N14 | **The keyless clamp mutates the layered env dictionary.** | `UnityPerf.GoKeyless` | `LlmClient.Setup` is fed `Env.Load()`, which reads FILES and never the process environment — so the only way to make the client keyless without editing a shared file is to lift `OPENAI_API_KEY` / `ANTHROPIC_API_KEY` out of the cached dictionary before Boot reads it, and pin `LLM_PROVIDER` to a name no branch matches. | Belt and braces: `Generator.Disabled = true` is also set (the line `perf_probe.gd` writes for the same reason), and the report says loudly if `Llm.Enabled` came up true anyway. `Env.Reload()` would undo it, and nothing the probe walks calls it. |
| N15 | **The garage row deals a run, and a run mutates.** | `UnityPerf.StandUpRun` | `GarageScreen.OnBuild` calls `StartWeek()`, which burns cash, drains loyalty and advances the week — so the room measured is week 10 of the fixture, not week 9. That is also true of the Godot twin. | Deliberate and identical on both sides; the fixture is `perf_probe.gd`'s `_build_garage` transcribed. No save is written, because the harness switch is on. |

## N6-3. What the Godot probe measures that this one does not

| Godot column / mode | Status | Why |
|---|---|---|
| `pk phy` (TIME_PHYSICS_PROCESS) | No twin | This port runs no physics at all; there is nothing to report but a zero. |
| `RUNWAY_PERF_BLAME=1` (which node is repainting, by name and class) | Ported, as `RUNWAY_UPERF_BLAME=1` | Godot connects to every `CanvasItem.draw`; UGUI has no per-element callback, so the queues are WALKED instead — each `ICanvasElement` gives up its `transform`, and the last five names on the way up plus the component type make the address. Off by default: naming an element costs a string per dirty element per frame, which is the exact allocation `gc/s` exists to catch. |
| The soak's CPU-seconds figure (`/usr/bin/time -l`, one screen per process) | Replaced, not ported | The Godot soak fixes the wall clock and lets the shell measure energy. The brief's Unity soak is a different instrument: one process, ten scripted minutes ACROSS the screens, reporting max frame ms, p99 and the count of frames past 50ms — which is what E5 actually asks for. `/usr/bin/time -l` around the whole invocation still gives the energy number if it is wanted. |
| `vram MB` (RENDER_TEXTURE_MEM_USED) | Approximated | `Texture.currentTextureMemory` is CPU-side texture memory, not driver VRAM. It moves for the same reasons and by the same amounts (a 94MB sheet is a 94MB sheet), so the columns join — but they are not the same counter. |

## N6-4. The seam

Nothing shared is edited and nothing shared is subclassed. The probe reaches into
exactly five public surfaces, all read or call, never write:

| What it touches | Why |
|---|---|
| `Boot.Go(state)` / `Boot.ScreenLayer` / `Boot.OverlayLayer` / `Boot.Curtain` | to stand each screen up the way the game does, and to bare the stage between them |
| `Boot.Generator.Disabled` | the twin of `_gen.disabled = true` — no network in a heat measurement |
| `ScreenRegistry.Has(state)` | a screen no lane has registered yet is a skipped row with a note, never a crash |
| `DiceRoll.Create(parent, 17)` | the die is not a screen and has no registry entry |
| `RunDriver.Current` (`BeginFreshRun` / `ApplyDraft` / `Deck` / `State`) | the room needs a run behind it; the fixture is dealt from the shipped deck |

The one hookup that would remove a guess is `"RUNWAY_UPERF"` in `Boot.HarnessVars`
(see N12). Everything else is already public.

---

# D1 — THE LINE-BOIL LANE — `App/DrawnUI.Boil.cs` + `App/InkBoil.cs` + `App/InkBoilClock.cs`

Compiled and run: `-batchmode -quit -executeMethod Runway.EditorTools.BoilShot.Shoot`
came back exit 0 with zero errors from these three files, and wrote its frames.

## D1-1. Blocking risks — these stop a compile if they are wrong

| # | API | Where | Why uncertain | Fallback coded |
|---|-----|-------|---------------|----------------|
| D1-A1 | `DrawnUI` is **not** `partial` | — | The lane was specified as a `partial class DrawnUI` extension, but `DrawnUI.cs` declares `public static class DrawnUI` with no `partial`. A second `partial` declaration is CS0260, a hard error, and adding the keyword is an edit to a shared file. | The kit is a **separate** static class, `DrawnBoil`, living in the file the brief names (`App/DrawnUI.Boil.cs`). Nothing needs `DrawnUI` to change. If the owner later marks `DrawnUI` partial, `DrawnBoil`'s members move across as-is. |
| D1-A2 | `CanvasRenderer.SetTexture(Texture)` | `InkBoil.Put` | It is how `Graphic.UpdateMaterial` itself hands a sprite's texture to the shared UI material, so it is as stable as UGUI — but it is not a documented user-facing swap, and a graphic that rebuilds resets it. | `DrawnBoil.SwapMode = Swap.Sprite` switches the whole kit to `Image.overrideSprite` (variant sprites are baked alongside the textures, same pivot and PPU, so geometry is identical). The shot harness falls to that mode automatically if the renderer swap shows no motion, and to a CPU composite if that also fails. **Verified live on this machine: the renderer swap rendered.** |
| D1-A3 | `Texture2D.isReadable` | `DrawnBoil.InkTexture` | The property exists on `Texture2D` (not on `Texture`), and the code casts before reading it. | Only reached inside the eligibility test, whose whole job is to say no; a false negative means one line does not boil. |
| D1-A4 | `Texture2D.EncodeToPNG()` | `BoilShot` (editor only) | An extension method from `com.unity.modules.imageconversion`, which **is** in `Packages/manifest.json`. | The whole file is under `Assets/Scripts/Editor/`, so a player build never sees it. |
| D1-A5 | `RectInt` | `BoilShot` | Editor-harness only; long stable since 2017. | — |

## D1-2. Runtime risks — they compile, but may not do what the look wants

| # | Thing | Where | Risk | Fallback coded |
|---|-------|-------|------|----------------|
| D1-B1 | **Re-seeding the rasteriser would have broken the amplitude ceiling.** | `InkBoilBake` | Baking variants by calling `WobbleRectSprite(..., seed + 1)` gives an INDEPENDENT wobble: two draws from ±jitter can differ by 2×jitter, i.e. up to 4px on the sheet style — a crawl, well past D1c's 1.5px. | The variants are the SAME bake displaced through a smooth field: direction from `System.Random(seed + 1)`, radius from `System.Random(seed + 2)`, and the second field held 60° from the first at equal radius. Three states at `0`, `v`, `w` with `\|v\| = \|w\| = \|v − w\| = r ≤ Amplitude` — so **every** frame-to-frame move is bounded by `Amplitude` (default 1.1px, hard-clamped at 1.5px) in any play order. Measured on the six-piece kit: worst move **0.919px**. |
| D1-B2 | **Three copies of every ink texture.** | `InkBoilBake` | Ink bakes are full-card-size even when only the border is inked (a 1140×880 keys sheet is a 4.1MB RGBA32 edge), so boil triples it. | `DrawnBoil.MaxPixels` (default 2,097,152 — a 1448² bake) refuses anything larger outright, and the cache is keyed by SOURCE TEXTURE, so the forty cards wearing one 420×76 edge pay once. Only one screen is resident at a time; the measured worst case is the two big sheets, ≈ +16MB. |
| D1-B3 | **Eligibility is a signature test, not a name list.** | `DrawnBoil.InkTexture` | `Sweep` must never catch a photograph, a film sheet or a glyph. | Four gates, all positive: not a `TMP_Text`/`Text` (nor sharing an object with one); a `Texture2D` mapped WHOLE (`sprite.textureRect` = the texture, or `RawImage.uvRect` = 0,0,1,1); `isReadable` — which alone rejects every sheet loaded with `nonReadable: true`; and pure-white RGB wherever alpha ≠ 0, which is what `DrawnUI`'s rasteriser writes and no photograph does. The last gate is EXACT, not sampled: it rides the pass the bake already makes over every pixel, so a sparse card edge cannot be mistaken for an empty one. Refusals are cached per texture. |
| D1-B4 | **A graphic that rebuilds drops the boil for up to 125ms.** | `InkBoil.Put` | `Graphic.UpdateMaterial` re-sets the canvas renderer's texture to the sprite's own, so a colour/sprite/enable change reverts a boiling line to its bake. | The clock re-applies on every tick, so it self-heals within one held frame, and what shows in the meantime is the canonical bake — correct, never wrong. `OnDisable` puts the bake back deliberately, so a still screenshot is always the signed-off art. |
| D1-B5 | **Lockstep swapping reads as a blink.** | `InkBoil.Bind` | Forty elements turning over on the same tick is a strobe, not a boil. | Each instance takes a phase of 0/1/2 from its instance id, so the page holds a mix at every instant while the clock stays a single list walked once per tick. |
| D1-B6 | `System.Random(int.MinValue)` | `InkBoilBake.Build` | `Math.Abs(Seed)` inside the legacy constructor overflows on `int.MinValue` on some runtimes. | Every seed is masked to `& 0x3FFFFFF` before `+1`/`+2`, so it cannot be negative and cannot overflow. The instance phase is masked the same way. |
| D1-B7 | **A stretched bake magnifies its own boil.** | `DrawnBoil.DrawScale` | Amplitude is a TEXTURE quantity. The stage spotlight is a 104px disc drawn across a 400px pool, so a 1.1px texture move lands as 4px on the page — the one way this lane could break its own ceiling without any of its numbers looking wrong. | `Apply` measures `rect.size / texture.size` and refuses anything where `Amplitude × scale` would pass `MaxAmplitude`. Drawn below 1:1 is always allowed, because the move only shrinks. Proven in the shot: the spotlight piece takes no component while the six 1:1 pieces all do. |

## D1-3. Verified, not guessed

Measured by `BoilShot` on a six-piece kit (paper button, sheet, coral rule,
standing rule, hollow ring, filled ring) rendered through a real `Canvas` +
`Camera` at 900×620, holding the shared clock on each of its three drawings:

- **6 of 15 graphics** boiled. The cream fills, the drop shadows, the button's
  invisible hit rect, both lines of type and the blown-up spotlight disc were
  all refused.
- **Text carries no `InkBoil`** — asserted on the label, and the difference map
  is pure black over every glyph.
- **The spotlight carries no `InkBoil`** — a 104px bake drawn 200px wide is over
  the draw-scale ceiling and is left as it was.
- **Edges move, fills do not**: ~10,700 pixels differ between held drawings; the
  cream sheet interior and the filled ring's solid interior are **0 differing
  pixels** across all three.
- **Worst edge move between any two drawings: 0.919px** against a 1.5px ceiling.
- **Kill-switch**: with `RUNWAY_FX_BOIL=0` the same run attaches 0 components,
  the clock never wakes (`Ticking False`), and the three frames are byte-identical.

## D1-4. The seam

`DrawnBoil.Apply(Graphic, seed)` is the only entry point; `DrawnBoil.Sweep(root)`
is the same thing over a subtree. Both return null / 0 when the switch is off, and
neither bakes anything in that case. The clock builds itself on first registration
and disables its own behaviour when the last `InkBoil` goes down, so a build with
the switch off has no clock, no Update and no extra texture.

---

# D7 — THE MIX LANE — `Assets/Scripts/Audio/RunwayMix.cs`

An `.mixer` is a binary asset whose snapshots only the editor's own inspector writes
safely, so the asset was not authored — the SNAPSHOT PATTERN was ported instead:
named states, one table of per-group numbers, a 0.3s lerp between them, and one
Update driver over `AudioSource.volume` and `AudioLowPassFilter.cutoffFrequency`.

**Nothing in this project plays a sound yet.** `Boot.BuildFurniture` raises the only
audio object in the build (an `AudioListener` on the camera, `Boot.cs:101`) and C6
above records that `MusicManager` was never ported — a `grep AudioSource` over
`Assets/Scripts/` returns exactly that one line. So this lane ships the CONTROLLER
and the registration seam: the first `AudioSource` this codebase ever creates joins
the mix with one line beside it, and the five OST loops and fourteen sfx already
staged in `Assets/Art/music` and `Assets/Art/sfx` need nothing else from the mix.

The state table, per group (dB gain · low-pass cutoff in Hz, `open` = 22000, the
filter's ceiling, at which the component is switched off entirely):

|  | music | sfx | world |
|---|---|---|---|
| normal | 0 · open | 0 · open | 0 · open |
| curtained | **-6** · open | 0 · open | -6 · 1200 |
| binder | -3 · **900** | 0 · open | -6 · 700 |
| red (bed) | **-3 · 2400** | 0 · open | -3 · 2400 |

The three bold cells are the brief's contract (`Duck(0.5) == -6dB` while the curtain
is shut, the binder's muffle, the red week's thin filter). The rest is the same
authored logic applied to the other beds, with one rule holding all of it together:
**SFX is never ducked and never filtered**, because a muffled click reads as a
dropped input.

**Red is a bed, not a moment.** Cash below zero is a condition a run sits inside for
weeks; the curtain and the binder are moments that pass over it. So red LAYERS: its
dB adds and its cutoff takes the lower lid. Under the curtain in the red the music
sits at -9dB behind a 2400Hz lid, and when the curtain lifts the mix returns to the
RED bed rather than to calm. `SetState("normal")` clears the MOMENT only;
`SetRed(false)` is what leaves the red, and it is the same call that enters it.

**Kill-switch: `RUNWAY_FX_MIX=0`** — a runtime env read through `Env.Get`, not a
scripting define, so the D8 matrix toggles it without a recompile. Off means
`SetState` no-ops, `RegisterSource` refuses, no filter component is ever added and
every source keeps the volume its own code set. Absent or `1` is on.

## D7-1. Blocking risks — these stop a compile if they are wrong

| # | API | Where | Why uncertain | Fallback coded |
|---|-----|-------|---------------|----------------|
| D7-1 | `com.unity.modules.audio` in `Packages/manifest.json` | the whole file | `AudioSource` / `AudioLowPassFilter` are type-forwarded to `UnityEngine.AudioModule`; a manifest that drops the module fails exactly the way a `ParticleSystem` reference fails without `com.unity.modules.particlesystem`. | Nothing to code — the module IS in the manifest and both compile flavours resolve `UnityEngine.AudioModule.dll`. Listed so that nobody prunes it while trimming the manifest. |
| D7-2 | `AudioLowPassFilter.cutoffFrequency` | `RunwayMix.ApplyVoice` | The cutoff itself is stable back to Unity 4. Its neighbour is not: the resonance property was spelled `lowpassResonaceQ` (sic) for years before becoming `lowpassResonanceQ`, and the two are one keystroke apart. | The resonance is NEVER touched. The lane writes `cutoffFrequency` and `enabled`, nothing else, so the typo era cannot reach it. |
| D7-3 | `AudioLowPassFilter` needs an `AudioBehaviour` on the same GameObject | `RunwayMix.RegisterSource` | `[RequireComponent]` cannot auto-add an abstract type, so adding the filter to a bare GameObject throws instead of compiling a fix in. | The filter is only ever added to `src.gameObject` — the object of the AudioSource being registered, which satisfies the requirement by definition. |
| D7-4 | `MixGroup` sharing `RunwayMix.cs` | `Assets/Scripts/Audio/` | Convention F1 says one MonoBehaviour per file, named after the file. | Obeyed: `RunwayMix` is the file's only MonoBehaviour and carries the name; `MixGroup`, `Bus` and `Voice` are plain types, which F1 allows to share their owner's file. |

## D7-2. Runtime risks — they compile, but may not do what the game wants

| # | Thing | Where | Risk | Fallback coded |
|---|-------|-------|------|----------------|
| D7-5 | **`Update` does not run outside play mode**, so a headless probe cannot drive the mix by existing. | the driver | An editor-only harness would have had to fake `Time`, and a state controller that can only be proven in a window is a state controller nobody proves. | The whole mechanism is `public static void Tick(float dt)`. `Update` is one line that feeds it `Time.unscaledDeltaTime` (B13). A harness steps it with a fixed dt and reads exact numbers. |
| D7-6 | **Something else writes `AudioSource.volume`.** | `ApplyVoice` | A ported `MusicManager` crossfades by tweening volume — the same float this lane owns. Two writers on one float is a stutter nobody can debug from a log. | The mix MULTIPLIES: the volume at registration is the source's BASE and the state is a gain over it. If the volume moves behind our back the base is re-read from it through the gain we last applied, so a crossfade rides on top of a duck instead of fighting it. |
| D7-7 | The filter is **bypassed, not removed**, when the lid is open. | `ApplyVoice` | `enabled = false` is what keeps a fully-open filter from costing a DSP block, but a foreign script that toggles the component would fight it. | Nothing else in this build touches audio, and the toggle only fires when the lid crosses 22kHz, not per frame. `Unregister` / `Shutdown` destroy a filter this lane added and restore the cutoff and enabled flag of one it merely borrowed. |
| D7-8 | The gain lerps in **dB**, the cutoff **geometrically**. | `Tick` | A straight lerp of the amplitude ducks fast then crawls, and a straight lerp of Hz spends most of a sweep in the top octave: both read as a fault rather than a move. | Deliberate, and asserted: at half of the 0.3s the dB is the arithmetic midpoint and the cutoff is `sqrt(from × to)`. A lid that is not moving is copied rather than round-tripped through `log`/`exp`, which otherwise lands 22000 on 21999.99 and reads as a stray filter. |
| D7-9 | Only Music and World carry a filter component. | `RegisterSource` | Hard-coding "SFX is never muffled" would rot the moment the table changed. | `EverMuffled(group)` walks the state table itself, so a future state that puts a lid on SFX gives every SFX source its filter automatically. |
| D7-10 | The kill-switch is read **once** and cached. | `Enabled` | An env lookup per frame is a syscall nobody needs, but it means flipping `RUNWAY_FX_MIX` mid-run does nothing until `RefreshSwitch()`. Sources that tried to register while it was off are never adopted. | `RefreshSwitch()` is public and the harness uses it. In the game the switch is read before the first sound and never changes. |
| D7-11 | `volume` is the ONLY gain this lane touches. | `ApplyVoice` | A 3D source's audibility also rides `spatialBlend`, rolloff and distance — none of which the mix knows about. | Every sound in a 1536×1024 drawn game is 2D. If a positional source ever ships, its rolloff still applies underneath the mix's gain rather than being overwritten by it. |
| D7-12 | A destroyed source is dropped on the **next tick**, not at destruction. | `Tick` | The lists hold Unity object references; a screen swap can destroy a source while the mix still has it. | The tick loop walks backwards and removes any entry whose `Src` compares null, so a destroyed source costs one null check and then leaves. No event, no callback, no leak. |

## D7-3. Verified, not guessed

- **The whole state walk was run headless** (`Runway.EditorTools.MixProbe.Run`,
  `-batchmode -nographics -quit`): six dummy sources across the three beds, twelve
  state boards, **322 assertions, 0 failures**. Every group's dB and lid AND every
  individual source's `volume`, `cutoffFrequency` and filter `enabled` flag were
  compared against numbers derived in the probe by hand (`volume = base × 10^(dB/20)`,
  mid-lerp dB `= (from+to)/2`, mid-lerp Hz `= sqrt(from × to)`), never read back from
  the controller. Tolerances: 0.0005 on a volume, 0.05Hz on a lid.
- **Mid-lerp is asserted, not just the rest state**: at 0.15s of the 0.3s fade the
  curtain duck reads -3.0dB and the world's lid sits on 5138.09Hz, the binder reads
  -1.5dB / 4449.72Hz, the red bed -1.5dB / 7266.36Hz.
- **The layering is asserted**: curtained+red = -9dB/2400Hz on music, -9dB/1200Hz on
  world; binder+red = -6dB/900Hz on music, -9dB/700Hz on world; and `SetState("normal")`
  while red returns to -3dB/2400Hz rather than to 0dB.
- **The kill-switch is asserted from the outside**: with `RUNWAY_FX_MIX=0` a fresh
  source registers false, receives NO `AudioLowPassFilter`, and holds its own 0.9
  volume through a full second of ticks with the state set to `binder` and red on.
- **Handing back is asserted**: after `Shutdown()` every source is at its own level,
  the filters this lane added are destroyed, and a filter it merely borrowed is back
  at its own 5000Hz and enabled.
- **Audio is inaudible in batch mode** — there is no output device and `-nographics`
  gives the DSP nothing to run into. The probe proves the numbers the mix writes, not
  the sound they make; the ear test belongs to a run with a window and speakers.

## D7-4. The seam — the one-line hookups this lane needs

Nothing shared was edited. Six lines, in three files, install the whole lane:

| File · line | The line | Why there |
|---|---|---|
| `App/Curtain.cs` · after `RequestLoop();` (159, in `Close`) | `Runway.Audio.RunwayMix.SetState("curtained");` | the 0.3s duck runs UNDER the 0.45s sweep, so the music is already down when the drapes meet |
| `App/Curtain.cs` · after the guard (167, in `Open`) | `Runway.Audio.RunwayMix.SetState("normal");` | the unduck rides the 0.55s parting, not the frame after it |
| `App/Curtain.cs` · after `RequestLoop();` (184, in `SnapShut`) | `Runway.Audio.RunwayMix.SetState("curtained");` | the title opens on a shut house; the mix must already be behind it |
| `Game/BinderScreen.cs` · after `IsOpen = true;` (59) | `Runway.Audio.RunwayMix.SetState("binder");` | the clipboard comes up and the room goes muffled behind it |
| `Game/BinderScreen.cs` · in `OnDestroy()` (64) | `Runway.Audio.RunwayMix.SetState("normal");` | the binder's only exit; `Dismiss` destroys the object |
| `Game/GarageScreen.cs` · after `_lastBreath = t;` (471) | `Runway.Audio.RunwayMix.SetRed(State.Cash < 0);` | the room already re-reads the money on its own 12fps breath; the call is idempotent, so a repeat costs nothing and never restarts a transition |

Putting the curtain's three lines in `Curtain.cs` rather than at its call sites is
what makes the coverage total: `Boot.RevealTitle`, `TurnRunner.DropRoutine` and
`TurnRunner.RaiseCurtain` all go through those same three methods, and so will
anything added later. The call-site alternative is `TurnRunner.cs:107` / `:125` plus
`Boot.cs:227` / `:229` — three files instead of one, and it misses whatever comes next.

Registration is the OTHER half, and it belongs to whoever creates the first source:

```csharp
var src = go.AddComponent<AudioSource>();
Runway.Audio.RunwayMix.RegisterSource(src, "music");   // or "sfx" / "world"
```

No `Install()` call is needed anywhere — registering or setting a state raises the
driver. `RunwayMix.Install()` exists as the lane's single explicit entry point for a
harness that wants the driver standing before any sound does.

---

# D2 — THE INK-REVEAL LANE — `Effects/InkReveal.cs` + `Game/GarageScreen.InkReveal.cs`

Compiled and run on this machine: `-batchmode -quit -executeMethod
Runway.EditorTools.InkRevealFilm.Film` (WITHOUT `-nographics`) came back exit 0 with
zero compile errors and rendered its six frames through a real canvas.

**No shader ships.** The brief allowed a one-property `_Cutoff` shader; it was not
taken, so there is no shader-compile risk in this lane at all, and nothing here
depends on the render pipeline, the stencil buffer or a material variant.

## D2-1. Blocking risks — these stop a compile if they are wrong

| # | API | Where | Why uncertain | Fallback coded |
|---|-----|-------|---------------|----------------|
| D2-A1 | `GarageScreen` is **not** `partial` | — | The lane was specified as a `partial class GarageScreen` extension, but `GarageScreen.cs:31` declares `public sealed class GarageScreen` with no `partial`. A second `partial` declaration is CS0260, a hard error, and adding the keyword is an edit to a shared file. Same wall D1 hit on `DrawnUI`. | The hook is a **separate** static class, `GarageInk`, in the file the brief names (`Game/GarageScreen.InkReveal.cs`), and the hookup is one line. If the owner marks `GarageScreen` partial at integration, `GarageInk.Apply` moves across as a static member unchanged. |
| D2-A2 | `MonoBehaviour.StartCoroutine` on a `RawImage` | `InkReveal.Host` | `Graphic : UIBehaviour : MonoBehaviour`, so a UI component can host a coroutine — but Unity refuses to start one on a behaviour that is not active and enabled, and the room image is created disabled. | `Host()` returns the RawImage **only** when `isActiveAndEnabled` (which `Begin` guarantees by enabling it first), falls to `Boot.Instance`, and returns null if neither stands. A null host degrades to `Instant()` — the picture still lands, it just does not paint. |
| D2-A3 | No nested `MonoBehaviour` ships | — | The obvious shape was a small driver component on the cover object, but `AddComponent<T>()` on a **nested** type has no MonoScript behind it and the codebase's own rule (§F1) is one MonoBehaviour per file named after the file. | The reveal is a plain coroutine hosted on an object that already exists. This lane adds **zero** component types, so nothing can fail to attach. |
| D2-A4 | `Texture2D.LoadImage` / `EncodeToPNG` | `InkRevealFilm` (editor only) | Extension methods from `com.unity.modules.imageconversion`, which **is** in `Packages/manifest.json`. | The whole file is under `Assets/Scripts/Editor/`, so a player build never sees it. |

## D2-2. Runtime risks — they compile, but may not do what the look wants

| # | Thing | Where | Risk | Fallback coded |
|---|-------|-------|------|----------------|
| D2-B1 | **The reveal moves a COVER, not the picture.** | `InkReveal.Attach` | The painting is opaque from frame one; what animates is a full-rect child of the room image, tinted the wall's own cream, whose alpha is one of twelve 192x128 masks. Nothing masks, clips or re-composites the picture, so the room image's own material, texture and geometry are untouched. | If the cover is ever destroyed early or fails to build, `Begin` falls to `Instant` and the picture is simply there. There is no state in which the room is missing. |
| D2-B2 | `Mask` / `RectMask2D` deliberately NOT used | — | A stencil `Mask` would have done the same job through `UI/Default`'s `UNITY_UI_ALPHACLIP` keyword, which `StencilMaterial` enables — real, but it is an assumption about a built-in shader's keyword set, plus an extra draw call and a stencil write per reveal. | Not taken. A RawImage's own alpha channel needs no keyword and no stencil. |
| D2-B3 | **The cover is a CHILD of the room image, on purpose.** | `InkReveal.Attach` | `GarageScreen.BuildRoom` creates wall, floor and horizon, and `AdoptComposed` then puts the picture at **sibling index 1** — so the floor tint and the wobbled horizon rule draw *over* the painting. A cover placed as a sibling would have hidden them for one second and popped them back. As a child it sits under them, and they never move. | Verified in the film: the sage floor band and the ink horizon are continuous across both the painted and the unpainted halves of every frame. If someone later re-orders `_room`'s children, the cover follows the picture wherever it goes. |
| D2-B4 | 192x128 mask blown up 8x to the 1536x1024 stage | `InkReveal.Cut` | Bilinear upscaling IS the softening — a brush edge wants about 20px of ramp on this canvas and that is what 8x plus a 0.010 time-feather gives. A change to `FilterMode.Point` would turn every stroke edge into 8px stairs. | `filterMode` is set explicitly on every mask rather than inherited. The mask resolution is independent of the stage: a bigger stage only softens the edge further. |
| D2-B5 | **Equal paint per frame is computed, not authored.** | `InkReveal.Pace` | Laying fourteen strokes on an even time slice paints 92% of the room in the first half-second and then idles for the rest — measured, before the pass existed. `Pace` re-spaces the whole time field through its own distribution so each twelfth of the second lays a twelfth of the room. The remap is strictly increasing, so stroke order and stroke travel are untouched. | Measured after the pass, from the shipped masks: 91.0 / 82.6 / 74.2 / 65.7 / 57.5 / 49.0 / 40.5 / 32.1 / 23.5 / 14.9 / 7.0 / 0.0 percent still cream. Any future edit to the stroke table is paced automatically. |
| D2-B6 | **Two frames of deafness before a click can skip.** | `InkReveal.Play` | A late render is adopted the same instant the click that closed the beat is still down (`TurnRunner.LateScene`), so an ungated skip would be triggered by the very press that asked for the room. | `Deaf = 0.18f`; the same guard the binder uses against its own opening key. Legacy `Input` throughout, consistent with §A4. |
| D2-B7 | Held masks: 12 x 192 x 128 RGBA | `InkReveal.Masks` | 1.2MB on the GPU and the same again on the CPU (they stay readable so a harness can measure them). They do **not** depend on the picture, so they are built once per session and every weekly repaint is free. | `InkReveal.Forget()` drops them. Build cost is one pass over 24,576 pixels per mask, paid on the first painted room only. |
| D2-B8 | Per-frame cost | `InkReveal.Play` | One `cover.texture = masks[k]` every 83ms, which dirties one Graphic and rebuilds one quad — twelve times per reveal, and nothing at all on the frames between. After the coroutine starts there is no allocation: the masks are held, the clock is a float, and `yield return null` allocates nothing. | — |
| D2-B9 | The kill-switch is an **env var**, not a scripting define | `InkReveal.Enabled` | The brief suggested `RUNWAY_FX_REVEAL` as a define. A define is a Player Settings edit — a shared file — and cannot be flipped between two runs of the same build, which is exactly what the D8 kill-switch matrix needs. | Read through `Env.Get`, so it layers with `.env` and `keys.env` like every other switch in the game. Absent or `1` paints; `0` (also `off`/`false`/`no`) restores the old path. |

## D2-3. Verified, not guessed

- **The old behaviour is preserved byte-for-byte.** `InkReveal.Instant` is the three
  lines `AdoptComposed` runs today (`DrawnUI.Group` → `alpha = 0` → `FadeTo(1, 0.4s)`),
  so `RUNWAY_FX_REVEAL=0` is a true no-op rather than a different quiet behaviour.
- **The film renders the shipped code, not a copy of it.** `InkRevealFilm` rebuilds
  the garage's own layering and calls the real `InkReveal.Attach` / `InkReveal.Step`.
  Only the clock is substituted, because a coroutine does not run in edit mode.
- **The frames were eyeballed.** Step 0 is a tapered brush touching down at the lower
  left; step 2 is that one stroke travelled diagonally with a broken dry-brush tip;
  step 6 is a second stroke crossed over the first with cream still in all four
  corners; step 10 is ragged leftovers. It reads as painting, not as a wipe.
- **Every frame changes by the same amount** (see D2-B5), so the last step is no more
  of an event than any other and there is nothing to pop at the end: the twelfth mask
  is empty by construction, because every time in the field is clamped below
  `1 - Feather`.

## D2-4. The seam — the one-line hookup this lane needs

In `GarageScreen.AdoptComposed`, inside the `SheetLoop.LoadTexture` callback,
these three lines (**`GarageScreen.cs:362-364`**):

```csharp
var g = DrawnUI.Group(_composed.rectTransform);
g.alpha = 0f;
boot.StartCoroutine(DrawnUI.FadeTo(g, 1f, 0.4f));
```

become one:

```csharp
GarageInk.Apply(_composed, tex);
```

`HideDrawnRoom(true)` on the line above stays exactly where it is. The two lines
above that (`_composed.texture = tex; _composed.enabled = true;`) may stay or go —
`Apply` does both itself.

That is the whole hookup. `AdoptComposed` is the only door a painted room comes
through: the first room of a run, every weekly repaint, and the late render that
lands after the beat has closed all reach it (`TurnRunner.cs:320` and `:352`), so
D2b is covered by this one line and no other.
