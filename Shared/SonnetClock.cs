using UnityEngine;

namespace CheryTools.Sonnet.Shared
{
    // Shared source file linked into the KeyViewer and Overlayer projects (see SonnetWindowChrome for how
    // shared sources are compiled). Clock for visual animation (key presses, key rain, token animation,
    // data refresh cadence). It follows real time, except while Time.captureDeltaTime is set (AET Recorder
    // offline renders set Time.captureFramerate): then every frame advances by exactly that step, however
    // long the frame took to render, so overlays stay in sync with the rendered gameplay instead of racing
    // ahead with wall-clock time. Unscaled by Time.timeScale, monotonic across mode switches.
    internal static class SonnetClock
    {
        private static int _frame = -1;
        private static double _now;
        private static double _lastReal;
        private static float _delta;

        // Seconds since the clock started; use instead of Time.unscaledTime for animation timestamps.
        internal static float Now
        {
            get
            {
                Advance();
                return (float)_now;
            }
        }

        // Duration of the current frame; use instead of Time.unscaledDeltaTime for animation steps.
        internal static float DeltaTime
        {
            get
            {
                Advance();
                return _delta;
            }
        }

        private static void Advance()
        {
            int frame = Time.frameCount;
            if (frame == _frame) return;
            double real = Time.unscaledTimeAsDouble;
            if (_frame < 0)
            {
                _delta = Time.unscaledDeltaTime;
                _now = real;
            }
            else
            {
                float capture = Time.captureDeltaTime;
                // Frames skipped since the last query (an idle module) count as whole capture steps.
                _delta = capture > 0f ? capture * (frame - _frame) : (float)(real - _lastReal);
                _now += _delta;
            }
            _frame = frame;
            _lastReal = real;
        }
    }
}
