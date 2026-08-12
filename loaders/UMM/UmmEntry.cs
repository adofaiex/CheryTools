using UnityModManagerNet;

namespace CheryTools.Loaders.UMM
{
    public static class UmmEntry
    {
        public static bool Load(UnityModManager.ModEntry entry)
        {
            var logger = new DelegateModLogger(
                entry.Logger.Log,
                entry.Logger.Warning,
                entry.Logger.Error);
            var host = new BasicModHost(
                "UnityModManager",
                entry.Path,
                logger,
                entry.Info.Id,
                entry.Info.Version?.ToString());

            if (!Main.Initialize(host)) return false;

            entry.OnToggle = (_, value) => Main.SetEnabled(value);
            entry.OnGUI = _ => Main.DrawLoaderGui();
            entry.OnSaveGUI = _ => Main.SaveSettings();
            entry.OnUnload = _ =>
            {
                Main.Shutdown();
                return true;
            };
            return true;
        }
    }
}
