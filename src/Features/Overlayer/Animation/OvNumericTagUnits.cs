using System.Globalization;

namespace CheryTools
{
    internal enum OvNumericUnitKind
    {
        Number = 0,
        Percentage = 1,
        Milliseconds = 2,
        Seconds = 3,
        BeatsPerMinute = 4,
        Multiplier = 5,
        FramesPerSecond = 6,
        PerSecond = 7
    }

    internal static class OvNumericTagUnits
    {
        public static OvNumericUnitKind GetUnit(string tag)
        {
            switch (NormalizeTagName(tag))
            {
                case "acc":
                case "xacc":
                case "progress":
                case "interval":
                    return OvNumericUnitKind.Percentage;
                case "timing":
                    return OvNumericUnitKind.Milliseconds;
                case "maptime":
                case "maptime:p":
                case "musictime":
                case "musictime:p":
                    return OvNumericUnitKind.Seconds;
                case "bpm":
                case "tbpm":
                case "cbpm":
                    return OvNumericUnitKind.BeatsPerMinute;
                case "x": return OvNumericUnitKind.Multiplier;
                case "fps": return OvNumericUnitKind.FramesPerSecond;
                case "cur": return OvNumericUnitKind.PerSecond;
                default: return OvNumericUnitKind.Number;
            }
        }

        public static bool IsPercentage(string tag)
        {
            return GetUnit(tag) == OvNumericUnitKind.Percentage;
        }

        public static string GetDisplayName(OvNumericUnitKind unit)
        {
            switch (unit)
            {
                case OvNumericUnitKind.Percentage: return "百分数（0-100）";
                case OvNumericUnitKind.Milliseconds: return "毫秒";
                case OvNumericUnitKind.Seconds: return "秒";
                case OvNumericUnitKind.BeatsPerMinute: return "BPM";
                case OvNumericUnitKind.Multiplier: return "倍速";
                case OvNumericUnitKind.FramesPerSecond: return "FPS";
                case OvNumericUnitKind.PerSecond: return "每秒数值";
                default: return "普通数值";
            }
        }

        public static string GetLabelSuffix(OvNumericUnitKind unit)
        {
            switch (unit)
            {
                case OvNumericUnitKind.Percentage: return " (%)";
                case OvNumericUnitKind.Milliseconds: return " (ms)";
                case OvNumericUnitKind.Seconds: return " (s)";
                case OvNumericUnitKind.BeatsPerMinute: return " (BPM)";
                case OvNumericUnitKind.Multiplier: return " (x)";
                case OvNumericUnitKind.FramesPerSecond: return " (FPS)";
                case OvNumericUnitKind.PerSecond: return " (/s)";
                default: return string.Empty;
            }
        }

        public static string GetDragFormat(OvNumericUnitKind unit)
        {
            switch (unit)
            {
                case OvNumericUnitKind.Percentage: return "%.4f %%";
                case OvNumericUnitKind.Milliseconds: return "%.4f ms";
                case OvNumericUnitKind.Seconds: return "%.4f s";
                case OvNumericUnitKind.BeatsPerMinute: return "%.4f BPM";
                case OvNumericUnitKind.Multiplier: return "%.4f x";
                case OvNumericUnitKind.FramesPerSecond: return "%.4f FPS";
                case OvNumericUnitKind.PerSecond: return "%.4f /s";
                default: return "%.4f";
            }
        }

        public static string FormatValue(float value, OvNumericUnitKind unit)
        {
            string number = value.ToString("0.####", CultureInfo.InvariantCulture);
            switch (unit)
            {
                case OvNumericUnitKind.Percentage: return number + "%";
                case OvNumericUnitKind.Milliseconds: return number + "ms";
                case OvNumericUnitKind.Seconds: return number + "s";
                case OvNumericUnitKind.BeatsPerMinute: return number + " BPM";
                case OvNumericUnitKind.Multiplier: return number + "x";
                case OvNumericUnitKind.FramesPerSecond: return number + " FPS";
                case OvNumericUnitKind.PerSecond: return number + "/s";
                default: return number;
            }
        }

        private static string NormalizeTagName(string tag)
        {
            string source = (tag ?? string.Empty).Trim();
            if (source.Length >= 2 && source[0] == '{' && source[source.Length - 1] == '}')
                source = source.Substring(1, source.Length - 2).Trim();

            int colon = source.LastIndexOf(':');
            if (colon > 0 && int.TryParse(source.Substring(colon + 1), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out _))
                source = source.Substring(0, colon);
            return source.ToLowerInvariant();
        }
    }
}
