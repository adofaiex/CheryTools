using System;
using System.Collections.Generic;
using ImGuiNET;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;

namespace CheryTools.Sonnet.Shared
{
    // Shared source file. It is linked into the Framework and into each module project that needs it
    // (<Compile Include="..\..\Shared\..." Link="Shared\..." />), so every assembly gets its own internal
    // copy and modules keep depending on Contracts only.
    //
    // Window chrome for the framework panel and the standalone editor windows (KeyViewer FreeMake,
    // Overlayer text editor and node editor): edge/corner resize handles with proximity highlighting,
    // window clamping, and cover-fit UVs for background images.
    internal static class SonnetWindowChrome
    {
        internal const int EdgeLeft = 1;
        internal const int EdgeRight = 2;
        internal const int EdgeTop = 4;
        internal const int EdgeBottom = 8;

        private const float HandleEdge = 9f;
        private const float HandleCorner = 24f;
        private const float WindowMargin = 8f;
        private const ImGuiWindowFlags HandleFlags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBackground
            | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing;

        private static readonly string[] HandleNames =
            { "left", "right", "top", "bottom", "top_left", "top_right", "bottom_left", "bottom_right" };
        private static readonly Dictionary<string, string[]> HandleIds = new Dictionary<string, string[]>(StringComparer.Ordinal);

        // Draws the eight invisible resize handles around a window, applies drag deltas to position and
        // size (kept on screen and at least minimumSize), and renders the proximity highlight.
        internal static void DrawResizeHandles(string idPrefix, ref NumVector2 position, ref NumVector2 size,
            NumVector2 display, NumVector2 minimumSize)
        {
            string[] ids = GetHandleIds(idPrefix);
            NumVector2 minimum = new NumVector2(Math.Min(minimumSize.X, display.X - 16f), Math.Min(minimumSize.Y, display.Y - 16f));
            float spanY = Math.Max(1f, size.Y - HandleCorner * 2f);
            float spanX = Math.Max(1f, size.X - HandleCorner * 2f);
            int hovered = 0;
            int active = 0;
            ResizeHandle(ids, 0, new NumVector2(0f, HandleCorner), new NumVector2(HandleEdge, spanY), EdgeLeft, ImGuiMouseCursor.ResizeEW, display, minimum, ref position, ref size, ref hovered, ref active);
            ResizeHandle(ids, 1, new NumVector2(size.X - HandleEdge, HandleCorner), new NumVector2(HandleEdge, spanY), EdgeRight, ImGuiMouseCursor.ResizeEW, display, minimum, ref position, ref size, ref hovered, ref active);
            ResizeHandle(ids, 2, new NumVector2(HandleCorner, 0f), new NumVector2(spanX, HandleEdge), EdgeTop, ImGuiMouseCursor.ResizeNS, display, minimum, ref position, ref size, ref hovered, ref active);
            ResizeHandle(ids, 3, new NumVector2(HandleCorner, size.Y - HandleEdge), new NumVector2(spanX, HandleEdge), EdgeBottom, ImGuiMouseCursor.ResizeNS, display, minimum, ref position, ref size, ref hovered, ref active);
            ResizeHandle(ids, 4, NumVector2.Zero, new NumVector2(HandleCorner), EdgeLeft | EdgeTop, ImGuiMouseCursor.ResizeNWSE, display, minimum, ref position, ref size, ref hovered, ref active);
            ResizeHandle(ids, 5, new NumVector2(size.X - HandleCorner, 0f), new NumVector2(HandleCorner), EdgeRight | EdgeTop, ImGuiMouseCursor.ResizeNESW, display, minimum, ref position, ref size, ref hovered, ref active);
            ResizeHandle(ids, 6, new NumVector2(0f, size.Y - HandleCorner), new NumVector2(HandleCorner), EdgeLeft | EdgeBottom, ImGuiMouseCursor.ResizeNESW, display, minimum, ref position, ref size, ref hovered, ref active);
            ResizeHandle(ids, 7, size - new NumVector2(HandleCorner), new NumVector2(HandleCorner), EdgeRight | EdgeBottom, ImGuiMouseCursor.ResizeNWSE, display, minimum, ref position, ref size, ref hovered, ref active);
            RenderResizeVisuals(position, size, active, hovered);
        }

        private static string[] GetHandleIds(string idPrefix)
        {
            if (HandleIds.TryGetValue(idPrefix, out string[] ids)) return ids;
            ids = new string[HandleNames.Length * 2];
            for (int i = 0; i < HandleNames.Length; i++)
            {
                ids[i * 2] = "##" + idPrefix + "ResizeWindow_" + HandleNames[i];
                ids[i * 2 + 1] = "##" + idPrefix + "Resize_" + HandleNames[i];
            }

            HandleIds[idPrefix] = ids;
            return ids;
        }

        private static void ResizeHandle(string[] ids, int index, NumVector2 localPosition, NumVector2 handleSize, int edges,
            ImGuiMouseCursor cursor, NumVector2 display, NumVector2 minimum,
            ref NumVector2 position, ref NumVector2 size, ref int hoveredEdges, ref int activeEdges)
        {
            ImGui.SetNextWindowPos(position + localPosition, ImGuiCond.Always);
            ImGui.SetNextWindowSize(handleSize, ImGuiCond.Always);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, NumVector2.Zero);
            ImGui.Begin(ids[index * 2], HandleFlags);
            ImGui.InvisibleButton(ids[index * 2 + 1], handleSize);
            bool isHovered = ImGui.IsItemHovered();
            bool isActive = ImGui.IsItemActive();
            if (isHovered) hoveredEdges |= edges;
            if (isActive) activeEdges |= edges;
            if (isHovered || isActive) ImGui.SetMouseCursor(cursor);
            if (isActive && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                NumVector2 delta = ImGui.GetIO().MouseDelta;
                NumVector2 nextPosition = position;
                NumVector2 nextSize = size;
                float right = position.X + size.X;
                float bottom = position.Y + size.Y;
                if ((edges & EdgeLeft) != 0)
                {
                    nextPosition.X = Clamp(position.X + delta.X, WindowMargin, right - minimum.X);
                    nextSize.X = right - nextPosition.X;
                }
                if ((edges & EdgeRight) != 0) nextSize.X += delta.X;
                if ((edges & EdgeTop) != 0)
                {
                    nextPosition.Y = Clamp(position.Y + delta.Y, WindowMargin, bottom - minimum.Y);
                    nextSize.Y = bottom - nextPosition.Y;
                }
                if ((edges & EdgeBottom) != 0) nextSize.Y += delta.Y;
                size = ClampSize(nextSize, display, minimum, WindowMargin);
                position = ClampPosition(nextPosition, size, display, WindowMargin);
            }
            ImGui.End();
            ImGui.PopStyleVar();
        }

        internal static NumVector2 ClampPosition(NumVector2 position, NumVector2 size, NumVector2 display, float margin)
        {
            float maxX = Math.Max(margin, display.X - size.X - margin);
            float maxY = Math.Max(margin, display.Y - size.Y - margin);
            position.X = Clamp(position.X, margin, maxX);
            position.Y = Clamp(position.Y, margin, maxY);
            return position;
        }

        internal static NumVector2 ClampSize(NumVector2 size, NumVector2 display, NumVector2 minimum, float margin)
        {
            float maxWidth = Math.Max(320f, display.X - margin * 2f);
            float maxHeight = Math.Max(320f, display.Y - margin * 2f);
            size.X = Clamp(size.X, Math.Min(minimum.X, maxWidth), maxWidth);
            size.Y = Clamp(size.Y, Math.Min(minimum.Y, maxHeight), maxHeight);
            return size;
        }

        // UVs that crop an image to fill an area while keeping its aspect ratio (CSS "cover").
        internal static void CalculateCoverUv(int imageWidth, int imageHeight, float areaWidth, float areaHeight,
            out NumVector2 uv0, out NumVector2 uv1)
        {
            float imageAspect = imageWidth / (float)Math.Max(1, imageHeight);
            float areaAspect = areaWidth / Math.Max(1f, areaHeight);
            uv0 = NumVector2.Zero;
            uv1 = NumVector2.One;
            if (imageAspect > areaAspect)
            {
                float crop = (1f - areaAspect / imageAspect) * 0.5f;
                uv0.X = crop;
                uv1.X = 1f - crop;
            }
            else
            {
                float crop = (1f - imageAspect / areaAspect) * 0.5f;
                uv0.Y = crop;
                uv1.Y = 1f - crop;
            }
        }

        // Proximity grip: faint bottom-right hatch when the mouse is away; corner and edge grips that
        // brighten as the mouse approaches, hovers or drags a handle.
        internal static void RenderResizeVisuals(NumVector2 position, NumVector2 size, int activeEdges, int hoveredEdges)
        {
            NumVector2 mousePos = ImGui.GetIO().MousePos;
            const float proximity = 36f;
            float minX = position.X;
            float maxX = position.X + size.X;
            float minY = position.Y;
            float maxY = position.Y + size.Y;

            bool inOuter = mousePos.X >= minX - proximity && mousePos.X <= maxX + proximity &&
                           mousePos.Y >= minY - proximity && mousePos.Y <= maxY + proximity;
            bool inCore = mousePos.X >= minX + proximity && mousePos.X <= maxX - proximity &&
                          mousePos.Y >= minY + proximity && mousePos.Y <= maxY - proximity;
            bool isNear = (inOuter && !inCore) || activeEdges != 0 || hoveredEdges != 0;

            ImDrawListPtr draw = ImGui.GetForegroundDrawList();

            if (!isNear)
            {
                uint faintColor = ImGui.GetColorU32(new NumVector4(0.5f, 0.65f, 0.85f, 0.18f));
                for (int i = 1; i <= 3; i++)
                {
                    float o = i * 4.5f;
                    draw.AddLine(new NumVector2(maxX - o, maxY), new NumVector2(maxX, maxY - o), faintColor, 1.4f);
                }
                return;
            }

            DrawCornerGrip(draw, new NumVector2(minX, minY), 1f, 1f, EdgeLeft | EdgeTop, mousePos, activeEdges, hoveredEdges);
            DrawCornerGrip(draw, new NumVector2(maxX, minY), -1f, 1f, EdgeRight | EdgeTop, mousePos, activeEdges, hoveredEdges);
            DrawCornerGrip(draw, new NumVector2(minX, maxY), 1f, -1f, EdgeLeft | EdgeBottom, mousePos, activeEdges, hoveredEdges);
            DrawCornerGrip(draw, new NumVector2(maxX, maxY), -1f, -1f, EdgeRight | EdgeBottom, mousePos, activeEdges, hoveredEdges, isBottomRight: true);

            DrawEdgeGrip(draw, new NumVector2((minX + maxX) * 0.5f, minY), true, EdgeTop, minX, maxX, mousePos, activeEdges, hoveredEdges);
            DrawEdgeGrip(draw, new NumVector2((minX + maxX) * 0.5f, maxY), true, EdgeBottom, minX, maxX, mousePos, activeEdges, hoveredEdges);
            DrawEdgeGrip(draw, new NumVector2(minX, (minY + maxY) * 0.5f), false, EdgeLeft, minY, maxY, mousePos, activeEdges, hoveredEdges);
            DrawEdgeGrip(draw, new NumVector2(maxX, (minY + maxY) * 0.5f), false, EdgeRight, minY, maxY, mousePos, activeEdges, hoveredEdges);
        }

        private static void DrawCornerGrip(ImDrawListPtr draw, NumVector2 c, float dx, float dy, int edgeMask,
            NumVector2 mousePos, int activeEdges, int hoveredEdges, bool isBottomRight = false)
        {
            float dist = NumVector2.Distance(mousePos, c);
            bool isActive = (activeEdges & edgeMask) == edgeMask;
            bool isHovered = (hoveredEdges & edgeMask) == edgeMask;
            bool isClose = dist <= 42f;

            float alpha = isActive ? 1.0f : isHovered ? 0.95f : isClose ? Lerp(0.45f, 0.85f, 1f - dist / 42f) : 0.32f;
            uint col = ImGui.GetColorU32(GripColor(isActive, isHovered, alpha));

            float arm = isActive || isHovered ? 20f : 16f;
            float stroke = isActive || isHovered ? 2.8f : 2.0f;
            draw.AddLine(c, new NumVector2(c.X + dx * arm, c.Y), col, stroke);
            draw.AddLine(c, new NumVector2(c.X, c.Y + dy * arm), col, stroke);

            if (isBottomRight)
            {
                for (int i = 1; i <= 3; i++)
                {
                    float o = i * 5.2f;
                    draw.AddLine(new NumVector2(c.X + dx * o, c.Y), new NumVector2(c.X, c.Y + dy * o), col, stroke > 2f ? 2.0f : 1.5f);
                }
            }
            else
            {
                draw.AddCircleFilled(new NumVector2(c.X + dx * 7f, c.Y + dy * 7f), 3.2f, col);
            }
        }

        private static void DrawEdgeGrip(ImDrawListPtr draw, NumVector2 mid, bool isHorizontal, int edgeMask,
            float rangeMin, float rangeMax, NumVector2 mousePos, int activeEdges, int hoveredEdges)
        {
            bool isActive = (activeEdges & edgeMask) != 0;
            bool isHovered = (hoveredEdges & edgeMask) != 0;

            float alongPos = isHorizontal ? mousePos.X : mousePos.Y;
            float perpDist = Math.Abs(isHorizontal ? mousePos.Y - mid.Y : mousePos.X - mid.X);
            bool withinSpan = alongPos >= rangeMin + 28f && alongPos <= rangeMax - 28f;
            bool isClose = withinSpan && perpDist <= 28f;

            if (!isActive && !isHovered && !isClose) return;

            float alpha = isActive ? 1.0f : isHovered ? 0.90f : Lerp(0.35f, 0.75f, 1f - perpDist / 28f);
            uint col = ImGui.GetColorU32(GripColor(isActive, isHovered, alpha));
            float halfLen = isActive || isHovered ? 24f : 18f;
            if (isHorizontal)
                draw.AddRectFilled(new NumVector2(mid.X - halfLen, mid.Y - 2.5f), new NumVector2(mid.X + halfLen, mid.Y + 2.5f), col, 2.5f);
            else
                draw.AddRectFilled(new NumVector2(mid.X - 2.5f, mid.Y - halfLen), new NumVector2(mid.X + 2.5f, mid.Y + halfLen), col, 2.5f);
        }

        private static NumVector4 GripColor(bool isActive, bool isHovered, float alpha)
        {
            return isActive ? new NumVector4(0.85f, 0.95f, 1f, alpha)
                : isHovered ? new NumVector4(0.38f, 0.82f, 1f, alpha)
                : new NumVector4(0.35f, 0.72f, 0.98f, alpha);
        }

        private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp(t, 0f, 1f);
    }
}
