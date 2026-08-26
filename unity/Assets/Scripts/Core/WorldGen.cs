using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace Runway.Core
{
    /// <summary>One row of the investor archetype deck: the maths the prose may never touch.</summary>
    public sealed class InvestorArchetype
    {
        [JsonProperty("archetype")] public string Archetype = "";
        /// <summary>x = founder-friendly(-1)..predatory(+1), y = contrarian(-1)..momentum(+1)</summary>
        [JsonProperty("coords")] public List<double> Coords = new List<double> { 0.0, 0.0 };
        [JsonProperty("thesis")] public string Thesis = "";
        [JsonProperty("tactics")] public List<string> Tactics = new List<string>();
    }

    // ── the LLM's world, as it arrives over the wire ─────────────────────────────

    public sealed class LlmMarket
    {
        [JsonProperty("tam_buyers")] public double? TamBuyers;
        [JsonProperty("customer_patience_weeks")] public double? CustomerPatienceWeeks;
        [JsonProperty("one_liner")] public string OneLiner = "";
    }

    public sealed class LlmInvestor
    {
        [JsonProperty("name")] public string Name = "an investor";
        // null means the key was absent, which is what picks the fallback archetype.
        [JsonProperty("archetype")] public string Archetype;
        [JsonProperty("thesis")] public string Thesis = "";
        [JsonProperty("trait")] public string Trait = "";
        [JsonProperty("bond")] public string Bond = "";
        [JsonProperty("flaw")] public string Flaw = "";
        [JsonProperty("secret")] public string Secret = "";
    }

    public sealed class LlmRival
    {
        [JsonProperty("name")] public string Name = "a rival";
        [JsonProperty("what_they_do")] public string WhatTheyDo = "";
        [JsonProperty("strength")] public string Strength = "scrappy";
        [JsonProperty("tactics")] public List<string> Tactics;
    }

    public sealed class LlmIdentity
    {
        [JsonProperty("one_liner")] public string OneLiner = "";
        [JsonProperty("who_for")] public string WhoFor = "";
    }

    public sealed class LlmTopic
    {
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("one_line")] public string OneLine = "";
    }

    public sealed class LlmGrowthTopics
    {
        [JsonProperty("ads")] public LlmTopic Ads;
        [JsonProperty("content")] public LlmTopic Content;
        [JsonProperty("referrals")] public LlmTopic Referrals;
        [JsonProperty("outbound")] public LlmTopic Outbound;
    }

    public sealed class LlmWorksTerms
    {
        [JsonProperty("unit_word")] public string UnitWord = "";
        [JsonProperty("capacity_word")] public string CapacityWord = "";
        [JsonProperty("relief_word")] public string ReliefWord = "";
    }

    public sealed class LlmSpendLine
    {
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("buys")] public string Buys = "";
        [JsonProperty("amt")] public double Amt;
        [JsonProperty("bucket")] public string Bucket = "";
        [JsonProperty("contract_notice")] public int ContractNotice;
    }

    public sealed class LlmPriceBook
    {
        [JsonProperty("open_site_pack")] public double? OpenSitePack;
        [JsonProperty("relocation_fee")] public double? RelocationFee;
        [JsonProperty("machine_shipping")] public double? MachineShipping;
        [JsonProperty("lease_break_weeks")] public double? LeaseBreakWeeks;
        [JsonProperty("contract_notice_wks")] public double? ContractNoticeWks;
        [JsonProperty("refinance_break_fee")] public double? RefinanceBreakFee;
        [JsonProperty("freelance_rate")] public double? FreelanceRate;
        [JsonProperty("subcontract_rate")] public double? SubcontractRate;
        [JsonProperty("account_fire_penalty")] public double? AccountFirePenalty;

        public double? Get(string key)
        {
            switch (key)
            {
                case "open_site_pack": return OpenSitePack;
                case "relocation_fee": return RelocationFee;
                case "machine_shipping": return MachineShipping;
                case "lease_break_weeks": return LeaseBreakWeeks;
                case "contract_notice_wks": return ContractNoticeWks;
                case "refinance_break_fee": return RefinanceBreakFee;
                case "freelance_rate": return FreelanceRate;
                case "subcontract_rate": return SubcontractRate;
                case "account_fire_penalty": return AccountFirePenalty;
            }
            return null;
        }
    }

    public sealed class LlmBirthFeature
    {
        [JsonProperty("name")] public string Name = "";
        [JsonProperty("job")] public string Job = "";
        [JsonProperty("keep_wk")] public double KeepWk;
        [JsonProperty("unit_cost_add")] public double UnitCostAdd;
    }

    public sealed class LlmWorld
    {
        [JsonProperty("market")] public LlmMarket Market;
        [JsonProperty("investors")] public List<LlmInvestor> Investors;
        [JsonProperty("rivals")] public List<LlmRival> Rivals;
        [JsonProperty("identity")] public LlmIdentity Identity;
        [JsonProperty("growth_topics")] public LlmGrowthTopics GrowthTopics;
        [JsonProperty("works_terms")] public LlmWorksTerms WorksTerms;
        [JsonProperty("spend_book")] public List<LlmSpendLine> SpendBook;
        [JsonProperty("price_book")] public LlmPriceBook PriceBook;
        [JsonProperty("birth_features")] public List<LlmBirthFeature> BirthFeatures;
    }

    /// <summary>
    /// THE WORLD BIBLE — generated once per run.
    ///
    /// Deterministic core: names from a seeded Markov chain (Nomina's count^1.3
    /// weighting), investors assembled Personae-style (archetype + alignment coords
    /// + trait + bond + flaw + secret), rivals with tactics. The LLM ENRICHES
    /// (Theta from the pitch, thesis lines in the world's own words) — but a
    /// keyless run gets a complete, playable world from here alone.
    /// </summary>
    public static class WorldGen
    {
        // ── Markov names (Nomina) ────────────────────────────────────────────────
        public static readonly string[] NAME_SEEDS = {
            "vanta", "loomly", "brightside", "koda", "meridian", "fluxo",
            "harbor", "nimbus", "verdant", "quill", "atlasgo", "pebble", "crestline",
            "sundial", "fernwood", "arclight", "tidepool", "monarch", "juniper", "cobalt",
            "drift", "ember", "willow", "stonefruit", "larkspur", "novabeam", "haven",
            "maple", "cinder", "bluefin", "orchard", "signal", "lumen", "basalt" };

        public static readonly string[] FUND_SUFFIX = {
            "Capital", "Ventures", "Partners", "Collective", "Fund", "Syndicate" };

        /// <summary>People are not companies: hires, cofounders and walk-ons draw from these.</summary>
        public static readonly string[] FIRST_NAMES = {
            "Mara", "Nico", "Priya", "Jonas", "Aiko", "Sam", "Lena",
            "Ravi", "Ines", "Theo", "Dana", "Milo", "Zara", "Owen", "Nadia", "Felix",
            "June", "Marco", "Elif", "Casper", "Rosa", "Ade", "Petra", "Yuki", "Bram" };

        public static readonly string[] LAST_NAMES = {
            "Sorel", "Okafor", "Lindgren", "Vance", "Marchetti", "Bakker",
            "Ito", "Novak", "Ferreira", "Duval", "Haddad", "Kowalski", "Mbeki", "Ander",
            "Voss", "Reyes", "Tanaka", "Bergstrom", "Cissé", "Moreau", "Silva", "Grant" };

        public static string PersonName(Rng rng)
        {
            string first = FIRST_NAMES[(int)(rng.Randi() % (uint)FIRST_NAMES.Length)];
            string last = LAST_NAMES[(int)(rng.Randi() % (uint)LAST_NAMES.Length)];
            return first + " " + last;
        }

        /// <summary>
        /// An insertion-ordered counter. GDScript Dictionaries keep insertion order
        /// and _pick_weighted walks them in exactly that order, so the C# port has
        /// to keep it too or the same stream would pick a different letter.
        /// </summary>
        private sealed class Counter
        {
            public readonly List<string> Keys = new List<string>();
            private readonly Dictionary<string, int> _v = new Dictionary<string, int>();

            public void Bump(string k)
            {
                int c;
                if (!_v.TryGetValue(k, out c))
                {
                    Keys.Add(k);
                    c = 0;
                }
                _v[k] = c + 1;
            }

            public int Get(string k)
            {
                int c;
                return _v.TryGetValue(k, out c) ? c : 0;
            }
        }

        private sealed class Chain
        {
            public Counter Initial = new Counter();
            public Dictionary<string, Counter> Trans = new Dictionary<string, Counter>();
            public List<int> Lens = new List<int>();
        }

        private static Chain BuildChain()
        {
            var ch = new Chain();
            foreach (string nm in NAME_SEEDS)
            {
                ch.Lens.Add(nm.Length);
                string first = nm.Substring(0, 1);
                ch.Initial.Bump(first);
                for (int i = 0; i < nm.Length - 1; i++)
                {
                    string a = nm.Substring(i, 1);
                    string b = nm.Substring(i + 1, 1);
                    Counter inner;
                    if (!ch.Trans.TryGetValue(a, out inner))
                    {
                        inner = new Counter();
                        ch.Trans[a] = inner;
                    }
                    inner.Bump(b);
                }
            }
            return ch;
        }

        private static string PickWeighted(Counter counts, Rng rng)
        {
            double total = 0.0;
            foreach (string k in counts.Keys)
            {
                total += Math.Pow(counts.Get(k), 1.3);
            }
            double x = rng.Randf() * total;
            foreach (string k in counts.Keys)
            {
                x -= Math.Pow(counts.Get(k), 1.3);
                if (x <= 0.0)
                {
                    return k;
                }
            }
            return counts.Keys.Count > 0 ? counts.Keys[0] : "a";
        }

        public static string MakeName(Rng rng)
        {
            Chain ch = BuildChain();
            for (int attempt = 0; attempt < 12; attempt++)
            {
                int ln = ch.Lens[(int)(rng.Randi() % (uint)ch.Lens.Count)];
                string outp = PickWeighted(ch.Initial, rng);
                while (outp.Length < ln)
                {
                    string last = outp.Substring(outp.Length - 1, 1);
                    Counter next;
                    if (!ch.Trans.TryGetValue(last, out next))
                    {
                        break;
                    }
                    outp += PickWeighted(next, rng);
                }
                if (Pronounceable(outp))
                {
                    return Gd.Capitalize(outp);
                }
            }
            return "Fernbay";   // the safety name after twelve unlucky draws
        }

        /// <summary>
        /// No three consonants in a row, at least one vowel per 3 letters — names a
        /// founder could actually say on a podcast.
        /// </summary>
        public static bool Pronounceable(string s)
        {
            const string vowels = "aeiouy";
            int run = 0;
            int vCount = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (vowels.IndexOf(c) >= 0)
                {
                    run = 0;
                    vCount += 1;
                }
                else
                {
                    run += 1;
                    if (run >= 3)
                    {
                        return false;
                    }
                }
            }
            return vCount * 3 >= s.Length;
        }

        // ── investor archetypes (Personae-style assembly) ────────────────────────
        public static readonly List<InvestorArchetype> INVESTOR_ARCHETYPES = new List<InvestorArchetype>
        {
            new InvestorArchetype { Archetype = "the momentum fund", Coords = new List<double> { 0.3, 0.9 },
                Thesis = "growth is the only truth; everything else is commentary",
                Tactics = new List<string> { "pushes for blitz spending", "goes cold the week growth dips" } },
            new InvestorArchetype { Archetype = "the contrarian angel", Coords = new List<double> { -0.6, -0.8 },
                Thesis = "the best deals look wrong to everyone else",
                Tactics = new List<string> { "funds what others passed on", "hates consensus rounds" } },
            new InvestorArchetype { Archetype = "the operator VC", Coords = new List<double> { -0.3, 0.2 },
                Thesis = "founders who ship beat founders who pitch",
                Tactics = new List<string> { "asks for the metrics dashboard", "intros real customers" } },
            new InvestorArchetype { Archetype = "the shark", Coords = new List<double> { 0.9, 0.4 },
                Thesis = "desperation is a pricing signal",
                Tactics = new List<string> { "waits until you are broke", "term sheets with teeth" } },
            new InvestorArchetype { Archetype = "the thesis tourist", Coords = new List<double> { 0.1, 0.6 },
                Thesis = "whatever the current wave is, they surfed in last month",
                Tactics = new List<string> { "loves the space this quarter", "vanishes next quarter" } },
        };

        public static readonly string[] INVESTOR_TRAITS = {
            "never blinks in meetings", "answers email at 3am only",
            "quotes their own blog", "keeps a kill list of passed deals",
            "brings a dog to diligence", "speaks entirely in sports metaphors",
            "has one great exit and infinite slides about it" };

        public static readonly string[] INVESTOR_BONDS = {
            "led the seed of a company you admire",
            "lost money on a company exactly like yours", "owes your ex-boss a favor",
            "is raising their own fund and needs winners" };

        public static readonly string[] INVESTOR_FLAWS = {
            "mistakes confidence for competence",
            "cannot say no in the room, says it by email", "reads only the top line",
            "funds people who remind them of themselves" };

        public static readonly string[] INVESTOR_SECRETS = {
            "their fund is nearly out of dry powder",
            "they already backed a competitor quietly", "their LPs are pushing for exits",
            "they decided in the first five minutes" };

        public static readonly List<List<string>> RIVAL_TACTICS = new List<List<string>>
        {
            new List<string> { "undercut pricing", "poached a customer", "shipped a clone feature" },
            new List<string> { "raised a loud round", "hired away talent", "bought ads on your name" },
            new List<string> { "landed a press feature", "announced a partnership", "opened your segment" },
        };

        /// <summary>
        /// What a business of this shape plausibly sells, priced by the market it
        /// serves — the deterministic skeleton the LLM refines. fair_price is the
        /// street's reference; elasticity is how hard demand punishes deviation.
        /// </summary>
        public static List<Offer> DefaultOffers(string what, string who, Rng rng)
        {
            // THE AUDIENCE SCALES THE INVOICE (C5 audit D1): only Software
            // priced by `who` — a Consumer was billed at SMB rates across four
            // thousand customers, a measured +$100k/wk money printer. Costs
            // scale WITH price so margin holds. Twin of world_gen.gd.
            double aud = who == "Consumer" ? 0.25 : (who == "Enterprise" ? 4.0 : 1.0);
            switch (what)
            {
                case "Service":
                    return new List<Offer>
                    {
                        new Offer { Name = "standard session", Unit = "per session",
                            FairPrice = rng.RandiRange(45, 85) * aud, Elasticity = 2.6,
                            UnitCost = 18.0 * aud, Price = 0.0, Weight = 0.7 },
                        // the premium lane is INELASTIC (C5 D2): pricing above
                        // fair must be a real strategy somewhere, not a cliff
                        new Offer { Name = "premium package", Unit = "per package",
                            FairPrice = rng.RandiRange(140, 260) * aud, Elasticity = 0.8,
                            UnitCost = 55.0 * aud, Price = 0.0, Weight = 0.3 },
                    };
                case "Hardware":
                    {
                        // GDScript evaluates a dictionary literal's values in written order:
                        // the device's fair_price, then its unit_cost, then the kit's price.
                        double devFair = rng.RandiRange(120, 420);
                        double devCost = rng.RandiRange(40, 150);
                        double kitFair = rng.RandiRange(25, 60);
                        return new List<Offer>
                        {
                            new Offer { Name = "the device", Unit = "per unit",
                                FairPrice = devFair * aud, Elasticity = 0.9,
                                UnitCost = devCost * aud, Price = 0.0, Weight = 0.8 },
                            new Offer { Name = "accessories", Unit = "per kit",
                                FairPrice = kitFair * aud, Elasticity = 2.4,
                                UnitCost = 9.0 * aud, Price = 0.0, Weight = 0.2 },
                        };
                    }
                case "Marketplace":
                    return new List<Offer>
                    {
                        // dollars per order, and SAYS so (C5 D7: a percent was
                        // booked as dollars; 25% read as 3x-fair greed)
                        new Offer { Name = "platform take, per order", Unit = "per order",
                            FairPrice = rng.RandiRange(8, 18) * aud, Elasticity = 3.0,
                            UnitCost = 1.0 * aud, Price = 0.0, Weight = 1.0 },
                    };
                default:
                    {
                        // Consumer is a flat 12 and draws NO die at all.
                        int baseP = who == "Consumer" ? 12
                            : (who == "SMB" ? rng.RandiRange(29, 79) : rng.RandiRange(190, 590));
                        return new List<Offer>
                        {
                            new Offer { Name = "monthly plan", Unit = "per month",
                                FairPrice = baseP, Elasticity = who != "Enterprise" ? 2.2 : 1.5,
                                UnitCost = 3.0, Price = 0.0, Weight = 0.8 },
                            new Offer { Name = "annual plan", Unit = "per year",
                                FairPrice = baseP * 10.0, Elasticity = 0.8,
                                UnitCost = 30.0, Price = 0.0, Weight = 0.2 },
                        };
                    }
            }
        }

        /// <summary>The complete deterministic bible, keyed on the run seed.</summary>
        public static void Build(GameState state)
        {
            Rng rng = Rng.World(state.SimSeed);
            // investors: three, distinct archetypes
            var picks = new List<InvestorArchetype>(INVESTOR_ARCHETYPES);
            var invs = new List<Investor>();
            for (int i = 0; i < 3; i++)
            {
                int idx = (int)(rng.Randi() % (uint)picks.Count);
                InvestorArchetype a = picks[idx];
                picks.RemoveAt(idx);
                string nm = MakeName(rng);
                string suffix = FUND_SUFFIX[(int)(rng.Randi() % (uint)FUND_SUFFIX.Length)];
                invs.Add(new Investor
                {
                    Name = nm + " " + suffix,
                    Archetype = a.Archetype,
                    Coords = a.Coords,
                    Thesis = a.Thesis,
                    Trait = INVESTOR_TRAITS[(int)(rng.Randi() % (uint)INVESTOR_TRAITS.Length)],
                    Bond = INVESTOR_BONDS[(int)(rng.Randi() % (uint)INVESTOR_BONDS.Length)],
                    Flaw = INVESTOR_FLAWS[(int)(rng.Randi() % (uint)INVESTOR_FLAWS.Length)],
                    Secret = INVESTOR_SECRETS[(int)(rng.Randi() % (uint)INVESTOR_SECRETS.Length)],
                    Tactics = a.Tactics,
                });
            }
            state.Investors = invs;
            // rivals: two, born from the same market
            var rivals = new List<Rival>();
            double rivalStrength = state.Theta != null ? state.Theta.RivalStrength : 20.0;
            for (int i = 0; i < 2; i++)
            {
                string nm = MakeName(rng);
                double strength = rivalStrength * rng.RandfRange(0.8, 1.3);
                List<string> tactics = RIVAL_TACTICS[(int)(rng.Randi() % (uint)RIVAL_TACTICS.Count)];
                string secret = rng.Randf() < 0.3 ? "quietly running out of money" : "";
                rivals.Add(new Rival
                {
                    Name = nm,
                    Strength = strength,
                    Tactics = tactics,
                    WeeksSinceMove = 0,
                    Secret = secret,
                });
            }
            state.Rivals = rivals;
            if (state.Offers == null || state.Offers.Count == 0)
            {
                state.Offers = DefaultOffers(state.BizWhat, state.BizWho, rng);
                // DECISIONS.md (catalog): the flagship carries ONE starter fixed
                // line so the catalog-overhead lane is alive from week 1.
                if (state.Offers != null && state.Offers.Count > 0)
                {
                    double aud0 = state.BizWho == "Consumer" ? 0.25
                                : state.BizWho == "Enterprise" ? 4.0 : 1.0;
                    Offer flag0 = state.Offers[0];
                    flag0.FixedLines = new List<CostLine>
                    {
                        new CostLine { Label = "the tools that make it", Amount = 15.0 * aud0 },
                    };
                    SimEngine.SyncOfferCosts(flag0);
                }
            }
            SeedRivalConduct(state, rng);
            // THE BIRTH BOOK'S KEYLESS HALF (DAG2 L-GEN): pure static tables,
            // no rng — dead last so every draw keeps its sequence position.
            DefaultBirth(state);
        }

        /// <summary>
        /// THE RIVALS' CONDUCT (03-rivals section 1): a war chest, a strategic
        /// bent, a price posture and a share of voice — what turns a strength
        /// number into a company that DOES things.
        ///
        /// Drawn at the very END of Build, after the offers, and never in the
        /// middle: inserting draws earlier would shift every later investor and
        /// offer draw and silently break worldgen determinism for every seed
        /// that already exists.
        /// </summary>
        public static void SeedRivalConduct(GameState state, Rng rng)
        {
            string[] focuses = { "price", "product", "growth" };
            foreach (Rival rd in state.Rivals)
            {
                rd.Vigor = rng.RandfRange(40.0, 70.0);
                rd.Hype = rng.RandfRange(10.0, 40.0);
                rd.Focus = focuses[(int)(rng.Randi() % 3u)];
                rd.PricePosture = 1.0;
                rd.LastAction = "";
                rd.Log = new List<string>();
                rd.Cooldowns = new Dictionary<string, int>();
                rd.Sniffing = 0;
            }
        }

        /// <summary>
        /// Merge an LLM-generated world onto the deterministic skeleton: names,
        /// theses and rivals come from the model (born from the pitch); coords and
        /// tactics decks come from the archetype so the engine math never depends
        /// on prose.
        /// </summary>
        public static bool ApplyLlmWorld(GameState state, LlmWorld gen)
        {
            if (gen == null)
            {
                return false;
            }
            LlmMarket market = gen.Market;
            if (market != null)
            {
                Theta th = state.Theta != null ? state.Theta.Duplicate() : new Theta();
                th.Tam = market.TamBuyers ?? (state.Theta != null ? state.Theta.Tam : 100000.0);
                th.LifetimeWk = market.CustomerPatienceWeeks ?? (state.Theta != null ? state.Theta.LifetimeWk : 40.0);
                state.Theta = SimEngine.ClampTheta(th);
                state.SetMeta("market_line", market.OneLiner ?? "");
            }
            var byArch = new Dictionary<string, InvestorArchetype>();
            foreach (InvestorArchetype a in INVESTOR_ARCHETYPES)
            {
                byArch[a.Archetype] = a;
            }
            var invs = new List<Investor>();
            if (gen.Investors != null)
            {
                foreach (LlmInvestor d in gen.Investors)
                {
                    InvestorArchetype arch;
                    if (!byArch.TryGetValue(d.Archetype ?? "", out arch))
                    {
                        arch = INVESTOR_ARCHETYPES[2];
                    }
                    invs.Add(new Investor
                    {
                        Name = Gd.Left(d.Name ?? "an investor", 40),
                        Archetype = d.Archetype ?? arch.Archetype,
                        Coords = arch.Coords,
                        Thesis = d.Thesis ?? "",
                        Trait = d.Trait ?? "",
                        Bond = d.Bond ?? "",
                        Flaw = d.Flaw ?? "",
                        Secret = d.Secret ?? "",
                        Tactics = arch.Tactics,
                    });
                }
            }
            if (invs.Count == 3)
            {
                state.Investors = invs;
            }
            var strMap = new Dictionary<string, double>
            {
                { "struggling", 12.0 }, { "scrappy", 25.0 }, { "strong", 45.0 }, { "dominant", 70.0 }
            };
            var rivals = new List<Rival>();
            if (gen.Rivals != null)
            {
                foreach (LlmRival r in gen.Rivals)
                {
                    string whatTxt = r.WhatTheyDo ?? "";
                    if (whatTxt.Length >= 135 && !whatTxt.EndsWith("."))
                    {
                        int wcut = whatTxt.LastIndexOf(' ');
                        if (wcut > 40)
                        {
                            whatTxt = whatTxt.Substring(0, wcut) + "…";
                        }
                    }
                    double strength;
                    if (!strMap.TryGetValue(r.Strength ?? "scrappy", out strength))
                    {
                        strength = 25.0;
                    }
                    string rname = Gd.Left(r.Name ?? "a rival", 30);
                    string[] llmFocuses = { "price", "product", "growth" };
                    rivals.Add(new Rival
                    {
                        Name = rname,
                        What = whatTxt,
                        Strength = strength,
                        Tactics = r.Tactics ?? new List<string> { "shipped something loud" },
                        WeeksSinceMove = 0,
                        Secret = "",
                        // no rng in scope on the LLM path, so conduct takes its
                        // defaults and the bent comes from the name itself —
                        // twin-safe, no hash involved
                        Vigor = 55.0,
                        Hype = 20.0,
                        Focus = llmFocuses[rname.Length % 3],
                        PricePosture = 1.0,
                        LastAction = "",
                        Log = new List<string>(),
                        Cooldowns = new Dictionary<string, int>(),
                        Sniffing = 0,
                    });
                }
            }
            if (rivals.Count == 2)
            {
                state.Rivals = rivals;
            }
            // the same call births the binder's own book (clamped; defaults
            // stand wherever the model came back thin)
            ApplyBirth(state, gen);
            return true;
        }

        // ══ THE BIRTH BOOK (DAG2 L-GEN, DECISIONS.md) — world_gen.gd's twins ══
        // The LLM proposes inside bands; ApplyBirth clamps again (the law).
        // DefaultBirth is the deterministic fallback: a keyless run gets a
        // complete playable book from these tables alone, and nothing here
        // draws from the rng.

        static readonly string[] GROWTH_CHANNELS = { "ads", "content", "referrals", "outbound" };
        static readonly Dictionary<string, string[]> GROWTH_DEFAULTS = new Dictionary<string, string[]>
        {
            { "ads", new[] { "the paid plot", "watered, it blooms the same day; unwatered, it dies the same day — and every extra dollar buys a little less" } },
            { "content", new[] { "the compost bed", "a stock that compounds while it is fed and rots the month it is starved" } },
            { "referrals", new[] { "the cutting vine", "a multiplier gated on how much the regulars actually like the thing" } },
            { "outbound", new[] { "the knocking rows", "quota knocking — so many doors a week per person out knocking" } },
        };
        // unit_word, capacity_word, relief_word
        static readonly Dictionary<string, string[]> WORKS_TERMS_DEFAULTS = new Dictionary<string, string[]>
        {
            { "Service", new[] { "session", "bookable hours", "freelancers" } },
            { "Hardware", new[] { "unit", "machine slots", "the subcontract shop" } },
            { "Marketplace", new[] { "order", "active sellers", "recruited supply" } },
            { "Software", new[] { "seat", "headroom", "burst capacity" } },
        };
        public static readonly Dictionary<string, int[]> PRICE_BANDS = new Dictionary<string, int[]>
        {
            { "open_site_pack", new[] { 6000, 40000 } }, { "relocation_fee", new[] { 100, 1500 } },
            { "machine_shipping", new[] { 150, 4000 } }, { "lease_break_weeks", new[] { 4, 16 } },
            { "contract_notice_wks", new[] { 2, 12 } }, { "refinance_break_fee", new[] { 100, 2000 } },
            { "freelance_rate", new[] { 15, 300 } }, { "subcontract_rate", new[] { 10, 250 } },
            { "account_fire_penalty", new[] { 200, 5000 } },
        };
        public static readonly Dictionary<string, int> PRICE_BOOK_DEFAULT = new Dictionary<string, int>
        {
            { "open_site_pack", 18000 }, { "relocation_fee", 400 }, { "machine_shipping", 900 },
            { "lease_break_weeks", 8 }, { "contract_notice_wks", 4 }, { "refinance_break_fee", 350 },
            { "freelance_rate", 65 }, { "subcontract_rate", 30 }, { "account_fire_penalty", 1200 },
        };
        static readonly string[] SPEND_BUCKETS = { "sales", "care", "rnd", "office" };
        static readonly string[] FEATURE_JOBS = { "pull", "keep", "charge", "plumbing" };
        const double SPEND_LINE_CAP = 400.0;
        const double SPEND_BOOK_CAP = 900.0;
        // name, job, keep_wk, unit_cost_add — every set carries the four jobs
        static readonly Dictionary<string, object[][]> FEATURE_DEFAULTS = new Dictionary<string, object[][]>
        {
            { "Service", new[] {
                new object[] { "the signature protocol", "keep", 30, 2.0 },
                new object[] { "online booking", "pull", 20, 0.0 },
                new object[] { "the premium add-on", "charge", 15, 3.0 },
                new object[] { "the back office", "plumbing", 25, 0.0 } } },
            { "Hardware", new[] {
                new object[] { "the core device", "keep", 40, 0.0 },
                new object[] { "the companion app", "pull", 25, 0.0 },
                new object[] { "the pro accessory line", "charge", 20, 2.0 },
                new object[] { "the assembly jigs", "plumbing", 30, 0.0 } } },
            { "Marketplace", new[] {
                new object[] { "search & matching", "pull", 35, 0.0 },
                new object[] { "ratings & reviews", "keep", 20, 0.0 },
                new object[] { "escrow & payouts", "charge", 25, 1.0 },
                new object[] { "the data plumbing", "plumbing", 30, 0.0 } } },
            { "Software", new[] {
                new object[] { "the onboarding door", "pull", 20, 0.0 },
                new object[] { "the daily workflow", "keep", 35, 0.0 },
                new object[] { "the paid tier", "charge", 15, 0.0 },
                new object[] { "the data plumbing", "plumbing", 30, 0.0 } } },
        };

        static string[] WorksDefaultsFor(string bizWhat)
        {
            string[] terms;
            if (!WORKS_TERMS_DEFAULTS.TryGetValue(bizWhat ?? "", out terms))
                terms = WORKS_TERMS_DEFAULTS["Software"];
            return terms;
        }

        static string IdentityFallback(GameState state)
        {
            string idea = (state.CompanyIdea ?? "").Trim();
            return idea.Length > 0 ? Gd.Left(idea, 140)
                : "a small company doing what it says on the door";
        }

        /// <summary>Install the complete deterministic birth book. Guarded per
        /// field so a save-loaded or LLM-filled state is never clobbered.</summary>
        public static void DefaultBirth(GameState state)
        {
            if (state.Topics == null || state.Topics.Count == 0)
            {
                var growth = new Dictionary<string, object>();
                foreach (string ch in GROWTH_CHANNELS)
                    growth[ch] = new Dictionary<string, object>
                    {
                        { "name", GROWTH_DEFAULTS[ch][0] }, { "one_line", GROWTH_DEFAULTS[ch][1] },
                    };
                string[] terms = WorksDefaultsFor(state.BizWhat);
                state.Topics = new Dictionary<string, object>
                {
                    { "identity", new Dictionary<string, object>
                        { { "one_liner", IdentityFallback(state) }, { "who_for", state.BizWho ?? "" } } },
                    { "growth", growth },
                    { "works", new Dictionary<string, object>
                        { { "unit_word", terms[0] }, { "capacity_word", terms[1] }, { "relief_word", terms[2] } } },
                };
            }
            if (state.SpendBook == null || state.SpendBook.Count == 0)
            {
                state.SpendBook = new List<SpendLine>
                {
                    new SpendLine { Name = "sales", Buys = "closing what is already in the pipe", Amt = 0, Bucket = "sales" },
                    new SpendLine { Name = "care", Buys = "keeping the customers we have", Amt = 0, Bucket = "care" },
                    new SpendLine { Name = "r&d", Buys = "building the thing", Amt = 0, Bucket = "rnd" },
                    new SpendLine { Name = "office", Buys = "the room and the people in it", Amt = 0, Bucket = "office" },
                };
            }
            if (state.PriceBook == null || state.PriceBook.Count == 0)
            {
                var pb = new Dictionary<string, object>();
                foreach (var kv in PRICE_BOOK_DEFAULT) pb[kv.Key] = kv.Value;
                state.PriceBook = pb;
            }
            if (state.Features == null || state.Features.Count == 0)
            {
                object[][] defs;
                if (!FEATURE_DEFAULTS.TryGetValue(state.BizWhat ?? "", out defs))
                    defs = FEATURE_DEFAULTS["Software"];
                var rows = new List<Feature>();
                for (int i = 0; i < defs.Length; i++)
                    rows.Add(new Feature
                    {
                        Id = "ft_birth_" + (i + 1), Name = (string)defs[i][0],
                        Job = (string)defs[i][1], KeepWk = (int)defs[i][2],
                        UnitCostAdd = (double)defs[i][3], BornWk = state.Week,
                    });
                state.Features = rows;
            }
        }

        /// <summary>Clamp-and-write the LLM's birth blocks over the defaults.
        /// Also the PIVOT regeneration entry point (a pivot keeps its investors
        /// and rivals; only the business's own book is reborn).</summary>
        /// <summary>The prompt asks for plain ASCII; the clamp enforces it — a
        /// stray glyph near a length boundary drops out instead of shipping.</summary>
        static string Ascii(string t)
        {
            if (string.IsNullOrEmpty(t)) return "";
            var sb = new System.Text.StringBuilder(t.Length);
            foreach (char c in t)
                if (c >= 32 && c <= 126) sb.Append(c);
            return sb.ToString().Trim();
        }

        public static bool ApplyBirth(GameState state, LlmWorld gen)
        {
            if (gen == null) return false;
            // ── topics: identity + growth plots + works terms, per-piece fallback
            var growth2 = new Dictionary<string, object>();
            var topicIn = new Dictionary<string, LlmTopic>
            {
                { "ads", gen.GrowthTopics != null ? gen.GrowthTopics.Ads : null },
                { "content", gen.GrowthTopics != null ? gen.GrowthTopics.Content : null },
                { "referrals", gen.GrowthTopics != null ? gen.GrowthTopics.Referrals : null },
                { "outbound", gen.GrowthTopics != null ? gen.GrowthTopics.Outbound : null },
            };
            foreach (string ch in GROWTH_CHANNELS)
            {
                LlmTopic t = topicIn[ch];
                string nm = t != null ? Gd.Left(Ascii(t.Name), 28) : "";
                string ln = t != null ? Gd.Left(Ascii(t.OneLine), 110) : "";
                if (nm.Length == 0 || ln.Length == 0)
                    growth2[ch] = new Dictionary<string, object>
                    { { "name", GROWTH_DEFAULTS[ch][0] }, { "one_line", GROWTH_DEFAULTS[ch][1] } };
                else
                    growth2[ch] = new Dictionary<string, object> { { "name", nm }, { "one_line", ln } };
            }
            string[] termsDef = WorksDefaultsFor(state.BizWhat);
            string oneLiner = gen.Identity != null ? Gd.Left(Ascii(gen.Identity.OneLiner), 140) : "";
            if (oneLiner.Length == 0) oneLiner = IdentityFallback(state);
            string whoFor = gen.Identity != null ? Gd.Left(Ascii(gen.Identity.WhoFor), 80) : "";
            string unitWord = gen.WorksTerms != null ? Gd.Left(Ascii(gen.WorksTerms.UnitWord), 16) : "";
            string capWord = gen.WorksTerms != null ? Gd.Left(Ascii(gen.WorksTerms.CapacityWord), 28) : "";
            string reliefWord = gen.WorksTerms != null ? Gd.Left(Ascii(gen.WorksTerms.ReliefWord), 28) : "";
            state.Topics = new Dictionary<string, object>
            {
                { "identity", new Dictionary<string, object>
                    { { "one_liner", oneLiner },
                      { "who_for", whoFor.Length > 0 ? whoFor : (state.BizWho ?? "") } } },
                { "growth", growth2 },
                { "works", new Dictionary<string, object>
                    { { "unit_word", unitWord.Length > 0 ? unitWord : termsDef[0] },
                      { "capacity_word", capWord.Length > 0 ? capWord : termsDef[1] },
                      { "relief_word", reliefWord.Length > 0 ? reliefWord : termsDef[2] } } },
            };
            // ── the spend book: 4-10 clean rows or the bare four lines
            var book = new List<SpendLine>();
            double total = 0.0;
            if (gen.SpendBook != null)
            {
                foreach (LlmSpendLine row in gen.SpendBook)
                {
                    if (row == null || book.Count >= 10) continue;
                    string rname = Gd.Left(Ascii(row.Name), 28);
                    if (rname.Length == 0) continue;
                    double amt = Math.Max(0.0, Math.Min(SPEND_LINE_CAP, row.Amt));
                    book.Add(new SpendLine
                    {
                        Name = rname, Buys = Gd.Left(Ascii(row.Buys), 60),
                        Amt = Gd.RoundToInt(amt),
                        Bucket = Array.IndexOf(SPEND_BUCKETS, row.Bucket ?? "") >= 0 ? row.Bucket : "office",
                        ContractNotice = Gd.Clampi(row.ContractNotice, 0, PRICE_BANDS["contract_notice_wks"][1]),
                        Division = "",
                    });
                    total += amt;
                }
            }
            if (total > SPEND_BOOK_CAP)
            {
                double scale = SPEND_BOOK_CAP / total;
                foreach (SpendLine r2 in book) r2.Amt = Gd.RoundToInt(r2.Amt * scale);
            }
            if (book.Count >= 4) state.SpendBook = book;
            else DefaultSpendBookInto(state);
            // ── the price book: every key inside its band, missing at default
            var pb2 = new Dictionary<string, object>();
            foreach (var kv in PRICE_BOOK_DEFAULT)
            {
                int[] band = PRICE_BANDS[kv.Key];
                double? v = gen.PriceBook != null ? gen.PriceBook.Get(kv.Key) : null;
                pb2[kv.Key] = Gd.Clampi(Gd.RoundToInt(v ?? kv.Value), band[0], band[1]);
            }
            state.PriceBook = pb2;
            // ── birth features: 3-6 rows, plumbing guaranteed
            var feats = new List<Feature>();
            double fair = 0.0;
            if (state.Offers != null && state.Offers.Count > 0) fair = state.Offers[0].FairPrice;
            double addCap = fair <= 0.0 ? 40.0 : Math.Min(40.0, fair * 0.35);
            if (gen.BirthFeatures != null)
            {
                foreach (LlmBirthFeature f in gen.BirthFeatures)
                {
                    if (f == null || feats.Count >= 6) continue;
                    string fname = Gd.Left(Ascii(f.Name), 28);
                    if (fname.Length == 0) continue;
                    feats.Add(new Feature
                    {
                        Id = "ft_birth_" + (feats.Count + 1), Name = fname,
                        Job = Array.IndexOf(FEATURE_JOBS, f.Job ?? "") >= 0 ? f.Job : "keep",
                        Family = "", Solidity = "solid",
                        KeepWk = Gd.Clampi(Gd.RoundToInt(f.KeepWk), 0, 150),
                        UnitCostAdd = Math.Round(Math.Max(0.0, Math.Min(addCap, f.UnitCostAdd)), 2),
                        ProductId = "", BornWk = state.Week, Measured = 0.0,
                    });
                }
            }
            if (feats.Count >= 3)
            {
                bool plumbed = false;
                foreach (Feature f2 in feats) if (f2.Job == "plumbing") plumbed = true;
                if (!plumbed)
                {
                    if (feats.Count >= 6) feats.RemoveAt(feats.Count - 1);
                    feats.Add(new Feature
                    {
                        Id = "ft_birth_" + (feats.Count + 1), Name = "the plumbing",
                        Job = "plumbing", Family = "", Solidity = "solid", KeepWk = 25,
                        UnitCostAdd = 0.0, ProductId = "", BornWk = state.Week, Measured = 0.0,
                    });
                }
                state.Features = feats;
            }
            return true;
        }

        static void DefaultSpendBookInto(GameState state)
        {
            state.SpendBook = new List<SpendLine>
            {
                new SpendLine { Name = "sales", Buys = "closing what is already in the pipe", Amt = 0, Bucket = "sales" },
                new SpendLine { Name = "care", Buys = "keeping the customers we have", Amt = 0, Bucket = "care" },
                new SpendLine { Name = "r&d", Buys = "building the thing", Amt = 0, Bucket = "rnd" },
                new SpendLine { Name = "office", Buys = "the room and the people in it", Amt = 0, Bucket = "office" },
            };
        }

        /// <summary>
        /// Investor-founder compatibility: the alignment dot product becomes a DC
        /// nudge on raise checks against THIS investor. Friendly-and-aligned = easier ask.
        /// </summary>
        public static int InvestorDcMod(Investor investor, IList<double> founderCoords)
        {
            List<double> c = (investor != null && investor.Coords != null && investor.Coords.Count >= 2)
                ? investor.Coords : new List<double> { 0.0, 0.0 };
            double dot = c[0] * founderCoords[0] + c[1] * founderCoords[1];
            return Gd.Clampi(Gd.RoundToInt(-dot * 3.0), -3, 3);
        }

        /// <summary>One paragraph the DM receives every call: who exists in this world.</summary>
        public static string BibleDigest(GameState state)
        {
            var bits = new List<string>();
            foreach (Investor d in state.Investors)
            {
                bits.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0} ({1}): \"{2}\" — {3}; {4}; flaw: {5}",
                    d.Name ?? "?", d.Archetype ?? "?", d.Thesis ?? "", d.Trait ?? "", d.Bond ?? "", d.Flaw ?? ""));
            }
            foreach (Rival r in state.Rivals)
            {
                string what = r.What ?? "";
                bits.Add(string.Format(CultureInfo.InvariantCulture,
                    "RIVAL {0} ({1}){2}: plays {3}",
                    r.Name ?? "?", SimEngine.Fuzz(r.Strength),
                    what != "" ? " — " + what : "",
                    string.Join(", ", r.Tactics ?? new List<string>())));
            }
            return string.Join("\n", bits);
        }
    }
}
