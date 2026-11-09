using System;
using System.IO;
using System.Reflection;
using CheryTools.Sonnet.Contracts;
using ImGuiNET;
using UnityEngine;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;
using CheryTools.Sonnet.Shared;

namespace CheryTools.Sonnet.Framework
{
    internal static class SonnetPanelBackground
    {
        private static string _cachedPath;
        private static Texture2D _source;
        private static RenderTexture _blurA;
        private static RenderTexture _blurB;
        private static Material _blurMaterial;
        private static float _cachedBlurStrength = float.NaN;
        private static float _nextBuildTime;
        private static Func<Texture2D, byte[], bool> _loadImage;
        private static bool _loadImageResolved;
        // Status kept in both languages plus an untranslated detail (path or error), so it follows a
        // language switch without reloading the image.
        private static string _statusZh = "未加载背景图片";
        private static string _statusEn = "No background image loaded";
        private static string _statusDetail = string.Empty;

        // Panel rect and image UVs of the current frame; cards map sub-rects of the same image through
        // them so their frosted copy lines up exactly with the sharp image behind.
        private static NumVector2 _panelMin;
        private static NumVector2 _panelMax;
        private static NumVector2 _panelUv0;
        private static NumVector2 _panelUv1;
        private static Texture2D _vignette;
        private const int VignetteSize = 128;
        private const float VignetteCenter = 0.25f;

        internal static string Status => SonnetText.L(_statusZh, _statusEn) + _statusDetail;

        private static void SetStatus(string zh, string en, string detail = "")
        {
            _statusZh = zh;
            _statusEn = en;
            _statusDetail = detail ?? string.Empty;
        }

        internal static void Draw(ImDrawListPtr drawList, NumVector2 min, NumVector2 max, float rounding, FrameworkSettings settings)
        {
            _panelMin = min;
            _panelMax = max;
            if (settings == null || !settings.EnableBackgroundImage) return;
            EnsureLoaded(settings);
            Texture texture = settings.BlurBackgroundImage && _blurB != null ? (Texture)_blurB : _source;
            if (texture == null) return;

            // Cover UVs from the source size; the blur targets keep its aspect, so they share them.
            SonnetWindowChrome.CalculateCoverUv(_source.width, _source.height, max.X - min.X, max.Y - min.Y, out NumVector2 uv0, out NumVector2 uv1);
            float originalY0 = uv0.Y;
            uv0.Y = uv1.Y;
            uv1.Y = originalY0;
            _panelUv0 = uv0;
            _panelUv1 = uv1;
            float opacity = Mathf.Clamp01(settings.BackgroundImageOpacity);
            IntPtr textureId = SonnetTextureRegistry.Register(texture);
            drawList.AddImageRounded(textureId, min, max, uv0, uv1,
                ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, opacity)), rounding, ImDrawFlags.RoundCornersAll);
        }

        // Whole-panel dimming layer, flat or vignette (MaskStyle 1).
        internal static void DrawMask(ImDrawListPtr drawList, NumVector2 min, NumVector2 max, float rounding, FrameworkSettings settings)
        {
            float opacity = MaskOpacity(settings);
            if (opacity > 0f) DrawMaskRegion(drawList, min, max, rounding, settings, opacity);
        }

        // Frosted backdrop for a card inside the panel: the blurred image and the panel mask for that
        // rect, drawn into the card's own draw list before its fill. Without frosting the panel layers
        // underneath already show through, so nothing is drawn.
        internal static void DrawCardBackdrop(ImDrawListPtr drawList, NumVector2 min, NumVector2 max, float rounding, FrameworkSettings settings)
        {
            if (settings == null || !settings.FrostedCards || !settings.EnableBackgroundImage || _blurB == null) return;
            NumVector2 panelSize = _panelMax - _panelMin;
            if (panelSize.X <= 0f || panelSize.Y <= 0f) return;
            NumVector2 uv0 = Lerp(_panelUv0, _panelUv1, (min - _panelMin) / panelSize);
            NumVector2 uv1 = Lerp(_panelUv0, _panelUv1, (max - _panelMin) / panelSize);
            float opacity = Mathf.Clamp01(settings.BackgroundImageOpacity);
            drawList.AddImageRounded(SonnetTextureRegistry.Register(_blurB), min, max, uv0, uv1,
                ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, opacity)), rounding, ImDrawFlags.RoundCornersAll);
            float mask = MaskOpacity(settings);
            if (mask > 0f) DrawMaskRegion(drawList, min, max, rounding, settings, mask);
        }

        private static float MaskOpacity(FrameworkSettings settings)
        {
            return Mathf.Clamp(settings?.BackgroundOpacity ?? SonnetTheme.DefaultMaskOpacity, 0f, SonnetTheme.MaxLayerOpacity);
        }

        private static void DrawMaskRegion(ImDrawListPtr drawList, NumVector2 min, NumVector2 max, float rounding,
            FrameworkSettings settings, float opacity)
        {
            NumVector4 tint = SonnetTheme.Tint;
            uint color = ImGui.GetColorU32(new NumVector4(tint.X, tint.Y, tint.Z, opacity));
            NumVector2 panelSize = _panelMax - _panelMin;
            if (settings?.MaskStyle == 1 && panelSize.X > 0f && panelSize.Y > 0f)
            {
                // The vignette spans the whole panel; a card samples its own part of it.
                drawList.AddImageRounded(SonnetTextureRegistry.Register(Vignette()), min, max,
                    (min - _panelMin) / panelSize, (max - _panelMin) / panelSize, color, rounding, ImDrawFlags.RoundCornersAll);
            }
            else
            {
                drawList.AddRectFilled(min, max, color, rounding);
            }
        }

        // White texture whose alpha rises from VignetteCenter in the middle to 1 in the corners
        // (elliptical, so it follows the panel's aspect). Tinted and scaled by the mask color.
        private static Texture2D Vignette()
        {
            if (_vignette != null) return _vignette;
            var pixels = new Color32[VignetteSize * VignetteSize];
            for (int y = 0; y < VignetteSize; y++)
            {
                float v = (y + 0.5f) / VignetteSize * 2f - 1f;
                for (int x = 0; x < VignetteSize; x++)
                {
                    float u = (x + 0.5f) / VignetteSize * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v) / Mathf.Sqrt(2f);
                    float t = Mathf.Clamp01((d - 0.15f) / 0.85f);
                    t = t * t * (3f - 2f * t);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(VignetteCenter, 1f, t) * 255f);
                    pixels[y * VignetteSize + x] = new Color32(255, 255, 255, alpha);
                }
            }
            _vignette = new Texture2D(VignetteSize, VignetteSize, TextureFormat.RGBA32, false)
            {
                name = "Sonnet_PanelVignette",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            _vignette.SetPixels32(pixels);
            _vignette.Apply(false, false);
            return _vignette;
        }

        private static NumVector2 Lerp(NumVector2 a, NumVector2 b, NumVector2 t) => a + (b - a) * t;

        internal static void Reload()
        {
            _cachedPath = null;
            _cachedBlurStrength = float.NaN;
            _nextBuildTime = 0f;
        }

        internal static bool TryGetModuleBackground(FrameworkSettings settings, out ModuleBackgroundInfo background)
        {
            background = default(ModuleBackgroundInfo);
            if (settings == null || !settings.EnableBackgroundImage) return false;
            EnsureLoaded(settings);
            Texture texture = settings.BlurBackgroundImage && _blurB != null ? (Texture)_blurB : _source;
            if (texture == null) return false;
            background = new ModuleBackgroundInfo(
                SonnetTextureRegistry.Register(texture), texture.width, texture.height,
                Mathf.Clamp01(settings.BackgroundImageOpacity),
                Mathf.Clamp(settings.BackgroundOpacity, 0f, SonnetTheme.MaxLayerOpacity), true);
            return true;
        }

        private static void EnsureLoaded(FrameworkSettings settings)
        {
            string path = ResolvePath(settings.BackgroundImagePath);
            if (!string.Equals(path, _cachedPath, StringComparison.OrdinalIgnoreCase))
                LoadSource(path);
            if (_source == null || !(settings.BlurBackgroundImage || settings.FrostedCards)) return;

            float strength = Mathf.Clamp(settings.BackgroundImageBlurStrength, 1f, 20f);
            if (_blurB != null && Mathf.Abs(_cachedBlurStrength - strength) < 0.01f) return;
            if (Time.realtimeSinceStartup < _nextBuildTime) return;
            _nextBuildTime = Time.realtimeSinceStartup + 0.12f;
            BuildBlur(strength);
        }

        private static string ResolvePath(string configuredPath)
        {
            string path = (configuredPath ?? string.Empty).Trim().Trim('"');
            if (string.IsNullOrEmpty(path)) return string.Empty;
            path = Environment.ExpandEnvironmentVariables(path);
            if (!Path.IsPathRooted(path)) path = Path.Combine(SonnetMain.FrameworkDirectory ?? string.Empty, path);
            try { return Path.GetFullPath(path); }
            catch { return path; }
        }

        private static void LoadSource(string path)
        {
            ReleaseImageTargets();
            _cachedPath = path;
            _cachedBlurStrength = float.NaN;
            if (string.IsNullOrWhiteSpace(path))
            {
                SetStatus("未设置背景图片路径", "No background image path set");
                return;
            }
            if (!File.Exists(path))
            {
                SetStatus("找不到背景图片：", "Background image not found: ", path);
                return;
            }

            try
            {
                Func<Texture2D, byte[], bool> loader = ResolveImageLoader();
                if (loader == null)
                {
                    SetStatus("当前 Unity 环境不支持图片解码", "This Unity build cannot decode images");
                    return;
                }
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "Sonnet_PanelBackground",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (!loader(texture, File.ReadAllBytes(path)))
                {
                    UnityEngine.Object.Destroy(texture);
                    SetStatus("无法解码背景图片", "Could not decode the background image");
                    return;
                }
                _source = texture;
                SonnetTextureRegistry.Register(_source);
                SetStatus("已加载：", "Loaded: ", Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                SetStatus("背景图片加载失败：", "Failed to load background image: ", ex.GetBaseException().Message);
            }
        }

        private static Func<Texture2D, byte[], bool> ResolveImageLoader()
        {
            if (_loadImageResolved) return _loadImage;
            _loadImageResolved = true;
            Type type = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", false);
            if (type == null) return null;
            var method = type.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
            if (method != null)
            {
                try
                {
                    _loadImage = (Func<Texture2D, byte[], bool>)Delegate.CreateDelegate(typeof(Func<Texture2D, byte[], bool>), method);
                    return _loadImage;
                }
                catch { }
            }
            var methodWithMark = type.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
            if (methodWithMark == null) return null;
            try
            {
                var full = (Func<Texture2D, byte[], bool, bool>)Delegate.CreateDelegate(
                    typeof(Func<Texture2D, byte[], bool, bool>), methodWithMark);
                _loadImage = (texture, data) => full(texture, data, false);
            }
            catch { }
            return _loadImage;
        }

        private static void BuildBlur(float strength)
        {
            ReleaseBlurTargets();
            EnsureBlurMaterial();
            if (_blurMaterial == null)
            {
                SetStatus("未找到可用的背景模糊 Shader，正在显示原图", "No usable blur shader found; showing the original image");
                return;
            }

            float scale = Mathf.Min(1f, Mathf.Min(1920f / _source.width, 1080f / _source.height));
            int width = Mathf.Max(64, Mathf.RoundToInt(_source.width * scale * 0.65f));
            int height = Mathf.Max(64, Mathf.RoundToInt(_source.height * scale * 0.65f));
            _blurA = CreateTarget("Sonnet_PanelBlurA", width, height);
            _blurB = CreateTarget("Sonnet_PanelBlurB", width, height);

            _blurMaterial.SetTexture("_BaseTex", _source);
            _blurMaterial.SetTexture("_TileTex", Texture2D.whiteTexture);
            _blurMaterial.SetColor("_BaseTint", Color.white);
            _blurMaterial.SetColor("_BlurTint", Color.white);
            _blurMaterial.SetFloat("_Tinting", 0f);
            _blurMaterial.SetFloat("_BlurSize", 0.9f + strength * 0.30f);
            int iterations = Mathf.Clamp(Mathf.CeilToInt(strength / 5f), 1, 4);
            Texture input = _source;
            for (int i = 0; i < iterations; i++)
            {
                _blurMaterial.SetFloat("_Pass", i);
                Graphics.Blit(input, _blurA, _blurMaterial, 0);
                Graphics.Blit(_blurA, _blurB, _blurMaterial, 1);
                input = _blurB;
            }
            SonnetTextureRegistry.Register(_blurB);
            _cachedBlurStrength = strength;
            SetStatus("已加载并模糊：", "Loaded and blurred: ", Path.GetFileName(_cachedPath));
        }

        private static void EnsureBlurMaterial()
        {
            if (_blurMaterial != null) return;
            Shader shader = ResolveOfficialBlurShader();
            if (shader != null && shader.isSupported)
                _blurMaterial = new Material(shader) { name = "Sonnet_PanelBackgroundBlur", hideFlags = HideFlags.HideAndDontSave };
        }

        private static Shader ResolveOfficialBlurShader()
        {
            try
            {
                Type adoBase = Type.GetType("ADOBase, Assembly-CSharp", false);
                if (adoBase == null) return null;
                BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                object constants = adoBase.GetField("gc", staticFlags)?.GetValue(null)
                    ?? adoBase.GetProperty("gc", staticFlags)?.GetValue(null, null);
                if (constants == null) return null;
                Type constantsType = constants.GetType();
                BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                return constantsType.GetField("tileBlurShader", instanceFlags)?.GetValue(constants) as Shader
                    ?? constantsType.GetProperty("tileBlurShader", instanceFlags)?.GetValue(constants, null) as Shader;
            }
            catch
            {
                return null;
            }
        }

        private static RenderTexture CreateTarget(string name, int width, int height)
        {
            var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.Create();
            return texture;
        }

        internal static void Shutdown()
        {
            ReleaseImageTargets();
            if (_blurMaterial != null) UnityEngine.Object.Destroy(_blurMaterial);
            _blurMaterial = null;
            if (_vignette != null)
            {
                SonnetTextureRegistry.Unregister(_vignette);
                UnityEngine.Object.Destroy(_vignette);
                _vignette = null;
            }
            _cachedPath = null;
            _cachedBlurStrength = float.NaN;
        }

        private static void ReleaseImageTargets()
        {
            ReleaseBlurTargets();
            if (_source != null)
            {
                SonnetTextureRegistry.Unregister(_source);
                UnityEngine.Object.Destroy(_source);
                _source = null;
            }
        }

        private static void ReleaseBlurTargets()
        {
            Release(ref _blurA);
            Release(ref _blurB);
        }

        private static void Release(ref RenderTexture texture)
        {
            if (texture == null) return;
            SonnetTextureRegistry.Unregister(texture);
            texture.Release();
            UnityEngine.Object.Destroy(texture);
            texture = null;
        }
    }
}
