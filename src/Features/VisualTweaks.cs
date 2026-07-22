using System;
using HarmonyLib;
using UnityEngine;

namespace CheryTools
{
    public static class VisualTweaks
    {
        public static void ApplyCustomColors()
        {
            if (!Main.IsEnabled || Main.Settings == null || !Main.Settings.EnableCustomPlanetColors) return;

            var controller = scrController.instance;
            if (controller == null) return;

            // Apply for Red Planet
            if (controller.planetRed != null && controller.planetRed.planetRenderer != null)
            {
                var rRenderer = controller.planetRed.planetRenderer;
                rRenderer.EnableCustomColor();
                rRenderer.SetPlanetColor(FloatArrayToColor(Main.Settings.RedPlanetColor));
                ApplyRingColor(rRenderer, Main.Settings.RedRingColor);
                rRenderer.SetTailColor(FloatArrayToColor(Main.Settings.RedTailColor));
                ApplyPlanetTexture(rRenderer, Main.Settings.RedPlanetTexturePath);
            }

            // Apply for Blue Planet
            if (controller.planetBlue != null && controller.planetBlue.planetRenderer != null)
            {
                var bRenderer = controller.planetBlue.planetRenderer;
                bRenderer.EnableCustomColor();
                bRenderer.SetPlanetColor(FloatArrayToColor(Main.Settings.BluePlanetColor));
                ApplyRingColor(bRenderer, Main.Settings.BlueRingColor);
                bRenderer.SetTailColor(FloatArrayToColor(Main.Settings.BlueTailColor));
                ApplyPlanetTexture(bRenderer, Main.Settings.BluePlanetTexturePath);
            }

            // Apply for Green Planet
            if (controller.planetGreen != null && controller.planetGreen.planetRenderer != null)
            {
                var gRenderer = controller.planetGreen.planetRenderer;
                gRenderer.EnableCustomColor();
                gRenderer.SetPlanetColor(FloatArrayToColor(Main.Settings.GreenPlanetColor));
                ApplyRingColor(gRenderer, Main.Settings.GreenRingColor);
                gRenderer.SetTailColor(FloatArrayToColor(Main.Settings.GreenTailColor));
                ApplyPlanetTexture(gRenderer, Main.Settings.GreenPlanetTexturePath);
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

        private static void ApplyRingColor(PlanetRenderer renderer, float[] color)
        {
            if (renderer == null || renderer.ringComp == null) return;
            renderer.ringComp.color = FloatArrayToColor(color);
        }

        private static void ApplyPlanetTexture(PlanetRenderer renderer, string path)
        {
            if (renderer == null || renderer.sprite == null || Main.Settings == null || !Main.Settings.EnableCustomPlanetTextures)
                return;

            Texture referenceTexture = ADOBase.gc != null && ADOBase.gc.tex_planetWhite != null
                ? ADOBase.gc.tex_planetWhite
                : renderer.sprite.sprite;
            Texture2D texture = TextureManager.GetOrCreatePlanetSpriteTexture(path, referenceTexture);
            if (texture == null)
                return;

            renderer.sprite.sprite = texture;
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
