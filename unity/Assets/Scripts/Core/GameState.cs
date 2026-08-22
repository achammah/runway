using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace Runway.Core
{
    /// <summary>
    /// Core reads no files itself. The host hands it a reader — the dotnet test
    /// runner reads from disk, Unity reads from StreamingAssets — and Core only
    /// ever asks for a logical name ("items.json").
    /// </summary>
    public static class CoreFiles
    {
        public static Func<string, string> Reader;

        public static string Read(string logicalName)
        {
            if (Reader == null)
            {
                return string.Empty;
            }
            return Reader(logicalName) ?? string.Empty;
        }
    }

    // ── the loose GDScript dictionaries, given names and types ───────────────────

    public sealed class Status
    {
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("weeks_left")] public int WeeksLeft = 1;
    }

    public sealed class Clock
    {
        [JsonProperty("weeks_left")] public int WeeksLeft = 1;
        [JsonProperty("consequence")] public string Consequence = "";
    }

    public sealed class Commitment
    {
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("cash_wk")] public int CashWk;
        [JsonProperty("weeks_left")] public int WeeksLeft = 1;
    }

    /// <summary>A hire mid-onboarding: paid from day one, productive after two weeks.</summary>
    public sealed class PipelineHire
    {
        [JsonProperty("name")] public string Name = "hire";
        [JsonProperty("role")] public string Role = "engineer";
        [JsonProperty("salary")] public int Salary = 1200;
        [JsonProperty("weeks_in")] public int WeeksIn;
        [JsonProperty("quirk")] public string Quirk = "";
    }

    public sealed class Employee
    {
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("role")] public string Role = "";
        [JsonProperty("salary")] public int Salary;
        [JsonProperty("burnout")] public int Burnout;
        [JsonProperty("quirk")] public string Quirk = "";
    }

    public sealed class Cofounder
    {
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("role")] public string Role = "";
        [JsonProperty("commitment")] public string Commitment = "";
        [JsonProperty("equity")] public double Equity;
        [JsonProperty("vesting")] public string Vesting = "";
        // Null until the first round: apply_round falls back to `equity` exactly once.
        [JsonProperty("equity_diluted")] public double? EquityDiluted;
    }

    public sealed class Rival
    {
        [JsonProperty("name")] public string Name = "?";
        [JsonProperty("what")] public string What = "";
        [JsonProperty("strength")] public double Strength = 20.0;
        [JsonProperty("tactics")] public List<string> Tactics = new List<string>();
        [JsonProperty("weeks_since_move")] public int WeeksSinceMove;
        [JsonProperty("secret")] public string Secret = "";
    }

    public sealed class Investor
    {
        [JsonProperty("name")] public string Name = "?";
        [JsonProperty("archetype")] public string Archetype = "";
        [JsonProperty("coords")] public List<double> Coords = new List<double> { 0.0, 0.0 };
        [JsonProperty("thesis")] public string Thesis = "";
        [JsonProperty("trait")] public string Trait = "";
        [JsonProperty("bond")] public string Bond = "";
        [JsonProperty("flaw")] public string Flaw = "";
        [JsonProperty("secret")] public string Secret = "";
        [JsonProperty("tactics")] public List<string> Tactics = new List<string>();
    }

    /// <summary>What we sell. price 0 = NOT ON SALE, and an unpriced product earns nothing.</summary>
    public sealed class Offer
    {
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("unit")] public string Unit = "";
        [JsonProperty("fair_price")] public double FairPrice = 1.0;
        [JsonProperty("elasticity")] public double Elasticity = 2.0;
        [JsonProperty("unit_cost")] public double UnitCost;
        [JsonProperty("price")] public double Price;
        [JsonProperty("weight")] public double Weight = 1.0;

        public Offer Duplicate()
        {
            return (Offer)MemberwiseClone();
        }
    }

    public sealed class MetricSnapshot
    {
        [JsonProperty("wk")] public int Wk;
        [JsonProperty("cash")] public int Cash;
        [JsonProperty("customers")] public int Customers;
        [JsonProperty("revenue")] public int Revenue;
        [JsonProperty("burn")] public int Burn;
        [JsonProperty("morale")] public int Morale;
        [JsonProperty("debt")] public int Debt;
        [JsonProperty("hype")] public int Hype;
    }

    public sealed class HistoryEntry
    {
        [JsonProperty("week")] public int Week;
        [JsonProperty("entry")] public string Entry = "";
    }

    /// <summary>One week of the DM's memory: what was said, how it went, what it cost.</summary>
    public sealed class RunHistoryEntry
    {
        [JsonProperty("wk")] public int Wk;
        [JsonProperty("said")] public string Said = "";
        [JsonProperty("verdict")] public string Verdict = "";
        [JsonProperty("roll")] public int Roll;
        [JsonProperty("fx")] public string Fx = "";
    }

    public sealed class Timebomb
    {
        [JsonProperty("weeks_left")] public int WeeksLeft;
        [JsonProperty("event")] public string Event = "";
    }

    public sealed class ArcBeat
    {
        [JsonProperty("era")] public string Era = "";
        [JsonProperty("directive")] public string Directive = "";
    }

    public sealed class Arc
    {
        [JsonProperty("kind")] public string Kind = "arc";
        [JsonProperty("actors")] public List<string> Actors = new List<string>();
        [JsonProperty("beats")] public List<ArcBeat> Beats = new List<ArcBeat>();
    }

    public sealed class FounderArchetype
    {
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("keys")] public List<string> Keys = new List<string>();
        [JsonProperty("line")] public string Line = "";
    }

    /// <summary>The ledger's four weekly levers. Every dollar leaves cash and does something.</summary>
    public sealed class Budgets
    {
        [JsonProperty("marketing")] public int Marketing;
        [JsonProperty("sales")] public int Sales;
        [JsonProperty("care")] public int Care;
        [JsonProperty("rnd")] public int Rnd;

        public int Sum()
        {
            return Marketing + Sales + Care + Rnd;
        }
    }

    /// <summary>The founder's working assumptions — wrong on purpose, corrected by playing.</summary>
    public sealed class Beliefs
    {
        [JsonProperty("tam")] public double Tam;
        [JsonProperty("lifetime_wk")] public double LifetimeWk;

        public double this[string key]
        {
            get { return key == "tam" ? Tam : LifetimeWk; }
            set
            {
                if (key == "tam") { Tam = value; } else { LifetimeWk = value; }
            }
        }
    }

    // ── items.json, read once for its trait_mods ─────────────────────────────────

    internal sealed class ItemDef
    {
        [JsonProperty("id")] public string Id = "";
        [JsonProperty("trait_mods")] public Dictionary<string, int> TraitMods = new Dictionary<string, int>();
    }

    internal sealed class ItemsFile
    {
        [JsonProperty("items")] public List<ItemDef> Items = new List<ItemDef>();
    }

    /// <summary>
    /// The whole run state. Meters per PRD 3.3. Era ladder per Dossier 2:
    /// garage to coworking to office to floor to hq, each with its own rent and staff cap.
    /// </summary>
    public sealed class GameState
    {
        [JsonProperty("week")] public int Week = 1;
        [JsonProperty("era")] public string Era = "garage";
        [JsonProperty("archetype_id")] public string ArchetypeId = "";
        [JsonProperty("archetype_name")] public string ArchetypeName = "";

        [JsonProperty("competences")] public Dictionary<string, int> Competences = new Dictionary<string, int>
        {
            { "build", 3 }, { "sell", 3 }, { "raise", 3 }, { "recruit", 3 }, { "grit", 3 }
        };

        /// <summary>
        /// THE SIX HIDDEN TRAITS. Competences are what the founder DOES and get
        /// rolled; these are what the founder IS and are never rolled — they bend
        /// the dice and the terms from behind. Authored per archetype in
        /// data/archetypes.json, bent by what is in the bag, read by
        /// SimEngine.RollContext and GenerateOffers.
        /// </summary>
        [JsonProperty("traits")] public Dictionary<string, int> Traits = new Dictionary<string, int>
        {
            { "charisma", 3 }, { "luck", 3 }, { "network", 3 },
            { "focus", 3 }, { "credibility", 3 }, { "stamina", 3 }
        };

        [JsonProperty("structure_id")] public string StructureId = "solo";
        [JsonProperty("company_name")] public string CompanyName = "Untitled Inc";
        [JsonProperty("company_idea")] public string CompanyIdea = "";
        [JsonProperty("biz_what")] public string BizWhat = "Software";   // Software | Hardware | Marketplace | Service
        [JsonProperty("biz_who")] public string BizWho = "Consumer";     // Enterprise | SMB | Consumer
        [JsonProperty("funding_id")] public string FundingId = "bootstrap";
        [JsonProperty("pivots")] public int Pivots;
        [JsonProperty("last_outcome")] public Dictionary<string, object> LastOutcome = new Dictionary<string, object>();
        [JsonProperty("ceremony_payout")] public int CeremonyPayout;
        [JsonProperty("run_history")] public List<RunHistoryEntry> RunHistory = new List<RunHistoryEntry>();

        // ── SimEngine state ──────────────────────────────────────────────────────
        [JsonProperty("sim_seed")] public long SimSeed;
        /// <summary>The world constants. null mirrors GDScript's empty {}.</summary>
        [JsonProperty("theta")] public Theta Theta;
        [JsonProperty("statuses")] public List<Status> Statuses = new List<Status>();
        [JsonProperty("clocks")] public List<Clock> Clocks = new List<Clock>();
        [JsonProperty("commitments")] public List<Commitment> Commitments = new List<Commitment>();
        [JsonProperty("pipeline")] public List<PipelineHire> Pipeline = new List<PipelineHire>();
        [JsonProperty("price_mult")] public double PriceMult = 1.0;
        [JsonProperty("marketing_budget")] public int MarketingBudget;
        [JsonProperty("budgets")] public Budgets Budgets = new Budgets();
        /// <summary>null mirrors GDScript's empty {} — the first tick seeds it.</summary>
        [JsonProperty("beliefs")] public Beliefs Beliefs;
        [JsonProperty("offers")] public List<Offer> Offers = new List<Offer>();
        [JsonProperty("analytics_level")] public int AnalyticsLevel;
        [JsonProperty("tech_debt")] public double TechDebt = 10.0;
        [JsonProperty("fatigue")] public double Fatigue = 20.0;
        [JsonProperty("exhaustion")] public int Exhaustion;
        [JsonProperty("loan_principal")] public int LoanPrincipal;
        [JsonProperty("market_trend")] public double MarketTrend = 1.0;
        [JsonProperty("last_growth")] public double LastGrowth;
        [JsonProperty("rivals")] public List<Rival> Rivals = new List<Rival>();
        [JsonProperty("investors")] public List<Investor> Investors = new List<Investor>();
        [JsonProperty("xp")] public int Xp;
        [JsonProperty("level")] public int Level = 1;
        [JsonProperty("traits_tally")] public Dictionary<string, int> TraitsTally = new Dictionary<string, int>();
        [JsonProperty("xp_spent")] public int XpSpent;

        [JsonProperty("story_so_far")] public string StorySoFar = "";
        [JsonProperty("metric_history")] public List<MetricSnapshot> MetricHistory = new List<MetricSnapshot>();
        [JsonProperty("played_events")] public List<string> PlayedEvents = new List<string>();
        [JsonProperty("weeks_in_red")] public int WeeksInRed;
        [JsonProperty("history")] public List<HistoryEntry> History = new List<HistoryEntry>();
        [JsonProperty("founder_name")] public string FounderName = "";
        [JsonProperty("cofounders")] public List<Cofounder> Cofounders = new List<Cofounder>();
        [JsonProperty("employees")] public List<Employee> Employees = new List<Employee>();
        [JsonProperty("cash")] public int Cash;
        [JsonProperty("product")] public int Product;
        [JsonProperty("traction")] public int Traction;
        [JsonProperty("morale")] public int Morale = 60;
        [JsonProperty("hype")] public int Hype;
        [JsonProperty("founder_pct")] public double FounderPct = 100.0;
        [JsonProperty("board_seats_founder")] public int BoardSeatsFounder = 2;
        [JsonProperty("board_seats_investor")] public int BoardSeatsInvestor;
        [JsonProperty("rounds_raised")] public List<string> RoundsRaised = new List<string>();
        [JsonProperty("missed_payrolls")] public int MissedPayrolls;
        [JsonProperty("exit_value")] public int ExitValue;
        [JsonProperty("items")] public List<string> Items = new List<string>();
        [JsonProperty("flags")] public List<string> Flags = new List<string>();
        [JsonProperty("timebombs")] public List<Timebomb> Timebombs = new List<Timebomb>();
        [JsonProperty("future_weights")] public List<string> FutureWeights = new List<string>();
        [JsonProperty("arcs")] public List<Arc> Arcs = new List<Arc>();
        [JsonProperty("dead")] public bool Dead;
        [JsonProperty("death_cause")] public string DeathCause = "";

        /// <summary>Godot's Object metadata, which the engine uses for prev_revenue / unit_econ / market_line.</summary>
        [JsonProperty("meta")] public Dictionary<string, object> Meta = new Dictionary<string, object>();

        public const int RAMEN_PER_WEEK = 500;   // founder personal burn, Dossier 10

        public static readonly List<string> ERAS = new List<string> { "garage", "coworking", "office", "floor", "hq" };

        public static readonly Dictionary<string, string> ERA_NAMES = new Dictionary<string, string>
        {
            { "garage", "The Garage" },
            { "coworking", "Desk 47, WorkNest" },
            { "office", "The First Office" },
            { "floor", "The Startup Floor" },
            { "hq", "Headquarters" },
        };

        public static readonly Dictionary<string, int> ERA_RENT = new Dictionary<string, int>
        {
            { "garage", 150 }, { "coworking", 600 }, { "office", 3000 }, { "floor", 12000 }, { "hq", 45000 }
        };

        /// <summary>What one customer-week is worth, by era. Revenue only flows once something shipped.</summary>
        public static readonly Dictionary<string, int> ERA_REV_PER_CUSTOMER = new Dictionary<string, int>
        {
            { "garage", 4 }, { "coworking", 12 }, { "office", 40 }, { "floor", 100 }, { "hq", 310 }
        };

        public static readonly Dictionary<string, int> ERA_STAFF_CAP = new Dictionary<string, int>
        {
            { "garage", 2 }, { "coworking", 4 }, { "office", 9 }, { "floor", 20 }, { "hq", 40 }
        };

        public static readonly Dictionary<string, int> ERA_VALUATION_BASE = new Dictionary<string, int>
        {
            { "garage", 50000 }, { "coworking", 400000 }, { "office", 2000000 },
            { "floor", 12000000 }, { "hq", 60000000 }
        };

        /// <summary>
        /// Founder archetypes matched on the trait tally with the
        /// (-score, -coverage, name) tie-break that stops one spammed trait from
        /// handing a broad archetype the win.
        /// </summary>
        public static readonly List<FounderArchetype> FOUNDER_ARCHETYPES = new List<FounderArchetype>
        {
            new FounderArchetype { Name = "The Visionary",
                Keys = new List<string> { "long_term", "intuition_driven", "risk_taker", "independent" },
                Line = "You saw a future. Whether anyone else lived there was always a detail." },
            new FounderArchetype { Name = "The Operator",
                Keys = new List<string> { "long_term", "data_driven", "risk_averse", "quality_focused", "delegator" },
                Line = "The spreadsheet loved you back. That is rarer than it sounds." },
            new FounderArchetype { Name = "The Fundraiser",
                Keys = new List<string> { "short_term", "speed_focused", "collaborative", "diplomatic", "risk_taker" },
                Line = "You could sell a bridge to the river. The company was sometimes the bridge." },
            new FounderArchetype { Name = "The Product Builder",
                Keys = new List<string> { "long_term", "quality_focused", "hands_on", "collaborative" },
                Line = "You built the thing. Then rebuilt it. The market was an afterthought you got to eventually." },
            new FounderArchetype { Name = "The Firefighter",
                Keys = new List<string> { "short_term", "speed_focused", "hands_on", "risk_taker", "independent" },
                Line = "Every week was an emergency and you were magnificent in exactly that weather." },
            new FounderArchetype { Name = "The People-First Leader",
                Keys = new List<string> { "collaborative", "diplomatic", "risk_averse", "long_term" },
                Line = "The team would follow you anywhere. Occasionally somewhere profitable." },
        };

        public FounderArchetype FounderArchetypeMatch()
        {
            FounderArchetype best = null;
            double bestScore = -1.0;
            double bestCov = -1.0;
            foreach (FounderArchetype a in FOUNDER_ARCHETYPES)
            {
                int score = 0;
                int matched = 0;
                foreach (string k in a.Keys)
                {
                    int c;
                    TraitsTally.TryGetValue(k, out c);
                    if (c > 0)
                    {
                        matched += 1;
                        score += c;
                    }
                }
                double cov = matched / Gd.Maxf(a.Keys.Count, 1.0);
                if (score > bestScore || (score == bestScore && cov > bestCov))
                {
                    best = a;
                    bestScore = score;
                    bestCov = cov;
                }
            }
            return bestScore > 0.0 ? best : FOUNDER_ARCHETYPES[4];
        }

        public int EraIndex()
        {
            int i = ERAS.IndexOf(Era);
            return Gd.Maxi(0, i);
        }

        public string EraDisplayName()
        {
            string v;
            return ERA_NAMES.TryGetValue(Era, out v) ? v : Gd.Capitalize(Era);
        }

        public int StaffCap()
        {
            int v;
            return ERA_STAFF_CAP.TryGetValue(Era, out v) ? v : 2;
        }

        public bool CanHire()
        {
            return Employees.Count < StaffCap();
        }

        public int RevenuePerWeek()
        {
            if (!(HasFlag("launched") || HasFlag("first_revenue")))
            {
                return 0;
            }
            int per;
            double rate = ERA_REV_PER_CUSTOMER.TryGetValue(Era, out per) ? per : 4;
            if (HasFlag("premium_pricing"))
            {
                rate *= 1.25;
            }
            else if (HasFlag("cheap_pricing"))
            {
                rate *= 0.8;
            }
            return Gd.ToInt(Traction * rate);
        }

        /// <summary>NET weekly cash movement: rent + ramen + payroll - revenue. May go negative — that is the point.</summary>
        public int BurnPerWeek()
        {
            int salaries = 0;
            foreach (Employee e in Employees)
            {
                salaries += e.Salary;
            }
            int rent;
            if (!ERA_RENT.TryGetValue(Era, out rent))
            {
                rent = 150;
            }
            return rent + RAMEN_PER_WEEK + salaries - RevenuePerWeek();
        }

        public bool HasItem(string id)
        {
            return Items.Contains(id);
        }

        // ── the six traits ───────────────────────────────────────────────────────
        public static readonly List<string> TRAIT_NAMES = new List<string>
        {
            "charisma", "luck", "network", "focus", "credibility", "stamina"
        };

        private static Dictionary<string, Dictionary<string, int>> _itemTraits =
            new Dictionary<string, Dictionary<string, int>>();
        private static bool _itemTraitsRead;

        /// <summary>
        /// Every item's trait modifiers, read once from the same JSON the shelf
        /// reads. The engine has to be able to ask "what does this bag do to luck"
        /// without a ContentDb in the room.
        /// </summary>
        public static Dictionary<string, Dictionary<string, int>> ItemTraitTable()
        {
            if (_itemTraitsRead)
            {
                return _itemTraits;
            }
            _itemTraitsRead = true;
            string txt = CoreFiles.Read("items.json");
            if (string.IsNullOrEmpty(txt))
            {
                return _itemTraits;
            }
            ItemsFile parsed = JsonConvert.DeserializeObject<ItemsFile>(txt);
            if (parsed == null || parsed.Items == null)
            {
                return _itemTraits;
            }
            foreach (ItemDef it in parsed.Items)
            {
                if (it.TraitMods != null && it.TraitMods.Count > 0)
                {
                    _itemTraits[it.Id ?? ""] = it.TraitMods;
                }
            }
            return _itemTraits;
        }

        /// <summary>Drops the cached items.json read — a host swapping data sets calls this.</summary>
        public static void ResetItemTraitTable()
        {
            _itemTraits = new Dictionary<string, Dictionary<string, int>>();
            _itemTraitsRead = false;
        }

        /// <summary>What the bag alone does to a trait — the number the loadout line prints.</summary>
        public int ItemTraitDelta(string name)
        {
            Dictionary<string, Dictionary<string, int>> tbl = ItemTraitTable();
            int d = 0;
            foreach (string id in Items)
            {
                Dictionary<string, int> mods;
                if (tbl.TryGetValue(id ?? "", out mods) && mods != null)
                {
                    int v;
                    if (mods.TryGetValue(name, out v))
                    {
                        d += v;
                    }
                }
            }
            return d;
        }

        /// <summary>
        /// THE ONE READING anything is allowed to use: archetype base + what you
        /// packed, clamped to the 1..5 the whole game speaks.
        /// </summary>
        public int TraitLevel(string name)
        {
            int baseV;
            if (!Traits.TryGetValue(name, out baseV))
            {
                baseV = 3;
            }
            return Gd.Clampi(baseV + ItemTraitDelta(name), 1, 5);
        }

        public Dictionary<string, int> TraitSheet()
        {
            var outp = new Dictionary<string, int>();
            foreach (string t in TRAIT_NAMES)
            {
                outp[t] = TraitLevel(t);
            }
            return outp;
        }

        public int Competence(string name)
        {
            int v;
            return Competences.TryGetValue(name, out v) ? v : 3;
        }

        public bool HasFlag(string f)
        {
            return Flags.Contains(f);
        }

        public void SetFlag(string f)
        {
            if (!string.IsNullOrEmpty(f) && !Flags.Contains(f))
            {
                Flags.Add(f);
            }
        }

        public void ClampiMeters()
        {
            Product = Gd.Clampi(Product, 0, 100);
            Morale = Gd.Clampi(Morale, 0, 100);
            Hype = Gd.Clampi(Hype, 0, 100);
            Traction = Gd.Maxi(Traction, 0);
            FounderPct = Gd.Clampf(FounderPct, 0.0, 100.0);
        }

        // ── Godot Object metadata, ported ────────────────────────────────────────
        public object GetMeta(string key, object dflt = null)
        {
            object v;
            return Meta.TryGetValue(key, out v) ? v : dflt;
        }

        public double GetMetaF(string key, double dflt = 0.0)
        {
            object v;
            if (Meta.TryGetValue(key, out v) && v != null)
            {
                // ints written through SetMeta must read back (loyalty was
                // permanently 70 because `is double` dropped every int)
                try { return Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture); }
                catch (Exception) { return dflt; }
            }
            return dflt;
        }

        public void SetMeta(string key, object value)
        {
            Meta[key] = value;
        }

        /// <summary>Everything the player does goes here; the LLM engine reads it back.</summary>
        public void LogAction(string entry)
        {
            History.Add(new HistoryEntry { Week = Week, Entry = entry });
            while (History.Count > 40)
            {
                History.RemoveAt(0);
            }
        }

        public List<string> RecentActions(int n = 14)
        {
            var outp = new List<string>();
            int from = Gd.Maxi(0, History.Count - n);
            for (int i = from; i < History.Count; i++)
            {
                outp.Add("wk" + History[i].Week.ToString(CultureInfo.InvariantCulture) + ": " + History[i].Entry);
            }
            return outp;
        }

        // ── Cap table ────────────────────────────────────────────────────────────
        /// <summary>A new investor taking X% dilutes EVERYONE pro-rata.</summary>
        public void DiluteAll(double investorPct)
        {
            double keep = 1.0 - Gd.Clampf(investorPct, 0.0, 45.0) / 100.0;
            FounderPct *= keep;
            foreach (Cofounder c in Cofounders)
            {
                c.Equity = c.Equity * keep;
            }
            if (FounderPct < 50.0)
            {
                SetFlag("lost_majority");
            }
            if (FounderPct < 25.0)
            {
                SetFlag("employee_of_own_company");
            }
        }

        // ── Valuation / payout ───────────────────────────────────────────────────
        /// <summary>Era milestones dominate; meters modulate. Monotonic in era, product, traction, hype.</summary>
        public int Valuation()
        {
            int b;
            double baseV = ERA_VALUATION_BASE.TryGetValue(Era, out b) ? b : 50000;
            double mult = 0.5 + Product / 100.0 + Traction / 50.0 + Hype / 200.0;
            return Gd.ToInt(baseV * mult);
        }

        public int PayoutToday()
        {
            if (ExitValue > 0)
            {
                return Gd.ToInt(ExitValue * FounderPct / 100.0);
            }
            return Gd.ToInt(Valuation() * FounderPct / 100.0);
        }

        // ── Employees (burnout ladder fine -> frayed -> cooked -> gone) ───────────
        public static string BurnoutState(int b)
        {
            if (b >= 100) { return "gone"; }
            if (b >= 70) { return "cooked"; }
            if (b >= 40) { return "frayed"; }
            return "fine";
        }

        /// <summary>Weekly staff upkeep. Returns the log lines the screens print.</summary>
        public List<string> WeeklyStaffTick()
        {
            var lines = new List<string>();
            if (Cash > 0 && WeeksInRed == 0)
            {
                int target = BurnPerWeek() < 0 ? 70 : 60;
                int lift = BurnPerWeek() < 0 ? 4 : 2;
                if (Morale < target)
                {
                    Morale = Gd.Mini(Morale + lift, target);
                }
            }
            int rate = 2;
            if (Morale < 60) { rate = 5; }
            if (Morale < 40) { rate = 9; }
            if (Cash < 0) { rate += 4; }
            foreach (Employee e in new List<Employee>(Employees))
            {
                string before = BurnoutState(e.Burnout);
                e.Burnout = Gd.Clampi(e.Burnout + rate - (Morale >= 75 ? 3 : 0), 0, 100);
                string after = BurnoutState(e.Burnout);
                if (after != before && after == "cooked")
                {
                    SetFlag("staff_cooked");
                    lines.Add(e.Name + " is running on fumes.");
                }
                if (after == "gone")
                {
                    Employees.Remove(e);
                    Morale = Gd.Clampi(Morale - 8, 0, 100);
                    SetFlag("staff_quit");
                    lines.Add(e.Name + " quit. The chair is still warm.");
                }
            }
            return lines;
        }

        /// <summary>Payroll miss: call when cash went negative on payday. Two misses = demotion risk.</summary>
        public void NoteMissedPayroll()
        {
            MissedPayrolls += 1;
            SetFlag("missed_payroll");
            if (MissedPayrolls >= 2)
            {
                SetFlag("payroll_crisis");
            }
        }

        // ── Era ladder ───────────────────────────────────────────────────────────
        public sealed class EraMove
        {
            public bool Changed;
            public string From = "";
            public string To = "";
            public string Reason = "";
        }

        public EraMove AdvanceEraIfReady()
        {
            string from = Era;
            string to = "";
            string reason = "";
            switch (Era)
            {
                case "garage":
                    if (Product >= 60 && (Traction >= 5 || HasFlag("first_revenue")))
                    {
                        to = "coworking";
                        reason = "something works and someone noticed";
                    }
                    break;
                case "coworking":
                    if (HasFlag("launched") && Traction >= 25)
                    {
                        to = "office";
                        reason = "launched, and the numbers kept moving";
                    }
                    break;
                case "office":
                    // no moving into rent you can't pay — the deadly jumps need a cushion
                    if (HasFlag("pmf") && HasFlag("seed_raised") && Cash >= 6 * ERA_RENT["floor"])
                    {
                        to = "floor";
                        reason = "product-market fit with money behind it";
                    }
                    break;
                case "floor":
                    if (HasFlag("series_a") && Traction >= 100 && Cash >= 6 * ERA_RENT["hq"])
                    {
                        to = "hq";
                        reason = "Series A and a hundred believers";
                    }
                    break;
            }
            if (to == "")
            {
                return new EraMove { Changed = false };
            }
            Era = to;
            Morale = Gd.Clampi(Morale + 10, 0, 100);
            SetFlag("moved_up_" + to);
            LogAction(string.Format(CultureInfo.InvariantCulture, "MOVED UP: {0} -> {1} ({2})", from, to, reason));
            return new EraMove { Changed = true, From = from, To = to, Reason = reason };
        }

        /// <summary>Demotion (missed payroll x2 or a down round). Moving down hurts.</summary>
        public EraMove Demote(string reason)
        {
            int idx = EraIndex();
            if (idx <= 0)
            {
                return new EraMove { Changed = false };
            }
            string from = Era;
            Era = ERAS[idx - 1];
            Morale = Gd.Clampi(Morale - 25, 0, 100);
            MissedPayrolls = 0;
            Flags.Remove("payroll_crisis");
            SetFlag("moved_down_" + Era);
            LogAction(string.Format(CultureInfo.InvariantCulture, "MOVED DOWN: {0} -> {1} ({2})", from, Era, reason));
            return new EraMove { Changed = true, From = from, To = Era, Reason = reason };
        }

        /// <summary>The run director's beat directives that apply to the CURRENT era.</summary>
        public List<string> ActiveArcDirectives()
        {
            var outp = new List<string>();
            foreach (Arc a in Arcs)
            {
                foreach (ArcBeat b in a.Beats)
                {
                    if (b != null && b.Era == Era)
                    {
                        outp.Add(string.Format(CultureInfo.InvariantCulture, "{0} [{1}]: {2}",
                            (a.Kind ?? "arc").ToUpperInvariant(), string.Join(", ", a.Actors), b.Directive));
                    }
                }
            }
            return outp;
        }

        /// <summary>
        /// The DM's memory: every week verbatim for the recent past, compressed
        /// further back — decisions, verdicts, rolls and consequences compound.
        /// </summary>
        public List<object> HistoryDigest()
        {
            var outp = new List<object>();
            int n = RunHistory.Count;
            for (int i = 0; i < n; i++)
            {
                RunHistoryEntry h = RunHistory[i];
                if (i >= n - 12)
                {
                    outp.Add(h);
                }
                else
                {
                    outp.Add(new Dictionary<string, object>
                    {
                        { "wk", h.Wk },
                        { "said", Gd.Left(h.Said, 40) },
                        { "verdict", h.Verdict },
                    });
                }
            }
            return outp;
        }

        /// <summary>Compact digest for the LLM layer (stable field order).</summary>
        public Dictionary<string, object> ToDigest()
        {
            var staff = new List<string>();
            foreach (Employee e in Employees)
            {
                staff.Add(string.Format(CultureInfo.InvariantCulture, "{0} ({1}, burnout: {2})",
                    e.Name, e.Role, BurnoutState(e.Burnout)));
            }
            return new Dictionary<string, object>
            {
                { "week", Week },
                { "era", Era },
                { "era_name", EraDisplayName() },
                { "company_name", CompanyName },
                { "founder_name", FounderName },
                { "company_does", CompanyIdea },
                { "business_model", BizWhat + " for " + BizWho },
                { "funding_path", FundingId == "bootstrap" ? "bootstrapped" : "outside money taken (" + FundingId + ")" },
                { "rounds_raised", new List<string>(RoundsRaised) },
                { "employees", 1 + Cofounders.Count + Employees.Count },
                { "staff", staff },
                { "staff_cap", StaffCap() },
                { "customers", Traction },
                { "product_version", "v0." + Gd.Maxi(1, Product / 10).ToString(CultureInfo.InvariantCulture) },
                { "pivots_so_far", Pivots },
                { "weeks_in_the_red", WeeksInRed },
                { "recent_actions", RecentActions() },
                { "founder_archetype", ArchetypeName },
                { "competences", Competences },
                { "traits", TraitSheet() },
                { "cofounders", Cofounders },
                { "cash", Cash },
                { "weekly_burn", BurnPerWeek() },
                { "weekly_revenue", RevenuePerWeek() },
                { "valuation", Valuation() },
                { "board", string.Format(CultureInfo.InvariantCulture, "{0} founder seats, {1} investor seats",
                    BoardSeatsFounder, BoardSeatsInvestor) },
                { "product", Product },
                { "traction", Traction },
                { "morale", Morale },
                { "hype", Hype },
                { "founder_pct", FounderPct },
                { "items", new List<string>(Items) },
                { "flags", new List<string>(Flags) },
            };
        }
    }
}
