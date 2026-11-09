using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ADOFAI.EditorTweaks.ChartRendering.Features.ChartRendering
{
    // Renders the complete game view (world cameras plus HUD canvases) into an offscreen
    // render texture at the configured output size, independent of the window size.
    //
    // While active:
    //  - the scrCamera chain and every enabled camera that would draw to the screen are
    //    redirected into the capture texture;
    //  - Screen Space - Overlay canvases are switched to Screen Space - Camera on a dedicated
    //    UI camera that also targets the capture texture, with their scale factor multiplied
    //    by (output size / screen size) so the layout matches what the screen shows;
    //  - the screen shows a letterboxed preview of the capture texture, and the CheryTools
    //    ImGui panel stays on screen without being recorded.
    // Everything is restored on Dispose.
    internal sealed class ChartOffscreenGameViewCapture : IChartFrameCapture
    {
        private const string ImGuiCanvasName = "CheryTools_Sonnet_ImGui_Canvas";
        private const string InputBlockerCanvasName = "CheryTools_Sonnet_GameInputBlocker";
        private const float UiPlaneDistance = 100f;

        private readonly int rowBytes;
        private readonly TextureFormat readbackFormat;
        private readonly RenderTexture captureTarget;
        private readonly float uiScale;
        private readonly GameObject rootObject;
        private readonly Camera cameraCanvasUiCamera;
        private readonly Camera overlayUiCamera;
        private readonly Canvas previewCanvas;
        private readonly Dictionary<Camera, RenderTexture?> redirectedCameras = new Dictionary<Camera, RenderTexture?>();
        private readonly Dictionary<Canvas, CanvasState> trackedCanvases = new Dictionary<Canvas, CanvasState>();
        private readonly HashSet<int> ignoredCanvasIds = new HashSet<int>();
        private Camera[] cameraBuffer = new Camera[16];
        private bool disposed;

        public ChartOffscreenGameViewCapture(int width, int height, string captureFormat, string previewMode)
        {
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;
            if (width <= 0 || height <= 0 || screenWidth <= 0 || screenHeight <= 0)
            {
                throw new InvalidOperationException(Settings.Text("chartRendererInvalidGameViewSize"));
            }

            Width = width;
            Height = height;
            rowBytes = width * 4;
            readbackFormat = ChartFrameReadback.ResolveFormat(captureFormat);
            PixelFormatName = ChartFrameReadback.GetPixelFormatName(readbackFormat);
            uiScale = Mathf.Min((float)width / screenWidth, (float)height / screenHeight);

            captureTarget = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "ADOFAI.EditorTweaks.ChartRendering.ChartRender.OffscreenGameView",
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!captureTarget.Create())
            {
                UnityEngine.Object.Destroy(captureTarget);
                throw new InvalidOperationException("Failed to create offscreen game view render target " + width + "x" + height + ".");
            }

            rootObject = new GameObject("AetRecorder_OffscreenGameView");
            UnityEngine.Object.DontDestroyOnLoad(rootObject);
            try
            {
                cameraCanvasUiCamera = CreateUiCamera("AetRecorder_CameraCanvasUiCamera", 0f);
                overlayUiCamera = CreateUiCamera("AetRecorder_OverlayCanvasUiCamera", 1f);
                previewCanvas = CreatePreviewCanvas(previewMode);
                rootObject.AddComponent<ChartOffscreenCaptureDriver>().Tick = Refresh;
                Refresh();
            }
            catch
            {
                RestoreAll();
                UnityEngine.Object.Destroy(rootObject);
                captureTarget.Release();
                UnityEngine.Object.Destroy(captureTarget);
                throw;
            }

            ChartRenderDiagnostics.Log("Frame capture mode=game-view (offscreen). screen="
                + screenWidth + "x" + screenHeight
                + ", output=" + width + "x" + height
                + ", uiScale=" + uiScale.ToString("0.###")
                + ", readback=" + PixelFormatName
                + ", preview=" + previewMode
                + ", cameras=" + redirectedCameras.Count
                + ", canvases=" + trackedCanvases.Count + ".");
        }

        public string PixelFormatName { get; }

        public int Width { get; }

        public int Height { get; }

        public bool RequiresVerticalFlip => true;

        public ChartPendingFrame RequestFrame(int index, int repeatCount = 1)
        {
            ThrowIfDisposed();
            return new ChartPendingFrame(index, repeatCount, AsyncGPUReadback.Request(captureTarget, 0, readbackFormat), rowBytes, Height);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            RestoreAll();
            if (rootObject != null)
            {
                UnityEngine.Object.Destroy(rootObject);
            }

            captureTarget.Release();
            UnityEngine.Object.Destroy(captureTarget);
            ChartRenderDiagnostics.Log("Offscreen game view capture restored.");
        }

        // Runs every LateUpdate (before canvases are rebuilt and cameras render) so cameras and
        // canvases that appear mid-render are captured from the frame they first show up.
        private void Refresh()
        {
            if (disposed)
            {
                return;
            }

            RefreshCameras();
            RefreshCanvases();
        }

        private void RefreshCameras()
        {
            scrCamera? chain = scrCamera.instance;
            Camera? presenter = chain == null ? null : chain.Overlaycam;
            if (chain != null)
            {
                Redirect(chain.Bgcamstatic);
                Redirect(chain.BGcam);
                Redirect(chain.camobj);
            }

            int count = Camera.allCamerasCount;
            if (cameraBuffer.Length < count)
            {
                cameraBuffer = new Camera[Mathf.NextPowerOfTwo(count)];
            }

            count = Camera.GetAllCameras(cameraBuffer);
            float maxDepth = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                Camera camera = cameraBuffer[i];
                cameraBuffer[i] = null!;
                if (camera == null || camera == cameraCanvasUiCamera || camera == overlayUiCamera)
                {
                    continue;
                }

                // In RT-camera mode the game presents camRT through Overlaycam and a quad. The chain
                // is already rendered into the capture texture directly, so the presenter is skipped.
                if (camera == presenter)
                {
                    continue;
                }

                if (!redirectedCameras.ContainsKey(camera) && camera.targetTexture == null && camera.targetDisplay == 0)
                {
                    Redirect(camera);
                }

                if (redirectedCameras.ContainsKey(camera))
                {
                    if (camera.targetTexture != captureTarget)
                    {
                        camera.targetTexture = captureTarget;
                    }

                    maxDepth = Mathf.Max(maxDepth, camera.depth);
                }
            }

            if (maxDepth == float.MinValue)
            {
                maxDepth = 0f;
            }

            cameraCanvasUiCamera.depth = maxDepth + 1f;
            overlayUiCamera.depth = maxDepth + 2f;
        }

        private void Redirect(Camera? camera)
        {
            if (camera == null || redirectedCameras.ContainsKey(camera))
            {
                return;
            }

            redirectedCameras[camera] = camera.targetTexture;
            camera.targetTexture = captureTarget;
            ChartRenderDiagnostics.Log("Offscreen capture redirected camera " + camera.name
                + " (depth=" + camera.depth + ", previousTarget="
                + (redirectedCameras[camera] == null ? "screen" : redirectedCameras[camera]!.name) + ").");
        }

        private void RefreshCanvases()
        {
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || !canvas.isRootCanvas || trackedCanvases.ContainsKey(canvas))
                {
                    continue;
                }

                int id = canvas.GetInstanceID();
                if (ignoredCanvasIds.Contains(id))
                {
                    continue;
                }

                if (IsExcluded(canvas))
                {
                    ignoredCanvasIds.Add(id);
                    continue;
                }

                // Rechecked every frame rather than ignored for good: the camera may start drawing
                // to the screen later in the render.
                if (IsBoundToHiddenCamera(canvas))
                {
                    continue;
                }

                Track(canvas);
            }
        }

        // A screen-space-camera canvas whose camera never reaches the screen (disabled, or rendering
        // into its own texture) is invisible in game. Rebinding it to a capture camera would surface
        // it: the editor's workshop thumbnail camera, rendered manually and otherwise disabled, carries
        // the Neo Cosmos DLC badge this way.
        private bool IsBoundToHiddenCamera(Canvas canvas)
        {
            if (canvas.renderMode != RenderMode.ScreenSpaceCamera)
            {
                return false;
            }

            Camera? camera = canvas.worldCamera;
            if (camera == null || redirectedCameras.ContainsKey(camera))
            {
                return false;
            }

            return !camera.isActiveAndEnabled || camera.targetTexture != null;
        }

        private bool IsExcluded(Canvas canvas)
        {
            if (canvas == previewCanvas)
            {
                return true;
            }

            string name = canvas.gameObject.name;
            return name == ImGuiCanvasName || name == InputBlockerCanvasName;
        }

        private void Track(Canvas canvas)
        {
            RenderMode mode = canvas.renderMode;
            Camera? assignedCamera;
            if (mode == RenderMode.ScreenSpaceOverlay)
            {
                assignedCamera = overlayUiCamera;
            }
            else if (mode == RenderMode.ScreenSpaceCamera)
            {
                Camera? current = canvas.worldCamera;
                assignedCamera = current != null && redirectedCameras.ContainsKey(current) ? null : cameraCanvasUiCamera;
            }
            else
            {
                // World space canvases are drawn by the world cameras, which are already redirected.
                ignoredCanvasIds.Add(canvas.GetInstanceID());
                return;
            }

            CanvasScaler? scaler = canvas.GetComponent<CanvasScaler>();
            var state = new CanvasState
            {
                RenderMode = mode,
                WorldCamera = canvas.worldCamera,
                PlaneDistance = canvas.planeDistance,
                ScaleFactor = canvas.scaleFactor,
                ReferencePixelsPerUnit = canvas.referencePixelsPerUnit,
                Scaler = scaler != null && scaler.enabled ? scaler : null
            };

            // The scale factor the canvas uses on screen is captured first, then the scaler is
            // paused so the multiplied value sticks regardless of how the scaler would interpret
            // an offscreen render target.
            if (state.Scaler != null)
            {
                state.Scaler.enabled = false;
            }

            if (assignedCamera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = assignedCamera;
                canvas.planeDistance = mode == RenderMode.ScreenSpaceOverlay
                    ? UiPlaneDistance
                    : Mathf.Clamp(state.PlaneDistance, 1f, assignedCamera.farClipPlane - 1f);
                assignedCamera.cullingMask |= 1 << canvas.gameObject.layer;
            }

            canvas.scaleFactor = state.ScaleFactor * uiScale;
            canvas.referencePixelsPerUnit = state.ReferencePixelsPerUnit;
            trackedCanvases[canvas] = state;
            ChartRenderDiagnostics.Log("Offscreen capture took canvas " + canvas.gameObject.name
                + " (mode=" + mode
                + ", sortingOrder=" + canvas.sortingOrder
                + ", camera=" + (assignedCamera == null ? (canvas.worldCamera == null ? "none" : canvas.worldCamera.name) : assignedCamera.name)
                + ", scale=" + state.ScaleFactor.ToString("0.###") + "->" + canvas.scaleFactor.ToString("0.###") + ").");
        }

        private void RestoreAll()
        {
            foreach (KeyValuePair<Canvas, CanvasState> pair in trackedCanvases)
            {
                Canvas canvas = pair.Key;
                CanvasState state = pair.Value;
                if (canvas == null)
                {
                    continue;
                }

                try
                {
                    canvas.renderMode = state.RenderMode;
                    canvas.worldCamera = state.WorldCamera;
                    canvas.planeDistance = state.PlaneDistance;
                    canvas.scaleFactor = state.ScaleFactor;
                    canvas.referencePixelsPerUnit = state.ReferencePixelsPerUnit;
                    if (state.Scaler != null)
                    {
                        state.Scaler.enabled = true;
                    }
                }
                catch (Exception ex)
                {
                    ChartRenderDiagnostics.Log("Failed to restore canvas " + canvas.gameObject.name + ": " + ex.Message);
                }
            }

            trackedCanvases.Clear();

            foreach (KeyValuePair<Camera, RenderTexture?> pair in redirectedCameras)
            {
                Camera camera = pair.Key;
                if (camera != null && camera.targetTexture == captureTarget)
                {
                    camera.targetTexture = pair.Value;
                }
            }

            redirectedCameras.Clear();
        }

        private Camera CreateUiCamera(string name, float depthOffset)
        {
            var cameraObject = new GameObject(name);
            cameraObject.transform.SetParent(rootObject.transform, false);
            // Far away from gameplay so world objects that share a UI layer never enter the frustum.
            cameraObject.transform.position = new Vector3(0f, 0f, -20000f - depthOffset * 2000f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = Height * 0.5f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            camera.clearFlags = CameraClearFlags.Depth;
            camera.cullingMask = 0;
            camera.useOcclusionCulling = false;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.targetTexture = captureTarget;
            return camera;
        }

        private Canvas CreatePreviewCanvas(string previewMode)
        {
            var canvasObject = new GameObject("AetRecorder_OffscreenPreviewCanvas");
            canvasObject.transform.SetParent(rootObject.transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Below the CheryTools ImGui canvas (32767) and input blocker (32766) so the panel
            // stays usable, above everything else that is left on screen.
            canvas.sortingOrder = 32760;

            var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(canvasObject.transform, false);
            Stretch(backgroundObject.GetComponent<RectTransform>());
            Image background = backgroundObject.GetComponent<Image>();
            background.color = Color.black;
            background.raycastTarget = false;

            string mode = ChartRenderOptionValues.NormalizePreviewMode(previewMode);
            if (mode != ChartRenderOptionValues.PreviewMinimal)
            {
                var imageObject = new GameObject("Preview", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                imageObject.transform.SetParent(canvasObject.transform, false);
                Stretch(imageObject.GetComponent<RectTransform>());
                RawImage image = imageObject.GetComponent<RawImage>();
                image.texture = captureTarget;
                image.raycastTarget = false;
                image.color = mode == ChartRenderOptionValues.PreviewDim ? new Color(0.4f, 0.4f, 0.4f, 1f) : Color.white;
                AspectRatioFitter fitter = imageObject.GetComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = (float)Width / Height;
            }

            return canvas;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(ChartOffscreenGameViewCapture));
            }
        }

        private sealed class CanvasState
        {
            public RenderMode RenderMode;
            public Camera? WorldCamera;
            public float PlaneDistance;
            public float ScaleFactor;
            public float ReferencePixelsPerUnit;
            public CanvasScaler? Scaler;
        }
    }

    internal sealed class ChartOffscreenCaptureDriver : MonoBehaviour
    {
        public Action? Tick;

        private void LateUpdate()
        {
            Tick?.Invoke();
        }
    }
}
