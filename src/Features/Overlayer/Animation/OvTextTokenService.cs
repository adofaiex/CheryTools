using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CheryTools
{
    internal sealed class OvTextSourcePart
    {
        public bool IsStyleControl;
        public OvTextTokenBinding Token;
        public string Source = string.Empty;
    }

    internal static class OvTextTokenService
    {
        private sealed class CacheEntry
        {
            public string Source = string.Empty;
            public List<OvTextSourcePart> Parts = new List<OvTextSourcePart>();
        }

        private static readonly Dictionary<OverlayerText, CacheEntry> Cache = new Dictionary<OverlayerText, CacheEntry>();

        public static List<OvTextSourcePart> EnsureBindings(OverlayerText text)
        {
            if (text == null) return new List<OvTextSourcePart>();
            string body = text.TextFormat ?? string.Empty;
            if (Cache.TryGetValue(text, out CacheEntry cached)
                && string.Equals(cached.Source, body, StringComparison.Ordinal)
                && string.Equals(text.TokenSourceSnapshot, body, StringComparison.Ordinal))
            {
                return cached.Parts;
            }
            List<OvTextSourcePart> parts = Tokenize(body);
            List<OvTextTokenBinding> oldBindings = text.TokenBindings ?? new List<OvTextTokenBinding>();
            var newTokens = new List<OvTextTokenBinding>();
            for (int i = 0; i < parts.Count; i++)
            {
                if (!parts[i].IsStyleControl && parts[i].Token != null)
                {
                    newTokens.Add(parts[i].Token);
                }
            }

            ReconcileIds(oldBindings, newTokens);
            text.TokenBindings = newTokens;
            text.TokenSourceSnapshot = body;

            int tokenIndex = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                if (!parts[i].IsStyleControl && parts[i].Token != null)
                {
                    parts[i].Token = newTokens[tokenIndex++];
                }
            }
            Cache[text] = new CacheEntry { Source = body, Parts = parts };
            return parts;
        }

        public static void ClearCache()
        {
            Cache.Clear();
        }

        public static string GetDisplayName(OvTextTokenBinding token)
        {
            if (token == null) return "?";
            if (token.Kind == OvTextTokenKind.Whitespace) return "空格";
            if (token.Kind == OvTextTokenKind.LineBreak) return "换行";
            return token.Lexeme ?? string.Empty;
        }

        private static List<OvTextSourcePart> Tokenize(string source)
        {
            var result = new List<OvTextSourcePart>();
            string text = source ?? string.Empty;
            int index = 0;
            while (index < text.Length)
            {
                if (text[index] == '<')
                {
                    int close = text.IndexOf('>', index + 1);
                    if (close >= 0)
                    {
                        result.Add(new OvTextSourcePart
                        {
                            IsStyleControl = true,
                            Source = text.Substring(index, close - index + 1)
                        });
                        index = close + 1;
                        continue;
                    }
                }

                if (text[index] == '{')
                {
                    int close = FindTagClose(text, index);
                    if (close >= 0)
                    {
                        string tag = text.Substring(index, close - index + 1);
                        result.Add(NewTokenPart(OvTextTokenKind.DynamicTag, tag));
                        index = close + 1;
                        continue;
                    }
                }

                int nextControl = FindNextControl(text, index);
                string literal = text.Substring(index, nextControl - index);
                TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(literal);
                while (enumerator.MoveNext())
                {
                    string element = enumerator.GetTextElement();
                    OvTextTokenKind kind = element == "\n"
                        ? OvTextTokenKind.LineBreak
                        : (string.IsNullOrWhiteSpace(element) ? OvTextTokenKind.Whitespace : OvTextTokenKind.Literal);
                    result.Add(NewTokenPart(kind, element));
                }
                index = nextControl;
            }
            return result;
        }

        private static OvTextSourcePart NewTokenPart(OvTextTokenKind kind, string lexeme)
        {
            return new OvTextSourcePart
            {
                Source = lexeme ?? string.Empty,
                Token = new OvTextTokenBinding
                {
                    Kind = kind,
                    Lexeme = lexeme ?? string.Empty
                }
            };
        }

        private static int FindNextControl(string text, int start)
        {
            int tag = text.IndexOf('{', start);
            int rich = text.IndexOf('<', start);
            int next = text.Length;
            if (tag >= 0) next = Math.Min(next, tag);
            if (rich >= 0) next = Math.Min(next, rich);
            return next > start ? next : Math.Min(text.Length, start + 1);
        }

        private static int FindTagClose(string text, int open)
        {
            if (open < 0 || open >= text.Length || text[open] != '{') return -1;
            return text.IndexOf('}', open + 1);
        }

        private static void ReconcileIds(List<OvTextTokenBinding> oldTokens, List<OvTextTokenBinding> newTokens)
        {
            int oldCount = oldTokens != null ? oldTokens.Count : 0;
            int newCount = newTokens.Count;
            if (oldCount == 0)
            {
                for (int i = 0; i < newCount; i++) newTokens[i].Id = OvAnimationGraph.NewId();
                return;
            }

            if (oldCount > 512 || newCount > 512)
            {
                ReconcileLargeTokenSet(oldTokens, newTokens);
                return;
            }

            int[,] lengths = new int[oldCount + 1, newCount + 1];
            for (int i = oldCount - 1; i >= 0; i--)
            {
                for (int j = newCount - 1; j >= 0; j--)
                {
                    lengths[i, j] = SameToken(oldTokens[i], newTokens[j])
                        ? lengths[i + 1, j + 1] + 1
                        : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
                }
            }

            int oldIndex = 0;
            int newIndex = 0;
            while (oldIndex < oldCount && newIndex < newCount)
            {
                if (SameToken(oldTokens[oldIndex], newTokens[newIndex]))
                {
                    newTokens[newIndex].Id = string.IsNullOrEmpty(oldTokens[oldIndex].Id)
                        ? OvAnimationGraph.NewId()
                        : oldTokens[oldIndex].Id;
                    oldIndex++;
                    newIndex++;
                }
                else if (lengths[oldIndex + 1, newIndex] >= lengths[oldIndex, newIndex + 1])
                {
                    oldIndex++;
                }
                else
                {
                    newIndex++;
                }
            }
            for (int i = 0; i < newCount; i++)
            {
                if (string.IsNullOrEmpty(newTokens[i].Id)) newTokens[i].Id = OvAnimationGraph.NewId();
            }
        }

        private static void ReconcileLargeTokenSet(List<OvTextTokenBinding> oldTokens, List<OvTextTokenBinding> newTokens)
        {
            var available = new Dictionary<string, Queue<string>>(StringComparer.Ordinal);
            for (int i = 0; i < oldTokens.Count; i++)
            {
                OvTextTokenBinding token = oldTokens[i];
                if (token == null || string.IsNullOrEmpty(token.Id)) continue;
                string key = ((int)token.Kind).ToString(CultureInfo.InvariantCulture) + "|" + (token.Lexeme ?? string.Empty);
                if (!available.TryGetValue(key, out Queue<string> ids))
                {
                    ids = new Queue<string>();
                    available[key] = ids;
                }
                ids.Enqueue(token.Id);
            }
            for (int i = 0; i < newTokens.Count; i++)
            {
                OvTextTokenBinding token = newTokens[i];
                string key = ((int)token.Kind).ToString(CultureInfo.InvariantCulture) + "|" + (token.Lexeme ?? string.Empty);
                token.Id = available.TryGetValue(key, out Queue<string> ids) && ids.Count > 0
                    ? ids.Dequeue()
                    : OvAnimationGraph.NewId();
            }
        }

        private static bool SameToken(OvTextTokenBinding left, OvTextTokenBinding right)
        {
            return left != null && right != null
                && left.Kind == right.Kind
                && string.Equals(left.Lexeme, right.Lexeme, StringComparison.Ordinal);
        }
    }
}
