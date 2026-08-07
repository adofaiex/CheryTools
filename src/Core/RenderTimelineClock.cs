using UnityEngine;

namespace CheryTools
{
    /// <summary>
    /// Game-rendered KV/OV timeline. Unity fixes Time.deltaTime to
    /// 1 / Time.captureFramerate during offline chart rendering, so advancing this
    /// clock from deltaTime keeps overlays deterministic in captured video while
    /// still following pause and time-scale changes during normal play.
    /// </summary>
    internal static class RenderTimelineClock
    {
        private static int _sampledFrame = -1;
        private static double _time;
        private static float _frameDelta;

        public static float Time
        {
            get
            {
                SampleFrame();
                return (float)_time;
            }
        }

        public static float DeltaTime
        {
            get
            {
                SampleFrame();
                return _frameDelta;
            }
        }

        private static void SampleFrame()
        {
            int frame = UnityEngine.Time.frameCount;
            if (_sampledFrame == frame)
                return;

            _sampledFrame = frame;
            float delta = UnityEngine.Time.deltaTime;
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0f)
                delta = 0f;

            _frameDelta = delta;
            _time += delta;
        }
    }
}
