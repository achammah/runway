using System;
using System.IO;
using UnityEngine;

namespace Runway.App
{
    /// <summary>
    /// WHERE EVERYTHING LIVES. The Godot original addresses art as res:// and player
    /// data as user://; Unity has neither, so every path in the port goes through here.
    ///
    /// ART is resolved by probing roots in order, so the SAME code path works in the
    /// editor (where Assets/Art is on disk) and in a player build (where Build.cs has
    /// copied Assets/Art into StreamingAssets). Nothing is imported as a Sprite: the
    /// sheets are 5120x4608 and the title film is 48 full-screen frames, and the Godot
    /// build streams them off disk for exactly that reason.
    ///
    /// USER DATA sits in the same folder the Godot build uses (~/Library/Application
    /// Support/Runway) so ONE api key serves both builds — but every file this port
    /// WRITES that the Godot build also writes carries a .unity suffix, so a
    /// side-by-side never overwrites the other build's saved companies.
    /// </summary>
    public static class RunwayPaths
    {
        /// The Godot viewport, kept exactly: every screen coordinate in this port is a
        /// coordinate in the original.
        public const float StageWidth = 1536f;
        public const float StageHeight = 1024f;

        static string _userDir;
        static string _artRoot;
        static bool _artProbed;

        /// ~/Library/Application Support/Runway on macOS; persistentDataPath elsewhere.
        public static string UserDir
        {
            get
            {
                if (!string.IsNullOrEmpty(_userDir)) return _userDir;
                string home = Environment.GetEnvironmentVariable("HOME");
                if (string.IsNullOrEmpty(home))
                {
                    try { home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); }
                    catch (Exception) { home = null; }
                }
                if (Application.platform == RuntimePlatform.OSXPlayer
                    || Application.platform == RuntimePlatform.OSXEditor)
                {
                    if (!string.IsNullOrEmpty(home))
                        _userDir = Path.Combine(home, "Library/Application Support/Runway");
                }
                if (string.IsNullOrEmpty(_userDir)) _userDir = Application.persistentDataPath;
                TryCreateDir(_userDir);
                return _userDir;
            }
        }

        /// A file in the user folder (keys.env, seen_howto_v2.unity, run_slot_1.unity.json).
        public static string User(string fileName)
        {
            return Path.Combine(UserDir, fileName);
        }

        /// Generated scenes cache — Application.persistentDataPath/gen_scenes.
        public static string GenScenesDir
        {
            get
            {
                string d = Path.Combine(Application.persistentDataPath, "gen_scenes");
                TryCreateDir(d);
                return d;
            }
        }

        /// The folder that holds dice/, title/, sprites/, sfx/, music/, fonts/, env/.
        /// Empty string when no root exists (art simply degrades, exactly as in Godot).
        public static string ArtRoot
        {
            get
            {
                if (_artProbed) return _artRoot;
                _artProbed = true;
                string[] candidates =
                {
                    Path.Combine(Application.streamingAssetsPath, "Art"),
                    Path.Combine(Application.dataPath, "Art"),
                    Path.Combine(Application.dataPath, "../Assets/Art"),
                };
                foreach (string c in candidates)
                {
                    try
                    {
                        if (Directory.Exists(c)) { _artRoot = c; return _artRoot; }
                    }
                    catch (Exception) { /* an unreadable candidate is simply not the root */ }
                }
                _artRoot = "";
                Debug.LogWarning("RUNWAY! no art root found — the drawn fallbacks carry every screen. "
                                 + "Looked in: " + string.Join(" · ", candidates));
                return _artRoot;
            }
        }

        /// Absolute path for an art file ("title/video/frame_01.png"), or "" when absent.
        public static string Art(string relative)
        {
            string root = ArtRoot;
            if (string.IsNullOrEmpty(root)) return "";
            string p = Path.Combine(root, relative);
            try { return File.Exists(p) ? p : ""; }
            catch (Exception) { return ""; }
        }

        public static bool ArtExists(string relative)
        {
            return Art(relative).Length > 0;
        }

        /// A file:// URI UnityWebRequest accepts. The project path contains spaces, so
        /// this MUST go through Uri rather than string concatenation.
        public static string FileUrl(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return "";
            try { return new Uri(absolutePath).AbsoluteUri; }
            catch (Exception) { return "file://" + absolutePath; }
        }

        /// The url for an art file, or "" when the file is not there.
        public static string ArtUrl(string relative)
        {
            string p = Art(relative);
            return p.Length == 0 ? "" : FileUrl(p);
        }

        public static void TryCreateDir(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); }
            catch (Exception e) { Debug.LogWarning("RUNWAY! cannot create " + dir + ": " + e.Message); }
        }

        public static string ReadAllTextOrEmpty(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : ""; }
            catch (Exception e)
            {
                Debug.LogWarning("RUNWAY! cannot read " + path + ": " + e.Message);
                return "";
            }
        }

        public static bool WriteAllText(string path, string contents)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                TryCreateDir(dir);
                File.WriteAllText(path, contents);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("RUNWAY! cannot write " + path + ": " + e.Message);
                return false;
            }
        }

        /// StreamingAssets file (prompts/adjudicator.txt, items.json, build_stamp.txt).
        public static string Streaming(string relative)
        {
            return Path.Combine(Application.streamingAssetsPath, relative);
        }
    }
}
