using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Runway.App;

namespace Runway.Llm
{
    /// <summary>
    /// ONE LOCAL GENERATION BACKEND, shaped exactly like the seam it stands behind.
    ///
    /// `LlmClient.RequestJson` is (system prompt, user prompt, schema) → JObject, and
    /// that is the whole contract a local model has to satisfy. Nothing here knows what
    /// a GGUF is; the backend that does lives behind this interface and behind a
    /// compile define, so the game builds and ships with no package installed.
    ///
    /// The implementer owes three things and they are not negotiable:
    ///   · cb is called EXACTLY ONCE, ever. The router races a watchdog against it.
    ///   · cb lands on the MAIN THREAD. Callers touch Unity objects inside it.
    ///   · cb(null) on every failure. Every caller in the game already handles null —
    ///     that is the authored-content path, and it is the honest degradation.
    /// </summary>
    public interface ILocalCompletion
    {
        /// A model is loaded and this tier's agent is standing. False while a download
        /// or a load is in flight, and the router falls through to the network meanwhile
        /// rather than making the player wait on a 2.5 GB fetch.
        bool Ready { get; }

        /// Which tiers this backend will take. The pilot answers true for "clarify" and
        /// false for everything else. A backend must NEVER answer true for "assess":
        /// adjudication is a permanent no-go locally (20,317 tokens of prefill, and the
        /// one judgment in the game a 4B model cannot make).
        bool Handles(string tier);

        /// One line for the boot log.
        string Describe();

        /// system + user + schema → JObject. The RequestJson shape, callback-based,
        /// non-blocking.
        void Complete(string systemPrompt, string userPrompt, JObject schema,
                      string tier, int maxTokens, Action<JObject> cb);

        /// Drop whatever is in flight. The router calls this when its watchdog fires;
        /// a llama.cpp backend maps it to the agent's own cancel.
        void Cancel();
    }

    /// <summary>
    /// THE SEAM. One call from `LlmClient.RequestJson` decides, per request, whether a
    /// local backend takes it or the network does.
    ///
    /// It answers TRUE only when every one of these holds:
    ///   · RUNWAY_LOCAL_LLM=1
    ///   · a provider is registered and Ready
    ///   · the tier is the pilot's ("clarify") and the provider takes it
    /// Anything else returns FALSE and the caller continues into the network path it
    /// already had, unchanged. That is what makes this safe to leave wired in: with the
    /// flag unset it is one string compare per request and nothing else.
    ///
    /// It also owns the two guards a local backend needs and a hosted API gives away
    /// for free: a once-only answer (so a backend that calls back twice, or calls back
    /// after the watchdog gave up, cannot deliver twice into game code), and a schema
    /// check on the way out (a grammar guarantees SHAPE, and only when the grammar
    /// actually held — a parse that drifted must not reach the game wearing the right
    /// name).
    /// </summary>
    public static class LocalLlmRouter
    {
        /// The one switch. Set it and nothing else changes until a provider exists.
        public const string Flag = "RUNWAY_LOCAL_LLM";

        /// The pilot's tier, and the only one served locally today. Clarify is a
        /// boolean, an enum and one sentence under 90 characters: the schema carries
        /// nearly all of the quality burden, so a small model cannot embarrass us.
        public const string PilotTier = "clarify";

        /// Permanently remote, whatever a provider claims. Belt AND braces: the
        /// dossier's one hard no-go gets an explicit line rather than an implicit one.
        public const string NeverLocal = "assess";

        /// numPredict for clarify when the caller names no budget. ~30 tokens of output
        /// is the real shape; 64 is headroom, and a hard cap is the only thing standing
        /// between a looping model and a multi-second freeze with no error.
        public const int ClarifyPredictCap = 64;

        /// The caller's clock is 50s for clarify (LlmClient's own watchdog). Ours must
        /// fire first or the two clocks fight over one callback.
        public const float WatchdogSeconds = 40f;

        static ILocalCompletion _provider;
        static bool _looked;
        static bool _flagRead, _flagOn;

        // ── what happened, for probes and for the boot log ──────────────────────
        public static int Served;          // requests this router took
        public static int PassedThrough;   // requests it declined, i.e. the network's
        public static int Rejected;        // local replies that did not fit their schema
        public static int TimedOut;        // watchdog firings
        public static string LastServedTier = "";
        public static string LastNote = "";

        /// The flag, read once. Reset() re-reads it (probes flip it; a run does not).
        public static bool Active
        {
            get
            {
                if (!_flagRead)
                {
                    _flagOn = Env.Get(Flag, "") == "1";
                    _flagRead = true;
                }
                return _flagOn;
            }
        }

        /// The registered backend, or the canned one if its own flag asked for it, or
        /// null — which is the normal state and means "network only".
        public static ILocalCompletion Provider
        {
            get
            {
                if (_provider != null) return _provider;
                if (!_looked)
                {
                    _looked = true;
                    if (LocalLlmCanned.Wanted)
                    {
                        _provider = new LocalLlmCanned();
                        // Say it out loud, once. A canned deck that leaks into a real
                        // run answers every clarify with one of five fixed sentences
                        // and raises no error of its own.
                        Debug.Log("LOCAL LLM: " + _provider.Describe()
                                  + " — clarify is NOT being generated");
                    }
                }
                return _provider;
            }
        }

        /// A backend installs itself here. The LLMUnity adapter does it from a runtime
        /// init hook, so adding the package needs no edit to Boot.
        public static void Register(ILocalCompletion p)
        {
            _provider = p;
            _looked = true;
        }

        /// Full reset: wiring, flag and counters. For probes and for a keys-screen
        /// reload that may have changed the environment under us.
        public static void Reset()
        {
            _provider = null;
            _looked = false;
            _flagRead = false;
            Served = 0; PassedThrough = 0; Rejected = 0; TimedOut = 0;
            LastServedTier = "";
            LastNote = "";
        }

        /// Boot's one line.
        public static string Describe()
        {
            if (!Active) return "local off";
            ILocalCompletion p = Provider;
            if (p == null) return "local on, no provider — network only";
            return "local on: " + p.Describe() + (p.Ready ? "" : " (not ready yet)");
        }

        // ══ the seam ═══════════════════════════════════════════════════════════

        /// TRUE means this router has taken the request and WILL call cb. FALSE means
        /// the caller keeps going down its own path, having lost nothing.
        public static bool TryServe(MonoBehaviour host, string systemPrompt, string userPrompt,
                                    JObject schema, Action<JObject> cb, LlmOptions opts)
        {
            string tier = opts.Tier ?? "";

            if (!Active) return Pass(tier, "flag off");
            if (tier == NeverLocal) return Pass(tier, "assess is permanently remote");
            if (tier != PilotTier) return Pass(tier, "not the pilot's tier");
            ILocalCompletion p = Provider;
            if (p == null) return Pass(tier, "no provider registered");
            if (!p.Handles(tier)) return Pass(tier, "provider does not take " + tier);
            if (!p.Ready) return Pass(tier, "provider not ready");

            Served++;
            LastServedTier = tier;
            LastNote = "served " + tier + " locally (" + p.Describe() + ")";

            var call = new Pending(cb, schema, p);
            int budget = opts.MaxTokens > 0 ? opts.MaxTokens : ClarifyPredictCap;
            try
            {
                p.Complete(systemPrompt, userPrompt, schema, tier, budget, call.Answer);
            }
            catch (Exception e)
            {
                // print, not a warning: release builds swallow warnings, and this line
                // is the only witness a shipped session gets
                Debug.Log("LOCAL LLM threw on " + tier + ": " + e.Message);
                call.Answer(null);
                return true;
            }
            // A backend that already answered needs no clock. One that has not gets the
            // watchdog — outside play mode StartCoroutine is inert, which is why a
            // synchronous backend (the canned one) is what the probe drives.
            if (!call.Done && host != null)
            {
                try { host.StartCoroutine(Watch(call)); }
                catch (Exception) { /* no clock available; numPredict is the real bound */ }
            }
            return true;
        }

        static bool Pass(string tier, string why)
        {
            PassedThrough++;
            LastNote = "passed " + (tier.Length > 0 ? tier : "(untiered)") + " through: " + why;
            return false;
        }

        static IEnumerator Watch(Pending call)
        {
            while (!call.Done)
            {
                if (Time.realtimeSinceStartup - call.Start > WatchdogSeconds)
                {
                    TimedOut++;
                    Debug.Log(string.Format(
                        "LOCAL LLM WATCHDOG fired after {0:0}s — cancelling and falling back",
                        WatchdogSeconds));
                    call.GiveUp();
                    yield break;
                }
                yield return null;
            }
        }

        /// One request in flight. The backend and the watchdog both hold it; whichever
        /// gets there first is the only one that reaches game code.
        sealed class Pending
        {
            readonly Action<JObject> _cb;
            readonly JObject _schema;
            readonly ILocalCompletion _p;

            public bool Done;
            public readonly float Start;

            public Pending(Action<JObject> cb, JObject schema, ILocalCompletion p)
            {
                _cb = cb; _schema = schema; _p = p;
                Start = Time.realtimeSinceStartup;
            }

            public void Answer(JObject data)
            {
                if (Done) return;
                Done = true;
                if (data != null && _schema != null)
                {
                    var faults = new List<string>();
                    if (!LocalJson.Fits(_schema, data, faults))
                    {
                        Rejected++;
                        LastNote = "local reply did not fit its schema: "
                                   + string.Join("; ", faults.ToArray());
                        Debug.Log("LOCAL LLM " + LastNote);
                        data = null;   // the game's own null path, which every caller has
                    }
                }
                if (_cb != null) _cb(data);
            }

            public void GiveUp()
            {
                if (Done) return;
                try { if (_p != null) _p.Cancel(); } catch (Exception) { }
                LastNote = "local call timed out after " + WatchdogSeconds + "s";
                Answer(null);
            }
        }
    }

    /// <summary>
    /// DOES THIS REPLY FIT THE SCHEMA WE ASKED FOR — the guard on the way out of a
    /// local backend.
    ///
    /// A hosted API is contractually on the hook for its own structured output. A local
    /// model is not: llama.cpp's grammar enforces the shape only while the grammar is
    /// actually applied, and every step between the sampler and our JObject (an escape
    /// hatch GBNF, a truncated stream, a hand-written stub, a future backend) is ours.
    /// So we check, once, before the object reaches game code.
    ///
    /// It covers exactly the keyword set our five schemas use — type (including the
    /// ["number","string"] union), required, additionalProperties:false, enum,
    /// maxLength, minItems/maxItems, minimum/maximum, and nesting — and deliberately
    /// nothing else. It is a fit check for our own schemas, not a JSON Schema engine.
    /// </summary>
    public static class LocalJson
    {
        public static bool Fits(JToken schema, JToken value, List<string> faults)
        {
            return Fits(schema, value, faults, "$");
        }

        static bool Fits(JToken schema, JToken value, List<string> faults, string at)
        {
            if (schema == null) return true;
            if (value == null || value.Type == JTokenType.Undefined)
            {
                faults.Add(at + " is missing");
                return false;
            }

            bool ok = true;

            JToken t = schema["type"];
            if (t != null && !TypeOk(t, value))
            {
                // a wrong type makes every other check noise, so stop here
                faults.Add(at + " is " + value.Type.ToString().ToLowerInvariant()
                           + ", not " + Short(t));
                return false;
            }

            var en = schema["enum"] as JArray;
            if (en != null)
            {
                bool member = false;
                foreach (JToken e in en)
                    if (JToken.DeepEquals(e, value)) { member = true; break; }
                if (!member)
                {
                    faults.Add(at + " = " + Short(value) + " is outside its enum");
                    ok = false;
                }
            }

            if (value.Type == JTokenType.String)
            {
                JToken ml = schema["maxLength"];
                if (ml != null)
                {
                    int cap = (int)ml;
                    int len = value.ToString().Length;
                    if (len > cap)
                    {
                        faults.Add(at + " is " + len + " chars, over its maxLength " + cap);
                        ok = false;
                    }
                }
            }

            if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
            {
                double d = value.Value<double>();
                JToken mn = schema["minimum"];
                JToken mx = schema["maximum"];
                if (mn != null && d < (double)mn)
                { faults.Add(at + " = " + d + " is under its minimum " + (double)mn); ok = false; }
                if (mx != null && d > (double)mx)
                { faults.Add(at + " = " + d + " is over its maximum " + (double)mx); ok = false; }
            }

            var obj = value as JObject;
            if (obj != null)
            {
                var req = schema["required"] as JArray;
                if (req != null)
                {
                    foreach (JToken r in req)
                    {
                        string k = r.ToString();
                        if (obj[k] == null) { faults.Add(at + " has no \"" + k + "\""); ok = false; }
                    }
                }
                var props = schema["properties"] as JObject;
                if (props != null)
                {
                    JToken addl = schema["additionalProperties"];
                    bool closed = addl != null && addl.Type == JTokenType.Boolean && !(bool)addl;
                    foreach (KeyValuePair<string, JToken> kv in obj)
                    {
                        JToken sub = props[kv.Key];
                        if (sub == null)
                        {
                            if (closed)
                            {
                                faults.Add(at + " carries an undeclared \"" + kv.Key + "\"");
                                ok = false;
                            }
                            continue;
                        }
                        if (!Fits(sub, kv.Value, faults, at + "." + kv.Key)) ok = false;
                    }
                }
            }

            var arr = value as JArray;
            if (arr != null)
            {
                JToken mi = schema["minItems"];
                JToken ma = schema["maxItems"];
                if (mi != null && arr.Count < (int)mi)
                { faults.Add(at + " has " + arr.Count + " items, under minItems " + (int)mi); ok = false; }
                if (ma != null && arr.Count > (int)ma)
                { faults.Add(at + " has " + arr.Count + " items, over maxItems " + (int)ma); ok = false; }
                JToken items = schema["items"];
                if (items != null)
                {
                    for (int i = 0; i < arr.Count; i++)
                        if (!Fits(items, arr[i], faults, at + "[" + i + "]")) ok = false;
                }
            }

            return ok;
        }

        /// "type" is a string, or the ["number","string"] union our effects use.
        static bool TypeOk(JToken t, JToken v)
        {
            var many = t as JArray;
            if (many != null)
            {
                foreach (JToken one in many)
                    if (One(one.ToString(), v)) return true;
                return false;
            }
            return One(t.ToString(), v);
        }

        static bool One(string type, JToken v)
        {
            switch (type)
            {
                case "object":  return v.Type == JTokenType.Object;
                case "array":   return v.Type == JTokenType.Array;
                case "string":  return v.Type == JTokenType.String;
                case "boolean": return v.Type == JTokenType.Boolean;
                case "integer": return v.Type == JTokenType.Integer;
                case "number":  return v.Type == JTokenType.Integer || v.Type == JTokenType.Float;
                case "null":    return v.Type == JTokenType.Null;
            }
            return true;   // an unknown keyword is not a fault worth inventing
        }

        static string Short(JToken v)
        {
            if (v == null) return "(nothing)";
            string s = v.ToString(Formatting.None);
            return s.Length <= 60 ? s : s.Substring(0, 60) + "…";
        }
    }
}
