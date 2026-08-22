using UnityEngine;

namespace Runway.App
{
    /// <summary>
    /// WHICH BUILD AM I ACTUALLY RUNNING — the question that cost a whole session.
    /// The Godot build reads res://build_stamp.txt; this reads the same file out of
    /// StreamingAssets, prints it once at boot, and the title screen corners it.
    /// </summary>
    public static class BuildStamp
    {
        const string FileName = "build_stamp.txt";

        static string _value;

        public static string Value
        {
            get
            {
                if (_value != null) return _value;
                string txt = RunwayPaths.ReadAllTextOrEmpty(RunwayPaths.Streaming(FileName)).Trim();
                _value = txt.Length > 0 ? txt : "dev";
                return _value;
            }
        }

        /// One line, once, at boot — the heartbeat a shipped session is judged by.
        public static void PrintOnce()
        {
            if (_printed) return;
            _printed = true;
            Debug.Log("RUNWAY! build: " + Value + " · unity " + Application.unityVersion
                      + " · " + Application.platform);
        }

        static bool _printed;
    }
}
