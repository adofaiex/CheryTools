using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingFont = System.Drawing.Font;
using DrawingFontFamily = System.Drawing.FontFamily;
using DrawingFontStyle = System.Drawing.FontStyle;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingGraphicsUnit = System.Drawing.GraphicsUnit;
using DrawingPointF = System.Drawing.PointF;
using DrawingStringFormat = System.Drawing.StringFormat;
using DrawingStringFormatFlags = System.Drawing.StringFormatFlags;

namespace CheryTools
{
    internal sealed class ExternalOverlayStateBuilder
    {
        private readonly StringBuilder _items = new StringBuilder(4096);
        private readonly bool _stableBounds;
        private readonly bool _visible;
        private readonly bool _fullClient;
        private int _count;
        private static readonly object MeasureSync = new object();
        private static readonly DrawingBitmap MeasureBitmap = new DrawingBitmap(1, 1);
        private static readonly PrivateFontCollection MeasurePrivateFonts = new PrivateFontCollection();
        private static readonly Dictionary<string, DrawingFontFamily> MeasureFontFamilies = new Dictionary<string, DrawingFontFamily>(StringComparer.OrdinalIgnoreCase);
        private static DrawingGraphics _measureGraphics;

        public ExternalOverlayStateBuilder(bool stableBounds, bool visible = true, bool fullClient = false)
        {
            _stableBounds = stableBounds;
            _visible = visible;
            _fullClient = fullClient;
        }

        public bool HasItems
        {
            get { return _count > 0; }
        }

        public void AddText(
            string id,
            string text,
            float x,
            float y,
            float width,
            float height,
            float layoutX,
            float layoutY,
            float layoutWidth,
            float layoutHeight,
            OverlayerText style,
            float fontSize,
            float letterSpacing,
            float lineHeightOffset)
        {
            if (style == null) return;

            BeginItem();
            _items.Append('{');
            AppendString("kind", "text"); Comma();
            AppendString("id", id); Comma();
            AppendNumber("x", x); Comma();
            AppendNumber("y", y); Comma();
            AppendNumber("width", width); Comma();
            AppendNumber("height", height); Comma();
            AppendString("text", text ?? string.Empty); Comma();
            AppendNumber("layoutX", layoutX); Comma();
            AppendNumber("layoutY", layoutY); Comma();
            AppendNumber("layoutWidth", layoutWidth); Comma();
            AppendNumber("layoutHeight", layoutHeight); Comma();
            AppendString("fontFamily", ResolveFontFamily(style.FontPath)); Comma();
            AppendString("fontPath", CheryToolsAssets.ResolveAssetPath(style.FontPath)); Comma();
            AppendNumber("fontSize", Mathf.Max(1f, fontSize)); Comma();
            AppendNumber("letterSpacing", letterSpacing); Comma();
            AppendNumber("lineHeightOffset", lineHeightOffset); Comma();
            AppendNumber("alignment", style.Alignment); Comma();
            AppendColor("color", style.TextColor, 1f, 1f, 1f, 1f); Comma();
            AppendBool("shadow", style.EnableShadow); Comma();
            AppendColor("shadowColor", style.ShadowColor, 0f, 0f, 0f, 1f); Comma();
            AppendNumber("shadowX", style.ShadowOffset != null && style.ShadowOffset.Length > 0 ? style.ShadowOffset[0] : 0f); Comma();
            AppendNumber("shadowY", style.ShadowOffset != null && style.ShadowOffset.Length > 1 ? style.ShadowOffset[1] : 0f); Comma();
            AppendBool("outline", style.EnableOutline); Comma();
            AppendColor("outlineColor", style.OutlineColor, 0f, 0f, 0f, 1f); Comma();
            AppendNumber("outlineThickness", style.OutlineThickness);
            _items.Append('}');
        }

        public void AddImage(
            string id,
            string imagePath,
            float boundX,
            float boundY,
            float boundWidth,
            float boundHeight,
            float drawX,
            float drawY,
            float drawWidth,
            float drawHeight,
            float centerX,
            float centerY,
            float rotation,
            float opacity)
        {
            if (string.IsNullOrEmpty(imagePath)) return;

            BeginItem();
            _items.Append('{');
            AppendString("kind", "image"); Comma();
            AppendString("id", id); Comma();
            AppendNumber("x", boundX); Comma();
            AppendNumber("y", boundY); Comma();
            AppendNumber("width", boundWidth); Comma();
            AppendNumber("height", boundHeight); Comma();
            AppendString("imagePath", CheryToolsAssets.ResolveAssetPath(imagePath)); Comma();
            AppendNumber("drawX", drawX); Comma();
            AppendNumber("drawY", drawY); Comma();
            AppendNumber("drawWidth", drawWidth); Comma();
            AppendNumber("drawHeight", drawHeight); Comma();
            AppendNumber("centerX", centerX); Comma();
            AppendNumber("centerY", centerY); Comma();
            AppendNumber("rotation", rotation); Comma();
            AppendNumber("opacity", Mathf.Clamp01(opacity));
            _items.Append('}');
        }

        public void AddRect(
            string id,
            float x,
            float y,
            float width,
            float height,
            float r,
            float g,
            float b,
            float a)
        {
            if (width <= 0f || height <= 0f || a <= 0f) return;

            BeginItem();
            _items.Append('{');
            AppendString("kind", "rect"); Comma();
            AppendString("id", id); Comma();
            AppendNumber("x", x); Comma();
            AppendNumber("y", y); Comma();
            AppendNumber("width", width); Comma();
            AppendNumber("height", height); Comma();
            AppendColor("color", r, g, b, a);
            _items.Append('}');
        }

        public string Build()
        {
            StringBuilder json = new StringBuilder(_items.Length + 64);
            json.Append("{\"type\":\"ovState\",\"stable\":");
            json.Append(_stableBounds ? "true" : "false");
            json.Append(",\"visible\":");
            json.Append(_visible ? "true" : "false");
            json.Append(",\"fullClient\":");
            json.Append(_fullClient ? "true" : "false");
            json.Append(",\"items\":[");
            json.Append(_items);
            json.Append("]}");
            return json.ToString();
        }

        public static Vector2 EstimateTextSize(string text, string fontPath, float fontSize, float letterSpacing, float lineHeightOffset)
        {
            lock (MeasureSync)
            {
                try
                {
                    DrawingGraphics graphics = GetMeasureGraphics();
                    DrawingFontFamily family = GetMeasureFontFamily(fontPath);
                    float safeFontSize = Mathf.Clamp(fontSize, 1f, 512f);
                    float safeLineOffset = float.IsNaN(lineHeightOffset) || float.IsInfinity(lineHeightOffset) ? 0f : lineHeightOffset;
                    string[] lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                    float maxWidth = 1f;
                    float totalHeight = 0f;

                    using (DrawingStringFormat format = (DrawingStringFormat)DrawingStringFormat.GenericTypographic.Clone())
                    {
                        format.FormatFlags |= DrawingStringFormatFlags.NoClip;
                        foreach (string line in lines)
                        {
                            List<MeasureSegment> segments = ParseMeasureRichTextLine(line, safeFontSize);
                            maxWidth = Mathf.Max(maxWidth, MeasureLineWidth(graphics, family, segments, format, letterSpacing));
                            totalHeight += Mathf.Max(1f, GetLineMaxSize(segments, safeFontSize) + safeLineOffset);
                        }
                    }

                    return new Vector2(Mathf.Ceil(maxWidth), Mathf.Ceil(Mathf.Max(1f, totalHeight)));
                }
                catch
                {
                    return EstimateTextSizeFallback(text, fontSize, letterSpacing, lineHeightOffset);
                }
            }
        }

        public static Vector2 EstimateTextSize(string text, float fontSize, float letterSpacing, float lineHeightOffset)
        {
            return EstimateTextSize(text, string.Empty, fontSize, letterSpacing, lineHeightOffset);
        }

        private static Vector2 EstimateTextSizeFallback(string text, float fontSize, float letterSpacing, float lineHeightOffset)
        {
            string plain = StripRichTextTags(text ?? string.Empty);
            string[] lines = plain.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            float maxWidth = 1f;
            float safeFontSize = Mathf.Max(1f, fontSize);
            float charAdvance = safeFontSize * 0.56f + Mathf.Max(0f, letterSpacing);

            foreach (string line in lines)
            {
                maxWidth = Mathf.Max(maxWidth, Mathf.Max(1, line.Length) * charAdvance);
            }

            float lineHeight = Mathf.Max(1f, safeFontSize + lineHeightOffset);
            return new Vector2(Mathf.Ceil(maxWidth), Mathf.Ceil(Mathf.Max(1, lines.Length) * lineHeight));
        }

        private static DrawingGraphics GetMeasureGraphics()
        {
            if (_measureGraphics == null)
            {
                _measureGraphics = DrawingGraphics.FromImage(MeasureBitmap);
                _measureGraphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            }
            return _measureGraphics;
        }

        private static DrawingFontFamily GetMeasureFontFamily(string fontPath)
        {
            string resolved = CheryToolsAssets.ResolveAssetPath(fontPath);
            if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
            {
                if (MeasureFontFamilies.TryGetValue(resolved, out DrawingFontFamily cached))
                {
                    return cached;
                }

                try
                {
                    MeasurePrivateFonts.AddFontFile(resolved);
                    DrawingFontFamily family = MeasurePrivateFonts.Families[MeasurePrivateFonts.Families.Length - 1];
                    MeasureFontFamilies[resolved] = family;
                    return family;
                }
                catch
                {
                    // Fall through to installed fonts.
                }
            }

            const string fallbackName = "Microsoft YaHei UI";
            string key = "installed:" + fallbackName;
            if (!MeasureFontFamilies.TryGetValue(key, out DrawingFontFamily installed))
            {
                try
                {
                    installed = new DrawingFontFamily(fallbackName);
                }
                catch
                {
                    installed = DrawingFontFamily.GenericSansSerif;
                }
                MeasureFontFamilies[key] = installed;
            }
            return installed;
        }

        private static float MeasureLineWidth(DrawingGraphics graphics, DrawingFontFamily family, List<MeasureSegment> segments, DrawingStringFormat format, float letterSpacing)
        {
            float width = 0f;
            int visibleIndex = 0;
            int visibleCount = CountVisibleSegments(segments);
            foreach (MeasureSegment segment in segments)
            {
                if (string.IsNullOrEmpty(segment.Text)) continue;

                using (DrawingFont font = new DrawingFont(family, segment.FontSize, DrawingFontStyle.Regular, DrawingGraphicsUnit.Pixel))
                {
                    width += graphics.MeasureString(segment.Text, font, DrawingPointF.Empty, format).Width;
                }
                if (letterSpacing != 0f && segment.Text.Length > 1)
                {
                    width += letterSpacing * (segment.Text.Length - 1);
                }

                visibleIndex++;
                if (letterSpacing != 0f && visibleIndex < visibleCount)
                {
                    width += letterSpacing;
                }
            }
            return Mathf.Max(1f, width);
        }

        private static int CountVisibleSegments(List<MeasureSegment> segments)
        {
            int count = 0;
            foreach (MeasureSegment segment in segments)
            {
                if (!string.IsNullOrEmpty(segment.Text)) count++;
            }
            return count;
        }

        private static float GetLineMaxSize(List<MeasureSegment> segments, float fallback)
        {
            float max = fallback;
            foreach (MeasureSegment segment in segments)
            {
                max = Mathf.Max(max, segment.FontSize);
            }
            return max;
        }

        private static List<MeasureSegment> ParseMeasureRichTextLine(string line, float defaultSize)
        {
            List<MeasureSegment> segments = new List<MeasureSegment>();
            Stack<float> sizes = new Stack<float>();
            sizes.Push(defaultSize);

            int index = 0;
            while (index < line.Length)
            {
                int tagStart = line.IndexOf('<', index);
                if (tagStart < 0)
                {
                    AddMeasureSegment(segments, line.Substring(index), sizes.Peek());
                    break;
                }

                if (tagStart > index)
                {
                    AddMeasureSegment(segments, line.Substring(index, tagStart - index), sizes.Peek());
                }

                int tagEnd = line.IndexOf('>', tagStart + 1);
                if (tagEnd < 0)
                {
                    AddMeasureSegment(segments, line.Substring(tagStart), sizes.Peek());
                    break;
                }

                string tag = line.Substring(tagStart + 1, tagEnd - tagStart - 1).Trim();
                ApplyMeasureTag(tag, sizes, defaultSize);
                index = tagEnd + 1;
            }

            if (segments.Count == 0)
            {
                segments.Add(new MeasureSegment(" ", defaultSize));
            }
            return segments;
        }

        private static void AddMeasureSegment(List<MeasureSegment> segments, string text, float size)
        {
            if (string.IsNullOrEmpty(text)) return;
            segments.Add(new MeasureSegment(text, Mathf.Clamp(size, 1f, 512f)));
        }

        private static void ApplyMeasureTag(string tag, Stack<float> sizes, float defaultSize)
        {
            if (string.Equals(tag, "/size", StringComparison.OrdinalIgnoreCase))
            {
                if (sizes.Count > 1) sizes.Pop();
                return;
            }
            if (!tag.StartsWith("size=", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string value = Unquote(tag.Substring("size=".Length).Trim());
            if (value.EndsWith("%", StringComparison.Ordinal))
            {
                string percentText = value.Substring(0, value.Length - 1).Trim();
                if (float.TryParse(percentText, NumberStyles.Float, CultureInfo.InvariantCulture, out float percent))
                {
                    sizes.Push(Mathf.Clamp(defaultSize * percent / 100f, 1f, 512f));
                    return;
                }
            }
            else if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                sizes.Push(parsed > 0f ? Mathf.Clamp(parsed, 1f, 512f) : defaultSize);
                return;
            }

            sizes.Push(sizes.Peek());
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2 && ((value[0] == '"' && value[value.Length - 1] == '"') || (value[0] == '\'' && value[value.Length - 1] == '\'')))
            {
                return value.Substring(1, value.Length - 2);
            }
            return value;
        }

        public static string StripRichTextTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return Regex.Replace(text, "<.*?>", string.Empty);
        }

        private void BeginItem()
        {
            if (_count > 0) _items.Append(',');
            _count++;
        }

        private void AppendString(string name, string value)
        {
            AppendName(name);
            _items.Append('"');
            Escape(value ?? string.Empty, _items);
            _items.Append('"');
        }

        private void AppendNumber(string name, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = 0f;
            AppendName(name);
            _items.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private void AppendNumber(string name, int value)
        {
            AppendName(name);
            _items.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private void AppendBool(string name, bool value)
        {
            AppendName(name);
            _items.Append(value ? "true" : "false");
        }

        private void AppendColor(string name, float[] color, float r, float g, float b, float a)
        {
            AppendName(name);
            _items.Append('{');
            AppendNumber("r", color != null && color.Length > 0 ? color[0] : r); Comma();
            AppendNumber("g", color != null && color.Length > 1 ? color[1] : g); Comma();
            AppendNumber("b", color != null && color.Length > 2 ? color[2] : b); Comma();
            AppendNumber("a", color != null && color.Length > 3 ? color[3] : a);
            _items.Append('}');
        }

        private void AppendColor(string name, float r, float g, float b, float a)
        {
            AppendName(name);
            _items.Append('{');
            AppendNumber("r", r); Comma();
            AppendNumber("g", g); Comma();
            AppendNumber("b", b); Comma();
            AppendNumber("a", a);
            _items.Append('}');
        }

        private void AppendName(string name)
        {
            _items.Append('"');
            _items.Append(name);
            _items.Append("\":");
        }

        private void Comma()
        {
            _items.Append(',');
        }

        private static void Escape(string value, StringBuilder builder)
        {
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 32)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }
        }

        private static string ResolveFontFamily(string fontPath)
        {
            string resolved = CheryToolsAssets.ResolveAssetPath(fontPath);
            if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
            {
                return Path.GetFileNameWithoutExtension(resolved);
            }

            return "Microsoft YaHei UI";
        }

        private struct MeasureSegment
        {
            public readonly string Text;
            public readonly float FontSize;

            public MeasureSegment(string text, float fontSize)
            {
                Text = text;
                FontSize = fontSize;
            }
        }
    }
}
