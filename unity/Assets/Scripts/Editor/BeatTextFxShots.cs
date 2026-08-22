using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Game;

namespace Runway.EditorTools
{
    /// <summary>
    /// FILM THE BEAT'S INK, HEADLESS — the D3 lane's own evidence.
    ///
    /// Builds the reading beat's card in an off-screen camera, puts a judgement
    /// paragraph on it, drives `BeatInkSettle.Step` frame by frame with a synthetic
    /// clock (edit mode has no update loop and no coroutines) and writes PNGs at the
    /// moments the effect has to be visible in:
    ///
    ///   01 the first letters landing        02 the die chit stamping in
    ///   03 mid-sentence, letters in the air 04 the verdict at its punch
    ///   05 everything settled               06 a click landing every remaining letter
    ///   07 the same paragraph, lane switched off
    ///
    /// Beside every frame it prints a trace — per character alpha and how far above
    /// its line each one still is — so the film can be checked with a number as well
    /// as with an eye.
    ///
    ///   Unity -batchmode -quit -projectPath unity \
    ///         -executeMethod Runway.EditorTools.BeatTextFxShots.Shoot
    ///
    /// WITHOUT -nographics: the frames come off a real render texture.
    /// RUNWAY_FX_SHOTS names the output folder.
    /// </summary>
    public static class BeatTextFxShots
    {
        const int W = 1536;
        const int H = 1024;
        const float Dt = 1f / 60f;

        const string Headline = "The landlord finds your pitch deck in the recycling.";
        const string Judgement =
            "The die came up 14. Your grit adds +2 — total 16, and this needed 12. "
            + "It lands beautifully.  ·  betting the month";

        public static void Shoot()
        {
            string outDir = Environment.GetEnvironmentVariable("RUNWAY_FX_SHOTS");
            if (string.IsNullOrEmpty(outDir))
                outDir = Path.Combine(Path.GetTempPath(), "runway-d3");
            Directory.CreateDirectory(outDir);

            GameObject root = null;
            RenderTexture rt = null;
            try
            {
                Debug.Log("D3SHOT: out=" + outDir + " fxEnabled=" + ReadingBeatText.Enabled);

                Camera cam;
                RectTransform stage;
                root = Build(out cam, out stage, out rt);

                RectTransform card = Card(stage);
                DrawnUI.HandLabel(card, "WEEK 7", 0f, 40f, 56f, DrawnUI.Ink, 1080f,
                                  TextAlignmentOptions.Top);
                DrawnUI.HandLabel(card, "What happened", 96f, 126f, 26f,
                                  DrawnUI.WithAlpha(DrawnUI.Ink, 0.45f), 888f);
                TextMeshProUGUI top = DrawnUI.HandLabel(card, Headline, 96f, 166f, 34f,
                                                        DrawnUI.Ink, 888f);
                TextMeshProUGUI body = DrawnUI.HandLabel(card, Judgement, 96f, 250f, 34f,
                                                         DrawnUI.Ink, 888f);

                // the block above is already written; the judgement is the one filmed
                top.maxVisibleCharacters = top.text.Length;
                float secs = ReadingBeatText.Apply(body, 1f);
                BeatInkSettle fx = body.GetComponent<BeatInkSettle>();
                if (fx == null) throw new Exception("no BeatInkSettle on the body label");
                Debug.Log(string.Format(
                    "D3SHOT: write={0:0.00}s chars={1} cps={2:0.0} chit={3} verdictFire={4:0.000}",
                    secs, body.text.Length, fx.Pace, fx.ChitChar, fx.FirstVerdict));

                float tChit = fx.ChitChar >= 0
                    ? (fx.ChitChar + 1) / fx.Pace + ReadingBeatText.SettleSecs * 0.60f
                    : 0.70f;
                // the punch is over in 0.22s, so the frame wanted is the FIRST one at
                // or after it fires, not a comfortable moment afterwards
                float tVerdict = fx.FirstVerdict >= 0f ? fx.FirstVerdict + 0.004f : secs * 0.95f;

                float[] marks = { 0.26f, tChit, secs * 0.55f, tVerdict, secs + 0.60f };
                string[] names =
                {
                    "01_first_letters_landing", "02_die_chit_stamps", "03_mid_sentence_in_air",
                    "04_verdict_punch", "05_settled",
                };

                Array.Sort(marks, names);
                var trace = new StringBuilder();
                float t = 0f;
                int next = 0;
                for (int frame = 0; frame < 900 && next < marks.Length; frame++)
                {
                    fx.Step(Dt);
                    t += Dt;
                    while (next < marks.Length && t >= marks[next])
                    {
                        string path = Path.Combine(outDir, names[next] + ".png");
                        Capture(cam, rt, path);
                        trace.AppendLine(Trace(names[next], t, fx, body));
                        next++;
                    }
                }

                // THE CLICK: `ReadingBeat.SkipReading` lands every block by pushing the
                // frontier to the end. A fresh paragraph is caught a third of the way
                // in and shoved — every remaining letter must arrive already settled.
                TextMeshProUGUI skip = DrawnUI.HandLabel(card, Judgement, 96f, 420f, 34f,
                                                         DrawnUI.Ink, 888f);
                ReadingBeatText.Apply(skip, 1f);
                BeatInkSettle sfx = skip.GetComponent<BeatInkSettle>();
                for (int i = 0; i < 24; i++) sfx.Step(Dt);
                int before = sfx.Frontier;
                skip.maxVisibleCharacters = skip.text.Length;      // the beat's own skip
                sfx.Step(Dt);
                Capture(cam, rt, Path.Combine(outDir, "06_click_lands_all.png"));
                trace.AppendLine(Trace("06_click_lands_all", 24 * Dt, sfx, skip)
                                 + "  (frontier before the click=" + before + ")");

                // THE SWITCH: the same paragraph with the lane off is the beat as it
                // ships today — no component, no rewritten sentence, no chit.
                Environment.SetEnvironmentVariable("RUNWAY_FX_TEXT", "0");
                ReadingBeatText.Reread();
                TextMeshProUGUI off = DrawnUI.HandLabel(card, Judgement, 96f, 590f, 34f,
                                                        DrawnUI.Ink, 888f);
                float back = ReadingBeatText.Apply(off, 1.234f);
                bool clean = off.GetComponent<BeatInkSettle>() == null
                             && off.text == Judgement && Mathf.Approximately(back, 1.234f);
                off.maxVisibleCharacters = off.text.Length;
                Capture(cam, rt, Path.Combine(outDir, "07_killswitch_off.png"));
                trace.AppendLine("07_killswitch_off  untouched=" + clean
                                 + "  returned=" + back + "  enabled=" + ReadingBeatText.Enabled);
                Environment.SetEnvironmentVariable("RUNWAY_FX_TEXT", null);
                ReadingBeatText.Reread();

                File.WriteAllText(Path.Combine(outDir, "trace.txt"), trace.ToString());
                Debug.Log("D3SHOT TRACE\n" + trace);
                Debug.Log("D3SHOT: done, " + (marks.Length + 2) + " frames in " + outDir);
            }
            catch (Exception e)
            {
                Debug.LogError("D3SHOT FAILED: " + e);
                EditorApplication.Exit(1);
                return;
            }
            finally
            {
                if (rt != null) { RenderTexture.active = null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // ── the off-screen stage ───────────────────────────────────────────────

        static GameObject Build(out Camera cam, out RectTransform stage, out RenderTexture rt)
        {
            var root = new GameObject("D3Shot");
            root.hideFlags = HideFlags.DontSave;

            rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            rt.Create();

            var camGo = new GameObject("cam", typeof(Camera));
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = H * 0.5f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = DrawnUI.Stage;
            cam.targetTexture = rt;
            cam.aspect = (float)W / H;      // after the target, which resets it

            // WORLD SPACE, NOT SCREEN SPACE: one world unit is one pixel and the
            // canvas rect is the stage, so every Godot coordinate lands where it does
            // in the game and nothing depends on a screen this process does not have.
            var canvasGo = new GameObject("canvas", typeof(Canvas));
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(W, H);
            canvasRt.position = Vector3.zero;
            canvasRt.localScale = Vector3.one;

            stage = DrawnUI.Rect(canvasRt, "stage", 0f, 0f, W, H);
            return root;
        }

        /// The beat's paper, transcribed from `ReadingBeat.Begin` so the frames are
        /// the screen the player sees rather than a text sample on a grey field.
        static RectTransform Card(RectTransform stage)
        {
            DrawnUI.FullFill(stage, "veil", new Color(0.06f, 0.05f, 0.07f, 0.90f));
            RectTransform card = DrawnUI.Rect(stage, "card", 228f, 78f, 1080f, 868f);
            DrawnUI.Fill(card, "shadow", new Color(0f, 0f, 0f, 0.24f), 9f, 11f, 1080f, 868f);
            DrawnUI.Fill(card, "paper", DrawnUI.Cream, 0f, 0f, 1080f, 868f);
            for (float y = 132f; y < 868f - 90f; y += 44f)
                DrawnUI.Fill(card, "rule", DrawnUI.WithAlpha(DrawnUI.Sage, 0.30f),
                             84f, y, 1080f - 168f, 1.5f);
            DrawnUI.AddInkEdge(card, new Vector2(1080f, 868f), new DrawnUI.PaperStyle
            {
                ShadowOffset = Vector2.zero, ShadowAlpha = 0f, Inset = 4f,
                StepsPerEdge = 20, Jitter = 1.7f, Thickness = 3f, Seed = 7,
            });
            return card;
        }

        static void Capture(Camera cam, RenderTexture rt, string path)
        {
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var shot = new Texture2D(W, H, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0f, 0f, W, H), 0, 0);
            shot.Apply();
            File.WriteAllBytes(path, shot.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(shot);
            RenderTexture.active = prev;
        }

        // ── the numbers behind the picture ─────────────────────────────────────

        /// Per character: how inked it is (0-255) and how far it still is above its
        /// line. A frame is mid-settle when several characters are neither 0 nor 255.
        static string Trace(string name, float t, BeatInkSettle fx, TMP_Text label)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("{0}  t={1:0.000}s frontier={2}/{3} inFlight={4} verdict={5:0.000}x chit={6:0.00}",
                            name, t, fx.Frontier, label.textInfo.characterCount, fx.InFlight,
                            fx.PunchNow, fx.ChitInk);
            TMP_TextInfo ti = label.textInfo;
            int shown = 0;
            for (int i = Mathf.Max(fx.Frontier - 12, 0); i < fx.Frontier && shown < 12; i++)
            {
                TMP_CharacterInfo ci = ti.characterInfo[i];
                if (!ci.isVisible) continue;
                int m = ci.materialReferenceIndex;
                if (m < 0 || m >= ti.meshInfo.Length) continue;
                int vi = ci.vertexIndex;
                if (vi + 3 >= ti.meshInfo[m].vertices.Length) continue;
                sb.AppendFormat("  [{0}'{1}' ink={2}/255 above={3:0.00}px]",
                                i, ci.character, ti.meshInfo[m].colors32[vi].a, fx.Above(i));
                shown++;
            }
            return sb.ToString();
        }
    }
}
