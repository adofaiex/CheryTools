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
            public float ContentLeft;
            public float ContentTop;
            public float ContentWidth;
            public float ContentHeight;
        }

        private const int CanvasSortingOrder = RenderDepth.DefaultTextSortingOrder;
        private const int DefaultAtlasSize = 1024;
        private const int DefaultSamplingSize = 72;
        private const int DefaultAtlasPadding = 9;
        private const int UnusedTextRetentionFrames = 600;
        private const int MeasureCacheCapacity = 2048;

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

        // Shared material pool for plain texts: texts with identical font + outline +
        // underlay parameters share one Material so UGUI can batch them, instead of
        // one Instantiate per outlined text. Token style layers (ids containing
        // "__ct_") keep private materials because MakeTokenLayerFaceTransparent
        // mutates the face color per layer.
        private static readonly Dictionary<PooledMaterialKey, Material> _materialPool = new Dictionary<PooledMaterialKey, Material>(PooledMaterialKeyComparer.Instance);
        private static readonly Dictionary<PooledMaterialKey, int> _materialPoolRefs = new Dictionary<PooledMaterialKey, int>(PooledMaterialKeyComparer.Instance);
        private static readonly Dictionary<TextMeshProUGUI, PooledMaterialKey> _pooledMaterialKeys = new Dictionary<TextMeshProUGUI, PooledMaterialKey>();
        private static readonly List<Font> _createdFonts = new List<Font>();

        private readonly struct PooledMaterialKey
        {
            public readonly int FontId;
            public readonly float OutlineWidth;
            public readonly float OutlineSoftness;
            public readonly Vector4 OutlineColor;
            public readonly bool HasShadow;
            public readonly Vector4 ShadowColor;
            public readonly Vector2 ShadowOffset;
            public readonly float ShadowSoftness;

            public PooledMaterialKey(int fontId, float outlineWidth, float outlineSoftness, Vector4 outlineColor,
                bool hasShadow, Vector4 shadowColor, Vector2 shadowOffset, float shadowSoftness)
            {
                FontId = fontId;
                OutlineWidth = outlineWidth;
                OutlineSoftness = outlineSoftness;
                OutlineColor = outlineColor;
                HasShadow = hasShadow;
                ShadowColor = shadowColor;
                ShadowOffset = shadowOffset;
                ShadowSoftness = shadowSoftness;
            }
        }

        private sealed class PooledMaterialKeyComparer : IEqualityComparer<PooledMaterialKey>
        {
            public static readonly PooledMaterialKeyComparer Instance = new PooledMaterialKeyComparer();

            public bool Equals(PooledMaterialKey a, PooledMaterialKey b)
            {
                return a.FontId == b.FontId
                    && a.OutlineWidth == b.OutlineWidth
                    && a.OutlineSoftness == b.OutlineSoftness
                    && a.OutlineColor == b.OutlineColor
                    && a.HasShadow == b.HasShadow
                    && a.ShadowColor == b.ShadowColor
                    && a.ShadowOffset == b.ShadowOffset
                    && a.ShadowSoftness == b.ShadowSoftness;
            }

            public int GetHashCode(PooledMaterialKey key)
            {
                unchecked
                {
                    int hash = key.FontId;
                    hash = hash * 31 + key.OutlineWidth.GetHashCode();
                    hash = hash * 31 + key.OutlineSoftness.GetHashCode();
                    hash = hash * 31 + key.OutlineColor.GetHashCode();
                    hash = hash * 31 + (key.HasShadow ? 1 : 0);
                    hash = hash * 31 + key.ShadowColor.GetHashCode();
                    hash = hash * 31 + key.ShadowOffset.GetHashCode();
                    hash = hash * 31 + key.ShadowSoftness.GetHashCode();
                    return hash;
                }
            }
        }
        private static readonly Dictionary<TextMeshProUGUI, TextObjectState> _textStates = new Dictionary<TextMeshProUGUI, TextObjectState>();
        private static readonly Dictionary<TextMeshProUGUI, TokenMeshState> _tokenMeshStates = new Dictionary<TextMeshProUGUI, TokenMeshState>();
        private static readonly Dictionary<MeasureKey, Vector2> _measureCache = new Dictionary<MeasureKey, Vector2>(MeasureKeyComparer.Instance);
        private static readonly Queue<MeasureKey> _measureCacheOrder = new Queue<MeasureKey>();
        private static readonly Dictionary<string, int> _tokenLayerOrderHashes = new Dictionary<string, int>();
        private static readonly Dictionary<string, List<string>> _tokenLayerIdsByFace = new Dictionary<string, List<string>>();
        private static readonly List<string> _tokenLayerIdBuffer = new List<string>(8);
        private static readonly Dictionary<string, Vector4> _tokenColorGroupBuffer = new Dictionary<string, Vector4>();
        private static readonly Dictionary<string, TokenGroupRenderOperation> _tokenGroupOperationMap = new Dictionary<string, TokenGroupRenderOperation>();
        private static readonly List<TokenGroupRenderOperation> _tokenGroupOperations = new List<TokenGroupRenderOperation>(8);
        private static readonly List<TokenGroupRenderOperation> _tokenGroupOperationPool = new List<TokenGroupRenderOperation>(8);
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
            public bool UseVertexGradient;
            public Vector4 ColorTopLeft;
            public Vector4 ColorTopRight;
            public Vector4 ColorBottomRight;
            public Vector4 ColorBottomLeft;
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

        private sealed class TokenMeshState
        {
            public string Text = string.Empty;
            public int LayoutHash;
            // Grow-only pooled snapshots of the TMP mesh. A text with a dynamic tag
            // plus token animation rebuilds every refresh, so per-rebuild Clone()
            // calls were a steady allocation stream. BaseVertexCounts records how many
            // entries of each (possibly larger) buffer are valid.
            public Vector3[][] BaseVertices = new Vector3[0][];
            public Color32[][] BaseColors = new Color32[0][];
            public int[] BaseVertexCounts = new int[0];
            public string[] TokenByCharacter;
            // Token id per TMP link index ("ct_" prefix stripped, null for foreign
            // links). GetLinkID() allocates a fresh string per call, so ids are
            // resolved once per mesh rebuild instead of per link per frame.
            public string[] TokenIdByLink;
            public int LastPoseHash;
            public TokenRenderChannel LastChannel;
            public bool HasPoseHash;
            public int LastLayerMaskHash;
            public bool HasLayerMaskHash;
            public bool Applied;
        }

        private sealed class TokenGroupRenderOperation
        {
            public OvTokenGroupTransform Transform;
            public readonly HashSet<string> TokenIds = new HashSet<string>();
        }

        private enum TokenRenderChannel
        {
            Face,
            Outline,
            Shadow
        }

        public static void BeginFrame()
        {
            _frameId++;
        }

        public static void EndFrame()
        {
            List<string> staleIds = null;
            foreach (var pair in _texts)
            {
                if (pair.Value == null) continue;
                bool usedThisFrame = _frameMarks.TryGetValue(pair.Value, out int mark) && mark == _frameId;
                if (!usedThisFrame)
                {
                    if (pair.Value.gameObject.activeSelf)
                    {
                        pair.Value.gameObject.SetActive(false);
                    }

                    int lastUsedFrame = _frameMarks.TryGetValue(pair.Value, out mark) ? mark : _frameId;
                    if (_frameId - lastUsedFrame > UnusedTextRetentionFrames)
                    {
                        if (staleIds == null) staleIds = new List<string>();
                        staleIds.Add(pair.Key);
                    }
                }
            }

            if (staleIds == null) return;
            for (int i = 0; i < staleIds.Count; i++)
            {
                ReleaseTextObject(staleIds[i]);
            }
        }

        private static void ReleaseTextObject(string id)
        {
            if (!_texts.TryGetValue(id, out TextMeshProUGUI tmp) || tmp == null)
            {
                _texts.Remove(id);
                _textSortingOrders.Remove(id);
                _tokenLayerOrderHashes.Remove(id);
                _tokenLayerIdsByFace.Remove(id);
                return;
            }

            ReleaseTextMaterial(tmp);
            _textStates.Remove(tmp);
            _tokenMeshStates.Remove(tmp);
            _frameMarks.Remove(tmp);
            _texts.Remove(id);
            _textSortingOrders.Remove(id);
            _tokenLayerOrderHashes.Remove(id);
            _tokenLayerIdsByFace.Remove(id);
            UnityEngine.Object.Destroy(tmp.gameObject);
        }

        public static void Shutdown()
        {
            foreach (var pair in _materials)
            {
                if (pair.Value != null) UnityEngine.Object.Destroy(pair.Value);
            }
            _materials.Clear();
            _materialFonts.Clear();
            foreach (var pair in _materialPool)
            {
                if (pair.Value != null) UnityEngine.Object.Destroy(pair.Value);
            }
            _materialPool.Clear();
            _materialPoolRefs.Clear();
            _pooledMaterialKeys.Clear();
            _textStates.Clear();
            _tokenMeshStates.Clear();
            _measureCache.Clear();
            _measureCacheOrder.Clear();
            _tokenLayerOrderHashes.Clear();
            _tokenLayerIdsByFace.Clear();
            _tokenLayerIdBuffer.Clear();
            _tokenColorGroupBuffer.Clear();
            _tokenGroupOperationMap.Clear();
            _tokenGroupOperations.Clear();
            _tokenGroupOperationPool.Clear();
            _texts.Clear();
            _textSortingOrders.Clear();
            _layerCanvases.Clear();
            _layerRects.Clear();

            // Destroy runtime-created font assets, their dynamic atlas textures and
            // source Font objects; previously they leaked across mod enable cycles.
            foreach (var pair in _fontAssets)
            {
                TMP_FontAsset asset = pair.Value;
                if (asset == null) continue;
                if (asset.atlasTextures != null)
                {
                    for (int i = 0; i < asset.atlasTextures.Length; i++)
                    {
                        if (asset.atlasTextures[i] != null)
                        {
                            UnityEngine.Object.Destroy(asset.atlasTextures[i]);
                        }
                    }
                }
                if (asset.material != null)
                {
                    UnityEngine.Object.Destroy(asset.material);
                }
                UnityEngine.Object.Destroy(asset);
            }
            _fontAssets.Clear();
            for (int i = 0; i < _createdFonts.Count; i++)
            {
                if (_createdFonts[i] != null)
                {
                    UnityEngine.Object.Destroy(_createdFonts[i]);
                }
            }
            _createdFonts.Clear();
            _failedFontTimes.Clear();
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

        public static TextBounds DrawOverlayerText(string id, OverlayerText ovText, string text, float offsetX, float offsetY, float scaleX, float scaleY, float minWidth = 0f, float minHeight = 0f, bool useFixedSize = false, int sortingOrder = CanvasSortingOrder, Dictionary<string, OvTokenPose> tokenPoses = null)
        {
            float fontSize = Math.Max(1f, ovText.FontSize);
            Vector2 contentMeasured = MeasureText(ovText.FontPath, text, fontSize, ovText.LetterSpacing, ovText.LineHeightOffset);
            Vector2 measured = useFixedSize
                ? new Vector2(Math.Max(1f, minWidth), Math.Max(1f, minHeight))
                : contentMeasured;
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

            bool splitOutline = ovText.EnableOutline && HasTokenChannelOverride(tokenPoses, TokenRenderChannel.Outline);
            bool splitShadow = materialShadowEnabled && HasTokenChannelOverride(tokenPoses, TokenRenderChannel.Shadow);

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
                ovText.EnableOutline && !splitOutline,
                ColorArrayToVector4(ovText.OutlineColor, new Vector4(0f, 0f, 0f, 1f)),
                ovText.OutlineThickness,
                ovText.LetterSpacing,
                ovText.LineHeightOffset,
                0f,
                sortingOrder,
                materialShadowEnabled && !splitShadow,
                materialShadow,
                materialShadowOffset,
                materialShadowSoftness);

            ApplyTokenPoses(id, tokenPoses, TokenRenderChannel.Face);
            if (splitOutline || splitShadow)
            {
                DrawTokenStyleLayers(
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
                    ovText.LetterSpacing,
                    ovText.LineHeightOffset,
                    sortingOrder,
                    tokenPoses,
                    splitOutline,
                    ColorArrayToVector4(ovText.OutlineColor, new Vector4(0f, 0f, 0f, 1f)),
                    ovText.OutlineThickness,
                    splitShadow,
                    materialShadow,
                    materialShadowOffset,
                    materialShadowSoftness);
            }
            else
            {
                _tokenLayerIdsByFace.Remove(id);
            }

            float layoutLeft = pivotedX - pivotX * drawWidth * safeScaleX;
            float layoutTop = pivotedY - pivotY * drawHeight * safeScaleY;
            float contentWidth = Math.Max(1f, contentMeasured.x) * safeScaleX;
            float contentHeight = Math.Max(1f, contentMeasured.y) * safeScaleY;
            float contentLeft = layoutLeft;
            if (ovText.Alignment == 1)
            {
                contentLeft += (drawWidth * safeScaleX - contentWidth) * 0.5f;
            }
            else if (ovText.Alignment == 2)
            {
                contentLeft += drawWidth * safeScaleX - contentWidth;
            }

            return new TextBounds
            {
                Left = layoutLeft,
                Top = layoutTop,
                Width = drawWidth * safeScaleX,
                Height = drawHeight * safeScaleY,
                ContentLeft = contentLeft,
                ContentTop = layoutTop,
                ContentWidth = contentWidth,
                ContentHeight = contentHeight
            };
        }

        private static bool HasTokenChannelOverride(Dictionary<string, OvTokenPose> poses, TokenRenderChannel channel)
        {
            if (poses == null) return false;
            foreach (KeyValuePair<string, OvTokenPose> pair in poses)
            {
                if (channel == TokenRenderChannel.Outline && pair.Value.HasOutlineColorOverride) return true;
                if (channel == TokenRenderChannel.Shadow && pair.Value.HasShadowColorOverride) return true;
            }
            return false;
        }

        private static void DrawTokenStyleLayers(
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
            float characterSpacing,
            float lineSpacing,
            int sortingOrder,
            Dictionary<string, OvTokenPose> poses,
            bool splitOutline,
            Vector4 baseOutlineColor,
            float outlineThickness,
            bool splitShadow,
            Vector4 baseShadowColor,
            Vector2 shadowOffset,
            float shadowSoftness)
        {
            List<string> layerIds = _tokenLayerIdBuffer;
            layerIds.Clear();
            if (splitShadow)
            {
                DrawTokenStyleLayer(id + "__ct_shadow_base", text, fontPath, fontSize, width, height,
                    x, y, pivotX, pivotY, scaleX, scaleY, alignment, characterSpacing, lineSpacing,
                    sortingOrder, poses, TokenRenderChannel.Shadow, true, string.Empty,
                    Vector4.zero, 0f, baseShadowColor, shadowOffset, shadowSoftness);
                layerIds.Add(id + "__ct_shadow_base");
                Dictionary<string, Vector4> groups = CollectTokenColorGroups(poses, TokenRenderChannel.Shadow);
                foreach (KeyValuePair<string, Vector4> group in groups)
                {
                    string layerId = id + "__ct_shadow_" + group.Key;
                    DrawTokenStyleLayer(layerId, text, fontPath, fontSize, width, height,
                        x, y, pivotX, pivotY, scaleX, scaleY, alignment, characterSpacing, lineSpacing,
                        sortingOrder, poses, TokenRenderChannel.Shadow, false, group.Key,
                        Vector4.zero, 0f, group.Value, shadowOffset, shadowSoftness);
                    layerIds.Add(layerId);
                }
            }
            if (splitOutline)
            {
                DrawTokenStyleLayer(id + "__ct_outline_base", text, fontPath, fontSize, width, height,
                    x, y, pivotX, pivotY, scaleX, scaleY, alignment, characterSpacing, lineSpacing,
                    sortingOrder, poses, TokenRenderChannel.Outline, true, string.Empty,
                    baseOutlineColor, outlineThickness, Vector4.zero, Vector2.zero, 0f);
                layerIds.Add(id + "__ct_outline_base");
                Dictionary<string, Vector4> groups = CollectTokenColorGroups(poses, TokenRenderChannel.Outline);
                foreach (KeyValuePair<string, Vector4> group in groups)
                {
                    string layerId = id + "__ct_outline_" + group.Key;
                    DrawTokenStyleLayer(layerId, text, fontPath, fontSize, width, height,
                        x, y, pivotX, pivotY, scaleX, scaleY, alignment, characterSpacing, lineSpacing,
                        sortingOrder, poses, TokenRenderChannel.Outline, false, group.Key,
                        group.Value, outlineThickness, Vector4.zero, Vector2.zero, 0f);
                    layerIds.Add(layerId);
                }
            }
            RememberTokenStyleLayers(id, layerIds);
            PlaceTokenStyleLayersBeforeFace(id, layerIds);
        }

        private static void RememberTokenStyleLayers(string faceId, List<string> layerIds)
        {
            if (!_tokenLayerIdsByFace.TryGetValue(faceId, out List<string> remembered))
            {
                remembered = new List<string>(Math.Max(4, layerIds.Count));
                _tokenLayerIdsByFace[faceId] = remembered;
            }
            remembered.Clear();
            for (int i = 0; i < layerIds.Count; i++) remembered.Add(layerIds[i]);
        }

        private static void DrawTokenStyleLayer(
            string layerId,
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
            float characterSpacing,
            float lineSpacing,
            int sortingOrder,
            Dictionary<string, OvTokenPose> poses,
            TokenRenderChannel channel,
            bool baseLayer,
            string groupId,
            Vector4 outlineColor,
            float outlineThickness,
            Vector4 shadowColor,
            Vector2 shadowOffset,
            float shadowSoftness)
        {
            bool outline = channel == TokenRenderChannel.Outline;
            bool shadow = channel == TokenRenderChannel.Shadow;
            DrawText(layerId, text, fontPath, fontSize, width, height, x, y, pivotX, pivotY,
                scaleX, scaleY, alignment, Vector4.one, outline, outlineColor, outlineThickness,
                characterSpacing, lineSpacing, 0f, sortingOrder, shadow, shadowColor, shadowOffset, shadowSoftness);
            ApplyTokenPoses(layerId, poses, channel);
            ApplyTokenLayerMask(layerId, poses, channel, baseLayer, groupId);
            MakeTokenLayerFaceTransparent(layerId);
        }

        private static Dictionary<string, Vector4> CollectTokenColorGroups(
            Dictionary<string, OvTokenPose> poses, TokenRenderChannel channel)
        {
            Dictionary<string, Vector4> result = _tokenColorGroupBuffer;
            result.Clear();
            if (poses == null) return result;
            foreach (KeyValuePair<string, OvTokenPose> pair in poses)
            {
                OvTokenPose pose = pair.Value;
                if (channel == TokenRenderChannel.Outline && pose.HasOutlineColorOverride)
                {
                    string group = string.IsNullOrEmpty(pose.OutlineColorGroupId) ? pair.Key : pose.OutlineColorGroupId;
                    result[group] = new Vector4(pose.OutlineColorR, pose.OutlineColorG, pose.OutlineColorB, pose.OutlineColorA);
                }
                else if (channel == TokenRenderChannel.Shadow && pose.HasShadowColorOverride)
                {
                    string group = string.IsNullOrEmpty(pose.ShadowColorGroupId) ? pair.Key : pose.ShadowColorGroupId;
                    result[group] = new Vector4(pose.ShadowColorR, pose.ShadowColorG, pose.ShadowColorB, pose.ShadowColorA);
                }
            }
            return result;
        }

        private static void PlaceTokenStyleLayersBeforeFace(string faceId, List<string> layerIds)
        {
            if (!_texts.TryGetValue(faceId, out TextMeshProUGUI face) || face == null) return;

            unchecked
            {
                int orderHash = 17;
                for (int i = 0; i < layerIds.Count; i++)
                {
                    orderHash = orderHash * 31 + (layerIds[i] != null ? layerIds[i].GetHashCode() : 0);
                }
                if (_tokenLayerOrderHashes.TryGetValue(faceId, out int previousHash)
                    && previousHash == orderHash)
                {
                    return;
                }
                _tokenLayerOrderHashes[faceId] = orderHash;
            }

            for (int i = 0; i < layerIds.Count; i++)
            {
                if (!_texts.TryGetValue(layerIds[i], out TextMeshProUGUI layer) || layer == null) continue;
                layer.transform.SetAsLastSibling();
            }
            face.transform.SetAsLastSibling();
        }

        private static void MakeTokenLayerFaceTransparent(string layerId)
        {
            if (!_texts.TryGetValue(layerId, out TextMeshProUGUI tmp) || tmp == null) return;
            if (!_materials.TryGetValue(tmp, out Material material) || material == null) return;
            if (material.HasProperty(ShaderUtilities.ID_FaceColor))
            {
                if (material.GetColor(ShaderUtilities.ID_FaceColor) != Color.clear)
                {
                    material.SetColor(ShaderUtilities.ID_FaceColor, Color.clear);
                    tmp.SetMaterialDirty();
                }
            }
        }

        private static void ApplyTokenLayerMask(string layerId, Dictionary<string, OvTokenPose> poses,
            TokenRenderChannel channel, bool baseLayer, string groupId)
        {
            if (!_texts.TryGetValue(layerId, out TextMeshProUGUI tmp) || tmp == null) return;
            TMP_TextInfo info = tmp.textInfo;
            if (!_tokenMeshStates.TryGetValue(tmp, out TokenMeshState state)) return;
            if (state.TokenByCharacter == null || state.TokenByCharacter.Length != info.characterCount)
            {
                state.TokenByCharacter = BuildTokenCharacterMap(info);
            }

            unchecked
            {
                int maskHash = 17;
                maskHash = maskHash * 31 + state.LayoutHash;
                maskHash = maskHash * 31 + state.LastPoseHash;
                maskHash = maskHash * 31 + (int)channel;
                maskHash = maskHash * 31 + (baseLayer ? 1 : 0);
                maskHash = maskHash * 31 + (groupId != null ? groupId.GetHashCode() : 0);
                if (state.HasLayerMaskHash && state.LastLayerMaskHash == maskHash) return;
                state.LastLayerMaskHash = maskHash;
                state.HasLayerMaskHash = true;
            }

            string[] tokenByCharacter = state.TokenByCharacter;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible) continue;
                string tokenId = tokenByCharacter[i];
                bool visible = IsTokenVisibleInLayer(tokenId, poses, channel, baseLayer, groupId);
                if (visible) continue;
                int materialIndex = character.materialReferenceIndex;
                int vertex = character.vertexIndex;
                if (materialIndex < 0 || materialIndex >= info.meshInfo.Length) continue;
                Color32[] colors = info.meshInfo[materialIndex].colors32;
                if (colors == null || vertex < 0 || vertex + 3 >= colors.Length) continue;
                for (int v = 0; v < 4; v++)
                {
                    Color32 color = colors[vertex + v];
                    color.a = 0;
                    colors[vertex + v] = color;
                }
            }
            tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        private static string[] BuildTokenCharacterMap(TMP_TextInfo info)
        {
            string[] tokenByCharacter = new string[Math.Max(0, info.characterCount)];
            for (int i = 0; i < info.linkCount; i++)
            {
                TMP_LinkInfo link = info.linkInfo[i];
                string tokenId = ResolveLinkTokenId(link);
                if (tokenId == null) continue;
                int first = Math.Max(0, link.linkTextfirstCharacterIndex);
                int end = Math.Min(info.characterCount, first + Math.Max(0, link.linkTextLength));
                for (int c = first; c < end; c++) tokenByCharacter[c] = tokenId;
            }
            return tokenByCharacter;
        }

        private static string[] BuildTokenLinkMap(TMP_TextInfo info)
        {
            string[] tokenIdByLink = new string[Math.Max(0, info.linkCount)];
            for (int i = 0; i < info.linkCount; i++)
            {
                tokenIdByLink[i] = ResolveLinkTokenId(info.linkInfo[i]);
            }
            return tokenIdByLink;
        }

        private static string ResolveLinkTokenId(TMP_LinkInfo link)
        {
            string linkId = link.GetLinkID();
            if (string.IsNullOrEmpty(linkId) || !linkId.StartsWith("ct_", StringComparison.Ordinal)) return null;
            return linkId.Substring(3);
        }

        private static bool IsTokenVisibleInLayer(string tokenId, Dictionary<string, OvTokenPose> poses,
            TokenRenderChannel channel, bool baseLayer, string groupId)
        {
            if (string.IsNullOrEmpty(tokenId)) return baseLayer;
            OvTokenPose pose = default(OvTokenPose);
            bool hasPose = poses != null && poses.TryGetValue(tokenId, out pose);
            bool hasOverride = hasPose && (channel == TokenRenderChannel.Outline
                ? pose.HasOutlineColorOverride
                : pose.HasShadowColorOverride);
            if (baseLayer) return !hasOverride;
            if (!hasOverride) return false;
            string tokenGroup = channel == TokenRenderChannel.Outline ? pose.OutlineColorGroupId : pose.ShadowColorGroupId;
            if (string.IsNullOrEmpty(tokenGroup)) tokenGroup = tokenId;
            return string.Equals(tokenGroup, groupId, StringComparison.Ordinal);
        }

        private static bool ApplyTokenPoses(string id, Dictionary<string, OvTokenPose> poses,
            TokenRenderChannel channel = TokenRenderChannel.Face)
        {
            if (!_texts.TryGetValue(id, out TextMeshProUGUI tmp) || tmp == null) return false;
            bool hasPoses = poses != null && poses.Count > 0;
            if (!_tokenMeshStates.TryGetValue(tmp, out TokenMeshState state))
            {
                if (!hasPoses) return false;
                state = new TokenMeshState();
                _tokenMeshStates[tmp] = state;
            }

            int layoutHash = BuildTokenLayoutHash(tmp);
            bool rebuild = !string.Equals(state.Text, tmp.text, StringComparison.Ordinal)
                || state.LayoutHash != layoutHash
                || state.BaseVertices == null
                || state.BaseVertices.Length == 0;
            if (rebuild)
            {
                tmp.ForceMeshUpdate();
                TMP_TextInfo textInfo = tmp.textInfo;
                int meshCount = textInfo.meshInfo.Length;
                if (state.BaseVertices.Length != meshCount)
                {
                    state.BaseVertices = new Vector3[meshCount][];
                    state.BaseColors = new Color32[meshCount][];
                    state.BaseVertexCounts = new int[meshCount];
                }
                for (int i = 0; i < meshCount; i++)
                {
                    Vector3[] vertices = textInfo.meshInfo[i].vertices;
                    Color32[] colors = textInfo.meshInfo[i].colors32;
                    int vertexCount = vertices != null ? vertices.Length : 0;
                    Vector3[] vertexBuffer = state.BaseVertices[i];
                    if (vertexBuffer == null || vertexBuffer.Length < vertexCount)
                    {
                        vertexBuffer = new Vector3[vertexCount];
                        state.BaseVertices[i] = vertexBuffer;
                    }
                    if (vertexCount > 0) Array.Copy(vertices, vertexBuffer, vertexCount);

                    int colorCount = colors != null ? colors.Length : 0;
                    Color32[] colorBuffer = state.BaseColors[i];
                    if (colorBuffer == null || colorBuffer.Length < colorCount)
                    {
                        colorBuffer = new Color32[colorCount];
                        state.BaseColors[i] = colorBuffer;
                    }
                    if (colorCount > 0) Array.Copy(colors, colorBuffer, colorCount);
                    state.BaseVertexCounts[i] = vertexCount;
                }
                state.Text = tmp.text ?? string.Empty;
                state.LayoutHash = layoutHash;
                state.TokenByCharacter = BuildTokenCharacterMap(textInfo);
                state.TokenIdByLink = BuildTokenLinkMap(textInfo);
                state.HasPoseHash = false;
                state.HasLayerMaskHash = false;
                state.Applied = false;
            }

            if (!hasPoses && !state.Applied) return false;

            int poseHash = BuildTokenPoseHash(poses, channel);
            if (hasPoses && state.Applied && state.HasPoseHash
                && state.LastChannel == channel && state.LastPoseHash == poseHash)
            {
                return false;
            }

            TMP_TextInfo info = tmp.textInfo;
            RestoreTokenBaseMesh(info, state);
            if (hasPoses)
            {
                string[] tokenIdByLink = state.TokenIdByLink;
                for (int i = 0; i < info.linkCount; i++)
                {
                    string tokenId = tokenIdByLink != null && i < tokenIdByLink.Length
                        ? tokenIdByLink[i]
                        : ResolveLinkTokenId(info.linkInfo[i]);
                    if (tokenId == null) continue;
                    if (!poses.TryGetValue(tokenId, out OvTokenPose pose)) continue;
                    ApplyPoseToLink(info, info.linkInfo[i], pose, channel);
                }
                ApplyTokenGroupTransforms(info, poses, tokenIdByLink);
            }

            tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
            state.Applied = hasPoses;
            state.LastPoseHash = poseHash;
            state.LastChannel = channel;
            state.HasPoseHash = true;
            state.HasLayerMaskHash = false;
            return true;
        }

        private static int BuildTokenPoseHash(Dictionary<string, OvTokenPose> poses,
            TokenRenderChannel channel)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)channel;
                if (poses == null) return hash;
                foreach (KeyValuePair<string, OvTokenPose> pair in poses)
                {
                    OvTokenPose pose = pair.Value;
                    hash = hash * 31 + (pair.Key != null ? pair.Key.GetHashCode() : 0);
                    hash = hash * 31 + pose.OffsetX.GetHashCode();
                    hash = hash * 31 + pose.OffsetY.GetHashCode();
                    hash = hash * 31 + pose.ScaleX.GetHashCode();
                    hash = hash * 31 + pose.ScaleY.GetHashCode();
                    hash = hash * 31 + pose.Rotation.GetHashCode();
                    hash = hash * 31 + pose.Opacity.GetHashCode();
                    hash = hash * 31 + pose.HasTextColorOverride.GetHashCode();
                    hash = hash * 31 + pose.TextColorR.GetHashCode();
                    hash = hash * 31 + pose.TextColorG.GetHashCode();
                    hash = hash * 31 + pose.TextColorB.GetHashCode();
                    hash = hash * 31 + pose.TextColorA.GetHashCode();
                    hash = hash * 31 + pose.HasOutlineColorOverride.GetHashCode();
                    hash = hash * 31 + pose.OutlineColorR.GetHashCode();
                    hash = hash * 31 + pose.OutlineColorG.GetHashCode();
                    hash = hash * 31 + pose.OutlineColorB.GetHashCode();
                    hash = hash * 31 + pose.OutlineColorA.GetHashCode();
                    hash = hash * 31 + (pose.OutlineColorGroupId != null ? pose.OutlineColorGroupId.GetHashCode() : 0);
                    hash = hash * 31 + pose.HasShadowColorOverride.GetHashCode();
                    hash = hash * 31 + pose.ShadowColorR.GetHashCode();
                    hash = hash * 31 + pose.ShadowColorG.GetHashCode();
                    hash = hash * 31 + pose.ShadowColorB.GetHashCode();
                    hash = hash * 31 + pose.ShadowColorA.GetHashCode();
                    hash = hash * 31 + (pose.ShadowColorGroupId != null ? pose.ShadowColorGroupId.GetHashCode() : 0);
                    if (pose.GroupTransforms == null) continue;
                    for (int i = 0; i < pose.GroupTransforms.Count; i++)
                    {
                        OvTokenGroupTransform transform = pose.GroupTransforms[i];
                        if (transform == null) continue;
                        hash = hash * 31 + (transform.Id != null ? transform.Id.GetHashCode() : 0);
                        hash = hash * 31 + transform.Order;
                        hash = hash * 31 + transform.OffsetX.GetHashCode();
                        hash = hash * 31 + transform.OffsetY.GetHashCode();
                        hash = hash * 31 + transform.ScaleX.GetHashCode();
                        hash = hash * 31 + transform.ScaleY.GetHashCode();
                        hash = hash * 31 + transform.Rotation.GetHashCode();
                    }
                }
                return hash;
            }
        }

        private static void ApplyTokenGroupTransforms(TMP_TextInfo info,
            Dictionary<string, OvTokenPose> poses, string[] tokenIdByLink)
        {
            Dictionary<string, TokenGroupRenderOperation> operationMap = _tokenGroupOperationMap;
            List<TokenGroupRenderOperation> operations = _tokenGroupOperations;
            operationMap.Clear();
            operations.Clear();
            foreach (KeyValuePair<string, OvTokenPose> pair in poses)
            {
                List<OvTokenGroupTransform> transforms = pair.Value.GroupTransforms;
                if (transforms == null) continue;
                for (int i = 0; i < transforms.Count; i++)
                {
                    OvTokenGroupTransform transform = transforms[i];
                    if (transform == null || string.IsNullOrEmpty(transform.Id)) continue;
                    if (!operationMap.TryGetValue(transform.Id, out TokenGroupRenderOperation operation))
                    {
                        int poolIndex = operationMap.Count;
                        if (poolIndex < _tokenGroupOperationPool.Count)
                        {
                            operation = _tokenGroupOperationPool[poolIndex];
                            operation.TokenIds.Clear();
                        }
                        else
                        {
                            operation = new TokenGroupRenderOperation();
                            _tokenGroupOperationPool.Add(operation);
                        }
                        operation.Transform = transform;
                        operationMap[transform.Id] = operation;
                        operations.Add(operation);
                    }
                    operation.TokenIds.Add(pair.Key);
                }
            }
            if (operationMap.Count == 0) return;

            operations.Sort((left, right) =>
            {
                int order = left.Transform.Order.CompareTo(right.Transform.Order);
                return order != 0
                    ? order
                    : string.CompareOrdinal(left.Transform.Id, right.Transform.Id);
            });
            for (int i = 0; i < operations.Count; i++)
            {
                ApplyTokenGroupTransform(info, operations[i], tokenIdByLink);
            }
        }

        private static void ApplyTokenGroupTransform(TMP_TextInfo info, TokenGroupRenderOperation operation,
            string[] tokenIdByLink)
        {
            bool hasBounds = false;
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, 0f);
            for (int i = 0; i < info.linkCount; i++)
            {
                string tokenId = tokenIdByLink != null && i < tokenIdByLink.Length
                    ? tokenIdByLink[i]
                    : ResolveLinkTokenId(info.linkInfo[i]);
                if (tokenId == null || !operation.TokenIds.Contains(tokenId)) continue;
                ExpandLinkBounds(info, info.linkInfo[i], ref min, ref max, ref hasBounds);
            }
            if (!hasBounds) return;

            Vector2 pivot = new Vector2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);
            for (int i = 0; i < info.linkCount; i++)
            {
                string tokenId = tokenIdByLink != null && i < tokenIdByLink.Length
                    ? tokenIdByLink[i]
                    : ResolveLinkTokenId(info.linkInfo[i]);
                if (tokenId == null || !operation.TokenIds.Contains(tokenId)) continue;
                TransformLinkVertices(info, info.linkInfo[i], pivot, operation.Transform);
            }
        }

        private static void ExpandLinkBounds(TMP_TextInfo info, TMP_LinkInfo link, ref Vector3 min,
            ref Vector3 max, ref bool hasBounds)
        {
            int first = Math.Max(0, link.linkTextfirstCharacterIndex);
            int end = Math.Min(info.characterCount, first + Math.Max(0, link.linkTextLength));
            for (int i = first; i < end; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible) continue;
                int material = character.materialReferenceIndex;
                int vertex = character.vertexIndex;
                if (material < 0 || material >= info.meshInfo.Length) continue;
                Vector3[] vertices = info.meshInfo[material].vertices;
                if (vertices == null || vertex < 0 || vertex + 3 >= vertices.Length) continue;
                for (int v = 0; v < 4; v++)
                {
                    Vector3 point = vertices[vertex + v];
                    min.x = Math.Min(min.x, point.x);
                    min.y = Math.Min(min.y, point.y);
                    max.x = Math.Max(max.x, point.x);
                    max.y = Math.Max(max.y, point.y);
                    hasBounds = true;
                }
            }
        }

        private static void TransformLinkVertices(TMP_TextInfo info, TMP_LinkInfo link, Vector2 pivot,
            OvTokenGroupTransform transform)
        {
            int first = Math.Max(0, link.linkTextfirstCharacterIndex);
            int end = Math.Min(info.characterCount, first + Math.Max(0, link.linkTextLength));
            for (int i = first; i < end; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible) continue;
                int material = character.materialReferenceIndex;
                int vertex = character.vertexIndex;
                if (material < 0 || material >= info.meshInfo.Length) continue;
                Vector3[] vertices = info.meshInfo[material].vertices;
                if (vertices == null || vertex < 0 || vertex + 3 >= vertices.Length) continue;
                for (int v = 0; v < 4; v++)
                {
                    Vector3 point = vertices[vertex + v];
                    Vector2 transformed = TransformGroupedTokenPoint(
                        new Vector2(point.x, point.y), pivot, transform);
                    vertices[vertex + v] = new Vector3(transformed.x, transformed.y, point.z);
                }
            }
        }

        internal static Vector2 TransformGroupedTokenPoint(Vector2 point, Vector2 pivot,
            OvTokenGroupTransform transform)
        {
            if (transform == null) return point;
            float scaleX = Math.Abs(transform.ScaleX) < 0.0001f ? 1f : transform.ScaleX;
            float scaleY = Math.Abs(transform.ScaleY) < 0.0001f ? 1f : transform.ScaleY;
            float x = (point.x - pivot.x) * scaleX;
            float y = (point.y - pivot.y) * scaleY;
            float radians = transform.Rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(
                pivot.x + x * cos - y * sin + transform.OffsetX,
                pivot.y + x * sin + y * cos - transform.OffsetY);
        }

        private static int BuildTokenLayoutHash(TextMeshProUGUI tmp)
        {
            if (!_textStates.TryGetValue(tmp, out TextObjectState state)) return 0;
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (state.Font != null ? state.Font.GetInstanceID() : 0);
                hash = hash * 31 + state.FontSize.GetHashCode();
                hash = hash * 31 + state.Width.GetHashCode();
                hash = hash * 31 + state.Height.GetHashCode();
                hash = hash * 31 + state.Alignment;
                hash = hash * 31 + state.CharacterSpacing.GetHashCode();
                hash = hash * 31 + state.LineSpacing.GetHashCode();
                hash = hash * 31 + state.Color.GetHashCode();
                hash = hash * 31 + state.UseVertexGradient.GetHashCode();
                hash = hash * 31 + state.ColorTopLeft.GetHashCode();
                hash = hash * 31 + state.ColorTopRight.GetHashCode();
                hash = hash * 31 + state.ColorBottomRight.GetHashCode();
                hash = hash * 31 + state.ColorBottomLeft.GetHashCode();
                hash = hash * 31 + state.OutlineEnabled.GetHashCode();
                hash = hash * 31 + state.OutlineThickness.GetHashCode();
                hash = hash * 31 + state.OutlineSoftness.GetHashCode();
                hash = hash * 31 + state.ShadowEnabled.GetHashCode();
                hash = hash * 31 + state.ShadowOffsetX.GetHashCode();
                hash = hash * 31 + state.ShadowOffsetY.GetHashCode();
                hash = hash * 31 + state.ShadowSoftness.GetHashCode();
                return hash;
            }
        }

        private static void RestoreTokenBaseMesh(TMP_TextInfo info, TokenMeshState state)
        {
            int count = Math.Min(info.meshInfo.Length, state.BaseVertices.Length);
            for (int i = 0; i < count; i++)
            {
                // Pooled buffers may be larger than the live mesh; only the recorded
                // valid range is meaningful.
                int validCount = state.BaseVertexCounts != null && i < state.BaseVertexCounts.Length
                    ? state.BaseVertexCounts[i]
                    : 0;
                Vector3[] vertices = info.meshInfo[i].vertices;
                Vector3[] baseVertices = state.BaseVertices[i];
                if (vertices != null && baseVertices != null)
                {
                    Array.Copy(baseVertices, vertices, Math.Min(validCount, vertices.Length));
                }
                Color32[] colors = info.meshInfo[i].colors32;
                Color32[] baseColors = state.BaseColors[i];
                if (colors != null && baseColors != null)
                {
                    Array.Copy(baseColors, colors, Math.Min(validCount, colors.Length));
                }
            }
        }

        private static void ApplyPoseToLink(TMP_TextInfo info, TMP_LinkInfo link, OvTokenPose pose,
            TokenRenderChannel channel)
        {
            int first = Math.Max(0, link.linkTextfirstCharacterIndex);
            int end = Math.Min(info.characterCount, first + Math.Max(0, link.linkTextLength));
            bool hasBounds = false;
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, 0f);
            for (int i = first; i < end; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible) continue;
                int material = character.materialReferenceIndex;
                int vertex = character.vertexIndex;
                if (material < 0 || material >= info.meshInfo.Length) continue;
                Vector3[] vertices = info.meshInfo[material].vertices;
                if (vertices == null || vertex < 0 || vertex + 3 >= vertices.Length) continue;
                for (int v = 0; v < 4; v++)
                {
                    Vector3 point = vertices[vertex + v];
                    min.x = Math.Min(min.x, point.x);
                    min.y = Math.Min(min.y, point.y);
                    max.x = Math.Max(max.x, point.x);
                    max.y = Math.Max(max.y, point.y);
                    hasBounds = true;
                }
            }
            if (!hasBounds) return;

            Vector3 center = (min + max) * 0.5f;
            float scaleX = Math.Abs(pose.ScaleX) < 0.0001f ? 1f : pose.ScaleX;
            float scaleY = Math.Abs(pose.ScaleY) < 0.0001f ? 1f : pose.ScaleY;
            float radians = pose.Rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            float alpha = Mathf.Clamp01(pose.Opacity);

            for (int i = first; i < end; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible) continue;
                int material = character.materialReferenceIndex;
                int vertex = character.vertexIndex;
                if (material < 0 || material >= info.meshInfo.Length) continue;
                Vector3[] vertices = info.meshInfo[material].vertices;
                Color32[] colors = info.meshInfo[material].colors32;
                if (vertices == null || vertex < 0 || vertex + 3 >= vertices.Length) continue;
                for (int v = 0; v < 4; v++)
                {
                    Vector3 point = vertices[vertex + v] - center;
                    point.x *= scaleX;
                    point.y *= scaleY;
                    float rotatedX = point.x * cos - point.y * sin;
                    float rotatedY = point.x * sin + point.y * cos;
                    vertices[vertex + v] = center + new Vector3(rotatedX + pose.OffsetX, rotatedY - pose.OffsetY, point.z);
                    if (colors != null && vertex + v < colors.Length)
                    {
                        Color32 color = colors[vertex + v];
                        float channelAlpha = channel == TokenRenderChannel.Face && pose.HasTextColorOverride
                            ? Mathf.Clamp01(pose.TextColorA)
                            : 1f;
                        if (channel == TokenRenderChannel.Face && pose.HasTextColorOverride)
                        {
                            color.r = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(pose.TextColorR) * 255f), 0, 255);
                            color.g = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(pose.TextColorG) * 255f), 0, 255);
                            color.b = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(pose.TextColorB) * 255f), 0, 255);
                        }
                        color.a = (byte)Mathf.Clamp(Mathf.RoundToInt(color.a * alpha * channelAlpha), 0, 255);
                        colors[vertex + v] = color;
                    }
                }
            }
        }

        public static TextBounds DrawScreenText(string id, string text, string fontPath, float fontSize, Vector2 topLeft, Vector2 size, int alignment, Vector4 color, bool outlineEnabled, Vector4 outlineColor, float outlineThickness, int sortingOrder = CanvasSortingOrder, bool shadowEnabled = false, Vector4 shadowColor = default(Vector4), Vector2 shadowOffset = default(Vector2), float shadowSoftness = 0f, bool useVertexGradient = false, Vector4 colorTopLeft = default(Vector4), Vector4 colorTopRight = default(Vector4), Vector4 colorBottomRight = default(Vector4), Vector4 colorBottomLeft = default(Vector4))
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
            DrawText(id, text, fontPath, fontSize, safeSize.x, safeSize.y, pos.x, pos.y, 0f, 0f, 1f, 1f, alignment, color, outlineEnabled, outlineColor, outlineThickness, 0f, 0f, 0f, sortingOrder, materialShadowEnabled, shadowColor, materialShadowOffset, materialShadowSoftness, useVertexGradient, colorTopLeft, colorTopRight, colorBottomRight, colorBottomLeft);
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

        // Keeps an already-built TMP object alive without re-running text
        // measurement, layout, material setup or token mesh processing.  This is
        // the fast path used by the runtime baker for content whose complete
        // visual state is unchanged since the last overlay revision.
        public static bool KeepAlive(string id, int sortingOrder = CanvasSortingOrder)
        {
            bool kept = TouchScreenText(id, sortingOrder);
            if (_tokenLayerIdsByFace.TryGetValue(id, out List<string> layerIds))
            {
                for (int i = 0; i < layerIds.Count; i++)
                {
                    TouchScreenText(layerIds[i], sortingOrder);
                }
            }
            return kept;
        }

        // Struct cache key: the previous string.Join key allocated ~5 strings per
        // lookup even on a cache hit, and MeasureText runs for every OV text per frame.
        private readonly struct MeasureKey
        {
            public readonly string FontPath;
            public readonly string Text;
            public readonly float FontSize;
            public readonly float CharacterSpacing;
            public readonly float LineSpacing;

            public MeasureKey(string fontPath, string text, float fontSize, float characterSpacing, float lineSpacing)
            {
                FontPath = fontPath;
                Text = text;
                FontSize = fontSize;
                CharacterSpacing = characterSpacing;
                LineSpacing = lineSpacing;
            }
        }

        private sealed class MeasureKeyComparer : IEqualityComparer<MeasureKey>
        {
            public static readonly MeasureKeyComparer Instance = new MeasureKeyComparer();

            public bool Equals(MeasureKey a, MeasureKey b)
            {
                return a.FontSize == b.FontSize
                    && a.CharacterSpacing == b.CharacterSpacing
                    && a.LineSpacing == b.LineSpacing
                    && string.Equals(a.Text, b.Text, StringComparison.Ordinal)
                    && string.Equals(a.FontPath, b.FontPath, StringComparison.Ordinal);
            }

            public int GetHashCode(MeasureKey key)
            {
                unchecked
                {
                    int hash = key.FontPath != null ? key.FontPath.GetHashCode() : 0;
                    hash = hash * 31 + (key.Text != null ? key.Text.GetHashCode() : 0);
                    hash = hash * 31 + key.FontSize.GetHashCode();
                    hash = hash * 31 + key.CharacterSpacing.GetHashCode();
                    hash = hash * 31 + key.LineSpacing.GetHashCode();
                    return hash;
                }
            }
        }

        public static Vector2 MeasureText(string fontPath, string text, float fontSize, float characterSpacing, float lineSpacing)
        {
            if (!EnsureReady()) return Vector2.zero;
            string normalizedPath = NormalizeFontPath(fontPath);
            string safeText = text ?? string.Empty;
            MeasureKey cacheKey = new MeasureKey(normalizedPath, safeText, fontSize, characterSpacing, lineSpacing);
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
            while (_measureCache.Count >= MeasureCacheCapacity && _measureCacheOrder.Count > 0)
            {
                MeasureKey oldestKey = _measureCacheOrder.Dequeue();
                _measureCache.Remove(oldestKey);
            }
            _measureCache[cacheKey] = measured;
            _measureCacheOrder.Enqueue(cacheKey);
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
            float shadowSoftness = 0f,
            bool useVertexGradient = false,
            Vector4 colorTopLeft = default(Vector4),
            Vector4 colorTopRight = default(Vector4),
            Vector4 colorBottomRight = default(Vector4),
            Vector4 colorBottomLeft = default(Vector4))
        {
            float outlinePixels = ClampOutlineThickness(outlineThickness);

            DrawTextObject(id, text, fontPath, fontSize, width, height, x, y, pivotX, pivotY, scaleX, scaleY, alignment, color, outlineEnabled, outlineColor, outlinePixels, characterSpacing, lineSpacing, outlineSoftness, sortingOrder, shadowEnabled, shadowColor, shadowOffset, shadowSoftness, useVertexGradient, colorTopLeft, colorTopRight, colorBottomRight, colorBottomLeft);
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
            float shadowSoftness,
            bool useVertexGradient,
            Vector4 colorTopLeft,
            Vector4 colorTopRight,
            Vector4 colorBottomRight,
            Vector4 colorBottomLeft)
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
            VertexGradient vertexGradient = new VertexGradient(
                ToColor(useVertexGradient ? colorTopLeft : color),
                ToColor(useVertexGradient ? colorTopRight : color),
                ToColor(useVertexGradient ? colorBottomLeft : color),
                ToColor(useVertexGradient ? colorBottomRight : color));
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
                || !Approximately(lastState.Color, color)
                || lastState.UseVertexGradient != useVertexGradient
                || !Approximately(lastState.ColorTopLeft, colorTopLeft)
                || !Approximately(lastState.ColorTopRight, colorTopRight)
                || !Approximately(lastState.ColorBottomRight, colorBottomRight)
                || !Approximately(lastState.ColorBottomLeft, colorBottomLeft);
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
                tmp.enableVertexGradient = useVertexGradient;
                tmp.colorGradient = vertexGradient;
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

            // Only write the ~30-field state struct back when something changed;
            // the unconditional write was a per-text-per-frame dictionary copy.
            if (contentChanged || outlineChanged || transformChanged)
            {
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
                UseVertexGradient = useVertexGradient,
                ColorTopLeft = colorTopLeft,
                ColorTopRight = colorTopRight,
                ColorBottomRight = colorBottomRight,
                ColorBottomLeft = colorBottomLeft,
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
            }
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

        // Font load failures are remembered with a cooldown: without it, a missing or
        // broken font file re-probes the disk (and may retry the whole font-asset
        // creation) for every drawn text on every frame.
        private static readonly Dictionary<string, float> _failedFontTimes = new Dictionary<string, float>();
        private const float FailedFontRetrySeconds = 5f;

        private static TMP_FontAsset GetFontAsset(string fontPath)
        {
            string path = NormalizeFontPath(fontPath);
            if (_fontAssets.TryGetValue(path, out var asset) && asset != null) return asset;

            string failureKey = path ?? string.Empty;
            if (_failedFontTimes.TryGetValue(failureKey, out float failedAt))
            {
                if (Time.realtimeSinceStartup - failedAt < FailedFontRetrySeconds)
                {
                    return null;
                }
                _failedFontTimes.Remove(failureKey);
            }

            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                _failedFontTimes[failureKey] = Time.realtimeSinceStartup;
                return null;
            }

            try
            {
                Font font = new Font(path);
                _createdFonts.Add(font);
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

            if (asset == null)
            {
                _failedFontTimes[failureKey] = Time.realtimeSinceStartup;
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
                string bundled = System.IO.Path.Combine(Main.ModEntry.Path, "Resources", "MiSans-Bold.ttf");
                if (System.IO.File.Exists(bundled))
                {
                    normalizedPath = bundled;
                    _normalizedFontPathCache[rawPath] = normalizedPath;
                    return normalizedPath;
                }

                bundled = System.IO.Path.Combine(Main.ModEntry.Path, "MiSans-Bold.ttf");
                if (System.IO.File.Exists(bundled))
                {
                    normalizedPath = bundled;
                    _normalizedFontPathCache[rawPath] = normalizedPath;
                    return normalizedPath;
                }
            }

            string local = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "MiSans-Bold.ttf");
            if (!System.IO.File.Exists(local))
            {
                local = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MiSans-Bold.ttf");
            }
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
                ReleaseTextMaterial(tmp);
                tmp.fontSharedMaterial = font.material;
                tmp.fontMaterial = font.material;
                tmp.material = font.material;
                tmp.UpdateMeshPadding();
                tmp.SetMaterialDirty();
                tmp.SetVerticesDirty();
                return;
            }

            // Token style layers get a private material instance because their face
            // color is mutated per layer after this call; everything else shares a
            // pooled material keyed by the full style so identical texts can batch.
            bool isTokenLayer = tmp.gameObject.name.IndexOf("__ct_", StringComparison.Ordinal) >= 0;
            if (isTokenLayer)
            {
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

                ConfigureOutlineMaterial(mat, width, softness, outlineColor, hasShadow, shadowColor, shadowOffset, shadowSoftness);

                tmp.fontSharedMaterial = mat;
                tmp.fontMaterial = mat;
                tmp.material = mat;
                tmp.UpdateMeshPadding();
                tmp.SetMaterialDirty();
                tmp.SetVerticesDirty();
                return;
            }

            PooledMaterialKey key = new PooledMaterialKey(
                font.GetInstanceID(), width, softness, outlineColor,
                hasShadow, shadowColor, shadowOffset, shadowSoftness);

            if (_pooledMaterialKeys.TryGetValue(tmp, out PooledMaterialKey currentKey)
                && PooledMaterialKeyComparer.Instance.Equals(currentKey, key)
                && _materialPool.TryGetValue(key, out Material assigned)
                && assigned != null
                && ReferenceEquals(tmp.fontSharedMaterial, assigned))
            {
                return;
            }

            ReleaseTextMaterial(tmp);

            if (!_materialPool.TryGetValue(key, out Material pooled) || pooled == null)
            {
                pooled = UnityEngine.Object.Instantiate(font.material);
                ConfigureOutlineMaterial(pooled, width, softness, outlineColor, hasShadow, shadowColor, shadowOffset, shadowSoftness);
                _materialPool[key] = pooled;
                _materialPoolRefs[key] = 0;
            }
            _materialPoolRefs.TryGetValue(key, out int refs);
            _materialPoolRefs[key] = refs + 1;
            _pooledMaterialKeys[tmp] = key;

            // Only fontSharedMaterial is set on the pooled path: assigning fontMaterial
            // would make TMP treat the shared instance as text-owned and destroy it
            // with the text.
            tmp.fontSharedMaterial = pooled;
            tmp.UpdateMeshPadding();
            tmp.SetMaterialDirty();
            tmp.SetVerticesDirty();
        }

        private static void ConfigureOutlineMaterial(Material mat, float width, float softness, Vector4 outlineColor,
            bool hasShadow, Vector4 shadowColor, Vector2 shadowOffset, float shadowSoftness)
        {
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
        }

        private static void ReleaseTextMaterial(TextMeshProUGUI tmp)
        {
            if (_materials.TryGetValue(tmp, out Material privateMat))
            {
                if (privateMat != null) UnityEngine.Object.Destroy(privateMat);
                _materials.Remove(tmp);
                _materialFonts.Remove(tmp);
            }

            if (_pooledMaterialKeys.TryGetValue(tmp, out PooledMaterialKey key))
            {
                _pooledMaterialKeys.Remove(tmp);
                if (_materialPoolRefs.TryGetValue(key, out int refs))
                {
                    refs--;
                    if (refs <= 0)
                    {
                        if (_materialPool.TryGetValue(key, out Material pooled) && pooled != null)
                        {
                            UnityEngine.Object.Destroy(pooled);
                        }
                        _materialPool.Remove(key);
                        _materialPoolRefs.Remove(key);
                    }
                    else
                    {
                        _materialPoolRefs[key] = refs;
                    }
                }
            }
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
