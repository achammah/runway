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

    public sealed class LlmWorld
    {
        [JsonProperty("market")] public LlmMarket Market;
        [JsonProperty("investors")] public List<LlmInvestor> Investors;
        [JsonProperty("rivals")] public List<LlmRival> Rivals;
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
            return true;
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
