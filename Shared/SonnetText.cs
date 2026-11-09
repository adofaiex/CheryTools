using CheryTools.Sonnet.Contracts;

namespace CheryTools.Sonnet.Shared
{
    // Shared source file linked into the Framework and module projects (see SonnetWindowChrome for how
    // shared sources are compiled). Inline UI translation: every user-facing string is written as
    // L("中文", "English") at the point of use, so the two texts stay side by side and a language switch
    // applies on the next frame. With literal arguments the call allocates nothing.
    // Keep ImGui ID suffixes identical in both texts ("启用##x" / "Enable##x") so widget state survives
    // a switch, and never wrap strings that are stored data or compared in logic.
    internal static class SonnetText
    {
        internal static bool IsChinese => SonnetLanguage.IsChinese;

        internal static string L(string zh, string en) => SonnetLanguage.IsChinese ? zh : en;
    }
}
