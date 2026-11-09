using System;
using System.Collections.Generic;
using CheryTools.Sonnet.Contracts;
using ImGuiNET;
using UnityEngine;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;
using CheryTools.Sonnet.Shared;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.Tools.Preview
{
    public sealed class ToolsPreviewModule : ICheryModule
    {
        private static readonly IReadOnlyList<ModuleTab> ModuleTabs = new List<ModuleTab>
        {
            new ModuleTab("optimization", "优化", "Optimization"),
            new ModuleTab("editor", "编辑器", "Editor"),
            new ModuleTab("interface", "界面", "Interface"),
            new ModuleTab("planet", "星球", "Planets")
        };

        private static readonly KeyCode[] KeyCandidates = BuildKeyCandidates();
        private bool _waitingForKey;

        public ModuleMetadata Metadata { get; } = new ModuleMetadata(
            "tools.preview", "Tools", "Preview 0.1.0", "CheryTools", new NumVector4(0.34f, 0.79f, 1f, 1f))
        {
            NavigationTitle = "功能列表",
            NavigationTitleEn = "Features"
        };

        public IReadOnlyList<ModuleTab> Tabs => ModuleTabs;

        public void Initialize(ModuleContext context) => ToolsRuntime.Initialize(context);
        public void Update() => ToolsRuntime.Tick();

        public void Draw(ModuleUiContext context)
        {
            switch (context.ActiveTabId)
            {
                case "editor": DrawEditor(); break;
                case "interface": DrawInterface(); break;
                case "planet": DrawPlanet(); break;
                default: DrawOptimization(); break;
            }
        }

        public void Shutdown() => ToolsRuntime.Shutdown();

        private void DrawOptimization()
        {
            ToolsSettings settings = ToolsRuntime.Settings;
            DrawPageHeading(L("优化", "Optimization"));
            DrawSectionHeading(L("编辑器播放", "Editor playback"));
            bool disablePause = settings.DisableAutoplaySpacePause;
            if (ImGui.Checkbox(L("禁用自动播放空格暂停", "Disable Space pause during autoplay"), ref disablePause))
            {
                settings.DisableAutoplaySpacePause = disablePause;
                ToolsRuntime.SettingsChanged();
            }
            bool disableScroll = settings.DisablePlayModeScrollZoom;
            if (ImGui.Checkbox(L("禁用播放时滚轮缩放", "Disable scroll zoom during playback"), ref disableScroll))
            {
                settings.DisablePlayModeScrollZoom = disableScroll;
                ToolsRuntime.SettingsChanged();
            }

            DrawSectionHeading(L("输入", "Input"));
            bool antiBounce = settings.AntiBounceKeys;
            if (ImGui.Checkbox(L("防弹键", "Key debounce"), ref antiBounce))
            {
                settings.AntiBounceKeys = antiBounce;
                ToolsRuntime.SettingsChanged(true);
            }
            if (antiBounce)
            {
                float interval = settings.AntiBounceIntervalMs;
                ImGui.SetNextItemWidth(Math.Min(360f, ImGui.GetContentRegionAvail().X * 0.55f));
                if (SonnetWidgets.SliderFloat(L("防弹间隔", "Debounce interval"), ref interval, 1f, 50f, "%.0f ms"))
                {
                    settings.AntiBounceIntervalMs = interval;
                    ToolsRuntime.SettingsChanged(true);
                }
                ImGui.TextDisabled(L("推荐值: 10~20ms (过大易误吞高速连打而引发提前空敲)", "Recommended: 10-20 ms (too high can swallow fast repeated presses)"));
            }

            bool limit = settings.LimitInput;
            if (ImGui.Checkbox(L("启用独立按键限制", "Only accept listed keys"), ref limit))
            {
                settings.LimitInput = limit;
                ToolsRuntime.SettingsChanged(true);
            }
            DrawKeyList(settings);
        }

        private void DrawKeyList(ToolsSettings settings)
        {
            string label = _waitingForKey ? L("等待按键...", "Waiting for key...") : L("添加按键", "Add key");
            if (ImGui.Button(label, new NumVector2(132f, 0f))) _waitingForKey = true;
            ImGui.SameLine();
            ImGui.TextDisabled(settings.LimitedKeys.Count + " / " + ToolsSettings.MaxLimitedKeys);
            if (settings.LimitedKeys.Count > 0)
            {
                ImGui.SameLine();
                if (ImGui.Button(L("清空", "Clear")))
                {
                    settings.LimitedKeys.Clear();
                    ToolsRuntime.SettingsChanged(true);
                }
            }

            if (_waitingForKey)
            {
                for (int i = 0; i < KeyCandidates.Length; i++)
                {
                    KeyCode key = KeyCandidates[i];
                    if (!Input.GetKeyDown(key)) continue;
                    _waitingForKey = false;
                    if (settings.LimitedKeys.Count < ToolsSettings.MaxLimitedKeys && !settings.LimitedKeys.Contains(key))
                    {
                        settings.LimitedKeys.Add(key);
                        ToolsRuntime.SettingsChanged(true);
                    }
                    break;
                }
            }

            if (settings.LimitedKeys.Count == 0) return;
            ImGui.Spacing();
            if (BeginPaddedTable("##ToolsLimitedKeys", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 42f);
                ImGui.TableSetupColumn(L("按键", "Key"), ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn(L("操作", "Action"), ImGuiTableColumnFlags.WidthFixed, 72f);
                ImGui.TableHeadersRow();
                for (int i = 0; i < settings.LimitedKeys.Count; i++)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted((i + 1).ToString());
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(settings.LimitedKeys[i].ToString());
                    ImGui.TableNextColumn();
                    if (ImGui.SmallButton(L("删除##key_", "Delete##key_") + i))
                    {
                        settings.LimitedKeys.RemoveAt(i--);
                        ToolsRuntime.SettingsChanged(true);
                    }
                }
                EndPaddedTable();
            }
        }

        private static void DrawEditor()
        {
            ToolsEditorSettings editor = ToolsRuntime.Settings.Editor;
            DrawPageHeading(L("编辑器", "Editor"));
            ImGui.TextDisabled(L("在关卡编辑器中显示选区信息，并提供精细编辑辅助。", "Shows selection info in the level editor and adds fine editing helpers."));
            DrawSectionHeading(L("选区信息", "Selection info"));
            DrawSettingCheck(L("显示选中轨道角度", "Show selected tile angle"), ref editor.ShowFloorAngle);
            DrawSettingCheck(L("显示选中轨道拍数", "Show selected tile beats"), ref editor.ShowFloorBeats);
            DrawSettingCheck(L("显示选中轨道数量", "Show selected tile count"), ref editor.ShowFloorCount);
            DrawSettingCheck(L("显示选中轨道时长", "Show selected tile duration"), ref editor.ShowFloorDuration);
            DrawSettingCheck(L("使用 Tulttak 兼容计算方式", "Use Tulttak-compatible calculation"), ref editor.UseTulttakBehavior);

            DrawSectionHeading(L("旋转与事件", "Rotation and events"));
            DrawSettingCheck(L("启用精细旋转", "Fine tile rotation"), ref editor.FineTuneFloorRotations);
            float step = editor.FloorRotationStep;
            ImGui.SetNextItemWidth(Mathf.Min(280f, ImGui.GetContentRegionAvail().X));
            if (SonnetWidgets.SliderFloat(L("精细旋转步长", "Fine rotation step"), ref step, 0.1f, 180f, "%.1f°"))
            {
                editor.FloorRotationStep = step;
                ToolsRuntime.SettingsChanged();
            }
            DrawSettingCheck(L("旋转/翻转时同步轨道事件", "Sync tile events when rotating or flipping"), ref editor.SyncLevelEventValues);
            ImGui.TextDisabled(L("精细旋转快捷键：Ctrl + Alt + , / .", "Fine rotation shortcut: Ctrl + Alt + , / ."));
        }

        private static void DrawInterface()
        {
            ToolsSettings settings = ToolsRuntime.Settings;
            DrawPageHeading(L("界面", "Interface"));
            bool recording = settings.RecordingMode;
            if (ImGui.Checkbox(L("使用录制界面配置", "Use recording profile"), ref recording))
            {
                settings.RecordingMode = recording;
                ToolsRuntime.SettingsChanged();
            }
            ImGui.SameLine();
            ImGui.TextDisabled(recording ? L("当前：录制", "Active: recording") : L("当前：游玩", "Active: playing"));
            DrawVisibilityProfile(L("游玩界面", "Playing profile"), settings.PlayingUi, !recording);
            DrawVisibilityProfile(L("录制界面", "Recording profile"), settings.RecordingUi, recording);
        }

        private static void DrawVisibilityProfile(string title, ToolsUiVisibilitySettings profile, bool active)
        {
            ImGui.Spacing();
            // Both profiles intentionally contain the same controls. Keep their
            // ImGui IDs separate; otherwise ImGui reports duplicate IDs and edits
            // can affect the other profile or draw the item picker overlay.
            ImGui.PushID(title);
            if (!ImGui.CollapsingHeader(title + (active ? L("（当前）", " (active)") : ""), ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.PopID();
                return;
            }
            bool hide = profile.HideEverything;
            if (ImGui.Checkbox(L("隐藏全部界面", "Hide all UI"), ref hide)) { profile.HideEverything = hide; ToolsRuntime.SettingsChanged(); }
            ImGui.SameLine();
            if (ImGui.Button(L("恢复此配置", "Reset this profile")))
            {
                profile.HideEverything = false; profile.HideJudgment = false; profile.HideMissIndicators = false;
                profile.HideTitle = false; profile.HideOtto = false; profile.HideTimingTarget = false;
                profile.HideNoFailIcon = false; profile.HideBeta = false; profile.HideResult = false;
                profile.HideHitErrorMeter = false; profile.HideLastFloorFlash = false;
                ToolsUiVisibilityRuntime.Restore();
                ToolsRuntime.SettingsChanged();
            }
            DrawSectionHeading(L("隐藏项目", "Hidden elements"));
            DrawSettingCheck(L("判定文字", "Judgement text"), ref profile.HideJudgment);
            DrawSettingCheck(L("Miss 指示器", "Miss indicators"), ref profile.HideMissIndicators);
            DrawSettingCheck(L("关卡标题", "Level title"), ref profile.HideTitle);
            DrawSettingCheck(L("自动播放 / Otto", "Autoplay / Otto"), ref profile.HideOtto);
            DrawSettingCheck(L("难度与 Timing Target", "Difficulty and timing target"), ref profile.HideTimingTarget);
            DrawSettingCheck(L("No Fail 图标", "No Fail icon"), ref profile.HideNoFailIcon);
            DrawSettingCheck(L("Beta 提示", "Beta notice"), ref profile.HideBeta);
            DrawSettingCheck(L("结算文本", "Results text"), ref profile.HideResult);
            DrawSettingCheck(L("误差条", "Hit error meter"), ref profile.HideHitErrorMeter);
            DrawSettingCheck(L("终点闪白", "Final tile flash"), ref profile.HideLastFloorFlash);
            ImGui.PopID();
        }

        private static void DrawSettingCheck(string label, ref bool value)
        {
            bool next = value;
            if (ImGui.Checkbox(label, ref next))
            {
                value = next;
                ToolsRuntime.SettingsChanged();
            }
        }

        private static void DrawPlanet()
        {
            ToolsSettings settings = ToolsRuntime.Settings;
            DrawPageHeading(L("星球", "Planets"));
            DrawSectionHeading(L("判定文字", "Judgement text"));
            bool enabled = settings.HideHitTextEnabled;
            if (ImGui.Checkbox(L("启用判定文字隐藏", "Hide judgement text"), ref enabled))
            {
                settings.HideHitTextEnabled = enabled;
                ToolsRuntime.SettingsChanged();
            }
            if (enabled)
            {
                if (ImGui.Button(L("隐藏全部", "Hide all")))
                {
                    SetAllHitText(settings, true);
                    ToolsRuntime.SettingsChanged();
                }
                ImGui.SameLine();
                if (ImGui.Button(L("全部显示", "Show all")))
                {
                    SetAllHitText(settings, false);
                    ToolsRuntime.SettingsChanged();
                }
                DrawHitTextGrid(settings);
            }

            DrawSectionHeading(L("星球颜色", "Planet colors"));
            bool customColors = settings.EnableCustomPlanetColors;
            if (ImGui.Checkbox(L("启用自定义星球颜色", "Custom planet colors"), ref customColors))
            {
                settings.EnableCustomPlanetColors = customColors;
                ToolsRuntime.SettingsChanged(false, true);
            }
            if (customColors) DrawPlanetColors(settings);

            DrawSectionHeading(L("透明度", "Opacity"));
            bool opacity = settings.EnablePlanetOpacity;
            if (ImGui.Checkbox(L("启用星球透明度", "Custom planet opacity"), ref opacity))
            {
                settings.EnablePlanetOpacity = opacity;
                ToolsRuntime.SettingsChanged(false, true);
            }
            if (opacity) DrawPlanetOpacity(settings);
        }

        private static void DrawPlanetOpacity(ToolsSettings settings)
        {
            const float planetColumnWidth = 108f;
            const float valueColumnWidth = 390f;
            if (!BeginFixedDataTable("##PlanetOpacity", 4, planetColumnWidth, valueColumnWidth)) return;
            DrawTableHeader(L("星球", "Planets"), planetColumnWidth);
            DrawTableHeader(L("本体", "Body"), valueColumnWidth);
            DrawTableHeader(L("球环", "Ring"), valueColumnWidth);
            DrawTableHeader(L("拖尾", "Tail"), valueColumnWidth);
            DrawOpacityRow(L("火之行星", "Fire planet"), ref settings.RedPlanetOpacity, ref settings.RedRingOpacity, ref settings.RedTailOpacity);
            DrawOpacityRow(L("冰之行星", "Ice planet"), ref settings.BluePlanetOpacity, ref settings.BlueRingOpacity, ref settings.BlueTailOpacity);
            DrawOpacityRow(L("风之行星", "Wind planet"), ref settings.GreenPlanetOpacity, ref settings.GreenRingOpacity, ref settings.GreenTailOpacity);
            EndFixedDataTable();
        }

        private static void DrawOpacityRow(string name, ref float body, ref float ring, ref float tail)
        {
            // Table rows share one ID scope; without a per-row scope the three planets' sliders collide.
            ImGui.PushID(name);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            TableCenteredText(name, 108f);
            DrawOpacityCell("##body", ref body); DrawOpacityCell("##ring", ref ring); DrawOpacityCell("##tail", ref tail);
            ImGui.PopID();
        }

        private static void DrawOpacityCell(string id, ref float value)
        {
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1f);
            if (SonnetWidgets.SliderFloat(id, ref value, 0f, 100f, "%.0f%%")) ToolsRuntime.SettingsChanged(false, true);
        }

        private static void DrawHitTextGrid(ToolsSettings settings)
        {
            ImGui.Spacing();
            if (!BeginPaddedTable("##HitTextGrid", 3, ImGuiTableFlags.SizingStretchSame)) return;
            DrawHitToggle("Too Early", ref settings.HideTooEarly);
            DrawHitToggle("Very Early", ref settings.HideVeryEarly);
            DrawHitToggle("Early Perfect", ref settings.HideEarlyPerfect);
            DrawHitToggle("Perfect", ref settings.HidePerfect);
            DrawHitToggle("Late Perfect", ref settings.HideLatePerfect);
            DrawHitToggle("Very Late", ref settings.HideVeryLate);
            DrawHitToggle("Too Late", ref settings.HideTooLate);
            DrawHitToggle(L("多押", "Multipress"), ref settings.HideMultipress);
            DrawHitToggle("OverPress", ref settings.HideOverPress);
            DrawHitToggle(L("错过", "Miss"), ref settings.HideFailMiss);
            DrawHitToggle(L("按太快", "Overload"), ref settings.HideFailOverload);
            EndPaddedTable();
        }

        private static void DrawHitToggle(string label, ref bool value)
        {
            ImGui.TableNextColumn();
            bool changed = value;
            if (ImGui.Checkbox(label, ref changed))
            {
                value = changed;
                ToolsRuntime.SettingsChanged();
            }
        }

        private static void DrawPlanetColors(ToolsSettings settings)
        {
            ImGui.Spacing();
            const float planetColumnWidth = 108f;
            const float colorColumnWidth = 390f;
            if (!BeginFixedDataTable("##PlanetColors", 4, planetColumnWidth, colorColumnWidth)) return;
            DrawTableHeader(L("星球", "Planets"), planetColumnWidth);
            DrawTableHeader(L("本体", "Body"), colorColumnWidth);
            DrawTableHeader(L("球环", "Ring"), colorColumnWidth);
            DrawTableHeader(L("拖尾", "Tail"), colorColumnWidth);
            DrawPlanetColorRow(L("火之行星", "Fire planet"), "red", settings.RedPlanetColor, settings.RedRingColor, settings.RedTailColor);
            DrawPlanetColorRow(L("冰之行星", "Ice planet"), "blue", settings.BluePlanetColor, settings.BlueRingColor, settings.BlueTailColor);
            DrawPlanetColorRow(L("风之行星", "Wind planet"), "green", settings.GreenPlanetColor, settings.GreenRingColor, settings.GreenTailColor);
            EndFixedDataTable();
        }

        private static void DrawPlanetColorRow(string name, string id, float[] planet, float[] ring, float[] tail)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(name);
            ImGui.TableNextColumn();
            DrawColor("##planet_" + id, planet);
            ImGui.TableNextColumn();
            DrawColor("##ring_" + id, ring);
            ImGui.TableNextColumn();
            DrawColor("##tail_" + id, tail);
        }

        private static void DrawColor(string id, float[] color)
        {
            var value = new NumVector4(color[0], color[1], color[2], color[3]);
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.ColorEdit4(id, ref value, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreviewHalf))
            {
                color[0] = value.X; color[1] = value.Y; color[2] = value.Z; color[3] = value.W;
                ToolsRuntime.SettingsChanged(false, true);
            }
        }

        private static bool BeginFixedDataTable(string id, int columns, float firstColumnWidth, float otherColumnWidth)
        {
            const float horizontalMargin = 12f;
            const float verticalMargin = 6f;
            ImGui.Dummy(new NumVector2(1f, verticalMargin));
            ImGui.Indent(horizontalMargin);
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new NumVector2(12f, 7f));
            ImGuiTableFlags flags = ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingFixedFit;
            float width = firstColumnWidth + otherColumnWidth * (columns - 1);
            bool opened = ImGui.BeginTable(id, columns, flags, new NumVector2(width, 0f));
            if (!opened)
            {
                ImGui.PopStyleVar();
                ImGui.Unindent(horizontalMargin);
                return false;
            }
            ImGui.TableSetupColumn("##first", ImGuiTableColumnFlags.WidthFixed, firstColumnWidth);
            for (int i = 1; i < columns; i++)
                ImGui.TableSetupColumn("##column_" + i, ImGuiTableColumnFlags.WidthFixed, otherColumnWidth);
            ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
            return true;
        }

        private static void DrawTableHeader(string text, float width)
        {
            ImGui.TableNextColumn();
            TableCenteredText(text, width);
        }

        private static void EndFixedDataTable()
        {
            ImGui.EndTable();
            ImGui.PopStyleVar();
            ImGui.Unindent(12f);
            ImGui.Dummy(new NumVector2(1f, 6f));
        }

        private static void TableCenteredText(string value, float columnWidth)
        {
            string text = value ?? string.Empty;
            float textWidth = ImGui.CalcTextSize(text).X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Mathf.Max(0f, (columnWidth - 24f - textWidth) * 0.5f));
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(text);
        }

        private static void DrawPageHeading(string title)
        {
            ImGui.TextColored(SonnetPalette.Text, SonnetWidgets.Plain(title));
            ImGui.Separator();
        }

        private static void DrawSectionHeading(string title)
        {
            ImGui.Spacing();
            ImGui.TextColored(SonnetPalette.Muted, SonnetWidgets.Plain(title));
            ImGui.Separator();
        }

        private static bool BeginPaddedTable(string id, int columns, ImGuiTableFlags flags)
        {
            const float horizontalMargin = 12f;
            const float verticalMargin = 6f;
            ImGui.Dummy(new NumVector2(1f, verticalMargin));
            ImGui.Indent(horizontalMargin);
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new NumVector2(10f, 6f));
            NumVector2 available = ImGui.GetContentRegionAvail();
            bool opened = ImGui.BeginTable(id, columns, flags, new NumVector2(Mathf.Max(1f, available.X - horizontalMargin), 0f));
            if (!opened)
            {
                ImGui.PopStyleVar();
                ImGui.Unindent(horizontalMargin);
            }
            return opened;
        }

        private static void EndPaddedTable()
        {
            ImGui.EndTable();
            ImGui.PopStyleVar();
            ImGui.Unindent(12f);
            ImGui.Dummy(new NumVector2(1f, 6f));
        }

        private static void SetAllHitText(ToolsSettings settings, bool hidden)
        {
            settings.HideTooEarly = hidden; settings.HideVeryEarly = hidden; settings.HideEarlyPerfect = hidden;
            settings.HidePerfect = hidden; settings.HideLatePerfect = hidden; settings.HideVeryLate = hidden;
            settings.HideTooLate = hidden; settings.HideMultipress = hidden; settings.HideFailMiss = hidden;
            settings.HideFailOverload = hidden; settings.HideOverPress = hidden;
        }

        private static KeyCode[] BuildKeyCandidates()
        {
            Array values = Enum.GetValues(typeof(KeyCode));
            var result = new List<KeyCode>();
            for (int i = 0; i < values.Length; i++)
            {
                KeyCode key = (KeyCode)values.GetValue(i);
                int code = (int)key;
                if (code < 323 || code > 329) result.Add(key);
            }
            return result.ToArray();
        }
    }
}
