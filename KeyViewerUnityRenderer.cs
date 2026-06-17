using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CheryTools
{
    internal static class KeyViewerUnityRenderer
    {
        private const int CanvasSortingOrder = RenderDepth.EditOverlaySortingOrder;

        private static GameObject _root;
        private static RectTransform _rootRect;
        private static int _frameId;

        private static readonly Dictionary<int, RectTransform> _layerRoots = new Dictionary<int, RectTransform>();
        private static readonly Dictionary<int, CheryRectBatchGraphic> _rainBatches = new Dictionary<int, CheryRectBatchGraphic>();
        private static readonly Dictionary<int, CheryRectBatchGraphic> _rectBatches = new Dictionary<int, CheryRectBatchGraphic>();
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
            HideUnused(_images);
        }

        public static void HideAll()
        {
            foreach (var pair in _rainBatches)
            {
                if (pair.Value != null) pair.Value.gameObject.SetActive(false);
            }
            foreach (var pair in _rectBatches)
            {
                if (pair.Value != null) pair.Value.gameObject.SetActive(false);
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

        public static void DrawRect(string id, Vector2 topLeft, Vector2 size, uint fillColor, uint borderColor, float borderThickness, float cornerRadius, int sortingOrder = CanvasSortingOrder)
        {
            if (!EnsureReady()) return;
            CheryRectBatchGraphic batch = GetRectBatch(sortingOrder);
            if (batch == null) return;
            batch.AddRect(topLeft, size, ToColor(fillColor), ToColor(fillColor), ToColor(borderColor), borderThickness, cornerRadius);
        }

        public static void DrawGradientRect(string id, Vector2 topLeft, Vector2 size, uint topColor, uint bottomColor, int sortingOrder = CanvasSortingOrder)
        {
            DrawGradientRect(id, topLeft, size, topColor, bottomColor, 0f, sortingOrder);
        }

        public static void DrawGradientRect(string id, Vector2 topLeft, Vector2 size, uint topColor, uint bottomColor, float cornerRadius, int sortingOrder = CanvasSortingOrder)
        {
            if (!EnsureReady()) return;
            CheryRectBatchGraphic batch = GetRainBatch(sortingOrder);
            if (batch == null) return;
            batch.AddRect(topLeft, size, ToColor(topColor), ToColor(bottomColor), Color.clear, 0f, cornerRadius);
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

        private static CheryRectBatchGraphic GetRainBatch(int sortingOrder)
        {
            if (_rainBatches.TryGetValue(sortingOrder, out CheryRectBatchGraphic batch) && batch != null)
            {
                return batch;
            }

            batch = CreateRectBatch("KV_Rain_Batch_" + sortingOrder.ToString(), sortingOrder);
            _rainBatches[sortingOrder] = batch;
            return batch;
        }

        private static CheryRectBatchGraphic GetRectBatch(int sortingOrder)
        {
            if (_rectBatches.TryGetValue(sortingOrder, out CheryRectBatchGraphic batch) && batch != null)
            {
                return batch;
            }

            batch = CreateRectBatch("KV_Rect_Batch_" + sortingOrder.ToString(), sortingOrder);
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

        private static CheryRectBatchGraphic CreateRectBatch(string name, int sortingOrder)
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

        private static void HideUnused<T>(Dictionary<string, T> items) where T : Component
        {
            foreach (var pair in items)
            {
                T item = pair.Value;
                if (item == null) continue;
                GameObject go = item.gameObject;
                if (go.activeSelf && (!_frameMarks.TryGetValue(go, out int mark) || mark != _frameId))
                {
                    go.SetActive(false);
                }
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

    internal class KeyViewerImageGraphic : MaskableGraphic
    {
        private const int CornerSegments = 8;

        private Texture _texture;
        private float _alpha = 1f;
        private float _cornerRadius;
        private Vector2 _uvMin = Vector2.zero;
        private Vector2 _uvMax = Vector2.one;
        private readonly List<Vector2> _points = new List<Vector2>(CornerSegments * 4 + 4);

        public override Texture mainTexture
        {
            get { return _texture != null ? _texture : s_WhiteTexture; }
        }

        public void SetImage(Texture texture, float alpha, float cornerRadius)
        {
            SetImage(texture, alpha, cornerRadius, Vector2.zero, Vector2.one);
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
            BuildRoundedRect(r, radius, _points);
            AddTexturedFill(vh, _points, r, new Color(1f, 1f, 1f, _alpha), _uvMin, _uvMax);
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

        private static Vector2 ToUv(Vector2 point, Rect r, Vector2 uvMin, Vector2 uvMax)
        {
            float u = r.width <= 0f ? 0f : Mathf.InverseLerp(r.xMin, r.xMax, point.x);
            float v = r.height <= 0f ? 0f : Mathf.InverseLerp(r.yMin, r.yMax, point.y);
            return new Vector2(Mathf.Lerp(uvMin.x, uvMax.x, u), Mathf.Lerp(uvMin.y, uvMax.y, v));
        }

        private static void BuildRoundedRect(Rect r, float radius, List<Vector2> points)
        {
            points.Clear();
            if (radius <= 0.01f)
            {
                points.Add(new Vector2(r.xMin, r.yMax));
                points.Add(new Vector2(r.xMax, r.yMax));
                points.Add(new Vector2(r.xMax, r.yMin));
                points.Add(new Vector2(r.xMin, r.yMin));
                return;
            }

            AddArc(points, new Vector2(r.xMax - radius, r.yMax - radius), radius, 90f, 0f);
            AddArc(points, new Vector2(r.xMax - radius, r.yMin + radius), radius, 0f, -90f);
            AddArc(points, new Vector2(r.xMin + radius, r.yMin + radius), radius, -90f, -180f);
            AddArc(points, new Vector2(r.xMin + radius, r.yMax - radius), radius, 180f, 90f);
        }

        private static void AddArc(List<Vector2> points, Vector2 center, float radius, float startDeg, float endDeg)
        {
            for (int i = 0; i <= CornerSegments; i++)
            {
                float t = i / (float)CornerSegments;
                float deg = Mathf.Lerp(startDeg, endDeg, t);
                float rad = deg * Mathf.Deg2Rad;
                points.Add(new Vector2(center.x + Mathf.Cos(rad) * radius, center.y + Mathf.Sin(rad) * radius));
            }
        }
    }

    internal class CheryRectBatchGraphic : MaskableGraphic
    {
        private const int CornerSegments = 8;

        private struct RectCommand
        {
            public Vector2 TopLeft;
            public Vector2 Size;
            public Color TopColor;
            public Color BottomColor;
            public Color BorderColor;
            public float BorderThickness;
            public float CornerRadius;
            public bool IsSoftShadow;
            public float Softness;
        }

        private readonly List<RectCommand> _commands = new List<RectCommand>(64);
        private readonly List<Vector2> _outer = new List<Vector2>(CornerSegments * 4 + 4);
        private readonly List<Vector2> _inner = new List<Vector2>(CornerSegments * 4 + 4);
        private int _lastCommandHash;
        private int _lastCommandCount = -1;
        private bool _lastHadCommands;

        public void BeginFrame()
        {
            _commands.Clear();
        }

        public void AddRect(Vector2 topLeft, Vector2 size, Color topColor, Color bottomColor, Color borderColor, float borderThickness, float cornerRadius)
        {
            if (size.x <= 0f || size.y <= 0f) return;
            _commands.Add(new RectCommand
            {
                TopLeft = topLeft,
                Size = size,
                TopColor = topColor,
                BottomColor = bottomColor,
                BorderColor = borderColor,
                BorderThickness = Mathf.Max(0f, borderThickness),
                CornerRadius = Mathf.Max(0f, cornerRadius),
                IsSoftShadow = false,
                Softness = 0f
            });
        }

        public void AddSoftShadowRect(Vector2 topLeft, Vector2 size, Color topColor, Color bottomColor, float softness)
        {
            if (size.x <= 0f || size.y <= 0f) return;
            if (topColor.a <= 0f && bottomColor.a <= 0f) return;
            _commands.Add(new RectCommand
            {
                TopLeft = topLeft,
                Size = size,
                TopColor = topColor,
                BottomColor = bottomColor,
                BorderColor = Color.clear,
                BorderThickness = 0f,
                CornerRadius = 0f,
                IsSoftShadow = true,
                Softness = Mathf.Max(0f, softness)
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
                    RectCommand cmd = _commands[i];
                    hash = hash * 31 + cmd.TopLeft.x.GetHashCode();
                    hash = hash * 31 + cmd.TopLeft.y.GetHashCode();
                    hash = hash * 31 + cmd.Size.x.GetHashCode();
                    hash = hash * 31 + cmd.Size.y.GetHashCode();
                    hash = hash * 31 + cmd.TopColor.GetHashCode();
                    hash = hash * 31 + cmd.BottomColor.GetHashCode();
                    hash = hash * 31 + cmd.BorderColor.GetHashCode();
                    hash = hash * 31 + cmd.BorderThickness.GetHashCode();
                    hash = hash * 31 + cmd.CornerRadius.GetHashCode();
                    hash = hash * 31 + cmd.IsSoftShadow.GetHashCode();
                    hash = hash * 31 + cmd.Softness.GetHashCode();
                }
                return hash;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_commands.Count == 0) return;

            for (int i = 0; i < _commands.Count; i++)
            {
                AddCommand(vh, _commands[i]);
            }
        }

        private void AddCommand(VertexHelper vh, RectCommand cmd)
        {
            Rect r = ToLocalRect(cmd.TopLeft, cmd.Size);
            if (r.width <= 0f || r.height <= 0f) return;

            if (cmd.IsSoftShadow)
            {
                AddSoftShadowFill(vh, r, cmd.TopColor, cmd.BottomColor, cmd.Softness);
                return;
            }

            float radius = Mathf.Min(cmd.CornerRadius, Mathf.Min(r.width, r.height) * 0.5f);
            if (radius <= 0.01f)
            {
                if (cmd.TopColor.a > 0f || cmd.BottomColor.a > 0f)
                {
                    AddRectFill(vh, r, cmd.TopColor, cmd.BottomColor);
                }
                if (cmd.BorderThickness > 0f && cmd.BorderColor.a > 0f)
                {
                    AddRectBorder(vh, r, Mathf.Min(cmd.BorderThickness, Mathf.Min(r.width, r.height) * 0.5f), cmd.BorderColor);
                }
                return;
            }

            BuildRoundedRect(r, radius, _outer);

            if (cmd.TopColor.a > 0f || cmd.BottomColor.a > 0f)
            {
                AddRoundedFill(vh, _outer, r, cmd.TopColor, cmd.BottomColor);
            }

            if (cmd.BorderThickness > 0f && cmd.BorderColor.a > 0f)
            {
                float inset = Mathf.Min(cmd.BorderThickness, Mathf.Min(r.width, r.height) * 0.5f);
                Rect innerRect = new Rect(r.xMin + inset, r.yMin + inset, Mathf.Max(0f, r.width - inset * 2f), Mathf.Max(0f, r.height - inset * 2f));
                BuildRoundedRect(innerRect, Mathf.Max(0f, radius - inset), _inner);
                AddRoundedBorder(vh, _outer, _inner, cmd.BorderColor);
            }
        }

        private static Rect ToLocalRect(Vector2 topLeft, Vector2 size)
        {
            float width = Mathf.Max(1f, size.x);
            float height = Mathf.Max(1f, size.y);
            float left = Mathf.Round(topLeft.x) - Screen.width * 0.5f;
            float top = Screen.height * 0.5f - Mathf.Round(topLeft.y);
            return new Rect(left, top - height, width, height);
        }

        private static void AddSoftShadowFill(VertexHelper vh, Rect r, Color topColor, Color bottomColor, float softness)
        {
            if (softness <= 0.01f)
            {
                AddRectFill(vh, r, topColor, bottomColor);
                return;
            }

            float blur = Mathf.Max(1f, softness);
            Color clearTop = WithAlpha(topColor, 0f);
            Color clearBottom = WithAlpha(bottomColor, 0f);

            AddRectFill(vh, r, topColor, bottomColor);

            AddGradientQuad(
                vh,
                new Vector2(r.xMin - blur, r.yMax),
                new Vector2(r.xMin, r.yMax),
                new Vector2(r.xMin, r.yMin),
                new Vector2(r.xMin - blur, r.yMin),
                clearTop,
                topColor,
                bottomColor,
                clearBottom);
            AddGradientQuad(
                vh,
                new Vector2(r.xMax, r.yMax),
                new Vector2(r.xMax + blur, r.yMax),
                new Vector2(r.xMax + blur, r.yMin),
                new Vector2(r.xMax, r.yMin),
                topColor,
                clearTop,
                clearBottom,
                bottomColor);
            AddGradientQuad(
                vh,
                new Vector2(r.xMin, r.yMax + blur),
                new Vector2(r.xMax, r.yMax + blur),
                new Vector2(r.xMax, r.yMax),
                new Vector2(r.xMin, r.yMax),
                clearTop,
                clearTop,
                topColor,
                topColor);
            AddGradientQuad(
                vh,
                new Vector2(r.xMin, r.yMin),
                new Vector2(r.xMax, r.yMin),
                new Vector2(r.xMax, r.yMin - blur),
                new Vector2(r.xMin, r.yMin - blur),
                bottomColor,
                bottomColor,
                clearBottom,
                clearBottom);

            AddCornerShadow(vh, new Vector2(r.xMin, r.yMax), blur, 90f, 180f, topColor);
            AddCornerShadow(vh, new Vector2(r.xMax, r.yMax), blur, 0f, 90f, topColor);
            AddCornerShadow(vh, new Vector2(r.xMax, r.yMin), blur, -90f, 0f, bottomColor);
            AddCornerShadow(vh, new Vector2(r.xMin, r.yMin), blur, -180f, -90f, bottomColor);
        }

        private static void AddIndexedQuad(VertexHelper vh, int topLeft, int topRight, int bottomRight, int bottomLeft)
        {
            vh.AddTriangle(topLeft, topRight, bottomRight);
            vh.AddTriangle(topLeft, bottomRight, bottomLeft);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a *= Mathf.Clamp01(alpha);
            return color;
        }

        private static void AddGradientQuad(VertexHelper vh, Vector2 topLeft, Vector2 topRight, Vector2 bottomRight, Vector2 bottomLeft, Color colorTopLeft, Color colorTopRight, Color colorBottomRight, Color colorBottomLeft)
        {
            int start = vh.currentVertCount;
            vh.AddVert(topLeft, colorTopLeft, Vector2.zero);
            vh.AddVert(topRight, colorTopRight, Vector2.zero);
            vh.AddVert(bottomRight, colorBottomRight, Vector2.zero);
            vh.AddVert(bottomLeft, colorBottomLeft, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddCornerShadow(VertexHelper vh, Vector2 center, float radius, float startDeg, float endDeg, Color innerColor)
        {
            const int Segments = 10;
            Color outerColor = WithAlpha(innerColor, 0f);
            int centerIndex = vh.currentVertCount;
            vh.AddVert(center, innerColor, Vector2.zero);

            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments;
                float deg = Mathf.Lerp(startDeg, endDeg, t);
                float rad = deg * Mathf.Deg2Rad;
                Vector2 point = new Vector2(center.x + Mathf.Cos(rad) * radius, center.y + Mathf.Sin(rad) * radius);
                vh.AddVert(point, outerColor, Vector2.zero);
            }

            for (int i = 0; i < Segments; i++)
            {
                vh.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + i + 2);
            }
        }

        private static void AddRectFill(VertexHelper vh, Rect r, Color topColor, Color bottomColor)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector2(r.xMin, r.yMax), topColor, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMax), topColor, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMin), bottomColor, Vector2.zero);
            vh.AddVert(new Vector2(r.xMin, r.yMin), bottomColor, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddRectBorder(VertexHelper vh, Rect r, float thickness, Color color)
        {
            AddSolidQuad(vh, new Rect(r.xMin, r.yMax - thickness, r.width, thickness), color);
            AddSolidQuad(vh, new Rect(r.xMin, r.yMin, r.width, thickness), color);
            AddSolidQuad(vh, new Rect(r.xMin, r.yMin + thickness, thickness, Mathf.Max(0f, r.height - thickness * 2f)), color);
            AddSolidQuad(vh, new Rect(r.xMax - thickness, r.yMin + thickness, thickness, Mathf.Max(0f, r.height - thickness * 2f)), color);
        }

        private static void AddSolidQuad(VertexHelper vh, Rect r, Color color)
        {
            if (r.width <= 0f || r.height <= 0f) return;
            int start = vh.currentVertCount;
            vh.AddVert(new Vector2(r.xMin, r.yMax), color, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMax), color, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMin), color, Vector2.zero);
            vh.AddVert(new Vector2(r.xMin, r.yMin), color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddRoundedFill(VertexHelper vh, List<Vector2> points, Rect r, Color topColor, Color bottomColor)
        {
            int centerIndex = vh.currentVertCount;
            Vector2 center = r.center;
            vh.AddVert(center, EvaluateFillColor(center.y, r, topColor, bottomColor), Vector2.zero);

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                vh.AddVert(p, EvaluateFillColor(p.y, r, topColor, bottomColor), Vector2.zero);
            }

            for (int i = 0; i < points.Count; i++)
            {
                int next = i == points.Count - 1 ? 1 : i + 2;
                vh.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + next);
            }
        }

        private static void AddRoundedBorder(VertexHelper vh, List<Vector2> outer, List<Vector2> inner, Color color)
        {
            int count = Mathf.Min(outer.Count, inner.Count);
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                int start = vh.currentVertCount;

                vh.AddVert(outer[i], color, Vector2.zero);
                vh.AddVert(outer[next], color, Vector2.zero);
                vh.AddVert(inner[next], color, Vector2.zero);
                vh.AddVert(inner[i], color, Vector2.zero);

                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }

        private static Color EvaluateFillColor(float y, Rect r, Color topColor, Color bottomColor)
        {
            float t = r.height <= 0f ? 1f : Mathf.InverseLerp(r.yMin, r.yMax, y);
            return Color.Lerp(bottomColor, topColor, t);
        }

        private static void BuildRoundedRect(Rect r, float radius, List<Vector2> points)
        {
            points.Clear();
            if (radius <= 0.01f)
            {
                points.Add(new Vector2(r.xMin, r.yMax));
                points.Add(new Vector2(r.xMax, r.yMax));
                points.Add(new Vector2(r.xMax, r.yMin));
                points.Add(new Vector2(r.xMin, r.yMin));
                return;
            }

            AddArc(points, new Vector2(r.xMax - radius, r.yMax - radius), radius, 90f, 0f);
            AddArc(points, new Vector2(r.xMax - radius, r.yMin + radius), radius, 0f, -90f);
            AddArc(points, new Vector2(r.xMin + radius, r.yMin + radius), radius, -90f, -180f);
            AddArc(points, new Vector2(r.xMin + radius, r.yMax - radius), radius, 180f, 90f);
        }

        private static void AddArc(List<Vector2> points, Vector2 center, float radius, float startDeg, float endDeg)
        {
            for (int i = 0; i <= CornerSegments; i++)
            {
                float t = i / (float)CornerSegments;
                float deg = Mathf.Lerp(startDeg, endDeg, t);
                float rad = deg * Mathf.Deg2Rad;
                points.Add(new Vector2(center.x + Mathf.Cos(rad) * radius, center.y + Mathf.Sin(rad) * radius));
            }
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
                SetVerticesDirty();
                SetMaterialDirty();
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

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_commands.Count == 0) return;

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

            float[] xs = new float[] { inner.xMin - border, inner.xMin, inner.xMax, inner.xMax + border };
            float[] ys = new float[] { inner.yMin - border, inner.yMin, inner.yMax, inner.yMax + border };
            float[] us = new float[] { 0f, uvBorder, 1f - uvBorder, 1f };
            float[] vs = new float[] { 0f, uvBorder, 1f - uvBorder, 1f };

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
            float left = Mathf.Round(topLeft.x) - Screen.width * 0.5f;
            float top = Screen.height * 0.5f - Mathf.Round(topLeft.y);
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

    internal class KeyViewerRectGraphic : MaskableGraphic
    {
        private const int CornerSegments = 8;

        private Color _topColor = Color.white;
        private Color _bottomColor = Color.white;
        private Color _borderColor = Color.clear;
        private float _borderThickness;
        private float _cornerRadius;

        public void SetStyle(Color topColor, Color bottomColor, Color borderColor, float borderThickness, float cornerRadius)
        {
            float safeBorderThickness = Mathf.Max(0f, borderThickness);
            float safeCornerRadius = Mathf.Max(0f, cornerRadius);
            if (_topColor == topColor
                && _bottomColor == bottomColor
                && _borderColor == borderColor
                && Mathf.Approximately(_borderThickness, safeBorderThickness)
                && Mathf.Approximately(_cornerRadius, safeCornerRadius))
            {
                return;
            }

            _topColor = topColor;
            _bottomColor = bottomColor;
            _borderColor = borderColor;
            _borderThickness = safeBorderThickness;
            _cornerRadius = safeCornerRadius;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect r = rectTransform.rect;
            if (r.width <= 0f || r.height <= 0f) return;

            float radius = Mathf.Min(_cornerRadius, Mathf.Min(r.width, r.height) * 0.5f);
            List<Vector2> outer = BuildRoundedRect(r, radius);

            if (_topColor.a > 0f || _bottomColor.a > 0f)
            {
                AddFill(vh, outer, r);
            }

            if (_borderThickness > 0f && _borderColor.a > 0f)
            {
                float inset = Mathf.Min(_borderThickness, Mathf.Min(r.width, r.height) * 0.5f);
                Rect innerRect = new Rect(r.xMin + inset, r.yMin + inset, Mathf.Max(0f, r.width - inset * 2f), Mathf.Max(0f, r.height - inset * 2f));
                List<Vector2> inner = BuildRoundedRect(innerRect, Mathf.Max(0f, radius - inset));
                AddBorder(vh, outer, inner);
            }
        }

        private void AddFill(VertexHelper vh, List<Vector2> points, Rect r)
        {
            int centerIndex = vh.currentVertCount;
            Vector2 center = r.center;
            vh.AddVert(center, EvaluateFillColor(center.y, r), Vector2.zero);

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                vh.AddVert(p, EvaluateFillColor(p.y, r), Vector2.zero);
            }

            for (int i = 0; i < points.Count; i++)
            {
                int next = i == points.Count - 1 ? 1 : i + 2;
                vh.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + next);
            }
        }

        private void AddBorder(VertexHelper vh, List<Vector2> outer, List<Vector2> inner)
        {
            int count = Mathf.Min(outer.Count, inner.Count);
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                int start = vh.currentVertCount;

                vh.AddVert(outer[i], _borderColor, Vector2.zero);
                vh.AddVert(outer[next], _borderColor, Vector2.zero);
                vh.AddVert(inner[next], _borderColor, Vector2.zero);
                vh.AddVert(inner[i], _borderColor, Vector2.zero);

                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }

        private Color EvaluateFillColor(float y, Rect r)
        {
            float t = r.height <= 0f ? 1f : Mathf.InverseLerp(r.yMin, r.yMax, y);
            return Color.Lerp(_bottomColor, _topColor, t);
        }

        private static List<Vector2> BuildRoundedRect(Rect r, float radius)
        {
            List<Vector2> points = new List<Vector2>(CornerSegments * 4 + 4);

            if (radius <= 0.01f)
            {
                points.Add(new Vector2(r.xMin, r.yMax));
                points.Add(new Vector2(r.xMax, r.yMax));
                points.Add(new Vector2(r.xMax, r.yMin));
                points.Add(new Vector2(r.xMin, r.yMin));
                return points;
            }

            AddArc(points, new Vector2(r.xMax - radius, r.yMax - radius), radius, 90f, 0f);
            AddArc(points, new Vector2(r.xMax - radius, r.yMin + radius), radius, 0f, -90f);
            AddArc(points, new Vector2(r.xMin + radius, r.yMin + radius), radius, -90f, -180f);
            AddArc(points, new Vector2(r.xMin + radius, r.yMax - radius), radius, 180f, 90f);
            return points;
        }

        private static void AddArc(List<Vector2> points, Vector2 center, float radius, float startDeg, float endDeg)
        {
            for (int i = 0; i <= CornerSegments; i++)
            {
                float t = i / (float)CornerSegments;
                float deg = Mathf.Lerp(startDeg, endDeg, t);
                float rad = deg * Mathf.Deg2Rad;
                points.Add(new Vector2(center.x + Mathf.Cos(rad) * radius, center.y + Mathf.Sin(rad) * radius));
            }
        }
    }
}
