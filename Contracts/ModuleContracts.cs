using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;

namespace CheryTools.Sonnet.Contracts
{
    public interface IModuleLogger
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message);
    }

    public sealed class ModuleMetadata
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Version { get; }
        public string Category { get; }
        public string NavigationTitle { get; set; } = "功能列表";
        // English navigation title; when null the Chinese one is shown in both languages.
        public string NavigationTitleEn { get; set; } = "Features";
        public string DisplayNavigationTitle => SonnetLanguage.IsChinese || string.IsNullOrWhiteSpace(NavigationTitleEn)
            ? NavigationTitle : NavigationTitleEn;
        public NumVector4 AccentColor { get; }
        public int ApiVersion { get; }

        public ModuleMetadata(string id, string displayName, string version = "1.0.0", string category = "模块", NumVector4? accentColor = null, int apiVersion = ModuleApi.CurrentVersion)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "module" : id.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
            Version = string.IsNullOrWhiteSpace(version) ? "1.0.0" : version.Trim();
            Category = string.IsNullOrWhiteSpace(category) ? "模块" : category.Trim();
            AccentColor = accentColor ?? new NumVector4(0.28f, 0.78f, 1f, 1f);
            ApiVersion = apiVersion;
        }
    }

    public sealed class ModuleTab
    {
        public string Id { get; }
        public string Title { get; }
        // English title; when null the Chinese one is shown in both languages.
        public string TitleEn { get; }
        public string DisplayTitle => SonnetLanguage.IsChinese || TitleEn == null ? Title : TitleEn;

        public ModuleTab(string id, string title, string titleEn = null)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "main" : id.Trim();
            Title = string.IsNullOrWhiteSpace(title) ? Id : title.Trim();
            TitleEn = string.IsNullOrWhiteSpace(titleEn) ? null : titleEn.Trim();
        }
    }

    public static class ModuleApi
    {
        public const int CurrentVersion = 1;
    }

    public interface ICheryModule
    {
        ModuleMetadata Metadata { get; }
        IReadOnlyList<ModuleTab> Tabs { get; }
        void Initialize(ModuleContext context);
        void Update();
        void Draw(ModuleUiContext context);
        void Shutdown();
    }

    // Optional interface for editors/tool windows that must keep rendering independently
    // from the Framework shell (for example FreeMake).
    public interface ICheryModuleOverlay
    {
        bool HasOpenOverlay { get; }
        void DrawOverlay();
    }

    public sealed class ModuleContext
    {
        public ModuleMetadata Metadata { get; }
        public string ModuleDirectory { get; }
        public IModuleLogger Logger { get; }
        public IUguiModuleContext Ugui { get; }
        public IModuleVisualContext Visuals { get; }

        public ModuleContext(ModuleMetadata metadata, string moduleDirectory, IModuleLogger logger, IUguiModuleContext ugui)
            : this(metadata, moduleDirectory, logger, ugui, null)
        {
        }

        public ModuleContext(ModuleMetadata metadata, string moduleDirectory, IModuleLogger logger, IUguiModuleContext ugui,
            IModuleVisualContext visuals)
        {
            Metadata = metadata;
            ModuleDirectory = moduleDirectory ?? string.Empty;
            Logger = logger;
            Ugui = ugui;
            Visuals = visuals;
        }
    }

    public struct ModuleBackgroundInfo
    {
        public IntPtr TextureId { get; }
        public int TextureWidth { get; }
        public int TextureHeight { get; }
        public float ImageOpacity { get; }
        public float PanelOpacity { get; }
        public bool FlipY { get; }

        public ModuleBackgroundInfo(IntPtr textureId, int textureWidth, int textureHeight, float imageOpacity,
            float panelOpacity, bool flipY)
        {
            TextureId = textureId;
            TextureWidth = textureWidth;
            TextureHeight = textureHeight;
            ImageOpacity = imageOpacity;
            PanelOpacity = panelOpacity;
            FlipY = flipY;
        }
    }

    public interface IModuleVisualContext
    {
        bool TryGetPanelBackground(out ModuleBackgroundInfo background);

        // ImGui texture id for a texture a module wants to draw in its own ImGui windows.
        IntPtr RegisterTexture(Texture texture);
        void UnregisterTexture(Texture texture);
    }

    public sealed class ModuleUiContext
    {
        public ModuleMetadata Metadata { get; }
        public string ActiveTabId { get; }
        public NumVector2 ContentSize { get; }

        public ModuleUiContext(ModuleMetadata metadata, string activeTabId, NumVector2 contentSize)
        {
            Metadata = metadata;
            ActiveTabId = activeTabId ?? string.Empty;
            ContentSize = contentSize;
        }
    }

    public interface IUguiModuleContext
    {
        Canvas Canvas { get; }
        RectTransform Root { get; }
        GameObject CreateObject(string name);
        Text CreateText(string name, string text);
        void Clear();
    }

    public sealed class DelegateModuleLogger : IModuleLogger
    {
        private readonly Action<string> _info;
        private readonly Action<string> _warning;
        private readonly Action<string> _error;

        public DelegateModuleLogger(Action<string> info, Action<string> warning, Action<string> error)
        {
            _info = info;
            _warning = warning ?? info;
            _error = error ?? info;
        }

        public void Info(string message) => _info?.Invoke(message ?? string.Empty);
        public void Warning(string message) => _warning?.Invoke(message ?? string.Empty);
        public void Error(string message) => _error?.Invoke(message ?? string.Empty);
    }
}
