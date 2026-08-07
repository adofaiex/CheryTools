using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheryTools
{
    public class KeyDrop
    {
        public KVNode Node;
        public KVConfiguration Config;
        public float StartTime;
        public float? EndTime;
    }

    public class KeyViewerManager : MonoBehaviour
    {
        public static KeyViewerManager Instance;

        public Dictionary<KVNode, bool> IsNodePressed = new Dictionary<KVNode, bool>();
        public Dictionary<KVNode, float> KeyPressAnimationProgress = new Dictionary<KVNode, float>();

        private readonly Dictionary<KVConfiguration, Queue<float>> _hitTimestampsByConfig = new Dictionary<KVConfiguration, Queue<float>>();
        private readonly Dictionary<KVConfiguration, int> _currentKpsByConfig = new Dictionary<KVConfiguration, int>();
        public List<KeyDrop> ActiveDrops = new List<KeyDrop>();
        private readonly Dictionary<KVNode, string> _cachedKeyBindValues = new Dictionary<KVNode, string>();
        private readonly Dictionary<KVNode, KeyCode> _cachedKeyCodes = new Dictionary<KVNode, KeyCode>();
        private readonly List<KVNode> _activeNodesBuffer = new List<KVNode>();
        private readonly HashSet<KVNode> _activeNodeSet = new HashSet<KVNode>();
        private readonly Dictionary<KVNode, KVConfiguration> _activeNodeConfigMap = new Dictionary<KVNode, KVConfiguration>();
        private readonly Dictionary<KVConfiguration, HashSet<KeyCode>> _countedKeyDownsByConfig = new Dictionary<KVConfiguration, HashSet<KeyCode>>();
        private readonly HashSet<KVNode> _pressedNodes = new HashSet<KVNode>();
        private readonly HashSet<KeyCode> _keysToPoll = new HashSet<KeyCode>();
        private readonly Dictionary<KeyCode, KeyPollState> _polledKeys = new Dictionary<KeyCode, KeyPollState>();
        private readonly Dictionary<KVNode, float> _visualPressedUntil = new Dictionary<KVNode, float>();
        private readonly Dictionary<KVNode, KVConfiguration> _animationNodeConfigMap = new Dictionary<KVNode, KVConfiguration>();
        private readonly List<KVNode> _animationKeysBuffer = new List<KVNode>();
        private readonly List<KVNode> _visualPressedKeysBuffer = new List<KVNode>();
        private readonly List<KVNode> _pressedRemovalBuffer = new List<KVNode>();
        private bool _renderDirty = true;
        private long _activeNodesRevision = -1;
        private long _lastRenderedRevision = -1;
        private float _nextKpsUpdateTime = 0f;

        private float _saveTimer = 0f;

        private struct KeyPollState
        {
            public bool IsPressed;
            public bool IsDown;
        }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Main.ModEntry != null && Main.Settings != null)
                Main.RequestSave();
        }

        public void RefreshKeys()
        {
            _cachedKeyBindValues.Clear();
            _cachedKeyCodes.Clear();
            _activeNodesRevision = -1;
            _activeNodesBuffer.Clear();
            _activeNodeConfigMap.Clear();
            _activeNodeSet.Clear();
            _keysToPoll.Clear();
            _polledKeys.Clear();
            _animationNodeConfigMap.Clear();
            KeyPressAnimationProgress.Clear();
            _visualPressedUntil.Clear();
            _pressedNodes.RemoveWhere(node => node == null);
            MarkRenderDirty();
        }

        public int GetCurrentKps(KVConfiguration config)
        {
            return config != null && _currentKpsByConfig.TryGetValue(config, out int value) ? value : 0;
        }

        public void ResetCounts(KVConfiguration config)
        {
            if (config != null)
            {
                if (config.Nodes != null)
                {
                    foreach (KVNode node in config.Nodes) if (node != null) node.HitCount = 0;
                }
                config.TotalHits = 0;
                _hitTimestampsByConfig.Remove(config);
                _currentKpsByConfig.Remove(config);
                _countedKeyDownsByConfig.Remove(config);
            }
            ActiveDrops.Clear();
            _pressedNodes.Clear();
            KeyPressAnimationProgress.Clear();
            _visualPressedUntil.Clear();
            _animationNodeConfigMap.Clear();
            _nextKpsUpdateTime = 0f;
            MarkRenderDirty();
            if (Main.ModEntry != null && Main.Settings != null)
                Main.RequestSave();
        }

        public void ResetAllCounts()
        {
            if (Main.Settings != null && Main.Settings.KeyViewerConfigurations != null)
            {
                foreach (KVConfiguration config in Main.Settings.KeyViewerConfigurations)
                {
                    if (config == null) continue;
                    if (config.Nodes != null)
                        foreach (KVNode node in config.Nodes) if (node != null) node.HitCount = 0;
                    config.TotalHits = 0;
                }
            }
            _hitTimestampsByConfig.Clear();
            _currentKpsByConfig.Clear();
            _countedKeyDownsByConfig.Clear();
            ActiveDrops.Clear();
            _pressedNodes.Clear();
            KeyPressAnimationProgress.Clear();
            _visualPressedUntil.Clear();
            _animationNodeConfigMap.Clear();
            _nextKpsUpdateTime = 0f;
            MarkRenderDirty();
            if (Main.ModEntry != null && Main.Settings != null) Main.RequestSave();
        }

        public List<KVNode> GetActiveNodes()
        {
            long revision = OverlayRenderInvalidator.Revision;
            if (_activeNodesRevision == revision)
            {
                return _activeNodesBuffer;
            }

            _activeNodesBuffer.Clear();
            _activeNodeConfigMap.Clear();
            if (Main.Settings == null || Main.Settings.KeyViewerConfigurations == null)
            {
                _activeNodesRevision = revision;
                return _activeNodesBuffer;
            }

            foreach (var config in Main.Settings.KeyViewerConfigurations)
            {
                if (config == null || !config.IsEnabled || config.Nodes == null) continue;
                foreach (var node in config.Nodes)
                {
                    if (node == null) continue;
                    _activeNodesBuffer.Add(node);
                    _activeNodeConfigMap[node] = config;
                }
            }
            _activeNodesRevision = revision;
            return _activeNodesBuffer;
        }

        public bool ShouldUpdateOverlay(float now, float rate)
        {
            if (_renderDirty || _lastRenderedRevision != OverlayRenderInvalidator.Revision)
            {
                return true;
            }

            return HasActiveKeyRain() || HasActiveKeyPressAnimation();
        }

        public void MarkOverlayRendered()
        {
            _renderDirty = false;
            _lastRenderedRevision = OverlayRenderInvalidator.Revision;
        }

        private void MarkRenderDirty()
        {
            _renderDirty = true;
        }

        private static float GetKpsRefreshInterval()
        {
            if (Main.Settings == null)
            {
                return 0.25f;
            }

            float interval = Main.Settings.KeyViewerKpsRefreshInterval;
            if (interval <= 0f || float.IsNaN(interval) || float.IsInfinity(interval))
            {
                interval = 0.25f;
            }
            return Math.Max(0.05f, Math.Min(2.0f, interval));
        }

        private static float GetPressedVisualHoldDuration()
        {
            return 0.05f;
        }

        private bool HasActiveKeyRain()
        {
            if (ActiveDrops.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < ActiveDrops.Count; i++)
            {
                KeyDrop drop = ActiveDrops[i];
                if (drop == null) continue;
                if (IsKeyRainEnabled(drop.Config, drop.Node))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsKeyRainEnabled(KVConfiguration config, KVNode node)
        {
            if (node == null || node.NodeType != 0)
            {
                return false;
            }

            return node.UseCustomRain ? node.EnableKeyRain : (config != null && config.EnableKeyRain);
        }

        private static float ResolveKeyRainSpeed(KVConfiguration config, KVNode node)
        {
            return config != null ? config.KeyRainSpeed : 800.0f;
        }

        private static float ResolveKeyRainMaxHeight(KVConfiguration config, KVNode node)
        {
            return config != null ? config.KeyRainMaxHeight : 400.0f;
        }

        private bool HasActiveKeyPressAnimation()
        {
            if (KeyPressAnimationProgress.Count == 0)
            {
                return false;
            }

            foreach (var pair in KeyPressAnimationProgress)
            {
                float progress = pair.Value;
                if (progress > 0.0001f && progress < 0.9999f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsKeyPressAnimationEnabled(KVConfiguration config, KVNode node)
        {
            KeyPressAnimationSettings settings = KeyPressAnimationSettings.Resolve(config, node);
            return settings.Enabled && settings.Duration > 0f;
        }

        private void AdvanceKeyPressAnimations(List<KVNode> activeNodes)
        {
            if (activeNodes == null || activeNodes.Count == 0)
            {
                if (KeyPressAnimationProgress.Count > 0 || _animationNodeConfigMap.Count > 0)
                {
                    KeyPressAnimationProgress.Clear();
                    _animationNodeConfigMap.Clear();
                    MarkRenderDirty();
                }
                return;
            }

            _activeNodeSet.Clear();
            for (int i = 0; i < activeNodes.Count; i++)
            {
                KVNode node = activeNodes[i];
                if (node != null)
                {
                    _activeNodeSet.Add(node);
                }
            }

            bool changed = false;
            _animationKeysBuffer.Clear();
            foreach (var pair in KeyPressAnimationProgress)
            {
                _animationKeysBuffer.Add(pair.Key);
            }
            for (int i = 0; i < _animationKeysBuffer.Count; i++)
            {
                KVNode node = _animationKeysBuffer[i];
                if (node == null || !_activeNodeSet.Contains(node))
                {
                    KeyPressAnimationProgress.Remove(node);
                    _animationNodeConfigMap.Remove(node);
                    changed = true;
                }
            }

            for (int i = 0; i < activeNodes.Count; i++)
            {
                KVNode node = activeNodes[i];
                if (node == null || node.NodeType != 0) continue;
                _activeNodeConfigMap.TryGetValue(node, out KVConfiguration ownerConfig);
                KeyPressAnimationSettings animationSettings = KeyPressAnimationSettings.Resolve(ownerConfig, node);
                if (!animationSettings.Enabled || animationSettings.Duration <= 0f)
                {
                    if (KeyPressAnimationProgress.Remove(node))
                    {
                        _animationNodeConfigMap.Remove(node);
                        changed = true;
                    }
                    continue;
                }

                bool pressed = false;
                IsNodePressed.TryGetValue(node, out pressed);
                float current = 0f;
                KeyPressAnimationProgress.TryGetValue(node, out current);
                float target = pressed ? 1f : 0f;
                float step = RenderTimelineClock.DeltaTime / Mathf.Max(0.01f, animationSettings.Duration);
                float next = Mathf.MoveTowards(current, target, step);
                if (Mathf.Abs(next - current) > 0.0001f)
                {
                    KeyPressAnimationProgress[node] = next;
                    _animationNodeConfigMap[node] = ownerConfig;
                    changed = true;
                }
                else if (next > 0.0001f || pressed)
                {
                    KeyPressAnimationProgress[node] = next;
                    _animationNodeConfigMap[node] = ownerConfig;
                }
                else if (KeyPressAnimationProgress.Remove(node))
                {
                    _animationNodeConfigMap.Remove(node);
                    changed = true;
                }
            }

            if (changed)
            {
                MarkRenderDirty();
            }
        }

        public List<KVNode> GetEditingNodes()
        {
            return Main.Settings != null ? Main.Settings.GetSelectedKeyViewerNodes() : null;
        }

        private bool TryGetNodeKeyCode(KVNode node, out KeyCode keyCode)
        {
            keyCode = KeyCode.None;
            if (node == null) return false;

            string keyBind = node.KeyBind ?? string.Empty;
            if (_cachedKeyBindValues.TryGetValue(node, out string cachedBind)
                && string.Equals(cachedBind, keyBind, StringComparison.Ordinal)
                && _cachedKeyCodes.TryGetValue(node, out keyCode))
            {
                return keyCode != KeyCode.None;
            }

            if (!System.Enum.TryParse(keyBind, true, out keyCode))
            {
                keyCode = KeyCode.None;
            }

            _cachedKeyBindValues[node] = keyBind;
            _cachedKeyCodes[node] = keyCode;
            return keyCode != KeyCode.None;
        }

        private static bool SafeGetKey(KeyCode keyCode)
        {
            try
            {
                return Input.GetKey(keyCode);
            }
            catch
            {
                return false;
            }
        }

        private static bool SafeGetKeyDown(KeyCode keyCode)
        {
            try
            {
                return Input.GetKeyDown(keyCode);
            }
            catch
            {
                return false;
            }
        }

        private void PollActiveKeyStates(List<KVNode> activeNodes)
        {
            _keysToPoll.Clear();
            _polledKeys.Clear();

            if (activeNodes == null) return;

            for (int i = 0; i < activeNodes.Count; i++)
            {
                KVNode node = activeNodes[i];
                if (node == null || node.NodeType != 0) continue;
                if (TryGetNodeKeyCode(node, out KeyCode keyCode))
                {
                    _keysToPoll.Add(keyCode);
                }
            }

            foreach (KeyCode keyCode in _keysToPoll)
            {
                _polledKeys[keyCode] = new KeyPollState
                {
                    IsPressed = SafeGetKey(keyCode),
                    IsDown = SafeGetKeyDown(keyCode)
                };
            }
        }

        private bool TryGetPolledKeyState(KeyCode keyCode, out KeyPollState state)
        {
            return _polledKeys.TryGetValue(keyCode, out state);
        }

        void Update()
        {

            if (!Main.IsEnabled || Main.Settings == null || !Main.Settings.EnableKeyViewer) return;

            // Key rain, visual hold, KPS windows and key press interpolation share
            // the captured gameplay timeline. Editor Tweaks fixes Time.deltaTime
            // while rendering, so these values remain frame-accurate in exports.
            float currentTime = RenderTimelineClock.Time;
            bool hasInputActivity = Input.anyKey || Input.anyKeyDown;
            if (!hasInputActivity && _pressedNodes.Count == 0 && ActiveDrops.Count == 0
                && !HasPendingKpsSamples() && KeyPressAnimationProgress.Count == 0 && _visualPressedUntil.Count == 0)
            {
                return;
            }

            var activeNodes = GetActiveNodes();
            if (activeNodes != null)
            {
                if (ActiveDrops.Count > 0 || _pressedNodes.Count > 0 || _visualPressedUntil.Count > 0)
                {
                    _activeNodeSet.Clear();
                    foreach (var node in activeNodes)
                    {
                        if (node != null) _activeNodeSet.Add(node);
                    }

                    if (_pressedNodes.Count > 0)
                    {
                        // Manual two-pass removal instead of RemoveWhere: the lambda
                        // would capture _activeNodeSet and allocate every frame.
                        _pressedRemovalBuffer.Clear();
                        foreach (KVNode node in _pressedNodes)
                        {
                            if (node == null || !_activeNodeSet.Contains(node))
                            {
                                _pressedRemovalBuffer.Add(node);
                            }
                        }
                        if (_pressedRemovalBuffer.Count > 0)
                        {
                            for (int i = 0; i < _pressedRemovalBuffer.Count; i++)
                            {
                                _pressedNodes.Remove(_pressedRemovalBuffer[i]);
                            }
                            MarkRenderDirty();
                        }
                    }

                    if (_visualPressedUntil.Count > 0)
                    {
                        _visualPressedKeysBuffer.Clear();
                        foreach (var pair in _visualPressedUntil)
                        {
                            _visualPressedKeysBuffer.Add(pair.Key);
                        }

                        for (int i = 0; i < _visualPressedKeysBuffer.Count; i++)
                        {
                            KVNode node = _visualPressedKeysBuffer[i];
                            if (node == null || !_activeNodeSet.Contains(node))
                            {
                                _visualPressedUntil.Remove(node);
                                MarkRenderDirty();
                            }
                        }
                    }

                    for (int j = ActiveDrops.Count - 1; j >= 0; j--)
                    {
                        if (ActiveDrops[j].EndTime == null && (ActiveDrops[j].Node == null || !_activeNodeSet.Contains(ActiveDrops[j].Node)))
                        {
                            ActiveDrops[j].EndTime = currentTime;
                            MarkRenderDirty();
                        }
                    }
                }

                PollActiveKeyStates(activeNodes);
                foreach (HashSet<KeyCode> counted in _countedKeyDownsByConfig.Values) counted.Clear();
                foreach (var node in activeNodes)
                {
                    if (node == null) continue;
                    if (node.NodeType != 0) continue;
                    _activeNodeConfigMap.TryGetValue(node, out KVConfiguration ownerConfig);
                    
                    if (TryGetNodeKeyCode(node, out KeyCode kc))
                    {
                        TryGetPolledKeyState(kc, out KeyPollState keyState);
                        bool physicallyPressed = keyState.IsPressed;
                        if (keyState.IsDown)
                        {
                            float holdUntil = currentTime + GetPressedVisualHoldDuration();
                            if (!_visualPressedUntil.TryGetValue(node, out float existingHoldUntil) || holdUntil > existingHoldUntil)
                            {
                                _visualPressedUntil[node] = holdUntil;
                            }
                        }

                        bool heldByVisualWindow = _visualPressedUntil.TryGetValue(node, out float visualPressedUntil) && currentTime < visualPressedUntil;
                        if (!physicallyPressed && !heldByVisualWindow)
                        {
                            _visualPressedUntil.Remove(node);
                        }

                        bool isPressed = physicallyPressed || heldByVisualWindow;
                        if (!IsNodePressed.TryGetValue(node, out bool wasPressed) || wasPressed != isPressed)
                        {
                            MarkRenderDirty();
                        }
                        IsNodePressed[node] = isPressed;
                        if (isPressed)
                        {
                            _pressedNodes.Add(node);
                        }
                        else
                        {
                            _pressedNodes.Remove(node);
                        }

                        if (keyState.IsDown)
                        {
                            // 兜底防御：如果因为卡顿导致上次的键雨没有闭合，强制闭合
                            for (int j = ActiveDrops.Count - 1; j >= 0; j--)
                            {
                                if (ActiveDrops[j].Node == node && ActiveDrops[j].EndTime == null)
                                {
                                    ActiveDrops[j].EndTime = currentTime;
                                    MarkRenderDirty();
                                }
                            }
                            
                            node.HitCount++;
                            if (ownerConfig != null && GetCountedKeys(ownerConfig).Add(kc))
                            {
                                ownerConfig.TotalHits++;
                                GetHitTimestamps(ownerConfig).Enqueue(currentTime);
                            }
                            if (IsKeyRainEnabled(ownerConfig, node))
                            {
                                ActiveDrops.Add(new KeyDrop { Node = node, Config = ownerConfig, StartTime = currentTime, EndTime = null });
                                MarkRenderDirty();
                            }
                        }

                        // 使用 !isPressed 替代 GetKeyUp，无视丢帧卡顿，只要按键处于松开状态就强制切断雨滴
                        if (!physicallyPressed)
                        {
                            for (int j = ActiveDrops.Count - 1; j >= 0; j--)
                            {
                                if (ActiveDrops[j].Node == node && ActiveDrops[j].EndTime == null)
                                {
                                    ActiveDrops[j].EndTime = currentTime;
                                    MarkRenderDirty();
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (!IsNodePressed.TryGetValue(node, out bool wasPressed) || wasPressed)
                        {
                            MarkRenderDirty();
                        }
                        IsNodePressed[node] = false;
                        _pressedNodes.Remove(node);
                        _visualPressedUntil.Remove(node);
                    }
                }
                AdvanceKeyPressAnimations(activeNodes);
            }

            if (ActiveDrops.Count > 0)
            {
                for (int i = ActiveDrops.Count - 1; i >= 0; i--)
                {
                    KeyDrop drop = ActiveDrops[i];
                    KVConfiguration config = drop.Config;
                    float speed = ResolveKeyRainSpeed(config, drop.Node);
                    float maxHeight = ResolveKeyRainMaxHeight(config, drop.Node);
                    float maxLifespan = speed > 0f ? (maxHeight + 200f) / speed : 1f;
                    if (drop.EndTime.HasValue && (currentTime - drop.EndTime.Value) > maxLifespan)
                    {
                        ActiveDrops.RemoveAt(i);
                        MarkRenderDirty();
                    }
                }
            }

            if (currentTime >= _nextKpsUpdateTime)
            {
                bool kpsChanged = false;
                foreach (KeyValuePair<KVConfiguration, Queue<float>> pair in _hitTimestampsByConfig)
                {
                    Queue<float> timestamps = pair.Value;
                    while (timestamps.Count > 0 && currentTime - timestamps.Peek() > 1f) timestamps.Dequeue();
                    int previous = GetCurrentKps(pair.Key);
                    if (previous != timestamps.Count)
                    {
                        _currentKpsByConfig[pair.Key] = timestamps.Count;
                        kpsChanged = true;
                    }
                }
                if (kpsChanged) MarkRenderDirty();
                _nextKpsUpdateTime = currentTime + GetKpsRefreshInterval();
            }
        }

        private Queue<float> GetHitTimestamps(KVConfiguration config)
        {
            if (!_hitTimestampsByConfig.TryGetValue(config, out Queue<float> queue))
            {
                queue = new Queue<float>();
                _hitTimestampsByConfig[config] = queue;
            }
            return queue;
        }

        private HashSet<KeyCode> GetCountedKeys(KVConfiguration config)
        {
            if (!_countedKeyDownsByConfig.TryGetValue(config, out HashSet<KeyCode> keys))
            {
                keys = new HashSet<KeyCode>();
                _countedKeyDownsByConfig[config] = keys;
            }
            return keys;
        }

        private bool HasPendingKpsSamples()
        {
            foreach (Queue<float> queue in _hitTimestampsByConfig.Values)
                if (queue != null && queue.Count > 0) return true;
            return false;
        }
    }
}
