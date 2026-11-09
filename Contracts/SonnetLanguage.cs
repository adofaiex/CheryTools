namespace CheryTools.Sonnet.Contracts
{
    // UI language shared by the Framework and every module. The Framework resolves the user's choice
    // (follow the game, Simplified Chinese or English) and sets it; modules read it through SonnetText.L
    // (Shared/SonnetText.cs). It lives in Contracts so every assembly sees the same value.
    public static class SonnetLanguage
    {
        private static bool _isChinese = true;

        public static bool IsChinese
        {
            get { return _isChinese; }
            set
            {
                if (_isChinese == value) return;
                _isChinese = value;
                Revision++;
            }
        }

        // Incremented on every language change, for code that caches translated text.
        public static int Revision { get; private set; }
    }
}
