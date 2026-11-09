using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ADOFAI.EditorTweaks.ChartRendering.Features.ChartRendering
{
    internal static class ChartRenderPaths
    {
        private const int MaxBaseFileNameLength = 96;

        public static string GetFfmpegPath()
        {
            if (!string.IsNullOrWhiteSpace(Main.Settings.FfmpegCustomPath) && File.Exists(Main.Settings.FfmpegCustomPath))
            {
                return Main.Settings.FfmpegCustomPath;
            }

            string moduleDir = CheryTools.AetRecorder.Preview.AetRecorderRuntime.ModuleDirectory;
            if (!string.IsNullOrEmpty(moduleDir))
            {
                string bundled = Path.Combine(moduleDir, "ThirdParty", "FFmpeg", "ffmpeg.exe");
                if (File.Exists(bundled)) return bundled;

                string parentBundled = Path.Combine(moduleDir, "..", "ThirdParty", "FFmpeg", "ffmpeg.exe");
                if (File.Exists(parentBundled)) return Path.GetFullPath(parentBundled);
            }

            try
            {
                string gameFfmpeg = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "ffmpeg", "ffmpeg.exe"));
                if (File.Exists(gameFfmpeg)) return gameFfmpeg;
            }
            catch
            {
            }

            return "ffmpeg.exe";
        }

        public static string GetWorkspaceDirectory(Settings settings)
        {
            return ExpandPath(settings.ChartRenderWorkspaceDirectory);
        }

        public static string GetExportDirectory(Settings settings)
        {
            return ExpandPath(settings.ChartRenderExportDirectory);
        }

        public static string MakeSafeFileName(string raw)
        {
            raw = StripRichTextTags(raw);
            if (string.IsNullOrWhiteSpace(raw))
            {
                raw = "ADOFAI_Render";
            }

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                raw = raw.Replace(c, '_');
            }

            raw = Regex.Replace(raw, @"\s+", " ");
            raw = Regex.Replace(raw, @"_+", "_");
            raw = raw.Trim(' ', '.', '_');
            if (string.IsNullOrWhiteSpace(raw))
            {
                raw = "ADOFAI_Render";
            }

            return raw.Length <= MaxBaseFileNameLength
                ? raw
                : raw.Substring(0, MaxBaseFileNameLength).Trim(' ', '.', '_');
        }

        private static string StripRichTextTags(string raw)
        {
            return string.IsNullOrWhiteSpace(raw)
                ? string.Empty
                : Regex.Replace(raw, @"<[^>]*>", string.Empty);
        }

        private static string ExpandPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string expanded = Environment.ExpandEnvironmentVariables(path.Trim());
            return Path.GetFullPath(expanded);
        }
    }
}
