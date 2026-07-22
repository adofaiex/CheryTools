using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CheryTools
{
    internal static class OfficialLevelEditorPatches
    {
        private static readonly FieldInfo PauseButtonsField = AccessTools.Field(typeof(PauseMenu), "pauseButtons");
        private static readonly FieldInfo OpenInEditorButtonField = AccessTools.Field(typeof(PauseMenu), "openInEditorButton");
        private static readonly FieldInfo CurrentButtonsField = AccessTools.Field(typeof(PauseMenu), "currentButtons");
        private static readonly FieldInfo PauseMenuChainField = AccessTools.Field(typeof(PauseMenu), "pauseMenuChain");
        private static readonly FieldInfo ButtonsContainerField = AccessTools.Field(typeof(PauseMenu), "buttonsContainer");
        private static readonly MethodInfo RefreshLayoutMethod = AccessTools.Method(typeof(PauseMenu), "RefreshLayout");

        internal static bool ShouldInject()
        {
            if (!Main.IsEnabled || Main.Settings == null || !Main.Settings.EnableOfficialLevelEditorExperimental)
                return false;

            if (!ADOBase.isScnGame || !ADOBase.isOfficialLevel)
                return false;

            if (ADOBase.isCLSLevel || ADOBase.isTechFeaturedLevel || ADOBase.isMobile || ADOBase.isSwitch || ADOBase.isExpo)
                return false;

            return !GCS.practiceMode;
        }

        internal static void RefreshCurrentPauseMenu()
        {
            if (!Main.IsEnabled) return;
            try
            {
                PauseMenu pauseMenu = UnityEngine.Object.FindObjectOfType<PauseMenu>();
                if (pauseMenu != null && RefreshLayoutMethod != null)
                {
                    RefreshLayoutMethod.Invoke(pauseMenu, null);
                }
            }
            catch (Exception ex)
            {
                if (Main.Logger != null)
                    Main.Logger.Log("[CheryTools] 刷新官谱关卡编辑器按钮失败: " + ex.Message);
            }
        }

        internal static void EnsureOfficialEditorButton(PauseMenu pauseMenu)
        {
            if (!ShouldInject() || pauseMenu == null)
                return;

            if (PauseButtonsField == null || OpenInEditorButtonField == null)
                return;

            GeneralPauseButton[] pauseButtons = PauseButtonsField.GetValue(pauseMenu) as GeneralPauseButton[];
            GeneralPauseButton openInEditorButton = OpenInEditorButtonField.GetValue(pauseMenu) as GeneralPauseButton;
            if (pauseButtons == null || openInEditorButton == null)
                return;

            if (Array.IndexOf(pauseButtons, openInEditorButton) < 0)
            {
                List<GeneralPauseButton> updatedButtons = new List<GeneralPauseButton>(pauseButtons);
                int insertIndex = Math.Max(0, updatedButtons.Count - 1);
                updatedButtons.Insert(insertIndex, openInEditorButton);
                PauseButtonsField.SetValue(pauseMenu, updatedButtons.ToArray());
            }

            if (CurrentButtonsField != null)
            {
                List<GeneralPauseButton> currentButtons = CurrentButtonsField.GetValue(pauseMenu) as List<GeneralPauseButton>;
                if (currentButtons != null && !currentButtons.Contains(openInEditorButton))
                {
                    int insertIndex = Math.Max(0, currentButtons.Count - 1);
                    currentButtons.Insert(insertIndex, openInEditorButton);
                }
            }

            PauseMenuChain chain = PauseMenuChainField != null
                ? PauseMenuChainField.GetValue(pauseMenu) as PauseMenuChain
                : null;
            RectTransform buttonsContainer = ButtonsContainerField != null
                ? ButtonsContainerField.GetValue(pauseMenu) as RectTransform
                : null;
            if (chain != null)
            {
                chain.UpdateLinks();
                if (buttonsContainer != null)
                    chain.UpdateHeight(buttonsContainer);
            }
        }
    }

    [HarmonyPatch(typeof(PauseMenu), "RefreshLayout")]
    internal static class PauseMenu_RefreshLayout_OfficialLevelEditor_Patch
    {
        private static void Postfix(PauseMenu __instance)
        {
            OfficialLevelEditorPatches.EnsureOfficialEditorButton(__instance);
        }
    }
}
