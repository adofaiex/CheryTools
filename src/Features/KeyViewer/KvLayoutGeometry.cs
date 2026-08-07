using System;
using System.Collections.Generic;

namespace CheryTools
{
    internal readonly struct KvLayoutBounds
    {
        public readonly float MinX;
        public readonly float MinY;
        public readonly float MaxX;
        public readonly float MaxY;

        public float CenterX => (MinX + MaxX) * 0.5f;
        public float CenterY => (MinY + MaxY) * 0.5f;

        public KvLayoutBounds(float minX, float minY, float maxX, float maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }
    }

    internal static class KvLayoutGeometry
    {
        public static bool TryGetBounds(IList<KVNode> nodes, out KvLayoutBounds bounds)
        {
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            bool found = false;

            if (nodes != null)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    KVNode node = nodes[i];
                    if (node == null) continue;

                    float nodeScale = IsFinite(node.Scale) ? node.Scale : 1f;
                    float x2 = node.PositionX + node.Width * nodeScale;
                    float y2 = node.PositionY + node.Height * nodeScale;
                    float left = Math.Min(node.PositionX, x2);
                    float top = Math.Min(node.PositionY, y2);
                    float right = Math.Max(node.PositionX, x2);
                    float bottom = Math.Max(node.PositionY, y2);
                    if (!IsFinite(left) || !IsFinite(top) || !IsFinite(right) || !IsFinite(bottom))
                        continue;

                    minX = Math.Min(minX, left);
                    minY = Math.Min(minY, top);
                    maxX = Math.Max(maxX, right);
                    maxY = Math.Max(maxY, bottom);
                    found = true;
                }
            }

            bounds = found
                ? new KvLayoutBounds(minX, minY, maxX, maxY)
                : new KvLayoutBounds(0f, 0f, 0f, 0f);
            return found;
        }

        public static void SetScaleAroundCenter(KVConfiguration config, float newScale)
        {
            if (config == null) return;

            float oldScale = IsFinite(config.Scale) && Math.Abs(config.Scale) > 0.0001f
                ? config.Scale
                : 1f;
            float safeNewScale = IsFinite(newScale) && Math.Abs(newScale) > 0.0001f
                ? newScale
                : oldScale;

            if (Math.Abs(safeNewScale - oldScale) < 0.0001f)
            {
                config.Scale = safeNewScale;
                return;
            }

            if (TryGetBounds(config.Nodes, out KvLayoutBounds bounds))
            {
                float positionFactor = oldScale / safeNewScale - 1f;
                float offsetX = bounds.CenterX * positionFactor;
                float offsetY = bounds.CenterY * positionFactor;
                for (int i = 0; i < config.Nodes.Count; i++)
                {
                    KVNode node = config.Nodes[i];
                    if (node == null) continue;
                    node.PositionX += offsetX;
                    node.PositionY += offsetY;
                }
            }

            config.Scale = safeNewScale;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
