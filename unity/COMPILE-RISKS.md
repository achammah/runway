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

| N16 | **`rebuild/s` reads 0.0 for every baked SHEET loop, and the blame names nobody.** | measured, not theorised | On the first full run the only repainters found were: the title's frame SEQUENCE (`Title/painting/film [RawImage]`, 12.3/s), the birth STATUS LABEL (`Birth/label [TextMeshProUGUI]`, 12/s), the curtain's considering line (2118/s — see below), the dice shade/felt fade, and the garage's red vignette (12/s). **No `SheetLoop` in sheet mode appears at all** — not the birth loop, not the how-to film, not the curtain sway, not the dice cup. `07 howto` has no repainter whatsoever. | The instrument is not the suspect: `RawImage.uvRect`'s setter calls `SetVerticesDirty()` when the value changes (read directly from `com.unity.ugui@…/Runtime/UGUI/UI/Core/RawImage.cs:106-119`), and the title's sequence-mode loop IS caught, so the queue read demonstrably sees RawImage repaints. Either the sheet-mode loops are sitting on one frame, or something upstream of `SheetLoop.Apply` is. Reproduce with `RUNWAY_UPERF_BLAME=1` and read the "who is repainting" block. |
| N17 | **The curtain's considering line rebuilds EVERY frame.** | `Curtain.Update` | Blame: `top/curtain/label [TextMeshProUGUI]` at one rebuild per frame — 2118/s uncapped, which is ~30/s at the shipped cap, against Godot's measured 12-25 redraws/s for the whole screen. `Update` writes `_line.text`, `_line.color` and a new `SetTopLeft` on every tick instead of on the 12fps clock every other loop in this build rides. A TMP text rebuild is the most expensive rebuild there is. | Not this lane's to fix; it is E2's whole point, and now it has an address. |

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
| D1-B2 | **Three copies of every ink texture, and a bake to make them.** | `InkBoilBake` | Ink bakes are full-card-size even when only the border is inked, so boil triples both the memory and the build cost. MEASURED on the biggest bake the game makes, the 1140×880 keys/how-to sheet (a 1156×896 edge): `DrawnUI` rasterises it in **41.0ms**, and the boil redraws it twice in **42.3ms** more, for **7MB** of texture. That is a screen-build frame already over E5's 50ms hitch bar before this lane touched it, and this lane doubles it. Every other bake is small: the whole six-piece kit swept in **28.4ms**, JIT warm-up included. | Three mitigations, all in this lane's own files. The redraws start as a memcpy of the bake and re-sample only the blocks that can see ink — a tenth of a card edge. The CPU copy is dropped on upload (`KeepReadable = false`), halving what three copies would cost. And `DrawnBoil.MaxPixels` is the knob: at its default 2,097,152 the big sheets boil; drop it to `1 << 19` and exactly those two stop, capping the added build cost at ≈25ms while every card the player actually stares at keeps living. **N15's call, with the numbers above.** With the switch off the same probe adds 0.2ms and 0MB. |
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
- **Cost**: the whole kit swept in 28.4ms (JIT warm-up included), 0.5ms with the
  switch off. Steady-state is one texture pointer per boiling line, eight times a
  second, from one Update — no allocation, no canvas rebuild.
- **Kill-switch**: with `RUNWAY_FX_BOIL=0` the same run attaches 0 components,
  the clock never wakes (`Ticking False`), the three frames are byte-identical,
  and the biggest bake gains 0.2ms and 0MB.

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

---

# P0 — THE PARITY HARNESS — `App/UnityShots.cs` + `.Camera` + `.Fixtures` + `.Poke`

The twin of `game/tests/{new_screens,select,howto,birth,traits,binder}_shot.gd`:
`RUNWAY_USHOTS=<dir>` turns the player into a photographer, writes the 23 PNGs
under the identical filenames and quits. Unset, none of it exists at runtime.
Every API below was verified by a live run (23/23, none flat) — the notes say
which ones were guessed wrong first and what the symptom was.

## P0-1. Blocking risks — these stop a compile if they are wrong

| # | API | Where | Why uncertain | Fallback coded |
|---|-----|-------|---------------|----------------|
| P0-1 | **`UnityEngine.ScreenCapture` does not compile in this project.** | `UnityShotsCamera.Grab` | `com.unity.modules.screencapture` is NOT in `Packages/manifest.json`, so the type is unreferenced by the compiler. Verified with a one-line probe file: `error CS0103: The name 'ScreenCapture' does not exist in the current context`. | It is reached through `Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule")` + `MethodInfo.Invoke`, so the file compiles today **and** uses the native capture — the module IS shipped inside the player even though the compiler cannot see it, and the live run logs `USHOTS shutter: ScreenCapture.CaptureScreenshotAsTexture()`. If the type ever resolves to null, `Texture2D.ReadPixels` off the back buffer takes the same picture. Adding the module to the manifest changes nothing here; it only lets a future file name the type directly. |
| P0-2 | `Texture2D.EncodeToPNG()` | `UnityShotsCamera.Shoot` | An extension method from `UnityEngine.ImageConversion`, i.e. a module — the same trap as P0-1. | `com.unity.modules.imageconversion` **is** in the manifest, so it resolves. If it is ever removed, this is the second thing that breaks. |
| P0-3 | `Application.Quit(int exitCode)` | `UnityShots.Leave` | The int overload is 2019.3+; the no-arg one is ancient. | Only the player takes this branch; the editor branch sets `EditorApplication.isPlaying = false` inside `#if UNITY_EDITOR`, which is legal in a runtime file. |
| P0-4 | `BindingFlags.DeclaredOnly` + a manual walk up `Type.BaseType` | `UnityShotsPoke` | `GetMethods` with `NonPublic` does not return private members of base types, so the walk is required, not decorative. | A member that cannot be found logs `USHOTS POKE MISS` with the type and the name, and the run's summary lists every one — a harness that silently photographs the wrong state is the failure this guards. |

## P0-2. Runtime risks — they compile, but the picture can still be wrong

| # | Thing | Where | Risk | Fallback coded |
|---|-------|-------|------|----------------|
| P0-5 | **`Application.runInBackground` must be true or the harness hangs forever.** | `UnityShots.Awake` | Observed on the first live run: launched from a terminal the window never gains focus, the player stops drawing, and `WaitForEndOfFrame` never returns — no error, no timeout, no shots. | The one line in `Awake`. It is the reason the harness completes at all; do not remove it. |
| P0-6 | `WaitForEndOfFrame` in `-batchmode` | `UnityShotsCamera.Shoot` | Batch mode draws nothing, so the same hang. | `Application.isBatchMode` is checked before the first shot and refused with a message and exit code 4. `howto_shot.gd` carries the same warning for Godot. |
| P0-7 | `Environment.SetEnvironmentVariable("RUNWAY_SHOT", …)` reaching `Boot` | `UnityShots.Install` | This is what puts Boot on its harness path with **no edit to Boot** — no studio card, no keys gate, no curtain over the title, no paid render. It only works because `Install` is `BeforeSceneLoad` and `Boot.Launch` is `AfterSceneLoad`, which Unity orders strictly, and because `Env.Get` reads the process environment before its files. | The set is in `try/catch`, and the harness raises every screen through `Boot.Go` itself, so a failed set costs the curtain (which `HideCurtain` then deactivates) and nothing else. |
| P0-8 | `Screen.SetResolution(1536, 1024, false)` | `UnityShots.SizeTheStage` | The Godot reference PNGs are 1536×1024. A player launched without `-screen-width/-screen-height` may open at another size, and the editor's Game view ignores the call outright. | Every shot logs its real size and a mismatch prints `SHOT SIZE <name> …` naming the launch flags. The live run produced 23 × 1536×1024. |
| P0-9 | `RenderTexture.active = null` before `ReadPixels` | `UnityShotsCamera.ReadBackBuffer` | Without it a bound render texture would be read instead of the finished frame. | Only reached when the ScreenCapture path is unavailable. |
| P0-10 | `Texture2D.GetPixels32()` per shot | `UnityShotsCamera.Spread` | ~6MB per 1536×1024 capture, 23 times, to measure the luminance spread for the flat check. | Strided (prime stride, ≈200k samples), the texture is destroyed immediately after, and a throw is caught — the PNG is already on disk before the check runs, so the evidence is never lost to the checker. |
| P0-11 | `RunDriver.State` has a **private setter** | `UnityShotsFixtures.FreshRunState` | The Godot screens are handed a state (`bk.setup(st2)`, `b.setup(s)`); the Unity twins read `RunDriver.Current.State`, which cannot be assigned from outside. | A fixture calls the driver's own public `BeginFreshRun(false)` and writes its fields onto the state that comes back. No private seam, and every screen that reaches for the run finds the same object. |
| P0-12 | One frame of screen overlap in `Boot.Go` | every shot | `Go` adds the new screen and destroys the previous one a frame later, so a capture in that frame would photograph two screens. | Every shot is at least 0.3s after its `Go`, and the waits are transcribed from the `.gd` harnesses, so the overlap frame is never the photographed one. |

## P0-3. Fixture divergences — the same company, said in Core's words

Three fields cannot be transcribed literally because Core is typed where Godot's
`GameState` is a bag of dictionaries. All three are in `binder_shot.gd`:

| The `.gd` writes | The fixture writes | Why |
|---|---|---|
| `{"role": 0, "commitment": 0}` | `Role = "Sales"`, `Commitment = "Full-time"` | They are indices into `founder_draft_screen.gd`'s `ROLES` / `COMMITMENTS`; `Core.Cofounder` holds the words. Note that `binder.gd` prints the raw value (`str(cf.get("role"))`), so the Godot crew tab reads **"Nico Ferreira — 0 cofounder"** where the Unity twin reads **"— Sales cofounder"**. The port is right and the original is not; the shots will differ on that one line. |
| `metric_history[i]["product"]` | dropped | `MetricSnapshot` has no `product`, and nothing reads it — `binder.gd`'s `_series` only ever asks for cash, customers, morale, debt and hype. |
| `metric_history[i]["week"]` | `Wk` | The harness writes `week`; the engine itself writes `wk` (`sim_engine.gd:380`). Neither is read by a chart. |

## P0-4. The seam — what this lane needs from anybody else

**Nothing.** No hookup line, no shared-file change, no manifest entry. `Install`
is the single entry point, the env var is the switch, and
`RUNWAY_FX_USHOTS_OFF` in Scripting Define Symbols compiles all four files out.

## P0-5. What the first live set FOUND — for the B lanes, not for this one

All three are in shipped files this lane may not edit, and all three are why the
harness exists. Evidence: the strict set, plus the same set re-shot with
`RUNWAY_USHOTS_WARM=2.5` and `=10`, which is what separates "slow" from "broken".

1. **`SheetLoop.PlaySheet` delivers no frame at all in a player build; only
   `PlaySequence` works.** The per-frame path (title film `frame_NN.png`, keys
   mascot `chr_loop_hacker_NN.png`) paints correctly once it lands. The grid-sheet
   path — `title/howto_{1,2,3}.png`, `title/birth_intro.png`,
   `title/birth_loop.png`, and by inspection `title/curtain_loop.png` — is still an
   empty frame after **ten seconds** of settle. The files are present in the built
   `StreamingAssets/Art/title/` (12–16MB each) and nothing is logged. So six of the
   23 shots (`n3_howto`, `howto_p1..p3`, `n5_birth_fullframe`,
   `birth_intro_check`, `birth_loop_check`) currently photograph an empty film
   frame, and the how-to and the birth screen ship with no picture in them.
2. **An untextured `SheetLoop` is an opaque WHITE rectangle, not nothing.**
   `SheetLoop.Awake` sets `_target.color = Color.white` on a `RawImage` whose
   `texture` is still null, and UGUI draws that as a white quad — where Godot's
   empty `TextureRect` draws nothing. That is why finding 1 reads as a bright hole
   rather than as cream paper. `BirthScreen` already does the right thing for its
   logotype (`_logo.enabled = false` until `ArtCache.Load` returns); the same move
   inside `SheetLoop` would make every slow load invisible instead of loud.
3. **The title film's first frame takes longer than the 2.1s the .gd harness
   allows.** `n1_title_menu` is blank in the strict set and fully painted with
   +2.5s, and `n2_slot_panel` 0.8s later shows the painting through its veil. This
   one really is latency, not finding 1.
4. **The founder's idle loop never starts for the DEFAULT founder.**
   `FounderDraftScreen.OnBuild` ends with `ShowPage(0)` and then
   `_select.Select(0, false)`, which calls `_hero.Play(...)` while page 1 is still
   inactive — Unity logs *"Coroutine couldn't be started because the the game
   object 'hero' is inactive!"* twice per run. Arriving at CHOOSE YOUR FOUNDER
   without pressing left/right therefore shows a founder who is not breathing.
   `select_norm_check` and `traits_card` both look right only because the harness
   re-selects while the page is up, exactly as the two `.gd` harnesses do.

---

# D6 — THE SOFT-LIGHT LANE — `Effects/GlowSprites.cs` + `Resources/Shaders/RunwayGlow.shader`

NOT URP. The checklist settles it (`DEFERRED: URP migration — soft-light sprites
chosen instead`), so light here is what an illustrator does: additive radial washes
laid over the picture, and one multiply layer over the room when the money runs out.
Two new files, no existing file touched.

Kill-switch is the **environment**, not a scripting define: `RUNWAY_FX_GLOWS` absent
or `1` is on, `0` (also `off`/`false`) is off, read once and cached. Off means
`Apply` returns null, `MakeGlow` returns `Glow.Inert`, and **no GameObject and no
texture are created at all** — nothing to compile out, and D8 can toggle the lane
without a recompile. `GlowSprites.ForgetSwitch()` exists for a harness that changes
the environment after the first read.

## D6-1. Blocking risks — these stop a compile if they are wrong

| # | API | Where | Why uncertain | Fallback coded |
|---|-----|-------|---------------|----------------|
| D6-A1 | **`UI/Default` cannot be made additive.** Unity's UI shader hard-codes `Blend SrcAlpha OneMinusSrcAlpha` and exposes no `_SrcBlend`/`_DstBlend`, so `new Material(Shader.Find("UI/Default"))` + `SetFloat("_SrcBlend", …)` silently does nothing. `Sprites/Default` cannot do it either — it premultiplies (`c.rgb *= c.a`) before blending, so alpha 0 kills the colour instead of making it add. | `AdditiveMaterial`, `MultiplyMaterial` | This is the one real API question in the lane, and the answer is that there is no runtime-only way to get an additive UI Image. Runtime shader **compilation** does not exist in Unity either. | A 60-line `Runway/Glow` shader ships: UI/Default with `Blend [_SrcBlend] [_DstBlend]` as float properties, so ONE shader serves both the additive glow (`SrcAlpha One`) and the red multiply (`DstColor Zero`). Both materials are built at runtime from it. |
| D6-A2 | The shader could be stripped out of a player build | `GlowShader` | A shader referenced by nothing but a `Shader.Find` string is a stripping candidate, and a stripped shader is a magenta room. | It lives in **`Assets/Resources/Shaders/`** — everything under a Resources folder is always included — and is loaded with `Resources.Load<Shader>("Shaders/RunwayGlow")` first, `Shader.Find("Runway/Glow")` second. If both miss, the material is null, `Image.material` stays default, and the glows draw with **straight alpha blending**: they wash toward their own colour instead of adding. Degraded, logged once, never broken. The red overlay's fallback is nearly exact — an alpha veil at 0.19 of a dark cold colour is within a few percent of multiplying toward 0.85. |
| D6-A3 | `Component.GetComponentInParent<T>(bool includeInactive)` | `MakeGlow` | The `includeInactive` overload is 2020.1+; the project is Unity 6, so it exists. It matters because a draft page builds itself while inactive and the default overload would silently skip the rig. | Compiled and exercised; a null rig only costs the light its tick (no breathing, no red drain), never an exception. |
| D6-A4 | `ZTest Always` instead of `ZTest [unity_GUIZTestMode]` | the shader | The built-in resolves to `Always` for a ScreenSpaceOverlay canvas (which is what `Boot` builds) and to `LEqual` for the ScreenSpaceCamera canvas the evidence harness renders through. Pinning it makes both paths identical rather than depending on a global somebody else sets. | If a world-space canvas is ever put behind 3D geometry, this shader would draw through it. There is no 3D geometry in this game. |

## D6-2. Runtime risks — they compile, but may not do what the look wants

| # | Thing | Where | Risk | Fallback coded |
|---|-------|-------|------|----------------|
| D6-B1 | **Additive light over a CREAM wall clips.** | the drawn garage | `DrawnUI.Cream` is 0.949 — anything added to it goes to paper-white and takes the drawing with it. | The gradients are **saturated rather than bright**: warm runs (1.00, 0.90, 0.71) at the filament to (1.00, 0.50, 0.14) at the rim, so the room gains temperature long before it gains luminance, and the pool alphas are 0.13. Measured on the shipped cream room: only the bulb's own core clips, which is what a bulb does. The evidence set carries that shot (`d6-garage-drawn-cream.png`) next to the dark-room one so the difference is on the record rather than in someone's head. |
| D6-B2 | **The light must sit over a COMPOSED painting too.** | `_room` child order | A painting arrives with its own lighting baked in, so a second pool could double-light it. | Kept on, deliberately: `scene_room.gd`'s `ambient()` does exactly this in the Godot build — an additive delta laid over a composed still, "and where the bulb brightens it also brightens a character standing under it, which is correct rather than a bug". The glow layer is a child of `_room`, and `AdoptComposed` puts the painting at sibling index 1, so the light stays above it without any hookup. |
| D6-B3 | **The room appends children after we install.** | `Pin()` | `SyncRoom` → `BuildCrew` destroys and rebuilds the crew every week, and rebuilt crew rects are appended to `_room` — above the light and above the red multiply. | The rig compares two sibling indices per frame (two int reads, no allocation) and calls `SetAsLastSibling` only on the frame it is actually wrong, i.e. once a week. Pinning is **off** for the select stage, where the beam is deliberately installed *under* the pages. |
| D6-B4 | Writing `Graphic.color` dirties the canvas mesh | `Alphas()` | A glow that re-tinted every frame would be a rebuild storm on a full-screen quad (E2 wants zero rebuilds outside animation frames). | Breathing rides `localScale` — a transform write, no mesh rebuild — quantised to the room's own 12fps clock, so the swell costs nothing a canvas notices. Colour is written **only while the red state is easing** (0.9s), and then only when the value moves by more than 0.002. |
| D6-B5 | The laptop glow could burn over an empty spot | `Glow.FollowObject` | `item_itm_laptop` is only visible when the founder owns a laptop, it moves when its drawing lands and re-fits, and it is hidden under a painting. A glow at fixed coordinates would be wrong in all three cases. | The light asks the room where its object is: `_host.Find("item_itm_laptop")`, then tracks that rect's centre and `activeInHierarchy` on every beat. If the name is ever changed the glow falls back to the transcribed centre (478, 586) and simply stops following. |
| D6-B6 | `Graphic.material` never reads back null | `Install` | The getter answers with the default UI material when nothing was assigned, so `_red.material != null` is not a test of anything. | The question is asked of `MultiplyMaterial()` itself, and the answer is cached in `_redIsMultiply`. |
| D6-B7 | `Object.Destroy` is a no-op in edit mode | `Glow.Kill` | The evidence harness runs in the editor, where a deferred destroy never happens. | `GlowSprites.Gone` picks `DestroyImmediate` when `!Application.isPlaying`. In a player build the branch is dead. |
| D6-B8 | The static red flag outlives a screen | `SetRed` | `_redWanted` is static, so a rig installed later inherits whatever the last screen said. | Correct on purpose — a room rebuilt while cash is still negative comes up already cold — and the room re-asserts it every frame anyway. `RedAmount` starts at 1 rather than easing in that case, which is the right answer: the week did not just turn red. |
| D6-B9 | Two red layers on one screen | the garage | `GarageScreen` already owns `_redVignette`, a **warm** red pulse on `Rect` driven by `WeeksInRed`. This lane's overlay is a **cold** multiply inside `_room`. | They stack rather than fight — the vignette pulses at the edges over everything, the multiply drains the room under it — but if the two ever read muddy together, the vignette's alpha is the knob (`GarageScreen.cs:477-484`). Flagged for the integrator; not touched by this lane. |

## D6-3. Verified, not guessed

Rendered headless with a real graphics device (`-batchmode -quit`, **no**
`-nographics`) through `Assets/Scripts/Editor/GlowShots.cs`, five frames at
1536×1024 into the scratchpad, over a stand-in room carrying the garage's real
geometry and the real object drawings off disk:

- **The additive material rasterises.** The bulb reads as a hot amber core with a
  pool under it; the laptop reads cold against it. Not asserted — photographed.
- **Warmth is a number, and it inverts.** Mean red-minus-blue on the wall under the
  bulb: **+0.068 normal → −0.010 in the red** (115% of the warmth gone), and the
  unlit corner goes **+0.012 → −0.020**, i.e. across zero into cold. The lit wall is
  36% dimmer. `SetRed(true)` settles to 1.00 over 0.9s.
- **The breath is ±3% over 4s, measured.** A still cannot show a swell, so the bulb's
  own scale was sampled every 0.5s across one full cycle:
  `1.000 0.982 0.970 0.976 0.996 1.018 1.030 1.021 1.000` — swing 0.970 → 1.030,
  i.e. ±3.0%, one period per 4.0s, quantised to the room's 12fps beat.
- **The kill-switch is inert, not hidden.** With `RUNWAY_FX_GLOWS=0`, `Apply`
  returns null, `MakeGlow(...).Live` is false, and the shot is the bare room.
- **The bake is deterministic.** Two runs produced byte-identical numbers; the
  wobble harmonics and the grain are seeded (`System.Random(47)` / `(91)`).
- **Cost.** Two 256² RGBA textures (512KB, baked once), four extra transparent quads
  in the room and three on the select stage, one `Update` doing two int compares and
  a `sin` at 12fps.

## D6-4. The seam — the three one-line hookups this lane needs

```csharp
// 1. GarageScreen.OnBuild, after `_room = DrawnUI.FullRect(Rect, "room");` and
//    BuildRoom()/BuildHud() — the bulb pool, the laptop glow and the cold overlay:
Runway.Effects.GlowSprites.Apply(_room, Runway.Effects.GlowScene.Garage);

// 2. GarageScreen.Update, beside the existing `_redVignette` block — the red state:
Runway.Effects.GlowSprites.SetRed(State.Cash < 0);

// 3. FounderDraftScreen.OnBuild, immediately after the `env/stage.png` block and
//    BEFORE the pages are built, so the beam sits over the stage and under the UI:
Runway.Effects.GlowSprites.Apply(Rect, Runway.Effects.GlowScene.SelectStage);
```

No `using` is needed at any of the three sites. `Apply` is idempotent per host and
returns null when the switch is off; `SetRed` is safe to call every frame and safe
to call when nothing is installed.

---

# D3 — THE BEAT-TEXT LANE — `Game/ReadingBeat.TextFx.cs` + `Game/BeatInkSettle.cs`

The reading beat already writes its paragraphs in rather than printing them
(`ReadingBeat.WriteIn` walks `maxVisibleCharacters`, which is Godot's
`visible_ratio` tween ported). But a character that simply becomes visible POPS.
This lane puts the reveal on a pen instead: 40 characters a second, each one
falling the last 2px onto its line and inking up as it lands, the verdict word
punching once when it arrives, and the die that turned the week stamped into the
sentence beside its number. Two new files, no existing file touched.

Kill-switch is the **environment**, not a scripting define: `RUNWAY_FX_TEXT`
absent or `1` is on, `0` is off, read once and cached. Off means `Apply` hands
the caller its own timing back and touches nothing at all — no component, no
rewritten sentence, no chit — so the beat reads exactly as it ships today and D8
can toggle the lane without a recompile. `ReadingBeatText.Reread()` exists for a
harness (or the keys screen) that changes the environment after the first read.

## D3-1. Blocking risks — these stop a compile if they are wrong

| # | API | Where | Why uncertain | Fallback coded / what was verified |
|---|-----|-------|---------------|------------------------------------|
| D3-A1 | **`ReadingBeat` is not `partial`, so this lane could not extend it.** | the whole lane | The brief asked for `ReadingBeat.TextFx.cs` as a `partial class`. `ReadingBeat.cs:31` declares `public sealed class ReadingBeat : MonoBehaviour` with no `partial`, and a second declaration adding the keyword is **CS0260**, not a warning. Adding the keyword would have been an edit to a shared file, which the parallelism contract forbids. | Shipped as a static class (`ReadingBeatText`) plus its own MonoBehaviour (`BeatInkSettle`) in the same file name the brief asked for. Nothing about the seam changes: one static `Apply(TMP_Text, float)`. **The next lane that wants a `partial` on a screen must ask the integrator for the keyword first.** |
| D3-A2 | `TMP_TextInfo.CopyMeshInfoVertexData()`, `TMP_Text.UpdateVertexData(TMP_VertexDataUpdateFlags)`, `TMP_Text.havePropertiesChanged`, `TMP_MeshInfo.vertices/colors32`, `TMP_CharacterInfo.origin/xAdvance/baseLine/lineNumber` | `BeatInkSettle` | The whole lane is TMP mesh surgery, and these are the members that make it possible. | **Read out of the resolved package before a line was written** (`Library/PackageCache/com.unity.ugui@…/Runtime/TMP/`), not remembered: all present and public. |
| D3-A3 | **The manifest's TMP version is not the one that compiles.** | ledger baseline | `Packages/manifest.json` still says `com.unity.textmeshpro 3.0.9` + `com.unity.ugui 1.0.0`. `packages-lock.json` resolves **`com.unity.ugui 2.0.0` and `com.unity.textmeshpro 5.0.0`, both `builtin`** — Unity 6 folded TMP into UGUI exactly as risk A1 predicted, and `DrawnUI` is already using the merged-only `textWrappingMode`. | Nothing to do; recorded so the next lane reads the LOCK file rather than the manifest when it wants to know what TMP it is writing against. |
| D3-A4 | One MonoBehaviour per file, named after the file (convention F1) | `BeatInkSettle.cs` | A component defined in `ReadingBeat.TextFx.cs` would have a file name no class can match, and `AddComponent` of a class Unity never made a `MonoScript` for is a runtime failure, not a compile one. | The driver lives in `BeatInkSettle.cs`; `ReadingBeat.TextFx.cs` holds only static API and tuning. |

## D3-2. Runtime risks — they compile, but may not do what the reading wants

| # | Thing | Where | Risk | Fallback coded |
|---|-------|-------|------|----------------|
| D3-B1 | **`LateUpdate`, not a coroutine.** | `BeatInkSettle.Step` | `ReadingBeat.WriteIn` moves `maxVisibleCharacters` from the update phase, and TMP regenerates the whole mesh whenever it changes. A coroutine (which also resumes in the update phase) can be scheduled either side of it, so half the frames would have their vertex writes thrown away by TMP's regeneration and the newest letters would flicker. LateUpdate is guaranteed after every coroutine and before the canvas draws. | `Step(float dt)` takes its delta rather than reading the clock, so the identical code runs frame by frame in an editor harness with no play mode. Inside it: `if (havePropertiesChanged) ForceMeshUpdate()` **first**, which is what clears TMP's own dirty flags, then the vertices, then `UpdateVertexData`. Nothing can regenerate after us. |
| D3-B2 | **Two clocks drifting reads as a click.** | `_cps` / `Pace` | The lane owns the frontier and `WriteIn` still writes it too. An external jump of more than `LeapChars` (8) is deliberately read as the beat's own skip. A flat 40cps would have drifted away from `WriteIn`'s pace on any paragraph over 260 characters (where the beat's 6.5s ceiling bites) and tripped that detector within half a second. | The pace is read back OUT of the beat's own envelope: `cps = count / Clamp(count/40, 0.3, 6.5)`. Nominally 40; a very short line is still savoured over 0.3s and a very long one still never exceeds 6.5s, because the next beat is already on its way. Both clocks are then the same clock by construction. |
| D3-B3 | **The vertex cache assumes layout does not move.** | `Recache` | Every frame writes absolute positions from one copy taken at install. If TMP re-laid the text out, the copy would draw the OLD layout. | `maxVisibleCharacters` culls geometry but line-breaking runs over the whole string, and the body labels are `TopLeft`, so nothing re-centres: positions are stable by construction. It is still checked every frame (character count, and the rect size to ±0.5px) and re-copied if either moves, at most 4 times before the lane gives up. Giving up **lands the paragraph** rather than leaving it half-written. |
| D3-B4 | Per-frame allocation | `Paint` | "No GC per frame" is the bar, and TMP vertex work is the classic way to miss it. | The vertex copy, the verdict runs and the chit are built once in `Install`; character timings are arithmetic, not a table (`Born(i) = (i+1)/cps`). The per-frame loop writes into arrays that already exist and calls one `UpdateVertexData`. The one text regeneration a frame is the cost the beat **already pays** — `WriteIn` changes `maxVisibleCharacters` on two frames in three at 60fps — it is only moved earlier in the frame. |
| D3-B5 | **Runtime `TMP_SpriteAsset` was rejected, not forgotten.** | the die chit | The brief allowed it if it were provably safe. It is not: a runtime sprite asset needs a populated `TMP_SpriteCharacter`/`TMP_SpriteGlyph` table AND a material on the `TMP_Sprite` shader, which lives in the TMP **essential resources** — the same import risk B2 already flags as an owner action. A missing sprite shader is an invisible or magenta glyph inside a sentence. | Shipped as the brief's fallback: a positioned `RawImage` child of the label, placed from the laid-out geometry, `raycastTarget = false`, clipped by the beat's existing `RectMask2D` like any other graphic, fading and settling on the same curve as the character it rides on. |
| D3-B6 | **Opening a gap in the sentence can reflow it.** | `OpenDieGap` | The chit needs room, so the text is rewritten with a run of spaces after "The die came up 14." — and `ReadingBeat.Reveal` has ALREADY measured that block's height from `preferredHeight` and already advanced `_columnH`. A paragraph that grew a line would print into the beat below it. | The gap is sized from the width one space actually buys in this font (read off the space the sentence already has), inserted once, and then **verified**: same `lineCount`, and the character before the gap on the same line as the one after it. Either check failing puts the original string back and ships no chit. The wrap POINT may move (it does in the evidence frame); the line count may not. |
| D3-B7 | The die sheet is 4096×2560 and the beat must not hitch | `ReadingBeatText.DieTexture` | A synchronous decode of a 10MB PNG on the main thread is a >50ms spike, which E5 forbids. | Asked of `ArtCache` first, which in the game is always a hit: the cup played `dice/roll_NN.png` seconds earlier in the same turn. Then `ArtCache.Load` (async) when a `Boot` exists. The straight `File.ReadAllBytes` + `LoadImage` rung only runs when there is **no `Boot` at all** — an editor harness, which has no coroutine pump and no frames to cost. In a player that branch is dead. |
| D3-B8 | The settled die's crop is hard-coded | `DieCropX/Y/W/H` | Cropping the die out of the last cell of the roll sheet needs to know where inside that 512px cell the drawing sits, and reading pixels at runtime is not possible (the sheets load `nonReadable`). | **Measured, not guessed**: the alpha bounding box of frame 39 was taken on all twenty sheets. Nineteen of them read x 121..388, y 99..410 and the other three are within 2px. The constant is that box plus a pixel. If the dice art is ever re-rendered, this is the one number to re-measure. |
| D3-B9 | **The capitalised verdicts are dead words in BOTH builds.** | `Verdicts` | The checklist asks for BRILLIANT/BACKFIRED to punch. `main.gd:1756` builds that band table into a local `band` and then never prints it — the sentence carries the plain-words version instead (`It lands beautifully.` / `It goes wrong.`), and `TurnRunner.cs:232-240` ports exactly that. Emphasising only the capitals would have been an effect no player could ever see. | Both sets are in the table, matched longest-first and boundary-anchored so `It lands.` can never match inside `It lands beautifully.` and `BRILLIANT` can never match inside a longer word. If the integrator ever wants the capitals on screen, `beat.Say("", band)` in `TurnRunner` is the whole change and this lane already answers to it. |
| D3-B10 | A verdict that wraps across two lines | `FindVerdicts` | Scaling a run about the midpoint of its bounding box would shear it if half the word is on the next line. | Runs are cut at line ends and each segment scales about its own line's centre, sharing one fire time. Measured on the evidence frames: the phrase grows 234px → 246px (1.051x against 1.052x reported) and the `·` beside it does not move at all, so the punch is bounded to the verdict. |
| D3-B11 | `ReadingBeat._draining` would go stale if the lane replaced `WriteIn` | the seam | `_draining` is private and it is what makes a click during a write-in SKIP rather than CLOSE the beat (`OnClick`). A hookup that swapped `WriteIn` out for this lane would leave it permanently false, and a click on the last paragraph would close the beat mid-sentence. | The hookup deliberately keeps `WriteIn` running and only changes the number it is given. `WriteIn` keeps the bookkeeping and the skip latch; this lane owns the vertices and quietly rewrites the same frontier a moment later in LateUpdate. Nothing about the beat's click semantics moves. |

## D3-3. Verified, not guessed

Rendered headless with a real graphics device (`-batchmode -quit`, **no**
`-nographics`) through `Assets/Scripts/Editor/BeatTextFxShots.cs`: seven frames at
1536×1024 on the beat's own paper, carrying the judgement sentence in the exact
shape `TurnRunner` composes it. Every frame is filmed with a trace beside it, so
the film is checkable with a number as well as with an eye.

- **Characters land, they do not appear.** At any mid-write frame five characters
  are in the air on a gradient — `ink=255 · 253 · 244 · 218 · 166 · 80 /255` with
  `above = 0.00 · 0.01 · 0.08 · 0.29 · 0.69 · 1.37px`. A settled character reads
  exactly `0.00px`, which is the proof the mesh ends where TMP would have put it.
- **The verdict punches once.** `1.052x` reported at the fire frame; measured off
  the PNG, `It lands beautifully.` is **246px wide punching against 234px settled
  (1.051x)** while the `·` after it sits at the same pixel in both frames.
- **The die is stamped in the sentence**, cropped from the last frame of
  `dice/roll_14.png`, fading and settling with the character it rides on.
- **A click lands everything.** Frontier shoved 16 → 120 the way
  `ReadingBeat.SkipReading` does it: one frame later every character reads
  `ink=255/255 above=0.00px`. Nothing animates in, nothing is left behind.
- **The kill-switch is inert, not hidden.** With `RUNWAY_FX_TEXT=0`: no component
  on the label, `text` byte-identical to what the caller passed, and `Apply`
  returns the caller's own number. `07_killswitch_off.png` carries the switched-on
  and switched-off sentence on one sheet.
- **Cost.** One `TMP_MeshInfo[]` copy per body block (four `Vector3`/`Color32` per
  character, freed with the beat), one 4096×2560 texture already resident from the
  cup, and one `RawImage`. Zero allocation per frame.

## D3-4. The seam — the one-line hookup this lane needs

`ReadingBeat.Reveal`, the line that computes the write-in duration
(`ReadingBeat.cs:243`). Replace:

```csharp
            float secs = Mathf.Clamp(body.Length / 95f, 0.3f, 6.5f);
```

with:

```csharp
            float secs = ReadingBeatText.Apply(b, Mathf.Clamp(body.Length / 95f, 0.3f, 6.5f));
```

The `StartCoroutine(WriteIn(b, secs))` on the next line stays exactly as it is —
it keeps `_draining`, the skip latch and the click semantics (D3-B11). `Apply`
must run BEFORE `WriteIn` starts, which this ordering gives, because it may
lengthen `b.text` by a few spaces and `WriteIn` reads that length once.

No `using` is needed: `ReadingBeatText` is in `Runway.Game`, the same namespace as
`ReadingBeat`. With the switch off, `Apply` returns its second argument unchanged
and the line is what it was.

---

# D5 — THE PARTICLES LANE — `Effects/ParticleInk.cs` + `.Sim` + `.View` + `Motes/Scraps/Embers`

Five new files, no existing file touched. Three effects — dust in the select and
garage bulbs, a burst of paper off the LOCK-IN strike, embers off the title's
burning runway — all built from code, all wearing one 4-cell sprite sheet
rasterised at runtime by the same alpha-max/over compositor `DrawnUI` bakes its
wobbled card edges with.

Kill-switch is the **environment**, not a scripting define: `RUNWAY_FX_PARTICLES`
absent or `1` is on, `0` is off. Off means every entry point returns null having
created **no GameObject, no texture and no pool** — nothing to compile out, and D8
can toggle the lane without a recompile. It is read live on each entry point (a
handful of calls per screen, never per frame), so there is no cached switch for a
harness to reset.

## D5-1. Blocking risks — these stop a compile if they are wrong

| # | API | Where | Why uncertain | Fallback coded |
|---|-----|-------|---------------|----------------|
| D5-A1 | **`UnityEngine.ParticleSystem` DOES NOT EXIST IN THIS PROJECT.** `Packages/manifest.json` is a hand-trimmed list of built-in modules (audio, imageconversion, ui, unitywebrequest ×2) and `com.unity.modules.particlesystem` is not on it. Every mention of the type fails with `CS1069: … forwarded to assembly 'UnityEngine.ParticleSystemModule'`. Measured, not guessed: a first pass written against `ParticleSystem` produced 24 unique errors and nothing else. | would have been every file in the lane | Adding the module is one line in a SHARED file, which is not this lane's to write. | **The simulation is hand-rolled** — `Effects/DrawnParticleSim.cs`, one array of particle structs, emission / lifetime / gravity / Perlin wander / a fade at both ends. It is the whole of what these three effects needed, so the dependency buys nothing; what dropping it buys is no local-vs-world gravity ambiguity, no degrees-vs-radians trap, a `Step(dt)` the editor can drive by hand instead of `Simulate`, and a smaller player. **If the owner would rather have Unity's:** add `"com.unity.modules.particlesystem": "1.0.0"` to the manifest — nothing in this lane needs it. |
| D5-A2 | **A `Graphic` added from code can come up with a NULL `canvasRenderer`, and it fails SILENTLY.** `Graphic` carries `[RequireComponent(typeof(CanvasRenderer))]`, but `AddComponent<T>()` onto a bare code-built GameObject did not always honour it here. `Graphic.Rebuild()` opens with `if (canvasRenderer == null …) return;` — so `OnPopulateMesh` is never called, no error is logged, and the effect simulates perfectly and draws nothing. | `ParticleInk.Mount` | Found by instrumenting, not by reading: `ParticleShots.Probe` reported `live=6 drawn=0 … matCount=-1` (the `-1` being the null renderer) with a plain `Image` beside it rendering fine. | `Mount` adds the `CanvasRenderer` **by hand** before the graphic, and `DrawnParticleView` declares `[RequireComponent(typeof(CanvasRenderer))]` of its own. `DrawnParticleView.PopulateCalls` / `PopulateLastLive` are kept as two static counters (two int writes per rebuild) so the next lane whose graphic goes dark can ask the same question in one line. **Any lane building a Graphic from code should assume this.** |
| D5-A3 | **`Boot`'s canvas is ScreenSpaceOverlay, which paints after every camera.** | the whole lane | Anything drawn by a `Renderer` — including a `ParticleSystemRenderer`, had the module been there — is behind the entire game and invisible. | The particles are **UI geometry**: `DrawnParticleView : MaskableGraphic` builds one quad per particle straight into the canvas mesh. The effect then obeys sibling order, `CanvasGroup` screen fades, masks and the letterboxed stage rect for free, at one draw call. |
| D5-A4 | `VertexHelper.AddVert(Vector3, Color32, Vector2)` + `AddTriangle(int,int,int)` | `OnPopulateMesh` | The three-argument `AddVert` overload and the winding `Image` itself uses (BL, TL, TR, BR with `(0,1,2)`/`(2,3,0)`). | Compiled and photographed. Copied from `Image.GenerateSimpleSprite` rather than invented, so a change in UGUI's winding would change `Image` too. |
| D5-A5 | `public override Texture mainTexture` on a `MaskableGraphic` | `DrawnParticleView` | The property is `virtual` on `Graphic`, and the null case has to answer with something the default UI material can sample. | Returns `Texture2D.whiteTexture` (public, always present) when the sheet is missing, so a failed bake is a screen of white specks, never an exception. `SetMaterialDirty()` in `Bind` is what pushes it to the CanvasRenderer. |

## D5-2. Runtime risks — they compile, but may not do what the look wants

| # | Thing | Where | Risk | Fallback coded |
|---|-------|-------|------|----------------|
| D5-B1 | **Each effect gets its OWN nested `Canvas`.** | `ParticleInk.Mount` | Without it, marking the mote graphic dirty every frame re-batches the whole screen's canvas — a rebuild storm over a screen full of paper, which is exactly what E2 forbids. With it, the per-frame rebuild is 40 quads in their own batch. | Costs one extra draw call per effect. `ParticleInk.NestedCanvas = false` before an entry point puts the effect back inside the host's batch if a mask ever needs it (nested canvases and `RectMask2D` do not always agree). Measured either way in the probe: both render. |
| D5-B2 | `Object.Destroy` is a no-op in edit mode | `Scraps.Fire` | The burst self-destructs 1.4s after the press; in the editor that deferred destroy never happens and Unity logs an error. | Guarded by `Application.isPlaying`. The evidence harness destroys its own root with `DestroyImmediate`. |
| D5-B3 | **The scrap burst allocates on the press.** | `Scraps.Burst` | "Zero allocation after warmup" is a per-FRAME claim; a burst news a GameObject, a Canvas, a CanvasRenderer, a graphic and a 10-particle pool (~1KB) each time. | Deliberate and in the house style — every screen in this game is built from code on every transition, and a lock happens once a week. Between bursts nothing exists, so the steady-state cost is zero rather than a pooled object ticking. The two LOOPING effects (motes, embers) allocate once at screen build and never again: measured **0 B/frame**. |
| D5-B4 | The host rect may not be resolved when `Mount` runs | `ParticleInk.ToLocal` | Beam and band geometry is written in Godot top-left coordinates and converted against `rectTransform.rect`, which is zero until the anchors resolve. | `ToLocal` falls back to `RunwayPaths.StageWidth/Height` when the rect reads under 2px, which is right for every full-stage host in this game. Nothing in this project uses a LayoutGroup, so the rects resolve on assignment. |
| D5-B5 | **The embers are alpha-blended, not additive.** | `Embers` | A real ember adds light. `UI/Default` cannot be made additive (see D6-A1, which owns that problem and ships a shader for it). | Deliberate: the ember cell is a hot core at full alpha with a glow that gives up slowly, and it sits on a near-black painting, where alpha at 0.5–0.9 of coral reads as heat. If the integrator wants them to genuinely add, the one change is `view.material = GlowSprites.AdditiveMaterial()` after `Embers.TitleFire` — the two lanes' materials are compatible because both graphics are plain UI quads. |
| D5-B6 | The garage motes must sit over a COMPOSED painting as well as the drawn room | `Motes.GarageBulb` on `_room` | A painting arrives with its own light baked in. | The motes are the LAST child of `_room` and `AdoptComposed` inserts the painting at sibling index 1, so the dust stays in front of both rooms with no hookup — the same seam D6-B2 relies on. They are INK at 0.09–0.26 alpha rather than cream, because the drawn garage's wall is cream and cream dust on cream is nothing. |
| D5-B7 | `Time.unscaledDeltaTime`, and a hitch | `DrawnParticleView.LateUpdate` | Deliberate (B13): the air must not stop for `timeScale`. But one 400ms hitch with `dt` unclamped teleports every mote across the beam. | `DrawnParticleSim.Step` clamps `dt` to 0.1s. A hitch costs the air a moment, never a jump. |
| D5-B8 | Screen fades must take the air with them | all three | A curtain or a screen `Close()` that left dust hanging would be a pop. | The graphics are ordinary UGUI graphics under the screen's `CanvasGroup`, so `AppScreen.Close`'s fade carries them. Nothing extra is wired. |
| D5-B9 | `Mathf.PerlinNoise` frequency is per PIXEL, not per particle | `DrawnParticleSim.Step` | At 0.12 (a plausible-looking number) a mote crosses a noise cell every 8px and the wander reads as jitter. | Frequency is documented and set in cycles per pixel: 0.005 for motes (a cell about 200px across) and 0.010 for embers. Two `PerlinNoise` calls per particle per frame, 40 particles: inside the measured 0.03ms. |
| D5-B10 | A fixed seed would throw the same burst every week | `Scraps.Build` | The lock is pressed ~50 times a run; six identical scraps on six identical paths is the sort of thing a player notices by week four without knowing why. | A static counter walks the seed forward per press (`17 + _bursts++ * 7919`). Measured over eight locks: **7 8 9 9 9 10 8 7**, every one inside the 6–10 window. Reproducible inside a session, different every week. |

## D5-3. Verified, not guessed

Rendered headless with a real graphics device (`-batchmode -quit`, **no**
`-nographics`) through `Assets/Scripts/Editor/ParticleShots.cs`, four frames at
1536×1024 plus the sprite sheet itself, each effect built against a stand-in of the
surface it will really live on. `Shoot()` fails the run (exit 1) on any budget miss,
so these numbers are assertions, not observations:

- **Counts, inside the checklist's windows.** Motes 32 alive on the select cone and
  28 on the garage beam against a ceiling of 40; embers **min 8, max 12** sampled
  every frame across four seconds; the burst throws 6–10 and is at **exactly 0** by
  1.3s, so the page is clean before the curtain lifts.
- **Frame cost, an order of magnitude under budget.** Motes **0.0295 ms/frame**
  (simulation 0.0044, mesh rebuild 0.0251), embers **0.0147 ms** — against a 0.2ms
  budget and a 1ms-per-lane integration limit.
- **Zero allocation after warmup: 0 B/frame**, both looping systems, measured over
  600 frames of step + `Canvas.ForceUpdateCanvases()` after a forced collection.
  The particle pool is sized once, particles are read through the array slot rather
  than copied out of it, and the mesh is built from structs into the VertexHelper
  the rebuild already owns.
- **The kill-switch is inert, not hidden.** At `RUNWAY_FX_PARTICLES=0` all four
  entry points return null and the host rect's `childCount` is still 0 — nothing was
  built and then disabled. Clearing the variable brings the lane back on in the same
  process.
- **The drawn look is photographed, not asserted.** `00-sheet.png` is the 4-cell
  sheet at 4× over mid-grey: a speck with a firm middle and a halo, a hand-cut cream
  quad whose every side is walked with jitter and which has two lines of writing on
  it, a hot core with a glow, and the out-of-focus smear that one mote in eight and
  one ember in four wear instead.
- **The beam mask works.** `01-motes-draft.png` shows dust filling the trapezoid
  `FounderDraftScreen.Spotlight` draws and nothing outside it, thinning toward the
  edges rather than stopping at them.

## D5-4. The seam — the four one-line hookups this lane needs

```csharp
// 1. DraftSelectPage.Build, between `_page = DrawnUI.FullRect(...)` and the
//    heading — first child of the page, so the dust is over the stage art and
//    under the founder, the sheet and the roster:
Runway.Effects.Motes.DraftSpotlight(_page);

// 2. GarageScreen.OnBuild, immediately after `BuildRoom();` and before
//    `BuildHud();` — last child of _room, so it stays over the drawn room AND
//    over a composed painting:
Runway.Effects.Motes.GarageBulb(_room);

// 3. WeekCommit.StrikeThen, on the line after `if (img != null) img.fillAmount = 1f;`
//    — the paper flies as the strike completes, inside the 0.10s hold before the
//    week turns. Hosted on _lockRow's own parent (the leaning page), so the burst
//    leans with the book:
Runway.Effects.Scraps.Burst(_lockRow);

// 4. TitleScreen.OnBuild, after the film/still block and before the build stamp —
//    on _root, so the embers breathe with the painting they came off:
Runway.Effects.Embers.TitleFire(_root);
```

No `using` is needed at any of the four sites. Every entry point returns null when
the switch is off, tolerates a null host, and can be called again without stacking
(each call builds its own child; call once per screen build). The returned
component is the handle: `Motes.Fade()` / `Embers.Fade()` stop the feed and let what
is in the air live out its life, and disabling the GameObject freezes the effect.

**General forms**, if the integrator wants different geometry:
`Motes.Apply(host, beamRect, topWidth, tint, alphaLow, alphaHigh, seed)`,
`Embers.Apply(host, bandRect, seed)`, `Scraps.Burst(host, atRect)` and
`Scraps.BurstAt(host, x, y)` — every rect and coordinate in the host's own Godot
top-left space, like everything else in this port.

---

# D7c — THE CUE LANE — `Assets/Scripts/Audio/Sfx.cs` + `Audio/SfxHost.cs`

Weight: the fourteen sound effects. `Sfx.cs` is one static door over a six-voice
pool on a DontDestroyOnLoad host; `SfxHost.cs` is the MonoBehaviour that owns the
AudioSources and pumps the one coroutine per cue that reads it off disk. No shared
file is edited, no `partial` is claimed, and `RunwayMix` is reached only through its
public `RegisterSource` / `Unregister`.
`Assets/Scripts/Editor/SfxProbe.cs` is the evidence and runs as a gate
(`-executeMethod Runway.EditorTools.SfxProbe.Run`, exits 1 on any failed check).

**Compiled and run on this machine.** `tools/unity_compile.sh` → **0 unique errors**,
first pass. `SfxProbe.Run` → **137 checks · 0 failed**, all fourteen cues opened
through the shipped loader.

## D7c-1. Blocking risks — these stop a compile if they are wrong

| # | API | Where | Why uncertain | Fallback coded |
|---|-----|-------|---------------|----------------|
| D7c-A1 | `UnityWebRequestMultimedia.GetAudioClip(string, AudioType)` | `Sfx.LoadRoutine`, `Sfx.LoadBlocking` | The two-argument overload, and `UnityWebRequestMultimedia` lives in `com.unity.modules.unitywebrequestaudio` — a module that can be absent from a trimmed `manifest.json`. | `MusicManager` already names the same overload, so this lane cannot break a project the music lane compiles in. If the module ever goes, the long form is `new UnityWebRequest(url, "GET", new DownloadHandlerAudioClip(url, AudioType.WAV), null)`. **Verified live: 14/14 cues came back through this call.** |
| D7c-A2 | `DownloadHandlerAudioClip.GetContent(UnityWebRequest)` | `Sfx.Finish` | The static form (there is also an instance `audioClip` property). | Same module as A1 and the same precedent in `MusicManager`. `((DownloadHandlerAudioClip)req.downloadHandler).audioClip` is the one-line swap. **Verified live.** |
| D7c-A3 | `AudioType.WAV` | both loaders | An enum member, and an absent enum member is a hard error rather than a warning. | The 14 files are RIFF/PCM and nothing else in the build reads a different container. **Verified live.** |
| D7c-A4 | `AudioSource.spatialBlend` | `Sfx.MakeVoice` | Long stable, named only because it is the one property that decides whether a cue is heard at all: at the default `0` a source is 2D and plays at full level with no listener geometry involved; at `1` it would be positioned at the host's origin and could be inaudible. | Written explicitly as `0f` rather than trusted to the default. |
| D7c-A5 | `UnityEngine.Object.DontDestroyOnLoad` from a **static** class | `Sfx.Install` | `Sfx` does not derive from `Object`, so the unqualified call every MonoBehaviour uses does not resolve here. | Fully qualified, and gated on `Application.isPlaying` — it throws in edit mode (the shell's `RunwayMix.Install` pattern, D4-2f). |
| D7c-A6 | `AudioSettings.driverCapabilities` / `AudioSettings.speakerMode` | `SfxProbe` only | Editor/report surface; both have moved between `AudioSpeakerMode` shapes across versions. | Printed, never asserted, and the whole file is under `Assets/Scripts/Editor/` (the `Build.cs` pattern, A5). |

## D7c-2. Runtime risks — they compile, but may not do what the Godot original does

| # | Thing | Where | Risk | Fallback coded |
|---|-------|-------|------|----------------|
| D7c-B1 | **`AudioSource.isPlaying` reads `true` in batch mode with no output device.** | measured, not theorised | The probe's own table shows `isPlaying yes` on all fourteen voices under `-batchmode -nographics` with `graphicsDeviceType Null` and `driverCapabilities Stereo`. That is FMOD's transport state, not sound: nothing was ever presented. Reading it as proof of audio is exactly N11's trap in a different bed. | The probe raises a **BLIND RUN** banner and asserts only what survives — clip data, levels, pitch, pool rotation, mix registration, filter absence, loop counting and allocation. `isPlaying` is a REPORTED column. The ear test belongs to a run with a window and speakers. |
| D7c-B2 | **A cold cue's first hit can be silent.** | `Sfx.Play` | Cues are lazy per cue, so the very first `card_flip` of a run starts the load rather than the sound. It plays on arrival only if the clip lands inside `LateWindow` (0.25s); past that the ask is dropped and only the cache is warmed, because a whoosh answering a click 400ms late reads as a fault. Local WAVs of 3KB–1.2MB land well inside the window, but a cold page cache on a spinning disk might not. | `Sfx.Warm(...)` at a screen's build is the Godot `_ready()` behaviour and removes the risk entirely for that screen; `Sfx.WarmAll()` takes all fourteen. The whole set is **25.7s of audio · 2,074,586 sample-frames**, i.e. 4.2MB at 16-bit and 8.3MB at 32-bit float — under 10MB held for the life of the process either way. |
| D7c-B3 | **Six voices, and the seventh cue steals.** | `Sfx.Take` | The ring prefers a free voice and steals the one at the head when all six are sounding. `death` (6.5s), `win` (6.0s), `pivot` (4.5s) and `lock_week` (3.2s) are long enough to still be alive when the next four cues fire, so a stacked ending CAN cut a tail short. | Six is already 1.5× the busiest instant `main.gd` ever asks for (a week turning: `lock_week` + `cash` + `win` + `tick`). Raising `Sfx.Voices` is one constant; nothing else changes. The probe proves the ring visits all six. |
| D7c-B4 | **`pivot.wav` is never played by the Godot build.** | the whole game | 864KB on disk in both projects, and `grep -rn "pivot" game/src --include='*.gd'` returns 107 hits of which **not one** is an audio play — `_open_pivot`'s sheets play `cash` on each choice and `deposit` on the commit (`garage_view_screen.gd:2508/2518/2530/2883`). The cue is orphan art. | Shipped as a cue with a name and a level so a hookup is one word if the pivot sheet is ever given its sound, but it appears in the hookup table with **no site**. It is not a port gap; it is a gap in the original. |
| D7c-B5 | **`step` and `pickup` have no Unity home.** | — | Both belong to `scramble3d_screen.gd` (footfalls while running, grabbing an item), and the 3D scramble minigame is not in this port at all. | Both ship as cues with their Godot pitch jitter documented, and both are marked "no Unity site" in the hookup table. If the scramble is ever ported the two lines are `Sfx.Step(0f, 0.9f + Random.value * 0.25f)` and `Sfx.Pickup(0f, 0.95f + 0.1f * Random.value)`. |
| D7c-B6 | **The mix re-reads a voice's base level after every shot.** | `RunwayMix.ApplyVoice` ← `Sfx.Play` | Writing `src.volume` per shot is precisely the "somebody else moved it" case the mix handles: on its next tick it recomputes `Base = volume / gain`. That is harmless ONLY because the sfx bed's gain is 0dB in all four states, so `gain == 1` and `Base == volume` exactly. | If the mix table ever gives sfx a non-zero gain, the per-shot level and the bed will still resolve correctly (the mix multiplies), but the recorded `Base` would drift by one tick's worth of rounding per shot. Registering a dedicated child source per shot is the fix if that day comes. The probe asserts the bed is 0dB and un-lidded under `curtained`, under `binder`, and under `binder+red`. |
| D7c-B7 | **`MusicManager` builds its URL as `"file://" + path`.** | `Audio/MusicManager.cs:86` — NOT this lane's file | This project's own path contains a space (`.../Claude Code/runway/...`), which is exactly what B6 says must go through `new Uri(path).AbsoluteUri`. A raw-concatenated `file://` URL is not a valid URI and the music may simply never load on this machine. | This lane resolves every cue through `RunwayPaths.ArtUrl("sfx/<name>.wav")` instead, and the probe's `routes: 14 loaded through UnityWebRequestMultimedia` is the proof that route works with the space in it. **Flagged, not fixed — `MusicManager` is a shared file.** One line: `RunwayPaths.FileUrl(path)`. |
| D7c-B8 | **`LoadBlocking` spins the main thread.** | `Sfx.LoadBlocking` | Nothing pumps a coroutine outside play mode, so a harness's ask and any pre-play-mode call resolve inline with a `Thread.Sleep(1)` spin on `isDone`. A remote URL here would hang the editor for `LoadTimeout`. | The URL is always a local `file://` built by `RunwayPaths`, the spin carries a 10s timeout, and an abort waits 16ms before `Dispose` (the shell's B8). In play mode this branch is unreachable: `Play`/`LoopOn` take it only when `!Application.isPlaying`. |
| D7c-B9 | **The harness silence list is read from the process environment only.** | `Sfx.ReadSwitch` | `RUNWAY_USHOTS` / `RUNWAY_UPERF` / `RUNWAY_LANEWIRE` / `RUNWAY_SHOT` are read with `Environment.GetEnvironmentVariable`, not through `Env.Get`, so a harness switch set in `.env` would not silence the cues. | Deliberate, and identical to `MusicManager.Install`: a harness variable is set BY the harness, in the process. The kill-switch proper (`RUNWAY_FX_SFX`) does go through `Env.Get`, so it layers over `.env` and `keys.env` like every other switch (D4-2g). |
| D7c-B10 | **`pitch` changes duration, as it does in Godot.** | `Sfx.Play` | `AudioSource.pitch` is a rate multiplier, exactly like `AudioStreamPlayer.pitch_scale`, so the archetype chip at 1.14 is also 12% shorter. | Intended: it is the original's own behaviour, and the three sites that use it (`founder_draft_screen.gd:774/847/875`, `dice_roll.gd:43`) all use it on cues of 50ms–550ms where the shortening is the point. |

## D7c-3. Verified, not guessed

Measured by running the shipped `Sfx.cs` under `-batchmode -nographics`.
`SfxProbe.Run` passes **137/137**. Full log:
`scratchpad/sfx/cues.txt`.

- **All fourteen cues opened through the SHIPPED loader.** `routes: 14 loaded
  through UnityWebRequestMultimedia, 0 through AssetDatabase, 0 missing` — the
  `AssetDatabase` fallback the probe carries was never needed, so the evidence is
  about the code that ships and not about the editor's importer.
- **Every clip carries real audio**: length > 0, samples > 0, a real sample rate and
  1–2 channels, all fourteen. The table, as measured:
  `card_flip 0.120s`, `cash 0.050s`, `curtain 0.549s`, `death 6.500s`,
  `deposit 0.300s`, `dice_rattle 1.600s`, `lock_week 3.200s`, `pen_scratch 2.400s`,
  `pen_scribble 0.300s`, `pickup 0.090s`, `pivot 4.500s`, `step 0.050s`,
  `tick 0.034s`, `win 6.000s` — **25.7s in total**, three of them stereo at 48kHz
  and the rest mono at 22.05/44.1kHz.
- **Every level is the Godot number.** A bare call reproduces the original's own
  `volume_db` with no arithmetic at the call site: `curtain` 0.3981 (-8dB,
  `curtain.gd:81`), `dice_rattle` 0.5012 (-6dB, `dice_roll.gd:49`), `pen_scratch`
  0.1995 (-14dB, `journal_page.gd:625`), `pen_scribble` 0.3162 (-10dB,
  `journal_page.gd:626`), and 1.0000 for the other ten, which is Godot's own
  default. The two sites that disagree with the table are reproduced as trims:
  the dice cup's thin whoosh at **-14dB / pitch 1.25** and the 4th archetype chip
  at **pitch 1.14**.
- **The pool is a ring and it visits all six voices**: fourteen cues landed on
  voices `0 1 2 3 4 5 0 1 2 3 4 5 0 1`, each carrying its own clip and its own level.
- **Seven voices in the mix, no filter on any of them.** `RunwayMix.Count(Sfx) == 7`
  (six one-shot + one loop), and the sfx bed measures **0.0dB / 22000Hz** under
  `curtained`, under `binder` and under `binder+red` — the bed that is never ducked
  and never filtered, asserted rather than assumed.
- **The loop is held by a COUNT**, which is `loading_screen.gd`'s `_writing` counter:
  a second writer starting takes a second hold, one lifting does not stop the paper,
  the last one does. The reading beat's quieter hand measures -16dB against the
  journal's -14dB, from the same cue.
- **Zero per-play allocation**: **20,000 `Play()` calls moved the managed heap by
  0 bytes with 0 gen-0 collections.**
- **Kill-switch, both halves.** The ENVIRONMENT half: a second run with
  `RUNWAY_FX_SFX=0` set in the process reports `switch RUNWAY_FX_SFX=0 → sfx OFF`,
  so `Env.Get` reaches the switch (`scratchpad/sfx/cues_switch_off.txt`). The
  BEHAVIOUR half: `Sfx.SetEnabled(false)` tears the host down, `Install()` raises
  nothing, `Play` / `LoopOn` / `WarmAll` are no-ops, the sfx bed empties to 0 and no
  `runway_sfx` GameObject survives. One call brings the whole lane back and
  re-registers its voices. Both runs are 137/137.

## D7c-4. The seam

`Sfx` reaches into exactly one thing it does not own — `RunwayMix.RegisterSource` /
`Unregister`, through the public API, once per voice. `Boot` is not touched and does
not know the cue player exists. `RunwayPaths.ArtUrl` and `Env.Get` are read-only.

`Sfx.Install()` is the optional one-line entry point; without it the lane installs
itself on the first `Play`, `LoopOn` or `Warm`.

**The restraint law lives in this file, not at the call sites.** A cue that is not in
the table plays nothing and says so once; a cue whose file is missing marks itself
absent and never re-opens the file; the switch is checked before anything at all is
built, so a hookup cannot get it wrong.

**The fourteen doors**, so a hookup is a word rather than a string literal and a typo
is a compile error rather than a silent screen:

```csharp
Sfx.CardFlip();  Sfx.Cash();      Sfx.Curtain();   Sfx.Death();
Sfx.Deposit();   Sfx.DiceRattle(); Sfx.LockWeek(); Sfx.PenScribble();
Sfx.Pickup();    Sfx.Pivot();     Sfx.Step();      Sfx.Tick();     Sfx.Win();
Sfx.PenScratch(true);   // the one LOOPING cue: down…
Sfx.PenScratch(false);  // …and up. Held by a count, not a flag.
```

Every one takes `(float volumeDb = 0f, float pitch = 1f)`, where `volumeDb` is a
**trim on top of the cue's own Godot level** — so a bare call is the original's
loudness and the two sites that differ are expressed as the trim they are:

```csharp
Sfx.Curtain(-6f, 1.25f);          // dice_roll.gd:42-43 → -14dB, pitch 1.25
Sfx.PenScratch(true, -2f);        // loading_screen.gd:313 → -16dB
Sfx.Cash(0f, 0.9f + 0.08f * i);   // founder_draft_screen.gd:774, per chip
```

`using Runway.Audio;` is needed at each site, or write `Runway.Audio.Sfx.CardFlip()`.

---

# A-TAIL — THE SAVE / SLOT / KEY LANE — `Runway.ATail.Tests/` + `Assets/Scripts/Editor/ATailProbe.cs`

Checklist A15 (save round-trip), A16 (slots), A17 (key desk), A18 (authored error
states). New files only; nothing shipped was edited.

## A-TAIL-1. Blocking risks — these stop a compile if they are wrong

- **`SaveSlots`, `Env`, `RunwayPaths`, `RunSave`, `RunRecord` and `ContentDb` are NOT
  Unity-free.** They compile against exactly four UnityEngine symbols and no more:
  `Debug` (Log/LogWarning/LogError), `Mathf.Clamp(int,int,int)`, `Application`
  (`platform`/`dataPath`/`streamingAssetsPath`/`persistentDataPath`) and
  `UnityEngine.Random.value` (ContentDb's weighted draw only). `Runway.ATail.Tests/
  UnityShim.cs` supplies those four, so the SHIPPED sources — not copies — run under
  `dotnet run`. Anything else pulled into that csproj (a screen, `Boot`, `LlmClient`)
  drags in MonoBehaviour and will not compile there.
- **`RunRecord.LogEvent` needs `ContentDb`**, which needs `RunwayPaths` + `Directory`.
  `Game/ContentDb.cs` is therefore in the test csproj even though only its three
  static JSON readers are used.
- **`SaveSlotInfo` lives in `App/IRunDriver.cs`**, not in `SaveSlots.cs`. A suite that
  reads a slot table has to compile IRunDriver.cs too (it brings `NullRunDriver`,
  which needs nothing else).
- **Unity never compiles anything under `unity/Runway.ATail.Tests/`** — it is outside
  `Assets/`, exactly like `Runway.Core.Tests`. The `namespace UnityEngine` shim in
  there can therefore never collide with the real one.

## A-TAIL-2. Runtime risks — they compile, but will write into the player's folder

- **`RunwayPaths.UserDir` has no injection seam.** It reads `$HOME` and memoises into
  a private static `_userDir`. Two ways to redirect it, both verified:
  - **console host**: `Environment.SetEnvironmentVariable("HOME", tempDir)` BEFORE the
    first `UserDir` read, with `Application.platform = RuntimePlatform.OSXEditor`. The
    real macOS branch then resolves to `<temp>/Library/Application Support/Runway`.
  - **editor probe**: reflection on the private static —
    `typeof(RunwayPaths).GetField("_userDir", BindingFlags.NonPublic | BindingFlags.Static)`.
  Either way the redirect MUST be asserted before a single write: `ATailProbe.Redirect()`
  returns false and the probe aborts if `UserDir`, `Env.KeysPath` and `SaveSlots.Path(1)`
  are not all inside the temp tree. A renamed field must abort the run, never fall back
  to `~/Library/Application Support/Runway`.
- **`Env.Load()` is cached in a private static.** After any file write the value is
  stale until `Env.Reload()`. `SaveOpenAiKey`/`SaveKeyless` call `Reload` themselves;
  a raw `File.WriteAllText` to `Env.KeysPath` does not.
- **The editor probe must not write inside `Assets/`.** Anything dropped there is
  imported and can be committed by accident, so the probe tests the USER layer and the
  PROCESS layer only; the project `.env` layer is covered by the console suite, whose
  `Application.dataPath` points at a sandbox.
- **`-executeMethod` needs the same mutex as `tools/unity_compile.sh`.** Two editors on
  one Library corrupt or false-clean it. The probe runner takes
  `${TMPDIR}/runway_unity_compile.lock` with the identical mkdir/steal-a-dead-holder
  dance before launching Unity.
- **`EditorApplication.Exit(code)` is what makes the probe's result readable** — with
  `-quit` alone the editor exits 0 whatever the assertions did.

## A-TAIL-3. Verified, not guessed

Two runs, same assertions, two hosts.

- **`dotnet run --project unity/Runway.ATail.Tests`** — 104 checks, 95 held, 0 harness
  failures, 9 shipped-code defects (two root causes).
- **Unity 6000.0.82f1 `-batchmode -nographics -executeMethod
  Runway.EditorTests.ATailProbe.Run`** — 43 checks, 3 failed, the same root cause. The
  editor reproduces the console suite's A15 defect line for line, which is what proves
  the shim is not the reason anything passed.
- `bash tools/unity_compile.sh` → **0 errors** with `ATailProbe.cs` in the tree.

**A15 — Newtonsoft appends into pre-populated collections.** `ObjectCreationHandling`
defaults to `Auto`: for a field that is already a non-empty collection, the
deserializer reuses the instance and ADDS to it. `Investor.Coords`
(`GameState.cs:92`) is initialised `{ 0.0, 0.0 }`, so every load produces
`[0,0, 0.9,0.4]`, then `[0,0, 0,0, 0.9,0.4]` — measured 2 → 4 → 6 over three passes.
`WorldGen.InvestorDcMod` reads `Coords[0]`/`Coords[1]`, which are `0,0` after one
CONTINUE. The same rule resurrects the five default `Competences` and six default
`Traits` keys into any state that carries fewer. **Control run in the suite**: the
same shipped `GameState` round-tripped through
`JsonSerializer.Create(new JsonSerializerSettings { ObjectCreationHandling =
ObjectCreationHandling.Replace })` is byte-identical across three passes and all 67
public fields survive — the one-line fix is proven, not assumed.

**A15 — everything else round-trips.** 67 public `GameState` fields swept by
reflection, all 67 carrying a non-default value (a default proves nothing), across a
live 3-week fixture and a dead-run fixture. Only `Investors` moves.
`meta.ts` is re-stamped on every `Save`, so the whole FILE is never byte-identical
across two writes; the `state` and `record` blocks are.

**A15 — `GameState.GetMetaF` tests `v is double`.** Every `int` written through
`SetMeta` therefore reads back as the CALLER'S FALLBACK, before and after a save:
`RunDriver.Loyalty` (`cf_loyalty_N`) is permanently 70 and `JournalSpreads:383`
(`fundraising_week`) is permanently this week. `prev_revenue` and `unit_econ` are
doubles and survive; `unit_econ` returns as a `JObject` after a load, which
`BinderScreen.UnitEcon` already reads both ways.

**A16 — the slot layer holds.** 3 writes, a 3-row table with the exact
`{company} / {founder} · week {n} · last played {ago}` the title draws, the `Ago`
ladder word for word from `title_screen.gd`, an overwrite that truncates (no tail of
the old run survives), a delete that leaves the neighbours alone, and a CONTINUE whose
restored state matches field for field apart from the A15 defect. Unparseable, empty
and record-less saves all behave. **The one disagreement**: a `state`-less file parses,
so `SaveSlots.Read` marks it `Exists` and the title draws a dossier that `RunSave.Load`
then refuses — `Boot.StartRun` falls through to `BeginFreshRun`, so the click starts a
new run in that slot with nothing on screen saying the old company is gone.

**A17 — the layering is project `.env` → user `keys.env` → live process variable**, in
that order, and `Env.Load()` reports the FILE layering underneath the process one.
Quotes stripped, comments/blank/keyless/valueless lines skipped, pasted key trimmed,
`Reload()` required after a raw file edit. Keyless writes a marker with no key in it
and still inherits a dev `.env` key when one exists — by design (`KeysScreen.cs:93`).
The editor probe runs the MonoBehaviour half the console host cannot: `keys.env` →
`Env.Reload()` → `LlmClient.Setup` brings the client UP with `openai` /
`gpt-5.6-terra` assess / `gpt-5.6-luna` clarify, and "play without" brings it back
down to authored-only — that is `Boot.NotifyKeysChanged` verbatim.

**A17 — the key never reaches a log line.** Zero canaries across the console suite's
lines and the editor probe's 53 console lines, with the failure paths driven on
purpose (unwritable keys path, unparseable slot whose CONTENTS contain the canary).
Static half: no `Debug.Log*` argument in any of the 98 shipped files names `ApiKey`,
`OpenAiKey`, `okey`, `OPENAI_API_KEY` or `ANTHROPIC_API_KEY`. The key rides request
HEADERS only.

## A-TAIL-4. The seam

Nothing is hooked up. Both artefacts are run-on-demand:

```bash
$HOME/.dotnet/dotnet run --project unity/Runway.ATail.Tests     # 0 = clean
Unity -batchmode -quit -nographics -projectPath unity \
      -executeMethod Runway.EditorTests.ATailProbe.Run -logFile - # 0 = clean
```

`ATailProbe` is under `Assets/Scripts/Editor/`, so it is editor-only by folder and
never ships in a player build. It has no `Apply()` entry point and no kill-switch
define because it installs nothing: it only runs when `-executeMethod` names it.

---

# C-DRIVER — THE BEHAVIOUR HARNESS — `App/UnityFlow.cs` + `App/UnityFlow.Probe.cs`

Checklist section C: the live first-flow probe (C1), the week loop (C2), the clarify
ask-paths (C3), the money law (C4), the die at the table (C6) and no-repeat (C7).
New files only; nothing shipped was edited. Armed by `RUNWAY_UFLOW=<dir>`, inert
without it. Kill-switch define: `RUNWAY_FX_UFLOW_OFF`.

## C-DRIVER-1. Blocking risks — these stop a compile if they are wrong

| # | Thing | Why it matters | What was done |
|---|-------|----------------|---------------|
| CD-A1 | **The file is guarded by TWO defines**: `#if !RUNWAY_FX_UFLOW_OFF && !RUNWAY_FX_USHOTS_OFF`. | It reuses `UnityShotsCamera` (the shutter + the flat-frame check) and `UnityShotsPoke` (the one-shot reflection), both of which live inside `#if !RUNWAY_FX_USHOTS_OFF`. Guarding on only its own define would break the compile in the USHOTS-off variant of the kill-switch matrix. | Both defines guard both files. With USHOTS off, this lane simply does not exist — which is correct: without a shutter there is no evidence to take. |
| CD-A2 | `WeekCommit`, `JournalSpreads`, `JournalPage`, `ReadingBeat`, `DiceRoll`, `GarageScreen`, `BinderScreen`, `BookIntroScreen`, `BirthScreen`, `FounderDraftScreen`, `DraftBagPage`, `DraftCofounder` are all **public types**. | Every reach-in is ONE reflected field followed by ordinary typed calls. If any of these were made `internal`, the casts break at compile time rather than silently at runtime. | Deliberate: only the *members* are reflected, never the types. `Runway.Screens.TitleScreen` and `HowToScreen` are never named at all — they are poked through `AppScreen`, so a namespace move cannot break this file. |
| CD-A3 | `SaveSlots.SlotCount` is a `const int`. | `UnityFlowGuard` sizes its backup array from it at field-initialiser time. | If it ever becomes a property, the array initialiser must move into `BackUp`. |

## C-DRIVER-2. Runtime risks — they compile, but the walk can still be wrong

| # | Thing | Risk | What was done |
|---|-------|------|---------------|
| CD-B1 | **`RUNWAY_UFLOW` is deliberately NOT in `Boot.HarnessVars`.** | With `Boot.Harness` true the run skips the studio card, answers the title with the any-key contract (so NEW GAME never appears), jumps `AfterDraftRoutine` past BIRTH and BOOK straight into the garage, and turns art off — i.e. it skips four of the six stages C1 exists to prove. | No process variable is set at all. The consequence is that the run behaves like a player's in one more way: `SaveIfWeekTurned()` writes, and NEW GAME calls `ClearRun()`. See CD-B2. **If a future integrator adds `RUNWAY_UFLOW` to `HarnessVars`, this harness stops testing C1.** |
| CD-B2 | **The walk writes saves.** `Driver.ClearRun()` on the NEW GAME card, `RunSave.Save` once a week. | BUG-15 in Unity: a test run eating the owner's company. | `UnityFlowGuard` copies ALL THREE slots plus `SaveSlots.ActiveSlot` and the `seen_howto_v2.unity` mark before the first frame (BeforeSceneLoad, ahead of `Boot.Awake`), parks a copy of every occupied slot in the shot directory as `_slotN.backup.json`, and puts everything back at the end AND from `OnApplicationQuit`. A slot that was empty is DELETED on restore, never left holding the harness's company. Restore is idempotent. |
| CD-B3 | **A frame-polled reflection miss would be a 7,000-line failure storm.** `WeekCommit._pendingDice` is read every frame while the die is cast; the cup's sheet every frame while it loads; `GarageScreen._paintRibbon` 4×/s for up to 4 minutes; `ReadingBeat._proceed` 2×/s for up to 4 minutes. | `UnityShotsPoke` records a miss per call, and this lane counts every miss as a failure. One renamed field would bury the log and blow the exit code. | The polled members go through `UnityFlowReach` (same file): the `FieldInfo`/`MethodInfo` is resolved once per type+member, the miss is shouted once, and `Report` counts it once. Everything one-shot still goes through `UnityShotsPoke`, whose miss list the report also carries. |
| CD-B4 | **The dice sheets are not baked.** `Resources/Sheets/` holds birth/curtain/howto only, so `DiceRoll` streams `dice/roll_NN.png` through `UnityWebRequestTexture` and the resulting `Texture2D.name` is empty. | The obvious "which number did the cup show" reading returns nothing, and C6 would have no independent shown-number source. | `CupNumber()` reads `SheetLoop._inflight` FIRST — it holds `"dice/roll_07.png"` for the whole load — and falls back to `_sheet.name` for the baked path. It is frame-polled from the moment `_pendingDice` appears, which is the same frame `ShowDie` starts the load. If both come back empty, the beat's own judgement sentence ("The die came up 14.") carries C6, and if that is absent too the check FAILS rather than passing quietly. |
| CD-B5 | **The beat's judgement sentence is revealed on a reading clock.** It is the fourth queued paragraph and lands 10–25s in. | Reading `_bodies` at +2.2s would find nothing and C6 would report the shown number as unreadable every week. | The beat is photographed first, then `SkipReading()` catches the page up, and only then is `_bodies` scanned. |
| CD-B6 | **`Boot.OnTitleChoice` → `ColdOpen` drops the curtain and `RaiseCurtain` can no-op.** `DropCurtain` starts `Curtain.Close()` as a coroutine, so `IsShut` is still false when the book-read turn calls `RaiseCurtain()` on the same frame; the curtain then shuts and its 40s failsafe is what re-opens it. | The `c1_*_garage_settled` shot can be a photograph of a shut curtain. | Left as it is and photographed honestly — it is a shipped-behaviour finding, not a harness artefact. The room assert waits it out (`PaintCap` 260s), so it does not become a false C1 failure. |
| CD-B7 | **`HowToScreen` is 3 pages when the loops ship and 1 when they do not.** | Poking `Advance` a fixed 3 times would land on the DRAFT screen and log two misses, which this lane counts as failures. | The paging loop is bounded by `app.State == AppState.HowTo` and re-checks between every press. |
| CD-B8 | `Dictionary<K,V>` reached through `IDictionary` yields **boxed `KeyValuePair`**, not `DictionaryEntry`, from `IEnumerable.GetEnumerator()`. | `foreach (DictionaryEntry e in map)` over `JournalPage._rowIds` throws `InvalidCastException` at runtime — a bug that compiles. | `map.Keys` is walked and `map[key]` indexed instead. |
| CD-B9 | **`RUNWAY_NO_ART` makes C1's room claim unprovable.** With art off nothing is composed and no ribbon is raised, by design. | The room assert would burn 260s and then fail for the wrong reason. | The room check is skipped with a loud `UFLOW skip c1_room` line when `Boot.ArtEnabled` is false. Everything else still runs, so a no-art rehearsal is the cheap way to check the walk before paying for it. |
| CD-B10 | **`TMP_InputField.text` fires `onValueChanged`.** | The whole written-move path depends on it: `JournalPage.SetWritten` → `RaiseWritten` → `JournalSpreads` → `WeekCommit.Written` + `RefreshLock()`. | Belt and braces: `commit.Written` is also assigned directly, and `WeekCommit.CommitFromText` reads `_jp.WrittenText()` first regardless. |

## C-DRIVER-3. What this harness CANNOT see (stated, not hidden)

- **C4 exactness is measured on the receipt, not on `State.Cash` alone.** Cash moves
  three times inside one lock — `SimEngine.WeeklyTick` (rent, payroll, revenue), the
  DM's own ops, and the next week's `StartWeek` burn — and only the middle one is
  what C4 is about. So the exactness claim is asserted against the engine's own
  receipt line (`"spent $1500 on …"` / `"the bank stopped it at $X (wanted $Y)"`),
  the era clamp is asserted against `SimEngine.EraSpendCap`, and `State.Cash`
  before/after is asserted only to have MOVED. Every number (written, DM ask, other
  cash ops, receipt, era cap, cash in/out) plus the full tick log and decision log is
  printed, so the arithmetic can be settled by hand from the run's own output.
- **C5 (pricing economics) is not attempted.** It needs several weeks of tick logs at
  two prices; this lane proves only that a price ANSWER reaches `GameState.Offers`.
- **C8 (paint resilience) is not attempted** — it needs a black-hole render URL.
- **The clarify branches depend on a live model.** `EventGenerator.Clarify` only runs
  when a key is live, and whether it asks is the model's call. A silent pre-pass on a
  priceless sell move with unpriced offers is reported as a C3 FAILURE, because that
  is exactly the finding C3 exists to catch — not as a harness limitation.
- **`GarageScreen` is the only room.** An era transition that swapped the screen would
  be picked up by `RoomAlive()`, but the Unity port handles era moves inside
  `StartWeek`, so no era-transition screen is driven or photographed here.

## C-DRIVER-4. The seam

Nothing is hooked up and nothing needs to be. Both files install themselves from
`[RuntimeInitializeOnLoadMethod]` and are inert unless `RUNWAY_UFLOW` is set:

```bash
RUNWAY_UFLOW=/path/to/shots RUNWAY_UFLOW_WEEKS=3 \
  "build/mac/RUNWAY!.app/Contents/MacOS/RUNWAY!" \
  -screen-width 1536 -screen-height 1024 -screen-fullscreen 0
echo $?          # 0 = every check passed; N = N failed checks
```

`RUNWAY_UFLOW_WEEKS` (default 0) is the number of weeks played AFTER the first
flow's own week 1. `RUNWAY_UFLOW_SLOT` (default 3) is the slot the run is played in;
all three are backed up and restored regardless. `RUNWAY_NO_ART=1` still wins and
turns the run into a text-only rehearsal (see CD-B9). Windowed only — `-batchmode`
is detected and refused with exit 4, because `WaitForEndOfFrame` never returns
without a drawn frame.

Shots land in the directory as `c1_NN_<stage>.png` (the first flow, including its
week 1) and `c2_NN_wWW_<stage>.png` (the extra weeks). The last two lines of the log
are the verdict:

```
UFLOW failed checks: c3_price_ask · c6_die_cup
UFLOW DONE pass=41 fail=2
```

## D-SELECT-1. Blocking risks — these stop a compile if they are wrong

| # | API | Where | Why uncertain | Fallback coded |
|---|-----|-------|---------------|----------------|
| DS-A1 | `LinkedList<KeyValuePair<string,string>>` with `AddFirst` / `RemoveFirst` / node `Remove` | `ArtCache._fetchQueue` | The queue used to be a `Queue<T>`, which cannot push to the front — and an urgent lane is the whole point. `LinkedList` node surgery (`Remove(node)` then `AddFirst(node)`) is the one form that reuses the node rather than allocating. | `System.Collections.Generic`, already imported. If node reuse ever bites, `AddFirst(n.Value)` after `Remove(n)` is the same behaviour with one allocation. **Verified: 0 compile errors, and the probe asserts two asks for one path stay one queued job.** |
| DS-A2 | `Texture2D.LoadImage(byte[])` | `ArtCache.ReadFromDisk` | An extension method on `UnityEngine.ImageConversion`, in `com.unity.modules.imageconversion` — a module a trimmed `manifest.json` can drop. | `InkRevealFilm.cs:251` already calls it in this project, and `Texture2D.EncodeToPNG` (same module) is what every shot probe writes with. **Verified live: 3/3 pictures decoded through it.** |
| DS-A3 | `AsyncOperation` recognised inside a hand-driven `IEnumerator` | `ArtCache.Step` | `UnityWebRequestAsyncOperation` reaches the driver as `object`; the cast has to hit the base type, not the derived one. | Cast to the base `AsyncOperation`, which every yieldable operation derives from; a `yield return null` (a frame wait there is no frame for) falls through the same `if` untouched. |
| DS-A4 | `ShownArt : MonoBehaviour` beside a **static** class in one file | `GameUi.cs` | Two top-level types in a file whose name matches the static one — legal C#, but Unity refuses to serialise a `MonoBehaviour` whose class name does not match its file when it is added from the INSPECTOR. | Nothing ever adds it in the inspector: it is created by `AddComponent<ShownArt>()` at runtime only, which has no filename rule. `GameUi.cs` is not a component file and never appears in a scene. |
| DS-A5 | `EditorApplication.Exit(code)` | `DraftSelectProbe.Run` | Editor-only surface. | The whole file is under `Assets/Scripts/Editor/`, the A-TAIL-2 pattern. With `-quit` alone the editor exits 0 whatever the assertions did, so this is what makes the probe a gate. |

## D-SELECT-2. Runtime risks — they compile, but may not do what the stage needs

| # | Thing | Where | Risk | Fallback coded |
|---|-------|-------|------|----------------|
| DS-B1 | **The decode pacing is what made the founder late in the first place.** | `ArtCache.Pump` | One decode per frame is deliberate (an 889ms frame on the draft, D-perf) and it STAYS. Its cost is that a first-frame callback can arrive many frames after the ask — long enough for a re-select to have rebuilt the loop that asked. | Not removed, routed round: `urgent` puts the face at the FRONT of the same one-per-frame queue and promotes an ask that was already waiting. The pacing is untouched; only the order is. |
| DS-B2 | **A callback that outlives its loop must not take the picture with it.** | `DraftLoop.Reask` | `DRAFTLOOP cb dead target` in the live log was a dropped texture AND a wasted decode. | The picture is in `ArtCache` before any callback runs (`Deliver` fills `_tex` first), so the loop that replaced the dead one takes it off the cache synchronously, and `OnEnable` re-asks for any loop holding an id with no frames. The dead-target log line now prints whether the cache holds it. **Probe phase 3 proves it: the replacement loop is standing.** |
| DS-B3 | **A callback that outlives its ARCHETYPE used to be applied anyway.** | `DraftLoop.Reask` | The old callback checked only that the target was alive. Selecting B while A was in flight put A's frame into B's `_frames` and showed it — a founder wearing another founder's first frame, then breathing in two people. Found reading the path, not from the log. | The wanted id is captured at ask time and a stale answer is dropped with a `DRAFTLOOP cb stale` line. |
| DS-B4 | **Two asks for the same first frame answer on the same frame.** | `DraftLoop` | `Play` then `OnEnable`'s re-ask both land in one `Deliver`, and two hydrators would append the same 36 frames twice into one loop. | The second answer adds no frame (`_frames.Count == 0` guard) and starts no hydrator (`_hydrate == null` guard). |
| DS-B5 | **A founder who is up but not breathing.** | `DraftLoop.OnEnable` | If the first frame lands while the page is off screen, `StartCoroutine` is a silent no-show (P0-F4) and the loop is a still for the rest of the run. | Coming back on screen restarts the hydrator when there is exactly one frame and no hydrator. A loop that genuinely ships one frame pays one `File.Exists` per page entry and stops. |
| DS-B6 | **Poisoning a path is permanent, so only a missing FILE may do it.** | `ArtCache.Load` / `ArtCache.Deliver` | The old code cached null for `boot == null || url.Length == 0` and for every failed fetch. One early race — a screen built before Boot, a request that errored — blanked that drawing for the whole session, and `Known` then answered "absent" forever. | `Load` poisons only when `ArtUrl` comes back empty, which is `File.Exists` saying no. `Deliver` poisons only when `RunwayPaths.ArtExists` agrees the file is not there, and shouts a warning otherwise. With no runner the ask WAITS in the queue instead of being answered absent; the next `Load` that finds a runner drains it. **Probe phases 4 and 5 assert both halves.** |
| DS-B7 | **`Rebind` blanked the image before its load and never put it back.** | `GameUi.Rebind` | `img.enabled = false` up front plus an early `return` on a miss is a permanent hole in the page — the shape of the empty cone, and this flow can never fall back to a blank screen. | Nothing is switched off until the replacement is in hand. The one thing still taken down is a drawing of something ELSE that cannot be replaced (the bag detail panel swaps between items), because a wrong picture under a right name is worse than an honest gap — which is why the image carries a `ShownArt` mark naming what it is showing. |
| DS-B8 | **A pump whose runner dies stays "pumping" forever.** | `ArtCache._pumping` | If Boot is destroyed mid-drain, `_pumping` stays true, `Start` never restarts and every later ask waits in a queue nobody empties. Pre-existing; the `Runner` seam does not change it. | **Not fixed, stated.** Boot is a process-lifetime singleton, so the only way in is a scene teardown that also takes the screens. A repair, if it is ever needed, is to remember the runner the pump was started on and clear `_pumping` when it goes. |
| DS-B9 | **The trait pips cost 30 more baked sprites per re-select.** | `GameUi.TraitPips` | Every pip now carries its own `AddInkEdge` (a 21x21 bake plus a boil), on top of `StatPips`'s 25 — 55 bakes per arrow key. | Each trait bake is 441px against the stat pips' 1,344, so the row is roughly a third of what the sheet already pays on every pick. `RedrawPips` was already rebuilding the whole ledger per selection. |
| DS-B10 | **The `%+d` bag delta has 26px of gutter to live in.** | `GameUi.TraitPips` | The original draws it at x+216 at size 20 inside a 235-wide column whose pips end at 209 — i.e. 1px INTO the next column's word. It never shows there because `founder_draft_screen.gd:806` passes no deltas. | Ported at size 17, right-aligned to the column edge, so it clears the next word by 2px instead of colliding with it. The draft passes no deltas either, exactly as the original does not; `DraftSelectProbe`'s sheet shot is the only place it is drawn, so the drawing is proven rather than assumed. |
| DS-B11 | **Both ledgers are lettered in the wrong hand.** | `GameUi.StatPips`, `GameUi.TraitPips` — pre-existing | Godot draws the stat labels and the trait names with `_font_d` (Baloo2-Bold); the port draws them with `DrawnUI.HandLabel` (PatrickHand). The `%+d` delta is the one string the original sets in the writing hand, and it is the one this port now matches. | **Flagged, not fixed.** It is the same choice everywhere in the port (every paper button's word is a `HandLabel` where `_paper_card` uses `_font_d`), so it is a port-wide convention and not a select-stage defect. Changing it is a restyling of every screen, not a fix. |

## D-SELECT-3. Verified, not guessed

`Runway.EditorTools.DraftSelectProbe.Run` — **19 checks, 19 held, exit 0** — with
`bash tools/unity_compile.sh` → **0 errors** on the same tree.

The probe builds the select stage's hero with DraftSelectPage's own geometry (the
shadow at 465/742, the 560x560 holder pivoted on the feet, `DraftLoop.Attach`) and
photographs it. Each count is pixels inside the 560x560 hero box that differ from a
BASELINE frame of the same stage with the hero's image switched off — the floor shadow
is in both frames, so it cancels and what is counted is ink that is there because the
founder is there.

| shot | what it drives | ink in the hero box |
|------|----------------|---------------------|
| `0-baseline-empty-cone.png` | the hero's image off | — (the reference; this IS the defect) |
| `1-first-play.png` | cold cache, NO runner: the ask waits, the queue is pumped by hand | **97,142** of 313,600 (31.0%) |
| `2-after-reselect.png` | the loop destroyed and rebuilt, same founder, NOTHING pumped | **97,142** (31.0%) — the cache answered inside `Play` |
| `3-dead-target.png` | play, destroy the loop mid-queue, a new loop asks, then pump | **83,877** (26.7%) — the dead callback did not take the picture |
| `4-sheet-and-paper.png` | the two ledgers, and the draft card beside the title card | — (a look, not a count) |

Floor for the three hero shots is 20,000. Also asserted: a path with nothing on disk
answers at once and IS remembered as absent; a path whose file is on disk is never
answered "absent" merely because there was nothing to fetch on yet, and lands the
moment the queue is pumped; two asks for one path stay one queued job.

- **A file:// `UnityWebRequest` cannot complete under `-executeMethod`.** Measured, not
  assumed: `RUNWAY! texture load failed ()` with an EMPTY error is `result` still
  reading `InProgress` — nothing pumps the update loop while the method holds it, so
  `isDone` never flips. `ArtCache.PumpBlocking` therefore carries two routes and
  REPORTS which one each picture took (`route: 0 web, 3 disk` in batch); the queue, the
  ordering, the delivery and every waiting callback are the shipped ones either way.
  The 3s spin per job is why the run takes ~33s.
- **Nothing is playing, so the hydrator never runs in the probe** — `StartCoroutine` is
  inert outside play mode and only frame 01 is ever on screen, which is the frame the
  defect was about. The sway, the walk-on and the 36-frame breath belong to a run with
  a window.

## D-SELECT-4. The seam

Nothing to hook up: every change is inside the five files the stage already builds
from. The probe is run the way GlowShots is — headless WITH a graphics device, no
`-nographics`, under the same mutex `tools/unity_compile.sh` takes:

```bash
RUNWAY_SELECT_OUT=/tmp/d-select \
  /Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath unity \
  -executeMethod Runway.EditorTools.DraftSelectProbe.Run -logFile -
echo $?          # 0 = all 19 checks held
```

**Every editor launch re-bakes the font atlas.** `FontBaker` is `[InitializeOnLoad]`
with a static constructor, so a compile pass or a probe run — anything that starts the
editor — rewrites `Assets/Resources/Fonts/PatrickHand SDF.asset`, and the rewrite is
not byte-stable (two consecutive runs produced 101,163 and 98,296 changed lines). It is
not this lane's write and nothing here asks for it, but it means a batch run leaves a
large shared asset dirty in the working tree. Check `git status` after running the
probe and do not fold that file into an unrelated commit.

`ArtCache` grows three public members and no behaviour a run can reach differently:
`Load(path, cb, urgent)` (the third argument defaults to the old behaviour),
`Pending`, and `PumpBlocking()` with its two route counters — the last three exist for
a harness that has no frames, and the game never calls them.

---

# D-CHARTS — THE BINDER'S DRAWN CHARTS — `Game/DrawnChart.cs` + `Game/BinderScreen.cs` + `Game/BookIntroScreen.cs`

The nine binder pages were photographed against their Godot twins and eight drawings
came back wrong. `game/src/ui/binder.gd` is the truth for every number below; the
class it comes from is named in each row. Nothing outside those three files was
touched, and one new editor probe was added: `Editor/BinderChartShot.cs`.

## D-CHARTS-1. Blocking risks — these stop a compile if they are wrong

| # | API | Where | Why uncertain | Fallback coded |
|---|-----|-------|---------------|----------------|
| DC-A1 | `Transform.Find("shadow")` on the rect `GameUi.PaperSheet` returns | `BinderScreen.BuildParts`, `BookIntroScreen.OnBuild` | Both screens need a shadow the shared helper hard-codes at 0.18 alpha / 7×9, and `GameUi.cs` belongs to another lane. The child's NAME is the whole contract. | The lookup, the `GetComponent<Image>()` and the cast to `RectTransform` are each null-checked; a rename costs the corrected shadow and nothing else — the sheet still draws. If `PaperSheet` ever takes a style argument, both call sites should pass one instead. |
| DC-A2 | `Mathf.PI` used as a `const` initialiser (`const float TwoPi = Mathf.PI * 2f;`) | `DrawnChart.CapPie`, `BinderScreen.PieLabels` | Only legal because `Mathf.PI` is itself `const`. | It is, and `DrawnChart.Donut` already did this before this lane. |
| DC-A3 | `DrawnChart.Clock(int side, …)` fed from `BinderScreen.ClockSide` | `TabThreats` | `ClockSide` is deliberately `const int`, not `float`: a float would not convert implicitly and the compile would fail. | Declared `const int`; every use that needs a float (`Mount`, the text x) widens implicitly. |

## D-CHARTS-2. Runtime risks — they compile, but may not do what binder.gd does

| # | Thing | Where | Risk | Fallback coded |
|---|-------|-------|------|----------------|
| DC-B1 | **`Donut` and `CapPie` are two different drawings and must stay so.** | `DrawnChart` | The draft page's `CapTableDonut` (a ring, `innerFrac` 0.55, full alpha, no rim) and the binder's `_Pie` (a full pie at 0.38 of the box, 0.75 alpha, 4px ink rim) are separate classes in the original. Collapsing them "fixes" one page by breaking the other. | `Donut` was left byte-identical and still serves `DraftCrewPage`; `CapPie` is new and serves the binder alone. Their cache keys differ by prefix, so neither can serve the other's sprite. |
| DC-B2 | **The pie rim BLENDS; every other stroke in this file takes the brighter alpha.** | `DrawnChart.BlendStroke` / `BlendDisc` | The shared `Disc` is alpha-max so a stroke crossing itself does not blot. Laid over a 0.75-alpha wedge that rule eats the rim's soft edge wherever coverage is under 75%. | The rim, and only the rim, goes through a source-over compositor. Wedges, sparklines, the pen ellipse and the clock all still use the alpha-max path. |
| DC-B3 | **The sparkline's ground wash pre-fills the canvas at alpha 8.** | `DrawnChart.Spark` | `Disc` skips any pixel whose wanted alpha is not greater than what is there, so antialiased edge pixels under 3% coverage now keep the wash instead of the line. | Deliberate and immaterial — it is the outermost 1/32 of a stroke's falloff. The alternative, a separate `Fill` behind the sprite, would have put the wash outside the drawing the probe photographs. |
| DC-B4 | **The hi/lo numbers and the pie's labels are TMP labels, not raster.** | `DrawnChart.MountSpark`, `BinderScreen.PieLabels` | `draw_string` plants a BASELINE; `HandLabel` plants a top-left. The offset is the project's standing `0.78 × size` approximation (B5), so these five strings sit within a pixel or two of the original rather than exactly on it. | Same approximation the whole port already uses for `draw_string`. Every other coordinate on these pages is an explicit top-left transcribed from the `.gd` and is exact. |
| DC-B5 | **The pie's label loop reproduces a quirk of `_Pie._draw`.** | `BinderScreen.PieLabels` | A slice between 0.1% and 1% is DRAWN (the wedge loop skips at 0.001) but gets no label, and the label loop does not advance its angle past it — so every later label would be rotated backwards by that sliver. | Ported as-is rather than silently corrected, because the wedges and the names must agree about where a slice starts. Unreachable in practice: the three slices always sum to 100 and a founder holding under 1% has lost the game. |
| DC-B6 | **The pen ellipse is baked at its ink's own size and mounted 1:1.** | `BinderScreen.RingW/RingH/RingX` | If a caller ever mounts this sprite into a differently-sized rect, the stroke stretches again and the whole point is lost. | The three constants derive the box from the radii and the pad, so the box cannot drift from the sprite; `Refresh` moves it with `SetTopLeft` and never touches `sizeDelta`. |
| DC-B7 | **The ring and the tab rule are now created BEFORE the tab words.** | `BinderScreen.BuildParts` | Sibling order is z-order in uGUI. They were built after the words and drew over them; a Godot node draws under its own children, so the original passes the ring behind the label it circles. | Moved, not restyled. If anything is ever inserted between them and the words, it will land between the ring and the type. |
| DC-B8 | **The threats page's clock is drawn; ⚠ ▲ ▼ ↻ are still literal characters.** | `BinderScreen.TabThreats`, `TabLedger` | Only ⏰ was replaced. The other four ride whatever font fallback the hand font resolves to, which is another lane's mechanism. | The clock is a sprite that cannot fall back to a box. The remaining four are listed here so the fallback lane knows exactly which glyphs the binder still asks for. |

## D-CHARTS-3. Verified, not guessed — `Editor/BinderChartShot.cs`

Every drawing here is rasterised into a texture, so the evidence needs no camera and
no graphics device: the probe composites the real sprites onto cream at the SAME
top-left coordinates the binder mounts them at, writes a PNG each, then reads the
pixels back and states what it found.

```
Unity -batchmode -quit -nographics -projectPath unity \
      -executeMethod Runway.EditorTools.BinderChartShot.Shoot
```

`RUNWAY_CHARTS_OUT=<dir>` picks the output folder (default: a folder under the system
temp dir). It writes `pie.png`, `jar.png`, `spark.png`, `ring.png`, `ring_before.png`,
`clock.png` and `measurements.txt`. What the last run measured, against binder.gd:

| Drawing | Measured | binder.gd |
|---------|----------|-----------|
| cap pie | box 430×430, centre (255.0, 245.0), ink radius 165.8, no hole, slice alpha 191/255, rim present, wedges 62.0/18.0/20.0% | `size 430`, centre (255, 245), `r = minf(w,h) * 0.38` = 163.4 + half a 4px rim, `Color(col, 0.75)` = 191, `draw_arc(…, INK, 4.0)` |
| pie labels | (398.1, 309.2) · (12.0, 284.9) · (89.4, 69.7), all ink, all at r+40 = 203.4 from the centre | `p = c + Vector2(cos, sin) * (r + 40)`, `draw_string(font, p - Vector2(46, -8), …, 24, INK)` |
| debt jar | ground (166, 102, 78, 96) · level (168, …, 74, 94·fill) · a 4px outline round all 96 · lip (162, 99.5, 86, 5) | `draw_rect(6, 10, w-12, h-14)` · `(8, …, w-16, (h-16)·lv)` · `draw_rect(…, INK, false, 4.0)` · `draw_line((2,10) → (w-2,10), INK, 5.0)` |
| sparkline | corner pixel rgba(0,0,0,8) over 207 039 of 212 800 px; hi "13k" top-left (8, 6.4), lo "3k" top-left (8, 170.4) | `draw_rect(size, Color(0,0,0,0.03))`; `draw_string` at (8, 22) and (8, h-4), size 20, `Color(INK, 0.45)` |
| pen ring | 148×64 sprite mounted 1:1 → 140×54 of ink, 1321 coral px, stroke 3.50 all round | 33 points of `(cos·68, sin·26)`, jitter ±2, `draw_polyline(…, PEN, 3.5)` → ink up to 143×59 |
| pen ring, BEFORE | a 60r circle baked 128×128 and stretched into a 130×52 cell: 127×50 of ink, 607 coral px, stroke 3.28 across but 1.31 top and bottom | — a stretched circle cannot hold one width |

## D-CHARTS-4. What else the tab-by-tab read found

Read against `binder.gd` page by page. The columns, the tab pitch, the rules and every
label coordinate already matched and were not touched. Beyond the eight drawings:

- **`_tab_customers` had lost a clause.** At `analytics_level >= 2` the original ends
  its line with `· CAC roughly %s`, where the number is `"∞"` on no marketing and
  `int(mk / maxf(1.0, mk / 900.0))` otherwise — which is `min(marketing, 900)`. Level
  two was paying for a line the player already had. Restored.
- **The ledger printed a bare `?`.** The `$` lives in the original's format string, so
  an unknown CAC or LTV reads `$?`, not `?`. Restored.
- **The empty sparkline's placeholder moved.** `_Spark` puts its baseline at
  `size.y * 0.55`; the port had a top-left at `h * 0.4`, about 10px high on the vitals
  page. It now goes through the same baseline conversion as the hi/lo numbers.
- **Both sheet shadows were the shared helper's, not the screen's.** `_Clipboard` opens
  on `Color(0,0,0,0.25)` at (8, 12) and `book_intro_screen.gd`'s `_Sheet` on
  `Color(0,0,0,0.3)` at (8, 12); `GameUi.PaperSheet` gives every screen 0.18 at (7, 9).
  Each screen now corrects its own after the fact (DC-A1).

## D-CHARTS-5. The seam

Nothing. No other file needs a line. `BinderChartShot` lives under
`Assets/Scripts/Editor/`, so it is editor-only by folder, has no `Apply()` and
installs nothing — it runs only when `-executeMethod` names it.

---

# D-FLOW — THE SHELF, THE LOGOTYPE, THE TABLE — `Game/ShelfScroll.cs` + `Game/BirthScreen.cs` + `Game/DiceRoll.cs` + `Game/TurnRunner.cs` + `App/Build.cs`

Four defects measured against `game/src`, three of them things a player sees on the
first minute of a run. The Godot files they are measured against are
`screens/founder_draft_screen.gd` (`_ShelfBar`), `screens/book_intro_screen.gd`
(`_ScrollInk`), `ui/birth_screen.gd` and `ui/dice_roll.gd`. One new editor probe:
`Editor/FlowShots.cs`.

## D-FLOW-1. Blocking risks — these stop a compile if they are wrong

| # | API | Where | Why uncertain | Fallback coded |
|---|-----|-------|---------------|----------------|
| DF-A1 | `float.NaN` as a field initialiser and an `==` sentinel | `ShelfScroll._lastDrawn` | The redraw guard has to have a value no legal scroll offset can equal, and `Mathf.Approximately` cannot provide one — `0` is a legal offset and the shelf spends most of its life there. | `float.NaN` compares false against everything including itself, so the first draw always lands and any invalidation always redraws. It is a plain field, not a `const` default parameter, so no metadata encoding is involved. |
| DF-A2 | `Transform.GetChild(i) as RectTransform` + `RectTransform.rect.height` at build time | `ShelfScroll.TrackHeightOf` | Reading `rect` before a layout pass returns the wrong size for a rect with STRETCHED anchors. | Every track in this game is built by `DrawnUI.Fill`/`Rect`, which pin `anchorMin == anchorMax`, and for those `rect.size` is `sizeDelta` with no layout pass needed. The whole lookup falls back to the viewport height if it finds nothing, which is exactly what the code did before. |
| DF-A3 | `copied \|= Stage(...)` on a `bool` | `Build.EnsureSheets` | `\|=` on `bool` is bitwise-or-assign, and it does **not** short-circuit. | That is the wanted behaviour: every sheet must be considered, not just the ones before the first hit. |
| DF-A4 | `Runway.Build` named from inside `Runway.EditorTools` while `using UnityEditor;` is in scope | `FlowShots.SheetLedger` | `UnityEditor.Build` is a NAMESPACE. A bare `Build.X` is legal (namespace members beat using-directives) but reads as a trap. | Aliased at file scope: `using RunwayBuild = Runway.Build;`. Zero ambiguity, and it says which `Build` is meant. |
| DF-A5 | `Runway.Build.StagedSheetNames()` is public but the class is inside `#if UNITY_EDITOR` | `FlowShots` | A player build that could see it would not compile. | `FlowShots.cs` lives under `Assets/Scripts/Editor/`, so it is editor-only by folder and can never be compiled into a player. |

## D-FLOW-2. Runtime risks — they compile, but may not do what the original does

| # | Thing | Where | Risk | Fallback coded |
|---|-------|-------|------|----------------|
| DF-B1 | **The thumb is found by NAME and by COLUMN, not by reference.** | `ShelfScroll.TrackHeightOf` | The track is a sibling the caller drew; this reads its height off a child called `"track"` standing within 12px of the thumb's centre line. A rename, or a track built more than 12px away from its own thumb, silently costs the 476-vs-484 correction. | It falls back to the viewport height — the number the code used before, so a miss is the old behaviour, never a broken page. Both current callers (`DraftBagPage`, `BookIntroScreen`) name it `"track"` and centre thumb and track on the same x to within 0.0px. The column test is what lets one page carry two shelves. |
| DF-B2 | **The thumb's WIDTH is now written by the component, not by the caller.** | `ShelfScroll.Apply` | Both callers author a 7px thumb; Godot strokes 6. `Apply` overwrites `sizeDelta` wholesale, so a caller that wanted a different width would not get it. | Deliberate: the stroke width is Godot's, and the callers' x already puts a 6px thumb exactly where `draw_line` centred on absolute x 711 (draft) / 1325 (book) puts it. Changing it back is one constant. |
| DF-B3 | **The track stays drawn when there is nothing to scroll; Godot's shelf hides it.** | `ShelfScroll.Apply` | `_ShelfBar._draw` returns early at `maxs <= 8` and so draws neither line; `_ScrollInk` always draws both. One shared component cannot match both. | Only the THUMB is hidden (`enabled = maxs > 8`), which matches the book exactly and leaves the draft page one pencil line it did not have. A track with no thumb reads as "nothing to scroll", which is what it is. The component never touches the track object. |
| DF-B4 | **`SetContentHeight` now DRAWS, it does not just record.** | `ShelfScroll` | It is called from `BookIntroScreen.Relayout`, which runs inside a text-landed callback. It now writes `sizeDelta` and `anchoredPosition` on two rects from there. | Both are plain transform writes with no layout rebuild of their own, and `Relayout` is already writing rect positions on the line above. This is the fix for the book's thumb never appearing at all: the offset does not move when the column grows, so the old `_lastDrawn` guard swallowed every post-relayout draw. |
| DF-B5 | **The dice veil stops at 0.55 — the ceremony no longer owns the frame.** | `DiceRoll.VeilAt` | `dice_roll.gd` ramps its shade to a fully opaque 1.0, so this is a DELIBERATE divergence from the shipped Godot line, taken on the owner's live-play #179 ("background looks REALLY bad") and on that same file's own header: *"the page stays visible and darkens under the cup — no popping felt card"*. The code and its comment disagreed; the comment is what the owner asked for. | One constant, `DiceRoll.VeilCeiling`. Setting it to `1f` restores the Godot pixel exactly. The rise is unchanged at 0.55s. |
| DF-B6 | **The vignette disc is gone.** | `DiceRoll.BuildParts` | A `RingSprite` disc 1.16× the smaller screen axis used to sit under the die. Over a visible page it would read as a grey plate laid on the desk. | Removed rather than dimmed: at any alpha it is a flat circle over hand-drawn paper. The sheets carry their own alpha and their own light, which is what the disc was standing in for. |
| DF-B7 | **A missing streamed PNG is no longer "no sheet".** | `DiceRoll.SheetOnHand` | The gate used to be `RunwayPaths.ArtExists` alone. With the cup films staged into `Resources/Sheets`, a build without `StreamingAssets/Art/dice` would have skipped a ceremony whose film was sitting right there. | The `Resources` probe only runs when the streamed file is genuinely absent, and the texture it loads is the very one `SheetLoop` is about to play (Unity caches it, so the second load is free and `DiceRoll.OnDestroy` gives it back). `ReadingBeat.TextFx.DieSheet` still reads the STREAMED copy for the beat's still frame, which is why the PNGs stay staged as well. |
| DF-B8 | **`ShowDie` claims the top sibling slot immediately.** | `TurnRunner.ShowDie` | `Boot` builds the curtain first, so the cup already comes up above it; the extra `SetAsLastSibling` is belt-and-braces. If any future furniture is added to `TopLayer` after the cup and must sit ABOVE it, this is the line that will be in the way. | One line, and `DropRoutine` already did the same thing a moment later. The die-settle punch (`Impulse.DieSettled` in `DiceRoll.HoldThenFinish`) and the backfired flinch (`Impulse.Verdict` in `TurnRoutine`) were not touched. |
| DF-B9 | **The twenty cup films add ~200MB to the app.** | `Build.EnsureSheets` | 4096×2560 with alpha, block-compressed at 8 bits/px, is 10.0MB per sheet resident and on disk — five times the six title films put together. | Measured, not estimated (`FlowShots` reads each PNG's IHDR). Only ONE is resident at a time: `DiceRoll.OnDestroy` releases the loop and `SheetLoop.ReleaseSheet` hands a baked texture straight back through `Resources.UnloadAsset`. If the disk cost is ever the binding constraint, `SheetImport` can turn on `crunchedCompression` for `roll_*` alone — same VRAM, roughly a quarter of the bytes, at the cost of a decode hitch on load. That trade was NOT taken here: a hitch is the exact thing this fix exists to remove. |

## D-FLOW-3. Verified, not guessed — `Editor/FlowShots.cs`

Rendered headless with a real graphics device (`-batchmode -quit`, **no**
`-nographics`) at 1536×1024. The probe raises the SHIPPING components — the real
`ShelfScroll`, the real `BirthScreen.PlaceType`, the real `DiceRoll.Veil`/`VeilAt`
— so every number below is one the game produces.

```
RUNWAY_DFLOW_OUT=<dir> Unity -batchmode -quit -projectPath unity \
      -executeMethod Runway.EditorTools.FlowShots.Shoot
echo $?          # 0 = every assertion held
```

It writes `01-shelf-top.png`, `02-shelf-middle.png`, `03-shelf-bottom.png`,
`04-birth-logotype.png`, `05-dice-page-bare.png`, `06-dice-veil-rising.png`,
`07-dice-veil-settled.png`, `08-dice-old-blanked.png` and `measurements.txt`.

| Measured | Before | After | The original |
|----------|--------|-------|--------------|
| shelf thumb top at scroll 0 / 324 / 648 | 0.0 / 138.6 / 277.1 — track-RELATIVE, so at rest the thumb sat at the very top of the PAGE, 232px above its own track | 236.00 / 368.24 / 500.48, bottom 704.00 | `_ShelfBar` at (704, 232) size (14, 476): `ty = 4 + (size.y - 8 - th) * frac`, absolute `232 + ty`; last position bottoms out at `232 + 476 - 4` = 704 |
| shelf thumb size | 7 × 206.9 (sized against the 484 viewport) | 6.0 × 203.5 | `draw_line(…, 6.0)`; `th = max(size.y * clamp(scroll.size.y / grid.y, 0.1, 1), 30)` on the 476 BAR |
| a shelf that grew | thumb height frozen at whatever the first draw made it — the book's never appeared at all | 203.5 → 384.0 → 203.5 across `SetContentHeight` calls that never moved the offset | `_ShelfBar._process` guards on `scroll_vertical`, but Godot re-runs `_draw` on `queue_redraw`, and a resized `grid` is a new drawing |
| the book's thumb | never drawn | hidden while the column is empty, then at top 186.0 the moment the entry lands (track top 182) | `_ScrollInk` at (1316, 182) size (18, 660): `ty = 4 + (660 - 8 - th) * frac` |
| birth logotype top | 278.85 | 122.88 — **lifted 155.97px** | `draw_texture_rect(type, Rect2((w - lw) * 0.5, h * 0.12, lw, lh))` → 122.88 |
| birth logotype size | 952.32 × 259.46 (right, by luck: `Fit` clamps on the long side) | 952.32 × 259.46 | `lw = w * 0.62`, `lh = lw * 267 / 980` |
| journal paper under the dice, mean luma | 24.7 — the page was gone | 119.0, **51% of the open page's 233.8**; 187.9 while the veil is still rising at 0.22s | `dice_roll.gd`: "the page stays visible and darkens under the cup" |
| `Resources/Sheets` basenames | 6 | 26 — `birth_intro`, `birth_loop`, `curtain_loop`, `howto_1..3`, `roll_01..20`, all unique | `SheetLoop` looks a sheet up by BASENAME alone, so a collision would be a silently wrong film |
| dice sheets | streamed: 58.5MB of PNG, ~3.4s per load through `UnityWebRequest`, killable mid-load | staged imported as well: **+200.0MB** on disk, 10.0MB resident, one at a time | Godot pays this at export; `Resources/Sheets` is the Unity spelling |

Two traps this probe walked into and now documents:

- **`AssetDatabase.LoadAssetAtPath` is the WRONG texture to measure a fit against.**
  `type_main.png` is 980×267 on disk; the importer, left on its defaults, rounds it
  to the nearest power of two and hands back 1024×256. Measuring the fit against
  that gave a 166.66px error instead of the true 155.97. The runtime path is
  `ArtCache.Load` → `UnityWebRequestTexture`, which decodes the file as it is, so
  `FlowShots.TypeTexture` reads the bytes off disk and only falls back to the
  imported asset.
- **The evidence run needs the compile mutex too.** `tools/unity_compile.sh` takes
  `$TMPDIR/runway_unity_compile.lock`; a `-executeMethod` run opens the same Library
  and must queue behind it, or two editors corrupt it. A run started without the
  lock picked up a sibling lane's half-saved file and logged its compile errors into
  the middle of this probe's output.

## D-FLOW-4. The seam

Nothing. Every change is inside a file this lane owns, and no call site moved:

- `ShelfScroll.Attach` keeps its five-argument signature — the track's top and
  height are read off the page the caller already drew, so `DraftBagPage` and
  `BookIntroScreen` are untouched.
- `BirthScreen.PlaceType` replaces one `GameUi.Fit` call inside the same file.
- `Build.EnsureSheets` stages the dice itself; **nothing is copied until somebody
  builds**, which is why the twenty 10MB textures do not slow a compile pass down
  today.
- `FlowShots.cs` is editor-only by folder, installs nothing, and runs only when
  `-executeMethod` names it.

---

# D-TYPE — the two hands, the borrowed glyphs, and Godot's line box

The port drew every word in Patrick Hand at TMP's own metrics. Godot draws headings
in a second face, lays lines out on FreeType's rounded box plus a theme constant,
and quietly borrows an OS face for every character its own fonts lack. This section
is the three of those, plus the four geometry fixes that fell out of the same read.

Measured against Godot 4.7.1 itself, headless (`FontFile.load_dynamic_font` on the
same two `.ttf` files the game ships, `Label`/`TextParagraph` laid out and read
back), not against a screenshot.

## D-TYPE-1. Blocking risks — these stop a compile if they are wrong

| # | API | Where | Why it was a guess | Fallback |
|---|---|---|---|---|
| D-TYPE-A1 | `TMP_FontAsset.CreateFontAsset(string fontFilePath, int faceIndex, int samplingPointSize, int atlasPadding, GlyphRenderMode, int atlasWidth, int atlasHeight)` | `FontBaker.BakeGlyphs` | The path overload is newer than the `Font` overload the hand bake uses, and TMP has shipped four different `CreateFontAsset` shapes. | Verified present in `com.unity.ugui@8bb446d869cd/Runtime/TMP/TMP_FontAsset.cs:511`. If it ever goes, the family-name overload on the next line (`CreateFontAsset(familyName, styleName, pointSize)`, `:490`) does the same job through `FontEngine.TryGetSystemFontReference`, and the bake already falls through to it when the file path does not exist. |
| D-TYPE-A2 | `TMP_FontAsset.TryAddCharacters(string, out string missing)` | `FontBaker.BakeGlyphs` | Four overloads exist; only two report what was NOT added, and the bake's whole point is knowing which glyphs a face could not supply. | `:2011`. The whole bake is inside `try/catch` and the runtime ladder in `DrawnUI.Glyphs` builds the same chain from the OS by family name. |
| D-TYPE-A3 | `TMP_FontAsset.atlasPopulationMode` has a **setter** | `FontBaker.BakeGlyphs` | Freezing a dynamic asset to `Static` is what lets the borrowed glyphs ship without their font program. | `:90`, and the setter also nulls `m_SourceFontFile` for `Static`, which is exactly the wanted behaviour. |
| D-TYPE-A4 | `TMP_TextInfo.characterInfo[i].xAdvance` | `DrawnUI.MeasureWidth` | The pen position after a character, which is what Godot's `get_string_size()` returns. TMP also carries `lineInfo[].maxAdvance`, which drops trailing whitespace. | `GetPreferredValues(text).x` is kept as the catch, and a crude `length * size * 0.5f` under that. |
| D-TYPE-A5 | `FaceInfo.ascentLine` / `.descentLine` / `.lineHeight` / `.pointSize` | `DrawnUI.AscentRatio` etc. | Field names, not properties, on a TextCore struct that has been renamed once already (`Ascender` → `ascentLine`). | Every reader guards `pointSize > 0f` and falls back to Patrick Hand's literal hhea numbers (1.042 / 0.312). A renamed field is a compile error, not a silent zero. |
| D-TYPE-A6 | `TMP_Text.lineSpacing` is in **hundredths of the font size** | `DrawnUI.Written` | The property is documented as "font units" and the unit is not obvious. | Read off `TMP_Text.cs:3919`: `currentEmScale = fontSize * 0.01f * orthographicMultiplier`, and `m_lineOffset += … + m_lineSpacing * currentEmScale` (`:4698`). **Measured**: the probe's pitch table is exact to 0.00px at nine sizes, which only holds if the unit is right. |

## D-TYPE-2. Runtime risks — they compile, but may not do what Godot does

| # | Risk | Where | Detail | What was done |
|---|---|---|---|---|
| D-TYPE-B1 | **TMP's fallback walk stops dead at the first null entry.** | `DrawnUI.Chain` | `HasCharacter` and the shaping path both loop `for (i = 0; i < table.Count && table[i] != null; i++)` (`TMP_FontAsset.cs:1298`). The shipped `PatrickHand SDF.asset` carried **three empty slots** followed by a real entry, so a perfectly good face sat in the list and was never once asked for a character. | `Chain` strips every null before it inserts, and inserts at index 0. This was found by the probe, not by reading: the borrowed glyphs resolved on the display hand and not on the writing hand, and the only difference between them was three holes. |
| D-TYPE-B2 | **The borrowed glyphs come from an OS face at BAKE time.** | `FontBaker.GlyphFaces` | `/System/Library/Fonts/Supplemental/STIXTwoMath.otf` (22 of 25) and `Arial Unicode.ttf` (the pencil and the cross). Only the rasterised glyphs and their metrics land in the asset — no font program travels — but the bake itself only works on a machine that has those files. | Three rungs: the baked asset out of `Resources` first (which is what ships), the same face asked for by family name at runtime second, nothing third. A machine with neither gets the boxes it had before, and the game still runs. Godot does the identical borrowing at runtime — its importer ships `allow_system_fallback=true` — so this is the same behaviour moved earlier. |
| D-TYPE-B3 | **The borrowed face is not the face Godot borrows.** | the glyph chain | Godot on macOS resolves ★ ✓ ⏰ ⚠ through the system list, which for the emoji-range characters means Apple Color Emoji — a **colour bitmap** face TMP cannot use in an SDF atlas at all. STIX Two Math draws the same characters as black vector outlines. | Chosen deliberately: a monochrome outline sits in a hand-drawn game better than a colour emoji, and it is the only option TMP has. The one place it is loud is ⚠ (U+26A0), which STIX draws as a large open triangle. Two callers use it (`BinderScreen.cs:265`, `DraftCrewPage.cs:175/195`) — **not files this lane owns**. |
| D-TYPE-B4 | **A fallback glyph can change a line's height.** | any line containing a borrowed character | TMP takes the line's ascender from the tallest element on it, so a fallback with a taller face would push a body line down. | STIX's ascent is 0.762 of its em against Patrick Hand's 1.042 and Baloo2's 1.078, so it can never be the tallest thing on a line. Both faces are baked at `pointSize` 90, so no em rescaling is involved either. |
| D-TYPE-B5 | **The first baseline is 0.5–1px above Godot's.** | `DrawnUI.Written`, every label | Godot's ascent is FreeType's, rounded UP to a whole pixel (`FT_PIX_CEIL`): 32px at size 30 against TMP's continuous 31.26. TMP places its first line by its own ascent and there is no way to ask it for the rounded one without fighting `TextAlignmentOptions.Top`. | Left alone. It is under a pixel, it is uniform rather than cumulative, and the LINE PITCH — which does accumulate — is exact. `InkString` is exact on its own terms: the probe asked for baseline 90.00 and got 90.00. |
| D-TYPE-B6 | **`DrawnUI.Rule` puts its stroke half a pixel below the y it is given.** | `DrawnUI.Rule` | The host sits at `y - pad` and `WobbleLineSprite` centres the polyline at `th * 0.5` where `th = pad * 2 + 1`, so the stroke lands at `y + 0.5`. | **Not fixed.** Correcting it moves every rule in the game up half a pixel and the blast radius is every screen, not this lane's three files. Noted so the next reader does not re-derive it. |
| D-TYPE-B7 | **The dynamic hand and display assets grow on disk when an editor session renders text.** | `Assets/Resources/Fonts/*.asset` | `PatrickHand SDF.asset` is 10KB empty and 2.7MB after an editor pass has rendered a few hundred characters into its atlas; `AssetDatabase.SaveAssets()` from an unrelated bake persists it. | `RUNWAY!/Rebuild the fonts` deletes and re-bakes all four, and the tree is left in that clean state. Nothing depends on the atlas being pre-populated: the fallback wiring lives in `DrawnUI.LendGlyphs`, which runs on first use in the editor and in a player alike. |

## D-TYPE-3. Verified, not guessed — `Editor/TypeShot.cs`

`Unity -batchmode -quit -projectPath unity -executeMethod Runway.EditorTools.TypeShot.Shoot`
(**no `-nographics`** — the shots need a device to rasterise type with; the numbers
below come out either way). Four PNGs and a `measurements.txt`.

- **Line pitch is exact at every size tested.** Godot lays a `Label` out as
  `ceil(ascent) + ceil(descent) + 3`, the 3 being the default theme's
  `line_spacing` constant. At 19/21/24/26/28/30/34/48/58 the ported label now
  measures 29/32/37/40/42/45/50/69/83px baseline to baseline — **0.00px error at
  all nine**. TMP's own metric would have given 25.73/28.43/32.50/35.20/37.91/
  40.62/46.04/64.99/78.53, i.e. **12.2% tight at 24 and 9.7% tight at 30**.
- **The two line boxes are different and both are now right.** A Godot `Label`
  gets the theme's 3px; `draw_string`/`draw_multiline_string` go through no theme
  and get none. At size 30 that is 45px against 42px, measured on both sides.
  `HandLabel`'s `leading` parameter is which one, and it defaults to the `Label`
  case because that is what most call sites transcribe.
- **`draw_string` baselines land where they are asked.** The probe asked for
  baseline 90.00 at size 56 and measured 90.00. The `size * 0.78` guess it
  replaces put it at 104.67 — **14.67px low**, which is the how-to title sitting
  into its own film frame.
- **Widths match Godot to a pixel.** `'NEXT  '` @34 → 85.11 against 85.00;
  `"GOT IT — LET'S FOUND SOMETHING  "` @32 → 419.01 against 418.00;
  `'CHOOSE YOUR FOUNDER'` @58 → 514.77 against 515.00 in the hand and 638.75
  against 639.00 in the display hand.
- **The display hand is 24% wider than the writing hand at the same size**, which
  is the whole reason it has to be the right one: 639 against 515 at 58.
- **All 25 borrowed characters resolve**, on both hands, including the five that
  prompted this (U+2605 U+2713 U+23F0 U+26A0 U+2610). `glyphs.png` is the proof —
  every cell drawn, no boxes.

## D-TYPE-4. What the probe did NOT confirm

- **`GetPreferredValues` was not the cause of a wide how-to button.** Measured on
  this machine, the shipped path (`GetPreferredValues` on a hand with no borrowed
  face) returns 187.11 for the `NEXT  →` card against Godot's 187.00. The arrow
  was already being drawn — by `LiberationSans SDF - Fallback`, TMP's own default,
  at an advance of exactly 34.00px, which happens to be exactly what Godot's system
  fallback gives it. There is no 23–25px of padding to subtract.
- **What `GetPreferredValues` DOES get wrong is trailing whitespace**, which it
  drops and Godot counts: `'NEXT  '` @34 measured **69.47 against 85.00** (−15.53px)
  and the GOT IT stem @32 **404.29 against 418.00** (−13.71px). Every card sized
  from a word ending in a space was that much narrow. The pen-position read fixes
  it and is Godot's own definition of the call.
- **Net effect on the how-to button is −1.66px**, not −23: 187.11 → 185.34 against
  Godot's 187.00. The 1.66 is the arrow moving from TMP's implicit LiberationSans
  fallback (34.00px) to the game's own shipped glyph face (32.23px). Accepted:
  a face that ships and is named beats a face that happens to be in TMP's settings.

## D-TYPE-5. The seam

`DrawnUI` gained API; **no call site outside this lane's three files moved**.

- `HandLabel` keeps its signature and gains one optional trailing `leading`
  parameter, defaulted to the Godot `Label` value. Every existing caller compiles
  unchanged and gets the right box.
- `MeasureWidth(text, size)` keeps its signature; the new
  `MeasureWidth(text, size, font)` overload is what a display-font caller needs.
- `DisplayLabel`, `Display`, `Glyphs`, `Ascent`, `GodotLineSpacing`, `DiscSprite`
  and `RingSide` are new and unused outside this lane and the probe.
- `DrawnUI.PaperButton` was deliberately **not** re-routed to the display hand:
  `title_screen.gd` loads Patrick Hand only, so a wholesale swap would put the
  title screen in the wrong face.
- `FontBaker`'s menu item is now `RUNWAY!/Rebuild the fonts` (it bakes four assets,
  not one). `Editor/Bootstrap.cs:44` calls `FontBaker.Rebuild()`, which still
  exists with the same signature.

---

# D-ROUTE — which hand each word is in, and the cues the flow was missing

D-TYPE proved the display hand exists, measures right and bakes its borrowed glyphs.
It deliberately routed **nothing**: `DrawnUI.Display` shipped with one caller, the
probe. This section is the routing itself, plus the sound the same screens were
short of, plus one line of copy that was not true.

The rule is Godot's own and it is **per site, never wholesale**. Anything
`_font_d`/`_dlabel`/`_style_button`/`_paper_card`/`_bare_button`/`_ink_button`/
`_rule_under`/`StatPips`/`TraitPips` draws is Baloo2 Bold; anything `_label`/`_font`
draws is Patrick Hand. Every site in the eleven files was read against
`founder_draft_screen.gd` / `garage_view_screen.gd` before anything moved:
**48 went to the display hand** (27 labels, 15 paper-card captions, 6 pen-marked
controls) and **47 were checked and stayed in the writing one**. The two live side
by side on the same card more often than not, which is why a wholesale swap would
have been worse than no swap at all.

## D-ROUTE-1. Blocking risks — these stop a compile if they are wrong

| # | API | Where | Why it was a guess | Fallback |
|---|---|---|---|---|
| D-ROUTE-A1 | An optional trailing `TMP_FontAsset font = null` on three public button factories | `DrawnUI.PaperButton`, `DrawnUI.FlatButton`, `GameUi.InkWord` | Adding a parameter to a public method breaks any caller that already passes something positional in that slot. `PaperButton` had `hoverScale` and `PaperStyle?` before it; `FlatButton` and `InkWord` had `TextAlignmentOptions`. | Every caller was enumerated before the change — 7 `PaperButton` (three of them editor probes), 7 `FlatButton`, 14 `InkWord` — and none passes anything past the new tail. The default is `null`, which resolves to `Hand`: the exact behaviour that shipped. Compile is clean at 0 errors. |
| D-ROUTE-A2 | `DrawnUI.Written(font, …)` reachable from `PaperButton`/`FlatButton` | `DrawnUI` | `Written` is the private core `HandLabel`/`DisplayLabel` both wrap; the two button factories called `HandLabel` and had to reach one rung lower to take a font. | Same class, same file — a private static is visible to its own type. If it ever moves, `DisplayLabel`/`HandLabel` on a branch does the same job in two lines. |
| D-ROUTE-A3 | `Runway.Audio.Sfx` from `Runway.Game` and `Runway.Screens` | six files gained `using Runway.Audio;` | `Sfx` is a static class in a sibling namespace with no assembly definition between them; `GarageScreen` and `JournalSpreads` already reached it, which is the precedent. | Nothing in `Runway.Game` declares a competing `Sfx`, so the `using` cannot become ambiguous. Verified by compile. |
| D-ROUTE-A4 | `DrawnUI.Ascent(TMP_FontAsset, float)` | `DraftCrewPage` donut hole | The two-argument overload is newer than `Ascent(float)` and is what converts a `draw_string` BASELINE into a TMP top-left for a font that is not the hand. | Public since D-TYPE, and it guards `pointSize > 0f` internally with Patrick Hand's literal hhea numbers under it. A null display font returns the hand's ascent, which is 3% short and still on the right line. |

## D-ROUTE-2. Runtime risks — they compile, but may not do what Godot does

| # | Risk | Where | Detail | What was done |
|---|---|---|---|---|
| D-ROUTE-B1 | **Every draft door now answers, including the six Godot leaves silent.** | `FounderDraftScreen.Nav` | The original plays `cash.wav` on most doors and on **none** of the back arrows or the recruit modal's hang-up. Wrapping the factory covers all **fourteen** Nav sites at once, six of which the original does not cue. | Deliberate, and the one place this lane departs from the `.gd`. A back arrow that clicks and says nothing reads as a click that did not land; the alternative is fourteen call sites each remembering. The cue is the same `cash` at the same 0dB, so nothing new is heard, only more often. |
| D-ROUTE-B2 | **The first cue of a run can be dropped rather than played.** | `Sfx`, everywhere | `Sfx` is lazy per cue: the first ask of a cold cue starts the load and only plays it on arrival if that lands inside `LateWindow` (250ms). `DraftSelectPage.Select` is called once at build with index 0, so on a cold machine the boot click may be swallowed while `cash.wav` decodes. | Left alone — it is `Sfx`'s own documented contract and it is right (a whoosh answering 400ms late reads as a fault). If the boot click is ever wanted, `Sfx.Warm(Sfx.Cue.Cash, Sfx.Cue.CardFlip, Sfx.Cue.Deposit)` in `OnBuild` is the Godot `_ready()` behaviour and is one line. |
| D-ROUTE-B3 | **The cap-table hole stopped being one label and became two.** | `DraftCrewPage` | `CapTableDonut._draw()` writes the hole with **two** `draw_string` calls in Baloo2 Bold: the number at 34 on a 90px centred field at full ink, and `"yours"` at 18 on a 64px field at 70% ink, 28px of baseline apart. The port had folded them into `"100%\nyours"` on one wrapped 30px hand label, which gave the word under the number the number's own size and alpha. | Split, at the transcribed geometry: `centre + (-42, +4)` and `centre + (-32, +32)`, each top-left computed as that baseline less `DrawnUI.Ascent(Display, size)`. Only the number is re-bound on `Refresh`; `"yours"` never changes. The field is `_donutPct` now, not `_donutLabel` — the rename is contained to this file. |
| D-ROUTE-B4 | **`⚠` on the crew toggles is now shaped by the display hand's fallback chain.** | `DraftCrewPage` FULL-TIME / VESTED | D-TYPE-B3 flagged U+26A0 as the loud one: STIX Two Math draws it as a large open triangle. The character has not changed and neither has the face that supplies it — `LendGlyphs` hangs the same chain off both hands — but it now sits beside Baloo2 letterforms instead of Patrick Hand ones. | Accepted. Godot has the identical pairing (`_ink_button` sets `_font_d` and Godot's own system fallback draws the triangle), so this is the original's look, not a new one. |
| D-ROUTE-B5 | **The dice cues fire only when a sheet is actually on hand.** | `DiceRoll.BuildParts` | `dice_roll.gd` opens its whoosh in `_ready()` — before `roll(n)` has looked for a sheet — so the original plays a curtain even for a roll it then skips. The port's missing-sheet branch returns above the cue. | Deliberate: a whoosh with no cup after it is a sound with nothing attached. Both cues sit immediately after `PlaySheet`, so the ceremony that sounds is the ceremony that plays. Levels are the original's: `Curtain(-6f, 1.25f)` is the cue's own -8dB plus a -6 trim = -14dB at 1.25x, and `DiceRattle()` bare is -6dB. |
| D-ROUTE-B6 | **`Toggle` cues after the full-bag refusal, not before it.** | `DraftBagPage.Toggle` | `_toggle_bag` returns out of its shake branch **without** reaching `_sfx_click.play()`, so a bag that will not take the thing is silent. Cueing on entry would make a refusal sound exactly like an acceptance. | The cue sits after the if/else, which is where the `.gd` puts it. The take-it-back-out path reaches it too, which the original also does. |
| D-ROUTE-B7 | **A heading's rule is now measured in a wider hand, so it is 24% longer.** | `FounderDraftScreen.Heading` | Nothing on the seven pages sits under a heading closely enough to collide, but the rule now runs to 639px at 58 instead of 515. The widest is `CHOOSE YOUR FOUNDER` at x=60, ending at 701 — clear of the stat sheet at 936. | Checked at all seven headings plus the recruit modal's. The tightest is `WHO DO YOU CALL?` at x=188 @48, whose rule ends around 630 and whose first card starts at 188/y=306, a row below. |

## D-ROUTE-3. Verified, not guessed — `Editor/RouteShot.cs`

`Unity -batchmode -quit -projectPath unity -executeMethod Runway.EditorTools.RouteShot.Run`
(**no `-nographics`** — the frame needs a device to rasterise type with). Writes
`route-select.png` and `measurements.txt` to `$RUNWAY_ROUTE_OUT`.

One frame, built through the **shipping** calls — `FounderDraftScreen.Heading`,
`GameUi.StatPips`, `GameUi.TraitPips`, `DrawnUI.PaperButton` — so which hand each
element ended up in is looked at, not asserted about. It carries its own control:
the page heading is drawn twice, once through the shipping path and once the way it
read before, each under a rule measured in its own hand, with a coral tick at the
width Godot's headless probe reported.

- **The heading now measures what Godot measured.** Display 638.75 against Godot's
  639.00 (−0.25px); the hand measures 514.77 against 515.00. The rule under every
  draft heading was cut to the second number and is now cut to the first —
  **123.98px short of its own word at 58**, which the frame shows as a coral rule
  stopping a fifth of the way before the tick.
- **The trait rule was short by the same proportion.** `TraitPips` measures the word
  it just wrote: PARANOIA 89.03 display against 70.72 hand, CHARISMA 89.81 against
  70.97, LUCK 44.40 against 37.86. Both the word and its measurement moved together,
  so the dotted rule under each trait name now ends where the name does.
- **Both fonts resolve and they are different objects.** `PatrickHand SDF` and
  `Baloo2 SDF`, ascent 1.04x and 1.08x. The probe says so out loud if `Display`
  ever falls back to `Hand`, because that is the state in which every route below
  looks correct and none of them did anything.
- **The frame shows the mixed sheet, which is the point.** On one card:
  `THE GARAGE HACKER` display over a Patrick Hand tagline; BUILD/SELL/RAISE/RECRUIT/
  GRIT display; `HIDDEN TRAITS` display beside `click any trait for what it does` in
  the hand; six display trait names with hand-written `+1`/`−1` beside them;
  `IN THE BANK, DAY ONE` in the hand over `$12,000` in display. That interleaving is
  what a wholesale swap would have destroyed.
- **The cap-table hole reads as two things again**: `62%` at 34 full ink over
  `yours` at 18 at 70%, centred in the donut, on the crew page's own paper.
- **Both papers and both hands, side by side**: the draft's LOCK IN card (display
  caption, shadow (7,9) at 0.18) beside the title screen's (writing hand, (4,5) at
  0.35).
- 604,754 lit pixels of 1,572,864 — the device rasterised.

**Running it dirties two files** (D-TYPE-B7): rendering this much Baloo2 populates
`Assets/Resources/Fonts/Baloo2 SDF.asset` from 6KB to 1.9MB, and the editor persists
it. The tree here was restored to its committed state afterwards; nothing depends on
a pre-populated atlas, because the fallback wiring lives in `DrawnUI.LendGlyphs`,
which runs on first use in the editor and in a player alike.

## D-ROUTE-4. What stayed in the writing hand, and the line that proves it

34 sites were checked and left alone. The pattern is consistent enough to state as a
rule: **on this flow the printed thing is the label and the written thing is the
value's company** — headings, names, numbers and controls are `_dlabel`, and every
caption, subtitle, hint, blurb, mood and joke around them is `_label`.

| Left in the hand | `.gd` evidence |
|---|---|
| the tagline under the founder's name, `_dTag` | `founder_draft_screen.gd:541` `_label(…, 26, …)` |
| `click any trait for what it does` | `:576` |
| `IN THE BANK, DAY ONE`, `PERK`, and the perk text | `:603`, `:609`, `:612` — three `_label` around one `_dlabel` number at `:606` |
| the tip's body under its display head | `:643` (head is `_dlabel` at `:633`) |
| `LOCKED IN`, the 110px stamp | `:894` — the biggest word on the page, and it is hand-written |
| every page's subtitle line | `:923`, `:1087`, `:1154`, `:1297`, `:1517`, `:1622` |
| the shape card's description and its `✓` chip | `:1231`, and the chip is built on `_font` at `:1243` |
| the money card's cost line and flavour | `:1559`, `:1564` |
| the money strip's preview line | `:1582` |
| `everything you own`, the shelf's section captions, `▼ scroll` | `:1638`, `:1696`, `:1773` |
| the item detail's blurb and slot cost | `:1826`, `:1831` |
| `nothing packed yet.`, the packed ticks and names | `:1888`, `:2136`, `:2141` |
| the bag summary and the loadout note | `:1907`, `:1922` |
| the `+1`/`−1` beside each trait row | `:2751` — `TraitPips._draw()` uses `font`, not `fontd`, for this one element |
| `your slice`, the company name, `OUTVOTED!`, `is yours to run` | `:2297`, `:2303`, `:2310`, `:2316` |
| the cofounder's mood line | `:2434` |
| the empty chair's `☎` and `an empty chair` | `:2456`, `:2468` (only `+ RECRUIT` between them is `_dlabel`, `:2462`) |
| what a role gives you, on the recruit card | `:1469` |
| `whoever you call will want ~25%` | `:1493` |
| `the cap table`, under the donut | `:1321` |
| the ItemCard's hand-lettered name | `:2782` — its own `_draw` loads Patrick Hand |
| the garage's cap-table scrap, `%\nyours` | `garage_view_screen.gd:264` `_mk_label`, beside two `_mk_dlabel` chips at `:231` and `:311` |
| every `ModStrip`/`TokenLine` token | `:2846` — the strip loads Patrick Hand for the whole line |

## D-ROUTE-5. The seam

- **Three public signatures gained an optional trailing font**, defaulted to `null`
  → the writing hand. `DrawnUI.PaperButton`, `DrawnUI.FlatButton`, `GameUi.InkWord`.
  Every existing caller compiles and renders unchanged; the title screen, the keys
  screen, `MissingScreen`, `BinderScreen`, `BookIntroScreen` and `AutopsyScreen` were
  all read and all stay in the hand.
- **`FounderDraftScreen.Nav` is the only place a draft cue is registered.** Fourteen
  doors, one `Sfx.Cash()`. A page that wants a different cue passes its own `onClick`
  and gets both, which is what the original does on the pages that cue twice.
- **`DraftCrewPage._donutLabel` is now `_donutPct`** and there are two labels where
  there was one. Nothing outside the file touched either.
- **`GameUi.StatPips` and `GameUi.TraitPips` route internally.** No caller changed,
  including `Editor/DraftSelectProbe.cs`, which photographs both.
- **`GarageScreen`'s three HUD chips moved; its cap-table scrap did not.** The room's
  other five hand labels (the missing-run note, the item note's two lines, the paint
  ribbon, the dread-beat fun fact) are `_font` in the `.gd` and were left.
- **`KeysScreen`'s storage line is now true.** It read "never sent anywhere but
  OpenAI"; `SceneDirector.MiddlewareCall` sends this very key to the game's own
  render middleware as an `x-openai-api-key` header so the rooms can be painted. It
  now reads "sent only to OpenAI and the game's own art painter" — same voice, same
  band, one line, and nothing the build cannot keep.

---

# CONFIG: the settings, the packages, the build and the launch

`ProjectSettings/*.asset` + `Packages/manifest.json` + `App/Build.cs` + the shipped
`.app`. This lane changed nothing. It read the files, parsed the shipped build, and
timed a real launch; the ranked lever table, the streamed-art migration plan and the
verbatim settings diff live in `docs/config-plan.md`.

Baseline it was written against: Unity 6000.0.82f1, Apple M4 Pro, Metal, build stamp
`2026-08-22 20:24 · e8ab078`, `RUNWAY!.app` at 682 MB, energy probe at 280 to 405 fps
and 2.43 to 3.57 ms per frame with `gc/s` flat at 0.00 on all fourteen rows.

## CONFIG-1. Blocking risks: these break a build or a harness if a lever is applied wrong

| # | Setting | Where | Why it bites | What to do instead |
|---|---------|-------|--------------|--------------------|
| CFG-B1 | **Managed stripping blinds the perf probe.** | `UnityPerf.BindUi`, `UnityPerf.StatInt` | Both resolve by **string name**: `CanvasUpdateRegistry`'s private `m_LayoutRebuildQueue` / `m_GraphicRebuildQueue`, and `UnityEditor.UnityStats` via `Type.GetType`. Managed stripping (IL2CPP only, but that is exactly what a backend switch turns on) is designed to remove precisely this. The failure is **silent**: `rebuild/s` degrades to `n/a`, which the file itself documents as a legitimate outcome, so nobody would notice the harness had stopped measuring. | Stay on Mono. `managedStrippingLevel` and `stripEngineCode: 1` are already inert under Mono, so today there is no exposure. If IL2CPP is ever taken, a `link.xml` preserving `UnityEngine.UI` is mandatory **and** the probe needs an assertion that `rebuild/s` is not `n/a`. |
| CFG-B2 | **`Resources.UnloadAsset` vs `Destroy`.** | `ArtCache.Sweep` line 90 | `Sweep` calls `UnityEngine.Object.Destroy(t)` on every eviction. The moment `ArtCache` gains a Resources-first path, that destroys the **shared imported instance**: the next `Resources.Load` of the same path can hand back a destroyed object, and in the editor it can damage the asset itself. | Record provenance per cache entry and branch, exactly as `SheetLoop._sheetBaked` already does at line 339: `Resources.UnloadAsset` for baked, `Destroy` for streamed. This is the highest-consequence item in the whole migration. |
| CFG-B3 | **Deleting quality levels renumbers them.** | `QualitySettings.asset` | `m_CurrentQuality: 5` and every entry in `m_PerPlatformDefaultQuality` name **index 5**. Collapsing six levels to one without rewriting those nineteen keys leaves the project pointing at an index that no longer exists. | The diff in `docs/config-plan.md` 5.1 replaces the whole block, including all nineteen platform keys, with 0. Do not hand-edit the array alone. |
| CFG-B4 | **`Assets/Art` cannot be hidden by CONFIG alone.** | `Build.cs` `SourceArt`, `EnsureSheets`; `RunwayPaths.ArtRoot` | Renaming the folder to `Art~` (the only way to stop Unity importing 505 unused PNGs in place) breaks four hard-coded paths across two files plus a `.gitignore` line. Applying the rename without them makes `EnsureSheets` log `no sheet source` twenty-six times and ship a build with no films. | Treat it as a coordinated art-lane edit, not a settings change. `docs/config-plan.md` 5.5 lists all six touch points. The CONFIG-only alternative is to accept the editor cost: the build output is byte-identical either way. |

## CONFIG-2. Runtime risks: they build, but they change what the player gets

| # | Thing | Risk | What it costs, measured |
|---|-------|------|--------------------------|
| CFG-R1 | **`npotScale` silently resamples.** | `TextureImporterNPOTScale.ToNearest` rescales any non-power-of-two texture to the nearest POT. This is not a hypothetical: it is what `Assets/Art` does **today**, and it is measured out of `Library/Artifacts`. | `env_bed.png` 391x391 imports as **512x512**. `chr_loop_dropout_03.png` 378x378 imports as **256x256**, a 32% linear downscale of a founder animation frame. `chr_founder_grab_01.png` 347x347 likewise 256x256. Any Resources migration must set `npotScale = None`. |
| CFG-R2 | **Mipmaps cost 33% and buy nothing.** | `Assets/Art`'s importer has `enableMipMap: 1`. UI quads on a fixed 1536x1024 stage are never minified. | Measured 10.67 bits per pixel where the format is 8 bpp: `chr_loop_dropout_03` imports at 256x256 DXT5 with **9 mip levels**, 87,408 bytes instead of 65,536. `SheetImport` already sets `mipmapEnabled = false` for `Resources/Sheets`; the migration branch must too. |
| CFG-R3 | **BC7 is a size regression on this content.** | The brief, and `SheetImport.cs`'s own comment, both say the sheets ship as BC7. They do not, and what they actually ship as is **better**. | Parsed from `resources.assets`: `birth_loop`, `curtain_loop` and the three `howto_*` sheets are all 5120x4608, `m_CompleteImageSize` 11,796,480, `m_TextureFormat` **10 = DXT1** at 4.00 bpp, because the source PNGs are RGB with no alpha. `roll_01` is 4096x2560, 10,485,760, format **12 = DXT5** at 8.00 bpp. Forcing BC7 (8 bpp) on the six title sheets would **add 59 MB**. The rule is: `Compressed` for RGB source (yields DXT1, 4 bpp), `CompressedHQ` for RGBA source (yields BC7, 8 bpp, same size as the DXT5 it replaces and better quality). |
| CFG-R4 | **`GameUi.Fit` has no `uvRect`.** | `Fit` sizes the RectTransform from `tex.width / tex.height` and stretches the whole texture across it. Padding a texture to a multiple of 4 at import time would therefore draw the padding and shift the aspect. | 198 of the 478 migration candidates have a dimension not divisible by 4 (`sprites` 99, `sprites/gv` 20, `title/anim` 73, `title/layers` 6). Fix the **source** dimensions, or accept the RGBA32 fallback. Do not pad. |
| CFG-R5 | **`vSyncCount` and the 30 fps cap disagree.** | `QualitySettings.asset` ships `vSyncCount: 1` on the level Standalone selects. Unity's rule is that a non-zero `vSyncCount` makes `Application.targetFrameRate` ineffective. The shipped cap therefore depends entirely on `Boot.Awake` line 92 running before anything cares. | Not currently a bug (`Boot.Awake` and `UnityPerf.Prep` both force 0), but it is a window in which the project file and the code disagree. Setting the asset to 0 closes it. |
| CFG-R6 | **`macRetinaSupport: 1` is a look decision wearing a perf costume.** | With `defaultIsNativeResolution: 1`, a fixed 1536x1024 stage rasterises at 2x linear, four times the pixels, on any Retina display. Turning it off quarters fill cost and makes the hand-drawn art visibly softer. | Needs the owner's eyes, not a benchmark. Listed as EXPERIMENT in `docs/config-plan.md`, never as DO NOW. |
| CFG-R7 | **LZ4 pays a decompress on the load-critical assets.** | `BuildOptions.CompressWithLz4` reaches `resources.assets` and its `.resS`. It does **not** reach StreamingAssets (317 MB, copied raw), the Managed DLLs (25 MB) or the dylibs (67 MB), so 409 MB of the 682 MB app is outside the flag entirely. | The 269 MB it does reach is already block-compressed texture data, which is high entropy. Expect single digits, in exchange for a decompression step on exactly the sheets that were imported to make loading fast. Measure both sides before taking it. |
| CFG-R8 | **`Resources` cannot be stripped.** | Anything staged into `Assets/Resources` ships whether or not it is ever loaded, and Unity builds a lookup for the whole tree at boot. | Acceptable for the 478 migration files, which are game art that ships anyway. Do not extend the pattern to optional or generated content. |
| CFG-R9 | **`gen_scenes` must stay streamed, permanently.** | `RunwayPaths.GenScenesDir` is written at runtime by `SceneDirector` from LLM image generation. It does not exist at build time and can never be imported. | The `UnityWebRequestTexture` path, `ArtCache.Pump`, the urgent-promotion queue and the absent-path memoisation all have to keep working after the migration. That is why the design is Resources-**first**, stream-second, mirroring `SheetLoop.LoadSheet` rather than replacing it. |
| CFG-R10 | **Basenames collide across art folders.** | `Build.EnsureSheets` keys `Resources/Sheets` by basename alone, and `SheetLoop.LoadSheet` looks up `"Sheets/" + baseName`. That is safe for 26 hand-picked files. It is not safe for 478. | `sprites/chart_1.png` and `sprites/gv/chart_1.png` both reduce to `chart_1`. The migration must keep the folder structure: `Resources/Art/<relative path minus .png>`. |

## CONFIG-3. Verified, not guessed

Everything in this section was read off the shipped build, the repo, or a timed launch.
None of it is inferred from Unity's documentation.

**Scripting backend is Mono.** `Contents/MonoBleedingEdge/` (700 K) and
`Frameworks/libmonobdwgc-2.0.dylib` (6.7 MB) are present; there is no `GameAssembly.dylib`.
Consequence: `stripEngineCode: 1` and `managedStrippingLevel: {}` are both **inert**.
Proof in the build itself: **100 managed DLLs ship, 76 of them `UnityEngine.*Module.dll`**,
including `UnityEngine.PhysicsModule.dll` (172 K) and `UnityEngine.Physics2DModule.dll`
(188 K) for modules that are **not in the manifest**, plus the full BCL
(`mscorlib` 4.4 MB, `System.Xml` 3.0 MB, `System` 2.6 MB, `System.Data` 2.0 MB,
`UnityEngine.UIElementsModule` 2.0 MB).

**Nothing in this project pulls physics.** `manifest.json` and `packages-lock.json` both
resolve to exactly: `newtonsoft-json 3.2.1`, `textmeshpro 5.0.0`, `ugui 2.0.0`, and the
modules `audio`, `imageconversion`, `imgui` (pulled by ugui at depth 1), `ui`,
`unitywebrequest`, `unitywebrequestaudio`, `unitywebrequesttexture`. No script names a
`Collider`, `Rigidbody`, `PhysicsRaycaster` or `Physics2DRaycaster`; the only
`RequireComponent` attributes in the project are `SheetLoop -> RawImage` and
`DrawnParticleView -> CanvasRenderer`, and `Boot`'s `GraphicRaycaster` is UGUI.
`DynamicsManager.asset` and `Physics2DSettings.asset` are inert template leftovers.
PhysX still boots (`SDK Version: 4.1.2`, `Threading Mode: Multi-Threaded`) because on
Standalone with Mono the native backend is compiled into the monolithic `UnityPlayer.dylib`
and registers at engine init regardless of the manifest. **There is no lever here: it is
already removed as far as it can be removed.**

**Shipped texture formats, parsed from `resources.assets`.** Reading
`m_Width, m_Height, m_CompleteImageSize, m_MipsStripped, m_TextureFormat, m_MipCount`
as six little-endian int32 after each name string:

| sheet | dims | bytes | format | bpp |
|---|---|--:|---|--:|
| `birth_loop`, `curtain_loop`, `howto_1..3` | 5120x4608 | 11,796,480 | 10 = DXT1 | 4.00 |
| `birth_intro` | 5120x2880 | 7,372,800 | 10 = DXT1 | 4.00 |
| `roll_01..20` | 4096x2560 | 10,485,760 | 12 = DXT5 | 8.00 |

The arithmetic closes: 5 x 11,796,480 + 7,372,800 + 20 x 10,485,760 = 276,070,400 B =
263.3 MB, against `resources.assets.resS` at 265 MB by `du`. `m_MipCount` is 1 throughout,
confirming `SheetImport`'s `mipmapEnabled = false` is landing.

**App size, `du` by folder.** 682 MB total: StreamingAssets 317 MB,
`resources.assets.resS` 265 MB, Frameworks 67 MB, Managed 25 MB, splash logo 2.7 MB
(`globalgamemanagers.assets.resS`, byte-identical in size to `Library/SplashScreenCache`),
everything else ~5 MB.

**Universal build, slice by slice** (`lipo -detailed_info`):

| binary | x86_64 | arm64 |
|---|--:|--:|
| `UnityPlayer.dylib` | 32,472,848 | 28,714,448 |
| `libmonobdwgc-2.0.dylib` | 3,545,328 | 3,447,120 |
| `libmono-native.dylib` | 830,064 | 813,824 |
| `libMonoPosixHelper.dylib` | 255,808 | 288,448 |
| `MacOS/RUNWAY!` | 50,105 | 50,105 |
| **total** | **37,154,153 B = 35.43 MB** | 33,313,945 B |

An arm64-only build removes exactly **35.43 MB**. `Build.TryUniversalArchitecture` pins
`x64ARM64` today; the enum member for the alternative is `ARM64`.

**Startup, timed line by line** (player launched with `-logFile` on a FIFO, every line
stamped with elapsed wall clock):

| elapsed | line | window |
|--:|---|--:|
| 0.165 | `[UnityMemory] Configuration Parameters` | process alive |
| 0.821 | `Mono path[0] = ...` | **0.66 s** exec, dyld, signature validation |
| 0.875 | `Initializing Metal device caps: Apple M4 Pro` | 0.05 s module registration |
| 1.239 | `Begin MonoManager ReloadAssembly` | **0.36 s** GfxDevice and Metal caps |
| 1.393 | `Loaded All Assemblies, in 0.150 seconds` | **0.15 s** assembly load |
| 3.667 | `UnloadTime: 0.601625 ms` | **2.27 s** splash |
| 3.929 | `RUNWAY! LLM: off ... stage 1536x1024` | **0.26 s** Boot furniture and services |
| 6.849 | `USHOTS shutter` | 2.92 s of `UnityShots`'s own waits |

**The "7 s first paint" is 3.93 s of engine and game plus 2.92 s of harness.** The tail
matches `UnityShots.NewScreensSet`'s three literal
`WaitForSecondsRealtime(0.6 + 1.2 + 0.9)` calls exactly.

**The 2.27 s block is the Unity splash, not a first-scene Awake storm.**
`Assets/Scenes/Main.unity` is 125 lines with **zero GameObjects** and `level0` in the build
is **764 bytes**, so scene load is effectively free; assembly load is separately reported
at 0.150 s. It **cannot be turned off**:
`~/Library/Logs/Unity/Unity.Licensing.Client.log` reports
`"EntitlementGroupId":"6872877128979-UnityPersonal"` and `"ProductName":"Unity Personal"`.

**The art tree is duplicated on disk, not hardlinked.** `Assets/Art` (319 MB) and
`Assets/StreamingAssets/Art` (320 MB) each hold 505 PNGs and 540 `.meta` files, on distinct
inodes, regenerated by `Build.EnsureStreamingArt` on every build. Both are `.gitignore`d
(lines 65 and 66), so this costs editor import time and 491 MB of `Library/Artifacts`, not
repo size. `Assets/Art` is referenced by nothing and does **not** ship.

**Full PNG inventory** (dimensions and colour type read from each IHDR):

| folder | files | on disk | pixels | RGBA32 | dims not /4 | colour |
|---|--:|--:|--:|--:|--:|---|
| `sprites` | 299 | 30.3 MB | 44.7 Mpx | 170.4 MB | 99 | all RGBA |
| `sprites/gv` | 21 | 6.6 MB | 5.7 Mpx | 21.8 MB | 20 | all RGBA |
| `title/anim` | 79 | 13.1 MB | 12.2 Mpx | 46.6 MB | 73 | all RGBA |
| `journal_icons` | 20 | 1.1 MB | 1.3 Mpx | 5.0 MB | 0 | all RGBA |
| `title/layers` | 7 | 2.6 MB | 2.5 Mpx | 9.6 MB | 6 | 6 RGBA, 1 RGB |
| `env` | 4 | 5.7 MB | 5.8 Mpx | 22.0 MB | 0 | all RGB |
| `title/video` | 48 | 68.1 MB | 75.5 Mpx | 288.0 MB | 0 | all RGB |
| `title` (staged) | 7 | 73.8 MB | 134.3 Mpx | 512.2 MB | 0 | all RGB |
| `dice` (staged) | 20 | 58.5 MB | 209.7 Mpx | 800.0 MB | 0 | all RGBA |
| **total** | **505** | **259.7 MB** | **491.7 Mpx** | **1,875.5 MB** | **198** | |

Migration candidates are the 478 rows above the two staged folders: **127.4 MB of PNG,
147.7 Mpx, 563.3 MB as RGBA32**. Compressed by the CFG-R3 rule they land at **104.8 MB**,
which is **-458.5 MB of VRAM (-81%)** and **-22.6 MB of app size**.

**Audio, against what the game opens.** `AudioManager.asset` has
`m_VirtualVoiceCount: 512`, `m_RealVoiceCount: 32`, `m_DSPBufferSize: 1024` (Default,
21.3 ms at 48 kHz), `Default Speaker Mode: 2` (Stereo), `m_SampleRate: 0`. The game opens
`Sfx.Voices = 6` plus `MusicManager`'s tracks and stems, and `RunwayMix` sizes its three
group lists at 8 + 16 + 8, so **32 registered sources is the ceiling by construction**.

**Incremental GC is already on and already working.** `gcIncremental: 1` in
`ProjectSettings.asset`, `gc-max-time-slice=3` in the shipped `boot.config`, and `gc/s`
reads **0.00 on every one of the fourteen probe rows**.

**`metalAPIValidation: 1` is inert in this build.** It applies to development builds and
the editor; `Build.cs` builds with `BuildOptions.None`.

## CONFIG-4. The seam: what this lane needs from the others

- **The art lane owns the source-dimension question.** 198 of 478 files have a dimension
  not divisible by 4. Trimming them by 1 to 3 pixels is the difference between the
  migration set landing at 68.8 MB and at 157.6 MB, and between compressing everything and
  compressing 232 of 430 files. CONFIG cannot decide this: it changes pixels.
- **`ArtCache` needs two one-line-shaped changes before any staging happens.** A
  provenance flag per entry so `Sweep` can branch `Resources.UnloadAsset` against `Destroy`
  (CFG-B2), and a size estimate that stops assuming `width * height * 4` (it would
  over-count compressed entries by 4x to 8x and evict for no reason).
- **`Build.cs` grows one more `Ensure` step**, shaped exactly like `EnsureSheets`: copy the
  migration folders into `Assets/Resources/Art/<folder>/`, keep the same
  same-length-means-same-file guard so 127 MB is not re-copied per build.
- **`SheetImport` grows one branch** for the `Assets/Resources/Art/` prefix, with
  `npotScale = None`, `mipmapEnabled = false`, `alphaIsTransparency = true` (which the
  `Sheets` branch does not set and this content needs), and the RGB/RGBA compression split
  from CFG-R3.
- **The owner owns two calls that CONFIG cannot make**: whether Intel Mac support is in
  scope (worth exactly 35.43 MB), and whether `macRetinaSupport` stays on (the largest
  fill-rate multiplier in the build, and a visible change to how the art reads).
- **One diagnostic run is requested**: `m_LogWhenShaderIsCompiled: 1` for a single energy
  probe pass, to settle whether the 52.29 / 90.37 / 91.40 ms first-visit peaks on rows
  `01 title`, `05 birth (intro)` and `11 garage` are shader variant creation. If they are,
  a `ShaderVariantCollection` warmed inside the 2.27 s splash window is the fix. Revert
  the flag either way.
- **Nothing was applied.** No setting was edited, no build was run, no git command was
  issued. The two writes this lane made are `docs/config-plan.md` and this section.
