using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Core;
using Runway.Llm;

namespace Runway.Game
{
    /// <summary>
    /// THE COMMIT — the second half of garage_view_screen.gd's journal: the lock line,
    /// the clarify pre-pass, the die at the press, and everything the week does when it
    /// finally turns.
    ///
    /// THE COMMIT IS ONE PRESS. The old flow made the player lock twice — once to ask,
    /// once to accept. Here: the pen strikes a line under the words, the pre-pass asks
    /// its ONE question if the move hides its number, the die is cast AT THE PRESS
    /// (final, through the engine, with advantage and luck already applied), the
    /// curtain drops, and the world answers behind it.
    ///
    /// It is split out of `JournalSpreads` because the two halves answer to different
    /// things: the spreads draw the book, this drives the run.
    /// </summary>
    public sealed class WeekCommit
    {
        /// THE TELEGRAPH'S dictionary AND the roll's classifier are the same table, on
        /// purpose: the stat the player was shown while writing is the stat the die is
        /// rolled against. Two tables would be two games.
        internal static readonly Dictionary<string, string[]> StatSniff =
            new Dictionary<string, string[]>
        {
            { "build", new[] { "build", "ship", "code", "fix", "refactor", "feature", "prototype", "debug", "product" } },
            { "sell", new[] { "sell", "demo", "pitch to customer", "close", "customer", "pricing", "price", "door", "outreach", "market" } },
            { "raise", new[] { "raise", "investor", "term sheet", "fund", "vc", "angel", "round", "pitch deck" } },
            { "recruit", new[] { "hire", "recruit", "candidate", "interview", "offer letter", "poach", "team up" } },
            { "grit", new[] { "push through", "all night", "grind", "survive", "hold", "endure", "keep going", "morale" } },
        };

        readonly GarageScreen _g;
        readonly JournalSpreads _book;

        JournalPage _jp;
        RectTransform _lockRow;
        TextMeshProUGUI _lockWord;
        Button _lockBtn;
        JObject _pendingFree;
        JObject _pendingDice;

        public bool Adjudicating { get; private set; }
        /// The question on the page, while there is one. The spread draws it.
        public JObject Clarify { get; private set; }
        public bool ClarifyChecked;
        /// What the founder has written this week, kept across page turns.
        public string Written = "";

        GameState St { get { return _g.State; } }
        RunDriver Driver { get { return _g.Driver; } }

        public WeekCommit(GarageScreen g, JournalSpreads book)
        {
            _g = g;
            _book = book;
        }

        public void Reset()
        {
            _pendingFree = null;
            _pendingDice = null;
            Clarify = null;
            ClarifyChecked = false;
            Written = "";
        }

        /// Each rebuilt spread hands over the page it wants the lock drawn on.
        public void Attach(JournalPage jp) { _jp = jp; }

        public bool HasPending { get { return _pendingFree != null; } }

        /// Unclassifiable moves are GRIT: surviving the week on will alone is the
        /// default startup verb.
        public static string SniffStat(string t)
        {
            string low = (t ?? "").ToLower();
            string best = "grit";
            int bestHits = 0;
            foreach (var kv in StatSniff)
            {
                int hits = 0;
                for (int i = 0; i < kv.Value.Length; i++) if (low.Contains(kv.Value[i])) hits++;
                if (hits > bestHits) { bestHits = hits; best = kv.Key; }
            }
            return best;
        }

        // ══ the lock line ══════════════════════════════════════════════════════

        /// The commit lives in the CONTROLS BAND with the arrows, written not chromed.
        public void BuildLock()
        {
            if (_jp == null) return;
            if (_lockRow != null) Object.Destroy(_lockRow.gameObject);
            float ly = _jp.WritableBottom() - 54f;
            Vector2 sp = _jp.SpanAt(ly);
            _lockRow = DrawnUI.Rect(_jp.Space, "lock",
                sp.x + (sp.y - sp.x) * 0.5f - 170f, ly, 340f, 48f);
            var hit = _lockRow.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            _lockWord = DrawnUI.HandLabel(_lockRow, "", 0f, 0f, 34f, DrawnUI.Coral, 340f,
                                          TextAlignmentOptions.Center);
            _lockWord.rectTransform.anchorMin = Vector2.zero;
            _lockWord.rectTransform.anchorMax = Vector2.one;
            _lockWord.rectTransform.offsetMin = Vector2.zero;
            _lockWord.rectTransform.offsetMax = Vector2.zero;
            _lockBtn = _lockRow.gameObject.AddComponent<Button>();
            _lockBtn.transition = Selectable.Transition.None;
            _lockBtn.targetGraphic = hit;
            _lockBtn.onClick.AddListener(CommitPressed);
            RefreshLock();
        }

        /// READY = THE FOUNDER WROTE SOMETHING. The written move is the whole interface
        /// now; a verdict already in hand also counts.
        public void RefreshLock()
        {
            if (_lockWord == null) return;
            bool ready = _pendingFree != null || (Written ?? "").Trim().Length > 0;
            if (Adjudicating) ready = false;
            _lockWord.text = Adjudicating ? "the dice are out..."
                : (ready ? "ROLL THE WEEK" : "...decide first");
            _lockWord.color = ready ? DrawnUI.Coral : DrawnUI.WithAlpha(DrawnUI.Ink, 0.35f);
            if (_lockBtn != null) _lockBtn.interactable = ready;
        }

        /// THE COMMIT IS A CEREMONY, one beat long: the pen strikes a line under the
        /// words, the latch clicks, and only then does the week turn. An instant jump
        /// made the most consequential click in the game feel like a menu.
        void CommitPressed()
        {
            if (Adjudicating) return;
            Runway.Audio.Sfx.LockWeek();
            var boot = Boot.Instance;
            if (boot == null || _jp == null) { CommitFromText(); return; }
            boot.StartCoroutine(StrikeThen());
        }

        IEnumerator StrikeThen()
        {
            // under the WORDS, not the button's invisible box — the text is centred in it
            float w = DrawnUI.MeasureWidth(_lockWord.text, 34f);
            var rt = DrawnUI.Rect(_jp.Space, "stroke",
                _lockRow.anchoredPosition.x + (340f - w) * 0.5f - 8f,
                -_lockRow.anchoredPosition.y + 40f, w + 16f, 10f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = DrawnUI.WobbleLineSprite(Mathf.RoundToInt(w + 16f), 4f, 24, 1.4f, 23, 4);
            img.color = DrawnUI.Coral;
            img.raycastTarget = false;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 0f;
            float t = 0f;
            while (t < 0.14f)
            {
                t += Time.unscaledDeltaTime;
                if (img != null) img.fillAmount = Mathf.Clamp01(t / 0.14f);
                yield return null;
            }
            if (img != null) img.fillAmount = 1f;
            Runway.Effects.Scraps.Burst(_lockRow);
            yield return new WaitForSecondsRealtime(0.10f);
            CommitFromText();
        }

        // ══ the week goes to the world ═════════════════════════════════════════

        /// ONE press does the whole thing: the written move goes to the world, the
        /// verdict comes back, the week applies, the beat opens.
        public void CommitFromText()
        {
            // THE COMMIT GATE: one week in the world at a time. While the previous beat
            // is still open a press must do nothing — the probe caught a week whose
            // dice rolled and numbers applied while its beat was silently swallowed.
            if (Adjudicating || _g.WorldBusy) return;
            if (_pendingFree != null)
            {
                TurnRunner already = TurnRunner.Get();
                if (already != null) already.DropCurtain(null);
                ApplyLock();
                return;
            }

            string t = _jp != null ? _jp.WrittenText() : "";
            if (t.Length == 0) t = (Written ?? "").Trim();
            if (t.Length == 0) { ApplyLock(); return; }

            var boot = Boot.Instance;
            EventGenerator gen = boot != null ? boot.Generator : null;
            bool live = boot != null && boot.Llm != null && boot.Llm.Enabled;

            // ── THE PRE-PASS: one question when the move hides its number ──
            if (!ClarifyChecked && gen != null && live)
            {
                Adjudicating = true;
                RefreshLock();
                gen.Clarify(CoreSnapshot.From(St), _g.CurrentEvent, t, cq =>
                {
                    Adjudicating = false;
                    ClarifyChecked = true;
                    bool needs = ContentDb.Flag(cq, "needs_clarification");
                    string q = ContentDb.Str(cq, "question");
                    Debug.Log("CLARIFY " + (needs ? "asks: " + q : "silent"));
                    if (needs && q.Length > 0)
                    {
                        // ONE CLEAN LATIN LINE: the model once leaked a stray Cyrillic
                        // token onto the page — one line, ≤90, printable Latin-1 only
                        string raw = q.Split('\n')[0].Trim();
                        var sb = new System.Text.StringBuilder();
                        for (int i = 0; i < raw.Length && sb.Length < 90; i++)
                            if (raw[i] >= 32 && raw[i] < 592) sb.Append(raw[i]);
                        Clarify = new JObject
                        {
                            ["q"] = sb.ToString(),
                            ["kind"] = ContentDb.Str(cq, "kind", "other"),
                            ["base"] = t,
                        };
                        _book.Redraw();
                        return;
                    }
                    CommitFromText();
                });
                return;
            }

            ClarifyChecked = false;
            Adjudicating = true;
            RefreshLock();
            St.LogAction("wrote: " + (t.Length > 80 ? t.Substring(0, 80) : t));

            // THE DIE IS FINAL AT THE PRESS (owner: the roll happens right away). The
            // engine classifies the move's governing stat from the text itself — the
            // same classifier the telegraph showed the player — applies advantage or
            // disadvantage from state, and the cup pours the TRUE number instantly.
            // The keep rule, the founder's traits and luck's one intervention all live
            // in SimEngine.RollD20Ctx, so the die that lands on this desk is the same
            // die the contract suite proves every run.
            string stat = SniffStat(t);
            Rng rng = Driver.Rng;
            RollContext cx = SimEngine.RollD20Ctx(St, stat,
                () => rng != null ? rng.RandiRange(1, 20) : Random.Range(1, 21));
            string mode = "";
            if (cx.Advantage) mode = "advantage (" + string.Join(", ", cx.AdvReasons.ToArray()) + ")";
            else if (cx.Disadvantage) mode = "disadvantage (" + string.Join(", ", cx.DisReasons.ToArray()) + ")";
            if (!string.IsNullOrEmpty(cx.LuckNote))
                mode += (mode.Length > 0 ? " · " : "") + cx.LuckNote;
            _pendingDice = new JObject
            {
                ["a"] = cx.A, ["b"] = cx.B, ["used"] = cx.D20,
                ["stat"] = stat, ["mode"] = mode, ["mod"] = cx.Mod,
            };
            Debug.Log(string.Format("TURN dice used={0} of ({1},{2}) stat={3} {4}",
                cx.D20, cx.A, cx.B, stat, mode));

            TurnRunner runner = TurnRunner.Get();
            if (runner != null)
            {
                runner.ShowDie(cx.D20);          // the ceremony plays ON the room
                runner.DropCurtain(null);        // ...and the curtain waits for it
            }

            if (gen == null)
            {
                Adjudicating = false;
                Accept(EventGenerator.KeylessAdjudication(), t);
                return;
            }
            gen.Adjudicate(CoreSnapshot.From(St), _g.CurrentEvent, t, res =>
            {
                Adjudicating = false;
                if (res != null && Runway.Game.ContentDb.Str(res, "narration").Trim().Length == 0)
                    res["narration"] = Runway.Game.ContentDb.Str(
                        EventGenerator.KeylessAdjudication(), "narration");
                Accept(res ?? EventGenerator.KeylessAdjudication(), t);
            }, _pendingDice);
        }

        void Accept(JObject verdict, string playerText)
        {
            verdict["player_text"] = playerText;
            verdict["dice"] = _pendingDice ?? new JObject();
            verdict["week_played"] = St.Week;
            _pendingFree = verdict;
            var effects = verdict["effects"] as JArray;
            if (effects != null)
                foreach (JToken ef in effects)
                {
                    var e = ef as JObject;
                    if (ContentDb.Str(e, "op") == "set_flag"
                        && ContentDb.Str(e, "v") == "fundraising_open")
                        St.SetMeta("fundraising_week", St.Week);
                }
            ApplyLock();
        }

        /// The world asked, the founder answered: the move re-commits with it bound on.
        public void AnswerClarify(string answer)
        {
            string bas = ContentDb.Str(Clarify, "base");
            Clarify = null;
            Written = bas + " — " + answer;
            ClarifyChecked = true;
            CommitFromText();
        }

        // ══ the executor ═══════════════════════════════════════════════════════

        /// THE EXTENDED EXECUTOR (plan C2): classic meter ops go through EffectOps'
        /// clamps; engine ops (status/clock/levers/hire/loan/spend/budget) go through
        /// SimEngine — typed, clamped, catalog-only. Every op returns a receipt line.
        List<string> ApplyDmEffects(JToken effects)
        {
            var classic = new JArray();
            var outp = new List<string>();
            var arr = effects as JArray;
            if (arr == null) return outp;
            foreach (JToken t in arr)
            {
                var d = t as JObject;
                if (d == null) continue;
                string op = ContentDb.Str(d, "op");
                string why = ContentDb.Str(d, "why");
                switch (op)
                {
                    case "status":
                    {
                        string nm = ContentDb.Str(d, "v");
                        int wk = ContentDb.Int(d, "weeks", 2);
                        if (SimEngine.AddStatus(St, nm, wk))
                            outp.Add(string.Format("status: {0} for {1} wks — {2}", nm, wk, why));
                        break;
                    }
                    case "clock":
                    {
                        string cons = ContentDb.Str(d, "v");
                        int cw = ContentDb.Int(d, "weeks", 3);
                        SimEngine.AddClock(St, cw, cons);
                        outp.Add(string.Format("⏰ clock set ({0} wks): {1}", cw, cons));
                        break;
                    }
                    case "set_price":
                        St.PriceMult = Gd.Clampf(ContentDb.Num(d, "v", 1.0), 0.5, 2.0);
                        outp.Add(string.Format("price set to ×{0:0.00} — {1}", St.PriceMult, why));
                        break;
                    case "set_marketing":
                        St.MarketingBudget = Gd.Clampi(ContentDb.Int(d, "v", 0), 0, 50000);
                        outp.Add(string.Format("marketing ${0}/wk — {1}", St.MarketingBudget, why));
                        break;
                    case "hire":
                    {
                        string role = ContentDb.Str(d, "v", "engineer");
                        Rng hrng = Rng.FromKey(St.SimSeed + ":" + St.Week + ":" + St.Pipeline.Count);
                        string nm2 = WorldGen.PersonName(hrng);   // hires are people, not brands
                        int sal = role == "engineer" ? 1500 : role == "sales" ? 1200
                            : role == "support" ? 900 : role == "designer" ? 1100
                            : role == "ops" ? 1000 : 1200;
                        St.Pipeline.Add(new PipelineHire
                        {
                            Name = nm2, Role = role, Salary = sal, WeeksIn = 0,
                        });
                        outp.Add(string.Format("hired a {0} (${1}/wk, onboarding) — {2}",
                            role, sal, why));
                        break;
                    }
                    case "take_loan":
                    {
                        int amt = Gd.Clampi(ContentDb.Int(d, "v", 10000), 1000, 250000);
                        St.LoanPrincipal += amt;
                        St.Cash += amt;
                        outp.Add(string.Format("bridge loan +${0} at 18%/wk — {1}", amt, why));
                        break;
                    }
                    case "spend":
                    {
                        // THE MONEY LAW, engine side: the DM names the outlay, the ENGINE
                        // decides what cash can actually cover. Era-capped; never below
                        // zero — an unaffordable plan does not get its full spend.
                        int want = Gd.Clampi(ContentDb.Int(d, "v", 0), 0,
                                             SimEngine.EraSpendCap(St.Era));
                        int can = Gd.Mini(want, Gd.Maxi(St.Cash, 0));
                        if (can > 0)
                        {
                            St.Cash -= can;
                            outp.Add(string.Format("spent ${0} on {1} — {2}",
                                can, ContentDb.Str(d, "cat", "one_off"), why));
                        }
                        if (can < want)
                            outp.Add(string.Format(
                                "the bank stopped it at ${0} (wanted ${1}) — money you don't have doesn't spend",
                                can, want));
                        break;
                    }
                    case "set_budget":
                    {
                        string cat = ContentDb.Str(d, "cat", "marketing");
                        int amt = Gd.Clampi(ContentDb.Int(d, "v", 0), 0,
                                            SimEngine.EraSpendCap(St.Era));
                        if (!SetBudget(cat, amt)) { cat = "marketing"; SetBudget(cat, amt); }
                        if (cat == "marketing")
                            St.MarketingBudget = 0;   // one truth once the ledger takes over
                        outp.Add(string.Format("{0} budget set to ${1}/wk — {2}", cat, amt, why));
                        break;
                    }
                    default:
                        classic.Add(d);
                        break;
                }
            }
            outp.AddRange(EffectOps.ApplyAll(classic, St));
            Debug.Log("DM FX: " + string.Join("; ", outp.ToArray()));
            return outp;
        }

        /// Budgets is a typed record in Core, not a dictionary — this is the one place
        /// that has to speak both.
        bool SetBudget(string cat, int amt)
        {
            switch (cat)
            {
                case "marketing": St.Budgets.Marketing = amt; return true;
                case "sales": St.Budgets.Sales = amt; return true;
                case "care": St.Budgets.Care = amt; return true;
                case "rnd": St.Budgets.Rnd = amt; return true;
            }
            return false;
        }

        // ══ the week turns ═════════════════════════════════════════════════════

        void ApplyLock()
        {
            if (Adjudicating) return;
            _g.WeekPrev["cash"] = St.Cash;
            _g.WeekPrev["traction"] = St.Traction;
            _g.WeekPrev["product"] = St.Product;
            _g.WeekPrev["morale"] = St.Morale;
            var outcomeLog = new List<string>();

            // THE WORLD ACTS FIRST (plan A1): the hostile weekly tick runs before the
            // founder's move lands, and its receipts open the week's ledger.
            WeeklyReport tick = SimEngine.WeeklyTick(St);
            outcomeLog.AddRange(tick.Lines);
            for (int i = 0; i < tick.Events.Count; i++) outcomeLog.Add("⚡ " + tick.Events[i]);
            for (int i = 0; i < tick.FiredClocks.Count; i++)
            {
                outcomeLog.Add("⏰ THE DEADLINE HIT: " + tick.FiredClocks[i]);
                St.LogAction("deadline fired: " + tick.FiredClocks[i]);
            }

            string title = ContentDb.Str(_g.CurrentEvent, "title", "a quiet week");
            JObject outcome;
            if (_pendingFree != null)
            {
                List<string> log = ApplyDmEffects(_pendingFree["effects"]);
                outcomeLog.AddRange(log);
                string said = ContentDb.Str(_pendingFree, "player_text");
                Driver.Record.LogEvent(St.Week, _g.CurrentEvent, "[wrote] " + said, log);
                St.LogAction(string.Format("event '{0}' — wrote: {1} ({2})", title,
                    said.Length > 60 ? said.Substring(0, 60) : said,
                    ContentDb.Str(_pendingFree, "verdict")));
                outcome = new JObject
                {
                    ["title"] = title,
                    ["verdict"] = ContentDb.Str(_pendingFree, "verdict"),
                    ["said"] = said,
                    ["heard"] = ContentDb.Str(_pendingFree, "interpreted_as"),
                    ["narration"] = ContentDb.Str(_pendingFree, "narration"),
                    ["reality"] = ContentDb.Str(_pendingFree, "reality_check"),
                    ["dec_log"] = JArray.FromObject(log),
                    ["log"] = JArray.FromObject(outcomeLog),
                    // THE FULL DM PAYLOAD RIDES THE OUTCOME: the one-press commit sets
                    // the verdict and locks in the same frame, so a per-frame poll can
                    // miss a pending dict entirely — the exact silent failure that kept
                    // the whole beat-and-render pipeline dark through a real playthrough.
                    ["dm"] = _pendingFree.DeepClone(),
                    ["dm_seen"] = false,
                };
            }
            else
            {
                outcome = new JObject
                {
                    ["title"] = title, ["verdict"] = "", ["said"] = "", ["heard"] = "",
                    ["narration"] = "", ["reality"] = "",
                    ["dec_log"] = new JArray(), ["log"] = JArray.FromObject(outcomeLog),
                };
            }

            // the world never asks the same question twice
            string played = title.Trim();
            if (played.Length > 0)
            {
                St.PlayedEvents.Add(played);
                while (St.PlayedEvents.Count > 12) St.PlayedEvents.RemoveAt(0);
            }

            _g.LastOutcome = outcome;
            Driver.LastOutcome = outcome;
            // whatever branch wrote the week, the save remembers it (minus the one-shot dm)
            var forSave = (JObject)outcome.DeepClone();
            forSave.Remove("dm");
            St.LastOutcome = forSave.ToObject<Dictionary<string, object>>();

            var dmres = outcome["dm"] as JObject ?? new JObject();
            var traits = dmres["traits"] as JArray;
            if (traits != null)
                foreach (JToken tr in traits)
                {
                    string k = tr.ToString();
                    int had;
                    St.TraitsTally[k] = St.TraitsTally.TryGetValue(k, out had) ? had + 1 : 1;
                }
            string mem = ContentDb.Str(dmres, "memory").Trim();
            if (mem.Length > 0)
            {
                string[] words = mem.Split(' ');
                if (words.Length > 130) mem = string.Join(" ", words, 0, 130) + "…";
                St.StorySoFar = mem;
            }
            string[] milestones = { "launched", "first_revenue", "pmf", "seed_raised", "series_a" };
            for (int i = 0; i < milestones.Length; i++)
            {
                if (!St.HasFlag(milestones[i]) || St.HasFlag("xp_" + milestones[i])) continue;
                St.SetFlag("xp_" + milestones[i]);
                St.Xp += 1;
                outcomeLog.Add("★ MILESTONE: " + milestones[i]
                               + " — the founder levels (+1 stat to spend)");
            }

            // ...and the run's memory grows one week: what was said, what the die did,
            // what it cost — the DM reads this back every week from now on
            var fx = new List<string>();
            var effs = dmres["effects"] as JArray;
            if (effs != null)
                foreach (JToken ef in effs)
                {
                    var e = ef as JObject;
                    fx.Add(string.Format("{0} {1} — {2}", ContentDb.Str(e, "op"),
                        ContentDb.Str(e, "v"), ContentDb.Str(e, "why")));
                }
            St.RunHistory.Add(new RunHistoryEntry
            {
                Wk = St.Week,
                Said = Left(ContentDb.Str(outcome, "said"), 90),
                Verdict = ContentDb.Str(outcome, "verdict"),
                Roll = ContentDb.Int(dmres["dice"] as JObject, "used", 0),
                Fx = string.Join("; ", fx.ToArray()),
            });

            _pendingFree = null;
            _pendingDice = null;
            Written = "";
            St.ClampiMeters();
            _g.SyncRoom();

            // THE BEAT OPENS ON THE VERDICT the frame it is consumed, exactly once.
            var dmPayload = outcome["dm"] as JObject;
            if (dmPayload != null && !St.Dead && !St.HasFlag("exit_taken") && !_g.Over)
            {
                outcome["dm_seen"] = true;
                TurnRunner runner = TurnRunner.Get();
                if (runner != null) runner.BeginTurn((JObject)dmPayload.DeepClone());
            }

            if (St.Morale <= 0)
            {
                _g.Die("Founder Flatline — morale hit zero in week " + St.Week + ".");
                return;
            }
            // ACT BREAK — shipping an MVP is a chapter, not an ending. Below the top
            // floor the company keeps trading: a fresh week opens instead.
            if (St.Product >= 60 && St.Traction >= 10 && !St.HasFlag("act1_cleared"))
            {
                St.SetFlag("act1_cleared");
                Driver.Record.LogEvent(St.Week,
                    new JObject { ["id"] = "milestone", ["title"] = "MVP + first users" },
                    "era gate reached", null);
                    Runway.Audio.Sfx.Win();
            }
            _g.NextWeek();
        }

        static string Left(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= n ? s : s.Substring(0, n);
        }
    }
}
