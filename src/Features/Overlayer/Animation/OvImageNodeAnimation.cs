using System.Collections.Generic;

namespace CheryTools
{
    internal static class OvImageNodeAnimation
    {
        public const string TargetId = "__ov_image__";

        public static OvAnimationGraph CreateDefault()
        {
            OvAnimationGraph graph = OvAnimationGraph.CreateDefault();
            EnsureImageTarget(graph);
            return graph;
        }

        public static void EnsureImageTarget(OvAnimationGraph graph)
        {
            if (graph == null) return;
            graph.Normalize();
            if (graph.Nodes == null) return;
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                OvAnimationNode node = graph.Nodes[i];
                if (node == null || node.Kind != OvAnimationNodeKind.TokenInput) continue;
                EnsureNodeTarget(node);
            }
        }

        public static void EnsureNodeTarget(OvAnimationNode node)
        {
            if (node == null) return;
            if (node.SelectedTokenIds == null) node.SelectedTokenIds = new List<string>();
            if (node.SelectedTokenIds.Count == 1 && node.SelectedTokenIds[0] == TargetId) return;
            node.SelectedTokenIds.Clear();
            node.SelectedTokenIds.Add(TargetId);
        }

        public static OverlayerText CreateRuntimeProxy(OverlayerImage image)
        {
            var proxy = new OverlayerText
            {
                Name = "OV Image Node Animation",
                TokenBindings = new List<OvTextTokenBinding>
                {
                    new OvTextTokenBinding
                    {
                        Id = TargetId,
                        Kind = OvTextTokenKind.Literal,
                        Lexeme = "图片"
                    }
                }
            };
            SyncRuntimeProxy(proxy, image);
            return proxy;
        }

        public static void SyncRuntimeProxy(OverlayerText proxy, OverlayerImage image)
        {
            if (proxy == null || image == null) return;
            if (image.NodeAnimation == null) image.NodeAnimation = CreateDefault();
            proxy.TokenAnimation = image.NodeAnimation;
        }
    }
}
