using System;
using System.IO;
using ADOFAI.EditorTweaks.ChartRendering.Features.ChartRendering;
using Newtonsoft.Json;
using UnityEngine;

namespace ADOFAI.EditorTweaks.ChartRendering
{
    [Serializable]
    public class Settings
    {
        private const int MinSize = 16;
        private const int MaxWidth = 7680;
        private const int MaxHeight = 4320;
        private const int MaxFps = 240;

        public string FfmpegCustomPath = string.Empty;
        public string ChartRenderWorkspaceDirectory = string.Empty;
        public string ChartRenderExportDirectory = string.Empty;
        public int ChartRenderWidth = 1920;
        public int ChartRenderHeight = 1080;
        public int ChartRenderFps = 60;
        public int ChartRenderCrf = 18;
        public int ChartRenderBitrateMbps = ChartRenderBitratePresets.AutoBitrateMbps;
        public string ChartRenderPreset = "veryfast";
        public string ChartRenderEncoderMode = ChartRenderOptionValues.EncoderAutoBalanced;
        public string ChartRenderCaptureFormat = ChartRenderOptionValues.CaptureRgba;
        public string ChartRenderCaptureSource = ChartRenderOptionValues.CaptureSourceCamera;
        public string ChartRenderPreviewMode = ChartRenderOptionValues.PreviewFull;
        public string ChartRenderAudioFormat = ChartRenderOptionValues.AudioFormatAac;
        public string ChartRenderVideoFormat = ChartRenderOptionValues.VideoFormatMp4;
        public string ChartRenderVideoCodec = ChartRenderOptionValues.VideoCodecH264;
        public float ChartRenderCompletionTailSeconds = 5f;
        public float ChartRenderAudioSyncOffsetMs;
        public bool ChartRenderShowHitJudgments = true;
        public bool ChartRenderUseSelectedRange;
        public bool ChartRenderAdvancedSettingsExpanded;
        public bool ChartRenderProfessionalSettingsExpanded;
        public string ChartRenderCustomMuxArgs = string.Empty;

        public void EnsureDefaults(string? basePath = null)
        {
            if (string.IsNullOrWhiteSpace(basePath))
            {
                basePath = Path.Combine(Application.dataPath, "..", "Mods", "CheryTools", "Modules", "AetRecorder");
            }

            if (string.IsNullOrWhiteSpace(ChartRenderWorkspaceDirectory))
            {
                ChartRenderWorkspaceDirectory = Path.Combine(basePath, "Workspace");
            }

            if (string.IsNullOrWhiteSpace(ChartRenderExportDirectory))
            {
                string videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                ChartRenderExportDirectory = string.IsNullOrWhiteSpace(videos)
                    ? Path.Combine(ChartRenderWorkspaceDirectory, "Exports")
                    : Path.Combine(videos, "ADOFAI Renders");
            }

            Normalize();
        }

        public void Normalize()
        {
            ChartRenderWidth = MakeEven(Mathf.Clamp(ChartRenderWidth, MinSize, MaxWidth));
            ChartRenderHeight = MakeEven(Mathf.Clamp(ChartRenderHeight, MinSize, MaxHeight));
            ChartRenderFps = Mathf.Clamp(ChartRenderFps, 1, MaxFps);
            ChartRenderCrf = Mathf.Clamp(ChartRenderCrf, 0, 51);
            ChartRenderBitrateMbps = Mathf.Clamp(ChartRenderBitrateMbps, ChartRenderBitratePresets.AutoBitrateMbps, ChartRenderBitratePresets.MaxBitrateMbps);
            ChartRenderPreset = string.IsNullOrWhiteSpace(ChartRenderPreset) ? "veryfast" : ChartRenderPreset.Trim();
            ChartRenderEncoderMode = ChartRenderOptionValues.NormalizeEncoderMode(ChartRenderEncoderMode);
            ChartRenderCaptureFormat = ChartRenderOptionValues.NormalizeCaptureFormat(ChartRenderCaptureFormat);
            ChartRenderCaptureSource = ChartRenderOptionValues.NormalizeCaptureSource(ChartRenderCaptureSource);
            ChartRenderPreviewMode = ChartRenderOptionValues.NormalizePreviewMode(ChartRenderPreviewMode);
            ChartRenderAudioFormat = ChartRenderOptionValues.NormalizeAudioFormat(ChartRenderAudioFormat);
            ChartRenderVideoFormat = ChartRenderOptionValues.NormalizeVideoFormat(ChartRenderVideoFormat);
            ChartRenderVideoCodec = ChartRenderOptionValues.NormalizeVideoCodec(ChartRenderVideoCodec);
            ChartRenderCompletionTailSeconds = Mathf.Max(0f, ChartRenderCompletionTailSeconds);
            ChartRenderAudioSyncOffsetMs = Mathf.Clamp(ChartRenderAudioSyncOffsetMs, -5000f, 5000f);
            ChartRenderWorkspaceDirectory = ChartRenderWorkspaceDirectory ?? string.Empty;
            ChartRenderExportDirectory = ChartRenderExportDirectory ?? string.Empty;
            ChartRenderCustomMuxArgs = ChartRenderCustomMuxArgs ?? string.Empty;
            FfmpegCustomPath = FfmpegCustomPath ?? string.Empty;
        }

        public void ResetAllDefaults(string? basePath = null)
        {
            if (string.IsNullOrWhiteSpace(basePath))
            {
                basePath = Path.Combine(Application.dataPath, "..", "Mods", "CheryTools", "Modules", "AetRecorder");
            }

            FfmpegCustomPath = string.Empty;
            ChartRenderWorkspaceDirectory = Path.Combine(basePath, "Workspace");
            ChartRenderExportDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "ADOFAI Renders");
            ChartRenderWidth = 1920;
            ChartRenderHeight = 1080;
            ChartRenderFps = 60;
            ChartRenderCrf = 18;
            ChartRenderBitrateMbps = ChartRenderBitratePresets.AutoBitrateMbps;
            ChartRenderPreset = "veryfast";
            ChartRenderEncoderMode = ChartRenderOptionValues.EncoderAutoBalanced;
            ChartRenderCaptureFormat = ChartRenderOptionValues.CaptureRgba;
            ChartRenderCaptureSource = ChartRenderOptionValues.CaptureSourceCamera;
            ChartRenderPreviewMode = ChartRenderOptionValues.PreviewFull;
            ChartRenderAudioFormat = ChartRenderOptionValues.AudioFormatAac;
            ChartRenderVideoFormat = ChartRenderOptionValues.VideoFormatMp4;
            ChartRenderVideoCodec = ChartRenderOptionValues.VideoCodecH264;
            ChartRenderCompletionTailSeconds = 5f;
            ChartRenderAudioSyncOffsetMs = 0f;
            ChartRenderShowHitJudgments = true;
            ChartRenderUseSelectedRange = false;
            ChartRenderAdvancedSettingsExpanded = false;
            ChartRenderProfessionalSettingsExpanded = false;
            ChartRenderCustomMuxArgs = string.Empty;
            Normalize();
        }

        public static string Text(string key) { return Localization.Text(key); }

        public static Settings Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var settings = JsonConvert.DeserializeObject<Settings>(json);
                    if (settings != null)
                    {
                        settings.Normalize();
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Main.Log("Settings.Load error: " + ex.Message);
            }

            var fallback = new Settings();
            fallback.EnsureDefaults();
            return fallback;
        }

        public void Save(string path)
        {
            try
            {
                Normalize();
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Main.Log("Settings.Save error: " + ex.Message);
            }
        }

        private static int MakeEven(int value) { return value % 2 == 0 ? value : value + 1; }
    }
}
