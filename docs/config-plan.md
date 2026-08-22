# CONFIG PLAN, RUNWAY! Unity port

What the project's own settings, packages, build and startup actually cost, measured on
this machine against this build, and which of those costs is worth paying to remove.

Every number below was taken from the repo or from the shipped `.app`. Where a claim is
a Unity behaviour rather than something measured here, it says so and names the check
that would settle it. Nothing in this document has been applied.

* Unity 6000.0.82f1, macOS, Apple M4 Pro, Metal
* Build under test: `unity/build/mac/RUNWAY!.app`, stamp `2026-08-22 20:24 · e8ab078`
* Perf baseline: `unity_perf.md`, table mode, uncapped, keyless

---

## 1. The baseline, measured

### 1.1 Frame cost (from the energy probe, uncapped)

| | value |
|---|---|
| fps range across 14 rows | 280.1 (floor after garage) to 412.3 (floor after title) |
| frame ms range | 2.43 to 3.57 |
| worst peak | 91.40 ms (garage build), 90.37 ms (birth intro) |
| gc/s | 0.00 on every row |
| texture memory | 10.8 MB (bare stage) rising to 180.9 MB (dice mid-roll) |
| rebuild/s | 0 on floors, 12 to 50.7 on live screens |

The shipped cap is 30 fps (`Boot.Awake`). At 3.57 ms worst mean frame against a 33.3 ms
budget the game has roughly **9x CPU headroom**. That single fact decides half the table
below: levers that buy CPU are not worth their risk, levers that buy memory, load time and
app size are.

### 1.2 App size, `du` by folder

`RUNWAY!.app` total **682 MB**.

| path | size | what it is |
|---|--:|---|
| `Contents/Resources/Data/StreamingAssets` | 317 MB | the raw PNG art tree, copied by `Build.EnsureStreamingArt` |
| `Contents/Resources/Data/resources.assets.resS` | 265 MB | the 26 imported sheets (6 title, 20 dice) plus font atlases |
| `Contents/Frameworks` | 67 MB | `UnityPlayer.dylib` 58 MB, `libmonobdwgc-2.0.dylib` 6.7 MB, `libmono-native.dylib` 1.6 MB, `libMonoPosixHelper.dylib` 556 K, all fat |
| `Contents/Resources/Data/Managed` | 25 MB | 100 managed DLLs, 76 of them `UnityEngine.*Module.dll` |
| `Contents/Resources/Data/globalgamemanagers.assets.resS` | 2.7 MB | the Unity splash logo (byte-identical to `Library/SplashScreenCache`) |
| `Contents/MonoBleedingEdge` | 700 K | Mono config tree |
| everything else | ~5 MB | `Resources` 2.0 MB, `resources.assets` 1.5 MB, `sharedassets0` 512 K, signature 204 K, launcher 132 K |

### 1.3 Startup, measured line by line

Launched the shipped player with `-logFile` pointed at a FIFO and stamped every line with
elapsed wall clock (`RUNWAY_USHOTS` harness, `RUNWAY_NO_ART=1`).

| elapsed | log line | window |
|--:|---|--:|
| 0.165 | `[UnityMemory] Configuration Parameters` | process is alive |
| 0.821 | `Mono path[0] = ...` | **0.66 s** exec, dyld, code signature validation |
| 0.875 | `Initializing Metal device caps: Apple M4 Pro` | 0.05 s module registration |
| 1.239 | `Begin MonoManager ReloadAssembly` | **0.36 s** GfxDevice and Metal caps |
| 1.393 | `Loaded All Assemblies, in 0.150 seconds` | **0.15 s** managed assembly load |
| 3.667 | `UnloadTime: 0.601625 ms` | **2.27 s**, see below |
| 3.929 | `RUNWAY! LLM: off ... stage 1536x1024` | **0.26 s** Boot furniture, services, first flow frame |
| 6.849 | `USHOTS shutter` | 2.92 s of the harness's own `WaitForSecondsRealtime(0.6 + 1.2 + 0.9)` |

**The "7 s first paint" is two things stacked, and only the first 3.93 s is the game.**
The remaining 2.92 s is `UnityShots.Run` holding for the title to settle before it takes
`n1_title_menu`, and it matches the three literal waits in that file exactly.

**What is in the 2.27 s block.** Not a first-scene Awake storm: `Assets/Scenes/Main.unity`
is 125 lines with **zero GameObjects**, and `level0` in the build is **764 bytes**. Not
assembly loading either, that is the separately reported 0.150 s. It is the mandatory
Unity splash screen. Three pieces of evidence line up: `m_ShowUnitySplashScreen: 1`, a
2.7 MB splash logo shipping in `globalgamemanagers.assets.resS` that is byte-for-byte the
same size as `Library/SplashScreenCache/08/...`, and a 2.27 s window with nothing else in
it. Unity's documented minimum logo duration is 2 s.

---

## 2. The ranked lever table

Ranked by measured gain per unit of risk.

| # | lever | expected gain | cost / risk | verdict |
|---|---|---|---|---|
| 1 | **Streamed art to imported Resources** (478 files) | VRAM ceiling 563 MB to 105 MB (**-81%**); removes every main-thread PNG decode; app size **-22.6 MB** | 198 of 478 files have a dimension not divisible by 4; the wrong importer flags silently resample them (**measured**: 378x378 became 256x256) | **DO NOW** for the 280 files whose dimensions are already divisible by 4. **EXPERIMENT** for the other 198, gated on a source-dimension pass. |
| 2 | **Drop the film (`title/video`, 48 frames) into Resources as DXT1** | VRAM 288 MB to 36 MB (**-87%**); app size **-32.1 MB**; kills the streaming staircase in `SheetLoop.StreamSequence` | none material: all 48 are 1536x1024 (divisible by 4) and RGB with no alpha | **DO NOW** |
| 3 | **Quality: delete 5 unused levels, turn off MSAA and forced aniso on the one that ships** | removes a 2x multisampled backbuffer and its per-frame resolve at Retina resolution from a game with `cullingMask = 0` | must be re-probed to claim a number; the settings themselves are provably dead weight | **DO NOW** (the settings). **EXPERIMENT** (the gain: re-run the probe.) |
| 4 | **arm64-only instead of universal** | **-35.43 MB** app size, exactly measured; removes the unused x86_64 slice from dyld's page-in inside the measured 0.66 s exec window | drops Intel Mac support entirely | **DO NOW if Intel is out of scope**, otherwise SKIP |
| 5 | **Stop Unity importing `Assets/Art`** (505 unused PNGs, 540 `.meta`) | ~491 MB of `Library/Artifacts` and the import time behind it; zero build-size change (nothing references them, so nothing ships today) | interacts with lever 1: the migration wants these as sources | **DO NOW** |
| 6 | **Use the 2.27 s splash window for warm-up work** | converts dead wall clock into warm TMP atlases, warm cue clips and a warm ArtCache; the 52 to 91 ms first-visit peaks on rows 01, 05 and 11 are candidates | `preloadedAssets` holds the assets for the process lifetime | **DO NOW** (mechanism), **EXPERIMENT** (what goes in it) |
| 7 | **Audio virtual voice count 512 to 64** | 448 unused voice records the mixer walks; the game peaks at 6 SFX voices plus music, and `RunwayMix` caps its three groups at 8+16+8 = 32 | none | **DO NOW** |
| 8 | **Dice sheets DXT5 to DXT1-with-1-bit-alpha** | 209.7 MB to 104.9 MB in `resources.assets.resS`, **-105 MB** app size, half the VRAM per die | only valid if the 20 cup films use alpha as a cutout, not a gradient; a soft edge would band visibly | **EXPERIMENT** (look first, one A/B render) |
| 9 | **LZ4 build compression** (`BuildOptions.CompressWithLz4`) | compressible surface is ~269 MB of already block-compressed texture data; realistically single-digit percent | pays a decompress on every load, on the assets whose load time is the thing being optimised | **EXPERIMENT** (cheap to measure both sides) |
| 10 | **`macRetinaSupport: 0`** | quarters the fill rate: a fixed 1536x1024 stage currently rasterises at 2x linear on a Retina display | the hand-drawn art goes visibly softer; this is an owner look-decision, not an engineering one | **EXPERIMENT**, then ask |
| 11 | **IL2CPP + managed stripping** | the only backend where `stripEngineCode: 1` and `managedStrippingLevel` do anything at all | build minutes instead of seconds; `UnityPerf` reflects into `CanvasUpdateRegistry.m_LayoutRebuildQueue` and `UnityEditor.UnityStats` **by name**, so stripping blinds the perf harness without a `link.xml` | **SKIP.** 9x CPU headroom already. Nothing measurable to buy. |
| 12 | **Remove physics** | already removed from `manifest.json`; the rest cannot leave a Mono Standalone player | none, because there is nothing left to do | **SKIP** (done, see 3.6) |
| 13 | **Turn off the Unity splash** | 2.27 s | **license-blocked**: this is a Unity Personal seat | **SKIP**, superseded by lever 6 |
| 14 | **`metalAPIValidation: 0`** | nothing in the shipped app | applies to development builds and the editor only; the build is `BuildOptions.None` | **SKIP** for the player (inert). Optional for editor Play-mode speed. |
| 15 | **Incremental GC** | already on (`gcIncremental: 1`, `gc-max-time-slice=3` in the shipped `boot.config`) and measured `gc/s = 0.00` on all 14 rows | none | **SKIP** (already correct) |
| 16 | **`accelerometerFrequency: 60`** | inert on desktop | none | **SKIP** (cosmetic) |
| 17 | **DSP buffer 1024 to 512** | halves audio latency from 21.3 ms to 10.7 ms at 48 kHz | more mixer callbacks; `Sfx.LateWindow` is 0.25 s, so 21 ms was never a constraint | **SKIP** (no measured complaint) |

---

## 3. Lever detail

### 3.1 Scripting backend: Mono, and it should stay Mono

Confirmed Mono, not IL2CPP: the app ships `Contents/MonoBleedingEdge/` and
`libmonobdwgc-2.0.dylib`, and has no `GameAssembly.dylib`. `scriptingBackend: {}` in
`ProjectSettings.asset` means "platform default", which for Standalone is Mono2x.

Two settings in the file are **inert under Mono** and should not be read as if they were
working:

* `stripEngineCode: 1` is an IL2CPP-only setting. Proof in the build: 76 `UnityEngine.*Module.dll`
  files ship, including `UnityEngine.PhysicsModule.dll` (172 K) and
  `UnityEngine.Physics2DModule.dll` (188 K), for modules that are not in `manifest.json`.
* `managedStrippingLevel: {}` likewise. Mono ships the full BCL: `mscorlib.dll` 4.4 MB,
  `System.Xml.dll` 3.0 MB, `System.dll` 2.6 MB, `System.Data.dll` 2.0 MB,
  `UnityEngine.UIElementsModule.dll` 2.0 MB, for a game that uses none of them at runtime.

**Why not switch.** The case for IL2CPP is CPU and size. The measured CPU is 2.43 to 3.57 ms
against a 33.3 ms budget, so there is no CPU case. On size, 25 MB of Managed DLLs becomes a
`GameAssembly.dylib` that for a project this size is typically comparable or larger, so
there is no reliable size case either. Against that: build time goes from seconds to minutes,
and `UnityPerf.BindUi` / `UnityPerf.StatInt` resolve internal UGUI fields and an editor type
**by string name**, which managed stripping is specifically designed to remove. The perf
harness would go blind (`rebuild/s` reads `n/a`, which the file itself calls out as its own
failure mode) and the failure would be silent.

**Notarization is not a factor either way.** Both backends produce signed Mach-O binaries.
IL2CPP adds `GameAssembly.dylib`, which is one more thing to sign. Worth noting separately:
`Build.cs` today does **no signing and no notarization at all**, and `Contents/_CodeSignature`
is only the ad-hoc signature Unity applies. That is a shipping gap, not a config lever.

### 3.2 Quality settings: six levels, five of them dead, and the live one is a 3D preset

`QualitySettings.asset` ships Unity's stock six levels. `m_CurrentQuality: 5` and
`m_PerPlatformDefaultQuality: Standalone: 5` both select **Ultra**, which carries:

| Ultra setting | value | relevance to this game |
|---|---|---|
| `antiAliasing` | 2 (2x MSAA) | **the one that costs real money.** Forces a multisampled backbuffer plus a resolve every frame, at native Retina resolution, for a Screen Space Overlay canvas |
| `anisotropicTextures` | 2 (ForcedOn) | per-sampler cost for UI quads that are never minified |
| `vSyncCount` | 1 | contradicts `Boot.Awake`, which sets it to 0 at runtime |
| `shadows` / `shadowCascades` / `shadowDistance` | 2 / 4 / 150 | `Boot` sets `Cam.cullingMask = 0`. There is nothing 3D to shadow |
| `pixelLightCount` | 4 | no lights exist |
| `realtimeReflectionProbes`, `softParticles`, `softVegetation`, `billboardsFaceCameraPosition` | 1 | no probes, no `ParticleSystem` (the effects are UGUI meshes), no vegetation, no billboards |
| `lodBias` 2, `maximumLODLevel` 0, `terrain*` | | no LOD groups, no terrain |

The `vSyncCount` contradiction is worth stating plainly. The asset says 1, `Boot.Awake`
sets 0, and `UnityPerf.Prep` sets 0 again. Unity's rule is that a non-zero `vSyncCount`
makes `Application.targetFrameRate` ineffective, so the shipped 30 fps cap depends
entirely on `Boot` running its line first. Setting the asset to 0 removes a window in
which the project and the code disagree.

Deleting the five unused levels is not a perf lever by itself, it is a correctness one:
it makes it impossible to ship the wrong one.

### 3.3 Startup levers

**The splash cannot be removed.** `~/Library/Logs/Unity/Unity.Licensing.Client.log`
contains `"EntitlementGroupId":"6872877128979-UnityPersonal"` and
`"ProductName":"Unity Personal"`. On a Personal seat `PlayerSettings.SplashScreen.show`
is not available and the Unity logo is mandatory. Any code path that calls
`SplashScreen.Stop` to skip it is a licence-terms question, not an engineering one, and
this document does not propose it.

**So use it.** The splash runs concurrently with the first scene load, and the first scene
is empty, so 2.27 s is currently spent waiting. Three things could move into it:

1. `preloadedAssets` (currently `[]`). Assets listed there load during the player's boot,
   inside the splash window, and stay resident. Candidates: the TMP font assets
   (`PatrickHand SDF`, `Baloo2 SDF`, `RunwayGlyphs 0/1 SDF`), which `DrawnUI.Hand`
   otherwise resolves lazily on first text.
2. A `ShaderVariantCollection` in `m_PreloadedShaders` (currently `[]`). The probe's
   first-visit peaks (52.29 ms on `01 title`, 90.37 ms on `05 birth (intro)`, 91.40 ms on
   `11 garage`) are the right shape for first-use variant creation, but the probe cannot
   attribute them. **The check:** set `m_LogWhenShaderIsCompiled: 1`, run the table once,
   and see whether compiles land on those exact rows. Do that before adding a collection.
3. `Sfx.WarmAll()` (14 cues, 25.7 s of audio, under 10 MB held) and an ArtCache prewarm of
   the title's first frames.

**The 0.66 s exec window.** Half of it is dyld mapping and validating a fat
`UnityPlayer.dylib` whose x86_64 slice (30.97 MB) will never execute on this target.
See 3.5.

### 3.4 Build settings

`Build.cs` builds with `options = BuildOptions.None`. Consequences:

* **Compression is "Default"**, not LZ4. `BuildOptions.CompressWithLz4` would chunk-compress
  `resources.assets`, its `.resS` and `sharedassets0`. It does **not** touch
  `StreamingAssets` (copied raw, 317 MB), the Managed DLLs (25 MB) or the dylibs (67 MB),
  so 409 MB of the 682 MB app is out of reach of this flag entirely. The reachable 269 MB
  is already-block-compressed texture data, which is high entropy. Expect single-digit
  percent for a decompression step on exactly the assets whose load latency was the reason
  they were imported in the first place. Measure before committing.
* **Not a development build**, so `metalAPIValidation: 1` never applies to the player and
  `Profiler.GetTotalAllocatedMemoryLong` is the reduced non-development number. Worth
  knowing when reading the probe's `alloc MB` column.
* **`stripEngineCode` does nothing** (Mono, see 3.1).
* The build is **universal** via `Build.TryUniversalArchitecture`, which reflects
  `UnityEditor.OSXStandalone.UserBuildSettings.architecture` to `x64ARM64`.

### 3.5 Universal vs arm64, exactly measured

`lipo -detailed_info` on every Mach-O in the bundle:

| binary | x86_64 | arm64 |
|---|--:|--:|
| `Frameworks/UnityPlayer.dylib` | 32,472,848 | 28,714,448 |
| `Frameworks/libmonobdwgc-2.0.dylib` | 3,545,328 | 3,447,120 |
| `Frameworks/libmono-native.dylib` | 830,064 | 813,824 |
| `Frameworks/libMonoPosixHelper.dylib` | 255,808 | 288,448 |
| `MacOS/RUNWAY!` | 50,105 | 50,105 |
| **total** | **37,154,153 B = 35.43 MB** | 33,313,945 B |

Dropping to arm64 removes exactly **35.43 MB**. It also removes those bytes from what dyld
maps and what the code-signature check hashes at launch, inside the measured 0.66 s exec
window. This is the whole decision: **is Intel Mac support in scope?** If no, take it. If
yes, this lever does not exist.

### 3.6 Physics: already gone from the manifest, and the rest cannot leave

The brief asks what pulls `com.unity.modules.physics` / `physics2d`. **Nothing does.**

* Neither module is in `Packages/manifest.json`.
* Neither is in `Packages/packages-lock.json`. The full resolved set is
  `newtonsoft-json`, `textmeshpro 5.0.0`, `ugui 2.0.0`, and the modules `audio`,
  `imageconversion`, `imgui` (pulled by ugui at depth 1), `ui`, `unitywebrequest`,
  `unitywebrequestaudio`, `unitywebrequesttexture`.
* No script references a `Collider`, `Rigidbody`, `PhysicsRaycaster` or `Physics2DRaycaster`.
  The only `RequireComponent` attributes are `SheetLoop -> RawImage` and
  `DrawnParticleView -> CanvasRenderer`. `Boot` creates a `GraphicRaycaster`, which is
  UGUI, not physics.
* `ProjectSettings/DynamicsManager.asset` and `ProjectSettings/Physics2DSettings.asset`
  are inert leftovers of the project template.

PhysX still boots:

```
[Physics::Module] Initialized fallback backend.        (0.854 s)
[Physics::Module] Selected backend.                    (1.393 s)
[Physics::Module] Name: PhysX
[Physics::Module] SDK Version: 4.1.2
[Physics::Module] Threading Mode: Multi-Threaded
```

The reason is 3.1: on Standalone with Mono there is no engine-code stripping, so the native
physics backend is compiled into the monolithic `UnityPlayer.dylib` and registers at engine
init regardless of the manifest, and the two module DLLs ship for the same reason. Backend
selection sits inside the 0.15 s assembly-load window and is not separately resolvable.
Stepping an empty world with no bodies costs nothing per frame, and the probe's own header
already records `no twin: this port runs no physics at all`. **There is no lever here.**

### 3.7 Audio

`AudioManager.asset`: `Default Speaker Mode: 2` (Stereo, correct), `m_SampleRate: 0`
(follow the output device, correct), `m_DSPBufferSize: 1024` with
`m_RequestedDSPBufferSize: 0` (Default, 21.3 ms at 48 kHz), `m_VirtualVoiceCount: 512`,
`m_RealVoiceCount: 32`, `m_VirtualizeEffects: 1`, `m_EnableOutputSuspension: 1`.

Against what the game actually opens: `Sfx.Voices = 6`, plus `MusicManager`'s tracks and
stems, and `RunwayMix` sizes its three group lists at 8, 16 and 8, so **32 registered
sources is the ceiling by construction**. 512 virtual voices is 480 records the mixer
maintains for nothing. Dropping to 64 keeps a 2x margin over the hard ceiling.

`m_RealVoiceCount: 32` already equals that ceiling and should stay.

The DSP buffer is worth leaving alone: `Sfx` accepts a cue up to `LateWindow = 0.25 s`
after the ask, so 21.3 ms of output latency has never been the constraint, and halving the
buffer doubles the callback rate for no measured benefit.

### 3.8 Window, display, splash flags

| setting | value | read |
|---|---|---|
| `fullscreenMode` | 1 (FullScreenWindow) | correct. Exclusive fullscreen on macOS costs a mode switch and buys nothing over Metal's flip-model path, which `useFlipModelSwapchain: 1` already selects |
| `resizableWindow` | 0 | correct for a fixed 1536x1024 stage that letterboxes via the camera clear colour |
| `macRetinaSupport` | 1 | see lever 10. This is the largest single fill-rate multiplier in the build and it is a look decision |
| `defaultIsNativeResolution` | 1 | consistent with the above |
| `runInBackground` | 0 | correct, single-player |
| `visibleInBackground` | 1 | harmless |
| `use32BitDisplayBuffer` | 1 | correct, the art has gradients |
| `m_ActiveColorSpace` | 0 (Gamma) | correct for hand-drawn sRGB art with no lighting |
| `allowHDRDisplaySupport` / `useHDRDisplay` | 0 / 0 | correct, matches `hdr-display-enabled=0` in the shipped `boot.config` |
| `m_ShowUnitySplashScreen` | 1 | mandatory, Personal seat |
| `metalAPIValidation` | 1 | development builds and editor only, inert in this build |
| `accelerometerFrequency` | 60 | inert on desktop |
| `m_StackTraceTypes` | all ScriptOnly | correct, the cheap setting |

### 3.9 The duplicated art tree

`Assets/Art` and `Assets/StreamingAssets/Art` each hold **505 PNGs, 540 `.meta` files**,
319 MB and 320 MB, on **distinct inodes** (checked: not hardlinks). `Build.EnsureStreamingArt`
regenerates the second from the first on every build, by design, and `.gitignore` excludes
both, so this is not a repo problem.

It is an **editor** problem. Unity imports both trees, 1,080 asset records, and
`Library/Artifacts` is 491 MB. `Assets/Art` is referenced by nothing (the game streams it
through `RunwayPaths.ArtRoot`, which probes `StreamingAssets/Art` first), so it does not
ship. It costs import time and Library size for zero build output.

**And the import settings it does have are wrong**, which matters the moment lever 1 lands.
Measured from `Library/Artifacts`, not guessed:

| source | imported as | why |
|---|---|---|
| `env_bed.png` 391x391 RGBA | **512x512** DXT5, 10 mips, 349,552 B | `nPOTScale: 1` (ToNearest) rescales to power of two |
| `chr_loop_dropout_03.png` 378x378 RGBA | **256x256** DXT5, 9 mips, 87,408 B | same, and it rounded **down**: a 32% linear downscale of a founder animation frame |
| `chr_founder_grab_01.png` 347x347 RGBA | **256x256** DXT5, 9 mips, 87,408 B | same |

10.67 bits per pixel instead of 8 confirms the mip chain (`enableMipMap: 1`) is costing an
extra 33%. This is the exact failure mode the migration has to avoid, and it is already
happening today in the folder nobody looks at.

---

## 4. The streamed art migration plan

### 4.1 What is actually streamed, and what it costs

`ArtCache` loads every path through `SheetLoop.LoadTexture`, which is
`UnityWebRequestTexture.GetTexture(url, true)` against a `file://` URI. That decodes PNG
**on the main thread** in `DownloadHandlerTexture`, and produces **RGBA32** with no
compression and no mips. `ArtCache.Pump` paces it at one decode per frame precisely because
those decodes cluster (the file's own comment records an 889 ms frame on the draft before
the pump existed).

Full inventory of `unity/Assets/Art`, measured (width, height and colour type read from
each PNG's IHDR):

| folder | files | PNG on disk | pixels | RGBA32 | dims not /4 | colour |
|---|--:|--:|--:|--:|--:|---|
| `sprites` | 299 | 30.3 MB | 44.7 Mpx | **170.4 MB** | 99 | all RGBA |
| `sprites/gv` | 21 | 6.6 MB | 5.7 Mpx | **21.8 MB** | 20 | all RGBA |
| `title/anim` | 79 | 13.1 MB | 12.2 Mpx | **46.6 MB** | 73 | all RGBA |
| `journal_icons` | 20 | 1.1 MB | 1.3 Mpx | **5.0 MB** | 0 | all RGBA |
| `title/layers` | 7 | 2.6 MB | 2.5 Mpx | **9.6 MB** | 6 | 6 RGBA, 1 RGB |
| `env` | 4 | 5.7 MB | 5.8 Mpx | **22.0 MB** | 0 | all RGB |
| **migration set** | **430** | **59.3 MB** | **72.2 Mpx** | **275.3 MB** | **198** | |
| `title/video` (the film) | 48 | 68.1 MB | 75.5 Mpx | **288.0 MB** | 0 | all RGB |
| **total to migrate** | **478** | **127.4 MB** | **147.7 Mpx** | **563.3 MB** | **198** | |

For reference, what stays where it is:

| folder | files | why it does not move |
|---|--:|---|
| `title` (6 sheets) | 7 | already staged into `Resources/Sheets` by `Build.EnsureSheets` |
| `dice` (20 cup films) | 20 | same |
| `gen_scenes` (user content) | n/a | **must stay streamed**, see 4.5 |

### 4.2 The format the sheets actually ship as, and why "BC7 everywhere" is wrong

The brief and `SheetImport.cs` both say BC7. **They are wrong, and the correct answer is
better than BC7.** Read directly out of `resources.assets` by parsing the `Texture2D`
headers:

| sheet | dims | `m_CompleteImageSize` | `m_TextureFormat` | bpp |
|---|---|--:|---|--:|
| `birth_loop` | 5120x4608 | 11,796,480 | **10 = DXT1 (BC1)** | 4.00 |
| `curtain_loop` | 5120x4608 | 11,796,480 | **10 = DXT1 (BC1)** | 4.00 |
| `howto_1` | 5120x4608 | 11,796,480 | **10 = DXT1 (BC1)** | 4.00 |
| `roll_01` | 4096x2560 | 10,485,760 | **12 = DXT5 (BC3)** | 8.00 |

The arithmetic closes exactly: 5 sheets at 11,796,480 + `birth_intro` at 7,372,800 + 20 dice
at 10,485,760 = 276,070,400 B = 263.3 MB, which is `resources.assets.resS` at 265 MB by `du`.

This happens because `SheetImport.OnPreprocessTexture` sets
`TextureImporterCompression.Compressed` (normal quality), and on Standalone normal quality
picks **DXT1 for source with no alpha, DXT5 for source with alpha**. The six title sheets
are RGB with no alpha channel, so they get DXT1 at **4 bpp**. BC7 is 8 bpp.

**Therefore forcing BC7 on this content would double it, not shrink it.** The rule the
migration must follow instead:

* **RGB source, no alpha: `TextureImporterCompression.Compressed` (yields DXT1, 4 bpp).**
  Applies to `env` and all 48 film frames.
* **RGBA source: `TextureImporterCompression.CompressedHQ` (yields BC7, 8 bpp).** Same size
  as the DXT5 it replaces, strictly better quality on the gradients and soft edges that
  hand-drawn art is full of. Applies to `sprites`, `sprites/gv`, `journal_icons`,
  `title/anim`, `title/layers`.

BC7 is free here only where the content already needs 8 bpp. It is never a size win.

### 4.3 The blocker: 198 of 478 files cannot be block-compressed as they are

Block compression needs both dimensions divisible by 4. The measured split:

| set | files with both dims /4 | files without |
|---|--:|--:|
| `sprites` | 200 | **99** (14.7 Mpx) |
| `sprites/gv` | 1 | **20** (5.4 Mpx) |
| `title/anim` | 6 | **73** (11.2 Mpx) |
| `title/layers` | 1 | **6** (0.9 Mpx) |
| `journal_icons`, `env`, `title/video` | 72 | 0 |
| **total** | **280** | **198** (32.2 Mpx) |

Three ways out, and only one of them is good:

1. **`npotScale = ToNearest`.** Unity rescales to power of two so everything compresses.
   **Rejected, and it is not a theory**: this is what `Assets/Art` does today, and it turned
   `chr_loop_dropout_03.png` from 378x378 into **256x256**. A 32% linear downscale of a
   founder's animation frame is a visible quality loss, and it would be silent.
2. **`npotScale = None`, accept the fallback.** Unity leaves non-/4 textures uncompressed
   (RGBA32). Cost: the 198 files land at **123.0 MB** instead of 32.2 MB, so the whole
   migration set imports at **157.6 MB instead of 68.8 MB**. Still a large win over 275.3 MB
   of RGBA32 plus the decode, but it leaves most of the prize on the table.
3. **Fix the source dimensions to multiples of 4.** A one-time offline pass over 198 files,
   trimming or extending by 1 to 3 pixels. Then everything compresses, the migration set
   lands at **68.8 MB**, and nothing is resampled.

**Option 3 is the plan; option 2 is the fallback for anything the art lane will not touch.**

**Do not pad at import time.** `GameUi.Fit` computes the display rect from
`tex.width / tex.height` and stretches the whole texture across it, with no `uvRect`. A
padded texture would therefore draw its padding and get a slightly wrong aspect. Changing
`Fit` to carry a real-size plus `uvRect` is a code change owned by the art lane, not a
config lever. Fixing the source is cheaper and has no runtime cost.

### 4.4 Expected deltas

Assuming option 3 (source dimensions fixed) and the format rule from 4.2:

**VRAM, if the whole set were resident at once:**

| | today (RGBA32) | after | delta |
|---|--:|--:|--:|
| migration set (430 files) | 275.3 MB | 68.8 MB | **-206.5 MB** |
| film (48 frames) | 288.0 MB | 36.0 MB (DXT1) | **-252.0 MB** |
| **total** | **563.3 MB** | **104.8 MB** | **-458.5 MB (-81%)** |

In practice `ArtCache.Sweep` caps at 280 MB and the measured peak `tex MB` is 180.9 MB on
the dice row. The same working set under BC lands between a quarter and an eighth of that,
which means **`ArtCache.Sweep` would effectively stop firing** and the eviction-and-reload
churn goes away with it.

**App size:**

| | delta |
|---|--:|
| PNG leaves `StreamingAssets` | **-127.4 MB** |
| BC enters `resources.assets.resS` | **+104.8 MB** |
| **net** | **-22.6 MB** |

Split by set, because they pull in opposite directions: the film is **-32.1 MB** (68.1 MB
of PNG becomes 36.0 MB of DXT1), and the migration set is **+9.5 MB** (59.3 MB of PNG
becomes 68.8 MB of BC7, because PNG's lossless entropy coding beats a fixed 8 bpp on
line art with large flat regions). The film alone is the size win.

**Load time.** No measurement of the streamed sprite path exists yet, but the project has
already run this experiment on the sheets and recorded the result in two places:
`SheetImport.cs` says a 16 MB sheet cost **2.8 to 6.6 s** of runtime decode through
`UnityWebRequest` and arrives "in milliseconds" imported; `Build.cs` says `dice/roll_NN.png`
took **~3.4 s** streamed, long enough for the screen that asked to move on and kill the
load. The migration set is 478 smaller files rather than 26 big ones, so the per-file cost
is smaller and the queue is longer: 430 sprites at one decode per frame is 430 frames, or
**14.3 s at the shipped 30 fps**, to drain a full cold queue. Imported, there is no queue,
no `UnityWebRequest`, no main-thread decode, and no pump.

**What the pump becomes.** `ArtCache.Pump` and `PumpBlocking` do not disappear, because
`gen_scenes` still needs them. They stop being the path the shipped art takes.

### 4.5 What must stay streamed

**`gen_scenes` user content, without exception.** `RunwayPaths.GenScenesDir` is
`Application.persistentDataPath/gen_scenes`, written at runtime by `SceneDirector` from LLM
image generation. It cannot be imported because it does not exist at build time. The
`UnityWebRequestTexture` path, the decode pump, the urgent-promotion queue and the
absent-path memoisation all have to keep working for it.

This is the structural reason the migration is **Resources-first, stream-second**, exactly
mirroring `SheetLoop.LoadSheet`: try the baked asset, and if there is not one, fall through
to the existing streamed path unchanged.

### 4.6 Design: `ArtCache`'s Resources-first path

Mirror `SheetLoop.LoadSheet` lines 141 to 163. The shape, not the code:

* **Key.** `SheetLoop` looks a sheet up by basename alone (`Sheets/birth_loop`).
  `ArtCache` keys on the art-relative path (`sprites/gv/chart_1.png`,
  `journal_icons/cash.png`), and those paths **collide on basename**
  (`sprites/chart_1.png` vs `sprites/gv/chart_1.png` both end in `chart_1`). So the
  Resources mirror must preserve the folder structure:
  `Assets/Resources/Art/<relative path without .png>`, looked up as
  `Resources.Load<Texture2D>("Art/" + relative.Substring(0, relative.Length - 4))`.
  Do **not** copy `Build.EnsureSheets`'s flat basename scheme here.
* **Where it goes in `Load`.** After the `_tex` cache hit and before
  `RunwayPaths.ArtUrl(relative)`. A hit caches into `_tex` and calls back **on the same
  frame**, which is strictly better than today and needs no queue.
* **A miss falls through unchanged.** `gen_scenes`, and anything an art pass has not staged
  yet, take exactly the path they take now.
* **The staging step.** `Build.EnsureSheets` already does this for 26 files by copying and
  letting a postprocessor set the flags. The same pattern extends: a second `Ensure` copies
  the migration folders into `Assets/Resources/Art/<folder>/`, and `SheetImport` grows a
  branch for that prefix.

**The postprocessor branch for `Assets/Resources/Art/`:**

```
imp.textureType        = TextureImporterType.Default;
imp.maxTextureSize     = 2048;      // largest migrated source is 1920x1449
imp.mipmapEnabled      = false;     // UI quads, never minified; mips cost +33%
imp.npotScale          = TextureImporterNPOTScale.None;   // NEVER ToNearest
imp.isReadable         = false;
imp.sRGBTexture        = true;
imp.wrapMode           = TextureWrapMode.Clamp;
imp.filterMode         = FilterMode.Bilinear;
imp.alphaIsTransparency = true;     // stops halos on the bilinear edges of cutout art
imp.textureCompression = <alpha ? CompressedHQ : Compressed>;   // BC7 : DXT1, see 4.2
```

`alphaIsTransparency` is the one flag `SheetImport` does not currently set and this content
does need: the sheets are opaque RGB, the sprites are cutouts.

### 4.7 The risk list

| # | risk | why it bites | what removes it |
|---|---|---|---|
| R1 | **`Resources.UnloadAsset` vs `Destroy`** | `ArtCache.Sweep` calls `UnityEngine.Object.Destroy(t)` on everything it evicts. Destroying an asset loaded from Resources destroys the **shared instance**; the next `Resources.Load` of the same path can return a destroyed object, and in the editor it can damage the imported asset | `ArtCache` must record provenance per entry (baked vs streamed), exactly as `SheetLoop._sheetBaked` does, and branch: `Resources.UnloadAsset` for baked, `Destroy` for streamed. **This is the single highest-consequence item in the migration.** |
| R2 | **The sweep budget stops meaning what it meant** | `Sweep` estimates held bytes as `width * height * 4`. Under BC that is 4x to 8x too high, so it would evict aggressively for no reason | the size estimate has to read `GraphicsFormatUtility.GetBlockSize` / the actual format, or simply skip baked entries (they are already compressed and unloadable at zero cost) |
| R3 | **NPOT resample, silently** | measured today on `Assets/Art`: 378x378 became 256x256 | `npotScale = None` in the postprocessor, plus the source-dimension pass. Verify by asserting `tex.width == 378` in a probe after the migration |
| R4 | **Basename collisions** | `sprites/chart_1.png` and `sprites/gv/chart_1.png` | keep the folder structure under `Resources/Art/`, see 4.6 |
| R5 | **Cover-crop math** | `SheetLoop.CoverRect` divides by `texW`/`texH`. It is **unchanged** by this migration: the six sheets and 20 dice it applies to are already imported, already /4-clean, and are not being re-imported. The film's `Apply` path in sequence mode uses the full texture, so its `uvRect` is `(0,0,1,1)` regardless of format | nothing to do, but do not "helpfully" pad the film frames either |
| R6 | **`GameUi.Fit` aspect drift** | `Fit` sizes the rect from `tex.width / tex.height` with no `uvRect`, so any padding shows and the aspect shifts | do not pad. Fix source dimensions instead (4.3) |
| R7 | **Everything in Resources loads at startup? No, but the header does** | Unity builds a lookup for the whole `Resources` tree at boot. 478 more entries is small, but `Resources` is also the folder Unity cannot strip: anything staged there ships whether or not it is ever asked for | acceptable here because the whole set is game art that ships anyway. Do **not** extend the pattern to optional content |
| R8 | **`ArtCache.PumpBlocking`'s disk route** | it calls `tex.LoadImage`, which is the streamed path by definition. A Resources-first `Load` short-circuits before the queue, so baked paths never reach `PumpBlocking` | check that the editor harnesses (`FlowShots`, `DraftSelectProbe`) still see what they expect. `FlowShots` line 243 explicitly comments that it wants **the bytes off disk, not the imported asset** |
| R9 | **`ArtCache.Known` / absent-path memoisation** | `Load` caches `null` for a path with nothing behind it, permanently. A Resources hit must be checked **before** that decision, or a staged asset whose streamed copy was removed reads as absent forever | order the checks: `_tex` hit, then Resources, then `ArtUrl`, then the absent-path memo |
| R10 | **Build time and the copy** | `Build.EnsureSheets` skips a copy when source and destination lengths match. 478 more files makes that loop longer but it is still a stat per file | reuse the same guard; do not re-copy 127 MB per build |

### 4.8 Suggested order

1. **Film only** (48 files, `title/video`). Zero /4 risk, zero alpha risk, biggest single
   size win (-32.1 MB) and biggest single VRAM win (-252 MB). Proves R1, R2, R4 and R9 on
   a set small enough to eyeball. Also deletes the `StreamSequence` staircase.
2. **`journal_icons` + `env`** (24 files, all /4-clean). Proves the RGB/RGBA format split.
3. **The 232 /4-clean files** in `sprites`, `sprites/gv`, `title/anim`, `title/layers`.
4. **The 198 non-/4 files**, after the source-dimension pass. Or leave them streamed, which
   is a perfectly good permanent answer for a long tail.

Re-run the energy probe after step 1 and after step 3. The columns that should move are
`tex MB` (down hard) and the `05`/`09` peak `ms` (down); `fps` and `frame ms` should barely
move, because there was never a CPU problem.

---

## 5. Verbatim settings diff, the DO NOW set

Six edits. Nothing here has been applied.

### 5.1 `unity/ProjectSettings/QualitySettings.asset`

Replace the whole file. Six stock levels become one, named for what it is.

FIND (the entire file from line 7 to the end, i.e. `m_CurrentQuality:` through
`  tvOS: 2`)

REPLACE WITH:

```yaml
  m_CurrentQuality: 0
  m_QualitySettings:
  - serializedVersion: 4
    name: RUNWAY!
    pixelLightCount: 0
    shadows: 0
    shadowResolution: 0
    shadowProjection: 1
    shadowCascades: 1
    shadowDistance: 0
    shadowNearPlaneOffset: 3
    shadowCascade2Split: 0.33333334
    shadowCascade4Split: {x: 0.06666667, y: 0.2, z: 0.46666667}
    shadowmaskMode: 0
    skinWeights: 1
    globalTextureMipmapLimit: 0
    textureMipmapLimitSettings: []
    anisotropicTextures: 0
    antiAliasing: 0
    softParticles: 0
    softVegetation: 0
    realtimeReflectionProbes: 0
    billboardsFaceCameraPosition: 0
    useLegacyDetailDistribution: 0
    adaptiveVsync: 0
    vSyncCount: 0
    realtimeGICPUUsage: 25
    adaptiveVsyncExtraA: 0
    adaptiveVsyncExtraB: 0
    lodBias: 1
    maximumLODLevel: 0
    enableLODCrossFade: 0
    streamingMipmapsActive: 0
    streamingMipmapsAddAllCameras: 1
    streamingMipmapsMemoryBudget: 512
    streamingMipmapsRenderersPerFrame: 512
    streamingMipmapsMaxLevelReduction: 2
    streamingMipmapsMaxFileIORequests: 1024
    particleRaycastBudget: 0
    asyncUploadTimeSlice: 2
    asyncUploadBufferSize: 16
    asyncUploadPersistentBuffer: 1
    resolutionScalingFixedDPIFactor: 1
    customRenderPipeline: {fileID: 0}
    terrainQualityOverrides: 0
    terrainPixelError: 1
    terrainDetailDensityScale: 1
    terrainBasemapDistance: 1000
    terrainDetailDistance: 80
    terrainTreeDistance: 5000
    terrainBillboardStart: 50
    terrainFadeLength: 5
    terrainMaxTrees: 50
    excludedTargetPlatforms: []
  m_TextureMipmapLimitGroupNames: []
  m_PerPlatformDefaultQuality:
    Android: 0
    EmbeddedLinux: 0
    GameCoreScarlett: 0
    GameCoreXboxOne: 0
    Kepler: 0
    LinuxHeadlessSimulation: 0
    Nintendo Switch: 0
    Nintendo Switch 2: 0
    PS4: 0
    PS5: 0
    QNX: 0
    Server: 0
    Standalone: 0
    VisionOS: 0
    WebGL: 0
    Windows Store Apps: 0
    XboxOne: 0
    iPhone: 0
    tvOS: 0
```

What changed against the shipped Ultra level, and why:

| field | was | now | why |
|---|--:|--:|---|
| `antiAliasing` | 2 | **0** | no 3D geometry, `cullingMask = 0`. Removes a multisampled backbuffer and a per-frame resolve at Retina resolution |
| `anisotropicTextures` | 2 (ForcedOn) | **0** (Disable) | UI quads are never minified |
| `vSyncCount` | 1 | **0** | matches what `Boot.Awake` and `UnityPerf.Prep` already force, and un-breaks `Application.targetFrameRate` |
| `shadows` / `shadowCascades` / `shadowDistance` | 2 / 4 / 150 | **0 / 1 / 0** | nothing to shadow |
| `pixelLightCount` | 4 | **0** | no lights |
| `realtimeReflectionProbes`, `softParticles`, `softVegetation`, `billboardsFaceCameraPosition` | 1 | **0** | none of these exist in the scene |
| `particleRaycastBudget` | 4096 | **0** | no `ParticleSystem`; the ink effects are UGUI meshes |
| `lodBias` / `enableLODCrossFade` | 2 / 1 | **1 / 0** | no LOD groups |
| `skinWeights` | 255 | **1** | no skinned meshes |
| `m_CurrentQuality` and every platform default | 5 | **0** | one level, so the wrong one cannot be selected |

**Verify after applying:** re-run the energy probe table and compare `frame ms` and `fps`.
If nothing moves, MSAA was already being folded away by the Metal driver and the edit is
correctness-only, which is still worth keeping.

### 5.2 `unity/ProjectSettings/AudioManager.asset`

FIND:
```yaml
  m_VirtualVoiceCount: 512
  m_RealVoiceCount: 32
```

REPLACE WITH:
```yaml
  m_VirtualVoiceCount: 64
  m_RealVoiceCount: 32
```

`Sfx.Voices = 6`; `RunwayMix` sizes its three groups at 8 + 16 + 8 = 32 registered sources
maximum. 64 keeps a 2x margin. `m_RealVoiceCount` stays at 32 because that already equals
the ceiling.

### 5.3 `unity/ProjectSettings/ProjectSettings.asset`

FIND:
```yaml
  accelerometerFrequency: 60
```

REPLACE WITH:
```yaml
  accelerometerFrequency: 0
```

Inert on desktop. Zero-risk tidy so the file stops implying a sensor poll that does not exist.

### 5.4 `unity/Assets/Scripts/App/Build.cs`, arm64 only

**Apply only if Intel Mac support is out of scope.** Worth 35.43 MB exactly.

FIND:
```csharp
                object universal = Enum.Parse(archType, "x64ARM64");
                prop.SetValue(null, universal, null);
                Debug.Log("RUNWAY! macOS architecture: universal (x64 + Apple silicon)");
```

REPLACE WITH:
```csharp
                object arm = Enum.Parse(archType, "ARM64");
                prop.SetValue(null, arm, null);
                Debug.Log("RUNWAY! macOS architecture: ARM64 (Apple silicon only)");
```

The `try/catch` and the `Type.GetType` reflection around it are unchanged, so a Unity
version that renames the enum still loses only the architecture pin, never the build.

**Verify after applying:** `lipo -info "unity/build/mac/RUNWAY!.app/Contents/Frameworks/UnityPlayer.dylib"`
should report `arm64` alone, and `du -sh` on the `.app` should drop by about 35 MB.

### 5.5 Stop Unity importing `Assets/Art`

`Assets/Art` is 505 PNGs and 540 `.meta` files that nothing references, costing import time
and ~491 MB of `Library/Artifacts`. The staging in `Build.EnsureSheets` and
`Build.EnsureStreamingArt` reads it with `System.IO`, not `AssetDatabase`, so the importer
is not needed for either.

Cleanest fix that does not move files: rename the folder so Unity skips it. Unity ignores
any directory whose name ends in `~`.

* `unity/Assets/Art` becomes `unity/Assets/Art~`
* `unity/Assets/Art.meta` is deleted
* `Build.cs` constant `SourceArt` becomes `"Assets/Art~"`
* `Build.cs` `EnsureSheets` paths `"Assets/Art/title"` and `"Assets/Art/dice"` become
  `"Assets/Art~/title"` and `"Assets/Art~/dice"`
* `RunwayPaths.ArtRoot`'s editor fallback candidates
  `Path.Combine(Application.dataPath, "Art")` and `"../Assets/Art"` become the `Art~` forms
* `.gitignore` line 65 `unity/Assets/Art/` becomes `unity/Assets/Art~/`

**This is a multi-file coordinated edit and it touches the art lane's read path. Do not
apply it in isolation.** It is listed here because it is a config decision, not because
CONFIG should be the lane that makes it.

**Alternative that is CONFIG-only:** leave the tree where it is and accept the editor cost.
The build output is identical either way.

### 5.6 `unity/ProjectSettings/GraphicsSettings.asset`, one run only, then revert

Diagnostic, not a shipped setting.

FIND:
```yaml
  m_LogWhenShaderIsCompiled: 0
```

REPLACE WITH:
```yaml
  m_LogWhenShaderIsCompiled: 1
```

Run the energy probe table once. If shader compiles land on rows `01 title` (52.29 ms peak),
`05 birth (intro)` (90.37 ms) and `11 garage` (91.40 ms), then a `ShaderVariantCollection`
in `m_PreloadedShaders`, warmed inside the 2.27 s splash window, is the fix and lever 6
graduates from EXPERIMENT to DO NOW. If they do not, the peaks are UGUI mesh construction
and belong to another lane. **Revert to 0 either way**: this logs on every compile in the
editor and it is noisy.

---

## 6. How to reproduce every number here

| claim | command |
|---|---|
| app size by folder | `du -sh "unity/build/mac/RUNWAY!.app/Contents"/*` and `.../Contents/Resources/Data/*` |
| Mono, not IL2CPP | presence of `Contents/MonoBleedingEdge/` and `libmonobdwgc-2.0.dylib`, absence of `GameAssembly.dylib` |
| fat slice sizes | `lipo -detailed_info` on each Mach-O in `Contents/Frameworks` and `Contents/MacOS` |
| managed DLL inventory | `ls .../Data/Managed \| wc -l` (100), `\| grep -c '^UnityEngine\.'` (76) |
| shipped texture formats | parse `m_Width, m_Height, m_CompleteImageSize, m_MipsStripped, m_TextureFormat, m_MipCount` as six little-endian int32 after each name string in `.../Data/resources.assets`. Format 10 is DXT1, 12 is DXT5, 25 is BC7 |
| PNG inventory and colour type | read bytes 16 to 26 of each IHDR under `unity/Assets/Art` |
| NPOT resample, measured | same six-int32 parse over `unity/Library/Artifacts`, matched by asset name |
| startup timeline | `mkfifo` a pipe, run the player with `-logFile <pipe>`, read the pipe and stamp each line with elapsed wall clock |
| licence tier | `grep -o 'EntitlementGroupId":"[^"]*"' ~/Library/Logs/Unity/Unity.Licensing.Client.log` |
| perf baseline | the energy probe: `RUNWAY_UPERF=<dir> ./RUNWAY!`, writes `<dir>/unity_perf.md` |

---

## 7. Open questions for the owner

1. **Is Intel Mac support in scope?** It is worth exactly 35.43 MB and some launch time.
   Nothing else about the build changes.
2. **Is `macRetinaSupport: 1` a requirement or a default nobody chose?** It is the largest
   fill-rate multiplier in the build, and turning it off is a visible change to how the
   hand-drawn art reads. This needs eyes, not a benchmark.
3. **Will the art lane accept a source-dimension pass** trimming 198 files to multiples of
   4? It is the difference between 68.8 MB and 157.6 MB for the migration set, and between
   compressing everything and compressing 232 of 430 files.
4. **Do the 20 dice cup films use alpha as a hard cutout or a soft gradient?** A cutout
   means DXT1-with-1-bit-alpha and **-105 MB** of app size. A gradient means DXT5 stays.
