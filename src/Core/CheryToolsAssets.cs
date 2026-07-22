using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CheryTools
{
    [Serializable]
    public class KeyViewerPackage
    {
        public int FormatVersion = 3;
        public string ExportedAt = "";
        public int ExportScreenWidth = 0;
        public int ExportScreenHeight = 0;
        public List<KVConfiguration> KeyViewerConfigurations = new List<KVConfiguration>();
    }

    [Serializable]
    public class OverlayerPackage
    {
        public int FormatVersion = 4;
        public string ExportedAt = "";
        public int ExportScreenWidth = 0;
        public int ExportScreenHeight = 0;
        public List<OverlayerText> OverlayerTexts = new List<OverlayerText>();
        public List<OverlayerImage> OverlayerImages = new List<OverlayerImage>();
        public List<OverlayerVideo> OverlayerVideos = new List<OverlayerVideo>();
        public List<OverlayerProgressBar> OverlayerProgressBars = new List<OverlayerProgressBar>();
    }

    public class PackageImportResult
    {
        public bool AppliedResolutionAdaptation = false;
        public int SourceScreenWidth = 0;
        public int SourceScreenHeight = 0;
        public int TargetScreenWidth = 0;
        public int TargetScreenHeight = 0;
        public float ScaleX = 1f;
        public float ScaleY = 1f;
        public float UniformScale = 1f;
        public int ImportedItemCount = 0;
        public string ImportedComponentKind = "";
        public int FirstImportedIndex = -1;

        public bool HasSourceResolution
        {
            get { return SourceScreenWidth > 0 && SourceScreenHeight > 0; }
        }

        public string ToSummary()
        {
            string imported = "已追加 " + ImportedItemCount.ToString() + " 个组件";
            if (!AppliedResolutionAdaptation)
            {
                return imported + "；" + (HasSourceResolution ? "未进行分辨率适配" : "包内没有分辨率信息");
            }

            return imported + "；" + string.Format(
                "已按分辨率适配: {0}x{1} -> {2}x{3} (X {4:0.###}, Y {5:0.###})",
                SourceScreenWidth,
                SourceScreenHeight,
                TargetScreenWidth,
                TargetScreenHeight,
                ScaleX,
                ScaleY);
        }
    }

    internal static class CheryToolsAssets
    {
        private const string AssetsFolderName = "CheryToolsAssets";
        private const string ArchiveAssetsPrefix = "Assets";
        private static readonly Dictionary<string, string> _resolvedAssetPathCache = new Dictionary<string, string>();

        public static string GameRoot
        {
            get
            {
                string dataPath = Application.dataPath;
                if (!string.IsNullOrEmpty(dataPath))
                {
                    return Path.GetFullPath(Path.Combine(dataPath, ".."));
                }

                return Directory.GetCurrentDirectory();
            }
        }

        public static string AssetsRoot
        {
            get { return Path.Combine(GameRoot, AssetsFolderName); }
        }

        public static string ResolveAssetPath(string path)
        {
            string normalized = NormalizeInputPath(path);
            if (string.IsNullOrEmpty(normalized)) return string.Empty;

            if (_resolvedAssetPathCache.TryGetValue(normalized, out string cachedPath))
            {
                return cachedPath;
            }

            string archiveRelative = TryConvertArchiveRelativePath(normalized);
            if (!string.IsNullOrEmpty(archiveRelative))
            {
                string assetPath = Path.Combine(AssetsRoot, archiveRelative);
                if (File.Exists(assetPath)) return CacheResolvedAssetPath(normalized, Path.GetFullPath(assetPath));
            }

            if (Path.IsPathRooted(normalized) && File.Exists(normalized))
            {
                return CacheResolvedAssetPath(normalized, Path.GetFullPath(normalized));
            }

            string fromAssets = Path.Combine(AssetsRoot, normalized);
            if (File.Exists(fromAssets)) return CacheResolvedAssetPath(normalized, Path.GetFullPath(fromAssets));

            string fromGameRoot = Path.Combine(GameRoot, normalized);
            if (File.Exists(fromGameRoot)) return CacheResolvedAssetPath(normalized, Path.GetFullPath(fromGameRoot));

            if (Main.ModEntry != null)
            {
                string fromMod = Path.Combine(Main.ModEntry.Path, normalized);
                if (File.Exists(fromMod)) return CacheResolvedAssetPath(normalized, Path.GetFullPath(fromMod));
            }

            return normalized;
        }

        private static string CacheResolvedAssetPath(string normalized, string resolved)
        {
            if (_resolvedAssetPathCache.Count > 512)
            {
                _resolvedAssetPathCache.Clear();
            }
            _resolvedAssetPathCache[normalized] = resolved;
            return resolved;
        }

        public static string ImportExternalAsset(string path, string category)
        {
            string normalized = NormalizeInputPath(path);
            if (string.IsNullOrEmpty(normalized)) return string.Empty;

            string resolved = ResolveAssetPath(normalized);
            if (string.IsNullOrEmpty(resolved) || !File.Exists(resolved)) return normalized;
            if (string.Equals(category, "Videos", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(Path.GetExtension(resolved), ".mp4", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            string assetRelative = ToArchiveRelativeAssetPath(resolved);
            if (!string.IsNullOrEmpty(assetRelative))
            {
                string canonicalAsset = FindExistingAssetByContent(resolved, category);
                return !string.IsNullOrEmpty(canonicalAsset) ? canonicalAsset : assetRelative;
            }

            string safeCategory = SanitizePathSegment(category);
            if (string.IsNullOrEmpty(safeCategory)) safeCategory = "Misc";

            string targetDir = Path.Combine(AssetsRoot, safeCategory);
            Directory.CreateDirectory(targetDir);

            string existingAsset = FindExistingAssetByContent(resolved, safeCategory);
            if (!string.IsNullOrEmpty(existingAsset)) return existingAsset;

            string fileName = SanitizeFileName(Path.GetFileName(resolved));
            if (string.IsNullOrEmpty(fileName)) fileName = "asset";

            string targetPath = GetUniqueTargetPath(targetDir, fileName, resolved);
            if (!SamePath(resolved, targetPath))
            {
                File.Copy(resolved, targetPath, false);
            }

            return ToArchiveRelativeAssetPath(targetPath) ?? targetPath;
        }

        public static bool ImportSettingsAssets(Settings settings)
        {
            if (settings == null) return false;

            bool changed = false;
            changed |= ImportPath(ref settings.RedPlanetTexturePath, "Images");
            changed |= ImportPath(ref settings.BluePlanetTexturePath, "Images");
            changed |= ImportPath(ref settings.GreenPlanetTexturePath, "Images");
            if (settings.KeyViewerConfigurations != null)
            {
                foreach (KVConfiguration config in settings.KeyViewerConfigurations)
                {
                    if (config == null) continue;
                    changed |= ImportPath(ref config.FontPath, "Fonts");
                }
            }

            if (settings.OverlayerTexts != null)
            {
                foreach (OverlayerText text in settings.OverlayerTexts)
                {
                    if (text == null) continue;
                    changed |= ImportPath(ref text.FontPath, "Fonts");
                }
            }

            if (settings.OverlayerImages != null)
            {
                foreach (OverlayerImage image in settings.OverlayerImages)
                {
                    if (image == null) continue;
                    changed |= ImportPath(ref image.ImagePath, "Images");
                }
            }

            if (settings.OverlayerVideos != null)
            {
                foreach (OverlayerVideo video in settings.OverlayerVideos)
                {
                    if (video == null) continue;
                    changed |= ImportPath(ref video.VideoPath, "Videos");
                }
            }

            changed |= ImportNodeAssetPaths(settings.GetAllKeyViewerNodes());

            return changed;
        }

        public static string ExportCytPackage(Settings settings, string outputPath)
        {
            if (settings == null) throw new InvalidOperationException("Settings is null.");

            Directory.CreateDirectory(AssetsRoot);
            Settings exportSettings = CloneSettings(settings);
            RewriteAssetPathsForExport(exportSettings);
            List<string> assetPaths = CollectSettingsAssetPaths(exportSettings);

            if (File.Exists(outputPath)) File.Delete(outputPath);

            using (FileStream stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                ZipArchiveEntry settingsEntry = archive.CreateEntry("Settings.xml", System.IO.Compression.CompressionLevel.Optimal);
                using (Stream entryStream = settingsEntry.Open())
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(Settings));
                    serializer.Serialize(entryStream, exportSettings);
                }

                HashSet<string> addedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (assetPaths != null)
                {
                    foreach (string assetPath in assetPaths)
                    {
                        AddPackageAsset(archive, assetPath, addedEntries);
                    }
                }
            }

            return outputPath;
        }

        public static string ExportKeyViewerPackage(Settings settings, string outputPath)
        {
            if (settings == null) throw new InvalidOperationException("Settings is null.");

            Directory.CreateDirectory(AssetsRoot);
            KeyViewerPackage package = CreateKeyViewerPackage(settings);
            RewriteKeyViewerPackageAssetPaths(package);

            WritePackage(outputPath, "KeyViewer.xml", package, CollectKeyViewerAssetPaths(package));
            return outputPath;
        }

        public static string ExportKeyViewerPackage(Settings settings, KVConfiguration configuration, string outputPath)
        {
            if (settings == null) throw new InvalidOperationException("Settings is null.");
            if (configuration == null) throw new InvalidOperationException("KeyViewer configuration is null.");

            Directory.CreateDirectory(AssetsRoot);
            KeyViewerPackage package = CreateKeyViewerPackage(settings, new List<KVConfiguration> { configuration });
            RewriteKeyViewerPackageAssetPaths(package);
            WritePackage(outputPath, "KeyViewer.xml", package, CollectKeyViewerAssetPaths(package));
            return outputPath;
        }

        public static string ExportOverlayerPackage(Settings settings, string outputPath)
        {
            if (settings == null) throw new InvalidOperationException("Settings is null.");

            Directory.CreateDirectory(AssetsRoot);
            OverlayerPackage package = CreateOverlayerPackage(settings);
            RewriteOverlayerPackageAssetPaths(package);

            WritePackage(outputPath, "Overlayer.xml", package, CollectOverlayerAssetPaths(package));
            return outputPath;
        }

        public static string ExportOverlayerComponentPackage(Settings settings, string componentKind, int componentIndex,
            string outputPath)
        {
            if (settings == null) throw new InvalidOperationException("Settings is null.");

            Directory.CreateDirectory(AssetsRoot);
            OverlayerPackage package = CreateOverlayerPackage(settings, componentKind, componentIndex);
            RewriteOverlayerPackageAssetPaths(package);
            WritePackage(outputPath, "Overlayer.xml", package, CollectOverlayerAssetPaths(package));
            return outputPath;
        }

        public static PackageImportResult ImportKeyViewerPackage(Settings settings, string packagePath)
        {
            if (settings == null) throw new InvalidOperationException("Settings is null.");

            string normalizedPackagePath = NormalizeInputPath(packagePath);
            if (string.IsNullOrEmpty(normalizedPackagePath) || !File.Exists(normalizedPackagePath))
                throw new FileNotFoundException("KeyViewer package not found.", normalizedPackagePath);

            using (ZipArchive archive = ZipFile.OpenRead(normalizedPackagePath))
            {
                KeyViewerPackage package = ReadPackageManifest<KeyViewerPackage>(archive, "KeyViewer.xml");
                ExtractAssetsFromArchive(archive);
                PackageImportResult result = AdaptKeyViewerPackageToCurrentResolution(package);
                result.ImportedItemCount = package.KeyViewerConfigurations != null ? package.KeyViewerConfigurations.Count : 0;
                result.ImportedComponentKind = "kv";
                result.FirstImportedIndex = settings.KeyViewerConfigurations != null ? settings.KeyViewerConfigurations.Count : 0;
                ApplyKeyViewerPackage(settings, package);
                return result;
            }
        }

        public static PackageImportResult ImportOverlayerPackage(Settings settings, string packagePath)
        {
            if (settings == null) throw new InvalidOperationException("Settings is null.");

            string normalizedPackagePath = NormalizeInputPath(packagePath);
            if (string.IsNullOrEmpty(normalizedPackagePath) || !File.Exists(normalizedPackagePath))
                throw new FileNotFoundException("Overlayer package not found.", normalizedPackagePath);

            using (ZipArchive archive = ZipFile.OpenRead(normalizedPackagePath))
            {
                OverlayerPackage package = ReadPackageManifest<OverlayerPackage>(archive, "Overlayer.xml");
                ExtractAssetsFromArchive(archive);
                PackageImportResult result = AdaptOverlayerPackageToCurrentResolution(package);
                result.ImportedItemCount = (package.OverlayerTexts != null ? package.OverlayerTexts.Count : 0)
                    + (package.OverlayerImages != null ? package.OverlayerImages.Count : 0)
                    + (package.OverlayerVideos != null ? package.OverlayerVideos.Count : 0)
                    + (package.OverlayerProgressBars != null ? package.OverlayerProgressBars.Count : 0);
                if (package.OverlayerTexts != null && package.OverlayerTexts.Count > 0)
                {
                    result.ImportedComponentKind = "text";
                    result.FirstImportedIndex = settings.OverlayerTexts != null ? settings.OverlayerTexts.Count : 0;
                }
                else if (package.OverlayerImages != null && package.OverlayerImages.Count > 0)
                {
                    result.ImportedComponentKind = "image";
                    result.FirstImportedIndex = settings.OverlayerImages != null ? settings.OverlayerImages.Count : 0;
                }
                else if (package.OverlayerVideos != null && package.OverlayerVideos.Count > 0)
                {
                    result.ImportedComponentKind = "video";
                    result.FirstImportedIndex = settings.OverlayerVideos != null ? settings.OverlayerVideos.Count : 0;
                }
                else if (package.OverlayerProgressBars != null && package.OverlayerProgressBars.Count > 0)
                {
                    result.ImportedComponentKind = "progress";
                    result.FirstImportedIndex = settings.OverlayerProgressBars != null ? settings.OverlayerProgressBars.Count : 0;
                }
                ApplyOverlayerPackage(settings, package);
                return result;
            }
        }

        private static KeyViewerPackage CreateKeyViewerPackage(Settings settings,
            List<KVConfiguration> configurations = null)
        {
            KeyViewerPackage package = new KeyViewerPackage();
            package.ExportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            StampExportScreenSize(package);
            package.KeyViewerConfigurations = CloneByXml(configurations ?? settings.KeyViewerConfigurations)
                ?? new List<KVConfiguration>();
            ResetPackageKeyViewerStatistics(package.KeyViewerConfigurations);
            return package;
        }

        private static OverlayerPackage CreateOverlayerPackage(Settings settings)
        {
            OverlayerPackage package = new OverlayerPackage();
            package.ExportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            StampExportScreenSize(package);
            package.OverlayerTexts = CloneByXml(settings.OverlayerTexts) ?? new List<OverlayerText>();
            package.OverlayerImages = CloneByXml(settings.OverlayerImages) ?? new List<OverlayerImage>();
            package.OverlayerVideos = CloneByXml(settings.OverlayerVideos) ?? new List<OverlayerVideo>();
            package.OverlayerProgressBars = CloneByXml(settings.OverlayerProgressBars) ?? new List<OverlayerProgressBar>();
            return package;
        }

        private static OverlayerPackage CreateOverlayerPackage(Settings settings, string componentKind,
            int componentIndex)
        {
            var package = new OverlayerPackage { ExportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            StampExportScreenSize(package);
            string kind = (componentKind ?? string.Empty).Trim().ToLowerInvariant();
            if (kind == "text" && settings.OverlayerTexts != null
                && componentIndex >= 0 && componentIndex < settings.OverlayerTexts.Count)
                package.OverlayerTexts.Add(CloneByXml(settings.OverlayerTexts[componentIndex]));
            else if (kind == "image" && settings.OverlayerImages != null
                && componentIndex >= 0 && componentIndex < settings.OverlayerImages.Count)
                package.OverlayerImages.Add(CloneByXml(settings.OverlayerImages[componentIndex]));
            else if (kind == "video" && settings.OverlayerVideos != null
                && componentIndex >= 0 && componentIndex < settings.OverlayerVideos.Count)
                package.OverlayerVideos.Add(CloneByXml(settings.OverlayerVideos[componentIndex]));
            else if (kind == "progress" && settings.OverlayerProgressBars != null
                && componentIndex >= 0 && componentIndex < settings.OverlayerProgressBars.Count)
                package.OverlayerProgressBars.Add(CloneByXml(settings.OverlayerProgressBars[componentIndex]));
            else
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Overlayer component was not found.");
            return package;
        }

        private static void ResetPackageKeyViewerStatistics(List<KVConfiguration> configurations)
        {
            if (configurations == null) return;
            foreach (KVConfiguration config in configurations)
            {
                if (config == null) continue;
                config.TotalHits = 0;
                if (config.Nodes == null) continue;
                foreach (KVNode node in config.Nodes) if (node != null) node.HitCount = 0;
            }
        }

        private static void RewriteKeyViewerPackageAssetPaths(KeyViewerPackage package)
        {
            if (package.KeyViewerConfigurations == null) return;

            foreach (KVConfiguration config in package.KeyViewerConfigurations)
            {
                if (config == null) continue;
                config.FontPath = PreparePathForExport(config.FontPath, "Fonts");
                RewriteNodeAssetPaths(config.Nodes);
            }
        }

        private static void RewriteOverlayerPackageAssetPaths(OverlayerPackage package)
        {
            if (package.OverlayerTexts != null)
            {
                foreach (OverlayerText text in package.OverlayerTexts)
                {
                    if (text == null) continue;
                    text.FontPath = PreparePathForExport(text.FontPath, "Fonts");
                }
            }

            if (package.OverlayerImages != null)
            {
                foreach (OverlayerImage image in package.OverlayerImages)
                {
                    if (image == null) continue;
                    image.ImagePath = PreparePathForExport(image.ImagePath, "Images");
                }
            }

            if (package.OverlayerVideos != null)
            {
                foreach (OverlayerVideo video in package.OverlayerVideos)
                {
                    if (video == null) continue;
                    video.VideoPath = PreparePathForExport(video.VideoPath, "Videos");
                }
            }
        }

        private static List<string> CollectKeyViewerAssetPaths(KeyViewerPackage package)
        {
            List<string> paths = new List<string>();
            if (package.KeyViewerConfigurations == null) return paths;

            foreach (KVConfiguration config in package.KeyViewerConfigurations)
            {
                if (config == null) continue;
                AddAssetPath(paths, config.FontPath);
                if (config.Nodes == null) continue;
                foreach (KVNode node in config.Nodes)
                {
                    if (node == null) continue;
                    AddAssetPath(paths, node.KeyFontPath);
                    AddAssetPath(paths, node.CountFontPath);
                    AddAssetPath(paths, node.ImagePath);
                    AddAssetPath(paths, node.VideoPath);
                }
            }
            return paths;
        }

        private static List<string> CollectOverlayerAssetPaths(OverlayerPackage package)
        {
            List<string> paths = new List<string>();
            if (package.OverlayerTexts != null)
            {
                foreach (OverlayerText text in package.OverlayerTexts)
                {
                    if (text == null) continue;
                    AddAssetPath(paths, text.FontPath);
                }
            }
            if (package.OverlayerImages != null)
            {
                foreach (OverlayerImage image in package.OverlayerImages)
                {
                    if (image == null) continue;
                    AddAssetPath(paths, image.ImagePath);
                }
            }
            if (package.OverlayerVideos != null)
            {
                foreach (OverlayerVideo video in package.OverlayerVideos)
                {
                    if (video == null) continue;
                    AddAssetPath(paths, video.VideoPath);
                }
            }
            return paths;
        }

        private static List<string> CollectSettingsAssetPaths(Settings settings)
        {
            List<string> paths = new List<string>();
            if (settings == null) return paths;

            AddAssetPath(paths, settings.RedPlanetTexturePath);
            AddAssetPath(paths, settings.BluePlanetTexturePath);
            AddAssetPath(paths, settings.GreenPlanetTexturePath);
            if (settings.KeyViewerConfigurations != null)
            {
                foreach (KVConfiguration config in settings.KeyViewerConfigurations)
                {
                    if (config == null) continue;
                    AddAssetPath(paths, config.FontPath);
                    AddNodeAssetPaths(paths, config.Nodes);
                }
            }

            if (settings.OverlayerTexts != null)
            {
                foreach (OverlayerText text in settings.OverlayerTexts)
                {
                    if (text == null) continue;
                    AddAssetPath(paths, text.FontPath);
                }
            }

            if (settings.OverlayerImages != null)
            {
                foreach (OverlayerImage image in settings.OverlayerImages)
                {
                    if (image == null) continue;
                    AddAssetPath(paths, image.ImagePath);
                }
            }

            if (settings.OverlayerVideos != null)
            {
                foreach (OverlayerVideo video in settings.OverlayerVideos)
                {
                    if (video == null) continue;
                    AddAssetPath(paths, video.VideoPath);
                }
            }

            return paths;
        }

        private static void AddNodeAssetPaths(List<string> paths, List<KVNode> nodes)
        {
            if (paths == null || nodes == null) return;

            foreach (KVNode node in nodes)
            {
                if (node == null) continue;
                AddAssetPath(paths, node.KeyFontPath);
                AddAssetPath(paths, node.CountFontPath);
                AddAssetPath(paths, node.ImagePath);
                AddAssetPath(paths, node.VideoPath);
            }
        }

        private static void StampExportScreenSize(KeyViewerPackage package)
        {
            int width;
            int height;
            GetCurrentScreenSize(out width, out height);
            package.ExportScreenWidth = width;
            package.ExportScreenHeight = height;
        }

        private static void StampExportScreenSize(OverlayerPackage package)
        {
            int width;
            int height;
            GetCurrentScreenSize(out width, out height);
            package.ExportScreenWidth = width;
            package.ExportScreenHeight = height;
        }

        private static void GetCurrentScreenSize(out int width, out int height)
        {
            try
            {
                ReadUnityScreenSize(out width, out height);
            }
            catch (Exception)
            {
                // Package logic is also exercised outside Unity by the regression harness.
                width = 0;
                height = 0;
            }
        }

        private static void ReadUnityScreenSize(out int width, out int height)
        {
            width = Mathf.RoundToInt(Mathf.Max(1f, Screen.width));
            height = Mathf.RoundToInt(Mathf.Max(1f, Screen.height));
        }

        private static PackageImportResult CreateResolutionImportResult(int sourceWidth, int sourceHeight)
        {
            int targetWidth;
            int targetHeight;
            GetCurrentScreenSize(out targetWidth, out targetHeight);

            PackageImportResult result = new PackageImportResult();
            result.SourceScreenWidth = sourceWidth;
            result.SourceScreenHeight = sourceHeight;
            result.TargetScreenWidth = targetWidth;
            result.TargetScreenHeight = targetHeight;

            if (sourceWidth <= 0 || sourceHeight <= 0 || targetWidth <= 0 || targetHeight <= 0)
            {
                return result;
            }

            result.ScaleX = targetWidth / (float)sourceWidth;
            result.ScaleY = targetHeight / (float)sourceHeight;
            result.UniformScale = Mathf.Min(result.ScaleX, result.ScaleY);
            result.AppliedResolutionAdaptation = !Approximately(result.ScaleX, 1f) || !Approximately(result.ScaleY, 1f);
            return result;
        }

        private static PackageImportResult AdaptKeyViewerPackageToCurrentResolution(KeyViewerPackage package)
        {
            if (package == null) return new PackageImportResult();

            PackageImportResult result = CreateResolutionImportResult(package.ExportScreenWidth, package.ExportScreenHeight);
            if (!result.AppliedResolutionAdaptation)
            {
                return result;
            }

            float scale = result.UniformScale;
            ScaleKeyViewerConfigurations(package.KeyViewerConfigurations, scale);
            return result;
        }

        private static void ScaleKeyViewerConfigurations(List<KVConfiguration> configurations, float scale)
        {
            if (configurations == null) return;

            foreach (KVConfiguration config in configurations)
            {
                if (config == null) continue;

                config.Scale = ScaleValue(config.Scale, scale);
                config.BorderThickness = ScaleValue(config.BorderThickness, scale);
                config.KeyRainSpeed = ScaleValue(config.KeyRainSpeed, scale);
                config.KeyRainMaxHeight = ScaleValue(config.KeyRainMaxHeight, scale);
                config.KeyRainYOffsetRow1 = ScaleValue(config.KeyRainYOffsetRow1, scale);
                config.KeyRainYOffsetRow2 = ScaleValue(config.KeyRainYOffsetRow2, scale);

                ScaleKeyViewerNodes(config.Nodes, scale);
            }
        }

        private static void ScaleKeyViewerNodes(List<KVNode> nodes, float scale)
        {
            if (nodes == null) return;

            foreach (KVNode node in nodes)
            {
                if (node == null) continue;

                node.BorderThickness = ScaleOptionalValue(node.BorderThickness, scale);
                node.RainYOffset = ScaleValue(node.RainYOffset, scale);
            }
        }

        private static PackageImportResult AdaptOverlayerPackageToCurrentResolution(OverlayerPackage package)
        {
            if (package == null) return new PackageImportResult();

            PackageImportResult result = CreateResolutionImportResult(package.ExportScreenWidth, package.ExportScreenHeight);
            if (!result.AppliedResolutionAdaptation)
            {
                return result;
            }

            ScaleOverlayerTexts(package.OverlayerTexts, result.ScaleX, result.ScaleY, result.UniformScale);
            ScaleOverlayerImages(package.OverlayerImages, result.ScaleX, result.ScaleY, result.UniformScale);
            ScaleOverlayerVideos(package.OverlayerVideos, result.ScaleX, result.ScaleY, result.UniformScale);
            ScaleOverlayerProgressBars(package.OverlayerProgressBars, result.ScaleX, result.ScaleY, result.UniformScale);
            return result;
        }

        private static void ScaleOverlayerTexts(List<OverlayerText> texts, float scaleX, float scaleY, float uniformScale)
        {
            if (texts == null) return;

            foreach (OverlayerText text in texts)
            {
                if (text == null) continue;

                text.PositionX = ScaleValue(text.PositionX, scaleX);
                text.PositionY = ScaleValue(text.PositionY, scaleY);
                text.FontSize = ScaleValue(text.FontSize, uniformScale);
                text.LetterSpacing = ScaleValue(text.LetterSpacing, uniformScale);
                text.LineHeightOffset = ScaleValue(text.LineHeightOffset, uniformScale);
                ScaleOverlayerAnimations(text.Animations, scaleX, scaleY);
                ScaleTokenAnimationGraph(text.TokenAnimation, scaleX, scaleY);
            }
        }

        private static void ScaleOverlayerImages(List<OverlayerImage> images, float scaleX, float scaleY, float uniformScale)
        {
            if (images == null) return;

            foreach (OverlayerImage image in images)
            {
                if (image == null) continue;

                image.PositionX = ScaleValue(image.PositionX, scaleX);
                image.PositionY = ScaleValue(image.PositionY, scaleY);
                image.Scale = ScaleValue(image.Scale, uniformScale);
                ScaleOverlayerAnimations(image.Animations, scaleX, scaleY);
            }
        }

        private static void ScaleOverlayerVideos(List<OverlayerVideo> videos, float scaleX, float scaleY, float uniformScale)
        {
            if (videos == null) return;

            foreach (OverlayerVideo video in videos)
            {
                if (video == null) continue;

                video.PositionX = ScaleValue(video.PositionX, scaleX);
                video.PositionY = ScaleValue(video.PositionY, scaleY);
                video.Width = ScaleValue(video.Width, uniformScale);
                video.Height = ScaleValue(video.Height, uniformScale);
            }
        }

        private static void ScaleOverlayerProgressBars(List<OverlayerProgressBar> bars, float scaleX, float scaleY, float uniformScale)
        {
            if (bars == null) return;

            foreach (OverlayerProgressBar bar in bars)
            {
                if (bar == null) continue;

                bar.PositionX = ScaleValue(bar.PositionX, scaleX);
                bar.PositionY = ScaleValue(bar.PositionY, scaleY);
                bar.Width = ScaleValue(bar.Width, scaleX);
                bar.Height = ScaleValue(bar.Height, scaleY);
                bar.BorderThickness = ScaleValue(bar.BorderThickness, uniformScale);
                bar.CornerRadius = ScaleValue(bar.CornerRadius, uniformScale);
                ScalePair(bar.ShadowOffset, scaleX, scaleY);
                bar.ShadowSoftness = ScaleValue(bar.ShadowSoftness, uniformScale);
            }
        }

        private static void ScaleOverlayerAnimations(List<OverlayerAnimation> animations, float scaleX, float scaleY)
        {
            if (animations == null) return;

            foreach (OverlayerAnimation animation in animations)
            {
                if (animation == null) continue;

                animation.StartX = ScaleValue(animation.StartX, scaleX);
                animation.StartY = ScaleValue(animation.StartY, scaleY);
                animation.EndX = ScaleValue(animation.EndX, scaleX);
                animation.EndY = ScaleValue(animation.EndY, scaleY);
                ScaleAnimationJsonOffsets(animation, scaleX, scaleY);
                animation.ParseJson();
            }
        }

        private static void ScaleTokenAnimationGraph(OvAnimationGraph graph, float scaleX, float scaleY)
        {
            if (graph == null || graph.Nodes == null) return;
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                OvAnimationNode node = graph.Nodes[i];
                if (node == null || node.Kind != OvAnimationNodeKind.Tween || node.TweenProperty != OvTokenTweenProperty.Position) continue;
                node.FromX = ScaleValue(node.FromX, scaleX);
                node.FromY = ScaleValue(node.FromY, scaleY);
                node.ToX = ScaleValue(node.ToX, scaleX);
                node.ToY = ScaleValue(node.ToY, scaleY);
            }
        }

        private static void ScaleAnimationJsonOffsets(OverlayerAnimation animation, float scaleX, float scaleY)
        {
            if (animation == null || string.IsNullOrWhiteSpace(animation.JsonString))
            {
                return;
            }

            try
            {
                JArray frames = JArray.Parse(animation.JsonString);
                bool changed = false;
                foreach (JToken token in frames)
                {
                    JObject frame = token as JObject;
                    if (frame == null) continue;

                    JToken xToken = frame["x"];
                    if (xToken != null && xToken.Type != JTokenType.Null)
                    {
                        frame["x"] = xToken.Value<float>() * scaleX;
                        changed = true;
                    }

                    JToken yToken = frame["y"];
                    if (yToken != null && yToken.Type != JTokenType.Null)
                    {
                        frame["y"] = yToken.Value<float>() * scaleY;
                        changed = true;
                    }
                }

                if (changed)
                {
                    animation.JsonString = frames.ToString(Formatting.Indented);
                }
            }
            catch
            {
                // Keep custom animation JSON untouched when it is not a frame array.
            }
        }

        private static float ScaleValue(float value, float scale)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return value;
            }

            return value * scale;
        }

        private static float ScaleOptionalValue(float value, float scale)
        {
            return value < 0f ? value : ScaleValue(value, scale);
        }

        private static void ScalePair(float[] pair, float scaleX, float scaleY)
        {
            if (pair == null || pair.Length < 2) return;
            pair[0] = ScaleValue(pair[0], scaleX);
            pair[1] = ScaleValue(pair[1], scaleY);
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.0001f;
        }

        private static void AddAssetPath(List<string> paths, string path)
        {
            string normalized = NormalizeInputPath(path);
            if (string.IsNullOrEmpty(normalized)) return;
            if (string.IsNullOrEmpty(TryConvertArchiveRelativePath(normalized))) return;
            paths.Add(normalized);
        }

        private static void WritePackage<T>(string outputPath, string manifestName, T manifest, List<string> assetPaths)
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);

            using (FileStream stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                ZipArchiveEntry manifestEntry = archive.CreateEntry(manifestName, System.IO.Compression.CompressionLevel.Optimal);
                using (Stream entryStream = manifestEntry.Open())
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(T));
                    serializer.Serialize(entryStream, manifest);
                }

                HashSet<string> addedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (assetPaths == null) return;

                foreach (string assetPath in assetPaths)
                {
                    AddPackageAsset(archive, assetPath, addedEntries);
                }
            }
        }

        private static void AddPackageAsset(ZipArchive archive, string assetPath, HashSet<string> addedEntries)
        {
            string relative = TryConvertArchiveRelativePath(assetPath);
            if (string.IsNullOrEmpty(relative)) return;

            string fullPath = Path.Combine(AssetsRoot, relative);
            if (!File.Exists(fullPath)) return;

            string entryName = ArchiveAssetsPrefix + "/" + relative.Replace('\\', '/');
            if (!addedEntries.Add(entryName)) return;

            archive.CreateEntryFromFile(fullPath, entryName, System.IO.Compression.CompressionLevel.Optimal);
        }

        private static T ReadPackageManifest<T>(ZipArchive archive, string manifestName)
        {
            ZipArchiveEntry manifestEntry = archive.GetEntry(manifestName);
            if (manifestEntry == null)
                throw new InvalidDataException("Package does not contain " + manifestName + ".");

            using (Stream entryStream = manifestEntry.Open())
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                object value = serializer.Deserialize(entryStream);
                if (value == null)
                    throw new InvalidDataException("Package manifest is empty: " + manifestName + ".");
                return (T)value;
            }
        }

        private static void ExtractAssetsFromArchive(ZipArchive archive)
        {
            Directory.CreateDirectory(AssetsRoot);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                string entryName = entry.FullName.Replace('\\', '/');
                string prefix = ArchiveAssetsPrefix + "/";
                if (!entryName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relative = entryName.Substring(prefix.Length).Replace('/', Path.DirectorySeparatorChar);
                if (string.IsNullOrWhiteSpace(relative))
                {
                    continue;
                }

                string targetPath = Path.Combine(AssetsRoot, relative);
                if (!IsPathUnderRoot(targetPath, AssetsRoot))
                {
                    throw new InvalidDataException("Package contains an invalid asset path: " + entry.FullName);
                }

                string targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                entry.ExtractToFile(targetPath, true);
            }
        }

        private static void ApplyKeyViewerPackage(Settings settings, KeyViewerPackage package)
        {
            if (package == null)
                throw new InvalidDataException("KeyViewer package manifest is empty.");

            settings.EnsureKeyViewerConfigurations();
            int firstImportedIndex = settings.KeyViewerConfigurations.Count;
            List<KVConfiguration> imported = package.KeyViewerConfigurations ?? new List<KVConfiguration>();
            ResetPackageKeyViewerStatistics(imported);
            foreach (KVConfiguration config in imported)
            {
                if (config == null) continue;
                config.Name = MakeUniqueName(config.Name, "导入配置",
                    settings.KeyViewerConfigurations.ConvertAll(item => item != null ? item.Name : string.Empty));
                settings.KeyViewerConfigurations.Add(config);
            }
            if (settings.KeyViewerConfigurations.Count > firstImportedIndex)
                settings.KeyViewerSelectedConfigIndex = firstImportedIndex;
            settings.EnsureKeyViewerConfigurations();
        }

        private static void ApplyOverlayerPackage(Settings settings, OverlayerPackage package)
        {
            if (package == null)
                throw new InvalidDataException("Overlayer package manifest is empty.");

            if (settings.OverlayerTexts == null) settings.OverlayerTexts = new List<OverlayerText>();
            if (settings.OverlayerImages == null) settings.OverlayerImages = new List<OverlayerImage>();
            if (settings.OverlayerVideos == null) settings.OverlayerVideos = new List<OverlayerVideo>();
            if (settings.OverlayerProgressBars == null) settings.OverlayerProgressBars = new List<OverlayerProgressBar>();

            AppendNamedItems(settings.OverlayerTexts, package.OverlayerTexts, "导入文本", item => item.Name, (item, name) => item.Name = name);
            if (package.OverlayerImages != null)
                foreach (OverlayerImage item in package.OverlayerImages) if (item != null) settings.OverlayerImages.Add(item);
            AppendNamedItems(settings.OverlayerVideos, package.OverlayerVideos, "导入视频", item => item.Name, (item, name) => item.Name = name);
            AppendNamedItems(settings.OverlayerProgressBars, package.OverlayerProgressBars, "导入进度条", item => item.Name, (item, name) => item.Name = name);
        }

        private static void AppendNamedItems<T>(List<T> target, List<T> imported, string fallback,
            Func<T, string> getName, Action<T, string> setName) where T : class
        {
            if (target == null || imported == null) return;
            var names = target.ConvertAll(item => item != null ? getName(item) : string.Empty);
            foreach (T item in imported)
            {
                if (item == null) continue;
                string unique = MakeUniqueName(getName(item), fallback, names);
                setName(item, unique);
                names.Add(unique);
                target.Add(item);
            }
        }

        private static string MakeUniqueName(string requested, string fallback, List<string> existing)
        {
            string baseName = string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
            var used = new HashSet<string>(existing ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            if (!used.Contains(baseName)) return baseName;
            for (int suffix = 2; suffix < int.MaxValue; suffix++)
            {
                string candidate = baseName + " (" + suffix.ToString() + ")";
                if (!used.Contains(candidate)) return candidate;
            }
            return baseName + " (导入)";
        }

        public static void ImportCytPackage(string packagePath, string settingsPath)
        {
            string normalizedPackagePath = NormalizeInputPath(packagePath);
            if (string.IsNullOrEmpty(normalizedPackagePath) || !File.Exists(normalizedPackagePath))
            {
                throw new FileNotFoundException("CYT package not found.", normalizedPackagePath);
            }

            if (string.IsNullOrEmpty(settingsPath))
            {
                throw new ArgumentException("Settings path is empty.", nameof(settingsPath));
            }

            Directory.CreateDirectory(AssetsRoot);

            using (ZipArchive archive = ZipFile.OpenRead(normalizedPackagePath))
            {
                ZipArchiveEntry settingsEntry = archive.GetEntry("Settings.xml");
                if (settingsEntry == null)
                {
                    throw new InvalidDataException("CYT package does not contain Settings.xml.");
                }

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        continue;
                    }

                    string entryName = entry.FullName.Replace('\\', '/');
                    string prefix = ArchiveAssetsPrefix + "/";
                    if (!entryName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string relative = entryName.Substring(prefix.Length).Replace('/', Path.DirectorySeparatorChar);
                    if (string.IsNullOrWhiteSpace(relative))
                    {
                        continue;
                    }

                    string targetPath = Path.Combine(AssetsRoot, relative);
                    if (!IsPathUnderRoot(targetPath, AssetsRoot))
                    {
                        throw new InvalidDataException("CYT package contains an invalid asset path: " + entry.FullName);
                    }

                    string targetDirectory = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    entry.ExtractToFile(targetPath, true);
                }

                string settingsDirectory = Path.GetDirectoryName(settingsPath);
                if (!string.IsNullOrEmpty(settingsDirectory))
                {
                    Directory.CreateDirectory(settingsDirectory);
                }
                settingsEntry.ExtractToFile(settingsPath, true);
            }
        }

        private static void RewriteAssetPathsForExport(Settings settings)
        {
            settings.RedPlanetTexturePath = PreparePathForExport(settings.RedPlanetTexturePath, "Images");
            settings.BluePlanetTexturePath = PreparePathForExport(settings.BluePlanetTexturePath, "Images");
            settings.GreenPlanetTexturePath = PreparePathForExport(settings.GreenPlanetTexturePath, "Images");
            if (settings.KeyViewerConfigurations != null)
            {
                foreach (KVConfiguration config in settings.KeyViewerConfigurations)
                {
                    if (config == null) continue;
                    config.FontPath = PreparePathForExport(config.FontPath, "Fonts");
                }
            }

            if (settings.OverlayerTexts != null)
            {
                foreach (OverlayerText text in settings.OverlayerTexts)
                {
                    if (text == null) continue;
                    text.FontPath = PreparePathForExport(text.FontPath, "Fonts");
                }
            }

            if (settings.OverlayerImages != null)
            {
                foreach (OverlayerImage image in settings.OverlayerImages)
                {
                    if (image == null) continue;
                    image.ImagePath = PreparePathForExport(image.ImagePath, "Images");
                }
            }

            if (settings.OverlayerVideos != null)
            {
                foreach (OverlayerVideo video in settings.OverlayerVideos)
                {
                    if (video == null) continue;
                    video.VideoPath = PreparePathForExport(video.VideoPath, "Videos");
                }
            }

            RewriteNodeAssetPaths(settings.GetAllKeyViewerNodes());
        }

        private static void RewriteNodeAssetPaths(List<KVNode> nodes)
        {
            if (nodes == null) return;

            foreach (KVNode node in nodes)
            {
                if (node == null) continue;
                node.KeyFontPath = PreparePathForExport(node.KeyFontPath, "Fonts");
                node.CountFontPath = PreparePathForExport(node.CountFontPath, "Fonts");
                node.ImagePath = PreparePathForExport(node.ImagePath, "Images");
                node.VideoPath = PreparePathForExport(node.VideoPath, "Videos");
            }
        }

        private static bool ImportNodeAssetPaths(List<KVNode> nodes)
        {
            if (nodes == null) return false;

            bool changed = false;
            foreach (KVNode node in nodes)
            {
                if (node == null) continue;
                changed |= ImportPath(ref node.KeyFontPath, "Fonts");
                changed |= ImportPath(ref node.CountFontPath, "Fonts");
                changed |= ImportPath(ref node.ImagePath, "Images");
                changed |= ImportPath(ref node.VideoPath, "Videos");
            }

            return changed;
        }

        private static bool ImportPath(ref string path, string category)
        {
            string imported = ImportExternalAsset(path, category);
            if (string.Equals(path ?? string.Empty, imported ?? string.Empty, StringComparison.Ordinal))
            {
                return false;
            }

            path = imported;
            return true;
        }

        private static string PreparePathForExport(string path, string category)
        {
            string normalized = NormalizeInputPath(path);
            if (string.IsNullOrEmpty(normalized)) return string.Empty;

            string resolved = ResolveAssetPath(normalized);
            if (string.IsNullOrEmpty(resolved) || !File.Exists(resolved)) return normalized;

            string relative = ToArchiveRelativeAssetPath(resolved);
            if (!string.IsNullOrEmpty(relative))
            {
                string canonicalAsset = FindExistingAssetByContent(resolved, category);
                return !string.IsNullOrEmpty(canonicalAsset) ? canonicalAsset : relative;
            }

            return ImportExternalAsset(resolved, category);
        }

        private static Settings CloneSettings(Settings settings)
        {
            return CloneByXml(settings);
        }

        private static T CloneByXml<T>(T value)
        {
            if (value == null) return default(T);

            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.Serialize(stream, value);
                stream.Position = 0;
                return (T)serializer.Deserialize(stream);
            }
        }

        private static string TryConvertArchiveRelativePath(string path)
        {
            string normalized = path.Replace('\\', '/');
            if (normalized.Equals(ArchiveAssetsPrefix, StringComparison.OrdinalIgnoreCase)) return string.Empty;

            string prefix = ArchiveAssetsPrefix + "/";
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return normalized.Substring(prefix.Length).Replace('/', Path.DirectorySeparatorChar);
            }

            return string.Empty;
        }

        private static string ToArchiveRelativeAssetPath(string fullPath)
        {
            string relative = GetRelativePathUnderRoot(fullPath, AssetsRoot);
            if (string.IsNullOrEmpty(relative)) return null;

            return ArchiveAssetsPrefix + "/" + relative.Replace('\\', '/');
        }

        private static string GetRelativePathUnderRoot(string path, string root)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root)) return null;

            string fullPath = Path.GetFullPath(path);
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string rootWithSlash = fullRoot + Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(rootWithSlash, StringComparison.OrdinalIgnoreCase)) return null;

            return fullPath.Substring(rootWithSlash.Length);
        }

        private static bool IsPathUnderRoot(string path, string root)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root)) return false;

            string fullPath = Path.GetFullPath(path);
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string rootWithSlash = fullRoot + Path.DirectorySeparatorChar;

            return string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(rootWithSlash, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetUniqueTargetPath(string targetDir, string fileName, string sourcePath)
        {
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            string targetPath = Path.Combine(targetDir, fileName);
            if (!File.Exists(targetPath) || SamePath(targetPath, sourcePath)) return targetPath;

            for (int i = 1; i < 1000; i++)
            {
                string candidate = Path.Combine(targetDir, baseName + "_" + i.ToString() + ext);
                if (!File.Exists(candidate) || SamePath(candidate, sourcePath)) return candidate;
            }

            return Path.Combine(targetDir, baseName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ext);
        }

        private static string FindExistingAssetByContent(string sourcePath, string category)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath)) return null;

            string safeCategory = SanitizePathSegment(category);
            if (string.IsNullOrEmpty(safeCategory)) safeCategory = "Misc";

            string targetDir = Path.Combine(AssetsRoot, safeCategory);
            if (!Directory.Exists(targetDir)) return null;

            FileInfo sourceInfo;
            try
            {
                sourceInfo = new FileInfo(sourcePath);
            }
            catch
            {
                return null;
            }

            string[] files = Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            string sourceHash = null;
            foreach (string file in files)
            {
                try
                {
                    FileInfo candidateInfo = new FileInfo(file);
                    if (!candidateInfo.Exists || candidateInfo.Length != sourceInfo.Length) continue;

                    if (sourceHash == null)
                    {
                        sourceHash = ComputeFileHash(sourcePath);
                        if (string.IsNullOrEmpty(sourceHash)) return null;
                    }

                    string candidateHash = ComputeFileHash(file);
                    if (string.Equals(sourceHash, candidateHash, StringComparison.OrdinalIgnoreCase))
                    {
                        return ToArchiveRelativeAssetPath(file) ?? file;
                    }
                }
                catch
                {
                    continue;
                }
            }

            return null;
        }

        private static string ComputeFileHash(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (SHA256 sha = SHA256.Create())
                {
                    return Convert.ToBase64String(sha.ComputeHash(stream));
                }
            }
            catch
            {
                return null;
            }
        }

        private static string NormalizeInputPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            return path.Trim().Trim('"');
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return string.Empty;

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }

            return fileName;
        }

        private static string SanitizePathSegment(string segment)
        {
            if (string.IsNullOrEmpty(segment)) return string.Empty;

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                segment = segment.Replace(c, '_');
            }

            return segment;
        }

        private static bool SamePath(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            return string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }
    }
}
