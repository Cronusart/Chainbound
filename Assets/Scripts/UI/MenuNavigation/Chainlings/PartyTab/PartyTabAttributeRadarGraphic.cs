using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    /// <summary>
    /// Prefab-authored UI radar (pentagon grid + filled data polygon). Configure widths/colors in inspector.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PartyTabAttributeRadarGraphic : MaskableGraphic
    {
        public const int AxisCount = 5;

        [Header("Geometry")]
        [SerializeField] [Min(1f)] private float outerRadius = 72f;
        [Tooltip("Default −90° puts axis index 0 at bottom (SPD), then counter-clockwise: MDEF, MATK, RATK, RDEF.")]
        [SerializeField] private float rotationOffsetDegrees = -90f;
        [SerializeField] [Range(0, 4)] private int gridRingCount = 2;
        [SerializeField] private bool drawSpokes = true;

        [Header("Stroke Widths")]
        [SerializeField] [Min(0.5f)] private float dataStrokeWidth = 2.5f;
        [SerializeField] [Min(0.5f)] private float gridStrokeWidth = 1.25f;

        [Header("Colors")]
        [SerializeField] [Range(0f, 1f)] private float strokeLighten01 = 0.35f;
        [SerializeField] [Range(0f, 1f)] private float fillAlpha = 0.28f;
        [SerializeField] [Range(0f, 1f)] private float gridAlpha = 0.35f;
        [SerializeField] private bool useOverrideColors;
        [SerializeField] private Color overrideStrokeColor = Color.white;
        [SerializeField] private Color overrideFillColor = new Color(1f, 1f, 1f, 0.28f);
        [SerializeField] private Color overrideGridColor = new Color(1f, 1f, 1f, 0.35f);

        [Header("Glow Underlay Mode")]
        [Tooltip("When enabled, draws only a soft expanded data stroke (use a second Graphic behind the crisp radar).")]
        [SerializeField] private bool glowMode;
        [SerializeField] [Min(0.5f)] private float glowStrokeWidth = 8f;
        [SerializeField] [Range(0f, 1f)] private float glowColorAlpha = 0.35f;

        [Header("Draw Layers")]
        [SerializeField] private bool drawGrid = true;
        [SerializeField] private bool drawFill = true;
        [SerializeField] private bool drawDataStroke = true;

        private readonly float[] fills = new float[AxisCount];
        private Color typeBackground = new Color(0.45f, 0.55f, 0.75f, 1f);

        public float OuterRadius => outerRadius;
        public float RotationOffsetDegrees => rotationOffsetDegrees;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void SetTypeBackgroundColor(Color background)
        {
            typeBackground = background;
            useOverrideColors = false;
            SetVerticesDirty();
        }

        public void SetResolvedColors(Color stroke, Color fill, Color grid)
        {
            useOverrideColors = true;
            overrideStrokeColor = stroke;
            overrideFillColor = fill;
            overrideGridColor = grid;
            SetVerticesDirty();
        }

        public void SetFills(float[] fill01)
        {
            for (int i = 0; i < AxisCount; i++)
                fills[i] = fill01 != null && i < fill01.Length ? Mathf.Clamp01(fill01[i]) : 0f;

            SetVerticesDirty();
        }

        public void SetOuterRadius(float radius)
        {
            radius = Mathf.Max(1f, radius);
            if (Mathf.Approximately(outerRadius, radius))
                return;

            outerRadius = radius;
            SetVerticesDirty();
        }

        public void SetFillAt(int index, float fill01)
        {
            if (index < 0 || index >= AxisCount)
                return;

            fills[index] = Mathf.Clamp01(fill01);
            SetVerticesDirty();
        }

        public Vector2 GetAxisLocalPosition(int axisIndex, float radiusScale = 1f)
        {
            float radius = outerRadius * Mathf.Clamp01(radiusScale);
            return ComputeVertex(axisIndex, radius);
        }

        public Vector2 GetAxisPoint(int axisIndex, float radius)
        {
            return ComputeVertex(axisIndex, Mathf.Max(0f, radius));
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Color stroke = ResolveStrokeColor();
            Color fill = ResolveFillColor(stroke);
            Color grid = ResolveGridColor(stroke);

            if (glowMode)
            {
                DrawClosedStroke(vh, fills, outerRadius, glowStrokeWidth, WithAlpha(stroke, glowColorAlpha));
                return;
            }

            if (drawGrid)
            {
                int rings = Mathf.Max(0, gridRingCount);
                for (int r = 1; r <= rings; r++)
                {
                    float t = r / (float)(rings + 1);
                    DrawClosedStroke(vh, null, outerRadius * t, gridStrokeWidth, grid);
                }

                DrawClosedStroke(vh, null, outerRadius, gridStrokeWidth, grid);

                if (drawSpokes)
                {
                    for (int i = 0; i < AxisCount; i++)
                        DrawSegment(vh, Vector2.zero, ComputeVertex(i, outerRadius), gridStrokeWidth, grid);
                }
            }

            if (drawFill)
                DrawFilledPolygon(vh, fills, outerRadius, fill);

            if (drawDataStroke)
                DrawClosedStroke(vh, fills, outerRadius, dataStrokeWidth, stroke);
        }

        private Color ResolveStrokeColor()
        {
            if (useOverrideColors)
                return overrideStrokeColor;

            Color c = Color.Lerp(typeBackground, Color.white, Mathf.Clamp01(strokeLighten01));
            c.a = 1f;
            return c;
        }

        private Color ResolveFillColor(Color stroke)
        {
            if (useOverrideColors)
                return overrideFillColor;

            Color c = stroke;
            c.a = Mathf.Clamp01(fillAlpha);
            return c;
        }

        private Color ResolveGridColor(Color stroke)
        {
            if (useOverrideColors)
                return overrideGridColor;

            Color c = Color.Lerp(typeBackground, Color.white, Mathf.Clamp01(strokeLighten01 * 0.65f));
            c.a = Mathf.Clamp01(gridAlpha);
            return c;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private Vector2 ComputeVertex(int axisIndex, float radius)
        {
            float step = 360f / AxisCount;
            float angle = (rotationOffsetDegrees + axisIndex * step) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private void DrawFilledPolygon(VertexHelper vh, float[] axisFills, float radius, Color color)
        {
            int start = vh.currentVertCount;
            AddVert(vh, Vector2.zero, color);

            for (int i = 0; i < AxisCount; i++)
            {
                float t = axisFills != null && i < axisFills.Length ? Mathf.Clamp01(axisFills[i]) : 1f;
                AddVert(vh, ComputeVertex(i, radius * t), color);
            }

            for (int i = 0; i < AxisCount; i++)
            {
                int next = i + 1 < AxisCount ? i + 2 : 1;
                vh.AddTriangle(start, start + i + 1, start + next);
            }
        }

        private void DrawClosedStroke(
            VertexHelper vh,
            float[] axisFills,
            float radius,
            float width,
            Color color)
        {
            for (int i = 0; i < AxisCount; i++)
            {
                int next = (i + 1) % AxisCount;
                float t0 = axisFills != null ? Mathf.Clamp01(axisFills[i]) : 1f;
                float t1 = axisFills != null ? Mathf.Clamp01(axisFills[next]) : 1f;
                Vector2 a = ComputeVertex(i, radius * t0);
                Vector2 b = ComputeVertex(next, radius * t1);
                DrawSegment(vh, a, b, width, color);
            }
        }

        private static void DrawSegment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 dir = b - a;
            float len = dir.magnitude;
            if (len < 0.001f)
                return;

            Vector2 n = new Vector2(-dir.y, dir.x).normalized * (width * 0.5f);
            int start = vh.currentVertCount;
            AddVert(vh, a - n, color);
            AddVert(vh, a + n, color);
            AddVert(vh, b + n, color);
            AddVert(vh, b - n, color);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddVert(VertexHelper vh, Vector2 pos, Color color)
        {
            var vert = UIVertex.simpleVert;
            vert.position = pos;
            vert.color = color;
            vert.uv0 = Vector2.zero;
            vh.AddVert(vert);
        }
    }
}
