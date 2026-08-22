using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Runway.Game
{
    /// <summary>
    /// WHAT THE DRAFT HANDS BACK — founder_draft_screen.gd's `done.emit({...})`, given
    /// a type. It travels through IRunDriver.ApplyDraft(object), which is deliberately
    /// untyped so the App layer never sees a Core type or a screen's model.
    /// </summary>
    public sealed class DraftResult
    {
        /// the raw archetypes.json entry: id, name, stats, traits, start_cash_bonus, perk
        public JObject Archetype;
        /// the raw fundings entry: id, name, cash, equity_cost
        public JObject Funding;
        public List<DraftCofounder> Cofounders = new List<DraftCofounder>();
        public string CompanyName = "Untitled Inc";
        public string FounderName = "";
        public string CompanyIdea = "";
        public string BizWhat = "Software";
        public string BizWho = "Consumer";
        public List<string> Items = new List<string>();
        /// the YC-canon trap ids the founding earned — they become flags
        public List<string> Traps = new List<string>();
    }

    /// One line of the cap table as the draft leaves it.
    public sealed class DraftCofounder
    {
        public string Name = "";
        public string Role = "Tech";
        public string Commitment = "Full-time";
        public double Equity = 25.0;
        public bool Vesting = true;
    }
}
