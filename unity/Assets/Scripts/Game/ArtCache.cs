using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Runway.App;

namespace Runway.Game
{
    /// <summary>
    /// ONE COPY OF EVERY DRAWING, FOR THE WHOLE SESSION.
    ///
    /// The Godot build calls load() on a res:// path and the engine's own resource
    /// cache makes the second call free. Unity has no such cache for a file read off
    /// disk — every screen here streams its art through UnityWebRequestTexture — so
    /// the draft would re-download 144 loop frames each time a founder is re-selected
    /// and the journal would re-read its doodles every single week.
    ///
    /// So: one dictionary, keyed by the art-relative path, plus a waiting list per
    /// path so twelve callers asking for the same picture in the same frame cost one
    /// read. A missing file caches as null and is never asked for twice — the drawn
    /// fallback is then permanent for that path, which is the Godot contract.
    /// </summary>
    public static class ArtCache
    {
        static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, float> _lastUse = new Dictionary<string, float>();
        static readonly Dictionary<string, List<Action<Texture2D>>> _waiting =
            new Dictionary<string, List<Action<Texture2D>>>();

        /// True when the picture is already in hand (or already known absent).
        public static bool Known(string relative)
        {
            return relative != null && _tex.ContainsKey(relative);
        }

        public static Texture2D Peek(string relative)
        {
            Texture2D t;
            if (relative != null && _tex.TryGetValue(relative, out t))
            {
                _lastUse[relative] = Time.realtimeSinceStartup;
                return t;
            }
            return null;
        }

        /// THE FLOOR COMES BACK DOWN (perf soak: the hold-everything policy
        /// reached a 704MB steady state against a 400MB bar). Called between
        /// screens: while the cache is over budget, the textures nobody has
        /// asked for in `minAge` seconds are destroyed, oldest first. The
        /// age guard keeps everything the LIVE screen holds (it asked
        /// recently); an evicted picture simply reloads on next request.
        public static void Sweep(long maxBytes = 280L * 1024 * 1024, float minAge = 45f)
        {
            long held = 0;
            var order = new List<KeyValuePair<float, string>>();
            foreach (var kv in _tex)
            {
                if (kv.Value == null) continue;
                held += (long)kv.Value.width * kv.Value.height * 4;
                float used;
                _lastUse.TryGetValue(kv.Key, out used);
                order.Add(new KeyValuePair<float, string>(used, kv.Key));
            }
            if (held <= maxBytes) return;
            order.Sort((a, b) => a.Key.CompareTo(b.Key));
            float now = Time.realtimeSinceStartup;
            int dropped = 0;
            foreach (var pair in order)
            {
                if (held <= maxBytes) break;
                if (now - pair.Key < minAge) break;   // everything younger stays
                Texture2D t = _tex[pair.Value];
                _tex.Remove(pair.Value);              // reloadable on next ask
                _lastUse.Remove(pair.Value);
                held -= (long)t.width * t.height * 4;
                UnityEngine.Object.Destroy(t);
                dropped++;
            }
            if (dropped > 0)
                Debug.Log("RUNWAY! art sweep: " + dropped + " drawings released, "
                          + (held / (1024 * 1024)) + "MB held");
        }

        /// Hand the texture to `cb` — this frame if it is cached, later if it is not.
        /// `cb` is ALWAYS called exactly once, with null when the file is not there.
        public static void Load(string relative, Action<Texture2D> cb)
        {
            if (cb == null) return;
            if (string.IsNullOrEmpty(relative)) { cb(null); return; }

            Texture2D have;
            if (_tex.TryGetValue(relative, out have))
            {
                _lastUse[relative] = Time.realtimeSinceStartup;
                cb(have);
                return;
            }

            List<Action<Texture2D>> queue;
            if (_waiting.TryGetValue(relative, out queue)) { queue.Add(cb); return; }

            var boot = Boot.Instance;
            string url = RunwayPaths.ArtUrl(relative);
            if (boot == null || url.Length == 0)
            {
                _tex[relative] = null;      // never ask again for a file that is not there
                cb(null);
                return;
            }
            queue = new List<Action<Texture2D>> { cb };
            _waiting[relative] = queue;
            _fetchQueue.Enqueue(new KeyValuePair<string, string>(relative, url));
            if (!_pumping)
            {
                _pumping = true;
                boot.StartCoroutine(Pump());
            }
        }

        // ONE DECODE PER FRAME. Five founder cards hydrating 36-frame loops in
        // parallel used to cluster their PNG decodes (DownloadHandlerTexture
        // pays on the main thread) into single frames — the perf soak caught an
        // 889ms frame on the draft. A single FIFO pump makes the cost a steady
        // drip: each completion lands on its own frame, order preserved.
        static readonly Queue<KeyValuePair<string, string>> _fetchQueue =
            new Queue<KeyValuePair<string, string>>();
        static bool _pumping;

        static IEnumerator Pump()
        {
            while (_fetchQueue.Count > 0)
            {
                var job = _fetchQueue.Dequeue();
                Texture2D tex = null;
                yield return SheetLoop.LoadTexture(job.Value, t => tex = t);
                Deliver(job.Key, tex);
                yield return null;   // the next decode gets its own frame
            }
            _pumping = false;
        }

        static void Deliver(string relative, Texture2D tex)
        {
            _tex[relative] = tex;
            _lastUse[relative] = Time.realtimeSinceStartup;
            List<Action<Texture2D>> queue;
            if (_waiting.TryGetValue(relative, out queue))
            {
                _waiting.Remove(relative);
                for (int i = 0; i < queue.Count; i++)
                {
                    // one bad callback must not strand the other eleven
                    try { queue[i](tex); }
                    catch (Exception e) { Debug.LogWarning("RUNWAY! art callback threw: " + e.Message); }
                }
            }
        }

        /// The sprite folder's own naming: "itm_laptop" → "sprites/itm_laptop.png",
        /// "gv/chart_1" → "sprites/gv/chart_1.png". A path that already names a folder
        /// ("journal_icons/cash.png") is left alone.
        public static string SpritePath(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            if (name.EndsWith(".png")) return name;
            return "sprites/" + name + ".png";
        }

        public static string IconPath(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return "journal_icons/" + name + ".png";
        }
    }
}
