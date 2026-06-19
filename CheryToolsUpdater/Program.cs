using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace CheryToolsUpdater
{
    internal static class Program
    {
        private static string _logPath;

        private static int Main(string[] args)
        {
            try
            {
                Dictionary<string, string> options = ParseArgs(args);
                int pid = int.Parse(GetRequired(options, "pid"), CultureInfo.InvariantCulture);
                string zipPath = GetRequired(options, "zip");
                string modPath = GetRequired(options, "mod");
                string gamePath = GetRequired(options, "game");
                bool restart = string.Equals(GetOptional(options, "restart", "false"), "true", StringComparison.OrdinalIgnoreCase);
                _logPath = GetOptional(options, "log", Path.Combine(Path.GetTempPath(), "CheryToolsUpdater.log"));

                Log("CheryTools updater started.");
                Log("Zip: " + zipPath);
                Log("Mod: " + modPath);
                WaitForProcessExit(pid);

                string packageRoot = ExtractAndFindPackage(zipPath);
                InstallPackage(packageRoot, modPath);

                if (restart && File.Exists(gamePath))
                {
                    Log("Restarting game: " + gamePath);
                    ProcessStartInfo psi = new ProcessStartInfo(gamePath)
                    {
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(gamePath)
                    };
                    Process.Start(psi);
                }

                Log("Update completed.");
                return 0;
            }
            catch (Exception ex)
            {
                Log("Update failed: " + ex);
                return 1;
            }
        }

        private static void WaitForProcessExit(int pid)
        {
            try
            {
                Process process = Process.GetProcessById(pid);
                Log("Waiting for game process to exit: " + pid);
                process.WaitForExit();
                Thread.Sleep(500);
            }
            catch (ArgumentException)
            {
                Log("Game process already exited.");
            }
        }

        private static string ExtractAndFindPackage(string zipPath)
        {
            if (!File.Exists(zipPath))
            {
                throw new FileNotFoundException("Update package not found.", zipPath);
            }

            string extractDir = Path.Combine(Path.GetTempPath(), "CheryToolsUpdater", "Extract_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(extractDir);
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            string nested = Path.Combine(extractDir, "CheryTools");
            if (IsValidPackageDirectory(nested))
            {
                return nested;
            }
            if (IsValidPackageDirectory(extractDir))
            {
                return extractDir;
            }

            throw new InvalidDataException("Invalid update package. CheryTools.dll or Info.json is missing.");
        }

        private static void InstallPackage(string packageRoot, string modPath)
        {
            string parent = Directory.GetParent(modPath).FullName;
            string backupPath = Path.Combine(parent, "CheryTools_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
            string failedPath = modPath + "_Failed_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

            Log("Installing package.");
            Log("Package root: " + packageRoot);
            Log("Backup path: " + backupPath);

            bool movedToBackup = false;
            try
            {
                if (Directory.Exists(modPath))
                {
                    Directory.Move(modPath, backupPath);
                    movedToBackup = true;
                }

                CopyDirectory(packageRoot, modPath);
            }
            catch
            {
                if (Directory.Exists(modPath))
                {
                    try
                    {
                        Directory.Move(modPath, failedPath);
                    }
                    catch
                    {
                    }
                }
                if (movedToBackup && Directory.Exists(backupPath) && !Directory.Exists(modPath))
                {
                    Directory.Move(backupPath, modPath);
                    Log("Restored backup after failure.");
                }
                throw;
            }
        }

        private static bool IsValidPackageDirectory(string path)
        {
            return Directory.Exists(path)
                && File.Exists(Path.Combine(path, "Info.json"))
                && File.Exists(Path.Combine(path, "CheryTools.dll"));
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                string relative = directory.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
        }

        private static Dictionary<string, string> ParseArgs(string[] args)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];
                if (!key.StartsWith("--", StringComparison.Ordinal))
                {
                    continue;
                }

                key = key.Substring(2);
                string value = i + 1 < args.Length ? args[++i] : "";
                result[key] = value;
            }
            return result;
        }

        private static string GetRequired(Dictionary<string, string> options, string key)
        {
            string value;
            if (!options.TryGetValue(key, out value) || string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Missing argument: --" + key);
            }
            return value;
        }

        private static string GetOptional(Dictionary<string, string> options, string key, string fallback)
        {
            string value;
            return options.TryGetValue(key, out value) && !string.IsNullOrEmpty(value) ? value : fallback;
        }

        private static void Log(string message)
        {
            try
            {
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine;
                string path = string.IsNullOrEmpty(_logPath) ? Path.Combine(Path.GetTempPath(), "CheryToolsUpdater.log") : _logPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.AppendAllText(path, line);
            }
            catch
            {
            }
        }
    }
}
