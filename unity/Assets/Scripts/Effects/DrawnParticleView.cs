using UnityEngine;
using UnityEngine.UI;

namespace Runway.Effects
{
    /// <summary>
    /// THE BRIDGE. DrawnParticleSim moves things; this draws them. Once per frame it
    /// steps the sim and rebuilds one quad per live particle straight into the canvas
    /// mesh, so the air belongs to the same drawn stack as the paper it drifts over.
    ///
    /// WHY THE PARTICLES ARE UI GEOMETRY AND NOT A RENDERER. Boot's canvas is
    /// ScreenSpaceOverlay, which paints AFTER every camera — anything drawn by a
    /// Renderer sits behind the whole game, invisible. Drawn into the canvas instead,
    /// the effect obeys sibling order, screen fades (CanvasGroup), masks and the
    /// letterboxed stage rect for free, and costs one draw call to say so.
    ///
    /// ZERO ALLOCATION AFTER WARMUP: the sim's pool is sized once, and the mesh is
    /// built from structs into the VertexHelper the rebuild already owns. Particles
    /// are read through the array slot (`Pool[i].x`), never copied into a local.
    ///
    /// TWO CELLS, ONE SHEET. Each particle picks cell A or cell B off its own seed,
    /// which is fixed for its whole life — that is how one mote in eight is out of
    /// focus and one ember in four is a soft glow, with no second system.
    ///
    /// THE CONE MASK is what makes dust read as dust in a beam rather than dots in a
    /// box: alpha falls away with distance from the beam's centre line, and the beam
    /// widens as it falls. Off unless SetCone is called.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DrawnParticleView : MaskableGraphic
    {
        DrawnParticleSim _sim;
        int _drawn;
        Texture _sheet;

        Vector4 _cellA;          // uMin, vMin, uMax, vMax
        Vector4 _cellB;
        uint _bMask;             // (seed & _bMask) == 0 picks cell B; 0 = never

        bool _cone;
        float _coneCx, _coneYNarrow, _coneYWide, _coneHalfNarrow, _coneHalfWide;

        public DrawnParticleSim Sim { get { return _sim; } }

        /// How many particles were alive at the last step — the number the budget is
        /// checked against.
        public int Live { get { return _sim != null ? _sim.Live : 0; } }

        /// How many quads the last mesh rebuild actually put on the canvas. Live minus
        /// Drawn is what the cone mask and the fades took away.
        public int Drawn { get { return _drawn; } }

        public override Texture mainTexture
        {
            get { return _sheet != null ? _sheet : Texture2D.whiteTexture; }
        }

        /// Diagnosis counters for the evidence pass — how many rebuilds ran and what
        /// the last one saw. Cost: two int writes per rebuild.
        public static int PopulateCalls;
        public static int PopulateLastLive;

        public void Bind(DrawnParticleSim sim, Texture sheet, Vector4 cellA, Vector4 cellB,
                         uint secondaryMask)
        {
            // Reading `canvas` is what fills Graphic's own cache, and
            // SetVerticesDirty() is a silent no-op while that cache is null. Nothing
            // above a canvas can draw at all, so say so once rather than simulating
            // into a void.
            if (canvas == null)
                Debug.LogWarning("RUNWAY! drawn particles mounted with no canvas above "
                                 + "them — the effect will simulate and never draw.");
            _sim = sim;
            _sheet = sheet;
            _cellA = cellA;
            _cellB = cellB;
            _bMask = secondaryMask;
            _drawn = 0;
            raycastTarget = false;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        /// The beam, in this graphic's own local space: `yNarrow` is the lit end
        /// nearest the bulb, `yWide` the floor. Half-widths, not widths.
        public void SetCone(float centreX, float yNarrow, float halfNarrow,
                            float yWide, float halfWide)
        {
            if (halfNarrow <= 0f || Mathf.Abs(yWide - yNarrow) < 1f) { _cone = false; return; }
            _cone = true;
            _coneCx = centreX;
            _coneYNarrow = yNarrow;
            _coneYWide = yWide;
            _coneHalfNarrow = halfNarrow;
            _coneHalfWide = halfWide;
        }

        void LateUpdate()
        {
            Step(Time.unscaledDeltaTime);
        }

        /// One frame of air. Public because the editor evidence pass drives it by
        /// hand — nothing runs Update in edit mode.
        public void Step(float dt)
        {
            if (_sim == null) return;
            int before = _sim.Live;
            _sim.Step(dt);
            if (_sim.Live == 0 && before == 0 && _drawn == 0) return;   // at rest, free
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            _drawn = 0;
            PopulateCalls++;
            PopulateLastLive = _sim != null ? _sim.Live : -1;
            if (_sim == null || _sim.Live <= 0) return;

            DrawnParticleSim.P[] pool = _sim.Pool;
            int n = _sim.Live;
            for (int i = 0; i < n; i++)
            {
                float size = pool[i].size;
                if (size <= 0.05f) continue;

                Color32 col = pool[i].col;
                if (col.a == 0) continue;

                float px = pool[i].x;
                float py = pool[i].y;

                if (_cone)
                {
                    float k = Mathf.Clamp01((py - _coneYNarrow) / (_coneYWide - _coneYNarrow));
                    float half = Mathf.Lerp(_coneHalfNarrow, _coneHalfWide, k);
                    if (half < 1f) half = 1f;
                    float d = Mathf.Abs(px - _coneCx) / half;
                    if (d >= 1f) continue;
                    if (d > 0.72f)
                        col.a = (byte)Mathf.RoundToInt(col.a * ((1f - d) * (1f / 0.28f)));
                    if (col.a == 0) continue;
                }

                Vector4 cell = (_bMask != 0u && (pool[i].seed & _bMask) == 0u) ? _cellB : _cellA;

                float h = size * 0.5f;
                float rad = pool[i].rot * Mathf.Deg2Rad;
                float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
                float ax = cs * h, ay = sn * h;     // the quad's rotated half-x
                float bx = -sn * h, by = cs * h;    // and its rotated half-y

                vh.AddVert(new Vector3(px - ax - bx, py - ay - by, 0f), col,
                           new Vector2(cell.x, cell.y));
                vh.AddVert(new Vector3(px - ax + bx, py - ay + by, 0f), col,
                           new Vector2(cell.x, cell.w));
                vh.AddVert(new Vector3(px + ax + bx, py + ay + by, 0f), col,
                           new Vector2(cell.z, cell.w));
                vh.AddVert(new Vector3(px + ax - bx, py + ay - by, 0f), col,
                           new Vector2(cell.z, cell.y));

                int b = _drawn * 4;
                vh.AddTriangle(b, b + 1, b + 2);
                vh.AddTriangle(b + 2, b + 3, b);
                _drawn++;
            }
        }
    }
}
