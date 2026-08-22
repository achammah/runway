using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;

namespace Runway.Game
{
    /// <summary>
    /// THE FOUNDER, BREATHING — the per-archetype idle loop the draft plays.
    ///
    /// THE FIRST FRAME LOADS NOW, THE REST LOAD WHILE YOU LOOK. The Godot original
    /// pulled 144 sprites synchronously at boot and spent 1.4s of the launch doing it
    /// ("weird latency between startup and character selection"). Here frame 01 is
    /// requested immediately and shown the moment it lands; a hydrator then adds the
    /// rest a few at a time while the player reads the stat sheet. ArtCache holds them
    /// for the session, so switching back to a founder is free.
    ///
    /// A single still (`chr_arch_<id>.png`) is the fallback when no loop ships, which
    /// is the Godot contract: the page never has a hole in it.
    /// </summary>
    public sealed class DraftLoop : MonoBehaviour
    {
        const int MaxFrames = 36;
        const float LoopSeconds = 1f;      // one breath; 12 reborn frames = Godot's own 12fps

        RawImage _target;
        RectTransform _rt;
        readonly List<Texture2D> _frames = new List<Texture2D>();
        string _id = "";
        float _box_x, _box_y, _box_w, _box_h;
        float _t;
        int _shown = -1;
        Coroutine _hydrate;

        public static DraftLoop Attach(RectTransform parent, string name,
                                       float x, float y, float w, float h)
        {
            var rt = DrawnUI.Rect(parent, name, x, y, w, h);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            img.enabled = false;
            var loop = rt.gameObject.AddComponent<DraftLoop>();
            loop._target = img;
            loop._rt = rt;
            loop._box_x = x;
            loop._box_y = y;
            loop._box_w = w;
            loop._box_h = h;
            return loop;
        }

        string _pendingId, _pendingStill;   // Play() called while the page was
                                            // inactive (P0-F4: the default founder's
                                            // idle never started — StartCoroutine on
                                            // an inactive object is a silent no-show)
        string _still = "";                 // what the current id falls back to

        void OnEnable()
        {
            if (_pendingId != null)
            {
                string id = _pendingId, still = _pendingStill;
                _pendingId = null;
                _pendingStill = null;
                Play(id, still);
                return;
            }
            if (_id.Length == 0) return;
            // AN EMPTY CONE IS ALWAYS WORTH ONE MORE ASK. A loop that holds an id and
            // no frames lost its answer somewhere (its first-frame callback landed on
            // a target that had already been rebuilt, or the fetch failed). Coming
            // back on screen re-asks; the picture is cached by now, so it costs a
            // dictionary lookup and it is the difference between a founder and a hole.
            if (_frames.Count == 0) { Reask(); return; }
            // AND A FOUNDER WHO IS NOT BREATHING is the same bug one step in: the first
            // frame landed while this page was off screen, so the hydrator could not be
            // started from the callback (StartCoroutine on an inactive object is a
            // silent no-show) and the loop has been a still ever since.
            if (_frames.Count == 1 && _hydrate == null) _hydrate = StartCoroutine(Hydrate(_id));
        }

        /// Point the loop at an archetype. Instant when its frames are already cached.
        public void Play(string archetypeId, string stillSprite)
        {
            if (!isActiveAndEnabled)
            {
                Debug.Log("DRAFTLOOP parked " + archetypeId + " (inactive)");
                _pendingId = archetypeId;
                _pendingStill = stillSprite;
                return;
            }
            Debug.Log("DRAFTLOOP play " + archetypeId);
            if (_id == archetypeId && _frames.Count > 0) return;
            _id = archetypeId ?? "";
            _still = stillSprite ?? "";
            _frames.Clear();
            _shown = -1;
            _t = 0f;
            if (_hydrate != null) { StopCoroutine(_hydrate); _hydrate = null; }
            if (_id.Length == 0) return;
            Reask();
        }

        /// Ask for frame 01 of whatever this loop is currently pointed at. URGENT: the
        /// decode queue is paced one per frame and can be 144 loop frames long, so the
        /// face the player is looking at cuts in front of the card sprites.
        void Reask()
        {
            string want = _id;
            string first = string.Format("sprites/chr_loop_{0}_01.png", want);
            ArtCache.Load(first, tex =>
            {
                if (this == null || _target == null)
                {
                    // THE LOOP THAT ASKED IS GONE — a re-select rebuilt the page while
                    // the decode queue was draining. The picture is NOT lost with it:
                    // ArtCache holds it under the same path, so the loop that replaced
                    // this one takes it straight off the cache the moment it plays the
                    // same founder (and OnEnable re-asks if it already tried and missed).
                    Debug.Log("DRAFTLOOP cb dead target for " + want + " (cached="
                              + ArtCache.Known(first) + ", the next loop takes it)");
                    return;
                }
                if (_id != want)
                {
                    // the player moved on mid-flight; this frame belongs to nobody now
                    Debug.Log("DRAFTLOOP cb stale " + want + " (showing " + _id + ")");
                    return;
                }
                Debug.Log("DRAFTLOOP first frame " + (tex != null ? tex.width + "x" + tex.height : "NULL") + " for " + _id);
                if (tex == null)
                {
                    // no loop on disk: the still carries the page
                    if (_still.Length > 0)
                        GameUi.Rebind(_target, ArtCache.SpritePath(_still),
                                      _box_x, _box_y, _box_w, _box_h);
                    return;
                }
                if (_frames.Count == 0) _frames.Add(tex);
                Show(0);
                // ONE HYDRATOR. Two asks for the same first frame answer on the same
                // frame (Play, then OnEnable re-asking), and two hydrators would append
                // the same 36 frames twice into one loop.
                if (_hydrate == null) _hydrate = StartCoroutine(Hydrate(_id));
            }, true);
        }

        IEnumerator Hydrate(string id)
        {
            var wait = new WaitForSecondsRealtime(0.05f);
            int n = 2;
            while (n <= MaxFrames)
            {
                yield return wait;
                if (_id != id) yield break;
                for (int k = 0; k < 6 && n <= MaxFrames; k++, n++)
                {
                    string path = string.Format("sprites/chr_loop_{0}_{1:00}.png", id, n);
                    if (!RunwayPaths.ArtExists(path)) { n = MaxFrames + 1; break; }
                    bool got = false;
                    Texture2D landed = null;
                    ArtCache.Load(path, t => { landed = t; got = true; });
                    while (!got) yield return null;
                    if (_id != id) yield break;
                    if (landed == null) { n = MaxFrames + 1; break; }
                    _frames.Add(landed);
                }
            }
            _hydrate = null;
        }

        void Update()
        {
            if (_frames.Count <= 1) return;
            _t += Time.unscaledDeltaTime;
            float step = LoopSeconds / _frames.Count;
            int idx = Mathf.FloorToInt(_t / Mathf.Max(step, 0.03f)) % _frames.Count;
            if (idx == _shown) return;      // ONE REPAINT PER BAKED FRAME
            Show(idx);
        }

        void Show(int idx)
        {
            if (idx < 0 || idx >= _frames.Count || _target == null) return;
            _shown = idx;
            _target.texture = _frames[idx];
            _target.enabled = true;
            GameUi.Fit(_rt, _frames[idx], _box_x, _box_y, _box_w, _box_h);
        }

        /// The bob and sway the page breathes with, applied by the host each frame.
        public RectTransform Rt { get { return _rt; } }
        public float BoxX { get { return _box_x; } }
        public float BoxY { get { return _box_y; } }
    }
}
