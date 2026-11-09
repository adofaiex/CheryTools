using System;
using System.Threading;
using ADOFAI.EditorTweaks.Api.Rendering;
using ADOFAI.EditorTweaks.ChartRendering.Features.ChartRendering;
using ADOFAI.EditorTweaks.ChartRendering.Patching;

namespace ADOFAI.EditorTweaks.ChartRendering
{
    public static class Main
    {
        public static Settings Settings { get; internal set; } = new Settings();

        internal static int UnityThreadId { get; set; }

        public static bool IsLoaded { get; internal set; }

        public static void Log(string message)
        {
            CheryTools.AetRecorder.Preview.AetRecorderRuntime.Logger?.Info(message);
        }

        public static void LogError(string message)
        {
            CheryTools.AetRecorder.Preview.AetRecorderRuntime.Logger?.Error(message);
        }
    }
}
