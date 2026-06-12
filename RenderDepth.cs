using System;

namespace CheryTools
{
    internal static class RenderDepth
    {
        public const int MinDepth = -100;
        public const int MaxDepth = 100;

        public const int LayerBaseSortingOrder = 31960;
        public const int LayerStride = 4;

        public const int SublayerRain = 0;
        public const int SublayerGraphic = 1;
        public const int SublayerText = 3;

        public const int EditOverlaySortingOrder = 32765;
        public const int DefaultTextSortingOrder = 32766;

        public static int ClampDepth(int depth)
        {
            return Math.Max(MinDepth, Math.Min(MaxDepth, depth));
        }

        public static int ToSortingOrder(int depth, int sublayer)
        {
            int safeDepth = ClampDepth(depth);
            int safeSublayer = Math.Max(0, Math.Min(LayerStride - 1, sublayer));
            return LayerBaseSortingOrder + (safeDepth - MinDepth) * LayerStride + safeSublayer;
        }
    }
}
