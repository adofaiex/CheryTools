using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace ADOFAI.EditorTweaks.ChartRendering.Features.ChartRendering
{
    internal sealed class FfmpegEncoder : IDisposable
    {
        private static readonly object EncoderProbeLock = new object();
        private static bool? nvencAvailable;
        private static bool? hevcNvencAvailable;
        private static bool? av1NvencAvailable;

        private readonly string ffmpegPath;
        private readonly string tempVideoPath;
        private readonly string finalVideoPath;
        private readonly int width;
        private readonly int height;
        private readonly int fps;
        private readonly int crf;
        private readonly int configuredBitrateMbps;
        private readonly string encoderMode;
        private readonly string customPreset;
        private readonly string inputPixelFormat;
        private readonly bool flipVertically;
        private readonly int queueCapacityFrames;
        private readonly string audioFormat;
        private readonly string customMuxArgs;
        private readonly float audioSyncOffsetMs;
        private readonly string videoCodec;
        // Guards the process field, which a worker thread swaps (video -> mux -> null) while the
        // main thread may call Abort to cancel.
        private readonly object processLock = new object();
        private volatile bool aborted;
        private Process? process;
        private BlockingCollection<QueuedFrame>? frameQueue;
        private Thread? writerThread;
        private Exception? writerException;
        // Tail of the video process's stderr, so a failed render can say why FFmpeg stopped.
        private const int VideoStderrLimit = 4000;
        private readonly StringBuilder videoStderr = new StringBuilder();

        public FfmpegEncoder(
            string ffmpegPath,
            string tempVideoPath,
            string finalVideoPath,
            int width,
            int height,
            int fps,
            int crf,
            int bitrateMbps,
            string encoderMode,
            string customPreset,
            string inputPixelFormat,
            bool flipVertically,
            int queueCapacityFrames,
            float audioSyncOffsetMs,
            string audioFormat,
            string customMuxArgs,
            string videoCodec = ChartRenderOptionValues.VideoCodecH264)
        {
            this.ffmpegPath = ffmpegPath;
            this.tempVideoPath = tempVideoPath;
            this.finalVideoPath = finalVideoPath;
            this.width = width;
            this.height = height;
            this.fps = fps;
            this.crf = crf;
            configuredBitrateMbps = Math.Max(ChartRenderBitratePresets.AutoBitrateMbps, Math.Min(ChartRenderBitratePresets.MaxBitrateMbps, bitrateMbps));
            this.encoderMode = ChartRenderOptionValues.NormalizeEncoderMode(encoderMode);
            this.customPreset = string.IsNullOrWhiteSpace(customPreset) ? "veryfast" : customPreset.Trim();
            this.inputPixelFormat = ChartRenderOptionValues.NormalizeCaptureFormat(inputPixelFormat);
            this.flipVertically = flipVertically;
            this.queueCapacityFrames = Math.Max(1, queueCapacityFrames);
            this.audioSyncOffsetMs = audioSyncOffsetMs;
            this.audioFormat = ChartRenderOptionValues.NormalizeAudioFormat(audioFormat);
            this.customMuxArgs = (customMuxArgs ?? string.Empty).Trim();
            this.videoCodec = ChartRenderOptionValues.NormalizeVideoCodec(videoCodec);
        }

        public string EncoderName { get; private set; } = "unknown";

        public void BeginVideo()
        {
            string pixFmt = videoCodec == ChartRenderOptionValues.VideoCodecProRes ? "-pix_fmt yuv422p10le " : "-pix_fmt yuv420p ";
            string args = "-y -f rawvideo -pixel_format " + inputPixelFormat + " "
                + "-video_size " + width + "x" + height + " "
                + "-framerate " + fps + " "
                + "-i - -an -vf " + Quote(GetVideoFilter()) + " "
                + GetVideoEncoderArguments() + " "
                + pixFmt
                + Quote(tempVideoPath);

            ChartRenderDiagnostics.Log("FFmpeg video args: mode=" + encoderMode
                + " input=" + inputPixelFormat
                + " verticalFlip=" + flipVertically
                + " rate=" + (UsesConstantQuality() ? "crf " + ClampCrf() : GetTargetBitrateMbps() + "M")
                + " queueFrames=" + queueCapacityFrames
                + " args=" + args);
            process = StartVideoProcess(args);
            frameQueue = new BlockingCollection<QueuedFrame>(boundedCapacity: queueCapacityFrames);
            writerException = null;
            writerThread = new Thread(WriteLoop)
            {
                IsBackground = true,
                Name = "ADOFAI.EditorTweaks.ChartRendering.FFmpegVideoWriter"
            };
            writerThread.Start();
        }

        private string GetVideoFilter()
        {
            const string evenSizePadding = "pad=ceil(iw/2)*2:ceil(ih/2)*2";
            return flipVertically ? "vflip," + evenSizePadding : evenSizePadding;
        }

        public void WriteFrame(byte[] frame, int length, int repeatCount, Action<byte[]>? release, Func<bool>? isCancelRequested = null)
        {
            QueuedFrame queuedFrame = new QueuedFrame(frame, length, Math.Max(1, repeatCount), release);
            while (true)
            {
                if (isCancelRequested != null && isCancelRequested())
                {
                    throw new OperationCanceledException("Canceled.");
                }

                if (process == null || process.HasExited)
                {
                    throw VideoFailure("FFmpeg video process is not running.", null);
                }

                if (writerException != null)
                {
                    throw VideoFailure("FFmpeg writer failed.", writerException);
                }

                if (frameQueue!.TryAdd(queuedFrame, 50))
                {
                    return;
                }
            }
        }

        public void CompleteVideo()
        {
            Process? video;
            lock (processLock)
            {
                video = process;
            }

            if (video == null)
            {
                return;
            }

            frameQueue?.CompleteAdding();
            writerThread?.Join();
            if (aborted)
            {
                throw new OperationCanceledException("Canceled.");
            }

            if (writerException != null)
            {
                throw VideoFailure("FFmpeg writer failed.", writerException);
            }

            video.WaitForExit();
            if (aborted)
            {
                throw new OperationCanceledException("Canceled.");
            }

            if (video.ExitCode != 0)
            {
                throw VideoFailure("FFmpeg video encoding failed.", null);
            }

            lock (processLock)
            {
                process = null;
            }

            video.Dispose();
            frameQueue?.Dispose();
            frameQueue = null;
            writerThread = null;
        }

        // Thread-safe cancellation for the finalize/mux stages: kills the running FFmpeg process so
        // the worker thread unwinds on its own. It never disposes anything, because disposing a
        // Process while another thread waits on it can leave that wait hanging under Mono.
        public void Abort()
        {
            Process? current;
            lock (processLock)
            {
                aborted = true;
                current = process;
            }

            if (current == null)
            {
                return;
            }

            try
            {
                if (!current.HasExited)
                {
                    current.Kill();
                }
            }
            catch
            {
            }
        }

        public void MuxAudioFile(string audioPath)
        {
            if (!File.Exists(audioPath))
            {
                throw new InvalidOperationException("Captured audio file does not exist: " + audioPath);
            }

            long videoBytes = File.Exists(tempVideoPath) ? new FileInfo(tempVideoPath).Length : 0L;
            long audioBytes = new FileInfo(audioPath).Length;
            ChartRenderDiagnostics.Log("Muxing captured audio. videoBytes=" + videoBytes + " audioBytes=" + audioBytes + " audioSyncOffsetMs=" + Number(audioSyncOffsetMs));

            string args = BuildMuxArguments(audioPath);
            RunMuxProcess(args);
        }

        private string BuildMuxArguments(string audioPath)
        {
            string baseInputs = "-y -i " + Quote(tempVideoPath) + " -i " + Quote(audioPath) + " ";
            string outputArgs = (!string.IsNullOrWhiteSpace(customMuxArgs)
                    ? customMuxArgs + " "
                    : "-c:v copy " + GetAudioCodecArguments() + " -movflags +faststart ")
                + Quote(finalVideoPath);

            if (Math.Abs(audioSyncOffsetMs) < 0.001f)
            {
                if (!string.IsNullOrWhiteSpace(customMuxArgs))
                {
                    return baseInputs + outputArgs;
                }

                return baseInputs + "-map 0:v:0 -map 1:a:0 " + outputArgs;
            }

            string filter;
            if (audioSyncOffsetMs > 0f)
            {
                filter = "[1:a]atrim=start=" + Number(audioSyncOffsetMs * 0.001f) + ",asetpts=PTS-STARTPTS[a]";
            }
            else
            {
                int delayMs = Math.Max(0, (int)Math.Round(Math.Abs(audioSyncOffsetMs)));
                filter = "[1:a]adelay=" + delayMs.ToString(CultureInfo.InvariantCulture) + ":all=1,asetpts=PTS-STARTPTS[a]";
            }

            if (!string.IsNullOrWhiteSpace(customMuxArgs))
            {
                return baseInputs + "-filter_complex " + Quote(filter) + " " + outputArgs;
            }

            return baseInputs + "-filter_complex " + Quote(filter) + " -map 0:v:0 -map " + Quote("[a]") + " " + outputArgs;
        }

        public void Dispose()
        {
            Process? current;
            lock (processLock)
            {
                current = process;
                process = null;
            }

            if (current != null)
            {
                try
                {
                    if (!current.HasExited)
                    {
                        current.Kill();
                    }
                }
                catch
                {
                }

                current.Dispose();
            }

            if (frameQueue != null)
            {
                if (!frameQueue.IsAddingCompleted)
                {
                    frameQueue.CompleteAdding();
                }

                frameQueue.Dispose();
                frameQueue = null;
            }
        }

        private void WriteLoop()
        {
            try
            {
                foreach (QueuedFrame frame in frameQueue!.GetConsumingEnumerable())
                {
                    try
                    {
                        for (int i = 0; i < frame.RepeatCount; i++)
                        {
                            process!.StandardInput.BaseStream.Write(frame.Buffer, 0, frame.Length);
                        }
                    }
                    finally
                    {
                        frame.Release?.Invoke(frame.Buffer);
                    }
                }

                process!.StandardInput.Close();
            }
            catch (Exception ex)
            {
                writerException = ex;
            }
        }

        private string GetRealtimePreset()
        {
            if (string.IsNullOrWhiteSpace(customPreset))
            {
                return "ultrafast";
            }

            string lowered = customPreset.ToLowerInvariant();
            if (lowered == "cpu" || lowered == "x264" || lowered == "veryfast")
            {
                return "ultrafast";
            }

            if (lowered.StartsWith("x264:", StringComparison.Ordinal))
            {
                string x264Preset = customPreset.Substring("x264:".Length).Trim();
                return string.IsNullOrWhiteSpace(x264Preset) ? "ultrafast" : x264Preset;
            }

            return customPreset;
        }

        private string GetAudioCodecArguments()
        {
            switch (audioFormat)
            {
                case ChartRenderOptionValues.AudioFormatFlac:
                    return "-c:a flac";
                case ChartRenderOptionValues.AudioFormatAlac:
                    return "-c:a alac";
                default:
                    return "-c:a aac -b:a 320k -ac 2";
            }
        }

        private string GetVideoEncoderArguments()
        {
            if (encoderMode == ChartRenderOptionValues.EncoderCustom)
            {
                return GetCustomVideoEncoderArguments();
            }

            switch (videoCodec)
            {
                case ChartRenderOptionValues.VideoCodecHevc:
                    return GetHevcEncoderArguments();
                case ChartRenderOptionValues.VideoCodecAv1:
                    return GetAv1EncoderArguments();
                case ChartRenderOptionValues.VideoCodecProRes:
                    return GetProResEncoderArguments();
                default:
                    return GetH264EncoderArguments();
            }
        }

        private string GetH264EncoderArguments()
        {
            if (!ForcesCpuEncoder() && IsNvencAvailable())
            {
                string nvencPreset = GetNvencPreset();
                string tune = encoderMode == ChartRenderOptionValues.EncoderFastest ? "ll" : "hq";
                EncoderName = "h264_nvenc " + nvencPreset;
                Main.Log("Chart renderer using h264_nvenc preset " + nvencPreset + ".");
                return "-c:v h264_nvenc -preset " + nvencPreset + " -tune " + tune + " " + GetNvencBitrateArguments() + " -g " + Math.Max(1, fps * 2);
            }

            string x264Preset = GetX264Preset();
            EncoderName = "libx264 " + x264Preset;
            Main.Log("Chart renderer using libx264 preset " + x264Preset + ".");
            return "-c:v libx264 -preset " + Quote(x264Preset) + " " + GetX264BitrateArguments() + " -g " + Math.Max(1, fps * 2);
        }

        private string GetHevcEncoderArguments()
        {
            if (!ForcesCpuEncoder() && IsHevcNvencAvailable())
            {
                string nvencPreset = GetNvencPreset();
                string tune = encoderMode == ChartRenderOptionValues.EncoderFastest ? "ll" : "hq";
                EncoderName = "hevc_nvenc " + nvencPreset;
                Main.Log("Chart renderer using hevc_nvenc preset " + nvencPreset + ".");
                return "-c:v hevc_nvenc -preset " + nvencPreset + " -tune " + tune + " " + GetNvencBitrateArguments() + " -g " + Math.Max(1, fps * 2);
            }

            string x265Preset = GetX264Preset();
            EncoderName = "libx265 " + x265Preset;
            Main.Log("Chart renderer using libx265 preset " + x265Preset + ".");
            return "-c:v libx265 -preset " + Quote(x265Preset) + " " + GetX264BitrateArguments() + " -g " + Math.Max(1, fps * 2);
        }

        private string GetAv1EncoderArguments()
        {
            if (!ForcesCpuEncoder() && IsAv1NvencAvailable())
            {
                string nvencPreset = GetNvencPreset();
                string tune = encoderMode == ChartRenderOptionValues.EncoderFastest ? "ll" : "hq";
                EncoderName = "av1_nvenc " + nvencPreset;
                Main.Log("Chart renderer using av1_nvenc preset " + nvencPreset + ".");
                return "-c:v av1_nvenc -preset " + nvencPreset + " -tune " + tune + " " + GetNvencBitrateArguments(av1: true) + " -g " + Math.Max(1, fps * 2);
            }

            EncoderName = "libsvtav1 (preset 8)";
            Main.Log("Chart renderer using libsvtav1 preset 8.");
            return "-c:v libsvtav1 -preset 8 -crf " + ClampCrf() + " -g " + Math.Max(1, fps * 2);
        }

        private string GetProResEncoderArguments()
        {
            EncoderName = "prores_ks (Apple ProRes 422 HQ)";
            Main.Log("Chart renderer using prores_ks HQ.");
            return "-c:v prores_ks -profile:v 3 -vendor apl0 -bits_per_mb 8000";
        }

        private string GetCustomVideoEncoderArguments()
        {
            if (!ForcesCustomCpuEncoder() && IsNvencAvailable())
            {
                EncoderName = "h264_nvenc p1";
                Main.Log("Chart renderer using custom compatibility h264_nvenc.");
                return "-c:v h264_nvenc -preset p1 -tune ll " + GetNvencBitrateArguments() + " -g " + Math.Max(1, fps * 2);
            }

            string x264Preset = GetRealtimePreset();
            EncoderName = "libx264 " + x264Preset;
            Main.Log("Chart renderer using custom libx264 preset " + x264Preset + ".");
            return "-c:v libx264 -preset " + Quote(x264Preset) + " " + GetX264BitrateArguments() + " -g " + Math.Max(1, fps * 2);
        }

        private bool ForcesCpuEncoder()
        {
            return encoderMode == ChartRenderOptionValues.EncoderCpuCompatibility;
        }

        private bool ForcesCustomCpuEncoder()
        {
            string lowered = customPreset.ToLowerInvariant();
            return lowered == "cpu" || lowered == "x264" || lowered.StartsWith("x264:", StringComparison.Ordinal);
        }

        private string GetNvencPreset()
        {
            switch (encoderMode)
            {
                case ChartRenderOptionValues.EncoderFastest:
                    return "p1";
                case ChartRenderOptionValues.EncoderQuality:
                    return "p6";
                case ChartRenderOptionValues.EncoderBalanced:
                case ChartRenderOptionValues.EncoderAutoBalanced:
                default:
                    return "p4";
            }
        }

        private string GetX264Preset()
        {
            switch (encoderMode)
            {
                case ChartRenderOptionValues.EncoderFastest:
                    return "ultrafast";
                case ChartRenderOptionValues.EncoderQuality:
                    return "fast";
                case ChartRenderOptionValues.EncoderCpuCompatibility:
                case ChartRenderOptionValues.EncoderBalanced:
                case ChartRenderOptionValues.EncoderAutoBalanced:
                default:
                    return "veryfast";
            }
        }

        private int ClampCrf()
        {
            return Math.Max(0, Math.Min(51, crf));
        }

        private int GetTargetBitrateMbps()
        {
            return ChartRenderBitratePresets.ResolveTargetBitrateMbps(configuredBitrateMbps, width, height, fps);
        }

        // The panel only exposes CRF; the bitrate setting stays at Auto unless a render request
        // sets one. With Auto, CRF alone controls quality and no bitrate cap is applied, so a
        // lower CRF really gives a sharper (and larger) video.
        private bool UsesConstantQuality()
        {
            return configuredBitrateMbps <= ChartRenderBitratePresets.AutoBitrateMbps;
        }

        private string GetNvencBitrateArguments(bool av1 = false)
        {
            if (UsesConstantQuality())
            {
                // VBR with -cq still stops at the codec level's bitrate ceiling (about 120 Mbps
                // for 4K120 H.264), so low CRF values all looked the same. Constant QP has no
                // ceiling. AV1 NVENC uses a 0-255 QP scale.
                int qp = av1 ? ClampCrf() * 5 : ClampCrf();
                return "-rc constqp -qp " + qp;
            }

            int target = GetTargetBitrateMbps();
            int maxRate = ChartRenderBitratePresets.GetMaxRateMbps(target);
            int buffer = ChartRenderBitratePresets.GetBufferSizeMbps(target);
            return "-rc vbr -cq " + ClampCrf()
                + " -b:v " + target + "M"
                + " -maxrate " + maxRate + "M"
                + " -bufsize " + buffer + "M";
        }

        private string GetX264BitrateArguments()
        {
            if (UsesConstantQuality())
            {
                return "-crf " + ClampCrf();
            }

            int target = GetTargetBitrateMbps();
            int maxRate = ChartRenderBitratePresets.GetMaxRateMbps(target);
            int buffer = ChartRenderBitratePresets.GetBufferSizeMbps(target);
            return "-b:v " + target + "M"
                + " -maxrate " + maxRate + "M"
                + " -bufsize " + buffer + "M";
        }

        private bool IsNvencAvailable()
        {
            lock (EncoderProbeLock)
            {
                if (nvencAvailable.HasValue)
                {
                    return nvencAvailable.Value;
                }

                nvencAvailable = ProbeEncoder("h264_nvenc");
                return nvencAvailable.Value;
            }
        }

        private bool IsHevcNvencAvailable()
        {
            lock (EncoderProbeLock)
            {
                if (hevcNvencAvailable.HasValue)
                {
                    return hevcNvencAvailable.Value;
                }

                hevcNvencAvailable = ProbeEncoder("hevc_nvenc");
                return hevcNvencAvailable.Value;
            }
        }

        private bool IsAv1NvencAvailable()
        {
            lock (EncoderProbeLock)
            {
                if (av1NvencAvailable.HasValue)
                {
                    return av1NvencAvailable.Value;
                }

                av1NvencAvailable = ProbeEncoder("av1_nvenc");
                return av1NvencAvailable.Value;
            }
        }

        private bool ProbeEncoder(string encoder)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-hide_banner -loglevel error -y -f lavfi -i color=size=256x256:rate=1:duration=0.1 -c:v " + encoder + " -frames:v 1 -f null -",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (Process? probe = Process.Start(info))
                {
                    if (probe == null)
                    {
                        return false;
                    }

                    if (!probe.WaitForExit(5000))
                    {
                        try
                        {
                            probe.Kill();
                        }
                        catch
                        {
                        }

                        return false;
                    }

                    return probe.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private sealed class QueuedFrame
        {
            public QueuedFrame(byte[] buffer, int length, int repeatCount, Action<byte[]>? release)
            {
                Buffer = buffer;
                Length = length;
                RepeatCount = repeatCount;
                Release = release;
            }

            public byte[] Buffer { get; }

            public int Length { get; }

            public int RepeatCount { get; }

            public Action<byte[]>? Release { get; }
        }

        private Process StartVideoProcess(string arguments)
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-hide_banner -loglevel warning " + arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardError = true
            };

            lock (videoStderr)
            {
                videoStderr.Clear();
            }

            Process? started = Process.Start(info);
            if (started == null)
            {
                throw new InvalidOperationException("Failed to start FFmpeg.");
            }

            started.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null)
                {
                    return;
                }

                lock (videoStderr)
                {
                    videoStderr.AppendLine(e.Data);
                    if (videoStderr.Length > VideoStderrLimit)
                    {
                        videoStderr.Remove(0, videoStderr.Length - VideoStderrLimit);
                    }
                }
            };
            started.BeginErrorReadLine();
            return started;
        }

        // Builds the error for a failed video encode with FFmpeg's exit code and stderr, and
        // writes the same detail to the render log.
        private Exception VideoFailure(string message, Exception? inner)
        {
            Process? video = process;
            string exitCode = string.Empty;
            try
            {
                if (video != null && video.HasExited)
                {
                    video.WaitForExit(500);
                    exitCode = " Exit code " + video.ExitCode + ".";
                }
            }
            catch
            {
            }

            string stderr;
            lock (videoStderr)
            {
                stderr = videoStderr.ToString();
            }

            string detail = BuildProcessFailureDetail(string.Empty, stderr);
            ChartRenderDiagnostics.Log(message + exitCode + detail);
            return inner == null
                ? new InvalidOperationException(message + exitCode + detail)
                : new InvalidOperationException(message + exitCode + detail, inner);
        }

        private void RunMuxProcess(string arguments)
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-hide_banner -loglevel warning " + arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            StringBuilder stderr = new StringBuilder();
            StringBuilder stdout = new StringBuilder();
            ChartRenderDiagnostics.Log("FFmpeg mux args: " + arguments);

            if (aborted)
            {
                throw new OperationCanceledException("Canceled.");
            }

            Process? mux = Process.Start(info);
            if (mux == null)
            {
                throw new InvalidOperationException("Failed to start FFmpeg.");
            }

            bool abortedBeforeTracking;
            lock (processLock)
            {
                process = mux;
                abortedBeforeTracking = aborted;
            }

            if (abortedBeforeTracking)
            {
                try
                {
                    mux.Kill();
                }
                catch
                {
                }
            }

            try
            {
                using (mux)
                {
                    mux.ErrorDataReceived += (_, e) =>
                    {
                        if (e.Data != null)
                        {
                            stderr.AppendLine(e.Data);
                        }
                    };
                    mux.OutputDataReceived += (_, e) =>
                    {
                        if (e.Data != null)
                        {
                            stdout.AppendLine(e.Data);
                        }
                    };
                    mux.BeginErrorReadLine();
                    mux.BeginOutputReadLine();
                    mux.WaitForExit();
                    if (aborted)
                    {
                        throw new OperationCanceledException("Canceled.");
                    }

                    if (mux.ExitCode != 0)
                    {
                        string detail = BuildProcessFailureDetail(stdout.ToString(), stderr.ToString());
                        ChartRenderDiagnostics.Log("FFmpeg audio mux failed with exit code " + mux.ExitCode + "." + detail);
                        throw new InvalidOperationException("FFmpeg audio mux failed with exit code " + mux.ExitCode + "." + detail);
                    }
                }
            }
            finally
            {
                lock (processLock)
                {
                    process = null;
                }
            }
        }

        private static string BuildProcessFailureDetail(string stdout, string stderr)
        {
            StringBuilder detail = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                detail.AppendLine().Append("stderr: ").Append(stderr.Trim());
            }

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                detail.AppendLine().Append("stdout: ").Append(stdout.Trim());
            }

            return detail.ToString();
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string Number(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value)
                ? value.ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.######", CultureInfo.InvariantCulture);
        }
    }
}
