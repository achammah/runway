using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// THE JSON CONTENT PIPELINE — content_db.gd, ported. Items, archetypes and the
    /// authored event deck, loaded once from StreamingAssets, plus the eligibility
    /// query that answers "which authored cards could happen to this company now".
    ///
    /// IT IS ALSO THE HOST'S FILE READER. Runway.Core reads nothing itself: it asks
    /// for a logical name and the host answers. Installing that reader is this
    /// class's other job, and it happens the first time anything asks for content —
    /// so the engine can look up items.json from inside a trait calculation without
    /// the run lane having been started yet.
    ///
    /// Everything is JObject rather than a typed model on purpose: the LLM hands
    /// back cards in exactly this shape, so an authored card and a generated card
    /// are the same object to every screen that draws one.
    /// </summary>
    public sealed class ContentDb
    {
        public readonly Dictionary<string, JObject> Items = new Dictionary<string, JObject>();
        public readonly Dictionary<string, JObject> Events = new Dictionary<string, JObject>();
        public readonly List<JObject> ItemList = new List<JObject>();
        public JArray Archetypes = new JArray();
        public JArray Fundings = new JArray();

        static bool _readerInstalled;

        /// StreamingAssets is where Core's logical names resolve. Idempotent.
        public static void InstallCoreReader()
        {
            if (_readerInstalled) return;
            _readerInstalled = true;
            CoreFiles.Reader = name => RunwayPaths.ReadAllTextOrEmpty(RunwayPaths.Streaming(name));
            GameState.ResetItemTraitTable();
        }

        public void LoadAll()
        {
            InstallCoreReader();
            Items.Clear();
            Events.Clear();
            ItemList.Clear();

            JObject idata = ReadJson("items.json");
            if (idata != null)
            {
                var arr = idata["items"] as JArray;
                if (arr != null)
                {
                    foreach (JToken t in arr)
                    {
                        var it = t as JObject;
                        if (it == null) continue;
                        string id = Str(it, "id");
                        if (id.Length == 0) continue;
                        Items[id] = it;
                        ItemList.Add(it);
                    }
                }
            }

            JObject adata = ReadJson("archetypes.json");
            if (adata != null)
            {
                Archetypes = adata["archetypes"] as JArray ?? new JArray();
                Fundings = adata["fundings"] as JArray ?? new JArray();
            }

            LoadEventFolder();
            Debug.Log(string.Format("RUNWAY! content: {0} items, {1} events, {2} archetypes",
                Items.Count, Events.Count, Archetypes.Count));
        }

        /// The deck ships as one file per era band. The folder is enumerated where it
        /// can be (editor, macOS player) and falls back to the shipped names, so a
        /// platform with no readable StreamingAssets directory still deals cards.
        static readonly string[] KnownDecks =
        {
            "authored_core.json", "founding_traps.json", "garage_extra.json",
            "coworking.json", "office.json", "floor_hq.json", "opportunities.json",
        };

        void LoadEventFolder()
        {
            var names = new List<string>();
            try
            {
                string dir = RunwayPaths.Streaming("events");
                if (Directory.Exists(dir))
                {
                    string[] files = Directory.GetFiles(dir, "*.json");
                    for (int i = 0; i < files.Length; i++) names.Add(Path.GetFileName(files[i]));
                }
            }
            catch (Exception) { /* unreadable folder: the known names below stand */ }
            if (names.Count == 0) names.AddRange(KnownDecks);

            for (int i = 0; i < names.Count; i++)
            {
                JObject doc = ReadJson("events/" + names[i]);
                if (doc == null) continue;
                var arr = doc["events"] as JArray;
                if (arr == null) continue;
                foreach (JToken t in arr)
                {
                    var ev = t as JObject;
                    if (ev == null) continue;
                    string id = Str(ev, "id");
                    if (id.Length == 0) continue;
                    if (ev["tier"] == null) ev["tier"] = "authored";
                    Events[id] = ev;
                }
            }
        }

        static JObject ReadJson(string relative)
        {
            string txt = RunwayPaths.ReadAllTextOrEmpty(RunwayPaths.Streaming(relative));
            if (txt.Trim().Length == 0)
            {
                Debug.LogWarning("RUNWAY! content file missing: " + relative);
                return null;
            }
            try { return JObject.Parse(txt); }
            catch (Exception e)
            {
                Debug.LogError("RUNWAY! content file will not parse (" + relative + "): " + e.Message);
                return null;
            }
        }

        // ── the draw pool ──────────────────────────────────────────────────────

        /// Authored cards whose requirements match right now. weight 0 means the card
        /// is only reachable through a timebomb or a weight_future, never a draw.
        public List<JObject> EligibleEvents(GameState state)
        {
            var pool = new List<JObject>();
            if (state == null) return pool;
            foreach (JObject ev in Events.Values)
            {
                if (Num(ev, "weight", 1.0) <= 0.0) continue;
                var eras = ev["era"] as JArray;
                bool eraOk = false;
                if (eras == null || eras.Count == 0) eraOk = state.Era == "garage";
                else
                {
                    foreach (JToken e in eras)
                        if (e.ToString() == state.Era) { eraOk = true; break; }
                }
                if (!eraOk) continue;
                if (RequiresOk(ev["requires"] as JObject, state)) pool.Add(ev);
            }
            return pool;
        }

        static bool RequiresOk(JObject req, GameState state)
        {
            if (req == null) return true;
            var itemsAny = req["items_any"] as JArray;
            if (itemsAny != null)
            {
                bool hit = false;
                foreach (JToken t in itemsAny) if (state.HasItem(t.ToString())) hit = true;
                if (!hit) return false;
            }
            var flagsAll = req["flags_all"] as JArray;
            if (flagsAll != null)
                foreach (JToken t in flagsAll) if (!state.HasFlag(t.ToString())) return false;
            var flagsNone = req["flags_none"] as JArray;
            if (flagsNone != null)
                foreach (JToken t in flagsNone) if (state.HasFlag(t.ToString())) return false;

            if (Has(req, "cash_lte") && state.Cash > Int(req, "cash_lte", 0)) return false;
            if (Has(req, "cash_gte") && state.Cash < Int(req, "cash_gte", 0)) return false;
            if (Has(req, "product_gte") && state.Product < Int(req, "product_gte", 0)) return false;
            if (Has(req, "morale_lte") && state.Morale > Int(req, "morale_lte", 0)) return false;
            if (Has(req, "traction_gte") && state.Traction < Int(req, "traction_gte", 0)) return false;
            if (Has(req, "hype_gte") && state.Hype < Int(req, "hype_gte", 0)) return false;
            if (Has(req, "week_gte") && state.Week < Int(req, "week_gte", 0)) return false;
            if (Has(req, "staff_gte") && state.Employees.Count < Int(req, "staff_gte", 0)) return false;
            return true;
        }

        /// event_generator.gd's next_card() authored half: a weighted draw, with any
        /// card the world has already promised (weight_future) jumping the queue.
        public JObject DrawAuthored(GameState state, Rng rng)
        {
            List<JObject> eligible = EligibleEvents(state);
            if (eligible.Count == 0) return null;
            // NEVER THE SAME CARD TWICE while anything fresh remains: the live
            // probe watched week 3 re-deal week 1's card the moment the
            // generated pool went dry. (The Godot build carries the same
            // latent draw; its keyed pool cadence merely hid it.)
            if (state.PlayedEvents != null && state.PlayedEvents.Count > 0)
            {
                var fresh = new List<JObject>();
                for (int i = 0; i < eligible.Count; i++)
                    if (!state.PlayedEvents.Contains(Str(eligible[i], "title")))
                        fresh.Add(eligible[i]);
                if (fresh.Count > 0) eligible = fresh;
            }
            for (int i = 0; i < eligible.Count; i++)
            {
                string id = Str(eligible[i], "id");
                if (state.FutureWeights.Contains(id))
                {
                    state.FutureWeights.Remove(id);
                    return eligible[i];
                }
            }
            double total = 0.0;
            for (int i = 0; i < eligible.Count; i++) total += Math.Max(Num(eligible[i], "weight", 1.0), 0.0);
            if (total <= 0.0) return eligible[0];
            double roll = (rng != null ? rng.Randf() : UnityEngine.Random.value) * total;
            for (int i = 0; i < eligible.Count; i++)
            {
                roll -= Math.Max(Num(eligible[i], "weight", 1.0), 0.0);
                if (roll <= 0.0) return eligible[i];
            }
            return eligible[eligible.Count - 1];
        }

        // ── lookups the screens ask for ────────────────────────────────────────

        public JObject Item(string id)
        {
            JObject it;
            return id != null && Items.TryGetValue(id, out it) ? it : null;
        }

        public string ItemName(string id)
        {
            JObject it = Item(id);
            if (it != null) return Str(it, "name", id);
            if (string.IsNullOrEmpty(id)) return "";
            string s = id.Replace("itm_", "").Replace("_", " ");
            return s.Length == 0 ? id : char.ToUpper(s[0]) + s.Substring(1);
        }

        public int CarryCost(string id)
        {
            JObject it = Item(id);
            return it == null ? 1 : Int(it, "carry_cost", 1);
        }

        public int CashValue(string id)
        {
            JObject it = Item(id);
            return it == null ? 0 : Int(it, "cash_value", 0);
        }

        // ── tiny JSON readers, so no screen writes its own ─────────────────────

        public static bool Has(JObject o, string key)
        {
            return o != null && o[key] != null && o[key].Type != JTokenType.Null;
        }

        public static string Str(JObject o, string key, string fallback = "")
        {
            if (!Has(o, key)) return fallback;
            return o[key].ToString();
        }

        public static int Int(JObject o, string key, int fallback)
        {
            if (!Has(o, key)) return fallback;
            int v;
            if (int.TryParse(o[key].ToString(), out v)) return v;
            double d;
            if (double.TryParse(o[key].ToString(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out d))
                return (int)Math.Round(d);
            return fallback;
        }

        public static double Num(JObject o, string key, double fallback)
        {
            if (!Has(o, key)) return fallback;
            double d;
            return double.TryParse(o[key].ToString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out d) ? d : fallback;
        }

        public static bool Flag(JObject o, string key, bool fallback = false)
        {
            if (!Has(o, key)) return fallback;
            bool b;
            return bool.TryParse(o[key].ToString(), out b) ? b : fallback;
        }
    }
}
