using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ADOFAI;
using CheryTools.Sonnet.Shared;
using UnityEngine;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.Overlayer.Preview
{
    internal sealed class OvTagResolver
    {
        private enum TagFormat
        {
            Number,
            Duration,
            Percent,
            Text
        }

        // One parsed segment of a format string: either literal text or a "{tag[:decimals]}" reference.
        // Formats are parsed once and cached; each tag part memoizes its last resolved value per frame
        // and its last formatted string, so steady values cost no allocation.
        private sealed class TagPart
        {
            internal string Literal;
            internal string Key;
            internal int Decimals = -1; // -1 when the tag has no ":N" suffix
            internal string Raw;
            internal int Frame = -1;
            internal bool Resolved;
            internal double Numeric;
            internal int ResolvedDecimals;
            internal TagFormat Format;
            internal string ValueText;
            internal string Formatted;
            internal double FormattedNumeric;
            internal int FormattedDecimals;
            internal TagFormat FormattedKind;
        }

        // Incrementally maintained combo/score statistics. hitMargins only grows during a run, so new
        // entries are folded in instead of rescanning the whole list every refresh.
        private sealed class MarginStats
        {
            internal object ListRef;
            internal int Processed;
            internal int LastValue = -1;
            internal int Total = -1;
            internal int Frame = -1;
            internal int PerfectCombo;
            internal int PureCombo;
            internal double Weighted;
        }

        // {combo} (every non-breaking hit) was removed in favor of {allperfectcombo}. Saved configs and
        // imported packages are rewritten on normalize so they keep showing a combo.
        internal static string MigrateLegacyTags(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf("{combo", StringComparison.Ordinal) < 0) return value;
            return value.Replace("{combo}", "{allperfectcombo}").Replace("{combo:", "{allperfectcombo:");
        }

        private static readonly string[] MusicSeparators = { " - ", " — ", " – " };
        private static readonly Regex RichTextPattern = new Regex("<.*?>", RegexOptions.Compiled);
        private const float SessionTextLifetime = 1f;
        // Release version from Directory.Build.props (CheryToolsVersion), stamped into this assembly.
        private static readonly string ProductVersion =
            ((AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(typeof(OvTagResolver).Assembly,
                typeof(AssemblyInformationalVersionAttribute)))?.InformationalVersion ?? string.Empty;

        private readonly StringBuilder _builder = new StringBuilder(256);
        private readonly System.Collections.Generic.Dictionary<string, TagPart[]> _formatCache =
            new System.Collections.Generic.Dictionary<string, TagPart[]>(StringComparer.Ordinal);
        private readonly System.Collections.Generic.Dictionary<string, TagPart> _numericCache =
            new System.Collections.Generic.Dictionary<string, TagPart>(StringComparer.Ordinal);
        private readonly System.Collections.Generic.Dictionary<string, string> _sessionText =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
        private readonly MarginStats _margins = new MarginStats();
        private float _sessionTextExpires;
        private long _clockSecond = long.MinValue;
        private string _clock24 = string.Empty;
        private string _clock12 = string.Empty;
        private float _fpsSampleStarted;
        private float _fpsSum;
        private int _fpsSamples;
        private float _fps;
        private float _minFps;
        private float _maxFps;
        private int _lastControllerId;
        private object _lastFloorsRef;
        private object _lastFirstFloorRef;
        private int _lastLevelFloorsCount = -1;
        private bool _checkpointsScanned;
        private readonly System.Collections.Generic.List<int> _checkpointSequenceIds = new System.Collections.Generic.List<int>();
        private int _lastAttempts;
        private double _playedMapTime;
        private double _playedMusicTime;
        private static MethodInfo _legacyTimingMethod;
        private static bool _legacyTimingResolved;

        internal void Tick()
        {
            RefreshSessionStats();
            if (Time.unscaledTime >= _sessionTextExpires)
            {
                _sessionText.Clear();
                _sessionTextExpires = Time.unscaledTime + SessionTextLifetime;
            }
            float delta = SonnetClock.DeltaTime;
            if (delta > 0.000001f && delta < 1f)
            {
                _fpsSum += 1f / delta;
                _fpsSamples++;
            }
            float now = SonnetClock.Now;
            if (_fpsSampleStarted <= 0f) _fpsSampleStarted = now;
            if (now - _fpsSampleStarted < 0.25f) return;
            if (_fpsSamples > 0) _fps = _fpsSum / _fpsSamples;
            if (_fps > 0f)
            {
                if (_minFps <= 0f || _fps < _minFps) _minFps = _fps;
                if (_fps > _maxFps) _maxFps = _fps;
            }
            _fpsSampleStarted = now;
            _fpsSum = 0f;
            _fpsSamples = 0;
        }

        internal string ResolveText(string format)
        {
            if (string.IsNullOrEmpty(format)) return string.Empty;
            _builder.Clear();
            AppendResolved(_builder, format);
            return _builder.ToString();
        }

        // Appends the resolved format to target without creating intermediate strings for literals,
        // unknown tags, or tag values that did not change since they were last formatted.
        internal void AppendResolved(StringBuilder target, string format)
        {
            if (string.IsNullOrEmpty(format)) return;
            TagPart[] parts = GetParts(format);
            for (int i = 0; i < parts.Length; i++)
            {
                TagPart part = parts[i];
                if (part.Literal != null)
                {
                    target.Append(part.Literal);
                    continue;
                }

                string value = ResolvePartText(part);
                target.Append(value ?? part.Raw);
            }
        }

        // Returns the numeric value of a format that consists of exactly one numeric tag, rounded the
        // same way the rendered text is. Used by number-format animation nodes instead of parsing the
        // rendered string back into a double.
        internal bool TryResolveSingleNumber(string format, out double value)
        {
            value = 0d;
            if (string.IsNullOrEmpty(format)) return false;
            TagPart[] parts = GetParts(format);
            if (parts.Length != 1 || parts[0].Literal != null) return false;
            TagPart part = parts[0];
            if (!ResolvePart(part) || part.Format != TagFormat.Number) return false;
            double numeric = double.IsNaN(part.Numeric) || double.IsInfinity(part.Numeric) ? 0d : part.Numeric;
            value = Math.Round(numeric, Math.Max(0, part.ResolvedDecimals), MidpointRounding.AwayFromZero);
            return true;
        }

        internal double ResolveNumeric(string tag)
        {
            string source = tag ?? string.Empty;
            if (!_numericCache.TryGetValue(source, out TagPart part))
            {
                string body = source.Trim();
                if (body.Length >= 2 && body[0] == '{' && body[body.Length - 1] == '}')
                    body = body.Substring(1, body.Length - 2).Trim();
                part = ParseTag(body, source);
                _numericCache[source] = part;
            }

            return part != null && ResolvePart(part) ? part.Numeric : 0d;
        }

        private TagPart[] GetParts(string format)
        {
            if (_formatCache.TryGetValue(format, out TagPart[] cached)) return cached;
            var parts = new System.Collections.Generic.List<TagPart>();
            int cursor = 0;
            while (cursor < format.Length)
            {
                int open = format.IndexOf('{', cursor);
                int close = open < 0 ? -1 : format.IndexOf('}', open + 1);
                if (open < 0 || close < 0)
                {
                    parts.Add(new TagPart { Literal = format.Substring(cursor) });
                    break;
                }

                if (open > cursor) parts.Add(new TagPart { Literal = format.Substring(cursor, open - cursor) });
                string raw = format.Substring(open, close - open + 1);
                string body = format.Substring(open + 1, close - open - 1).Trim();
                parts.Add(ParseTag(body, raw) ?? new TagPart { Literal = raw });
                cursor = close + 1;
            }

            TagPart[] result = parts.ToArray();
            if (_formatCache.Count > 512) _formatCache.Clear();
            _formatCache[format] = result;
            return result;
        }

        private static TagPart ParseTag(string body, string raw)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            string name = body;
            int decimals = -1;
            int colon = body.LastIndexOf(':');
            if (colon > 0 && int.TryParse(body.Substring(colon + 1), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int parsedDecimals))
            {
                name = body.Substring(0, colon);
                decimals = Mathf.Clamp(parsedDecimals, 0, 8);
            }

            return new TagPart { Key = name.Trim().ToLowerInvariant(), Decimals = decimals, Raw = raw };
        }

        private bool ResolvePart(TagPart part)
        {
            int frame = Time.frameCount;
            if (part.Frame == frame) return part.Resolved;
            part.Frame = frame;
            int decimals = part.Decimals;
            part.Resolved = TryResolve(part.Key, ref decimals, out double numeric, out string text, out TagFormat format);
            part.Numeric = numeric;
            part.ResolvedDecimals = decimals;
            part.ValueText = text;
            part.Format = text != null ? TagFormat.Text : format;
            return part.Resolved;
        }

        private string ResolvePartText(TagPart part)
        {
            if (!ResolvePart(part)) return null;
            if (part.Format == TagFormat.Text) return part.ValueText ?? string.Empty;
            int decimals = part.ResolvedDecimals;
            if (part.Formatted != null && part.FormattedKind == part.Format
                && part.FormattedDecimals == decimals && part.FormattedNumeric.Equals(part.Numeric))
                return part.Formatted;

            string formatted;
            switch (part.Format)
            {
                case TagFormat.Duration: formatted = FormatDuration(part.Numeric); break;
                case TagFormat.Percent: formatted = decimals < 0 ? FormatPercent(part.Numeric) : FormatNumber(part.Numeric, decimals) + "%"; break;
                default: formatted = FormatNumber(part.Numeric, decimals); break;
            }

            part.FormattedKind = part.Format;
            part.FormattedDecimals = decimals;
            part.FormattedNumeric = part.Numeric;
            part.Formatted = formatted;
            return formatted;
        }

        private bool TryResolve(string key, ref int decimals, out double numeric, out string text, out TagFormat format)
        {
            numeric = 0d;
            text = null;
            format = TagFormat.Number;
            try
            {
                switch (key)
                {
                    case "fps": numeric = _fps; decimals = Math.Max(decimals, 0); return true;
                    case "minfps": numeric = _minFps; return true;
                    case "maxfps": numeric = _maxFps; return true;
                    case "ttile": numeric = GetTotalTiles(); return true;
                    case "atile": numeric = GetPassedTiles(); return true;
                    case "level": text = GetSessionText("level"); return true;
                    case "maptime": numeric = GetMapTime(false); format = TagFormat.Duration; return true;
                    case "maptime:p":
                    case "maptimep": numeric = GetPlayedTime(false); format = TagFormat.Duration; return true;
                    case "musictime": numeric = GetMusicTime(false); format = TagFormat.Duration; return true;
                    case "musictime:p":
                    case "musictimep": numeric = GetPlayedTime(true); format = TagFormat.Duration; return true;
                    case "cur": numeric = GetClicksPerSecond(); return true;
                    case "judge": text = GetJudgementMode(); return true;
                    case "interval": numeric = GetTimingWindowPercent(); format = TagFormat.Percent; return true;
                    case "datey": numeric = DateTime.Now.Year; return true;
                    case "datem": numeric = DateTime.Now.Month; return true;
                    case "dated": numeric = DateTime.Now.Day; return true;
                    case "wtime": text = GetClockText(false); return true;
                    case "wtime12": text = GetClockText(true); return true;
                    case "gameversion": text = Application.version ?? string.Empty; return true;
                    case "ctversion":
                    case "cherytoolsversion": text = ProductVersion; return true;
                    case "year": numeric = DateTime.Now.Year; return true;
                    case "month": numeric = DateTime.Now.Month; return true;
                    case "day": numeric = DateTime.Now.Day; return true;
                    case "worldtime": text = GetClockText(false); return true;
                    case "worldtime12": text = GetClockText(true); return true;
                    case "progress":
                        numeric = scrController.instance != null && scrController.instance.gameworld
                            ? scrController.instance.percentComplete * 100d : 0d;
                        DefaultDecimals(ref decimals, 2);
                        return true;
                    case "x":
                        numeric = scrConductor.instance != null && scrConductor.instance.song != null
                            ? scrConductor.instance.song.pitch : 1d;
                        if (scrConductor.instance == null && GCS.speedTrialMode) numeric = GCS.currentSpeedTrial;
                        DefaultDecimals(ref decimals, 2);
                        return true;
                    case "bpm":
                        numeric = GetBaseBpm();
                        DefaultDecimals(ref decimals, 2);
                        return true;
                    case "tbpm":
                    case "cbpm":
                        numeric = key == "tbpm" ? GetTrackBpm() : GetCurrentRealBpm();
                        DefaultDecimals(ref decimals, 2);
                        return true;
                    case "timing": numeric = GetLastHitTiming(); return true;
                    case "acc": return TrackerNumber(tracker => tracker.percentAcc * 100d, ref numeric, ref decimals);
                    case "xacc": return TrackerNumber(tracker => tracker.percentXAcc * 100d, ref numeric, ref decimals);
                    case "te": return TrackerHits(SonnetJudgement.TooEarly, ref numeric);
                    case "ve": return TrackerHits(SonnetJudgement.VeryEarly, ref numeric);
                    case "ep": return TrackerHits(SonnetJudgement.EarlyPerfect, ref numeric);
                    case "ap": numeric = GetAllPerfectHits(); return true;
                    case "-p":
                    case "p-": return TrackerHits(SonnetJudgement.PerfectMinus, ref numeric);
                    case "xp": return TrackerHits(SonnetJudgement.Perfect, ref numeric);
                    case "+p":
                    case "p+": return TrackerHits(SonnetJudgement.PerfectPlus, ref numeric);
                    case "lp": return TrackerHits(SonnetJudgement.LatePerfect, ref numeric);
                    case "vl": return TrackerHits(SonnetJudgement.VeryLate, ref numeric);
                    case "tl": return TrackerHits(SonnetJudgement.TooLate, ref numeric);
                    case "mp": return TrackerHits(SonnetJudgement.Multipress, ref numeric);
                    case "fm": return TrackerHits(SonnetJudgement.FailMiss, ref numeric);
                    case "fo": return TrackerHits(SonnetJudgement.FailOverload, ref numeric);
                    case "op": return TrackerHits(SonnetJudgement.OverPress, ref numeric);
                    case "ms": return TrackerHits(SonnetJudgement.Midspin, ref numeric);
                    case "ff": return TrackerHits(SonnetJudgement.FailedFloor, ref numeric);
                    // Matches the results screen, which counts misses only (GetDeaths adds overloads).
                    case "miss": return TrackerHits(SonnetJudgement.FailMiss, ref numeric);
                    case "combop":
                    case "perfectcombo":
                    case "prefectcombo":
                    case "allperfectcombo":
                    case "allprefectcombo": numeric = UpdateMargins().PerfectCombo; return true;
                    case "xcombo":
                    case "xperfectcombo":
                    case "xprefectcombo": numeric = UpdateMargins().PureCombo; return true;
                    case "score": numeric = GetScore(); return true;
                    case "music": text = GetSessionText("music"); return true;
                    case "artist": text = GetSessionText("artist"); return true;
                    case "title": text = GetSessionText("title"); return true;
                    case "cherysui": text = "超级卡哇伊的CherySui酱~~~"; return true;
                    case "cherysui:ct": text = "关注ct喵，关注ct谢谢喵"; return true;
                    case "attempts": numeric = _lastAttempts; return true;
                    case "checkpointused": numeric = Math.Max(0, scrController.checkpointsUsed); return true;
                    case "curcheckpoint": numeric = GetCurrentPassedCheckpoints(); return true;
                    case "totalcheckpoint": numeric = _checkpointSequenceIds.Count; return true;
                    case "totalplaytime": numeric = GetPlayedTime(true); format = TagFormat.Duration; return true;
                    default: return false;
                }
            }
            catch { return true; }
        }

        private static bool TrackerHits(SonnetJudgement kind, ref double value)
        {
            value = SonnetJudgements.Count(GetTracker(), kind);
            return true;
        }

        private static double GetAllPerfectHits()
        {
            scrMarginTracker tracker = GetTracker();
            return SonnetJudgements.Count(tracker, SonnetJudgement.Perfect)
                + SonnetJudgements.Count(tracker, SonnetJudgement.PerfectMinus)
                + SonnetJudgements.Count(tracker, SonnetJudgement.PerfectPlus);
        }

        // A tag's own default precision, used only when the format did not give ":N"; an explicit
        // ":0" or ":1" must still win over the default.
        private static void DefaultDecimals(ref int decimals, int fallback)
        {
            if (decimals < 0) decimals = fallback;
        }

        private static bool TrackerNumber(Func<scrMarginTracker, double> resolver, ref double value, ref int decimals)
        {
            scrMarginTracker tracker = GetTracker();
            value = tracker != null ? resolver(tracker) : 0d;
            DefaultDecimals(ref decimals, 2);
            return true;
        }

        private static scrMarginTracker GetTracker()
        {
            try
            {
                scrController controller = scrController.instance ?? ADOBase.controller;
                return controller?.playerOne != null ? controller.playerOne.marginTracker : null;
            }
            catch { return null; }
        }

        private void RefreshSessionStats()
        {
            try
            {
                scrController controller = scrController.instance;
                int controllerId = controller != null ? controller.GetInstanceID() : 0;
                var floors = scrLevelMaker.instance?.listFloors;
                object firstFloor = (floors != null && floors.Count > 0) ? floors[0] : null;

                bool levelReset = controllerId != _lastControllerId
                    || !ReferenceEquals(floors, _lastFloorsRef)
                    || !ReferenceEquals(firstFloor, _lastFirstFloorRef)
                    || (_lastLevelFloorsCount != (floors?.Count ?? -1));

                if (levelReset)
                {
                    _lastControllerId = controllerId;
                    _lastFloorsRef = floors;
                    _lastFirstFloorRef = firstFloor;
                    _lastLevelFloorsCount = floors?.Count ?? -1;
                    _checkpointsScanned = false;
                    _checkpointSequenceIds.Clear();
                    _sessionText.Clear();
                    _lastAttempts = ReadAttempts();
                }
                else if (_lastAttempts <= 0 && controller != null && controller.setupComplete)
                {
                    _lastAttempts = ReadAttempts();
                }

                if (controller == null || floors == null) return;

                if (!_checkpointsScanned && controller.setupComplete)
                {
                    _checkpointSequenceIds.Clear();
                    var unique = new System.Collections.Generic.HashSet<int>();
                    for (int i = 0; i < floors.Count; i++)
                    {
                        scrFloor floor = floors[i];
                        if (floor == null) continue;
                        ffxCheckpoint checkpoint = floor.GetComponent<ffxCheckpoint>();
                        int id = checkpoint != null ? floor.seqID + checkpoint.checkpointTileOffset : -1;
                        if (id >= 0 && unique.Add(id)) _checkpointSequenceIds.Add(id);
                    }
                    _checkpointSequenceIds.Sort();
                    _checkpointsScanned = true;
                }
            }
            catch { }
        }

        private double GetCurrentPassedCheckpoints()
        {
            try
            {
                scrController controller = scrController.instance;
                if (controller == null || _checkpointSequenceIds.Count == 0) return 0d;
                int current = controller.currentSeqID;
                if (current < 0) return 0d;
                int count = 0;
                for (int i = 0; i < _checkpointSequenceIds.Count; i++)
                {
                    if (_checkpointSequenceIds[i] <= current) count++;
                    else break;
                }
                return count;
            }
            catch { return 0d; }
        }

        private static int ReadAttempts()
        {
            try
            {
                if (ADOBase.isOfficialLevel && !string.IsNullOrEmpty(scrController.currentWorldString)
                    && WorldData.dict != null
                    && WorldData.dict.TryGetValue(scrController.currentWorldString, out WorldData world)
                    && world != null)
                    return Math.Max(0, Persistence.GetWorldAttempts(world.index));
                scnGame custom = scnGame.instance;
                if (custom != null && custom.levelData != null && !string.IsNullOrEmpty(custom.levelData.Hash))
                    return Math.Max(0, Persistence.GetCustomWorldAttempts(custom.levelData.Hash));
            }
            catch { }
            return 0;
        }

        private static double GetLastHitTiming()
        {
            EnsureLegacyTimingMethod();
            if (_legacyTimingMethod == null) return 0d;
            try
            {
                return Convert.ToDouble(_legacyTimingMethod.Invoke(null, null), CultureInfo.InvariantCulture);
            }
            catch { }
            return 0d;
        }

        private static void EnsureLegacyTimingMethod()
        {
            if (_legacyTimingResolved) return;
            _legacyTimingResolved = true;
            try
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type manager = assembly.GetType("CheryTools.OverlayerManager", false);
                    MethodInfo method = manager?.GetMethod("GetLastHitTimingMs", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (method != null)
                    {
                        _legacyTimingMethod = method;
                        return;
                    }
                }
            }
            catch { }
        }

        private static string FormatNumber(double value, int decimals)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) value = 0d;
            if (decimals <= 0) return Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
            return value.ToString("0." + new string('#', decimals), CultureInfo.InvariantCulture);
        }

        private static string FormatPercent(double value)
        {
            double rounded = Math.Round(value);
            string number = Math.Abs(value - rounded) < 0.001d
                ? rounded.ToString("0", CultureInfo.InvariantCulture)
                : value.ToString("0.##", CultureInfo.InvariantCulture);
            return number + "%";
        }

        private static string FormatDuration(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d) seconds = 0d;
            int total = Math.Max(0, (int)Math.Round(seconds, MidpointRounding.AwayFromZero));
            int hours = total / 3600;
            int minutes = (total / 60) % 60;
            int secs = total % 60;
            return hours > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", hours, minutes, secs)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", minutes, secs);
        }

        private static int GetTotalTiles()
        {
            try { return scrLevelMaker.instance?.listFloors != null ? Math.Max(0, scrLevelMaker.instance.listFloors.Count) : 0; }
            catch { return 0; }
        }

        private static int GetPassedTiles()
        {
            try
            {
                int total = GetTotalTiles();
                int passed = scrController.instance != null ? scrController.instance.currentSeqID + 1 : 0;
                return total > 0 ? Math.Max(0, Math.Min(total, passed)) : Math.Max(0, passed);
            }
            catch { return 0; }
        }

        private static string GetLevelAuthor()
        {
            try
            {
                string value = ADOBase.customLevel?.levelData?.author;
                if (string.IsNullOrEmpty(value) && scnGame.instance?.levelData != null)
                    value = scnGame.instance.levelData.author;
                if (string.IsNullOrEmpty(value) && ADOBase.isOfficialLevel && !string.IsNullOrEmpty(scrController.currentWorldString)
                    && WorldData.dict != null && WorldData.dict.TryGetValue(scrController.currentWorldString, out WorldData world)
                    && world != null)
                {
                    value = world.levelCredits?.people;
                }
                return StripRichText(value);
            }
            catch { return string.Empty; }
        }

        private static double GetBaseBpm()
        {
            try
            {
                return scrConductor.instance != null && IsFinitePositive(scrConductor.instance.bpm)
                    ? scrConductor.instance.bpm : 0d;
            }
            catch { }
            return 0d;
        }

        private static double GetTrackBpm()
        {
            try
            {
                scrConductor conductor = scrConductor.instance;
                if (conductor == null) return 0d;
                double pitch = conductor.song != null && IsFinitePositive(conductor.song.pitch)
                    ? conductor.song.pitch : 1d;
                double speed = GetCurrentFloorSpeed();
                return GetBaseBpm() * pitch * speed;
            }
            catch { return 0d; }
        }

        private static double GetCurrentRealBpm()
        {
            try
            {
                scrConductor conductor = scrConductor.instance;
                if (conductor == null) return 0d;
                double pitch = conductor.song != null && IsFinitePositive(conductor.song.pitch)
                    ? conductor.song.pitch : 1d;
                scrFloor floor = GetCurrentFloor();
                if (floor != null && floor.nextfloor != null)
                {
                    double interval = floor.nextfloor.entryTime - floor.entryTime;
                    if (interval > 0.000001d && !double.IsNaN(interval) && !double.IsInfinity(interval))
                    {
                        double value = 60d / interval * pitch;
                        if (value > 0d && !double.IsNaN(value) && !double.IsInfinity(value)) return value;
                    }
                }
                return GetTrackBpm();
            }
            catch { return 0d; }
        }

        private static scrFloor GetCurrentFloor()
        {
            try
            {
                scrController controller = scrController.instance;
                scrLevelMaker maker = scrLevelMaker.instance;
                if (controller == null || maker == null || maker.listFloors == null) return null;
                int index = controller.currentSeqID;
                return index >= 0 && index < maker.listFloors.Count ? maker.listFloors[index] : null;
            }
            catch { return null; }
        }

        private static double GetCurrentFloorSpeed()
        {
            scrFloor floor = GetCurrentFloor();
            return floor != null && IsFinitePositive(floor.speed) ? floor.speed : 1d;
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static double GetMapTime(bool played)
        {
            try
            {
                if (played) return Math.Max(0d, scrConductor.instance?.songposition_minusi ?? 0d);
                if (scrLevelMaker.instance?.listFloors == null || scrLevelMaker.instance.listFloors.Count == 0) return 0d;
                return Math.Max(0d, scrLevelMaker.instance.listFloors[scrLevelMaker.instance.listFloors.Count - 1].entryTime);
            }
            catch { return 0d; }
        }

        // The conductor keeps ticking after a fail or a win (death animation, results) and in menus, so
        // played time is only sampled during a live run. Terminal states hold the last sample; outside a
        // level, or in the editor's edit mode, it reads zero. The conductor stays enabled and its song
        // keeps running in edit mode until the first playtest, so edit mode is read from the editor.
        private double GetPlayedTime(bool music)
        {
            scrController controller = scrController.instance;
            scrConductor conductor = scrConductor.instance;
            if (controller == null || !controller.gameworld || conductor == null || !conductor.isActiveAndEnabled
                || IsEditorEditMode())
            {
                _playedMapTime = 0d;
                _playedMusicTime = 0d;
                return 0d;
            }

            States state = controller.state;
            if (state != States.Fail && state != States.Fail2 && state != States.Won)
            {
                _playedMapTime = GetMapTime(true);
                _playedMusicTime = GetMusicTime(true);
            }
            return music ? _playedMusicTime : _playedMapTime;
        }

        private static bool IsEditorEditMode()
        {
            try
            {
                if (!ADOBase.isLevelEditor) return false;
                scnEditor editor = scnEditor.instance ?? ADOBase.editor;
                return editor != null && !editor.playMode;
            }
            catch { return false; }
        }

        private static double GetMusicTime(bool played)
        {
            try
            {
                if (scrConductor.instance?.song == null) return 0d;
                if (played) return Math.Max(0d, scrConductor.instance.song.time);
                return scrConductor.instance.song.clip != null ? Math.Max(0d, scrConductor.instance.song.clip.length) : 0d;
            }
            catch { return 0d; }
        }

        private static double GetClicksPerSecond()
        {
            try
            {
                scrFloor floor = scrLevelMaker.instance?.listFloors != null && scrController.instance != null
                    && scrController.instance.currentSeqID >= 0 && scrController.instance.currentSeqID < scrLevelMaker.instance.listFloors.Count
                    ? scrLevelMaker.instance.listFloors[scrController.instance.currentSeqID] : null;
                double interval = floor?.nextfloor != null ? floor.nextfloor.entryTime - floor.entryTime : 0d;
                return interval > 0.000001d && scrConductor.instance?.song != null
                    ? scrConductor.instance.song.pitch / interval : 0d;
            }
            catch { return 0d; }
        }

        private static string GetJudgementMode()
        {
            try
            {
                switch (GCS.difficulty)
                {
                    case Difficulty.Lenient: return L("宽松", "Lenient");
                    case Difficulty.Strict: return L("严格", "Strict");
                    default: return L("标准", "Normal");
                }
            }
            catch { return L("标准", "Normal"); }
        }

        private static double GetTimingWindowPercent()
        {
            try
            {
                scrController controller = scrController.instance;
                scrLevelMaker maker = scrLevelMaker.instance;
                if (controller == null || maker?.listFloors == null || controller.currentSeqID < 0
                    || controller.currentSeqID >= maker.listFloors.Count) return 100d;
                scrFloor floor = maker.listFloors[controller.currentSeqID];
                return (floor?.nextfloor != null ? floor.nextfloor.marginScale : floor?.marginScale ?? 1f) * 100d;
            }
            catch { return 100d; }
        }

        private static string GetMusicText()
        {
            try
            {
                string text = scrUIController.instance?.txtLevelName?.text;
                if (!string.IsNullOrEmpty(text)) return StripRichText(text);

                // Same shape as the game's LevelData.fullCaption: "artist - song".
                if (!TryGetMusicParts(out string artist, out string title)) return string.Empty;
                return !string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(title) ? artist + " - " + title : title;
            }
            catch { return string.Empty; }
        }

        // Custom levels: LevelData.artist / song (author is the charter, see GetLevelAuthor).
        // Official levels: songCredits are the musicians, the title is the level caption.
        private static bool TryGetMusicParts(out string artist, out string title)
        {
            artist = string.Empty;
            title = string.Empty;
            LevelData data = ADOBase.customLevel?.levelData ?? scnGame.instance?.levelData;
            if (data != null)
            {
                artist = StripRichText(data.artist);
                title = StripRichText(data.song);
                if (!string.IsNullOrEmpty(artist) || !string.IsNullOrEmpty(title)) return true;
            }
            if (ADOBase.isOfficialLevel && !string.IsNullOrEmpty(scrController.currentWorldString)
                && WorldData.dict != null && WorldData.dict.TryGetValue(scrController.currentWorldString, out WorldData world)
                && world != null)
            {
                artist = StripRichText(world.songCredits?.people);
                title = StripRichText(scrController.instance?.caption);
                if (!string.IsNullOrEmpty(artist) || !string.IsNullOrEmpty(title)) return true;
            }
            return false;
        }

        private static void GetMusicParts(out string artist, out string title)
        {
            try
            {
                if (TryGetMusicParts(out artist, out title)) return;

                // Last resort: split the on-screen caption.
                string music = GetMusicText();
                title = music;
                for (int i = 0; i < MusicSeparators.Length; i++)
                {
                    int index = music.IndexOf(MusicSeparators[i], StringComparison.Ordinal);
                    if (index < 0) continue;
                    artist = music.Substring(0, index).Trim();
                    title = music.Substring(index + MusicSeparators[i].Length).Trim();
                    return;
                }
            }
            catch
            {
                artist = string.Empty;
                title = string.Empty;
            }
        }

        private static string StripRichText(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : RichTextPattern.Replace(value, string.Empty);
        }

        // Level/music strings change at most when a level loads, but building them concatenates and
        // runs a regex. They are cached per level and refreshed once per second as a safety net.
        private string GetSessionText(string key)
        {
            if (_sessionText.TryGetValue(key, out string cached)) return cached;
            string value;
            switch (key)
            {
                case "level": value = GetLevelAuthor(); break;
                case "music": value = GetMusicText(); break;
                case "artist": GetMusicParts(out value, out _); break;
                case "title": GetMusicParts(out _, out value); break;
                default: value = string.Empty; break;
            }

            value = value ?? string.Empty;
            _sessionText[key] = value;
            return value;
        }

        private string GetClockText(bool twelveHour)
        {
            DateTime now = DateTime.Now;
            long second = now.Ticks / TimeSpan.TicksPerSecond;
            if (second != _clockSecond)
            {
                _clockSecond = second;
                _clock24 = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                _clock12 = now.ToString("hh:mm:ss tt", CultureInfo.InvariantCulture);
            }

            return twelveHour ? _clock12 : _clock24;
        }

        private static bool IsComboSkip(SonnetJudgement kind) => kind == SonnetJudgement.Midspin || kind == SonnetJudgement.Auto;

        private static double ScoreWeight(SonnetJudgement kind)
        {
            switch (kind)
            {
                // 精准完美（3.3.x 的 Perfect、3.4.0 的 XPerfect）、PerfectPlus、自动 - 满分
                case SonnetJudgement.Perfect:
                case SonnetJudgement.PerfectPlus:
                case SonnetJudgement.Auto: return 1d;
                // PerfectMinus - 略低于满分
                case SonnetJudgement.PerfectMinus: return 0.95d;
                // EarlyPerfect, LatePerfect - 稍低
                case SonnetJudgement.EarlyPerfect:
                case SonnetJudgement.LatePerfect: return 0.9d;
                // VeryEarly, VeryLate - 一般
                case SonnetJudgement.VeryEarly:
                case SonnetJudgement.VeryLate: return 0.5d;
                // TooEarly, TooLate - 较差
                case SonnetJudgement.TooEarly:
                case SonnetJudgement.TooLate: return 0.2d;
                // 空敲、OverPress、中旋等不加分
                default: return 0d;
            }
        }

        // Folds hit margins recorded since the last call into the running combo and score totals.
        // Equivalent to the old backward scans: a combo counts consecutive non-breaking hits from the
        // end, skipping midspins and auto hits. A shrunk or replaced list triggers a full recount.
        private MarginStats UpdateMargins()
        {
            MarginStats stats = _margins;
            int frame = Time.frameCount;
            if (stats.Frame == frame) return stats;
            stats.Frame = frame;
            try
            {
                scrMarginTracker tracker = GetTracker();
                var list = tracker?.hitMargins;
                int total = Math.Max(0, GetTotalTiles() - 1);
                if (list == null)
                {
                    ResetMargins(stats, null, total);
                    return stats;
                }

                if (!ReferenceEquals(list, stats.ListRef) || list.Count < stats.Processed || total != stats.Total
                    || (stats.Processed > 0 && (int)list[stats.Processed - 1] != stats.LastValue))
                    ResetMargins(stats, list, total);

                for (int i = stats.Processed; i < list.Count; i++)
                {
                    SonnetJudgement kind = SonnetJudgements.Of(list[i]);
                    if (i < total) stats.Weighted += ScoreWeight(kind);
                    if (IsComboSkip(kind)) continue;
                    stats.PerfectCombo = SonnetJudgements.IsPerfect(kind) ? stats.PerfectCombo + 1 : 0;
                    stats.PureCombo = kind == SonnetJudgement.Perfect ? stats.PureCombo + 1 : 0;
                }

                stats.Processed = list.Count;
                stats.LastValue = list.Count > 0 ? (int)list[list.Count - 1] : -1;
            }
            catch
            {
                ResetMargins(stats, null, -1);
            }

            return stats;
        }

        private static void ResetMargins(MarginStats stats, object list, int total)
        {
            stats.ListRef = list;
            stats.Total = total;
            stats.Processed = 0;
            stats.LastValue = -1;
            stats.PerfectCombo = 0;
            stats.PureCombo = 0;
            stats.Weighted = 0d;
        }

        private double GetScore()
        {
            MarginStats stats = UpdateMargins();
            int total = stats.Total;
            if (stats.ListRef == null || total <= 0) return 0d;
            return Math.Round(900000d * Math.Min(total, stats.Weighted) / total
                + 100000d * Math.Min(total, stats.PerfectCombo) / total, 0, MidpointRounding.AwayFromZero);
        }
    }
}
