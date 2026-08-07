using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Xml.Serialization;

namespace CheryTools
{
    public enum AnimationTrigger
    {
        OnClick,
        OnComboIncrease
    }

    // 独立于 XML 序列化，用于 JsonConvert.DeserializeObject
    public class JsonAnimFrame
    {
        public float time; // 时间 (秒)
        public float? x; // 可空，如果未定义则保持不变
        public float? y;
        public float? zoomx;
        public float? zoomy;
        public float? opacity;
        public float? rotation;
        public string easing = "linear"; // 支持 "linear", "in-out-sine", "ease-out-quad" 等
    }

    [Serializable]
    public class OverlayerAnimation
    {
        public bool IsEnabled = false;
        public string Name = "New Animation";
        public AnimationTrigger Trigger = AnimationTrigger.OnClick;
        
        // 原始 JSON 字符串，供用户在编辑器里修改
        public string JsonString = "[\n  {\n    \"time\": 0.0,\n    \"zoomx\": 1.0,\n    \"zoomy\": 1.0,\n    \"easing\": \"linear\"\n  },\n  {\n    \"time\": 0.3,\n    \"zoomx\": 1.5,\n    \"zoomy\": 1.5,\n    \"easing\": \"ease-out-quad\"\n  },\n  {\n    \"time\": 0.6,\n    \"zoomx\": 1.0,\n    \"zoomy\": 1.0,\n    \"easing\": \"ease-out-quad\"\n  }\n]";

        // 图形化编辑支持
        public bool UseGraphicalAnimation = true;

        public float StartScale = 1.0f;
        public float StartRotation = 0f;
        public float StartX = 0f;
        public float StartY = 0f;
        public float StartOpacity = 1.0f;

        public float EndScale = 1.0f;
        public float EndRotation = 0f;
        public float EndX = 0f;
        public float EndY = 0f;
        public float EndOpacity = 1.0f;

        public float Duration = 0.5f;
        public string EasingType = "linear";

        // 反序列化后的对象列表，不参与 XML 序列化
        [XmlIgnore]
        public List<JsonAnimFrame> ParsedFrames = new List<JsonAnimFrame>();

        // 尝试解析 JSON 字符串
        public void ParseJson()
        {
            if (UseGraphicalAnimation)
            {
                ParsedFrames = new List<JsonAnimFrame>
                {
                    new JsonAnimFrame
                    {
                        time = 0f,
                        x = StartX,
                        y = StartY,
                        zoomx = StartScale,
                        zoomy = StartScale,
                        opacity = StartOpacity,
                        rotation = StartRotation,
                        easing = "linear"
                    },
                    new JsonAnimFrame
                    {
                        time = Duration,
                        x = EndX,
                        y = EndY,
                        zoomx = EndScale,
                        zoomy = EndScale,
                        opacity = EndOpacity,
                        rotation = EndRotation,
                        easing = EasingType
                    }
                };
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(JsonString))
                {
                    ParsedFrames = new List<JsonAnimFrame>();
                    return;
                }
                ParsedFrames = JsonConvert.DeserializeObject<List<JsonAnimFrame>>(JsonString);
                // 按照时间排序以防乱序
                if (ParsedFrames != null)
                {
                    ParsedFrames.Sort((a, b) => a.time.CompareTo(b.time));
                }
            }
            catch (Exception ex)
            {
                Main.Logger.Log($"[CheryTools] 解析动画 JSON 失败 ({Name}): {ex.Message}");
                ParsedFrames = new List<JsonAnimFrame>(); // 解析失败则置空
            }
        }
    }

    public static class EasingUtil
    {
        private static readonly Dictionary<string, string> NormalizedNames = new Dictionary<string, string>(StringComparer.Ordinal);

        // 缓动函数工具
        public static float EvaluateEasing(float t, string easingName)
        {
            if (string.IsNullOrEmpty(easingName)) return t;
            
            t = Math.Max(0f, Math.Min(1f, t));
            if (!NormalizedNames.TryGetValue(easingName, out string easeLower))
            {
                easeLower = easingName.ToLowerInvariant().Replace("-", "").Replace(" ", "");
                if (NormalizedNames.Count < 128) NormalizedNames[easingName] = easeLower;
            }
            
            if (easeLower.Contains("linear")) return t;

            // UI-friendly polynomial curves used by control-panel transitions.
            if (easeLower == "smoothstep") return t * t * (3f - 2f * t);
            if (easeLower == "smootherstep") return t * t * t * (t * (t * 6f - 15f) + 10f);
            
            // Sine
            if (easeLower == "easeinsine") return 1f - (float)Math.Cos(t * Math.PI / 2.0);
            if (easeLower == "easeoutsine") return (float)Math.Sin(t * Math.PI / 2.0);
            if (easeLower == "easeinoutsine" || easeLower == "inoutsine") return -0.5f * ((float)Math.Cos(Math.PI * t) - 1f);
            
            // Quad
            if (easeLower == "easeinquad") return t * t;
            if (easeLower == "easeoutquad") return t * (2f - t);
            if (easeLower == "easeinoutquad" || easeLower == "inoutquad") return t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;
            
            // Cubic
            if (easeLower == "easeincubic") return t * t * t;
            if (easeLower == "easeoutcubic") return 1f - (float)Math.Pow(1f - t, 3.0);
            if (easeLower == "easeinoutcubic" || easeLower == "inoutcubic") return t < 0.5f ? 4f * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 3.0) / 2f;
            
            // Quart
            if (easeLower == "easeinquart") return t * t * t * t;
            if (easeLower == "easeoutquart") return 1f - (float)Math.Pow(1f - t, 4.0);
            if (easeLower == "easeinoutquart" || easeLower == "inoutquart") return t < 0.5f ? 8f * t * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 4.0) / 2f;
            
            // Quint
            if (easeLower == "easeinquint" || easeLower == "inquint" || easeLower == "easeinquint") return t * t * t * t * t;
            if (easeLower == "easeoutquint" || easeLower == "outquint" || easeLower == "easeoutquint") return 1f - (float)Math.Pow(1f - t, 5.0);
            if (easeLower == "easeinoutquint" || easeLower == "inoutquint") return t < 0.5f ? 16f * t * t * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 5.0) / 2f;
            
            // Expo
            if (easeLower == "easeinexpo") return t == 0f ? 0f : (float)Math.Pow(2.0, 10.0 * t - 10.0);
            if (easeLower == "easeoutexpo") return t == 1f ? 1f : 1f - (float)Math.Pow(2.0, -10.0 * t);
            if (easeLower == "easeinoutexpo" || easeLower == "inoutexpo")
            {
                if (t == 0f) return 0f;
                if (t == 1f) return 1f;
                return t < 0.5f ? (float)Math.Pow(2.0, 20.0 * t - 10.0) / 2f : (2f - (float)Math.Pow(2.0, -20.0 * t + 10.0)) / 2f;
            }
            
            // Circ
            if (easeLower == "easeincirc") return 1f - (float)Math.Sqrt(1f - t * t);
            if (easeLower == "easeoutcirc") return (float)Math.Sqrt(1f - Math.Pow(t - 1f, 2.0));
            if (easeLower == "easeinoutcirc" || easeLower == "inoutcirc")
            {
                return t < 0.5f
                    ? (1f - (float)Math.Sqrt(1f - Math.Pow(2f * t, 2.0))) / 2f
                    : ((float)Math.Sqrt(1f - Math.Pow(-2f * t + 2f, 2.0)) + 1f) / 2f;
            }
            
            // Back
            if (easeLower == "easeinback")
            {
                float c1 = 1.70158f;
                float c3 = c1 + 1f;
                return c3 * t * t * t - c1 * t * t;
            }
            if (easeLower == "easeoutback")
            {
                float c1 = 1.70158f;
                float c3 = c1 + 1f;
                return 1f + c3 * (float)Math.Pow(t - 1f, 3.0) + c1 * (float)Math.Pow(t - 1f, 2.0);
            }
            if (easeLower == "easeinoutback" || easeLower == "inoutback")
            {
                float c1 = 1.70158f;
                float c2 = c1 * 1.525f;
                return t < 0.5f
                    ? ((float)Math.Pow(2f * t, 2.0) * ((c2 + 1f) * 2f * t - c2)) / 2f
                    : ((float)Math.Pow(2f * t - 2f, 2.0) * ((c2 + 1f) * (t * 2f - 2f) + c2) + 2f) / 2f;
            }
            
            return t; // Default to linear
        }
    }
}
