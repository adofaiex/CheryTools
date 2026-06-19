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
        public string RenderId;
    }

    public class KeyViewerManager : MonoBehaviour
    {
        public static KeyViewerManager Instance;

        public int CurrentKPS = 0;
        
        public Dictionary<KVNode, bool> IsNodePressed = new Dictionary<KVNode, bool>();
        public Dictionary<KVNode, float> KeyPressAnimationProgress = new Dictionary<KVNode, float>();

        private Queue<float> _hitTimestamps = new Queue<float>();
        public List<KeyDrop> ActiveDrops = new List<KeyDrop>();
        private readonly Dictionary<KVNode, string> _cachedKeyBindValues = new Dictionary<KVNode, string>();
        private readonly Dictionary<KVNode, KeyCode> _cachedKeyCodes = new Dictionary<KVNode, KeyCode>();
        private readonly List<KVNode> _activeNodesBuffer = new List<KVNode>();
        private readonly HashSet<KVNode> _activeNodeSet = new HashSet<KVNode>();
        private readonly Dictionary<KVNode, KVConfiguration> _activeNodeConfigMap = new Dictionary<KVNode, KVConfiguration>();
        private readonly HashSet<KeyCode> _countedKeyDowns = new HashSet<KeyCode>();
        private readonly HashSet<KVNode> _pressedNodes = new HashSet<KVNode>();
        private readonly HashSet<KeyCode> _keysToPoll = new HashSet<KeyCode>();
        private readonly Dictionary<KeyCode, KeyPollState> _polledKeys = new Dictionary<KeyCode, KeyPollState>();
        private readonly Dictionary<KVNode, KVConfiguration> _animationNodeConfigMap = new Dictionary<KVNode, KVConfiguration>();
        private readonly List<KVNode> _animationKeysBuffer = new List<KVNode>();
        private int _nextDropRenderId = 1;
        private bool _renderDirty = true;
        private long _activeNodesRevision = -1;
        private long _lastRenderedRevision = -1;

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
            _pressedNodes.RemoveWhere(node => node == null);
            MarkRenderDirty();
        }

        public void ResetCounts()
        {
            if (Main.Settings != null)
            {
                foreach (var n in Main.Settings.GetAllKeyViewerNodes())
                {
                    if (n != null) n.HitCount = 0;
                }
            }
            
            if (Main.Settings != null)
            {
                Main.Settings.TotalHits = 0;
            }
            _hitTimestamps.Clear();
            CurrentKPS = 0;
            ActiveDrops.Clear();
            _pressedNodes.Clear();
            KeyPressAnimationProgress.Clear();
            _animationNodeConfigMap.Clear();
            MarkRenderDirty();
            if (Main.ModEntry != null && Main.Settings != null)
                Main.RequestSave();
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
                float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, animationSettings.Duration);
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

            float currentTime = Time.unscaledTime;
            bool hasInputActivity = Input.anyKey || Input.anyKeyDown;
            if (!hasInputActivity && _pressedNodes.Count == 0 && ActiveDrops.Count == 0 && _hitTimestamps.Count == 0 && KeyPressAnimationProgress.Count == 0)
            {
                if (CurrentKPS != 0)
                {
                    CurrentKPS = 0;
                    MarkRenderDirty();
                }
                return;
            }

            var activeNodes = GetActiveNodes();
            if (activeNodes != null)
            {
                if (ActiveDrops.Count > 0 || _pressedNodes.Count > 0)
                {
                    _activeNodeSet.Clear();
                    foreach (var node in activeNodes)
                    {
                        if (node != null) _activeNodeSet.Add(node);
                    }

                    if (_pressedNodes.Count > 0)
                    {
                        int removedPressed = _pressedNodes.RemoveWhere(node => node == null || !_activeNodeSet.Contains(node));
                        if (removedPressed > 0)
                        {
                            MarkRenderDirty();
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
                _countedKeyDowns.Clear();
                foreach (var node in activeNodes)
                {
                    if (node == null) continue;
                    if (node.NodeType != 0) continue;
                    _activeNodeConfigMap.TryGetValue(node, out KVConfiguration ownerConfig);
                    
                    if (TryGetNodeKeyCode(node, out KeyCode kc))
                    {
                        TryGetPolledKeyState(kc, out KeyPollState keyState);
                        bool isPressed = keyState.IsPressed;
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
                            if (_countedKeyDowns.Add(kc))
                            {
                                Main.Settings.TotalHits++;
                                _hitTimestamps.Enqueue(currentTime);
                            }
                            if (IsKeyRainEnabled(ownerConfig, node))
                            {
                                ActiveDrops.Add(new KeyDrop { Node = node, Config = ownerConfig, StartTime = currentTime, EndTime = null, RenderId = "rain_" + (_nextDropRenderId++).ToString() });
                                MarkRenderDirty();
                            }
                        }

                        // 使用 !isPressed 替代 GetKeyUp，无视丢帧卡顿，只要按键处于松开状态就强制切断雨滴
                        if (!isPressed)
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

            while (_hitTimestamps.Count > 0 && currentTime - _hitTimestamps.Peek() > 1f)
            {
                _hitTimestamps.Dequeue();
            }
            int currentKps = _hitTimestamps.Count;
            if (CurrentKPS != currentKps)
            {
                CurrentKPS = currentKps;
                MarkRenderDirty();
            }
        }
    }
}
