using UnityEngine;
using UnityEngine.UI;

namespace CheryTools.KeyViewer.Preview
{
    /// <summary>
    /// Single-rectangle Gaussian shadow used by a pooled key-rain segment.
    /// The texture and nine-slice layout intentionally mirror the old CT renderer.
    /// </summary>
    internal sealed class SoftShadowGraphic : MaskableGraphic
    {
        private const int TextureSize = 96;
        private const int TextureBorder = 32;
        private const float TextureSigma = 14f;

        private static Texture2D _shadowTexture;
        private Color _topColor = Color.clear;
        private Color _bottomColor = Color.clear;
        private float _softness = 12f;
        private readonly float[] _xs = new float[4];
        private readonly float[] _ys = new float[4];
        private readonly float[] _us = new float[4];
        private readonly float[] _vs = new float[4];

        public override Texture mainTexture => EnsureShadowTexture();

        internal void SetStyle(Color topColor, Color bottomColor, float softness)
        {
            softness = Mathf.Max(0f, softness);
            if (_topColor == topColor && _bottomColor == bottomColor
                && Mathf.Approximately(_softness, softness)) return;
            _topColor = topColor;
            _bottomColor = bottomColor;
            _softness = softness;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_softness <= 0.01f || (_topColor.a <= 0f && _bottomColor.a <= 0f)) return;

            Rect inner = GetPixelAdjustedRect();
            if (inner.width <= 0f || inner.height <= 0f) return;

            float border = Mathf.Max(1f, _softness);
            float uvBorder = TextureBorder / (float)TextureSize;
            float[] xs = _xs;
            float[] ys = _ys;
            float[] us = _us;
            float[] vs = _vs;
            xs[0] = inner.xMin - border; xs[1] = inner.xMin; xs[2] = inner.xMax; xs[3] = inner.xMax + border;
            ys[0] = inner.yMin - border; ys[1] = inner.yMin; ys[2] = inner.yMax; ys[3] = inner.yMax + border;
            us[0] = 0f; us[1] = uvBorder; us[2] = 1f - uvBorder; us[3] = 1f;
            vs[0] = 0f; vs[1] = uvBorder; vs[2] = 1f - uvBorder; vs[3] = 1f;

            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    Vector2 bottomLeft = new Vector2(xs[x], ys[y]);
                    Vector2 bottomRight = new Vector2(xs[x + 1], ys[y]);
                    Vector2 topRight = new Vector2(xs[x + 1], ys[y + 1]);
                    Vector2 topLeft = new Vector2(xs[x], ys[y + 1]);
                    AddQuad(vh,
                        topLeft, topRight, bottomRight, bottomLeft,
                        new Vector2(us[x], vs[y + 1]), new Vector2(us[x + 1], vs[y + 1]),
                        new Vector2(us[x + 1], vs[y]), new Vector2(us[x], vs[y]),
                        EvaluateColor(topLeft.y, inner), EvaluateColor(topRight.y, inner),
                        EvaluateColor(bottomRight.y, inner), EvaluateColor(bottomLeft.y, inner));
                }
            }
        }

        private Color EvaluateColor(float y, Rect rect)
        {
            float clamped = Mathf.Clamp(y, rect.yMin, rect.yMax);
            float t = rect.height <= 0f ? 1f : Mathf.InverseLerp(rect.yMin, rect.yMax, clamped);
            return Color.Lerp(_bottomColor, _topColor, t);
        }

        private static void AddQuad(VertexHelper vh,
            Vector2 topLeft, Vector2 topRight, Vector2 bottomRight, Vector2 bottomLeft,
            Vector2 uvTopLeft, Vector2 uvTopRight, Vector2 uvBottomRight, Vector2 uvBottomLeft,
            Color colorTopLeft, Color colorTopRight, Color colorBottomRight, Color colorBottomLeft)
        {
            int start = vh.currentVertCount;
            vh.AddVert(topLeft, colorTopLeft, uvTopLeft);
            vh.AddVert(topRight, colorTopRight, uvTopRight);
            vh.AddVert(bottomRight, colorBottomRight, uvBottomRight);
            vh.AddVert(bottomLeft, colorBottomLeft, uvBottomLeft);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static Texture2D EnsureShadowTexture()
        {
            if (_shadowTexture != null) return _shadowTexture;
            _shadowTexture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "CheryTools_Sonnet_KeyRain_GaussianShadow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            Object.DontDestroyOnLoad(_shadowTexture);

            var pixels = new Color[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float alpha = GaussianBoxCoverage(x + 0.5f, TextureBorder, TextureSize - TextureBorder, TextureSigma)
                        * GaussianBoxCoverage(y + 0.5f, TextureBorder, TextureSize - TextureBorder, TextureSigma);
                    pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                }
            }
            _shadowTexture.SetPixels(pixels);
            _shadowTexture.Apply(false, true);
            return _shadowTexture;
        }

        private static float GaussianBoxCoverage(float point, float min, float max, float sigma)
        {
            float left = (point - min) / sigma;
            float right = (point - max) / sigma;
            return Mathf.Clamp01(NormalCdf(left) - NormalCdf(right));
        }

        private static float NormalCdf(float x)
        {
            return 0.5f * (1f + ErfApprox(x * 0.70710678118f));
        }

        private static float ErfApprox(float x)
        {
            float sign = x < 0f ? -1f : 1f;
            x = Mathf.Abs(x);
            float t = 1f / (1f + 0.3275911f * x);
            float y = 1f - (((((1.061405429f * t - 1.453152027f) * t) + 1.421413741f) * t
                - 0.284496736f) * t + 0.254829592f) * t * Mathf.Exp(-x * x);
            return sign * y;
        }
    }
}
