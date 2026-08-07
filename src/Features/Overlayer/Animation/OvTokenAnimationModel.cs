using System;
using System.Collections.Generic;

namespace CheryTools
{
    public enum OvTextTokenKind
    {
        Literal = 0,
        DynamicTag = 1,
        Whitespace = 2,
        LineBreak = 3
    }

    public enum OvAnimationNodeKind
    {
        TokenInput = 0,
        Trigger = 1,
        Tween = 2,
        Modify = 3,
        StateCondition = 4,
        Effect = 5,
        NumberFormat = 6,
        GroupFrame = 7,
        ColorChange = 8
    }

    public enum OvAnimationTriggerKind
    {
        Manual = 0,
        AnyKeyDown = 1,
        ComboIncrease = 2,
        SpecificKey = 3,
        JudgementOccurred = 4,
        ComboBreak = 5,
        Beat = 6,
        LevelStart = 7,
        LevelEnd = 8,
        TagValueChanged = 9,
        AutoplayState = 10,
        NoFailState = 11,
        JudgementModeState = 12,
        TagNumericCondition = 13
    }

    public enum OvTagNumericCompareKind
    {
        GreaterThan = 0,
        GreaterOrEqual = 1,
        LessThan = 2,
        LessOrEqual = 3,
        Equal = 4,
        NotEqual = 5,
        InRange = 6,
        OutsideRange = 7
    }

    public enum OvComboCounterKind
    {
        Pure = 0,
        Perfect = 1
    }

    public enum OvStateConditionKind
    {
        Autoplay = 0,
        NoFail = 1,
        JudgementMode = 2
    }

    public enum OvJudgementMode
    {
        Lenient = 0,
        Normal = 1,
        Strict = 2
    }

    public enum OvTokenTweenProperty
    {
        Position = 0,
        Scale = 1,
        Rotation = 2,
        Opacity = 3
    }

    public enum OvTokenEffectKind
    {
        ColorMap = 0
    }

    public enum OvEffectValueSourceKind
    {
        Tag = 0,
        Constant = 1
    }

    public enum OvEffectValueInterpretation
    {
        Auto = 0,
        Number = 1,
        Percentage = 2
    }

    public enum OvColorMapMode
    {
        Gradient = 0,
        Step = 1
    }

    public enum OvNumberFormatKind
    {
        Plain = 0,
        Percentage = 1,
        FixedDecimals = 2,
        ZeroPaddedInteger = 3
    }

    public enum OvPercentageInputKind
    {
        Auto = 0,
        Ratio = 1,
        Percent = 2
    }

    [Serializable]
    public sealed class OvColorPoint
    {
        public float Value;
        public float[] TextColor = new float[] { 1f, 1f, 1f, 1f };
        public float[] OutlineColor = new float[] { 0f, 0f, 0f, 1f };
        public float[] ShadowColor = new float[] { 0f, 0f, 0f, 1f };

        // v6 的未发布草稿只保存了一组 Color；保留该字段用于本地配置迁移。
        public float[] Color;

        public bool ShouldSerializeColor()
        {
            return false;
        }

        public static OvColorPoint Clone(OvColorPoint source)
        {
            return new OvColorPoint
            {
                Value = source != null ? source.Value : 0f,
                TextColor = CloneColor(source != null ? source.TextColor : null, 1f, 1f, 1f, 1f),
                OutlineColor = CloneColor(source != null ? source.OutlineColor : null, 0f, 0f, 0f, 1f),
                ShadowColor = CloneColor(source != null ? source.ShadowColor : null, 0f, 0f, 0f, 1f)
            };
        }

        private static float[] CloneColor(float[] source, float r, float g, float b, float a)
        {
            return source != null && source.Length >= 4
                ? (float[])source.Clone()
                : new float[] { r, g, b, a };
        }
    }

    [Serializable]
    public sealed class OvTextTokenBinding
    {
        public string Id = string.Empty;
        public OvTextTokenKind Kind = OvTextTokenKind.Literal;
        public string Lexeme = string.Empty;

        public static List<OvTextTokenBinding> CloneList(List<OvTextTokenBinding> source)
        {
            var result = new List<OvTextTokenBinding>();
            if (source == null) return result;
            for (int i = 0; i < source.Count; i++)
            {
                OvTextTokenBinding token = source[i];
                if (token == null) continue;
                result.Add(new OvTextTokenBinding { Id = token.Id, Kind = token.Kind, Lexeme = token.Lexeme });
            }
            return result;
        }
    }

    [Serializable]
    public sealed class OvAnimationLink
    {
        public string Id = string.Empty;
        public string FromNodeId = string.Empty;
        public string FromPort = string.Empty;
        public string ToNodeId = string.Empty;
        public string ToPort = string.Empty;
    }

    [Serializable]
    public sealed class OvAnimationNode
    {
        public string Id = string.Empty;
        public OvAnimationNodeKind Kind = OvAnimationNodeKind.TokenInput;
        public string Name = string.Empty;
        public float EditorX;
        public float EditorY;
        public float EditorWidth = 420f;
        public float EditorHeight = 260f;
        public string EditorNote = string.Empty;

        public List<string> SelectedTokenIds = new List<string>();

        public OvAnimationTriggerKind Trigger = OvAnimationTriggerKind.AnyKeyDown;
        public List<string> TriggerKeys = new List<string>();
        public bool TriggerOnKeyRelease;
        public int TriggerJudgementMask = 0xFFF;
        public OvComboCounterKind TriggerComboCounter = OvComboCounterKind.Pure;
        public int TriggerBeatInterval = 1;
        public int TriggerBeatOffset;
        public string TriggerTag = "{combo}";
        public OvTagNumericCompareKind TriggerNumericCompare = OvTagNumericCompareKind.GreaterOrEqual;
        public float TriggerNumericValue = 100f;
        public float TriggerNumericRangeMin;
        public float TriggerNumericRangeMax = 100f;
        public bool TriggerNumericIncludeMin = true;
        public bool TriggerNumericIncludeMax = true;
        public float TriggerNumericEpsilon = 0.0001f;
        public bool TriggerStateEnabled = true;
        public OvJudgementMode TriggerJudgementMode = OvJudgementMode.Normal;

        public string ModifyText = string.Empty;

        public OvStateConditionKind StateCondition = OvStateConditionKind.Autoplay;
        public bool ExpectedStateEnabled = true;
        public OvJudgementMode ExpectedJudgementMode = OvJudgementMode.Normal;

        public OvTokenEffectKind EffectKind = OvTokenEffectKind.ColorMap;
        public OvEffectValueSourceKind EffectValueSource = OvEffectValueSourceKind.Tag;
        public string EffectSourceTag = "{progress}";
        public float EffectConstantValue;
        public OvEffectValueInterpretation EffectValueInterpretation = OvEffectValueInterpretation.Auto;
        public OvColorMapMode ColorMapMode = OvColorMapMode.Gradient;
        public bool EffectTextColor = true;
        public bool EffectOutlineColor;
        public bool EffectShadowColor;
        public List<OvColorPoint> ColorPoints = new List<OvColorPoint>();

        public bool ColorChangeText = true;
        public bool ColorChangeOutline;
        public bool ColorChangeShadow;
        public float[] ColorChangeTextColor = new float[] { 1f, 1f, 1f, 1f };
        public float[] ColorChangeOutlineColor = new float[] { 0f, 0f, 0f, 1f };
        public float[] ColorChangeShadowColor = new float[] { 0f, 0f, 0f, 0.75f };

        public OvNumberFormatKind NumberFormatKind = OvNumberFormatKind.Plain;
        public OvPercentageInputKind PercentageInputKind = OvPercentageInputKind.Auto;
        public int NumberFormatDecimals;
        public int NumberFormatWidth = 4;

        public OvTokenTweenProperty TweenProperty = OvTokenTweenProperty.Position;
        public float FromX;
        public float FromY;
        public float ToX;
        public float ToY = -24f;
        public float Duration = 0.25f;
        public float StaggerDelay;
        public bool ReverseOrder;
        public bool TreatSelectedTokensAsGroup;
        public string Easing = "ease-out-quad";
    }

    [Serializable]
    public sealed class OvAnimationGraph
    {
        public int FormatVersion = 12;
        public bool Enabled;
        public bool HoldFinalPose;
        public List<OvAnimationNode> Nodes = new List<OvAnimationNode>();
        public List<OvAnimationLink> Links = new List<OvAnimationLink>();

        public void Normalize()
        {
            if (Nodes == null) Nodes = new List<OvAnimationNode>();
            if (Links == null) Links = new List<OvAnimationLink>();
            var migratedStateNodes = new HashSet<string>();
            for (int i = Nodes.Count - 1; i >= 0; i--)
            {
                OvAnimationNode node = Nodes[i];
                if (node == null)
                {
                    Nodes.RemoveAt(i);
                    continue;
                }
                if (string.IsNullOrEmpty(node.Id)) node.Id = NewId();
                if (node.Name == null) node.Name = string.Empty;
                if (node.EditorNote == null) node.EditorNote = string.Empty;
                if (node.EditorWidth < 180f || float.IsNaN(node.EditorWidth) || float.IsInfinity(node.EditorWidth)) node.EditorWidth = 420f;
                if (node.EditorHeight < 120f || float.IsNaN(node.EditorHeight) || float.IsInfinity(node.EditorHeight)) node.EditorHeight = 260f;
                if (node.SelectedTokenIds == null) node.SelectedTokenIds = new List<string>();
                if (node.TriggerKeys == null) node.TriggerKeys = new List<string>();
                if (node.TriggerBeatInterval < 1) node.TriggerBeatInterval = 1;
                if (node.TriggerTag == null) node.TriggerTag = "{combo}";
                if (float.IsNaN(node.TriggerNumericValue) || float.IsInfinity(node.TriggerNumericValue)) node.TriggerNumericValue = 100f;
                if (float.IsNaN(node.TriggerNumericRangeMin) || float.IsInfinity(node.TriggerNumericRangeMin)) node.TriggerNumericRangeMin = 0f;
                if (float.IsNaN(node.TriggerNumericRangeMax) || float.IsInfinity(node.TriggerNumericRangeMax)) node.TriggerNumericRangeMax = 100f;
                if (node.TriggerNumericRangeMin > node.TriggerNumericRangeMax)
                {
                    float swap = node.TriggerNumericRangeMin;
                    node.TriggerNumericRangeMin = node.TriggerNumericRangeMax;
                    node.TriggerNumericRangeMax = swap;
                }
                if (float.IsNaN(node.TriggerNumericEpsilon) || float.IsInfinity(node.TriggerNumericEpsilon) || node.TriggerNumericEpsilon < 0f)
                    node.TriggerNumericEpsilon = 0.0001f;
                if (node.ModifyText == null) node.ModifyText = string.Empty;
                if (node.EffectSourceTag == null) node.EffectSourceTag = "{progress}";
                if (node.ColorPoints == null) node.ColorPoints = new List<OvColorPoint>();
                for (int p = node.ColorPoints.Count - 1; p >= 0; p--)
                {
                    OvColorPoint point = node.ColorPoints[p];
                    if (point == null)
                    {
                        node.ColorPoints.RemoveAt(p);
                        continue;
                    }
                    if (float.IsNaN(point.Value) || float.IsInfinity(point.Value)) point.Value = 0f;
                    if ((FormatVersion < 7 || point.TextColor == null || point.TextColor.Length < 4)
                        && point.Color != null && point.Color.Length >= 4)
                    {
                        point.TextColor = (float[])point.Color.Clone();
                    }
                    point.TextColor = NormalizeColor(point.TextColor, 1f, 1f, 1f, 1f);
                    point.OutlineColor = NormalizeColor(point.OutlineColor, 0f, 0f, 0f, 1f);
                    point.ShadowColor = NormalizeColor(point.ShadowColor, 0f, 0f, 0f, 1f);
                    point.Color = null;
                }
                if (node.Kind == OvAnimationNodeKind.Effect && node.ColorPoints.Count == 0)
                {
                    node.ColorPoints.Add(CreateDefaultColorPoint(0f, 1f, 0.2f, 0.2f));
                    node.ColorPoints.Add(CreateDefaultColorPoint(100f, 0.2f, 1f, 0.35f));
                }
                node.ColorChangeTextColor = NormalizeColor(node.ColorChangeTextColor, 1f, 1f, 1f, 1f);
                node.ColorChangeOutlineColor = NormalizeColor(node.ColorChangeOutlineColor, 0f, 0f, 0f, 1f);
                node.ColorChangeShadowColor = NormalizeColor(node.ColorChangeShadowColor, 0f, 0f, 0f, 0.75f);
                if (node.Kind == OvAnimationNodeKind.ColorChange
                    && !node.ColorChangeText && !node.ColorChangeOutline && !node.ColorChangeShadow)
                {
                    node.ColorChangeText = true;
                }
                if (node.Kind == OvAnimationNodeKind.StateCondition)
                {
                    node.Kind = OvAnimationNodeKind.Trigger;
                    node.TriggerStateEnabled = node.ExpectedStateEnabled;
                    node.TriggerJudgementMode = node.ExpectedJudgementMode;
                    node.Trigger = node.StateCondition == OvStateConditionKind.NoFail
                        ? OvAnimationTriggerKind.NoFailState
                        : node.StateCondition == OvStateConditionKind.JudgementMode
                            ? OvAnimationTriggerKind.JudgementModeState
                            : OvAnimationTriggerKind.AutoplayState;
                    migratedStateNodes.Add(node.Id);
                }
                node.NumberFormatDecimals = Math.Max(0, Math.Min(8, node.NumberFormatDecimals));
                node.NumberFormatWidth = Math.Max(1, Math.Min(16, node.NumberFormatWidth));
                if (node.Duration <= 0f || float.IsNaN(node.Duration) || float.IsInfinity(node.Duration)) node.Duration = 0.25f;
                if (node.StaggerDelay < 0f || float.IsNaN(node.StaggerDelay) || float.IsInfinity(node.StaggerDelay)) node.StaggerDelay = 0f;
                if (string.IsNullOrEmpty(node.Easing)) node.Easing = "linear";
            }
            var nodeIds = new HashSet<string>();
            for (int i = 0; i < Nodes.Count; i++) nodeIds.Add(Nodes[i].Id);
            for (int i = Links.Count - 1; i >= 0; i--)
            {
                OvAnimationLink link = Links[i];
                if (link == null || !nodeIds.Contains(link.FromNodeId) || !nodeIds.Contains(link.ToNodeId)
                    || migratedStateNodes.Contains(link.ToNodeId))
                {
                    Links.RemoveAt(i);
                    continue;
                }
                if (string.IsNullOrEmpty(link.Id)) link.Id = NewId();
            }
            if (FormatVersion < 12) FormatVersion = 12;
        }

        public static OvAnimationGraph CreateDefault()
        {
            var graph = new OvAnimationGraph { Enabled = false };
            var input = new OvAnimationNode
            {
                Id = NewId(),
                Kind = OvAnimationNodeKind.TokenInput,
                Name = "Token Input",
                EditorX = 40f,
                EditorY = 180f
            };
            var trigger = new OvAnimationNode
            {
                Id = NewId(),
                Kind = OvAnimationNodeKind.Trigger,
                Name = "Any Key Down",
                Trigger = OvAnimationTriggerKind.AnyKeyDown,
                EditorX = 40f,
                EditorY = 40f
            };
            var tween = new OvAnimationNode
            {
                Id = NewId(),
                Kind = OvAnimationNodeKind.Tween,
                Name = "Move",
                TweenProperty = OvTokenTweenProperty.Position,
                FromX = 0f,
                FromY = 0f,
                ToX = 0f,
                ToY = -24f,
                Duration = 0.25f,
                Easing = "ease-out-quad",
                EditorX = 360f,
                EditorY = 80f
            };
            graph.Nodes.Add(input);
            graph.Nodes.Add(trigger);
            graph.Nodes.Add(tween);
            graph.Links.Add(NewLink(trigger.Id, "flow", tween.Id, "flow"));
            graph.Links.Add(NewLink(input.Id, "targets", tween.Id, "targets"));
            return graph;
        }

        public static OvAnimationGraph Clone(OvAnimationGraph source)
        {
            if (source == null) return CreateDefault();
            var clone = new OvAnimationGraph
            {
                FormatVersion = source.FormatVersion,
                Enabled = source.Enabled,
                HoldFinalPose = source.HoldFinalPose
            };
            if (source.Nodes != null)
            {
                foreach (OvAnimationNode node in source.Nodes)
                {
                    if (node == null) continue;
                    clone.Nodes.Add(CloneNode(node));
                }
            }
            if (source.Links != null)
            {
                foreach (OvAnimationLink link in source.Links)
                {
                    if (link == null) continue;
                    clone.Links.Add(new OvAnimationLink
                    {
                        Id = link.Id,
                        FromNodeId = link.FromNodeId,
                        FromPort = link.FromPort,
                        ToNodeId = link.ToNodeId,
                        ToPort = link.ToPort
                    });
                }
            }
            return clone;
        }

        public static OvAnimationNode CloneNode(OvAnimationNode node)
        {
            if (node == null) return null;
            return new OvAnimationNode
            {
                Id = node.Id,
                Kind = node.Kind,
                Name = node.Name,
                EditorX = node.EditorX,
                EditorY = node.EditorY,
                EditorWidth = node.EditorWidth,
                EditorHeight = node.EditorHeight,
                EditorNote = node.EditorNote,
                SelectedTokenIds = node.SelectedTokenIds != null ? new List<string>(node.SelectedTokenIds) : new List<string>(),
                Trigger = node.Trigger,
                TriggerKeys = node.TriggerKeys != null ? new List<string>(node.TriggerKeys) : new List<string>(),
                TriggerOnKeyRelease = node.TriggerOnKeyRelease,
                TriggerJudgementMask = node.TriggerJudgementMask,
                TriggerComboCounter = node.TriggerComboCounter,
                TriggerBeatInterval = node.TriggerBeatInterval,
                TriggerBeatOffset = node.TriggerBeatOffset,
                TriggerTag = node.TriggerTag,
                TriggerNumericCompare = node.TriggerNumericCompare,
                TriggerNumericValue = node.TriggerNumericValue,
                TriggerNumericRangeMin = node.TriggerNumericRangeMin,
                TriggerNumericRangeMax = node.TriggerNumericRangeMax,
                TriggerNumericIncludeMin = node.TriggerNumericIncludeMin,
                TriggerNumericIncludeMax = node.TriggerNumericIncludeMax,
                TriggerNumericEpsilon = node.TriggerNumericEpsilon,
                TriggerStateEnabled = node.TriggerStateEnabled,
                TriggerJudgementMode = node.TriggerJudgementMode,
                ModifyText = node.ModifyText,
                StateCondition = node.StateCondition,
                ExpectedStateEnabled = node.ExpectedStateEnabled,
                ExpectedJudgementMode = node.ExpectedJudgementMode,
                EffectKind = node.EffectKind,
                EffectValueSource = node.EffectValueSource,
                EffectSourceTag = node.EffectSourceTag,
                EffectConstantValue = node.EffectConstantValue,
                EffectValueInterpretation = node.EffectValueInterpretation,
                ColorMapMode = node.ColorMapMode,
                EffectTextColor = node.EffectTextColor,
                EffectOutlineColor = node.EffectOutlineColor,
                EffectShadowColor = node.EffectShadowColor,
                ColorPoints = CloneColorPoints(node.ColorPoints),
                ColorChangeText = node.ColorChangeText,
                ColorChangeOutline = node.ColorChangeOutline,
                ColorChangeShadow = node.ColorChangeShadow,
                ColorChangeTextColor = CloneColor(node.ColorChangeTextColor, 1f, 1f, 1f, 1f),
                ColorChangeOutlineColor = CloneColor(node.ColorChangeOutlineColor, 0f, 0f, 0f, 1f),
                ColorChangeShadowColor = CloneColor(node.ColorChangeShadowColor, 0f, 0f, 0f, 0.75f),
                NumberFormatKind = node.NumberFormatKind,
                PercentageInputKind = node.PercentageInputKind,
                NumberFormatDecimals = node.NumberFormatDecimals,
                NumberFormatWidth = node.NumberFormatWidth,
                TweenProperty = node.TweenProperty,
                FromX = node.FromX,
                FromY = node.FromY,
                ToX = node.ToX,
                ToY = node.ToY,
                Duration = node.Duration,
                StaggerDelay = node.StaggerDelay,
                ReverseOrder = node.ReverseOrder,
                TreatSelectedTokensAsGroup = node.TreatSelectedTokensAsGroup,
                Easing = node.Easing
            };
        }

        private static List<OvColorPoint> CloneColorPoints(List<OvColorPoint> source)
        {
            var result = new List<OvColorPoint>();
            if (source == null) return result;
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null) result.Add(OvColorPoint.Clone(source[i]));
            }
            return result;
        }

        private static OvColorPoint CreateDefaultColorPoint(float value, float r, float g, float b)
        {
            return new OvColorPoint
            {
                Value = value,
                TextColor = new float[] { r, g, b, 1f },
                OutlineColor = new float[] { 0f, 0f, 0f, 1f },
                ShadowColor = new float[] { 0f, 0f, 0f, 0.75f }
            };
        }

        private static float[] CloneColor(float[] color, float r, float g, float b, float a)
        {
            if (color == null || color.Length < 4) return new float[] { r, g, b, a };
            return new float[] { color[0], color[1], color[2], color[3] };
        }

        private static float[] NormalizeColor(float[] color, float r, float g, float b, float a)
        {
            if (color == null || color.Length < 4) return new float[] { r, g, b, a };
            for (int i = 0; i < 4; i++)
            {
                if (float.IsNaN(color[i]) || float.IsInfinity(color[i])) color[i] = i == 3 ? a : 0f;
                color[i] = Math.Max(0f, Math.Min(1f, color[i]));
            }
            return color;
        }

        public static OvAnimationLink NewLink(string fromNodeId, string fromPort, string toNodeId, string toPort)
        {
            return new OvAnimationLink
            {
                Id = NewId(),
                FromNodeId = fromNodeId,
                FromPort = fromPort,
                ToNodeId = toNodeId,
                ToPort = toPort
            };
        }

        public static string NewId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }

    public sealed class OvTokenGroupTransform
    {
        public string Id = string.Empty;
        public int Order;
        public float OffsetX;
        public float OffsetY;
        public float ScaleX = 1f;
        public float ScaleY = 1f;
        public float Rotation;
    }

    public struct OvTokenPose
    {
        public float OffsetX;
        public float OffsetY;
        public float ScaleX;
        public float ScaleY;
        public float Rotation;
        public float Opacity;
        public bool HasTextColorOverride;
        public float TextColorR;
        public float TextColorG;
        public float TextColorB;
        public float TextColorA;
        public bool HasOutlineColorOverride;
        public float OutlineColorR;
        public float OutlineColorG;
        public float OutlineColorB;
        public float OutlineColorA;
        public string OutlineColorGroupId;
        public bool HasShadowColorOverride;
        public float ShadowColorR;
        public float ShadowColorG;
        public float ShadowColorB;
        public float ShadowColorA;
        public string ShadowColorGroupId;
        public List<OvTokenGroupTransform> GroupTransforms;

        public static OvTokenPose Identity
        {
            get
            {
                return new OvTokenPose
                {
                    ScaleX = 1f,
                    ScaleY = 1f,
                    Opacity = 1f,
                    TextColorR = 1f,
                    TextColorG = 1f,
                    TextColorB = 1f,
                    TextColorA = 1f,
                    OutlineColorA = 1f,
                    ShadowColorA = 1f
                };
            }
        }
    }
}
