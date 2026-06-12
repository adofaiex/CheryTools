using System;
using System.Numerics;
using ImGuiNET;

namespace CheryTools
{
    internal static class TextStyleRenderer
    {
        private const float Diagonal = 0.70710678f;

        private static readonly Vector2[] OutlineDirections =
        {
            new Vector2(1f, 0f),
            new Vector2(-1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, -1f),
            new Vector2(Diagonal, Diagonal),
            new Vector2(-Diagonal, Diagonal),
            new Vector2(Diagonal, -Diagonal),
            new Vector2(-Diagonal, -Diagonal)
        };

        public static Vector4 ColorArrayToVector4(float[] color, Vector4 fallback)
        {
            if (color == null || color.Length < 4)
            {
                return fallback;
            }

            return new Vector4(color[0], color[1], color[2], color[3]);
        }

        public static uint ColorArrayToU32(float[] color, uint fallback)
        {
            if (color == null || color.Length < 4)
            {
                return fallback;
            }

            return ImGui.ColorConvertFloat4ToU32(new Vector4(color[0], color[1], color[2], color[3]));
        }

        public static float ClampOutlineThickness(float thickness)
        {
            if (float.IsNaN(thickness) || thickness <= 0f)
            {
                return 0f;
            }

            return Math.Min(thickness, 8f);
        }

        public static void AddText(
            ImDrawListPtr drawList,
            ImFontPtr font,
            float fontSize,
            Vector2 pos,
            uint textColor,
            string text,
            bool outlineEnabled,
            uint outlineColor,
            float outlineThickness)
        {
            AddTextOutline(drawList, font, fontSize, pos, outlineColor, text, outlineEnabled, outlineThickness);
            drawList.AddText(font, fontSize, pos, textColor, text);
        }

        public static void AddTextOutline(
            ImDrawListPtr drawList,
            ImFontPtr font,
            float fontSize,
            Vector2 pos,
            uint outlineColor,
            string text,
            bool outlineEnabled,
            float outlineThickness)
        {
            float thickness = ClampOutlineThickness(outlineThickness);
            if (!outlineEnabled || thickness <= 0f || string.IsNullOrEmpty(text) || ((outlineColor >> 24) & 0xFF) == 0)
            {
                return;
            }

            int rings = Math.Max(1, Math.Min(6, (int)Math.Ceiling(thickness)));
            for (int ring = 1; ring <= rings; ring++)
            {
                float radius = thickness * ring / rings;
                for (int i = 0; i < OutlineDirections.Length; i++)
                {
                    Vector2 offset = OutlineDirections[i] * radius;
                    drawList.AddText(font, fontSize, pos + offset, outlineColor, text);
                }
            }
        }
    }
}
