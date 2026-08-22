using UnityEngine;
using Runway.App;

namespace Runway.Screens
{
    /// <summary>
    /// THE ONE KEY — keys_screen.gd, ported. OpenAI only, and SELL the why: the
    /// first-boot screen where the player hands the game its narrator. One drawn sheet,
    /// the pitch in plain words, one paste line, one button. Written to the user folder
    /// — never the project folder. "play keyless" stays: the authored deck still works.
    /// </summary>
    public sealed class KeysScreen : AppScreen
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            ScreenRegistry.Register(AppState.Keys, typeof(KeysScreen));
            ScreenRegistry.RegisterOverlay(AppOverlay.Keys, typeof(KeysScreen));
        }

        static readonly Color Cream = DrawnUI.Hex("F2EAD3");
        static readonly Color Ink = DrawnUI.Hex("1E1E1E");
        static readonly Color Pen = DrawnUI.Hex("E86A5C");

        const string MascotFormat = "sprites/chr_loop_hacker_{0:00}.png";
        const int MascotFrames = 36;

        PaperInput _openai;

        protected override void OnBuild()
        {
            DrawnUI.FullFill(Rect, "bg", DrawnUI.Hex("22262B"), true);
            DrawnUI.PaperCard(Rect, new Vector2(1140f, 880f), 198f, 60f,
                              DrawnUI.PaperStyle.Sheet, "sheet");

            // the mascot vouches for the ask — ALIVE (owner: a small loop, transparent):
            // the hacker's matted idle frames play at card scale; still art is the fallback
            if (RunwayPaths.ArtExists(string.Format(MascotFormat, 1)))
            {
                var loop = SheetLoop.AttachAt(Rect, "mascot", 1010f, 110f, 270f, 270f);
                loop.PlaySequence(MascotFormat, MascotFrames, 1f / 0.09f);
            }
            else if (RunwayPaths.ArtExists("title/layers/founder.png"))
            {
                var still = SheetLoop.AttachAt(Rect, "mascot", 1010f, 110f, 270f, 270f);
                still.PlaySequence("title/layers/founder.png", 1, 1f);
            }

            DrawnUI.HandLabel(Rect, "ONE KEY MAKES THE WORLD ALIVE", 250f, 128f, 48f, Ink);
            DrawnUI.Rule(Rect, 252f, 204f, 560f, Pen, 4f, 4, 1.5f, 21);

            DrawnUI.HandLabel(Rect,
                "RUNWAY! is a fully generative survival game. There is no script: "
                + "your market, your rivals, your investors, every week's consequences "
                + "and every picture of your office are invented on the spot, for this "
                + "run only. Nobody else will ever play your company.",
                252f, 234f, 30f, DrawnUI.WithAlpha(Ink, 0.85f), 740f);

            DrawnUI.HandLabel(Rect,
                "The narrator behind all of that is OpenAI's model, and it works "
                + "for you, on your own key.",
                252f, 420f, 30f, DrawnUI.WithAlpha(Ink, 0.85f), 740f);

            _openai = PaperInput.Create(Rect, 252f, 528f, 1030f, 112f,
                                        "PASTE YOUR OPENAI API KEY", "sk-…", 28f);
            _openai.Submitted += _ => Save();

            DrawnUI.HandLabel(Rect,
                "· stored only on this machine, in your user folder — never in the game, never sent anywhere but OpenAI",
                256f, 668f, 24f, DrawnUI.WithAlpha(Ink, 0.55f), 1000f);
            DrawnUI.HandLabel(Rect,
                "· a typical evening of play costs about a coffee in API credit",
                256f, 708f, 24f, DrawnUI.WithAlpha(Ink, 0.55f), 1000f);
            DrawnUI.HandLabel(Rect,
                "· get one at platform.openai.com → API keys",
                256f, 748f, 24f, DrawnUI.WithAlpha(Ink, 0.55f), 1000f);

            DrawnUI.FlatButton(Rect, "BRING THE WORLD TO LIFE  →", 690f, 830f, 600f, 70f,
                               38f, Pen, Ink, Save);

            DrawnUI.FlatButton(Rect, "play without — authored world only", 252f, 840f,
                               420f, 52f, 24f, DrawnUI.WithAlpha(Ink, 0.5f), Pen, SkipKeyless);
        }

        void Save()
        {
            string ok = _openai != null ? _openai.Value : "";
            if (ok.Length == 0)
            {
                _openai.SetLabel("PASTE YOUR OPENAI API KEY — it looks like sk-…");
                return;
            }
            // an Atlas key already present in a dev .env keeps working; never asked for
            Env.SaveOpenAiKey(ok);
            if (Boot.Instance != null) Boot.Instance.NotifyKeysChanged();
            Finish();
        }

        void SkipKeyless()
        {
            Env.SaveKeyless();
            if (Boot.Instance != null) Boot.Instance.NotifyKeysChanged();
            Finish();
        }
    }
}
