using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Reflection;
using CheryTools.Sonnet.Contracts;
using Newtonsoft.Json;
using UnityEngine;
using SysCompressionLevel = System.IO.Compression.CompressionLevel;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.Overlayer.Preview
{
    [Serializable, XmlRoot("CheryToolsSonnetOverlayer")]
    public sealed class OvTransferPackage
    {
        public int FormatVersion = 1;
        public string Kind = "Overlayer";
        public string ExportedAt = string.Empty;
        public int ScreenWidth;
        public int ScreenHeight;
        public List<OvTextComponent> Texts = new List<OvTextComponent>();
        public List<OvImageComponent> Images = new List<OvImageComponent>();
        public List<OvVideoComponent> Videos = new List<OvVideoComponent>();
        public List<OvProgressComponent> ProgressBars = new List<OvProgressComponent>();
    }

    [Serializable, XmlRoot("OverlayerPackage")]
    public sealed class OvLegacyPackage
    {
        public int FormatVersion;
        public string ExportedAt = string.Empty;
        public int ExportScreenWidth;
        public int ExportScreenHeight;
        [XmlArrayItem("OverlayerText")]
        public List<LegacyOverlayerText> OverlayerTexts;
        [XmlArrayItem("OverlayerImage")]
        public List<LegacyOverlayerImage> OverlayerImages;
        [XmlArrayItem("OverlayerVideo")]
        public List<LegacyOverlayerVideo> OverlayerVideos;
        [XmlArrayItem("OverlayerProgressBar")]
        public List<LegacyOverlayerProgressBar> OverlayerProgressBars;
    }

    [Serializable, XmlRoot("Settings")]
    public sealed class LegacyCytSettingsOvPackage
    {
        public int LastKnownScreenWidth;
        public int LastKnownScreenHeight;
        [XmlArrayItem("OverlayerText")]
        public List<LegacyOverlayerText> OverlayerTexts = new List<LegacyOverlayerText>();
        [XmlArrayItem("OverlayerImage")]
        public List<LegacyOverlayerImage> OverlayerImages = new List<LegacyOverlayerImage>();
        [XmlArrayItem("OverlayerVideo")]
        public List<LegacyOverlayerVideo> OverlayerVideos = new List<LegacyOverlayerVideo>();
        [XmlArrayItem("OverlayerProgressBar")]
        public List<LegacyOverlayerProgressBar> OverlayerProgressBars = new List<LegacyOverlayerProgressBar>();
    }

    [Serializable, XmlRoot("CytPackageInfo")]
    public sealed class LegacyCytPackageInfo
    {
        public int FormatVersion = 1;
        public int ExportScreenWidth;
        public int ExportScreenHeight;
    }

    [Serializable]
    public sealed class LegacyOverlayerText
    {
        public string Name = string.Empty;
        public bool IsEnabled = true;
        public bool ShowInGame = true;
        public bool OnlyShowPlaying;
        public string TextFormat = string.Empty;
        public float PositionX = 50f;
        public float PositionY = 50f;
        public float FontSize = 32f;
        public float[] TextColor = new float[] { 1f, 1f, 1f, 1f };
        public int Alignment;
        public string FontPath = string.Empty;
        public int Depth;
        public bool EnableShadow;
        public float[] ShadowColor = new float[] { 0f, 0f, 0f, 1f };
        public float[] ShadowOffset = new float[] { 2f, 2f };
        public float ShadowSoftness;
        public bool EnableOutline;
        public float[] OutlineColor = new float[] { 0f, 0f, 0f, 1f };
        public float OutlineThickness = 1f;
        public float LineHeightOffset;
        public float LetterSpacing;
        public float PivotX;
        public float PivotY;
        public string TokenSourceSnapshot = string.Empty;
        public List<OvTextTokenBinding> TokenBindings = new List<OvTextTokenBinding>();
        public OvAnimationGraph TokenAnimation;
        [XmlArrayItem("OverlayerAnimation")]
        public List<LegacyOverlayerAnimation> Animations = new List<LegacyOverlayerAnimation>();
    }

    [Serializable]
    public sealed class LegacyOverlayerImage
    {
        public string Name = string.Empty;
        public bool IsEnabled = true;
        public bool ShowInGame = true;
        public bool OnlyShowPlaying;
        public string ImagePath = string.Empty;
        public float PositionX = 200f;
        public float PositionY = 200f;
        public float Scale = 1f;
        public float Rotation;
        public float Opacity = 1f;
        public int Depth;
        public float PivotX;
        public float PivotY;
        public OvAnimationGraph NodeAnimation;
        [XmlArrayItem("OverlayerAnimation")]
        public List<LegacyOverlayerAnimation> Animations = new List<LegacyOverlayerAnimation>();
    }

    [Serializable]
    public sealed class LegacyOverlayerVideo
    {
        public string Name = string.Empty;
        public bool IsEnabled = true;
        public bool ShowInGame = true;
        public bool OnlyShowPlaying;
        public string VideoPath = string.Empty;
        public bool Loop = true;
        public float ContentScale = 1f;
        public float ContentOffsetX;
        public float ContentOffsetY;
        public float PositionX = 200f;
        public float PositionY = 200f;
        public float Width = 320f;
        public float Height = 180f;
        public float Rotation;
        public float Opacity = 1f;
        public int Depth;
        public float PivotX;
        public float PivotY;
    }

    [Serializable]
    public sealed class LegacyOverlayerProgressBar
    {
        public string Name = string.Empty;
        public bool IsEnabled = true;
        public bool ShowInGame = true;
        public bool OnlyShowPlaying;
        public float PositionX = 50f;
        public float PositionY = 100f;
        public float Width = 300f;
        public float Height = 20f;
        public float Opacity = 1f;
        public int Depth;
        public float PivotX;
        public float PivotY;
        public float[] BackgroundColor = new float[] { 0f, 0f, 0f, 0.45f };
        public float[] FillColor = new float[] { 0.2f, 0.75f, 1f, 0.95f };
        public float[] BorderColor = new float[] { 1f, 1f, 1f, 0.8f };
        public float BorderThickness = 1f;
        public float CornerRadius;
        public OvProgressFillDirection FillDirection;
        public LegacyOverlayerProgressValueSource ValueSource = new LegacyOverlayerProgressValueSource();
        public LegacyOverlayerProgressValueSource MinSource = new LegacyOverlayerProgressValueSource();
        public LegacyOverlayerProgressValueSource MaxSource = new LegacyOverlayerProgressValueSource();
        public bool Reverse;
        public bool ClampValue = true;
        public bool EnableFillGradient;
        public float[] FillGradientStartColor = new float[] { 1f, 0.25f, 0.25f, 0.95f };
        public float[] FillGradientEndColor = new float[] { 0.25f, 1f, 0.35f, 0.95f };
        public bool EnableShadow;
        public float[] ShadowColor = new float[] { 0f, 0f, 0f, 0.45f };
        public float[] ShadowOffset = new float[] { 2f, 2f };
        public float ShadowSoftness;
    }

    public enum LegacyOverlayerProgressValueKind
    {
        Constant = 0,
        Progress = 1,
        Accuracy = 2,
        XAccuracy = 3,
        CurrentClicksPerSecond = 4,
        MapPlayedTime = 5,
        MapTotalTime = 6,
        MusicPlayedTime = 7,
        MusicTotalTime = 8,
        PureCombo = 9,
        PerfectCombo = 10,
        Miss = 11,
        FailMiss = 12,
        FailOverload = 13
    }

    [Serializable]
    public sealed class LegacyOverlayerProgressValueSource
    {
        public LegacyOverlayerProgressValueKind Kind = LegacyOverlayerProgressValueKind.Constant;
        public double Constant;
    }

    [Serializable]
    public sealed class LegacyOverlayerAnimation
    {
        public bool IsEnabled;
        public string Name = L("旧版动画", "Legacy animation");
        public LegacyAnimationTrigger Trigger = LegacyAnimationTrigger.OnClick;
        public string JsonString = string.Empty;
        public bool UseGraphicalAnimation = true;
        public float StartScale = 1f;
        public float StartRotation;
        public float StartX;
        public float StartY;
        public float StartOpacity = 1f;
        public float EndScale = 1f;
        public float EndRotation;
        public float EndX;
        public float EndY;
        public float EndOpacity = 1f;
        public float Duration = 0.5f;
        public string EasingType = "linear";

        [XmlIgnore]
        public List<LegacyAnimationFrame> ParsedFrames = new List<LegacyAnimationFrame>();

        public void ParseJson()
        {
            if (UseGraphicalAnimation)
            {
                ParsedFrames = new List<LegacyAnimationFrame>
                {
                    new LegacyAnimationFrame { time = 0f, x = StartX, y = StartY, zoomx = StartScale, zoomy = StartScale,
                        opacity = StartOpacity, rotation = StartRotation, easing = "linear" },
                    new LegacyAnimationFrame { time = Math.Max(0.01f, Duration), x = EndX, y = EndY, zoomx = EndScale, zoomy = EndScale,
                        opacity = EndOpacity, rotation = EndRotation, easing = EasingType }
                };
                return;
            }
            try
            {
                ParsedFrames = string.IsNullOrWhiteSpace(JsonString)
                    ? new List<LegacyAnimationFrame>()
                    : JsonConvert.DeserializeObject<List<LegacyAnimationFrame>>(JsonString) ?? new List<LegacyAnimationFrame>();
                ParsedFrames.Sort((a, b) => a.time.CompareTo(b.time));
            }
            catch
            {
                ParsedFrames = new List<LegacyAnimationFrame>();
            }
        }
    }

    public enum LegacyAnimationTrigger
    {
        OnClick = 0,
        OnComboIncrease = 1
    }

    public sealed class LegacyAnimationFrame
    {
        public float time;
        public float? x;
        public float? y;
        public float? zoomx;
        public float? zoomy;
        public float? opacity;
        public float? rotation;
        public string easing = "linear";
    }

    internal static class OvPackageService
    {
        private const string ManifestName = "Overlayer.Sonnet.xml";
        private const string LegacyManifestName = "Overlayer.xml";
        private const string AssetsPrefix = "Assets";
        private const int MaxArchiveEntries = 1024;
        private const long MaxManifestBytes = 8L * 1024L * 1024L;
        private const long MaxAssetBytes = 512L * 1024L * 1024L;

        internal static string LastStatus { get; set; } = string.Empty;

        private static readonly Lazy<XmlSerializer> ModernSerializer =
            new Lazy<XmlSerializer>(() => new XmlSerializer(typeof(OvTransferPackage)));

        private static readonly Lazy<XmlSerializer> LegacySerializer =
            new Lazy<XmlSerializer>(() => new XmlSerializer(typeof(OvLegacyPackage)));

        private static readonly Lazy<XmlSerializer> CytSettingsSerializer =
            new Lazy<XmlSerializer>(() => new XmlSerializer(typeof(LegacyCytSettingsOvPackage)));

        private static readonly Lazy<XmlSerializer> CytPackageInfoSerializer =
            new Lazy<XmlSerializer>(() => new XmlSerializer(typeof(LegacyCytPackageInfo)));

        private static int SafeScreenWidth()
        {
            try
            {
                var prop = typeof(Screen).GetProperty("width", BindingFlags.Public | BindingFlags.Static);
                if (prop != null)
                {
                    object val = prop.GetValue(null);
                    if (val is int w && w > 0) return w;
                }
            }
            catch { }
            return 1920;
        }

        private static int SafeScreenHeight()
        {
            try
            {
                var prop = typeof(Screen).GetProperty("height", BindingFlags.Public | BindingFlags.Static);
                if (prop != null)
                {
                    object val = prop.GetValue(null);
                    if (val is int h && h > 0) return h;
                }
            }
            catch { }
            return 1080;
        }

        internal static bool Write(OvSettings settings, string outputPath)
        {
            string temporaryPath = null;
            try
            {
                if (settings == null || string.IsNullOrWhiteSpace(outputPath))
                {
                    LastStatus = L("导出失败：没有可导出的配置", "Export failed: nothing to export");
                    return false;
                }

                var package = new OvTransferPackage
                {
                    ExportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ScreenWidth = SafeScreenWidth(),
                    ScreenHeight = SafeScreenHeight(),
                    Texts = CloneTexts(settings.Texts),
                    Images = CloneImages(settings.Images),
                    Videos = CloneVideos(settings.Videos),
                    ProgressBars = CloneProgressBars(settings.ProgressBars)
                };

                var assetMap = BuildAssetMap(package);
                string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                Directory.CreateDirectory(directory);
                temporaryPath = Path.Combine(directory, "." + Path.GetFileName(outputPath) + ".tmp_" + Guid.NewGuid().ToString("N"));

                using (var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
                {
                    var manifestEntry = archive.CreateEntry(ManifestName, SysCompressionLevel.Optimal);
                    using (var stream = manifestEntry.Open())
                        ModernSerializer.Value.Serialize(stream, package);

                    PackAssets(archive, assetMap);
                }

                if (File.Exists(outputPath)) File.Replace(temporaryPath, outputPath, null);
                else File.Move(temporaryPath, outputPath);
                temporaryPath = null;
                LastStatus = L($"已导出: {Path.GetFileName(outputPath)}", $"Exported: {Path.GetFileName(outputPath)}");
                return true;
            }
            catch (Exception ex)
            {
                LastStatus = L($"导出失败: {ex.Message}", $"Export failed: {ex.Message}");
                if (!string.IsNullOrEmpty(temporaryPath)) TryDeleteDirectoryOrFile(temporaryPath);
                return false;
            }
        }

        private static bool IsZipArchive(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (fs.Length < 4) return false;
                    byte[] header = new byte[4];
                    if (fs.Read(header, 0, 4) < 4) return false;
                    return header[0] == 0x50 && header[1] == 0x4B &&
                           ((header[2] == 0x03 && header[3] == 0x04) ||
                            (header[2] == 0x05 && header[3] == 0x06) ||
                            (header[2] == 0x07 && header[3] == 0x08));
                }
            }
            catch { return false; }
        }

        private static string GetXmlRootElementName(string xmlPath)
        {
            try
            {
                using (var reader = XmlReader.Create(xmlPath, new XmlReaderSettings { IgnoreWhitespace = true, IgnoreComments = true }))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                            return reader.LocalName;
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        internal static bool Read(OvSettings settings, string packagePath, out bool legacy)
        {
            legacy = false;
            try
            {
                if (!File.Exists(packagePath))
                {
                    LastStatus = L("配置包文件不存在", "Package file not found");
                    return false;
                }

                if (IsZipArchive(packagePath))
                {
                    using (var archive = ZipFile.OpenRead(packagePath))
                    {
                        ValidateArchive(archive);
                        var modernEntry = archive.GetEntry(ManifestName);
                        if (modernEntry != null)
                        {
                            return ReadModernPackage(settings, archive, packagePath);
                        }

                        var legacyEntry = archive.GetEntry(LegacyManifestName);
                        if (legacyEntry != null)
                        {
                            legacy = true;
                            return ReadLegacyPackage(settings, archive, packagePath);
                        }

                        var cytSettingsEntry = archive.GetEntry("Settings.xml");
                        if (cytSettingsEntry != null)
                        {
                            legacy = true;
                            return ReadCytPackage(settings, archive, packagePath);
                        }

                        LastStatus = L("配置包格式无效：未找到清单文件或 Settings.xml", "Invalid package: no manifest or Settings.xml found");
                        return false;
                    }
                }
                else
                {
                    return ReadRawXmlFile(settings, packagePath, out legacy);
                }
            }
            catch (Exception ex)
            {
                LastStatus = L($"导入失败: {ex.Message}", $"Import failed: {ex.Message}") + (ex.InnerException != null ? " -> " + ex.InnerException.Message : "");
                return false;
            }
        }

        private static bool ReadCytPackage(OvSettings settings, ZipArchive archive, string packagePath)
        {
            var settingsEntry = archive.GetEntry("Settings.xml");
            if (settingsEntry == null)
            {
                LastStatus = L("导入失败：未找到 Settings.xml", "Import failed: Settings.xml not found");
                return false;
            }

            int sourceWidth = 0;
            int sourceHeight = 0;
            var infoEntry = archive.GetEntry("PackageInfo.xml");
            if (infoEntry != null)
            {
                try
                {
                    var info = DeserializeEntry<LegacyCytPackageInfo>(infoEntry, CytPackageInfoSerializer.Value, L("CYT 信息清单", "CYT info manifest"));
                    if (info != null)
                    {
                        sourceWidth = info.ExportScreenWidth;
                        sourceHeight = info.ExportScreenHeight;
                    }
                }
                catch { }
            }

            var cytSettings = DeserializeEntry<LegacyCytSettingsOvPackage>(settingsEntry, CytSettingsSerializer.Value, L("CYT 设置清单", "CYT settings manifest"));
            if (cytSettings == null)
            {
                LastStatus = L("导入失败：无法解析 Settings.xml", "Import failed: could not parse Settings.xml");
                return false;
            }

            if (sourceWidth <= 0 || sourceHeight <= 0)
            {
                sourceWidth = cytSettings.LastKnownScreenWidth > 0 ? cytSettings.LastKnownScreenWidth : SafeScreenWidth();
                sourceHeight = cytSettings.LastKnownScreenHeight > 0 ? cytSettings.LastKnownScreenHeight : SafeScreenHeight();
            }

            var package = new OvLegacyPackage
            {
                FormatVersion = 1,
                ExportScreenWidth = sourceWidth,
                ExportScreenHeight = sourceHeight,
                OverlayerTexts = cytSettings.OverlayerTexts ?? new List<LegacyOverlayerText>(),
                OverlayerImages = cytSettings.OverlayerImages ?? new List<LegacyOverlayerImage>(),
                OverlayerVideos = cytSettings.OverlayerVideos ?? new List<LegacyOverlayerVideo>(),
                OverlayerProgressBars = cytSettings.OverlayerProgressBars ?? new List<LegacyOverlayerProgressBar>()
            };

            int rawTexts = package.OverlayerTexts.Count;
            int rawImages = package.OverlayerImages.Count;
            int rawVideos = package.OverlayerVideos.Count;
            int rawProgress = package.OverlayerProgressBars.Count;

            if (rawTexts == 0 && rawImages == 0 && rawVideos == 0 && rawProgress == 0)
            {
                LastStatus = L("旧版 .cyt 包中没有可导入的 Overlayer 组件", "The old .cyt package has no Overlayer components to import");
                return false;
            }

            string importedRoot = ExtractAssets(archive, packagePath);
            try
            {
                var converted = ConvertLegacyPackage(package);
                RewriteImportedAssetPaths(converted, importedRoot);
                AdaptResolution(converted);
                NormalizePackageGraphs(converted);
                MergeIntoSettings(settings, converted);

                int total = converted.Texts.Count + converted.Images.Count + converted.Videos.Count + converted.ProgressBars.Count;
                LastStatus = L($"已从 .cyt 导入旧版 OV 配置: {total} 个组件 (原始: {rawTexts}T/{rawImages}I/{rawVideos}V/{rawProgress}P)", $"Imported old OV settings from .cyt: {total} components (source: {rawTexts}T/{rawImages}I/{rawVideos}V/{rawProgress}P)");
                return true;
            }
            catch
            {
                TryDeleteDirectoryOrFile(importedRoot);
                throw;
            }
        }

        private static bool ReadRawXmlFile(OvSettings settings, string xmlPath, out bool legacy)
        {
            legacy = false;
            string rootName = GetXmlRootElementName(xmlPath);
            if (string.Equals(rootName, "CheryToolsSonnetOverlayer", StringComparison.OrdinalIgnoreCase))
            {
                using (var stream = File.OpenRead(xmlPath))
                {
                    var package = ModernSerializer.Value.Deserialize(stream) as OvTransferPackage;
                    if (package == null) { LastStatus = L("配置内容为空", "The settings are empty"); return false; }
                    NormalizePackageCollections(package);
                    ResolveLocalAssetPathsForPackage(package);
                    AdaptResolution(package);
                    NormalizePackageGraphs(package);
                    MergeIntoSettings(settings, package);
                    int total = package.Texts.Count + package.Images.Count + package.Videos.Count + package.ProgressBars.Count;
                    LastStatus = L($"已导入 {total} 个组件 ({package.Texts.Count} 文本, {package.Images.Count} 图片, {package.Videos.Count} 视频, {package.ProgressBars.Count} 进度条)", $"Imported {total} components ({package.Texts.Count} text, {package.Images.Count} images, {package.Videos.Count} videos, {package.ProgressBars.Count} progress bars)");
                    return true;
                }
            }
            else if (string.Equals(rootName, "OverlayerPackage", StringComparison.OrdinalIgnoreCase))
            {
                legacy = true;
                using (var stream = File.OpenRead(xmlPath))
                {
                    var package = LegacySerializer.Value.Deserialize(stream) as OvLegacyPackage;
                    if (package == null) { LastStatus = L("旧版配置内容为空", "The old settings are empty"); return false; }
                    if (package.OverlayerTexts == null) package.OverlayerTexts = new List<LegacyOverlayerText>();
                    if (package.OverlayerImages == null) package.OverlayerImages = new List<LegacyOverlayerImage>();
                    if (package.OverlayerVideos == null) package.OverlayerVideos = new List<LegacyOverlayerVideo>();
                    if (package.OverlayerProgressBars == null) package.OverlayerProgressBars = new List<LegacyOverlayerProgressBar>();
                    var converted = ConvertLegacyPackage(package);
                    ResolveLocalAssetPathsForPackage(converted);
                    AdaptResolution(converted);
                    NormalizePackageGraphs(converted);
                    MergeIntoSettings(settings, converted);
                    int total = converted.Texts.Count + converted.Images.Count + converted.Videos.Count + converted.ProgressBars.Count;
                    LastStatus = L($"已导入旧版 XML 配置: {total} 个组件", $"Imported old XML settings: {total} components");
                    return true;
                }
            }
            else if (string.Equals(rootName, "Settings", StringComparison.OrdinalIgnoreCase))
            {
                legacy = true;
                using (var stream = File.OpenRead(xmlPath))
                {
                    var cytSettings = CytSettingsSerializer.Value.Deserialize(stream) as LegacyCytSettingsOvPackage;
                    if (cytSettings == null) { LastStatus = L("Settings 内容为空", "Settings is empty"); return false; }
                    int width = cytSettings.LastKnownScreenWidth > 0 ? cytSettings.LastKnownScreenWidth : SafeScreenWidth();
                    int height = cytSettings.LastKnownScreenHeight > 0 ? cytSettings.LastKnownScreenHeight : SafeScreenHeight();
                    var legacyPackage = new OvLegacyPackage
                    {
                        FormatVersion = 1,
                        ExportScreenWidth = width,
                        ExportScreenHeight = height,
                        OverlayerTexts = cytSettings.OverlayerTexts ?? new List<LegacyOverlayerText>(),
                        OverlayerImages = cytSettings.OverlayerImages ?? new List<LegacyOverlayerImage>(),
                        OverlayerVideos = cytSettings.OverlayerVideos ?? new List<LegacyOverlayerVideo>(),
                        OverlayerProgressBars = cytSettings.OverlayerProgressBars ?? new List<LegacyOverlayerProgressBar>()
                    };
                    var converted = ConvertLegacyPackage(legacyPackage);
                    ResolveLocalAssetPathsForPackage(converted);
                    AdaptResolution(converted);
                    NormalizePackageGraphs(converted);
                    MergeIntoSettings(settings, converted);
                    int total = converted.Texts.Count + converted.Images.Count + converted.Videos.Count + converted.ProgressBars.Count;
                    LastStatus = L($"已从 Settings.xml 导入 OV 配置: {total} 个组件", $"Imported OV settings from Settings.xml: {total} components");
                    return true;
                }
            }
            else
            {
                LastStatus = L($"无法识别的 XML 根节点: <{rootName}>", $"Unrecognized XML root element: <{rootName}>");
                return false;
            }
        }

        private static void ResolveLocalAssetPathsForPackage(OvTransferPackage package)
        {
            if (package == null) return;
            if (package.Texts != null)
            {
                foreach (var text in package.Texts)
                {
                    if (text != null && !string.IsNullOrWhiteSpace(text.FontPath))
                        text.FontPath = ResolveLocalAssetPath(text.FontPath);
                }
            }
            if (package.Images != null)
            {
                foreach (var img in package.Images)
                {
                    if (img != null && !string.IsNullOrWhiteSpace(img.ImagePath))
                        img.ImagePath = ResolveLocalAssetPath(img.ImagePath);
                }
            }
            if (package.Videos != null)
            {
                foreach (var vid in package.Videos)
                {
                    if (vid != null && !string.IsNullOrWhiteSpace(vid.VideoPath))
                        vid.VideoPath = ResolveLocalAssetPath(vid.VideoPath);
                }
            }
        }

        private static bool ReadModernPackage(OvSettings settings, ZipArchive archive, string packagePath)
        {
            var manifestEntry = archive.GetEntry(ManifestName);
            OvTransferPackage package;
            package = DeserializeEntry<OvTransferPackage>(manifestEntry, ModernSerializer.Value, L("现代配置清单", "manifest"));
            NormalizePackageCollections(package);

            string importedRoot = ExtractAssets(archive, packagePath);
            try
            {
                RewriteImportedAssetPaths(package, importedRoot);
                AdaptResolution(package);
                NormalizePackageGraphs(package);
                MergeIntoSettings(settings, package);
            }
            catch
            {
                TryDeleteDirectoryOrFile(importedRoot);
                throw;
            }

            int total = package.Texts.Count + package.Images.Count + package.Videos.Count + package.ProgressBars.Count;
            LastStatus = L($"已导入 {total} 个组件 ({package.Texts.Count} 文本, {package.Images.Count} 图片, {package.Videos.Count} 视频, {package.ProgressBars.Count} 进度条)", $"Imported {total} components ({package.Texts.Count} text, {package.Images.Count} images, {package.Videos.Count} videos, {package.ProgressBars.Count} progress bars)");
            return true;
        }

        private static bool ReadLegacyPackage(OvSettings settings, ZipArchive archive, string packagePath)
        {
            var manifestEntry = archive.GetEntry(LegacyManifestName);
            OvLegacyPackage package;
            package = DeserializeEntry<OvLegacyPackage>(manifestEntry, LegacySerializer.Value, L("旧版配置清单", "legacy manifest"));
            if (package.OverlayerTexts == null) package.OverlayerTexts = new List<LegacyOverlayerText>();
            if (package.OverlayerImages == null) package.OverlayerImages = new List<LegacyOverlayerImage>();
            if (package.OverlayerVideos == null) package.OverlayerVideos = new List<LegacyOverlayerVideo>();
            if (package.OverlayerProgressBars == null) package.OverlayerProgressBars = new List<LegacyOverlayerProgressBar>();

            int rawTexts = package.OverlayerTexts?.Count ?? 0;
            int rawImages = package.OverlayerImages?.Count ?? 0;
            int rawVideos = package.OverlayerVideos?.Count ?? 0;
            int rawProgress = package.OverlayerProgressBars?.Count ?? 0;
            LastStatus = L($"旧版包读取: {rawTexts} 文本, {rawImages} 图片, {rawVideos} 视频, {rawProgress} 进度条", $"Old package contents: {rawTexts} text, {rawImages} images, {rawVideos} videos, {rawProgress} progress bars");

            string importedRoot = ExtractAssets(archive, packagePath);
            try
            {
                var converted = ConvertLegacyPackage(package);
                RewriteImportedAssetPaths(converted, importedRoot);
                AdaptResolution(converted);
                NormalizePackageGraphs(converted);
                MergeIntoSettings(settings, converted);

                int total = converted.Texts.Count + converted.Images.Count + converted.Videos.Count + converted.ProgressBars.Count;
                LastStatus = L($"已导入旧版配置包: {total} 个组件 (原始: {rawTexts}T/{rawImages}I/{rawVideos}V/{rawProgress}P)", $"Imported old package: {total} components (source: {rawTexts}T/{rawImages}I/{rawVideos}V/{rawProgress}P)");
                return true;
            }
            catch
            {
                TryDeleteDirectoryOrFile(importedRoot);
                throw;
            }
        }

        private static OvTransferPackage ConvertLegacyPackage(OvLegacyPackage legacy)
        {
            var package = new OvTransferPackage
            {
                FormatVersion = 1,
                ExportedAt = legacy.ExportedAt,
                ScreenWidth = legacy.ExportScreenWidth,
                ScreenHeight = legacy.ExportScreenHeight
            };

            if (legacy.OverlayerTexts != null)
            {
                foreach (var old in legacy.OverlayerTexts)
                {
                    if (old == null) continue;
                    var converted = new OvTextComponent
                    {
                        Name = old.Name,
                        Enabled = old.IsEnabled,
                        ShowInGame = old.ShowInGame,
                        OnlyShowPlaying = old.OnlyShowPlaying,
                        PositionX = old.PositionX,
                        PositionY = old.PositionY,
                        Opacity = 1f,
                        Depth = old.Depth,
                        PivotX = old.PivotX,
                        PivotY = old.PivotY,
                        TextFormat = old.TextFormat,
                        FontSize = old.FontSize,
                        FontPath = old.FontPath,
                        Alignment = old.Alignment,
                        TextColor = old.TextColor ?? new float[] { 1f, 1f, 1f, 1f },
                        OutlineEnabled = old.EnableOutline,
                        OutlineColor = old.OutlineColor ?? new float[] { 0f, 0f, 0f, 1f },
                        OutlineThickness = old.OutlineThickness,
                        ShadowEnabled = old.EnableShadow,
                        ShadowColor = old.ShadowColor ?? new float[] { 0f, 0f, 0f, 0.7f },
                        ShadowOffset = old.ShadowOffset ?? new float[] { 2f, 2f },
                        ShadowSoftness = old.ShadowSoftness,
                        LetterSpacing = old.LetterSpacing,
                        LineHeightOffset = old.LineHeightOffset,
                        TokenSourceSnapshot = old.TokenSourceSnapshot ?? old.TextFormat ?? string.Empty,
                        TokenBindings = OvTextTokenBinding.CloneList(old.TokenBindings),
                        TokenAnimation = old.TokenAnimation != null ? OvAnimationGraph.Clone(old.TokenAnimation) : null
                    };
                    if (converted.TokenBindings == null || converted.TokenBindings.Count == 0)
                        OvTextTokenService.EnsureBindings(converted);
                    converted.TokenAnimation = MergeLegacyAnimations(converted.TokenAnimation,
                        old.Animations, converted.TokenBindings, false);
                    package.Texts.Add(converted);
                }
            }

            if (legacy.OverlayerImages != null)
            {
                foreach (var old in legacy.OverlayerImages)
                {
                    if (old == null) continue;
                    var converted = new OvImageComponent
                    {
                        Name = old.Name,
                        Enabled = old.IsEnabled,
                        ShowInGame = old.ShowInGame,
                        OnlyShowPlaying = old.OnlyShowPlaying,
                        PositionX = old.PositionX,
                        PositionY = old.PositionY,
                        Opacity = old.Opacity,
                        Depth = old.Depth,
                        PivotX = old.PivotX,
                        PivotY = old.PivotY,
                        ImagePath = old.ImagePath,
                        Scale = old.Scale,
                        Rotation = old.Rotation,
                        NodeAnimation = old.NodeAnimation != null ? OvAnimationGraph.Clone(old.NodeAnimation) : null
                    };
                    converted.NodeAnimation = MergeLegacyAnimations(converted.NodeAnimation,
                        old.Animations, null, true);
                    package.Images.Add(converted);
                }
            }

            if (legacy.OverlayerVideos != null)
            {
                foreach (var old in legacy.OverlayerVideos)
                {
                    if (old == null) continue;
                    package.Videos.Add(new OvVideoComponent
                    {
                        Name = old.Name,
                        Enabled = old.IsEnabled,
                        ShowInGame = old.ShowInGame,
                        OnlyShowPlaying = old.OnlyShowPlaying,
                        PositionX = old.PositionX,
                        PositionY = old.PositionY,
                        Opacity = old.Opacity,
                        Depth = old.Depth,
                        PivotX = old.PivotX,
                        PivotY = old.PivotY,
                        VideoPath = old.VideoPath,
                        Loop = old.Loop,
                        Width = old.Width,
                        Height = old.Height,
                        ContentScale = old.ContentScale,
                        ContentOffsetX = old.ContentOffsetX,
                        ContentOffsetY = old.ContentOffsetY,
                        Rotation = old.Rotation
                    });
                }
            }

            if (legacy.OverlayerProgressBars != null)
            {
                foreach (var old in legacy.OverlayerProgressBars)
                {
                    if (old == null) continue;
                    package.ProgressBars.Add(new OvProgressComponent
                    {
                        Name = old.Name,
                        Enabled = old.IsEnabled,
                        ShowInGame = old.ShowInGame,
                        OnlyShowPlaying = old.OnlyShowPlaying,
                        PositionX = old.PositionX,
                        PositionY = old.PositionY,
                        Opacity = old.Opacity,
                        Depth = old.Depth,
                        PivotX = old.PivotX,
                        PivotY = old.PivotY,
                        Width = old.Width,
                        Height = old.Height,
                        FillDirection = old.FillDirection,
                        ValueTag = LegacyValueTag(old.ValueSource),
                        ValueIsConstant = IsLegacyConstant(old.ValueSource),
                        ValueConstant = LegacyConstant(old.ValueSource),
                        MinimumTag = LegacyValueTag(old.MinSource),
                        MaximumTag = LegacyValueTag(old.MaxSource),
                        Minimum = LegacyConstant(old.MinSource),
                        Maximum = LegacyConstant(old.MaxSource),
                        Reverse = old.Reverse,
                        ClampValue = old.ClampValue,
                        EnableFillGradient = old.EnableFillGradient,
                        FillGradientStartColor = old.FillGradientStartColor ?? new float[] { 1f, 0.25f, 0.25f, 0.95f },
                        FillGradientEndColor = old.FillGradientEndColor ?? new float[] { 0.25f, 1f, 0.35f, 0.95f },
                        EnableShadow = old.EnableShadow,
                        ShadowColor = old.ShadowColor ?? new float[] { 0f, 0f, 0f, 0.45f },
                        ShadowOffset = old.ShadowOffset ?? new float[] { 2f, 2f },
                        ShadowSoftness = old.ShadowSoftness,
                        BackgroundColor = old.BackgroundColor ?? new float[] { 0f, 0f, 0f, 0.45f },
                        FillColor = old.FillColor ?? new float[] { 0.2f, 0.75f, 1f, 0.95f },
                        BorderColor = old.BorderColor ?? new float[] { 1f, 1f, 1f, 0.8f },
                        BorderThickness = old.BorderThickness,
                        CornerRadius = old.CornerRadius
                    });
                }
            }

            return package;
        }

        private static OvAnimationGraph MergeLegacyAnimations(OvAnimationGraph graph,
            List<LegacyOverlayerAnimation> legacyAnimations, List<OvTextTokenBinding> tokens, bool image)
        {
            graph = graph != null ? OvAnimationGraph.Clone(graph)
                : (image ? OvImageNodeAnimation.CreateDefault() : OvAnimationGraph.CreateDefault());
            if (image) OvImageNodeAnimation.EnsureImageTarget(graph);
            if (legacyAnimations == null || legacyAnimations.Count == 0) return graph;

            bool migratedAny = false;
            for (int i = 0; i < legacyAnimations.Count; i++)
            {
                LegacyOverlayerAnimation animation = legacyAnimations[i];
                if (animation == null || !animation.IsEnabled) continue;
                animation.ParseJson();
                if (animation.ParsedFrames == null || animation.ParsedFrames.Count < 2) continue;
                AddLegacyAnimationBranch(graph, animation, tokens, image);
                migratedAny = true;
            }
            if (migratedAny) graph.Enabled = true;
            graph.HoldFinalPose = graph.HoldFinalPose || migratedAny;
            if (graph.Nodes == null || graph.Nodes.Count == 0)
                graph = image ? OvImageNodeAnimation.CreateDefault() : OvAnimationGraph.CreateDefault();
            graph.Normalize();
            if (image) OvImageNodeAnimation.EnsureImageTarget(graph);
            return graph;
        }

        private static void AddLegacyAnimationBranch(OvAnimationGraph graph,
            LegacyOverlayerAnimation animation, List<OvTextTokenBinding> tokens, bool image)
        {
            List<string> targetIds = new List<string>();
            if (image) targetIds.Add(OvImageNodeAnimation.TargetId);
            else if (tokens != null)
            {
                for (int i = 0; i < tokens.Count; i++)
                    if (tokens[i] != null && !string.IsNullOrEmpty(tokens[i].Id)) targetIds.Add(tokens[i].Id);
            }

            var input = new OvAnimationNode
            {
                Id = OvAnimationGraph.NewId(), Kind = OvAnimationNodeKind.TokenInput,
                Name = image ? L("旧版图片目标", "Legacy image target") : L("旧版全部 Token", "Legacy all tokens"), SelectedTokenIds = targetIds,
                EditorX = 30f, EditorY = 190f
            };
            var trigger = new OvAnimationNode
            {
                Id = OvAnimationGraph.NewId(), Kind = OvAnimationNodeKind.Trigger,
                Name = string.IsNullOrEmpty(animation.Name) ? L("旧版动画触发", "Legacy animation trigger") : animation.Name,
                Trigger = animation.Trigger == LegacyAnimationTrigger.OnComboIncrease
                    ? OvAnimationTriggerKind.ComboIncrease : OvAnimationTriggerKind.AnyKeyDown,
                EditorX = 30f, EditorY = 40f
            };
            graph.Nodes.Add(input);
            graph.Nodes.Add(trigger);

            string positionPrevious = trigger.Id;
            string scalePrevious = trigger.Id;
            string rotationPrevious = trigger.Id;
            string opacityPrevious = trigger.Id;
            float x = 0f, y = 0f, scaleX = 1f, scaleY = 1f, rotation = 0f, opacity = 1f;
            List<LegacyAnimationFrame> frames = animation.ParsedFrames;
            LegacyAnimationFrame first = frames[0];
            if (first != null)
            {
                x = first.x ?? x; y = first.y ?? y;
                scaleX = first.zoomx ?? scaleX; scaleY = first.zoomy ?? scaleY;
                rotation = first.rotation ?? rotation; opacity = first.opacity ?? opacity;
            }
            for (int i = 1; i < frames.Count; i++)
            {
                LegacyAnimationFrame frame = frames[i] ?? new LegacyAnimationFrame();
                LegacyAnimationFrame previous = frames[i - 1] ?? new LegacyAnimationFrame();
                float nextX = frame.x ?? x, nextY = frame.y ?? y;
                float nextScaleX = frame.zoomx ?? scaleX, nextScaleY = frame.zoomy ?? scaleY;
                float nextRotation = frame.rotation ?? rotation, nextOpacity = frame.opacity ?? opacity;
                float duration = Math.Max(0.01f, frame.time - previous.time);
                string easing = string.IsNullOrWhiteSpace(frame.easing) ? "linear" : frame.easing;
                float editorX = 340f + (i - 1) * 300f;

                if (i == 1 || !Approximately(x, nextX) || !Approximately(y, nextY))
                {
                    var node = NewLegacyTween(L("位移", "Position"), OvTokenTweenProperty.Position, x, y, nextX, nextY, duration, easing, editorX, 55f);
                    graph.Nodes.Add(node); graph.Links.Add(OvAnimationGraph.NewLink(positionPrevious, "flow", node.Id, "flow"));
                    graph.Links.Add(OvAnimationGraph.NewLink(input.Id, "targets", node.Id, "targets")); positionPrevious = node.Id;
                }
                if (i == 1 || !Approximately(scaleX, nextScaleX) || !Approximately(scaleY, nextScaleY))
                {
                    var node = NewLegacyTween(L("缩放", "Scale"), OvTokenTweenProperty.Scale, scaleX, scaleY, nextScaleX, nextScaleY, duration, easing, editorX, 205f);
                    graph.Nodes.Add(node); graph.Links.Add(OvAnimationGraph.NewLink(scalePrevious, "flow", node.Id, "flow"));
                    graph.Links.Add(OvAnimationGraph.NewLink(input.Id, "targets", node.Id, "targets")); scalePrevious = node.Id;
                }
                if (i == 1 || !Approximately(rotation, nextRotation))
                {
                    var node = NewLegacyTween(L("旋转", "Rotation"), OvTokenTweenProperty.Rotation, rotation, 0f, nextRotation, 0f, duration, easing, editorX, 355f);
                    graph.Nodes.Add(node); graph.Links.Add(OvAnimationGraph.NewLink(rotationPrevious, "flow", node.Id, "flow"));
                    graph.Links.Add(OvAnimationGraph.NewLink(input.Id, "targets", node.Id, "targets")); rotationPrevious = node.Id;
                }
                if (i == 1 || !Approximately(opacity, nextOpacity))
                {
                    var node = NewLegacyTween(L("透明度", "Opacity"), OvTokenTweenProperty.Opacity, opacity, 0f, nextOpacity, 0f, duration, easing, editorX, 505f);
                    graph.Nodes.Add(node); graph.Links.Add(OvAnimationGraph.NewLink(opacityPrevious, "flow", node.Id, "flow"));
                    graph.Links.Add(OvAnimationGraph.NewLink(input.Id, "targets", node.Id, "targets")); opacityPrevious = node.Id;
                }
                x = nextX; y = nextY; scaleX = nextScaleX; scaleY = nextScaleY; rotation = nextRotation; opacity = nextOpacity;
            }
        }

        private static OvAnimationNode NewLegacyTween(string name, OvTokenTweenProperty property,
            float fromX, float fromY, float toX, float toY, float duration, string easing, float editorX, float editorY)
        {
            return new OvAnimationNode
            {
                Id = OvAnimationGraph.NewId(), Kind = OvAnimationNodeKind.Tween, Name = name,
                TweenProperty = property, FromX = fromX, FromY = fromY, ToX = toX, ToY = toY,
                Duration = duration, Easing = easing, EditorX = editorX, EditorY = editorY
            };
        }

        private static bool Approximately(float left, float right) { return Math.Abs(left - right) <= 0.0001f; }

        private static bool IsLegacyConstant(LegacyOverlayerProgressValueSource source)
        {
            return source == null || source.Kind == LegacyOverlayerProgressValueKind.Constant;
        }

        private static double LegacyConstant(LegacyOverlayerProgressValueSource source)
        {
            return source != null ? source.Constant : 0d;
        }

        private static string LegacyValueTag(LegacyOverlayerProgressValueSource source)
        {
            if (IsLegacyConstant(source)) return string.Empty;
            switch (source.Kind)
            {
                case LegacyOverlayerProgressValueKind.Progress: return "{progress}";
                case LegacyOverlayerProgressValueKind.Accuracy: return "{acc}";
                case LegacyOverlayerProgressValueKind.XAccuracy: return "{xacc}";
                case LegacyOverlayerProgressValueKind.CurrentClicksPerSecond: return "{cur}";
                case LegacyOverlayerProgressValueKind.MapPlayedTime: return "{maptime:p}";
                case LegacyOverlayerProgressValueKind.MapTotalTime: return "{maptime}";
                case LegacyOverlayerProgressValueKind.MusicPlayedTime: return "{musictime:p}";
                case LegacyOverlayerProgressValueKind.MusicTotalTime: return "{musictime}";
                case LegacyOverlayerProgressValueKind.PureCombo: return "{XPrefectCombo}";
                case LegacyOverlayerProgressValueKind.PerfectCombo: return "{AllPrefectCombo}";
                case LegacyOverlayerProgressValueKind.Miss: return "{miss}";
                case LegacyOverlayerProgressValueKind.FailMiss: return "{fm}";
                case LegacyOverlayerProgressValueKind.FailOverload: return "{fo}";
                default: return string.Empty;
            }
        }

        private static void AdaptResolution(OvTransferPackage package)
        {
            if (package.ScreenWidth <= 0 || package.ScreenHeight <= 0)
                return;

            float scaleX = (float)SafeScreenWidth() / package.ScreenWidth;
            float scaleY = (float)SafeScreenHeight() / package.ScreenHeight;
            float uniform = Mathf.Min(scaleX, scaleY);

            foreach (var text in package.Texts)
            {
                text.PositionX *= scaleX;
                text.PositionY *= scaleY;
                text.FontSize *= uniform;
                ScalePair(text.ShadowOffset, scaleX, scaleY);
                text.ShadowSoftness *= uniform;
                text.OutlineThickness *= uniform;
                text.LetterSpacing *= uniform;
                text.LineHeightOffset *= uniform;
                ScaleAnimationGraph(text.TokenAnimation, scaleX, scaleY);
            }

            foreach (var image in package.Images)
            {
                image.PositionX *= scaleX;
                image.PositionY *= scaleY;
                image.Scale *= uniform;
                ScaleAnimationGraph(image.NodeAnimation, scaleX, scaleY);
            }

            foreach (var video in package.Videos)
            {
                video.PositionX *= scaleX;
                video.PositionY *= scaleY;
                video.Width *= uniform;
                video.Height *= uniform;
                video.ContentOffsetX *= scaleX;
                video.ContentOffsetY *= scaleY;
                video.ContentScale *= uniform;
            }

            foreach (var progress in package.ProgressBars)
            {
                progress.PositionX *= scaleX;
                progress.PositionY *= scaleY;
                progress.Width *= uniform;
                progress.Height *= uniform;
                progress.BorderThickness *= uniform;
                progress.CornerRadius *= uniform;
            }
        }

        private static void ScalePair(float[] values, float scaleX, float scaleY)
        {
            if (values == null || values.Length < 2) return;
            values[0] *= scaleX;
            values[1] *= scaleY;
        }

        private static void ScaleAnimationGraph(OvAnimationGraph graph, float scaleX, float scaleY)
        {
            if (graph == null || graph.Nodes == null) return;
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                OvAnimationNode node = graph.Nodes[i];
                if (node == null || node.Kind != OvAnimationNodeKind.Tween) continue;
                if (node.TweenProperty != OvTokenTweenProperty.Position) continue;
                node.FromX *= scaleX;
                node.ToX *= scaleX;
                node.FromY *= scaleY;
                node.ToY *= scaleY;
            }
        }

        private static void MergeIntoSettings(OvSettings settings, OvTransferPackage package)
        {
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in settings.Texts) existingNames.Add(t.Name);
            foreach (var i in settings.Images) existingNames.Add(i.Name);
            foreach (var v in settings.Videos) existingNames.Add(v.Name);
            foreach (var p in settings.ProgressBars) existingNames.Add(p.Name);

            foreach (var text in package.Texts)
            {
                text.Name = UniqueName(text.Name, L("导入文本", "Imported text"), existingNames);
                existingNames.Add(text.Name);
                settings.Texts.Add(text);
            }

            foreach (var image in package.Images)
            {
                image.Name = UniqueName(image.Name, L("导入图片", "Imported image"), existingNames);
                existingNames.Add(image.Name);
                settings.Images.Add(image);
            }

            foreach (var video in package.Videos)
            {
                video.Name = UniqueName(video.Name, L("导入视频", "Imported video"), existingNames);
                existingNames.Add(video.Name);
                settings.Videos.Add(video);
            }

            foreach (var progress in package.ProgressBars)
            {
                progress.Name = UniqueName(progress.Name, L("导入进度条", "Imported progress bar"), existingNames);
                existingNames.Add(progress.Name);
                settings.ProgressBars.Add(progress);
            }
        }

        private static void NormalizePackageGraphs(OvTransferPackage package)
        {
            if (package == null) return;
            if (package.Texts != null)
            {
                for (int i = 0; i < package.Texts.Count; i++)
                    package.Texts[i]?.TokenAnimation?.Normalize();
            }
            if (package.Images != null)
            {
                for (int i = 0; i < package.Images.Count; i++)
                {
                    OvImageComponent image = package.Images[i];
                    if (image == null) continue;
                    if (image.NodeAnimation == null) image.NodeAnimation = OvImageNodeAnimation.CreateDefault();
                    OvImageNodeAnimation.EnsureImageTarget(image.NodeAnimation);
                }
            }
        }

        private static void NormalizePackageCollections(OvTransferPackage package)
        {
            if (package == null) throw new InvalidDataException(L("配置清单内容无效", "The manifest is invalid"));
            if (package.Texts == null) package.Texts = new List<OvTextComponent>();
            if (package.Images == null) package.Images = new List<OvImageComponent>();
            if (package.Videos == null) package.Videos = new List<OvVideoComponent>();
            if (package.ProgressBars == null) package.ProgressBars = new List<OvProgressComponent>();
        }

        private static string UniqueName(string requested, string fallback, HashSet<string> existing)
        {
            string baseName = string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
            if (!existing.Contains(baseName)) return baseName;

            for (int i = 2; i < 9999; i++)
            {
                string candidate = $"{baseName} ({i})";
                if (!existing.Contains(candidate)) return candidate;
            }

            return baseName + L(" (导入)", " (imported)");
        }

        private sealed class AssetPayload
        {
            internal string FullPath;
            internal string EntryPath;
        }

        private static List<AssetPayload> BuildAssetMap(OvTransferPackage package)
        {
            var payloads = new List<AssetPayload>();
            var pathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var entryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < package.Texts.Count; i++)
            {
                OvTextComponent text = package.Texts[i];
                if (text != null) text.FontPath = MapAssetPath(text.FontPath, "Fonts", pathMap, entryNames, payloads);
            }
            for (int i = 0; i < package.Images.Count; i++)
            {
                OvImageComponent image = package.Images[i];
                if (image != null) image.ImagePath = MapAssetPath(image.ImagePath, "Images", pathMap, entryNames, payloads);
            }
            for (int i = 0; i < package.Videos.Count; i++)
            {
                OvVideoComponent video = package.Videos[i];
                if (video != null) video.VideoPath = MapAssetPath(video.VideoPath, "Videos", pathMap, entryNames, payloads);
            }
            return payloads;
        }

        private static string MapAssetPath(string sourcePath, string category,
            Dictionary<string, string> pathMap, HashSet<string> entryNames, List<AssetPayload> payloads)
        {
            if (string.IsNullOrWhiteSpace(sourcePath)) return sourcePath;
            string fullPath = ResolveLocalAssetPath(sourcePath);
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath)) return sourcePath;
            if (pathMap.TryGetValue(fullPath, out string cached)) return cached;

            string fileName = Path.GetFileName(fullPath);
            if (string.IsNullOrEmpty(fileName)) return sourcePath;
            string stem = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            string entryPath = $"{AssetsPrefix}/{category}/{fileName}";
            for (int suffix = 2; entryNames.Contains(entryPath); suffix++)
                entryPath = $"{AssetsPrefix}/{category}/{stem}_{suffix}{extension}";

            entryNames.Add(entryPath);
            pathMap[fullPath] = entryPath;
            payloads.Add(new AssetPayload { FullPath = fullPath, EntryPath = entryPath });
            return entryPath;
        }

        private static string ResolveLocalAssetPath(string value)
        {
            string path = (value ?? string.Empty).Trim().Trim('"');
            if (string.IsNullOrEmpty(path)) return string.Empty;
            try
            {
                string normalized = path.Replace('/', Path.DirectorySeparatorChar);
                if (Path.IsPathRooted(normalized) && File.Exists(normalized))
                    return Path.GetFullPath(normalized);

                string assemblyDir = Path.GetDirectoryName(typeof(OvPackageService).Assembly.Location);
                string gameRoot = GameRoot();
                string[] candidates = new[]
                {
                    !string.IsNullOrEmpty(assemblyDir) ? Path.Combine(assemblyDir, normalized) : null,
                    Path.Combine(gameRoot, normalized),
                    Path.Combine(gameRoot, "CheryToolsAssets", normalized),
                    normalized.StartsWith("Assets" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                        ? Path.Combine(gameRoot, "CheryToolsAssets", normalized.Substring(7))
                        : null
                };
                foreach (var candidate in candidates)
                {
                    if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                        return Path.GetFullPath(candidate);
                }
                return path;
            }
            catch { return path; }
        }

        private static void PackAssets(ZipArchive archive, List<AssetPayload> payloads)
        {
            for (int i = 0; i < payloads.Count; i++)
            {
                AssetPayload payload = payloads[i];
                if (payload == null || !File.Exists(payload.FullPath)) continue;
                archive.CreateEntryFromFile(payload.FullPath, payload.EntryPath, SysCompressionLevel.Optimal);
            }
        }

        private static string ExtractAssets(ZipArchive archive, string packagePath)
        {
            string pkgName = SafeName(Path.GetFileNameWithoutExtension(packagePath));
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            string moduleDir = Path.GetDirectoryName(typeof(OvPackageService).Assembly.Location);
            string targetRoot = Path.Combine(moduleDir, "Assets", "Overlayer", "Imported", $"{pkgName}_{timestamp}");
            bool created = false;
            try
            {
                Directory.CreateDirectory(targetRoot);
                created = true;
                string fullRoot = Path.GetFullPath(targetRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                var extracted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in archive.Entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.Name)) continue;
                    string entryName = NormalizeEntryName(entry.FullName);
                    if (!entryName.StartsWith(AssetsPrefix + "/", StringComparison.OrdinalIgnoreCase)) continue;
                    string relativePath = entryName.Substring(AssetsPrefix.Length + 1)
                        .Replace('/', Path.DirectorySeparatorChar);
                    string targetPath = Path.GetFullPath(Path.Combine(targetRoot, relativePath));
                    if (!targetPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException(L($"配置包包含非法资源路径: {entry.FullName}", $"The package contains an invalid asset path: {entry.FullName}"));
                    if (!extracted.Add(entryName))
                        throw new InvalidDataException(L($"配置包包含重复资源路径: {entry.FullName}", $"The package contains a duplicate asset path: {entry.FullName}"));
                    string parent = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                    entry.ExtractToFile(targetPath, overwrite: false);
                }
                return targetRoot;
            }
            catch
            {
                if (created) TryDeleteDirectoryOrFile(targetRoot);
                throw;
            }
        }

        private static void RewriteImportedAssetPaths(OvTransferPackage package, string importedRoot)
        {
            var map = BuildImportedAssetMap(importedRoot);
            for (int i = 0; i < package.Texts.Count; i++)
                if (package.Texts[i] != null) package.Texts[i].FontPath = ResolveImportedAsset(package.Texts[i].FontPath, "Fonts", map);
            for (int i = 0; i < package.Images.Count; i++)
                if (package.Images[i] != null) package.Images[i].ImagePath = ResolveImportedAsset(package.Images[i].ImagePath, "Images", map);
            for (int i = 0; i < package.Videos.Count; i++)
                if (package.Videos[i] != null) package.Videos[i].VideoPath = ResolveImportedAsset(package.Videos[i].VideoPath, "Videos", map);
        }

        private static Dictionary<string, string> BuildImportedAssetMap(string importedRoot)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(importedRoot) || !Directory.Exists(importedRoot)) return map;
            string[] files = Directory.GetFiles(importedRoot, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string relative = GetRelativePathCompat(importedRoot, files[i]);
                map[AssetsPrefix + "/" + relative] = files[i];
            }
            return map;
        }

        private static string ResolveImportedAsset(string value, string category, Dictionary<string, string> map)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            string normalized = NormalizeEntryName(value.Trim().Trim('"'));
            if (!normalized.StartsWith(AssetsPrefix + "/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.StartsWith(category + "/", StringComparison.OrdinalIgnoreCase)
                    ? AssetsPrefix + "/" + normalized : normalized;
            if (map.TryGetValue(normalized, out string extracted)) return extracted;
            return value;
        }

        private static string NormalizeEntryName(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/').TrimStart('/');
        }

        private static string GetRelativePathCompat(string root, string path)
        {
            string rootFull = Path.GetFullPath(root);
            string pathFull = Path.GetFullPath(path);
            if (!rootFull.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                && !rootFull.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                rootFull += Path.DirectorySeparatorChar;
            }
            Uri rootUri = new Uri(rootFull, UriKind.Absolute);
            Uri pathUri = new Uri(pathFull, UriKind.Absolute);
            return NormalizeEntryName(Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString()));
        }

        private static void ValidateArchive(ZipArchive archive)
        {
            if (archive == null || archive.Entries.Count > MaxArchiveEntries)
                throw new InvalidDataException(L("配置包条目数量超出限制", "The package has too many entries"));
            long assetBytes = 0;
            for (int i = 0; i < archive.Entries.Count; i++)
            {
                ZipArchiveEntry entry = archive.Entries[i];
                string name = NormalizeEntryName(entry.FullName);
                if (name.Equals(ManifestName, StringComparison.OrdinalIgnoreCase)
                    || name.Equals(LegacyManifestName, StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Settings.xml", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("PackageInfo.xml", StringComparison.OrdinalIgnoreCase))
                {
                    if (entry.Length > MaxManifestBytes) throw new InvalidDataException(L("配置清单过大", "The manifest is too large"));
                }
                if (name.StartsWith(AssetsPrefix + "/", StringComparison.OrdinalIgnoreCase))
                {
                    if (entry.Length < 0 || entry.Length > MaxAssetBytes)
                        throw new InvalidDataException(L("配置包中的单个资源过大", "An asset in the package is too large"));
                    assetBytes = checked(assetBytes + entry.Length);
                    if (assetBytes > MaxAssetBytes) throw new InvalidDataException(L("配置包资源总量过大", "The package assets are too large in total"));
                }
            }
        }

        private static T DeserializeEntry<T>(ZipArchiveEntry entry, XmlSerializer serializer, string description)
        {
            if (entry == null) throw new InvalidDataException(L("缺少", "Missing ") + description);
            if (entry.Length > MaxManifestBytes) throw new InvalidDataException(description + L("过大", " is too large"));
            using (Stream stream = entry.Open())
            using (XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxManifestBytes
            }))
            {
                object value = serializer.Deserialize(reader);
                if (!(value is T)) throw new InvalidDataException(description + L("内容无效", " is invalid"));
                return (T)value;
            }
        }

        private static void TryDeleteDirectoryOrFile(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
                else if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        private static List<OvTextComponent> CloneTexts(List<OvTextComponent> source)
        {
            var result = new List<OvTextComponent>();
            if (source == null) return result;
            for (int i = 0; i < source.Count; i++)
            {
                OvTextComponent item = source[i];
                if (item == null) continue;
                result.Add(new OvTextComponent
                {
                    Id = item.Id, Name = item.Name, Enabled = item.Enabled, ShowInGame = item.ShowInGame,
                    OnlyShowPlaying = item.OnlyShowPlaying, PositionX = item.PositionX, PositionY = item.PositionY,
                    Opacity = item.Opacity, Depth = item.Depth, PivotX = item.PivotX, PivotY = item.PivotY,
                    TextFormat = item.TextFormat, TokenSourceSnapshot = item.TokenSourceSnapshot,
                    TokenBindings = OvTextTokenBinding.CloneList(item.TokenBindings),
                    TokenAnimation = OvAnimationGraph.Clone(item.TokenAnimation), FontSize = item.FontSize,
                    FontPath = item.FontPath, Alignment = item.Alignment, TextColor = CloneArray(item.TextColor),
                    OutlineEnabled = item.OutlineEnabled, OutlineColor = CloneArray(item.OutlineColor),
                    OutlineThickness = item.OutlineThickness, ShadowEnabled = item.ShadowEnabled,
                    ShadowColor = CloneArray(item.ShadowColor), ShadowOffset = CloneArray(item.ShadowOffset),
                    ShadowSoftness = item.ShadowSoftness, LetterSpacing = item.LetterSpacing,
                    LineHeightOffset = item.LineHeightOffset
                });
            }
            return result;
        }

        private static List<OvImageComponent> CloneImages(List<OvImageComponent> source)
        {
            var result = new List<OvImageComponent>();
            if (source == null) return result;
            for (int i = 0; i < source.Count; i++)
            {
                OvImageComponent item = source[i];
                if (item == null) continue;
                result.Add(new OvImageComponent
                {
                    Id = item.Id, Name = item.Name, Enabled = item.Enabled, ShowInGame = item.ShowInGame,
                    OnlyShowPlaying = item.OnlyShowPlaying, PositionX = item.PositionX, PositionY = item.PositionY,
                    Opacity = item.Opacity, Depth = item.Depth, PivotX = item.PivotX, PivotY = item.PivotY,
                    ImagePath = item.ImagePath, Scale = item.Scale, Rotation = item.Rotation,
                    NodeAnimation = OvAnimationGraph.Clone(item.NodeAnimation)
                });
            }
            return result;
        }

        private static List<OvVideoComponent> CloneVideos(List<OvVideoComponent> source)
        {
            var result = new List<OvVideoComponent>();
            if (source == null) return result;
            for (int i = 0; i < source.Count; i++)
            {
                OvVideoComponent item = source[i];
                if (item == null) continue;
                result.Add(new OvVideoComponent
                {
                    Id = item.Id, Name = item.Name, Enabled = item.Enabled, ShowInGame = item.ShowInGame,
                    OnlyShowPlaying = item.OnlyShowPlaying, PositionX = item.PositionX, PositionY = item.PositionY,
                    Opacity = item.Opacity, Depth = item.Depth, PivotX = item.PivotX, PivotY = item.PivotY,
                    VideoPath = item.VideoPath, Loop = item.Loop, Width = item.Width, Height = item.Height,
                    ContentScale = item.ContentScale, ContentOffsetX = item.ContentOffsetX,
                    ContentOffsetY = item.ContentOffsetY, Rotation = item.Rotation
                });
            }
            return result;
        }

        private static List<OvProgressComponent> CloneProgressBars(List<OvProgressComponent> source)
        {
            var result = new List<OvProgressComponent>();
            if (source == null) return result;
            for (int i = 0; i < source.Count; i++)
            {
                OvProgressComponent item = source[i];
                if (item == null) continue;
                result.Add(new OvProgressComponent
                {
                    Id = item.Id, Name = item.Name, Enabled = item.Enabled, ShowInGame = item.ShowInGame,
                    OnlyShowPlaying = item.OnlyShowPlaying, PositionX = item.PositionX, PositionY = item.PositionY,
                    Opacity = item.Opacity, Depth = item.Depth, PivotX = item.PivotX, PivotY = item.PivotY,
                    ValueTag = item.ValueTag, Minimum = item.Minimum, Maximum = item.Maximum, Width = item.Width,
                    ValueIsConstant = item.ValueIsConstant, ValueConstant = item.ValueConstant,
                    MinimumTag = item.MinimumTag, MaximumTag = item.MaximumTag,
                    Height = item.Height, FillDirection = item.FillDirection, Reverse = item.Reverse, ClampValue = item.ClampValue,
                    BackgroundColor = CloneArray(item.BackgroundColor),
                    FillColor = CloneArray(item.FillColor), BorderColor = CloneArray(item.BorderColor),
                    EnableFillGradient = item.EnableFillGradient,
                    FillGradientStartColor = CloneArray(item.FillGradientStartColor),
                    FillGradientEndColor = CloneArray(item.FillGradientEndColor),
                    BorderThickness = item.BorderThickness, CornerRadius = item.CornerRadius,
                    EnableShadow = item.EnableShadow, ShadowColor = CloneArray(item.ShadowColor),
                    ShadowOffset = CloneArray(item.ShadowOffset), ShadowSoftness = item.ShadowSoftness
                });
            }
            return result;
        }

        private static float[] CloneArray(float[] source)
        {
            return source == null ? null : (float[])source.Clone();
        }

        internal static string GameRoot()
        {
            try
            {
                var prop = typeof(Application).GetProperty("dataPath", BindingFlags.Public | BindingFlags.Static);
                if (prop != null)
                {
                    string path = prop.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(path))
                        return Path.GetFullPath(Path.Combine(path, ".."));
                }
            }
            catch { }
            return Directory.GetCurrentDirectory();
        }

        internal static string SafeName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return L("配置", "settings");
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (char c in input)
            {
                if (Array.IndexOf(invalid, c) < 0)
                    sb.Append(c);
            }
            return sb.Length > 0 ? sb.ToString() : L("配置", "settings");
        }
    }
}
