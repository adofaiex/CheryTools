using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CheryTools.Sonnet.Contracts;
using UnityModManagerNet;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.Sonnet.Framework
{
    internal sealed class ModuleFile
    {
        internal string Path;
        internal string FileName;
        internal string Hash;
        internal bool Official;
    }

    // Decides which DLLs in Modules\ count as official. A packaged build carries the SHA-256 of every
    // official module in the CheryToolsOfficialModules assembly metadata (stamped by package.ps1);
    // any other DLL, or an official name with a different hash, is third-party and loads only when the
    // player allows it. While third-party code is loaded the mod reports a different UMM id, so
    // whitelist-based tools stop treating the game as running plain CheryTools.
    internal static class ModuleIntegrity
    {
        internal const string UnofficialModId = "CheryTools-Unofficial";
        private const string ManifestKey = "CheryToolsOfficialModules";

        // Build-time helpers that sit in Modules\ but are never loaded as modules.
        private static readonly string[] IgnoredFiles =
        {
            "CheryTools.Sonnet.Contracts.dll",
            "CheryTools.Sonnet.Example.dll"
        };

        // Used only by builds without a stamped manifest (plain dotnet build during development).
        private static readonly string[] OfficialFileNames =
        {
            "CheryTools.Tools.Preview.dll",
            "CheryTools.KeyViewer.Preview.dll",
            "CheryTools.Overlayer.Preview.dll",
            "CheryTools.AetRecorder.Preview.dll"
        };

        private static readonly List<ModuleFile> _toLoad = new List<ModuleFile>();
        private static readonly List<ModuleFile> _blocked = new List<ModuleFile>();

        internal static IReadOnlyList<ModuleFile> ToLoad => _toLoad;
        internal static IReadOnlyList<ModuleFile> Blocked => _blocked;
        // False on development builds, where official modules are recognized by file name only.
        internal static bool HashesVerified { get; private set; }
        // True once third-party code is allowed to load; the UMM id and the on-screen notice follow it.
        internal static bool ThirdPartyActive { get; private set; }
        // The setting as it was when this session started; a later change waits for a restart.
        internal static bool AllowedAtStartup { get; private set; }

        internal static void Scan(string modulesDirectory, bool allowThirdParty, IModuleLogger logger)
        {
            _toLoad.Clear();
            _blocked.Clear();
            ThirdPartyActive = false;
            AllowedAtStartup = allowThirdParty;
            Dictionary<string, string> manifest = ReadManifest();
            HashesVerified = manifest.Count > 0;
            if (!Directory.Exists(modulesDirectory)) return;

            foreach (string path in Directory.GetFiles(modulesDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                string fileName = System.IO.Path.GetFileName(path);
                if (IgnoredFiles.Any(name => string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase))) continue;

                var file = new ModuleFile { Path = path, FileName = fileName, Hash = HashFile(path, logger) };
                file.Official = HashesVerified
                    ? manifest.TryGetValue(fileName, out string expected)
                        && string.Equals(expected, file.Hash, StringComparison.OrdinalIgnoreCase)
                    : OfficialFileNames.Any(name => string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase));

                if (file.Official || allowThirdParty) _toLoad.Add(file);
                else _blocked.Add(file);
                if (!file.Official && allowThirdParty) ThirdPartyActive = true;
            }

            // Official modules load first: the loader skips a module whose id is already taken, so a
            // third-party DLL reusing an official id must never get in ahead of the real one.
            List<ModuleFile> ordered = _toLoad.Where(file => file.Official).Concat(_toLoad.Where(file => !file.Official)).ToList();
            _toLoad.Clear();
            _toLoad.AddRange(ordered);

            LogSummary(logger);
        }

        // Swaps the id UMM and other mods see. Only the in-memory ModInfo changes; Info.json on disk
        // keeps "CheryTools", so removing the third-party modules and restarting restores it.
        internal static void ApplyModId(UnityModManager.ModEntry entry, IModuleLogger logger)
        {
            if (!ThirdPartyActive || entry?.Info == null) return;
            entry.Info.Id = UnofficialModId;
            logger?.Warning(L("已加载第三方模块，本次运行的 UMM 模组 ID 改为 ", "Third-party modules are loaded; the UMM mod id for this session is ")
                + UnofficialModId);
        }

        private static void LogSummary(IModuleLogger logger)
        {
            if (logger == null) return;
            logger.Info(HashesVerified
                ? L("模块校验：按官方哈希校验", "Module check: verified against official hashes")
                : L("模块校验：开发构建，只按文件名识别官方模块", "Module check: development build, official modules recognized by file name only"));
            foreach (ModuleFile file in _toLoad)
                logger.Info((file.Official ? L("[官方] ", "[official] ") : L("[第三方] ", "[third-party] "))
                    + file.FileName + " " + ShortHash(file.Hash));
            foreach (ModuleFile file in _blocked)
                logger.Warning(L("[第三方，未加载] ", "[third-party, not loaded] ") + file.FileName + " " + ShortHash(file.Hash)
                    + L("。如需加载，请在框架设置中允许第三方模块并重启游戏。", ". To load it, allow third-party modules in the framework settings and restart the game."));
        }

        private static Dictionary<string, string> ReadManifest()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Reflection.AssemblyMetadataAttribute attribute in typeof(ModuleIntegrity).Assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false))
            {
                if (attribute.Key != ManifestKey || string.IsNullOrWhiteSpace(attribute.Value)) continue;
                // "File.dll=HASH|File2.dll=HASH" ('|' because MSBuild splits command-line properties on ';')
                foreach (string pair in attribute.Value.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int equals = pair.IndexOf('=');
                    if (equals <= 0) continue;
                    result[pair.Substring(0, equals).Trim()] = pair.Substring(equals + 1).Trim();
                }
            }
            return result;
        }

        private static string HashFile(string path, IModuleLogger logger)
        {
            try
            {
                using (var sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(path))
                    return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
            catch (Exception ex)
            {
                logger?.Warning(L("无法读取模块文件：", "Could not read module file: ") + path + " " + ex.GetBaseException().Message);
                return string.Empty;
            }
        }

        private static string ShortHash(string hash) => string.IsNullOrEmpty(hash) ? "-" : hash.Substring(0, Math.Min(16, hash.Length));
    }
}
