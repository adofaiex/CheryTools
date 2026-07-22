using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CheryTools
{
    public class RichTextParser
    {
        public class ParsedSegment
        {
            public string RenderText;
            public System.Numerics.Vector4 Color;
            // < 0 means relative scale (-1.5f for 150%), > 0 means absolute size
            public float SizeValue = -1.0f;
            public bool HasColorTag;
            public bool HasSizeTag;
            public int SourceStart;
            public int SourceLength;
        }

        private static readonly Regex TagRegex = new Regex(@"<\s*(/)?\s*(color|size)\s*(?:=\s*([^>]+?))?\s*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static List<ParsedSegment> Parse(string input, System.Numerics.Vector4 defaultColor)
        {
            var segments = new List<ParsedSegment>();
            if (string.IsNullOrEmpty(input)) return segments;

            var matches = TagRegex.Matches(input);
            int currentIndex = 0;

            var colorStack = new Stack<System.Numerics.Vector4>();
            var sizeStack = new Stack<float>();

            System.Numerics.Vector4 currentColor = defaultColor;
            float currentSize = -1.0f; // -1 means default 1.0x scale

            foreach (Match match in matches)
            {
                AppendTextSegments(segments, input, currentIndex, match.Index - currentIndex, currentColor, currentSize, colorStack.Count > 0, sizeStack.Count > 0);

                bool isClosing = match.Groups[1].Success;
                string tagName = match.Groups[2].Value.ToLowerInvariant();
                string tagValue = match.Groups[3].Success ? match.Groups[3].Value.Trim().Trim('"', '\'') : string.Empty;
                if (!isClosing && tagName == "color")
                {
                    colorStack.Push(currentColor);
                    if (tagValue.StartsWith("#", StringComparison.Ordinal))
                    {
                        tagValue = tagValue.Substring(1);
                    }
                    currentColor = ParseHexColor(tagValue, currentColor);
                }
                else if (isClosing && tagName == "color")
                {
                    if (colorStack.Count > 0) currentColor = colorStack.Pop();
                    else currentColor = defaultColor;
                }
                else if (!isClosing && tagName == "size")
                {
                    sizeStack.Push(currentSize);
                    string sizeStr = tagValue;
                    if (sizeStr.EndsWith("%"))
                    {
                        if (float.TryParse(sizeStr.Substring(0, sizeStr.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float percent))
                        {
                            currentSize = -(percent / 100f); // Negative to indicate relative scale
                        }
                    }
                    else if (float.TryParse(sizeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float absSize))
                    {
                        currentSize = absSize; // Positive absolute size
                    }
                }
                else if (isClosing && tagName == "size")
                {
                    if (sizeStack.Count > 0) currentSize = sizeStack.Pop();
                    else currentSize = -1.0f;
                }

                currentIndex = match.Index + match.Length;
            }

            if (currentIndex < input.Length)
            {
                AppendTextSegments(segments, input, currentIndex, input.Length - currentIndex, currentColor, currentSize, colorStack.Count > 0, sizeStack.Count > 0);
            }

            return segments;
        }

        private static void AppendTextSegments(List<ParsedSegment> segments, string input, int start, int length, System.Numerics.Vector4 color, float size, bool hasColorTag, bool hasSizeTag)
        {
            if (length <= 0)
            {
                return;
            }

            int end = start + length;
            int lineStart = start;
            while (lineStart < end)
            {
                int newline = input.IndexOf('\n', lineStart, end - lineStart);
                bool hasNewline = newline >= 0;
                int lineEnd = hasNewline ? newline : end;
                int segmentEnd = hasNewline ? newline + 1 : lineEnd;
                segments.Add(new ParsedSegment
                {
                    RenderText = input.Substring(lineStart, segmentEnd - lineStart),
                    Color = color,
                    SizeValue = size,
                    HasColorTag = hasColorTag,
                    HasSizeTag = hasSizeTag,
                    SourceStart = lineStart,
                    SourceLength = segmentEnd - lineStart
                });
                lineStart = segmentEnd;
            }
        }

        public static System.Numerics.Vector4 ParseHexColor(string hex, System.Numerics.Vector4 fallback)
        {
            if (hex.Length == 6 || hex.Length == 8)
            {
                try
                {
                    float r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
                    float g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
                    float b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
                    float a = 1f;
                    if (hex.Length == 8)
                        a = Convert.ToInt32(hex.Substring(6, 2), 16) / 255f;
                    return new System.Numerics.Vector4(r, g, b, a);
                }
                catch { return fallback; }
            }
            return fallback;
        }
    }
}
