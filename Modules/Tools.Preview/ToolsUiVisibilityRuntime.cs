using System.Collections.Generic;
using UnityEngine;

namespace CheryTools.Tools.Preview
{
    internal static class ToolsUiVisibilityRuntime
    {
        private static readonly Dictionary<GameObject, bool> HiddenObjects = new Dictionary<GameObject, bool>();
        private static readonly List<scrMissIndicator> _missIndicators = new List<scrMissIndicator>();
        private static readonly List<scrEnableIfBeta> _betaIndicators = new List<scrEnableIfBeta>();
        private static readonly List<scrShowIfDebug> _autoplayStatusTexts = new List<scrShowIfDebug>();
        private static bool _hudStateCaptured;
        private static bool _hudState;
        private static int _controllerId;
        private static bool _lastEditorContext;
        private static bool _lastGameWorld;
        private static bool _contextInitialized;

        internal static bool ShouldHideJudgment()
        {
            ToolsUiVisibilitySettings profile = SelectedProfile();
            return ToolsRuntime.IsActive && profile != null &&
                (profile.HideEverything || profile.HideJudgment);
        }

        internal static bool ShouldHideLastFloorFlash()
        {
            ToolsUiVisibilitySettings profile = SelectedProfile();
            return ToolsRuntime.IsActive && profile != null &&
                (profile.HideEverything || profile.HideLastFloorFlash);
        }

        internal static void Tick()
        {
            if (!ToolsRuntime.IsActive || ToolsRuntime.Settings == null)
            {
                Restore();
                return;
            }

            ToolsUiVisibilitySettings profile = SelectedProfile();
            if (profile == null)
            {
                Restore();
                return;
            }

            scrController controller = scrController.instance;
            scnEditor editor = ADOBase.isLevelEditor ? scnEditor.instance : null;
            bool levelEditor = editor != null;
            bool editorContext = levelEditor && !IsEditorPlaying(editor);
            bool gameWorld = controller != null && controller.gameworld;
            int controllerId = controller != null ? controller.GetInstanceID() : 0;
            if (_contextInitialized && (controllerId != _controllerId || editorContext != _lastEditorContext))
                Restore();
            _contextInitialized = true;
            _controllerId = controllerId;
            _lastEditorContext = editorContext;
            _lastGameWorld = gameWorld;

            bool hideAll = profile.HideEverything;
            // RDC.noHud affects more than the visible HUD in the editor and can
            // prevent the editor camera from following the moving planet.
            SetHudHidden(hideAll && !levelEditor);

            scrUIController ui = scrUIController.instance;
            if (ui != null)
            {
                SetHidden(ui.txtLevelName?.gameObject, hideAll || profile.HideTitle);
                SetHidden(ui.difficultyContainer?.gameObject, hideAll || profile.HideTimingTarget);
                SetHidden(ui.difficultyFadeContainer?.gameObject, hideAll || profile.HideTimingTarget);
                SetHidden(ui.noFailImage?.gameObject, hideAll || profile.HideNoFailIcon);
                // These are the actual gameplay UI fields in the current game.
                // The editor has separate Otto/no-fail controls below.
                SetHidden(ui.unlockKeyLimiterImage?.gameObject, hideAll || profile.HideNoFailIcon);
                SetHidden(ui.autoplayButton?.gameObject, hideAll || profile.HideOtto);
                SetHidden(ui.txtCongrats?.gameObject, hideAll || profile.HideResult);
                SetHidden(ui.txtAprilCongrats?.gameObject, hideAll || profile.HideResult);
                SetHidden(ui.txtPercent?.gameObject, hideAll || profile.HideResult);
                SetHidden(ui.txtResults?.gameObject, hideAll || profile.HideResult);
                SetHidden(ui.txtAllStrictClear?.gameObject, hideAll || profile.HideResult);
                SetHidden(ui.txtCountdown?.gameObject, hideAll);
                SetHidden(ui.txtPressToStart?.gameObject, hideAll);
                SetHidden(ui.pauseButton?.gameObject, hideAll);
                SetHidden(ui.modifiersContainer?.gameObject, hideAll);
                SetHidden(ui.txtOffset?.gameObject, hideAll);
                SetHidden(ui.txtDebug?.gameObject, hideAll);
                SetHidden(ui.txtTryCalibrating?.gameObject, hideAll);
            }

            if (controller != null)
            {
                bool hideMeter = hideAll || profile.HideHitErrorMeter;
                if (controller.errorMeter != null)
                {
                    SetHidden(controller.errorMeter.gameObject, hideMeter);
                    SetHidden(controller.errorMeter.wrapperRectTransform?.gameObject, hideMeter);
                    SetHidden(MeterObject(CurvedMeterField, controller.errorMeter), hideMeter);
                    SetHidden(MeterObject(StraightMeterField, controller.errorMeter), hideMeter);
                    SetHidden(controller.errorMeter.handImage?.gameObject, hideMeter);
                }
                SetHidden(controller.txtCongrats?.gameObject, hideAll || profile.HideResult);
                SetHidden(controller.txtAprilCongrats?.gameObject, hideAll || profile.HideResult);
                SetHidden(controller.txtPercent?.gameObject, hideAll || profile.HideResult);
                SetHidden(controller.detailedResults?.gameObject, hideAll || profile.HideResult);
                SetHidden(controller.txtAllStrictClear?.gameObject, hideAll || profile.HideResult);
            }

            bool hideBeta = hideAll || profile.HideBeta;
            for (int i = _betaIndicators.Count - 1; i >= 0; i--)
            {
                scrEnableIfBeta indicator = _betaIndicators[i];
                if (indicator == null)
                {
                    _betaIndicators.RemoveAt(i);
                    continue;
                }
                SetHidden(indicator.gameObject, hideBeta);
            }

            bool hideAutoplayText = hideAll || profile.HideOtto;
            for (int i = _autoplayStatusTexts.Count - 1; i >= 0; i--)
            {
                scrShowIfDebug text = _autoplayStatusTexts[i];
                if (text == null)
                {
                    _autoplayStatusTexts.RemoveAt(i);
                    continue;
                }
                SetHidden(text.gameObject, hideAutoplayText);
            }

            bool hideMissIndicators = hideAll || profile.HideMissIndicators;
            for (int i = _missIndicators.Count - 1; i >= 0; i--)
            {
                scrMissIndicator indicator = _missIndicators[i];
                if (indicator == null)
                {
                    _missIndicators.RemoveAt(i);
                    continue;
                }
                SetHidden(indicator.gameObject, hideMissIndicators);
            }
            if (controller != null && controller.playerOne != null && controller.playerOne.missesOnCurrFloor != null)
            {
                var misses = controller.playerOne.missesOnCurrFloor;
                for (int i = 0; i < misses.Count; i++)
                {
                    scrMissIndicator indicator = misses[i];
                    if (indicator != null) SetHidden(indicator.gameObject, hideMissIndicators);
                }
            }

            if (editor != null)
            {
                SetHidden(editor.autoImage?.gameObject, hideAll || profile.HideOtto);
                SetHidden(editor.buttonAuto?.gameObject, hideAll || profile.HideOtto);
                SetHidden(editor.editorDifficultySelector?.gameObject, hideAll || profile.HideTimingTarget);
                SetHidden(editor.buttonNoFail?.gameObject, hideAll || profile.HideNoFailIcon);
            }
        }

        // scrUIController.Update and scrController.Update reapply UI elements
        // from GCS/game state every frame. Reapply only the affected objects from
        // their postfixes instead of running the complete resolver multiple times.
        internal static void RefreshPersistentGameUi()
        {
            if (!ToolsRuntime.IsActive || ToolsRuntime.Settings == null) return;

            ToolsUiVisibilitySettings profile = SelectedProfile();
            if (profile == null) return;
            // Runs after scrController.Update and scrUIController.Update every frame. With nothing to
            // hide and nothing to restore there is no work, which is the common configuration.
            if (HiddenObjects.Count == 0 && !HidesPersistentUi(profile)) return;

            bool hideAll = profile.HideEverything;
            scrUIController ui = scrUIController.instance;
            if (ui != null)
            {
                SetHidden(ui.txtLevelName?.gameObject, hideAll || profile.HideTitle);
                SetHidden(ui.difficultyContainer?.gameObject, hideAll || profile.HideTimingTarget);
                SetHidden(ui.difficultyFadeContainer?.gameObject, hideAll || profile.HideTimingTarget);
                SetHidden(ui.noFailImage?.gameObject, hideAll || profile.HideNoFailIcon);
                SetHidden(ui.unlockKeyLimiterImage?.gameObject, hideAll || profile.HideNoFailIcon);
                SetHidden(ui.autoplayButton?.gameObject, hideAll || profile.HideOtto);
                SetHidden(ui.txtCongrats?.gameObject, hideAll || profile.HideResult);
                SetHidden(ui.txtAprilCongrats?.gameObject, hideAll || profile.HideResult);
                SetHidden(ui.txtPercent?.gameObject, hideAll || profile.HideResult);
                SetHidden(ui.txtResults?.gameObject, hideAll || profile.HideResult);
                SetHidden(ui.txtAllStrictClear?.gameObject, hideAll || profile.HideResult);
                SetHidden(ui.txtCountdown?.gameObject, hideAll);
                SetHidden(ui.txtPressToStart?.gameObject, hideAll);
                SetHidden(ui.pauseButton?.gameObject, hideAll);
                SetHidden(ui.modifiersContainer?.gameObject, hideAll);
                SetHidden(ui.txtOffset?.gameObject, hideAll);
                SetHidden(ui.txtDebug?.gameObject, hideAll);
                SetHidden(ui.txtTryCalibrating?.gameObject, hideAll);
            }

            scrController controller = scrController.instance;
            if (controller != null)
            {
                bool hideMeter = hideAll || profile.HideHitErrorMeter;
                if (controller.errorMeter != null)
                {
                    SetHidden(controller.errorMeter.gameObject, hideMeter);
                    SetHidden(controller.errorMeter.wrapperRectTransform?.gameObject, hideMeter);
                    SetHidden(MeterObject(CurvedMeterField, controller.errorMeter), hideMeter);
                    SetHidden(MeterObject(StraightMeterField, controller.errorMeter), hideMeter);
                    SetHidden(controller.errorMeter.handImage?.gameObject, hideMeter);
                }
                SetHidden(controller.txtCongrats?.gameObject, hideAll || profile.HideResult);
                SetHidden(controller.txtAprilCongrats?.gameObject, hideAll || profile.HideResult);
                SetHidden(controller.txtPercent?.gameObject, hideAll || profile.HideResult);
                SetHidden(controller.detailedResults?.gameObject, hideAll || profile.HideResult);
                SetHidden(controller.txtAllStrictClear?.gameObject, hideAll || profile.HideResult);
            }
        }

        internal static void Restore()
        {
            foreach (KeyValuePair<GameObject, bool> pair in HiddenObjects)
                if (pair.Key != null) pair.Key.SetActive(pair.Value);
            HiddenObjects.Clear();
            _missIndicators.Clear();
            _betaIndicators.Clear();
            _autoplayStatusTexts.Clear();
            if (_hudStateCaptured)
            {
                RDC.noHud = _hudState;
                _hudStateCaptured = false;
            }
            _contextInitialized = false;
            _controllerId = 0;
        }

        private static bool HidesPersistentUi(ToolsUiVisibilitySettings profile)
        {
            return profile.HideEverything || profile.HideTitle || profile.HideTimingTarget
                || profile.HideNoFailIcon || profile.HideOtto || profile.HideResult || profile.HideHitErrorMeter;
        }

        private static ToolsUiVisibilitySettings SelectedProfile()
        {
            ToolsSettings settings = ToolsRuntime.Settings;
            return settings == null ? null : settings.RecordingMode ? settings.RecordingUi : settings.PlayingUi;
        }

        private static void SetHudHidden(bool hidden)
        {
            if (hidden)
            {
                if (!_hudStateCaptured)
                {
                    _hudState = RDC.noHud;
                    _hudStateCaptured = true;
                }
                RDC.noHud = true;
                return;
            }

            if (_hudStateCaptured)
            {
                RDC.noHud = _hudState;
                _hudStateCaptured = false;
            }
        }

        // curvedMeter/straightMeter are GameObject fields on game 3.3.x and Image fields on 3.4.0. A compiled
        // field access binds to one type and throws MissingFieldException on the other, so they are read by
        // name and either form is turned into its GameObject.
        private static readonly System.Reflection.FieldInfo CurvedMeterField = typeof(scrHitErrorMeter).GetField("curvedMeter");
        private static readonly System.Reflection.FieldInfo StraightMeterField = typeof(scrHitErrorMeter).GetField("straightMeter");

        private static GameObject MeterObject(System.Reflection.FieldInfo field, scrHitErrorMeter meter)
        {
            object value = field?.GetValue(meter);
            if (value is GameObject gameObject) return gameObject != null ? gameObject : null;
            Component component = value as Component;
            return component != null ? component.gameObject : null;
        }

        private static void SetHidden(GameObject gameObject, bool hidden)
        {
            if (gameObject == null) return;
            if (hidden)
            {
                if (!HiddenObjects.ContainsKey(gameObject)) HiddenObjects.Add(gameObject, gameObject.activeSelf);
                if (gameObject.activeSelf) gameObject.SetActive(false);
            }
            else if (HiddenObjects.TryGetValue(gameObject, out bool originalActive))
            {
                gameObject.SetActive(originalActive);
                HiddenObjects.Remove(gameObject);
            }
        }

        internal static void OnMissIndicatorSpawned(scrMissIndicator indicator)
        {
            if (indicator == null || !ToolsRuntime.IsActive) return;
            if (!_missIndicators.Contains(indicator)) _missIndicators.Add(indicator);
            ToolsUiVisibilitySettings profile = SelectedProfile();
            if (profile != null && (profile.HideEverything || profile.HideMissIndicators))
                SetHidden(indicator.gameObject, true);
        }

        internal static void OnBetaIndicatorSpawned(scrEnableIfBeta indicator)
        {
            if (indicator == null || !ToolsRuntime.IsActive) return;
            if (!_betaIndicators.Contains(indicator)) _betaIndicators.Add(indicator);
            ToolsUiVisibilitySettings profile = SelectedProfile();
            if (profile != null && (profile.HideEverything || profile.HideBeta))
                SetHidden(indicator.gameObject, true);
        }

        internal static void RegisterAutoplayStatusText(scrShowIfDebug text)
        {
            if (text != null && !_autoplayStatusTexts.Contains(text))
                _autoplayStatusTexts.Add(text);
        }

        internal static void RefreshAutoplayStatusText(scrShowIfDebug text)
        {
            if (text == null) return;
            RegisterAutoplayStatusText(text);
            ToolsUiVisibilitySettings profile = SelectedProfile();
            if (ToolsRuntime.IsActive && profile != null)
                SetHidden(text.gameObject, profile.HideEverything || profile.HideOtto);
        }

        private static bool IsEditorPlaying(scnEditor editor)
        {
            try { return editor != null && editor.playMode; }
            catch { return EditorRuntimeCompatibility.IsPlayMode(editor); }
        }
    }
}
