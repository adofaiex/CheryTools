using System;
using System.Collections.Generic;
using HarmonyLib;
using SkyHook;
using UnityEngine;

namespace CheryTools.Tools.Preview
{
    [HarmonyPatch(typeof(scrController), nameof(scrController.TogglePauseGame))]
    internal static class TogglePauseGamePatch
    {
        private static bool Prefix(scrController __instance, ref bool __result)
        {
            if (!ToolsRuntime.IsActive || !ToolsRuntime.Settings.DisableAutoplaySpacePause ||
                !ADOBase.isLevelEditor || !RDC.auto || !Input.GetKeyDown(KeyCode.Space)) return true;
            __result = __instance != null && __instance.paused;
            return false;
        }
    }

    [HarmonyPatch(typeof(scnEditor), nameof(scnEditor.ZoomCamera), new[] { typeof(float), typeof(bool), typeof(bool) })]
    internal static class EditorZoomCameraPatch
    {
        private static bool Prefix(scnEditor __instance, float delta)
        {
            return !ToolsRuntime.IsActive || !ToolsRuntime.Settings.DisablePlayModeScrollZoom ||
                !ADOBase.isLevelEditor || !EditorRuntimeCompatibility.IsPlayMode(__instance) ||
                !EditorRuntimeCompatibility.IsMouseWheelZoom(delta);
        }
    }

    [HarmonyPatch(typeof(scrHitTextManager), "ShowHitText")]
    internal static class HitTextPatch
    {
        private static bool Prefix(HitMargin hitMargin) => !ToolsVisualRuntime.ShouldHideHitText(hitMargin);
    }

    [HarmonyPatch(typeof(scrUIController), "Update")]
    internal static class ScrUiControllerUpdatePatch
    {
        public static void Postfix()
        {
            ToolsUiVisibilityRuntime.RefreshPersistentGameUi();
        }
    }

    [HarmonyPatch(typeof(scrController), "Update")]
    internal static class ScrControllerUpdatePatch
    {
        public static void Postfix()
        {
            ToolsUiVisibilityRuntime.RefreshPersistentGameUi();
        }
    }

    [HarmonyPatch(typeof(scrShowIfDebug), "Update")]
    internal static class ScrShowIfDebugUpdatePatch
    {
        public static void Postfix(scrShowIfDebug __instance)
        {
            ToolsUiVisibilityRuntime.RefreshAutoplayStatusText(__instance);
        }
    }

    [HarmonyPatch(typeof(scrMissIndicator), "Awake")]
    internal static class ScrMissIndicatorAwakePatch
    {
        public static void Postfix(scrMissIndicator __instance)
        {
            ToolsUiVisibilityRuntime.OnMissIndicatorSpawned(__instance);
        }
    }

    [HarmonyPatch(typeof(scrEnableIfBeta), "Awake")]
    internal static class ScrEnableIfBetaAwakePatch
    {
        public static void Postfix(scrEnableIfBeta __instance)
        {
            ToolsUiVisibilityRuntime.OnBetaIndicatorSpawned(__instance);
        }
    }

    [HarmonyPatch(typeof(scrController), "OnLandOnPortal")]
    internal static class ScrControllerLastFloorFlashPatch
    {
        public static void Postfix(Portal portalDestination)
        {
            // OnLandOnPortal is the game's completion path and starts the
            // white end-of-level flash near the beginning of the method. Kill
            // only that newly started flash after the method returns. The
            // same method also handles ordinary portal travel, which must keep
            // its flash effect.
            if (portalDestination == Portal.EndOfLevel &&
                ToolsUiVisibilityRuntime.ShouldHideLastFloorFlash())
                scrFlash.FlashKill();
        }
    }

    [HarmonyPatch(typeof(PlanetRenderer), "LoadPlanetColor")]
    internal static class PlanetRendererColorPatch
    {
        private static void Postfix() => ToolsVisualRuntime.ApplyCustomColors();
    }

    [HarmonyPatch(typeof(PlanetarySystem), "LoadPlanetColors")]
    internal static class PlanetarySystemColorPatch
    {
        private static void Postfix() => ToolsVisualRuntime.ApplyCustomColors();
    }

    [HarmonyPatch(typeof(PlanetarySystem), "ApplyMultiplanetColors")]
    internal static class MultiplanetColorPatch
    {
        private static void Postfix() => ToolsVisualRuntime.ApplyCustomColors();
    }

    [HarmonyPatch(typeof(RDInputType_Keyboard), "MainIgnoreActive")]
    internal static class KeyboardInputPatch
    {
        private static void Postfix(RDInputType_Keyboard __instance, ButtonState state, ref int __result)
        {
            ToolsInputRuntime.Filter(__instance, state, ref __result);
        }
    }

    [HarmonyPatch(typeof(RDInputType_AsyncKeyboard), "Main")]
    internal static class AsyncKeyboardInputPatch
    {
        private static void Postfix(RDInputType_AsyncKeyboard __instance, ButtonState state, ref int __result)
        {
            ToolsInputRuntime.Filter(__instance, state, ref __result);
        }
    }

    // Runs inside the game's key-state queries, which happen many times per frame on the judgement
    // path. Key names (Enum.ToString is slow and allocates) are computed once per key value and the
    // allow decision is cached, so steady-state filtering performs no allocation.
    internal static class ToolsInputRuntime
    {
        private static readonly HashSet<string> Allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> LastDownTime = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int> LastDownFrame = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<KeyCode, string> KeyCodeNames = new Dictionary<KeyCode, string>();
        private static readonly Dictionary<KeyLabel, string> AsyncLabelNames = new Dictionary<KeyLabel, string>();
        private static readonly Dictionary<KeyCode, bool> KeyCodeAllowed = new Dictionary<KeyCode, bool>();
        private static readonly Dictionary<KeyLabel, bool> AsyncLabelAllowed = new Dictionary<KeyLabel, bool>();
        private static readonly Predicate<AnyKeyCode> NotAllowedPredicate = key => !IsAllowed(key);
        private static readonly Predicate<AnyKeyCode> BouncedPredicate = IsBounced;

        internal static void Rebuild()
        {
            Allowed.Clear();
            KeyCodeAllowed.Clear();
            AsyncLabelAllowed.Clear();
            AddWithAliases("Insert");
            AddWithAliases("Escape");
            AddWithAliases("LeftControl");
            AddWithAliases("RightControl");
            AddWithAliases("F10");
            List<KeyCode> keys = ToolsRuntime.Settings?.LimitedKeys;
            if (keys != null)
                for (int i = 0; i < Math.Min(ToolsSettings.MaxLimitedKeys, keys.Count); i++) AddWithAliases(keys[i].ToString());
            if (ToolsRuntime.Settings == null || !ToolsRuntime.Settings.AntiBounceKeys)
            {
                LastDownTime.Clear();
                LastDownFrame.Clear();
            }
        }

        internal static void Filter(RDInputType instance, ButtonState state, ref int result)
        {
            if (!ToolsRuntime.IsActive || instance == null || ToolsRuntime.Settings == null) return;
            bool limit = ToolsRuntime.Settings.LimitInput && Allowed.Count > 5;
            bool antiBounce = ToolsRuntime.Settings.AntiBounceKeys;
            if (!limit && !antiBounce) return;
            RDInputType.MainStateCount stateCount = null;
            switch (state)
            {
                case ButtonState.WentDown: stateCount = instance.pressCount; break;
                case ButtonState.IsDown: stateCount = instance.heldCount; break;
                case ButtonState.WentUp: stateCount = instance.releaseCount; break;
                case ButtonState.IsUp: stateCount = instance.isReleaseCount; break;
            }
            if (stateCount?.keys == null) return;
            int removed = 0;
            if (stateCount.keys.Count == 0) return;
            if (limit) removed += stateCount.keys.RemoveAll(NotAllowedPredicate);
            if (antiBounce && state == ButtonState.WentDown) removed += stateCount.keys.RemoveAll(BouncedPredicate);
            if (removed > 0) result = stateCount.keys.Count;
        }

        internal static void Reset()
        {
            Allowed.Clear();
            KeyCodeAllowed.Clear();
            AsyncLabelAllowed.Clear();
            LastDownTime.Clear();
            LastDownFrame.Clear();
        }

        private static bool IsAllowed(AnyKeyCode key)
        {
            object value = key.value;
            if (value is KeyCode code)
            {
                if (!KeyCodeAllowed.TryGetValue(code, out bool allowed))
                {
                    allowed = Allowed.Contains(GetName(key));
                    KeyCodeAllowed[code] = allowed;
                }
                return allowed;
            }

            if (value is AsyncKeyCode asyncCode)
            {
                if (!AsyncLabelAllowed.TryGetValue(asyncCode.label, out bool allowed))
                {
                    allowed = Allowed.Contains(GetName(key));
                    AsyncLabelAllowed[asyncCode.label] = allowed;
                }
                return allowed;
            }

            return Allowed.Contains(string.Empty);
        }

        private static bool IsBounced(AnyKeyCode key)
        {
            string name = GetName(key);
            if (string.IsNullOrEmpty(name)) return false;
            float interval = Mathf.Clamp(ToolsRuntime.Settings.AntiBounceIntervalMs, 1f, 100f) / 1000f;
            float now = Time.unscaledTime;
            int frame = Time.frameCount;
            if (LastDownTime.TryGetValue(name, out float previous) && now - previous < interval)
            {
                if (!LastDownFrame.TryGetValue(name, out int previousFrame) || previousFrame != frame)
                {
                    return true;
                }
                return false;
            }
            LastDownTime[name] = now;
            LastDownFrame[name] = frame;
            return false;
        }

        private static string GetName(AnyKeyCode key)
        {
            if (key.value is KeyCode code)
            {
                if (!KeyCodeNames.TryGetValue(code, out string name))
                {
                    name = code.ToString();
                    KeyCodeNames[code] = name;
                }
                return name;
            }

            if (key.value is AsyncKeyCode asyncCode)
            {
                if (!AsyncLabelNames.TryGetValue(asyncCode.label, out string name))
                {
                    name = asyncCode.label.ToString();
                    AsyncLabelNames[asyncCode.label] = name;
                }
                return name;
            }

            return string.Empty;
        }

        // Unity KeyCode name -> async input (SkyHook KeyLabel) name, for every key whose names
        // differ. Keys are stored as KeyCode, but with async input on the game reports KeyLabel
        // names, so a missing entry silently blocks that key. Same table on 3.3.1 and 3.4.0.
        private static readonly Dictionary<string, string[]> AsyncAliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["BackQuote"] = new[] { "Grave" },
            ["Equals"] = new[] { "Equal" },
            ["LeftBracket"] = new[] { "LeftBrace" },
            ["RightBracket"] = new[] { "RightBrace" },
            ["Quote"] = new[] { "Apostrophe" },
            ["Return"] = new[] { "Enter" },
            ["Period"] = new[] { "Dot" },
            ["LeftShift"] = new[] { "LShift" },
            ["RightShift"] = new[] { "RShift" },
            ["LeftControl"] = new[] { "LControl" },
            ["RightControl"] = new[] { "RControl" },
            ["LeftAlt"] = new[] { "LAlt" },
            ["RightAlt"] = new[] { "RAlt" },
            ["LeftWindows"] = new[] { "Super" },
            ["RightWindows"] = new[] { "Super" },
            ["LeftMeta"] = new[] { "Super" },
            ["RightMeta"] = new[] { "Super" },
            ["Print"] = new[] { "PrintScreen" },
            ["Pause"] = new[] { "PauseBreak" },
            ["UpArrow"] = new[] { "ArrowUp" },
            ["DownArrow"] = new[] { "ArrowDown" },
            ["LeftArrow"] = new[] { "ArrowLeft" },
            ["RightArrow"] = new[] { "ArrowRight" },
            ["KeypadDivide"] = new[] { "KeypadSlash" },
            ["KeypadMultiply"] = new[] { "KeypadAsterisk" },
            ["KeypadPeriod"] = new[] { "KeypadDot" },
            ["Mouse0"] = new[] { "MouseLeft" },
            ["Mouse1"] = new[] { "MouseRight" },
            ["Mouse2"] = new[] { "MouseMiddle" },
            ["Mouse3"] = new[] { "MouseX1" },
            ["Mouse4"] = new[] { "MouseX2" },
            ["Escape"] = new[] { "Esc" }
        };

        private static void AddWithAliases(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            Allowed.Add(key);
            if (AsyncAliases.TryGetValue(key, out string[] aliases))
                foreach (string alias in aliases) Allowed.Add(alias);
        }
    }
}
