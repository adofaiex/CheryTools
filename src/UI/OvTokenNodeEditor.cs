using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using UnityEngine;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace CheryTools
{
    internal static class OvTokenNodeEditor
    {
        private const float HeaderHeight = 28f;
        private const int UndoLimit = 10;
        private static OverlayerText _text;
        private static OverlayerImage _image;
        private static string _selectedNodeId = string.Empty;
        private static readonly HashSet<string> SelectedNodeIds = new HashSet<string>();
        private static string _draggingNodeId = string.Empty;
        private static readonly HashSet<string> DraggingNodeIds = new HashSet<string>();
        private static string _resizingGroupId = string.Empty;
        private static string _linkNodeId = string.Empty;
        private static string _linkPort = string.Empty;
        private static bool _marqueeActive;
        private static Vector2 _marqueeStart;
        private static Vector2 _marqueeEnd;
        private static readonly HashSet<string> MarqueeBaseSelection = new HashSet<string>();
        private static bool _duplicatePlacementActive;
        private static Vector2 _duplicateMouseStartGraph;
        private static readonly Dictionary<string, Vector2> DuplicateStartPositions = new Dictionary<string, Vector2>();
        private static readonly HashSet<string> DuplicateNodeIds = new HashSet<string>();
        private static readonly HashSet<string> DuplicateSourceSelection = new HashSet<string>();
        private static Vector2 _canvasMin;
        private static Vector2 _canvasMax;
        private static readonly List<OvAnimationGraph> UndoHistory = new List<OvAnimationGraph>();
        private static OvAnimationGraph _historyBaseline;
        private static bool _historyTransactionActive;
        private static bool _historyPendingCommit;
        private static bool _suppressHistory;
        private static Vector2 _pan = new Vector2(110f, 80f);
        private static float _zoom = 1f;
        private static bool _centerNext = true;
        private static string _keyCaptureNodeId = string.Empty;
        private static int _keyCaptureStartFrame = -1;
        private static readonly KeyCode[] KeyboardKeys = BuildKeyboardKeys();
        private static readonly string[] JudgementLabels =
        {
            "Too Early", "Very Early", "Early Perfect", "Perfect",
            "Late Perfect", "Very Late", "Too Late", "多押",
            "错过", "按太快", "Auto", "OverPress"
        };
        public static bool IsOpen { get; private set; }

        private static bool IsImageMode
        {
            get { return _image != null; }
        }

        private static OvAnimationGraph CurrentGraph
        {
            get { return IsImageMode ? _image.NodeAnimation : _text != null ? _text.TokenAnimation : null; }
        }

        private static void SetCurrentGraph(OvAnimationGraph graph)
        {
            if (IsImageMode) _image.NodeAnimation = graph;
            else if (_text != null) _text.TokenAnimation = graph;
        }

        private struct NodeGeometry
        {
            public Vector2 Min;
            public Vector2 Max;
            public Vector2 FlowIn;
            public Vector2 TargetIn;
            public Vector2 FlowOut;
            public Vector2 TargetOut;
        }

        public static void Open(OverlayerText text)
        {
            if (text == null) return;
            _text = text;
            _image = null;
            if (CurrentGraph == null) SetCurrentGraph(OvAnimationGraph.CreateDefault());
            OvTextTokenService.EnsureBindings(_text);
            ResetEditorState();
            _suppressHistory = true;
            PopulateDefaultSelection(_text);
            _suppressHistory = false;
            IsOpen = true;
            _centerNext = true;
            _historyBaseline = OvAnimationGraph.Clone(CurrentGraph);
            if (CurrentGraph.Nodes != null && CurrentGraph.Nodes.Count > 0)
            {
                SelectOnly(CurrentGraph.Nodes[0].Id);
            }
        }

        public static void Open(OverlayerImage image)
        {
            if (image == null) return;
            _text = null;
            _image = image;
            if (_image.NodeAnimation == null) _image.NodeAnimation = OvImageNodeAnimation.CreateDefault();
            OvImageNodeAnimation.EnsureImageTarget(_image.NodeAnimation);
            ResetEditorState();
            IsOpen = true;
            _centerNext = true;
            _historyBaseline = OvAnimationGraph.Clone(_image.NodeAnimation);
            if (_image.NodeAnimation.Nodes != null && _image.NodeAnimation.Nodes.Count > 0)
            {
                SelectOnly(_image.NodeAnimation.Nodes[0].Id);
            }
        }

        public static void Draw()
        {
            if (!IsOpen) return;
            if ((_text == null && _image == null) || Main.Settings == null)
            {
                IsOpen = false;
                return;
            }

            if (IsImageMode)
            {
                if (_image.NodeAnimation == null) _image.NodeAnimation = OvImageNodeAnimation.CreateDefault();
            }
            else
            {
                OvTextTokenService.EnsureBindings(_text);
                if (CurrentGraph == null) SetCurrentGraph(OvAnimationGraph.CreateDefault());
            }
            if (!IsImageMode) EnsureGraphIds(CurrentGraph);

            if (_centerNext)
            {
                ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
                _centerNext = false;
            }
            ImGui.SetNextWindowSize(new Vector2(1180f, 720f), ImGuiCond.FirstUseEver);
            bool open = IsOpen;
            string title = IsImageMode
                ? "OV 图片节点动画##OvTokenNodeEditor"
                : "OV Token 节点动画 - " + (string.IsNullOrEmpty(_text.Name) ? "文本" : _text.Name) + "##OvTokenNodeEditor";
            if (!ImGui.Begin(title, ref open, ImGuiWindowFlags.NoCollapse))
            {
                ImGui.End();
                IsOpen = open;
                return;
            }
            IsOpen = open;

            DrawToolbar();
            ImGui.Separator();

            Vector2 available = ImGui.GetContentRegionAvail();
            float inspectorWidth = Math.Max(280f, available.X * 0.25f);
            ImGui.BeginChild("OvNodeCanvas", new Vector2(available.X - inspectorWidth - 8f, available.Y), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            DrawCanvas();
            ImGui.EndChild();
            ImGui.SameLine();
            ImGui.BeginChild("OvNodeInspector", new Vector2(inspectorWidth, available.Y), ImGuiChildFlags.Borders);
            DrawInspector();
            ImGui.EndChild();

            HandleEditorShortcuts();
            FinalizeHistoryTransaction();
            ImGui.End();
        }

        private static void DrawToolbar()
        {
            OvAnimationGraph graph = CurrentGraph;
            bool enabled = graph.Enabled;
            if (ImGui.Checkbox("启用##OvNodeGraphEnabled", ref enabled))
            {
                graph.Enabled = enabled;
                MarkGraphChanged();
            }
            ImGui.SameLine();
            if (ImGui.Button("播放"))
            {
                graph.Enabled = true;
                MarkGraphChanged();
                if (IsImageMode) OverlayerManager.Instance?.PreviewImageNodeAnimation(_image);
                else OverlayerManager.Instance?.PreviewTokenAnimation(_text);
            }
            ImGui.SameLine();
            if (ImGui.Button("停止"))
            {
                if (IsImageMode) OverlayerManager.Instance?.StopImageNodeAnimation(_image);
                else OverlayerManager.Instance?.StopTokenAnimation(_text);
            }
            ImGui.SameLine();
            if (ImGui.Button("回到原点"))
            {
                _pan = new Vector2(110f, 80f);
                _zoom = 1f;
            }
            ImGui.SameLine();
            bool hold = graph.HoldFinalPose;
            if (ImGui.Checkbox("保持最终姿态", ref hold))
            {
                graph.HoldFinalPose = hold;
                MarkGraphChanged();
            }
            ImGui.SameLine();
            ImGui.TextDisabled("中键平移 | Ctrl 多选 | 框选 | Shift+D 复制 | Ctrl+Z 撤销 | Delete 删除");

        }

        private static void DrawCanvas()
        {
            OvAnimationGraph graph = CurrentGraph;
            Vector2 canvasMin = ImGui.GetCursorScreenPos();
            Vector2 canvasSize = ImGui.GetContentRegionAvail();
            canvasSize.X = Math.Max(64f, canvasSize.X);
            canvasSize.Y = Math.Max(64f, canvasSize.Y);
            Vector2 canvasMax = canvasMin + canvasSize;
            _canvasMin = canvasMin;
            _canvasMax = canvasMax;
            ImDrawListPtr draw = ImGui.GetWindowDrawList();
            uint background = Color(0.055f, 0.06f, 0.07f, 1f);
            draw.AddRectFilled(canvasMin, canvasMax, background);

            ImGui.InvisibleButton("##OvNodeCanvasSurface", canvasSize);
            bool hovered = ImGui.IsItemHovered();
            ImGuiIOPtr io = ImGui.GetIO();

            UpdateDuplicatePlacement(io.MousePos);

            if (hovered && ImGui.IsMouseDragging(ImGuiMouseButton.Middle))
            {
                _pan += io.MouseDelta;
            }
            if (hovered && Math.Abs(io.MouseWheel) > 0.001f)
            {
                float oldZoom = _zoom;
                _zoom = Math.Max(0.45f, Math.Min(2f, _zoom * (io.MouseWheel > 0f ? 1.1f : 0.9f)));
                Vector2 mouseLocal = io.MousePos - canvasMin;
                _pan = mouseLocal - (mouseLocal - _pan) * (_zoom / oldZoom);
            }

            DrawGrid(draw, canvasMin, canvasMax);
            var geometry = new Dictionary<string, NodeGeometry>();
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                OvAnimationNode node = graph.Nodes[i];
                if (node == null) continue;
                geometry[node.Id] = GetGeometry(node, canvasMin);
            }
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                OvAnimationNode node = graph.Nodes[i];
                if (node == null || node.Kind != OvAnimationNodeKind.GroupFrame || !geometry.TryGetValue(node.Id, out NodeGeometry g)) continue;
                DrawNode(draw, node, g);
            }
            DrawLinks(draw, graph, geometry);
            HandleCanvasInteraction(graph, geometry, canvasMin, canvasMax, hovered);
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                OvAnimationNode node = graph.Nodes[i];
                if (node == null || node.Kind == OvAnimationNodeKind.GroupFrame || !geometry.TryGetValue(node.Id, out NodeGeometry g)) continue;
                DrawNode(draw, node, g);
            }

            DrawMarquee(draw);

            if (!string.IsNullOrEmpty(_linkNodeId) && geometry.TryGetValue(_linkNodeId, out NodeGeometry source))
            {
                Vector2 start = _linkPort == "flow" ? source.FlowOut : source.TargetOut;
                DrawBezier(draw, start, io.MousePos, _linkPort == "flow" ? FlowColor() : TargetColor(), 3f);
            }

            if (hovered && (ImGui.IsMouseClicked(ImGuiMouseButton.Right)
                || ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.A))))
            {
                ImGui.OpenPopup("OvNodeAddPopup");
            }
            if (ImGui.BeginPopup("OvNodeAddPopup"))
            {
                Vector2 graphPosition = (io.MousePos - canvasMin - _pan) / _zoom;
                if (ImGui.MenuItem(IsImageMode ? "输入 / 当前图片" : "输入 / Token 选择")) AddNode(OvAnimationNodeKind.TokenInput, graphPosition);
                if (!IsImageMode && ImGui.MenuItem("修改 / Token 内容")) AddModify(graphPosition);
                if (!IsImageMode && ImGui.MenuItem("修改 / 数值格式")) AddNumberFormat(graphPosition);
                if (ImGui.MenuItem("布局 / 分组框")) AddGroupFrame(graphPosition);
                if (ImGui.BeginMenu("触发器"))
                {
                    if (ImGui.MenuItem("手动预览")) AddTrigger(OvAnimationTriggerKind.Manual, graphPosition);
                    if (ImGui.MenuItem("任意按键按下")) AddTrigger(OvAnimationTriggerKind.AnyKeyDown, graphPosition);
                    if (ImGui.MenuItem("Combo 增加")) AddTrigger(OvAnimationTriggerKind.ComboIncrease, graphPosition);
                    if (ImGui.MenuItem("指定按键")) AddTrigger(OvAnimationTriggerKind.SpecificKey, graphPosition);
                    if (ImGui.MenuItem("判定发生")) AddTrigger(OvAnimationTriggerKind.JudgementOccurred, graphPosition);
                    if (ImGui.MenuItem("Combo 断连")) AddTrigger(OvAnimationTriggerKind.ComboBreak, graphPosition);
                    if (ImGui.MenuItem("节拍")) AddTrigger(OvAnimationTriggerKind.Beat, graphPosition);
                    if (ImGui.MenuItem("关卡开始")) AddTrigger(OvAnimationTriggerKind.LevelStart, graphPosition);
                    if (ImGui.MenuItem("关卡结束")) AddTrigger(OvAnimationTriggerKind.LevelEnd, graphPosition);
                    if (ImGui.MenuItem("Tag 值变化")) AddTrigger(OvAnimationTriggerKind.TagValueChanged, graphPosition);
                    if (ImGui.MenuItem("Tag 数值条件")) AddTrigger(OvAnimationTriggerKind.TagNumericCondition, graphPosition);
                    if (ImGui.MenuItem("自动播放状态")) AddTrigger(OvAnimationTriggerKind.AutoplayState, graphPosition);
                    if (ImGui.MenuItem("不会死亡状态")) AddTrigger(OvAnimationTriggerKind.NoFailState, graphPosition);
                    if (ImGui.MenuItem("判定模式状态")) AddTrigger(OvAnimationTriggerKind.JudgementModeState, graphPosition);
                    ImGui.EndMenu();
                }
                if (ImGui.BeginMenu("动画"))
                {
                    if (ImGui.MenuItem("位移")) AddTween(OvTokenTweenProperty.Position, graphPosition);
                    if (ImGui.MenuItem("缩放")) AddTween(OvTokenTweenProperty.Scale, graphPosition);
                    if (ImGui.MenuItem("旋转")) AddTween(OvTokenTweenProperty.Rotation, graphPosition);
                    if (ImGui.MenuItem("透明度")) AddTween(OvTokenTweenProperty.Opacity, graphPosition);
                    ImGui.EndMenu();
                }
                if (!IsImageMode && ImGui.BeginMenu("持续效果"))
                {
                    if (ImGui.MenuItem("颜色映射 / 渐变")) AddColorEffect(graphPosition);
                    ImGui.EndMenu();
                }
                ImGui.EndPopup();
            }
        }

        private static void HandleCanvasInteraction(OvAnimationGraph graph, Dictionary<string, NodeGeometry> geometry, Vector2 canvasMin, Vector2 canvasMax, bool hovered)
        {
            ImGuiIOPtr io = ImGui.GetIO();

            if (_duplicatePlacementActive)
            {
                if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) FinishDuplicatePlacement();
                else if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) || Input.GetKeyDown(KeyCode.Escape)) CancelDuplicatePlacement();
                return;
            }

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                _draggingNodeId = string.Empty;
                DraggingNodeIds.Clear();
                bool ctrl = IsCtrlDown();

                for (int i = graph.Nodes.Count - 1; i >= 0; i--)
                {
                    OvAnimationNode node = graph.Nodes[i];
                    if (node == null || node.Kind == OvAnimationNodeKind.GroupFrame
                        || !geometry.TryGetValue(node.Id, out NodeGeometry g)) continue;
                    if (HasOutput(node, "flow") && Distance(io.MousePos, g.FlowOut) <= 9f)
                    {
                        _linkNodeId = node.Id;
                        _linkPort = "flow";
                        return;
                    }
                    if (HasOutput(node, "targets") && Distance(io.MousePos, g.TargetOut) <= 9f)
                    {
                        _linkNodeId = node.Id;
                        _linkPort = "targets";
                        return;
                    }
                    if (PointInRect(io.MousePos, g.Min, new Vector2(g.Max.X, g.Min.Y + HeaderHeight * _zoom)))
                    {
                        if (!UpdateSelectionForClick(node.Id, ctrl)) return;
                        BeginNodeDrag(node.Id, graph, geometry);
                        return;
                    }
                    if (PointInRect(io.MousePos, g.Min, g.Max))
                    {
                        UpdateSelectionForClick(node.Id, ctrl);
                        return;
                    }
                }

                for (int i = graph.Nodes.Count - 1; i >= 0; i--)
                {
                    OvAnimationNode node = graph.Nodes[i];
                    if (node == null || node.Kind != OvAnimationNodeKind.GroupFrame
                        || !geometry.TryGetValue(node.Id, out NodeGeometry g)) continue;
                    Vector2 resizeMin = g.Max - new Vector2(16f, 16f) * _zoom;
                    if (PointInRect(io.MousePos, resizeMin, g.Max))
                    {
                        if (!UpdateSelectionForClick(node.Id, ctrl)) return;
                        _resizingGroupId = node.Id;
                        return;
                    }
                    if (PointInRect(io.MousePos, g.Min, new Vector2(g.Max.X, g.Min.Y + HeaderHeight * _zoom)))
                    {
                        if (!UpdateSelectionForClick(node.Id, ctrl)) return;
                        BeginNodeDrag(node.Id, graph, geometry);
                        return;
                    }
                }

                if (hovered)
                {
                    _marqueeActive = true;
                    _marqueeStart = io.MousePos;
                    _marqueeEnd = io.MousePos;
                    MarqueeBaseSelection.Clear();
                    if (ctrl)
                    {
                        foreach (string id in SelectedNodeIds) MarqueeBaseSelection.Add(id);
                    }
                    else
                    {
                        ClearSelection();
                    }
                }
            }

            if (!string.IsNullOrEmpty(_resizingGroupId) && ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                OvAnimationNode node = FindNode(_resizingGroupId);
                if (node != null)
                {
                    node.EditorWidth = Math.Max(180f, node.EditorWidth + io.MouseDelta.X / _zoom);
                    node.EditorHeight = Math.Max(120f, node.EditorHeight + io.MouseDelta.Y / _zoom);
                }
            }
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) && !string.IsNullOrEmpty(_resizingGroupId))
            {
                _resizingGroupId = string.Empty;
                MarkGraphChanged();
            }

            if (!string.IsNullOrEmpty(_draggingNodeId) && ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                foreach (string nodeId in DraggingNodeIds)
                {
                    OvAnimationNode node = FindNode(nodeId);
                    if (node == null) continue;
                    node.EditorX += io.MouseDelta.X / _zoom;
                    node.EditorY += io.MouseDelta.Y / _zoom;
                }
            }
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) && !string.IsNullOrEmpty(_draggingNodeId))
            {
                _draggingNodeId = string.Empty;
                DraggingNodeIds.Clear();
                MarkGraphChanged();
            }

            if (_marqueeActive)
            {
                _marqueeEnd = new Vector2(
                    Math.Max(canvasMin.X, Math.Min(canvasMax.X, io.MousePos.X)),
                    Math.Max(canvasMin.Y, Math.Min(canvasMax.Y, io.MousePos.Y)));
                UpdateMarqueeSelection(graph, geometry);
                if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    _marqueeActive = false;
                    MarqueeBaseSelection.Clear();
                }
            }

            if (!string.IsNullOrEmpty(_linkNodeId) && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                string toNode = string.Empty;
                string toPort = string.Empty;
                foreach (KeyValuePair<string, NodeGeometry> pair in geometry)
                {
                    OvAnimationNode node = FindNode(pair.Key);
                    if (node == null || node.Id == _linkNodeId) continue;
                    if (_linkPort == "flow" && HasInput(node, "flow") && Distance(io.MousePos, pair.Value.FlowIn) <= 11f)
                    {
                        toNode = node.Id;
                        toPort = "flow";
                        break;
                    }
                    if (_linkPort == "targets" && HasInput(node, "targets") && Distance(io.MousePos, pair.Value.TargetIn) <= 11f)
                    {
                        toNode = node.Id;
                        toPort = "targets";
                        break;
                    }
                }
                if (!string.IsNullOrEmpty(toNode)) AddLink(_linkNodeId, _linkPort, toNode, toPort);
                _linkNodeId = string.Empty;
                _linkPort = string.Empty;
            }
        }

        private static void DrawGrid(ImDrawListPtr draw, Vector2 min, Vector2 max)
        {
            float spacing = 32f * _zoom;
            uint minor = Color(0.105f, 0.11f, 0.125f, 1f);
            float offsetX = _pan.X % spacing;
            float offsetY = _pan.Y % spacing;
            for (float x = min.X + offsetX; x < max.X; x += spacing) draw.AddLine(new Vector2(x, min.Y), new Vector2(x, max.Y), minor, 1f);
            for (float y = min.Y + offsetY; y < max.Y; y += spacing) draw.AddLine(new Vector2(min.X, y), new Vector2(max.X, y), minor, 1f);
        }

        private static void DrawLinks(ImDrawListPtr draw, OvAnimationGraph graph, Dictionary<string, NodeGeometry> geometry)
        {
            if (graph.Links == null) return;
            for (int i = 0; i < graph.Links.Count; i++)
            {
                OvAnimationLink link = graph.Links[i];
                if (link == null || !geometry.TryGetValue(link.FromNodeId, out NodeGeometry from) || !geometry.TryGetValue(link.ToNodeId, out NodeGeometry to)) continue;
                Vector2 a = link.FromPort == "targets" ? from.TargetOut : from.FlowOut;
                Vector2 b = link.ToPort == "targets" ? to.TargetIn : to.FlowIn;
                DrawBezier(draw, a, b, link.FromPort == "targets" ? TargetColor() : FlowColor(), 2.5f);
            }
        }

        private static void DrawNode(ImDrawListPtr draw, OvAnimationNode node, NodeGeometry g)
        {
            bool selected = SelectedNodeIds.Contains(node.Id);
            ImFontPtr canvasFont = ImGui.GetFont();
            float canvasFontSize = ImGui.GetFontSize() * _zoom;
            if (node.Kind == OvAnimationNodeKind.GroupFrame)
            {
                uint frameFill = Color(0.12f, 0.18f, 0.22f, 0.22f);
                uint frameHeader = Color(0.16f, 0.28f, 0.34f, 0.72f);
                uint frameBorder = selected ? Color(0.95f, 0.55f, 0.18f, 1f) : Color(0.30f, 0.48f, 0.56f, 0.8f);
                draw.AddRectFilled(g.Min, g.Max, frameFill, 4f);
                draw.AddRectFilled(g.Min, new Vector2(g.Max.X, g.Min.Y + HeaderHeight * _zoom), frameHeader, 4f);
                draw.AddRect(g.Min, g.Max, frameBorder, 4f, ImDrawFlags.None, selected ? 2.5f : 1.5f);
                draw.AddText(canvasFont, canvasFontSize, g.Min + new Vector2(10f, 6f) * _zoom, Color(0.92f, 0.95f, 0.96f, 1f),
                    string.IsNullOrWhiteSpace(node.Name) ? "分组" : node.Name);
                if (!string.IsNullOrWhiteSpace(node.EditorNote))
                {
                    draw.AddText(canvasFont, canvasFontSize, g.Min + new Vector2(12f, 40f) * _zoom,
                        Color(0.62f, 0.72f, 0.76f, 0.9f), SummarizeNote(node.EditorNote, 42));
                }
                Vector2 handle = g.Max - new Vector2(12f, 12f) * _zoom;
                draw.AddTriangleFilled(g.Max, new Vector2(handle.X, g.Max.Y), new Vector2(g.Max.X, handle.Y), frameBorder);
                return;
            }

            uint body = Color(0.11f, 0.12f, 0.14f, 0.98f);
            uint header = node.Kind == OvAnimationNodeKind.Trigger
                ? Color(0.36f, 0.16f, 0.12f, 1f)
                : node.Kind == OvAnimationNodeKind.TokenInput
                    ? Color(0.12f, 0.28f, 0.34f, 1f)
                     : node.Kind == OvAnimationNodeKind.Modify
                         ? Color(0.12f, 0.32f, 0.22f, 1f)
                         : node.Kind == OvAnimationNodeKind.NumberFormat
                             ? Color(0.18f, 0.30f, 0.18f, 1f)
                         : node.Kind == OvAnimationNodeKind.Effect
                             ? Color(0.10f, 0.34f, 0.30f, 1f)
                         : Color(0.20f, 0.16f, 0.34f, 1f);
            draw.AddRectFilled(g.Min, g.Max, body, 5f);
            draw.AddRectFilled(g.Min, new Vector2(g.Max.X, g.Min.Y + HeaderHeight * _zoom), header, 5f);
            draw.AddRect(g.Min, g.Max, selected ? Color(0.95f, 0.55f, 0.18f, 1f) : Color(0.24f, 0.25f, 0.28f, 1f), 5f, ImDrawFlags.None, selected ? 2.5f : 1f);
            draw.AddText(canvasFont, canvasFontSize, g.Min + new Vector2(10f, 6f) * _zoom,
                Color(0.94f, 0.94f, 0.94f, 1f), NodeTitle(node));
            draw.AddText(canvasFont, canvasFontSize, g.Min + new Vector2(12f, 43f) * _zoom,
                Color(0.68f, 0.7f, 0.74f, 1f), NodeSummary(node));
            if (!string.IsNullOrWhiteSpace(node.EditorNote))
            {
                draw.AddText(canvasFont, canvasFontSize, g.Min + new Vector2(12f, 73f) * _zoom,
                    Color(0.54f, 0.57f, 0.62f, 1f), SummarizeNote(node.EditorNote, 28));
            }

            if (HasInput(node, "flow")) DrawSocket(draw, g.FlowIn, FlowColor());
            if (HasInput(node, "targets")) DrawSocket(draw, g.TargetIn, TargetColor());
            if (HasOutput(node, "flow")) DrawSocket(draw, g.FlowOut, FlowColor());
            if (HasOutput(node, "targets")) DrawSocket(draw, g.TargetOut, TargetColor());
        }

        private static void DrawInspector()
        {
            OvAnimationNode node = FindNode(_selectedNodeId);
            if (node == null)
            {
                ImGui.TextDisabled("请选择一个节点");
                return;
            }
            if (SelectedNodeIds.Count > 1)
            {
                ImGui.TextDisabled("已选择 " + SelectedNodeIds.Count + " 个节点");
            }
            ImGui.Text(NodeTitle(node));
            ImGui.Separator();
            if (node.Kind == OvAnimationNodeKind.GroupFrame) DrawGroupFrameInspector(node);
            else if (node.Kind == OvAnimationNodeKind.TokenInput) DrawTokenInspector(node);
            else if (node.Kind == OvAnimationNodeKind.Trigger) DrawTriggerInspector(node);
            else if (node.Kind == OvAnimationNodeKind.Modify) DrawModifyInspector(node);
            else if (node.Kind == OvAnimationNodeKind.NumberFormat) DrawNumberFormatInspector(node);
            else if (node.Kind == OvAnimationNodeKind.Effect) DrawEffectInspector(node);
            else DrawTweenInspector(node);

            if (node.Kind != OvAnimationNodeKind.GroupFrame)
            {
                ImGui.Spacing();
                ImGui.SeparatorText("节点备注");
                string note = node.EditorNote ?? string.Empty;
                if (ImGui.InputTextMultiline("##node_note", ref note, 2048, new Vector2(-1f, 84f)))
                {
                    node.EditorNote = note;
                    MarkGraphChanged();
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            if (ImGui.Button(SelectedNodeIds.Count > 1 ? "删除选中节点" : "删除节点")) DeleteSelectedNodes();
        }

        private static void DrawGroupFrameInspector(OvAnimationNode node)
        {
            string title = node.Name ?? string.Empty;
            if (ImGui.InputText("分组名称", ref title, 128u))
            {
                node.Name = title;
                MarkGraphChanged();
            }

            Vector2 size = new Vector2(node.EditorWidth, node.EditorHeight);
            if (ImGui.DragFloat2("分组尺寸", ref size, 1f, 120f, 2000f))
            {
                node.EditorWidth = Math.Max(180f, size.X);
                node.EditorHeight = Math.Max(120f, size.Y);
                MarkGraphChanged();
            }

            string note = node.EditorNote ?? string.Empty;
            if (ImGui.InputTextMultiline("分组备注", ref note, 4096, new Vector2(-1f, 140f)))
            {
                node.EditorNote = note;
                MarkGraphChanged();
            }
            ImGui.TextDisabled("拖动标题移动分组；拖动右下角调整大小。移动分组时，框内节点会一并移动。");
        }

        private static void DrawTokenInspector(OvAnimationNode node)
        {
            if (IsImageMode)
            {
                OvImageNodeAnimation.EnsureNodeTarget(node);
                ImGui.TextWrapped("该输入节点固定输出当前图片，无需选择 Token。");
                ImGui.TextDisabled("把“图片输入”连接到动画节点的目标端口即可。");
                return;
            }
            ImGui.TextWrapped("选择该输入节点要输出的语义 Token。");
            if (node.SelectedTokenIds == null) node.SelectedTokenIds = new List<string>();
            if (ImGui.Button("全选"))
            {
                node.SelectedTokenIds.Clear();
                for (int i = 0; i < _text.TokenBindings.Count; i++) node.SelectedTokenIds.Add(_text.TokenBindings[i].Id);
                MarkGraphChanged();
            }
            ImGui.SameLine();
            if (ImGui.Button("清空"))
            {
                node.SelectedTokenIds.Clear();
                MarkGraphChanged();
            }
            ImGui.Separator();
            for (int i = 0; i < _text.TokenBindings.Count; i++)
            {
                OvTextTokenBinding token = _text.TokenBindings[i];
                bool selected = node.SelectedTokenIds.Contains(token.Id);
                string label = (i + 1).ToString() + "  " + OvTextTokenService.GetDisplayName(token) + "##token_" + token.Id;
                if (ImGui.Checkbox(label, ref selected))
                {
                    if (selected) node.SelectedTokenIds.Add(token.Id);
                    else node.SelectedTokenIds.Remove(token.Id);
                    MarkGraphChanged();
                }
                if (token.Kind == OvTextTokenKind.DynamicTag)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(0.32f, 0.78f, 0.9f, 1f), "动态标签");
                }
            }
        }

        private static void DrawTriggerInspector(OvAnimationNode node)
        {
            int value = (int)node.Trigger;
            if (ImGui.Combo("触发事件", ref value,
                "手动预览\0任意按键按下\0Combo 增加\0指定按键\0判定发生\0Combo 断连\0节拍\0关卡开始\0关卡结束\0Tag 值变化\0自动播放状态\0不会死亡状态\0判定模式状态\0Tag 数值条件\0"))
            {
                node.Trigger = (OvAnimationTriggerKind)value;
                StopKeyCapture();
                MarkGraphChanged();
            }

            ImGui.Spacing();
            if (node.Trigger == OvAnimationTriggerKind.SpecificKey) DrawSpecificKeyTrigger(node);
            else if (node.Trigger == OvAnimationTriggerKind.JudgementOccurred) DrawJudgementTrigger(node);
            else if (node.Trigger == OvAnimationTriggerKind.ComboBreak) DrawComboBreakTrigger(node);
            else if (node.Trigger == OvAnimationTriggerKind.Beat) DrawBeatTrigger(node);
            else if (node.Trigger == OvAnimationTriggerKind.TagValueChanged) DrawTagChangeTrigger(node);
            else if (node.Trigger == OvAnimationTriggerKind.TagNumericCondition) DrawTagNumericConditionTrigger(node);
            else if (node.Trigger == OvAnimationTriggerKind.AutoplayState
                || node.Trigger == OvAnimationTriggerKind.NoFailState) DrawToggleStateTrigger(node);
            else if (node.Trigger == OvAnimationTriggerKind.JudgementModeState) DrawJudgementModeStateTrigger(node);
        }

        private static void DrawSpecificKeyTrigger(OvAnimationNode node)
        {
            if (node.TriggerKeys == null) node.TriggerKeys = new List<string>();
            int mode = node.TriggerOnKeyRelease ? 1 : 0;
            if (ImGui.Combo("按键动作", ref mode, "按下\0松开\0"))
            {
                node.TriggerOnKeyRelease = mode == 1;
                MarkGraphChanged();
            }

            ImGui.Text("已绑定按键");
            if (node.TriggerKeys.Count == 0) ImGui.TextDisabled("尚未绑定");
            for (int i = 0; i < node.TriggerKeys.Count; i++)
            {
                string key = node.TriggerKeys[i] ?? string.Empty;
                ImGui.Text(KeyDisplayNames.GetKeySymbol(key) + "  (" + key + ")");
                ImGui.SameLine();
                if (ImGui.SmallButton("移除##trigger_key_" + i))
                {
                    node.TriggerKeys.RemoveAt(i);
                    MarkGraphChanged();
                    break;
                }
            }

            bool capturing = string.Equals(_keyCaptureNodeId, node.Id, StringComparison.Ordinal);
            if (ImGui.Button(capturing ? "等待按键..." : "添加按键"))
            {
                _keyCaptureNodeId = node.Id;
                _keyCaptureStartFrame = Time.frameCount;
                capturing = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("清空##trigger_keys"))
            {
                node.TriggerKeys.Clear();
                StopKeyCapture();
                MarkGraphChanged();
            }
            if (!capturing || Time.frameCount <= _keyCaptureStartFrame) return;

            ImGui.TextColored(new Vector4(1f, 0.85f, 0.35f, 1f), "按下键盘按键，Esc 取消");
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                StopKeyCapture();
                return;
            }
            if (!TryGetPressedKeyboardKey(out KeyCode pressedKey)) return;
            string keyName = pressedKey.ToString();
            if (!node.TriggerKeys.Contains(keyName)) node.TriggerKeys.Add(keyName);
            StopKeyCapture();
            MarkGraphChanged();
        }

        private static void DrawJudgementTrigger(OvAnimationNode node)
        {
            if (ImGui.Button("全选##trigger_judgements"))
            {
                node.TriggerJudgementMask = (1 << JudgementLabels.Length) - 1;
                MarkGraphChanged();
            }
            ImGui.SameLine();
            if (ImGui.Button("清空##trigger_judgements"))
            {
                node.TriggerJudgementMask = 0;
                MarkGraphChanged();
            }
            for (int i = 0; i < JudgementLabels.Length; i++)
            {
                bool selected = (node.TriggerJudgementMask & (1 << i)) != 0;
                if (ImGui.Checkbox(JudgementLabels[i] + "##judgement_" + i, ref selected))
                {
                    if (selected) node.TriggerJudgementMask |= 1 << i;
                    else node.TriggerJudgementMask &= ~(1 << i);
                    MarkGraphChanged();
                }
            }
        }

        private static void DrawComboBreakTrigger(OvAnimationNode node)
        {
            int counter = (int)node.TriggerComboCounter;
            if (ImGui.Combo("Combo 类型", ref counter, "Pure Combo\0Perfect Combo\0"))
            {
                node.TriggerComboCounter = (OvComboCounterKind)counter;
                MarkGraphChanged();
            }
        }

        private static void DrawBeatTrigger(OvAnimationNode node)
        {
            int interval = Math.Max(1, node.TriggerBeatInterval);
            if (ImGui.DragInt("每 N 拍", ref interval, 0.1f, 1, 1024))
            {
                node.TriggerBeatInterval = Math.Max(1, interval);
                MarkGraphChanged();
            }
            int offset = node.TriggerBeatOffset;
            if (ImGui.DragInt("节拍偏移", ref offset, 0.1f, -1024, 1024))
            {
                node.TriggerBeatOffset = offset;
                MarkGraphChanged();
            }
        }

        private static void DrawTagChangeTrigger(OvAnimationNode node)
        {
            DrawTriggerTagSelector(node, "change");
            ImGui.TextDisabled("首次读取只建立基准，之后值变化时触发");
        }

        private static void DrawTagNumericConditionTrigger(OvAnimationNode node)
        {
            DrawTriggerTagSelector(node, "numeric");
            OvNumericUnitKind numericUnit = OvNumericTagUnits.GetUnit(node.TriggerTag);
            ImGui.TextDisabled("Tag 数值类型：" + OvNumericTagUnits.GetDisplayName(numericUnit));
            string unitSuffix = OvNumericTagUnits.GetLabelSuffix(numericUnit);
            string valueFormat = OvNumericTagUnits.GetDragFormat(numericUnit);

            int compare = (int)node.TriggerNumericCompare;
            if (ImGui.Combo("数值条件", ref compare,
                "大于 (>)\0大于等于 (>=)\0小于 (<)\0小于等于 (<=)\0等于 (=)\0不等于 (!=)\0位于区间\0位于区间外\0"))
            {
                node.TriggerNumericCompare = (OvTagNumericCompareKind)compare;
                MarkGraphChanged();
            }

            bool range = node.TriggerNumericCompare == OvTagNumericCompareKind.InRange
                || node.TriggerNumericCompare == OvTagNumericCompareKind.OutsideRange;
            if (range)
            {
                float min = node.TriggerNumericRangeMin;
                float max = node.TriggerNumericRangeMax;
                if (ImGui.DragFloat("区间最小值" + unitSuffix, ref min, 0.1f, float.MinValue, float.MaxValue, valueFormat))
                {
                    node.TriggerNumericRangeMin = min;
                    MarkGraphChanged();
                }
                if (ImGui.DragFloat("区间最大值" + unitSuffix, ref max, 0.1f, float.MinValue, float.MaxValue, valueFormat))
                {
                    node.TriggerNumericRangeMax = max;
                    MarkGraphChanged();
                }
                bool includeMin = node.TriggerNumericIncludeMin;
                bool includeMax = node.TriggerNumericIncludeMax;
                if (ImGui.Checkbox("包含最小值", ref includeMin)) { node.TriggerNumericIncludeMin = includeMin; MarkGraphChanged(); }
                ImGui.SameLine();
                if (ImGui.Checkbox("包含最大值", ref includeMax)) { node.TriggerNumericIncludeMax = includeMax; MarkGraphChanged(); }
            }
            else
            {
                float target = node.TriggerNumericValue;
                if (ImGui.DragFloat("比较数值" + unitSuffix, ref target, 0.1f, float.MinValue, float.MaxValue, valueFormat))
                {
                    node.TriggerNumericValue = target;
                    MarkGraphChanged();
                }
                if (node.TriggerNumericCompare == OvTagNumericCompareKind.Equal
                    || node.TriggerNumericCompare == OvTagNumericCompareKind.NotEqual)
                {
                    float epsilon = Math.Max(0f, node.TriggerNumericEpsilon);
                    if (ImGui.DragFloat("比较误差" + unitSuffix, ref epsilon, 0.0001f, 0f, 1000000f, valueFormat))
                    {
                        node.TriggerNumericEpsilon = Math.Max(0f, epsilon);
                        MarkGraphChanged();
                    }
                }
            }

            ImGui.TextDisabled("首次读取时若条件成立会触发；之后仅在从不成立变为成立时触发");
        }

        private static void DrawTriggerTagSelector(OvAnimationNode node, string idSuffix)
        {
            string tag = node.TriggerTag ?? string.Empty;
            if (ImGui.InputText("监听 Tag##" + idSuffix, ref tag, 128u))
            {
                node.TriggerTag = tag;
                MarkGraphChanged();
            }
            if (!IsImageMode && ImGui.BeginCombo("从当前文本选择##" + idSuffix, string.IsNullOrEmpty(tag) ? "选择动态 Tag" : tag))
            {
                for (int i = 0; i < _text.TokenBindings.Count; i++)
                {
                    OvTextTokenBinding token = _text.TokenBindings[i];
                    if (token == null || token.Kind != OvTextTokenKind.DynamicTag || string.IsNullOrEmpty(token.Lexeme)) continue;
                    if (ImGui.Selectable(token.Lexeme + "##trigger_tag_" + idSuffix + "_" + i,
                        string.Equals(tag, token.Lexeme, StringComparison.Ordinal)))
                    {
                        node.TriggerTag = token.Lexeme;
                        MarkGraphChanged();
                    }
                }
                ImGui.EndCombo();
            }
            if (IsImageMode) ImGui.TextDisabled("图片没有文本 Token，请直接填写要监听的 Tag。");
        }

        private static void DrawTweenInspector(OvAnimationNode node)
        {
            int property = (int)node.TweenProperty;
            if (ImGui.Combo("动画属性", ref property, "位移\0缩放\0旋转\0透明度\0"))
            {
                node.TweenProperty = (OvTokenTweenProperty)property;
                ApplyTweenDefaults(node);
                MarkGraphChanged();
            }

            if (node.TweenProperty == OvTokenTweenProperty.Position)
            {
                Vector2 from = new Vector2(node.FromX, node.FromY);
                Vector2 to = new Vector2(node.ToX, node.ToY);
                if (ImGui.DragFloat2("起始位置", ref from, 0.5f)) { node.FromX = from.X; node.FromY = from.Y; MarkGraphChanged(); }
                if (ImGui.DragFloat2("结束位置", ref to, 0.5f)) { node.ToX = to.X; node.ToY = to.Y; MarkGraphChanged(); }
            }
            else if (node.TweenProperty == OvTokenTweenProperty.Scale)
            {
                Vector2 from = new Vector2(node.FromX, node.FromY);
                Vector2 to = new Vector2(node.ToX, node.ToY);
                if (ImGui.DragFloat2("起始缩放", ref from, 0.01f, 0.01f, 10f)) { node.FromX = from.X; node.FromY = from.Y; MarkGraphChanged(); }
                if (ImGui.DragFloat2("结束缩放", ref to, 0.01f, 0.01f, 10f)) { node.ToX = to.X; node.ToY = to.Y; MarkGraphChanged(); }
            }
            else
            {
                float from = node.FromX;
                float to = node.ToX;
                if (ImGui.DragFloat("起始值", ref from, node.TweenProperty == OvTokenTweenProperty.Opacity ? 0.01f : 1f)) { node.FromX = from; MarkGraphChanged(); }
                if (ImGui.DragFloat("结束值", ref to, node.TweenProperty == OvTokenTweenProperty.Opacity ? 0.01f : 1f)) { node.ToX = to; MarkGraphChanged(); }
            }

            float duration = node.Duration;
            if (ImGui.DragFloat("持续时间", ref duration, 0.01f, 0.01f, 30f, "%.2f 秒")) { node.Duration = Math.Max(0.01f, duration); MarkGraphChanged(); }
            bool asGroup = node.TreatSelectedTokensAsGroup;
            if (!IsImageMode && ImGui.Checkbox("将所选 Token 作为一个整体", ref asGroup))
            {
                node.TreatSelectedTokensAsGroup = asGroup;
                MarkGraphChanged();
            }
            if (IsImageMode)
            {
                node.TreatSelectedTokensAsGroup = false;
                node.StaggerDelay = 0f;
                node.ReverseOrder = false;
            }
            else if (node.TreatSelectedTokensAsGroup)
            {
                ImGui.TextDisabled("整体模式使用共同中心，所有 Token 同步播放。可跨行和非连续选择。");
            }
            else
            {
                float stagger = node.StaggerDelay;
                if (ImGui.DragFloat("Token 交错延迟", ref stagger, 0.005f, 0f, 5f, "%.3f 秒")) { node.StaggerDelay = Math.Max(0f, stagger); MarkGraphChanged(); }
                bool reverse = node.ReverseOrder;
                if (ImGui.Checkbox("反转 Token 顺序", ref reverse)) { node.ReverseOrder = reverse; MarkGraphChanged(); }
            }

            if (ImGui.Button("断开输入连接"))
            {
                CurrentGraph.Links.RemoveAll(link => link != null && link.ToNodeId == node.Id);
                MarkGraphChanged();
            }

            ImGui.Text("缓动类型");
            string selectedEasing = string.IsNullOrWhiteSpace(node.Easing) ? "linear" : node.Easing;
            string easingPopupId = "ov_tween_easing_popup_" + node.Id;
            if (ImGui.Button(selectedEasing + "##ov_tween_easing_button_" + node.Id, new Vector2(-1f, 0f)))
            {
                ImGui.OpenPopup(easingPopupId);
            }
            if (CheryToolsMenu.DrawEasingSelectorPopup(easingPopupId, ref selectedEasing))
            {
                node.Easing = selectedEasing;
                MarkGraphChanged();
            }
        }

        private static void DrawModifyInspector(OvAnimationNode node)
        {
            string content = node.ModifyText ?? string.Empty;
            if (ImGui.InputTextMultiline("替换内容", ref content, 4096, new Vector2(-1f, 120f)))
            {
                node.ModifyText = content;
                MarkGraphChanged();
            }
            if (ImGui.Button("断开输入连接##modify"))
            {
                CurrentGraph.Links.RemoveAll(link => link != null && link.ToNodeId == node.Id);
                MarkGraphChanged();
            }
        }

        private static void DrawEffectInspector(OvAnimationNode node)
        {
            ImGui.TextWrapped("持续读取数值，并把颜色映射到所选 Token。字颜色、描边颜色和阴影颜色是三个独立通道。");

            int source = (int)node.EffectValueSource;
            if (ImGui.Combo("数值来源", ref source, "Tag\0常数\0"))
            {
                node.EffectValueSource = (OvEffectValueSourceKind)source;
                MarkGraphChanged();
            }
            OvNumericUnitKind valueUnit;
            if (node.EffectValueSource == OvEffectValueSourceKind.Tag)
            {
                string tag = node.EffectSourceTag ?? string.Empty;
                if (ImGui.InputText("Tag", ref tag, 256u))
                {
                    node.EffectSourceTag = tag;
                    MarkGraphChanged();
                }
                if (ImGui.BeginCombo("从当前文本选择##effect_tag", string.IsNullOrEmpty(tag) ? "选择动态 Tag" : tag))
                {
                    for (int i = 0; i < _text.TokenBindings.Count; i++)
                    {
                        OvTextTokenBinding token = _text.TokenBindings[i];
                        if (token == null || token.Kind != OvTextTokenKind.DynamicTag || string.IsNullOrEmpty(token.Lexeme)) continue;
                        if (ImGui.Selectable(token.Lexeme + "##effect_tag_" + i, string.Equals(tag, token.Lexeme, StringComparison.Ordinal)))
                        {
                            node.EffectSourceTag = token.Lexeme;
                            MarkGraphChanged();
                        }
                    }
                    ImGui.EndCombo();
                }
                valueUnit = OvNumericTagUnits.GetUnit(node.EffectSourceTag);
                ImGui.TextDisabled("Tag 数值类型：" + OvNumericTagUnits.GetDisplayName(valueUnit));
                if (valueUnit == OvNumericUnitKind.Percentage)
                    ImGui.TextDisabled("Tag 与所有颜色点统一使用 0-100 的百分数数值");
            }
            else
            {
                int constantType = node.EffectValueInterpretation == OvEffectValueInterpretation.Percentage ? 1 : 0;
                if (ImGui.Combo("常数类型", ref constantType, "普通数值\0百分数（0-100）\0"))
                {
                    node.EffectValueInterpretation = constantType == 1
                        ? OvEffectValueInterpretation.Percentage
                        : OvEffectValueInterpretation.Number;
                    MarkGraphChanged();
                }
                valueUnit = constantType == 1 ? OvNumericUnitKind.Percentage : OvNumericUnitKind.Number;
                float constant = node.EffectConstantValue;
                if (ImGui.DragFloat("常数值" + OvNumericTagUnits.GetLabelSuffix(valueUnit), ref constant, 0.1f,
                    float.MinValue, float.MaxValue, OvNumericTagUnits.GetDragFormat(valueUnit)))
                {
                    node.EffectConstantValue = constant;
                    MarkGraphChanged();
                }
            }
            int mode = (int)node.ColorMapMode;
            if (ImGui.Combo("映射模式", ref mode, "线性渐变\0阶梯切换\0"))
            {
                node.ColorMapMode = (OvColorMapMode)mode;
                MarkGraphChanged();
            }

            ImGui.SeparatorText("作用通道");
            bool textColor = node.EffectTextColor;
            bool outlineColor = node.EffectOutlineColor;
            bool shadowColor = node.EffectShadowColor;
            bool channelChanged = false;
            if (ImGui.Checkbox("字颜色", ref textColor)) channelChanged = true;
            ImGui.SameLine();
            if (ImGui.Checkbox("描边颜色", ref outlineColor)) channelChanged = true;
            ImGui.SameLine();
            if (ImGui.Checkbox("阴影颜色", ref shadowColor)) channelChanged = true;
            if (channelChanged)
            {
                if (!textColor && !outlineColor && !shadowColor) textColor = true;
                node.EffectTextColor = textColor;
                node.EffectOutlineColor = outlineColor;
                node.EffectShadowColor = shadowColor;
                MarkGraphChanged();
            }

            if (node.ColorPoints == null) node.ColorPoints = new List<OvColorPoint>();
            ImGui.SeparatorText("颜色点");
            int removeIndex = -1;
            for (int i = 0; i < node.ColorPoints.Count; i++)
            {
                OvColorPoint point = node.ColorPoints[i];
                if (point == null) continue;
                ImGui.PushID("effect_point_" + i);
                ImGui.Text("颜色点 " + (i + 1));
                ImGui.SameLine();
                if (node.ColorPoints.Count > 1 && ImGui.SmallButton("删除")) removeIndex = i;
                float value = point.Value;
                if (ImGui.DragFloat("数值" + OvNumericTagUnits.GetLabelSuffix(valueUnit), ref value, 0.1f,
                    float.MinValue, float.MaxValue, OvNumericTagUnits.GetDragFormat(valueUnit)))
                {
                    point.Value = value;
                    MarkGraphChanged();
                }
                if (node.EffectTextColor && EditColorArray("字颜色", ref point.TextColor, new Vector4(1f, 1f, 1f, 1f))) MarkGraphChanged();
                if (node.EffectOutlineColor && EditColorArray("描边颜色", ref point.OutlineColor, new Vector4(0f, 0f, 0f, 1f))) MarkGraphChanged();
                if (node.EffectShadowColor && EditColorArray("阴影颜色", ref point.ShadowColor, new Vector4(0f, 0f, 0f, 1f))) MarkGraphChanged();
                ImGui.Separator();
                ImGui.PopID();
            }
            if (removeIndex >= 0)
            {
                node.ColorPoints.RemoveAt(removeIndex);
                MarkGraphChanged();
            }
            if (ImGui.Button("添加颜色点"))
            {
                OvColorPoint point = node.ColorPoints.Count > 0
                    ? OvColorPoint.Clone(node.ColorPoints[node.ColorPoints.Count - 1])
                    : new OvColorPoint();
                point.Value = node.ColorPoints.Count > 0 ? node.ColorPoints[node.ColorPoints.Count - 1].Value + 10f : 0f;
                node.ColorPoints.Add(point);
                MarkGraphChanged();
            }
            ImGui.SameLine();
            if (ImGui.Button("按数值排序"))
            {
                node.ColorPoints.Sort((left, right) => left.Value.CompareTo(right.Value));
                MarkGraphChanged();
            }

            if (ImGui.Button("断开 Token 连接##effect"))
            {
                CurrentGraph.Links.RemoveAll(link => link != null
                    && link.ToNodeId == node.Id && link.ToPort == "targets");
                MarkGraphChanged();
            }
        }

        private static void DrawNumberFormatInspector(OvAnimationNode node)
        {
            ImGui.TextWrapped("持续改变动态 Tag 的显示格式，不修改原始数值。颜色效果和后续数值计算仍读取格式化前的值。");

            int kind = (int)node.NumberFormatKind;
            if (ImGui.Combo("显示格式", ref kind, "普通数值\0百分比\0固定小数\0整数补零\0"))
            {
                node.NumberFormatKind = (OvNumberFormatKind)kind;
                MarkGraphChanged();
            }

            if (node.NumberFormatKind == OvNumberFormatKind.Percentage)
            {
                int input = (int)node.PercentageInputKind;
                if (ImGui.Combo("百分比输入", ref input, "自动判断\00-1 比例\00-100 百分数\0"))
                {
                    node.PercentageInputKind = (OvPercentageInputKind)input;
                    MarkGraphChanged();
                }
                int decimals = Math.Max(0, Math.Min(8, node.NumberFormatDecimals));
                if (ImGui.DragInt("小数位", ref decimals, 0.1f, 0, 8))
                {
                    node.NumberFormatDecimals = Math.Max(0, Math.Min(8, decimals));
                    MarkGraphChanged();
                }
                ImGui.TextDisabled("比例 1 → 100%，百分数 1 → 1%");
            }
            else if (node.NumberFormatKind == OvNumberFormatKind.FixedDecimals)
            {
                int decimals = Math.Max(0, Math.Min(8, node.NumberFormatDecimals));
                if (ImGui.DragInt("小数位", ref decimals, 0.1f, 0, 8))
                {
                    node.NumberFormatDecimals = Math.Max(0, Math.Min(8, decimals));
                    MarkGraphChanged();
                }
                ImGui.TextDisabled("示例：1，小数位 2 → 1.00");
            }
            else if (node.NumberFormatKind == OvNumberFormatKind.ZeroPaddedInteger)
            {
                int width = Math.Max(1, Math.Min(16, node.NumberFormatWidth));
                if (ImGui.DragInt("数字位数", ref width, 0.1f, 1, 16))
                {
                    node.NumberFormatWidth = Math.Max(1, Math.Min(16, width));
                    MarkGraphChanged();
                }
                ImGui.TextDisabled("示例：1，数字位数 4 → 0001");
            }
            else
            {
                ImGui.TextDisabled("移除无意义的尾随零，例如 1.00 → 1");
            }

            ImGui.Spacing();
            ImGui.TextDisabled("仅动态 Tag Token 会被格式化；普通文字保持不变。多个格式节点指向同一 Token 时，后创建的节点生效。");
            if (ImGui.Button("断开 Token 连接##number_format"))
            {
                CurrentGraph.Links.RemoveAll(link => link != null
                    && link.ToNodeId == node.Id && link.ToPort == "targets");
                MarkGraphChanged();
            }
        }

        private static bool EditColorArray(string label, ref float[] color, Vector4 fallback)
        {
            if (color == null || color.Length < 4)
            {
                color = new float[] { fallback.X, fallback.Y, fallback.Z, fallback.W };
            }
            Vector4 value = new Vector4(color[0], color[1], color[2], color[3]);
            if (!ImGui.ColorEdit4(label, ref value, ImGuiColorEditFlags.AlphaBar)) return false;
            color[0] = value.X;
            color[1] = value.Y;
            color[2] = value.Z;
            color[3] = value.W;
            return true;
        }

        private static void DrawToggleStateTrigger(OvAnimationNode node)
        {
            int enabled = node.TriggerStateEnabled ? 1 : 0;
            if (ImGui.Combo("触发状态", ref enabled, "关闭\0启用\0"))
            {
                node.TriggerStateEnabled = enabled == 1;
                MarkGraphChanged();
            }
        }

        private static void DrawJudgementModeStateTrigger(OvAnimationNode node)
        {
            int mode = (int)node.TriggerJudgementMode;
            if (ImGui.Combo("触发状态", ref mode, "宽松\0标准\0严格\0"))
            {
                node.TriggerJudgementMode = (OvJudgementMode)mode;
                MarkGraphChanged();
            }
        }

        private static void AddNode(OvAnimationNodeKind kind, Vector2 position)
        {
            var node = new OvAnimationNode { Id = OvAnimationGraph.NewId(), Kind = kind, EditorX = position.X, EditorY = position.Y };
            if (kind == OvAnimationNodeKind.TokenInput)
            {
                node.Name = IsImageMode ? "Image Input" : "Token Input";
                if (IsImageMode) node.SelectedTokenIds.Add(OvImageNodeAnimation.TargetId);
            }
            CurrentGraph.Nodes.Add(node);
            SelectOnly(node.Id);
            MarkGraphChanged();
        }

        private static void AddTrigger(OvAnimationTriggerKind trigger, Vector2 position)
        {
            var node = new OvAnimationNode
            {
                Id = OvAnimationGraph.NewId(), Kind = OvAnimationNodeKind.Trigger, Trigger = trigger,
                Name = trigger.ToString(), EditorX = position.X, EditorY = position.Y
            };
            CurrentGraph.Nodes.Add(node);
            SelectOnly(node.Id);
            MarkGraphChanged();
        }

        private static void AddTween(OvTokenTweenProperty property, Vector2 position)
        {
            var node = new OvAnimationNode
            {
                Id = OvAnimationGraph.NewId(), Kind = OvAnimationNodeKind.Tween, TweenProperty = property,
                EditorX = position.X, EditorY = position.Y, Duration = 0.25f, Easing = "ease-out-quad"
            };
            ApplyTweenDefaults(node);
            CurrentGraph.Nodes.Add(node);
            SelectOnly(node.Id);
            MarkGraphChanged();
        }

        private static void AddModify(Vector2 position)
        {
            var node = new OvAnimationNode
            {
                Id = OvAnimationGraph.NewId(),
                Kind = OvAnimationNodeKind.Modify,
                Name = "Modify Token Content",
                EditorX = position.X,
                EditorY = position.Y,
                ModifyText = string.Empty
            };
            CurrentGraph.Nodes.Add(node);
            SelectOnly(node.Id);
            MarkGraphChanged();
        }

        private static void AddColorEffect(Vector2 position)
        {
            var node = new OvAnimationNode
            {
                Id = OvAnimationGraph.NewId(),
                Kind = OvAnimationNodeKind.Effect,
                Name = "Color Map",
                EffectKind = OvTokenEffectKind.ColorMap,
                EffectValueSource = OvEffectValueSourceKind.Tag,
                EffectSourceTag = "{progress}",
                EffectValueInterpretation = OvEffectValueInterpretation.Auto,
                ColorMapMode = OvColorMapMode.Gradient,
                EffectTextColor = true,
                EditorX = position.X,
                EditorY = position.Y
            };
            node.ColorPoints.Add(new OvColorPoint
            {
                Value = 0f,
                TextColor = new float[] { 1f, 0.2f, 0.2f, 1f },
                OutlineColor = new float[] { 0f, 0f, 0f, 1f },
                ShadowColor = new float[] { 0f, 0f, 0f, 0.75f }
            });
            node.ColorPoints.Add(new OvColorPoint
            {
                Value = 100f,
                TextColor = new float[] { 0.2f, 1f, 0.35f, 1f },
                OutlineColor = new float[] { 0f, 0f, 0f, 1f },
                ShadowColor = new float[] { 0f, 0f, 0f, 0.75f }
            });
            CurrentGraph.Nodes.Add(node);
            SelectOnly(node.Id);
            MarkGraphChanged();
        }

        private static void AddNumberFormat(Vector2 position)
        {
            var node = new OvAnimationNode
            {
                Id = OvAnimationGraph.NewId(),
                Kind = OvAnimationNodeKind.NumberFormat,
                Name = "Number Format",
                NumberFormatKind = OvNumberFormatKind.Plain,
                PercentageInputKind = OvPercentageInputKind.Auto,
                NumberFormatDecimals = 0,
                NumberFormatWidth = 4,
                EditorX = position.X,
                EditorY = position.Y
            };
            CurrentGraph.Nodes.Add(node);
            SelectOnly(node.Id);
            MarkGraphChanged();
        }

        private static void AddGroupFrame(Vector2 position)
        {
            var node = new OvAnimationNode
            {
                Id = OvAnimationGraph.NewId(),
                Kind = OvAnimationNodeKind.GroupFrame,
                Name = "新分组",
                EditorX = position.X,
                EditorY = position.Y,
                EditorWidth = 420f,
                EditorHeight = 260f
            };
            CurrentGraph.Nodes.Add(node);
            SelectOnly(node.Id);
            MarkGraphChanged();
        }

        private static void ApplyTweenDefaults(OvAnimationNode node)
        {
            node.FromX = 0f;
            node.FromY = 0f;
            node.ToX = 0f;
            node.ToY = 0f;
            if (node.TweenProperty == OvTokenTweenProperty.Position) node.ToY = -24f;
            else if (node.TweenProperty == OvTokenTweenProperty.Scale) { node.FromX = node.FromY = 1f; node.ToX = node.ToY = 1.25f; }
            else if (node.TweenProperty == OvTokenTweenProperty.Rotation) node.ToX = 15f;
            else if (node.TweenProperty == OvTokenTweenProperty.Opacity) { node.FromX = 1f; node.ToX = 0f; }
        }

        private static void AddLink(string fromNode, string fromPort, string toNode, string toPort)
        {
            OvAnimationGraph graph = CurrentGraph;
            for (int i = graph.Links.Count - 1; i >= 0; i--)
            {
                OvAnimationLink link = graph.Links[i];
                if (link != null && link.ToNodeId == toNode && link.ToPort == toPort) graph.Links.RemoveAt(i);
            }
            graph.Links.Add(OvAnimationGraph.NewLink(fromNode, fromPort, toNode, toPort));
            MarkGraphChanged();
        }

        private static void DeleteSelectedNodes()
        {
            OvAnimationGraph graph = CurrentGraph;
            if (graph == null || SelectedNodeIds.Count == 0) return;
            var deleting = new HashSet<string>(SelectedNodeIds);
            graph.Nodes.RemoveAll(node => node != null && deleting.Contains(node.Id));
            graph.Links.RemoveAll(link => link != null
                && (deleting.Contains(link.FromNodeId) || deleting.Contains(link.ToNodeId)));
            if (deleting.Contains(_keyCaptureNodeId)) StopKeyCapture();
            ClearSelection();
            MarkGraphChanged();
        }

        private static OvAnimationNode FindNode(string id)
        {
            if (CurrentGraph?.Nodes == null) return null;
            for (int i = 0; i < CurrentGraph.Nodes.Count; i++)
            {
                OvAnimationNode node = CurrentGraph.Nodes[i];
                if (node != null && node.Id == id) return node;
            }
            return null;
        }

        private static NodeGeometry GetGeometry(OvAnimationNode node, Vector2 canvasMin)
        {
            Vector2 size = node.Kind == OvAnimationNodeKind.GroupFrame
                ? new Vector2(Math.Max(180f, node.EditorWidth), Math.Max(120f, node.EditorHeight))
                : node.Kind == OvAnimationNodeKind.Tween || node.Kind == OvAnimationNodeKind.Modify
                    || node.Kind == OvAnimationNodeKind.Effect || node.Kind == OvAnimationNodeKind.NumberFormat
                    ? new Vector2(230f, string.IsNullOrWhiteSpace(node.EditorNote) ? 118f : 142f)
                    : new Vector2(210f, string.IsNullOrWhiteSpace(node.EditorNote) ? 96f : 126f);
            Vector2 min = canvasMin + _pan + new Vector2(node.EditorX, node.EditorY) * _zoom;
            Vector2 max = min + size * _zoom;
            return new NodeGeometry
            {
                Min = min,
                Max = max,
                FlowIn = new Vector2(min.X, min.Y + 48f * _zoom),
                TargetIn = new Vector2(min.X, min.Y + 82f * _zoom),
                FlowOut = new Vector2(max.X, min.Y + 48f * _zoom),
                TargetOut = new Vector2(max.X, min.Y + 68f * _zoom)
            };
        }

        private static string NodeTitle(OvAnimationNode node)
        {
            if (node.Kind == OvAnimationNodeKind.GroupFrame) return string.IsNullOrWhiteSpace(node.Name) ? "分组" : node.Name;
            if (node.Kind == OvAnimationNodeKind.TokenInput) return IsImageMode ? "图片输入" : "Token 输入";
            if (node.Kind == OvAnimationNodeKind.Trigger) return "事件：" + TriggerLabel(node.Trigger);
            if (node.Kind == OvAnimationNodeKind.Modify) return "修改：Token 内容";
            if (node.Kind == OvAnimationNodeKind.NumberFormat) return "修改：数值格式";
            if (node.Kind == OvAnimationNodeKind.Effect) return "效果：颜色映射";
            return "动画：" + TweenLabel(node.TweenProperty);
        }

        private static string NodeSummary(OvAnimationNode node)
        {
            if (node.Kind == OvAnimationNodeKind.TokenInput) return IsImageMode
                ? "当前图片"
                : "已选择 " + (node.SelectedTokenIds?.Count ?? 0) + " 个 Token";
            if (node.Kind == OvAnimationNodeKind.Trigger) return TriggerSummary(node);
            if (node.Kind == OvAnimationNodeKind.Modify) return SummarizeModifyText(node.ModifyText);
            if (node.Kind == OvAnimationNodeKind.NumberFormat) return NumberFormatSummary(node);
            if (node.Kind == OvAnimationNodeKind.Effect) return EffectSummary(node);
            return node.Duration.ToString("0.00") + " 秒  " + (node.Easing ?? "linear")
                + (node.TreatSelectedTokensAsGroup ? " · 整体" : string.Empty);
        }

        private static string EffectSummary(OvAnimationNode node)
        {
            string channels = string.Empty;
            if (node.EffectTextColor) channels += "字";
            if (node.EffectOutlineColor) channels += "描";
            if (node.EffectShadowColor) channels += "影";
            if (channels.Length == 0) channels = "无通道";
            return (node.ColorMapMode == OvColorMapMode.Gradient ? "渐变" : "阶梯")
                + " · " + channels + " · " + (node.ColorPoints?.Count ?? 0) + " 点";
        }

        private static string NumberFormatSummary(OvAnimationNode node)
        {
            switch (node.NumberFormatKind)
            {
                case OvNumberFormatKind.Percentage:
                    return "百分比 · " + Math.Max(0, Math.Min(8, node.NumberFormatDecimals)) + " 位小数";
                case OvNumberFormatKind.FixedDecimals:
                    return "固定 " + Math.Max(0, Math.Min(8, node.NumberFormatDecimals)) + " 位小数";
                case OvNumberFormatKind.ZeroPaddedInteger:
                    return "整数补零 · " + Math.Max(1, Math.Min(16, node.NumberFormatWidth)) + " 位";
                default:
                    return "普通数值";
            }
        }

        private static string SummarizeModifyText(string value)
        {
            if (string.IsNullOrEmpty(value)) return "替换为空内容";
            string singleLine = value.Replace('\r', ' ').Replace('\n', ' ');
            return singleLine.Length <= 18 ? singleLine : singleLine.Substring(0, 18) + "...";
        }

        private static string TriggerLabel(OvAnimationTriggerKind trigger)
        {
            switch (trigger)
            {
                case OvAnimationTriggerKind.AnyKeyDown: return "任意按键按下";
                case OvAnimationTriggerKind.ComboIncrease: return "Combo 增加";
                case OvAnimationTriggerKind.SpecificKey: return "指定按键";
                case OvAnimationTriggerKind.JudgementOccurred: return "判定发生";
                case OvAnimationTriggerKind.ComboBreak: return "Combo 断连";
                case OvAnimationTriggerKind.Beat: return "节拍";
                case OvAnimationTriggerKind.LevelStart: return "关卡开始";
                case OvAnimationTriggerKind.LevelEnd: return "关卡结束";
                case OvAnimationTriggerKind.TagValueChanged: return "Tag 值变化";
                case OvAnimationTriggerKind.TagNumericCondition: return "Tag 数值条件";
                case OvAnimationTriggerKind.AutoplayState: return "自动播放状态";
                case OvAnimationTriggerKind.NoFailState: return "不会死亡状态";
                case OvAnimationTriggerKind.JudgementModeState: return "判定模式状态";
                default: return "手动预览";
            }
        }

        private static string TriggerSummary(OvAnimationNode node)
        {
            switch (node.Trigger)
            {
                case OvAnimationTriggerKind.SpecificKey:
                    return (node.TriggerOnKeyRelease ? "松开 " : "按下 ") + (node.TriggerKeys?.Count ?? 0) + " 个按键";
                case OvAnimationTriggerKind.JudgementOccurred:
                    return "已选择 " + CountBits(node.TriggerJudgementMask) + " 种判定";
                case OvAnimationTriggerKind.ComboBreak:
                    return node.TriggerComboCounter == OvComboCounterKind.Perfect ? "Perfect Combo" : "Pure Combo";
                case OvAnimationTriggerKind.Beat:
                    return "每 " + Math.Max(1, node.TriggerBeatInterval) + " 拍，偏移 " + node.TriggerBeatOffset;
                case OvAnimationTriggerKind.TagValueChanged:
                    return string.IsNullOrEmpty(node.TriggerTag) ? "未指定 Tag" : node.TriggerTag;
                case OvAnimationTriggerKind.TagNumericCondition:
                    return TagNumericConditionSummary(node);
                case OvAnimationTriggerKind.AutoplayState:
                case OvAnimationTriggerKind.NoFailState:
                    return node.TriggerStateEnabled ? "状态：启用" : "状态：关闭";
                case OvAnimationTriggerKind.JudgementModeState:
                    if (node.TriggerJudgementMode == OvJudgementMode.Lenient) return "状态：宽松";
                    if (node.TriggerJudgementMode == OvJudgementMode.Strict) return "状态：严格";
                    return "状态：标准";
                default:
                    return "流程输出";
            }
        }

        private static string TagNumericConditionSummary(OvAnimationNode node)
        {
            string tag = string.IsNullOrEmpty(node.TriggerTag) ? "未指定 Tag" : node.TriggerTag;
            OvNumericUnitKind unit = OvNumericTagUnits.GetUnit(node.TriggerTag);
            string target = OvNumericTagUnits.FormatValue(node.TriggerNumericValue, unit);
            switch (node.TriggerNumericCompare)
            {
                case OvTagNumericCompareKind.GreaterThan: return tag + " > " + target;
                case OvTagNumericCompareKind.GreaterOrEqual: return tag + " >= " + target;
                case OvTagNumericCompareKind.LessThan: return tag + " < " + target;
                case OvTagNumericCompareKind.LessOrEqual: return tag + " <= " + target;
                case OvTagNumericCompareKind.Equal: return tag + " = " + target;
                case OvTagNumericCompareKind.NotEqual: return tag + " != " + target;
                case OvTagNumericCompareKind.InRange:
                case OvTagNumericCompareKind.OutsideRange:
                    string min = OvNumericTagUnits.FormatValue(
                        Math.Min(node.TriggerNumericRangeMin, node.TriggerNumericRangeMax), unit);
                    string max = OvNumericTagUnits.FormatValue(
                        Math.Max(node.TriggerNumericRangeMin, node.TriggerNumericRangeMax), unit);
                    string left = node.TriggerNumericIncludeMin ? "[" : "(";
                    string right = node.TriggerNumericIncludeMax ? "]" : ")";
                    return tag + (node.TriggerNumericCompare == OvTagNumericCompareKind.InRange ? " in " : " out ")
                        + left + min + ", " + max + right;
                default:
                    return tag;
            }
        }

        private static int CountBits(int value)
        {
            int count = 0;
            uint bits = unchecked((uint)value);
            while (bits != 0)
            {
                count += (int)(bits & 1u);
                bits >>= 1;
            }
            return count;
        }

        private static string TweenLabel(OvTokenTweenProperty property)
        {
            if (property == OvTokenTweenProperty.Position) return "位移";
            if (property == OvTokenTweenProperty.Scale) return "缩放";
            if (property == OvTokenTweenProperty.Rotation) return "旋转";
            return "透明度";
        }

        private static void HandleEditorShortcuts()
        {
            if (!ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows)) return;
            ImGuiIOPtr io = ImGui.GetIO();
            if (io.WantTextInput || !string.IsNullOrEmpty(_keyCaptureNodeId)) return;

            if (IsCtrlDown() && Input.GetKeyDown(KeyCode.Z))
            {
                Undo();
                return;
            }
            if (IsShiftDown() && Input.GetKeyDown(KeyCode.D))
            {
                StartDuplicatePlacement();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Delete) && !_duplicatePlacementActive)
            {
                DeleteSelectedNodes();
            }
        }

        private static void ResetEditorState()
        {
            ClearSelection();
            _draggingNodeId = string.Empty;
            DraggingNodeIds.Clear();
            _resizingGroupId = string.Empty;
            _linkNodeId = string.Empty;
            _linkPort = string.Empty;
            _marqueeActive = false;
            MarqueeBaseSelection.Clear();
            _duplicatePlacementActive = false;
            DuplicateStartPositions.Clear();
            DuplicateNodeIds.Clear();
            DuplicateSourceSelection.Clear();
            UndoHistory.Clear();
            _historyBaseline = null;
            _historyTransactionActive = false;
            _historyPendingCommit = false;
            _suppressHistory = false;
        }

        private static bool UpdateSelectionForClick(string nodeId, bool ctrl)
        {
            if (string.IsNullOrEmpty(nodeId)) return false;
            if (ctrl)
            {
                if (SelectedNodeIds.Contains(nodeId))
                {
                    SelectedNodeIds.Remove(nodeId);
                    if (string.Equals(_selectedNodeId, nodeId, StringComparison.Ordinal))
                    {
                        _selectedNodeId = FirstSelectedNodeId();
                    }
                    return false;
                }
                SelectedNodeIds.Add(nodeId);
                _selectedNodeId = nodeId;
                return true;
            }

            if (!SelectedNodeIds.Contains(nodeId)) SelectOnly(nodeId);
            else _selectedNodeId = nodeId;
            return true;
        }

        private static void SelectOnly(string nodeId)
        {
            SelectedNodeIds.Clear();
            if (!string.IsNullOrEmpty(nodeId)) SelectedNodeIds.Add(nodeId);
            _selectedNodeId = nodeId ?? string.Empty;
        }

        private static void ClearSelection()
        {
            SelectedNodeIds.Clear();
            _selectedNodeId = string.Empty;
        }

        private static string FirstSelectedNodeId()
        {
            foreach (string id in SelectedNodeIds) return id;
            return string.Empty;
        }

        private static void BeginNodeDrag(string nodeId, OvAnimationGraph graph,
            Dictionary<string, NodeGeometry> geometry)
        {
            _draggingNodeId = nodeId;
            DraggingNodeIds.Clear();
            foreach (string selectedId in SelectedNodeIds) DraggingNodeIds.Add(selectedId);

            foreach (string selectedId in SelectedNodeIds)
            {
                OvAnimationNode selected = FindNode(selectedId);
                if (selected == null || selected.Kind != OvAnimationNodeKind.GroupFrame
                    || !geometry.TryGetValue(selectedId, out NodeGeometry frame)) continue;
                for (int i = 0; i < graph.Nodes.Count; i++)
                {
                    OvAnimationNode candidate = graph.Nodes[i];
                    if (candidate == null || candidate.Id == selectedId
                        || !geometry.TryGetValue(candidate.Id, out NodeGeometry candidateGeometry)) continue;
                    Vector2 center = (candidateGeometry.Min + candidateGeometry.Max) * 0.5f;
                    if (PointInRect(center, frame.Min, frame.Max)) DraggingNodeIds.Add(candidate.Id);
                }
            }
        }

        private static void UpdateMarqueeSelection(OvAnimationGraph graph,
            Dictionary<string, NodeGeometry> geometry)
        {
            Vector2 min = Vector2.Min(_marqueeStart, _marqueeEnd);
            Vector2 max = Vector2.Max(_marqueeStart, _marqueeEnd);
            SelectedNodeIds.Clear();
            foreach (string id in MarqueeBaseSelection) SelectedNodeIds.Add(id);

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                OvAnimationNode node = graph.Nodes[i];
                if (node == null || !geometry.TryGetValue(node.Id, out NodeGeometry g)) continue;
                if (RectsOverlap(min, max, g.Min, g.Max)) SelectedNodeIds.Add(node.Id);
            }
            if (!SelectedNodeIds.Contains(_selectedNodeId)) _selectedNodeId = FirstSelectedNodeId();
        }

        private static void DrawMarquee(ImDrawListPtr draw)
        {
            if (!_marqueeActive) return;
            Vector2 min = Vector2.Min(_marqueeStart, _marqueeEnd);
            Vector2 max = Vector2.Max(_marqueeStart, _marqueeEnd);
            draw.AddRectFilled(min, max, Color(0.22f, 0.60f, 0.82f, 0.12f));
            draw.AddRect(min, max, Color(0.30f, 0.72f, 0.96f, 0.9f), 0f, ImDrawFlags.None, 1.5f);
        }

        private static void StartDuplicatePlacement()
        {
            if (_duplicatePlacementActive || SelectedNodeIds.Count == 0 || CurrentGraph == null) return;
            OvAnimationGraph graph = CurrentGraph;
            var sourceIds = new HashSet<string>(SelectedNodeIds);
            var idMap = new Dictionary<string, string>();
            var newNodes = new List<OvAnimationNode>();
            DuplicateSourceSelection.Clear();
            foreach (string id in sourceIds) DuplicateSourceSelection.Add(id);

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                OvAnimationNode source = graph.Nodes[i];
                if (source == null || !sourceIds.Contains(source.Id)) continue;
                OvAnimationNode clone = OvAnimationGraph.CloneNode(source);
                clone.Id = OvAnimationGraph.NewId();
                idMap[source.Id] = clone.Id;
                newNodes.Add(clone);
            }
            if (newNodes.Count == 0) return;

            var newLinks = new List<OvAnimationLink>();
            for (int i = 0; i < graph.Links.Count; i++)
            {
                OvAnimationLink source = graph.Links[i];
                if (source == null || !idMap.TryGetValue(source.FromNodeId, out string from)
                    || !idMap.TryGetValue(source.ToNodeId, out string to)) continue;
                newLinks.Add(OvAnimationGraph.NewLink(from, source.FromPort, to, source.ToPort));
            }

            graph.Nodes.AddRange(newNodes);
            graph.Links.AddRange(newLinks);
            DuplicateNodeIds.Clear();
            DuplicateStartPositions.Clear();
            ClearSelection();
            for (int i = 0; i < newNodes.Count; i++)
            {
                OvAnimationNode node = newNodes[i];
                DuplicateNodeIds.Add(node.Id);
                DuplicateStartPositions[node.Id] = new Vector2(node.EditorX, node.EditorY);
                SelectedNodeIds.Add(node.Id);
                _selectedNodeId = node.Id;
            }
            _duplicateMouseStartGraph = ScreenToGraph(ImGui.GetMousePos());
            _duplicatePlacementActive = true;
            MarkGraphChanged();
        }

        private static void UpdateDuplicatePlacement(Vector2 mousePosition)
        {
            if (!_duplicatePlacementActive) return;
            Vector2 delta = ScreenToGraph(mousePosition) - _duplicateMouseStartGraph;
            foreach (KeyValuePair<string, Vector2> pair in DuplicateStartPositions)
            {
                OvAnimationNode node = FindNode(pair.Key);
                if (node == null) continue;
                node.EditorX = pair.Value.X + delta.X;
                node.EditorY = pair.Value.Y + delta.Y;
            }
        }

        private static void FinishDuplicatePlacement()
        {
            _duplicatePlacementActive = false;
            DuplicateStartPositions.Clear();
            DuplicateNodeIds.Clear();
            DuplicateSourceSelection.Clear();
            MarkGraphChanged();
        }

        private static void CancelDuplicatePlacement()
        {
            if (!_duplicatePlacementActive || CurrentGraph == null) return;
            OvAnimationGraph graph = CurrentGraph;
            graph.Nodes.RemoveAll(node => node != null && DuplicateNodeIds.Contains(node.Id));
            graph.Links.RemoveAll(link => link != null
                && (DuplicateNodeIds.Contains(link.FromNodeId) || DuplicateNodeIds.Contains(link.ToNodeId)));
            ClearSelection();
            foreach (string id in DuplicateSourceSelection) SelectedNodeIds.Add(id);
            _selectedNodeId = FirstSelectedNodeId();
            if (_historyTransactionActive && UndoHistory.Count > 0) UndoHistory.RemoveAt(UndoHistory.Count - 1);
            _historyTransactionActive = false;
            _historyPendingCommit = false;
            _duplicatePlacementActive = false;
            DuplicateStartPositions.Clear();
            DuplicateNodeIds.Clear();
            DuplicateSourceSelection.Clear();
            _historyBaseline = OvAnimationGraph.Clone(graph);
            Main.RequestSave();
        }

        private static Vector2 ScreenToGraph(Vector2 screenPosition)
        {
            return (screenPosition - _canvasMin - _pan) / _zoom;
        }

        private static void MarkGraphChanged()
        {
            if (!_suppressHistory && CurrentGraph != null)
            {
                if (!_historyTransactionActive)
                {
                    if (_historyBaseline == null) _historyBaseline = OvAnimationGraph.Clone(CurrentGraph);
                    UndoHistory.Add(OvAnimationGraph.Clone(_historyBaseline));
                    if (UndoHistory.Count > UndoLimit) UndoHistory.RemoveAt(0);
                    _historyTransactionActive = true;
                }
                _historyPendingCommit = true;
            }
            Main.RequestSave();
        }

        private static void FinalizeHistoryTransaction()
        {
            if (!_historyPendingCommit || _duplicatePlacementActive) return;
            if (ImGui.IsAnyItemActive() || !string.IsNullOrEmpty(_draggingNodeId)
                || !string.IsNullOrEmpty(_resizingGroupId)) return;
            _historyBaseline = OvAnimationGraph.Clone(CurrentGraph);
            _historyTransactionActive = false;
            _historyPendingCommit = false;
        }

        private static void Undo()
        {
            if (CurrentGraph == null || UndoHistory.Count == 0) return;
            OvAnimationGraph snapshot = UndoHistory[UndoHistory.Count - 1];
            UndoHistory.RemoveAt(UndoHistory.Count - 1);
            _suppressHistory = true;
            SetCurrentGraph(OvAnimationGraph.Clone(snapshot));
            CurrentGraph.Normalize();
            if (IsImageMode) OvImageNodeAnimation.EnsureImageTarget(CurrentGraph);
            _suppressHistory = false;
            _historyBaseline = OvAnimationGraph.Clone(CurrentGraph);
            _historyTransactionActive = false;
            _historyPendingCommit = false;
            _duplicatePlacementActive = false;
            DuplicateStartPositions.Clear();
            DuplicateNodeIds.Clear();
            DuplicateSourceSelection.Clear();
            RemoveMissingSelections();
            Main.RequestSave();
        }

        private static void RemoveMissingSelections()
        {
            if (CurrentGraph?.Nodes == null)
            {
                ClearSelection();
                return;
            }
            var valid = new HashSet<string>();
            for (int i = 0; i < CurrentGraph.Nodes.Count; i++)
            {
                OvAnimationNode node = CurrentGraph.Nodes[i];
                if (node != null) valid.Add(node.Id);
            }
            SelectedNodeIds.RemoveWhere(id => !valid.Contains(id));
            if (!SelectedNodeIds.Contains(_selectedNodeId)) _selectedNodeId = FirstSelectedNodeId();
        }

        private static string SummarizeNote(string value, int limit)
        {
            string singleLine = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
            return singleLine.Length <= limit ? singleLine : singleLine.Substring(0, limit) + "...";
        }

        private static bool RectsOverlap(Vector2 leftMin, Vector2 leftMax, Vector2 rightMin, Vector2 rightMax)
        {
            return leftMin.X <= rightMax.X && leftMax.X >= rightMin.X
                && leftMin.Y <= rightMax.Y && leftMax.Y >= rightMin.Y;
        }

        private static bool IsCtrlDown()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

        private static bool IsShiftDown()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        private static bool HasInput(OvAnimationNode node, string port)
        {
            return ((node.Kind == OvAnimationNodeKind.Tween || node.Kind == OvAnimationNodeKind.Modify)
                    && (port == "flow" || port == "targets"))
                || (node.Kind == OvAnimationNodeKind.Effect && port == "targets")
                || (node.Kind == OvAnimationNodeKind.NumberFormat && port == "targets");
        }

        private static bool HasOutput(OvAnimationNode node, string port)
        {
            return (port == "flow" && (node.Kind == OvAnimationNodeKind.Trigger
                    || node.Kind == OvAnimationNodeKind.Tween
                    || node.Kind == OvAnimationNodeKind.Modify))
                || (port == "targets" && node.Kind == OvAnimationNodeKind.TokenInput);
        }

        private static void EnsureGraphIds(OvAnimationGraph graph)
        {
            graph.Normalize();
        }

        private static void PopulateDefaultSelection(OverlayerText text)
        {
            OvAnimationGraph graph = text.TokenAnimation;
            if (graph == null || graph.Enabled || graph.Nodes == null || text.TokenBindings == null) return;
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                OvAnimationNode node = graph.Nodes[i];
                if (node == null || node.Kind != OvAnimationNodeKind.TokenInput || node.SelectedTokenIds == null || node.SelectedTokenIds.Count > 0) continue;
                for (int t = 0; t < text.TokenBindings.Count; t++) node.SelectedTokenIds.Add(text.TokenBindings[t].Id);
                MarkGraphChanged();
                break;
            }
        }

        private static void StopKeyCapture()
        {
            _keyCaptureNodeId = string.Empty;
            _keyCaptureStartFrame = -1;
        }

        private static KeyCode[] BuildKeyboardKeys()
        {
            Array values = Enum.GetValues(typeof(KeyCode));
            var keys = new List<KeyCode>();
            for (int i = 0; i < values.Length; i++)
            {
                KeyCode key = (KeyCode)values.GetValue(i);
                if (key == KeyCode.None) continue;
                string name = key.ToString();
                if (name.StartsWith("Mouse", StringComparison.Ordinal)
                    || name.StartsWith("Joystick", StringComparison.Ordinal)) continue;
                keys.Add(key);
            }
            return keys.ToArray();
        }

        private static bool TryGetPressedKeyboardKey(out KeyCode pressedKey)
        {
            pressedKey = KeyCode.None;
            if (!Input.anyKeyDown) return false;
            for (int i = 0; i < KeyboardKeys.Length; i++)
            {
                if (!Input.GetKeyDown(KeyboardKeys[i])) continue;
                pressedKey = KeyboardKeys[i];
                return true;
            }
            return false;
        }

        private static void DrawSocket(ImDrawListPtr draw, Vector2 position, uint color)
        {
            draw.AddCircleFilled(position, 6f * _zoom, color);
            draw.AddCircle(position, 7f * _zoom, Color(0.04f, 0.04f, 0.04f, 1f), 16, 1.5f);
        }

        private static void DrawBezier(ImDrawListPtr draw, Vector2 from, Vector2 to, uint color, float thickness)
        {
            float handle = Math.Max(45f, Math.Abs(to.X - from.X) * 0.45f);
            draw.AddBezierCubic(from, from + new Vector2(handle, 0f), to - new Vector2(handle, 0f), to, color, thickness);
        }

        private static bool PointInRect(Vector2 point, Vector2 min, Vector2 max)
        {
            return point.X >= min.X && point.X <= max.X && point.Y >= min.Y && point.Y <= max.Y;
        }

        private static float Distance(Vector2 a, Vector2 b)
        {
            float x = a.X - b.X;
            float y = a.Y - b.Y;
            return (float)Math.Sqrt(x * x + y * y);
        }

        private static uint Color(float r, float g, float b, float a)
        {
            return ImGui.ColorConvertFloat4ToU32(new Vector4(r, g, b, a));
        }

        private static uint FlowColor() { return Color(0.95f, 0.62f, 0.22f, 1f); }
        private static uint TargetColor() { return Color(0.22f, 0.72f, 0.88f, 1f); }
    }
}
