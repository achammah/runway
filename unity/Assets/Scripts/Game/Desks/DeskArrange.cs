using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// THE ARRANGE MODE — the works' WRITE view (twin of desk_arrange.gd;
    /// DECISIONS: ARRANGE MODE + ARRANGE EDITS THE BINS; mockup 14). Bins +
    /// chips, two presses no drag; CHIP MOVES CONFIRM ONE BY ONE; bin
    /// operations stage into ONE composite receipt (the teardown wizard); the
    /// ghost bin is the SAME open_site door as the written move; Esc abandons
    /// through the binder's desk-mode pop. Every price is the ENGINE's own
    /// week-stable quote.
    /// </summary>
    public static class DeskArrange
    {
        const float BinW = 176f, BinH = 200f, BinGap = 12f;

        static string DS(BinderScreen b, string key)
        {
            object v;
            return b.Desk.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }

        public static void Draw(BinderScreen b)
        {
            GameState s = b.State;
            DeskKit.Back(b, "back to the works", () =>
            {
                b.Desk["mode"] = "";
                foreach (string k in new[] { "chip_k", "staged2", "teardown", "open_roof",
                    "edit", "arrange_axis" })
                    b.Desk.Remove(k);
            });
            if (DS(b, "open_roof") == "True" || (b.Desk.ContainsKey("open_roof")
                && b.Desk["open_roof"] is bool && (bool)b.Desk["open_roof"]))
            {
                OpenRoofSheet(b, s);
                return;
            }
            string td = DS(b, "teardown");
            if (td.Length > 0) { TeardownSheet(b, s, td); return; }
            b.L("ARRANGE — press a thing, then press its new home", 230f, 8f,
                DeskKit.Status, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 620f);
            b.L("ARRANGING · Esc exits", DeskKit.XId + 890f, 10f, DeskKit.Detail,
                DrawnUI.Coral, 230f);
            string axis = Axis(b, s);
            float y = 58f;
            if (axis == "product") y = ProductBins(b, s, y);
            else
            {
                y = SiteBins(b, s, y);
                if (DS(b, "edit").Length > 0) y += 104f;
            }
            if (SimDivisions.ProductsCount(s) >= 2 && axis == "site")
            {
                DeskKit.Word(b, "arrange the paper instead (offers → products) →",
                    DeskKit.XId, y, () =>
                    {
                        b.Desk["arrange_axis"] = "product";
                        b.Desk.Remove("chip_k");
                        b.Desk.Remove("staged2");
                    }, DeskKit.Detail, DrawnUI.Blue, 560f);
                y += 44f;
            }
            else if (axis == "product")
            {
                DeskKit.Word(b, "back to the roofs (people, machines, spend) →",
                    DeskKit.XId, y, () =>
                    {
                        b.Desk["arrange_axis"] = "site";
                        b.Desk.Remove("chip_k");
                        b.Desk.Remove("staged2");
                    }, DeskKit.Detail, DrawnUI.Blue, 560f);
                y += 44f;
            }
            b.L("bound to their objects (never move by hand): rent → its roof · serving costs → their offer · interest → its note",
                DeskKit.XId, y, DeskKit.Law, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 1100f);
            y += 40f;
            var staged = b.Desk.ContainsKey("staged2")
                ? b.Desk["staged2"] as Dictionary<string, object> : null;
            if (staged == null || staged.Count == 0)
            {
                string picked = DS(b, "chip_k");
                DeskKit.Footer(b, "ink is free · brick is priced · obligations survive removal",
                    picked.Length > 0
                        ? "now press its new home — the receipt prints before anything books"
                        : "two presses stage a move; chip moves confirm one by one",
                    "", 806f, 840f);
                return;
            }
            StagedReceipt(b, s, staged, y);
        }

        // ───────────────────────────── the bins ────────────────────────────

        static string Axis(BinderScreen b, GameState s)
        {
            string ax = DS(b, "arrange_axis");
            if (ax != "product") ax = "site";
            if (ax == "product" && SimDivisions.ProductsCount(s) < 2) ax = "site";
            return ax;
        }

        static List<EquipmentItem> Eq(GameState s)
        {
            return s.Hardware != null && s.Hardware.Equipment != null
                ? s.Hardware.Equipment : new List<EquipmentItem>();
        }

        static float SiteBins(BinderScreen b, GameState s, float y)
        {
            var ids = new List<string> { "" };
            foreach (Site site in s.Sites) ids.Add(site.Id ?? "");
            int shownSites = Gd.Mini(ids.Count, 4);
            string picked = DS(b, "chip_k");
            float bx = DeskKit.XId;
            for (int i = 0; i < shownSites; i++)
                bx = OneBin(b, s, bx, y, ids[i], picked);
            if (ids.Count > shownSites)
                b.L("+" + (ids.Count - shownSites) + " more roofs", bx + 4f, y + 8f,
                    DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 150f);
            DeskKit.CardBox shared = DeskKit.Bin(b, bx, y, BinW, BinH, new DeskKit.BinCfg
            {
                Title = "SHARED / HQ", Note = "what has no roof",
                OnPress = () => PressBin(b, s, "shared"),
            });
            float cy = shared.Cursor;
            float cx = shared.ContentX;
            int nShared = 0;
            for (int li = 0; li < s.SpendBook.Count; li++)
            {
                SpendLine l = s.SpendBook[li];
                if (!string.IsNullOrEmpty(l.Division)) continue;
                if (nShared < 2)
                {
                    string key = "s:" + li;
                    DeskKit.ChipToken(b, cx, cy, new DeskKit.ChipCfg
                    {
                        Text = Left(l.Name ?? "line", 12), Kind = "spend",
                        Selected = picked == key, OnPress = () => PressChip(b, key),
                    });
                    cy += 42f;
                }
                nShared += 1;
            }
            if (nShared > 2)
                b.L("+" + (nShared - 2) + " more", cx, cy + 2f, DeskKit.Law, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 120f);
            else if (nShared == 0)
                b.L("you · brand ads", cx, cy + 2f, DeskKit.Law, DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), 150f);
            bx += BinW + BinGap;
            DeskKit.Bin(b, bx, y, BinW, BinH, new DeskKit.BinCfg
            {
                Ghost = true, OnPress = () => { b.Desk["open_roof"] = true; },
            });
            return y + BinH + 22f;
        }

        static float OneBin(BinderScreen b, GameState s, float bx, float y, string id,
            string picked)
        {
            bool isHome = id.Length == 0;
            bool red = !isHome
                && SimDivisions.MarkedUntil(s, "works_red", id) >= SimDivisions.RED_WEEKS;
            DeskKit.CardBox f = DeskKit.Bin(b, bx, y, BinW, BinH, new DeskKit.BinCfg
            {
                Title = Left(SimDivisions.RoofName(s, id), 14), Note = BinCounts(s, id),
                Closing = red, OnPress = () => PressBin(b, s, id),
            });
            float cy = f.Cursor;
            float cx = f.ContentX;
            int chips = 0;
            for (int ei = 0; ei < s.Employees.Count; ei++)
            {
                Employee e = s.Employees[ei];
                if ((e.Site ?? "") != id) continue;
                if (chips < 2)
                {
                    string key = "e:" + ei;
                    string first = (e.Name ?? "?").Split(' ')[0];
                    DeskKit.ChipToken(b, cx, cy, new DeskKit.ChipCfg
                    {
                        Text = first, Kind = "person", Selected = picked == key,
                        OnPress = () => PressChip(b, key),
                    });
                    cy += 42f;
                }
                chips += 1;
            }
            int mChips = 0;
            List<EquipmentItem> eq = Eq(s);
            for (int mi = 0; mi < eq.Count; mi++)
            {
                EquipmentItem m = eq[mi];
                if ((m.Site ?? "") != id) continue;
                if (chips < 2 && mChips < 1)
                {
                    string mkey = "m:" + mi;
                    DeskKit.ChipToken(b, cx, cy, new DeskKit.ChipCfg
                    {
                        Text = Left(m.Name ?? "?", 12), Kind = "machine",
                        Selected = picked == mkey, OnPress = () => PressChip(b, mkey),
                    });
                    cy += 42f;
                    chips += 1;
                }
                mChips += 1;
            }
            int crowd = CrowdCount(s, id) - Gd.Mini(chips, 2);
            if (crowd > 0)
                b.L("+" + crowd + " more", cx, cy + 2f, DeskKit.Law, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 120f);
            if (!isHome)
            {
                string captured = id;
                DeskKit.Word(b, "edit", bx + 6f, y + BinH - 40f, () =>
                {
                    b.Desk["edit"] = captured;
                    b.Desk["open_roof"] = false;
                }, DeskKit.Law, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 70f);
                DeskKit.Word(b, "close", bx + 86f, y + BinH - 40f, () =>
                {
                    b.Desk["teardown"] = captured;
                }, DeskKit.Law, DeskKit.Alert, 80f);
            }
            if (DS(b, "edit") == id && !isHome)
                EditPanel(b, s, id, bx, y + BinH + 4f);
            return bx + BinW + BinGap;
        }

        static string BinCounts(GameState s, string id)
        {
            int heads = 0;
            foreach (Employee e in s.Employees)
                if ((e.Site ?? "") == id) heads += 1;
            if (id.Length == 0 && s.Sites.Count == 0) heads += 1;
            string hands = heads + " hand" + (heads == 1 ? "" : "s");
            Site site = SimDivisions.SiteById(s, id);
            if (site == null)
                return id.Length == 0 ? hands + " · the era's roof" : hands;
            return string.Format("{0} · rent ${1}/wk", hands, site.RentWk);
        }

        static int CrowdCount(GameState s, string id)
        {
            int n = 0;
            foreach (Employee e in s.Employees)
                if ((e.Site ?? "") == id) n += 1;
            foreach (EquipmentItem m in Eq(s))
                if ((m.Site ?? "") == id) n += 1;
            return n;
        }

        static float ProductBins(BinderScreen b, GameState s, float y)
        {
            string picked = DS(b, "chip_k");
            var pids = new List<string>();
            foreach (Offer o in s.Offers)
            {
                string pid = o.ProductId ?? "";
                if (!pids.Contains(pid)) pids.Add(pid);
            }
            float bx = DeskKit.XId;
            for (int i = 0; i < pids.Count && i < 5; i++)
            {
                string pid2 = pids[i];
                DeskKit.CardBox f = DeskKit.Bin(b, bx, y, BinW + 30f, BinH, new DeskKit.BinCfg
                {
                    Title = Left(pid2.Length == 0 ? "the flagship" : pid2, 14),
                    Note = "a grouping of offers — paper",
                    OnPress = () => PressBin(b, s, "p:" + pid2),
                });
                float cy = f.Cursor;
                float cx = f.ContentX;
                int n = 0;
                for (int oi = 0; oi < s.Offers.Count; oi++)
                {
                    Offer od = s.Offers[oi];
                    if ((od.ProductId ?? "") != pid2) continue;
                    if (n < 3)
                    {
                        string key = "o:" + oi;
                        DeskKit.ChipToken(b, cx, cy, new DeskKit.ChipCfg
                        {
                            Text = Left(od.Name ?? "?", 14), Kind = "spend",
                            Selected = picked == key, OnPress = () => PressChip(b, key),
                        });
                        cy += 42f;
                    }
                    n += 1;
                }
                if (n > 3)
                    b.L("+" + (n - 3) + " more", cx, cy + 2f, DeskKit.Law, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 120f);
                bx += BinW + 30f + BinGap;
            }
            b.L("PAPER divisions restructure FREE — a product is a grouping of offers, and regrouping is ink",
                DeskKit.XId, y + BinH + 6f, DeskKit.Law, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 1100f);
            return y + BinH + 40f;
        }

        // ─────────────────── two presses, one receipt ──────────────────────

        static void PressChip(BinderScreen b, string key)
        {
            if (DS(b, "chip_k") == key) b.Desk.Remove("chip_k");
            else
            {
                b.Desk["chip_k"] = key;
                b.Desk.Remove("staged2");
            }
        }

        static void PressBin(BinderScreen b, GameState s, string target)
        {
            string key = DS(b, "chip_k");
            if (key.Length == 0) return;
            string kind = key.Split(':')[0];
            int idx = int.Parse(key.Split(':')[1], CultureInfo.InvariantCulture);
            var st = new Dictionary<string, object>
                { { "kind", kind }, { "idx", idx }, { "to", target } };
            switch (kind)
            {
                case "e":
                    if (target.StartsWith("p:") || target == "shared") return;
                    st["quote"] = SimDivisions.ReassignQuote(s, idx, target);
                    break;
                case "m":
                    if (target.StartsWith("p:") || target == "shared") return;
                    st["quote"] = SimDivisions.MoveQuote(s, idx, target);
                    break;
                case "o":
                    if (!target.StartsWith("p:")) return;
                    st["quote"] = new Dictionary<string, object>();
                    break;
                case "s":
                    if (target.StartsWith("p:")) return;
                    st["quote"] = new Dictionary<string, object>();
                    break;
            }
            b.Desk["staged2"] = st;
            b.Desk.Remove("chip_k");
        }

        static void StagedReceipt(BinderScreen b, GameState s, Dictionary<string, object> st,
            float y)
        {
            string kind = st.ContainsKey("kind") ? (string)st["kind"] : "";
            int idx = Convert.ToInt32(st["idx"], CultureInfo.InvariantCulture);
            string target = (string)st["to"];
            var q = st["quote"] as Dictionary<string, object> ?? new Dictionary<string, object>();
            var lines = new List<DeskKit.TicketLine>();
            string total = "";
            Color totalCol = DrawnUI.Coral;
            Action fire = null;
            int fee = q.ContainsKey("fee")
                ? Convert.ToInt32(q["fee"], CultureInfo.InvariantCulture) : 0;
            switch (kind)
            {
                case "e":
                {
                    string nm = idx < s.Employees.Count ? s.Employees[idx].Name ?? "?" : "?";
                    lines.Add(new DeskKit.TicketLine
                        { Label = nm + " → " + SimDivisions.RoofName(s, target),
                          Value = "$" + fee + " now" });
                    lines.Add(new DeskKit.TicketLine
                        { Label = "the ramp at the new roof", Value = "1 wk at zero" });
                    total = "$" + fee + " now";
                    fire = () =>
                    {
                        var res = SimDivisions.ReassignEmployee(s, idx, target);
                        b.Desk["note"] = (bool)res["ok"] ? "" : (string)res["why"];
                        b.Desk.Remove("staged2");
                    };
                    break;
                }
                case "m":
                {
                    List<EquipmentItem> eq = Eq(s);
                    string mn = idx < eq.Count ? eq[idx].Name ?? "?" : "?";
                    lines.Add(new DeskKit.TicketLine
                        { Label = mn + " → " + SimDivisions.RoofName(s, target) + " (shipping)",
                          Value = "$" + fee + " now" });
                    lines.Add(new DeskKit.TicketLine
                        { Label = "off the floor", Value = "1 wk offline" });
                    total = "$" + fee + " now";
                    fire = () =>
                    {
                        var res = SimDivisions.MoveMachine(s, idx, target);
                        b.Desk["note"] = (bool)res["ok"] ? "" : (string)res["why"];
                        b.Desk.Remove("staged2");
                    };
                    break;
                }
                case "o":
                {
                    string onm = idx < s.Offers.Count ? s.Offers[idx].Name ?? "?" : "?";
                    string pid = target.StartsWith("p:") ? target.Substring(2) : "";
                    lines.Add(new DeskKit.TicketLine
                        { Label = onm + " files under " + (pid.Length == 0 ? "the flagship" : pid),
                          Value = "free — ink" });
                    total = "$0 — paper is paper";
                    totalCol = DrawnUI.Sage;
                    fire = () =>
                    {
                        SimDivisions.TagOffer(s, idx, pid);
                        b.Desk.Remove("staged2");
                    };
                    break;
                }
                case "s":
                {
                    string snm = idx < s.SpendBook.Count ? s.SpendBook[idx].Name ?? "?" : "?";
                    lines.Add(new DeskKit.TicketLine
                        { Label = snm + " files under "
                            + (target == "shared" || target.Length == 0
                                ? "SHARED/HQ" : SimDivisions.RoofName(s, target)),
                          Value = "free — ink" });
                    total = "$0 — paper is paper";
                    totalCol = DrawnUI.Sage;
                    fire = () =>
                    {
                        SimDivisions.TagSpendLine(s, idx, target == "shared" ? "" : target);
                        b.Desk.Remove("staged2");
                    };
                    break;
                }
            }
            float endY = DeskKit.Ticket(b, DeskKit.XId, y, 560f,
                "the staged change — nothing is booked yet", lines, "the price of the move",
                total, "the engine quoted this — signing books exactly these numbers", totalCol);
            DeskKit.Arm(b, "arrange_confirm", "CONFIRM the move", "press again — it books now",
                620f, y + 30f, fire, 360f);
            DeskKit.Word(b, "tear it up", 620f, y + 84f, () => b.Desk.Remove("staged2"),
                DeskKit.Status, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 240f);
            string note = DS(b, "note");
            if (note.Length > 0)
                b.L(note, 620f, y + 130f, DeskKit.Detail, DrawnUI.Coral, 500f);
            DeskKit.Footer(b, "", "Esc abandons the whole staged change", "",
                Mathf.Max(endY, 760f), 840f);
        }

        // ─────────────── the edit panel (brick + ink) ──────────────────────

        static void EditPanel(BinderScreen b, GameState s, string id, float x, float y)
        {
            Site site = SimDivisions.SiteById(s, id);
            if (site == null) return;
            float px = Mathf.Clamp(x, DeskKit.XId, 700f);
            b.L("edit " + site.Name + " — rename is ink; the roof is brick", px, y,
                DeskKit.Detail, DrawnUI.Ink, 420f);
            DeskKit.Word(b, "rename (free) →", px, y + 30f, () =>
            {
                string[] pool = SimDivisions.NAME_POOL;
                int i = (Array.IndexOf(pool, site.Name) + 1 + pool.Length) % pool.Length;
                SimDivisions.RenameSite(s, id, pool[i]);
            }, DeskKit.Detail, DrawnUI.Blue, 200f);
            Dictionary<string, object> up = SimDivisions.RelaseQuote(s, id, 1);
            Dictionary<string, object> down = SimDivisions.RelaseQuote(s, id, -1);
            DeskKit.Arm(b, "relase_up_" + id,
                "bigger roof — rent $" + up["new_rent"] + "/wk",
                "press again — $" + up["fee"] + " books, a moving week",
                px + 210f, y + 24f, () =>
                {
                    SimDivisions.EditSite(s, id, 1);
                    b.Desk.Remove("edit");
                }, 340f, DeskKit.Detail);
            DeskKit.Arm(b, "relase_dn_" + id,
                "smaller roof — rent $" + down["new_rent"] + "/wk",
                "press again — $" + down["fee"] + " books, a moving week",
                px + 560f, y + 24f, () =>
                {
                    SimDivisions.EditSite(s, id, -1);
                    b.Desk.Remove("edit");
                }, 340f, DeskKit.Detail);
        }

        // ───────────── the teardown wizard (one receipt) ───────────────────

        static void TeardownSheet(BinderScreen b, GameState s, string id)
        {
            Site site = SimDivisions.SiteById(s, id);
            if (site == null) { b.Desk.Remove("teardown"); return; }
            b.L("CLOSING " + (site.Name ?? "?").ToUpper() + " — every piece decided, one total",
                230f, 8f, DeskKit.Status, DrawnUI.Ink, 700f);
            float y = 56f;
            Dictionary<string, string> decisions = Decisions(b, s, id);
            bool any = false;
            for (int ei = 0; ei < s.Employees.Count; ei++)
            {
                Employee e = s.Employees[ei];
                if ((e.Site ?? "") != id) continue;
                any = true;
                string key = "e:" + ei;
                string cur = decisions.ContainsKey(key) ? decisions[key] : "go";
                b.L(e.Name ?? "?", DeskKit.XId + 10f, y, DeskKit.Detail, DrawnUI.Ink, 240f);
                int sev = SimLabor.SeveranceFor(s, e);
                string caption = cur == "go"
                    ? "let go — severance $" + sev
                    : string.Format("move → {0} (+${1})",
                        SimDivisions.RoofName(s, cur.Substring(5)),
                        Gd.RoundToInt(SimDivisions.Pb(s, "relocation_fee")));
                string capturedKey = key;
                string capturedCur = cur;
                DeskKit.Word(b, caption, DeskKit.XId + 260f, y - 6f, () =>
                {
                    b.Desk["td_" + capturedKey] = NextDecision(s, id, capturedCur);
                }, DeskKit.Detail, cur == "go" ? DrawnUI.Coral : DrawnUI.Blue, 460f);
                y += 40f;
            }
            List<EquipmentItem> eq = Eq(s);
            for (int mi = 0; mi < eq.Count; mi++)
            {
                EquipmentItem m = eq[mi];
                if ((m.Site ?? "") != id) continue;
                any = true;
                string mkey = "m:" + mi;
                string mcur = decisions.ContainsKey(mkey) ? decisions[mkey] : "sell";
                b.L(m.Name ?? "?", DeskKit.XId + 10f, y, DeskKit.Detail, DrawnUI.Ink, 240f);
                string mcaption = mcur == "sell"
                    ? "sell at half — +$" + SimFactory.ResaleValue(m.Id)
                    : string.Format("move → {0} (+${1}, 1 wk offline)",
                        SimDivisions.RoofName(s, mcur.Substring(5)),
                        Gd.RoundToInt(SimDivisions.Pb(s, "machine_shipping")));
                string capturedM = mkey;
                string capturedMc = mcur;
                DeskKit.Word(b, mcaption, DeskKit.XId + 260f, y - 6f, () =>
                {
                    b.Desk["td_" + capturedM] = NextMachineDecision(s, id, capturedMc);
                }, DeskKit.Detail, mcur == "sell" ? DrawnUI.Sage : DrawnUI.Blue, 460f);
                y += 40f;
            }
            if (!any)
            {
                b.L("nothing lives under this roof but the lease and its customers",
                    DeskKit.XId + 10f, y, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 700f);
                y += 40f;
            }
            Dictionary<string, object> q = SimDivisions.CloseQuote(s, id, decisions);
            var lines = new List<DeskKit.TicketLine>();
            foreach (Dictionary<string, object> l in
                (List<Dictionary<string, object>>)q["lines"])
            {
                int amt = Convert.ToInt32(l["amount"], CultureInfo.InvariantCulture);
                lines.Add(new DeskKit.TicketLine
                {
                    Label = (string)l["label"],
                    Value = (amt >= 0 ? "+" : "−") + "$" + DeskWorks.Commas(Math.Abs(amt)),
                    Col = amt >= 0 ? DrawnUI.Sage : DrawnUI.Coral,
                });
            }
            int kept = Convert.ToInt32(q["kept"], CultureInfo.InvariantCulture);
            int lost = Convert.ToInt32(q["lost"], CultureInfo.InvariantCulture);
            lines.Add(new DeskKit.TicketLine
                { Label = "≈" + kept + " transfer (with churn risk)", Value = "kept, fragile" });
            lines.Add(new DeskKit.TicketLine
            {
                Label = "≈" + lost + " lost with the roof",
                Value = "−$" + DeskWorks.Commas(Convert.ToInt32(q["lost_rev_wk"],
                    CultureInfo.InvariantCulture)) + "/wk",
                Col = DrawnUI.Coral,
            });
            lines.Add(new DeskKit.TicketLine
            {
                Label = "bills that die with the roof",
                Value = "+$" + DeskWorks.Commas(Convert.ToInt32(q["freed_wk"],
                    CultureInfo.InvariantCulture)) + "/wk freed",
                Col = DrawnUI.Sage,
            });
            int net = Convert.ToInt32(q["net_now"], CultureInfo.InvariantCulture);
            int payback = Convert.ToInt32(q["payback_wk"], CultureInfo.InvariantCulture);
            int marginWk = Convert.ToInt32(q["site_margin_wk"], CultureInfo.InvariantCulture);
            string foot = payback >= 0
                ? string.Format("{0} loses ${1}/wk — this closing pays back in ≈{2} weeks",
                    site.Name, DeskWorks.Commas(Math.Abs(Gd.Mini(marginWk, 0))), payback)
                : "the roof still earns its keep — closing frees no net money";
            float endY = DeskKit.Ticket(b, DeskKit.XId + 40f, y + 6f, 620f,
                "CLOSING " + (site.Name ?? "?").ToUpper() + " — ONE TOTAL", lines,
                "closing costs, net, today",
                (net >= 0 ? "+" : "−") + "$" + DeskWorks.Commas(Math.Abs(net)), foot,
                net >= 0 ? DrawnUI.Sage : DrawnUI.Coral);
            DeskKit.Arm(b, "close_site_" + id, "CLOSE IT — two-tap",
                string.Format("press again — the roof comes down for {0}${1}",
                    net >= 0 ? "+" : "−", DeskWorks.Commas(Math.Abs(net))),
                700f, y + 40f, () =>
                {
                    SimDivisions.CloseSite(s, id, Decisions(b, s, id));
                    var stale = new List<string>();
                    foreach (string k in b.Desk.Keys)
                        if (k.StartsWith("td_")) stale.Add(k);
                    foreach (string k in stale) b.Desk.Remove(k);
                    b.Desk.Remove("teardown");
                }, 380f);
            DeskKit.Word(b, "or Esc keeps " + site.Name, 700f, y + 94f,
                () => b.Desk.Remove("teardown"), DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 320f);
            DeskKit.Footer(b, string.Format(
                    "severance is always owed · machines sell at half · the lease breaks at {0} weeks of rent",
                    Gd.RoundToInt(SimDivisions.Pb(s, "lease_break_weeks"))),
                "the customers are decided FOR you — some transfer fragile, the rest die with the roof",
                "", Mathf.Max(endY + 6f, 760f), 840f);
        }

        static Dictionary<string, string> Decisions(BinderScreen b, GameState s, string id)
        {
            var outp = new Dictionary<string, string>();
            for (int ei = 0; ei < s.Employees.Count; ei++)
                if ((s.Employees[ei].Site ?? "") == id)
                    outp["e:" + ei] = DS(b, "td_e:" + ei).Length > 0 ? DS(b, "td_e:" + ei) : "go";
            List<EquipmentItem> eq = Eq(s);
            for (int mi = 0; mi < eq.Count; mi++)
                if ((eq[mi].Site ?? "") == id)
                    outp["m:" + mi] = DS(b, "td_m:" + mi).Length > 0 ? DS(b, "td_m:" + mi) : "sell";
            return outp;
        }

        static string NextDecision(GameState s, string closingId, string cur)
        {
            var dests = new List<string> { "" };
            foreach (Site site in s.Sites)
            {
                string sid = site.Id ?? "";
                if (sid != closingId) dests.Add(sid);
            }
            if (cur == "go") return "move:" + dests[0];
            int at = dests.IndexOf(cur.StartsWith("move:") ? cur.Substring(5) : "");
            if (at >= 0 && at < dests.Count - 1) return "move:" + dests[at + 1];
            return "go";
        }

        static string NextMachineDecision(GameState s, string closingId, string cur)
        {
            string nxt = NextDecision(s, closingId, cur == "sell" ? "go" : cur);
            return nxt == "go" ? "sell" : nxt;
        }

        // ─────────── the ghost bin: the open_site door, priced ─────────────

        static void OpenRoofSheet(BinderScreen b, GameState s)
        {
            Dictionary<string, object> q = SimDivisions.QuoteSite(s);
            int pack = Convert.ToInt32(q["pack"], CultureInfo.InvariantCulture);
            b.L("A NEW ROOF — the same door as the written move, priced before you sign",
                230f, 8f, DeskKit.Status, DrawnUI.Ink, 800f);
            string nm = DS(b, "roof_name");
            if (nm.Length == 0) nm = (string)q["name"];
            float y = 64f;
            b.L("the sign over the door:", DeskKit.XId + 10f, y, DeskKit.Detail,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 260f);
            string capturedNm = nm;
            DeskKit.Word(b, nm + "  (another name →)", DeskKit.XId + 280f, y - 6f, () =>
            {
                string[] pool = SimDivisions.NAME_POOL;
                int i = (Array.IndexOf(pool, capturedNm) + 1 + pool.Length) % pool.Length;
                b.Desk["roof_name"] = pool[i];
            }, DeskKit.Status, DrawnUI.Blue, 420f);
            y += 52f;
            var lines = new List<DeskKit.TicketLine>();
            foreach (Dictionary<string, object> pl in SimDivisions.PackLines(pack))
                lines.Add(new DeskKit.TicketLine
                {
                    Label = (string)pl["label"],
                    Value = "$" + DeskWorks.Commas(Convert.ToInt32(pl["amount"],
                        CultureInfo.InvariantCulture)),
                });
            lines.Add(new DeskKit.TicketLine
            {
                Label = "rent, from the first Monday",
                Value = "$" + DeskWorks.Commas(Convert.ToInt32(q["rent_wk"],
                    CultureInfo.InvariantCulture)) + "/wk",
            });
            lines.Add(new DeskKit.TicketLine
            {
                Label = "local wages",
                Value = "×" + Convert.ToDouble(q["wage_mult"], CultureInfo.InvariantCulture)
                    .ToString("F2", CultureInfo.InvariantCulture),
            });
            lines.Add(new DeskKit.TicketLine
                { Label = "demand ramps on its own curve", Value = "≈12 wks" });
            float endY = DeskKit.Ticket(b, DeskKit.XId + 40f, y, 560f,
                "OPENING " + nm.ToUpper(), lines, "the pack, signed today",
                "−$" + DeskWorks.Commas(pack),
                "the price book quoted this at run start — the path was visible before the decision",
                DrawnUI.Coral);
            bool can = s.Cash >= pack && pack <= SimEngine.EraSpendCap(s.Era);
            if (can)
            {
                DeskKit.Arm(b, "open_roof", "OPEN THE ROOF",
                    "press again — $" + DeskWorks.Commas(pack) + " signs now",
                    660f, y + 40f, () =>
                    {
                        SimDivisions.OpenSite(s, capturedNm);
                        b.Desk.Remove("open_roof");
                        b.Desk.Remove("roof_name");
                    }, 380f);
            }
            else
            {
                b.L("the pack refuses: " + (s.Cash < pack
                        ? "$" + DeskWorks.Commas(pack - s.Cash) + " short"
                        : "past what a " + s.Era + " can sign for"),
                    660f, y + 44f, DeskKit.Detail, DrawnUI.Coral, 420f);
            }
            DeskKit.Word(b, "not today", 660f, y + 96f, () =>
            {
                b.Desk.Remove("open_roof");
                b.Desk.Remove("roof_name");
            }, DeskKit.Status, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 200f);
            DeskKit.Footer(b,
                "one op, two doors — the written move and this bin sign the SAME receipt",
                "Esc keeps the money", "", Mathf.Max(endY, 760f), 840f);
        }

        static string Left(string s, int n)
        {
            return s.Length > n ? s.Substring(0, n) : s;
        }

        public static void Handle(BinderScreen b, string id)
        {
            if (id == "leave") b.Desk["mode"] = "";
        }
    }
}
