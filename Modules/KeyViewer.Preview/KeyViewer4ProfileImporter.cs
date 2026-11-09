using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.KeyViewer.Preview
{
    internal static class KeyViewer4ProfileImporter
    {
        private sealed class Placement
        {
            public KvKey Node;
            public float Left;
            public float Top;
            public float Width;
            public float Height;
        }

        public static bool CanImport(JObject root)
        {
            return root != null && Array(root, "Keys") != null;
        }

        public static KvProfile ImportToSonnet(JObject root, string sourceName, out string warning)
        {
            warning = string.Empty;
            JArray keys = Array(root, "Keys");
            if (keys == null || keys.Count == 0) throw new InvalidDataException(L("KV4 配置没有 Keys 列表。", "The KV4 profile has no Keys list."));

            string name = Path.GetFileNameWithoutExtension(sourceName ?? L("KV4 配置", "KV4 profile"));
            var profile = new KvProfile
            {
                Name = string.IsNullOrWhiteSpace(name) ? L("KV4 配置", "KV4 profile") : "KV4 - " + name,
                Enabled = true,
                ShowInGame = true,
                OnlyShowPlaying = Bool(root, "ViewOnlyGamePlay"),
                LayoutVersion = 1,
                Keys = new List<KvKey>()
            };

            JObject profileVector = Object(root, "VectorConfig") ?? new JObject();
            float[] profileOffset = StateVector2(profileVector, "Offset", "Released", 0f, 0f);
            float[] profileScale = StateVector2(profileVector, "Scale", "Released", 1f, 1f);
            profileScale[0] = Clamp(profileScale[0], 0.05f, 20f);
            profileScale[1] = Clamp(profileScale[1], 0.05f, 20f);
            float spacing = Clamp(Float(root, "KeySpacing", 10f), -500f, 500f);

            var placements = new List<Placement>();
            float cursor = 0f;
            int totalHits = 0;

            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i] as JObject;
                if (key == null) continue;

                string sourceCode = String(key, "Code", "None");
                KeyCode bind = MapKeyCode(sourceCode);
                string dummyName = String(key, "DummyName", "");

                JObject vector = Object(key, "VectorConfig") ?? new JObject();
                float[] releasedScale = StateVector2(vector, "Scale", "Released", 1f, 1f);
                float[] pressedScale = StateVector2(vector, "Scale", "Pressed", releasedScale[0], releasedScale[1]);
                float[] releasedOffset = StateVector2(vector, "Offset", "Released", 0f, 0f);
                float[] pressedOffset = StateVector2(vector, "Offset", "Pressed", releasedOffset[0], releasedOffset[1]);
                releasedScale[0] = Clamp(releasedScale[0], 0.05f, 20f);
                releasedScale[1] = Clamp(releasedScale[1], 0.05f, 20f);
                pressedScale[0] = Clamp(pressedScale[0], 0.05f, 20f);
                pressedScale[1] = Clamp(pressedScale[1], 0.05f, 20f);

                float baseWidth = 100f * releasedScale[0];
                float baseHeight = (Bool(key, "EnableCountText", true) ? 150f : 100f) * releasedScale[1];
                JObject background = Object(key, "BackgroundConfig") ?? new JObject();
                float[] backgroundScale = StateVector2(Object(background, "VectorConfig") ?? new JObject(), "Scale", "Released", 1f, 1f);
                float[] backgroundOffset = StateVector2(Object(background, "VectorConfig") ?? new JObject(), "Offset", "Released", 0f, 0f);
                float width = baseWidth * Clamp(backgroundScale[0], 0.05f, 20f);
                float height = baseHeight * Clamp(backgroundScale[1], 0.05f, 20f);
                float keyCenterX = (Bool(key, "DisableSorting") ? 0f : cursor + baseWidth / 2f) + releasedOffset[0];
                float keyCenterY = releasedOffset[1];
                float left = keyCenterX - width / 2f + backgroundOffset[0] * releasedScale[0];
                float top = keyCenterY - height / 2f - backgroundOffset[1] * releasedScale[1];

                string customLabel = String(Object(key, "Text") ?? new JObject(), "Released", "");
                if (string.IsNullOrWhiteSpace(customLabel))
                    customLabel = bind == KeyCode.None && !string.IsNullOrWhiteSpace(dummyName) ? dummyName : KvSettings.KeyName(bind);

                int countVal = Math.Max(0, Int(key, "Count", 0));
                totalHits += countVal;

                var node = new KvKey
                {
                    Bind = bind,
                    Label = customLabel,
                    Width = Math.Max(16f, width * profileScale[0]),
                    Height = Math.Max(16f, height * profileScale[1]),
                    BorderThickness = Bool(key, "EnableOutlineImage", true) ? 2f * Average(profileScale) : 0f,
                    CornerRadius = Math.Max(0f, Float(key, "BackgroundRoundness", 0f) * 90f * Math.Min(releasedScale[0], releasedScale[1]) * Math.Min(profileScale[0], profileScale[1])),
                    HitCount = countVal,
                    HideCount = !Bool(key, "EnableCountText", true),
                    LabelSize = Math.Max(8f, Float(key, "TextFontSize", 75f) * 0.35f * Average(profileScale)),
                    CountSize = Math.Max(8f, Float(key, "CountTextFontSize", 50f) * 0.35f * Average(profileScale)),
                    UseCustomStyle = true,
                    BackgroundNormal = ParseColor(background, "Released", new[] { 0.2f, 0.2f, 0.2f, 0.8f }),
                    BackgroundPressed = ParseColor(background, "Pressed", new[] { 0.8f, 0.8f, 0.8f, 0.8f }),
                    BorderNormal = ParseColor(Object(key, "OutlineConfig") ?? new JObject(), "Released", new[] { 0.4f, 0.4f, 0.4f, 1f }),
                    BorderPressed = ParseColor(Object(key, "OutlineConfig") ?? new JObject(), "Pressed", new[] { 1f, 1f, 1f, 1f }),
                    TextNormal = ParseColor(Object(key, "TextConfig") ?? new JObject(), "Released", new[] { 1f, 1f, 1f, 1f }),
                    TextPressed = ParseColor(Object(key, "TextConfig") ?? new JObject(), "Pressed", new[] { 0f, 0f, 0f, 1f }),
                    PositionX = left,
                    PositionY = top,
                    Depth = 0,
                    CountAlignment = 1
                };

                // 文字偏移
                JObject textConfig = Object(key, "TextConfig") ?? new JObject();
                JObject countConfig = Object(key, "CountTextConfig") ?? new JObject();
                float[] textOffset = StateVector2(Object(textConfig, "VectorConfig") ?? new JObject(), "Offset", "Released", 0f, 0f);
                float[] countOffset = StateVector2(Object(countConfig, "VectorConfig") ?? new JObject(), "Offset", "Released", 0f, 0f);
                node.LabelOffsetX = textOffset[0] * keyScaleX(releasedScale) * profileScale[0];
                node.LabelOffsetY = -textOffset[1] * keyScaleY(releasedScale) * profileScale[1] - baseHeight * profileScale[1] / 4f;
                node.CountOffsetX = countOffset[0] * keyScaleX(releasedScale) * profileScale[0];
                node.CountOffsetY = -countOffset[1] * keyScaleY(releasedScale) * profileScale[1] + baseHeight * profileScale[1] / 4f;

                // 键雨
                if (Bool(key, "RainEnabled"))
                {
                    JObject rain = Object(key, "Rain") ?? new JObject();
                    node.UseCustomRain = true;
                    node.RainEnabled = true;
                    node.RainRow = 1;
                    node.RainColor = ParseColor(Object(rain, "ObjectConfig") ?? new JObject(), "Released", new[] { 0.8f, 0.5f, 1f, 0.8f });
                    node.RainWidthRatio = 0.8f;
                }
                else
                {
                    node.UseCustomRain = true;
                    node.RainEnabled = false;
                    node.RainRow = 0;
                }

                // 按下动画
                float scaleRatio = (pressedScale[0] / releasedScale[0] + pressedScale[1] / releasedScale[1]) / 2f;
                float animOffsetX = (pressedOffset[0] - releasedOffset[0]) * profileScale[0];
                float animOffsetY = -(pressedOffset[1] - releasedOffset[1]) * profileScale[1];
                if (Math.Abs(scaleRatio - 1f) > 0.001f || Math.Abs(animOffsetX) > 0.001f || Math.Abs(animOffsetY) > 0.001f)
                {
                    node.UseCustomAnimation = true;
                    node.PressAnimationEnabled = true;
                    node.PressAnimationScale = Clamp(scaleRatio, 0.1f, 4f);
                    node.PressAnimationOffsetX = animOffsetX;
                    node.PressAnimationOffsetY = animOffsetY;
                    node.PressAnimationDuration = 0.12f;
                    node.PressAnimationEasing = "ease-out-quad";
                }

                placements.Add(new Placement { Node = node, Left = left, Top = top, Width = width, Height = height });
                profile.Keys.Add(node);
                cursor += Bool(key, "DisableSorting") ? 0f : baseWidth + spacing;
            }

            profile.TotalHits = totalHits;
            NormalizeLayout(placements, profileVector, profileOffset, profileScale);
            return profile;
        }

        private static float keyScaleX(float[] scale) => scale != null && scale.Length > 0 ? scale[0] : 1f;
        private static float keyScaleY(float[] scale) => scale != null && scale.Length > 1 ? scale[1] : 1f;

        private static void NormalizeLayout(List<Placement> placements, JObject profileVector, float[] profileOffset, float[] profileScale)
        {
            if (placements.Count == 0) return;
            float minX = float.PositiveInfinity, minY = float.PositiveInfinity, maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            for (int i = 0; i < placements.Count; i++)
            {
                Placement p = placements[i];
                float left = p.Left * profileScale[0];
                float top = p.Top * profileScale[1];
                minX = Math.Min(minX, left); minY = Math.Min(minY, top);
                maxX = Math.Max(maxX, left + p.Width * profileScale[0]); maxY = Math.Max(maxY, top + p.Height * profileScale[1]);
            }
            float width = Math.Max(1f, maxX - minX), height = Math.Max(1f, maxY - minY);
            float screenW = Screen.width > 0 ? Screen.width : 1920f;
            float screenH = Screen.height > 0 ? Screen.height : 1080f;
            string anchor = String(profileVector, "Anchor", "MiddleCenter");
            string pivot = String(profileVector, "Pivot", "MiddleCenter");
            float anchorX = anchor.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0 ? -screenW / 2f : anchor.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0 ? screenW / 2f : 0f;
            float anchorY = anchor.IndexOf("Top", StringComparison.OrdinalIgnoreCase) >= 0 ? -screenH / 2f : anchor.IndexOf("Bottom", StringComparison.OrdinalIgnoreCase) >= 0 ? screenH / 2f : 0f;
            float pivotX = pivot.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0 ? 0f : pivot.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0 ? 1f : 0.5f;
            float pivotY = pivot.IndexOf("Top", StringComparison.OrdinalIgnoreCase) >= 0 ? 0f : pivot.IndexOf("Bottom", StringComparison.OrdinalIgnoreCase) >= 0 ? 1f : 0.5f;
            float translationX = anchorX + profileOffset[0] * profileScale[0] - (minX + width * pivotX);
            float translationY = anchorY - profileOffset[1] * profileScale[1] - (minY + height * pivotY);

            for (int i = 0; i < placements.Count; i++)
            {
                Placement p = placements[i];
                p.Node.PositionX = p.Left * profileScale[0] + translationX;
                p.Node.PositionY = p.Top * profileScale[1] + translationY;
            }
        }

        private static float Average(float[] values) => values != null && values.Length >= 2 ? (values[0] + values[1]) * 0.5f : 1f;

        private static float Clamp(float val, float min, float max) => Math.Max(min, Math.Min(max, val));

        private static JToken Get(JObject obj, string name) => obj?.GetValue(name, StringComparison.OrdinalIgnoreCase);
        private static JObject Object(JObject obj, string name) => Get(obj, name) as JObject;
        private static JArray Array(JObject obj, string name) => Get(obj, name) as JArray;

        private static string String(JObject obj, string name, string fallback = "")
        {
            JToken token = Get(obj, name);
            if (token == null || token.Type == JTokenType.Null) return fallback;
            return token.Type == JTokenType.String ? token.Value<string>() ?? fallback : token.ToString();
        }

        private static bool Bool(JObject obj, string name, bool fallback = false)
        {
            JToken token = Get(obj, name);
            if (token == null || token.Type == JTokenType.Null) return fallback;
            bool parsed;
            return token.Type == JTokenType.Boolean ? token.Value<bool>() : bool.TryParse(token.ToString(), out parsed) ? parsed : fallback;
        }

        private static int Int(JObject obj, string name, int fallback = 0) => (int)Math.Round(Number(Get(obj, name), fallback));
        private static float Float(JObject obj, string name, float fallback = 0f) => (float)Number(Get(obj, name), fallback);

        private static double Number(JToken token, double fallback = 0d)
        {
            if (token == null || token.Type == JTokenType.Null) return fallback;
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                double value = token.Value<double>();
                return double.IsNaN(value) || double.IsInfinity(value) ? fallback : value;
            }
            double parsed;
            return double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) && !double.IsNaN(parsed) && !double.IsInfinity(parsed) ? parsed : fallback;
        }

        private static float[] StateVector2(JObject vectorConfig, string property, string state, float fallbackX, float fallbackY)
        {
            JObject values = Object(vectorConfig, property);
            if (values == null) return new[] { fallbackX, fallbackY };
            JToken token = Get(values, state) ?? Get(values, "Released");
            var array = token as JArray;
            if (array == null) return new[] { fallbackX, fallbackY };
            return new[]
            {
                (float)Number(array.Count > 0 ? array[0] : null, fallbackX),
                (float)Number(array.Count > 1 ? array[1] : null, fallbackY)
            };
        }

        private static readonly Regex RgbPattern = new Regex(
            @"^rgba?\s*\(\s*([^,]+)\s*,\s*([^,]+)\s*,\s*([^,\)]+)(?:\s*,\s*([^\)]+))?\s*\)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static float[] ParseColor(JObject objectConfig, string state, float[] fallback)
        {
            JObject colorPair = Object(objectConfig, "Color");
            if (colorPair == null) return fallback;
            JToken stateToken = Get(colorPair, state) ?? Get(colorPair, "Released");
            if (stateToken == null) return fallback;
            if (stateToken is JObject obj)
            {
                string[] corners = { "topLeft", "topRight", "bottomLeft", "bottomRight" };
                List<float[]> parsed = new List<float[]>();
                for (int i = 0; i < corners.Length; i++)
                {
                    JToken corner = Get(obj, corners[i]);
                    if (corner != null) parsed.Add(ParseCss(corner.ToString(), fallback));
                }
                if (parsed.Count > 0)
                {
                    float[] avg = new float[4];
                    for (int i = 0; i < parsed.Count; i++)
                        for (int c = 0; c < 4; c++) avg[c] += parsed[i][c];
                    for (int c = 0; c < 4; c++) avg[c] /= parsed.Count;
                    return avg;
                }
            }
            return ParseCss(stateToken.ToString(), fallback);
        }

        private static float[] ParseCss(string text, float[] fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            string value = text.Trim();
            if (value.Equals("transparent", StringComparison.OrdinalIgnoreCase)) return new[] { 0f, 0f, 0f, 0f };
            if (value.Equals("black", StringComparison.OrdinalIgnoreCase)) return new[] { 0f, 0f, 0f, 1f };
            if (value.Equals("white", StringComparison.OrdinalIgnoreCase)) return new[] { 1f, 1f, 1f, 1f };

            if (value[0] == '#')
            {
                string hex = value.Substring(1);
                if (hex.Length == 3 || hex.Length == 4)
                {
                    string expanded = "";
                    for (int i = 0; i < hex.Length; i++) expanded += new string(hex[i], 2);
                    hex = expanded;
                }
                if (hex.Length == 6 || hex.Length == 8)
                {
                    uint packed;
                    if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out packed))
                    {
                        if (hex.Length == 6) packed = (packed << 8) | 0xFFu;
                        return new[]
                        {
                            ((packed >> 24) & 0xFF) / 255f,
                            ((packed >> 16) & 0xFF) / 255f,
                            ((packed >> 8) & 0xFF) / 255f,
                            (packed & 0xFF) / 255f
                        };
                    }
                }
            }

            Match match = RgbPattern.Match(value);
            if (match.Success)
            {
                float r = ParseCssChannel(match.Groups[1].Value);
                float g = ParseCssChannel(match.Groups[2].Value);
                float b = ParseCssChannel(match.Groups[3].Value);
                float a = match.Groups[4].Success ? ParseCssAlpha(match.Groups[4].Value) : 1f;
                return new[] { r, g, b, a };
            }
            return fallback;
        }

        private static float ParseCssChannel(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0f;
            text = text.Trim();
            if (text.EndsWith("%", StringComparison.Ordinal))
            {
                float pct;
                if (float.TryParse(text.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out pct))
                    return Mathf.Clamp01(pct / 100f);
            }
            float val;
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                return Mathf.Clamp01(val / 255f);
            return 0f;
        }

        private static float ParseCssAlpha(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 1f;
            text = text.Trim();
            if (text.EndsWith("%", StringComparison.Ordinal))
            {
                float pct;
                if (float.TryParse(text.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out pct))
                    return Mathf.Clamp01(pct / 100f);
            }
            float val;
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                return Mathf.Clamp01(val);
            return 1f;
        }

        private static readonly Dictionary<string, KeyCode> KeyMap = new Dictionary<string, KeyCode>(StringComparer.OrdinalIgnoreCase)
        {
            ["LEFT SHIFT"] = KeyCode.LeftShift, ["RIGHT SHIFT"] = KeyCode.RightShift,
            ["LEFT CTRL"] = KeyCode.LeftControl, ["LEFT CONTROL"] = KeyCode.LeftControl,
            ["RIGHT CTRL"] = KeyCode.RightControl, ["RIGHT CONTROL"] = KeyCode.RightControl,
            ["LEFT ALT"] = KeyCode.LeftAlt, ["RIGHT ALT"] = KeyCode.RightAlt,
            ["BACKSPACE"] = KeyCode.Backspace, ["RETURN"] = KeyCode.Return, ["ENTER"] = KeyCode.Return,
            ["ESC"] = KeyCode.Escape, ["SPACE"] = KeyCode.Space, ["TAB"] = KeyCode.Tab,
            ["LEFT ARROW"] = KeyCode.LeftArrow, ["RIGHT ARROW"] = KeyCode.RightArrow,
            ["UP ARROW"] = KeyCode.UpArrow, ["DOWN ARROW"] = KeyCode.DownArrow,
            ["."] = KeyCode.Period, [","] = KeyCode.Comma, ["/"] = KeyCode.Slash,
            ["\\"] = KeyCode.Backslash, [";"] = KeyCode.Semicolon, ["'"] = KeyCode.Quote,
            ["["] = KeyCode.LeftBracket, ["]"] = KeyCode.RightBracket, ["`"] = KeyCode.BackQuote,
            ["-"] = KeyCode.Minus, ["="] = KeyCode.Equals
        };

        private static KeyCode MapKeyCode(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return KeyCode.None;
            string key = source.Trim();
            KeyCode result;
            if (Enum.TryParse(key, true, out result)) return result;
            if (KeyMap.TryGetValue(key, out result)) return result;
            if (key.Length == 1 && key[0] >= 'A' && key[0] <= 'Z')
            {
                if (Enum.TryParse(key, true, out result)) return result;
            }
            return KeyCode.None;
        }
    }
}
