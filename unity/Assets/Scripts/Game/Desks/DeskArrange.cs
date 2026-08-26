using System.Collections.Generic;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// THE ARRANGE MODE SHELL (twin of desk_arrange.gd; DECISIONS: the works'
    /// WRITE view; mockup 14). Bins + chips + two-press moves + the staged
    /// receipt + two-tap CONFIRM; Esc abandons via the desk-mode pop. Prices
    /// are placeholders — L-DIVWORKS wires the real ops and the price book.
    /// </summary>
    public static class DeskArrange
    {
        const float BinW = 260f, BinH = 190f;

        public static void Draw(BinderScreen b)
        {
            GameState state = b.State;
            DeskKit.Back(b, "back to the works", () =>
            {
                b.Desk["mode"] = "";
                b.Desk.Remove("chip");
                b.Desk.Remove("staged");
            });
            b.L("ARRANGE — press a thing, then press its new home", 220f, 8f,
                DeskKit.Status, DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 760f);
            float y = 64f;
            string[] bins = { "HQ — the roof", "SHARED / HQ" };
            float bx = DeskKit.XId;
            for (int i = 0; i < bins.Length; i++)
            {
                string binName = bins[i];
                DeskKit.Bin(b, bx, y, BinW, BinH, new DeskKit.BinCfg
                {
                    Title = binName,
                    Note = i == 0 ? "everything lives here today"
                        : "what has no single roof — allocated vs direct IS the lesson",
                    OnPress = () => PressBin(b, binName),
                });
                bx += BinW + 22f;
            }
            DeskKit.Bin(b, bx, y, BinW, BinH, new DeskKit.BinCfg
            {
                Ghost = true,
                OnPress = () =>
                {
                    b.Desk["staged_note"] = "a new roof opens through the open_site door — "
                        + "the lease quote, capex and hire pack arrive as one priced receipt";
                },
            });
            y += BinH + 26f;
            b.L("THE PIECES", DeskKit.XId, y, DeskKit.Detail,
                DrawnUI.WithAlpha(DrawnUI.Ink, 0.6f), 300f);
            y += 36f;
            float cx = DeskKit.XId;
            object pickedObj;
            b.Desk.TryGetValue("chip", out pickedObj);
            string picked = pickedObj != null ? pickedObj.ToString() : "";
            for (int i = 0; i < state.Employees.Count; i++)
            {
                string nm = state.Employees[i].Name ?? "someone";
                cx = DeskKit.ChipToken(b, cx, y, new DeskKit.ChipCfg
                {
                    Text = nm, Kind = "person", Selected = picked == nm,
                    OnPress = () => PressChip(b, nm),
                });
                if (cx > 900f) { cx = DeskKit.XId; y += 46f; }
            }
            if (state.Employees.Count == 0)
                b.L("nobody on payroll yet — chips appear as the company does",
                    DeskKit.XId, y, DeskKit.Detail, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f),
                    700f);
            y += 52f;
            cx = DeskKit.XId;
            string[] levers = { "sales", "care", "rnd", "office" };
            for (int i = 0; i < levers.Length; i++)
            {
                string ln = levers[i];
                cx = DeskKit.ChipToken(b, cx, y, new DeskKit.ChipCfg
                {
                    Text = ln + " $" + b.Budget(ln) + "/wk", Kind = "spend",
                    Selected = picked == ln,
                    OnPress = () => PressChip(b, ln),
                });
            }
            y += 58f;
            b.L("bound to their objects (never move by hand): rent → its roof · "
                + "serving costs → their offer · interest → its note", DeskKit.XId, y,
                DeskKit.Law, DrawnUI.WithAlpha(DrawnUI.Ink, 0.5f), 1080f);
            y += 44f;
            object stagedObj;
            b.Desk.TryGetValue("staged", out stagedObj);
            var staged = stagedObj as List<string[]>;
            if (staged == null || staged.Count == 0)
            {
                object noteObj;
                b.Desk.TryGetValue("staged_note", out noteObj);
                if (noteObj != null)
                    b.L(noteObj.ToString(), DeskKit.XId, y, DeskKit.Detail, DrawnUI.Blue,
                        1080f);
                DeskKit.Footer(b,
                    "ink is free · brick is priced · obligations survive removal",
                    "two presses stage a move; nothing is booked until the receipt is confirmed",
                    "");
                return;
            }
            var lines = new List<DeskKit.TicketLine>();
            for (int i = 0; i < staged.Count; i++)
                lines.Add(new DeskKit.TicketLine
                {
                    Label = staged[i][0] + " → " + staged[i][1],
                    Value = "$400 now · 1 wk ramp", Col = DrawnUI.Coral,
                });
            float endY = DeskKit.Ticket(b, DeskKit.XId, y, 560f,
                "the staged change — nothing is booked yet", lines,
                "the price of the move", "$" + (staged.Count * 400) + " now",
                "placeholder pricing — the price book wires in with the divisions lane");
            DeskKit.Arm(b, "arrange_confirm", "CONFIRM the change",
                "press again — $" + (staged.Count * 400) + " books now", 620f, y + 30f,
                () =>
                {
                    b.Desk.Remove("staged");
                    b.Desk["staged_note"] =
                        "the shell confirmed — the real ops land with L-DIVWORKS";
                }, 360f);
            DeskKit.Word(b, "tear it up", 620f, y + 84f, () => b.Desk.Remove("staged"),
                DeskKit.Status, DrawnUI.WithAlpha(DrawnUI.Ink, 0.7f), 240f);
            DeskKit.Footer(b,
                "Esc abandons the whole staged change — the mode pop is the abandon", "",
                "", Mathf.Max(endY, 700f));
        }

        static void PressChip(BinderScreen b, string nm)
        {
            object cur;
            b.Desk.TryGetValue("chip", out cur);
            if (cur != null && cur.ToString() == nm) b.Desk.Remove("chip");
            else b.Desk["chip"] = nm;
        }

        static void PressBin(BinderScreen b, string binName)
        {
            object cur;
            b.Desk.TryGetValue("chip", out cur);
            if (cur == null || cur.ToString().Length == 0) return;
            object stagedObj;
            b.Desk.TryGetValue("staged", out stagedObj);
            var staged = stagedObj as List<string[]> ?? new List<string[]>();
            staged.Add(new[] { cur.ToString(), binName });
            b.Desk["staged"] = staged;
            b.Desk.Remove("chip");
        }

        public static void Handle(BinderScreen b, string id)
        {
            if (id == "leave") b.Desk["mode"] = "";
        }
    }
}
