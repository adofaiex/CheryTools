using System;
using UnityEngine;

namespace CheryTools
{
    public class KeyViewerOverlay : MonoBehaviour
    {
        public static KeyViewerOverlay Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                _bakedNodes.Clear();
                Instance = null;
            }
        }

        private sealed class NodeRenderIds
        {
            public string BackgroundImage;
            public string Box;
            public string KeyText;
            public string CountText;
            public string KpsLabel;
            public string KpsValue;
            public string TotalLabel;
            public string TotalValue;
        }

        private readonly System.Collections.Generic.Dictionary<KVNode, NodeRenderIds> _nodeRenderIds = new System.Collections.Generic.Dictionary<KVNode, NodeRenderIds>();
        // Reused per DrawKeyRain call: drops are filtered once and split by row, so
        // the config/node matching (including the O(n) Nodes.Contains fallback) no
        // longer runs twice over every active drop.
        private readonly System.Collections.Generic.List<KeyDrop> _rainRow1Buffer = new System.Collections.Generic.List<KeyDrop>();
        private readonly System.Collections.Generic.List<KeyDrop> _rainRow2Buffer = new System.Collections.Generic.List<KeyDrop>();
        private readonly System.Collections.Generic.Dictionary<string, TextDrawState> _textDrawStates = new System.Collections.Generic.Dictionary<string, TextDrawState>();
        private int _nextNodeRenderId = 1;
        private bool _hadVideoLastFrame = false;

        private sealed class TextDrawState
        {
            public string Text;
            public string FontPath;
            public float FontSize;
            public Vector2 Position;
            public Vector2 Size;
            public int Alignment;
            public uint Color;
            public bool UseGradient;
            public uint ColorTopLeft;
            public uint ColorTopRight;
            public uint ColorBottomRight;
            public uint ColorBottomLeft;
            public bool OutlineEnabled;
            public uint OutlineColor;
            public float OutlineThickness;
            public bool ShadowEnabled;
            public uint ShadowColor;
            public Vector2 ShadowOffset;
            public float ShadowSoftness;
            public int SortingOrder;
        }

        // Immutable, revision-scoped render data.  Editing remains fully free-form,
        // but gameplay no longer resolves the same colors, fonts, geometry and
        // animation settings for every node on every rain/animation frame.
        private sealed class BakedKvNode
        {
            public KVConfiguration Owner;
            public NodeRenderIds Ids;
            public float FinalScale;
            public Vector2 BasePosition;
            public Vector2 BaseSize;
            public uint BackgroundNormal;
            public uint BackgroundPressed;
            public uint BorderNormal;
            public uint BorderPressed;
            public uint TextNormal;
            public uint TextPressed;
            public bool KeyOutlineEnabled;
            public bool CountOutlineEnabled;
            public uint KeyOutlineColor;
            public uint CountOutlineColor;
            public float KeyOutlineThickness;
            public float CountOutlineThickness;
            public bool KeyShadowEnabled;
            public bool CountShadowEnabled;
            public uint KeyShadowColor;
            public uint CountShadowColor;
            public Vector2 KeyShadowOffset;
            public Vector2 CountShadowOffset;
            public float KeyShadowSoftness;
            public float CountShadowSoftness;
            public bool HideCountText;
            public KeyPressAnimationSettings Animation;
            public float BorderThickness;
            public float CornerRadius;
            public int GraphicSortingOrder;
            public int TextSortingOrder;
            public string KeyFontPath;
            public string CountFontPath;
            public string Label;
            public bool RainEnabled;
            public float RainWidthRatio;
            public uint RainBaseColor;
            public uint RainFarColor;
            public bool RainGradientEnabled;
            public bool RainHorizontalGradientEnabled;
            public int RainGradientMode;
            public uint RainHorizontalColor;
            public float RainYOffset;
            public float RainCornerRadius;
            public float RainFadeHeight;
            public float RainFadePower;
            public float RainGradientHeight;
            public float RainGradientPower;
            public bool RainShadowEnabled;
            public uint RainShadowColor;
            public Vector2 RainShadowOffset;
            public float RainShadowSoftness;
            public float RainShadowStrength;
            public int RainShadowSortingOrder;
            public int RainSortingOrder;
            public bool ImageBaked;
            public float ImageBakedAlpha;
            public bool KeyTextBaked;
            public uint KeyTextBakedColor;
            public bool CountTextBaked;
            public uint CountTextBakedColor;
            public int CountTextBakedValue;
            public bool PairLabelBaked;
        }

        private readonly System.Collections.Generic.Dictionary<KVNode, BakedKvNode> _bakedNodes
            = new System.Collections.Generic.Dictionary<KVNode, BakedKvNode>();
        private long _kvBakeRevision = -1;
        private int _kvBakeScreenWidth = -1;
        private int _kvBakeScreenHeight = -1;

        private struct ColorCorners
        {
            public uint TopLeft;
            public uint TopRight;
            public uint BottomRight;
            public uint BottomLeft;

            public static ColorCorners Solid(uint color)
            {
                return new ColorCorners
                {
                    TopLeft = color,
                    TopRight = color,
                    BottomRight = color,
                    BottomLeft = color
                };
            }
        }

        private NodeRenderIds GetNodeRenderIds(KVNode node)
        {
            if (node == null) return null;
            if (_nodeRenderIds.TryGetValue(node, out NodeRenderIds ids)) return ids;

            string prefix = "KV_" + (_nextNodeRenderId++).ToString();
            ids = new NodeRenderIds
            {
                BackgroundImage = prefix + "_bg",
                Box = prefix + "_box",
                KeyText = prefix + "_key",
                CountText = prefix + "_count",
                KpsLabel = prefix + "_kps_label",
                KpsValue = prefix + "_kps_value",
                TotalLabel = prefix + "_total_label",
                TotalValue = prefix + "_total_value"
            };
            _nodeRenderIds[node] = ids;
            return ids;
        }

        private uint Vector4ToColor(float[] arr)
        {
            if (arr == null || arr.Length < 4) return 0xFFFFFFFF;
            byte r = (byte)(Mathf.Clamp01(arr[0]) * 255);
            byte g = (byte)(Mathf.Clamp01(arr[1]) * 255);
            byte b = (byte)(Mathf.Clamp01(arr[2]) * 255);
            byte a = (byte)(Mathf.Clamp01(arr[3]) * 255);
            return (uint)((a << 24) | (b << 16) | (g << 8) | r);
        }

        private uint MultiplyAlpha(uint color, float ratio)
        {
            byte a = (byte)((color >> 24) & 0xFF);
            byte b = (byte)((color >> 16) & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte r = (byte)(color & 0xFF);
            byte newA = (byte)(a * Mathf.Clamp01(ratio));
            return (uint)((newA << 24) | (b << 16) | (g << 8) | r);
        }

        private uint LerpColor(uint from, uint to, float t)
        {
            t = Mathf.Clamp01(t);
            byte fr = (byte)(from & 0xFF);
            byte fg = (byte)((from >> 8) & 0xFF);
            byte fb = (byte)((from >> 16) & 0xFF);
            byte fa = (byte)((from >> 24) & 0xFF);
            byte tr = (byte)(to & 0xFF);
            byte tg = (byte)((to >> 8) & 0xFF);
            byte tb = (byte)((to >> 16) & 0xFF);
            byte ta = (byte)((to >> 24) & 0xFF);
            byte r = (byte)Mathf.RoundToInt(Mathf.Lerp(fr, tr, t));
            byte g = (byte)Mathf.RoundToInt(Mathf.Lerp(fg, tg, t));
            byte b = (byte)Mathf.RoundToInt(Mathf.Lerp(fb, tb, t));
            byte a = (byte)Mathf.RoundToInt(Mathf.Lerp(fa, ta, t));
            return (uint)((a << 24) | (b << 16) | (g << 8) | r);
        }

        private uint MatchAlpha(uint color, uint alphaSource)
        {
            byte a = (byte)((color >> 24) & 0xFF);
            byte b = (byte)((color >> 16) & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte r = (byte)(color & 0xFF);
            float sourceAlpha = ((alphaSource >> 24) & 0xFF) / 255f;
            byte newA = (byte)Mathf.RoundToInt(a * sourceAlpha);
            return (uint)((newA << 24) | (b << 16) | (g << 8) | r);
        }

        private ColorCorners LerpCorners(ColorCorners from, ColorCorners to, float t)
        {
            return new ColorCorners
            {
                TopLeft = LerpColor(from.TopLeft, to.TopLeft, t),
                TopRight = LerpColor(from.TopRight, to.TopRight, t),
                BottomRight = LerpColor(from.BottomRight, to.BottomRight, t),
                BottomLeft = LerpColor(from.BottomLeft, to.BottomLeft, t)
            };
        }

        private bool IsSolid(ColorCorners colors)
        {
            return colors.TopLeft == colors.TopRight
                && colors.TopLeft == colors.BottomRight
                && colors.TopLeft == colors.BottomLeft;
        }

        private float GetKeyPressAnimationProgress(KVNode node, bool pressed, KeyPressAnimationSettings animationSettings)
        {
            if (node == null || !animationSettings.Enabled)
            {
                return pressed ? 1f : 0f;
            }

            float progress = pressed ? 1f : 0f;
            if (KeyViewerManager.Instance != null && KeyViewerManager.Instance.KeyPressAnimationProgress.TryGetValue(node, out float rawProgress))
            {
                progress = rawProgress;
            }
            return EasingUtil.EvaluateEasing(progress, animationSettings.Easing);
        }

        private void PrepareKvRuntimeBake()
        {
            long revision = OverlayRenderInvalidator.Revision;
            if (_kvBakeRevision == revision
                && _kvBakeScreenWidth == Screen.width
                && _kvBakeScreenHeight == Screen.height)
            {
                return;
            }

            _bakedNodes.Clear();
            _kvBakeRevision = revision;
            _kvBakeScreenWidth = Screen.width;
            _kvBakeScreenHeight = Screen.height;
        }

        private BakedKvNode GetBakedNode(KVConfiguration config, KVNode node, Vector2 center,
            float globalScale, float rounding, float configBorderThickness)
        {
            if (_bakedNodes.TryGetValue(node, out BakedKvNode baked)
                && ReferenceEquals(baked.Owner, config))
            {
                return baked;
            }

            bool useNodeColor = node.UseCustomColor;
            bool useNodeOutline = node.UseCustomOutline;
            bool useNodeShadow = node.UseCustomShadow;
            float finalScale = globalScale * node.Scale;
            baked = new BakedKvNode
            {
                Owner = config,
                Ids = GetNodeRenderIds(node),
                FinalScale = finalScale,
                BasePosition = new Vector2(center.x + node.PositionX * globalScale, center.y + node.PositionY * globalScale),
                BaseSize = new Vector2(node.Width * finalScale, node.Height * finalScale),
                BackgroundNormal = Vector4ToColor(useNodeColor ? node.ColorBgNormal : config.ColorBgNormal),
                BackgroundPressed = Vector4ToColor(useNodeColor ? node.ColorBgPressed : config.ColorBgPressed),
                BorderNormal = Vector4ToColor(useNodeColor ? node.ColorBorderNormal : config.ColorBorderNormal),
                BorderPressed = Vector4ToColor(useNodeColor ? node.ColorBorderPressed : config.ColorBorderPressed),
                TextNormal = Vector4ToColor(useNodeColor ? node.ColorTextNormal : config.ColorTextNormal),
                TextPressed = Vector4ToColor(useNodeColor ? node.ColorTextPressed : config.ColorTextPressed),
                KeyOutlineEnabled = useNodeOutline ? node.KeyTextOutlineEnabled : config.KeyTextOutlineEnabled,
                CountOutlineEnabled = useNodeOutline ? node.CountTextOutlineEnabled : config.CountTextOutlineEnabled,
                KeyOutlineColor = useNodeOutline
                    ? TextStyleRenderer.ColorArrayToU32(node.KeyTextOutlineColor, 0xFF000000)
                    : TextStyleRenderer.ColorArrayToU32(config.KeyTextOutlineColor, 0xFF000000),
                CountOutlineColor = useNodeOutline
                    ? TextStyleRenderer.ColorArrayToU32(node.CountTextOutlineColor, 0xFF000000)
                    : TextStyleRenderer.ColorArrayToU32(config.CountTextOutlineColor, 0xFF000000),
                KeyOutlineThickness = useNodeOutline ? node.KeyTextOutlineThickness : config.KeyTextOutlineThickness,
                CountOutlineThickness = useNodeOutline ? node.CountTextOutlineThickness : config.CountTextOutlineThickness,
                KeyShadowEnabled = useNodeShadow ? node.KeyTextShadowEnabled : config.KeyTextShadowEnabled,
                CountShadowEnabled = useNodeShadow ? node.CountTextShadowEnabled : config.CountTextShadowEnabled,
                HideCountText = config.HideCountText || node.HideCountText,
                Animation = KeyPressAnimationSettings.Resolve(config, node),
                BorderThickness = node.BorderThickness >= 0f ? node.BorderThickness : configBorderThickness,
                CornerRadius = ResolveNodeCornerRadius(node, rounding, globalScale),
                GraphicSortingOrder = RenderDepth.ToSortingOrder(node.Depth, RenderDepth.SublayerGraphic),
                TextSortingOrder = RenderDepth.ToSortingOrder(node.Depth, RenderDepth.SublayerText),
                KeyFontPath = GetKeyFontPath(config, node),
                CountFontPath = GetCountFontPath(config, node),
                Label = !string.IsNullOrEmpty(node.CustomText)
                    ? node.CustomText
                    : node.NodeType == 0
                        ? KeyDisplayNames.GetKeySymbol(node.KeyBind)
                        : node.NodeType == 1 ? "KPS" : "Total"
            };

            if (baked.KeyShadowEnabled)
            {
                baked.KeyShadowColor = useNodeShadow
                    ? TextStyleRenderer.ColorArrayToU32(node.KeyTextShadowColor, 0xB3000000)
                    : TextStyleRenderer.ColorArrayToU32(config.KeyTextShadowColor, 0xB3000000);
                float[] offset = useNodeShadow ? node.KeyTextShadowOffset : config.KeyTextShadowOffset;
                baked.KeyShadowOffset = new Vector2(
                    offset != null && offset.Length > 0 ? offset[0] : 2f,
                    offset != null && offset.Length > 1 ? offset[1] : 2f);
                baked.KeyShadowSoftness = useNodeShadow ? node.KeyTextShadowSoftness : config.KeyTextShadowSoftness;
            }
            if (baked.CountShadowEnabled)
            {
                baked.CountShadowColor = useNodeShadow
                    ? TextStyleRenderer.ColorArrayToU32(node.CountTextShadowColor, 0xB3000000)
                    : TextStyleRenderer.ColorArrayToU32(config.CountTextShadowColor, 0xB3000000);
                float[] offset = useNodeShadow ? node.CountTextShadowOffset : config.CountTextShadowOffset;
                baked.CountShadowOffset = new Vector2(
                    offset != null && offset.Length > 0 ? offset[0] : 2f,
                    offset != null && offset.Length > 1 ? offset[1] : 2f);
                baked.CountShadowSoftness = useNodeShadow ? node.CountTextShadowSoftness : config.CountTextShadowSoftness;
            }

            bool row1 = node.RainRow == 1;
            bool customRain = node.UseCustomRain;
            baked.RainEnabled = customRain ? node.EnableKeyRain : config.EnableKeyRain;
            baked.RainWidthRatio = customRain ? node.RainWidthRatio : (row1 ? config.KeyRainWidthRatio1 : config.KeyRainWidthRatio2);
            baked.RainBaseColor = Vector4ToColor(customRain ? node.RainColor : (row1 ? config.KeyRainColorRow1 : config.KeyRainColorRow2));
            baked.RainFarColor = Vector4ToColor(customRain ? node.RainGradientEndColor : (row1 ? config.KeyRainGradientEndColorRow1 : config.KeyRainGradientEndColorRow2));
            baked.RainGradientEnabled = customRain ? node.RainGradientEnabled : config.KeyRainGradientEnabled;
            baked.RainHorizontalGradientEnabled = customRain ? node.RainHorizontalGradientEnabled : config.KeyRainHorizontalGradientEnabled;
            baked.RainGradientMode = customRain ? node.RainGradientMode : config.KeyRainGradientMode;
            baked.RainHorizontalColor = MatchAlpha(
                Vector4ToColor(customRain
                    ? node.RainHorizontalGradientEndColor
                    : (row1 ? config.KeyRainHorizontalGradientEndColorRow1 : config.KeyRainHorizontalGradientEndColorRow2)),
                baked.RainBaseColor);
            baked.RainYOffset = customRain ? node.RainYOffset : (row1 ? config.KeyRainYOffsetRow1 : config.KeyRainYOffsetRow2);
            baked.RainCornerRadius = Mathf.Max(0f, customRain ? node.RainCornerRadius : config.KeyRainCornerRadius) * globalScale;
            baked.RainFadeHeight = customRain ? node.RainFadeHeight : config.KeyRainFadeHeight;
            baked.RainFadePower = customRain ? node.RainFadePower : config.KeyRainFadePower;
            baked.RainGradientHeight = customRain ? node.RainGradientHeight : config.KeyRainGradientHeight;
            baked.RainGradientPower = customRain ? node.RainGradientPower : config.KeyRainGradientPower;

            bool customRainShadow = node.UseCustomRainShadow;
            baked.RainShadowEnabled = customRainShadow ? node.RainShadowEnabled : config.KeyRainShadowEnabled;
            baked.RainShadowStrength = Mathf.Clamp01(customRainShadow ? node.RainShadowStrength : config.KeyRainShadowStrength);
            if (baked.RainShadowEnabled && baked.RainShadowStrength > 0f)
            {
                baked.RainShadowColor = Vector4ToColor(customRainShadow ? node.RainShadowColor : config.KeyRainShadowColor);
                baked.RainShadowEnabled = Alpha01(baked.RainShadowColor) > 0f;
                baked.RainShadowOffset = ResolvePair(customRainShadow ? node.RainShadowOffset : config.KeyRainShadowOffset, 0f, 0f) * globalScale;
                baked.RainShadowSoftness = Mathf.Max(0f, customRainShadow ? node.RainShadowSoftness : config.KeyRainShadowSoftness) * globalScale;
            }
            baked.RainShadowSortingOrder = RenderDepth.ToSortingOrder(node.Depth, RenderDepth.SublayerRainShadow);
            baked.RainSortingOrder = RenderDepth.ToSortingOrder(node.Depth, RenderDepth.SublayerRain);

            _bakedNodes[node] = baked;
            return baked;
        }

        private static float Alpha01(uint color)
        {
            return ((color >> 24) & 0xFF) / 255f;
        }

        private Vector4 ColorU32ToVector4(uint color)
        {
            return new Vector4(
                (color & 0xFF) / 255f,
                ((color >> 8) & 0xFF) / 255f,
                ((color >> 16) & 0xFF) / 255f,
                ((color >> 24) & 0xFF) / 255f);
        }

        private static float TextBoxHeight(float fontSize)
        {
            return Mathf.Max(1f, fontSize * 1.25f);
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) < 0.001f;
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Approximately(a.x, b.x) && Approximately(a.y, b.y);
        }

        private static float ResolveNodeCornerRadius(KVNode node, float defaultRadius, float globalScale)
        {
            if (node == null) return defaultRadius;
            float radius = node.CornerRadius;
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f)
                return defaultRadius;
            return Mathf.Max(0f, radius * globalScale);
        }

        private string GetKeyFontPath(KVConfiguration config, KVNode node)
        {
            if (node != null && !string.IsNullOrEmpty(node.KeyFontPath)) return node.KeyFontPath;
            if (config != null && !string.IsNullOrEmpty(config.FontPath)) return config.FontPath;
            return string.Empty;
        }

        private string GetCountFontPath(KVConfiguration config, KVNode node)
        {
            if (node != null && !string.IsNullOrEmpty(node.CountFontPath)) return node.CountFontPath;
            if (config != null && !string.IsNullOrEmpty(config.FontPath)) return config.FontPath;
            return string.Empty;
        }

        private void DrawText(string id, string text, string fontPath, float fontSize, Vector2 pos, Vector2 size, int alignment, uint color, bool outlineEnabled, uint outlineColor, float outlineThickness, bool shadowEnabled, uint shadowColor, Vector2 shadowOffset, float shadowSoftness, int sortingOrder)
        {
            DrawText(id, text, fontPath, fontSize, pos, size, alignment, ColorCorners.Solid(color), outlineEnabled, outlineColor, outlineThickness, shadowEnabled, shadowColor, shadowOffset, shadowSoftness, sortingOrder);
        }

        private void DrawText(string id, string text, string fontPath, float fontSize, Vector2 pos, Vector2 size, int alignment, ColorCorners colors, bool outlineEnabled, uint outlineColor, float outlineThickness, bool shadowEnabled, uint shadowColor, Vector2 shadowOffset, float shadowSoftness, int sortingOrder)
        {
            string safeText = text ?? string.Empty;
            string safeFontPath = fontPath ?? string.Empty;
            Vector2 safeSize = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            bool useGradient = !IsSolid(colors);
            uint color = colors.TopLeft;

            if (_textDrawStates.TryGetValue(id, out TextDrawState state)
                && state.SortingOrder == sortingOrder
                && string.Equals(state.Text, safeText, StringComparison.Ordinal)
                && string.Equals(state.FontPath, safeFontPath, StringComparison.Ordinal)
                && Approximately(state.FontSize, fontSize)
                && Approximately(state.Position, pos)
                && Approximately(state.Size, safeSize)
                && state.Alignment == alignment
                && state.Color == color
                && state.UseGradient == useGradient
                && state.ColorTopLeft == colors.TopLeft
                && state.ColorTopRight == colors.TopRight
                && state.ColorBottomRight == colors.BottomRight
                && state.ColorBottomLeft == colors.BottomLeft
                && state.OutlineEnabled == outlineEnabled
                && state.OutlineColor == outlineColor
                && Approximately(state.OutlineThickness, outlineThickness)
                && state.ShadowEnabled == shadowEnabled
                && state.ShadowColor == shadowColor
                && Approximately(state.ShadowOffset, shadowOffset)
                && Approximately(state.ShadowSoftness, shadowSoftness)
                && SdfTextRenderer.TouchScreenText(id, sortingOrder))
            {
                return;
            }

            Vector4 shadowVector = shadowEnabled ? ColorU32ToVector4(shadowColor) : default(Vector4);
            SdfTextRenderer.DrawScreenText(
                id,
                safeText,
                safeFontPath,
                fontSize,
                pos,
                safeSize,
                alignment,
                ColorU32ToVector4(color),
                outlineEnabled,
                ColorU32ToVector4(outlineColor),
                outlineThickness,
                sortingOrder,
                shadowEnabled,
                shadowVector,
                shadowOffset,
                shadowSoftness,
                useGradient,
                ColorU32ToVector4(colors.TopLeft),
                ColorU32ToVector4(colors.TopRight),
                ColorU32ToVector4(colors.BottomRight),
                ColorU32ToVector4(colors.BottomLeft));

            if (state == null)
            {
                state = new TextDrawState();
                _textDrawStates[id] = state;
            }

            state.Text = safeText;
            state.FontPath = safeFontPath;
            state.FontSize = fontSize;
            state.Position = pos;
            state.Size = safeSize;
            state.Alignment = alignment;
            state.Color = color;
            state.UseGradient = useGradient;
            state.ColorTopLeft = colors.TopLeft;
            state.ColorTopRight = colors.TopRight;
            state.ColorBottomRight = colors.BottomRight;
            state.ColorBottomLeft = colors.BottomLeft;
            state.OutlineEnabled = outlineEnabled;
            state.OutlineColor = outlineColor;
            state.OutlineThickness = outlineThickness;
            state.ShadowEnabled = shadowEnabled;
            state.ShadowColor = shadowColor;
            state.ShadowOffset = shadowOffset;
            state.ShadowSoftness = shadowSoftness;
            state.SortingOrder = sortingOrder;
        }

        public void RenderUI()
        {
            if (!ShouldRender())
            {
                KeyViewerUnityRenderer.HideAll();
                PauseVideoIfNeeded();
                return;
            }

            if (Main.Settings.KeyViewerConfigurations == null || Main.Settings.KeyViewerConfigurations.Count == 0)
            {
                KeyViewerUnityRenderer.HideAll();
                PauseVideoIfNeeded();
                return;
            }

            PrepareKvRuntimeBake();
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            bool drewAny = false;
            bool hasVideoThisFrame = false;
            bool isPlaying = Main.IsGamePlaying();
            bool editMode = FreeMakeEditor.IsOpen;

            KeyViewerUnityRenderer.BeginFrame();
            foreach (KVConfiguration config in Main.Settings.KeyViewerConfigurations)
            {
                if (config == null || !config.IsEnabled || config.Nodes == null || config.Nodes.Count == 0)
                    continue;
                if (!IsConfigVisible(config, isPlaying, editMode))
                    continue;

                float globalScale = config.Scale;
                float rounding = (float)Math.Floor(6 * globalScale);
                float borderThickness = config.BorderThickness;
                uint kpsColor = Vector4ToColor(config.ColorKps);
                uint totalColor = Vector4ToColor(config.ColorTotal);

                DrawKeyRain(config, center, globalScale);
                DrawBackgroundImages(config, config.Nodes, center, globalScale, ref hasVideoThisFrame);
                DrawNodes(config, config.Nodes, center, globalScale, rounding, borderThickness, kpsColor, totalColor);
                drewAny = true;
            }
            KeyViewerUnityRenderer.EndFrame();
            if (hasVideoThisFrame)
            {
                VideoTextureManager.EndFrame("KV");
            }
            else if (_hadVideoLastFrame)
            {
                VideoTextureManager.PauseAll("KV");
            }
            _hadVideoLastFrame = hasVideoThisFrame;

            if (!drewAny)
            {
                KeyViewerUnityRenderer.HideAll();
                PauseVideoIfNeeded();
            }
        }

        private void PauseVideoIfNeeded()
        {
            if (!_hadVideoLastFrame) return;
            VideoTextureManager.PauseAll("KV");
            _hadVideoLastFrame = false;
        }

        private static void BeginVideoFrameIfNeeded(ref bool hasVideoThisFrame)
        {
            if (hasVideoThisFrame) return;
            VideoTextureManager.BeginFrame("KV");
            hasVideoThisFrame = true;
        }

        private bool ShouldRender()
        {
            if (!Main.IsEnabled || Main.Settings == null || !Main.Settings.EnableKeyViewer) return false;
            if (KeyViewerManager.Instance == null) return false;

            bool onlyShowPlaying = Main.Settings.KeyViewerOnlyShowPlaying;
            bool isGamePlaying = Main.IsGamePlaying();
            bool isEditorOpen = FreeMakeEditor.IsOpen;

            if (onlyShowPlaying && !isGamePlaying && !isEditorOpen)
            {
                return false;
            }

            return true;
        }

        private static bool IsConfigVisible(KVConfiguration config, bool isPlaying, bool editMode)
        {
            if (config == null) return false;
            if (editMode) return true;
            if (!config.ShowInGame && isPlaying) return false;
            if (config.OnlyShowPlaying && !isPlaying) return false;
            return true;
        }

        public bool ShouldRenderOverlayNow()
        {
            if (!ShouldRender()) return false;
            var configs = Main.Settings.KeyViewerConfigurations;
            if (configs == null || configs.Count == 0) return false;
            bool isPlaying = Main.IsGamePlaying();
            bool editMode = FreeMakeEditor.IsOpen;
            for (int i = 0; i < configs.Count; i++)
            {
                KVConfiguration config = configs[i];
                if (config == null || !config.IsEnabled || config.Nodes == null || config.Nodes.Count == 0)
                    continue;
                if (!IsConfigVisible(config, isPlaying, editMode))
                    continue;
                return true;
            }
            return false;
        }

        private void DrawBackgroundImages(KVConfiguration config, System.Collections.Generic.List<KVNode> activeNodes, Vector2 center, float globalScale, ref bool hasVideoThisFrame)
        {
            foreach (var node in activeNodes)
            {
                if (node == null || (node.NodeType != 3 && node.NodeType != 4)) continue;

                bool pressed = false;
                KeyViewerManager.Instance.IsNodePressed.TryGetValue(node, out pressed);

                float alpha = node.UseCustomColor ? (pressed ? node.ColorBgPressed[3] : node.ColorBgNormal[3]) : node.Opacity;
                BakedKvNode baked = GetBakedNode(config, node, center, globalScale, 0f, config.BorderThickness);
                Vector2 pos = baked.BasePosition;
                Vector2 size = baked.BaseSize;
                NodeRenderIds ids = baked.Ids;
                if (ids == null) continue;

                if (node.NodeType == 3
                    && baked.ImageBaked
                    && Approximately(baked.ImageBakedAlpha, alpha)
                    && KeyViewerUnityRenderer.KeepImageAlive(ids.BackgroundImage, baked.GraphicSortingOrder))
                {
                    continue;
                }

                Texture texture = null;
                if (node.NodeType == 4)
                {
                    BeginVideoFrameIfNeeded(ref hasVideoThisFrame);
                    texture = VideoTextureManager.GetOrCreateVideoTexture(
                        "KV",
                        ids.BackgroundImage,
                        node.VideoPath,
                        true,
                        Mathf.CeilToInt(Mathf.Abs(size.x)),
                        Mathf.CeilToInt(Mathf.Abs(size.y)),
                        true);
                }
                else
                {
                    texture = TextureManager.GetOrCreateTexture2D(node.ImagePath, size.x, size.y);
                }
                if (texture == null) continue;

                KeyViewerUnityRenderer.DrawImage(ids.BackgroundImage, texture, pos, size, alpha, baked.CornerRadius, baked.GraphicSortingOrder);
                if (node.NodeType == 3)
                {
                    baked.ImageBaked = true;
                    baked.ImageBakedAlpha = alpha;
                }
            }
        }

        private void DrawNodes(KVConfiguration config, System.Collections.Generic.List<KVNode> activeNodes, Vector2 center, float globalScale, float rounding, float borderThickness, uint kpsColor, uint totalColor)
        {
            foreach (var node in activeNodes)
            {
                if (node == null || node.NodeType == 3 || node.NodeType == 4) continue;

                BakedKvNode baked = GetBakedNode(config, node, center, globalScale, rounding, borderThickness);
                uint bgNormal = baked.BackgroundNormal;
                uint bgPressed = baked.BackgroundPressed;
                uint borderNormal = baked.BorderNormal;
                uint borderPressed = baked.BorderPressed;
                uint textNormal = baked.TextNormal;
                uint textPressed = baked.TextPressed;
                bool keyOutlineEnabled = baked.KeyOutlineEnabled;
                bool countOutlineEnabled = baked.CountOutlineEnabled;
                uint keyOutlineColor = baked.KeyOutlineColor;
                uint countOutlineColor = baked.CountOutlineColor;
                float keyOutlineThickness = baked.KeyOutlineThickness;
                float countOutlineThickness = baked.CountOutlineThickness;
                bool keyShadowEnabled = baked.KeyShadowEnabled;
                bool countShadowEnabled = baked.CountShadowEnabled;
                uint keyShadowColor = baked.KeyShadowColor;
                uint countShadowColor = baked.CountShadowColor;
                Vector2 keyShadowOffset = baked.KeyShadowOffset;
                Vector2 countShadowOffset = baked.CountShadowOffset;
                float keyShadowSoftness = baked.KeyShadowSoftness;
                float countShadowSoftness = baked.CountShadowSoftness;
                bool hideCountText = baked.HideCountText;

                bool pressed = false;
                KeyViewerManager.Instance.IsNodePressed.TryGetValue(node, out pressed);
                KeyPressAnimationSettings animationSettings = baked.Animation;
                float animationProgress = node.NodeType == 0 ? GetKeyPressAnimationProgress(node, pressed, animationSettings) : 0f;
                float pressScale = node.NodeType == 0 && animationSettings.Enabled
                    ? Mathf.Lerp(1f, animationSettings.Scale, animationProgress)
                    : 1f;
                Vector2 animationOffset = node.NodeType == 0 && animationSettings.Enabled
                    ? new Vector2(animationSettings.OffsetX, animationSettings.OffsetY) * globalScale * animationProgress
                    : Vector2.zero;
                float finalScale = baked.FinalScale;
                float animatedFinalScale = finalScale * pressScale;
                float animatedGlobalScale = globalScale * pressScale;
                Vector2 basePos = baked.BasePosition;
                Vector2 baseSize = baked.BaseSize;
                Vector2 size = new Vector2(node.Width * animatedFinalScale, node.Height * animatedFinalScale);
                Vector2 pos = basePos + (baseSize - size) * 0.5f + animationOffset;
                NodeRenderIds ids = baked.Ids;
                if (ids == null) continue;
                int graphicSortingOrder = baked.GraphicSortingOrder;
                int textSortingOrder = baked.TextSortingOrder;
                float cornerRadius = baked.CornerRadius * pressScale;

                if (node.NodeType == 0)
                {
                    float colorProgress = animationSettings.Enabled && animationSettings.AffectColors ? animationProgress : (pressed ? 1f : 0f);
                    ColorCorners bgColors = LerpCorners(ColorCorners.Solid(bgNormal), ColorCorners.Solid(bgPressed), colorProgress);
                    ColorCorners borderColors = LerpCorners(ColorCorners.Solid(borderNormal), ColorCorners.Solid(borderPressed), colorProgress);
                    ColorCorners textColors = LerpCorners(ColorCorners.Solid(textNormal), ColorCorners.Solid(textPressed), colorProgress);
                    KeyViewerUnityRenderer.DrawRect(ids.Box, pos, size, bgColors.TopLeft, bgColors.TopRight, bgColors.BottomRight, bgColors.BottomLeft, borderColors.TopLeft, borderColors.TopRight, borderColors.BottomRight, borderColors.BottomLeft, baked.BorderThickness, cornerRadius, graphicSortingOrder);

                    string labelStr = baked.Label;

                    float keyFontSize = 20.0f * animatedGlobalScale * node.TextScale;
                    string keyFontPath = baked.KeyFontPath;
                    float keyTextHeight = TextBoxHeight(keyFontSize);
                    Vector2 keyPos = new Vector2(
                        pos.x + node.TextOffsetX * animatedFinalScale,
                        hideCountText
                            ? pos.y + (size.y - keyTextHeight) * 0.5f + node.TextOffsetY * animatedFinalScale
                            : pos.y + 5 * animatedFinalScale + node.TextOffsetY * animatedFinalScale);
                    bool keptKeyText = !animationSettings.Enabled
                        && baked.KeyTextBaked
                        && baked.KeyTextBakedColor == textColors.TopLeft
                        && SdfTextRenderer.KeepAlive(ids.KeyText, textSortingOrder);
                    if (!keptKeyText)
                    {
                        DrawText(ids.KeyText, labelStr, keyFontPath, keyFontSize, keyPos, new Vector2(size.x, keyTextHeight), 1, textColors, keyOutlineEnabled, keyOutlineColor, keyOutlineThickness, keyShadowEnabled, keyShadowColor, keyShadowOffset, keyShadowSoftness, textSortingOrder);
                        if (!animationSettings.Enabled)
                        {
                            baked.KeyTextBaked = true;
                            baked.KeyTextBakedColor = textColors.TopLeft;
                        }
                    }

                    if (!hideCountText)
                    {
                        if (node.CachedHitCountText == null || node.CachedHitCountValue != node.HitCount)
                        {
                            node.CachedHitCountValue = node.HitCount;
                            node.CachedHitCountText = node.HitCount.ToString();
                        }
                        string countStr = node.CachedHitCountText;
                        float countFontSize = 20.0f * animatedGlobalScale * node.CountScale;
                        string countFontPath = baked.CountFontPath;
                        float countTextHeight = TextBoxHeight(countFontSize);
                        Vector2 countPos = new Vector2(
                            pos.x + node.CountOffsetX * animatedFinalScale,
                            pos.y + size.y - countTextHeight - 5 * animatedFinalScale + node.CountOffsetY * animatedFinalScale);
                        bool keptCountText = !animationSettings.Enabled
                            && baked.CountTextBaked
                            && baked.CountTextBakedValue == node.HitCount
                            && baked.CountTextBakedColor == textColors.TopLeft
                            && SdfTextRenderer.KeepAlive(ids.CountText, textSortingOrder);
                        if (!keptCountText)
                        {
                            DrawText(ids.CountText, countStr, countFontPath, countFontSize, countPos, new Vector2(size.x, countTextHeight), ClampTextAlignment(node.CountTextAlignment, 1), textColors, countOutlineEnabled, countOutlineColor, countOutlineThickness, countShadowEnabled, countShadowColor, countShadowOffset, countShadowSoftness, textSortingOrder);
                            if (!animationSettings.Enabled)
                            {
                                baked.CountTextBaked = true;
                                baked.CountTextBakedValue = node.HitCount;
                                baked.CountTextBakedColor = textColors.TopLeft;
                            }
                        }
                    }
                }
                else if (node.NodeType == 1)
                {
                    ColorCorners bgColors = ColorCorners.Solid(bgNormal);
                    ColorCorners borderColors = ColorCorners.Solid(borderNormal);
                    KeyViewerUnityRenderer.DrawRect(ids.Box, pos, size, bgColors.TopLeft, bgColors.TopRight, bgColors.BottomRight, bgColors.BottomLeft, borderColors.TopLeft, borderColors.TopRight, borderColors.BottomRight, borderColors.BottomLeft, baked.BorderThickness, cornerRadius, graphicSortingOrder);

                    string label = baked.Label;
                    int kps = KeyViewerManager.Instance.GetCurrentKps(config);
                    if (config.CachedKpsText == null || config.CachedKpsValue != kps)
                    {
                        config.CachedKpsValue = kps;
                        config.CachedKpsText = kps.ToString();
                    }
                    string val = config.CachedKpsText;
                    // 节点勾选「独立颜色」时用节点自己的文本颜色（baked.TextNormal 已解析），
                    // 否则回退到 config 级的「底部统计文本」颜色，保持老配置外观不变。
                    uint kpsTextColor = node.UseCustomColor ? textNormal : kpsColor;
                    DrawPairText(config, node, pos, size, finalScale, globalScale, label, val, kpsTextColor, keyOutlineEnabled, keyOutlineColor, keyOutlineThickness, countOutlineEnabled, countOutlineColor, countOutlineThickness, keyShadowEnabled, keyShadowColor, keyShadowOffset, keyShadowSoftness, countShadowEnabled, countShadowColor, countShadowOffset, countShadowSoftness, ids.KpsLabel, ids.KpsValue, hideCountText, textSortingOrder, baked);
                }
                else if (node.NodeType == 2)
                {
                    ColorCorners bgColors = ColorCorners.Solid(bgNormal);
                    ColorCorners borderColors = ColorCorners.Solid(borderNormal);
                    KeyViewerUnityRenderer.DrawRect(ids.Box, pos, size, bgColors.TopLeft, bgColors.TopRight, bgColors.BottomRight, bgColors.BottomLeft, borderColors.TopLeft, borderColors.TopRight, borderColors.BottomRight, borderColors.BottomLeft, baked.BorderThickness, cornerRadius, graphicSortingOrder);

                    string label = baked.Label;
                    if (config.CachedTotalHitsText == null || config.CachedTotalHitsValue != config.TotalHits)
                    {
                        config.CachedTotalHitsValue = config.TotalHits;
                        config.CachedTotalHitsText = config.TotalHits.ToString();
                    }
                    string val = config.CachedTotalHitsText;
                    uint totalTextColor = node.UseCustomColor ? textNormal : totalColor;
                    DrawPairText(config, node, pos, size, finalScale, globalScale, label, val, totalTextColor, keyOutlineEnabled, keyOutlineColor, keyOutlineThickness, countOutlineEnabled, countOutlineColor, countOutlineThickness, keyShadowEnabled, keyShadowColor, keyShadowOffset, keyShadowSoftness, countShadowEnabled, countShadowColor, countShadowOffset, countShadowSoftness, ids.TotalLabel, ids.TotalValue, hideCountText, textSortingOrder, baked);
                }
            }
        }

        private void DrawPairText(KVConfiguration config, KVNode node, Vector2 pos, Vector2 size, float finalScale, float globalScale, string label, string value, uint color, bool keyOutlineEnabled, uint keyOutlineColor, float keyOutlineThickness, bool countOutlineEnabled, uint countOutlineColor, float countOutlineThickness, bool keyShadowEnabled, uint keyShadowColor, Vector2 keyShadowOffset, float keyShadowSoftness, bool countShadowEnabled, uint countShadowColor, Vector2 countShadowOffset, float countShadowSoftness, string labelId, string valueId, bool hideValue, int sortingOrder, BakedKvNode baked)
        {
            float keyFontSize = 20.0f * globalScale * node.TextScale;
            string keyFontPath = baked.KeyFontPath;
            float labelHeight = TextBoxHeight(keyFontSize);
            if (hideValue)
            {
                Vector2 centeredLabelPos = new Vector2(
                    pos.x + node.TextOffsetX * finalScale,
                    pos.y + (size.y - labelHeight) * 0.5f + node.TextOffsetY * finalScale);
                if (!baked.PairLabelBaked || !SdfTextRenderer.KeepAlive(labelId, sortingOrder))
                {
                    DrawText(labelId, label, keyFontPath, keyFontSize, centeredLabelPos, new Vector2(size.x, labelHeight), 1, color, keyOutlineEnabled, keyOutlineColor, keyOutlineThickness, keyShadowEnabled, keyShadowColor, keyShadowOffset, keyShadowSoftness, sortingOrder);
                    baked.PairLabelBaked = true;
                }
                return;
            }

            Vector2 labelPos = new Vector2(
                pos.x + node.TextOffsetX * finalScale,
                pos.y + 5 * finalScale + node.TextOffsetY * finalScale);
            if (!baked.PairLabelBaked || !SdfTextRenderer.KeepAlive(labelId, sortingOrder))
            {
                DrawText(labelId, label, keyFontPath, keyFontSize, labelPos, new Vector2(size.x, labelHeight), 1, color, keyOutlineEnabled, keyOutlineColor, keyOutlineThickness, keyShadowEnabled, keyShadowColor, keyShadowOffset, keyShadowSoftness, sortingOrder);
                baked.PairLabelBaked = true;
            }

            float countFontSize = 20.0f * globalScale * node.CountScale;
            string countFontPath = baked.CountFontPath;
            float valueHeight = TextBoxHeight(countFontSize);
            Vector2 valuePos = new Vector2(
                pos.x + node.CountOffsetX * finalScale,
                pos.y + size.y - valueHeight - 5 * finalScale + node.CountOffsetY * finalScale);
            DrawText(valueId, value, countFontPath, countFontSize, valuePos, new Vector2(size.x, valueHeight), ClampTextAlignment(node.CountTextAlignment, 1), color, countOutlineEnabled, countOutlineColor, countOutlineThickness, countShadowEnabled, countShadowColor, countShadowOffset, countShadowSoftness, sortingOrder);
        }

        private static int ClampTextAlignment(int alignment, int fallback)
        {
            return alignment >= 0 && alignment <= 2 ? alignment : fallback;
        }

        private void DrawKeyRain(KVConfiguration config, Vector2 center, float globalScale)
        {
            if (config == null || KeyViewerManager.Instance.ActiveDrops.Count <= 0) return;

            float speed = config.KeyRainSpeed;
            float maxHeight = config.KeyRainMaxHeight;
            int fadeMode = config.KeyRainFadeMode;
            float currentTime = RenderTimelineClock.Time;

            _rainRow1Buffer.Clear();
            _rainRow2Buffer.Clear();
            var activeDrops = KeyViewerManager.Instance.ActiveDrops;
            for (int i = 0; i < activeDrops.Count; i++)
            {
                KeyDrop candidate = activeDrops[i];
                KVNode candidateNode = candidate.Node;
                if (candidateNode == null || candidateNode.NodeType != 0) continue;
                if (candidateNode.UseCustomRain ? !candidateNode.EnableKeyRain : !config.EnableKeyRain) continue;
                if (candidate.Config != null)
                {
                    if (!ReferenceEquals(candidate.Config, config)) continue;
                }
                else if (config.Nodes == null || !config.Nodes.Contains(candidateNode))
                {
                    continue;
                }

                if (candidateNode.RainRow == 1) _rainRow1Buffer.Add(candidate);
                else _rainRow2Buffer.Add(candidate);
            }

            // Row 1 drops must be emitted before row 2 so their triangles keep the
            // same in-batch draw order as the old two-pass loop.
            for (int pass = 0; pass < 2; pass++)
            {
                var drops = pass == 0 ? _rainRow1Buffer : _rainRow2Buffer;
                for (int i = 0; i < drops.Count; i++)
                {
                    KeyDrop drop = drops[i];
                    KVNode node = drop.Node;
                    BakedKvNode baked = GetBakedNode(config, node, center, globalScale,
                        (float)Math.Floor(6 * globalScale), config.BorderThickness);
                    if (!baked.RainEnabled) continue;

                    float ratio = baked.RainWidthRatio;
                    uint baseColor = baked.RainBaseColor;
                    uint farColor = baked.RainFarColor;
                    bool gradientEnabled = baked.RainGradientEnabled;
                    bool horizontalGradientEnabled = baked.RainHorizontalGradientEnabled;
                    int gradientMode = baked.RainGradientMode;
                    uint horizontalColor = baked.RainHorizontalColor;
                    float fadeHeight = baked.RainFadeHeight;
                    float fadePower = baked.RainFadePower;
                    float gradientHeight = baked.RainGradientHeight;
                    float gradientPower = baked.RainGradientPower;

                    float boxW = baked.BaseSize.x;
                    float keyX = baked.BasePosition.x;
                    float keyY = baked.BasePosition.y - baked.RainYOffset;
                    float dropW = boxW * ratio;
                    float dropX = keyX + (boxW - dropW) * 0.5f;
                    float endTime = drop.EndTime ?? currentTime;
                    float dropBottomY = keyY - speed * (currentTime - endTime);
                    float dropTopY = keyY - speed * (currentTime - drop.StartTime);

                    if (dropBottomY < keyY - maxHeight && fadeMode == 0) continue;

                    float clampedBottomY = Math.Min(dropBottomY, keyY);
                    float clampedTopY = Math.Max(dropTopY, keyY - maxHeight);
                    if (clampedBottomY <= clampedTopY) continue;

                    uint topColor;
                    uint bottomColor;
                    if (gradientEnabled && gradientMode == 1)
                    {
                        float gradientDistance = Mathf.Max(1f, maxHeight * Mathf.Clamp(gradientHeight, 0.05f, 3f));
                        float curvePower = Mathf.Clamp(gradientPower, 0.1f, 5f);
                        float topHeightRatio = Mathf.Pow(Mathf.Clamp01((keyY - clampedTopY) / gradientDistance), curvePower);
                        float bottomHeightRatio = Mathf.Pow(Mathf.Clamp01((keyY - clampedBottomY) / gradientDistance), curvePower);
                        topColor = LerpColor(baseColor, farColor, topHeightRatio);
                        bottomColor = LerpColor(baseColor, farColor, bottomHeightRatio);
                    }
                    else
                    {
                        topColor = gradientEnabled ? farColor : baseColor;
                        bottomColor = baseColor;
                    }
                    if (fadeMode == 1)
                    {
                        float fadeDistance = Mathf.Max(1f, maxHeight * Mathf.Clamp(fadeHeight, 0.05f, 3f));
                        float curvePower = Mathf.Clamp(fadePower, 0.1f, 5f);
                        float bottomAlphaRatio = Mathf.Pow(1.0f - Mathf.Clamp01((keyY - clampedBottomY) / fadeDistance), curvePower);
                        float topAlphaRatio = Mathf.Pow(1.0f - Mathf.Clamp01((keyY - clampedTopY) / fadeDistance), curvePower);
                        bottomColor = MultiplyAlpha(bottomColor, bottomAlphaRatio);
                        topColor = MultiplyAlpha(topColor, topAlphaRatio);
                    }

                    float snapLeft = Mathf.Round(dropX);
                    float snapRight = Mathf.Round(dropX + dropW);
                    float snapTop = Mathf.Round(clampedTopY);
                    float snapBottom = Mathf.Round(clampedBottomY);
                    if (snapRight <= snapLeft || snapBottom <= snapTop) continue;

                    bool useHeightCurveFill =
                        (fadeMode == 1 && (Mathf.Abs(fadeHeight - 1f) > 0.0001f || Mathf.Abs(fadePower - 1f) > 0.0001f))
                        || (gradientEnabled && gradientMode == 1 && (Mathf.Abs(gradientHeight - 1f) > 0.0001f || Mathf.Abs(gradientPower - 1f) > 0.0001f));
                    if (baked.RainShadowEnabled)
                    {
                        uint shadowTopColor = MultiplyAlpha(baked.RainShadowColor, baked.RainShadowStrength * Alpha01(topColor));
                        uint shadowBottomColor = MultiplyAlpha(baked.RainShadowColor, baked.RainShadowStrength * Alpha01(bottomColor));
                        DrawKeyRainShadow(
                            new Vector2(snapLeft, snapTop),
                            new Vector2(snapRight - snapLeft, snapBottom - snapTop),
                            shadowTopColor,
                            shadowBottomColor,
                            baked.RainShadowOffset,
                            baked.RainShadowSoftness,
                            baked.RainShadowSortingOrder);
                    }
                    if (useHeightCurveFill)
                    {
                        KeyViewerUnityRenderer.DrawKeyRainCurveRect(
                            "rain",
                            new Vector2(snapLeft, snapTop),
                            new Vector2(snapRight - snapLeft, snapBottom - snapTop),
                            baseColor,
                            farColor,
                            gradientEnabled,
                            gradientMode == 1,
                            fadeMode,
                            keyY,
                            maxHeight,
                            fadeHeight,
                            fadePower,
                            gradientHeight,
                            gradientPower,
                            horizontalGradientEnabled,
                            horizontalColor,
                            baked.RainCornerRadius,
                            baked.RainSortingOrder);
                    }
                    else
                    {
                        uint topRightColor = horizontalGradientEnabled ? MatchAlpha(horizontalColor, topColor) : topColor;
                        uint bottomRightColor = horizontalGradientEnabled
                            ? (gradientEnabled ? LerpColor(bottomColor, MatchAlpha(horizontalColor, bottomColor), 0.5f) : MatchAlpha(horizontalColor, bottomColor))
                            : bottomColor;
                        KeyViewerUnityRenderer.DrawGradientRect(
                            "rain",
                            new Vector2(snapLeft, snapTop),
                            new Vector2(snapRight - snapLeft, snapBottom - snapTop),
                            topColor,
                            topRightColor,
                            bottomRightColor,
                            bottomColor,
                            baked.RainCornerRadius,
                            baked.RainSortingOrder);
                    }
                }
            }
        }

        private static Vector2 ResolvePair(float[] value, float fallbackX, float fallbackY)
        {
            return new Vector2(
                value != null && value.Length > 0 ? value[0] : fallbackX,
                value != null && value.Length > 1 ? value[1] : fallbackY);
        }

        private void DrawKeyRainShadow(Vector2 topLeft, Vector2 size, uint topColor, uint bottomColor, Vector2 offset, float softness, int sortingOrder)
        {
            if (Alpha01(topColor) <= 0f && Alpha01(bottomColor) <= 0f) return;
            KeyViewerUnityRenderer.DrawSoftGradientShadowRect(
                "rain_shadow",
                topLeft + offset,
                size,
                topColor,
                bottomColor,
                softness,
                sortingOrder);
        }
    }
}
