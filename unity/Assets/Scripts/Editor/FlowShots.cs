using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;
using Runway.Game;
using RunwayBuild = Runway.Build;

namespace Runway.EditorTools
{
    /// <summary>
    /// D-FLOW EVIDENCE. Four measured defects, shot against the SHIPPING code — the
    /// probe raises the real `ShelfScroll`, calls the real `BirthScreen.PlaceType`
    /// and the real `DiceRoll.Veil`/`VeilAt`, so a number here is a number the game
    /// produces, not one this file recomputes.
    ///
    ///   1 · the shelf thumb, drawn at three scroll positions — its top must land
    ///       inside the track it hangs beside, not at the top of the page
    ///   2 · the birth logotype — its top must sit on StageHeight * 0.12
    ///   3 · the dice backdrop — the page must still be READABLE under the veil
    ///   4 · the sheet ledger — every basename Resources/Sheets will hold, unique,
    ///       with what the twenty cup films cost imported
    ///
    ///   RUNWAY_DFLOW_OUT=&lt;dir&gt; /Applications/.../Unity -batchmode -quit \
    ///     -projectPath unity -executeMethod Runway.EditorTools.FlowShots.Shoot
    ///
    /// NOT -nographics: this renders.
    /// </summary>
    public static class FlowShots
    {
        const int W = 1536;
        const int H = 1024;

        static GameObject _root;
        static Camera _cam;
        static RectTransform _stage;
        static bool _failed;
        static StringBuilder _log;

        public static void Shoot()
        {
            string dir = Environment.GetEnvironmentVariable("RUNWAY_DFLOW_OUT");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Path.GetTempPath(), "d-flow");
            Directory.CreateDirectory(dir);
            _log = new StringBuilder();
            Say("D-FLOW evidence -> " + dir);
            Say("stage " + W + "x" + H + " · device " + SystemInfo.graphicsDeviceType);

            try
            {
                ShelfShots(dir);
                BirthShot(dir);
                DiceShots(dir);
                SheetLedger();
            }
            catch (Exception e)
            {
                _failed = true;
                Say("THREW: " + e);
            }
            finally
            {
                Teardown();
            }

            Say(_failed ? "RESULT: FAILED" : "RESULT: every assertion held");
            try { File.WriteAllText(Path.Combine(dir, "measurements.txt"), _log.ToString()); }
            catch (Exception) { }
            EditorApplication.Exit(_failed ? 1 : 0);
        }

        // ══ 1 · the shelf thumb ════════════════════════════════════════════════

        /// The draft page's own numbers, transcribed from DraftBagPage.BuildShelf.
        const float ViewX = 52f, ViewY = 228f, ViewW = 660f, ViewH = 484f;
        const float TrackX = 710f, TrackY = 232f, TrackW = 3f, TrackH = 476f;
        const float ThumbX = 708f;
        const float ContentH = 1132f;      // a full bag: four buckets, nine rows

        static void ShelfShots(string dir)
        {
            Say("");
            Say("── 1 · THE SHELF THUMB ──────────────────────────────────────────");
            Say("Godot _ShelfBar: bar at (704, 232) size (14, 476); track and thumb");
            Say("drawn on the bar's local x=7, so ABSOLUTE x 711, thumb stroke 6px.");
            Say("y = trackTop + 4 + (trackH - 8 - th) * frac.");

            Stage(DrawnUI.Hex("22262B"));
            GameUi.PaperSheet(_stage, 44f, 166f, 676f, 566f, 2, 4f, null, "shelf");
            Caption(_stage, "everything you own", 78f, 178f, 28f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.62f));

            var viewport = DrawnUI.Rect(_stage, "shelfview", ViewX, ViewY, ViewW, ViewH);
            var grid = DrawnUI.Rect(viewport, "shelfgrid", 0f, 0f, ViewW, ContentH);
            for (int i = 0; i < 9; i++)
            {
                float y = 10f + i * 124f;
                Caption(grid, "row " + (i + 1), 12f, y - 4f, 22f,
                        DrawnUI.WithAlpha(DrawnUI.Sage, 0.95f));
                for (int c = 0; c < 5; c++)
                    DrawnUI.Fill(grid, "tile", DrawnUI.WithAlpha(DrawnUI.Ink, c % 2 == 0 ? 0.16f : 0.10f),
                                 12f + c * 122f, y + 22f, 112f, 88f);
            }

            var track = DrawnUI.Fill(_stage, "track", DrawnUI.WithAlpha(DrawnUI.Ink, 0.15f),
                                     TrackX, TrackY, TrackW, TrackH);
            track.raycastTarget = false;
            var thumbRt = DrawnUI.Rect(_stage, "thumb", ThumbX, TrackY, 7f, 60f);
            var thumb = thumbRt.gameObject.AddComponent<Image>();
            thumb.color = DrawnUI.WithAlpha(DrawnUI.Coral, 0.85f);
            thumb.raycastTarget = false;

            ShelfScroll s = ShelfScroll.Attach(viewport, grid, ViewH, ContentH, thumb);
            Say("resolved: trackTop " + s.TrackTop.ToString("0.0")
                + " · trackH " + s.TrackHeight.ToString("0.0")
                + " · maxScroll " + s.MaxScroll.ToString("0.0")
                + "   (must be 232.0 / 476.0 / 648.0)");
            Assert("track resolved from the page, not from the viewport",
                   Near(s.TrackTop, TrackY, 0.5f) && Near(s.TrackHeight, TrackH, 0.5f));

            float th = thumbRt.sizeDelta.y;
            float lo = TrackY + 4f;
            float hi = TrackY + TrackH - 4f - th;
            Say("thumb " + thumbRt.sizeDelta.x.ToString("0.0") + "x" + th.ToString("0.0")
                + "px · legal top range [" + lo.ToString("0.0") + ", " + hi.ToString("0.0") + "]");
            Assert("thumb stroke is Godot's 6px", Near(thumbRt.sizeDelta.x, 6f, 0.01f));

            float[] at = { 0f, s.MaxScroll * 0.5f, s.MaxScroll };
            string[] names = { "01-shelf-top.png", "02-shelf-middle.png", "03-shelf-bottom.png" };
            float last = float.NegativeInfinity;
            for (int i = 0; i < at.Length; i++)
            {
                s.ScrollTo(at[i]);
                float top = s.ThumbTop;
                float contentTop = DrawnUI.TopLeftY(grid);
                Say("  scroll " + at[i].ToString("0.0").PadLeft(6)
                    + " · thumb top " + top.ToString("0.00").PadLeft(7)
                    + " · thumb bottom " + (top + th).ToString("0.00").PadLeft(7)
                    + " · content top " + contentTop.ToString("0.0").PadLeft(7)
                    + " · enabled " + thumb.enabled);
                Assert("thumb inside the track at scroll " + at[i].ToString("0"),
                       top >= lo - 0.01f && top + th <= TrackY + TrackH - 4f + 0.01f);
                Assert("thumb moved DOWN at scroll " + at[i].ToString("0"), top > last);
                last = top;
                Texture2D shot = Save(Path.Combine(dir, names[i]));
                UnityEngine.Object.DestroyImmediate(shot);
            }

            // THE RESIZE REDRAW: the offset does not move, so only a cleared
            // early-out can put a new thumb on the page. This is the book's bug.
            s.ScrollTo(0f);
            s.SetContentHeight(600f);
            float shortTh = thumbRt.sizeDelta.y;
            s.SetContentHeight(ContentH);
            Say("resize: contentH 1132 -> 600 -> 1132 · thumb height "
                + th.ToString("0.0") + " -> " + shortTh.ToString("0.0")
                + " -> " + thumbRt.sizeDelta.y.ToString("0.0") + " (scroll never moved)");
            Assert("SetContentHeight redraws a thumb the scroll offset cannot",
                   !Near(shortTh, th, 0.5f) && Near(thumbRt.sizeDelta.y, th, 0.5f));

            // ...and the same again from a shelf that started with nothing to scroll,
            // which is exactly how the book opens: placeholder first, entry later.
            var v2 = DrawnUI.Rect(_stage, "bookview", -4000f, 0f, 1080f, 660f);
            var c2 = DrawnUI.Rect(v2, "bookcol", 0f, 0f, 1080f, 660f);
            var t2 = DrawnUI.Fill(_stage, "track", DrawnUI.WithAlpha(DrawnUI.Ink, 0.18f),
                                  -2676f, 182f, 3f, 660f);
            var th2rt = DrawnUI.Rect(_stage, "thumb", -2678f, 182f, 7f, 60f);
            var th2 = th2rt.gameObject.AddComponent<Image>();
            ShelfScroll book = ShelfScroll.Attach(v2, c2, 660f, 660f, th2);
            bool hiddenWhileEmpty = !th2.enabled;
            book.SetContentHeight(2400f);
            Say("book: empty column -> thumb enabled " + (!hiddenWhileEmpty)
                + " · after the entry lands -> enabled " + th2.enabled
                + " at top " + book.ThumbTop.ToString("0.0") + " (track top 182)");
            Assert("the book's thumb appears the moment its entry lands",
                   hiddenWhileEmpty && th2.enabled && Near(book.ThumbTop, 186f, 0.5f));
            Assert("two tracks on one page do not confuse each other",
                   Near(book.TrackHeight, 660f, 0.5f) && Near(t2.rectTransform.rect.height, 660f, 0.5f));
        }

        // ══ 2 · the birth logotype ═════════════════════════════════════════════

        static void BirthShot(string dir)
        {
            Say("");
            Say("── 2 · THE BIRTH LOGOTYPE ───────────────────────────────────────");
            Say("Godot: draw_texture_rect(type, Rect2((w - lw) * 0.5, h * 0.12, lw, lh)).");

            Texture2D tex = TypeTexture();
            if (tex == null)
            {
                _failed = true;
                Say("FAILED: Assets/Art/title/layers/type_main.png would not load");
                return;
            }

            float lw = RunwayPaths.StageWidth * 0.62f;
            float boxX = (RunwayPaths.StageWidth - lw) * 0.5f;
            float boxY = RunwayPaths.StageHeight * 0.12f;
            float boxH = lw * 0.6f;

            Stage(DrawnUI.Cream);
            DrawnUI.FullFill(_stage, "ground", DrawnUI.Cream);
            // the box the fit is given, drawn so the shot shows what was WRONG:
            // the type used to be centred in this, and it is 571px tall
            var box = DrawnUI.Fill(_stage, "box", DrawnUI.WithAlpha(DrawnUI.Sage, 0.10f),
                                   boxX, boxY, lw, boxH);
            box.raycastTarget = false;
            DrawnUI.Fill(_stage, "pin", DrawnUI.WithAlpha(DrawnUI.Coral, 0.9f),
                         0f, boxY, RunwayPaths.StageWidth, 2f);

            var logoRt = DrawnUI.Rect(_stage, "logo", boxX, boxY, lw, lw * 0.42f);
            var logo = logoRt.gameObject.AddComponent<RawImage>();
            logo.texture = tex;
            logo.raycastTarget = false;

            // what the old call produced, measured rather than remembered
            GameUi.Fit(logoRt, tex, boxX, boxY, lw, boxH);
            float before = DrawnUI.TopLeftY(logoRt);
            BirthScreen.PlaceType(logoRt, tex, boxX, boxY, lw, boxH);
            float after = DrawnUI.TopLeftY(logoRt);

            Say("type " + tex.width + "x" + tex.height
                + " · box (" + boxX.ToString("0.00") + ", " + boxY.ToString("0.00")
                + ") " + lw.ToString("0.00") + "x" + boxH.ToString("0.00"));
            Say("drawn " + logoRt.sizeDelta.x.ToString("0.00") + "x"
                + logoRt.sizeDelta.y.ToString("0.00")
                + " · top BEFORE (Fit, centred) " + before.ToString("0.00")
                + " · AFTER (pinned) " + after.ToString("0.00")
                + " · lifted " + (before - after).ToString("0.00") + "px");
            Assert("the type's top sits on StageHeight * 0.12 = 122.88 (123px)",
                   Near(after, boxY, 0.5f));
            Assert("the aspect is the picture's own",
                   Near(logoRt.sizeDelta.y,
                        logoRt.sizeDelta.x * tex.height / tex.width, 0.5f));

            Texture2D shot = Save(Path.Combine(dir, "04-birth-logotype.png"));
            UnityEngine.Object.DestroyImmediate(shot);
        }

        /// THE BYTES OFF DISK, NOT THE IMPORTED ASSET. `ArtCache.Load` decodes the
        /// PNG through UnityWebRequestTexture at its true 980x267; the importer,
        /// left on its defaults, rounds the same file to the nearest power of two
        /// and hands back 1024x256. Measuring the fit against 1024x256 would be
        /// measuring a picture the game never draws.
        static Texture2D TypeTexture()
        {
            const string rel = "Assets/Art/title/layers/type_main.png";
            try
            {
                string abs = Path.Combine(ProjectRoot(), rel);
                if (File.Exists(abs))
                {
                    var made = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(made, File.ReadAllBytes(abs))) return made;
                }
            }
            catch (Exception) { }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(rel);
        }

        // ══ 3 · the dice backdrop ══════════════════════════════════════════════

        /// A patch of bare journal paper — no ink on it, so what it reads is
        /// exactly what the veil did to the page.
        static readonly RectInt Paper = new RectInt(330, 620, 240, 90);

        static void DiceShots(string dir)
        {
            Say("");
            Say("── 3 · THE DICE BACKDROP ────────────────────────────────────────");
            Say("dice_roll.gd: \"the page stays visible and darkens under the cup —");
            Say("no popping felt card\". Live-play #179: \"background looks REALLY bad\".");

            Stage(DrawnUI.Hex("22262B"));
            JournalPage();
            Texture2D bare = Save(Path.Combine(dir, "05-dice-page-bare.png"));
            float open = MeanLuma(bare, Paper);
            UnityEngine.Object.DestroyImmediate(bare);

            Image veil = DiceRoll.Veil(_stage);
            veil.color = DiceRoll.VeilAt(DiceRoll.VeilRise * 0.4f);
            Texture2D rising = Save(Path.Combine(dir, "06-dice-veil-rising.png"));
            float mid = MeanLuma(rising, Paper);
            UnityEngine.Object.DestroyImmediate(rising);

            veil.color = DiceRoll.VeilAt(99f);        // long settled
            Texture2D settled = Save(Path.Combine(dir, "07-dice-veil-settled.png"));
            float under = MeanLuma(settled, Paper);
            UnityEngine.Object.DestroyImmediate(settled);

            // what the old backdrop did, on the same paper, for the same picture
            veil.color = new Color(0.11f, 0.095f, 0.08f, 1f);
            Texture2D blank = Save(Path.Combine(dir, "08-dice-old-blanked.png"));
            float blanked = MeanLuma(blank, Paper);
            UnityEngine.Object.DestroyImmediate(blank);

            Say("veil ceiling " + DiceRoll.VeilCeiling.ToString("0.00")
                + " reached in " + DiceRoll.VeilRise.ToString("0.00") + "s"
                + " · alpha at 0.22s " + DiceRoll.VeilAt(0.22f).a.ToString("0.000")
                + " · at 10s " + DiceRoll.VeilAt(10f).a.ToString("0.000"));
            Say("journal paper " + Paper + " mean luma:");
            Say("  open page          " + open.ToString("0.0"));
            Say("  veil rising (0.22s)" + mid.ToString("0.0").PadLeft(7));
            Say("  veil settled       " + under.ToString("0.0")
                + "   (" + (under / Mathf.Max(open, 1f) * 100f).ToString("0") + "% of the open page)");
            Say("  OLD, opaque        " + blanked.ToString("0.0") + "   — the page was gone");

            Assert("the veil stops at 0.55 and never blanks",
                   Near(DiceRoll.VeilAt(99f).a, 0.55f, 0.001f));
            Assert("the page is still READ under the veil", under > open * 0.35f);
            Assert("the veil is a veil, not a tint", under < open * 0.75f);
            Assert("it is unmistakably brighter than the old backdrop", under > blanked * 2f);
            Assert("the veil only darkens as it rises", mid > under - 0.5f);
            Assert("no vignette disc is built", _stage.Find("felt") == null);
        }

        /// The page the week is written on: a cream sheet, a heading, a coral rule
        /// and the reader's own handwriting. It stands in for the open journal.
        static void JournalPage()
        {
            GameUi.PaperSheet(_stage, 168f, 42f, 1200f, 916f, 3, 4f, null, "sheet");
            Caption(_stage, "NORTHSTAR LABS — a founder's logbook", 220f, 76f, 42f, DrawnUI.Ink);
            Caption(_stage, "week eleven — the week the pilot said yes", 222f, 132f, 26f,
                    DrawnUI.WithAlpha(DrawnUI.Ink, 0.55f));
            DrawnUI.Fill(_stage, "rule", DrawnUI.WithAlpha(DrawnUI.Coral, 0.8f),
                         222f, 178f, 620f, 4f);
            for (int i = 0; i < 7; i++)
                Caption(_stage, "we shipped the thing and then we called them back.",
                        222f, 210f + i * 46f, 30f, DrawnUI.WithAlpha(DrawnUI.Ink, 0.8f));
            // a written line of the player's own, in pen
            Caption(_stage, "I am going to call every one of them myself.",
                        222f, 790f, 34f, DrawnUI.Coral);
        }

        // ══ 4 · the sheet ledger ═══════════════════════════════════════════════

        static void SheetLedger()
        {
            Say("");
            Say("── 4 · THE SHEET LEDGER ─────────────────────────────────────────");
            string[] names = RunwayBuild.StagedSheetNames();
            Array.Sort(names);
            var seen = new System.Collections.Generic.HashSet<string>();
            string clash = "";
            for (int i = 0; i < names.Length; i++)
                if (!seen.Add(names[i])) clash += " " + names[i];
            Say("Resources/Sheets will hold " + names.Length + " basenames: "
                + string.Join(", ", names));
            Assert("SheetLoop's basename lookup has nothing to collide over", clash.Length == 0);
            if (clash.Length > 0) Say("  COLLIDES:" + clash);

            long png = 0L, gpu = 0L;
            int counted = 0, w = 0, h = 0;
            string diceDir = Path.Combine(ProjectRoot(), "Assets/Art/dice");
            for (int i = 1; i <= 20; i++)
            {
                string p = Path.Combine(diceDir, string.Format("roll_{0:00}.png", i));
                if (!File.Exists(p)) { Say("  MISSING " + p); _failed = true; continue; }
                png += new FileInfo(p).Length;
                if (PngSize(p, out w, out h)) { gpu += (long)w * h; counted++; }   // BC7/DXT5 = 8bpp
            }
            Say("dice sheets on disk: " + counted + " files, last read " + w + "x" + h + " with alpha");
            Say("  streamed today (PNG, kept — ReadingBeat.DieSheet still reads it): "
                + Mb(png) + "MB");
            Say("  imported (block-compressed at 8 bits/px):              +" + Mb(gpu) + "MB");
            Say("  app-size delta of this fix:                            +" + Mb(gpu) + "MB");
            Say("  resident at once: ONE sheet (" + Mb((long)w * h)
                + "MB), released by DiceRoll.OnDestroy");
            Assert("all twenty cup films are on disk", counted == 20);
        }

        static bool PngSize(string path, out int w, out int h)
        {
            w = 0; h = 0;
            try
            {
                var head = new byte[24];
                using (var fs = File.OpenRead(path))
                    if (fs.Read(head, 0, 24) < 24) return false;
                w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
                h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
                return w > 0 && h > 0;
            }
            catch (Exception) { return false; }
        }

        static string Mb(long bytes) { return (bytes / 1048576.0).ToString("0.0"); }

        // ══ the kit ════════════════════════════════════════════════════════════

        static void Stage(Color bg)
        {
            Teardown();
            _root = new GameObject("d-flow");
            _root.hideFlags = HideFlags.DontSave;

            var camGo = new GameObject("cam");
            camGo.transform.SetParent(_root.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -10f);
            camGo.transform.localRotation = Quaternion.identity;
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = H * 0.5f;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 100f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = bg;
            _cam.aspect = (float)W / H;

            var canvasGo = new GameObject("canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(_root.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _cam;
            var crt = canvasGo.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(W, H);
            crt.localPosition = Vector3.zero;
            crt.localRotation = Quaternion.identity;
            crt.localScale = Vector3.one;

            _stage = DrawnUI.FullRect(crt, "stage");
        }

        static void Teardown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            _root = null;
            _cam = null;
            _stage = null;
        }

        /// A line of handwriting is not worth failing a shot over: a bare editor
        /// with no TMP essentials throws here and nowhere else.
        static void Caption(RectTransform parent, string text, float x, float y,
                            float size, Color c)
        {
            try { DrawnUI.HandLabel(parent, text, x, y, size, c); }
            catch (Exception) { }
        }

        static Texture2D Save(string path)
        {
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;
            rt.Create();
            RenderTexture prev = RenderTexture.active;
            _cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            _cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new UnityEngine.Rect(0f, 0f, W, H), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prev;
            _cam.targetTexture = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            Say("wrote " + path);
            return tex;
        }

        /// Mean brightness inside a TOP-LEFT box. The readback is stored bottom-up,
        /// as PNG wants, so the row is flipped here.
        static float MeanLuma(Texture2D tex, RectInt box)
        {
            Color32[] px = tex.GetPixels32();
            double sum = 0;
            int n = 0;
            for (int y = box.yMin; y < box.yMax; y++)
            {
                int row = (tex.height - 1 - y) * tex.width;
                for (int x = box.xMin; x < box.xMax; x++)
                {
                    Color32 p = px[row + x];
                    sum += 0.299 * p.r + 0.587 * p.g + 0.114 * p.b;
                    n++;
                }
            }
            return n == 0 ? 0f : (float)(sum / n);
        }

        static bool Near(float a, float b, float eps) { return Mathf.Abs(a - b) <= eps; }

        static void Assert(string what, bool held)
        {
            if (!held) _failed = true;
            Say((held ? "  ok   " : "  FAIL ") + what);
        }

        static void Say(string line)
        {
            Debug.Log("DFLOW: " + line);
            if (_log != null) _log.Append(line).Append('\n');
        }

        static string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }
    }
}
