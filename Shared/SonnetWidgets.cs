using System;
using ImGuiNET;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;

namespace CheryTools.Sonnet.Shared
{
    // Shared source file linked into the Framework and module projects (see SonnetWindowChrome for how
    // shared sources are compiled). Drop-in replacements for ImGui sliders: the stock slider keeps all
    // interaction (drag, click, ctrl+click input, value text) while its frame and grab are hidden and
    // redrawn as a filled track with a pill handle in the theme accent (style SliderGrab color).
    internal static class SonnetWidgets
    {
        private const float GrabPadding = 2f; // ImGui SliderBehavior constant

        // ImGui.NET hands Text/TextColored/TextDisabled/TextWrapped strings to ImGui's printf-style
        // formatter, so a '%' in tag output, a path or a level name is read as a format specifier
        // ("% n" writes through a garbage pointer and the CRT kills the game). Wrap any text that is
        // not a fixed literal.
        internal static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.IndexOf('%') < 0 ? text : text.Replace("%", "%%");
        }

        // Button width that never clips its label; English text is often wider than the Chinese
        // the fixed widths were chosen for.
        internal static float FitWidth(string label, float minWidth)
        {
            return Math.Max(minWidth, ImGui.CalcTextSize(label, true).X + ImGui.GetStyle().FramePadding.X * 2f);
        }

        internal static bool FitButton(string label, float minWidth, float height)
        {
            return ImGui.Button(label, new NumVector2(FitWidth(label, minWidth), height));
        }

        // SameLine when the next item of this width still fits, otherwise it starts a new row.
        internal static void FlowNext(float nextWidth)
        {
            float right = ImGui.GetItemRectMax().X + ImGui.GetStyle().ItemSpacing.X + nextWidth;
            if (right <= ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X) ImGui.SameLine();
        }

        // Drag speed per pixel that suits the value's range: position-like values move in 0.5
        // steps, while a value limited to 0..1 would otherwise jump from end to end.
        internal static float DragSpeed(float min, float max)
        {
            float range = max - min;
            if (range <= 1f) return 0.005f;
            if (range <= 5f) return 0.01f;
            if (range <= 20f) return 0.05f;
            if (range <= 100f) return 0.1f;
            return 0.5f;
        }

        internal static string DragFormat(float min, float max)
        {
            return max - min <= 20f ? "%.2f" : "%.1f";
        }

        internal static bool SliderFloat(string label, ref float value, float min, float max, string format = "%.3f")
        {
            Track track = BeginTrack(value, min, max, false);
            bool changed = ImGui.SliderFloat(label, ref value, min, max, format);
            EndTrack(ref track, value);
            return changed;
        }

        internal static bool SliderInt(string label, ref int value, int min, int max, string format = "%d")
        {
            Track track = BeginTrack(value, min, max, true);
            bool changed = ImGui.SliderInt(label, ref value, min, max, format);
            EndTrack(ref track, value);
            return changed;
        }

        private struct Track
        {
            internal ImDrawListPtr Draw;
            internal NumVector2 Min;
            internal NumVector2 Max;
            internal float ValueMin;
            internal float ValueMax;
            internal float GrabSize;
            internal bool Split;
        }

        private static Track BeginTrack(float value, float min, float max, bool integer)
        {
            ImGuiStylePtr style = ImGui.GetStyle();
            var track = new Track
            {
                Draw = ImGui.GetWindowDrawList(),
                Min = ImGui.GetCursorScreenPos(),
                ValueMin = min,
                ValueMax = max
            };
            track.Max = track.Min + new NumVector2(ImGui.CalcItemWidth(), ImGui.GetFrameHeight());

            // Same grab size as ImGui's SliderBehavior, so the handle sits where the drag maps.
            float sliderSize = track.Max.X - track.Min.X - GrabPadding * 2f;
            float grab = style.GrabMinSize;
            if (integer && max >= min) grab = Math.Max(sliderSize / (max - min + 1f), style.GrabMinSize);
            track.GrabSize = Math.Min(grab, sliderSize);

            // Channel 0 holds the track under ImGui's value text (channel 1). Splitting cannot nest, so
            // inside an already split list (tables) the fill is drawn up front from the pre-drag value.
            track.Split = track.Draw._Splitter._Count <= 1;
            if (track.Split)
            {
                track.Draw.ChannelsSplit(2);
                track.Draw.ChannelsSetCurrent(1);
            }
            else
            {
                DrawFill(ref track, value);
            }

            ImGui.PushStyleColor(ImGuiCol.FrameBg, 0u);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, 0u);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, 0u);
            ImGui.PushStyleColor(ImGuiCol.SliderGrab, 0u);
            ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, 0u);
            return track;
        }

        private static void EndTrack(ref Track track, float value)
        {
            ImGui.PopStyleColor(5);
            bool active = ImGui.IsItemActive();
            bool hovered = ImGui.IsItemHovered();
            if (track.Split)
            {
                track.Draw.ChannelsSetCurrent(0);
                DrawFill(ref track, value, hovered || active);
                track.Draw.ChannelsMerge();
            }

            ImGuiStylePtr style = ImGui.GetStyle();
            NumVector4 accent = style.Colors[(int)ImGuiCol.SliderGrab];
            if (active) accent = NumVector4.Lerp(accent, new NumVector4(1f, 1f, 1f, 1f), 0.35f);
            float x = HandleX(ref track, value);
            float width = active || hovered ? 7f : 5f;
            float inset = Math.Max(4f, (track.Max.Y - track.Min.Y) * 0.2f);
            track.Draw.AddRectFilled(new NumVector2(x - width * 0.5f, track.Min.Y + inset),
                new NumVector2(x + width * 0.5f, track.Max.Y - inset), ImGui.GetColorU32(accent), width * 0.5f);
        }

        private static void DrawFill(ref Track track, float value, bool highlight = false)
        {
            ImGuiStylePtr style = ImGui.GetStyle();
            float rounding = style.FrameRounding;
            NumVector4 frame = style.Colors[(int)(highlight ? ImGuiCol.FrameBgHovered : ImGuiCol.FrameBg)];
            track.Draw.AddRectFilled(track.Min, track.Max, ImGui.GetColorU32(frame), rounding);
            NumVector4 accent = style.Colors[(int)ImGuiCol.SliderGrab];
            accent.W *= 0.30f;
            float x = HandleX(ref track, value);
            if (x > track.Min.X + 0.5f)
                track.Draw.AddRectFilled(track.Min, new NumVector2(x, track.Max.Y), ImGui.GetColorU32(accent),
                    rounding, ImDrawFlags.RoundCornersLeft);
        }

        private static float HandleX(ref Track track, float value)
        {
            float range = track.ValueMax - track.ValueMin;
            float t = range != 0f ? (value - track.ValueMin) / range : 0f;
            t = Math.Max(0f, Math.Min(1f, t));
            float sliderSize = track.Max.X - track.Min.X - GrabPadding * 2f;
            float usable = sliderSize - track.GrabSize;
            return track.Min.X + GrabPadding + track.GrabSize * 0.5f + usable * t;
        }
    }
}
