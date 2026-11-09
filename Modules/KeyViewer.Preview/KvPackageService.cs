using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Xml.Serialization;
using System.Reflection;
using UnityEngine;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.KeyViewer.Preview
{
    [Serializable]
    [XmlRoot("CheryToolsSonnetKeyViewer")]
    public sealed class KvTransferPackage
    {
        public int FormatVersion = 1;
        public string Kind = "profile";
        public string ExportedAt = string.Empty;
        public int ScreenWidth;
        public int ScreenHeight;
        public List<KvProfile> Profiles = new List<KvProfile>();
        public List<KvKey> Nodes = new List<KvKey>();
    }

    internal static class KvPackageService
    {
        private const string ManifestName = "KeyViewer.Sonnet.xml";
        private const string LegacyManifestName = "KeyViewer.xml";
        internal static string LastStatus { get; private set; } = string.Empty;

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

        private static void FilterStatsIfNeeded(KvTransferPackage package)
        {
            if (package == null) return;
            bool includeStats = KvRuntime.Settings?.PackageIncludeStats ?? true;
            if (includeStats) return;

            if (package.Profiles != null)
            {
                foreach (KvProfile profile in package.Profiles)
                {
                    if (profile == null) continue;
                    profile.TotalHits = 0;
                    if (profile.Keys != null)
                    {
                        foreach (KvKey key in profile.Keys)
                        {
                            if (key != null) key.HitCount = 0;
                        }
                    }
                }
            }

            if (package.Nodes != null)
            {
                foreach (KvKey node in package.Nodes)
                {
                    if (node != null) node.HitCount = 0;
                }
            }
        }

        internal static void ExportProfile(KvProfile profile)
        {
            if (profile == null) return;
            try
            {
                string path = ModernFileDialog.ShowSaveFileDialog(L("导出 KV 配置", "Export KV profile"), L("CheryTools KV 配置 (*.ctkv)|*.ctkv", "CheryTools KV profile (*.ctkv)|*.ctkv"),
                    GameRoot(), SafeName(profile.Name) + ".ctkv");
                if (string.IsNullOrEmpty(path)) { LastStatus = L("已取消导出", "Export canceled"); return; }
                var package = CreatePackage("profile");
                package.Profiles.Add(KvGeometry.CloneProfile(profile));
                FilterStatsIfNeeded(package);
                path = Write(path, package);
                LastStatus = L("已导出配置：", "Exported profile: ") + path;
            }
            catch (Exception ex)
            {
                LastStatus = L("导出失败：", "Export failed: ") + ex.Message;
                KvRuntime.Logger?.Warning(L("KV 配置导出失败：", "KV profile export failed: ") + ex);
            }
        }

        internal static void ExportAll(KvSettings settings)
        {
            if (settings == null || settings.Profiles == null) return;
            try
            {
                string path = ModernFileDialog.ShowSaveFileDialog(L("导出全部 KV 配置", "Export all KV profiles"), L("CheryTools KV 配置 (*.ctkv)|*.ctkv", "CheryTools KV profile (*.ctkv)|*.ctkv"),
                    GameRoot(), "KeyViewer_All.ctkv");
                if (string.IsNullOrEmpty(path)) { LastStatus = L("已取消导出", "Export canceled"); return; }
                var package = CreatePackage("all");
                foreach (KvProfile profile in settings.Profiles)
                    if (profile != null) package.Profiles.Add(KvGeometry.CloneProfile(profile));
                FilterStatsIfNeeded(package);
                path = Write(path, package);
                LastStatus = L("已导出全部配置：", "Exported all profiles: ") + path;
            }
            catch (Exception ex)
            {
                LastStatus = L("导出失败：", "Export failed: ") + ex.Message;
                KvRuntime.Logger?.Warning(L("KV 全部配置导出失败：", "Exporting all KV profiles failed: ") + ex);
            }
        }

        internal static void ExportNode(KvKey node)
        {
            if (node == null) return;
            try
            {
                string name = node.NodeType == 1 ? "KPS" : node.NodeType == 2 ? "Total" : KvSettings.KeyName(node.Bind);
                string path = ModernFileDialog.ShowSaveFileDialog(L("导出 KV 组件", "Export KV nodes"), L("CheryTools KV 组件 (*.ctkv)|*.ctkv", "CheryTools KV nodes (*.ctkv)|*.ctkv"),
                    GameRoot(), SafeName(name) + ".ctkv");
                if (string.IsNullOrEmpty(path)) { LastStatus = L("已取消导出", "Export canceled"); return; }
                var package = CreatePackage("node");
                package.Nodes.Add(KvGeometry.CloneNode(node));
                FilterStatsIfNeeded(package);
                path = Write(path, package);
                LastStatus = L("已导出组件：", "Exported node: ") + path;
            }
            catch (Exception ex)
            {
                LastStatus = L("导出失败：", "Export failed: ") + ex.Message;
                KvRuntime.Logger?.Warning(L("KV 组件导出失败：", "KV node export failed: ") + ex);
            }
        }

        internal static void ExportNodes(IEnumerable<KvKey> nodes)
        {
            if (nodes == null) return;
            try
            {
                var package = CreatePackage("nodes");
                foreach (KvKey node in nodes) if (node != null) package.Nodes.Add(KvGeometry.CloneNode(node));
                if (package.Nodes.Count == 0) return;
                FilterStatsIfNeeded(package);
                string path = ModernFileDialog.ShowSaveFileDialog(L("导出 KV 组件", "Export KV nodes"), L("CheryTools KV 组件 (*.ctkv)|*.ctkv", "CheryTools KV nodes (*.ctkv)|*.ctkv"),
                    GameRoot(), package.Nodes.Count == 1 ? "KeyViewer_Node.ctkv" : "KeyViewer_" + package.Nodes.Count + "_Nodes.ctkv");
                if (string.IsNullOrEmpty(path)) { LastStatus = L("已取消导出", "Export canceled"); return; }
                path = Write(path, package);
                LastStatus = L("已导出 ", "Exported ") + package.Nodes.Count + L(" 个组件：", " nodes: ") + path;
            }
            catch (Exception ex)
            {
                LastStatus = L("导出失败：", "Export failed: ") + ex.Message;
                KvRuntime.Logger?.Warning(L("KV 组件导出失败：", "KV node export failed: ") + ex);
            }
        }

        internal static void Import(KvSettings settings, KvProfile targetProfile)
        {
            if (settings == null) return;
            try
            {
                string path = ModernFileDialog.ShowOpenFileDialog(L("导入 KV 配置或组件", "Import KV profile or nodes"),
                    L("支持的全部配置 (*.ctkv;*.cyt;*.xml;*.json)|*.ctkv;*.cyt;*.xml;*.json|CheryTools KV 配置 (*.ctkv)|*.ctkv|Keyviewer4 布局配置 (*.json)|*.json|CheryTools 完整配置包 (*.cyt)|*.cyt|XML 配置文件 (*.xml)|*.xml|所有文件 (*.*)|*.*", "All supported (*.ctkv;*.cyt;*.xml;*.json)|*.ctkv;*.cyt;*.xml;*.json|CheryTools KV profile (*.ctkv)|*.ctkv|KeyViewer4 layout (*.json)|*.json|CheryTools full package (*.cyt)|*.cyt|XML settings (*.xml)|*.xml|All files (*.*)|*.*"),
                    GameRoot());
                if (string.IsNullOrEmpty(path)) { LastStatus = L("已取消导入", "Import canceled"); return; }
                ImportFromFile(settings, path, targetProfile, false);
            }
            catch (Exception ex)
            {
                LastStatus = L("导入失败：", "Import failed: ") + ex.Message;
                KvRuntime.Logger?.Warning(L("KV 导入失败：", "KV import failed: ") + ex);
            }
        }

        internal static bool ImportFromFile(KvSettings settings, string path, KvProfile targetProfile = null, bool clearExisting = false)
        {
            if (settings == null || string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            try
            {
                bool legacy;
                KvTransferPackage package = Read(path, out legacy);
                if (package.FormatVersion > 1) throw new InvalidDataException(L("配置版本高于当前模块支持的版本。", "The profile was made by a newer version than this module supports."));
                FilterStatsIfNeeded(package);
                float scaleX = package.ScreenWidth > 0 ? SafeScreenWidth() / (float)package.ScreenWidth : 1f;
                float scaleY = package.ScreenHeight > 0 ? SafeScreenHeight() / (float)package.ScreenHeight : 1f;
                if (legacy)
                {
                    float uniform = Mathf.Min(scaleX, scaleY);
                    scaleX = scaleY = uniform;
                }
                if (clearExisting && settings.Profiles != null)
                {
                    settings.Profiles.Clear();
                }
                int importedProfiles = 0, importedNodes = 0;
                if (package.Profiles != null)
                {
                    foreach (KvProfile source in package.Profiles)
                    {
                        if (source == null) continue;
                        KvProfile profile = KvGeometry.CloneProfile(source);
                        if (!clearExisting)
                        {
                            profile.Name = UniqueName(settings, profile.Name);
                        }
                        AdaptProfile(profile, scaleX, scaleY);
                        settings.Profiles.Add(profile);
                        importedProfiles++;
                    }
                }
                if (package.Nodes != null && package.Nodes.Count > 0)
                {
                    if (targetProfile == null) targetProfile = settings.Profiles.Count > 0 ? settings.Profiles[0] : null;
                    if (targetProfile == null)
                    {
                        targetProfile = KvSettings.CreateProfile(L("导入配置", "Imported profile"));
                        targetProfile.Keys.Clear();
                        settings.Profiles.Add(targetProfile);
                    }
                    foreach (KvKey source in package.Nodes)
                    {
                        if (source == null) continue;
                        KvKey node = KvGeometry.CloneNode(source);
                        AdaptNode(node, scaleX, scaleY);
                        node.PositionX += 20f;
                        node.PositionY += 20f;
                        targetProfile.Keys.Add(node);
                        importedNodes++;
                    }
                }
                settings.Normalize();
                KvRuntime.Changed();
                KvRuntime.InvalidateVisuals();
                LastStatus = (legacy ? L("旧版 CT 配置转换完成：", "Converted old CT profile: ") : L("导入完成：", "Import complete: "))
                    + (clearExisting ? L("载入 ", "loaded ") : L("追加 ", "added ")) + importedProfiles + L(" 个配置，", " profiles, ") + importedNodes + L(" 个组件", " nodes");
                return true;
            }
            catch (Exception ex)
            {
                LastStatus = L("导入失败：", "Import failed: ") + ex.Message;
                KvRuntime.Logger?.Warning(L("KV 导入失败：", "KV import failed: ") + ex);
                return false;
            }
        }

        private static KvTransferPackage CreatePackage(string kind)
        {
            return new KvTransferPackage
            {
                Kind = kind,
                ExportedAt = DateTime.Now.ToString("O"),
                ScreenWidth = SafeScreenWidth(),
                ScreenHeight = SafeScreenHeight()
            };
        }

        private sealed class PackageAsset
        {
            internal string Source;
            internal string Entry;
        }

        private static string Write(string path, KvTransferPackage package)
        {
            if (!path.EndsWith(".ctkv", StringComparison.OrdinalIgnoreCase)) path += ".ctkv";
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var assets = new List<PackageAsset>();
            PreparePackageAssets(package, assets);
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry(ManifestName, System.IO.Compression.CompressionLevel.Optimal);
                using (Stream entryStream = entry.Open()) new XmlSerializer(typeof(KvTransferPackage)).Serialize(entryStream, package);
                foreach (PackageAsset asset in assets)
                    archive.CreateEntryFromFile(asset.Source, asset.Entry, System.IO.Compression.CompressionLevel.Optimal);
            }
            return path;
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

        private static bool IsJsonFile(string path)
        {
            try
            {
                using (var reader = new StreamReader(path))
                {
                    int ch;
                    while ((ch = reader.Read()) != -1)
                    {
                        if (char.IsWhiteSpace((char)ch)) continue;
                        return ch == '{';
                    }
                }
            }
            catch { }
            return false;
        }

        private static KvTransferPackage Read(string path, out bool legacy)
        {
            legacy = false;
            if (!File.Exists(path))
                throw new FileNotFoundException(L("找不到配置文件。", "Profile file not found."), path);

            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || (!IsZipArchive(path) && IsJsonFile(path)))
            {
                string jsonText = File.ReadAllText(path);
                var root = Newtonsoft.Json.Linq.JObject.Parse(jsonText);
                if (KeyViewer4ProfileImporter.CanImport(root))
                {
                    string warning;
                    KvProfile imported = KeyViewer4ProfileImporter.ImportToSonnet(root, Path.GetFileNameWithoutExtension(path), out warning);
                    var pkg = new KvTransferPackage
                    {
                        FormatVersion = 1,
                        Kind = "profile",
                        ExportedAt = DateTime.Now.ToString("O"),
                        ScreenWidth = SafeScreenWidth(),
                        ScreenHeight = SafeScreenHeight(),
                        Profiles = new List<KvProfile> { imported }
                    };
                    legacy = false;
                    return pkg;
                }
                throw new InvalidDataException(L("不是可识别的 Keyviewer4 布局文件（缺少 Keys 配置列表）。", "Not a recognizable KeyViewer4 layout file (no Keys list)."));
            }

            if (IsZipArchive(path))
            {
                using (ZipArchive archive = ZipFile.OpenRead(path))
                {
                    ZipArchiveEntry entry = archive.GetEntry(ManifestName);
                    if (entry != null)
                    {
                        KvTransferPackage result;
                        using (Stream stream = entry.Open())
                            result = new XmlSerializer(typeof(KvTransferPackage)).Deserialize(stream) as KvTransferPackage
                                ?? throw new InvalidDataException(L("配置内容为空。", "The profile is empty."));
                        ExtractPackageAssets(archive, result, path);
                        return result;
                    }

                    entry = archive.GetEntry(LegacyManifestName);
                    if (entry != null)
                    {
                        LegacyKvPackage source;
                        using (Stream stream = entry.Open())
                            source = new XmlSerializer(typeof(LegacyKvPackage)).Deserialize(stream) as LegacyKvPackage
                                ?? throw new InvalidDataException(L("旧版配置内容为空。", "The old profile is empty."));
                        legacy = true;
                        KvTransferPackage converted = ConvertLegacy(source);
                        ExtractPackageAssets(archive, converted, path);
                        return converted;
                    }

                    entry = archive.GetEntry("Settings.xml");
                    if (entry != null)
                    {
                        legacy = true;
                        int sourceWidth = 0;
                        int sourceHeight = 0;
                        ZipArchiveEntry infoEntry = archive.GetEntry("PackageInfo.xml");
                        if (infoEntry != null)
                        {
                            try
                            {
                                using (Stream infoStream = infoEntry.Open())
                                {
                                    var info = new XmlSerializer(typeof(LegacyCytPackageInfo)).Deserialize(infoStream) as LegacyCytPackageInfo;
                                    if (info != null)
                                    {
                                        sourceWidth = info.ExportScreenWidth;
                                        sourceHeight = info.ExportScreenHeight;
                                    }
                                }
                            }
                            catch { }
                        }

                        LegacyCytSettingsKvPackage cytSettings;
                        using (Stream stream = entry.Open())
                        {
                            cytSettings = new XmlSerializer(typeof(LegacyCytSettingsKvPackage)).Deserialize(stream) as LegacyCytSettingsKvPackage
                                ?? throw new InvalidDataException(L("Settings.xml 内容为空。", "Settings.xml is empty."));
                        }

                        if (sourceWidth <= 0 || sourceHeight <= 0)
                        {
                            sourceWidth = cytSettings.LastKnownScreenWidth > 0 ? cytSettings.LastKnownScreenWidth : SafeScreenWidth();
                            sourceHeight = cytSettings.LastKnownScreenHeight > 0 ? cytSettings.LastKnownScreenHeight : SafeScreenHeight();
                        }

                        LegacyKvPackage legacyPackage = BuildLegacyPackageFromCytSettings(cytSettings, sourceWidth, sourceHeight);
                        KvTransferPackage converted = ConvertLegacy(legacyPackage);
                        ExtractPackageAssets(archive, converted, path);
                        return converted;
                    }

                    throw new InvalidDataException(L("这不是可识别的 KV 配置，或配置文件已经损坏。", "This is not a recognizable KV profile, or the file is damaged."));
                }
            }
            else
            {
                string rootName = GetXmlRootElementName(path);
                if (string.Equals(rootName, "CheryToolsSonnetKeyViewer", StringComparison.OrdinalIgnoreCase))
                {
                    using (var stream = File.OpenRead(path))
                    {
                        var result = new XmlSerializer(typeof(KvTransferPackage)).Deserialize(stream) as KvTransferPackage
                            ?? throw new InvalidDataException(L("配置内容为空。", "The profile is empty."));
                        ResolveLocalAssetPathsForKvPackage(result);
                        return result;
                    }
                }
                else if (string.Equals(rootName, "KeyViewerPackage", StringComparison.OrdinalIgnoreCase))
                {
                    legacy = true;
                    using (var stream = File.OpenRead(path))
                    {
                        var source = new XmlSerializer(typeof(LegacyKvPackage)).Deserialize(stream) as LegacyKvPackage
                            ?? throw new InvalidDataException(L("旧版配置内容为空。", "The old profile is empty."));
                        var converted = ConvertLegacy(source);
                        ResolveLocalAssetPathsForKvPackage(converted);
                        return converted;
                    }
                }
                else if (string.Equals(rootName, "Settings", StringComparison.OrdinalIgnoreCase))
                {
                    legacy = true;
                    using (var stream = File.OpenRead(path))
                    {
                        var cytSettings = new XmlSerializer(typeof(LegacyCytSettingsKvPackage)).Deserialize(stream) as LegacyCytSettingsKvPackage
                            ?? throw new InvalidDataException(L("Settings 内容为空。", "Settings is empty."));
                        int width = cytSettings.LastKnownScreenWidth > 0 ? cytSettings.LastKnownScreenWidth : SafeScreenWidth();
                        int height = cytSettings.LastKnownScreenHeight > 0 ? cytSettings.LastKnownScreenHeight : SafeScreenHeight();
                        var legacyPackage = BuildLegacyPackageFromCytSettings(cytSettings, width, height);
                        var converted = ConvertLegacy(legacyPackage);
                        ResolveLocalAssetPathsForKvPackage(converted);
                        return converted;
                    }
                }
                else
                {
                    throw new InvalidDataException(L($"无法识别的 XML 格式：根节点为 <{rootName}>。", $"Unrecognized XML format: root element <{rootName}>."));
                }
            }
        }

        private static LegacyKvPackage BuildLegacyPackageFromCytSettings(LegacyCytSettingsKvPackage cytSettings, int width, int height)
        {
            var package = new LegacyKvPackage
            {
                FormatVersion = 1,
                ExportScreenWidth = width,
                ExportScreenHeight = height,
                KeyViewerConfigurations = new List<LegacyKvProfile>()
            };

            if (cytSettings.KeyViewerConfigurations != null && cytSettings.KeyViewerConfigurations.Count > 0)
            {
                package.KeyViewerConfigurations.AddRange(cytSettings.KeyViewerConfigurations);
            }
            else
            {
                var layouts = new List<KeyValuePair<string, List<LegacyKvNode>>>();
                if (cytSettings.Layout16K != null && cytSettings.Layout16K.Count > 0)
                    layouts.Add(new KeyValuePair<string, List<LegacyKvNode>>("16K", cytSettings.Layout16K));
                if (cytSettings.Layout12K != null && cytSettings.Layout12K.Count > 0)
                    layouts.Add(new KeyValuePair<string, List<LegacyKvNode>>("12K", cytSettings.Layout12K));
                if (cytSettings.Layout10K != null && cytSettings.Layout10K.Count > 0)
                    layouts.Add(new KeyValuePair<string, List<LegacyKvNode>>("10K", cytSettings.Layout10K));
                if (cytSettings.Layout8K != null && cytSettings.Layout8K.Count > 0)
                    layouts.Add(new KeyValuePair<string, List<LegacyKvNode>>("8K", cytSettings.Layout8K));

                int selectedTab = cytSettings.KeyViewerLayoutTab;
                for (int i = 0; i < layouts.Count; i++)
                {
                    var layout = layouts[i];
                    var profile = new LegacyKvProfile
                    {
                        Name = L("旧 ", "Old ") + layout.Key,
                        IsEnabled = (i == selectedTab) || (i == 0 && selectedTab >= layouts.Count),
                        Nodes = layout.Value ?? new List<LegacyKvNode>()
                    };
                    package.KeyViewerConfigurations.Add(profile);
                }
            }

            if (package.KeyViewerConfigurations.Count == 0)
            {
                throw new InvalidDataException(L("配置中没有找到有效的 KeyViewer 按键配置（KeyViewerConfigurations 或 Layout16K~8K）。", "No valid KeyViewer key layout found in the settings (KeyViewerConfigurations or Layout16K-8K)."));
            }

            return package;
        }

        private static void ResolveLocalAssetPathsForKvPackage(KvTransferPackage package)
        {
            if (package == null) return;
            if (package.Profiles != null)
            {
                foreach (var profile in package.Profiles)
                {
                    if (profile == null) continue;
                    profile.FontPath = ResolveExistingAsset(profile.FontPath) ?? profile.FontPath;
                    if (profile.Keys != null)
                    {
                        foreach (var node in profile.Keys)
                        {
                            if (node == null) continue;
                            node.ImagePath = ResolveExistingAsset(node.ImagePath) ?? node.ImagePath;
                            node.VideoPath = ResolveExistingAsset(node.VideoPath) ?? node.VideoPath;
                            node.LabelFontPath = ResolveExistingAsset(node.LabelFontPath) ?? node.LabelFontPath;
                            node.CountFontPath = ResolveExistingAsset(node.CountFontPath) ?? node.CountFontPath;
                        }
                    }
                }
            }
            if (package.Nodes != null)
            {
                foreach (var node in package.Nodes)
                {
                    if (node == null) continue;
                    node.ImagePath = ResolveExistingAsset(node.ImagePath) ?? node.ImagePath;
                    node.VideoPath = ResolveExistingAsset(node.VideoPath) ?? node.VideoPath;
                    node.LabelFontPath = ResolveExistingAsset(node.LabelFontPath) ?? node.LabelFontPath;
                    node.CountFontPath = ResolveExistingAsset(node.CountFontPath) ?? node.CountFontPath;
                }
            }
        }

        private static KvTransferPackage ConvertLegacy(LegacyKvPackage source)
        {
            var package = new KvTransferPackage
            {
                Kind = "profile",
                ExportedAt = DateTime.Now.ToString("O"),
                ScreenWidth = source.ExportScreenWidth,
                ScreenHeight = source.ExportScreenHeight
            };
            if (source.KeyViewerConfigurations == null) return package;
            foreach (LegacyKvProfile legacy in source.KeyViewerConfigurations)
            {
                if (legacy == null) continue;
                KvProfile profile = ConvertLegacyProfile(legacy);
                if (profile.Keys.Count > 0) package.Profiles.Add(profile);
            }
            if (package.Profiles.Count == 0)
                throw new InvalidDataException(L("旧版配置中没有可转换的按键、KPS 或总计数组件。", "The old profile has no keys, KPS or Total nodes to convert."));
            return package;
        }

        private static KvProfile ConvertLegacyProfile(LegacyKvProfile source)
        {
            var profile = new KvProfile
            {
                Name = string.IsNullOrWhiteSpace(source.Name) ? L("导入配置", "Imported profile") : source.Name,
                Enabled = source.IsEnabled,
                ShowInGame = source.ShowInGame,
                OnlyShowPlaying = source.OnlyShowPlaying,
                LayoutVersion = 1,
                Scale = Positive(source.Scale, 1f),
                // 旧版未单独配置节点圆角时使用 6px 基准圆角。
                CornerRadius = 6f,
                BorderThickness = NonNegative(source.BorderThickness, 2f),
                HideCount = source.HideCountText,
                BackgroundNormal = Color(source.ColorBgNormal, 0.075f, 0.085f, 0.11f, 0.88f),
                BackgroundPressed = Color(source.ColorBgPressed, 0.30f, 0.76f, 1f, 0.96f),
                BorderNormal = Color(source.ColorBorderNormal, 0.42f, 0.48f, 0.58f, 0.70f),
                BorderPressed = Color(source.ColorBorderPressed, 0.72f, 0.92f, 1f, 1f),
                TextNormal = Color(source.ColorTextNormal, 0.94f, 0.96f, 1f, 1f),
                TextPressed = Color(source.ColorTextPressed, 0.015f, 0.025f, 0.04f, 1f),
                RainEnabled = source.EnableKeyRain,
                RainSpeed = Positive(source.KeyRainSpeed, 620f),
                RainMaxHeight = Positive(source.KeyRainMaxHeight, 420f),
                RainWidthRatio = Positive(source.KeyRainWidthRatio1, 0.72f),
                RainWidthRatio2 = Positive(source.KeyRainWidthRatio2, 0.55f),
                RainYOffset = source.KeyRainYOffsetRow1,
                RainYOffset2 = source.KeyRainYOffsetRow2,
                RainCornerRadius = NonNegative(source.KeyRainCornerRadius, 7f),
                RainColor = Color(source.KeyRainColorRow1, 0.30f, 0.76f, 1f, 0.74f),
                RainColor2 = Color(source.KeyRainColorRow2, 0.50f, 0.80f, 1f, 0.74f),
                RainEndColor = Color(source.KeyRainGradientEndColorRow1, 0.48f, 0.34f, 1f, 0.08f),
                RainEndColor2 = Color(source.KeyRainGradientEndColorRow2, 0.25f, 1f, 0.80f, 0.08f),
                RainRightColor = Color(source.KeyRainHorizontalGradientEndColorRow1, 0.45f, 0.75f, 1f, 0.74f),
                RainRightColor2 = Color(source.KeyRainHorizontalGradientEndColorRow2, 1f, 0.65f, 0.35f, 0.74f),
                RainGradientEnabled = source.KeyRainGradientEnabled,
                RainGradientMode = source.KeyRainGradientMode,
                RainGradientHeight = Positive(source.KeyRainGradientHeight, 1f),
                RainGradientPower = Positive(source.KeyRainGradientPower, 1f),
                RainHorizontalGradientEnabled = source.KeyRainHorizontalGradientEnabled,
                RainFadeMode = source.KeyRainFadeMode,
                RainFadeHeight = Positive(source.KeyRainFadeHeight, 0.22f),
                RainFadePower = Positive(source.KeyRainFadePower, 1f),
                RainOutlineEnabled = source.KeyRainOutlineEnabled,
                RainOutlineColor = Color(source.KeyRainOutlineColor, 0f, 0f, 0f, 1f),
                RainOutlineThickness = NonNegative(source.KeyRainOutlineThickness, 2f),
                RainShadowEnabled = source.KeyRainShadowEnabled,
                RainShadowColor = Color(source.KeyRainShadowColor, 0f, 0f, 0f, 0.35f),
                RainShadowOffset = Pair(source.KeyRainShadowOffset, 0f, 0f, true),
                RainShadowSoftness = NonNegative(source.KeyRainShadowSoftness, 12f),
                RainShadowStrength = Mathf.Clamp01(source.KeyRainShadowStrength),
                FontPath = source.FontPath ?? string.Empty,
                LabelOutlineEnabled = source.KeyTextOutlineEnabled,
                LabelOutlineColor = Color(source.KeyTextOutlineColor, 0f, 0f, 0f, 1f),
                LabelOutlineThickness = NonNegative(source.KeyTextOutlineThickness, 1f),
                CountOutlineEnabled = source.CountTextOutlineEnabled,
                CountOutlineColor = Color(source.CountTextOutlineColor, 0f, 0f, 0f, 1f),
                CountOutlineThickness = NonNegative(source.CountTextOutlineThickness, 1f),
                LabelShadowEnabled = source.KeyTextShadowEnabled,
                LabelShadowColor = Color(source.KeyTextShadowColor, 0f, 0f, 0f, 0.7f),
                LabelShadowOffset = Pair(source.KeyTextShadowOffset, 2f, -2f, true),
                CountShadowEnabled = source.CountTextShadowEnabled,
                CountShadowColor = Color(source.CountTextShadowColor, 0f, 0f, 0f, 0.7f),
                CountShadowOffset = Pair(source.CountTextShadowOffset, 2f, -2f, true),
                PressAnimationEnabled = source.KeyPressAnimationEnabled,
                PressAnimationDuration = Positive(source.KeyPressAnimationDuration, 0.12f),
                PressAnimationEasing = source.KeyPressAnimationEasing,
                PressAnimationAffectColors = source.KeyPressAnimationAffectColors,
                PressAnimationScale = Positive(source.KeyPressAnimationScale, 1f),
                PressAnimationOffsetX = source.KeyPressAnimationOffsetX,
                PressAnimationOffsetY = -source.KeyPressAnimationOffsetY
            };
            profile.Keys.Clear();
            if (source.Nodes == null) return profile;
            foreach (LegacyKvNode legacyNode in source.Nodes)
            {
                KvKey node = ConvertLegacyNode(profile, source, legacyNode);
                if (node != null) profile.Keys.Add(node);
            }
            return profile;
        }

        private static KvKey ConvertLegacyNode(KvProfile profile, LegacyKvProfile owner, LegacyKvNode source)
        {
            if (source == null || source.NodeType < 0 || source.NodeType > 4) return null;
            KeyCode bind;
            if (!Enum.TryParse(source.KeyBind ?? string.Empty, true, out bind)) bind = KeyCode.None;
            float nodeScale = Positive(source.Scale, 1f);
            float convertedWidth = Positive(source.Width, profile.KeyWidth) * nodeScale;
            float convertedHeight = Positive(source.Height, profile.KeyHeight) * nodeScale;
            var node = new KvKey
            {
                NodeType = source.NodeType,
                Bind = source.NodeType == 0 || source.NodeType == 3 || source.NodeType == 4 ? bind : KeyCode.None,
                Label = !string.IsNullOrEmpty(source.CustomText)
                    ? source.CustomText
                    : source.NodeType == 1 ? "KPS" : source.NodeType == 2 ? "TOTAL"
                    : bind == KeyCode.None ? string.Empty : KvSettings.KeyName(bind),
                ImagePath = source.ImagePath ?? string.Empty,
                VideoPath = source.VideoPath ?? string.Empty,
                VideoLoop = source.VideoLoop,
                MediaScale = Positive(source.VideoContentScale, 1f),
                MediaOffsetX = source.VideoContentOffsetX,
                MediaOffsetY = -source.VideoContentOffsetY,
                Opacity = Mathf.Clamp01(source.Opacity),
                Depth = source.Depth,
                Locked = source.IsUnselectable,
                // 旧版坐标表示节点左上角，且 Y 轴向下；Sonnet/UGUI 使用节点中心且 Y 轴向上。
                PositionX = source.PositionX + convertedWidth * 0.5f,
                PositionY = -(source.PositionY + convertedHeight * 0.5f),
                Width = convertedWidth,
                Height = convertedHeight,
                RainEnabled = source.EnableKeyRain,
                HideCount = source.HideCountText,
                CornerRadius = source.CornerRadius < 0f ? -1f : source.CornerRadius * nodeScale,
                BorderThickness = source.BorderThickness < 0f ? -1f : source.BorderThickness * nodeScale,
                // 旧版字号只受配置整体缩放和各自的 TextScale/CountScale 影响，
                // 不受节点 Scale 影响。这里保存的是应用配置缩放前的字号。
                LabelSize = 20f * Positive(source.TextScale, 1f),
                CountSize = 20f * Positive(source.CountScale, 1f),
                LabelFontPath = source.KeyFontPath ?? string.Empty,
                CountFontPath = source.CountFontPath ?? string.Empty,
                LabelOffsetX = source.TextOffsetX * nodeScale,
                LabelOffsetY = -source.TextOffsetY * nodeScale,
                CountOffsetX = source.CountOffsetX * nodeScale,
                CountOffsetY = -source.CountOffsetY * nodeScale,
                CountAlignment = source.CountTextAlignment,
                // 很早的配置没有持久化 RainRow；旧版会按节点所在行迁移。
                RainRow = source.RainRow != 0 ? source.RainRow : (source.PositionY < 80f ? 1 : 2),
                UseCustomTextEffects = source.UseCustomOutline || source.UseCustomShadow,
                LabelOutlineEnabled = source.KeyTextOutlineEnabled,
                LabelOutlineColor = Color(source.KeyTextOutlineColor, 0f, 0f, 0f, 1f),
                LabelOutlineThickness = NonNegative(source.KeyTextOutlineThickness, 1f),
                CountOutlineEnabled = source.CountTextOutlineEnabled,
                CountOutlineColor = Color(source.CountTextOutlineColor, 0f, 0f, 0f, 1f),
                CountOutlineThickness = NonNegative(source.CountTextOutlineThickness, 1f),
                LabelShadowEnabled = source.KeyTextShadowEnabled,
                LabelShadowColor = Color(source.KeyTextShadowColor, 0f, 0f, 0f, 0.7f),
                LabelShadowOffset = Pair(source.KeyTextShadowOffset, 2f, -2f, true),
                CountShadowEnabled = source.CountTextShadowEnabled,
                CountShadowColor = Color(source.CountTextShadowColor, 0f, 0f, 0f, 0.7f),
                CountShadowOffset = Pair(source.CountTextShadowOffset, 2f, -2f, true),
                UseCustomRain = source.UseCustomRain,
                RainWidthRatio = Positive(source.RainWidthRatio, 0.72f),
                // 旧版键雨偏移在绘制时从按键起点减去；UGUI 的正 Y 本身向上，
                // 因而这里保持数值符号即可得到相同方向。
                RainYOffset = source.RainYOffset,
                RainCornerRadius = NonNegative(source.RainCornerRadius, 7f),
                RainColor = Color(source.RainColor, 0.30f, 0.76f, 1f, 0.74f),
                RainEndColor = Color(source.RainGradientEndColor, 0.48f, 0.34f, 1f, 0.08f),
                RainRightColor = Color(source.RainHorizontalGradientEndColor, 0.45f, 0.75f, 1f, 0.74f),
                RainGradientEnabled = source.RainGradientEnabled,
                RainGradientMode = source.RainGradientMode,
                RainGradientHeight = Positive(source.RainGradientHeight, 1f),
                RainGradientPower = Positive(source.RainGradientPower, 1f),
                RainHorizontalGradientEnabled = source.RainHorizontalGradientEnabled,
                RainFadeMode = source.RainFadeMode,
                RainFadeHeight = Positive(source.RainFadeHeight, 0.22f),
                RainFadePower = Positive(source.RainFadePower, 1f),
                RainOutlineEnabled = source.UseCustomRainOutline && source.RainOutlineEnabled,
                RainOutlineColor = Color(source.RainOutlineColor, 0f, 0f, 0f, 0.85f),
                RainOutlineThickness = NonNegative(source.RainOutlineThickness, 1f),
                RainShadowEnabled = source.UseCustomRainShadow && source.RainShadowEnabled,
                RainShadowColor = Color(source.RainShadowColor, 0f, 0f, 0f, 0.35f),
                RainShadowOffset = Pair(source.RainShadowOffset, 2f, -2f, true),
                RainShadowSoftness = NonNegative(source.RainShadowSoftness, 12f),
                RainShadowStrength = Mathf.Clamp01(source.RainShadowStrength),
                UseCustomAnimation = source.UseCustomKeyPressAnimation,
                PressAnimationEnabled = source.KeyPressAnimationEnabled,
                PressAnimationDuration = Positive(source.KeyPressAnimationDuration, 0.12f),
                PressAnimationEasing = source.KeyPressAnimationEasing,
                PressAnimationAffectColors = source.KeyPressAnimationAffectColors,
                PressAnimationScale = Positive(source.KeyPressAnimationScale, 1f),
                PressAnimationOffsetX = source.KeyPressAnimationOffsetX,
                PressAnimationOffsetY = -source.KeyPressAnimationOffsetY
            };

            bool statisticColor = source.NodeType == 1 || source.NodeType == 2;
            node.UseCustomStyle = source.UseCustomColor || statisticColor || source.BorderThickness >= 0f
                || source.CornerRadius >= 0f || source.HideCountText;
            node.BackgroundNormal = Color(source.UseCustomColor ? source.ColorBgNormal : owner.ColorBgNormal,
                profile.BackgroundNormal[0], profile.BackgroundNormal[1], profile.BackgroundNormal[2], profile.BackgroundNormal[3]);
            node.BackgroundPressed = Color(source.UseCustomColor ? source.ColorBgPressed : owner.ColorBgPressed,
                profile.BackgroundPressed[0], profile.BackgroundPressed[1], profile.BackgroundPressed[2], profile.BackgroundPressed[3]);
            node.BorderNormal = Color(source.UseCustomColor ? source.ColorBorderNormal : owner.ColorBorderNormal,
                profile.BorderNormal[0], profile.BorderNormal[1], profile.BorderNormal[2], profile.BorderNormal[3]);
            node.BorderPressed = Color(source.UseCustomColor ? source.ColorBorderPressed : owner.ColorBorderPressed,
                profile.BorderPressed[0], profile.BorderPressed[1], profile.BorderPressed[2], profile.BorderPressed[3]);
            float[] statistic = source.NodeType == 1 ? owner.ColorKps : owner.ColorTotal;
            node.TextNormal = Color(statisticColor ? statistic : source.UseCustomColor ? source.ColorTextNormal : owner.ColorTextNormal,
                profile.TextNormal[0], profile.TextNormal[1], profile.TextNormal[2], profile.TextNormal[3]);
            node.TextPressed = Color(statisticColor ? statistic : source.UseCustomColor ? source.ColorTextPressed : owner.ColorTextPressed,
                profile.TextPressed[0], profile.TextPressed[1], profile.TextPressed[2], profile.TextPressed[3]);
            return node;
        }

        private static float Positive(float value, float fallback)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f ? value : fallback;
        }

        private static float NonNegative(float value, float fallback)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f ? value : fallback;
        }

        private static float[] Color(float[] value, float r, float g, float b, float a)
        {
            if (value == null || value.Length < 4) return new[] { r, g, b, a };
            return new[]
            {
                Mathf.Clamp01(value[0]), Mathf.Clamp01(value[1]), Mathf.Clamp01(value[2]), Mathf.Clamp01(value[3])
            };
        }

        private static float[] Pair(float[] value, float x, float y, bool flipY)
        {
            if (value == null || value.Length < 2) return new[] { x, y };
            return new[] { value[0], flipY ? -value[1] : value[1] };
        }

        private static void PreparePackageAssets(KvTransferPackage package, List<PackageAsset> assets)
        {
            var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (package.Profiles != null)
            {
                foreach (KvProfile profile in package.Profiles)
                {
                    if (profile == null) continue;
                    profile.FontPath = PrepareAsset(profile.FontPath, "Fonts", assets, sources, entries);
                    PrepareNodeAssets(profile.Keys, assets, sources, entries);
                }
            }
            PrepareNodeAssets(package.Nodes, assets, sources, entries);
        }

        private static void PrepareNodeAssets(IEnumerable<KvKey> nodes, List<PackageAsset> assets,
            Dictionary<string, string> sources, HashSet<string> entries)
        {
            if (nodes == null) return;
            foreach (KvKey node in nodes)
            {
                if (node == null) continue;
                node.ImagePath = PrepareAsset(node.ImagePath, "Images", assets, sources, entries);
                node.VideoPath = PrepareAsset(node.VideoPath, "Videos", assets, sources, entries);
                node.LabelFontPath = PrepareAsset(node.LabelFontPath, "Fonts", assets, sources, entries);
                node.CountFontPath = PrepareAsset(node.CountFontPath, "Fonts", assets, sources, entries);
            }
        }

        private static string PrepareAsset(string path, string category, List<PackageAsset> assets,
            Dictionary<string, string> sources, HashSet<string> entries)
        {
            string source = ResolveExistingAsset(path);
            if (string.IsNullOrEmpty(source)) return path ?? string.Empty;
            string existing;
            if (sources.TryGetValue(source, out existing)) return existing;
            string fileName = SafeName(Path.GetFileName(source));
            if (string.IsNullOrWhiteSpace(fileName)) fileName = "asset.bin";
            string stem = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            string entry = "Assets/" + category + "/" + fileName;
            int suffix = 2;
            while (!entries.Add(entry)) entry = "Assets/" + category + "/" + stem + "_" + suffix++ + extension;
            sources[source] = entry;
            assets.Add(new PackageAsset { Source = source, Entry = entry });
            return entry;
        }

        private static string ResolveExistingAsset(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                string normalized = path.Trim().Replace('/', Path.DirectorySeparatorChar);
                if (Path.IsPathRooted(normalized))
                {
                    string full = Path.GetFullPath(normalized);
                    return File.Exists(full) ? full : null;
                }
                string[] roots =
                {
                    KvRuntime.ModuleDirectory,
                    GameRoot(),
                    Path.Combine(GameRoot(), "CheryToolsAssets")
                };
                foreach (string root in roots)
                {
                    if (string.IsNullOrEmpty(root)) continue;
                    string full = Path.GetFullPath(Path.Combine(root, normalized));
                    if (File.Exists(full)) return full;
                }
                if (normalized.StartsWith("Assets" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    string stripped = normalized.Substring("Assets".Length + 1);
                    string full = Path.GetFullPath(Path.Combine(GameRoot(), "CheryToolsAssets", stripped));
                    if (File.Exists(full)) return full;
                }
            }
            catch { }
            return null;
        }

        private static void ExtractPackageAssets(ZipArchive archive, KvTransferPackage package, string packagePath)
        {
            if (archive == null || package == null) return;
            string moduleRoot = !string.IsNullOrEmpty(KvRuntime.ModuleDirectory) ? KvRuntime.ModuleDirectory : GameRoot();
            string packageName = SafeName(Path.GetFileNameWithoutExtension(packagePath));
            string targetRoot = Path.Combine(moduleRoot, "Assets", "KeyViewer", "Imported", packageName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmssfff"));
            string fullRoot = Path.GetFullPath(targetRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            bool extracted = false;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                string entryName = entry.FullName.Replace('\\', '/');
                if (!entryName.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) continue;
                string relative = entryName.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar);
                string target = Path.GetFullPath(Path.Combine(targetRoot, relative));
                if (!target.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(L("配置包包含非法资源路径：", "The package contains an invalid asset path: ") + entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                entry.ExtractToFile(target, true);
                extracted = true;
            }
            if (!extracted) return;
            RewriteExtractedPaths(package, targetRoot);
        }

        private static void RewriteExtractedPaths(KvTransferPackage package, string root)
        {
            if (package.Profiles != null)
            {
                foreach (KvProfile profile in package.Profiles)
                {
                    if (profile == null) continue;
                    profile.FontPath = ExtractedPath(profile.FontPath, root);
                    RewriteExtractedNodePaths(profile.Keys, root);
                }
            }
            RewriteExtractedNodePaths(package.Nodes, root);
        }

        private static void RewriteExtractedNodePaths(IEnumerable<KvKey> nodes, string root)
        {
            if (nodes == null) return;
            foreach (KvKey node in nodes)
            {
                if (node == null) continue;
                node.ImagePath = ExtractedPath(node.ImagePath, root);
                node.VideoPath = ExtractedPath(node.VideoPath, root);
                node.LabelFontPath = ExtractedPath(node.LabelFontPath, root);
                node.CountFontPath = ExtractedPath(node.CountFontPath, root);
            }
        }

        private static string ExtractedPath(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            string normalized = path.Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return path;
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(Path.Combine(root,
                normalized.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(L("配置清单包含非法资源路径：", "The manifest contains an invalid asset path: ") + path);
            return File.Exists(candidate) ? candidate : path;
        }

        private static void AdaptProfile(KvProfile profile, float scaleX, float scaleY)
        {
            if (profile == null) return;
            float uniform = Mathf.Min(scaleX, scaleY);
            profile.OffsetX *= scaleX;
            profile.OffsetY *= scaleY;
            profile.KeyWidth *= uniform;
            profile.KeyHeight *= uniform;
            profile.Gap *= uniform;
            profile.CornerRadius *= uniform;
            profile.BorderThickness *= uniform;
            profile.LabelSize *= uniform;
            profile.CountSize *= uniform;
            profile.LabelOutlineThickness *= uniform;
            profile.CountOutlineThickness *= uniform;
            ScalePair(profile.LabelShadowOffset, scaleX, scaleY);
            ScalePair(profile.CountShadowOffset, scaleX, scaleY);
            profile.RainSpeed *= scaleY;
            profile.RainMaxHeight *= scaleY;
            profile.RainYOffset *= scaleY;
            profile.RainYOffset2 *= scaleY;
            profile.RainYOffset3 *= scaleY;
            profile.RainYOffset4 *= scaleY;
            profile.RainCornerRadius *= uniform;
            profile.RainOutlineThickness *= uniform;
            ScalePair(profile.RainShadowOffset, scaleX, scaleY);
            profile.RainShadowSoftness *= uniform;
            profile.PressAnimationOffsetX *= scaleX;
            profile.PressAnimationOffsetY *= scaleY;
            if (profile.Keys == null) return;
            foreach (KvKey node in profile.Keys)
            {
                if (node == null) continue;
                AdaptNode(node, scaleX, scaleY);
            }
        }

        private static void AdaptNode(KvKey node, float scaleX, float scaleY)
        {
            if (node == null) return;
            float uniform = Mathf.Min(scaleX, scaleY);
            node.PositionX *= scaleX;
            node.PositionY *= scaleY;
            if (node.Width >= 0f) node.Width *= uniform;
            if (node.Height >= 0f) node.Height *= uniform;
            if (node.CornerRadius >= 0f) node.CornerRadius *= uniform;
            if (node.BorderThickness >= 0f) node.BorderThickness *= uniform;
            if (node.LabelSize >= 0f) node.LabelSize *= uniform;
            if (node.CountSize >= 0f) node.CountSize *= uniform;
            node.LabelOffsetX *= scaleX;
            node.LabelOffsetY *= scaleY;
            node.CountOffsetX *= scaleX;
            node.CountOffsetY *= scaleY;
            node.MediaOffsetX *= scaleX;
            node.MediaOffsetY *= scaleY;
            node.LabelOutlineThickness *= uniform;
            node.CountOutlineThickness *= uniform;
            ScalePair(node.LabelShadowOffset, scaleX, scaleY);
            ScalePair(node.CountShadowOffset, scaleX, scaleY);
            node.RainYOffset *= scaleY;
            node.RainCornerRadius *= uniform;
            node.RainOutlineThickness *= uniform;
            ScalePair(node.RainShadowOffset, scaleX, scaleY);
            node.RainShadowSoftness *= uniform;
            node.PressAnimationOffsetX *= scaleX;
            node.PressAnimationOffsetY *= scaleY;
        }

        private static void ScalePair(float[] pair, float scaleX, float scaleY)
        {
            if (pair == null || pair.Length < 2) return;
            pair[0] *= scaleX;
            pair[1] *= scaleY;
        }

        private static string UniqueName(KvSettings settings, string baseName)
        {
            if (string.IsNullOrWhiteSpace(baseName)) baseName = L("导入配置", "Imported profile");
            string candidate = baseName;
            int suffix = 2;
            while (settings.Profiles.Exists(profile => profile != null && string.Equals(profile.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                candidate = baseName + " " + suffix++;
            return candidate;
        }

        private static string SafeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "KeyViewer";
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            return name;
        }

        private static string GameRoot()
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
            return Environment.CurrentDirectory;
        }
    }
}
