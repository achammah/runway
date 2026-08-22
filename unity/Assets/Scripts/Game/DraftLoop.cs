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
        const float LoopSeconds = 2f;      // one full breath, however many frames there are

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

        void OnEnable()
        {
            if (_pendingId == null) return;
            string id = _pendingId, still = _pendingStill;
            _pendingId = null;
            _pendingStill = null;
            Play(id, still);
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
            _frames.Clear();
            _shown = -1;
            _t = 0f;
            if (_hydrate != null) { StopCoroutine(_hydrate); _hydrate = null; }
            if (_id.Length == 0) return;

            string first = string.Format("sprites/chr_loop_{0}_01.png", _id);
            ArtCache.Load(first, tex =>
            {
                if (this == null || _target == null) { Debug.Log("DRAFTLOOP cb dead target"); return; }
                Debug.Log("DRAFTLOOP first frame " + (tex != null ? tex.width + "x" + tex.height : "NULL") + " for " + _id);
                if (tex == null)
                {
                    // no loop on disk: the still carries the page
                    if (!string.IsNullOrEmpty(stillSprite))
                        GameUi.Rebind(_target, ArtCache.SpritePath(stillSprite),
                                      _box_x, _box_y, _box_w, _box_h);
                    return;
                }
                _frames.Add(tex);
                Show(0);
                _hydrate = StartCoroutine(Hydrate(_id));
            });
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
