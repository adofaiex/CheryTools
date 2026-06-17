namespace CheryTools
{
    internal static class OverlayRenderInvalidator
    {
        private static long _revision = 1;

        public static long Revision
        {
            get { return _revision; }
        }

        public static void InvalidateAll()
        {
            unchecked
            {
                _revision++;
                if (_revision <= 0)
                {
                    _revision = 1;
                }
            }
        }
    }
}
