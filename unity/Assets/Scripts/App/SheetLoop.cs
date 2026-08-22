using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Runway.App
{
    /// <summary>
    /// THE BAKED LOOP PLAYER. Nothing in this game is drawn faster than 12fps: every
    /// loop, every sway, every die is a baked sheet. Two shapes exist —
    ///
    ///   SHEET    one PNG holding a grid of 1024x576 cells (5x8 = 40 frames for the
    ///            how-to pages, the birth loop and the curtain sway; 5x5 = 24 for the
    ///            birth arrival; 8x5 of 512px for the dice cups)
    ///   SEQUENCE one PNG per frame, streamed (the title's 48-frame film)
    ///
    /// Both are cover-cropped in SOURCE space, exactly as draw_texture_rect_region()
    /// does in Godot: the picture fills the frame, the overflow is cropped, and it is
    /// never stretched and never letterboxed.
    ///
    /// The sequence loads its FIRST frame and shows it, then streams the rest three at
    /// a time while the title breathes — the fix for a launch that was SUPER SLOW when
    /// 48 full-screen frames loaded before the first pixel.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class SheetLoop : MonoBehaviour
    {
        public const float DefaultFps = 12f;
        public const float CellW = 1024f;
        public const float CellH = 576f;

        RawImage _target;
        RectTransform _rt;

        // sheet mode
        Texture2D _sheet;
        int _cols = 5;
        int _frames = 40;
        float _cellW = CellW;
        float _cellH = CellH;

        // sequence mode
        readonly List<Texture2D> _sequence = new List<Texture2D>();
        bool _sequenceMode;

        float _fps = DefaultFps;
        float _t;
        int _shown = -1;
        bool _playing;
        bool _once;
        bool _ownsTextures = true;

        /// Fires when a ONCE-mode player reaches its last frame.
        public event Action Finished;

        /// True once there is something on screen at all.
        public bool HasArt { get { return _sheet != null || _sequence.Count > 0; } }

        public int LoadedFrames { get { return _sequenceMode ? _sequence.Count : _frames; } }

        /// The clock, in seconds since the loop started. Screens that draw on top of
        /// the art (the birth logotype's bob, the curtain's considering line) ride it.
        public float Clock { get { return _t; } }

        void Awake()
        {
            _target = GetComponent<RawImage>();
            _rt = GetComponent<RectTransform>();
            _target.color = Color.white;
            _target.raycastTarget = false;
            // an untextured RawImage paints an opaque WHITE rectangle (P0-F2:
            // six harness shots showed a bright hole where a film should be) —
            // the image stays off until a frame actually lands
            _target.enabled = false;
        }

        // ── sheets ─────────────────────────────────────────────────────────────

        // Args parked while the object was inactive. StartCoroutine on an
        // inactive object is a SILENT no-show, and screens build their pages
        // inactive — that is how every build-time film vanished from player
        // builds while later-started sequences lived (P0-F1). The player is
        // the wrong place to trust callers about activation order; it defers.
        object[] _pendingSheet;
        object[] _pendingSeq;

        void OnEnable()
        {
            if (_pendingSheet != null)
            {
                var a = _pendingSheet; _pendingSheet = null;
                PlaySheet((string)a[0], (int)a[1], (int)a[2], (float)a[3],
                          (bool)a[4], (float)a[5], (float)a[6]);
            }
            else if (_pendingSeq != null)
            {
                var a = _pendingSeq; _pendingSeq = null;
                PlaySequence((string)a[0], (int)a[1], (float)a[2], (bool)a[3]);
            }
        }

        /// Play a grid sheet. `relativeArtPath` is "title/birth_loop.png" and friends.
        /// Missing file: nothing happens and HasArt stays false, so the caller's drawn
        /// fallback carries the screen — the Godot contract, unchanged.
        public Coroutine PlaySheet(string relativeArtPath, int cols, int frames,
                                   float fps = DefaultFps, bool once = false,
                                   float cellW = CellW, float cellH = CellH)
        {
            if (!isActiveAndEnabled)
            {
                _pendingSheet = new object[] { relativeArtPath, cols, frames, fps, once, cellW, cellH };
                _pendingSeq = null;
                return null;
            }
            _sequenceMode = false;
            _cols = Mathf.Max(cols, 1);
            _frames = Mathf.Max(frames, 1);
            _cellW = cellW;
            _cellH = cellH;
            _fps = fps;
            _once = once;
            _t = 0f;
            _shown = -1;
            return StartCoroutine(LoadSheet(relativeArtPath));
        }

        string _inflight;
        float _inflightT0;
        bool _sheetBaked;   // came from Resources: unload, never Destroy

        IEnumerator LoadSheet(string relativeArtPath)
        {
            // THE FILMS ARE IMPORTED, NOT STREAMED. A 16MB sheet PNG cost
            // 2.8-6.6s of runtime decode through UnityWebRequest, and the
            // birth loop's load was KILLED by its own screen moving on.
            // Imported (BC7, GPU-ready) the same sheet arrives in
            // milliseconds and holds a quarter of the VRAM. Godot pays this
            // exact cost at export; Resources/Sheets is the Unity spelling.
            int slash = relativeArtPath.LastIndexOf('/');
            string baseName = relativeArtPath.Substring(slash + 1);
            if (baseName.EndsWith(".png")) baseName = baseName.Substring(0, baseName.Length - 4);
            Texture2D baked = Resources.Load<Texture2D>("Sheets/" + baseName);
            if (baked != null)
            {
                ReleaseSheet();
                _sheet = baked;
                _sheetBaked = true;
                _target.texture = _sheet;
                _target.enabled = true;
                _shown = -1;
                _playing = true;
                Debug.Log("RUNWAY! sheet baked: " + relativeArtPath + " " + baked.width + "x" + baked.height);
                yield break;
            }

            string url = RunwayPaths.ArtUrl(relativeArtPath);
            if (url.Length == 0)
            {
                Debug.Log("RUNWAY! no sheet at " + relativeArtPath + " — drawn fallback stands.");
                yield break;
            }
            _inflight = relativeArtPath;
            _inflightT0 = Time.realtimeSinceStartup;
            Texture2D tex = null;
            yield return LoadTexture(url, t => tex = t);
            _inflight = null;
            if (tex == null)
            {
                Debug.Log("RUNWAY! sheet NULL: " + relativeArtPath + " after "
                          + (Time.realtimeSinceStartup - _inflightT0).ToString("0.0") + "s");
                yield break;
            }
            ReleaseSheet();
            _sheet = tex;
            _sheetBaked = false;
            _target.texture = _sheet;
            _target.enabled = true;
            _shown = -1;
            _playing = true;
            Debug.Log("RUNWAY! sheet up: " + relativeArtPath + " " + tex.width + "x" + tex.height
                      + " in " + (Time.realtimeSinceStartup - _inflightT0).ToString("0.0") + "s");
        }

        // ── sequences ──────────────────────────────────────────────────────────

        /// Play a numbered frame sequence: format is "title/video/frame_{0:00}.png".
        /// Streams from frame 1; the loop plays whatever has landed.
        public Coroutine PlaySequence(string relativeFormat, int maxFrames,
                                      float fps = DefaultFps, bool once = false)
        {
            if (!isActiveAndEnabled)
            {
                _pendingSeq = new object[] { relativeFormat, maxFrames, fps, once };
                _pendingSheet = null;
                return null;
            }
            _sequenceMode = true;
            _fps = fps;
            _once = once;
            _t = 0f;
            _shown = -1;
            return StartCoroutine(StreamSequence(relativeFormat, maxFrames));
        }

        IEnumerator StreamSequence(string relativeFormat, int maxFrames)
        {
            // THE FIRST FRAME NOW, THE REST WHILE THE TITLE BREATHES
            string first = RunwayPaths.ArtUrl(string.Format(relativeFormat, 1));
            if (first.Length == 0) yield break;
            Texture2D t0 = null;
            yield return LoadTexture(first, t => t0 = t);
            if (t0 == null) yield break;
            _sequence.Add(t0);
            _target.texture = t0;
            _target.enabled = true;
            _shown = -1;
            _playing = true;

            var wait = new WaitForSecondsRealtime(0.04f);
            while (_sequence.Count < maxFrames)
            {
                yield return wait;
                for (int n = 0; n < 3 && _sequence.Count < maxFrames; n++)
                {
                    string url = RunwayPaths.ArtUrl(string.Format(relativeFormat, _sequence.Count + 1));
                    if (url.Length == 0) yield break;
                    Texture2D tex = null;
                    yield return LoadTexture(url, t => tex = t);
                    if (tex == null) yield break;
                    _sequence.Add(tex);
                }
            }
        }

        // ── the clock ──────────────────────────────────────────────────────────

        void Update()
        {
            if (!_playing) return;
            _t += Time.unscaledDeltaTime;
            int total = _sequenceMode ? _sequence.Count : _frames;
            if (total <= 0) return;

            int idx = Mathf.FloorToInt(_t * _fps);
            if (_once)
            {
                if (idx >= total)
                {
                    idx = total - 1;
                    if (_shown == idx)
                    {
                        _playing = false;
                        var f = Finished;
                        if (f != null) f();
                        return;
                    }
                }
            }
            else
            {
                idx %= total;
            }
            if (idx == _shown) return;   // ONE REPAINT PER BAKED FRAME
            _shown = idx;
            Apply(idx);
        }

        void Apply(int idx)
        {
            float want = Aspect();
            if (_sequenceMode)
            {
                if (idx < 0 || idx >= _sequence.Count) return;
                Texture2D tex = _sequence[idx];
                _target.texture = tex;
                _target.uvRect = CoverRect(0f, 0f, tex.width, tex.height, tex.width, tex.height, want);
                return;
            }
            if (_sheet == null) return;
            float sx = (idx % _cols) * _cellW;
            float sy = (idx / _cols) * _cellH;
            _target.uvRect = CoverRect(sx, sy, _cellW, _cellH, _sheet.width, _sheet.height, want);
        }

        float Aspect()
        {
            float w = _rt.rect.width;
            float h = _rt.rect.height;
            if (h <= 0.001f) return 1.5f;
            return w / h;
        }

        /// Cover-crop a cell, in the source space, into the target's aspect — then turn
        /// it into a uvRect. Unity's UV origin is BOTTOM-left and the sheets are laid
        /// out top-left, so the row is flipped here and nowhere else.
        public static UnityEngine.Rect CoverRect(float cellX, float cellY, float cellW, float cellH,
                                                 float texW, float texH, float want)
        {
            float sw = cellW;
            float sh = cellH;
            if (sw / sh > want) sw = sh * want;
            else sh = sw / want;
            float x = cellX + (cellW - sw) * 0.5f;
            float yTop = cellY + (cellH - sh) * 0.5f;
            return new UnityEngine.Rect(x / texW, 1f - (yTop + sh) / texH, sw / texW, sh / texH);
        }

        // ── memory ─────────────────────────────────────────────────────────────

        /// THE SHEET IS BORROWED, NOT KEPT. The curtain gives back its 94MB sway the
        /// moment it lifts; the birth screen drops the arrival once it is spent.
        public void Release()
        {
            if (_inflight != null)
            {
                Debug.Log("RUNWAY! sheet load KILLED: " + _inflight + " after "
                          + (Time.realtimeSinceStartup - _inflightT0).ToString("0.0") + "s");
                _inflight = null;
            }
            StopAllCoroutines();   // a load still in flight must not resurrect the sheet
            _playing = false;
            _shown = -1;
            if (_target != null) { _target.texture = null; _target.enabled = false; }
            ReleaseSheet();
            if (_ownsTextures)
            {
                for (int i = 0; i < _sequence.Count; i++)
                    if (_sequence[i] != null) Destroy(_sequence[i]);
            }
            _sequence.Clear();
        }

        void ReleaseSheet()
        {
            if (_sheet != null)
            {
                if (_sheetBaked) Resources.UnloadAsset(_sheet);   // give the VRAM back
                else if (_ownsTextures) Destroy(_sheet);
            }
            _sheet = null;
            _sheetBaked = false;
        }

        void OnDestroy()
        {
            Release();
        }

        /// One texture off disk. Every art file in this port is loaded this way: the
        /// sheets are far too big to import as Sprites, and the film is 48 of them.
        public static IEnumerator LoadTexture(string url, Action<Texture2D> onDone)
        {
            UnityWebRequest req = UnityWebRequestTexture.GetTexture(url, true);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("RUNWAY! texture load failed (" + req.error + "): " + url);
                req.Dispose();
                if (onDone != null) onDone(null);
                yield break;
            }
            Texture2D tex = DownloadHandlerTexture.GetContent(req);
            if (tex != null)
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
            }
            req.Dispose();
            if (onDone != null) onDone(tex);
        }

        /// A RawImage stretched over its parent, with a player already on it.
        public static SheetLoop Attach(RectTransform parent, string name)
        {
            var rt = DrawnUI.FullRect(parent, name);
            rt.gameObject.AddComponent<RawImage>();
            return rt.gameObject.AddComponent<SheetLoop>();
        }

        public static SheetLoop AttachAt(RectTransform parent, string name,
                                         float x, float y, float w, float h)
        {
            var rt = DrawnUI.Rect(parent, name, x, y, w, h);
            rt.gameObject.AddComponent<RawImage>();
            return rt.gameObject.AddComponent<SheetLoop>();
        }
    }
}
