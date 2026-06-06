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

        // 反序列化后的对象列表，不参与 XML 序列化
        [XmlIgnore]
        public List<JsonAnimFrame> ParsedFrames = new List<JsonAnimFrame>();

        // 尝试解析 JSON 字符串
        public void ParseJson()
        {
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
        // 缓动函数工具
        public static float EvaluateEasing(float t, string easingName)
        {
            if (string.IsNullOrEmpty(easingName)) return t;
            
            t = Math.Max(0f, Math.Min(1f, t));
            string easeLower = easingName.ToLowerInvariant();
            
            if (easeLower.Contains("linear")) return t;
            
            if (easeLower == "ease-out-quad" || easeLower == "easeoutquad")
            {
                return t * (2f - t);
            }
            else if (easeLower == "ease-in-quad" || easeLower == "easeinquad")
            {
                return t * t;
            }
            else if (easeLower == "in-out-sine" || easeLower == "ease-in-out-sine" || easeLower == "easeinoutsine")
            {
                return -0.5f * ((float)Math.Cos(Math.PI * t) - 1f);
            }
            else if (easeLower == "ease-in-out-quad" || easeLower == "easeinoutquad")
            {
                return t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;
            }
            else if (easeLower == "in-quint" || easeLower == "inquint" || easeLower == "ease-in-quint")
            {
                return t * t * t * t * t;
            }
            else if (easeLower == "out-quint" || easeLower == "outquint" || easeLower == "ease-out-quint")
            {
                float t1 = t - 1f;
                return 1f + t1 * t1 * t1 * t1 * t1;
            }
            
            return t; // Default to linear
        }
    }
}
