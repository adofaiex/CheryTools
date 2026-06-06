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

        private Vector3[] _vertices = new Vector3[0];
        private Vector2[] _uvs = new Vector2[0];
        private Color32[] _colors = new Color32[0];
        private int[] _indices = new int[0];

        public Action OnLayout;

        public static Dictionary<string, ImFontPtr> CustomFonts = new Dictionary<string, ImFontPtr>();
        public static Dictionary<string, ImFontPtr> CustomLargeFonts = new Dictionary<string, ImFontPtr>();
        public static ImFontPtr DefaultHighResFont;  // 48px - for text sizes ≤72px
        public static ImFontPtr DefaultLargeFont;      // 128px - for text sizes >72px
        public static bool NeedsFontAtlasRebuild = false;
        private static List<GCHandle> _pinnedHandles = new List<GCHandle>();

        void Awake()
        {
            _context = ImGui.CreateContext();
            ImGui.SetCurrentContext(_context);

            RebuildFontAtlas();

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

        public static ImFontPtr GetHighResFontOrDefault(string path)
        {
            if (!string.IsNullOrEmpty(path) && CustomFonts.TryGetValue(path, out var f1)) return f1;
            if (Main.Settings != null && !string.IsNullOrEmpty(Main.Settings.KeyViewerFontPath) && CustomFonts.TryGetValue(Main.Settings.KeyViewerFontPath, out var f2)) return f2;
            return DefaultHighResFont;
        }

        public void RebuildFontAtlas()
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

            string defaultFontPath = System.IO.Path.Combine(Main.ModEntry.Path, "MiSans-Bold.ttf");
            
            // Build the optimized glyph ranges for 128px fonts
            IntPtr largeFontRanges = GetLargeFontGlyphRanges();

            IntPtr customChineseFull = GetCustomGlyphRanges(io.Fonts.GetGlyphRangesChineseFull());
            IntPtr customChineseCommon = GetCustomGlyphRanges(io.Fonts.GetGlyphRangesChineseSimplifiedCommon());

            if (System.IO.File.Exists(defaultFontPath))
            {
                io.Fonts.AddFontFromFileTTF(defaultFontPath, 20.0f, null, customChineseFull);
                DefaultHighResFont = io.Fonts.AddFontFromFileTTF(defaultFontPath, 48.0f, null, customChineseCommon);
                DefaultLargeFont = io.Fonts.AddFontFromFileTTF(defaultFontPath, 128.0f, null, largeFontRanges);
            }
            else
            {
                io.Fonts.AddFontDefault();
                DefaultHighResFont = io.Fonts.AddFontDefault();
                DefaultLargeFont = DefaultHighResFont; // fallback
            }

            if (Main.Settings != null && Main.Settings.OverlayerTexts != null)
            {
                foreach (var txt in Main.Settings.OverlayerTexts)
                {
                    if (!string.IsNullOrEmpty(txt.FontPath))
                    {
                        if (System.IO.File.Exists(txt.FontPath))
                        {
                            if (!CustomFonts.ContainsKey(txt.FontPath))
                            {
                                try {
                                    var ptr = io.Fonts.AddFontFromFileTTF(txt.FontPath, 48.0f, null, customChineseCommon);
                                    CustomFonts[txt.FontPath] = ptr;
                                } catch (Exception e) {
                                    Main.Logger.Log($"Failed to load font {txt.FontPath}: {e.Message}");
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

                            if (maxEffectiveSize > 72f && !CustomLargeFonts.ContainsKey(txt.FontPath))
                            {
                                try {
                                    var ptr = io.Fonts.AddFontFromFileTTF(txt.FontPath, 128.0f, null, largeFontRanges);
                                    CustomLargeFonts[txt.FontPath] = ptr;
                                    Main.Logger.Log($"Loaded large custom font {txt.FontPath} at 128px with optimized glyph ranges");
                                } catch (Exception e) {
                                    Main.Logger.Log($"Failed to load large font {txt.FontPath}: {e.Message}");
                                }
                            }
                        }
                    }
                }
            }

            if (Main.Settings != null && !string.IsNullOrEmpty(Main.Settings.KeyViewerFontPath) && !CustomFonts.ContainsKey(Main.Settings.KeyViewerFontPath))
            {
                if (System.IO.File.Exists(Main.Settings.KeyViewerFontPath))
                {
                    try {
                        var ptr = io.Fonts.AddFontFromFileTTF(Main.Settings.KeyViewerFontPath, 48.0f, null, customChineseCommon);
                        CustomFonts[Main.Settings.KeyViewerFontPath] = ptr;
                    } catch (Exception e) {
                        Main.Logger.Log($"Failed to load font {Main.Settings.KeyViewerFontPath}: {e.Message}");
                    }
                }
            }

            if (Main.Settings != null)
            {
                var allNodes = new System.Collections.Generic.List<KVNode>();
                if (Main.Settings.Layout16K != null) allNodes.AddRange(Main.Settings.Layout16K);
                if (Main.Settings.Layout12K != null) allNodes.AddRange(Main.Settings.Layout12K);
                if (Main.Settings.Layout10K != null) allNodes.AddRange(Main.Settings.Layout10K);
                if (Main.Settings.Layout8K != null) allNodes.AddRange(Main.Settings.Layout8K);

                foreach (var node in allNodes)
                {
                    if (!string.IsNullOrEmpty(node.KeyFontPath) && !CustomFonts.ContainsKey(node.KeyFontPath) && System.IO.File.Exists(node.KeyFontPath))
                    {
                        try {
                            var ptr = io.Fonts.AddFontFromFileTTF(node.KeyFontPath, 48.0f, null, customChineseCommon);
                            CustomFonts[node.KeyFontPath] = ptr;
                        } catch (Exception e) {
                            Main.Logger.Log($"Failed to load font {node.KeyFontPath}: {e.Message}");
                        }
                    }
                    if (node.NodeType == 0 && !string.IsNullOrEmpty(node.CountFontPath) && !CustomFonts.ContainsKey(node.CountFontPath) && System.IO.File.Exists(node.CountFontPath))
                    {
                        try {
                            var ptr = io.Fonts.AddFontFromFileTTF(node.CountFontPath, 48.0f, null, customChineseCommon);
                            CustomFonts[node.CountFontPath] = ptr;
                        } catch (Exception e) {
                            Main.Logger.Log($"Failed to load font {node.CountFontPath}: {e.Message}");
                        }
                    }
                }
            }

            io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out int bytesPerPixel);
            
            if (_fontTexture != null) Destroy(_fontTexture);
            
            // Simple clean texture - no mipmaps, no blur hacks needed
            // Font loaded at 64px means typical 30-50px display is only 1.3-2.1x downscale
            // Bilinear filtering handles this well without artifacts
            _fontTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            _fontTexture.filterMode = FilterMode.Bilinear;
            _fontTexture.wrapMode = TextureWrapMode.Clamp;
            _fontTexture.LoadRawTextureData(pixels, width * height * bytesPerPixel);
            _fontTexture.Apply(false);
            
            io.Fonts.SetTexID((IntPtr)_fontTexture.GetInstanceID());
            io.Fonts.ClearTexData();

            if (_material != null)
            {
                _material.mainTexture = _fontTexture;
            }
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

            var io = ImGui.GetIO();
            io.DisplaySize = new System.Numerics.Vector2(Screen.width, Screen.height);
            io.DeltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.001f); // Fix assert when paused

            io.AddMousePosEvent(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
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

            OnLayout?.Invoke();

            ImGui.Render();
            UpdateCanvas();
        }

        private unsafe void UpdateCanvas()
        {
            var drawData = ImGui.GetDrawData();
            if (drawData.CmdListsCount == 0)
            {
                for (int i = 0; i < _renderers.Count; i++) _renderers[i].Clear();
                return;
            }

            int cmdCount = 0;
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
                    _vertices[i] = new Vector3(vtxPtr[i].pos.X, -vtxPtr[i].pos.Y, 0); // Invert Y for Canvas
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
                    int[] subIndices = new int[elemCount];
                    Array.Copy(_indices, (int)pcmd.IdxOffset, subIndices, 0, elemCount);

                    var io = ImGui.GetIO();
                    float offsetX = -io.DisplaySize.X / 2.0f;
                    float offsetY = io.DisplaySize.Y / 2.0f;

                    // Apply offset so that ImGui (0,0) ends up at Top-Left of Screen
                    Vector3[] shiftedVertices = new Vector3[vtxCount];
                    for (int i = 0; i < vtxCount; i++)
                    {
                        shiftedVertices[i] = new Vector3(offsetX + _vertices[i].x, offsetY + _vertices[i].y, 0); // Note: _vertices[i].y is already -pos.Y
                    }

                    meshObj.Clear();
                    meshObj.SetVertices(shiftedVertices, 0, vtxCount);
                    meshObj.SetUVs(0, _uvs, 0, vtxCount);
                    meshObj.SetColors(_colors, 0, vtxCount);
                    meshObj.SetIndices(subIndices, 0, elemCount, MeshTopology.Triangles, 0);

                    // Transform clipping rect to match the centered RectTransform
                    float clipX = offsetX + pcmd.ClipRect.X;
                    float clipY = offsetY - pcmd.ClipRect.W;
                    float clipW = pcmd.ClipRect.Z - pcmd.ClipRect.X;
                    float clipH = pcmd.ClipRect.W - pcmd.ClipRect.Y;
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
    }
}
