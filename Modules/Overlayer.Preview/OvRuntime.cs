using System.IO;
using CheryTools.Sonnet.Contracts;
using UnityEngine;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.Overlayer.Preview
{
    internal static class OvRuntime
    {
        private static string _settingsPath;
        private static bool _dirty;
        private static float _saveAt;

        internal static OvSettings Settings { get; private set; }
        internal static IModuleLogger Logger { get; private set; }
        internal static IUguiModuleContext Ugui { get; private set; }
        internal static IModuleVisualContext Visuals { get; private set; }
        internal static OvRuntimeHost Host { get; private set; }
        internal static string ModuleDirectory { get; private set; }
        internal static int LayoutRevision { get; private set; }
        internal static int VisualRevision { get; private set; }

        internal static void Initialize(ModuleContext context)
        {
            Logger = context.Logger;
            Ugui = context.Ugui;
            Visuals = context.Visuals;
            ModuleDirectory = context.ModuleDirectory;
            _settingsPath = Path.Combine(context.ModuleDirectory, "CheryTools.Overlayer.Preview.xml");
            if (!File.Exists(_settingsPath))
            {
                string defaultPkg = ResolveDefaultPackage(context, "DefaultOverlayer.ctov", "*OV*.ctov");
                if (!string.IsNullOrEmpty(defaultPkg))
                {
                    Settings = new OvSettings();
                    bool legacy;
                    if (OvPackageService.Read(Settings, defaultPkg, out legacy))
                    {
                        Settings.Normalize();
                        Settings.Save(_settingsPath, Logger);
                        Logger?.Info(L("已应用 Overlayer 默认配置：", "Applied Overlayer default settings: ") + Path.GetFileName(defaultPkg));
                    }
                    else
                    {
                        Settings = null;
                    }
                }
            }
            if (Settings == null)
            {
                Settings = OvSettings.Load(_settingsPath, Logger);
                Settings.Normalize();
            }
            GameObject hostObject = context.Ugui.CreateObject("OverlayerPreviewRuntime");
            Host = hostObject.AddComponent<OvRuntimeHost>();
            LayoutRevision++;
            VisualRevision++;
            Logger?.Info(L("Overlayer Preview 0.1.0 已加载", "Overlayer Preview 0.1.0 loaded"));
        }

        internal static void Tick()
        {
            if (_dirty && Time.unscaledTime >= _saveAt) SaveNow();
        }

        internal static void Changed(bool layoutChanged = false)
        {
            // Continuous ImGui drags are visual updates. Full normalization walks
            // every component and allocates ID sets, so reserve it for add/remove,
            // media/font recreation and import operations.
            if (layoutChanged)
            {
                Settings?.Normalize();
                LayoutRevision++;
            }
            VisualRevision++;
            _dirty = true;
            _saveAt = Time.unscaledTime + 0.6f;
        }

        internal static string ResolvePreviewText(string format)
        {
            if (string.IsNullOrEmpty(format)) return string.Empty;
            return Host != null ? Host.ResolveText(format) : format;
        }

        internal static void Shutdown()
        {
            SaveNow();
            Host?.ClearRuntime();
            Host = null;
            Ugui?.Clear();
            Settings = null;
            Ugui = null;
            Visuals = null;
            Logger = null;
            ModuleDirectory = null;
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
