using System;
using System.Collections.Generic;
using CheryTools.Sonnet.Contracts;
using ImGuiNET;
using UnityEngine;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;

namespace CheryTools.ReadHelper.Preview
{
    public sealed class ReadHelperPreviewModule : ICheryModule
    {
        private const string TabMain = "main";

        public ModuleMetadata Metadata { get; } = new ModuleMetadata(
            "readhelper.preview", "读谱辅助", "0.1.0", "CheryTools", new NumVector4(0.30f, 0.82f, 0.88f, 1f))
        {
            NavigationTitle = "读谱辅助"
        };

        public IReadOnlyList<ModuleTab> Tabs { get; } = new List<ModuleTab>
        {
            new ModuleTab(TabMain, "基础与样式")
        };

        public void Initialize(ModuleContext context) => ReadHelperRuntime.Initialize(context);
        public void Update() { }
        public void Shutdown() => ReadHelperRuntime.Shutdown();

        public void Draw(ModuleUiContext context)
        {
            ReadHelperSettings settings = ReadHelperRuntime.Settings;
            if (settings == null) return;

            DrawToolbar(settings);
            ImGui.Spacing();
            DrawLivePreview(settings);
            ImGui.Spacing();
            DrawSettingsForm(settings);
        }

        private static void DrawToolbar(ReadHelperSettings settings)
        {
            bool changed = false;

            bool enabled = settings.Enabled;
            if (ImGui.Checkbox("启用读谱辅助##enabled", ref enabled))
            {
                settings.Enabled = enabled;
                changed = true;
            }
            ImGui.SameLine();
            bool showInGame = settings.ShowInGameOnly;
            if (ImGui.Checkbox("仅在游玩中显示##show_ingame", ref showInGame))
            {
                settings.ShowInGameOnly = showInGame;
                changed = true;
            }
            ImGui.SameLine();
            bool showInEditor = settings.ShowInEditor;
            if (ImGui.Checkbox("编辑器中启用##show_editor", ref showInEditor))
            {
                settings.ShowInEditor = showInEditor;
                changed = true;
            }
            ImGui.SameLine();
            bool hidePaused = settings.HideWhenPaused;
            if (ImGui.Checkbox("暂停时隐藏##hide_paused", ref hidePaused))
            {
                settings.HideWhenPaused = hidePaused;
                changed = true;
            }

            ImGui.SameLine();
            if (ImGui.Button("恢复默认设置##reset"))
            {
                settings.ResetToDefaults();
                changed = true;
            }

            if (changed) ReadHelperRuntime.SaveSettings();
        }

        private struct PreviewNote
        {
            public float Offset;
            public int Type;
            public string Label;
        }

        private static void DrawLivePreview(ReadHelperSettings settings)
        {
            ImGui.TextDisabled("实时预览 (两端音符向中间判定线流动，透明度两端渐变淡出)");
            NumVector2 avail = ImGui.GetContentRegionAvail();
            float previewHeight = 150f;
            NumVector2 size = new NumVector2(avail.X, previewHeight);

            NumVector2 pos = ImGui.GetCursorScreenPos();
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();

            // 背景框
            uint bgCol = ImGui.GetColorU32(new NumVector4(0.06f, 0.07f, 0.09f, 0.95f));
            uint borderCol = ImGui.GetColorU32(new NumVector4(0.2f, 0.22f, 0.28f, 0.8f));
            drawList.AddRectFilled(pos, pos + size, bgCol, 8f);
            drawList.AddRect(pos, pos + size, borderCol, 8f);

            // 裁剪范围
            drawList.PushClipRect(pos, pos + size, true);

            float centerX = pos.X + size.X * 0.5f;
            float centerY = pos.Y + size.Y * 0.5f;
            float previewTrackWidth = Mathf.Min(size.X - 40f, settings.TrackWidth * 0.6f);
            float trackHalfW = previewTrackWidth * 0.5f;
            float noteHeight = settings.NoteHeight * 0.6f;
            float doubleTapWidth = settings.DoubleTapWidth * 0.8f;
            float doubleTapBorder = Mathf.Max(1f, settings.DoubleTapBorderThickness * 0.8f);
            float noteWidth = settings.NoteWidth * 0.8f;
            float targetBoxW = settings.TargetBoxWidth * 0.6f;
            float targetBoxH = settings.TargetBoxHeight * 0.6f;

            // 1. 绘制中央判定框
            uint targetBoxU32 = ColorU32(settings.TargetBoxColor, 1f);
            uint targetLineU32 = ColorU32(settings.TargetLineColor, 1f);
            drawList.AddRect(
                new NumVector2(centerX - targetBoxW * 0.5f, centerY - targetBoxH * 0.5f),
                new NumVector2(centerX + targetBoxW * 0.5f, centerY + targetBoxH * 0.5f),
                targetBoxU32, 2f, ImDrawFlags.None, 2f);
            drawList.AddLine(
                new NumVector2(centerX, centerY - targetBoxH * 0.5f + 4f),
                new NumVector2(centerX, centerY + targetBoxH * 0.5f - 4f),
                targetLineU32, settings.TargetLineWidth);

            // 2. 模拟流动时间 (0..1 循环)
            float t = (Time.unscaledTime * 0.6f) % 1.0f;

            // 左侧双押音符预览 (向右流向中间: x < centerX)
            float[] sampleDoubleTaps = { (t + 0.15f) % 1f, (t + 0.65f) % 1f };
            for (int i = 0; i < sampleDoubleTaps.Length; i++)
            {
                float u = sampleDoubleTaps[i];
                float alpha = Mathf.Pow(1f - Mathf.Clamp01(u), settings.FadePower);
                float x = centerX - u * trackHalfW;

                uint border = ColorU32(settings.DoubleTapBorderColor, alpha);
                uint fill = ColorU32(settings.DoubleTapFillColor, alpha);

                drawList.AddRectFilled(
                    new NumVector2(x - doubleTapWidth * 0.5f, centerY - noteHeight * 0.5f),
                    new NumVector2(x + doubleTapWidth * 0.5f, centerY + noteHeight * 0.5f),
                    fill);
                drawList.AddRect(
                    new NumVector2(x - doubleTapWidth * 0.5f, centerY - noteHeight * 0.5f),
                    new NumVector2(x + doubleTapWidth * 0.5f, centerY + noteHeight * 0.5f),
                    border, 0f, ImDrawFlags.None, doubleTapBorder);
            }

            PreviewNote[] rightNotes =
            {
                new PreviewNote { Offset = (t + 0.05f) % 1f, Type = 0, Label = "" },
                new PreviewNote { Offset = (t + 0.35f) % 1f, Type = 1, Label = "4/4 180BPM x0.5" },
                new PreviewNote { Offset = (t + 0.60f) % 1f, Type = 0, Label = "" },
                new PreviewNote { Offset = (t + 0.85f) % 1f, Type = 2, Label = "4/4 360BPM x2" }
            };

            for (int i = 0; i < rightNotes.Length; i++)
            {
                PreviewNote p = rightNotes[i];
                float u = p.Offset;
                float alpha = Mathf.Pow(1f - Mathf.Clamp01(u), settings.FadePower);
                float x = centerX + u * trackHalfW;

                float[] baseColor = p.Type == 1 ? settings.DecelColor : p.Type == 2 ? settings.AccelColor : settings.NormalNoteColor;
                uint col = ColorU32(baseColor, alpha);
                float w = p.Type != 0 ? (noteWidth + 1f) : noteWidth;

                drawList.AddLine(
                    new NumVector2(x, centerY - noteHeight * 0.5f),
                    new NumVector2(x, centerY + noteHeight * 0.5f),
                    col, w);

                if (!string.IsNullOrEmpty(p.Label) && settings.ShowSpeedLabels)
                {
                    NumVector2 textPos = new NumVector2(x - 40f, centerY - noteHeight * 0.5f - 16f);
                    drawList.AddText(textPos, col, p.Label);
                }
            }

            drawList.PopClipRect();
            ImGui.Dummy(size);
        }

        private static void DrawSettingsForm(ReadHelperSettings settings)
        {
            bool changed = false;

            if (ImGui.CollapsingHeader("位置与尺寸 (Position & Size)", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.BeginTable("pos_size_table", 2, ImGuiTableFlags.SizingStretchProp))
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("水平位置 (X 偏移)");
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    changed |= ImGui.SliderFloat("##pos_x", ref settings.PositionX, -960f, 960f, "%.0f px");

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("垂直位置 (Y 偏移)");
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    changed |= ImGui.SliderFloat("##pos_y", ref settings.PositionY, -540f, 540f, "%.0f px");

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("整体缩放比例");
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    changed |= ImGui.SliderFloat("##scale", ref settings.Scale, 0.2f, 2.5f, "%.2fx");

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("时间轴总宽度");
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    changed |= ImGui.SliderFloat("##track_w", ref settings.TrackWidth, 300f, 1800f, "%.0f px");

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("音符线条高度");
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    changed |= ImGui.SliderFloat("##note_h", ref settings.NoteHeight, 30f, 240f, "%.0f px");

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("普通音符粗细");
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    changed |= ImGui.SliderFloat("##note_w", ref settings.NoteWidth, 1f, 12f, "%.1f px");

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("双押方块宽度");
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    changed |= ImGui.SliderFloat("##dt_w", ref settings.DoubleTapWidth, 4f, 30f, "%.1f px");

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("双押描边厚度");
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    changed |= ImGui.SliderFloat("##dt_border", ref settings.DoubleTapBorderThickness, 1f, 6f, "%.1f px");

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("判定框尺寸 (宽 x 高)");
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(100f);
                    changed |= ImGui.DragFloat("##target_w", ref settings.TargetBoxWidth, 1f, 8f, 60f, "宽 %.0f");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(100f);
                    changed |= ImGui.DragFloat("##target_h", ref settings.TargetBoxHeight, 1f, 20f, 300f, "高 %.0f");

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("判定线粗细");
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    changed |= ImGui.SliderFloat("##target_line", ref settings.TargetLineWidth, 1f, 10f, "%.1f px");

                    ImGui.EndTable();
                }
            }

            ImGui.Spacing();

            if (ImGui.CollapsingHeader("时间与流动 (Timing & Flow)", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.BeginTable("timing_table", 2, ImGuiTableFlags.SizingStretchProp))
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("提前显示视窗 (秒数)");
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    changed |= ImGui.SliderFloat("##lookahead", ref settings.LookAheadSeconds, 0.4f, 3.5f, "%.2f 秒");

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("双押角度判定阈值");
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    changed |= ImGui.SliderFloat("##angle_thresh", ref settings.DoubleTapAngleThreshold, 5f, 60f, "%.0f°");

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("边缘渐变淡出强度");
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(-1f);
                    changed |= ImGui.SliderFloat("##fade_pow", ref settings.FadePower, 0.2f, 3.0f, "%.2f");

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("显示速度变化文字");
                    ImGui.TableNextColumn();
                    changed |= ImGui.Checkbox("##show_speed_labels", ref settings.ShowSpeedLabels);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("速度文字字号与垂直偏距");
                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(100f);
                    changed |= ImGui.DragInt("##font_size", ref settings.FontSize, 1, 10, 36, "%d pt");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(100f);
                    changed |= ImGui.DragFloat("##label_off_y", ref settings.LabelOffsetY, 1f, 0f, 60f, "偏移 %.0f");

                    ImGui.EndTable();
                }
            }

            ImGui.Spacing();

            if (ImGui.CollapsingHeader("颜色配置 (Color Styling)", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.BeginTable("colors_table", 2, ImGuiTableFlags.SizingStretchProp))
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("普通音符颜色 (白线)");
                    ImGui.TableNextColumn();
                    changed |= DrawColorField("##col_normal", settings.NormalNoteColor);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("双押音符内部填充 (白底)");
                    ImGui.TableNextColumn();
                    changed |= DrawColorField("##col_dt_fill", settings.DoubleTapFillColor);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("双押音符外部描边 (粉色描边)");
                    ImGui.TableNextColumn();
                    changed |= DrawColorField("##col_dt_border", settings.DoubleTapBorderColor);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("减速方块与文字 (青色 x0.5)");
                    ImGui.TableNextColumn();
                    changed |= DrawColorField("##col_decel", settings.DecelColor);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("加速方块与文字 (深红 x2)");
                    ImGui.TableNextColumn();
                    changed |= DrawColorField("##col_accel", settings.AccelColor);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("判定框颜色");
                    ImGui.TableNextColumn();
                    changed |= DrawColorField("##col_target_box", settings.TargetBoxColor);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("判定线中心轴颜色");
                    ImGui.TableNextColumn();
                    changed |= DrawColorField("##col_target_line", settings.TargetLineColor);

                    ImGui.EndTable();
                }
            }

            if (changed) ReadHelperRuntime.SaveSettings();
        }

        private static bool DrawColorField(string id, float[] value)
        {
            var color = new NumVector4(value[0], value[1], value[2], value[3]);
            ImGui.SetNextItemWidth(-1f);
            bool changed = ImGui.ColorEdit4(id, ref color, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreviewHalf);
            if (changed)
            {
                value[0] = color.X;
                value[1] = color.Y;
                value[2] = color.Z;
                value[3] = color.W;
            }
            return changed;
        }

        private static uint ColorU32(float[] value, float alphaMultiplier)
        {
            if (value == null || value.Length < 4) return 0xFFFFFFFF;
            float a = Mathf.Clamp01(value[3] * alphaMultiplier);
            return ImGui.GetColorU32(new NumVector4(value[0], value[1], value[2], a));
        }
    }
}
