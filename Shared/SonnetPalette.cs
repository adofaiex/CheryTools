using ImGuiNET;
using NumVector4 = System.Numerics.Vector4;

namespace CheryTools.Sonnet.Shared
{
    // Shared source file linked into the module projects (see SonnetWindowChrome for how shared sources
    // are compiled). Theme colors for module UI, read from the live ImGui style that the Framework theme
    // writes (Text, TextDisabled, CheckMark = accent), so modules follow the user's theme without
    // referencing the Framework. Only valid while drawing (an ImGui context must exist).
    internal static class SonnetPalette
    {
        internal static NumVector4 Text => ImGui.GetStyle().Colors[(int)ImGuiCol.Text];

        internal static NumVector4 Muted => ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];

        internal static NumVector4 Accent(float alpha = 1f)
        {
            NumVector4 accent = ImGui.GetStyle().Colors[(int)ImGuiCol.CheckMark];
            return new NumVector4(accent.X, accent.Y, accent.Z, alpha);
        }
    }
}
