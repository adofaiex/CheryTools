using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace CheryTools
{
    [Serializable]
    public class OverlayerImage
    {
        public bool IsEnabled = true;
        public bool ShowInGame = true;
        public string ImagePath = "";
        public float PositionX = 200f;
        public float PositionY = 200f;
        public float Scale = 1.0f;
        public float Rotation = 0f;
        public float Opacity = 1.0f;
        public int Depth = 0;

        public float PivotX = 0f;
        public float PivotY = 0f;

        public System.Collections.Generic.List<OverlayerAnimation> Animations = new System.Collections.Generic.List<OverlayerAnimation>();

        [System.Xml.Serialization.XmlIgnore]
        public float LastWidth = 100f;
        [System.Xml.Serialization.XmlIgnore]
        public float LastHeight = 100f;
    }

    [Serializable]
    public class OverlayerVideo
    {
        public string Name = "新视频";
        public bool IsEnabled = true;
        public bool ShowInGame = true;
        public string VideoPath = "";
        public bool Loop = true;
        public float ContentScale = 1.0f;
        public float ContentOffsetX = 0f;
        public float ContentOffsetY = 0f;
        public float PositionX = 200f;
        public float PositionY = 200f;
        public float Width = 320f;
        public float Height = 180f;
        public float Rotation = 0f;
        public float Opacity = 1.0f;
        public int Depth = 0;

        public float PivotX = 0f;
        public float PivotY = 0f;

        [System.Xml.Serialization.XmlIgnore]
        public float LastWidth = 320f;
        [System.Xml.Serialization.XmlIgnore]
        public float LastHeight = 180f;
    }

    public enum OverlayerProgressValueKind
    {
        Constant = 0,
        Progress = 1,
        Accuracy = 2,
        XAccuracy = 3,
        Kps = 4,
        CurrentClicksPerSecond = 5,
        MapPlayedTime = 6,
        MapTotalTime = 7,
        MusicPlayedTime = 8,
        MusicTotalTime = 9,
        PureCombo = 10,
        PerfectCombo = 11,
        Miss = 12,
        FailMiss = 13,
        FailOverload = 14
    }

    public enum OverlayerProgressFillDirection
    {
        LeftToRight = 0,
        RightToLeft = 1,
        BottomToTop = 2,
        TopToBottom = 3
    }

    [Serializable]
    public class OverlayerProgressValueSource
    {
        public OverlayerProgressValueKind Kind = OverlayerProgressValueKind.Constant;
        public double Constant = 0.0;

        public OverlayerProgressValueSource()
        {
        }

        public OverlayerProgressValueSource(OverlayerProgressValueKind kind, double constant = 0.0)
        {
            Kind = kind;
            Constant = constant;
        }
    }

    [Serializable]
    public class OverlayerProgressBar
    {
        public string Name = "新进度条";
        public bool IsEnabled = true;
        public bool ShowInGame = true;

        public OverlayerProgressValueSource ValueSource = new OverlayerProgressValueSource(OverlayerProgressValueKind.Progress);
        public OverlayerProgressValueSource MinSource = new OverlayerProgressValueSource(OverlayerProgressValueKind.Constant, 0.0);
        public OverlayerProgressValueSource MaxSource = new OverlayerProgressValueSource(OverlayerProgressValueKind.Constant, 100.0);

        public float PositionX = 50f;
        public float PositionY = 100f;
        public float Width = 300f;
        public float Height = 20f;
        public float Opacity = 1.0f;
        public int Depth = 0;

        public float PivotX = 0f;
        public float PivotY = 0f;

        public OverlayerProgressFillDirection FillDirection = OverlayerProgressFillDirection.LeftToRight;
        public bool Reverse = false;
        public bool ClampValue = true;

        public float[] BackgroundColor = new float[] { 0f, 0f, 0f, 0.45f };
        public float[] FillColor = new float[] { 0.2f, 0.75f, 1f, 0.95f };
        public bool EnableFillGradient = false;
        public float[] FillGradientStartColor = new float[] { 1f, 0.25f, 0.25f, 0.95f };
        public float[] FillGradientEndColor = new float[] { 0.25f, 1f, 0.35f, 0.95f };
        public float[] BorderColor = new float[] { 1f, 1f, 1f, 0.8f };
        public float BorderThickness = 1f;
        public float CornerRadius = 0f;

        public bool EnableShadow = false;
        public float[] ShadowColor = new float[] { 0f, 0f, 0f, 0.45f };
        public float[] ShadowOffset = new float[] { 2f, 2f };
        public float ShadowSoftness = 0f;

        [System.Xml.Serialization.XmlIgnore]
        public float LastWidth = 300f;
        [System.Xml.Serialization.XmlIgnore]
        public float LastHeight = 20f;
    }

    [Serializable]
    public class KVAxisGradient
    {
        public bool VerticalEnabled = false;
        public bool HorizontalEnabled = false;
        public float[] VerticalEndColor = new float[] { 1f, 1f, 1f, 1f };
        public float[] HorizontalEndColor = new float[] { 1f, 1f, 1f, 1f };
    }

    [Serializable]
    public class KVNode
    {
        public int NodeType = 0; // 0 = Normal Key, 1 = KPS Box, 2 = Total Box, 3 = Background Image Key, 4 = Video Key
        public string KeyBind = "None";
        public string CustomText = "";
        public string ImagePath = "";
        public string VideoPath = "";
        public bool VideoLoop = true;
        public float VideoContentScale = 1.0f;
        public float VideoContentOffsetX = 0f;
        public float VideoContentOffsetY = 0f;
        public bool IsUnselectable = false;
        public float Opacity = 1.0f;
        public int Depth = 0;
        
        public float PositionX = 0f;
        public float PositionY = 0f;
        
        public float Width = 50f;
        public float Height = 50f;
        public float BorderThickness = -1f;
        public float CornerRadius = -1f;
        
        public float Scale = 1f;
        public float TextOffsetY = 0f;
        public float TextOffsetX = 0f;
        public float TextScale = 1f;
        public float CountOffsetY = 0f;
        public float CountOffsetX = 0f;
        public float CountScale = 1f;
        public string KeyFontPath = "";
        public string CountFontPath = "";
        public bool HideCountText = false;

        public bool UseCustomOutline = false;
        public bool KeyTextOutlineEnabled = false;
        public float[] KeyTextOutlineColor = new float[] { 0f, 0f, 0f, 1f };
        public float KeyTextOutlineThickness = 1f;
        public bool CountTextOutlineEnabled = false;
        public float[] CountTextOutlineColor = new float[] { 0f, 0f, 0f, 1f };
        public float CountTextOutlineThickness = 1f;

        public bool UseCustomShadow = false;
        public bool KeyTextShadowEnabled = false;
        public float[] KeyTextShadowColor = new float[] { 0f, 0f, 0f, 0.7f };
        public float[] KeyTextShadowOffset = new float[] { 2f, 2f };
        public float KeyTextShadowSoftness = 0f;
        public bool CountTextShadowEnabled = false;
        public float[] CountTextShadowColor = new float[] { 0f, 0f, 0f, 0.7f };
        public float[] CountTextShadowOffset = new float[] { 2f, 2f };
        public float CountTextShadowSoftness = 0f;
        
        public bool UseCustomColor = false;
        public bool UseCustomColorGradient = false;
        public float[] ColorBgNormal = new float[] { 0.2f, 0.2f, 0.2f, 0.8f };
        public float[] ColorBgPressed = new float[] { 0.8f, 0.8f, 0.8f, 0.8f };
        public float[] ColorBorderNormal = new float[] { 0.4f, 0.4f, 0.4f, 1.0f };
        public float[] ColorBorderPressed = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
        public float[] ColorTextNormal = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
        public float[] ColorTextPressed = new float[] { 0.0f, 0.0f, 0.0f, 1.0f };
        public KVAxisGradient BackgroundGradientNormal = new KVAxisGradient();
        public KVAxisGradient BackgroundGradientPressed = new KVAxisGradient();
        public KVAxisGradient BorderGradientNormal = new KVAxisGradient();
        public KVAxisGradient BorderGradientPressed = new KVAxisGradient();
        public KVAxisGradient TextGradientNormal = new KVAxisGradient();
        public KVAxisGradient TextGradientPressed = new KVAxisGradient();

        public int RainRow = 0;
        public bool EnableKeyRain = true;
        public bool UseCustomRain = false;
        public float[] RainColor = new float[] { 0.8f, 0.5f, 1.0f, 0.8f };
        public bool RainGradientEnabled = false;
        public bool RainHorizontalGradientEnabled = false;
        public float[] RainGradientEndColor = new float[] { 1.0f, 0.25f, 0.8f, 0.8f };
        public float[] RainHorizontalGradientEndColor = new float[] { 0.45f, 0.75f, 1.0f, 0.8f };
        public int RainGradientMode = 0; // 0=UV, 1=Height mask
        public float RainFadeHeight = 1.0f;
        public float RainFadePower = 1.0f;
        public float RainGradientHeight = 1.0f;
        public float RainGradientPower = 1.0f;
        public float RainWidthRatio = 0.8f;
        public float RainYOffset = 0f;
        public float RainCornerRadius = 0f;
        public bool UseCustomRainShadow = false;
        public bool RainShadowEnabled = false;
        public float[] RainShadowColor = new float[] { 0f, 0f, 0f, 0.35f };
        public float[] RainShadowOffset = new float[] { 0f, 0f };
        public float RainShadowSoftness = 12f;
        public float RainShadowStrength = 1f;

        public bool UseCustomKeyPressAnimation = false;
        public bool KeyPressAnimationEnabled = false;
        public float KeyPressAnimationDuration = 0.12f;
        public string KeyPressAnimationEasing = "ease-out-quad";
        public bool KeyPressAnimationAffectColors = true;
        public float KeyPressAnimationScale = 1.0f;
        public float KeyPressAnimationOffsetX = 0f;
        public float KeyPressAnimationOffsetY = 0f;

        public int HitCount = 0;
        
        public KVNode() {}
        public KVNode(string bind, float px, float py) {
            KeyBind = bind;
            PositionX = px;
            PositionY = py;
        }
        public KVNode(int type, float px, float py, float w, float h) {
            NodeType = type;
            PositionX = px;
            PositionY = py;
            Width = w;
            Height = h;
        }
    }

    [Serializable]
    public class KVConfiguration
    {
        public string Name = "新配置";
        public bool IsEnabled = true;
        public bool ShowInGame = true;
        public System.Collections.Generic.List<KVNode> Nodes = new System.Collections.Generic.List<KVNode>();

        public bool AppearanceMigrated = false;
        public string FontPath = "";
        public float Scale = 1.0f;
        public float BorderThickness = 2.0f;
        public bool HideCountText = false;
        public float GlobalTextOffsetX = 0f;
        public float GlobalTextOffsetY = 0f;
        public float GlobalCountOffsetX = 0f;
        public float GlobalCountOffsetY = 0f;
        public float DefaultWidth = 50f;
        public float DefaultHeight = 50f;

        public float[] ColorBgNormal = new float[] { 0.2f, 0.1f, 0.3f, 0.8f };
        public float[] ColorBgPressed = new float[] { 0.5f, 0.2f, 0.8f, 1.0f };
        public float[] ColorBorderNormal = new float[] { 0.6f, 0.3f, 0.9f, 0.8f };
        public float[] ColorBorderPressed = new float[] { 0.8f, 0.4f, 1.0f, 1.0f };
        public float[] ColorTextNormal = new float[] { 0.8f, 0.8f, 0.8f, 1.0f };
        public float[] ColorTextPressed = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
        public KVAxisGradient BackgroundGradientNormal = new KVAxisGradient();
        public KVAxisGradient BackgroundGradientPressed = new KVAxisGradient();
        public KVAxisGradient BorderGradientNormal = new KVAxisGradient();
        public KVAxisGradient BorderGradientPressed = new KVAxisGradient();
        public KVAxisGradient TextGradientNormal = new KVAxisGradient();
        public KVAxisGradient TextGradientPressed = new KVAxisGradient();
        public float[] ColorKps = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
        public float[] ColorTotal = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };

        public bool KeyTextOutlineEnabled = false;
        public float[] KeyTextOutlineColor = new float[] { 0f, 0f, 0f, 1f };
        public float KeyTextOutlineThickness = 1f;
        public bool CountTextOutlineEnabled = false;
        public float[] CountTextOutlineColor = new float[] { 0f, 0f, 0f, 1f };
        public float CountTextOutlineThickness = 1f;

        public bool KeyTextShadowEnabled = false;
        public float[] KeyTextShadowColor = new float[] { 0f, 0f, 0f, 0.7f };
        public float[] KeyTextShadowOffset = new float[] { 2f, 2f };
        public float KeyTextShadowSoftness = 0f;
        public bool CountTextShadowEnabled = false;
        public float[] CountTextShadowColor = new float[] { 0f, 0f, 0f, 0.7f };
        public float[] CountTextShadowOffset = new float[] { 2f, 2f };
        public float CountTextShadowSoftness = 0f;

        public bool EnableKeyRain = false;
        public float KeyRainSpeed = 800.0f;
        public float KeyRainMaxHeight = 400.0f;
        public int KeyRainFadeMode = 1;
        public float KeyRainWidthRatio1 = 0.8f;
        public float KeyRainWidthRatio2 = 0.4f;
        public float KeyRainYOffsetRow1 = 0.0f;
        public float KeyRainYOffsetRow2 = 0.0f;
        public float KeyRainCornerRadius = 0f;
        public float[] KeyRainColorRow1 = new float[] { 0.8f, 0.5f, 1.0f, 0.8f };
        public float[] KeyRainColorRow2 = new float[] { 0.5f, 0.8f, 1.0f, 0.8f };
        public bool KeyRainGradientEnabled = false;
        public bool KeyRainHorizontalGradientEnabled = false;
        public float[] KeyRainGradientEndColorRow1 = new float[] { 1.0f, 0.25f, 0.8f, 0.8f };
        public float[] KeyRainGradientEndColorRow2 = new float[] { 0.25f, 1.0f, 0.8f, 0.8f };
        public float[] KeyRainHorizontalGradientEndColorRow1 = new float[] { 0.45f, 0.75f, 1.0f, 0.8f };
        public float[] KeyRainHorizontalGradientEndColorRow2 = new float[] { 1.0f, 0.65f, 0.35f, 0.8f };
        public int KeyRainGradientMode = 0; // 0=UV, 1=Height mask
        public float KeyRainFadeHeight = 1.0f;
        public float KeyRainFadePower = 1.0f;
        public float KeyRainGradientHeight = 1.0f;
        public float KeyRainGradientPower = 1.0f;
        public bool KeyRainShadowEnabled = false;
        public float[] KeyRainShadowColor = new float[] { 0f, 0f, 0f, 0.35f };
        public float[] KeyRainShadowOffset = new float[] { 0f, 0f };
        public float KeyRainShadowSoftness = 12f;
        public float KeyRainShadowStrength = 1f;

        public bool KeyPressAnimationEnabled = false;
        public float KeyPressAnimationDuration = 0.12f;
        public string KeyPressAnimationEasing = "ease-out-quad";
        public bool KeyPressAnimationAffectColors = true;
        public float KeyPressAnimationScale = 1.0f;
        public float KeyPressAnimationOffsetX = 0f;
        public float KeyPressAnimationOffsetY = 0f;
    }

    [Serializable]
    public class OverlayerText
    {
        public string Name = "新模块";
        public bool IsEnabled = true;
        public bool ShowInGame = true;
        public string TextFormat = "<color=#DA59FFFF>{fo}</color>  <color=#FF0000FF>{te}</color>  <color=#FF8E00FF>{ve}</color>  <color=#D7FF27FF>{ep}</color>  <color=#4DFF2DFF>{p}</color>  <color=#D7FF27FF>{lp}</color>  <color=#FF8E00FF>{vl}</color>  <color=#FF0000FF>{tl}</color>  <color=#DA59FFFF>{fm}</color>";
        public float PositionX = 50f;
        public float PositionY = 50f;
        public float FontSize = 32f;
        public float[] TextColor = new float[] { 1f, 1f, 1f, 1f };
        public int Alignment = 0; // 0: Left, 1: Center, 2: Right
        public string FontPath = ""; // 字体文件绝对路径

        public int Depth = 0;

        public bool EnableShadow = false;
        public float[] ShadowColor = new float[] { 0f, 0f, 0f, 1f };
        public float[] ShadowOffset = new float[] { 2f, 2f };
        public float ShadowSoftness = 0f;
        public bool EnableOutline = false;
        public float[] OutlineColor = new float[] { 0f, 0f, 0f, 1f };
        public float OutlineThickness = 1f;
        public float LineHeightOffset = 0f;
        public float LetterSpacing = 0f;
        
        public System.Collections.Generic.List<OverlayerAnimation> Animations = new System.Collections.Generic.List<OverlayerAnimation>();

        public float PivotX = 0f;
        public float PivotY = 0f;

        [System.Xml.Serialization.XmlIgnore]
        public float LastWidth = 100f;
        [System.Xml.Serialization.XmlIgnore]
        public float LastHeight = 40f;
    }

    [Serializable]
    public class GameUIElementSetting
    {
        public string Id = "";
        public bool Enabled = false;
        public bool Visible = true;
        public float OffsetX = 0f;
        public float OffsetY = 0f;
        public float Scale = 1f;
        public float Alpha = 1f;

        public GameUIElementSetting()
        {
        }

        public GameUIElementSetting(string id)
        {
            Id = id;
        }
    }

    public class Settings : UnityModManager.ModSettings
    {
        public bool OverlayerSystemEnabled = false;
        public bool OverlayerEditMode = false;
        public bool OverlayerOnlyShowPlaying = false;
        public System.Collections.Generic.List<OverlayerText> OverlayerTexts = new System.Collections.Generic.List<OverlayerText>();
        public System.Collections.Generic.List<OverlayerImage> OverlayerImages = new System.Collections.Generic.List<OverlayerImage>();
        public System.Collections.Generic.List<OverlayerVideo> OverlayerVideos = new System.Collections.Generic.List<OverlayerVideo>();
        public System.Collections.Generic.List<OverlayerProgressBar> OverlayerProgressBars = new System.Collections.Generic.List<OverlayerProgressBar>();
        // Visual Settings
        public bool EnableCustomPlanetColors = false;

        public float[] RedPlanetColor = new float[] { 1f, 0f, 0f, 1f };
        public float[] RedRingColor = new float[] { 1f, 0f, 0f, 0.4f };
        public float[] RedTailColor = new float[] { 1f, 0f, 0f, 1f };

        public float[] BluePlanetColor = new float[] { 0f, 0f, 1f, 1f };
        public float[] BlueRingColor = new float[] { 0f, 0f, 1f, 0.4f };
        public float[] BlueTailColor = new float[] { 0f, 0f, 1f, 1f };

        public float[] GreenPlanetColor = new float[] { 0f, 1f, 0f, 1f };
        public float[] GreenRingColor = new float[] { 0f, 1f, 0f, 0.4f };
        public float[] GreenTailColor = new float[] { 0f, 1f, 0f, 1f };

        public float[] ComboColor = new float[4] { 1f, 1f, 1f, 1f };
        public float[] AccuracyColor = new float[4] { 1f, 1f, 1f, 1f };
        
        public string LevelNameFont = "";

        public bool HideHitTextEnabled = false;
        public bool HideHitTextTooEarly = false;
        public bool HideHitTextVeryEarly = false;
        public bool HideHitTextEarlyPerfect = false;
        public bool HideHitTextPerfect = false;
        public bool HideHitTextLatePerfect = false;
        public bool HideHitTextVeryLate = false;
        public bool HideHitTextTooLate = false;
        public bool HideHitTextMultipress = false;
        public bool HideHitTextFailMiss = false;
        public bool HideHitTextFailOverload = false;
        public bool HideHitTextOverPress = false;

        public KeyCode ToggleMenuKey = KeyCode.Insert;
        public string Language = LocalizationManager.DefaultLanguage;
        public float ImGuiPanelScale = 1.0f;
        public float OverlayUpdateRate = 240.0f;
        public float ImageRenderScale = 1.0f;

        // Gameplay UI Settings
        public bool GameUIControlEnabled = false;
        public bool GameUIDeveloperUnlocked = false;
        public System.Collections.Generic.List<GameUIElementSetting> GameUIElements = new System.Collections.Generic.List<GameUIElementSetting>();

        // Optimization Settings
        public bool DisableAutoplaySpacePause = false;
        public bool DisablePlayModeScrollZoom = false;
        public bool ToolsAntiBounceKeys = false;
        public float ToolsAntiBounceIntervalMs = 50f;
        public bool ToolsLimitInput = false;
        public System.Collections.Generic.List<KeyCode> ToolsLimitedKeys = new System.Collections.Generic.List<KeyCode>();

        // Integration Settings
        public bool XPerfectIntegrationEnabled = false;

        // KeyViewer Settings
        public bool EnableKeyViewer = true;
        public bool LimitInput = false;
        public bool KeyViewerOnlyShowPlaying = false;
        public bool KeyViewerHideCountText = false;
        
        public int KeyViewerSelectedConfigIndex = 0;
        public float KeyViewerScale = 1.0f;
        public float KeyViewerBorderThickness = 2.0f;

        // Colors
        public float[] KeyViewerColorBgNormal = new float[] { 0.2f, 0.1f, 0.3f, 0.8f }; 
        public float[] KeyViewerColorBgPressed = new float[] { 0.5f, 0.2f, 0.8f, 1.0f }; 
        
        public float[] KeyViewerColorBorderNormal = new float[] { 0.6f, 0.3f, 0.9f, 0.8f }; 
        public float[] KeyViewerColorBorderPressed = new float[] { 0.8f, 0.4f, 1.0f, 1.0f }; 
        
        public float[] KeyViewerColorTextNormal = new float[] { 0.8f, 0.8f, 0.8f, 1.0f }; 
        public float[] KeyViewerColorTextPressed = new float[] { 1.0f, 1.0f, 1.0f, 1.0f }; 

        public float[] KeyViewerColorKps = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
        public float[] KeyViewerColorTotal = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };

        public bool KeyViewerKeyTextOutlineEnabled = false;
        public float[] KeyViewerKeyTextOutlineColor = new float[] { 0f, 0f, 0f, 1f };
        public float KeyViewerKeyTextOutlineThickness = 1f;
        public bool KeyViewerCountTextOutlineEnabled = false;
        public float[] KeyViewerCountTextOutlineColor = new float[] { 0f, 0f, 0f, 1f };
        public float KeyViewerCountTextOutlineThickness = 1f;

        public bool KeyViewerKeyTextShadowEnabled = false;
        public float[] KeyViewerKeyTextShadowColor = new float[] { 0f, 0f, 0f, 0.7f };
        public float[] KeyViewerKeyTextShadowOffset = new float[] { 2f, 2f };
        public float KeyViewerKeyTextShadowSoftness = 0f;
        public bool KeyViewerCountTextShadowEnabled = false;
        public float[] KeyViewerCountTextShadowColor = new float[] { 0f, 0f, 0f, 0.7f };
        public float[] KeyViewerCountTextShadowOffset = new float[] { 2f, 2f };
        public float KeyViewerCountTextShadowSoftness = 0f;

        // KeyRain Settings
        public bool EnableKeyRain = false;
        public float KeyRainSpeed = 800.0f;
        public float KeyRainMaxHeight = 400.0f;
        public int KeyRainFadeMode = 1; // 0=Clip, 1=Fade
        public float KeyRainFadeHeight = 1.0f;
        public float KeyRainFadePower = 1.0f;
        public float KeyRainWidthRatio1 = 0.8f;
        public float KeyRainWidthRatio2 = 0.4f;
        public float KeyRainYOffsetRow1 = 0.0f;
        public float KeyRainYOffsetRow2 = 0.0f;
        public float KeyRainCornerRadius = 0f;
        public float[] KeyRainColorRow1 = new float[] { 0.8f, 0.5f, 1.0f, 0.8f };
        public float[] KeyRainColorRow2 = new float[] { 0.5f, 0.8f, 1.0f, 0.8f };
        public float KeyRainGradientHeight = 1.0f;
        public float KeyRainGradientPower = 1.0f;

        private static readonly string[] DefaultKeyBindings = new string[16] {
            "Tab", "Alpha1", "Alpha2", "E", "P", "Equals", "Backspace", "Backslash",
            "UpArrow", "LeftShift", "C", "Space", "Comma", "Period", "Return", "H"
        };

        

        public int TotalHits = 0;

        public string KeyViewerFontPath = "";
        
        public float GlobalTextOffsetX = 0f;
        public float GlobalTextOffsetY = 0f;
        public float GlobalCountOffsetX = 0f;
        public float GlobalCountOffsetY = 0f;
        
        public float KeyViewerDefaultWidth = 50f;
        public float KeyViewerDefaultHeight = 50f;
        
        public System.Collections.Generic.List<KVConfiguration> KeyViewerConfigurations = new System.Collections.Generic.List<KVConfiguration>();

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            try
            {
                string filepath = System.IO.Path.Combine(modEntry.Path, "Settings.xml");
                if (System.IO.File.Exists(filepath))
                {
                    System.IO.File.Delete(filepath);
                }
                Save(this, modEntry);
            }
            catch (Exception e)
            {
                Main.Logger.Log("Failed to save settings: " + e.Message);
            }
        }

        public void InitNulls()
        {
            if (ToolsLimitedKeys == null)
                ToolsLimitedKeys = new System.Collections.Generic.List<KeyCode>();
            if (ToolsLimitedKeys.Count > 30)
                ToolsLimitedKeys.RemoveRange(30, ToolsLimitedKeys.Count - 30);
            if (ToolsAntiBounceIntervalMs <= 0f || float.IsNaN(ToolsAntiBounceIntervalMs) || float.IsInfinity(ToolsAntiBounceIntervalMs))
                ToolsAntiBounceIntervalMs = 50f;
            ToolsAntiBounceIntervalMs = Math.Max(1f, Math.Min(500f, ToolsAntiBounceIntervalMs));
            Language = LocalizationManager.NormalizeLanguage(Language);
            if (ImGuiPanelScale <= 0f || float.IsNaN(ImGuiPanelScale) || float.IsInfinity(ImGuiPanelScale))
                ImGuiPanelScale = 1.0f;
            ImGuiPanelScale = Math.Max(0.6f, Math.Min(2.0f, ImGuiPanelScale));
            if (OverlayUpdateRate <= 0f || float.IsNaN(OverlayUpdateRate) || float.IsInfinity(OverlayUpdateRate))
                OverlayUpdateRate = 240.0f;
            OverlayUpdateRate = Math.Max(30.0f, Math.Min(360.0f, OverlayUpdateRate));
            if (ImageRenderScale <= 0f || float.IsNaN(ImageRenderScale) || float.IsInfinity(ImageRenderScale))
                ImageRenderScale = 1.0f;
            ImageRenderScale = Math.Max(0.25f, Math.Min(2.0f, ImageRenderScale));
            EnsureGameUIElementSettings();
            
            if (RedPlanetColor == null || RedPlanetColor.Length != 4) RedPlanetColor = new float[] { 1f, 0f, 0f, 1f };
            if (RedRingColor == null || RedRingColor.Length != 4) RedRingColor = new float[] { 1f, 0f, 0f, 0.4f };
            if (RedTailColor == null || RedTailColor.Length != 4) RedTailColor = new float[] { 1f, 0f, 0f, 1f };
            if (BluePlanetColor == null || BluePlanetColor.Length != 4) BluePlanetColor = new float[] { 0f, 0f, 1f, 1f };
            if (BlueRingColor == null || BlueRingColor.Length != 4) BlueRingColor = new float[] { 0f, 0f, 1f, 0.4f };
            if (BlueTailColor == null || BlueTailColor.Length != 4) BlueTailColor = new float[] { 0f, 0f, 1f, 1f };
            if (GreenPlanetColor == null || GreenPlanetColor.Length != 4) GreenPlanetColor = new float[] { 0f, 1f, 0f, 1f };
            if (GreenRingColor == null || GreenRingColor.Length != 4) GreenRingColor = new float[] { 0f, 1f, 0f, 0.4f };
            if (GreenTailColor == null || GreenTailColor.Length != 4) GreenTailColor = new float[] { 0f, 1f, 0f, 1f };
            if (KeyViewerColorBgNormal == null || KeyViewerColorBgNormal.Length != 4) KeyViewerColorBgNormal = new float[] { 0.0f, 0.0f, 0.0f, 0.6f };
            if (KeyViewerColorBgPressed == null || KeyViewerColorBgPressed.Length != 4) KeyViewerColorBgPressed = new float[] { 0.2f, 0.6f, 1.0f, 0.8f };
            if (KeyViewerColorBorderNormal == null || KeyViewerColorBorderNormal.Length != 4) KeyViewerColorBorderNormal = new float[] { 0.5f, 0.5f, 0.5f, 0.5f };
            if (KeyViewerColorBorderPressed == null || KeyViewerColorBorderPressed.Length != 4) KeyViewerColorBorderPressed = new float[] { 0.8f, 0.9f, 1.0f, 0.8f };
            if (KeyViewerColorTextNormal == null || KeyViewerColorTextNormal.Length != 4) KeyViewerColorTextNormal = new float[] { 1.0f, 1.0f, 1.0f, 0.8f };
            if (KeyViewerKeyTextOutlineColor == null || KeyViewerKeyTextOutlineColor.Length != 4) KeyViewerKeyTextOutlineColor = new float[] { 0f, 0f, 0f, 1f };
            if (KeyViewerCountTextOutlineColor == null || KeyViewerCountTextOutlineColor.Length != 4) KeyViewerCountTextOutlineColor = new float[] { 0f, 0f, 0f, 1f };
            if (KeyViewerKeyTextOutlineThickness < 0f) KeyViewerKeyTextOutlineThickness = 1f;
            if (KeyViewerCountTextOutlineThickness < 0f) KeyViewerCountTextOutlineThickness = 1f;
            if (KeyViewerKeyTextShadowColor == null || KeyViewerKeyTextShadowColor.Length != 4) KeyViewerKeyTextShadowColor = new float[] { 0f, 0f, 0f, 0.7f };
            if (KeyViewerKeyTextShadowOffset == null || KeyViewerKeyTextShadowOffset.Length != 2) KeyViewerKeyTextShadowOffset = new float[] { 2f, 2f };
            if (float.IsNaN(KeyViewerKeyTextShadowSoftness) || float.IsInfinity(KeyViewerKeyTextShadowSoftness) || KeyViewerKeyTextShadowSoftness < 0f) KeyViewerKeyTextShadowSoftness = 0f;
            if (KeyViewerCountTextShadowColor == null || KeyViewerCountTextShadowColor.Length != 4) KeyViewerCountTextShadowColor = new float[] { 0f, 0f, 0f, 0.7f };
            if (KeyViewerCountTextShadowOffset == null || KeyViewerCountTextShadowOffset.Length != 2) KeyViewerCountTextShadowOffset = new float[] { 2f, 2f };
            if (float.IsNaN(KeyViewerCountTextShadowSoftness) || float.IsInfinity(KeyViewerCountTextShadowSoftness) || KeyViewerCountTextShadowSoftness < 0f) KeyViewerCountTextShadowSoftness = 0f;
            
            if (OverlayerTexts != null)
            {
                foreach (var t in OverlayerTexts)
                {
                    if (t == null) continue;
                    t.Depth = RenderDepth.ClampDepth(t.Depth);
                    if (t.TextColor == null || t.TextColor.Length != 4) t.TextColor = new float[] { 1f, 1f, 1f, 1f };
                    if (t.ShadowColor == null || t.ShadowColor.Length != 4) t.ShadowColor = new float[] { 0f, 0f, 0f, 1f };
                    if (t.ShadowOffset == null || t.ShadowOffset.Length != 2) t.ShadowOffset = new float[] { 2f, 2f };
                    if (float.IsNaN(t.ShadowSoftness) || float.IsInfinity(t.ShadowSoftness) || t.ShadowSoftness < 0f) t.ShadowSoftness = 0f;
                    if (t.OutlineColor == null || t.OutlineColor.Length != 4) t.OutlineColor = new float[] { 0f, 0f, 0f, 1f };
                    if (t.OutlineThickness < 0f) t.OutlineThickness = 1f;
                    if (t.Animations != null)
                    {
                        foreach (var anim in t.Animations)
                        {
                            anim.ParseJson();
                        }
                    }
                }
            }
            if (OverlayerImages != null)
            {
                foreach (var img in OverlayerImages)
                {
                    if (img == null) continue;
                    img.Depth = RenderDepth.ClampDepth(img.Depth);
                    if (img.Animations != null)
                    {
                        foreach (var anim in img.Animations)
                        {
                            anim.ParseJson();
                        }
                    }
                }
            }
            if (OverlayerVideos == null)
                OverlayerVideos = new System.Collections.Generic.List<OverlayerVideo>();
            if (OverlayerVideos.Count > 2)
                OverlayerVideos.RemoveRange(2, OverlayerVideos.Count - 2);
            for (int i = 0; i < OverlayerVideos.Count; i++)
            {
                if (OverlayerVideos[i] == null)
                    OverlayerVideos[i] = new OverlayerVideo();
                EnsureOverlayerVideoDefaults(OverlayerVideos[i]);
            }
            if (OverlayerProgressBars == null)
                OverlayerProgressBars = new System.Collections.Generic.List<OverlayerProgressBar>();
            for (int i = 0; i < OverlayerProgressBars.Count; i++)
            {
                if (OverlayerProgressBars[i] == null)
                    OverlayerProgressBars[i] = new OverlayerProgressBar();
                EnsureOverlayerProgressBarDefaults(OverlayerProgressBars[i]);
            }
            if (KeyViewerColorTextPressed == null || KeyViewerColorTextPressed.Length != 4) KeyViewerColorTextPressed = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
            if (KeyRainColorRow1 == null || KeyRainColorRow1.Length != 4) KeyRainColorRow1 = new float[] { 0.8f, 0.5f, 1.0f, 0.8f };
            if (KeyRainColorRow2 == null || KeyRainColorRow2.Length != 4) KeyRainColorRow2 = new float[] { 0.5f, 0.8f, 1.0f, 0.8f };

            EnsureKeyViewerConfigurations();

            if (OverlayerTexts == null) OverlayerTexts = new System.Collections.Generic.List<OverlayerText>();
            if (OverlayerTexts.Count == 0)
            {
                OverlayerTexts.Add(new OverlayerText());
            }

            foreach (var txt in OverlayerTexts)
            {
                if (txt == null) continue;
                txt.Depth = RenderDepth.ClampDepth(txt.Depth);
                if (string.IsNullOrEmpty(txt.Name))
                    txt.Name = "新模块";
                if (txt.TextColor == null || txt.TextColor.Length != 4)
                    txt.TextColor = new float[] { 1f, 1f, 1f, 1f };
                if (txt.ShadowColor == null || txt.ShadowColor.Length != 4)
                    txt.ShadowColor = new float[] { 0f, 0f, 0f, 1f };
                if (txt.ShadowOffset == null || txt.ShadowOffset.Length != 2)
                    txt.ShadowOffset = new float[] { 2f, 2f };
                if (float.IsNaN(txt.ShadowSoftness) || float.IsInfinity(txt.ShadowSoftness) || txt.ShadowSoftness < 0f)
                    txt.ShadowSoftness = 0f;
                if (txt.OutlineColor == null || txt.OutlineColor.Length != 4)
                    txt.OutlineColor = new float[] { 0f, 0f, 0f, 1f };
                if (txt.OutlineThickness < 0f)
                    txt.OutlineThickness = 1f;
            }

            var allNodes = GetAllKeyViewerNodes();

            foreach (var node in allNodes)
            {
                if (node == null) continue;
                node.Depth = RenderDepth.ClampDepth(node.Depth);
                if (node.RainRow == 0)
                {
                    node.RainRow = (node.PositionY < 80f) ? 1 : 2;
                }
                if (node.RainColor == null || node.RainColor.Length != 4)
                {
                    node.RainColor = (node.RainRow == 1) ? new float[] { 0.8f, 0.5f, 1.0f, 0.8f } : new float[] { 0.5f, 0.8f, 1.0f, 0.8f };
                }
                if (node.RainGradientEndColor == null || node.RainGradientEndColor.Length != 4)
                {
                    node.RainGradientEndColor = (node.RainRow == 1) ? new float[] { 1.0f, 0.25f, 0.8f, 0.8f } : new float[] { 0.25f, 1.0f, 0.8f, 0.8f };
                }
                if (node.RainWidthRatio <= 0.01f)
                {
                    node.RainWidthRatio = 0.8f;
                }
                if (node.KeyTextOutlineColor == null || node.KeyTextOutlineColor.Length != 4)
                {
                    node.KeyTextOutlineColor = new float[] { 0f, 0f, 0f, 1f };
                }
                if (node.CountTextOutlineColor == null || node.CountTextOutlineColor.Length != 4)
                {
                    node.CountTextOutlineColor = new float[] { 0f, 0f, 0f, 1f };
                }
                if (node.KeyTextOutlineThickness < 0f)
                {
                    node.KeyTextOutlineThickness = 1f;
                }
                if (node.CountTextOutlineThickness < 0f)
                {
                    node.CountTextOutlineThickness = 1f;
                }
                if (node.KeyTextShadowColor == null || node.KeyTextShadowColor.Length != 4)
                {
                    node.KeyTextShadowColor = new float[] { 0f, 0f, 0f, 0.7f };
                }
                if (node.KeyTextShadowOffset == null || node.KeyTextShadowOffset.Length != 2)
                {
                    node.KeyTextShadowOffset = new float[] { 2f, 2f };
                }
                if (float.IsNaN(node.KeyTextShadowSoftness) || float.IsInfinity(node.KeyTextShadowSoftness) || node.KeyTextShadowSoftness < 0f)
                {
                    node.KeyTextShadowSoftness = 0f;
                }
                if (node.CountTextShadowColor == null || node.CountTextShadowColor.Length != 4)
                {
                    node.CountTextShadowColor = new float[] { 0f, 0f, 0f, 0.7f };
                }
                if (node.CountTextShadowOffset == null || node.CountTextShadowOffset.Length != 2)
                {
                    node.CountTextShadowOffset = new float[] { 2f, 2f };
                }
                if (float.IsNaN(node.CountTextShadowSoftness) || float.IsInfinity(node.CountTextShadowSoftness) || node.CountTextShadowSoftness < 0f)
                {
                    node.CountTextShadowSoftness = 0f;
                }
            }
        }

        private static float[] CloneColor(float[] source, float[] fallback)
        {
            float[] result = new float[4];
            float[] src = source != null && source.Length == 4 ? source : fallback;
            if (src == null || src.Length != 4)
            {
                src = new float[] { 1f, 1f, 1f, 1f };
            }
            Array.Copy(src, result, 4);
            return result;
        }

        private static float[] ClonePair(float[] source, float fallbackX, float fallbackY)
        {
            return new float[]
            {
                source != null && source.Length > 0 ? source[0] : fallbackX,
                source != null && source.Length > 1 ? source[1] : fallbackY
            };
        }

        private static KVAxisGradient CloneAxisGradient(KVAxisGradient source, float[] fallbackVertical, float[] fallbackHorizontal)
        {
            if (source == null)
            {
                return new KVAxisGradient
                {
                    VerticalEndColor = CloneColor(fallbackVertical, new float[] { 1f, 1f, 1f, 1f }),
                    HorizontalEndColor = CloneColor(fallbackHorizontal, new float[] { 1f, 1f, 1f, 1f })
                };
            }

            return new KVAxisGradient
            {
                VerticalEnabled = source.VerticalEnabled,
                HorizontalEnabled = source.HorizontalEnabled,
                VerticalEndColor = CloneColor(source.VerticalEndColor, fallbackVertical),
                HorizontalEndColor = CloneColor(source.HorizontalEndColor, fallbackHorizontal)
            };
        }

        private static KVNode CloneKeyViewerNode(KVNode source)
        {
            if (source == null) return new KVNode();

            return new KVNode
            {
                NodeType = source.NodeType,
                KeyBind = source.KeyBind,
                CustomText = source.CustomText,
                ImagePath = source.ImagePath,
                VideoPath = source.VideoPath,
                VideoLoop = source.VideoLoop,
                VideoContentScale = source.VideoContentScale,
                VideoContentOffsetX = source.VideoContentOffsetX,
                VideoContentOffsetY = source.VideoContentOffsetY,
                IsUnselectable = source.IsUnselectable,
                Opacity = source.Opacity,
                Depth = source.Depth,
                PositionX = source.PositionX,
                PositionY = source.PositionY,
                Width = source.Width,
                Height = source.Height,
                BorderThickness = source.BorderThickness,
                CornerRadius = source.CornerRadius,
                Scale = source.Scale,
                TextOffsetY = source.TextOffsetY,
                TextOffsetX = source.TextOffsetX,
                TextScale = source.TextScale,
                CountOffsetY = source.CountOffsetY,
                CountOffsetX = source.CountOffsetX,
                CountScale = source.CountScale,
                KeyFontPath = source.KeyFontPath,
                CountFontPath = source.CountFontPath,
                HideCountText = source.HideCountText,
                UseCustomOutline = source.UseCustomOutline,
                KeyTextOutlineEnabled = source.KeyTextOutlineEnabled,
                KeyTextOutlineColor = CloneColor(source.KeyTextOutlineColor, new float[] { 0f, 0f, 0f, 1f }),
                KeyTextOutlineThickness = source.KeyTextOutlineThickness,
                CountTextOutlineEnabled = source.CountTextOutlineEnabled,
                CountTextOutlineColor = CloneColor(source.CountTextOutlineColor, new float[] { 0f, 0f, 0f, 1f }),
                CountTextOutlineThickness = source.CountTextOutlineThickness,
                UseCustomShadow = source.UseCustomShadow,
                KeyTextShadowEnabled = source.KeyTextShadowEnabled,
                KeyTextShadowColor = CloneColor(source.KeyTextShadowColor, new float[] { 0f, 0f, 0f, 0.7f }),
                KeyTextShadowOffset = ClonePair(source.KeyTextShadowOffset, 2f, 2f),
                KeyTextShadowSoftness = source.KeyTextShadowSoftness,
                CountTextShadowEnabled = source.CountTextShadowEnabled,
                CountTextShadowColor = CloneColor(source.CountTextShadowColor, new float[] { 0f, 0f, 0f, 0.7f }),
                CountTextShadowOffset = ClonePair(source.CountTextShadowOffset, 2f, 2f),
                CountTextShadowSoftness = source.CountTextShadowSoftness,
                UseCustomColor = source.UseCustomColor,
                UseCustomColorGradient = source.UseCustomColorGradient,
                ColorBgNormal = CloneColor(source.ColorBgNormal, new float[] { 0.2f, 0.2f, 0.2f, 0.8f }),
                ColorBgPressed = CloneColor(source.ColorBgPressed, new float[] { 0.8f, 0.8f, 0.8f, 0.8f }),
                ColorBorderNormal = CloneColor(source.ColorBorderNormal, new float[] { 0.4f, 0.4f, 0.4f, 1.0f }),
                ColorBorderPressed = CloneColor(source.ColorBorderPressed, new float[] { 1.0f, 1.0f, 1.0f, 1.0f }),
                ColorTextNormal = CloneColor(source.ColorTextNormal, new float[] { 1.0f, 1.0f, 1.0f, 1.0f }),
                ColorTextPressed = CloneColor(source.ColorTextPressed, new float[] { 0.0f, 0.0f, 0.0f, 1.0f }),
                BackgroundGradientNormal = CloneAxisGradient(source.BackgroundGradientNormal, source.ColorBgNormal, source.ColorBgNormal),
                BackgroundGradientPressed = CloneAxisGradient(source.BackgroundGradientPressed, source.ColorBgPressed, source.ColorBgPressed),
                BorderGradientNormal = CloneAxisGradient(source.BorderGradientNormal, source.ColorBorderNormal, source.ColorBorderNormal),
                BorderGradientPressed = CloneAxisGradient(source.BorderGradientPressed, source.ColorBorderPressed, source.ColorBorderPressed),
                TextGradientNormal = CloneAxisGradient(source.TextGradientNormal, source.ColorTextNormal, source.ColorTextNormal),
                TextGradientPressed = CloneAxisGradient(source.TextGradientPressed, source.ColorTextPressed, source.ColorTextPressed),
                RainRow = source.RainRow,
                EnableKeyRain = source.EnableKeyRain,
                UseCustomRain = source.UseCustomRain,
                RainColor = CloneColor(source.RainColor, new float[] { 0.8f, 0.5f, 1.0f, 0.8f }),
                RainGradientEnabled = source.RainGradientEnabled,
                RainHorizontalGradientEnabled = source.RainHorizontalGradientEnabled,
                RainGradientEndColor = CloneColor(source.RainGradientEndColor, new float[] { 1f, 0.25f, 0.8f, 0.8f }),
                RainHorizontalGradientEndColor = CloneColor(source.RainHorizontalGradientEndColor, new float[] { 0.45f, 0.75f, 1f, 0.8f }),
                RainGradientMode = source.RainGradientMode,
                RainFadeHeight = source.RainFadeHeight,
                RainFadePower = source.RainFadePower,
                RainGradientHeight = source.RainGradientHeight,
                RainGradientPower = source.RainGradientPower,
                RainWidthRatio = source.RainWidthRatio,
                RainYOffset = source.RainYOffset,
                RainCornerRadius = source.RainCornerRadius,
                UseCustomRainShadow = source.UseCustomRainShadow,
                RainShadowEnabled = source.RainShadowEnabled,
                RainShadowColor = CloneColor(source.RainShadowColor, new float[] { 0f, 0f, 0f, 0.35f }),
                RainShadowOffset = ClonePair(source.RainShadowOffset, 0f, 0f),
                RainShadowSoftness = source.RainShadowSoftness,
                RainShadowStrength = source.RainShadowStrength,
                UseCustomKeyPressAnimation = source.UseCustomKeyPressAnimation,
                KeyPressAnimationEnabled = source.KeyPressAnimationEnabled,
                KeyPressAnimationDuration = source.KeyPressAnimationDuration,
                KeyPressAnimationEasing = source.KeyPressAnimationEasing,
                KeyPressAnimationAffectColors = source.KeyPressAnimationAffectColors,
                KeyPressAnimationScale = source.KeyPressAnimationScale,
                KeyPressAnimationOffsetX = source.KeyPressAnimationOffsetX,
                KeyPressAnimationOffsetY = source.KeyPressAnimationOffsetY,
                HitCount = source.HitCount
            };
        }

        private static System.Collections.Generic.List<KVNode> CloneKeyViewerNodes(System.Collections.Generic.List<KVNode> nodes)
        {
            var result = new System.Collections.Generic.List<KVNode>();
            if (nodes == null) return result;

            foreach (KVNode node in nodes)
            {
                result.Add(CloneKeyViewerNode(node));
            }
            return result;
        }

        private static OverlayerProgressValueSource CloneProgressValueSource(OverlayerProgressValueSource source)
        {
            if (source == null) return new OverlayerProgressValueSource();
            return new OverlayerProgressValueSource(source.Kind, source.Constant);
        }

        private static OverlayerAnimation CloneOverlayerAnimation(OverlayerAnimation source)
        {
            if (source == null) return new OverlayerAnimation();

            var clone = new OverlayerAnimation
            {
                IsEnabled = source.IsEnabled,
                Name = source.Name,
                Trigger = source.Trigger,
                JsonString = source.JsonString,
                UseGraphicalAnimation = source.UseGraphicalAnimation,
                StartScale = source.StartScale,
                StartRotation = source.StartRotation,
                StartX = source.StartX,
                StartY = source.StartY,
                StartOpacity = source.StartOpacity,
                EndScale = source.EndScale,
                EndRotation = source.EndRotation,
                EndX = source.EndX,
                EndY = source.EndY,
                EndOpacity = source.EndOpacity,
                Duration = source.Duration,
                EasingType = source.EasingType
            };
            clone.ParseJson();
            return clone;
        }

        private static System.Collections.Generic.List<OverlayerAnimation> CloneOverlayerAnimations(System.Collections.Generic.List<OverlayerAnimation> animations)
        {
            var result = new System.Collections.Generic.List<OverlayerAnimation>();
            if (animations == null) return result;

            foreach (OverlayerAnimation animation in animations)
            {
                result.Add(CloneOverlayerAnimation(animation));
            }
            return result;
        }

        public KVConfiguration CloneKeyViewerConfiguration(KVConfiguration source)
        {
            if (source == null) return new KVConfiguration();

            return new KVConfiguration
            {
                Name = string.IsNullOrEmpty(source.Name) ? "KV 配置 副本" : source.Name + " 副本",
                IsEnabled = source.IsEnabled,
                ShowInGame = source.ShowInGame,
                Nodes = CloneKeyViewerNodes(source.Nodes),
                AppearanceMigrated = source.AppearanceMigrated,
                FontPath = source.FontPath,
                Scale = source.Scale,
                BorderThickness = source.BorderThickness,
                HideCountText = source.HideCountText,
                GlobalTextOffsetX = source.GlobalTextOffsetX,
                GlobalTextOffsetY = source.GlobalTextOffsetY,
                GlobalCountOffsetX = source.GlobalCountOffsetX,
                GlobalCountOffsetY = source.GlobalCountOffsetY,
                DefaultWidth = source.DefaultWidth,
                DefaultHeight = source.DefaultHeight,
                ColorBgNormal = CloneColor(source.ColorBgNormal, new float[] { 0.2f, 0.1f, 0.3f, 0.8f }),
                ColorBgPressed = CloneColor(source.ColorBgPressed, new float[] { 0.5f, 0.2f, 0.8f, 1.0f }),
                ColorBorderNormal = CloneColor(source.ColorBorderNormal, new float[] { 0.6f, 0.3f, 0.9f, 0.8f }),
                ColorBorderPressed = CloneColor(source.ColorBorderPressed, new float[] { 0.8f, 0.4f, 1.0f, 1.0f }),
                ColorTextNormal = CloneColor(source.ColorTextNormal, new float[] { 0.8f, 0.8f, 0.8f, 1.0f }),
                ColorTextPressed = CloneColor(source.ColorTextPressed, new float[] { 1.0f, 1.0f, 1.0f, 1.0f }),
                BackgroundGradientNormal = CloneAxisGradient(source.BackgroundGradientNormal, source.ColorBgNormal, source.ColorBgNormal),
                BackgroundGradientPressed = CloneAxisGradient(source.BackgroundGradientPressed, source.ColorBgPressed, source.ColorBgPressed),
                BorderGradientNormal = CloneAxisGradient(source.BorderGradientNormal, source.ColorBorderNormal, source.ColorBorderNormal),
                BorderGradientPressed = CloneAxisGradient(source.BorderGradientPressed, source.ColorBorderPressed, source.ColorBorderPressed),
                TextGradientNormal = CloneAxisGradient(source.TextGradientNormal, source.ColorTextNormal, source.ColorTextNormal),
                TextGradientPressed = CloneAxisGradient(source.TextGradientPressed, source.ColorTextPressed, source.ColorTextPressed),
                ColorKps = CloneColor(source.ColorKps, new float[] { 1f, 1f, 1f, 1f }),
                ColorTotal = CloneColor(source.ColorTotal, new float[] { 1f, 1f, 1f, 1f }),
                KeyTextOutlineEnabled = source.KeyTextOutlineEnabled,
                KeyTextOutlineColor = CloneColor(source.KeyTextOutlineColor, new float[] { 0f, 0f, 0f, 1f }),
                KeyTextOutlineThickness = source.KeyTextOutlineThickness,
                CountTextOutlineEnabled = source.CountTextOutlineEnabled,
                CountTextOutlineColor = CloneColor(source.CountTextOutlineColor, new float[] { 0f, 0f, 0f, 1f }),
                CountTextOutlineThickness = source.CountTextOutlineThickness,
                KeyTextShadowEnabled = source.KeyTextShadowEnabled,
                KeyTextShadowColor = CloneColor(source.KeyTextShadowColor, new float[] { 0f, 0f, 0f, 0.7f }),
                KeyTextShadowOffset = ClonePair(source.KeyTextShadowOffset, 2f, 2f),
                KeyTextShadowSoftness = source.KeyTextShadowSoftness,
                CountTextShadowEnabled = source.CountTextShadowEnabled,
                CountTextShadowColor = CloneColor(source.CountTextShadowColor, new float[] { 0f, 0f, 0f, 0.7f }),
                CountTextShadowOffset = ClonePair(source.CountTextShadowOffset, 2f, 2f),
                CountTextShadowSoftness = source.CountTextShadowSoftness,
                EnableKeyRain = source.EnableKeyRain,
                KeyRainSpeed = source.KeyRainSpeed,
                KeyRainMaxHeight = source.KeyRainMaxHeight,
                KeyRainFadeMode = source.KeyRainFadeMode,
                KeyRainWidthRatio1 = source.KeyRainWidthRatio1,
                KeyRainWidthRatio2 = source.KeyRainWidthRatio2,
                KeyRainYOffsetRow1 = source.KeyRainYOffsetRow1,
                KeyRainYOffsetRow2 = source.KeyRainYOffsetRow2,
                KeyRainCornerRadius = source.KeyRainCornerRadius,
                KeyRainColorRow1 = CloneColor(source.KeyRainColorRow1, new float[] { 0.8f, 0.5f, 1f, 0.8f }),
                KeyRainColorRow2 = CloneColor(source.KeyRainColorRow2, new float[] { 0.5f, 0.8f, 1f, 0.8f }),
                KeyRainGradientEnabled = source.KeyRainGradientEnabled,
                KeyRainHorizontalGradientEnabled = source.KeyRainHorizontalGradientEnabled,
                KeyRainGradientEndColorRow1 = CloneColor(source.KeyRainGradientEndColorRow1, new float[] { 1f, 0.25f, 0.8f, 0.8f }),
                KeyRainGradientEndColorRow2 = CloneColor(source.KeyRainGradientEndColorRow2, new float[] { 0.25f, 1f, 0.8f, 0.8f }),
                KeyRainHorizontalGradientEndColorRow1 = CloneColor(source.KeyRainHorizontalGradientEndColorRow1, new float[] { 0.45f, 0.75f, 1f, 0.8f }),
                KeyRainHorizontalGradientEndColorRow2 = CloneColor(source.KeyRainHorizontalGradientEndColorRow2, new float[] { 1f, 0.65f, 0.35f, 0.8f }),
                KeyRainGradientMode = source.KeyRainGradientMode,
                KeyRainFadeHeight = source.KeyRainFadeHeight,
                KeyRainFadePower = source.KeyRainFadePower,
                KeyRainGradientHeight = source.KeyRainGradientHeight,
                KeyRainGradientPower = source.KeyRainGradientPower,
                KeyRainShadowEnabled = source.KeyRainShadowEnabled,
                KeyRainShadowColor = CloneColor(source.KeyRainShadowColor, new float[] { 0f, 0f, 0f, 0.35f }),
                KeyRainShadowOffset = ClonePair(source.KeyRainShadowOffset, 0f, 0f),
                KeyRainShadowSoftness = source.KeyRainShadowSoftness,
                KeyRainShadowStrength = source.KeyRainShadowStrength,
                KeyPressAnimationEnabled = source.KeyPressAnimationEnabled,
                KeyPressAnimationDuration = source.KeyPressAnimationDuration,
                KeyPressAnimationEasing = source.KeyPressAnimationEasing,
                KeyPressAnimationAffectColors = source.KeyPressAnimationAffectColors,
                KeyPressAnimationScale = source.KeyPressAnimationScale,
                KeyPressAnimationOffsetX = source.KeyPressAnimationOffsetX,
                KeyPressAnimationOffsetY = source.KeyPressAnimationOffsetY
            };
        }

        public OverlayerText CloneOverlayerText(OverlayerText source)
        {
            if (source == null) return new OverlayerText();

            return new OverlayerText
            {
                Name = string.IsNullOrEmpty(source.Name) ? "文本 副本" : source.Name + " 副本",
                IsEnabled = source.IsEnabled,
                ShowInGame = source.ShowInGame,
                TextFormat = source.TextFormat,
                PositionX = source.PositionX + 16f,
                PositionY = source.PositionY + 16f,
                FontSize = source.FontSize,
                TextColor = CloneColor(source.TextColor, new float[] { 1f, 1f, 1f, 1f }),
                Alignment = source.Alignment,
                FontPath = source.FontPath,
                Depth = source.Depth,
                EnableShadow = source.EnableShadow,
                ShadowColor = CloneColor(source.ShadowColor, new float[] { 0f, 0f, 0f, 1f }),
                ShadowOffset = new float[] {
                    source.ShadowOffset != null && source.ShadowOffset.Length > 0 ? source.ShadowOffset[0] : 2f,
                    source.ShadowOffset != null && source.ShadowOffset.Length > 1 ? source.ShadowOffset[1] : 2f
                },
                ShadowSoftness = source.ShadowSoftness,
                EnableOutline = source.EnableOutline,
                OutlineColor = CloneColor(source.OutlineColor, new float[] { 0f, 0f, 0f, 1f }),
                OutlineThickness = source.OutlineThickness,
                LineHeightOffset = source.LineHeightOffset,
                LetterSpacing = source.LetterSpacing,
                Animations = CloneOverlayerAnimations(source.Animations),
                PivotX = source.PivotX,
                PivotY = source.PivotY,
                LastWidth = source.LastWidth,
                LastHeight = source.LastHeight
            };
        }

        public OverlayerImage CloneOverlayerImage(OverlayerImage source)
        {
            if (source == null) return new OverlayerImage();

            return new OverlayerImage
            {
                IsEnabled = source.IsEnabled,
                ShowInGame = source.ShowInGame,
                ImagePath = source.ImagePath,
                PositionX = source.PositionX + 16f,
                PositionY = source.PositionY + 16f,
                Scale = source.Scale,
                Rotation = source.Rotation,
                Opacity = source.Opacity,
                Depth = source.Depth,
                PivotX = source.PivotX,
                PivotY = source.PivotY,
                Animations = CloneOverlayerAnimations(source.Animations),
                LastWidth = source.LastWidth,
                LastHeight = source.LastHeight
            };
        }

        public OverlayerVideo CloneOverlayerVideo(OverlayerVideo source)
        {
            if (source == null) return new OverlayerVideo();

            return new OverlayerVideo
            {
                Name = string.IsNullOrEmpty(source.Name) ? "视频 副本" : source.Name + " 副本",
                IsEnabled = source.IsEnabled,
                ShowInGame = source.ShowInGame,
                VideoPath = source.VideoPath,
                Loop = source.Loop,
                ContentScale = source.ContentScale,
                ContentOffsetX = source.ContentOffsetX,
                ContentOffsetY = source.ContentOffsetY,
                PositionX = source.PositionX + 16f,
                PositionY = source.PositionY + 16f,
                Width = source.Width,
                Height = source.Height,
                Rotation = source.Rotation,
                Opacity = source.Opacity,
                Depth = source.Depth,
                PivotX = source.PivotX,
                PivotY = source.PivotY,
                LastWidth = source.LastWidth,
                LastHeight = source.LastHeight
            };
        }

        public OverlayerProgressBar CloneOverlayerProgressBar(OverlayerProgressBar source)
        {
            if (source == null) return new OverlayerProgressBar();

            return new OverlayerProgressBar
            {
                Name = string.IsNullOrEmpty(source.Name) ? "进度条 副本" : source.Name + " 副本",
                IsEnabled = source.IsEnabled,
                ShowInGame = source.ShowInGame,
                ValueSource = CloneProgressValueSource(source.ValueSource),
                MinSource = CloneProgressValueSource(source.MinSource),
                MaxSource = CloneProgressValueSource(source.MaxSource),
                PositionX = source.PositionX + 16f,
                PositionY = source.PositionY + 16f,
                Width = source.Width,
                Height = source.Height,
                Opacity = source.Opacity,
                Depth = source.Depth,
                PivotX = source.PivotX,
                PivotY = source.PivotY,
                FillDirection = source.FillDirection,
                Reverse = source.Reverse,
                ClampValue = source.ClampValue,
                BackgroundColor = CloneColor(source.BackgroundColor, new float[] { 0f, 0f, 0f, 0.45f }),
                FillColor = CloneColor(source.FillColor, new float[] { 0.2f, 0.75f, 1f, 0.95f }),
                EnableFillGradient = source.EnableFillGradient,
                FillGradientStartColor = CloneColor(source.FillGradientStartColor, new float[] { 1f, 0.25f, 0.25f, 0.95f }),
                FillGradientEndColor = CloneColor(source.FillGradientEndColor, new float[] { 0.25f, 1f, 0.35f, 0.95f }),
                BorderColor = CloneColor(source.BorderColor, new float[] { 1f, 1f, 1f, 0.8f }),
                BorderThickness = source.BorderThickness,
                CornerRadius = source.CornerRadius,
                EnableShadow = source.EnableShadow,
                ShadowColor = CloneColor(source.ShadowColor, new float[] { 0f, 0f, 0f, 0.45f }),
                ShadowOffset = new float[] {
                    source.ShadowOffset != null && source.ShadowOffset.Length > 0 ? source.ShadowOffset[0] : 2f,
                    source.ShadowOffset != null && source.ShadowOffset.Length > 1 ? source.ShadowOffset[1] : 2f
                },
                ShadowSoftness = source.ShadowSoftness,
                LastWidth = source.LastWidth,
                LastHeight = source.LastHeight
            };
        }

        private void CopyGlobalKeyViewerAppearanceTo(KVConfiguration config)
        {
            if (config == null) return;

            config.FontPath = KeyViewerFontPath ?? "";
            config.Scale = KeyViewerScale;
            config.BorderThickness = KeyViewerBorderThickness;
            config.HideCountText = KeyViewerHideCountText;
            config.GlobalTextOffsetX = GlobalTextOffsetX;
            config.GlobalTextOffsetY = GlobalTextOffsetY;
            config.GlobalCountOffsetX = GlobalCountOffsetX;
            config.GlobalCountOffsetY = GlobalCountOffsetY;
            config.DefaultWidth = KeyViewerDefaultWidth;
            config.DefaultHeight = KeyViewerDefaultHeight;
            config.ColorBgNormal = CloneColor(KeyViewerColorBgNormal, new float[] { 0.2f, 0.1f, 0.3f, 0.8f });
            config.ColorBgPressed = CloneColor(KeyViewerColorBgPressed, new float[] { 0.5f, 0.2f, 0.8f, 1.0f });
            config.ColorBorderNormal = CloneColor(KeyViewerColorBorderNormal, new float[] { 0.6f, 0.3f, 0.9f, 0.8f });
            config.ColorBorderPressed = CloneColor(KeyViewerColorBorderPressed, new float[] { 0.8f, 0.4f, 1.0f, 1.0f });
            config.ColorTextNormal = CloneColor(KeyViewerColorTextNormal, new float[] { 0.8f, 0.8f, 0.8f, 1.0f });
            config.ColorTextPressed = CloneColor(KeyViewerColorTextPressed, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            config.BackgroundGradientNormal = CloneAxisGradient(null, config.ColorBgNormal, config.ColorBgNormal);
            config.BackgroundGradientPressed = CloneAxisGradient(null, config.ColorBgPressed, config.ColorBgPressed);
            config.BorderGradientNormal = CloneAxisGradient(null, config.ColorBorderNormal, config.ColorBorderNormal);
            config.BorderGradientPressed = CloneAxisGradient(null, config.ColorBorderPressed, config.ColorBorderPressed);
            config.TextGradientNormal = CloneAxisGradient(null, config.ColorTextNormal, config.ColorTextNormal);
            config.TextGradientPressed = CloneAxisGradient(null, config.ColorTextPressed, config.ColorTextPressed);
            config.ColorKps = CloneColor(KeyViewerColorKps, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            config.ColorTotal = CloneColor(KeyViewerColorTotal, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            config.KeyTextOutlineEnabled = KeyViewerKeyTextOutlineEnabled;
            config.KeyTextOutlineColor = CloneColor(KeyViewerKeyTextOutlineColor, new float[] { 0f, 0f, 0f, 1f });
            config.KeyTextOutlineThickness = KeyViewerKeyTextOutlineThickness;
            config.CountTextOutlineEnabled = KeyViewerCountTextOutlineEnabled;
            config.CountTextOutlineColor = CloneColor(KeyViewerCountTextOutlineColor, new float[] { 0f, 0f, 0f, 1f });
            config.CountTextOutlineThickness = KeyViewerCountTextOutlineThickness;
            config.KeyTextShadowEnabled = KeyViewerKeyTextShadowEnabled;
            config.KeyTextShadowColor = CloneColor(KeyViewerKeyTextShadowColor, new float[] { 0f, 0f, 0f, 0.7f });
            config.KeyTextShadowOffset = ClonePair(KeyViewerKeyTextShadowOffset, 2f, 2f);
            config.KeyTextShadowSoftness = KeyViewerKeyTextShadowSoftness;
            config.CountTextShadowEnabled = KeyViewerCountTextShadowEnabled;
            config.CountTextShadowColor = CloneColor(KeyViewerCountTextShadowColor, new float[] { 0f, 0f, 0f, 0.7f });
            config.CountTextShadowOffset = ClonePair(KeyViewerCountTextShadowOffset, 2f, 2f);
            config.CountTextShadowSoftness = KeyViewerCountTextShadowSoftness;
            config.EnableKeyRain = EnableKeyRain;
            config.KeyRainSpeed = KeyRainSpeed;
            config.KeyRainMaxHeight = KeyRainMaxHeight;
            config.KeyRainFadeMode = KeyRainFadeMode;
            config.KeyRainFadeHeight = KeyRainFadeHeight;
            config.KeyRainFadePower = KeyRainFadePower;
            config.KeyRainWidthRatio1 = KeyRainWidthRatio1;
            config.KeyRainWidthRatio2 = KeyRainWidthRatio2;
            config.KeyRainYOffsetRow1 = KeyRainYOffsetRow1;
            config.KeyRainYOffsetRow2 = KeyRainYOffsetRow2;
            config.KeyRainCornerRadius = KeyRainCornerRadius;
            config.KeyRainColorRow1 = CloneColor(KeyRainColorRow1, new float[] { 0.8f, 0.5f, 1.0f, 0.8f });
            config.KeyRainColorRow2 = CloneColor(KeyRainColorRow2, new float[] { 0.5f, 0.8f, 1.0f, 0.8f });
            config.KeyRainGradientEnabled = false;
            config.KeyRainHorizontalGradientEnabled = false;
            config.KeyRainGradientEndColorRow1 = new float[] { 1.0f, 0.25f, 0.8f, 0.8f };
            config.KeyRainGradientEndColorRow2 = new float[] { 0.25f, 1.0f, 0.8f, 0.8f };
            config.KeyRainHorizontalGradientEndColorRow1 = CloneColor(config.KeyRainColorRow1, new float[] { 0.8f, 0.5f, 1.0f, 0.8f });
            config.KeyRainHorizontalGradientEndColorRow2 = CloneColor(config.KeyRainColorRow2, new float[] { 0.5f, 0.8f, 1.0f, 0.8f });
            config.KeyRainGradientHeight = KeyRainGradientHeight;
            config.KeyRainGradientPower = KeyRainGradientPower;
            config.KeyPressAnimationEnabled = false;
            config.KeyPressAnimationDuration = 0.12f;
            config.KeyPressAnimationEasing = "ease-out-quad";
            config.KeyPressAnimationAffectColors = true;
            config.KeyPressAnimationScale = 1.0f;
            config.KeyPressAnimationOffsetX = 0f;
            config.KeyPressAnimationOffsetY = 0f;
            config.AppearanceMigrated = true;
        }

        private void EnsureKeyViewerConfigurationAppearance(KVConfiguration config)
        {
            if (config == null) return;

            if (!config.AppearanceMigrated)
            {
                CopyGlobalKeyViewerAppearanceTo(config);
            }

            if (config.FontPath == null) config.FontPath = "";
            if (float.IsNaN(config.Scale) || float.IsInfinity(config.Scale) || config.Scale <= 0f)
                config.Scale = 1.0f;
            config.Scale = Math.Max(0.5f, Math.Min(3f, config.Scale));
            if (float.IsNaN(config.BorderThickness) || float.IsInfinity(config.BorderThickness) || config.BorderThickness < 0f)
                config.BorderThickness = 2.0f;
            config.BorderThickness = Math.Max(0f, Math.Min(10f, config.BorderThickness));
            if (float.IsNaN(config.DefaultWidth) || float.IsInfinity(config.DefaultWidth) || config.DefaultWidth <= 0f)
                config.DefaultWidth = 50f;
            if (float.IsNaN(config.DefaultHeight) || float.IsInfinity(config.DefaultHeight) || config.DefaultHeight <= 0f)
                config.DefaultHeight = 50f;
            config.DefaultWidth = Math.Max(10f, Math.Min(500f, config.DefaultWidth));
            config.DefaultHeight = Math.Max(10f, Math.Min(500f, config.DefaultHeight));

            config.ColorBgNormal = CloneColor(config.ColorBgNormal, new float[] { 0.2f, 0.1f, 0.3f, 0.8f });
            config.ColorBgPressed = CloneColor(config.ColorBgPressed, new float[] { 0.5f, 0.2f, 0.8f, 1.0f });
            config.ColorBorderNormal = CloneColor(config.ColorBorderNormal, new float[] { 0.6f, 0.3f, 0.9f, 0.8f });
            config.ColorBorderPressed = CloneColor(config.ColorBorderPressed, new float[] { 0.8f, 0.4f, 1.0f, 1.0f });
            config.ColorTextNormal = CloneColor(config.ColorTextNormal, new float[] { 0.8f, 0.8f, 0.8f, 1.0f });
            config.ColorTextPressed = CloneColor(config.ColorTextPressed, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            config.BackgroundGradientNormal = CloneAxisGradient(config.BackgroundGradientNormal, config.ColorBgNormal, config.ColorBgNormal);
            config.BackgroundGradientPressed = CloneAxisGradient(config.BackgroundGradientPressed, config.ColorBgPressed, config.ColorBgPressed);
            config.BorderGradientNormal = CloneAxisGradient(config.BorderGradientNormal, config.ColorBorderNormal, config.ColorBorderNormal);
            config.BorderGradientPressed = CloneAxisGradient(config.BorderGradientPressed, config.ColorBorderPressed, config.ColorBorderPressed);
            config.TextGradientNormal = CloneAxisGradient(config.TextGradientNormal, config.ColorTextNormal, config.ColorTextNormal);
            config.TextGradientPressed = CloneAxisGradient(config.TextGradientPressed, config.ColorTextPressed, config.ColorTextPressed);
            config.ColorKps = CloneColor(config.ColorKps, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            config.ColorTotal = CloneColor(config.ColorTotal, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            config.KeyTextOutlineColor = CloneColor(config.KeyTextOutlineColor, new float[] { 0f, 0f, 0f, 1f });
            config.CountTextOutlineColor = CloneColor(config.CountTextOutlineColor, new float[] { 0f, 0f, 0f, 1f });
            if (config.KeyTextOutlineThickness < 0f) config.KeyTextOutlineThickness = 1f;
            if (config.CountTextOutlineThickness < 0f) config.CountTextOutlineThickness = 1f;
            config.KeyTextShadowColor = CloneColor(config.KeyTextShadowColor, new float[] { 0f, 0f, 0f, 0.7f });
            config.KeyTextShadowOffset = ClonePair(config.KeyTextShadowOffset, 2f, 2f);
            if (float.IsNaN(config.KeyTextShadowSoftness) || float.IsInfinity(config.KeyTextShadowSoftness) || config.KeyTextShadowSoftness < 0f) config.KeyTextShadowSoftness = 0f;
            config.CountTextShadowColor = CloneColor(config.CountTextShadowColor, new float[] { 0f, 0f, 0f, 0.7f });
            config.CountTextShadowOffset = ClonePair(config.CountTextShadowOffset, 2f, 2f);
            if (float.IsNaN(config.CountTextShadowSoftness) || float.IsInfinity(config.CountTextShadowSoftness) || config.CountTextShadowSoftness < 0f) config.CountTextShadowSoftness = 0f;

            if (float.IsNaN(config.KeyRainSpeed) || float.IsInfinity(config.KeyRainSpeed) || config.KeyRainSpeed <= 0f)
                config.KeyRainSpeed = 800.0f;
            if (float.IsNaN(config.KeyRainMaxHeight) || float.IsInfinity(config.KeyRainMaxHeight) || config.KeyRainMaxHeight <= 0f)
                config.KeyRainMaxHeight = 400.0f;
            config.KeyRainSpeed = Math.Max(100f, Math.Min(2000f, config.KeyRainSpeed));
            config.KeyRainMaxHeight = Math.Max(100f, Math.Min(1500f, config.KeyRainMaxHeight));
            config.KeyRainFadeMode = Math.Max(0, Math.Min(1, config.KeyRainFadeMode));
            config.KeyRainFadeHeight = NormalizeRainCurveHeight(config.KeyRainFadeHeight);
            config.KeyRainFadePower = NormalizeRainCurvePower(config.KeyRainFadePower);
            config.KeyRainGradientHeight = NormalizeRainCurveHeight(config.KeyRainGradientHeight);
            config.KeyRainGradientPower = NormalizeRainCurvePower(config.KeyRainGradientPower);
            config.KeyRainWidthRatio1 = Math.Max(0.05f, Math.Min(2.0f, config.KeyRainWidthRatio1));
            config.KeyRainWidthRatio2 = Math.Max(0.05f, Math.Min(2.0f, config.KeyRainWidthRatio2));
            if (float.IsNaN(config.KeyRainCornerRadius) || float.IsInfinity(config.KeyRainCornerRadius) || config.KeyRainCornerRadius < 0f)
                config.KeyRainCornerRadius = 0f;
            config.KeyRainCornerRadius = Math.Max(0f, Math.Min(256f, config.KeyRainCornerRadius));
            config.KeyRainColorRow1 = CloneColor(config.KeyRainColorRow1, new float[] { 0.8f, 0.5f, 1.0f, 0.8f });
            config.KeyRainColorRow2 = CloneColor(config.KeyRainColorRow2, new float[] { 0.5f, 0.8f, 1.0f, 0.8f });
            config.KeyRainGradientEndColorRow1 = CloneColor(config.KeyRainGradientEndColorRow1, new float[] { 1f, 0.25f, 0.8f, 0.8f });
            config.KeyRainGradientEndColorRow2 = CloneColor(config.KeyRainGradientEndColorRow2, new float[] { 0.25f, 1f, 0.8f, 0.8f });
            config.KeyRainHorizontalGradientEndColorRow1 = CloneColor(config.KeyRainHorizontalGradientEndColorRow1, config.KeyRainColorRow1);
            config.KeyRainHorizontalGradientEndColorRow2 = CloneColor(config.KeyRainHorizontalGradientEndColorRow2, config.KeyRainColorRow2);
            config.KeyRainGradientMode = Math.Max(0, Math.Min(1, config.KeyRainGradientMode));
            config.KeyRainShadowColor = CloneColor(config.KeyRainShadowColor, new float[] { 0f, 0f, 0f, 0.35f });
            config.KeyRainShadowOffset = ClonePair(config.KeyRainShadowOffset, 0f, 0f);
            NormalizeLegacyKeyRainShadowOffset(config.KeyRainShadowOffset);
            if (float.IsNaN(config.KeyRainShadowSoftness) || float.IsInfinity(config.KeyRainShadowSoftness) || config.KeyRainShadowSoftness < 0f)
                config.KeyRainShadowSoftness = 12f;
            if (float.IsNaN(config.KeyRainShadowStrength) || float.IsInfinity(config.KeyRainShadowStrength))
                config.KeyRainShadowStrength = 1f;
            config.KeyRainShadowSoftness = Math.Max(0f, Math.Min(64f, config.KeyRainShadowSoftness));
            config.KeyRainShadowStrength = Math.Max(0f, Math.Min(1f, config.KeyRainShadowStrength));
            if (float.IsNaN(config.KeyPressAnimationDuration) || float.IsInfinity(config.KeyPressAnimationDuration) || config.KeyPressAnimationDuration <= 0f)
                config.KeyPressAnimationDuration = 0.12f;
            config.KeyPressAnimationDuration = Math.Max(0.01f, Math.Min(2.0f, config.KeyPressAnimationDuration));
            if (string.IsNullOrEmpty(config.KeyPressAnimationEasing))
                config.KeyPressAnimationEasing = "ease-out-quad";
            if (float.IsNaN(config.KeyPressAnimationScale) || float.IsInfinity(config.KeyPressAnimationScale))
                config.KeyPressAnimationScale = 1.0f;
            config.KeyPressAnimationScale = Math.Max(0.2f, Math.Min(3.0f, config.KeyPressAnimationScale));
            if (float.IsNaN(config.KeyPressAnimationOffsetX) || float.IsInfinity(config.KeyPressAnimationOffsetX))
                config.KeyPressAnimationOffsetX = 0f;
            if (float.IsNaN(config.KeyPressAnimationOffsetY) || float.IsInfinity(config.KeyPressAnimationOffsetY))
                config.KeyPressAnimationOffsetY = 0f;
            config.KeyPressAnimationOffsetX = Math.Max(-200f, Math.Min(200f, config.KeyPressAnimationOffsetX));
            config.KeyPressAnimationOffsetY = Math.Max(-200f, Math.Min(200f, config.KeyPressAnimationOffsetY));
        }

        private static void EnsureKeyViewerNodeDefaults(KVNode node)
        {
            if (node == null) return;
            if (node.NodeType < 0 || node.NodeType > 4)
                node.NodeType = 0;
            if (float.IsNaN(node.CornerRadius) || float.IsInfinity(node.CornerRadius))
                node.CornerRadius = -1f;
            node.CornerRadius = Math.Max(-1f, Math.Min(256f, node.CornerRadius));
            if (float.IsNaN(node.RainCornerRadius) || float.IsInfinity(node.RainCornerRadius) || node.RainCornerRadius < 0f)
                node.RainCornerRadius = 0f;
            node.RainCornerRadius = Math.Max(0f, Math.Min(256f, node.RainCornerRadius));
            node.ColorBgNormal = CloneColor(node.ColorBgNormal, new float[] { 0.2f, 0.2f, 0.2f, 0.8f });
            node.ColorBgPressed = CloneColor(node.ColorBgPressed, new float[] { 0.8f, 0.8f, 0.8f, 0.8f });
            node.ColorBorderNormal = CloneColor(node.ColorBorderNormal, new float[] { 0.4f, 0.4f, 0.4f, 1.0f });
            node.ColorBorderPressed = CloneColor(node.ColorBorderPressed, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            node.ColorTextNormal = CloneColor(node.ColorTextNormal, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            node.ColorTextPressed = CloneColor(node.ColorTextPressed, new float[] { 0.0f, 0.0f, 0.0f, 1.0f });
            node.BackgroundGradientNormal = CloneAxisGradient(node.BackgroundGradientNormal, node.ColorBgNormal, node.ColorBgNormal);
            node.BackgroundGradientPressed = CloneAxisGradient(node.BackgroundGradientPressed, node.ColorBgPressed, node.ColorBgPressed);
            node.BorderGradientNormal = CloneAxisGradient(node.BorderGradientNormal, node.ColorBorderNormal, node.ColorBorderNormal);
            node.BorderGradientPressed = CloneAxisGradient(node.BorderGradientPressed, node.ColorBorderPressed, node.ColorBorderPressed);
            node.TextGradientNormal = CloneAxisGradient(node.TextGradientNormal, node.ColorTextNormal, node.ColorTextNormal);
            node.TextGradientPressed = CloneAxisGradient(node.TextGradientPressed, node.ColorTextPressed, node.ColorTextPressed);
            node.RainColor = CloneColor(node.RainColor, new float[] { 0.8f, 0.5f, 1.0f, 0.8f });
            node.RainGradientEndColor = CloneColor(node.RainGradientEndColor, new float[] { 1f, 0.25f, 0.8f, 0.8f });
            node.RainHorizontalGradientEndColor = CloneColor(node.RainHorizontalGradientEndColor, node.RainColor);
            node.RainGradientMode = Math.Max(0, Math.Min(1, node.RainGradientMode));
            node.RainFadeHeight = NormalizeRainCurveHeight(node.RainFadeHeight);
            node.RainFadePower = NormalizeRainCurvePower(node.RainFadePower);
            node.RainGradientHeight = NormalizeRainCurveHeight(node.RainGradientHeight);
            node.RainGradientPower = NormalizeRainCurvePower(node.RainGradientPower);
            node.RainShadowColor = CloneColor(node.RainShadowColor, new float[] { 0f, 0f, 0f, 0.35f });
            node.RainShadowOffset = ClonePair(node.RainShadowOffset, 0f, 0f);
            NormalizeLegacyKeyRainShadowOffset(node.RainShadowOffset);
            if (float.IsNaN(node.RainShadowSoftness) || float.IsInfinity(node.RainShadowSoftness) || node.RainShadowSoftness < 0f)
                node.RainShadowSoftness = 12f;
            if (float.IsNaN(node.RainShadowStrength) || float.IsInfinity(node.RainShadowStrength))
                node.RainShadowStrength = 1f;
            node.RainShadowSoftness = Math.Max(0f, Math.Min(64f, node.RainShadowSoftness));
            node.RainShadowStrength = Math.Max(0f, Math.Min(1f, node.RainShadowStrength));
            if (float.IsNaN(node.KeyPressAnimationDuration) || float.IsInfinity(node.KeyPressAnimationDuration) || node.KeyPressAnimationDuration <= 0f)
                node.KeyPressAnimationDuration = 0.12f;
            node.KeyPressAnimationDuration = Math.Max(0.01f, Math.Min(2.0f, node.KeyPressAnimationDuration));
            if (string.IsNullOrEmpty(node.KeyPressAnimationEasing))
                node.KeyPressAnimationEasing = "ease-out-quad";
            if (float.IsNaN(node.KeyPressAnimationScale) || float.IsInfinity(node.KeyPressAnimationScale))
                node.KeyPressAnimationScale = 1.0f;
            node.KeyPressAnimationScale = Math.Max(0.2f, Math.Min(3.0f, node.KeyPressAnimationScale));
            if (float.IsNaN(node.KeyPressAnimationOffsetX) || float.IsInfinity(node.KeyPressAnimationOffsetX))
                node.KeyPressAnimationOffsetX = 0f;
            if (float.IsNaN(node.KeyPressAnimationOffsetY) || float.IsInfinity(node.KeyPressAnimationOffsetY))
                node.KeyPressAnimationOffsetY = 0f;
            node.KeyPressAnimationOffsetX = Math.Max(-200f, Math.Min(200f, node.KeyPressAnimationOffsetX));
            node.KeyPressAnimationOffsetY = Math.Max(-200f, Math.Min(200f, node.KeyPressAnimationOffsetY));
            if (node.NodeType == 4)
                node.VideoLoop = true;
            if (float.IsNaN(node.VideoContentScale) || float.IsInfinity(node.VideoContentScale) || node.VideoContentScale <= 0f)
                node.VideoContentScale = 1f;
            node.VideoContentScale = Math.Max(0.1f, Math.Min(10f, node.VideoContentScale));
            if (float.IsNaN(node.VideoContentOffsetX) || float.IsInfinity(node.VideoContentOffsetX))
                node.VideoContentOffsetX = 0f;
            if (float.IsNaN(node.VideoContentOffsetY) || float.IsInfinity(node.VideoContentOffsetY))
                node.VideoContentOffsetY = 0f;
            node.VideoContentOffsetX = Math.Max(-1f, Math.Min(1f, node.VideoContentOffsetX));
            node.VideoContentOffsetY = Math.Max(-1f, Math.Min(1f, node.VideoContentOffsetY));
        }

        private static void NormalizeLegacyKeyRainShadowOffset(float[] offset)
        {
            if (offset == null || offset.Length < 2) return;
            if (Math.Abs(offset[0] - 3f) <= 0.001f && Math.Abs(offset[1] - 3f) <= 0.001f)
            {
                offset[0] = 0f;
                offset[1] = 0f;
            }
        }

        private static float NormalizeRainCurveHeight(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                value = 1f;
            return Math.Max(0.05f, Math.Min(3.0f, value));
        }

        private static float NormalizeRainCurvePower(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                value = 1f;
            return Math.Max(0.1f, Math.Min(5.0f, value));
        }

        public void EnsureKeyViewerConfigurations()
        {
            if (KeyViewerConfigurations == null)
                KeyViewerConfigurations = new System.Collections.Generic.List<KVConfiguration>();

            var seenNodeLists = new System.Collections.Generic.HashSet<System.Collections.Generic.List<KVNode>>();
            var seenNodes = new System.Collections.Generic.HashSet<KVNode>();

            for (int i = 0; i < KeyViewerConfigurations.Count; i++)
            {
                if (KeyViewerConfigurations[i] == null)
                    KeyViewerConfigurations[i] = new KVConfiguration();

                KVConfiguration config = KeyViewerConfigurations[i];
                if (string.IsNullOrEmpty(config.Name))
                    config.Name = "KV 配置 " + (i + 1).ToString();
                if (config.Nodes == null)
                    config.Nodes = new System.Collections.Generic.List<KVNode>();
                else if (!seenNodeLists.Add(config.Nodes))
                    config.Nodes = CloneKeyViewerNodes(config.Nodes);

                for (int j = 0; j < config.Nodes.Count; j++)
                {
                    if (config.Nodes[j] == null)
                    {
                        config.Nodes[j] = new KVNode();
                    }
                    else if (!seenNodes.Add(config.Nodes[j]))
                    {
                        config.Nodes[j] = CloneKeyViewerNode(config.Nodes[j]);
                        seenNodes.Add(config.Nodes[j]);
                    }
                    EnsureKeyViewerNodeDefaults(config.Nodes[j]);
                }
                EnforceSingleVideoNode(config.Nodes);
                EnsureKeyViewerConfigurationAppearance(config);
            }

            if (KeyViewerSelectedConfigIndex >= KeyViewerConfigurations.Count)
                KeyViewerSelectedConfigIndex = KeyViewerConfigurations.Count - 1;
            if (KeyViewerSelectedConfigIndex < 0)
                KeyViewerSelectedConfigIndex = KeyViewerConfigurations.Count > 0 ? 0 : -1;
        }

        public KVConfiguration GetSelectedKeyViewerConfiguration()
        {
            EnsureKeyViewerConfigurations();
            if (KeyViewerConfigurations.Count == 0)
                return null;

            if (KeyViewerSelectedConfigIndex < 0 || KeyViewerSelectedConfigIndex >= KeyViewerConfigurations.Count)
                KeyViewerSelectedConfigIndex = 0;
            return KeyViewerConfigurations[KeyViewerSelectedConfigIndex];
        }

        public System.Collections.Generic.List<KVNode> GetSelectedKeyViewerNodes()
        {
            KVConfiguration config = GetSelectedKeyViewerConfiguration();
            return config != null ? config.Nodes : null;
        }

        public System.Collections.Generic.List<KVNode> GetAllKeyViewerNodes()
        {
            var nodes = new System.Collections.Generic.List<KVNode>();
            if (KeyViewerConfigurations == null)
                return nodes;

            foreach (var config in KeyViewerConfigurations)
            {
                if (config == null || config.Nodes == null) continue;
                nodes.AddRange(config.Nodes);
            }
            return nodes;
        }

        private static void EnforceSingleVideoNode(System.Collections.Generic.List<KVNode> nodes)
        {
            if (nodes == null) return;

            bool hasVideo = false;
            for (int i = 0; i < nodes.Count; i++)
            {
                KVNode node = nodes[i];
                if (node == null || node.NodeType != 4) continue;

                if (!hasVideo)
                {
                    hasVideo = true;
                    node.VideoLoop = true;
                }
                else
                {
                    node.NodeType = 3;
                }
            }
        }

        public KVConfiguration FindKeyViewerConfigurationForNode(KVNode node)
        {
            if (node == null || KeyViewerConfigurations == null)
                return null;

            foreach (var config in KeyViewerConfigurations)
            {
                if (config == null || config.Nodes == null) continue;
                if (config.Nodes.Contains(node)) return config;
            }
            return null;
        }

        public KVConfiguration CreateKeyViewerConfiguration(string name, int presetKeyCount)
        {
            var config = new KVConfiguration();
            config.Name = string.IsNullOrEmpty(name) ? "新配置" : name;
            config.IsEnabled = true;
            CopyGlobalKeyViewerAppearanceTo(config);
            config.Nodes = presetKeyCount > 0
                ? GenerateDefaultKVLayout(presetKeyCount, config.DefaultWidth, config.DefaultHeight)
                : new System.Collections.Generic.List<KVNode>();
            return config;
        }

        private static void EnsureOverlayerVideoDefaults(OverlayerVideo video)
        {
            if (video == null) return;
            if (string.IsNullOrEmpty(video.Name))
                video.Name = "新视频";
            if (float.IsNaN(video.Width) || float.IsInfinity(video.Width) || video.Width <= 0f)
                video.Width = 320f;
            if (float.IsNaN(video.Height) || float.IsInfinity(video.Height) || video.Height <= 0f)
                video.Height = 180f;
            if (float.IsNaN(video.Opacity) || float.IsInfinity(video.Opacity))
                video.Opacity = 1f;
            video.Opacity = Math.Max(0f, Math.Min(1f, video.Opacity));
            if (float.IsNaN(video.PivotX) || float.IsInfinity(video.PivotX))
                video.PivotX = 0f;
            if (float.IsNaN(video.PivotY) || float.IsInfinity(video.PivotY))
                video.PivotY = 0f;
            video.PivotX = Math.Max(0f, Math.Min(1f, video.PivotX));
            video.PivotY = Math.Max(0f, Math.Min(1f, video.PivotY));
            video.Depth = RenderDepth.ClampDepth(video.Depth);
            video.Loop = true;
            if (float.IsNaN(video.ContentScale) || float.IsInfinity(video.ContentScale) || video.ContentScale <= 0f)
                video.ContentScale = 1f;
            video.ContentScale = Math.Max(0.1f, Math.Min(10f, video.ContentScale));
            if (float.IsNaN(video.ContentOffsetX) || float.IsInfinity(video.ContentOffsetX))
                video.ContentOffsetX = 0f;
            if (float.IsNaN(video.ContentOffsetY) || float.IsInfinity(video.ContentOffsetY))
                video.ContentOffsetY = 0f;
            video.ContentOffsetX = Math.Max(-1f, Math.Min(1f, video.ContentOffsetX));
            video.ContentOffsetY = Math.Max(-1f, Math.Min(1f, video.ContentOffsetY));
        }

        private static void EnsureOverlayerProgressBarDefaults(OverlayerProgressBar bar)
        {
            if (bar == null) return;
            if (string.IsNullOrEmpty(bar.Name))
                bar.Name = "新进度条";
            if (bar.ValueSource == null)
                bar.ValueSource = new OverlayerProgressValueSource(OverlayerProgressValueKind.Progress);
            if (bar.MinSource == null)
                bar.MinSource = new OverlayerProgressValueSource(OverlayerProgressValueKind.Constant, 0.0);
            if (bar.MaxSource == null)
                bar.MaxSource = new OverlayerProgressValueSource(OverlayerProgressValueKind.Constant, 100.0);
            if (bar.BackgroundColor == null || bar.BackgroundColor.Length != 4)
                bar.BackgroundColor = new float[] { 0f, 0f, 0f, 0.45f };
            if (bar.FillColor == null || bar.FillColor.Length != 4)
                bar.FillColor = new float[] { 0.2f, 0.75f, 1f, 0.95f };
            if (bar.FillGradientStartColor == null || bar.FillGradientStartColor.Length != 4)
                bar.FillGradientStartColor = new float[] { 1f, 0.25f, 0.25f, 0.95f };
            if (bar.FillGradientEndColor == null || bar.FillGradientEndColor.Length != 4)
                bar.FillGradientEndColor = new float[] { 0.25f, 1f, 0.35f, 0.95f };
            if (bar.BorderColor == null || bar.BorderColor.Length != 4)
                bar.BorderColor = new float[] { 1f, 1f, 1f, 0.8f };
            if (bar.ShadowColor == null || bar.ShadowColor.Length != 4)
                bar.ShadowColor = new float[] { 0f, 0f, 0f, 0.45f };
            if (bar.ShadowOffset == null || bar.ShadowOffset.Length != 2)
                bar.ShadowOffset = new float[] { 2f, 2f };

            if (float.IsNaN(bar.Width) || float.IsInfinity(bar.Width) || bar.Width <= 0f)
                bar.Width = 300f;
            if (float.IsNaN(bar.Height) || float.IsInfinity(bar.Height) || bar.Height <= 0f)
                bar.Height = 20f;
            if (float.IsNaN(bar.Opacity) || float.IsInfinity(bar.Opacity))
                bar.Opacity = 1f;
            bar.Opacity = Math.Max(0f, Math.Min(1f, bar.Opacity));
            bar.Depth = RenderDepth.ClampDepth(bar.Depth);
            if (float.IsNaN(bar.PivotX) || float.IsInfinity(bar.PivotX))
                bar.PivotX = 0f;
            if (float.IsNaN(bar.PivotY) || float.IsInfinity(bar.PivotY))
                bar.PivotY = 0f;
            bar.PivotX = Math.Max(0f, Math.Min(1f, bar.PivotX));
            bar.PivotY = Math.Max(0f, Math.Min(1f, bar.PivotY));
            if (float.IsNaN(bar.BorderThickness) || float.IsInfinity(bar.BorderThickness) || bar.BorderThickness < 0f)
                bar.BorderThickness = 0f;
            if (float.IsNaN(bar.CornerRadius) || float.IsInfinity(bar.CornerRadius) || bar.CornerRadius < 0f)
                bar.CornerRadius = 0f;
            if (float.IsNaN(bar.ShadowSoftness) || float.IsInfinity(bar.ShadowSoftness) || bar.ShadowSoftness < 0f)
                bar.ShadowSoftness = 0f;

            if (!Enum.IsDefined(typeof(OverlayerProgressValueKind), bar.ValueSource.Kind))
                bar.ValueSource.Kind = OverlayerProgressValueKind.Progress;
            if (!Enum.IsDefined(typeof(OverlayerProgressValueKind), bar.MinSource.Kind))
                bar.MinSource.Kind = OverlayerProgressValueKind.Constant;
            if (!Enum.IsDefined(typeof(OverlayerProgressValueKind), bar.MaxSource.Kind))
                bar.MaxSource.Kind = OverlayerProgressValueKind.Constant;
            if (!Enum.IsDefined(typeof(OverlayerProgressFillDirection), bar.FillDirection))
                bar.FillDirection = OverlayerProgressFillDirection.LeftToRight;
        }

        public void EnsureGameUIElementSettings()
        {
            if (GameUIElements == null)
                GameUIElements = new System.Collections.Generic.List<GameUIElementSetting>();

            foreach (var target in GameUIManager.Targets)
            {
                if (FindGameUIElement(target.Id) == null)
                {
                    GameUIElements.Add(new GameUIElementSetting(target.Id));
                }
            }

            foreach (var element in GameUIElements)
            {
                if (element == null)
                    continue;

                if (string.IsNullOrEmpty(element.Id))
                    element.Id = "";

                if (float.IsNaN(element.Scale) || float.IsInfinity(element.Scale) || element.Scale <= 0f)
                    element.Scale = 1f;
                element.Scale = Math.Max(0.05f, Math.Min(5f, element.Scale));

                if (float.IsNaN(element.Alpha) || float.IsInfinity(element.Alpha))
                    element.Alpha = 1f;
                element.Alpha = Math.Max(0f, Math.Min(1f, element.Alpha));

                if (float.IsNaN(element.OffsetX) || float.IsInfinity(element.OffsetX))
                    element.OffsetX = 0f;
                if (float.IsNaN(element.OffsetY) || float.IsInfinity(element.OffsetY))
                    element.OffsetY = 0f;
            }
        }

        public GameUIElementSetting GetGameUIElement(string id)
        {
            if (GameUIElements == null)
                GameUIElements = new System.Collections.Generic.List<GameUIElementSetting>();

            var existing = FindGameUIElement(id);
            if (existing != null)
                return existing;

            var created = new GameUIElementSetting(id);
            GameUIElements.Add(created);
            return created;
        }

        public void ResetGameUIElementSettings()
        {
            GameUIElements = new System.Collections.Generic.List<GameUIElementSetting>();
            EnsureGameUIElementSettings();
        }

        private GameUIElementSetting FindGameUIElement(string id)
        {
            if (GameUIElements == null)
                return null;

            foreach (var element in GameUIElements)
            {
                if (element != null && string.Equals(element.Id, id, StringComparison.Ordinal))
                    return element;
            }

            return null;
        }

        public System.Collections.Generic.List<KVNode> GenerateDefaultKVLayout(int count)
        {
            return GenerateDefaultKVLayout(count, KeyViewerDefaultWidth, KeyViewerDefaultHeight);
        }

        public System.Collections.Generic.List<KVNode> GenerateDefaultKVLayout(int count, float defaultWidth, float defaultHeight)
        {
            var list = new System.Collections.Generic.List<KVNode>();
            int rows = count > 8 ? 2 : 1;
            float padding = 4f;
            float boxWidth = defaultWidth;
            float boxHeight = defaultHeight;
            float startX = 20f;
            float startY = 50f;

            for (int r = 0; r < rows; r++)
            {
                int cols = (r == 1) ? (count - 8) : System.Math.Min(count, 8);
                for (int c = 0; c < cols; c++)
                {
                    int index = r * 8 + c;
                    string bind = index < DefaultKeyBindings.Length ? DefaultKeyBindings[index] : "None";
                    string customText = "";
                    
                    if (bind == "Tab") customText = "Tab";
                    else if (bind == "Backspace") customText = "Back";
                    else if (bind == "LeftShift") customText = "LS";
                    else if (bind == "RightShift") customText = "RS";
                    else if (bind == "Space") customText = "Spc";
                    else if (bind == "Return") customText = "Ent";
                    else if (bind == "Equals") customText = "=";
                    else if (bind == "Backslash") customText = "\\";
                    else if (bind == "Comma") customText = ",";
                    else if (bind == "Period") customText = ".";
                    else if (bind.StartsWith("Alpha")) customText = bind.Substring(5);

                    list.Add(new KVNode(bind, startX + c * (boxWidth + padding), startY + r * (boxHeight + padding)) { 
                        Width = boxWidth, 
                        Height = boxHeight,
                        CustomText = customText,
                        TextScale = 0.85f,
                        CountScale = 0.65f
                    });
                }
            }

            // Generate KPS and Total Boxes
            float fullWidth = 8 * boxWidth + 7 * padding;
            float shortBoxHeight = 30f;
            float kpsTotalWidth = (fullWidth - padding) / 2.0f;
            
            if (count == 16)
            {
                float row3Y = startY + 2 * (boxHeight + padding);
                list.Add(new KVNode(1, startX, row3Y, kpsTotalWidth, shortBoxHeight));
                list.Add(new KVNode(2, startX + kpsTotalWidth + padding, row3Y, kpsTotalWidth, shortBoxHeight));
            }
            else if (count == 8)
            {
                float row2Y = startY + boxHeight + padding;
                list.Add(new KVNode(1, startX, row2Y, kpsTotalWidth, shortBoxHeight));
                list.Add(new KVNode(2, startX + kpsTotalWidth + padding, row2Y, kpsTotalWidth, shortBoxHeight));
            }
            else
            {
                float row2Y = startY + boxHeight + padding;
                float kWidth = count == 12 ? (2 * boxWidth + padding) : (3 * boxWidth + 2 * padding);
                int row2Keys = count == 12 ? 4 : 2;
                
                list.Add(new KVNode(1, startX, row2Y, kWidth, boxHeight));
                float totalX = startX + kWidth + padding + row2Keys * (boxWidth + padding);
                list.Add(new KVNode(2, totalX, row2Y, kWidth, boxHeight));
            }

            return list;
        }

        
    }

    public static class Main
    {
        public static UnityModManager.ModEntry.ModLogger Logger;
        public static Settings Settings;
        public static Harmony harmony;
        public static bool IsEnabled = false;

        public static UnityModManager.ModEntry ModEntry;
        
        public static bool _isSaveRequested = false;
        public static void RequestSave()
        {
            _isSaveRequested = true;
            OverlayRenderInvalidator.InvalidateAll();
        }
        
        private static UnityEngine.GameObject _imguiGameObject;

        private static IntPtr _cimguiHandle = IntPtr.Zero;

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            Logger = modEntry.Logger;
            ModEntry = modEntry;
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            Settings.InitNulls();
            LocalizationManager.Initialize(Settings.Language);
            if (CheryToolsAssets.ImportSettingsAssets(Settings))
            {
                Settings.Save(modEntry);
                LocalizationManager.Reload(Settings.Language);
            }
            
            EnsureNativeDependenciesLoaded(modEntry);

            harmony = new Harmony(modEntry.Info.Id);

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.Label("CheryTools 正在使用 Dear ImGui 作为其界面。");
            string keyName = Settings.ToggleMenuKey.ToString();
            if (GUILayout.Button($"打开 ImGui 面板 (或在游戏中按 {keyName} 键)", GUILayout.Width(350)))
            {
                CheryToolsMenu.IsMenuOpen = !CheryToolsMenu.IsMenuOpen;
            }
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.Save(modEntry);
        }

        public static bool IsGamePlaying()
        {
            if (scrController.instance == null) return false;
            if (!scrController.instance.gameworld) return false;
            if (scrController.instance.paused) return false;
            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            IsEnabled = value;
            if (value)
            {
                harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
                
                if (_imguiGameObject == null)
                {
                    Logger.Log("Initializing CheryTools_ImGui GameObject...");
                    _imguiGameObject = new UnityEngine.GameObject("CheryTools_ImGui");
                    UnityEngine.GameObject.DontDestroyOnLoad(_imguiGameObject);
                    var controller = _imguiGameObject.AddComponent<ImGuiController>();
                    if (_imguiGameObject.GetComponent<CheryToolsMenu>() == null)
                        _imguiGameObject.AddComponent<CheryToolsMenu>();
                    
                    if (_imguiGameObject.GetComponent<KeyViewerManager>() == null)
                        _imguiGameObject.AddComponent<KeyViewerManager>();

                    if (_imguiGameObject.GetComponent<KeyViewerOverlay>() == null)
                        _imguiGameObject.AddComponent<KeyViewerOverlay>();
                        

                    if (_imguiGameObject.GetComponent<OverlayerManager>() == null)
                        _imguiGameObject.AddComponent<OverlayerManager>();

                    if (_imguiGameObject.GetComponent<GameUIManager>() == null)
                        _imguiGameObject.AddComponent<GameUIManager>();

                    controller.OnImGuiLayout += _imguiGameObject.GetComponent<CheryToolsMenu>().RenderUI;
                    controller.OnOverlayLayout += _imguiGameObject.GetComponent<KeyViewerOverlay>().RenderUI;
                    controller.OnOverlayLayout += _imguiGameObject.GetComponent<OverlayerManager>().RenderUI;
                    Logger.Log("ImGuiController, CheryToolsMenu, KeyViewer, Overlayer components added to GameObject.");
                }
                
                InputInterceptor.UpdateAllowedKeys();
            }
            else
            {
                harmony.UnpatchAll(modEntry.Info.Id);
                InputInterceptor.ResetPatches();
                
                if (_imguiGameObject != null)
                {
                    UnityEngine.GameObject.Destroy(_imguiGameObject);
                    _imguiGameObject = null;
                }
                SdfTextRenderer.Shutdown();
                KeyViewerUnityRenderer.Shutdown();
                OverlayerUnityRenderer.Shutdown();
                TextureManager.Clear();
                VideoTextureManager.Shutdown();
            }
            return true;
        }

        private static void EnsureNativeDependenciesLoaded(UnityModManager.ModEntry modEntry)
        {
            if (_cimguiHandle != IntPtr.Zero || modEntry == null)
            {
                return;
            }

            string cimguiPath = System.IO.Path.Combine(modEntry.Path, "cimgui.dll");
            if (!System.IO.File.Exists(cimguiPath))
            {
                Logger?.Log("[CheryTools] cimgui.dll not found: " + cimguiPath);
                return;
            }

            _cimguiHandle = LoadLibrary(cimguiPath);
            if (_cimguiHandle == IntPtr.Zero)
            {
                int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                Logger?.Log("[CheryTools] Failed to load cimgui.dll from mod folder. Win32Error=" + error);
            }
        }

        [HarmonyPatch(typeof(scrPlayer), "CountValidKeysPressed")]
        public static class scrPlayer_CountValidKeysPressed_Patch
        {
            public static bool Prefix(ref int __result)
            {
                // If the ImGui menu is open, we block all game inputs!
                if (CheryToolsMenu.IsMenuOpen)
                {
                    __result = 0;
                    return false;
                }
                return true;
            }
        }

        internal static bool ShouldHideHitText(HitMargin hitMargin)
        {
            if (!IsEnabled || Settings == null || !Settings.HideHitTextEnabled)
                return false;

            switch (hitMargin)
            {
                case HitMargin.TooEarly: return Settings.HideHitTextTooEarly;
                case HitMargin.VeryEarly: return Settings.HideHitTextVeryEarly;
                case HitMargin.EarlyPerfect: return Settings.HideHitTextEarlyPerfect;
                case HitMargin.Perfect: return Settings.HideHitTextPerfect;
                case HitMargin.LatePerfect: return Settings.HideHitTextLatePerfect;
                case HitMargin.VeryLate: return Settings.HideHitTextVeryLate;
                case HitMargin.TooLate: return Settings.HideHitTextTooLate;
                case HitMargin.Multipress: return Settings.HideHitTextMultipress;
                case HitMargin.FailMiss: return Settings.HideHitTextFailMiss;
                case HitMargin.FailOverload: return Settings.HideHitTextFailOverload;
                case HitMargin.OverPress: return Settings.HideHitTextOverPress;
                default: return false;
            }
        }
    }

    [HarmonyPatch(typeof(scrHitTextManager), "ShowHitText")]
    public static class scrHitTextManager_ShowHitText_Patch
    {
        public static bool Prefix(HitMargin hitMargin)
        {
            return !Main.ShouldHideHitText(hitMargin);
        }
    }

    [HarmonyPatch(typeof(scrShowIfDebug), "Awake")]
    public static class scrShowIfDebug_Awake_Patch
    {
        public static void Postfix(scrShowIfDebug __instance)
        {
            GameUIManager.RegisterAutoplayStatusText(__instance);
        }
    }

    [HarmonyPatch(typeof(scrEnableIfBeta), "Awake")]
    public static class scrEnableIfBeta_Awake_Patch
    {
        public static void Postfix(scrEnableIfBeta __instance)
        {
            GameUIManager.RegisterBuildWatermark(__instance);
        }
    }

    public static class EditorInputOptimizationPatches
    {
        private static readonly FieldInfo ScnEditorPlayModeField = AccessTools.Field(typeof(scnEditor), "playMode");
        private static readonly PropertyInfo ScnEditorPlayModeProperty = AccessTools.Property(typeof(scnEditor), "playMode");

        internal static bool GetKeyDownForAutoplayPause(KeyCode key)
        {
            if (key == KeyCode.Space
                && Main.IsEnabled
                && Main.Settings != null
                && Main.Settings.DisableAutoplaySpacePause
                && ADOBase.isLevelEditor
                && RDC.auto)
            {
                return false;
            }

            return Input.GetKeyDown(key);
        }

        internal static bool ShouldBlockPlayModeScrollZoom(scnEditor editor)
        {
            if (!Main.IsEnabled || Main.Settings == null || !Main.Settings.DisablePlayModeScrollZoom)
                return false;
            if (!ADOBase.isLevelEditor || editor == null)
                return false;

            if (ScnEditorPlayModeField != null && ScnEditorPlayModeField.FieldType == typeof(bool))
            {
                return (bool)ScnEditorPlayModeField.GetValue(editor);
            }

            if (ScnEditorPlayModeProperty != null && ScnEditorPlayModeProperty.PropertyType == typeof(bool))
            {
                return (bool)ScnEditorPlayModeProperty.GetValue(editor, null);
            }

            return scrController.instance != null && scrController.instance.gameworld && !scrController.instance.paused;
        }

        internal static void ZoomCameraFromMouseWheel(scnEditor editor, float delta, bool anchorAtPointer, bool instant)
        {
            if (ShouldBlockPlayModeScrollZoom(editor))
                return;

            editor.ZoomCamera(delta, anchorAtPointer, instant);
        }

        internal static bool LoadsKeyCode(CodeInstruction instruction, KeyCode key)
        {
            if (instruction == null)
                return false;

            int expected = (int)key;
            if (instruction.opcode == OpCodes.Ldc_I4_M1) return expected == -1;
            if (instruction.opcode == OpCodes.Ldc_I4_0) return expected == 0;
            if (instruction.opcode == OpCodes.Ldc_I4_1) return expected == 1;
            if (instruction.opcode == OpCodes.Ldc_I4_2) return expected == 2;
            if (instruction.opcode == OpCodes.Ldc_I4_3) return expected == 3;
            if (instruction.opcode == OpCodes.Ldc_I4_4) return expected == 4;
            if (instruction.opcode == OpCodes.Ldc_I4_5) return expected == 5;
            if (instruction.opcode == OpCodes.Ldc_I4_6) return expected == 6;
            if (instruction.opcode == OpCodes.Ldc_I4_7) return expected == 7;
            if (instruction.opcode == OpCodes.Ldc_I4_8) return expected == 8;

            if ((instruction.opcode == OpCodes.Ldc_I4 || instruction.opcode == OpCodes.Ldc_I4_S) && instruction.operand != null)
            {
                try
                {
                    return Convert.ToInt32(instruction.operand) == expected;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(scnEditor), "Update")]
    public static class scnEditor_Update_AutoplayPause_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getKeyDown = AccessTools.Method(typeof(Input), nameof(Input.GetKeyDown), new[] { typeof(KeyCode) });
            MethodInfo guardedGetKeyDown = AccessTools.Method(typeof(EditorInputOptimizationPatches), nameof(EditorInputOptimizationPatches.GetKeyDownForAutoplayPause));
            MethodInfo zoomCamera = AccessTools.Method(typeof(scnEditor), nameof(scnEditor.ZoomCamera), new[] { typeof(float), typeof(bool), typeof(bool) });
            MethodInfo guardedZoomCamera = AccessTools.Method(typeof(EditorInputOptimizationPatches), nameof(EditorInputOptimizationPatches.ZoomCameraFromMouseWheel));
            var list = new List<CodeInstruction>(instructions);
            bool replacedSpacePause = false;
            bool replacedMouseWheelZoom = false;

            for (int i = 1; i < list.Count; i++)
            {
                if (!replacedSpacePause && list[i].Calls(getKeyDown) && EditorInputOptimizationPatches.LoadsKeyCode(list[i - 1], KeyCode.Space))
                {
                    list[i].operand = guardedGetKeyDown;
                    replacedSpacePause = true;
                    continue;
                }

                if (!replacedMouseWheelZoom && list[i].Calls(zoomCamera))
                {
                    list[i].opcode = OpCodes.Call;
                    list[i].operand = guardedZoomCamera;
                    replacedMouseWheelZoom = true;
                }
            }

            return list;
        }
    }

}
