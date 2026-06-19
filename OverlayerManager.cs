using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using ImGuiNET;

namespace CheryTools
{
    public class OverlayerManager : MonoBehaviour
    {
        public static OverlayerManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private static float _fpsTimer = 0f;
        private static float _cachedFps = 0f;
        private static int _draggingIndex = -1;
        private static int _draggingIndexImg = -1;
        private static int _draggingIndexBar = -1;
        private static int _draggingIndexVideo = -1;
        private bool _hadVideoLastFrame = false;
        private const float OvSnapThreshold = 5f;
        private static float _ovDragStartX = 0f;
        private static float _ovDragStartY = 0f;
        private static float _ovDragTotalDeltaX = 0f;
        private static float _ovDragTotalDeltaY = 0f;
        private static readonly Regex RichTextTagRegex = new Regex("<.*?>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex FpsTagRegex = new Regex(@"\{fps(?:[:](\d+))?\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex AccTagRegex = new Regex(@"\{acc(?:[:](\d+))?\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex XAccTagRegex = new Regex(@"\{xacc(?:[:](\d+))?\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ProgressTagRegex = new Regex(@"\{progress(?:[:](\d+))?\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex BpmTagRegex = new Regex(@"\{bpm(?:[:](\d+))?\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex TrackBpmTagRegex = new Regex(@"\{tbpm(?:[:](\d+))?\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex CurrentBpmTagRegex = new Regex(@"\{cbpm(?:[:](\d+))?\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly string[] HitCountTags = new string[] { "{te}", "{ve}", "{ep}", "{p}", "{lp}", "{vl}", "{tl}", "{miss}", "{fm}", "{fo}" };

        private enum OvTagKind
        {
            Literal,
            Kps,
            TotalHits,
            TotalTiles,
            PassedTiles,
            LevelAuthor,
            Speed,
            Fps,
            MapTime,
            MapPlayedTime,
            MusicTime,
            MusicPlayedTime,
            CurrentClicks,
            Judge,
            Interval,
            DateYear,
            DateMonth,
            DateDay,
            WorldTime,
            WorldTime12,
            Bpm,
            TrackBpm,
            CurrentBpm,
            TooEarly,
            VeryEarly,
            EarlyPerfect,
            Perfect,
            LatePerfect,
            VeryLate,
            TooLate,
            Miss,
            FailMiss,
            FailOverload,
            Accuracy,
            XAccuracy,
            Progress,
            PureCombo,
            PerfectCombo,
            Music,
            XPerfectXpp,
            XPerfectEpp,
            XPerfectLpp,
            Expression
        }

        private struct OvTagToken
        {
            public OvTagKind Kind;
            public string Literal;
            public int Decimals;
        }

        private sealed class OvTagPlan
        {
            public string Format;
            public OvTagToken[] Tokens;
            public bool HasTags;
        }

        private sealed class OvExpressionParser
        {
            private readonly string _text;
            private readonly Func<string, double> _resolveVariable;
            private int _index;

            public OvExpressionParser(string text, Func<string, double> resolveVariable)
            {
                _text = text ?? string.Empty;
                _resolveVariable = resolveVariable;
            }

            public bool TryEvaluate(out double value)
            {
                value = 0.0;
                try
                {
                    value = ParseAddSubtract();
                    SkipWhitespace();
                    return _index >= _text.Length && !double.IsNaN(value) && !double.IsInfinity(value);
                }
                catch
                {
                    value = 0.0;
                    return false;
                }
            }

            private double ParseAddSubtract()
            {
                double value = ParseMultiplyDivide();
                while (true)
                {
                    SkipWhitespace();
                    if (Match('+'))
                    {
                        value += ParseMultiplyDivide();
                    }
                    else if (Match('-'))
                    {
                        value -= ParseMultiplyDivide();
                    }
                    else
                    {
                        return value;
                    }
                }
            }

            private double ParseMultiplyDivide()
            {
                double value = ParsePower();
                while (true)
                {
                    SkipWhitespace();
                    if (Match('*'))
                    {
                        value *= ParsePower();
                    }
                    else if (Match('/'))
                    {
                        double denominator = ParsePower();
                        value = Math.Abs(denominator) <= double.Epsilon ? 0.0 : value / denominator;
                    }
                    else
                    {
                        return value;
                    }
                }
            }

            private double ParsePower()
            {
                double value = ParseUnary();
                SkipWhitespace();
                if (Match('^'))
                {
                    value = Math.Pow(value, ParsePower());
                }
                return value;
            }

            private double ParseUnary()
            {
                SkipWhitespace();
                if (Match('+'))
                {
                    return ParseUnary();
                }
                if (Match('-'))
                {
                    return -ParseUnary();
                }
                return ParsePrimary();
            }

            private double ParsePrimary()
            {
                SkipWhitespace();
                if (Match('('))
                {
                    double value = ParseAddSubtract();
                    SkipWhitespace();
                    if (!Match(')'))
                    {
                        throw new FormatException("Missing closing parenthesis.");
                    }
                    return value;
                }

                if (_index < _text.Length && (char.IsDigit(_text[_index]) || _text[_index] == '.'))
                {
                    return ParseNumber();
                }

                string identifier = ParseIdentifier();
                if (identifier.Length == 0)
                {
                    throw new FormatException("Expected expression value.");
                }

                SkipWhitespace();
                if (string.Equals(identifier, "sqrt", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Match('('))
                    {
                        throw new FormatException("sqrt requires parentheses.");
                    }
                    double value = ParseAddSubtract();
                    SkipWhitespace();
                    if (!Match(')'))
                    {
                        throw new FormatException("Missing sqrt closing parenthesis.");
                    }
                    return Math.Sqrt(Math.Max(0.0, value));
                }

                return _resolveVariable != null ? _resolveVariable(identifier) : 0.0;
            }

            private double ParseNumber()
            {
                int start = _index;
                bool hasDot = false;
                while (_index < _text.Length)
                {
                    char c = _text[_index];
                    if (char.IsDigit(c))
                    {
                        _index++;
                    }
                    else if (c == '.' && !hasDot)
                    {
                        hasDot = true;
                        _index++;
                    }
                    else
                    {
                        break;
                    }
                }

                string number = _text.Substring(start, _index - start);
                if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    throw new FormatException("Invalid number.");
                }
                return value;
            }

            private string ParseIdentifier()
            {
                int start = _index;
                while (_index < _text.Length)
                {
                    char c = _text[_index];
                    if (char.IsLetterOrDigit(c) || c == '_' || c == ':' || c == '.')
                    {
                        _index++;
                    }
                    else
                    {
                        break;
                    }
                }

                return _text.Substring(start, _index - start);
            }

            private bool Match(char expected)
            {
                SkipWhitespace();
                if (_index < _text.Length && _text[_index] == expected)
                {
                    _index++;
                    return true;
                }
                return false;
            }

            private void SkipWhitespace()
            {
                while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
                {
                    _index++;
                }
            }
        }

        private readonly System.Collections.Generic.Dictionary<OverlayerText, OvTagPlan> _ovTagPlans = new System.Collections.Generic.Dictionary<OverlayerText, OvTagPlan>();
        private readonly StringBuilder _ovTagBuilder = new StringBuilder(256);

        private struct OvSnapCandidate
        {
            public float Value;
            public float MinLimit;
            public float MaxLimit;
        }

        private struct OvAlignLine
        {
            public bool IsVertical;
            public float Coord;
            public float MinLimit;
            public float MaxLimit;
        }

        private static readonly System.Collections.Generic.List<OvAlignLine> _activeOvAlignLines = new System.Collections.Generic.List<OvAlignLine>();

        private int _lastHitCount = 0;
        private int _currentPureCombo = 0;
        private int _currentPerfectCombo = 0;
        private bool _renderDirty = true;
        private long _lastRenderedRevision = -1;
        private long _dynamicScanRevision = -1;
        private bool _hasRateDynamicContent;
        private bool _hasFpsDynamicContent;
        private bool _hasClockDynamicContent;
        private float _nextPeriodicOverlayUpdateTime = 0f;
        private long _runtimeScanRevision = -1;
        private bool _needsHitTracker;
        private bool _needsComboTracker;
        private bool _hasTextAnimations;
        private bool _hasImageAnimations;
        private bool _hasClickAnimations;
        private bool _hasComboAnimations;

        private static bool IsPointInRect(System.Numerics.Vector2 point, System.Numerics.Vector2 min, System.Numerics.Vector2 max)
        {
            return point.X >= min.X && point.X <= max.X && point.Y >= min.Y && point.Y <= max.Y;
        }

        private static bool TryGetCustomFont(string path, bool large, out ImFontPtr font)
        {
            font = default(ImFontPtr);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var fonts = large ? ImGuiController.CustomLargeFonts : ImGuiController.CustomFonts;
            if (fonts.TryGetValue(path, out font))
            {
                return true;
            }

            string resolvedPath = CheryToolsAssets.ResolveAssetPath(path);
            return !string.IsNullOrEmpty(resolvedPath) && fonts.TryGetValue(resolvedPath, out font);
        }

        public bool ShouldUpdateOverlay(float now, float rate)
        {
            if (_renderDirty || _lastRenderedRevision != OverlayRenderInvalidator.Revision)
            {
                return true;
            }

            float interval = GetPeriodicOverlayInterval(rate);
            return interval > 0f && now >= _nextPeriodicOverlayUpdateTime;
        }

        public void MarkOverlayRendered()
        {
            _renderDirty = false;
            _lastRenderedRevision = OverlayRenderInvalidator.Revision;

            float rate = Main.Settings != null ? Main.Settings.OverlayUpdateRate : 240f;
            if (float.IsNaN(rate) || float.IsInfinity(rate) || rate <= 0f)
            {
                rate = 240f;
            }
            rate = Mathf.Clamp(rate, 30f, 360f);

            float interval = GetPeriodicOverlayInterval(rate);
            _nextPeriodicOverlayUpdateTime = interval > 0f
                ? Time.unscaledTime + interval
                : float.PositiveInfinity;
        }

        private void MarkRenderDirty()
        {
            _renderDirty = true;
        }

        private float GetPeriodicOverlayInterval(float rate)
        {
            if (Main.Settings == null || !Main.Settings.OverlayerSystemEnabled)
            {
                return -1f;
            }

            if (Main.Settings.OverlayerEditMode)
            {
                return 1f / Mathf.Clamp(rate, 30f, 360f);
            }

            ScanDynamicOverlayFlags();
            if (_hasRateDynamicContent && Main.IsGamePlaying())
            {
                return 1f / Mathf.Clamp(rate, 30f, 360f);
            }

            if (_hasFpsDynamicContent)
            {
                return 0.25f;
            }

            if (_hasClockDynamicContent)
            {
                return 1f;
            }

            return -1f;
        }

        private void ScanDynamicOverlayFlags()
        {
            long revision = OverlayRenderInvalidator.Revision;
            if (_dynamicScanRevision == revision)
            {
                return;
            }

            _dynamicScanRevision = revision;
            _hasRateDynamicContent = false;
            _hasFpsDynamicContent = false;
            _hasClockDynamicContent = false;

            if (Main.Settings == null)
            {
                return;
            }

            var texts = Main.Settings.OverlayerTexts;
            if (texts != null)
            {
                for (int i = 0; i < texts.Count; i++)
                {
                    OverlayerText text = texts[i];
                    if (text == null || !text.IsEnabled) continue;

                    string format = text.TextFormat ?? string.Empty;
                    if (format.Contains("{fps"))
                    {
                        _hasFpsDynamicContent = true;
                    }
                    if (format.Contains("{wtime"))
                    {
                        _hasClockDynamicContent = true;
                    }
                    if (format.Contains("{progress")
                        || format.Contains("{maptime:p}")
                        || format.Contains("{musictime:p}")
                        || format.Contains("{cur}")
                        || format.Contains("{atile}")
                        || format.Contains("{bpm")
                        || format.Contains("{tbpm")
                        || format.Contains("{cbpm")
                        || format.Contains("{interval}")
                        || format.Contains("{x}")
                        || format.Contains("{xperfect:")
                        || format.IndexOf("{expr:", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _hasRateDynamicContent = true;
                    }

                    if (text.Animations != null && text.Animations.Count > 0)
                    {
                        for (int j = 0; j < text.Animations.Count; j++)
                        {
                            if (text.Animations[j] != null && text.Animations[j].IsEnabled)
                            {
                                _hasRateDynamicContent = true;
                                break;
                            }
                        }
                    }
                }
            }

            var images = Main.Settings.OverlayerImages;
            if (images != null)
            {
                for (int i = 0; i < images.Count; i++)
                {
                    OverlayerImage image = images[i];
                    if (image == null || !image.IsEnabled || image.Animations == null) continue;
                    for (int j = 0; j < image.Animations.Count; j++)
                    {
                        if (image.Animations[j] != null && image.Animations[j].IsEnabled)
                        {
                            _hasRateDynamicContent = true;
                            break;
                        }
                    }
                }
            }

            var bars = Main.Settings.OverlayerProgressBars;
            if (bars != null)
            {
                for (int i = 0; i < bars.Count; i++)
                {
                    OverlayerProgressBar bar = bars[i];
                    if (bar == null || !bar.IsEnabled) continue;
                    if (IsDynamicProgressSource(bar.ValueSource)
                        || IsDynamicProgressSource(bar.MinSource)
                        || IsDynamicProgressSource(bar.MaxSource))
                    {
                        _hasRateDynamicContent = true;
                        break;
                    }
                }
            }
        }

        private static bool IsDynamicProgressSource(OverlayerProgressValueSource source)
        {
            return source != null && source.Kind != OverlayerProgressValueKind.Constant;
        }

        private void ScanRuntimeInterestFlags()
        {
            long revision = OverlayRenderInvalidator.Revision;
            if (_runtimeScanRevision == revision)
            {
                return;
            }

            _runtimeScanRevision = revision;
            _needsHitTracker = false;
            _needsComboTracker = false;
            _hasTextAnimations = false;
            _hasImageAnimations = false;
            _hasClickAnimations = false;
            _hasComboAnimations = false;

            if (Main.Settings == null)
            {
                return;
            }

            var texts = Main.Settings.OverlayerTexts;
            if (texts != null)
            {
                for (int i = 0; i < texts.Count; i++)
                {
                    OverlayerText text = texts[i];
                    if (text == null || (!text.IsEnabled && !Main.Settings.OverlayerEditMode)) continue;

                    string format = text.TextFormat ?? string.Empty;
                    if (ContainsAny(format, HitCountTags))
                    {
                        _needsHitTracker = true;
                    }
                    if (format.Contains("{combo}"))
                    {
                        _needsComboTracker = true;
                    }

                    ScanAnimationInterest(text.Animations, true);
                }
            }

            var images = Main.Settings.OverlayerImages;
            if (images != null)
            {
                for (int i = 0; i < images.Count; i++)
                {
                    OverlayerImage image = images[i];
                    if (image == null || (!image.IsEnabled && !Main.Settings.OverlayerEditMode)) continue;
                    ScanAnimationInterest(image.Animations, false);
                }
            }
        }

        private void ScanAnimationInterest(System.Collections.Generic.List<OverlayerAnimation> animations, bool isTextAnimation)
        {
            if (animations == null)
            {
                return;
            }

            for (int i = 0; i < animations.Count; i++)
            {
                OverlayerAnimation anim = animations[i];
                if (anim == null || !anim.IsEnabled) continue;

                if (isTextAnimation)
                {
                    _hasTextAnimations = true;
                }
                else
                {
                    _hasImageAnimations = true;
                }

                if (anim.Trigger == AnimationTrigger.OnClick)
                {
                    _hasClickAnimations = true;
                }
                else if (anim.Trigger == AnimationTrigger.OnComboIncrease)
                {
                    _hasComboAnimations = true;
                    _needsComboTracker = true;
                }
            }
        }

        private static bool ContainsAny(string value, string[] tokens)
        {
            if (string.IsNullOrEmpty(value) || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                if (value.Contains(tokens[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatDuration(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0.0)
                seconds = 0.0;

            int totalSeconds = (int)Math.Floor(seconds);
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds / 60) % 60;
            int secs = totalSeconds % 60;

            if (hours > 0)
                return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", hours, minutes, secs);

            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", minutes, secs);
        }

        private static string GetMusicText()
        {
            string musicText = "Author - SongName";
            if (scrUIController.instance != null && scrUIController.instance.txtLevelName != null)
            {
                musicText = scrUIController.instance.txtLevelName.text;
            }

            return RichTextTagRegex.Replace(musicText, string.Empty);
        }

        private static string GetLevelAuthorText()
        {
            try
            {
                if (ADOBase.customLevel != null && ADOBase.customLevel.levelData != null)
                {
                    string author = ADOBase.customLevel.levelData.author;
                    if (!string.IsNullOrEmpty(author))
                        return RichTextTagRegex.Replace(author, string.Empty);
                }
            }
            catch
            {
            }

            return "";
        }

        private static string GetSpeedMultiplierText()
        {
            float speed = 1f;
            if (scrConductor.instance != null && scrConductor.instance.song != null)
            {
                speed = scrConductor.instance.song.pitch;
            }
            else if (GCS.speedTrialMode)
            {
                speed = GCS.currentSpeedTrial;
            }

            return speed.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string FormatNumberTrimZeros(double value, int decimals)
        {
            decimals = Math.Max(0, Math.Min(6, decimals));
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                value = 0.0;
            }

            double rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
            if (Math.Abs(rounded) < Math.Pow(10.0, -decimals) * 0.5)
            {
                rounded = 0.0;
            }

            if (decimals == 0)
            {
                return rounded.ToString("0", CultureInfo.InvariantCulture);
            }

            return rounded.ToString("0." + new string('#', decimals), CultureInfo.InvariantCulture);
        }

        private static int GetTagDecimals(Match match, int defaultDecimals)
        {
            int decimals = defaultDecimals;
            if (match.Groups[1].Success && !int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out decimals))
            {
                decimals = defaultDecimals;
            }

            return Math.Max(0, Math.Min(6, decimals));
        }

        private OvTagPlan GetOvTagPlan(OverlayerText ovText, string format)
        {
            if (ovText != null
                && _ovTagPlans.TryGetValue(ovText, out OvTagPlan cached)
                && string.Equals(cached.Format, format, StringComparison.Ordinal))
            {
                return cached;
            }

            OvTagPlan plan = CompileOvTagPlan(format);
            if (ovText != null)
            {
                _ovTagPlans[ovText] = plan;
            }
            return plan;
        }

        private static OvTagPlan CompileOvTagPlan(string format)
        {
            string safeFormat = format ?? string.Empty;
            var tokens = new System.Collections.Generic.List<OvTagToken>();
            bool hasTags = false;
            int index = 0;

            while (index < safeFormat.Length)
            {
                int open = safeFormat.IndexOf('{', index);
                if (open < 0)
                {
                    AddLiteralToken(tokens, safeFormat.Substring(index));
                    break;
                }

                if (open > index)
                {
                    AddLiteralToken(tokens, safeFormat.Substring(index, open - index));
                }

                int close = FindOvTagClose(safeFormat, open);
                if (close < 0)
                {
                    AddLiteralToken(tokens, safeFormat.Substring(open));
                    break;
                }

                string tagBody = safeFormat.Substring(open + 1, close - open - 1);
                if (TryParseExpressionTag(tagBody, out string expression))
                {
                    tokens.Add(new OvTagToken
                    {
                        Kind = OvTagKind.Expression,
                        Literal = expression,
                        Decimals = 2
                    });
                    hasTags = true;
                }
                else if (TryParseOvTag(tagBody, out OvTagKind kind, out int decimals))
                {
                    tokens.Add(new OvTagToken
                    {
                        Kind = kind,
                        Decimals = decimals
                    });
                    hasTags = true;
                }
                else
                {
                    AddLiteralToken(tokens, safeFormat.Substring(open, close - open + 1));
                }

                index = close + 1;
            }

            return new OvTagPlan
            {
                Format = safeFormat,
                Tokens = tokens.ToArray(),
                HasTags = hasTags
            };
        }

        private static int FindOvTagClose(string format, int open)
        {
            if (format == null || open < 0 || open >= format.Length || format[open] != '{')
            {
                return -1;
            }

            const string ExprPrefix = "{expr:";
            if (!StartsWithOrdinalIgnoreCase(format, open, ExprPrefix))
            {
                return format.IndexOf('}', open + 1);
            }

            int depth = 0;
            for (int i = open + ExprPrefix.Length; i < format.Length; i++)
            {
                char c = format[i];
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    if (depth == 0)
                    {
                        return i;
                    }
                    depth--;
                }
            }

            return -1;
        }

        private static bool StartsWithOrdinalIgnoreCase(string value, int start, string prefix)
        {
            return value != null
                && prefix != null
                && start >= 0
                && start + prefix.Length <= value.Length
                && string.Compare(value, start, prefix, 0, prefix.Length, StringComparison.OrdinalIgnoreCase) == 0;
        }

        private static void AddLiteralToken(System.Collections.Generic.List<OvTagToken> tokens, string literal)
        {
            if (string.IsNullOrEmpty(literal)) return;
            tokens.Add(new OvTagToken
            {
                Kind = OvTagKind.Literal,
                Literal = literal
            });
        }

        private static bool TryParseOvTag(string tagBody, out OvTagKind kind, out int decimals)
        {
            kind = OvTagKind.Literal;
            decimals = 0;

            if (string.IsNullOrEmpty(tagBody))
            {
                return false;
            }

            switch (tagBody)
            {
                case "kps": kind = OvTagKind.Kps; return true;
                case "tot": kind = OvTagKind.TotalHits; return true;
                case "ttile": kind = OvTagKind.TotalTiles; return true;
                case "atile": kind = OvTagKind.PassedTiles; return true;
                case "level": kind = OvTagKind.LevelAuthor; return true;
                case "x": kind = OvTagKind.Speed; return true;
                case "maptime": kind = OvTagKind.MapTime; return true;
                case "maptime:p": kind = OvTagKind.MapPlayedTime; return true;
                case "musictime": kind = OvTagKind.MusicTime; return true;
                case "musictime:p": kind = OvTagKind.MusicPlayedTime; return true;
                case "cur": kind = OvTagKind.CurrentClicks; return true;
                case "judge": kind = OvTagKind.Judge; return true;
                case "interval": kind = OvTagKind.Interval; return true;
                case "datey": kind = OvTagKind.DateYear; return true;
                case "datem": kind = OvTagKind.DateMonth; return true;
                case "dated": kind = OvTagKind.DateDay; return true;
                case "wtime": kind = OvTagKind.WorldTime; return true;
                case "wtime12": kind = OvTagKind.WorldTime12; return true;
                case "te": kind = OvTagKind.TooEarly; return true;
                case "ve": kind = OvTagKind.VeryEarly; return true;
                case "ep": kind = OvTagKind.EarlyPerfect; return true;
                case "p": kind = OvTagKind.Perfect; return true;
                case "lp": kind = OvTagKind.LatePerfect; return true;
                case "vl": kind = OvTagKind.VeryLate; return true;
                case "tl": kind = OvTagKind.TooLate; return true;
                case "miss": kind = OvTagKind.Miss; return true;
                case "fm": kind = OvTagKind.FailMiss; return true;
                case "fo": kind = OvTagKind.FailOverload; return true;
                case "combo": kind = OvTagKind.PureCombo; return true;
                case "combo:p": kind = OvTagKind.PerfectCombo; return true;
                case "music": kind = OvTagKind.Music; return true;
                case "xperfect:xpp":
                    kind = OvTagKind.XPerfectXpp; return true;
                case "xperfect:epp":
                    kind = OvTagKind.XPerfectEpp; return true;
                case "xperfect:lpp":
                    kind = OvTagKind.XPerfectLpp; return true;
            }

            if (TryParseDecimalTag(tagBody, "fps", 0, out decimals))
            {
                kind = OvTagKind.Fps;
                return true;
            }
            if (TryParseDecimalTag(tagBody, "acc", 2, out decimals))
            {
                kind = OvTagKind.Accuracy;
                return true;
            }
            if (TryParseDecimalTag(tagBody, "xacc", 2, out decimals))
            {
                kind = OvTagKind.XAccuracy;
                return true;
            }
            if (TryParseDecimalTag(tagBody, "progress", 2, out decimals))
            {
                kind = OvTagKind.Progress;
                return true;
            }
            if (TryParseDecimalTag(tagBody, "bpm", 2, out decimals))
            {
                kind = OvTagKind.Bpm;
                return true;
            }
            if (TryParseDecimalTag(tagBody, "tbpm", 2, out decimals))
            {
                kind = OvTagKind.TrackBpm;
                return true;
            }
            if (TryParseDecimalTag(tagBody, "cbpm", 2, out decimals))
            {
                kind = OvTagKind.CurrentBpm;
                return true;
            }

            return false;
        }

        private static bool TryParseExpressionTag(string tagBody, out string expression)
        {
            expression = null;
            if (string.IsNullOrEmpty(tagBody) || !tagBody.StartsWith("expr:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            expression = tagBody.Substring(5).Trim();
            return expression.Length > 0;
        }

        private static bool TryParseDecimalTag(string tagBody, string name, int defaultDecimals, out int decimals)
        {
            decimals = defaultDecimals;
            if (string.Equals(tagBody, name, StringComparison.Ordinal))
            {
                return true;
            }

            string prefix = name + ":";
            if (!tagBody.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            string decimalText = tagBody.Substring(prefix.Length);
            if (!int.TryParse(decimalText, NumberStyles.Integer, CultureInfo.InvariantCulture, out decimals))
            {
                decimals = defaultDecimals;
            }
            decimals = Math.Max(0, Math.Min(6, decimals));
            return true;
        }

        private string ResolveOvTextTags(OverlayerText ovText, string format)
        {
            OverlayerRegexDocument regexDocument = OverlayerRegexProcessor.GetDocument(format);
            string bodyFormat = regexDocument != null ? regexDocument.Body : (format ?? string.Empty);
            OvTagPlan plan = GetOvTagPlan(ovText, bodyFormat);
            if (plan == null || !plan.HasTags || plan.Tokens == null || plan.Tokens.Length == 0)
            {
                return OverlayerRegexProcessor.Apply(bodyFormat, regexDocument, ovText != null ? ovText.Name : "OV Text");
            }

            _ovTagBuilder.Length = 0;

            DateTime now = default(DateTime);
            bool nowReady = false;
            scrMarginTracker tracker = null;
            bool trackerReady = false;
            bool bpmReady = false;
            float baseBpm = 0f;
            double trackBpm = 0.0;
            double currentBpm = 0.0;

            for (int i = 0; i < plan.Tokens.Length; i++)
            {
                OvTagToken token = plan.Tokens[i];
                if (token.Kind == OvTagKind.Literal)
                {
                    _ovTagBuilder.Append(token.Literal);
                    continue;
                }

                _ovTagBuilder.Append(EvaluateOvTag(token, ref now, ref nowReady, ref tracker, ref trackerReady, ref bpmReady, ref baseBpm, ref trackBpm, ref currentBpm));
            }

            return OverlayerRegexProcessor.Apply(_ovTagBuilder.ToString(), regexDocument, ovText != null ? ovText.Name : "OV Text");
        }

        private string EvaluateOvTag(
            OvTagToken token,
            ref DateTime now,
            ref bool nowReady,
            ref scrMarginTracker tracker,
            ref bool trackerReady,
            ref bool bpmReady,
            ref float baseBpm,
            ref double trackBpm,
            ref double currentBpm)
        {
            switch (token.Kind)
            {
                case OvTagKind.Kps:
                    return (KeyViewerManager.Instance != null ? KeyViewerManager.Instance.CurrentKPS : 0).ToString(CultureInfo.InvariantCulture);
                case OvTagKind.TotalHits:
                    return Main.Settings != null ? Main.Settings.TotalHits.ToString(CultureInfo.InvariantCulture) : "0";
                case OvTagKind.TotalTiles:
                    return GetTotalTileCount().ToString(CultureInfo.InvariantCulture);
                case OvTagKind.PassedTiles:
                    return GetPassedTileCount().ToString(CultureInfo.InvariantCulture);
                case OvTagKind.LevelAuthor:
                    return GetLevelAuthorText();
                case OvTagKind.Speed:
                    return GetSpeedMultiplierText();
                case OvTagKind.Fps:
                    return FormatNumberTrimZeros(_cachedFps, token.Decimals);
                case OvTagKind.MapTime:
                    return TryGetMapTotalSeconds(out double mapTotalSeconds) ? FormatDuration(mapTotalSeconds) : "0:00";
                case OvTagKind.MapPlayedTime:
                    return TryGetMapPlayedSeconds(out double mapPlayedSeconds) ? FormatDuration(mapPlayedSeconds) : "0:00";
                case OvTagKind.MusicTime:
                    return TryGetMusicTotalSeconds(out double musicTotalSeconds) ? FormatDuration(musicTotalSeconds) : "0:00";
                case OvTagKind.MusicPlayedTime:
                    return TryGetMusicPlayedSeconds(out double musicPlayedSeconds) ? FormatDuration(musicPlayedSeconds) : "0:00";
                case OvTagKind.CurrentClicks:
                    return TryGetCurrentClicksPerSecond(out double cps) ? FormatNumberTrimZeros(cps, 2) : "0";
                case OvTagKind.Judge:
                    return GetJudgeText();
                case OvTagKind.Interval:
                    return GetCurrentTimingWindowScaleText();
                case OvTagKind.DateYear:
                    EnsureNow(ref now, ref nowReady);
                    return now.ToString("yyyy", CultureInfo.InvariantCulture);
                case OvTagKind.DateMonth:
                    EnsureNow(ref now, ref nowReady);
                    return now.ToString("MM", CultureInfo.InvariantCulture);
                case OvTagKind.DateDay:
                    EnsureNow(ref now, ref nowReady);
                    return now.ToString("dd", CultureInfo.InvariantCulture);
                case OvTagKind.WorldTime:
                    EnsureNow(ref now, ref nowReady);
                    return now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                case OvTagKind.WorldTime12:
                    EnsureNow(ref now, ref nowReady);
                    return now.ToString("hh:mm:ss tt", CultureInfo.InvariantCulture);
                case OvTagKind.Bpm:
                    EnsureBpmValues(ref bpmReady, ref baseBpm, ref trackBpm, ref currentBpm);
                    return FormatNumberTrimZeros(baseBpm, token.Decimals);
                case OvTagKind.TrackBpm:
                    EnsureBpmValues(ref bpmReady, ref baseBpm, ref trackBpm, ref currentBpm);
                    return FormatNumberTrimZeros(trackBpm, token.Decimals);
                case OvTagKind.CurrentBpm:
                    EnsureBpmValues(ref bpmReady, ref baseBpm, ref trackBpm, ref currentBpm);
                    return FormatNumberTrimZeros(currentBpm, token.Decimals);
                case OvTagKind.TooEarly:
                    return GetTrackerHitCount(ref tracker, ref trackerReady, HitMargin.TooEarly);
                case OvTagKind.VeryEarly:
                    return GetTrackerHitCount(ref tracker, ref trackerReady, HitMargin.VeryEarly);
                case OvTagKind.EarlyPerfect:
                    return GetTrackerHitCount(ref tracker, ref trackerReady, HitMargin.EarlyPerfect);
                case OvTagKind.Perfect:
                    return GetTrackerHitCount(ref tracker, ref trackerReady, HitMargin.Perfect);
                case OvTagKind.LatePerfect:
                    return GetTrackerHitCount(ref tracker, ref trackerReady, HitMargin.LatePerfect);
                case OvTagKind.VeryLate:
                    return GetTrackerHitCount(ref tracker, ref trackerReady, HitMargin.VeryLate);
                case OvTagKind.TooLate:
                    return GetTrackerHitCount(ref tracker, ref trackerReady, HitMargin.TooLate);
                case OvTagKind.Miss:
                    EnsureTracker(ref tracker, ref trackerReady);
                    return tracker != null ? tracker.GetDeaths().ToString(CultureInfo.InvariantCulture) : "0";
                case OvTagKind.FailMiss:
                    return GetTrackerHitCount(ref tracker, ref trackerReady, HitMargin.FailMiss);
                case OvTagKind.FailOverload:
                    return GetTrackerHitCount(ref tracker, ref trackerReady, HitMargin.FailOverload);
                case OvTagKind.Accuracy:
                    EnsureTracker(ref tracker, ref trackerReady);
                    return FormatNumberTrimZeros(tracker != null ? tracker.percentAcc * 100f : 0f, token.Decimals);
                case OvTagKind.XAccuracy:
                    EnsureTracker(ref tracker, ref trackerReady);
                    return FormatNumberTrimZeros(tracker != null ? tracker.percentXAcc * 100f : 0f, token.Decimals);
                case OvTagKind.Progress:
                    EnsureTracker(ref tracker, ref trackerReady);
                    double progress = 0.0;
                    if (tracker != null && scrController.instance != null && scrController.instance.gameworld)
                    {
                        progress = scrController.instance.percentComplete * 100.0;
                    }
                    return FormatNumberTrimZeros(progress, token.Decimals);
                case OvTagKind.PureCombo:
                    return _currentPureCombo.ToString(CultureInfo.InvariantCulture);
                case OvTagKind.PerfectCombo:
                    return _currentPerfectCombo.ToString(CultureInfo.InvariantCulture);
                case OvTagKind.Music:
                    return GetMusicText();
                case OvTagKind.XPerfectXpp:
                    return IsXPerfectIntegrationActive() ? XPerfectBridge.XPerfectCount().ToString(CultureInfo.InvariantCulture) : "0";
                case OvTagKind.XPerfectEpp:
                    return IsXPerfectIntegrationActive() ? XPerfectBridge.PlusPerfectCount().ToString(CultureInfo.InvariantCulture) : "0";
                case OvTagKind.XPerfectLpp:
                    return IsXPerfectIntegrationActive() ? XPerfectBridge.MinusPerfectCount().ToString(CultureInfo.InvariantCulture) : "0";
                case OvTagKind.Expression:
                    return FormatNumberTrimZeros(EvaluateExpressionTag(token.Literal, ref tracker, ref trackerReady, ref bpmReady, ref baseBpm, ref trackBpm, ref currentBpm), token.Decimals);
                default:
                    return token.Literal ?? string.Empty;
            }
        }

        private double EvaluateExpressionTag(
            string expression,
            ref scrMarginTracker tracker,
            ref bool trackerReady,
            ref bool bpmReady,
            ref float baseBpm,
            ref double trackBpm,
            ref double currentBpm)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return 0.0;
            }

            scrMarginTracker localTracker = tracker;
            bool localTrackerReady = trackerReady;
            bool localBpmReady = bpmReady;
            float localBaseBpm = baseBpm;
            double localTrackBpm = trackBpm;
            double localCurrentBpm = currentBpm;

            string normalized = NormalizeExpressionVariables(expression);
            var parser = new OvExpressionParser(normalized, name => ResolveExpressionVariable(name, ref localTracker, ref localTrackerReady, ref localBpmReady, ref localBaseBpm, ref localTrackBpm, ref localCurrentBpm));
            bool evaluated = parser.TryEvaluate(out double value);

            tracker = localTracker;
            trackerReady = localTrackerReady;
            bpmReady = localBpmReady;
            baseBpm = localBaseBpm;
            trackBpm = localTrackBpm;
            currentBpm = localCurrentBpm;

            if (!evaluated || double.IsNaN(value) || double.IsInfinity(value))
            {
                return 0.0;
            }

            return value;
        }

        private static string NormalizeExpressionVariables(string expression)
        {
            if (string.IsNullOrEmpty(expression))
            {
                return string.Empty;
            }

            return Regex.Replace(expression, @"\{([^{}]+)\}", match =>
            {
                string name = match.Groups[1].Value.Trim();
                int colon = name.IndexOf(':');
                if (colon > 0)
                {
                    string suffix = name.Substring(colon + 1);
                    if (suffix.Length > 0 && int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    {
                        name = name.Substring(0, colon);
                    }
                }

                return name;
            });
        }

        private double ResolveExpressionVariable(
            string name,
            ref scrMarginTracker tracker,
            ref bool trackerReady,
            ref bool bpmReady,
            ref float baseBpm,
            ref double trackBpm,
            ref double currentBpm)
        {
            if (string.IsNullOrEmpty(name))
            {
                return 0.0;
            }

            switch (name.Trim().ToLowerInvariant())
            {
                case "fps":
                    return _cachedFps;
                case "kps":
                    return KeyViewerManager.Instance != null ? KeyViewerManager.Instance.CurrentKPS : 0.0;
                case "tot":
                    return Main.Settings != null ? Main.Settings.TotalHits : 0.0;
                case "ttile":
                    return GetTotalTileCount();
                case "atile":
                    return GetPassedTileCount();
                case "x":
                    return GetSpeedMultiplierValue();
                case "cur":
                    return TryGetCurrentClicksPerSecond(out double cps) ? cps : 0.0;
                case "bpm":
                    EnsureBpmValues(ref bpmReady, ref baseBpm, ref trackBpm, ref currentBpm);
                    return baseBpm;
                case "tbpm":
                    EnsureBpmValues(ref bpmReady, ref baseBpm, ref trackBpm, ref currentBpm);
                    return trackBpm;
                case "cbpm":
                    EnsureBpmValues(ref bpmReady, ref baseBpm, ref trackBpm, ref currentBpm);
                    return currentBpm;
                case "acc":
                    EnsureTracker(ref tracker, ref trackerReady);
                    return tracker != null ? tracker.percentAcc * 100.0 : 0.0;
                case "xacc":
                    EnsureTracker(ref tracker, ref trackerReady);
                    return tracker != null ? tracker.percentXAcc * 100.0 : 0.0;
                case "progress":
                    EnsureTracker(ref tracker, ref trackerReady);
                    return tracker != null && scrController.instance != null && scrController.instance.gameworld
                        ? scrController.instance.percentComplete * 100.0
                        : 0.0;
                case "maptime":
                    return TryGetMapTotalSeconds(out double mapTotalSeconds) ? mapTotalSeconds : 0.0;
                case "maptime:p":
                case "maptimep":
                    return TryGetMapPlayedSeconds(out double mapPlayedSeconds) ? mapPlayedSeconds : 0.0;
                case "musictime":
                    return TryGetMusicTotalSeconds(out double musicTotalSeconds) ? musicTotalSeconds : 0.0;
                case "musictime:p":
                case "musictimep":
                    return TryGetMusicPlayedSeconds(out double musicPlayedSeconds) ? musicPlayedSeconds : 0.0;
                case "te":
                    return GetTrackerHitCountValue(ref tracker, ref trackerReady, HitMargin.TooEarly);
                case "ve":
                    return GetTrackerHitCountValue(ref tracker, ref trackerReady, HitMargin.VeryEarly);
                case "ep":
                    return GetTrackerHitCountValue(ref tracker, ref trackerReady, HitMargin.EarlyPerfect);
                case "p":
                    return GetTrackerHitCountValue(ref tracker, ref trackerReady, HitMargin.Perfect);
                case "lp":
                    return GetTrackerHitCountValue(ref tracker, ref trackerReady, HitMargin.LatePerfect);
                case "vl":
                    return GetTrackerHitCountValue(ref tracker, ref trackerReady, HitMargin.VeryLate);
                case "tl":
                    return GetTrackerHitCountValue(ref tracker, ref trackerReady, HitMargin.TooLate);
                case "miss":
                    EnsureTracker(ref tracker, ref trackerReady);
                    return tracker != null ? tracker.GetDeaths() : 0.0;
                case "fm":
                    return GetTrackerHitCountValue(ref tracker, ref trackerReady, HitMargin.FailMiss);
                case "fo":
                    return GetTrackerHitCountValue(ref tracker, ref trackerReady, HitMargin.FailOverload);
                case "combo":
                    return _currentPureCombo;
                case "combo:p":
                case "combop":
                    return _currentPerfectCombo;
                case "xperfect:xpp":
                case "xperfectxpp":
                    return IsXPerfectIntegrationActive() ? XPerfectBridge.XPerfectCount() : 0.0;
                case "xperfect:epp":
                case "xperfectepp":
                    return IsXPerfectIntegrationActive() ? XPerfectBridge.PlusPerfectCount() : 0.0;
                case "xperfect:lpp":
                case "xperfectlpp":
                    return IsXPerfectIntegrationActive() ? XPerfectBridge.MinusPerfectCount() : 0.0;
                default:
                    return 0.0;
            }
        }

        private static double GetSpeedMultiplierValue()
        {
            if (scrConductor.instance != null && scrConductor.instance.song != null)
            {
                return scrConductor.instance.song.pitch;
            }
            if (GCS.speedTrialMode)
            {
                return GCS.currentSpeedTrial;
            }

            return 1.0;
        }

        private static bool IsXPerfectIntegrationActive()
        {
            return Main.Settings != null
                && Main.Settings.XPerfectIntegrationEnabled
                && XPerfectBridge.Active;
        }

        private static void EnsureNow(ref DateTime now, ref bool nowReady)
        {
            if (nowReady) return;
            now = DateTime.Now;
            nowReady = true;
        }

        private static void EnsureTracker(ref scrMarginTracker tracker, ref bool trackerReady)
        {
            if (trackerReady) return;
            trackerReady = true;
            if (scrController.instance != null && scrController.instance.playerOne != null)
            {
                tracker = scrController.instance.playerOne.marginTracker;
            }
        }

        private static string GetTrackerHitCount(ref scrMarginTracker tracker, ref bool trackerReady, HitMargin margin)
        {
            EnsureTracker(ref tracker, ref trackerReady);
            return tracker != null ? tracker.GetHits(margin).ToString(CultureInfo.InvariantCulture) : "0";
        }

        private static double GetTrackerHitCountValue(ref scrMarginTracker tracker, ref bool trackerReady, HitMargin margin)
        {
            EnsureTracker(ref tracker, ref trackerReady);
            return tracker != null ? tracker.GetHits(margin) : 0.0;
        }

        private static void EnsureBpmValues(ref bool bpmReady, ref float baseBpm, ref double trackBpm, ref double currentBpm)
        {
            if (bpmReady) return;
            bpmReady = true;

            if (scrConductor.instance == null)
            {
                baseBpm = 0f;
                trackBpm = 0.0;
                currentBpm = 0.0;
                return;
            }

            float pitch = scrConductor.instance.song.pitch;
            baseBpm = scrConductor.instance.bpm;
            trackBpm = baseBpm * pitch;
            currentBpm = trackBpm;

            if (scrController.instance != null && scrLevelMaker.instance != null)
            {
                int seqID = scrController.instance.currentSeqID;
                if (seqID >= 0 && seqID < scrLevelMaker.instance.listFloors.Count)
                {
                    scrFloor currentFloor = scrLevelMaker.instance.listFloors[seqID];
                    trackBpm = baseBpm * pitch * currentFloor.speed;
                    currentBpm = trackBpm;

                    if (currentFloor.nextfloor != null)
                    {
                        currentBpm = (60.0 / (currentFloor.nextfloor.entryTime - currentFloor.entryTime)) * pitch;
                    }
                }
            }
        }

        private static string BuildOvTextLayoutKey(OverlayerText ovText, string renderedText)
        {
            string text = renderedText ?? string.Empty;
            string fontPath = ovText.FontPath ?? string.Empty;
            return string.Concat(
                fontPath,
                "|", text,
                "|", ovText.FontSize.ToString("R", CultureInfo.InvariantCulture),
                "|", ovText.LetterSpacing.ToString("R", CultureInfo.InvariantCulture),
                "|", ovText.LineHeightOffset.ToString("R", CultureInfo.InvariantCulture));
        }

        private static bool TryGetCurrentFloor(out scrFloor currentFloor)
        {
            currentFloor = null;
            if (scrController.instance == null || scrLevelMaker.instance == null || scrLevelMaker.instance.listFloors == null)
                return false;

            int seqID = scrController.instance.currentSeqID;
            if (seqID < 0 || seqID >= scrLevelMaker.instance.listFloors.Count)
                return false;

            currentFloor = scrLevelMaker.instance.listFloors[seqID];
            return currentFloor != null;
        }

        private static int GetTotalTileCount()
        {
            if (scrLevelMaker.instance == null || scrLevelMaker.instance.listFloors == null)
                return 0;

            return Math.Max(0, scrLevelMaker.instance.listFloors.Count);
        }

        private static int GetPassedTileCount()
        {
            if (scrController.instance == null)
                return 0;

            int total = GetTotalTileCount();
            int currentSeqId = scrController.instance.currentSeqID + 1;
            if (total <= 0)
                return Math.Max(0, currentSeqId);

            return Math.Max(0, Math.Min(total, currentSeqId));
        }

        private static bool TryGetMapTotalSeconds(out double seconds)
        {
            seconds = 0.0;
            if (scrLevelMaker.instance == null || scrLevelMaker.instance.listFloors == null || scrLevelMaker.instance.listFloors.Count == 0)
                return false;

            scrFloor lastFloor = scrLevelMaker.instance.listFloors[scrLevelMaker.instance.listFloors.Count - 1];
            if (lastFloor == null)
                return false;

            seconds = Math.Max(0.0, lastFloor.entryTime);
            return true;
        }

        private static bool TryGetMapPlayedSeconds(out double seconds)
        {
            seconds = 0.0;
            if (scrConductor.instance == null)
                return false;

            seconds = Math.Max(0.0, scrConductor.instance.songposition_minusi);
            if (TryGetMapTotalSeconds(out double total))
                seconds = Math.Min(seconds, total);

            return true;
        }

        private static bool TryGetMusicTotalSeconds(out double seconds)
        {
            seconds = 0.0;
            if (scrConductor.instance == null || scrConductor.instance.song == null || scrConductor.instance.song.clip == null)
                return false;

            seconds = Math.Max(0.0, scrConductor.instance.song.clip.length);
            return true;
        }

        private static bool TryGetMusicPlayedSeconds(out double seconds)
        {
            seconds = 0.0;
            if (scrConductor.instance == null || scrConductor.instance.song == null)
                return false;

            seconds = Math.Max(0.0, scrConductor.instance.song.time);
            if (TryGetMusicTotalSeconds(out double total))
                seconds = Math.Min(seconds, total);

            return true;
        }

        private static bool TryGetCurrentClicksPerSecond(out double cps)
        {
            cps = 0.0;
            if (scrConductor.instance == null || scrConductor.instance.song == null)
                return false;
            if (!TryGetCurrentFloor(out scrFloor currentFloor) || currentFloor.nextfloor == null)
                return false;

            double delta = currentFloor.nextfloor.entryTime - currentFloor.entryTime;
            if (delta <= 0.000001)
                return false;

            cps = scrConductor.instance.song.pitch / delta;
            return true;
        }

        private static string GetJudgeText()
        {
            switch (GCS.difficulty)
            {
                case Difficulty.Lenient:
                    return "\u5BBD\u677E";
                case Difficulty.Strict:
                    return "\u4E25\u683C";
                case Difficulty.Normal:
                default:
                    return "\u666E\u901A";
            }
        }

        private static string FormatPercent(double percent)
        {
            double rounded = Math.Round(percent);
            if (Math.Abs(percent - rounded) < 0.001)
                return rounded.ToString("0", CultureInfo.InvariantCulture) + "%";

            return percent.ToString("0.##", CultureInfo.InvariantCulture) + "%";
        }

        private static string GetCurrentTimingWindowScaleText()
        {
            double scale = 1.0;
            if (TryGetCurrentFloor(out scrFloor currentFloor))
            {
                if (currentFloor.nextfloor != null)
                    scale = currentFloor.nextfloor.marginScale;
                else
                    scale = currentFloor.marginScale;
            }

            return FormatPercent(scale * 100.0);
        }

        public class AnimPlaybackState
        {
            public float CurrentTime = 0f;
            public bool IsPlaying = false;
        }
        
        private System.Collections.Generic.Dictionary<OverlayerAnimation, AnimPlaybackState> _animStates = new System.Collections.Generic.Dictionary<OverlayerAnimation, AnimPlaybackState>();
        private readonly System.Collections.Generic.Dictionary<OverlayerText, string> _ovTextLayoutKeys = new System.Collections.Generic.Dictionary<OverlayerText, string>();
        private readonly System.Collections.Generic.Dictionary<OverlayerText, UnityEngine.Vector2> _ovTextStableSizes = new System.Collections.Generic.Dictionary<OverlayerText, UnityEngine.Vector2>();
        private readonly System.Collections.Generic.List<string> _ovTextRenderIds = new System.Collections.Generic.List<string>();
        private readonly System.Collections.Generic.List<string> _ovTextSelectIds = new System.Collections.Generic.List<string>();
        private readonly System.Collections.Generic.List<OvImageRenderIds> _ovImageRenderIds = new System.Collections.Generic.List<OvImageRenderIds>();
        private readonly System.Collections.Generic.List<OvImageRenderIds> _ovVideoRenderIds = new System.Collections.Generic.List<OvImageRenderIds>();
        private readonly System.Collections.Generic.List<OvProgressBarRenderIds> _ovProgressBarRenderIds = new System.Collections.Generic.List<OvProgressBarRenderIds>();

        private sealed class OvImageRenderIds
        {
            public string Image;
            public string Missing;
            public string MissingOutline;
            public string Select;
        }

        private sealed class OvProgressBarRenderIds
        {
            public string Background;
            public string Fill;
            public string Border;
            public string Select;
        }

        private static string GetIndexedRenderId(System.Collections.Generic.List<string> ids, string prefix, int index)
        {
            while (ids.Count <= index)
            {
                ids.Add(prefix + ids.Count.ToString());
            }
            return ids[index];
        }

        private OvImageRenderIds GetOvImageRenderIds(int index)
        {
            while (_ovImageRenderIds.Count <= index)
            {
                int id = _ovImageRenderIds.Count;
                string prefix = "ov_img_" + id.ToString();
                _ovImageRenderIds.Add(new OvImageRenderIds
                {
                    Image = prefix,
                    Missing = prefix + "_missing",
                    MissingOutline = prefix + "_missing_outline",
                    Select = prefix + "_select"
                });
            }
            return _ovImageRenderIds[index];
        }

        private OvImageRenderIds GetOvVideoRenderIds(int index)
        {
            while (_ovVideoRenderIds.Count <= index)
            {
                int id = _ovVideoRenderIds.Count;
                string prefix = "ov_video_" + id.ToString();
                _ovVideoRenderIds.Add(new OvImageRenderIds
                {
                    Image = prefix,
                    Missing = prefix + "_missing",
                    MissingOutline = prefix + "_missing_outline",
                    Select = prefix + "_select"
                });
            }
            return _ovVideoRenderIds[index];
        }

        private OvProgressBarRenderIds GetOvProgressBarRenderIds(int index)
        {
            while (_ovProgressBarRenderIds.Count <= index)
            {
                int id = _ovProgressBarRenderIds.Count;
                string prefix = "ov_bar_" + id.ToString();
                _ovProgressBarRenderIds.Add(new OvProgressBarRenderIds
                {
                    Background = prefix + "_bg",
                    Fill = prefix + "_fill",
                    Border = prefix + "_border",
                    Select = prefix + "_select"
                });
            }
            return _ovProgressBarRenderIds[index];
        }

        public AnimPlaybackState GetAnimState(OverlayerAnimation anim)
        {
            if (!_animStates.TryGetValue(anim, out var state))
            {
                state = new AnimPlaybackState();
                _animStates[anim] = state;
            }
            return state;
        }

        private bool _anyKeyPressedThisFrame = false;
        private bool _comboIncreasedThisFrame = false;

        private (float x, float y, float sx, float sy, float opacity, float rotation) EvaluateAnimState(OverlayerAnimation anim, float currentTime)
        {
            if (anim.ParsedFrames == null || anim.ParsedFrames.Count == 0) return (0f, 0f, 1f, 1f, 1f, 0f);
            
            var frames = anim.ParsedFrames;
            JsonAnimFrame prev = frames[0];
            JsonAnimFrame next = frames[frames.Count - 1];
            
            if (currentTime <= prev.time) return (prev.x ?? 0f, prev.y ?? 0f, prev.zoomx ?? 1f, prev.zoomy ?? 1f, prev.opacity ?? 1f, prev.rotation ?? 0f);
            if (currentTime >= next.time) return (next.x ?? 0f, next.y ?? 0f, next.zoomx ?? 1f, next.zoomy ?? 1f, next.opacity ?? 1f, next.rotation ?? 0f);
            
            for (int k = 0; k < frames.Count - 1; k++)
            {
                if (currentTime >= frames[k].time && currentTime <= frames[k+1].time)
                {
                    prev = frames[k];
                    next = frames[k+1];
                    break;
                }
            }
            
            float t = (currentTime - prev.time) / (next.time - prev.time);
            float easedT = EasingUtil.EvaluateEasing(t, next.easing);
            
            float x = (prev.x ?? 0f) + ((next.x ?? 0f) - (prev.x ?? 0f)) * easedT;
            float y = (prev.y ?? 0f) + ((next.y ?? 0f) - (prev.y ?? 0f)) * easedT;
            float sx = (prev.zoomx ?? 1f) + ((next.zoomx ?? 1f) - (prev.zoomx ?? 1f)) * easedT;
            float sy = (prev.zoomy ?? 1f) + ((next.zoomy ?? 1f) - (prev.zoomy ?? 1f)) * easedT;
            float opacity = (prev.opacity ?? 1f) + ((next.opacity ?? 1f) - (prev.opacity ?? 1f)) * easedT;
            float rotation = (prev.rotation ?? 0f) + ((next.rotation ?? 0f) - (prev.rotation ?? 0f)) * easedT;
            
            return (x, y, sx, sy, opacity, rotation);
        }

        private void Update()
        {
            if (!Main.IsEnabled || Main.Settings == null || !Main.Settings.OverlayerSystemEnabled)
            {
                return;
            }

            if (Main.Settings.OverlayerOnlyShowPlaying && !Main.IsGamePlaying() && !Main.Settings.OverlayerEditMode)
            {
                return;
            }

            ScanRuntimeInterestFlags();
            if (!_needsHitTracker && !_needsComboTracker && !_hasTextAnimations && !_hasImageAnimations)
            {
                return;
            }

            _anyKeyPressedThisFrame = _hasClickAnimations && Input.anyKeyDown;
            _comboIncreasedThisFrame = false;

            if ((_needsHitTracker || _needsComboTracker || _hasComboAnimations)
                && scrController.instance != null
                && scrController.instance.playerOne != null
                && scrController.instance.playerOne.marginTracker != null)
            {
                var hitMargins = scrController.instance.playerOne.marginTracker.hitMargins;
                int currentHitCount = hitMargins.Count;

                if (currentHitCount < _lastHitCount || (currentHitCount == 0 && _lastHitCount > 0))
                {
                    // Restarted or reset
                    _currentPureCombo = 0;
                    _currentPerfectCombo = 0;
                    MarkRenderDirty();
                }
                else if (currentHitCount > _lastHitCount)
                {
                    for (int i = _lastHitCount; i < currentHitCount; i++)
                    {
                        HitMargin hit = hitMargins[i];
                        if (hit == HitMargin.Perfect || hit == HitMargin.Auto)
                        {
                            _currentPureCombo++;
                            _currentPerfectCombo++;
                            _comboIncreasedThisFrame = true;
                        }
                        else if (hit == HitMargin.EarlyPerfect || hit == HitMargin.LatePerfect)
                        {
                            _currentPureCombo = 0;
                            _currentPerfectCombo++;
                            _comboIncreasedThisFrame = true;
                        }
                        else
                        {
                            _currentPureCombo = 0;
                            _currentPerfectCombo = 0;
                        }
                    }
                    MarkRenderDirty();
                }
                _lastHitCount = currentHitCount;
            }
            else
            {
                if (_lastHitCount != 0 || _currentPureCombo != 0 || _currentPerfectCombo != 0)
                {
                    MarkRenderDirty();
                }
                _lastHitCount = 0;
                _currentPureCombo = 0;
                _currentPerfectCombo = 0;
            }

            // Advance animation frames and trigger check
            if (_hasTextAnimations && Main.Settings.OverlayerTexts != null)
            {
                foreach (var ovText in Main.Settings.OverlayerTexts)
                {
                    if (ovText.Animations == null) continue;
                    foreach (var anim in ovText.Animations)
                    {
                        if (!anim.IsEnabled) continue;
                        
                        var state = GetAnimState(anim);
                        // 移除对 AnimationEditorUI.IsOpen 的依赖，因为我们删了它
                        if (anim.Trigger == AnimationTrigger.OnClick && _anyKeyPressedThisFrame)
                        {
                            state.IsPlaying = true;
                            state.CurrentTime = 0f;
                            MarkRenderDirty();
                        }
                        else if (anim.Trigger == AnimationTrigger.OnComboIncrease && _comboIncreasedThisFrame)
                        {
                            state.IsPlaying = true;
                            state.CurrentTime = 0f;
                            MarkRenderDirty();
                        }

                        if (state.IsPlaying && anim.ParsedFrames != null && anim.ParsedFrames.Count > 0)
                        {
                            float maxTime = anim.ParsedFrames[anim.ParsedFrames.Count - 1].time;
                            state.CurrentTime += UnityEngine.Time.unscaledDeltaTime;
                            MarkRenderDirty();
                            if (state.CurrentTime > maxTime)
                            {
                                state.IsPlaying = false;
                                state.CurrentTime = maxTime;
                            }
                        }
                    }
                }
            }

            if (_hasImageAnimations && Main.Settings.OverlayerImages != null)
            {
                foreach (var ovImg in Main.Settings.OverlayerImages)
                {
                    if (ovImg.Animations == null) continue;
                    foreach (var anim in ovImg.Animations)
                    {
                        if (!anim.IsEnabled) continue;
                        
                        var state = GetAnimState(anim);
                        if (anim.Trigger == AnimationTrigger.OnClick && _anyKeyPressedThisFrame)
                        {
                            state.IsPlaying = true;
                            state.CurrentTime = 0f;
                            MarkRenderDirty();
                        }
                        else if (anim.Trigger == AnimationTrigger.OnComboIncrease && _comboIncreasedThisFrame)
                        {
                            state.IsPlaying = true;
                            state.CurrentTime = 0f;
                            MarkRenderDirty();
                        }

                        if (state.IsPlaying && anim.ParsedFrames != null && anim.ParsedFrames.Count > 0)
                        {
                            float maxTime = anim.ParsedFrames[anim.ParsedFrames.Count - 1].time;
                            state.CurrentTime += UnityEngine.Time.unscaledDeltaTime;
                            MarkRenderDirty();
                            if (state.CurrentTime > maxTime)
                            {
                                state.IsPlaying = false;
                                state.CurrentTime = maxTime;
                            }
                        }
                    }
                }
            }
        }

        public void RenderUI()
        {
            if (!Main.Settings.OverlayerSystemEnabled)
            {
                OverlayerUnityRenderer.HideAll();
                PauseVideoIfNeeded();
                return;
            }

            bool editMode = Main.Settings.OverlayerEditMode;
            if (!editMode) _activeOvAlignLines.Clear();
            if (CheryToolsMenu.IsMenuOpen) _activeOvAlignLines.Clear();
            if (Main.Settings.OverlayerOnlyShowPlaying && !Main.IsGamePlaying() && !editMode)
            {
                OverlayerUnityRenderer.HideAll();
                PauseVideoIfNeeded();
                return;
            }

            OverlayerUnityRenderer.BeginFrame();
            var texts = Main.Settings.OverlayerTexts;
            bool isPlaying = Main.IsGamePlaying();
            bool hasVideoThisFrame = false;

            _fpsTimer += UnityEngine.Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.25f)
            {
                _cachedFps = 1.0f / UnityEngine.Time.unscaledDeltaTime;
                _fpsTimer = 0f;
            }
            for (int i = 0; i < texts.Count; i++)
            {
                var ovText = texts[i];
                if (ovText == null) continue;
                if (!ovText.IsEnabled && !editMode) continue;
                if (!ovText.ShowInGame && isPlaying && !editMode) continue;

                float animOffsetX = 0f;
                float animOffsetY = 0f;
                float animScaleXMult = 1f;
                float animScaleYMult = 1f;

                if (ovText.Animations != null)
                {
                    foreach (var anim in ovText.Animations)
                    {
                        if (!anim.IsEnabled) continue;
                        
                        var state = GetAnimState(anim);
                        if (!state.IsPlaying && state.CurrentTime <= 0f) continue;
                        
                        var evaluated = EvaluateAnimState(anim, state.CurrentTime);
                        
                        animOffsetX += evaluated.x;
                        animOffsetY += evaluated.y;
                        
                        animScaleXMult *= evaluated.sx;
                        animScaleYMult *= evaluated.sy;
                    }
                }

                string rawText = ResolveOvTextTags(ovText, ovText.TextFormat ?? string.Empty);
                
                // Process placeholders
                if (rawText.Contains("{"))
                {
                    if (rawText.Contains("{kps}"))
                    {
                        int kps = 0;
                        if (KeyViewerManager.Instance != null) kps = KeyViewerManager.Instance.CurrentKPS;
                        rawText = rawText.Replace("{kps}", kps.ToString());
                    }
                    if (rawText.Contains("{tot}"))
                    {
                        int tot = Main.Settings.TotalHits;
                        rawText = rawText.Replace("{tot}", tot.ToString());
                    }
                    if (rawText.Contains("{ttile}"))
                    {
                        rawText = rawText.Replace("{ttile}", GetTotalTileCount().ToString(CultureInfo.InvariantCulture));
                    }
                    if (rawText.Contains("{atile}"))
                    {
                        rawText = rawText.Replace("{atile}", GetPassedTileCount().ToString(CultureInfo.InvariantCulture));
                    }
                    if (rawText.Contains("{level}"))
                    {
                        rawText = rawText.Replace("{level}", GetLevelAuthorText());
                    }
                    if (rawText.Contains("{x}"))
                    {
                        rawText = rawText.Replace("{x}", GetSpeedMultiplierText());
                    }
                    if (rawText.Contains("{fps"))
                    {
                        rawText = FpsTagRegex.Replace(rawText, match => {
                            int decimals = GetTagDecimals(match, 0);
                            return FormatNumberTrimZeros(_cachedFps, decimals);
                        });
                    }
                    if (rawText.Contains("{maptime}"))
                    {
                        rawText = rawText.Replace("{maptime}", TryGetMapTotalSeconds(out double mapTotalSeconds) ? FormatDuration(mapTotalSeconds) : "0:00");
                    }
                    if (rawText.Contains("{maptime:p}"))
                    {
                        rawText = rawText.Replace("{maptime:p}", TryGetMapPlayedSeconds(out double mapPlayedSeconds) ? FormatDuration(mapPlayedSeconds) : "0:00");
                    }
                    if (rawText.Contains("{musictime}"))
                    {
                        rawText = rawText.Replace("{musictime}", TryGetMusicTotalSeconds(out double musicTotalSeconds) ? FormatDuration(musicTotalSeconds) : "0:00");
                    }
                    if (rawText.Contains("{musictime:p}"))
                    {
                        rawText = rawText.Replace("{musictime:p}", TryGetMusicPlayedSeconds(out double musicPlayedSeconds) ? FormatDuration(musicPlayedSeconds) : "0:00");
                    }
                    if (rawText.Contains("{cur}"))
                    {
                        rawText = rawText.Replace("{cur}", TryGetCurrentClicksPerSecond(out double cps) ? FormatNumberTrimZeros(cps, 2) : "0");
                    }
                    if (rawText.Contains("{judge}"))
                    {
                        rawText = rawText.Replace("{judge}", GetJudgeText());
                    }
                    if (rawText.Contains("{interval}"))
                    {
                        rawText = rawText.Replace("{interval}", GetCurrentTimingWindowScaleText());
                    }
                    if (rawText.Contains("{date"))
                    {
                        DateTime now = DateTime.Now;
                        if (rawText.Contains("{datey}"))
                            rawText = rawText.Replace("{datey}", now.ToString("yyyy", CultureInfo.InvariantCulture));
                        if (rawText.Contains("{datem}"))
                            rawText = rawText.Replace("{datem}", now.ToString("MM", CultureInfo.InvariantCulture));
                        if (rawText.Contains("{dated}"))
                            rawText = rawText.Replace("{dated}", now.ToString("dd", CultureInfo.InvariantCulture));
                    }
                    if (rawText.Contains("{wtime"))
                    {
                        DateTime now = DateTime.Now;
                        if (rawText.Contains("{wtime12}"))
                            rawText = rawText.Replace("{wtime12}", now.ToString("hh:mm:ss tt", CultureInfo.InvariantCulture));
                        if (rawText.Contains("{wtime}"))
                            rawText = rawText.Replace("{wtime}", now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
                    }
                    if (scrConductor.instance != null)
                    {
                        float pitch = scrConductor.instance.song.pitch;
                        float baseBpm = scrConductor.instance.bpm;
                        double tbpm = baseBpm * pitch;
                        double cbpm = tbpm;

                        if (scrController.instance != null && scrLevelMaker.instance != null)
                        {
                            int seqID = scrController.instance.currentSeqID;
                            if (seqID >= 0 && seqID < scrLevelMaker.instance.listFloors.Count)
                            {
                                scrFloor currentFloor = scrLevelMaker.instance.listFloors[seqID];
                                tbpm = baseBpm * pitch * currentFloor.speed;
                                cbpm = tbpm;
                                
                                if (currentFloor.nextfloor != null)
                                {
                                    cbpm = (60.0 / (currentFloor.nextfloor.entryTime - currentFloor.entryTime)) * pitch;
                                }
                            }
                        }

                        if (rawText.Contains("{bpm"))
                            rawText = BpmTagRegex.Replace(rawText, match => FormatNumberTrimZeros(baseBpm, GetTagDecimals(match, 2)));
                        if (rawText.Contains("{tbpm"))
                            rawText = TrackBpmTagRegex.Replace(rawText, match => FormatNumberTrimZeros(tbpm, GetTagDecimals(match, 2)));
                        if (rawText.Contains("{cbpm"))
                            rawText = CurrentBpmTagRegex.Replace(rawText, match => FormatNumberTrimZeros(cbpm, GetTagDecimals(match, 2)));
                    }
                    
                    scrMarginTracker tracker = null;
                    if (scrController.instance != null && scrController.instance.playerOne != null)
                        tracker = scrController.instance.playerOne.marginTracker;

                    if (rawText.Contains("{te}")) rawText = rawText.Replace("{te}", tracker != null ? tracker.GetHits(HitMargin.TooEarly).ToString() : "0");
                    if (rawText.Contains("{ve}")) rawText = rawText.Replace("{ve}", tracker != null ? tracker.GetHits(HitMargin.VeryEarly).ToString() : "0");
                    if (rawText.Contains("{ep}")) rawText = rawText.Replace("{ep}", tracker != null ? tracker.GetHits(HitMargin.EarlyPerfect).ToString() : "0");
                    if (rawText.Contains("{p}")) rawText = rawText.Replace("{p}", tracker != null ? tracker.GetHits(HitMargin.Perfect).ToString() : "0");
                    if (rawText.Contains("{lp}")) rawText = rawText.Replace("{lp}", tracker != null ? tracker.GetHits(HitMargin.LatePerfect).ToString() : "0");
                    if (rawText.Contains("{vl}")) rawText = rawText.Replace("{vl}", tracker != null ? tracker.GetHits(HitMargin.VeryLate).ToString() : "0");
                    if (rawText.Contains("{tl}")) rawText = rawText.Replace("{tl}", tracker != null ? tracker.GetHits(HitMargin.TooLate).ToString() : "0");
                    if (rawText.Contains("{miss}")) rawText = rawText.Replace("{miss}", tracker != null ? tracker.GetDeaths().ToString() : "0");
                    if (rawText.Contains("{fm}")) rawText = rawText.Replace("{fm}", tracker != null ? tracker.GetHits(HitMargin.FailMiss).ToString() : "0");
                    if (rawText.Contains("{fo}")) rawText = rawText.Replace("{fo}", tracker != null ? tracker.GetHits(HitMargin.FailOverload).ToString() : "0");
                    
                    if (rawText.Contains("{acc"))
                    {
                        rawText = AccTagRegex.Replace(rawText, match => {
                            float acc = tracker != null ? (tracker.percentAcc * 100f) : 0f;
                            int decimals = GetTagDecimals(match, 2);
                            return FormatNumberTrimZeros(acc, decimals);
                        });
                    }
                    if (rawText.Contains("{xacc"))
                    {
                        rawText = XAccTagRegex.Replace(rawText, match => {
                            float acc = tracker != null ? (tracker.percentXAcc * 100f) : 0f;
                            int decimals = GetTagDecimals(match, 2);
                            return FormatNumberTrimZeros(acc, decimals);
                        });
                    }
                    if (rawText.Contains("{progress"))
                    {
                        rawText = ProgressTagRegex.Replace(rawText, match => {
                            int decimals = GetTagDecimals(match, 2);
                            double p = 0;
                            if (tracker != null && scrController.instance != null && scrController.instance.gameworld)
                            {
                                p = scrController.instance.percentComplete * 100.0;
                            }
                            return FormatNumberTrimZeros(p, decimals);
                        });
                    }

                    if (rawText.Contains("{combo}"))
                    {
                        rawText = rawText.Replace("{combo}", _currentPureCombo.ToString());
                    }
                    if (rawText.Contains("{combo:p}"))
                    {
                        rawText = rawText.Replace("{combo:p}", _currentPerfectCombo.ToString());
                    }

                    if (rawText.Contains("{music}"))
                    {
                        rawText = rawText.Replace("{music}", GetMusicText());
                    }
                    if (rawText.Contains("{xperfect:"))
                    {
                        rawText = rawText.Replace("{xperfect:xpp}", IsXPerfectIntegrationActive() ? XPerfectBridge.XPerfectCount().ToString(CultureInfo.InvariantCulture) : "0");
                        rawText = rawText.Replace("{xperfect:epp}", IsXPerfectIntegrationActive() ? XPerfectBridge.PlusPerfectCount().ToString(CultureInfo.InvariantCulture) : "0");
                        rawText = rawText.Replace("{xperfect:lpp}", IsXPerfectIntegrationActive() ? XPerfectBridge.MinusPerfectCount().ToString(CultureInfo.InvariantCulture) : "0");
                    }
                }

                string layoutKey = BuildOvTextLayoutKey(ovText, rawText);
                UnityEngine.Vector2 cachedLayoutSize = UnityEngine.Vector2.zero;
                bool useCachedLayoutSize = _ovTextLayoutKeys.TryGetValue(ovText, out string previousLayoutKey)
                    && string.Equals(previousLayoutKey, layoutKey, StringComparison.Ordinal)
                    && _ovTextStableSizes.TryGetValue(ovText, out cachedLayoutSize);

                string textRenderId = GetIndexedRenderId(_ovTextRenderIds, "ov_text_", i);
                SdfTextRenderer.TextBounds textBounds = SdfTextRenderer.DrawOverlayerText(
                    textRenderId,
                    ovText,
                    rawText,
                    animOffsetX,
                    animOffsetY,
                    animScaleXMult,
                    animScaleYMult,
                    useCachedLayoutSize ? cachedLayoutSize.x : 0f,
                    useCachedLayoutSize ? cachedLayoutSize.y : 0f,
                    useCachedLayoutSize,
                    RenderDepth.ToSortingOrder(ovText.Depth, RenderDepth.SublayerText));

                float contentWindowWidth = Mathf.Max(1f, textBounds.Width);
                float contentWindowHeight = Mathf.Max(1f, textBounds.Height);
                if (!useCachedLayoutSize)
                {
                    float safeScaleX = Mathf.Max(0.001f, animScaleXMult);
                    float safeScaleY = Mathf.Max(0.001f, animScaleYMult);
                    _ovTextLayoutKeys[ovText] = layoutKey;
                    _ovTextStableSizes[ovText] = new UnityEngine.Vector2(contentWindowWidth / safeScaleX, contentWindowHeight / safeScaleY);
                }
                float visualLeft = textBounds.Left;
                float visualTop = textBounds.Top;
                float visualWidth = contentWindowWidth;
                float visualHeight = contentWindowHeight;

                if (editMode && !CheryToolsMenu.IsMenuOpen)
                {
                    System.Numerics.Vector2 screenMousePos = ImGuiController.ScreenMousePos;
                    System.Numerics.Vector2 screenMouseDelta = ImGuiController.ScreenMouseDelta;
                    System.Numerics.Vector2 screenDisplaySize = ImGuiController.ScreenDisplaySize;
                    var hitMin = new System.Numerics.Vector2(visualLeft, visualTop);
                    var hitMax = new System.Numerics.Vector2(visualLeft + visualWidth, visualTop + visualHeight);
                    bool isTextHit = IsPointInRect(screenMousePos, hitMin, hitMax);
                    bool mouseDown = Input.GetMouseButton(0);
                    bool mouseClicked = Input.GetMouseButtonDown(0);

                    if (!mouseDown)
                    {
                        if (_draggingIndex == i)
                        {
                            _draggingIndex = -1;
                            _ovDragTotalDeltaX = 0f;
                            _ovDragTotalDeltaY = 0f;
                            _activeOvAlignLines.Clear();
                        }
                    }
                    else if (_draggingIndex == -1 && _draggingIndexImg == -1 && _draggingIndexBar == -1 && _draggingIndexVideo == -1 && isTextHit && mouseClicked)
                    {
                        _draggingIndex = i;
                        _ovDragStartX = ovText.PositionX;
                        _ovDragStartY = ovText.PositionY;
                        _ovDragTotalDeltaX = 0f;
                        _ovDragTotalDeltaY = 0f;
                        _activeOvAlignLines.Clear();
                    }

                    if (_draggingIndex == i)
                    {
                        var delta = screenMouseDelta;
                        if (delta.X != 0f || delta.Y != 0f)
                        {
                            _ovDragTotalDeltaX += delta.X;
                            _ovDragTotalDeltaY += delta.Y;
                            MoveOvTextWithSnapping(ovText, contentWindowWidth, contentWindowHeight, _ovDragStartX + _ovDragTotalDeltaX, _ovDragStartY + _ovDragTotalDeltaY, screenDisplaySize);
                            Main.RequestSave();
                        }
                    }
                }
                else if (_draggingIndex == i)
                {
                    _draggingIndex = -1;
                    _ovDragTotalDeltaX = 0f;
                    _ovDragTotalDeltaY = 0f;
                }

                if (editMode && !CheryToolsMenu.IsMenuOpen)
                {
                    OverlayerUnityRenderer.DrawOutlineRect(
                        GetIndexedRenderId(_ovTextSelectIds, "ov_text_select_", i),
                        new UnityEngine.Vector2(visualLeft, visualTop),
                        new UnityEngine.Vector2(visualWidth, visualHeight),
                        0xFF00FF00u,
                        2f);
                }

                ovText.LastWidth = contentWindowWidth;
                ovText.LastHeight = contentWindowHeight;
                continue;

            }

            var images = Main.Settings.OverlayerImages;
            for (int i = 0; i < images.Count; i++)
            {
                var ovImg = images[i];
                if (ovImg == null) continue;
                if (!ovImg.IsEnabled && !editMode) continue;
                if (!ovImg.ShowInGame && isPlaying && !editMode) continue;

                RenderOvImageUnity(i, ovImg, editMode);
                continue;
            }

            var videos = Main.Settings.OverlayerVideos;
            if (videos != null)
            {
                for (int i = 0; i < videos.Count; i++)
                {
                    var ovVideo = videos[i];
                    if (ovVideo == null) continue;
                    if (!ovVideo.IsEnabled && !editMode) continue;
                    if (!ovVideo.ShowInGame && isPlaying && !editMode) continue;

                    BeginVideoFrameIfNeeded(ref hasVideoThisFrame);
                    RenderOvVideoUnity(i, ovVideo, editMode);
                }
            }

            var progressBars = Main.Settings.OverlayerProgressBars;
            if (progressBars != null)
            {
                for (int i = 0; i < progressBars.Count; i++)
                {
                    var bar = progressBars[i];
                    if (bar == null) continue;
                    if (!bar.IsEnabled && !editMode) continue;
                    if (!bar.ShowInGame && isPlaying && !editMode) continue;

                    RenderOvProgressBarUnity(i, bar, editMode);
                }
            }

            if (editMode && !CheryToolsMenu.IsMenuOpen)
            {
                DrawActiveOvAlignLines();
            }

            OverlayerUnityRenderer.EndFrame();
            if (hasVideoThisFrame)
            {
                VideoTextureManager.EndFrame("OV");
            }
            else if (_hadVideoLastFrame)
            {
                VideoTextureManager.PauseAll("OV");
            }
            _hadVideoLastFrame = hasVideoThisFrame;
        }

        private void PauseVideoIfNeeded()
        {
            if (!_hadVideoLastFrame) return;
            VideoTextureManager.PauseAll("OV");
            _hadVideoLastFrame = false;
        }

        private static void BeginVideoFrameIfNeeded(ref bool hasVideoThisFrame)
        {
            if (hasVideoThisFrame) return;
            VideoTextureManager.BeginFrame("OV");
            hasVideoThisFrame = true;
        }

        private void RenderOvImageUnity(int index, OverlayerImage ovImg, bool editMode)
        {
            OvImageRenderIds renderIds = GetOvImageRenderIds(index);
            int sortingOrder = RenderDepth.ToSortingOrder(ovImg.Depth, RenderDepth.SublayerGraphic);
            float animOffsetX = 0f;
            float animOffsetY = 0f;
            float animScaleXMult = 1f;
            float animScaleYMult = 1f;
            float animOpacityMult = 1f;
            float animRotationOffset = 0f;

            if (ovImg.Animations != null)
            {
                foreach (var anim in ovImg.Animations)
                {
                    if (!anim.IsEnabled) continue;

                    var state = GetAnimState(anim);
                    if (!state.IsPlaying && state.CurrentTime <= 0f) continue;

                    var evaluated = EvaluateAnimState(anim, state.CurrentTime);
                    animOffsetX += evaluated.x;
                    animOffsetY += evaluated.y;
                    animScaleXMult *= evaluated.sx;
                    animScaleYMult *= evaluated.sy;
                    animOpacityMult *= evaluated.opacity;
                    animRotationOffset += evaluated.rotation;
                }
            }

            Texture2D tex = null;
            int sourceWidth = 0;
            int sourceHeight = 0;
            bool hasTexture = TextureManager.TryGetImageSize(ovImg.ImagePath, out sourceWidth, out sourceHeight);
            if (!hasTexture)
            {
                tex = TextureManager.GetOrCreateTexture2D(ovImg.ImagePath);
                hasTexture = tex != null;
                if (hasTexture)
                {
                    sourceWidth = tex.width;
                    sourceHeight = tex.height;
                }
            }
            if (!hasTexture && !editMode)
            {
                return;
            }

            float boundW;
            float boundH;
            float minXLocal = 0f;
            float minYLocal = 0f;
            float l1x = 0f;
            float l1y = 0f;
            float l2x = 0f;
            float l2y = 0f;
            float l3x = 0f;
            float l3y = 0f;
            float l4x = 0f;
            float l4y = 0f;
            float imgWidth = 0f;
            float imgHeight = 0f;

            if (hasTexture)
            {
                imgWidth = sourceWidth * ovImg.Scale * animScaleXMult;
                imgHeight = sourceHeight * ovImg.Scale * animScaleYMult;
                float rad = (ovImg.Rotation + animRotationOffset) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);
                float hw = imgWidth * 0.5f;
                float hh = imgHeight * 0.5f;

                l1x = (-hw) * cos - (-hh) * sin; l1y = (-hw) * sin + (-hh) * cos;
                l2x = (hw) * cos - (-hh) * sin; l2y = (hw) * sin + (-hh) * cos;
                l3x = (hw) * cos - (hh) * sin; l3y = (hw) * sin + (hh) * cos;
                l4x = (-hw) * cos - (hh) * sin; l4y = (-hw) * sin + (hh) * cos;

                float maxXLocal = Mathf.Max(l1x, Mathf.Max(l2x, Mathf.Max(l3x, l4x)));
                float maxYLocal = Mathf.Max(l1y, Mathf.Max(l2y, Mathf.Max(l3y, l4y)));
                minXLocal = Mathf.Min(l1x, Mathf.Min(l2x, Mathf.Min(l3x, l4x)));
                minYLocal = Mathf.Min(l1y, Mathf.Min(l2y, Mathf.Min(l3y, l4y)));

                boundW = maxXLocal - minXLocal;
                boundH = maxYLocal - minYLocal;
            }
            else
            {
                boundW = Mathf.Max(120f, ovImg.LastWidth);
                boundH = Mathf.Max(48f, ovImg.LastHeight);
            }

            boundW = Mathf.Max(1f, boundW);
            boundH = Mathf.Max(1f, boundH);

            float topLeftX = (ovImg.PositionX + animOffsetX) - ovImg.PivotX * boundW;
            float topLeftY = (ovImg.PositionY + animOffsetY) - ovImg.PivotY * boundH;
            var topLeft = new UnityEngine.Vector2(topLeftX, topLeftY);
            var size = new UnityEngine.Vector2(boundW, boundH);

            if (hasTexture)
            {
                if (tex == null)
                {
                    tex = TextureManager.GetOrCreateTexture2D(ovImg.ImagePath, Mathf.Abs(imgWidth), Mathf.Abs(imgHeight));
                }
                if (tex == null)
                {
                    OverlayerUnityRenderer.DrawFilledRect(renderIds.Missing, topLeft, size, 0x66000000u, 0f, sortingOrder);
                    OverlayerUnityRenderer.DrawOutlineRect(renderIds.MissingOutline, topLeft, size, 0xFF33CCFFu, 2f, 0f, sortingOrder);
                    return;
                }

                float cx = topLeftX - minXLocal;
                float cy = topLeftY - minYLocal;

                OverlayerUnityRenderer.DrawImageQuad(
                    renderIds.Image,
                    tex,
                    topLeft,
                    size,
                    new UnityEngine.Vector2(cx + l1x, cy + l1y),
                    new UnityEngine.Vector2(cx + l2x, cy + l2y),
                    new UnityEngine.Vector2(cx + l3x, cy + l3y),
                    new UnityEngine.Vector2(cx + l4x, cy + l4y),
                    ovImg.Opacity * animOpacityMult,
                    sortingOrder);
            }
            else
            {
                OverlayerUnityRenderer.DrawFilledRect(renderIds.Missing, topLeft, size, 0x66000000u, 0f, sortingOrder);
                OverlayerUnityRenderer.DrawOutlineRect(renderIds.MissingOutline, topLeft, size, 0xFF33CCFFu, 2f, 0f, sortingOrder);
            }

            ovImg.LastWidth = boundW;
            ovImg.LastHeight = boundH;

            bool canEditOverlay = editMode && !CheryToolsMenu.IsMenuOpen;
            if (canEditOverlay)
            {
                System.Numerics.Vector2 screenMousePos = ImGuiController.ScreenMousePos;
                System.Numerics.Vector2 screenMouseDelta = ImGuiController.ScreenMouseDelta;
                System.Numerics.Vector2 screenDisplaySize = ImGuiController.ScreenDisplaySize;
                var hitMin = new System.Numerics.Vector2(topLeftX, topLeftY);
                var hitMax = new System.Numerics.Vector2(topLeftX + boundW, topLeftY + boundH);
                bool isImageHit = IsPointInRect(screenMousePos, hitMin, hitMax);
                bool mouseDown = Input.GetMouseButton(0);
                bool mouseClicked = Input.GetMouseButtonDown(0);

                if (!mouseDown)
                {
                    if (_draggingIndexImg == index)
                    {
                        _draggingIndexImg = -1;
                        _ovDragTotalDeltaX = 0f;
                        _ovDragTotalDeltaY = 0f;
                        _activeOvAlignLines.Clear();
                    }
                }
                else if (_draggingIndexImg == -1 && _draggingIndex == -1 && _draggingIndexBar == -1 && _draggingIndexVideo == -1 && isImageHit && mouseClicked)
                {
                    _draggingIndexImg = index;
                    _ovDragStartX = ovImg.PositionX;
                    _ovDragStartY = ovImg.PositionY;
                    _ovDragTotalDeltaX = 0f;
                    _ovDragTotalDeltaY = 0f;
                    _activeOvAlignLines.Clear();
                }

                if (_draggingIndexImg == index)
                {
                    var delta = screenMouseDelta;
                    if (delta.X != 0f || delta.Y != 0f)
                    {
                        _ovDragTotalDeltaX += delta.X;
                        _ovDragTotalDeltaY += delta.Y;
                        MoveOvImageWithSnapping(ovImg, boundW, boundH, _ovDragStartX + _ovDragTotalDeltaX, _ovDragStartY + _ovDragTotalDeltaY, screenDisplaySize);
                        Main.RequestSave();
                    }
                }

                OverlayerUnityRenderer.DrawOutlineRect(renderIds.Select, topLeft, size, 0xFF00FF00u, 2f);
            }
            else if (_draggingIndexImg == index)
            {
                _draggingIndexImg = -1;
                _ovDragTotalDeltaX = 0f;
                _ovDragTotalDeltaY = 0f;
            }
        }

        private void RenderOvVideoUnity(int index, OverlayerVideo video, bool editMode)
        {
            OvImageRenderIds renderIds = GetOvVideoRenderIds(index);
            int sortingOrder = RenderDepth.ToSortingOrder(video.Depth, RenderDepth.SublayerGraphic);

            float videoWidth = Mathf.Max(1f, video.Width);
            float videoHeight = Mathf.Max(1f, video.Height);
            float rad = video.Rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            float hw = videoWidth * 0.5f;
            float hh = videoHeight * 0.5f;

            float l1x = (-hw) * cos - (-hh) * sin; float l1y = (-hw) * sin + (-hh) * cos;
            float l2x = (hw) * cos - (-hh) * sin; float l2y = (hw) * sin + (-hh) * cos;
            float l3x = (hw) * cos - (hh) * sin; float l3y = (hw) * sin + (hh) * cos;
            float l4x = (-hw) * cos - (hh) * sin; float l4y = (-hw) * sin + (hh) * cos;

            float maxXLocal = Mathf.Max(l1x, Mathf.Max(l2x, Mathf.Max(l3x, l4x)));
            float maxYLocal = Mathf.Max(l1y, Mathf.Max(l2y, Mathf.Max(l3y, l4y)));
            float minXLocal = Mathf.Min(l1x, Mathf.Min(l2x, Mathf.Min(l3x, l4x)));
            float minYLocal = Mathf.Min(l1y, Mathf.Min(l2y, Mathf.Min(l3y, l4y)));
            float boundW = Mathf.Max(1f, maxXLocal - minXLocal);
            float boundH = Mathf.Max(1f, maxYLocal - minYLocal);
            float topLeftX = video.PositionX - video.PivotX * boundW;
            float topLeftY = video.PositionY - video.PivotY * boundH;
            var topLeft = new UnityEngine.Vector2(topLeftX, topLeftY);
            var size = new UnityEngine.Vector2(boundW, boundH);

            Texture texture = VideoTextureManager.GetOrCreateVideoTexture(
                "OV",
                renderIds.Image,
                video.VideoPath,
                true,
                Mathf.CeilToInt(videoWidth),
                Mathf.CeilToInt(videoHeight),
                video.IsEnabled);

            if (texture != null)
            {
                float cx = topLeftX - minXLocal;
                float cy = topLeftY - minYLocal;
                OverlayerUnityRenderer.DrawImageQuad(
                    renderIds.Image,
                    texture,
                    topLeft,
                    size,
                    new UnityEngine.Vector2(cx + l1x, cy + l1y),
                    new UnityEngine.Vector2(cx + l2x, cy + l2y),
                    new UnityEngine.Vector2(cx + l3x, cy + l3y),
                    new UnityEngine.Vector2(cx + l4x, cy + l4y),
                    video.Opacity,
                    sortingOrder);
            }
            else if (editMode)
            {
                OverlayerUnityRenderer.DrawFilledRect(renderIds.Missing, topLeft, size, 0x66000000u, 0f, sortingOrder);
                OverlayerUnityRenderer.DrawOutlineRect(renderIds.MissingOutline, topLeft, size, 0xFF33CCFFu, 2f, 0f, sortingOrder);
            }

            video.LastWidth = boundW;
            video.LastHeight = boundH;

            bool canEditOverlay = editMode && !CheryToolsMenu.IsMenuOpen;
            if (canEditOverlay)
            {
                System.Numerics.Vector2 screenMousePos = ImGuiController.ScreenMousePos;
                System.Numerics.Vector2 screenMouseDelta = ImGuiController.ScreenMouseDelta;
                System.Numerics.Vector2 screenDisplaySize = ImGuiController.ScreenDisplaySize;
                var hitMin = new System.Numerics.Vector2(topLeftX, topLeftY);
                var hitMax = new System.Numerics.Vector2(topLeftX + boundW, topLeftY + boundH);
                bool isVideoHit = IsPointInRect(screenMousePos, hitMin, hitMax);
                bool mouseDown = Input.GetMouseButton(0);
                bool mouseClicked = Input.GetMouseButtonDown(0);

                if (!mouseDown)
                {
                    if (_draggingIndexVideo == index)
                    {
                        _draggingIndexVideo = -1;
                        _ovDragTotalDeltaX = 0f;
                        _ovDragTotalDeltaY = 0f;
                        _activeOvAlignLines.Clear();
                    }
                }
                else if (_draggingIndexVideo == -1 && _draggingIndexImg == -1 && _draggingIndex == -1 && _draggingIndexBar == -1 && isVideoHit && mouseClicked)
                {
                    _draggingIndexVideo = index;
                    _ovDragStartX = video.PositionX;
                    _ovDragStartY = video.PositionY;
                    _ovDragTotalDeltaX = 0f;
                    _ovDragTotalDeltaY = 0f;
                    _activeOvAlignLines.Clear();
                }

                if (_draggingIndexVideo == index)
                {
                    var delta = screenMouseDelta;
                    if (delta.X != 0f || delta.Y != 0f)
                    {
                        _ovDragTotalDeltaX += delta.X;
                        _ovDragTotalDeltaY += delta.Y;
                        MoveOvVideoWithSnapping(video, boundW, boundH, _ovDragStartX + _ovDragTotalDeltaX, _ovDragStartY + _ovDragTotalDeltaY, screenDisplaySize);
                        Main.RequestSave();
                    }
                }

                OverlayerUnityRenderer.DrawOutlineRect(renderIds.Select, topLeft, size, 0xFF00FF00u, 2f);
            }
            else if (_draggingIndexVideo == index)
            {
                _draggingIndexVideo = -1;
                _ovDragTotalDeltaX = 0f;
                _ovDragTotalDeltaY = 0f;
            }
        }

        private void RenderOvProgressBarUnity(int index, OverlayerProgressBar bar, bool editMode)
        {
            OvProgressBarRenderIds renderIds = GetOvProgressBarRenderIds(index);
            int sortingOrder = RenderDepth.ToSortingOrder(bar.Depth, RenderDepth.SublayerGraphic);

            float width = Mathf.Max(1f, bar.Width);
            float height = Mathf.Max(1f, bar.Height);
            float topLeftX = bar.PositionX - bar.PivotX * width;
            float topLeftY = bar.PositionY - bar.PivotY * height;
            var topLeft = new UnityEngine.Vector2(topLeftX, topLeftY);
            var size = new UnityEngine.Vector2(width, height);
            float opacity = Mathf.Clamp01(bar.Opacity);
            float cornerRadius = Mathf.Min(Mathf.Max(0f, bar.CornerRadius), Mathf.Min(width, height) * 0.5f);

            DrawProgressBarShadow(index, bar, topLeft, size, opacity, cornerRadius, sortingOrder);

            if (HasVisibleColor(bar.BackgroundColor, opacity))
            {
                OverlayerUnityRenderer.DrawFilledRect(renderIds.Background, topLeft, size, PackColor(bar.BackgroundColor, opacity), cornerRadius, sortingOrder);
            }

            double min = ResolveProgressValue(bar.MinSource);
            double max = ResolveProgressValue(bar.MaxSource);
            double value = ResolveProgressValue(bar.ValueSource);
            double normalized;
            double range = max - min;
            if (Math.Abs(range) <= 0.000001)
            {
                normalized = value >= max ? 1.0 : 0.0;
            }
            else
            {
                normalized = (value - min) / range;
            }

            if (bar.ClampValue)
            {
                normalized = Math.Max(0.0, Math.Min(1.0, normalized));
            }
            if (bar.Reverse)
            {
                normalized = 1.0 - normalized;
            }

            float fillAmount = Mathf.Clamp01((float)normalized);
            if (fillAmount > 0.0001f && HasVisibleProgressFillColor(bar, fillAmount, opacity))
            {
                var fillTopLeft = topLeft;
                var fillSize = size;
                switch (bar.FillDirection)
                {
                    case OverlayerProgressFillDirection.RightToLeft:
                        fillSize.x = width * fillAmount;
                        fillTopLeft.x = topLeftX + width - fillSize.x;
                        break;
                    case OverlayerProgressFillDirection.BottomToTop:
                        fillSize.y = height * fillAmount;
                        fillTopLeft.y = topLeftY + height - fillSize.y;
                        break;
                    case OverlayerProgressFillDirection.TopToBottom:
                        fillSize.y = height * fillAmount;
                        break;
                    case OverlayerProgressFillDirection.LeftToRight:
                    default:
                        fillSize.x = width * fillAmount;
                        break;
                }

                if (fillSize.x > 0.5f && fillSize.y > 0.5f)
                {
                    float fillRadius = Mathf.Min(cornerRadius, Mathf.Min(fillSize.x, fillSize.y) * 0.5f);
                    OverlayerUnityRenderer.DrawFilledRect(renderIds.Fill, fillTopLeft, fillSize, PackProgressFillColor(bar, fillAmount, opacity), fillRadius, sortingOrder);
                }
            }

            if (bar.BorderThickness > 0f && HasVisibleColor(bar.BorderColor, opacity))
            {
                OverlayerUnityRenderer.DrawOutlineRect(renderIds.Border, topLeft, size, PackColor(bar.BorderColor, opacity), bar.BorderThickness, cornerRadius, sortingOrder);
            }

            bar.LastWidth = width;
            bar.LastHeight = height;

            bool canEditOverlay = editMode && !CheryToolsMenu.IsMenuOpen;
            if (canEditOverlay)
            {
                System.Numerics.Vector2 screenMousePos = ImGuiController.ScreenMousePos;
                System.Numerics.Vector2 screenMouseDelta = ImGuiController.ScreenMouseDelta;
                System.Numerics.Vector2 screenDisplaySize = ImGuiController.ScreenDisplaySize;
                var hitMin = new System.Numerics.Vector2(topLeftX, topLeftY);
                var hitMax = new System.Numerics.Vector2(topLeftX + width, topLeftY + height);
                bool isBarHit = IsPointInRect(screenMousePos, hitMin, hitMax);
                bool mouseDown = Input.GetMouseButton(0);
                bool mouseClicked = Input.GetMouseButtonDown(0);

                if (!mouseDown)
                {
                    if (_draggingIndexBar == index)
                    {
                        _draggingIndexBar = -1;
                        _ovDragTotalDeltaX = 0f;
                        _ovDragTotalDeltaY = 0f;
                        _activeOvAlignLines.Clear();
                    }
                }
                else if (_draggingIndexBar == -1 && _draggingIndex == -1 && _draggingIndexImg == -1 && _draggingIndexVideo == -1 && isBarHit && mouseClicked)
                {
                    _draggingIndexBar = index;
                    _ovDragStartX = bar.PositionX;
                    _ovDragStartY = bar.PositionY;
                    _ovDragTotalDeltaX = 0f;
                    _ovDragTotalDeltaY = 0f;
                    _activeOvAlignLines.Clear();
                }

                if (_draggingIndexBar == index)
                {
                    var delta = screenMouseDelta;
                    if (delta.X != 0f || delta.Y != 0f)
                    {
                        _ovDragTotalDeltaX += delta.X;
                        _ovDragTotalDeltaY += delta.Y;
                        MoveOvProgressBarWithSnapping(bar, width, height, _ovDragStartX + _ovDragTotalDeltaX, _ovDragStartY + _ovDragTotalDeltaY, screenDisplaySize);
                        Main.RequestSave();
                    }
                }

                OverlayerUnityRenderer.DrawOutlineRect(renderIds.Select, topLeft, size, 0xFF00FF00u, 2f);
            }
            else if (_draggingIndexBar == index)
            {
                _draggingIndexBar = -1;
                _ovDragTotalDeltaX = 0f;
                _ovDragTotalDeltaY = 0f;
            }
        }

        private void DrawProgressBarShadow(int index, OverlayerProgressBar bar, UnityEngine.Vector2 topLeft, UnityEngine.Vector2 size, float opacity, float cornerRadius, int sortingOrder)
        {
            if (bar == null || !bar.EnableShadow || !HasVisibleColor(bar.ShadowColor, opacity))
            {
                return;
            }

            float shadowX = bar.ShadowOffset != null && bar.ShadowOffset.Length > 0 ? bar.ShadowOffset[0] : 0f;
            float shadowY = bar.ShadowOffset != null && bar.ShadowOffset.Length > 1 ? bar.ShadowOffset[1] : 0f;
            float softness = Mathf.Max(0f, bar.ShadowSoftness);
            var shadowTopLeft = new UnityEngine.Vector2(topLeft.x + shadowX, topLeft.y + shadowY);

            if (softness <= 0.01f)
            {
                OverlayerUnityRenderer.DrawFilledRect("ov_bar_shadow_" + index.ToString(), shadowTopLeft, size, PackColor(bar.ShadowColor, opacity), cornerRadius, sortingOrder);
                return;
            }

            int steps = Mathf.Clamp(Mathf.CeilToInt(softness / 1.5f), 3, 16);
            for (int i = steps; i >= 1; i--)
            {
                float t = (i - 0.5f) / steps;
                float expand = softness * t;
                float alphaScale = 0.42f * Mathf.Pow(1f - t, 1.7f) / Mathf.Max(1f, steps * 0.45f);
                var layerTopLeft = new UnityEngine.Vector2(shadowTopLeft.x - expand, shadowTopLeft.y - expand);
                var layerSize = new UnityEngine.Vector2(size.x + expand * 2f, size.y + expand * 2f);
                OverlayerUnityRenderer.DrawFilledRect(
                    "ov_bar_shadow_" + index.ToString() + "_" + i.ToString(),
                    layerTopLeft,
                    layerSize,
                    PackColor(bar.ShadowColor, opacity * Mathf.Clamp01(alphaScale)),
                    cornerRadius + expand,
                    sortingOrder);
            }

            OverlayerUnityRenderer.DrawFilledRect(
                "ov_bar_shadow_core_" + index.ToString(),
                shadowTopLeft,
                size,
                PackColor(bar.ShadowColor, opacity * 0.48f),
                cornerRadius,
                sortingOrder);
        }

        private double ResolveProgressValue(OverlayerProgressValueSource source)
        {
            if (source == null)
            {
                return 0.0;
            }

            scrMarginTracker tracker = null;
            if (scrController.instance != null && scrController.instance.playerOne != null)
                tracker = scrController.instance.playerOne.marginTracker;

            switch (source.Kind)
            {
                case OverlayerProgressValueKind.Progress:
                    if (tracker != null && scrController.instance != null && scrController.instance.gameworld)
                        return scrController.instance.percentComplete * 100.0;
                    return 0.0;
                case OverlayerProgressValueKind.Accuracy:
                    return tracker != null ? tracker.percentAcc * 100.0 : 0.0;
                case OverlayerProgressValueKind.XAccuracy:
                    return tracker != null ? tracker.percentXAcc * 100.0 : 0.0;
                case OverlayerProgressValueKind.Kps:
                    return KeyViewerManager.Instance != null ? KeyViewerManager.Instance.CurrentKPS : 0.0;
                case OverlayerProgressValueKind.CurrentClicksPerSecond:
                    return TryGetCurrentClicksPerSecond(out double cps) ? cps : 0.0;
                case OverlayerProgressValueKind.MapPlayedTime:
                    return TryGetMapPlayedSeconds(out double mapPlayedSeconds) ? mapPlayedSeconds : 0.0;
                case OverlayerProgressValueKind.MapTotalTime:
                    return TryGetMapTotalSeconds(out double mapTotalSeconds) ? mapTotalSeconds : 0.0;
                case OverlayerProgressValueKind.MusicPlayedTime:
                    return TryGetMusicPlayedSeconds(out double musicPlayedSeconds) ? musicPlayedSeconds : 0.0;
                case OverlayerProgressValueKind.MusicTotalTime:
                    return TryGetMusicTotalSeconds(out double musicTotalSeconds) ? musicTotalSeconds : 0.0;
                case OverlayerProgressValueKind.PureCombo:
                    return _currentPureCombo;
                case OverlayerProgressValueKind.PerfectCombo:
                    return _currentPerfectCombo;
                case OverlayerProgressValueKind.Miss:
                    return tracker != null ? tracker.GetDeaths() : 0.0;
                case OverlayerProgressValueKind.FailMiss:
                    return tracker != null ? tracker.GetHits(HitMargin.FailMiss) : 0.0;
                case OverlayerProgressValueKind.FailOverload:
                    return tracker != null ? tracker.GetHits(HitMargin.FailOverload) : 0.0;
                case OverlayerProgressValueKind.Constant:
                default:
                    return source.Constant;
            }
        }

        private static bool HasVisibleProgressFillColor(OverlayerProgressBar bar, float fillAmount, float opacity)
        {
            if (bar == null || !bar.EnableFillGradient)
            {
                return bar != null && HasVisibleColor(bar.FillColor, opacity);
            }

            float[] start = bar.FillGradientStartColor;
            float[] end = bar.FillGradientEndColor;
            if (start == null || start.Length < 4 || end == null || end.Length < 4)
            {
                return HasVisibleColor(bar.FillColor, opacity);
            }

            float t = Mathf.Clamp01(fillAmount);
            return Mathf.Lerp(start[3], end[3], t) * opacity > 0.001f;
        }

        private static uint PackProgressFillColor(OverlayerProgressBar bar, float fillAmount, float opacity)
        {
            if (bar == null || !bar.EnableFillGradient)
            {
                return PackColor(bar != null ? bar.FillColor : null, opacity);
            }

            float[] start = bar.FillGradientStartColor;
            float[] end = bar.FillGradientEndColor;
            if (start == null || start.Length < 4 || end == null || end.Length < 4)
            {
                return PackColor(bar.FillColor, opacity);
            }

            float t = Mathf.Clamp01(fillAmount);
            int r = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(start[0], end[0], t) * 255f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(start[1], end[1], t) * 255f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(start[2], end[2], t) * 255f), 0, 255);
            int a = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(start[3], end[3], t) * Mathf.Clamp01(opacity) * 255f), 0, 255);
            return (uint)(r | (g << 8) | (b << 16) | (a << 24));
        }

        private static bool HasVisibleColor(float[] color, float opacity)
        {
            return color != null && color.Length >= 4 && color[3] * opacity > 0.001f;
        }

        private static uint PackColor(float[] color, float opacity)
        {
            if (color == null || color.Length < 4)
            {
                return 0u;
            }

            int r = Mathf.Clamp(Mathf.RoundToInt(color[0] * 255f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(color[1] * 255f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(color[2] * 255f), 0, 255);
            int a = Mathf.Clamp(Mathf.RoundToInt(color[3] * Mathf.Clamp01(opacity) * 255f), 0, 255);
            return (uint)(r | (g << 8) | (b << 16) | (a << 24));
        }

        private void MoveOvTextWithSnapping(OverlayerText ovText, float width, float height, float targetX, float targetY, System.Numerics.Vector2 displaySize)
        {
            var snapped = CalculateOvSnappedPosition(
                targetX,
                targetY,
                ovText.PivotX,
                ovText.PivotY,
                width,
                height,
                displaySize,
                ovText,
                null,
                null,
                null);

            ovText.PositionX = snapped.X;
            ovText.PositionY = snapped.Y;
        }

        private void MoveOvImageWithSnapping(OverlayerImage ovImg, float width, float height, float targetX, float targetY, System.Numerics.Vector2 displaySize)
        {
            var snapped = CalculateOvSnappedPosition(
                targetX,
                targetY,
                ovImg.PivotX,
                ovImg.PivotY,
                width,
                height,
                displaySize,
                null,
                ovImg,
                null,
                null);

            ovImg.PositionX = snapped.X;
            ovImg.PositionY = snapped.Y;
        }

        private void MoveOvVideoWithSnapping(OverlayerVideo video, float width, float height, float targetX, float targetY, System.Numerics.Vector2 displaySize)
        {
            var snapped = CalculateOvSnappedPosition(
                targetX,
                targetY,
                video.PivotX,
                video.PivotY,
                width,
                height,
                displaySize,
                null,
                null,
                video,
                null);

            video.PositionX = snapped.X;
            video.PositionY = snapped.Y;
        }

        private void MoveOvProgressBarWithSnapping(OverlayerProgressBar bar, float width, float height, float targetX, float targetY, System.Numerics.Vector2 displaySize)
        {
            var snapped = CalculateOvSnappedPosition(
                targetX,
                targetY,
                bar.PivotX,
                bar.PivotY,
                width,
                height,
                displaySize,
                null,
                null,
                null,
                bar);

            bar.PositionX = snapped.X;
            bar.PositionY = snapped.Y;
        }

        private System.Numerics.Vector2 CalculateOvSnappedPosition(float targetX, float targetY, float pivotX, float pivotY, float width, float height, System.Numerics.Vector2 displaySize, OverlayerText ignoreText, OverlayerImage ignoreImage, OverlayerVideo ignoreVideo, OverlayerProgressBar ignoreProgressBar)
        {
            _activeOvAlignLines.Clear();
            if (IsOvSnapDisabled())
            {
                return new System.Numerics.Vector2(targetX, targetY);
            }

            var xRefs = new System.Collections.Generic.List<OvSnapCandidate>();
            var yRefs = new System.Collections.Generic.List<OvSnapCandidate>();

            AddOvSnapCandidate(xRefs, 0f, 0f, displaySize.Y);
            AddOvSnapCandidate(xRefs, displaySize.X * 0.5f, 0f, displaySize.Y);
            AddOvSnapCandidate(xRefs, displaySize.X, 0f, displaySize.Y);
            AddOvSnapCandidate(yRefs, 0f, 0f, displaySize.X);
            AddOvSnapCandidate(yRefs, displaySize.Y * 0.5f, 0f, displaySize.X);
            AddOvSnapCandidate(yRefs, displaySize.Y, 0f, displaySize.X);

            AddOvComponentSnapCandidates(xRefs, yRefs, ignoreText, ignoreImage, ignoreVideo, ignoreProgressBar);

            float left = targetX - pivotX * width;
            float top = targetY - pivotY * height;
            float[] itemX = new float[] { left, left + width * 0.5f, left + width };
            float[] itemY = new float[] { top, top + height * 0.5f, top + height };

            bool hasCorrX = false;
            bool hasCorrY = false;
            float bestCorrX = 0f;
            float bestCorrY = 0f;
            float bestAbsX = OvSnapThreshold;
            float bestAbsY = OvSnapThreshold;
            OvSnapCandidate bestXRef = default(OvSnapCandidate);
            OvSnapCandidate bestYRef = default(OvSnapCandidate);

            foreach (var candidate in xRefs)
            {
                for (int i = 0; i < itemX.Length; i++)
                {
                    float correction = candidate.Value - itemX[i];
                    float abs = Mathf.Abs(correction);
                    if (abs <= bestAbsX)
                    {
                        hasCorrX = true;
                        bestAbsX = abs;
                        bestCorrX = correction;
                        bestXRef = candidate;
                    }
                }
            }

            foreach (var candidate in yRefs)
            {
                for (int i = 0; i < itemY.Length; i++)
                {
                    float correction = candidate.Value - itemY[i];
                    float abs = Mathf.Abs(correction);
                    if (abs <= bestAbsY)
                    {
                        hasCorrY = true;
                        bestAbsY = abs;
                        bestCorrY = correction;
                        bestYRef = candidate;
                    }
                }
            }

            if (hasCorrX) targetX += bestCorrX;
            if (hasCorrY) targetY += bestCorrY;

            float snappedLeft = targetX - pivotX * width;
            float snappedTop = targetY - pivotY * height;
            if (hasCorrX)
            {
                bool fullHeightRef = bestXRef.MinLimit <= 0.001f && bestXRef.MaxLimit >= displaySize.Y - 0.001f;
                float minLimit = fullHeightRef ? snappedTop - 48f : Mathf.Min(snappedTop, bestXRef.MinLimit);
                float maxLimit = fullHeightRef ? snappedTop + height + 48f : Mathf.Max(snappedTop + height, bestXRef.MaxLimit);
                _activeOvAlignLines.Add(new OvAlignLine
                {
                    IsVertical = true,
                    Coord = bestXRef.Value,
                    MinLimit = minLimit,
                    MaxLimit = maxLimit
                });
            }
            if (hasCorrY)
            {
                bool fullWidthRef = bestYRef.MinLimit <= 0.001f && bestYRef.MaxLimit >= displaySize.X - 0.001f;
                float minLimit = fullWidthRef ? snappedLeft - 48f : Mathf.Min(snappedLeft, bestYRef.MinLimit);
                float maxLimit = fullWidthRef ? snappedLeft + width + 48f : Mathf.Max(snappedLeft + width, bestYRef.MaxLimit);
                _activeOvAlignLines.Add(new OvAlignLine
                {
                    IsVertical = false,
                    Coord = bestYRef.Value,
                    MinLimit = minLimit,
                    MaxLimit = maxLimit
                });
            }

            return new System.Numerics.Vector2(targetX, targetY);
        }

        private bool IsOvSnapDisabled()
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        private void AddOvComponentSnapCandidates(System.Collections.Generic.List<OvSnapCandidate> xRefs, System.Collections.Generic.List<OvSnapCandidate> yRefs, OverlayerText ignoreText, OverlayerImage ignoreImage, OverlayerVideo ignoreVideo, OverlayerProgressBar ignoreProgressBar)
        {
            if (Main.Settings == null) return;

            if (Main.Settings.OverlayerTexts != null)
            {
                foreach (var text in Main.Settings.OverlayerTexts)
                {
                    if (object.ReferenceEquals(text, ignoreText)) continue;
                    AddOvRectSnapCandidates(xRefs, yRefs, text.PositionX, text.PositionY, text.PivotX, text.PivotY, text.LastWidth, text.LastHeight);
                }
            }

            if (Main.Settings.OverlayerImages != null)
            {
                foreach (var image in Main.Settings.OverlayerImages)
                {
                    if (object.ReferenceEquals(image, ignoreImage)) continue;
                    AddOvRectSnapCandidates(xRefs, yRefs, image.PositionX, image.PositionY, image.PivotX, image.PivotY, image.LastWidth, image.LastHeight);
                }
            }

            if (Main.Settings.OverlayerVideos != null)
            {
                foreach (var video in Main.Settings.OverlayerVideos)
                {
                    if (video == null || object.ReferenceEquals(video, ignoreVideo)) continue;
                    AddOvRectSnapCandidates(xRefs, yRefs, video.PositionX, video.PositionY, video.PivotX, video.PivotY, video.LastWidth, video.LastHeight);
                }
            }

            if (Main.Settings.OverlayerProgressBars != null)
            {
                foreach (var bar in Main.Settings.OverlayerProgressBars)
                {
                    if (bar == null || object.ReferenceEquals(bar, ignoreProgressBar)) continue;
                    AddOvRectSnapCandidates(xRefs, yRefs, bar.PositionX, bar.PositionY, bar.PivotX, bar.PivotY, bar.LastWidth, bar.LastHeight);
                }
            }
        }

        private void AddOvRectSnapCandidates(System.Collections.Generic.List<OvSnapCandidate> xRefs, System.Collections.Generic.List<OvSnapCandidate> yRefs, float positionX, float positionY, float pivotX, float pivotY, float width, float height)
        {
            width = Mathf.Max(1f, width);
            height = Mathf.Max(1f, height);

            float left = positionX - pivotX * width;
            float top = positionY - pivotY * height;
            float right = left + width;
            float bottom = top + height;

            AddOvSnapCandidate(xRefs, left, top, bottom);
            AddOvSnapCandidate(xRefs, left + width * 0.5f, top, bottom);
            AddOvSnapCandidate(xRefs, right, top, bottom);

            AddOvSnapCandidate(yRefs, top, left, right);
            AddOvSnapCandidate(yRefs, top + height * 0.5f, left, right);
            AddOvSnapCandidate(yRefs, bottom, left, right);
        }

        private void AddOvSnapCandidate(System.Collections.Generic.List<OvSnapCandidate> refs, float value, float minLimit, float maxLimit)
        {
            refs.Add(new OvSnapCandidate
            {
                Value = value,
                MinLimit = minLimit,
                MaxLimit = maxLimit
            });
        }

        private void DrawActiveOvAlignLines()
        {
            if (_activeOvAlignLines.Count == 0) return;

            const uint color = 0xF52DC7FFu;
            const float thickness = 1.5f;
            int lineIndex = 0;
            foreach (var line in _activeOvAlignLines)
            {
                if (line.IsVertical)
                {
                    OverlayerUnityRenderer.DrawFilledRect(
                        $"ov_snap_v_{lineIndex}",
                        new UnityEngine.Vector2(line.Coord - thickness * 0.5f, line.MinLimit),
                        new UnityEngine.Vector2(thickness, Mathf.Max(1f, line.MaxLimit - line.MinLimit)),
                        color);
                }
                else
                {
                    OverlayerUnityRenderer.DrawFilledRect(
                        $"ov_snap_h_{lineIndex}",
                        new UnityEngine.Vector2(line.MinLimit, line.Coord - thickness * 0.5f),
                        new UnityEngine.Vector2(Mathf.Max(1f, line.MaxLimit - line.MinLimit), thickness),
                        color);
                }
                lineIndex++;
            }
        }

        private float CalculateRichTextWidth(string currentLine, float letterSpacing, float baseFontSize, OverlayerText ovText, float animScaleXMult)
        {
            float totalWidth = 0f;
            var segments = RichTextParser.Parse(currentLine, new System.Numerics.Vector4(1,1,1,1));
            
            float safeBaseFontSize = (baseFontSize > 0) ? baseFontSize : 100f;

            foreach (var seg in segments)
            {
                float targetSize = baseFontSize;
                if (seg.HasSizeTag)
                {
                    if (seg.SizeValue > 0)
                    {
                        targetSize = seg.SizeValue;
                    }
                    else if (seg.SizeValue < 0)
                    {
                        targetSize = -seg.SizeValue * baseFontSize;
                    }
                }
                
                // Select font and base size for this segment
                bool hasCustomFont = TryGetCustomFont(ovText.FontPath, false, out ImFontPtr customFont);
                bool hasCustomLargeFont = TryGetCustomFont(ovText.FontPath, true, out ImFontPtr customLargeFont);
                
                ImFontPtr segFont;
                float segFontBaseSize;
                
                if (targetSize > 72f)
                {
                    if (hasCustomLargeFont)
                    {
                        segFont = customLargeFont;
                        segFontBaseSize = 128.0f;
                    }
                    else if (hasCustomFont)
                    {
                        segFont = customFont;
                        segFontBaseSize = 48.0f;
                    }
                    else
                    {
                        segFont = ImGuiController.DefaultLargeFont;
                        segFontBaseSize = 128.0f;
                    }
                }
                else
                {
                    if (hasCustomFont)
                    {
                        segFont = customFont;
                        segFontBaseSize = 48.0f;
                    }
                    else
                    {
                        segFont = ImGuiController.DefaultHighResFont;
                        segFontBaseSize = 48.0f;
                    }
                }
                
                float segScale = (targetSize / segFontBaseSize) * animScaleXMult;
                
                ImGui.PushFont(segFont);
                try
                {
                    if (letterSpacing == 0f)
                    {
                        totalWidth += ImGui.CalcTextSize(seg.RenderText).X * segScale;
                    }
                    else
                    {
                        for (int i = 0; i < seg.RenderText.Length; i++)
                        {
                            totalWidth += ImGui.CalcTextSize(seg.RenderText[i].ToString()).X * segScale;
                            if (i < seg.RenderText.Length - 1) totalWidth += letterSpacing * segScale;
                        }
                    }
                }
                finally
                {
                    ImGui.PopFont();
                }
            }
            return totalWidth;
        }

        private float CalcRawTextWidth(string text, float letterSpacing)
        {
            if (letterSpacing == 0f) return ImGui.CalcTextSize(text).X;
            float w = 0;
            for (int i = 0; i < text.Length; i++)
            {
                w += ImGui.CalcTextSize(text[i].ToString()).X;
                if (i < text.Length - 1) w += letterSpacing;
            }
            return w;
        }

        private void RenderRawText(string text, float letterSpacing, System.Numerics.Vector4 color, bool outlineEnabled, System.Numerics.Vector4 outlineColor, float outlineThickness)
        {
            var drawList = ImGui.GetWindowDrawList();
            var font = ImGui.GetFont();
            float fontSize = ImGui.GetFontSize();
            uint textColor = ImGui.ColorConvertFloat4ToU32(color);
            uint outlineColorU32 = ImGui.ColorConvertFloat4ToU32(outlineColor);

            if (letterSpacing == 0f)
            {
                TextStyleRenderer.AddText(drawList, font, fontSize, ImGui.GetCursorScreenPos(), textColor, text, outlineEnabled, outlineColorU32, outlineThickness);
                ImGui.Dummy(font.CalcTextSizeA(fontSize, float.MaxValue, 0f, text));
                ImGui.SameLine(0, 0);
                return;
            }

            for (int i = 0; i < text.Length; i++)
            {
                string character = text[i].ToString();
                TextStyleRenderer.AddText(drawList, font, fontSize, ImGui.GetCursorScreenPos(), textColor, character, outlineEnabled, outlineColorU32, outlineThickness);
                ImGui.Dummy(font.CalcTextSizeA(fontSize, float.MaxValue, 0f, character));
                if (i < text.Length - 1)
                {
                    ImGui.SameLine(0, letterSpacing);
                }
            }
            ImGui.SameLine(0, 0);
        }

        private void RenderRichTextLine(string[] lines, System.Numerics.Vector4 defaultColor, int alignment, float maxLineWidth, float[] lineRenderWidths, float letterSpacing, float lineHeightOffset, bool isShadow, float scale, float baseFontSize, float xOffset, float windowWidth, OverlayerText ovText, float animScaleXMult)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                string currentLine = lines[i];
                bool isFirstSegmentOnLine = true;
                
                float initialCursorY = ImGui.GetCursorPosY();

                float thisLineWidth = lineRenderWidths[i];
                float pad = ImGui.GetStyle().WindowPadding.X;

                float startX = pad + xOffset;

                if (alignment == 1) // Center
                {
                    startX = (windowWidth - thisLineWidth) / 2.0f + xOffset;
                }
                else if (alignment == 2) // Right
                {
                    startX = windowWidth - thisLineWidth - pad + xOffset;
                }

                ImGui.SetCursorPosX(startX);
                isFirstSegmentOnLine = true;

                var segments = RichTextParser.Parse(currentLine, defaultColor);
                float safeBaseFontSize = (baseFontSize > 0) ? baseFontSize : 100f;

                foreach (var seg in segments)
                {
                    if (!isFirstSegmentOnLine) ImGui.SameLine(0, 0);
                    
                    float targetSize = baseFontSize;
                    if (seg.HasSizeTag && seg.SizeValue > 0)
                    {
                        targetSize = seg.SizeValue;
                    }
                    else if (seg.HasSizeTag && seg.SizeValue < 0)
                    {
                        targetSize = -seg.SizeValue * baseFontSize;
                    }

                    // Select font and base size for this segment
                    bool hasCustomFont = TryGetCustomFont(ovText.FontPath, false, out ImFontPtr customFont);
                    bool hasCustomLargeFont = TryGetCustomFont(ovText.FontPath, true, out ImFontPtr customLargeFont);
                    
                    ImFontPtr segFont;
                    float segFontBaseSize;
                    
                    if (targetSize > 72f)
                    {
                        if (hasCustomLargeFont)
                        {
                            segFont = customLargeFont;
                            segFontBaseSize = 128.0f;
                        }
                        else if (hasCustomFont)
                        {
                            segFont = customFont;
                            segFontBaseSize = 48.0f;
                        }
                        else
                        {
                            segFont = ImGuiController.DefaultLargeFont;
                            segFontBaseSize = 128.0f;
                        }
                    }
                    else
                    {
                        if (hasCustomFont)
                        {
                            segFont = customFont;
                            segFontBaseSize = 48.0f;
                        }
                        else
                        {
                            segFont = ImGuiController.DefaultHighResFont;
                            segFontBaseSize = 48.0f;
                        }
                    }

                    float segScale = (targetSize / segFontBaseSize) * animScaleXMult;

                    ImGui.PushFont(segFont);
                    try
                    {
                        ImGui.SetWindowFontScale(segScale);
                        System.Numerics.Vector4 c = isShadow
                            ? defaultColor
                            : (seg.HasColorTag
                                ? seg.Color
                                : TextStyleRenderer.ColorArrayToVector4(ovText.TextColor, defaultColor));
                        bool outlineEnabled = !isShadow && ovText.EnableOutline;
                        System.Numerics.Vector4 outlineColor = TextStyleRenderer.ColorArrayToVector4(ovText.OutlineColor, new System.Numerics.Vector4(0f, 0f, 0f, 1f));
                        RenderRawText(seg.RenderText, letterSpacing, c, outlineEnabled, outlineColor, ovText.OutlineThickness);
                        isFirstSegmentOnLine = false;
                    }
                    finally
                    {
                        ImGui.PopFont();
                        ImGui.SetWindowFontScale(scale); // Restore window scale
                    }
                }
                
                ImGui.SetCursorPosY(initialCursorY + ImGui.GetFontSize() + lineHeightOffset);
                ImGui.SetCursorPosX(ImGui.GetStyle().WindowPadding.X);
                ImGui.Dummy(new System.Numerics.Vector2(0, 0));
            }
        }

        private System.Numerics.Vector4 ParseHexColor(string hex, System.Numerics.Vector4 fallback)
        {
            if (hex.Length == 6 || hex.Length == 8)
            {
                try
                {
                    float r = System.Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
                    float g = System.Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
                    float b = System.Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
                    float a = 1f;
                    if (hex.Length == 8)
                        a = System.Convert.ToInt32(hex.Substring(6, 2), 16) / 255f;
                    return new System.Numerics.Vector4(r, g, b, a);
                }
                catch { return fallback; }
            }
            return fallback;
        }
    }
}
