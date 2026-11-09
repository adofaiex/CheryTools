using System;
using System.IO;
using System.Xml.Serialization;
using CheryTools.Sonnet.Contracts;
using UnityEngine;

namespace CheryTools.ReadHelper.Preview
{
    [Serializable]
    public sealed class ReadHelperSettings
    {
        private static readonly Lazy<XmlSerializer> Serializer =
            new Lazy<XmlSerializer>(() => new XmlSerializer(typeof(ReadHelperSettings)));

        // --- 全局开关 ---
        public bool Enabled = true;
        public bool ShowInGameOnly = true;
        public bool HideWhenPaused = false;
        public bool ShowInEditor = true;
        public bool ShowSpeedLabels = true;

        // --- 位置与尺寸 ---
        public float PositionX = 0f;
        public float PositionY = 260f;
        public float Scale = 1f;
        public float TrackWidth = 900f;
        public float NoteHeight = 90f;
        public float NoteWidth = 3f;
        public float DoubleTapWidth = 8f;
        public float DoubleTapBorderThickness = 2f;
        public float TargetBoxWidth = 22f;
        public float TargetBoxHeight = 110f;
        public float TargetLineWidth = 2f;

        // --- 时间与流动参数 ---
        public float LookAheadSeconds = 1.5f;
        public float DoubleTapAngleThreshold = 30f;
        public float FadePower = 1.0f;
        public string TimeSignature = "4/4";
        public int FontSize = 14;
        public float LabelOffsetY = 18f;

        // --- 颜色配置 (RGBA 0..1) ---
        public float[] NormalNoteColor = { 1f, 1f, 1f, 1f };
        public float[] DoubleTapFillColor = { 1f, 1f, 1f, 1f };
        public float[] DoubleTapBorderColor = { 1f, 0.44f, 0.75f, 1f }; // #FF70C0 粉色
        public float[] DecelColor = { 0.30f, 0.82f, 0.88f, 1f }; // #4DD0E1 青蓝色
        public float[] AccelColor = { 0.90f, 0.22f, 0.21f, 1f }; // #E53935 深红色
        public float[] TargetBoxColor = { 0.85f, 0.85f, 0.85f, 0.85f };
        public float[] TargetLineColor = { 1f, 1f, 1f, 0.95f };

        public void Normalize()
        {
            PositionX = Finite(PositionX, -3840f, 3840f, 0f);
            PositionY = Finite(PositionY, -2160f, 2160f, 260f);
            Scale = Finite(Scale, 0.2f, 3f, 1f);
            TrackWidth = Finite(TrackWidth, 200f, 3000f, 900f);
            NoteHeight = Finite(NoteHeight, 10f, 500f, 90f);
            NoteWidth = Finite(NoteWidth, 1f, 30f, 3f);
            DoubleTapWidth = Finite(DoubleTapWidth, 2f, 50f, 8f);
            DoubleTapBorderThickness = Finite(DoubleTapBorderThickness, 0.5f, 10f, 2f);
            TargetBoxWidth = Finite(TargetBoxWidth, 6f, 100f, 22f);
            TargetBoxHeight = Finite(TargetBoxHeight, 20f, 500f, 110f);
            TargetLineWidth = Finite(TargetLineWidth, 1f, 20f, 2f);

            LookAheadSeconds = Finite(LookAheadSeconds, 0.3f, 5f, 1.5f);
            DoubleTapAngleThreshold = Finite(DoubleTapAngleThreshold, 1f, 90f, 30f);
            FadePower = Finite(FadePower, 0.1f, 5f, 1f);
            FontSize = Mathf.Clamp(FontSize, 8, 48);
            LabelOffsetY = Finite(LabelOffsetY, -100f, 100f, 18f);
            if (string.IsNullOrWhiteSpace(TimeSignature)) TimeSignature = "4/4";

            NormalizeColor(ref NormalNoteColor, 1f, 1f, 1f, 1f);
            NormalizeColor(ref DoubleTapFillColor, 1f, 1f, 1f, 1f);
            NormalizeColor(ref DoubleTapBorderColor, 1f, 0.44f, 0.75f, 1f);
            NormalizeColor(ref DecelColor, 0.30f, 0.82f, 0.88f, 1f);
            NormalizeColor(ref AccelColor, 0.90f, 0.22f, 0.21f, 1f);
            NormalizeColor(ref TargetBoxColor, 0.85f, 0.85f, 0.85f, 0.85f);
            NormalizeColor(ref TargetLineColor, 1f, 1f, 1f, 0.95f);
        }

        public void ResetToDefaults()
        {
            Enabled = true;
            ShowInGameOnly = true;
            HideWhenPaused = false;
            ShowInEditor = true;
            ShowSpeedLabels = true;

            PositionX = 0f;
            PositionY = 260f;
            Scale = 1f;
            TrackWidth = 900f;
            NoteHeight = 90f;
            NoteWidth = 3f;
            DoubleTapWidth = 8f;
            DoubleTapBorderThickness = 2f;
            TargetBoxWidth = 22f;
            TargetBoxHeight = 110f;
            TargetLineWidth = 2f;

            LookAheadSeconds = 1.5f;
            DoubleTapAngleThreshold = 30f;
            FadePower = 1.0f;
            TimeSignature = "4/4";
            FontSize = 14;
            LabelOffsetY = 18f;

            NormalNoteColor = new[] { 1f, 1f, 1f, 1f };
            DoubleTapFillColor = new[] { 1f, 1f, 1f, 1f };
            DoubleTapBorderColor = new[] { 1f, 0.44f, 0.75f, 1f };
            DecelColor = new[] { 0.30f, 0.82f, 0.88f, 1f };
            AccelColor = new[] { 0.90f, 0.22f, 0.21f, 1f };
            TargetBoxColor = new[] { 0.85f, 0.85f, 0.85f, 0.85f };
            TargetLineColor = new[] { 1f, 1f, 1f, 0.95f };
        }

        internal static ReadHelperSettings Load(string path, IModuleLogger logger)
        {
            try
            {
                if (File.Exists(path))
                {
                    using (FileStream stream = File.OpenRead(path))
                    {
                        var settings = (ReadHelperSettings)Serializer.Value.Deserialize(stream);
                        settings.Normalize();
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Warning("ReadHelper 配置读取失败，使用默认配置：" + ex.Message);
            }
            var defaults = new ReadHelperSettings();
            defaults.Normalize();
            return defaults;
        }

        internal void Save(string path, IModuleLogger logger)
        {
            try
            {
                Normalize();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (FileStream stream = File.Create(path))
                {
                    Serializer.Value.Serialize(stream, this);
                }
            }
            catch (Exception ex)
            {
                logger?.Warning("ReadHelper 配置保存失败：" + ex.Message);
            }
        }

        private static float Finite(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            return Mathf.Clamp(value, min, max);
        }

        private static void NormalizeColor(ref float[] value, float r, float g, float b, float a)
        {
            if (value == null || value.Length != 4) value = new[] { r, g, b, a };
            for (int i = 0; i < 4; i++) value[i] = Finite(value[i], 0f, 1f, i == 3 ? a : 1f);
        }
    }
}
