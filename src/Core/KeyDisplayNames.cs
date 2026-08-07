using System.Collections.Generic;
using UnityEngine;

namespace CheryTools
{
    internal static class KeyDisplayNames
    {
        // Both overloads sit on the per-node render path, so resolved symbols are
        // memoized: Enum.TryParse/ToString and the prefix checks only run the first
        // time a given key is seen. The domain is finite (KeyCode values + a handful
        // of user-typed bind strings), so the caches are naturally bounded.
        private static readonly Dictionary<string, string> _symbolsByName = new Dictionary<string, string>();
        private static readonly Dictionary<int, string> _symbolsByCode = new Dictionary<int, string>();

        public static string GetKeySymbol(string keyName)
        {
            if (string.IsNullOrWhiteSpace(keyName)) return "";
            if (_symbolsByName.TryGetValue(keyName, out string cached))
            {
                return cached;
            }

            string symbol = System.Enum.TryParse(keyName, true, out KeyCode key)
                ? GetKeySymbol(key)
                : keyName;
            if (_symbolsByName.Count < 1024)
            {
                _symbolsByName[keyName] = symbol;
            }
            return symbol;
        }

        public static string GetKeySymbol(KeyCode key)
        {
            if (key == KeyCode.None) return "";
            int code = (int)key;
            if (_symbolsByCode.TryGetValue(code, out string cached))
            {
                return cached;
            }

            string symbol = ResolveKeySymbol(key);
            _symbolsByCode[code] = symbol;
            return symbol;
        }

        private static string ResolveKeySymbol(KeyCode key)
        {
            string name = key.ToString();

            if (name.StartsWith("Alpha", System.StringComparison.Ordinal)) return name.Substring(5);
            if (name.StartsWith("Keypad", System.StringComparison.Ordinal)) return name.Substring(6);
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
