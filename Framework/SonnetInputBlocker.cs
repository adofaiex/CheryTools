using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using CheryTools.Sonnet.Contracts;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.Sonnet.Framework
{
    /// <summary>
    /// Framework-level game input isolation.  Modules do not need to know which
    /// game input API a particular screen uses: while the Sonnet shell or an
    /// ImGui overlay is open, the framework short-circuits the game's input
    /// queries and navigation handlers.
    /// </summary>
    internal static class SonnetInputBlocker
    {
        private const string HarmonyId = "CheryTools.Sonnet.Framework.InputBlocker";
        private static Harmony _harmony;
        private static IModuleLogger _logger;
        private static readonly List<MethodBase> PatchedMethods = new List<MethodBase>();
        private static int _cachedBlockFrame = -1;
        private static bool _cachedBlockState;

        internal static bool IsUiInputBlocked
        {
            get
            {
                int frame = Time.frameCount;
                if (_cachedBlockFrame == frame) return _cachedBlockState;
                _cachedBlockFrame = frame;
                // Include the opening frame so Insert cannot be consumed by the
                // game before the Framework has toggled its panel state. Cache the
                // result because RDInput hot paths can query this many times/frame.
                _cachedBlockState = SonnetMain.IsEnabled
                    && (SonnetMain.IsAnyImGuiWindowOpen || Input.GetKeyDown(SonnetMain.PanelShortcut));
                return _cachedBlockState;
            }
        }

        internal static void Initialize(IModuleLogger logger)
        {
            if (_harmony != null) return;
            _logger = logger;
            _harmony = new Harmony(HarmonyId);

            PatchResultMethod("RDInput", "GetState", typeof(bool), 2, nameof(BlockBoolResult));
            PatchResultMethod("RDInput", "GetMain", typeof(int), 1, nameof(BlockIntResult));
            PatchKeyListMethod("RDInput", "GetStateKeys");
            PatchResultMethod("RDEditorUtils", "CheckForKeyCombo", typeof(bool), 3, nameof(BlockBoolResult));

            PatchNavigationMethod("scnLevelSelect", "JumpAndWipeWithKey", 3);
            PatchNavigationMethod("scnTaroMenu2", "JumpWithKey", 1);
            PatchNavigationMethod("scnTaroMenu3", "JumpWithKey", 1);
            PatchNavigationMethod("scnLevelSelectTaro", "JumpWithKey", 1);
            PatchNavigationMethod("scrController", "DebugUpdate", 0);
            PatchNavigationMethod("scrPlayer", "CountValidKeysPressed", -1);

            _logger?.Info(L("Sonnet Framework 已启用游戏输入隔离", "Sonnet Framework game input isolation enabled"));
        }

        internal static void Shutdown()
        {
            if (_harmony == null) return;
            try { _harmony.UnpatchAll(HarmonyId); }
            catch (Exception ex) { _logger?.Warning(L("移除 Sonnet 输入隔离补丁失败：", "Failed to remove Sonnet input isolation patches: ") + ex.Message); }
            PatchedMethods.Clear();
            _cachedBlockFrame = -1;
            _cachedBlockState = false;
            _harmony = null;
            _logger = null;
        }

        private static void PatchResultMethod(string typeName, string methodName, Type returnType, int parameterCount, string prefixName)
        {
            MethodInfo target = FindMethod(typeName, methodName, parameterCount);
            if (target == null || target.ReturnType != returnType) return;
            MethodInfo prefix = typeof(SonnetInputBlocker).GetMethod(prefixName, BindingFlags.Static | BindingFlags.NonPublic);
            Patch(target, prefix);
        }

        // 3.3.1: List<AnyKeyCode> GetStateKeys(state). 3.4.0: void GetStateKeys(keys, state).
        // Keep these patches non-generic: Harmony re-resolves stored patches by
        // metadata token, so a closed generic patch comes back as its open
        // definition and breaks every other mod patching the same method.
        private static void PatchKeyListMethod(string typeName, string methodName)
        {
            MethodInfo target = FindMethod(typeName, methodName, 1);
            if (target != null && typeof(IList).IsAssignableFrom(target.ReturnType))
            {
                MethodInfo postfix = typeof(SonnetInputBlocker).GetMethod(nameof(ClearKeyListResult), BindingFlags.Static | BindingFlags.NonPublic);
                Patch(target, postfix: postfix);
                return;
            }

            target = FindMethod(typeName, methodName, 2);
            if (target == null || target.ReturnType != typeof(void)) return;
            if (!typeof(IList).IsAssignableFrom(target.GetParameters()[0].ParameterType)) return;
            Patch(target, typeof(SonnetInputBlocker).GetMethod(nameof(BlockKeyBuffer), BindingFlags.Static | BindingFlags.NonPublic));
        }

        private static void PatchNavigationMethod(string typeName, string methodName, int parameterCount)
        {
            MethodInfo target = FindMethod(typeName, methodName, parameterCount);
            if (target == null) return;
            MethodInfo prefix;
            if (target.ReturnType == typeof(bool))
                prefix = typeof(SonnetInputBlocker).GetMethod(nameof(BlockBoolResult), BindingFlags.Static | BindingFlags.NonPublic);
            else if (target.ReturnType == typeof(int))
                prefix = typeof(SonnetInputBlocker).GetMethod(nameof(BlockIntResult), BindingFlags.Static | BindingFlags.NonPublic);
            else if (target.ReturnType == typeof(void))
                prefix = typeof(SonnetInputBlocker).GetMethod(nameof(BlockVoid), BindingFlags.Static | BindingFlags.NonPublic);
            else
                return;
            Patch(target, prefix);
        }

        private static void Patch(MethodInfo target, MethodInfo prefix = null, MethodInfo postfix = null)
        {
            try
            {
                _harmony.Patch(target,
                    prefix: prefix != null ? new HarmonyMethod(prefix) : null,
                    postfix: postfix != null ? new HarmonyMethod(postfix) : null);
                PatchedMethods.Add(target);
            }
            catch (Exception ex)
            {
                _logger?.Warning(L("未能隔离游戏输入方法 ", "Could not isolate game input method ") + target.DeclaringType?.Name + "." + target.Name + L("：", ": ") + ex.Message);
            }
        }

        private static MethodInfo FindMethod(string typeName, string methodName, int parameterCount)
        {
            Type type = FindType(typeName);
            if (type == null) return null;
            BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            return type.GetMethods(flags)
                .Where(method => method.Name == methodName)
                .Where(method => parameterCount < 0 || method.GetParameters().Length == parameterCount)
                .OrderBy(method => method.IsPublic ? 0 : 1)
                .FirstOrDefault();
        }

        private static Type FindType(string typeName)
        {
            Type type = AccessTools.TypeByName(typeName);
            if (type != null) return type;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.GetType(typeName, false);
                    if (type != null) return type;
                }
                catch { }
            }
            return null;
        }

        private static bool BlockBoolResult(ref bool __result)
        {
            if (!IsUiInputBlocked) return true;
            __result = false;
            return false;
        }

        private static bool BlockIntResult(ref int __result)
        {
            if (!IsUiInputBlocked) return true;
            __result = 0;
            return false;
        }

        private static void ClearKeyListResult(object __result)
        {
            if (IsUiInputBlocked) (__result as IList)?.Clear();
        }

        private static bool BlockKeyBuffer(object __0)
        {
            if (!IsUiInputBlocked) return true;
            (__0 as IList)?.Clear();
            return false;
        }

        private static bool BlockVoid()
        {
            return !IsUiInputBlocked;
        }
    }
}
