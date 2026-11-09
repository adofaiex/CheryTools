using System;
using ImGuiNET;
using UnityEngine;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;
using CheryTools.Sonnet.Shared;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.Overlayer.Preview
{
    internal static class OvEasing
    {
        internal static readonly string[] Names =
        {
            "linear", "smoothstep", "smootherstep",
            "ease-in-sine", "ease-out-sine", "ease-in-out-sine",
            "ease-in-quad", "ease-out-quad", "ease-in-out-quad",
            "ease-in-cubic", "ease-out-cubic", "ease-in-out-cubic",
            "ease-in-quart", "ease-out-quart", "ease-in-out-quart",
            "ease-in-quint", "ease-out-quint", "ease-in-out-quint",
            "ease-in-expo", "ease-out-expo", "ease-in-out-expo",
            "ease-in-circ", "ease-out-circ", "ease-in-out-circ",
            "ease-in-back", "ease-out-back", "ease-in-out-back"
        };

        internal static float Evaluate(float value, string easing)
        {
            float t = Mathf.Clamp01(value);
            switch ((easing ?? "linear").Trim().ToLowerInvariant())
            {
                case "smoothstep": return t * t * (3f - 2f * t);
                case "smootherstep": return t * t * t * (t * (t * 6f - 15f) + 10f);
                case "ease-in-sine": return 1f - Mathf.Cos(t * Mathf.PI * 0.5f);
                case "ease-out-sine": return Mathf.Sin(t * Mathf.PI * 0.5f);
                case "ease-in-out-sine": return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
                case "ease-in-quad": return t * t;
                case "ease-out-quad": return 1f - (1f - t) * (1f - t);
                case "ease-in-out-quad": return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
                case "ease-in-cubic": return t * t * t;
                case "ease-out-cubic": return 1f - Mathf.Pow(1f - t, 3f);
                case "ease-in-out-cubic": return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
                case "ease-in-quart": return t * t * t * t;
                case "ease-out-quart": return 1f - Mathf.Pow(1f - t, 4f);
                case "ease-in-out-quart": return t < 0.5f ? 8f * Mathf.Pow(t, 4f) : 1f - Mathf.Pow(-2f * t + 2f, 4f) * 0.5f;
                case "ease-in-quint": return Mathf.Pow(t, 5f);
                case "ease-out-quint": return 1f - Mathf.Pow(1f - t, 5f);
                case "ease-in-out-quint": return t < 0.5f ? 16f * Mathf.Pow(t, 5f) : 1f - Mathf.Pow(-2f * t + 2f, 5f) * 0.5f;
                case "ease-in-expo": return t <= 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);
                case "ease-out-expo": return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                case "ease-in-out-expo": return t <= 0f ? 0f : t >= 1f ? 1f : t < 0.5f ? Mathf.Pow(2f, 20f * t - 10f) * 0.5f : (2f - Mathf.Pow(2f, -20f * t + 10f)) * 0.5f;
                case "ease-in-circ": return 1f - Mathf.Sqrt(1f - t * t);
                case "ease-out-circ": return Mathf.Sqrt(1f - Mathf.Pow(t - 1f, 2f));
                case "ease-in-out-circ": return t < 0.5f ? (1f - Mathf.Sqrt(1f - Mathf.Pow(2f * t, 2f))) * 0.5f : (Mathf.Sqrt(1f - Mathf.Pow(-2f * t + 2f, 2f)) + 1f) * 0.5f;
                case "ease-in-back": { const float c1 = 1.70158f; return (c1 + 1f) * t * t * t - c1 * t * t; }
                case "ease-out-back": { const float c1 = 1.70158f; return 1f + (c1 + 1f) * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f); }
                case "ease-in-out-back": { const float c2 = 1.70158f * 1.525f; return t < 0.5f ? Mathf.Pow(2f * t, 2f) * ((c2 + 1f) * 2f * t - c2) * 0.5f : (Mathf.Pow(2f * t - 2f, 2f) * ((c2 + 1f) * (t * 2f - 2f) + c2) + 2f) * 0.5f; }
                default: return t;
            }
        }
    }

    internal static class OvEasingSelector
    {
        internal static bool DrawPopup(string popupId, ref string selected)
        {
            string before = selected;
            ImGui.SetNextWindowSize(new NumVector2(710f, 520f), ImGuiCond.Appearing);
            if (!ImGui.BeginPopup(popupId)) return false;
            ImGui.TextUnformatted(L("选择缓动类型", "Choose easing"));
            ImGui.Separator();
            const float width = 122f;
            const float height = 70f;
            for (int i = 0; i < OvEasing.Names.Length; i++)
            {
                string name = OvEasing.Names[i];
                NumVector2 min = ImGui.GetCursorScreenPos();
                bool active = string.Equals(selected, name, StringComparison.OrdinalIgnoreCase);
                if (ImGui.InvisibleButton("##Ease_" + name, new NumVector2(width, height))) selected = name;
                ImDrawListPtr draw = ImGui.GetWindowDrawList();
                uint border = ImGui.GetColorU32(active ? SonnetPalette.Accent(1f) : new NumVector4(1f, 1f, 1f, 0.16f));
                draw.AddRectFilled(min, min + new NumVector2(width, height), ImGui.GetColorU32(new NumVector4(0.03f, 0.04f, 0.06f, 0.88f)), 6f);
                draw.AddRect(min, min + new NumVector2(width, height), border, 6f, ImDrawFlags.RoundCornersAll, active ? 2f : 1f);
                NumVector2 graphMin = min + new NumVector2(9f, 8f);
                NumVector2 graphSize = new NumVector2(width - 18f, 36f);
                NumVector2 previous = graphMin + new NumVector2(0f, graphSize.Y);
                for (int p = 1; p <= 28; p++)
                {
                    float t = p / 28f;
                    NumVector2 next = graphMin + new NumVector2(graphSize.X * t, graphSize.Y * (1f - OvEasing.Evaluate(t, name)));
                    draw.AddLine(previous, next, ImGui.GetColorU32(SonnetPalette.Accent(1f)), 1.5f);
                    previous = next;
                }
                draw.AddText(min + new NumVector2(8f, 50f), ImGui.GetColorU32(SonnetPalette.Text), name);
                if ((i + 1) % 5 != 0) ImGui.SameLine(0f, 14f);
                else ImGui.Dummy(new NumVector2(0f, 8f));
            }
            ImGui.EndPopup();
            return !string.Equals(before, selected, StringComparison.Ordinal);
        }
    }
}
