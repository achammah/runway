# LOCAL-NARRATOR: integration dossier for LLMUnity

Assessment of https://github.com/undreamai/LLMUnity as an offline generation
backend for keyless players. Scope: can a local GGUF model stand in behind our
existing `RequestJson` seam so a keyless run still GENERATES its world, its
foundings and its clarify questions instead of dealing an authored deck.

Source read: LLMUnity v3.0.3 (shallow clone), Apache 2.0, bundling llama.cpp
(MIT) via LlamaLib v2.0.5 (llama.cpp b8209). Package `ai.undream.llm`, minimum
Unity 2022.3.16f1, single dependency `com.unity.nuget.newtonsoft-json` 3.0.2.
We run Unity 6000.0.82f1 with Newtonsoft 3.2.1 and no asmdefs, so its types
land in Assembly-CSharp with zero assembly wiring.

## Verdict first

GO, but only for the cheap tiers, and the reason is a number nobody had
written down yet.

| Call site | System prompt | Typical output | Local feasibility |
|---|---|---|---|
| `Clarify` | 2,206 tok (`clarify.txt`) | ~30 tok | Comfortable |
| `GenerateWorld` | 282 tok (const) | ~800 tok | Comfortable |
| `GenerateArcs` | 230 tok (const) | ~600 tok | Comfortable |
| event card (`SYSTEM_PROMPT`) | 479 tok (const) | ~250 tok | Comfortable |
| `Adjudicate` | **20,317 tok** (`adjudicator.txt`) | ~700 tok | **No** |

Four of our five call sites carry a system prompt under 2,300 tokens, three of
them under 500. Exactly one is enormous, and it is an order of magnitude past
the rest. It is also the one tier a small model could never carry anyway:
adjudication is the deepest judgment in the game, the one we deliberately give
terra. So the expensive tier and the impossible tier are the same tier, and
everything the keyless player is actually missing is cheap.

Recommendation: ship a clarify only pilot to prove the machinery, then take
worldgen as the first tier that a player can feel. Never route `Adjudicate`
locally. Details in section (d).

## (a) Exact integration shape

### The seam we already have

`unity/Assets/Scripts/LLM/LlmClient.cs` exposes one entry point:

```csharp
public void RequestJson(string systemPrompt, string userPrompt, JObject schema,
                        Action<JObject> cb, LlmOptions opts)
```

It is callback based, never blocks, and each call is independent. `LlmOptions`
carries `Tier` ("assess" or "clarify"), `Director` and `MaxTokens`. `Enabled`
gates everything: `EventGenerator.Live` is `Llm != null && Llm.Enabled`, and
when it is false the game already falls back to authored content. That gate is
the whole reason this experiment is cheap: a local backend does not need new
fallback paths, it needs to make `Enabled` true without a key.

Consumers are only two files. `Boot.cs:149` builds it
(`Llm = svc.AddComponent<LlmClient>()`) and `EventGenerator` holds it in a
field and calls it from six sites. `SceneDirector` touches only the static
`LlmClient.TryParse`, so it is untouched.

### The shape

`LlmClient` is `sealed`, so a subclass is out. Extract a small interface and
let both backends implement it.

```csharp
// NEW unity/Assets/Scripts/LLM/ILlmClient.cs
public interface ILlmClient
{
    bool Enabled { get; }
    string Describe();                       // Boot's one line log
    void RequestJson(string systemPrompt, string userPrompt, JObject schema,
                     Action<JObject> cb);
    void RequestJson(string systemPrompt, string userPrompt, JObject schema,
                     Action<JObject> cb, LlmOptions opts);
}
```

`LlmClient` already exposes `Enabled` as a property, so it satisfies the
interface by adding `: ILlmClient` plus a three line `Describe()`. `Provider`
and `Model` are public fields and fields cannot satisfy an interface, which is
why `Describe()` exists rather than exposing them. `Boot.Llm` and
`EventGenerator.Llm` change type from `LlmClient` to `ILlmClient`, and `Boot`
picks the backend behind the flag:

```csharp
if (Env.Get("RUNWAY_LOCAL_LLM", "") == "1")
    Llm = svc.AddComponent<LocalLlmClient>();       // NEW
else
    Llm = svc.AddComponent<LlmClient>();
Llm.Setup(...);
```

Smaller blast radius alternative if touching `Boot` and `EventGenerator` is
unwanted: keep the concrete type and add a `LocalLlmClient` delegate field
inside `LlmClient.RequestJson`, so only `LlmClient.cs` changes. It works, but
it puts experimental local code inside the shipped network client, which is the
file most exposed to a live run. The interface is cleaner and costs 16 edited
lines total.

### Which LLMUnity calls map to it

LLMUnity splits the model host from the callers. One `LLM` MonoBehaviour owns
the loaded GGUF; N `LLMAgent` components each get their own native handle,
their own slot, their own system prompt, their own grammar and their own
sampling parameters. That per agent isolation is what lets one loaded model
serve several of our tiers at once.

| Our seam | LLMUnity call | Reference |
|---|---|---|
| the model, once | `LLM` component: `SetModel(path)`, `numGPULayers`, `contextSize`, `parallelPrompts` | `Runtime/LLM.cs:181,118,144,131` |
| model present? | `await LLM.WaitUntilModelSetup(Action<float> progress)` | `Runtime/LLM.cs:560` |
| ready? | `await llm.WaitUntilReady()`, then `llm.started && !llm.failed` | `Runtime/LLM.cs:542,286,289` |
| `Enabled` | the above, plus the schema assigned | |
| one tier | one `LLMAgent` component | `Runtime/LLMAgent.cs:20` |
| `systemPrompt` arg | `agent.systemPrompt` set ONCE at setup | `Runtime/LLMAgent.cs:69` |
| `schema` arg | `agent.grammar = schema.ToString()` set ONCE at setup | `Runtime/LLMClient.cs:190,425` |
| `userPrompt` arg | `await agent.Chat(user, null, null, addToHistory: false)` | `Runtime/LLMAgent.cs:269` |
| `opts.MaxTokens` | `agent.numPredict` | `Runtime/LLMClient.cs:58` |
| `opts.Tier` | selects WHICH agent instance | |
| `Action<JObject> cb` | `JObject.Parse(result)` on the awaited string | |
| the 90s watchdog | `agent.CancelRequests()` plus our own coroutine clock | `Runtime/LLMAgent.cs:408` |

Two details carry most of the value.

`addToHistory: false` is mandatory. Our calls are stateless one shots, not a
conversation. Left at the default `true`, every call would accrete history and
walk into the context ceiling.

The system prompt must be set once and never varied, because llama.cpp caches
the processed prefix (`cachePrompt` defaults true, `Runtime/LLMClient.cs:62`).
With a fixed system prompt on a fixed slot, the 2,206 token clarify preamble is
prefilled once at warmup and every later call only pays for the small user
delta. `LLMAgent.Warmup()` (`Runtime/LLMAgent.cs:308`) exists precisely to do
that pass at load time, with `numPredict = 0`. Call it during the boot curtain.

Threading: `Chat` bottoms out in `Task.Run` (`Runtime/LlamaLib/LLMAgent.cs:271`),
as does model load (`Runtime/LLM.cs:504`). Inference does not occupy the main
thread. Covered further in section (c).

### Grammar: pass our schemas straight through

`SetGrammar(string)` accepts "GBNF or JSON schema format"
(`Runtime/LLMClient.cs:424`), and the editor's Load Grammar button filters on
`json,gbnf` (`Editor/LLMClientEditor.cs:37`). The dispatch is not a guess: in
LlamaLib `src/LLM.cpp:243-252`, if the string parses as JSON it is forwarded as
a `json_schema`, otherwise it is treated as raw GBNF. llama.cpp then runs its
own `json-schema-to-grammar` converter over it.

**So the integration needs no conversion layer at all.** `LlmClient` already
holds each schema as a `JObject`; `agent.grammar = schema.ToString()` is the
whole mapping. Every keyword our five schemas use is supported by that
converter:

| We use | Supported | Note |
|---|---|---|
| `type`, `properties`, `required` | yes | |
| `additionalProperties: false` | yes | llama.cpp defaults it to false regardless |
| `enum` | yes | |
| `maxLength` on strings | yes | only when `"type": "string"` is explicit, which ours always is |
| `minItems` / `maxItems` | yes | via bounded repetition |
| `minimum` / `maximum` | yes, integers only | ours are on `dc` and `weeks`, both integer, so fine |
| `["number","string"]` union | yes | the `v` field in our effects |
| nested objects and arrays | yes | |

Four behaviours to design around, none of them blocking:

1. **Key order is fixed** to schema declaration order. Harmless for us, since
   `JObject.Parse` does not care, but it does mean the model writes fields in
   our declared order. Worth a glance at `AdjudicateSchema`, where `narration`
   is declared before `verdict` and `roll`, so the model commits to prose
   before it commits to the judgment behind it. On a small model that ordering
   is actively harmful, which is one more reason that tier stays remote.
2. **The schema is never injected into the prompt.** llama.cpp constrains the
   output without telling the model what the shape is. Our prompts were written
   against hosted APIs that behave the same way and already describe their
   output, so this is mostly covered, but it should be verified per tier.
3. Because llama.cpp samples optimistically (sample, check the one token,
   only re-scan the whole vocabulary on rejection), grammar overhead is
   proportional to how often the model disagrees with the schema. Describing
   the shape in the prompt is therefore a **speed** optimisation as much as a
   quality one.
4. `pattern` short circuits `maxLength` if both are present. We use no
   `pattern`, so this is only a note for future schema edits.

Hand written GBNF stays available as an escape hatch if we ever need a
constraint the converter drops, but the pilot should not spend a line on it.

#### Worked example: the CLARIFY schema

Ours (`LlmClient.cs:169`):

```json
{ "type": "object", "additionalProperties": false,
  "required": ["needs_clarification", "question", "kind"],
  "properties": {
    "needs_clarification": {"type": "boolean"},
    "question": {"type": "string", "maxLength": 90},
    "kind": {"type": "string",
             "enum": ["amount","target","resource","price","other"]} } }
```

That object, serialised with `ToString()`, is what we assign to
`agent.grammar`. Nothing else. For the pilot the integration cost of the schema
is one line.

It is still worth knowing what llama.cpp turns it into, both to reason about
what is actually enforced and because this is the escape hatch form if the
converter ever disappoints. Roughly:

```gbnf
# RUNWAY! clarify. Key order is FIXED: a grammar cannot say "these three keys
# in any order" without an exponential alternation, and JObject.Parse does not
# care about order anyway. additionalProperties:false and required are both
# satisfied by construction, because no other production exists.

root ::= "{" ws
           "\"needs_clarification\"" ws ":" ws boolean ws "," ws
           "\"question\""            ws ":" ws question ws "," ws
           "\"kind\""                ws ":" ws kind    ws
         "}"

boolean  ::= "true" | "false"

# maxLength 90, enforced structurally. An escape sequence counts as one char
# here and decodes to one char, so the bound matches JSON string semantics.
question ::= "\"" char{0,90} "\""

kind ::= "\"amount\"" | "\"target\"" | "\"resource\"" | "\"price\"" | "\"other\""

# lifted from llama.cpp grammars/json.gbnf, so it is known good: any char
# except quote, backslash, DEL and the C0 control range.
char ::= [^"\\\x7F\x00-\x1F] | [\\] (["\\bfnrt/] | "u" [0-9a-fA-F]{4})
ws   ::= [ \t\n]{0,2}
```

Every constraint in the JSON schema survives: the boolean is a boolean, the
enum cannot be escaped, the 90 char ceiling is hard, and no fourth key can be
emitted. Nothing here is model specific.

The same holds for the other four schemas without extra work, including
`AdjudicateSchema` with its 13 top level fields, two nested objects, three
bounded arrays, six enums and the `["number","string"]` union. All of it is
inside the converter's supported set, so the reason not to run adjudication
locally is quality and prefill cost, never the schema.

## (b) Model recommendation

LLMUnity ships a curated list as a hardcoded dictionary
(`Runtime/LLMUnitySetup.cs:125`), all Q4_K_M, all with direct Hugging Face
URLs. In our target band:

Sizes marked "measured" came from a HEAD request against the exact URL in their
table, stated in decimal GB as a download UI would show it, so they are the real
number the player sees.

| Entry | Ref | Q4_K_M download | Reasoning model? |
|---|---|---|---|
| Qwen 3.5 4B | `:146` | **2.74 GB** (measured) | yes, avoid |
| Gemma 3 4B | `:148` | 2.49 GB | no, but Gemma license |
| Phi 4 mini | `:149` | 2.49 GB | no, MIT |
| Llama 3.2 3B | `:147` | **2.02 GB** (measured) | no |
| Qwen 3.5 2B | `:153` | **1.28 GB** (measured) | yes, avoid |
| Llama 3.2 1B / Gemma 3 1B | `:155`, `:156` | ~0.8 GB | too small regardless |

**Do not take their default.** Every Qwen 3.5 entry in that list is a hybrid
reasoning checkpoint, and reasoning models are the one family where constrained
decoding is currently broken. llama.cpp issue #20345 was closed as fixed, then
reopened in comments through 2026-04 and 2026-05: grammar constraints leak into
`reasoning_content`, the model loops emitting garbage while `content` stays
empty, and `--reasoning-budget 0` aborts with `Unexpected empty grammar stack`.
LLMUnity has its own live instance of this, issue #401, on macOS, on v3.0.3,
with Qwen 3.5 0.8B emitting empty `<think>` blocks whatever the reasoning
toggle says. Turning reasoning off is not a fix; the checkpoint has to be non
reasoning by construction.

Recommendation: **Qwen3-4B-Instruct-2507 at Q4_K_M**, 2.50 GB, fetched by us
rather than from their list.

Why this one:

- It is architecturally non reasoning. Its model card states it does not emit
  `<think>` blocks at all, which sidesteps the entire bug class above.
- Apache 2.0, verified against the Hugging Face API. That matters more than it
  looks: see the license traps below.
- Strongest instruction following in the band (IFEval 83.4), which is the only
  capability this job needs. We are not asking for reasoning, we are asking for
  invented names, theses and one short question, inside a grammar that already
  guarantees the shape.
- Q4_K_M is the right quant. Q5_K_M buys little on models this small and costs
  roughly 20 percent more size and memory; Q3 and below degrade instruction
  following noticeably, which is exactly the axis we depend on.

Smaller alternative if 2.50 GB proves too much to ask: **Llama-3.2-3B-Instruct**
at 2.02 GB, also non reasoning, IFEval 77.0, and the checkpoint that the
llama.cpp thread explicitly confirms grammar works on. Cost is the Llama 3.2
Community License, which wants a "Built with Llama" notice and has a 700M MAU
clause. Neither is a real obstacle for us, but it is a legal review rather than
a shrug.

License traps to avoid, all verified:

- **Qwen2.5-3B is not Apache 2.0.** It carries the Qwen RESEARCH license,
  non commercial, explicit "request a license from us" for commercial use. The
  trap is that most of the Qwen2.5 family (0.5B, 1.5B, 7B, 14B, 32B) IS Apache
  2.0 and only 3B and 72B are not, so the family reputation is misleading.
  Disqualifying for a shipped game.
- **Gemma 2 and Gemma 3** are under the Gemma Terms of Use, not Apache, with a
  Prohibited Use Policy that flows down onto our own players and a unilateral
  termination right. Gemma 4 is Apache but has an open llama.cpp grammar
  crash (#23677, "Unexpected empty grammar stack", closed as not planned).
- The function calling specialists (xLAM-2, Hammer2.1) are CC-BY-NC or
  qwen-research. All non commercial.

Do not use the 1B models. Under a constraining grammar a 1B model produces
structurally perfect and semantically empty output, which is the worst failure
mode we could ship: it looks like it worked.

### Two defaults that must change

Metal is opt in and off by default: `_numGPULayers = 0` (`Runtime/LLM.cs:53`).
Set it above zero or the model runs on CPU and every number below is wrong.
Use 999 to offload every layer.

One correction worth recording, because the naming invites the wrong
conclusion: `numGPULayers` does **not** select a Metal library. On
`osx-arm64` Metal is compiled into both the `acc` and `no-acc` dylibs, with
shaders embedded in the binary (so the classic missing `.metallib` failure
cannot happen). The `acc` / `no-acc` axis is Accelerate and BLAS only.
Relatedly, LlamaLib reports an empty GPU architecture list on macOS because its
GPU branch is Windows and Linux only, which is cosmetic: `-ngl` still offloads.

Flash attention is the second one. LlamaLib does not leave llama.cpp on auto,
it emits `-fa off` whenever the bool is false, and LLMUnity defaults it to
false. Set `flashAttention = true`.

### Expected throughput

Planning ranges for a 3B class Q4_K_M with Metal offload on. Anchored on two
primary measurements (Gemma-3-1B Q4_0 on an M1 Air 8GB at 1031 prefill and 57.0
decode, from llama.cpp discussion #12985; Llama-3.2-3B Q4_K_M on an M4 Mac mini
16GB at 1720 prefill and 46.7 decode) and cross checked against the pinned 7B
community table in llama.cpp discussion #4167. Decode is bandwidth bound and
lands at roughly 75 to 85 percent of a naive linear extrapolation down from 7B;
prefill is compute bound and extrapolates almost exactly.

| Machine | Prefill | Decode |
|---|---|---|
| M1 / M2 / M4 base | 500 to 1700 tok/s | 40 to 50 tok/s |
| M1 / M2 / M3 Pro | 800 to 1500 tok/s | 60 to 80 tok/s |
| M2 / M3 / M4 Max | 1500 to 3000 tok/s | 100 to 150 tok/s |

The wide prefill spread inside one chip class is batch size. LLMUnity ships
`batchSize = 512`, the conservative end; raising it moves toward the upper
number. Replace all of this with a ten minute `llama-bench` run on a real
target Mac before anything depends on it.

Applied to our actual budgets, at the pessimistic end of the base tier:

- **Clarify**, warm: the 2,206 token preamble is already cached, the user delta
  is a few hundred tokens, output is about 30 tokens. Comfortably under a
  second. Cold, the first call adds roughly 1.5 to 4 seconds of preamble
  prefill, once.
- **Worldgen**: 282 token prefill is instant, about 800 tokens of output at 40
  to 50 tok/s gives 16 to 20 seconds. This already happens behind the founding
  curtain, which narrates a wait.
- **Adjudicate**: 20,317 tokens of prefill before anything else. At 500 tok/s
  that is 40 seconds, and it is re paid whenever the cache is evicted. Then 700
  tokens of decode on top. This is the arithmetic behind the no go, and the
  memory figures below make it worse.

### Memory

Weights are the small half of the story. KV cache is
`2 x layers x kv_heads x head_dim x ctx x bytes_per_elem`, and **LLMUnity
exposes no KV quantization at all**: no `--cache-type-k/v` anywhere in
`Runtime/` or `Editor/`, so we are on F16 KV unless we go around the component
(see the escape hatch below). F16 KV at 8k context:

| Model | KV heads | KV at 8k | KV at 16k |
|---|---|---|---|
| Llama-3.2-3B | 8 | 896 MiB | 1792 MiB |
| Qwen 3B class | 2 | 288 MiB | 576 MiB |

The narrow GQA config on the Qwen models gives a roughly 3x smaller KV cache at
equal context, which is a second, independent argument for the Qwen pick and
matters most in exactly the scenario we are ruling out.

Total resident for the cheap tiers at 4k to 8k context: about 2.5 GB of weights
plus 0.3 to 0.9 GB of KV plus 0.5 to 1.0 GB of compute buffers and runtime, so
budget **3.5 to 4 GB**. Comfortable on 16 GB, genuinely tight on an 8 GB Mac
once Unity and our textures are resident, and Metal caps GPU allocation near 75
percent of unified memory, which on 8 GB is about 6 GB for everything.

For adjudication the same arithmetic gives roughly 2.7 GB of KV cache alone on
a Llama style model at the 24k it would need, on top of weights. That tier is
not merely slow locally, it does not fit on the machines we would be shipping
it to.

`SetModel` reads the GGUF header and clamps `contextSize` to what the model
supports (`Runtime/LLM.cs:582`), and warns above 32,768.

**Escape hatch, worth knowing but not for the pilot:**
`LLMService.FromCommand(string)` routes through llama.cpp's full server
argument parser, so every llama-server flag is reachable, including
`--cache-type-k q8_0` (which would roughly halve the KV figures above) and
custom batch sizes. The `LLM` MonoBehaviour does not use that path, so taking
it means bypassing or subclassing the component.

The dev machine for this work is an M4 Pro with 48 GB, which is far above the
player floor. Any pilot measurement taken here must be discounted before it is
believed; the number that matters is an 8 GB M1.

## (c) Risk sheet

### Adjudication quality against terra

This is the risk that matters and it is not close. `Adjudicate` is asked to
weigh a free form move against the full run state, choose a governing stat and
a DC, apply order of magnitude money reasoning tied to the era, respect named
staff and their burnout, honour funding path constraints, gate milestone flags,
and write 210 to 290 words of second person prose in a specific dry register.
We then run a `Sentinel` pass over the result and retry once with the faults
echoed (`EventGenerator.cs:389-406`), which tells us the frontier model already
fails these checks often enough to need a net.

A 3B or 4B model under a grammar will return well formed JSON that satisfies
every structural check and fails every judgment the prompt actually asks for.
The grammar guarantees shape, never sense. Worse, our `Sentinel` catches
continuity faults, not flatness, so bad local adjudication would pass the net
and reach the player. Do not route this tier locally. The existing keyless stub
(`KeylessAdjudication`) is a more honest degradation than a small model
pretending to judge.

Two findings from the literature sharpen this rather than soften it. "Let Me
Speak Freely?" (EMNLP Industry 2024) measured format constraints degrading
reasoning accuracy by up to 27 percentage points, precisely because a JSON
schema forces the answer field to be emitted before the reasoning that should
support it has happened. That is a description of our `AdjudicateSchema`: key
order is fixed to declaration order, and we declare `narration` before
`verdict`, `roll` and `effects`. A frontier model absorbs that; a 3B model
writes 290 words of prose and then picks a verdict to match what it already
wrote. The recommended mitigation in that literature is free form reasoning
first and constrained decoding only on a final structuring step, which llama.cpp
supports through lazy grammars with trigger patterns, but LLMUnity does not
expose them.

The cheap tiers are a different proposition because the bar is different.
Worldgen competes against our deterministic `WorldGen` fallback, not against
terra. "Better than a deterministic generator at inventing three investors and
two rivals" is a bar a 4B instruct model clears. That framing should govern how the
pilot is judged.

### App size and the optional download

The model must never enter the bundle. LLMUnity supports this directly: set
`LLMManager.SetDownloadOnStart(true)` (`Runtime/LLMManager.cs:521`) with a
non empty `url` on the entry, and `LLMManager.Build`
(`Runtime/LLMManager.cs:629`) skips the StreamingAssets copy. At runtime
`LLM.Awake` calls `LLMManager.Setup()` and the game awaits
`LLM.WaitUntilModelSetup(progress)` with an `Action<float>` from 0 to 1. The
`Samples~/MobileDemo` scene is exactly this flow. Downloads resume across
launches (`Runtime/ResumingWebClient.cs:41`, resume is on by default at
runtime).

Three real problems with their path, all avoidable:

1. **On macOS desktop their runtime download target is inside the .app
   bundle.** `GetDownloadAssetPath` returns `Application.streamingAssetsPath`
   on desktop (`Runtime/LLMUnitySetup.cs:239`), which on macOS is
   `<App>.app/Contents/Resources/Data/StreamingAssets`. Writing 2.5 GB there
   invalidates the code signature, and an app still under quarantine runs from
   a read only translocated mount where the write simply fails. Do not use it.
   Download ourselves with the public, non editor gated
   `LLMUnitySetup.DownloadFile(url, savePath, overwrite, callback, progressCallback)`
   (`Runtime/LLMUnitySetup.cs:289`) into `Application.persistentDataPath`, then
   hand `SetModel` the absolute path. That works because
   `GetLLMManagerAssetRuntime` passes an existing absolute path straight
   through (`Runtime/LLM.cs:886-905`). Keep the `LLM` GameObject disabled until
   the file is present, since `Awake` bails on `!enabled` and `SetModel`
   asserts the service has not started.
2. **The model list is frozen at build time** into
   `StreamingAssets/LLMManager.json`. Every `LLMManager.Download*` and
   `SetDownloadOnStart` method is inside `#if UNITY_EDITOR`
   (`Runtime/LLMManager.cs:300-641`) and is stripped from players. There is no
   supported runtime API to add or choose a model URL. Rolling our own per
   point 1 sidesteps this too.
3. **There is no delete or evict API at all**, at runtime or in the editor. The
   bin button only forgets the entry, it does not free the disk. If players can
   turn this feature off we owe them a `File.Delete` and our own bookkeeping.

The native library is a separate cost and it is a large one at development
time: `LlamaLib-v2.0.5.zip` is a single monolithic archive carrying every
platform including the CUDA runtimes, **1.82 GB measured**, pulled on every
editor domain reload (cached and hash checked since v3.0.2). Shipped size is
fine because
`LLMBuilder.BuildLibraryPlatforms` (`Runtime/LLMBuilder.cs:195`) moves every
non target platform out before the build; a macOS build keeps only the four
`osx` dylibs. The multi gigabyte cost lands on our machines, not the player's.

### Main thread behaviour

Better than expected, with one caveat.

- Token generation runs on a thread pool thread: `ChatAsync` and
  `CompletionAsync` are `await Task.Run(...)`
  (`Runtime/LlamaLib/LLMAgent.cs:271`, `Runtime/LlamaLib/LLM.cs:202`).
- Model load also runs off thread, inside `Task.Run` under a static lock
  (`Runtime/LLM.cs:504`). Loading a 2.5 GB GGUF will not stall a frame, though
  it will hammer the disk and the unified memory bus while it happens.
- Streaming callbacks are explicitly marshalled back to the main thread
  (`Utils.WrapCallbackForAsync`, or `WrapActionForMainThread` under IL2CPP,
  `Runtime/LLMClient.cs:584-593`).

Caveat: our `cb(JObject)` touches Unity objects and must land on the main
thread. Awaiting `Chat` from a main thread started async method resumes on
Unity's synchronization context, so it does in practice, but the pilot should
marshal explicitly rather than depend on it, because `RequestJson` returns void
and will be started fire and forget as `_ = RunAsync(...)`.

Second caveat: with all layers offloaded, generation contends with rendering
for the GPU. A local call during an animated screen may cost frames even though
it costs no main thread time. Worth measuring against our existing perf
harness, and an argument for running local generation behind the curtain rather
than during play.

### Runaway generation, which is a freeze in a game

`numPredict` defaults to -1, meaning unlimited (`Runtime/LLMClient.cs:58`). A
grammar that permits unbounded repetition plus a model that starts looping
generates until the context window is exhausted. On a background thread with a
spinning UI that reads as a multi second freeze with no error. Our schemas all
carry `maxItems`, so the arrays are bounded, but the free text fields are only
bounded by `maxLength`, and the failure mode is cheap to prevent: set a hard
`numPredict` per tier. Also cap `numThreads`, which defaults to -1 and will
otherwise take every core it can and starve the render thread.

### Signing, notarization and the platform traps

This is the least explored part of the package and the part most likely to cost
unplanned days.

- **There is no code signing or notarization step anywhere in their build
  pipeline.** The macOS post build path only patches the Xcode project for
  framework linking and library search paths. No issue on either repository
  mentions `codesign`, `notarize`, Gatekeeper or quarantine. That is untested,
  not fine: we ship signed and notarized, and we will be signing four bundled
  dylibs ourselves.
- **A macOS build bundles both `osx-x64` and `osx-arm64`**
  (`Runtime/LLMBuilder.cs:210-212`), and the loader picks by
  `RuntimeInformation.ProcessArchitecture`. So an Intel player running under
  Rosetta on Apple Silicon loads the x64 dylib, which has **Metal compiled
  out**, and silently runs on CPU. Ship an Apple Silicon or Universal player,
  and treat "why is it so slow on my M2" as a Rosetta question first.
- **The deployment floor is macOS 13.3**, set in LlamaLib's CMake. Worth
  checking against whatever floor RUNWAY! already commits to.
- **Unity 6 on macOS is where their friction concentrates.** We are on
  6000.0.82f1. Open at the time of writing: #396 (osx dylib "not found in
  project" on v3.0.3, build still succeeds), #398 (Newtonsoft `CS0246` on a
  fresh Unity 6.3 project, a regression of the v3.0.1 fix), #411 (LlamaLib
  install failing for hours). Recently closed but never root caused: #365, a
  whole Editor crash on second Play on macOS, and #320, an Editor crash on re
  entering Play Mode with `CAMetalLayer ignoring invalid setDrawableSize`.
  None of these is fatal, but budget for an install that does not work first
  try.

Two smaller notes: backend GPU side sampling is disabled whenever a grammar is
active, and the faster LLGuidance grammar backend is not compiled into
LlamaLib, so we are on the native GBNF sampler with whatever that costs.

### Verify first, before anything is built on it

The multi tier design rests on one inferred fact: that grammar and sampling
parameters are stored per agent handle, not on the shared context. The C#
strongly implies it, because `LLMAgent_Construct` returns its own `IntPtr`
(`Runtime/LlamaLib/LLMAgent.cs:89`) and `LLM_Set_Grammar` takes that per agent
handle. But the storage lives in the native library, which we cannot read. If
grammar turns out to be global on the shared context, concurrent tiers with
different schemas would corrupt each other and every call would have to be
serialized behind a lock. Prove this with two agents holding different grammars
before designing around it. This is the single assumption worth an hour before
any design work.

## (d) Go / no go, and the smallest pilot

**GO** for cheap, offline, generative tiers behind `RUNWAY_LOCAL_LLM=1`.
**NO GO**, permanently, for `Adjudicate`.

The pilot: clarify only.

- One `LLM` component, **Qwen3-4B-Instruct-2507 Q4_K_M**, `numGPULayers = 999`,
  `flashAttention = true`, `contextSize = 4096`, `parallelPrompts = 1`,
  `numThreads` capped rather than -1.
- One `LLMAgent`, `systemPrompt` from the shipped `clarify.txt`,
  `grammar = LlmClient.ClarifySchema.ToString()`, `numPredict = 64`,
  `temperature` low.
- `LocalLlmClient.RequestJson` accepts only `opts.Tier == "clarify"` and calls
  `cb(null)` for everything else, which every caller already handles.
- Model fetched on first use into `persistentDataPath` with a progress UI, not
  bundled, and not through their StreamingAssets download path.
- `Warmup()` during the boot curtain so the first real call is warm.

Why clarify is the right first step: it is the one tier where the schema carries
nearly all the quality burden (a boolean, an enum, and one sentence under 90
characters), so a weak model cannot embarrass us. It proves the seam, the
constrained decoding path, the threading, the packaging and the download UX with
almost no exposure to the quality question.

Then the milestone that actually matters to a keyless player: **worldgen**.
282 token prompt, once per run, behind an existing curtain, judged against a
deterministic fallback rather than against terra. That is where the feature
stops being plumbing and starts being the reason to build it. Event cards and
arcs follow on the same machinery if worldgen lands.

Explicitly out of scope: adjudication, and any attempt to shrink
`adjudicator.txt` to fit a local model. A shortened adjudicator is a different
game, not a cheaper one.

## (e) Effort estimate

Pilot, clarify only:

| File | Change | Lines |
|---|---|---|
| `unity/Assets/Scripts/LLM/ILlmClient.cs` | new | ~30 |
| `unity/Assets/Scripts/LLM/LocalLlmClient.cs` | new | ~220 |
| `unity/Assets/Scripts/LLM/LlmClient.cs` | add interface, `Describe()` | ~4 |
| `unity/Assets/Scripts/App/Boot.cs` | flag branch, field type | ~10 |
| `unity/Assets/Scripts/LLM/EventGenerator.cs` | field and param type | ~2 |
| `unity/Packages/manifest.json` | add the package | 1 |

Roughly 250 new lines, 17 edited lines, two new files and three touched files.
The 220 line estimate for `LocalLlmClient` covers setup and readiness, the
download with progress, the agent pool, warmup, tier routing, the watchdog and
cancel, main thread marshalling, and parse failure handling. There is no
grammar code and no grammar data file, because the schemas go across as they
already are.

Adding worldgen afterwards: one more agent, one more `ToString()`, about 25
lines. Every later tier is the same 25 lines, so the code cost stops growing
after the pilot.

Excluded from these numbers, and larger than them: the evaluation work. Deciding
whether local worldgen beats our deterministic fallback needs a fixture set of
pitches, both paths run over them, and a human read. Budget that as the real
cost of the second milestone.

## Appendix: facts worth keeping

- Apache 2.0 on LLMUnity; llama.cpp is MIT. Per model licenses vary a great
  deal and the family name is not a reliable guide: check each checkpoint, not
  each vendor.
- LlamaLib is pinned to **v2.0.5** at `Runtime/LLMUnitySetup.cs:103`, while the
  CHANGELOG for v3.0.3 says v2.0.4. Trust the constant.
- `_numGPULayers` defaults to 0, `_contextSize` to 8192, `_batchSize` to 512,
  `_parallelPrompts` to 1, `_numThreads` to -1, `numPredict` to -1,
  `temperature` to 0.2, `cachePrompt` to true (`Runtime/LLM.cs:49-73`,
  `Runtime/LLMClient.cs:58-122`).
- `--context-shift` is always on, which we do not want silently rescuing an
  over long prompt. Prefer to fail loudly and keep prompts inside the window.
- Slot count is `max(parallelPrompts, 1)` when set, otherwise the number of
  registered clients (`Runtime/LLM.cs:802`).
- `LLM.WaitUntilModelSetup` appends to `LLMManager.downloadProgressCallbacks`
  with no matching remove, so repeated calls leak callbacks.
- Their iOS and visionOS path has an offline start bug: the existence check
  uses `GetAssetPath` while the download used `GetDownloadAssetPath`, so an
  already downloaded app issues a HEAD on every launch and fails to start with
  no network. Desktop and Android are unaffected, so it does not touch us
  today, but it does bear on how much we trust their setup path.
- Their editor toggle is labelled "Download on Start"; the README calls it
  "Download on Build". Same flag, stale docs.

## WIRING STATUS

The pilot's plumbing is in the tree and proven. The model is not, and will not be
until the owner opts in — nothing was downloaded, no package was added, and
`Packages/manifest.json` is untouched.

### What exists

| File | What it is |
|---|---|
| `Assets/Scripts/LLM/LocalLlm.cs` | `ILocalCompletion`, the backend contract. `LocalLlmRouter`, the seam. `LocalJson.Fits`, the schema check on the way out. |
| `Assets/Scripts/LLM/LocalLlmCanned.cs` | A deterministic backend that answers clarify from a fixed deck, so the routing is testable with no model. |
| `Assets/Scripts/LLM/LocalLlmUnityAdapter.cs` | The LLMUnity backend, written to the mapping in section (a), entirely behind `#if RUNWAY_LLMUNITY`. Compiles to nothing today. |
| `Assets/Scripts/Editor/LocalNarratorProbe.cs` | The gate. 47 checks, 47 held, exit 0. |
| `Assets/Scripts/LLM/LlmClient.cs` | One seam call, seven lines including its comment. The only shipped file touched. |

The seam, in `RequestJson`, above the `Enabled` gate:

```csharp
if (LocalLlmRouter.TryServe(this, systemPrompt, userPrompt, schema, cb, opts)) return;
```

It answers `true` only when the flag is `1`, a provider is registered and `Ready`,
and the tier is `clarify`. Anything else returns `false` and the caller continues
into the network path it already had. With the flag unset that decision is one
string compare per request, which is what makes it safe to leave wired in.

Three properties the router owns, because a local backend does not come with them
the way a hosted API does:

- **`assess` is refused in `TryServe` itself**, before any provider is consulted.
  Probe phase 5 registers a backend that claims every tier and watches it get
  refused anyway. The permanent no-go is enforced in one place, not by manners.
- **A once-only answer.** The backend and the watchdog race for the same cell;
  whichever arrives first is the only one game code ever sees, so a late reply
  cannot land on a turn that already moved on.
- **A schema check before the object reaches game code.** A grammar guarantees shape
  only while the grammar actually held, and every step between the sampler and our
  `JObject` is ours. A reply that does not fit becomes `cb(null)`, which is the
  authored path every caller already has.

**What is deliberately NOT wired: the keyless run.** The seam sits above
`LlmClient.Enabled`, so the local path is open to a keyless client and the probe
drives exactly that. But `EventGenerator.Live` is `Llm != null && Llm.Enabled`, and
`Clarify()` returns early on `!Live`. So today the pilot lights up on a machine that
also has a key — it proves the machinery, not yet the feature. Closing that is
section (a) verbatim: extract `ILlmClient`, give `Enabled` a local answer, change two
field types in `Boot` and `EventGenerator`. About 16 edited lines across two files,
and the right moment for it is the same commit that adds the package, so that
experimental routing never sits in front of a live run's `Live` gate for nothing.

### Proving it today, with no model

```bash
bash tools/unity_compile.sh          # 0 errors

RUNWAY_LOCAL_OUT=/tmp/d-local \
Unity -batchmode -quit -nographics -projectPath unity \
      -executeMethod Runway.EditorTools.LocalNarratorProbe.Run
```

Or in a real run, where every clarify question comes from the deck rather than the
wire:

```bash
RUNWAY_LOCAL_LLM=1 RUNWAY_LOCAL_LLM_CANNED=1 ...
```

`RUNWAY_LOCAL_LLM_CANNED=poison` serves a deliberately out-of-schema reply, so the
guard above can be watched catching it rather than assumed to.

### The two activation steps

1. **The package.** Add to `Packages/manifest.json`:

   ```json
   "ai.undream.llm": "https://github.com/undreamai/LLMUnity.git#v3.0.3"
   ```

   Its one dependency, `com.unity.nuget.newtonsoft-json`, is already at 3.2.1. There
   are no asmdefs in this project, so its types land in Assembly-CSharp with no
   assembly wiring. Budget for the first pull: `LlamaLib-v2.0.5.zip` is 1.82 GB and
   arrives on editor domain reload, and the risk sheet lists four open Unity 6 /
   macOS issues on that package. An install that does not work first try is expected.

2. **The define.** Player Settings ▸ Other Settings ▸ Scripting Define Symbols, add
   `RUNWAY_LLMUNITY`. `scriptingDefineSymbols` is `{}` today.

Then run with `RUNWAY_LOCAL_LLM=1`. The adapter installs itself from a
`[RuntimeInitializeOnLoadMethod]` — no edit to `Boot` — and fetches the model on
first use. Two optional overrides: `RUNWAY_LOCAL_LLM_MODEL` (file name) and
`RUNWAY_LOCAL_LLM_URL`.

The default is **Qwen3-4B-Instruct-2507 Q4_K_M**, 2.50 GB, Apache 2.0, non-reasoning
by construction. Do not swap it for anything in LLMUnity's curated list without
re-reading section (b) first: every Qwen 3.5 entry there is a hybrid reasoning
checkpoint, and constrained decoding is currently broken on that whole family.

### The packaging rules, restated

These are the ones that cost days if they are discovered late rather than read here.

- **The model never enters the bundle, and never enters StreamingAssets.** LLMUnity's
  own runtime download target on macOS desktop is
  `<App>.app/Contents/Resources/Data/StreamingAssets`. Writing 2.5 GB there
  invalidates the code signature, and an app still under quarantine runs from a
  read-only translocated mount where the write simply fails. The adapter downloads
  into `Application.persistentDataPath/models/` itself and hands `SetModel` the
  absolute path, which `GetLLMManagerAssetRuntime` passes straight through.
- **Their model list is frozen at build time**, and every `LLMManager.Download*` and
  `SetDownloadOnStart` method is inside `#if UNITY_EDITOR`. A player has no supported
  way to name a model at all. Rolling our own download sidesteps this too.
- **The `LLM` GameObject stays disabled until the file is on disk.** `Awake` bails on
  `!enabled`, and `SetModel` asserts the service has not started.
- **There is no delete or evict API**, at runtime or in the editor. If players can
  ever turn this off, we owe them a `File.Delete` and our own bookkeeping.
- **Ship an Apple Silicon or Universal player.** A macOS build bundles both `osx-x64`
  and `osx-arm64` and the loader picks by process architecture, so an Intel player
  under Rosetta loads the x64 dylib, which has Metal compiled out, and silently runs
  on CPU. Treat "why is it so slow on my M2" as a Rosetta question first.
- **We sign and notarize; they have no signing step anywhere.** Four bundled dylibs
  become ours to sign. Nothing in either repository mentions `codesign`, `notarize`,
  Gatekeeper or quarantine. That is untested, not fine.
- **Two defaults must be changed or every throughput number above is wrong.**
  `numGPULayers` is 0 (Metal off) and `flashAttention` is false (LlamaLib emits
  `-fa off`, it is not on auto). The adapter sets 999 and true.
- **`numPredict` and `numThreads` both default to -1.** Unlimited generation on a
  background thread reads as a multi-second freeze with no error, and -1 threads
  starves the render thread. The adapter caps both.

Full risk ledger for what landed: `unity/COMPILE-RISKS.md`, section **D-LOCAL**.
