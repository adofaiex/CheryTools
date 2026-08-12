using System;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;

[assembly: MelonInfo(
    typeof(CheryTools.Loaders.MelonLoader.CheryToolsMelonMod),
    CheryTools.BuildInfo.DisplayName,
    CheryTools.BuildInfo.DisplayVersion,
    CheryTools.BuildInfo.Author)]
[assembly: MelonGame("7th Beat Games", "A Dance of Fire and Ice")]

namespace CheryTools.Loaders.MelonLoader
{
    public sealed class CheryToolsMelonMod : MelonMod
    {
        private bool _ownsCore;

        public override void OnInitializeMelon()
        {
            string dataPath = Path.Combine(MelonEnvironment.UserDataDirectory, BuildInfo.ModId);
            Directory.CreateDirectory(dataPath);
            TryMigrateUmmSettings(dataPath);

            var logger = new DelegateModLogger(
                LoggerInstance.Msg,
                LoggerInstance.Warning,
                LoggerInstance.Error);
            var host = new BasicModHost(
                "MelonLoader",
                dataPath,
                logger,
                BuildInfo.ModId,
                BuildInfo.DisplayVersion);

            _ownsCore = Main.Initialize(host);
            if (!_ownsCore)
            {
                LoggerInstance.Warning("CheryTools initialization was skipped because another loader owns it.");
                return;
            }

            Main.SetEnabled(true);
        }

        public override void OnDeinitializeMelon()
        {
            if (_ownsCore) Main.Shutdown();
        }

        private void TryMigrateUmmSettings(string targetDirectory)
        {
            string target = Path.Combine(targetDirectory, "Settings.xml");
            if (File.Exists(target)) return;

            string source = Path.Combine(
                MelonEnvironment.GameRootDirectory,
                "Mods",
                BuildInfo.ModId,
                "Settings.xml");
            if (!File.Exists(source)) return;

            try
            {
                File.Copy(source, target, false);
                LoggerInstance.Msg("Imported existing UnityModManager CheryTools settings.");
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Unable to import UnityModManager settings: " + ex.Message);
            }
        }
    }
}
