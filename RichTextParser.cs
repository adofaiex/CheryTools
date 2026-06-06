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
        }

        private static readonly Regex TagRegex = new Regex(@"<(color=#([0-9a-fA-F]{6,8})|/color|size=([^>]+)|/size)>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
                if (match.Index > currentIndex)
                {
                    segments.Add(new ParsedSegment
                    {
                        RenderText = input.Substring(currentIndex, match.Index - currentIndex),
                        Color = currentColor,
                        SizeValue = currentSize,
                        HasColorTag = colorStack.Count > 0,
                        HasSizeTag = sizeStack.Count > 0
                    });
                }

                string tagFull = match.Value.ToLower();
                if (tagFull.StartsWith("<color=#"))
                {
                    colorStack.Push(currentColor);
                    currentColor = ParseHexColor(match.Groups[2].Value, defaultColor);
                }
                else if (tagFull == "</color>")
                {
                    if (colorStack.Count > 0) currentColor = colorStack.Pop();
                    else currentColor = defaultColor;
                }
                else if (tagFull.StartsWith("<size="))
                {
                    sizeStack.Push(currentSize);
                    string sizeStr = match.Groups[3].Value.Trim();
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
                else if (tagFull == "</size>")
                {
                    if (sizeStack.Count > 0) currentSize = sizeStack.Pop();
                    else currentSize = -1.0f;
                }

                currentIndex = match.Index + match.Length;
            }

            if (currentIndex < input.Length)
            {
                segments.Add(new ParsedSegment
                {
                    RenderText = input.Substring(currentIndex),
                    Color = currentColor,
                    SizeValue = currentSize,
                    HasColorTag = colorStack.Count > 0,
                    HasSizeTag = sizeStack.Count > 0
                });
            }

            return segments;
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
