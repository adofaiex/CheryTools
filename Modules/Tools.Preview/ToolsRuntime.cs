using System;
using System.IO;
using System.Reflection;
using CheryTools.Sonnet.Contracts;
using HarmonyLib;
using UnityEngine;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.Tools.Preview
{
    internal static class ToolsRuntime
    {
        private const string HarmonyId = "cn.cherysui.cherytools.sonnet.tools.preview";
        private static Harmony _harmony;
        private static IUguiModuleContext _ugui;
        private static string _settingsPath;
        private static bool _initialized;
        private static bool _dirty;
        private static float _saveAt;
        private static float _nextVisualRefresh;

        internal static ToolsSettings Settings { get; private set; }
        internal static IModuleLogger Logger { get; private set; }
        internal static bool IsActive => _initialized;

        internal static void Initialize(ModuleContext context)
        {
            Logger = context.Logger;
            _ugui = context.Ugui;
            _settingsPath = Path.Combine(context.ModuleDirectory, "CheryTools.Tools.Preview.xml");
            Settings = ToolsSettings.Load(_settingsPath, Logger);
            Settings.Normalize();

            _harmony = new Harmony(HarmonyId);
            _harmony.PatchAll(typeof(ToolsRuntime).Assembly);
            ToolsInputRuntime.Rebuild();
            _initialized = true;
            _nextVisualRefresh = 0f;
            Logger?.Info(L("Tools Preview 0.1.0 已加载", "Tools Preview 0.1.0 loaded"));
        }

        internal static void Tick()
        {
            if (!_initialized) return;
            if (_dirty && Time.unscaledTime >= _saveAt) SaveNow();
            if (IsActive && (Settings.EnableCustomPlanetColors || Settings.EnablePlanetOpacity) &&
                Time.unscaledTime >= _nextVisualRefresh)
            {
                _nextVisualRefresh = Time.unscaledTime + 1f;
                ToolsVisualRuntime.Refresh();
            }

            // UI visibility is deliberately owned by the module runtime rather than
            // the ImGui layer. The module UI may be closed while the selected profile
            // still needs to be active in-game. Editor display updates are driven by
            // the scnEditor Harmony patch below; running them here as well would do
            // the same work twice per frame.
            ToolsUiVisibilityRuntime.Tick();
        }

        internal static void SettingsChanged(bool inputChanged = false, bool visualChanged = false)
        {
            Settings.Normalize();
            _dirty = true;
            _saveAt = Time.unscaledTime + 0.35f;
            if (inputChanged) ToolsInputRuntime.Rebuild();
            if (visualChanged)
            {
                ToolsVisualRuntime.Refresh();
            }
        }

        internal static void Shutdown()
        {
            if (!_initialized) return;
            ToolsUiVisibilityRuntime.Restore();
            ToolsEditorRuntime.Restore();
            ToolsVisualRuntime.RestoreDefaultColors();
            SaveNow();
            try { _harmony?.UnpatchAll(HarmonyId); }
            catch (Exception ex) { Logger?.Warning(L("Tools Preview 补丁卸载失败：", "Failed to remove Tools Preview patches: ") + ex.Message); }
            ToolsInputRuntime.Reset();
            _harmony = null;
            _ugui = null;
            _initialized = false;
        }

        private static void SaveNow()
        {
            if (Settings == null || string.IsNullOrEmpty(_settingsPath)) return;
            Settings.Save(_settingsPath, Logger);
            _dirty = false;
        }
    }

    internal static class EditorRuntimeCompatibility
    {
        private static readonly Func<scnEditor, bool> PlayModeGetter = CreatePlayModeGetter();

        internal static bool IsPlayMode(scnEditor editor)
        {
            if (editor == null) return false;
            try
            {
                return PlayModeGetter != null
                    ? PlayModeGetter(editor)
                    : scrController.instance != null && scrController.instance.gameworld && !scrController.instance.paused;
            }
            catch
            {
                return scrController.instance != null && scrController.instance.gameworld && !scrController.instance.paused;
            }
        }

        internal static bool IsMouseWheelZoom(float requestedDelta)
        {
            Vector2 wheel = RDInput.mouseScrollDelta;
            return Mathf.Abs(wheel.y) > 0.05f && Mathf.Abs(requestedDelta - wheel.y) < 0.0001f;
        }

        private static Func<scnEditor, bool> CreatePlayModeGetter()
        {
            PropertyInfo property = AccessTools.Property(typeof(scnEditor), "playMode");
            MethodInfo getter = property?.GetGetMethod(true);
            if (getter != null && getter.ReturnType == typeof(bool))
            {
                try { return (Func<scnEditor, bool>)Delegate.CreateDelegate(typeof(Func<scnEditor, bool>), null, getter); }
                catch { }
            }
            FieldInfo field = AccessTools.Field(typeof(scnEditor), "playMode");
            return field != null && field.FieldType == typeof(bool) ? editor => (bool)field.GetValue(editor) : null;
        }
    }
}
