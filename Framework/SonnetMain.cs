using System;
using System.IO;
using CheryTools.Sonnet.Contracts;
using UnityEngine;
using UnityModManagerNet;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.Sonnet.Framework
{
    public static class SonnetMain
    {
        public const string ModId = "CheryTools";
        public const string DisplayName = "CheryTools";
        // Release version from Directory.Build.props (CheryToolsVersion), stamped into the assembly.
        public static readonly string DisplayVersion = ReadVersion();
        // Release name from Directory.Build.props (CheryToolsReleaseName), e.g. "26.5 Preview".
        public static readonly string ReleaseName = ReadMetadata("CheryToolsReleaseName", DisplayVersion);
        public static bool IsEnabled { get; private set; }
        public static bool IsPanelOpen { get; internal set; }
        /// <summary>
        /// True while the Framework shell or one of its ImGui overlay windows is
        /// visible.  This is intentionally owned by the Framework rather than a
        /// module so game-input isolation remains a bottom-layer feature.
        /// </summary>
        public static bool IsAnyImGuiWindowOpen => IsPanelOpen || _hasOpenOverlay;
        internal static KeyCode PanelShortcut => Settings?.PanelShortcut ?? KeyCode.Insert;
        public static IModuleLogger Logger { get; private set; }
        public static string FrameworkDirectory { get; private set; }
        public static FrameworkSettings Settings { get; private set; }
        internal static SonnetModuleLoader Modules { get; private set; }
        private static GameObject _hostObject;
        private static bool _initialized;
        private static bool _hasOpenOverlay;

        private static string ReadVersion()
        {
            var attribute = (System.Reflection.AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                typeof(SonnetMain).Assembly, typeof(System.Reflection.AssemblyInformationalVersionAttribute));
            return string.IsNullOrWhiteSpace(attribute?.InformationalVersion) ? "dev" : attribute.InformationalVersion;
        }

        private static int _nextLanguageCheckFrame;
        private static System.Reflection.FieldInfo _gameLanguageField;
        private static bool _gameLanguageResolved;

        // Resolves FrameworkSettings.Language into SonnetLanguage.IsChinese. "auto" follows the game's own
        // language (RDString.language, read by reflection since the Framework does not reference
        // Assembly-CSharp). The game sets it after mods load and the player can change it at any time,
        // so it is re-read about twice a second.
        internal static void UpdateLanguage(bool force = false)
        {
            int frame = Time.frameCount;
            if (!force && frame < _nextLanguageCheckFrame) return;
            _nextLanguageCheckFrame = frame + 30;
            string choice = Settings?.Language;
            if (choice == FrameworkSettings.LanguageChinese) SonnetLanguage.IsChinese = true;
            else if (choice == FrameworkSettings.LanguageEnglish) SonnetLanguage.IsChinese = false;
            else SonnetLanguage.IsChinese = IsChineseLanguage(GameLanguage());
        }

        private static SystemLanguage GameLanguage()
        {
            if (!_gameLanguageResolved)
            {
                _gameLanguageResolved = true;
                _gameLanguageField = Type.GetType("RDString, Assembly-CSharp", false)?.GetField("language",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            }
            try
            {
                if (_gameLanguageField?.GetValue(null) is SystemLanguage language) return language;
            }
            catch
            {
            }
            return Application.systemLanguage;
        }

        private static bool IsChineseLanguage(SystemLanguage language)
        {
            return language == SystemLanguage.Chinese || language == SystemLanguage.ChineseSimplified
                || language == SystemLanguage.ChineseTraditional;
        }

        private static string ReadMetadata(string key, string fallback)
        {
            foreach (System.Reflection.AssemblyMetadataAttribute attribute in typeof(SonnetMain).Assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false))
            {
                if (attribute.Key == key && !string.IsNullOrWhiteSpace(attribute.Value)) return attribute.Value;
            }
            return fallback;
        }

        public static bool Initialize(UnityModManager.ModEntry entry)
        {
            if (_initialized) return true;
            Logger = new DelegateModuleLogger(entry.Logger.Log, entry.Logger.Warning, entry.Logger.Error);
            FrameworkDirectory = entry.Path;
            Settings = FrameworkSettings.Load(Path.Combine(entry.Path, "Framework.xml"), Logger);
            UpdateLanguage(force: true);
            IsPanelOpen = Settings.PanelOpen;
            IsEnabled = true;
            _hasOpenOverlay = false;
            SonnetInputBlocker.Initialize(Logger);
            NativeDependencyLoader.Load(entry.Path, Logger);
            _hostObject = new GameObject("CheryTools_Sonnet_Framework");
            UnityEngine.Object.DontDestroyOnLoad(_hostObject);
            _hostObject.AddComponent<SonnetImGuiController>();
            _initialized = true;
            return true;
        }

        internal static void AttachCanvas(Canvas canvas)
        {
            if (Modules != null) return;
            Modules = new SonnetModuleLoader(FrameworkDirectory, canvas, Logger);
            Modules.LoadAll();
            Modules.ApplyDisplayOrder(Settings?.ModuleOrder);
            if (Settings != null) Settings.ModuleOrder = Modules.GetDisplayOrder();
            Modules.SetRuntimeActive(IsEnabled);
        }

        internal static void SetOverlayState(bool open)
        {
            _hasOpenOverlay = open;
        }

        public static bool SetEnabled(bool enabled)
        {
            if (!_initialized) return false;
            IsEnabled = enabled;
            if (!enabled) IsPanelOpen = false;
            Modules?.SetRuntimeActive(enabled);
            return true;
        }

        public static void DrawLoaderGui()
        {
            GUILayout.Label("CheryTools Sonnet " + DisplayVersion + L(" 模块化框架", " modular framework"));
            if (GUILayout.Button(IsPanelOpen ? L("关闭框架面板", "Close framework panel") : L("打开框架面板", "Open framework panel"), GUILayout.Width(220f)))
                IsPanelOpen = !IsPanelOpen;
            GUILayout.Label(L("游戏内快捷键：Insert", "In-game shortcut: Insert"));
        }

        public static void SaveSettings()
        {
            if (Settings == null) return;
            Settings.PanelOpen = IsPanelOpen;
            if (Modules != null) Settings.ModuleOrder = Modules.GetDisplayOrder();
            Settings.Save(Path.Combine(FrameworkDirectory ?? string.Empty, "Framework.xml"), Logger);
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            SaveSettings();
            if (Modules != null) Modules.Shutdown();
            Modules = null;
            _hasOpenOverlay = false;
            SonnetInputBlocker.Shutdown();
            if (_hostObject != null) UnityEngine.Object.Destroy(_hostObject);
            _hostObject = null;
            IsEnabled = false;
            _initialized = false;
        }
    }

    public static class EntryPoint
    {
        public static bool Load(UnityModManager.ModEntry entry)
        {
            if (!SonnetMain.Initialize(entry)) return false;
            entry.OnToggle = (_, value) => SonnetMain.SetEnabled(value);
            entry.OnGUI = _ => SonnetMain.DrawLoaderGui();
            entry.OnSaveGUI = _ => SonnetMain.SaveSettings();
            entry.OnUnload = _ => { SonnetMain.Shutdown(); return true; };
            return true;
        }
    }
}
