using System;
using System.IO;
using System.Threading;
using ADOFAI.EditorTweaks.Api.Rendering;
using ADOFAI.EditorTweaks.ChartRendering;
using ADOFAI.EditorTweaks.ChartRendering.Features.ChartRendering;
using ADOFAI.EditorTweaks.ChartRendering.Patching;
using CheryTools.Sonnet.Contracts;
using UnityEngine;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.AetRecorder.Preview
{
    internal static class AetRecorderRuntime
    {
        private static string _settingsPath = string.Empty;
        private static bool _dirty;
        private static float _saveAt;

        internal static Settings Settings { get; private set; } = new Settings();
        internal static IModuleLogger? Logger { get; private set; }
        internal static string ModuleDirectory { get; private set; } = string.Empty;
        internal static bool IsInitialized { get; private set; }

        internal static void Initialize(ModuleContext context)
        {
            Logger = context.Logger;
            ModuleDirectory = context.ModuleDirectory;
            _settingsPath = Path.Combine(context.ModuleDirectory, "CheryTools.AetRecorder.Preview.json");

            Settings = Settings.Load(_settingsPath);
            Settings.EnsureDefaults(ModuleDirectory);

            Main.UnityThreadId = Thread.CurrentThread.ManagedThreadId;
            Main.Settings = Settings;
            Main.IsLoaded = true;

            PatchManager.ApplyAll("cherytools.sonnet.aetrecorder");
            if (PatchManager.IsAvailable(PatchFeature.ChartRendering))
            {
                ChartRenderService.Ensure();
                Logger?.Info(L("AetRecorder 渲染服务已就绪", "AetRecorder render service ready"));
            }
            else
            {
                ChartRenderService.Destroy();
                ChartRenderAvailability availability = ChartRenderService.GetAvailability();
                Logger?.Error(L("AetRecorder 补丁加载失败: ", "AetRecorder patches failed to load: ") + availability.Message);
            }

            IsInitialized = true;
            Logger?.Info(L("AET Recorder 0.1.0 (Chart Rendering Engine) 已初始化", "AET Recorder 0.1.0 (Chart Rendering Engine) initialized"));
        }

        internal static void Tick()
        {
            if (_dirty && Time.unscaledTime >= _saveAt)
            {
                SaveNow();
            }
        }

        internal static void SettingsChanged()
        {
            _dirty = true;
            _saveAt = Time.unscaledTime + 0.6f;
        }

        internal static void Shutdown()
        {
            SaveNow();

            if (ChartRenderService.IsActive)
            {
                try
                {
                    ChartRenderApi.CurrentTask?.Cancel();
                }
                catch
                {
                }
            }

            ChartRenderService.Destroy();
            PatchManager.UnpatchAll();
            Main.IsLoaded = false;
            IsInitialized = false;

            Logger = null;
            ModuleDirectory = string.Empty;
        }

        private static void SaveNow()
        {
            if (Settings == null || string.IsNullOrEmpty(_settingsPath)) return;
            Settings.Save(_settingsPath);
            _dirty = false;
        }
    }
}
