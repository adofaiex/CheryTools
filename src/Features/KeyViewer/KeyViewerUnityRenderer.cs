using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CheryTools
{
    internal static class KeyViewerUnityRenderer
    {
        private const int CanvasSortingOrder = RenderDepth.EditOverlaySortingOrder;
        private const int UnusedImageRetentionFrames = 600;

        private static GameObject _root;
        private static RectTransform _rootRect;
        private static int _frameId;

        private static readonly Dictionary<int, RectTransform> _layerRoots = new Dictionary<int, RectTransform>();
        private static readonly Dictionary<int, CheryRectBatchGroup> _rainBatches = new Dictionary<int, CheryRectBatchGroup>();
        private static readonly Dictionary<int, CheryRectBatchGroup> _rectBatches = new Dictionary<int, CheryRectBatchGroup>();
        private static readonly Dictionary<int, CheryShadowBatchGraphic> _shadowBatches = new Dictionary<int, CheryShadowBatchGraphic>();
        private static readonly Dictionary<string, KeyViewerImageGraphic> _images = new Dictionary<string, KeyViewerImageGraphic>();
        private static readonly Dictionary<string, int> _imageSortingOrders = new Dictionary<string, int>();
        private static readonly Dictionary<GameObject, int> _frameMarks = new Dictionary<GameObject, int>();

        public static void BeginFrame()
        {
            _frameId++;
            EnsureReady();
            foreach (var pair in _rainBatches)
            {
                if (pair.Value != null) pair.Value.BeginFrame();
            }
            foreach (var pair in _rectBatches)
            {
                if (pair.Value != null) pair.Value.BeginFrame();
            }
            foreach (var pair in _shadowBatches)
            {
                if (pair.Value != null) pair.Value.BeginFrame();
            }
        }

        public static void EndFrame()
        {
            foreach (var pair in _rainBatches)
            {
                if (pair.Value != null) pair.Value.EndFrame();
            }
            foreach (var pair in _rectBatches)
            {
                if (pair.Value != null) pair.Value.EndFrame();
            }
            foreach (var pair in _shadowBatches)
            {
                if (pair.Value != null) pair.Value.EndFrame();
            }
            CleanupUnusedImages();
        }

        public static void HideAll()
        {
            foreach (var pair in _rainBatches)
            {
                if (pair.Value != null) pair.Value.HideAll();
            }
            foreach (var pair in _rectBatches)
            {
                if (pair.Value != null) pair.Value.HideAll();
            }
            foreach (var pair in _shadowBatches)
            {
                if (pair.Value != null) pair.Value.gameObject.SetActive(false);
            }
            foreach (var pair in _images)
            {
                if (pair.Value != null) pair.Value.gameObject.SetActive(false);
            }
        }

        public static void Shutdown()
        {
            _rainBatches.Clear();
            _rectBatches.Clear();
            _shadowBatches.Clear();
            _layerRoots.Clear();
            _images.Clear();
            _imageSortingOrders.Clear();
            _frameMarks.Clear();

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }

            _rootRect = null;
        }

        public static void DrawRect(
            string id,
            Vector2 topLeft,
            Vector2 size,
            uint fillTopLeft,
            uint fillTopRight,
            uint fillBottomRight,
            uint fillBottomLeft,
            uint borderTopLeft,
            uint borderTopRight,
            uint borderBottomRight,
            uint borderBottomLeft,
            float borderThickness,
            float cornerRadius,
            int sortingOrder = CanvasSortingOrder)
        {
            if (!EnsureReady()) return;
            CheryRectBatchGroup batch = GetRectBatch(sortingOrder);
            if (batch == null) return;
            batch.AddRect(
                topLeft,
                size,
                ToColor(fillTopLeft),
                ToColor(fillTopRight),
                ToColor(fillBottomRight),
                ToColor(fillBottomLeft),
                ToColor(borderTopLeft),
                ToColor(borderTopRight),
                ToColor(borderBottomRight),
                ToColor(borderBottomLeft),
                borderThickness,
                cornerRadius);
        }

        public static void DrawGradientRect(
            string id,
            Vector2 topLeft,
            Vector2 size,
            uint topLeftColor,
            uint topRightColor,
            uint bottomRightColor,
            uint bottomLeftColor,
            float cornerRadius,
            int sortingOrder = CanvasSortingOrder)
        {
            if (!EnsureReady()) return;
            CheryRectBatchGroup batch = GetRainBatch(sortingOrder);
            if (batch == null) return;
            batch.AddRect(topLeft, size, ToColor(topLeftColor), ToColor(topRightColor), ToColor(bottomRightColor), ToColor(bottomLeftColor), Color.clear, Color.clear, Color.clear, Color.clear, 0f, cornerRadius);
        }

        public static void DrawKeyRainCurveRect(
            string id,
            Vector2 topLeft,
            Vector2 size,
            uint baseColor,
            uint farColor,
            bool gradientEnabled,
            bool heightMaskGradient,
            int fadeMode,
            float keyY,
            float maxHeight,
            float fadeHeight,
            float fadePower,
            float gradientHeight,
            float gradientPower,
            bool horizontalGradientEnabled,
            uint horizontalColor,
            float cornerRadius,
            int sortingOrder = CanvasSortingOrder)
        {
            if (!EnsureReady()) return;
            CheryRectBatchGroup batch = GetRainBatch(sortingOrder);
            if (batch == null) return;
            batch.AddKeyRainCurveRect(
                topLeft,
                size,
                ToColor(baseColor),
                ToColor(farColor),
                gradientEnabled,
                heightMaskGradient,
                fadeMode,
                keyY,
                maxHeight,
                fadeHeight,
                fadePower,
                gradientHeight,
                gradientPower,
                horizontalGradientEnabled,
                ToColor(horizontalColor),
                cornerRadius);
        }

        public static void DrawSoftGradientShadowRect(string id, Vector2 topLeft, Vector2 size, uint topColor, uint bottomColor, float softness, int sortingOrder = CanvasSortingOrder)
        {
            if (!EnsureReady()) return;
            CheryShadowBatchGraphic batch = GetShadowBatch(sortingOrder);
            if (batch == null) return;
            batch.AddShadow(topLeft, size, ToColor(topColor), ToColor(bottomColor), softness);
        }

        public static void DrawImage(string id, Texture texture, Vector2 topLeft, Vector2 size, float alpha, float cornerRadius = 0f, int sortingOrder = CanvasSortingOrder)
        {
            DrawImage(id, texture, topLeft, size, alpha, cornerRadius, Vector2.zero, Vector2.one, sortingOrder);
        }

        public static void DrawImage(string id, Texture texture, Vector2 topLeft, Vector2 size, float alpha, float cornerRadius, Vector2 uvMin, Vector2 uvMax, int sortingOrder = CanvasSortingOrder)
        {
            if (!EnsureReady() || texture == null) return;
            KeyViewerImageGraphic image = GetImage(id, sortingOrder);
            if (image == null) return;

            SetRectTransform(image.rectTransform, topLeft, size);
            image.SetImage(texture, alpha, cornerRadius, uvMin, uvMax);
            Mark(image.gameObject);
        }

        public static bool KeepImageAlive(string id, int sortingOrder = CanvasSortingOrder)
        {
            if (!EnsureReady()) return false;
            if (!_images.TryGetValue(id, out KeyViewerImageGraphic image) || image == null)
            {
                return false;
            }
            if (!_imageSortingOrders.TryGetValue(id, out int currentOrder) || currentOrder != sortingOrder)
            {
                return false;
            }
            Mark(image.gameObject);
            return true;
        }

        private static bool EnsureReady()
        {
            if (_root != null && _rootRect != null) return true;

            try
            {
                _root = new GameObject("CheryTools_KV_Unity_Root", typeof(RectTransform));
                UnityEngine.Object.DontDestroyOnLoad(_root);

                _rootRect = _root.GetComponent<RectTransform>();
                _rootRect.anchorMin = Vector2.zero;
                _rootRect.anchorMax = Vector2.one;
                _rootRect.offsetMin = Vector2.zero;
                _rootRect.offsetMax = Vector2.zero;
                _rootRect.pivot = new Vector2(0.5f, 0.5f);
                return true;
            }
            catch (Exception ex)
            {
                if (Main.Logger != null)
                {
                    Main.Logger.Log("[CheryTools] KV Unity renderer init failed: " + ex.Message);
                }
                return false;
            }
        }

        private static RectTransform GetLayerRoot(int sortingOrder)
        {
            if (!EnsureReady()) return null;
            if (_layerRoots.TryGetValue(sortingOrder, out RectTransform existing) && existing != null)
            {
                return existing;
            }

            GameObject go = new GameObject("KV_Layer_" + sortingOrder.ToString(), typeof(RectTransform));
            go.transform.SetParent(_rootRect, false);

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            _layerRoots[sortingOrder] = rt;
            return rt;
        }

        private static CheryRectBatchGroup GetRainBatch(int sortingOrder)
        {
            if (_rainBatches.TryGetValue(sortingOrder, out CheryRectBatchGroup batch) && batch != null)
            {
                return batch;
            }

            batch = CreateRectBatchGroup("KV_Rain_Batch_" + sortingOrder.ToString(), sortingOrder);
            _rainBatches[sortingOrder] = batch;
            return batch;
        }

        private static CheryRectBatchGroup GetRectBatch(int sortingOrder)
        {
            if (_rectBatches.TryGetValue(sortingOrder, out CheryRectBatchGroup batch) && batch != null)
            {
                return batch;
            }

            batch = CreateRectBatchGroup("KV_Rect_Batch_" + sortingOrder.ToString(), sortingOrder);
            _rectBatches[sortingOrder] = batch;
            return batch;
        }

        private static CheryShadowBatchGraphic GetShadowBatch(int sortingOrder)
        {
            if (_shadowBatches.TryGetValue(sortingOrder, out CheryShadowBatchGraphic batch) && batch != null)
            {
                return batch;
            }

            batch = CreateShadowBatch("KV_Shadow_Batch_" + sortingOrder.ToString(), sortingOrder);
            _shadowBatches[sortingOrder] = batch;
            return batch;
        }

        private static CheryRectBatchGroup CreateRectBatchGroup(string name, int sortingOrder)
        {
            if (!EnsureReady()) return null;
            RectTransform layerRoot = GetLayerRoot(sortingOrder);
            if (layerRoot == null) return null;

            // Stable graphic first so it renders below the dynamic one.
            CheryRectBatchGraphic stableGraphic = CreateRectBatchGraphic(name + "_stable", layerRoot);
            CheryRectBatchGraphic dynamicGraphic = CreateRectBatchGraphic(name + "_dynamic", layerRoot);
            if (stableGraphic == null || dynamicGraphic == null) return null;

            var group = new CheryRectBatchGroup();
            group.Initialize(stableGraphic, dynamicGraphic);
            return group;
        }

        internal static CheryRectBatchGraphic CreateRectBatchGraphic(string name, RectTransform layerRoot)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(layerRoot, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            CheryRectBatchGraphic batch = go.AddComponent<CheryRectBatchGraphic>();
            batch.raycastTarget = false;
            return batch;
        }

        private static CheryShadowBatchGraphic CreateShadowBatch(string name, int sortingOrder)
        {
            if (!EnsureReady()) return null;
            RectTransform layerRoot = GetLayerRoot(sortingOrder);
            if (layerRoot == null) return null;

            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(layerRoot, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            CheryShadowBatchGraphic batch = go.AddComponent<CheryShadowBatchGraphic>();
            batch.raycastTarget = false;
            return batch;
        }

        private static KeyViewerImageGraphic GetImage(string id, int sortingOrder)
        {
            RectTransform layerRoot = GetLayerRoot(sortingOrder);
            if (layerRoot == null) return null;

            if (_images.TryGetValue(id, out var image) && image != null)
            {
                if (!_imageSortingOrders.TryGetValue(id, out int currentOrder)
                    || currentOrder != sortingOrder
                    || image.transform.parent != layerRoot)
                {
                    image.transform.SetParent(layerRoot, false);
                    _imageSortingOrders[id] = sortingOrder;
                }
                return image;
            }

            GameObject go = new GameObject("KV_Image_" + id, typeof(RectTransform));
            go.transform.SetParent(layerRoot, false);
            image = go.AddComponent<KeyViewerImageGraphic>();
            image.raycastTarget = false;
            _images[id] = image;
            _imageSortingOrders[id] = sortingOrder;
            return image;
        }

        private static void SetRectTransform(RectTransform rt, Vector2 topLeft, Vector2 size)
        {
            Vector2 anchor = new Vector2(0f, 1f);
            Vector2 position = new Vector2(Mathf.Round(topLeft.x), -Mathf.Round(topLeft.y));
            Vector2 safeSize = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            if (rt.anchorMin != anchor) rt.anchorMin = anchor;
            if (rt.anchorMax != anchor) rt.anchorMax = anchor;
            if (rt.pivot != anchor) rt.pivot = anchor;
            if (rt.anchoredPosition != position) rt.anchoredPosition = position;
            if (rt.sizeDelta != safeSize) rt.sizeDelta = safeSize;
            if (rt.localScale != Vector3.one) rt.localScale = Vector3.one;
        }

        private static void Mark(GameObject go)
        {
            if (!go.activeSelf) go.SetActive(true);
            _frameMarks[go] = _frameId;
        }

        private static void CleanupUnusedImages()
        {
            List<string> staleIds = null;
            foreach (var pair in _images)
            {
                KeyViewerImageGraphic item = pair.Value;
                if (item == null) continue;
                GameObject go = item.gameObject;
                bool usedThisFrame = _frameMarks.TryGetValue(go, out int mark) && mark == _frameId;
                if (usedThisFrame) continue;

                if (go.activeSelf)
                {
                    go.SetActive(false);
                }

                int lastUsedFrame = _frameMarks.TryGetValue(go, out mark) ? mark : _frameId;
                if (_frameId - lastUsedFrame > UnusedImageRetentionFrames)
                {
                    if (staleIds == null) staleIds = new List<string>();
                    staleIds.Add(pair.Key);
                }
            }

            if (staleIds == null) return;
            for (int i = 0; i < staleIds.Count; i++)
            {
                string id = staleIds[i];
                if (!_images.TryGetValue(id, out KeyViewerImageGraphic image)) continue;
                if (image != null)
                {
                    _frameMarks.Remove(image.gameObject);
                    UnityEngine.Object.Destroy(image.gameObject);
                }
                _images.Remove(id);
                _imageSortingOrders.Remove(id);
            }
        }

        private static Color ToColor(uint color)
        {
            return new Color(
                (color & 0xFF) / 255f,
                ((color >> 8) & 0xFF) / 255f,
                ((color >> 16) & 0xFF) / 255f,
                ((color >> 24) & 0xFF) / 255f);
        }
    }

    internal static class KeyViewerRoundedRectMesh
    {
        public const int MaxCornerSegments = 16;
        public const float AntiAliasHalfWidth = 0.5f;

        private const int MinCornerSegments = 4;
        private const float SegmentScale = 1.25f;
        private static readonly Vector2[][] UnitContours = CreateUnitContours();

        public static int CalculateCornerSegments(float radius)
        {
            if (radius <= 0.01f) return MinCornerSegments;

            // Approximation of the segment count needed for <= 0.2 px chord
            // error. This avoids an Acos call for every rounded command.
            int segments = Mathf.CeilToInt(SegmentScale * Mathf.Sqrt(radius));
            return Mathf.Clamp(segments, MinCornerSegments, MaxCornerSegments);
        }

        public static Rect Expand(Rect r, float amount)
        {
            return new Rect(r.xMin - amount, r.yMin - amount, r.width + amount * 2f, r.height + amount * 2f);
        }

        public static Rect Inset(Rect r, float amount)
        {
            float safe = Mathf.Min(Mathf.Max(0f, amount), Mathf.Min(r.width, r.height) * 0.5f);
            return new Rect(r.xMin + safe, r.yMin + safe, Mathf.Max(0f, r.width - safe * 2f), Mathf.Max(0f, r.height - safe * 2f));
        }

        public static void Build(Rect r, float radius, int segments, List<Vector2> points)
        {
            points.Clear();
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(r.width, r.height) * 0.5f);
            segments = Mathf.Clamp(segments, MinCornerSegments, MaxCornerSegments);

            Vector2[] samples = UnitContours[segments];
            int arcLength = segments + 1;
            AddSampledArc(points, new Vector2(r.xMax - radius, r.yMax - radius), radius, samples, 0, arcLength);
            AddSampledArc(points, new Vector2(r.xMax - radius, r.yMin + radius), radius, samples, arcLength, arcLength);
            AddSampledArc(points, new Vector2(r.xMin + radius, r.yMin + radius), radius, samples, arcLength * 2, arcLength);
            AddSampledArc(points, new Vector2(r.xMin + radius, r.yMax - radius), radius, samples, arcLength * 3, arcLength);
        }

        private static void AddSampledArc(List<Vector2> points, Vector2 center, float radius, Vector2[] samples, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 sample = samples[offset + i];
                points.Add(new Vector2(center.x + sample.x * radius, center.y + sample.y * radius));
            }
        }

        private static Vector2[][] CreateUnitContours()
        {
            var contours = new Vector2[MaxCornerSegments + 1][];
            for (int segments = MinCornerSegments; segments <= MaxCornerSegments; segments++)
            {
                int arcLength = segments + 1;
                Vector2[] samples = new Vector2[arcLength * 4];
                int index = 0;
                SampleUnitArc(samples, ref index, segments, 90f, 0f);
                SampleUnitArc(samples, ref index, segments, 0f, -90f);
                SampleUnitArc(samples, ref index, segments, -90f, -180f);
                SampleUnitArc(samples, ref index, segments, 180f, 90f);
                contours[segments] = samples;
            }
            return contours;
        }

        private static void SampleUnitArc(Vector2[] samples, ref int index, int segments, float startDeg, float endDeg)
        {
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float rad = Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad;
                samples[index++] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            }
        }
    }

    internal class KeyViewerImageGraphic : MaskableGraphic
    {
        private Texture _texture;
        private float _alpha = 1f;
        private float _cornerRadius;
        private Vector2 _uvMin = Vector2.zero;
        private Vector2 _uvMax = Vector2.one;
        private readonly List<Vector2> _innerPoints = new List<Vector2>(KeyViewerRoundedRectMesh.MaxCornerSegments * 4 + 4);
        private readonly List<Vector2> _outerPoints = new List<Vector2>(KeyViewerRoundedRectMesh.MaxCornerSegments * 4 + 4);

        public override Texture mainTexture
        {
            get { return _texture != null ? _texture : s_WhiteTexture; }
        }

        public void SetImage(Texture texture, float alpha, float cornerRadius, Vector2 uvMin, Vector2 uvMax)
        {
            float safeAlpha = Mathf.Clamp01(alpha);
            float safeCornerRadius = Mathf.Max(0f, cornerRadius);
            bool textureChanged = _texture != texture;
            bool meshChanged = !Mathf.Approximately(_alpha, safeAlpha)
                || !Mathf.Approximately(_cornerRadius, safeCornerRadius)
                || _uvMin != uvMin
                || _uvMax != uvMax;

            if (!textureChanged && !meshChanged)
            {
                return;
            }

            _texture = texture;
            _alpha = safeAlpha;
            _cornerRadius = safeCornerRadius;
            _uvMin = uvMin;
            _uvMax = uvMax;
            if (meshChanged) SetVerticesDirty();
            if (textureChanged) SetMaterialDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (r.width <= 0f || r.height <= 0f) return;

            float radius = Mathf.Min(_cornerRadius, Mathf.Min(r.width, r.height) * 0.5f);
            if (radius <= 0.01f)
            {
                AddTexturedRect(vh, r, new Color(1f, 1f, 1f, _alpha), _uvMin, _uvMax);
                return;
            }

            int segments = KeyViewerRoundedRectMesh.CalculateCornerSegments(radius);
            float aa = Mathf.Min(KeyViewerRoundedRectMesh.AntiAliasHalfWidth, Mathf.Min(r.width, r.height) * 0.25f);
            Rect innerRect = KeyViewerRoundedRectMesh.Inset(r, aa);
            Rect outerRect = KeyViewerRoundedRectMesh.Expand(r, aa);
            KeyViewerRoundedRectMesh.Build(innerRect, Mathf.Max(0f, radius - aa), segments, _innerPoints);
            KeyViewerRoundedRectMesh.Build(outerRect, radius + aa, segments, _outerPoints);

            Color color = new Color(1f, 1f, 1f, _alpha);
            AddTexturedFill(vh, _innerPoints, r, color, _uvMin, _uvMax);
            AddTexturedFringe(vh, _outerPoints, _innerPoints, r, color, _uvMin, _uvMax);
        }

        private static void AddTexturedRect(VertexHelper vh, Rect r, Color color, Vector2 uvMin, Vector2 uvMax)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector2(r.xMin, r.yMax), color, new Vector2(uvMin.x, uvMax.y));
            vh.AddVert(new Vector2(r.xMax, r.yMax), color, uvMax);
            vh.AddVert(new Vector2(r.xMax, r.yMin), color, new Vector2(uvMax.x, uvMin.y));
            vh.AddVert(new Vector2(r.xMin, r.yMin), color, uvMin);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddTexturedFill(VertexHelper vh, List<Vector2> points, Rect r, Color color, Vector2 uvMin, Vector2 uvMax)
        {
            int centerIndex = vh.currentVertCount;
            Vector2 center = r.center;
            vh.AddVert(center, color, ToUv(center, r, uvMin, uvMax));

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                vh.AddVert(p, color, ToUv(p, r, uvMin, uvMax));
            }

            for (int i = 0; i < points.Count; i++)
            {
                int next = i == points.Count - 1 ? 1 : i + 2;
                vh.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + next);
            }
        }

        private static void AddTexturedFringe(VertexHelper vh, List<Vector2> outer, List<Vector2> inner, Rect r, Color color, Vector2 uvMin, Vector2 uvMax)
        {
            int count = Mathf.Min(outer.Count, inner.Count);
            if (count < 2) return;

            Color transparent = color;
            transparent.a = 0f;
            int outerStart = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                vh.AddVert(outer[i], transparent, ToUv(outer[i], r, uvMin, uvMax));
            }

            int innerStart = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                vh.AddVert(inner[i], color, ToUv(inner[i], r, uvMin, uvMax));
            }

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                vh.AddTriangle(outerStart + i, outerStart + next, innerStart + next);
                vh.AddTriangle(outerStart + i, innerStart + next, innerStart + i);
            }
        }

        private static Vector2 ToUv(Vector2 point, Rect r, Vector2 uvMin, Vector2 uvMax)
        {
            float u = r.width <= 0f ? 0f : Mathf.InverseLerp(r.xMin, r.xMax, point.x);
            float v = r.height <= 0f ? 0f : Mathf.InverseLerp(r.yMin, r.yMax, point.y);
            return new Vector2(Mathf.Lerp(uvMin.x, uvMax.x, u), Mathf.Lerp(uvMin.y, uvMax.y, v));
        }

    }

    internal struct CheryRectCommand
        {
        public Vector2 TopLeft;
        public Vector2 Size;
        public Color TopLeftColor;
        public Color TopRightColor;
        public Color BottomRightColor;
        public Color BottomLeftColor;
        public Color BorderTopLeftColor;
        public Color BorderTopRightColor;
        public Color BorderBottomRightColor;
        public Color BorderBottomLeftColor;
        public float BorderThickness;
        public float CornerRadius;
        public bool IsKeyRainCurve;
        public Color FarColor;
        public bool GradientEnabled;
        public bool HeightMaskGradient;
        public int FadeMode;
        public float KeyY;
        public float MaxHeight;
        public float FadeHeight;
        public float FadePower;
        public float GradientHeight;
        public float GradientPower;
        public bool HorizontalGradientEnabled;
        public Color HorizontalColor;
    }

    // Splits a rect batch into a "stable" graphic (commands identical to last
    // frame) and a "dynamic" graphic (commands that changed), so one animating rect
    // no longer re-triangulates every rect in its layer. The stable graphic renders
    // first (below); draw-order correctness is preserved by promoting any stable
    // command that would have to render above an overlapping dynamic command into
    // the dynamic graphic. Per-command hashes are computed once at Add time.
    internal sealed class CheryRectBatchGroup
    {
        private readonly List<CheryRectCommand> _commands = new List<CheryRectCommand>(64);
        private readonly List<int> _hashes = new List<int>(64);
        private readonly List<int> _prevHashes = new List<int>(64);
        private readonly List<bool> _dynamicFlags = new List<bool>(64);
        private readonly List<int> _stableIndices = new List<int>(64);
        private readonly List<int> _dynamicIndices = new List<int>(64);
        private int _lastStableViewHash;
        private int _lastDynamicViewHash;

        public CheryRectBatchGraphic StableGraphic;
        public CheryRectBatchGraphic DynamicGraphic;

        public void Initialize(CheryRectBatchGraphic stableGraphic, CheryRectBatchGraphic dynamicGraphic)
        {
            StableGraphic = stableGraphic;
            DynamicGraphic = dynamicGraphic;
            stableGraphic.SetView(_commands, _stableIndices);
            dynamicGraphic.SetView(_commands, _dynamicIndices);
        }

        public void BeginFrame()
        {
            _commands.Clear();
            _hashes.Clear();
        }

        public void HideAll()
        {
            if (StableGraphic != null && StableGraphic.gameObject.activeSelf)
            {
                StableGraphic.gameObject.SetActive(false);
            }
            if (DynamicGraphic != null && DynamicGraphic.gameObject.activeSelf)
            {
                DynamicGraphic.gameObject.SetActive(false);
            }
        }

        public void AddRect(Vector2 topLeft, Vector2 size, Color topColor, Color bottomColor, Color borderColor, float borderThickness, float cornerRadius)
        {
            AddRect(topLeft, size, topColor, topColor, bottomColor, bottomColor, borderColor, borderColor, borderColor, borderColor, borderThickness, cornerRadius);
        }

        public void AddRect(
            Vector2 topLeft,
            Vector2 size,
            Color topLeftColor,
            Color topRightColor,
            Color bottomRightColor,
            Color bottomLeftColor,
            Color borderTopLeftColor,
            Color borderTopRightColor,
            Color borderBottomRightColor,
            Color borderBottomLeftColor,
            float borderThickness,
            float cornerRadius)
        {
            if (size.x <= 0f || size.y <= 0f) return;
            var cmd = new CheryRectCommand
            {
                TopLeft = topLeft,
                Size = size,
                TopLeftColor = topLeftColor,
                TopRightColor = topRightColor,
                BottomRightColor = bottomRightColor,
                BottomLeftColor = bottomLeftColor,
                BorderTopLeftColor = borderTopLeftColor,
                BorderTopRightColor = borderTopRightColor,
                BorderBottomRightColor = borderBottomRightColor,
                BorderBottomLeftColor = borderBottomLeftColor,
                BorderThickness = Mathf.Max(0f, borderThickness),
                CornerRadius = Mathf.Max(0f, cornerRadius),
                IsKeyRainCurve = false
            };
            _commands.Add(cmd);
            _hashes.Add(HashCommand(ref cmd));
        }

        public void AddKeyRainCurveRect(
            Vector2 topLeft,
            Vector2 size,
            Color baseColor,
            Color farColor,
            bool gradientEnabled,
            bool heightMaskGradient,
            int fadeMode,
            float keyY,
            float maxHeight,
            float fadeHeight,
            float fadePower,
            float gradientHeight,
            float gradientPower,
            bool horizontalGradientEnabled,
            Color horizontalColor,
            float cornerRadius)
        {
            if (size.x <= 0f || size.y <= 0f) return;
            if (baseColor.a <= 0f && (!gradientEnabled || farColor.a <= 0f)) return;
            var cmd = new CheryRectCommand
            {
                TopLeft = topLeft,
                Size = size,
                TopLeftColor = baseColor,
                TopRightColor = baseColor,
                BottomRightColor = baseColor,
                BottomLeftColor = baseColor,
                BorderTopLeftColor = Color.clear,
                BorderTopRightColor = Color.clear,
                BorderBottomRightColor = Color.clear,
                BorderBottomLeftColor = Color.clear,
                BorderThickness = 0f,
                CornerRadius = Mathf.Max(0f, cornerRadius),
                IsKeyRainCurve = true,
                FarColor = farColor,
                GradientEnabled = gradientEnabled,
                HeightMaskGradient = heightMaskGradient,
                FadeMode = fadeMode,
                KeyY = keyY,
                MaxHeight = Mathf.Max(1f, maxHeight),
                FadeHeight = Mathf.Clamp(fadeHeight, 0.05f, 3f),
                FadePower = Mathf.Clamp(fadePower, 0.1f, 5f),
                GradientHeight = Mathf.Clamp(gradientHeight, 0.05f, 3f),
                GradientPower = Mathf.Clamp(gradientPower, 0.1f, 5f),
                HorizontalGradientEnabled = horizontalGradientEnabled,
                HorizontalColor = horizontalColor
            };
            _commands.Add(cmd);
            _hashes.Add(HashCommand(ref cmd));
        }

        public void EndFrame()
        {
            int count = _commands.Count;
            _dynamicFlags.Clear();
            bool countChanged = count != _prevHashes.Count;
            for (int i = 0; i < count; i++)
            {
                _dynamicFlags.Add(countChanged || _hashes[i] != _prevHashes[i]);
            }

            // Promote stable commands that sit above (were submitted after) an
            // overlapping dynamic command; iterate to a fixpoint because a promotion
            // can force further promotions above it.
            bool promoted = count > 0;
            int iterations = 0;
            while (promoted && iterations++ < 8)
            {
                promoted = false;
                for (int i = 0; i < count; i++)
                {
                    if (_dynamicFlags[i]) continue;
                    for (int j = 0; j < i; j++)
                    {
                        if (!_dynamicFlags[j]) continue;
                        if (CommandsOverlap(_commands[i], _commands[j]))
                        {
                            _dynamicFlags[i] = true;
                            promoted = true;
                            break;
                        }
                    }
                }
            }
            if (iterations >= 8)
            {
                for (int i = 0; i < count; i++) _dynamicFlags[i] = true;
            }

            _stableIndices.Clear();
            _dynamicIndices.Clear();
            int stableViewHash;
            int dynamicViewHash;
            unchecked
            {
                stableViewHash = 17;
                dynamicViewHash = 17;
                for (int i = 0; i < count; i++)
                {
                    if (_dynamicFlags[i])
                    {
                        _dynamicIndices.Add(i);
                        dynamicViewHash = dynamicViewHash * 31 + _hashes[i];
                    }
                    else
                    {
                        _stableIndices.Add(i);
                        stableViewHash = stableViewHash * 31 + _hashes[i];
                    }
                }
            }

            ApplyView(StableGraphic, _stableIndices.Count > 0, stableViewHash, ref _lastStableViewHash);
            ApplyView(DynamicGraphic, _dynamicIndices.Count > 0, dynamicViewHash, ref _lastDynamicViewHash);

            _prevHashes.Clear();
            _prevHashes.AddRange(_hashes);
        }

        private static void ApplyView(CheryRectBatchGraphic graphic, bool hasContent, int viewHash, ref int lastViewHash)
        {
            if (graphic == null) return;
            if (graphic.gameObject.activeSelf != hasContent)
            {
                graphic.gameObject.SetActive(hasContent);
            }
            if (!hasContent)
            {
                lastViewHash = 0;
                return;
            }
            if (viewHash != lastViewHash)
            {
                lastViewHash = viewHash;
                graphic.SetVerticesDirty();
            }
        }

        private static bool CommandsOverlap(CheryRectCommand a, CheryRectCommand b)
        {
            return a.TopLeft.x < b.TopLeft.x + b.Size.x
                && b.TopLeft.x < a.TopLeft.x + a.Size.x
                && a.TopLeft.y < b.TopLeft.y + b.Size.y
                && b.TopLeft.y < a.TopLeft.y + a.Size.y;
        }

        private static int HashCommand(ref CheryRectCommand cmd)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + cmd.TopLeft.x.GetHashCode();
                hash = hash * 31 + cmd.TopLeft.y.GetHashCode();
                hash = hash * 31 + cmd.Size.x.GetHashCode();
                hash = hash * 31 + cmd.Size.y.GetHashCode();
                hash = hash * 31 + cmd.TopLeftColor.GetHashCode();
                hash = hash * 31 + cmd.TopRightColor.GetHashCode();
                hash = hash * 31 + cmd.BottomRightColor.GetHashCode();
                hash = hash * 31 + cmd.BottomLeftColor.GetHashCode();
                hash = hash * 31 + cmd.BorderTopLeftColor.GetHashCode();
                hash = hash * 31 + cmd.BorderTopRightColor.GetHashCode();
                hash = hash * 31 + cmd.BorderBottomRightColor.GetHashCode();
                hash = hash * 31 + cmd.BorderBottomLeftColor.GetHashCode();
                hash = hash * 31 + cmd.BorderThickness.GetHashCode();
                hash = hash * 31 + cmd.CornerRadius.GetHashCode();
                hash = hash * 31 + cmd.IsKeyRainCurve.GetHashCode();
                if (cmd.IsKeyRainCurve)
                {
                    hash = hash * 31 + cmd.FarColor.GetHashCode();
                    hash = hash * 31 + cmd.GradientEnabled.GetHashCode();
                    hash = hash * 31 + cmd.HeightMaskGradient.GetHashCode();
                    hash = hash * 31 + cmd.FadeMode.GetHashCode();
                    hash = hash * 31 + cmd.KeyY.GetHashCode();
                    hash = hash * 31 + cmd.MaxHeight.GetHashCode();
                    hash = hash * 31 + cmd.FadeHeight.GetHashCode();
                    hash = hash * 31 + cmd.FadePower.GetHashCode();
                    hash = hash * 31 + cmd.GradientHeight.GetHashCode();
                    hash = hash * 31 + cmd.GradientPower.GetHashCode();
                    hash = hash * 31 + cmd.HorizontalGradientEnabled.GetHashCode();
                    hash = hash * 31 + cmd.HorizontalColor.GetHashCode();
                }
                return hash;
            }
        }
    }

    internal class CheryRectBatchGraphic : MaskableGraphic
    {
        private readonly List<Vector2> _outer = new List<Vector2>(KeyViewerRoundedRectMesh.MaxCornerSegments * 4 + 4);
        private readonly List<Vector2> _inner = new List<Vector2>(KeyViewerRoundedRectMesh.MaxCornerSegments * 4 + 4);
        private readonly List<Vector2> _aaOuter = new List<Vector2>(KeyViewerRoundedRectMesh.MaxCornerSegments * 4 + 4);
        private readonly List<Vector2> _aaInner = new List<Vector2>(KeyViewerRoundedRectMesh.MaxCornerSegments * 4 + 4);

        // View over the owning CheryRectBatchGroup command list: this graphic only
        // triangulates the command indices assigned to it.
        private List<CheryRectCommand> _viewCommands;
        private List<int> _viewIndices;

        internal void SetView(List<CheryRectCommand> commands, List<int> indices)
        {
            _viewCommands = commands;
            _viewIndices = indices;
        }

        // Screen.width/height are native calls; EvaluateKeyRainCurveColor runs per
        // vertex, so both are sampled once per mesh rebuild instead.
        private static float _screenHalfWidth;
        private static float _screenHalfHeight;


        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_viewCommands == null || _viewIndices == null || _viewIndices.Count == 0) return;

            _screenHalfWidth = Screen.width * 0.5f;
            _screenHalfHeight = Screen.height * 0.5f;
            for (int i = 0; i < _viewIndices.Count; i++)
            {
                int index = _viewIndices[i];
                if (index < 0 || index >= _viewCommands.Count) continue;
                AddCommand(vh, _viewCommands[index]);
            }
        }

        private void AddCommand(VertexHelper vh, CheryRectCommand cmd)
        {
            Rect r = ToLocalRect(cmd.TopLeft, cmd.Size);
            if (r.width <= 0f || r.height <= 0f) return;

            if (cmd.IsKeyRainCurve)
            {
                AddKeyRainCurveFill(vh, r, cmd);
                return;
            }

            float radius = Mathf.Min(cmd.CornerRadius, Mathf.Min(r.width, r.height) * 0.5f);
            if (radius <= 0.01f)
            {
                if (HasVisibleFill(cmd))
                {
                    AddRectFill(vh, r, cmd.TopLeftColor, cmd.TopRightColor, cmd.BottomRightColor, cmd.BottomLeftColor);
                }
                if (cmd.BorderThickness > 0f && HasVisibleBorder(cmd))
                {
                    AddRectBorder(vh, r, Mathf.Min(cmd.BorderThickness, Mathf.Min(r.width, r.height) * 0.5f), cmd);
                }
                return;
            }

            bool hasFill = HasVisibleFill(cmd);
            bool hasBorder = cmd.BorderThickness > 0f && HasVisibleBorder(cmd);
            float borderInset = hasBorder
                ? Mathf.Min(cmd.BorderThickness, Mathf.Min(r.width, r.height) * 0.5f)
                : 0f;

            if (hasFill)
            {
                if (hasBorder)
                {
                    // Stop the fill at the opaque side of the border's AA band.
                    // Drawing two identical fringes on top of each other would
                    // over-cover the edge and make it look darker/thicker.
                    AddRoundedFillClipped(vh, r, radius, CalculateBorderAa(r, borderInset), cmd.TopLeftColor, cmd.TopRightColor, cmd.BottomRightColor, cmd.BottomLeftColor);
                }
                else
                {
                    AddRoundedFillAA(vh, r, radius, cmd.TopLeftColor, cmd.TopRightColor, cmd.BottomRightColor, cmd.BottomLeftColor);
                }
            }

            if (hasBorder)
            {
                AddRoundedBorderAA(vh, r, radius, borderInset, cmd);
            }
        }

        private static bool HasVisibleFill(CheryRectCommand cmd)
        {
            return cmd.TopLeftColor.a > 0f || cmd.TopRightColor.a > 0f || cmd.BottomRightColor.a > 0f || cmd.BottomLeftColor.a > 0f;
        }

        private static bool HasVisibleBorder(CheryRectCommand cmd)
        {
            return cmd.BorderTopLeftColor.a > 0f || cmd.BorderTopRightColor.a > 0f || cmd.BorderBottomRightColor.a > 0f || cmd.BorderBottomLeftColor.a > 0f;
        }

        private static Rect ToLocalRect(Vector2 topLeft, Vector2 size)
        {
            float width = Mathf.Max(1f, size.x);
            float height = Mathf.Max(1f, size.y);
            float left = Mathf.Round(topLeft.x) - _screenHalfWidth;
            float top = _screenHalfHeight - Mathf.Round(topLeft.y);
            return new Rect(left, top - height, width, height);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a *= Mathf.Clamp01(alpha);
            return color;
        }

        private static void AddRectFill(VertexHelper vh, Rect r, Color topColor, Color bottomColor)
        {
            AddRectFill(vh, r, topColor, topColor, bottomColor, bottomColor);
        }

        private static void AddRectFill(VertexHelper vh, Rect r, Color topLeftColor, Color topRightColor, Color bottomRightColor, Color bottomLeftColor)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector2(r.xMin, r.yMax), topLeftColor, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMax), topRightColor, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMin), bottomRightColor, Vector2.zero);
            vh.AddVert(new Vector2(r.xMin, r.yMin), bottomLeftColor, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private void AddKeyRainCurveFill(VertexHelper vh, Rect r, CheryRectCommand cmd)
        {
            float radius = Mathf.Min(cmd.CornerRadius, Mathf.Min(r.width, r.height) * 0.5f);
            if (radius > 0.01f)
            {
                int cornerSegments = KeyViewerRoundedRectMesh.CalculateCornerSegments(radius);
                float aa = Mathf.Min(KeyViewerRoundedRectMesh.AntiAliasHalfWidth, Mathf.Min(r.width, r.height) * 0.25f);
                Rect innerRect = KeyViewerRoundedRectMesh.Inset(r, aa);
                Rect outerRect = KeyViewerRoundedRectMesh.Expand(r, aa);
                KeyViewerRoundedRectMesh.Build(innerRect, Mathf.Max(0f, radius - aa), cornerSegments, _inner);
                KeyViewerRoundedRectMesh.Build(outerRect, radius + aa, cornerSegments, _aaOuter);

                int centerIndex = vh.currentVertCount;
                Vector2 center = r.center;
                vh.AddVert(center, EvaluateKeyRainCurveColor(center.y, r, cmd, 0.5f), Vector2.zero);

                for (int i = 0; i < _inner.Count; i++)
                {
                    Vector2 p = _inner[i];
                    float xT = r.width <= 0f ? 0f : Mathf.InverseLerp(r.xMin, r.xMax, p.x);
                    vh.AddVert(p, EvaluateKeyRainCurveColor(p.y, r, cmd, xT), Vector2.zero);
                }

                for (int i = 0; i < _inner.Count; i++)
                {
                    int next = i == _inner.Count - 1 ? 1 : i + 2;
                    vh.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + next);
                }

                AddKeyRainCurveFringe(vh, _aaOuter, _inner, r, cmd);
                return;
            }

            int segments = Mathf.Clamp(Mathf.CeilToInt(r.height / 18f), 4, 32);
            int previousLeft = -1;
            int previousRight = -1;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float y = Mathf.Lerp(r.yMin, r.yMax, t);
                float inset = CalculateRoundedInset(r, radius, y);
                float left = r.xMin + inset;
                float right = r.xMax - inset;
                if (right <= left)
                {
                    float centerX = (r.xMin + r.xMax) * 0.5f;
                    left = centerX;
                    right = centerX;
                }

                Color leftColor = EvaluateKeyRainCurveColor(y, r, cmd, false);
                Color rightColor = EvaluateKeyRainCurveColor(y, r, cmd, true);
                int leftIndex = vh.currentVertCount;
                vh.AddVert(new Vector2(left, y), leftColor, Vector2.zero);
                int rightIndex = vh.currentVertCount;
                vh.AddVert(new Vector2(right, y), rightColor, Vector2.zero);

                if (previousLeft >= 0)
                {
                    vh.AddTriangle(previousLeft, previousRight, rightIndex);
                    vh.AddTriangle(previousLeft, rightIndex, leftIndex);
                }

                previousLeft = leftIndex;
                previousRight = rightIndex;
            }
        }

        private static float CalculateRoundedInset(Rect r, float radius, float y)
        {
            if (radius <= 0.01f) return 0f;

            float inset = 0f;
            float bottomCenterY = r.yMin + radius;
            if (y < bottomCenterY)
            {
                float dy = y - bottomCenterY;
                inset = Mathf.Max(inset, radius - Mathf.Sqrt(Mathf.Max(0f, radius * radius - dy * dy)));
            }

            float topCenterY = r.yMax - radius;
            if (y > topCenterY)
            {
                float dy = y - topCenterY;
                inset = Mathf.Max(inset, radius - Mathf.Sqrt(Mathf.Max(0f, radius * radius - dy * dy)));
            }

            return Mathf.Min(inset, r.width * 0.5f);
        }

        private static Color EvaluateKeyRainCurveColor(float localY, Rect r, CheryRectCommand cmd, bool rightSide)
        {
            return EvaluateKeyRainCurveColor(localY, r, cmd, rightSide ? 1f : 0f);
        }

        private static Color EvaluateKeyRainCurveColor(float localY, Rect r, CheryRectCommand cmd, float xT)
        {
            float screenY = _screenHalfHeight - localY;
            float screenTop = cmd.TopLeft.y;
            float screenBottom = cmd.TopLeft.y + cmd.Size.y;

            Color color;
            if (cmd.GradientEnabled && cmd.HeightMaskGradient)
            {
                float gradientDistance = Mathf.Max(1f, cmd.MaxHeight * cmd.GradientHeight);
                float gradientT = Mathf.Pow(Mathf.Clamp01((cmd.KeyY - screenY) / gradientDistance), cmd.GradientPower);
                color = Color.Lerp(cmd.TopLeftColor, cmd.FarColor, gradientT);
            }
            else if (cmd.GradientEnabled)
            {
                float uvT = Mathf.Clamp01((screenBottom - screenY) / Mathf.Max(1f, screenBottom - screenTop));
                color = Color.Lerp(cmd.TopLeftColor, cmd.FarColor, uvT);
            }
            else
            {
                color = cmd.TopLeftColor;
            }

            if (cmd.HorizontalGradientEnabled)
            {
                Color horizontalColor = WithAlpha(cmd.HorizontalColor, color.a);
                float horizontalT = Mathf.Clamp01(xT);
                Color rightColor = cmd.GradientEnabled ? Color.Lerp(color, horizontalColor, 0.5f) : horizontalColor;
                color = Color.Lerp(color, rightColor, horizontalT);
            }

            if (cmd.FadeMode == 1)
            {
                float fadeDistance = Mathf.Max(1f, cmd.MaxHeight * cmd.FadeHeight);
                float alpha = Mathf.Pow(1f - Mathf.Clamp01((cmd.KeyY - screenY) / fadeDistance), cmd.FadePower);
                color.a *= alpha;
            }

            return color;
        }

        private static void AddRectBorder(VertexHelper vh, Rect r, float thickness, CheryRectCommand cmd)
        {
            AddRectFill(vh, new Rect(r.xMin, r.yMax - thickness, r.width, thickness), cmd.BorderTopLeftColor, cmd.BorderTopRightColor, EvaluateBorderColor(r.xMax, r.yMax - thickness, r, cmd), EvaluateBorderColor(r.xMin, r.yMax - thickness, r, cmd));
            AddRectFill(vh, new Rect(r.xMin, r.yMin, r.width, thickness), EvaluateBorderColor(r.xMin, r.yMin + thickness, r, cmd), EvaluateBorderColor(r.xMax, r.yMin + thickness, r, cmd), cmd.BorderBottomRightColor, cmd.BorderBottomLeftColor);
            AddRectFill(vh, new Rect(r.xMin, r.yMin + thickness, thickness, Mathf.Max(0f, r.height - thickness * 2f)), EvaluateBorderColor(r.xMin, r.yMax - thickness, r, cmd), EvaluateBorderColor(r.xMin + thickness, r.yMax - thickness, r, cmd), EvaluateBorderColor(r.xMin + thickness, r.yMin + thickness, r, cmd), EvaluateBorderColor(r.xMin, r.yMin + thickness, r, cmd));
            AddRectFill(vh, new Rect(r.xMax - thickness, r.yMin + thickness, thickness, Mathf.Max(0f, r.height - thickness * 2f)), EvaluateBorderColor(r.xMax - thickness, r.yMax - thickness, r, cmd), EvaluateBorderColor(r.xMax, r.yMax - thickness, r, cmd), EvaluateBorderColor(r.xMax, r.yMin + thickness, r, cmd), EvaluateBorderColor(r.xMax - thickness, r.yMin + thickness, r, cmd));
        }

        private static void AddRoundedFill(VertexHelper vh, List<Vector2> points, Rect r, Color topLeftColor, Color topRightColor, Color bottomRightColor, Color bottomLeftColor)
        {
            int centerIndex = vh.currentVertCount;
            Vector2 center = r.center;
            vh.AddVert(center, EvaluateFillColor(center, r, topLeftColor, topRightColor, bottomRightColor, bottomLeftColor), Vector2.zero);

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                vh.AddVert(p, EvaluateFillColor(p, r, topLeftColor, topRightColor, bottomRightColor, bottomLeftColor), Vector2.zero);
            }

            for (int i = 0; i < points.Count; i++)
            {
                int next = i == points.Count - 1 ? 1 : i + 2;
                vh.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + next);
            }
        }

        private void AddRoundedFillAA(VertexHelper vh, Rect r, float radius, Color topLeftColor, Color topRightColor, Color bottomRightColor, Color bottomLeftColor)
        {
            int cornerSegments = KeyViewerRoundedRectMesh.CalculateCornerSegments(radius);
            float aa = Mathf.Min(KeyViewerRoundedRectMesh.AntiAliasHalfWidth, Mathf.Min(r.width, r.height) * 0.25f);
            Rect innerRect = KeyViewerRoundedRectMesh.Inset(r, aa);
            Rect outerRect = KeyViewerRoundedRectMesh.Expand(r, aa);
            KeyViewerRoundedRectMesh.Build(innerRect, Mathf.Max(0f, radius - aa), cornerSegments, _inner);
            KeyViewerRoundedRectMesh.Build(outerRect, radius + aa, cornerSegments, _aaOuter);

            AddRoundedFill(vh, _inner, r, topLeftColor, topRightColor, bottomRightColor, bottomLeftColor);
            AddRoundedColorRing(vh, _aaOuter, _inner, r, topLeftColor, topRightColor, bottomRightColor, bottomLeftColor, 0f, 1f);
        }

        private void AddRoundedFillClipped(VertexHelper vh, Rect r, float radius, float inset, Color topLeftColor, Color topRightColor, Color bottomRightColor, Color bottomLeftColor)
        {
            int cornerSegments = KeyViewerRoundedRectMesh.CalculateCornerSegments(radius);
            Rect clippedRect = KeyViewerRoundedRectMesh.Inset(r, inset);
            KeyViewerRoundedRectMesh.Build(clippedRect, Mathf.Max(0f, radius - inset), cornerSegments, _inner);
            AddRoundedFill(vh, _inner, r, topLeftColor, topRightColor, bottomRightColor, bottomLeftColor);
        }

        private static float CalculateBorderAa(Rect r, float thickness)
        {
            return Mathf.Min(
                KeyViewerRoundedRectMesh.AntiAliasHalfWidth,
                Mathf.Min(thickness * 0.5f, Mathf.Min(r.width, r.height) * 0.25f));
        }

        private void AddRoundedBorderAA(VertexHelper vh, Rect r, float radius, float thickness, CheryRectCommand cmd)
        {
            if (thickness <= 0.01f) return;

            int cornerSegments = KeyViewerRoundedRectMesh.CalculateCornerSegments(radius);
            float aa = CalculateBorderAa(r, thickness);
            Rect innerRect = KeyViewerRoundedRectMesh.Inset(r, thickness);
            float innerRadius = Mathf.Max(0f, radius - thickness);

            if (innerRect.width <= 0.01f || innerRect.height <= 0.01f)
            {
                AddRoundedFillAA(vh, r, radius, cmd.BorderTopLeftColor, cmd.BorderTopRightColor, cmd.BorderBottomRightColor, cmd.BorderBottomLeftColor);
                return;
            }

            KeyViewerRoundedRectMesh.Build(KeyViewerRoundedRectMesh.Expand(r, aa), radius + aa, cornerSegments, _aaOuter);
            KeyViewerRoundedRectMesh.Build(KeyViewerRoundedRectMesh.Inset(r, aa), Mathf.Max(0f, radius - aa), cornerSegments, _outer);
            KeyViewerRoundedRectMesh.Build(KeyViewerRoundedRectMesh.Expand(innerRect, aa), innerRadius + aa, cornerSegments, _inner);
            KeyViewerRoundedRectMesh.Build(KeyViewerRoundedRectMesh.Inset(innerRect, aa), Mathf.Max(0f, innerRadius - aa), cornerSegments, _aaInner);

            AddRoundedColorRing(vh, _aaOuter, _outer, r, cmd.BorderTopLeftColor, cmd.BorderTopRightColor, cmd.BorderBottomRightColor, cmd.BorderBottomLeftColor, 0f, 1f);
            AddRoundedColorRing(vh, _outer, _inner, r, cmd.BorderTopLeftColor, cmd.BorderTopRightColor, cmd.BorderBottomRightColor, cmd.BorderBottomLeftColor, 1f, 1f);
            AddRoundedColorRing(vh, _inner, _aaInner, r, cmd.BorderTopLeftColor, cmd.BorderTopRightColor, cmd.BorderBottomRightColor, cmd.BorderBottomLeftColor, 1f, 0f);
        }

        private static void AddRoundedColorRing(VertexHelper vh, List<Vector2> outer, List<Vector2> inner, Rect r, Color topLeftColor, Color topRightColor, Color bottomRightColor, Color bottomLeftColor, float outerAlpha, float innerAlpha)
        {
            int count = Mathf.Min(outer.Count, inner.Count);
            if (count < 2) return;

            int outerStart = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                Color color = EvaluateFillColor(outer[i], r, topLeftColor, topRightColor, bottomRightColor, bottomLeftColor);
                color.a *= outerAlpha;
                vh.AddVert(outer[i], color, Vector2.zero);
            }

            int innerStart = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                Color color = EvaluateFillColor(inner[i], r, topLeftColor, topRightColor, bottomRightColor, bottomLeftColor);
                color.a *= innerAlpha;
                vh.AddVert(inner[i], color, Vector2.zero);
            }

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                vh.AddTriangle(outerStart + i, outerStart + next, innerStart + next);
                vh.AddTriangle(outerStart + i, innerStart + next, innerStart + i);
            }
        }

        private static void AddKeyRainCurveFringe(VertexHelper vh, List<Vector2> outer, List<Vector2> inner, Rect r, CheryRectCommand cmd)
        {
            int count = Mathf.Min(outer.Count, inner.Count);
            if (count < 2) return;

            int outerStart = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                Vector2 point = outer[i];
                float xT = r.width <= 0f ? 0f : Mathf.InverseLerp(r.xMin, r.xMax, point.x);
                Color color = EvaluateKeyRainCurveColor(point.y, r, cmd, xT);
                color.a = 0f;
                vh.AddVert(point, color, Vector2.zero);
            }

            int innerStart = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                Vector2 point = inner[i];
                float xT = r.width <= 0f ? 0f : Mathf.InverseLerp(r.xMin, r.xMax, point.x);
                vh.AddVert(point, EvaluateKeyRainCurveColor(point.y, r, cmd, xT), Vector2.zero);
            }

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                vh.AddTriangle(outerStart + i, outerStart + next, innerStart + next);
                vh.AddTriangle(outerStart + i, innerStart + next, innerStart + i);
            }
        }

        private static Color EvaluateFillColor(float y, Rect r, Color topColor, Color bottomColor)
        {
            float t = r.height <= 0f ? 1f : Mathf.InverseLerp(r.yMin, r.yMax, y);
            return Color.Lerp(bottomColor, topColor, t);
        }

        private static Color EvaluateFillColor(Vector2 point, Rect r, Color topLeftColor, Color topRightColor, Color bottomRightColor, Color bottomLeftColor)
        {
            float tx = r.width <= 0f ? 0f : Mathf.InverseLerp(r.xMin, r.xMax, point.x);
            float ty = r.height <= 0f ? 1f : Mathf.InverseLerp(r.yMin, r.yMax, point.y);
            Color bottom = Color.Lerp(bottomLeftColor, bottomRightColor, tx);
            Color top = Color.Lerp(topLeftColor, topRightColor, tx);
            return Color.Lerp(bottom, top, ty);
        }

        private static Color EvaluateBorderColor(float x, float y, Rect r, CheryRectCommand cmd)
        {
            return EvaluateFillColor(new Vector2(x, y), r, cmd.BorderTopLeftColor, cmd.BorderTopRightColor, cmd.BorderBottomRightColor, cmd.BorderBottomLeftColor);
        }

    }

    internal class CheryShadowBatchGraphic : MaskableGraphic
    {
        private const int TextureSize = 96;
        private const int TextureBorder = 32;
        private const float TextureSigma = 14f;

        private struct ShadowCommand
        {
            public Vector2 TopLeft;
            public Vector2 Size;
            public Color TopColor;
            public Color BottomColor;
            public float Softness;
        }

        private static Texture2D _shadowTexture;
        private readonly List<ShadowCommand> _commands = new List<ShadowCommand>(64);
        private int _lastCommandHash;
        private int _lastCommandCount = -1;
        private bool _lastHadCommands;

        public override Texture mainTexture
        {
            get { return EnsureShadowTexture(); }
        }

        public void BeginFrame()
        {
            _commands.Clear();
        }

        public void AddShadow(Vector2 topLeft, Vector2 size, Color topColor, Color bottomColor, float softness)
        {
            if (size.x <= 0f || size.y <= 0f) return;
            if (softness <= 0.01f) return;
            if (topColor.a <= 0f && bottomColor.a <= 0f) return;

            _commands.Add(new ShadowCommand
            {
                TopLeft = topLeft,
                Size = size,
                TopColor = topColor,
                BottomColor = bottomColor,
                Softness = Mathf.Max(1f, softness)
            });
        }

        public void EndFrame()
        {
            bool hasCommands = _commands.Count > 0;
            bool activeChanged = gameObject.activeSelf != hasCommands;
            if (gameObject.activeSelf != hasCommands)
            {
                gameObject.SetActive(hasCommands);
            }

            if (!hasCommands)
            {
                _lastHadCommands = false;
                _lastCommandCount = 0;
                _lastCommandHash = 0;
                return;
            }

            int commandHash = CalculateCommandHash();
            bool commandsChanged = activeChanged
                || !_lastHadCommands
                || _lastCommandCount != _commands.Count
                || _lastCommandHash != commandHash;
            if (commandsChanged)
            {
                _lastHadCommands = true;
                _lastCommandCount = _commands.Count;
                _lastCommandHash = commandHash;
                // The material and its static gaussian texture never change after
                // creation, so only the vertices need re-dirtying here.
                SetVerticesDirty();
            }
        }

        private int CalculateCommandHash()
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < _commands.Count; i++)
                {
                    ShadowCommand cmd = _commands[i];
                    hash = hash * 31 + cmd.TopLeft.x.GetHashCode();
                    hash = hash * 31 + cmd.TopLeft.y.GetHashCode();
                    hash = hash * 31 + cmd.Size.x.GetHashCode();
                    hash = hash * 31 + cmd.Size.y.GetHashCode();
                    hash = hash * 31 + cmd.TopColor.GetHashCode();
                    hash = hash * 31 + cmd.BottomColor.GetHashCode();
                    hash = hash * 31 + cmd.Softness.GetHashCode();
                }
                return hash;
            }
        }

        // Reused across AddCommand calls (main-thread only) to avoid four array
        // allocations per shadow per mesh rebuild.
        private static readonly float[] _sliceXs = new float[4];
        private static readonly float[] _sliceYs = new float[4];
        private static readonly float[] _sliceUs = new float[4];
        private static readonly float[] _sliceVs = new float[4];
        private static float _screenHalfWidth;
        private static float _screenHalfHeight;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_commands.Count == 0) return;

            _screenHalfWidth = Screen.width * 0.5f;
            _screenHalfHeight = Screen.height * 0.5f;
            for (int i = 0; i < _commands.Count; i++)
            {
                AddCommand(vh, _commands[i]);
            }
        }

        private void AddCommand(VertexHelper vh, ShadowCommand cmd)
        {
            Rect inner = ToLocalRect(cmd.TopLeft, cmd.Size);
            if (inner.width <= 0f || inner.height <= 0f) return;

            float border = Mathf.Max(1f, cmd.Softness);
            float uvBorder = TextureBorder / (float)TextureSize;

            float[] xs = _sliceXs;
            float[] ys = _sliceYs;
            float[] us = _sliceUs;
            float[] vs = _sliceVs;
            xs[0] = inner.xMin - border; xs[1] = inner.xMin; xs[2] = inner.xMax; xs[3] = inner.xMax + border;
            ys[0] = inner.yMin - border; ys[1] = inner.yMin; ys[2] = inner.yMax; ys[3] = inner.yMax + border;
            us[0] = 0f; us[1] = uvBorder; us[2] = 1f - uvBorder; us[3] = 1f;
            vs[0] = 0f; vs[1] = uvBorder; vs[2] = 1f - uvBorder; vs[3] = 1f;

            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    Vector2 bottomLeft = new Vector2(xs[x], ys[y]);
                    Vector2 bottomRight = new Vector2(xs[x + 1], ys[y]);
                    Vector2 topRight = new Vector2(xs[x + 1], ys[y + 1]);
                    Vector2 topLeft = new Vector2(xs[x], ys[y + 1]);

                    Vector2 uvBottomLeft = new Vector2(us[x], vs[y]);
                    Vector2 uvBottomRight = new Vector2(us[x + 1], vs[y]);
                    Vector2 uvTopRight = new Vector2(us[x + 1], vs[y + 1]);
                    Vector2 uvTopLeft = new Vector2(us[x], vs[y + 1]);

                    Color colorBottomLeft = EvaluateFillColor(Mathf.Clamp(bottomLeft.y, inner.yMin, inner.yMax), inner, cmd.TopColor, cmd.BottomColor);
                    Color colorBottomRight = EvaluateFillColor(Mathf.Clamp(bottomRight.y, inner.yMin, inner.yMax), inner, cmd.TopColor, cmd.BottomColor);
                    Color colorTopRight = EvaluateFillColor(Mathf.Clamp(topRight.y, inner.yMin, inner.yMax), inner, cmd.TopColor, cmd.BottomColor);
                    Color colorTopLeft = EvaluateFillColor(Mathf.Clamp(topLeft.y, inner.yMin, inner.yMax), inner, cmd.TopColor, cmd.BottomColor);

                    AddTexturedQuad(vh, topLeft, topRight, bottomRight, bottomLeft, uvTopLeft, uvTopRight, uvBottomRight, uvBottomLeft, colorTopLeft, colorTopRight, colorBottomRight, colorBottomLeft);
                }
            }
        }

        private static void AddTexturedQuad(VertexHelper vh, Vector2 topLeft, Vector2 topRight, Vector2 bottomRight, Vector2 bottomLeft, Vector2 uvTopLeft, Vector2 uvTopRight, Vector2 uvBottomRight, Vector2 uvBottomLeft, Color colorTopLeft, Color colorTopRight, Color colorBottomRight, Color colorBottomLeft)
        {
            int start = vh.currentVertCount;
            vh.AddVert(topLeft, colorTopLeft, uvTopLeft);
            vh.AddVert(topRight, colorTopRight, uvTopRight);
            vh.AddVert(bottomRight, colorBottomRight, uvBottomRight);
            vh.AddVert(bottomLeft, colorBottomLeft, uvBottomLeft);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static Rect ToLocalRect(Vector2 topLeft, Vector2 size)
        {
            float width = Mathf.Max(1f, size.x);
            float height = Mathf.Max(1f, size.y);
            float left = Mathf.Round(topLeft.x) - _screenHalfWidth;
            float top = _screenHalfHeight - Mathf.Round(topLeft.y);
            return new Rect(left, top - height, width, height);
        }

        private static Color EvaluateFillColor(float y, Rect r, Color topColor, Color bottomColor)
        {
            float t = r.height <= 0f ? 1f : Mathf.InverseLerp(r.yMin, r.yMax, y);
            return Color.Lerp(bottomColor, topColor, t);
        }

        private static Texture2D EnsureShadowTexture()
        {
            if (_shadowTexture != null) return _shadowTexture;

            _shadowTexture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            _shadowTexture.name = "CheryTools_KeyRain_GaussianShadow";
            _shadowTexture.wrapMode = TextureWrapMode.Clamp;
            _shadowTexture.filterMode = FilterMode.Bilinear;
            UnityEngine.Object.DontDestroyOnLoad(_shadowTexture);

            Color[] pixels = new Color[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float alpha = GaussianBoxCoverage(x + 0.5f, TextureBorder, TextureSize - TextureBorder, TextureSigma)
                        * GaussianBoxCoverage(y + 0.5f, TextureBorder, TextureSize - TextureBorder, TextureSigma);
                    pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                }
            }
            _shadowTexture.SetPixels(pixels);
            _shadowTexture.Apply(false, true);
            return _shadowTexture;
        }

        private static float GaussianBoxCoverage(float point, float min, float max, float sigma)
        {
            float left = (point - min) / sigma;
            float right = (point - max) / sigma;
            return Mathf.Clamp01(NormalCdf(left) - NormalCdf(right));
        }

        private static float NormalCdf(float x)
        {
            return 0.5f * (1f + ErfApprox(x * 0.70710678118f));
        }

        private static float ErfApprox(float x)
        {
            float sign = x < 0f ? -1f : 1f;
            x = Mathf.Abs(x);
            float t = 1f / (1f + 0.3275911f * x);
            float y = 1f - (((((1.061405429f * t - 1.453152027f) * t) + 1.421413741f) * t - 0.284496736f) * t + 0.254829592f) * t * Mathf.Exp(-x * x);
            return sign * y;
        }
    }
}
