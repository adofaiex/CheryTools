using System;
using UnityEngine;

namespace CheryTools.KeyViewer.Preview
{
    internal struct KvBounds
    {
        internal float MinX, MinY, MaxX, MaxY;
        internal float Width => MaxX - MinX;
        internal float Height => MaxY - MinY;
        internal Vector2 Center => new Vector2((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f);
    }

    // Placement of an image or video inside its key, relative to the key center (y up), plus the
    // part of the texture shown. Shared by the runtime and the FreeMake canvas.
    internal struct KvMediaLayout
    {
        internal float Width, Height, X, Y;
        internal float UvX, UvY, UvWidth, UvHeight;
    }

    internal static class KvGeometry
    {
        internal static KvMediaLayout MediaLayout(int fit, float boxWidth, float boxHeight, float textureWidth, float textureHeight,
            float mediaScale, float pixelScale, float offsetX, float offsetY)
        {
            var layout = new KvMediaLayout { X = offsetX, Y = offsetY, UvWidth = 1f, UvHeight = 1f };
            float texW = textureWidth > 0f ? textureWidth : boxWidth;
            float texH = textureHeight > 0f ? textureHeight : boxHeight;
            switch (fit)
            {
                case 1:
                case 2:
                case 3:
                    float k = fit == 3 ? mediaScale * pixelScale
                        : (fit == 1 ? Math.Min(boxWidth / texW, boxHeight / texH) : Math.Max(boxWidth / texW, boxHeight / texH)) * mediaScale;
                    CropAxis(boxWidth, texW * k, offsetX, out layout.Width, out layout.X, out layout.UvX, out layout.UvWidth);
                    CropAxis(boxHeight, texH * k, offsetY, out layout.Height, out layout.Y, out layout.UvY, out layout.UvHeight);
                    break;
                case 4:
                    float tile = Math.Max(0.0001f, mediaScale * pixelScale);
                    layout.Width = boxWidth;
                    layout.Height = boxHeight;
                    layout.X = layout.Y = 0f;
                    layout.UvWidth = boxWidth / (texW * tile);
                    layout.UvHeight = boxHeight / (texH * tile);
                    layout.UvX = -offsetX / (texW * tile);
                    layout.UvY = -offsetY / (texH * tile);
                    break;
                default:
                    layout.Width = boxWidth * mediaScale;
                    layout.Height = boxHeight * mediaScale;
                    break;
            }
            layout.Width = Math.Max(1f, layout.Width);
            layout.Height = Math.Max(1f, layout.Height);
            return layout;
        }

        // Content smaller than the box moves freely; larger content is cropped to the box and the
        // offset pans inside it instead of spilling out of the key.
        private static void CropAxis(float box, float drawn, float offset, out float size, out float position, out float uvStart, out float uvLength)
        {
            if (drawn <= box)
            {
                size = drawn;
                position = offset;
                uvStart = 0f;
                uvLength = 1f;
                return;
            }
            size = box;
            position = 0f;
            uvLength = box / drawn;
            uvStart = Mathf.Clamp((1f - uvLength) * 0.5f - offset / drawn, 0f, 1f - uvLength);
        }

        internal static float Width(KvProfile profile, KvKey node) => Resolve(node?.Width ?? -1f, profile?.KeyWidth ?? 72f);
        internal static float Height(KvProfile profile, KvKey node) => Resolve(node?.Height ?? -1f, profile?.KeyHeight ?? 72f);

        internal static bool TryGetBounds(KvProfile profile, out KvBounds bounds)
        {
            float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            bool found = false;
            if (profile?.Keys != null)
            {
                foreach (KvKey node in profile.Keys)
                {
                    if (node == null) continue;
                    float halfW = Width(profile, node) * 0.5f;
                    float halfH = Height(profile, node) * 0.5f;
                    minX = Math.Min(minX, node.PositionX - halfW);
                    maxX = Math.Max(maxX, node.PositionX + halfW);
                    minY = Math.Min(minY, node.PositionY - halfH);
                    maxY = Math.Max(maxY, node.PositionY + halfH);
                    found = true;
                }
            }
            bounds = found ? new KvBounds { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY } : default(KvBounds);
            return found;
        }

        internal static KvKey CloneNode(KvKey source, bool preserveCounts = false)
        {
            if (source == null) return null;
            return new KvKey
            {
                NodeType = source.NodeType,
                Bind = source.Bind,
                Label = source.Label,
                ImagePath = source.ImagePath,
                VideoPath = source.VideoPath,
                VideoLoop = source.VideoLoop,
                MediaScale = source.MediaScale,
                MediaOffsetX = source.MediaOffsetX,
                MediaOffsetY = source.MediaOffsetY,
                MediaFit = source.MediaFit,
                Opacity = source.Opacity,
                Depth = source.Depth,
                Locked = source.Locked,
                HitCount = preserveCounts ? source.HitCount : 0,
                RainEnabled = source.RainEnabled,
                RainRow = source.RainRow,
                PositionX = source.PositionX,
                PositionY = source.PositionY,
                Width = source.Width,
                Height = source.Height,
                UseCustomStyle = source.UseCustomStyle,
                CornerRadius = source.CornerRadius,
                BorderThickness = source.BorderThickness,
                LabelSize = source.LabelSize,
                CountSize = source.CountSize,
                LabelFontPath = source.LabelFontPath,
                CountFontPath = source.CountFontPath,
                LabelOffsetX = source.LabelOffsetX,
                LabelOffsetY = source.LabelOffsetY,
                CountOffsetX = source.CountOffsetX,
                CountOffsetY = source.CountOffsetY,
                CountAlignment = source.CountAlignment,
                HideCount = source.HideCount,
                BackgroundNormal = Copy(source.BackgroundNormal),
                BackgroundPressed = Copy(source.BackgroundPressed),
                BorderNormal = Copy(source.BorderNormal),
                BorderPressed = Copy(source.BorderPressed),
                TextNormal = Copy(source.TextNormal),
                TextPressed = Copy(source.TextPressed),
                UseCustomTextEffects = source.UseCustomTextEffects,
                LabelOutlineEnabled = source.LabelOutlineEnabled,
                LabelOutlineColor = Copy(source.LabelOutlineColor),
                LabelOutlineThickness = source.LabelOutlineThickness,
                CountOutlineEnabled = source.CountOutlineEnabled,
                CountOutlineColor = Copy(source.CountOutlineColor),
                CountOutlineThickness = source.CountOutlineThickness,
                LabelShadowEnabled = source.LabelShadowEnabled,
                LabelShadowColor = Copy(source.LabelShadowColor),
                LabelShadowOffset = Copy(source.LabelShadowOffset),
                CountShadowEnabled = source.CountShadowEnabled,
                CountShadowColor = Copy(source.CountShadowColor),
                CountShadowOffset = Copy(source.CountShadowOffset),
                UseCustomRain = source.UseCustomRain,
                RainWidthRatio = source.RainWidthRatio,
                RainYOffset = source.RainYOffset,
                RainCornerRadius = source.RainCornerRadius,
                RainColor = Copy(source.RainColor),
                RainEndColor = Copy(source.RainEndColor),
                RainRightColor = Copy(source.RainRightColor),
                RainGradientEnabled = source.RainGradientEnabled,
                RainGradientMode = source.RainGradientMode,
                RainGradientHeight = source.RainGradientHeight,
                RainGradientPower = source.RainGradientPower,
                RainHorizontalGradientEnabled = source.RainHorizontalGradientEnabled,
                RainFadeMode = source.RainFadeMode,
                RainFadeHeight = source.RainFadeHeight,
                RainFadePower = source.RainFadePower,
                RainOutlineEnabled = source.RainOutlineEnabled,
                RainOutlineColor = Copy(source.RainOutlineColor),
                RainOutlineThickness = source.RainOutlineThickness,
                RainShadowEnabled = source.RainShadowEnabled,
                RainShadowColor = Copy(source.RainShadowColor),
                RainShadowOffset = Copy(source.RainShadowOffset),
                RainShadowSoftness = source.RainShadowSoftness,
                RainShadowStrength = source.RainShadowStrength,
                UseCustomAnimation = source.UseCustomAnimation,
                PressAnimationEnabled = source.PressAnimationEnabled,
                PressAnimationDuration = source.PressAnimationDuration,
                PressAnimationEasing = source.PressAnimationEasing,
                PressAnimationAffectColors = source.PressAnimationAffectColors,
                PressAnimationScale = source.PressAnimationScale,
                PressAnimationOffsetX = source.PressAnimationOffsetX,
                PressAnimationOffsetY = source.PressAnimationOffsetY
            };
        }

        internal static KvProfile CloneProfile(KvProfile source, bool preserveCounts = false)
        {
            if (source == null) return null;
            var target = new KvProfile
            {
                Name = source.Name,
                Enabled = source.Enabled,
                ShowInGame = source.ShowInGame,
                OnlyShowPlaying = source.OnlyShowPlaying,
                TotalHits = preserveCounts ? source.TotalHits : 0,
                LayoutVersion = 1,
                OffsetX = source.OffsetX,
                OffsetY = source.OffsetY,
                Scale = source.Scale,
                KeyWidth = source.KeyWidth,
                KeyHeight = source.KeyHeight,
                Gap = source.Gap,
                CornerRadius = source.CornerRadius,
                BorderThickness = source.BorderThickness,
                LabelSize = source.LabelSize,
                CountSize = source.CountSize,
                HideCount = source.HideCount,
                ShowKps = source.ShowKps,
                ShowTotal = source.ShowTotal,
                BackgroundNormal = Copy(source.BackgroundNormal),
                BackgroundPressed = Copy(source.BackgroundPressed),
                BorderNormal = Copy(source.BorderNormal),
                BorderPressed = Copy(source.BorderPressed),
                TextNormal = Copy(source.TextNormal),
                TextPressed = Copy(source.TextPressed),
                FontPath = source.FontPath,
                LabelOutlineEnabled = source.LabelOutlineEnabled,
                LabelOutlineColor = Copy(source.LabelOutlineColor),
                LabelOutlineThickness = source.LabelOutlineThickness,
                CountOutlineEnabled = source.CountOutlineEnabled,
                CountOutlineColor = Copy(source.CountOutlineColor),
                CountOutlineThickness = source.CountOutlineThickness,
                LabelShadowEnabled = source.LabelShadowEnabled,
                LabelShadowColor = Copy(source.LabelShadowColor),
                LabelShadowOffset = Copy(source.LabelShadowOffset),
                CountShadowEnabled = source.CountShadowEnabled,
                CountShadowColor = Copy(source.CountShadowColor),
                CountShadowOffset = Copy(source.CountShadowOffset),
                RainEnabled = source.RainEnabled,
                RainSpeed = source.RainSpeed,
                RainMaxHeight = source.RainMaxHeight,
                RainWidthRatio = source.RainWidthRatio,
                RainWidthRatio2 = source.RainWidthRatio2,
                RainYOffset = source.RainYOffset,
                RainYOffset2 = source.RainYOffset2,
                RainCornerRadius = source.RainCornerRadius,
                RainColor = Copy(source.RainColor),
                RainEndColor = Copy(source.RainEndColor),
                RainRightColor = Copy(source.RainRightColor),
                RainColor2 = Copy(source.RainColor2),
                RainEndColor2 = Copy(source.RainEndColor2),
                RainRightColor2 = Copy(source.RainRightColor2),
                RainGradientEnabled = source.RainGradientEnabled,
                RainGradientMode = source.RainGradientMode,
                RainGradientHeight = source.RainGradientHeight,
                RainGradientPower = source.RainGradientPower,
                RainHorizontalGradientEnabled = source.RainHorizontalGradientEnabled,
                RainFadeMode = source.RainFadeMode,
                RainFadeHeight = source.RainFadeHeight,
                RainFadePower = source.RainFadePower,
                RainOutlineEnabled = source.RainOutlineEnabled,
                RainOutlineColor = Copy(source.RainOutlineColor),
                RainOutlineThickness = source.RainOutlineThickness,
                RainShadowEnabled = source.RainShadowEnabled,
                RainShadowColor = Copy(source.RainShadowColor),
                RainShadowOffset = Copy(source.RainShadowOffset),
                RainShadowSoftness = source.RainShadowSoftness,
                RainShadowStrength = source.RainShadowStrength,
                PressAnimationEnabled = source.PressAnimationEnabled,
                PressAnimationDuration = source.PressAnimationDuration,
                PressAnimationEasing = source.PressAnimationEasing,
                PressAnimationAffectColors = source.PressAnimationAffectColors,
                PressAnimationScale = source.PressAnimationScale,
                PressAnimationOffsetX = source.PressAnimationOffsetX,
                PressAnimationOffsetY = source.PressAnimationOffsetY
            };
            target.Keys.Clear();
            if (source.Keys != null)
                foreach (KvKey node in source.Keys) target.Keys.Add(CloneNode(node, preserveCounts));
            return target;
        }

        internal static float Resolve(float value, float fallback) => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value) ? value : fallback;
        internal static float[] Copy(float[] value) => value == null ? null : (float[])value.Clone();
    }
}
