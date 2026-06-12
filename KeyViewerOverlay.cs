using System;
using UnityEngine;

namespace CheryTools
{
    public class KeyViewerOverlay : MonoBehaviour
    {
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
        private int _nextNodeRenderId = 1;

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

        private string GetKeySymbol(KeyCode key)
        {
            if (key == KeyCode.None) return "";
            string name = key.ToString();

            if (name.StartsWith("Alpha")) return name.Substring(5);
            if (name.StartsWith("Keypad")) return name.Substring(6);
            switch (key)
            {
                case KeyCode.LeftShift: return "LS";
                case KeyCode.RightShift: return "RS";
                case KeyCode.LeftControl: return "LC";
                case KeyCode.RightControl: return "RC";
                case KeyCode.LeftAlt: return "LA";
                case KeyCode.RightAlt: return "RA";
                case KeyCode.Space: return "Spc";
                case KeyCode.Return: return "Ent";
                case KeyCode.Backspace: return "Bsp";
                case KeyCode.Escape: return "Esc";
                case KeyCode.UpArrow: return "Up";
                case KeyCode.DownArrow: return "Down";
                case KeyCode.LeftArrow: return "Left";
                case KeyCode.RightArrow: return "Right";
                case KeyCode.Tab: return "Tab";
                case KeyCode.Equals: return "=";
                case KeyCode.Minus: return "-";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Backslash: return "\\";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
            }
            return name;
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
            return Main.Settings != null ? Main.Settings.KeyViewerFontPath : string.Empty;
        }

        private string GetCountFontPath(KVConfiguration config, KVNode node)
        {
            if (node != null && !string.IsNullOrEmpty(node.CountFontPath)) return node.CountFontPath;
            if (config != null && !string.IsNullOrEmpty(config.FontPath)) return config.FontPath;
            return Main.Settings != null ? Main.Settings.KeyViewerFontPath : string.Empty;
        }

        private void DrawText(string id, string text, string fontPath, float fontSize, Vector2 pos, Vector2 size, int alignment, uint color, bool outlineEnabled, uint outlineColor, float outlineThickness, int sortingOrder)
        {
            SdfTextRenderer.DrawScreenText(
                id,
                text,
                fontPath,
                fontSize,
                pos,
                new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y)),
                alignment,
                ColorU32ToVector4(color),
                outlineEnabled,
                ColorU32ToVector4(outlineColor),
                outlineThickness,
                sortingOrder);
        }

        public void RenderUI()
        {
            if (!ShouldRender())
            {
                KeyViewerUnityRenderer.HideAll();
                return;
            }

            if (Main.Settings.KeyViewerConfigurations == null || Main.Settings.KeyViewerConfigurations.Count == 0)
            {
                KeyViewerUnityRenderer.HideAll();
                return;
            }

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            bool drewAny = false;

            KeyViewerUnityRenderer.BeginFrame();
            foreach (KVConfiguration config in Main.Settings.KeyViewerConfigurations)
            {
                if (config == null || !config.IsEnabled || config.Nodes == null || config.Nodes.Count == 0)
                    continue;

                float globalScale = config.Scale;
                float rounding = (float)Math.Floor(6 * globalScale);
                float borderThickness = config.BorderThickness;
                uint kpsColor = Vector4ToColor(config.ColorKps);
                uint totalColor = Vector4ToColor(config.ColorTotal);

                DrawKeyRain(config, center, globalScale);
                DrawBackgroundImages(config, config.Nodes, center, globalScale);
                DrawNodes(config, config.Nodes, center, globalScale, rounding, borderThickness, kpsColor, totalColor);
                drewAny = true;
            }
            KeyViewerUnityRenderer.EndFrame();

            if (!drewAny)
            {
                KeyViewerUnityRenderer.HideAll();
            }
        }

        private bool ShouldRender()
        {
            if (!Main.IsEnabled || Main.Settings == null || !Main.Settings.EnableKeyViewer) return false;
            if (KeyViewerManager.Instance == null) return false;
            if (Main.Settings.KeyViewerOnlyShowPlaying && !Main.IsGamePlaying() && !FreeMakeEditor.IsOpen) return false;
            return true;
        }

        private void DrawBackgroundImages(KVConfiguration config, System.Collections.Generic.List<KVNode> activeNodes, Vector2 center, float globalScale)
        {
            foreach (var node in activeNodes)
            {
                if (node == null || node.NodeType != 3) continue;

                bool pressed = false;
                KeyViewerManager.Instance.IsNodePressed.TryGetValue(node, out pressed);

                float alpha = node.UseCustomColor ? (pressed ? node.ColorBgPressed[3] : node.ColorBgNormal[3]) : node.Opacity;
                float finalScale = globalScale * node.Scale;
                Vector2 pos = new Vector2(center.x + node.PositionX * globalScale, center.y + node.PositionY * globalScale);
                Vector2 size = new Vector2(node.Width * finalScale, node.Height * finalScale);
                Texture2D texture = TextureManager.GetOrCreateTexture2D(node.ImagePath, size.x, size.y);
                if (texture == null) continue;

                NodeRenderIds ids = GetNodeRenderIds(node);
                if (ids == null) continue;
                int sortingOrder = RenderDepth.ToSortingOrder(node.Depth, RenderDepth.SublayerGraphic);
                float cornerRadius = ResolveNodeCornerRadius(node, 0f, globalScale);
                KeyViewerUnityRenderer.DrawImage(ids.BackgroundImage, texture, pos, size, alpha, cornerRadius, sortingOrder);
            }
        }

        private void DrawNodes(KVConfiguration config, System.Collections.Generic.List<KVNode> activeNodes, Vector2 center, float globalScale, float rounding, float borderThickness, uint kpsColor, uint totalColor)
        {
            foreach (var node in activeNodes)
            {
                if (node == null || node.NodeType == 3) continue;

                uint bgNormal = node.UseCustomColor ? Vector4ToColor(node.ColorBgNormal) : Vector4ToColor(config.ColorBgNormal);
                uint bgPressed = node.UseCustomColor ? Vector4ToColor(node.ColorBgPressed) : Vector4ToColor(config.ColorBgPressed);
                uint borderNormal = node.UseCustomColor ? Vector4ToColor(node.ColorBorderNormal) : Vector4ToColor(config.ColorBorderNormal);
                uint borderPressed = node.UseCustomColor ? Vector4ToColor(node.ColorBorderPressed) : Vector4ToColor(config.ColorBorderPressed);
                uint textNormal = node.UseCustomColor ? Vector4ToColor(node.ColorTextNormal) : Vector4ToColor(config.ColorTextNormal);
                uint textPressed = node.UseCustomColor ? Vector4ToColor(node.ColorTextPressed) : Vector4ToColor(config.ColorTextPressed);

                bool keyOutlineEnabled = node.UseCustomOutline ? node.KeyTextOutlineEnabled : config.KeyTextOutlineEnabled;
                bool countOutlineEnabled = node.UseCustomOutline ? node.CountTextOutlineEnabled : config.CountTextOutlineEnabled;
                uint keyOutlineColor = node.UseCustomOutline ? TextStyleRenderer.ColorArrayToU32(node.KeyTextOutlineColor, 0xFF000000) : TextStyleRenderer.ColorArrayToU32(config.KeyTextOutlineColor, 0xFF000000);
                uint countOutlineColor = node.UseCustomOutline ? TextStyleRenderer.ColorArrayToU32(node.CountTextOutlineColor, 0xFF000000) : TextStyleRenderer.ColorArrayToU32(config.CountTextOutlineColor, 0xFF000000);
                float keyOutlineThickness = node.UseCustomOutline ? node.KeyTextOutlineThickness : config.KeyTextOutlineThickness;
                float countOutlineThickness = node.UseCustomOutline ? node.CountTextOutlineThickness : config.CountTextOutlineThickness;
                bool hideCountText = config.HideCountText || node.HideCountText;

                float finalScale = globalScale * node.Scale;
                Vector2 pos = new Vector2(center.x + node.PositionX * globalScale, center.y + node.PositionY * globalScale);
                Vector2 size = new Vector2(node.Width * finalScale, node.Height * finalScale);
                bool pressed = false;
                KeyViewerManager.Instance.IsNodePressed.TryGetValue(node, out pressed);
                NodeRenderIds ids = GetNodeRenderIds(node);
                if (ids == null) continue;
                int graphicSortingOrder = RenderDepth.ToSortingOrder(node.Depth, RenderDepth.SublayerGraphic);
                int textSortingOrder = RenderDepth.ToSortingOrder(node.Depth, RenderDepth.SublayerText);
                float cornerRadius = ResolveNodeCornerRadius(node, rounding, globalScale);

                if (node.NodeType == 0)
                {
                    uint bgColor = pressed ? bgPressed : bgNormal;
                    uint borderColor = pressed ? borderPressed : borderNormal;
                    uint txtColor = pressed ? textPressed : textNormal;
                    float bThick = node.BorderThickness >= 0f ? node.BorderThickness : borderThickness;
                    KeyViewerUnityRenderer.DrawRect(ids.Box, pos, size, bgColor, borderColor, bThick, cornerRadius, graphicSortingOrder);

                    string labelStr = !string.IsNullOrEmpty(node.CustomText) ? node.CustomText : KeyDisplayNames.GetKeySymbol(node.KeyBind);

                    float keyFontSize = 20.0f * globalScale * node.TextScale;
                    string keyFontPath = GetKeyFontPath(config, node);
                    float keyTextHeight = TextBoxHeight(keyFontSize);
                    Vector2 keyPos = new Vector2(
                        pos.x + (node.TextOffsetX + config.GlobalTextOffsetX) * finalScale,
                        hideCountText
                            ? pos.y + (size.y - keyTextHeight) * 0.5f + (node.TextOffsetY + config.GlobalTextOffsetY) * finalScale
                            : pos.y + 5 * finalScale + (node.TextOffsetY + config.GlobalTextOffsetY) * finalScale);
                    DrawText(ids.KeyText, labelStr, keyFontPath, keyFontSize, keyPos, new Vector2(size.x, keyTextHeight), 1, txtColor, keyOutlineEnabled, keyOutlineColor, keyOutlineThickness, textSortingOrder);

                    if (!hideCountText)
                    {
                        string countStr = node.HitCount.ToString();
                        float countFontSize = 20.0f * globalScale * node.CountScale;
                        string countFontPath = GetCountFontPath(config, node);
                        float countTextHeight = TextBoxHeight(countFontSize);
                        Vector2 countPos = new Vector2(
                            pos.x + (node.CountOffsetX + config.GlobalCountOffsetX) * finalScale,
                            pos.y + size.y - countTextHeight - 5 * finalScale + (node.CountOffsetY + config.GlobalCountOffsetY) * finalScale);
                        DrawText(ids.CountText, countStr, countFontPath, countFontSize, countPos, new Vector2(size.x, countTextHeight), 1, txtColor, countOutlineEnabled, countOutlineColor, countOutlineThickness, textSortingOrder);
                    }
                }
                else if (node.NodeType == 1)
                {
                    float bThick = node.BorderThickness >= 0f ? node.BorderThickness : borderThickness;
                    KeyViewerUnityRenderer.DrawRect(ids.Box, pos, size, bgNormal, borderNormal, bThick, cornerRadius, graphicSortingOrder);

                    string label = !string.IsNullOrEmpty(node.CustomText) ? node.CustomText : "KPS";
                    string val = KeyViewerManager.Instance.CurrentKPS.ToString();
                    DrawPairText(config, node, pos, size, finalScale, globalScale, label, val, kpsColor, keyOutlineEnabled, keyOutlineColor, keyOutlineThickness, countOutlineEnabled, countOutlineColor, countOutlineThickness, ids.KpsLabel, ids.KpsValue, hideCountText, textSortingOrder);
                }
                else if (node.NodeType == 2)
                {
                    float bThick = node.BorderThickness >= 0f ? node.BorderThickness : borderThickness;
                    KeyViewerUnityRenderer.DrawRect(ids.Box, pos, size, bgNormal, borderNormal, bThick, cornerRadius, graphicSortingOrder);

                    string label = !string.IsNullOrEmpty(node.CustomText) ? node.CustomText : "Total";
                    string val = Main.Settings.TotalHits.ToString();
                    DrawPairText(config, node, pos, size, finalScale, globalScale, label, val, totalColor, keyOutlineEnabled, keyOutlineColor, keyOutlineThickness, countOutlineEnabled, countOutlineColor, countOutlineThickness, ids.TotalLabel, ids.TotalValue, hideCountText, textSortingOrder);
                }
            }
        }

        private void DrawPairText(KVConfiguration config, KVNode node, Vector2 pos, Vector2 size, float finalScale, float globalScale, string label, string value, uint color, bool keyOutlineEnabled, uint keyOutlineColor, float keyOutlineThickness, bool countOutlineEnabled, uint countOutlineColor, float countOutlineThickness, string labelId, string valueId, bool hideValue, int sortingOrder)
        {
            float keyFontSize = 20.0f * globalScale * node.TextScale;
            string keyFontPath = GetKeyFontPath(config, node);
            float labelHeight = TextBoxHeight(keyFontSize);
            if (hideValue)
            {
                Vector2 centeredLabelPos = new Vector2(
                    pos.x + (node.TextOffsetX + config.GlobalTextOffsetX) * finalScale,
                    pos.y + (size.y - labelHeight) * 0.5f + (node.TextOffsetY + config.GlobalTextOffsetY) * finalScale);
                DrawText(labelId, label, keyFontPath, keyFontSize, centeredLabelPos, new Vector2(size.x, labelHeight), 1, color, keyOutlineEnabled, keyOutlineColor, keyOutlineThickness, sortingOrder);
                return;
            }

            float textPadding = 8 * finalScale;
            float textWidth = Mathf.Max(1f, size.x - textPadding * 2f);
            Vector2 labelPos = new Vector2(
                pos.x + textPadding + (node.TextOffsetX + config.GlobalTextOffsetX) * finalScale,
                pos.y + (size.y - labelHeight) * 0.5f + (node.TextOffsetY + config.GlobalTextOffsetY) * finalScale);
            DrawText(labelId, label, keyFontPath, keyFontSize, labelPos, new Vector2(textWidth, labelHeight), 0, color, keyOutlineEnabled, keyOutlineColor, keyOutlineThickness, sortingOrder);

            float countFontSize = 20.0f * globalScale * node.CountScale;
            string countFontPath = GetCountFontPath(config, node);
            float valueHeight = TextBoxHeight(countFontSize);
            Vector2 valuePos = new Vector2(
                pos.x + textPadding + (node.CountOffsetX + config.GlobalCountOffsetX) * finalScale,
                pos.y + (size.y - valueHeight) * 0.5f + (node.CountOffsetY + config.GlobalCountOffsetY) * finalScale);
            DrawText(valueId, value, countFontPath, countFontSize, valuePos, new Vector2(textWidth, valueHeight), 2, color, countOutlineEnabled, countOutlineColor, countOutlineThickness, sortingOrder);
        }

        private void DrawKeyRain(KVConfiguration config, Vector2 center, float globalScale)
        {
            if (config == null || !config.EnableKeyRain || KeyViewerManager.Instance.ActiveDrops.Count <= 0) return;

            float speed = config.KeyRainSpeed;
            float maxHeight = config.KeyRainMaxHeight;
            int fadeMode = config.KeyRainFadeMode;
            float currentTime = Time.unscaledTime;

            uint row1Color = Vector4ToColor(config.KeyRainColorRow1);
            uint row2Color = Vector4ToColor(config.KeyRainColorRow2);
            float row1Ratio = config.KeyRainWidthRatio1;
            float row2Ratio = config.KeyRainWidthRatio2;

            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < KeyViewerManager.Instance.ActiveDrops.Count; i++)
                {
                    KeyDrop drop = KeyViewerManager.Instance.ActiveDrops[i];
                    KVNode node = drop.Node;
                    if (node == null || node.NodeType != 0) continue;
                    if (config.Nodes == null || !config.Nodes.Contains(node)) continue;

                    bool isRow1 = node.RainRow == 1;
                    if (pass == 0 && !isRow1) continue;
                    if (pass == 1 && isRow1) continue;

                    float ratio;
                    uint baseColor;
                    float currentYOffset;
                    if (node.UseCustomRain)
                    {
                        ratio = node.RainWidthRatio;
                        baseColor = Vector4ToColor(node.RainColor);
                        currentYOffset = node.RainYOffset;
                    }
                    else
                    {
                        ratio = isRow1 ? row1Ratio : row2Ratio;
                        baseColor = isRow1 ? row1Color : row2Color;
                        currentYOffset = isRow1 ? config.KeyRainYOffsetRow1 : config.KeyRainYOffsetRow2;
                    }

                    float finalScale = globalScale * node.Scale;
                    float boxW = node.Width * finalScale;
                    float keyX = center.x + node.PositionX * globalScale;
                    float keyY = center.y + node.PositionY * globalScale - currentYOffset;
                    float dropW = boxW * ratio;
                    float dropX = keyX + (boxW - dropW) * 0.5f;
                    float endTime = drop.EndTime ?? currentTime;
                    float dropBottomY = keyY - speed * (currentTime - endTime);
                    float dropTopY = keyY - speed * (currentTime - drop.StartTime);

                    if (dropBottomY < keyY - maxHeight && fadeMode == 0) continue;

                    float clampedBottomY = Math.Min(dropBottomY, keyY);
                    float clampedTopY = Math.Max(dropTopY, keyY - maxHeight);
                    if (clampedBottomY <= clampedTopY) continue;

                    uint topColor = baseColor;
                    uint bottomColor = baseColor;
                    if (fadeMode == 1)
                    {
                        float bottomAlphaRatio = 1.0f - Mathf.Clamp((keyY - clampedBottomY) / maxHeight, 0f, 1f);
                        float topAlphaRatio = 1.0f - Mathf.Clamp((keyY - clampedTopY) / maxHeight, 0f, 1f);
                        bottomColor = MultiplyAlpha(baseColor, bottomAlphaRatio);
                        topColor = MultiplyAlpha(baseColor, topAlphaRatio);
                    }

                    float snapLeft = Mathf.Round(dropX);
                    float snapRight = Mathf.Round(dropX + dropW);
                    float snapTop = Mathf.Round(clampedTopY);
                    float snapBottom = Mathf.Round(clampedBottomY);
                    if (snapRight <= snapLeft || snapBottom <= snapTop) continue;

                    int sortingOrder = RenderDepth.ToSortingOrder(node.Depth, RenderDepth.SublayerRain);
                    KeyViewerUnityRenderer.DrawGradientRect(
                        drop.RenderId,
                        new Vector2(snapLeft, snapTop),
                        new Vector2(snapRight - snapLeft, snapBottom - snapTop),
                        topColor,
                        bottomColor,
                        sortingOrder);
                }
            }
        }
    }
}
