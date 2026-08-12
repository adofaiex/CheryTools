using System;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace CheryTools.Loaders.BepInEx
{
    [BepInPlugin(PluginId, BuildInfo.DisplayName, BuildInfo.AssemblyVersion)]
    [BepInProcess("A Dance of Fire and Ice.exe")]
    public sealed class CheryToolsBepInPlugin : BaseUnityPlugin
    {
        private const string PluginId = "adofaiex.cherytools";
        private bool _ownsCore;

        private void Awake()
        {
            string pluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                ?? Paths.PluginPath;
            Directory.CreateDirectory(pluginPath);
            TryMigrateUmmSettings(pluginPath);

            var logger = new DelegateModLogger(
                message => Logger.LogInfo(message),
                message => Logger.LogWarning(message),
                message => Logger.LogError(message));
            var host = new BasicModHost(
                "BepInEx",
                pluginPath,
                logger,
                BuildInfo.ModId,
                BuildInfo.DisplayVersion);

            _ownsCore = Main.Initialize(host);
            if (_ownsCore)
                Main.SetEnabled(true);
            else
                Logger.LogWarning("CheryTools initialization was skipped because another loader owns it.");
        }

        private void OnDestroy()
        {
            if (_ownsCore) Main.Shutdown();
        }

        private void TryMigrateUmmSettings(string targetDirectory)
        {
            string target = Path.Combine(targetDirectory, "Settings.xml");
            if (File.Exists(target)) return;

            string gameRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string source = Path.Combine(gameRoot, "Mods", BuildInfo.ModId, "Settings.xml");
            if (!File.Exists(source)) return;

            try
            {
                File.Copy(source, target, false);
                Logger.LogInfo("Imported existing UnityModManager CheryTools settings.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Unable to import UnityModManager settings: " + ex.Message);
            }
        }
    }
}
