using System;
using System.Collections.Generic;
using CheryTools.Sonnet.Contracts;
using ImGuiNET;
using UnityEngine;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;
using CheryTools.Sonnet.Shared;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.KeyViewer.Preview
{
    public sealed class KeyViewerPreviewModule : ICheryModule, ICheryModuleOverlay
    {
        private const string ProfilePrefix = "profile:";
        private const string SettingsTab = "settings";
        private static readonly KeyCode[] KeyCandidates = BuildKeyCandidates();
        private readonly Dictionary<string, int> _editorPage = new Dictionary<string, int>(StringComparer.Ordinal);
        private string _waitingProfileId;
        private string _waitingKeyId;
        private string _deleteProfileId;

        public ModuleMetadata Metadata { get; } = new ModuleMetadata(
            "keyviewer.preview", "KeyViewer", "Preview 0.1.0", "CheryTools", new NumVector4(0.39f, 0.80f, 1f, 1f))
        {
            NavigationTitle = "KV 配置",
            NavigationTitleEn = "KV Profiles"
        };

        public IReadOnlyList<ModuleTab> Tabs
        {
            get
            {
                var tabs = new List<ModuleTab>();
                KvSettings settings = KvRuntime.Settings;
                if (settings?.Profiles != null)
                    for (int i = 0; i < settings.Profiles.Count; i++)
                    {
                        KvProfile profile = settings.Profiles[i];
                        if (profile != null) tabs.Add(new ModuleTab(ProfilePrefix + profile.Id, profile.Name));
                    }
                tabs.Add(new ModuleTab(SettingsTab, "KV 设置", "KV Settings"));
                return tabs;
            }
        }

        public void Initialize(ModuleContext context) => KvRuntime.Initialize(context);
        public void Update() => KvRuntime.Tick();
        public void Shutdown() => KvRuntime.Shutdown();

        public void Draw(ModuleUiContext context)
        {
            KvSettings settings = KvRuntime.Settings;
            if (settings == null) return;
            CaptureWaitingKey(settings);
            if (string.Equals(context.ActiveTabId, SettingsTab, StringComparison.Ordinal))
            {
                DrawGlobalSettings(settings);
            }
            else
            {
                string profileId = context.ActiveTabId != null && context.ActiveTabId.StartsWith(ProfilePrefix, StringComparison.Ordinal)
                    ? context.ActiveTabId.Substring(ProfilePrefix.Length)
                    : string.Empty;
                KvProfile profile = settings.FindProfile(profileId) ?? (settings.Profiles.Count > 0 ? settings.Profiles[0] : null);
                if (profile != null) DrawProfile(settings, profile);
            }
        }

        public bool HasOpenOverlay => KvFreeMakeEditor.IsOpen;

        public void DrawOverlay()
        {
            KvFreeMakeEditor.Draw(KvRuntime.Settings);
        }

        private void DrawProfile(KvSettings settings, KvProfile profile)
        {
            DrawProfileToolbar(settings, profile);
            int page;
            if (!_editorPage.TryGetValue(profile.Id, out page)) page = 0;
            DrawSegmentedEditor(profile.Id, ref page);
            _editorPage[profile.Id] = page;
            switch (page)
            {
                case 1: DrawAppearance(profile); break;
                case 2: DrawRain(profile); break;
                default: DrawLayout(profile); break;
            }
        }

        private void DrawProfileToolbar(KvSettings settings, KvProfile profile)
        {
            bool enabled = profile.Enabled;
            if (ImGui.Checkbox(L("启用此配置##profile_enabled", "Enable profile##profile_enabled"), ref enabled))
            {
                profile.Enabled = enabled;
                KvRuntime.Changed(false);
            }
            ImGui.SameLine();
            bool showInGame = profile.ShowInGame;
            if (ImGui.Checkbox(L("游戏内显示##profile_ingame", "Show in game##profile_ingame"), ref showInGame))
            {
                profile.ShowInGame = showInGame;
                KvRuntime.Changed(false);
            }
            ImGui.SameLine();
            bool onlyPlaying = profile.OnlyShowPlaying;
            if (ImGui.Checkbox(L("仅游玩时显示##profile_playing", "Only while playing##profile_playing"), ref onlyPlaying))
            {
                profile.OnlyShowPlaying = onlyPlaying;
                KvRuntime.Changed(false);
            }
            ImGui.Spacing();
        }

        private static void DrawSegmentedEditor(string id, ref int selected)
        {
            string[] labels = { L("布局", "Layout"), L("外观", "Style"), L("键雨", "Key rain") };
            float available = ImGui.GetContentRegionAvail().X;
            float width = Mathf.Min(132f, (available - 16f) / labels.Length);
            ImDrawListPtr draw = ImGui.GetWindowDrawList();
            for (int i = 0; i < labels.Length; i++)
            {
                if (i > 0) ImGui.SameLine(0f, 8f);
                NumVector2 min = ImGui.GetCursorScreenPos();
                if (ImGui.InvisibleButton("##KvEditorTab_" + id + "_" + i, new NumVector2(width, 34f))) selected = i;
                bool hovered = ImGui.IsItemHovered();
                if (hovered) draw.AddRectFilled(min, min + new NumVector2(width, 34f), ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, 0.035f)), 5f);
                NumVector2 text = ImGui.CalcTextSize(labels[i]);
                draw.AddText(min + new NumVector2((width - text.X) * 0.5f, (34f - text.Y) * 0.5f),
                    ImGui.GetColorU32(selected == i ? SonnetPalette.Text : SonnetPalette.Muted), labels[i]);
                if (selected == i) draw.AddLine(min + new NumVector2(18f, 33f), min + new NumVector2(width - 18f, 33f),
                    ImGui.GetColorU32(SonnetPalette.Accent(1f)), 2f);
            }
            ImGui.Dummy(new NumVector2(1f, 8f));
        }

        private void DrawLayout(KvProfile profile)
        {
            DrawCard("##KvLayoutGeneral", 202f, () =>
            {
                SectionTitle(L("配置与位置", "Profile and position"), L("布局坐标以屏幕中心为原点", "Coordinates are relative to the screen center"));
                string name = profile.Name ?? string.Empty;
                ImGui.SetNextItemWidth(Mathf.Min(360f, ImGui.GetContentRegionAvail().X));
                if (ImGui.InputText(L("配置名称", "Profile name"), ref name, 64)) { profile.Name = name; KvRuntime.Changed(false); }
                float x = profile.OffsetX;
                float half = Mathf.Max(80f, (ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(L("位置 X", "Position X")).X
                    - ImGui.CalcTextSize(L("位置 Y", "Position Y")).X - ImGui.GetStyle().ItemInnerSpacing.X * 2f - ImGui.GetStyle().ItemSpacing.X - 4f) * 0.5f);
                ImGui.SetNextItemWidth(half);
                if (ImGui.DragFloat(L("位置 X", "Position X"), ref x, 1f, -4000f, 4000f, "%.0f")) { profile.OffsetX = x; KvRuntime.Changed(false); }
                ImGui.SameLine();
                float y = profile.OffsetY;
                ImGui.SetNextItemWidth(half);
                if (ImGui.DragFloat(L("位置 Y", "Position Y"), ref y, 1f, -4000f, 4000f, "%.0f")) { profile.OffsetY = y; KvRuntime.Changed(false); }
                float scale = profile.Scale;
                ImGui.SetNextItemWidth(Mathf.Min(420f, ImGui.GetContentRegionAvail().X));
                if (SonnetWidgets.SliderFloat(L("整体缩放", "Scale"), ref scale, 0.1f, 4f, "%.2fx")) { profile.Scale = scale; KvRuntime.Changed(false); }
            });

            // The card grows with the key list so the table rarely scrolls. The table takes whatever
            // height is left, so the card itself never needs a second, outer scrollbar.
            float keyRowHeight = ImGui.GetFrameHeight() + ImGui.GetStyle().CellPadding.Y * 2f + 1f;
            float keysCardHeight = Mathf.Clamp((profile.Keys.Count + 1) * keyRowHeight + 124f, 300f, 680f);
            DrawCard("##KvKeys", keysCardHeight, () =>
            {
                SectionTitle(L("按键", "Keys"), profile.Keys.Count + L(" 个组件", " components"));
                string openLabel = L("打开 FreeMake", "Open FreeMake");
                string importLabel = L("导入配置", "Import");
                string exportLabel = L("导出配置", "Export");
                string resetLabel = L("重置数据", "Reset stats");
                string statsLabel = L("包含统计数据", "Include statistics");
                if (SonnetWidgets.FitButton(openLabel, 128f, 0f)) KvFreeMakeEditor.Open(profile);
                SonnetWidgets.FlowNext(SonnetWidgets.FitWidth(importLabel, 90f));
                if (SonnetWidgets.FitButton(importLabel, 90f, 0f)) KvPackageService.Import(KvRuntime.Settings, profile);
                SonnetWidgets.FlowNext(SonnetWidgets.FitWidth(exportLabel, 90f));
                if (SonnetWidgets.FitButton(exportLabel, 90f, 0f)) KvPackageService.ExportProfile(profile);
                SonnetWidgets.FlowNext(SonnetWidgets.FitWidth(resetLabel, 90f));
                if (SonnetWidgets.FitButton(resetLabel, 90f, 0f))
                {
                    profile.TotalHits = 0;
                    profile.CurrentKps = 0;
                    foreach (KvKey key in profile.Keys) key.HitCount = 0;
                    KvRuntime.Changed(false);
                }
                SonnetWidgets.FlowNext(ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X + ImGui.CalcTextSize(statsLabel).X);
                bool includeStats = KvRuntime.Settings.PackageIncludeStats;
                if (ImGui.Checkbox(statsLabel, ref includeStats))
                {
                    KvRuntime.Settings.PackageIncludeStats = includeStats;
                    KvRuntime.Changed(false);
                }
                ImGui.Spacing();
                if (ImGui.BeginTable("KvKeyTable", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new NumVector2(0f, -1f)))
                {
                    ImGui.TableSetupScrollFreeze(0, 1);
                    ImGui.TableSetupColumn(L("类型 / 绑定", "Type / key"), ImGuiTableColumnFlags.WidthFixed, 110f);
                    ImGui.TableSetupColumn(L("显示文字", "Label"), ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn(L("键雨", "Key rain"), ImGuiTableColumnFlags.WidthFixed, 54f);
                    ImGui.TableSetupColumn(L("计数", "Count"), ImGuiTableColumnFlags.WidthFixed, 70f);
                    ImGui.TableSetupColumn(L("操作", "Actions"), ImGuiTableColumnFlags.WidthFixed, 64f);
                    ImGui.TableHeadersRow();
                    for (int i = 0; i < profile.Keys.Count; i++)
                    {
                        KvKey key = profile.Keys[i];
                        ImGui.PushID(key.Id);
                        ImGui.TableNextRow();

                        ImGui.TableSetColumnIndex(0);
                        bool waiting = string.Equals(_waitingProfileId, profile.Id, StringComparison.Ordinal) && string.Equals(_waitingKeyId, key.Id, StringComparison.Ordinal);
                        string bindLabel = key.NodeType == 1 ? "KPS" : key.NodeType == 2 ? L("总计数", "Total")
                            : waiting ? L("按下按键…", "Press a key...") : (key.NodeType == 3 ? L("图 · ", "Img · ") : key.NodeType == 4 ? L("视 · ", "Vid · ") : string.Empty) + KvSettings.KeyName(key.Bind);
                        if (ImGui.Button(bindLabel, new NumVector2(-1f, 0f)) && KvSettings.IsInputNode(key))
                        {
                            _waitingProfileId = profile.Id;
                            _waitingKeyId = key.Id;
                        }

                        ImGui.TableSetColumnIndex(1);
                        if (KvSettings.IsMediaNode(key))
                        {
                            ImGui.AlignTextToFramePadding();
                            ImGui.TextDisabled(key.NodeType == 3 ? L("图片按键", "Image key") : L("视频按键", "Video key"));
                        }
                        else
                        {
                            string label = key.Label ?? string.Empty;
                            ImGui.SetNextItemWidth(-1f);
                            if (ImGui.InputText("##Label", ref label, 24)) { key.Label = label; KvRuntime.Changed(false); }
                        }

                        ImGui.TableSetColumnIndex(2);
                        bool rain = key.RainEnabled;
                        if (KvSettings.IsInputNode(key) && ImGui.Checkbox("##Rain", ref rain))
                        {
                            key.RainEnabled = rain;
                            key.UseCustomRain = true;
                            KvRuntime.Changed(false);
                        }

                        ImGui.TableSetColumnIndex(3);
                        ImGui.AlignTextToFramePadding();
                        ImGui.TextUnformatted(key.HitCount.ToString());

                        ImGui.TableSetColumnIndex(4);
                        ImGui.AlignTextToFramePadding();
                        if (ImGui.SmallButton(L("删除", "Delete")))
                        {
                            profile.Keys.RemoveAt(i--);
                            KvRuntime.Changed();
                        }
                        ImGui.PopID();
                    }
                    ImGui.EndTable();
                }
            });
        }

        private static void DrawAppearance(KvProfile profile)
        {
            DrawCard("##KvGeometry", 244f, () =>
            {
                SectionTitle(L("几何", "Geometry"), L("所有尺寸在整体缩放前计算", "Sizes are before the overall scale"));
                if (DragPair(L("按键尺寸", "Key size"), ref profile.KeyWidth, ref profile.KeyHeight, 1f, 400f)) KvRuntime.Changed(false);
                float gap = profile.Gap;
                ImGui.SetNextItemWidth(260f);
                if (ImGui.DragFloat(L("按键间距", "Key gap"), ref gap, 0.5f, 0f, 160f, "%.1f")) { profile.Gap = gap; KvRuntime.Changed(false); }
                float radius = profile.CornerRadius;
                ImGui.SetNextItemWidth(260f);
                if (SonnetWidgets.SliderFloat(L("圆角", "Corner radius"), ref radius, 0f, 80f, "%.1f")) { profile.CornerRadius = radius; KvRuntime.Changed(false); }
                float border = profile.BorderThickness;
                ImGui.SetNextItemWidth(260f);
                if (SonnetWidgets.SliderFloat(L("边框", "Border"), ref border, 0f, 16f, "%.1f")) { profile.BorderThickness = border; KvRuntime.Changed(false); }
                if (DragPair(L("文字大小", "Text size"), ref profile.LabelSize, ref profile.CountSize, 8f, 120f)) KvRuntime.Changed(false);
                bool hide = profile.HideCount;
                if (ImGui.Checkbox(L("隐藏按键计数", "Hide key counts"), ref hide)) { profile.HideCount = hide; KvRuntime.Changed(false); }
                ImGui.SameLine();
                ImGui.TextDisabled(L("KPS 与总计数现为独立组件，可在布局页或 FreeMake 中管理", "KPS and Total are separate components; manage them on the Layout page or in FreeMake"));
            });

            DrawCard("##KvFont", 160f, () =>
            {
                SectionTitle(L("字体", "Font"), L("配置内所有节点默认使用；FreeMake 可为单个节点覆盖", "Default for every node in the profile; FreeMake can override it per node"));
                string fontPath = profile.FontPath ?? string.Empty;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.InputTextWithHint("##KvProfileFont", L("留空使用游戏默认字体", "Leave empty for the game font"), ref fontPath, 1024))
                {
                    profile.FontPath = fontPath;
                    KvRuntime.Changed(false);
                }
                if (ImGui.IsItemDeactivatedAfterEdit()) KvRuntime.Changed();
                if (SonnetWidgets.FitButton(L("选择字体", "Choose font"), 104f, 0f))
                {
                    string picked = ModernFileDialog.ShowOpenFileDialog(L("选择 KV 字体", "Choose KV font"),
                        L("字体文件 (*.ttf;*.otf)|*.ttf;*.otf|所有文件 (*.*)|*.*", "Font files (*.ttf;*.otf)|*.ttf;*.otf|All files (*.*)|*.*"),
                        ExistingDirectory(fontPath));
                    if (!string.IsNullOrEmpty(picked)) { profile.FontPath = picked; KvRuntime.Changed(); }
                }
                ImGui.SameLine();
                if (SonnetWidgets.FitButton(L("恢复默认", "Reset"), 104f, 0f) && !string.IsNullOrEmpty(profile.FontPath))
                {
                    profile.FontPath = string.Empty;
                    KvRuntime.Changed();
                }
                ImGui.TextDisabled(L("支持 TTF / OTF；字体变更会重建一次 KV UGUI 文本。", "TTF / OTF supported; changing the font rebuilds the KV text once."));
            });

            DrawCard("##KvColors", 210f, () =>
            {
                SectionTitle(L("颜色", "Colors"), L("普通状态与按下状态分开设置", "Normal and pressed states are set separately"));
                if (ImGui.BeginTable("KvColorTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX))
                {
                    ImGui.TableSetupColumn(L("项目", "Item"), ImGuiTableColumnFlags.WidthFixed, 80f);
                    ImGui.TableSetupColumn(L("普通状态", "Normal"), ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn(L("按下状态", "Pressed"), ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableHeadersRow();
                    ColorRow(L("背景", "Background"), "bg", profile.BackgroundNormal, profile.BackgroundPressed);
                    ColorRow(L("边框", "Border"), "border", profile.BorderNormal, profile.BorderPressed);
                    ColorRow(L("文字", "Text"), "text", profile.TextNormal, profile.TextPressed);
                    ImGui.EndTable();
                }
            });

            float textEffectsHeight = 124f
                + (profile.LabelOutlineEnabled || profile.CountOutlineEnabled ? 72f : 0f)
                + (profile.LabelShadowEnabled || profile.CountShadowEnabled ? 108f : 0f)
                + (profile.LabelShadowEnabled ? 30f : 0f)
                + (profile.CountShadowEnabled ? 30f : 0f);
            DrawCard("##KvTextEffects", textEffectsHeight, () =>
            {
                SectionTitle(L("文字效果", "Text effects"), L("按键文字与计数文字可分别设置", "Key labels and counts are set separately"));
                bool labelOutline = profile.LabelOutlineEnabled;
                if (ImGui.Checkbox(L("按键文字描边", "Label outline"), ref labelOutline)) { profile.LabelOutlineEnabled = labelOutline; KvRuntime.Changed(false); }
                ImGui.SameLine();
                bool countOutline = profile.CountOutlineEnabled;
                if (ImGui.Checkbox(L("计数文字描边", "Count outline"), ref countOutline)) { profile.CountOutlineEnabled = countOutline; KvRuntime.Changed(false); }
                if (labelOutline || countOutline)
                {
                    if (ColorEdit(L("按键描边颜色", "Label outline color"), "profile_label_outline", profile.LabelOutlineColor)) KvRuntime.Changed(false);
                    ImGui.SameLine();
                    if (ColorEdit(L("计数描边颜色", "Count outline color"), "profile_count_outline", profile.CountOutlineColor)) KvRuntime.Changed(false);
                    if (DragPair(L("描边粗细", "Outline thickness"), ref profile.LabelOutlineThickness, ref profile.CountOutlineThickness, 0f, 8f)) KvRuntime.Changed(false);
                }
                ImGui.Spacing();
                bool labelShadow = profile.LabelShadowEnabled;
                if (ImGui.Checkbox(L("按键文字阴影", "Label shadow"), ref labelShadow)) { profile.LabelShadowEnabled = labelShadow; KvRuntime.Changed(false); }
                ImGui.SameLine();
                bool countShadow = profile.CountShadowEnabled;
                if (ImGui.Checkbox(L("计数文字阴影", "Count shadow"), ref countShadow)) { profile.CountShadowEnabled = countShadow; KvRuntime.Changed(false); }
                if (labelShadow || countShadow)
                {
                    if (ColorEdit(L("按键阴影颜色", "Label shadow color"), "profile_label_shadow", profile.LabelShadowColor)) KvRuntime.Changed(false);
                    ImGui.SameLine();
                    if (ColorEdit(L("计数阴影颜色", "Count shadow color"), "profile_count_shadow", profile.CountShadowColor)) KvRuntime.Changed(false);
                    if (DragPairUnbounded(L("按键阴影偏移", "Label shadow offset"), ref profile.LabelShadowOffset[0], ref profile.LabelShadowOffset[1])) KvRuntime.Changed(false);
                    if (profile.LabelShadowEnabled && DragFloatUnbounded(L("按键阴影柔度", "Label shadow softness"), ref profile.LabelShadowSoftness, true)) KvRuntime.Changed(false);
                    if (DragPairUnbounded(L("计数阴影偏移", "Count shadow offset"), ref profile.CountShadowOffset[0], ref profile.CountShadowOffset[1])) KvRuntime.Changed(false);
                    if (profile.CountShadowEnabled && DragFloatUnbounded(L("计数阴影柔度", "Count shadow softness"), ref profile.CountShadowSoftness, true)) KvRuntime.Changed(false);
                }
            });

            DrawCard("##KvPressAnimation", 252f, () =>
            {
                SectionTitle(L("按键动画", "Press animation"), L("按下与松开共享同一条缓动曲线", "Press and release share one easing curve"));
                bool enabled = profile.PressAnimationEnabled;
                if (ImGui.Checkbox(L("启用按键动画", "Enable press animation"), ref enabled)) { profile.PressAnimationEnabled = enabled; KvRuntime.Changed(false); }
                float duration = profile.PressAnimationDuration;
                ImGui.SetNextItemWidth(360f);
                if (SonnetWidgets.SliderFloat(L("动画时长", "Duration"), ref duration, 0.01f, 2f, "%.2f s")) { profile.PressAnimationDuration = duration; KvRuntime.Changed(false); }
                float scale = profile.PressAnimationScale;
                ImGui.SetNextItemWidth(360f);
                if (SonnetWidgets.SliderFloat(L("按下缩放", "Pressed scale"), ref scale, 0.2f, 2f, "%.2fx")) { profile.PressAnimationScale = scale; KvRuntime.Changed(false); }
                if (DragPair(L("按下偏移", "Pressed offset"), ref profile.PressAnimationOffsetX, ref profile.PressAnimationOffsetY, -200f, 200f)) KvRuntime.Changed(false);
                bool colors = profile.PressAnimationAffectColors;
                if (ImGui.Checkbox(L("颜色同步过渡", "Animate colors"), ref colors)) { profile.PressAnimationAffectColors = colors; KvRuntime.Changed(false); }
                int easing = EasingIndex(profile.PressAnimationEasing);
                ImGui.SetNextItemWidth(360f);
                if (ImGui.Combo(L("缓动", "Easing"), ref easing, L("线性\0缓出二次\0缓入二次\0缓入缓出二次\0缓出三次\0缓入缓出三次\0回弹缓出\0", "Linear\0Ease out quad\0Ease in quad\0Ease in-out quad\0Ease out cubic\0Ease in-out cubic\0Ease out back\0")))
                { profile.PressAnimationEasing = EasingName(easing); KvRuntime.Changed(false); }
            });
        }

        private static void DrawRain(KvProfile profile)
        {
            DrawCard("##KvRainGeneral", 440f, () =>
            {
                SectionTitle(L("键雨", "Key rain"), L("按住时生长，松开后继续向上离场；支持四排独立图层与深度", "Grows while held and rises away after release; four rows with their own layer and depth"));
                bool enabled = profile.RainEnabled;
                if (ImGui.Checkbox(L("启用键雨", "Enable key rain"), ref enabled)) { profile.RainEnabled = enabled; KvRuntime.Changed(false); }
                float speed = profile.RainSpeed;
                ImGui.SetNextItemWidth(Mathf.Min(420f, ImGui.GetContentRegionAvail().X));
                if (SonnetWidgets.SliderFloat(L("速度", "Speed"), ref speed, 50f, 3000f, "%.0f px/s")) { profile.RainSpeed = speed; KvRuntime.Changed(false); }
                float height = profile.RainMaxHeight;
                ImGui.SetNextItemWidth(Mathf.Min(420f, ImGui.GetContentRegionAvail().X));
                if (SonnetWidgets.SliderFloat(L("最大高度", "Max height"), ref height, 40f, 1800f, "%.0f px")) { profile.RainMaxHeight = height; KvRuntime.Changed(false); }

                float ratio = profile.RainWidthRatio;
                ImGui.SetNextItemWidth(Mathf.Min(420f, ImGui.GetContentRegionAvail().X));
                if (SonnetWidgets.SliderFloat(L("第一排宽度比例", "Row 1 width"), ref ratio, 0.05f, 2f, "%.2fx")) { profile.RainWidthRatio = ratio; KvRuntime.Changed(false); }
                float ratio2 = profile.RainWidthRatio2;
                ImGui.SetNextItemWidth(Mathf.Min(420f, ImGui.GetContentRegionAvail().X));
                if (SonnetWidgets.SliderFloat(L("第二排宽度比例", "Row 2 width"), ref ratio2, 0.05f, 2f, "%.2fx")) { profile.RainWidthRatio2 = ratio2; KvRuntime.Changed(false); }
                float ratio3 = profile.RainWidthRatio3;
                ImGui.SetNextItemWidth(Mathf.Min(420f, ImGui.GetContentRegionAvail().X));
                if (SonnetWidgets.SliderFloat(L("第三排宽度比例", "Row 3 width"), ref ratio3, 0.05f, 2f, "%.2fx")) { profile.RainWidthRatio3 = ratio3; KvRuntime.Changed(false); }
                float ratio4 = profile.RainWidthRatio4;
                ImGui.SetNextItemWidth(Mathf.Min(420f, ImGui.GetContentRegionAvail().X));
                if (SonnetWidgets.SliderFloat(L("第四排宽度比例", "Row 4 width"), ref ratio4, 0.05f, 2f, "%.2fx")) { profile.RainWidthRatio4 = ratio4; KvRuntime.Changed(false); }

                if (DragPair(L("第 1 / 2 排高度偏移", "Row 1 / 2 height offset"), ref profile.RainYOffset, ref profile.RainYOffset2, -400f, 400f)) KvRuntime.Changed(false);
                if (DragPair(L("第 3 / 4 排高度偏移", "Row 3 / 4 height offset"), ref profile.RainYOffset3, ref profile.RainYOffset4, -400f, 400f)) KvRuntime.Changed(false);

                // 四排独立深度
                int d1 = profile.RainDepth1, d2 = profile.RainDepth2, d3 = profile.RainDepth3, d4 = profile.RainDepth4;
                ImGui.TextDisabled(L("各排独立渲染深度 (Depth)", "Render depth per row"));
                ImGui.SetNextItemWidth(90f);
                if (ImGui.DragInt(L("排1深度", "Row 1 depth"), ref d1, 1f, -1000, 1000)) { profile.RainDepth1 = d1; KvRuntime.Changed(); }
                ImGui.SameLine();
                ImGui.SetNextItemWidth(90f);
                if (ImGui.DragInt(L("排2深度", "Row 2 depth"), ref d2, 1f, -1000, 1000)) { profile.RainDepth2 = d2; KvRuntime.Changed(); }
                ImGui.SameLine();
                ImGui.SetNextItemWidth(90f);
                if (ImGui.DragInt(L("排3深度", "Row 3 depth"), ref d3, 1f, -1000, 1000)) { profile.RainDepth3 = d3; KvRuntime.Changed(); }
                ImGui.SameLine();
                ImGui.SetNextItemWidth(90f);
                if (ImGui.DragInt(L("排4深度", "Row 4 depth"), ref d4, 1f, -1000, 1000)) { profile.RainDepth4 = d4; KvRuntime.Changed(); }

                float radius = profile.RainCornerRadius;
                ImGui.SetNextItemWidth(Mathf.Min(420f, ImGui.GetContentRegionAvail().X));
                if (SonnetWidgets.SliderFloat(L("键雨圆角", "Rain corner radius"), ref radius, 0f, 80f, "%.1f")) { profile.RainCornerRadius = radius; KvRuntime.Changed(false); }
            });
            float rainColorsHeight = 310f
                + (profile.RainHorizontalGradientEnabled ? 144f : 0f)
                + (profile.RainGradientEnabled ? 36f : 0f)
                + (profile.RainGradientEnabled && profile.RainGradientMode == 1 ? 36f : 0f)
                + (profile.RainFadeMode == 1 ? 36f : 0f);
            DrawCard("##KvRainColors", rainColorsHeight, () =>
            {
                SectionTitle(L("颜色", "Colors"), L("四排键雨独立色彩，第2排固定在第1排之上，第3/4排依次递增", "Each row has its own colors; row 2 draws above row 1, rows 3 and 4 above that"));
                bool gradient = profile.RainGradientEnabled;
                if (ImGui.Checkbox(L("纵向渐变", "Vertical gradient"), ref gradient)) { profile.RainGradientEnabled = gradient; KvRuntime.Changed(false); }
                ImGui.SameLine();
                bool horizontal = profile.RainHorizontalGradientEnabled;
                if (ImGui.Checkbox(L("横向渐变", "Horizontal gradient"), ref horizontal)) { profile.RainHorizontalGradientEnabled = horizontal; KvRuntime.Changed(false); }
                if (gradient)
                {
                    int gradientMode = profile.RainGradientMode;
                    ImGui.SetNextItemWidth(300f);
                    if (ImGui.Combo(L("纵向渐变方式", "Vertical gradient mode"), ref gradientMode, L("随雨段渐变（UV）\0按高度渐变\0", "Per segment (UV)\0By height\0")))
                    { profile.RainGradientMode = gradientMode; KvRuntime.Changed(false); }
                    if (gradientMode == 1 && DragPair(L("渐变范围 / 曲线", "Gradient range / curve"), ref profile.RainGradientHeight, ref profile.RainGradientPower, 0.05f, 5f))
                        KvRuntime.Changed(false);
                }
                if (ColorEdit(L("第一排近端", "Row 1 near"), "rain_near", profile.RainColor)) KvRuntime.Changed(false);
                ImGui.SameLine();
                if (ColorEdit(L("第一排远端", "Row 1 far"), "rain_far", profile.RainEndColor)) KvRuntime.Changed(false);
                if (horizontal && ColorEdit(L("第一排右侧", "Row 1 right"), "rain_right", profile.RainRightColor)) KvRuntime.Changed(false);

                if (ColorEdit(L("第二排近端", "Row 2 near"), "rain2_near", profile.RainColor2)) KvRuntime.Changed(false);
                ImGui.SameLine();
                if (ColorEdit(L("第二排远端", "Row 2 far"), "rain2_far", profile.RainEndColor2)) KvRuntime.Changed(false);
                if (horizontal && ColorEdit(L("第二排右侧", "Row 2 right"), "rain2_right", profile.RainRightColor2)) KvRuntime.Changed(false);

                if (ColorEdit(L("第三排近端", "Row 3 near"), "rain3_near", profile.RainColor3)) KvRuntime.Changed(false);
                ImGui.SameLine();
                if (ColorEdit(L("第三排远端", "Row 3 far"), "rain3_far", profile.RainEndColor3)) KvRuntime.Changed(false);
                if (horizontal && ColorEdit(L("第三排右侧", "Row 3 right"), "rain3_right", profile.RainRightColor3)) KvRuntime.Changed(false);

                if (ColorEdit(L("第四排近端", "Row 4 near"), "rain4_near", profile.RainColor4)) KvRuntime.Changed(false);
                ImGui.SameLine();
                if (ColorEdit(L("第四排远端", "Row 4 far"), "rain4_far", profile.RainEndColor4)) KvRuntime.Changed(false);
                if (horizontal && ColorEdit(L("第四排右侧", "Row 4 right"), "rain4_right", profile.RainRightColor4)) KvRuntime.Changed(false);

                int fade = profile.RainFadeMode;
                ImGui.SetNextItemWidth(300f);
                if (ImGui.Combo(L("消失方式", "Fade out"), ref fade, L("高度裁剪\0羽化透明\0", "Clip at height\0Feather\0"))) { profile.RainFadeMode = fade; KvRuntime.Changed(false); }
                if (fade == 1 && DragPair(L("羽化范围 / 程度", "Feather range / strength"), ref profile.RainFadeHeight, ref profile.RainFadePower, 0.05f, 5f)) KvRuntime.Changed(false);
            });
            float rainEffectsHeight = 116f
                + (profile.RainOutlineEnabled ? 72f : 0f)
                + (profile.RainShadowEnabled ? 144f : 0f);
            DrawCard("##KvRainEffects", rainEffectsHeight, () =>
            {
                SectionTitle(L("键雨效果", "Rain effects"), L("阴影与外部描边", "Shadow and outline"));
                bool outline = profile.RainOutlineEnabled;
                if (ImGui.Checkbox(L("启用描边", "Outline"), ref outline)) { profile.RainOutlineEnabled = outline; KvRuntime.Changed(false); }
                if (outline)
                {
                    if (ColorEdit(L("描边颜色", "Outline color"), "rain_outline", profile.RainOutlineColor)) KvRuntime.Changed(false);
                    float thickness = profile.RainOutlineThickness;
                    if (SonnetWidgets.SliderFloat(L("描边粗细", "Outline thickness"), ref thickness, 0f, 10f, "%.1f")) { profile.RainOutlineThickness = thickness; KvRuntime.Changed(false); }
                }
                bool shadow = profile.RainShadowEnabled;
                if (ImGui.Checkbox(L("启用阴影", "Shadow"), ref shadow)) { profile.RainShadowEnabled = shadow; KvRuntime.Changed(false); }
                if (shadow)
                {
                    if (ColorEdit(L("阴影颜色", "Shadow color"), "rain_shadow", profile.RainShadowColor)) KvRuntime.Changed(false);
                    if (DragPairUnbounded(L("阴影偏移", "Shadow offset"), ref profile.RainShadowOffset[0], ref profile.RainShadowOffset[1])) KvRuntime.Changed(false);
                    float softness = profile.RainShadowSoftness;
                    ImGui.SetNextItemWidth(300f);
                    if (SonnetWidgets.SliderFloat(L("阴影柔度", "Shadow softness"), ref softness, 0f, 64f, "%.1f")) { profile.RainShadowSoftness = softness; KvRuntime.Changed(false); }
                    float strength = profile.RainShadowStrength;
                    ImGui.SetNextItemWidth(300f);
                    if (SonnetWidgets.SliderFloat(L("阴影强度", "Shadow strength"), ref strength, 0f, 1f, "%.2f")) { profile.RainShadowStrength = strength; KvRuntime.Changed(false); }
                }
            });
        }

        private void DrawGlobalSettings(KvSettings settings)
        {
            DrawCard("##KvGlobal", 206f, () =>
            {
                SectionTitle(L("运行", "General"), L("这些选项影响全部 KV 配置", "These options affect every KV profile"));
                bool enabled = settings.Enabled;
                if (ImGui.Checkbox(L("启用 KeyViewer", "Enable KeyViewer"), ref enabled)) { settings.Enabled = enabled; KvRuntime.Changed(false); }
                bool only = settings.OnlyShowPlaying;
                if (ImGui.Checkbox(L("全部配置仅游玩时显示", "All profiles only while playing"), ref only)) { settings.OnlyShowPlaying = only; KvRuntime.Changed(false); }
                bool clear = settings.ClearStatsOnLaunch;
                if (ImGui.Checkbox(L("每次启动清空计数", "Clear counts on every launch"), ref clear)) { settings.ClearStatsOnLaunch = clear; KvRuntime.Changed(false); }
                float window = settings.KpsWindowSeconds;
                ImGui.SetNextItemWidth(360f);
                if (SonnetWidgets.SliderFloat(L("KPS 统计窗口", "KPS window"), ref window, 0.25f, 3f, "%.2f s")) { settings.KpsWindowSeconds = window; KvRuntime.Changed(false); }
                if (SonnetWidgets.FitButton(L("清空全部计数", "Clear all counts"), 136f, 0f))
                {
                    settings.ClearCounts();
                    KvRuntime.Changed(false);
                }
            });

            float profilesCardHeight = Mathf.Clamp(120f + settings.Profiles.Count * 36f, 156f, 360f);
            DrawCard("##KvProfiles", profilesCardHeight, () =>
            {
                SectionTitle(L("配置管理", "Profiles"), settings.Profiles.Count + L(" 个配置", " profiles"));
                if (SonnetWidgets.FitButton(L("新建配置", "New profile"), 112f, 0f))
                {
                    settings.Profiles.Add(KvSettings.CreateProfile(L("配置 ", "Profile ") + (settings.Profiles.Count + 1)));
                    KvRuntime.Changed();
                }
                ImGui.SameLine();
                if (SonnetWidgets.FitButton(L("导入配置 (.ctkv/.cyt)", "Import (.ctkv/.cyt)"), 150f, 0f))
                    KvPackageService.Import(settings, settings.Profiles.Count > 0 ? settings.Profiles[0] : null);
                ImGui.SameLine();
                if (SonnetWidgets.FitButton(L("导出全部", "Export all"), 112f, 0f)) KvPackageService.ExportAll(settings);
                ImGui.SameLine();
                ImGui.TextDisabled(L("导入只会追加，不会删除现有内容", "Importing only adds; nothing existing is removed"));
                ImGui.Spacing();
                if (ImGui.BeginTable("KvProfilesTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX))
                {
                    ImGui.TableSetupColumn(L("配置名称", "Profile name"), ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn(L("按键组件", "Keys"), ImGuiTableColumnFlags.WidthFixed, 90f);
                    ImGui.TableSetupColumn(L("击键总计", "Total presses"), ImGuiTableColumnFlags.WidthFixed, 100f);
                    ImGui.TableSetupColumn(L("操作", "Actions"), ImGuiTableColumnFlags.WidthFixed, 130f);
                    ImGui.TableHeadersRow();
                    for (int i = 0; i < settings.Profiles.Count; i++)
                    {
                        KvProfile profile = settings.Profiles[i];
                        ImGui.PushID(profile.Id);
                        ImGui.TableNextRow();

                        ImGui.TableSetColumnIndex(0);
                        ImGui.AlignTextToFramePadding();
                        ImGui.TextUnformatted(profile.Name);

                        ImGui.TableSetColumnIndex(1);
                        ImGui.AlignTextToFramePadding();
                        ImGui.TextUnformatted(profile.Keys.Count + L(" 个", ""));

                        ImGui.TableSetColumnIndex(2);
                        ImGui.AlignTextToFramePadding();
                        ImGui.TextUnformatted(profile.TotalHits.ToString());

                        ImGui.TableSetColumnIndex(3);
                        ImGui.AlignTextToFramePadding();
                        if (ImGui.SmallButton(L("复制", "Copy")))
                        {
                            KvProfile copy = KvGeometry.CloneProfile(profile);
                            copy.Name = profile.Name + L(" 副本", " copy");
                            settings.Profiles.Insert(i + 1, copy);
                            KvRuntime.Changed();
                            ImGui.PopID();
                            break;
                        }
                        ImGui.SameLine(0f, 6f);
                        bool confirming = string.Equals(_deleteProfileId, profile.Id, StringComparison.Ordinal);
                        string deleteLabel = confirming ? L("确认删除", "Confirm") : L("删除", "Delete");
                        if (ImGui.SmallButton(deleteLabel))
                        {
                            if (confirming)
                            {
                                settings.Profiles.RemoveAt(i);
                                if (settings.Profiles.Count == 0)
                                {
                                    settings.Profiles.Add(KvSettings.CreateProfile(L("默认配置", "Default profile")));
                                }
                                _deleteProfileId = null;
                                KvRuntime.Changed();
                                KvRuntime.InvalidateVisuals();
                                ImGui.PopID();
                                break;
                            }
                            _deleteProfileId = profile.Id;
                        }
                        ImGui.PopID();
                    }
                    ImGui.EndTable();
                }
            });
            if (!string.IsNullOrEmpty(KvPackageService.LastStatus)) ImGui.TextWrapped(SonnetWidgets.Plain(KvPackageService.LastStatus));
        }

        private void CaptureWaitingKey(KvSettings settings)
        {
            if (string.IsNullOrEmpty(_waitingProfileId) || string.IsNullOrEmpty(_waitingKeyId)) return;
            for (int i = 0; i < KeyCandidates.Length; i++)
            {
                KeyCode code = KeyCandidates[i];
                if (!Input.GetKeyDown(code)) continue;
                if (code == KeyCode.Escape)
                {
                    _waitingProfileId = _waitingKeyId = null;
                    return;
                }
                KvProfile profile = settings.FindProfile(_waitingProfileId);
                KvKey key = profile?.Keys.Find(item => item != null && string.Equals(item.Id, _waitingKeyId, StringComparison.Ordinal));
                if (key != null)
                {
                    key.Bind = code;
                    key.Label = KvSettings.KeyName(code);
                    KvRuntime.InputChanged();
                }
                _waitingProfileId = _waitingKeyId = null;
                return;
            }
        }

        private static void DrawCard(string id, float height, Action content)
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new NumVector4(0.012f, 0.018f, 0.028f, 0.68f));
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 10f);
            ImGui.BeginChild(id, new NumVector2(0f, height), ImGuiChildFlags.None);
            // Advance through padding using normal layout flow. Manual cursor
            // positioning can make ImGui think the child extends its parent.
            ImGui.Dummy(new NumVector2(1f, 14f));
            ImGui.Indent(18f);
            float innerWidth = Mathf.Max(1f, ImGui.GetContentRegionAvail().X - 18f);
            float innerHeight = Mathf.Max(1f, height - 28f);
            ImGui.BeginChild(id + "_inner", new NumVector2(innerWidth, innerHeight), ImGuiChildFlags.None,
                ImGuiWindowFlags.NoBackground | (id == "##KvKeys" ? ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse : ImGuiWindowFlags.None));
            content();
            ImGui.EndChild();
            ImGui.Unindent(18f);
            ImGui.EndChild();
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
            ImGui.Spacing();
        }


        private static void SectionTitle(string title, string description)
        {
            ImGui.TextColored(SonnetPalette.Text, SonnetWidgets.Plain(title));
            if (!string.IsNullOrEmpty(description))
            {
                ImGui.SameLine();
                ImGui.TextDisabled(SonnetWidgets.Plain(description));
            }
            ImGui.Separator();
            ImGui.Spacing();
        }

        private static string ExistingDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    string directory = System.IO.Directory.Exists(path) ? path : System.IO.Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory) && System.IO.Directory.Exists(directory)) return directory;
                }
            }
            catch { }
            return Environment.CurrentDirectory;
        }

        private static bool DragPair(string label, ref float first, ref float second, float min, float max)
        {
            float width = Mathf.Max(100f, (ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(label).X - 34f) * 0.5f);
            float speed = SonnetWidgets.DragSpeed(min, max);
            string format = SonnetWidgets.DragFormat(min, max);
            ImGui.SetNextItemWidth(width);
            bool changed = ImGui.DragFloat("##" + label + "_x", ref first, speed, min, max, format);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(width);
            changed |= ImGui.DragFloat(label, ref second, speed, min, max, format);
            return changed;
        }

        private static bool DragPairUnbounded(string label, ref float first, ref float second)
        {
            float width = Mathf.Max(100f, (ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(label).X - 34f) * 0.5f);
            ImGui.SetNextItemWidth(width);
            bool changed = ImGui.DragFloat("##" + label + "_x", ref first, 0.5f, 0f, 0f, "%.1f");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(width);
            changed |= ImGui.DragFloat(label, ref second, 0.5f, 0f, 0f, "%.1f");
            return changed;
        }

        private static bool DragFloatUnbounded(string label, ref float value, bool nonNegative)
        {
            bool changed = ImGui.DragFloat(label, ref value, 0.5f, 0f, 0f, "%.1f");
            if (changed && nonNegative && value < 0f) value = 0f;
            return changed;
        }

        private static void ColorRow(string label, string id, float[] normal, float[] pressed)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(label);
            ImGui.TableNextColumn(); if (ColorEdit("##normal_" + id, "normal_" + id, normal)) KvRuntime.Changed(false);
            ImGui.TableNextColumn(); if (ColorEdit("##pressed_" + id, "pressed_" + id, pressed)) KvRuntime.Changed(false);
        }

        private static bool ColorEdit(string label, string id, float[] value)
        {
            var color = new NumVector4(value[0], value[1], value[2], value[3]);
            ImGui.PushID(id);
            ImGui.SetNextItemWidth(-1f);
            bool changed = ImGui.ColorEdit4(label, ref color, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreviewHalf);
            ImGui.PopID();
            if (changed) { value[0] = color.X; value[1] = color.Y; value[2] = color.Z; value[3] = color.W; }
            return changed;
        }

        private static uint ColorU32(float[] value)
        {
            return ImGui.GetColorU32(new NumVector4(value[0], value[1], value[2], value[3]));
        }

        private static KvKey CreateMediaNode(int nodeType)
        {
            return KvSettings.CreateMedia(nodeType);
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

        private static KeyCode[] BuildKeyCandidates()
        {
            Array values = Enum.GetValues(typeof(KeyCode));
            var result = new List<KeyCode>();
            for (int i = 0; i < values.Length; i++)
            {
                KeyCode key = (KeyCode)values.GetValue(i);
                int code = (int)key;
                if (key != KeyCode.None && (code < 323 || code > 329)) result.Add(key);
            }
            return result.ToArray();
        }
    }
}
