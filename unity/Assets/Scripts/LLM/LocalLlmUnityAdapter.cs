// ═══════════════════════════════════════════════════════════════════════════════
// THE LLMUNITY BACKEND — the half of the local pilot that needs a package and a
// 2.5 GB model, held behind a compile define so the game builds and ships without
// either. With RUNWAY_LLMUNITY unset (the default, and the state of
// ProjectSettings today) this whole file compiles to nothing.
//
// TO LIGHT IT UP, two steps and a download — see the WIRING STATUS section of
// unity/briefs/LOCAL-NARRATOR-dossier.md for the full text:
//   1. Packages/manifest.json  →  "ai.undream.llm": "https://github.com/undreamai/LLMUnity.git#v3.0.3"
//   2. Player Settings ▸ Scripting Define Symbols  →  add RUNWAY_LLMUNITY
//   3. run once with RUNWAY_LOCAL_LLM=1; the model is fetched into
//      persistentDataPath on first use and never enters the bundle.
//
// Everything below is written against LLMUnity v3.0.3 as read in the dossier's API
// mapping, with the file:line references kept inline so a signature that has moved
// can be found rather than guessed at. It has never been compiled — see
// COMPILE-RISKS L1.
// ═══════════════════════════════════════════════════════════════════════════════
#if RUNWAY_LLMUNITY
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using LLMUnity;
using Runway.App;

namespace Runway.Llm
{
    /// <summary>
    /// One loaded GGUF, one agent per tier, behind ILocalCompletion.
    ///
    /// Four decisions here are load-bearing and each one is a defect avoided:
    ///
    /// · THE MODEL LIVES IN persistentDataPath, fetched by us. LLMUnity's own runtime
    ///   download target on macOS desktop is inside the .app bundle
    ///   (Runtime/LLMUnitySetup.cs:239 → StreamingAssets → Contents/Resources/…).
    ///   Writing 2.5 GB there invalidates the code signature, and an app still under
    ///   quarantine runs from a read-only translocated mount where the write simply
    ///   fails. SetModel takes an absolute path straight through (Runtime/LLM.cs:886).
    ///
    /// · THE HOST GAMEOBJECT STAYS DISABLED until the file is on disk. LLM.Awake bails
    ///   on !enabled, and SetModel asserts the service has not started.
    ///
    /// · SYSTEM PROMPT AND GRAMMAR ARE SET ONCE, at agent construction. llama.cpp
    ///   caches the processed prefix (cachePrompt defaults true), so a fixed system
    ///   prompt on a fixed slot means the 2,206-token clarify preamble is prefilled at
    ///   warmup and every later call pays only for the user delta. Varying it throws
    ///   that away on every call.
    ///
    /// · addToHistory: false ON EVERY CALL. Our requests are stateless one-shots. Left
    ///   at the default true, every call accretes history and walks the context ceiling.
    /// </summary>
    public sealed class LocalLlmUnityAdapter : MonoBehaviour, ILocalCompletion
    {
        // ── the pilot's settings (dossier section d) ────────────────────────────

        /// Non-reasoning by construction, Apache 2.0, IFEval 83.4. Do NOT take
        /// LLMUnity's curated default: every Qwen 3.5 entry in it is a hybrid
        /// reasoning checkpoint, and constrained decoding is currently broken on that
        /// whole family (llama.cpp #20345, LLMUnity #401).
        public const string DefaultModelFile = "Qwen3-4B-Instruct-2507-Q4_K_M.gguf";

        /// Metal is opt-in and OFF by default (_numGPULayers = 0, Runtime/LLM.cs:53).
        /// Leave it at zero and every throughput number in the dossier is wrong.
        public const int GpuLayers = 999;

        /// LlamaLib emits `-fa off` whenever this bool is false, and LLMUnity defaults
        /// it to false. It is not on auto.
        public const bool FlashAttention = true;

        /// Clarify's preamble is 2,206 tokens and its user delta is a few hundred.
        /// 4096 keeps the KV cache small; SetModel clamps to what the GGUF supports.
        public const int ContextSize = 4096;

        public const int ParallelPrompts = 1;

        /// numPredict defaults to -1 (unlimited). On a background thread with a
        /// spinning UI, a looping model reads as a multi-second freeze with no error.
        public const int ClarifyPredict = LocalLlmRouter.ClarifyPredictCap;

        /// The shape is the grammar's job, not the sampler's. Low, not zero.
        public const float ClarifyTemperature = 0.2f;

        LLM _llm;
        LLMAgent _clarify;
        string _pinnedSystem = "";
        string _pinnedGrammar = "";

        bool _ready;
        string _note = "starting";
        float _downloaded;      // 0..1 while the model is coming down

        readonly Queue<Action> _toMain = new Queue<Action>();

        // ══ installation ═══════════════════════════════════════════════════════

        /// Self-installing: adding the package and the define is the whole activation,
        /// with no edit to Boot. The router asks Env for the flag itself, so an
        /// unflagged run never builds this object at all.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (Env.Get(LocalLlmRouter.Flag, "") != "1") return;
            var go = new GameObject("~localllm");
            DontDestroyOnLoad(go);
            var self = go.AddComponent<LocalLlmUnityAdapter>();
            LocalLlmRouter.Register(self);
            Debug.Log("LOCAL LLM: LLMUnity adapter installed, bringing the model up");
        }

        void Awake()
        {
            _ = Bring();
        }

        void Update()
        {
            // Chat bottoms out in Task.Run (Runtime/LlamaLib/LLMAgent.cs:271), so the
            // reply arrives on a pool thread. Callers touch Unity objects inside cb, so
            // it is marshalled HERE rather than trusted to resume on Unity's context —
            // RequestJson returns void and is started fire-and-forget.
            while (true)
            {
                Action a = null;
                lock (_toMain) { if (_toMain.Count > 0) a = _toMain.Dequeue(); }
                if (a == null) break;
                try { a(); } catch (Exception e) { Debug.Log("LOCAL LLM callback threw: " + e.Message); }
            }
        }

        void OnMain(Action a)
        {
            lock (_toMain) { _toMain.Enqueue(a); }
        }

        // ══ ILocalCompletion ═══════════════════════════════════════════════════

        public bool Ready { get { return _ready && _clarify != null; } }

        public bool Handles(string tier)
        {
            // The pilot is clarify and only clarify. `assess` is a permanent no: 20,317
            // tokens of prefill, ~2.7 GB of KV cache at the context it needs, and the
            // one judgment in the game a 4B model cannot make.
            return tier == LocalLlmRouter.PilotTier;
        }

        public string Describe()
        {
            return "LLMUnity/" + ModelFile + " — " + _note;
        }

        public void Cancel()
        {
            try { if (_clarify != null) _clarify.CancelRequests(); }   // LLMAgent.cs:408
            catch (Exception) { }
        }

        public void Complete(string systemPrompt, string userPrompt, JObject schema,
                             string tier, int maxTokens, Action<JObject> cb)
        {
            if (cb == null) return;
            if (!Ready || tier != LocalLlmRouter.PilotTier) { cb(null); return; }
            _ = Run(systemPrompt, userPrompt, schema, maxTokens, cb);
        }

        async Task Run(string systemPrompt, string userPrompt, JObject schema,
                       int maxTokens, Action<JObject> cb)
        {
            string text = null;
            try
            {
                // The prefix cache is the whole reason clarify is cheap warm. A prompt
                // that does not match what we pinned would silently throw it away, so
                // say so once rather than pay for it every week.
                if (systemPrompt != _pinnedSystem)
                    Debug.Log("LOCAL LLM: the clarify system prompt changed under a warm "
                              + "agent — the prefix cache is cold again this call");
                if (schema != null && schema.ToString() != _pinnedGrammar)
                    Debug.Log("LOCAL LLM: the clarify schema changed under a warm agent");

                _clarify.numPredict = maxTokens > 0 ? maxTokens : ClarifyPredict;
                // addToHistory:false — stateless one-shots, never a conversation.
                text = await _clarify.Chat(userPrompt, null, null, false);   // LLMAgent.cs:269
            }
            catch (Exception e)
            {
                string msg = e.Message;
                OnMain(() => { Debug.Log("LOCAL LLM chat failed: " + msg); cb(null); });
                return;
            }
            JObject data = LlmClient.TryParse(text);
            string peek = text == null ? "(nothing)"
                        : (text.Length <= 120 ? text : text.Substring(0, 120));
            OnMain(() =>
            {
                if (data == null)
                    Debug.Log("LOCAL LLM reply was not the schema'd JSON (" + peek + ")");
                cb(data);   // the router checks it against the schema before game code sees it
            });
        }

        // ══ bring-up ═══════════════════════════════════════════════════════════

        public static string ModelFile
        {
            get { return Env.Get("RUNWAY_LOCAL_LLM_MODEL", DefaultModelFile); }
        }

        /// Ours, not theirs: LLMUnity's model list is frozen into StreamingAssets at
        /// build time and every Download*/SetDownloadOnStart method is inside
        /// #if UNITY_EDITOR (Runtime/LLMManager.cs:300-641), so a player has no
        /// supported way to name a model at all.
        public static string ModelUrl
        {
            get
            {
                return Env.Get("RUNWAY_LOCAL_LLM_URL",
                    "https://huggingface.co/Qwen/Qwen3-4B-Instruct-2507-GGUF/resolve/main/"
                    + DefaultModelFile + "?download=true");
            }
        }

        /// NOT StreamingAssets. See the class comment.
        public static string ModelPath
        {
            get { return Path.Combine(Application.persistentDataPath, "models", ModelFile); }
        }

        /// 0..1 while the model is downloading, for a progress UI on the boot curtain.
        public float DownloadProgress { get { return _downloaded; } }

        async Task Bring()
        {
            try
            {
                string path = ModelPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                if (!File.Exists(path))
                {
                    _note = "downloading";
                    // The public, non-editor-gated download (Runtime/LLMUnitySetup.cs:289).
                    // Resumes across launches (Runtime/ResumingWebClient.cs:41).
                    await LLMUnitySetup.DownloadFile(ModelUrl, path, false, null,
                                                     p => { _downloaded = p; });
                }
                if (!File.Exists(path))
                {
                    _note = "no model on disk";
                    return;
                }
                _downloaded = 1f;

                // The host is built DISABLED so Awake cannot start the service before
                // SetModel has been called on it.
                var host = new GameObject("~localllm-host");
                host.transform.SetParent(transform, false);
                host.SetActive(false);
                _llm = host.AddComponent<LLM>();
                _llm.numGPULayers = GpuLayers;          // LLM.cs:118 — 0 by default
                _llm.flashAttention = FlashAttention;
                _llm.contextSize = ContextSize;         // LLM.cs:144, clamped by the GGUF
                _llm.parallelPrompts = ParallelPrompts; // LLM.cs:131
                // -1 takes every core it can and starves the render thread.
                _llm.numThreads = Math.Max(2, SystemInfo.processorCount / 2);
                _llm.SetModel(path);                    // LLM.cs:181
                host.SetActive(true);

                _note = "loading";
                await _llm.WaitUntilReady();            // LLM.cs:542
                if (!_llm.started || _llm.failed)       // LLM.cs:286,289
                {
                    _note = "the model did not start";
                    Debug.Log("LOCAL LLM: " + _note + " (" + path + ")");
                    return;
                }

                BuildClarifyAgent();
                _note = "ready";
                Debug.Log("LOCAL LLM: " + Describe());
            }
            catch (Exception e)
            {
                _note = "bring-up failed: " + e.Message;
                Debug.Log("LOCAL LLM: " + _note);
            }
        }

        /// One agent, one slot, one system prompt, one grammar — all pinned here and
        /// never touched again. `opts.Tier` selects WHICH agent; a second tier is a
        /// second one of these and about 25 lines.
        void BuildClarifyAgent()
        {
            string sys = RunwayPaths.ReadAllTextOrEmpty(RunwayPaths.Streaming("prompts/clarify.txt"));
            string grammar = LlmClient.ClarifySchema.ToString();

            var go = new GameObject("~localllm-clarify");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            _clarify = go.AddComponent<LLMAgent>();
            _clarify.llm = _llm;
            _clarify.systemPrompt = sys;                 // LLMAgent.cs:69, set ONCE
            // SetGrammar accepts "GBNF or JSON schema format" (LLMClient.cs:424) and
            // llama.cpp runs its own json-schema-to-grammar over anything that parses as
            // JSON. So our schemas go across exactly as they are: no conversion layer,
            // no grammar file, one ToString().
            _clarify.grammar = grammar;                  // LLMClient.cs:190,425
            _clarify.numPredict = ClarifyPredict;        // LLMClient.cs:58
            _clarify.temperature = ClarifyTemperature;
            go.SetActive(true);

            _pinnedSystem = sys;
            _pinnedGrammar = grammar;
            _ready = true;

            // Prefill the 2,206-token preamble now, with numPredict 0, so the first real
            // call is warm (LLMAgent.cs:308). Belongs on the boot curtain.
            _ = Warm();
        }

        async Task Warm()
        {
            try { await _clarify.Warmup(); _note = "ready, warm"; }
            catch (Exception e) { Debug.Log("LOCAL LLM warmup failed (harmless): " + e.Message); }
        }
    }
}
#endif
