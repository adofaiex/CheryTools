using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CheryTools.KeyViewer.Preview
{
    /// <summary>Pixel-aligned rounded rectangle with a one-pixel vertex-alpha AA fringe.</summary>
    internal sealed class RoundedRectGraphic : MaskableGraphic
    {
        private const float AntiAliasWidth = 1f;
        private float _radius = 8f;
        private float _borderThickness;
        private Color _fill = Color.white;
        private Color _bottomLeft = Color.white;
        private Color _bottomRight = Color.white;
        private Color _topLeft = Color.white;
        private Color _topRight = Color.white;
        private Color _border = Color.clear;
        private bool _rainCurve;
        private Color _rainBase;
        private Color _rainFar;
        private Color _rainRight;
        private bool _rainGradientEnabled;
        private bool _rainHeightGradient;
        private bool _rainHorizontalGradient;
        private int _rainFadeMode;
        private float _rainBottomDistance;
        private float _rainTopDistance;
        private float _rainMaxHeight = 1f;
        private float _rainFadeHeight = 1f;
        private float _rainFadePower = 1f;
        private float _rainGradientHeight = 1f;
        private float _rainGradientPower = 1f;
        // Mesh rebuilding is a hot path for key rain. Keep contour storage on the
        // component instead of allocating one or more Lists for every rebuild.
        private readonly List<Vector2> _outer = new List<Vector2>(128);
        private readonly List<Vector2> _inner = new List<Vector2>(128);
        private readonly List<Vector2> _aaOuter = new List<Vector2>(128);
        private readonly List<Vector2> _innerFringe = new List<Vector2>(128);

        internal void SetStyle(Color fill, Color border, float borderThickness, float radius)
        {
            if (!_rainCurve && _bottomLeft == fill && _bottomRight == fill && _topLeft == fill && _topRight == fill
                && _border == border && Mathf.Approximately(_borderThickness, borderThickness)
                && Mathf.Approximately(_radius, radius)) return;
            _fill = fill;
            _rainCurve = false;
            _bottomLeft = _bottomRight = _topLeft = _topRight = fill;
            _border = border;
            _borderThickness = Mathf.Max(0f, borderThickness);
            _radius = Mathf.Max(0f, radius);
            SetVerticesDirty();
        }

        internal void SetGradientStyle(Color bottomLeft, Color bottomRight, Color topLeft, Color topRight,
            Color border, float borderThickness, float radius)
        {
            if (!_rainCurve && _bottomLeft == bottomLeft && _bottomRight == bottomRight && _topLeft == topLeft && _topRight == topRight
                && _border == border && Mathf.Approximately(_borderThickness, borderThickness)
                && Mathf.Approximately(_radius, radius)) return;
            _fill = bottomLeft;
            _rainCurve = false;
            _bottomLeft = bottomLeft;
            _bottomRight = bottomRight;
            _topLeft = topLeft;
            _topRight = topRight;
            _border = border;
            _borderThickness = Mathf.Max(0f, borderThickness);
            _radius = Mathf.Max(0f, radius);
            SetVerticesDirty();
        }

        internal void SetRainStyle(Color baseColor, Color farColor, Color rightColor,
            bool gradientEnabled, bool heightGradient, bool horizontalGradient, int fadeMode,
            float bottomDistance, float topDistance, float maxHeight,
            float fadeHeight, float fadePower, float gradientHeight, float gradientPower,
            Color border, float borderThickness, float radius)
        {
            if (_rainCurve && _rainBase == baseColor && _rainFar == farColor && _rainRight == rightColor
                && _rainGradientEnabled == gradientEnabled && _rainHeightGradient == heightGradient
                && _rainHorizontalGradient == horizontalGradient && _rainFadeMode == fadeMode
                && Mathf.Approximately(_rainBottomDistance, bottomDistance)
                && Mathf.Approximately(_rainTopDistance, topDistance)
                && Mathf.Approximately(_rainMaxHeight, maxHeight)
                && Mathf.Approximately(_rainFadeHeight, fadeHeight)
                && Mathf.Approximately(_rainFadePower, fadePower)
                && Mathf.Approximately(_rainGradientHeight, gradientHeight)
                && Mathf.Approximately(_rainGradientPower, gradientPower)
                && _border == border && Mathf.Approximately(_borderThickness, borderThickness)
                && Mathf.Approximately(_radius, radius)) return;

            _rainCurve = true;
            _rainBase = baseColor;
            _rainFar = farColor;
            _rainRight = rightColor;
            _rainGradientEnabled = gradientEnabled;
            _rainHeightGradient = heightGradient;
            _rainHorizontalGradient = horizontalGradient;
            _rainFadeMode = fadeMode;
            _rainBottomDistance = Mathf.Max(0f, bottomDistance);
            _rainTopDistance = Mathf.Max(_rainBottomDistance, topDistance);
            _rainMaxHeight = Mathf.Max(1f, maxHeight);
            _rainFadeHeight = Mathf.Clamp(fadeHeight, 0.05f, 3f);
            _rainFadePower = Mathf.Clamp(fadePower, 0.1f, 5f);
            _rainGradientHeight = Mathf.Clamp(gradientHeight, 0.05f, 3f);
            _rainGradientPower = Mathf.Clamp(gradientPower, 0.1f, 5f);
            _border = border;
            _borderThickness = Mathf.Max(0f, borderThickness);
            _radius = Mathf.Max(0f, radius);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;
            float radius = Mathf.Min(_radius, Mathf.Min(rect.width, rect.height) * 0.5f);
            int segments = radius > 0.01f ? Mathf.Clamp(Mathf.CeilToInt(Mathf.PI * 0.5f * radius), 4, 32) : 1;
            List<Vector2> outer = BuildContour(rect, radius, segments, _outer);
            float border = Mathf.Min(_borderThickness, Mathf.Min(rect.width, rect.height) * 0.5f);
            bool hasBorder = border > 0.01f && _border.a > 0.001f;
            if (hasBorder)
            {
                Rect innerRect = new Rect(rect.x + border, rect.y + border,
                    Mathf.Max(0f, rect.width - border * 2f), Mathf.Max(0f, rect.height - border * 2f));
                float innerRadius = Mathf.Max(0f, radius - border);
                List<Vector2> inner = BuildContour(innerRect, innerRadius, segments, _inner);

                // 1. 内部填充核心（底色填至标称内边缘）
                AddFill(vh, inner, innerRect, innerRadius, rect);

                // 2. 实心边框环（inner 至 outer）
                AddRing(vh, inner, outer, _border, _border);

                // 3. 内边缘 AA 羽化环（在 inner 处向内部淡出 1 像素，平滑覆盖底色以消除 1-bit 硬阶梯断崖）
                if (innerRadius > 0.5f && innerRect.width > AntiAliasWidth * 2f + 0.01f && innerRect.height > AntiAliasWidth * 2f + 0.01f)
                {
                    float fringeRadius = Mathf.Max(0f, innerRadius - AntiAliasWidth);
                    Rect fringeRect = new Rect(innerRect.x + AntiAliasWidth, innerRect.y + AntiAliasWidth,
                        innerRect.width - AntiAliasWidth * 2f, innerRect.height - AntiAliasWidth * 2f);
                    List<Vector2> innerFringe = BuildContour(fringeRect, fringeRadius, segments, _innerFringe);
                    Color clearBorder = _border;
                    clearBorder.a = 0f;
                    AddRing(vh, innerFringe, inner, clearBorder, _border);
                }

                // 4. 外边缘 AA 羽化环（outer 处向外淡出 1 像素至透明）
                if (radius > 0.01f)
                {
                    Rect expanded = new Rect(rect.x - AntiAliasWidth, rect.y - AntiAliasWidth,
                        rect.width + AntiAliasWidth * 2f, rect.height + AntiAliasWidth * 2f);
                    List<Vector2> outerAA = BuildContour(expanded, radius + AntiAliasWidth, segments, _aaOuter);
                    Color clearBorder = _border;
                    clearBorder.a = 0f;
                    AddRing(vh, outer, outerAA, _border, clearBorder);
                }
            }
            else
            {
                AddFill(vh, outer, rect, radius, rect);
                if (radius > 0.01f)
                {
                    Rect expanded = new Rect(rect.x - AntiAliasWidth, rect.y - AntiAliasWidth,
                        rect.width + AntiAliasWidth * 2f, rect.height + AntiAliasWidth * 2f);
                    List<Vector2> outerAA = BuildContour(expanded, radius + AntiAliasWidth, segments, _aaOuter);
                    int count = Mathf.Min(outer.Count, outerAA.Count);
                    int innerStart = vh.currentVertCount;
                    for (int i = 0; i < count; i++) AddVertex(vh, outer[i], FillColor(outer[i], rect));
                    int outerStart = vh.currentVertCount;
                    for (int i = 0; i < count; i++)
                    {
                        Color c = FillColor(outerAA[i], rect);
                        c.a = 0f;
                        AddVertex(vh, outerAA[i], c);
                    }
                    AddRingTriangles(vh, count, innerStart, outerStart);
                }
            }
        }

        private void AddFill(VertexHelper vh, List<Vector2> contour, Rect geometryRect, float radius, Rect gradientRect)
        {
            bool needsCurveSampling = _rainCurve
                && ((_rainFadeMode == 1
                        && (!Mathf.Approximately(_rainFadeHeight, 1f) || !Mathf.Approximately(_rainFadePower, 1f)))
                    || (_rainGradientEnabled && _rainHeightGradient
                        && (!Mathf.Approximately(_rainGradientHeight, 1f) || !Mathf.Approximately(_rainGradientPower, 1f))));
            if (needsCurveSampling)
            {
                AddRainFill(vh, geometryRect, radius, gradientRect);
                return;
            }
            AddFilledCenter(vh, contour, geometryRect.center, gradientRect);
        }

        private void AddRainFill(VertexHelper vh, Rect geometryRect, float radius, Rect gradientRect)
        {
            // A center fan only samples a non-linear height curve at the two ends
            // and at the center.  That creates the large triangular bands visible
            // in the broken key-rain gradient.  Sample the curve in horizontal
            // strips, like the old CT rain renderer did.
            // The strips in the straight middle can be far apart, but the corners must be sampled
            // along their arc or they turn into straight chamfers that look like pointed tips.
            float cornerRadius = Mathf.Min(radius, Mathf.Min(geometryRect.width, geometryRect.height) * 0.5f);
            int cornerSteps = cornerRadius > 0.01f ? Mathf.Clamp(Mathf.CeilToInt(Mathf.PI * 0.5f * cornerRadius / 1.5f), 2, 24) : 0;
            float middleBottom = geometryRect.yMin + cornerRadius;
            float middleTop = geometryRect.yMax - cornerRadius;
            int strips = Mathf.Clamp(Mathf.CeilToInt((middleTop - middleBottom) / 12f), 1, 64);
            int previousLeft = -1;
            int previousRight = -1;
            for (int i = 0; i <= cornerSteps; i++)
            {
                float angle = cornerSteps == 0 ? 0f : i / (float)cornerSteps * Mathf.PI * 0.5f;
                AddRainStrip(vh, geometryRect, radius, gradientRect, middleBottom - Mathf.Cos(angle) * cornerRadius, ref previousLeft, ref previousRight);
            }
            for (int i = 1; i <= strips; i++)
                AddRainStrip(vh, geometryRect, radius, gradientRect, Mathf.Lerp(middleBottom, middleTop, i / (float)strips), ref previousLeft, ref previousRight);
            for (int i = 1; i <= cornerSteps; i++)
            {
                float angle = i / (float)cornerSteps * Mathf.PI * 0.5f;
                AddRainStrip(vh, geometryRect, radius, gradientRect, middleTop + Mathf.Sin(angle) * cornerRadius, ref previousLeft, ref previousRight);
            }
        }

        private void AddRainStrip(VertexHelper vh, Rect geometryRect, float radius, Rect gradientRect, float y, ref int previousLeft, ref int previousRight)
        {
            float inset = RoundedInset(geometryRect, radius, y);
            float left = geometryRect.xMin + inset;
            float right = geometryRect.xMax - inset;
            if (right < left) right = left = geometryRect.center.x;

            int leftIndex = AddVertex(vh, new Vector2(left, y), FillColor(new Vector2(left, y), gradientRect));
            int rightIndex = AddVertex(vh, new Vector2(right, y), FillColor(new Vector2(right, y), gradientRect));
            if (previousLeft >= 0)
            {
                vh.AddTriangle(previousLeft, previousRight, rightIndex);
                vh.AddTriangle(previousLeft, rightIndex, leftIndex);
            }
            previousLeft = leftIndex;
            previousRight = rightIndex;
        }

        private static float RoundedInset(Rect rect, float radius, float y)
        {
            if (radius <= 0.01f) return 0f;
            float inset = 0f;
            float bottomCenter = rect.yMin + radius;
            if (y < bottomCenter)
            {
                float dy = y - bottomCenter;
                inset = Mathf.Max(inset, radius - Mathf.Sqrt(Mathf.Max(0f, radius * radius - dy * dy)));
            }
            float topCenter = rect.yMax - radius;
            if (y > topCenter)
            {
                float dy = y - topCenter;
                inset = Mathf.Max(inset, radius - Mathf.Sqrt(Mathf.Max(0f, radius * radius - dy * dy)));
            }
            return Mathf.Min(inset, rect.width * 0.5f);
        }

        private void AddFilledCenter(VertexHelper vh, List<Vector2> contour, Vector2 center, Rect gradientRect)
        {
            int centerIndex = AddVertex(vh, center, FillColor(center, gradientRect));
            int start = vh.currentVertCount;
            for (int i = 0; i < contour.Count; i++) AddVertex(vh, contour[i], FillColor(contour[i], gradientRect));
            for (int i = 0; i < contour.Count; i++)
                vh.AddTriangle(centerIndex, start + i, start + ((i + 1) % contour.Count));
        }

        private static void AddRing(VertexHelper vh, List<Vector2> inner, List<Vector2> outer, Color innerColor, Color outerColor)
        {
            int count = Mathf.Min(inner.Count, outer.Count);
            if (count < 3) return;
            int innerStart = vh.currentVertCount;
            for (int i = 0; i < count; i++) AddVertex(vh, inner[i], innerColor);
            int outerStart = vh.currentVertCount;
            for (int i = 0; i < count; i++) AddVertex(vh, outer[i], outerColor);
            AddRingTriangles(vh, count, innerStart, outerStart);
        }

        private static void AddRingTriangles(VertexHelper vh, int count, int innerStart, int outerStart)
        {
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                vh.AddTriangle(innerStart + i, outerStart + i, outerStart + next);
                vh.AddTriangle(innerStart + i, outerStart + next, innerStart + next);
            }
        }

        private Color FillColor(Vector2 position, Rect rect)
        {
            float x = rect.width <= 0.001f ? 0.5f : Mathf.Clamp01((position.x - rect.xMin) / rect.width);
            float y = rect.height <= 0.001f ? 0.5f : Mathf.Clamp01((position.y - rect.yMin) / rect.height);
            if (_rainCurve) return RainColor(x, y);
            return Color.Lerp(Color.Lerp(_bottomLeft, _bottomRight, x), Color.Lerp(_topLeft, _topRight, x), y);
        }

        private Color RainColor(float x, float y)
        {
            float distance = Mathf.Lerp(_rainBottomDistance, _rainTopDistance, y);
            Color value;
            if (_rainGradientEnabled)
            {
                float gradientT = _rainHeightGradient
                    ? Mathf.Pow(Mathf.Clamp01(distance / Mathf.Max(1f, _rainMaxHeight * _rainGradientHeight)), _rainGradientPower)
                    : Mathf.Clamp01(y);
                value = Color.Lerp(_rainBase, _rainFar, gradientT);
            }
            else
            {
                value = _rainBase;
            }

            if (_rainHorizontalGradient)
            {
                Color horizontal = _rainRight;
                horizontal.a *= value.a;
                Color right = _rainGradientEnabled ? Color.Lerp(value, horizontal, 0.5f) : horizontal;
                value = Color.Lerp(value, right, Mathf.Clamp01(x));
            }

            if (_rainFadeMode == 1)
            {
                float fadeDistance = Mathf.Max(1f, _rainMaxHeight * _rainFadeHeight);
                float alpha = Mathf.Pow(1f - Mathf.Clamp01(distance / fadeDistance), _rainFadePower);
                value.a *= alpha;
            }
            return value;
        }

        private static List<Vector2> BuildContour(Rect rect, float radius, int segments, List<Vector2> points)
        {
            radius = Mathf.Min(Mathf.Max(0f, radius), Mathf.Min(rect.width, rect.height) * 0.5f);
            points.Clear();
            AddCorner(points, rect.xMax - radius, rect.yMax - radius, radius, 0f, 90f, segments);
            AddCorner(points, rect.xMin + radius, rect.yMax - radius, radius, 90f, 180f, segments);
            AddCorner(points, rect.xMin + radius, rect.yMin + radius, radius, 180f, 270f, segments);
            AddCorner(points, rect.xMax - radius, rect.yMin + radius, radius, 270f, 360f, segments);
            return points;
        }

        private static int AddVertex(VertexHelper vh, Vector2 position, Color color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vh.AddVert(vertex);
            return vh.currentVertCount - 1;
        }

        private static void AddCorner(List<Vector2> points, float centerX, float centerY, float radius,
            float start, float end, int segments)
        {
            if (radius <= 0.001f)
            {
                for (int i = 0; i <= segments; i++)
                {
                    points.Add(new Vector2(centerX, centerY));
                }
                return;
            }
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(start, end, i / (float)segments) * Mathf.Deg2Rad;
                points.Add(new Vector2(centerX + Mathf.Cos(angle) * radius, centerY + Mathf.Sin(angle) * radius));
            }
        }
    }
}
