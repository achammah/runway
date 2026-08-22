using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Runway.App;
using Runway.Core;

namespace Runway.Game
{
    /// <summary>
    /// THE LAST PAGE — autopsy_screen.gd, ported to the minimum that tells the truth.
    ///
    /// ONE sheet of the founder's log book: the title, one line naming how the run
    /// ended, the payout on its own line, then the causal chain as a short column, and
    /// the way onward inked into the bottom corner. Nothing else exists.
    ///
    /// THE GEOMETRY IS THE SHELL'S. The Godot original carried a private copy of the
    /// paper quad, the printed ruling and the layout maths and the rest of the book
    /// drifted away from it; this builds on JournalPage instead, so the sheet, the
    /// rules every baseline lands on and the four zones are defined once.
    ///
    /// A SHORT COLUMN: how it started, then the last moves before the end. The whole
    /// record is in the save; this page is the eulogy, not the archive.
    /// </summary>
    public sealed class AutopsyScreen : AppScreen
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            ScreenRegistry.Register(AppState.Autopsy, typeof(AutopsyScreen));
            ScreenRegistry.Register(AppState.Finale, typeof(AutopsyScreen));
        }

        const int ChainMax = 4;

        protected override void OnBuild()
        {
            // a backstop under the room: if a scene ever fails, the page sits on night
            // rather than on whatever the viewport was last cleared to
            DrawnUI.FullFill(Rect, "night", DrawnUI.Hex("2C3238"), true);

            var pg = JournalPage.Create(Rect);
            pg.Instant = true;              // the last page is already written
            var runner = TurnRunner.Get();
            if (runner != null && runner.ScenePath.Length > 0) pg.BackdropPath = runner.ScenePath;
            pg.Build("THE LAST PAGE");

            string headline = Payload as string ?? "";
            string[] parts = headline.Split('\n');
            if (parts.Length > 0 && parts[0].Length > 0)
                pg.LineFitted(parts[0], pg.RulePitch() * 6f);
            for (int i = 1; i < parts.Length; i++)
                if (parts[i].Trim().Length > 0) pg.Line(parts[i].Trim());

            // the chain: how it started, then the last moves before the end
            RunDriver driver = RunDriver.Current;
            List<string> chain = driver != null && driver.Record != null
                ? driver.Record.CausalLines() : new List<string>();
            if (chain.Count > ChainMax + 1)
            {
                var kept = new List<string> { chain[0] };
                for (int i = chain.Count - ChainMax; i < chain.Count; i++) kept.Add(chain[i]);
                chain = kept;
            }
            for (int i = 0; i < chain.Count; i++)
            {
                if (pg.RoomToFence("ending") < pg.RulePitch() * 1.5f) break;
                pg.Line(chain[i], i > 0 && i < chain.Count - 1, "ending");
            }

            GameState st = driver != null ? driver.State : null;
            if (st != null && pg.RoomToFence("ending") >= pg.RulePitch())
                pg.Line(string.Format("{0} weeks · {1} · {2} customers · v0.{3}",
                    st.Week, st.EraDisplayName(), st.Traction, st.Product), true, "ending");

            // the way onward, inked into the corner
            GameUi.InkWord(Rect, "ONE MORE RUN  →", RunwayPaths.StageWidth - 520f,
                RunwayPaths.StageHeight - 96f, 420f, 60f, 36f, DrawnUI.Coral,
                () => Finish());
        }

        /// THE LAST PAGE IS READ, NOT DISMISSED. The click that ended the run must not
        /// also close its eulogy, so any-key only arms after the page has settled.
        float _age;

        void Update()
        {
            _age += Time.unscaledDeltaTime;
            if (_age > 1.2f && Input.anyKeyDown) Finish();
        }
    }
}
