using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using CheryTools.Sonnet.Shared;
using static CheryTools.Sonnet.Shared.SonnetUguiKit;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.KeyViewer.Preview
{
    [DefaultExecutionOrder(31000)]
    internal sealed class KvRuntimeHost : MonoBehaviour
    {
        private enum EaseMode
        {
            EaseOutQuad,
            Linear,
            EaseInQuad,
            EaseInOutQuad,
            EaseOutCubic,
            EaseInCubic,
            EaseInOutCubic,
            EaseOutBack
        }

        private struct KeyPollState
        {
            internal bool Pressed;
            internal bool Down;
        }

        /// <summary>
        /// Configuration-only values compiled once per visual revision. Runtime
        /// frames consume this compact state instead of repeatedly resolving
        /// inheritance, parsing easing strings and converting color arrays.
        /// </summary>
        private sealed class BakedNode
        {
            internal bool IsInput;
            internal string Label;
            internal int StatisticType;
            internal bool ShowText;
            internal bool HideCount;
            internal float Width;
            internal float Height;
            internal float Radius;
            internal float Border;
            internal Color BackgroundNormal;
            internal Color BackgroundPressed;
            internal Color BorderNormal;
            internal Color BorderPressed;
            internal Color TextNormal;
            internal Color TextPressed;
            internal bool AnimationEnabled;
            internal float AnimationDuration;
            internal float AnimationScale;
            internal float AnimationOffsetX;
            internal float AnimationOffsetY;
            internal bool AnimationAffectColors;
            internal EaseMode Easing;

            internal bool RainEnabled;
            internal float RainWidthRatio;
            internal float RainYOffset;
            internal float RainRadius;
            internal Color RainNear;
            internal Color RainFar;
            internal Color RainRight;
            internal bool RainGradientEnabled;
            internal bool RainHeightGradient;
            internal bool RainHorizontalGradient;
            internal int RainFadeMode;
            internal float RainGradientHeight;
            internal float RainGradientPower;
            internal float RainFadeHeight;
            internal float RainFadePower;
            internal bool RainOutlineEnabled;
            internal Color RainOutlineColor;
            internal float RainOutlineThickness;
            internal bool RainShadowEnabled;
            internal Color RainShadowColor;
            internal float RainShadowOffsetX;
            internal float RainShadowOffsetY;
            internal float RainShadowSoftness;
            internal float RainShadowStrength;
        }

        private sealed class KeyView
        {
            internal KvKey Model;
            internal RectTransform Rect;
            internal RoundedRectGraphic Graphic;
            internal TextMeshProUGUI Label;
            internal TextMeshProUGUI Count;
            internal RawImage Media;
            internal Texture2D ImageTexture;
            internal RenderTexture VideoTexture;
            internal Component VideoPlayer;
            internal Vector2 BasePosition;
            internal float PressProgress;
            internal bool LastPressed;
            internal bool Pressed;
            internal bool VisualDirty = true;
            internal float LastVisualScale = float.NaN;
            internal Vector2 LastPressOffset = new Vector2(float.NaN, float.NaN);
            internal int LastCount = int.MinValue;
            internal bool VideoStarted;
            internal bool LastVideoLoop;
            internal int LastMediaFit = -1;
            internal BakedNode Baked;
            internal readonly TextEffectCache LabelEffects = new TextEffectCache();
            internal readonly TextEffectCache CountEffects = new TextEffectCache();
        }

        private sealed class TextEffectCache
        {
            private bool _initialized;
            private TMP_FontAsset _font;
            private bool _outlineEnabled;
            private Color _outlineColor;
            private float _outlineThickness;
            private bool _shadowEnabled;
            private Color _shadowColor;
            private float _shadowX;
            private float _shadowY;
            private float _shadowSoftness;
            private float _fontSize;

            internal void Invalidate() => _initialized = false;

            internal bool Matches(TextMeshProUGUI text, bool outlineEnabled, Color outlineColor,
                float outlineThickness, bool shadowEnabled, Color shadowColor, float[] shadowOffset,
                float shadowSoftness)
            {
                float shadowX = shadowOffset != null && shadowOffset.Length > 0 ? shadowOffset[0] : 2f;
                float shadowY = shadowOffset != null && shadowOffset.Length > 1 ? shadowOffset[1] : -2f;
                return _initialized
                    && _font == text.font
                    && _fontSize.Equals(text.fontSize)
                    && _outlineEnabled == outlineEnabled
                    && _outlineColor.Equals(outlineColor)
                    && _outlineThickness.Equals(outlineThickness)
                    && _shadowEnabled == shadowEnabled
                    && _shadowColor.Equals(shadowColor)
                    && _shadowX.Equals(shadowX)
                    && _shadowY.Equals(shadowY)
                    && _shadowSoftness.Equals(shadowSoftness);
            }

            internal void Capture(TextMeshProUGUI text, bool outlineEnabled, Color outlineColor,
                float outlineThickness, bool shadowEnabled, Color shadowColor, float[] shadowOffset,
                float shadowSoftness)
            {
                _initialized = true;
                _font = text.font;
                _fontSize = text.fontSize;
                _outlineEnabled = outlineEnabled;
                _outlineColor = outlineColor;
                _outlineThickness = outlineThickness;
                _shadowEnabled = shadowEnabled;
                _shadowColor = shadowColor;
                _shadowX = shadowOffset != null && shadowOffset.Length > 0 ? shadowOffset[0] : 2f;
                _shadowY = shadowOffset != null && shadowOffset.Length > 1 ? shadowOffset[1] : -2f;
                _shadowSoftness = shadowSoftness;
            }
        }

        private sealed class RainSegment
        {
            internal int RowIndex;
            internal KvKey Key;
            internal KeyView KeyView;
            internal RectTransform Rect;
            internal RoundedRectGraphic Graphic;
            internal RectTransform ShadowRect;
            internal SoftShadowGraphic ShadowGraphic;
            internal float StartTime;
            internal float EndTime = -1f;
        }

        private sealed class ProfileView
        {
            internal KvProfile Model;
            internal RectTransform Root;
            internal readonly RectTransform[] RainLayers = new RectTransform[4];
            internal readonly RectTransform[] RainShadowLayers = new RectTransform[4];
            internal readonly RectTransform[] RainBarLayers = new RectTransform[4];
            internal RectTransform KeyLayer;
            internal readonly List<KeyView> Keys = new List<KeyView>();
            internal readonly List<RainSegment> ActiveRain = new List<RainSegment>();
            internal readonly Stack<RainSegment>[] RainPools = new[]
            {
                new Stack<RainSegment>(),
                new Stack<RainSegment>(),
                new Stack<RainSegment>(),
                new Stack<RainSegment>()
            };
            internal readonly Dictionary<string, RainSegment> HeldRain = new Dictionary<string, RainSegment>(StringComparer.Ordinal);
            internal readonly Queue<float> KpsTimes = new Queue<float>();
            internal int VisualRevision = -1;
            internal bool DynamicDirty = true;
            internal float Scale = 1f;
            internal float RainMaxHeight;
            internal float RainSpeed;
        }

        private readonly Dictionary<string, ProfileView> _profiles = new Dictionary<string, ProfileView>(StringComparer.Ordinal);
        private readonly List<ProfileView> _profileViewList = new List<ProfileView>();
        private readonly HashSet<KeyCode> _countedThisFrame = new HashSet<KeyCode>();
        private readonly List<KeyCode> _inputKeys = new List<KeyCode>();
        private readonly HashSet<KeyCode> _inputKeySet = new HashSet<KeyCode>();
        private readonly Dictionary<KeyCode, KeyPollState> _polledKeys = new Dictionary<KeyCode, KeyPollState>();
        private readonly Dictionary<string, TMP_FontAsset> _fontCache = new Dictionary<string, TMP_FontAsset>(StringComparer.OrdinalIgnoreCase);
        private readonly List<TMP_FontAsset> _createdFontAssets = new List<TMP_FontAsset>();
        private readonly List<Font> _createdFontSources = new List<Font>();
        private readonly Dictionary<TextMeshProUGUI, Material> _textMaterials = new Dictionary<TextMeshProUGUI, Material>();
        private int _builtRevision = -1;
        private int _builtInputRevision = -1;
        private Font _font;
        private TMP_FontAsset _fontAsset;
        private bool _hasTrackedPressed;
        private bool _runtimeContextInitialized;
        private bool _lastPlaying;
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;
        private static Func<Texture2D, byte[], bool> _loadImage;
        private static bool _loadImageResolved;
        private static readonly int OutlineWidthProperty = ShaderUtilities.ID_OutlineWidth;
        private static readonly int OutlineColorProperty = ShaderUtilities.ID_OutlineColor;
        private static readonly int OutlineSoftnessProperty = Shader.PropertyToID("_OutlineSoftness");
        private static readonly int UnderlayColorProperty = Shader.PropertyToID("_UnderlayColor");
        private static readonly int UnderlayOffsetXProperty = Shader.PropertyToID("_UnderlayOffsetX");
        private static readonly int UnderlayOffsetYProperty = Shader.PropertyToID("_UnderlayOffsetY");
        private static readonly int UnderlayDilateProperty = Shader.PropertyToID("_UnderlayDilate");
        private static readonly int UnderlaySoftnessProperty = Shader.PropertyToID("_UnderlaySoftness");

        private void Awake()
        {
            RectTransform rect = transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _fontAsset = CreateFontAsset(_font, "CheryTools_KV_DefaultFont");
            if (_fontAsset == null)
                _fontAsset = TMP_Settings.defaultFontAsset;
        }

        private void OnEnable()
        {
            _builtRevision = -1;
            _runtimeContextInitialized = false;
        }

        private void OnDisable()
        {
            _hasTrackedPressed = false;
            _polledKeys.Clear();
            foreach (ProfileView profile in _profiles.Values)
            {
                foreach (KeyView key in profile.Keys)
                {
                    key.LastPressed = false;
                    key.Pressed = false;
                    key.VisualDirty = true;
                }
                profile.DynamicDirty = true;
                profile.HeldRain.Clear();
                for (int i = 0; i < profile.ActiveRain.Count; i++) profile.ActiveRain[i].EndTime = SonnetClock.Now;
            }
        }

        private void Update()
        {
            KvSettings settings = KvRuntime.Settings;
            if (!KvRuntime.IsActive || settings == null || !settings.Enabled) return;
            EnsureBuilt(settings);
            float now = SonnetClock.Now;
            bool kpsDue = false;
            for (int p = 0; p < settings.Profiles.Count; p++)
            {
                KvProfile candidate = settings.Profiles[p];
                ProfileView candidateView;
                if (candidate == null || !candidate.Enabled || !_profiles.TryGetValue(candidate.Id, out candidateView)
                    || candidateView.KpsTimes.Count == 0) continue;
                if (candidateView.KpsTimes.Peek() < now - settings.KpsWindowSeconds)
                {
                    kpsDue = true;
                    break;
                }
            }

            bool pollInput = _hasTrackedPressed || Input.anyKey || Input.anyKeyDown;
            if (!pollInput && !kpsDue) return;
            if (pollInput) PollInputStates();
            else _polledKeys.Clear();

            bool anyCountsChanged = false;
            for (int p = 0; p < settings.Profiles.Count; p++)
            {
                KvProfile profile = settings.Profiles[p];
                // 统计和显示是两条独立的路径。ShowInGame / OnlyShowPlaying
                // 只决定 LateUpdate 是否绘制，不能让隐藏的配置停止接收输入，
                // 否则切回显示状态后 KPS 会丢失这一段时间的数据。
                if (profile == null || !profile.Enabled)
                {
                    if (profile != null)
                    {
                        profile.CurrentKps = 0;
                        ProfileView disabledView;
                        if (_profiles.TryGetValue(profile.Id, out disabledView)) disabledView.KpsTimes.Clear();
                    }
                    continue;
                }
                ProfileView view;
                if (!_profiles.TryGetValue(profile.Id, out view))
                {
                    profile.CurrentKps = 0;
                    continue;
                }
                _countedThisFrame.Clear();
                bool countsChanged = false;
                if (pollInput)
                {
                    for (int i = 0; i < view.Keys.Count; i++)
                    {
                        KeyView keyView = view.Keys[i];
                        KvKey key = keyView.Model;
                        BakedNode baked = keyView.Baked;
                        if (baked == null || !baked.IsInput) continue;
                        KeyCode bind = key.Bind;
                        KeyPollState state;
                        _polledKeys.TryGetValue(bind, out state);
                        bool down = state.Down || (state.Pressed && !keyView.LastPressed);
                        if (down)
                        {
                            key.HitCount++;
                            if (_countedThisFrame.Add(bind))
                            {
                                profile.TotalHits++;
                                view.KpsTimes.Enqueue(now);
                            }
                            if (baked.RainEnabled) BeginRain(view, keyView);
                            countsChanged = true;
                        }
                        // 不依赖 GetKeyUp。卡顿跨帧时松开事件可能丢失，但只要当前
                        // 物理状态已经松开，就必须结束仍在持续的雨段。
                        if (!state.Pressed) EndRain(view, key);
                        if (keyView.LastPressed != state.Pressed)
                        {
                            keyView.LastPressed = state.Pressed;
                            keyView.Pressed = state.Pressed;
                            keyView.VisualDirty = true;
                            view.DynamicDirty = true;
                        }
                    }
                }
                float cutoff = now - settings.KpsWindowSeconds;
                while (view.KpsTimes.Count > 0 && view.KpsTimes.Peek() < cutoff) view.KpsTimes.Dequeue();
                int currentKps = view.KpsTimes.Count;
                if (profile.CurrentKps != currentKps)
                {
                    profile.CurrentKps = currentKps;
                    view.DynamicDirty = true;
                }
                if (countsChanged)
                {
                    view.DynamicDirty = true;
                    anyCountsChanged = true;
                }
            }
            if (anyCountsChanged) KvRuntime.CountsChanged();
        }

        private void LateUpdate()
        {
            KvSettings settings = KvRuntime.Settings;
            if (settings == null) return;
            EnsureBuilt(settings);
            bool playing = KvRuntime.IsPlaying();
            bool runtimeContextChanged = !_runtimeContextInitialized
                || _lastPlaying != playing
                || _lastScreenWidth != Screen.width
                || _lastScreenHeight != Screen.height;
            if (runtimeContextChanged)
            {
                _runtimeContextInitialized = true;
                _lastPlaying = playing;
                _lastScreenWidth = Screen.width;
                _lastScreenHeight = Screen.height;
            }
            for (int p = 0; p < _profileViewList.Count; p++)
            {
                ProfileView view = _profileViewList[p];
                bool visible = KvRuntime.IsActive && settings.Enabled && ShouldRun(view.Model, settings, playing);
                bool becameVisible = visible && !view.Root.gameObject.activeSelf;
                if (view.Root.gameObject.activeSelf != visible)
                {
                    if (!visible) ResetViewState(view);
                    view.Root.gameObject.SetActive(visible);
                }
                bool refreshRuntimeVisuals = runtimeContextChanged || becameVisible;
                if (refreshRuntimeVisuals) InvalidateRuntimeVisuals(view);
                if (!visible) continue;
                bool force = refreshRuntimeVisuals || view.VisualRevision != KvRuntime.VisualRevision;
                if (!force && !view.DynamicDirty && view.ActiveRain.Count == 0 && !HasPendingVideo(view)) continue;
                UpdateProfile(view, force, force || refreshRuntimeVisuals);
                if (force) view.VisualRevision = KvRuntime.VisualRevision;
            }
        }

        private static void InvalidateRuntimeVisuals(ProfileView view)
        {
            if (view == null) return;
            view.VisualRevision = -1;
            view.DynamicDirty = true;
            for (int i = 0; i < view.Keys.Count; i++)
            {
                KeyView key = view.Keys[i];
                key.VisualDirty = true;
                key.LastVisualScale = float.NaN;
                key.LastPressOffset = new Vector2(float.NaN, float.NaN);
                key.LabelEffects.Invalidate();
                key.CountEffects.Invalidate();
                if (key.Label != null) key.Label.SetAllDirty();
                if (key.Count != null) key.Count.SetAllDirty();
                if (key.Graphic != null) key.Graphic.SetAllDirty();
            }
        }

        internal void InvalidateAll()
        {
            for (int p = 0; p < _profileViewList.Count; p++)
            {
                InvalidateRuntimeVisuals(_profileViewList[p]);
            }
        }

        internal void ClearRuntime()
        {
            for (int p = 0; p < _profileViewList.Count; p++)
            {
                ProfileView view = _profileViewList[p];
                if (view.Root != null)
                {
                    for (int k = 0; k < view.Keys.Count; k++) ReleaseMedia(view.Keys[k]);
                    view.Root.gameObject.SetActive(false);
                    Destroy(view.Root.gameObject);
                }
            }
            _profiles.Clear();
            _profileViewList.Clear();
            _inputKeys.Clear();
            _inputKeySet.Clear();
            _polledKeys.Clear();
            _hasTrackedPressed = false;
            _builtRevision = -1;
            _builtInputRevision = -1;
        }

        private void EnsureBuilt(KvSettings settings)
        {
            if (_builtRevision != KvRuntime.LayoutRevision)
            {
                ClearRuntime();
                for (int i = 0; i < settings.Profiles.Count; i++) BuildProfile(settings.Profiles[i]);
                RebuildInputBindings();
                _builtRevision = KvRuntime.LayoutRevision;
                _builtInputRevision = KvRuntime.InputRevision;
                return;
            }
            if (_builtInputRevision == KvRuntime.InputRevision) return;
            RebuildInputBindings();
            _builtInputRevision = KvRuntime.InputRevision;
        }

        private void BuildProfile(KvProfile profile)
        {
            var view = new ProfileView { Model = profile };
            view.Root = CreateRect("KV_Profile_" + profile.Id, transform);
            view.Root.anchorMin = view.Root.anchorMax = new Vector2(0.5f, 0.5f);
            view.Root.pivot = new Vector2(0.5f, 0.5f);
            view.Root.anchoredPosition = Pixel(new Vector2(profile.OffsetX, profile.OffsetY));
            // 不再缩放已经栅格化的 UGUI 子树。所有最终尺寸都在创建/更新时
            // 烘焙 profile.Scale，避免文字和圆角被二次采样后发糊。
            view.Root.localScale = Vector3.one;
            IsolateCanvas(view.Root);
            for (int r = 0; r < 4; r++)
            {
                view.RainLayers[r] = CreateRect("Rain_Row" + (r + 1), view.Root);
                Stretch(view.RainLayers[r]);
                IsolateCanvas(view.RainLayers[r]);
                view.RainShadowLayers[r] = CreateRect("Shadows", view.RainLayers[r]);
                Stretch(view.RainShadowLayers[r]);
                view.RainBarLayers[r] = CreateRect("Bars", view.RainLayers[r]);
                Stretch(view.RainBarLayers[r]);
            }
            view.KeyLayer = view.Root;

            var ordered = new List<KvKey>(profile.Keys);
            ordered.Sort((left, right) => left.Depth.CompareTo(right.Depth));
            for (int i = 0; i < ordered.Count; i++)
            {
                KvKey key = ordered[i];
                float scale = Mathf.Max(0.01f, profile.Scale);
                float width = Pixel(Resolve(key.Width, profile.KeyWidth) * scale);
                float height = Pixel(Resolve(key.Height, profile.KeyHeight) * scale);
                RectTransform keyRect = CreateRect("Key_" + key.Id, view.Root);
                keyRect.anchorMin = keyRect.anchorMax = new Vector2(0.5f, 0.5f);
                keyRect.pivot = new Vector2(0.5f, 0.5f);
                keyRect.sizeDelta = new Vector2(width, height);
                keyRect.anchoredPosition = Pixel(new Vector2(key.PositionX * scale, key.PositionY * scale));
                var graphic = keyRect.gameObject.AddComponent<RoundedRectGraphic>();
                graphic.raycastTarget = false;
                RawImage media = null;
                Texture2D imageTexture = null;
                RenderTexture videoTexture = null;
                Component videoPlayer = null;
                if (key.NodeType == 3 || key.NodeType == 4)
                    CreateMedia(key, keyRect, width, height, out media, out imageTexture, out videoTexture, out videoPlayer);
                TMP_FontAsset labelFont = ResolveProfileFont(string.IsNullOrWhiteSpace(key.LabelFontPath) ? profile.FontPath : key.LabelFontPath);
                TMP_FontAsset countFont = ResolveProfileFont(string.IsNullOrWhiteSpace(key.CountFontPath) ? profile.FontPath : key.CountFontPath);
                TextMeshProUGUI label = CreateText("Label", keyRect, Resolve(key.LabelSize, profile.LabelSize) * scale, FontStyle.Normal, labelFont);
                TextMeshProUGUI count = CreateText("Count", keyRect, Resolve(key.CountSize, profile.CountSize) * scale, FontStyle.Normal, countFont);
                var keyView = new KeyView
                {
                    Model = key, Rect = keyRect, Graphic = graphic, Label = label, Count = count,
                    Media = media, ImageTexture = imageTexture, VideoTexture = videoTexture, VideoPlayer = videoPlayer,
                    BasePosition = Pixel(new Vector2(key.PositionX * scale, key.PositionY * scale))
                };
                view.Keys.Add(keyView);
            }

            // 根据每排雨滴独立深度与按键深度，统一编排层级
            int[] rainDepths = { profile.RainDepth1, profile.RainDepth2, profile.RainDepth3, profile.RainDepth4 };
            var layerElements = new List<(Transform transform, int depth, int subOrder)>(4 + view.Keys.Count);
            for (int r = 0; r < 4; r++)
            {
                layerElements.Add((view.RainLayers[r], rainDepths[r], 10 + r * 10));
            }
            for (int i = 0; i < view.Keys.Count; i++)
            {
                layerElements.Add((view.Keys[i].Rect, view.Keys[i].Model.Depth, 50 + i));
            }
            layerElements.Sort((a, b) =>
            {
                int cmp = a.depth.CompareTo(b.depth);
                return cmp != 0 ? cmp : a.subOrder.CompareTo(b.subOrder);
            });
            for (int i = 0; i < layerElements.Count; i++)
            {
                layerElements[i].transform.SetAsLastSibling();
            }

            CompileProfile(view);
            InvalidateRuntimeVisuals(view);
            _profiles[profile.Id] = view;
            _profileViewList.Add(view);
        }

        private void CompileProfile(ProfileView view)
        {
            KvProfile profile = view.Model;
            float scale = Mathf.Max(0.01f, profile.Scale);
            view.Scale = scale;
            view.RainMaxHeight = profile.RainMaxHeight;
            view.RainSpeed = profile.RainSpeed;
            for (int i = 0; i < view.Keys.Count; i++)
            {
                KeyView key = view.Keys[i];
                KvKey node = key.Model;
                bool custom = node.UseCustomStyle;
                bool customAnimation = node.UseCustomAnimation;
                bool customRain = node.UseCustomRain;
                int row = node.RainRow <= 0 ? 1 : node.RainRow;
                float opacity = Mathf.Clamp01(node.Opacity);
                string label = node.Label;
                if (label == null) label = node.NodeType == 1 ? "KPS" : node.NodeType == 2 ? "TOTAL" : string.Empty;

                Color backgroundNormal = ToColor(custom ? node.BackgroundNormal : profile.BackgroundNormal);
                Color backgroundPressed = ToColor(custom ? node.BackgroundPressed : profile.BackgroundPressed);
                Color borderNormal = ToColor(custom ? node.BorderNormal : profile.BorderNormal);
                Color borderPressed = ToColor(custom ? node.BorderPressed : profile.BorderPressed);
                Color textNormal = ToColor(custom ? node.TextNormal : profile.TextNormal);
                Color textPressed = ToColor(custom ? node.TextPressed : profile.TextPressed);
                backgroundNormal.a *= opacity;
                backgroundPressed.a *= opacity;
                borderNormal.a *= opacity;
                borderPressed.a *= opacity;
                textNormal.a *= opacity;
                textPressed.a *= opacity;

                Color rainNear = ToColor(customRain ? node.RainColor : row == 2 ? profile.RainColor2 : row == 3 ? profile.RainColor3 : row == 4 ? profile.RainColor4 : profile.RainColor);
                Color rainFar = ToColor(customRain ? node.RainEndColor : row == 2 ? profile.RainEndColor2 : row == 3 ? profile.RainEndColor3 : row == 4 ? profile.RainEndColor4 : profile.RainEndColor);
                Color rainRight = ToColor(customRain ? node.RainRightColor : row == 2 ? profile.RainRightColor2 : row == 3 ? profile.RainRightColor3 : row == 4 ? profile.RainRightColor4 : profile.RainRightColor);
                rainRight.a = rainNear.a;
                float[] shadowOffset = customRain ? node.RainShadowOffset : profile.RainShadowOffset;

                key.Baked = new BakedNode
                {
                    IsInput = KvSettings.IsInputNode(node),
                    Label = label,
                    StatisticType = node.NodeType == 1 ? 1 : node.NodeType == 2 ? 2 : 0,
                    ShowText = !KvSettings.IsMediaNode(node)
                        && (node.NodeType == 1 || node.NodeType == 2 || KvSettings.IsInputNode(node) || !string.IsNullOrEmpty(label)),
                    HideCount = KvSettings.HidesCount(profile, node),
                    Width = Resolve(node.Width, profile.KeyWidth),
                    Height = Resolve(node.Height, profile.KeyHeight),
                    Radius = custom ? Resolve(node.CornerRadius, profile.CornerRadius) : profile.CornerRadius,
                    Border = custom ? Resolve(node.BorderThickness, profile.BorderThickness) : profile.BorderThickness,
                    BackgroundNormal = backgroundNormal,
                    BackgroundPressed = backgroundPressed,
                    BorderNormal = borderNormal,
                    BorderPressed = borderPressed,
                    TextNormal = textNormal,
                    TextPressed = textPressed,
                    AnimationEnabled = customAnimation ? node.PressAnimationEnabled : profile.PressAnimationEnabled,
                    AnimationDuration = Mathf.Max(0.01f, customAnimation ? node.PressAnimationDuration : profile.PressAnimationDuration),
                    AnimationScale = customAnimation ? node.PressAnimationScale : profile.PressAnimationScale,
                    AnimationOffsetX = (customAnimation ? node.PressAnimationOffsetX : profile.PressAnimationOffsetX) * scale,
                    AnimationOffsetY = (customAnimation ? node.PressAnimationOffsetY : profile.PressAnimationOffsetY) * scale,
                    AnimationAffectColors = customAnimation ? node.PressAnimationAffectColors : profile.PressAnimationAffectColors,
                    Easing = ParseEase(customAnimation ? node.PressAnimationEasing : profile.PressAnimationEasing),
                    RainEnabled = (customRain ? node.RainEnabled : profile.RainEnabled) && node.RainRow != 0,
                    RainWidthRatio = customRain ? node.RainWidthRatio : row == 2 ? profile.RainWidthRatio2 : row == 3 ? profile.RainWidthRatio3 : row == 4 ? profile.RainWidthRatio4 : profile.RainWidthRatio,
                    RainYOffset = customRain ? node.RainYOffset : row == 2 ? profile.RainYOffset2 : row == 3 ? profile.RainYOffset3 : row == 4 ? profile.RainYOffset4 : profile.RainYOffset,
                    RainRadius = (customRain ? node.RainCornerRadius : profile.RainCornerRadius) * scale,
                    RainNear = rainNear,
                    RainFar = rainFar,
                    RainRight = rainRight,
                    RainGradientEnabled = customRain ? node.RainGradientEnabled : profile.RainGradientEnabled,
                    RainHeightGradient = (customRain ? node.RainGradientMode : profile.RainGradientMode) == 1,
                    RainHorizontalGradient = customRain ? node.RainHorizontalGradientEnabled : profile.RainHorizontalGradientEnabled,
                    RainFadeMode = customRain ? node.RainFadeMode : profile.RainFadeMode,
                    RainGradientHeight = customRain ? node.RainGradientHeight : profile.RainGradientHeight,
                    RainGradientPower = customRain ? node.RainGradientPower : profile.RainGradientPower,
                    RainFadeHeight = customRain ? node.RainFadeHeight : profile.RainFadeHeight,
                    RainFadePower = customRain ? node.RainFadePower : profile.RainFadePower,
                    RainOutlineEnabled = customRain ? node.RainOutlineEnabled : profile.RainOutlineEnabled,
                    RainOutlineColor = ToColor(customRain ? node.RainOutlineColor : profile.RainOutlineColor),
                    RainOutlineThickness = (customRain ? node.RainOutlineThickness : profile.RainOutlineThickness) * scale,
                    RainShadowEnabled = customRain ? node.RainShadowEnabled : profile.RainShadowEnabled,
                    RainShadowColor = ToColor(customRain ? node.RainShadowColor : profile.RainShadowColor),
                    RainShadowOffsetX = shadowOffset != null && shadowOffset.Length > 0 ? shadowOffset[0] * scale : 0f,
                    RainShadowOffsetY = shadowOffset != null && shadowOffset.Length > 1 ? shadowOffset[1] * scale : 0f,
                    RainShadowSoftness = (customRain ? node.RainShadowSoftness : profile.RainShadowSoftness) * scale,
                    RainShadowStrength = Mathf.Clamp01(customRain ? node.RainShadowStrength : profile.RainShadowStrength)
                };
                key.BasePosition = Pixel(new Vector2(node.PositionX * scale, node.PositionY * scale));
                key.VisualDirty = true;
            }
            view.DynamicDirty = true;
        }

        private void RebuildInputBindings()
        {
            _inputKeys.Clear();
            _inputKeySet.Clear();
            foreach (ProfileView view in _profiles.Values)
            {
                for (int i = 0; i < view.Keys.Count; i++)
                {
                    KvKey model = view.Keys[i].Model;
                    if (KvSettings.IsInputNode(model) && model.Bind != KeyCode.None && _inputKeySet.Add(model.Bind))
                        _inputKeys.Add(model.Bind);
                }
            }
        }

        private void PollInputStates()
        {
            _polledKeys.Clear();
            bool anyPressed = false;
            for (int i = 0; i < _inputKeys.Count; i++)
            {
                KeyCode code = _inputKeys[i];
                bool pressed = Input.GetKey(code);
                _polledKeys[code] = new KeyPollState { Pressed = pressed, Down = Input.GetKeyDown(code) };
                anyPressed |= pressed;
            }
            _hasTrackedPressed = anyPressed;
        }

        private static bool HasPendingVideo(ProfileView view)
        {
            for (int i = 0; i < view.Keys.Count; i++)
            {
                KeyView key = view.Keys[i];
                if (key.VideoPlayer != null && !key.VideoStarted) return true;
            }
            return false;
        }

        private void UpdateProfile(ProfileView view, bool force, bool refreshTextMeshes = false)
        {
            KvProfile profile = view.Model;
            if (force) CompileProfile(view);
            float globalScale = view.Scale;
            if (force)
            {
                view.Root.anchoredPosition = Pixel(new Vector2(profile.OffsetX, profile.OffsetY));
                view.Root.localScale = Vector3.one;
            }
            bool keepDynamic = false;
            for (int i = 0; i < view.Keys.Count; i++)
            {
                KeyView key = view.Keys[i];
                KvKey node = key.Model;
                BakedNode baked = key.Baked;
                if (baked == null) continue;
                float target = key.Pressed ? 1f : 0f;
                float previousProgress = key.PressProgress;
                key.PressProgress = baked.AnimationEnabled
                    ? Mathf.MoveTowards(key.PressProgress, target, SonnetClock.DeltaTime / baked.AnimationDuration)
                    : target;
                bool progressChanged = !Mathf.Approximately(previousProgress, key.PressProgress);
                int displayCount = baked.StatisticType == 1 ? profile.CurrentKps
                    : baked.StatisticType == 2 ? profile.TotalHits : node.HitCount;
                bool needsVisual = force || key.VisualDirty || progressChanged || key.LastCount != displayCount;
                if (!Mathf.Approximately(key.PressProgress, target)) keepDynamic = true;
                if (!needsVisual)
                {
                    if (key.VideoPlayer != null && !key.VideoStarted) UpdateVideoPlayback(key);
                    continue;
                }

                float eased = Ease(key.PressProgress, baked.Easing);
                float pressScale = baked.AnimationEnabled ? Mathf.Lerp(1f, baked.AnimationScale, eased) : 1f;
                float pressX = baked.AnimationEnabled ? Mathf.Lerp(0f, baked.AnimationOffsetX, eased) : 0f;
                float pressY = baked.AnimationEnabled ? Mathf.Lerp(0f, baked.AnimationOffsetY, eased) : 0f;
                float visualScale = globalScale * pressScale;
                bool scaleChanged = force || !Mathf.Approximately(key.LastVisualScale, visualScale);
                Vector2 pressOffset = new Vector2(pressX, pressY);
                bool offsetChanged = force || key.LastPressOffset != pressOffset;
                if (scaleChanged)
                {
                    key.LastVisualScale = visualScale;
                    key.Rect.localScale = Vector3.one;
                    key.Rect.sizeDelta = Pixel(new Vector2(baked.Width * visualScale, baked.Height * visualScale));
                }
                if (offsetChanged)
                {
                    key.LastPressOffset = pressOffset;
                    key.Rect.anchoredPosition = Pixel(key.BasePosition + pressOffset);
                }
                float colorProgress = baked.AnimationEnabled && baked.AnimationAffectColors ? eased : key.Pressed ? 1f : 0f;
                Color backgroundColor = Color.Lerp(baked.BackgroundNormal, baked.BackgroundPressed, colorProgress);
                Color borderColor = Color.Lerp(baked.BorderNormal, baked.BorderPressed, colorProgress);
                Color textColor = Color.Lerp(baked.TextNormal, baked.TextPressed, colorProgress);
                key.Graphic.SetStyle(backgroundColor, borderColor, baked.Border * visualScale, baked.Radius * visualScale);
                key.Label.color = textColor;
                key.Count.color = textColor;
                if (key.Media != null) key.Media.color = new Color(1f, 1f, 1f, node.Opacity);
                if (force)
                {
                    if (!string.Equals(key.Label.text, baked.Label, StringComparison.Ordinal)) key.Label.text = baked.Label;
                    if (key.Label.gameObject.activeSelf != baked.ShowText) key.Label.gameObject.SetActive(baked.ShowText);
                    bool showCount = baked.ShowText && !baked.HideCount;
                    if (key.Count.gameObject.activeSelf != showCount) key.Count.gameObject.SetActive(showCount);
                }
                if (baked.ShowText && (force || scaleChanged))
                {
                    UpdateTextLayout(key, profile, node, visualScale);
                    // Font size affects the TMP underlay scale, so effects only
                    // need reevaluation when the configuration or visual scale changed.
                    UpdateTextEffects(key, profile, node, visualScale);
                    if (refreshTextMeshes)
                    {
                        key.Label.ForceMeshUpdate(true, true);
                        if (key.Count.gameObject.activeSelf) key.Count.ForceMeshUpdate(true, true);
                    }
                }
                if (force || scaleChanged) UpdateMediaLayout(key, node, visualScale, Resolve(node.BorderThickness, view.Model.BorderThickness));
                if (key.VideoPlayer != null && !key.VideoStarted) UpdateVideoPlayback(key);
                if (key.LastCount != displayCount)
                {
                    key.LastCount = displayCount;
                    SetCountText(key.Count, displayCount);
                }
                key.VisualDirty = false;
            }
            view.DynamicDirty = keepDynamic;
            if (view.ActiveRain.Count > 0) UpdateRain(view);
        }

        private static void BeginRain(ProfileView profile, KeyView key)
        {
            if (key.Baked == null || !key.Baked.RainEnabled) return;
            float now = SonnetClock.Now;
            // 若卡顿让上一次松开未被消费，新一次按下前先强制闭合旧雨段。
            // 否则 HeldRain 会被新段覆盖，旧段失去引用并永久保持激活。
            for (int i = profile.ActiveRain.Count - 1; i >= 0; i--)
            {
                RainSegment previous = profile.ActiveRain[i];
                if (previous != null && ReferenceEquals(previous.Key, key.Model) && previous.EndTime < 0f)
                    previous.EndTime = now;
            }
            profile.HeldRain.Remove(key.Model.Id);
            int row = Mathf.Clamp(key.Model.RainRow <= 0 ? 1 : key.Model.RainRow, 1, 4) - 1;
            RainSegment segment = profile.RainPools[row].Count > 0 ? profile.RainPools[row].Pop() : CreateRain(profile, row);
            segment.Key = key.Model;
            segment.KeyView = key;
            segment.StartTime = now;
            segment.EndTime = -1f;
            // 池中的雨段仍保留上次使用时的尺寸。先清零，避免按下的首帧
            // 在真正计算出高度之前闪出一块旧雨段。
            segment.Rect.sizeDelta = Vector2.zero;
            segment.ShadowRect.sizeDelta = Vector2.zero;
            segment.ShadowRect.gameObject.SetActive(false);
            segment.Rect.gameObject.SetActive(true);
            profile.ActiveRain.Add(segment);
            profile.HeldRain[key.Model.Id] = segment;
        }

        private static void EndRain(ProfileView profile, KvKey key)
        {
            RainSegment segment;
            if (!profile.HeldRain.TryGetValue(key.Id, out segment)) return;
            profile.HeldRain.Remove(key.Id);
            segment.EndTime = SonnetClock.Now;
        }

        private static RainSegment CreateRain(ProfileView profile, int rowIndex)
        {
            RectTransform shadowRect = CreateRect("RainShadow", profile.RainShadowLayers[rowIndex]);
            shadowRect.anchorMin = shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
            shadowRect.pivot = new Vector2(0.5f, 0f);
            var shadowGraphic = shadowRect.gameObject.AddComponent<SoftShadowGraphic>();
            shadowGraphic.raycastTarget = false;
            RectTransform rect = CreateRect("RainSegment", profile.RainBarLayers[rowIndex]);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            var graphic = rect.gameObject.AddComponent<RoundedRectGraphic>();
            graphic.raycastTarget = false;
            return new RainSegment
            {
                RowIndex = rowIndex,
                Rect = rect,
                Graphic = graphic,
                ShadowRect = shadowRect,
                ShadowGraphic = shadowGraphic
            };
        }

        private void UpdateRain(ProfileView view)
        {
            float now = SonnetClock.Now;
            // 旧版的整体缩放只影响按键几何，不会再次缩放键雨速度和最大高度。
            // 导入阶段已经根据屏幕尺寸换算过这两个像素量；此处再乘 Scale 会
            // 让 461px 的键雨被错误拉到约 656px（1.422x），裁剪和羽化都会错位。
            float maxHeight = view.RainMaxHeight;
            float speed = view.RainSpeed;
            for (int i = view.ActiveRain.Count - 1; i >= 0; i--)
            {
                RainSegment segment = view.ActiveRain[i];
                float top = Mathf.Min(maxHeight, Mathf.Max(0f, now - segment.StartTime) * speed);
                float bottom = segment.EndTime >= 0f ? Mathf.Max(0f, now - segment.EndTime) * speed : 0f;
                // BeginRain 与 LateUpdate 可能处于同一帧，此时 top == bottom == 0。
                // 旧判断会立刻回收刚创建的雨段，导致整个键雨系统永远没有
                // 机会进入第二帧。只有已经松开的雨段才允许按长度回收。
                bool ended = segment.EndTime >= 0f;
                if (ended && (bottom >= maxHeight || top <= bottom))
                {
                    segment.Rect.gameObject.SetActive(false);
                    segment.ShadowRect.gameObject.SetActive(false);
                    view.ActiveRain.RemoveAt(i);
                    view.RainPools[segment.RowIndex].Push(segment);
                    continue;
                }
                if (top <= bottom) continue;
                float height = top - bottom;
                float keyWidth = segment.KeyView.Rect.sizeDelta.x;
                float keyHeight = segment.KeyView.Rect.sizeDelta.y;
                BakedNode baked = segment.KeyView.Baked;
                if (baked == null) continue;
                float width = keyWidth * baked.RainWidthRatio;
                Vector2 keyPosition = segment.KeyView.Rect.anchoredPosition;
                // 采用底部中心 pivot(0.5, 0)，底端固定附着在按键上方，彻底消除每帧取整与中心对称拉伸造成的抽搐抖动。
                segment.Rect.sizeDelta = new Vector2(width, height);
                segment.Rect.anchoredPosition = new Vector2(keyPosition.x,
                    keyPosition.y + keyHeight * 0.5f + baked.RainYOffset + bottom);
                // 与旧版一致：UV 模式始终让一整段雨滴从近端色走到远端色；
                // 高度模式则根据雨滴顶点距按键的真实高度采样。羽化同样按
                // 真实高度计算，而不是只在最大高度末端做一小截淡出。
                segment.Graphic.SetRainStyle(baked.RainNear, baked.RainFar, baked.RainRight,
                    baked.RainGradientEnabled, baked.RainHeightGradient, baked.RainHorizontalGradient, baked.RainFadeMode,
                    bottom, top, maxHeight, baked.RainFadeHeight, baked.RainFadePower,
                    baked.RainGradientHeight, baked.RainGradientPower,
                    baked.RainOutlineEnabled ? baked.RainOutlineColor : Color.clear,
                    baked.RainOutlineEnabled ? baked.RainOutlineThickness : 0f,
                    Mathf.Min(baked.RainRadius, height * 0.5f));
                // The shadow graphic draws its soft edge outside its rect. The rain stops at the
                // maximum height, so the rect has to end a soft edge earlier or the shadow keeps
                // glowing above the rain.
                float shadowBottom01 = bottom + baked.RainShadowOffsetY;
                float shadowTop01 = Mathf.Min(top + baked.RainShadowOffsetY, maxHeight - baked.RainShadowSoftness);
                bool showShadow = baked.RainShadowEnabled && baked.RainShadowSoftness > 0.01f && baked.RainShadowStrength > 0f
                    && shadowTop01 > shadowBottom01;
                if (segment.ShadowRect.gameObject.activeSelf != showShadow)
                    segment.ShadowRect.gameObject.SetActive(showShadow);
                if (segment.ShadowRect.gameObject.activeSelf)
                {
                    segment.ShadowRect.sizeDelta = new Vector2(width, shadowTop01 - shadowBottom01);
                    segment.ShadowRect.anchoredPosition = new Vector2(keyPosition.x + baked.RainShadowOffsetX,
                        keyPosition.y + keyHeight * 0.5f + baked.RainYOffset + shadowBottom01);
                    Color bottomFill = EvaluateRainVerticalColor(baked.RainNear, baked.RainFar, baked.RainGradientEnabled,
                        baked.RainHeightGradient, baked.RainFadeMode, bottom, 0f, maxHeight,
                        baked.RainFadeHeight, baked.RainFadePower, baked.RainGradientHeight, baked.RainGradientPower);
                    Color topFill = EvaluateRainVerticalColor(baked.RainNear, baked.RainFar, baked.RainGradientEnabled,
                        baked.RainHeightGradient, baked.RainFadeMode, top, 1f, maxHeight,
                        baked.RainFadeHeight, baked.RainFadePower, baked.RainGradientHeight, baked.RainGradientPower);
                    Color shadowBottom = baked.RainShadowColor;
                    Color shadowTop = baked.RainShadowColor;
                    shadowBottom.a *= baked.RainShadowStrength * bottomFill.a;
                    shadowTop.a *= baked.RainShadowStrength * topFill.a;
                    segment.ShadowGraphic.SetStyle(shadowTop, shadowBottom, baked.RainShadowSoftness);
                }
            }
        }

        private static Color EvaluateRainVerticalColor(Color near, Color far, bool gradientEnabled,
            bool heightGradient, int fadeMode, float distance, float uv,
            float maxHeight, float fadeHeight, float fadePower,
            float gradientHeight, float gradientPower)
        {
            Color value = near;
            if (gradientEnabled)
            {
                float gradientT = heightGradient
                    ? Mathf.Pow(Mathf.Clamp01(distance / Mathf.Max(1f, maxHeight * Mathf.Clamp(gradientHeight, 0.05f, 3f))),
                        Mathf.Clamp(gradientPower, 0.1f, 5f))
                    : Mathf.Clamp01(uv);
                value = Color.Lerp(near, far, gradientT);
            }
            if (fadeMode == 1)
            {
                float fadeDistance = Mathf.Max(1f, maxHeight * Mathf.Clamp(fadeHeight, 0.05f, 3f));
                float alpha = Mathf.Pow(1f - Mathf.Clamp01(distance / fadeDistance),
                    Mathf.Clamp(fadePower, 0.1f, 5f));
                value.a *= alpha;
            }
            return value;
        }

        private static void UpdateTextLayout(KeyView view, KvProfile profile, KvKey node, float scale)
        {
            float labelSize = Mathf.Max(1f, Resolve(node.LabelSize, profile.LabelSize) * scale);
            float countSize = Mathf.Max(1f, Resolve(node.CountSize, profile.CountSize) * scale);
            view.Label.fontSize = Mathf.RoundToInt(labelSize);
            view.Count.fontSize = Mathf.RoundToInt(countSize);
            bool hideCount = KvSettings.HidesCount(profile, node);
            float width = view.Rect.sizeDelta.x;
            RectTransform labelRect = view.Label.rectTransform;
            if (hideCount)
            {
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.sizeDelta = Pixel(new Vector2(width, labelSize * 1.25f));
                labelRect.anchoredPosition = Pixel(new Vector2(node.LabelOffsetX * scale, node.LabelOffsetY * scale));
            }
            else
            {
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 1f);
                labelRect.pivot = new Vector2(0.5f, 1f);
                labelRect.sizeDelta = Pixel(new Vector2(width, labelSize * 1.25f));
                labelRect.anchoredPosition = Pixel(new Vector2(node.LabelOffsetX * scale, (-5f + node.LabelOffsetY) * scale));
            }
            RectTransform countRect = view.Count.rectTransform;
            countRect.anchorMin = countRect.anchorMax = new Vector2(0.5f, 0f);
            countRect.pivot = new Vector2(0.5f, 0f);
            countRect.sizeDelta = Pixel(new Vector2(width, countSize * 1.25f));
            countRect.anchoredPosition = Pixel(new Vector2(node.CountOffsetX * scale, (5f + node.CountOffsetY) * scale));
            view.Count.alignment = node.CountAlignment == 0 ? TextAlignmentOptions.MidlineLeft
                : node.CountAlignment == 2 ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.Midline;
        }

        private void UpdateTextEffects(KeyView view, KvProfile profile, KvKey node, float scale)
        {
            bool custom = node.UseCustomTextEffects;
            ApplyTextMaterialIfChanged(view.Label, view.LabelEffects,
                custom ? node.LabelOutlineEnabled : profile.LabelOutlineEnabled,
                custom ? node.LabelOutlineColor : profile.LabelOutlineColor,
                custom ? node.LabelOutlineThickness : profile.LabelOutlineThickness,
                custom ? node.LabelShadowEnabled : profile.LabelShadowEnabled,
                custom ? node.LabelShadowColor : profile.LabelShadowColor,
                custom ? node.LabelShadowOffset : profile.LabelShadowOffset,
                custom ? node.LabelShadowSoftness : profile.LabelShadowSoftness, scale);
            ApplyTextMaterialIfChanged(view.Count, view.CountEffects,
                custom ? node.CountOutlineEnabled : profile.CountOutlineEnabled,
                custom ? node.CountOutlineColor : profile.CountOutlineColor,
                custom ? node.CountOutlineThickness : profile.CountOutlineThickness,
                custom ? node.CountShadowEnabled : profile.CountShadowEnabled,
                custom ? node.CountShadowColor : profile.CountShadowColor,
                custom ? node.CountShadowOffset : profile.CountShadowOffset,
                custom ? node.CountShadowSoftness : profile.CountShadowSoftness, scale);
        }

        private void ApplyTextMaterialIfChanged(TextMeshProUGUI text, TextEffectCache cache,
            bool outlineEnabled, float[] outlineColor, float outlineThickness, bool shadowEnabled,
            float[] shadowColor, float[] shadowOffset, float shadowSoftness, float scale)
        {
            Color resolvedOutline = ToColor(outlineColor);
            Color resolvedShadow = ToColor(shadowColor);
            if (cache.Matches(text, outlineEnabled, resolvedOutline, outlineThickness,
                shadowEnabled, resolvedShadow, shadowOffset, shadowSoftness)) return;
            ApplyTextMaterial(text, outlineEnabled, outlineColor, outlineThickness, shadowEnabled,
                shadowColor, shadowOffset, shadowSoftness, scale);
            cache.Capture(text, outlineEnabled, resolvedOutline, outlineThickness, shadowEnabled,
                resolvedShadow, shadowOffset, shadowSoftness);
        }

        private void ApplyTextMaterial(TextMeshProUGUI text, bool outlineEnabled, float[] outlineColor,
            float outlineThickness, bool shadowEnabled, float[] shadowColor, float[] shadowOffset,
            float shadowSoftness, float scale)
        {
            if (text == null || text.font == null) return;
            Color outline = ToColor(outlineColor);
            Color shadow = ToColor(shadowColor);
            float width = outlineEnabled && outline.a > 0f
                ? Mathf.Clamp(Mathf.Max(0f, outlineThickness) / 32f, 0f, 0.35f) : 0f;
            bool hasShadow = shadowEnabled && shadow.a > 0f;
            Material material;
            if (!width.Equals(0f) || hasShadow)
            {
                if (!_textMaterials.TryGetValue(text, out material) || material == null)
                {
                    material = UnityEngine.Object.Instantiate(text.font.material);
                    material.name = text.font.name + "_CheryToolsTextMaterial";
                    _textMaterials[text] = material;
                }
                material.SetFloat(OutlineWidthProperty, width);
                material.SetColor(OutlineColorProperty, outline);
                if (material.HasProperty(OutlineSoftnessProperty)) material.SetFloat(OutlineSoftnessProperty, width > 0f ? 0.05f : 0f);
                if (width > 0f) material.EnableKeyword("OUTLINE_ON");
                else material.DisableKeyword("OUTLINE_ON");
                ApplyTextUnderlay(material, hasShadow, shadow, shadowOffset, shadowSoftness, text.fontSize);
                text.fontSharedMaterial = material;
            }
            else
            {
                ReleaseTextMaterial(text);
                text.fontSharedMaterial = text.font.material;
            }
            text.UpdateMeshPadding();
            text.SetMaterialDirty();
            text.SetVerticesDirty();
        }

        private static void ApplyTextUnderlay(Material material, bool enabled, Color color,
            float[] offset, float shadowSoftness, float fontSize)
        {
            if (material == null || !material.HasProperty(UnderlayColorProperty)
                || !material.HasProperty(UnderlayOffsetXProperty)
                || !material.HasProperty(UnderlayOffsetYProperty)
                || !material.HasProperty(UnderlayDilateProperty)
                || !material.HasProperty(UnderlaySoftnessProperty)) return;
            if (!enabled)
            {
                material.DisableKeyword("UNDERLAY_ON");
                material.DisableKeyword("UNDERLAY_INNER");
                material.SetColor(UnderlayColorProperty, Color.clear);
                material.SetFloat(UnderlayOffsetXProperty, 0f);
                material.SetFloat(UnderlayOffsetYProperty, 0f);
                material.SetFloat(UnderlayDilateProperty, 0f);
                material.SetFloat(UnderlaySoftnessProperty, 0f);
                return;
            }
            float shadowScale = Mathf.Clamp(Mathf.Max(1f, fontSize) / 32f, 0.5f, 2f);
            float x = offset != null && offset.Length > 0 ? offset[0] * shadowScale : 2f * shadowScale;
            float y = offset != null && offset.Length > 1 ? offset[1] * shadowScale : -2f * shadowScale;
            material.EnableKeyword("UNDERLAY_ON");
            material.DisableKeyword("UNDERLAY_INNER");
            material.SetColor(UnderlayColorProperty, color);
            material.SetFloat(UnderlayOffsetXProperty, Mathf.Clamp(x / 32f, -1f, 1f));
            material.SetFloat(UnderlayOffsetYProperty, Mathf.Clamp(-y / 32f, -1f, 1f));
            material.SetFloat(UnderlayDilateProperty, 0f);
            float softness = Mathf.Max(0f, shadowSoftness) * shadowScale;
            material.SetFloat(UnderlaySoftnessProperty, Mathf.Clamp(softness / 32f, 0f, 1f));
        }

        private void ReleaseTextMaterial(TextMeshProUGUI text)
        {
            Material material;
            if (!_textMaterials.TryGetValue(text, out material)) return;
            if (material != null) Destroy(material);
            _textMaterials.Remove(text);
        }

        private static void UpdateMediaLayout(KeyView view, KvKey node, float scale, float borderThickness)
        {
            if (view.Media == null) return;
            Vector2 parent = view.Rect.sizeDelta;
            float inset = Mathf.Max(0f, borderThickness) * 2f * scale;
            bool video = node.NodeType == 4;
            // VideoPlayer does its own fitting inside the render texture; only tiling needs UVs.
            int layoutFit = video && node.MediaFit != 4 ? 0 : node.MediaFit;
            Texture texture = view.Media.texture;
            KvMediaLayout layout = KvGeometry.MediaLayout(layoutFit, Mathf.Max(1f, parent.x - inset), Mathf.Max(1f, parent.y - inset),
                video || texture == null ? 0f : texture.width, video || texture == null ? 0f : texture.height,
                node.MediaScale, scale, node.MediaOffsetX * scale, node.MediaOffsetY * scale);
            view.Media.rectTransform.sizeDelta = new Vector2(layout.Width, layout.Height);
            view.Media.rectTransform.anchoredPosition = Pixel(new Vector2(layout.X, layout.Y));
            view.Media.uvRect = new Rect(layout.UvX, layout.UvY, layout.UvWidth, layout.UvHeight);
            if (view.LastMediaFit != node.MediaFit)
            {
                view.LastMediaFit = node.MediaFit;
                if (texture != null) texture.wrapMode = node.MediaFit == 4 ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                if (view.VideoPlayer != null) SetEnumProperty(view.VideoPlayer, "aspectRatio", VideoAspectName(node.MediaFit));
            }
            if (view.VideoPlayer != null)
            {
                if (view.LastVideoLoop != node.VideoLoop)
                {
                    SetProperty(view.VideoPlayer, "isLooping", node.VideoLoop);
                    view.LastVideoLoop = node.VideoLoop;
                }
                UpdateVideoPlayback(view);
            }
        }

        // Value 0 keeps VideoPlayer's own default, so existing video keys look as they did.
        private static string VideoAspectName(int fit)
        {
            switch (fit)
            {
                case 1: return "FitInside";
                case 2: return "FitOutside";
                case 3: return "NoScaling";
                case 4: return "Stretch";
                default: return "FitHorizontally";
            }
        }

        internal bool TryGetMediaTexture(string profileId, string nodeId, out Texture texture)
        {
            texture = null;
            for (int i = 0; i < _profileViewList.Count; i++)
            {
                ProfileView view = _profileViewList[i];
                if (view?.Model == null || !string.Equals(view.Model.Id, profileId, StringComparison.Ordinal)) continue;
                foreach (KeyView key in view.Keys)
                {
                    if (key.Media == null || key.Model == null || !string.Equals(key.Model.Id, nodeId, StringComparison.Ordinal)) continue;
                    texture = key.Media.texture;
                    return texture != null;
                }
            }
            return false;
        }

        // Standalone copy for the FreeMake canvas; it stays usable when the profile is not on screen.
        internal static Texture2D LoadPreviewImage(string path)
        {
            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Repeat,
                    hideFlags = HideFlags.HideAndDontSave
                };
                Func<Texture2D, byte[], bool> loader = ResolveImageLoader();
                if (loader != null && loader(texture, File.ReadAllBytes(path))) return texture;
                Destroy(texture);
            }
            catch (Exception ex)
            {
                KvRuntime.Logger?.Warning(L("KV 媒体节点加载失败：", "Failed to load KV media node: ") + ex.GetBaseException().Message);
            }
            return null;
        }

        private static void UpdateVideoPlayback(KeyView view)
        {
            if (view == null || view.VideoPlayer == null || view.VideoStarted) return;
            if (GetBoolProperty(view.VideoPlayer, "isPlaying"))
            {
                view.VideoStarted = true;
                return;
            }
            if (!GetBoolProperty(view.VideoPlayer, "isPrepared")) return;
            SonnetUguiKit.Invoke(view.VideoPlayer, "Play");
            view.VideoStarted = true;
        }

        private static void CreateMedia(KvKey node, RectTransform parent, float width, float height,
            out RawImage media, out Texture2D imageTexture, out RenderTexture videoTexture, out Component videoPlayer)
        {
            media = null;
            imageTexture = null;
            videoTexture = null;
            videoPlayer = null;
            string path = ResolveMediaPath(node.NodeType == 3 ? node.ImagePath : node.VideoPath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            RectTransform rect = CreateRect("Media", parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Mathf.Max(1f, width - 4f), Mathf.Max(1f, height - 4f));
            media = rect.gameObject.AddComponent<RawImage>();
            media.raycastTarget = false;
            try
            {
                if (node.NodeType == 3)
                {
                    imageTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                    {
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp,
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    Func<Texture2D, byte[], bool> loader = ResolveImageLoader();
                    if (loader == null || !loader(imageTexture, File.ReadAllBytes(path)))
                    {
                        Destroy(imageTexture);
                        imageTexture = null;
                    }
                    media.texture = imageTexture;
                }
                else
                {
                    // 宽高必须使用同一个采样倍数。分别 Clamp 会在宽度先触及
                    // 1920 上限时改变 RenderTexture 宽高比，VideoPlayer 随后便会
                    // 把画面压扁（例如 1010x328 被塞进 1920x812）。
                    float textureScale = Mathf.Min(4f,
                        Mathf.Min(1920f / Mathf.Max(1f, width), 1080f / Mathf.Max(1f, height)));
                    int targetWidth = Mathf.Max(2, Mathf.RoundToInt(width * textureScale));
                    int targetHeight = Mathf.Max(2, Mathf.RoundToInt(height * textureScale));
                    videoTexture = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32)
                    {
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp,
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    videoTexture.Create();
                    Type videoType = Type.GetType("UnityEngine.Video.VideoPlayer, UnityEngine.VideoModule", false);
                    if (videoType == null) throw new NotSupportedException(L("当前 Unity 环境没有 VideoPlayer", "VideoPlayer is not available in this Unity build"));
                    videoPlayer = rect.gameObject.AddComponent(videoType);
                    SetProperty(videoPlayer, "playOnAwake", true);
                    SetProperty(videoPlayer, "isLooping", node.VideoLoop);
                    SetEnumProperty(videoPlayer, "audioOutputMode", "None");
                    SetEnumProperty(videoPlayer, "renderMode", "RenderTexture");
                    SetProperty(videoPlayer, "targetTexture", videoTexture);
                    SetProperty(videoPlayer, "url", path);
                    media.texture = videoTexture;
                    SonnetUguiKit.Invoke(videoPlayer, "Prepare");
                }
            }
            catch (Exception ex)
            {
                KvRuntime.Logger?.Warning(L("KV 媒体节点加载失败：", "Failed to load KV media node: ") + ex.GetBaseException().Message);
            }
        }

        internal static string ResolveMediaPath(string value)
        {
            string path = (value ?? string.Empty).Trim().Trim('"');
            if (string.IsNullOrEmpty(path)) return string.Empty;
            if (!Path.IsPathRooted(path))
            {
                string modulePath = string.IsNullOrEmpty(KvRuntime.ModuleDirectory) ? null : Path.Combine(KvRuntime.ModuleDirectory, path);
                if (!string.IsNullOrEmpty(modulePath) && File.Exists(modulePath)) path = modulePath;
                else path = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), path);
            }
            try { return Path.GetFullPath(path); }
            catch { return path; }
        }

        private static void ResetViewState(ProfileView view)
        {
            if (view == null) return;
            float now = SonnetClock.Now;
            foreach (KeyView key in view.Keys)
            {
                key.LastPressed = false;
                key.Pressed = false;
                key.PressProgress = 0f;
                key.VisualDirty = true;
                if (key.VideoPlayer != null) key.VideoStarted = false;
            }
            foreach (RainSegment segment in view.HeldRain.Values)
                if (segment != null && segment.EndTime < 0f) segment.EndTime = now;
            view.HeldRain.Clear();
            view.KpsTimes.Clear();
            view.Model.CurrentKps = 0;
            view.DynamicDirty = true;
        }

        private static void ReleaseMedia(KeyView key)
        {
            if (key == null) return;
            if (key.VideoPlayer != null)
            {
                SonnetUguiKit.Invoke(key.VideoPlayer, "Stop");
                SetProperty(key.VideoPlayer, "targetTexture", null);
            }
            if (key.VideoTexture != null)
            {
                key.VideoTexture.Release();
                Destroy(key.VideoTexture);
            }
            if (key.ImageTexture != null) Destroy(key.ImageTexture);
        }

        private static Func<Texture2D, byte[], bool> ResolveImageLoader()
        {
            if (_loadImageResolved) return _loadImage;
            _loadImageResolved = true;
            Type type = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", false);
            if (type == null) return null;
            var method = type.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
            if (method != null)
            {
                try { _loadImage = (Func<Texture2D, byte[], bool>)Delegate.CreateDelegate(typeof(Func<Texture2D, byte[], bool>), method); }
                catch { }
            }
            if (_loadImage != null) return _loadImage;
            var extended = type.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
            if (extended == null) return null;
            try
            {
                var full = (Func<Texture2D, byte[], bool, bool>)Delegate.CreateDelegate(typeof(Func<Texture2D, byte[], bool, bool>), extended);
                _loadImage = (texture, data) => full(texture, data, false);
            }
            catch { }
            return _loadImage;
        }

        private static bool GetBoolProperty(Component target, string name)
        {
            try { return target != null && (bool)target.GetType().GetProperty(name).GetValue(target, null); }
            catch { return false; }
        }

        private static EaseMode ParseEase(string easing)
        {
            switch ((easing ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "linear": return EaseMode.Linear;
                case "ease-in-quad": return EaseMode.EaseInQuad;
                case "ease-in-out-quad": return EaseMode.EaseInOutQuad;
                case "ease-out-cubic": return EaseMode.EaseOutCubic;
                case "ease-in-cubic": return EaseMode.EaseInCubic;
                case "ease-in-out-cubic": return EaseMode.EaseInOutCubic;
                case "ease-out-back": return EaseMode.EaseOutBack;
                default: return EaseMode.EaseOutQuad;
            }
        }

        private static float Ease(float value, EaseMode easing)
        {
            float t = Mathf.Clamp01(value);
            switch (easing)
            {
                case EaseMode.Linear: return t;
                case EaseMode.EaseInQuad: return t * t;
                case EaseMode.EaseInOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
                case EaseMode.EaseOutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case EaseMode.EaseInCubic: return t * t * t;
                case EaseMode.EaseInOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
                case EaseMode.EaseOutBack:
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
                default: return 1f - (1f - t) * (1f - t);
            }
        }

        private static bool ShouldRun(KvProfile profile, KvSettings settings, bool playing)
        {
            if (profile == null || !profile.Enabled || profile.Keys == null || profile.Keys.Count == 0) return false;
            if ((settings.OnlyShowPlaying || profile.OnlyShowPlaying) && !playing) return false;
            if (!profile.ShowInGame && playing) return false;
            return true;
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, float size, FontStyle style, TMP_FontAsset font)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font != null ? font : _fontAsset;
            text.fontSize = Mathf.Max(1f, size);
            text.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : FontStyles.Normal;
            text.alignment = TextAlignmentOptions.Midline;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false;
            return text;
        }

        private TMP_FontAsset ResolveProfileFont(string value)
        {
            string path = ResolveMediaPath(value);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return _fontAsset;
            TMP_FontAsset cached;
            if (_fontCache.TryGetValue(path, out cached)) return cached != null ? cached : _fontAsset;
            TMP_FontAsset loaded = null;
            try
            {
                // 旧版 KV 并不是把 Font 直接交给 UGUI.Text，而是先把它
                // 转成 TMP_FontAsset。Unity.TextMeshPro 会从 TTF/OTF 生成
                // 动态 SDF 图集，中文和自定义字体都走同一条可靠的渲染链。
                Font source = new Font(path);
                if (source != null)
                {
                    _createdFontSources.Add(source);
                    loaded = CreateFontAsset(source, "CheryTools_KV_" + Path.GetFileNameWithoutExtension(path));
                }

                if (loaded != null)
                    KvRuntime.Logger?.Info(L("KV 已加载自定义字体：", "KV loaded custom font: ") + Path.GetFileName(path));
                else
                    KvRuntime.Logger?.Warning(L("KV 字体加载失败，已回退默认字体：", "KV font failed to load, using the default font: ") + path);
            }
            catch (Exception ex)
            {
                KvRuntime.Logger?.Warning(L("KV 字体加载失败：", "KV font failed to load: ") + ex.GetBaseException().Message);
            }
            _fontCache[path] = loaded;
            return loaded != null ? loaded : _fontAsset;
        }

        private TMP_FontAsset CreateFontAsset(Font source, string assetName)
        {
            if (source == null) return null;
            try
            {
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                    source,
                    90,
                    9,
                    GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic,
                    true);
                if (asset == null) return null;
                asset.name = assetName;
                asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                asset.isMultiAtlasTexturesEnabled = true;
                _createdFontAssets.Add(asset);
                return asset;
            }
            catch (Exception ex)
            {
                KvRuntime.Logger?.Warning(L("KV SDF 字体图集创建失败，已回退默认字体：", "Could not create the KV SDF font atlas, using the default font: ") + ex.GetBaseException().Message);
                return null;
            }
        }

        private void OnDestroy()
        {
            foreach (Material material in _textMaterials.Values)
                if (material != null) Destroy(material);
            _textMaterials.Clear();
            for (int i = 0; i < _createdFontAssets.Count; i++)
            {
                TMP_FontAsset asset = _createdFontAssets[i];
                if (asset == null) continue;
                if (asset.atlasTextures != null)
                    for (int j = 0; j < asset.atlasTextures.Length; j++)
                        if (asset.atlasTextures[j] != null) Destroy(asset.atlasTextures[j]);
                if (asset.material != null) Destroy(asset.material);
                Destroy(asset);
            }
            for (int i = 0; i < _createdFontSources.Count; i++)
                if (_createdFontSources[i] != null) Destroy(_createdFontSources[i]);
            _createdFontAssets.Clear();
            _createdFontSources.Clear();
            _fontCache.Clear();
        }

        private static readonly char[] CountDigits = new char[12];

        // Counters change on every key press; writing the digits into a reused buffer avoids
        // allocating a string per change. TMP copies the characters, so the buffer can be shared.
        private static void SetCountText(TextMeshProUGUI text, int value)
        {
            int length = 0;
            long remaining = value;
            bool negative = remaining < 0;
            if (negative) remaining = -remaining;
            do
            {
                CountDigits[CountDigits.Length - 1 - length] = (char)('0' + (int)(remaining % 10));
                remaining /= 10;
                length++;
            }
            while (remaining > 0);
            if (negative)
            {
                CountDigits[CountDigits.Length - 1 - length] = '-';
                length++;
            }

            text.SetCharArray(CountDigits, CountDigits.Length - length, length);
        }

        private static float Resolve(float value, float fallback) => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value) ? value : fallback;
    }
}
