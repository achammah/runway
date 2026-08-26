using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// DESK — THE COMPANY · "the street" (twin of desk_street_page.gd). W2
    /// lane: L-COMPANY. THE QUESTION: "what is the world doing to us?"
    /// Spec: 12-binder-rework-2.md § the street + 11-binder-rework.md.
    ///
    /// HERO the weather · 1 THE WEATHER (drawn season band + what changes
    /// this week) · 2 THE RIVALS (posture chips, heat dot, the last-3 record;
    /// acts that came at YOU stay face-up) · 3 THE INVESTORS' MOOD (multiples
    /// band + appetite word — the raise's radar) · 4 TAKEN FROM US (each row
    /// with its counter-desk jump). A page you READ; its only controls jump.
    /// </summary>
    public static class DeskStreetPage
    {
        public const string Question = "what is the world doing to us?";

        public static string[] HeroSummary(GameState s)
        {
            return new[] { SeasonBig(s), WeekSentence(s) };
        }

        static string SeasonBig(GameState s)
        {
            switch (SimStreet.Season(s))
            {
                case "winter": return "funding winter";
                case "boom": return "a boom";
            }
            switch (SimStreet.TrendBand(s.MarketTrend))
            {
                case "tailwinds": return "tailwinds";
                case "headwinds": return "headwinds";
            }
            return "a calm street";
        }

        static string WeekSentence(GameState s)
        {
            if (SimEngine.HasStatus(s, "funding_winter"))
                return string.Format("checks shrink and terms bite — valuations ×0.6 · {0} wks left",
                    SimStreet.WeeksLeft(s, "funding_winter"));
            if (SimEngine.HasStatus(s, "boom"))
                return string.Format("every round oversubscribed — valuations ×1.3 · {0} wks left",
                    SimStreet.WeeksLeft(s, "boom"));
            if (SimEngine.HasStatus(s, "winter_watch")) return SimStreet.BANNER["winter_watch"];
            if (SimEngine.HasStatus(s, "boom_watch")) return SimStreet.BANNER["boom_watch"];
            return SimStreet.SeasonRead(s.MarketTrend);
        }

        static bool CameAtYou(Rival rd)
        {
            if (rd.Sniffing > 0) return true;
            return rd.LastAction == "poach" || rd.LastAction == "price_cut"
                || rd.LastAction == "sniff";
        }

        static List<Rival> Ranked(GameState s)
        {
            var rows = new List<Rival>(s.Rivals);
            rows.Sort((a, c) => c.Strength.CompareTo(a.Strength));
            var faced = new List<Rival>();
            var calm = new List<Rival>();
            for (int i = 0; i < rows.Count; i++)
                (CameAtYou(rows[i]) ? faced : calm).Add(rows[i]);
            faced.AddRange(calm);
            return faced;
        }

        static int Heat(GameState s, Rival rd)
        {
            double gap = rd.Strength - SimStreet.PlayerPower(s);
            if (gap > 15.0 || CameAtYou(rd)) return 3;
            return gap > 0.0 ? 2 : 1;
        }

        static string Appetite(GameState s)
        {
            if (SimEngine.HasStatus(s, "funding_winter")) return "cold — fewer inbound knocks";
            double score = s.RaiseState != null ? s.RaiseState.InterestScore : 0.0;
            if (score >= 50.0) return "hungry — knocks likely";
            if (score >= 25.0) return "warm — worth a call";
            if (score > 0.0) return "curious — traction talks first";
            return "quiet — nobody is dialing yet";
        }

        sealed class WireRow
        {
            public string Label = "";
            public string Desk = "";
        }

        static List<WireRow> Wire(GameState s)
        {
            var rows = new List<WireRow>();
            if (SimEngine.HasStatus(s, "price_war"))
            {
                int down = (int)Math.Round((1.0 - SimEngine.StreetFairMult(s)) * 100.0);
                rows.Add(new WireRow { Label = string.Format(
                    "price war — the going rate is down {0}% ({1} wks left)",
                    down, SimStreet.WeeksLeft(s, "price_war")), Desk = "offers" });
            }
            if ((int)s.GetMetaF("poach_wk", -1) == s.Week)
                rows.Add(new WireRow { Label = string.Format(
                    "{0} was called with a number this week",
                    Convert.ToString(s.GetMeta("poach_name", "someone"))), Desk = "team" });
            else if ((int)s.GetMetaF("poach_failed_wk", -1) == s.Week)
                rows.Add(new WireRow { Label = string.Format(
                    "{0} was called — they stayed, this time",
                    Convert.ToString(s.GetMeta("poach_failed_name", "someone"))), Desk = "team" });
            for (int i = 0; i < s.Rivals.Count; i++)
            {
                Rival rd = s.Rivals[i];
                if (rd.Sniffing > 0)
                    rows.Add(new WireRow { Label = string.Format(
                        "{0} is circling — asking your price", rd.Name), Desk = "cap table" });
                else if (rd.Focus == "price" && rd.PricePosture <= 0.92)
                    rows.Add(new WireRow { Label = string.Format(
                        "{0} is undercutting from below your price umbrella", rd.Name),
                        Desk = "offers" });
            }
            return rows;
        }

        static Color SeasonCol(GameState s)
        {
            switch (SimStreet.Season(s))
            {
                case "winter": return DrawnUI.Coral;
                case "boom": return DrawnUI.Sage;
            }
            return DrawnUI.Blue;
        }

        public static void Draw(BinderScreen b)
        {
            GameState s = b.State;
            object mode;
            if (b.Desk.TryGetValue("mode", out mode) && Convert.ToString(mode) == "rivals")
            {
                DrawAllRivals(b, s);
                return;
            }

            // HERO — the weather answers the tab's question in one second
            float y = DeskKit.HeroBand(b, SeasonBig(s), WeekSentence(s), SeasonCol(s));

            // 1 · THE WEATHER
            DeskKit.CardBox z1 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, 92f, 1,
                "the weather", "");
            DeskKit.Meter(b, z1.ContentX, z1.ContentY + 4f, 560f, 1f, SeasonCol(s),
                SimStreet.SeasonRead(s.MarketTrend));
            string shock = "";
            if (SimEngine.HasStatus(s, "funding_winter")) shock = "funding_winter";
            else if (SimEngine.HasStatus(s, "boom")) shock = "boom";
            if (shock != "")
                DeskKit.ClockChip(b, z1.ContentX + 950f, z1.ContentY + 2f,
                    string.Format("{0} wks left", SimStreet.WeeksLeft(s, shock)));
            y = z1.Bottom + 12f;

            // 2 · THE RIVALS — the record is the tell
            List<Rival> ranked = Ranked(s);
            float z2H = 74f + Math.Min(ranked.Count, 3) * 84f
                        + (ranked.Count > 3 ? 44f : 0f) + 6f;
            if (ranked.Count == 0) z2H = 74f + 96f;
            DeskKit.CardBox z2 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, z2H, 2,
                "the rivals", "read the record, not the vibes — the pattern is the tell");
            float ry = z2.Cursor;
            if (ranked.Count == 0)
                DeskKit.Empty(b, z2.ContentX, ry,
                    "nobody is competing with you this week.",
                    "that is rarer, and more temporary, than it feels.");
            for (int i = 0; i < Math.Min(ranked.Count, 3); i++)
            {
                Rival rd = ranked[i];
                DeskKit.SevDot(b, z2.ContentX, ry + 2f, Heat(s, rd));
                b.L(rd.Name, z2.ContentX + 34f, ry - 4f, DeskKit.Row, DrawnUI.Ink, 380f);
                if (CameAtYou(rd))
                    b.L("-> they came at YOU", z2.ContentX + 430f, ry, DeskKit.Detail,
                        DeskKit.Alert, 300f);
                b.L(SimEngine.Fuzz(rd.Strength), z2.ContentX + 900f, ry, DeskKit.Detail,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 180f);
                float cx = z2.ContentX + 34f;
                cx = DeskKit.ChipToken(b, cx, ry + 26f, new DeskKit.ChipCfg
                    { Text = SimStreet.VigorWord(rd.Vigor) });
                cx = DeskKit.ChipToken(b, cx, ry + 26f, new DeskKit.ChipCfg
                    { Text = SimStreet.PostureWord(rd.PricePosture) });
                cx = DeskKit.ChipToken(b, cx, ry + 26f, new DeskKit.ChipCfg
                    { Text = "fights on " + (rd.Focus ?? "growth") });
                DeskKit.ChipToken(b, cx, ry + 26f, new DeskKit.ChipCfg
                    { Text = SimStreet.HypeWord(rd.Hype) });
                if (rd.Log != null && rd.Log.Count > 0)
                {
                    int from = Math.Max(rd.Log.Count - 3, 0);
                    b.L(string.Join("  ·  ", rd.Log.GetRange(from, rd.Log.Count - from)
                            .ToArray()), z2.ContentX + 34f, ry + 60f, 17f,
                        DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 1040f);
                }
                ry += 84f;
            }
            if (ranked.Count > 3)
                DeskKit.FoldRow(b, z2.ContentX, ry, ranked.Count - 3, "rivals",
                    () => { b.Desk["mode"] = "rivals"; });
            y = z2.Bottom + 12f;

            // 3 · THE INVESTORS' MOOD — the raise's radar reads this
            DeskKit.CardBox z3 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, 118f, 3,
                "the investors' mood", "");
            b.L(string.Format("the street pays ×{0:0.0} the usual  ·  appetite: {1}",
                SimEngine.ShockValMult(s), Appetite(s)),
                z3.ContentX, z3.ContentY, DeskKit.Detail,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.85f), 800f);
            var names = new List<string>();
            for (int i = 0; i < Math.Min(s.Investors.Count, 3); i++)
                names.Add(s.Investors[i].Name);
            string book = names.Count > 0
                ? "in the book: " + string.Join(", ", names.ToArray())
                : "no investors in the book yet";
            if (s.Investors.Count > 3)
                book += string.Format("  +{0} more", s.Investors.Count - 3);
            b.L(book, z3.ContentX, z3.ContentY + 30f, DeskKit.Detail,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 760f);
            DeskKit.Word(b, "feeds THE RAISE ->", z3.ContentX + 840f, z3.ContentY + 12f,
                () => b.FocusDesk("the raise"), DeskKit.Detail,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 260f);
            y = z3.Bottom + 12f;

            // 4 · TAKEN FROM US / THE WIRE — every row names its counter-desk
            List<WireRow> wire = Wire(s);
            float z4H = 52f + Math.Max(Math.Min(wire.Count, 2) * 30f, 28f) + 6f;
            DeskKit.CardBox z4 = DeskKit.Zone(b, DeskKit.XId, y, 1120f, z4H, 4,
                "taken from us", "");
            float wy = z4.ContentY - 14f;
            if (wire.Count == 0)
                b.L("nothing taken this week — the street is only resting.",
                    z4.ContentX, wy, DeskKit.Detail,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f), 1000f);
            for (int i = 0; i < Math.Min(wire.Count, 2); i++)
            {
                WireRow row = wire[i];
                string tail = i == 1 && wire.Count > 2
                    ? string.Format("   · +{0} more on threats", wire.Count - 2) : "";
                DeskKit.SevDot(b, z4.ContentX, wy + 2f, 2);
                b.L(row.Label + tail, z4.ContentX + 32f, wy, DeskKit.Detail,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.85f), 810f);
                string dsk = row.Desk;
                DeskKit.Word(b, dsk + " ->", z4.ContentX + 880f, wy - 6f,
                    () => b.FocusDesk(dsk), DeskKit.Detail, DrawnUI.Coral, 200f);
                wy += 30f;
            }

            double pressure = 0.0;
            for (int i = 0; i < s.Rivals.Count; i++) pressure += s.Rivals[i].Strength;
            pressure = Math.Min(pressure / Math.Max(s.Rivals.Count, 1) / 100.0 * 0.5, 0.45);
            // the foot rides below the last zone when the stack runs deep
            float fy = Math.Max(820f, z4.Bottom + 6f);
            DeskKit.Footer(b,
                string.Format("rival pressure is shaving {0}% off adoption · the trend "
                    + "multiplies every sale ×{1:0.00}",
                    (int)Math.Round(pressure * 100.0), s.MarketTrend),
                "none of this is yours to change from here — the street acts, the desks answer",
                "", fy, fy + 32f);
        }

        static void DrawAllRivals(BinderScreen b, GameState s)
        {
            DeskKit.Back(b, "← the street", () => { b.Desk.Remove("mode"); });
            // the drill still answers the tab's question before the list starts
            int came = 0;
            for (int i = 0; i < s.Rivals.Count; i++)
                if (CameAtYou(s.Rivals[i])) came += 1;
            float y = DeskKit.HeroBand(b,
                s.Rivals.Count + " rival" + (s.Rivals.Count == 1 ? "" : "s") + " on the street",
                came > 0
                    ? came + " came at YOU this month — every rap sheet below, loudest first"
                    : "every rap sheet below, loudest first", DrawnUI.Ink, 44f);
            List<Rival> ranked = Ranked(s);
            for (int i = 0; i < ranked.Count; i++)
            {
                Rival rd = ranked[i];
                int from = rd.Log != null ? Math.Max(rd.Log.Count - 3, 0) : 0;
                y = DeskKit.LogBlock(b, y, new DeskKit.LogRow
                {
                    Identity = string.Format("{0} — {1}", rd.Name, SimEngine.Fuzz(rd.Strength)),
                    Posture = SimStreet.PostureLine(rd),
                    Plays = "plays: " + string.Join(", ",
                        (rd.Tactics ?? new List<string>()).ToArray()),
                    Trail = rd.Log != null
                        ? rd.Log.GetRange(from, rd.Log.Count - from) : new List<string>(),
                });
            }
        }

        public static void Handle(BinderScreen b, string id)
        {
            if (id.StartsWith("go:")) b.FocusDesk(id.Substring(3));
            else if (id == "rivals") b.Desk["mode"] = "rivals";
            else if (id == "back") b.Desk.Remove("mode");
        }
    }
}
