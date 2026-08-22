using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Runway.App;

namespace Runway.Llm
{
    /// Where the week's painting has got to. The screens POLL this rather than being
    /// called back, because the book, the beat and the room all read it differently.
    public enum PaintStatus
    {
        Idle,
        Painting,
        Done,
        Failed,
    }

    /// <summary>
    /// THE GENERATIVE SCENE PIPELINE — scene_director.gd's v2 path, ported.
    ///
    /// One call per staged beat: the scene is GENERATED for this exact week — place,
    /// condition, cast and the move itself — instead of resolved from a library and
    /// composited. Reference images plus an instruction JSON with an over-specified
    /// background frame. Hidden entirely under the reading beat.
    ///
    /// THE STATUS SURFACES ARE PART OF THE CONTRACT. Every generated scene carries a
    /// blank whiteboard upper-left and a pinned blank sheet upper-right: the game then
    /// WRITES the week's numbers onto them in the founder's hand. We told the model
    /// where to put them, so we know where they are — the contract IS the annotation,
    /// no detection required.
    ///
    /// THE KEY CARRIES THE WEEK. The old place|cond|cast key returned the SAME composed
    /// image for every similar week across ALL runs — the owner's "there is never a new
    /// image that generates". The cache now only serves a same-week retry.
    ///
    /// THE RENDER GETS THREE CHANCES. One middleware 502 left week after week on the
    /// stock room; a transient upstream error must never cost the picture.
    /// </summary>
    public sealed class SceneDirector : MonoBehaviour
    {
        public const string MiddlewareEdit =
            "https://nano-banana-production-e03b.up.railway.app/edit-image-openai";
        public const string MiddlewareGen =
            "https://nano-banana-production-e03b.up.railway.app/generate-image-openai";

        /// 0..1, for the reading beat's pen stroke.
        public event Action<float> Progress;
        /// the composed scene is on disk
        public event Action<string> Ready;
        public event Action<string> Failed;

        public const string CharacterLaw =
            "EVERY person in this world — the cast, customers, strangers, "
            + "officials, passers-by — is the SAME species: a solid ink-black bean blob with two "
            + "blank white oval eyes (left bigger), one ink cowlick, tiny cream sneakers, NO pupils, "
            + "NO mouth, NO clothing. NEVER draw a realistic human. The cast are identified by their "
            + "carried props exactly as in the reference images; strangers get incidental props.";

        public const string StyleLaw =
            "Hand-drawn wobbly felt-pen ink, flat fills, no gradients. Palette only: "
            + "cream #F2EAD3, ink #1E1E1E, coral #E86A5C, yellow #F4B942, sage #8FA582, blue #6E8CA0, white.";

        public const string FrameLaw =
            "One wide establishing shot, camera at standing eye height, level. "
            + "The room reads at a glance. Top tenth and bottom seventh of the frame stay calm and "
            + "uncluttered. A BLANK whiteboard hangs on the left wall in the upper-left quarter of "
            + "the frame; a BLANK pinned paper sheet hangs in the upper-right quarter. Both stay "
            + "completely blank: no text, no numbers, no letters anywhere in the image.";

        const string RegistryFile = "registry.json";

        /// the request's own ceiling; the manual wall below is what catches a HANG
        const int RequestTimeoutSeconds = 180;
        const float RequestWallSeconds = 200f;
        const int DownloadTimeoutSeconds = 120;
        const float DownloadWallSeconds = 150f;

        JObject _registry;
        readonly HashSet<string> _inflight = new HashSet<string>();

        // ── the paint gate ─────────────────────────────────────────────────────

        /// day one's warm painting: the book holds the reader while this is Painting.
        public PaintStatus WarmStatus { get; private set; }
        public string WarmName { get; private set; }

        /// bumped by any cancel; orphans everything in flight
        public int TurnSeq { get; private set; }

        /// The last finished render, and how far the one in flight has got.
        public string ScenePath { get; private set; }
        public float SceneProgress { get; private set; }

        public void Setup()
        {
            WarmStatus = PaintStatus.Idle;
            WarmName = "";
            ScenePath = "";
            LoadRegistry();
            Debug.Log("RUNWAY! scene director ready · cache " + RunwayPaths.GenScenesDir);
        }

        /// NOTHING IN FLIGHT SURVIVES THE END OF A RUN. Bumping the sequence orphans the
        /// render and anything the director still has in the air.
        public void CancelTurn()
        {
            TurnSeq++;
            ScenePath = "";
            SceneProgress = 0f;
            WarmStatus = PaintStatus.Idle;
            WarmName = "";
        }

        // ══ the warm render ════════════════════════════════════════════════════

        /// THE PAINT STARTS AT THE SIGNATURE (owner): the moment day one is written, its
        /// room starts rendering — while the book is still being read. The turn's own
        /// call later coalesces onto this warm render through the per-week cache key.
        public void WarmScene(JObject scene, JArray cast, string[] castUrls, string beat,
                              string outName, JObject company)
        {
            if (scene == null || scene.Count == 0) return;
            var boot = Boot.Instance;
            if (boot != null && !boot.ArtEnabled) return;
            if (Env.Get("RUNWAY_GPT_SCENES", "") == "0") return;
            Debug.Log("TURN art WARM start (" + outName + ")");
            WarmStatus = PaintStatus.Painting;
            WarmName = outName;
            MakeSceneV2(scene, cast, castUrls, beat, outName, company);
        }

        // ══ the whole week in one image ════════════════════════════════════════

        /// `scene` is the DM's own staging: novel_place preferred, else the place
        /// phrase; `condition` dresses it; `cast` acts in it.
        public void MakeSceneV2(JObject scene, JArray cast, string[] castUrls, string beat,
                                string outName, JObject company = null)
        {
            StartCoroutine(MakeSceneV2Routine(scene, cast, castUrls, beat, outName, company));
        }

        IEnumerator MakeSceneV2Routine(JObject scene, JArray cast, string[] castUrls,
                                       string beat, string outName, JObject company)
        {
            int seq = TurnSeq;   // a cancel between here and the answer orphans it
            LoadRegistry();
            string desc = EventGenerator.Str(scene, "novel_place").Trim();
            if (desc.Length == 0)
                desc = EventGenerator.Str(scene, "place", "a small startup workspace").Replace("_", " ");
            string cond = EventGenerator.Str(scene, "condition", "steady");

            string key = string.Format("{0}|{1}|{2}|{3}", outName,
                EventGenerator.Left(desc, 60), cond, CastSig(cast));

            string cached = CachedPath(key);
            if (cached.Length > 0)
            {
                Report(1f);
                Announce(cached, outName, seq);
                yield break;
            }

            // a warm prefetch may already be painting this exact scene: wait for it
            // instead of paying twice, then serve its result
            if (_inflight.Contains(key))
            {
                while (_inflight.Contains(key)) yield return null;
                string cached2 = CachedPath(key);
                if (cached2.Length > 0)
                {
                    Report(1f);
                    Announce(cached2, outName, seq);
                }
                else
                {
                    Fail("v2 generation failed (warm attempt)", seq);
                }
                yield break;
            }
            _inflight.Add(key);

            string dressing = Dressing(cond);

            var roster = new JArray();
            if (cast != null)
            {
                for (int i = 0; i < cast.Count; i++)
                {
                    var c = cast[i] as JObject;
                    string who = EventGenerator.Str(c, "role");
                    if (who.Length == 0) who = EventGenerator.Str(c, "who", "founder");
                    roster.Add(new JObject
                    {
                        ["place"] = string.Format("reference image {0}", i + 1),
                        ["who"] = who,
                        ["doing"] = EventGenerator.Str(c, "doing", "at work"),
                    });
                }
            }

            // THE TRADE IS IN THE PICTURE. A spa's week happens among towels and
            // treatment tables; a drone company's among props and battery crates.
            // Without this the model defaulted every scene to generic desks.
            string trade = "";
            if (company != null && company.Count > 0)
                trade = string.Format(
                    " The company in this scene: {0} — {1} ({2} for {3}). The room visibly belongs to THIS trade: its tools, stock and props are present and specific.",
                    EventGenerator.Str(company, "name"), EventGenerator.Str(company, "idea"),
                    EventGenerator.Str(company, "what"), EventGenerator.Str(company, "who"));

            var instr = new JObject
            {
                ["task"] = "draw one finished game scene",
                ["scene"] = desc + ". " + dressing + trade
                            + (string.IsNullOrEmpty(beat) ? "" : " This week: " + beat),
                ["cast"] = roster,
                ["character_law"] = CharacterLaw,
                ["style"] = StyleLaw,
                ["frame"] = FrameLaw,
                ["must_hold"] = new JArray
                {
                    "every referenced character appears exactly once, integrated into the room's own light with a soft contact shadow",
                    "no text, numbers or letters anywhere in the image",
                    "the whiteboard and the pinned sheet stay blank",
                },
            };

            var body = new JObject
            {
                ["prompt"] = instr.ToString(Formatting.None),
                ["quality"] = "medium",
                ["size"] = "1536x1024",
                ["output_format"] = "png",
            };
            string endpoint = MiddlewareGen;
            if (castUrls != null && castUrls.Length > 0)
            {
                body["referenceImages"] = new JArray(castUrls);
                endpoint = MiddlewareEdit;
            }
            Report(0.05f);

            // THE RENDER GETS THREE CHANCES. Backoff 3s, then 8s.
            string path = "";
            for (int attempt = 0; attempt < 3; attempt++)
            {
                yield return MiddlewareCall(endpoint, body, outName, p => path = p);
                if (path.Length > 0) break;
                Debug.Log(string.Format("SceneDirector v2: attempt {0} failed{1}",
                    attempt + 1, attempt < 2 ? " — retrying" : " — giving up"));
                if (attempt < 2)
                    yield return new WaitForSecondsRealtime(3f + 5f * attempt);
            }

            if (path.Length == 0)
            {
                _inflight.Remove(key);
                Fail("v2 generation failed after 3 attempts", seq);
                yield break;
            }
            Remember(key, path);
            _inflight.Remove(key);
            Announce(path, outName, seq);
        }

        static string Dressing(string cond)
        {
            switch (cond)
            {
                case "thriving": return "The place is well kept: good kit, full shelves, a sense of money.";
                case "steady": return "The place is lived in and ordinary.";
                case "in_the_red": return "The place is fraying: bare shelves, an unpaid notice, a dead plant, litter.";
            }
            return "";
        }

        static string CastSig(JArray cast)
        {
            if (cast == null) return "";
            var bits = new List<string>();
            foreach (JToken t in cast)
            {
                var c = t as JObject;
                string who = EventGenerator.Str(c, "who");
                if (who.Length == 0) who = EventGenerator.Str(c, "role", "?");
                bits.Add(who);
            }
            return string.Join("+", bits.ToArray());
        }

        // ══ the middleware ═════════════════════════════════════════════════════

        /// THE ONE KEY, WHEREVER IT LIVES: the player's key is in the user folder, which
        /// the env layering puts over the dev .env — the renderer must read the SAME
        /// stack the narrator does, never its own private path.
        static string OpenAiKey()
        {
            return Env.Get("OPENAI_API_KEY", "").Trim();
        }

        /// One middleware round-trip: the response carries imageUrl directly (no
        /// polling). Hands back the downloaded local path, or "" on any failure — the
        /// caller owns the fallback, and a failure costs a picture, never a turn.
        IEnumerator MiddlewareCall(string endpoint, JObject body, string outName,
                                   Action<string> onDone)
        {
            string okey = OpenAiKey();
            if (okey.Length == 0)
            {
                Debug.Log("SceneDirector v2: no OpenAI key — art is off, the game runs on "
                          + "the reading beat alone");
                onDone("");
                yield break;
            }

            var req = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(body.ToString(Formatting.None)));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("x-openai-api-key", okey);
            req.timeout = RequestTimeoutSeconds;

            Report(0.2f);

            // THE WATCHDOG (owner live: a request hung PAST its own 180s timeout with no
            // completion and no error — the ladder never advanced). The wait is raced
            // against a hard 200s wall; a silent hang becomes a failed attempt.
            UnityWebRequestAsyncOperation op = req.SendWebRequest();
            float waited = 0f;
            while (!op.isDone && waited < RequestWallSeconds)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                waited += 0.5f;
            }
            if (!op.isDone)
            {
                Debug.Log(string.Format("SceneDirector v2: request HUNG {0}s — cancelled", (int)waited));
                req.Abort();
                yield return null;
                req.Dispose();
                onDone("");
                yield break;
            }

            long code = req.responseCode;
            string bodyTxt = "";
            try { bodyTxt = req.downloadHandler != null ? req.downloadHandler.text : ""; }
            catch (Exception) { bodyTxt = ""; }
            req.Dispose();

            if (code < 200L || code >= 300L)
            {
                Debug.Log(string.Format("SceneDirector v2: middleware {0} — {1}",
                    code, EventGenerator.Left(bodyTxt, 160)));
                onDone("");
                yield break;
            }
            JObject parsed = LlmClient.TryParse(bodyTxt);
            if (parsed == null) { onDone(""); yield break; }
            string url = EventGenerator.Str(parsed, "imageUrl");
            if (url.Length == 0)
            {
                Debug.Log("SceneDirector v2: no imageUrl — " + EventGenerator.Left(bodyTxt, 160));
                onDone("");
                yield break;
            }

            Report(0.75f);

            string outPath = Path.Combine(RunwayPaths.GenScenesDir, outName + ".png");
            var dl = UnityWebRequest.Get(url);
            var handler = new DownloadHandlerFile(outPath);
            handler.removeFileOnAbort = true;
            dl.downloadHandler = handler;
            dl.timeout = DownloadTimeoutSeconds;

            UnityWebRequestAsyncOperation dop = dl.SendWebRequest();
            float dlWaited = 0f;
            while (!dop.isDone && dlWaited < DownloadWallSeconds)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                dlWaited += 0.5f;
            }
            if (!dop.isDone)
            {
                Debug.Log("SceneDirector v2: download HUNG — cancelled");
                dl.Abort();
                yield return null;
                dl.Dispose();
                onDone("");
                yield break;
            }
            long dcode = dl.responseCode;
            bool dok = dl.result == UnityWebRequest.Result.Success;
            dl.Dispose();

            bool onDisk = false;
            try { onDisk = File.Exists(outPath) && new FileInfo(outPath).Length > 4096L; }
            catch (Exception) { onDisk = false; }

            if (!dok || dcode < 200L || dcode >= 300L || !onDisk)
            {
                onDone("");
                yield break;
            }
            Report(1f);
            onDone(outPath);
        }

        // ══ the registry ═══════════════════════════════════════════════════════

        void LoadRegistry()
        {
            if (_registry != null) return;
            string txt = RunwayPaths.ReadAllTextOrEmpty(
                Path.Combine(RunwayPaths.GenScenesDir, RegistryFile));
            _registry = LlmClient.TryParse(txt) ?? new JObject();
        }

        string CachedPath(string key)
        {
            LoadRegistry();
            var hit = _registry[key] as JObject;
            if (hit == null) return "";
            string p = EventGenerator.Str(hit, "path");
            try { return p.Length > 0 && File.Exists(p) ? p : ""; }
            catch (Exception) { return ""; }
        }

        void Remember(string key, string path)
        {
            LoadRegistry();
            _registry[key] = new JObject { ["path"] = path };
            RunwayPaths.WriteAllText(Path.Combine(RunwayPaths.GenScenesDir, RegistryFile),
                                     _registry.ToString(Formatting.None));
        }

        // ══ reporting ══════════════════════════════════════════════════════════

        /// Progress never runs backwards: the director reports the novel-room generation
        /// and the compose on one channel, and the second stage restarts its own count.
        /// A pen that goes back down reads as a bug.
        void Report(float f)
        {
            SceneProgress = Mathf.Max(SceneProgress, Mathf.Clamp01(f));
            var p = Progress;
            if (p != null) p(SceneProgress);
        }

        void Announce(string path, string outName, int seq)
        {
            if (WarmStatus == PaintStatus.Painting && WarmName.Length > 0
                && path.Contains(WarmName))
                WarmStatus = PaintStatus.Done;
            if (seq != TurnSeq) return;   // belongs to a run that has already ended
            Debug.Log("TURN art landed: " + path);
            ScenePath = path;
            var r = Ready;
            if (r != null) r(path);
        }

        /// A FAILED RENDER IS A COSMETIC LOSS. The previous room stays, the week
        /// continues, and the only trace is a line in the log for whoever is watching.
        void Fail(string reason, int seq)
        {
            if (WarmStatus == PaintStatus.Painting) WarmStatus = PaintStatus.Failed;
            if (seq != TurnSeq) return;
            Debug.Log("TURN art FAILED: " + reason);
            Debug.Log("RUNWAY! scene skipped (" + reason + ") — keeping the previous room");
            var f = Failed;
            if (f != null) f(reason);
        }
    }
}
