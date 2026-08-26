using System;
using System.Collections;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Runway.App;

namespace Runway.Llm
{
    /// <summary>
    /// THE BINDER PORTRAIT (DECISIONS § THE BINDER PORTRAIT) — portrait_client.gd's
    /// twin. One transparent PNG of the company's own well-used ring binder,
    /// generated ONCE at run start and regenerated only on a rename or a
    /// nature-changing pivot.
    ///
    /// PURE TRANSPORT: payload in, JObject out — this lane never sees a
    /// Runway.Core type. The label in the image is BLANK on purpose (image
    /// models garble text); the binder screen overlays the company name. The
    /// drawn kraft cover is the instant placeholder and the permanent fallback —
    /// the binder never waits on this call, and a failure costs a picture,
    /// never a turn.
    ///
    /// THE MODEL LADDER (the "gpt image 2" ruling): ask for "gpt-image-2"
    /// first; if the API reports no such model, fall back to "gpt-image-1".
    /// Any OTHER failure does not burn the second model — same account, same
    /// request, the retry would fail the same way.
    /// </summary>
    public sealed class PortraitClient : MonoBehaviour
    {
        public const string OutFileName = "binder_portrait.png";
        public const string ImagesUrl = "https://api.openai.com/v1/images/generations";
        public static readonly string[] Models = { "gpt-image-2", "gpt-image-1" };

        /// The look (owner-amended): a NICE 3D-ILLUSTRATED object — soft-shaded,
        /// gently dimensional, a chunky real binder prop — still inside the game
        /// palette and its hand-drawn world. The label stays BLANK; the four
        /// index tabs are the divider-group colors; the background transparent.
        public const string PROMPT =
            "A single chunky, well-used ring binder as a game prop, "
            + "seen straight on, slightly three-quarter: soft-shaded 3D illustration, "
            + "gentle volume and a soft drop shadow baked only under the object "
            + "itself, clean silhouette, flat-color palette. Kraft-brown cardboard "
            + "cover, visibly used at the corners. FOUR thick index tabs sticking out "
            + "of the page edge, top to bottom: green sage #8FA582, coral red "
            + "#E86A5C, muted blue #6E8CA0, warm yellow #F4B942. Untidy papers "
            + "poking out unevenly between the covers. A BLANK taped paper label on "
            + "the front cover — completely blank, nothing written on it. Palette "
            + "only: ink #1E1E1E, coral #E86A5C, yellow #F4B942, sage #8FA582, blue "
            + "#6E8CA0, cream #F2EAD3. No gradients except the soft shading, no "
            + "text anywhere, no letters, no numbers, no logos. Transparent "
            + "background: nothing behind or around the binder at all.";

        const int RequestTimeoutSeconds = 240;
        const float RequestWallSeconds = 260f;
        const float DownloadWallSeconds = 150f;

        bool _inflight;

        public static string OutPath { get { return RunwayPaths.User(OutFileName); } }

        /// Fire the portrait. payload (all optional): {"force": bool}. cb gets
        /// {"path": "..."} on success, null on failure. Cached: an existing PNG
        /// answers immediately; force regenerates (rename / pivot). Coalesced:
        /// a second call while one is painting is dropped — the binder polls
        /// the file, so nobody is left waiting.
        public void Generate(JObject payload, Action<JObject> cb)
        {
            bool force = payload != null && payload.Value<bool?>("force") == true;
            if (!force && File.Exists(OutPath))
            {
                if (cb != null) cb(new JObject { ["path"] = OutPath });
                return;
            }
            if (_inflight) return;
            string key = Env.Get("OPENAI_API_KEY", "").Trim();
            if (key.Length == 0)
            {
                if (cb != null) cb(null);
                return;
            }
            _inflight = true;
            StartCoroutine(Ladder(key, cb));
        }

        IEnumerator Ladder(string key, Action<JObject> cb)
        {
            foreach (string model in Models)
            {
                string verdict = "";
                yield return Attempt(model, key, v => verdict = v);
                if (verdict == "ok")
                {
                    _inflight = false;
                    if (cb != null) cb(new JObject { ["path"] = OutPath });
                    yield break;
                }
                if (verdict != "model_missing") break;
            }
            _inflight = false;
            if (cb != null) cb(null);
        }

        /// One request against one model. Hands back "ok" | "model_missing" | "failed".
        IEnumerator Attempt(string model, string key, Action<string> onDone)
        {
            var body = new JObject
            {
                ["model"] = model,
                ["prompt"] = PROMPT,
                ["background"] = "transparent",
                ["output_format"] = "png",
                ["size"] = "1024x1024",
                ["quality"] = "medium",
                ["n"] = 1,
            };
            var req = new UnityWebRequest(ImagesUrl, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(body.ToString(Formatting.None)));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + key);
            req.timeout = RequestTimeoutSeconds;

            // the render ladder's lesson: the request's own clock has slept
            // through wedged sockets — race it against a hard wall so a silent
            // hang becomes a failed attempt, never a portrait that never lands
            UnityWebRequestAsyncOperation op = req.SendWebRequest();
            float waited = 0f;
            while (!op.isDone && waited < RequestWallSeconds)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                waited += 0.5f;
            }
            if (!op.isDone)
            {
                Debug.Log(string.Format("PortraitClient: request HUNG {0}s — cancelled", (int)waited));
                req.Abort();
                yield return null;
                req.Dispose();
                onDone("failed");
                yield break;
            }
            long code = req.responseCode;
            string bodyTxt = "";
            try { bodyTxt = req.downloadHandler != null ? req.downloadHandler.text : ""; }
            catch (Exception) { bodyTxt = ""; }
            req.Dispose();

            if (code < 200L || code >= 300L)
            {
                Debug.Log(string.Format("PortraitClient: {0} -> HTTP {1} — {2}",
                    model, code, EventGenerator.Left(bodyTxt, 200)));
                onDone(LooksLikeMissingModel(code, bodyTxt) ? "model_missing" : "failed");
                yield break;
            }
            JObject parsed = LlmClient.TryParse(bodyTxt);
            var data = parsed != null ? parsed["data"] as JArray : null;
            if (data == null || data.Count == 0) { onDone("failed"); yield break; }
            var first = data[0] as JObject;
            string b64 = first != null ? EventGenerator.Str(first, "b64_json") : "";
            if (b64.Length > 0)
            {
                byte[] bytes;
                try { bytes = Convert.FromBase64String(b64); }
                catch (Exception) { onDone("failed"); yield break; }
                onDone(SavePng(bytes) ? "ok" : "failed");
                yield break;
            }
            string url = first != null ? EventGenerator.Str(first, "url") : "";
            if (url.Length == 0) { onDone("failed"); yield break; }
            yield return Download(url, onDone);
        }

        /// "no such model" wears several coats; every one mentions the model.
        /// Any other 4xx (quota, org verification) must NOT read as missing.
        static bool LooksLikeMissingModel(long code, string bodyTxt)
        {
            if (code != 400L && code != 404L) return false;
            string low = (bodyTxt ?? "").ToLowerInvariant();
            if (low.Contains("model_not_found")) return true;
            return low.Contains("model") && (low.Contains("not found")
                || low.Contains("does not exist") || low.Contains("unknown")
                || low.Contains("invalid model"));
        }

        IEnumerator Download(string url, Action<string> onDone)
        {
            var dl = UnityWebRequest.Get(url);
            dl.timeout = 120;
            UnityWebRequestAsyncOperation op = dl.SendWebRequest();
            float waited = 0f;
            while (!op.isDone && waited < DownloadWallSeconds)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                waited += 0.5f;
            }
            if (!op.isDone)
            {
                dl.Abort();
                yield return null;
                dl.Dispose();
                onDone("failed");
                yield break;
            }
            long code = dl.responseCode;
            byte[] bytes = null;
            try { bytes = dl.downloadHandler != null ? dl.downloadHandler.data : null; }
            catch (Exception) { bytes = null; }
            dl.Dispose();
            if (code < 200L || code >= 300L || bytes == null) { onDone("failed"); yield break; }
            onDone(SavePng(bytes) ? "ok" : "failed");
        }

        /// A partial body written as a file is how truncated art shipped before:
        /// a PNG that does not open with the PNG magic and end in IEND is not a PNG.
        static bool SavePng(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4096) return false;
            if (bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47)
                return false;
            string tail = Encoding.ASCII.GetString(bytes, bytes.Length - 8, 4);
            if (tail != "IEND") return false;
            try
            {
                File.WriteAllBytes(OutPath, bytes);
                return true;
            }
            catch (Exception) { return false; }
        }
    }
}
