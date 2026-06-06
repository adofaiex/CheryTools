using System;
using HarmonyLib;
using UnityEngine;

namespace CheryTools
{
    public static class VisualTweaks
    {
        public static void ApplyCustomColors()
        {
            if (!Main.IsEnabled || !Main.Settings.EnableCustomPlanetColors) return;

            var controller = scrController.instance;
            if (controller == null) return;

            // Apply for Red Planet
            if (controller.planetRed != null && controller.planetRed.planetRenderer != null)
            {
                var rRenderer = controller.planetRed.planetRenderer;
                rRenderer.EnableCustomColor();
                rRenderer.SetPlanetColor(FloatArrayToColor(Main.Settings.RedPlanetColor));
                rRenderer.SetTailColor(FloatArrayToColor(Main.Settings.RedTailColor));
            }

            // Apply for Blue Planet
            if (controller.planetBlue != null && controller.planetBlue.planetRenderer != null)
            {
                var bRenderer = controller.planetBlue.planetRenderer;
                bRenderer.EnableCustomColor();
                bRenderer.SetPlanetColor(FloatArrayToColor(Main.Settings.BluePlanetColor));
                bRenderer.SetTailColor(FloatArrayToColor(Main.Settings.BlueTailColor));
            }

            // Apply for Green Planet
            if (controller.planetGreen != null && controller.planetGreen.planetRenderer != null)
            {
                var gRenderer = controller.planetGreen.planetRenderer;
                gRenderer.EnableCustomColor();
                gRenderer.SetPlanetColor(FloatArrayToColor(Main.Settings.GreenPlanetColor));
                gRenderer.SetTailColor(FloatArrayToColor(Main.Settings.GreenTailColor));
            }
        }

        public static void RestoreDefaultColors()
        {
            var controller = scrController.instance;
            if (controller == null) return;

            if (controller.planetRed != null && controller.planetRed.planetRenderer != null)
                controller.planetRed.planetRenderer.LoadPlanetColor(true);

            if (controller.planetBlue != null && controller.planetBlue.planetRenderer != null)
                controller.planetBlue.planetRenderer.LoadPlanetColor(false);

            if (controller.planetGreen != null && controller.planetGreen.planetRenderer != null)
                controller.planetGreen.planetRenderer.LoadPlanetColor(false);
        }

        private static Color FloatArrayToColor(float[] arr)
        {
            if (arr == null || arr.Length < 4) return Color.white;
            return new Color(arr[0], arr[1], arr[2], arr[3]);
        }
        public static void ApplyLevelNameUI()
        {
            if (!Main.IsEnabled) return;
            if (scrUIController.instance == null || scrUIController.instance.txtLevelName == null) return;
            
            // Just hide or show the native level name based on the setting
            scrUIController.instance.txtLevelName.gameObject.SetActive(!Main.Settings.HideNativeLevelName);
        }

    }

    [HarmonyPatch(typeof(scrController), "Start")]
    public static class scrController_Start_Patch
    {
        public static void Postfix()
        {
            VisualTweaks.ApplyLevelNameUI();
        }
    }

    [HarmonyPatch(typeof(PlanetRenderer), "LoadPlanetColor")]
    public static class PlanetRenderer_LoadPlanetColor_Patch
    {
        public static void Postfix()
        {
            // Call ApplyCustomColors after the game loads the default planet color.
            // This ensures our custom colors override the default ones at the start of a level.
            VisualTweaks.ApplyCustomColors();
        }
    }
}
