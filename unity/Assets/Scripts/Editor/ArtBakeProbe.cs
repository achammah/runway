#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Runway.Game;
using RunwayBuild = Runway.Build;   // `Build` alone collides with the UnityEditor.Build namespace

namespace Runway.EditorTools
{
    /// <summary>
    /// THE STREAMED-ART MIGRATION, MEASURED RATHER THAN ASSERTED IN A DOCUMENT.
    ///
    /// It stages the mirror, imports it, and then asks the four questions the
    /// migration can actually fail on:
    ///
    ///   1 IS THE PICTURE THE PICTURE?   `npotScale = ToNearest` is what
    ///                                   `Assets/Art` is set to today, and it turns
    ///                                   a 378x378 founder frame into 256x256 in
    ///                                   silence. Every staged texture is compared
    ///                                   against its own source PNG's IHDR: same
    ///                                   width, same height, no mip chain.
    ///   2 DID IT COMPRESS, AND AS WHAT? A source with no alpha must land as DXT1
    ///                                   (4 bits a pixel) and one with alpha as BC7
    ///                                   (8). RGBA32 here means the block
    ///                                   compressor refused the file and the whole
    ///                                   point was lost.
    ///   3 DOES `ArtCache` TAKE THE BAKED ROUTE, and does it answer inside the call
    ///                                   rather than through the one-per-frame
    ///                                   queue?
    ///   4 DOES `Sweep` GIVE A BAKED TEXTURE BACK INSTEAD OF DESTROYING IT?
    ///                                   `Destroy` on an asset loaded from Resources
    ///                                   destroys the SHARED instance. The editor
    ///                                   refuses and logs "Destroying assets is not
    ///                                   permitted to avoid data loss" — so this
    ///                                   probe listens for that exact error while it
    ///                                   forces a full eviction, and then reloads
    ///                                   every evicted path to prove the asset is
    ///                                   still there, still the right size and still
    ///                                   the right format.
    ///
    /// And it prints the two numbers the sweep budget turns on: what the cache
    /// believed a held picture cost (`width * height * 4`) against what it costs.
    ///
    ///   Unity -batchmode -quit -projectPath unity \
    ///         -executeMethod Runway.EditorTools.ArtBakeProbe.Run
    ///
    /// Output goes to $RUNWAY_ARTBAKE_OUT (default /tmp/d-migrate). Exits 1 on any
    /// failed check, so it is a gate and not only a report.
    /// </summary>
    public static class ArtBakeProbe
    {
        const string MirrorRoot = "Assets/Resources/Art";
        const int FilmFrames = 48;
        const long ShippedBudget = 280L * 1024 * 1024;

        static readonly StringBuilder _log = new StringBuilder();
        static int _checks, _fails;
        static int _assetDestroyErrors;
        static bool _listening;

        public static void Run()
        {
            string dir = Environment.GetEnvironmentVariable("RUNWAY_ARTBAKE_OUT");
            if (string.IsNullOrEmpty(dir)) dir = "/tmp/d-migrate";
            Directory.CreateDirectory(dir);

            Say("══ RUNWAY! ART BAKE PROBE ═══════════════════════════════════════");
            Say("");

            try
            {
                Stage();
                List<Sample> samples = SampleSet();
                LoadThrough(samples);
                FullSet();
                Film();
                SweepKeepsBakedAlive(samples);
            }
            catch (Exception e)
            {
                _fails++;
                Say("THREW: " + e);
            }

            Say("");
            Say("── " + (_checks - _fails) + "/" + _checks + " checks passed");
            string outPath = Path.Combine(dir, "report.txt");
            File.WriteAllText(outPath, _log.ToString());
            Debug.Log("RUNWAY! art bake probe wrote " + outPath);
            Console.Write(_log.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(_fails == 0 ? 0 : 1);
        }

        // ══ 1 · stage and import ═══════════════════════════════════════════════

        static void Stage()
        {
            Say("── 1 · STAGE AND IMPORT ─────────────────────────────────────────");
            double t0 = EditorApplication.timeSinceStartup;
            RunwayBuild.EnsureSheets();
            RunwayBuild.EnsureArtResources();
            AssetDatabase.Refresh();
            Say("   staged and imported in "
                + (EditorApplication.timeSinceStartup - t0).ToString("0.0") + "s");

            string root = Path.Combine(ProjectRoot(), MirrorRoot);
            Truth("the mirror exists at " + MirrorRoot, Directory.Exists(root));
            if (!Directory.Exists(root)) return;

            string[] files = Directory.GetFiles(root, "*.png", SearchOption.AllDirectories);
            long png = 0L;
            foreach (string f in files) png += new FileInfo(f).Length;
            Say("   " + files.Length + " drawings mirrored, " + Mb(png) + "MB of PNG source");
            Truth("the mirror is not empty", files.Length > 0);

            // THE FOLDER STRUCTURE IS THE WHOLE POINT (sprites/chart_1.png and
            // sprites/gv/chart_1.png are two pictures with one basename).
            var byBase = new Dictionary<string, int>(StringComparer.Ordinal);
            int collisions = 0;
            foreach (string f in files)
            {
                string b = Path.GetFileNameWithoutExtension(f);
                int n;
                byBase.TryGetValue(b, out n);
                byBase[b] = n + 1;
                if (n == 1) collisions++;
            }
            Say("   " + collisions + " basename(s) appear more than once — which a flat "
                + "Resources/Sheets scheme would have silently merged");
            Truth("the mirror keeps the folder structure, so a collision is harmless",
                  Directory.GetDirectories(root, "*", SearchOption.AllDirectories).Length > 0);
            Say("");
        }

        // ══ 2 · ten paths through ArtCache ═════════════════════════════════════

        struct Sample
        {
            public string Rel;      // art-relative, with .png
            public int W, H;
            public bool Alpha;
        }

        /// Ten paths, chosen to span every mirrored folder and BOTH compression
        /// branches, then topped up from whatever actually staged so the probe still
        /// has ten subjects if the art tree moves under it.
        static List<Sample> SampleSet()
        {
            var wanted = new List<string>
            {
                "env/stage.png",                     // RGB, 1536x1024 → DXT1
                "env/select_stage_scene.png",        // RGB, the largest mirrored source
                "journal_icons/cash.png",            // RGBA, 256x256 → BC7
                "journal_icons/runway.png",
                "sprites/chr_loop_hacker_01.png",    // the founder loops, the reason the cache exists
                "sprites/chr_loop_dropout_01.png",
                "sprites/gv/board_1.png",            // the folder that collides on basename
                "title/anim/bill_01.png",
                "title/layers/base.png",             // RGB with a folder of RGBA beside it
                "sprites/itm_laptop.png",
            };

            var picked = new List<Sample>();
            foreach (string rel in wanted) Add(picked, rel);
            if (picked.Count < 10)
            {
                string root = Path.Combine(ProjectRoot(), MirrorRoot);
                if (Directory.Exists(root))
                    foreach (string f in Directory.GetFiles(root, "*.png", SearchOption.AllDirectories))
                    {
                        if (picked.Count >= 10) break;
                        Add(picked, f.Substring(root.Length + 1).Replace('\\', '/'));
                    }
            }
            return picked;
        }

        static void Add(List<Sample> into, string rel)
        {
            foreach (Sample s in into) if (s.Rel == rel) return;
            string src = Path.Combine(ProjectRoot(), "Assets/Art", rel.Replace('/', Path.DirectorySeparatorChar));
            int w, h;
            bool alpha;
            if (!File.Exists(src) || !RunwayBuild.PngHeader(src, out w, out h, out alpha)) return;
            if ((w & 3) != 0 || (h & 3) != 0) return;   // not staged, by design
            into.Add(new Sample { Rel = rel, W = w, H = h, Alpha = alpha });
        }

        static void LoadThrough(List<Sample> samples)
        {
            Say("── 2 · TEN PATHS THROUGH ArtCache ───────────────────────────────");
            Truth("ten mirrored sample paths were found", samples.Count == 10);
            Say("");
            Say("   path                                    source     imported   fmt      "
                + "held (old→new)");

            foreach (Sample s in samples)
            {
                int routeBefore = ArtCache.BakedRoute;
                bool answered = false;
                Texture2D got = null;
                ArtCache.Load(s.Rel, t => { answered = true; got = t; });

                Truth(s.Rel + " · answered INSIDE the call — no runner, no queue, no decode",
                      answered);
                Truth(s.Rel + " · took the baked route", ArtCache.BakedRoute == routeBefore + 1);
                Truth(s.Rel + " · is remembered as baked, so Sweep will give it back",
                      ArtCache.IsBaked(s.Rel));
                Truth(s.Rel + " · nothing was queued for it", ArtCache.Pending == 0);
                if (got == null) { Truth(s.Rel + " · a texture came back", false); continue; }

                // R3: the ONLY thing that proves npotScale never rescaled anything
                Truth(s.Rel + " · is still " + s.W + "x" + s.H + " — not resampled to a "
                      + "power of two", got.width == s.W && got.height == s.H);
                Truth(s.Rel + " · has no mip chain", got.mipmapCount == 1);

                TextureFormat want = s.Alpha ? TextureFormat.BC7 : TextureFormat.DXT1;
                Truth(s.Rel + " · imported as " + want + " ("
                      + (s.Alpha ? "alpha → 8" : "no alpha → 4") + " bits a pixel), not "
                      + "RGBA32", got.format == want);

                long old = (long)got.width * got.height * 4L;
                long now = ArtCache.Footprint(got);
                Truth(s.Rel + " · costs what it costs, not " + (old / Math.Max(now, 1L))
                      + "x that", now < old);
                Say(string.Format("   {0,-40}{1,4}x{2,-5}{3,4}x{4,-5}{5,-9}{6,7} → {7} KB",
                                  s.Rel, s.W, s.H, got.width, got.height, got.format,
                                  old / 1024, now / 1024));
            }
            Say("");
        }

        // ══ 3 · the whole staged set ═══════════════════════════════════════════

        static void FullSet()
        {
            Say("── 3 · THE WHOLE MIRROR ─────────────────────────────────────────");
            string root = Path.Combine(ProjectRoot(), MirrorRoot);
            if (!Directory.Exists(root)) { Truth("the mirror exists", false); return; }

            int n = 0, resampled = 0, uncompressed = 0, mipped = 0, missing = 0;
            long rgba32 = 0L, actual = 0L;
            foreach (string f in Directory.GetFiles(root, "*.png", SearchOption.AllDirectories))
            {
                string rel = f.Substring(root.Length + 1).Replace('\\', '/');
                string key = "Art/" + rel.Substring(0, rel.Length - 4);
                var tex = Resources.Load<Texture2D>(key);
                if (tex == null) { missing++; continue; }
                n++;
                int w, h;
                bool alpha;
                if (RunwayBuild.PngHeader(f, out w, out h, out alpha))
                {
                    if (tex.width != w || tex.height != h) { resampled++; Say("   RESAMPLED " + rel + ": " + w + "x" + h + " → " + tex.width + "x" + tex.height); }
                    rgba32 += (long)w * h * 4L;
                }
                if (tex.format != TextureFormat.DXT1 && tex.format != TextureFormat.BC7)
                {
                    uncompressed++;
                    Say("   NOT BLOCK COMPRESSED " + rel + ": " + tex.format);
                }
                if (tex.mipmapCount > 1) mipped++;
                actual += ArtCache.Footprint(tex);
            }

            Say("   " + n + " textures resolved through Resources.Load");
            Truth("every mirrored PNG resolves as a Resources texture", missing == 0);
            Truth("nothing was resampled to a power of two", resampled == 0);
            Truth("everything block compressed (DXT1 or BC7)", uncompressed == 0);
            Truth("nothing carries a mip chain", mipped == 0);
            Say("   streamed today, as RGBA32: " + Mb(rgba32) + "MB");
            Say("   imported, block compressed:  " + Mb(actual) + "MB   ("
                + Mb(rgba32 - actual) + "MB of VRAM the migration gives back)");
            Truth("the whole mirror resident at once fits inside the shipped 280MB "
                  + "sweep budget, so Sweep stops firing on it", actual < ShippedBudget);
            Say("");
        }

        // ══ 4 · the film ═══════════════════════════════════════════════════════

        static void Film()
        {
            Say("── 4 · THE TITLE FILM (Resources/Sheets, one home not two) ──────");
            int found = 0, wrong = 0;
            long rgba32 = 0L, actual = 0L;
            for (int i = 1; i <= FilmFrames; i++)
            {
                // exactly the key SheetLoop.BakedFrame builds
                var tex = Resources.Load<Texture2D>(string.Format("Sheets/frame_{0:00}", i));
                if (tex == null) continue;
                found++;
                rgba32 += (long)tex.width * tex.height * 4L;
                actual += ArtCache.Footprint(tex);
                if (tex.format != TextureFormat.DXT1 || tex.mipmapCount > 1) wrong++;
            }
            Truth("all " + FilmFrames + " frames answer SheetLoop.BakedFrame's own key",
                  found == FilmFrames);
            Truth("every frame is DXT1 with no mips (RGB, no alpha, 4 bits a pixel)",
                  found > 0 && wrong == 0);
            Say("   streamed today, as RGBA32: " + Mb(rgba32) + "MB");
            Say("   imported, as DXT1:          " + Mb(actual) + "MB   ("
                + Mb(rgba32 - actual) + "MB of VRAM, the biggest single win in the set)");

            string mirrored = Path.Combine(ProjectRoot(), MirrorRoot, "title/video");
            Truth("the film is NOT mirrored a second time under Resources/Art",
                  !Directory.Exists(mirrored));
            Say("");
        }

        // ══ 5 · the blocker: Sweep must not destroy a shared asset ═════════════

        static void SweepKeepsBakedAlive(List<Sample> samples)
        {
            Say("── 5 · SWEEP GIVES BAKED TEXTURES BACK, IT DOES NOT DESTROY THEM ─");

            Listen();
            _assetDestroyErrors = 0;
            int held = 0;
            foreach (Sample s in samples) if (ArtCache.IsBaked(s.Rel)) held++;
            Truth("the samples are held, and held as baked", held == samples.Count);

            // maxBytes 0 forces the loop; minAge 0 removes the live-screen guard, so
            // every one of them is evicted in this single call
            ArtCache.Sweep(0L, 0f);

            int stillHeld = 0;
            foreach (Sample s in samples) if (ArtCache.Known(s.Rel)) stillHeld++;
            Truth("the sweep actually evicted all ten", stillHeld == 0);

            // THE ASSERTION THIS PROBE EXISTS FOR. `Object.Destroy` on an asset makes
            // the editor log this exact error; `Resources.UnloadAsset` is silent.
            Truth("no asset was destroyed — Resources.UnloadAsset carried every "
                  + "eviction (" + _assetDestroyErrors + " destroy-an-asset errors)",
                  _assetDestroyErrors == 0);

            int dead = 0, changed = 0;
            foreach (Sample s in samples)
            {
                string key = "Art/" + s.Rel.Substring(0, s.Rel.Length - 4);
                var again = Resources.Load<Texture2D>(key);
                if (again == null) { dead++; Say("   DEAD AFTER SWEEP: " + s.Rel); continue; }
                if (again.width != s.W || again.height != s.H
                    || again.format != (s.Alpha ? TextureFormat.BC7 : TextureFormat.DXT1))
                {
                    changed++;
                    Say("   DAMAGED AFTER SWEEP: " + s.Rel + " → " + again.width + "x"
                        + again.height + " " + again.format);
                }
                var onDisk = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    MirrorRoot + "/" + s.Rel);
                if (onDisk == null) { dead++; Say("   ASSET GONE: " + s.Rel); }
            }
            Truth("every swept path loads again from Resources", dead == 0);
            Truth("and comes back the same size and the same format", changed == 0);

            // and it goes round again through ArtCache, on the baked route
            int before = ArtCache.BakedRoute;
            bool answered = false;
            ArtCache.Load(samples[0].Rel, t => { answered = true; });
            Truth("a swept path reloads through ArtCache, still baked, still answered "
                  + "inside the call",
                  answered && ArtCache.BakedRoute == before + 1 && ArtCache.IsBaked(samples[0].Rel));
            Deafen();
            Say("");
        }

        static void Listen()
        {
            if (_listening) return;
            Application.logMessageReceived += OnLog;
            _listening = true;
        }

        static void Deafen()
        {
            if (!_listening) return;
            Application.logMessageReceived -= OnLog;
            _listening = false;
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception) return;
            if (condition != null && condition.Contains("Destroying assets is not permitted"))
                _assetDestroyErrors++;
        }

        // ══ plumbing ═══════════════════════════════════════════════════════════

        static void Truth(string what, bool ok)
        {
            _checks++;
            if (!ok) _fails++;
            Say((ok ? "   ok   " : "   FAIL ") + what);
        }

        static void Say(string line)
        {
            _log.Append(line).Append('\n');
        }

        static string Mb(long bytes)
        {
            return (bytes / (1024f * 1024f)).ToString("0.0");
        }

        static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }
    }
}
#endif
