using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Runway.App
{
    /// <summary>
    /// THREE SLOTS, with last-played times on the title — save_system.gd's slot half.
    /// Only the METADATA lives here: the run payload belongs to the lane that owns
    /// GameState, and it writes into the same file under "state" and "record".
    ///
    /// The files sit beside the Godot build's, with a .unity suffix, so an owner-run
    /// side-by-side shares one api key and never overwrites the other build's
    /// companies.
    /// </summary>
    public static class SaveSlots
    {
        public const int SlotCount = 3;
        public const int Version = 2;

        public static int ActiveSlot = 1;

        public static string Path(int slot)
        {
            int s = Mathf.Clamp(slot, 1, SlotCount);
            return RunwayPaths.User(string.Format("run_slot_{0}.unity.json", s));
        }

        public static bool Exists(int slot)
        {
            try { return File.Exists(Path(slot)); }
            catch (Exception) { return false; }
        }

        /// One row for the slot table. A file that will not parse reads as empty —
        /// a corrupt save must look like a free desk, never take the title down.
        public static SaveSlotInfo Read(int slot)
        {
            var row = new SaveSlotInfo { Slot = slot, Exists = false };
            string txt = RunwayPaths.ReadAllTextOrEmpty(Path(slot));
            if (txt.Length == 0) return row;
            try
            {
                JObject doc = JObject.Parse(txt);
                JObject meta = doc["meta"] as JObject;
                JObject state = doc["state"] as JObject;
                // a state-less file parses but cannot LOAD: claiming Exists
                // made CONTINUE silently start a fresh run over that slot
                if (state == null) return row;
                row.Exists = true;
                row.Company = Str(meta, "company", Str(state, "company_name", "a company"));
                row.Founder = Str(meta, "founder", Str(state, "founder_name", ""));
                row.Week = Int(meta, "week", Int(state, "week", 0));
                row.Timestamp = Long(meta, "ts", 0L);
            }
            catch (Exception e)
            {
                Debug.LogWarning("RUNWAY! slot " + slot + " will not parse (" + e.Message
                                 + ") — showing it as an empty desk.");
                row.Exists = false;
            }
            return row;
        }

        public static void Clear(int slot)
        {
            try { if (File.Exists(Path(slot))) File.Delete(Path(slot)); }
            catch (Exception e) { Debug.LogWarning("RUNWAY! cannot clear slot " + slot + ": " + e.Message); }
        }

        /// The meta block a run save must carry for the title to read it.
        public static JObject Meta(string company, string founder, int week)
        {
            return new JObject
            {
                ["company"] = company ?? "",
                ["founder"] = founder ?? "",
                ["week"] = week,
                ["ts"] = Now,
            };
        }

        public static long Now
        {
            get { return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds; }
        }

        /// title_screen.gd's _ago(), word for word.
        public static string Ago(long ts)
        {
            if (ts <= 0L) return "a while ago";
            long d = Now - ts;
            if (d < 3600L) return string.Format("{0} min ago", Math.Max(d / 60L, 1L));
            if (d < 86400L) return string.Format("{0} h ago", d / 3600L);
            return string.Format("{0} days ago", d / 86400L);
        }

        static string Str(JObject o, string key, string fallback)
        {
            if (o == null) return fallback;
            JToken t = o[key];
            return t == null || t.Type == JTokenType.Null ? fallback : t.ToString();
        }

        static int Int(JObject o, string key, int fallback)
        {
            if (o == null) return fallback;
            JToken t = o[key];
            if (t == null || t.Type == JTokenType.Null) return fallback;
            int v;
            return int.TryParse(t.ToString(), out v) ? v : fallback;
        }

        static long Long(JObject o, string key, long fallback)
        {
            if (o == null) return fallback;
            JToken t = o[key];
            if (t == null || t.Type == JTokenType.Null) return fallback;
            long v;
            return long.TryParse(t.ToString(), out v) ? v : fallback;
        }
    }
}
