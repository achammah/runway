using System;
using System.Collections.Generic;

// THE HOST STAND-IN. Runway.App (RunwayPaths, SaveSlots, Env) and Runway.Game
// (RunSave, RunRecord, ContentDb) are shipped files that compile against
// UnityEngine for exactly four things: Debug, Mathf, Application and Random.
// This file supplies those four so the SHIPPED SOURCES themselves — not copies,
// not re-implementations — run under `dotnet run`.
//
// It is also the A17 seam: every Debug line the shipped code prints lands in
// UnityEngine.Debug.Lines, where the key-leak assertion can read it.
//
// It lives ONLY in this test project. Unity never compiles anything under
// unity/Runway.ATail.Tests/, so there is no chance of a duplicate-symbol clash
// with the real UnityEngine.
namespace UnityEngine
{
    public enum RuntimePlatform
    {
        OSXEditor = 0,
        OSXPlayer = 1,
        WindowsPlayer = 2,
        LinuxPlayer = 13,
    }

    public static class Application
    {
        public static RuntimePlatform platform = RuntimePlatform.OSXEditor;
        public static string dataPath = "";
        public static string streamingAssetsPath = "";
        public static string persistentDataPath = "";
        public static int targetFrameRate;
    }

    /// Every line the shipped code prints, kept verbatim for the leak sweep.
    public static class Debug
    {
        public static readonly List<string> Lines = new List<string>();
        public static bool Echo;

        public static void Log(object m) { Add("LOG", m); }
        public static void LogWarning(object m) { Add("WARN", m); }
        public static void LogError(object m) { Add("ERROR", m); }

        static void Add(string kind, object m)
        {
            string line = kind + ": " + Convert.ToString(m);
            Lines.Add(line);
            if (Echo) { Console.WriteLine("      [unity] " + line); }
        }

        public static void Clear() { Lines.Clear(); }
    }

    public static class Mathf
    {
        public static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static float Clamp01(float v) { return Clamp(v, 0f, 1f); }
        public static int Max(int a, int b) { return a > b ? a : b; }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static int Min(int a, int b) { return a < b ? a : b; }
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static int Abs(int a) { return a < 0 ? -a : a; }
        public static float Abs(float a) { return a < 0f ? -a : a; }
        public static int RoundToInt(float f) { return (int)Math.Round(f, MidpointRounding.AwayFromZero); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
    }

    public static class Random
    {
        static readonly System.Random _r = new System.Random(20260817);
        public static float value { get { return (float)_r.NextDouble(); } }
    }
}
