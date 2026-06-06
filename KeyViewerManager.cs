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
    }

    public class KeyViewerManager : MonoBehaviour
    {
        public static KeyViewerManager Instance;

        public int CurrentKPS = 0;
        
        public Dictionary<KVNode, bool> IsNodePressed = new Dictionary<KVNode, bool>();

        private Queue<float> _hitTimestamps = new Queue<float>();
        public List<KeyDrop> ActiveDrops = new List<KeyDrop>();

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
            if (Main.Settings.Layout16K != null) foreach (var n in Main.Settings.Layout16K) n.HitCount = 0;
            if (Main.Settings.Layout12K != null) foreach (var n in Main.Settings.Layout12K) n.HitCount = 0;
            if (Main.Settings.Layout10K != null) foreach (var n in Main.Settings.Layout10K) n.HitCount = 0;
            if (Main.Settings.Layout8K != null) foreach (var n in Main.Settings.Layout8K) n.HitCount = 0;
            
            Main.Settings.TotalHits = 0;
            _hitTimestamps.Clear();
            CurrentKPS = 0;
            ActiveDrops.Clear();
            if (Main.ModEntry != null && Main.Settings != null)
                Main.RequestSave();
        }

        public List<KVNode> GetActiveNodes()
        {
            switch (Main.Settings.KeyViewerLayoutTab)
            {
                case 0: return Main.Settings.Layout16K;
                case 1: return Main.Settings.Layout12K;
                case 2: return Main.Settings.Layout10K;
                case 3: return Main.Settings.Layout8K;
                default: return Main.Settings.Layout16K;
            }
        }

        void Update()
        {

            if (!Main.IsEnabled || !Main.Settings.EnableKeyViewer) return;

            float currentTime = Time.unscaledTime;

            var activeNodes = GetActiveNodes();
            if (activeNodes != null)
            {
                foreach (var node in activeNodes)
                {
                    if (node.NodeType != 0) continue;
                    
                    if (System.Enum.TryParse(node.KeyBind, true, out KeyCode kc) && kc != KeyCode.None)
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
                            Main.Settings.TotalHits++;
                            _hitTimestamps.Enqueue(currentTime);
                            ActiveDrops.Add(new KeyDrop { Node = node, StartTime = currentTime, EndTime = null });
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

            if (Main.Settings.EnableKeyRain && Main.Settings.KeyRainSpeed > 0)
            {
                float maxLifespan = (Main.Settings.KeyRainMaxHeight + 200) / Main.Settings.KeyRainSpeed;
                ActiveDrops.RemoveAll(d => d.EndTime.HasValue && (currentTime - d.EndTime.Value) > maxLifespan);
            }

            while (_hitTimestamps.Count > 0 && currentTime - _hitTimestamps.Peek() > 1f)
            {
                _hitTimestamps.Dequeue();
            }
            CurrentKPS = _hitTimestamps.Count;
        }
    }
}
