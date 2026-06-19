using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using UnityEngine;

namespace CheryTools
{
    internal static class ExternalOverlayBridge
    {
        private static string _pipeName;
        private static Process _process;
        private static NamedPipeClientStream _pipe;
        private static StreamWriter _writer;
        private static float _nextSendTime;
        private static string _lastPayloadText;
        private static bool _missingExeLogged;
        private static bool _connectFailureLogged;
        private static bool _startedProcess;

        public static bool IsRunning
        {
            get
            {
                try
                {
                    return _process != null && !_process.HasExited;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static void Start()
        {
            EnsureProcess();
        }

        public static void Stop()
        {
            DisposePipe();

            try
            {
                if (_startedProcess && _process != null && !_process.HasExited)
                {
                    _process.Kill();
                }
            }
            catch
            {
                // The overlay is a helper process; shutdown failures are non-fatal for the mod.
            }
            finally
            {
                _process = null;
                _startedProcess = false;
                _lastPayloadText = null;
            }
        }

        public static void SendText(string text)
        {
            SendPayload(text);
        }

        public static void SendRenderState(string json)
        {
            SendPayload(json);
        }

        private static void SendPayload(string payloadText)
        {
            if (Time.realtimeSinceStartup < _nextSendTime)
            {
                return;
            }
            _nextSendTime = Time.realtimeSinceStartup + GetSendIntervalSeconds();

            EnsureProcess();
            if (_writer != null && _pipe != null && _pipe.IsConnected && string.Equals(_lastPayloadText, payloadText, StringComparison.Ordinal))
            {
                return;
            }

            if (!EnsurePipeConnected())
            {
                return;
            }

            try
            {
                string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadText ?? string.Empty));
                _writer.WriteLine(payload);
                _writer.Flush();
                _lastPayloadText = payloadText;
            }
            catch
            {
                DisposePipe();
            }
        }

        private static float GetSendIntervalSeconds()
        {
            float rate = GetOverlayRate();
            return 1f / rate;
        }

        private static float GetOverlayRate()
        {
            float rate = Main.Settings != null ? Main.Settings.OverlayUpdateRate : 240f;
            if (float.IsNaN(rate) || float.IsInfinity(rate) || rate <= 0f)
            {
                rate = 240f;
            }

            if (Main.Settings != null && Main.Settings.OverlayerEditMode)
            {
                rate = Mathf.Max(rate, 240f);
            }

            return Mathf.Clamp(rate, 30f, 360f);
        }

        private static void EnsureProcess()
        {
            if (_pipe != null && _pipe.IsConnected)
            {
                return;
            }

            if (_process != null && !_process.HasExited)
            {
                return;
            }

            string exePath = GetOverlayExePath();
            if (!File.Exists(exePath))
            {
                if (!_missingExeLogged)
                {
                    Main.Logger?.Log("CheryToolsOverlay.exe not found: " + exePath);
                    _missingExeLogged = true;
                }
                return;
            }

            _pipeName = "CheryToolsOverlay_" + Process.GetCurrentProcess().Id.ToString();
            string arguments = "--pid " + Process.GetCurrentProcess().Id + " --fps 360 --pipe \"" + _pipeName + "\"";

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(exePath),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                _process = Process.Start(startInfo);
                _startedProcess = _process != null;
                _lastPayloadText = null;
                _connectFailureLogged = false;
            }
            catch (Exception ex)
            {
                Main.Logger?.Log("Failed to start CheryToolsOverlay.exe: " + ex.Message);
            }
        }

        private static bool EnsurePipeConnected()
        {
            if (_writer != null && _pipe != null && _pipe.IsConnected)
            {
                return true;
            }

            DisposePipe();
            if (string.IsNullOrEmpty(_pipeName))
            {
                return false;
            }

            try
            {
                _pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
                _pipe.Connect(0);
                _writer = new StreamWriter(_pipe, Encoding.UTF8);
                _connectFailureLogged = false;
                return true;
            }
            catch
            {
                DisposePipe();
                if (!_connectFailureLogged)
                {
                    Main.Logger?.Log("Waiting for CheryToolsOverlay pipe: " + _pipeName);
                    _connectFailureLogged = true;
                }
                return false;
            }
        }

        private static void DisposePipe()
        {
            try { _writer?.Dispose(); } catch { }
            try { _pipe?.Dispose(); } catch { }
            _writer = null;
            _pipe = null;
            _lastPayloadText = null;
        }

        private static string GetOverlayExePath()
        {
            string modPath = Main.ModEntry != null ? Main.ModEntry.Path : AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(modPath, "CheryToolsOverlay", "CheryToolsOverlay.exe");
        }
    }
}
