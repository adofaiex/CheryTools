using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using ImGuiNET;

namespace CheryTools
{
    public class ImGuiController : MonoBehaviour
    {
        private Material _material;
        private Texture2D _fontTexture;
        private IntPtr _context;
        
        private Canvas _canvas;
        private List<CanvasRenderer> _renderers = new List<CanvasRenderer>();
        private List<Mesh> _meshes = new List<Mesh>();
        private bool _imguiCanvasHasContent = false;
        private float _nextOverlayUpdateTime = 0f;
        private long _lastOverlayRevision = -1;
        private int _lastOverlayStateHash = 0;

        private Vector3[] _vertices = new Vector3[0];
        private Vector2[] _uvs = new Vector2[0];
        private Color32[] _colors = new Color32[0];
        private int[] _indices = new int[0];
        private int[] _subIndices = new int[0];

        public static float PanelScale { get; private set; } = 1.0f;
        private static System.Numerics.Vector2 _screenMousePos = System.Numerics.Vector2.Zero;
        private static System.Numerics.Vector2 _screenMouseDelta = System.Numerics.Vector2.Zero;
        public static System.Numerics.Vector2 ScreenDisplaySize
        {
            get { return new System.Numerics.Vector2(Screen.width, Screen.height); }
        }
        public static System.Numerics.Vector2 ScreenMousePos
        {
            get { return _screenMousePos; }
        }
        public static System.Numerics.Vector2 ScreenMouseDelta
        {
            get { return _screenMouseDelta; }
        }

        public Action OnImGuiLayout;
        public Action OnOverlayLayout;

        public static Dictionary<string, ImFontPtr> CustomFonts = new Dictionary<string, ImFontPtr>();
        public static Dictionary<string, ImFontPtr> CustomLargeFonts = new Dictionary<string, ImFontPtr>();
        public static ImFontPtr ChineseDefaultUIFont; // 20px MiSans - used for mixed CJK UI labels
        public static ImFontPtr DefaultHighResFont;  // 48px - for text sizes ≤72px
        public static ImFontPtr DefaultLargeFont;      // 128px - for text sizes >72px
        public static bool NeedsFontAtlasRebuild = false;
        private static List<GCHandle> _pinnedHandles = new List<GCHandle>();
        private const string ChineseDefaultFontFile = "Resources/MiSans-Bold.ttf";
        private const string LatinKoreanDefaultFontFile = "Resources/Maplestory OTF Bold.otf";

        void Awake()
        {
            _context = ImGui.CreateContext();
            ImGui.SetCurrentContext(_context);

            RebuildFontAtlas();
            ClampPanelStyle();

            var shader = Shader.Find("UI/Default");
            _material = new Material(shader);
            _material.hideFlags = HideFlags.HideAndDontSave;
            _material.mainTexture = _fontTexture;

            var canvasObj = new GameObject("CheryTools_ImGui_Canvas");
            DontDestroyOnLoad(canvasObj);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32767; // Draw over everything
            
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1.0f;
        }

        private static float NormalizePanelScale(float scale)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
            {
                scale = 1.0f;
            }
            return Mathf.Clamp(scale, 0.6f, 2.0f);
        }

        private void ApplyPanelScale(float scale)
        {
            PanelScale = NormalizePanelScale(scale);
            var io = ImGui.GetIO();
            io.FontGlobalScale = 1.0f;
            ClampPanelStyle();
        }

        private static void ClampPanelStyle()
        {
            var style = ImGui.GetStyle();
            style.WindowRounding = 6.0f;
            style.ChildRounding = 5.0f;
            style.PopupRounding = 5.0f;
            style.FrameRounding = 4.0f;
            style.GrabRounding = 4.0f;
            style.ScrollbarRounding = 6.0f;
            style.TabRounding = 4.0f;
            if (style.WindowMinSize.X < 1.0f || style.WindowMinSize.Y < 1.0f)
            {
                style.WindowMinSize = new System.Numerics.Vector2(
                    Mathf.Max(1.0f, style.WindowMinSize.X),
                    Mathf.Max(1.0f, style.WindowMinSize.Y));
            }
            style.ScrollbarSize = Mathf.Max(1.0f, style.ScrollbarSize);
            style.GrabMinSize = Mathf.Max(1.0f, style.GrabMinSize);
        }

        public static ImFontPtr GetHighResFontOrDefault(string path)
        {
            if (!string.IsNullOrEmpty(path) && CustomFonts.TryGetValue(path, out var f1)) return f1;
            string resolvedPath = CheryToolsAssets.ResolveAssetPath(path);
            if (!string.IsNullOrEmpty(resolvedPath) && CustomFonts.TryGetValue(resolvedPath, out var fResolved)) return fResolved;
            if (Main.Settings != null && !string.IsNullOrEmpty(Main.Settings.KeyViewerFontPath) && CustomFonts.TryGetValue(Main.Settings.KeyViewerFontPath, out var f2)) return f2;
            string resolvedGlobalPath = Main.Settings != null ? CheryToolsAssets.ResolveAssetPath(Main.Settings.KeyViewerFontPath) : string.Empty;
            if (!string.IsNullOrEmpty(resolvedGlobalPath) && CustomFonts.TryGetValue(resolvedGlobalPath, out var f3)) return f3;
            return DefaultHighResFont;
        }

        private static void StoreFont(Dictionary<string, ImFontPtr> fonts, string key, string resolvedPath, ImFontPtr font)
        {
            if (!string.IsNullOrEmpty(key)) fonts[key] = font;
            if (!string.IsNullOrEmpty(resolvedPath)) fonts[resolvedPath] = font;
        }

        public void RebuildFontAtlas()
        {
            if (TryRebuildFontAtlas(true, true, true, "full")) return;
            if (TryRebuildFontAtlas(true, true, false, "without custom large fonts")) return;
            if (TryRebuildFontAtlas(false, true, false, "without large fonts")) return;
            TryRebuildFontAtlas(false, false, false, "default fonts only");
        }

        private bool TryRebuildFontAtlas(bool loadDefaultLargeFont, bool loadCustomFonts, bool loadCustomLargeFonts, string modeName)
        {
            try
            {
                RebuildFontAtlasInternal(loadDefaultLargeFont, loadCustomFonts, loadCustomLargeFonts);
                if (!loadDefaultLargeFont || !loadCustomFonts || !loadCustomLargeFonts)
                {
                    Main.Logger?.Log($"[CheryTools] Rebuilt ImGui font atlas in fallback mode: {modeName}");
                }
                return true;
            }
            catch (Exception ex)
            {
                Main.Logger?.Log($"[CheryTools] Failed to rebuild ImGui font atlas ({modeName}): {ex.Message}");
                try
                {
                    ImGui.GetIO().Fonts.ClearTexData();
                }
                catch
                {
                    // Keep the previous font texture if cleanup itself fails.
                }
                return false;
            }
        }

        private void RebuildFontAtlasInternal(bool loadDefaultLargeFont, bool loadCustomFonts, bool loadCustomLargeFonts)
        {
            var io = ImGui.GetIO();
            io.Fonts.Clear();
            CustomFonts.Clear();
            CustomLargeFonts.Clear();

            // Free any previous pinned handles after fonts are cleared
            foreach (var h in _pinnedHandles)
            {
                if (h.IsAllocated) h.Free();
            }
            _pinnedHandles.Clear();

            string defaultFontPath = GetDefaultUIFontPath();
            
            // Build the optimized glyph ranges for 128px fonts
            bool needsLargeFontRanges = loadDefaultLargeFont || loadCustomLargeFonts;
            IntPtr largeFontRanges = needsLargeFontRanges ? GetLargeFontGlyphRanges() : IntPtr.Zero;

            IntPtr customChineseFull = GetCustomGlyphRanges(io.Fonts.GetGlyphRangesChineseFull());
            IntPtr customChineseCommon = GetCustomGlyphRanges(io.Fonts.GetGlyphRangesChineseSimplifiedCommon());

            if (System.IO.File.Exists(defaultFontPath))
            {
                ImFontPtr defaultUIFont = io.Fonts.AddFontFromFileTTF(defaultFontPath, 20.0f, null, customChineseFull);
                ChineseDefaultUIFont = defaultUIFont;
                DefaultHighResFont = io.Fonts.AddFontFromFileTTF(defaultFontPath, 48.0f, null, customChineseCommon);
                DefaultLargeFont = loadDefaultLargeFont
                    ? io.Fonts.AddFontFromFileTTF(defaultFontPath, 128.0f, null, largeFontRanges)
                    : DefaultHighResFont;

                string chineseFontPath = System.IO.Path.Combine(Main.ModEntry.Path, ChineseDefaultFontFile);
                if (!string.Equals(defaultFontPath, chineseFontPath, StringComparison.OrdinalIgnoreCase)
                    && System.IO.File.Exists(chineseFontPath))
                {
                    ChineseDefaultUIFont = io.Fonts.AddFontFromFileTTF(chineseFontPath, 20.0f, null, customChineseFull);
                }
            }
            else
            {
                ChineseDefaultUIFont = io.Fonts.AddFontDefault();
                DefaultHighResFont = io.Fonts.AddFontDefault();
                DefaultLargeFont = DefaultHighResFont; // fallback
            }

            if (loadCustomFonts && Main.Settings != null && Main.Settings.OverlayerTexts != null)
            {
                foreach (var txt in Main.Settings.OverlayerTexts)
                {
                    if (!string.IsNullOrEmpty(txt.FontPath))
                    {
                        string fontKey = txt.FontPath;
                        string resolvedFontPath = CheryToolsAssets.ResolveAssetPath(fontKey);
                        if (System.IO.File.Exists(resolvedFontPath))
                        {
                            if (!CustomFonts.ContainsKey(fontKey) && !CustomFonts.ContainsKey(resolvedFontPath))
                            {
                                try {
                                    var ptr = io.Fonts.AddFontFromFileTTF(resolvedFontPath, 48.0f, null, customChineseCommon);
                                    StoreFont(CustomFonts, fontKey, resolvedFontPath, ptr);
                                } catch (Exception e) {
                                    Main.Logger.Log($"Failed to load font {resolvedFontPath}: {e.Message}");
                                }
                            }

                            // Calculate max effective size to see if a large version is needed
                            float maxEffectiveSize = txt.FontSize;
                            if (!string.IsNullOrEmpty(txt.TextFormat))
                            {
                                try {
                                    var tempSegs = RichTextParser.Parse(txt.TextFormat, new System.Numerics.Vector4(1, 1, 1, 1));
                                    foreach (var seg in tempSegs)
                                    {
                                        if (seg.HasSizeTag)
                                        {
                                            if (seg.SizeValue > 0)
                                            {
                                                maxEffectiveSize = Math.Max(maxEffectiveSize, seg.SizeValue);
                                            }
                                            else if (seg.SizeValue < 0)
                                            {
                                                maxEffectiveSize = Math.Max(maxEffectiveSize, -seg.SizeValue * txt.FontSize);
                                            }
                                        }
                                    }
                                } catch (Exception ex) {
                                    Main.Logger.Log($"Error parsing text format for size check: {ex.Message}");
                                }
                            }

                            if (loadCustomLargeFonts && maxEffectiveSize > 72f && !CustomLargeFonts.ContainsKey(fontKey) && !CustomLargeFonts.ContainsKey(resolvedFontPath))
                            {
                                try {
                                    var ptr = io.Fonts.AddFontFromFileTTF(resolvedFontPath, 128.0f, null, largeFontRanges);
                                    StoreFont(CustomLargeFonts, fontKey, resolvedFontPath, ptr);
                                    Main.Logger.Log($"Loaded large custom font {resolvedFontPath} at 128px with optimized glyph ranges");
                                } catch (Exception e) {
                                    Main.Logger.Log($"Failed to load large font {resolvedFontPath}: {e.Message}");
                                }
                            }
                        }
                    }
                }
            }

            if (loadCustomFonts && Main.Settings != null && !string.IsNullOrEmpty(Main.Settings.KeyViewerFontPath))
            {
                string fontKey = Main.Settings.KeyViewerFontPath;
                string resolvedFontPath = CheryToolsAssets.ResolveAssetPath(fontKey);
                if (!CustomFonts.ContainsKey(fontKey) && !CustomFonts.ContainsKey(resolvedFontPath) && System.IO.File.Exists(resolvedFontPath))
                {
                    try {
                        var ptr = io.Fonts.AddFontFromFileTTF(resolvedFontPath, 48.0f, null, customChineseCommon);
                        StoreFont(CustomFonts, fontKey, resolvedFontPath, ptr);
                    } catch (Exception e) {
                        Main.Logger.Log($"Failed to load font {resolvedFontPath}: {e.Message}");
                    }
                }
            }

            if (loadCustomFonts && Main.Settings != null)
            {
                var allNodes = Main.Settings.GetAllKeyViewerNodes();

                foreach (var node in allNodes)
                {
                    string keyFontKey = node.KeyFontPath;
                    string keyFontPath = CheryToolsAssets.ResolveAssetPath(keyFontKey);
                    if (!string.IsNullOrEmpty(keyFontKey) && !CustomFonts.ContainsKey(keyFontKey) && !CustomFonts.ContainsKey(keyFontPath) && System.IO.File.Exists(keyFontPath))
                    {
                        try {
                            var ptr = io.Fonts.AddFontFromFileTTF(keyFontPath, 48.0f, null, customChineseCommon);
                            StoreFont(CustomFonts, keyFontKey, keyFontPath, ptr);
                        } catch (Exception e) {
                            Main.Logger.Log($"Failed to load font {keyFontPath}: {e.Message}");
                        }
                    }
                    string countFontKey = node.CountFontPath;
                    string countFontPath = CheryToolsAssets.ResolveAssetPath(countFontKey);
                    if (node.NodeType == 0 && !string.IsNullOrEmpty(countFontKey) && !CustomFonts.ContainsKey(countFontKey) && !CustomFonts.ContainsKey(countFontPath) && System.IO.File.Exists(countFontPath))
                    {
                        try {
                            var ptr = io.Fonts.AddFontFromFileTTF(countFontPath, 48.0f, null, customChineseCommon);
                            StoreFont(CustomFonts, countFontKey, countFontPath, ptr);
                        } catch (Exception e) {
                            Main.Logger.Log($"Failed to load font {countFontPath}: {e.Message}");
                        }
                    }
                }
            }

            io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out int bytesPerPixel);
            int maxTextureSize = Mathf.Max(1, SystemInfo.maxTextureSize);
            if (width <= 0 || height <= 0 || width > maxTextureSize || height > maxTextureSize)
            {
                io.Fonts.ClearTexData();
                throw new InvalidOperationException($"atlas size {width}x{height} exceeds Unity max texture size {maxTextureSize}");
            }
            
            // Simple clean texture - no mipmaps, no blur hacks needed
            // Font loaded at 64px means typical 30-50px display is only 1.3-2.1x downscale
            // Bilinear filtering handles this well without artifacts
            Texture2D newFontTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            newFontTexture.filterMode = FilterMode.Bilinear;
            newFontTexture.wrapMode = TextureWrapMode.Clamp;
            newFontTexture.LoadRawTextureData(pixels, width * height * bytesPerPixel);
            newFontTexture.Apply(false);

            if (_fontTexture != null) Destroy(_fontTexture);
            _fontTexture = newFontTexture;
            
            io.Fonts.SetTexID((IntPtr)_fontTexture.GetInstanceID());
            io.Fonts.ClearTexData();

            if (_material != null)
            {
                _material.mainTexture = _fontTexture;
            }
        }

        private static string GetDefaultUIFontPath()
        {
            string language = Main.Settings != null
                ? LocalizationManager.NormalizeLanguage(Main.Settings.Language)
                : LocalizationManager.DefaultLanguage;

            if (string.Equals(language, "English", StringComparison.OrdinalIgnoreCase)
                || string.Equals(language, "Korean", StringComparison.OrdinalIgnoreCase))
            {
                string localizedFont = System.IO.Path.Combine(Main.ModEntry.Path, LatinKoreanDefaultFontFile);
                if (System.IO.File.Exists(localizedFont))
                {
                    return localizedFont;
                }
            }

            return System.IO.Path.Combine(Main.ModEntry.Path, ChineseDefaultFontFile);
        }

        private static IntPtr GetLargeFontGlyphRanges()
        {
            var uniqueChars = new SortedSet<ushort>();
            
            // 1. Add Basic ASCII printable characters (0x0020 to 0x007E)
            for (ushort c = 0x0020; c <= 0x007E; c++)
            {
                uniqueChars.Add(c);
            }
            
            // 2. Add extra characters commonly used in rhythm games and alignment arrows
            string commonChars = "连击完成太早迟完美←↑→↓↖↗↘↙≤▶┼⊕";
            foreach (char c in commonChars)
            {
                uniqueChars.Add(c);
            }
            
            // 3. Scan all OverlayerTexts to add any characters they use in their text format
            if (Main.Settings != null && Main.Settings.OverlayerTexts != null)
            {
                foreach (var txt in Main.Settings.OverlayerTexts)
                {
                    float maxEffectiveSize = txt.FontSize;
                    if (!string.IsNullOrEmpty(txt.TextFormat))
                    {
                        try {
                            var tempSegs = RichTextParser.Parse(txt.TextFormat, new System.Numerics.Vector4(1, 1, 1, 1));
                            foreach (var seg in tempSegs)
                            {
                                if (seg.HasSizeTag)
                                {
                                    if (seg.SizeValue > 0)
                                    {
                                        maxEffectiveSize = Math.Max(maxEffectiveSize, seg.SizeValue);
                                    }
                                    else if (seg.SizeValue < 0)
                                    {
                                        maxEffectiveSize = Math.Max(maxEffectiveSize, -seg.SizeValue * txt.FontSize);
                                    }
                                }
                            }
                        } catch { }
                    }

                    if (maxEffectiveSize > 72f && !string.IsNullOrEmpty(txt.TextFormat))
                    {
                        foreach (char c in txt.TextFormat)
                        {
                            uniqueChars.Add(c);
                        }
                    }
                }
            }
            
            // 4. Build ranges list
            var rangesList = new List<ushort>();
            ushort start = 0;
            ushort prev = 0;
            bool hasStart = false;
            
            foreach (var c in uniqueChars)
            {
                if (!hasStart)
                {
                    start = c;
                    prev = c;
                    hasStart = true;
                }
                else if (c == prev + 1)
                {
                    prev = c;
                }
                else
                {
                    rangesList.Add(start);
                    rangesList.Add(prev);
                    start = c;
                    prev = c;
                }
            }
            if (hasStart)
            {
                rangesList.Add(start);
                rangesList.Add(prev);
            }
            rangesList.Add(0); // Null terminator
            
            ushort[] rangesArray = rangesList.ToArray();
            var handle = GCHandle.Alloc(rangesArray, GCHandleType.Pinned);
            _pinnedHandles.Add(handle);
            return handle.AddrOfPinnedObject();
        }

        void OnDestroy()
        {
            if (_context != IntPtr.Zero)
            {
                ImGui.DestroyContext(_context);
                _context = IntPtr.Zero;
            }
            foreach (var h in _pinnedHandles)
            {
                if (h.IsAllocated) h.Free();
            }
            _pinnedHandles.Clear();

            if (_fontTexture != null) Destroy(_fontTexture);
            if (_material != null) Destroy(_material);
            if (_canvas != null) Destroy(_canvas.gameObject);
            foreach (var m in _meshes) Destroy(m);
            TextureManager.Clear();
        }

        void Update()
        {
            if (NeedsFontAtlasRebuild)
            {
                NeedsFontAtlasRebuild = false;
                RebuildFontAtlas();
            }

            var currentMousePos = new System.Numerics.Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            _screenMouseDelta = currentMousePos - _screenMousePos;
            _screenMousePos = currentMousePos;

            if (ShouldUpdateOverlay())
            {
                SdfTextRenderer.BeginFrame();
                OnOverlayLayout?.Invoke();
                SdfTextRenderer.EndFrame();
                KeyViewerManager.Instance?.MarkOverlayRendered();
                OverlayerManager.Instance?.MarkOverlayRendered();
            }

            if (!ShouldRenderImGui())
            {
                ClearImGuiCanvasOnce();
                return;
            }

            ApplyPanelScale(Main.Settings != null ? Main.Settings.ImGuiPanelScale : 1.0f);
            var io = ImGui.GetIO();
            float panelScale = Mathf.Max(0.001f, PanelScale);
            io.DisplaySize = new System.Numerics.Vector2(Screen.width / panelScale, Screen.height / panelScale);
            io.DisplayFramebufferScale = System.Numerics.Vector2.One;
            io.DeltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.001f); // Fix assert when paused

            io.AddMousePosEvent(Input.mousePosition.x / panelScale, (Screen.height - Input.mousePosition.y) / panelScale);
            io.AddMouseButtonEvent(0, Input.GetMouseButton(0));
            io.AddMouseButtonEvent(1, Input.GetMouseButton(1));
            io.AddMouseButtonEvent(2, Input.GetMouseButton(2));
            io.AddMouseWheelEvent(0f, Input.mouseScrollDelta.y);

            if (!string.IsNullOrEmpty(Input.inputString))
            {
                foreach (char c in Input.inputString)
                {
                    if (c != '\b' && c != '\r' && c != '\n')
                    {
                        io.AddInputCharacter(c);
                    }
                }
            }

            io.AddKeyEvent(ImGuiKey.Backspace, Input.GetKey(KeyCode.Backspace));
            io.AddKeyEvent(ImGuiKey.Delete, Input.GetKey(KeyCode.Delete));
            io.AddKeyEvent(ImGuiKey.LeftArrow, Input.GetKey(KeyCode.LeftArrow));
            io.AddKeyEvent(ImGuiKey.RightArrow, Input.GetKey(KeyCode.RightArrow));
            io.AddKeyEvent(ImGuiKey.UpArrow, Input.GetKey(KeyCode.UpArrow));
            io.AddKeyEvent(ImGuiKey.DownArrow, Input.GetKey(KeyCode.DownArrow));
            io.AddKeyEvent(ImGuiKey.Enter, Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter));
            io.AddKeyEvent(ImGuiKey.Escape, Input.GetKey(KeyCode.Escape));

            io.AddKeyEvent(ImGuiKey.ModCtrl, Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
            io.AddKeyEvent(ImGuiKey.ModShift, Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            io.AddKeyEvent(ImGuiKey.ModAlt, Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));
            io.AddKeyEvent(ImGuiKey.ModSuper, Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand));

            io.AddKeyEvent(ImGuiKey.C, Input.GetKey(KeyCode.C));
            io.AddKeyEvent(ImGuiKey.V, Input.GetKey(KeyCode.V));
            io.AddKeyEvent(ImGuiKey.X, Input.GetKey(KeyCode.X));
            io.AddKeyEvent(ImGuiKey.A, Input.GetKey(KeyCode.A));

            io.AddKeyEvent(ImGuiKey.Insert, Input.GetKey(KeyCode.Insert));

            ImGui.NewFrame();

            try
            {
                OnImGuiLayout?.Invoke();
            }
            catch (Exception ex)
            {
                Main.Logger?.Log("[CheryTools] ImGui layout exception: " + ex);
            }
            finally
            {
                ImGui.Render();
            }
            UpdateCanvas();
        }

        private static bool ShouldRenderImGui()
        {
            return CheryToolsMenu.IsMenuOpen || FreeMakeEditor.IsOpen;
        }

        private bool ShouldUpdateOverlay()
        {
            if (CheryToolsMenu.IsMenuOpen || FreeMakeEditor.IsOpen)
            {
                _nextOverlayUpdateTime = Time.unscaledTime;
                return true;
            }

            if (Main.Settings != null && Main.Settings.OverlayerEditMode)
            {
                _nextOverlayUpdateTime = Time.unscaledTime;
                return true;
            }

            float rate = Main.Settings != null ? Main.Settings.OverlayUpdateRate : 240.0f;
            if (float.IsNaN(rate) || float.IsInfinity(rate) || rate <= 0f)
            {
                rate = 120.0f;
            }
            rate = Mathf.Clamp(rate, 30.0f, 360.0f);
            float now = Time.unscaledTime;

            long revision = OverlayRenderInvalidator.Revision;
            int stateHash = BuildOverlayStateHash();
            if (revision != _lastOverlayRevision || stateHash != _lastOverlayStateHash)
            {
                _lastOverlayRevision = revision;
                _lastOverlayStateHash = stateHash;
                _nextOverlayUpdateTime = now;
                return true;
            }

            bool keyViewerNeedsUpdate = KeyViewerManager.Instance != null && KeyViewerManager.Instance.ShouldUpdateOverlay(now, rate);
            bool overlayerNeedsUpdate = OverlayerManager.Instance != null && OverlayerManager.Instance.ShouldUpdateOverlay(now, rate);
            if (!keyViewerNeedsUpdate && !overlayerNeedsUpdate)
            {
                return false;
            }

            if (now < _nextOverlayUpdateTime)
            {
                return false;
            }

            _nextOverlayUpdateTime = now + 1.0f / rate;
            return true;
        }

        private int BuildOverlayStateHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Screen.width;
                hash = hash * 31 + Screen.height;
                hash = hash * 31 + (Main.IsEnabled ? 1 : 0);
                hash = hash * 31 + (Main.IsGamePlaying() ? 1 : 0);
                hash = hash * 31 + (CheryToolsMenu.IsMenuOpen ? 1 : 0);
                hash = hash * 31 + (FreeMakeEditor.IsOpen ? 1 : 0);
                if (Main.Settings != null)
                {
                    hash = hash * 31 + (Main.Settings.EnableKeyViewer ? 1 : 0);
                    hash = hash * 31 + (Main.Settings.KeyViewerOnlyShowPlaying ? 1 : 0);
                    hash = hash * 31 + (Main.Settings.OverlayerSystemEnabled ? 1 : 0);
                    hash = hash * 31 + (Main.Settings.OverlayerOnlyShowPlaying ? 1 : 0);
                    hash = hash * 31 + (Main.Settings.OverlayerEditMode ? 1 : 0);
                }
                return hash;
            }
        }

        private void ClearImGuiCanvasOnce()
        {
            if (!_imguiCanvasHasContent)
            {
                return;
            }

            for (int i = 0; i < _renderers.Count; i++)
            {
                _renderers[i].Clear();
            }
            _imguiCanvasHasContent = false;
        }

        private unsafe void UpdateCanvas()
        {
            var drawData = ImGui.GetDrawData();
            if (drawData.CmdListsCount == 0)
            {
                for (int i = 0; i < _renderers.Count; i++) _renderers[i].Clear();
                _imguiCanvasHasContent = false;
                return;
            }

            _imguiCanvasHasContent = true;
            int cmdCount = 0;
            float panelScale = Mathf.Max(0.001f, PanelScale);
            float offsetX = -Screen.width / 2.0f;
            float offsetY = Screen.height / 2.0f;
            for (int n = 0; n < drawData.CmdListsCount; n++)
            {
                ImDrawListPtr cmdList = drawData.CmdLists[n];
                int vtxCount = cmdList.VtxBuffer.Size;
                int idxCount = cmdList.IdxBuffer.Size;

                if (_vertices.Length < vtxCount)
                {
                    _vertices = new Vector3[vtxCount];
                    _uvs = new Vector2[vtxCount];
                    _colors = new Color32[vtxCount];
                }
                if (_indices.Length < idxCount)
                {
                    _indices = new int[idxCount];
                }

                ImDrawVert* vtxPtr = (ImDrawVert*)cmdList.VtxBuffer.Data;
                for (int i = 0; i < vtxCount; i++)
                {
                    _vertices[i] = new Vector3(offsetX + vtxPtr[i].pos.X * panelScale, offsetY - vtxPtr[i].pos.Y * panelScale, 0); // Invert Y for Canvas
                    _uvs[i] = new Vector2(vtxPtr[i].uv.X, vtxPtr[i].uv.Y);
                    uint c = vtxPtr[i].col;
                    _colors[i] = new Color32((byte)(c & 0xFF), (byte)((c >> 8) & 0xFF), (byte)((c >> 16) & 0xFF), (byte)((c >> 24) & 0xFF));
                }

                ushort* idxPtr = (ushort*)cmdList.IdxBuffer.Data;
                for (int i = 0; i < idxCount; i++)
                {
                    _indices[i] = idxPtr[i];
                }

                for (int cmdi = 0; cmdi < cmdList.CmdBuffer.Size; cmdi++)
                {
                    ImDrawCmdPtr pcmd = cmdList.CmdBuffer[cmdi];
                    if (pcmd.UserCallback != IntPtr.Zero) continue;

                    if (cmdCount >= _renderers.Count)
                    {
                        var go = new GameObject("ImGui_Cmd_" + cmdCount, typeof(RectTransform));
                        go.transform.SetParent(_canvas.transform, false);
                        // Do not touch RectTransform properties to leave it centered at Canvas origin
                        var cr = go.AddComponent<CanvasRenderer>();
                        _renderers.Add(cr);
                        var mesh = new Mesh();
                        mesh.MarkDynamic();
                        _meshes.Add(mesh);
                    }

                    var renderer = _renderers[cmdCount];
                    var meshObj = _meshes[cmdCount];

                    int elemCount = (int)pcmd.ElemCount;
                    if (_subIndices.Length < elemCount)
                    {
                        _subIndices = new int[elemCount];
                    }
                    Array.Copy(_indices, (int)pcmd.IdxOffset, _subIndices, 0, elemCount);

                    meshObj.Clear();
                    meshObj.SetVertices(_vertices, 0, vtxCount);
                    meshObj.SetUVs(0, _uvs, 0, vtxCount);
                    meshObj.SetColors(_colors, 0, vtxCount);
                    meshObj.SetIndices(_subIndices, 0, elemCount, MeshTopology.Triangles, 0);

                    // Transform clipping rect to match the centered RectTransform
                    float clipX = offsetX + pcmd.ClipRect.X * panelScale;
                    float clipY = offsetY - pcmd.ClipRect.W * panelScale;
                    float clipW = (pcmd.ClipRect.Z - pcmd.ClipRect.X) * panelScale;
                    float clipH = (pcmd.ClipRect.W - pcmd.ClipRect.Y) * panelScale;
                    renderer.EnableRectClipping(new Rect(clipX, clipY, clipW, clipH));
                    
                    Texture2D renderTex = _fontTexture;
                    if (pcmd.TextureId != IntPtr.Zero && pcmd.TextureId != (IntPtr)_fontTexture.GetInstanceID())
                    {
                        Texture2D customTex = TextureManager.GetTextureByPtr(pcmd.TextureId);
                        if (customTex != null) renderTex = customTex;
                    }
                    
                    renderer.SetMaterial(_material, renderTex);
                    renderer.SetColor(Color.white); // Explicitly ensure vertex colors are not multiplied by zero/black
                    renderer.SetMesh(meshObj);

                    cmdCount++;
                }
            }

            for (int i = cmdCount; i < _renderers.Count; i++)
            {
                _renderers[i].Clear();
            }
        }

        private static unsafe IntPtr GetCustomGlyphRanges(IntPtr baseRangesPtr)
        {
            var uniqueChars = new SortedSet<ushort>();

            if (baseRangesPtr != IntPtr.Zero)
            {
                ushort* p = (ushort*)baseRangesPtr.ToPointer();
                while (*p != 0)
                {
                    ushort start = *p++;
                    ushort end = *p++;
                    for (int c = start; c <= end; c++)
                    {
                        uniqueChars.Add((ushort)c);
                    }
                }
            }

            string extraSymbols = "←↑→↓↖↗↘↙≤▶┼⊕";
            foreach (char c in extraSymbols)
            {
                uniqueChars.Add(c);
            }

            AddRange(uniqueChars, 0x1100, 0x11FF); // Hangul Jamo
            AddRange(uniqueChars, 0x3130, 0x318F); // Hangul Compatibility Jamo
            AddRange(uniqueChars, 0xAC00, 0xD7AF); // Hangul Syllables

            var rangesList = new List<ushort>();
            ushort rangeStart = 0;
            ushort prev = 0;
            bool hasStart = false;

            foreach (var c in uniqueChars)
            {
                if (!hasStart)
                {
                    rangeStart = c;
                    prev = c;
                    hasStart = true;
                }
                else if (c == prev + 1)
                {
                    prev = c;
                }
                else
                {
                    rangesList.Add(rangeStart);
                    rangesList.Add(prev);
                    rangeStart = c;
                    prev = c;
                }
            }
            if (hasStart)
            {
                rangesList.Add(rangeStart);
                rangesList.Add(prev);
            }
            rangesList.Add(0); // Null terminator

            ushort[] rangesArray = rangesList.ToArray();
            var handle = GCHandle.Alloc(rangesArray, GCHandleType.Pinned);
            _pinnedHandles.Add(handle);
            return handle.AddrOfPinnedObject();
        }

        private static void AddRange(SortedSet<ushort> chars, int start, int end)
        {
            for (int c = start; c <= end; c++)
            {
                chars.Add((ushort)c);
            }
        }
    }
}
