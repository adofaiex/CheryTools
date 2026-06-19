using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml.Serialization;

namespace CheryTools
{
    [Serializable]
    [XmlRoot("Settings")]
    public class LegacyKeyViewerSettings
    {
        public int KeyViewerLayoutTab = 0;
        public List<KVNode> Layout16K;
        public List<KVNode> Layout12K;
        public List<KVNode> Layout10K;
        public List<KVNode> Layout8K;
    }

    internal static class LegacyKeyViewerImporter
    {
        private const string ArchiveAssetsPrefix = "Assets";

        private struct LegacyLayout
        {
            public string Name;
            public int TabIndex;
            public List<KVNode> Nodes;
        }

        public static int ImportFromXmlFile(Settings targetSettings, string xmlPath, out string message)
        {
            if (targetSettings == null)
                throw new InvalidOperationException("Settings is null.");
            if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
                throw new FileNotFoundException("Legacy settings XML not found.", xmlPath);

            using (FileStream stream = File.OpenRead(xmlPath))
            {
                return ImportFromSettingsStream(targetSettings, stream, out message);
            }
        }

        public static int ImportFromCytPackage(Settings targetSettings, string cytPath, out string message)
        {
            if (targetSettings == null)
                throw new InvalidOperationException("Settings is null.");
            if (string.IsNullOrEmpty(cytPath) || !File.Exists(cytPath))
                throw new FileNotFoundException("CYT package not found.", cytPath);

            using (ZipArchive archive = ZipFile.OpenRead(cytPath))
            {
                ZipArchiveEntry settingsEntry = archive.GetEntry("Settings.xml");
                if (settingsEntry == null)
                    throw new InvalidDataException("CYT package does not contain Settings.xml.");

                int imported;
                using (Stream settingsStream = settingsEntry.Open())
                {
                    imported = ImportFromSettingsStream(targetSettings, settingsStream, out message);
                }

                if (imported > 0)
                {
                    ExtractAssetsFromCyt(archive);
                }

                return imported;
            }
        }

        private static int ImportFromSettingsStream(Settings targetSettings, Stream stream, out string message)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(LegacyKeyViewerSettings));
            LegacyKeyViewerSettings legacy = serializer.Deserialize(stream) as LegacyKeyViewerSettings;
            if (legacy == null)
            {
                message = "没有读取到旧 KV 配置。";
                return 0;
            }

            List<LegacyLayout> layouts = CollectLegacyLayouts(legacy);
            if (layouts.Count == 0)
            {
                message = "没有找到旧 Layout16K/12K/10K/8K 配置。";
                return 0;
            }

            targetSettings.EnsureKeyViewerConfigurations();

            int selectedImport = layouts.FindIndex(layout => layout.TabIndex == legacy.KeyViewerLayoutTab);
            if (selectedImport < 0)
                selectedImport = 0;

            int firstAddedIndex = -1;
            int selectedAddedIndex = -1;
            for (int i = 0; i < layouts.Count; i++)
            {
                LegacyLayout layout = layouts[i];
                KVConfiguration config = new KVConfiguration();
                config.Name = MakeUniqueName(targetSettings, "旧 " + layout.Name);
                config.IsEnabled = i == selectedImport;
                config.Nodes = layout.Nodes ?? new List<KVNode>();

                targetSettings.KeyViewerConfigurations.Add(config);
                int addedIndex = targetSettings.KeyViewerConfigurations.Count - 1;
                if (firstAddedIndex < 0)
                    firstAddedIndex = addedIndex;
                if (i == selectedImport)
                    selectedAddedIndex = addedIndex;
            }

            targetSettings.KeyViewerSelectedConfigIndex = selectedAddedIndex >= 0 ? selectedAddedIndex : firstAddedIndex;
            targetSettings.EnsureKeyViewerConfigurations();

            message = "已导入旧 KV 配置 " + layouts.Count.ToString() + " 个。";
            return layouts.Count;
        }

        private static List<LegacyLayout> CollectLegacyLayouts(LegacyKeyViewerSettings legacy)
        {
            List<LegacyLayout> layouts = new List<LegacyLayout>();
            AddLayout(layouts, "16K", 0, legacy.Layout16K);
            AddLayout(layouts, "12K", 1, legacy.Layout12K);
            AddLayout(layouts, "10K", 2, legacy.Layout10K);
            AddLayout(layouts, "8K", 3, legacy.Layout8K);
            return layouts;
        }

        private static void AddLayout(List<LegacyLayout> layouts, string name, int tabIndex, List<KVNode> nodes)
        {
            if (nodes == null || nodes.Count == 0) return;
            layouts.Add(new LegacyLayout
            {
                Name = name,
                TabIndex = tabIndex,
                Nodes = nodes
            });
        }

        private static string MakeUniqueName(Settings settings, string baseName)
        {
            if (settings.KeyViewerConfigurations == null)
                return baseName;

            string candidate = baseName;
            int suffix = 2;
            while (ContainsConfigName(settings, candidate))
            {
                candidate = baseName + " " + suffix.ToString();
                suffix++;
            }
            return candidate;
        }

        private static bool ContainsConfigName(Settings settings, string name)
        {
            foreach (KVConfiguration config in settings.KeyViewerConfigurations)
            {
                if (config == null) continue;
                if (string.Equals(config.Name, name, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void ExtractAssetsFromCyt(ZipArchive archive)
        {
            Directory.CreateDirectory(CheryToolsAssets.AssetsRoot);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;

                string entryName = entry.FullName.Replace('\\', '/');
                string prefix = ArchiveAssetsPrefix + "/";
                if (!entryName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string relative = entryName.Substring(prefix.Length).Replace('/', Path.DirectorySeparatorChar);
                if (string.IsNullOrWhiteSpace(relative)) continue;

                string targetPath = Path.Combine(CheryToolsAssets.AssetsRoot, relative);
                if (!IsPathUnderRoot(targetPath, CheryToolsAssets.AssetsRoot))
                    throw new InvalidDataException("CYT package contains an invalid asset path: " + entry.FullName);

                string targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDirectory))
                    Directory.CreateDirectory(targetDirectory);

                entry.ExtractToFile(targetPath, true);
            }
        }

        private static bool IsPathUnderRoot(string path, string root)
        {
            string fullPath = Path.GetFullPath(path);
            string fullRoot = Path.GetFullPath(root);
            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                fullRoot += Path.DirectorySeparatorChar;
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }
    }
}
