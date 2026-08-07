using System;
using UnityEngine;
using UnityEngine.UI;

namespace CheryTools
{
    /// <summary>
    /// Renders a shared, separable-Gaussian copy of the game behind the control
    /// panel surfaces. The blur is produced entirely in Unity's render layer:
    /// one camera capture is reused by every panel and no fullscreen dim quad is
    /// created.
    /// </summary>
    internal static class ImGuiPanelBackdrop
    {
        // Above the game, below all CT KV/OV preview layers and the ImGui panel.
        private const int CanvasSortingOrder = RenderDepth.LayerBaseSortingOrder - 1;
        private const float CaptureInterval = 1f / 60f;
        private const float DefaultTransitionDuration = 0.5f;

        private static GameObject _root;
        private static Canvas _canvas;
        private static RawImage _fullscreen;

        private static RenderTexture _blurA;
        private static RenderTexture _blurB;
        private static RenderTexture _blurred;
        private static Material _blurMaterial;
        private static int _sourceWidth;
        private static int _sourceHeight;
        private static float _nextCaptureTime;

        private static Camera _captureCamera;
        private static ImGuiPanelBackdropCapture _captureHook;
        private static bool _active;
        private static int _blurStrength = 10;
        private static bool _targetVisible;
        private static float _visibility;
        private static float _transitionFrom;
        private static float _transitionTo;
        private static float _transitionStartedAt;
        private static float _transitionDuration;
        private static string _transitionEasing = "smootherstep";

        internal static float PanelVisibility => Mathf.Clamp01(_visibility);
        internal static bool ShouldRenderPanel => _targetVisible || _visibility > 0.001f;

        internal static void Initialize()
        {
            if (_root != null)
                return;

            _root = new GameObject("CheryTools_ImGui_BlurredBackdrop");
            UnityEngine.Object.DontDestroyOnLoad(_root);

            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = CanvasSortingOrder;

            CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            _fullscreen = CreateFullscreenSurface();
            _root.SetActive(false);
        }

        internal static void SetActive(bool active)
        {
            Initialize();
            bool blurEnabled = Main.Settings == null || Main.Settings.ImGuiPanelBlurEnabled;
            int requestedStrength = Main.Settings != null ? Main.Settings.ImGuiPanelBlurStrength : 10;
            requestedStrength = Mathf.Clamp(requestedStrength, 1, 20);
            if (_blurStrength != requestedStrength)
                _blurStrength = requestedStrength;

            float now = Time.realtimeSinceStartup;
            UpdateTransition(now);

            // The menu transition remains available even when background blur is
            // disabled, because the same timeline also drives the ImGui panel fade.
            bool requestedVisible = active;
            if (requestedVisible != _targetVisible)
            {
                _targetVisible = requestedVisible;
                _transitionFrom = _visibility;
                _transitionTo = requestedVisible ? 1f : 0f;
                _transitionStartedAt = now;
                float configuredDuration = Main.Settings != null
                    ? Main.Settings.ImGuiPanelBlurTransitionDuration
                    : DefaultTransitionDuration;
                if (float.IsNaN(configuredDuration) || float.IsInfinity(configuredDuration))
                    configuredDuration = DefaultTransitionDuration;
                configuredDuration = Mathf.Clamp(configuredDuration, 0f, 2f);
                _transitionEasing = Main.Settings != null && !string.IsNullOrWhiteSpace(Main.Settings.ImGuiPanelBlurTransitionEasing)
                    ? Main.Settings.ImGuiPanelBlurTransitionEasing
                    : "smootherstep";
                _transitionDuration = configuredDuration * Mathf.Abs(_transitionTo - _transitionFrom);
                if (_transitionDuration <= 0.0001f)
                    _visibility = _transitionTo;
            }

            UpdateTransition(now);
            _active = blurEnabled && (_targetVisible || _visibility > 0.001f);

            if (_root != null && _root.activeSelf != _active)
                _root.SetActive(_active);

            if (_fullscreen != null)
                _fullscreen.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_visibility));

            EnsureCaptureCamera(_active);
            if (_captureHook != null)
                _captureHook.enabled = _active;
        }

        internal static void Capture(RenderTexture source)
        {
            if (!_active || source == null || source.width <= 0 || source.height <= 0)
                return;

            float now = Time.realtimeSinceStartup;
            if (now < _nextCaptureTime)
                return;
            _nextCaptureTime = now + CaptureInterval;

            EnsureTargets(source.width, source.height);
            if (_blurA == null || _blurB == null || _blurred == null)
                return;

            EnsureBlurMaterial();
            if (_blurMaterial == null)
            {
                // Keep the panel usable if the game's official blur shader is not
                // ready yet (for example during very early scene initialization).
                Graphics.Blit(source, _blurA);
                Graphics.Blit(_blurA, _blurred);
                return;
            }

            float normalizedStrength = (_blurStrength - 1f) / 19f;
            float targetBlurSize = Mathf.Lerp(0.6f, 4.5f, normalizedStrength);
            float effectProgress = Mathf.Clamp01(_visibility);
            float blurSize = Mathf.Lerp(0.1f, targetBlurSize, effectProgress);
            int targetIterations = 1 + Mathf.RoundToInt(normalizedStrength * 3f);
            int iterations = Math.Max(1, Mathf.CeilToInt(targetIterations * effectProgress));

            // Match ADOFAI's scrBlur pipeline. Pass 0 and pass 1 form one
            // horizontal/vertical blur pair; _Pass expands the radius smoothly
            // on subsequent pairs. The final copy deliberately skips scrBlur's
            // tint-composite pass because ImGui panels apply their own tint.
            _blurMaterial.SetTexture("_BaseTex", source);
            _blurMaterial.SetTexture("_TileTex", Texture2D.whiteTexture);
            _blurMaterial.SetColor("_BaseTint", Color.white);
            _blurMaterial.SetColor("_BlurTint", Color.white);
            _blurMaterial.SetFloat("_Tinting", 0f);
            _blurMaterial.SetFloat("_BlurSize", blurSize);

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                _blurMaterial.SetFloat("_Pass", iteration);
                Graphics.Blit(iteration == 0 ? source : _blurB, _blurA, _blurMaterial, 0);
                Graphics.Blit(_blurA, _blurB, _blurMaterial, 1);
            }
            Graphics.Blit(_blurB, _blurred);
        }

        internal static void Shutdown()
        {
            ReleaseTargets();

            if (_blurMaterial != null)
                UnityEngine.Object.Destroy(_blurMaterial);
            _blurMaterial = null;

            if (_captureHook != null)
                UnityEngine.Object.Destroy(_captureHook);
            _captureHook = null;
            _captureCamera = null;

            if (_root != null)
                UnityEngine.Object.Destroy(_root);
            _root = null;
            _canvas = null;
            _fullscreen = null;
            _active = false;
            _targetVisible = false;
            _visibility = 0f;
            _transitionFrom = 0f;
            _transitionTo = 0f;
            _transitionStartedAt = 0f;
            _transitionDuration = 0f;
            _transitionEasing = "smootherstep";
        }

        private static void UpdateTransition(float now)
        {
            if (_transitionDuration <= 0.0001f)
            {
                _visibility = _transitionTo;
                return;
            }

            float time = Mathf.Clamp01((now - _transitionStartedAt) / _transitionDuration);
            float easedTime = Mathf.Clamp01(EasingUtil.EvaluateEasing(time, _transitionEasing));
            _visibility = Mathf.Lerp(_transitionFrom, _transitionTo, easedTime);
            if (time >= 1f)
            {
                _visibility = _transitionTo;
                _transitionDuration = 0f;
            }
        }

        private static RawImage CreateFullscreenSurface()
        {
            GameObject child = new GameObject("FullscreenBlur", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            child.transform.SetParent(_root.transform, false);
            RawImage image = child.GetComponent<RawImage>();
            image.raycastTarget = false;
            // This is the blurred game frame itself, not a fullscreen black mask.
            image.color = Color.white;
            image.uvRect = new Rect(0f, 0f, 1f, 1f);

            RectTransform rectTransform = image.rectTransform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            return image;
        }

        private static void EnsureCaptureCamera(bool active)
        {
            if (!active)
                return;

            Camera candidate = FindCaptureCamera();
            if (candidate == _captureCamera && _captureHook != null)
                return;

            if (_captureHook != null)
                UnityEngine.Object.Destroy(_captureHook);

            _captureCamera = candidate;
            _captureHook = null;
            if (_captureCamera != null)
            {
                _captureHook = _captureCamera.gameObject.AddComponent<ImGuiPanelBackdropCapture>();
                _captureHook.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private static Camera FindCaptureCamera()
        {
            Camera main = Camera.main;
            if (IsUsableCamera(main))
                return main;

            Camera[] cameras = Camera.allCameras;
            Camera best = null;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (!IsUsableCamera(camera))
                    continue;
                if (best == null || camera.depth > best.depth)
                    best = camera;
            }
            return best;
        }

        private static bool IsUsableCamera(Camera camera)
        {
            return camera != null
                && camera.enabled
                && camera.gameObject.activeInHierarchy
                && camera.targetTexture == null;
        }

        private static void EnsureTargets(int width, int height)
        {
            if (_blurred != null && _sourceWidth == width && _sourceHeight == height)
                return;

            ReleaseTargets();
            _sourceWidth = width;
            _sourceHeight = height;

            // Half resolution is enough for a translucent panel backdrop while
            // retaining smooth gradients and avoiding the old pixelated result.
            int blurWidth = Math.Max(128, width / 2);
            int blurHeight = Math.Max(72, height / 2);

            _blurA = CreateTarget("CheryTools_GaussianBlurA", blurWidth, blurHeight);
            _blurB = CreateTarget("CheryTools_GaussianBlurB", blurWidth, blurHeight);
            _blurred = CreateTarget("CheryTools_GaussianBlurResult", blurWidth, blurHeight);

            ApplyOutputTexture();
        }

        private static void ApplyOutputTexture()
        {
            if (_fullscreen != null)
                _fullscreen.texture = _blurred;
        }

        private static RenderTexture CreateTarget(string name, int width, int height)
        {
            RenderTexture texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            texture.name = name;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.useMipMap = false;
            texture.autoGenerateMips = false;
            texture.antiAliasing = 1;
            texture.Create();
            return texture;
        }

        private static void ReleaseTargets()
        {
            ReleaseTarget(ref _blurA);
            ReleaseTarget(ref _blurB);
            ReleaseTarget(ref _blurred);
            _sourceWidth = 0;
            _sourceHeight = 0;
        }

        private static void EnsureBlurMaterial()
        {
            if (_blurMaterial != null)
                return;

            RDConstants constants = ADOBase.gc;
            Shader shader = constants != null ? constants.tileBlurShader : null;
            if (shader == null || !shader.isSupported)
                return;

            _blurMaterial = new Material(shader);
            _blurMaterial.name = "CheryTools_ImGui_OfficialTileBlur";
            _blurMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        private static void ReleaseTarget(ref RenderTexture texture)
        {
            if (texture == null)
                return;
            texture.Release();
            UnityEngine.Object.Destroy(texture);
            texture = null;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class ImGuiPanelBackdropCapture : MonoBehaviour
    {
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            ImGuiPanelBackdrop.Capture(source);
            Graphics.Blit(source, destination);
        }
    }
}
