using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// THE 60-SECOND WEEK — garage_view_screen.gd's journal, drawing half.
    ///
    /// The book holds exactly TWO spreads:
    ///   0 · THE WEEK THAT WAS   the world's reply, the deltas, the crew. Read only.
    ///   1 · THE WEEK AHEAD      the situation, the big written move, and the only
    ///                           commit in the loop.
    ///
    /// Everything the five old pages asked for in widgets, the founder now simply
    /// WRITES, and the world adjudicates it. NO OPTIONS BEFORE THE PLAYER WRITES: the
    /// page states the situation and offers one clean, unmistakable writing area.
    ///
    /// This file decides WHAT is on each page. `WeekCommit` owns what happens when the
    /// week is locked; `JournalPage` owns the geometry, the type and the ruling.
    /// </summary>
    public sealed class JournalSpreads
    {
        readonly GarageScreen _g;
        readonly RectTransform _host;
        readonly WeekCommit _commit;
        readonly Dictionary<string, bool> _seenSpreads = new Dictionary<string, bool>();

        RectTransform _pageHost;
        JournalPage _jp;
        TextMeshProUGUI _telegraph;
        int _pageI;
        int _turnDir;

        public bool Adjudicating { get { return _commit.Adjudicating; } }

        /// The pre-roll card is on the page, so Esc has somewhere safe to go.
        public bool PrerollUp { get { return _commit.Preroll != null; } }

        public void PrerollEscape() { _commit.PrerollFix(); }

        GameState St { get { return _g.State; } }
        RunDriver Driver { get { return _g.Driver; } }

        public JournalSpreads(GarageScreen g, RectTransform host)
        {
            _g = g;
            _host = host;
            _commit = new WeekCommit(g, this);
        }

        public void ResetWeek()
        {
            _pageI = 0;
            _commit.Reset();
        }

        // ══ opening the book ═══════════════════════════════════════════════════

        public void Open()
        {
            for (int i = _host.childCount - 1; i >= 0; i--)
                Object.Destroy(_host.GetChild(i).gameObject);
            GameUi.Scrim(_host, new Color(0.06f, 0.05f, 0.05f, 0.30f), () =>
            {
                if (!Adjudicating) _g.CloseJournal();
            });
            _pageHost = DrawnUI.FullRect(_host, "pages");
            ShowSpread();
        }

        /// The commit calls this when the world asks its clarifying question — the page
        /// has to be re-laid around it.
        public void Redraw() { ShowSpread(); }

        void ShowSpread()
        {
            // THE TURN IS PHYSICAL. On an arrow press the outgoing page keeps its sheet,
            // rides ON TOP of the new one and slides away while the new sheet lands from
            // the side you are heading. Any other rebuild swaps instantly.
            JournalPage old = _jp;
            if (old != null && _turnDir != 0) old.ExitTurn(_turnDir);
            else
            {
                for (int i = _pageHost.childCount - 1; i >= 0; i--)
                    Object.Destroy(_pageHost.GetChild(i).gameObject);
            }

            Runway.Audio.Sfx.CardFlip();
            var pg = JournalPage.Create(_pageHost);
            _jp = pg;
            _commit.Attach(pg);
            if (old != null && _turnDir != 0) old.transform.SetAsLastSibling();

            // A PAGE YOU HAVE ALREADY READ IS DRY INK. Only a spread's first showing
            // performs the writing; turning back opens a written page, because a log
            // book that rewrote itself on every glance would be a screen, not a book.
            string key = St.Week + ":" + _pageI;
            pg.Instant = _seenSpreads.ContainsKey(key);
            pg.BackdropPath = _g.ComposedPath;
            bool firstOpen = !pg.Instant && _turnDir == 0;
            _seenSpreads[key] = true;
            pg.Build("WEEK " + St.Week);
            if (firstOpen) pg.EnterTurn(1);   // opening the book is a gesture too

            pg.PrevPage += () =>
            {
                if (_pageI == 0) { _g.CloseJournal(); return; }
                _pageI -= 1;
                _turnDir = -1;
                ShowSpread();
            };
            pg.NextPage += () =>
            {
                _pageI = Mathf.Min(_pageI + 1, 1);
                _turnDir = 1;
                ShowSpread();
            };
            pg.Written += t =>
            {
                _commit.Written = t;
                UpdateTelegraph(t);
                _commit.RefreshLock();
            };

            if (_pageI == 0) SpreadWas(); else SpreadAhead();
            pg.Arrows(_pageI > 0, _pageI < 1);
            if (_turnDir != 0) { pg.EnterTurn(_turnDir); _turnDir = 0; }
        }

        // ══ spread 0 — the week that was ═══════════════════════════════════════

        /// The owner, after playing: "we don't have actual text output for week N about
        /// the CONSEQUENCES of week N-1... no sense of progression". So the week OPENS
        /// with the story and only reaches the numbers underneath it. A bare status page
        /// is a defect.
        ///
        /// THE PAGE IS THE DIARY, NOT THE CHAPTER: the beat already read the narration
        /// aloud; the log prints the DM's journal_note — the founder's own scribble —
        /// and never replays the beat's text.
        ///
        /// THE PAGE IS BUDGETED BACKWARDS: the two strips get ~310px, and the narration
        /// is trimmed to whatever remains. The first cut of this page did none of that
        /// and the numbers fell off the paper curl.
        void SpreadWas()
        {
            JObject outcome = _g.LastOutcome ?? new JObject();
            var dm = outcome["dm"] as JObject ?? new JObject();
            List<object[]> lines = StoryLines(outcome, dm);
            bool hasCrew = CrewFaces().Count > 1;
            float stripsH = 150f + (hasCrew ? 130f : 0f);
            string note = ContentDb.Str(dm, "journal_note").Trim();
            string verdict = ContentDb.Str(outcome, "verdict");

            if (lines.Count == 0 && note.Length == 0)
            {
                _jp.Line(St.Week <= 1
                    ? "Week one. Nothing has happened to you yet. After this, everything that does is yours."
                    : "A quiet week. The rent noticed it anyway.");
            }
            else
            {
                string narr = "";
                var shorts = new List<object[]>();
                for (int i = 0; i < lines.Count; i++)
                {
                    if ((bool)lines[i][2] && narr.Length == 0) narr = (string)lines[i][0];
                    else shorts.Add(lines[i]);
                }
                float shortH = shorts.Count > 0 ? 102f : 0f;
                string headline = ContentDb.Str(dm, "headline");
                if (headline.Length > 0) _jp.Line(headline);
                if (note.Length > 0) _jp.LineFitted(note, stripsH + shortH + 24f);
                else if (narr.Length > 0)
                {
                    _jp.LineFitted(narr, stripsH + shortH + 24f);
                    if (verdict == "brilliant" || verdict == "backfired")
                        _jp.MarginMark(verdict == "brilliant" ? "star" : "cross");
                    // nat 20 / nat 1: the die itself gets its stamp
                    int used = ContentDb.Int(dm["dice"] as JObject, "used", 0);
                    if (used == 20)
                        _jp.Line("Rolled a natural 20. Some weeks the universe pays for lunch.");
                    else if (used == 1)
                        _jp.Line("Rolled a 1. Everything that could go sideways did.");
                }
                // ONE annotation, full ink. Faint text under a printed rule read as
                // struck-through; the margin mark already carries the judgement.
                if (shorts.Count > 0) _jp.LineFitted((string)shorts[0][0], stripsH);
            }
            DeltaStrip();
            CrewStrip();
        }

        /// THE RECEIPTS (owner: every impact with its reasoning) — each effect prints
        /// with its why: "+$1,200 — the pilot invoice cleared".
        List<object[]> StoryLines(JObject outcome, JObject dm)
        {
            var outp = new List<object[]>();
            string verdict = ContentDb.Str(outcome, "verdict").Trim();
            string narration = ContentDb.Str(outcome, "narration").Trim();
            string reality = ContentDb.Str(outcome, "reality").Trim();
            // said/heard live on the READING BEAT; the page is the diary and skips them
            if (verdict.Length > 0)
                outp.Add(new object[] { "The world called it " + verdict.ToLower() + ".", false, false });
            if (narration.Length > 0) outp.Add(new object[] { narration, false, true });
            if (reality.Length > 0) outp.Add(new object[] { reality, true, true });

            var effects = dm["effects"] as JArray;
            if (effects == null) return outp;
            foreach (JToken t in effects)
            {
                var d = t as JObject;
                string op = ContentDb.Str(d, "op");
                string why = ContentDb.Str(d, "why");
                if (why.Length == 0 || op == "set_flag") continue;
                int v = ContentDb.Int(d, "v", 0);
                string label = "";
                if (op == "product_delta") label = "product";
                else if (op == "traction_delta") label = "customers";
                else if (op == "morale_delta") label = "morale";
                else if (op == "hype_delta") label = "hype";
                string amt = (v >= 0 ? "+" : "−")
                    + (op == "cash_delta" ? "$" + GameUi.Money(Mathf.Abs(v)) : Mathf.Abs(v).ToString());
                outp.Add(new object[] {
                    string.Format("   {0} {1} — {2}", amt, label, why), false, true });
            }
            return outp;
        }

        /// The week's numbers as drawings, not sentences: the jar of runway, the build,
        /// the crowd. Values live in one-line captions; nothing is pressable.
        void DeltaStrip()
        {
            if (_jp.RoomToFence("ending") < 210f)
            {
                if (_jp.RoomToFence("ending") >= 60f)
                    _jp.Line(string.Format("${0} · v0.{1} · {2} customers",
                        GameUi.Money(St.Cash), St.Product, St.Traction), false, "ending");
                return;
            }
            int net = St.BurnPerWeek();
            int weeks = net <= 0 ? 999 : Gd.Maxi(Mathf.FloorToInt((float)St.Cash / net), 0);
            string runway = weeks < 999 ? weeks + " wks" : "gaining";
            string mood = St.Morale > 65 ? "fine" : (St.Morale > 35 ? "fraying" : "cooked");
            var chips = new List<RowItem>
            {
                RowItem.Of("cash", string.Format("${0}{1}\n{2}",
                    GameUi.Money(St.Cash), Chg("cash", St.Cash, true), runway),
                    Icon("cash", "itm_savings_jar")),
                RowItem.Of("prod", string.Format("v0.{0}{1}", St.Product, Chg("product", St.Product)),
                    Icon("product", "itm_laptop")),
                RowItem.Of("cust", string.Format("{0} customers{1}", St.Traction,
                    Chg("traction", St.Traction)), Icon("customers", "gv/chart_1")),
                RowItem.Of("mood", mood + Chg("morale", St.Morale),
                    Icon("morale", "itm_energy_drinks")),
            };
            _jp.IconRow(chips, new Vector2(124f, 116f), "ending");
        }

        /// The journal's OWN drawings first (doodles a founder would make); the big
        /// art's sprites only until those land.
        static string Icon(string doodle, string fallback)
        {
            string p = ArtCache.IconPath(doodle);
            return RunwayPaths.ArtExists(p) ? p : ArtCache.SpritePath(fallback);
        }

        /// " (+3)" / " (-2)" — what this week DID, next to what IS. Blank when unmoved,
        /// because a zero delta every week is wallpaper.
        string Chg(string key, int now, bool money = false)
        {
            int prev;
            if (!_g.WeekPrev.TryGetValue(key, out prev)) return "";
            int d = now - prev;
            if (d == 0) return "";
            if (money) return string.Format("  ({0}${1})", d > 0 ? "+" : "-", GameUi.Money(Mathf.Abs(d)));
            return string.Format("  ({0}{1})", d > 0 ? "+" : "", d);
        }

        /// Who is still here, at a glance: small faces, moods drawn on them, no input.
        void CrewStrip()
        {
            List<RowItem> faces = CrewFaces();
            if (faces.Count <= 1 || _jp.RoomToFence("ending") < 190f) return;
            if (faces.Count > 5) faces = faces.GetRange(0, 5);
            _jp.IconRow(faces, new Vector2(110f, 100f), "ending");
        }

        List<RowItem> CrewFaces()
        {
            var faces = new List<RowItem>();
            if (St == null) return faces;
            faces.Add(RowItem.Of("you", "you", Icon("you", "chr_arch_" + St.ArchetypeId)));
            for (int i = 0; i < St.Cofounders.Count; i++)
            {
                Cofounder cf = St.Cofounders[i];
                int loy = Driver != null ? Driver.Loyalty(i) : 70;
                string mood = loy > 70 ? "happy" : (loy > 30 ? "neutral" : "resentful");
                string roleL = (cf.Role ?? "?").ToLower();
                string doodle = roleL.Contains("sales") ? "cofd_sales"
                    : (roleL.Contains("business") ? "cofd_business"
                    : (roleL.Contains("idea") || roleL.Contains("hustler") ? "cofd_idea" : "cofd_tech"));
                // the doodle carries identity; the mood comes through the caption
                string cap = mood == "happy" ? roleL
                    : roleL + "\n(" + (mood == "neutral" ? "uneasy" : "resentful") + ")";
                faces.Add(RowItem.Of("cf" + i, cap, Icon(doodle, "cf_" + Slug(roleL) + "_" + mood)));
            }
            for (int i = 0; i < St.Employees.Count; i++)
            {
                string bs = GameState.BurnoutState(St.Employees[i].Burnout);
                string mood = bs == "cooked" || bs == "gone" ? "resentful"
                    : (bs == "frayed" ? "neutral" : "happy");
                faces.Add(RowItem.Of("emp" + i, (St.Employees[i].Name ?? "hire").ToLower(),
                    Icon("employee", "cf_technical_" + mood)));
            }
            return faces;
        }

        static string Slug(string roleL)
        {
            if (roleL.Contains("business") || roleL.Contains("sales")) return "business";
            if (roleL.Contains("design")) return "design";
            if (roleL.Contains("idea") || roleL.Contains("hustler")) return "idea";
            return "technical";
        }

        // ══ spread 1 — the week ahead ══════════════════════════════════════════

        void SpreadAhead()
        {
            // WHERE YOU STAND, one line — the ask is meaningless without it. Full ink on
            // purpose, and COMPACT on purpose: this line must never wrap.
            int net = St.BurnPerWeek();
            int weeks = net <= 0 ? 999 : Gd.Maxi(Mathf.FloorToInt((float)St.Cash / net), 0);
            string cashS = Mathf.Abs(St.Cash) >= 10000
                ? string.Format("${0:0}k", St.Cash / 1000f) : "$" + GameUi.Money(St.Cash);
            _jp.Line(string.Format("{0} · {1} · {2} cust · v0.{3}", cashS,
                weeks < 999 ? weeks + " wks" : "cash+", St.Traction, St.Product));

            // THE PRE-ROLL REVIEW OWNS THE WHOLE SHEET. The founder has already read
            // the week and written the move; what is left is one question, and the list
            // that asks it needs the paper the prose would have eaten. Squeezed under
            // the situation it printed its own headline and not one of the items — a
            // card that says something is wrong without saying what.
            if (_commit.Preroll != null) { PrerollCard(); return; }

            string situation = _g.CurrentEvent == null || _g.CurrentEvent.Count == 0
                ? "Nothing came for you this week. The week is yours."
                : ContentDb.Str(_g.CurrentEvent, "title") + " — " + ContentDb.Str(_g.CurrentEvent, "body");

            bool specialUsed = TermSheets() || MnaOffer() || LevelUp();

            // the field gets FOUR rules of reserved paper plus the ASK LINE — except on
            // a term-sheet week, where the cards ARE the question and the prose yields.
            // On those squeezed pages the ASK must survive whole: the title is the first
            // casualty (owner: "text is being too much cut").
            if (specialUsed)
            {
                string ask = ContentDb.Str(_g.CurrentEvent, "body").Trim();
                _jp.LineFitted(ask.Length > 0 ? ask : situation, _jp.RulePitch() * 2f + 72f);
            }
            else
            {
                _jp.LineFitted(situation, _jp.RulePitch() * 4f + 60f);
                _jp.Line("So — what do you do?");
            }

            if (Adjudicating)
            {
                _jp.Line("the world considers your move...", true, "ending");
                _commit.BuildLock();
                return;
            }
            if (_commit.Clarify != null) { ClarifyBlock(); return; }

            TMPro.TMP_InputField te = _jp.WriteField("", "ending");
            if (te != null)
            {
                var ph = te.placeholder as TextMeshProUGUI;
                if (ph != null) ph.text = "write what you actually do…";
                _jp.SetWritten(_commit.Written);
                te.onSubmit.AddListener(_ => _commit.CommitFromText());
            }
            BuildTelegraph();
            _commit.BuildLock();
        }

        /// THE TERM SHEETS (plan A2/UI-13): when a raise move opened the round, the
        /// three offers sit on the decision page as drawn cards — sign one by tapping,
        /// or write anything else and let them expire.
        bool TermSheets()
        {
            int frAge = St.Week - Gd.ToInt(St.GetMetaF("fundraising_week", St.Week));
            if (St.HasFlag("fundraising_open") && frAge > 2)
            {
                St.Flags.Remove("fundraising_open");
                _jp.Line("the term sheets expired unsigned.", true);
            }
            if (!St.HasFlag("fundraising_open")) return false;

            List<FundingOffer> offers = SimEngine.GenerateOffers(St, St.Investors);
            _jp.Line("THE TERM SHEETS ARE ON THE TABLE:");
            // why they are priced like this: the room was warm before you spoke
            double warm = SimEngine.WarmthPct(St);
            if (warm > 0.0)
                _jp.Line(string.Format("they knew your name: {0:0}% less equity asked.", warm), true);
            var cards = new List<RowItem>();
            for (int i = 0; i < offers.Count; i++)
            {
                string tag = (offers[i].Investor ?? "?").Split(' ')[0];
                if (tag.Length > 7) tag = tag.Substring(0, 7);
                cards.Add(RowItem.Of("ts:" + i, string.Format("{0} {1:0}k/{2:0}%",
                    tag, offers[i].Amount / 1000f, offers[i].EquityPct)));
            }
            _jp.IconRow(cards, new Vector2(230f, 40f), "body");
            List<FundingOffer> captured = offers;
            _jp.ChoiceMade += id =>
            {
                if (!id.StartsWith("ts:")) return;
                int idx;
                if (!int.TryParse(id.Substring(3), out idx) || idx >= captured.Count) return;
                FundingOffer o = captured[idx];
                SimEngine.ApplyRound(St, o.Amount, o.EquityPct);
                SimBoard.OnRoundClosed(St, o.Amount, o.EquityPct);
                St.Flags.Remove("fundraising_open");
                St.SetFlag(St.RoundsRaised.Count <= 2 ? "seed_raised" : "series_a");
                St.LogAction(string.Format("signed {0}: ${1} for {2:0.0}%",
                    o.Investor, o.Amount, o.EquityPct));
                Runway.Audio.Sfx.Win();
                _commit.RefreshLock();
            };
            return true;
        }

        /// THE OFFER ON THE TABLE (coordinator seam for lane 08): an M&A/board
        /// card block mirroring the term-sheet idiom — SimBoard owns content
        /// and consequences; the journal only draws and routes.
        bool MnaOffer()
        {
            var block = SimBoard.JournalOffer(St);
            if (block == null || block.Count == 0) return false;
            _jp.Line(Runway.Game.ContentDb.Str(block, "title", "AN OFFER IS ON THE TABLE:"));
            var mnaCards = new List<RowItem>();
            var arr = block["cards"] as Newtonsoft.Json.Linq.JArray;
            if (arr != null)
                foreach (var t in arr)
                {
                    var c = t as Newtonsoft.Json.Linq.JObject;
                    mnaCards.Add(RowItem.Of(Runway.Game.ContentDb.Str(c, "id"),
                                            Runway.Game.ContentDb.Str(c, "text")));
                }
            if (mnaCards.Count > 0)
            {
                _jp.IconRow(mnaCards, new Vector2(230f, 40f), "body");
                _jp.ChoiceMade += id =>
                {
                    string receipt = SimBoard.JournalPick(St, id);
                    if (string.IsNullOrEmpty(receipt)) return;
                    St.LogAction(receipt);
                    Runway.Audio.Sfx.Win();
                    _commit.RefreshLock();
                };
            }
            return true;
        }

        /// THE LEVEL-UP (plan B4): a banked milestone point is spent HERE, as a pen
        /// circle on the stat of your choice — the D&D moment, on paper.
        bool LevelUp()
        {
            if (St.Xp <= St.XpSpent) return false;
            _jp.Line("★ You leveled — circle the muscle that grew:");
            var items = new List<RowItem>();
            for (int i = 0; i < FounderDraftScreen.StatNames.Length; i++)
            {
                string s = FounderDraftScreen.StatNames[i];
                // "recruit 3" is the one caption that wraps inside a 110px cell; the
                // sheet says "hire", the id stays canonical
                items.Add(RowItem.Of("lv:" + s, string.Format("{0} {1}",
                    s == "recruit" ? "hire" : s, St.Competence(s))));
            }
            _jp.IconRow(items, new Vector2(110f, 42f), "body");
            _jp.ChoiceMade += id =>
            {
                if (!id.StartsWith("lv:") || St.Xp <= St.XpSpent) return;
                string s2 = id.Substring(3);
                St.Competences[s2] = Gd.Mini(St.Competence(s2) + 1, 5);
                St.XpSpent += 1;
                St.LogAction(string.Format("leveled {0} to {1}", s2, St.Competence(s2)));
                Runway.Audio.Sfx.Win();
            };
            return true;
        }

        /// THE PRE-ROLL REVIEW CARD (docs/design/DECISIONS.md #2). The world's last word
        /// before the dice: every outstanding item, named in the business term it
        /// belongs to, with the desk that owns it. Two exits and no third — go fix it
        /// (the binder opens on the loudest one) or roll anyway. The engine decides what
        /// counts as outstanding (SimEngine.PrerollItems); this page only reads it, so
        /// every roll site in the game can show the same card.
        public void PrerollCard()
        {
            var rows = _commit.Preroll["items"] as JArray ?? new JArray();
            _jp.Line("before the die rolls:");
            int shown = 0;
            foreach (JToken row in rows)
            {
                // EVERY LINE HERE STEALS PAPER FROM THE TWO EXITS, and a card whose way
                // out fell off the sheet is a dead end: the chips get their room first.
                if (shown >= 4 || _jp.RoomToFence("body") < _jp.RulePitch() + 40f) break;
                var it = row as JObject;
                // the bang carries the alarm, the WORDS carry the meaning — read this
                // page in grey and it still says which desk and what is wrong
                _jp.Line(string.Format("{0}{1} — {2}",
                    ContentDb.Int(it, "severity", 2) >= 3 ? "! " : "",
                    ContentDb.Str(it, "desk"), ContentDb.Str(it, "label")));
                shown += 1;
            }
            if (shown < rows.Count)
                _jp.Line(string.Format("…and {0} more, on the threats page.", rows.Count - shown),
                         true);
            _jp.Line("fix them, or roll and live with it.", false, "ending");
            _jp.IconRow(new List<RowItem> {
                RowItem.Of("pre:fix", "go fix it"),
                RowItem.Of("pre:roll", "roll anyway"),
            }, new Vector2(240f, 42f), "ending");
            _jp.ChoiceMade += id =>
            {
                if (id == "pre:fix") _commit.PrerollFix();
                else if (id == "pre:roll") _commit.PrerollRoll();
            };
        }

        /// THE WORLD ASKS FIRST: the question in coral, then chips (amounts or prices)
        /// or a plain answer line. The world is waiting on the ANSWER, so there is no
        /// lock row underneath to overlap the field.
        void ClarifyBlock()
        {
            JObject clarify = _commit.Clarify;
            _jp.Line(ContentDb.Str(clarify, "q"), false, "ending");
            string kind = ContentDb.Str(clarify, "kind");
            if (kind == "amount")
            {
                int cap = SimEngine.EraSpendCap(St.Era);
                var opts = new List<RowItem>();
                int[] steps = { cap / 24, cap / 6, cap / 2 };
                for (int i = 0; i < steps.Length; i++)
                {
                    int a = Mathf.RoundToInt(steps[i] / 50f) * 50;
                    opts.Add(RowItem.Of("clr:" + a, "$" + GameUi.Money(a)));
                }
                _jp.IconRow(opts, new Vector2(130f, 42f), "ending");
                _jp.ChoiceMade += id =>
                {
                    if (!id.StartsWith("clr:")) return;
                    int amt;
                    if (int.TryParse(id.Substring(4), out amt))
                        _commit.AnswerClarify("budget: $" + GameUi.Money(amt));
                };
            }
            else if (kind == "price")
            {
                // the chips ladder around the FIRST unpriced offer's street price; the
                // tap writes the price into the ENGINE, then the move re-commits
                int targetI = -1;
                for (int i = 0; i < St.Offers.Count; i++)
                    if (St.Offers[i].Price <= 0.0) { targetI = i; break; }
                if (targetI >= 0)
                {
                    Offer target = St.Offers[targetI];
                    double fair = target.FairPrice;
                    var opts = new List<RowItem>();
                    float[] mults = { 0.7f, 1.0f, 1.4f };
                    for (int i = 0; i < mults.Length; i++)
                    {
                        int pv = Gd.Maxi(Mathf.RoundToInt((float)fair * mults[i]), 1);
                        opts.Add(RowItem.Of("prc:" + pv, "$" + GameUi.Money(pv)));
                    }
                    _jp.IconRow(opts, new Vector2(130f, 42f), "ending");
                    string tName = target.Name ?? "the offer";
                    Offer captured = target;
                    _jp.ChoiceMade += id =>
                    {
                        if (!id.StartsWith("prc:")) return;
                        int pv2;
                        if (!int.TryParse(id.Substring(4), out pv2)) return;
                        captured.Price = pv2;
                        St.LogAction(string.Format("priced {0} at ${1}", tName, pv2));
                        _commit.AnswerClarify(string.Format("we price {0} at ${1}", tName, pv2));
                    };
                }
            }
            TMPro.TMP_InputField ce = _jp.WriteField("", "ending");
            if (ce == null) return;
            var cph = ce.placeholder as TextMeshProUGUI;
            if (cph != null)
                cph.text = kind == "amount" ? "type an amount, or tap one…" : "answer, then roll…";
            ce.onSubmit.AddListener(t =>
            {
                if (t.Trim().Length > 0) _commit.AnswerClarify(t.Trim());
            });
        }

        // ══ the telegraph ══════════════════════════════════════════════════════

        void BuildTelegraph()
        {
            float y = _jp.WritableBottom() - 2f * _jp.RulePitch() + 6f;
            Vector2 sp = _jp.SpanAt(y);
            _telegraph = DrawnUI.HandLabel(_jp.Space, "", sp.x + 4f, y, 22f,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), sp.y - sp.x, TextAlignmentOptions.TopLeft);
            UpdateTelegraph(_commit.Written);
        }

        /// THE TELEGRAPH (research: the decision-matrix pattern): as the founder writes,
        /// a faint margin note says how the move READS — the governing stat, the
        /// modifier, and any advantage their loadout grants. Never the odds, never the
        /// DC: proof the sheet matters, mystery intact.
        void UpdateTelegraph(string t)
        {
            if (_telegraph == null) return;
            string low = (t ?? "").ToLower();
            if (low.Trim().Length < 8) { _telegraph.text = ""; return; }
            string best = "";
            int bestHits = 0;
            foreach (var kv in WeekCommit.StatSniff)
            {
                int hits = 0;
                for (int i = 0; i < kv.Value.Length; i++) if (low.Contains(kv.Value[i])) hits++;
                if (hits > bestHits) { bestHits = hits; best = kv.Key; }
            }
            if (best.Length == 0) { _telegraph.text = ""; return; }
            int mod = St.Competence(best) - 3;
            RollContext cx = SimEngine.RollContext(St, best);
            string badge = "";
            if (cx.Advantage) badge = "  ·  advantage (" + string.Join(", ", cx.AdvReasons.ToArray()) + ")";
            else if (cx.Disadvantage) badge = "  ·  disadvantage (" + string.Join(", ", cx.DisReasons.ToArray()) + ")";
            // THE WHOLE FORMULA, VISIBLE AT THE PEN (owner: "a better view of how
            // build/sell/raise is taken into account").
            _telegraph.text = string.Format(
                "reads as {0}  ·  your {1} {2} → d20 {3}{4}  ·  world sets DC: routine 6-8 · "
                + "solid 9-11 · bold 12-14 · wild 15-16{5}",
                best.ToUpper(), best, St.Competence(best),
                mod >= 0 ? "+" : "−", Mathf.Abs(mod), badge);
        }
    }
}
