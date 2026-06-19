using UnityEngine;

namespace CheryTools
{
    internal static class KeyDisplayNames
    {
        public static string GetKeySymbol(string keyName)
        {
            if (string.IsNullOrWhiteSpace(keyName)) return "";
            if (System.Enum.TryParse(keyName, true, out KeyCode key))
            {
                return GetKeySymbol(key);
            }
            return keyName;
        }

        public static string GetKeySymbol(KeyCode key)
        {
            if (key == KeyCode.None) return "";
            string name = key.ToString();

            if (name.StartsWith("Alpha")) return name.Substring(5);
            if (name.StartsWith("Keypad")) return name.Substring(6);
            switch (key)
            {
                case KeyCode.LeftShift: return "LS";
                case KeyCode.RightShift: return "RS";
                case KeyCode.LeftControl: return "LC";
                case KeyCode.RightControl: return "RC";
                case KeyCode.LeftAlt: return "LA";
                case KeyCode.RightAlt: return "RA";
                case KeyCode.Space: return "Spc";
                case KeyCode.Return: return "Ent";
                case KeyCode.Backspace: return "Bsp";
                case KeyCode.Escape: return "Esc";
                case KeyCode.UpArrow: return "Up";
                case KeyCode.DownArrow: return "Down";
                case KeyCode.LeftArrow: return "Left";
                case KeyCode.RightArrow: return "Right";
                case KeyCode.Tab: return "Tab";
                case KeyCode.Equals: return "=";
                case KeyCode.Minus: return "-";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Backslash: return "\\";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
            }
            return name;
        }
    }
}
