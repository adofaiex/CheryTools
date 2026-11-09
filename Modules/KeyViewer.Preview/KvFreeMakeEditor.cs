using System;
using System.Collections.Generic;
using ImGuiNET;
using CheryTools.Sonnet.Contracts;
using UnityEngine;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;
using CheryTools.Sonnet.Shared;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.KeyViewer.Preview
{
    internal static class KvFreeMakeEditor
    {
        private static bool _open;
        private static string _profileId;
        private static readonly HashSet<string> Selected = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Vector2> DragOrigins = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        private static readonly List<List<KvKey>> Undo = new List<List<KvKey>>();
        private static readonly List<KvKey> Clipboard = new List<KvKey>();
        private static readonly KeyCode[] KeyCandidates = BuildKeyCandidates();
        private static float _zoom = 1f;
        private static NumVector2 _pan;
        private static bool _dragging;
        private static NumVector2 _dragStart;
        private static bool _boxSelecting;
        private static NumVector2 _boxStart;
        private static NumVector2 _boxEnd;
        private static bool _placingDuplicates;
        private static NumVector2 _duplicateStartMouse;
        private static NumVector2 _windowPosition;
        private static NumVector2 _windowSize;
        private static bool _windowPositionInitialized;
        private static string _capturingKeyId;
        private static float _inspectorWidth = 310f;
        private static readonly List<KvKey> HitNodes = new List<KvKey>();
        private static readonly List<KvKey> DrawNodes = new List<KvKey>();
        private static readonly Dictionary<string, Texture2D> PreviewImages = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private struct SnapGuide
        {
            internal bool Vertical;
            internal float Value;
        }
        private static readonly List<SnapGuide> SnapGuides = new List<SnapGuide>(2);
        private static float _lastCanvasClickTime = -10f;
        private static NumVector2 _lastCanvasClickPosition;
        private const float CanvasDoubleClickTime = 0.35f;
        private const float CanvasDoubleClickDistance = 8f;

        internal static bool IsOpen => _open;

        internal static void Open(KvProfile profile)
        {
            if (profile == null) return;
            _profileId = profile.Id;
            _open = true;
            Selected.Clear();
            Undo.Clear();
            _zoom = 1f;
            _pan = NumVector2.Zero;
        }

        internal static void Draw(KvSettings settings)
        {
            if (!_open || settings == null) return;
            KvProfile profile = settings.FindProfile(_profileId);
            if (profile == null) { _open = false; return; }
            CaptureKey(profile);

            NumVector2 display = ImGui.GetIO().DisplaySize;
            // Framework 主面板使用 8.5% 的屏幕页边距。FreeMake 必须完整落在
            // 主面板内部，否则 Framework 最后绘制的外边框会穿过编辑器窗口。
            float shellMarginX = Mathf.Max(52f, display.X * 0.085f);
            float shellMarginY = Mathf.Max(38f, display.Y * 0.085f);
            float availableWidth = Mathf.Max(760f, display.X - (shellMarginX + 24f) * 2f);
            float availableHeight = Mathf.Max(540f, display.Y - (shellMarginY + 24f) * 2f);
            if (!_windowPositionInitialized)
            {
                _windowSize = new NumVector2(Mathf.Min(1740f, availableWidth), Mathf.Min(980f, availableHeight));
                _windowPosition = (display - _windowSize) * 0.5f;
                _windowPositionInitialized = true;
            }
            _windowSize = SonnetWindowChrome.ClampSize(_windowSize, display, new NumVector2(760f, 540f), 8f);
            NumVector2 windowSize = _windowSize;
            _windowPosition = SonnetWindowChrome.ClampPosition(_windowPosition, windowSize, display, 8f);
            ImGui.SetNextWindowPos(_windowPosition, ImGuiCond.Always);
            ImGui.SetNextWindowSize(windowSize, ImGuiCond.Always);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 16f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, NumVector2.Zero);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new NumVector4(0.006f, 0.010f, 0.016f, 0.96f));
            bool open = _open;
            ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
            if (!ImGui.Begin("##SonnetKvFreeMake", ref open, flags))
            {
                ImGui.End();
                ImGui.PopStyleColor();
                ImGui.PopStyleVar(2);
                _open = open;
                return;
            }
            _open = open;
            DrawSynchronizedBackground(windowSize);
            DrawWindowChrome(profile, windowSize, ref open);
            _open = open;
            if (_placingDuplicates && Input.GetKeyDown(KeyCode.Escape)) UndoLast(profile);
            ImGui.SetCursorPos(new NumVector2(0f, 66f));
            DrawToolbar(profile, windowSize.X);
            ImGui.SetCursorPos(new NumVector2(18f, 122f));
            NumVector2 available = ImGui.GetContentRegionAvail();
            available -= new NumVector2(18f, 18f);
            float inspectorWidth = Mathf.Clamp(_inspectorWidth, 278f, Mathf.Min(440f, available.X * 0.42f));
            float canvasWidth = Mathf.Max(380f, available.X - inspectorWidth - 14f);
            DrawCanvas(profile, new NumVector2(canvasWidth, available.Y));
            ImGui.SameLine(0f, 4f);
            DrawInspectorSplitter(available.Y);
            ImGui.SameLine(0f, 0f);
            DrawInspector(profile, new NumVector2(inspectorWidth, available.Y));
            ImGui.End();
            SonnetWindowChrome.DrawResizeHandles("FreeMake", ref _windowPosition, ref _windowSize, display, new NumVector2(760f, 540f));
            ImGui.PopStyleColor();
            ImGui.PopStyleVar(2);
            if (!_open) ReleasePreviewImages();
        }

        private static void DrawSynchronizedBackground(NumVector2 size)
        {
            IModuleVisualContext visuals = KvRuntime.Visuals;
            ModuleBackgroundInfo background;
            if (visuals == null || !visuals.TryGetPanelBackground(out background) || background.TextureId == IntPtr.Zero) return;
            NumVector2 min = ImGui.GetWindowPos();
            NumVector2 max = min + size;
            NumVector2 uv0;
            NumVector2 uv1;
            SonnetWindowChrome.CalculateCoverUv(background.TextureWidth, background.TextureHeight, size.X, size.Y, out uv0, out uv1);
            if (background.FlipY)
            {
                float y = uv0.Y;
                uv0.Y = uv1.Y;
                uv1.Y = y;
            }
            ImDrawListPtr draw = ImGui.GetWindowDrawList();
            draw.AddImageRounded(background.TextureId, min, max, uv0, uv1,
                ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, background.ImageOpacity)), 16f, ImDrawFlags.RoundCornersAll);
            draw.AddRectFilled(min, max,
                ImGui.GetColorU32(new NumVector4(0.006f, 0.010f, 0.016f, background.PanelOpacity)), 16f);
        }

        private static void DrawWindowChrome(KvProfile profile, NumVector2 size, ref bool open)
        {
            NumVector2 origin = ImGui.GetWindowPos();
            ImDrawListPtr draw = ImGui.GetWindowDrawList();
            draw.AddRectFilled(origin, origin + new NumVector2(size.X, 66f),
                ImGui.GetColorU32(new NumVector4(0.012f, 0.019f, 0.030f, 0.84f)), 16f, ImDrawFlags.RoundCornersTop);
            draw.AddLine(origin + new NumVector2(20f, 65.5f), origin + new NumVector2(size.X - 20f, 65.5f),
                ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, 0.10f)), 1f);
            ImDrawListPtr borderDraw = ImGui.GetForegroundDrawList();
            borderDraw.AddRect(origin + new NumVector2(0.5f), origin + size - new NumVector2(0.5f),
                ImGui.GetColorU32(new NumVector4(0.94f, 0.97f, 1f, 0.28f)), 15.5f, ImDrawFlags.RoundCornersAll, 1f);
            borderDraw.AddRect(origin + new NumVector2(1.5f), origin + size - new NumVector2(1.5f),
                ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, 0.055f)), 14.5f, ImDrawFlags.RoundCornersAll, 1f);

            draw.AddText(origin + new NumVector2(24f, 15f), ImGui.GetColorU32(SonnetPalette.Text), "FreeMake");
            draw.AddText(origin + new NumVector2(24f, 38f), ImGui.GetColorU32(SonnetPalette.Muted),
                profile.Name + L("  ·  自由布局编辑器", "  ·  FreeMake layout editor"));
            string help = L("Ctrl 多选  ·  中键平移  ·  滚轮缩放", "Ctrl multi-select  ·  Middle drag to pan  ·  Wheel to zoom");
            NumVector2 helpSize = ImGui.CalcTextSize(help);
            if (size.X > 840f)
                draw.AddText(origin + new NumVector2(size.X - helpSize.X - 72f, 24f),
                    ImGui.GetColorU32(SonnetPalette.Muted), help);

            ImGui.SetCursorScreenPos(origin + new NumVector2(8f, 8f));
            ImGui.InvisibleButton("##DragFreeMake", new NumVector2(Mathf.Max(0f, size.X - 74f), 58f));
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                _windowPosition = SonnetWindowChrome.ClampPosition(_windowPosition + ImGui.GetIO().MouseDelta, size, ImGui.GetIO().DisplaySize, 8f);

            NumVector2 closeMin = origin + new NumVector2(size.X - 50f, 17f);
            ImGui.SetCursorScreenPos(closeMin);
            if (ImGui.InvisibleButton("##CloseFreeMake", new NumVector2(32f, 32f))) open = false;
            bool hovered = ImGui.IsItemHovered();
            if (hovered) draw.AddRectFilled(closeMin, closeMin + new NumVector2(32f, 32f),
                ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, 0.055f)), 7f);
            uint closeColor = ImGui.GetColorU32(hovered
                ? SonnetPalette.Text
                : SonnetPalette.Muted);
            draw.AddLine(closeMin + new NumVector2(10f, 10f), closeMin + new NumVector2(22f, 22f), closeColor, 1.6f);
            draw.AddLine(closeMin + new NumVector2(22f, 10f), closeMin + new NumVector2(10f, 22f), closeColor, 1.6f);
        }

        private static void DrawToolbar(KvProfile profile, float width)
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, NumVector4.Zero);
            ImGui.BeginChild("##FreeMakeToolbar", new NumVector2(width, 48f), ImGuiChildFlags.None, ImGuiWindowFlags.NoBackground);
            NumVector2 toolbarOrigin = ImGui.GetWindowPos();
            ImGui.GetWindowDrawList().AddRectFilled(
                toolbarOrigin,
                toolbarOrigin + new NumVector2(width, 48f),
                ImGui.GetColorU32(new NumVector4(0.014f, 0.018f, 0.024f, 0.48f)));
            ImGui.SetCursorPos(new NumVector2(18f, 8f));
            bool compact = width < 1080f;
            if (ToolbarButton("add_key", L("＋ 按键", "+ Key"), 78f, true)) AddNode(profile, KvSettings.CreateKey(KeyCode.A));
            ImGui.SameLine();
            if (ToolbarButton("add_kps", "＋ KPS", 72f, true)) AddNode(profile, KvSettings.CreateStat(1));
            ImGui.SameLine();
            if (ToolbarButton("add_total", L("＋ 总计数", "+ Total"), 92f, true)) AddNode(profile, KvSettings.CreateStat(2));
            ImGui.SameLine();
            if (ToolbarButton("add_image", L("＋ 图片", "+ Image"), 76f, true)) AddNode(profile, CreateMediaNode(3));
            ImGui.SameLine();
            if (ToolbarButton("add_video", L("＋ 视频", "+ Video"), 76f, true)) AddNode(profile, CreateMediaNode(4));
            ImGui.SameLine(0f, compact ? 10f : 20f);
            if (ToolbarButton("duplicate", compact ? L("复制", "Copy") : L("复制  Shift+D", "Copy  Shift+D"), compact ? 62f : 126f, false, Selected.Count > 0)) BeginDuplicate(profile);
            ImGui.SameLine();
            if (ToolbarButton("delete", compact ? L("删除", "Delete") : L("删除  Del", "Delete  Del"), compact ? 62f : 98f, false, Selected.Count > 0)) DeleteSelected(profile);
            ImGui.SameLine();
            if (ToolbarButton("undo", compact ? L("撤销", "Undo") : L("撤销  Ctrl+Z", "Undo  Ctrl+Z"), compact ? 62f : 118f, false, Undo.Count > 0)) UndoLast(profile);
            ImGui.GetWindowDrawList().AddLine(
                toolbarOrigin + new NumVector2(18f, 47.5f),
                toolbarOrigin + new NumVector2(width - 18f, 47.5f),
                ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, 0.075f)), 1f);
            ImGui.EndChild();
            ImGui.PopStyleColor();
        }

        private static bool ToolbarButton(string id, string label, float width, bool accent, bool enabled = true)
        {
            NumVector2 min = ImGui.GetCursorScreenPos();
            NumVector2 size = new NumVector2(Mathf.Max(width, ImGui.CalcTextSize(label).X + 20f), 32f);
            ImGui.InvisibleButton("##FreeMakeToolbar_" + id, size);
            bool hovered = enabled && ImGui.IsItemHovered();
            bool active = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
            ImDrawListPtr draw = ImGui.GetWindowDrawList();
            NumVector4 fill = accent
                ? active ? SonnetPalette.Accent(0.25f) : hovered ? SonnetPalette.Accent(0.17f) : SonnetPalette.Accent(0.085f)
                : active ? new NumVector4(1f, 1f, 1f, 0.085f) : hovered ? new NumVector4(1f, 1f, 1f, 0.055f) : new NumVector4(1f, 1f, 1f, 0.022f);
            if (!enabled) fill.W *= 0.42f;
            draw.AddRectFilled(min, min + size, ImGui.GetColorU32(fill), 7f);
            NumVector2 text = ImGui.CalcTextSize(label);
            draw.AddText(min + new NumVector2((size.X - text.X) * 0.5f, (size.Y - text.Y) * 0.5f),
                ImGui.GetColorU32(enabled ? SonnetPalette.Text : SonnetPalette.Muted), label);
            return enabled && ImGui.IsItemClicked(ImGuiMouseButton.Left);
        }

        private static void DrawCanvas(KvProfile profile, NumVector2 size)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 12f);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new NumVector4(0.004f, 0.008f, 0.014f, 0.82f));
            ImGui.BeginChild("##FreeMakeCanvas", size, ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            NumVector2 min = ImGui.GetWindowPos();
            NumVector2 canvasSize = ImGui.GetWindowSize();
            NumVector2 max = min + canvasSize;
            ImGui.InvisibleButton("##FreeMakeCanvasInput", canvasSize);
            bool hovered = ImGui.IsItemHovered();
            ImDrawListPtr draw = ImGui.GetWindowDrawList();
            draw.AddRect(min + new NumVector2(0.5f), max - new NumVector2(0.5f),
                ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, 0.11f)), 11.5f, ImDrawFlags.RoundCornersAll, 1f);
            draw.PushClipRect(min, max, true);
            DrawGrid(draw, min, max);
            NumVector2 center = min + canvasSize * 0.5f + _pan;

            if (hovered && Mathf.Abs(ImGui.GetIO().MouseWheel) > 0.001f)
            {
                NumVector2 mouse = ImGui.GetIO().MousePos;
                float oldZoom = _zoom;
                _zoom = Mathf.Clamp(_zoom * (ImGui.GetIO().MouseWheel > 0f ? 1.12f : 0.89f), 0.18f, 4f);
                NumVector2 before = (mouse - center) / oldZoom;
                _pan += before * (oldZoom - _zoom);
                center = min + canvasSize * 0.5f + _pan;
            }
            if (hovered && ImGui.IsMouseDragging(ImGuiMouseButton.Middle)) _pan += ImGui.GetIO().MouseDelta;

            HandleShortcuts(profile, hovered);
            HandlePointer(profile, hovered, center);
            DrawSnapGuides(draw, min, max, center, profile);
            DrawScreenBounds(draw, profile, center);
            DrawNodes.Clear();
            DrawNodes.AddRange(profile.Keys);
            DrawNodes.Sort((left, right) =>
            {
                int depth = left.Depth.CompareTo(right.Depth);
                if (depth != 0) return depth;
                return profile.Keys.IndexOf(left).CompareTo(profile.Keys.IndexOf(right));
            });
            for (int i = 0; i < DrawNodes.Count; i++) DrawNode(draw, profile, DrawNodes[i], center);
            if (_boxSelecting)
            {
                NumVector2 boxMin = new NumVector2(Math.Min(_boxStart.X, _boxEnd.X), Math.Min(_boxStart.Y, _boxEnd.Y));
                NumVector2 boxMax = new NumVector2(Math.Max(_boxStart.X, _boxEnd.X), Math.Max(_boxStart.Y, _boxEnd.Y));
                draw.AddRectFilled(boxMin, boxMax, ImGui.GetColorU32(SonnetPalette.Accent(0.1f)));
                draw.AddRect(boxMin, boxMax, ImGui.GetColorU32(SonnetPalette.Accent(0.82f)), 0f, ImDrawFlags.None, 1.5f);
            }
            draw.PopClipRect();
            ImGui.EndChild();
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();
        }

        private static void HandlePointer(KvProfile profile, bool hovered, NumVector2 center)
        {
            NumVector2 mouse = ImGui.GetIO().MousePos;
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (_placingDuplicates)
            {
                ApplySnappedDrag(profile, mouse, _duplicateStartMouse, center);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    _placingDuplicates = false;
                    SnapGuides.Clear();
                    KvRuntime.Changed(false);
                }
                return;
            }

            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                CollectHitNodes(profile, center, mouse, HitNodes);
                bool doubleClick = ConsumeCanvasDoubleClick(mouse);
                KvKey hit = PickNodeForCanvasClick(HitNodes, doubleClick, ctrl);
                if (hit != null)
                {
                    if (doubleClick)
                    {
                        Selected.Clear();
                        Selected.Add(hit.Id);
                    }
                    else if (ctrl)
                    {
                        if (!Selected.Add(hit.Id)) Selected.Remove(hit.Id);
                    }
                    else if (!Selected.Contains(hit.Id))
                    {
                        Selected.Clear();
                        Selected.Add(hit.Id);
                    }
                    if (!ctrl && !doubleClick)
                    {
                        PushUndo(profile);
                        BeginDrag(profile, mouse);
                    }
                }
                else
                {
                    if (!ctrl) Selected.Clear();
                    _boxSelecting = true;
                    _boxStart = _boxEnd = mouse;
                }
            }

            if (_dragging && ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                ApplySnappedDrag(profile, mouse, _dragStart, center);
            }
            if (_dragging && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                _dragging = false;
                SnapGuides.Clear();
                KvRuntime.Changed(false);
            }
            if (_boxSelecting && ImGui.IsMouseDown(ImGuiMouseButton.Left)) _boxEnd = mouse;
            if (_boxSelecting && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                _boxSelecting = false;
                SelectInBox(profile, center, ctrl);
            }
        }

        private static void HandleShortcuts(KvProfile profile, bool hovered)
        {
            if (!hovered || ImGui.GetIO().WantTextInput) return;
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (ctrl && Input.GetKeyDown(KeyCode.A))
            {
                Selected.Clear();
                foreach (KvKey node in profile.Keys) if (!node.Locked) Selected.Add(node.Id);
            }
            if (ctrl && Input.GetKeyDown(KeyCode.C)) CopySelected(profile);
            if (ctrl && Input.GetKeyDown(KeyCode.V)) PasteClipboard(profile);
            if (ctrl && Input.GetKeyDown(KeyCode.Z)) UndoLast(profile);
            if (shift && Input.GetKeyDown(KeyCode.D) && Selected.Count > 0) BeginDuplicate(profile);
            if (Input.GetKeyDown(KeyCode.Delete) && Selected.Count > 0) DeleteSelected(profile);
        }

        private static void DrawNode(ImDrawListPtr draw, KvProfile profile, KvKey node, NumVector2 center)
        {
            NumVector2 position = ToScreen(profile, node, center);
            float scale = CanvasScale(profile);
            float width = KvGeometry.Width(profile, node) * scale;
            float height = KvGeometry.Height(profile, node) * scale;
            NumVector2 min = position - new NumVector2(width, height) * 0.5f;
            NumVector2 max = position + new NumVector2(width, height) * 0.5f;
            bool selected = Selected.Contains(node.Id);
            bool pressed = KvSettings.IsInputNode(node) && Input.GetKey(node.Bind);
            float[] fill = node.UseCustomStyle
                ? (pressed ? node.BackgroundPressed : node.BackgroundNormal)
                : (pressed ? profile.BackgroundPressed : profile.BackgroundNormal);
            float[] border = node.UseCustomStyle
                ? (pressed ? node.BorderPressed : node.BorderNormal)
                : (pressed ? profile.BorderPressed : profile.BorderNormal);
            float radius = KvGeometry.Resolve(node.CornerRadius, profile.CornerRadius) * scale;
            float borderThickness = KvGeometry.Resolve(node.BorderThickness, profile.BorderThickness);
            draw.AddRectFilled(min, max, Color(fill, node.Opacity), radius);
            if (KvSettings.IsMediaNode(node))
            {
                DrawMediaContent(draw, profile, node, position, width, height, borderThickness, scale);
                // A transparent key would vanish from the editor; keep a faint outline to grab it by.
                draw.AddRect(min, max, selected ? ImGui.GetColorU32(SonnetPalette.Accent(1f)) : ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, 0.16f)),
                    radius, ImDrawFlags.RoundCornersAll, selected ? 2.5f : 1f);
                return;
            }
            draw.AddRect(min, max, selected ? ImGui.GetColorU32(SonnetPalette.Accent(1f)) : Color(border, node.Opacity), radius,
                ImDrawFlags.RoundCornersAll, selected ? 2.5f : Mathf.Max(1f, borderThickness * scale));
            float[] textColor = node.UseCustomStyle
                ? (pressed ? node.TextPressed : node.TextNormal)
                : (pressed ? profile.TextPressed : profile.TextNormal);
            string label = node.NodeType == 1 ? "KPS" : node.NodeType == 2 ? "TOTAL" : node.Label ?? string.Empty;
            bool statistic = node.NodeType == 1 || node.NodeType == 2;
            bool showText = statistic || KvSettings.IsInputNode(node) || !string.IsNullOrEmpty(label);
            if (!showText) return;
            int countValue = node.NodeType == 1 ? profile.CurrentKps : node.NodeType == 2 ? profile.TotalHits : node.HitCount;
            bool hideCount = KvSettings.HidesCount(profile, node);
            float labelFontSize = Mathf.Max(1f, KvGeometry.Resolve(node.LabelSize, profile.LabelSize) * scale);
            float countFontSize = Mathf.Max(1f, KvGeometry.Resolve(node.CountSize, profile.CountSize) * scale);
            NumVector2 labelTextSize = ImGui.GetFont().CalcTextSizeA(labelFontSize, float.MaxValue, 0f, label);
            float labelBoxHeight = labelFontSize * 1.25f;
            float labelX = position.X - labelTextSize.X * 0.5f + node.LabelOffsetX * scale;
            float labelY = hideCount
                ? min.Y + (height - labelBoxHeight) * 0.5f - node.LabelOffsetY * scale
                : min.Y + 5f * scale - node.LabelOffsetY * scale;
            draw.AddText(ImGui.GetFont(), labelFontSize, new NumVector2(labelX, labelY), Color(textColor, node.Opacity), label);
            if (!hideCount)
            {
                string count = countValue.ToString();
                NumVector2 countTextSize = ImGui.GetFont().CalcTextSizeA(countFontSize, float.MaxValue, 0f, count);
                float countX = node.CountAlignment == 0 ? min.X + node.CountOffsetX * scale
                    : node.CountAlignment == 2 ? max.X - countTextSize.X + node.CountOffsetX * scale
                    : position.X - countTextSize.X * 0.5f + node.CountOffsetX * scale;
                float countY = max.Y - countFontSize * 1.25f - 5f * scale - node.CountOffsetY * scale;
                draw.AddText(ImGui.GetFont(), countFontSize, new NumVector2(countX, countY), Color(textColor, node.Opacity), count);
            }
        }

        // Draws the same image the game shows, placed by the same layout code. Videos reuse the live
        // render texture when the profile is on screen, otherwise a label stands in.
        private static void DrawMediaContent(ImDrawListPtr draw, KvProfile profile, KvKey node, NumVector2 position,
            float width, float height, float borderThickness, float scale)
        {
            Texture texture = null;
            if (node.NodeType == 3) texture = GetPreviewImage(node.ImagePath);
            else KvRuntime.Host?.TryGetMediaTexture(profile.Id, node.Id, out texture);
            IntPtr textureId = texture != null && KvRuntime.Visuals != null ? KvRuntime.Visuals.RegisterTexture(texture) : IntPtr.Zero;
            if (textureId == IntPtr.Zero)
            {
                string placeholder = node.NodeType == 3 ? L("图片", "Image") : L("视频", "Video");
                draw.AddText(position - ImGui.CalcTextSize(placeholder) * 0.5f, ImGui.GetColorU32(SonnetPalette.Muted), placeholder);
                return;
            }

            bool video = node.NodeType == 4;
            float inset = Mathf.Max(0f, borderThickness) * 2f * scale;
            KvMediaLayout layout = KvGeometry.MediaLayout(video && node.MediaFit != 4 ? 0 : node.MediaFit,
                Mathf.Max(1f, width - inset), Mathf.Max(1f, height - inset),
                video ? 0f : texture.width, video ? 0f : texture.height,
                node.MediaScale, scale, node.MediaOffsetX * scale, node.MediaOffsetY * scale);
            NumVector2 center = position + new NumVector2(layout.X, -layout.Y);
            NumVector2 half = new NumVector2(layout.Width, layout.Height) * 0.5f;
            draw.AddImage(textureId, center - half, center + half,
                new NumVector2(layout.UvX, layout.UvY + layout.UvHeight), new NumVector2(layout.UvX + layout.UvWidth, layout.UvY),
                ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, Mathf.Clamp01(node.Opacity))));
        }

        private static Texture2D GetPreviewImage(string rawPath)
        {
            string path = KvRuntimeHost.ResolveMediaPath(rawPath);
            if (string.IsNullOrEmpty(path)) return null;
            if (PreviewImages.TryGetValue(path, out Texture2D cached)) return cached;
            Texture2D loaded = System.IO.File.Exists(path) ? KvRuntimeHost.LoadPreviewImage(path) : null;
            // Missing files are cached too so the canvas does not hit the disk every frame.
            PreviewImages[path] = loaded;
            return loaded;
        }

        private static void ReleasePreviewImages()
        {
            foreach (Texture2D texture in PreviewImages.Values)
            {
                if (texture == null) continue;
                KvRuntime.Visuals?.UnregisterTexture(texture);
                UnityEngine.Object.Destroy(texture);
            }
            PreviewImages.Clear();
        }

        private static void DrawInspector(KvProfile profile, NumVector2 size)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 12f);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new NumVector4(0.010f, 0.016f, 0.025f, 0.80f));
            ImGui.BeginChild("##FreeMakeInspector", size, ImGuiChildFlags.None);
            NumVector2 cardMin = ImGui.GetWindowPos();
            ImGui.GetWindowDrawList().AddRect(cardMin + new NumVector2(0.5f), cardMin + size - new NumVector2(0.5f),
                ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, 0.10f)), 11.5f, ImDrawFlags.RoundCornersAll, 1f);
            ImGui.SetCursorPos(new NumVector2(18f, 16f));
            ImGui.BeginChild("##FreeMakeInspectorInner", size - new NumVector2(36f, 32f), ImGuiChildFlags.None, ImGuiWindowFlags.NoBackground);
            ImGui.TextColored(SonnetPalette.Text, L("节点属性", "Node properties"));
            ImGui.SameLine();
            ImGui.TextColored(SonnetPalette.Accent(1f), Selected.Count + L(" 个选中", " selected"));
            ImGui.Separator();
            ImGui.Spacing();
            InspectorSection(L("配置默认字体", "Profile font"));
            string profileFont = profile.FontPath ?? string.Empty;
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.InputTextWithHint("##FreeMakeProfileFont", L("留空使用游戏默认字体", "Leave empty for the game font"), ref profileFont, 1024))
            {
                profile.FontPath = profileFont;
                KvRuntime.Changed(false);
            }
            if (ImGui.IsItemDeactivatedAfterEdit()) KvRuntime.Changed();
            if (InspectorMiniButton("pick_profile_font", L("选择字体", "Choose font"), 92f))
            {
                string picked = PickFont(profileFont);
                if (!string.IsNullOrEmpty(picked)) { profile.FontPath = picked; KvRuntime.Changed(); }
            }
            ImGui.SameLine();
            if (InspectorMiniButton("clear_profile_font", L("恢复默认", "Reset"), 92f) && !string.IsNullOrEmpty(profile.FontPath))
            {
                profile.FontPath = string.Empty;
                KvRuntime.Changed();
            }
            ImGui.Spacing();
            List<KvKey> selectedNodes = GetSelectedNodes(profile);
            KvKey node = selectedNodes.Count == 1 ? selectedNodes[0] : null;
            if (selectedNodes.Count == 0)
            {
                ImGui.TextWrapped(L("选择一个或多个节点以编辑属性。", "Select one or more nodes to edit their properties."));
            }
            else if (selectedNodes.Count > 1)
            {
                DrawBatchInspector(profile, selectedNodes);
                ImGui.Spacing();
                if (WideActionButton("export_selection", L("导出所选组件", "Export selection")))
                {
                    KvPackageService.ExportNodes(selectedNodes);
                }
            }
            else
            {
                string typeName = node.NodeType == 0 ? L("按键节点", "Key node") : node.NodeType == 1 ? L("KPS 节点", "KPS node") : node.NodeType == 2 ? L("总计数节点", "Total node")
                    : node.NodeType == 3 ? L("图片按键节点", "Image key node") : L("视频按键节点", "Video key node");
                ImGui.TextDisabled(typeName);
                int nodeType = node.NodeType;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.Combo(L("节点类型", "Node type"), ref nodeType, L("普通按键\0KPS\0总计数\0图片按键\0视频按键\0", "Key\0KPS\0Total\0Image key\0Video key\0")))
                {
                    node.NodeType = nodeType;
                    if (nodeType == 1 || nodeType == 2)
                    {
                        node.Bind = KeyCode.None;
                        node.Label = nodeType == 1 ? "KPS" : "TOTAL";
                        node.RainEnabled = false;
                        node.RainRow = 0;
                    }
                    else
                    {
                        if (node.Bind == KeyCode.None) node.Bind = KeyCode.A;
                        if (string.IsNullOrWhiteSpace(node.Label) || node.Label == "KPS" || node.Label == "TOTAL")
                            node.Label = KvSettings.KeyName(node.Bind);
                        if (node.RainRow == 0) node.RainRow = 1;
                    }
                    KvRuntime.Changed();
                }
                ImGui.Spacing();
                bool isMedia = KvSettings.IsMediaNode(node);
                if (KvSettings.IsInputNode(node) || node.NodeType == 1 || node.NodeType == 2)
                {
                    if (!isMedia)
                    {
                        string label = node.Label ?? string.Empty;
                        ImGui.TextDisabled(L("显示文字", "Label"));
                        ImGui.SetNextItemWidth(-1f);
                        if (ImGui.InputText("##FreeMakeNodeLabel", ref label, 24)) { node.Label = label; KvRuntime.Changed(false); }
                    }
                    string bindText = string.Equals(_capturingKeyId, node.Id, StringComparison.Ordinal)
                        ? L("请按下按键 · Esc 取消", "Press a key · Esc to cancel") : L("绑定按键 · ", "Bound key · ") + KvSettings.KeyName(node.Bind);
                    if (WideActionButton("bind_" + node.Id, bindText)) _capturingKeyId = node.Id;
                }
                if (node.NodeType == 3 || node.NodeType == 4)
                {
                    string path = node.NodeType == 3 ? node.ImagePath ?? string.Empty : node.VideoPath ?? string.Empty;
                    ImGui.TextDisabled(node.NodeType == 3 ? L("图片路径", "Image path") : L("视频路径", "Video path"));
                    ImGui.SetNextItemWidth(-1f);
                    if (ImGui.InputText("##FreeMakeMediaPath", ref path, 1024))
                    {
                        if (node.NodeType == 3) node.ImagePath = path; else node.VideoPath = path;
                        KvRuntime.Changed();
                    }
                    string mediaFilter = node.NodeType == 3
                        ? L("图片文件 (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件 (*.*)|*.*", "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*")
                        : L("视频文件 (*.mp4;*.webm;*.mov;*.avi;*.ogv)|*.mp4;*.webm;*.mov;*.avi;*.ogv|所有文件 (*.*)|*.*", "Video files (*.mp4;*.webm;*.mov;*.avi;*.ogv)|*.mp4;*.webm;*.mov;*.avi;*.ogv|All files (*.*)|*.*");
                    if (WideActionButton("pick_media_" + node.Id, L("选择", "Choose ") + (node.NodeType == 3 ? L("图片", "image") : L("视频", "video")) + L("文件", " file")))
                    {
                        string picked = ModernFileDialog.ShowOpenFileDialog(node.NodeType == 3 ? L("选择图片", "Choose image") : L("选择视频", "Choose video"), mediaFilter,
                            string.IsNullOrWhiteSpace(path) ? Environment.CurrentDirectory : System.IO.Path.GetDirectoryName(path));
                        if (!string.IsNullOrEmpty(picked))
                        {
                            if (node.NodeType == 3) node.ImagePath = picked; else node.VideoPath = picked;
                            KvRuntime.Changed();
                        }
                    }
                    int mediaFit = node.MediaFit;
                    ImGui.TextDisabled(L("填充方式", "Fill mode"));
                    ImGui.SetNextItemWidth(-1f);
                    if (ImGui.Combo("##FreeMakeMediaFit", ref mediaFit, MediaFitItems(node.NodeType))) { node.MediaFit = mediaFit; KvRuntime.Changed(false); }
                    float mediaScale = node.MediaScale;
                    if (LabeledStepFloat("media_scale", L("内容缩放", "Content scale"), ref mediaScale, 0.1f, 0.05f, 10f)) { node.MediaScale = mediaScale; KvRuntime.Changed(false); }
                    if (LabeledDragFloat("media_offset_x", L("内容偏移 X", "Content offset X"), ref node.MediaOffsetX, -2000f, 2000f)) KvRuntime.Changed(false);
                    if (LabeledDragFloat("media_offset_y", L("内容偏移 Y", "Content offset Y"), ref node.MediaOffsetY, -2000f, 2000f)) KvRuntime.Changed(false);
                    if (node.NodeType == 4)
                    {
                        bool loop = node.VideoLoop;
                        if (ImGui.Checkbox(L("循环播放", "Loop"), ref loop)) { node.VideoLoop = loop; KvRuntime.Changed(false); }
                    }
                }
                InspectorSection(L("几何与层级", "Geometry and layer"));
                float x = node.PositionX, y = node.PositionY;
                if (LabeledDragFloat("node_x", L("位置 X", "Position X"), ref x, -10000f, 10000f)) { node.PositionX = x; KvRuntime.Changed(false); }
                if (LabeledDragFloat("node_y", L("位置 Y", "Position Y"), ref y, -10000f, 10000f)) { node.PositionY = y; KvRuntime.Changed(false); }
                float width = KvGeometry.Width(profile, node), height = KvGeometry.Height(profile, node);
                if (LabeledDragFloat("node_width", L("宽度", "Width"), ref width, 1f, 1200f)) { node.Width = width; KvRuntime.Changed(false); }
                if (LabeledDragFloat("node_height", L("高度", "Height"), ref height, 1f, 1200f)) { node.Height = height; KvRuntime.Changed(false); }
                if (LabeledPercent("node_opacity", L("透明度", "Opacity"), ref node.Opacity)) KvRuntime.Changed(false);
                int depth = node.Depth;
                if (LabeledStepInt("node_depth", L("渲染层级", "Render order"), ref depth, -1000, 1000)) { node.Depth = depth; KvRuntime.Changed(); }
                bool locked = node.Locked;
                if (ImGui.Checkbox(L("锁定节点", "Lock node"), ref locked)) { node.Locked = locked; KvRuntime.Changed(false); }
                if (!isMedia)
                {
                    int hitCount = node.HitCount;
                    if (LabeledDragInt("node_hit_count", L("击键统计 (HitCount)", "Hit count"), ref hitCount, 0, 99999999))
                    {
                        node.HitCount = Mathf.Max(0, hitCount);
                        KvRuntime.Changed(false);
                    }
                }

                DrawNodeStyle(profile, node, isMedia);

                if (!isMedia)
                {
                InspectorSection(L("文字", "Text"));
                float labelSize = KvGeometry.Resolve(node.LabelSize, profile.LabelSize);
                if (LabeledDragFloat("label_size", L("主文字大小", "Label size"), ref labelSize, 8f, 240f)) { node.LabelSize = labelSize; KvRuntime.Changed(false); }
                float countSize = KvGeometry.Resolve(node.CountSize, profile.CountSize);
                if (LabeledDragFloat("count_size", L("计数大小", "Count size"), ref countSize, 8f, 240f)) { node.CountSize = countSize; KvRuntime.Changed(false); }
                ImGui.TextDisabled(L("主文字字体（留空继承配置）", "Label font (empty uses the profile font)"));
                string labelFont = node.LabelFontPath ?? string.Empty;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.InputText("##FreeMakeLabelFont", ref labelFont, 1024)) { node.LabelFontPath = labelFont; KvRuntime.Changed(false); }
                if (ImGui.IsItemDeactivatedAfterEdit()) KvRuntime.Changed();
                if (InspectorMiniButton("pick_label_font", L("选择主文字字体", "Choose label font"), 132f))
                {
                    string picked = PickFont(labelFont);
                    if (!string.IsNullOrEmpty(picked)) { node.LabelFontPath = picked; KvRuntime.Changed(); }
                }
                ImGui.SameLine();
                if (InspectorMiniButton("clear_label_font", L("继承配置", "Use profile"), 92f) && !string.IsNullOrEmpty(node.LabelFontPath))
                { node.LabelFontPath = string.Empty; KvRuntime.Changed(); }
                ImGui.TextDisabled(L("计数字体（留空继承配置）", "Count font (empty uses the profile font)"));
                string countFont = node.CountFontPath ?? string.Empty;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.InputText("##FreeMakeCountFont", ref countFont, 1024)) { node.CountFontPath = countFont; KvRuntime.Changed(false); }
                if (ImGui.IsItemDeactivatedAfterEdit()) KvRuntime.Changed();
                if (InspectorMiniButton("pick_count_font", L("选择计数字体", "Choose count font"), 132f))
                {
                    string picked = PickFont(countFont);
                    if (!string.IsNullOrEmpty(picked)) { node.CountFontPath = picked; KvRuntime.Changed(); }
                }
                ImGui.SameLine();
                if (InspectorMiniButton("clear_count_font", L("继承配置", "Use profile"), 92f) && !string.IsNullOrEmpty(node.CountFontPath))
                { node.CountFontPath = string.Empty; KvRuntime.Changed(); }
                if (LabeledDragFloat("label_offset_x", L("主文字偏移 X", "Label offset X"), ref node.LabelOffsetX, -1000f, 1000f)) KvRuntime.Changed(false);
                if (LabeledDragFloat("label_offset_y", L("主文字偏移 Y", "Label offset Y"), ref node.LabelOffsetY, -1000f, 1000f)) KvRuntime.Changed(false);
                if (LabeledDragFloat("count_offset_x", L("计数偏移 X", "Count offset X"), ref node.CountOffsetX, -1000f, 1000f)) KvRuntime.Changed(false);
                if (LabeledDragFloat("count_offset_y", L("计数偏移 Y", "Count offset Y"), ref node.CountOffsetY, -1000f, 1000f)) KvRuntime.Changed(false);
                int countAlignment = node.CountAlignment;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.Combo(L("计数对齐", "Count alignment"), ref countAlignment, L("左对齐\0居中\0右对齐\0", "Left\0Center\0Right\0"))) { node.CountAlignment = countAlignment; KvRuntime.Changed(false); }
                bool hideCount = node.HideCount;
                if (ImGui.Checkbox(L("隐藏计数", "Hide count"), ref hideCount)) { node.HideCount = hideCount; KvRuntime.Changed(false); }

                InspectorSection(L("独立文字效果", "Node text effects"));
                bool customText = node.UseCustomTextEffects;
                if (ImGui.Checkbox(L("覆盖配置文字效果", "Override profile text effects"), ref customText)) { node.UseCustomTextEffects = customText; KvRuntime.Changed(false); }
                if (customText)
                {
                    bool labelOutline = node.LabelOutlineEnabled;
                    if (ImGui.Checkbox(L("主文字描边", "Label outline"), ref labelOutline)) { node.LabelOutlineEnabled = labelOutline; KvRuntime.Changed(false); }
                    bool countOutline = node.CountOutlineEnabled;
                    if (ImGui.Checkbox(L("计数描边", "Count outline"), ref countOutline)) { node.CountOutlineEnabled = countOutline; KvRuntime.Changed(false); }
                    if (labelOutline && InspectorColor(L("主文字描边颜色", "Label outline color"), "node_label_outline", node.LabelOutlineColor)) KvRuntime.Changed(false);
                    if (countOutline && InspectorColor(L("计数描边颜色", "Count outline color"), "node_count_outline", node.CountOutlineColor)) KvRuntime.Changed(false);
                    if (labelOutline && LabeledDragFloat("label_outline_width", L("主文字描边粗细", "Label outline thickness"), ref node.LabelOutlineThickness, 0f, 8f)) KvRuntime.Changed(false);
                    if (countOutline && LabeledDragFloat("count_outline_width", L("计数描边粗细", "Count outline thickness"), ref node.CountOutlineThickness, 0f, 8f)) KvRuntime.Changed(false);
                    bool labelShadow = node.LabelShadowEnabled;
                    if (ImGui.Checkbox(L("主文字阴影", "Label shadow"), ref labelShadow)) { node.LabelShadowEnabled = labelShadow; KvRuntime.Changed(false); }
                    bool countShadow = node.CountShadowEnabled;
                    if (ImGui.Checkbox(L("计数阴影", "Count shadow"), ref countShadow)) { node.CountShadowEnabled = countShadow; KvRuntime.Changed(false); }
                    if (labelShadow)
                    {
                        if (InspectorColor(L("主文字阴影颜色", "Label shadow color"), "node_label_shadow", node.LabelShadowColor)) KvRuntime.Changed(false);
                    if (LabeledDragFloatUnbounded("label_shadow_x", L("主文字阴影 X", "Label shadow X"), ref node.LabelShadowOffset[0])) KvRuntime.Changed(false);
                    if (LabeledDragFloatUnbounded("label_shadow_y", L("主文字阴影 Y", "Label shadow Y"), ref node.LabelShadowOffset[1])) KvRuntime.Changed(false);
                    if (LabeledDragFloatUnbounded("label_shadow_softness", L("主文字阴影柔度", "Label shadow softness"), ref node.LabelShadowSoftness, true)) KvRuntime.Changed(false);
                }
                    if (countShadow)
                    {
                        if (InspectorColor(L("计数阴影颜色", "Count shadow color"), "node_count_shadow", node.CountShadowColor)) KvRuntime.Changed(false);
                    if (LabeledDragFloatUnbounded("count_shadow_x", L("计数阴影 X", "Count shadow X"), ref node.CountShadowOffset[0])) KvRuntime.Changed(false);
                    if (LabeledDragFloatUnbounded("count_shadow_y", L("计数阴影 Y", "Count shadow Y"), ref node.CountShadowOffset[1])) KvRuntime.Changed(false);
                    if (LabeledDragFloatUnbounded("count_shadow_softness", L("计数阴影柔度", "Count shadow softness"), ref node.CountShadowSoftness, true)) KvRuntime.Changed(false);
                }
                }

                }

                InspectorSection(L("键雨", "Key rain"));
                if (KvSettings.IsInputNode(node))
                {
                    bool rain = node.RainEnabled;
                    if (ImGui.Checkbox(L("启用此节点键雨", "Key rain for this node"), ref rain))
                    {
                        node.RainEnabled = rain;
                        node.UseCustomRain = true;
                        KvRuntime.Changed(false);
                    }
                    int row = node.RainRow;
                    ImGui.SetNextItemWidth(-1f);
                    if (ImGui.Combo(L("键雨分组", "Rain row"), ref row, L("不显示\0第一排\0第二排\0第三排\0第四排\0", "Off\0Row 1\0Row 2\0Row 3\0Row 4\0"))) { node.RainRow = row; node.RainEnabled = row != 0; KvRuntime.Changed(false); }
                    bool customRain = node.UseCustomRain;
                    if (ImGui.Checkbox(L("独立键雨样式", "Node rain style"), ref customRain)) { node.UseCustomRain = customRain; KvRuntime.Changed(false); }
                    if (customRain)
                    {
                        if (LabeledDragFloat("rain_width", L("宽度比例", "Width ratio"), ref node.RainWidthRatio, 0.05f, 2f)) KvRuntime.Changed(false);
                        if (LabeledDragFloat("rain_y", L("高度偏移", "Height offset"), ref node.RainYOffset, -400f, 400f)) KvRuntime.Changed(false);
                        if (LabeledDragFloat("rain_radius", L("圆角", "Corner radius"), ref node.RainCornerRadius, 0f, 200f)) KvRuntime.Changed(false);
                        if (InspectorColor(L("近端颜色", "Near color"), "node_rain_near", node.RainColor)) KvRuntime.Changed(false);
                        if (InspectorColor(L("远端颜色", "Far color"), "node_rain_far", node.RainEndColor)) KvRuntime.Changed(false);
                        if (InspectorColor(L("右侧颜色", "Right color"), "node_rain_right", node.RainRightColor)) KvRuntime.Changed(false);
                        bool rainGradient = node.RainGradientEnabled;
                        if (ImGui.Checkbox(L("纵向渐变", "Vertical gradient"), ref rainGradient)) { node.RainGradientEnabled = rainGradient; KvRuntime.Changed(false); }
                        if (rainGradient)
                        {
                            int gradientMode = node.RainGradientMode;
                            ImGui.SetNextItemWidth(-1f);
                            if (ImGui.Combo(L("纵向渐变方式", "Vertical gradient mode"), ref gradientMode, L("随雨段渐变（UV）\0按高度渐变\0", "Per segment (UV)\0By height\0")))
                            { node.RainGradientMode = gradientMode; KvRuntime.Changed(false); }
                            if (gradientMode == 1)
                            {
                                if (LabeledDragFloat("rain_gradient_height", L("渐变范围", "Gradient range"), ref node.RainGradientHeight, 0.05f, 3f)) KvRuntime.Changed(false);
                                if (LabeledDragFloat("rain_gradient_power", L("渐变曲线", "Gradient curve"), ref node.RainGradientPower, 0.1f, 5f)) KvRuntime.Changed(false);
                            }
                        }
                        bool rainHorizontal = node.RainHorizontalGradientEnabled;
                        if (ImGui.Checkbox(L("横向渐变", "Horizontal gradient"), ref rainHorizontal)) { node.RainHorizontalGradientEnabled = rainHorizontal; KvRuntime.Changed(false); }
                        int fadeMode = node.RainFadeMode;
                        ImGui.SetNextItemWidth(-1f);
                        if (ImGui.Combo(L("消失方式", "Fade out"), ref fadeMode, L("高度裁剪\0羽化透明\0", "Clip at height\0Feather\0"))) { node.RainFadeMode = fadeMode; KvRuntime.Changed(false); }
                        if (fadeMode == 1)
                        {
                            if (LabeledDragFloat("rain_fade_height", L("羽化范围", "Feather range"), ref node.RainFadeHeight, 0.05f, 3f)) KvRuntime.Changed(false);
                            if (LabeledDragFloat("rain_fade_power", L("羽化程度", "Feather strength"), ref node.RainFadePower, 0.1f, 5f)) KvRuntime.Changed(false);
                        }
                        bool rainOutline = node.RainOutlineEnabled;
                        if (ImGui.Checkbox(L("键雨描边", "Rain outline"), ref rainOutline)) { node.RainOutlineEnabled = rainOutline; KvRuntime.Changed(false); }
                        if (rainOutline)
                        {
                            if (InspectorColor(L("键雨描边颜色", "Rain outline color"), "node_rain_outline", node.RainOutlineColor)) KvRuntime.Changed(false);
                            if (LabeledDragFloat("rain_outline_width", L("键雨描边粗细", "Rain outline thickness"), ref node.RainOutlineThickness, 0f, 10f)) KvRuntime.Changed(false);
                        }
                        bool rainShadow = node.RainShadowEnabled;
                        if (ImGui.Checkbox(L("键雨阴影", "Rain shadow"), ref rainShadow)) { node.RainShadowEnabled = rainShadow; KvRuntime.Changed(false); }
                        if (rainShadow)
                        {
                            if (InspectorColor(L("键雨阴影颜色", "Rain shadow color"), "node_rain_shadow", node.RainShadowColor)) KvRuntime.Changed(false);
                            if (LabeledDragFloatUnbounded("rain_shadow_x", L("键雨阴影 X", "Rain shadow X"), ref node.RainShadowOffset[0])) KvRuntime.Changed(false);
                            if (LabeledDragFloatUnbounded("rain_shadow_y", L("键雨阴影 Y", "Rain shadow Y"), ref node.RainShadowOffset[1])) KvRuntime.Changed(false);
                            if (LabeledDragFloat("rain_shadow_softness", L("键雨阴影柔度", "Rain shadow softness"), ref node.RainShadowSoftness, 0f, 64f)) KvRuntime.Changed(false);
                            if (LabeledDragFloat("rain_shadow_strength", L("键雨阴影强度", "Rain shadow strength"), ref node.RainShadowStrength, 0f, 1f)) KvRuntime.Changed(false);
                        }
                    }
                }

                InspectorSection(L("按键动画", "Press animation"));
                bool customAnimation = node.UseCustomAnimation;
                if (ImGui.Checkbox(L("独立动画", "Node animation"), ref customAnimation)) { node.UseCustomAnimation = customAnimation; KvRuntime.Changed(false); }
                if (customAnimation)
                {
                    bool enabledAnimation = node.PressAnimationEnabled;
                    if (ImGui.Checkbox(L("启用动画", "Enable animation"), ref enabledAnimation)) { node.PressAnimationEnabled = enabledAnimation; KvRuntime.Changed(false); }
                    if (LabeledDragFloat("anim_duration", L("时长", "Duration"), ref node.PressAnimationDuration, 0.01f, 2f)) KvRuntime.Changed(false);
                    if (LabeledDragFloat("anim_scale", L("按下缩放", "Pressed scale"), ref node.PressAnimationScale, 0.2f, 3f)) KvRuntime.Changed(false);
                    if (LabeledDragFloat("anim_x", L("按下偏移 X", "Pressed offset X"), ref node.PressAnimationOffsetX, -200f, 200f)) KvRuntime.Changed(false);
                    if (LabeledDragFloat("anim_y", L("按下偏移 Y", "Pressed offset Y"), ref node.PressAnimationOffsetY, -200f, 200f)) KvRuntime.Changed(false);
                    bool affectColors = node.PressAnimationAffectColors;
                    if (ImGui.Checkbox(L("颜色同步过渡", "Animate colors"), ref affectColors)) { node.PressAnimationAffectColors = affectColors; KvRuntime.Changed(false); }
                    int easing = EasingIndex(node.PressAnimationEasing);
                    ImGui.SetNextItemWidth(-1f);
                    if (ImGui.Combo(L("缓动", "Easing"), ref easing, L("线性\0缓出二次\0缓入二次\0缓入缓出二次\0缓出三次\0缓入缓出三次\0回弹缓出\0", "Linear\0Ease out quad\0Ease in quad\0Ease in-out quad\0Ease out cubic\0Ease in-out cubic\0Ease out back\0")))
                    { node.PressAnimationEasing = EasingName(easing); KvRuntime.Changed(false); }
                }
                ImGui.Spacing();
                if (WideActionButton("export_node", L("导出此组件", "Export this node"))) KvPackageService.ExportNode(node);
            }
            if (!string.IsNullOrEmpty(KvPackageService.LastStatus))
            {
                ImGui.Spacing();
                ImGui.TextWrapped(SonnetWidgets.Plain(KvPackageService.LastStatus));
            }
            ImGui.EndChild();
            ImGui.EndChild();
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();
        }

        private static void DrawNodeStyle(KvProfile profile, KvKey node, bool isMedia)
        {
            InspectorSection(L("背景与边框", "Background and border"));
            if (isMedia && WideActionButton("clear_style_" + node.Id, L("去掉背景和边框", "Remove background and border")))
            {
                node.UseCustomStyle = true;
                node.BorderThickness = 0f;
                node.BackgroundNormal[3] = node.BackgroundPressed[3] = 0f;
                node.BorderNormal[3] = node.BorderPressed[3] = 0f;
                KvRuntime.Changed(false);
            }
            bool custom = node.UseCustomStyle;
            if (ImGui.Checkbox(L("单独设置此节点（不跟随配置）", "Customize this node (ignore profile)"), ref custom)) { node.UseCustomStyle = custom; KvRuntime.Changed(false); }
            if (!custom) return;
            float radius = KvGeometry.Resolve(node.CornerRadius, profile.CornerRadius);
            if (LabeledDragFloat("node_radius", L("圆角", "Corner radius"), ref radius, 0f, 400f)) { node.CornerRadius = radius; KvRuntime.Changed(false); }
            float border = KvGeometry.Resolve(node.BorderThickness, profile.BorderThickness);
            if (LabeledDragFloat("node_border", L("边框", "Border"), ref border, 0f, 60f)) { node.BorderThickness = border; KvRuntime.Changed(false); }
            if (InspectorColor(L("普通背景", "Normal background"), "node_bg_normal", node.BackgroundNormal)) KvRuntime.Changed(false);
            if (InspectorColor(L("按下背景", "Pressed background"), "node_bg_pressed", node.BackgroundPressed)) KvRuntime.Changed(false);
            if (InspectorColor(L("普通边框", "Normal border"), "node_border_normal", node.BorderNormal)) KvRuntime.Changed(false);
            if (InspectorColor(L("按下边框", "Pressed border"), "node_border_pressed", node.BorderPressed)) KvRuntime.Changed(false);
            if (isMedia) return;
            if (InspectorColor(L("普通文字", "Normal text"), "node_text_normal", node.TextNormal)) KvRuntime.Changed(false);
            if (InspectorColor(L("按下文字", "Pressed text"), "node_text_pressed", node.TextPressed)) KvRuntime.Changed(false);
        }

        private static string MediaFitItems(int nodeType)
        {
            return nodeType == 4
                ? L("默认\0适应\0填充\0原始大小居中\0平铺\0", "Default\0Fit\0Fill\0Center\0Tile\0")
                : L("拉伸\0适应\0填充\0原始大小居中\0平铺\0", "Stretch\0Fit\0Fill\0Center\0Tile\0");
        }

        private static void DrawBatchInspector(KvProfile profile, List<KvKey> nodes)
        {
            ImGui.TextDisabled(L("批量属性模式 · 带 * 的值当前不一致", "Batch edit · values marked * differ between nodes"));
            bool allMedia = nodes.TrueForAll(KvSettings.IsMediaNode);
            bool anyMedia = nodes.Exists(KvSettings.IsMediaNode);

            InspectorSection(L("多选布局", "Arrange selection"));
            DrawAlignmentControls(profile);

            InspectorSection(L("几何与层级", "Geometry and layer"));
            KvKey first = nodes[0];
            float x = first.PositionX;
            bool sameX = AllFloat(nodes, item => item.PositionX, x);
            if (LabeledDragFloat("batch_x", MixedLabel(L("位置 X", "Position X"), sameX), ref x, -10000f, 10000f))
            {
                float delta = x - first.PositionX;
                foreach (KvKey item in nodes) item.PositionX += delta;
                KvRuntime.Changed(false);
            }
            float y = first.PositionY;
            bool sameY = AllFloat(nodes, item => item.PositionY, y);
            if (LabeledDragFloat("batch_y", MixedLabel(L("位置 Y", "Position Y"), sameY), ref y, -10000f, 10000f))
            {
                float delta = y - first.PositionY;
                foreach (KvKey item in nodes) item.PositionY += delta;
                KvRuntime.Changed(false);
            }
            BatchFloat(nodes, "batch_width", L("宽度", "Width"), item => KvGeometry.Width(profile, item), (item, value) => item.Width = value, 1f, 1200f, false);
            BatchFloat(nodes, "batch_height", L("高度", "Height"), item => KvGeometry.Height(profile, item), (item, value) => item.Height = value, 1f, 1200f, false);
            BatchPercent(nodes, "batch_opacity", L("透明度", "Opacity"), item => item.Opacity, (item, value) => item.Opacity = value);
            BatchStepInt(nodes, "batch_depth", L("渲染层级", "Render order"), item => item.Depth, (item, value) => item.Depth = value, -1000, 1000);
            BatchBool(nodes, "batch_locked", L("锁定节点", "Lock node"), item => item.Locked, (item, value) => item.Locked = value, false);
            if (!anyMedia)
                BatchInt(nodes, "batch_hit_count", L("击键统计 (HitCount)", "Hit count"), item => item.HitCount, (item, value) => item.HitCount = Mathf.Max(0, value), 0, 99999999, false);

            bool allInput = nodes.TrueForAll(KvSettings.IsInputNode);
            if (!anyMedia)
            {
            InspectorSection(L("文字", "Text"));
            if (allInput)
            {
                string label = first.Label ?? string.Empty;
                bool sameLabel = nodes.TrueForAll(item => string.Equals(item.Label ?? string.Empty, label, StringComparison.Ordinal));
                ImGui.TextDisabled(MixedLabel(L("显示文字", "Label"), sameLabel));
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.InputText("##FreeMake_batch_label", ref label, 24))
                {
                    foreach (KvKey item in nodes) item.Label = label;
                    KvRuntime.Changed(false);
                }
            }
            BatchFloat(nodes, "batch_label_size", L("主文字大小", "Label size"), item => KvGeometry.Resolve(item.LabelSize, profile.LabelSize),
                (item, value) => item.LabelSize = value, 8f, 240f, false);
            BatchFloat(nodes, "batch_count_size", L("计数大小", "Count size"), item => KvGeometry.Resolve(item.CountSize, profile.CountSize),
                (item, value) => item.CountSize = value, 8f, 240f, false);
            BatchFloat(nodes, "batch_label_x", L("主文字偏移 X", "Label offset X"), item => item.LabelOffsetX, (item, value) => item.LabelOffsetX = value, -1000f, 1000f, false);
            BatchFloat(nodes, "batch_label_y", L("主文字偏移 Y", "Label offset Y"), item => item.LabelOffsetY, (item, value) => item.LabelOffsetY = value, -1000f, 1000f, false);
            BatchFloat(nodes, "batch_count_x", L("计数偏移 X", "Count offset X"), item => item.CountOffsetX, (item, value) => item.CountOffsetX = value, -1000f, 1000f, false);
            BatchFloat(nodes, "batch_count_y", L("计数偏移 Y", "Count offset Y"), item => item.CountOffsetY, (item, value) => item.CountOffsetY = value, -1000f, 1000f, false);
            BatchInt(nodes, "batch_count_align", L("计数对齐（0左 / 1中 / 2右）", "Count alignment (0 left / 1 center / 2 right)"), item => item.CountAlignment,
                (item, value) => item.CountAlignment = Mathf.Clamp(value, 0, 2), 0, 2, false);
            BatchBool(nodes, "batch_hide_count", L("隐藏计数", "Hide count"), item => item.HideCount, (item, value) => item.HideCount = value, false);
            }

            InspectorSection(L("颜色与外观", "Colors and style"));
            BatchBool(nodes, "batch_custom_style", L("独立样式", "Node style"), item => item.UseCustomStyle, (item, value) => item.UseCustomStyle = value, false);
            BatchFloat(nodes, "batch_radius", L("圆角", "Corner radius"), item => KvGeometry.Resolve(item.CornerRadius, profile.CornerRadius),
                (item, value) => { item.CornerRadius = value; item.UseCustomStyle = true; }, 0f, 400f, false);
            BatchFloat(nodes, "batch_border", L("边框", "Border"), item => KvGeometry.Resolve(item.BorderThickness, profile.BorderThickness),
                (item, value) => { item.BorderThickness = value; item.UseCustomStyle = true; }, 0f, 60f, false);
            BatchColor(nodes, "batch_bg_normal", L("普通背景", "Normal background"), item => item.BackgroundNormal, (item, value) => item.BackgroundNormal = value, true);
            BatchColor(nodes, "batch_bg_pressed", L("按下背景", "Pressed background"), item => item.BackgroundPressed, (item, value) => item.BackgroundPressed = value, true);
            BatchColor(nodes, "batch_border_normal", L("普通边框", "Normal border"), item => item.BorderNormal, (item, value) => item.BorderNormal = value, true);
            BatchColor(nodes, "batch_border_pressed", L("按下边框", "Pressed border"), item => item.BorderPressed, (item, value) => item.BorderPressed = value, true);
            if (!anyMedia)
            {
            BatchColor(nodes, "batch_text_normal", L("普通文字", "Normal text"), item => item.TextNormal, (item, value) => item.TextNormal = value, true);
            BatchColor(nodes, "batch_text_pressed", L("按下文字", "Pressed text"), item => item.TextPressed, (item, value) => item.TextPressed = value, true);

            InspectorSection(L("文字效果", "Text effects"));
            BatchBool(nodes, "batch_custom_text", L("覆盖配置文字效果", "Override profile text effects"), item => item.UseCustomTextEffects,
                (item, value) => item.UseCustomTextEffects = value, false);
            BatchBool(nodes, "batch_label_outline", L("主文字描边", "Label outline"), item => item.LabelOutlineEnabled,
                (item, value) => { item.LabelOutlineEnabled = value; item.UseCustomTextEffects = true; }, false);
            BatchBool(nodes, "batch_count_outline", L("计数描边", "Count outline"), item => item.CountOutlineEnabled,
                (item, value) => { item.CountOutlineEnabled = value; item.UseCustomTextEffects = true; }, false);
            BatchFloat(nodes, "batch_label_outline_width", L("主文字描边粗细", "Label outline thickness"), item => item.LabelOutlineThickness,
                (item, value) => { item.LabelOutlineThickness = value; item.UseCustomTextEffects = true; }, 0f, 8f, false);
            BatchFloat(nodes, "batch_count_outline_width", L("计数描边粗细", "Count outline thickness"), item => item.CountOutlineThickness,
                (item, value) => { item.CountOutlineThickness = value; item.UseCustomTextEffects = true; }, 0f, 8f, false);
            BatchColor(nodes, "batch_label_outline_color", L("主文字描边颜色", "Label outline color"), item => item.LabelOutlineColor,
                (item, value) => { item.LabelOutlineColor = value; item.UseCustomTextEffects = true; }, false);
            BatchColor(nodes, "batch_count_outline_color", L("计数描边颜色", "Count outline color"), item => item.CountOutlineColor,
                (item, value) => { item.CountOutlineColor = value; item.UseCustomTextEffects = true; }, false);
            BatchBool(nodes, "batch_label_shadow", L("主文字阴影", "Label shadow"), item => item.LabelShadowEnabled,
                (item, value) => { item.LabelShadowEnabled = value; item.UseCustomTextEffects = true; }, false);
            BatchBool(nodes, "batch_count_shadow", L("计数阴影", "Count shadow"), item => item.CountShadowEnabled,
                (item, value) => { item.CountShadowEnabled = value; item.UseCustomTextEffects = true; }, false);
            BatchColor(nodes, "batch_label_shadow_color", L("主文字阴影颜色", "Label shadow color"), item => item.LabelShadowColor,
                (item, value) => { item.LabelShadowColor = value; item.UseCustomTextEffects = true; }, false);
            BatchColor(nodes, "batch_count_shadow_color", L("计数阴影颜色", "Count shadow color"), item => item.CountShadowColor,
                (item, value) => { item.CountShadowColor = value; item.UseCustomTextEffects = true; }, false);
            BatchFloatUnbounded(nodes, "batch_label_shadow_x", L("主文字阴影 X", "Label shadow X"), item => item.LabelShadowOffset[0],
                (item, value) => { item.LabelShadowOffset[0] = value; item.UseCustomTextEffects = true; });
            BatchFloatUnbounded(nodes, "batch_label_shadow_y", L("主文字阴影 Y", "Label shadow Y"), item => item.LabelShadowOffset[1],
                (item, value) => { item.LabelShadowOffset[1] = value; item.UseCustomTextEffects = true; });
            BatchFloatUnbounded(nodes, "batch_label_shadow_softness", L("主文字阴影柔度", "Label shadow softness"), item => item.LabelShadowSoftness,
                (item, value) => { item.LabelShadowSoftness = value; item.UseCustomTextEffects = true; });
            BatchFloatUnbounded(nodes, "batch_count_shadow_x", L("计数阴影 X", "Count shadow X"), item => item.CountShadowOffset[0],
                (item, value) => { item.CountShadowOffset[0] = value; item.UseCustomTextEffects = true; });
            BatchFloatUnbounded(nodes, "batch_count_shadow_y", L("计数阴影 Y", "Count shadow Y"), item => item.CountShadowOffset[1],
                (item, value) => { item.CountShadowOffset[1] = value; item.UseCustomTextEffects = true; });
            BatchFloatUnbounded(nodes, "batch_count_shadow_softness", L("计数阴影柔度", "Count shadow softness"), item => item.CountShadowSoftness,
                (item, value) => { item.CountShadowSoftness = value; item.UseCustomTextEffects = true; });
            }

            if (allInput)
            {
                InspectorSection(L("键雨", "Key rain"));
                BatchInt(nodes, "batch_rain_row", L("键雨分组（0关闭 / 1第一排 / 2第二排 / 3第三排 / 4第四排）", "Rain row (0 off / 1-4 row)"), item => item.RainRow,
                    (item, value) => item.RainRow = Mathf.Clamp(value, 0, 4), 0, 4, false);
                BatchBool(nodes, "batch_custom_rain", L("独立键雨样式", "Node rain style"), item => item.UseCustomRain, (item, value) => item.UseCustomRain = value, false);
                BatchBool(nodes, "batch_rain_enabled", L("启用此节点键雨", "Key rain for this node"), item => item.RainEnabled,
                    (item, value) => { item.RainEnabled = value; item.UseCustomRain = true; }, false);
                BatchFloat(nodes, "batch_rain_width", L("键雨宽度比例", "Rain width ratio"), item => item.RainWidthRatio,
                    (item, value) => { item.RainWidthRatio = value; item.UseCustomRain = true; }, 0.05f, 2f, false);
                BatchFloat(nodes, "batch_rain_y", L("键雨高度偏移", "Rain height offset"), item => item.RainYOffset,
                    (item, value) => { item.RainYOffset = value; item.UseCustomRain = true; }, -400f, 400f, false);
                BatchFloat(nodes, "batch_rain_radius", L("键雨圆角", "Rain corner radius"), item => item.RainCornerRadius,
                    (item, value) => { item.RainCornerRadius = value; item.UseCustomRain = true; }, 0f, 200f, false);
                BatchColor(nodes, "batch_rain_near", L("键雨近端颜色", "Rain near color"), item => item.RainColor,
                    (item, value) => { item.RainColor = value; item.UseCustomRain = true; }, false);
                BatchColor(nodes, "batch_rain_far", L("键雨远端颜色", "Rain far color"), item => item.RainEndColor,
                    (item, value) => { item.RainEndColor = value; item.UseCustomRain = true; }, false);
                BatchColor(nodes, "batch_rain_right", L("键雨右侧颜色", "Rain right color"), item => item.RainRightColor,
                    (item, value) => { item.RainRightColor = value; item.UseCustomRain = true; }, false);
                BatchBool(nodes, "batch_rain_gradient", L("纵向渐变", "Vertical gradient"), item => item.RainGradientEnabled,
                    (item, value) => { item.RainGradientEnabled = value; item.UseCustomRain = true; }, false);
                BatchInt(nodes, "batch_rain_gradient_mode", L("渐变方式（0随雨段 / 1按高度）", "Gradient mode (0 per segment / 1 by height)"), item => item.RainGradientMode,
                    (item, value) => { item.RainGradientMode = Mathf.Clamp(value, 0, 1); item.UseCustomRain = true; }, 0, 1, false);
                BatchFloat(nodes, "batch_rain_gradient_height", L("渐变范围", "Gradient range"), item => item.RainGradientHeight,
                    (item, value) => { item.RainGradientHeight = value; item.UseCustomRain = true; }, 0.05f, 3f, false);
                BatchFloat(nodes, "batch_rain_gradient_power", L("渐变曲线", "Gradient curve"), item => item.RainGradientPower,
                    (item, value) => { item.RainGradientPower = value; item.UseCustomRain = true; }, 0.1f, 5f, false);
                BatchBool(nodes, "batch_rain_horizontal", L("横向渐变", "Horizontal gradient"), item => item.RainHorizontalGradientEnabled,
                    (item, value) => { item.RainHorizontalGradientEnabled = value; item.UseCustomRain = true; }, false);
                BatchInt(nodes, "batch_rain_fade_mode", L("消失方式（0高度裁剪 / 1羽化透明）", "Fade out (0 clip / 1 feather)"), item => item.RainFadeMode,
                    (item, value) => { item.RainFadeMode = Mathf.Clamp(value, 0, 1); item.UseCustomRain = true; }, 0, 1, false);
                BatchFloat(nodes, "batch_rain_fade_height", L("羽化范围", "Feather range"), item => item.RainFadeHeight,
                    (item, value) => { item.RainFadeHeight = value; item.UseCustomRain = true; }, 0.05f, 3f, false);
                BatchFloat(nodes, "batch_rain_fade_power", L("羽化程度", "Feather strength"), item => item.RainFadePower,
                    (item, value) => { item.RainFadePower = value; item.UseCustomRain = true; }, 0.1f, 5f, false);
                BatchBool(nodes, "batch_rain_shadow", L("键雨阴影", "Rain shadow"), item => item.RainShadowEnabled,
                    (item, value) => { item.RainShadowEnabled = value; item.UseCustomRain = true; }, false);
                BatchColor(nodes, "batch_rain_shadow_color", L("键雨阴影颜色", "Rain shadow color"), item => item.RainShadowColor,
                    (item, value) => { item.RainShadowColor = value; item.UseCustomRain = true; }, false);
                BatchFloatUnbounded(nodes, "batch_rain_shadow_x", L("键雨阴影 X", "Rain shadow X"), item => item.RainShadowOffset[0],
                    (item, value) => { item.RainShadowOffset[0] = value; item.UseCustomRain = true; });
                BatchFloatUnbounded(nodes, "batch_rain_shadow_y", L("键雨阴影 Y", "Rain shadow Y"), item => item.RainShadowOffset[1],
                    (item, value) => { item.RainShadowOffset[1] = value; item.UseCustomRain = true; });
                BatchFloat(nodes, "batch_rain_shadow_softness", L("键雨阴影柔度", "Rain shadow softness"), item => item.RainShadowSoftness,
                    (item, value) => { item.RainShadowSoftness = value; item.UseCustomRain = true; }, 0f, 64f, false);
                BatchFloat(nodes, "batch_rain_shadow_strength", L("键雨阴影强度", "Rain shadow strength"), item => item.RainShadowStrength,
                    (item, value) => { item.RainShadowStrength = value; item.UseCustomRain = true; }, 0f, 1f, false);

                if (allMedia)
                {
                    InspectorSection(L("媒体内容", "Media"));
                    int batchFit = nodes[0].MediaFit;
                    ImGui.TextDisabled(MixedLabel(L("填充方式", "Fill mode"), nodes.TrueForAll(item => item.MediaFit == batchFit)));
                    ImGui.SetNextItemWidth(-1f);
                    if (ImGui.Combo("##FreeMake_batch_media_fit", ref batchFit, MediaFitItems(nodes[0].NodeType)))
                    {
                        foreach (KvKey item in nodes) item.MediaFit = batchFit;
                        KvRuntime.Changed(false);
                    }
                    float batchScale = nodes[0].MediaScale;
                    if (LabeledStepFloat("batch_media_scale", MixedLabel(L("内容缩放", "Content scale"), AllFloat(nodes, item => item.MediaScale, batchScale)), ref batchScale, 0.1f, 0.05f, 10f))
                    {
                        foreach (KvKey item in nodes) item.MediaScale = batchScale;
                        KvRuntime.Changed(false);
                    }
                    BatchFloat(nodes, "batch_media_x", L("内容偏移 X", "Content offset X"), item => item.MediaOffsetX,
                        (item, value) => item.MediaOffsetX = value, -2000f, 2000f, false);
                    BatchFloat(nodes, "batch_media_y", L("内容偏移 Y", "Content offset Y"), item => item.MediaOffsetY,
                        (item, value) => item.MediaOffsetY = value, -2000f, 2000f, false);
                    if (nodes.TrueForAll(item => item.NodeType == 4))
                        BatchBool(nodes, "batch_video_loop", L("循环播放", "Loop"), item => item.VideoLoop, (item, value) => item.VideoLoop = value, false);
                }

                InspectorSection(L("按键动画", "Press animation"));
                BatchBool(nodes, "batch_custom_anim", L("独立动画", "Node animation"), item => item.UseCustomAnimation, (item, value) => item.UseCustomAnimation = value, false);
                BatchBool(nodes, "batch_anim_enabled", L("启用动画", "Enable animation"), item => item.PressAnimationEnabled,
                    (item, value) => { item.PressAnimationEnabled = value; item.UseCustomAnimation = true; }, false);
                BatchFloat(nodes, "batch_anim_duration", L("时长", "Duration"), item => item.PressAnimationDuration,
                    (item, value) => { item.PressAnimationDuration = value; item.UseCustomAnimation = true; }, 0.01f, 2f, false);
                BatchFloat(nodes, "batch_anim_scale", L("按下缩放", "Pressed scale"), item => item.PressAnimationScale,
                    (item, value) => { item.PressAnimationScale = value; item.UseCustomAnimation = true; }, 0.2f, 3f, false);
                BatchFloat(nodes, "batch_anim_x", L("按下偏移 X", "Pressed offset X"), item => item.PressAnimationOffsetX,
                    (item, value) => { item.PressAnimationOffsetX = value; item.UseCustomAnimation = true; }, -200f, 200f, false);
                BatchFloat(nodes, "batch_anim_y", L("按下偏移 Y", "Pressed offset Y"), item => item.PressAnimationOffsetY,
                    (item, value) => { item.PressAnimationOffsetY = value; item.UseCustomAnimation = true; }, -200f, 200f, false);
            }
        }

        private static List<KvKey> GetSelectedNodes(KvProfile profile)
        {
            var result = new List<KvKey>();
            if (profile?.Keys == null) return result;
            foreach (KvKey node in profile.Keys)
                if (node != null && Selected.Contains(node.Id)) result.Add(node);
            return result;
        }

        private static void BatchFloat(List<KvKey> nodes, string id, string label, Func<KvKey, float> get,
            Action<KvKey, float> set, float min, float max, bool layoutChanged)
        {
            float value = get(nodes[0]);
            bool same = AllFloat(nodes, get, value);
            if (!LabeledDragFloat(id, MixedLabel(label, same), ref value, min, max)) return;
            foreach (KvKey node in nodes) set(node, value);
            KvRuntime.Changed(layoutChanged);
        }

        private static void BatchFloatUnbounded(List<KvKey> nodes, string id, string label, Func<KvKey, float> get,
            Action<KvKey, float> set)
        {
            float value = get(nodes[0]);
            bool same = AllFloat(nodes, get, value);
            if (!LabeledDragFloatUnbounded(id, MixedLabel(label, same), ref value)) return;
            foreach (KvKey node in nodes) set(node, value);
            KvRuntime.Changed(false);
        }

        private static void BatchInt(List<KvKey> nodes, string id, string label, Func<KvKey, int> get,
            Action<KvKey, int> set, int min, int max, bool layoutChanged)
        {
            int value = get(nodes[0]);
            bool same = nodes.TrueForAll(node => get(node) == value);
            ImGui.TextDisabled(MixedLabel(label, same));
            ImGui.SetNextItemWidth(-1f);
            if (!ImGui.DragInt("##FreeMake_" + id, ref value, 1f, min, max)) return;
            foreach (KvKey node in nodes) set(node, value);
            KvRuntime.Changed(layoutChanged);
        }

        private static void BatchStepInt(List<KvKey> nodes, string id, string label, Func<KvKey, int> get,
            Action<KvKey, int> set, int min, int max)
        {
            int value = get(nodes[0]);
            bool same = nodes.TrueForAll(node => get(node) == value);
            if (!LabeledStepInt(id, MixedLabel(label, same), ref value, min, max)) return;
            foreach (KvKey node in nodes) set(node, value);
            KvRuntime.Changed();
        }

        private static void BatchPercent(List<KvKey> nodes, string id, string label, Func<KvKey, float> get, Action<KvKey, float> set)
        {
            float value = get(nodes[0]);
            bool same = AllFloat(nodes, get, value);
            if (!LabeledPercent(id, MixedLabel(label, same), ref value)) return;
            foreach (KvKey node in nodes) set(node, value);
            KvRuntime.Changed(false);
        }

        private static void BatchBool(List<KvKey> nodes, string id, string label, Func<KvKey, bool> get,
            Action<KvKey, bool> set, bool layoutChanged)
        {
            bool value = get(nodes[0]);
            bool same = nodes.TrueForAll(node => get(node) == value);
            if (!ImGui.Checkbox(MixedLabel(label, same) + "##" + id, ref value)) return;
            foreach (KvKey node in nodes) set(node, value);
            KvRuntime.Changed(layoutChanged);
        }

        private static void BatchColor(List<KvKey> nodes, string id, string label, Func<KvKey, float[]> get,
            Action<KvKey, float[]> set, bool forceCustomStyle)
        {
            float[] first = get(nodes[0]);
            if (first == null || first.Length < 4) return;
            bool same = nodes.TrueForAll(node => SameColor(first, get(node)));
            float[] editable = (float[])first.Clone();
            if (!InspectorColor(MixedLabel(label, same), id, editable)) return;
            foreach (KvKey node in nodes)
            {
                set(node, (float[])editable.Clone());
                if (forceCustomStyle) node.UseCustomStyle = true;
            }
            KvRuntime.Changed(false);
        }

        private static bool AllFloat(List<KvKey> nodes, Func<KvKey, float> get, float first)
            => nodes.TrueForAll(node => Mathf.Abs(get(node) - first) < 0.001f);

        private static bool SameColor(float[] left, float[] right)
        {
            if (left == null || right == null || left.Length < 4 || right.Length < 4) return false;
            for (int i = 0; i < 4; i++) if (Mathf.Abs(left[i] - right[i]) >= 0.001f) return false;
            return true;
        }

        private static string MixedLabel(string label, bool same) => same ? label : label + " *";

        private static bool LabeledDragFloat(string id, string label, ref float value, float min, float max)
        {
            ImGui.TextDisabled(label);
            ImGui.SetNextItemWidth(-1f);
            return ImGui.DragFloat("##FreeMake_" + id, ref value, SonnetWidgets.DragSpeed(min, max), min, max, SonnetWidgets.DragFormat(min, max));
        }

        // 0..1 value shown and edited as a percentage.
        private static bool LabeledPercent(string id, string label, ref float value)
        {
            ImGui.TextDisabled(label);
            ImGui.SetNextItemWidth(-1f);
            float percent = value * 100f;
            if (!ImGui.DragFloat("##FreeMake_" + id, ref percent, 0.5f, 0f, 100f, "%.0f%%")) return false;
            value = Mathf.Clamp01(percent / 100f);
            return true;
        }

        // Value edited with - / + buttons, or by typing into the field.
        private static bool LabeledStepFloat(string id, string label, ref float value, float step, float min, float max)
        {
            ImGui.TextDisabled(label);
            ImGui.SetNextItemWidth(-1f);
            if (!ImGui.InputFloat("##FreeMake_" + id, ref value, step, step * 5f, "%.2f")) return false;
            value = Mathf.Clamp(value, min, max);
            return true;
        }

        private static bool LabeledStepInt(string id, string label, ref int value, int min, int max)
        {
            ImGui.TextDisabled(label);
            ImGui.SetNextItemWidth(-1f);
            if (!ImGui.InputInt("##FreeMake_" + id, ref value)) return false;
            value = Mathf.Clamp(value, min, max);
            return true;
        }

        private static bool LabeledDragInt(string id, string label, ref int value, int min, int max)
        {
            ImGui.TextDisabled(label);
            ImGui.SetNextItemWidth(-1f);
            return ImGui.DragInt("##FreeMake_" + id, ref value, 1f, min, max);
        }

        private static bool LabeledDragFloatUnbounded(string id, string label, ref float value, bool nonNegative = false)
        {
            ImGui.TextDisabled(label);
            ImGui.SetNextItemWidth(-1f);
            bool changed = ImGui.DragFloat("##FreeMake_" + id, ref value, 0.5f, 0f, 0f, "%.1f");
            if (changed && nonNegative && value < 0f) value = 0f;
            return changed;
        }

        private static void DrawInspectorSplitter(float height)
        {
            NumVector2 min = ImGui.GetCursorScreenPos();
            ImGui.InvisibleButton("##FreeMakeInspectorSplitter", new NumVector2(10f, height));
            bool hovered = ImGui.IsItemHovered();
            bool active = ImGui.IsItemActive();
            if (active && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                _inspectorWidth = Mathf.Clamp(_inspectorWidth - ImGui.GetIO().MouseDelta.X, 278f, 440f);
            uint color = ImGui.GetColorU32(active
                ? SonnetPalette.Accent(0.78f)
                : hovered ? SonnetPalette.Accent(0.42f) : new NumVector4(1f, 1f, 1f, 0.07f));
            ImGui.GetWindowDrawList().AddLine(min + new NumVector2(5f, 10f), min + new NumVector2(5f, height - 10f), color, active ? 2f : 1f);
        }

        private static void DrawAlignmentControls(KvProfile profile)
        {
            const float gap = 5f;
            float width = Mathf.Max(54f, (ImGui.GetContentRegionAvail().X - gap * 2f) / 3f);
            if (InspectorMiniButton("align_left", L("左对齐", "Align left"), width)) AlignSelected(profile, 0);
            ImGui.SameLine(0f, gap);
            if (InspectorMiniButton("align_hcenter", L("横居中", "Center H"), width)) AlignSelected(profile, 1);
            ImGui.SameLine(0f, gap);
            if (InspectorMiniButton("align_right", L("右对齐", "Align right"), width)) AlignSelected(profile, 2);
            if (InspectorMiniButton("align_top", L("上对齐", "Align top"), width)) AlignSelected(profile, 3);
            ImGui.SameLine(0f, gap);
            if (InspectorMiniButton("align_vcenter", L("纵居中", "Center V"), width)) AlignSelected(profile, 4);
            ImGui.SameLine(0f, gap);
            if (InspectorMiniButton("align_bottom", L("下对齐", "Align bottom"), width)) AlignSelected(profile, 5);
        }

        private static bool InspectorMiniButton(string id, string label, float width)
        {
            NumVector2 min = ImGui.GetCursorScreenPos();
            NumVector2 size = new NumVector2(Mathf.Max(width, ImGui.CalcTextSize(label).X + 16f), 30f);
            ImGui.InvisibleButton("##FreeMakeMini_" + id, size);
            bool hovered = ImGui.IsItemHovered();
            ImDrawListPtr draw = ImGui.GetWindowDrawList();
            draw.AddRectFilled(min, min + size, ImGui.GetColorU32(hovered
                ? SonnetPalette.Accent(0.12f) : new NumVector4(1f, 1f, 1f, 0.025f)), 6f);
            NumVector2 text = ImGui.CalcTextSize(label);
            draw.AddText(min + (size - text) * 0.5f, ImGui.GetColorU32(SonnetPalette.Text), label);
            return ImGui.IsItemClicked(ImGuiMouseButton.Left);
        }

        private static string PickFont(string currentPath)
        {
            string initial = Environment.CurrentDirectory;
            try
            {
                if (!string.IsNullOrWhiteSpace(currentPath))
                {
                    string directory = System.IO.Directory.Exists(currentPath)
                        ? currentPath : System.IO.Path.GetDirectoryName(currentPath);
                    if (!string.IsNullOrEmpty(directory) && System.IO.Directory.Exists(directory)) initial = directory;
                }
            }
            catch { }
            return ModernFileDialog.ShowOpenFileDialog(L("选择 KV 字体", "Choose KV font"),
                L("字体文件 (*.ttf;*.otf)|*.ttf;*.otf|所有文件 (*.*)|*.*", "Font files (*.ttf;*.otf)|*.ttf;*.otf|All files (*.*)|*.*"), initial);
        }

        private static void AlignSelected(KvProfile profile, int mode)
        {
            if (profile == null || Selected.Count < 2) return;
            var nodes = new List<KvKey>();
            foreach (KvKey item in profile.Keys) if (item != null && Selected.Contains(item.Id)) nodes.Add(item);
            if (nodes.Count < 2) return;
            PushUndo(profile);
            float target;
            if (mode == 0)
            {
                target = float.PositiveInfinity;
                foreach (KvKey item in nodes) target = Math.Min(target, item.PositionX - KvGeometry.Width(profile, item) * 0.5f);
                foreach (KvKey item in nodes) item.PositionX = target + KvGeometry.Width(profile, item) * 0.5f;
            }
            else if (mode == 1)
            {
                target = 0f; foreach (KvKey item in nodes) target += item.PositionX; target /= nodes.Count;
                foreach (KvKey item in nodes) item.PositionX = target;
            }
            else if (mode == 2)
            {
                target = float.NegativeInfinity;
                foreach (KvKey item in nodes) target = Math.Max(target, item.PositionX + KvGeometry.Width(profile, item) * 0.5f);
                foreach (KvKey item in nodes) item.PositionX = target - KvGeometry.Width(profile, item) * 0.5f;
            }
            else if (mode == 3)
            {
                target = float.NegativeInfinity;
                foreach (KvKey item in nodes) target = Math.Max(target, item.PositionY + KvGeometry.Height(profile, item) * 0.5f);
                foreach (KvKey item in nodes) item.PositionY = target - KvGeometry.Height(profile, item) * 0.5f;
            }
            else if (mode == 4)
            {
                target = 0f; foreach (KvKey item in nodes) target += item.PositionY; target /= nodes.Count;
                foreach (KvKey item in nodes) item.PositionY = target;
            }
            else
            {
                target = float.PositiveInfinity;
                foreach (KvKey item in nodes) target = Math.Min(target, item.PositionY - KvGeometry.Height(profile, item) * 0.5f);
                foreach (KvKey item in nodes) item.PositionY = target + KvGeometry.Height(profile, item) * 0.5f;
            }
            KvRuntime.Changed(false);
        }

        private static void SetSelectedLocked(KvProfile profile, bool locked)
        {
            if (profile == null || Selected.Count == 0) return;
            foreach (KvKey item in profile.Keys) if (item != null && Selected.Contains(item.Id)) item.Locked = locked;
            KvRuntime.Changed(false);
        }

        private static void InspectorSection(string title)
        {
            ImGui.Spacing();
            ImGui.Spacing();
            NumVector2 start = ImGui.GetCursorScreenPos();
            ImGui.TextColored(SonnetPalette.Muted, SonnetWidgets.Plain(title));
            float textWidth = ImGui.CalcTextSize(title).X;
            float remaining = ImGui.GetContentRegionAvail().X;
            if (remaining > 12f)
            {
                float y = start.Y + ImGui.GetTextLineHeight() * 0.55f;
                ImGui.GetWindowDrawList().AddLine(
                    new NumVector2(start.X + textWidth + 10f, y),
                    new NumVector2(start.X + textWidth + 10f + remaining, y),
                    ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, 0.075f)), 1f);
            }
            ImGui.Spacing();
        }

        private static bool InspectorColor(string label, string id, float[] color)
        {
            if (color == null || color.Length < 4) return false;
            ImGui.PushID(id);
            NumVector4 value = new NumVector4(color[0], color[1], color[2], color[3]);
            ImGui.SetNextItemWidth(-1f);
            bool changed = ImGui.ColorEdit4(label, ref value,
                ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.NoInputs);
            if (changed)
            {
                color[0] = value.X;
                color[1] = value.Y;
                color[2] = value.Z;
                color[3] = value.W;
            }
            ImGui.PopID();
            return changed;
        }

        private static int EasingIndex(string easing)
        {
            switch ((easing ?? string.Empty).ToLowerInvariant())
            {
                case "linear": return 0;
                case "ease-in-quad": return 2;
                case "ease-in-out-quad": return 3;
                case "ease-out-cubic": return 4;
                case "ease-in-out-cubic": return 5;
                case "ease-out-back": return 6;
                default: return 1;
            }
        }

        private static string EasingName(int index)
        {
            switch (index)
            {
                case 0: return "linear";
                case 2: return "ease-in-quad";
                case 3: return "ease-in-out-quad";
                case 4: return "ease-out-cubic";
                case 5: return "ease-in-out-cubic";
                case 6: return "ease-out-back";
                default: return "ease-out-quad";
            }
        }

        private static bool WideActionButton(string id, string label)
        {
            float width = ImGui.GetContentRegionAvail().X;
            NumVector2 min = ImGui.GetCursorScreenPos();
            NumVector2 size = new NumVector2(width, 36f);
            ImGui.InvisibleButton("##FreeMakeAction_" + id, size);
            bool hovered = ImGui.IsItemHovered();
            ImDrawListPtr draw = ImGui.GetWindowDrawList();
            draw.AddRectFilled(min, min + size, ImGui.GetColorU32(hovered
                ? SonnetPalette.Accent(0.13f)
                : new NumVector4(1f, 1f, 1f, 0.026f)), 7f);
            NumVector2 text = ImGui.CalcTextSize(label);
            draw.AddText(min + new NumVector2((width - text.X) * 0.5f, (size.Y - text.Y) * 0.5f),
                ImGui.GetColorU32(SonnetPalette.Text), label);
            return ImGui.IsItemClicked(ImGuiMouseButton.Left);
        }

        private static void AddNode(KvProfile profile, KvKey node)
        {
            PushUndo(profile);
            node.PositionX = 0f;
            node.PositionY = 0f;
            profile.Keys.Add(node);
            Selected.Clear();
            Selected.Add(node.Id);
            KvRuntime.Changed();
        }

        private static KvKey CreateMediaNode(int nodeType)
        {
            return KvSettings.CreateMedia(nodeType);
        }

        private static void CopySelected(KvProfile profile)
        {
            Clipboard.Clear();
            foreach (KvKey node in profile.Keys)
                if (Selected.Contains(node.Id)) Clipboard.Add(KvGeometry.CloneNode(node, true));
        }

        private static void PasteClipboard(KvProfile profile)
        {
            if (Clipboard.Count == 0) return;
            PushUndo(profile);
            Selected.Clear();
            foreach (KvKey source in Clipboard)
            {
                KvKey clone = KvGeometry.CloneNode(source);
                clone.Locked = false;
                clone.PositionX += 24f;
                clone.PositionY -= 24f;
                profile.Keys.Add(clone);
                Selected.Add(clone.Id);
            }
            KvRuntime.Changed();
        }

        private static void BeginDrag(KvProfile profile, NumVector2 mouse)
        {
            DragOrigins.Clear();
            foreach (KvKey node in profile.Keys)
                if (Selected.Contains(node.Id) && !node.Locked) DragOrigins[node.Id] = new Vector2(node.PositionX, node.PositionY);
            _dragStart = mouse;
            _dragging = DragOrigins.Count > 0;
            SnapGuides.Clear();
        }

        private static void BeginDuplicate(KvProfile profile)
        {
            PushUndo(profile);
            var clones = new List<KvKey>();
            foreach (KvKey node in profile.Keys)
                if (Selected.Contains(node.Id))
                {
                    KvKey clone = KvGeometry.CloneNode(node);
                    clone.Locked = false;
                    clones.Add(clone);
                }
            Selected.Clear();
            DragOrigins.Clear();
            foreach (KvKey clone in clones)
            {
                profile.Keys.Add(clone);
                Selected.Add(clone.Id);
                DragOrigins[clone.Id] = new Vector2(clone.PositionX, clone.PositionY);
            }
            _duplicateStartMouse = ImGui.GetIO().MousePos;
            _placingDuplicates = true;
            SnapGuides.Clear();
            KvRuntime.Changed();
        }

        private static void DeleteSelected(KvProfile profile)
        {
            PushUndo(profile);
            profile.Keys.RemoveAll(node => node != null && Selected.Contains(node.Id));
            Selected.Clear();
            KvRuntime.Changed();
        }

        private static void PushUndo(KvProfile profile)
        {
            var snapshot = new List<KvKey>();
            foreach (KvKey node in profile.Keys) snapshot.Add(KvGeometry.CloneNode(node, true));
            Undo.Add(snapshot);
            if (Undo.Count > 10) Undo.RemoveAt(0);
        }

        private static void UndoLast(KvProfile profile)
        {
            if (Undo.Count == 0) return;
            List<KvKey> snapshot = Undo[Undo.Count - 1];
            Undo.RemoveAt(Undo.Count - 1);
            profile.Keys = snapshot;
            Selected.Clear();
            _dragging = _boxSelecting = _placingDuplicates = false;
            KvRuntime.Changed();
        }

        private static KvKey SingleSelected(KvProfile profile)
        {
            if (Selected.Count != 1) return null;
            foreach (KvKey node in profile.Keys) if (Selected.Contains(node.Id)) return node;
            return null;
        }

        private static void CollectHitNodes(KvProfile profile, NumVector2 center, NumVector2 mouse, List<KvKey> hits)
        {
            hits.Clear();
            float scale = CanvasScale(profile);
            for (int i = profile.Keys.Count - 1; i >= 0; i--)
            {
                KvKey node = profile.Keys[i];
                NumVector2 position = ToScreen(profile, node, center);
                float halfW = KvGeometry.Width(profile, node) * scale * 0.5f;
                float halfH = KvGeometry.Height(profile, node) * scale * 0.5f;
                if (mouse.X >= position.X - halfW && mouse.X <= position.X + halfW && mouse.Y >= position.Y - halfH && mouse.Y <= position.Y + halfH)
                    hits.Add(node);
            }
            hits.Sort((left, right) =>
            {
                int depth = right.Depth.CompareTo(left.Depth);
                if (depth != 0) return depth;
                return profile.Keys.IndexOf(right).CompareTo(profile.Keys.IndexOf(left));
            });
        }

        private static KvKey PickNodeForCanvasClick(List<KvKey> hits, bool doubleClick, bool ctrl)
        {
            if (hits == null || hits.Count == 0) return null;
            KvKey selectedHit = null;
            for (int i = 0; i < hits.Count; i++)
                if (Selected.Contains(hits[i].Id)) { selectedHit = hits[i]; break; }
            if (doubleClick && hits.Count > 1)
            {
                int index = selectedHit == null ? -1 : hits.IndexOf(selectedHit);
                return hits[(index + 1 + hits.Count) % hits.Count];
            }
            if (!ctrl && selectedHit != null) return selectedHit;
            return hits[0];
        }

        private static bool ConsumeCanvasDoubleClick(NumVector2 mouse)
        {
            float now = Time.unscaledTime;
            NumVector2 delta = mouse - _lastCanvasClickPosition;
            bool result = now - _lastCanvasClickTime <= CanvasDoubleClickTime
                && delta.LengthSquared() <= CanvasDoubleClickDistance * CanvasDoubleClickDistance;
            _lastCanvasClickTime = now;
            _lastCanvasClickPosition = mouse;
            return result;
        }

        private static void SelectInBox(KvProfile profile, NumVector2 center, bool additive)
        {
            if (!additive) Selected.Clear();
            float minX = Math.Min(_boxStart.X, _boxEnd.X), maxX = Math.Max(_boxStart.X, _boxEnd.X);
            float minY = Math.Min(_boxStart.Y, _boxEnd.Y), maxY = Math.Max(_boxStart.Y, _boxEnd.Y);
            foreach (KvKey node in profile.Keys)
            {
                if (node.Locked) continue;
                NumVector2 pos = ToScreen(profile, node, center);
                float halfW = KvGeometry.Width(profile, node) * CanvasScale(profile) * 0.5f;
                float halfH = KvGeometry.Height(profile, node) * CanvasScale(profile) * 0.5f;
                if (pos.X + halfW >= minX && pos.X - halfW <= maxX && pos.Y + halfH >= minY && pos.Y - halfH <= maxY) Selected.Add(node.Id);
            }
        }

        private static float CanvasScale(KvProfile profile) => _zoom * Mathf.Max(0.01f, profile?.Scale ?? 1f);

        private static void ApplySnappedDrag(KvProfile profile, NumVector2 mouse, NumVector2 dragStart, NumVector2 center)
        {
            SnapGuides.Clear();
            if (profile == null || DragOrigins.Count == 0) return;

            float scale = CanvasScale(profile);
            NumVector2 requested = (mouse - dragStart) / scale;
            // 画布坐标的 Y 轴向下，而配置中的 PositionY 轴向上。
            requested.Y = -requested.Y;

            SnapReferenceX.Clear();
            SnapReferenceY.Clear();
            AddScreenBoundaryReferences(profile, center, scale);
            foreach (KvKey node in profile.Keys)
            {
                if (node == null || Selected.Contains(node.Id)) continue;
                float halfWidth = KvGeometry.Width(profile, node) * 0.5f;
                float halfHeight = KvGeometry.Height(profile, node) * 0.5f;
                SnapReferenceX.Add(node.PositionX - halfWidth);
                SnapReferenceX.Add(node.PositionX);
                SnapReferenceX.Add(node.PositionX + halfWidth);
                SnapReferenceY.Add(node.PositionY - halfHeight);
                SnapReferenceY.Add(node.PositionY);
                SnapReferenceY.Add(node.PositionY + halfHeight);
            }

            float snapLimit = 5f / Mathf.Max(0.01f, scale);
            bool hasX, hasY;
            float guideX, guideY;
            float correctionX = FindSnapCorrection(profile, requested.X, requested.Y, true, snapLimit, out hasX, out guideX);
            float correctionY = FindSnapCorrection(profile, requested.X, requested.Y, false, snapLimit, out hasY, out guideY);
            if (hasX) SnapGuides.Add(new SnapGuide { Vertical = true, Value = guideX });
            if (hasY) SnapGuides.Add(new SnapGuide { Vertical = false, Value = guideY });

            float finalDeltaX = requested.X + (hasX ? correctionX : 0f);
            float finalDeltaY = requested.Y + (hasY ? correctionY : 0f);
            foreach (KvKey node in profile.Keys)
            {
                Vector2 origin;
                if (!Selected.Contains(node.Id) || !DragOrigins.TryGetValue(node.Id, out origin)) continue;
                node.PositionX = origin.x + finalDeltaX;
                node.PositionY = origin.y + finalDeltaY;
            }
        }

        private static readonly List<float> SnapReferenceX = new List<float>(32);
        private static readonly List<float> SnapReferenceY = new List<float>(32);

        private static void AddScreenBoundaryReferences(KvProfile profile, NumVector2 center, float scale)
        {
            float zoom = Mathf.Max(0.01f, _zoom);
            float boundaryCenterX = center.X - profile.OffsetX * zoom;
            float boundaryCenterY = center.Y + profile.OffsetY * zoom;
            float halfWidth = Screen.width * 0.5f * zoom;
            float halfHeight = Screen.height * 0.5f * zoom;
            float left = (boundaryCenterX - halfWidth - center.X) / scale;
            float right = (boundaryCenterX + halfWidth - center.X) / scale;
            float top = -(boundaryCenterY - halfHeight - center.Y) / scale;
            float bottom = -(boundaryCenterY + halfHeight - center.Y) / scale;

            SnapReferenceX.Add(left);
            SnapReferenceX.Add((left + right) * 0.5f);
            SnapReferenceX.Add(right);
            SnapReferenceY.Add(top);
            SnapReferenceY.Add((top + bottom) * 0.5f);
            SnapReferenceY.Add(bottom);
        }

        private static float FindSnapCorrection(KvProfile profile, float requestedX, float requestedY, bool horizontal,
            float snapLimit, out bool found, out float snappedReference)
        {
            found = false;
            snappedReference = 0f;
            float bestCorrection = 0f;
            float bestDistance = float.MaxValue;
            List<float> references = horizontal ? SnapReferenceX : SnapReferenceY;
            foreach (KeyValuePair<string, Vector2> pair in DragOrigins)
            {
                KvKey node = profile.Keys.Find(item => item != null && string.Equals(item.Id, pair.Key, StringComparison.Ordinal));
                if (node == null) continue;
                float halfSize = (horizontal ? KvGeometry.Width(profile, node) : KvGeometry.Height(profile, node)) * 0.5f;
                float target = horizontal ? pair.Value.x + requestedX : pair.Value.y + requestedY;
                float[] candidates = { target - halfSize, target, target + halfSize };
                for (int c = 0; c < candidates.Length; c++)
                {
                    for (int r = 0; r < references.Count; r++)
                    {
                        float difference = references[r] - candidates[c];
                        float distance = Mathf.Abs(difference);
                        if (distance <= snapLimit && distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestCorrection = difference;
                            snappedReference = references[r];
                            found = true;
                        }
                    }
                }
            }
            return bestCorrection;
        }

        private static void DrawSnapGuides(ImDrawListPtr draw, NumVector2 min, NumVector2 max, NumVector2 center, KvProfile profile)
        {
            if (SnapGuides.Count == 0 || profile == null) return;
            float scale = CanvasScale(profile);
            uint color = ImGui.GetColorU32(SonnetPalette.Accent(0.62f));
            for (int i = 0; i < SnapGuides.Count; i++)
            {
                SnapGuide guide = SnapGuides[i];
                if (guide.Vertical)
                {
                    float x = center.X + guide.Value * scale;
                    draw.AddLine(new NumVector2(x, min.Y), new NumVector2(x, max.Y), color, 1.25f);
                }
                else
                {
                    float y = center.Y - guide.Value * scale;
                    draw.AddLine(new NumVector2(min.X, y), new NumVector2(max.X, y), color, 1.25f);
                }
            }
        }

        private static NumVector2 ToScreen(KvProfile profile, KvKey node, NumVector2 center)
        {
            float scale = CanvasScale(profile);
            return center + new NumVector2(node.PositionX * scale, -node.PositionY * scale);
        }

        private static void DrawScreenBounds(ImDrawListPtr draw, KvProfile profile, NumVector2 center)
        {
            float zoom = _zoom;
            NumVector2 boundaryCenter = center - new NumVector2(profile.OffsetX * zoom, -profile.OffsetY * zoom);
            NumVector2 half = new NumVector2(Screen.width * 0.5f * zoom, Screen.height * 0.5f * zoom);
            NumVector2 min = boundaryCenter - half;
            NumVector2 max = boundaryCenter + half;
            uint color = ImGui.GetColorU32(SonnetPalette.Accent(0.5f));
            draw.AddRect(min, max, color, 0f, ImDrawFlags.None, Mathf.Max(1f, 1.5f * zoom));
            string label = L("游戏屏幕范围 (", "Game screen (") + Screen.width + " x " + Screen.height + ")";
            draw.AddText(min + new NumVector2(8f, 8f) * zoom, color, label);
        }

        private static void DrawGrid(ImDrawListPtr draw, NumVector2 min, NumVector2 max)
        {
            float spacing = 32f * _zoom;
            if (spacing < 12f) spacing *= 4f;
            float originX = min.X + _pan.X % spacing;
            float originY = min.Y + _pan.Y % spacing;
            uint color = ImGui.GetColorU32(new NumVector4(0.74f, 0.84f, 0.96f, 0.040f));
            for (float x = originX; x < max.X; x += spacing) draw.AddLine(new NumVector2(x, min.Y), new NumVector2(x, max.Y), color);
            for (float y = originY; y < max.Y; y += spacing) draw.AddLine(new NumVector2(min.X, y), new NumVector2(max.X, y), color);
        }

        private static void CaptureKey(KvProfile profile)
        {
            if (string.IsNullOrEmpty(_capturingKeyId)) return;
            for (int i = 0; i < KeyCandidates.Length; i++)
            {
                KeyCode code = KeyCandidates[i];
                if (!Input.GetKeyDown(code)) continue;
                if (code == KeyCode.Escape) { _capturingKeyId = null; return; }
                KvKey node = profile.Keys.Find(item => item != null && string.Equals(item.Id, _capturingKeyId, StringComparison.Ordinal));
                if (node != null)
                {
                    node.Bind = code;
                    if (string.IsNullOrWhiteSpace(node.Label) || node.Label == "IMAGE" || node.Label == "VIDEO")
                        node.Label = KvSettings.KeyName(code);
                    KvRuntime.InputChanged();
                }
                _capturingKeyId = null;
                return;
            }
        }

        private static KeyCode[] BuildKeyCandidates()
        {
            Array values = Enum.GetValues(typeof(KeyCode));
            var keys = new List<KeyCode>();
            for (int i = 0; i < values.Length; i++)
            {
                KeyCode key = (KeyCode)values.GetValue(i);
                int code = (int)key;
                if (key != KeyCode.None && (code < 323 || code > 329)) keys.Add(key);
            }
            return keys.ToArray();
        }

        private static uint Color(float[] value) => ImGui.GetColorU32(new NumVector4(value[0], value[1], value[2], value[3]));

        private static uint Color(float[] value, float opacity) =>
            ImGui.GetColorU32(new NumVector4(value[0], value[1], value[2], value[3] * Mathf.Clamp01(opacity)));
    }
}
