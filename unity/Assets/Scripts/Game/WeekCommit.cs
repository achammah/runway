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
        /// THE ROUNDS (owner: "multiple rounds of questions/answers until no
        /// ambiguity"): each answer re-runs the pre-pass on the merged move,
        /// up to 3 questions per commit, until it goes silent.
        int _clarifyRounds;
        bool _priceAsked;
        /// THE PRE-ROLL REVIEW (docs/design/DECISIONS.md #2, owner requirement):
        /// before ANY dice roll — the weekly lock included — the world lays out
        /// everything still outstanding and offers two exits: go back and fix it,
        /// or roll anyway. Zero items, no card. It asks ONCE a week: a founder who
        /// has already read the list and chosen is not asked to read it again on
        /// the next press.
        public JObject Preroll { get; private set; }
        bool _prerollDone;
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
            _clarifyRounds = 0;
            _priceAsked = false;
            Preroll = null;
            _prerollDone = false;
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

        RectTransform _lockBadge;

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
            // DAG3 — THE LOCK IN BADGE: the count of standing attention items
            // rides the commit button all week (the binder-bang idiom with a
            // number), so the player sees how much is unset BEFORE the
            // pre-roll card has to say so.
            if (_lockBadge != null) Object.Destroy(_lockBadge.gameObject);
            _lockBadge = null;
            int nAtt = St != null ? SimEngine.AttentionItems(St).Count : 0;
            if (nAtt > 0 && _lockRow != null)
                _lockBadge = DeskKit.CountBadge(_lockRow, 318f, -8f, nAtt);
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

            // ── THE PRICING LAW IS ENGINE-OWNED (owner #192): customers on
            // the books with not one price on the wall IS the clarification —
            // it cannot depend on the model noticing.
            if (!ClarifyChecked && !_priceAsked && Clarify == null
                && (St.Traction > 0 || St.HasFlag("launched")) && St.Offers != null)
            {
                bool anyPriced = false;
                string firstName = "the offer";
                for (int i = 0; i < St.Offers.Count; i++)
                    if (St.Offers[i] != null)
                    {
                        if (i == 0 && St.Offers[i].Name != null && St.Offers[i].Name.Length > 0)
                            firstName = St.Offers[i].Name;
                        if (St.Offers[i].Price > 0.0 || St.Offers[i].PriceSet) { anyPriced = true; break; }
                    }
                if (!anyPriced)
                {
                    _priceAsked = true;
                    Clarify = new JObject
                    {
                        ["q"] = "customers are here and nothing has a price — what does "
                                + firstName + " cost?",
                        ["kind"] = "price",
                        ["base"] = t,
                    };
                    _book.Redraw();
                    return;
                }
            }

            // ── THE PRE-PASS: one question when the move hides its number ──
            if (!ClarifyChecked && _clarifyRounds < 3 && gen != null && live)
            {
                Adjudicating = true;
                RefreshLock();
                gen.Clarify(CoreSnapshot.From(St), _g.CurrentEvent, t, cq =>
                {
                    Adjudicating = false;
                    bool needs = ContentDb.Flag(cq, "needs_clarification");
                    string q = ContentDb.Str(cq, "question");
                    Debug.Log("CLARIFY " + (needs ? "asks: " + q : "silent"));
                    if (needs && q.Length > 0)
                    {
                        _clarifyRounds++;
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
                    ClarifyChecked = true;   // silence ends the rounds
                    CommitFromText();
                });
                return;
            }

            // ── THE PRE-ROLL REVIEW (docs/design/DECISIONS.md #2) ──────────────
            // The world has stopped asking questions; the die has not been cast.
            // THIS is the moment to show what is still unset — an unpriced offer,
            // a note you cannot cover, a bet finished and unshipped — because
            // after the roll it is a consequence and no longer a choice. Zero
            // outstanding items, no card.
            if (!_prerollDone)
            {
                _prerollDone = true;
                List<AttentionItem> outstanding = SimEngine.PrerollItems(St);
                // THE PROBES NEVER STALL: a headless run answers "roll anyway".
                bool autoRoll = Env.Get("RUNWAY_UFLOW", "").Length > 0
                    || Env.Get("RUNWAY_USHOTS", "").Length > 0
                    || Env.Get("RUNWAY_UPERF", "").Length > 0
                    || Env.Flag("RUNWAY_FULLRUN") || Env.Flag("RUNWAY_FIRSTFLOW")
                    || Env.Flag("RUNWAY_SHOT");
                if (outstanding.Count > 0 && !autoRoll)
                {
                    var rows = new JArray();
                    foreach (AttentionItem it in outstanding)
                        rows.Add(new JObject
                        {
                            ["desk"] = it.Desk, ["label"] = it.Label, ["severity"] = it.Severity,
                        });
                    Preroll = new JObject { ["items"] = rows, ["base"] = t };
                    _book.Redraw();
                    return;
                }
            }

            ClarifyChecked = false;
            _clarifyRounds = 0;
            _priceAsked = false;
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

        /// GO FIX IT: the book closes and the binder opens ON the loudest item's desk —
        /// the founder lands looking at the thing the world stopped them for. DAG3 S2:
        /// the jump goes through JumpToAsk, so a row that names its control lands with
        /// the coach's spotlight already on the switch.
        public void PrerollFix()
        {
            if (Preroll == null) return;
            var rows = Preroll["items"] as JArray;
            var row = rows != null && rows.Count > 0 ? rows[0] as JObject : null;
            string toDesk = row != null ? ContentDb.Str(row, "desk") : "";
            string toControl = row != null ? ContentDb.Str(row, "control") : "";
            Written = ContentDb.Str(Preroll, "base");
            Preroll = null;
            _g.CloseJournal();
            _g.OpenBinderOnAsk(toDesk, toControl);
        }

        /// ROLL ANYWAY: the week goes as written, and the card does not ask twice.
        public void PrerollRoll()
        {
            if (Preroll == null) return;
            Written = ContentDb.Str(Preroll, "base");
            Preroll = null;
            _book.Redraw();
            CommitFromText();
        }

        /// The world asked, the founder answered: the move re-commits with it bound on.
        public void AnswerClarify(string answer)
        {
            string bas = ContentDb.Str(Clarify, "base");
            Clarify = null;
            Written = bas + " — " + answer;
            // the merged move goes back through the pre-pass (owner: rounds
            // until no ambiguity); the round cap keeps the pace playable
            ClarifyChecked = _clarifyRounds >= 3;
            CommitFromText();
        }

        // ══ the executor ═══════════════════════════════════════════════════════

        /// THE EXTENDED EXECUTOR (plan C2): classic meter ops go through EffectOps'
        /// clamps; engine ops (status/clock/levers/hire/loan/spend/budget) go through
        /// SimEngine — typed, clamped, catalog-only. Every op returns a receipt line.
        static Dictionary<string, object> DmDict(JToken d)
        {
            return d.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
        }

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
                    case "draft_offer":
                        // THE JOURNAL DRAFTS THE FORM (owner): the binder's offer
                        // form opens on these words; the DM never creates or
                        // prices the offer itself.
                        St.OfferDraft = Gd.Left(ContentDb.Str(d, "v"), 500);
                        outp.Add("an offer drafted from the week's move — finish it in the binder");
                        break;
                    case "price_offer":
                    {
                        // THE PRICE LANDS IN THE OFFER (owner: the world must
                        // know what we sell and at how much). cat = offer name,
                        // v = $ per unit; v=0 is a CONSCIOUS free choice.
                        double pv = Gd.Clampf(ContentDb.Num(d, "v", 0.0), 0.0, 50000.0);
                        string pname = ContentDb.Str(d, "cat").Trim();
                        Offer hit = null;
                        if (St.Offers != null)
                        {
                            foreach (Offer po in St.Offers)
                            {
                                string onm = (po.Name ?? "").ToLowerInvariant();
                                string pl = pname.ToLowerInvariant();
                                if (pname.Length > 0 && (onm.Contains(pl) || (onm.Length > 0 && pl.Contains(onm))))
                                {
                                    hit = po; break;
                                }
                            }
                            if (hit == null)
                                foreach (Offer po in St.Offers)
                                    if (po.Price <= 0.0 && !po.PriceSet) { hit = po; break; }
                        }
                        if (hit == null && pname.Length > 0)
                        {
                            // A NAME NOBODY SELLS YET is a new offer — and a new
                            // offer goes through AddOffer like every other, so
                            // the world's clamps (fair price, marginal cost,
                            // elasticity, shelf weight) apply to it too.
                            // Appending the object raw was a hole straight past
                            // every one of them (DECISIONS.md Wave A #4).
                            hit = SimCatalog.AddOffer(St, pname, "per order", pv, 0.0, 2.0, 1.0);
                        }
                        else if (hit == null && St.Offers != null && St.Offers.Count > 0)
                        {
                            hit = St.Offers[0];   // everything priced: it's a reprice
                        }
                        if (hit != null)
                        {
                            hit.Price = pv;
                            hit.PriceSet = true;
                            outp.Add(pv > 0.0
                                ? string.Format("{0} priced at ${1} — {2}", hit.Name, (int)pv, why)
                                : string.Format("{0} is free on purpose — {1}", hit.Name, why));
                        }
                        break;
                    }
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
                            : role == "ops" ? 1000 : role == "marketing" ? 1300 : 1200;
                        St.Pipeline.Add(new PipelineHire
                        {
                            Name = nm2, Role = role, Salary = sal, WeeksIn = 0,
                        });
                        outp.Add(string.Format("hired a {0} (${1}/wk, onboarding) — {2}",
                            role, sal, why));
                        break;
                    }
                    case "pivot_audience":
                    {
                        var pa = SimPivot.PivotAudience(St, ContentDb.Str(d, "v"));
                        if (pa.Ok) foreach (string pl in pa.Lines) outp.Add("THE PIVOT: " + pl);
                        else outp.Add("the pivot was refused — " + pa.Reason);
                        break;
                    }
                    case "pivot_product":
                    {
                        var pp = SimPivot.PivotProduct(St, ContentDb.Str(d, "v"));
                        if (pp.Ok) foreach (string pl2 in pp.Lines) outp.Add("THE PIVOT: " + pl2);
                        else outp.Add("the pivot was refused — " + pp.Reason);
                        break;
                    }
                    case "pitch_investor":
                    {
                        string pline = SimOwnership.OpPitchInvestor(St, ContentDb.Str(d, "v"));
                        if (pline.Length > 0) outp.Add(pline + " — " + why);
                        break;
                    }
                    case "sign_instrument":
                    {
                        string sline = SimOwnership.OpSignInstrument(St, ContentDb.Str(d, "v"));
                        if (sline.Length > 0) outp.Add(sline + " — " + why);
                        break;
                    }
                    case "send_offer":
                    {
                        string cname = ContentDb.Str(d, "v").ToLowerInvariant();
                        string targetId = "";
                        if (St.Recruitment != null && St.Recruitment.Candidates != null)
                            foreach (var c in St.Recruitment.Candidates)
                            {
                                object nmO; c.TryGetValue("name", out nmO);
                                object stO; c.TryGetValue("stage", out stO);
                                string nm = nmO as string ?? "";
                                string stg = stO as string ?? "";
                                if (nm.ToLowerInvariant().Contains(cname)
                                    && (stg == "applied" || stg == "interviewed"))
                                {
                                    object idO; c.TryGetValue("id", out idO);
                                    targetId = idO as string ?? "";
                                    break;
                                }
                            }
                        if (targetId.Length > 0)
                        {
                            int mk = SimLabor.MarketSalary("engineer", St.Era);
                            string oline = SimOwnership.OpSendOffer(St, targetId,
                                ContentDb.Int(d, "cash", mk), 0.0);
                            if (oline.Length > 0) outp.Add(oline + " — " + why);
                        }
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
                        // THE LANES the DM may set. The four acquisition channels
                        // are accepted if a move ever names one, but the narrator
                        // keeps speaking founder-language: "marketing" is the
                        // whole top of the funnel, and the ENGINE decides which
                        // channels that means — a narrator must never silently
                        // overwrite a curated mix.
                        string cat = ContentDb.Str(d, "cat", "marketing");
                        int amt = Gd.Clampi(ContentDb.Int(d, "v", 0), 0,
                                            SimEngine.EraSpendCap(St.Era));
                        if (!SetBudget(cat, amt)) { cat = "marketing"; SetBudget(cat, amt); }
                        if (cat == "marketing")
                            St.MarketingBudget = 0;   // one truth once the ledger takes over
                        outp.Add(string.Format("{0} budget set to ${1}/wk — {2}", cat, amt, why));
                        break;
                    }
                    case "push_lead":
                    {
                        // THE FOUNDER LEANS ON A DEAL. The engine clamps the heat
                        // swing before the pipeline ever sees it; the lane matches
                        // the named lead and writes the receipt. No live lead by
                        // that name is a real answer, not an error: the world says
                        // so and moves on.
                        int heat = Gd.Clampi(ContentDb.Int(d, "v", 0), -40, 40);
                        string leadNm = ContentDb.Str(d, "cat").Trim();
                        string pushed = SimPipeline.PushLead(St, leadNm, heat);
                        outp.Add(pushed.Length > 0
                            ? pushed
                            : string.Format("no live deal called '{0}' — the push found nobody", leadNm));
                        break;
                    }
                    case "open_site": outp.Add(SimDivisions.OpOpenSite(St, DmDict(d))); break;
                    case "close_site": outp.Add(SimDivisions.OpCloseSite(St, DmDict(d))); break;
                    case "reassign_employee": outp.Add(SimDivisions.OpReassignEmployee(St, DmDict(d))); break;
                    case "move_machine": outp.Add(SimDivisions.OpMoveMachine(St, DmDict(d))); break;
                    case "tag_offer": outp.Add(SimDivisions.OpTagOffer(St, DmDict(d))); break;
                    case "tag_spend_line": outp.Add(SimDivisions.OpTagSpendLine(St, DmDict(d))); break;
                    case "refinance_note": outp.Add(SimWorks.OpRefinanceNote(St, DmDict(d))); break;
                    case "fire_account": outp.Add(SimWorks.OpFireAccount(St, DmDict(d))); break;
                    case "retire_product": outp.Add(SimWorks.OpRetireProduct(St, DmDict(d))); break;
                    case "set_relief": outp.Add(SimWorks.OpSetRelief(St, DmDict(d))); break;
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
                // founder-language: the whole top of the funnel, split by the lane
                case "marketing": SimFunnel.SetMarketing(St, amt); return true;
                case "ads": St.Budgets.Ads = amt; return true;
                case "content": St.Budgets.Content = amt; return true;
                case "referrals": St.Budgets.Referrals = amt; return true;
                case "outbound": St.Budgets.Outbound = amt; return true;
                case "sales": St.Budgets.Sales = amt; return true;
                case "care": St.Budgets.Care = amt; return true;
                case "rnd": St.Budgets.Rnd = amt; return true;
                case "office": St.Budgets.Office = amt; return true;
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

            // THE PIVOT resolves at LOCK IN (DECISIONS § THE PIVOT; twin of
            // garage_view_screen._apply_lock — this seam was MISSING here: an
            // armed pivot never resolved in this engine). The armed flag dies
            // with the resolution; the receipt narrates through the outcome log.
            PivotReceipt pivotRes = SimPivot.ResolveArmed(St);
            if (pivotRes != null && pivotRes.Ok)
            {
                foreach (string pvl in pivotRes.Lines)
                    outcomeLog.Add("THE PIVOT: " + pvl);
                // the regeneration recipe (coordinator ruling): a nature-changing
                // pivot re-dresses the run — one GenerateWorld applied through
                // the BIRTH applier, then the birth illustrations + the three
                // identity images re-fire under the pivot-suffixed key, all
                // forced. Keyless runs skip cleanly; numbers never wait on art.
                var bootP = Boot.Instance;
                if (bootP != null && bootP.Generator != null
                    && bootP.Llm != null && bootP.Llm.Enabled)
                {
                    var scratch = new Runway.Llm.RunSnapshot
                    {
                        CompanyName = St.CompanyName ?? "",
                        CompanyIdea = St.CompanyIdea ?? "",
                        BizWhat = St.BizWhat ?? "Software",
                        BizWho = St.BizWho ?? "Consumer",
                    };
                    bootP.Generator.GenerateWorld(scratch, gen =>
                    {
                        if (gen == null || gen.Count == 0) return;
                        try
                        {
                            LlmWorld world = gen.ToObject<LlmWorld>();
                            if (!WorldGen.ApplyBirth(St, world)) return;
                            if (bootP.Director != null)
                            {
                                object growthObj = null;
                                if (St.Topics != null)
                                    St.Topics.TryGetValue("growth", out growthObj);
                                var growthJ = growthObj as Newtonsoft.Json.Linq.JObject;
                                if (growthJ == null && growthObj != null)
                                    growthJ = Newtonsoft.Json.Linq.JObject.FromObject(growthObj);
                                bootP.Director.MakeBirthIllustrations(
                                    St.SimSeed + "_p" + St.Pivots, growthJ,
                                    new Newtonsoft.Json.Linq.JObject
                                    {
                                        ["name"] = St.CompanyName ?? "",
                                        ["idea"] = St.CompanyIdea ?? "",
                                        ["what"] = St.BizWhat ?? "",
                                        ["who"] = St.BizWho ?? "",
                                    });
                            }
                            var pc = bootP.gameObject.GetComponent<Runway.Llm.PortraitClient>();
                            if (pc == null)
                                pc = bootP.gameObject.AddComponent<Runway.Llm.PortraitClient>();
                            string unitWord = "";
                            object worksObj;
                            if (St.Topics != null && St.Topics.TryGetValue("works", out worksObj))
                            {
                                var wd = worksObj as System.Collections.Generic.Dictionary<string, object>;
                                if (wd != null && wd.ContainsKey("unit_word"))
                                    unitWord = wd["unit_word"] as string ?? "";
                                var wj = worksObj as Newtonsoft.Json.Linq.JObject;
                                if (wj != null)
                                    unitWord = wj.Value<string>("unit_word") ?? "";
                            }
                            var co = new Newtonsoft.Json.Linq.JObject
                            {
                                ["idea"] = St.CompanyIdea ?? "",
                                ["what"] = St.BizWhat ?? "",
                                ["who"] = St.BizWho ?? "",
                                ["unit"] = unitWord,
                                ["force"] = true,
                            };
                            pc.Generate(new Newtonsoft.Json.Linq.JObject { ["force"] = true }, null);
                            pc.GenerateLogo(co, null);
                            pc.GenerateMake(co, null);
                            pc.GeneratePitch(co, null);
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning("PIVOT regen would not apply ("
                                + e.Message + ") — the run keeps its old dressing.");
                        }
                    });
                }
            }

            // THE WORLD ACTS FIRST (plan A1): the hostile weekly tick runs before the
            // founder's move lands, and its receipts open the week's ledger.
            WeeklyReport tick = SimEngine.WeeklyTick(St);
            var bootG = Boot.Instance;
            if (tick.ApplicantsNew > 0 && bootG != null && bootG.Generator != null)
            {
                // fire-and-forget: the cards are already playable; the reply
                // only swaps words. This side owns the Core types — the LLM
                // assembly is pure transport.
                var dressPayload = SimLabor.DressingPayload(St);
                if (dressPayload != null && dressPayload.Count > 0)
                    bootG.Generator.DressApplicants(dressPayload, res =>
                    {
                        if (res != null)
                            SimLabor.DressApplicants(St, res["candidates"] as Newtonsoft.Json.Linq.JArray);
                    });
            }
            if (SimRoadmap.LastRefreshed > 0 && bootG != null && bootG.Generator != null)
            {
                var betPayload = SimRoadmap.DressingPayload(St);
                if (betPayload != null && betPayload.Count > 0)
                    bootG.Generator.DressBets(betPayload, res =>
                    {
                        if (res != null)
                            SimRoadmap.DressBets(St, res["bets"] as Newtonsoft.Json.Linq.JArray);
                    });
            }
            if (bootG != null && bootG.Generator != null)
            {
                // the lead board is playable before the reply (05 §10)
                var leadPayload = SimPipeline.DressingPayload(St);
                if (leadPayload != null && leadPayload.Count > 0)
                    bootG.Generator.DressLeads(Newtonsoft.Json.Linq.JObject.FromObject(leadPayload), res =>
                    {
                        if (res != null)
                            SimPipeline.DressLeadsRows(St, res["leads"] as Newtonsoft.Json.Linq.JArray);
                    });
            }
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
