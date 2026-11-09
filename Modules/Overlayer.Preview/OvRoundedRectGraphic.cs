using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CheryTools.Overlayer.Preview
{
    /// <summary>Small allocation-free rounded rectangle used by OV progress bars.</summary>
    internal sealed class OvRoundedRectGraphic : MaskableGraphic
    {
        private const float AntiAliasWidth = 1f;
        private Color _fill = Color.white;
        private Color _border = Color.clear;
        private float _borderWidth;
        private float _radius;
        private bool _gradient;
        private bool _gradientVertical;
        private Color _gradientStart = Color.white;
        private Color _gradientEnd = Color.white;
        private readonly List<Vector2> _outer = new List<Vector2>(100);
        private readonly List<Vector2> _inner = new List<Vector2>(100);
        private readonly List<Vector2> _aa = new List<Vector2>(100);

        internal void SetStyle(Color fill, Color border, float borderWidth, float radius)
        {
            SetGradientStyle(fill, fill, border, borderWidth, radius, false);
        }

        internal void SetGradientStyle(Color start, Color end, Color border, float borderWidth,
            float radius, bool vertical)
        {
            borderWidth = Mathf.Max(0f, borderWidth);
            radius = Mathf.Max(0f, radius);
            if (_fill == start && _gradientEnd == end && _border == border
                && _gradient == !Approximately(start, end) && _gradientVertical == vertical
                && Mathf.Approximately(_borderWidth, borderWidth) && Mathf.Approximately(_radius, radius)) return;
            _fill = start;
            _gradientStart = start;
            _gradientEnd = end;
            _gradient = !Approximately(start, end);
            _gradientVertical = vertical;
            _border = border;
            _borderWidth = borderWidth;
            _radius = radius;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;
            float radius = Mathf.Min(_radius, Mathf.Min(rect.width, rect.height) * 0.5f);
            int segments = Mathf.Clamp(Mathf.CeilToInt(Mathf.PI * Mathf.Max(1f, radius) / 4f), 3, 24);
            BuildContour(rect, radius, segments, _outer);
            float border = Mathf.Min(_borderWidth, Mathf.Min(rect.width, rect.height) * 0.5f);
            bool hasBorder = border > 0.01f && _border.a > 0.001f;
            if (hasBorder)
            {
                if (rect.width <= border * 2f + 0.01f || rect.height <= border * 2f + 0.01f)
                {
                    AddFan(vh, _outer, _border);
                }
                else
                {
                    Rect innerRect = new Rect(rect.x + border, rect.y + border,
                        Mathf.Max(0f, rect.width - border * 2f), Mathf.Max(0f, rect.height - border * 2f));
                    BuildContour(innerRect, Mathf.Max(0f, radius - border), segments, _inner);
                    if (_fill.a > 0.001f) AddFan(vh, _inner);
                    AddRing(vh, _inner, _outer, _border, _border);
                }
            }
            else if (_fill.a > 0.001f)
            {
                AddFan(vh, _outer);
            }

            if (radius > 0.01f)
            {
                Rect expanded = new Rect(rect.x - AntiAliasWidth, rect.y - AntiAliasWidth,
                    rect.width + AntiAliasWidth * 2f, rect.height + AntiAliasWidth * 2f);
                BuildContour(expanded, radius + AntiAliasWidth, segments, _aa);
                Color solid = hasBorder ? _border : _fill;
                Color clear = solid; clear.a = 0f;
                AddRing(vh, _outer, _aa, solid, clear);
            }
        }

        private void AddFan(VertexHelper vh, List<Vector2> points, Color? colorOverride = null)
        {
            if (points.Count < 3) return;
            Vector2 center = Vector2.zero;
            for (int i = 0; i < points.Count; i++) center += points[i];
            center /= points.Count;
            int centerIndex = vh.currentVertCount;
            AddVertex(vh, center, colorOverride ?? EvaluateColor(center));
            int start = vh.currentVertCount;
            for (int i = 0; i < points.Count; i++)
                AddVertex(vh, points[i], colorOverride ?? EvaluateColor(points[i]));
            for (int i = 0; i < points.Count; i++)
            {
                int next = (i + 1) % points.Count;
                if ((points[i] - points[next]).sqrMagnitude > 0.00001f)
                    vh.AddTriangle(centerIndex, start + i, start + next);
            }
        }

        private Color EvaluateColor(Vector2 point)
        {
            if (!_gradient) return _fill;
            Rect rect = GetPixelAdjustedRect();
            float t = _gradientVertical
                ? Mathf.InverseLerp(rect.yMin, rect.yMax, point.y)
                : Mathf.InverseLerp(rect.xMin, rect.xMax, point.x);
            return Color.Lerp(_gradientStart, _gradientEnd, Mathf.Clamp01(t));
        }

        private static bool Approximately(Color left, Color right)
        {
            return Mathf.Abs(left.r - right.r) < 0.0001f
                && Mathf.Abs(left.g - right.g) < 0.0001f
                && Mathf.Abs(left.b - right.b) < 0.0001f
                && Mathf.Abs(left.a - right.a) < 0.0001f;
        }

        private static void AddRing(VertexHelper vh, List<Vector2> inner, List<Vector2> outer,
            Color innerColor, Color outerColor)
        {
            int count = Mathf.Min(inner.Count, outer.Count);
            if (count < 3) return;
            int innerStart = vh.currentVertCount;
            for (int i = 0; i < count; i++) AddVertex(vh, inner[i], innerColor);
            int outerStart = vh.currentVertCount;
            for (int i = 0; i < count; i++) AddVertex(vh, outer[i], outerColor);
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                if ((outer[i] - outer[next]).sqrMagnitude > 0.00001f && (inner[i] - outer[i]).sqrMagnitude > 0.00001f)
                    vh.AddTriangle(innerStart + i, outerStart + i, outerStart + next);
                if ((inner[i] - inner[next]).sqrMagnitude > 0.00001f && (inner[next] - outer[next]).sqrMagnitude > 0.00001f)
                    vh.AddTriangle(innerStart + i, outerStart + next, innerStart + next);
            }
        }

        private static void BuildContour(Rect rect, float radius, int segments, List<Vector2> points)
        {
            points.Clear();
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);
            AddCorner(points, rect.xMax - radius, rect.yMax - radius, radius, 0f, 90f, segments);
            AddCorner(points, rect.xMin + radius, rect.yMax - radius, radius, 90f, 180f, segments);
            AddCorner(points, rect.xMin + radius, rect.yMin + radius, radius, 180f, 270f, segments);
            AddCorner(points, rect.xMax - radius, rect.yMin + radius, radius, 270f, 360f, segments);
        }

        private static void AddCorner(List<Vector2> points, float cx, float cy, float radius,
            float startDegrees, float endDegrees, int segments)
        {
            if (radius <= 0.001f)
            {
                for (int i = 0; i <= segments; i++)
                    points.Add(new Vector2(cx, cy));
                return;
            }
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(startDegrees, endDegrees, i / (float)segments) * Mathf.Deg2Rad;
                points.Add(new Vector2(cx + Mathf.Cos(angle) * radius, cy + Mathf.Sin(angle) * radius));
            }
        }

        private static void AddVertex(VertexHelper vh, Vector2 position, Color color)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vh.AddVert(vertex);
        }
    }
}
