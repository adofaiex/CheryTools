using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace CheryTools
{
    internal static class SdfTextRenderer
    {
        public struct TextBounds
        {
            public float Left;
            public float Top;
            public float Width;
            public float Height;
        }

        private const int CanvasSortingOrder = RenderDepth.DefaultTextSortingOrder;
        private const int CanvasSortingOrderBehindImGui = RenderDepth.DefaultTextSortingOrder;
        private const int DefaultAtlasSize = 1024;
        private const int DefaultSamplingSize = 72;
        private const int DefaultAtlasPadding = 9;

        private static GameObject _root;
        private static Canvas _canvas;
        private static CanvasScaler _scaler;
        private static RectTransform _rootRect;
        private static readonly Dictionary<int, Canvas> _layerCanvases = new Dictionary<int, Canvas>();
        private static readonly Dictionary<int, RectTransform> _layerRects = new Dictionary<int, RectTransform>();
        private static readonly Dictionary<string, TMP_FontAsset> _fontAssets = new Dictionary<string, TMP_FontAsset>();
        private static readonly Dictionary<string, string> _normalizedFontPathCache = new Dictionary<string, string>();
        private static readonly Dictionary<string, TextMeshProUGUI> _texts = new Dictionary<string, TextMeshProUGUI>();
        private static readonly Dictionary<string, int> _textSortingOrders = new Dictionary<string, int>();
        private static readonly Dictionary<TextMeshProUGUI, Material> _materials = new Dictionary<TextMeshProUGUI, Material>();
        private static readonly Dictionary<TextMeshProUGUI, TMP_FontAsset> _materialFonts = new Dictionary<TextMeshProUGUI, TMP_FontAsset>();
        private static readonly Dictionary<TextMeshProUGUI, TextObjectState> _textStates = new Dictionary<TextMeshProUGUI, TextObjectState>();
        private static readonly Dictionary<string, Vector2> _measureCache = new Dictionary<string, Vector2>();
        private static readonly int OutlineSoftnessProperty = Shader.PropertyToID("_OutlineSoftness");
        private static readonly int UnderlayColorProperty = Shader.PropertyToID("_UnderlayColor");
        private static readonly int UnderlayOffsetXProperty = Shader.PropertyToID("_UnderlayOffsetX");
        private static readonly int UnderlayOffsetYProperty = Shader.PropertyToID("_UnderlayOffsetY");
        private static readonly int UnderlayDilateProperty = Shader.PropertyToID("_UnderlayDilate");
        private static readonly int UnderlaySoftnessProperty = Shader.PropertyToID("_UnderlaySoftness");
        private static TextMeshProUGUI _measureText;
        private static int _frameId;
        private static bool _isReady;
        private static bool _loggedInitError;
        private static readonly Dictionary<TextMeshProUGUI, int> _frameMarks = new Dictionary<TextMeshProUGUI, int>();

        private struct TextObjectState
        {
            public TMP_FontAsset Font;
            public string Text;
            public float FontSize;
            public float Width;
            public float Height;
            public float X;
            public float Y;
            public float PivotX;
            public float PivotY;
            public float ScaleX;
            public float ScaleY;
            public int Alignment;
            public Vector4 Color;
            public bool OutlineEnabled;
            public Vector4 OutlineColor;
            public float OutlineThickness;
            public float OutlineSoftness;
            public bool ShadowEnabled;
            public Vector4 ShadowColor;
            public float ShadowOffsetX;
            public float ShadowOffsetY;
            public float ShadowSoftness;
            public float CharacterSpacing;
            public float LineSpacing;
        }

        public static void BeginFrame()
        {
            _frameId++;
            UpdateCanvasSortingOrder();
        }

        public static void EndFrame()
        {
            UpdateCanvasSortingOrder();

            foreach (var pair in _texts)
            {
                if (pair.Value == null) continue;
                if (pair.Value.gameObject.activeSelf && (!_frameMarks.TryGetValue(pair.Value, out int mark) || mark != _frameId))
                {
                    pair.Value.gameObject.SetActive(false);
                }
            }
        }

        public static void Shutdown()
        {
            foreach (var pair in _materials)
            {
                if (pair.Value != null) UnityEngine.Object.Destroy(pair.Value);
            }
            _materials.Clear();
            _materialFonts.Clear();
            _textStates.Clear();
            _measureCache.Clear();
            _texts.Clear();
            _textSortingOrders.Clear();
            _layerCanvases.Clear();
            _layerRects.Clear();
            _fontAssets.Clear();
            _normalizedFontPathCache.Clear();
            _measureText = null;

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }

            _canvas = null;
            _scaler = null;
            _rootRect = null;
            _isReady = false;
            _loggedInitError = false;
        }

        public static TextBounds DrawOverlayerText(string id, OverlayerText ovText, string text, float offsetX, float offsetY, float scaleX, float scaleY, float minWidth = 0f, float minHeight = 0f, bool useFixedSize = false, int sortingOrder = CanvasSortingOrder)
        {
            float fontSize = Math.Max(1f, ovText.FontSize);
            Vector2 measured = useFixedSize
                ? new Vector2(Math.Max(1f, minWidth), Math.Max(1f, minHeight))
                : MeasureText(ovText.FontPath, text, fontSize, ovText.LetterSpacing, ovText.LineHeightOffset);
            float drawWidth = Math.Max(Math.Max(1f, measured.x), minWidth);
            float drawHeight = Math.Max(Math.Max(1f, measured.y), minHeight);

            float pivotX = Mathf.Clamp01(ovText.PivotX);
            float pivotY = Mathf.Clamp01(ovText.PivotY);
            float pivotedX = RoundPixel(ovText.PositionX + offsetX);
            float pivotedY = RoundPixel(ovText.PositionY + offsetY);
            float safeScaleX = Math.Max(0.001f, scaleX);
            float safeScaleY = Math.Max(0.001f, scaleY);

            bool materialShadowEnabled = ovText.EnableShadow;
            Vector4 materialShadow = Vector4.zero;
            Vector2 materialShadowOffset = Vector2.zero;
            float materialShadowSoftness = 0f;
            if (materialShadowEnabled)
            {
                materialShadow = ColorArrayToVector4(ovText.ShadowColor, new Vector4(0f, 0f, 0f, 1f));
                float shadowScale = fontSize / 32f;
                materialShadowOffset = new Vector2(
                    (ovText.ShadowOffset != null && ovText.ShadowOffset.Length > 0 ? ovText.ShadowOffset[0] : 0f) * shadowScale,
                    (ovText.ShadowOffset != null && ovText.ShadowOffset.Length > 1 ? ovText.ShadowOffset[1] : 0f) * shadowScale);
                materialShadowSoftness = Mathf.Max(0f, ovText.ShadowSoftness) * shadowScale;
                materialShadowEnabled = materialShadow.w > 0f;
            }

            DrawText(
                id,
                text,
                ovText.FontPath,
                fontSize,
                drawWidth,
                drawHeight,
                pivotedX,
                pivotedY,
                pivotX,
                pivotY,
                safeScaleX,
                safeScaleY,
                ovText.Alignment,
                ColorArrayToVector4(ovText.TextColor, new Vector4(1f, 1f, 1f, 1f)),
                ovText.EnableOutline,
                ColorArrayToVector4(ovText.OutlineColor, new Vector4(0f, 0f, 0f, 1f)),
                ovText.OutlineThickness,
                ovText.LetterSpacing,
                ovText.LineHeightOffset,
                0f,
                sortingOrder,
                materialShadowEnabled,
                materialShadow,
                materialShadowOffset,
                materialShadowSoftness);

            return new TextBounds
            {
                Left = pivotedX - pivotX * drawWidth * safeScaleX,
                Top = pivotedY - pivotY * drawHeight * safeScaleY,
                Width = drawWidth * safeScaleX,
                Height = drawHeight * safeScaleY
            };
        }

        public static TextBounds DrawScreenText(string id, string text, string fontPath, float fontSize, Vector2 topLeft, Vector2 size, int alignment, Vector4 color, bool outlineEnabled, Vector4 outlineColor, float outlineThickness, int sortingOrder = CanvasSortingOrder, bool shadowEnabled = false, Vector4 shadowColor = default(Vector4), Vector2 shadowOffset = default(Vector2), float shadowSoftness = 0f)
        {
            Vector2 safeSize = new Vector2(Math.Max(1f, size.x), Math.Max(1f, size.y));
            Vector2 pos = new Vector2(RoundPixel(topLeft.x), RoundPixel(topLeft.y));
            bool materialShadowEnabled = shadowEnabled && shadowColor.w > 0f;
            Vector2 materialShadowOffset = Vector2.zero;
            float materialShadowSoftness = 0f;
            if (materialShadowEnabled)
            {
                float shadowScale = Math.Max(1f, fontSize) / 32f;
                materialShadowOffset = shadowOffset * shadowScale;
                materialShadowSoftness = Mathf.Max(0f, shadowSoftness) * shadowScale;
            }
            DrawText(id, text, fontPath, fontSize, safeSize.x, safeSize.y, pos.x, pos.y, 0f, 0f, 1f, 1f, alignment, color, outlineEnabled, outlineColor, outlineThickness, 0f, 0f, 0f, sortingOrder, materialShadowEnabled, shadowColor, materialShadowOffset, materialShadowSoftness);
            return new TextBounds
            {
                Left = pos.x,
                Top = pos.y,
                Width = safeSize.x,
                Height = safeSize.y
            };
        }

        public static bool TouchScreenText(string id, int sortingOrder = CanvasSortingOrder)
        {
            if (!EnsureReady()) return false;

            if (!_texts.TryGetValue(id, out TextMeshProUGUI tmp) || tmp == null)
            {
                return false;
            }

            if (!_textSortingOrders.TryGetValue(id, out int currentOrder) || currentOrder != sortingOrder)
            {
                return false;
            }

            if (!tmp.gameObject.activeSelf)
            {
                tmp.gameObject.SetActive(true);
            }

            _frameMarks[tmp] = _frameId;
            return true;
        }

        public static Vector2 MeasureText(string fontPath, string text, float fontSize, float characterSpacing, float lineSpacing)
        {
            if (!EnsureReady()) return Vector2.zero;
            string normalizedPath = NormalizeFontPath(fontPath);
            string safeText = text ?? string.Empty;
            string cacheKey = string.Join("|",
                normalizedPath,
                safeText,
                fontSize.ToString("R", CultureInfo.InvariantCulture),
                characterSpacing.ToString("R", CultureInfo.InvariantCulture),
                lineSpacing.ToString("R", CultureInfo.InvariantCulture));
            if (_measureCache.TryGetValue(cacheKey, out Vector2 cached))
            {
                return cached;
            }

            TMP_FontAsset fontAsset = GetFontAsset(normalizedPath);
            if (fontAsset == null) return Vector2.zero;

            if (_measureText == null)
            {
                _measureText = CreateTextObject("__measure", CanvasSortingOrder);
                _measureText.gameObject.SetActive(false);
            }

            _measureText.font = fontAsset;
            _measureText.fontSize = fontSize;
            _measureText.richText = true;
            _measureText.textWrappingMode = TextWrappingModes.NoWrap;
            _measureText.overflowMode = TextOverflowModes.Overflow;
            _measureText.characterSpacing = characterSpacing;
            _measureText.lineSpacing = lineSpacing;
            Vector2 measured = _measureText.GetPreferredValues(safeText, float.PositiveInfinity, float.PositiveInfinity);
            if (_measureCache.Count > 1024)
            {
                _measureCache.Clear();
            }
            _measureCache[cacheKey] = measured;
            return measured;
        }

        private static void DrawText(
            string id,
            string text,
            string fontPath,
            float fontSize,
            float width,
            float height,
            float x,
            float y,
            float pivotX,
            float pivotY,
            float scaleX,
            float scaleY,
            int alignment,
            Vector4 color,
            bool outlineEnabled,
            Vector4 outlineColor,
            float outlineThickness,
            float characterSpacing,
            float lineSpacing,
            float outlineSoftness = 0f,
            int sortingOrder = CanvasSortingOrder,
            bool shadowEnabled = false,
            Vector4 shadowColor = default(Vector4),
            Vector2 shadowOffset = default(Vector2),
            float shadowSoftness = 0f)
        {
            float outlinePixels = ClampOutlineThickness(outlineThickness);

            DrawTextObject(id, text, fontPath, fontSize, width, height, x, y, pivotX, pivotY, scaleX, scaleY, alignment, color, outlineEnabled, outlineColor, outlinePixels, characterSpacing, lineSpacing, outlineSoftness, sortingOrder, shadowEnabled, shadowColor, shadowOffset, shadowSoftness);
        }

        private static void DrawTextObject(
            string id,
            string text,
            string fontPath,
            float fontSize,
            float width,
            float height,
            float x,
            float y,
            float pivotX,
            float pivotY,
            float scaleX,
            float scaleY,
            int alignment,
            Vector4 color,
            bool outlineEnabled,
            Vector4 outlineColor,
            float outlineThickness,
            float characterSpacing,
            float lineSpacing,
            float outlineSoftness,
            int sortingOrder,
            bool shadowEnabled,
            Vector4 shadowColor,
            Vector2 shadowOffset,
            float shadowSoftness)
        {
            if (!EnsureReady()) return;
            TMP_FontAsset fontAsset = GetFontAsset(fontPath);
            if (fontAsset == null) return;

            TextMeshProUGUI tmp = GetTextObject(id, sortingOrder);
            if (tmp == null) return;

            if (!tmp.gameObject.activeSelf)
            {
                tmp.gameObject.SetActive(true);
            }

            string safeText = text ?? string.Empty;
            float safeFontSize = Math.Max(1f, fontSize);
            TextAlignmentOptions tmpAlignment = ToTmpAlignment(alignment);
            Color tmpColor = ToColor(color);
            Vector2 roundedPosition = new Vector2(RoundPixel(x), -RoundPixel(y));
            Vector2 safeSize = new Vector2(Math.Max(1f, width), Math.Max(1f, height));
            Vector2 safePivot = new Vector2(pivotX, 1f - pivotY);
            Vector3 safeScale = new Vector3(scaleX, scaleY, 1f);
            bool effectiveShadowEnabled = shadowEnabled && shadowColor.w > 0f;
            Vector4 effectiveShadowColor = effectiveShadowEnabled ? shadowColor : Vector4.zero;
            Vector2 effectiveShadowOffset = effectiveShadowEnabled ? shadowOffset : Vector2.zero;
            float effectiveShadowSoftness = effectiveShadowEnabled ? Mathf.Max(0f, shadowSoftness) : 0f;

            bool hasState = _textStates.TryGetValue(tmp, out TextObjectState lastState);
            bool contentChanged = !hasState
                || lastState.Font != fontAsset
                || !string.Equals(lastState.Text, safeText, StringComparison.Ordinal)
                || !Approximately(lastState.FontSize, safeFontSize)
                || !Approximately(lastState.CharacterSpacing, characterSpacing)
                || !Approximately(lastState.LineSpacing, lineSpacing)
                || lastState.Alignment != alignment
                || !Approximately(lastState.Color, color);
            bool outlineChanged = !hasState
                || lastState.Font != fontAsset
                || lastState.OutlineEnabled != outlineEnabled
                || !Approximately(lastState.OutlineColor, outlineColor)
                || !Approximately(lastState.OutlineThickness, outlineThickness)
                || !Approximately(lastState.OutlineSoftness, outlineSoftness)
                || lastState.ShadowEnabled != effectiveShadowEnabled
                || !Approximately(lastState.ShadowColor, effectiveShadowColor)
                || !Approximately(lastState.ShadowOffsetX, effectiveShadowOffset.x)
                || !Approximately(lastState.ShadowOffsetY, effectiveShadowOffset.y)
                || !Approximately(lastState.ShadowSoftness, effectiveShadowSoftness);
            bool transformChanged = !hasState
                || !Approximately(lastState.X, roundedPosition.x)
                || !Approximately(lastState.Y, roundedPosition.y)
                || !Approximately(lastState.Width, safeSize.x)
                || !Approximately(lastState.Height, safeSize.y)
                || !Approximately(lastState.PivotX, safePivot.x)
                || !Approximately(lastState.PivotY, safePivot.y)
                || !Approximately(lastState.ScaleX, safeScale.x)
                || !Approximately(lastState.ScaleY, safeScale.y);

            if (contentChanged)
            {
                tmp.font = fontAsset;
                tmp.text = safeText;
                tmp.richText = true;
                tmp.fontSize = safeFontSize;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.characterSpacing = characterSpacing;
                tmp.lineSpacing = lineSpacing;
                tmp.alignment = tmpAlignment;
                tmp.color = tmpColor;
                tmp.raycastTarget = false;
            }
            if (outlineChanged)
            {
                ApplyOutline(tmp, outlineEnabled, outlineColor, outlineThickness, outlineSoftness, effectiveShadowEnabled, effectiveShadowColor, effectiveShadowOffset, effectiveShadowSoftness);
            }

            RectTransform rt = tmp.rectTransform;
            if (transformChanged)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = safePivot;
                rt.anchoredPosition = roundedPosition;
                rt.sizeDelta = safeSize;
                rt.localScale = safeScale;
            }

            _textStates[tmp] = new TextObjectState
            {
                Font = fontAsset,
                Text = safeText,
                FontSize = safeFontSize,
                Width = safeSize.x,
                Height = safeSize.y,
                X = roundedPosition.x,
                Y = roundedPosition.y,
                PivotX = safePivot.x,
                PivotY = safePivot.y,
                ScaleX = safeScale.x,
                ScaleY = safeScale.y,
                Alignment = alignment,
                Color = color,
                OutlineEnabled = outlineEnabled,
                OutlineColor = outlineColor,
                OutlineThickness = outlineThickness,
                OutlineSoftness = outlineSoftness,
                ShadowEnabled = effectiveShadowEnabled,
                ShadowColor = effectiveShadowColor,
                ShadowOffsetX = effectiveShadowOffset.x,
                ShadowOffsetY = effectiveShadowOffset.y,
                ShadowSoftness = effectiveShadowSoftness,
                CharacterSpacing = characterSpacing,
                LineSpacing = lineSpacing
            };
            _frameMarks[tmp] = _frameId;
        }

        private static TextMeshProUGUI GetTextObject(string id, int sortingOrder)
        {
            RectTransform layerRoot = GetLayerRoot(sortingOrder);
            if (layerRoot == null) return null;

            if (_texts.TryGetValue(id, out var tmp) && tmp != null)
            {
                if (!_textSortingOrders.TryGetValue(id, out int currentOrder)
                    || currentOrder != sortingOrder
                    || tmp.transform.parent != layerRoot)
                {
                    tmp.transform.SetParent(layerRoot, false);
                    _textSortingOrders[id] = sortingOrder;
                }
                return tmp;
            }

            tmp = CreateTextObject(id, sortingOrder);
            _texts[id] = tmp;
            _textSortingOrders[id] = sortingOrder;
            return tmp;
        }

        private static TextMeshProUGUI CreateTextObject(string id, int sortingOrder)
        {
            if (!EnsureReady()) return null;
            RectTransform layerRoot = GetLayerRoot(sortingOrder);
            if (layerRoot == null) return null;

            GameObject go = new GameObject("SDF_Text_" + id, typeof(RectTransform));
            go.transform.SetParent(layerRoot, false);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.raycastTarget = false;
            tmp.maskable = false;
            tmp.margin = Vector4.zero;
            return tmp;
        }

        private static bool EnsureReady()
        {
            if (_isReady && _root != null && _rootRect != null) return true;

            try
            {
                _root = new GameObject("CheryTools_SDF_Text_Root", typeof(RectTransform));
                UnityEngine.Object.DontDestroyOnLoad(_root);

                _rootRect = _root.GetComponent<RectTransform>();
                _rootRect.anchorMin = Vector2.zero;
                _rootRect.anchorMax = Vector2.one;
                _rootRect.offsetMin = Vector2.zero;
                _rootRect.offsetMax = Vector2.zero;
                _rootRect.pivot = new Vector2(0.5f, 0.5f);

                RectTransform defaultLayer = CreateLayerRoot(CanvasSortingOrder);
                if (defaultLayer == null)
                {
                    _isReady = false;
                    return false;
                }

                _canvas = _layerCanvases[CanvasSortingOrder];
                _scaler = _canvas.GetComponent<CanvasScaler>();
                UpdateCanvasSortingOrder();

                _isReady = true;
                return true;
            }
            catch (Exception ex)
            {
                if (!_loggedInitError && Main.Logger != null)
                {
                    Main.Logger.Log("[CheryTools] SDF text renderer init failed: " + ex.Message);
                    _loggedInitError = true;
                }
                _isReady = false;
                return false;
            }
        }

        private static RectTransform GetLayerRoot(int sortingOrder)
        {
            if (!EnsureReady()) return null;
            if (_layerRects.TryGetValue(sortingOrder, out RectTransform existing) && existing != null)
            {
                return existing;
            }

            return CreateLayerRoot(sortingOrder);
        }

        private static RectTransform CreateLayerRoot(int sortingOrder)
        {
            if (_rootRect == null) return null;
            if (_layerRects.TryGetValue(sortingOrder, out RectTransform existing) && existing != null)
            {
                return existing;
            }

            GameObject go = new GameObject("SDF_Text_Layer_" + sortingOrder.ToString(), typeof(RectTransform));
            go.transform.SetParent(_rootRect, false);

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1.0f;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            _layerCanvases[sortingOrder] = canvas;
            _layerRects[sortingOrder] = rt;
            return rt;
        }

        private static void UpdateCanvasSortingOrder()
        {
            if (_canvas == null) return;

            int targetOrder = CheryToolsMenu.IsMenuOpen ? CanvasSortingOrderBehindImGui : CanvasSortingOrder;
            if (_canvas.sortingOrder != targetOrder)
            {
                _canvas.sortingOrder = targetOrder;
            }
        }

        private static TMP_FontAsset GetFontAsset(string fontPath)
        {
            string path = NormalizeFontPath(fontPath);
            if (_fontAssets.TryGetValue(path, out var asset) && asset != null) return asset;

            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                return null;
            }

            try
            {
                Font font = new Font(path);
                asset = TMP_FontAsset.CreateFontAsset(
                    font,
                    DefaultSamplingSize,
                    DefaultAtlasPadding,
                    GlyphRenderMode.SDFAA,
                    DefaultAtlasSize,
                    DefaultAtlasSize,
                    AtlasPopulationMode.Dynamic,
                    true);

                if (asset != null)
                {
                    asset.name = "CheryTools_SDF_" + System.IO.Path.GetFileNameWithoutExtension(path);
                    asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                    asset.isMultiAtlasTexturesEnabled = true;
                    _fontAssets[path] = asset;
                }
            }
            catch (Exception ex)
            {
                if (Main.Logger != null)
                    Main.Logger.Log("[CheryTools] Failed to create SDF font asset " + path + ": " + ex.Message);
                asset = null;
            }

            return asset;
        }

        private static string NormalizeFontPath(string fontPath)
        {
            string rawPath = fontPath ?? string.Empty;
            if (_normalizedFontPathCache.TryGetValue(rawPath, out string cachedPath))
            {
                return cachedPath;
            }

            if (_normalizedFontPathCache.Count > 256)
            {
                _normalizedFontPathCache.Clear();
            }

            string normalizedPath;
            string resolved = CheryToolsAssets.ResolveAssetPath(fontPath);
            if (!string.IsNullOrEmpty(resolved) && System.IO.File.Exists(resolved))
            {
                normalizedPath = resolved;
                _normalizedFontPathCache[rawPath] = normalizedPath;
                return normalizedPath;
            }

            if (Main.ModEntry != null)
            {
                string bundled = System.IO.Path.Combine(Main.ModEntry.Path, "MiSans-Bold.ttf");
                if (System.IO.File.Exists(bundled))
                {
                    normalizedPath = bundled;
                    _normalizedFontPathCache[rawPath] = normalizedPath;
                    return normalizedPath;
                }
            }

            string local = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MiSans-Bold.ttf");
            normalizedPath = local;
            _normalizedFontPathCache[rawPath] = normalizedPath;
            return normalizedPath;
        }

        private static void ApplyOutline(
            TextMeshProUGUI tmp,
            bool enabled,
            Vector4 outlineColor,
            float outlineThickness,
            float outlineSoftness,
            bool shadowEnabled,
            Vector4 shadowColor,
            Vector2 shadowOffset,
            float shadowSoftness)
        {
            TMP_FontAsset font = tmp.font;
            if (font == null) return;

            float width = enabled && outlineColor.w > 0f ? Mathf.Clamp(outlineThickness / 32f, 0f, 0.35f) : 0f;
            float softness = enabled && outlineColor.w > 0f ? Mathf.Clamp(outlineSoftness / 32f, 0f, 1f) : 0f;
            bool hasShadow = shadowEnabled && shadowColor.w > 0f;

            if (width <= 0f && softness <= 0f && !hasShadow)
            {
                if (_materials.TryGetValue(tmp, out var oldMat) && oldMat != null)
                {
                    UnityEngine.Object.Destroy(oldMat);
                }
                _materials.Remove(tmp);
                _materialFonts.Remove(tmp);
                tmp.fontSharedMaterial = font.material;
                tmp.fontMaterial = font.material;
                tmp.material = font.material;
                tmp.UpdateMeshPadding();
                tmp.SetMaterialDirty();
                tmp.SetVerticesDirty();
                return;
            }

            bool needsMaterial = !_materials.TryGetValue(tmp, out var mat)
                || mat == null
                || !_materialFonts.TryGetValue(tmp, out var materialFont)
                || materialFont != font;

            if (needsMaterial)
            {
                if (mat != null) UnityEngine.Object.Destroy(mat);
                tmp.fontSharedMaterial = font.material;
                mat = UnityEngine.Object.Instantiate(tmp.fontSharedMaterial);
                _materials[tmp] = mat;
                _materialFonts[tmp] = font;
                tmp.fontMaterial = mat;
            }

            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, ToColor(outlineColor));
            mat.SetFloat(OutlineSoftnessProperty, softness);
            if (width > 0f)
            {
                mat.EnableKeyword("OUTLINE_ON");
            }
            else
            {
                mat.DisableKeyword("OUTLINE_ON");
            }

            ApplyUnderlay(mat, hasShadow, shadowColor, shadowOffset, shadowSoftness);

            tmp.fontSharedMaterial = mat;
            tmp.fontMaterial = mat;
            tmp.material = mat;
            tmp.UpdateMeshPadding();
            tmp.SetMaterialDirty();
            tmp.SetVerticesDirty();
        }

        private static void ApplyUnderlay(Material mat, bool enabled, Vector4 shadowColor, Vector2 shadowOffset, float shadowSoftness)
        {
            if (mat == null)
            {
                return;
            }

            bool supportsUnderlay = mat.HasProperty(UnderlayColorProperty)
                && mat.HasProperty(UnderlayOffsetXProperty)
                && mat.HasProperty(UnderlayOffsetYProperty)
                && mat.HasProperty(UnderlayDilateProperty)
                && mat.HasProperty(UnderlaySoftnessProperty);
            if (!supportsUnderlay)
            {
                mat.DisableKeyword("UNDERLAY_ON");
                mat.DisableKeyword("UNDERLAY_INNER");
                return;
            }

            if (!enabled)
            {
                mat.DisableKeyword("UNDERLAY_ON");
                mat.DisableKeyword("UNDERLAY_INNER");
                mat.SetColor(UnderlayColorProperty, Color.clear);
                mat.SetFloat(UnderlayOffsetXProperty, 0f);
                mat.SetFloat(UnderlayOffsetYProperty, 0f);
                mat.SetFloat(UnderlayDilateProperty, 0f);
                mat.SetFloat(UnderlaySoftnessProperty, 0f);
                return;
            }

            mat.EnableKeyword("UNDERLAY_ON");
            mat.DisableKeyword("UNDERLAY_INNER");
            mat.SetColor(UnderlayColorProperty, ToColor(shadowColor));
            mat.SetFloat(UnderlayOffsetXProperty, Mathf.Clamp(shadowOffset.x / 32f, -1f, 1f));
            mat.SetFloat(UnderlayOffsetYProperty, Mathf.Clamp(-shadowOffset.y / 32f, -1f, 1f));
            mat.SetFloat(UnderlayDilateProperty, 0f);
            mat.SetFloat(UnderlaySoftnessProperty, Mathf.Clamp(shadowSoftness / 32f, 0f, 1f));
        }

        private static float ClampOutlineThickness(float thickness)
        {
            if (float.IsNaN(thickness) || thickness <= 0f)
            {
                return 0f;
            }

            return Math.Min(thickness, 8f);
        }

        private static TextAlignmentOptions ToTmpAlignment(int alignment)
        {
            switch (alignment)
            {
                case 1:
                    return TextAlignmentOptions.Center;
                case 2:
                    return TextAlignmentOptions.Right;
                default:
                    return TextAlignmentOptions.Left;
            }
        }

        private static Color ToColor(Vector4 color)
        {
            return new Color(
                Mathf.Clamp01(color.x),
                Mathf.Clamp01(color.y),
                Mathf.Clamp01(color.z),
                Mathf.Clamp01(color.w));
        }

        private static Vector4 ColorArrayToVector4(float[] color, Vector4 fallback)
        {
            if (color == null || color.Length < 4) return fallback;
            return new Vector4(color[0], color[1], color[2], color[3]);
        }

        private static float RoundPixel(float value)
        {
            return Mathf.Round(value);
        }

        private static bool Approximately(float a, float b)
        {
            return Math.Abs(a - b) < 0.001f;
        }

        private static bool Approximately(Vector4 a, Vector4 b)
        {
            return Approximately(a.x, b.x)
                && Approximately(a.y, b.y)
                && Approximately(a.z, b.z)
                && Approximately(a.w, b.w);
        }
    }
}
