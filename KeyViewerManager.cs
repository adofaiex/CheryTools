using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheryTools
{
    public class KeyDrop
    {
        public KVNode Node;
        public float StartTime;
        public float? EndTime;
        public string RenderId;
    }

    public class KeyViewerManager : MonoBehaviour
    {
        public static KeyViewerManager Instance;

        public int CurrentKPS = 0;
        
        public Dictionary<KVNode, bool> IsNodePressed = new Dictionary<KVNode, bool>();

        private Queue<float> _hitTimestamps = new Queue<float>();
        public List<KeyDrop> ActiveDrops = new List<KeyDrop>();
        private readonly Dictionary<KVNode, string> _cachedKeyBindValues = new Dictionary<KVNode, string>();
        private readonly Dictionary<KVNode, KeyCode> _cachedKeyCodes = new Dictionary<KVNode, KeyCode>();
        private readonly List<KVNode> _activeNodesBuffer = new List<KVNode>();
        private readonly HashSet<KVNode> _activeNodeSet = new HashSet<KVNode>();
        private readonly HashSet<KeyCode> _countedKeyDowns = new HashSet<KeyCode>();
        private int _nextDropRenderId = 1;

        private float _saveTimer = 0f;

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
            // Empty, tracked dynamically in Update now
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
            if (Main.ModEntry != null && Main.Settings != null)
                Main.RequestSave();
        }

        public List<KVNode> GetActiveNodes()
        {
            _activeNodesBuffer.Clear();
            if (Main.Settings == null || Main.Settings.KeyViewerConfigurations == null)
            {
                return _activeNodesBuffer;
            }

            foreach (var config in Main.Settings.KeyViewerConfigurations)
            {
                if (config == null || !config.IsEnabled || config.Nodes == null) continue;
                _activeNodesBuffer.AddRange(config.Nodes);
            }
            return _activeNodesBuffer;
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

        void Update()
        {

            if (!Main.IsEnabled || Main.Settings == null || !Main.Settings.EnableKeyViewer) return;

            float currentTime = Time.unscaledTime;

            var activeNodes = GetActiveNodes();
            _activeNodeSet.Clear();
            if (activeNodes != null)
            {
                foreach (var node in activeNodes)
                {
                    if (node != null) _activeNodeSet.Add(node);
                }

                for (int j = ActiveDrops.Count - 1; j >= 0; j--)
                {
                    if (ActiveDrops[j].EndTime == null && (ActiveDrops[j].Node == null || !_activeNodeSet.Contains(ActiveDrops[j].Node)))
                    {
                        ActiveDrops[j].EndTime = currentTime;
                    }
                }

                _countedKeyDowns.Clear();
                foreach (var node in activeNodes)
                {
                    if (node == null) continue;
                    if (node.NodeType != 0) continue;
                    
                    if (TryGetNodeKeyCode(node, out KeyCode kc))
                    {
                        bool isPressed = Input.GetKey(kc);
                        IsNodePressed[node] = isPressed;

                        if (Input.GetKeyDown(kc))
                        {
                            // 兜底防御：如果因为卡顿导致上次的键雨没有闭合，强制闭合
                            for (int j = ActiveDrops.Count - 1; j >= 0; j--)
                            {
                                if (ActiveDrops[j].Node == node && ActiveDrops[j].EndTime == null)
                                {
                                    ActiveDrops[j].EndTime = currentTime;
                                }
                            }
                            
                            node.HitCount++;
                            if (_countedKeyDowns.Add(kc))
                            {
                                Main.Settings.TotalHits++;
                                _hitTimestamps.Enqueue(currentTime);
                            }
                            ActiveDrops.Add(new KeyDrop { Node = node, StartTime = currentTime, EndTime = null, RenderId = "rain_" + (_nextDropRenderId++).ToString() });
                        }

                        // 使用 !isPressed 替代 GetKeyUp，无视丢帧卡顿，只要按键处于松开状态就强制切断雨滴
                        if (!isPressed)
                        {
                            for (int j = ActiveDrops.Count - 1; j >= 0; j--)
                            {
                                if (ActiveDrops[j].Node == node && ActiveDrops[j].EndTime == null)
                                {
                                    ActiveDrops[j].EndTime = currentTime;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        IsNodePressed[node] = false;
                    }
                }
            }

            if (ActiveDrops.Count > 0)
            {
                for (int i = ActiveDrops.Count - 1; i >= 0; i--)
                {
                    KeyDrop drop = ActiveDrops[i];
                    KVConfiguration config = Main.Settings.FindKeyViewerConfigurationForNode(drop.Node);
                    float speed = config != null && config.EnableKeyRain ? config.KeyRainSpeed : 800.0f;
                    float maxHeight = config != null && config.EnableKeyRain ? config.KeyRainMaxHeight : 400.0f;
                    float maxLifespan = speed > 0f ? (maxHeight + 200f) / speed : 1f;
                    if (drop.EndTime.HasValue && (currentTime - drop.EndTime.Value) > maxLifespan)
                    {
                        ActiveDrops.RemoveAt(i);
                    }
                }
            }

            while (_hitTimestamps.Count > 0 && currentTime - _hitTimestamps.Peek() > 1f)
            {
                _hitTimestamps.Dequeue();
            }
            CurrentKPS = _hitTimestamps.Count;
        }
    }
}
