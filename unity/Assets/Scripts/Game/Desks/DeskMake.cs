using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — THE COMPANY · "what we make" (twin of desk_make.gd). W2: L-MAKE.
    /// THE QUESTION THIS DESK ANSWERS: "what are we making, and how solid is it?"
    ///
    /// THE KANBAN WALL (DECISIONS style 5; mockups 16 + 17): the hero plate,
    /// the four-column pipeline wall (SHELF commit arms -> NEXT queue ->
    /// BUILDING with stand-down −25% -> READY, dice at the press behind the
    /// pre-roll review), the LIVE band grouped by job with families and
    /// attention-first folds, rung 3's LINEUP + SHARED PLUMBING, and the cost
    /// footer matching SimFeatures' own numbers. Solidity wears the kit's
    /// marks: solid calm, creaky sev 2, breaking sev 3 — red means act.
    /// </summary>
    public static class DeskMake
    {
        public const string Question = "what are we making, and how solid is it?";

        const float ColW = 272f;
        const float ColH = 404f;
        const float ColY = 112f;
        const float LiveY = 524f;
        const float FootY = 818f;
        const float RulesY = 848f;
        static readonly string[] JobOrder = { "pull", "keep", "charge", "plumbing" };
        static readonly Dictionary<string, string> JobLabel = new Dictionary<string, string>
        {
            { "pull", "BRINGS THEM IN" }, { "keep", "KEEPS THEM" },
            { "charge", "LETS US CHARGE" }, { "plumbing", "THE PLUMBING" },
        };
        const int Rung2Live = 13;   // DECISIONS: rung 1 holds "≤ ~12 live"
        const int FreshWks = 4;

        /// A committed bet that has finished is READY, not BUILDING — the two
        /// counts and the two columns never show the same bet twice.
        static List<Bet> BuildingBets(GameState s)
        {
            var outp = new List<Bet>();
            List<Bet> committed = SimRoadmap.CommittedBets(s);
            for (int i = 0; i < committed.Count; i++)
                if (!committed[i].Ready) outp.Add(committed[i]);
            return outp;
        }

        /// The group overview's card IS this page's hero (the quartet law).
        public static string[] HeroSummary(GameState s)
        {
            int creaks = SimFeatures.CreakCount(s);
            string line = string.Format(CultureInfo.InvariantCulture,
                "{0} live · {1} building · {2} ready", s.Features.Count,
                BuildingBets(s).Count, SimRoadmap.ReadyBets(s).Count);
            if (creaks > 0)
                line += string.Format(CultureInfo.InvariantCulture, " · {0} creak{1}",
                    creaks, creaks == 1 ? "" : "s");
            return new[]
            {
                "v0." + Math.Max(1, s.Product / 10).ToString(CultureInfo.InvariantCulture),
                line,
            };
        }

        public static void Draw(BinderScreen b)
        {
            GameState st = b.State;
            SimRoadmap.EnsureBoard(st);
            SimFeatures.SeedDefaults(st);
            switch (Mode(b))
            {
                case "preroll": PrerollCard(b); return;
                case "shipped": ShipCard(b); return;
                case "family": FamilyPage(b); return;
                case "job": JobPage(b); return;
                case "product": ProductPage(b); return;
            }
            if (SimFeatures.ProductIds(st).Count > 0)
            {
                Lineup(b, st);
                return;
            }
            Hero(b, st);
            Wall(b, st);
            LiveBand(b, st);
            CostFoot(b, st);
        }

        // ═══════════════════════════ THE HERO ═══════════════════════════════

        static void Hero(BinderScreen b, GameState st)
        {
            string name = Topic(st, "make_name", st.CompanyName);
            string line = Topic(st, "make_line", st.CompanyIdea ?? "");
            if (line.Length > 60) line = line.Substring(0, 60);
            if (line == "") line = "the thing we make";
            string version = "v0." + Math.Max(1, st.Product / 10)
                .ToString(CultureInfo.InvariantCulture);
            // the plate's name lane is one line — a 24-char company name trims
            // with an ellipsis, never wrapping under the version chip
            string plateName = name.Length > 17 ? name.Substring(0, 16).TrimEnd() + "…" : name;
            DeskKit.HeroPlate(b, 10f, 6f, plateName, version, "what we make");
            // THE MAKE illustration — BESIDE the plate (never over its title);
            // the plate alone is the fallback.
            try
            {
                string mp = Runway.Llm.PortraitClient.MakePath;
                if (System.IO.File.Exists(mp))
                {
                    byte[] mb = System.IO.File.ReadAllBytes(mp);
                    var mt = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (mt.LoadImage(mb))
                    {
                        // hosted under a DrawnUI.Rect (the proven placement
                        // idiom — a bare RawImage under Content drifted to the
                        // layout's mercy; owner screenshot showed it floating)
                        var host = DrawnUI.Rect(b.Content, "make_illus", 434f, 4f, 84f, 84f);
                        var mi = new GameObject("img", typeof(RectTransform),
                            typeof(UnityEngine.UI.RawImage)).GetComponent<UnityEngine.UI.RawImage>();
                        mi.texture = mt;
                        mi.rectTransform.SetParent(host, false);
                        DrawnUI.SetTopLeft(mi.rectTransform, 0f, 0f);
                        mi.rectTransform.sizeDelta = new Vector2(84f, 84f);
                        mi.raycastTarget = false;
                    }
                }
            }
            catch { }
            b.L(line, 536f, 8f, 32f, DrawnUI.Ink, 574f);
            // a bet that is READY is no longer BUILDING — each shows once
            string counts = string.Format(CultureInfo.InvariantCulture,
                "{0} live · {1} building · {2} ready · {3} on the shelf",
                st.Features.Count, BuildingBets(st).Count,
                SimRoadmap.ReadyBets(st).Count, ShelfRows(st).Count);
            b.L(counts, 536f, 52f, 21f, Ink(0.6f), 574f);
            int creaks = SimFeatures.CreakCount(st);
            if (creaks > 0)
            {
                int tax = SimFeatures.CreakTaxPct(st);
                string cl = string.Format(CultureInfo.InvariantCulture,
                    "creaks at '{0}'", SimFeatures.WorstCreakName(st));
                if (tax > 0)
                    cl += string.Format(CultureInfo.InvariantCulture,
                        " — build speed −{0}%", tax);
                b.L(cl, 536f, 78f, 21f, DrawnUI.Coral, 420f);
            }
            List<Bet> ready = SimRoadmap.ReadyBets(st);
            if (ready.Count > 0)
            {
                int left = SimRoadmap.StallLeft(st, ready[0]);
                DeskKit.ClockChip(b, 956f, 78f, left > 0
                    ? string.Format(CultureInfo.InvariantCulture, "self-ships in {0} wk", left)
                    : "self-ships this week");
            }
            // (the wall already teaches the dice and the stand-down burn —
            // READY says "ship it, or it ships itself", BUILDING owns the
            // stand-down arm — so the hero keeps no duplicate note)
        }

        // ═══════════════════════ THE PIPELINE WALL ═══════════════════════════

        sealed class ShelfRow
        {
            public string Id = "";
            public bool Board;
            public string Name = "";
            public int CostUsd;
            public int Weeks;
            public int OddsPct;
            public string JobWords = "";
        }

        static void Wall(BinderScreen b, GameState st)
        {
            List<ShelfRow> shelf = ShelfRows(st);
            List<Bet> queued = SimFeatures.QueuedBets(st);
            List<Bet> building = BuildingBets(st);
            List<Bet> ready = SimRoadmap.ReadyBets(st);
            // ── THE SHELF: priced ideas, press one to commit it
            DeskKit.WallCol c1 = DeskKit.WallColumn(b, 10f, ColY, ColW, ColH,
                "THE SHELF", "priced ideas — take one on");
            // the column never overflows its box: three exact, or two + the count
            int shelfCap = shelf.Count <= 3 ? 3 : 2;
            int shown = 0;
            foreach (ShelfRow row in shelf)
            {
                if (shown >= shelfCap) break;
                DeskKit.WallCard(b, c1, new DeskKit.WallCardCfg
                {
                    Title = row.Name,
                    Facts = new List<string>
                    {
                        string.Format(CultureInfo.InvariantCulture,
                            "${0} · {1} wk · {2}% · {3}", row.CostUsd, row.Weeks,
                            row.OddsPct, row.JobWords),
                    },
                });
                ShelfArm(b, c1, st, row);
                shown++;
            }
            if (shelf.Count > shown)
            {
                DeskKit.More(b, c1.ContentX, c1.Cursor, shelf.Count - shown, "ideas wait");
                c1.Cursor += 30f;
            }
            b.L("write your own in THIS WEEK — the world prices it",
                c1.ContentX, c1.Cursor + 2f, 15f, Ink(0.45f), ColW - 20f);
            // ── NEXT: the committed queue, reorder freely
            DeskKit.WallCol c2 = DeskKit.WallColumn(b, 292f, ColY, ColW, ColH,
                "NEXT", "the queue — reorder freely");
            if (queued.Count == 0)
                b.L("nothing waits — the shelf commits straight to the team",
                    c2.ContentX, c2.Cursor + 4f, 15f, Ink(0.45f), ColW - 20f);
            int qn = 0;
            foreach (Bet qbet in queued)
            {
                if (qn >= 3) break;
                string starts = qn == 0 ? "when a slot frees"
                    : string.Format(CultureInfo.InvariantCulture, "after '{0}'",
                        Clip(queued[qn - 1].Name ?? "", 14));
                DeskKit.WallCard(b, c2, new DeskKit.WallCardCfg
                {
                    Title = qbet.Name,
                    Facts = new List<string> { "starts · " + starts },
                });
                QueueWords(b, c2, st, qbet.Id, qn, queued.Count);
                qn++;
            }
            if (queued.Count > qn)
                DeskKit.More(b, c2.ContentX, c2.Cursor, queued.Count - qn, "queued behind");
            // ── BUILDING: money burning against odds
            string head3 = building.Count <= 1 ? "BUILDING"
                : string.Format(CultureInfo.InvariantCulture, "BUILDING — {0}", building.Count);
            DeskKit.WallCol c3 = DeskKit.WallColumn(b, 574f, ColY, ColW, ColH,
                head3, "money burning against odds");
            int rndWk = SimFeatures.BuildTotal(st);
            // three compact bars fit; the rebuild note only rides when there is room
            int buildCap = building.Count <= 3 ? 3 : 2;
            bool rebuildFact = building.Count <= 2;
            int bn = 0;
            foreach (Bet bet in building)
            {
                if (bn >= buildCap) break;
                var facts = new List<string>
                {
                    string.Format(CultureInfo.InvariantCulture,
                        "{0}% built · odds {1}% · ${2}/wk", SimRoadmap.ProgressPct(bet),
                        SimRoadmap.ShipOddsPct(st, bet),
                        rndWk / Math.Max(building.Count, 1)),
                };
                if (rebuildFact && bet.Kind == "debt" && SimFeatures.CreakCount(st) > 0)
                    facts.Add("kills the creak when it lands");
                DeskKit.WallCard(b, c3, new DeskKit.WallCardCfg
                {
                    Title = bet.Name,
                    Facts = facts,
                    Progress = Mathf.Clamp01((float)(bet.Progress
                        / Math.Max(bet.CostRndWeeks, 0.001))),
                });
                StandDownArm(b, c3, st, bet);
                bn++;
            }
            if (building.Count > bn)
                DeskKit.More(b, c3.ContentX, c3.Cursor, building.Count - bn, "building behind");
            if (building.Count == 0)
                b.L("nothing committed — the rnd money polishes base quality",
                    c3.ContentX, c3.Cursor + 4f, 15f, Ink(0.45f), ColW - 20f);
            // ── READY: ship it, or it ships itself
            DeskKit.WallCol c4 = DeskKit.WallColumn(b, 856f, ColY, ColW, ColH,
                "READY", "ship it, or it ships itself");
            int rn = 0;
            foreach (Bet rbet in ready)
            {
                if (rn >= 2) break;
                int left = SimRoadmap.StallLeft(st, rbet);
                DeskKit.WallCard(b, c4, new DeskKit.WallCardCfg
                {
                    Title = rbet.Name,
                    Ready = true,
                    Facts = new List<string>
                    {
                        "promises · " + BetJobWords(rbet),
                        left > 0 ? string.Format(CultureInfo.InvariantCulture,
                            "slips out in {0} wk", left) : "slips out this week",
                    },
                });
                ShipButton(b, c4, st, rbet);
                rn++;
            }
            if (ready.Count == 0)
                b.L("nothing built yet — a finished bet waits here for the dice",
                    c4.ContentX, c4.Cursor + 4f, 15f, Ink(0.45f), ColW - 20f);
        }

        /// THE SHELF ROWS: the roadmap's own candidates first, then the lane's.
        static List<ShelfRow> ShelfRows(GameState st)
        {
            var outp = new List<ShelfRow>();
            foreach (Bet bd in SimRoadmap.BoardBets(st))
            {
                if (bd.Committed || bd.Ready || bd.CommittedWeek < 0 || bd.Progress > 0.0)
                    continue;
                outp.Add(new ShelfRow
                {
                    Id = bd.Id, Board = true, Name = bd.Name,
                    CostUsd = (int)(bd.CostRndWeeks * SimRoadmap.RND_PER_WEEK),
                    Weeks = (int)Math.Ceiling(bd.CostRndWeeks),
                    OddsPct = SimRoadmap.ShipOddsPct(st, bd),
                    JobWords = BetJobWords(bd),
                });
            }
            foreach (SimFeatures.ShelfCandidate cand in SimFeatures.ShelfCandidates(st))
                outp.Add(new ShelfRow
                {
                    Id = cand.Id, Board = false, Name = cand.Name,
                    CostUsd = cand.CostUsd, Weeks = cand.Weeks,
                    OddsPct = cand.OddsPct, JobWords = cand.JobWords,
                });
            return outp;
        }

        static string BetJobWords(Bet bet)
        {
            if (bet.Kind == "debt") return "kills a creak";
            string job;
            if (!SimFeatures.KIND_TO_JOB.TryGetValue(bet.Kind ?? "", out job))
                job = "plumbing";
            string words;
            return SimFeatures.JOB_WORDS.TryGetValue(job, out words) ? words : "plumbing";
        }

        /// The commit arm under a shelf card — the mutation law's two-tap.
        static void ShelfArm(BinderScreen b, DeskKit.WallCol col, GameState st, ShelfRow row)
        {
            string id = row.Id;
            bool full = SimRoadmap.CommittedBets(st).Count >= SimRoadmap.WipCap(st);
            string plain = full ? "queue it ->" : "take it on ->";
            string armed = string.Format(CultureInfo.InvariantCulture,
                "sure? ${0} · {1} wk", row.CostUsd, row.Weeks);
            bool isBoard = row.Board;
            DeskKit.Arm(b, "take:" + id, plain, armed, col.ContentX, col.Cursor - 8f, () =>
            {
                if (isBoard)
                {
                    if (SimRoadmap.CommittedBets(st).Count < SimRoadmap.WipCap(st))
                        SimRoadmap.CommitBet(st, id);
                    else
                        SimFeatures.EnqueueBet(st, id);
                }
                else
                {
                    SimFeatures.CommitShelf(st, id);
                }
            }, 250f, 18f);
            col.Cursor += 26f;
        }

        /// sooner · later · drop — the queue's own words under its card.
        static void QueueWords(BinderScreen b, DeskKit.WallCol col, GameState st,
                               string id, int pos, int count)
        {
            float x = col.ContentX;
            float y = col.Cursor - 8f;
            if (pos > 0)
                DeskKit.Word(b, "sooner", x, y, () => SimFeatures.QueueMove(st, id, -1),
                    17f, Ink(0.7f), 70f);
            if (pos < count - 1)
                DeskKit.Word(b, "later", x + 78f, y, () => SimFeatures.QueueMove(st, id, 1),
                    17f, Ink(0.7f), 62f);
            DeskKit.Word(b, "drop", x + 148f, y, () => SimFeatures.DequeueBet(st, id),
                17f, Ink(0.7f), 60f);
            col.Cursor = y + 34f;
        }

        /// Standing down is priced (−25% of the build) — the arm quotes it.
        static void StandDownArm(BinderScreen b, DeskKit.WallCol col, GameState st, Bet bet)
        {
            string id = bet.Id;
            DeskKit.Arm(b, "down:" + id, "stand down", "sure? 25% of the build is lost",
                col.ContentX, col.Cursor - 14f,
                () => SimRoadmap.UncommitBet(st, id), 250f, 18f);
            col.Cursor += 18f;
        }

        /// SHIP: the dice roll AT the press, behind the pre-roll review.
        static void ShipButton(BinderScreen b, DeskKit.WallCol col, GameState st, Bet bet)
        {
            string id = bet.Id;
            float x = col.ContentX;
            float y = col.Cursor - 8f;
            Button btn = null;
            btn = DeskKit.Word(b, "SHIP ->", x, y, () =>
            {
                b.Desk.Remove("armed");
                if (PrerollRows(st).Count > 0)
                {
                    b.Desk["mode"] = "preroll";
                    b.Desk["bet"] = id;
                    b.Refresh();
                    return;
                }
                DeskKit.SignStroke(b, btn, "SHIP ->", x, y, () => Fire(b, id));
            }, 22f, DrawnUI.Ink, 160f, false);
            col.Cursor = y + 36f;
        }

        // ═══════════════════ LIVE — THE INVENTORY BAND ═══════════════════════

        static void LiveBand(BinderScreen b, GameState st)
        {
            bool rung2 = st.Features.Count >= Rung2Live;
            string head = rung2
                ? string.Format(CultureInfo.InvariantCulture,
                    "LIVE — {0} FEATURES · attention face-up, the healthy fold",
                    st.Features.Count)
                : "LIVE — WHAT IT'S MADE OF TODAY";
            b.L(head, 10f, LiveY, 22f, DrawnUI.Ink, 1100f);
            DeskKit.PenRule(b, LiveY + 30f);
            float y = LiveY + 44f;
            foreach (string job in JobOrder)
            {
                List<Feature> members = JobMembers(st, job, "");
                if (members.Count == 0) continue;
                y = JobRow(b, st, job, members, y, rung2);
            }
        }

        sealed class FamSlot
        {
            public string Family = "";
            public List<Feature> Members = new List<Feature>();
        }

        /// One job group: label column, up to three cards — families and
        /// attention first — then the honest fold in the label's own column.
        static float JobRow(BinderScreen b, GameState st, string job,
                            List<Feature> members, float y, bool rung2)
        {
            members = AttentionFirst(members, st);
            string lbl;
            if (!JobLabel.TryGetValue(job, out lbl)) lbl = job;
            b.L(lbl.ToUpper(), 10f, y + 8f, 15f, Ink(0.55f), 148f);
            var featSlots = new List<Feature>();
            var famSlots = new List<FamSlot>();
            int foldedN = 0;
            int foldedKeep = 0;
            if (!rung2)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    if (i < 3) featSlots.Add(members[i]);
                    else { foldedN++; foldedKeep += members[i].KeepWk; }
                }
            }
            else
            {
                Dictionary<string, List<Feature>> fams = FamiliesOf(members);
                foreach (var kv in fams)
                {
                    if (famSlots.Count + featSlots.Count >= 3 || kv.Key == "") continue;
                    famSlots.Add(new FamSlot { Family = kv.Key, Members = kv.Value });
                }
                foreach (Feature md in members)
                {
                    if ((md.Family ?? "") != "") continue;
                    bool hot = md.Solidity != "solid"
                        || (md.BornWk > 0 && st.Week - md.BornWk <= FreshWks);
                    if (hot && famSlots.Count + featSlots.Count < 3)
                        featSlots.Add(md);
                    else { foldedN++; foldedKeep += md.KeepWk; }
                }
            }
            float x = 160f;
            foreach (FamSlot fs in famSlots)
            {
                FamilyCard(b, st, x, y, fs);
                x += 320f;
            }
            foreach (Feature fd in featSlots)
            {
                FeatureCard(b, st, x, y, fd);
                x += 320f;
            }
            if (foldedN > 0)
            {
                string jobCopy = job;
                DeskKit.Word(b, string.Format(CultureInfo.InvariantCulture,
                    "the other {0} — ${1}/wk", foldedN, foldedKeep), 10f, y + 28f, () =>
                {
                    b.Desk["mode"] = "job";
                    b.Desk["job"] = jobCopy;
                }, 15f, Ink(0.5f), 148f);
            }
            return y + 64f;
        }

        /// One live feature card: solidity mark, name, keep right.
        static void FeatureCard(BinderScreen b, GameState st, float x, float y, Feature fd)
        {
            DeskKit.CardBox frame = DeskKit.CardFrame(b, x, y, 306f, 56f, "");
            float cx = frame.ContentX;
            int sev = SoliditySev(fd.Solidity);
            if (sev > 0) DeskKit.SevDot(b, cx - 6f, y + 12f, sev);
            var nmL = b.L(fd.Name, cx + (sev > 0 ? 24f : 0f), y + 8f, 20f, DrawnUI.Ink, 190f);
            // one line, clipped — a generated name must not escape the card
            nmL.textWrappingMode = TextWrappingModes.NoWrap;
            nmL.overflowMode = TextOverflowModes.Ellipsis;
            var v = b.L(string.Format(CultureInfo.InvariantCulture, "${0}/wk", fd.KeepWk),
                x + 306f - 110f, y + 8f, 18f, Ink(0.6f), 96f);
            v.alignment = TMPro.TextAlignmentOptions.TopRight;
            string note = FeatureNote(st, fd);
            if (note != "")
                b.L(note, cx, y + 32f, 14f, sev > 0 ? DrawnUI.Coral : Ink(0.5f), 270f);
        }

        /// The family card: worst-member mark, ×N, summed keep; opens members.
        static void FamilyCard(BinderScreen b, GameState st, float x, float y, FamSlot fs)
        {
            int keep = 0;
            int worst = 0;
            string creakyName = "";
            foreach (Feature md in fs.Members)
            {
                keep += md.KeepWk;
                int s = SoliditySev(md.Solidity);
                if (s > worst) { worst = s; creakyName = md.Name; }
            }
            DeskKit.CardBox frame = DeskKit.CardFrame(b, x, y, 306f, 56f, "");
            float cx = frame.ContentX;
            if (worst > 0) DeskKit.SevDot(b, cx - 6f, y + 12f, worst);
            b.L(string.Format(CultureInfo.InvariantCulture, "{0} ×{1}", fs.Family,
                fs.Members.Count), cx + (worst > 0 ? 24f : 0f), y + 8f, 20f,
                DrawnUI.Ink, 190f);
            var v = b.L(string.Format(CultureInfo.InvariantCulture, "${0}/wk", keep),
                x + 306f - 110f, y + 8f, 18f, Ink(0.6f), 96f);
            v.alignment = TMPro.TextAlignmentOptions.TopRight;
            if (worst > 0 && creakyName != "")
                b.L("1 creaky member — " + creakyName, cx, y + 32f, 14f, DrawnUI.Coral, 270f);
            string famCopy = fs.Family;
            Button hit = DeskKit.Word(b, "", x, y, () =>
            {
                b.Desk["mode"] = "family";
                b.Desk["family"] = famCopy;
            }, 14f, DrawnUI.Ink, 306f);
            hit.GetComponent<RectTransform>().sizeDelta = new Vector2(306f, 56f);
        }

        /// What a card whispers under its name: the creak, or the verdict.
        static string FeatureNote(GameState st, Feature fd)
        {
            if (fd.Solidity == "breaking") return "BREAKING — rebuild it";
            if (fd.Solidity == "creaky") return "creaky — rebuild candidate";
            if (fd.Measured > 0.0 && st.Week - fd.BornWk <= FreshWks * 2)
            {
                int promised = SimFeatures.PromisedUnits(st, fd);
                if (promised > 0)
                    return string.Format(CultureInfo.InvariantCulture,
                        "promised +{0}, measured +{1:0.0}", promised, fd.Measured);
                return string.Format(CultureInfo.InvariantCulture,
                    "measured +{0:0.0}", fd.Measured);
            }
            return "";
        }

        static int SoliditySev(string solidity)
        {
            if (solidity == "breaking") return 3;
            if (solidity == "creaky") return 2;
            return 0;
        }

        /// Creaks and fresh landings sort to the front: a cap can only ever
        /// fold the healthy (the collapse law's face-up half).
        static List<Feature> AttentionFirst(List<Feature> members, GameState st)
        {
            var scored = new List<KeyValuePair<int, Feature>>();
            for (int i = 0; i < members.Count; i++)
            {
                Feature md = members[i];
                int score = 0;
                if (md.Solidity == "breaking") score = 30;
                else if (md.Solidity == "creaky") score = 20;
                if (md.BornWk > 0 && st.Week - md.BornWk <= FreshWks) score += 10;
                scored.Add(new KeyValuePair<int, Feature>(score * 1000 - i, md));
            }
            scored.Sort((a, b2) => b2.Key.CompareTo(a.Key));
            var outp = new List<Feature>();
            foreach (var kv in scored) outp.Add(kv.Value);
            return outp;
        }

        static List<Feature> JobMembers(GameState st, string job, string productId)
        {
            var outp = new List<Feature>();
            foreach (Feature f in st.Features)
                if (f.Job == job && (f.ProductId ?? "") == productId)
                    outp.Add(f);
            return outp;
        }

        static Dictionary<string, List<Feature>> FamiliesOf(List<Feature> members)
        {
            var fams = new Dictionary<string, List<Feature>>();
            foreach (Feature md in members)
            {
                string fam = md.Family ?? "";
                if (fam == "") continue;
                if (!fams.ContainsKey(fam)) fams[fam] = new List<Feature>();
                fams[fam].Add(md);
            }
            return fams;
        }

        // ═══════════════════════ THE COST FOOTER ═════════════════════════════

        /// Build + keep + per-unit -> the works + the creak tax — the numbers
        /// MUST match SimFeatures' own reads.
        static void CostFoot(BinderScreen b, GameState st)
        {
            string computed = string.Format(CultureInfo.InvariantCulture,
                "building ${0}/wk · keeping {1} features ${2}/wk · they add ${3:0.00}/unit -> the works",
                SimFeatures.BuildTotal(st), st.Features.Count,
                SimFeatures.KeepTotal(st), SimFeatures.UnitCostTotal(st, ""));
            int creaks = SimFeatures.CreakCount(st);
            string warning = "";
            if (creaks > 0)
            {
                int tax = SimFeatures.CreakTaxPct(st);
                warning = tax > 0
                    ? string.Format(CultureInfo.InvariantCulture,
                        "{0} creak{1} tax build speed −{2}% — a rebuild bet kills a creak",
                        creaks, creaks == 1 ? "" : "s", tax)
                    : string.Format(CultureInfo.InvariantCulture,
                        "{0} creak{1} on the wall — a rebuild bet firms them up",
                        creaks, creaks == 1 ? "" : "s");
            }
            DeskKit.Footer(b,
                computed,
                "features are never free — every landing signs a keep line; the creaky card IS the debt, pointable",
                warning, FootY, RulesY);
        }

        // ═══════════════════ RUNG 3 — THE LINEUP ═════════════════════════════

        static void Lineup(BinderScreen b, GameState st)
        {
            List<string> pids = SimFeatures.ProductIds(st);
            b.L("THE LINEUP", 10f, 8f, 40f, DrawnUI.Ink, 500f);
            b.L(string.Format(CultureInfo.InvariantCulture, "· {0} things we make",
                pids.Count + 1), 250f, 20f, 22f, Ink(0.6f), 300f);
            b.L("press a product — its whole wall opens. Red climbs from any feature to this page.",
                10f, 58f, 20f, Ink(0.6f), 1100f);
            float y = 108f;
            b.L("VERSION", 430f, y, 15f, Ink(0.42f), 100f);
            b.L("FEATURES · BUILDING", 560f, y, 15f, Ink(0.42f), 220f);
            b.L("KEEP+BUILD", 930f, y, 15f, Ink(0.42f), 180f);
            y += 24f;
            y = LineupRow(b, st, "", Topic(st, "make_name", st.CompanyName), y);
            foreach (string pid in pids)
                y = LineupRow(b, st, pid, pid, y);
            y += 10f;
            b.L("SHARED PLUMBING — every product stands on these; a creak HERE taxes every build",
                10f, y, 20f, DrawnUI.Ink, 1100f);
            DeskKit.PenRule(b, y + 28f);
            y += 42f;
            List<Feature> shared = JobMembers(st, "plumbing", "");
            float x = 10f;
            int pn = 0;
            foreach (Feature f in shared)
            {
                if (pn >= 3) break;
                FeatureCard(b, st, x, y, f);
                x += 320f;
                pn++;
            }
            if (shared.Count > pn)
                DeskKit.More(b, 10f, y + 62f, shared.Count - pn, "shared pieces");
            CostFoot(b, st);
        }

        static float LineupRow(BinderScreen b, GameState st, string pid, string name, float y)
        {
            int live = 0;
            int worst = 0;
            int keep = 0;
            foreach (Feature f in st.Features)
            {
                if ((f.ProductId ?? "") != pid) continue;
                if (pid == "" && f.Job == "plumbing") continue;   // SHARED band's
                live++;
                keep += f.KeepWk;
                worst = Math.Max(worst, SoliditySev(f.Solidity));
            }
            int building = pid == "" ? SimRoadmap.CommittedBets(st).Count : 0;
            int value = keep + (pid == "" ? SimFeatures.BuildTotal(st) : 0);
            string pidCopy = pid;
            return DeskKit.HeroRow(b, y, new DeskKit.HeroRowCfg
            {
                Name = Clip(name, 22),
                Facts = string.Format(CultureInfo.InvariantCulture,
                    "v0.{0} · {1} live · {2} building", Math.Max(1, st.Product / 10),
                    live, building),
                Value = string.Format(CultureInfo.InvariantCulture, "${0}/wk", value),
                Sev = worst,
                OnPress = () =>
                {
                    b.Desk["mode"] = "product";
                    b.Desk["pid"] = pidCopy;
                },
            });
        }

        // ═══════════════════════ THE SUB-PAGES ═══════════════════════════════

        static void FamilyPage(BinderScreen b)
        {
            GameState st = b.State;
            string fam = Desk(b, "family");
            DeskKit.Back(b, "back to the wall", () => b.Desk.Clear());
            string famTitle = fam.ToLower().StartsWith("the ", StringComparison.Ordinal)
                ? fam.ToUpper() : "THE " + fam.ToUpper();
            b.L(famTitle + " FAMILY", 10f, 60f, 34f, DrawnUI.Ink, 1100f);
            var members = new List<Feature>();
            foreach (Feature f in st.Features)
                if ((f.Family ?? "") == fam) members.Add(f);
            b.L(string.Format(CultureInfo.InvariantCulture,
                "{0} features · ${1}/wk to keep · families are ink — regroup them in arrange",
                members.Count, SumKeep(members)), 10f, 106f, 20f, Ink(0.6f), 1100f);
            CardGrid(b, st, members, 150f);
        }

        static void JobPage(BinderScreen b)
        {
            GameState st = b.State;
            string job = Desk(b, "job");
            DeskKit.Back(b, "back to the wall", () => b.Desk.Clear());
            string lbl;
            if (!JobLabel.TryGetValue(job, out lbl)) lbl = job;
            b.L(lbl, 10f, 60f, 34f, DrawnUI.Ink, 1100f);
            List<Feature> members = JobMembers(st, job, "");
            b.L(string.Format(CultureInfo.InvariantCulture,
                "{0} features · ${1}/wk to keep", members.Count, SumKeep(members)),
                10f, 106f, 20f, Ink(0.6f), 1100f);
            CardGrid(b, st, members, 150f);
        }

        static void ProductPage(BinderScreen b)
        {
            GameState st = b.State;
            string pid = Desk(b, "pid");
            DeskKit.Back(b, "back to the lineup", () => b.Desk.Clear());
            string name = pid == "" ? Topic(st, "make_name", st.CompanyName) : pid;
            b.L(Clip(name, 26), 10f, 60f, 34f, DrawnUI.Ink, 700f);
            float y = 116f;
            foreach (string job in JobOrder)
            {
                if (pid == "" && job == "plumbing") continue;   // SHARED band's
                List<Feature> members = JobMembers(st, job, pid);
                if (members.Count == 0) continue;
                string lbl;
                if (!JobLabel.TryGetValue(job, out lbl)) lbl = job;
                b.L(lbl, 10f, y + 8f, 15f, Ink(0.55f), 148f);
                float x = 160f;
                int pn = 0;
                foreach (Feature f in members)
                {
                    if (pn >= 3) break;
                    FeatureCard(b, st, x, y, f);
                    x += 320f;
                    pn++;
                }
                if (members.Count > pn)
                    DeskKit.More(b, 10f, y + 30f, members.Count - pn, "more");
                y += 66f;
            }
            b.L("the pipeline wall is shared — bets land on the flagship first",
                10f, y + 12f, 17f, Ink(0.5f), 1100f);
        }

        static void CardGrid(BinderScreen b, GameState st, List<Feature> members, float y0)
        {
            float y = y0;
            float x = 10f;
            int n = 0;
            foreach (Feature f in members)
            {
                if (n >= 12) break;
                FeatureCard(b, st, x, y, f);
                x += 320f;
                n++;
                if (n % 3 == 0) { x = 10f; y += 66f; }
            }
            if (members.Count > n)
                DeskKit.More(b, 10f, y + 66f, members.Count - n, "more features");
        }

        static int SumKeep(List<Feature> members)
        {
            int total = 0;
            foreach (Feature m in members) total += m.KeepWk;
            return total;
        }

        // ═══════════ THE SHIP RITUAL (pre-roll review -> dice -> receipt) ══════

        static List<AttentionItem> PrerollRows(GameState st)
        {
            var outp = new List<AttentionItem>();
            foreach (AttentionItem it in SimEngine.PrerollItems(st))
            {
                if (it.Key == "bet_ready") continue;
                outp.Add(it);
            }
            return outp;
        }

        static void PrerollCard(BinderScreen b)
        {
            GameState st = b.State;
            string id = Desk(b, "bet");
            Bet bet = SimRoadmap.BetById(st, id);
            if (bet == null) { b.Desk.Clear(); Draw(b); return; }
            List<AttentionItem> rows = PrerollRows(st);
            var read = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (i >= 5)
                {
                    read.Add(string.Format(CultureInfo.InvariantCulture,
                        "…and {0} more, on the threats page.", rows.Count - i));
                    break;
                }
                read.Add(string.Format(CultureInfo.InvariantCulture, "{0}{1} — {2}",
                    rows[i].Severity >= 3 ? "! " : "", rows[i].Desk, rows[i].Label));
            }
            string toDesk = rows.Count > 0 ? rows[0].Desk : "";
            DeskKit.Review(b, new DeskKit.ReviewCard
            {
                Banner = string.Format(CultureInfo.InvariantCulture,
                    "before the dice: '{0}' ships on a d20 vs DC {1}", bet.Name,
                    SimRoadmap.BetDc(bet)),
                Read = read,
                Verdict = "fix them, or roll and live with it.",
                Note = string.Format(CultureInfo.InvariantCulture,
                    "clean ship ~{0}% at build {1} — debt, focus and flow all move that number",
                    SimRoadmap.ShipOddsPct(st, bet), st.Competence("build")),
                Confirm = "roll anyway",
                Cancel = "go fix it",
                OnConfirm = () => Fire(b, id),
                OnCancel = () =>
                {
                    b.Desk.Clear();
                    if (!string.IsNullOrEmpty(toDesk)) b.FocusDesk(toDesk);
                },
            });
        }

        static void Fire(BinderScreen b, string id)
        {
            SimRoadmap.ShipResult res = SimRoadmap.ShipReady(b.State, id);
            b.Desk.Clear();
            if (res != null)
            {
                b.Desk["mode"] = "shipped";
                b.Desk["ship"] = res;
            }
            b.Refresh();
        }

        static void ShipCard(BinderScreen b)
        {
            object stored;
            var res = b.Desk.TryGetValue("ship", out stored)
                ? stored as SimRoadmap.ShipResult : null;
            if (res == null) { b.Desk.Clear(); Draw(b); return; }
            DeskKit.Back(b, "back to the wall", () => b.Desk.Clear());
            float y = 90f;
            b.L(res.Event, DeskKit.XId, y, DeskKit.TitleSize,
                res.Band == "brilliant" || res.Band == "fine" ? DrawnUI.Sage : DrawnUI.Coral,
                1100f);
            y += 64f;
            y = DeskKit.Rule(b, y);
            for (int i = 0; i < res.Lines.Count; i++)
            {
                var l = b.L(res.Lines[i], DeskKit.XId, y, DeskKit.Status, Ink(0.85f), 1100f);
                y += Mathf.Max(BinderScreen.Height(l), 32f) + 6f;
            }
            if (res.Band != "backfired")
                b.L("it joins the wall at the week's close — keep-cost signs on with it.",
                    DeskKit.XId, y, DeskKit.Detail, Ink(0.6f), 1100f);
            DeskKit.Footer(b,
                string.Format(CultureInfo.InvariantCulture,
                    "the dice were {0}{1} against DC {2} — margin {3}",
                    res.D20, res.Mod.ToString("+0;-0;+0", CultureInfo.InvariantCulture),
                    res.Dc, res.Total - res.Dc),
                "LAUNCH RISK: scope widens the spread — preparation is the only thing that moves the odds.",
                "");
        }

        // ── the small helpers ────────────────────────────────────────────────

        static string Mode(BinderScreen b) { return Desk(b, "mode"); }

        static string Desk(BinderScreen b, string key)
        {
            object v;
            return b.Desk.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }

        static string Topic(GameState st, string key, string fallback)
        {
            object v;
            if (st.Topics != null && st.Topics.TryGetValue(key, out v) && v != null)
            {
                string s = v.ToString();
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return fallback ?? "";
        }

        static string Clip(string s, int n)
        {
            s = s ?? "";
            return s.Length > n ? s.Substring(0, n) : s;
        }

        static Color Ink(float a) { return DrawnUI.WithAlpha(DrawnUI.Ink, a); }

        /// Every control carries its own closure; the router stays because
        /// `BinderScreen` names desks in its match.
        public static void Handle(BinderScreen b, string id)
        {
        }
    }
}
