using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using CheryTools.Sonnet.Contracts;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.Overlayer.Preview
{
    [Serializable]
    public abstract class OvComponent
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Name = L("新组件", "New component");
        public bool Enabled = true;
        public bool ShowInGame = true;
        public bool OnlyShowPlaying;
        public float PositionX = 50f;
        public float PositionY = 50f;
        public float Opacity = 1f;
        public int Depth;
        public float PivotX;
        public float PivotY;
    }

    [Serializable]
    public sealed class OvTextComponent : OvComponent
    {
        public string TextFormat = "FPS {fps}";
        public string TokenSourceSnapshot = string.Empty;
        public List<OvTextTokenBinding> TokenBindings = new List<OvTextTokenBinding>();
        public OvAnimationGraph TokenAnimation = OvAnimationGraph.CreateDefault();
        public float FontSize = 32f;
        public string FontPath = string.Empty;
        public int Alignment;
        public float[] TextColor = { 1f, 1f, 1f, 1f };
        public bool OutlineEnabled;
        public float[] OutlineColor = { 0f, 0f, 0f, 1f };
        public float OutlineThickness = 1f;
        public bool ShadowEnabled;
        public float[] ShadowColor = { 0f, 0f, 0f, 0.7f };
        public float[] ShadowOffset = { 2f, 2f };
        public float ShadowSoftness;
        public float LetterSpacing;
        public float LineHeightOffset;
    }

    [Serializable]
    public sealed class OvImageComponent : OvComponent
    {
        public string ImagePath = string.Empty;
        public float Scale = 1f;
        public float Rotation;
        public OvAnimationGraph NodeAnimation = OvImageNodeAnimation.CreateDefault();
    }

    [Serializable]
    public sealed class OvVideoComponent : OvComponent
    {
        public string VideoPath = string.Empty;
        public bool Loop = true;
        public float Width = 320f;
        public float Height = 180f;
        public float ContentScale = 1f;
        public float ContentOffsetX;
        public float ContentOffsetY;
        public float Rotation;
    }

    public enum OvProgressFillDirection
    {
        LeftToRight,
        RightToLeft,
        BottomToTop,
        TopToBottom
    }

    [Serializable]
    public sealed class OvProgressComponent : OvComponent
    {
        public string ValueTag = "{progress}";
        public bool ValueIsConstant;
        public double ValueConstant;
        public string MinimumTag = string.Empty;
        public string MaximumTag = string.Empty;
        public double Minimum;
        public double Maximum = 100d;
        public float Width = 300f;
        public float Height = 20f;
        public OvProgressFillDirection FillDirection;
        public float[] BackgroundColor = { 0f, 0f, 0f, 0.45f };
        public float[] FillColor = { 0.2f, 0.75f, 1f, 0.95f };
        public float[] BorderColor = { 1f, 1f, 1f, 0.8f };
        public float BorderThickness = 1f;
        public float CornerRadius;
        public bool Reverse;
        public bool ClampValue = true;
        public bool EnableFillGradient;
        public float[] FillGradientStartColor = { 1f, 0.25f, 0.25f, 0.95f };
        public float[] FillGradientEndColor = { 0.25f, 1f, 0.35f, 0.95f };
        public bool EnableShadow;
        public float[] ShadowColor = { 0f, 0f, 0f, 0.45f };
        public float[] ShadowOffset = { 2f, 2f };
        public float ShadowSoftness;
    }

    [Serializable, XmlRoot("CheryToolsSonnetOverlayer")]
    public sealed class OvSettings
    {
        private static readonly Lazy<XmlSerializer> Serializer =
            new Lazy<XmlSerializer>(() => new XmlSerializer(typeof(OvSettings)));

        public bool Enabled = true;
        public bool OnlyShowPlaying;
        public bool EditMode;
        public float DataUpdateRate = 60f;
        public List<OvTextComponent> Texts = new List<OvTextComponent>();
        public List<OvImageComponent> Images = new List<OvImageComponent>();
        public List<OvVideoComponent> Videos = new List<OvVideoComponent>();
        public List<OvProgressComponent> ProgressBars = new List<OvProgressComponent>();

        internal static OvSettings Load(string path, IModuleLogger logger)
        {
            try
            {
                if (File.Exists(path))
                    using (FileStream stream = File.OpenRead(path))
                        return Serializer.Value.Deserialize(stream) as OvSettings ?? CreateDefault();
            }
            catch (Exception ex)
            {
                logger?.Warning(L("OV 配置读取失败，已使用默认配置：", "Could not read OV settings, using defaults: ") + ex.GetBaseException().Message);
                try
                {
                    if (File.Exists(path))
                    {
                        string backupPath = path + ".corrupted." + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak";
                        if (!File.Exists(backupPath))
                        {
                            File.Copy(path, backupPath, true);
                            logger?.Info(L("已保留异常配置文件备份：", "Kept a backup of the damaged settings file: ") + backupPath);
                        }
                    }
                }
                catch { }
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
            catch (Exception ex) { logger?.Warning(L("OV 配置保存失败：", "Could not save OV settings: ") + ex.GetBaseException().Message); }
        }

        internal void Normalize()
        {
            if (Texts == null) Texts = new List<OvTextComponent>();
            if (Images == null) Images = new List<OvImageComponent>();
            if (Videos == null) Videos = new List<OvVideoComponent>();
            if (ProgressBars == null) ProgressBars = new List<OvProgressComponent>();
            DataUpdateRate = Math.Max(15f, Math.Min(360f, DataUpdateRate));
            NormalizeList(Texts, L("文本", "Text"));
            NormalizeList(Images, L("图片", "Image"));
            NormalizeList(Videos, L("视频", "Video"));
            NormalizeList(ProgressBars, L("进度条", "Progress bar"));
        }

        private static void NormalizeList<T>(List<T> values, string prefix) where T : OvComponent
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = values.Count - 1; i >= 0; i--)
            {
                T value = values[i];
                if (value == null) { values.RemoveAt(i); continue; }
                if (string.IsNullOrWhiteSpace(value.Id) || !ids.Add(value.Id))
                {
                    value.Id = Guid.NewGuid().ToString("N");
                    ids.Add(value.Id);
                }
                if (string.IsNullOrWhiteSpace(value.Name)) value.Name = prefix + " " + (i + 1);
                value.Opacity = Math.Max(0f, Math.Min(1f, value.Opacity));
                value.PivotX = Math.Max(0f, Math.Min(1f, value.PivotX));
                value.PivotY = Math.Max(0f, Math.Min(1f, value.PivotY));
                if (value is OvTextComponent text)
                {
                    if (text.TextFormat == null) text.TextFormat = "FPS {fps}";
                    if (float.IsNaN(text.FontSize) || float.IsInfinity(text.FontSize) || text.FontSize < 8f || text.FontSize > 240f)
                        text.FontSize = 32f;
                    if (text.FontPath == null) text.FontPath = string.Empty;
                    text.TextColor = Color(text.TextColor, 1f, 1f, 1f, 1f);
                    text.OutlineColor = Color(text.OutlineColor, 0f, 0f, 0f, 1f);
                    text.ShadowColor = Color(text.ShadowColor, 0f, 0f, 0f, 0.7f);
                    text.ShadowOffset = Pair(text.ShadowOffset, 2f, 2f);
                    if (text.TokenBindings == null) text.TokenBindings = new List<OvTextTokenBinding>();
                    if (text.TokenSourceSnapshot == null) text.TokenSourceSnapshot = string.Empty;
                    text.TextFormat = OvTagResolver.MigrateLegacyTags(text.TextFormat);
                    text.TokenSourceSnapshot = OvTagResolver.MigrateLegacyTags(text.TokenSourceSnapshot);
                    foreach (OvTextTokenBinding binding in text.TokenBindings)
                        if (binding != null) binding.Lexeme = OvTagResolver.MigrateLegacyTags(binding.Lexeme);
                    if (text.TokenAnimation == null) text.TokenAnimation = OvAnimationGraph.CreateDefault();
                    text.TokenAnimation.Normalize();
                }
                else if (value is OvVideoComponent video)
                {
                    video.Width = Math.Max(1f, video.Width);
                    video.Height = Math.Max(1f, video.Height);
                    video.ContentScale = Math.Max(0.01f, video.ContentScale);
                }
                else if (value is OvImageComponent image)
                {
                    image.Scale = Math.Max(0.01f, image.Scale);
                    if (image.NodeAnimation == null) image.NodeAnimation = OvImageNodeAnimation.CreateDefault();
                    OvImageNodeAnimation.EnsureImageTarget(image.NodeAnimation);
                }
                else if (value is OvProgressComponent progress)
                {
                    progress.Width = Math.Max(1f, progress.Width);
                    progress.Height = Math.Max(1f, progress.Height);
                    progress.BackgroundColor = Color(progress.BackgroundColor, 0f, 0f, 0f, 0.45f);
                    progress.FillColor = Color(progress.FillColor, 0.2f, 0.75f, 1f, 0.95f);
                    progress.BorderColor = Color(progress.BorderColor, 1f, 1f, 1f, 0.8f);
                    progress.FillGradientStartColor = Color(progress.FillGradientStartColor, 1f, 0.25f, 0.25f, 0.95f);
                    progress.FillGradientEndColor = Color(progress.FillGradientEndColor, 0.25f, 1f, 0.35f, 0.95f);
                    progress.ShadowColor = Color(progress.ShadowColor, 0f, 0f, 0f, 0.45f);
                    progress.ShadowOffset = Pair(progress.ShadowOffset, 2f, 2f);
                    if (progress.ValueTag == null) progress.ValueTag = string.Empty;
                    if (progress.MinimumTag == null) progress.MinimumTag = string.Empty;
                    if (progress.MaximumTag == null) progress.MaximumTag = string.Empty;
                    progress.ValueTag = OvTagResolver.MigrateLegacyTags(progress.ValueTag);
                    progress.MinimumTag = OvTagResolver.MigrateLegacyTags(progress.MinimumTag);
                    progress.MaximumTag = OvTagResolver.MigrateLegacyTags(progress.MaximumTag);
                    if (double.IsNaN(progress.ValueConstant) || double.IsInfinity(progress.ValueConstant)) progress.ValueConstant = 0d;
                    if (double.IsNaN(progress.Minimum) || double.IsInfinity(progress.Minimum)) progress.Minimum = 0d;
                    if (double.IsNaN(progress.Maximum) || double.IsInfinity(progress.Maximum)) progress.Maximum = 100d;
                    if (float.IsNaN(progress.ShadowSoftness) || float.IsInfinity(progress.ShadowSoftness) || progress.ShadowSoftness < 0f)
                        progress.ShadowSoftness = 0f;
                }
            }
        }

        private static float[] Color(float[] value, float r, float g, float b, float a)
        {
            return value != null && value.Length >= 4 ? value : new[] { r, g, b, a };
        }

        private static float[] Pair(float[] value, float x, float y)
        {
            return value != null && value.Length >= 2 ? value : new[] { x, y };
        }

        private static OvSettings CreateDefault()
        {
            var settings = new OvSettings();
            settings.Texts.Add(new OvTextComponent { Name = "FPS", TextFormat = "FPS {fps}" });
            settings.Normalize();
            return settings;
        }
    }
}
