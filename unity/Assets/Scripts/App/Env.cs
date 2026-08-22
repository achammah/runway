using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Runway.App
{
    /// <summary>
    /// THE ONE KEY, WHEREVER IT LIVES — the port of dotenv.gd plus the key file the
    /// keys screen writes.
    ///
    /// Layering, lowest priority first (the Godot original's order, kept exactly):
    ///   1. the project/app .env  (res://.env  ->  next to the executable, or the
    ///      project root in the editor)
    ///   2. keys.env in the user folder (user://keys.env), which OVERRIDES it
    /// and, on top of both, a real process environment variable — because
    /// scene_director.gd reads OS.get_environment("OPENAI_API_KEY") first and the
    /// renderer must read the SAME stack the narrator does.
    /// </summary>
    public static class Env
    {
        public const string KeysFileName = "keys.env";

        static Dictionary<string, string> _cache;

        public static string KeysPath { get { return RunwayPaths.User(KeysFileName); } }

        /// True once the player has answered the keys screen at all — with a key or
        /// with "play without". main.gd gates the whole first boot on this file.
        public static bool KeysFileExists
        {
            get
            {
                try { return File.Exists(KeysPath); }
                catch (Exception) { return false; }
            }
        }

        /// The layered environment. Cached; call Reload() after the keys screen saves.
        public static Dictionary<string, string> Load()
        {
            if (_cache != null) return _cache;
            var env = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string p in ProjectEnvCandidates()) Merge(env, Parse(p));
            Merge(env, Parse(KeysPath));
            _cache = env;
            return _cache;
        }

        public static Dictionary<string, string> Reload()
        {
            _cache = null;
            return Load();
        }

        /// One value out of the layered stack, with the process environment on top.
        public static string Get(string key, string fallback = "")
        {
            string live = null;
            try { live = Environment.GetEnvironmentVariable(key); }
            catch (Exception) { /* sandboxed hosts can refuse; the files still answer */ }
            if (!string.IsNullOrEmpty(live)) return live.Trim();
            string v;
            if (Load().TryGetValue(key, out v) && !string.IsNullOrEmpty(v)) return v;
            return fallback;
        }

        /// A harness/opt-out switch: set and non-empty, exactly like OS.get_environment.
        public static bool Flag(string key)
        {
            return Get(key, "").Length > 0;
        }

        public static string OpenAiKey { get { return Get("OPENAI_API_KEY", ""); } }

        /// The keys screen's save: one line, user folder only, never the project.
        public static bool SaveOpenAiKey(string key)
        {
            bool ok = RunwayPaths.WriteAllText(KeysPath, "OPENAI_API_KEY=" + key.Trim() + "\n");
            Reload();
            return ok;
        }

        /// "play without — authored world only": the file exists, so the gate is
        /// answered, and it holds no key.
        public static bool SaveKeyless()
        {
            bool ok = RunwayPaths.WriteAllText(KeysPath, "# keyless by choice\n");
            Reload();
            return ok;
        }

        static IEnumerable<string> ProjectEnvCandidates()
        {
            // editor: <project>/.env  ·  player: next to the .app, and inside it
            yield return Path.Combine(Application.dataPath, "../.env");
            yield return Path.Combine(Application.dataPath, "../../.env");
            yield return Path.Combine(Application.streamingAssetsPath, ".env");
        }

        static void Merge(Dictionary<string, string> into, Dictionary<string, string> from)
        {
            foreach (var kv in from) into[kv.Key] = kv.Value;
        }

        /// KEY=value, # comments, optional double quotes — dotenv.gd, line for line.
        public static Dictionary<string, string> Parse(string path)
        {
            var env = new Dictionary<string, string>(StringComparer.Ordinal);
            string txt;
            try
            {
                if (!File.Exists(path)) return env;
                txt = File.ReadAllText(path);
            }
            catch (Exception) { return env; }
            foreach (string rawLine in txt.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();
                if (val.Length >= 2 && val.StartsWith("\"") && val.EndsWith("\""))
                    val = val.Substring(1, val.Length - 2);
                env[key] = val;
            }
            return env;
        }
    }
}
