using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using ImGuiNET;
using UnityEngine;
using UnityEngine.UI;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;
using CheryTools.Sonnet.Shared;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.Sonnet.Framework
{
    public sealed unsafe class SonnetImGuiController : MonoBehaviour
    {
        private IntPtr _context;
        private Texture2D _fontTexture;
        private Material _material;
        private Canvas _canvas;
        private Canvas _inputBlockerCanvas;
        private Image _inputBlockerImage;
        private readonly List<CanvasRenderer> _renderers = new List<CanvasRenderer>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private Vector3[] _vertices = new Vector3[0];
        private UnityEngine.Vector2[] _uvs = new UnityEngine.Vector2[0];
        private Color32[] _colors = new Color32[0];
        private int[] _indices = new int[0];
        private int[] _commandIndices = new int[0];
        private float _scale = 1f;
        private bool _hasContent;
        private NumVector2 _panelSize;
        private NumVector2 _displaySize;
        private NumVector2 _panelPosition;
        private bool _panelPositionChanged;
        private bool _panelSizeChanged;
        private string _dragModuleId;
        private bool _modulePressPending;
        private bool _moduleDragActive;
        private bool _waitingForPanelShortcut;
        private float _modulePressStartedAt;
        private int _moduleDragTargetIndex;
        private string _lastAnimatedModuleId = "";
        private string _lastAnimatedTabId = "";
        private float _contentAlpha = 1f;
        internal static ImFontPtr HeadingFont { get; private set; }
        internal static float PanelScale { get; private set; } = 1f;
        private static readonly string DefaultBrandTitle = "CheryTools " + SonnetMain.ReleaseName;
        private static string DefaultBrandSubtitle => L("愿世界，如你我所愿", "May the world... be as we wish");
        private const float BrandTitleSize = 24f;
        private const float BrandSubtitleSize = 17f;

        private void Awake()
        {
            _context = ImGui.CreateContext();
            ImGui.SetCurrentContext(_context);
            ImGui.GetIO().NativePtr->IniFilename = null;
            // ImGui 1.91 pops a "conflicting ID" debug tooltip over duplicate widget IDs; it is meant for
            // developer builds, not players.
            ImGui.GetIO().ConfigDebugHighlightIdConflicts = false;
            BuildFontAtlas();
            SonnetTheme.Configure(SonnetMain.Settings);
            SonnetTheme.Apply(1f);
            SonnetImeBridge.Initialize();

            Shader shader = Shader.Find("UI/Default");
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, mainTexture = _fontTexture };
            var canvasObject = new GameObject("CheryTools_Sonnet_ImGui_Canvas");
            DontDestroyOnLoad(canvasObject);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32767;
            _canvas.gameObject.AddComponent<GraphicRaycaster>();
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            CreateInputBlocker();
        }

        private void Start()
        {
            SonnetMain.AttachCanvas(_canvas);
        }

        private void Update()
        {
            SonnetMain.UpdateLanguage();
            if (!SonnetMain.IsEnabled)
            {
                SonnetImeBridge.Suspend();
                ClearCanvas();
                return;
            }
            if (_waitingForPanelShortcut)
            {
                KeyCode captured = CapturePanelShortcut();
                if (captured != KeyCode.None)
                {
                    SonnetMain.Settings.PanelShortcut = captured;
                    _waitingForPanelShortcut = false;
                    SonnetMain.SaveSettings();
                }
            }
            else if (SonnetMain.PanelShortcut != KeyCode.None && Input.GetKeyDown(SonnetMain.PanelShortcut))
            {
                SonnetMain.IsPanelOpen = !SonnetMain.IsPanelOpen;
            }
            SonnetMain.Modules?.Update();
            bool hasOverlay = SonnetMain.Modules?.HasOpenOverlays ?? false;
            SonnetMain.SetOverlayState(hasOverlay);
            SetInputBlockerActive(SonnetMain.IsAnyImGuiWindowOpen);
            if (!SonnetMain.IsPanelOpen && !hasOverlay)
            {
                SonnetImeBridge.Suspend();
                ClearCanvas();
                return;
            }

            _scale = SonnetTheme.ClampScale(SonnetMain.Settings?.UiScale ?? 1f);
            PanelScale = _scale;
            var io = ImGui.GetIO();
            io.DisplaySize = new NumVector2(Screen.width / _scale, Screen.height / _scale);
            io.DisplayFramebufferScale = NumVector2.One;
            io.DeltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
            SonnetImeBridge.BeginFrame();
            io.AddMousePosEvent(Input.mousePosition.x / _scale, (Screen.height - Input.mousePosition.y) / _scale);
            io.AddMouseButtonEvent(0, Input.GetMouseButton(0));
            io.AddMouseButtonEvent(1, Input.GetMouseButton(1));
            io.AddMouseButtonEvent(2, Input.GetMouseButton(2));
            io.AddMouseWheelEvent(Input.mouseScrollDelta.x, Input.mouseScrollDelta.y);
            AddKeys(io, io.WantTextInput || !string.IsNullOrEmpty(Input.compositionString));
            SonnetImeBridge.AddInputCharacters(io, Input.inputString);

            ImGui.NewFrame();
            if (SonnetMain.IsPanelOpen) DrawFrameworkPanel();
            SonnetMain.Modules?.DrawOverlays();
            SonnetImeBridge.DrawCompositionPreview();
            try { ImGui.Render(); }
            finally { SonnetImeBridge.EndFrame(); }
            UpdateCanvas();
        }

        private void DrawFrameworkPanel()
        {
            NumVector2 display = ImGui.GetIO().DisplaySize;
            float marginX = Mathf.Max(52f, display.X * 0.085f);
            float marginY = Mathf.Max(38f, display.Y * 0.085f);
            NumVector2 defaultPanelSize = new NumVector2(display.X - marginX * 2f, display.Y - marginY * 2f);
            NumVector2 panelSize = SonnetMain.Settings != null && SonnetMain.Settings.HasCustomPanelSize
                ? new NumVector2(SonnetMain.Settings.PanelWidth, SonnetMain.Settings.PanelHeight)
                : defaultPanelSize;
            panelSize = SonnetWindowChrome.ClampSize(panelSize, display, new NumVector2(760f, 540f), 8f);
            NumVector2 panelMin = SonnetMain.Settings != null && SonnetMain.Settings.HasCustomPanelPosition
                ? new NumVector2(SonnetMain.Settings.PanelPositionX, SonnetMain.Settings.PanelPositionY)
                : (display - panelSize) * 0.5f;
            panelMin = SonnetWindowChrome.ClampPosition(panelMin, panelSize, display, 8f);
            if (SonnetMain.Settings != null && SonnetMain.Settings.HasCustomPanelPosition)
            {
                SonnetMain.Settings.PanelPositionX = panelMin.X;
                SonnetMain.Settings.PanelPositionY = panelMin.Y;
            }
            NumVector2 panelMax = panelMin + panelSize;
            _panelSize = panelSize;
            _displaySize = display;
            _panelPosition = panelMin;
            ImDrawListPtr background = ImGui.GetBackgroundDrawList();
            const float panelRounding = 16f;
            SonnetPanelBackground.Draw(background, panelMin, panelMax, panelRounding, SonnetMain.Settings);
            // Whole-panel dimming layer; the sidebar and content cards add their own (card opacity).
            SonnetPanelBackground.DrawMask(background, panelMin, panelMax, panelRounding, SonnetMain.Settings);

            ImGui.SetNextWindowPos(panelMin);
            ImGui.SetNextWindowSize(panelSize);
            ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus;
            if (!ImGui.Begin("##SonnetShell", flags)) { ImGui.End(); return; }

            // 子窗口使用独立 DrawList，直接贴住 Shell 边缘时会把父窗口绘制的
            // 圆角边框盖住。这里保留 1px 的真实内容安全区；底下仍然是面板
            // 背景，不会形成旧实现那种额外黑边。
            const float shellInset = 1f;
            ImGui.SetCursorPos(new NumVector2(shellInset));
            NumVector2 available = panelSize - new NumVector2(shellInset * 2f);
            float sidebarWidth = Mathf.Clamp(available.X * 0.20f, 205f, 315f);
            DrawSidebar(sidebarWidth, available.Y);
            ImGui.SameLine(0f, 0f);
            DrawContent(available.X - sidebarWidth, available.Y);

            ImGui.End();
            DrawPanelBorderOverlay(panelMin, panelSize, panelRounding);
            DrawPanelResizeHandles(panelMin, panelSize);
        }

        private static void DrawPanelBorderOverlay(NumVector2 position, NumVector2 size, float rounding)
        {
            // 边框窗口在 Shell 及其 Child 之后、模块浮层之前提交：既不会再被
            // 侧栏覆盖，也不会压住随后绘制的 FreeMake 等独立编辑器。
            ImGui.SetNextWindowPos(position);
            ImGui.SetNextWindowSize(size);
            ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoSavedSettings
                | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav
                | ImGuiWindowFlags.NoBackground;
            ImGui.Begin("##SonnetShellBorder", flags);
            DrawPanelBorder(ImGui.GetWindowDrawList(), position, position + size, rounding);
            ImGui.End();
        }

        private void DrawSidebar(float width, float height)
        {
            BeginCard("##SonnetSidebar", new NumVector2(width, height), SonnetTheme.Sidebar);
            // Two-line brand header, both lines user-editable. They are drawn from the loaded fonts at a
            // set size (heading font scaled down for the title, body font for the subtitle), so any custom
            // text renders without extra atlas glyphs; text wider than the sidebar shrinks to fit.
            const float horizontalPadding = 24f;
            const float headerTop = 16f;
            float subtitleY = headerTop + BrandTitleSize + 8f;
            float dividerY = subtitleY + BrandSubtitleSize + 14f;
            float navTop = dividerY + 18f;
            DrawPanelDragRegion("sidebar", width, dividerY + 6f);
            string brandTitle = string.IsNullOrWhiteSpace(SonnetMain.Settings?.BrandTitle) ? DefaultBrandTitle : SonnetMain.Settings.BrandTitle;
            string brandSubtitle = string.IsNullOrWhiteSpace(SonnetMain.Settings?.BrandSubtitle) ? DefaultBrandSubtitle : SonnetMain.Settings.BrandSubtitle;
            NumVector2 origin = ImGui.GetWindowPos();
            float textWidth = width - horizontalPadding * 2f;
            ImDrawListPtr headerDraw = ImGui.GetWindowDrawList();
            DrawFittedText(headerDraw, HeadingFont.NativePtr != null ? HeadingFont : ImGui.GetFont(), BrandTitleSize,
                origin + new NumVector2(horizontalPadding, headerTop), textWidth, SonnetTheme.Text, brandTitle);
            DrawFittedText(headerDraw, ImGui.GetFont(), BrandSubtitleSize,
                origin + new NumVector2(horizontalPadding, subtitleY), textWidth, SonnetTheme.MutedText, brandSubtitle);
            // The exact build code (what testers report) is on hover.
            NumVector2 headerMin = origin + new NumVector2(horizontalPadding, headerTop);
            NumVector2 headerMax = origin + new NumVector2(width - horizontalPadding, dividerY);
            if (ImGui.IsMouseHoveringRect(headerMin, headerMax) && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
                ImGui.SetTooltip("CheryTools " + SonnetMain.ReleaseName + L("\n构建 ", "\nBuild ") + SonnetMain.DisplayVersion);
            DrawHorizontalLine(horizontalPadding, width - horizontalPadding, dividerY);
            ImGui.SetCursorPos(new NumVector2(horizontalPadding, navTop));
            var modules = SonnetMain.Modules?.DisplayModules;
            if (modules != null)
            {
                for (int i = 0; i < modules.Count; i++)
                {
                    LoadedModule module = modules[i];
                    ImGui.SetCursorPosX(horizontalPadding);
                    DrawReorderableModuleItem(module, i, modules.Count, width - horizontalPadding * 2f);
                }

                if (_moduleDragActive && ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    float rowPitch = 42f + ImGui.GetStyle().ItemSpacing.Y;
                    float firstRowY = ImGui.GetWindowPos().Y + navTop;
                    float relativeY = ImGui.GetMousePos().Y - firstRowY;
                    _moduleDragTargetIndex = Mathf.Clamp(
                        Mathf.FloorToInt((relativeY + 21f) / Mathf.Max(1f, rowPitch)), 0, modules.Count);
                    float indicatorY = firstRowY + _moduleDragTargetIndex * rowPitch;
                    DrawModuleDropIndicator(
                        new NumVector2(ImGui.GetWindowPos().X + horizontalPadding, indicatorY),
                        width - horizontalPadding * 2f);
                }
            }

            if (_modulePressPending && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                if (_moduleDragActive)
                    FinishModuleDrag();
                else
                {
                    LoadedModule releasedModule = modules == null ? null : modules.FirstOrDefault(item =>
                        string.Equals(item.Metadata.Id, _dragModuleId, StringComparison.Ordinal));
                    if (releasedModule != null) SelectModule(releasedModule);
                    CancelModuleDrag();
                }
            }

            ImGui.SetCursorPos(new NumVector2(horizontalPadding, height - 62f));
            if (DrawSidebarItem("__settings", L("设置", "Settings"), string.Equals(SonnetMain.Settings.SelectedModuleId, "__settings", StringComparison.Ordinal), width - horizontalPadding * 2f))
                SonnetMain.Settings.SelectedModuleId = "__settings";
            ImGui.EndChild();
        }

        private void DrawReorderableModuleItem(LoadedModule module, int index, int count, float width)
        {
            NumVector2 size = new NumVector2(width, 42f);
            NumVector2 min = ImGui.GetCursorScreenPos();
            if (_moduleDragActive && _moduleDragTargetIndex == index)
                DrawModuleDropIndicator(min, width);

            bool active = string.Equals(SonnetMain.Settings.SelectedModuleId, module.Metadata.Id, StringComparison.Ordinal);
            bool clicked = ImGui.InvisibleButton("##Nav_" + module.Metadata.Id, size);
            bool hovered = ImGui.IsItemHovered();
            bool held = ImGui.IsItemActive() && ImGui.IsMouseDown(ImGuiMouseButton.Left);
            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                _modulePressPending = true;
                _moduleDragActive = false;
                _dragModuleId = module.Metadata.Id;
                _modulePressStartedAt = Time.unscaledTime;
                _moduleDragTargetIndex = index;
            }

            if (_modulePressPending && held && string.Equals(_dragModuleId, module.Metadata.Id, StringComparison.Ordinal))
            {
                if (!_moduleDragActive && Time.unscaledTime - _modulePressStartedAt >= 0.30f)
                    _moduleDragActive = true;
                // The list computes the insertion slot after all rows are laid out.
            }

            if (_moduleDragActive && string.Equals(_dragModuleId, module.Metadata.Id, StringComparison.Ordinal))
                active = false;
            if (active || hovered || (_moduleDragActive && string.Equals(_dragModuleId, module.Metadata.Id, StringComparison.Ordinal)))
            {
                NumVector4 background = active
                    ? new NumVector4(1f, 1f, 1f, 0.075f)
                    : new NumVector4(1f, 1f, 1f, 0.045f);
                ImGui.GetWindowDrawList().AddRectFilled(min, min + size,
                    ImGui.GetColorU32(background), 6f);
            }
            if (active)
            {
                ImGui.GetWindowDrawList().AddRectFilled(
                    new NumVector2(min.X + 2f, min.Y + 8f),
                    new NumVector2(min.X + 5.5f, min.Y + size.Y - 8f),
                    ImGui.GetColorU32(SonnetTheme.Accent), 2f);
            }
            NumVector2 textSize = ImGui.CalcTextSize(module.Metadata.DisplayName);
            NumVector2 textPos = min + new NumVector2((width - textSize.X) * 0.5f, (size.Y - textSize.Y) * 0.5f);
            ImGui.GetWindowDrawList().AddText(textPos,
                ImGui.GetColorU32(active ? SonnetTheme.Text : SonnetTheme.MutedText), module.Metadata.DisplayName);

            if (clicked && !_moduleDragActive && !_modulePressPending)
                SelectModule(module);

        }

        private void DrawModuleDropIndicator(NumVector2 position, float width)
        {
            ImGui.GetWindowDrawList().AddLine(
                position + new NumVector2(4f, 0f), position + new NumVector2(width - 4f, 0f),
                ImGui.GetColorU32(SonnetTheme.Accent), 2f);
        }

        private void FinishModuleDrag()
        {
            if (_moduleDragActive && !string.IsNullOrEmpty(_dragModuleId)
                && SonnetMain.Modules != null
                && SonnetMain.Modules.ReorderDisplay(_dragModuleId, _moduleDragTargetIndex))
            {
                SonnetMain.Settings.ModuleOrder = SonnetMain.Modules.GetDisplayOrder();
                SonnetMain.SaveSettings();
            }
            CancelModuleDrag();
        }

        private void CancelModuleDrag()
        {
            _dragModuleId = null;
            _modulePressPending = false;
            _moduleDragActive = false;
            _moduleDragTargetIndex = 0;
        }

        private static void SelectModule(LoadedModule module)
        {
            if (module == null || SonnetMain.Settings == null) return;
            SonnetMain.Settings.SelectedModuleId = module.Metadata.Id;
            SonnetMain.Settings.SelectedTabId = module.Instance.Tabs != null && module.Instance.Tabs.Count > 0
                ? module.Instance.Tabs[0].Id : string.Empty;
        }

        private static bool DrawSidebarItem(string id, string label, bool active, float width)
        {
            NumVector2 size = new NumVector2(width, 42f);
            NumVector2 min = ImGui.GetCursorScreenPos();
            bool clicked = ImGui.InvisibleButton("##Nav_" + id, size);
            bool hovered = ImGui.IsItemHovered();
            ImDrawListPtr draw = ImGui.GetWindowDrawList();
            if (active || hovered)
            {
                NumVector4 background = active
                    ? new NumVector4(1f, 1f, 1f, 0.075f)
                    : new NumVector4(1f, 1f, 1f, 0.045f);
                draw.AddRectFilled(min, min + size, ImGui.GetColorU32(background), 6f);
            }
            if (active)
            {
                draw.AddRectFilled(
                    new NumVector2(min.X + 2f, min.Y + 8f),
                    new NumVector2(min.X + 5.5f, min.Y + size.Y - 8f),
                    ImGui.GetColorU32(SonnetTheme.Accent), 2f);
            }
            NumVector2 textSize = ImGui.CalcTextSize(label);
            NumVector2 textPos = min + new NumVector2((width - textSize.X) * 0.5f, (size.Y - textSize.Y) * 0.5f);
            draw.AddText(textPos, ImGui.GetColorU32(active ? SonnetTheme.Text : SonnetTheme.MutedText), label);
            return clicked;
        }

        private void DrawContent(float width, float height)
        {
            ImGui.BeginChild("##SonnetContent", new NumVector2(width, height), ImGuiChildFlags.None);
            DrawPanelDragRegion("content", Mathf.Max(0f, width - 64f), 68f);
            string id = SonnetMain.Settings.SelectedModuleId;
            if (string.IsNullOrEmpty(id) || (id != "__settings" && SonnetMain.Modules?.Find(id) == null))
            {
                var displayModules = SonnetMain.Modules?.DisplayModules;
                var first = displayModules != null && displayModules.Count > 0 ? displayModules[0] : null;
                id = first?.Metadata.Id ?? "__settings";
                SonnetMain.Settings.SelectedModuleId = id;
            }

            string currentTabId = SonnetMain.Settings.SelectedTabId ?? string.Empty;
            if (_lastAnimatedModuleId != id || _lastAnimatedTabId != currentTabId)
            {
                _lastAnimatedModuleId = id;
                _lastAnimatedTabId = currentTabId;
                if (SonnetMain.Settings.EnableUiAnimations)
                {
                    _contentAlpha = 0f;
                }
            }

            if (SonnetMain.Settings.EnableUiAnimations)
            {
                float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.001f, 0.1f);
                if (_contentAlpha < 1f)
                    _contentAlpha = Mathf.MoveTowards(_contentAlpha, 1f, dt * 7f);
            }
            else
            {
                _contentAlpha = 1f;
            }

            LoadedModule module = id == "__settings" ? null : SonnetMain.Modules.Find(id);
            DrawModuleHeader(module, width);
            ImGui.SetCursorPosY(76f);

            // Page switches fade the text in; cards and controls stay put.
            int pushedTextColors = 0;
            if (SonnetMain.Settings.EnableUiAnimations && _contentAlpha < 0.999f)
            {
                ImGuiStylePtr style = ImGui.GetStyle();
                NumVector4 text = style.Colors[(int)ImGuiCol.Text];
                NumVector4 disabled = style.Colors[(int)ImGuiCol.TextDisabled];
                ImGui.PushStyleColor(ImGuiCol.Text, new NumVector4(text.X, text.Y, text.Z, text.W * _contentAlpha));
                ImGui.PushStyleColor(ImGuiCol.TextDisabled, new NumVector4(disabled.X, disabled.Y, disabled.Z, disabled.W * _contentAlpha));
                pushedTextColors = 2;
            }

            try
            {
                if (id == "__settings")
                {
                    ImGui.SetCursorPosX(24f);
                    BeginCard("##FrameworkSettingsPage", new NumVector2(width - 48f, height - 100f), SonnetTheme.Content);
                    NumVector2 settingsInnerSize = ImGui.GetWindowSize() - new NumVector2(52f, 44f);
                    ImGui.SetCursorPos(new NumVector2(26f, 22f));
                    ImGui.BeginChild("##FrameworkSettingsInner", settingsInnerSize, ImGuiChildFlags.None, ImGuiWindowFlags.NoBackground);
                    DrawFrameworkSettings();
                    ImGui.EndChild();
                    ImGui.EndChild();
                }
                else
                {
                    if (module.Failed)
                    {
                        ImGui.SetCursorPosX(28f);
                        ImGui.TextColored(new NumVector4(1f, 0.42f, 0.42f, 1f), L("模块运行时发生错误，已被框架隔离。详情请查看日志。", "This module hit an error and was isolated by the framework. See the log for details."));
                    }
                    else
                        DrawModuleWorkspace(module);
                }
            }
            finally
            {
                if (pushedTextColors > 0) ImGui.PopStyleColor(pushedTextColors);
            }
            ImGui.EndChild();
        }

        // A panel card: frosted backdrop (blurred image and panel mask, when enabled) under its tint
        // fill. Ends with ImGui.EndChild like BeginChild.
        private static void BeginCard(string id, NumVector2 size, NumVector4 fill)
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, 0u);
            ImGui.BeginChild(id, size, ImGuiChildFlags.None);
            ImGui.PopStyleColor();
            NumVector2 min = ImGui.GetWindowPos();
            NumVector2 max = min + ImGui.GetWindowSize();
            float rounding = ImGui.GetStyle().ChildRounding;
            ImDrawListPtr draw = ImGui.GetWindowDrawList();
            SonnetPanelBackground.DrawCardBackdrop(draw, min, max, rounding, SonnetMain.Settings);
            if (fill.W > 0f) draw.AddRectFilled(min, max, ImGui.GetColorU32(fill), rounding);
        }

        private void DrawPanelDragRegion(string id, float width, float height)
        {
            NumVector2 restore = ImGui.GetCursorPos();
            ImGui.SetCursorPos(new NumVector2(8f, 8f));
            ImGui.InvisibleButton("##SonnetPanelDrag_" + id, new NumVector2(Mathf.Max(0f, width - 8f), Mathf.Max(0f, height - 8f)));
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                NumVector2 current = SonnetMain.Settings.HasCustomPanelPosition
                    ? new NumVector2(SonnetMain.Settings.PanelPositionX, SonnetMain.Settings.PanelPositionY)
                    : _panelPosition;
                current += ImGui.GetIO().MouseDelta;
                current = SonnetWindowChrome.ClampPosition(current, _panelSize, _displaySize, 8f);
                SonnetMain.Settings.HasCustomPanelPosition = true;
                SonnetMain.Settings.PanelPositionX = current.X;
                SonnetMain.Settings.PanelPositionY = current.Y;
                _panelPositionChanged = true;
            }
            if (ImGui.IsItemDeactivated() && _panelPositionChanged)
            {
                _panelPositionChanged = false;
                SonnetMain.SaveSettings();
            }
            ImGui.SetCursorPos(restore);
        }

        private void DrawPanelResizeHandles(NumVector2 position, NumVector2 size)
        {
            const float edge = 9f;
            const float corner = 24f;
            int hoveredEdges = 0;
            int activeEdges = 0;
            DrawPanelResizeHandle("left", new NumVector2(0f, corner), new NumVector2(edge, Mathf.Max(1f, size.Y - corner * 2f)), 1, ImGuiMouseCursor.ResizeEW, position, size, ref hoveredEdges, ref activeEdges);
            DrawPanelResizeHandle("right", new NumVector2(size.X - edge, corner), new NumVector2(edge, Mathf.Max(1f, size.Y - corner * 2f)), 2, ImGuiMouseCursor.ResizeEW, position, size, ref hoveredEdges, ref activeEdges);
            DrawPanelResizeHandle("top", new NumVector2(corner, 0f), new NumVector2(Mathf.Max(1f, size.X - corner * 2f), edge), 4, ImGuiMouseCursor.ResizeNS, position, size, ref hoveredEdges, ref activeEdges);
            DrawPanelResizeHandle("bottom", new NumVector2(corner, size.Y - edge), new NumVector2(Mathf.Max(1f, size.X - corner * 2f), edge), 8, ImGuiMouseCursor.ResizeNS, position, size, ref hoveredEdges, ref activeEdges);
            DrawPanelResizeHandle("top_left", NumVector2.Zero, new NumVector2(corner), 1 | 4, ImGuiMouseCursor.ResizeNWSE, position, size, ref hoveredEdges, ref activeEdges);
            DrawPanelResizeHandle("top_right", new NumVector2(size.X - corner, 0f), new NumVector2(corner), 2 | 4, ImGuiMouseCursor.ResizeNESW, position, size, ref hoveredEdges, ref activeEdges);
            DrawPanelResizeHandle("bottom_left", new NumVector2(0f, size.Y - corner), new NumVector2(corner), 1 | 8, ImGuiMouseCursor.ResizeNESW, position, size, ref hoveredEdges, ref activeEdges);
            DrawPanelResizeHandle("bottom_right", size - new NumVector2(corner), new NumVector2(corner), 2 | 8, ImGuiMouseCursor.ResizeNWSE, position, size, ref hoveredEdges, ref activeEdges);
            SonnetWindowChrome.RenderResizeVisuals(position, size, activeEdges, hoveredEdges);
        }

        private void DrawPanelResizeHandle(string id, NumVector2 localPosition, NumVector2 handleSize, int edges,
            ImGuiMouseCursor cursor, NumVector2 position, NumVector2 size, ref int hoveredEdges, ref int activeEdges)
        {
            ImGui.SetNextWindowPos(position + localPosition, ImGuiCond.Always);
            ImGui.SetNextWindowSize(handleSize, ImGuiCond.Always);
            ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBackground
                | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, NumVector2.Zero);
            ImGui.Begin("##SonnetResizeWindow_" + id, flags);
            ImGui.InvisibleButton("##SonnetResize_" + id, handleSize);
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
                NumVector2 minimum = new NumVector2(Mathf.Min(760f, _displaySize.X - 16f), Mathf.Min(540f, _displaySize.Y - 16f));
                NumVector2 maximum = new NumVector2(Mathf.Max(minimum.X, _displaySize.X - 16f), Mathf.Max(minimum.Y, _displaySize.Y - 16f));
                float right = position.X + size.X;
                float bottom = position.Y + size.Y;
                if ((edges & 1) != 0)
                {
                    nextPosition.X = Mathf.Clamp(position.X + delta.X, 8f, right - minimum.X);
                    nextSize.X = right - nextPosition.X;
                }
                if ((edges & 2) != 0) nextSize.X = Mathf.Clamp(size.X + delta.X, minimum.X, maximum.X - position.X + 8f);
                if ((edges & 4) != 0)
                {
                    nextPosition.Y = Mathf.Clamp(position.Y + delta.Y, 8f, bottom - minimum.Y);
                    nextSize.Y = bottom - nextPosition.Y;
                }
                if ((edges & 8) != 0) nextSize.Y = Mathf.Clamp(size.Y + delta.Y, minimum.Y, maximum.Y - position.Y + 8f);
                nextSize = SonnetWindowChrome.ClampSize(nextSize, _displaySize, minimum, 8f);
                nextPosition = SonnetWindowChrome.ClampPosition(nextPosition, nextSize, _displaySize, 8f);
                SonnetMain.Settings.HasCustomPanelPosition = true;
                SonnetMain.Settings.PanelPositionX = nextPosition.X;
                SonnetMain.Settings.PanelPositionY = nextPosition.Y;
                SonnetMain.Settings.HasCustomPanelSize = true;
                SonnetMain.Settings.PanelWidth = nextSize.X;
                SonnetMain.Settings.PanelHeight = nextSize.Y;
                _panelSizeChanged = true;
            }
            if (ImGui.IsItemDeactivated() && _panelSizeChanged)
            {
                _panelSizeChanged = false;
                SonnetMain.SaveSettings();
            }
            ImGui.End();
            ImGui.PopStyleVar();
        }

        private static void DrawModuleHeader(LoadedModule module, float width)
        {
            ImGui.SetCursorPos(new NumVector2(26f, 18f));
            if (HeadingFont.NativePtr != null) ImGui.PushFont(HeadingFont);
            ImGui.TextColored(SonnetTheme.Text, SonnetWidgets.Plain(module?.Metadata.DisplayName ?? L("设置", "Settings")));
            if (HeadingFont.NativePtr != null) ImGui.PopFont();
            ImGui.SameLine();
            ImGui.SetCursorPosY(29f);
            ImGui.TextColored(SonnetTheme.MutedText, module == null ? "Framework" : L("模块版本 ", "Module version ") + module.Metadata.Version);

            NumVector2 closeMin = new NumVector2(ImGui.GetWindowPos().X + width - 55f, ImGui.GetWindowPos().Y + 20f);
            ImGui.SetCursorScreenPos(closeMin);
            if (ImGui.InvisibleButton("##CloseSonnet", new NumVector2(32f, 32f))) SonnetMain.IsPanelOpen = false;
            uint color = ImGui.GetColorU32(ImGui.IsItemHovered() ? SonnetTheme.Text : SonnetTheme.MutedText);
            ImDrawListPtr draw = ImGui.GetWindowDrawList();
            draw.AddLine(closeMin + new NumVector2(9f, 9f), closeMin + new NumVector2(23f, 23f), color, 1.6f);
            draw.AddLine(closeMin + new NumVector2(23f, 9f), closeMin + new NumVector2(9f, 23f), color, 1.6f);
        }

        private static void DrawModuleWorkspace(LoadedModule module)
        {
            NumVector2 available = ImGui.GetContentRegionAvail();
            var moduleTabs = module.Instance.Tabs;
            bool hasTabs = moduleTabs != null && moduleTabs.Count > 0;
            if (hasTabs)
            {
                bool selectedTabExists = false;
                for (int i = 0; i < moduleTabs.Count; i++)
                    if (string.Equals(moduleTabs[i].Id, SonnetMain.Settings.SelectedTabId, StringComparison.Ordinal)) { selectedTabExists = true; break; }
                if (!selectedTabExists) SonnetMain.Settings.SelectedTabId = moduleTabs[0].Id;
            }
            const float gap = 16f;
            ImGui.SetCursorPosX(24f);
            available.X -= 48f;
            available.Y -= 24f;
            float tabWidth = hasTabs ? Mathf.Clamp(available.X * 0.16f, 165f, 230f) : 0f;
            if (hasTabs)
            {
                BeginCard("##ModuleTabs", new NumVector2(tabWidth, available.Y), SonnetTheme.Tabs);
                ImGui.SetCursorPosY(16f);
                string navigationTitle = module.Metadata.DisplayNavigationTitle;
                if (string.IsNullOrWhiteSpace(navigationTitle)) navigationTitle = L("功能列表", "Features");
                NumVector2 navigationTitleSize = ImGui.CalcTextSize(navigationTitle);
                ImGui.SetCursorPosX((tabWidth - navigationTitleSize.X) * 0.5f);
                ImGui.TextColored(SonnetTheme.Text, SonnetWidgets.Plain(navigationTitle));
                float lineHalfWidth = Mathf.Min(62f, tabWidth * 0.32f);
                DrawHorizontalLine(tabWidth * 0.5f - lineHalfWidth, tabWidth * 0.5f + lineHalfWidth, 48f);
                ImGui.SetCursorPosY(58f);
                foreach (var tab in moduleTabs)
                {
                    bool active = string.Equals(SonnetMain.Settings.SelectedTabId, tab.Id, StringComparison.Ordinal);
                    if (DrawModuleTab(module.Metadata.Id + "_" + tab.Id, tab.DisplayTitle, active, tabWidth, 38f))
                        SonnetMain.Settings.SelectedTabId = tab.Id;
                }
                ImGui.EndChild();
                ImGui.SameLine(0f, gap);
            }

            float pageWidth = available.X - (hasTabs ? tabWidth + gap : 0f);
            BeginCard("##ModulePage", new NumVector2(pageWidth, available.Y), SonnetTheme.Content);
            NumVector2 moduleInnerSize = ImGui.GetWindowSize() - new NumVector2(52f, 44f);
            ImGui.SetCursorPos(new NumVector2(26f, 22f));
            ImGui.BeginChild("##ModulePageInner", moduleInnerSize, ImGuiChildFlags.None, ImGuiWindowFlags.NoBackground);
            SonnetMain.Modules.DrawCurrent(module.Metadata.Id, SonnetMain.Settings.SelectedTabId, ImGui.GetContentRegionAvail());
            ImGui.EndChild();
            ImGui.EndChild();
        }

        private void DrawFrameworkSettings()
        {
            ImGui.TextColored(SonnetTheme.Text, L("框架", "Framework"));
            ImGui.Separator();
            DrawLanguageSelector();
            string shortcutLabel = _shortcutLabel(SonnetMain.PanelShortcut);
            if (ImGui.Button(L("绑定面板按键\t【", "Bind panel key\t[") + shortcutLabel + L("】", "]") + "##BindPanelKey"))
                _requestShortcut();
            ImGui.SameLine();
            ImGui.TextDisabled(_waitingShortcut() ? L("请按下新的按键", "Press a new key") : L("点击按钮后绑定", "Click the button, then press a key"));
            ImGui.Spacing();
            ImGui.TextColored(SonnetTheme.Text, L("品牌与动效", "Branding and animation"));
            ImGui.Separator();
            // Empty fields fall back to the defaults, shown as the input hint.
            string brandTitle = SonnetMain.Settings.BrandTitle ?? "";
            ImGui.SetNextItemWidth(Mathf.Min(360f, ImGui.GetContentRegionAvail().X - 100f));
            if (ImGui.InputTextWithHint(L("左上角标题", "Panel title"), DefaultBrandTitle, ref brandTitle, 64))
            {
                SonnetMain.Settings.BrandTitle = brandTitle;
                SonnetMain.SaveSettings();
            }
            ImGui.SameLine();
            if (ImGui.Button(L("恢复默认##ResetBrandTitle", "Reset##ResetBrandTitle")))
            {
                SonnetMain.Settings.BrandTitle = "";
                SonnetMain.SaveSettings();
            }

            string brandSubtitle = SonnetMain.Settings.BrandSubtitle ?? "";
            ImGui.SetNextItemWidth(Mathf.Min(360f, ImGui.GetContentRegionAvail().X - 100f));
            if (ImGui.InputTextWithHint(L("副标题", "Subtitle"), DefaultBrandSubtitle, ref brandSubtitle, 64))
            {
                SonnetMain.Settings.BrandSubtitle = brandSubtitle;
                SonnetMain.SaveSettings();
            }
            ImGui.SameLine();
            if (ImGui.Button(L("恢复默认##ResetBrandSubtitle", "Reset##ResetBrandSubtitle")))
            {
                SonnetMain.Settings.BrandSubtitle = "";
                SonnetMain.SaveSettings();
            }

            bool enableAnim = SonnetMain.Settings.EnableUiAnimations;
            if (ImGui.Checkbox(L("启用界面平滑过渡动效", "Smooth page transitions"), ref enableAnim))
            {
                SonnetMain.Settings.EnableUiAnimations = enableAnim;
                SonnetMain.SaveSettings();
            }

            ImGui.Spacing();
            ImGui.TextColored(SonnetTheme.Text, L("渲染", "Display"));
            ImGui.Separator();
            float scale = SonnetMain.Settings.UiScale;
            if (SonnetWidgets.SliderFloat(L("界面缩放", "UI scale"), ref scale, 0.75f, 1.5f, "%.2fx")) SonnetMain.Settings.UiScale = scale;
            ImGui.Spacing();
            ImGui.TextColored(SonnetTheme.Text, L("主题", "Theme"));
            ImGui.Separator();
            DrawThemeSettings(SonnetMain.Settings);
            ImGui.Spacing();
            ImGui.TextColored(SonnetTheme.Text, L("背景图片", "Background image"));
            ImGui.Separator();
            bool imageEnabled = SonnetMain.Settings.EnableBackgroundImage;
            if (ImGui.Checkbox(L("启用背景图片", "Show background image"), ref imageEnabled)) SonnetMain.Settings.EnableBackgroundImage = imageEnabled;
            string imagePath = SonnetMain.Settings.BackgroundImagePath ?? string.Empty;
            ImGui.SetNextItemWidth(Mathf.Min(620f, ImGui.GetContentRegionAvail().X - 210f));
            if (ImGui.InputText(L("图片路径", "Image path"), ref imagePath, 1024)) SonnetMain.Settings.BackgroundImagePath = imagePath;
            ImGui.SameLine();
            if (ImGui.Button(L("重新加载", "Reload"))) SonnetPanelBackground.Reload();
            ImGui.SameLine();
            if (ImGui.Button(L("默认背景", "Default image")))
            {
                SonnetMain.Settings.BackgroundImagePath = FrameworkSettings.DefaultBackgroundImagePath;
                SonnetMain.SaveSettings();
            }
            float imageOpacity = SonnetMain.Settings.BackgroundImageOpacity;
            if (SonnetWidgets.SliderFloat(L("图片透明度", "Image opacity"), ref imageOpacity, 0f, 1f, "%.2f")) SonnetMain.Settings.BackgroundImageOpacity = imageOpacity;
            bool imageBlur = SonnetMain.Settings.BlurBackgroundImage;
            if (ImGui.Checkbox(L("模糊背景图片", "Blur background image"), ref imageBlur)) SonnetMain.Settings.BlurBackgroundImage = imageBlur;
            ImGui.SameLine(0f, 28f);
            bool frosted = SonnetMain.Settings.FrostedCards;
            if (ImGui.Checkbox(L("控件背景模糊", "Frosted panels"), ref frosted)) SonnetMain.Settings.FrostedCards = frosted;
            if (imageBlur || frosted)
            {
                float imageBlurStrength = SonnetMain.Settings.BackgroundImageBlurStrength;
                if (SonnetWidgets.SliderFloat(L("图片模糊强度", "Blur strength"), ref imageBlurStrength, 1f, 20f, "%.1f"))
                    SonnetMain.Settings.BackgroundImageBlurStrength = imageBlurStrength;
            }
            ImGui.TextWrapped(SonnetWidgets.Plain(SonnetPanelBackground.Status));
            ImGui.Spacing();
            ImGui.TextColored(SonnetTheme.Text, L("特别感谢", "Special thanks"));
            ImGui.Separator();
            ImGui.TextUnformatted("ADOFAI Tweaks");
            ImGui.TextWrapped("https://github.com/PizzaLovers007/AdofaiTweaks");
            if (ImGui.Button(L("打开 ADOFAI Tweaks 仓库", "Open ADOFAI Tweaks repository")))
                Application.OpenURL("https://github.com/PizzaLovers007/AdofaiTweaks");
            ImGui.Spacing();
            ImGui.TextUnformatted("AETC - AAA幽蓝色栗子梦男");
            ImGui.TextWrapped("https://github.com/adofaiex/ADOFAI.EditorTweaks.ChartRendering");
            if (ImGui.Button(L("打开 AETC 项目地址", "Open AETC project page")))
                Application.OpenURL("https://github.com/adofaiex/ADOFAI.EditorTweaks.ChartRendering");
            ImGui.SameLine();
            if (ImGui.Button(L("访问 B站主页", "Visit Bilibili page")))
                Application.OpenURL("https://space.bilibili.com/1458477881");
            ImGui.Spacing();
            ImGui.TextDisabled(L("模块通过独立 DLL 接入；KV、OV 不属于 Framework 本体。", "Modules load as separate DLLs; KV and OV are not part of the Framework itself."));
        }

        private string _shortcutLabel(KeyCode key) => key == KeyCode.None ? L("未设置", "Not set") : key.ToString();

        private void _requestShortcut() => _waitingForPanelShortcut = true;

        private bool _waitingShortcut() => _waitingForPanelShortcut;

        private static KeyCode CapturePanelShortcut()
        {
            Array values = Enum.GetValues(typeof(KeyCode));
            for (int i = 0; i < values.Length; i++)
            {
                KeyCode key = (KeyCode)values.GetValue(i);
                int code = (int)key;
                if (code >= (int)KeyCode.Mouse0) continue;
                if (key != KeyCode.None && Input.GetKeyDown(key)) return key;
            }
            return KeyCode.None;
        }

        private static void DrawPanelBorder(ImDrawListPtr drawList, NumVector2 min, NumVector2 max, float rounding)
        {
            // 恢复原设计的三层边缘：外侧暗边负责与游戏画面分离，主线提供
            // 清晰轮廓，内侧极淡高光避免边缘呈现为一根生硬的灰线。
            // 临时放宽裁剪区域，才能保留位于窗口外侧的 3px 暗边。
            drawList.PushClipRect(NumVector2.Zero, ImGui.GetIO().DisplaySize, false);
            drawList.AddRect(min - NumVector2.One, max + NumVector2.One,
                ImGui.GetColorU32(new NumVector4(0f, 0f, 0f, 0.58f)), rounding + 1f,
                ImDrawFlags.RoundCornersAll, 3f);
            NumVector2 alignedMin = min + new NumVector2(0.5f, 0.5f);
            NumVector2 alignedMax = max - new NumVector2(0.5f, 0.5f);
            drawList.AddRect(alignedMin, alignedMax,
                ImGui.GetColorU32(new NumVector4(0.94f, 0.97f, 1f, 0.82f)), rounding - 0.5f,
                ImDrawFlags.RoundCornersAll, 1f);
            drawList.AddRect(alignedMin + NumVector2.One, alignedMax - NumVector2.One,
                ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, 0.11f)), rounding - 1.5f,
                ImDrawFlags.RoundCornersAll, 1f);
            drawList.PopClipRect();
        }

        private static readonly string[] LanguageCodes = { FrameworkSettings.LanguageAuto, FrameworkSettings.LanguageChinese, FrameworkSettings.LanguageEnglish };
        private static readonly string[] LanguageNamesZh = { "跟随游戏", "简体中文", "English" };
        private static readonly string[] LanguageNamesEn = { "Follow game", "简体中文", "English" };

        // The label and the language names stay bilingual, so a player can always find their way back.
        private static void DrawLanguageSelector()
        {
            FrameworkSettings settings = SonnetMain.Settings;
            int index = Math.Max(0, Array.IndexOf(LanguageCodes, settings.Language ?? FrameworkSettings.LanguageAuto));
            string[] names = SonnetText.IsChinese ? LanguageNamesZh : LanguageNamesEn;
            ImGui.SetNextItemWidth(Mathf.Min(360f, ImGui.GetContentRegionAvail().X - 100f));
            if (ImGui.Combo("语言 / Language##UiLanguage", ref index, names, names.Length))
            {
                settings.Language = LanguageCodes[index];
                SonnetMain.UpdateLanguage(force: true);
                SonnetMain.SaveSettings();
            }
        }

        private static void DrawThemeSettings(FrameworkSettings settings)
        {
            bool changed = false;
            bool save = false;
            for (int i = 0; i < SonnetTheme.Presets.Length; i++)
            {
                SonnetTheme.Preset preset = SonnetTheme.Presets[i];
                if (i > 0) ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, new NumVector4(preset.Accent[0], preset.Accent[1], preset.Accent[2], 1f));
                if (ImGui.Button(preset.DisplayName + "##ThemePreset" + i))
                {
                    settings.AccentColor = (float[])preset.Accent.Clone();
                    settings.MaskTint = (float[])preset.Tint.Clone();
                    changed = save = true;
                }
                ImGui.PopStyleColor();
            }

            const ImGuiColorEditFlags colorFlags = ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha;
            System.Numerics.Vector3 accent = ToRgb(settings.AccentColor, SonnetTheme.DefaultAccent);
            if (ImGui.ColorEdit3(L("强调色", "Accent color"), ref accent, colorFlags))
            {
                settings.AccentColor = new[] { accent.X, accent.Y, accent.Z };
                changed = true;
            }
            ImGui.SameLine(0f, 28f);
            System.Numerics.Vector3 tint = ToRgb(settings.MaskTint, SonnetTheme.DefaultTint);
            if (ImGui.ColorEdit3(L("遮罩色调", "Mask tint"), ref tint, colorFlags))
            {
                settings.MaskTint = new[] { tint.X, tint.Y, tint.Z };
                changed = true;
            }

            int maskStyle = settings.MaskStyle == 1 ? 1 : 0;
            if (ImGui.RadioButton(L("平铺遮罩", "Flat mask"), maskStyle == 0)) { settings.MaskStyle = 0; save = true; }
            ImGui.SameLine(0f, 20f);
            if (ImGui.RadioButton(L("晕影遮罩", "Vignette mask"), maskStyle == 1)) { settings.MaskStyle = 1; save = true; }

            float sliderWidth = Mathf.Min(360f, ImGui.GetContentRegionAvail().X - 100f);
            float mask = settings.BackgroundOpacity;
            ImGui.SetNextItemWidth(sliderWidth);
            if (SonnetWidgets.SliderFloat(L("背景遮罩", "Background mask"), ref mask, 0f, SonnetTheme.MaxLayerOpacity, "%.2f"))
                settings.BackgroundOpacity = mask;
            float card = settings.CardOpacity;
            ImGui.SetNextItemWidth(sliderWidth);
            if (SonnetWidgets.SliderFloat(L("卡片底色", "Panel opacity"), ref card, 0f, SonnetTheme.MaxLayerOpacity, "%.2f"))
            {
                settings.CardOpacity = card;
                changed = true;
            }
            if (ImGui.Button(L("恢复默认主题", "Reset theme")))
            {
                settings.AccentColor = SonnetTheme.DefaultAccent;
                settings.MaskTint = SonnetTheme.DefaultTint;
                settings.BackgroundOpacity = SonnetTheme.DefaultMaskOpacity;
                settings.CardOpacity = SonnetTheme.DefaultCardOpacity;
                settings.MaskStyle = SonnetTheme.DefaultMaskStyle;
                changed = save = true;
            }
            ImGui.PushStyleColor(ImGuiCol.Text, SonnetTheme.MutedText);
            ImGui.TextWrapped(L("背景遮罩压暗整个面板，卡片底色只作用于侧栏和内容区。背景较亮时调高卡片底色，文字会更清晰。", "Background mask dims the whole window; panel opacity applies only to the sidebar and content panels. Raise panel opacity for clearer text on bright backgrounds."));
            ImGui.PopStyleColor();

            if (changed) SonnetTheme.Configure(settings);
            if (save) SonnetMain.SaveSettings();
        }

        private static System.Numerics.Vector3 ToRgb(float[] value, float[] fallback)
        {
            float[] rgb = value != null && value.Length >= 3 ? value : fallback;
            return new System.Numerics.Vector3(rgb[0], rgb[1], rgb[2]);
        }

        private static void DrawFittedText(ImDrawListPtr draw, ImFontPtr font, float size, NumVector2 position,
            float maxWidth, NumVector4 color, string text)
        {
            float textWidth = font.CalcTextSizeA(size, float.MaxValue, 0f, text).X;
            if (textWidth > maxWidth && textWidth > 0f) size *= maxWidth / textWidth;
            draw.AddText(font, size, position, ImGui.GetColorU32(color), text);
        }

        private static void DrawHorizontalLine(float startX, float endX, float y)
        {
            NumVector2 origin = ImGui.GetWindowPos();
            ImGui.GetWindowDrawList().AddLine(
                origin + new NumVector2(startX, y), origin + new NumVector2(endX, y),
                SonnetTheme.Color(SonnetTheme.Hairline), 1f);
        }

        private static bool DrawModuleTab(string id, string label, bool active, float width, float height)
        {
            NumVector2 min = ImGui.GetCursorScreenPos();
            NumVector2 size = new NumVector2(width, height);
            bool clicked = ImGui.InvisibleButton("##ModuleTab_" + id, size);
            bool hovered = ImGui.IsItemHovered();
            ImDrawListPtr draw = ImGui.GetWindowDrawList();
            if (active || hovered)
            {
                NumVector4 background = active
                    ? new NumVector4(1f, 1f, 1f, 0.075f)
                    : new NumVector4(1f, 1f, 1f, 0.04f);
                draw.AddRectFilled(min + new NumVector2(8f, 2f), min + new NumVector2(width - 8f, height - 2f),
                    ImGui.GetColorU32(background), 5f);
            }
            if (active)
            {
                draw.AddRectFilled(
                    new NumVector2(min.X + 10f, min.Y + 7f),
                    new NumVector2(min.X + 13f, min.Y + height - 7f),
                    ImGui.GetColorU32(SonnetTheme.Accent), 1.5f);
            }
            NumVector2 textSize = ImGui.CalcTextSize(label);
            NumVector2 textPos = min + new NumVector2((width - textSize.X) * 0.5f, (height - textSize.Y) * 0.5f);
            draw.AddText(textPos, ImGui.GetColorU32(active ? SonnetTheme.Text : SonnetTheme.MutedText), label);
            return clicked;
        }

        private void BuildFontAtlas()
        {
            ImGuiIOPtr io = ImGui.GetIO();
            string font = Path.Combine(SonnetMain.FrameworkDirectory ?? string.Empty, "Resources", "MiSans-Bold.ttf");
            if (File.Exists(font))
            {
                IntPtr chineseGlyphs = io.Fonts.GetGlyphRangesChineseFull();
                io.Fonts.AddFontFromFileTTF(font, 20f, null, chineseGlyphs);
                HeadingFont = io.Fonts.AddFontFromFileTTF(font, 29f, null, chineseGlyphs);
            }
            else
            {
                HeadingFont = io.Fonts.AddFontDefault();
            }
            io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out int bpp);
            _fontTexture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            _fontTexture.LoadRawTextureData(pixels, width * height * bpp);
            _fontTexture.Apply(false, false);
            io.Fonts.SetTexID((IntPtr)_fontTexture.GetInstanceID());
            io.Fonts.ClearTexData();
        }

        private static void AddKeys(ImGuiIOPtr io, bool textEditing)
        {
            // 菜单默认只接受鼠标，避免游戏中的 Tab、Space、方向键等意外
            // 切换焦点、触发按钮或改变滑块。仅当文本框正在编辑时才注入
            // 文本编辑按键；FreeMake 的画布快捷键由其自身直接读取 Unity Input。
            // 即使不在编辑文本也要发送 false，避免离开文本框时把最后一个
            // 按键状态永久留在 ImGui 中。
            io.AddKeyEvent(ImGuiKey.Tab, false);
            io.AddKeyEvent(ImGuiKey.Space, false);
            AddKey(io, ImGuiKey.LeftArrow, KeyCode.LeftArrow, textEditing); AddKey(io, ImGuiKey.RightArrow, KeyCode.RightArrow, textEditing);
            AddKey(io, ImGuiKey.UpArrow, KeyCode.UpArrow, textEditing); AddKey(io, ImGuiKey.DownArrow, KeyCode.DownArrow, textEditing);
            AddKey(io, ImGuiKey.Backspace, KeyCode.Backspace, textEditing); AddKey(io, ImGuiKey.Delete, KeyCode.Delete, textEditing);
            AddKey(io, ImGuiKey.Enter, KeyCode.Return, textEditing); AddKey(io, ImGuiKey.Escape, KeyCode.Escape, textEditing);
            AddKey(io, ImGuiKey.A, KeyCode.A, textEditing); AddKey(io, ImGuiKey.C, KeyCode.C, textEditing); AddKey(io, ImGuiKey.V, KeyCode.V, textEditing);
            AddKey(io, ImGuiKey.X, KeyCode.X, textEditing); AddKey(io, ImGuiKey.Y, KeyCode.Y, textEditing); AddKey(io, ImGuiKey.Z, KeyCode.Z, textEditing);
            io.AddKeyEvent(ImGuiKey.ModCtrl, Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
            io.AddKeyEvent(ImGuiKey.ModShift, Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            io.AddKeyEvent(ImGuiKey.ModAlt, Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));
        }

        private static void AddKey(ImGuiIOPtr io, ImGuiKey key, KeyCode code, bool enabled) => io.AddKeyEvent(key, enabled && Input.GetKey(code));

        private void UpdateCanvas()
        {
            ImDrawDataPtr data = ImGui.GetDrawData();
            if (data.CmdListsCount == 0) { ClearCanvas(); return; }
            _hasContent = true;
            int commandIndex = 0;
            float offsetX = -Screen.width * 0.5f;
            float offsetY = Screen.height * 0.5f;
            for (int listIndex = 0; listIndex < data.CmdListsCount; listIndex++)
            {
                ImDrawListPtr list = data.CmdLists[listIndex];
                int vertexCount = list.VtxBuffer.Size;
                int indexCount = list.IdxBuffer.Size;
                EnsureArrays(vertexCount, indexCount);
                ImDrawVert* vertices = (ImDrawVert*)list.VtxBuffer.Data;
                for (int i = 0; i < vertexCount; i++)
                {
                    _vertices[i] = new Vector3(offsetX + vertices[i].pos.X * _scale, offsetY - vertices[i].pos.Y * _scale, 0f);
                    _uvs[i] = new UnityEngine.Vector2(vertices[i].uv.X, vertices[i].uv.Y);
                    uint color = vertices[i].col;
                    _colors[i] = new Color32((byte)color, (byte)(color >> 8), (byte)(color >> 16), (byte)(color >> 24));
                }
                ushort* indices = (ushort*)list.IdxBuffer.Data;
                for (int i = 0; i < indexCount; i++) _indices[i] = indices[i];
                for (int i = 0; i < list.CmdBuffer.Size; i++)
                {
                    ImDrawCmdPtr command = list.CmdBuffer[i];
                    if (command.UserCallback != IntPtr.Zero) continue;
                    EnsureRenderer(commandIndex);
                    int count = (int)command.ElemCount;
                    if (_commandIndices.Length < count) _commandIndices = new int[count];
                    for (int n = 0; n < count; n++) _commandIndices[n] = _indices[(int)command.IdxOffset + n] + (int)command.VtxOffset;
                    Mesh mesh = _meshes[commandIndex]; mesh.Clear(); mesh.SetVertices(_vertices, 0, vertexCount); mesh.SetUVs(0, _uvs, 0, vertexCount); mesh.SetColors(_colors, 0, vertexCount); mesh.SetIndices(_commandIndices, 0, count, MeshTopology.Triangles, 0);
                    float clipX = offsetX + command.ClipRect.X * _scale; float clipY = offsetY - command.ClipRect.W * _scale; float clipW = (command.ClipRect.Z - command.ClipRect.X) * _scale; float clipH = (command.ClipRect.W - command.ClipRect.Y) * _scale;
                    Texture renderTexture = _fontTexture;
                    if (command.TextureId != IntPtr.Zero && command.TextureId != (IntPtr)_fontTexture.GetInstanceID())
                        renderTexture = SonnetTextureRegistry.Resolve(command.TextureId) ?? _fontTexture;
                    _renderers[commandIndex].EnableRectClipping(new Rect(clipX, clipY, clipW, clipH)); _renderers[commandIndex].SetMaterial(_material, renderTexture); _renderers[commandIndex].SetColor(Color.white); _renderers[commandIndex].SetMesh(mesh); commandIndex++;
                }
            }
            for (int i = commandIndex; i < _renderers.Count; i++) _renderers[i].Clear();
        }

        private void EnsureArrays(int vertices, int indices)
        {
            if (_vertices.Length < vertices) { _vertices = new Vector3[vertices]; _uvs = new UnityEngine.Vector2[vertices]; _colors = new Color32[vertices]; }
            if (_indices.Length < indices) _indices = new int[indices];
        }

        private void EnsureRenderer(int index)
        {
            if (index < _renderers.Count) return;
            var child = new GameObject("Sonnet_ImGui_Command_" + index, typeof(RectTransform)); child.transform.SetParent(_canvas.transform, false); _renderers.Add(child.AddComponent<CanvasRenderer>());
            var mesh = new Mesh { name = "Sonnet_ImGui_Mesh_" + index }; mesh.MarkDynamic(); _meshes.Add(mesh);
        }

        private void ClearCanvas()
        {
            if (!_hasContent) return;
            foreach (var renderer in _renderers) renderer.Clear();
            _hasContent = false;
        }

        private void OnDestroy()
        {
            if (_context != IntPtr.Zero) { ImGui.SetCurrentContext(_context); SonnetImeBridge.Shutdown(); ImGui.DestroyContext(_context); _context = IntPtr.Zero; }
            if (_fontTexture != null) Destroy(_fontTexture); if (_material != null) Destroy(_material); if (_canvas != null) Destroy(_canvas.gameObject);
            if (_inputBlockerCanvas != null) Destroy(_inputBlockerCanvas.gameObject);
            foreach (var mesh in _meshes) if (mesh != null) Destroy(mesh);
            SonnetPanelBackground.Shutdown();
            SonnetTextureRegistry.Clear();
        }

        private void CreateInputBlocker()
        {
            var blockerObject = new GameObject("CheryTools_Sonnet_GameInputBlocker");
            DontDestroyOnLoad(blockerObject);
            _inputBlockerCanvas = blockerObject.AddComponent<Canvas>();
            _inputBlockerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _inputBlockerCanvas.sortingOrder = 32766;
            blockerObject.AddComponent<GraphicRaycaster>();

            var imageObject = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(blockerObject.transform, false);
            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _inputBlockerImage = imageObject.GetComponent<Image>();
            _inputBlockerImage.color = Color.clear;
            _inputBlockerImage.raycastTarget = true;
            SetInputBlockerActive(false);
        }

        private void SetInputBlockerActive(bool active)
        {
            if (_inputBlockerCanvas != null) _inputBlockerCanvas.enabled = active;
        }
    }
}
