using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml.Serialization;
using UnityEngine;

namespace CheryTools
{
    [Serializable]
    public class KeyViewerPackage
    {
        public int FormatVersion = 1;
        public string ExportedAt = "";
        public bool EnableKeyViewer = true;
        public bool KeyViewerOnlyShowPlaying = false;
        public bool LimitInput = false;
        public bool KeyViewerHideCountText = false;
        public int KeyViewerSelectedConfigIndex = 0;
        public float KeyViewerScale = 1.0f;
        public float KeyViewerBorderThickness = 2.0f;
        public string KeyViewerFontPath = "";
        public float GlobalTextOffsetX = 0f;
        public float GlobalTextOffsetY = 0f;
        public float GlobalCountOffsetX = 0f;
        public float GlobalCountOffsetY = 0f;
        public float KeyViewerDefaultWidth = 50f;
        public float KeyViewerDefaultHeight = 50f;
        public float[] KeyViewerColorBgNormal = new float[] { 0.2f, 0.1f, 0.3f, 0.8f };
        public float[] KeyViewerColorBgPressed = new float[] { 0.5f, 0.2f, 0.8f, 1.0f };
        public float[] KeyViewerColorBorderNormal = new float[] { 0.6f, 0.3f, 0.9f, 0.8f };
        public float[] KeyViewerColorBorderPressed = new float[] { 0.8f, 0.4f, 1.0f, 1.0f };
        public float[] KeyViewerColorTextNormal = new float[] { 0.8f, 0.8f, 0.8f, 1.0f };
        public float[] KeyViewerColorTextPressed = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
        public float[] KeyViewerColorKps = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
        public float[] KeyViewerColorTotal = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
        public bool KeyViewerKeyTextOutlineEnabled = false;
        public float[] KeyViewerKeyTextOutlineColor = new float[] { 0f, 0f, 0f, 1f };
        public float KeyViewerKeyTextOutlineThickness = 1f;
        public bool KeyViewerCountTextOutlineEnabled = false;
        public float[] KeyViewerCountTextOutlineColor = new float[] { 0f, 0f, 0f, 1f };
        public float KeyViewerCountTextOutlineThickness = 1f;
        public bool EnableKeyRain = false;
        public float KeyRainSpeed = 800.0f;
        public float KeyRainMaxHeight = 400.0f;
        public int KeyRainFadeMode = 1;
        public float KeyRainWidthRatio1 = 0.8f;
        public float KeyRainWidthRatio2 = 0.4f;
        public float KeyRainYOffsetRow1 = 0.0f;
        public float KeyRainYOffsetRow2 = 0.0f;
        public float[] KeyRainColorRow1 = new float[] { 0.8f, 0.5f, 1.0f, 0.8f };
        public float[] KeyRainColorRow2 = new float[] { 0.5f, 0.8f, 1.0f, 0.8f };
        public List<KVConfiguration> KeyViewerConfigurations = new List<KVConfiguration>();
    }

    [Serializable]
    public class OverlayerPackage
    {
        public int FormatVersion = 1;
        public string ExportedAt = "";
        public bool OverlayerSystemEnabled = true;
        public bool OverlayerOnlyShowPlaying = false;
        public bool OverlayerEditMode = false;
        public List<OverlayerText> OverlayerTexts = new List<OverlayerText>();
        public List<OverlayerImage> OverlayerImages = new List<OverlayerImage>();
        public List<OverlayerProgressBar> OverlayerProgressBars = new List<OverlayerProgressBar>();
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

            string assetRelative = ToArchiveRelativeAssetPath(resolved);
            if (!string.IsNullOrEmpty(assetRelative)) return assetRelative;

            string safeCategory = SanitizePathSegment(category);
            if (string.IsNullOrEmpty(safeCategory)) safeCategory = "Misc";

            string targetDir = Path.Combine(AssetsRoot, safeCategory);
            Directory.CreateDirectory(targetDir);

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
            changed |= ImportPath(ref settings.KeyViewerFontPath, "Fonts");
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

            changed |= ImportNodeAssetPaths(settings.GetAllKeyViewerNodes());

            return changed;
        }

        public static string ExportCytPackage(Settings settings)
        {
            if (settings == null) throw new InvalidOperationException("Settings is null.");

            Directory.CreateDirectory(AssetsRoot);
            Settings exportSettings = CloneSettings(settings);
            RewriteAssetPathsForExport(exportSettings);

            string outputPath = Path.Combine(GameRoot, "CheryTools_Settings_Backup.cyt");
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

                if (Directory.Exists(AssetsRoot))
                {
                    foreach (string file in Directory.GetFiles(AssetsRoot, "*", SearchOption.AllDirectories))
                    {
                        string relative = GetRelativePathUnderRoot(file, AssetsRoot);
                        if (string.IsNullOrEmpty(relative)) continue;

                        string entryName = ArchiveAssetsPrefix + "/" + relative.Replace('\\', '/');
                        archive.CreateEntryFromFile(file, entryName, System.IO.Compression.CompressionLevel.Optimal);
                    }
                }
            }

            return outputPath;
        }

        public static string ExportKeyViewerPackage(Settings settings)
        {
            if (settings == null) throw new InvalidOperationException("Settings is null.");

            Directory.CreateDirectory(AssetsRoot);
            KeyViewerPackage package = CreateKeyViewerPackage(settings);
            RewriteKeyViewerPackageAssetPaths(package);

            string outputPath = Path.Combine(GameRoot, "CheryTools_KeyViewer.ctkv");
            WritePackage(outputPath, "KeyViewer.xml", package, CollectKeyViewerAssetPaths(package));
            return outputPath;
        }

        public static string ExportOverlayerPackage(Settings settings)
        {
            if (settings == null) throw new InvalidOperationException("Settings is null.");

            Directory.CreateDirectory(AssetsRoot);
            OverlayerPackage package = CreateOverlayerPackage(settings);
            RewriteOverlayerPackageAssetPaths(package);

            string outputPath = Path.Combine(GameRoot, "CheryTools_Overlayer.ctov");
            WritePackage(outputPath, "Overlayer.xml", package, CollectOverlayerAssetPaths(package));
            return outputPath;
        }

        public static void ImportKeyViewerPackage(Settings settings, string packagePath)
        {
            if (settings == null) throw new InvalidOperationException("Settings is null.");

            string normalizedPackagePath = NormalizeInputPath(packagePath);
            if (string.IsNullOrEmpty(normalizedPackagePath) || !File.Exists(normalizedPackagePath))
                throw new FileNotFoundException("KeyViewer package not found.", normalizedPackagePath);

            using (ZipArchive archive = ZipFile.OpenRead(normalizedPackagePath))
            {
                KeyViewerPackage package = ReadPackageManifest<KeyViewerPackage>(archive, "KeyViewer.xml");
                ExtractAssetsFromArchive(archive);
                ApplyKeyViewerPackage(settings, package);
            }
        }

        public static void ImportOverlayerPackage(Settings settings, string packagePath)
        {
            if (settings == null) throw new InvalidOperationException("Settings is null.");

            string normalizedPackagePath = NormalizeInputPath(packagePath);
            if (string.IsNullOrEmpty(normalizedPackagePath) || !File.Exists(normalizedPackagePath))
                throw new FileNotFoundException("Overlayer package not found.", normalizedPackagePath);

            using (ZipArchive archive = ZipFile.OpenRead(normalizedPackagePath))
            {
                OverlayerPackage package = ReadPackageManifest<OverlayerPackage>(archive, "Overlayer.xml");
                ExtractAssetsFromArchive(archive);
                ApplyOverlayerPackage(settings, package);
            }
        }

        private static KeyViewerPackage CreateKeyViewerPackage(Settings settings)
        {
            KeyViewerPackage package = new KeyViewerPackage();
            package.ExportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            package.EnableKeyViewer = settings.EnableKeyViewer;
            package.KeyViewerOnlyShowPlaying = settings.KeyViewerOnlyShowPlaying;
            package.LimitInput = settings.LimitInput;
            package.KeyViewerHideCountText = settings.KeyViewerHideCountText;
            package.KeyViewerSelectedConfigIndex = settings.KeyViewerSelectedConfigIndex;
            package.KeyViewerScale = settings.KeyViewerScale;
            package.KeyViewerBorderThickness = settings.KeyViewerBorderThickness;
            package.KeyViewerFontPath = settings.KeyViewerFontPath;
            package.GlobalTextOffsetX = settings.GlobalTextOffsetX;
            package.GlobalTextOffsetY = settings.GlobalTextOffsetY;
            package.GlobalCountOffsetX = settings.GlobalCountOffsetX;
            package.GlobalCountOffsetY = settings.GlobalCountOffsetY;
            package.KeyViewerDefaultWidth = settings.KeyViewerDefaultWidth;
            package.KeyViewerDefaultHeight = settings.KeyViewerDefaultHeight;
            package.KeyViewerColorBgNormal = CloneByXml(settings.KeyViewerColorBgNormal);
            package.KeyViewerColorBgPressed = CloneByXml(settings.KeyViewerColorBgPressed);
            package.KeyViewerColorBorderNormal = CloneByXml(settings.KeyViewerColorBorderNormal);
            package.KeyViewerColorBorderPressed = CloneByXml(settings.KeyViewerColorBorderPressed);
            package.KeyViewerColorTextNormal = CloneByXml(settings.KeyViewerColorTextNormal);
            package.KeyViewerColorTextPressed = CloneByXml(settings.KeyViewerColorTextPressed);
            package.KeyViewerColorKps = CloneByXml(settings.KeyViewerColorKps);
            package.KeyViewerColorTotal = CloneByXml(settings.KeyViewerColorTotal);
            package.KeyViewerKeyTextOutlineEnabled = settings.KeyViewerKeyTextOutlineEnabled;
            package.KeyViewerKeyTextOutlineColor = CloneByXml(settings.KeyViewerKeyTextOutlineColor);
            package.KeyViewerKeyTextOutlineThickness = settings.KeyViewerKeyTextOutlineThickness;
            package.KeyViewerCountTextOutlineEnabled = settings.KeyViewerCountTextOutlineEnabled;
            package.KeyViewerCountTextOutlineColor = CloneByXml(settings.KeyViewerCountTextOutlineColor);
            package.KeyViewerCountTextOutlineThickness = settings.KeyViewerCountTextOutlineThickness;
            package.EnableKeyRain = settings.EnableKeyRain;
            package.KeyRainSpeed = settings.KeyRainSpeed;
            package.KeyRainMaxHeight = settings.KeyRainMaxHeight;
            package.KeyRainFadeMode = settings.KeyRainFadeMode;
            package.KeyRainWidthRatio1 = settings.KeyRainWidthRatio1;
            package.KeyRainWidthRatio2 = settings.KeyRainWidthRatio2;
            package.KeyRainYOffsetRow1 = settings.KeyRainYOffsetRow1;
            package.KeyRainYOffsetRow2 = settings.KeyRainYOffsetRow2;
            package.KeyRainColorRow1 = CloneByXml(settings.KeyRainColorRow1);
            package.KeyRainColorRow2 = CloneByXml(settings.KeyRainColorRow2);
            package.KeyViewerConfigurations = CloneByXml(settings.KeyViewerConfigurations) ?? new List<KVConfiguration>();
            return package;
        }

        private static OverlayerPackage CreateOverlayerPackage(Settings settings)
        {
            OverlayerPackage package = new OverlayerPackage();
            package.ExportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            package.OverlayerSystemEnabled = settings.OverlayerSystemEnabled;
            package.OverlayerOnlyShowPlaying = settings.OverlayerOnlyShowPlaying;
            package.OverlayerEditMode = settings.OverlayerEditMode;
            package.OverlayerTexts = CloneByXml(settings.OverlayerTexts) ?? new List<OverlayerText>();
            package.OverlayerImages = CloneByXml(settings.OverlayerImages) ?? new List<OverlayerImage>();
            package.OverlayerProgressBars = CloneByXml(settings.OverlayerProgressBars) ?? new List<OverlayerProgressBar>();
            return package;
        }

        private static void RewriteKeyViewerPackageAssetPaths(KeyViewerPackage package)
        {
            package.KeyViewerFontPath = PreparePathForExport(package.KeyViewerFontPath, "Fonts");
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
        }

        private static List<string> CollectKeyViewerAssetPaths(KeyViewerPackage package)
        {
            List<string> paths = new List<string>();
            AddAssetPath(paths, package.KeyViewerFontPath);
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
            return paths;
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

            settings.EnableKeyViewer = package.EnableKeyViewer;
            settings.KeyViewerOnlyShowPlaying = package.KeyViewerOnlyShowPlaying;
            settings.LimitInput = package.LimitInput;
            settings.KeyViewerHideCountText = package.KeyViewerHideCountText;
            settings.KeyViewerSelectedConfigIndex = package.KeyViewerSelectedConfigIndex;
            settings.KeyViewerScale = package.KeyViewerScale;
            settings.KeyViewerBorderThickness = package.KeyViewerBorderThickness;
            settings.KeyViewerFontPath = package.KeyViewerFontPath ?? "";
            settings.GlobalTextOffsetX = package.GlobalTextOffsetX;
            settings.GlobalTextOffsetY = package.GlobalTextOffsetY;
            settings.GlobalCountOffsetX = package.GlobalCountOffsetX;
            settings.GlobalCountOffsetY = package.GlobalCountOffsetY;
            settings.KeyViewerDefaultWidth = package.KeyViewerDefaultWidth;
            settings.KeyViewerDefaultHeight = package.KeyViewerDefaultHeight;
            settings.KeyViewerColorBgNormal = package.KeyViewerColorBgNormal;
            settings.KeyViewerColorBgPressed = package.KeyViewerColorBgPressed;
            settings.KeyViewerColorBorderNormal = package.KeyViewerColorBorderNormal;
            settings.KeyViewerColorBorderPressed = package.KeyViewerColorBorderPressed;
            settings.KeyViewerColorTextNormal = package.KeyViewerColorTextNormal;
            settings.KeyViewerColorTextPressed = package.KeyViewerColorTextPressed;
            settings.KeyViewerColorKps = package.KeyViewerColorKps;
            settings.KeyViewerColorTotal = package.KeyViewerColorTotal;
            settings.KeyViewerKeyTextOutlineEnabled = package.KeyViewerKeyTextOutlineEnabled;
            settings.KeyViewerKeyTextOutlineColor = package.KeyViewerKeyTextOutlineColor;
            settings.KeyViewerKeyTextOutlineThickness = package.KeyViewerKeyTextOutlineThickness;
            settings.KeyViewerCountTextOutlineEnabled = package.KeyViewerCountTextOutlineEnabled;
            settings.KeyViewerCountTextOutlineColor = package.KeyViewerCountTextOutlineColor;
            settings.KeyViewerCountTextOutlineThickness = package.KeyViewerCountTextOutlineThickness;
            settings.EnableKeyRain = package.EnableKeyRain;
            settings.KeyRainSpeed = package.KeyRainSpeed;
            settings.KeyRainMaxHeight = package.KeyRainMaxHeight;
            settings.KeyRainFadeMode = package.KeyRainFadeMode;
            settings.KeyRainWidthRatio1 = package.KeyRainWidthRatio1;
            settings.KeyRainWidthRatio2 = package.KeyRainWidthRatio2;
            settings.KeyRainYOffsetRow1 = package.KeyRainYOffsetRow1;
            settings.KeyRainYOffsetRow2 = package.KeyRainYOffsetRow2;
            settings.KeyRainColorRow1 = package.KeyRainColorRow1;
            settings.KeyRainColorRow2 = package.KeyRainColorRow2;
            settings.KeyViewerConfigurations = package.KeyViewerConfigurations ?? new List<KVConfiguration>();
            settings.EnsureKeyViewerConfigurations();
        }

        private static void ApplyOverlayerPackage(Settings settings, OverlayerPackage package)
        {
            if (package == null)
                throw new InvalidDataException("Overlayer package manifest is empty.");

            settings.OverlayerSystemEnabled = package.OverlayerSystemEnabled;
            settings.OverlayerOnlyShowPlaying = package.OverlayerOnlyShowPlaying;
            settings.OverlayerEditMode = package.OverlayerEditMode;
            settings.OverlayerTexts = package.OverlayerTexts ?? new List<OverlayerText>();
            settings.OverlayerImages = package.OverlayerImages ?? new List<OverlayerImage>();
            settings.OverlayerProgressBars = package.OverlayerProgressBars ?? new List<OverlayerProgressBar>();
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
            settings.KeyViewerFontPath = PreparePathForExport(settings.KeyViewerFontPath, "Fonts");
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
            if (!string.IsNullOrEmpty(relative)) return relative;

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
