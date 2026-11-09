using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.Tools.Preview
{
    [Serializable]
    public sealed class ToolsEditorSettings
    {
        public bool ShowFloorAngle;
        public bool ShowFloorBeats;
        public bool ShowFloorCount;
        public bool ShowFloorDuration;
        public bool UseTulttakBehavior;
        public bool FineTuneFloorRotations;
        public float FloorRotationStep = 22.5f;
        public bool SyncLevelEventValues;
    }

    [Serializable]
    public sealed class ToolsUiVisibilitySettings
    {
        public bool HideEverything;
        public bool HideJudgment;
        public bool HideMissIndicators;
        public bool HideTitle;
        public bool HideOtto;
        public bool HideTimingTarget;
        public bool HideNoFailIcon;
        public bool HideBeta;
        public bool HideResult;
        public bool HideHitErrorMeter;
        public bool HideLastFloorFlash;
    }

    [Serializable]
    public sealed class ToolsSettings
    {
        public const int MaxLimitedKeys = 128;

        public bool DisableAutoplaySpacePause;
        public bool DisablePlayModeScrollZoom;
        public bool AntiBounceKeys;
        public float AntiBounceIntervalMs = 15f;
        public bool LimitInput;
        public List<KeyCode> LimitedKeys = new List<KeyCode>();

        public bool HideHitTextEnabled;
        public bool HideTooEarly;
        public bool HideVeryEarly;
        public bool HideEarlyPerfect;
        public bool HidePerfect;
        public bool HideLatePerfect;
        public bool HideVeryLate;
        public bool HideTooLate;
        public bool HideMultipress;
        public bool HideFailMiss;
        public bool HideFailOverload;
        public bool HideOverPress;

        public bool EnableCustomPlanetColors;
        public float[] RedPlanetColor = { 1f, 0f, 0f, 1f };
        public float[] RedRingColor = { 1f, 0f, 0f, 0.4f };
        public float[] RedTailColor = { 1f, 0f, 0f, 1f };
        public float[] BluePlanetColor = { 0f, 0.45f, 1f, 1f };
        public float[] BlueRingColor = { 0f, 0.45f, 1f, 0.4f };
        public float[] BlueTailColor = { 0f, 0.45f, 1f, 1f };
        public float[] GreenPlanetColor = { 0.3f, 0.7f, 0f, 1f };
        public float[] GreenRingColor = { 0.3f, 0.7f, 0f, 0.4f };
        public float[] GreenTailColor = { 0.3f, 0.7f, 0f, 1f };

        public ToolsEditorSettings Editor = new ToolsEditorSettings();
        public ToolsUiVisibilitySettings PlayingUi = new ToolsUiVisibilitySettings();
        public ToolsUiVisibilitySettings RecordingUi = new ToolsUiVisibilitySettings();
        public bool RecordingMode;
        public bool UseRecordingModeShortcut;
        public KeyCode RecordingModeShortcut = KeyCode.F8;

        public bool EnablePlanetOpacity;
        public float RedPlanetOpacity = 100f;
        public float RedRingOpacity = 100f;
        public float RedTailOpacity = 100f;
        public float BluePlanetOpacity = 100f;
        public float BlueRingOpacity = 100f;
        public float BlueTailOpacity = 100f;
        public float GreenPlanetOpacity = 100f;
        public float GreenRingOpacity = 100f;
        public float GreenTailOpacity = 100f;

        internal void Normalize()
        {
            AntiBounceIntervalMs = ClampFinite(AntiBounceIntervalMs, 1f, 100f, 15f);
            if (LimitedKeys == null) LimitedKeys = new List<KeyCode>();
            if (LimitedKeys.Count > MaxLimitedKeys)
                LimitedKeys.RemoveRange(MaxLimitedKeys, LimitedKeys.Count - MaxLimitedKeys);
            NormalizeColor(ref RedPlanetColor, 1f, 0f, 0f, 1f);
            NormalizeColor(ref RedRingColor, 1f, 0f, 0f, 0.4f);
            NormalizeColor(ref RedTailColor, 1f, 0f, 0f, 1f);
            NormalizeColor(ref BluePlanetColor, 0f, 0.45f, 1f, 1f);
            NormalizeColor(ref BlueRingColor, 0f, 0.45f, 1f, 0.4f);
            NormalizeColor(ref BlueTailColor, 0f, 0.45f, 1f, 1f);
            NormalizeColor(ref GreenPlanetColor, 0.3f, 0.7f, 0f, 1f);
            NormalizeColor(ref GreenRingColor, 0.3f, 0.7f, 0f, 0.4f);
            NormalizeColor(ref GreenTailColor, 0.3f, 0.7f, 0f, 1f);
            if (Editor == null) Editor = new ToolsEditorSettings();
            Editor.FloorRotationStep = ClampFinite(Editor.FloorRotationStep, 0.1f, 180f, 22.5f);
            if (PlayingUi == null) PlayingUi = new ToolsUiVisibilitySettings();
            if (RecordingUi == null) RecordingUi = new ToolsUiVisibilitySettings();
            RedPlanetOpacity = ClampFinite(RedPlanetOpacity, 0f, 100f, 100f);
            RedRingOpacity = ClampFinite(RedRingOpacity, 0f, 100f, 100f);
            RedTailOpacity = ClampFinite(RedTailOpacity, 0f, 100f, 100f);
            BluePlanetOpacity = ClampFinite(BluePlanetOpacity, 0f, 100f, 100f);
            BlueRingOpacity = ClampFinite(BlueRingOpacity, 0f, 100f, 100f);
            BlueTailOpacity = ClampFinite(BlueTailOpacity, 0f, 100f, 100f);
            GreenPlanetOpacity = ClampFinite(GreenPlanetOpacity, 0f, 100f, 100f);
            GreenRingOpacity = ClampFinite(GreenRingOpacity, 0f, 100f, 100f);
            GreenTailOpacity = ClampFinite(GreenTailOpacity, 0f, 100f, 100f);
        }

        internal static ToolsSettings Load(string path, CheryTools.Sonnet.Contracts.IModuleLogger logger)
        {
            if (!File.Exists(path)) return new ToolsSettings();
            try
            {
                var serializer = new XmlSerializer(typeof(ToolsSettings));
                using (var stream = File.OpenRead(path)) return serializer.Deserialize(stream) as ToolsSettings ?? new ToolsSettings();
            }
            catch (Exception ex)
            {
                logger?.Warning(L("Tools Preview 配置读取失败，使用默认设置：", "Could not read Tools Preview settings, using defaults: ") + ex.Message);
                return new ToolsSettings();
            }
        }

        internal void Save(string path, CheryTools.Sonnet.Contracts.IModuleLogger logger)
        {
            try
            {
                Normalize();
                var serializer = new XmlSerializer(typeof(ToolsSettings));
                using (var stream = File.Create(path)) serializer.Serialize(stream, this);
            }
            catch (Exception ex)
            {
                logger?.Warning(L("Tools Preview 配置保存失败：", "Could not save Tools Preview settings: ") + ex.Message);
            }
        }

        private static float ClampFinite(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = fallback;
            return Mathf.Clamp(value, min, max);
        }

        private static void NormalizeColor(ref float[] color, float r, float g, float b, float a)
        {
            if (color == null || color.Length != 4) color = new[] { r, g, b, a };
            for (int i = 0; i < 4; i++) color[i] = ClampFinite(color[i], 0f, 1f, i == 3 ? a : 1f);
        }
    }
}
