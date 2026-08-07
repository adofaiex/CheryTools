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

    [DefaultExecutionOrder(32000)]
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
            new GameUITargetDefinition("buildWatermark", "\u6D4B\u8BD5\u7248\u672C\u6C34\u5370", ResolveBuildWatermarks),
            new GameUITargetDefinition("resultScreen", "\u7ED3\u7B97\u9875\u9762 UI", ResolveResultScreen)
        };

        private readonly Dictionary<string, ElementState> _states = new Dictionary<string, ElementState>();
        private static readonly List<scrShowIfDebug> _autoplayStatusTexts = new List<scrShowIfDebug>();
        private static readonly List<scrEnableIfBeta> _buildWatermarkTexts = new List<scrEnableIfBeta>();
        private static readonly List<RectTransform> _buildWatermarkRectsBuffer = new List<RectTransform>();
        private static readonly List<RectTransform> _resultScreenRectsBuffer = new List<RectTransform>();
        private static bool _didInitialTargetScan;
        private readonly List<RectTransform> _resolvedRectsBuffer = new List<RectTransform>(4);
        private readonly HashSet<string> _activeStateIdsBuffer = new HashSet<string>();
        private bool _wasApplying = false;
        // Reused by the restore paths to avoid allocating a List plus a prefix string.
        private readonly List<string> _restoreKeysBuffer = new List<string>();
        private static readonly Dictionary<string, string> _targetPrefixCache = new Dictionary<string, string>();

        private static string GetTargetPrefix(string targetId)
        {
            if (!_targetPrefixCache.TryGetValue(targetId, out string prefix))
            {
                prefix = targetId + "#";
                _targetPrefixCache[targetId] = prefix;
            }
            return prefix;
        }

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

            if (!HasEnabledTargets())
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

        private static bool HasEnabledTargets()
        {
            var elements = Main.Settings != null ? Main.Settings.GameUIElements : null;
            if (elements == null)
                return false;

            for (int i = 0; i < elements.Count; i++)
            {
                GameUIElementSetting setting = elements[i];
                if (setting != null && setting.Enabled)
                    return true;
            }
            return false;
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
                || string.Equals(id, "autoplaySwitch", StringComparison.Ordinal)
                || string.Equals(id, "resultScreen", StringComparison.Ordinal);
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

        private static List<RectTransform> ResolveResultScreen()
        {
            _resultScreenRectsBuffer.Clear();

            scrUIController ui = scrUIController.instance;
            if (ui != null)
            {
                if (ui.txtCongrats != null)
                    TryAddRect(_resultScreenRectsBuffer, ui.txtCongrats.rectTransform);
                if (ui.txtAprilCongrats != null)
                    TryAddRect(_resultScreenRectsBuffer, ui.txtAprilCongrats.rectTransform);
                if (ui.txtAllStrictClear != null)
                    TryAddRect(_resultScreenRectsBuffer, ui.txtAllStrictClear.rectTransform);

                AddDetailedResultsRect(ui.txtResults, _resultScreenRectsBuffer);

                EndscreenLanterns[] lanterns = ui.endscreenLanternsSets;
                if (lanterns != null)
                {
                    for (int i = 0; i < lanterns.Length; i++)
                    {
                        EndscreenLanterns lantern = lanterns[i];
                        if (lantern == null)
                            continue;

                        TryAddRect(_resultScreenRectsBuffer, lantern.GetComponent<RectTransform>());
                    }
                }
            }

            scrController controller = scrController.instance;
            if (controller != null)
            {
                if (controller.txtCongrats != null)
                    TryAddRect(_resultScreenRectsBuffer, controller.txtCongrats.rectTransform);
                if (controller.txtAprilCongrats != null)
                    TryAddRect(_resultScreenRectsBuffer, controller.txtAprilCongrats.rectTransform);
                if (controller.txtAllStrictClear != null)
                    TryAddRect(_resultScreenRectsBuffer, controller.txtAllStrictClear.rectTransform);

                AddDetailedResultsRect(controller.detailedResults, _resultScreenRectsBuffer);
            }

            return _resultScreenRectsBuffer;
        }

        private static void AddDetailedResultsRect(DetailedResults detailedResults, List<RectTransform> results)
        {
            if (detailedResults == null || results == null)
                return;

            RectTransform root = detailedResults.GetComponent<RectTransform>();
            if (root != null)
            {
                TryAddRect(results, root);
                return;
            }

            if (detailedResults.textComponent != null)
            {
                TryAddRect(results, detailedResults.textComponent.rectTransform);
            }
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

            Vector2 targetPosition = state.AnchoredPosition + new Vector2(offsetX, offsetY);
            Vector3 targetScale = new Vector3(state.LocalScale.x * scale, state.LocalScale.y * scale, state.LocalScale.z);
            if (!Approximately(rect.anchoredPosition, targetPosition))
            {
                rect.anchoredPosition = targetPosition;
            }
            if (!Approximately(rect.localScale, targetScale))
            {
                rect.localScale = targetScale;
            }

            CanvasGroup group = state.CanvasGroup;
            if (group != null)
            {
                bool targetInteractable = setting.Visible && state.CanvasInteractable;
                bool targetBlocksRaycasts = setting.Visible && state.CanvasBlocksRaycasts;
                if (!Mathf.Approximately(group.alpha, alpha))
                {
                    group.alpha = alpha;
                }
                if (group.interactable != targetInteractable)
                {
                    group.interactable = targetInteractable;
                }
                if (group.blocksRaycasts != targetBlocksRaycasts)
                {
                    group.blocksRaycasts = targetBlocksRaycasts;
                }
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

        private readonly struct TargetStateKey
        {
            public readonly string TargetId;
            public readonly int InstanceId;

            public TargetStateKey(string targetId, int instanceId)
            {
                TargetId = targetId;
                InstanceId = instanceId;
            }
        }

        private sealed class TargetStateKeyComparer : IEqualityComparer<TargetStateKey>
        {
            public static readonly TargetStateKeyComparer Instance = new TargetStateKeyComparer();

            public bool Equals(TargetStateKey a, TargetStateKey b)
            {
                return a.InstanceId == b.InstanceId
                    && string.Equals(a.TargetId, b.TargetId, StringComparison.Ordinal);
            }

            public int GetHashCode(TargetStateKey key)
            {
                unchecked
                {
                    int hash = key.TargetId != null ? key.TargetId.GetHashCode() : 0;
                    return hash * 31 + key.InstanceId;
                }
            }
        }

        // MakeTargetStateId ran per resolved rect per LateUpdate and allocated two strings
        // each time. The result only depends on (targetId, instance id), so memoize it.
        // Instance ids are not reused within a session, but scene reloads create new rects,
        // so the cache is capped and dropped wholesale on overflow (it is pure memoization).
        private const int TargetStateIdCacheLimit = 256;
        private static readonly Dictionary<TargetStateKey, string> _targetStateIdCache
            = new Dictionary<TargetStateKey, string>(TargetStateKeyComparer.Instance);

        private static string MakeTargetStateId(string targetId, RectTransform rect)
        {
            TargetStateKey key = new TargetStateKey(targetId, rect.GetInstanceID());
            if (_targetStateIdCache.TryGetValue(key, out string stateId))
            {
                return stateId;
            }

            if (_targetStateIdCache.Count >= TargetStateIdCacheLimit)
            {
                _targetStateIdCache.Clear();
            }

            stateId = GetTargetPrefix(targetId) + key.InstanceId.ToString();
            _targetStateIdCache[key] = stateId;
            return stateId;
        }

        private void RestoreTargetStates(string targetId)
        {
            if (_states.Count == 0) return;
            string prefix = GetTargetPrefix(targetId);
            _restoreKeysBuffer.Clear();
            foreach (var key in _states.Keys)
            {
                if (string.Equals(key, targetId, StringComparison.Ordinal) || key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    _restoreKeysBuffer.Add(key);
                }
            }

            for (int i = 0; i < _restoreKeysBuffer.Count; i++)
            {
                Restore(_restoreKeysBuffer[i]);
            }
        }

        private void RestoreInactiveTargetStates(string targetId, HashSet<string> activeStateIds)
        {
            if (_states.Count == 0) return;
            string prefix = GetTargetPrefix(targetId);
            _restoreKeysBuffer.Clear();
            foreach (var key in _states.Keys)
            {
                if ((string.Equals(key, targetId, StringComparison.Ordinal) || key.StartsWith(prefix, StringComparison.Ordinal))
                    && (activeStateIds == null || !activeStateIds.Contains(key)))
                {
                    _restoreKeysBuffer.Add(key);
                }
            }

            for (int i = 0; i < _restoreKeysBuffer.Count; i++)
            {
                Restore(_restoreKeysBuffer[i]);
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
                if (!Approximately(graphicState.Graphic.color, color))
                {
                    graphicState.Graphic.color = color;
                }
            }
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.001f && Mathf.Abs(a.y - b.y) < 0.001f;
        }

        private static bool Approximately(Vector3 a, Vector3 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.001f
                && Mathf.Abs(a.y - b.y) < 0.001f
                && Mathf.Abs(a.z - b.z) < 0.001f;
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.001f
                && Mathf.Abs(a.g - b.g) < 0.001f
                && Mathf.Abs(a.b - b.b) < 0.001f
                && Mathf.Abs(a.a - b.a) < 0.001f;
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
