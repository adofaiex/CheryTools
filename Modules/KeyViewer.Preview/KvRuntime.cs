using System;
using System.IO;
using CheryTools.Sonnet.Contracts;
using UnityEngine;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.KeyViewer.Preview
{
    internal static class KvRuntime
    {
        private static string _settingsPath;
        private static IUguiModuleContext _ugui;
        private static IModuleVisualContext _visuals;
        private static bool _initialized;
        private static bool _dirty;
        private static float _saveAt;
        private static int _layoutRevision;
        private static int _visualRevision;
        private static int _inputRevision;

        internal static KvSettings Settings { get; private set; }
        internal static IModuleLogger Logger { get; private set; }
        internal static KvRuntimeHost Host { get; private set; }
        internal static IModuleVisualContext Visuals => _visuals;
        internal static string ModuleDirectory { get; private set; }
        internal static int LayoutRevision => _layoutRevision;
        internal static int VisualRevision => _visualRevision;
        internal static int InputRevision => _inputRevision;
        internal static bool IsActive => _initialized && _ugui?.Root != null && _ugui.Root.gameObject.activeInHierarchy;

        internal static void Initialize(ModuleContext context)
        {
            Logger = context.Logger;
            _ugui = context.Ugui;
            _visuals = context.Visuals;
            ModuleDirectory = context.ModuleDirectory;
            _settingsPath = Path.Combine(context.ModuleDirectory, "CheryTools.KeyViewer.Preview.xml");
            if (!File.Exists(_settingsPath))
            {
                string defaultPkg = ResolveDefaultPackage(context, "DefaultKeyViewer.ctkv", "*KV*.ctkv");
                if (!string.IsNullOrEmpty(defaultPkg))
                {
                    Settings = new KvSettings();
                    if (KvPackageService.ImportFromFile(Settings, defaultPkg, null, true))
                    {
                        Settings.Save(_settingsPath, Logger);
                        Logger?.Info(L("已应用 KeyViewer 默认配置：", "Applied KeyViewer default profile: ") + Path.GetFileName(defaultPkg));
                    }
                    else
                    {
                        Settings = null;
                    }
                }
            }
            if (Settings == null)
            {
                Settings = KvSettings.Load(_settingsPath, Logger);
                Settings.Normalize();
            }
            if (Settings.ClearStatsOnLaunch) Settings.ClearCounts();
            GameObject hostObject = context.Ugui.CreateObject("KeyViewerPreviewRuntime");
            Host = hostObject.AddComponent<KvRuntimeHost>();
            _layoutRevision++;
            _visualRevision++;
            _inputRevision++;
            _initialized = true;
            Logger?.Info(L("KeyViewer Preview 0.1.0 已加载", "KeyViewer Preview 0.1.0 loaded"));
        }

        internal static void InvalidateVisuals()
        {
            _visualRevision++;
            Host?.InvalidateAll();
        }

        internal static void Tick()
        {
            if (!_initialized) return;
            if (_dirty && !IsPlaying() && Time.unscaledTime >= _saveAt) SaveNow();
        }

        internal static void Changed(bool layoutChanged = true)
        {
            // Continuous ImGui drags can fire every frame. Full normalization
            // scans every profile/node and allocates ID sets, so reserve it for
            // structural edits and import paths. Widgets already clamp visual values.
            if (layoutChanged)
            {
                Settings.Normalize();
                _layoutRevision++;
            }
            _visualRevision++;
            _dirty = true;
            _saveAt = Time.unscaledTime + 0.6f;
        }

        internal static void CountsChanged()
        {
            _dirty = true;
            _saveAt = Time.unscaledTime + 2f;
        }

        internal static void InputChanged()
        {
            _inputRevision++;
            _visualRevision++;
            _dirty = true;
            _saveAt = Time.unscaledTime + 0.6f;
        }

        internal static bool IsPlaying()
        {
            try
            {
                return scrController.instance != null && scrController.instance.gameworld && !scrController.instance.paused;
            }
            catch { return false; }
        }

        internal static void Shutdown()
        {
            if (!_initialized) return;
            SaveNow();
            Host?.ClearRuntime();
            Host = null;
            Settings = null;
            _ugui = null;
            _visuals = null;
            ModuleDirectory = null;
            _initialized = false;
        }

        private static void SaveNow()
        {
            if (Settings == null || string.IsNullOrEmpty(_settingsPath)) return;
            Settings.Save(_settingsPath, Logger);
            _dirty = false;
        }

        private static string ResolveDefaultPackage(ModuleContext context, string preferredName, string pattern)
        {
            try
            {
                string[] searchDirs = new[]
                {
                    Path.Combine(context.ModuleDirectory, "..", "Defaults"),
                    Path.Combine(context.ModuleDirectory, "Defaults"),
                    Path.Combine(context.ModuleDirectory, "..", "Package", "Defaults")
                };

                foreach (string dir in searchDirs)
                {
                    if (!Directory.Exists(dir)) continue;
                    string exact = Path.Combine(dir, preferredName);
                    if (File.Exists(exact)) return Path.GetFullPath(exact);

                    var matches = Directory.GetFiles(dir, pattern);
                    if (matches.Length > 0) return Path.GetFullPath(matches[0]);
                }
            }
            catch { }
            return null;
        }
    }
}
