using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Runway.Llm
{
    /// Per-request knobs — the `opts` Dictionary llm_client.gd takes.
    public struct LlmOptions
    {
        /// "assess" (terra, the deepest judgment) or "clarify" (luna, one cheap question).
        public string Tier;
        /// Tier-3 run direction: the optionally stronger director model.
        public bool Director;
        /// anthropic only; a 180-word narration plus the other fields does not fit in 700.
        public int MaxTokens;

        public static LlmOptions Assess { get { return new LlmOptions { Tier = "assess" }; } }
        public static LlmOptions Clarify { get { return new LlmOptions { Tier = "clarify" }; } }
    }

    /// <summary>
    /// Async LLM client for the Simulation Engine — llm_client.gd, ported.
    /// Provider-agnostic: OpenAI (chat completions + json_schema response_format) or
    /// Anthropic (messages + output_config json_schema). The key comes from the layered
    /// env; the game runs fully without one.
    ///
    /// Callback-based: each request is its own UnityWebRequest, so event prefetch and
    /// free-move adjudication run in parallel.
    /// </summary>
    public sealed class LlmClient : MonoBehaviour
    {
        public string Provider = "";
        public string ApiKey = "";
        public string Model = "";
        public string AssessModel = "";
        public string ClarifyModel = "";
        public string DirectorModel = "";

        public const string OpenAiUrl = "https://api.openai.com/v1/chat/completions";
        public const string AnthropicUrl = "https://api.anthropic.com/v1/messages";

        /// 35s cut off real terra founding calls in the shipped app (the book showed an
        /// empty entry and settle-in paid the whole call again). 90s is the cap; the
        /// beat/curtain narrate the wait.
        public const int TimeoutSeconds = 90;

        // ══ SCHEMAS — ported verbatim from llm_client.gd ═══════════════════════

        /// Schema for generated event cards (shared shape with authored cards).
        public static readonly JObject EventSchema = JObject.Parse(@"{
          ""type"": ""object"",
          ""additionalProperties"": false,
          ""required"": [""title"", ""body"", ""choices""],
          ""properties"": {
            ""title"": {""type"": ""string"", ""maxLength"": 60},
            ""body"": {""type"": ""string"", ""maxLength"": 420},
            ""choices"": {
              ""type"": ""array"", ""minItems"": 2, ""maxItems"": 4,
              ""items"": {
                ""type"": ""object"",
                ""additionalProperties"": false,
                ""required"": [""label"", ""effects""],
                ""properties"": {
                  ""label"": {""type"": ""string"", ""maxLength"": 48},
                  ""effects"": {
                    ""type"": ""array"", ""minItems"": 1, ""maxItems"": 4,
                    ""items"": {
                      ""type"": ""object"",
                      ""additionalProperties"": false,
                      ""required"": [""op"", ""v""],
                      ""properties"": {
                        ""op"": {""type"": ""string"", ""enum"": [""cash_delta"", ""product_delta"", ""traction_delta"", ""morale_delta"", ""hype_delta"", ""set_flag""]},
                        ""v"": {""type"": [""number"", ""string""]}
                      }
                    }
                  }
                }
              }
            }
          }
        }");

        /// Schema for adjudicating a player's free-form move. ONE CALL RETURNS THE WHOLE
        /// TURN: the text the player reads while the art renders, AND everything needed
        /// to build the scene.
        public static readonly JObject AdjudicateSchema = JObject.Parse(@"{
          ""type"": ""object"",
          ""additionalProperties"": false,
          ""required"": [""interpreted_as"", ""reality_check"", ""narration"", ""verdict"", ""effects"",
            ""headline"", ""scene"", ""cast"", ""roll"", ""traits"", ""memory"", ""journal_note""],
          ""properties"": {
            ""interpreted_as"": {""type"": ""string"", ""maxLength"": 160},
            ""reality_check"": {""type"": ""string"", ""maxLength"": 240},
            ""narration"": {""type"": ""string"", ""maxLength"": 2400},
            ""verdict"": {""type"": ""string"", ""enum"": [""brilliant"", ""fine"", ""risky"", ""backfired""]},
            ""headline"": {""type"": ""string"", ""maxLength"": 90},
            ""roll"": {
              ""type"": ""object"", ""additionalProperties"": false,
              ""required"": [""stat"", ""dc""],
              ""properties"": {
                ""stat"": {""type"": ""string"", ""enum"": [""build"", ""sell"", ""raise"", ""recruit"", ""grit""]},
                ""dc"": {""type"": ""integer"", ""minimum"": 2, ""maximum"": 19}
              }
            },
            ""scene"": {
              ""type"": ""object"", ""additionalProperties"": false,
              ""required"": [""family"", ""place"", ""time"", ""condition"", ""framing"", ""novel_place"", ""beat""],
              ""properties"": {
                ""family"": {""type"": ""string"", ""enum"": [""home_retreat"", ""scrappy_workspace"",
                  ""legit_workspace"", ""money"", ""customer"", ""institutional"", ""transit"",
                  ""social"", ""body_mind"", ""endings""]},
                ""place"": {""type"": ""string"", ""maxLength"": 40},
                ""time"": {""type"": ""string"", ""enum"": [""day"", ""night"", ""small_hours""]},
                ""condition"": {""type"": ""string"", ""enum"": [""thriving"", ""steady"", ""in_the_red""]},
                ""framing"": {""type"": ""string"", ""enum"": [""wide"", ""medium""]},
                ""novel_place"": {""type"": ""string"", ""maxLength"": 220},
                ""beat"": {""type"": ""string"", ""maxLength"": 160}
              }
            },
            ""traits"": {
              ""type"": ""array"", ""minItems"": 0, ""maxItems"": 3,
              ""items"": {""type"": ""string"", ""enum"": [""long_term"", ""short_term"",
                ""risk_taker"", ""risk_averse"", ""data_driven"", ""intuition_driven"",
                ""quality_focused"", ""speed_focused"", ""hands_on"", ""delegator"",
                ""collaborative"", ""independent"", ""diplomatic"", ""confrontational""]}
            },
            ""memory"": {""type"": ""string"", ""maxLength"": 1200},
            ""journal_note"": {""type"": ""string"", ""maxLength"": 220},
            ""cast"": {
              ""type"": ""array"", ""minItems"": 0, ""maxItems"": 5,
              ""items"": {
                ""type"": ""object"", ""additionalProperties"": false,
                ""required"": [""who"", ""mood"", ""doing""],
                ""properties"": {
                  ""who"": {""type"": ""string"", ""enum"": [""founder"", ""sales"", ""business"", ""tech"",
                    ""hustler"", ""idea_friend""]},
                  ""mood"": {""type"": ""string"", ""enum"": [""fine"", ""burnt"", ""gone""]},
                  ""doing"": {""type"": ""string"", ""maxLength"": 70}
                }
              }
            },
            ""effects"": {
              ""type"": ""array"", ""minItems"": 0, ""maxItems"": 4,
              ""items"": {
                ""type"": ""object"",
                ""additionalProperties"": false,
                ""required"": [""op"", ""v"", ""why"", ""weeks"", ""cat""],
                ""properties"": {
                  ""op"": {""type"": ""string"", ""enum"": [""cash_delta"", ""product_delta"",
                    ""traction_delta"", ""morale_delta"", ""hype_delta"", ""set_flag"",
                    ""status"", ""clock"", ""set_price"", ""set_marketing"", ""hire"", ""take_loan"",
                    ""spend"", ""set_budget""]},
                  ""v"": {""type"": [""number"", ""string""]},
                  ""why"": {""type"": ""string"", ""maxLength"": 90},
                  ""weeks"": {""type"": ""integer"", ""minimum"": 1, ""maximum"": 12},
                  ""cat"": {""type"": ""string"", ""enum"": ["""", ""marketing"", ""sales"", ""care"", ""rnd"", ""one_off""]}
                }
              }
            }
          }
        }");

        /// Schema for the clarify pre-pass (luna): one reluctant follow-up question.
        public static readonly JObject ClarifySchema = JObject.Parse(@"{
          ""type"": ""object"", ""additionalProperties"": false,
          ""required"": [""needs_clarification"", ""question"", ""kind""],
          ""properties"": {
            ""needs_clarification"": {""type"": ""boolean""},
            ""question"": {""type"": ""string"", ""maxLength"": 90},
            ""kind"": {""type"": ""string"", ""enum"": [""amount"", ""target"", ""resource"", ""price"", ""other""]}
          }
        }");

        /// Schema for run-start world generation: the bible born from the pitch.
        public static readonly JObject WorldSchema = JObject.Parse(@"{
          ""type"": ""object"", ""additionalProperties"": false,
          ""required"": [""market"", ""investors"", ""rivals""],
          ""properties"": {
            ""market"": {
              ""type"": ""object"", ""additionalProperties"": false,
              ""required"": [""tam_buyers"", ""customer_patience_weeks"", ""one_liner""],
              ""properties"": {
                ""tam_buyers"": {""type"": ""integer"", ""minimum"": 2000, ""maximum"": 5000000},
                ""customer_patience_weeks"": {""type"": ""integer"", ""minimum"": 6, ""maximum"": 200},
                ""one_liner"": {""type"": ""string"", ""maxLength"": 140}
              }
            },
            ""investors"": {
              ""type"": ""array"", ""minItems"": 3, ""maxItems"": 3,
              ""items"": {
                ""type"": ""object"", ""additionalProperties"": false,
                ""required"": [""name"", ""archetype"", ""thesis"", ""trait"", ""bond"", ""flaw"", ""secret""],
                ""properties"": {
                  ""name"": {""type"": ""string"", ""maxLength"": 40},
                  ""archetype"": {""type"": ""string"", ""enum"": [""the momentum fund"",
                    ""the contrarian angel"", ""the operator VC"", ""the shark"", ""the thesis tourist""]},
                  ""thesis"": {""type"": ""string"", ""maxLength"": 200},
                  ""trait"": {""type"": ""string"", ""maxLength"": 80},
                  ""bond"": {""type"": ""string"", ""maxLength"": 90},
                  ""flaw"": {""type"": ""string"", ""maxLength"": 80},
                  ""secret"": {""type"": ""string"", ""maxLength"": 90}
                }
              }
            },
            ""rivals"": {
              ""type"": ""array"", ""minItems"": 2, ""maxItems"": 2,
              ""items"": {
                ""type"": ""object"", ""additionalProperties"": false,
                ""required"": [""name"", ""what_they_do"", ""strength"", ""tactics""],
                ""properties"": {
                  ""name"": {""type"": ""string"", ""maxLength"": 30},
                  ""what_they_do"": {""type"": ""string"", ""maxLength"": 140},
                  ""strength"": {""type"": ""string"", ""enum"": [""struggling"", ""scrappy"", ""strong"", ""dominant""]},
                  ""tactics"": {""type"": ""array"", ""minItems"": 3, ""maxItems"": 3,
                    ""items"": {""type"": ""string"", ""maxLength"": 60}}
                }
              }
            }
          }
        }");

        /// Schema for the Tier-3 run director: the run's narrative arcs.
        public static readonly JObject ArcSchema = JObject.Parse(@"{
          ""type"": ""object"",
          ""additionalProperties"": false,
          ""required"": [""arcs""],
          ""properties"": {
            ""arcs"": {
              ""type"": ""array"", ""minItems"": 1, ""maxItems"": 3,
              ""items"": {
                ""type"": ""object"",
                ""additionalProperties"": false,
                ""required"": [""arc_id"", ""kind"", ""premise"", ""actors"", ""beats"", ""escalation_rule""],
                ""properties"": {
                  ""arc_id"": {""type"": ""string"", ""maxLength"": 40},
                  ""kind"": {""type"": ""string"", ""enum"": [""rival"", ""press"", ""cofounder"", ""investor"", ""customer""]},
                  ""premise"": {""type"": ""string"", ""maxLength"": 240},
                  ""actors"": {""type"": ""array"", ""minItems"": 1, ""maxItems"": 3, ""items"": {""type"": ""string"", ""maxLength"": 60}},
                  ""beats"": {
                    ""type"": ""array"", ""minItems"": 1, ""maxItems"": 5,
                    ""items"": {
                      ""type"": ""object"",
                      ""additionalProperties"": false,
                      ""required"": [""era"", ""directive""],
                      ""properties"": {
                        ""era"": {""type"": ""string"", ""enum"": [""garage"", ""coworking"", ""office"", ""floor"", ""hq""]},
                        ""directive"": {""type"": ""string"", ""maxLength"": 200}
                      }
                    }
                  },
                  ""escalation_rule"": {""type"": ""string"", ""maxLength"": 160}
                }
              }
            }
          }
        }");

        // ══ setup ══════════════════════════════════════════════════════════════

        public void Setup(Dictionary<string, string> env)
        {
            string openaiKey = Val(env, "OPENAI_API_KEY");
            string anthropicKey = Val(env, "ANTHROPIC_API_KEY");
            Provider = Val(env, "LLM_PROVIDER");
            if (Provider == "")
            {
                if (openaiKey != "") Provider = "openai";
                else if (anthropicKey != "") Provider = "anthropic";
            }
            ApiKey = "";
            Model = "";
            AssessModel = "";
            ClarifyModel = "";
            switch (Provider)
            {
                case "openai":
                    ApiKey = openaiKey;
                    // Default measured head-to-head on this exact prompt and schema, twice
                    // each. The adjudication gates the whole week, so the faster
                    // equal-quality model wins.
                    Model = Val(env, "OPENAI_MODEL", "gpt-5.6-luna");
                    // THE TWO-TIER SPLIT (owner): the ASSESSMENT runs terra — the deepest
                    // judgment in the game; the CLARIFY pre-pass runs luna — one cheap
                    // question, speed is the feature.
                    AssessModel = Val(env, "OPENAI_ASSESS_MODEL", "gpt-5.6-terra");
                    ClarifyModel = Val(env, "OPENAI_CLARIFY_MODEL", "gpt-5.6-luna");
                    break;
                case "anthropic":
                    ApiKey = anthropicKey;
                    Model = Val(env, "ANTHROPIC_MODEL", "claude-haiku-4-5-20251001");
                    break;
            }
            switch (Provider)
            {
                case "openai":
                    DirectorModel = Val(env, "OPENAI_DIRECTOR_MODEL", Model);
                    break;
                case "anthropic":
                    DirectorModel = Val(env, "ANTHROPIC_DIRECTOR_MODEL", Model);
                    break;
                default:
                    DirectorModel = Model;
                    break;
            }
            if (DirectorModel == "") DirectorModel = Model;
            if (ApiKey == "") Provider = "";
        }

        static string Val(Dictionary<string, string> env, string key, string fallback = "")
        {
            string v;
            if (env != null && env.TryGetValue(key, out v) && !string.IsNullOrEmpty(v)) return v;
            return fallback;
        }

        public bool Enabled { get { return Provider != "" && ApiKey != ""; } }

        string ModelFor(LlmOptions opts)
        {
            if (opts.Director) return DirectorModel;
            switch (opts.Tier)
            {
                case "assess": return AssessModel != "" ? AssessModel : Model;
                case "clarify": return ClarifyModel != "" ? ClarifyModel : Model;
            }
            return Model;
        }

        /// assessment = terra FAST (deep judgment, still on the week's critical path);
        /// clarify = luna NORMAL (cheap, one question, no need for the fast lane).
        string ServiceTierFor(LlmOptions opts)
        {
            if (opts.Tier == "clarify") return "default";
            string over = Runway.App.Env.Get("RUNWAY_LLM_TIER", "");
            if (over.Length > 0) return over;
            return "fast";
        }

        // ══ the request ════════════════════════════════════════════════════════

        /// Fire an async structured request. cb receives the parsed object (null on
        /// failure). Never blocks; each call is independent.
        public void RequestJson(string systemPrompt, string userPrompt, JObject schema,
                                Action<JObject> cb)
        {
            RequestJson(systemPrompt, userPrompt, schema, cb, default(LlmOptions));
        }

        public void RequestJson(string systemPrompt, string userPrompt, JObject schema,
                                Action<JObject> cb, LlmOptions opts)
        {
            // THE LOCAL SEAM (RUNWAY_LOCAL_LLM=1). It sits ABOVE the Enabled gate on
            // purpose: a local backend's whole point is a run with no key, and this
            // request is the one it is allowed to take. It returns false for every tier
            // and every flag state it does not own — with the flag unset that is one
            // string compare — so an ordinary run falls straight through into the
            // network path below, unchanged.
            if (LocalLlmRouter.TryServe(this, systemPrompt, userPrompt, schema, cb, opts)) return;
            if (!Enabled)
            {
                if (cb != null) cb(null);
                return;
            }
            StartCoroutine(Send(systemPrompt, userPrompt, schema, cb, opts));
        }

        IEnumerator Send(string systemPrompt, string userPrompt, JObject schema,
                         Action<JObject> cb, LlmOptions opts)
        {
            string url;
            string payload;
            var headers = new List<KeyValuePair<string, string>>();
            headers.Add(new KeyValuePair<string, string>("Content-Type", "application/json"));

            if (Provider == "openai")
            {
                url = OpenAiUrl;
                headers.Add(new KeyValuePair<string, string>("Authorization", "Bearer " + ApiKey));
                var body = new JObject
                {
                    ["model"] = ModelFor(opts),
                    ["messages"] = new JArray
                    {
                        new JObject { ["role"] = "system", ["content"] = systemPrompt },
                        new JObject { ["role"] = "user", ["content"] = userPrompt },
                    },
                    ["response_format"] = new JObject
                    {
                        ["type"] = "json_schema",
                        ["json_schema"] = new JObject
                        {
                            ["name"] = "structured_reply",
                            ["strict"] = true,
                            ["schema"] = schema,
                        },
                    },
                    // FAST MODE. The adjudication sits on the critical path of every week:
                    // measured on this exact prompt and schema, about half the latency with
                    // no change in output. Set RUNWAY_LLM_TIER=standard to opt out.
                    ["service_tier"] = ServiceTierFor(opts),
                };
                payload = body.ToString(Formatting.None);
            }
            else if (Provider == "anthropic")
            {
                url = AnthropicUrl;
                headers.Add(new KeyValuePair<string, string>("x-api-key", ApiKey));
                headers.Add(new KeyValuePair<string, string>("anthropic-version", "2023-06-01"));
                var body = new JObject
                {
                    ["model"] = ModelFor(opts),
                    ["max_tokens"] = opts.MaxTokens > 0 ? opts.MaxTokens : 1400,
                    ["system"] = systemPrompt,
                    ["messages"] = new JArray
                    {
                        new JObject { ["role"] = "user", ["content"] = userPrompt },
                    },
                    ["output_config"] = new JObject
                    {
                        ["format"] = new JObject
                        {
                            ["type"] = "json_schema",
                            ["schema"] = schema,
                        },
                    },
                };
                payload = body.ToString(Formatting.None);
            }
            else
            {
                if (cb != null) cb(null);
                yield break;
            }

            var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            req.downloadHandler = new DownloadHandlerBuffer();
            for (int i = 0; i < headers.Count; i++)
                req.SetRequestHeader(headers[i].Key, headers[i].Value);
            // TWO CLOCKS (the Godot build's #175 lesson, ported): the soft
            // timeout below is the engine's own; the hard watchdog races it
            // because a wedged socket was caught sleeping straight through a
            // soft clock — the book waited forever on a founding that was
            // never coming. founding/clarify are prose on the fast lane and
            // must die fast enough for the caller's retry to land in-wait.
            string tier = opts.Tier ?? "";
            float wd = (tier == "founding" || tier == "clarify") ? 50f : 100f;
            req.timeout = Mathf.Max(5, (int)wd - 5);

            var op = req.SendWebRequest();
            float t0 = Time.realtimeSinceStartup;
            bool wedged = false;
            while (!op.isDone)
            {
                if (Time.realtimeSinceStartup - t0 > wd)
                {
                    wedged = true;
                    Debug.Log(string.Format(
                        "LLM WATCHDOG fired after {0:0}s — aborting the wedged request", wd));
                    req.Abort();
                    break;
                }
                yield return null;
            }
            if (wedged)
            {
                // one more frame so the abort settles before Dispose
                yield return null;
                req.Dispose();
                if (cb != null) cb(null);
                yield break;
            }

            long code = req.responseCode;
            bool ok = req.result == UnityWebRequest.Result.Success;
            string text = "";
            try { text = req.downloadHandler != null ? req.downloadHandler.text : ""; }
            catch (Exception) { text = ""; }
            string err = req.error;
            req.Dispose();

            if (!ok || code < 200L || code >= 300L)
            {
                // print, not a warning: release builds swallow warnings, and this line is
                // the only witness a shipped session gets
                Debug.Log(string.Format("LLM request FAILED (result={0} http={1}): {2}",
                    err, code, Left(text, 300)));
                if (cb != null) cb(null);
                yield break;
            }

            JObject parsed = TryParse(text);
            if (parsed == null)
            {
                Debug.Log("LLM reply envelope would not parse (" + Left(text, 120) + ")");
                if (cb != null) cb(null);
                yield break;
            }

            string content = "";
            if (Provider == "openai")
            {
                var choices = parsed["choices"] as JArray;
                if (choices != null && choices.Count > 0)
                {
                    JToken msg = choices[0]["message"];
                    if (msg != null && msg["content"] != null) content = msg["content"].ToString();
                }
            }
            else
            {
                var blocks = parsed["content"] as JArray;
                if (blocks != null)
                {
                    foreach (JToken block in blocks)
                    {
                        if (block["type"] != null && block["type"].ToString() == "text"
                            && block["text"] != null)
                            content += block["text"].ToString();
                    }
                }
            }

            JObject data = TryParse(content);
            if (data == null)
                Debug.Log("LLM content was not the schema'd JSON (" + Left(content, 120) + ")");
            if (cb != null) cb(data);
        }

        public static JObject TryParse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            try { return JObject.Parse(text); }
            catch (Exception) { return null; }
        }

        static string Left(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= n ? s : s.Substring(0, n);
        }
    }
}
