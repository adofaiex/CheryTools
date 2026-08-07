using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace CheryTools
{
    // In-game performance HUD for benchmarking optimization work. Sampling runs every
    // frame; the visible text is re-formatted at most twice per second so the HUD does
    // not perturb the allocation numbers it reports.
    internal static class PerfHud
    {
        private const int FrameWindow = 256;
        private const float DisplayRefreshInterval = 0.5f;
        private const float AllocWindowSeconds = 1f;
        private const float Gen0WindowSeconds = 10f;
        private const string RenderId = "cherytools_perf_hud";

        private static readonly float[] _frameTimes = new float[FrameWindow];
        private static readonly float[] _sortBuffer = new float[FrameWindow];
        private static int _frameCursor;
        private static int _frameCount;

        private static long _lastHeapBytes;
        private static double _allocBytesAccum;
        private static float _allocWindowStart;
        private static float _allocRateKBps;
        private static bool _hasHeapSample;

        private static int _gen0Start;
        private static float _gen0WindowStart;
        private static float _gen0PerMinute;
        private static bool _hasGen0Rate;

        private static float _nextDisplayRefresh;
        private static string _displayText = string.Empty;
        private static readonly StringBuilder _builder = new StringBuilder(192);
        private static bool _wasEnabled;

        public static bool Enabled
        {
            get { return Main.Settings != null && Main.Settings.ShowPerfHud; }
        }

        // Called once per frame from ImGuiController.Update, before any overlay work.
        public static void Sample()
        {
            if (!Enabled)
            {
                if (_wasEnabled) Reset();
                return;
            }
            _wasEnabled = true;

            float now = Time.unscaledTime;
            _frameTimes[_frameCursor] = Time.unscaledDeltaTime * 1000f;
            _frameCursor = (_frameCursor + 1) % FrameWindow;
            if (_frameCount < FrameWindow) _frameCount++;

            long heap = GC.GetTotalMemory(false);
            if (!_hasHeapSample)
            {
                _hasHeapSample = true;
                _allocWindowStart = now;
                _gen0Start = GC.CollectionCount(0);
                _gen0WindowStart = now;
            }
            else
            {
                // Negative deltas are collections; only forward growth is allocation.
                long delta = heap - _lastHeapBytes;
                if (delta > 0) _allocBytesAccum += delta;
            }
            _lastHeapBytes = heap;

            float allocElapsed = now - _allocWindowStart;
            if (allocElapsed >= AllocWindowSeconds)
            {
                _allocRateKBps = (float)(_allocBytesAccum / 1024.0 / allocElapsed);
                _allocBytesAccum = 0.0;
                _allocWindowStart = now;
            }

            float gen0Elapsed = now - _gen0WindowStart;
            if (gen0Elapsed >= Gen0WindowSeconds)
            {
                int current = GC.CollectionCount(0);
                _gen0PerMinute = (current - _gen0Start) * 60f / gen0Elapsed;
                _hasGen0Rate = true;
                _gen0Start = current;
                _gen0WindowStart = now;
            }
        }

        // Called from ImGuiController.ShouldUpdateOverlay so the HUD text keeps
        // refreshing (at 2 Hz) even when everything else is idle.
        public static bool WantsOverlayRefresh(float now)
        {
            return now >= _nextDisplayRefresh;
        }

        // Hooked into ImGuiController.OnOverlayLayout alongside KV/OV RenderUI.
        public static void RenderUI()
        {
            if (!Enabled) return;

            float now = Time.unscaledTime;
            if (now >= _nextDisplayRefresh)
            {
                _nextDisplayRefresh = now + DisplayRefreshInterval;
                RebuildDisplayText();
            }

            if (_displayText.Length == 0) return;

            SdfTextRenderer.DrawScreenText(
                RenderId,
                _displayText,
                null,
                18f,
                new Vector2(8f, 8f),
                new Vector2(420f, 60f),
                0,
                new Vector4(0.55f, 1f, 0.55f, 0.95f),
                true,
                new Vector4(0f, 0f, 0f, 0.9f),
                3f);
        }

        private static void RebuildDisplayText()
        {
            if (_frameCount == 0) return;

            Array.Copy(_frameTimes, _sortBuffer, _frameCount);
            Array.Sort(_sortBuffer, 0, _frameCount);
            float sum = 0f;
            for (int i = 0; i < _frameCount; i++) sum += _sortBuffer[i];
            float avg = sum / _frameCount;
            int p99Index = Math.Min(_frameCount - 1, (int)(_frameCount * 0.99f));
            float p99 = _sortBuffer[p99Index];
            float max = _sortBuffer[_frameCount - 1];
            float fps = avg > 0.0001f ? 1000f / avg : 0f;

            _builder.Length = 0;
            _builder.Append("ms avg ").Append(avg.ToString("F2", CultureInfo.InvariantCulture))
                .Append("  p99 ").Append(p99.ToString("F2", CultureInfo.InvariantCulture))
                .Append("  max ").Append(max.ToString("F2", CultureInfo.InvariantCulture))
                .Append("  (").Append(fps.ToString("F0", CultureInfo.InvariantCulture)).Append(" fps)")
                .Append('\n')
                .Append("gc ").Append(_allocRateKBps.ToString("F1", CultureInfo.InvariantCulture)).Append(" KB/s")
                .Append("  gen0 ");
            if (_hasGen0Rate)
            {
                _builder.Append(_gen0PerMinute.ToString("F1", CultureInfo.InvariantCulture)).Append("/min");
            }
            else
            {
                _builder.Append("--");
            }
            _displayText = _builder.ToString();
        }

        private static void Reset()
        {
            _wasEnabled = false;
            _frameCursor = 0;
            _frameCount = 0;
            _hasHeapSample = false;
            _allocBytesAccum = 0.0;
            _allocRateKBps = 0f;
            _gen0PerMinute = 0f;
            _hasGen0Rate = false;
            _nextDisplayRefresh = 0f;
            _displayText = string.Empty;
        }
    }
}
