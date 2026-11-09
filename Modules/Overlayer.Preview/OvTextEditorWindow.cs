using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ImGuiNET;
using UnityEngine;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;
using CheryTools.Sonnet.Shared;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.Overlayer.Preview
{
    internal static class OvTextEditorWindow
    {
        internal readonly struct TagItem
        {
            public readonly string Tag;
            private readonly string _description;
            private readonly string _descriptionEn;
            // Chinese category name: an internal key compared against Categories, shown via CategoriesEn.
            public readonly string Category;
            public readonly bool PlaceCursorInside;

            public TagItem(string tag, string description, string descriptionEn, string category, bool placeCursorInside = false)
            {
                Tag = tag;
                _description = description;
                _descriptionEn = descriptionEn;
                Category = category;
                PlaceCursorInside = placeCursorInside;
            }

            public string Description => L(_description, _descriptionEn);
        }

        private static readonly TagItem[] AllTags =
        {
            // 系统
            new TagItem("{fps}", "当前实时 FPS", "Current FPS", "系统"),
            new TagItem("{fps:1}", "当前 FPS（1 位小数）", "Current FPS (1 decimal)", "系统"),
            new TagItem("{fps:2}", "当前 FPS（2 位小数）", "Current FPS (2 decimals)", "系统"),
            new TagItem("{minfps}", "本次运行最低 FPS", "Lowest FPS this session", "系统"),
            new TagItem("{maxfps}", "本次运行最高 FPS", "Highest FPS this session", "系统"),
            new TagItem("{gameversion}", "ADOFAI 游戏版本", "ADOFAI game version", "系统"),
            new TagItem("{ctversion}", "CheryTools 版本", "CheryTools version", "系统"),
            new TagItem("{cherytoolsversion}", "CheryTools 完整版本号", "CheryTools full version", "系统"),

            // 谱面
            new TagItem("{progress}", "当前关卡游玩进度", "Level progress", "谱面"),
            new TagItem("{progress:2}", "当前进度（2 位小数）", "Progress (2 decimals)", "谱面"),
            new TagItem("{bpm}", "基础速度 BPM", "Base BPM", "谱面"),
            new TagItem("{tbpm}", "当前轨道速度 BPM", "Current track BPM", "谱面"),
            new TagItem("{cbpm}", "当前实际物理 BPM", "Current real BPM", "谱面"),
            new TagItem("{cur}", "当前每秒击打频率 (CPS)", "Current hits per second (CPS)", "谱面"),
            new TagItem("{x}", "播放倍速", "Playback speed", "谱面"),
            new TagItem("{level}", "谱面创作者信息", "Level author", "谱面"),
            new TagItem("{maptime}", "谱面总时长", "Level length", "谱面"),
            new TagItem("{maptime:p}", "谱面已播放时长", "Level time played", "谱面"),
            new TagItem("{musictime}", "音乐总时长", "Music length", "谱面"),
            new TagItem("{musictime:p}", "音乐已播放时长", "Music time played", "谱面"),
            new TagItem("{ttile}", "总轨道数量", "Total tiles", "谱面"),
            new TagItem("{atile}", "已通过轨道数量", "Tiles passed", "谱面"),

            // 判定
            new TagItem("{judge}", "当前判定模式名称", "Current difficulty name", "判定"),
            new TagItem("{interval}", "当前判定窗口百分比", "Current timing window (%)", "判定"),
            new TagItem("{acc}", "总体判定准确率", "Overall accuracy", "判定"),
            new TagItem("{acc:2}", "准确率（2 位小数）", "Accuracy (2 decimals)", "判定"),
            new TagItem("{xacc}", "X-Accuracy 纯度", "X-Accuracy", "判定"),
            new TagItem("{xacc:2}", "X-Accuracy（2 位小数）", "X-Accuracy (2 decimals)", "判定"),
            new TagItem("{timing}", "末次击打偏移 ms", "Last hit offset (ms)", "判定"),
            new TagItem("{timing:2}", "末次击打偏移 ms（2 位小数）", "Last hit offset (ms, 2 decimals)", "判定"),
            new TagItem("{te}", "Too Early 数量", "Too Early count", "判定"),
            new TagItem("{ve}", "Very Early 数量", "Very Early count", "判定"),
            new TagItem("{ep}", "Early Perfect 数量", "Early Perfect count", "判定"),
            new TagItem("{ap}", "所有完美无瑕数量", "All Perfect count", "判定"),
            new TagItem("{-p}", "提前无瑕数量", "Perfect- count", "判定"),
            new TagItem("{xp}", "X 完美数量", "XPerfect count", "判定"),
            new TagItem("{+p}", "延后完美无瑕数量", "Perfect+ count", "判定"),
            new TagItem("{lp}", "Late Perfect 数量", "Late Perfect count", "判定"),
            new TagItem("{vl}", "Very Late 数量", "Very Late count", "判定"),
            new TagItem("{tl}", "Too Late 数量", "Too Late count", "判定"),
            new TagItem("{fm}", "过早敲击错失数量", "Miss count", "判定"),
            new TagItem("{fo}", "按太快错失数量", "Overload count", "判定"),
            new TagItem("{miss}", "Miss / 死亡数量", "Miss count", "判定"),

            // 统计
            new TagItem("{AllPrefectCombo}", "全完美无瑕连击数", "All Perfect combo", "统计"),
            new TagItem("{XPrefectCombo}", "X 纯度连击数", "XPerfect combo", "统计"),
            new TagItem("{score}", "当前累计分数", "Current score", "统计"),
            new TagItem("{music}", "曲目信息", "Music info", "统计"),
            new TagItem("{artist}", "曲师名称", "Artist", "统计"),
            new TagItem("{title}", "曲名", "Song title", "统计"),
            new TagItem("{attempts}", "当前谱面重试次数", "Attempts on this level", "统计"),
            new TagItem("{checkpointused}", "已使用检查点数量", "Checkpoints used", "统计"),
            new TagItem("{curcheckpoint}", "已抵达检查点序号", "Checkpoint reached", "统计"),
            new TagItem("{totalcheckpoint}", "检查点总数量", "Total checkpoints", "统计"),
            new TagItem("{totalplaytime}", "本次谱面游玩累计时长", "Play time this run", "统计"),
            new TagItem("{datey}", "当前系统年份", "Current year", "统计"),
            new TagItem("{datem}", "当前系统月份", "Current month", "统计"),
            new TagItem("{dated}", "当前系统日期", "Current day", "统计"),
            new TagItem("{wtime}", "当前时间 (24 小时制)", "Current time (24-hour)", "统计"),
            new TagItem("{wtime12}", "当前时间 (12 小时制)", "Current time (12-hour)", "统计"),

            // 样式
            new TagItem("<color=#FFFFFFFF></color>", "白色文本", "White text", "样式", true),
            new TagItem("<color=#56C8FFFF></color>", "青蓝亮色", "Bright cyan", "样式", true),
            new TagItem("<color=#FF6B6BFF></color>", "警示红色", "Warning red", "样式", true),
            new TagItem("<color=#FFE066FF></color>", "亮黄色", "Bright yellow", "样式", true),
            new TagItem("<color=#69DB7CFF></color>", "浅绿色", "Light green", "样式", true),
            new TagItem("<color=#D0BFFFFF></color>", "浅紫色", "Light purple", "样式", true),
            new TagItem("<size=150%></size>", "放大字号 150%", "Larger text 150%", "样式", true),
            new TagItem("<size=80%></size>", "缩小字号 80%", "Smaller text 80%", "样式", true),
            new TagItem("<size=32></size>", "固定字号 32px", "Fixed size 32px", "样式", true),
            new TagItem("<b></b>", "加粗粗体", "Bold", "样式", true),
            new TagItem("<i></i>", "斜体文本", "Italic", "样式", true),
            new TagItem("<u></u>", "下划线", "Underline", "样式", true),
            new TagItem("<s></s>", "删除线", "Strikethrough", "样式", true),
            new TagItem("<alpha=#80></alpha>", "半透明通道 (50%)", "Half transparent (50%)", "样式", true),
            new TagItem("<align=center></align>", "居中排版", "Center aligned", "样式", true)
        };

        private static readonly string[] Categories = { "全部", "系统", "谱面", "判定", "统计", "样式" };
        private static readonly string[] CategoriesEn = { "All", "System", "Level", "Judgement", "Stats", "Style" };

        private static OvTextComponent _text;
        private static bool _centerNext = true;
        private static NumVector2 _windowPosition;
        private static NumVector2 _windowSize;
        private static bool _windowGeometryInitialized;
        private static string _searchQuery = string.Empty;
        private static int _selectedCategoryIndex;
        private static bool _showTagDrawer = true;
        private static float _drawerWidth = 320f;
        private static readonly Regex DynamicTagCounterRegex = new Regex(@"\{[^{}\r\n]+\}", RegexOptions.Compiled);

        public static bool IsOpen { get; private set; }

        public static void Open(OvTextComponent text)
        {
            if (text == null) return;
            _text = text;
            IsOpen = true;
            _centerNext = true;
            _searchQuery = string.Empty;
        }

        public static void Close()
        {
            IsOpen = false;
        }

        public static void Draw()
        {
            if (!IsOpen) return;
            if (_text == null || OvRuntime.Settings == null)
            {
                IsOpen = false;
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            NumVector2 display = ImGui.GetIO().DisplaySize;
            if (!_windowGeometryInitialized || _centerNext)
            {
                _windowSize = new NumVector2(
                    Math.Min(1120f, Math.Max(840f, display.X - 140f)),
                    Math.Min(720f, Math.Max(520f, display.Y - 100f)));
                _windowPosition = (display - _windowSize) * 0.5f;
                _windowGeometryInitialized = true;
                _centerNext = false;
            }

            _windowSize.X = Math.Min(Math.Max(800f, _windowSize.X), Math.Max(800f, display.X - 16f));
            _windowSize.Y = Math.Min(Math.Max(480f, _windowSize.Y), Math.Max(480f, display.Y - 16f));
            _windowPosition.X = Math.Min(Math.Max(8f - _windowSize.X + 120f, _windowPosition.X), display.X - 120f);
            _windowPosition.Y = Math.Min(Math.Max(8f, _windowPosition.Y), display.Y - 72f);

            ImGui.SetNextWindowPos(_windowPosition, ImGuiCond.Always);
            ImGui.SetNextWindowSize(_windowSize, ImGuiCond.Always);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 16f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, NumVector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 7f);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new NumVector2(10f, 6f));
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new NumVector4(1f, 1f, 1f, 0.055f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new NumVector4(1f, 1f, 1f, 0.10f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, SonnetPalette.Accent(0.13f));
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new NumVector4(0.006f, 0.010f, 0.016f, 0.94f));

            bool open = IsOpen;
            ImGuiWindowFlags windowFlags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings
                | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

            if (!ImGui.Begin("##SonnetOvTextEditorWindow", ref open, windowFlags))
            {
                ImGui.End();
                ImGui.PopStyleColor(4);
                ImGui.PopStyleVar(4);
                IsOpen = open;
                return;
            }

            IsOpen = open;
            DrawWindowBackground(_windowSize);
            DrawHeaderChrome(ref open);
            IsOpen = open;

            // 主体区：编辑器 + 右侧 Tag 侧栏（保持侧栏设计）
            float headerH = 56f;
            float footerH = 32f;
            float margin = 16f;
            float bodyY = headerH + 8f;
            float bodyHeight = _windowSize.Y - bodyY - footerH - 4f;
            float availableWidth = _windowSize.X - margin * 2f;

            string format = _text.TextFormat ?? string.Empty;

            ImGui.SetCursorPos(new NumVector2(margin, bodyY));
            DrawEditorAndSidebar(availableWidth, bodyHeight, ref format);

            // 底部一体化状态栏（单行极简实时预览与统计）
            ImGui.SetCursorPos(new NumVector2(margin, _windowSize.Y - footerH - 4f));
            DrawFooterStatusBar(availableWidth, footerH, format);

            ImGui.End();

            SonnetWindowChrome.DrawResizeHandles("OvText", ref _windowPosition, ref _windowSize, display, new NumVector2(800f, 480f));
            ImGui.PopStyleColor(4);
            ImGui.PopStyleVar(4);
        }

        private static void DrawWindowBackground(NumVector2 size)
        {
            var visuals = OvRuntime.Visuals;
            CheryTools.Sonnet.Contracts.ModuleBackgroundInfo background;
            if (visuals == null || !visuals.TryGetPanelBackground(out background)
                || background.TextureId == IntPtr.Zero) return;

            NumVector2 min = ImGui.GetWindowPos();
            NumVector2 max = min + size;
            SonnetWindowChrome.CalculateCoverUv(background.TextureWidth, background.TextureHeight, size.X, size.Y,
                out NumVector2 uv0, out NumVector2 uv1);
            if (background.FlipY)
            {
                float y = uv0.Y;
                uv0.Y = uv1.Y;
                uv1.Y = y;
            }
            ImDrawListPtr draw = ImGui.GetWindowDrawList();
            draw.AddImageRounded(background.TextureId, min, max, uv0, uv1,
                ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, background.ImageOpacity * 0.70f)), 16f,
                ImDrawFlags.RoundCornersAll);
            draw.AddRectFilled(min, max,
                ImGui.GetColorU32(new NumVector4(0.006f, 0.010f, 0.016f, background.PanelOpacity * 0.85f)), 16f);
        }

        private static void DrawHeaderChrome(ref bool open)
        {
            NumVector2 origin = ImGui.GetWindowPos();
            ImDrawListPtr draw = ImGui.GetWindowDrawList();

            // 标题栏底色与底部分割线
            draw.AddRectFilled(origin, origin + new NumVector2(_windowSize.X, 56f),
                ImGui.GetColorU32(new NumVector4(0.010f, 0.015f, 0.024f, 0.88f)), 16f,
                ImDrawFlags.RoundCornersTop);
            draw.AddLine(origin + new NumVector2(16f, 55.5f), origin + new NumVector2(_windowSize.X - 16f, 55.5f),
                ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, 0.08f)), 1f);

            // 主标题与副标题（去除多余突兀的药丸 Badge，保持原生干净排版）
            string compName = string.IsNullOrEmpty(_text.Name) ? L("文本", "Text") : _text.Name;
            string titleText = L("OV 文本编辑器 - ", "OV text editor - ") + compName;
            draw.AddText(origin + new NumVector2(20f, 13f), ImGui.GetColorU32(SonnetPalette.Text),
                titleText);
            draw.AddText(origin + new NumVector2(20f, 33f), ImGui.GetColorU32(SonnetPalette.Muted),
                L("实时富文本与动态变量编辑  ·  按 ESC 快捷退出", "Live rich text and tag editing  ·  ESC to close"));

            // 顶部拖拽手柄
            ImGui.SetCursorScreenPos(origin + new NumVector2(8f, 4f));
            ImGui.InvisibleButton("##DragOvTextEditor", new NumVector2(Math.Max(1f, _windowSize.X - 440f), 48f));
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                _windowPosition += ImGui.GetIO().MouseDelta;

            // 右侧操作栏：侧栏开关 / 撤销 / 重做 / 清空 / 复制 / 关闭（单行排布，绝不溢出重叠）
            float rightPadding = 16f;
            float closeBtnW = 32f;
            float actionBtnW = 52f;
            float toggleBtnW = 86f;
            float gap = 6f;

            float actionsTotalWidth = toggleBtnW + gap + actionBtnW * 4f + gap * 3f + gap * 2f + closeBtnW;
            float rightX = _windowSize.X - actionsTotalWidth - rightPadding;

            ImGui.SetCursorPos(new NumVector2(rightX, 13f));

            string format = _text.TextFormat ?? string.Empty;

            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 7f);

            // 侧栏收起/展开按钮
            string toggleLabel = _showTagDrawer ? L("收起侧栏", "Hide sidebar") : L("展开侧栏", "Show sidebar");
            if (ImGui.Button(toggleLabel + "##ToggleSidebar", new NumVector2(toggleBtnW, 30f)))
            {
                _showTagDrawer = !_showTagDrawer;
            }

            ImGui.SameLine(0f, gap);
            if (ImGui.Button(L("撤销##HeaderUndo", "Undo##HeaderUndo"), new NumVector2(actionBtnW, 30f)))
            {
                if (OvRichTextEditor.Undo("OvTextEditorWindow_Code", ref format))
                {
                    _text.TextFormat = format;
                    OvRuntime.Changed();
                }
            }

            ImGui.SameLine(0f, gap);
            if (ImGui.Button(L("重做##HeaderRedo", "Redo##HeaderRedo"), new NumVector2(actionBtnW, 30f)))
            {
                if (OvRichTextEditor.Redo("OvTextEditorWindow_Code", ref format))
                {
                    _text.TextFormat = format;
                    OvRuntime.Changed();
                }
            }

            ImGui.SameLine(0f, gap);
            if (ImGui.Button(L("清空##HeaderClear", "Clear##HeaderClear"), new NumVector2(actionBtnW, 30f)))
            {
                OvRichTextEditor.Clear("OvTextEditorWindow_Code", ref format);
                _text.TextFormat = format;
                OvRuntime.Changed();
            }

            ImGui.SameLine(0f, gap);
            if (ImGui.Button(L("复制##HeaderCopy", "Copy##HeaderCopy"), new NumVector2(actionBtnW, 30f)))
            {
                GUIUtility.systemCopyBuffer = format ?? string.Empty;
            }

            // 关闭按钮
            ImGui.SameLine(0f, gap * 2f);
            NumVector2 closeMin = origin + new NumVector2(_windowSize.X - 44f, 13f);
            ImGui.SetCursorScreenPos(closeMin);
            if (ImGui.InvisibleButton("##CloseOvTextEditor", new NumVector2(30f, 30f))) open = false;
            bool hovered = ImGui.IsItemHovered();
            if (hovered) draw.AddRectFilled(closeMin, closeMin + new NumVector2(30f, 30f),
                ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, 0.08f)), 7f);
            uint closeColor = ImGui.GetColorU32(hovered
                ? SonnetPalette.Text
                : SonnetPalette.Muted);
            draw.AddLine(closeMin + new NumVector2(9f, 9f), closeMin + new NumVector2(21f, 21f), closeColor, 1.6f);
            draw.AddLine(closeMin + new NumVector2(21f, 9f), closeMin + new NumVector2(9f, 21f), closeColor, 1.6f);

            ImGui.PopStyleVar();

            // 外轮廓发光描边
            ImDrawListPtr foreground = ImGui.GetForegroundDrawList();
            foreground.AddRect(origin + new NumVector2(0.5f), origin + _windowSize - new NumVector2(0.5f),
                ImGui.GetColorU32(SonnetPalette.Accent(0.28f)), 15.5f,
                ImDrawFlags.RoundCornersAll, 1f);
        }

        private static void DrawEditorAndSidebar(float width, float height, ref string format)
        {
            if (_showTagDrawer)
            {
                _drawerWidth = Mathf.Clamp(_drawerWidth, 220f, Mathf.Max(240f, width * 0.65f));
                float splitterWidth = 6f;
                float editorWidth = Mathf.Max(260f, width - _drawerWidth - splitterWidth - 8f);

                // 左侧：代码编辑器（半透玻璃质感）
                if (OvRichTextEditor.Draw("OvTextEditorWindow_Code", ref format, new NumVector2(editorWidth, height)))
                {
                    _text.TextFormat = format;
                    OvRuntime.Changed();
                }

                ImGui.SameLine(0f, 4f);
                // 拖拽分割条（类似 FreeMake）
                ImGui.InvisibleButton("##OvTagDrawerSplitter", new NumVector2(splitterWidth, height));
                if (ImGui.IsItemHovered() || ImGui.IsItemActive())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
                }
                if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    _drawerWidth = Mathf.Clamp(_drawerWidth - ImGui.GetIO().MouseDelta.X, 220f, width * 0.65f);
                }

                ImDrawListPtr draw = ImGui.GetWindowDrawList();
                NumVector2 splitMin = ImGui.GetItemRectMin();
                NumVector2 splitMax = ImGui.GetItemRectMax();
                uint splitColor = ImGui.GetColorU32(ImGui.IsItemActive() ? SonnetPalette.Accent(0.85f)
                    : ImGui.IsItemHovered() ? SonnetPalette.Accent(0.45f)
                    : new NumVector4(1f, 1f, 1f, 0.08f));
                draw.AddRectFilled(splitMin + new NumVector2(2f, 0f), splitMax - new NumVector2(2f, 0f), splitColor, 2f);

                ImGui.SameLine(0f, 4f);
                DrawTagSidebar(_drawerWidth, height, ref format);
            }
            else
            {
                if (OvRichTextEditor.Draw("OvTextEditorWindow_Code", ref format, new NumVector2(width, height)))
                {
                    _text.TextFormat = format;
                    OvRuntime.Changed();
                }
            }
        }

        private static void DrawTagSidebar(float width, float height, ref string format)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 8f);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new NumVector4(0.008f, 0.013f, 0.021f, 0.78f));
            ImGui.PushStyleColor(ImGuiCol.Border, new NumVector4(1f, 1f, 1f, 0.09f));

            ImGui.BeginChild("##OvTextEditorTagSidebar", new NumVector2(width, height), ImGuiChildFlags.Borders);

            // 侧栏标题与项数
            ImGui.SetCursorPos(new NumVector2(12f, 10f));
            ImGui.TextColored(SonnetPalette.Accent(1f), L("Tag 变量库", "Tag library"));
            ImGui.SameLine();
            ImGui.TextDisabled($"({AllTags.Length})");

            // 搜索输入框
            ImGui.SetCursorPosX(12f);
            ImGui.SetNextItemWidth(width - 24f);
            ImGui.InputTextWithHint("##TagSearchInput", L("搜索 Tag 或说明...", "Search tags or descriptions..."), ref _searchQuery, 64);

            // 分类 Pills 选择器
            ImGui.SetCursorPosX(12f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 9f);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new NumVector2(8f, 2f));

            for (int i = 0; i < Categories.Length; i++)
            {
                bool isSelected = _selectedCategoryIndex == i;
                if (isSelected)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, SonnetPalette.Accent(0.28f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, SonnetPalette.Accent(0.4f));
                    ImGui.PushStyleColor(ImGuiCol.Text, SonnetPalette.Accent(1f));
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, new NumVector4(1f, 1f, 1f, 0.04f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new NumVector4(1f, 1f, 1f, 0.09f));
                    ImGui.PushStyleColor(ImGuiCol.Text, SonnetPalette.Muted);
                }

                if (ImGui.Button((SonnetText.IsChinese ? Categories[i] : CategoriesEn[i]) + "##CatTab_" + i))
                {
                    _selectedCategoryIndex = i;
                }
                ImGui.PopStyleColor(3);

                if (i < Categories.Length - 1 && ImGui.GetCursorPosX() + 46f < width - 12f)
                {
                    ImGui.SameLine(0f, 4f);
                }
            }

            ImGui.PopStyleVar(2);

            // 细分隔线
            ImGui.Spacing();
            NumVector2 sepStart = ImGui.GetCursorScreenPos() + new NumVector2(12f, 0f);
            ImGui.GetWindowDrawList().AddLine(sepStart, sepStart + new NumVector2(width - 24f, 0f),
                ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, 0.06f)), 1f);
            ImGui.Spacing();

            // 滚动卡片列表（无生硬横线，整洁卡片悬停）
            string selectedCatName = Categories[_selectedCategoryIndex];
            string filter = (_searchQuery ?? string.Empty).Trim();

            ImGui.BeginChild("##TagCardsScrollList", new NumVector2(0f, 0f), ImGuiChildFlags.None);

            for (int i = 0; i < AllTags.Length; i++)
            {
                TagItem item = AllTags[i];

                if (_selectedCategoryIndex > 0 && !string.Equals(item.Category, selectedCatName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(filter)
                    && item.Tag.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                    && item.Description.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                ImGui.PushID("TagCard_" + i);

                NumVector2 cardPos = ImGui.GetCursorScreenPos() + new NumVector2(8f, 0f);
                float cardW = width - 16f;
                float cardH = 42f;

                bool cardHovered = ImGui.IsMouseHoveringRect(cardPos, cardPos + new NumVector2(cardW, cardH));
                if (cardHovered)
                {
                    ImGui.GetWindowDrawList().AddRectFilled(cardPos, cardPos + new NumVector2(cardW, cardH),
                        ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, 0.04f)), 6f);
                }

                ImGui.SetCursorPosX(12f);
                ImGui.BeginGroup();

                // 标签名
                NumVector4 tagColor = item.PlaceCursorInside
                    ? new NumVector4(0.82f, 0.58f, 1f, 1f)
                    : SonnetPalette.Accent(1f);
                ImGui.TextColored(tagColor, SonnetWidgets.Plain(item.Tag));

                // 插入按钮
                float btnWidth = 50f;
                ImGui.SameLine(width - btnWidth - 32f);
                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5f);
                ImGui.PushStyleColor(ImGuiCol.Button, new NumVector4(0.38f, 0.80f, 1f, cardHovered ? 0.22f : 0.12f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, SonnetPalette.Accent(0.35f));

                if (ImGui.SmallButton(L("插入##InsertTag_", "Insert##InsertTag_") + i))
                {
                    OvRichTextEditor.InsertAtCursor("OvTextEditorWindow_Code", ref format, item.Tag, item.PlaceCursorInside);
                    _text.TextFormat = format;
                    OvRuntime.Changed();
                }

                ImGui.PopStyleColor(2);
                ImGui.PopStyleVar();

                // 说明文本
                ImGui.TextColored(SonnetPalette.Muted, SonnetWidgets.Plain(item.Description));

                ImGui.EndGroup();
                ImGui.PopID();

                ImGui.Spacing();
            }

            ImGui.EndChild();

            ImGui.EndChild();
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar();
        }

        private static void DrawFooterStatusBar(float width, float height, string format)
        {
            string resolved = OvRuntime.ResolvePreviewText(format);

            int tagCount = 0;
            if (!string.IsNullOrEmpty(format))
            {
                tagCount = DynamicTagCounterRegex.Matches(format).Count;
            }

            int lineCount = 1;
            for (int i = 0; i < format.Length; i++)
            {
                if (format[i] == '\n') lineCount++;
            }

            ImGui.PushStyleColor(ImGuiCol.ChildBg, NumVector4.Zero);
            ImGui.BeginChild("##FooterStatusBar", new NumVector2(width, height), ImGuiChildFlags.None);

            NumVector2 origin = ImGui.GetWindowPos();
            ImDrawListPtr draw = ImGui.GetWindowDrawList();

            // 顶部分割线
            draw.AddLine(origin, origin + new NumVector2(width, 0f),
                ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, 0.07f)), 1f);

            ImGui.SetCursorPos(new NumVector2(4f, 6f));

            // 左侧：实时解析预览
            ImGui.TextColored(SonnetPalette.Accent(1f), L("实时解析:", "Live preview:"));
            ImGui.SameLine(0f, 8f);

            if (string.IsNullOrEmpty(resolved))
            {
                ImGui.TextDisabled(L("(空)", "(empty)"));
            }
            else
            {
                string singleLine = resolved.Replace("\r", "").Replace('\n', ' ');
                if (singleLine.Length > 80) singleLine = singleLine.Substring(0, 77) + "...";
                ImGui.TextColored(SonnetPalette.Text, SonnetWidgets.Plain(singleLine));
            }

            // 右侧：紧凑状态指标
            string stats = L($"{format.Length} 字符 · {lineCount} 行 · {tagCount} 变量 · ESC 退出", $"{format.Length} chars · {lineCount} lines · {tagCount} tags · ESC to close");
            float statsW = ImGui.CalcTextSize(stats).X;
            ImGui.SameLine(width - statsW - 6f);
            ImGui.TextDisabled(stats);

            ImGui.EndChild();
            ImGui.PopStyleColor();
        }
    }
}
