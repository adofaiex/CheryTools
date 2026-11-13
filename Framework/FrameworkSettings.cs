using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.Sonnet.Framework
{
    [Serializable]
    public sealed class FrameworkSettings
    {
        public bool PanelOpen;
        public KeyCode PanelShortcut = KeyCode.Insert;
        public float UiScale = 1.0f;
        // Whole-panel dimming layer over the background image (0 = none).
        public float BackgroundOpacity = SonnetTheme.DefaultMaskOpacity;
        // Dimming of the sidebar and content cards, on top of BackgroundOpacity.
        public float CardOpacity = SonnetTheme.DefaultCardOpacity;
        public float[] AccentColor = SonnetTheme.DefaultAccent;
        public float[] MaskTint = SonnetTheme.DefaultTint;
        // 0 = flat mask, 1 = vignette (dark edges, clearer center).
        public int MaskStyle = SonnetTheme.DefaultMaskStyle;
        // Sidebar and content cards show a blurred copy of the background behind them.
        public bool FrostedCards;
        public bool EnableBackgroundImage = true;
        public string BackgroundImagePath = DefaultBackgroundImagePath;
        public float BackgroundImageOpacity = 1.0f;
        public bool BlurBackgroundImage = false;
        public float BackgroundImageBlurStrength = 1f;
        public bool HasCustomPanelPosition;
        public float PanelPositionX;
        public float PanelPositionY;
        public bool HasCustomPanelSize;
        public float PanelWidth;
        public float PanelHeight;
        public string SelectedModuleId = "";
        public string SelectedTabId = "";
        // 仅控制框架侧栏的显示顺序，不改变模块 DLL 的加载、Update 或 Overlay 顺序。
        public List<string> ModuleOrder = new List<string>();
        // Panel header lines. Empty means the default ("CheryTools <release name>" and the tagline);
        // storing a concrete default would freeze the header at whatever version first saved the file.
        public string BrandTitle = "";
        public string BrandSubtitle = "";
        // Read only, for migration: up to 26w43a the header showed a separate version label after the
        // title. It is folded into BrandTitle on load and never written back.
        public string BrandVersion;
        public bool EnableUiAnimations = true;
        // UI language: LanguageAuto follows the game's language, the others force one.
        public string Language = LanguageAuto;
        // Loads DLLs in Modules\ that are not official modules. Read once at startup (see ModuleIntegrity).
        public bool AllowThirdPartyModules;

        internal const string LanguageAuto = "auto";
        internal const string LanguageChinese = "zh";
        internal const string LanguageEnglish = "en";

        public bool ShouldSerializeBrandVersion() => false;

        // Old builds wrote their own build code (e.g. "26w41c") as the version label default.
        private static readonly System.Text.RegularExpressions.Regex LegacyBuildCode =
            new System.Text.RegularExpressions.Regex(@"^\d{2}w\d{2}[a-z]$");
        private const string LegacyDefaultBrandTitle = "CheryTools";
        internal const string DefaultBackgroundImagePath = "Resources\\CyreneBackground.png";
        // Background shipped as the default up to 26w43a; a stored copy of it follows the new default.
        private const string LegacyDefaultBackgroundImagePath = "Resources\\XuanlingBackground.png";

        private static void MigrateBackground(FrameworkSettings settings)
        {
            string path = (settings.BackgroundImagePath ?? string.Empty).Trim().Replace('/', '\\');
            if (string.Equals(path, LegacyDefaultBackgroundImagePath, StringComparison.OrdinalIgnoreCase))
                settings.BackgroundImagePath = DefaultBackgroundImagePath;
        }

        private static void MigrateBrand(FrameworkSettings settings)
        {
            string title = settings.BrandTitle?.Trim() ?? string.Empty;
            string version = settings.BrandVersion?.Trim() ?? string.Empty;
            if (LegacyBuildCode.IsMatch(version)) version = string.Empty;
            if (title == LegacyDefaultBrandTitle) title = string.Empty;
            if (version.Length > 0) title = (title.Length > 0 ? title : LegacyDefaultBrandTitle) + " " + version;
            settings.BrandTitle = title;
            settings.BrandSubtitle = settings.BrandSubtitle ?? string.Empty;
            settings.BrandVersion = null;
        }

        public static FrameworkSettings Load(string path, CheryTools.Sonnet.Contracts.IModuleLogger logger)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return new FrameworkSettings();
            try
            {
                var serializer = new XmlSerializer(typeof(FrameworkSettings));
                FrameworkSettings settings;
                using (var stream = File.OpenRead(path))
                    settings = serializer.Deserialize(stream) as FrameworkSettings ?? new FrameworkSettings();
                MigrateBrand(settings);
                MigrateBackground(settings);
                return settings;
            }
            catch (Exception ex)
            {
                logger?.Warning(L("无法读取 Framework.xml，使用默认设置：", "Could not read Framework.xml, using defaults: ") + ex.Message);
                return new FrameworkSettings();
            }
        }

        public void Save(string path, CheryTools.Sonnet.Contracts.IModuleLogger logger)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                var serializer = new XmlSerializer(typeof(FrameworkSettings));
                using (var stream = File.Create(path)) serializer.Serialize(stream, this);
            }
            catch (Exception ex)
            {
                logger?.Warning(L("无法保存 Framework.xml：", "Could not save Framework.xml: ") + ex.Message);
            }
        }
    }
}
