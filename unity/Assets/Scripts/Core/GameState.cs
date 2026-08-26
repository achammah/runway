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

    /// <summary>
    /// The week's honest P&L — one record the binder reads whole, written once
    /// in tick section 9 (docs/design/00-spine.md section 2). Every lane is
    /// present every week; an inactive subsystem simply reports 0, so a reader
    /// never has to ask whether a key exists.
    ///
    /// THE IDENTITY, pinned by a twin test on every week of a mixed run:
    ///   burn = cogs + rent + payroll + infra + marketing + sales + care + rnd
    ///        + office + offer_fixed + severance + recruiting + production
    ///        + subcontract + equip_upkeep + carrying
    ///        + recruit_ads + relief + site_rent + incident
    ///   net  = revenue - burn - liabilities_wk - interest - tax
    ///
    /// Burn is OPERATING spend only. Interest and tax sit OUTSIDE it because
    /// that is the real income-statement shape — operating profit, then the
    /// cost of debt, then the state, then what is actually yours.
    /// </summary>
    public sealed class Pnl
    {
        [JsonProperty("revenue")] public int Revenue;
        [JsonProperty("cogs")] public int Cogs;
        [JsonProperty("rent")] public int Rent;
        [JsonProperty("payroll")] public int Payroll;
        [JsonProperty("infra")] public int Infra;
        // the levers; marketing is the sum of the four channel budgets
        [JsonProperty("marketing")] public int Marketing;
        [JsonProperty("sales")] public int Sales;
        [JsonProperty("care")] public int Care;
        [JsonProperty("rnd")] public int Rnd;
        [JsonProperty("office")] public int Office;
        [JsonProperty("offer_fixed")] public int OfferFixed;      // catalog weekly overheads (01)
        [JsonProperty("severance")] public int Severance;         // the firing invoice (02)
        [JsonProperty("recruiting")] public int Recruiting;       // recruiter retainer (02)
        [JsonProperty("production")] public int Production;       // in-house build cost (09)
        [JsonProperty("subcontract")] public int Subcontract;     // contract-mfr premium (09)
        [JsonProperty("equip_upkeep")] public int EquipUpkeep;    // machine upkeep (09)
        [JsonProperty("carrying")] public int Carrying;           // stock carrying cost (09)
        // ── DAG2 W1 — pre-registered at zero (the record's names are fixed
        // here, so a new money flow must exist before its lane can write it).
        // They sit INSIDE burn, like every operating lane above.
        [JsonProperty("recruit_ads")] public int RecruitAds;      // role adverts (ownership/recruitment)
        [JsonProperty("relief")] public int Relief;               // works relief valves (freelance/burst/subcontract)
        [JsonProperty("site_rent")] public int SiteRent;          // per-site rents beside the era's own roof (divisions)
        [JsonProperty("incident")] public int Incident;
        [JsonProperty("liabilities_wk")] public int LiabilitiesWk;
        [JsonProperty("interest")] public int Interest;           // the bank — OUTSIDE burn (06)
        [JsonProperty("tax")] public int Tax;                     // the state — OUTSIDE burn (06)
        [JsonProperty("burn")] public int Burn;
        [JsonProperty("net")] public int Net;
        [JsonProperty("learning")] public double Learning = 1.0;  // meta: a multiplier, not money
    }

    /// <summary>
    /// THE WORKING MONEY RECORD the tick's money section passes to every lane
    /// (docs/design/HOOKS.md). One field per P&L lane; the engine fills its own,
    /// each subsystem writes ONLY the lanes it owns, and the engine then sums
    /// burn and copies the whole thing into the Pnl record above.
    ///
    /// Doubles, not ints: rounding once at the record keeps a lane's cents from
    /// being lost twice.
    /// </summary>
    public sealed class MoneyWork
    {
        public double Revenue;
        public double Cogs;
        public int Rent;
        public int Payroll;
        public int Infra;
        public double Marketing;
        public double Sales;
        public double Care;
        public double Rnd;
        public double Office;
        public double OfferFixed;
        public double Severance;
        public double Recruiting;
        public double Production;
        public double Subcontract;
        public double EquipUpkeep;
        public double Carrying;
        // DAG2 W1 — the three new operating lanes, zero until their W2 lanes fill them
        public double RecruitAds;
        public double Relief;
        public double SiteRent;
        public double Incident;
        public int LiabilitiesWk;
        public double Interest;
        public double Tax;
        public int Burn;
    }

    /// <summary>
    /// One row of the attention registry (docs/design/00-spine.md section 4) —
    /// the single list behind every bang in the game: binder tab marks, the
    /// garage badge, the garage ticker, the threats desk, the pre-roll review.
    /// `Label` is pedagogy in 40 characters or less: the ticker prints it
    /// verbatim, so it must name the problem in the term the player is learning.
    /// </summary>
    public sealed class AttentionItem
    {
        [JsonProperty("desk")] public string Desk = "";
        [JsonProperty("key")] public string Key = "";
        [JsonProperty("severity")] public int Severity = 1;   // 1 note, 2 warn, 3 alarm
        [JsonProperty("label")] public string Label = "";
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
        // 02-labor: skill 3 is exact legacy parity — every pre-wave hire was
        // implicitly average, so an old save behaves identically.
        [JsonProperty("skill")] public int Skill = 3;
    }

    public sealed class Employee
    {
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("role")] public string Role = "";
        [JsonProperty("salary")] public int Salary;
        [JsonProperty("burnout")] public int Burnout;
        [JsonProperty("quirk")] public string Quirk = "";
        // ── 02-labor, all additive: an old save loads at exact legacy parity
        [JsonProperty("skill")] public int Skill = 3;
        [JsonProperty("hired_week")] public int HiredWeek = -1;      // -1 = tenure unknown
        [JsonProperty("wants_raise")] public bool WantsRaise;
        [JsonProperty("asked_week")] public int AskedWeek = -1;
        [JsonProperty("underpaid_since")] public int UnderpaidSince = -1;   // the receipt's clock
        // DAG2 W1: which roof this person works under. "" = the home roof;
        // the divisions lane sets it via reassign_employee. Inert until then.
        [JsonProperty("site")] public string Site = "";
    }

    /// <summary>A seat the company is trying to fill (02-labor).</summary>
    public sealed class OpenRole
    {
        [JsonProperty("role")] public string Role = "engineer";
        [JsonProperty("offered_salary")] public int OfferedSalary = 1200;
        [JsonProperty("opened_week")] public int OpenedWeek;
        [JsonProperty("seats")] public int Seats = 1;      // more than 1 only at hq
    }

    /// <summary>Someone who answered the advert. Skill and ask are ENGINE-drawn;
    /// the LLM only ever dresses the name, quirk and one-liner.</summary>
    public sealed class Applicant
    {
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("role")] public string Role = "engineer";
        [JsonProperty("skill")] public int Skill = 3;          // 1-5
        [JsonProperty("ask")] public int Ask = 1200;           // $/wk
        [JsonProperty("quirk")] public string Quirk = "";
        [JsonProperty("one_liner")] public string OneLiner = "";
        [JsonProperty("applied_week")] public int AppliedWeek;
        [JsonProperty("source")] public string Source = "inbound";   // inbound | referral
    }

    /// <summary>A named prospect in the enterprise pipeline (05).</summary>
    public sealed class Lead
    {
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("flavor")] public string Flavor = "";
        [JsonProperty("seats")] public int Seats = 3;
        [JsonProperty("stage")] public string Stage = "meeting";   // meeting|pilot|procurement|contract
        [JsonProperty("age_weeks")] public int AgeWeeks;
        [JsonProperty("heat")] public int Heat = 50;               // 0-100
    }

    /// <summary>A signed account. Its seats live inside `traction` (05).</summary>
    public sealed class Logo
    {
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("seats")] public int Seats;
        [JsonProperty("since_wk")] public int SinceWk;
        [JsonProperty("renewal_wk")] public int RenewalWk;   // 0 until the floor era
    }

    /// <summary>Running totals the customers desk reads (05).</summary>
    public sealed class PipeStats
    {
        [JsonProperty("signed")] public int Signed;
        [JsonProperty("lost")] public int Lost;
        [JsonProperty("cycle_sum")] public int CycleSum;
        [JsonProperty("seats_signed")] public int SeatsSigned;
        [JsonProperty("spend")] public double Spend;
        [JsonProperty("first_wk")] public int FirstWk;
    }

    /// <summary>
    /// One note on the books (06). A single principal+rate could never hold a
    /// 4%/wk bank note and an 18%/wk shark at once, and amortization needs terms
    /// per note — so debt is a list, and the legacy `loan_principal` migrates
    /// into it as a shark record the first time the finance lane runs.
    /// </summary>
    public sealed class Loan
    {
        [JsonProperty("kind")] public string Kind = "shark";   // shark | bank | venture
        [JsonProperty("principal")] public int Principal;      // original draw, for receipts
        [JsonProperty("balance")] public int Balance;
        [JsonProperty("rate_wk")] public double RateWk;        // frozen at signing
        [JsonProperty("term_wk")] public int TermWk;           // 0 for shark
        [JsonProperty("taken_week")] public int TakenWeek;
        [JsonProperty("pay_wk")] public int PayWk;             // level payment; 0 for shark/venture
        [JsonProperty("missed")] public int Missed;
    }

    /// <summary>One thing the team could chase, on the roadmap board (07).</summary>
    public sealed class Bet
    {
        [JsonProperty("id")] public string Id = "";
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("desc")] public string Desc = "";
        [JsonProperty("kind")] public string Kind = "";        // quality|retention|reach|debt|platform
        [JsonProperty("ambition")] public int Ambition = 1;    // 1-3
        [JsonProperty("cost_rnd_weeks")] public double CostRndWeeks;
        [JsonProperty("progress")] public double Progress;
        [JsonProperty("committed")] public bool Committed;
        [JsonProperty("committed_week")] public int CommittedWeek;
        [JsonProperty("ready")] public bool Ready;
        [JsonProperty("shipped")] public bool Shipped;
        [JsonProperty("shipped_week")] public int ShippedWeek;
        [JsonProperty("band")] public string Band = "";
        [JsonProperty("era")] public string Era = "";
    }

    /// <summary>The plan of record a priced round installs (08).</summary>
    public sealed class BoardState
    {
        [JsonProperty("target_growth_pct")] public double TargetGrowthPct;
        [JsonProperty("target_revenue")] public int TargetRevenue;
        [JsonProperty("base_revenue")] public int BaseRevenue;
        [JsonProperty("review_week")] public int ReviewWeek;
        [JsonProperty("strikes")] public int Strikes;      // 0-3 missed covenants on record
        [JsonProperty("goodwill")] public int Goodwill;    // 0-3 clean quarters on record
    }

    /// <summary>An offer for the whole company, with a hard clock (08).</summary>
    public sealed class MnaOffer
    {
        [JsonProperty("buyer")] public string Buyer = "";
        [JsonProperty("price")] public int Price;
        [JsonProperty("why")] public string Why = "";
        [JsonProperty("premium")] public double Premium;
        [JsonProperty("expires_week")] public int ExpiresWeek;
    }

    /// <summary>A machine on the floor. Capacity and upkeep are denormalized at
    /// purchase so a later catalog rebalance never rewrites an owned asset (09).</summary>
    public sealed class EquipmentItem
    {
        [JsonProperty("id")] public string Id = "";
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("capacity_add")] public double CapacityAdd;
        [JsonProperty("upkeep_wk")] public double UpkeepWk;
        [JsonProperty("bought_week")] public int BoughtWeek;
        // DAG2 W1: which roof the machine stands under. "" = the home roof;
        // the divisions lane sets it via move_machine. Inert until then.
        [JsonProperty("site")] public string Site = "";
    }

    /// <summary>The factory (09). Null on every run that is not Hardware.</summary>
    public sealed class HardwareState
    {
        [JsonProperty("stock")] public int Stock;
        [JsonProperty("capacity_base")] public double CapacityBase = 6.0;   // founder hand-assembly
        [JsonProperty("equipment")] public List<EquipmentItem> Equipment = new List<EquipmentItem>();
        [JsonProperty("production_target")] public int ProductionTarget = -1;   // -1 = AUTO
        [JsonProperty("produced_total")] public int ProducedTotal;   // drives the BUILD learning curve
        [JsonProperty("subcontract_on")] public bool SubcontractOn;
        [JsonProperty("demand_ema")] public double DemandEma;
    }

    // ── DAG2 W1 — the binder rework's durable records (docs/design/DAG2.md
    // §W1, docs/design/DECISIONS.md). The W1 spine plants the FIELDS; the W2
    // lanes fill the LOGIC. JSON names are the Godot save keys, byte-for-byte.

    /// <summary>A roof the company operates under (divisions). Never generated:
    /// born only from a real open_site op; the LLM names, never numbers.</summary>
    public sealed class Site
    {
        [JsonProperty("id")] public string Id = "";
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("rent_wk")] public int RentWk;
        [JsonProperty("wage_mult")] public double WageMult = 1.0;
        [JsonProperty("learning_count")] public int LearningCount;   // per-site learning curve
        [JsonProperty("demand_weight")] public double DemandWeight = 1.0;   // the funnel splits reach by this
        [JsonProperty("opened_wk")] public int OpenedWk;
    }

    /// <summary>One generated org-spend line (the spend book). bucket is one of
    /// the four engine levers — sales | care | rnd | office; engine math is
    /// untouched (a lever = the sum of its lines).</summary>
    public sealed class SpendLine
    {
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("buys")] public string Buys = "";              // one-line "what this buys"
        [JsonProperty("amt")] public int Amt;                        // $/wk
        [JsonProperty("bucket")] public string Bucket = "office";
        [JsonProperty("contract_notice")] public int ContractNotice; // 0 = stoppable instantly; N = notice weeks bill through
        [JsonProperty("division")] public string Division = "";      // "" = shared/HQ; an ink tag, set in arrange mode
    }

    /// <summary>One ESOP grant: {n%, 208-wk vest, 52-wk cliff}. Leavers keep
    /// vested; unvested returns to the pool.</summary>
    public sealed class EsopGrant
    {
        [JsonProperty("emp_id")] public string EmpId = "";
        [JsonProperty("pct")] public double Pct;
        [JsonProperty("vest_start_wk")] public int VestStartWk;
    }

    /// <summary>The option pool. null mirrors GDScript's empty {} — no pool
    /// has been born yet.</summary>
    public sealed class Esop
    {
        [JsonProperty("pool_pct")] public double PoolPct;
        [JsonProperty("granted")] public List<EsopGrant> Granted = new List<EsopGrant>();
    }

    /// <summary>One instrument on the cap table (ownership). Fields that do
    /// not apply to a kind stay at their zero default — a SAFE has no rate, a
    /// note has no pct. `prefs` is the liquidation-preference multiple
    /// (0 = none; a standard priced round writes 1.0); `protective` and
    /// `drag_threshold` are the powers the offer desk reads years later.</summary>
    public sealed class Instrument
    {
        [JsonProperty("kind")] public string Kind = "safe";   // safe | note | priced | bridge
        [JsonProperty("holder")] public string Holder = "";
        [JsonProperty("amount")] public int Amount;
        [JsonProperty("cap")] public int Cap;                 // valuation cap (safe/note)
        [JsonProperty("discount")] public double Discount;    // 0.2 = 20% (safe/note)
        [JsonProperty("rate")] public double Rate;            // weekly interest (note/bridge)
        [JsonProperty("maturity_wk")] public int MaturityWk;  // 0 = none
        [JsonProperty("pct")] public double Pct;              // equity taken (priced)
        [JsonProperty("prefs")] public double Prefs;          // liquidation preference multiple, 0 = none
        [JsonProperty("protective")] public bool Protective;  // protective provisions signed
        [JsonProperty("drag_threshold")] public double DragThreshold;   // % of preferred that can force a sale, 0 = none
        [JsonProperty("signed_wk")] public int SignedWk;
    }

    /// <summary>The fundraising pipeline's durable state. null mirrors
    /// GDScript's empty {} — no raise has ever opened.</summary>
    public sealed class RaiseState
    {
        [JsonProperty("stages")] public List<Dictionary<string, object>> Stages =
            new List<Dictionary<string, object>>();
        [JsonProperty("interest_score")] public double InterestScore;
        [JsonProperty("active")] public bool Active;
        [JsonProperty("founder_time_tax")] public double FounderTimeTax;   // an active raise slows the shop
    }

    /// <summary>The hiring pipeline with real offers (recruitment). null
    /// mirrors GDScript's empty {} — nothing advertised yet. Row shapes are
    /// the ownership lane's to pin when it lands.</summary>
    public sealed class Recruitment
    {
        [JsonProperty("roles")] public List<Dictionary<string, object>> Roles =
            new List<Dictionary<string, object>>();
        [JsonProperty("candidates")] public List<Dictionary<string, object>> Candidates =
            new List<Dictionary<string, object>>();
        [JsonProperty("offers_out")] public List<Dictionary<string, object>> OffersOut =
            new List<Dictionary<string, object>>();
    }

    /// <summary>One feature of what we make — an ENGINE OBJECT (birth features
    /// from world gen + landed bets), never prose.</summary>
    public sealed class Feature
    {
        [JsonProperty("id")] public string Id = "";
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("job")] public string Job = "plumbing";   // pull | keep | charge | plumbing
        [JsonProperty("family")] public string Family = "";     // ink — a free tag, regroupable
        [JsonProperty("solidity")] public string Solidity = "solid";   // solid | creaky | breaking
        [JsonProperty("keep_wk")] public int KeepWk;            // $/wk — features are never free
        [JsonProperty("unit_cost_add")] public double UnitCostAdd;   // per-unit impact on the works' ticket
        [JsonProperty("product_id")] public string ProductId = "";   // "" = the flagship
        [JsonProperty("born_wk")] public int BornWk;
        [JsonProperty("measured")] public double Measured;      // measured payoff on recent landings, 0 = not yet
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
        // ── THEIR CONDUCT (03-rivals): what turns a strength number into a
        // company that DOES things. All additive — an old save loads at these
        // defaults and behaves exactly as it did.
        [JsonProperty("vigor")] public double Vigor = 55.0;            // war chest: acting burns it, resting restores it
        [JsonProperty("focus")] public string Focus = "growth";        // price | product | growth
        [JsonProperty("price_posture")] public double PricePosture = 1.0;   // their price vs the street's
        [JsonProperty("hype")] public double Hype = 20.0;              // share of voice, decays like adstock
        [JsonProperty("last_action")] public string LastAction = "";
        [JsonProperty("log")] public List<string> Log = new List<string>();   // the street tab's action log, cap 6
        [JsonProperty("cooldowns")] public Dictionary<string, int> Cooldowns = new Dictionary<string, int>();
        [JsonProperty("sniffing")] public int Sniffing;                // acquisition interest, handed to M&A
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

    /// <summary>
    /// One line of an offer's cost sheet: what it costs, and WHAT IT IS. A
    /// number the founder can read as "materials $12, courier $4" is a number
    /// they can act on; a lump `unit_cost` is not (01-catalog).
    /// </summary>
    public sealed class CostLine
    {
        [JsonProperty("label")] public string Label = "";
        [JsonProperty("amount")] public double Amount;
    }

    /// <summary>What we sell. price 0 = NOT ON SALE, and an unpriced product earns nothing.</summary>
    public sealed class Offer
    {
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("unit")] public string Unit = "";
        [JsonProperty("fair_price")] public double FairPrice = 0.0;   // 0 = unknown: pain falls back to price (Godot parity)
        [JsonProperty("elasticity")] public double Elasticity = 2.0;
        [JsonProperty("unit_cost")] public double UnitCost;
        [JsonProperty("price")] public double Price;
        [JsonProperty("price_set")] public bool PriceSet;   // a CONSCIOUS price choice, $0 included
        [JsonProperty("weight")] public double Weight = 1.0;
        // ── the itemized cost sheet (01-catalog). null = a legacy offer that
        // only ever had the scalar UnitCost; lines are never synthesized on load.
        [JsonProperty("cost_lines")] public List<CostLine> CostLines;    // variable, $ per unit
        [JsonProperty("fixed_lines")] public List<CostLine> FixedLines;  // $ per week, volume-free
        [JsonProperty("fixed_wk")] public double FixedWk;                // derived cache = sum of FixedLines
        // DAG2 W1: which product this offer packages. "" = the flagship; set
        // when a second product ships (roadmap) or by tag_offer. Inert until then.
        [JsonProperty("product_id")] public string ProductId = "";

        /// <summary>
        /// A DEEP copy. MemberwiseClone alone would hand the copy the SAME line
        /// objects, so editing one offer's cost sheet would silently edit the
        /// other's — the exact bug a duplicate is meant to prevent.
        /// </summary>
        public Offer Duplicate()
        {
            var c = (Offer)MemberwiseClone();
            if (CostLines != null)
            {
                c.CostLines = new List<CostLine>();
                foreach (CostLine l in CostLines)
                    c.CostLines.Add(new CostLine { Label = l.Label, Amount = l.Amount });
            }
            if (FixedLines != null)
            {
                c.FixedLines = new List<CostLine>();
                foreach (CostLine l in FixedLines)
                    c.FixedLines.Add(new CostLine { Label = l.Label, Amount = l.Amount });
            }
            return c;
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
        // 06-finance: nullable because pre-wave rows genuinely do not have it —
        // a reader falls back to (revenue - burn), which is close enough for
        // history and exact from here on.
        [JsonProperty("net")] public int? Net;
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

    /// <summary>
    /// The ledger's weekly levers. Every dollar leaves cash and does something.
    ///
    /// `Marketing` is the LEGACY key, kept so an old save still deserializes:
    /// SimEngine.MigrateBudgets folds it into `Ads` on load and at every tick
    /// start, and it stays 0 from then on. Acquisition spend is the four
    /// channels (04-funnel) — paid ads saturate, content compounds, referrals
    /// amplify word of mouth, outbound is quota math.
    /// </summary>
    public sealed class Budgets
    {
        [JsonProperty("marketing")] public int Marketing;   // legacy hook only — migrates into Ads
        [JsonProperty("ads")] public int Ads;
        [JsonProperty("content")] public int Content;
        [JsonProperty("referrals")] public int Referrals;
        [JsonProperty("outbound")] public int Outbound;
        [JsonProperty("sales")] public int Sales;
        [JsonProperty("care")] public int Care;
        [JsonProperty("rnd")] public int Rnd;
        [JsonProperty("office")] public int Office;   // food, perks, benefits

        /// <summary>Every lever, including the legacy key so a pre-migration
        /// read (runway, the desk total) is never short by the ads lane.</summary>
        public int Sum()
        {
            return Marketing + Ads + Content + Referrals + Outbound
                   + Sales + Care + Rnd + Office;
        }

        /// <summary>The four acquisition channels — what buys reach.</summary>
        public int Acquisition()
        {
            return Marketing + Ads + Content + Referrals + Outbound;
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
        [JsonProperty("last_pnl")] public Pnl LastPnl;   // the binder reads the week whole
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

        // ── SUBSYSTEM STATE (docs/design/00-spine.md section 8) ──────────────
        // Every field is additive with a default and a JSON name byte-identical
        // to the Godot save key, so a pre-wave save loads at the default and
        // RunSave.Version stays 2. Durable state is a FIELD, never Meta.

        // 01 catalog — cumulative customer-weeks served; drives the learning curve
        [JsonProperty("served_total")] public int ServedTotal;
        // 02 labor market
        [JsonProperty("open_roles")] public List<OpenRole> OpenRoles = new List<OpenRole>();
        [JsonProperty("applicants")] public List<Applicant> Applicants = new List<Applicant>();
        [JsonProperty("recruiters")] public int Recruiters;          // 0-2, floor era up
        [JsonProperty("severance_due")] public int SeveranceDue;     // the firing invoice, booked next tick
        // 04 funnel — the content channel's compounding stock
        [JsonProperty("content_equity")] public double ContentEquity;
        // 05 enterprise pipeline
        [JsonProperty("leads")] public List<Lead> Leads = new List<Lead>();
        [JsonProperty("logos")] public List<Logo> Logos = new List<Logo>();
        [JsonProperty("pipe_units")] public double PipeUnits;        // interest not yet attached to a name
        [JsonProperty("pipe_churn_acc")] public double PipeChurnAcc; // fractional account-churn accumulator
        [JsonProperty("pipe_stats")] public PipeStats PipeStats = new PipeStats();
        // 06 finance — structured notes (the legacy shark LoanPrincipal still stands)
        [JsonProperty("loans")] public List<Loan> Loans = new List<Loan>();
        [JsonProperty("tax_loss_carry")] public int TaxLossCarry;    // shelters later profit
        [JsonProperty("last_round_amount")] public int LastRoundAmount;
        [JsonProperty("receivables")] public List<Commitment> Receivables = new List<Commitment>();
        // 07 roadmap bets
        [JsonProperty("bets")] public List<Bet> Bets = new List<Bet>();
        [JsonProperty("platform_level")] public int PlatformLevel;   // 0-4, compounds velocity
        // 08 board + M&A — null until a round closes / an offer lands
        [JsonProperty("board")] public BoardState Board;
        [JsonProperty("mna")] public MnaOffer Mna;
        [JsonProperty("mna_last_week")] public int MnaLastWeek = -99;
        [JsonProperty("option_pool_pct")] public double OptionPoolPct;
        [JsonProperty("founder_banked")] public int FounderBanked;   // secondary proceeds, kept either way
        [JsonProperty("macro_season")] public string MacroSeason = "steady";   // written by macro only
        // 09 hardware production — null on every run that is not Hardware
        [JsonProperty("hardware")] public HardwareState Hardware;

        // ── DAG2 W1 — the binder rework's durable fields (docs/design/DAG2.md
        // §W1). Same law as above: additive with a default, JSON names
        // byte-identical to the Godot save keys, RunSave.Version stays 2.
        // Typed nulls mirror GDScript's empty {}; empty lists mirror [].
        // divisions & sites (W2 L-DIVWORKS)
        [JsonProperty("sites")] public List<Site> Sites = new List<Site>();
        /// <summary>THE PRICE BOOK: the structural price schedule generated at
        /// run start, empty until world-gen fills it. Keys: open_site_pack,
        /// relocation_fee, machine_shipping, lease_break_weeks,
        /// contract_notice_wks, refinance_break_fee, freelance_rate,
        /// subcontract_rate, account_fire_penalty.</summary>
        [JsonProperty("price_book")] public Dictionary<string, object> PriceBook =
            new Dictionary<string, object>();
        /// <summary>Generated-at-birth vocabulary (growth plots, spend rooms,
        /// works terms) — dressing only; engine numbers never live here.</summary>
        [JsonProperty("topics")] public Dictionary<string, object> Topics =
            new Dictionary<string, object>();
        [JsonProperty("spend_book")] public List<SpendLine> SpendBook = new List<SpendLine>();
        // the ownership cluster (W2 L-OWN)
        [JsonProperty("esop")] public Esop Esop;                       // null = no pool born yet
        [JsonProperty("instruments")] public List<Instrument> Instruments = new List<Instrument>();
        [JsonProperty("raise_state")] public RaiseState RaiseState;    // null = no raise opened
        [JsonProperty("recruitment")] public Recruitment Recruitment;  // null = nothing advertised
        // the features pipeline behind WHAT WE MAKE (W2 L-MAKE)
        [JsonProperty("features")] public List<Feature> Features = new List<Feature>();
        /// <summary>THE OFFER — the momentary buyout desk. Empty = nothing on
        /// the table; a live offer extends the board lane's M&amp;A offers with
        /// structure {cash, stock+lockup, earnout+controller, retention}.</summary>
        [JsonProperty("buyout_offer")] public Dictionary<string, object> BuyoutOffer =
            new Dictionary<string, object>();

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
                    // the SAME cushion law as office→floor: promotion into 5x
                    // rent was bankrupting healthy companies at week 21 (C5 D5)
                    if (HasFlag("launched") && Traction >= 25
                        && Cash >= 6 * ERA_RENT["office"])
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
            var d = new Dictionary<string, object>
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
                // 08 — present only when live.
                { "board_review", Board == null ? "" : string.Format(
                    "covenant ${0}/wk by wk {1} · now ${2}/wk · strikes {3} · goodwill {4}",
                    Board.TargetRevenue, Board.ReviewWeek,
                    LastPnl != null ? LastPnl.Revenue : 0, Board.Strikes, Board.Goodwill) },
                { "acquisition_offer", Mna == null ? "" : string.Format(
                    "{0} at ${1} — the no-shop ends wk {2}", Mna.Buyer, Mna.Price, Mna.ExpiresWeek) },
                { "ipo_window", HasFlag("ipo_window") },
                { "product", Product },
                { "traction", Traction },
                { "morale", Morale },
                { "hype", Hype },
                { "founder_pct", FounderPct },
                { "items", new List<string>(Items) },
                { "flags", new List<string>(Flags) },
            };
            // 05 — the named pipeline rides the digest (empty off Enterprise)
            foreach (var kv in SimPipeline.DigestRows(this)) { d[kv.Key] = kv.Value; }
            return d;
        }
    }
}
