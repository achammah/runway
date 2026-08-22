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
            if (relative != null && _tex.TryGetValue(relative, out t)) return t;
            return null;
        }

        /// Hand the texture to `cb` — this frame if it is cached, later if it is not.
        /// `cb` is ALWAYS called exactly once, with null when the file is not there.
        public static void Load(string relative, Action<Texture2D> cb)
        {
            if (cb == null) return;
            if (string.IsNullOrEmpty(relative)) { cb(null); return; }

            Texture2D have;
            if (_tex.TryGetValue(relative, out have)) { cb(have); return; }

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
