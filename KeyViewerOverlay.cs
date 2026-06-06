using System;
using UnityEngine;
using ImGuiNET;

namespace CheryTools
{
    public class KeyViewerOverlay : MonoBehaviour
    {
        private string GetKeySymbol(KeyCode key)
        {
            if (key == KeyCode.None) return "";
            string name = key.ToString();
            
            // Map common keys to symbols or shorter names
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
                case KeyCode.UpArrow: return "↑";
                case KeyCode.DownArrow: return "↓";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.RightArrow: return "→";
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
            byte r = (byte)(arr[0] * 255);
            byte g = (byte)(arr[1] * 255);
            byte b = (byte)(arr[2] * 255);
            byte a = (byte)(arr[3] * 255);
            return (uint)((a << 24) | (b << 16) | (g << 8) | r);
        }

        private void DrawBox(ImDrawListPtr drawList, System.Numerics.Vector2 pMin, System.Numerics.Vector2 pMax, uint bgColor, uint borderColor, float rounding, float borderThickness)
        {
            byte bgAlpha = (byte)(bgColor >> 24);
            byte borderAlpha = (byte)(borderColor >> 24);

            if (bgAlpha > 0)
            {
                drawList.AddRectFilled(pMin, pMax, bgColor, rounding);
            }

            if (borderThickness > 0 && borderAlpha > 0)
            {
                drawList.AddRect(pMin, pMax, borderColor, rounding, ImDrawFlags.None, borderThickness);
            }
        }

        private uint MultiplyAlpha(uint color, float ratio)
        {
            byte a = (byte)((color >> 24) & 0xFF);
            byte b = (byte)((color >> 16) & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte r = (byte)(color & 0xFF);
            byte newA = (byte)(a * ratio);
            return (uint)((newA << 24) | (b << 16) | (g << 8) | r);
        }

        public void RenderUI()
        {
            if (!Main.IsEnabled || !Main.Settings.EnableKeyViewer) return;
            if (Main.Settings.KeyViewerOnlyShowPlaying && !Main.IsGamePlaying() && !FreeMakeEditor.IsOpen) return;
            if (KeyViewerManager.Instance == null) return;

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoInputs;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new System.Numerics.Vector2(0, 0));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new System.Numerics.Vector2(0, 0));
            bool customFontPushed = false;
            try
            {
                // Load Custom Font if requested
                if (!string.IsNullOrEmpty(Main.Settings.KeyViewerFontPath) && ImGuiController.CustomFonts.ContainsKey(Main.Settings.KeyViewerFontPath))
                {
                    ImGui.PushFont(ImGuiController.CustomFonts[Main.Settings.KeyViewerFontPath]);
                    customFontPushed = true;
                }
                try
                {
                    ImGui.SetNextWindowPos(System.Numerics.Vector2.Zero);
                    ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
                    bool isVisible = ImGui.Begin("KeyViewer_Overlay", flags);
                    try
                    {
            if (isVisible)
            {
                var drawList = ImGui.GetWindowDrawList();
                var displaySize = ImGui.GetIO().DisplaySize;
                var p = new System.Numerics.Vector2(displaySize.X * 0.5f, displaySize.Y * 0.5f); 
                
                float globalScale = Main.Settings.KeyViewerScale;
                float rounding = (float)Math.Floor(6 * globalScale);
                float borderThickness = Main.Settings.KeyViewerBorderThickness;

                var activeNodes = KeyViewerManager.Instance.GetActiveNodes();
                if (activeNodes != null)
                {
                    float maxX = 0f;
                    float maxY = 0f;

                    uint kpsColor = Vector4ToColor(Main.Settings.KeyViewerColorKps);
                    uint totalColor = Vector4ToColor(Main.Settings.KeyViewerColorTotal);

                    // Pass 1: Draw Background Images
                    foreach (var node in activeNodes)
                    {
                        if (node.NodeType != 3) continue;

                        float finalScale = globalScale * node.Scale;
                        float w = node.Width * finalScale;
                        float h = node.Height * finalScale;
                        
                        System.Numerics.Vector2 pos = new System.Numerics.Vector2(p.X + node.PositionX * globalScale, p.Y + node.PositionY * globalScale);
                        System.Numerics.Vector2 pMax = new System.Numerics.Vector2(pos.X + w, pos.Y + h);

                        if (pos.X + w > maxX) maxX = pos.X + w;
                        if (pos.Y + h > maxY) maxY = pos.Y + h;

                        IntPtr texPtr = TextureManager.GetOrCreateTexture(node.ImagePath);
                        if (texPtr != IntPtr.Zero)
                        {
                            bool pressed = false;
                            KeyViewerManager.Instance.IsNodePressed.TryGetValue(node, out pressed);
                            
                            float alpha = node.UseCustomColor ? (pressed ? node.ColorBgPressed[3] : node.ColorBgNormal[3]) : node.Opacity;
                            uint tintColor = ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1, 1, 1, alpha));
                            
                            drawList.AddImage(texPtr, pos, pMax, new System.Numerics.Vector2(0, 1), new System.Numerics.Vector2(1, 0), tintColor);
                        }
                    }

                    // Pass 2: Draw other nodes
                    foreach (var node in activeNodes)
                    {
                        if (node.NodeType == 3) continue;

                        uint bgNormal = node.UseCustomColor ? Vector4ToColor(node.ColorBgNormal) : Vector4ToColor(Main.Settings.KeyViewerColorBgNormal);
                        uint bgPressed = node.UseCustomColor ? Vector4ToColor(node.ColorBgPressed) : Vector4ToColor(Main.Settings.KeyViewerColorBgPressed);
                        uint borderNormal = node.UseCustomColor ? Vector4ToColor(node.ColorBorderNormal) : Vector4ToColor(Main.Settings.KeyViewerColorBorderNormal);
                        uint borderPressed = node.UseCustomColor ? Vector4ToColor(node.ColorBorderPressed) : Vector4ToColor(Main.Settings.KeyViewerColorBorderPressed);
                        uint textNormal = node.UseCustomColor ? Vector4ToColor(node.ColorTextNormal) : Vector4ToColor(Main.Settings.KeyViewerColorTextNormal);
                        uint textPressed = node.UseCustomColor ? Vector4ToColor(node.ColorTextPressed) : Vector4ToColor(Main.Settings.KeyViewerColorTextPressed);

                        float finalScale = globalScale * node.Scale;
                        float w = node.Width * finalScale;
                        float h = node.Height * finalScale;
                        
                        System.Numerics.Vector2 pos = new System.Numerics.Vector2(p.X + node.PositionX * globalScale, p.Y + node.PositionY * globalScale);
                        System.Numerics.Vector2 pMax = new System.Numerics.Vector2(pos.X + w, pos.Y + h);

                        if (pos.X + w > maxX) maxX = pos.X + w;
                        if (pos.Y + h > maxY) maxY = pos.Y + h;

                        if (node.NodeType == 0) // Normal Key
                        {
                            bool pressed = false;
                            KeyViewerManager.Instance.IsNodePressed.TryGetValue(node, out pressed);

                            uint bgColor = pressed ? bgPressed : bgNormal;
                            uint borderColor = pressed ? borderPressed : borderNormal;
                            uint txtColor = pressed ? textPressed : textNormal;

                            float bThick = node.BorderThickness >= 0f ? node.BorderThickness : borderThickness;
                            DrawBox(drawList, pos, pMax, bgColor, borderColor, rounding, bThick);

                            string labelStr = !string.IsNullOrEmpty(node.CustomText) ? node.CustomText : GetKeySymbol(
                                System.Enum.TryParse(node.KeyBind, true, out KeyCode kc) ? kc : KeyCode.None
                            );
                            
                            ImFontPtr keyFont = ImGuiController.GetHighResFontOrDefault(node.KeyFontPath);
                            float keyFontSize = 20.0f * globalScale * node.TextScale;
                            var symSize = keyFont.CalcTextSizeA(keyFontSize, float.MaxValue, 0f, labelStr);
                            drawList.AddText(keyFont, keyFontSize, new System.Numerics.Vector2(pos.X + (w - symSize.X) * 0.5f + (node.TextOffsetX + Main.Settings.GlobalTextOffsetX) * finalScale, pos.Y + 5 * finalScale + (node.TextOffsetY + Main.Settings.GlobalTextOffsetY) * finalScale), txtColor, labelStr);

                            string countStr = node.HitCount.ToString();
                            ImFontPtr countFont = ImGuiController.GetHighResFontOrDefault(node.CountFontPath);
                            float countFontSize = 20.0f * globalScale * node.CountScale;
                            var countSize = countFont.CalcTextSizeA(countFontSize, float.MaxValue, 0f, countStr);
                            drawList.AddText(countFont, countFontSize, new System.Numerics.Vector2(pos.X + (w - countSize.X) * 0.5f + (node.CountOffsetX + Main.Settings.GlobalCountOffsetX) * finalScale, pos.Y + h - countSize.Y - 5 * finalScale + (node.CountOffsetY + Main.Settings.GlobalCountOffsetY) * finalScale), txtColor, countStr);
                        }
                        else if (node.NodeType == 1) // KPS
                        {
                            float bThick = node.BorderThickness >= 0f ? node.BorderThickness : borderThickness;
                            DrawBox(drawList, pos, pMax, bgNormal, borderNormal, rounding, bThick);

                            string label = !string.IsNullOrEmpty(node.CustomText) ? node.CustomText : "KPS";
                            string val = KeyViewerManager.Instance.CurrentKPS.ToString();
                            
                            ImFontPtr keyFont = ImGuiController.GetHighResFontOrDefault(node.KeyFontPath);
                            float keyFontSize = 20.0f * globalScale * node.TextScale;
                            var symSize = keyFont.CalcTextSizeA(keyFontSize, float.MaxValue, 0f, label);
                            drawList.AddText(keyFont, keyFontSize, new System.Numerics.Vector2(pos.X + 8 * finalScale + (node.TextOffsetX + Main.Settings.GlobalTextOffsetX) * finalScale, pos.Y + (h - symSize.Y) * 0.5f + (node.TextOffsetY + Main.Settings.GlobalTextOffsetY) * finalScale), kpsColor, label);

                            ImFontPtr countFont = ImGuiController.GetHighResFontOrDefault(node.CountFontPath);
                            float countFontSize = 20.0f * globalScale * node.CountScale;
                            var valSize = countFont.CalcTextSizeA(countFontSize, float.MaxValue, 0f, val);
                            drawList.AddText(countFont, countFontSize, new System.Numerics.Vector2(pos.X + w - valSize.X - 8 * finalScale + (node.CountOffsetX + Main.Settings.GlobalCountOffsetX) * finalScale, pos.Y + (h - valSize.Y) * 0.5f + (node.CountOffsetY + Main.Settings.GlobalCountOffsetY) * finalScale), kpsColor, val);
                        }
                        else if (node.NodeType == 2) // Total
                        {
                            float bThick = node.BorderThickness >= 0f ? node.BorderThickness : borderThickness;
                            DrawBox(drawList, pos, pMax, bgNormal, borderNormal, rounding, bThick);

                            string label = !string.IsNullOrEmpty(node.CustomText) ? node.CustomText : "Total";
                            string val = Main.Settings.TotalHits.ToString();
                            
                            ImFontPtr keyFont = ImGuiController.GetHighResFontOrDefault(node.KeyFontPath);
                            float keyFontSize = 20.0f * globalScale * node.TextScale;
                            var symSize = keyFont.CalcTextSizeA(keyFontSize, float.MaxValue, 0f, label);
                            drawList.AddText(keyFont, keyFontSize, new System.Numerics.Vector2(pos.X + 8 * finalScale + (node.TextOffsetX + Main.Settings.GlobalTextOffsetX) * finalScale, pos.Y + (h - symSize.Y) * 0.5f + (node.TextOffsetY + Main.Settings.GlobalTextOffsetY) * finalScale), totalColor, label);

                            ImFontPtr countFont = ImGuiController.GetHighResFontOrDefault(node.CountFontPath);
                            float countFontSize = 20.0f * globalScale * node.CountScale;
                            var valSize = countFont.CalcTextSizeA(countFontSize, float.MaxValue, 0f, val);
                            drawList.AddText(countFont, countFontSize, new System.Numerics.Vector2(pos.X + w - valSize.X - 8 * finalScale + (node.CountOffsetX + Main.Settings.GlobalCountOffsetX) * finalScale, pos.Y + (h - valSize.Y) * 0.5f + (node.CountOffsetY + Main.Settings.GlobalCountOffsetY) * finalScale), totalColor, val);
                        }
                    }

                    // No longer need dummy to expand window since it is set next window size to full screen
                }

                // --- Key Rain Rendering ---
                if (Main.Settings.EnableKeyRain && KeyViewerManager.Instance.ActiveDrops.Count > 0)
                {
                    var bgDrawList = ImGui.GetBackgroundDrawList();
                    float speed = Main.Settings.KeyRainSpeed;
                    float maxHeight = Main.Settings.KeyRainMaxHeight;
                    int fadeMode = Main.Settings.KeyRainFadeMode;
                    float currentTime = UnityEngine.Time.unscaledTime;
                    
                    uint c1 = Vector4ToColor(Main.Settings.KeyRainColorRow1);
                    uint c2 = Vector4ToColor(Main.Settings.KeyRainColorRow2);
                    float wRatio1 = Main.Settings.KeyRainWidthRatio1;
                    float wRatio2 = Main.Settings.KeyRainWidthRatio2;

                    for (int pass = 0; pass < 2; pass++)
                    {
                        foreach (var drop in KeyViewerManager.Instance.ActiveDrops)
                        {
                            var node = drop.Node;
                            if (node == null || node.NodeType != 0) continue;

                            bool isRow1 = (node.RainRow == 1);
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
                            ratio = isRow1 ? wRatio1 : wRatio2;
                            baseColor = isRow1 ? c1 : c2;
                            currentYOffset = isRow1 ? Main.Settings.KeyRainYOffsetRow1 : Main.Settings.KeyRainYOffsetRow2;
                        }
                        
                        float finalScale = globalScale * node.Scale;
                        float boxW = node.Width * finalScale;
                        float keyX = p.X + node.PositionX * globalScale;
                        float keyY = p.Y + node.PositionY * globalScale - currentYOffset;
                        
                        float dropW = boxW * ratio;
                        float dropX = keyX + (boxW - dropW) * 0.5f;

                        float endTime = drop.EndTime ?? currentTime;
                        
                        float dropBottomY = keyY - speed * (currentTime - endTime);
                        float dropTopY = keyY - speed * (currentTime - drop.StartTime);

                        if (dropBottomY < keyY - maxHeight && fadeMode == 0) continue;

                        float clampedBottomY = Math.Min(dropBottomY, keyY);
                        float clampedTopY = Math.Max(dropTopY, keyY - maxHeight);
                        
                        if (clampedBottomY <= clampedTopY) continue;

                        if (fadeMode == 0) // Clip Mode
                        {
                            bgDrawList.AddRectFilled(new System.Numerics.Vector2(dropX, clampedTopY), new System.Numerics.Vector2(dropX + dropW, clampedBottomY), baseColor);
                        }
                        else if (fadeMode == 1) // Fade Mode
                        {
                            float bottomAlphaRatio = 1.0f - UnityEngine.Mathf.Clamp((keyY - clampedBottomY) / maxHeight, 0f, 1f);
                            float topAlphaRatio = 1.0f - UnityEngine.Mathf.Clamp((keyY - clampedTopY) / maxHeight, 0f, 1f);
                            
                            uint colBottom = MultiplyAlpha(baseColor, bottomAlphaRatio);
                            uint colTop = MultiplyAlpha(baseColor, topAlphaRatio);
                            
                            bgDrawList.AddRectFilledMultiColor(
                                new System.Numerics.Vector2(dropX, clampedTopY), 
                                new System.Numerics.Vector2(dropX + dropW, clampedBottomY),
                                colTop, colTop, colBottom, colBottom
                            );
                        }
                        }
                    }
                }
            }

                    }
                    finally
                    {
                        ImGui.End();
                    }
                }
                finally
                {
                    if (customFontPushed)
                    {
                        ImGui.PopFont();
                    }
                }
            }
            finally
            {
                ImGui.PopStyleVar(2);
            }
        }
    }
}
