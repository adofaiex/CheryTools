using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CheryTools
{
    internal static class OverlayerUnityRenderer
    {
        private const int CanvasSortingOrder = RenderDepth.EditOverlaySortingOrder;

        private static GameObject _root;
        private static RectTransform _rootRect;
        private static int _frameId;

        private static readonly Dictionary<int, RectTransform> _layerRoots = new Dictionary<int, RectTransform>();
        private static readonly Dictionary<string, OverlayerImageGraphic> _images = new Dictionary<string, OverlayerImageGraphic>();
        private static readonly Dictionary<string, int> _imageSortingOrders = new Dictionary<string, int>();
        private static readonly Dictionary<int, CheryRectBatchGraphic> _rectBatches = new Dictionary<int, CheryRectBatchGraphic>();
        private static readonly Dictionary<GameObject, int> _frameMarks = new Dictionary<GameObject, int>();

        public static void BeginFrame()
        {
            _frameId++;
            EnsureReady();
            foreach (var pair in _rectBatches)
            {
                if (pair.Value != null) pair.Value.BeginFrame();
            }
        }

        public static void EndFrame()
        {
            HideUnused(_images);
            foreach (var pair in _rectBatches)
            {
                if (pair.Value != null) pair.Value.EndFrame();
            }
        }

        public static void HideAll()
        {
            foreach (var pair in _images)
            {
                if (pair.Value != null) pair.Value.gameObject.SetActive(false);
            }
            foreach (var pair in _rectBatches)
            {
                if (pair.Value != null) pair.Value.gameObject.SetActive(false);
            }
        }

        public static void Shutdown()
        {
            _images.Clear();
            _imageSortingOrders.Clear();
            _rectBatches.Clear();
            _layerRoots.Clear();
            _frameMarks.Clear();

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }

            _rootRect = null;
        }

        public static void DrawImageQuad(string id, Texture texture, Vector2 topLeft, Vector2 size, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float alpha, int sortingOrder = CanvasSortingOrder)
        {
            DrawImageQuad(id, texture, topLeft, size, p1, p2, p3, p4, alpha, Vector2.zero, Vector2.one, sortingOrder);
        }

        public static void DrawImageQuad(string id, Texture texture, Vector2 topLeft, Vector2 size, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float alpha, Vector2 uvMin, Vector2 uvMax, int sortingOrder = CanvasSortingOrder)
        {
            if (!EnsureReady() || texture == null) return;
            OverlayerImageGraphic image = GetImage(id, sortingOrder);
            if (image == null) return;

            SetRectTransform(image.rectTransform, topLeft, size);
            image.SetImage(texture, p1 - topLeft, p2 - topLeft, p3 - topLeft, p4 - topLeft, alpha, uvMin, uvMax);
            Mark(image.gameObject);
        }

        public static void DrawOutlineRect(string id, Vector2 topLeft, Vector2 size, uint borderColor, float thickness, float cornerRadius = 0f, int sortingOrder = CanvasSortingOrder)
        {
            if (!EnsureReady()) return;
            CheryRectBatchGraphic batch = GetRectBatch(sortingOrder);
            if (batch == null) return;
            batch.AddRect(topLeft, size, Color.clear, Color.clear, ToColor(borderColor), Mathf.Max(1f, thickness), Mathf.Max(0f, cornerRadius));
        }

        public static void DrawFilledRect(string id, Vector2 topLeft, Vector2 size, uint fillColor, float cornerRadius = 0f, int sortingOrder = CanvasSortingOrder)
        {
            if (!EnsureReady()) return;
            CheryRectBatchGraphic batch = GetRectBatch(sortingOrder);
            if (batch == null) return;
            Color color = ToColor(fillColor);
            batch.AddRect(topLeft, size, color, color, Color.clear, 0f, Mathf.Max(0f, cornerRadius));
        }

        private static bool EnsureReady()
        {
            if (_root != null && _rootRect != null) return true;

            try
            {
                _root = new GameObject("CheryTools_OV_Unity_Root", typeof(RectTransform));
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
                    Main.Logger.Log("[CheryTools] OV Unity renderer init failed: " + ex.Message);
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

            GameObject go = new GameObject("OV_Layer_" + sortingOrder.ToString(), typeof(RectTransform));
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

        private static OverlayerImageGraphic GetImage(string id, int sortingOrder)
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

            GameObject go = new GameObject("OV_Image_" + id, typeof(RectTransform));
            go.transform.SetParent(layerRoot, false);
            image = go.AddComponent<OverlayerImageGraphic>();
            image.raycastTarget = false;
            _images[id] = image;
            _imageSortingOrders[id] = sortingOrder;
            return image;
        }

        private static CheryRectBatchGraphic GetRectBatch(int sortingOrder)
        {
            if (_rectBatches.TryGetValue(sortingOrder, out CheryRectBatchGraphic batch) && batch != null)
            {
                return batch;
            }

            RectTransform layerRoot = GetLayerRoot(sortingOrder);
            if (layerRoot == null) return null;

            GameObject go = new GameObject("OV_Rect_Batch_" + sortingOrder.ToString(), typeof(RectTransform));
            go.transform.SetParent(layerRoot, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            batch = go.AddComponent<CheryRectBatchGraphic>();
            batch.raycastTarget = false;
            _rectBatches[sortingOrder] = batch;
            return batch;
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
            if (rt.localRotation != Quaternion.identity) rt.localRotation = Quaternion.identity;
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

    internal class OverlayerImageGraphic : MaskableGraphic
    {
        private Texture _texture;
        private Vector2 _p1;
        private Vector2 _p2;
        private Vector2 _p3;
        private Vector2 _p4;
        private float _alpha = 1f;
        private Vector2 _uvMin = Vector2.zero;
        private Vector2 _uvMax = Vector2.one;

        public override Texture mainTexture
        {
            get { return _texture != null ? _texture : s_WhiteTexture; }
        }

        public void SetImage(Texture texture, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float alpha)
        {
            SetImage(texture, p1, p2, p3, p4, alpha, Vector2.zero, Vector2.one);
        }

        public void SetImage(Texture texture, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float alpha, Vector2 uvMin, Vector2 uvMax)
        {
            float safeAlpha = Mathf.Clamp01(alpha);
            bool textureChanged = _texture != texture;
            bool meshChanged = _p1 != p1 || _p2 != p2 || _p3 != p3 || _p4 != p4 || !Mathf.Approximately(_alpha, safeAlpha) || _uvMin != uvMin || _uvMax != uvMax;

            if (!textureChanged && !meshChanged)
            {
                return;
            }

            _texture = texture;
            _p1 = p1;
            _p2 = p2;
            _p3 = p3;
            _p4 = p4;
            _alpha = safeAlpha;
            _uvMin = uvMin;
            _uvMax = uvMax;
            if (meshChanged) SetVerticesDirty();
            if (textureChanged) SetMaterialDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Color c = new Color(1f, 1f, 1f, _alpha);

            int start = vh.currentVertCount;
            vh.AddVert(ToUiLocal(_p1), c, new Vector2(_uvMin.x, _uvMax.y));
            vh.AddVert(ToUiLocal(_p2), c, new Vector2(_uvMax.x, _uvMax.y));
            vh.AddVert(ToUiLocal(_p3), c, new Vector2(_uvMax.x, _uvMin.y));
            vh.AddVert(ToUiLocal(_p4), c, new Vector2(_uvMin.x, _uvMin.y));

            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static Vector2 ToUiLocal(Vector2 screenLocal)
        {
            return new Vector2(screenLocal.x, -screenLocal.y);
        }
    }
}
