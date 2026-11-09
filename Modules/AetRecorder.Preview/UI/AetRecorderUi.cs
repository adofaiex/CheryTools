using System;
using System.Diagnostics;
using System.IO;
using ADOFAI.EditorTweaks.Api.Rendering;
using ADOFAI.EditorTweaks.ChartRendering;
using ADOFAI.EditorTweaks.ChartRendering.Features.ChartRendering;
using ADOFAI.EditorTweaks.ChartRendering.Patching;
using ImGuiNET;
using UnityEngine;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;
using CheryTools.Sonnet.Shared;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.AetRecorder.Preview.UI
{
    internal static class AetRecorderUi
    {
        private static string _ffmpegVersionOutput = string.Empty;
        private static ChartRenderTask? _observedTask;
        private static string _lastCompletedPath = string.Empty;
        private static bool _lastTaskFailed;
        private static string _lastTaskError = string.Empty;

        internal static void Draw(string activeTabId)
        {
            Settings settings = AetRecorderRuntime.Settings;
            if (settings == null) return;

            PushControlStyle();
            switch (activeTabId)
            {
                case "settings":
                    DrawSettingsTab(settings);
                    break;
                case "diagnostics":
                    DrawDiagnosticsTab(settings);
                    break;
                default:
                    DrawRenderTab(settings);
                    break;
            }
            PopControlStyle();
        }

        private static void PushControlStyle()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 7f);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new NumVector2(10f, 6f));
            ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 9f);
            ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new NumVector4(1f, 1f, 1f, 0.055f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new NumVector4(1f, 1f, 1f, 0.10f));
        }

        private static void PopControlStyle()
        {
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(4);
        }

        #region Tab: Render Control

        private static void DrawRenderTab(Settings settings)
        {
            DrawPageHeading(L("谱面离线录制控制", "Offline chart rendering"));

            bool isLevelLoaded = ChartRenderPlaybackController.IsPlayableLevelLoaded();
            ChartRenderAvailability availability = ChartRenderService.GetAvailability();
            ChartRenderTask? currentTask = ChartRenderService.CurrentTask;
            bool isRendering = currentTask != null && !currentTask.IsTerminal;

            // Track completed/terminal task for result display
            if (currentTask != null)
            {
                _observedTask = currentTask;
            }
            if (_observedTask != null && _observedTask.IsTerminal)
            {
                if (_observedTask.State == ChartRenderTaskState.Completed)
                {
                    _lastCompletedPath = _observedTask.Result?.OutputPath ?? string.Empty;
                    _lastTaskFailed = false;
                    _lastTaskError = string.Empty;
                }
                else if (_observedTask.State == ChartRenderTaskState.Failed)
                {
                    _lastTaskFailed = true;
                    _lastTaskError = _observedTask.Result?.Message ?? L("渲染过程中发生错误", "An error occurred while rendering");
                }
            }

            // Status Card
            DrawSectionHeading(L("谱面环境状态", "Level status"));
            if (isLevelLoaded)
            {
                string sceneInfo = GetCurrentLevelDescription();
                ImGui.TextColored(new NumVector4(0.35f, 0.95f, 0.65f, 1f), L("[就绪] ", "[Ready] ") + SonnetWidgets.Plain(sceneInfo));
                int dspBufferSize = AudioSettings.GetConfiguration().dspBufferSize;
                if (dspBufferSize > ChartRenderAudioBuffer.RequiredDspBufferSize)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new NumVector4(1f, 0.75f, 0.35f, 1f));
                    int required = ChartRenderAudioBuffer.RequiredDspBufferSize;
                    ImGui.TextWrapped(L(
                        $"当前音频缓冲为 {dspBufferSize}。渲染时会临时切换到 {required} 以录制声音，并自动重新加载谱面音乐；"
                        + $"其他模组的音效（如 CReplay）可能需要重新进入谱面才能恢复。在游戏设置中将音频缓冲改为 {required} 可避免这次切换。",
                        $"The audio buffer is {dspBufferSize}. Renders switch it to {required} to capture sound and reload the level music; "
                        + $"sounds from other mods (such as CReplay) may need the level to be reopened. Set the buffer to {required} in the game settings to avoid the switch."));
                    ImGui.PopStyleColor();
                }
            }
            else
            {
                ImGui.TextColored(new NumVector4(1f, 0.45f, 0.45f, 1f), L("[未就绪] 当前未处于可播放的谱面或编辑器环境中。请先打开自制谱或官方关卡。", "[Not ready] No playable level or editor is open. Open a custom or official level first."));
            }

            if (!availability.Available)
            {
                ImGui.TextColored(new NumVector4(1f, 0.75f, 0.35f, 1f), L("渲染服务限制: ", "Render service limitation: ") + SonnetWidgets.Plain(availability.Message));
            }

            ImGui.Spacing();

            // Range & Options
            DrawSectionHeading(L("录制参数", "Render options"));

            bool inEditor = ADOBase.editor != null;
            bool useSelectedRange = settings.ChartRenderUseSelectedRange;
            if (inEditor)
            {
                if (ImGui.RadioButton(L("整首谱面 (Whole Level)", "Whole level"), !useSelectedRange))
                {
                    settings.ChartRenderUseSelectedRange = false;
                    AetRecorderRuntime.SettingsChanged();
                }
                ImGui.SameLine();
                if (ImGui.RadioButton(L("编辑器选中段落 (Selected Range)", "Selected range in editor"), useSelectedRange))
                {
                    settings.ChartRenderUseSelectedRange = true;
                    AetRecorderRuntime.SettingsChanged();
                }
            }
            else
            {
                ImGui.TextDisabled(L("录制范围: 整首关卡 (片段录制需在编辑器模式)", "Range: whole level (selected ranges need the editor)"));
            }

            bool showJudgments = settings.ChartRenderShowHitJudgments;
            if (ImGui.Checkbox(L("渲染判定文字 (Hit Judgments)", "Render hit judgements"), ref showJudgments))
            {
                settings.ChartRenderShowHitJudgments = showJudgments;
                AetRecorderRuntime.SettingsChanged();
            }

            ImGui.SameLine();
            ImGui.TextDisabled(L("| 分辨率: ", "| Resolution: ") + $"{settings.ChartRenderWidth}x{settings.ChartRenderHeight} @ {settings.ChartRenderFps} FPS");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Action Buttons
            if (!isRendering)
            {
                bool canStart = isLevelLoaded && availability.Available;
                if (!canStart) ImGui.BeginDisabled();

                ImGui.PushStyleColor(ImGuiCol.Button, new NumVector4(0.18f, 0.65f, 0.55f, 0.85f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new NumVector4(0.24f, 0.78f, 0.66f, 0.95f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new NumVector4(0.14f, 0.55f, 0.45f, 1f));

                if (SonnetWidgets.FitButton(L("开始录制谱面视频", "Start rendering"), 220f, 40f))
                {
                    StartRender(settings);
                }

                ImGui.PopStyleColor(3);

                if (!canStart) ImGui.EndDisabled();
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new NumVector4(0.75f, 0.22f, 0.22f, 0.85f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new NumVector4(0.88f, 0.28f, 0.28f, 0.95f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new NumVector4(0.65f, 0.18f, 0.18f, 1f));

                if (SonnetWidgets.FitButton(L("取消录制 (Cancel / Esc)", "Cancel (Esc)"), 220f, 40f))
                {
                    currentTask?.Cancel();
                }

                ImGui.PopStyleColor(3);
            }

            ImGui.Spacing();

            // Active Progress Dashboard
            if (isRendering && currentTask != null)
            {
                DrawSectionHeading(L("实时渲染仪表盘", "Render progress"));
                ChartRenderProgress progress = currentTask.Progress;

                ImGui.ProgressBar(progress.Value, new NumVector2(-1f, 26f), $"{(progress.Value * 100f):F1}% - {progress.Stage}");

                if (!string.IsNullOrEmpty(progress.Detail))
                {
                    ImGui.TextDisabled(SonnetWidgets.Plain(progress.Detail));
                }

                ImGui.Spacing();
                if (ImGui.BeginTable("RenderMetricsTable", 4, ImGuiTableFlags.BordersInnerH))
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.TextDisabled(L("帧数进度", "Frames"));
                    ImGui.Text($"{progress.WrittenFrames} / {(progress.TotalFrames > 0 ? progress.TotalFrames.ToString() : "~")}");

                    ImGui.TableSetColumnIndex(1);
                    ImGui.TextDisabled(L("处理速度", "Speed"));
                    ImGui.Text($"{progress.ProcessingFramesPerSecond:F1} FPS");

                    ImGui.TableSetColumnIndex(2);
                    ImGui.TextDisabled(L("预计剩余", "Time left"));
                    ImGui.Text(progress.EstimatedRemaining > TimeSpan.Zero ? $"{progress.EstimatedRemaining:mm\\:ss}" : "--:--");

                    ImGui.TableSetColumnIndex(3);
                    ImGui.TextDisabled(L("流畅度/重复帧", "Smoothness / repeated frames"));
                    string smoothness = progress.DuplicateRatio <= 0.05f ? L("流畅", "Smooth") : progress.DuplicateRatio <= 0.2f ? L("良好", "Good") : L("轻微重复", "Some repeats");
                    ImGui.Text($"{smoothness} ({(progress.DuplicateRatio * 100f):F1}%%)");

                    ImGui.EndTable();
                }

                if (!string.IsNullOrEmpty(progress.MemoryBudget))
                {
                    ImGui.Spacing();
                    ImGui.TextDisabled(L("显存/内存预算: ", "Memory budget: ") + progress.MemoryBudget + L(" | 队列: ", " | Queue: ") + progress.QueueBudget);
                }
            }

            // Results Dashboard
            if (!string.IsNullOrEmpty(_lastCompletedPath) && File.Exists(_lastCompletedPath))
            {
                ImGui.Spacing();
                DrawSectionHeading(L("最近导出成果", "Latest output"));
                ImGui.TextColored(new NumVector4(0.35f, 0.95f, 0.65f, 1f), L("[渲染成功] 视频已保存至:", "[Done] Video saved to:"));
                ImGui.TextWrapped(SonnetWidgets.Plain(_lastCompletedPath));

                if (SonnetWidgets.FitButton(L("打开视频所在目录", "Open folder"), 160f, 32f))
                {
                    OpenFileExplorerAndSelect(_lastCompletedPath);
                }
                ImGui.SameLine();
                if (SonnetWidgets.FitButton(L("直接播放成品视频", "Play video"), 160f, 32f))
                {
                    OpenMediaFile(_lastCompletedPath);
                }
            }
            else if (_lastTaskFailed)
            {
                ImGui.Spacing();
                DrawSectionHeading(L("渲染失败", "Render failed"));
                ImGui.TextColored(new NumVector4(1f, 0.35f, 0.35f, 1f), L("[错误] ", "[Error] ") + SonnetWidgets.Plain(_lastTaskError));
            }
        }

        private static void StartRender(Settings settings)
        {
            try
            {
                ChartRenderRequest request = ChartRenderService.CreateRequestFromCurrentSettings();
                ChartRenderStartResult start = ChartRenderService.Start(request);
                if (!start.Success)
                {
                    AetRecorderRuntime.Logger?.Error(L("无法启动录制: ", "Could not start rendering: ") + start.Message);
                }
            }
            catch (Exception ex)
            {
                AetRecorderRuntime.Logger?.Error(L("启动录制异常: ", "Error starting render: ") + ex.Message);
            }
        }

        private static string GetCurrentLevelDescription()
        {
            if (ADOBase.editor != null)
            {
                scnEditor editor = ADOBase.editor;
                string levelName = editor.customLevel?.levelData?.song ?? L("当前编辑器谱面", "Current editor level");
                string author = editor.customLevel?.levelData?.author ?? L("未知作者", "Unknown author");
                int floors = editor.floors != null ? editor.floors.Count : 0;
                return L($"编辑器模式: 《{levelName}》 by {author} (共 {floors} 块)", $"Editor: {levelName} by {author} ({floors} tiles)");
            }

            if (ADOBase.controller != null)
            {
                scrController controller = ADOBase.controller;
                string level = string.IsNullOrEmpty(controller.levelName) ? ADOBase.sceneName : controller.levelName;
                return L($"游戏模式: 《{level}》 ({ADOBase.sceneName})", $"Game: {level} ({ADOBase.sceneName})");
            }

            return L("就绪", "Ready");
        }

        #endregion

        #region Tab: Settings

        private static void DrawSettingsTab(Settings settings)
        {
            DrawPageHeading(L("录制画质与参数设置", "Quality and output settings"));

            // Resolution Presets
            DrawSectionHeading(L("画面分辨率 (Resolution)", "Resolution"));
            int currentW = settings.ChartRenderWidth;
            int currentH = settings.ChartRenderHeight;

            if (PresetButton("1080P (FHD)", currentW == 1920 && currentH == 1080))
            {
                settings.ChartRenderWidth = 1920; settings.ChartRenderHeight = 1080;
                AetRecorderRuntime.SettingsChanged();
            }
            ImGui.SameLine();
            if (PresetButton("2K (QHD)", currentW == 2560 && currentH == 1440))
            {
                settings.ChartRenderWidth = 2560; settings.ChartRenderHeight = 1440;
                AetRecorderRuntime.SettingsChanged();
            }
            ImGui.SameLine();
            if (PresetButton("4K (UHD)", currentW == 3840 && currentH == 2160))
            {
                settings.ChartRenderWidth = 3840; settings.ChartRenderHeight = 2160;
                AetRecorderRuntime.SettingsChanged();
            }
            ImGui.SameLine();
            if (PresetButton("720P (HD)", currentW == 1280 && currentH == 720))
            {
                settings.ChartRenderWidth = 1280; settings.ChartRenderHeight = 720;
                AetRecorderRuntime.SettingsChanged();
            }

            int w = settings.ChartRenderWidth;
            int h = settings.ChartRenderHeight;
            ImGui.SetNextItemWidth(140f);
            if (ImGui.InputInt(L("宽度 (Width)", "Width"), ref w))
            {
                settings.ChartRenderWidth = w;
                AetRecorderRuntime.SettingsChanged();
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(140f);
            if (ImGui.InputInt(L("高度 (Height)", "Height"), ref h))
            {
                settings.ChartRenderHeight = h;
                AetRecorderRuntime.SettingsChanged();
            }

            ImGui.Spacing();

            // FPS Presets
            DrawSectionHeading(L("录制帧率 (Framerate)", "Frame rate"));
            int currentFps = settings.ChartRenderFps;
            if (PresetButton("60 FPS", currentFps == 60))
            {
                settings.ChartRenderFps = 60; AetRecorderRuntime.SettingsChanged();
            }
            ImGui.SameLine();
            if (PresetButton("120 FPS", currentFps == 120))
            {
                settings.ChartRenderFps = 120; AetRecorderRuntime.SettingsChanged();
            }
            ImGui.SameLine();
            if (PresetButton("240 FPS", currentFps == 240))
            {
                settings.ChartRenderFps = 240; AetRecorderRuntime.SettingsChanged();
            }
            ImGui.SameLine();
            if (PresetButton("30 FPS", currentFps == 30))
            {
                settings.ChartRenderFps = 30; AetRecorderRuntime.SettingsChanged();
            }

            int fps = settings.ChartRenderFps;
            ImGui.SetNextItemWidth(200f);
            if (SonnetWidgets.SliderInt(L("帧率", "Frame rate"), ref fps, 1, 240, "%d FPS"))
            {
                settings.ChartRenderFps = fps;
                AetRecorderRuntime.SettingsChanged();
            }

            ImGui.Spacing();

            // Encoder & Quality
            DrawSectionHeading(L("编码器与画质", "Encoder and quality"));

            string[] videoCodecs = { L("H.264 / AVC (最通用兼容)", "H.264 / AVC (most compatible)"), L("HEVC / H.265 (高效压缩高质量)", "HEVC / H.265 (efficient, high quality)"), L("AV1 (下一代高效编码)", "AV1 (next-generation codec)"), L("ProRes 422 HQ (专业剪辑后期)", "ProRes 422 HQ (for editing)") };
            string[] videoCodecKeys = { ChartRenderOptionValues.VideoCodecH264, ChartRenderOptionValues.VideoCodecHevc, ChartRenderOptionValues.VideoCodecAv1, ChartRenderOptionValues.VideoCodecProRes };
            int currentCodecIndex = Math.Max(0, Array.IndexOf(videoCodecKeys, settings.ChartRenderVideoCodec));

            ImGui.SetNextItemWidth(320f);
            if (ImGui.Combo(L("视频编码格式", "Video codec"), ref currentCodecIndex, videoCodecs, videoCodecs.Length))
            {
                settings.ChartRenderVideoCodec = videoCodecKeys[currentCodecIndex];
                AetRecorderRuntime.SettingsChanged();
            }

            // Every mode except compatibility uses NVENC when the GPU has it and falls back to the
            // CPU encoder otherwise; the labels describe speed versus quality, not the device.
            string[] encoderModes = { L("自动平衡", "Auto balanced"), L("最快（画质最低）", "Fastest (lowest quality)"), L("均衡", "Balanced"), L("高画质（较慢）", "High quality (slower)"), L("兼容模式（仅用 CPU 编码）", "Compatibility (CPU encoding only)") };
            string[] encoderKeys = { ChartRenderOptionValues.EncoderAutoBalanced, ChartRenderOptionValues.EncoderFastest, ChartRenderOptionValues.EncoderBalanced, ChartRenderOptionValues.EncoderQuality, ChartRenderOptionValues.EncoderCpuCompatibility };
            int currentEncoderIndex = Math.Max(0, Array.IndexOf(encoderKeys, settings.ChartRenderEncoderMode));

            ImGui.SetNextItemWidth(320f);
            if (ImGui.Combo(L("编码模式", "Encoder mode"), ref currentEncoderIndex, encoderModes, encoderModes.Length))
            {
                settings.ChartRenderEncoderMode = encoderKeys[currentEncoderIndex];
                AetRecorderRuntime.SettingsChanged();
            }

            int crf = settings.ChartRenderCrf;
            ImGui.SetNextItemWidth(320f);
            if (SonnetWidgets.SliderInt(L("CRF 质量因子", "CRF quality"), ref crf, 0, 51, L("%d (越小越清晰，推荐 18)", "%d (lower is sharper, 18 recommended)")))
            {
                settings.ChartRenderCrf = crf;
                AetRecorderRuntime.SettingsChanged();
            }

            ImGui.Spacing();

            // Capture Source & Formats
            DrawSectionHeading(L("捕获来源与封装", "Capture source and container"));

            string[] captureSources = { L("纯净摄像机 (Camera，推荐无UI)", "Clean camera (no UI, recommended)"), L("完整游戏画面 (GameView，包含HUD)", "Full game view (with HUD)") };
            string[] captureSourceKeys = { ChartRenderOptionValues.CaptureSourceCamera, ChartRenderOptionValues.CaptureSourceGameView };
            int currentSourceIndex = Math.Max(0, Array.IndexOf(captureSourceKeys, settings.ChartRenderCaptureSource));
            ImGui.SetNextItemWidth(320f);
            if (ImGui.Combo(L("画面捕获来源", "Capture source"), ref currentSourceIndex, captureSources, captureSources.Length))
            {
                settings.ChartRenderCaptureSource = captureSourceKeys[currentSourceIndex];
                AetRecorderRuntime.SettingsChanged();
            }

            string[] videoFormats = { L("MP4 封装 (.mp4)", "MP4 (.mp4)"), L("MKV 封装 (.mkv)", "MKV (.mkv)"), L("MOV 封装 (.mov)", "MOV (.mov)") };
            string[] videoFormatKeys = { ChartRenderOptionValues.VideoFormatMp4, ChartRenderOptionValues.VideoFormatMkv, ChartRenderOptionValues.VideoFormatMov };
            int currentFormatIndex = Math.Max(0, Array.IndexOf(videoFormatKeys, settings.ChartRenderVideoFormat));
            ImGui.SetNextItemWidth(320f);
            if (ImGui.Combo(L("视频容器封装", "Container"), ref currentFormatIndex, videoFormats, videoFormats.Length))
            {
                settings.ChartRenderVideoFormat = videoFormatKeys[currentFormatIndex];
                AetRecorderRuntime.SettingsChanged();
            }

            float tailSeconds = settings.ChartRenderCompletionTailSeconds;
            ImGui.SetNextItemWidth(320f);
            if (SonnetWidgets.SliderFloat(L("结尾留白时长", "Tail padding"), ref tailSeconds, 0f, 15f, L("%.1f 秒", "%.1f s")))
            {
                settings.ChartRenderCompletionTailSeconds = tailSeconds;
                AetRecorderRuntime.SettingsChanged();
            }

            float offsetMs = settings.ChartRenderAudioSyncOffsetMs;
            ImGui.SetNextItemWidth(320f);
            if (SonnetWidgets.SliderFloat(L("音画同步微调", "Audio offset"), ref offsetMs, -500f, 500f, "%.0f ms"))
            {
                settings.ChartRenderAudioSyncOffsetMs = offsetMs;
                AetRecorderRuntime.SettingsChanged();
            }

            ImGui.Spacing();

            // Paths
            DrawSectionHeading(L("路径配置", "Paths"));

            string exportDir = settings.ChartRenderExportDirectory ?? string.Empty;
            ImGui.SetNextItemWidth(450f);
            if (ImGui.InputText(L("导出目录 (Export)", "Output folder"), ref exportDir, 512))
            {
                settings.ChartRenderExportDirectory = exportDir;
                AetRecorderRuntime.SettingsChanged();
            }
            ImGui.SameLine();
            if (ImGui.Button(L("打开导出目录", "Open output folder")))
            {
                if (!string.IsNullOrEmpty(exportDir) && Directory.Exists(exportDir))
                {
                    Process.Start("explorer.exe", exportDir);
                }
            }
            ImGui.SameLine();
            if (ImGui.Button(L("打开渲染日志", "Open render logs")))
            {
                string logDir = ChartRenderDiagnostics.LogDirectory(ChartRenderPaths.GetWorkspaceDirectory(settings));
                Directory.CreateDirectory(logDir);
                Process.Start("explorer.exe", logDir);
            }

            string customFfmpeg = settings.FfmpegCustomPath ?? string.Empty;
            ImGui.SetNextItemWidth(450f);
            if (ImGui.InputText(L("自定义 FFmpeg 路径", "Custom FFmpeg path"), ref customFfmpeg, 512))
            {
                settings.FfmpegCustomPath = customFfmpeg;
                AetRecorderRuntime.SettingsChanged();
            }
            ImGui.TextDisabled(L("留空时将自动寻找游戏目录、模块内嵌或系统 PATH 中的 ffmpeg.exe", "Leave empty to look for ffmpeg.exe in the game folder, the bundled copy or the system PATH"));

            ImGui.Spacing();
            if (SonnetWidgets.FitButton(L("恢复全部默认参数", "Reset all settings"), 160f, 30f))
            {
                settings.ResetAllDefaults(AetRecorderRuntime.ModuleDirectory);
                AetRecorderRuntime.SettingsChanged();
            }
        }

        private static bool PresetButton(string label, bool selected)
        {
            if (selected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new NumVector4(0.20f, 0.65f, 0.55f, 0.85f));
            }
            bool clicked = ImGui.Button(label, new NumVector2(110f, 28f));
            if (selected)
            {
                ImGui.PopStyleColor();
            }
            return clicked;
        }

        #endregion

        #region Tab: Diagnostics

        private static void DrawDiagnosticsTab(Settings settings)
        {
            DrawPageHeading(L("AET Recorder 诊断与运行状态", "AET Recorder diagnostics"));

            DrawSectionHeading(L("Harmony 补丁清单", "Harmony patches"));
            if (ImGui.BeginTable("PatchStatusTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn(L("功能组", "Feature group"), ImGuiTableColumnFlags.WidthFixed, 180f);
                ImGui.TableSetupColumn(L("状态", "Status"), ImGuiTableColumnFlags.WidthFixed, 110f);
                ImGui.TableSetupColumn(L("补丁数量", "Patches"), ImGuiTableColumnFlags.WidthFixed, 90f);
                ImGui.TableSetupColumn(L("诊断信息", "Details"), ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();

                foreach (PatchGroupStatus status in PatchManager.Statuses)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.TextUnformatted(status.Name);

                    ImGui.TableSetColumnIndex(1);
                    if (status.State == PatchGroupState.Active)
                    {
                        ImGui.TextColored(new NumVector4(0.35f, 0.95f, 0.65f, 1f), L("Active (已挂载)", "Active"));
                    }
                    else if (status.State == PatchGroupState.Failed)
                    {
                        ImGui.TextColored(new NumVector4(1f, 0.35f, 0.35f, 1f), L("Failed (失败)", "Failed"));
                    }
                    else if (status.State == PatchGroupState.Blocked)
                    {
                        ImGui.TextColored(new NumVector4(1f, 0.75f, 0.35f, 1f), L("Blocked (受阻)", "Blocked"));
                    }
                    else
                    {
                        ImGui.TextDisabled("Inactive");
                    }

                    ImGui.TableSetColumnIndex(2);
                    ImGui.TextUnformatted(status.PatchCount.ToString());

                    ImGui.TableSetColumnIndex(3);
                    if (!string.IsNullOrEmpty(status.Reason))
                    {
                        ImGui.TextColored(new NumVector4(1f, 0.45f, 0.45f, 1f), SonnetWidgets.Plain(status.Reason));
                    }
                    else
                    {
                        ImGui.TextDisabled(L("正常", "OK"));
                    }
                }

                ImGui.EndTable();
            }

            ImGui.Spacing();

            // FFmpeg Executable Diagnostic
            DrawSectionHeading(L("FFmpeg 可执行文件", "FFmpeg executable"));
            string ffmpegPath = ChartRenderPaths.GetFfmpegPath();
            bool exists = !string.IsNullOrEmpty(ffmpegPath) && File.Exists(ffmpegPath);

            if (exists)
            {
                ImGui.TextColored(new NumVector4(0.35f, 0.95f, 0.65f, 1f), L("[检测到 FFmpeg] ", "[FFmpeg found] ") + SonnetWidgets.Plain(ffmpegPath));
            }
            else
            {
                ImGui.TextColored(new NumVector4(1f, 0.35f, 0.35f, 1f), L("[未找到 FFmpeg] 路径: ", "[FFmpeg not found] Path: ") + SonnetWidgets.Plain(ffmpegPath));
            }

            if (SonnetWidgets.FitButton(L("检测 FFmpeg 版本与硬件编码支持", "Check FFmpeg version and hardware encoders"), 260f, 32f))
            {
                CheckFfmpegDiagnostics(ffmpegPath);
            }

            if (!string.IsNullOrEmpty(_ffmpegVersionOutput))
            {
                ImGui.Spacing();
                ImGui.BeginChild("FfmpegOutputBox", new NumVector2(-1f, 140f), ImGuiChildFlags.Borders);
                ImGui.TextUnformatted(_ffmpegVersionOutput);
                ImGui.EndChild();
            }
        }

        private static void CheckFfmpegDiagnostics(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _ffmpegVersionOutput = L("错误: FFmpeg 可执行文件不存在，请检查路径。", "Error: the FFmpeg executable does not exist. Check the path.");
                return;
            }

            try
            {
                var psi = new ProcessStartInfo(path, "-hide_banner -version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(3000);
                    _ffmpegVersionOutput = string.IsNullOrEmpty(stdout) ? stderr : stdout;
                }
            }
            catch (Exception ex)
            {
                _ffmpegVersionOutput = L("执行失败: ", "Failed to run: ") + ex.Message;
            }
        }

        #endregion

        #region Helpers

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

        private static void OpenFileExplorerAndSelect(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else
                {
                    string? dir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        Process.Start("explorer.exe", dir);
                    }
                }
            }
            catch
            {
            }
        }

        private static void OpenMediaFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }
            }
            catch
            {
            }
        }

        #endregion
    }
}
