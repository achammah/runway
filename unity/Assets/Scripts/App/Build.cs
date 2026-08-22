#if UNITY_EDITOR
using System;
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

        /// Every basename Resources/Sheets is asked to hold. SheetLoop looks a
        /// sheet up by BASENAME alone ("Sheets/birth_loop", "Sheets/roll_07"), so
        /// two sources that share one are the same sheet as far as it is
        /// concerned. `roll_NN` collides with nothing the title folder ships.
        public static string[] StagedSheetNames()
        {
            var names = new string[TitleSheets.Length + DiceSheets];
            for (int i = 0; i < TitleSheets.Length; i++) names[i] = TitleSheets[i];
            for (int i = 1; i <= DiceSheets; i++)
                names[TitleSheets.Length + i - 1] = string.Format("roll_{0:00}", i);
            return names;
        }

        static void EnsureSheets()
        {
            string dstDir = Path.Combine(ProjectRoot(), "Assets/Resources/Sheets");
            Directory.CreateDirectory(dstDir);
            string titleDir = Path.Combine(ProjectRoot(), "Assets/Art/title");
            string diceDir = Path.Combine(ProjectRoot(), "Assets/Art/dice");

            bool copied = false;
            foreach (string n in TitleSheets) copied |= Stage(titleDir, dstDir, n);
            for (int i = 1; i <= DiceSheets; i++)
                copied |= Stage(diceDir, dstDir, string.Format("roll_{0:00}", i));
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
