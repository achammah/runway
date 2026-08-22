using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// THE RUN, ON DISK — save_system.gd's run half, ported onto the slot files
    /// SaveSlots already owns.
    ///
    /// The Godot original hand-lists every field of GameState into a dictionary and
    /// re-reads them one by one on load, which is how a new field silently stops
    /// being saved. Runway.Core is Newtonsoft-serializable end to end — every field
    /// carries its own [JsonProperty] — so the whole state goes down as one object
    /// and comes back as one object, and a field added to Core is saved the day it
    /// exists.
    ///
    /// The file is the SAME shape SaveSlots.Read() expects: { version, meta, state,
    /// record }, so the title screen's slot table reads a run this lane wrote without
    /// knowing anything about it.
    /// </summary>
    public static class RunSave
    {
        public const int Version = 2;

        public static bool Save(int slot, GameState state, RunRecord record)
        {
            if (state == null) return false;
            try
            {
                var doc = new JObject
                {
                    ["version"] = Version,
                    ["meta"] = SaveSlots.Meta(state.CompanyName, state.FounderName, state.Week),
                    ["state"] = JObject.FromObject(state),
                    ["record"] = record != null ? JObject.FromObject(record) : new JObject(),
                };
                return RunwayPaths.WriteAllText(SaveSlots.Path(slot), doc.ToString(Formatting.None));
            }
            catch (Exception e)
            {
                Debug.LogWarning("RUNWAY! could not save slot " + slot + ": " + e.Message);
                return false;
            }
        }

        /// Loads a slot into `state`/`record`. Returns false — leaving both null — for
        /// a missing OR unreadable file, so the flow falls through to a fresh run
        /// exactly as main.gd does rather than half-restoring a company.
        public static bool Load(int slot, out GameState state, out RunRecord record)
        {
            state = null;
            record = null;
            string txt = RunwayPaths.ReadAllTextOrEmpty(SaveSlots.Path(slot));
            if (txt.Trim().Length == 0) return false;
            try
            {
                JObject doc = JObject.Parse(txt);
                var sd = doc["state"] as JObject;
                if (sd == null) return false;
                state = sd.ToObject<GameState>();
                if (state == null) return false;
                var rd = doc["record"] as JObject;
                record = rd != null ? rd.ToObject<RunRecord>() : null;
                if (record == null) record = new RunRecord();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("RUNWAY! slot " + slot + " will not load (" + e.Message
                                 + ") — starting fresh instead.");
                state = null;
                record = null;
                return false;
            }
        }
    }
}
