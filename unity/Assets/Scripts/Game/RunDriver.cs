using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Runway.App;
using Runway.Core;
using Runway.Llm;

namespace Runway.Game
{
    /// <summary>
    /// THE RUN, BEHIND THE SEAM — main.gd's half that Boot deliberately does not own.
    ///
    /// Boot owns the ORDER of the flow. This owns the RUN: the seed, the state, the
    /// record, the content deck, WorldGen, SimEngine, the world bible, day one, and
    /// the saves. Every method here is the beat of main.gd its comment names.
    ///
    /// THE TWO PREFETCHES ARE THE WHOLE POINT OF THIS FILE.
    ///   · THE BIBLE starts on the BAG page, keyed on "name|idea", so by the time the
    ///     papers are signed the world is usually already written and the birth screen
    ///     only shows for a breath. A re-entry with the same pitch never pays twice.
    ///   · DAY ONE starts the moment the bible lands, so the book opens on the
    ///     founder's own first entry instead of a placeholder — and the moment day one
    ///     is written, its room starts painting, while the book is still being read.
    ///
    /// NOTHING HERE AWAITS A NETWORK CALL WITHOUT A CEILING. Boot's own 25s gate
    /// covers the bible; the founding retries exactly once and then stands down; the
    /// paint is watched by the book and released by the director either way.
    /// </summary>
    public sealed class RunDriver : IRunDriver
    {
        /// The screens reach the run through this — never through Boot.
        public static RunDriver Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (Current == null) Boot.PendingDriver = new RunDriver();
        }

        public RunDriver()
        {
            Current = this;
            ContentDb.InstallCoreReader();
        }

        // ── the run ────────────────────────────────────────────────────────────
        public GameState State { get; private set; }
        public RunRecord Record { get; private set; }
        public Rng Rng { get; private set; }
        public ContentDb Content { get; private set; }

        int _slot = 1;
        bool _contentLoaded;

        public ContentDb Deck
        {
            get
            {
                if (Content == null) Content = new ContentDb();
                if (!_contentLoaded) { _contentLoaded = true; Content.LoadAll(); }
                return Content;
            }
        }

        EventGenerator Generator
        {
            get { return Boot.Instance != null ? Boot.Instance.Generator : null; }
        }

        bool LlmLive
        {
            get { return Boot.Instance != null && Boot.Instance.Llm != null && Boot.Instance.Llm.Enabled; }
        }

        // ══ slots ══════════════════════════════════════════════════════════════

        public SaveSlotInfo[] ListSlots()
        {
            var rows = new SaveSlotInfo[SaveSlots.SlotCount];
            for (int i = 0; i < rows.Length; i++) rows[i] = SaveSlots.Read(i + 1);
            return rows;
        }

        public void SetActiveSlot(int slot)
        {
            _slot = Mathf.Clamp(slot, 1, SaveSlots.SlotCount);
            SaveSlots.ActiveSlot = _slot;
        }

        public void ClearRun() { SaveSlots.Clear(_slot); }

        public bool HasSavedRun() { return SaveSlots.Read(_slot).Exists; }

        /// _start_run's resume half: the slot, the rng rebuilt off seed+week, and a
        /// cleared card pool so last session's prefetch never lands on this week.
        public bool ResumeSavedRun()
        {
            GameState st;
            RunRecord rec;
            if (!RunSave.Load(_slot, out st, out rec)) return false;
            State = st;
            Record = rec;
            Rng = new Rng((ulong)(Record.SeedValue + State.Week));
            if (Generator != null) Generator.Pool.Clear();
            _worldgenKey = State.CompanyName + "|" + State.CompanyIdea;
            _worldgenLanded = true;      // a resumed run already has its world
            _worldgenInflight = false;
            Deck.LoadAll();
            Debug.Log(string.Format("RUNWAY! resumed {0}, week {1} ({2})",
                State.CompanyName, State.Week, State.Era));
            return true;
        }

        /// _start_run's fresh half.
        public void BeginFreshRun(bool daily)
        {
            long seed = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (daily)
            {
                DateTime d = DateTime.Now;
                seed = d.Year * 10000L + d.Month * 100L + d.Day;
            }
            Rng = new Rng((ulong)seed);
            State = new GameState();
            Record = new RunRecord { SeedValue = seed };
            _worldgenKey = "";
            _worldgenRes = null;
            _worldgenInflight = false;
            _worldgenLanded = false;
            _foundingRes = null;
            _foundingInflight = false;
            BookShowedEntry = false;
            Deck.LoadAll();
            if (Generator != null)
            {
                Generator.Pool.Clear();
                Generator.Disabled = daily;     // authored-only: a daily run deals no generated card
            }
            Debug.Log("RUNWAY! new run · seed " + seed + (daily ? " · DAILY" : ""));
        }

        public string CompanyName { get { return State != null ? State.CompanyName : ""; } }

        // ══ _after_draft, the engine half ══════════════════════════════════════

        public void ApplyDraft(object draftResult)
        {
            var d = draftResult as DraftResult;
            if (d == null)
            {
                Debug.LogWarning("RUNWAY! the draft handed back something that is not a DraftResult.");
                return;
            }
            if (State == null) BeginFreshRun(false);

            JObject arch = d.Archetype ?? new JObject();
            JObject fund = d.Funding ?? new JObject();

            State.ArchetypeId = ContentDb.Str(arch, "id");
            State.ArchetypeName = ContentDb.Str(arch, "name", "founder");
            var stats = arch["stats"] as JObject;
            if (stats != null)
            {
                var comp = new Dictionary<string, int>();
                foreach (var kv in stats) comp[kv.Key] = ContentDb.Int(stats, kv.Key, 3);
                State.Competences = comp;
            }
            var traits = arch["traits"] as JObject;
            if (traits != null)
            {
                var tr = new Dictionary<string, int>();
                foreach (var kv in traits) tr[kv.Key] = ContentDb.Int(traits, kv.Key, 3);
                State.Traits = tr;
            }
            State.CompanyName = string.IsNullOrEmpty(d.CompanyName) ? "Untitled Inc" : d.CompanyName;
            State.FounderName = d.FounderName ?? "";
            State.CompanyIdea = d.CompanyIdea ?? "";
            State.BizWhat = string.IsNullOrEmpty(d.BizWhat) ? "Software" : d.BizWhat;
            State.BizWho = string.IsNullOrEmpty(d.BizWho) ? "Consumer" : d.BizWho;
            State.FundingId = ContentDb.Str(fund, "id", "bootstrap");
            State.StructureId = d.Cofounders.Count == 0 ? "solo" : "team";

            // cap table: founding splits 100%, then investors dilute EVERYONE pro-rata
            double cfEquity = 0.0;
            State.Cofounders = new List<Cofounder>();
            for (int i = 0; i < d.Cofounders.Count; i++)
            {
                DraftCofounder c = d.Cofounders[i];
                cfEquity += c.Equity;
                State.Cofounders.Add(new Cofounder
                {
                    Name = c.Name ?? "",
                    Role = c.Role ?? "Tech",
                    Commitment = c.Commitment ?? "Full-time",
                    Equity = c.Equity,
                    Vesting = c.Vesting ? "4y/1y cliff" : "",
                });
            }
            double dilution = 1.0 - ContentDb.Num(fund, "equity_cost", 0.0) / 100.0;
            for (int i = 0; i < State.Cofounders.Count; i++)
                State.Cofounders[i].EquityDiluted = State.Cofounders[i].Equity * dilution;
            State.FounderPct = (100.0 - cfEquity) * dilution;
            State.Cash += ContentDb.Int(arch, "start_cash_bonus", 0) + ContentDb.Int(fund, "cash", 0);

            // competence coverage: full-time roles patch stats, part-time patches half
            for (int i = 0; i < d.Cofounders.Count; i++)
            {
                DraftCofounder c = d.Cofounders[i];
                bool full = c.Commitment == "Full-time";
                switch (c.Role)
                {
                    case "Tech":
                    case "Technical":
                        Bump("build", full ? 4 : 3);
                        break;
                    case "Business":
                        Bump("sell", full ? 4 : 3);
                        Bump("raise", 3);
                        break;
                    case "Design":
                        State.Competences["build"] = Gd.Mini(5, State.Competence("build") + 1);
                        Bump("sell", 3);
                        break;
                    // "Sales", "Hustler", "The Idea Friend": the joke is that they patch nothing
                }
            }
            if (d.Cofounders.Count > 0) State.SetFlag("has_cofounder");
            for (int i = 0; i < d.Traps.Count; i++)
            {
                string t = d.Traps[i];
                if (t != "solo") State.SetFlag(t);
            }
            for (int i = 0; i < d.Items.Count; i++)
            {
                State.Items.Add(d.Items[i]);
                State.Cash += Deck.CashValue(d.Items[i]);
            }
            if (State.Cash <= 0) State.Cash = 1500;   // emergency couch cushions

            // loyalty is a per-cofounder consumable Core does not model; it rides the
            // state's own metadata so it saves with everything else
            for (int i = 0; i < State.Cofounders.Count; i++) SetLoyalty(i, 70);

            string who = State.FounderName.Length > 0 ? State.FounderName : State.ArchetypeName;
            Record.LogEvent(0,
                new JObject { ["id"] = "draft", ["title"] = "The Founding of " + State.CompanyName },
                string.Format("{0} ({1}) · {2} cofounder(s) · {3} · kept {4:0}%",
                    who, State.ArchetypeName, d.Cofounders.Count,
                    ContentDb.Str(fund, "name"), State.FounderPct),
                null);

            // THE WORLD IS BORN: seed the engine, then the bible
            if (State.SimSeed == 0) State.SimSeed = Record.SeedValue;
            if (State.Theta == null) State.Theta = SimEngine.DefaultTheta(State.BizWhat, State.BizWho);
            if (State.Investors == null || State.Investors.Count == 0) WorldGen.Build(State);

            // Tier-3 run director: the run's narrative arcs
            if (Generator != null)
            {
                Generator.GenerateArcs(CoreSnapshot.From(State), arcs =>
                {
                    if (State == null || arcs == null || arcs.Count == 0) return;
                    try { State.Arcs = arcs.ToObject<List<Arc>>(); }
                    catch (Exception e) { Debug.LogWarning("RUNWAY! arcs rejected: " + e.Message); }
                });
            }
        }

        void Bump(string stat, int to)
        {
            State.Competences[stat] = Gd.Maxi(State.Competence(stat), to);
        }

        // ── cofounder loyalty, kept on the state's metadata ────────────────────

        public int Loyalty(int index)
        {
            if (State == null) return 70;
            return Gd.ToInt(State.GetMetaF("cf_loyalty_" + index, 70.0));
        }

        public void SetLoyalty(int index, int value)
        {
            if (State == null) return;
            State.SetMeta("cf_loyalty_" + index, Gd.Clampi(value, 0, 100));
        }

        // ══ the world bible ════════════════════════════════════════════════════

        JObject _worldgenRes;
        string _worldgenKey = "";
        bool _worldgenInflight;
        bool _worldgenLanded;

        public bool WorldgenInFlight { get { return _worldgenInflight; } }
        public bool WorldgenLanded { get { return _worldgenLanded; } }

        /// THE BAG PAGE STARTS THE WORLD: the pitch is written by the time the founder
        /// is packing, so the bible generates behind them.
        public void PrefetchWorld(string companyName, string companyIdea,
                                  string bizWhat, string bizWho)
        {
            if (!LlmLive || Generator == null) return;
            string nm = (companyName ?? "").Trim();
            if (nm.Length == 0) return;
            string key = nm + "|" + (companyIdea ?? "").Trim();
            if (_worldgenKey == key) return;      // this pitch is already paid for
            _worldgenKey = key;
            _worldgenRes = null;
            _worldgenLanded = false;
            _worldgenInflight = true;
            var scratch = new RunSnapshot
            {
                CompanyName = nm,
                CompanyIdea = companyIdea ?? "",
                BizWhat = bizWhat ?? "Software",
                BizWho = bizWho ?? "Consumer",
            };
            Generator.GenerateWorld(scratch, gen =>
            {
                _worldgenInflight = false;
                _worldgenRes = gen;
                _worldgenLanded = gen != null && gen.Count > 0;
                Debug.Log("WORLDGEN prefetched during the bag page"
                          + (_worldgenLanded ? "" : " (empty)"));
            });
        }

        /// The birth screen's own guard: the prefetch missed (a name edited at the last
        /// second, or a direct entry) — start it now.
        public void EnsureWorldgen()
        {
            if (State == null) return;
            string here = State.CompanyName + "|" + State.CompanyIdea;
            if (_worldgenKey == here) return;
            PrefetchWorld(State.CompanyName, State.CompanyIdea, State.BizWhat, State.BizWho);
        }

        /// _finish_worldgen: apply the bible + seed beliefs, then start day one. The
        /// entry is returned only if it has ALREADY landed — otherwise the book opens
        /// on its placeholder and FoundingLanded feeds it in.
        public string FinishWorldgen()
        {
            if (State == null) return "";
            if (_worldgenRes == null || _worldgenRes.Count == 0)
                Debug.Log("WORLDGEN: skeleton world (prefetch empty or timed out)");
            else
            {
                try
                {
                    LlmWorld world = _worldgenRes.ToObject<LlmWorld>();
                    WorldGen.ApplyLlmWorld(State, world);
                    JObject market = _worldgenRes["market"] as JObject;
                    string oneLiner = ContentDb.Str(market, "one_liner");
                    if (oneLiner.Length > 0) State.SetMeta("market_line", oneLiner);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("RUNWAY! the bible would not apply (" + e.Message
                                     + ") — the deterministic skeleton stands.");
                }
            }
            SimEngine.SeedBeliefs(State);
            PrefetchFounding(1);
            if (_foundingRes != null)
                return ContentDb.Str(_foundingRes, "narration");
            return "";
        }

        // ══ day one ════════════════════════════════════════════════════════════

        JObject _foundingRes;
        bool _foundingInflight;

        /// Set by the book once the reader has actually HELD the entry on screen — a
        /// fast SETTLE IN before it landed still earns the day-one beat.
        public bool BookShowedEntry;

        /// The book subscribes: the entry arrived while the page was open.
        public event Action<string> FoundingLanded;
        /// The book subscribes: the warm paint finished (or finally failed).
        public event Action PaintSettled;

        /// The last line of defense for day one (#175 parity with the Godot
        /// build): no key or three dead attempts — the engine writes the entry
        /// from facts it owns. No fiction, the same lowercase night-writing.
        public JObject AuthoredFounding()
        {
            var lines = new List<string>();
            lines.Add("signed the lease tonight. the shutter sticks. the key works.");
            if (State.CompanyIdea.Length > 0)
                lines.Add("what we are promising: "
                          + State.CompanyIdea.Trim().TrimEnd('.').ToLowerInvariant() + ".");
            lines.Add("cash in the drawer: $" + State.Cash + ". that is the whole runway.");
            var priced = new List<string>();
            if (State.Offers != null)
                foreach (var o in State.Offers)
                    if (o != null && o.Price > 0.0)
                        priced.Add(o.Name.ToLowerInvariant() + " at $" + (int)o.Price);
            if (priced.Count == 0)
                lines.Add("nothing has a price on it yet. that conversation is coming, "
                          + "and i suspect it will hurt.");
            else
                lines.Add("prices on the wall: " + string.Join(", ", priced) + ".");
            lines.Add("tomorrow we open the door and find out which of my guesses survive.");
            return new JObject
            {
                ["narration"] = string.Join("\n\n", lines),
                ["reality_check"] = "",
                ["scene"] = new JObject(),
                ["cast"] = new JArray(),
                ["effects"] = new JArray(),
            };
        }

        public bool FoundingReady { get { return _foundingRes != null; } }
        public bool FoundingInFlight { get { return _foundingInflight; } }

        /// The authored fallback, installed as THE founding when the wait ran
        /// out with nothing in hand — feeds the book exactly like a live one.
        public string AdoptAuthoredFounding()
        {
            if (_foundingRes != null)
            {
                string live = ContentDb.Str(_foundingRes, "narration");
                if (live.Trim().Length > 0) return live;
                // schema-valid but EMPTY narration: the book would hold its
                // door forever on a blank entry — the authored one stands in
            }
            _foundingRes = AuthoredFounding();
            string narration = ContentDb.Str(_foundingRes, "narration");
            var fl = FoundingLanded;
            if (fl != null) fl(narration);
            return narration;
        }

        string FoundingMove()
        {
            string whoFounds = State.FounderName.Length > 0
                ? " The founder writing this is " + State.FounderName + "."
                : "";
            string idea = State.CompanyIdea.Length > 0
                ? State.CompanyIdea
                : "a company that refuses to explain itself";
            return string.Format(
                "This is day one of {0} — {1}.{2}"
                + " Write DAY ONE as the FIRST ENTRY OF THE FOUNDER'S OWN LOGBOOK "
                + "(a journal de bord): first person, plain sentences, the opening "
                + "pages of a business memoir. I sign the lease, I look at the room, "
                + "I name what we are actually promising and what it will cost. The "
                + "place, the crew, the first stake in the ground. No dice language, "
                + "no verdict talk, no company-brochure tone — a person writing at "
                + "night on day one.", State.CompanyName, idea, whoFounds);
        }

        void PrefetchFounding(int attempt)
        {
            if (!LlmLive || Generator == null || _foundingInflight || State == null) return;
            _foundingInflight = true;
            Debug.Log("FOUNDING request (attempt " + attempt + ")");
            Generator.Adjudicate(CoreSnapshot.From(State), new JObject(), FoundingMove(), res =>
            {
                _foundingInflight = false;
                if ((res == null || res.Count == 0) && attempt < 3)
                {
                    // transport failures must not cost day one: the hard watchdog
                    // kills wedged attempts fast enough that two retries still
                    // land inside the birth screen's wait
                    Debug.Log("FOUNDING empty — retry " + (attempt + 1));
                    PrefetchFounding(attempt + 1);
                    return;
                }
                if (res == null || res.Count == 0)
                {
                    // every attempt is dead: the engine writes day one itself —
                    // a plain true entry beats an empty page every time (#175)
                    Debug.Log("FOUNDING dead after " + attempt + " attempts — the engine writes day one");
                    res = AuthoredFounding();
                }
                _foundingRes = res;
                Debug.Log("FOUNDING landed (" + ContentDb.Str(res, "narration").Length + " chars)");
                // THE PAINT STARTS AT THE SIGNATURE: the room begins rendering while
                // the book is still being read.
                WarmScene(res);
                string narration = ContentDb.Str(res, "narration");
                var fl = FoundingLanded;
                if (fl != null) fl(narration);
                // the player is already in the room waiting on the curtain: play it
                if (Boot.Instance != null && Boot.Instance.State == AppState.Garage)
                    ConsumeFounding();
            }, null, "founding");   // NO DICE ON DAY ONE; the founding tier gets the fast 50s watchdog
        }

        /// THE ROOM NEVER STAYS BARREN (owner #208): a failed founding paint
        /// gets re-kicked by the garage — the retained verdict re-enters the
        /// director, which paints or fails honestly a second time.
        public void RewarmFounding()
        {
            if (_foundingRes != null) WarmScene(_foundingRes);
        }

        void WarmScene(JObject dm)
        {
            var director = Boot.Instance != null ? Boot.Instance.Director : null;
            if (director == null || State == null) return;
            var scene = dm["scene"] as JObject;
            if (scene == null || scene.Count == 0) return;
            string outName = string.Format("run{0}_wk{1:00}", Record != null ? Record.SeedValue : 0,
                                           State.Week);
            director.WarmScene(scene, CastRoster(dm["cast"] as JArray), new string[0],
                               ContentDb.Str(scene, "beat"), outName, CompanyCtx());
        }

        public JObject CompanyCtx()
        {
            if (State == null) return new JObject();
            return new JObject
            {
                ["name"] = State.CompanyName,
                ["idea"] = State.CompanyIdea,
                ["what"] = State.BizWhat,
                ["who"] = State.BizWho,
            };
        }

        /// _cold_open: the curtain drops, day one is written, and the beat reads it
        /// while the first image of THIS company renders behind it.
        public void ColdOpen()
        {
            if (!LlmLive || Generator == null) return;
            TurnRunner runner = TurnRunner.Get();
            if (runner != null) runner.DropCurtain("day one is being written…");
            if (_foundingRes != null) { ConsumeFounding(); return; }
            if (_foundingInflight) return;    // the callback consumes it the moment it lands
            PrefetchFounding(1);              // direct entry: write it now
        }

        void ConsumeFounding()
        {
            JObject res = _foundingRes;
            _foundingRes = null;
            TurnRunner runner = TurnRunner.Get();
            if (res == null || State == null)
            {
                if (runner != null) runner.RaiseCurtain();
                return;
            }
            res["player_text"] = "";
            res["interpreted_as"] = "";
            res.Remove("dice");
            res.Remove("roll");
            res["week_played"] = 0;
            // only skip the beat if the reader actually HELD the entry on the book
            res["book_read"] = BookShowedEntry;

            var outcome = new JObject
            {
                ["title"] = "day one",
                ["verdict"] = "",
                ["said"] = "",
                ["heard"] = "",
                ["narration"] = ContentDb.Str(res, "narration"),
                ["reality"] = ContentDb.Str(res, "reality_check"),
                ["dec_log"] = new JArray(),
                ["log"] = new JArray(),
                ["dm"] = res.DeepClone(),
                ["dm_seen"] = true,
            };
            LastOutcome = outcome;
            if (runner != null) runner.BeginTurn(res);
        }

        /// What last week's locked decision caused. The garage reads it for the
        /// was-page; the driver keeps it so a screen rebuild cannot lose it.
        public JObject LastOutcome;

        // ── the cast the painter is told about ────────────────────────────────

        static readonly Dictionary<string, string> RoleKeys = new Dictionary<string, string>
        {
            { "tech", "tech" }, { "technical", "tech" }, { "design", "tech" },
            { "engineer", "tech" }, { "business", "business" }, { "ops", "business" },
            { "sales", "sales" }, { "hustler", "hustler" }, { "idea", "idea_friend" },
            { "the idea friend", "idea_friend" },
        };

        public static string RoleKey(string role)
        {
            string k = (role ?? "").ToLower().Trim();
            string v;
            if (RoleKeys.TryGetValue(k, out v)) return v;
            foreach (var kv in RoleKeys) if (k.Contains(kv.Key)) return kv.Value;
            return "tech";
        }

        /// _cast_pack, minus the sprite-url filter: `assets/scenes/refs.json` is not in
        /// this project, so nobody can be shown to the model — but the roster still
        /// NAMES this company's real people, and the generator path paints from the
        /// description. A cast the run does not have is still dropped.
        public JArray CastRoster(JArray dmCast)
        {
            var outCast = new JArray();
            if (dmCast == null || State == null) return outCast;
            Dictionary<string, JObject> roster = CrewRoster();
            var used = new List<string>();
            foreach (JToken t in dmCast)
            {
                var c = t as JObject;
                if (c == null) continue;
                string who = ContentDb.Str(c, "who").ToLower();
                if (!roster.ContainsKey(who) || used.Contains(who)) continue;
                used.Add(who);
                JObject person = roster[who];
                outCast.Add(new JObject
                {
                    ["who"] = who,
                    ["role"] = ContentDb.Str(person, "role") + ContentDb.Str(person, "mood_words"),
                    ["doing"] = ContentDb.Str(c, "doing", "at work"),
                });
            }
            if (outCast.Count == 0 && roster.ContainsKey("founder"))
            {
                JObject f = roster["founder"];
                outCast.Add(new JObject
                {
                    ["who"] = "founder",
                    ["role"] = ContentDb.Str(f, "role") + ContentDb.Str(f, "mood_words"),
                    ["doing"] = "in the middle of it",
                });
            }
            return outCast;
        }

        /// Who this company actually contains, keyed by the words the DM uses. The
        /// moods follow the same rules the room uses, so the picture never disagrees
        /// with the crew line.
        public Dictionary<string, JObject> CrewRoster()
        {
            var outp = new Dictionary<string, JObject>();
            if (State == null) return outp;
            bool fBurnt = State.Morale <= 30 || State.WeeksInRed >= 2;
            outp["founder"] = new JObject
            {
                ["role"] = State.ArchetypeName.Length > 0
                    ? State.ArchetypeName.ToLower() + ", the founder" : "founder",
                ["mood"] = fBurnt ? "burnt" : "fine",
                ["mood_words"] = fBurnt ? " (burnt out, running on fumes)" : "",
            };
            for (int i = 0; i < State.Cofounders.Count; i++)
            {
                Cofounder cf = State.Cofounders[i];
                string key = RoleKey(cf.Role);
                if (outp.ContainsKey(key)) continue;
                bool sour = Loyalty(i) <= 30 || State.Morale <= 20
                            || State.HasFlag("trap_underpaid_cofounder");
                outp[key] = new JObject
                {
                    ["role"] = (cf.Role ?? "tech").ToLower() + " cofounder",
                    ["mood"] = sour ? "burnt" : "fine",
                    ["mood_words"] = sour ? " (burnt out, running on fumes)" : "",
                };
            }
            for (int i = 0; i < State.Employees.Count; i++)
            {
                Employee e = State.Employees[i];
                string key = RoleKey(e.Role);
                if (outp.ContainsKey(key)) continue;
                string bs = GameState.BurnoutState(e.Burnout);
                bool cooked = bs == "cooked" || bs == "gone";
                outp[key] = new JObject
                {
                    ["role"] = (e.Name ?? "the hire").ToLower() + ", the " + (e.Role ?? "hire").ToLower(),
                    ["mood"] = cooked ? "burnt" : "fine",
                    ["mood_words"] = cooked ? " (burnt out, running on fumes)" : "",
                };
            }
            return outp;
        }

        // ══ saving and ending ══════════════════════════════════════════════════

        int _lastSavedWeek = -1;

        /// main.gd's _process save: once per week change, never under a harness.
        public void SaveIfWeekTurned()
        {
            if (State == null || Boot.Instance == null) return;
            if (State.Week == _lastSavedWeek) return;
            _lastSavedWeek = State.Week;
            if (!Boot.Instance.Harness) RunSave.Save(_slot, State, Record);
        }

        /// A run that ended clears its desk — one ongoing run at a time.
        public void EndRun()
        {
            SaveSlots.Clear(_slot);
            _lastSavedWeek = -1;
        }

        /// The Godot _after_grind: an exit earns its headline, and the last page is
        /// where every ending lands.
        public void AfterGrind(JObject result)
        {
            TurnRunner runner = TurnRunner.Get();
            if (runner != null) runner.CancelTurn();
            string headline;
            if (ContentDb.Has(result, "death"))
                headline = ContentDb.Str(result, "death");
            else if (State != null && State.HasFlag("acquired_exit"))
                headline = "SOLD THE COMPANY — you shook the hand in week " + State.Week + ".";
            else if (ContentDb.Has(result, "victory") && State != null && State.Era == "hq")
                headline = "RANG THE BELL — " + State.CompanyName + " went public in week " + State.Week + ".";
            else
                headline = "SURVIVED: MVP shipped, first users on board. (Act 1 gate — more acts coming.)";
            if (State != null)
                headline += string.Format("\nYour slice today: ${0}  ({1:0}% of the company)",
                    GameUi.Money(State.PayoutToday()), State.FounderPct);
            EndRun();
            var boot = Boot.Instance;
            if (boot == null) return;
            boot.Go(AppState.Autopsy, headline, s => { s.Done += _ => boot.ToTitle(); });
        }

        /// main.gd's _check_exit, called on a week change. Returns true when the run
        /// is over and the last page has been asked for.
        public const int RunWeekCap = 78;

        public bool CheckExit()
        {
            if (State == null || State.Dead || State.HasFlag("exit_taken")) return false;
            TurnRunner runner = TurnRunner.Get();
            if (runner != null && runner.TurnBusy) return false;   // a week still being read
            string reason = "";
            if (State.Era == "hq" && State.Valuation() >= 25000000 && State.Traction >= 70)
                reason = "ipo";
            else if (State.Week >= RunWeekCap)
                reason = (State.Era == "hq" && State.Cash > 0) ? "ipo" : "timeout";
            if (reason.Length == 0) return false;
            State.SetFlag("exit_taken");
            if (runner != null) runner.CancelTurn();
            if (reason == "timeout")
            {
                AfterGrind(new JObject
                {
                    ["death"] = string.Format(
                        "THE LONG HAUL — {0} weeks in, the story ran out before the money did.",
                        State.Week),
                });
            }
            else
            {
                AfterGrind(new JObject { ["victory"] = true });
            }
            return true;
        }

        /// The book asks the director whether day one's room is still being painted.
        public PaintStatus WarmPaint
        {
            get
            {
                var d = Boot.Instance != null ? Boot.Instance.Director : null;
                return d != null ? d.WarmStatus : PaintStatus.Idle;
            }
        }

        public void NotifyPaintSettled()
        {
            var ps = PaintSettled;
            if (ps != null) ps();
        }
    }
}
