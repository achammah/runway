using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Runway.Audio
{
    /// <summary>
    /// The OST, ported from game/src/core/music_manager.gd: five 96 BPM loops in
    /// one key family with bar-aligned seamless loop points, 2.5s (one bar)
    /// crossfades between states, and mood stems (whistle/hum) that drift in
    /// over the garage loop when morale is high.
    ///
    /// Godot's main.gd drives its manager by hand from a dozen sites; here the
    /// manager DRIVES ITSELF by polling Boot.Instance four times a second, so
    /// integration needs zero hookup lines. The mapping is main.gd's, verbatim:
    /// title/keys/howto/studio → title · draft/birth/book → selection ·
    /// garage → in_the_red when cash&lt;0 else garage, whistle at morale≥75,
    /// hum at ≥55 · finale/autopsy → last_page.
    ///
    /// Unity has no AudioStreamWAV.loop_end, so each clip is TRIMMED to its
    /// bar-aligned loop point at load (GetData into a shorter clip) and then
    /// AudioSource.loop is sample-exact. Every source registers with RunwayMix
    /// ("music"), whose ducks multiply against this manager's own fades.
    /// Kill-switch: RUNWAY_FX_MUSIC=0. Silent under harness envs.
    /// </summary>
    public sealed class MusicManager : MonoBehaviour
    {
        const float Xfade = 2.5f;   // one bar at 96 BPM
        const float FloorDb = -60f;

        struct Track { public string File; public float LoopEnd; public float Db; }

        static readonly Dictionary<string, Track> Tracks = new Dictionary<string, Track>
        {
            { "title",      new Track { File = "01_title.wav",      LoopEnd = 62.5f, Db = -9f } },
            { "selection",  new Track { File = "02_selection.wav",  LoopEnd = 30.0f, Db = -10f } },
            { "garage",     new Track { File = "03_garage.wav",     LoopEnd = 20.0f, Db = -12f } },
            { "in_the_red", new Track { File = "04_in_the_red.wav", LoopEnd = 42.5f, Db = -11f } },
            { "last_page",  new Track { File = "05_last_page.wav",  LoopEnd = 30.0f, Db = -10f } },
        };
        static readonly Dictionary<string, Track> Stems = new Dictionary<string, Track>
        {
            { "whistle", new Track { File = "stem_whistle.wav", LoopEnd = 0f, Db = -16f } },
            { "hum",     new Track { File = "stem_hum.wav",     LoopEnd = 0f, Db = -18f } },
        };

        class Voice
        {
            public AudioSource Src;
            public float TargetDb, CurrentDb;
            public bool Loaded;
        }

        readonly Dictionary<string, Voice> _voices = new Dictionary<string, Voice>();
        string _current = "";
        string _stem = "";
        float _poll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            // harness runs measure and photograph; music is noise there
            string[] silence = { "RUNWAY_USHOTS", "RUNWAY_UPERF", "RUNWAY_LANEWIRE", "RUNWAY_SHOT" };
            foreach (string v in silence)
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(v))) return;
            string sw = Environment.GetEnvironmentVariable("RUNWAY_FX_MUSIC");
            if (sw == "0" || sw == "off" || sw == "false") { Debug.Log("RUNWAY! music OFF (switch)"); return; }
            var go = new GameObject("runway_music");
            DontDestroyOnLoad(go);
            go.AddComponent<MusicManager>();
        }

        void Awake()
        {
            foreach (var kv in Tracks) StartCoroutine(LoadVoice(kv.Key, kv.Value, "music"));
            foreach (var kv in Stems) StartCoroutine(LoadVoice(kv.Key, kv.Value, "music"));
        }

        IEnumerator LoadVoice(string name, Track cfg, string bed)
        {
            string path = Path.Combine(Runway.App.RunwayPaths.ArtRoot, "music", cfg.File);
            if (!File.Exists(path)) { Debug.Log("RUNWAY! music missing " + path); yield break; }
            using (var req = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.WAV))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                { Debug.Log("RUNWAY! music load failed " + cfg.File + ": " + req.error); yield break; }
                AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                if (clip == null) yield break;
                clip = TrimToLoop(clip, cfg.LoopEnd, name);
                var src = gameObject.AddComponent<AudioSource>();
                src.clip = clip;
                src.loop = true;
                src.playOnAwake = false;
                src.volume = 0f;
                var v = new Voice { Src = src, TargetDb = FloorDb, CurrentDb = FloorDb, Loaded = true };
                _voices[name] = v;
                RunwayMix.RegisterSource(src, bed);
                // the state may already want this voice (loads race the poll)
                if (name == _current) FadeTo(name, TrackDb(name));
                else if (name == _stem) FadeTo(name, StemDb(name));
            }
        }

        /// Bar-aligned seamless loop: cut the tail so AudioSource.loop lands
        /// exactly on the bar. Godot did this with loop_end; Unity gets a copy.
        static AudioClip TrimToLoop(AudioClip clip, float loopEndSec, string name)
        {
            if (loopEndSec <= 0f) return clip;
            int want = Mathf.Min(clip.samples, Mathf.RoundToInt(loopEndSec * clip.frequency));
            if (want >= clip.samples || want <= 0) return clip;
            var data = new float[want * clip.channels];
            clip.GetData(data, 0);
            var cut = AudioClip.Create(name + "_loop", want, clip.channels, clip.frequency, false);
            cut.SetData(data, 0);
            return cut;
        }

        static float TrackDb(string n) { return Tracks.ContainsKey(n) ? Tracks[n].Db : FloorDb; }
        static float StemDb(string n) { return Stems.ContainsKey(n) ? Stems[n].Db : FloorDb; }

        void Update()
        {
            // fades tick every frame (cheap: 7 voices, two floats each)
            float dt = Time.unscaledDeltaTime;
            foreach (var v in _voices.Values)
            {
                if (!v.Loaded) continue;
                if (!Mathf.Approximately(v.CurrentDb, v.TargetDb))
                {
                    float step = (Mathf.Abs(TrackSpanDb(v)) / Xfade) * dt;
                    v.CurrentDb = Mathf.MoveTowards(v.CurrentDb, v.TargetDb, step);
                    ApplyDb(v);
                }
            }
            _poll += dt;
            if (_poll < 0.25f) return;
            _poll = 0f;
            Drive();
        }

        // span for a constant-time fade: always the full floor→target distance
        float TrackSpanDb(Voice v) { return 51f; }   // -60 → about -9, one bar

        void ApplyDb(Voice v)
        {
            v.Src.volume = v.CurrentDb <= FloorDb + 0.5f ? 0f : Mathf.Pow(10f, v.CurrentDb / 20f);
            if (v.Src.volume <= 0f && v.Src.isPlaying && Mathf.Approximately(v.TargetDb, FloorDb))
                v.Src.Stop();
        }

        /// main.gd's mapping, from polled state instead of call sites.
        void Drive()
        {
            var boot = Runway.App.Boot.Instance;
            if (boot == null) return;
            string track = _current;
            string stem = "";
            switch (boot.State)
            {
                case Runway.App.AppState.StudioCard:
                case Runway.App.AppState.Keys:
                case Runway.App.AppState.Title:
                case Runway.App.AppState.HowTo:
                    track = "title"; break;
                case Runway.App.AppState.Draft:
                case Runway.App.AppState.Birth:
                case Runway.App.AppState.Book:
                    track = "selection"; break;
                case Runway.App.AppState.Garage:
                    var g = boot.CurrentScreen as Runway.Game.GarageScreen;
                    var st = g != null ? g.State : null;
                    if (st != null && st.Cash < 0) { track = "in_the_red"; }
                    else
                    {
                        track = "garage";
                        if (st != null && st.Morale >= 75) stem = "whistle";
                        else if (st != null && st.Morale >= 55) stem = "hum";
                    }
                    break;
                case Runway.App.AppState.Finale:
                case Runway.App.AppState.Autopsy:
                    track = "last_page"; break;
            }
            Play(track);
            SetStem(stem);
        }

        /// Crossfade to a track ("" = silence). Safe to call every poll.
        public void Play(string name)
        {
            if (name == _current) return;
            string prev = _current;
            _current = name;
            if (prev != "" && _voices.ContainsKey(prev)) FadeTo(prev, FloorDb);
            if (name != "" && _voices.ContainsKey(name)) FadeTo(name, TrackDb(name));
        }

        public void SetStem(string name)
        {
            if (name == _stem) return;
            string prev = _stem;
            _stem = name;
            if (prev != "" && _voices.ContainsKey(prev)) FadeTo(prev, FloorDb);
            if (name != "" && _voices.ContainsKey(name)) FadeTo(name, StemDb(name));
        }

        void FadeTo(string name, float db)
        {
            var v = _voices[name];
            if (!v.Loaded) return;
            v.TargetDb = db;
            if (db > FloorDb && !v.Src.isPlaying) v.Src.Play();
        }
    }
}
