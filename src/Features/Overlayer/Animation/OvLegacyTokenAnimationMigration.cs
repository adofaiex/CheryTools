using System;
using System.Collections.Generic;

namespace CheryTools
{
    internal static class OvLegacyTokenAnimationMigration
    {
        public static bool Migrate(OverlayerText text)
        {
            if (text == null || text.Animations == null || text.Animations.Count == 0) return false;
            OverlayerAnimation legacy = null;
            for (int i = 0; i < text.Animations.Count; i++)
            {
                if (text.Animations[i] != null && text.Animations[i].IsEnabled)
                {
                    legacy = text.Animations[i];
                    break;
                }
            }

            if (legacy == null)
            {
                text.Animations.Clear();
                return true;
            }

            legacy.ParseJson();
            OvAnimationGraph graph = new OvAnimationGraph
            {
                Enabled = true,
                HoldFinalPose = true
            };
            var input = new OvAnimationNode
            {
                Id = OvAnimationGraph.NewId(),
                Kind = OvAnimationNodeKind.TokenInput,
                Name = "All Tokens",
                EditorX = 30f,
                EditorY = 190f,
                SelectedTokenIds = new List<string>()
            };
            if (text.TokenBindings != null)
            {
                for (int i = 0; i < text.TokenBindings.Count; i++)
                {
                    if (text.TokenBindings[i] != null) input.SelectedTokenIds.Add(text.TokenBindings[i].Id);
                }
            }
            var trigger = new OvAnimationNode
            {
                Id = OvAnimationGraph.NewId(),
                Kind = OvAnimationNodeKind.Trigger,
                Name = "Legacy Trigger",
                Trigger = legacy.Trigger == AnimationTrigger.OnComboIncrease
                    ? OvAnimationTriggerKind.ComboIncrease
                    : OvAnimationTriggerKind.AnyKeyDown,
                EditorX = 30f,
                EditorY = 40f
            };
            graph.Nodes.Add(input);
            graph.Nodes.Add(trigger);

            List<JsonAnimFrame> frames = legacy.ParsedFrames;
            if (frames == null || frames.Count < 2)
            {
                frames = new List<JsonAnimFrame>
                {
                    new JsonAnimFrame { time = 0f, x = legacy.StartX, y = legacy.StartY, zoomx = legacy.StartScale, zoomy = legacy.StartScale, easing = "linear" },
                    new JsonAnimFrame { time = Math.Max(0.01f, legacy.Duration), x = legacy.EndX, y = legacy.EndY, zoomx = legacy.EndScale, zoomy = legacy.EndScale, easing = legacy.EasingType }
                };
            }

            string previousPositionNode = trigger.Id;
            string previousScaleNode = trigger.Id;
            float x = frames[0].x ?? 0f;
            float y = frames[0].y ?? 0f;
            float scaleX = frames[0].zoomx ?? 1f;
            float scaleY = frames[0].zoomy ?? scaleX;
            for (int i = 1; i < frames.Count; i++)
            {
                JsonAnimFrame frame = frames[i];
                JsonAnimFrame previous = frames[i - 1];
                float nextX = frame.x ?? x;
                float nextY = frame.y ?? y;
                float nextScaleX = frame.zoomx ?? scaleX;
                float nextScaleY = frame.zoomy ?? scaleY;
                float duration = Math.Max(0.01f, frame.time - previous.time);
                string easing = string.IsNullOrEmpty(frame.easing) ? "linear" : frame.easing;

                if (Math.Abs(nextX - x) > 0.0001f || Math.Abs(nextY - y) > 0.0001f || i == 1)
                {
                    var move = NewTween(OvTokenTweenProperty.Position, x, y, nextX, nextY, duration, easing, 330f + (i - 1) * 260f, 55f);
                    graph.Nodes.Add(move);
                    graph.Links.Add(OvAnimationGraph.NewLink(previousPositionNode, "flow", move.Id, "flow"));
                    graph.Links.Add(OvAnimationGraph.NewLink(input.Id, "targets", move.Id, "targets"));
                    previousPositionNode = move.Id;
                }
                if (Math.Abs(nextScaleX - scaleX) > 0.0001f || Math.Abs(nextScaleY - scaleY) > 0.0001f || i == 1)
                {
                    var scale = NewTween(OvTokenTweenProperty.Scale, scaleX, scaleY, nextScaleX, nextScaleY, duration, easing, 330f + (i - 1) * 260f, 215f);
                    graph.Nodes.Add(scale);
                    graph.Links.Add(OvAnimationGraph.NewLink(previousScaleNode, "flow", scale.Id, "flow"));
                    graph.Links.Add(OvAnimationGraph.NewLink(input.Id, "targets", scale.Id, "targets"));
                    previousScaleNode = scale.Id;
                }

                x = nextX;
                y = nextY;
                scaleX = nextScaleX;
                scaleY = nextScaleY;
            }

            text.TokenAnimation = graph;
            text.Animations.Clear();
            return true;
        }

        private static OvAnimationNode NewTween(OvTokenTweenProperty property, float fromX, float fromY, float toX, float toY,
            float duration, string easing, float editorX, float editorY)
        {
            return new OvAnimationNode
            {
                Id = OvAnimationGraph.NewId(),
                Kind = OvAnimationNodeKind.Tween,
                Name = property.ToString(),
                TweenProperty = property,
                FromX = fromX,
                FromY = fromY,
                ToX = toX,
                ToY = toY,
                Duration = duration,
                Easing = easing,
                EditorX = editorX,
                EditorY = editorY
            };
        }
    }
}
