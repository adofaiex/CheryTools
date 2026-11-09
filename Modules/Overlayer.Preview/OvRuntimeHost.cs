using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using CheryTools.Sonnet.Shared;
using static CheryTools.Sonnet.Shared.SonnetUguiKit;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.Overlayer.Preview
{
    [DefaultExecutionOrder(30900)]
    internal sealed class OvRuntimeHost : MonoBehaviour
    {
        private abstract class ComponentView
        {
            internal OvComponent Model;
            internal RectTransform Root;
        }

        private sealed class TextView : ComponentView
        {
            internal TextMeshProUGUI Text;
            internal string LastText;
            internal Material Material;
            internal readonly Dictionary<string, TokenSpan> TokenSpans = new Dictionary<string, TokenSpan>(StringComparer.Ordinal);
            internal long LastNodeRevision = -1;
        }

        private struct TokenSpan
        {
            internal int Start;
            internal int End;
        }

        private sealed class ImageView : ComponentView
        {
            internal RawImage Image;
            internal Texture2D Texture;
            internal long LastNodeRevision = -1;
        }

        private sealed class VideoView : ComponentView
        {
            internal RawImage Image;
            internal RectTransform Content;
            internal RenderTexture Texture;
            internal Component Player;
            internal bool Started;
            internal bool Loop;
        }

        private sealed class ProgressView : ComponentView
        {
            internal RectTransform ShadowRect;
            internal OvRoundedRectGraphic Shadow;
            internal OvRoundedRectGraphic Background;
            internal RectTransform FillMask;
            internal RectTransform FillRect;
            internal OvRoundedRectGraphic Fill;
            internal OvRoundedRectGraphic Border;
            internal float LastRatio = float.NaN;
        }

        private sealed class TokenGroupOperation
        {
            internal OvTokenGroupTransform Transform;
            internal readonly HashSet<string> TokenIds = new HashSet<string>(StringComparer.Ordinal);
        }

        private readonly List<ComponentView> _views = new List<ComponentView>();
        private readonly Dictionary<string, TMP_FontAsset> _fontCache = new Dictionary<string, TMP_FontAsset>(StringComparer.OrdinalIgnoreCase);
        private readonly List<TMP_FontAsset> _createdFontAssets = new List<TMP_FontAsset>();
        private readonly List<Font> _createdFonts = new List<Font>();
        private readonly OvTagResolver _tags = new OvTagResolver();
        internal string ResolveText(string format) => _tags.ResolveText(format);
        private readonly OvTokenAnimationRuntime _nodeAnimations = new OvTokenAnimationRuntime();
        private readonly OvAnimationTriggerFrame _triggerFrame = new OvAnimationTriggerFrame();
        private readonly List<OvTextComponent> _nodeTexts = new List<OvTextComponent>();
        private readonly List<OvTextComponent> _imageAnimationProxies = new List<OvTextComponent>();
        private readonly Dictionary<OvImageComponent, OvTextComponent> _imageProxyMap = new Dictionary<OvImageComponent, OvTextComponent>();
        private readonly HashSet<KeyCode> _watchedNodeKeys = new HashSet<KeyCode>();
        private readonly StringBuilder _tokenBuilder = new StringBuilder(256);
        private readonly Dictionary<string, TokenGroupOperation> _tokenGroupOperationMap =
            new Dictionary<string, TokenGroupOperation>(StringComparer.Ordinal);
        private readonly List<TokenGroupOperation> _tokenGroupOperations = new List<TokenGroupOperation>();
        private readonly List<TokenGroupOperation> _tokenGroupOperationPool = new List<TokenGroupOperation>();
        private bool _disabledApplied;
        private int _builtLayoutRevision = -1;
        private int _appliedVisualRevision = -1;
        private float _nextDynamicUpdate;
        private int _screenWidth = -1;
        private int _screenHeight = -1;
        private bool _playingInitialized;
        private bool _lastPlaying;
        private TMP_FontAsset _defaultFont;
        private int _nodeGraphRevision = -1;
        private bool _nodePlayingInitialized;
        private bool _nodeLastPlaying;
        private scrMarginTracker _nodeTracker;
        private int _nodeTrackerCount;
        private int _nodePureCombo;
        private int _nodePerfectCombo;
        private int _nodeControllerInstanceId;
        private int _nodeControllerState = (int)States.None;
        private bool _nodeControllerStateInitialized;
        private int _nodeLastBeatNumber = int.MinValue;
        private static Func<Texture2D, byte[], bool> _loadImage;
        private static bool _imageLoaderResolved;
        private static readonly int OutlineWidthProperty = ShaderUtilities.ID_OutlineWidth;
        private static readonly int OutlineColorProperty = ShaderUtilities.ID_OutlineColor;
        private static readonly int UnderlayColorProperty = Shader.PropertyToID("_UnderlayColor");
        private static readonly int UnderlayOffsetXProperty = Shader.PropertyToID("_UnderlayOffsetX");
        private static readonly int UnderlayOffsetYProperty = Shader.PropertyToID("_UnderlayOffsetY");
        private static readonly int UnderlayDilateProperty = Shader.PropertyToID("_UnderlayDilate");
        private static readonly int UnderlaySoftnessProperty = Shader.PropertyToID("_UnderlaySoftness");

        private void Awake()
        {
            RectTransform rect = transform as RectTransform;
            if (rect != null) Stretch(rect);
            string[] fontCandidates = {
                Path.Combine(OvRuntime.ModuleDirectory ?? string.Empty, "..", "Resources", "MiSans-Bold.ttf"),
                Path.Combine(OvRuntime.ModuleDirectory ?? string.Empty, "Resources", "MiSans-Bold.ttf"),
                Path.Combine(OvRuntime.ModuleDirectory ?? string.Empty, "MiSans-Bold.ttf"),
                Path.Combine(Directory.GetParent(OvRuntime.ModuleDirectory ?? string.Empty)?.FullName ?? string.Empty, "Resources", "MiSans-Bold.ttf")
            };
            for (int i = 0; i < fontCandidates.Length; i++)
            {
                string candidate = fontCandidates[i];
                if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                {
                    _defaultFont = ResolveFont(candidate);
                    if (_defaultFont != null) break;
                }
            }
            if (_defaultFont == null) _defaultFont = TMP_Settings.defaultFontAsset;
            if (_defaultFont == null)
            {
                Font builtin = Resources.GetBuiltinResource<Font>("Arial.ttf");
                _defaultFont = CreateFontAsset(builtin, "CheryTools_OV_DefaultFont");
            }
            _triggerFrame.ResolveTagValue = (text, tag) => _tags.ResolveText(tag ?? string.Empty);
            _triggerFrame.ResolveTagNumber = (text, tag) => _tags.ResolveNumeric(tag);
        }

        private void LateUpdate()
        {
            OvSettings settings = OvRuntime.Settings;
            if (settings == null) return;
            EnsureBuilt(settings);
            if (!settings.Enabled)
            {
                // Nothing is visible while the Overlayer is switched off: hide the views once and skip
                // tag sampling and the node animation runtime entirely.
                if (!_disabledApplied)
                {
                    _disabledApplied = true;
                    for (int i = 0; i < _views.Count; i++)
                    {
                        ComponentView hidden = _views[i];
                        if (!hidden.Root.gameObject.activeSelf) continue;
                        hidden.Root.gameObject.SetActive(false);
                        if (hidden is VideoView hiddenVideo && hiddenVideo.Player != null)
                        {
                            SonnetUguiKit.Invoke(hiddenVideo.Player, "Pause");
                            hiddenVideo.Started = false;
                        }
                    }
                }
                return;
            }

            if (_disabledApplied)
            {
                _disabledApplied = false;
                _playingInitialized = false;
                _nextDynamicUpdate = 0f;
            }

            _tags.Tick();
            bool playing = IsPlaying();
            bool runtimeChanged = !_playingInitialized || _lastPlaying != playing
                || _screenWidth != Screen.width || _screenHeight != Screen.height;
            if (runtimeChanged)
            {
                _playingInitialized = true;
                _lastPlaying = playing;
                _screenWidth = Screen.width;
                _screenHeight = Screen.height;
                ApplyVisuals(true);
            }
            else if (_appliedVisualRevision != OvRuntime.VisualRevision) ApplyVisuals(false);

            UpdateNodeRuntime(settings, playing);

            float now = SonnetClock.Now;
            bool dynamicDue = now >= _nextDynamicUpdate;
            if (dynamicDue)
            {
                float rate = Mathf.Clamp(settings.DataUpdateRate, 15f, 360f);
                _nextDynamicUpdate = now + 1f / rate;
            }
            bool globalVisible = settings.Enabled && (!settings.OnlyShowPlaying || playing);
            for (int i = 0; i < _views.Count; i++)
            {
                ComponentView view = _views[i];
                bool visible = globalVisible && ShouldShow(view.Model, playing);
                if (view.Root.gameObject.activeSelf != visible)
                {
                    view.Root.gameObject.SetActive(visible);
                    if (!visible && view is VideoView hiddenVideo && hiddenVideo.Player != null)
                    {
                        SonnetUguiKit.Invoke(hiddenVideo.Player, "Pause");
                        hiddenVideo.Started = false;
                    }
                }
                if (!visible) continue;
                if (view is TextView textView)
                {
                    long nodeRevision = _nodeAnimations.GetVisualRevision((OvTextComponent)textView.Model);
                    if (dynamicDue || runtimeChanged || textView.LastNodeRevision != nodeRevision)
                        UpdateText(textView, runtimeChanged || textView.LastNodeRevision != nodeRevision);
                }
                else if (view is ImageView imageView)
                {
                    OvTextComponent proxy = GetImageProxy((OvImageComponent)imageView.Model, false);
                    long nodeRevision = proxy != null ? _nodeAnimations.GetVisualRevision(proxy) : 0;
                    if (runtimeChanged || imageView.LastNodeRevision != nodeRevision)
                    {
                        ApplyImageStyle(imageView);
                        imageView.LastNodeRevision = nodeRevision;
                    }
                }
                else if (view is ProgressView progressView && (dynamicDue || runtimeChanged)) UpdateProgress(progressView);
                else if (view is VideoView videoView) UpdateVideoPlayback(videoView);
            }
        }

        internal void ClearRuntime()
        {
            for (int i = 0; i < _views.Count; i++) ReleaseView(_views[i]);
            _views.Clear();
            _builtLayoutRevision = -1;
            _appliedVisualRevision = -1;
        }

        internal void Preview(OvTextComponent model)
        {
            if (model == null) return;
            _nodeAnimations.Preview(model);
        }

        internal void Preview(OvImageComponent model)
        {
            OvTextComponent proxy = GetImageProxy(model, true);
            if (proxy != null) _nodeAnimations.Preview(proxy);
        }

        internal void StopPreview(OvTextComponent model)
        {
            if (model != null) _nodeAnimations.Stop(model);
        }

        internal void StopPreview(OvImageComponent model)
        {
            OvTextComponent proxy = GetImageProxy(model, false);
            if (proxy != null) _nodeAnimations.Stop(proxy);
        }

        private void EnsureBuilt(OvSettings settings)
        {
            if (_builtLayoutRevision == OvRuntime.LayoutRevision) return;
            ClearRuntime();
            var ordered = new List<OvComponent>(settings.Texts.Count + settings.Images.Count
                + settings.Videos.Count + settings.ProgressBars.Count);
            ordered.AddRange(settings.Texts);
            ordered.AddRange(settings.Images);
            ordered.AddRange(settings.Videos);
            ordered.AddRange(settings.ProgressBars);
            ordered.Sort((left, right) => left.Depth.CompareTo(right.Depth));
            for (int i = 0; i < ordered.Count; i++)
            {
                OvComponent model = ordered[i];
                ComponentView view = model is OvTextComponent text ? BuildText(text)
                    : model is OvImageComponent image ? BuildImage(image)
                    : model is OvVideoComponent video ? BuildVideo(video)
                    : model is OvProgressComponent progress ? BuildProgress(progress)
                    : null;
                if (view != null) _views.Add(view);
            }
            _builtLayoutRevision = OvRuntime.LayoutRevision;
            _disabledApplied = false;
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            ApplyVisuals(true);
            _nextDynamicUpdate = 0f;
        }

        private void UpdateNodeRuntime(OvSettings settings, bool playing)
        {
            _triggerFrame.Reset();
            RebuildNodeInputsIfNeeded(settings);

            try { _triggerFrame.AnyKeyDown = Input.anyKeyDown; }
            catch { }
            foreach (KeyCode key in _watchedNodeKeys)
            {
                try
                {
                    if (Input.GetKeyDown(key)) _triggerFrame.KeysDown.Add(key);
                    if (Input.GetKeyUp(key)) _triggerFrame.KeysUp.Add(key);
                }
                catch { }
            }

            scrController controller = null;
            try { controller = scrController.instance; }
            catch { }
            CaptureLevelState(controller);
            CaptureJudgements(controller);

            try { _triggerFrame.AutoplayEnabled = RDC.auto; }
            catch { }
            try { _triggerFrame.NoFailEnabled = controller != null ? controller.noFail : GCS.useNoFail; }
            catch { }
            try { _triggerFrame.JudgementMode = (int)GCS.difficulty; }
            catch { }

            try
            {
                scrConductor conductor = scrConductor.instance;
                if (conductor != null && conductor.onBeatHappened && conductor.beatNumber != _nodeLastBeatNumber)
                {
                    _nodeLastBeatNumber = conductor.beatNumber;
                    _triggerFrame.BeatHappened = true;
                    _triggerFrame.BeatNumber = conductor.beatNumber;
                }
            }
            catch { }

            if (!_nodePlayingInitialized)
            {
                _nodePlayingInitialized = true;
                _nodeLastPlaying = playing;
            }
            else
            {
                _nodeLastPlaying = playing;
            }

            float delta = playing ? Time.deltaTime : SonnetClock.DeltaTime;
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0f) delta = 0f;
            long revision = OvRuntime.VisualRevision;
            _nodeAnimations.Update(_nodeTexts, _triggerFrame, delta, revision);
            _nodeAnimations.Update(_imageAnimationProxies, _triggerFrame, delta, revision);
        }

        private void RebuildNodeInputsIfNeeded(OvSettings settings)
        {
            if (_nodeGraphRevision == OvRuntime.VisualRevision) return;
            _nodeGraphRevision = OvRuntime.VisualRevision;
            _nodeTexts.Clear();
            _imageAnimationProxies.Clear();
            _watchedNodeKeys.Clear();

            if (settings.Texts != null)
            {
                for (int i = 0; i < settings.Texts.Count; i++)
                {
                    OvTextComponent text = settings.Texts[i];
                    if (!OvTokenAnimationRuntime.HasEnabledGraph(text)) continue;
                    _nodeTexts.Add(text);
                    CollectWatchedKeys(text.TokenAnimation);
                }
            }
            if (settings.Images != null)
            {
                for (int i = 0; i < settings.Images.Count; i++)
                {
                    OvImageComponent image = settings.Images[i];
                    if (image == null || image.NodeAnimation == null || !image.NodeAnimation.Enabled
                        || image.NodeAnimation.Nodes == null || image.NodeAnimation.Nodes.Count == 0) continue;
                    OvTextComponent proxy = GetImageProxy(image, true);
                    if (proxy == null) continue;
                    _imageAnimationProxies.Add(proxy);
                    CollectWatchedKeys(image.NodeAnimation);
                }
            }
        }

        private void CollectWatchedKeys(OvAnimationGraph graph)
        {
            if (graph?.Nodes == null) return;
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                OvAnimationNode node = graph.Nodes[i];
                if (node == null || node.Kind != OvAnimationNodeKind.Trigger
                    || node.Trigger != OvAnimationTriggerKind.SpecificKey || node.TriggerKeys == null) continue;
                for (int k = 0; k < node.TriggerKeys.Count; k++)
                {
                    if (Enum.TryParse(node.TriggerKeys[k], true, out KeyCode key) && key != KeyCode.None)
                        _watchedNodeKeys.Add(key);
                }
            }
        }

        private void CaptureLevelState(scrController controller)
        {
            int controllerId = 0;
            int state = (int)States.None;
            try
            {
                if (controller != null)
                {
                    controllerId = controller.GetInstanceID();
                    state = (int)controller.state;
                }
            }
            catch { }

            if (controllerId != _nodeControllerInstanceId)
            {
                if (_nodeControllerInstanceId != 0 && _nodeControllerStateInitialized
                    && !IsTerminalLevelState(_nodeControllerState) && controllerId == 0)
                {
                    _triggerFrame.LevelEnded = true;
                }
                _nodeControllerInstanceId = controllerId;
                _nodeControllerStateInitialized = false;
                _nodeControllerState = (int)States.None;
                _nodeLastBeatNumber = int.MinValue;
            }
            if (controllerId == 0) return;
            if (!_nodeControllerStateInitialized)
            {
                _nodeControllerStateInitialized = true;
                _nodeControllerState = state;
                return;
            }
            if (state == _nodeControllerState) return;
            if (state == (int)States.PlayerControl && _nodeControllerState != (int)States.PlayerControl)
            {
                _triggerFrame.LevelStarted = true;
                _nodeLastBeatNumber = int.MinValue;
            }
            if (IsTerminalLevelState(state) && !IsTerminalLevelState(_nodeControllerState))
            {
                _triggerFrame.LevelEnded = true;
                if (state == (int)States.Won) _triggerFrame.LevelWon = true;
                else _triggerFrame.LevelFailed = true;
            }
            _nodeControllerState = state;
        }

        private void CaptureJudgements(scrController controller)
        {
            scrMarginTracker tracker = null;
            try
            {
                if (controller != null && controller.playerOne != null) tracker = controller.playerOne.marginTracker;
            }
            catch { }
            int count = 0;
            try { count = tracker?.hitMargins != null ? tracker.hitMargins.Count : 0; }
            catch { tracker = null; }

            if (!ReferenceEquals(tracker, _nodeTracker) || count < _nodeTrackerCount)
            {
                _nodeTracker = tracker;
                _nodeTrackerCount = 0;
                _nodePureCombo = 0;
                _nodePerfectCombo = 0;
                for (int i = 0; i < count; i++) ApplyJudgement(SonnetJudgements.Of(tracker.hitMargins[i]), false);
                _nodeTrackerCount = count;
                return;
            }
            if (tracker == null) return;
            for (int i = _nodeTrackerCount; i < count; i++) ApplyJudgement(SonnetJudgements.Of(tracker.hitMargins[i]), true);
            _nodeTrackerCount = count;
        }

        private void ApplyJudgement(SonnetJudgement kind, bool emit)
        {
            int previousPure = _nodePureCombo;
            int previousPerfect = _nodePerfectCombo;
            bool increased = false;

            // 纯极致完美判定（3.3.x 的 Perfect、3.4.0 的 XPerfect）
            if (kind == SonnetJudgement.Perfect)
            {
                _nodePureCombo++;
                _nodePerfectCombo++;
                increased = true;
            }
            // 符号完美保持全完美连击，打断纯 X 完美连击
            else if (kind == SonnetJudgement.PerfectPlus || kind == SonnetJudgement.PerfectMinus)
            {
                _nodePureCombo = 0;
                _nodePerfectCombo++;
                increased = true;
            }
            // 中旋与自动方块不属于玩家击打物量，但保持两种连击链不断连
            else if (kind == SonnetJudgement.Midspin || kind == SonnetJudgement.Auto)
            {
            }
            // 稍快/稍慢/偏快/偏慢/太快/太慢/失误等所有其他判定打断所有 Combo
            else
            {
                _nodePureCombo = 0;
                _nodePerfectCombo = 0;
            }
            if (!emit) return;
            // Trigger masks use the version-independent bit layout shown in the node editor.
            int bit = SonnetJudgements.MaskBit(kind);
            if (bit >= 0) _triggerFrame.Judgements.Add(bit);
            if (increased) _triggerFrame.ComboIncreased = true;
            if (previousPure > 0 && _nodePureCombo == 0) _triggerFrame.PureComboBroken = true;
            if (previousPerfect > 0 && _nodePerfectCombo == 0) _triggerFrame.PerfectComboBroken = true;
        }

        private static bool IsTerminalLevelState(int state)
        {
            return state == (int)States.Won || state == (int)States.Fail || state == (int)States.Fail2;
        }

        private OvTextComponent GetImageProxy(OvImageComponent image, bool create)
        {
            if (image == null) return null;
            if (!_imageProxyMap.TryGetValue(image, out OvTextComponent proxy))
            {
                if (!create) return null;
                proxy = OvImageNodeAnimation.CreateRuntimeProxy(image);
                _imageProxyMap[image] = proxy;
            }
            else OvImageNodeAnimation.SyncRuntimeProxy(proxy, image);
            return proxy;
        }

        private TextView BuildText(OvTextComponent model)
        {
            RectTransform root = CreateComponentRoot("OV_Text_" + model.Id, transform);
            TextMeshProUGUI text = root.gameObject.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.richText = true;
            text.extraPadding = true;
            text.enableCulling = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.font = ResolveFont(model.FontPath) ?? _defaultFont;
            return new TextView { Model = model, Root = root, Text = text };
        }

        private ImageView BuildImage(OvImageComponent model)
        {
            RectTransform root = CreateComponentRoot("OV_Image_" + model.Id, transform);
            RawImage image = root.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            Texture2D texture = LoadTexture(model.ImagePath);
            image.texture = texture;
            return new ImageView { Model = model, Root = root, Image = image, Texture = texture };
        }

        private VideoView BuildVideo(OvVideoComponent model)
        {
            RectTransform root = CreateComponentRoot("OV_Video_" + model.Id, transform);
            RectTransform content = CreateRect("Content", root);
            RawImage image = content.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            var view = new VideoView { Model = model, Root = root, Content = content, Image = image, Loop = model.Loop };
            string path = ResolvePath(model.VideoPath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return view;
            try
            {
                float textureScale = Mathf.Min(4f,
                    Mathf.Min(1920f / Mathf.Max(1f, model.Width), 1080f / Mathf.Max(1f, model.Height)));
                int width = Mathf.Max(2, Mathf.RoundToInt(model.Width * textureScale));
                int height = Mathf.Max(2, Mathf.RoundToInt(model.Height * textureScale));
                view.Texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                view.Texture.Create();
                Type videoType = Type.GetType("UnityEngine.Video.VideoPlayer, UnityEngine.VideoModule", false);
                if (videoType == null) return view;
                view.Player = content.gameObject.AddComponent(videoType);
                SetProperty(view.Player, "playOnAwake", true);
                SetProperty(view.Player, "isLooping", model.Loop);
                SetEnumProperty(view.Player, "audioOutputMode", "None");
                SetEnumProperty(view.Player, "renderMode", "RenderTexture");
                SetProperty(view.Player, "targetTexture", view.Texture);
                SetProperty(view.Player, "url", path);
                view.Image.texture = view.Texture;
                SonnetUguiKit.Invoke(view.Player, "Prepare");
            }
            catch (Exception ex) { OvRuntime.Logger?.Warning(L("OV 视频加载失败：", "OV video failed to load: ") + ex.GetBaseException().Message); }
            return view;
        }

        private ProgressView BuildProgress(OvProgressComponent model)
        {
            RectTransform root = CreateComponentRoot("OV_Progress_" + model.Id, transform);

            RectTransform shadowRect = CreateRect("Shadow", root);
            OvRoundedRectGraphic shadow = shadowRect.gameObject.AddComponent<OvRoundedRectGraphic>();
            shadow.raycastTarget = false;

            RectTransform bgRect = CreateRect("Background", root);
            Stretch(bgRect);
            OvRoundedRectGraphic background = bgRect.gameObject.AddComponent<OvRoundedRectGraphic>();
            background.raycastTarget = false;

            RectTransform fillMask = CreateRect("FillMask", root);
            fillMask.gameObject.AddComponent<RectMask2D>();

            RectTransform fillRect = CreateRect("FillGraphic", fillMask);
            OvRoundedRectGraphic fill = fillRect.gameObject.AddComponent<OvRoundedRectGraphic>();
            fill.raycastTarget = false;

            RectTransform borderRect = CreateRect("Border", root);
            Stretch(borderRect);
            OvRoundedRectGraphic border = borderRect.gameObject.AddComponent<OvRoundedRectGraphic>();
            border.raycastTarget = false;

            return new ProgressView
            {
                Model = model, Root = root, ShadowRect = shadowRect, Shadow = shadow,
                Background = background, FillMask = fillMask, FillRect = fillRect,
                Fill = fill, Border = border
            };
        }

        private void ApplyVisuals(bool forceText)
        {
            _views.Sort((left, right) => left.Model.Depth.CompareTo(right.Model.Depth));
            for (int i = 0; i < _views.Count; i++)
            {
                ComponentView view = _views[i];
                view.Root.SetSiblingIndex(i);
                ApplyBase(view);
                if (view is TextView text) ApplyTextStyle(text, forceText);
                else if (view is ImageView image) ApplyImageStyle(image);
                else if (view is VideoView video) ApplyVideoStyle(video);
                else if (view is ProgressView progress) ApplyProgressStyle(progress);
            }
            _appliedVisualRevision = OvRuntime.VisualRevision;
        }

        private static void ApplyBase(ComponentView view)
        {
            OvComponent model = view.Model;
            RectTransform rect = view.Root;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(Mathf.Clamp01(model.PivotX), 1f - Mathf.Clamp01(model.PivotY));
            rect.anchoredPosition = Pixel(new Vector2(model.PositionX, -model.PositionY));
        }

        private void ApplyTextStyle(TextView view, bool force)
        {
            OvTextComponent model = (OvTextComponent)view.Model;
            view.Text.font = ResolveFont(model.FontPath) ?? _defaultFont;
            view.Text.fontSize = Mathf.Max(1f, model.FontSize);
            view.Text.characterSpacing = model.LetterSpacing;
            view.Text.lineSpacing = model.LineHeightOffset;
            view.Text.alignment = model.Alignment == 1 ? TextAlignmentOptions.Top
                : model.Alignment == 2 ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
            view.Text.color = WithOpacity(ToColor(model.TextColor), model.Opacity);
            ApplyTextEffects(view, model);
            // 字号、间距以及材质 padding 都会改变首选尺寸，即使实际字符串
            // 没变也必须重算一次，否则新描边/阴影仍会使用旧文字包围盒。
            if (force) view.LastText = null;
            view.Text.SetAllDirty();
            UpdateText(view, true);
        }

        private void ApplyImageStyle(ImageView view)
        {
            OvImageComponent model = (OvImageComponent)view.Model;
            float width = view.Texture != null ? view.Texture.width : 100f;
            float height = view.Texture != null ? view.Texture.height : 100f;
            view.Root.sizeDelta = Pixel(new Vector2(width * Mathf.Max(0.01f, model.Scale), height * Mathf.Max(0.01f, model.Scale)));
            OvTokenPose pose = OvTokenPose.Identity;
            OvTextComponent proxy = GetImageProxy(model, false);
            Dictionary<string, OvTokenPose> poses = proxy != null ? _nodeAnimations.GetPoses(proxy) : null;
            if (poses != null && poses.TryGetValue(OvImageNodeAnimation.TargetId, out OvTokenPose resolvedPose)) pose = resolvedPose;
            float offsetX = pose.OffsetX;
            float offsetY = pose.OffsetY;
            float scaleX = pose.ScaleX;
            float scaleY = pose.ScaleY;
            float rotation = pose.Rotation;
            if (pose.GroupTransforms != null)
            {
                for (int i = 0; i < pose.GroupTransforms.Count; i++)
                {
                    OvTokenGroupTransform group = pose.GroupTransforms[i];
                    if (group == null) continue;
                    offsetX += group.OffsetX;
                    offsetY += group.OffsetY;
                    scaleX *= Mathf.Abs(group.ScaleX) < 0.0001f ? 1f : group.ScaleX;
                    scaleY *= Mathf.Abs(group.ScaleY) < 0.0001f ? 1f : group.ScaleY;
                    rotation += group.Rotation;
                }
            }
            view.Root.anchoredPosition = Pixel(new Vector2(model.PositionX + offsetX, -model.PositionY - offsetY));
            view.Root.localScale = new Vector3(Mathf.Max(0.0001f, scaleX), Mathf.Max(0.0001f, scaleY), 1f);
            view.Root.localEulerAngles = new Vector3(0f, 0f, -model.Rotation - rotation);
            view.Image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(model.Opacity * pose.Opacity));
        }

        private static void ApplyVideoStyle(VideoView view)
        {
            OvVideoComponent model = (OvVideoComponent)view.Model;
            view.Root.sizeDelta = Pixel(new Vector2(Mathf.Max(1f, model.Width), Mathf.Max(1f, model.Height)));
            view.Root.localEulerAngles = new Vector3(0f, 0f, -model.Rotation);
            view.Content.anchorMin = view.Content.anchorMax = new Vector2(0.5f, 0.5f);
            view.Content.pivot = new Vector2(0.5f, 0.5f);
            view.Content.sizeDelta = Pixel(view.Root.sizeDelta * Mathf.Max(0.01f, model.ContentScale));
            view.Content.anchoredPosition = Pixel(new Vector2(model.ContentOffsetX, -model.ContentOffsetY));
            view.Image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(model.Opacity));
            if (view.Player != null && view.Loop != model.Loop)
            {
                SetProperty(view.Player, "isLooping", model.Loop);
                view.Loop = model.Loop;
            }
        }

        private void ApplyProgressStyle(ProgressView view)
        {
            OvProgressComponent model = (OvProgressComponent)view.Model;
            float width = Mathf.Max(1f, model.Width);
            float height = Mathf.Max(1f, model.Height);
            view.Root.sizeDelta = Pixel(new Vector2(width, height));
            if (view.ShadowRect != null)
            {
                view.ShadowRect.anchorMin = view.ShadowRect.anchorMax = new Vector2(0.5f, 0.5f);
                view.ShadowRect.pivot = new Vector2(0.5f, 0.5f);
                view.ShadowRect.sizeDelta = Pixel(new Vector2(width, height));
                float shadowX = model.ShadowOffset != null && model.ShadowOffset.Length > 0 ? model.ShadowOffset[0] : 0f;
                float shadowY = model.ShadowOffset != null && model.ShadowOffset.Length > 1 ? model.ShadowOffset[1] : 0f;
                view.ShadowRect.anchoredPosition = Pixel(new Vector2(shadowX, -shadowY));
                view.Shadow.SetStyle(model.EnableShadow ? WithOpacity(ToColor(model.ShadowColor), model.Opacity) : Color.clear,
                    Color.clear, 0f, model.CornerRadius);
            }
            view.Background.SetStyle(WithOpacity(ToColor(model.BackgroundColor), model.Opacity),
                Color.clear, 0f, model.CornerRadius);
            Color borderColor = model.BorderThickness > 0.01f ? WithOpacity(ToColor(model.BorderColor), model.Opacity) : Color.clear;
            view.Border.SetStyle(Color.clear, borderColor, model.BorderThickness, model.CornerRadius);
            view.Fill.SetStyle(WithOpacity(ToColor(model.FillColor), model.Opacity), Color.clear, 0f, model.CornerRadius);
            view.FillRect.sizeDelta = Pixel(new Vector2(width, height));
            view.LastRatio = float.NaN;
            UpdateProgress(view);
        }

        private void UpdateText(TextView view, bool force)
        {
            OvTextComponent model = (OvTextComponent)view.Model;
            string value = ComposeTokenText(model, view.TokenSpans, view.LastText);
            bool contentChanged = !ReferenceEquals(view.LastText, value);
            long nodeRevision = _nodeAnimations.GetVisualRevision(model);
            bool animDirty = view.LastNodeRevision != nodeRevision;
            if (!force && !contentChanged && !animDirty) return;
            if (contentChanged)
            {
                view.LastText = value;
                view.Text.text = value;
            }

            if (contentChanged || force)
            {
                float maxWidth = Mathf.Max(1f, Screen.width * 2f);
                Vector2 preferred = view.Text.GetPreferredValues(value, maxWidth, 0f);
                Vector2 targetSize = Pixel(new Vector2(Mathf.Max(1f, preferred.x + 4f), Mathf.Max(model.FontSize, preferred.y + 4f)));
                if (Mathf.Abs(view.Root.sizeDelta.x - targetSize.x) > 0.5f || Mathf.Abs(view.Root.sizeDelta.y - targetSize.y) > 0.5f)
                {
                    view.Root.sizeDelta = targetSize;
                }
            }

            Dictionary<string, OvTokenPose> poses = _nodeAnimations.GetPoses(model);
            if (poses != null && poses.Count > 0)
            {
                view.Text.ForceMeshUpdate(true, true);
                ApplyTokenPoses(view, model, poses);
            }
            view.LastNodeRevision = nodeRevision;
        }

        // Builds the text into the shared builder and only materializes a new string when the content
        // differs from previous; otherwise previous is returned so callers can compare by reference.
        private string ComposeTokenText(OvTextComponent model, Dictionary<string, TokenSpan> spans, string previous)
        {
            List<OvTextSourcePart> parts = OvTextTokenService.EnsureBindings(model);
            Dictionary<string, string> overrides = _nodeAnimations.GetTextOverrides(model);
            Dictionary<string, OvAnimationNode> formats = _nodeAnimations.GetNumberFormats(model);
            _tokenBuilder.Clear();
            spans.Clear();
            for (int i = 0; i < parts.Count; i++)
            {
                OvTextSourcePart part = parts[i];
                if (part == null) continue;
                if (part.IsStyleControl || part.Token == null)
                {
                    _tokenBuilder.Append(part.Source ?? string.Empty);
                    continue;
                }
                OvTextTokenBinding token = part.Token;
                int start = _tokenBuilder.Length;
                if (overrides != null && overrides.TryGetValue(token.Id, out string replacement))
                    _tokenBuilder.Append(replacement ?? string.Empty);
                else if (token.Kind == OvTextTokenKind.DynamicTag)
                {
                    if (formats != null && formats.TryGetValue(token.Id, out OvAnimationNode formatNode) && formatNode != null
                        && _tags.TryResolveSingleNumber(token.Lexeme, out double number))
                        _tokenBuilder.Append(FormatTokenNumber(token, number, formatNode));
                    else
                        _tags.AppendResolved(_tokenBuilder, token.Lexeme ?? string.Empty);
                }
                else _tokenBuilder.Append(token.Lexeme ?? string.Empty);
                spans[token.Id] = new TokenSpan { Start = start, End = _tokenBuilder.Length };
            }
            return BuilderEquals(_tokenBuilder, previous) ? previous : _tokenBuilder.ToString();
        }

        private static bool BuilderEquals(StringBuilder builder, string value)
        {
            if (value == null || builder.Length != value.Length) return false;
            for (int i = 0; i < value.Length; i++)
            {
                if (builder[i] != value[i]) return false;
            }

            return true;
        }

        private sealed class FormattedNumberCache
        {
            internal double Value;
            internal OvAnimationNode Node;
            internal OvNumberFormatKind Kind;
            internal int Decimals;
            internal int Width;
            internal OvPercentageInputKind PercentInput;
            internal string Text;
        }

        private readonly Dictionary<OvTextTokenBinding, FormattedNumberCache> _formattedNumbers =
            new Dictionary<OvTextTokenBinding, FormattedNumberCache>();

        private string FormatTokenNumber(OvTextTokenBinding token, double value, OvAnimationNode node)
        {
            if (!_formattedNumbers.TryGetValue(token, out FormattedNumberCache cache))
            {
                if (_formattedNumbers.Count > 256) _formattedNumbers.Clear();
                cache = new FormattedNumberCache();
                _formattedNumbers[token] = cache;
            }
            else if (cache.Text != null && cache.Value.Equals(value) && ReferenceEquals(cache.Node, node)
                && cache.Kind == node.NumberFormatKind && cache.Decimals == node.NumberFormatDecimals
                && cache.Width == node.NumberFormatWidth && cache.PercentInput == node.PercentageInputKind)
                return cache.Text;

            cache.Value = value;
            cache.Node = node;
            cache.Kind = node.NumberFormatKind;
            cache.Decimals = node.NumberFormatDecimals;
            cache.Width = node.NumberFormatWidth;
            cache.PercentInput = node.PercentageInputKind;
            cache.Text = FormatTokenNumber(token.Lexeme, value, node);
            return cache.Text;
        }

        private static string FormatTokenNumber(string tag, double value, OvAnimationNode node)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) value = 0d;
            int decimals = Math.Max(0, Math.Min(8, node.NumberFormatDecimals));
            string fixedFormat = decimals <= 0 ? "0" : "0." + new string('0', decimals);
            switch (node.NumberFormatKind)
            {
                case OvNumberFormatKind.Percentage:
                    bool ratio = node.PercentageInputKind == OvPercentageInputKind.Ratio
                        || (node.PercentageInputKind == OvPercentageInputKind.Auto && !OvNumericTagUnits.IsPercentage(tag));
                    if (ratio) value *= 100d;
                    return value.ToString(fixedFormat, CultureInfo.InvariantCulture) + "%";
                case OvNumberFormatKind.FixedDecimals:
                    return value.ToString(fixedFormat, CultureInfo.InvariantCulture);
                case OvNumberFormatKind.ZeroPaddedInteger:
                    int width = Math.Max(1, Math.Min(16, node.NumberFormatWidth));
                    double rounded = Math.Round(value, 0, MidpointRounding.AwayFromZero);
                    string digits = Math.Abs(rounded).ToString("0", CultureInfo.InvariantCulture).PadLeft(width, '0');
                    return rounded < 0d ? "-" + digits : digits;
                default:
                    return value.ToString("0.########", CultureInfo.InvariantCulture);
            }
        }

        private void ApplyTokenPoses(TextView view, OvTextComponent model, Dictionary<string, OvTokenPose> poses)
        {
            if (poses == null || poses.Count == 0 || view.Text.textInfo == null) return;
            TMP_TextInfo info = view.Text.textInfo;
            foreach (KeyValuePair<string, OvTokenPose> pair in poses)
            {
                if (!view.TokenSpans.TryGetValue(pair.Key, out TokenSpan span)) continue;
                ApplyPoseToSpan(info, span, pair.Value);
            }
            ApplyTokenGroupTransforms(info, view.TokenSpans, poses);
            view.Text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }

        private static void ApplyPoseToSpan(TMP_TextInfo info, TokenSpan span, OvTokenPose pose)
        {
            bool hasBounds = false;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible || character.index < span.Start || character.index >= span.End) continue;
                Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
                for (int v = 0; v < 4; v++)
                {
                    Vector3 point = vertices[character.vertexIndex + v];
                    min.x = Mathf.Min(min.x, point.x);
                    min.y = Mathf.Min(min.y, point.y);
                    max.x = Mathf.Max(max.x, point.x);
                    max.y = Mathf.Max(max.y, point.y);
                    hasBounds = true;
                }
            }
            if (!hasBounds) return;
            Vector2 center = (min + max) * 0.5f;
            float scaleX = Mathf.Abs(pose.ScaleX) < 0.0001f ? 1f : pose.ScaleX;
            float scaleY = Mathf.Abs(pose.ScaleY) < 0.0001f ? 1f : pose.ScaleY;
            float radians = pose.Rotation * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible || character.index < span.Start || character.index >= span.End) continue;
                int materialIndex = character.materialReferenceIndex;
                int vertexIndex = character.vertexIndex;
                Vector3[] vertices = info.meshInfo[materialIndex].vertices;
                Color32[] colors = info.meshInfo[materialIndex].colors32;
                for (int v = 0; v < 4; v++)
                {
                    Vector2 local = (Vector2)vertices[vertexIndex + v] - center;
                    local = new Vector2(local.x * scaleX, local.y * scaleY);
                    local = new Vector2(local.x * cosine - local.y * sine, local.x * sine + local.y * cosine);
                    vertices[vertexIndex + v] = center + local + new Vector2(pose.OffsetX, -pose.OffsetY);
                    Color32 color = colors[vertexIndex + v];
                    if (pose.HasTextColorOverride)
                    {
                        color.r = (byte)Mathf.RoundToInt(Mathf.Clamp01(pose.TextColorR) * 255f);
                        color.g = (byte)Mathf.RoundToInt(Mathf.Clamp01(pose.TextColorG) * 255f);
                        color.b = (byte)Mathf.RoundToInt(Mathf.Clamp01(pose.TextColorB) * 255f);
                        color.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(pose.TextColorA * pose.Opacity) * 255f);
                    }
                    else color.a = (byte)Mathf.RoundToInt(color.a * Mathf.Clamp01(pose.Opacity));
                    colors[vertexIndex + v] = color;
                }
            }
        }

        private void ApplyTokenGroupTransforms(TMP_TextInfo info, Dictionary<string, TokenSpan> spans,
            Dictionary<string, OvTokenPose> poses)
        {
            _tokenGroupOperationMap.Clear();
            _tokenGroupOperations.Clear();
            foreach (KeyValuePair<string, OvTokenPose> pair in poses)
            {
                List<OvTokenGroupTransform> transforms = pair.Value.GroupTransforms;
                if (transforms == null || !spans.ContainsKey(pair.Key)) continue;
                for (int i = 0; i < transforms.Count; i++)
                {
                    OvTokenGroupTransform transform = transforms[i];
                    if (transform == null || string.IsNullOrEmpty(transform.Id)) continue;
                    if (!_tokenGroupOperationMap.TryGetValue(transform.Id, out TokenGroupOperation operation))
                    {
                        int poolIndex = _tokenGroupOperations.Count;
                        if (poolIndex < _tokenGroupOperationPool.Count) operation = _tokenGroupOperationPool[poolIndex];
                        else
                        {
                            operation = new TokenGroupOperation();
                            _tokenGroupOperationPool.Add(operation);
                        }
                        operation.Transform = transform;
                        operation.TokenIds.Clear();
                        _tokenGroupOperationMap[transform.Id] = operation;
                        _tokenGroupOperations.Add(operation);
                    }
                    operation.TokenIds.Add(pair.Key);
                }
            }
            _tokenGroupOperations.Sort((left, right) =>
            {
                int order = left.Transform.Order.CompareTo(right.Transform.Order);
                return order != 0 ? order : string.CompareOrdinal(left.Transform.Id, right.Transform.Id);
            });
            for (int i = 0; i < _tokenGroupOperations.Count; i++)
                ApplyTokenGroupTransform(info, spans, _tokenGroupOperations[i]);
        }

        private static void ApplyTokenGroupTransform(TMP_TextInfo info, Dictionary<string, TokenSpan> spans,
            TokenGroupOperation operation)
        {
            bool hasBounds = false;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (string tokenId in operation.TokenIds)
            {
                if (!spans.TryGetValue(tokenId, out TokenSpan span)) continue;
                ExpandSpanBounds(info, span, ref min, ref max, ref hasBounds);
            }
            if (!hasBounds) return;
            Vector2 pivot = (min + max) * 0.5f;
            foreach (string tokenId in operation.TokenIds)
            {
                if (!spans.TryGetValue(tokenId, out TokenSpan span)) continue;
                TransformSpanVertices(info, span, pivot, operation.Transform);
            }
        }

        private static void ExpandSpanBounds(TMP_TextInfo info, TokenSpan span, ref Vector2 min,
            ref Vector2 max, ref bool hasBounds)
        {
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible || character.index < span.Start || character.index >= span.End) continue;
                Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
                for (int v = 0; v < 4; v++)
                {
                    Vector3 point = vertices[character.vertexIndex + v];
                    min.x = Mathf.Min(min.x, point.x);
                    min.y = Mathf.Min(min.y, point.y);
                    max.x = Mathf.Max(max.x, point.x);
                    max.y = Mathf.Max(max.y, point.y);
                    hasBounds = true;
                }
            }
        }

        private static void TransformSpanVertices(TMP_TextInfo info, TokenSpan span, Vector2 pivot,
            OvTokenGroupTransform transform)
        {
            float scaleX = Mathf.Abs(transform.ScaleX) < 0.0001f ? 1f : transform.ScaleX;
            float scaleY = Mathf.Abs(transform.ScaleY) < 0.0001f ? 1f : transform.ScaleY;
            float radians = transform.Rotation * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible || character.index < span.Start || character.index >= span.End) continue;
                Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
                for (int v = 0; v < 4; v++)
                {
                    int index = character.vertexIndex + v;
                    Vector2 local = (Vector2)vertices[index] - pivot;
                    local = new Vector2(local.x * scaleX, local.y * scaleY);
                    local = new Vector2(local.x * cosine - local.y * sine, local.x * sine + local.y * cosine);
                    Vector3 point = vertices[index];
                    vertices[index] = new Vector3(pivot.x + local.x + transform.OffsetX,
                        pivot.y + local.y - transform.OffsetY, point.z);
                }
            }
        }

        private void UpdateProgress(ProgressView view)
        {
            OvProgressComponent model = (OvProgressComponent)view.Model;
            double min = string.IsNullOrWhiteSpace(model.MinimumTag) ? model.Minimum : _tags.ResolveNumeric(model.MinimumTag);
            double max = string.IsNullOrWhiteSpace(model.MaximumTag) ? model.Maximum : _tags.ResolveNumeric(model.MaximumTag);
            double value = model.ValueIsConstant ? model.ValueConstant : _tags.ResolveNumeric(model.ValueTag);
            double normalized = (max <= min) ? 0d : (value - min) / (max - min);
            if (double.IsNaN(normalized) || double.IsInfinity(normalized)) normalized = 0d;
            if (model.ClampValue) normalized = Math.Max(0d, Math.Min(1d, normalized));
            if (model.Reverse) normalized = 1d - normalized;
            float ratio = Mathf.Clamp01((float)normalized);
            if (Mathf.Approximately(view.LastRatio, ratio)) return;
            view.LastRatio = ratio;
            Color fillColor = ToColor(model.FillColor);
            if (model.EnableFillGradient)
                fillColor = Color.Lerp(ToColor(model.FillGradientStartColor), ToColor(model.FillGradientEndColor), ratio);
            view.Fill.SetStyle(WithOpacity(fillColor, model.Opacity), Color.clear, 0f, model.CornerRadius);
            Vector2 size = view.Root.sizeDelta;
            switch (model.FillDirection)
            {
                case OvProgressFillDirection.RightToLeft:
                    SetFillMask(view.FillMask, view.FillRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(size.x * ratio, size.y));
                    break;
                case OvProgressFillDirection.BottomToTop:
                    SetFillMask(view.FillMask, view.FillRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(size.x, size.y * ratio));
                    break;
                case OvProgressFillDirection.TopToBottom:
                    SetFillMask(view.FillMask, view.FillRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(size.x, size.y * ratio));
                    break;
                default:
                    SetFillMask(view.FillMask, view.FillRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(size.x * ratio, size.y));
                    break;
            }
        }

        private static void SetFillMask(RectTransform fillMask, RectTransform fillRect,
            Vector2 anchor, Vector2 pivot, Vector2 maskSize)
        {
            fillMask.anchorMin = fillMask.anchorMax = anchor;
            fillMask.pivot = pivot;
            fillMask.anchoredPosition = Vector2.zero;
            fillMask.sizeDelta = Pixel(maskSize);

            fillRect.anchorMin = fillRect.anchorMax = anchor;
            fillRect.pivot = pivot;
            fillRect.anchoredPosition = Vector2.zero;
            // Size the fill to the progress itself so its leading end keeps the
            // rounded corner instead of being cut straight by the mask.
            fillRect.sizeDelta = Pixel(maskSize);
        }

        private void ApplyTextEffects(TextView view, OvTextComponent model)
        {
            if (view.Text.font == null) return;
            if (view.Material == null)
            {
                view.Material = Instantiate(view.Text.font.material);
                view.Material.name = view.Text.font.name + "_CheryToolsOvMaterial";
            }
            Color outline = ToColor(model.OutlineColor);
            float outlineWidth = model.OutlineEnabled && outline.a > 0f
                ? Mathf.Clamp(Mathf.Max(0f, model.OutlineThickness) / 32f, 0f, 0.35f) : 0f;
            view.Material.SetFloat(OutlineWidthProperty, outlineWidth);
            view.Material.SetColor(OutlineColorProperty, outline);
            if (outlineWidth > 0f) view.Material.EnableKeyword("OUTLINE_ON");
            else view.Material.DisableKeyword("OUTLINE_ON");
            ApplyUnderlay(view.Material, model);
            view.Text.fontSharedMaterial = view.Material;
            // TMP 会依据当前材质参数计算字形几何的外扩范围。缺少这一步时，
            // Underlay/Outline 仍使用文字本体的旧 padding，阴影与描边便会在
            // RectTransform 边缘被截断。
            view.Text.UpdateMeshPadding();
            view.Text.SetMaterialDirty();
            view.Text.SetVerticesDirty();
        }

        private static void ApplyUnderlay(Material material, OvTextComponent model)
        {
            if (material == null || !material.HasProperty(UnderlayColorProperty)) return;
            Color shadow = ToColor(model.ShadowColor);
            bool enabled = model.ShadowEnabled && shadow.a > 0f;
            if (!enabled)
            {
                material.DisableKeyword("UNDERLAY_ON");
                material.SetColor(UnderlayColorProperty, Color.clear);
                return;
            }
            float x = model.ShadowOffset != null && model.ShadowOffset.Length > 0 ? model.ShadowOffset[0] : 0f;
            float y = model.ShadowOffset != null && model.ShadowOffset.Length > 1 ? model.ShadowOffset[1] : 0f;
            material.EnableKeyword("UNDERLAY_ON");
            material.DisableKeyword("UNDERLAY_INNER");
            material.SetColor(UnderlayColorProperty, shadow);
            if (material.HasProperty(UnderlayOffsetXProperty)) material.SetFloat(UnderlayOffsetXProperty, Mathf.Clamp(x / 32f, -1f, 1f));
            if (material.HasProperty(UnderlayOffsetYProperty)) material.SetFloat(UnderlayOffsetYProperty, Mathf.Clamp(-y / 32f, -1f, 1f));
            if (material.HasProperty(UnderlayDilateProperty)) material.SetFloat(UnderlayDilateProperty, 0f);
            if (material.HasProperty(UnderlaySoftnessProperty)) material.SetFloat(UnderlaySoftnessProperty, Mathf.Clamp01(Mathf.Max(0f, model.ShadowSoftness) / 32f));
        }

        private Texture2D LoadTexture(string value)
        {
            string path = ResolvePath(value);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                Func<Texture2D, byte[], bool> loader = ResolveImageLoader();
                if (loader != null && loader(texture, File.ReadAllBytes(path))) return texture;
                Destroy(texture);
            }
            catch (Exception ex) { OvRuntime.Logger?.Warning(L("OV 图片加载失败：", "OV image failed to load: ") + ex.GetBaseException().Message); }
            return null;
        }

        private TMP_FontAsset ResolveFont(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return _defaultFont;
            string path = ResolvePath(value);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                if (!Path.IsPathRooted(value))
                {
                    string parentPath = Path.Combine(Directory.GetParent(OvRuntime.ModuleDirectory ?? string.Empty)?.FullName ?? string.Empty, value.Trim().Trim('"'));
                    if (File.Exists(parentPath)) path = parentPath;
                }
            }
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return _defaultFont;
            if (_fontCache.TryGetValue(path, out TMP_FontAsset cached)) return cached ?? _defaultFont;
            try
            {
                Font source = new Font(path);
                _createdFonts.Add(source);
                TMP_FontAsset asset = CreateFontAsset(source, "CheryTools_OV_" + Path.GetFileNameWithoutExtension(path));
                if (asset != null)
                {
                    _fontCache[path] = asset;
                    return asset;
                }
            }
            catch (Exception ex) { OvRuntime.Logger?.Warning(L("OV 字体加载失败：", "OV font failed to load: ") + ex.GetBaseException().Message); }
            _fontCache[path] = _defaultFont;
            return _defaultFont;
        }

        private TMP_FontAsset CreateFontAsset(Font source, string name)
        {
            if (source == null) return null;
            try
            {
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(source, 72, 9, GlyphRenderMode.SDFAA,
                    1024, 1024, AtlasPopulationMode.Dynamic, true);
                if (asset == null) return null;
                asset.name = name;
                asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                asset.isMultiAtlasTexturesEnabled = true;
                _createdFontAssets.Add(asset);
                return asset;
            }
            catch { return null; }
        }

        private static bool ShouldShow(OvComponent model, bool playing)
        {
            if (model == null || !model.Enabled) return false;
            if (model.OnlyShowPlaying && !playing) return false;
            if (!model.ShowInGame && playing) return false;
            return true;
        }

        private static bool IsPlaying()
        {
            try
            {
                if (ADOBase.isLevelEditor)
                {
                    scnEditor editor = scnEditor.instance ?? ADOBase.editor;
                    if (editor != null)
                    {
                        return editor.playMode && !editor.pausedInPlayMode;
                    }
                }
                return scrController.instance != null && scrController.instance.gameworld && !scrController.instance.paused;
            }
            catch { return false; }
        }

        private static string ResolvePath(string value)
        {
            string path = (value ?? string.Empty).Trim().Trim('"');
            if (string.IsNullOrEmpty(path)) return string.Empty;
            if (!Path.IsPathRooted(path)) path = Path.Combine(OvRuntime.ModuleDirectory ?? string.Empty, path);
            try { return Path.GetFullPath(path); }
            catch { return path; }
        }

        private static Func<Texture2D, byte[], bool> ResolveImageLoader()
        {
            if (_imageLoaderResolved) return _loadImage;
            _imageLoaderResolved = true;
            Type type = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", false);
            var method = type?.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
            if (method != null)
            {
                try { _loadImage = (Func<Texture2D, byte[], bool>)Delegate.CreateDelegate(typeof(Func<Texture2D, byte[], bool>), method); }
                catch { }
            }
            return _loadImage;
        }

        private static void UpdateVideoPlayback(VideoView view)
        {
            if (view.Player == null || view.Started) return;
            if (GetBoolProperty(view.Player, "isPlaying")) { view.Started = true; return; }
            if (!GetBoolProperty(view.Player, "isPrepared")) return;
            SonnetUguiKit.Invoke(view.Player, "Play");
            view.Started = true;
        }

        private static void ReleaseView(ComponentView view)
        {
            if (view == null) return;
            if (view is TextView text && text.Material != null) Destroy(text.Material);
            if (view is ImageView image && image.Texture != null) Destroy(image.Texture);
            if (view is VideoView video)
            {
                if (video.Player != null) { SonnetUguiKit.Invoke(video.Player, "Stop"); SetProperty(video.Player, "targetTexture", null); }
                if (video.Texture != null) { video.Texture.Release(); Destroy(video.Texture); }
            }
            if (view.Root != null) Destroy(view.Root.gameObject);
        }

        private void OnDestroy()
        {
            ClearRuntime();
            for (int i = 0; i < _createdFontAssets.Count; i++)
            {
                TMP_FontAsset asset = _createdFontAssets[i];
                if (asset == null) continue;
                if (asset.atlasTextures != null)
                    for (int j = 0; j < asset.atlasTextures.Length; j++) if (asset.atlasTextures[j] != null) Destroy(asset.atlasTextures[j]);
                if (asset.material != null) Destroy(asset.material);
                Destroy(asset);
            }
            for (int i = 0; i < _createdFonts.Count; i++) if (_createdFonts[i] != null) Destroy(_createdFonts[i]);
            _createdFontAssets.Clear();
            _createdFonts.Clear();
            _fontCache.Clear();
        }

        // Each component gets its own nested canvas so a text or progress bar that changes every frame
        // only rebatches itself, not every other Overlayer and KeyViewer element on the module canvas.
        private static RectTransform CreateComponentRoot(string name, Transform parent)
        {
            RectTransform rect = CreateRect(name, parent);
            IsolateCanvas(rect);
            return rect;
        }

        private static Color WithOpacity(Color color, float opacity)
        {
            color.a *= Mathf.Clamp01(opacity);
            return color;
        }

        private static bool GetBoolProperty(Component target, string name)
        {
            try { return target != null && (bool)target.GetType().GetProperty(name).GetValue(target, null); }
            catch { return false; }
        }
    }
}
