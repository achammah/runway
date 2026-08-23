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
    ///
    /// A MISS AND A FAILURE ARE NOT THE SAME THING. Only a path with nothing on disk
    /// behind it is remembered as absent. A request that errored, or arrived before
    /// there was anything to fetch on, leaves the path UNKNOWN so the next ask really
    /// tries again — one such race used to blank a drawing for the rest of the session.
    ///
    /// AND A FACE BEATS A CARD SPRITE. Decodes are paced one per frame (see Pump), so
    /// a queue of 144 loop frames can hold the one picture the player is looking at
    /// for two seconds. `urgent` puts an ask at the FRONT of that queue, and promotes
    /// one that is already waiting.
    ///
    /// BAKED FIRST, STREAMED SECOND — and the cache REMEMBERS WHICH. `Build` mirrors
    /// every drawing it can block-compress into `Assets/Resources/Art/<the same
    /// relative path>`, so most asks are now answered by an imported, GPU-ready
    /// texture on the very frame they are made: no web request, no main-thread
    /// decode, no place in the queue. Everything else — `gen_scenes`, which is
    /// written after the build, and every source whose dimensions cannot be block
    /// compressed — falls through to exactly the path it took before.
    ///
    /// The provenance is not bookkeeping for its own sake. A baked texture is a
    /// SHARED asset: `Destroy` on it destroys the one copy Unity holds, so the next
    /// `Resources.Load` of that path can hand back a destroyed object and, in the
    /// editor, the imported asset itself can be damaged. Baked entries are given
    /// back with `Resources.UnloadAsset`; only streamed ones are destroyed. Same
    /// distinction `SheetLoop._sheetBaked` makes, for the same reason.
    /// </summary>
    public static class ArtCache
    {
        static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, float> _lastUse = new Dictionary<string, float>();
        static readonly Dictionary<string, List<Action<Texture2D>>> _waiting =
            new Dictionary<string, List<Action<Texture2D>>>();

        /// WHERE EACH HELD PICTURE CAME FROM. A path in here is an asset borrowed
        /// from Resources; a path in `_tex` and not in here was decoded off disk and
        /// belongs to the cache. Nothing else may be inferred from a Texture2D at
        /// runtime — a compressed format is not proof of provenance and a name is
        /// not either.
        static readonly HashSet<string> _baked = new HashSet<string>();

        /// True when this path is already held AND came from Resources.
        public static bool IsBaked(string relative)
        {
            return relative != null && _baked.Contains(relative);
        }

        /// True when there is an imported mirror behind this path at all — asked
        /// BEFORE any load, which is what a screen's "is there art for this?" gate
        /// and the harnesses both need: it says which contract applies (baked:
        /// answered inside the call · streamed: queued, then pumped) without having
        /// asked for the picture yet.
        ///
        /// It does resolve the asset to answer, so the texture is in memory
        /// afterwards but NOT in `_tex` and NOT counted by `Sweep`. Ask it about a
        /// path, not about a folder full of them, and follow a true answer with the
        /// `Load` that was going to happen anyway.
        public static bool HasBaked(string relative)
        {
            return Baked(relative) != null;
        }

        /// True when the picture is already in hand (or already known absent).
        public static bool Known(string relative)
        {
            return relative != null && _tex.ContainsKey(relative);
        }

        /// How many pictures are still waiting their turn at the decoder.
        public static int Pending { get { return _fetchQueue.Count; } }

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

        /// WHAT A HELD PICTURE ACTUALLY COSTS. A streamed decode is RGBA32 — four
        /// bytes a pixel, which is the number this budget was written against. A
        /// BAKED one is block compressed: DXT1 at 4 bits a pixel, BC7 and DXT5 at 8.
        /// Charging those `width * height * 4` over-counts them by four to eight
        /// times, and a budget that believes a screen is holding 280MB when it is
        /// holding 40 evicts the whole screen to reclaim bytes nobody has.
        ///
        /// Read off the texture's own format rather than its provenance, so a
        /// streamed RGBA32 and an imported BC7 of the same picture each cost what
        /// they cost. Block formats are charged by whole 4x4 blocks, which is how
        /// the hardware stores them.
        public static long Footprint(Texture2D t)
        {
            if (t == null) return 0L;
            long blocks = (long)((t.width + 3) / 4) * ((t.height + 3) / 4);
            long bytes;
            switch (t.format)
            {
                case TextureFormat.DXT1:                       // BC1, 4 bpp
                case TextureFormat.DXT1Crunched:
                case TextureFormat.BC4:
                    bytes = blocks * 8L;
                    break;
                case TextureFormat.DXT5:                       // BC3, 8 bpp
                case TextureFormat.DXT5Crunched:
                case TextureFormat.BC7:                        // 8 bpp
                case TextureFormat.BC5:
                case TextureFormat.BC6H:
                    bytes = blocks * 16L;
                    break;
                case TextureFormat.RGB24:
                    bytes = (long)t.width * t.height * 3L;
                    break;
                case TextureFormat.R8:
                case TextureFormat.Alpha8:
                    bytes = (long)t.width * t.height;
                    break;
                default:                                       // RGBA32 and friends
                    bytes = (long)t.width * t.height * 4L;
                    break;
            }
            // a mip chain is a third again on top; nothing this cache holds has one
            // today (both roads switch mips off), so this is a guard, not a cost
            if (t.mipmapCount > 1) bytes += bytes / 3L;
            return bytes;
        }

        /// THE FLOOR COMES BACK DOWN (perf soak: the hold-everything policy
        /// reached a 704MB steady state against a 400MB bar). Called between
        /// screens: while the cache is over budget, the textures nobody has
        /// asked for in `minAge` seconds are released, oldest first. The
        /// age guard keeps everything the LIVE screen holds (it asked
        /// recently); an evicted picture simply reloads on next request.
        ///
        /// A BAKED ENTRY IS GIVEN BACK, NOT DESTROYED. `Destroy` on an asset loaded
        /// from Resources destroys the SHARED instance — every other holder of that
        /// texture is left pointing at a dead object, the next `Resources.Load` of
        /// the path can return it dead, and in the editor the imported asset itself
        /// is at risk. `Resources.UnloadAsset` returns the memory and leaves the
        /// asset loadable, which is the whole point of having staged it.
        public static void Sweep(long maxBytes = 280L * 1024 * 1024, float minAge = 45f)
        {
            long held = 0;
            var order = new List<KeyValuePair<float, string>>();
            foreach (var kv in _tex)
            {
                if (kv.Value == null) continue;
                held += Footprint(kv.Value);
                float used;
                _lastUse.TryGetValue(kv.Key, out used);
                order.Add(new KeyValuePair<float, string>(used, kv.Key));
            }
            if (held <= maxBytes) return;
            order.Sort((a, b) => a.Key.CompareTo(b.Key));
            float now = Time.realtimeSinceStartup;
            int dropped = 0, given = 0;
            foreach (var pair in order)
            {
                if (held <= maxBytes) break;
                if (now - pair.Key < minAge) break;   // everything younger stays
                Texture2D t = _tex[pair.Value];
                bool baked = _baked.Remove(pair.Value);
                _tex.Remove(pair.Value);              // reloadable on next ask
                _lastUse.Remove(pair.Value);
                held -= Footprint(t);
                if (baked) { Resources.UnloadAsset(t); given++; }
                else UnityEngine.Object.Destroy(t);
                dropped++;
            }
            if (dropped > 0)
                Debug.Log("RUNWAY! art sweep: " + dropped + " drawings released ("
                          + given + " given back to Resources, " + (dropped - given)
                          + " destroyed), " + (held / (1024 * 1024)) + "MB held");
        }

        /// Hand the texture to `cb` — this frame if it is cached, later if it is not.
        /// `cb` is called exactly once, with null when the file is not on disk. When
        /// there is no runner to fetch on YET the ask waits in the queue instead of
        /// being answered with a false "absent"; the next Load that finds a runner
        /// drains it. `urgent` jumps the ask to the front of the decode queue.
        public static void Load(string relative, Action<Texture2D> cb, bool urgent = false)
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

            // THE BAKED MIRROR, BEFORE ANYTHING TOUCHES THE DISK. An imported
            // texture needs no runner, no request and no decode, so this answers on
            // the SAME FRAME the ask was made. It has to sit above the absent-path
            // memo below as well as above the queue: a staged drawing whose streamed
            // copy is no longer beside it would otherwise be remembered as missing
            // for the rest of the session.
            Texture2D baked = Baked(relative);
            if (baked != null)
            {
                _tex[relative] = baked;
                _baked.Add(relative);
                _lastUse[relative] = Time.realtimeSinceStartup;
                BakedRoute++;
                cb(baked);
                return;
            }

            Boot boot = Boot.Instance;
            List<Action<Texture2D>> queue;
            if (_waiting.TryGetValue(relative, out queue))
            {
                queue.Add(cb);
                if (urgent) Promote(relative);   // a face that arrived late still cuts in
                Start(boot);
                return;
            }

            string url = RunwayPaths.ArtUrl(relative);
            if (url.Length == 0)
            {
                // NOTHING ON DISK BEHIND THIS PATH. Remembered as absent and never
                // asked for again — the drawn fallback is permanent for it, which is
                // the Godot contract. This is the ONLY door to that memory.
                _tex[relative] = null;
                cb(null);
                return;
            }

            queue = new List<Action<Texture2D>> { cb };
            _waiting[relative] = queue;
            var job = new KeyValuePair<string, string>(relative, url);
            if (urgent) _fetchQueue.AddFirst(job); else _fetchQueue.AddLast(job);
            Start(boot);
        }

        /// THE IMPORTED MIRROR, LOOKED UP BY THE WHOLE RELATIVE PATH. `SheetLoop`
        /// can key a sheet on its basename because 26 hand-picked names cannot
        /// clash; these paths CAN — `sprites/chart_1.png` and
        /// `sprites/gv/chart_1.png` are two different drawings — so the folder
        /// structure is preserved under `Resources/Art` and the whole path minus its
        /// extension is the key. Null for everything that was never staged, which is
        /// how `gen_scenes` and every not-yet-compressible source keep streaming.
        static Texture2D Baked(string relative)
        {
            if (string.IsNullOrEmpty(relative)) return null;
            if (!relative.EndsWith(".png", StringComparison.Ordinal)) return null;
            return Resources.Load<Texture2D>("Art/" + relative.Substring(0, relative.Length - 4));
        }

        static void Start(MonoBehaviour boot)
        {
            if (_pumping || boot == null || _fetchQueue.Count == 0) return;
            _pumping = true;
            boot.StartCoroutine(Pump());
        }

        /// Move a job that is already queued to the front — the picture stopped being
        /// background the moment somebody put it on screen.
        static void Promote(string relative)
        {
            for (var n = _fetchQueue.First; n != null; n = n.Next)
            {
                if (n.Value.Key != relative) continue;
                if (n == _fetchQueue.First) return;
                _fetchQueue.Remove(n);
                _fetchQueue.AddFirst(n);
                return;
            }
        }

        // ONE DECODE PER FRAME. Five founder cards hydrating 36-frame loops in
        // parallel used to cluster their PNG decodes (DownloadHandlerTexture
        // pays on the main thread) into single frames — the perf soak caught an
        // 889ms frame on the draft. A single pump makes the cost a steady drip:
        // each completion lands on its own frame, order preserved except where
        // `urgent` has cut in.
        static readonly LinkedList<KeyValuePair<string, string>> _fetchQueue =
            new LinkedList<KeyValuePair<string, string>>();
        static bool _pumping;

        static IEnumerator Pump()
        {
            while (_fetchQueue.Count > 0)
            {
                var job = _fetchQueue.First.Value;
                _fetchQueue.RemoveFirst();
                Texture2D tex = null;
                yield return SheetLoop.LoadTexture(job.Value, t => tex = t);
                Deliver(job.Key, tex);
                yield return null;   // the next decode gets its own frame
            }
            _pumping = false;
        }

        /// THE SAME QUEUE, DRAINED WITH NO COROUTINE — for the editor and a batch
        /// harness, where nothing is ticking. Never on the game's path: the game has
        /// frames. Returns how many pictures it fetched.
        ///
        /// TWO ROUTES, AND IT SAYS WHICH ONE IT TOOK. The shipped route is the pump's
        /// own — SheetLoop.LoadTexture, driven by hand. A file:// UnityWebRequest only
        /// completes when something pumps the update loop, and `-executeMethod` holds
        /// that loop for the whole call, so in batch it can never come back; when it
        /// does not, the bytes are read and decoded here instead. The queue, the
        /// ordering, the delivery and every waiting callback are the shipped ones on
        /// either route.
        /// THREE ROUTES NOW, AND THE COUNTERS SAY WHICH CARRIED WHAT. `BakedRoute`
        /// is incremented in `Load` rather than here, because a baked ask never
        /// reaches this queue at all — which is the point of it.
        public static int WebRoute, DiskRoute, BakedRoute;

        public static int PumpBlocking(int max = 4096)
        {
            int done = 0;
            while (_fetchQueue.Count > 0 && done < max)
            {
                var job = _fetchQueue.First.Value;
                _fetchQueue.RemoveFirst();
                Texture2D tex = null;
                Step(SheetLoop.LoadTexture(job.Value, t => tex = t));
                if (tex != null) WebRoute++;
                else { tex = ReadFromDisk(job.Key); if (tex != null) DiskRoute++; }
                Deliver(job.Key, tex);
                done++;
            }
            return done;
        }

        static Texture2D ReadFromDisk(string relative)
        {
            string abs = RunwayPaths.Art(relative);
            if (abs.Length == 0) return null;
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(System.IO.File.ReadAllBytes(abs)))
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                    return null;
                }
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                return tex;
            }
            catch (Exception e)
            {
                Debug.LogWarning("RUNWAY! blocking read failed for " + relative + ": " + e.Message);
                return null;
            }
        }

        /// Drive a coroutine by hand: a nested routine recurses, an AsyncOperation is
        /// spun to completion, and a frame wait is simply not waited for — there are
        /// no frames here.
        static void Step(IEnumerator e)
        {
            while (e.MoveNext())
            {
                var nested = e.Current as IEnumerator;
                if (nested != null) { Step(nested); continue; }
                var op = e.Current as AsyncOperation;
                if (op == null) continue;
                float t0 = Time.realtimeSinceStartup;
                while (!op.isDone)
                {
                    float waited = Time.realtimeSinceStartup - t0;
                    if (waited > 3f)
                    {
                        Debug.LogWarning("RUNWAY! a blocking fetch never completed in "
                                         + waited.ToString("0.0") + "s — nothing is pumping "
                                         + "the update loop, so the disk route takes it");
                        break;
                    }
                    System.Threading.Thread.Sleep(1);
                }
            }
        }

        static void Deliver(string relative, Texture2D tex)
        {
            // A FAILED REQUEST IS NOT A MISSING FILE. A path whose file is right there
            // stays unknown when the fetch comes back empty, so the next ask retries;
            // caching that null was how one early race poisoned a drawing for good.
            if (tex != null || !RunwayPaths.ArtExists(relative))
            {
                _tex[relative] = tex;
                _baked.Remove(relative);   // whatever lands here was decoded, not borrowed
                _lastUse[relative] = Time.realtimeSinceStartup;
            }
            else
            {
                Debug.LogWarning("RUNWAY! art fetch came back empty for a file that IS "
                                 + "on disk: " + relative + " — left unknown, not poisoned");
            }
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
