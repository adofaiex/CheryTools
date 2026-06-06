using System;
using UnityEngine;
using ImGuiNET;

namespace CheryTools
{
    public class OverlayerManager : MonoBehaviour
    {
        public static OverlayerManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private static float _fpsTimer = 0f;
        private static float _cachedFps = 0f;
        private static int _draggingIndex = -1;

        private int _lastHitCount = 0;
        private int _currentPureCombo = 0;
        private int _currentPerfectCombo = 0;

        public class AnimPlaybackState
        {
            public float CurrentTime = 0f;
            public bool IsPlaying = false;
        }
        
        private System.Collections.Generic.Dictionary<OverlayerAnimation, AnimPlaybackState> _animStates = new System.Collections.Generic.Dictionary<OverlayerAnimation, AnimPlaybackState>();

        public AnimPlaybackState GetAnimState(OverlayerAnimation anim)
        {
            if (!_animStates.TryGetValue(anim, out var state))
            {
                state = new AnimPlaybackState();
                _animStates[anim] = state;
            }
            return state;
        }

        private bool _anyKeyPressedThisFrame = false;
        private bool _comboIncreasedThisFrame = false;

        private (float x, float y, float sx, float sy, float opacity) EvaluateAnimState(OverlayerAnimation anim, float currentTime)
        {
            if (anim.ParsedFrames == null || anim.ParsedFrames.Count == 0) return (0f, 0f, 1f, 1f, 1f);
            
            var frames = anim.ParsedFrames;
            JsonAnimFrame prev = frames[0];
            JsonAnimFrame next = frames[frames.Count - 1];
            
            if (currentTime <= prev.time) return (prev.x ?? 0f, prev.y ?? 0f, prev.zoomx ?? 1f, prev.zoomy ?? 1f, prev.opacity ?? 1f);
            if (currentTime >= next.time) return (next.x ?? 0f, next.y ?? 0f, next.zoomx ?? 1f, next.zoomy ?? 1f, next.opacity ?? 1f);
            
            for (int k = 0; k < frames.Count - 1; k++)
            {
                if (currentTime >= frames[k].time && currentTime <= frames[k+1].time)
                {
                    prev = frames[k];
                    next = frames[k+1];
                    break;
                }
            }
            
            float t = (currentTime - prev.time) / (next.time - prev.time);
            float easedT = EasingUtil.EvaluateEasing(t, next.easing);
            
            float x = (prev.x ?? 0f) + ((next.x ?? 0f) - (prev.x ?? 0f)) * easedT;
            float y = (prev.y ?? 0f) + ((next.y ?? 0f) - (prev.y ?? 0f)) * easedT;
            float sx = (prev.zoomx ?? 1f) + ((next.zoomx ?? 1f) - (prev.zoomx ?? 1f)) * easedT;
            float sy = (prev.zoomy ?? 1f) + ((next.zoomy ?? 1f) - (prev.zoomy ?? 1f)) * easedT;
            float opacity = (prev.opacity ?? 1f) + ((next.opacity ?? 1f) - (prev.opacity ?? 1f)) * easedT;
            
            return (x, y, sx, sy, opacity);
        }

        private void Update()
        {
            _anyKeyPressedThisFrame = Input.anyKeyDown;
            _comboIncreasedThisFrame = false;

            if (scrController.instance != null && scrController.instance.playerOne != null && scrController.instance.playerOne.marginTracker != null)
            {
                var hitMargins = scrController.instance.playerOne.marginTracker.hitMargins;
                int currentHitCount = hitMargins.Count;

                if (currentHitCount < _lastHitCount || (currentHitCount == 0 && _lastHitCount > 0))
                {
                    // Restarted or reset
                    _currentPureCombo = 0;
                    _currentPerfectCombo = 0;
                }
                else if (currentHitCount > _lastHitCount)
                {
                    for (int i = _lastHitCount; i < currentHitCount; i++)
                    {
                        HitMargin hit = hitMargins[i];
                        if (hit == HitMargin.Perfect || hit == HitMargin.Auto)
                        {
                            _currentPureCombo++;
                            _currentPerfectCombo++;
                            _comboIncreasedThisFrame = true;
                        }
                        else if (hit == HitMargin.EarlyPerfect || hit == HitMargin.LatePerfect)
                        {
                            _currentPureCombo = 0;
                            _currentPerfectCombo++;
                            _comboIncreasedThisFrame = true;
                        }
                        else
                        {
                            _currentPureCombo = 0;
                            _currentPerfectCombo = 0;
                        }
                    }
                }
                _lastHitCount = currentHitCount;
            }
            else
            {
                _lastHitCount = 0;
                _currentPureCombo = 0;
                _currentPerfectCombo = 0;
            }

            // Advance animation frames and trigger check
            if (Main.Settings.OverlayerTexts != null)
            {
                foreach (var ovText in Main.Settings.OverlayerTexts)
                {
                    if (ovText.Animations == null) continue;
                    foreach (var anim in ovText.Animations)
                    {
                        if (!anim.IsEnabled) continue;
                        
                        var state = GetAnimState(anim);
                        // 移除对 AnimationEditorUI.IsOpen 的依赖，因为我们删了它
                        if (anim.Trigger == AnimationTrigger.OnClick && _anyKeyPressedThisFrame)
                        {
                            state.IsPlaying = true;
                            state.CurrentTime = 0f;
                        }
                        else if (anim.Trigger == AnimationTrigger.OnComboIncrease && _comboIncreasedThisFrame)
                        {
                            state.IsPlaying = true;
                            state.CurrentTime = 0f;
                        }

                        if (state.IsPlaying && anim.ParsedFrames != null && anim.ParsedFrames.Count > 0)
                        {
                            float maxTime = anim.ParsedFrames[anim.ParsedFrames.Count - 1].time;
                            state.CurrentTime += UnityEngine.Time.unscaledDeltaTime;
                            if (state.CurrentTime > maxTime)
                            {
                                state.IsPlaying = false;
                                state.CurrentTime = maxTime;
                            }
                        }
                    }
                }
            }
        }

        public void RenderUI()
        {
            if (!Main.Settings.OverlayerSystemEnabled) return;

            bool editMode = Main.Settings.OverlayerEditMode;
            if (Main.Settings.OverlayerOnlyShowPlaying && !Main.IsGamePlaying() && !editMode) return;

            var texts = Main.Settings.OverlayerTexts;

            _fpsTimer += UnityEngine.Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.25f)
            {
                _cachedFps = 1.0f / UnityEngine.Time.unscaledDeltaTime;
                _fpsTimer = 0f;
            }
            
            for (int i = 0; i < texts.Count; i++)
            {
                var ovText = texts[i];
                if (!ovText.IsEnabled && !editMode) continue;

                float animOffsetX = 0f;
                float animOffsetY = 0f;
                float animScaleXMult = 1f;
                float animScaleYMult = 1f;
                float animOpacityMult = 1f;

                if (ovText.Animations != null)
                {
                    foreach (var anim in ovText.Animations)
                    {
                        if (!anim.IsEnabled) continue;
                        
                        var state = GetAnimState(anim);
                        if (!state.IsPlaying && state.CurrentTime <= 0f) continue;
                        
                        var evaluated = EvaluateAnimState(anim, state.CurrentTime);
                        
                        animOffsetX += evaluated.x;
                        animOffsetY += evaluated.y;
                        
                        animScaleXMult *= evaluated.sx;
                        animScaleYMult *= evaluated.sy;
                        
                        animOpacityMult *= evaluated.opacity;
                    }
                }

                string rawText = ovText.TextFormat;
                
                // Process placeholders
                if (rawText.Contains("{"))
                {
                    if (rawText.Contains("{kps}"))
                    {
                        int kps = 0;
                        if (KeyViewerManager.Instance != null) kps = KeyViewerManager.Instance.CurrentKPS;
                        rawText = rawText.Replace("{kps}", kps.ToString());
                    }
                    if (rawText.Contains("{tot}"))
                    {
                        int tot = Main.Settings.TotalHits;
                        rawText = rawText.Replace("{tot}", tot.ToString());
                    }
                    if (rawText.Contains("{fps"))
                    {
                        rawText = System.Text.RegularExpressions.Regex.Replace(rawText, @"\{fps(?:[:](\d+))?\}", match => {
                            if (match.Groups[1].Success) {
                                return _cachedFps.ToString("F" + match.Groups[1].Value);
                            }
                            return ((int)_cachedFps).ToString();
                        });
                    }
                    if (scrConductor.instance != null)
                    {
                        float pitch = scrConductor.instance.song.pitch;
                        float baseBpm = scrConductor.instance.bpm;
                        double tbpm = baseBpm * pitch;
                        double cbpm = tbpm;

                        if (scrController.instance != null && scrLevelMaker.instance != null)
                        {
                            int seqID = scrController.instance.currentSeqID;
                            if (seqID >= 0 && seqID < scrLevelMaker.instance.listFloors.Count)
                            {
                                scrFloor currentFloor = scrLevelMaker.instance.listFloors[seqID];
                                tbpm = baseBpm * pitch * currentFloor.speed;
                                cbpm = tbpm;
                                
                                if (currentFloor.nextfloor != null)
                                {
                                    cbpm = (60.0 / (currentFloor.nextfloor.entryTime - currentFloor.entryTime)) * pitch;
                                }
                            }
                        }

                        if (rawText.Contains("{bpm}"))
                            rawText = rawText.Replace("{bpm}", ((int)baseBpm).ToString());
                        if (rawText.Contains("{tbpm}"))
                            rawText = rawText.Replace("{tbpm}", ((int)System.Math.Round(tbpm)).ToString());
                        if (rawText.Contains("{cbpm}"))
                            rawText = rawText.Replace("{cbpm}", ((int)System.Math.Round(cbpm)).ToString());
                    }
                    
                    scrMarginTracker tracker = null;
                    if (scrController.instance != null && scrController.instance.playerOne != null)
                        tracker = scrController.instance.playerOne.marginTracker;

                    if (rawText.Contains("{te}")) rawText = rawText.Replace("{te}", tracker != null ? tracker.GetHits(HitMargin.TooEarly).ToString() : "0");
                    if (rawText.Contains("{ve}")) rawText = rawText.Replace("{ve}", tracker != null ? tracker.GetHits(HitMargin.VeryEarly).ToString() : "0");
                    if (rawText.Contains("{ep}")) rawText = rawText.Replace("{ep}", tracker != null ? tracker.GetHits(HitMargin.EarlyPerfect).ToString() : "0");
                    if (rawText.Contains("{p}")) rawText = rawText.Replace("{p}", tracker != null ? tracker.GetHits(HitMargin.Perfect).ToString() : "0");
                    if (rawText.Contains("{lp}")) rawText = rawText.Replace("{lp}", tracker != null ? tracker.GetHits(HitMargin.LatePerfect).ToString() : "0");
                    if (rawText.Contains("{vl}")) rawText = rawText.Replace("{vl}", tracker != null ? tracker.GetHits(HitMargin.VeryLate).ToString() : "0");
                    if (rawText.Contains("{tl}")) rawText = rawText.Replace("{tl}", tracker != null ? tracker.GetHits(HitMargin.TooLate).ToString() : "0");
                    if (rawText.Contains("{miss}")) rawText = rawText.Replace("{miss}", tracker != null ? tracker.GetDeaths().ToString() : "0");
                    
                    if (rawText.Contains("{acc"))
                    {
                        rawText = System.Text.RegularExpressions.Regex.Replace(rawText, @"\{acc(?:[:](\d+))?\}", match => {
                            float acc = tracker != null ? (tracker.percentAcc * 100f) : 0f;
                            if (match.Groups[1].Success) {
                                return acc.ToString("F" + match.Groups[1].Value);
                            }
                            return acc.ToString("F2");
                        });
                    }
                    if (rawText.Contains("{xacc"))
                    {
                        rawText = System.Text.RegularExpressions.Regex.Replace(rawText, @"\{xacc(?:[:](\d+))?\}", match => {
                            float acc = tracker != null ? (tracker.percentXAcc * 100f) : 0f;
                            if (match.Groups[1].Success) {
                                return acc.ToString("F" + match.Groups[1].Value);
                            }
                            return acc.ToString("F2");
                        });
                    }
                    if (rawText.Contains("{progress}"))
                    {
                        rawText = System.Text.RegularExpressions.Regex.Replace(rawText, @"\{progress(?:[:](\d+))?\}", match => {
                            int decimals = 2;
                            if (match.Groups[1].Success) int.TryParse(match.Groups[1].Value, out decimals);
                            string formatStr = "F" + decimals;
                            double p = 0;
                            if (tracker != null && scrController.instance != null && scrController.instance.gameworld)
                            {
                                p = scrController.instance.percentComplete * 100.0;
                            }
                            return p.ToString(formatStr);
                        });
                    }

                    if (rawText.Contains("{combo}"))
                    {
                        rawText = rawText.Replace("{combo}", _currentPureCombo.ToString());
                    }
                    if (rawText.Contains("{combo:p}"))
                    {
                        rawText = rawText.Replace("{combo:p}", _currentPerfectCombo.ToString());
                    }

                    if (rawText.Contains("{music}"))
                    {
                        string musicText = "Author - SongName";
                        if (scrUIController.instance != null && scrUIController.instance.txtLevelName != null)
                        {
                            musicText = scrUIController.instance.txtLevelName.text;
                        }
                        // 去除可能包含的富文本标签，如 <size=0> 或 </color>
                        musicText = System.Text.RegularExpressions.Regex.Replace(musicText, "<.*?>", string.Empty);
                        rawText = rawText.Replace("{music}", musicText);
                    }
                }

                ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
                
                flags |= ImGuiWindowFlags.NoMove;

                if (!editMode)
                {
                    flags |= ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoInputs;
                }

                // Calculate max effective size for this block (checks FontSize and formula size tags)
                float maxEffectiveSize = ovText.FontSize;
                if (!string.IsNullOrEmpty(rawText))
                {
                    try {
                        var tempSegs = RichTextParser.Parse(rawText, new System.Numerics.Vector4(1, 1, 1, 1));
                        foreach (var seg in tempSegs)
                        {
                            if (seg.HasSizeTag)
                            {
                                if (seg.SizeValue > 0)
                                {
                                    maxEffectiveSize = Mathf.Max(maxEffectiveSize, seg.SizeValue);
                                }
                                else if (seg.SizeValue < 0)
                                {
                                    maxEffectiveSize = Mathf.Max(maxEffectiveSize, -seg.SizeValue * ovText.FontSize);
                                }
                            }
                        }
                    } catch (Exception) {
                        // fallback to base FontSize
                    }
                }

                // Two-tier font selection: use 128px font for large text, 48px for small/medium
                bool hasCustomFont = !string.IsNullOrEmpty(ovText.FontPath) && ImGuiController.CustomFonts.ContainsKey(ovText.FontPath);
                bool hasCustomLargeFont = hasCustomFont && ImGuiController.CustomLargeFonts.ContainsKey(ovText.FontPath);
                ImFontPtr targetFont;
                float fontBaseSize;

                if (maxEffectiveSize > 72f)
                {
                    if (hasCustomLargeFont)
                    {
                        targetFont = ImGuiController.CustomLargeFonts[ovText.FontPath];
                        fontBaseSize = 128.0f;
                    }
                    else if (hasCustomFont)
                    {
                        targetFont = ImGuiController.CustomFonts[ovText.FontPath];
                        fontBaseSize = 48.0f;
                    }
                    else
                    {
                        targetFont = ImGuiController.DefaultLargeFont;
                        fontBaseSize = 128.0f;
                    }
                }
                else
                {
                    if (hasCustomFont)
                    {
                        targetFont = ImGuiController.CustomFonts[ovText.FontPath];
                        fontBaseSize = 48.0f;
                    }
                    else
                    {
                        targetFont = ImGuiController.DefaultHighResFont;
                        fontBaseSize = 48.0f;
                    }
                }

                float scale = (ovText.FontSize / fontBaseSize) * animScaleXMult;

                ImGui.PushFont(targetFont);
                try
                {
                    // Calculate widths BEFORE Begin.
                    // CalculateRichTextWidth returns absolute width in pixels, so no need to multiply by scale.
                    string[] lines = rawText.Split('\n');
                    float[] lineRenderWidths = new float[lines.Length];
                    float maxLineWidth = 0;
                    for (int j = 0; j < lines.Length; j++)
                    {
                        float lw = CalculateRichTextWidth(lines[j], ovText.LetterSpacing, ovText.FontSize, ovText, animScaleXMult);
                        lineRenderWidths[j] = lw;
                        maxLineWidth = Mathf.Max(maxLineWidth, lw);
                    }

                    float baseFontHeight = ImGui.GetFontSize();
                    float totalHeight = 0f;
                    for (int j = 0; j < lines.Length; j++)
                    {
                        totalHeight += baseFontHeight * scale;
                        if (j < lines.Length - 1) totalHeight += ovText.LineHeightOffset * scale;
                    }

                    float windowPadX = ImGui.GetStyle().WindowPadding.X;
                    float windowPadY = ImGui.GetStyle().WindowPadding.Y;
                    float shadowExtraX = ovText.EnableShadow ? Mathf.Abs(ovText.ShadowOffset[0]) * scale : 0f;
                    float shadowExtraY = ovText.EnableShadow ? Mathf.Abs(ovText.ShadowOffset[1]) * scale : 0f;

                    // Safety margin: 3% of text width + 8px to cover glyph overhang and rounding errors
                    float safetyMargin = maxLineWidth * 0.03f + 8f;
                    float windowWidth = maxLineWidth + windowPadX * 2.0f + shadowExtraX + safetyMargin;
                    float windowHeight = totalHeight + windowPadY * 2.0f + shadowExtraY + 4f;

                    float topLeftX = (ovText.PositionX + animOffsetX) - ovText.PivotX * windowWidth;
                    float topLeftY = (ovText.PositionY + animOffsetY) - ovText.PivotY * windowHeight;

                    ImGui.SetNextWindowPos(new System.Numerics.Vector2(topLeftX, topLeftY), ImGuiCond.Always);
                    ImGui.SetNextWindowSize(new System.Numerics.Vector2(windowWidth, windowHeight), ImGuiCond.Always);

                    if (editMode)
                    {
                        ImGui.PushStyleColor(ImGuiCol.WindowBg, new System.Numerics.Vector4(0f, 0f, 0f, 0f));
                        ImGui.PushStyleColor(ImGuiCol.Border, new System.Numerics.Vector4(0f, 0f, 0f, 0f));
                        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
                    }
                    try
                    {
                        bool isVisible = ImGui.Begin($"CheryTools_OV_Block_{i}", flags);
                        try
                        {
                            if (isVisible)
                            {
                                ImGui.SetWindowFontScale(scale);

                                if (editMode)
                                {
                                    ImGuiIOPtr io = ImGui.GetIO();
                                    if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
                                    {
                                        if (_draggingIndex == i)
                                        {
                                            _draggingIndex = -1;
                                        }
                                    }
                                    else if (_draggingIndex == -1 && ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                                    {
                                        _draggingIndex = i;
                                    }

                                    if (_draggingIndex == i)
                                    {
                                        var delta = io.MouseDelta;
                                        if (delta.X != 0f || delta.Y != 0f)
                                        {
                                            ovText.PositionX += delta.X;
                                            ovText.PositionY += delta.Y;
                                            Main.RequestSave();
                                        }
                                    }
                                }

                                var colorVec = new System.Numerics.Vector4(ovText.TextColor[0], ovText.TextColor[1], ovText.TextColor[2], ovText.TextColor[3] * animOpacityMult);
                                
                                if (editMode)
                                {
                                    var min = ImGui.GetWindowPos();
                                    var max = min + ImGui.GetWindowSize();
                                    ImGui.GetWindowDrawList().AddRect(min, max, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(0.2f, 0.6f, 1.0f, 1.0f)), 0f, ImDrawFlags.None, 2.0f);
                                }

                                if (ovText.EnableShadow)
                                {
                                    var shadowColVec = new System.Numerics.Vector4(ovText.ShadowColor[0], ovText.ShadowColor[1], ovText.ShadowColor[2], ovText.ShadowColor[3] * animOpacityMult);
                                    float shadowOffX = ovText.ShadowOffset[0] * scale;
                                    float shadowOffY = ovText.ShadowOffset[1] * scale;
                                    var startPos = ImGui.GetCursorPos();
                                    ImGui.SetCursorPosY(startPos.Y + shadowOffY);
                                    RenderRichTextLine(lines, shadowColVec, ovText.Alignment, maxLineWidth, lineRenderWidths, ovText.LetterSpacing, ovText.LineHeightOffset * scale, true, scale, ovText.FontSize, shadowOffX, windowWidth, ovText, animScaleXMult);
                                    ImGui.SetCursorPos(startPos);
                                }

                                RenderRichTextLine(lines, colorVec, ovText.Alignment, maxLineWidth, lineRenderWidths, ovText.LetterSpacing, ovText.LineHeightOffset * scale, false, scale, ovText.FontSize, 0f, windowWidth, ovText, animScaleXMult);
                                
                                ImGui.SetWindowFontScale(1.0f);
                                ovText.LastWidth = windowWidth;
                                ovText.LastHeight = windowHeight;
                            }
                        }
                        finally
                        {
                            ImGui.End();
                        }
                    }
                    finally
                    {
                        if (editMode)
                        {
                            ImGui.PopStyleVar(1);
                            ImGui.PopStyleColor(2);
                        }
                    }
                }
                finally
                {
                    ImGui.PopFont();
                }
            }

            var images = Main.Settings.OverlayerImages;
            for (int i = 0; i < images.Count; i++)
            {
                var ovImg = images[i];
                if (!ovImg.IsEnabled && !editMode) continue;
                
                ImGuiWindowFlags imgFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize;
                if (!editMode) imgFlags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoInputs;
                
                if (editMode)
                {
                    ImGui.PushStyleColor(ImGuiCol.WindowBg, new System.Numerics.Vector4(0f, 0f, 0f, 0f));
                    ImGui.PushStyleColor(ImGuiCol.Border, new System.Numerics.Vector4(0f, 0f, 0f, 0f));
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
                }
                try
                {
                    float topLeftX = ovImg.PositionX - ovImg.PivotX * ovImg.LastWidth;
                    float topLeftY = ovImg.PositionY - ovImg.PivotY * ovImg.LastHeight;
                    ImGui.SetNextWindowPos(new System.Numerics.Vector2(topLeftX, topLeftY), ImGuiCond.Always);
                    
                    bool isVisibleImg = ImGui.Begin($"CheryTools_OV_Img_{i}", imgFlags);
                    try
                    {
                        if (isVisibleImg)
                        {

                            IntPtr texPtr = TextureManager.GetOrCreateTexture(ovImg.ImagePath);
                            if (texPtr != IntPtr.Zero)
                            {
                                Texture2D tex = TextureManager.GetTextureByPtr(texPtr);
                                if (tex != null)
                                {
                                    float w = tex.width * ovImg.Scale;
                                    float h = tex.height * ovImg.Scale;
                                    
                                    var p = ImGui.GetCursorScreenPos();
                                    var drawList = ImGui.GetWindowDrawList();
                                    
                                    float rad = ovImg.Rotation * Mathf.Deg2Rad;
                                    float cos = Mathf.Cos(rad);
                                    float sin = Mathf.Sin(rad);
                                    
                                    float hw = w / 2f;
                                    float hh = h / 2f;
                                    
                                    float l1x = (-hw) * cos - (-hh) * sin; float l1y = (-hw) * sin + (-hh) * cos;
                                    float l2x = (hw) * cos - (-hh) * sin; float l2y = (hw) * sin + (-hh) * cos;
                                    float l3x = (hw) * cos - (hh) * sin; float l3y = (hw) * sin + (hh) * cos;
                                    float l4x = (-hw) * cos - (hh) * sin; float l4y = (-hw) * sin + (hh) * cos;

                                    float minXLocal = Mathf.Min(l1x, Mathf.Min(l2x, Mathf.Min(l3x, l4x)));
                                    float maxXLocal = Mathf.Max(l1x, Mathf.Max(l2x, Mathf.Max(l3x, l4x)));
                                    float minYLocal = Mathf.Min(l1y, Mathf.Min(l2y, Mathf.Min(l3y, l4y)));
                                    float maxYLocal = Mathf.Max(l1y, Mathf.Max(l2y, Mathf.Max(l3y, l4y)));

                                    float cx = p.X - minXLocal;
                                    float cy = p.Y - minYLocal;
                                    
                                    System.Numerics.Vector2 p1 = new System.Numerics.Vector2(cx + l1x, cy + l1y);
                                    System.Numerics.Vector2 p2 = new System.Numerics.Vector2(cx + l2x, cy + l2y);
                                    System.Numerics.Vector2 p3 = new System.Numerics.Vector2(cx + l3x, cy + l3y);
                                    System.Numerics.Vector2 p4 = new System.Numerics.Vector2(cx + l4x, cy + l4y);
                                    
                                    uint tint = ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1, 1, 1, ovImg.Opacity));
                                    
                                    drawList.AddImageQuad(texPtr, p1, p2, p3, p4, new System.Numerics.Vector2(0, 1), new System.Numerics.Vector2(1, 1), new System.Numerics.Vector2(1, 0), new System.Numerics.Vector2(0, 0), tint);
                                    
                                    float boundW = maxXLocal - minXLocal;
                                    float boundH = maxYLocal - minYLocal;
                                    ImGui.Dummy(new System.Numerics.Vector2(boundW, boundH));

                                    float currentTotalWidth = boundW + ImGui.GetStyle().WindowPadding.X * 2.0f;
                                    float currentTotalHeight = boundH + ImGui.GetStyle().WindowPadding.Y * 2.0f;

                                    ovImg.LastWidth = currentTotalWidth;
                                    ovImg.LastHeight = currentTotalHeight;

                                    if (editMode)
                                    {
                                        var newPos = ImGui.GetWindowPos();
                                        float calculatedPosX = newPos.X + ovImg.PivotX * currentTotalWidth;
                                        float calculatedPosY = newPos.Y + ovImg.PivotY * currentTotalHeight;
                                        if (Mathf.Abs(calculatedPosX - ovImg.PositionX) > 0.1f || Mathf.Abs(calculatedPosY - ovImg.PositionY) > 0.1f)
                                        {
                                            ovImg.PositionX = calculatedPosX;
                                            ovImg.PositionY = calculatedPosY;
                                            Main.RequestSave();
                                        }
                                        drawList.AddRect(new System.Numerics.Vector2(p.X, p.Y), new System.Numerics.Vector2(p.X + boundW, p.Y + boundH), 0xFF00FF00, 0f, ImDrawFlags.None, 2f);
                                    }
                                }
                            }
                            else
                            {
                                if (editMode)
                                {
                                    ImGui.Text("[图片丢失/未设置? 请在设置中配置路径]");
                                }
                            }
                        }
                    }
                    finally
                    {
                        ImGui.End();
                    }
                }
                finally
                {
                    if (editMode)
                    {
                        ImGui.PopStyleVar(1);
                        ImGui.PopStyleColor(2);
                    }
                }
            }
        }

        private float CalculateRichTextWidth(string currentLine, float letterSpacing, float baseFontSize, OverlayerText ovText, float animScaleXMult)
        {
            float totalWidth = 0f;
            var segments = RichTextParser.Parse(currentLine, new System.Numerics.Vector4(1,1,1,1));
            
            float safeBaseFontSize = (baseFontSize > 0) ? baseFontSize : 100f;

            foreach (var seg in segments)
            {
                float targetSize = baseFontSize;
                if (seg.HasSizeTag)
                {
                    if (seg.SizeValue > 0)
                    {
                        targetSize = seg.SizeValue;
                    }
                    else if (seg.SizeValue < 0)
                    {
                        targetSize = -seg.SizeValue * baseFontSize;
                    }
                }
                
                // Select font and base size for this segment
                bool hasCustomFont = !string.IsNullOrEmpty(ovText.FontPath) && ImGuiController.CustomFonts.ContainsKey(ovText.FontPath);
                bool hasCustomLargeFont = hasCustomFont && ImGuiController.CustomLargeFonts.ContainsKey(ovText.FontPath);
                
                ImFontPtr segFont;
                float segFontBaseSize;
                
                if (targetSize > 72f)
                {
                    if (hasCustomLargeFont)
                    {
                        segFont = ImGuiController.CustomLargeFonts[ovText.FontPath];
                        segFontBaseSize = 128.0f;
                    }
                    else if (hasCustomFont)
                    {
                        segFont = ImGuiController.CustomFonts[ovText.FontPath];
                        segFontBaseSize = 48.0f;
                    }
                    else
                    {
                        segFont = ImGuiController.DefaultLargeFont;
                        segFontBaseSize = 128.0f;
                    }
                }
                else
                {
                    if (hasCustomFont)
                    {
                        segFont = ImGuiController.CustomFonts[ovText.FontPath];
                        segFontBaseSize = 48.0f;
                    }
                    else
                    {
                        segFont = ImGuiController.DefaultHighResFont;
                        segFontBaseSize = 48.0f;
                    }
                }
                
                float segScale = (targetSize / segFontBaseSize) * animScaleXMult;
                
                ImGui.PushFont(segFont);
                try
                {
                    if (letterSpacing == 0f)
                    {
                        totalWidth += ImGui.CalcTextSize(seg.RenderText).X * segScale;
                    }
                    else
                    {
                        for (int i = 0; i < seg.RenderText.Length; i++)
                        {
                            totalWidth += ImGui.CalcTextSize(seg.RenderText[i].ToString()).X * segScale;
                            if (i < seg.RenderText.Length - 1) totalWidth += letterSpacing * segScale;
                        }
                    }
                }
                finally
                {
                    ImGui.PopFont();
                }
            }
            return totalWidth;
        }

        private float CalcRawTextWidth(string text, float letterSpacing)
        {
            if (letterSpacing == 0f) return ImGui.CalcTextSize(text).X;
            float w = 0;
            for (int i = 0; i < text.Length; i++)
            {
                w += ImGui.CalcTextSize(text[i].ToString()).X;
                if (i < text.Length - 1) w += letterSpacing;
            }
            return w;
        }

        private void RenderRawText(string text, float letterSpacing, System.Numerics.Vector4 color)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            try
            {
                if (letterSpacing == 0f)
                {
                    ImGui.TextUnformatted(text);
                    ImGui.SameLine(0, 0);
                    return;
                }
                for (int i = 0; i < text.Length; i++)
                {
                    ImGui.TextUnformatted(text[i].ToString());
                    if (i < text.Length - 1)
                    {
                        ImGui.SameLine(0, letterSpacing);
                    }
                }
                ImGui.SameLine(0, 0);
            }
            finally
            {
                ImGui.PopStyleColor();
            }
        }

        private void RenderRichTextLine(string[] lines, System.Numerics.Vector4 defaultColor, int alignment, float maxLineWidth, float[] lineRenderWidths, float letterSpacing, float lineHeightOffset, bool isShadow, float scale, float baseFontSize, float xOffset, float windowWidth, OverlayerText ovText, float animScaleXMult)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                string currentLine = lines[i];
                bool isFirstSegmentOnLine = true;
                
                float initialCursorY = ImGui.GetCursorPosY();

                float thisLineWidth = lineRenderWidths[i];
                float pad = ImGui.GetStyle().WindowPadding.X;

                float startX = pad + xOffset;

                if (alignment == 1) // Center
                {
                    startX = (windowWidth - thisLineWidth) / 2.0f + xOffset;
                }
                else if (alignment == 2) // Right
                {
                    startX = windowWidth - thisLineWidth - pad + xOffset;
                }

                ImGui.SetCursorPosX(startX);
                isFirstSegmentOnLine = true;

                var segments = RichTextParser.Parse(currentLine, defaultColor);
                float safeBaseFontSize = (baseFontSize > 0) ? baseFontSize : 100f;

                foreach (var seg in segments)
                {
                    if (!isFirstSegmentOnLine) ImGui.SameLine(0, 0);
                    
                    float targetSize = baseFontSize;
                    if (seg.HasSizeTag && seg.SizeValue > 0)
                    {
                        targetSize = seg.SizeValue;
                    }
                    else if (seg.HasSizeTag && seg.SizeValue < 0)
                    {
                        targetSize = -seg.SizeValue * baseFontSize;
                    }

                    // Select font and base size for this segment
                    bool hasCustomFont = !string.IsNullOrEmpty(ovText.FontPath) && ImGuiController.CustomFonts.ContainsKey(ovText.FontPath);
                    bool hasCustomLargeFont = hasCustomFont && ImGuiController.CustomLargeFonts.ContainsKey(ovText.FontPath);
                    
                    ImFontPtr segFont;
                    float segFontBaseSize;
                    
                    if (targetSize > 72f)
                    {
                        if (hasCustomLargeFont)
                        {
                            segFont = ImGuiController.CustomLargeFonts[ovText.FontPath];
                            segFontBaseSize = 128.0f;
                        }
                        else if (hasCustomFont)
                        {
                            segFont = ImGuiController.CustomFonts[ovText.FontPath];
                            segFontBaseSize = 48.0f;
                        }
                        else
                        {
                            segFont = ImGuiController.DefaultLargeFont;
                            segFontBaseSize = 128.0f;
                        }
                    }
                    else
                    {
                        if (hasCustomFont)
                        {
                            segFont = ImGuiController.CustomFonts[ovText.FontPath];
                            segFontBaseSize = 48.0f;
                        }
                        else
                        {
                            segFont = ImGuiController.DefaultHighResFont;
                            segFontBaseSize = 48.0f;
                        }
                    }

                    float segScale = (targetSize / segFontBaseSize) * animScaleXMult;

                    ImGui.PushFont(segFont);
                    try
                    {
                        ImGui.SetWindowFontScale(segScale);
                        System.Numerics.Vector4 c = isShadow ? defaultColor : seg.Color;
                        RenderRawText(seg.RenderText, letterSpacing, c);
                        isFirstSegmentOnLine = false;
                    }
                    finally
                    {
                        ImGui.PopFont();
                        ImGui.SetWindowFontScale(scale); // Restore window scale
                    }
                }
                
                ImGui.SetCursorPosY(initialCursorY + ImGui.GetFontSize() + lineHeightOffset);
                ImGui.SetCursorPosX(ImGui.GetStyle().WindowPadding.X);
                ImGui.Dummy(new System.Numerics.Vector2(0, 0));
            }
        }

        private System.Numerics.Vector4 ParseHexColor(string hex, System.Numerics.Vector4 fallback)
        {
            if (hex.Length == 6 || hex.Length == 8)
            {
                try
                {
                    float r = System.Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
                    float g = System.Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
                    float b = System.Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
                    float a = 1f;
                    if (hex.Length == 8)
                        a = System.Convert.ToInt32(hex.Substring(6, 2), 16) / 255f;
                    return new System.Numerics.Vector4(r, g, b, a);
                }
                catch { return fallback; }
            }
            return fallback;
        }
    }
}
