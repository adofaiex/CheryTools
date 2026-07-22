using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Steamworks;
using UnityModManagerNet;

namespace CheryTools
{
    public static class CloudSettingsManager
    {
        private const string CloudFileName = "cherytools_settings";

        public static bool IsSteamAvailable => SteamClient.IsValid;

        public static bool HasCloudFile()
        {
            return SteamClient.IsValid
                && SteamRemoteStorage.FileExists(CloudFileName);
        }

        public static bool TryReadFromCloud(Settings settings, UnityModManager.ModEntry modEntry)
        {
            if (!SteamClient.IsValid)
            {
                Main.Logger.Log("[CloudSync] Steam not initialized, cannot read from cloud.");
                return false;
            }

            if (!SteamRemoteStorage.FileExists(CloudFileName))
            {
                Main.Logger.Log("[CloudSync] No cloud file found.");
                return false;
            }

            try
            {
                int fileSize = SteamRemoteStorage.FileSize(CloudFileName);
                if (fileSize <= 0)
                {
                    Main.Logger.Log("[CloudSync] Cloud file is empty.");
                    return false;
                }

                byte[] data = SteamRemoteStorage.FileRead(CloudFileName);
                if (data == null || data.Length == 0)
                {
                    Main.Logger.Log("[CloudSync] Failed to read cloud file.");
                    return false;
                }

                string json = Encoding.UTF8.GetString(data);
                JObject root = JObject.Parse(json);

                string cloudVersion = root.Value<string>("version") ?? "unknown";
                Main.Logger.Log($"[CloudSync] Read cloud data version: {cloudVersion}");

                string xmlContent = root.Value<string>("xml");
                if (string.IsNullOrEmpty(xmlContent))
                {
                    Main.Logger.Log("[CloudSync] Cloud data contains no XML content.");
                    return false;
                }

                string settingsPath = System.IO.Path.Combine(modEntry.Path, "Settings.xml");
                File.WriteAllText(settingsPath, xmlContent, Encoding.UTF8);

                Main.Logger.Log("[CloudSync] Settings.xml written from cloud, reloading...");

                var newSettings = UnityModManager.ModSettings.Load<Settings>(modEntry);
                newSettings.InitNulls();

                // Copy all fields from loaded settings into the current instance
                CopySettingsFields(newSettings, settings);

                return true;
            }
            catch (Exception ex)
            {
                Main.Logger.Log("[CloudSync] Failed to read from cloud: " + ex.ToString());
                return false;
            }
        }

        public static bool WriteToCloud(Settings settings, UnityModManager.ModEntry modEntry)
        {
            if (!SteamClient.IsValid)
            {
                Main.Logger.Log("[CloudSync] Steam not initialized, cannot write to cloud.");
                return false;
            }

            try
            {
                string settingsPath = System.IO.Path.Combine(modEntry.Path, "Settings.xml");
                if (!File.Exists(settingsPath))
                {
                    Main.Logger.Log("[CloudSync] Settings.xml not found, saving first.");
                    settings.Save(modEntry);
                }

                string xmlContent = File.ReadAllText(settingsPath, Encoding.UTF8);

                var root = new JObject
                {
                    ["version"] = GetModVersion(),
                    ["xml"] = xmlContent
                };

                string json = root.ToString(Formatting.None);
                byte[] data = Encoding.UTF8.GetBytes(json);

                bool success = SteamRemoteStorage.FileWrite(CloudFileName, data);
                if (success)
                {
                    Main.Logger.Log($"[CloudSync] Uploaded to cloud ({data.Length} bytes).");
                }
                else
                {
                    Main.Logger.Log("[CloudSync] SteamRemoteStorage.FileWrite returned false.");
                }

                return success;
            }
            catch (Exception ex)
            {
                Main.Logger.Log("[CloudSync] Failed to write to cloud: " + ex.ToString());
                return false;
            }
        }

        private static string GetModVersion()
        {
            return Main.ModEntry?.Info?.Version ?? "unknown";
        }

        /// <summary>
        /// Copies all public instance fields from source Settings to target Settings.
        /// This is needed because UnityModManager.ModSettings.Load creates a new instance.
        /// </summary>
        private static void CopySettingsFields(Settings source, Settings target)
        {
            if (source == null || target == null) return;

            var fields = typeof(Settings).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var field in fields)
            {
                try
                {
                    object value = field.GetValue(source);
                    field.SetValue(target, value);
                }
                catch (Exception ex)
                {
                    Main.Logger.Log($"[CloudSync] Failed to copy field '{field.Name}': {ex.Message}");
                }
            }
        }
    }
}
