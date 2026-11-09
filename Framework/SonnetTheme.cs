using ImGuiNET;
using UnityEngine;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;

namespace CheryTools.Sonnet.Framework
{
    internal static class SonnetTheme
    {
        internal sealed class Preset
        {
            internal readonly string Name;
            internal readonly string NameEn;
            internal readonly float[] Accent;
            internal readonly float[] Tint;

            internal Preset(string name, string nameEn, float[] accent, float[] tint)
            {
                Name = name;
                NameEn = nameEn;
                Accent = accent;
                Tint = tint;
            }

            internal string DisplayName => CheryTools.Sonnet.Shared.SonnetText.L(Name, NameEn);
        }

        // The first preset is the default theme. Accent drives every highlight (buttons, checks, sliders,
        // selection); tint is the hue of the dimming layers between the background image and the controls.
        internal static readonly Preset[] Presets =
        {
            new Preset("樱粉", "Sakura", Hex(0xFFBEDC), Hex(0x35183A)),
            new Preset("冰蓝", "Ice", new[] { 0.38f, 0.80f, 1.00f }, new[] { 0.020f, 0.035f, 0.060f }),
            new Preset("薄荷", "Mint", new[] { 0.36f, 0.92f, 0.70f }, new[] { 0.015f, 0.050f, 0.040f }),
            new Preset("琥珀", "Amber", new[] { 1.00f, 0.72f, 0.30f }, new[] { 0.060f, 0.040f, 0.015f }),
            new Preset("紫罗兰", "Violet", new[] { 0.70f, 0.56f, 1.00f }, new[] { 0.045f, 0.025f, 0.075f })
        };

        internal const float DefaultMaskOpacity = 0.70f;
        internal const float DefaultCardOpacity = 0.30f;
        internal const int DefaultMaskStyle = 1;
        internal const float MaxLayerOpacity = 0.90f;

        internal static readonly NumVector4 Text = new NumVector4(0.93f, 0.95f, 0.98f, 1f);
        internal static readonly NumVector4 MutedText = new NumVector4(0.80f, 0.82f, 0.88f, 1f);
        internal static readonly NumVector4 Hairline = new NumVector4(0.92f, 0.95f, 0.98f, 0.18f);
        internal static NumVector4 Accent { get; private set; }
        internal static NumVector4 AccentSoft { get; private set; }
        // RGB of the dimming layers; alpha is applied per layer.
        internal static NumVector4 Tint { get; private set; }
        internal static NumVector4 Sidebar { get; private set; }
        internal static NumVector4 Content { get; private set; }
        internal static NumVector4 Tabs { get; private set; }

        static SonnetTheme()
        {
            Configure(null);
        }

        internal static float[] DefaultAccent => (float[])Presets[0].Accent.Clone();
        internal static float[] DefaultTint => (float[])Presets[0].Tint.Clone();

        // Recomputes the palette from the settings and pushes it into the ImGui style. Call after any
        // theme setting changes; it is not run per frame.
        internal static void Configure(FrameworkSettings settings)
        {
            NumVector4 accent = Rgb(settings?.AccentColor, Presets[0].Accent);
            NumVector4 tint = Rgb(settings?.MaskTint, Presets[0].Tint);
            float card = Mathf.Clamp(settings?.CardOpacity ?? DefaultCardOpacity, 0f, MaxLayerOpacity);
            Accent = accent;
            AccentSoft = WithAlpha(accent, 0.13f);
            Tint = tint;
            Content = WithAlpha(tint, card);
            Sidebar = WithAlpha(tint, Mathf.Min(0.95f, card + 0.08f));
            Tabs = WithAlpha(tint, Mathf.Min(0.95f, card + 0.05f));
            if (_styleReady) ApplyColors();
        }

        // Set once Apply has run, i.e. an ImGui context exists to receive style colors.
        private static bool _styleReady;

        internal static void Apply(float scale)
        {
            var style = ImGui.GetStyle();
            style.Alpha = 1f;
            style.AntiAliasedLines = true;
            style.AntiAliasedLinesUseTex = true;
            style.AntiAliasedFill = true;
            style.WindowPadding = NumVector2.Zero;
            style.FramePadding = new NumVector2(12f, 8f) * scale;
            style.ItemSpacing = new NumVector2(10f, 8f) * scale;
            style.ItemInnerSpacing = new NumVector2(8f, 6f) * scale;
            style.CellPadding = new NumVector2(12f, 8f) * scale;
            style.WindowRounding = 18f * scale;
            style.ChildRounding = 12f * scale;
            style.FrameRounding = 7f * scale;
            style.PopupRounding = 8f * scale;
            style.ScrollbarRounding = 8f * scale;
            style.GrabRounding = 5f * scale;
            style.TabRounding = 7f * scale;
            style.WindowBorderSize = 0f;
            style.ChildBorderSize = 0f;
            style.FrameBorderSize = 0f;
            style.ScrollbarSize = 10f * scale;
            style.GrabMinSize = 15f * scale;
            _styleReady = true;
            ApplyColors();
        }

        private static void ApplyColors()
        {
            var style = ImGui.GetStyle();
            NumVector4 accent = Accent;
            NumVector4 tint = Tint;
            style.Colors[(int)ImGuiCol.Text] = Text;
            style.Colors[(int)ImGuiCol.TextDisabled] = MutedText;
            style.Colors[(int)ImGuiCol.WindowBg] = new NumVector4(0f, 0f, 0f, 0f);
            style.Colors[(int)ImGuiCol.ChildBg] = new NumVector4(0f, 0f, 0f, 0f);
            style.Colors[(int)ImGuiCol.PopupBg] = WithAlpha(Scale(tint, 0.6f), 0.98f);
            style.Colors[(int)ImGuiCol.Border] = new NumVector4(1f, 1f, 1f, 0.12f);
            style.Colors[(int)ImGuiCol.FrameBg] = new NumVector4(1f, 1f, 1f, 0.055f);
            style.Colors[(int)ImGuiCol.FrameBgHovered] = new NumVector4(1f, 1f, 1f, 0.10f);
            style.Colors[(int)ImGuiCol.FrameBgActive] = AccentSoft;
            style.Colors[(int)ImGuiCol.Button] = new NumVector4(1f, 1f, 1f, 0.035f);
            style.Colors[(int)ImGuiCol.ButtonHovered] = WithAlpha(accent, 0.16f);
            style.Colors[(int)ImGuiCol.ButtonActive] = WithAlpha(accent, 0.28f);
            style.Colors[(int)ImGuiCol.Header] = new NumVector4(1f, 1f, 1f, 0.04f);
            style.Colors[(int)ImGuiCol.HeaderHovered] = WithAlpha(accent, 0.14f);
            style.Colors[(int)ImGuiCol.HeaderActive] = AccentSoft;
            style.Colors[(int)ImGuiCol.Separator] = new NumVector4(1f, 1f, 1f, 0.10f);
            style.Colors[(int)ImGuiCol.TableHeaderBg] = WithAlpha(Lerp(tint, accent, 0.18f), 0.78f);
            style.Colors[(int)ImGuiCol.TableRowBg] = WithAlpha(tint, 0.34f);
            style.Colors[(int)ImGuiCol.TableRowBgAlt] = new NumVector4(1f, 1f, 1f, 0.025f);
            style.Colors[(int)ImGuiCol.TableBorderStrong] = WithAlpha(accent, 0.14f);
            style.Colors[(int)ImGuiCol.TableBorderLight] = new NumVector4(1f, 1f, 1f, 0.055f);
            style.Colors[(int)ImGuiCol.CheckMark] = accent;
            style.Colors[(int)ImGuiCol.SliderGrab] = accent;
            style.Colors[(int)ImGuiCol.SliderGrabActive] = WithAlpha(Lerp(accent, new NumVector4(1f, 1f, 1f, 1f), 0.4f), 1f);
            style.Colors[(int)ImGuiCol.TextSelectedBg] = WithAlpha(accent, 0.35f);
        }

        internal static uint Color(NumVector4 color)
        {
            return ImGui.GetColorU32(color);
        }

        internal static float ClampScale(float value)
        {
            return Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? 1f : value, 0.75f, 1.5f);
        }

        private static NumVector4 Rgb(float[] value, float[] fallback)
        {
            float[] rgb = value != null && value.Length >= 3 ? value : fallback;
            return new NumVector4(Mathf.Clamp01(rgb[0]), Mathf.Clamp01(rgb[1]), Mathf.Clamp01(rgb[2]), 1f);
        }

        private static float[] Hex(int rgb) =>
            new[] { ((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f };

        private static NumVector4 WithAlpha(NumVector4 color, float alpha) => new NumVector4(color.X, color.Y, color.Z, alpha);

        private static NumVector4 Scale(NumVector4 color, float factor) => new NumVector4(color.X * factor, color.Y * factor, color.Z * factor, color.W);

        private static NumVector4 Lerp(NumVector4 a, NumVector4 b, float t) => a + (b - a) * t;
    }
}
