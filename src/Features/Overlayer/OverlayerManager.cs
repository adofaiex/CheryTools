using System;
using System.Collections.Generic;
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
                _tokenTriggerFrame.ResolveTagValue = ResolveTokenTriggerTagValue;
                _tokenTriggerFrame.ResolveTagNumber = ResolveTokenEffectTagNumber;
                _asyncRuntime = new OvAsyncRuntimePipeline();
                _asyncRuntime.Start();
            }
        }

        private void OnDestroy()
        {
            if (!ReferenceEquals(Instance, this)) return;
            _asyncRuntime?.Dispose();
            _asyncRuntime = null;
            _bakedOvTexts.Clear();
            _bakedOvImages.Clear();
            Instance = null;
        }

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
        private static readonly string[] HitCountTags = new string[] { "{te}", "{ve}", "{ep}", "{p}", "{lp}", "{vl}", "{tl}", "{miss}", "{fm}", "{fo}" };

        private enum OvTagKind
        {
            Literal,
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
            Score,
            Music,
            Artist,
            Title,
            Timing,
            XPerfectXpp,
            XPerfectEpp,
            XPerfectLpp,
            Attempts,
            CheckpointsUsed,
            CurrentCheckpoints,
            TotalCheckpoints,
            GameVersion,
            CheryToolsVersion,
            TotalPlaytime,
            MinFps,
            MaxFps
        }

        private struct OvTagToken
        {
            public OvTagKind Kind;
            public string Literal;
            public int Decimals;
        }

        private static double _lastHitTimingMs;
        private static bool _hasLastHitTiming;
        private static readonly Dictionary<int, HitMargin> ScoreFirstJudgements = new Dictionary<int, HitMargin>();
        // Running sum of GetScoreJudgementWeight over ScoreFirstJudgements. Kept in
        // sync at record/clear time so {score} evaluation is O(1) instead of scanning
        // every judged tile per frame (tens of thousands of iterations on long maps).
        private static double _scoreWeightedSum;
        private static scrController _scoreController;
        private static scrMarginTracker _scoreMarginTracker;
        private static int _scoreLastMarginCount;
        private static int _activeScoreTargetSeqId = -1;

        private sealed class OvTagPlan
        {
            public string Format;
            public OvTagToken[] Tokens;
            // Last string this plan resolved to. When the freshly built content matches it,
            // the cached instance is returned so the per-frame ToString() allocation is skipped
            // (the common case: the bake fast path only needs the value for a string comparison).
            public string LastResolved;
            public bool HasTags;
            public bool HasFpsTags;
            public bool HasClockTags;
            public bool HasRateTags;
            public bool HasScoreTags;
        }

        private readonly System.Collections.Generic.Dictionary<OverlayerText, OvTagPlan> _ovTagPlans = new System.Collections.Generic.Dictionary<OverlayerText, OvTagPlan>();
        private readonly System.Collections.Generic.Dictionary<string, OvTagPlan> _transientOvTagPlans = new System.Collections.Generic.Dictionary<string, OvTagPlan>();
        private readonly StringBuilder _ovTagBuilder = new StringBuilder(256);
        private readonly StringBuilder _ovTokenTextBuilder = new StringBuilder(256);
        // Same purpose as OvTagPlan.LastResolved, for the token-graph render path.
        private readonly System.Collections.Generic.Dictionary<OverlayerText, string> _ovTokenTextMemo = new System.Collections.Generic.Dictionary<OverlayerText, string>();
        private readonly System.Collections.Generic.List<OvSnapCandidate> _snapXRefsBuffer = new System.Collections.Generic.List<OvSnapCandidate>(32);
        private readonly System.Collections.Generic.List<OvSnapCandidate> _snapYRefsBuffer = new System.Collections.Generic.List<OvSnapCandidate>(32);
        private bool _renderMusicCacheReady;
        private string _renderMusicText = string.Empty;
        private string _renderMusicArtist = string.Empty;
        private string _renderMusicTitle = string.Empty;

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

        private int _currentPureCombo = 0;
        private int _currentPerfectCombo = 0;
        private bool _renderDirty = true;
        private long _lastRenderedRevision = -1;
        private long _dynamicScanRevision = -1;
        // Tag-driven numeric content ({accuracy}, {progress}, progress bar sources, ...).
        // Refreshed at OverlayerDataUpdateRate: above ~60 Hz these are indistinguishable.
        private bool _hasRateDynamicContent;
        // Frame-driven animation content (per-text/per-image animation tracks). Kept on
        // the full OverlayUpdateRate so motion stays smooth. Playing animations drive
        // redraws through MarkRenderDirty() in Update() and do not rely on this interval;
        // the flag only governs the idle periodic refresh.
        private bool _hasAnimationDynamicContent;
        private bool _hasFpsDynamicContent;
        private bool _hasClockDynamicContent;
        private float _nextPeriodicOverlayUpdateTime = 0f;
        private long _runtimeScanRevision = -1;
        private long _lastUpdateScanRevision = -1;
        private bool _needsHitTracker;
        private bool _needsComboTracker;
        private bool _hasTextAnimations;
        private bool _hasImageAnimations;
        private bool _hasClickAnimations;
        private bool _hasComboAnimations;
        private bool _hasTokenAnimations;
        private bool _hasTokenAnyKeyAnimations;
        private bool _hasTokenBeatAnimations;
        private readonly HashSet<KeyCode> _watchedTokenKeys = new HashSet<KeyCode>();
        private readonly List<KeyCode> _capturedKeysDown = new List<KeyCode>(8);
        private readonly List<KeyCode> _capturedKeysUp = new List<KeyCode>(8);
        private OvAsyncRuntimePipeline _asyncRuntime;
        private long _asyncRuntimeFrameId;
        private bool _asyncRuntimeActive;
        private bool _asyncRuntimeStateReady;
        private bool _asyncRuntimeErrorLogged;
        private float _nextOvRuntimeTickTime;
        private float _lastOvTokenUpdateTime;
        private int _runtimeTrackerInstanceId;
        private int _runtimeTrackerCount;
        private int _runtimeTrackerGeneration;
        private bool _runtimeAutoplayEnabled;
        private bool _runtimeNoFailEnabled;
        private int _runtimeJudgementMode = (int)OvJudgementMode.Normal;
        private readonly OvAnimationTriggerFrame _tokenTriggerFrame = new OvAnimationTriggerFrame();
        private readonly OvTokenAnimationRuntime _tokenAnimationRuntime = new OvTokenAnimationRuntime();
        private readonly System.Collections.Generic.Dictionary<OverlayerImage, OverlayerText> _imageAnimationProxies
            = new System.Collections.Generic.Dictionary<OverlayerImage, OverlayerText>();
        private readonly System.Collections.Generic.List<OverlayerText> _imageAnimationProxyBuffer
            = new System.Collections.Generic.List<OverlayerText>();

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
                ? RenderTimelineClock.Time + interval
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

            // Animation content needs the full rate to stay smooth; numeric tag content
            // does not. When both are present the faster interval wins, so animations
            // are never slowed down by the data rate.
            bool playing = Main.IsGamePlaying();
            if (_hasAnimationDynamicContent && playing)
            {
                return 1f / Mathf.Clamp(rate, 30f, 360f);
            }

            if (_hasRateDynamicContent && playing)
            {
                return 1f / GetDataUpdateRate();
            }

            if (_hasFpsDynamicContent)
            {
                return GetFpsTagRefreshInterval();
            }

            if (_hasClockDynamicContent)
            {
                return 1f;
            }

            return -1f;
        }

        private static float GetDataUpdateRate()
        {
            if (Main.Settings == null)
            {
                return 60f;
            }

            float dataRate = Main.Settings.OverlayerDataUpdateRate;
            if (dataRate <= 0f || float.IsNaN(dataRate) || float.IsInfinity(dataRate))
            {
                dataRate = 60f;
            }
            return Mathf.Clamp(dataRate, 15f, 360f);
        }

        private static float GetFpsTagRefreshInterval()
        {
            if (Main.Settings == null)
            {
                return 0.25f;
            }

            float interval = Main.Settings.OverlayerFpsTagRefreshInterval;
            if (interval <= 0f || float.IsNaN(interval) || float.IsInfinity(interval))
            {
                interval = 0.25f;
            }
            return Math.Max(0.05f, Math.Min(2.0f, interval));
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
            _hasAnimationDynamicContent = false;
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

                    // Use cached flags from compiled tag plan instead of string scanning
                    OvTagPlan plan = GetOvTagPlan(text, text.TextFormat);
                    if (plan.HasFpsTags)
                    {
                        _hasFpsDynamicContent = true;
                    }
                    if (plan.HasClockTags)
                    {
                        _hasClockDynamicContent = true;
                    }
                    if (plan.HasRateTags)
                    {
                        _hasRateDynamicContent = true;
                    }

                    OvAnimationGraph tokenGraph = text.TokenAnimation;
                    if (tokenGraph != null && tokenGraph.Enabled && tokenGraph.Nodes != null)
                    {
                        for (int n = 0; n < tokenGraph.Nodes.Count; n++)
                        {
                            OvAnimationNode node = tokenGraph.Nodes[n];
                            if (node == null) continue;
                            OvTagPlan nodePlan = null;
                            if (node.Kind == OvAnimationNodeKind.Trigger
                                && node.Trigger == OvAnimationTriggerKind.TagValueChanged)
                            {
                                nodePlan = GetTransientOvTagPlan(node.TriggerTag ?? string.Empty);
                            }
                            else if (node.Kind == OvAnimationNodeKind.Modify)
                            {
                                nodePlan = GetTransientOvTagPlan(node.ModifyText ?? string.Empty);
                            }
                            else if (node.Kind == OvAnimationNodeKind.Effect
                                && node.EffectValueSource == OvEffectValueSourceKind.Tag)
                            {
                                nodePlan = GetTransientOvTagPlan(node.EffectSourceTag ?? string.Empty);
                            }
                            if (nodePlan == null) continue;
                            if (nodePlan.HasFpsTags) _hasFpsDynamicContent = true;
                            if (nodePlan.HasClockTags) _hasClockDynamicContent = true;
                            if (nodePlan.HasRateTags) _hasRateDynamicContent = true;
                        }
                    }

                    if (text.Animations != null && text.Animations.Count > 0)
                    {
                        for (int j = 0; j < text.Animations.Count; j++)
                        {
                            if (text.Animations[j] != null && text.Animations[j].IsEnabled)
                            {
                                _hasAnimationDynamicContent = true;
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
                            _hasAnimationDynamicContent = true;
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
            _hasTokenAnimations = false;
            _hasTokenAnyKeyAnimations = false;
            _hasTokenBeatAnimations = false;
            _watchedTokenKeys.Clear();

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
                    OvTagPlan runtimePlan = GetOvTagPlan(text, format);
                    if (ContainsAny(format, HitCountTags))
                    {
                        _needsHitTracker = true;
                    }
                    if (runtimePlan != null && runtimePlan.HasScoreTags)
                    {
                        _needsHitTracker = true;
                        _needsComboTracker = true;
                    }
                    if (format.Contains("{combo}"))
                    {
                        _needsComboTracker = true;
                    }

                    ScanAnimationInterest(text.Animations, true);
                    ScanTokenAnimationInterest(text.TokenAnimation);
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
                    ScanTokenAnimationInterest(image.NodeAnimation);
                }
            }
        }

        private void ScanTokenAnimationInterest(OvAnimationGraph graph)
        {
            if (graph == null || !graph.Enabled || graph.Nodes == null) return;
            _hasTokenAnimations = true;
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                OvAnimationNode node = graph.Nodes[i];
                if (node == null) continue;
                if (node.Kind == OvAnimationNodeKind.Modify)
                {
                    _needsComboTracker = true;
                    continue;
                }
                if (node.Kind == OvAnimationNodeKind.Effect)
                {
                    if (node.EffectValueSource == OvEffectValueSourceKind.Tag)
                    {
                        _needsHitTracker = true;
                        _needsComboTracker = true;
                    }
                    continue;
                }
                if (node.Kind != OvAnimationNodeKind.Trigger) continue;
                if (node.Trigger == OvAnimationTriggerKind.AnyKeyDown)
                {
                    _hasTokenAnyKeyAnimations = true;
                }
                else if (node.Trigger == OvAnimationTriggerKind.ComboIncrease)
                {
                    _needsComboTracker = true;
                }
                else if (node.Trigger == OvAnimationTriggerKind.JudgementOccurred)
                {
                    _needsHitTracker = true;
                }
                else if (node.Trigger == OvAnimationTriggerKind.ComboBreak)
                {
                    _needsComboTracker = true;
                }
                else if (node.Trigger == OvAnimationTriggerKind.Beat)
                {
                    _hasTokenBeatAnimations = true;
                }
                else if (node.Trigger == OvAnimationTriggerKind.TagValueChanged)
                {
                    _needsComboTracker = true;
                }
                else if (node.Trigger == OvAnimationTriggerKind.SpecificKey && node.TriggerKeys != null)
                {
                    for (int k = 0; k < node.TriggerKeys.Count; k++)
                    {
                        if (Enum.TryParse(node.TriggerKeys[k], true, out KeyCode key) && key != KeyCode.None)
                        {
                            _watchedTokenKeys.Add(key);
                        }
                    }
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
            // Output only depends on the whole second, so duration tags allocate at most
            // once per second instead of once per frame. Same cap policy as the numeric cache.
            if (_durationCache.TryGetValue(totalSeconds, out string cached))
            {
                return cached;
            }

            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds / 60) % 60;
            int secs = totalSeconds % 60;

            if (_durationCache.Count >= NumericFormatCacheLimit)
            {
                _durationCache.Clear();
            }

            string formatted = hours > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", hours, minutes, secs)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", minutes, secs);

            _durationCache[totalSeconds] = formatted;
            return formatted;
        }

        private string GetMusicText()
        {
            if (_renderMusicCacheReady) return _renderMusicText;

            string musicText = "Author - SongName";
            if (scrUIController.instance != null && scrUIController.instance.txtLevelName != null)
            {
                musicText = scrUIController.instance.txtLevelName.text;
            }

            _renderMusicText = RichTextTagRegex.Replace(musicText, string.Empty);
            _renderMusicCacheReady = true;
            return _renderMusicText;
        }

        private void SplitMusicText(out string artist, out string title)
        {
            if (_renderMusicCacheReady && (_renderMusicArtist.Length > 0 || _renderMusicTitle.Length > 0))
            {
                artist = _renderMusicArtist;
                title = _renderMusicTitle;
                return;
            }

            string musicText = GetMusicText();
            artist = string.Empty;
            title = musicText;
            if (string.IsNullOrWhiteSpace(musicText))
            {
                title = string.Empty;
                _renderMusicArtist = artist;
                _renderMusicTitle = title;
                return;
            }

            string[] separators = new string[] { " - ", " — ", " – " };
            foreach (string separator in separators)
            {
                int index = musicText.IndexOf(separator, StringComparison.Ordinal);
                if (index < 0)
                {
                    continue;
                }

                artist = musicText.Substring(0, index).Trim();
                title = musicText.Substring(index + separator.Length).Trim();
                _renderMusicArtist = artist;
                _renderMusicTitle = title;
                return;
            }

            _renderMusicArtist = artist;
            _renderMusicTitle = title;
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

        // Index = decimal count; avoids composing "0.###" format strings per call.
        private static readonly string[] TrimZeroFormats =
        {
            "0", "0.#", "0.##", "0.###", "0.####", "0.#####", "0.######"
        };

        private static readonly string[] FixedFormats =
        {
            "F0", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8"
        };

        private static string GetFixedFormat(int decimals)
        {
            return FixedFormats[Math.Max(0, Math.Min(FixedFormats.Length - 1, decimals))];
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

            // The rounded value fully determines the output, so memoize per decimals slot.
            // Values that repeat frame to frame (every tag on a bake hit) then cost no allocation.
            // On overflow the slot is dropped and refills immediately: the live value set in any
            // one frame is tiny, and refilling costs a single ToString() -- unlike the bake/plan
            // caches, this one is cheap to rebuild, so clearing is safe here.
            var slot = _trimZeroCaches[decimals];
            if (slot == null)
            {
                slot = new System.Collections.Generic.Dictionary<double, string>();
                _trimZeroCaches[decimals] = slot;
            }
            if (slot.TryGetValue(rounded, out string cached))
            {
                return cached;
            }

            if (slot.Count >= NumericFormatCacheLimit)
            {
                slot.Clear();
            }

            string formatted = rounded.ToString(TrimZeroFormats[decimals], CultureInfo.InvariantCulture);
            slot[rounded] = formatted;
            return formatted;
        }

        // Clock/date tag output is constant within a whole second (or a whole day for the date
        // parts), but was reformatted every overlay pass. One memo slot per format is enough:
        // each format appears at most once per resolve, so slots never contend.
        private const int ClockSlotYear = 0;
        private const int ClockSlotMonth = 1;
        private const int ClockSlotDay = 2;
        private const int ClockSlotTime24 = 3;
        private const int ClockSlotTime12 = 4;
        private static readonly long[] _clockMemoKeys = new long[5] { -1L, -1L, -1L, -1L, -1L };
        private static readonly string[] _clockMemoValues = new string[5];

        private static string FormatClock(DateTime now, string format, int slot)
        {
            // Date parts only change at midnight; the time parts change once per second.
            long key = slot <= ClockSlotDay
                ? now.Date.Ticks
                : now.Ticks / TimeSpan.TicksPerSecond;

            if (_clockMemoKeys[slot] == key && _clockMemoValues[slot] != null)
            {
                return _clockMemoValues[slot];
            }

            string formatted = now.ToString(format, CultureInfo.InvariantCulture);
            _clockMemoKeys[slot] = key;
            _clockMemoValues[slot] = formatted;
            return formatted;
        }

        // Integer tags (tile counts, deaths, combos, checkpoints, hit counts) hold the same
        // value across many frames, so memoizing removes their per-frame allocation entirely.
        private static string FormatInt(int value)
        {
            if (_intFormatCache.TryGetValue(value, out string cached))
            {
                return cached;
            }

            if (_intFormatCache.Count >= IntFormatCacheLimit)
            {
                _intFormatCache.Clear();
            }

            string formatted = value.ToString(CultureInfo.InvariantCulture);
            _intFormatCache[value] = formatted;
            return formatted;
        }

        private const int IntFormatCacheLimit = 1024;
        private static readonly System.Collections.Generic.Dictionary<int, string> _intFormatCache
            = new System.Collections.Generic.Dictionary<int, string>();

        private const int NumericFormatCacheLimit = 512;
        private static readonly System.Collections.Generic.Dictionary<double, string>[] _trimZeroCaches
            = new System.Collections.Generic.Dictionary<double, string>[7];
        private static readonly System.Collections.Generic.Dictionary<int, string> _durationCache
            = new System.Collections.Generic.Dictionary<int, string>();

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

        // Last frame each transient plan was requested. Eviction removes the least
        // recently used half but never anything used this frame, so 128+ live tags
        // no longer thrash the cache (the old dictionary-order eviction could drop a
        // plan that was requested moments earlier and recompile it every frame).
        private readonly System.Collections.Generic.Dictionary<string, int> _transientOvTagPlanLastUse
            = new System.Collections.Generic.Dictionary<string, int>();
        private readonly System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>> _transientEvictionBuffer
            = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>(128);

        private OvTagPlan GetTransientOvTagPlan(string format)
        {
            string safeFormat = format ?? string.Empty;
            int currentFrame = Time.frameCount;
            if (_transientOvTagPlans.TryGetValue(safeFormat, out OvTagPlan cached))
            {
                _transientOvTagPlanLastUse[safeFormat] = currentFrame;
                return cached;
            }

            if (_transientOvTagPlans.Count >= 128)
            {
                _transientEvictionBuffer.Clear();
                foreach (var pair in _transientOvTagPlans)
                {
                    _transientOvTagPlanLastUse.TryGetValue(pair.Key, out int lastUse);
                    if (lastUse != currentFrame)
                    {
                        _transientEvictionBuffer.Add(
                            new System.Collections.Generic.KeyValuePair<string, int>(pair.Key, lastUse));
                    }
                }
                _transientEvictionBuffer.Sort((a, b) => a.Value.CompareTo(b.Value));

                int removeCount = Math.Min(64, _transientEvictionBuffer.Count);
                for (int i = 0; i < removeCount; i++)
                {
                    string key = _transientEvictionBuffer[i].Key;
                    _transientOvTagPlans.Remove(key);
                    _transientOvTagPlanLastUse.Remove(key);
                }
            }

            OvTagPlan plan = CompileOvTagPlan(safeFormat);
            _transientOvTagPlans[safeFormat] = plan;
            _transientOvTagPlanLastUse[safeFormat] = currentFrame;
            return plan;
        }

        private static OvTagPlan CompileOvTagPlan(string format)
        {
            string safeFormat = format ?? string.Empty;
            var tokens = new System.Collections.Generic.List<OvTagToken>();
            bool hasTags = false;
            bool hasFps = false;
            bool hasClock = false;
            bool hasRate = false;
            bool hasScore = false;
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
                if (TryParseOvTag(tagBody, out OvTagKind kind, out int decimals))
                {
                    tokens.Add(new OvTagToken
                    {
                        Kind = kind,
                        Decimals = decimals
                    });
                    hasTags = true;

                    // Cache dynamic content flags based on tag kind
                    switch (kind)
                    {
                        case OvTagKind.Fps:
                        case OvTagKind.MinFps:
                        case OvTagKind.MaxFps:
                            hasFps = true;
                            break;
                        case OvTagKind.WorldTime:
                        case OvTagKind.WorldTime12:
                            hasClock = true;
                            break;
                        case OvTagKind.Progress:
                        case OvTagKind.MapPlayedTime:
                        case OvTagKind.MusicPlayedTime:
                        case OvTagKind.CurrentClicks:
                        case OvTagKind.PassedTiles:
                        case OvTagKind.Bpm:
                        case OvTagKind.TrackBpm:
                        case OvTagKind.CurrentBpm:
                        case OvTagKind.Interval:
                        case OvTagKind.Speed:
                        case OvTagKind.Timing:
                        case OvTagKind.XAccuracy:
                        case OvTagKind.XPerfectXpp:
                        case OvTagKind.XPerfectEpp:
                        case OvTagKind.XPerfectLpp:
                        case OvTagKind.Attempts:
                        case OvTagKind.CheckpointsUsed:
                        case OvTagKind.CurrentCheckpoints:
                        case OvTagKind.TotalCheckpoints:
                        case OvTagKind.TotalPlaytime:
                            hasRate = true;
                            break;
                        case OvTagKind.Score:
                            hasScore = true;
                            break;
                    }
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
                HasTags = hasTags,
                HasFpsTags = hasFps,
                HasClockTags = hasClock,
                HasRateTags = hasRate,
                HasScoreTags = hasScore
            };
        }

        private static int FindOvTagClose(string format, int open)
        {
            if (format == null || open < 0 || open >= format.Length || format[open] != '{')
            {
                return -1;
            }

            return format.IndexOf('}', open + 1);
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

            string normalizedTagBody = tagBody.ToLowerInvariant();
            switch (normalizedTagBody)
            {
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
                case "score": kind = OvTagKind.Score; return true;
                case "music": kind = OvTagKind.Music; return true;
                case "artist": kind = OvTagKind.Artist; return true;
                case "title": kind = OvTagKind.Title; return true;
                case "xperfect:xpp":
                    kind = OvTagKind.XPerfectXpp; return true;
                case "xperfect:epp":
                    kind = OvTagKind.XPerfectEpp; return true;
                case "xperfect:lpp":
                    kind = OvTagKind.XPerfectLpp; return true;
                case "attempts": kind = OvTagKind.Attempts; return true;
                case "checkpointused": kind = OvTagKind.CheckpointsUsed; return true;
                case "curcheckpoint": kind = OvTagKind.CurrentCheckpoints; return true;
                case "totalcheckpoint": kind = OvTagKind.TotalCheckpoints; return true;
                case "gameversion": kind = OvTagKind.GameVersion; return true;
                case "cherytoolsversion": kind = OvTagKind.CheryToolsVersion; return true;
                case "totalplaytime": kind = OvTagKind.TotalPlaytime; return true;
            }

            if (TryParseDecimalTag(normalizedTagBody, "fps", 0, out decimals))
            {
                kind = OvTagKind.Fps;
                return true;
            }
            if (TryParseDecimalTag(normalizedTagBody, "minfps", 0, out decimals))
            {
                kind = OvTagKind.MinFps;
                return true;
            }
            if (TryParseDecimalTag(normalizedTagBody, "maxfps", 0, out decimals))
            {
                kind = OvTagKind.MaxFps;
                return true;
            }
            if (TryParseDecimalTag(normalizedTagBody, "acc", 2, out decimals))
            {
                kind = OvTagKind.Accuracy;
                return true;
            }
            if (TryParseDecimalTag(normalizedTagBody, "xacc", 2, out decimals))
            {
                kind = OvTagKind.XAccuracy;
                return true;
            }
            if (TryParseDecimalTag(normalizedTagBody, "progress", 2, out decimals))
            {
                kind = OvTagKind.Progress;
                return true;
            }
            if (TryParseDecimalTag(normalizedTagBody, "bpm", 2, out decimals))
            {
                kind = OvTagKind.Bpm;
                return true;
            }
            if (TryParseDecimalTag(normalizedTagBody, "tbpm", 2, out decimals))
            {
                kind = OvTagKind.TrackBpm;
                return true;
            }
            if (TryParseDecimalTag(normalizedTagBody, "cbpm", 2, out decimals))
            {
                kind = OvTagKind.CurrentBpm;
                return true;
            }
            if (TryParseDecimalTag(normalizedTagBody, "timing", 0, out decimals))
            {
                kind = OvTagKind.Timing;
                return true;
            }

            return false;
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
            OvTagPlan plan = GetOvTagPlan(ovText, format ?? string.Empty);
            return ResolveOvTextTagBody(plan);
        }

        private string ResolveOvTextTagBody(OvTagPlan plan)
        {
            if (plan == null || !plan.HasTags || plan.Tokens == null || plan.Tokens.Length == 0)
            {
                return plan != null ? (plan.Format ?? string.Empty) : string.Empty;
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

            if (BuilderContentEquals(_ovTagBuilder, plan.LastResolved))
            {
                return plan.LastResolved;
            }

            plan.LastResolved = _ovTagBuilder.ToString();
            return plan.LastResolved;
        }

        // Ordinal comparison against a StringBuilder without materializing it.
        private static bool BuilderContentEquals(StringBuilder builder, string value)
        {
            if (value == null || builder.Length != value.Length)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                if (builder[i] != value[i]) return false;
            }
            return true;
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
                case OvTagKind.TotalTiles:
                    return FormatInt(GetTotalTileCount());
                case OvTagKind.PassedTiles:
                    return FormatInt(GetPassedTileCount());
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
                    return FormatClock(now, "yyyy", ClockSlotYear);
                case OvTagKind.DateMonth:
                    EnsureNow(ref now, ref nowReady);
                    return FormatClock(now, "MM", ClockSlotMonth);
                case OvTagKind.DateDay:
                    EnsureNow(ref now, ref nowReady);
                    return FormatClock(now, "dd", ClockSlotDay);
                case OvTagKind.WorldTime:
                    EnsureNow(ref now, ref nowReady);
                    return FormatClock(now, "HH:mm:ss", ClockSlotTime24);
                case OvTagKind.WorldTime12:
                    EnsureNow(ref now, ref nowReady);
                    return FormatClock(now, "hh:mm:ss tt", ClockSlotTime12);
                case OvTagKind.Bpm:
                    EnsureBpmValues(ref bpmReady, ref baseBpm, ref trackBpm, ref currentBpm);
                    return FormatNumberTrimZeros(baseBpm, token.Decimals);
                case OvTagKind.TrackBpm:
                    EnsureBpmValues(ref bpmReady, ref baseBpm, ref trackBpm, ref currentBpm);
                    return FormatNumberTrimZeros(trackBpm, token.Decimals);
                case OvTagKind.CurrentBpm:
                    EnsureBpmValues(ref bpmReady, ref baseBpm, ref trackBpm, ref currentBpm);
                    return FormatNumberTrimZeros(currentBpm, token.Decimals);
                case OvTagKind.Timing:
                    return FormatNumberTrimZeros(_hasLastHitTiming ? _lastHitTimingMs : 0.0, token.Decimals);
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
                    return tracker != null ? FormatInt(tracker.GetDeaths()) : "0";
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
                    return FormatInt(_currentPureCombo);
                case OvTagKind.PerfectCombo:
                    return FormatInt(_currentPerfectCombo);
                case OvTagKind.Score:
                    return GetScoreValue(ref tracker, ref trackerReady).ToString("0", CultureInfo.InvariantCulture);
                case OvTagKind.Music:
                    return GetMusicText();
                case OvTagKind.Artist:
                    SplitMusicText(out string artist, out _);
                    return artist;
                case OvTagKind.Title:
                    SplitMusicText(out _, out string title);
                    return title;
                case OvTagKind.XPerfectXpp:
                    return IsXPerfectIntegrationActive() ? FormatInt(XPerfectBridge.XPerfectCount()) : "0";
                case OvTagKind.XPerfectEpp:
                    return IsXPerfectIntegrationActive() ? FormatInt(XPerfectBridge.PlusPerfectCount()) : "0";
                case OvTagKind.XPerfectLpp:
                    return IsXPerfectIntegrationActive() ? FormatInt(XPerfectBridge.MinusPerfectCount()) : "0";
                case OvTagKind.Attempts:
                    return FormatInt(OvLevelStatsTracker.GetCurrentLevelAttempts());
                case OvTagKind.CheckpointsUsed:
                    return FormatInt(Math.Max(0, scrController.checkpointsUsed));
                case OvTagKind.CurrentCheckpoints:
                    return FormatInt(OvLevelStatsTracker.CurrentCheckpointCount);
                case OvTagKind.TotalCheckpoints:
                    return FormatInt(OvLevelStatsTracker.TotalCheckpointCount);
                case OvTagKind.GameVersion:
                    return Application.version ?? string.Empty;
                case OvTagKind.CheryToolsVersion:
                    return Main.ModEntry != null && Main.ModEntry.Info != null
                        ? Main.ModEntry.Info.Version.ToString()
                        : string.Empty;
                case OvTagKind.TotalPlaytime:
                    return FormatDuration(OvLevelStatsTracker.CurrentLevelPlaytimeSeconds);
                case OvTagKind.MinFps:
                    return FormatNumberTrimZeros(OvLevelStatsTracker.CurrentMinFps, token.Decimals);
                case OvTagKind.MaxFps:
                    return FormatNumberTrimZeros(OvLevelStatsTracker.CurrentMaxFps, token.Decimals);
                default:
                    return token.Literal ?? string.Empty;
            }
        }

        // Both normalizers run for every effect/token tag on every overlay refresh, and
        // the tag vocabulary is tiny, so the string surgery is memoized. Capacity-capped
        // (drop-new-on-full) so user-typed garbage can't grow the maps unboundedly.
        private static readonly System.Collections.Generic.Dictionary<string, string> _tagReferenceCache
            = new System.Collections.Generic.Dictionary<string, string>();
        private static readonly System.Collections.Generic.Dictionary<string, string> _lowerTagNameCache
            = new System.Collections.Generic.Dictionary<string, string>();
        private const int TagNameCacheCapacity = 256;

        private static string NormalizeTagReference(string tag)
        {
            if (tag == null) return string.Empty;
            if (_tagReferenceCache.TryGetValue(tag, out string cached)) return cached;

            string source = tag.Trim();
            if (source.Length >= 2 && source[0] == '{' && source[source.Length - 1] == '}')
            {
                source = source.Substring(1, source.Length - 2).Trim();
            }
            int colon = source.LastIndexOf(':');
            if (colon > 0 && int.TryParse(source.Substring(colon + 1), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out _))
            {
                source = source.Substring(0, colon);
            }
            if (_tagReferenceCache.Count < TagNameCacheCapacity)
            {
                _tagReferenceCache[tag] = source;
            }
            return source;
        }

        private static string NormalizeTagNameLower(string name)
        {
            if (_lowerTagNameCache.TryGetValue(name, out string cached)) return cached;
            string normalized = name.Trim().ToLowerInvariant();
            if (_lowerTagNameCache.Count < TagNameCacheCapacity)
            {
                _lowerTagNameCache[name] = normalized;
            }
            return normalized;
        }

        private double ResolveNumericTagValue(
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

            switch (NormalizeTagNameLower(name))
            {
                case "fps":
                    return _cachedFps;
                case "minfps":
                    return OvLevelStatsTracker.CurrentMinFps;
                case "maxfps":
                    return OvLevelStatsTracker.CurrentMaxFps;
                case "attempts":
                    return OvLevelStatsTracker.GetCurrentLevelAttempts();
                case "checkpointused":
                    return Math.Max(0, scrController.checkpointsUsed);
                case "curcheckpoint":
                    return OvLevelStatsTracker.CurrentCheckpointCount;
                case "totalcheckpoint":
                    return OvLevelStatsTracker.TotalCheckpointCount;
                case "totalplaytime":
                    return OvLevelStatsTracker.CurrentLevelPlaytimeSeconds;
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
                case "timing":
                    return _hasLastHitTiming ? _lastHitTimingMs : 0.0;
                case "interval":
                    return GetCurrentTimingWindowScalePercent();
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
                case "score":
                    return GetScoreValue(ref tracker, ref trackerReady);
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

        internal static void BeginScoreTileHit(scrPlayer player)
        {
            _activeScoreTargetSeqId = ResolveScoreTargetSeqId(player);
        }

        internal static void EndScoreTileHit()
        {
            _activeScoreTargetSeqId = -1;
        }

        internal static void RecordScoreJudgement(scrMarginTracker tracker, HitMargin margin)
        {
            EnsureScoreRun(tracker);
            Instance?.EnqueueRuntimeJudgement(tracker, margin);
            int targetSeqId = _activeScoreTargetSeqId;
            if (targetSeqId < 0)
            {
                targetSeqId = ResolveScoreTargetSeqId(
                    scrController.instance != null ? scrController.instance.playerOne : null);
            }
            if (targetSeqId < 0 && scrController.instance != null)
            {
                targetSeqId = Math.Max(0, scrController.instance.currentSeqID + 1);
            }
            if (targetSeqId < 0) return;

            if (RecordFirstTileJudgement(ScoreFirstJudgements, targetSeqId, margin))
            {
                _scoreWeightedSum += GetScoreJudgementWeight(margin);
                Instance?.MarkRenderDirty();
            }
            int currentCount = tracker != null && tracker.hitMargins != null ? tracker.hitMargins.Count : 0;
            _scoreLastMarginCount = currentCount + 1;
        }

        private void EnqueueRuntimeJudgement(scrMarginTracker tracker, HitMargin margin)
        {
            OvAsyncRuntimePipeline runtime = _asyncRuntime;
            if (runtime == null || tracker == null) return;
            int trackerId;
            try
            {
                trackerId = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(tracker);
            }
            catch
            {
                return;
            }
            runtime.EnqueueJudgement(trackerId, (int)margin);
        }

        private static int ResolveScoreTargetSeqId(scrPlayer player)
        {
            try
            {
                if (player == null || player.planetarySystem == null) return -1;
                scrPlanet planet = player.planetarySystem.chosenPlanet;
                if (planet == null || planet.currfloor == null) return -1;
                scrFloor current = planet.currfloor;
                return current.nextfloor != null ? current.nextfloor.seqID : current.seqID;
            }
            catch
            {
                return -1;
            }
        }

        private static void EnsureScoreRun(scrMarginTracker tracker)
        {
            scrController controller = scrController.instance;
            int currentCount = tracker != null && tracker.hitMargins != null ? tracker.hitMargins.Count : 0;
            bool identityChanged = !ReferenceEquals(_scoreController, controller)
                || !ReferenceEquals(_scoreMarginTracker, tracker);
            bool fullRestart = !identityChanged
                && currentCount < _scoreLastMarginCount
                && (controller == null || controller.currentSeqID <= 0);
            if (identityChanged || fullRestart)
            {
                ScoreFirstJudgements.Clear();
                _scoreWeightedSum = 0.0;
            }
            _scoreController = controller;
            _scoreMarginTracker = tracker;
            _scoreLastMarginCount = currentCount;
        }

        private static bool RecordFirstTileJudgement(Dictionary<int, HitMargin> firstJudgements,
            int tileSeqId, HitMargin margin)
        {
            if (firstJudgements == null || tileSeqId < 0 || firstJudgements.ContainsKey(tileSeqId)) return false;
            firstJudgements[tileSeqId] = margin;
            return true;
        }

        private double GetScoreValue(ref scrMarginTracker tracker, ref bool trackerReady)
        {
            EnsureTracker(ref tracker, ref trackerReady);
            EnsureScoreRun(tracker);
            return CalculateScoreValue(GetScoreTileCount(), _scoreWeightedSum, _currentPerfectCombo);
        }

        private static int GetScoreTileCount()
        {
            return GetScoreTileCountFromTotal(GetTotalTileCount());
        }

        private static int GetScoreTileCountFromTotal(int totalTiles)
        {
            return Math.Max(0, totalTiles - 1);
        }

        private static double CalculateScoreValue(int totalTiles, double weightedJudgements,
            int perfectCombo)
        {
            if (totalTiles <= 0) return 0.0;
            weightedJudgements = Math.Max(0.0, Math.Min(totalTiles, weightedJudgements));
            int safeCombo = Math.Max(0, Math.Min(totalTiles, perfectCombo));
            double judgementScore = 900000.0 * weightedJudgements / totalTiles;
            double comboScore = 100000.0 * safeCombo / totalTiles;
            return Math.Max(0.0, Math.Min(1000000.0,
                Math.Round(judgementScore + comboScore, 0, MidpointRounding.AwayFromZero)));
        }

        private static double GetScoreJudgementWeight(HitMargin margin)
        {
            switch (margin)
            {
                case HitMargin.Perfect:
                case HitMargin.Auto:
                    return 1.0;
                case HitMargin.EarlyPerfect:
                case HitMargin.LatePerfect:
                    return 0.9;
                case HitMargin.VeryEarly:
                case HitMargin.VeryLate:
                    return 0.5;
                case HitMargin.TooEarly:
                case HitMargin.TooLate:
                    return 0.2;
                default:
                    return 0.0;
            }
        }

        private static string GetTrackerHitCount(ref scrMarginTracker tracker, ref bool trackerReady, HitMargin margin)
        {
            EnsureTracker(ref tracker, ref trackerReady);
            return tracker != null ? FormatInt(tracker.GetHits(margin)) : "0";
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

        internal static void RecordLastHitTiming(double timingMs)
        {
            if (double.IsNaN(timingMs) || double.IsInfinity(timingMs))
            {
                return;
            }

            _lastHitTimingMs = Math.Max(-9999.0, Math.Min(9999.0, timingMs));
            _hasLastHitTiming = true;
        }

        // Value-type layout key: the previous string key needed six float.ToString("R")
        // calls plus a builder ToString per text per frame just to detect "unchanged".
        internal readonly struct OvTextLayoutKey : IEquatable<OvTextLayoutKey>
        {
            private readonly string _fontPath;
            private readonly string _text;
            private readonly float _fontSize;
            private readonly float _letterSpacing;
            private readonly float _lineHeightOffset;
            private readonly int _alignment;
            private readonly float _pivotX;
            private readonly float _pivotY;
            private readonly int _displayWidth;
            private readonly int _displayHeight;

            public OvTextLayoutKey(OverlayerText ovText, string renderedText, int displayWidth, int displayHeight)
            {
                _fontPath = ovText.FontPath ?? string.Empty;
                _text = renderedText ?? string.Empty;
                _fontSize = ovText.FontSize;
                _letterSpacing = ovText.LetterSpacing;
                _lineHeightOffset = ovText.LineHeightOffset;
                _alignment = ovText.Alignment;
                _pivotX = ovText.PivotX;
                _pivotY = ovText.PivotY;
                _displayWidth = displayWidth;
                _displayHeight = displayHeight;
            }

            public bool Equals(OvTextLayoutKey other)
            {
                return _fontSize == other._fontSize
                    && _letterSpacing == other._letterSpacing
                    && _lineHeightOffset == other._lineHeightOffset
                    && _alignment == other._alignment
                    && _pivotX == other._pivotX
                    && _pivotY == other._pivotY
                    && _displayWidth == other._displayWidth
                    && _displayHeight == other._displayHeight
                    && string.Equals(_text, other._text, StringComparison.Ordinal)
                    && string.Equals(_fontPath, other._fontPath, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is OvTextLayoutKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _text != null ? _text.GetHashCode() : 0;
                    hash = hash * 31 + _fontSize.GetHashCode();
                    hash = hash * 31 + _alignment;
                    return hash;
                }
            }
        }

        private OvTextLayoutKey BuildOvTextLayoutKey(OverlayerText ovText, string renderedText)
        {
            System.Numerics.Vector2 displaySize = ImGuiController.ScreenDisplaySize;
            return new OvTextLayoutKey(
                ovText,
                renderedText,
                Mathf.RoundToInt(displaySize.X),
                Mathf.RoundToInt(displaySize.Y));
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
            return FormatPercent(GetCurrentTimingWindowScalePercent());
        }

        private static double GetCurrentTimingWindowScalePercent()
        {
            double scale = 1.0;
            if (TryGetCurrentFloor(out scrFloor currentFloor))
            {
                if (currentFloor.nextfloor != null)
                    scale = currentFloor.nextfloor.marginScale;
                else
                    scale = currentFloor.marginScale;
            }

            return scale * 100.0;
        }

        public class AnimPlaybackState
        {
            public float CurrentTime = 0f;
            public bool IsPlaying = false;
        }
        
        private System.Collections.Generic.Dictionary<OverlayerAnimation, AnimPlaybackState> _animStates = new System.Collections.Generic.Dictionary<OverlayerAnimation, AnimPlaybackState>();
        private readonly System.Collections.Generic.Dictionary<OverlayerText, OvTextLayoutKey> _ovTextLayoutKeys = new System.Collections.Generic.Dictionary<OverlayerText, OvTextLayoutKey>();
        private readonly System.Collections.Generic.Dictionary<OverlayerText, UnityEngine.Vector2> _ovTextStableSizes = new System.Collections.Generic.Dictionary<OverlayerText, UnityEngine.Vector2>();
        private readonly System.Collections.Generic.List<string> _ovTextRenderIds = new System.Collections.Generic.List<string>();
        private readonly System.Collections.Generic.List<string> _ovTextSelectIds = new System.Collections.Generic.List<string>();
        private readonly System.Collections.Generic.List<OvImageRenderIds> _ovImageRenderIds = new System.Collections.Generic.List<OvImageRenderIds>();
        private readonly System.Collections.Generic.List<OvImageRenderIds> _ovVideoRenderIds = new System.Collections.Generic.List<OvImageRenderIds>();
        private readonly System.Collections.Generic.List<OvProgressBarRenderIds> _ovProgressBarRenderIds = new System.Collections.Generic.List<OvProgressBarRenderIds>();
        private readonly System.Collections.Generic.Dictionary<OverlayerText, BakedOvText> _bakedOvTexts
            = new System.Collections.Generic.Dictionary<OverlayerText, BakedOvText>();
        private readonly System.Collections.Generic.Dictionary<OverlayerImage, BakedOvImage> _bakedOvImages
            = new System.Collections.Generic.Dictionary<OverlayerImage, BakedOvImage>();
        private long _ovBakeRevision = -1;
        private bool _ovBakeWasEditMode;
        private bool _ovBakeManualMode;

        private sealed class BakedOvText
        {
            public string RenderId;
            public string RenderedText;
            public int SortingOrder;
            public long TokenVisualRevision;
            public SdfTextRenderer.TextBounds Bounds;
        }

        private sealed class BakedOvImage
        {
            public string RenderId;
            public int SortingOrder;
            public long TokenVisualRevision;
        }

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

        public void SetManualBakeMode(bool enabled)
        {
            if (_ovBakeManualMode == enabled) return;

            _ovBakeManualMode = enabled;
            ClearOvRuntimeBake();
        }

        public void RequestManualBake()
        {
            // Rebuild on the next overlay pass. This keeps dynamic tags,
            // judgement data and token animation evaluation live.
            ClearOvRuntimeBake();
        }

        private void ClearOvRuntimeBake()
        {
            _bakedOvTexts.Clear();
            _bakedOvImages.Clear();
            _ovBakeRevision = OverlayRenderInvalidator.Revision;
            _ovBakeWasEditMode = Main.Settings != null && Main.Settings.OverlayerEditMode;
            _renderDirty = true;
        }

        private void PrepareOvRuntimeBake(bool editMode)
        {
            bool configuredManualMode = Main.Settings != null && Main.Settings.OverlayerManualBakeEnabled;
            if (_ovBakeManualMode != configuredManualMode)
            {
                _ovBakeManualMode = configuredManualMode;
                ClearOvRuntimeBake();
            }

            long revision = OverlayRenderInvalidator.Revision;
            bool editModeChanged = editMode != _ovBakeWasEditMode;
            bool revisionChanged = _ovBakeRevision != revision;
            if (editModeChanged || (revisionChanged && !_ovBakeManualMode))
            {
                _bakedOvTexts.Clear();
                _bakedOvImages.Clear();
                _ovBakeRevision = revision;
                _ovBakeWasEditMode = editMode;
            }
        }

        private bool CanBakeOvText(OverlayerText text, bool editMode)
        {
            if (text == null || editMode)
            {
                return false;
            }

            if (text.Animations != null)
            {
                for (int i = 0; i < text.Animations.Count; i++)
                {
                    OverlayerAnimation animation = text.Animations[i];
                    if (animation != null && animation.IsEnabled) return false;
                }
            }
            return true;
        }

        private static bool CanBakeOvImage(OverlayerImage image, bool editMode)
        {
            if (image == null || editMode)
            {
                return false;
            }

            if (image.Animations != null)
            {
                for (int i = 0; i < image.Animations.Count; i++)
                {
                    OverlayerAnimation animation = image.Animations[i];
                    if (animation != null && animation.IsEnabled) return false;
                }
            }
            return true;
        }

        private static bool HasEnabledImageNodeGraph(OverlayerImage image)
        {
            OvAnimationGraph graph = image != null ? image.NodeAnimation : null;
            return graph != null
                && graph.Enabled
                && graph.Nodes != null
                && graph.Nodes.Count > 0;
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

        private string ResolveTokenTriggerTagValue(OverlayerText text, string tag)
        {
            OvTagPlan plan = GetTransientOvTagPlan(tag ?? string.Empty);
            return ResolveOvTextTagBody(plan);
        }

        private double ResolveTokenEffectTagNumber(OverlayerText text, string tag)
        {
            string source = NormalizeTagReference(tag);
            if (source.Length == 0) return double.NaN;

            scrMarginTracker tracker = null;
            bool trackerReady = false;
            bool bpmReady = false;
            float baseBpm = 0f;
            double trackBpm = 0.0;
            double currentBpm = 0.0;

            return ResolveNumericTagValue(source, ref tracker, ref trackerReady, ref bpmReady,
                ref baseBpm, ref trackBpm, ref currentBpm);
        }

        private bool TryFormatTokenNumber(OverlayerText text, string tag, OvAnimationNode formatNode,
            string fallback, out string formatted)
        {
            formatted = fallback;
            if (formatNode == null) return false;
            string resolved = ResolveTokenTriggerTagValue(text, tag);
            if (!double.TryParse(resolved, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                || double.IsNaN(value) || double.IsInfinity(value))
            {
                return false;
            }

            formatted = FormatTokenNumberValue(value, tag, formatNode);
            return true;
        }

        private static string FormatTokenNumberValue(double value, string tag, OvAnimationNode formatNode)
        {
            int decimals = Math.Max(0, Math.Min(8, formatNode.NumberFormatDecimals));
            switch (formatNode.NumberFormatKind)
            {
                case OvNumberFormatKind.Percentage:
                    bool ratioInput = formatNode.PercentageInputKind == OvPercentageInputKind.Ratio
                        || (formatNode.PercentageInputKind == OvPercentageInputKind.Auto
                            && !IsNativePercentageTag(tag));
                    if (ratioInput) value *= 100.0;
                    return value.ToString(GetFixedFormat(decimals), CultureInfo.InvariantCulture) + "%";
                case OvNumberFormatKind.FixedDecimals:
                    return value.ToString(GetFixedFormat(decimals), CultureInfo.InvariantCulture);
                case OvNumberFormatKind.ZeroPaddedInteger:
                    int width = Math.Max(1, Math.Min(16, formatNode.NumberFormatWidth));
                    double rounded = Math.Round(value, 0, MidpointRounding.AwayFromZero);
                    string digits = Math.Abs(rounded).ToString("0", CultureInfo.InvariantCulture).PadLeft(width, '0');
                    return rounded < 0.0 ? "-" + digits : digits;
                default:
                    return FormatNumberTrimZeros(value, 8);
            }
        }

        private static bool IsNativePercentageTag(string tag)
        {
            return OvNumericTagUnits.IsPercentage(tag);
        }

        private OvRuntimeMainSnapshot CaptureRuntimeSnapshot(bool trackJudgements)
        {
            var snapshot = new OvRuntimeMainSnapshot
            {
                FrameId = ++_asyncRuntimeFrameId,
                Active = true,
                TrackJudgements = trackJudgements,
                TimelineTime = RenderTimelineClock.Time,
                TimelineDeltaTime = RenderTimelineClock.DeltaTime,
                CalculateFps = _hasFpsDynamicContent,
                FpsRefreshInterval = GetFpsTagRefreshInterval(),
                JudgementMode = (int)OvJudgementMode.Normal
            };

            scrController controller = null;
            try
            {
                controller = scrController.instance;
                if (_hasTokenAnimations && controller != null)
                {
                    snapshot.ControllerInstanceId = controller.GetInstanceID();
                    snapshot.ControllerState = (int)controller.state;
                }
                snapshot.NoFailEnabled = controller != null ? controller.noFail : GCS.useNoFail;
            }
            catch
            {
            }
            try
            {
                snapshot.AutoplayEnabled = RDC.auto;
            }
            catch
            {
            }
            try
            {
                snapshot.JudgementMode = (int)GCS.difficulty;
            }
            catch
            {
            }

            bool captureAnyKey = _hasClickAnimations || _hasTokenAnyKeyAnimations;
            if (captureAnyKey)
            {
                try
                {
                    snapshot.AnyKeyDown = Input.anyKeyDown;
                }
                catch
                {
                }
            }
            CaptureSpecificKeys(ref snapshot);

            if (_hasTokenBeatAnimations)
            {
                try
                {
                    scrConductor conductor = scrConductor.instance;
                    if (conductor != null && conductor.onBeatHappened)
                    {
                        snapshot.BeatEvents = new[]
                        {
                            new OvRuntimeBeatEvent
                            {
                                ConductorInstanceId = conductor.GetInstanceID(),
                                BeatNumber = conductor.beatNumber
                            }
                        };
                    }
                }
                catch
                {
                }
            }

            CaptureTrackerBootstrap(controller, trackJudgements, ref snapshot);
            return snapshot;
        }

        private void CaptureSpecificKeys(ref OvRuntimeMainSnapshot snapshot)
        {
            if (_watchedTokenKeys.Count == 0) return;
            _capturedKeysDown.Clear();
            _capturedKeysUp.Clear();
            foreach (KeyCode key in _watchedTokenKeys)
            {
                try
                {
                    if (Input.GetKeyDown(key)) _capturedKeysDown.Add(key);
                    if (Input.GetKeyUp(key)) _capturedKeysUp.Add(key);
                }
                catch
                {
                }
            }
            if (_capturedKeysDown.Count > 0) snapshot.KeysDown = _capturedKeysDown.ToArray();
            if (_capturedKeysUp.Count > 0) snapshot.KeysUp = _capturedKeysUp.ToArray();
        }

        private void CaptureTrackerBootstrap(scrController controller, bool trackJudgements,
            ref OvRuntimeMainSnapshot snapshot)
        {
            scrMarginTracker tracker = null;
            if (trackJudgements && controller != null && controller.playerOne != null)
            {
                tracker = controller.playerOne.marginTracker;
            }

            int trackerId = 0;
            int hitCount = 0;
            if (tracker != null)
            {
                try
                {
                    trackerId = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(tracker);
                    hitCount = tracker.hitMargins != null ? tracker.hitMargins.Count : 0;
                }
                catch
                {
                    tracker = null;
                    trackerId = 0;
                    hitCount = 0;
                }
            }

            bool reset = trackerId != _runtimeTrackerInstanceId
                || (trackerId != 0 && hitCount < _runtimeTrackerCount);
            if (reset)
            {
                _runtimeTrackerGeneration++;
                _runtimeTrackerInstanceId = trackerId;
                if (tracker != null && hitCount > 0)
                {
                    int[] bootstrap = new int[hitCount];
                    for (int i = 0; i < hitCount; i++) bootstrap[i] = (int)tracker.hitMargins[i];
                    snapshot.BootstrapJudgements = bootstrap;
                }
                else
                {
                    snapshot.BootstrapJudgements = Array.Empty<int>();
                }
                snapshot.BootstrapJudgementSequence = _asyncRuntime != null
                    ? _asyncRuntime.LastEnqueuedJudgementSequence
                    : 0L;
            }

            _runtimeTrackerCount = hitCount;
            snapshot.TrackerInstanceId = trackerId;
            snapshot.TrackerGeneration = _runtimeTrackerGeneration;
        }

        private bool ConsumeAsyncRuntimeResults()
        {
            bool consumed = false;
            _anyKeyPressedThisFrame = false;
            _comboIncreasedThisFrame = false;
            _tokenTriggerFrame.AutoplayEnabled = _runtimeAutoplayEnabled;
            _tokenTriggerFrame.NoFailEnabled = _runtimeNoFailEnabled;
            _tokenTriggerFrame.JudgementMode = _runtimeJudgementMode;

            OvAsyncRuntimePipeline runtime = _asyncRuntime;
            if (runtime == null) return false;
            while (runtime.TryDequeue(out OvRuntimeComputedFrame result))
            {
                consumed = true;
                if (result.Reset)
                {
                    _currentPureCombo = 0;
                    _currentPerfectCombo = 0;
                    _asyncRuntimeStateReady = false;
                    continue;
                }

                _asyncRuntimeStateReady = true;
                _runtimeAutoplayEnabled = result.AutoplayEnabled;
                _runtimeNoFailEnabled = result.NoFailEnabled;
                _runtimeJudgementMode = result.JudgementMode;
                _tokenTriggerFrame.AutoplayEnabled = _runtimeAutoplayEnabled;
                _tokenTriggerFrame.NoFailEnabled = _runtimeNoFailEnabled;
                _tokenTriggerFrame.JudgementMode = _runtimeJudgementMode;

                _currentPureCombo = result.PureCombo;
                _currentPerfectCombo = result.PerfectCombo;
                _comboIncreasedThisFrame |= result.ComboIncreased;
                _tokenTriggerFrame.PureComboBroken |= result.PureComboBroken;
                _tokenTriggerFrame.PerfectComboBroken |= result.PerfectComboBroken;
                _tokenTriggerFrame.LevelStarted |= result.LevelStarted;
                _tokenTriggerFrame.LevelEnded |= result.LevelEnded;
                _anyKeyPressedThisFrame |= result.AnyKeyDown;
                _tokenTriggerFrame.AnyKeyDown |= result.AnyKeyDown;

                if (result.BeatHappened)
                {
                    _tokenTriggerFrame.BeatHappened = true;
                    _tokenTriggerFrame.BeatNumber = result.BeatNumber;
                }
                if (result.Judgements != null)
                {
                    for (int i = 0; i < result.Judgements.Length; i++)
                    {
                        _tokenTriggerFrame.Judgements.Add(result.Judgements[i]);
                    }
                }
                if (result.KeysDown != null)
                {
                    for (int i = 0; i < result.KeysDown.Length; i++)
                    {
                        _tokenTriggerFrame.KeysDown.Add(result.KeysDown[i]);
                    }
                }
                if (result.KeysUp != null)
                {
                    for (int i = 0; i < result.KeysUp.Length; i++)
                    {
                        _tokenTriggerFrame.KeysUp.Add(result.KeysUp[i]);
                    }
                }
                if (result.FpsUpdated)
                {
                    _cachedFps = result.Fps;
                    OvLevelStatsTracker.RecordFps(result.Fps);
                }
                if (result.RenderStateChanged) MarkRenderDirty();
            }

            _tokenTriggerFrame.ComboIncreased = _comboIncreasedThisFrame;
            string error = runtime.ConsumeError();
            if (!_asyncRuntimeErrorLogged && !string.IsNullOrEmpty(error))
            {
                _asyncRuntimeErrorLogged = true;
                Main.Logger?.Log("[CheryTools] OV async runtime error (reported once): " + error);
            }
            return consumed;
        }

        private static float GetOvRuntimeInterval()
        {
            float rate = Main.Settings != null ? Main.Settings.OverlayUpdateRate : 240f;
            if (float.IsNaN(rate) || float.IsInfinity(rate) || rate <= 0f)
            {
                rate = 240f;
            }
            return 1f / Mathf.Clamp(rate, 30f, 360f);
        }

        private static bool HasTransientRuntimeInput(OvRuntimeMainSnapshot snapshot)
        {
            return snapshot.AnyKeyDown
                || (snapshot.KeysDown != null && snapshot.KeysDown.Length > 0)
                || (snapshot.KeysUp != null && snapshot.KeysUp.Length > 0)
                || (snapshot.BeatEvents != null && snapshot.BeatEvents.Length > 0);
        }

        private void DeactivateAsyncRuntime()
        {
            if (!_asyncRuntimeActive || _asyncRuntime == null) return;
            _asyncRuntimeActive = false;
            _asyncRuntimeStateReady = false;
            _nextOvRuntimeTickTime = 0f;
            _lastOvTokenUpdateTime = 0f;
            _runtimeTrackerInstanceId = 0;
            _runtimeTrackerCount = 0;
            _runtimeTrackerGeneration++;
            _asyncRuntime.Publish(new OvRuntimeMainSnapshot
            {
                FrameId = ++_asyncRuntimeFrameId,
                Active = false,
                JudgementMode = (int)OvJudgementMode.Normal
            });
            _currentPureCombo = 0;
            _currentPerfectCombo = 0;
        }

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
                DeactivateAsyncRuntime();
                return;
            }

            if (Main.Settings.OverlayerOnlyShowPlaying && !Main.IsGamePlaying() && !Main.Settings.OverlayerEditMode)
            {
                DeactivateAsyncRuntime();
                return;
            }

            long currentRevision = OverlayRenderInvalidator.Revision;
            if (_lastUpdateScanRevision != currentRevision)
            {
                _lastUpdateScanRevision = currentRevision;
                ScanRuntimeInterestFlags();
                ScanDynamicOverlayFlags();
                if (!_hasTokenAnimations)
                {
                    _lastOvTokenUpdateTime = 0f;
                }
            }

            // Early exit if no dynamic content needs updating
            if (!_needsHitTracker && !_needsComboTracker && !_hasTextAnimations && !_hasImageAnimations && !_hasTokenAnimations && !_hasFpsDynamicContent)
            {
                DeactivateAsyncRuntime();
                return;
            }

            _tokenTriggerFrame.Reset();
            bool trackJudgements = _needsHitTracker || _needsComboTracker || _hasComboAnimations;
            _asyncRuntimeActive = true;
            float now = RenderTimelineClock.Time;
            float runtimeInterval = GetOvRuntimeInterval();
            OvRuntimeMainSnapshot snapshot = CaptureRuntimeSnapshot(trackJudgements);
            bool hasTransientInput = HasTransientRuntimeInput(snapshot)
                || (_asyncRuntime != null && _asyncRuntime.HasPendingJudgements);
            bool runtimeTickDue = !_asyncRuntimeStateReady
                || now >= _nextOvRuntimeTickTime
                || hasTransientInput;
            if (runtimeTickDue)
            {
                _asyncRuntime?.Publish(snapshot);
                _nextOvRuntimeTickTime = now + runtimeInterval;
            }
            bool consumedRuntimeResult = ConsumeAsyncRuntimeResults();

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
                            state.CurrentTime += RenderTimelineClock.DeltaTime;
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
                            state.CurrentTime += RenderTimelineClock.DeltaTime;
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

            bool shouldAdvanceTokenRuntime = _hasTokenAnimations
                && _asyncRuntimeStateReady
                && (runtimeTickDue || consumedRuntimeResult);
            if (shouldAdvanceTokenRuntime)
            {
                float tokenDelta = _lastOvTokenUpdateTime > 0f
                    ? Mathf.Max(0f, now - _lastOvTokenUpdateTime)
                    : RenderTimelineClock.DeltaTime;
                _lastOvTokenUpdateTime = now;
                long renderRevision = OverlayRenderInvalidator.Revision;
                if (_tokenAnimationRuntime.Update(
                    Main.Settings.OverlayerTexts,
                    _tokenTriggerFrame,
                    tokenDelta,
                    renderRevision))
                {
                    MarkRenderDirty();
                }
                if (_tokenAnimationRuntime.Update(
                    BuildImageAnimationProxyList(),
                    _tokenTriggerFrame,
                    tokenDelta,
                    renderRevision))
                {
                    MarkRenderDirty();
                }
            }
        }

        public void PreviewTokenAnimation(OverlayerText text)
        {
            _tokenAnimationRuntime.Preview(text);
            MarkRenderDirty();
        }

        public void StopTokenAnimation(OverlayerText text)
        {
            _tokenAnimationRuntime.Stop(text);
            MarkRenderDirty();
        }

        public void PreviewImageNodeAnimation(OverlayerImage image)
        {
            if (image == null) return;
            _tokenAnimationRuntime.Preview(GetImageAnimationProxy(image));
            MarkRenderDirty();
        }

        public void StopImageNodeAnimation(OverlayerImage image)
        {
            if (image == null || !_imageAnimationProxies.TryGetValue(image, out OverlayerText proxy)) return;
            _tokenAnimationRuntime.Stop(proxy);
            MarkRenderDirty();
        }

        private OverlayerText GetImageAnimationProxy(OverlayerImage image)
        {
            if (!_imageAnimationProxies.TryGetValue(image, out OverlayerText proxy))
            {
                proxy = OvImageNodeAnimation.CreateRuntimeProxy(image);
                _imageAnimationProxies[image] = proxy;
            }
            else
            {
                OvImageNodeAnimation.SyncRuntimeProxy(proxy, image);
            }
            return proxy;
        }

        private System.Collections.Generic.List<OverlayerText> BuildImageAnimationProxyList()
        {
            _imageAnimationProxyBuffer.Clear();
            if (Main.Settings == null || Main.Settings.OverlayerImages == null) return _imageAnimationProxyBuffer;
            for (int i = 0; i < Main.Settings.OverlayerImages.Count; i++)
            {
                OverlayerImage image = Main.Settings.OverlayerImages[i];
                if (!HasEnabledImageNodeGraph(image)) continue;
                OverlayerText proxy = GetImageAnimationProxy(image);
                _imageAnimationProxyBuffer.Add(proxy);
            }
            return _imageAnimationProxyBuffer;
        }

        private string ResolveOvTextForRender(OverlayerText ovText, string format)
        {
            if (!OvTokenAnimationRuntime.HasEnabledGraph(ovText))
            {
                return ResolveOvTextTags(ovText, format);
            }

            System.Collections.Generic.List<OvTextSourcePart> parts = OvTextTokenService.EnsureBindings(ovText);
            System.Collections.Generic.Dictionary<string, string> textOverrides = _tokenAnimationRuntime.GetTextOverrides(ovText);
            System.Collections.Generic.Dictionary<string, OvAnimationNode> numberFormats = _tokenAnimationRuntime.GetNumberFormats(ovText);
            _ovTokenTextBuilder.Length = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                OvTextSourcePart part = parts[i];
                if (part.IsStyleControl || part.Token == null)
                {
                    _ovTokenTextBuilder.Append(part.Source);
                    continue;
                }

                string rendered = part.Source;
                if (textOverrides != null && textOverrides.TryGetValue(part.Token.Id, out string replacement))
                {
                    OvTagPlan replacementPlan = GetTransientOvTagPlan(replacement ?? string.Empty);
                    rendered = ResolveOvTextTagBody(replacementPlan);
                }
                else if (part.Token.Kind == OvTextTokenKind.DynamicTag)
                {
                    OvTagPlan tokenPlan = GetTransientOvTagPlan(part.Source);
                    rendered = ResolveOvTextTagBody(tokenPlan);
                    if (numberFormats != null
                        && numberFormats.TryGetValue(part.Token.Id, out OvAnimationNode formatNode))
                    {
                        if (TryFormatTokenNumber(ovText, part.Source, formatNode, rendered, out string numberFormatted))
                        {
                            rendered = numberFormatted;
                        }
                    }
                }
                _ovTokenTextBuilder.Append("<link=\"ct_");
                _ovTokenTextBuilder.Append(part.Token.Id);
                _ovTokenTextBuilder.Append("\">");
                _ovTokenTextBuilder.Append(rendered);
                _ovTokenTextBuilder.Append("</link>");
            }

            if (_ovTokenTextMemo.TryGetValue(ovText, out string memoized)
                && BuilderContentEquals(_ovTokenTextBuilder, memoized))
            {
                return memoized;
            }

            string built = _ovTokenTextBuilder.ToString();
            _ovTokenTextMemo[ovText] = built;
            return built;
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
            PrepareOvRuntimeBake(editMode);
            if (!editMode) _activeOvAlignLines.Clear();
            if (CheryToolsMenu.IsMenuOpen) _activeOvAlignLines.Clear();
            if (Main.Settings.OverlayerOnlyShowPlaying && !Main.IsGamePlaying() && !editMode)
            {
                OverlayerUnityRenderer.HideAll();
                PauseVideoIfNeeded();
                return;
            }

            OverlayerUnityRenderer.BeginFrame();
            _renderMusicCacheReady = false;
            _renderMusicText = string.Empty;
            _renderMusicArtist = string.Empty;
            _renderMusicTitle = string.Empty;
            var texts = Main.Settings.OverlayerTexts;
            bool isPlaying = Main.IsGamePlaying();
            bool hasVideoThisFrame = false;

            for (int i = 0; i < texts.Count; i++)
            {
                var ovText = texts[i];
                if (ovText == null) continue;
                if (!ovText.IsEnabled && !editMode) continue;
                if (!IsItemVisible(ovText.ShowInGame, ovText.OnlyShowPlaying, isPlaying, editMode)) continue;

                string textRenderId = GetIndexedRenderId(_ovTextRenderIds, "ov_text_", i);
                int textSortingOrder = RenderDepth.ToSortingOrder(ovText.Depth, RenderDepth.SublayerText);
                bool canBakeText = CanBakeOvText(ovText, editMode);
                long tokenVisualRevision = _tokenAnimationRuntime.GetVisualRevision(ovText);
                string rawText = ResolveOvTextForRender(ovText, ovText.TextFormat ?? string.Empty);
                if (canBakeText
                    && _bakedOvTexts.TryGetValue(ovText, out BakedOvText bakedText)
                    && string.Equals(bakedText.RenderId, textRenderId, StringComparison.Ordinal)
                    && string.Equals(bakedText.RenderedText, rawText, StringComparison.Ordinal)
                    && bakedText.SortingOrder == textSortingOrder
                    && bakedText.TokenVisualRevision == tokenVisualRevision
                    && SdfTextRenderer.KeepAlive(textRenderId, textSortingOrder))
                {
                    ovText.LastWidth = Mathf.Max(1f, bakedText.Bounds.ContentWidth);
                    ovText.LastHeight = Mathf.Max(1f, bakedText.Bounds.ContentHeight);
                    continue;
                }

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

                System.Numerics.Vector2 screenDisplaySizeForText = ImGuiController.ScreenDisplaySize;
                float textPivotX = Mathf.Clamp01(ovText.PivotX);
                float textLayoutMinWidth = 0f;
                if (ovText.Alignment == 1)
                {
                    textLayoutMinWidth = Mathf.Max(0f, 2f * Mathf.Min(ovText.PositionX, screenDisplaySizeForText.X - ovText.PositionX));
                }
                else if (ovText.Alignment == 2)
                {
                    textLayoutMinWidth = Mathf.Max(0f, ovText.PositionX);
                }
                else
                {
                    textLayoutMinWidth = Mathf.Max(0f, screenDisplaySizeForText.X - ovText.PositionX);
                }
                if (textPivotX > 0.001f && textPivotX < 0.999f)
                {
                    textLayoutMinWidth = Mathf.Max(textLayoutMinWidth, 2f * Mathf.Min(ovText.PositionX / textPivotX, (screenDisplaySizeForText.X - ovText.PositionX) / (1f - textPivotX)));
                }
                textLayoutMinWidth = Mathf.Max(0f, Mathf.Min(screenDisplaySizeForText.X, textLayoutMinWidth));

                OvTextLayoutKey layoutKey = BuildOvTextLayoutKey(ovText, rawText);
                UnityEngine.Vector2 cachedLayoutSize = UnityEngine.Vector2.zero;
                bool useCachedLayoutSize = _ovTextLayoutKeys.TryGetValue(ovText, out OvTextLayoutKey previousLayoutKey)
                    && previousLayoutKey.Equals(layoutKey)
                    && _ovTextStableSizes.TryGetValue(ovText, out cachedLayoutSize);

                SdfTextRenderer.TextBounds textBounds = SdfTextRenderer.DrawOverlayerText(
                    textRenderId,
                    ovText,
                    rawText,
                    animOffsetX,
                    animOffsetY,
                    animScaleXMult,
                    animScaleYMult,
                    Mathf.Max(textLayoutMinWidth, useCachedLayoutSize ? cachedLayoutSize.x : 0f),
                    useCachedLayoutSize ? cachedLayoutSize.y : 0f,
                    useCachedLayoutSize,
                    textSortingOrder,
                    _tokenAnimationRuntime.GetPoses(ovText));

                if (canBakeText)
                {
                    _bakedOvTexts[ovText] = new BakedOvText
                    {
                        RenderId = textRenderId,
                        RenderedText = rawText,
                        SortingOrder = textSortingOrder,
                        TokenVisualRevision = tokenVisualRevision,
                        Bounds = textBounds
                    };
                }
                else
                {
                    _bakedOvTexts.Remove(ovText);
                }

                float contentWindowWidth = Mathf.Max(1f, textBounds.Width);
                float contentWindowHeight = Mathf.Max(1f, textBounds.Height);
                if (!useCachedLayoutSize)
                {
                    float safeScaleX = Mathf.Max(0.001f, animScaleXMult);
                    float safeScaleY = Mathf.Max(0.001f, animScaleYMult);
                    _ovTextLayoutKeys[ovText] = layoutKey;
                    _ovTextStableSizes[ovText] = new UnityEngine.Vector2(contentWindowWidth / safeScaleX, contentWindowHeight / safeScaleY);
                }
                float visualLeft = textBounds.ContentLeft;
                float visualTop = textBounds.ContentTop;
                float visualWidth = Mathf.Max(1f, textBounds.ContentWidth);
                float visualHeight = Mathf.Max(1f, textBounds.ContentHeight);

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
                            // Persist once on release. During the drag the edit-mode
                            // path already forces a full-rate refresh, so the old
                            // per-frame RequestSave only recompiled every token graph
                            // and killed running animations.
                            Main.RequestSave();
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
                            MoveOvTextWithSnapping(ovText, visualWidth, visualHeight, _ovDragStartX + _ovDragTotalDeltaX, _ovDragStartY + _ovDragTotalDeltaY, screenDisplaySize);
                        }
                    }
                }
                else if (_draggingIndex == i)
                {
                    _draggingIndex = -1;
                    _ovDragTotalDeltaX = 0f;
                    _ovDragTotalDeltaY = 0f;
                    Main.RequestSave();
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

                ovText.LastWidth = visualWidth;
                ovText.LastHeight = visualHeight;
                continue;

            }

            var images = Main.Settings.OverlayerImages;
            for (int i = 0; i < images.Count; i++)
            {
                var ovImg = images[i];
                if (ovImg == null) continue;
                if (!ovImg.IsEnabled && !editMode) continue;
                if (!IsItemVisible(ovImg.ShowInGame, ovImg.OnlyShowPlaying, isPlaying, editMode)) continue;

                bool canBakeImage = CanBakeOvImage(ovImg, editMode);
                long tokenVisualRevision = 0;
                if (HasEnabledImageNodeGraph(ovImg))
                {
                    tokenVisualRevision = _tokenAnimationRuntime.GetVisualRevision(GetImageAnimationProxy(ovImg));
                }
                OvImageRenderIds imageRenderIds = GetOvImageRenderIds(i);
                int imageSortingOrder = RenderDepth.ToSortingOrder(ovImg.Depth, RenderDepth.SublayerGraphic);
                if (canBakeImage
                    && _bakedOvImages.TryGetValue(ovImg, out BakedOvImage bakedImage)
                    && string.Equals(bakedImage.RenderId, imageRenderIds.Image, StringComparison.Ordinal)
                    && bakedImage.SortingOrder == imageSortingOrder
                    && bakedImage.TokenVisualRevision == tokenVisualRevision
                    && OverlayerUnityRenderer.KeepImageAlive(imageRenderIds.Image, imageSortingOrder))
                {
                    continue;
                }

                RenderOvImageUnity(i, ovImg, editMode);
                if (canBakeImage && OverlayerUnityRenderer.KeepImageAlive(imageRenderIds.Image, imageSortingOrder))
                {
                    _bakedOvImages[ovImg] = new BakedOvImage
                    {
                        RenderId = imageRenderIds.Image,
                        SortingOrder = imageSortingOrder,
                        TokenVisualRevision = tokenVisualRevision
                    };
                }
                else
                {
                    _bakedOvImages.Remove(ovImg);
                }
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
                    if (!IsItemVisible(ovVideo.ShowInGame, ovVideo.OnlyShowPlaying, isPlaying, editMode)) continue;

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
                    if (!IsItemVisible(bar.ShowInGame, bar.OnlyShowPlaying, isPlaying, editMode)) continue;

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

        public bool ShouldRenderOverlayNow()
        {
            if (!Main.IsEnabled || Main.Settings == null || !Main.Settings.OverlayerSystemEnabled)
                return false;

            bool editMode = Main.Settings.OverlayerEditMode;
            bool isPlaying = Main.IsGamePlaying();
            bool onlyShowPlaying = Main.Settings.OverlayerOnlyShowPlaying;

            if (onlyShowPlaying && !isPlaying && !editMode)
            {
                return false;
            }

            if (HasVisibleOverlayerTexts(isPlaying, editMode)) return true;
            if (HasVisibleOverlayerImages(isPlaying, editMode)) return true;
            if (HasVisibleOverlayerVideos(isPlaying, editMode)) return true;
            if (HasVisibleOverlayerProgressBars(isPlaying, editMode)) return true;
            return false;
        }

        private static bool IsItemVisible(bool showInGame, bool onlyShowPlaying, bool isPlaying, bool editMode)
        {
            if (editMode) return true;
            if (!showInGame && isPlaying) return false;
            if (onlyShowPlaying && !isPlaying) return false;
            return true;
        }

        private static bool HasVisibleOverlayerTexts(bool isPlaying, bool editMode)
        {
            var texts = Main.Settings != null ? Main.Settings.OverlayerTexts : null;
            if (texts == null) return false;
            for (int i = 0; i < texts.Count; i++)
            {
                OverlayerText text = texts[i];
                if (text == null) continue;
                if (!text.IsEnabled && !editMode) continue;
                if (!IsItemVisible(text.ShowInGame, text.OnlyShowPlaying, isPlaying, editMode)) continue;
                return true;
            }
            return false;
        }

        private static bool HasVisibleOverlayerImages(bool isPlaying, bool editMode)
        {
            var images = Main.Settings != null ? Main.Settings.OverlayerImages : null;
            if (images == null) return false;
            for (int i = 0; i < images.Count; i++)
            {
                OverlayerImage image = images[i];
                if (image == null) continue;
                if (!image.IsEnabled && !editMode) continue;
                if (!IsItemVisible(image.ShowInGame, image.OnlyShowPlaying, isPlaying, editMode)) continue;
                return true;
            }
            return false;
        }

        private static bool HasVisibleOverlayerVideos(bool isPlaying, bool editMode)
        {
            var videos = Main.Settings != null ? Main.Settings.OverlayerVideos : null;
            if (videos == null) return false;
            for (int i = 0; i < videos.Count; i++)
            {
                OverlayerVideo video = videos[i];
                if (video == null) continue;
                if (!video.IsEnabled && !editMode) continue;
                if (!IsItemVisible(video.ShowInGame, video.OnlyShowPlaying, isPlaying, editMode)) continue;
                return true;
            }
            return false;
        }

        private static bool HasVisibleOverlayerProgressBars(bool isPlaying, bool editMode)
        {
            var bars = Main.Settings != null ? Main.Settings.OverlayerProgressBars : null;
            if (bars == null) return false;
            for (int i = 0; i < bars.Count; i++)
            {
                OverlayerProgressBar bar = bars[i];
                if (bar == null) continue;
                if (!bar.IsEnabled && !editMode) continue;
                if (!IsItemVisible(bar.ShowInGame, bar.OnlyShowPlaying, isPlaying, editMode)) continue;
                return true;
            }
            return false;
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

            OverlayerText imageAnimationProxy = HasEnabledImageNodeGraph(ovImg)
                ? GetImageAnimationProxy(ovImg)
                : null;
            System.Collections.Generic.Dictionary<string, OvTokenPose> imagePoses
                = imageAnimationProxy != null
                    ? _tokenAnimationRuntime.GetPoses(imageAnimationProxy)
                    : null;
            if (imagePoses != null
                && imagePoses.TryGetValue(OvImageNodeAnimation.TargetId, out OvTokenPose imagePose))
            {
                AccumulateImageNodePose(imagePose, ref animOffsetX, ref animOffsetY,
                    ref animScaleXMult, ref animScaleYMult, ref animOpacityMult, ref animRotationOffset);
            }

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
                        Main.RequestSave();
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
                    }
                }

                OverlayerUnityRenderer.DrawOutlineRect(renderIds.Select, topLeft, size, 0xFF00FF00u, 2f);
            }
            else if (_draggingIndexImg == index)
            {
                _draggingIndexImg = -1;
                _ovDragTotalDeltaX = 0f;
                _ovDragTotalDeltaY = 0f;
                Main.RequestSave();
            }
        }

        internal static void AccumulateImageNodePose(OvTokenPose pose, ref float offsetX, ref float offsetY,
            ref float scaleX, ref float scaleY, ref float opacity, ref float rotation)
        {
            offsetX += pose.OffsetX;
            offsetY += pose.OffsetY;
            scaleX *= pose.ScaleX;
            scaleY *= pose.ScaleY;
            opacity *= pose.Opacity;
            rotation += pose.Rotation;
            if (pose.GroupTransforms == null) return;
            for (int i = 0; i < pose.GroupTransforms.Count; i++)
            {
                OvTokenGroupTransform transform = pose.GroupTransforms[i];
                if (transform == null) continue;
                offsetX += transform.OffsetX;
                offsetY += transform.OffsetY;
                scaleX *= transform.ScaleX;
                scaleY *= transform.ScaleY;
                rotation += transform.Rotation;
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
                        Main.RequestSave();
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
                    }
                }

                OverlayerUnityRenderer.DrawOutlineRect(renderIds.Select, topLeft, size, 0xFF00FF00u, 2f);
            }
            else if (_draggingIndexVideo == index)
            {
                _draggingIndexVideo = -1;
                _ovDragTotalDeltaX = 0f;
                _ovDragTotalDeltaY = 0f;
                Main.RequestSave();
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
                        Main.RequestSave();
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
                    }
                }

                OverlayerUnityRenderer.DrawOutlineRect(renderIds.Select, topLeft, size, 0xFF00FF00u, 2f);
            }
            else if (_draggingIndexBar == index)
            {
                _draggingIndexBar = -1;
                _ovDragTotalDeltaX = 0f;
                _ovDragTotalDeltaY = 0f;
                Main.RequestSave();
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

            // Soft shadows use the shared 9-slice gaussian batch (36 vertices, single
            // draw) instead of the old stack of up to 17 translucent rounded rects.
            // The shadow sublayer sits below the bar's graphic sublayer, and being a
            // separate graphic it no longer re-triangulates when the fill width
            // changes every frame.
            int shadowSortingOrder = RenderDepth.ToSortingOrder(bar.Depth, RenderDepth.SublayerRainShadow);
            OverlayerUnityRenderer.DrawSoftShadowRect(
                shadowTopLeft,
                size,
                PackColor(bar.ShadowColor, opacity),
                softness,
                shadowSortingOrder);
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

            _snapXRefsBuffer.Clear();
            _snapYRefsBuffer.Clear();

            AddOvSnapCandidate(_snapXRefsBuffer, 0f, 0f, displaySize.Y);
            AddOvSnapCandidate(_snapXRefsBuffer, displaySize.X * 0.5f, 0f, displaySize.Y);
            AddOvSnapCandidate(_snapXRefsBuffer, displaySize.X, 0f, displaySize.Y);
            AddOvSnapCandidate(_snapYRefsBuffer, 0f, 0f, displaySize.X);
            AddOvSnapCandidate(_snapYRefsBuffer, displaySize.Y * 0.5f, 0f, displaySize.X);
            AddOvSnapCandidate(_snapYRefsBuffer, displaySize.Y, 0f, displaySize.X);

            AddOvComponentSnapCandidates(_snapXRefsBuffer, _snapYRefsBuffer, ignoreText, ignoreImage, ignoreVideo, ignoreProgressBar);

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

            foreach (var candidate in _snapXRefsBuffer)
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

            foreach (var candidate in _snapYRefsBuffer)
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

    }
}
