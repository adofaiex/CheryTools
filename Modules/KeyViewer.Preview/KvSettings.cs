using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using CheryTools.Sonnet.Contracts;
using UnityEngine;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.KeyViewer.Preview
{
    [Serializable]
    public sealed class KvKey
    {
        public string Id = Guid.NewGuid().ToString("N");
        public int NodeType; // 0=按键，1=KPS，2=总计数，3=图片按键，4=视频按键
        public KeyCode Bind = KeyCode.A;
        public string Label = "A";
        public string ImagePath = string.Empty;
        public string VideoPath = string.Empty;
        public bool VideoLoop = true;
        public float MediaScale = 1f;
        public float MediaOffsetX;
        public float MediaOffsetY;
        public int MediaFit; // 0=拉伸（视频为默认适配），1=适应，2=填充，3=居中，4=平铺
        public float Opacity = 1f;
        public int Depth;
        public bool Locked;
        public int HitCount;
        public bool RainEnabled = true;
        public int RainRow = 1;
        public float PositionX;
        public float PositionY;
        public float Width = -1f;
        public float Height = -1f;
        public bool UseCustomStyle;
        public float CornerRadius = -1f;
        public float BorderThickness = -1f;
        public float LabelSize = -1f;
        public float CountSize = -1f;
        // 留空时继承所属配置的 FontPath。
        public string LabelFontPath = string.Empty;
        public string CountFontPath = string.Empty;
        public float LabelOffsetX;
        public float LabelOffsetY;
        public float CountOffsetX;
        public float CountOffsetY;
        public int CountAlignment = 1;
        public bool HideCount;
        public float[] BackgroundNormal = { 0.075f, 0.085f, 0.11f, 0.88f };
        public float[] BackgroundPressed = { 0.30f, 0.76f, 1f, 0.96f };
        public float[] BorderNormal = { 0.42f, 0.48f, 0.58f, 0.70f };
        public float[] BorderPressed = { 0.72f, 0.92f, 1f, 1f };
        public float[] TextNormal = { 0.94f, 0.96f, 1f, 1f };
        public float[] TextPressed = { 0.015f, 0.025f, 0.04f, 1f };

        public bool UseCustomTextEffects;
        public bool LabelOutlineEnabled;
        public float[] LabelOutlineColor = { 0f, 0f, 0f, 1f };
        public float LabelOutlineThickness = 1f;
        public bool CountOutlineEnabled;
        public float[] CountOutlineColor = { 0f, 0f, 0f, 1f };
        public float CountOutlineThickness = 1f;
        public bool LabelShadowEnabled;
        public float[] LabelShadowColor = { 0f, 0f, 0f, 0.7f };
        public float[] LabelShadowOffset = { 2f, -2f };
        public float LabelShadowSoftness;
        public bool CountShadowEnabled;
        public float[] CountShadowColor = { 0f, 0f, 0f, 0.7f };
        public float[] CountShadowOffset = { 2f, -2f };
        public float CountShadowSoftness;

        public bool UseCustomRain;
        public float RainWidthRatio = 0.72f;
        public float RainYOffset;
        public float RainCornerRadius = 7f;
        public float[] RainColor = { 0.30f, 0.76f, 1f, 0.74f };
        public float[] RainEndColor = { 0.48f, 0.34f, 1f, 0.08f };
        public float[] RainRightColor = { 0.45f, 0.75f, 1f, 0.74f };
        public bool RainGradientEnabled;
        public int RainGradientMode; // 0=随雨段 UV，1=按距按键高度
        public float RainGradientHeight = 1f;
        public float RainGradientPower = 1f;
        public bool RainHorizontalGradientEnabled;
        public int RainFadeMode = 1;
        public float RainFadeHeight = 0.22f;
        public float RainFadePower = 1f;
        public bool RainOutlineEnabled;
        public float[] RainOutlineColor = { 0f, 0f, 0f, 0.85f };
        public float RainOutlineThickness = 1f;
        public bool RainShadowEnabled;
        public float[] RainShadowColor = { 0f, 0f, 0f, 0.35f };
        public float[] RainShadowOffset = { 2f, -2f };
        public float RainShadowSoftness = 12f;
        public float RainShadowStrength = 1f;

        public bool UseCustomAnimation;
        public bool PressAnimationEnabled;
        public float PressAnimationDuration = 0.12f;
        public string PressAnimationEasing = "ease-out-quad";
        public bool PressAnimationAffectColors = true;
        public float PressAnimationScale = 0.94f;
        public float PressAnimationOffsetX;
        public float PressAnimationOffsetY;
    }

    [Serializable]
    public sealed class KvProfile
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Name = L("新配置", "New profile");
        public bool Enabled = true;
        public bool ShowInGame = true;
        public bool OnlyShowPlaying;
        public int TotalHits;
        public List<KvKey> Keys = new List<KvKey>();
        public int LayoutVersion;

        public float OffsetX;
        public float OffsetY = -330f;
        public float Scale = 1f;
        public float KeyWidth = 72f;
        public float KeyHeight = 72f;
        public float Gap = 8f;
        public float CornerRadius = 10f;
        public float BorderThickness = 2f;
        public float LabelSize = 22f;
        public float CountSize = 13f;
        public bool HideCount;
        public bool ShowKps = true;
        public bool ShowTotal = true;

        public float[] BackgroundNormal = { 0.075f, 0.085f, 0.11f, 0.88f };
        public float[] BackgroundPressed = { 0.30f, 0.76f, 1f, 0.96f };
        public float[] BorderNormal = { 0.42f, 0.48f, 0.58f, 0.70f };
        public float[] BorderPressed = { 0.72f, 0.92f, 1f, 1f };
        public float[] TextNormal = { 0.94f, 0.96f, 1f, 1f };
        public float[] TextPressed = { 0.015f, 0.025f, 0.04f, 1f };

        public string FontPath = string.Empty;
        public bool LabelOutlineEnabled;
        public float[] LabelOutlineColor = { 0f, 0f, 0f, 1f };
        public float LabelOutlineThickness = 1f;
        public bool CountOutlineEnabled;
        public float[] CountOutlineColor = { 0f, 0f, 0f, 1f };
        public float CountOutlineThickness = 1f;
        public bool LabelShadowEnabled;
        public float[] LabelShadowColor = { 0f, 0f, 0f, 0.7f };
        public float[] LabelShadowOffset = { 2f, -2f };
        public float LabelShadowSoftness;
        public bool CountShadowEnabled;
        public float[] CountShadowColor = { 0f, 0f, 0f, 0.7f };
        public float[] CountShadowOffset = { 2f, -2f };
        public float CountShadowSoftness;

        public bool RainEnabled;
        public float RainSpeed = 620f;
        public float RainMaxHeight = 420f;
        public float RainWidthRatio = 0.72f;
        public float RainWidthRatio2 = 0.55f;
        public float RainWidthRatio3 = 0.55f;
        public float RainWidthRatio4 = 0.55f;
        public float RainYOffset;
        public float RainYOffset2;
        public float RainYOffset3;
        public float RainYOffset4;
        public float RainCornerRadius = 7f;
        public float[] RainColor = { 0.30f, 0.76f, 1f, 0.74f };
        public float[] RainEndColor = { 0.48f, 0.34f, 1f, 0.08f };
        public float[] RainRightColor = { 0.45f, 0.75f, 1f, 0.74f };
        public float[] RainColor2 = { 0.50f, 0.80f, 1f, 0.74f };
        public float[] RainEndColor2 = { 0.25f, 1f, 0.80f, 0.08f };
        public float[] RainRightColor2 = { 1f, 0.65f, 0.35f, 0.74f };
        public float[] RainColor3 = { 0.85f, 0.45f, 1f, 0.74f };
        public float[] RainEndColor3 = { 0.55f, 0.20f, 0.90f, 0.08f };
        public float[] RainRightColor3 = { 0.95f, 0.55f, 1f, 0.74f };
        public float[] RainColor4 = { 1f, 0.80f, 0.30f, 0.74f };
        public float[] RainEndColor4 = { 0.95f, 0.50f, 0.15f, 0.08f };
        public float[] RainRightColor4 = { 1f, 0.90f, 0.45f, 0.74f };
        public int RainDepth1 = 0;
        public int RainDepth2 = 1;
        public int RainDepth3 = 2;
        public int RainDepth4 = 3;
        public bool RainGradientEnabled = true;
        public int RainGradientMode; // 0=随雨段 UV，1=按距按键高度
        public float RainGradientHeight = 1f;
        public float RainGradientPower = 1f;
        public bool RainHorizontalGradientEnabled;
        public int RainFadeMode = 1;
        public float RainFadeHeight = 0.22f;
        public float RainFadePower = 1f;
        public bool RainOutlineEnabled;
        public float[] RainOutlineColor = { 0f, 0f, 0f, 0.85f };
        public float RainOutlineThickness = 1f;
        public bool RainShadowEnabled;
        public float[] RainShadowColor = { 0f, 0f, 0f, 0.35f };
        public float[] RainShadowOffset = { 2f, -2f };
        public float RainShadowSoftness = 12f;
        public float RainShadowStrength = 1f;

        public bool PressAnimationEnabled;
        public float PressAnimationDuration = 0.12f;
        public string PressAnimationEasing = "ease-out-quad";
        public bool PressAnimationAffectColors = true;
        public float PressAnimationScale = 0.94f;
        public float PressAnimationOffsetX;
        public float PressAnimationOffsetY;

        [XmlIgnore] public int CurrentKps;
    }

    [Serializable]
    public sealed class KvSettings
    {
        // Defer XmlSerializer generation until a load/save actually happens. Keeping
        // Value access inside the existing try blocks also preserves the old fallback
        // behavior if serializer generation fails on an unusual Unity runtime.
        private static readonly Lazy<XmlSerializer> Serializer =
            new Lazy<XmlSerializer>(() => new XmlSerializer(typeof(KvSettings)));
        public bool Enabled = true;
        public bool OnlyShowPlaying;
        public bool ClearStatsOnLaunch;
        public float KpsWindowSeconds = 1f;
        public bool PackageIncludeStats = false;
        public List<KvProfile> Profiles = new List<KvProfile>();

        internal static KvSettings Load(string path, IModuleLogger logger)
        {
            try
            {
                if (File.Exists(path))
                {
                    using (FileStream stream = File.OpenRead(path))
                        return Serializer.Value.Deserialize(stream) as KvSettings ?? CreateDefault();
                }
            }
            catch (Exception ex)
            {
                logger?.Warning(L("KeyViewer 配置读取失败，已使用默认配置：", "Could not read KeyViewer settings, using defaults: ") + ex.Message);
            }
            return CreateDefault();
        }

        internal void Save(string path, IModuleLogger logger)
        {
            try
            {
                Normalize();
                using (FileStream stream = File.Create(path)) Serializer.Value.Serialize(stream, this);
            }
            catch (Exception ex)
            {
                logger?.Warning(L("KeyViewer 配置保存失败：", "Could not save KeyViewer settings: ") + ex.Message);
            }
        }

        internal static KvSettings CreateDefault()
        {
            var settings = new KvSettings();
            settings.Profiles.Add(CreateProfile(L("默认配置", "Default profile")));
            return settings;
        }

        internal static KvProfile CreateProfile(string name)
        {
            var profile = new KvProfile { Name = string.IsNullOrWhiteSpace(name) ? L("新配置", "New profile") : name.Trim(), LayoutVersion = 1 };
            KeyCode[] keys = { KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.Semicolon };
            float totalWidth = keys.Length * profile.KeyWidth + (keys.Length - 1) * profile.Gap;
            float startX = -totalWidth * 0.5f + profile.KeyWidth * 0.5f;
            for (int i = 0; i < keys.Length; i++)
            {
                KvKey key = CreateKey(keys[i]);
                key.PositionX = startX + i * (profile.KeyWidth + profile.Gap);
                profile.Keys.Add(key);
            }
            profile.Keys.Add(CreateStat(1, -totalWidth * 0.5f + 72f, profile.KeyHeight * 0.5f + 32f));
            profile.Keys.Add(CreateStat(2, totalWidth * 0.5f - 72f, profile.KeyHeight * 0.5f + 32f));
            return profile;
        }

        internal static KvKey CreateKey(KeyCode key)
        {
            return new KvKey { NodeType = 0, Bind = key, Label = KeyName(key) };
        }

        internal static KvKey CreateStat(int nodeType, float x = 0f, float y = 0f)
        {
            return new KvKey
            {
                NodeType = nodeType == 2 ? 2 : 1,
                Bind = KeyCode.None,
                Label = nodeType == 2 ? "TOTAL" : "KPS",
                PositionX = x,
                PositionY = y,
                Width = 144f,
                Height = 42f,
                RainEnabled = false,
                HideCount = false
            };
        }

        internal void Normalize()
        {
            KpsWindowSeconds = Finite(KpsWindowSeconds, 0.25f, 3f, 1f);
            if (Profiles == null) Profiles = new List<KvProfile>();
            Profiles.RemoveAll(profile => profile == null);
            if (Profiles.Count == 0) Profiles.Add(CreateProfile(L("默认配置", "Default profile")));
            var profileIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Profiles.Count; i++)
            {
                KvProfile profile = Profiles[i];
                if (string.IsNullOrWhiteSpace(profile.Id) || !profileIds.Add(profile.Id))
                {
                    profile.Id = Guid.NewGuid().ToString("N");
                    profileIds.Add(profile.Id);
                }
                profile.Name = string.IsNullOrWhiteSpace(profile.Name) ? L("配置 ", "Profile ") + (i + 1) : profile.Name.Trim();
                profile.OffsetX = Finite(profile.OffsetX, -4000f, 4000f, 0f);
                profile.OffsetY = Finite(profile.OffsetY, -4000f, 4000f, -330f);
                profile.Scale = Finite(profile.Scale, 0.1f, 4f, 1f);
                profile.KeyWidth = Finite(profile.KeyWidth, 1f, 400f, 72f);
                profile.KeyHeight = Finite(profile.KeyHeight, 1f, 400f, 72f);
                profile.Gap = Finite(profile.Gap, 0f, 160f, 8f);
                profile.CornerRadius = Finite(profile.CornerRadius, 0f, 200f, 10f);
                profile.BorderThickness = Finite(profile.BorderThickness, 0f, 30f, 2f);
                profile.LabelSize = Finite(profile.LabelSize, 8f, 160f, 22f);
                profile.CountSize = Finite(profile.CountSize, 8f, 120f, 13f);
                if (profile.FontPath == null) profile.FontPath = string.Empty;
                profile.RainSpeed = Finite(profile.RainSpeed, 50f, 3000f, 620f);
                profile.RainMaxHeight = Finite(profile.RainMaxHeight, 40f, 1800f, 420f);
                profile.RainWidthRatio = Finite(profile.RainWidthRatio, 0.05f, 2f, 0.72f);
                profile.RainCornerRadius = Finite(profile.RainCornerRadius, 0f, 200f, 7f);
                NormalizeColor(ref profile.BackgroundNormal, 0.075f, 0.085f, 0.11f, 0.88f);
                NormalizeColor(ref profile.BackgroundPressed, 0.30f, 0.76f, 1f, 0.96f);
                NormalizeColor(ref profile.BorderNormal, 0.42f, 0.48f, 0.58f, 0.70f);
                NormalizeColor(ref profile.BorderPressed, 0.72f, 0.92f, 1f, 1f);
                NormalizeColor(ref profile.TextNormal, 0.94f, 0.96f, 1f, 1f);
                NormalizeColor(ref profile.TextPressed, 0.015f, 0.025f, 0.04f, 1f);
                NormalizeColor(ref profile.LabelOutlineColor, 0f, 0f, 0f, 1f);
                NormalizeColor(ref profile.CountOutlineColor, 0f, 0f, 0f, 1f);
                NormalizeColor(ref profile.LabelShadowColor, 0f, 0f, 0f, 0.7f);
                NormalizeColor(ref profile.CountShadowColor, 0f, 0f, 0f, 0.7f);
                NormalizePair(ref profile.LabelShadowOffset, 2f, -2f);
                NormalizePair(ref profile.CountShadowOffset, 2f, -2f);
                profile.LabelOutlineThickness = Finite(profile.LabelOutlineThickness, 0f, 8f, 1f);
                profile.CountOutlineThickness = Finite(profile.CountOutlineThickness, 0f, 8f, 1f);
                profile.LabelShadowSoftness = NonNegativeFinite(profile.LabelShadowSoftness, 0f);
                profile.CountShadowSoftness = NonNegativeFinite(profile.CountShadowSoftness, 0f);
                NormalizeColor(ref profile.RainColor, 0.30f, 0.76f, 1f, 0.74f);
                NormalizeColor(ref profile.RainEndColor, 0.48f, 0.34f, 1f, 0.08f);
                NormalizeColor(ref profile.RainRightColor, 0.45f, 0.75f, 1f, 0.74f);
                NormalizeColor(ref profile.RainColor2, 0.50f, 0.80f, 1f, 0.74f);
                NormalizeColor(ref profile.RainEndColor2, 0.25f, 1f, 0.80f, 0.08f);
                NormalizeColor(ref profile.RainRightColor2, 1f, 0.65f, 0.35f, 0.74f);
                NormalizeColor(ref profile.RainColor3, 0.85f, 0.45f, 1f, 0.74f);
                NormalizeColor(ref profile.RainEndColor3, 0.55f, 0.20f, 0.90f, 0.08f);
                NormalizeColor(ref profile.RainRightColor3, 0.95f, 0.55f, 1f, 0.74f);
                NormalizeColor(ref profile.RainColor4, 1f, 0.80f, 0.30f, 0.74f);
                NormalizeColor(ref profile.RainEndColor4, 0.95f, 0.50f, 0.15f, 0.08f);
                NormalizeColor(ref profile.RainRightColor4, 1f, 0.90f, 0.45f, 0.74f);
                NormalizeColor(ref profile.RainOutlineColor, 0f, 0f, 0f, 0.85f);
                NormalizeColor(ref profile.RainShadowColor, 0f, 0f, 0f, 0.35f);
                NormalizePair(ref profile.RainShadowOffset, 2f, -2f);
                profile.RainShadowSoftness = Finite(profile.RainShadowSoftness, 0f, 64f, 12f);
                profile.RainShadowStrength = Finite(profile.RainShadowStrength, 0f, 1f, 1f);
                profile.RainWidthRatio = Finite(profile.RainWidthRatio, 0.05f, 2f, 0.72f);
                profile.RainWidthRatio2 = Finite(profile.RainWidthRatio2, 0.05f, 2f, 0.55f);
                profile.RainWidthRatio3 = Finite(profile.RainWidthRatio3, 0.05f, 2f, 0.55f);
                profile.RainWidthRatio4 = Finite(profile.RainWidthRatio4, 0.05f, 2f, 0.55f);
                profile.RainYOffset = Finite(profile.RainYOffset, -400f, 400f, 0f);
                profile.RainYOffset2 = Finite(profile.RainYOffset2, -400f, 400f, 0f);
                profile.RainYOffset3 = Finite(profile.RainYOffset3, -400f, 400f, 0f);
                profile.RainYOffset4 = Finite(profile.RainYOffset4, -400f, 400f, 0f);
                profile.RainDepth1 = Mathf.Clamp(profile.RainDepth1, -1000, 1000);
                profile.RainDepth2 = Mathf.Clamp(profile.RainDepth2, -1000, 1000);
                profile.RainDepth3 = Mathf.Clamp(profile.RainDepth3, -1000, 1000);
                profile.RainDepth4 = Mathf.Clamp(profile.RainDepth4, -1000, 1000);
                profile.RainFadeMode = Mathf.Clamp(profile.RainFadeMode, 0, 1);
                profile.RainGradientMode = Mathf.Clamp(profile.RainGradientMode, 0, 1);
                profile.RainGradientHeight = Finite(profile.RainGradientHeight, 0.05f, 3f, 1f);
                profile.RainGradientPower = Finite(profile.RainGradientPower, 0.1f, 5f, 1f);
                profile.RainFadeHeight = Finite(profile.RainFadeHeight, 0.05f, 3f, 0.22f);
                profile.RainFadePower = Finite(profile.RainFadePower, 0.1f, 5f, 1f);
                profile.RainOutlineThickness = Finite(profile.RainOutlineThickness, 0f, 10f, 1f);
                profile.PressAnimationDuration = Finite(profile.PressAnimationDuration, 0.01f, 2f, 0.12f);
                profile.PressAnimationScale = Finite(profile.PressAnimationScale, 0.2f, 3f, 0.94f);
                profile.PressAnimationOffsetX = Finite(profile.PressAnimationOffsetX, -200f, 200f, 0f);
                profile.PressAnimationOffsetY = Finite(profile.PressAnimationOffsetY, -200f, 200f, 0f);
                if (string.IsNullOrWhiteSpace(profile.PressAnimationEasing)) profile.PressAnimationEasing = "ease-out-quad";
                if (profile.Keys == null) profile.Keys = new List<KvKey>();
                profile.Keys.RemoveAll(key => key == null);
                if (profile.LayoutVersion <= 0)
                {
                    List<KvKey> inputKeys = profile.Keys.FindAll(key => key.NodeType == 0);
                    float totalWidth = inputKeys.Count > 0 ? inputKeys.Count * profile.KeyWidth + (inputKeys.Count - 1) * profile.Gap : 0f;
                    float startX = -totalWidth * 0.5f + profile.KeyWidth * 0.5f;
                    for (int keyIndex = 0; keyIndex < inputKeys.Count; keyIndex++)
                        inputKeys[keyIndex].PositionX = startX + keyIndex * (profile.KeyWidth + profile.Gap);
                    if (profile.ShowKps) profile.Keys.Add(CreateStat(1, -totalWidth * 0.5f + 72f, profile.KeyHeight * 0.5f + 32f));
                    if (profile.ShowTotal) profile.Keys.Add(CreateStat(2, totalWidth * 0.5f - 72f, profile.KeyHeight * 0.5f + 32f));
                    profile.LayoutVersion = 1;
                }
                var keyIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (KvKey key in profile.Keys)
                {
                    if (string.IsNullOrWhiteSpace(key.Id) || !keyIds.Add(key.Id))
                    {
                        key.Id = Guid.NewGuid().ToString("N");
                        keyIds.Add(key.Id);
                    }
                    key.NodeType = Mathf.Clamp(key.NodeType, 0, 4);
                    // 空字符串是有效的显示内容。旧逻辑把用户刚清空的文字立刻
                    // 改回按键名，并触发运行时布局重建，媒体节点因此会反复重建。
                    if (key.Label == null) key.Label = key.NodeType == 1 ? "KPS" : key.NodeType == 2 ? "TOTAL" : KeyName(key.Bind);
                    // 旧版纯装饰框、背景视频同样使用 NodeType 0/4，但没有绑定按键。
                    // 早期 Sonnet 导入器把它们的空文字错误转换为了 "None"。
                    if (key.Bind == KeyCode.None && (key.NodeType == 0 || key.NodeType == 3 || key.NodeType == 4)
                        && string.Equals(key.Label, "None", StringComparison.Ordinal)) key.Label = string.Empty;
                    key.PositionX = Finite(key.PositionX, -10000f, 10000f, 0f);
                    key.PositionY = Finite(key.PositionY, -10000f, 10000f, 0f);
                    key.Width = key.Width < 0f ? -1f : Finite(key.Width, 1f, 1200f, profile.KeyWidth);
                    key.Height = key.Height < 0f ? -1f : Finite(key.Height, 1f, 1200f, profile.KeyHeight);
                    key.CornerRadius = key.CornerRadius < 0f ? -1f : Finite(key.CornerRadius, 0f, 400f, profile.CornerRadius);
                    key.BorderThickness = key.BorderThickness < 0f ? -1f : Finite(key.BorderThickness, 0f, 60f, profile.BorderThickness);
                    key.LabelSize = key.LabelSize < 0f ? -1f : Finite(key.LabelSize, 8f, 240f, profile.LabelSize);
                    key.CountSize = key.CountSize < 0f ? -1f : Finite(key.CountSize, 8f, 240f, profile.CountSize);
                    if (key.LabelFontPath == null) key.LabelFontPath = string.Empty;
                    if (key.CountFontPath == null) key.CountFontPath = string.Empty;
                    key.MediaScale = Finite(key.MediaScale, 0.05f, 10f, 1f);
                    key.MediaOffsetX = Finite(key.MediaOffsetX, -2000f, 2000f, 0f);
                    key.MediaOffsetY = Finite(key.MediaOffsetY, -2000f, 2000f, 0f);
                    key.MediaFit = Mathf.Clamp(key.MediaFit, 0, 4);
                    key.Opacity = Finite(key.Opacity, 0f, 1f, 1f);
                    key.Depth = Mathf.Clamp(key.Depth, -1000, 1000);
                    key.RainRow = Mathf.Clamp(key.RainRow, 0, 4);
                    key.LabelOffsetX = Finite(key.LabelOffsetX, -1000f, 1000f, 0f);
                    key.LabelOffsetY = Finite(key.LabelOffsetY, -1000f, 1000f, 0f);
                    key.CountOffsetX = Finite(key.CountOffsetX, -1000f, 1000f, 0f);
                    key.CountOffsetY = Finite(key.CountOffsetY, -1000f, 1000f, 0f);
                    key.CountAlignment = Mathf.Clamp(key.CountAlignment, 0, 2);
                    NormalizeColor(ref key.BackgroundNormal, profile.BackgroundNormal[0], profile.BackgroundNormal[1], profile.BackgroundNormal[2], profile.BackgroundNormal[3]);
                    NormalizeColor(ref key.BackgroundPressed, profile.BackgroundPressed[0], profile.BackgroundPressed[1], profile.BackgroundPressed[2], profile.BackgroundPressed[3]);
                    NormalizeColor(ref key.BorderNormal, profile.BorderNormal[0], profile.BorderNormal[1], profile.BorderNormal[2], profile.BorderNormal[3]);
                    NormalizeColor(ref key.BorderPressed, profile.BorderPressed[0], profile.BorderPressed[1], profile.BorderPressed[2], profile.BorderPressed[3]);
                    NormalizeColor(ref key.TextNormal, profile.TextNormal[0], profile.TextNormal[1], profile.TextNormal[2], profile.TextNormal[3]);
                    NormalizeColor(ref key.TextPressed, profile.TextPressed[0], profile.TextPressed[1], profile.TextPressed[2], profile.TextPressed[3]);
                    NormalizeColor(ref key.LabelOutlineColor, 0f, 0f, 0f, 1f);
                    NormalizeColor(ref key.CountOutlineColor, 0f, 0f, 0f, 1f);
                    NormalizeColor(ref key.LabelShadowColor, 0f, 0f, 0f, 0.7f);
                    NormalizeColor(ref key.CountShadowColor, 0f, 0f, 0f, 0.7f);
                    NormalizePair(ref key.LabelShadowOffset, 2f, -2f);
                    NormalizePair(ref key.CountShadowOffset, 2f, -2f);
                    key.LabelOutlineThickness = Finite(key.LabelOutlineThickness, 0f, 8f, 1f);
                    key.CountOutlineThickness = Finite(key.CountOutlineThickness, 0f, 8f, 1f);
                    key.LabelShadowSoftness = NonNegativeFinite(key.LabelShadowSoftness, 0f);
                    key.CountShadowSoftness = NonNegativeFinite(key.CountShadowSoftness, 0f);
                    key.RainWidthRatio = Finite(key.RainWidthRatio, 0.05f, 2f, 0.72f);
                    key.RainYOffset = Finite(key.RainYOffset, -400f, 400f, 0f);
                    key.RainCornerRadius = Finite(key.RainCornerRadius, 0f, 200f, 7f);
                    NormalizeColor(ref key.RainColor, 0.30f, 0.76f, 1f, 0.74f);
                    NormalizeColor(ref key.RainEndColor, 0.48f, 0.34f, 1f, 0.08f);
                    NormalizeColor(ref key.RainRightColor, 0.45f, 0.75f, 1f, 0.74f);
                    key.RainFadeMode = Mathf.Clamp(key.RainFadeMode, 0, 1);
                    key.RainGradientMode = Mathf.Clamp(key.RainGradientMode, 0, 1);
                    key.RainGradientHeight = Finite(key.RainGradientHeight, 0.05f, 3f, 1f);
                    key.RainGradientPower = Finite(key.RainGradientPower, 0.1f, 5f, 1f);
                    key.RainFadeHeight = Finite(key.RainFadeHeight, 0.05f, 3f, 0.22f);
                    key.RainFadePower = Finite(key.RainFadePower, 0.1f, 5f, 1f);
                    NormalizeColor(ref key.RainOutlineColor, 0f, 0f, 0f, 0.85f);
                    key.RainOutlineThickness = Finite(key.RainOutlineThickness, 0f, 10f, 1f);
                    NormalizeColor(ref key.RainShadowColor, 0f, 0f, 0f, 0.35f);
                    NormalizePair(ref key.RainShadowOffset, 2f, -2f);
                    key.RainShadowSoftness = Finite(key.RainShadowSoftness, 0f, 64f, 12f);
                    key.RainShadowStrength = Finite(key.RainShadowStrength, 0f, 1f, 1f);
                    key.PressAnimationDuration = Finite(key.PressAnimationDuration, 0.01f, 2f, 0.12f);
                    key.PressAnimationScale = Finite(key.PressAnimationScale, 0.2f, 3f, 0.94f);
                    key.PressAnimationOffsetX = Finite(key.PressAnimationOffsetX, -200f, 200f, 0f);
                    key.PressAnimationOffsetY = Finite(key.PressAnimationOffsetY, -200f, 200f, 0f);
                    if (string.IsNullOrWhiteSpace(key.PressAnimationEasing)) key.PressAnimationEasing = "ease-out-quad";
                }
            }
        }

        internal KvProfile FindProfile(string id)
        {
            if (Profiles == null) return null;
            for (int i = 0; i < Profiles.Count; i++)
                if (Profiles[i] != null && string.Equals(Profiles[i].Id, id, StringComparison.Ordinal)) return Profiles[i];
            return null;
        }

        internal void ClearCounts()
        {
            foreach (KvProfile profile in Profiles)
            {
                profile.TotalHits = 0;
                profile.CurrentKps = 0;
                foreach (KvKey key in profile.Keys) key.HitCount = 0;
            }
        }

        internal static string KeyName(KeyCode key)
        {
            string name = key.ToString();
            if (name.StartsWith("Alpha", StringComparison.Ordinal)) return name.Substring(5);
            if (name.StartsWith("Keypad", StringComparison.Ordinal)) return name.Substring(6);
            switch (key)
            {
                case KeyCode.LeftShift: return "LS";
                case KeyCode.RightShift: return "RS";
                case KeyCode.LeftControl: return "LC";
                case KeyCode.RightControl: return "RC";
                case KeyCode.Space: return "Spc";
                case KeyCode.Return: return "Ent";
                case KeyCode.Backspace: return "Bsp";
                case KeyCode.Escape: return "Esc";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                default: return name;
            }
        }

        internal static bool IsMediaNode(KvKey key)
        {
            return key != null && (key.NodeType == 3 || key.NodeType == 4);
        }

        // New image and video keys show only their content: no background, no border.
        internal static KvKey CreateMedia(int nodeType)
        {
            var clear = new[] { 0f, 0f, 0f, 0f };
            return new KvKey
            {
                NodeType = nodeType == 4 ? 4 : 3,
                Bind = KeyCode.A,
                Label = string.Empty,
                MediaFit = 1,
                Width = 180f,
                Height = 100f,
                RainEnabled = true,
                RainRow = 1,
                UseCustomStyle = true,
                BorderThickness = 0f,
                BackgroundNormal = (float[])clear.Clone(),
                BackgroundPressed = (float[])clear.Clone(),
                BorderNormal = (float[])clear.Clone(),
                BorderPressed = (float[])clear.Clone()
            };
        }

        internal static bool IsInputNode(KvKey key)
        {
            return key != null && key.Bind != KeyCode.None
                && (key.NodeType == 0 || key.NodeType == 3 || key.NodeType == 4);
        }

        // The profile-wide "hide key count" switch covers key nodes only. KPS and TOTAL nodes show
        // nothing but their number, so they follow their own per-node setting alone.
        internal static bool HidesCount(KvProfile profile, KvKey key)
        {
            if (key == null) return false;
            if (key.HideCount) return true;
            return profile != null && profile.HideCount && key.NodeType != 1 && key.NodeType != 2;
        }

        private static float Finite(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = fallback;
            return Mathf.Clamp(value, min, max);
        }

        private static float NonNegativeFinite(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            return value < 0f ? 0f : value;
        }

        private static void NormalizeColor(ref float[] value, float r, float g, float b, float a)
        {
            if (value == null || value.Length != 4) value = new[] { r, g, b, a };
            for (int i = 0; i < 4; i++) value[i] = Finite(value[i], 0f, 1f, i == 3 ? a : 1f);
        }

        private static void NormalizePair(ref float[] value, float x, float y)
        {
            if (value == null || value.Length != 2) value = new[] { x, y };
            if (float.IsNaN(value[0]) || float.IsInfinity(value[0])) value[0] = x;
            if (float.IsNaN(value[1]) || float.IsInfinity(value[1])) value[1] = y;
        }
    }
}
