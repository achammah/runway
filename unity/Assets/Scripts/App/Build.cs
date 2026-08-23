#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Runway
{
    /// <summary>
    /// THE CLI BUILD. macOS universal via Unity batchmode:
    ///
    ///   "$UNITY" -batchmode -quit -projectPath unity -buildTarget OSXUniversal \
    ///            -executeMethod Runway.Build.BuildMac
    ///
    /// The method does three things before it calls BuildPipeline, because a player
    /// build needs them and an empty project has none of them:
    ///   1. a scene to boot into — the game builds every screen from code, so the
    ///      scene is genuinely empty and exists only to give the runtime something
    ///      to load
    ///   2. the art in StreamingAssets — Assets/Art is 300MB+ of sheets and film
    ///      frames that are streamed off disk, not imported, so the build copies the
    ///      folder rather than the importer swallowing it
    ///   3. a build stamp — the question that cost a whole session
    /// </summary>
    public static class Build
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string StreamingArt = "Assets/StreamingAssets/Art";
        const string SourceArt = "Assets/Art";
        const string StampPath = "Assets/StreamingAssets/build_stamp.txt";
        const string OutDir = "build/mac";
        const string AppName = "RUNWAY!.app";

        [MenuItem("RUNWAY!/Build macOS")]
        public static void BuildMac()
        {
            bool batch = Application.isBatchMode;
            try
            {
                PlayerSettings.companyName = "Assem Studio";
                PlayerSettings.productName = "RUNWAY!";
                TryUniversalArchitecture();

                string scene = EnsureScene();
                EnsureSheets();
                EnsureArtResources();
                EnsureStreamingArt();
                WriteBuildStamp();
                AssetDatabase.Refresh();

                string outPath = Path.Combine(ProjectRoot(), OutDir, AppName);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { scene },
                    locationPathName = outPath,
                    target = BuildTarget.StandaloneOSX,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None,
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                Debug.Log(string.Format("RUNWAY! build {0}: {1} · {2} bytes · {3}",
                    summary.result, outPath, summary.totalSize, summary.totalTime));

                if (batch) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogError("RUNWAY! build threw: " + e);
                if (batch) EditorApplication.Exit(2);
            }
        }

        /// The game constructs every screen in code, so one empty scene is the whole
        /// scene list. Created on first build and then left alone.
        static string EnsureScene()
        {
            if (!File.Exists(Path.Combine(ProjectRoot(), ScenePath)))
            {
                Directory.CreateDirectory(Path.Combine(ProjectRoot(), "Assets/Scenes"));
                Scene s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(s, ScenePath);
                AssetDatabase.Refresh();
                Debug.Log("RUNWAY! created the boot scene at " + ScenePath);
            }
            bool listed = false;
            foreach (EditorBuildSettingsScene e in EditorBuildSettings.scenes)
                if (e.path == ScenePath) listed = true;
            if (!listed)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            return ScenePath;
        }

        /// Assets/Art is streamed, not imported: RunwayPaths probes StreamingAssets
        /// first, so the build copies the folder in. Skipped when it is already there
        /// and newer, because it is 300MB.
        static void EnsureStreamingArt()
        {
            string src = Path.Combine(ProjectRoot(), SourceArt);
            string dst = Path.Combine(ProjectRoot(), StreamingArt);
            if (!Directory.Exists(src))
            {
                Debug.LogWarning("RUNWAY! no " + SourceArt + " to stage — the build ships drawn only.");
                return;
            }
            CopyTree(src, dst);
            Debug.Log("RUNWAY! staged the art into " + StreamingArt);
        }

        static void CopyTree(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            // MIRROR, not accrete: the copy never deleted, so 132 sprites the
            // art lanes had replaced still shipped (9.3MB of ghosts). Anything
            // in the staging copy that no longer exists at the source dies.
            foreach (string stale in Directory.GetFiles(dst))
            {
                string name = Path.GetFileName(stale);
                if (name.StartsWith(".")) continue;
                if (!File.Exists(Path.Combine(src, name))) File.Delete(stale);
            }
            foreach (string staleDir in Directory.GetDirectories(dst))
            {
                string name = Path.GetFileName(staleDir);
                if (!Directory.Exists(Path.Combine(src, name)))
                    Directory.Delete(staleDir, true);
            }
            foreach (string file in Directory.GetFiles(src))
            {
                string name = Path.GetFileName(file);
                if (name.StartsWith(".") || name.EndsWith(".meta")) continue;
                string target = Path.Combine(dst, name);
                if (File.Exists(target)
                    && File.GetLastWriteTimeUtc(target) >= File.GetLastWriteTimeUtc(file)) continue;
                File.Copy(file, target, true);
            }
            foreach (string dir in Directory.GetDirectories(src))
            {
                string name = Path.GetFileName(dir);
                if (name.StartsWith(".")) continue;
                CopyTree(dir, Path.Combine(dst, name));
            }
        }

        /// The film sheets play as IMPORTED textures (GPU-ready — a 16MB PNG cost
        /// seconds of runtime decode through UnityWebRequest). The PNGs are
        /// local-only art, so the build stages them into Resources/Sheets itself;
        /// SheetImport.cs sets the import flags for anything that lands there.
        static readonly string[] TitleSheets =
        {
            "howto_1", "howto_2", "howto_3",
            "birth_intro", "birth_loop", "curtain_loop",
        };

        /// THE TWENTY CUP FILMS GET THE SAME TREATMENT. `dice/roll_NN.png` is
        /// 4096x2560 with alpha and took ~3.4s through UnityWebRequest, which is
        /// long enough for the screen that asked for it to move on and KILL the
        /// load — the exact failure the six title films were pulled out of the
        /// stream for. Imported they arrive in milliseconds. Only one is resident
        /// at a time: DiceRoll.OnDestroy releases the loop, which gives the
        /// texture straight back through Resources.UnloadAsset.
        const int DiceSheets = 20;

        /// AND THE TITLE FILM, 48 FRAMES, IN THE SAME HOME. `SheetLoop.BakedFrame`
        /// looks a sequence frame up by BASENAME ("Sheets/frame_01"), exactly as
        /// `LoadSheet` does for a grid sheet, so Resources/Sheets is where the film
        /// belongs and it is NOT mirrored into Resources/Art as well — one home per
        /// picture, never two. 48 frames of 1536x1024 RGB with no alpha: 288MB of
        /// streamed RGBA32 becomes 36MB of DXT1, the single biggest VRAM win in this
        /// migration, and `StreamSequence`'s three-at-a-time staircase stops running
        /// because every frame is there before the first one is shown.
        const int FilmFrames = 48;

        /// Every basename Resources/Sheets is asked to hold. SheetLoop looks a
        /// sheet up by BASENAME alone ("Sheets/birth_loop", "Sheets/roll_07"), so
        /// two sources that share one are the same sheet as far as it is
        /// concerned. `roll_NN` collides with nothing the title folder ships, and
        /// `frame_NN` collides with neither — which is exactly what FlowShots'
        /// sheet ledger is there to keep true as this list grows.
        public static string[] StagedSheetNames()
        {
            var names = new string[TitleSheets.Length + DiceSheets + FilmFrames];
            int at = 0;
            for (int i = 0; i < TitleSheets.Length; i++) names[at++] = TitleSheets[i];
            for (int i = 1; i <= DiceSheets; i++) names[at++] = string.Format("roll_{0:00}", i);
            for (int i = 1; i <= FilmFrames; i++) names[at++] = string.Format("frame_{0:00}", i);
            return names;
        }

        public static void EnsureSheets()
        {
            string dstDir = Path.Combine(ProjectRoot(), "Assets/Resources/Sheets");
            Directory.CreateDirectory(dstDir);
            string titleDir = Path.Combine(ProjectRoot(), "Assets/Art/title");
            string diceDir = Path.Combine(ProjectRoot(), "Assets/Art/dice");
            string filmDir = Path.Combine(ProjectRoot(), "Assets/Art/title/video");

            bool copied = false;
            foreach (string n in TitleSheets) copied |= Stage(titleDir, dstDir, n);
            for (int i = 1; i <= DiceSheets; i++)
                copied |= Stage(diceDir, dstDir, string.Format("roll_{0:00}", i));
            if (Directory.Exists(filmDir))
                for (int i = 1; i <= FilmFrames; i++)
                    copied |= Stage(filmDir, dstDir, string.Format("frame_{0:00}", i));
            if (copied) AssetDatabase.Refresh();
        }

        /// One sheet into Resources. Same length means the same file: these are
        /// copies of an unchanging source, and re-copying 200MB on every build to
        /// prove it would cost more than it is worth.
        static bool Stage(string srcDir, string dstDir, string name)
        {
            string src = Path.Combine(srcDir, name + ".png");
            string dst = Path.Combine(dstDir, name + ".png");
            if (!File.Exists(src)) { Debug.LogWarning("RUNWAY! no sheet source " + src); return false; }
            if (File.Exists(dst) && new FileInfo(dst).Length == new FileInfo(src).Length) return false;
            File.Copy(src, dst, true);
            return true;
        }

        // ── the imported art mirror ────────────────────────────────────────────

        /// THE SECOND HOME FOR THE STREAMED ART, AND THE REASON IT IS SHAPED LIKE
        /// THIS. Every drawing the game asks `ArtCache` for arrives today through
        /// `UnityWebRequestTexture`: a PNG decode on the MAIN THREAD, RGBA32 in
        /// VRAM at four bytes a pixel, and a place in the one-decode-per-frame
        /// queue. The same file IMPORTED is block compressed (DXT1 at 4 bits a
        /// pixel where there is no alpha, BC7 at 8 where there is), arrives with no
        /// decode at all, and never joins the queue.
        ///
        /// THE FOLDER STRUCTURE IS PRESERVED, unlike Resources/Sheets. `SheetLoop`
        /// looks a sheet up by BASENAME, which is safe for 26 hand-picked names;
        /// `ArtCache` keys on the art-relative path and those paths COLLIDE on
        /// basename — `sprites/chart_1.png` and `sprites/gv/chart_1.png` are two
        /// different pictures. So the mirror is `Assets/Resources/Art/<the same
        /// relative path>` and `ArtCache` looks it up by that whole path.
        ///
        /// ONLY WHAT CAN ACTUALLY BE COMPRESSED IS STAGED. Block compression needs
        /// both dimensions divisible by 4. A source that is not would either import
        /// as RGBA32 — the same bytes it already costs streamed, plus a second copy
        /// in the build — or, with the wrong importer flag, be RESAMPLED to the
        /// nearest power of two: that is what `Assets/Art`'s own settings do today,
        /// and it turns a 378x378 founder frame into 256x256, silently. So the
        /// staging step reads each PNG's IHDR and skips what it cannot compress.
        /// Those files keep streaming exactly as they do now, and the moment the art
        /// lane trims one to a multiple of 4 the next build picks it up with no code
        /// change here.
        ///
        /// `gen_scenes` is not here and never can be: it is written at runtime,
        /// after the build, by the scene director. That is the whole reason
        /// `ArtCache` is Resources-FIRST and stream-second rather than one or the
        /// other.
        const string ArtResourcesRoot = "Assets/Resources/Art";

        /// The folders that mirror, listed one by one and NOT walked recursively:
        /// `dice/` and the six sheets in `title/` are already staged flat into
        /// Resources/Sheets, `title/video` goes there too (see FilmFrames), and
        /// `fonts/`, `music/`, `sfx/` hold no pictures at all.
        static readonly string[] MigrationFolders =
        {
            "sprites", "sprites/gv", "journal_icons", "env", "title/anim", "title/layers",
        };

        public static void EnsureArtResources()
        {
            string artRoot = Path.Combine(ProjectRoot(), SourceArt);
            string dstRoot = Path.Combine(ProjectRoot(), ArtResourcesRoot);
            if (!Directory.Exists(artRoot))
            {
                Debug.LogWarning("RUNWAY! no " + SourceArt + " to mirror — every drawing streams.");
                return;
            }

            var live = new HashSet<string>(StringComparer.Ordinal);
            bool copied = false;
            int staged = 0, unstageable = 0;
            long srcBytes = 0L, gpuBytes = 0L;

            foreach (string folder in MigrationFolders)
            {
                string srcDir = Path.Combine(artRoot, folder.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(srcDir)) continue;
                string dstDir = Path.Combine(dstRoot, folder.Replace('/', Path.DirectorySeparatorChar));
                foreach (string src in Directory.GetFiles(srcDir, "*.png"))
                {
                    string name = Path.GetFileName(src);
                    // the extension is re-checked because a search pattern is not a
                    // promise: `*.png` has been known to hand back `x.png.meta`, and
                    // a .meta staged into Resources is a mess to unpick
                    if (name.StartsWith(".") || !name.EndsWith(".png", StringComparison.Ordinal)) continue;
                    int w, h;
                    bool alpha;
                    if (!PngHeader(src, out w, out h, out alpha)) { unstageable++; continue; }
                    // BLOCK COMPRESSION NEEDS BOTH DIMENSIONS DIVISIBLE BY 4
                    if ((w & 3) != 0 || (h & 3) != 0) { unstageable++; continue; }

                    live.Add(folder + "/" + name);
                    staged++;
                    srcBytes += new FileInfo(src).Length;
                    // BC7 is 8 bits a pixel, DXT1 is 4 — see SheetImport's split
                    gpuBytes += alpha ? (long)w * h : (long)w * h / 2;

                    Directory.CreateDirectory(dstDir);
                    string dst = Path.Combine(dstDir, name);
                    // same length means the same file: these are copies of an
                    // unchanging source and re-copying the tree every build to
                    // prove it would cost more than it is worth
                    if (File.Exists(dst) && new FileInfo(dst).Length == new FileInfo(src).Length) continue;
                    File.Copy(src, dst, true);
                    copied = true;
                }
            }

            copied |= PruneArtResources(dstRoot, live);
            if (copied) AssetDatabase.Refresh();
            Debug.Log("RUNWAY! art mirror: " + staged + " drawings imported into "
                      + ArtResourcesRoot + " (" + Mb(srcBytes) + "MB of PNG, ~" + Mb(gpuBytes)
                      + "MB block-compressed on the GPU) · " + unstageable
                      + " left streaming because a dimension is not divisible by 4");
        }

        /// A STAGED FILE WHOSE SOURCE IS GONE WOULD SHIP FOREVER. Resources is the
        /// one folder Unity cannot strip: whatever is left under it is in the build
        /// whether or not a line of code ever asks for it. `Assets/Art` genuinely
        /// loses files — the founder loops went from 36 frames to 12 — so every
        /// build deletes the mirror entries that no longer have a live, still
        /// compressible source. Scoped to Resources/Art and nothing else.
        static bool PruneArtResources(string dstRoot, HashSet<string> live)
        {
            if (!Directory.Exists(dstRoot)) return false;
            bool changed = false;
            foreach (string file in Directory.GetFiles(dstRoot, "*.png", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(".png", StringComparison.Ordinal)) continue;   // never a .meta
                string rel = file.Substring(dstRoot.Length).Replace('\\', '/').TrimStart('/');
                if (live.Contains(rel)) continue;
                string assetPath = ArtResourcesRoot + "/" + rel;
                if (!AssetDatabase.DeleteAsset(assetPath))
                {
                    try
                    {
                        File.Delete(file);
                        if (File.Exists(file + ".meta")) File.Delete(file + ".meta");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("RUNWAY! stale mirror entry " + assetPath
                                         + " would not delete: " + e.Message);
                        continue;
                    }
                }
                Debug.Log("RUNWAY! art mirror dropped " + assetPath + " — no source behind it");
                changed = true;
            }
            if (changed) PruneEmptyDirs(dstRoot);
            return changed;
        }

        static void PruneEmptyDirs(string dir)
        {
            foreach (string sub in Directory.GetDirectories(dir)) PruneEmptyDirs(sub);
            if (dir.Replace('\\', '/').EndsWith(ArtResourcesRoot)) return;   // keep the root itself
            try
            {
                if (Directory.GetFileSystemEntries(dir).Length > 0) return;
                Directory.Delete(dir);
                if (File.Exists(dir + ".meta")) File.Delete(dir + ".meta");
            }
            catch (Exception) { /* a folder that will not go is not a build failure */ }
        }

        /// PNG's IHDR, READ DIRECTLY. Two callers need it before Unity has an
        /// opinion: the staging step above, which must know the DIMENSIONS to
        /// decide whether a file can be block compressed at all, and
        /// `SheetImport`, which must know whether there is an ALPHA CHANNEL to
        /// choose DXT1 (4 bits a pixel, no alpha) over BC7 (8 bits, alpha). Both
        /// facts are in the first 26 bytes of the file, so neither needs the
        /// importer, an AssetDatabase round trip, or a decode.
        ///
        /// A PALETTE image (colour type 3) is reported as having alpha: it may
        /// carry a tRNS chunk further down the file, and paying 8 bits a pixel for
        /// a picture that turns out to be opaque is a cost, while dropping a
        /// transparency it did have is a defect.
        public static bool PngHeader(string path, out int w, out int h, out bool alpha)
        {
            w = 0;
            h = 0;
            alpha = false;
            try
            {
                using (FileStream fs = File.OpenRead(path))
                {
                    var b = new byte[26];
                    int read = 0;
                    while (read < 26)
                    {
                        int got = fs.Read(b, read, 26 - read);
                        if (got <= 0) return false;
                        read += got;
                    }
                    if (b[0] != 0x89 || b[1] != 'P' || b[2] != 'N' || b[3] != 'G') return false;
                    if (b[12] != 'I' || b[13] != 'H' || b[14] != 'D' || b[15] != 'R') return false;
                    w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                    h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                    int colourType = b[25];   // 0 grey · 2 RGB · 3 palette · 4 grey+A · 6 RGBA
                    alpha = colourType == 3 || colourType == 4 || colourType == 6;
                    return w > 0 && h > 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        static string Mb(long bytes)
        {
            return (bytes / (1024f * 1024f)).ToString("0.0");
        }

        static void WriteBuildStamp()
        {
            Directory.CreateDirectory(Path.Combine(ProjectRoot(), "Assets/StreamingAssets"));
            // date + sha, the same contract as the Godot DMG stamp: the owner
            // reads it off the title corner to know THIS build has the fixes
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " · " + GitSha()
                           + " · unity " + Application.unityVersion;
            File.WriteAllText(Path.Combine(ProjectRoot(), StampPath), stamp);
        }

        static string GitSha()
        {
            try
            {
                var p = new System.Diagnostics.Process();
                p.StartInfo.FileName = "git";
                p.StartInfo.Arguments = "rev-parse --short HEAD";
                p.StartInfo.WorkingDirectory = ProjectRoot();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.Start();
                string sha = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(3000);
                return sha == "" ? "nogit" : sha;
            }
            catch { return "nogit"; }
        }

        /// x64 + Apple silicon. The property moved between Unity versions, so it is set
        /// through reflection: a version that names it differently loses the universal
        /// slice, never the build.
        static void TryUniversalArchitecture()
        {
            try
            {
                Type t = Type.GetType("UnityEditor.OSXStandalone.UserBuildSettings, UnityEditor.OSXStandaloneModule");
                if (t == null) return;
                var prop = t.GetProperty("architecture",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop == null) return;
                Type archType = prop.PropertyType;
                object universal = Enum.Parse(archType, "x64ARM64");
                prop.SetValue(null, universal, null);
                Debug.Log("RUNWAY! macOS architecture: universal (x64 + Apple silicon)");
            }
            catch (Exception e)
            {
                Debug.Log("RUNWAY! could not set a universal architecture (" + e.Message
                          + ") — the Player Settings default stands.");
            }
        }

        static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }
    }
}
#endif
