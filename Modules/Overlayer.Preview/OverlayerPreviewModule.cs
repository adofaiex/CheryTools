using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CheryTools.Sonnet.Contracts;
using ImGuiNET;
using UnityEngine;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;
using CheryTools.Sonnet.Shared;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.Overlayer.Preview
{
    public sealed class OverlayerPreviewModule : ICheryModule, ICheryModuleOverlay
    {
        private readonly struct TagInsertItem
        {
            internal readonly string Tag;
            private readonly string _description;
            private readonly string _descriptionEn;

            internal TagInsertItem(string tag, string description, string descriptionEn)
            {
                Tag = tag;
                _description = description;
                _descriptionEn = descriptionEn;
            }

            internal string Description => L(_description, _descriptionEn);
        }

        private static readonly TagInsertItem[] TagInsertItems =
        {
            new TagInsertItem("{fps}", "当前 FPS", "Current FPS"),
            new TagInsertItem("{fps:1}", "当前 FPS，最多 1 位小数", "Current FPS, up to 1 decimal"),
            new TagInsertItem("{fps:2}", "当前 FPS，最多 2 位小数", "Current FPS, up to 2 decimals"),
            new TagInsertItem("{minfps}", "本次最低 FPS", "Lowest FPS this session"),
            new TagInsertItem("{maxfps}", "本次最高 FPS", "Highest FPS this session"),
            new TagInsertItem("{progress}", "当前关卡进度", "Level progress"),
            new TagInsertItem("{progress:2}", "当前关卡进度，最多 2 位小数", "Level progress, up to 2 decimals"),
            new TagInsertItem("{bpm}", "基础 BPM", "Base BPM"),
            new TagInsertItem("{tbpm}", "当前轨道速度 BPM", "Current track BPM"),
            new TagInsertItem("{cbpm}", "当前真实 BPM", "Current real BPM"),
            new TagInsertItem("{x}", "播放倍速", "Playback speed"),
            new TagInsertItem("{level}", "谱面作者", "Level author"),
            new TagInsertItem("{maptime}", "谱面总时长", "Level length"),
            new TagInsertItem("{maptime:p}", "谱面已游玩时长", "Level time played"),
            new TagInsertItem("{musictime}", "音乐总时长", "Music length"),
            new TagInsertItem("{musictime:p}", "音乐已播放时长", "Music time played"),
            new TagInsertItem("{cur}", "当前每秒击打数", "Current hits per second"),
            new TagInsertItem("{judge}", "当前判定模式", "Current difficulty"),
            new TagInsertItem("{interval}", "当前判定窗口", "Current timing window"),
            new TagInsertItem("{datey}", "当前年份", "Current year"),
            new TagInsertItem("{datem}", "当前月份", "Current month"),
            new TagInsertItem("{dated}", "当前日期", "Current day"),
            new TagInsertItem("{wtime}", "当前时间，24 小时制", "Current time, 24-hour"),
            new TagInsertItem("{wtime12}", "当前时间，12 小时制", "Current time, 12-hour"),
            new TagInsertItem("{year}", "当前年份（兼容别名）", "Current year (alias)"),
            new TagInsertItem("{month}", "当前月份（兼容别名）", "Current month (alias)"),
            new TagInsertItem("{day}", "当前日期（兼容别名）", "Current day (alias)"),
            new TagInsertItem("{worldtime}", "当前时间，24 小时制（兼容别名）", "Current time, 24-hour (alias)"),
            new TagInsertItem("{worldtime12}", "当前时间，12 小时制（兼容别名）", "Current time, 12-hour (alias)"),
            new TagInsertItem("{acc}", "准确率", "Accuracy"),
            new TagInsertItem("{xacc}", "X-Accuracy", "X-Accuracy"),
            new TagInsertItem("{acc:2}", "准确率，最多 2 位小数", "Accuracy, up to 2 decimals"),
            new TagInsertItem("{xacc:2}", "X-Accuracy，最多 2 位小数", "X-Accuracy, up to 2 decimals"),
            new TagInsertItem("{ttile}", "总轨道数量", "Total tiles"),
            new TagInsertItem("{atile}", "已通过轨道数量", "Tiles passed"),
            new TagInsertItem("{te}", "Too Early 数量", "Too Early count"),
            new TagInsertItem("{ve}", "Very Early 数量", "Very Early count"),
            new TagInsertItem("{ep}", "Early Perfect 数量", "Early Perfect count"),
            new TagInsertItem("{ap}", "所有完美无瑕数量", "All Perfect count"),
            new TagInsertItem("{-p}", "提前无瑕数量", "Perfect- count"),
            new TagInsertItem("{xp}", "X 无瑕数量", "XPerfect count"),
            new TagInsertItem("{+p}", "落后完美无瑕数量", "Perfect+ count"),
            new TagInsertItem("{lp}", "Late Perfect 数量", "Late Perfect count"),
            new TagInsertItem("{vl}", "Very Late 数量", "Very Late count"),
            new TagInsertItem("{tl}", "Too Late 数量", "Too Late count"),
            new TagInsertItem("{fm}", "错过数量", "Miss count"),
            new TagInsertItem("{fo}", "按太快数量", "Overload count"),
            new TagInsertItem("{miss}", "死亡 / Miss 数量", "Miss count"),
            new TagInsertItem("{AllPrefectCombo}", "所有完美无瑕连击", "All Perfect combo"),
            new TagInsertItem("{XPrefectCombo}", "X 完美无瑕连击", "XPerfect combo"),
            new TagInsertItem("{score}", "当前分数", "Current score"),
            new TagInsertItem("{music}", "音乐信息", "Music info"),
            new TagInsertItem("{artist}", "曲师", "Artist"),
            new TagInsertItem("{title}", "曲名", "Song title"),
            new TagInsertItem("{attempts}", "当前谱面尝试次数", "Attempts on this level"),
            new TagInsertItem("{checkpointused}", "使用的检查点数量", "Checkpoints used"),
            new TagInsertItem("{curcheckpoint}", "当前检查点数量", "Current checkpoint"),
            new TagInsertItem("{totalcheckpoint}", "总检查点数量", "Total checkpoints"),
            new TagInsertItem("{totalplaytime}", "本次谱面游玩时长", "Play time this run"),
            new TagInsertItem("{cherysui}", "CherySui 彩蛋", "CherySui easter egg"),
            new TagInsertItem("{cherysui:ct}", "CheryTools 彩蛋", "CheryTools easter egg"),
            new TagInsertItem("{gameversion}", "游戏版本", "Game version"),
            new TagInsertItem("{ctversion}", "CheryTools 版本（兼容别名）", "CheryTools version (alias)"),
            new TagInsertItem("{cherytoolsversion}", "CheryTools 版本", "CheryTools version"),
            new TagInsertItem("<color=#FFFFFFFF></color>", "TMP 富文本颜色", "TMP rich text color"),
            new TagInsertItem("<size=150%></size>", "TMP 相对字号", "TMP relative size")
        };

        private static readonly IReadOnlyList<ModuleTab> ModuleTabs = new List<ModuleTab>
        {
            new ModuleTab("texts", "文本", "Text"),
            new ModuleTab("images", "图片", "Images"),
            new ModuleTab("videos", "视频", "Videos"),
            new ModuleTab("progress", "进度条", "Progress bars"),
            new ModuleTab("settings", "OV 设置", "OV Settings")
        };

        private int _selectedText;
        private int _selectedImage;
        private int _selectedVideo;
        private int _selectedProgress;
        private int _textCategory;
        private int _imageCategory;
        private int _videoCategory;
        private int _progressCategory;
        private string _tagSearch = string.Empty;
        private int _textCursorUtf8Offset;
        private readonly ImGuiInputTextCallback _captureTextCursor;

        public unsafe OverlayerPreviewModule()
        {
            _captureTextCursor = CaptureTextCursor;
        }

        public ModuleMetadata Metadata { get; } = new ModuleMetadata(
            "overlayer.preview", "Overlayer", "Preview 0.1.0", "CheryTools", new NumVector4(0.76f, 0.47f, 1f, 1f))
        {
            NavigationTitle = "OV 组件",
            NavigationTitleEn = "OV Components"
        };

        public IReadOnlyList<ModuleTab> Tabs => ModuleTabs;
        public void Initialize(ModuleContext context) => OvRuntime.Initialize(context);
        public void Update() => OvRuntime.Tick();

        public void Draw(ModuleUiContext context)
        {
            OvSettings settings = OvRuntime.Settings;
            if (settings == null) return;
            PushControlStyle();
            switch (context.ActiveTabId)
            {
                case "images": DrawImages(settings); break;
                case "videos": DrawVideos(settings); break;
                case "progress": DrawProgress(settings); break;
                case "settings": DrawSettings(settings); break;
                default: DrawTexts(settings); break;
            }
            PopControlStyle();
        }

        private static void PushControlStyle()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 7f);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new NumVector2(10f, 6f));
            ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 9f);
            ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);

            // FrameBg is shared by sliders, drags, text inputs and combos.  Keep it
            // translucent here; the old near-opaque value made every slider look
            // like a solid bar merely to style the combo selector.
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new NumVector4(1f, 1f, 1f, 0.055f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new NumVector4(1f, 1f, 1f, 0.10f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, SonnetPalette.Accent(0.13f));

            // Combo popups have their own surface and row colours.  Styling these
            // independently prevents the dropdown from falling back to the stock
            // ImGui popup while leaving ordinary controls transparent.
            ImGui.PushStyleColor(ImGuiCol.PopupBg, new NumVector4(0.010f, 0.015f, 0.023f, 0.965f));
            ImGui.PushStyleColor(ImGuiCol.Border, SonnetPalette.Accent(0.16f));
            ImGui.PushStyleColor(ImGuiCol.Header, new NumVector4(1f, 1f, 1f, 0.045f));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, SonnetPalette.Accent(0.16f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, SonnetPalette.Accent(0.24f));
        }

        private static void PopControlStyle()
        {
            ImGui.PopStyleColor(8);
            ImGui.PopStyleVar(4);
        }

        public void Shutdown() => OvRuntime.Shutdown();
        public bool HasOpenOverlay => OvTokenNodeEditor.IsOpen || OvTextEditorWindow.IsOpen;
        public void DrawOverlay()
        {
            if (OvTokenNodeEditor.IsOpen) OvTokenNodeEditor.Draw();
            if (OvTextEditorWindow.IsOpen) OvTextEditorWindow.Draw();
        }

        private void DrawTexts(OvSettings settings)
        {
            DrawPageIntro(L("文本组件", "Text components"), L("文本、基础 Tag 与文字效果由 Overlayer 模块的独立 UGUI 运行时渲染。", "Text, tags and text effects are rendered by the Overlayer module's own UGUI runtime."));
            DrawList(settings.Texts, ref _selectedText, () => new OvTextComponent { Name = L("文本 ", "Text ") + (settings.Texts.Count + 1) });
            if (!TrySelected(settings.Texts, ref _selectedText, out OvTextComponent item)) return;
            DrawCommon(item);
            DrawCategoryTabs("text", ref _textCategory, L("布局", "Layout"), L("外观", "Style"), L("内容", "Content"), L("节点动画", "Node animation"));
            if (_textCategory == 0) DrawLayout(item);
            else if (_textCategory == 1) DrawTextAppearance(item);
            else if (_textCategory == 2) DrawTextContent(item);
            else DrawTextAnimation(item);
        }

        private void DrawTextContent(OvTextComponent item)
        {
            DrawSectionTitle(L("文本内容", "Text content"));

            if (SonnetWidgets.FitButton(L("打开独立文本编辑器##OpenOvStandaloneEditor", "Open text editor##OpenOvStandaloneEditor"), 160f, 0f))
            {
                OvTextEditorWindow.Open(item);
            }
            ImGui.SameLine();
            if (ImGui.Button(L("插入 Tag##InlineOpenTagPopup", "Insert tag##InlineOpenTagPopup")))
            {
                ImGui.OpenPopup("##OvTagInsertPopup");
            }
            ImGui.SameLine();
            string format = item.TextFormat ?? string.Empty;
            if (ImGui.Button(L("清空##InlineClearFormat", "Clear##InlineClearFormat")))
            {
                OvRichTextEditor.Clear("OvInlineTextFormat_" + item.Id, ref format);
                item.TextFormat = format;
                OvRuntime.Changed();
            }
            ImGui.SameLine();
            ImGui.TextDisabled(L("代码高亮与行号编辑器 · 双击选词 · 点击左侧进入全屏大窗口", "Highlighted editor with line numbers · double-click selects a word · click the left side for the full editor"));

            if (OvRichTextEditor.Draw("OvInlineTextFormat_" + item.Id, ref format, new NumVector2(-1f, 140f)))
            {
                item.TextFormat = format;
                OvRuntime.Changed();
            }

            // 实时预览卡片
            string preview = OvRuntime.ResolvePreviewText(format);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new NumVector4(0.008f, 0.013f, 0.021f, 0.80f));
            ImGui.PushStyleColor(ImGuiCol.Border, new NumVector4(1f, 1f, 1f, 0.08f));

            if (ImGui.BeginChild("##InlinePreviewCard", new NumVector2(-1f, 32f), ImGuiChildFlags.Borders))
            {
                ImGui.SetCursorPos(new NumVector2(10f, 6f));
                ImGui.TextColored(SonnetPalette.Accent(1f), L("实时解析预览:", "Live preview:"));
                ImGui.SameLine();
                if (string.IsNullOrEmpty(preview))
                {
                    ImGui.TextDisabled(L("(无内容)", "(empty)"));
                }
                else
                {
                    string singleLine = preview.Replace("\r", "").Replace('\n', ' ');
                    if (singleLine.Length > 120) singleLine = singleLine.Substring(0, 117) + "...";
                    ImGui.TextColored(SonnetPalette.Text, SonnetWidgets.Plain(singleLine));
                }
            }
            // Must run even when BeginChild returns false (the card is scrolled out of view).
            ImGui.EndChild();
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar();

            DrawTagInsertPopup(item);
        }

        private static void DrawInlineQuickTag(string label, string tag, bool placeInside, OvTextComponent item, ref string format)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new NumVector4(1f, 1f, 1f, 0.045f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, SonnetPalette.Accent(0.2f));

            if (ImGui.Button(label + "##InlineQuick_" + label))
            {
                OvRichTextEditor.InsertAtCursor("OvInlineTextFormat_" + item.Id, ref format, tag, placeInside);
                item.TextFormat = format;
                OvRuntime.Changed();
            }

            ImGui.PopStyleColor(2);
        }

        private static void DrawTextAppearance(OvTextComponent item)
        {
            DrawSectionTitle(L("文字样式", "Text style"));
            float size = item.FontSize;
            ImGui.SetNextItemWidth(Math.Min(520f, ImGui.GetContentRegionAvail().X));
            if (SonnetWidgets.SliderFloat(L("字体大小", "Font size"), ref size, 8f, 240f, "%.0f px")) { item.FontSize = size; OvRuntime.Changed(); }
            int alignment = item.Alignment;
            ImGui.SetNextItemWidth(Math.Min(520f, ImGui.GetContentRegionAvail().X));
            if (ImGui.Combo(L("文字对齐", "Alignment"), ref alignment, L("左对齐\0居中\0右对齐\0", "Left\0Center\0Right\0"))) { item.Alignment = alignment; OvRuntime.Changed(); }
            if (ColorEdit(L("文字颜色", "Text color"), item.TextColor)) OvRuntime.Changed();
            ImGui.Spacing();
            ImGui.TextUnformatted(L("自定义字体", "Custom font"));
            string font = item.FontPath ?? string.Empty;
            float btnWidth = 80f;
            float inputWidth = Math.Max(120f, ImGui.GetContentRegionAvail().X - btnWidth * 2f - 16f);
            ImGui.SetNextItemWidth(inputWidth);
            if (ImGui.InputTextWithHint("##OvTextFont", L("留空使用默认字体 (MiSans-Bold / Arial)", "Leave empty for the default font (MiSans-Bold / Arial)"), ref font, 1024))
            {
                item.FontPath = font;
                OvRuntime.Changed();
            }
            if (ImGui.IsItemDeactivatedAfterEdit()) OvRuntime.Changed(true);
            ImGui.SameLine();
            if (ImGui.Button(L("选择字体##OvPickFont", "Choose font##OvPickFont"), new NumVector2(btnWidth, 0f)))
            {
                string initial = string.IsNullOrEmpty(item.FontPath)
                    ? OvPackageService.GameRoot()
                    : (File.Exists(item.FontPath) ? Path.GetDirectoryName(item.FontPath) : OvPackageService.GameRoot());
                string picked = ModernFileDialog.ShowOpenFileDialog(L("选择 OV 字体", "Choose OV font"),
                    L("字体文件 (*.ttf;*.otf)|*.ttf;*.otf|所有文件 (*.*)|*.*", "Font files (*.ttf;*.otf)|*.ttf;*.otf|All files (*.*)|*.*"), initial);
                if (!string.IsNullOrEmpty(picked))
                {
                    item.FontPath = picked;
                    OvRuntime.Changed(true);
                }
            }
            ImGui.SameLine();
            bool hasCustomFont = !string.IsNullOrEmpty(item.FontPath);
            if (!hasCustomFont) ImGui.BeginDisabled();
            if (ImGui.Button(L("清除##OvClearFont", "Clear##OvClearFont"), new NumVector2(btnWidth, 0f)) && hasCustomFont)
            {
                item.FontPath = string.Empty;
                OvRuntime.Changed(true);
            }
            if (!hasCustomFont) ImGui.EndDisabled();
            ImGui.TextDisabled(L("留空时自动使用 MiSans 或内置默认字体；支持 .ttf / .otf 字体文件。", "Empty uses MiSans or the built-in default font; .ttf and .otf fonts are supported."));
            ImGui.Spacing();
            if (ImGui.Checkbox(L("文字描边", "Text outline"), ref item.OutlineEnabled)) OvRuntime.Changed();
            if (item.OutlineEnabled)
            {
                if (ColorEdit(L("描边颜色", "Outline color"), item.OutlineColor)) OvRuntime.Changed();
                if (ImGui.DragFloat(L("描边粗细", "Outline thickness"), ref item.OutlineThickness, 0.05f, 0f, 16f, "%.2f")) OvRuntime.Changed();
            }
            if (ImGui.Checkbox(L("文字阴影", "Text shadow"), ref item.ShadowEnabled)) OvRuntime.Changed();
            if (item.ShadowEnabled)
            {
                if (ColorEdit(L("阴影颜色", "Shadow color"), item.ShadowColor)) OvRuntime.Changed();
                if (DragPair(L("阴影偏移", "Shadow offset"), ref item.ShadowOffset[0], ref item.ShadowOffset[1], -1000f, 1000f, 0.2f)) OvRuntime.Changed();
                if (ImGui.DragFloat(L("阴影柔度", "Shadow softness"), ref item.ShadowSoftness, 0.1f, 0f, 256f, "%.1f")) OvRuntime.Changed();
            }
            if (ImGui.DragFloat(L("字间距", "Letter spacing"), ref item.LetterSpacing, 0.1f, -100f, 500f, "%.1f")) OvRuntime.Changed();
            if (ImGui.DragFloat(L("行高偏移", "Line height offset"), ref item.LineHeightOffset, 0.1f, -100f, 500f, "%.1f")) OvRuntime.Changed();
        }

        private static void DrawTextAnimation(OvTextComponent item)
        {
            DrawSectionTitle(L("节点动画", "Node animation"));
            if (ImGui.Button(L("打开 Token 节点编辑器", "Open token node editor"))) OvTokenNodeEditor.Open(item);
            ImGui.SameLine();
            ImGui.TextDisabled(item.TokenAnimation != null && item.TokenAnimation.Enabled ? L("已启用", "Enabled") : L("未启用", "Disabled"));
        }

        private void DrawImages(OvSettings settings)
        {
            DrawPageIntro(L("图片组件", "Image components"), L("图片资源、缩放、旋转和节点动画将作为独立渲染对象管理。", "Images are separate render objects with their own scale, rotation and node animation."));
            DrawList(settings.Images, ref _selectedImage, () => new OvImageComponent { Name = L("图片 ", "Image ") + (settings.Images.Count + 1) });
            if (!TrySelected(settings.Images, ref _selectedImage, out OvImageComponent item)) return;
            DrawCommon(item);
            DrawCategoryTabs("image", ref _imageCategory, L("布局", "Layout"), L("外观", "Style"), L("节点动画", "Node animation"));
            if (_imageCategory == 0) DrawLayout(item);
            else if (_imageCategory == 1) DrawImageAppearance(item);
            else DrawImageAnimation(item);
        }

        private static void DrawImageAppearance(OvImageComponent item)
        {
            DrawSectionTitle(L("图片内容", "Image content"));
            string path = item.ImagePath ?? string.Empty;
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.InputText(L("图片路径", "Image path"), ref path, 1024)) { item.ImagePath = path; OvRuntime.Changed(); }
            if (ImGui.IsItemDeactivatedAfterEdit()) OvRuntime.Changed(true);
            if (ImGui.DragFloat(L("缩放", "Scale"), ref item.Scale, 0.01f, 0.01f, 20f, "%.2fx")) OvRuntime.Changed();
            if (ImGui.DragFloat(L("旋转", "Rotation"), ref item.Rotation, 0.2f, -3600f, 3600f, "%.1f°")) OvRuntime.Changed();
        }

        private static void DrawImageAnimation(OvImageComponent item)
        {
            DrawSectionTitle(L("节点动画", "Node animation"));
            if (ImGui.Button(L("打开图片节点编辑器", "Open image node editor"))) OvTokenNodeEditor.Open(item);
            ImGui.SameLine();
            ImGui.TextDisabled(item.NodeAnimation != null && item.NodeAnimation.Enabled ? L("已启用", "Enabled") : L("未启用", "Disabled"));
        }

        private void DrawVideos(OvSettings settings)
        {
            DrawPageIntro(L("视频组件", "Video components"), L("视频解码与 RenderTexture 生命周期会沿用 KV 已验证的安全路径。", "Video decoding and RenderTexture handling follow the same safe path as KV."));
            DrawList(settings.Videos, ref _selectedVideo, () => new OvVideoComponent { Name = L("视频 ", "Video ") + (settings.Videos.Count + 1) });
            if (!TrySelected(settings.Videos, ref _selectedVideo, out OvVideoComponent item)) return;
            DrawCommon(item);
            DrawCategoryTabs("video", ref _videoCategory, L("布局", "Layout"), L("外观", "Style"));
            if (_videoCategory == 0) DrawLayout(item);
            else DrawVideoAppearance(item);
        }

        private static void DrawVideoAppearance(OvVideoComponent item)
        {
            DrawSectionTitle(L("视频内容", "Video content"));
            string path = item.VideoPath ?? string.Empty;
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.InputText(L("视频路径", "Video path"), ref path, 1024)) { item.VideoPath = path; OvRuntime.Changed(); }
            if (ImGui.IsItemDeactivatedAfterEdit()) OvRuntime.Changed(true);
            if (ImGui.Checkbox(L("循环播放", "Loop"), ref item.Loop)) OvRuntime.Changed();
            if (DragPair(L("尺寸", "Size"), ref item.Width, ref item.Height, 1f, 4096f)) OvRuntime.Changed();
            if (ImGui.DragFloat(L("内容缩放", "Content scale"), ref item.ContentScale, 0.01f, 0.01f, 20f, "%.2fx")) OvRuntime.Changed();
            if (DragPair(L("内容偏移", "Content offset"), ref item.ContentOffsetX, ref item.ContentOffsetY, -4000f, 4000f)) OvRuntime.Changed();
            if (ImGui.DragFloat(L("旋转", "Rotation"), ref item.Rotation, 0.2f, -3600f, 3600f, "%.1f°")) OvRuntime.Changed();
        }

        private void DrawProgress(OvSettings settings)
        {
            DrawPageIntro(L("进度条组件", "Progress bars"), L("数值源将统一使用 Tag 数值解析，不再维护第二套重复状态读取逻辑。", "Values come from tag parsing, the same as text components."));
            DrawList(settings.ProgressBars, ref _selectedProgress, () => new OvProgressComponent { Name = L("进度条 ", "Progress bar ") + (settings.ProgressBars.Count + 1) });
            if (!TrySelected(settings.ProgressBars, ref _selectedProgress, out OvProgressComponent item)) return;
            DrawCommon(item);
            DrawCategoryTabs("progress", ref _progressCategory, L("布局", "Layout"), L("外观", "Style"));
            if (_progressCategory == 0) DrawLayout(item);
            else DrawProgressAppearance(item);
        }

        private static void DrawProgressAppearance(OvProgressComponent item)
        {
            DrawSectionTitle(L("进度与样式", "Value and style"));
            string tag = item.ValueTag ?? string.Empty;
            if (ImGui.InputText(L("数值 Tag", "Value tag"), ref tag, 128)) { item.ValueTag = tag; OvRuntime.Changed(); }
            float min = (float)item.Minimum;
            float max = (float)item.Maximum;
            if (DragPair(L("数值范围", "Value range"), ref min, ref max, -1000000f, 1000000f)) { item.Minimum = min; item.Maximum = max; OvRuntime.Changed(); }
            if (DragPair(L("尺寸", "Size"), ref item.Width, ref item.Height, 1f, 4096f)) OvRuntime.Changed();
            int direction = (int)item.FillDirection;
            if (ImGui.Combo(L("填充方向", "Fill direction"), ref direction, L("从左到右\0从右到左\0从下到上\0从上到下\0", "Left to right\0Right to left\0Bottom to top\0Top to bottom\0")))
            { item.FillDirection = (OvProgressFillDirection)direction; OvRuntime.Changed(); }
            if (ColorEdit(L("背景颜色", "Background color"), item.BackgroundColor)) OvRuntime.Changed();
            if (ColorEdit(L("填充颜色", "Fill color"), item.FillColor)) OvRuntime.Changed();
            if (ColorEdit(L("边框颜色", "Border color"), item.BorderColor)) OvRuntime.Changed();
            if (ImGui.DragFloat(L("边框粗细", "Border thickness"), ref item.BorderThickness, 0.1f, 0f, 64f, "%.1f")) OvRuntime.Changed();
            if (ImGui.DragFloat(L("圆角", "Corner radius"), ref item.CornerRadius, 0.1f, 0f, 512f, "%.1f")) OvRuntime.Changed();
        }

        private static void DrawSettings(OvSettings settings)
        {
            DrawPageIntro(L("OV 设置", "OV settings"), L("全局开关、刷新调度与后续烘焙策略均由 Overlayer 模块独立维护。", "Global switches and refresh settings for the Overlayer module."));
            if (ImGui.Checkbox(L("启用 Overlayer", "Enable Overlayer"), ref settings.Enabled)) OvRuntime.Changed();
            if (ImGui.Checkbox(L("全部组件仅游玩时显示", "All components only while playing"), ref settings.OnlyShowPlaying)) OvRuntime.Changed();
            if (ImGui.Checkbox(L("允许在游戏画面拖动组件", "Allow dragging components on screen"), ref settings.EditMode)) OvRuntime.Changed();
            float rate = settings.DataUpdateRate;
            if (SonnetWidgets.SliderFloat(L("数值刷新率", "Value refresh rate"), ref rate, 15f, 360f, "%.0f FPS")) { settings.DataUpdateRate = rate; OvRuntime.Changed(); }
            ImGui.Spacing();
            ImGui.TextColored(new NumVector4(1f, 0.76f, 0.34f, 1f), L("迁移状态", "Migration status"));
            ImGui.BulletText(L("模块数据层与设置持久化：已接入", "Module data and settings storage: done"));
            ImGui.BulletText(L("UGUI 文本 / 图片 / 视频 / 进度条渲染：已接入", "UGUI text / image / video / progress rendering: done"));
            ImGui.BulletText(L("基础 Tag 运行时：已接入", "Basic tag runtime: done"));
            ImGui.BulletText(L("完整旧版 Tag 与兼容别名：已接入", "Full legacy tags and aliases: done"));
            ImGui.BulletText(L("Token / 图片节点编辑器：已接入", "Token / image node editors: done"));
            ImGui.Spacing();
            ImGui.TextColored(SonnetPalette.Accent(1f), L("配置导入导出", "Import and export"));
            if (ImGui.Button(L("导出 OV 配置 (.ctov)", "Export OV settings (.ctov)")))
            {
                ExportOverlayerPackage(settings);
            }
            ImGui.SameLine();
            if (ImGui.Button(L("导入 OV 配置 (.ctov/.cyt)", "Import OV settings (.ctov/.cyt)")))
            {
                ImportOverlayerPackage(settings);
            }
            if (!string.IsNullOrEmpty(OvPackageService.LastStatus))
            {
                ImGui.TextWrapped(SonnetWidgets.Plain(OvPackageService.LastStatus));
            }
        }

        private static void ExportOverlayerPackage(OvSettings settings)
        {
            try
            {
                string defaultName = OvPackageService.SafeName("CheryTools_Overlayer") + ".ctov";
                string path = ModernFileDialog.ShowSaveFileDialog(
                    L("导出 Overlayer 配置", "Export Overlayer settings"),
                    L("CheryTools Overlayer 配置 (*.ctov)|*.ctov", "CheryTools Overlayer settings (*.ctov)|*.ctov"),
                    OvPackageService.GameRoot(),
                    defaultName);

                if (!string.IsNullOrEmpty(path))
                {
                    if (OvPackageService.Write(settings, path)) OvRuntime.Changed();
                }
            }
            catch (Exception ex)
            {
                OvPackageService.LastStatus = L("导出异常: ", "Export error: ") + ex.Message;
            }
        }

        private static void ImportOverlayerPackage(OvSettings settings)
        {
            try
            {
                string path = ModernFileDialog.ShowOpenFileDialog(
                    L("导入 Overlayer 配置", "Import Overlayer settings"),
                    L("CheryTools Overlayer 配置 (*.ctov;*.cyt;*.xml)|*.ctov;*.cyt;*.xml|CheryTools 完整配置包 (*.cyt)|*.cyt|CheryTools Overlayer 配置包 (*.ctov)|*.ctov|XML 配置文件 (*.xml)|*.xml|所有文件 (*.*)|*.*", "CheryTools Overlayer settings (*.ctov;*.cyt;*.xml)|*.ctov;*.cyt;*.xml|CheryTools full package (*.cyt)|*.cyt|CheryTools Overlayer package (*.ctov)|*.ctov|XML settings (*.xml)|*.xml|All files (*.*)|*.*"),
                    OvPackageService.GameRoot());

                if (!string.IsNullOrEmpty(path))
                {
                    bool legacy;
                    if (OvPackageService.Read(settings, path, out legacy))
                    {
                        OvRuntime.Changed(true);
                    }
                }
            }
            catch (Exception ex)
            {
                OvPackageService.LastStatus = L("导入异常: ", "Import error: ") + ex.Message;
            }
        }

        private static void DrawCommon(OvComponent item)
        {
            DrawSectionTitle(L("组件与显示", "Component and visibility"));
            string name = item.Name ?? string.Empty;
            ImGui.SetNextItemWidth(Math.Min(520f, ImGui.GetContentRegionAvail().X));
            if (ImGui.InputText(L("组件名称", "Component name"), ref name, 96)) { item.Name = name; OvRuntime.Changed(); }
            if (ImGui.Checkbox(L("启用组件", "Enable component"), ref item.Enabled)) OvRuntime.Changed();
            ImGui.SameLine();
            if (ImGui.Checkbox(L("游戏内显示", "Show in game"), ref item.ShowInGame)) OvRuntime.Changed();
            ImGui.SameLine();
            if (ImGui.Checkbox(L("仅游玩时显示", "Only while playing"), ref item.OnlyShowPlaying)) OvRuntime.Changed();
        }

        private static void DrawLayout(OvComponent item)
        {
            DrawSectionTitle(L("布局", "Layout"));
            if (DragPair(L("位置", "Position"), ref item.PositionX, ref item.PositionY, -10000f, 10000f)) OvRuntime.Changed();
            if (DragPair(L("轴心", "Pivot"), ref item.PivotX, ref item.PivotY, 0f, 1f, 0.01f)) OvRuntime.Changed();
            ImGui.SetNextItemWidth(Math.Min(520f, ImGui.GetContentRegionAvail().X));
            if (SonnetWidgets.SliderFloat(L("透明度", "Opacity"), ref item.Opacity, 0f, 1f, "%.2f")) OvRuntime.Changed();
            ImGui.SetNextItemWidth(Math.Min(520f, ImGui.GetContentRegionAvail().X));
            if (ImGui.DragInt(L("渲染深度", "Render depth"), ref item.Depth, 1f, -1000, 1000)) OvRuntime.Changed();
            DrawQuickAlignment(item);
        }

        private static void DrawCategoryTabs(string id, ref int selected, params string[] labels)
        {
            float available = ImGui.GetContentRegionAvail().X;
            float gap = 8f;
            float width = Math.Min(132f, Math.Max(70f, (available - gap * (labels.Length - 1)) / labels.Length));
            NumVector2 origin = ImGui.GetCursorScreenPos();
            ImDrawListPtr draw = ImGui.GetWindowDrawList();
            for (int i = 0; i < labels.Length; i++)
            {
                if (i > 0) ImGui.SameLine(0f, gap);
                NumVector2 min = ImGui.GetCursorScreenPos();
                NumVector2 size = new NumVector2(width, 34f);
                if (ImGui.InvisibleButton("##OvCategory_" + id + i, size)) selected = i;
                if (ImGui.IsItemHovered())
                    draw.AddRectFilled(min, min + size,
                        ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, 0.035f)), 5f);
                NumVector2 textSize = ImGui.CalcTextSize(labels[i]);
                draw.AddText(min + new NumVector2((width - textSize.X) * 0.5f, (34f - textSize.Y) * 0.5f),
                    ImGui.GetColorU32(selected == i
                        ? SonnetPalette.Text
                        : SonnetPalette.Muted), labels[i]);
                if (selected == i)
                    draw.AddLine(min + new NumVector2(18f, 33f), min + new NumVector2(width - 18f, 33f),
                        ImGui.GetColorU32(SonnetPalette.Accent(1f)), 2f);
            }
            ImGui.SetCursorScreenPos(new NumVector2(origin.X, ImGui.GetCursorScreenPos().Y + 8f));
            ImGui.Separator();
        }

        private static void DrawPageIntro(string title, string description)
        {
            ImGui.TextColored(SonnetPalette.Text, SonnetWidgets.Plain(title));
            ImGui.TextDisabled(SonnetWidgets.Plain(description));
            ImGui.Spacing();
        }

        private static void DrawList<T>(List<T> values, ref int selected, Func<T> factory) where T : OvComponent
        {
            if (selected >= values.Count) selected = Math.Max(0, values.Count - 1);
            string preview = values.Count > 0 && selected >= 0 && selected < values.Count
                ? values[selected]?.Name ?? L("未命名", "Unnamed") : L("尚未创建组件", "No components yet");
            float selectorWidth = Math.Min(360f, Math.Max(160f, ImGui.GetContentRegionAvail().X - 190f));
            ImGui.SetNextItemWidth(selectorWidth);
            if (ImGui.BeginCombo("##OvComponentSelector", preview))
            {
                for (int i = 0; i < values.Count; i++)
                {
                    T value = values[i];
                    bool active = selected == i;
                    if (ImGui.Selectable((value?.Name ?? L("未命名", "Unnamed")) + "##" + (value?.Id ?? i.ToString()), active)) selected = i;
                    if (active) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
            ImGui.SameLine();
            if (ImGui.Button(L("+ 新建", "+ New"))) { values.Add(factory()); selected = values.Count - 1; OvRuntime.Changed(true); }
            ImGui.SameLine();
            bool canDelete = values.Count > 0;
            if (!canDelete) ImGui.BeginDisabled();
            if (ImGui.Button(L("删除", "Delete")) && canDelete) { values.RemoveAt(selected); selected = Math.Max(0, selected - 1); OvRuntime.Changed(true); }
            if (!canDelete) ImGui.EndDisabled();
        }

        private static bool TrySelected<T>(List<T> values, ref int selected, out T item) where T : OvComponent
        {
            if (values.Count == 0) { item = null; ImGui.TextDisabled(L("尚未创建组件。", "No components yet.")); return false; }
            selected = Math.Max(0, Math.Min(selected, values.Count - 1));
            item = values[selected];
            return item != null;
        }

        private static bool DragPair(string label, ref float left, ref float right, float min, float max, float speed = 1f)
        {
            ImGui.PushID(label);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(label);
            ImGui.SameLine(112f);
            float available = ImGui.GetContentRegionAvail().X;
            float width = Math.Max(86f, (available - 42f) * 0.5f);
            ImGui.SetNextItemWidth(width);
            bool changed = ImGui.DragFloat("X##Value", ref left, speed, min, max, "%.2f");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(width);
            changed = ImGui.DragFloat("Y##Value", ref right, speed, min, max, "%.2f") || changed;
            ImGui.PopID();
            return changed;
        }

        private static bool ColorEdit(string label, float[] value)
        {
            if (value == null || value.Length < 4) return false;
            var color = new NumVector4(value[0], value[1], value[2], value[3]);
            ImGui.SetNextItemWidth(Math.Min(520f, ImGui.GetContentRegionAvail().X));
            if (!ImGui.ColorEdit4(label, ref color, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreviewHalf)) return false;
            value[0] = color.X;
            value[1] = color.Y;
            value[2] = color.Z;
            value[3] = color.W;
            return true;
        }

        private static void DrawSectionTitle(string title)
        {
            ImGui.Spacing();
            ImGui.SeparatorText(title);
        }

        private static void DrawQuickAlignment(OvComponent item)
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(L("快速对齐", "Quick align"));
            ImGui.SameLine(112f);
            ImGui.PushID("OvAlign_" + (item.Id ?? string.Empty));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new NumVector2(4f, 4f));
            NumVector2 buttonSize = new NumVector2(30f, 26f);
            bool changed = false;
            for (int i = 0; i < 9; i++)
            {
                if (i % 3 != 0) ImGui.SameLine();
                else if (i > 0) ImGui.SetCursorPosX(112f);
                if (ImGui.InvisibleButton("##" + i, buttonSize))
                {
                    AlignComponent(item, i);
                    changed = true;
                }
                NumVector2 buttonMin = ImGui.GetItemRectMin();
                NumVector2 buttonMax = ImGui.GetItemRectMax();
                bool hovered = ImGui.IsItemHovered();
                bool active = ImGui.IsItemActive();
                ImGui.GetWindowDrawList().AddRectFilled(buttonMin, buttonMax,
                    ImGui.GetColorU32(active ? SonnetPalette.Accent(0.3f)
                        : hovered ? SonnetPalette.Accent(0.16f)
                        : new NumVector4(0.02f, 0.04f, 0.07f, 0.50f)), 6f);
                DrawAlignmentGlyph(buttonMin, buttonMax, i,
                    hovered ? SonnetPalette.Accent()
                    : SonnetPalette.Muted);
            }
            ImGui.PopStyleVar();
            ImGui.PopID();
            if (changed) OvRuntime.Changed();
        }

        private static void DrawAlignmentGlyph(NumVector2 min, NumVector2 max, int index, NumVector4 color)
        {
            ImDrawListPtr draw = ImGui.GetWindowDrawList();
            NumVector2 center = (min + max) * 0.5f;
            if (index == 4)
            {
                draw.AddCircleFilled(center, 3f, ImGui.GetColorU32(color), 12);
                return;
            }

            int column = index % 3;
            int row = index / 3;
            NumVector2 direction = new NumVector2(column - 1f, row - 1f);
            float length = direction.Length();
            if (length > 0.001f) direction /= length;
            NumVector2 tip = center + direction * 7f;
            NumVector2 tail = center - direction * 5f;
            uint packed = ImGui.GetColorU32(color);
            draw.AddLine(tail, tip, packed, 1.8f);

            NumVector2 perpendicular = new NumVector2(-direction.Y, direction.X);
            NumVector2 headBase = tip - direction * 4f;
            draw.AddLine(tip, headBase + perpendicular * 3f, packed, 1.8f);
            draw.AddLine(tip, headBase - perpendicular * 3f, packed, 1.8f);
        }

        private static void AlignComponent(OvComponent item, int index)
        {
            int column = index % 3;
            int row = index / 3;
            const float margin = 20f;
            item.PositionX = column == 0 ? margin : column == 1 ? Screen.width * 0.5f : Screen.width - margin;
            item.PositionY = row == 0 ? margin : row == 1 ? Screen.height * 0.5f : Screen.height - margin;
            item.PivotX = column * 0.5f;
            item.PivotY = row * 0.5f;
            if (item is OvTextComponent text) text.Alignment = column;
        }

        private void DrawTagInsertPopup(OvTextComponent item)
        {
            ImGui.SetNextWindowSize(new NumVector2(560f, 430f), ImGuiCond.Appearing);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new NumVector2(18f, 16f));
            if (!ImGui.BeginPopup("##OvTagInsertPopup"))
            {
                ImGui.PopStyleVar();
                return;
            }
            ImGui.TextColored(SonnetPalette.Text, L("插入 Tag", "Insert tag"));
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint("##OvTagSearch", L("搜索 Tag 或说明…", "Search tags or descriptions..."), ref _tagSearch, 96);
            ImGui.Spacing();
            string query = (_tagSearch ?? string.Empty).Trim();
            const float tagColumnWidth = 175f;
            const float descriptionColumnWidth = 245f;
            const float actionColumnWidth = 80f;
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new NumVector2(12f, 7f));
            if (ImGui.BeginTable("##OvTagTable", 3,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY
                | ImGuiTableFlags.SizingFixedFit,
                new NumVector2(-1f, 340f)))
            {
                ImGui.TableSetupColumn("Tag", ImGuiTableColumnFlags.WidthFixed, tagColumnWidth);
                ImGui.TableSetupColumn(L("说明", "Description"), ImGuiTableColumnFlags.WidthFixed, descriptionColumnWidth);
                ImGui.TableSetupColumn(L("操作", "Action"), ImGuiTableColumnFlags.WidthFixed, actionColumnWidth);
                ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
                ImGui.TableSetColumnIndex(0); TableCenteredText("Tag", tagColumnWidth);
                ImGui.TableSetColumnIndex(1); TableCenteredText(L("说明", "Description"), descriptionColumnWidth);
                ImGui.TableSetColumnIndex(2); TableCenteredText(L("操作", "Action"), actionColumnWidth);
                for (int i = 0; i < TagInsertItems.Length; i++)
                {
                    TagInsertItem candidate = TagInsertItems[i];
                    if (query.Length > 0
                        && candidate.Tag.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0
                        && candidate.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    TableCenteredText(candidate.Tag, tagColumnWidth);
                    ImGui.TableNextColumn();
                    TableCenteredText(candidate.Description, descriptionColumnWidth);
                    ImGui.TableNextColumn();
                    float buttonWidth = ImGui.CalcTextSize(L("插入", "Insert")).X + 22f;
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f,
                        (actionColumnWidth - 24f - buttonWidth) * 0.5f));
                    if (ImGui.SmallButton(L("插入##", "Insert##") + i))
                    {
                        string current = item.TextFormat ?? string.Empty;
                        bool placeInside = candidate.Tag.StartsWith("<") && candidate.Tag.EndsWith(">");
                        OvRichTextEditor.InsertAtCursor("OvInlineTextFormat_" + item.Id, ref current, candidate.Tag, placeInside);
                        item.TextFormat = current;
                        OvRuntime.Changed();
                    }
                }
                ImGui.EndTable();
            }
            ImGui.PopStyleVar();
            ImGui.EndPopup();
            ImGui.PopStyleVar();
        }

        private static void TableCenteredText(string value, float columnWidth)
        {
            string text = value ?? string.Empty;
            float textWidth = ImGui.CalcTextSize(text).X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (columnWidth - 24f - textWidth) * 0.5f));
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(text);
        }

        private unsafe int CaptureTextCursor(ImGuiInputTextCallbackData* data)
        {
            if (data != null) _textCursorUtf8Offset = Math.Max(0, data->CursorPos);
            return 0;
        }

        private static int Utf8OffsetToUtf16Index(string value, int byteOffset)
        {
            if (string.IsNullOrEmpty(value) || byteOffset <= 0) return 0;
            int utf16Index = 0;
            int utf8Count = 0;
            while (utf16Index < value.Length)
            {
                int charCount = char.IsHighSurrogate(value[utf16Index])
                    && utf16Index + 1 < value.Length && char.IsLowSurrogate(value[utf16Index + 1]) ? 2 : 1;
                int nextBytes = Encoding.UTF8.GetByteCount(value.Substring(utf16Index, charCount));
                if (utf8Count + nextBytes > byteOffset) break;
                utf8Count += nextBytes;
                utf16Index += charCount;
            }
            return utf16Index;
        }
    }
}
