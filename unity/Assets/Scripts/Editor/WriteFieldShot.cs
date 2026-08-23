using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Runway.App;
using Runway.Game;
using Debug = UnityEngine.Debug;

namespace Runway.EditorTools
{
    /// <summary>
    /// THE JOURNAL'S WRITE FIELD, FOCUSED AND PHOTOGRAPHED — the one part of this
    /// polish pass that cannot be settled in edit mode.
    ///
    /// WHY PLAY MODE. TMP_InputField builds its caret inside `if
    /// (Application.isPlaying)` and nowhere else, and drives it from LateUpdate and a
    /// blink coroutine. An edit-mode probe can build the field, measure it and render
    /// it, and there will be no caret in the picture — not because the field is broken
    /// but because the caret does not exist yet. So this enters play mode, builds a
    /// REAL JournalPage through PageBlocks.WriteField — the same stencil Mask, the same
    /// four-degree lean, the same auto-focus — and photographs it.
    ///
    /// WHAT IT ANSWERS, in measurements as well as pictures:
    ///   · does a caret exist at all, and is its parent the MASKED VIEWPORT
    ///   · does its material carry the stencil comparison, or is it drawing through
    ///     the mask (the two ways "the mask hides the caret" could have been true)
    ///   · is it visible on the first frame the field holds focus, or half a blink late
    ///   · does a selection wash render, and does a REAL triple click raise one
    ///
    ///   RUNWAY_POLISH_OUT=&lt;dir&gt; /Applications/.../Unity -batchmode \
    ///     -projectPath unity -executeMethod Runway.EditorTools.WriteFieldShot.Run
    ///
    /// NO -quit and NO -nographics: -quit would end the process before play mode ever
    /// starts, and this renders. The driver exits the editor itself when it is done.
    /// </summary>
    public static class WriteFieldShot
    {
        const string OutKey = "runway.wfshot.out";

        public static void Run()
        {
            string dir = Environment.GetEnvironmentVariable("RUNWAY_POLISH_OUT");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Path.GetTempPath(), "d-polish");
            Directory.CreateDirectory(dir);
            SessionState.SetString(OutKey, dir);
            Debug.Log("WFSHOT: entering play mode · out " + dir);
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.EnterPlaymode();
        }

        /// Entering play mode reloads the domain, which loses the subscription made in
        /// Run(). This puts it back. Both paths are covered because the option to skip
        /// the reload exists and either one alone would be a hang in batch mode.
        [InitializeOnLoadMethod]
        static void Arm()
        {
            if (string.IsNullOrEmpty(SessionState.GetString(OutKey, ""))) return;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (EditorApplication.isPlaying) Spawn();
        }

        static void OnPlayMode(PlayModeStateChange c)
        {
            if (c == PlayModeStateChange.EnteredPlayMode) Spawn();
        }

        static void Spawn()
        {
            string dir = SessionState.GetString(OutKey, "");
            if (string.IsNullOrEmpty(dir)) return;
            SessionState.EraseString(OutKey);
            var go = new GameObject("wfshot");
            go.AddComponent<WriteFieldDriver>().Dir = dir;
        }
    }

    /// The runtime half: it exists for one play-mode session and then ends it.
    public sealed class WriteFieldDriver : MonoBehaviour
    {
        public string Dir;

        const int W = 1536;
        const int H = 1024;

        Camera _cam;
        RectTransform _canvas;
        RenderTexture _rt;
        StringBuilder _log;
        int _fails;
        bool _done;
        float _t0;

        /// A BATCH EDITOR IN PLAY MODE HAS NO ONE TO CLOSE IT. If anything below waits
        /// on something that never comes, this ends the session anyway rather than
        /// holding the project's lock until a person notices.
        void Update()
        {
            if (!_done && Time.realtimeSinceStartup - _t0 > 120f)
            {
                Fail("watchdog: 120s without finishing");
                Finish();
            }
        }

        IEnumerator Start()
        {
            _t0 = Time.realtimeSinceStartup;
            DontDestroyOnLoad(gameObject);
            _log = new StringBuilder();
            Say("── P-INPUT · THE JOURNAL WRITE FIELD, FOCUSED  (play mode) ──");

            TMP_InputField field = null;
            try
            {
                BuildStage();
                var page = JournalPage.Create(DrawnUI.FullRect(_canvas, "stage"));
                page.Instant = true;                       // no reveal: focus at once
                page.Build("polish");
                page.Line("week 3. the pilot customer wants a discount.", false, "body");
                field = PageBlocks.WriteField(page, "what do you actually do?", "body");
            }
            catch (Exception e) { Fail("build threw: " + e); Finish(); yield break; }

            if (field == null) { Fail("WriteField returned null"); Finish(); yield break; }

            // ── 1 · the caret, on the frame focus lands ────────────────────────
            int waited = 0;
            while (!field.isFocused && waited++ < 30) yield return null;
            Say("  focus            " + (field.isFocused ? "yes, after " + waited
                + " frame(s) — WriteField calls ActivateInputField on an Instant page"
                : "NO — ActivateInputField never took"));
            if (!field.isFocused) Fail("the field never took focus");
            yield return null;
            CaretFacts(field);
            Shoot("caret.png", null);
            Shoot("caret_zoom.png", field.textViewport);

            // ── 2 · a REAL double click on a word ──────────────────────────────
            field.text = "buy the pilot customer lunch and hold the price";
            for (int i = 0; i < 3; i++) yield return null;
            try { DoubleClick(field, 16); }            // the 's' inside "customer"
            catch (Exception e) { Fail("double click threw: " + e); }
            // TMP turns STRING indices into CARET indices, and generates the wash, in
            // OnFillVBO on a geometry rebuild — a capture on the same frame photographs
            // the caret it had before
            for (int i = 0; i < 4; i++) yield return null;
            string word = Selected(field);
            // TMP draws the caret OR the wash, never both: a picture with a caret in it
            // is a picture of hasSelection == false, so the flag is stated beside it
            Say("  double click     → \"" + word + "\"   hasSelection "
                + (field.selectionStringAnchorPosition != field.selectionStringFocusPosition)
                + "   (through TMP's own OnPointerDown, twice inside the 0.5s delay)");
            if (word != "customer") Fail("double click selected \"" + word + "\", not the word");
            Shoot("selection_zoom.png", field.textViewport);

            // ── 3 · a REAL third click, through the event system ───────────────
            field.selectionStringAnchorPosition = 0;
            field.selectionStringFocusPosition = 0;
            yield return null;
            try { TripleClick(field); } catch (Exception e) { Fail("triple click threw: " + e); }
            for (int i = 0; i < 4; i++) yield return null;
            string all = Selected(field);
            Say("  triple click     → \"" + all + "\"   hasSelection "
                + (field.selectionStringAnchorPosition != field.selectionStringFocusPosition));
            if (all != field.text) Fail("triple click did not select the whole entry");
            Shoot("selectall_zoom.png", field.textViewport);

            // ── 4 · what TMP would have done to a first click ──────────────────
            Say("  onFocusSelectAll " + field.onFocusSelectAll
                + "   (TMP ships true, which skips ALL of OnPointerDown's caret work"
                + " on a cold field — no caret placement and no first word select)");
            Say("  caretBlinkRate   " + field.caretBlinkRate.ToString("0.00")
                + " blinks/s   caretWidth " + field.caretWidth);

            Finish();
        }

        // ── what the caret actually is ─────────────────────────────────────────

        void CaretFacts(TMP_InputField f)
        {
            Type t = typeof(TMP_InputField);
            var caretRt = Field<RectTransform>(t, f, "caretRectTrans");
            var cr = Field<CanvasRenderer>(t, f, "m_CachedInputRenderer");
            object vis = Field<object>(t, f, "m_CaretVisible");

            if (caretRt == null)
            {
                Fail("NO CARET OBJECT — TMP builds one only if Application.isPlaying");
                return;
            }
            Transform vp = f.textViewport != null ? f.textViewport.transform : null;
            bool inside = caretRt.parent == vp;
            Say("  caret object     yes, parent \"" + caretRt.parent.name + "\""
                + (inside ? " — the MASKED VIEWPORT, so the stencil applies to it"
                          : " — NOT the viewport"));
            if (!inside) Fail("the caret is not parented to the masked viewport");

            Say("  caret visible    " + (vis == null ? "?" : vis.ToString())
                + "   on the first frame the field held focus");
            if (vis is bool && !(bool)vis) Fail("the caret was not visible on frame one");

            if (cr != null && cr.materialCount > 0)
            {
                Material m = cr.GetMaterial(0);
                int comp = m != null && m.HasProperty("_StencilComp") ? m.GetInt("_StencilComp") : -1;
                int reff = m != null && m.HasProperty("_Stencil") ? m.GetInt("_Stencil") : -1;
                // CompareFunction.Equal is 3, Always is 8. Equal means the stencil the
                // viewport's Mask wrote is being tested, which is the whole question.
                Say("  caret material   \"" + (m == null ? "null" : m.name)
                    + "\"  _StencilComp " + comp + " (" + (comp == 3 ? "Equal — MASKED"
                    : comp == 8 ? "Always — DRAWS THROUGH THE MASK" : "?") + ")"
                    + "  _Stencil " + reff);
                if (comp == 8)
                    Fail("the caret ignores the stencil: TMP's OnEnable forced the plain"
                         + " UI material and no material rebuild followed");
            }

            // and where it is, against the window it must stay inside
            var vpRt = f.textViewport;
            if (vpRt != null)
            {
                Rect a = WorldRect(caretRt), b = WorldRect(vpRt);
                bool held = a.xMin >= b.xMin - 1f && a.xMax <= b.xMax + 1f
                         && a.yMin >= b.yMin - 1f && a.yMax <= b.yMax + 1f;
                Say("  caret rect       " + Say(a) + "   viewport " + Say(b)
                    + "   " + (held ? "inside" : "OVERHANGS"));
            }
        }

        static string Say(Rect r)
        {
            return string.Format("({0:0},{1:0} {2:0}x{3:0})", r.xMin, r.yMin, r.width, r.height);
        }

        static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float x0 = Mathf.Min(c[0].x, c[2].x), x1 = Mathf.Max(c[0].x, c[2].x);
            float y0 = Mathf.Min(c[0].y, c[2].y), y1 = Mathf.Max(c[0].y, c[2].y);
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }

        static T Field<T>(Type t, object o, string name) where T : class
        {
            FieldInfo fi = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            return fi == null ? null : fi.GetValue(o) as T;
        }

        static string Selected(TMP_InputField f)
        {
            int a = Mathf.Min(f.selectionStringAnchorPosition, f.selectionStringFocusPosition);
            int b = Mathf.Max(f.selectionStringAnchorPosition, f.selectionStringFocusPosition);
            string s = f.text ?? "";
            a = Mathf.Clamp(a, 0, s.Length);
            b = Mathf.Clamp(b, a, s.Length);
            return s.Substring(a, b - a);
        }

        /// The third click, sent the way the input module sends one: to the field's
        /// own GameObject, with the run-of-clicks counter the module maintains.
        void TripleClick(TMP_InputField f)
        {
            var e = Click(f, f.textComponent.rectTransform.position);
            e.clickCount = 3;
            ExecuteEvents.Execute(f.gameObject, e, ExecuteEvents.pointerClickHandler);
        }

        /// TWO POINTER-DOWNS AT ONE CHARACTER, in one frame. TMP calls it a double
        /// click when the second lands inside `m_DoubleClickDelay` (0.5s) of the first,
        /// so this exercises the real OnPointerDown both times — including the
        /// `hadFocusBefore || !m_OnFocusSelectAll` gate that used to skip the whole
        /// caret-and-word pass, and TMP_TextUtilities.FindIntersectingWord under it.
        void DoubleClick(TMP_InputField f, int charIndex)
        {
            f.textComponent.ForceMeshUpdate();
            TMP_TextInfo ti = f.textComponent.textInfo;
            if (ti == null || charIndex >= ti.characterCount) { Fail("no such character"); return; }
            TMP_CharacterInfo ci = ti.characterInfo[charIndex];
            Vector3 mid = f.textComponent.transform.TransformPoint(
                (ci.bottomLeft + ci.topRight) * 0.5f);
            var e = Click(f, mid);
            ExecuteEvents.Execute(f.gameObject, e, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(f.gameObject, e, ExecuteEvents.pointerDownHandler);
        }

        /// A pointer event aimed at a WORLD point. `pressEventCamera` is read off the
        /// press raycast's module, so the raycaster has to be filled in or TMP maps the
        /// click through no camera at all and lands on the wrong character.
        PointerEventData Click(TMP_InputField f, Vector3 world)
        {
            if (EventSystem.current == null)
                new GameObject("events", typeof(EventSystem));
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(_cam, world);
            var e = new PointerEventData(EventSystem.current);
            e.button = PointerEventData.InputButton.Left;
            e.position = screen;
            var hit = new RaycastResult
            {
                gameObject = f.gameObject,
                module = _canvas.GetComponent<GraphicRaycaster>(),
                screenPosition = screen,
                worldPosition = world,
            };
            e.pointerPressRaycast = hit;
            e.pointerCurrentRaycast = hit;
            return e;
        }

        // ── the stand-in stage ─────────────────────────────────────────────────

        void BuildStage()
        {
            var camGo = new GameObject("cam");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -10f);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = H * 0.5f;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 100f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = DrawnUI.Hex("2A2620");
            _cam.aspect = (float)W / H;

            // the target texture is attached for the WHOLE run, not just the captures,
            // so screen-space coordinates are the same 1536x1024 in a batch editor as
            // in a window — the pointer events below are aimed with them
            _rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            _rt.antiAliasing = 1;
            _rt.Create();
            _cam.targetTexture = _rt;

            var canvasGo = new GameObject("canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _cam;
            canvasGo.AddComponent<GraphicRaycaster>();
            _canvas = canvasGo.GetComponent<RectTransform>();
            _canvas.sizeDelta = new Vector2(W, H);
            _canvas.localPosition = Vector3.zero;
            _canvas.localRotation = Quaternion.identity;
            _canvas.localScale = Vector3.one;

            if (EventSystem.current == null)
                new GameObject("events", typeof(EventSystem));
        }

        /// The whole stage, or a 3x blow-up of one rect of it — a 2px caret in a 1536
        /// wide picture is a rumour, not evidence.
        void Shoot(string name, RectTransform zoom)
        {
            Canvas.ForceUpdateCanvases();
            _cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var full = new Texture2D(W, H, TextureFormat.RGBA32, false);
            full.ReadPixels(new Rect(0f, 0f, W, H), 0, 0);
            full.Apply(false, false);
            RenderTexture.active = prev;

            Texture2D shot = full;
            if (zoom != null)
            {
                Rect r = ScreenRect(zoom);
                int x0 = Mathf.Clamp(Mathf.FloorToInt(r.xMin) - 24, 0, W - 2);
                int y0 = Mathf.Clamp(Mathf.FloorToInt(r.yMin) - 24, 0, H - 2);
                int cw = Mathf.Clamp(Mathf.CeilToInt(r.width) + 48, 2, W - x0);
                int ch = Mathf.Clamp(Mathf.CeilToInt(r.height) + 48, 2, H - y0);
                Color32[] src = full.GetPixels32();
                const int K = 3;
                var big = new Color32[cw * K * ch * K];
                for (int y = 0; y < ch * K; y++)
                    for (int x = 0; x < cw * K; x++)
                        big[y * cw * K + x] = src[(y0 + y / K) * W + (x0 + x / K)];
                shot = new Texture2D(cw * K, ch * K, TextureFormat.RGBA32, false);
                shot.SetPixels32(big);
                shot.Apply(false, false);
            }
            File.WriteAllBytes(Path.Combine(Dir, name), shot.EncodeToPNG());
            if (shot != full) UnityEngine.Object.DestroyImmediate(shot);
            UnityEngine.Object.DestroyImmediate(full);
            Debug.Log("WFSHOT " + name);
        }

        Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector2 s = RectTransformUtility.WorldToScreenPoint(_cam, c[i]);
                x0 = Mathf.Min(x0, s.x); x1 = Mathf.Max(x1, s.x);
                y0 = Mathf.Min(y0, s.y); y1 = Mathf.Max(y1, s.y);
            }
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }

        // ── the log ────────────────────────────────────────────────────────────

        void Say(string line)
        {
            Debug.Log("WFSHOT: " + line);
            if (_log != null) _log.Append(line).Append('\n');
        }

        void Fail(string why)
        {
            _fails++;
            Debug.LogError("WFSHOT FAIL: " + why);
            if (_log != null) _log.Append("  !! ").Append(why).Append('\n');
        }

        void Finish()
        {
            if (_done) return;
            _done = true;
            try
            {
                Say("");
                File.AppendAllText(Path.Combine(Dir, "measurements.txt"), _log.ToString());
            }
            catch (Exception) { }
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); UnityEngine.Object.DestroyImmediate(_rt); }
            Debug.Log("WFSHOT DONE · fails " + _fails);
            EditorApplication.Exit(_fails == 0 ? 0 : 1);
        }
    }
}
