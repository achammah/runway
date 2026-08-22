using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Runway.App;

namespace Runway.Game
{
    /// <summary>
    /// ONE PARAGRAPH, LANDING — the hand behind `ReadingBeatText.Apply`.
    ///
    /// It rides on the label itself (one component per body block) and rewrites the
    /// mesh TMP built, in place, from a copy taken once:
    ///
    ///   * the reveal is its own clock, 40 characters a second, so the pace does not
    ///     depend on who else is writing to `maxVisibleCharacters`;
    ///   * each character falls the last two pixels onto its line and inks up as it
    ///     lands, so five or six letters are always in the air;
    ///   * a verdict word punches to 106% the instant its last letter lands and
    ///     settles back over 0.22s, once;
    ///   * the die that turned the week is stamped into the sentence beside its number;
    ///   * anyone else shoving the frontier forward by more than a few characters is
    ///     read as a SKIP — the beat's own click — and everything lands settled.
    ///
    /// WHY LateUpdate AND NOT A COROUTINE: `ReadingBeat.WriteIn` moves the frontier
    /// from the update phase. LateUpdate is guaranteed to run after every coroutine
    /// and before the canvas draws, so the mesh is regenerated and re-inked in one
    /// place with nothing able to overwrite it afterwards. `Step` takes its own delta
    /// so an editor harness can drive the same code frame by frame with no play mode.
    ///
    /// NOTHING IS ALLOCATED PER FRAME: the vertex copy, the verdict runs and the chit
    /// are all built once in `Install`; every frame after that writes into arrays that
    /// already exist. Character timings are arithmetic, not a table.
    /// </summary>
    public sealed class BeatInkSettle : MonoBehaviour
    {
        const int AllVisible = 99999;    // TMP's own "no limit" for maxVisibleCharacters
        const int RecacheBudget = 4;     // a re-laid-out label re-copies, but not forever

        TMP_Text _t;
        TMP_MeshInfo[] _cache;
        Vector2 _cacheSize;
        int _count;
        float _cps;                      // this paragraph's own pace (see Pace)
        int _self;                       // how far OUR clock has written
        int _lastWrote;                  // what we last put on the label
        float _now;
        bool _skipped;
        bool _spent;
        int _recaches;

        // the verdict runs, split per line so a wrapped verdict still scales squarely
        int[] _runS = new int[0];
        int[] _runE = new int[0];
        float[] _runFire = new float[0];
        Vector3[] _runC = new Vector3[0];
        int _runN;

        // the die chit
        RawImage _chit;
        RectTransform _chitRt;
        Texture2D _chitTex;
        Vector2 _chitHome;
        int _chitChar = -1;

        /// True once every character has landed and the verdict has settled.
        public bool Spent { get { return _spent; } }
        /// How far the reveal has written, in characters.
        public int Frontier { get { return _self; } }
        /// The beat's clock, in seconds since `Install`.
        public float Elapsed { get { return _now; } }
        /// The character the die chit rides on, or -1 when the sentence has no die.
        public int ChitChar { get { return _chit == null ? -1 : _chitChar; } }
        /// When the first verdict in this paragraph punches, or -1 when it has none.
        public float FirstVerdict { get { return _runN > 0 ? _runFire[0] : -1f; } }
        /// <summary>
        /// THIS PARAGRAPH'S PACE, in characters a second.
        ///
        /// Nominally 40 — but the beat's own floor and ceiling on a block's write-in
        /// are older than this effect and they are the reading contract: a very short
        /// line is savoured over 0.3s and a very long one is never allowed to take
        /// more than 6.5s, because the next beat is already on its way. So the pace is
        /// read back out of that envelope, which also keeps this clock and the beat's
        /// own `WriteIn` clock EXACTLY in step — two clocks drifting apart would read
        /// as the reader having clicked.
        /// </summary>
        public float Pace { get { return _cps; } }
        /// What the first verdict is scaled to right now: 1 when it is not punching.
        public float PunchNow
        {
            get { Vector3 c; return _runN > 0 ? Punch(_runS[0], out c) : 1f; }
        }

        /// How inked the die chit is right now, 0 to 1.
        public float ChitInk { get { return _chit == null ? 0f : _chit.color.a; } }

        /// How far character `i` still is above where it comes to rest, in pixels.
        /// A settled character reads 0; one still in the air reads up to `SettleDrop`.
        public float Above(int i)
        {
            TMP_TextInfo ti = _t != null ? _t.textInfo : null;
            if (ti == null || _cache == null || i < 0 || i >= ti.characterCount) return 0f;
            TMP_CharacterInfo ci = ti.characterInfo[i];
            if (!ci.isVisible) return 0f;
            int m = ci.materialReferenceIndex;
            if (m < 0 || m >= ti.meshInfo.Length || m >= _cache.Length) return 0f;
            int vi = ci.vertexIndex;
            if (vi < 0 || vi >= ti.meshInfo[m].vertices.Length || vi >= _cache[m].vertices.Length)
                return 0f;
            return ti.meshInfo[m].vertices[vi].y - _cache[m].vertices[vi].y;
        }

        /// How many characters are mid-landing right now.
        public int InFlight
        {
            get
            {
                if (_skipped || _count == 0) return 0;
                int first = Mathf.CeilToInt((_now - ReadingBeatText.SettleSecs) * _cps) - 1;
                return Mathf.Clamp(_self - Mathf.Max(first, 0), 0, _count);
            }
        }

        // ── install ────────────────────────────────────────────────────────────

        /// Take the label. Called from `ReadingBeatText.Apply`, and safe to repeat.
        public void Install(TMP_Text t)
        {
            _t = t;
            _now = 0f;
            _skipped = false;
            _spent = false;
            _recaches = 0;
            _runN = 0;
            enabled = true;
            if (_t == null) { enabled = false; return; }

            int keep = _t.maxVisibleCharacters;
            _t.maxVisibleCharacters = AllVisible;
            _t.ForceMeshUpdate();

            OpenDieGap();

            if (!Recache(AllVisible)) { enabled = false; return; }

            // A LABEL ALREADY HALF-WRITTEN KEEPS ITS PLACE. TMP's own "no limit" means
            // nobody has started, so that reads as "from the top".
            _self = keep >= _count ? 0 : Mathf.Clamp(keep, 0, _count);
            _now = _cps > 0f ? _self / _cps : 0f;
            _t.maxVisibleCharacters = _self;
            _lastWrote = _self;
            _t.ForceMeshUpdate();
            Step(0f);       // the first drawn frame already carries the effect
        }

        void LateUpdate()
        {
            Step(Time.unscaledDeltaTime);
        }

        // ── the frame ──────────────────────────────────────────────────────────

        /// One frame of the hand. `dt` is passed in so the same code runs headless.
        public void Step(float dt)
        {
            if (_t == null || _cache == null) { enabled = false; return; }
            if (_spent) return;
            _now += Mathf.Max(dt, 0f);

            TMP_TextInfo ti = _t.textInfo;
            if (ti == null) return;
            if (ti.characterCount != _count || Resized())
            {
                if (_recaches >= RecacheBudget || !Recache(_self)) { Release(); return; }
                _recaches++;
                ti = _t.textInfo;
            }

            // SOMEBODY LANDED THE BLOCK: the reader clicked, or the beat's own skip
            // ran. Anything past the frontier arrives already settled.
            int ext = _t.maxVisibleCharacters;
            if (ext != _lastWrote && ext >= _self + ReadingBeatText.LeapChars) _skipped = true;

            if (_skipped) _self = _count;
            else
            {
                int want = Mathf.Clamp(Mathf.FloorToInt(_now * _cps), 0, _count);
                if (want > _self) _self = want;
            }
            if (_t.maxVisibleCharacters != _self) _t.maxVisibleCharacters = _self;
            _lastWrote = _self;

            if (_t.havePropertiesChanged) _t.ForceMeshUpdate();
            Paint(_t.textInfo);
            Chit();

            float last = _count > 0 ? Born(_count - 1) : 0f;
            float tail = ReadingBeatText.SettleSecs + ReadingBeatText.VerdictSecs;
            if (_self >= _count && (_skipped || _now >= last + tail)) Release();
        }

        /// The ink is dry: leave the mesh exactly as TMP would have drawn it and stop.
        /// A PARAGRAPH IS NEVER LEFT HALF-WRITTEN — this is also the bail-out when the
        /// label is re-laid-out under us, and the words matter more than the effect.
        void Release()
        {
            if (_t != null && _count > 0 && _t.maxVisibleCharacters < _count)
                _t.maxVisibleCharacters = _count;
            _spent = true;
            enabled = false;
        }

        bool Resized()
        {
            if (_t == null) return false;
            Vector2 s = _t.rectTransform.rect.size;
            return Mathf.Abs(s.x - _cacheSize.x) > 0.5f || Mathf.Abs(s.y - _cacheSize.y) > 0.5f;
        }

        // ── the mesh ───────────────────────────────────────────────────────────

        void Paint(TMP_TextInfo ti)
        {
            if (ti == null || _cache == null) return;
            int f = Mathf.Min(_self, ti.characterCount);
            for (int i = 0; i < f; i++)
            {
                TMP_CharacterInfo ci = ti.characterInfo[i];
                if (!ci.isVisible) continue;
                int m = ci.materialReferenceIndex;
                if (m < 0 || m >= ti.meshInfo.Length || m >= _cache.Length) continue;

                Vector3[] src = _cache[m].vertices;
                Color32[] srcC = _cache[m].colors32;
                Vector3[] dst = ti.meshInfo[m].vertices;
                Color32[] dstC = ti.meshInfo[m].colors32;
                int vi = ci.vertexIndex;
                if (src == null || dst == null) continue;
                if (vi < 0 || vi + 3 >= src.Length || vi + 3 >= dst.Length) continue;

                float e = Landed(i);
                float dy = ReadingBeatText.SettleDrop * (1f - e);
                Vector3 centre;
                float s = Punch(i, out centre);

                for (int k = 0; k < 4; k++)
                {
                    Vector3 p = src[vi + k];
                    if (s != 1f) p = centre + (p - centre) * s;
                    p.y += dy;
                    dst[vi + k] = p;
                    if (srcC == null || dstC == null) continue;
                    if (vi + k >= srcC.Length || vi + k >= dstC.Length) continue;
                    Color32 col = srcC[vi + k];
                    col.a = (byte)(col.a * e);
                    dstC[vi + k] = col;
                }
            }
            _t.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }

        /// 0 the instant a character is revealed, 1 once it has finished landing.
        float Landed(int i)
        {
            if (_skipped) return 1f;
            // CHARACTER i IS DRAWN WHEN THE FRONTIER PASSES IT, at (i+1)/cps — not at
            // i/cps. Starting its fall a step early would have it appear halfway down
            // and half inked, which is the pop this whole layer exists to remove.
            float age = _now - Born(i);
            return DrawnUI.EaseOutCubic(Mathf.Clamp01(age / ReadingBeatText.SettleSecs));
        }

        float Born(int i) { return (i + 1) / _cps; }

        /// The verdict's one-time punch for this character, and what it scales about.
        float Punch(int i, out Vector3 centre)
        {
            centre = Vector3.zero;
            if (_skipped) return 1f;
            for (int r = 0; r < _runN; r++)
            {
                if (i < _runS[r] || i > _runE[r]) continue;
                float u = (_now - _runFire[r]) / ReadingBeatText.VerdictSecs;
                if (u < 0f || u >= 1f) return 1f;
                centre = _runC[r];
                return 1f + (ReadingBeatText.VerdictScale - 1f) * (1f - DrawnUI.EaseOutCubic(u));
            }
            return 1f;
        }

        bool Recache(int restoreTo)
        {
            if (_t == null) return false;
            _t.maxVisibleCharacters = AllVisible;
            _t.ForceMeshUpdate();
            TMP_TextInfo ti = _t.textInfo;
            if (ti == null || ti.characterCount <= 0) return false;
            _cache = ti.CopyMeshInfoVertexData();
            _count = ti.characterCount;
            _cps = _count / Mathf.Max(ReadingBeatText.Pace(_count), 0.001f);
            _cacheSize = _t.rectTransform.rect.size;
            // WHILE EVERYTHING IS STILL LAID OUT: a culled character has no geometry
            // and no `isVisible`, so the verdict's box can only be measured here.
            FindVerdicts();
            _t.maxVisibleCharacters = Mathf.Clamp(restoreTo, 0, _count);
            _lastWrote = _t.maxVisibleCharacters;
            _t.ForceMeshUpdate();
            return _cache != null && _cache.Length > 0;
        }

        // ── the verdict ────────────────────────────────────────────────────────

        /// Every verdict in this paragraph, cut at line ends so a wrapped verdict
        /// scales about its own line rather than about the gap between two.
        void FindVerdicts()
        {
            _runN = 0;
            TMP_TextInfo ti = _t != null ? _t.textInfo : null;
            if (ti == null || _cache == null) return;
            Func<int, char> read = At;

            int i = 0;
            while (i < _count)
            {
                int n = ReadingBeatText.VerdictAt(read, _count, i);
                if (n <= 0) { i++; continue; }
                // THE WORD LANDS, THEN IT SETTLES: the punch waits for the last letter
                // to be fully inked, so what settles is a word and not a word being
                // written. It is one movement the eye can read, once, in 0.22s.
                float fire = Born(i + n - 1) + ReadingBeatText.SettleSecs;
                int seg = i;
                for (int k = i; k < i + n; k++)
                {
                    bool last = k == i + n - 1;
                    bool breaks = !last &&
                        ti.characterInfo[k + 1].lineNumber != ti.characterInfo[seg].lineNumber;
                    if (!last && !breaks) continue;
                    AddRun(seg, k, fire, ti);
                    seg = k + 1;
                }
                i += n;
            }
        }

        char At(int i)
        {
            TMP_TextInfo ti = _t != null ? _t.textInfo : null;
            if (ti == null || i < 0 || i >= ti.characterInfo.Length) return '\0';
            return ti.characterInfo[i].character;
        }

        void AddRun(int from, int to, float fire, TMP_TextInfo ti)
        {
            if (to < from) return;
            Vector3 lo = new Vector3(float.MaxValue, float.MaxValue, 0f);
            Vector3 hi = new Vector3(float.MinValue, float.MinValue, 0f);
            bool any = false;
            for (int i = from; i <= to; i++)
            {
                TMP_CharacterInfo ci = ti.characterInfo[i];
                if (!ci.isVisible) continue;
                int m = ci.materialReferenceIndex;
                if (m < 0 || m >= _cache.Length || _cache[m].vertices == null) continue;
                int vi = ci.vertexIndex;
                if (vi < 0 || vi + 3 >= _cache[m].vertices.Length) continue;
                for (int k = 0; k < 4; k++)
                {
                    Vector3 p = _cache[m].vertices[vi + k];
                    lo.x = Mathf.Min(lo.x, p.x); lo.y = Mathf.Min(lo.y, p.y);
                    hi.x = Mathf.Max(hi.x, p.x); hi.y = Mathf.Max(hi.y, p.y);
                    any = true;
                }
            }
            if (!any) return;
            if (_runN >= _runS.Length) Grow();
            _runS[_runN] = from;
            _runE[_runN] = to;
            _runFire[_runN] = fire;
            _runC[_runN] = new Vector3((lo.x + hi.x) * 0.5f, (lo.y + hi.y) * 0.5f, 0f);
            _runN++;
        }

        void Grow()
        {
            int n = Mathf.Max(_runS.Length * 2, 4);
            Array.Resize(ref _runS, n);
            Array.Resize(ref _runE, n);
            Array.Resize(ref _runFire, n);
            Array.Resize(ref _runC, n);
        }

        // ── the die chit ───────────────────────────────────────────────────────

        /// Open a gap after the number the cup settled on and hang the die in it.
        /// Every failure is silent and total: the sentence goes back to what it was.
        void OpenDieGap()
        {
            if (_chit != null || _t == null) return;
            string s = _t.text;
            int after;
            int roll = ReadingBeatText.FindDie(s, out after);
            if (roll <= 0) return;
            string sheet = ReadingBeatText.DieSheet(roll);
            if (sheet.Length == 0) return;

            TMP_TextInfo ti = _t.textInfo;
            if (ti == null || ti.characterCount <= 0) return;
            int lines = ti.lineCount;
            float spaceW = SpaceWidth(ti);
            if (spaceW <= 0.5f) return;

            float h = _t.fontSize * ReadingBeatText.DieHeightEm;
            float w = h * (ReadingBeatText.DieCropW / ReadingBeatText.DieCropH);
            int n = Mathf.Clamp(Mathf.CeilToInt((w + _t.fontSize * ReadingBeatText.DieRoomEm) / spaceW),
                                2, ReadingBeatText.GapMaxChars);

            _t.text = s.Substring(0, after) + new string(ReadingBeatText.GapChar, n) + s.Substring(after);
            _t.ForceMeshUpdate();
            ti = _t.textInfo;

            // THE CHIT MAY NOT STRADDLE A LINE, AND THE PARAGRAPH MAY NOT GROW ONE:
            // the block's height was measured before this gap existed, and a taller
            // paragraph would print into the beat below it.
            bool ok = ti != null && ti.lineCount == lines && after >= 1
                      && after + n < ti.characterCount
                      && ti.characterInfo[after - 1].lineNumber == ti.characterInfo[after + n].lineNumber;
            if (!ok)
            {
                _t.text = s;
                _t.ForceMeshUpdate();
                return;
            }

            float left = ti.characterInfo[after].origin;
            float right = ti.characterInfo[after + n - 1].xAdvance;
            float baseline = ti.characterInfo[after + n - 1].baseLine;
            _chitHome = new Vector2((left + right) * 0.5f - w * 0.5f,
                                    baseline + h - _t.fontSize * ReadingBeatText.DieSitEm);
            _chitChar = after + n - 1;

            _chitRt = DrawnUI.Rect(_t.rectTransform, "die", 0f, 0f, w, h);
            _chitRt.anchoredPosition = _chitHome;
            _chit = _chitRt.gameObject.AddComponent<RawImage>();
            _chit.raycastTarget = false;
            _chit.color = new Color(1f, 1f, 1f, 0f);
            ReadingBeatText.DieTexture(sheet, Dressed);
        }

        void Dressed(Texture2D tex)
        {
            if (this == null || _chit == null || tex == null) return;
            _chitTex = tex;
            _chit.texture = tex;
            _chit.uvRect = ReadingBeatText.DieUv(tex.width, tex.height);
        }

        void Chit()
        {
            if (_chit == null || _chitRt == null || _chitChar < 0) return;
            float e = _chitTex == null ? 0f : Landed(_chitChar);
            _chit.color = new Color(1f, 1f, 1f, e);
            _chitRt.anchoredPosition =
                new Vector2(_chitHome.x, _chitHome.y + ReadingBeatText.SettleDrop * (1f - e));
        }

        static float SpaceWidth(TMP_TextInfo ti)
        {
            for (int i = 0; i < ti.characterCount; i++)
            {
                if (ti.characterInfo[i].character != ' ') continue;
                float w = ti.characterInfo[i].xAdvance - ti.characterInfo[i].origin;
                if (w > 0.5f) return w;
            }
            return 0f;
        }
    }
}
