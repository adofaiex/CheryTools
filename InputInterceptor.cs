using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CheryTools
{
    public static class InputInterceptor
    {
        private const float DefaultAntiBounceIntervalSeconds = 0.05f;
        private static HashSet<string> _allowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> _lastWentDownTimes = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int> _lastWentDownFrames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static bool _filteringEnabled;
        private static bool _inputPatchesApplied;

        public static void UpdateAllowedKeys()
        {
            _allowedKeys.Clear();
            bool shouldLimitInput = Main.IsEnabled
                && Main.Settings != null
                && (Main.Settings.ToolsLimitInput || (Main.Settings.EnableKeyViewer && Main.Settings.LimitInput));
            bool shouldAntiBounce = Main.IsEnabled
                && Main.Settings != null
                && Main.Settings.ToolsAntiBounceKeys;
            
            // Default whitelist
            _allowedKeys.Add("Escape");
            _allowedKeys.Add("Esc");
            _allowedKeys.Add("LeftControl");
            _allowedKeys.Add("RightControl");
            _allowedKeys.Add("F10");

            Action<string> addKeyWithAliases = (k) => {
                if (string.IsNullOrEmpty(k)) return;
                _allowedKeys.Add(k);

                if (k.Equals("Escape", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("Esc");
                if (k.Equals("Esc", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("Escape");
                
                if (k.Equals("Equals", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("Equal");
                if (k.Equals("Equal", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("Equals");

                if (k.Equals("Return", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("Enter");
                if (k.Equals("Enter", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("Return");

                if (k.Equals("LeftShift", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("LShift");
                if (k.Equals("LShift", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("LeftShift");

                if (k.Equals("RightShift", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("RShift");
                if (k.Equals("RShift", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("RightShift");

                if (k.Equals("LeftControl", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("LControl");
                if (k.Equals("LControl", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("LeftControl");

                if (k.Equals("RightControl", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("RControl");
                if (k.Equals("RControl", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("RightControl");

                if (k.Equals("LeftAlt", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("LAlt");
                if (k.Equals("LAlt", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("LeftAlt");

                if (k.Equals("RightAlt", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("RAlt");
                if (k.Equals("RAlt", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("RightAlt");

                if (k.Equals("LeftCommand", StringComparison.OrdinalIgnoreCase) || 
                    k.Equals("LeftWindows", StringComparison.OrdinalIgnoreCase) || 
                    k.Equals("RightWindows", StringComparison.OrdinalIgnoreCase) || 
                    k.Equals("RightCommand", StringComparison.OrdinalIgnoreCase))
                {
                    _allowedKeys.Add("Super");
                }
                if (k.Equals("Super", StringComparison.OrdinalIgnoreCase))
                {
                    _allowedKeys.Add("LeftCommand");
                    _allowedKeys.Add("LeftWindows");
                    _allowedKeys.Add("RightWindows");
                    _allowedKeys.Add("RightCommand");
                }

                if (k.Equals("UpArrow", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("ArrowUp");
                if (k.Equals("ArrowUp", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("UpArrow");

                if (k.Equals("DownArrow", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("ArrowDown");
                if (k.Equals("ArrowDown", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("DownArrow");

                if (k.Equals("LeftArrow", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("ArrowLeft");
                if (k.Equals("ArrowLeft", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("LeftArrow");

                if (k.Equals("RightArrow", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("ArrowRight");
                if (k.Equals("ArrowRight", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("RightArrow");

                if (k.Equals("Period", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("Dot");
                if (k.Equals("Dot", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("Period");

                if (k.Equals("Quote", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("Apostrophe");
                if (k.Equals("Apostrophe", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("Quote");

                if (k.Equals("LeftBracket", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("LeftBrace");
                if (k.Equals("LeftBrace", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("LeftBracket");

                if (k.Equals("RightBracket", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("RightBrace");
                if (k.Equals("RightBrace", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("RightBracket");

                if (k.Equals("BackQuote", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("Grave");
                if (k.Equals("Grave", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("BackQuote");

                if (k.Equals("Pause", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("PauseBreak");
                if (k.Equals("PauseBreak", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("Pause");

                if (k.Equals("KeypadDivide", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("KeypadSlash");
                if (k.Equals("KeypadSlash", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("KeypadDivide");

                if (k.Equals("KeypadMultiply", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("KeypadAsterisk");
                if (k.Equals("KeypadAsterisk", StringComparison.OrdinalIgnoreCase)) _allowedKeys.Add("KeypadMultiply");
            };

            // Whitelist toggle menu key
            if (Main.Settings != null)
            {
                addKeyWithAliases(Main.Settings.ToggleMenuKey.ToString());

                if (Main.Settings.ToolsLimitInput)
                {
                    if (Main.Settings.ToolsLimitedKeys != null)
                    {
                        int count = Math.Min(30, Main.Settings.ToolsLimitedKeys.Count);
                        for (int i = 0; i < count; i++)
                        {
                            addKeyWithAliases(Main.Settings.ToolsLimitedKeys[i].ToString());
                        }
                    }
                }
                else if (Main.Settings.EnableKeyViewer && Main.Settings.LimitInput && Main.Settings.KeyViewerConfigurations != null)
                {
                    addKeyWithAliases("Space");
                    foreach (KVConfiguration config in Main.Settings.KeyViewerConfigurations)
                    {
                        if (config == null || !config.IsEnabled || config.Nodes == null) continue;
                        foreach (KVNode node in config.Nodes)
                        {
                            if (node == null || node.NodeType != 0) continue;
                            addKeyWithAliases(node.KeyBind);
                        }
                    }
                }
            }
            else
            {
                _allowedKeys.Add("Insert");
            }

            _filteringEnabled = shouldLimitInput;
            if (!shouldAntiBounce)
            {
                _lastWentDownTimes.Clear();
                _lastWentDownFrames.Clear();
            }
            UpdateInputPatches(shouldLimitInput || shouldAntiBounce);
        }

        public static void ResetPatches()
        {
            _filteringEnabled = false;
            _inputPatchesApplied = false;
            _lastWentDownTimes.Clear();
            _lastWentDownFrames.Clear();
        }

        private static void UpdateInputPatches(bool shouldPatch)
        {
            if (Main.harmony == null || shouldPatch == _inputPatchesApplied)
            {
                return;
            }

            MethodInfo keyboardMethod = AccessTools.Method(typeof(RDInputType_Keyboard), "MainIgnoreActive");
            MethodInfo asyncKeyboardMethod = AccessTools.Method(typeof(RDInputType_AsyncKeyboard), "Main");
            MethodInfo keyboardPostfix = AccessTools.Method(typeof(Patch_RDInputType_Keyboard_MainIgnoreActive), nameof(Patch_RDInputType_Keyboard_MainIgnoreActive.Postfix));
            MethodInfo asyncKeyboardPostfix = AccessTools.Method(typeof(Patch_RDInputType_AsyncKeyboard_Main), nameof(Patch_RDInputType_AsyncKeyboard_Main.Postfix));
            if (keyboardMethod == null || asyncKeyboardMethod == null || keyboardPostfix == null || asyncKeyboardPostfix == null)
            {
                return;
            }

            if (shouldPatch)
            {
                Main.harmony.Patch(keyboardMethod, postfix: new HarmonyMethod(keyboardPostfix));
                Main.harmony.Patch(asyncKeyboardMethod, postfix: new HarmonyMethod(asyncKeyboardPostfix));
            }
            else
            {
                Main.harmony.Unpatch(keyboardMethod, keyboardPostfix);
                Main.harmony.Unpatch(asyncKeyboardMethod, asyncKeyboardPostfix);
            }
            _inputPatchesApplied = shouldPatch;
        }

        public static bool IsKeyAllowed(AnyKeyCode anyKey)
        {
            string keyName = GetKeyName(anyKey);
            if (string.IsNullOrEmpty(keyName)) return false;
            return _allowedKeys.Contains(keyName);
        }

        public static void FilterInputState(RDInputType instance, ButtonState state, ref int result)
        {
            if (!Main.IsEnabled || Main.Settings == null) return;

            bool shouldLimitInput = _filteringEnabled
                && (Main.Settings.ToolsLimitInput || (Main.Settings.EnableKeyViewer && Main.Settings.LimitInput));
            bool shouldAntiBounce = Main.Settings.ToolsAntiBounceKeys;
            if (!shouldLimitInput && !shouldAntiBounce) return;

            RDInputType.MainStateCount stateCount = null;
            switch (state)
            {
                case ButtonState.WentDown: stateCount = instance.pressCount; break;
                case ButtonState.IsDown: stateCount = instance.heldCount; break;
                case ButtonState.WentUp: stateCount = instance.releaseCount; break;
                case ButtonState.IsUp: stateCount = instance.isReleaseCount; break;
            }

            if (stateCount != null && stateCount.keys != null)
            {
                int removed = 0;
                if (shouldLimitInput)
                {
                    removed += stateCount.keys.RemoveAll(k => !IsKeyAllowed(k));
                }
                if (shouldAntiBounce && state == ButtonState.WentDown)
                {
                    removed += stateCount.keys.RemoveAll(IsBouncedWentDown);
                }
                if (removed > 0)
                {
                    result = stateCount.keys.Count;
                }
            }
        }

        private static bool IsBouncedWentDown(AnyKeyCode anyKey)
        {
            string keyName = GetKeyName(anyKey);
            if (string.IsNullOrEmpty(keyName)) return false;

            float now = Time.unscaledTime;
            int frame = Time.frameCount;
            float interval = GetAntiBounceIntervalSeconds();
            if (_lastWentDownTimes.TryGetValue(keyName, out float lastTime) && now - lastTime < interval)
            {
                if (!_lastWentDownFrames.TryGetValue(keyName, out int lastFrame) || lastFrame != frame)
                {
                    return true;
                }
                return false;
            }

            _lastWentDownTimes[keyName] = now;
            _lastWentDownFrames[keyName] = frame;
            return false;
        }

        private static float GetAntiBounceIntervalSeconds()
        {
            if (Main.Settings == null)
            {
                return DefaultAntiBounceIntervalSeconds;
            }

            float intervalMs = Main.Settings.ToolsAntiBounceIntervalMs;
            if (intervalMs <= 0f || float.IsNaN(intervalMs) || float.IsInfinity(intervalMs))
            {
                intervalMs = 50f;
            }
            intervalMs = Math.Max(1f, Math.Min(500f, intervalMs));
            return intervalMs / 1000f;
        }

        private static string GetKeyName(AnyKeyCode anyKey)
        {
            if (anyKey.value is KeyCode kc)
            {
                return kc.ToString();
            }
            if (anyKey.value is AsyncKeyCode akc)
            {
                return akc.label.ToString();
            }
            return null;
        }
    }

    public static class Patch_RDInputType_Keyboard_MainIgnoreActive
    {
        public static void Postfix(RDInputType_Keyboard __instance, ButtonState state, ref int __result)
        {
            InputInterceptor.FilterInputState(__instance, state, ref __result);
        }
    }

    public static class Patch_RDInputType_AsyncKeyboard_Main
    {
        public static void Postfix(RDInputType_AsyncKeyboard __instance, ButtonState state, ref int __result)
        {
            InputInterceptor.FilterInputState(__instance, state, ref __result);
        }
    }
}
