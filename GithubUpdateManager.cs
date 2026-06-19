using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CheryTools
{
    internal static class GithubUpdateManager
    {
        private const string ReleasesApiUrl = "https://api.github.com/repos/CherySui/CheryTools/releases?per_page=10";
        private const string UpdaterFileName = "CheryToolsUpdater.exe";
        private static readonly object Sync = new object();

        private static bool _busy;
        private static bool _updateAvailable;
        private static bool _downloadReady;
        private static string _status = "";
        private static string _latestVersion = "";
        private static string _releaseName = "";
        private static string _releaseNotes = "";
        private static string _assetName = "";
        private static string _assetUrl = "";
        private static string _downloadedZipPath = "";

        public static bool IsBusy { get { lock (Sync) return _busy; } }
        public static bool UpdateAvailable { get { lock (Sync) return _updateAvailable; } }
        public static bool DownloadReady { get { lock (Sync) return _downloadReady; } }
        public static string Status { get { lock (Sync) return _status; } }
        public static string LatestVersion { get { lock (Sync) return _latestVersion; } }
        public static string ReleaseName { get { lock (Sync) return _releaseName; } }
        public static string ReleaseNotes { get { lock (Sync) return _releaseNotes; } }
        public static string AssetName { get { lock (Sync) return _assetName; } }
        public static string DownloadedZipPath { get { lock (Sync) return _downloadedZipPath; } }

        public static string CurrentVersion
        {
            get
            {
                try
                {
                    return Main.ModEntry != null && Main.ModEntry.Info != null
                        ? Main.ModEntry.Info.Version
                        : "Alpha 0.0.0";
                }
                catch
                {
                    return "Alpha 0.0.0";
                }
            }
        }

        public static void CheckForUpdates()
        {
            if (!TryBeginBusy("正在检查更新..."))
            {
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    string json = DownloadString(ReleasesApiUrl);
                    JArray releases = JArray.Parse(json);
                    JObject release = FindReleaseWithPackage(releases);
                    if (release == null)
                    {
                        FinishBusy("没有找到可用的 CheryTools 更新包。");
                        return;
                    }

                    JObject asset = FindPackageAsset(release);
                    if (asset == null)
                    {
                        FinishBusy("最新 Release 中没有找到 CheryTools zip 更新包。");
                        return;
                    }

                    string remoteVersionText = GetReleaseVersionText(release);
                    Version currentVersion = ExtractVersion(CurrentVersion);
                    Version remoteVersion = ExtractVersion(remoteVersionText);
                    bool newer = remoteVersion != null && currentVersion != null && remoteVersion > currentVersion;
                    if (!newer)
                    {
                        lock (Sync)
                        {
                            _updateAvailable = false;
                            _downloadReady = false;
                            _latestVersion = remoteVersionText;
                            _releaseName = (string)release["name"] ?? (string)release["tag_name"] ?? "";
                            _releaseNotes = (string)release["body"] ?? "";
                            _assetName = (string)asset["name"] ?? "";
                            _assetUrl = (string)asset["browser_download_url"] ?? "";
                            _downloadedZipPath = "";
                            _status = "当前已是最新版本。";
                        }
                        return;
                    }

                    lock (Sync)
                    {
                        _updateAvailable = true;
                        _downloadReady = false;
                        _latestVersion = remoteVersionText;
                        _releaseName = (string)release["name"] ?? (string)release["tag_name"] ?? "";
                        _releaseNotes = (string)release["body"] ?? "";
                        _assetName = (string)asset["name"] ?? "";
                        _assetUrl = (string)asset["browser_download_url"] ?? "";
                        _downloadedZipPath = "";
                        _status = "发现新版本：" + _latestVersion;
                    }
                }
                catch (Exception ex)
                {
                    FinishBusy("检查更新失败：" + ex.Message);
                }
                finally
                {
                    lock (Sync)
                    {
                        _busy = false;
                    }
                }
            });
        }

        public static void DownloadUpdate()
        {
            string url;
            string assetName;
            lock (Sync)
            {
                if (_busy || string.IsNullOrEmpty(_assetUrl))
                {
                    return;
                }
                _busy = true;
                _status = "正在下载更新包...";
                url = _assetUrl;
                assetName = string.IsNullOrEmpty(_assetName) ? "CheryTools_Update.zip" : _assetName;
            }

            Task.Run(() =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    string updateDir = GetUpdateDirectory();
                    Directory.CreateDirectory(updateDir);
                    string safeName = MakeSafeFileName(assetName);
                    string zipPath = Path.Combine(updateDir, safeName);
                    using (WebClient client = CreateWebClient())
                    {
                        client.DownloadFile(url, zipPath);
                    }

                    ValidatePackage(zipPath);
                    lock (Sync)
                    {
                        _downloadedZipPath = zipPath;
                        _downloadReady = true;
                        _status = "更新包已下载：" + zipPath;
                    }
                }
                catch (Exception ex)
                {
                    lock (Sync)
                    {
                        _downloadReady = false;
                        _downloadedZipPath = "";
                        _status = "下载更新失败：" + ex.Message;
                    }
                }
                finally
                {
                    lock (Sync)
                    {
                        _busy = false;
                    }
                }
            });
        }

        public static void InstallAndRestart()
        {
            string zipPath;
            lock (Sync)
            {
                if (_busy || !_downloadReady || string.IsNullOrEmpty(_downloadedZipPath))
                {
                    return;
                }
                zipPath = _downloadedZipPath;
                _status = "正在启动更新器...";
            }

            try
            {
                ValidatePackage(zipPath);
                string modPath = Main.ModEntry != null ? Main.ModEntry.Path : AppDomain.CurrentDomain.BaseDirectory;
                string updaterSource = Path.Combine(modPath, UpdaterFileName);
                if (!File.Exists(updaterSource))
                {
                    SetStatus("找不到更新器：" + updaterSource);
                    return;
                }

                Process current = Process.GetCurrentProcess();
                string gameExe = current.MainModule != null ? current.MainModule.FileName : "";
                string tempDir = Path.Combine(Path.GetTempPath(), "CheryToolsUpdater", DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
                Directory.CreateDirectory(tempDir);
                string updaterTemp = Path.Combine(tempDir, UpdaterFileName);
                File.Copy(updaterSource, updaterTemp, true);

                string logPath = Path.Combine(GetUpdateDirectory(), "update.log");
                string arguments =
                    "--pid " + Quote(current.Id.ToString(CultureInfo.InvariantCulture)) +
                    " --zip " + Quote(zipPath) +
                    " --mod " + Quote(modPath) +
                    " --game " + Quote(gameExe) +
                    " --restart " + Quote("true") +
                    " --log " + Quote(logPath);

                ProcessStartInfo psi = new ProcessStartInfo(updaterTemp, arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = tempDir
                };
                Process.Start(psi);
                SetStatus("更新器已启动，游戏将关闭并在安装后重启。");
                Application.Quit();
            }
            catch (Exception ex)
            {
                SetStatus("启动更新器失败：" + ex.Message);
            }
        }

        private static JObject FindReleaseWithPackage(JArray releases)
        {
            if (releases == null) return null;
            foreach (JToken token in releases)
            {
                JObject release = token as JObject;
                if (release == null) continue;
                if ((bool?)release["draft"] == true) continue;
                if (FindPackageAsset(release) != null)
                {
                    return release;
                }
            }
            return null;
        }

        private static JObject FindPackageAsset(JObject release)
        {
            JArray assets = release["assets"] as JArray;
            if (assets == null) return null;

            JObject fallback = null;
            foreach (JToken token in assets)
            {
                JObject asset = token as JObject;
                string name = asset != null ? ((string)asset["name"] ?? "") : "";
                string url = asset != null ? ((string)asset["browser_download_url"] ?? "") : "";
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url)) continue;
                if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;

                if (name.IndexOf("CheryTools", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (name.IndexOf("Alpha", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return asset;
                    }
                    fallback = fallback ?? asset;
                }
            }
            return fallback;
        }

        private static string GetReleaseVersionText(JObject release)
        {
            string name = (string)release["name"];
            string tag = (string)release["tag_name"];
            return !string.IsNullOrEmpty(name) && ExtractVersion(name) != null ? name : (tag ?? name ?? "");
        }

        private static Version ExtractVersion(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            Match match = Regex.Match(text, @"(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?");
            if (!match.Success) return null;

            int major = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            int minor = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            int build = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            int revision = match.Groups[4].Success ? int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) : -1;
            return revision >= 0 ? new Version(major, minor, build, revision) : new Version(major, minor, build);
        }

        private static string DownloadString(string url)
        {
            using (WebClient client = CreateWebClient())
            {
                return client.DownloadString(url);
            }
        }

        private static WebClient CreateWebClient()
        {
            WebClient client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = "CheryTools-Updater";
            client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return client;
        }

        private static void ValidatePackage(string zipPath)
        {
            if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
            {
                throw new FileNotFoundException("更新包不存在。", zipPath);
            }

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                bool hasInfo = false;
                bool hasDll = false;
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string name = entry.FullName.Replace('/', '\\');
                    if (name.Equals("CheryTools\\Info.json", StringComparison.OrdinalIgnoreCase) || name.Equals("Info.json", StringComparison.OrdinalIgnoreCase))
                        hasInfo = true;
                    if (name.Equals("CheryTools\\CheryTools.dll", StringComparison.OrdinalIgnoreCase) || name.Equals("CheryTools.dll", StringComparison.OrdinalIgnoreCase))
                        hasDll = true;
                }
                if (!hasInfo || !hasDll)
                {
                    throw new InvalidDataException("更新包结构不正确，缺少 Info.json 或 CheryTools.dll。");
                }
            }
        }

        private static string GetUpdateDirectory()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(local))
            {
                local = Path.GetTempPath();
            }
            return Path.Combine(local, "CheryTools", "Updates");
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        private static bool TryBeginBusy(string status)
        {
            lock (Sync)
            {
                if (_busy)
                {
                    return false;
                }
                _busy = true;
                _status = status;
                return true;
            }
        }

        private static void FinishBusy(string status)
        {
            lock (Sync)
            {
                _status = status;
                _busy = false;
            }
        }

        private static void SetStatus(string status)
        {
            lock (Sync)
            {
                _status = status;
            }
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
