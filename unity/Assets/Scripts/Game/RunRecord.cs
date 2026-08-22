using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Runway.Game
{
    /// <summary>
    /// FULL RUN LOGGING — run_record.gd, ported. Every event, every choice, every
    /// effect line, in the order it happened. It drives the last page and it is the
    /// only thing in a save that remembers WHY the numbers are what they are.
    ///
    /// Newtonsoft-serializable, because it rides in the same save file the state does.
    /// </summary>
    public sealed class RunRecord
    {
        [JsonProperty("seed_value")] public long SeedValue;
        [JsonProperty("entries")] public List<RecordEntry> Entries = new List<RecordEntry>();

        public void LogEvent(int week, JObject ev, string choiceLabel, IList<string> effectsLog)
        {
            var e = new RecordEntry
            {
                Week = week,
                Kind = "event",
                EventId = ContentDb.Str(ev, "id", "generated"),
                Tier = ContentDb.Str(ev, "tier", "authored"),
                Title = ContentDb.Str(ev, "title", "?"),
                Choice = choiceLabel ?? "",
            };
            if (effectsLog != null) e.Effects = new List<string>(effectsLog);
            Entries.Add(e);
        }

        public void LogDeath(int week, string cause)
        {
            Entries.Add(new RecordEntry { Week = week, Kind = "death", Title = cause ?? "" });
        }

        /// The causal chain for the last page: walk it forward, read it back.
        public List<string> CausalLines()
        {
            var lines = new List<string>();
            for (int i = 0; i < Entries.Count; i++)
            {
                RecordEntry e = Entries[i];
                switch (e.Kind)
                {
                    case "event":
                        string tag = e.Tier == "generated" ? " *" : "";
                        lines.Add(string.Format("Week {0} — {1} → \"{2}\"{3}",
                            e.Week, e.Title, e.Choice, tag));
                        break;
                    case "death":
                        lines.Add(string.Format("Week {0} — DIED: {1}", e.Week, e.Title));
                        break;
                    default:
                        lines.Add(string.Format("Week {0} — {1}", e.Week, e.Title));
                        break;
                }
            }
            return lines;
        }
    }

    public sealed class RecordEntry
    {
        [JsonProperty("week")] public int Week;
        [JsonProperty("kind")] public string Kind = "event";
        [JsonProperty("event_id")] public string EventId = "";
        [JsonProperty("tier")] public string Tier = "authored";
        [JsonProperty("title")] public string Title = "";
        [JsonProperty("choice")] public string Choice = "";
        [JsonProperty("effects")] public List<string> Effects = new List<string>();
    }
}
