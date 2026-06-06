using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace CheryTools
{
    [Serializable]
    public class OverlayerImage
    {
        public bool IsEnabled = true;
        public string ImagePath = "";
        public float PositionX = 200f;
        public float PositionY = 200f;
        public float Scale = 1.0f;
        public float Rotation = 0f;
        public float Opacity = 1.0f;

        public float PivotX = 0f;
        public float PivotY = 0f;

        [System.Xml.Serialization.XmlIgnore]
        public float LastWidth = 100f;
        [System.Xml.Serialization.XmlIgnore]
        public float LastHeight = 100f;
    }

    [Serializable]
    public class KVNode
    {
        public int NodeType = 0; // 0 = Normal Key, 1 = KPS Box, 2 = Total Box, 3 = Background Image Key
        public string KeyBind = "None";
        public string CustomText = "";
        public string ImagePath = "";
        public bool IsUnselectable = false;
        public float Opacity = 1.0f;
        
        public float PositionX = 0f;
        public float PositionY = 0f;
        
        public float Width = 50f;
        public float Height = 50f;
        public float BorderThickness = -1f;
        
        public float Scale = 1f;
        public float TextOffsetY = 0f;
        public float TextOffsetX = 0f;
        public float TextScale = 1f;
        public float CountOffsetY = 0f;
        public float CountOffsetX = 0f;
        public float CountScale = 1f;
        public string KeyFontPath = "";
        public string CountFontPath = "";
        
        public bool UseCustomColor = false;
        public float[] ColorBgNormal = new float[] { 0.2f, 0.2f, 0.2f, 0.8f };
        public float[] ColorBgPressed = new float[] { 0.8f, 0.8f, 0.8f, 0.8f };
        public float[] ColorBorderNormal = new float[] { 0.4f, 0.4f, 0.4f, 1.0f };
        public float[] ColorBorderPressed = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
        public float[] ColorTextNormal = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
        public float[] ColorTextPressed = new float[] { 0.0f, 0.0f, 0.0f, 1.0f };

        public int RainRow = 0;
        public bool UseCustomRain = false;
        public float[] RainColor = new float[] { 0.8f, 0.5f, 1.0f, 0.8f };
        public float RainWidthRatio = 0.8f;
        public float RainYOffset = 0f;

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
    public class OverlayerText
    {
        public string Name = "新模块";
        public bool IsEnabled = true;
        public string TextFormat = "<color=#FF0000FF>{te} </color><color=#FF8E00FF>{ve} </color><color=#D7FF27FF>{ep}</color><color=#30FF20FF> {p}</color> <color=#D7FF27FF>{lp}</color> <color=#FF8E00FF>{vl} </color><color=#FF0000FF>{tl}</color>";
        public float PositionX = 50f;
        public float PositionY = 50f;
        public float FontSize = 32f;
        public float[] TextColor = new float[] { 1f, 1f, 1f, 1f };
        public int Alignment = 0; // 0: Left, 1: Center, 2: Right
        public string FontPath = ""; // 字体文件绝对路径

        public bool EnableShadow = false;
        public float[] ShadowColor = new float[] { 0f, 0f, 0f, 1f };
        public float[] ShadowOffset = new float[] { 2f, 2f };
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

    public class Settings : UnityModManager.ModSettings
    {
        public bool OverlayerSystemEnabled = false;
        public bool OverlayerEditMode = false;
        public bool OverlayerOnlyShowPlaying = false;
        public System.Collections.Generic.List<OverlayerText> OverlayerTexts = new System.Collections.Generic.List<OverlayerText>();
        public System.Collections.Generic.List<OverlayerImage> OverlayerImages = new System.Collections.Generic.List<OverlayerImage>();
        
        public bool EnableLegacyPauseFix = true;

        // Visual Settings
        public bool EnableCustomPlanetColors = false;

        public float[] RedPlanetColor = new float[] { 1f, 0f, 0f, 1f };
        public float[] RedTailColor = new float[] { 1f, 0f, 0f, 1f };

        public float[] BluePlanetColor = new float[] { 0f, 0f, 1f, 1f };
        public float[] BlueTailColor = new float[] { 0f, 0f, 1f, 1f };

        public float[] GreenPlanetColor = new float[] { 0f, 1f, 0f, 1f };
        public float[] GreenTailColor = new float[] { 0f, 1f, 0f, 1f };

        public bool HideNativeLevelName = false;

        public float[] ComboColor = new float[4] { 1f, 1f, 1f, 1f };
        public float[] AccuracyColor = new float[4] { 1f, 1f, 1f, 1f };
        
        public string LevelNameFont = "";
        
        public KeyCode ToggleMenuKey = KeyCode.Insert;

        // KeyViewer Settings
        public bool EnableKeyViewer = true;
        public bool LimitInput = false;
        public bool KeyViewerOnlyShowPlaying = false;
        
        public int KeyViewerLayoutTab = 0; // 0=16Key, 1=12Key, 2=8Key, 3=4Key
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

        // KeyRain Settings
        public bool EnableKeyRain = false;
        public float KeyRainSpeed = 800.0f;
        public float KeyRainMaxHeight = 400.0f;
        public int KeyRainFadeMode = 1; // 0=Clip, 1=Fade
        public float KeyRainWidthRatio1 = 0.8f;
        public float KeyRainWidthRatio2 = 0.4f;
        public float KeyRainYOffsetRow1 = 0.0f;
        public float KeyRainYOffsetRow2 = 0.0f;
        public float[] KeyRainColorRow1 = new float[] { 0.8f, 0.5f, 1.0f, 0.8f };
        public float[] KeyRainColorRow2 = new float[] { 0.5f, 0.8f, 1.0f, 0.8f };

        public string[] KeyBindings = new string[16] { 
            "Tab", "Alpha1", "Alpha2", "E", "P", "Equals", "Backspace", "Backslash",
            "UpArrow", "LeftShift", "C", "Space", "Comma", "Period", "Return", "H" 
        };

        

        public int[] HitCounts = new int[16];
        public int TotalHits = 0;

        public string KeyViewerFontPath = "";
        
        public float GlobalTextOffsetX = 0f;
        public float GlobalTextOffsetY = 0f;
        public float GlobalCountOffsetX = 0f;
        public float GlobalCountOffsetY = 0f;
        
        public float KeyViewerDefaultWidth = 50f;
        public float KeyViewerDefaultHeight = 50f;
        
        public System.Collections.Generic.List<KVNode> Layout16K;
        public System.Collections.Generic.List<KVNode> Layout12K;
        public System.Collections.Generic.List<KVNode> Layout10K;
        public System.Collections.Generic.List<KVNode> Layout8K;

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
            if (KeyBindings == null || KeyBindings.Length != 16)
                KeyBindings = new string[16] { "Tab", "Alpha1", "Alpha2", "E", "P", "Equals", "Backspace", "Backslash", "UpArrow", "LeftShift", "C", "Space", "Comma", "Period", "Return", "H" };
            if (HitCounts == null || HitCounts.Length != 16)
                HitCounts = new int[16];
            
            if (GreenTailColor == null || GreenTailColor.Length != 4) GreenTailColor = new float[] { 0f, 1f, 0f, 1f };
            if (RedTailColor == null || RedTailColor.Length != 4) RedTailColor = new float[] { 1f, 0f, 0f, 1f };
            if (KeyViewerColorBgNormal == null || KeyViewerColorBgNormal.Length != 4) KeyViewerColorBgNormal = new float[] { 0.0f, 0.0f, 0.0f, 0.6f };
            if (KeyViewerColorBgPressed == null || KeyViewerColorBgPressed.Length != 4) KeyViewerColorBgPressed = new float[] { 0.2f, 0.6f, 1.0f, 0.8f };
            if (KeyViewerColorBorderNormal == null || KeyViewerColorBorderNormal.Length != 4) KeyViewerColorBorderNormal = new float[] { 0.5f, 0.5f, 0.5f, 0.5f };
            if (KeyViewerColorBorderPressed == null || KeyViewerColorBorderPressed.Length != 4) KeyViewerColorBorderPressed = new float[] { 0.8f, 0.9f, 1.0f, 0.8f };
            if (KeyViewerColorTextNormal == null || KeyViewerColorTextNormal.Length != 4) KeyViewerColorTextNormal = new float[] { 1.0f, 1.0f, 1.0f, 0.8f };
            
            if (OverlayerTexts != null)
            {
                foreach (var t in OverlayerTexts)
                {
                    if (t.TextColor == null || t.TextColor.Length != 4) t.TextColor = new float[] { 1f, 1f, 1f, 1f };
                    if (t.Animations != null)
                    {
                        foreach (var anim in t.Animations)
                        {
                            anim.ParseJson();
                        }
                    }
                }
            }
            if (KeyViewerColorTextPressed == null || KeyViewerColorTextPressed.Length != 4) KeyViewerColorTextPressed = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
            if (KeyRainColorRow1 == null || KeyRainColorRow1.Length != 4) KeyRainColorRow1 = new float[] { 0.8f, 0.5f, 1.0f, 0.8f };
            if (KeyRainColorRow2 == null || KeyRainColorRow2.Length != 4) KeyRainColorRow2 = new float[] { 0.5f, 0.8f, 1.0f, 0.8f };

            if (Layout16K == null || Layout16K.Count == 0) Layout16K = GenerateDefaultKVLayout(16);
            if (Layout12K == null || Layout12K.Count == 0) Layout12K = GenerateDefaultKVLayout(12);
            if (Layout10K == null || Layout10K.Count == 0) Layout10K = GenerateDefaultKVLayout(10);
            if (Layout8K == null || Layout8K.Count == 0) Layout8K = GenerateDefaultKVLayout(8);

            if (OverlayerTexts == null) OverlayerTexts = new System.Collections.Generic.List<OverlayerText>();
            if (OverlayerTexts.Count == 0)
            {
                OverlayerTexts.Add(new OverlayerText());
            }

            foreach (var txt in OverlayerTexts)
            {
                if (string.IsNullOrEmpty(txt.Name))
                    txt.Name = "新模块";
                if (txt.TextColor == null || txt.TextColor.Length != 4)
                    txt.TextColor = new float[] { 1f, 1f, 1f, 1f };
            }

            var allNodes = new System.Collections.Generic.List<KVNode>();
            if (Layout16K != null) allNodes.AddRange(Layout16K);
            if (Layout12K != null) allNodes.AddRange(Layout12K);
            if (Layout10K != null) allNodes.AddRange(Layout10K);
            if (Layout8K != null) allNodes.AddRange(Layout8K);

            foreach (var node in allNodes)
            {
                if (node.RainRow == 0)
                {
                    node.RainRow = (node.PositionY < 80f) ? 1 : 2;
                }
                if (node.RainColor == null || node.RainColor.Length != 4)
                {
                    node.RainColor = (node.RainRow == 1) ? new float[] { 0.8f, 0.5f, 1.0f, 0.8f } : new float[] { 0.5f, 0.8f, 1.0f, 0.8f };
                }
                if (node.RainWidthRatio <= 0.01f)
                {
                    node.RainWidthRatio = 0.8f;
                }
            }
        }

        public System.Collections.Generic.List<KVNode> GenerateDefaultKVLayout(int count)
        {
            var list = new System.Collections.Generic.List<KVNode>();
            int rows = count > 8 ? 2 : 1;
            float padding = 4f;
            float boxWidth = KeyViewerDefaultWidth;
            float boxHeight = KeyViewerDefaultHeight;
            float startX = 20f;
            float startY = 50f;

            for (int r = 0; r < rows; r++)
            {
                int cols = (r == 1) ? (count - 8) : System.Math.Min(count, 8);
                for (int c = 0; c < cols; c++)
                {
                    int index = r * 8 + c;
                    string bind = (KeyBindings != null && index < KeyBindings.Length) ? KeyBindings[index] : "None";
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
        }
        
        private static UnityEngine.GameObject _imguiGameObject;

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            Logger = modEntry.Logger;
            ModEntry = modEntry;
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            Settings.InitNulls();
            
            // Allow native DllImport to find cimgui.dll in the mod folder
            SetDllDirectory(modEntry.Path);

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

                    controller.OnLayout += _imguiGameObject.GetComponent<CheryToolsMenu>().RenderUI;
                    controller.OnLayout += _imguiGameObject.GetComponent<KeyViewerOverlay>().RenderUI;
                    controller.OnLayout += _imguiGameObject.GetComponent<OverlayerManager>().RenderUI;
                    Logger.Log("ImGuiController, CheryToolsMenu, KeyViewer, Overlayer components added to GameObject.");
                }
                
                InputInterceptor.UpdateAllowedKeys();
            }
            else
            {
                harmony.UnpatchAll(modEntry.Info.Id);
                
                if (_imguiGameObject != null)
                {
                    UnityEngine.GameObject.Destroy(_imguiGameObject);
                    _imguiGameObject = null;
                }
                TextureManager.Clear();
            }
            return true;
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
    }

    [HarmonyPatch(typeof(ADOFAI.LevelData), "Decode")]
    public static class LevelData_Decode_Patch
    {
        public static void Postfix(ADOFAI.LevelData __instance, System.Collections.Generic.Dictionary<string, object> dict)
        {
            if (Main.IsEnabled && Main.Settings.EnableLegacyPauseFix && __instance != null)
            {
                __instance.legacyPause = true;

                if (dict != null && dict.TryGetValue("actions", out object actionsObj) && actionsObj is System.Collections.Generic.List<object> actionsList)
                {
                    foreach (var ev in __instance.levelEvents)
                    {
                        if (ev.eventType == ADOFAI.LevelEventType.Pause)
                        {
                            if (IsTurnaround(__instance.angleData, ev.floor))
                            {
                                float originalDuration = GetOriginalDuration(actionsList, ev.floor) ?? (ev.GetNullable<float>("duration") ?? 0f);
                                ev["duration"] = Math.Max(0f, originalDuration - 1f);
                            }
                        }
                    }
                }
            }
        }

        private static float? GetOriginalDuration(System.Collections.Generic.List<object> actionsList, int floor)
        {
            foreach (var obj in actionsList)
            {
                if (obj is System.Collections.Generic.Dictionary<string, object> actionDict)
                {
                    if (actionDict.TryGetValue("floor", out object fObj) && Convert.ToInt32(fObj) == floor)
                    {
                        if (actionDict.TryGetValue("eventType", out object eObj) && eObj.ToString() == "Pause")
                        {
                            if (actionDict.TryGetValue("duration", out object dObj))
                            {
                                return Convert.ToSingle(dObj);
                            }
                        }
                    }
                }
            }
            return null;
        }

        private static bool IsTurnaround(System.Collections.Generic.List<float> angleData, int floor)
        {
            if (angleData == null || floor <= 0 || floor >= angleData.Count) return false;
            
            int prevIndex = floor - 1;
            while (prevIndex >= 0 && angleData[prevIndex] == 999f) prevIndex--;
            float prevAngle = (prevIndex >= 0) ? angleData[prevIndex] : 0f;
            
            float currAngle = angleData[floor];
            if (currAngle == 999f) return false;
            
            float diff = Math.Abs(currAngle - prevAngle) % 360f;
            return Math.Abs(diff - 180f) < 0.1f;
        }
    }
}
