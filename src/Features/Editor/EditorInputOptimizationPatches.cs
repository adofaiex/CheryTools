using HarmonyLib;
using UnityEngine;

namespace CheryTools
{
    internal static class EditorInputOptimization
    {
        internal static bool ShouldBlockAutoplaySpacePause()
        {
            return Main.IsEnabled
                && Main.Settings != null
                && Main.Settings.DisableAutoplaySpacePause
                && ADOBase.isLevelEditor
                && RDC.auto
                && Input.GetKeyDown(KeyCode.Space);
        }

        internal static bool ShouldBlockPlayModeScrollZoom(scnEditor editor, float delta)
        {
            return Main.IsEnabled
                && Main.Settings != null
                && Main.Settings.DisablePlayModeScrollZoom
                && ADOBase.isLevelEditor
                && EditorRuntimeCompatibility.IsPlayMode(editor)
                && EditorRuntimeCompatibility.IsMouseWheelZoom(delta);
        }
    }

    /// <summary>
    /// Intercepts the final pause action instead of rebuilding scnEditor.Update.
    /// The Space check keeps buttons and programmatic pause changes untouched.
    /// </summary>
    [HarmonyPatch(typeof(scrController), nameof(scrController.TogglePauseGame))]
    internal static class ScrControllerTogglePauseGameEditorPatch
    {
        private static bool Prefix(scrController __instance, ref bool __result)
        {
            if (!EditorInputOptimization.ShouldBlockAutoplaySpacePause())
                return true;

            __result = __instance != null && __instance.paused;
            return false;
        }
    }

    /// <summary>
    /// Intercepts only mouse-wheel zoom at the final action boundary. Keyboard
    /// zoom actions and programmatic camera positioning remain available.
    /// </summary>
    [HarmonyPatch(typeof(scnEditor), nameof(scnEditor.ZoomCamera),
        new[] { typeof(float), typeof(bool), typeof(bool) })]
    internal static class ScnEditorZoomCameraMouseWheelPatch
    {
        private static bool Prefix(scnEditor __instance, float delta)
        {
            return !EditorInputOptimization.ShouldBlockPlayModeScrollZoom(__instance, delta);
        }
    }
}
