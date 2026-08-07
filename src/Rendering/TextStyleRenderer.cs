using System;
using System.Numerics;
using ImGuiNET;

namespace CheryTools
{
    internal static class TextStyleRenderer
    {
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

    }
}
