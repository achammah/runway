using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Runway.Llm;

namespace Runway.EditorTools
{
    /// <summary>
    /// THE LOCAL SEAM, PROVEN WITH NO MODEL ON DISK.
    ///
    /// The local-narrator pilot has two halves. One is generation: can a 4B GGUF write
    /// the clarify question? That needs a package and a 2.5 GB download and it is the
    /// owner's call to make. The other is ROUTING: when RUNWAY_LOCAL_LLM=1 and a
    /// backend is standing, does a clarify request actually leave the network path,
    /// come back the right shape, and does everything else keep going out to the wire?
    ///
    /// That half can break silently. A seam that quietly still calls the network looks
    /// exactly like a seam that works — right up until someone unplugs the ethernet.
    /// So this probe drives the router with a canned backend and asserts the routing
    /// directly, today, with nothing installed.
    ///
    ///   1 OFF            flag unset — the seam is inert, the network keeps the request
    ///   2 NO PROVIDER    flag set, nothing registered — still the network's
    ///   3 THE DECK       flag + canned — three clarify calls, schema-checked, and the
    ///                    alternation is the one the deck promises
    ///   4 NOT THE WIRE   the same, on a client whose Enabled is TRUE — the request
    ///                    never reaches the network path
    ///   5 THE NO-GO      a backend that claims every tier still does not get `assess`
    ///   6 THE GUARD      an out-of-schema local reply is rejected, not handed up
    ///   7 ONCE ONLY      a backend that calls back twice delivers once
    ///
    /// HOW 4 IS PROVEN, since "no network happened" is an absence. The callback fires
    /// SYNCHRONOUSLY, inside RequestJson's own stack frame, carrying a PARSED JObject.
    /// The network path cannot do that in any mode. It hands off to StartCoroutine, and
    /// the earliest `Send` can produce a parsed object is after `while (!op.isDone)
    /// yield return null` — a yield, by construction, and one nothing pumps here.
    /// The one thing `Send` CAN do in its first synchronous segment is refuse an
    /// unrecognised provider with cb(null), and phase 4 watches it do exactly that on
    /// the control request. So the two outcomes are told apart by what arrives, not by
    /// whether anything arrives: a synchronous NULL is the network path declining, a
    /// synchronous OBJECT has exactly one possible author. The router's own counter
    /// agrees with it either way.
    ///
    /// Runs headless, no graphics device needed:
    ///
    ///   Unity -batchmode -quit -nographics -projectPath unity \
    ///         -executeMethod Runway.EditorTools.LocalNarratorProbe.Run
    ///
    /// Output goes to $RUNWAY_LOCAL_OUT (default /tmp/d-local). Exits 1 on any failed
    /// check, so it is a gate and not only a report.
    /// </summary>
    public static class LocalNarratorProbe
    {
        static readonly StringBuilder _log = new StringBuilder();
        static int _checks, _fails;

        static GameObject _rig;
        static LlmClient _keyless;   // Enabled == false, the keyless player
        static LlmClient _keyed;     // Enabled == true, and still no wire is touched

        public static void Run()
        {
            string dir = Environment.GetEnvironmentVariable("RUNWAY_LOCAL_OUT");
            if (string.IsNullOrEmpty(dir)) dir = "/tmp/d-local";
            Directory.CreateDirectory(dir);

            Say("RUNWAY! LOCAL NARRATOR · the seam, with no model on disk · "
                + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Say("unity " + Application.unityVersion + " · batchmode=" + Application.isBatchMode);
            Say("out: " + dir);
            Say("");

            try
            {
                BuildRig();
                Off();
                NoProvider();
                Deck();
                NotTheWire();
                NoGo();
                Guard();
                OnceOnly();
            }
            catch (Exception e)
            {
                _checks++; _fails++;
                Say("FAILED WITH AN EXCEPTION: " + e);
            }
            finally
            {
                Teardown();
            }

            Say("");
            Say("LOCAL DONE pass=" + (_checks - _fails) + " fail=" + _fails);
            try { File.WriteAllText(Path.Combine(dir, "measurements.txt"), _log.ToString()); }
            catch (Exception) { }
            EditorApplication.Exit(_fails == 0 ? 0 : 1);
        }

        // ══ 1 · the flag is unset, which is every shipped run today ═════════════

        static void Off()
        {
            Head("1 · OFF — RUNWAY_LOCAL_LLM unset");
            Flags(null, null);

            Truth("the router is not active", !LocalLlmRouter.Active);
            Truth("and it describes itself as off", LocalLlmRouter.Describe() == "local off");

            Reply r = Ask(_keyless, "clarify", LlmClient.ClarifySchema);
            Truth("the request was passed through, not served",
                  LocalLlmRouter.Served == 0 && LocalLlmRouter.PassedThrough == 1);
            Truth("the keyless client answered it the way it always has — one null",
                  r.Calls == 1 && r.Data == null);
            Say("   note: " + LocalLlmRouter.LastNote);
        }

        // ══ 2 · the flag is set but nothing is standing behind it ═══════════════

        static void NoProvider()
        {
            Head("2 · NO PROVIDER — the flag is on, no backend is registered");
            Flags("1", null);

            Truth("the router is active", LocalLlmRouter.Active);
            Truth("but it has no provider", LocalLlmRouter.Provider == null);
            Truth("so it says so", LocalLlmRouter.Describe().Contains("no provider"));

            Reply r = Ask(_keyless, "clarify", LlmClient.ClarifySchema);
            Truth("a clarify request still goes to the network",
                  LocalLlmRouter.Served == 0 && LocalLlmRouter.PassedThrough == 1);
            Truth("and still comes back null on a keyless client",
                  r.Calls == 1 && r.Data == null);
            Say("   note: " + LocalLlmRouter.LastNote);
        }

        // ══ 3 · the canned deck ════════════════════════════════════════════════

        static void Deck()
        {
            Head("3 · THE DECK — RUNWAY_LOCAL_LLM=1 RUNWAY_LOCAL_LLM_CANNED=1");
            Flags("1", "1");

            var canned = LocalLlmRouter.Provider as LocalLlmCanned;
            Truth("the router built the canned backend from its own flag", canned != null);
            Say("   " + LocalLlmRouter.Describe());

            bool[] wanted = { true, false, true };
            for (int i = 0; i < wanted.Length; i++)
            {
                Reply r = Ask(_keyless, "clarify", LlmClient.ClarifySchema);
                Truth("call " + i + " · answered exactly once", r.Calls == 1);
                Truth("call " + i + " · answered synchronously, which the wire cannot do",
                      r.Sync);
                Truth("call " + i + " · carries an object", r.Data != null);
                if (r.Data == null) continue;

                var faults = new List<string>();
                bool fits = LocalJson.Fits(LlmClient.ClarifySchema, r.Data, faults);
                Truth("call " + i + " · fits ClarifySchema"
                      + (fits ? "" : " — " + string.Join("; ", faults.ToArray())), fits);

                bool needs = r.Data.Value<bool>("needs_clarification");
                string q = r.Data.Value<string>("question") ?? "";
                string kind = r.Data.Value<string>("kind") ?? "";
                Truth("call " + i + " · needs_clarification is " + wanted[i]
                      + " (the deck alternates by call index)", needs == wanted[i]);
                Say("      question(" + q.Length + "/90) \"" + q + "\"  kind=" + kind);
            }

            Truth("three served, none passed through, none rejected",
                  LocalLlmRouter.Served == 3 && LocalLlmRouter.PassedThrough == 0
                  && LocalLlmRouter.Rejected == 0);
            Truth("and the deck was dealt from exactly three times — the router adds no "
                  + "calls of its own and swallows none",
                  canned != null && canned.Calls == 3);
        }

        // ══ 4 · the network was not called ═════════════════════════════════════

        static void NotTheWire()
        {
            Head("4 · NOT THE WIRE — the same routing on a client that COULD call out");
            Flags("1", "1");

            Truth("this client is Enabled — the network path is open to it",
                  _keyed.Enabled);

            Reply r = Ask(_keyed, "clarify", LlmClient.ClarifySchema);
            Truth("the router took it", LocalLlmRouter.Served == 1
                  && LocalLlmRouter.LastServedTier == "clarify");
            Truth("the answer arrived inside RequestJson's own stack frame", r.Sync);
            Truth("carrying a parsed object", r.Data != null);
            Say("      → the network path hands off to StartCoroutine, and the earliest");
            Say("        `Send` can hold a PARSED object is past `while (!op.isDone)");
            Say("        yield return null` — a yield by construction, and one nothing");
            Say("        pumps here. A synchronous schema-valid object therefore has one");
            Say("        possible author. The control below shows what `Send` CAN do in");
            Say("        the same frame, and it is not this.");

            // the control: the tier the router does not own, on the same open client
            Reply a = Ask(_keyed, "assess", LlmClient.AdjudicateSchema);
            Truth("an assess request on the same client was NOT served locally",
                  LocalLlmRouter.Served == 1 && LocalLlmRouter.PassedThrough == 1);
            Truth("and no adjudication object came back from the local side",
                  a.Data == null);
            Say("   the assess callback fired " + a.Calls + " time(s) — either way it went "
                + "down LlmClient's own path: 0 is the network coroutine sitting unpumped "
                + "in edit mode, 1 is that coroutine's first segment refusing an unknown "
                + "provider. Neither is a local answer.");
            Say("   note: " + LocalLlmRouter.LastNote);
        }

        // ══ 5 · the permanent no-go ════════════════════════════════════════════

        static void NoGo()
        {
            Head("5 · THE NO-GO — `assess` is refused by the router, not by manners");
            Flags("1", null);
            var greedy = new Greedy();
            LocalLlmRouter.Register(greedy);

            Truth("this backend claims every tier, assess included",
                  greedy.Handles("assess") && greedy.Handles("clarify"));

            Ask(_keyless, "assess", LlmClient.AdjudicateSchema);
            Truth("the router refused it anyway", LocalLlmRouter.Served == 0
                  && LocalLlmRouter.PassedThrough == 1);
            Truth("the backend was never asked", greedy.Calls == 0);
            Say("   note: " + LocalLlmRouter.LastNote);

            Ask(_keyless, "clarify", LlmClient.ClarifySchema);
            Truth("and the same backend IS asked for clarify", greedy.Calls == 1);
        }

        // ══ 6 · the schema guard ═══════════════════════════════════════════════

        static void Guard()
        {
            Head("6 · THE GUARD — a local reply that does not fit is not handed up");
            Flags("1", null);
            var canned = new LocalLlmCanned();
            canned.Poison = true;
            LocalLlmRouter.Register(canned);
            Say("   " + LocalLlmRouter.Describe());

            Reply r = Ask(_keyless, "clarify", LlmClient.ClarifySchema);
            Truth("the router took the request", LocalLlmRouter.Served == 1);
            Truth("checked the reply and threw it out", LocalLlmRouter.Rejected == 1);
            Truth("game code got null — the authored path, which every caller has",
                  r.Calls == 1 && r.Data == null);
            Say("   faults: " + LocalLlmRouter.LastNote);

            // and the checker itself, on both sides, so the faults above are legible
            var good = new JObject
            {
                ["needs_clarification"] = true,
                ["question"] = "How much of the runway are you putting behind this?",
                ["kind"] = "amount",
            };
            var f1 = new List<string>();
            Truth("LocalJson.Fits accepts a well-formed clarify reply",
                  LocalJson.Fits(LlmClient.ClarifySchema, good, f1) && f1.Count == 0);

            var bad = new JObject
            {
                ["needs_clarification"] = "yes",              // wrong type
                ["question"] = new string('x', 120),          // over maxLength 90
                ["kind"] = "vibes",                           // outside the enum
                ["extra"] = 1,                                // additionalProperties:false
            };
            var f2 = new List<string>();
            Truth("and rejects a reply that breaks four keywords at once",
                  !LocalJson.Fits(LlmClient.ClarifySchema, bad, f2) && f2.Count >= 4);
            foreach (string f in f2) Say("      · " + f);

            // the union and the bounded array, exercised where the game actually uses them
            var effect = new JObject { ["op"] = "cash_delta", ["v"] = "a lot" };
            var f3 = new List<string>();
            JToken effSchema = LlmClient.EventSchema["properties"]["choices"]["items"]
                                        ["properties"]["effects"]["items"];
            Truth("the [\"number\",\"string\"] union takes a string",
                  LocalJson.Fits(effSchema, effect, f3) && f3.Count == 0);
            effect["v"] = -2500;
            var f4 = new List<string>();
            Truth("and takes a number", LocalJson.Fits(effSchema, effect, f4) && f4.Count == 0);
            effect["op"] = "set_vibes";
            var f5 = new List<string>();
            Truth("but not an op nobody declared",
                  !LocalJson.Fits(effSchema, effect, f5) && f5.Count == 1);
            Say("      · " + (f5.Count > 0 ? f5[0] : ""));
        }

        // ══ 7 · once only ══════════════════════════════════════════════════════

        static void OnceOnly()
        {
            Head("7 · ONCE ONLY — the guard the watchdog leans on");
            Flags("1", null);
            LocalLlmRouter.Register(new DoubleTalker());

            Reply r = Ask(_keyless, "clarify", LlmClient.ClarifySchema);
            Say("   the backend called its callback twice");
            Truth("game code was called exactly once", r.Calls == 1);
            Truth("with the FIRST answer, not the second",
                  r.Data != null && r.Data.Value<string>("kind") == "amount");
            Say("      → the watchdog and the backend race for the same cell; whichever");
            Say("        arrives first is the only one game code ever sees, so a late");
            Say("        reply cannot land on a turn that already moved on.");
        }

        // ══ the rig ════════════════════════════════════════════════════════════

        static void BuildRig()
        {
            _rig = new GameObject("~localrig");
            _rig.hideFlags = HideFlags.HideAndDontSave;

            _keyless = _rig.AddComponent<LlmClient>();
            _keyless.Provider = "";
            _keyless.ApiKey = "";

            // Enabled is (Provider != "" && ApiKey != ""), and `Send` recognises only
            // "openai" and "anthropic". A third name therefore opens the network path
            // fully while making it impossible for this probe to contact anything.
            _keyed = _rig.AddComponent<LlmClient>();
            _keyed.Provider = "probe";
            _keyed.ApiKey = "not-a-key";
            _keyed.Model = "none";

            Say("keyless client Enabled=" + _keyless.Enabled
                + " · keyed client Enabled=" + _keyed.Enabled
                + " (provider \"probe\", which reaches no host)");
        }

        static void Teardown()
        {
            LocalLlmRouter.Reset();
            Environment.SetEnvironmentVariable(LocalLlmRouter.Flag, null);
            Environment.SetEnvironmentVariable(LocalLlmCanned.Flag, null);
            if (_rig != null) UnityEngine.Object.DestroyImmediate(_rig);
        }

        /// Set the two switches and clear the router's memory of both, so each phase
        /// starts from zero counters and re-reads the environment.
        static void Flags(string local, string canned)
        {
            Environment.SetEnvironmentVariable(LocalLlmRouter.Flag, local);
            Environment.SetEnvironmentVariable(LocalLlmCanned.Flag, canned);
            LocalLlmRouter.Reset();
        }

        sealed class Reply
        {
            public int Calls;
            public JObject Data;
            public bool Sync;
        }

        /// One request through the real seam, recording whether the answer came back
        /// inside RequestJson's own frame.
        static Reply Ask(LlmClient c, string tier, JObject schema)
        {
            var r = new Reply();
            bool returned = false;
            var opts = new LlmOptions { Tier = tier };
            try
            {
                c.RequestJson("SYSTEM PROMPT", "{\"move\":\"probe\"}", schema,
                    d => { r.Calls++; r.Data = d; r.Sync = !returned; }, opts);
            }
            catch (Exception e)
            {
                // StartCoroutine outside play mode is inert; if a Unity version ever
                // makes it throw instead, that is the network path refusing to run,
                // which is still not a local answer.
                Say("   (RequestJson threw on the fall-through path: " + e.Message + ")");
            }
            returned = true;
            return r;
        }

        // ── two backends that exist only to be misbehaved ───────────────────────

        /// Claims every tier, so the router's own refusal is what gets tested rather
        /// than a well-mannered provider's.
        sealed class Greedy : ILocalCompletion
        {
            public int Calls;
            public bool Ready { get { return true; } }
            public bool Handles(string tier) { return true; }
            public string Describe() { return "greedy (claims every tier)"; }
            public void Cancel() { }
            public void Complete(string sys, string user, JObject schema, string tier,
                                 int maxTokens, Action<JObject> cb)
            {
                Calls++;
                if (cb != null)
                    cb(new JObject
                    {
                        ["needs_clarification"] = false,
                        ["question"] = "",
                        ["kind"] = "other",
                    });
            }
        }

        /// Answers twice — a stream that completes after its own watchdog already gave
        /// up, or a backend with a retry that forgot it had one.
        sealed class DoubleTalker : ILocalCompletion
        {
            public bool Ready { get { return true; } }
            public bool Handles(string tier) { return tier == LocalLlmRouter.PilotTier; }
            public string Describe() { return "double talker"; }
            public void Cancel() { }
            public void Complete(string sys, string user, JObject schema, string tier,
                                 int maxTokens, Action<JObject> cb)
            {
                if (cb == null) return;
                cb(new JObject
                {
                    ["needs_clarification"] = true,
                    ["question"] = "How much of the runway are you putting behind this?",
                    ["kind"] = "amount",
                });
                cb(new JObject
                {
                    ["needs_clarification"] = true,
                    ["question"] = "And who is doing it?",
                    ["kind"] = "resource",
                });
            }
        }

        // ══ the paperwork ══════════════════════════════════════════════════════

        static void Head(string title)
        {
            Say("");
            Say(title);
        }

        static void Truth(string what, bool ok)
        {
            _checks++;
            if (!ok) _fails++;
            Say((ok ? "   ok   " : "   FAIL ") + what);
        }

        static void Say(string line)
        {
            Debug.Log("LOCAL: " + line);
            _log.Append(line).Append('\n');
        }
    }
}
