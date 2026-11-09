using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.KeyViewer.Preview
{
    // 只描述转换 Sonnet KV 所需的旧版字段。XmlSerializer 会忽略其余旧字段，
    // 因而不需要把旧 CheryTools 的整套模型重新带进模块。
    [Serializable]
    [XmlRoot("KeyViewerPackage")]
    public sealed class LegacyKvPackage
    {
        public int FormatVersion;
        public int ExportScreenWidth;
        public int ExportScreenHeight;

        [XmlArrayItem("KVConfiguration")]
        public List<LegacyKvProfile> KeyViewerConfigurations = new List<LegacyKvProfile>();
    }

    [Serializable]
    [XmlRoot("Settings")]
    public sealed class LegacyCytSettingsKvPackage
    {
        public int LastKnownScreenWidth;
        public int LastKnownScreenHeight;
        public int KeyViewerLayoutTab;

        [XmlArrayItem("KVConfiguration")]
        public List<LegacyKvProfile> KeyViewerConfigurations = new List<LegacyKvProfile>();

        [XmlArrayItem("KVNode")]
        public List<LegacyKvNode> Layout16K;
        [XmlArrayItem("KVNode")]
        public List<LegacyKvNode> Layout12K;
        [XmlArrayItem("KVNode")]
        public List<LegacyKvNode> Layout10K;
        [XmlArrayItem("KVNode")]
        public List<LegacyKvNode> Layout8K;
    }

    [Serializable]
    [XmlRoot("CytPackageInfo")]
    public sealed class LegacyCytPackageInfo
    {
        public int FormatVersion = 1;
        public int ExportScreenWidth;
        public int ExportScreenHeight;
    }

    [Serializable]
    public sealed class LegacyKvProfile
    {
        public string Name = L("导入配置", "Imported profile");
        public bool IsEnabled = true;
        public bool ShowInGame = true;
        public bool OnlyShowPlaying;

        [XmlArrayItem("KVNode")]
        public List<LegacyKvNode> Nodes = new List<LegacyKvNode>();

        public float Scale = 1f;
        public float BorderThickness = 2f;
        public bool HideCountText;
        public string FontPath = string.Empty;

        public float[] ColorBgNormal = { 0.2f, 0.1f, 0.3f, 0.8f };
        public float[] ColorBgPressed = { 0.5f, 0.2f, 0.8f, 1f };
        public float[] ColorBorderNormal = { 0.6f, 0.3f, 0.9f, 0.8f };
        public float[] ColorBorderPressed = { 0.8f, 0.4f, 1f, 1f };
        public float[] ColorTextNormal = { 0.8f, 0.8f, 0.8f, 1f };
        public float[] ColorTextPressed = { 1f, 1f, 1f, 1f };
        public float[] ColorKps = { 1f, 1f, 1f, 1f };
        public float[] ColorTotal = { 1f, 1f, 1f, 1f };

        public bool KeyTextOutlineEnabled;
        public float[] KeyTextOutlineColor = { 0f, 0f, 0f, 1f };
        public float KeyTextOutlineThickness = 1f;
        public bool CountTextOutlineEnabled;
        public float[] CountTextOutlineColor = { 0f, 0f, 0f, 1f };
        public float CountTextOutlineThickness = 1f;
        public bool KeyTextShadowEnabled;
        public float[] KeyTextShadowColor = { 0f, 0f, 0f, 0.7f };
        public float[] KeyTextShadowOffset = { 2f, 2f };
        public bool CountTextShadowEnabled;
        public float[] CountTextShadowColor = { 0f, 0f, 0f, 0.7f };
        public float[] CountTextShadowOffset = { 2f, 2f };

        public bool EnableKeyRain;
        public float KeyRainSpeed = 800f;
        public float KeyRainMaxHeight = 400f;
        public float KeyRainWidthRatio1 = 0.8f;
        public float KeyRainWidthRatio2 = 0.4f;
        public float KeyRainYOffsetRow1;
        public float KeyRainYOffsetRow2;
        public float KeyRainCornerRadius;
        public float[] KeyRainColorRow1 = { 0.8f, 0.5f, 1f, 0.8f };
        public float[] KeyRainColorRow2 = { 0.5f, 0.8f, 1f, 0.8f };
        public bool KeyRainGradientEnabled;
        public bool KeyRainHorizontalGradientEnabled;
        public float[] KeyRainGradientEndColorRow1 = { 1f, 0.25f, 0.8f, 0.8f };
        public float[] KeyRainGradientEndColorRow2 = { 0.25f, 1f, 0.8f, 0.8f };
        public float[] KeyRainHorizontalGradientEndColorRow1 = { 0.45f, 0.75f, 1f, 0.8f };
        public float[] KeyRainHorizontalGradientEndColorRow2 = { 1f, 0.65f, 0.35f, 0.8f };
        public int KeyRainGradientMode;
        public float KeyRainGradientHeight = 1f;
        public float KeyRainGradientPower = 1f;
        public int KeyRainFadeMode = 1;
        public float KeyRainFadeHeight = 0.22f;
        public float KeyRainFadePower = 1f;
        public bool KeyRainOutlineEnabled;
        public float[] KeyRainOutlineColor = { 0f, 0f, 0f, 1f };
        public float KeyRainOutlineThickness = 2f;
        public bool KeyRainShadowEnabled;
        public float[] KeyRainShadowColor = { 0f, 0f, 0f, 0.35f };
        public float[] KeyRainShadowOffset = { 0f, 0f };
        public float KeyRainShadowSoftness = 12f;
        public float KeyRainShadowStrength = 1f;

        public bool KeyPressAnimationEnabled;
        public float KeyPressAnimationDuration = 0.12f;
        public string KeyPressAnimationEasing = "ease-out-quad";
        public bool KeyPressAnimationAffectColors = true;
        public float KeyPressAnimationScale = 1f;
        public float KeyPressAnimationOffsetX;
        public float KeyPressAnimationOffsetY;
    }

    [Serializable]
    public sealed class LegacyKvNode
    {
        public int NodeType;
        public string KeyBind = "None";
        public string CustomText = string.Empty;
        public string ImagePath = string.Empty;
        public string VideoPath = string.Empty;
        public bool VideoLoop = true;
        public float VideoContentScale = 1f;
        public float VideoContentOffsetX;
        public float VideoContentOffsetY;
        public bool IsUnselectable;
        public float Opacity = 1f;
        public int Depth;
        public float PositionX;
        public float PositionY;
        public float Width = 50f;
        public float Height = 50f;
        public float Scale = 1f;
        public float BorderThickness = -1f;
        public float CornerRadius = -1f;
        public float TextScale = 1f;
        public float CountScale = 1f;
        public string KeyFontPath = string.Empty;
        public string CountFontPath = string.Empty;
        public float TextOffsetX;
        public float TextOffsetY;
        public float CountOffsetX;
        public float CountOffsetY;
        public int CountTextAlignment = 1;
        public bool HideCountText;

        public bool UseCustomOutline;
        public bool KeyTextOutlineEnabled;
        public float[] KeyTextOutlineColor = { 0f, 0f, 0f, 1f };
        public float KeyTextOutlineThickness = 1f;
        public bool CountTextOutlineEnabled;
        public float[] CountTextOutlineColor = { 0f, 0f, 0f, 1f };
        public float CountTextOutlineThickness = 1f;
        public bool UseCustomShadow;
        public bool KeyTextShadowEnabled;
        public float[] KeyTextShadowColor = { 0f, 0f, 0f, 0.7f };
        public float[] KeyTextShadowOffset = { 2f, 2f };
        public bool CountTextShadowEnabled;
        public float[] CountTextShadowColor = { 0f, 0f, 0f, 0.7f };
        public float[] CountTextShadowOffset = { 2f, 2f };

        public bool UseCustomColor;
        public float[] ColorBgNormal = { 0.2f, 0.2f, 0.2f, 0.8f };
        public float[] ColorBgPressed = { 0.8f, 0.8f, 0.8f, 0.8f };
        public float[] ColorBorderNormal = { 0.4f, 0.4f, 0.4f, 1f };
        public float[] ColorBorderPressed = { 1f, 1f, 1f, 1f };
        public float[] ColorTextNormal = { 1f, 1f, 1f, 1f };
        public float[] ColorTextPressed = { 0f, 0f, 0f, 1f };

        public bool EnableKeyRain = true;
        public int RainRow;
        public bool UseCustomRain;
        public float[] RainColor = { 0.8f, 0.5f, 1f, 0.8f };
        public float[] RainGradientEndColor = { 1f, 0.25f, 0.8f, 0.8f };
        public float[] RainHorizontalGradientEndColor = { 0.45f, 0.75f, 1f, 0.8f };
        public bool RainGradientEnabled;
        public bool RainHorizontalGradientEnabled;
        public int RainGradientMode;
        public float RainGradientHeight = 1f;
        public float RainGradientPower = 1f;
        public int RainFadeMode = 1;
        public float RainFadeHeight = 0.22f;
        public float RainFadePower = 1f;
        public float RainWidthRatio = 0.8f;
        public float RainYOffset;
        public float RainCornerRadius;
        public bool UseCustomRainShadow;
        public bool RainShadowEnabled;
        public float[] RainShadowColor = { 0f, 0f, 0f, 0.35f };
        public float[] RainShadowOffset = { 0f, 0f };
        public float RainShadowSoftness = 12f;
        public float RainShadowStrength = 1f;
        public bool UseCustomRainOutline;
        public bool RainOutlineEnabled;
        public float[] RainOutlineColor = { 0f, 0f, 0f, 1f };
        public float RainOutlineThickness = 2f;

        public bool UseCustomKeyPressAnimation;
        public bool KeyPressAnimationEnabled;
        public float KeyPressAnimationDuration = 0.12f;
        public string KeyPressAnimationEasing = "ease-out-quad";
        public bool KeyPressAnimationAffectColors = true;
        public float KeyPressAnimationScale = 1f;
        public float KeyPressAnimationOffsetX;
        public float KeyPressAnimationOffsetY;
    }
}
