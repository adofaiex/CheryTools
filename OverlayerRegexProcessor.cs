using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CheryTools
{
    internal sealed class OverlayerRegexDocument
    {
        public string Source = string.Empty;
        public string Body = string.Empty;
        public bool RegexEnabled;
        public OverlayerRegexRule[] Rules = new OverlayerRegexRule[0];

        public bool HasRules
        {
            get { return Rules != null && Rules.Length > 0; }
        }
    }

    internal sealed class OverlayerRegexRule
    {
        public int LineNumber;
        public string Pattern = string.Empty;
        public string Replacement = string.Empty;
        public Regex Regex;
    }

    internal static class OverlayerRegexProcessor
    {
        private const RegexOptions DefaultOptions = RegexOptions.CultureInvariant;
        private const int MaxCachedDocuments = 128;

        private static readonly Dictionary<string, OverlayerRegexDocument> Cache = new Dictionary<string, OverlayerRegexDocument>();
        private static readonly HashSet<string> LoggedErrors = new HashSet<string>();

        public static OverlayerRegexDocument GetDocument(string source)
        {
            string safeSource = source ?? string.Empty;
            if (Cache.TryGetValue(safeSource, out OverlayerRegexDocument cached))
            {
                return cached;
            }

            OverlayerRegexDocument document = ParseDocument(safeSource);
            if (Cache.Count >= MaxCachedDocuments)
            {
                Cache.Clear();
            }
            Cache[safeSource] = document;
            return document;
        }

        public static string Apply(string text, OverlayerRegexDocument document, string context)
        {
            if (string.IsNullOrEmpty(text) || document == null || !document.RegexEnabled || !document.HasRules)
            {
                return text ?? string.Empty;
            }

            string result = text;
            for (int i = 0; i < document.Rules.Length; i++)
            {
                OverlayerRegexRule rule = document.Rules[i];
                if (rule == null || rule.Regex == null)
                {
                    continue;
                }

                try
                {
                    result = rule.Regex.Replace(result, rule.Replacement ?? string.Empty);
                }
                catch (Exception ex)
                {
                    LogOnce("apply|" + rule.LineNumber + "|" + rule.Pattern + "|" + ex.GetType().FullName,
                        $"[CheryTools] OV regex apply failed at line {rule.LineNumber} ({context}): {ex.Message}");
                }
            }

            return result;
        }

        public static bool IsControlLine(string lineText)
        {
            string trimmed = (lineText ?? string.Empty).TrimStart();
            return trimmed.StartsWith("##", StringComparison.Ordinal)
                || IsImportLine(trimmed)
                || IsRegexRuleLine(trimmed);
        }

        public static bool IsImportLine(string trimmedLine)
        {
            string line = trimmedLine ?? string.Empty;
            return line.StartsWith("#import regex", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("#enable regex", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsRegexRuleLine(string trimmedLine)
        {
            string line = trimmedLine ?? string.Empty;
            return line.StartsWith("#regex", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("#replace", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("#substitute", StringComparison.OrdinalIgnoreCase);
        }

        private static OverlayerRegexDocument ParseDocument(string source)
        {
            string normalized = source.Replace("\r\n", "\n").Replace('\r', '\n');
            var body = new StringBuilder(normalized.Length);
            var rules = new List<OverlayerRegexRule>();
            bool regexEnabled = false;

            int lineStart = 0;
            int lineNumber = 1;
            while (lineStart <= normalized.Length)
            {
                int newline = normalized.IndexOf('\n', lineStart);
                bool hasNewline = newline >= 0;
                int lineEnd = hasNewline ? newline : normalized.Length;
                string line = normalized.Substring(lineStart, lineEnd - lineStart);
                string trimmed = line.TrimStart();

                if (trimmed.StartsWith("##", StringComparison.Ordinal))
                {
                    // Comment line: hidden from the rendered OV text.
                }
                else if (IsImportLine(trimmed))
                {
                    regexEnabled = true;
                }
                else if (IsRegexRuleLine(trimmed))
                {
                    if (TryParseRule(trimmed, lineNumber, out OverlayerRegexRule rule))
                    {
                        rules.Add(rule);
                    }
                }
                else
                {
                    body.Append(line);
                    if (hasNewline)
                    {
                        body.Append('\n');
                    }
                }

                if (!hasNewline)
                {
                    break;
                }
                lineStart = newline + 1;
                lineNumber++;
            }

            return new OverlayerRegexDocument
            {
                Source = source,
                Body = body.ToString(),
                RegexEnabled = regexEnabled,
                Rules = rules.ToArray()
            };
        }

        private static bool TryParseRule(string trimmedLine, int lineNumber, out OverlayerRegexRule rule)
        {
            rule = null;
            string rest = GetDirectiveBody(trimmedLine);
            if (string.IsNullOrWhiteSpace(rest))
            {
                return false;
            }

            int index = 0;
            SkipWhite(rest, ref index);
            if (index < rest.Length && rest[index] == 's' && index + 1 < rest.Length && !char.IsLetterOrDigit(rest[index + 1]) && !char.IsWhiteSpace(rest[index + 1]))
            {
                index++;
            }

            if (index >= rest.Length)
            {
                return false;
            }

            char delimiter = rest[index++];
            if (char.IsWhiteSpace(delimiter))
            {
                LogOnce("parse|" + lineNumber, $"[CheryTools] OV regex parse failed at line {lineNumber}: missing delimiter");
                return false;
            }

            if (!ReadDelimited(rest, ref index, delimiter, out string pattern)
                || !ReadDelimited(rest, ref index, delimiter, out string replacement))
            {
                LogOnce("parse|" + lineNumber + "|" + trimmedLine, $"[CheryTools] OV regex parse failed at line {lineNumber}: expected #regex /pattern/replacement/options");
                return false;
            }

            string optionText = index < rest.Length ? rest.Substring(index).Trim() : string.Empty;
            RegexOptions options = ParseOptions(optionText);
            try
            {
                rule = new OverlayerRegexRule
                {
                    LineNumber = lineNumber,
                    Pattern = pattern,
                    Replacement = replacement,
                    Regex = new Regex(pattern, options)
                };
                return true;
            }
            catch (Exception ex)
            {
                LogOnce("compile|" + lineNumber + "|" + pattern, $"[CheryTools] OV regex compile failed at line {lineNumber}: {ex.Message}");
                return false;
            }
        }

        private static string GetDirectiveBody(string trimmedLine)
        {
            string[] names = { "#substitute", "#replace", "#regex" };
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                if (trimmedLine.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                {
                    return trimmedLine.Substring(name.Length);
                }
            }
            return string.Empty;
        }

        private static bool ReadDelimited(string text, ref int index, char delimiter, out string value)
        {
            var builder = new StringBuilder();
            while (index < text.Length)
            {
                char c = text[index++];
                if (c == '\\' && index < text.Length && text[index] == delimiter)
                {
                    builder.Append(delimiter);
                    index++;
                    continue;
                }
                if (c == delimiter)
                {
                    value = builder.ToString();
                    return true;
                }
                builder.Append(c);
            }

            value = builder.ToString();
            return false;
        }

        private static RegexOptions ParseOptions(string optionText)
        {
            RegexOptions options = DefaultOptions;
            if (string.IsNullOrWhiteSpace(optionText))
            {
                return options;
            }

            string trimmed = optionText.Trim();
            string[] tokens = trimmed.Split(new[] { ',', '|', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                tokens = new[] { trimmed };
            }

            for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
            {
                string token = tokens[tokenIndex].Trim();
                if (token.Length == 0)
                {
                    continue;
                }

                if (TryAddNamedOption(token, ref options))
                {
                    continue;
                }

                for (int i = 0; i < token.Length; i++)
                {
                    switch (token[i])
                    {
                        case 'i':
                            options |= RegexOptions.IgnoreCase;
                            break;
                        case 'm':
                            options |= RegexOptions.Multiline;
                            break;
                        case 's':
                            options |= RegexOptions.Singleline;
                            break;
                        case 'x':
                            options |= RegexOptions.IgnorePatternWhitespace;
                            break;
                        case 'n':
                            options |= RegexOptions.ExplicitCapture;
                            break;
                        case 'r':
                            options |= RegexOptions.RightToLeft;
                            break;
                        case 'e':
                            options |= RegexOptions.ECMAScript;
                            break;
                        case 'c':
                            options |= RegexOptions.Compiled;
                            break;
                        case 'g':
                            // Regex.Replace is global by default.
                            break;
                    }
                }
            }

            return options;
        }

        private static bool TryAddNamedOption(string token, ref RegexOptions options)
        {
            if (string.Equals(token, "IgnoreCase", StringComparison.OrdinalIgnoreCase))
            {
                options |= RegexOptions.IgnoreCase;
                return true;
            }
            if (string.Equals(token, "Multiline", StringComparison.OrdinalIgnoreCase))
            {
                options |= RegexOptions.Multiline;
                return true;
            }
            if (string.Equals(token, "Singleline", StringComparison.OrdinalIgnoreCase))
            {
                options |= RegexOptions.Singleline;
                return true;
            }
            if (string.Equals(token, "IgnorePatternWhitespace", StringComparison.OrdinalIgnoreCase))
            {
                options |= RegexOptions.IgnorePatternWhitespace;
                return true;
            }
            if (string.Equals(token, "ExplicitCapture", StringComparison.OrdinalIgnoreCase))
            {
                options |= RegexOptions.ExplicitCapture;
                return true;
            }
            if (string.Equals(token, "Compiled", StringComparison.OrdinalIgnoreCase))
            {
                options |= RegexOptions.Compiled;
                return true;
            }
            if (string.Equals(token, "RightToLeft", StringComparison.OrdinalIgnoreCase))
            {
                options |= RegexOptions.RightToLeft;
                return true;
            }
            if (string.Equals(token, "ECMAScript", StringComparison.OrdinalIgnoreCase))
            {
                options |= RegexOptions.ECMAScript;
                return true;
            }
            if (string.Equals(token, "CultureInvariant", StringComparison.OrdinalIgnoreCase))
            {
                options |= RegexOptions.CultureInvariant;
                return true;
            }
            return false;
        }

        private static void SkipWhite(string text, ref int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }
        }

        private static void LogOnce(string key, string message)
        {
            if (LoggedErrors.Add(key))
            {
                Main.Logger?.Log(message);
            }
        }
    }
}
