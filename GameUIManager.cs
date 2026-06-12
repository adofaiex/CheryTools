using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CheryTools
{
    public sealed class GameUITargetDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly Func<RectTransform> Resolve;
        public readonly Func<List<RectTransform>> ResolveAll;

        public GameUITargetDefinition(string id, string displayName, Func<RectTransform> resolve)
        {
            Id = id;
            DisplayName = displayName;
            Resolve = resolve;
            ResolveAll = null;
        }

        public GameUITargetDefinition(string id, string displayName, Func<List<RectTransform>> resolveAll)
        {
            Id = id;
            DisplayName = displayName;
            Resolve = null;
            ResolveAll = resolveAll;
        }
    }

    public class GameUIManager : MonoBehaviour
    {
        public const string DeveloperUnlockKey = "CHERYUI110";
        public static GameUIManager Instance { get; private set; }

        public static readonly GameUITargetDefinition[] Targets =
        {
            new GameUITargetDefinition("levelName", "\u5173\u5361\u540D\u79F0", () => scrUIController.instance != null && scrUIController.instance.txtLevelName != null ? scrUIController.instance.txtLevelName.rectTransform : null),
            new GameUITargetDefinition("countdown", "\u5012\u8BA1\u65F6", () => scrUIController.instance != null && scrUIController.instance.txtCountdown != null ? scrUIController.instance.txtCountdown.rectTransform : null),
            new GameUITargetDefinition("hitErrorMeter", "\u51C6\u5EA6\u6761", ResolveHitErrorMeter),
            new GameUITargetDefinition("difficultySwitch", "\u5224\u5B9A\u6A21\u5F0F\u5207\u6362", ResolveDifficultySwitch),
            new GameUITargetDefinition("noFailSwitch", "\u4E0D\u4F1A\u5931\u8D25\u6A21\u5F0F\u5F00\u5173", ResolveNoFailSwitch),
            new GameUITargetDefinition("autoplaySwitch", "\u81EA\u52A8\u6F14\u594F\u5F00\u5173", ResolveAutoplaySwitch),
            new GameUITargetDefinition("autoplayText", "\u81EA\u52A8\u64AD\u653E\u6587\u5B57", ResolveAutoplayText),
            new GameUITargetDefinition("buildWatermark", "\u6D4B\u8BD5\u7248\u672C\u6C34\u5370", ResolveBuildWatermarks)
        };

        private readonly Dictionary<string, ElementState> _states = new Dictionary<string, ElementState>();
        private static readonly List<scrShowIfDebug> _autoplayStatusTexts = new List<scrShowIfDebug>();
        private static readonly List<scrEnableIfBeta> _buildWatermarkTexts = new List<scrEnableIfBeta>();
        private static readonly List<RectTransform> _buildWatermarkRectsBuffer = new List<RectTransform>();
        private static bool _didInitialTargetScan;
        private readonly List<RectTransform> _resolvedRectsBuffer = new List<RectTransform>(4);
        private readonly HashSet<string> _activeStateIdsBuffer = new HashSet<string>();
        private bool _wasApplying = false;

        private sealed class ElementState
        {
            public RectTransform Rect;
            public Vector2 AnchoredPosition;
            public Vector3 LocalScale;
            public CanvasGroup CanvasGroup;
            public bool HadCanvasGroup;
            public float CanvasAlpha;
            public bool CanvasInteractable;
            public bool CanvasBlocksRaycasts;
            public GraphicState[] Graphics;
        }

        private sealed class GraphicState
        {
            public Graphic Graphic;
            public Color Color;
        }

        private void Awake()
        {
            Instance = this;
            RegisterExistingTargetsOnce();
        }

        private void OnDestroy()
        {
            RestoreAll();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void LateUpdate()
        {
            if (!ShouldApply())
            {
                if (_wasApplying)
                {
                    RestoreAll();
                    _wasApplying = false;
                }
                return;
            }

            _wasApplying = true;

            foreach (var target in Targets)
            {
                var setting = Main.Settings.GetGameUIElement(target.Id);
                if (setting == null || !setting.Enabled)
                {
                    RestoreTargetStates(target.Id);
                    continue;
                }

                SafeResolveAll(target, _resolvedRectsBuffer);
                if (_resolvedRectsBuffer.Count == 0)
                {
                    RestoreTargetStates(target.Id);
                    continue;
                }

                _activeStateIdsBuffer.Clear();
                bool multiTarget = target.ResolveAll != null;
                for (int i = 0; i < _resolvedRectsBuffer.Count; i++)
                {
                    RectTransform rect = _resolvedRectsBuffer[i];
                    if (rect == null) continue;

                    string stateId = multiTarget ? MakeTargetStateId(target.Id, rect) : target.Id;
                    _activeStateIdsBuffer.Add(stateId);
                    Apply(stateId, target.Id, rect, setting);
                }

                RestoreInactiveTargetStates(target.Id, _activeStateIdsBuffer);
            }
        }

        public void RestoreAll()
        {
            foreach (var key in new List<string>(_states.Keys))
            {
                Restore(key);
            }
        }

        public void RestoreTarget(string id)
        {
            RestoreTargetStates(id);
        }

        public static bool IsRestrictedAdvancedTarget(string id)
        {
            return string.Equals(id, "difficultySwitch", StringComparison.Ordinal)
                || string.Equals(id, "noFailSwitch", StringComparison.Ordinal)
                || string.Equals(id, "autoplaySwitch", StringComparison.Ordinal);
        }

        public static bool AreRestrictedAdvancedControlsUnlocked()
        {
            return Main.Settings != null && Main.Settings.GameUIDeveloperUnlocked;
        }

        internal static void RegisterAutoplayStatusText(scrShowIfDebug component)
        {
            if (component == null)
                return;

            for (int i = 0; i < _autoplayStatusTexts.Count; i++)
            {
                if (_autoplayStatusTexts[i] == component)
                    return;
            }

            _autoplayStatusTexts.Add(component);
        }

        internal static void RegisterBuildWatermark(scrEnableIfBeta component)
        {
            if (component == null)
                return;

            for (int i = 0; i < _buildWatermarkTexts.Count; i++)
            {
                if (_buildWatermarkTexts[i] == component)
                    return;
            }

            _buildWatermarkTexts.Add(component);
        }

        private static void RegisterExistingTargetsOnce()
        {
            if (_didInitialTargetScan)
                return;

            _didInitialTargetScan = true;

            try
            {
                scrShowIfDebug[] autoplayTexts = Resources.FindObjectsOfTypeAll<scrShowIfDebug>();
                for (int i = 0; i < autoplayTexts.Length; i++)
                {
                    RegisterAutoplayStatusText(autoplayTexts[i]);
                }

                scrEnableIfBeta[] watermarks = Resources.FindObjectsOfTypeAll<scrEnableIfBeta>();
                for (int i = 0; i < watermarks.Length; i++)
                {
                    RegisterBuildWatermark(watermarks[i]);
                }
            }
            catch (Exception ex)
            {
                Main.Logger?.Log("Game UI initial target scan failed: " + ex.Message);
            }
        }

        private static RectTransform ResolveHitErrorMeter()
        {
            if (scrController.instance == null || scrController.instance.errorMeter == null)
                return null;

            return scrController.instance.errorMeter.wrapperRectTransform;
        }

        private static RectTransform ResolveDifficultySwitch()
        {
            if (scnEditor.instance != null && scnEditor.instance.editorDifficultySelector != null)
            {
                if (scnEditor.instance.editorDifficultySelector.bullseyeImage != null)
                    return scnEditor.instance.editorDifficultySelector.bullseyeImage.rectTransform;

                if (scnEditor.instance.editorDifficultySelector.buttonChangeDifficulty != null)
                    return scnEditor.instance.editorDifficultySelector.buttonChangeDifficulty.GetComponent<RectTransform>();

                return scnEditor.instance.editorDifficultySelector.GetComponent<RectTransform>();
            }

            return scrUIController.instance != null ? scrUIController.instance.difficultyContainer : null;
        }

        private static RectTransform ResolveNoFailSwitch()
        {
            if (scnEditor.instance != null && scnEditor.instance.buttonNoFail != null)
                return scnEditor.instance.buttonNoFail.GetComponent<RectTransform>();

            return scrUIController.instance != null && scrUIController.instance.noFailImage != null
                ? scrUIController.instance.noFailImage.rectTransform
                : null;
        }

        private static RectTransform ResolveAutoplaySwitch()
        {
            if (scnEditor.instance != null)
            {
                if (scnEditor.instance.autoImage != null)
                    return scnEditor.instance.autoImage.rectTransform;

                if (scnEditor.instance.buttonAuto != null)
                    return scnEditor.instance.buttonAuto.GetComponent<RectTransform>();
            }

            return scrUIController.instance != null && scrUIController.instance.autoplayButton != null
                ? scrUIController.instance.autoplayButton.GetComponent<RectTransform>()
                : null;
        }

        private static RectTransform ResolveAutoplayText()
        {
            RegisterExistingTargetsOnce();

            RectTransform fallback = null;
            for (int i = _autoplayStatusTexts.Count - 1; i >= 0; i--)
            {
                scrShowIfDebug component = _autoplayStatusTexts[i];
                if (component == null)
                {
                    _autoplayStatusTexts.RemoveAt(i);
                    continue;
                }

                Text text = component.GetComponent<Text>();
                if (text == null || !IsValidSceneRect(text.rectTransform))
                    continue;

                if (component.hideWithNoAuto)
                    return text.rectTransform;

                if (fallback == null)
                    fallback = text.rectTransform;
            }

            return fallback;
        }

        private static List<RectTransform> ResolveBuildWatermarks()
        {
            RegisterExistingTargetsOnce();
            _buildWatermarkRectsBuffer.Clear();

            for (int i = _buildWatermarkTexts.Count - 1; i >= 0; i--)
            {
                scrEnableIfBeta component = _buildWatermarkTexts[i];
                if (component == null)
                {
                    _buildWatermarkTexts.RemoveAt(i);
                    continue;
                }

                if (!component.setBuildText)
                    continue;

                TMP_Text text = component.GetComponent<TMP_Text>();
                if (text == null || !IsValidSceneRect(text.rectTransform))
                    continue;

                TryAddRect(_buildWatermarkRectsBuffer, text.rectTransform);
            }

            return _buildWatermarkRectsBuffer;
        }

        private static bool IsValidSceneRect(RectTransform rect)
        {
            return rect != null
                && rect.gameObject != null
                && rect.gameObject.scene.IsValid();
        }

        private static void TryAddRect(List<RectTransform> results, RectTransform rect)
        {
            if (results == null || !IsValidSceneRect(rect)) return;

            int instanceId = rect.GetInstanceID();
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i] != null && results[i].GetInstanceID() == instanceId)
                    return;
            }

            results.Add(rect);
        }

        private static bool ShouldApply()
        {
            if (!Main.IsEnabled || Main.Settings == null || !Main.Settings.GameUIControlEnabled)
                return false;
            if (scrUIController.instance == null && scnEditor.instance == null)
                return false;
            if (ADOBase.isLevelEditor || ADOBase.isScnGame || ADOBase.isCLS)
                return true;

            return scrController.instance != null && scrController.instance.gameworld;
        }

        private static void SafeResolveAll(GameUITargetDefinition target, List<RectTransform> results)
        {
            if (results == null) return;
            results.Clear();

            try
            {
                if (target.ResolveAll != null)
                {
                    NormalizeResolvedRects(target.ResolveAll(), results);
                    return;
                }

                RectTransform rect = target.Resolve != null ? target.Resolve() : null;
                if (rect != null)
                {
                    results.Add(rect);
                }
            }
            catch (Exception ex)
            {
                Main.Logger?.Log("Game UI target resolve failed: " + target.Id + " - " + ex.Message);
                results.Clear();
            }
        }

        private void Apply(string stateId, string targetId, RectTransform rect, GameUIElementSetting setting)
        {
            ElementState state = GetOrCaptureState(stateId, rect);
            if (state == null)
                return;

            bool restrictAdvanced = IsRestrictedAdvancedTarget(targetId) && !AreRestrictedAdvancedControlsUnlocked();
            float offsetX = restrictAdvanced ? 0f : setting.OffsetX;
            float offsetY = restrictAdvanced ? 0f : setting.OffsetY;
            float alpha = setting.Visible ? (restrictAdvanced ? 1f : Mathf.Clamp01(setting.Alpha)) : 0f;
            float scale = restrictAdvanced ? 1f : Mathf.Clamp(setting.Scale, 0.05f, 5f);

            rect.anchoredPosition = state.AnchoredPosition + new Vector2(offsetX, offsetY);
            rect.localScale = new Vector3(state.LocalScale.x * scale, state.LocalScale.y * scale, state.LocalScale.z);

            CanvasGroup group = state.CanvasGroup;
            if (group != null)
            {
                group.alpha = alpha;
                group.interactable = setting.Visible && state.CanvasInteractable;
                group.blocksRaycasts = setting.Visible && state.CanvasBlocksRaycasts;
            }

            ApplyGraphicAlpha(state, alpha);
        }

        private static void NormalizeResolvedRects(List<RectTransform> source, List<RectTransform> result)
        {
            if (result == null) return;
            if (source == null) return;

            for (int i = 0; i < source.Count; i++)
            {
                RectTransform rect = source[i];
                if (!IsValidSceneRect(rect)) continue;

                TryAddRect(result, rect);
            }
        }

        private static string MakeTargetStateId(string targetId, RectTransform rect)
        {
            return targetId + "#" + rect.GetInstanceID().ToString();
        }

        private void RestoreTargetStates(string targetId)
        {
            string prefix = targetId + "#";
            var keys = new List<string>();
            foreach (var key in _states.Keys)
            {
                if (string.Equals(key, targetId, StringComparison.Ordinal) || key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    keys.Add(key);
                }
            }

            for (int i = 0; i < keys.Count; i++)
            {
                Restore(keys[i]);
            }
        }

        private void RestoreInactiveTargetStates(string targetId, HashSet<string> activeStateIds)
        {
            string prefix = targetId + "#";
            var keys = new List<string>();
            foreach (var key in _states.Keys)
            {
                if ((string.Equals(key, targetId, StringComparison.Ordinal) || key.StartsWith(prefix, StringComparison.Ordinal))
                    && (activeStateIds == null || !activeStateIds.Contains(key)))
                {
                    keys.Add(key);
                }
            }

            for (int i = 0; i < keys.Count; i++)
            {
                Restore(keys[i]);
            }
        }

        private ElementState GetOrCaptureState(string id, RectTransform rect)
        {
            if (_states.TryGetValue(id, out ElementState state))
            {
                if (state.Rect == rect)
                    return state;

                Restore(id);
            }

            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            bool hadCanvasGroup = group != null;
            if (group == null)
            {
                group = rect.gameObject.AddComponent<CanvasGroup>();
            }

            Graphic[] graphics = rect.GetComponentsInChildren<Graphic>(true);
            GraphicState[] graphicStates = new GraphicState[graphics.Length];
            for (int i = 0; i < graphics.Length; i++)
            {
                graphicStates[i] = new GraphicState
                {
                    Graphic = graphics[i],
                    Color = graphics[i].color
                };
            }

            state = new ElementState
            {
                Rect = rect,
                AnchoredPosition = rect.anchoredPosition,
                LocalScale = rect.localScale,
                CanvasGroup = group,
                HadCanvasGroup = hadCanvasGroup,
                CanvasAlpha = group != null ? group.alpha : 1f,
                CanvasInteractable = group == null || group.interactable,
                CanvasBlocksRaycasts = group == null || group.blocksRaycasts,
                Graphics = graphicStates
            };
            _states[id] = state;
            return state;
        }

        private static void ApplyGraphicAlpha(ElementState state, float alpha)
        {
            if (state.Graphics == null)
                return;

            foreach (var graphicState in state.Graphics)
            {
                if (graphicState == null || graphicState.Graphic == null)
                    continue;

                Color color = graphicState.Graphic.color;
                color.a = graphicState.Color.a * alpha;
                graphicState.Graphic.color = color;
            }
        }

        private void Restore(string id)
        {
            if (!_states.TryGetValue(id, out ElementState state))
                return;

            if (state.Rect != null)
            {
                state.Rect.anchoredPosition = state.AnchoredPosition;
                state.Rect.localScale = state.LocalScale;
            }

            if (state.CanvasGroup != null)
            {
                state.CanvasGroup.alpha = state.CanvasAlpha;
                state.CanvasGroup.interactable = state.CanvasInteractable;
                state.CanvasGroup.blocksRaycasts = state.CanvasBlocksRaycasts;

                if (!state.HadCanvasGroup)
                {
                    Destroy(state.CanvasGroup);
                }
            }

            if (state.Graphics != null)
            {
                foreach (var graphicState in state.Graphics)
                {
                    if (graphicState != null && graphicState.Graphic != null)
                    {
                        graphicState.Graphic.color = graphicState.Color;
                    }
                }
            }

            _states.Remove(id);
        }
    }
}
