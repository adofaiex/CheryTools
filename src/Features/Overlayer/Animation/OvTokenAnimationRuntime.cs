using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheryTools
{
    internal sealed class OvAnimationTriggerFrame
    {
        public bool AnyKeyDown;
        public bool ComboIncreased;
        public bool PureComboBroken;
        public bool PerfectComboBroken;
        public bool BeatHappened;
        public int BeatNumber;
        public bool LevelStarted;
        public bool LevelEnded;
        public bool AutoplayEnabled;
        public bool NoFailEnabled;
        public int JudgementMode;
        public readonly List<int> Judgements = new List<int>();
        public readonly HashSet<KeyCode> KeysDown = new HashSet<KeyCode>();
        public readonly HashSet<KeyCode> KeysUp = new HashSet<KeyCode>();
        public Func<OverlayerText, string, string> ResolveTagValue;
        public Func<OverlayerText, string, double> ResolveTagNumber;

        public void Reset()
        {
            AnyKeyDown = false;
            ComboIncreased = false;
            PureComboBroken = false;
            PerfectComboBroken = false;
            BeatHappened = false;
            BeatNumber = 0;
            LevelStarted = false;
            LevelEnded = false;
            AutoplayEnabled = false;
            NoFailEnabled = false;
            JudgementMode = (int)OvJudgementMode.Normal;
            Judgements.Clear();
            KeysDown.Clear();
            KeysUp.Clear();
        }
    }

    internal sealed class OvTokenAnimationRuntime
    {
        private sealed class ScheduledTween
        {
            public OvAnimationNode Node;
            public float StartTime;
            public List<string> TokenIds = new List<string>();
            public string GroupTransformId = string.Empty;
            public int GroupTransformOrder;
            // Reused every frame by ApplyGroupedTween; a grouped tween previously
            // allocated a fresh transform object per evaluation.
            public readonly OvTokenGroupTransform SharedTransform = new OvTokenGroupTransform();

            public float EndTime
            {
                get
                {
                    float stagger = Node.TreatSelectedTokensAsGroup
                        ? 0f
                        : Mathf.Max(0f, Node.StaggerDelay) * Math.Max(0, TokenIds.Count - 1);
                    return StartTime + Mathf.Max(0.01f, Node.Duration) + stagger;
                }
            }
        }

        private sealed class ScheduledModification
        {
            public OvAnimationNode Node;
            public float StartTime;
            public List<string> TokenIds = new List<string>();
        }

        private sealed class ColorLayer
        {
            public OvAnimationNode Node;
            public List<string> TokenIds = new List<string>();
            public List<OvColorPoint> SortedColorPoints = new List<OvColorPoint>();
            public HashSet<string> TriggerNodeIds = new HashSet<string>();
            public bool HasFlowInput;
        }

        private struct EffectStyle
        {
            public bool Valid;
            public bool Text;
            public bool Outline;
            public bool Shadow;
            public Vector4 TextColor;
            public Vector4 OutlineColor;
            public Vector4 ShadowColor;
        }

        private sealed class TriggerSchedule
        {
            public string TriggerNodeId;
            public OvAnimationNode TriggerNode;
            public List<KeyCode> TriggerKeys = new List<KeyCode>();
            public List<ScheduledTween> Tweens = new List<ScheduledTween>();
            public List<ScheduledModification> Modifications = new List<ScheduledModification>();
            public float Duration;
        }

        private sealed class Playback
        {
            public TriggerSchedule Schedule;
            public float Time;
            public bool Holding;
            public bool AutoplayEnabled;
            public bool NoFailEnabled;
            public int JudgementMode;
            public HashSet<ScheduledModification> AppliedModifications = new HashSet<ScheduledModification>();
        }

        private sealed class TextState
        {
            public long CompiledRevision = -1;
            // Monotonic component-local revision used by the renderer to keep a
            // baked mesh alive while an enabled node graph is visually dormant.
            public long VisualRevision = 1;
            public bool NeedsPostPlaybackRefresh;
            public List<TriggerSchedule> Schedules = new List<TriggerSchedule>();
            public Dictionary<string, Playback> Playbacks = new Dictionary<string, Playback>();
            public List<ColorLayer> ColorLayers = new List<ColorLayer>();
            public Dictionary<string, OvTokenPose> Poses = new Dictionary<string, OvTokenPose>();
            public Dictionary<string, string> TokenTextOverrides = new Dictionary<string, string>();
            public Dictionary<string, OvAnimationNode> NumberFormats = new Dictionary<string, OvAnimationNode>();
            public Dictionary<string, string> TriggerTagValues = new Dictionary<string, string>();
            public Dictionary<string, int> StateTriggerValues = new Dictionary<string, int>();
            public Dictionary<string, bool> NumericConditionValues = new Dictionary<string, bool>();
            public Dictionary<string, EffectStyle> EffectStyles = new Dictionary<string, EffectStyle>();
            public Dictionary<string, bool> ColorLayerActiveStates = new Dictionary<string, bool>();
            public HashSet<string> ActiveColorTriggerIds = new HashSet<string>();
            public HashSet<string> PreviewColorTriggerIds = new HashSet<string>();
            public List<string> CompletedIds = new List<string>();
            // Pool for OvTokenPose.GroupTransforms lists. Poses is a struct dictionary
            // rebuilt every update, so without pooling every grouped-tween token
            // allocated a fresh List per frame. Reset via ClearPoses.
            public readonly List<List<OvTokenGroupTransform>> GroupTransformListPool = new List<List<OvTokenGroupTransform>>();
            public int GroupTransformListsUsed;
        }

        private static void ClearPoses(TextState state)
        {
            state.Poses.Clear();
            state.GroupTransformListsUsed = 0;
        }

        private static List<OvTokenGroupTransform> RentGroupTransformList(TextState state)
        {
            if (state.GroupTransformListsUsed < state.GroupTransformListPool.Count)
            {
                List<OvTokenGroupTransform> pooled = state.GroupTransformListPool[state.GroupTransformListsUsed];
                state.GroupTransformListsUsed++;
                pooled.Clear();
                return pooled;
            }

            List<OvTokenGroupTransform> created = new List<OvTokenGroupTransform>(4);
            state.GroupTransformListPool.Add(created);
            state.GroupTransformListsUsed++;
            return created;
        }

        private readonly Dictionary<OverlayerText, TextState> _states = new Dictionary<OverlayerText, TextState>();

        private static void BumpVisualRevision(TextState state)
        {
            if (state == null) return;
            unchecked
            {
                state.VisualRevision++;
                if (state.VisualRevision <= 0) state.VisualRevision = 1;
            }
        }

        public static bool HasEnabledGraph(OverlayerText text)
        {
            return text != null
                && text.TokenAnimation != null
                && text.TokenAnimation.Enabled
                && text.TokenAnimation.Nodes != null
                && text.TokenAnimation.Nodes.Count > 0;
        }

        public bool Update(List<OverlayerText> texts, OvAnimationTriggerFrame triggerFrame, float deltaTime, long revision)
        {
            bool changed = false;
            if (texts == null || triggerFrame == null) return false;
            for (int i = 0; i < texts.Count; i++)
            {
                OverlayerText text = texts[i];
                if (!HasEnabledGraph(text))
                {
                    if (text != null && _states.TryGetValue(text, out TextState disabledState)
                        && (disabledState.Poses.Count > 0 || disabledState.TokenTextOverrides.Count > 0))
                    {
                        ClearPoses(disabledState);
                        disabledState.TokenTextOverrides.Clear();
                        disabledState.NeedsPostPlaybackRefresh = false;
                        BumpVisualRevision(disabledState);
                        changed = true;
                    }
                    continue;
                }

                TextState state = GetOrCompile(text, revision);
                bool textChanged = false;
                if (state.NeedsPostPlaybackRefresh)
                {
                    // A non-held playback contributes its final pose during its
                    // completion frame. Invalidate once more on the next update so
                    // that the baked mesh can return to the remaining/base pose.
                    state.NeedsPostPlaybackRefresh = false;
                    textChanged = true;
                }
                state.ActiveColorTriggerIds.Clear();
                if (state.PreviewColorTriggerIds.Count > 0)
                {
                    foreach (string triggerId in state.PreviewColorTriggerIds) state.ActiveColorTriggerIds.Add(triggerId);
                    state.PreviewColorTriggerIds.Clear();
                }
                for (int s = 0; s < state.Schedules.Count; s++)
                {
                    TriggerSchedule schedule = state.Schedules[s];
                    bool fired = ShouldFire(schedule, text, state, triggerFrame);
                    if (fired)
                    {
                        state.Playbacks[schedule.TriggerNodeId] = CreatePlayback(schedule, triggerFrame);
                        textChanged = true;
                    }
                    if (fired || IsSustainedColorTriggerActive(schedule, text, triggerFrame))
                    {
                        state.ActiveColorTriggerIds.Add(schedule.TriggerNodeId);
                    }
                }

                ClearPoses(state);
                if (state.Playbacks.Count > 0)
                {
                    List<string> completed = state.CompletedIds;
                    completed.Clear();
                    foreach (KeyValuePair<string, Playback> pair in state.Playbacks)
                    {
                        Playback playback = pair.Value;
                        if (!playback.Holding)
                        {
                            float advance = Mathf.Max(0f, deltaTime);
                            playback.Time += advance;
                            // The evaluated pose can still change on the exact frame
                            // that reaches Duration, including a held final pose.
                            if (advance > 0f) textChanged = true;
                        }
                        if (EvaluatePlayback(playback, state)) textChanged = true;
                        if (playback.Time >= playback.Schedule.Duration)
                        {
                            if (text.TokenAnimation.HoldFinalPose)
                            {
                                playback.Time = playback.Schedule.Duration;
                                playback.Holding = true;
                            }
                            else
                            {
                                completed.Add(pair.Key);
                            }
                        }
                        else
                        {
                            textChanged = true;
                        }
                    }
                    for (int c = 0; c < completed.Count; c++)
                    {
                        state.Playbacks.Remove(completed[c]);
                        textChanged = true;
                    }
                    if (state.Playbacks.Count == 0 && completed.Count > 0)
                    {
                        ClearPoses(state);
                    }
                    else if (completed.Count > 0)
                    {
                        state.NeedsPostPlaybackRefresh = true;
                    }
                }
                if (EvaluateColorLayers(text, state, triggerFrame.ResolveTagNumber)) textChanged = true;
                if (textChanged)
                {
                    BumpVisualRevision(state);
                    changed = true;
                }
            }
            return changed;
        }

        public void Preview(OverlayerText text)
        {
            if (!HasEnabledGraph(text)) return;
            TextState state = GetOrCompile(text, OverlayRenderInvalidator.Revision);
            bool started = false;
            for (int i = 0; i < state.Schedules.Count; i++)
            {
                TriggerSchedule schedule = state.Schedules[i];
                if (schedule.TriggerNode != null
                    && (schedule.TriggerNode.Trigger == OvAnimationTriggerKind.Manual || state.Schedules.Count == 1))
                {
                    state.Playbacks[schedule.TriggerNodeId] = CreatePlayback(schedule, null);
                    state.PreviewColorTriggerIds.Add(schedule.TriggerNodeId);
                    started = true;
                }
            }
            if (state.Playbacks.Count == 0 && state.Schedules.Count > 0)
            {
                TriggerSchedule first = state.Schedules[0];
                state.Playbacks[first.TriggerNodeId] = CreatePlayback(first, null);
                state.PreviewColorTriggerIds.Add(first.TriggerNodeId);
                started = true;
            }
            if (started) BumpVisualRevision(state);
        }

        public void Stop(OverlayerText text)
        {
            if (text == null || !_states.TryGetValue(text, out TextState state)) return;
            bool hadVisualOutput = state.Playbacks.Count > 0
                || state.Poses.Count > 0
                || state.TokenTextOverrides.Count > 0
                || state.ActiveColorTriggerIds.Count > 0
                || state.PreviewColorTriggerIds.Count > 0;
            state.Playbacks.Clear();
            ClearPoses(state);
            state.TokenTextOverrides.Clear();
            state.ActiveColorTriggerIds.Clear();
            state.PreviewColorTriggerIds.Clear();
            state.NeedsPostPlaybackRefresh = false;
            if (hadVisualOutput) BumpVisualRevision(state);
        }

        public long GetVisualRevision(OverlayerText text)
        {
            if (text != null && _states.TryGetValue(text, out TextState state)) return state.VisualRevision;
            return 0;
        }

        public Dictionary<string, OvTokenPose> GetPoses(OverlayerText text)
        {
            if (text != null && _states.TryGetValue(text, out TextState state)) return state.Poses;
            return null;
        }

        public Dictionary<string, string> GetTextOverrides(OverlayerText text)
        {
            if (text != null && _states.TryGetValue(text, out TextState state)) return state.TokenTextOverrides;
            return null;
        }

        public Dictionary<string, OvAnimationNode> GetNumberFormats(OverlayerText text)
        {
            if (text != null && _states.TryGetValue(text, out TextState state)) return state.NumberFormats;
            return null;
        }

        private TextState GetOrCompile(OverlayerText text, long revision)
        {
            if (!_states.TryGetValue(text, out TextState state))
            {
                state = new TextState();
                _states[text] = state;
            }
            if (state.CompiledRevision != revision)
            {
                Compile(text, state);
                state.CompiledRevision = revision;
                BumpVisualRevision(state);
            }
            return state;
        }

        private sealed class PlaybackSnapshot
        {
            public float Time;
            public bool Holding;
            public bool AutoplayEnabled;
            public bool NoFailEnabled;
            public int JudgementMode;
            public readonly HashSet<string> AppliedNodeIds = new HashSet<string>();
        }

        private static void Compile(OverlayerText text, TextState state)
        {
            // Any settings change bumps the global revision and recompiles this graph.
            // Snapshot the running playbacks first so an unrelated settings tweak (or
            // a drag-end save) no longer hard-resets animations that are mid-flight;
            // they are re-attached to the freshly compiled schedules by trigger id.
            Dictionary<string, PlaybackSnapshot> playbackSnapshots = null;
            if (state.Playbacks.Count > 0)
            {
                playbackSnapshots = new Dictionary<string, PlaybackSnapshot>();
                foreach (KeyValuePair<string, Playback> pair in state.Playbacks)
                {
                    Playback playback = pair.Value;
                    if (playback == null) continue;
                    var snapshot = new PlaybackSnapshot
                    {
                        Time = playback.Time,
                        Holding = playback.Holding,
                        AutoplayEnabled = playback.AutoplayEnabled,
                        NoFailEnabled = playback.NoFailEnabled,
                        JudgementMode = playback.JudgementMode
                    };
                    foreach (ScheduledModification applied in playback.AppliedModifications)
                    {
                        if (applied != null && applied.Node != null && !string.IsNullOrEmpty(applied.Node.Id))
                        {
                            snapshot.AppliedNodeIds.Add(applied.Node.Id);
                        }
                    }
                    playbackSnapshots[pair.Key] = snapshot;
                }
            }

            state.Schedules.Clear();
            state.ColorLayers.Clear();
            state.Playbacks.Clear();
            ClearPoses(state);
            state.TokenTextOverrides.Clear();
            state.NumberFormats.Clear();
            state.TriggerTagValues.Clear();
            state.StateTriggerValues.Clear();
            state.NumericConditionValues.Clear();
            state.EffectStyles.Clear();
            state.ColorLayerActiveStates.Clear();
            state.ActiveColorTriggerIds.Clear();
            state.PreviewColorTriggerIds.Clear();
            state.NeedsPostPlaybackRefresh = false;
            OvAnimationGraph graph = text.TokenAnimation;
            if (graph == null || graph.Nodes == null) return;

            var nodes = new Dictionary<string, OvAnimationNode>();
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                OvAnimationNode node = graph.Nodes[i];
                if (node == null) continue;
                if (string.IsNullOrEmpty(node.Id)) node.Id = OvAnimationGraph.NewId();
                nodes[node.Id] = node;
            }

            var colorLayers = new Dictionary<string, ColorLayer>();
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                OvAnimationNode node = graph.Nodes[i];
                if (node == null || (node.Kind != OvAnimationNodeKind.Effect
                    && node.Kind != OvAnimationNodeKind.ColorChange)) continue;
                var layer = new ColorLayer { Node = node };
                colorLayers[node.Id] = layer;
                state.ColorLayers.Add(layer);
            }

            var flow = new Dictionary<string, List<string>>();
            var targets = new Dictionary<string, List<string>>();
            if (graph.Links != null)
            {
                for (int i = 0; i < graph.Links.Count; i++)
                {
                    OvAnimationLink link = graph.Links[i];
                    if (link == null) continue;
                    if (link.FromPort == "flow" && link.ToPort == "flow")
                    {
                        AddEdge(flow, link.FromNodeId, link.ToNodeId);
                        if (colorLayers.TryGetValue(link.ToNodeId, out ColorLayer targetLayer)) targetLayer.HasFlowInput = true;
                    }
                    if (link.FromPort == "targets" && link.ToPort == "targets") AddEdge(targets, link.ToNodeId, link.FromNodeId);
                }
            }

            for (int i = 0; i < state.ColorLayers.Count; i++)
            {
                ColorLayer layer = state.ColorLayers[i];
                CollectDirectTargets(layer.Node.Id, nodes, targets, layer.TokenIds);
                if (layer.Node.Kind == OvAnimationNodeKind.Effect && layer.Node.ColorPoints != null)
                {
                    for (int p = 0; p < layer.Node.ColorPoints.Count; p++)
                    {
                        if (layer.Node.ColorPoints[p] != null) layer.SortedColorPoints.Add(layer.Node.ColorPoints[p]);
                    }
                    layer.SortedColorPoints.Sort((left, right) => left.Value.CompareTo(right.Value));
                }
            }

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                OvAnimationNode trigger = graph.Nodes[i];
                if (trigger == null || trigger.Kind != OvAnimationNodeKind.Trigger) continue;
                var schedule = new TriggerSchedule { TriggerNodeId = trigger.Id, TriggerNode = trigger };
                if (trigger.TriggerKeys != null)
                {
                    for (int k = 0; k < trigger.TriggerKeys.Count; k++)
                    {
                        if (Enum.TryParse(trigger.TriggerKeys[k], true, out KeyCode key)
                            && key != KeyCode.None
                            && !schedule.TriggerKeys.Contains(key))
                        {
                            schedule.TriggerKeys.Add(key);
                        }
                    }
                }
                Traverse(trigger.Id, 0f, nodes, flow, targets, schedule,
                    colorLayers, new List<string>(), new HashSet<string>(), 0);
                SortModificationTargets(schedule.Modifications, text.TokenBindings);
                for (int t = 0; t < schedule.Tweens.Count; t++) schedule.Duration = Mathf.Max(schedule.Duration, schedule.Tweens[t].EndTime);
                if (schedule.Duration <= 0f) schedule.Duration = 0.01f;
                state.Schedules.Add(schedule);
            }

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                OvAnimationNode formatNode = graph.Nodes[i];
                if (formatNode == null || formatNode.Kind != OvAnimationNodeKind.NumberFormat) continue;
                var tokenIds = new List<string>();
                CollectDirectTargets(formatNode.Id, nodes, targets, tokenIds);
                for (int t = 0; t < tokenIds.Count; t++)
                {
                    state.NumberFormats[tokenIds[t]] = formatNode;
                }
            }

            if (playbackSnapshots != null)
            {
                for (int i = 0; i < state.Schedules.Count; i++)
                {
                    TriggerSchedule schedule = state.Schedules[i];
                    if (!playbackSnapshots.TryGetValue(schedule.TriggerNodeId, out PlaybackSnapshot snapshot)) continue;

                    var playback = new Playback
                    {
                        Schedule = schedule,
                        Time = Mathf.Min(snapshot.Time, schedule.Duration),
                        Holding = snapshot.Holding,
                        AutoplayEnabled = snapshot.AutoplayEnabled,
                        NoFailEnabled = snapshot.NoFailEnabled,
                        JudgementMode = snapshot.JudgementMode
                    };
                    for (int m = 0; m < schedule.Modifications.Count; m++)
                    {
                        ScheduledModification modification = schedule.Modifications[m];
                        if (modification != null && modification.Node != null
                            && snapshot.AppliedNodeIds.Contains(modification.Node.Id))
                        {
                            playback.AppliedModifications.Add(modification);
                        }
                    }
                    state.Playbacks[schedule.TriggerNodeId] = playback;
                }
            }
        }

        private static void CollectDirectTargets(string nodeId, Dictionary<string, OvAnimationNode> nodes,
            Dictionary<string, List<string>> targets, List<string> result)
        {
            if (!targets.TryGetValue(nodeId, out List<string> sourceIds)) return;
            for (int i = 0; i < sourceIds.Count; i++)
            {
                if (!nodes.TryGetValue(sourceIds[i], out OvAnimationNode source) || source.SelectedTokenIds == null) continue;
                for (int j = 0; j < source.SelectedTokenIds.Count; j++)
                {
                    string tokenId = source.SelectedTokenIds[j];
                    if (!string.IsNullOrEmpty(tokenId) && !result.Contains(tokenId)) result.Add(tokenId);
                }
            }
        }

        private static bool ShouldFire(TriggerSchedule schedule, OverlayerText text, TextState state,
            OvAnimationTriggerFrame frame)
        {
            OvAnimationNode node = schedule.TriggerNode;
            if (node == null) return false;
            switch (node.Trigger)
            {
                case OvAnimationTriggerKind.AnyKeyDown:
                    return frame.AnyKeyDown;
                case OvAnimationTriggerKind.ComboIncrease:
                    return frame.ComboIncreased;
                case OvAnimationTriggerKind.SpecificKey:
                    return IsConfiguredKeyTriggered(schedule, frame, node.TriggerOnKeyRelease);
                case OvAnimationTriggerKind.JudgementOccurred:
                    return HasMatchingJudgement(frame.Judgements, node.TriggerJudgementMask);
                case OvAnimationTriggerKind.ComboBreak:
                    return node.TriggerComboCounter == OvComboCounterKind.Perfect
                        ? frame.PerfectComboBroken
                        : frame.PureComboBroken;
                case OvAnimationTriggerKind.Beat:
                    return IsMatchingBeat(frame, node.TriggerBeatInterval, node.TriggerBeatOffset);
                case OvAnimationTriggerKind.LevelStart:
                    return frame.LevelStarted;
                case OvAnimationTriggerKind.LevelEnd:
                    return frame.LevelEnded;
                case OvAnimationTriggerKind.TagValueChanged:
                    return HasTagValueChanged(schedule, text, state, frame);
                case OvAnimationTriggerKind.TagNumericCondition:
                    return HasTagNumericCondition(schedule, text, state, frame);
                case OvAnimationTriggerKind.AutoplayState:
                    return HasStateValueEvent(state, schedule.TriggerNodeId, frame.AutoplayEnabled ? 1 : 0,
                        node.TriggerStateEnabled ? 1 : 0);
                case OvAnimationTriggerKind.NoFailState:
                    return HasStateValueEvent(state, schedule.TriggerNodeId, frame.NoFailEnabled ? 1 : 0,
                        node.TriggerStateEnabled ? 1 : 0);
                case OvAnimationTriggerKind.JudgementModeState:
                    return HasStateValueEvent(state, schedule.TriggerNodeId, frame.JudgementMode,
                        (int)node.TriggerJudgementMode);
                default:
                    return false;
            }
        }

        private static bool IsSustainedColorTriggerActive(TriggerSchedule schedule, OverlayerText text,
            OvAnimationTriggerFrame frame)
        {
            OvAnimationNode node = schedule != null ? schedule.TriggerNode : null;
            if (node == null || frame == null) return false;
            switch (node.Trigger)
            {
                case OvAnimationTriggerKind.TagNumericCondition:
                    if (string.IsNullOrWhiteSpace(node.TriggerTag) || frame.ResolveTagNumber == null) return false;
                    double value = frame.ResolveTagNumber(text, node.TriggerTag);
                    return !double.IsNaN(value) && !double.IsInfinity(value)
                        && EvaluateTagNumericCondition(node, value);
                case OvAnimationTriggerKind.AutoplayState:
                    return frame.AutoplayEnabled == node.TriggerStateEnabled;
                case OvAnimationTriggerKind.NoFailState:
                    return frame.NoFailEnabled == node.TriggerStateEnabled;
                case OvAnimationTriggerKind.JudgementModeState:
                    return frame.JudgementMode == (int)node.TriggerJudgementMode;
                default:
                    return false;
            }
        }

        private static bool IsConfiguredKeyTriggered(TriggerSchedule schedule, OvAnimationTriggerFrame frame,
            bool onRelease)
        {
            if (frame == null) return false;
            HashSet<KeyCode> triggered = onRelease ? frame.KeysUp : frame.KeysDown;
            for (int i = 0; i < schedule.TriggerKeys.Count; i++)
            {
                if (triggered.Contains(schedule.TriggerKeys[i])) return true;
            }
            return false;
        }

        private static bool HasMatchingJudgement(List<int> judgements, int mask)
        {
            if (judgements == null || mask == 0) return false;
            for (int i = 0; i < judgements.Count; i++)
            {
                int judgement = judgements[i];
                if (judgement >= 0 && judgement < 31 && (mask & (1 << judgement)) != 0) return true;
            }
            return false;
        }

        private static bool IsMatchingBeat(OvAnimationTriggerFrame frame, int interval, int offset)
        {
            if (!frame.BeatHappened) return false;
            interval = Math.Max(1, interval);
            int remainder = (frame.BeatNumber - offset) % interval;
            if (remainder < 0) remainder += interval;
            return remainder == 0;
        }

        private static bool HasTagValueChanged(TriggerSchedule schedule, OverlayerText text, TextState state,
            OvAnimationTriggerFrame frame)
        {
            string tag = schedule.TriggerNode.TriggerTag;
            if (string.IsNullOrWhiteSpace(tag) || frame.ResolveTagValue == null) return false;
            string value = frame.ResolveTagValue(text, tag) ?? string.Empty;
            if (!state.TriggerTagValues.TryGetValue(schedule.TriggerNodeId, out string previous))
            {
                state.TriggerTagValues[schedule.TriggerNodeId] = value;
                return false;
            }
            if (string.Equals(previous, value, StringComparison.Ordinal)) return false;
            state.TriggerTagValues[schedule.TriggerNodeId] = value;
            return true;
        }

        private static bool HasTagNumericCondition(TriggerSchedule schedule, OverlayerText text, TextState state,
            OvAnimationTriggerFrame frame)
        {
            OvAnimationNode node = schedule.TriggerNode;
            string tag = node != null ? node.TriggerTag : string.Empty;
            if (string.IsNullOrWhiteSpace(tag) || frame.ResolveTagNumber == null) return false;

            double value = frame.ResolveTagNumber(text, tag);
            if (double.IsNaN(value) || double.IsInfinity(value)) return false;

            bool matches = EvaluateTagNumericCondition(node, value);
            if (!state.NumericConditionValues.TryGetValue(schedule.TriggerNodeId, out bool previous))
            {
                state.NumericConditionValues[schedule.TriggerNodeId] = matches;
                // 与状态触发器一致：首次读取时若条件已经成立，也立即向后续节点传参。
                return matches;
            }

            state.NumericConditionValues[schedule.TriggerNodeId] = matches;
            return !previous && matches;
        }

        internal static bool EvaluateTagNumericCondition(OvAnimationNode node, double value)
        {
            if (node == null || double.IsNaN(value) || double.IsInfinity(value)) return false;

            double epsilon = Math.Max(0.0, node.TriggerNumericEpsilon);
            double target = node.TriggerNumericValue;
            switch (node.TriggerNumericCompare)
            {
                case OvTagNumericCompareKind.GreaterThan:
                    return value > target;
                case OvTagNumericCompareKind.GreaterOrEqual:
                    return value >= target;
                case OvTagNumericCompareKind.LessThan:
                    return value < target;
                case OvTagNumericCompareKind.LessOrEqual:
                    return value <= target;
                case OvTagNumericCompareKind.Equal:
                    return Math.Abs(value - target) <= epsilon;
                case OvTagNumericCompareKind.NotEqual:
                    return Math.Abs(value - target) > epsilon;
                case OvTagNumericCompareKind.InRange:
                case OvTagNumericCompareKind.OutsideRange:
                    double min = Math.Min(node.TriggerNumericRangeMin, node.TriggerNumericRangeMax);
                    double max = Math.Max(node.TriggerNumericRangeMin, node.TriggerNumericRangeMax);
                    bool aboveMin = node.TriggerNumericIncludeMin ? value >= min : value > min;
                    bool belowMax = node.TriggerNumericIncludeMax ? value <= max : value < max;
                    bool inside = aboveMin && belowMax;
                    return node.TriggerNumericCompare == OvTagNumericCompareKind.InRange ? inside : !inside;
                default:
                    return false;
            }
        }

        private static bool HasStateValueEvent(TextState state, string triggerNodeId, int currentValue, int expectedValue)
        {
            if (!state.StateTriggerValues.TryGetValue(triggerNodeId, out int previousValue))
            {
                state.StateTriggerValues[triggerNodeId] = currentValue;
                return currentValue == expectedValue;
            }
            if (previousValue == currentValue) return false;
            state.StateTriggerValues[triggerNodeId] = currentValue;
            return currentValue == expectedValue;
        }

        private static Playback CreatePlayback(TriggerSchedule schedule, OvAnimationTriggerFrame frame)
        {
            return new Playback
            {
                Schedule = schedule,
                AutoplayEnabled = frame != null && frame.AutoplayEnabled,
                NoFailEnabled = frame != null && frame.NoFailEnabled,
                JudgementMode = frame != null ? frame.JudgementMode : (int)OvJudgementMode.Normal
            };
        }

        private static void SortModificationTargets(List<ScheduledModification> modifications,
            List<OvTextTokenBinding> tokenBindings)
        {
            if (modifications == null || tokenBindings == null || tokenBindings.Count == 0) return;
            var order = new Dictionary<string, int>();
            for (int i = 0; i < tokenBindings.Count; i++)
            {
                OvTextTokenBinding token = tokenBindings[i];
                if (token != null && !string.IsNullOrEmpty(token.Id)) order[token.Id] = i;
            }
            for (int i = 0; i < modifications.Count; i++)
            {
                modifications[i].TokenIds.Sort((left, right) =>
                {
                    int leftOrder = order.TryGetValue(left, out int l) ? l : int.MaxValue;
                    int rightOrder = order.TryGetValue(right, out int r) ? r : int.MaxValue;
                    return leftOrder.CompareTo(rightOrder);
                });
            }
        }

        private static void Traverse(string nodeId, float startTime, Dictionary<string, OvAnimationNode> nodes,
            Dictionary<string, List<string>> flow, Dictionary<string, List<string>> targets,
            TriggerSchedule schedule, Dictionary<string, ColorLayer> colorLayers,
            List<string> inheritedTokenIds, HashSet<string> path, int depth)
        {
            if (depth > 128 || !path.Add(nodeId)) return;
            float nextStart = startTime;
            List<string> nextTokenIds = inheritedTokenIds;
            nodes.TryGetValue(nodeId, out OvAnimationNode node);
            if (node != null && (node.Kind == OvAnimationNodeKind.Tween
                || node.Kind == OvAnimationNodeKind.Modify
                || node.Kind == OvAnimationNodeKind.Effect
                || node.Kind == OvAnimationNodeKind.ColorChange))
            {
                if (targets.TryGetValue(node.Id, out List<string> sourceIds))
                {
                    nextTokenIds = new List<string>();
                    for (int i = 0; i < sourceIds.Count; i++)
                    {
                        if (nodes.TryGetValue(sourceIds[i], out OvAnimationNode source) && source.SelectedTokenIds != null)
                        {
                            for (int j = 0; j < source.SelectedTokenIds.Count; j++)
                            {
                                string tokenId = source.SelectedTokenIds[j];
                                if (!nextTokenIds.Contains(tokenId)) nextTokenIds.Add(tokenId);
                            }
                        }
                    }
                }
                if (node.Kind == OvAnimationNodeKind.Tween)
                {
                    int tweenOrder = schedule.Tweens.Count;
                    var tween = new ScheduledTween
                    {
                        Node = node,
                        StartTime = startTime,
                        GroupTransformId = schedule.TriggerNodeId + ":" + tweenOrder + ":" + node.Id,
                        GroupTransformOrder = tweenOrder
                    };
                    tween.TokenIds.AddRange(nextTokenIds);
                    if (!node.TreatSelectedTokensAsGroup && node.ReverseOrder) tween.TokenIds.Reverse();
                    schedule.Tweens.Add(tween);
                    nextStart = tween.EndTime;
                }
                else if (node.Kind == OvAnimationNodeKind.Modify)
                {
                    var modification = new ScheduledModification { Node = node, StartTime = startTime };
                    modification.TokenIds.AddRange(nextTokenIds);
                    schedule.Modifications.Add(modification);
                }

                if ((node.Kind == OvAnimationNodeKind.Effect || node.Kind == OvAnimationNodeKind.ColorChange)
                    && colorLayers.TryGetValue(node.Id, out ColorLayer layer))
                {
                    layer.TriggerNodeIds.Add(schedule.TriggerNodeId);
                    for (int i = 0; i < nextTokenIds.Count; i++)
                    {
                        string tokenId = nextTokenIds[i];
                        if (!string.IsNullOrEmpty(tokenId) && !layer.TokenIds.Contains(tokenId)) layer.TokenIds.Add(tokenId);
                    }
                }
            }
            if (flow.TryGetValue(nodeId, out List<string> next))
            {
                for (int i = 0; i < next.Count; i++)
                {
                    Traverse(next[i], nextStart, nodes, flow, targets, schedule,
                        colorLayers, nextTokenIds, new HashSet<string>(path), depth + 1);
                }
            }
        }

        private static bool EvaluatePlayback(Playback playback, TextState state)
        {
            Dictionary<string, OvTokenPose> poses = state.Poses;
            Dictionary<string, string> textOverrides = state.TokenTextOverrides;
            bool changed = false;
            for (int i = 0; i < playback.Schedule.Tweens.Count; i++)
            {
                ScheduledTween tween = playback.Schedule.Tweens[i];
                float duration = Mathf.Max(0.01f, tween.Node.Duration);
                if (tween.Node.TreatSelectedTokensAsGroup)
                {
                    float local = playback.Time - tween.StartTime;
                    if (local < 0f) continue;
                    float progress = EasingUtil.EvaluateEasing(Mathf.Clamp01(local / duration), tween.Node.Easing);
                    ApplyGroupedTween(state, tween, progress);
                    continue;
                }
                for (int tokenIndex = 0; tokenIndex < tween.TokenIds.Count; tokenIndex++)
                {
                    float local = playback.Time - tween.StartTime - Mathf.Max(0f, tween.Node.StaggerDelay) * tokenIndex;
                    if (local < 0f) continue;
                    float progress = EasingUtil.EvaluateEasing(Mathf.Clamp01(local / duration), tween.Node.Easing);
                    string tokenId = tween.TokenIds[tokenIndex];
                    if (!poses.TryGetValue(tokenId, out OvTokenPose pose)) pose = OvTokenPose.Identity;
                    ApplyTween(ref pose, tween.Node, progress);
                    poses[tokenId] = pose;
                }
            }
            for (int i = 0; i < playback.Schedule.Modifications.Count; i++)
            {
                ScheduledModification modification = playback.Schedule.Modifications[i];
                if (playback.Time < modification.StartTime
                    || !playback.AppliedModifications.Add(modification)) continue;
                for (int tokenIndex = 0; tokenIndex < modification.TokenIds.Count; tokenIndex++)
                {
                    string tokenId = modification.TokenIds[tokenIndex];
                    string content = tokenIndex == 0 ? (modification.Node.ModifyText ?? string.Empty) : string.Empty;
                    if (!textOverrides.TryGetValue(tokenId, out string current)
                        || !string.Equals(current, content, StringComparison.Ordinal))
                    {
                        textOverrides[tokenId] = content;
                        changed = true;
                    }
                }
            }
            return changed;
        }

        private static void ApplyGroupedTween(TextState state, ScheduledTween tween,
            float progress)
        {
            Dictionary<string, OvTokenPose> poses = state.Poses;
            OvAnimationNode node = tween.Node;
            if (node.TweenProperty == OvTokenTweenProperty.Opacity)
            {
                for (int i = 0; i < tween.TokenIds.Count; i++)
                {
                    string tokenId = tween.TokenIds[i];
                    if (!poses.TryGetValue(tokenId, out OvTokenPose pose)) pose = OvTokenPose.Identity;
                    ApplyTween(ref pose, node, progress);
                    poses[tokenId] = pose;
                }
                return;
            }

            float x = Mathf.Lerp(node.FromX, node.ToX, progress);
            float y = Mathf.Lerp(node.FromY, node.ToY, progress);
            OvTokenGroupTransform transform = tween.SharedTransform;
            transform.Id = tween.GroupTransformId;
            transform.Order = tween.GroupTransformOrder;
            transform.OffsetX = 0f;
            transform.OffsetY = 0f;
            transform.ScaleX = 0f;
            transform.ScaleY = 0f;
            transform.Rotation = 0f;
            switch (node.TweenProperty)
            {
                case OvTokenTweenProperty.Position:
                    transform.OffsetX = x;
                    transform.OffsetY = y;
                    break;
                case OvTokenTweenProperty.Scale:
                    transform.ScaleX = x;
                    transform.ScaleY = Math.Abs(y) < 0.0001f ? x : y;
                    break;
                case OvTokenTweenProperty.Rotation:
                    transform.Rotation = x;
                    break;
            }

            for (int i = 0; i < tween.TokenIds.Count; i++)
            {
                string tokenId = tween.TokenIds[i];
                if (!poses.TryGetValue(tokenId, out OvTokenPose pose)) pose = OvTokenPose.Identity;
                if (pose.GroupTransforms == null) pose.GroupTransforms = RentGroupTransformList(state);
                pose.GroupTransforms.Add(transform);
                poses[tokenId] = pose;
            }
        }

        private static bool EvaluateColorLayers(OverlayerText text, TextState state,
            Func<OverlayerText, string, double> resolveTagNumber)
        {
            bool changed = false;
            for (int i = 0; i < state.ColorLayers.Count; i++)
            {
                ColorLayer layer = state.ColorLayers[i];
                bool active = IsColorLayerActive(layer, state);
                if (!state.ColorLayerActiveStates.TryGetValue(layer.Node.Id, out bool previous) || previous != active)
                {
                    state.ColorLayerActiveStates[layer.Node.Id] = active;
                    changed = true;
                }
            }

            // Gradients form the base color layer.
            for (int i = 0; i < state.ColorLayers.Count; i++)
            {
                ColorLayer layer = state.ColorLayers[i];
                if (layer.Node == null || layer.Node.Kind != OvAnimationNodeKind.Effect
                    || !IsColorLayerActive(layer, state) || layer.TokenIds.Count == 0) continue;

                OvAnimationNode node = layer.Node;
                double value = node.EffectValueSource == OvEffectValueSourceKind.Constant
                    ? node.EffectConstantValue
                    : resolveTagNumber != null
                        ? resolveTagNumber(text, node.EffectSourceTag)
                        : double.NaN;
                EffectStyle style = EvaluateEffectStyle(node, layer.SortedColorPoints, value);
                if (!state.EffectStyles.TryGetValue(node.Id, out EffectStyle previousStyle)
                    || !EffectStyleEquals(previousStyle, style))
                {
                    state.EffectStyles[node.Id] = style;
                    changed = true;
                }
                if (style.Valid) ApplyEffectStyle(state, layer.TokenIds, node.Id, style);
            }

            // Triggered color changes are evaluated second and therefore only
            // override gradient channels while their upstream trigger is active.
            for (int i = 0; i < state.ColorLayers.Count; i++)
            {
                ColorLayer layer = state.ColorLayers[i];
                if (layer.Node == null || layer.Node.Kind != OvAnimationNodeKind.ColorChange
                    || !IsColorLayerActive(layer, state) || layer.TokenIds.Count == 0) continue;
                ApplyColorChangeStyle(state, layer.TokenIds, layer.Node);
            }
            return changed;
        }

        private static bool IsColorLayerActive(ColorLayer layer, TextState state)
        {
            if (layer == null || layer.Node == null) return false;
            if (!layer.HasFlowInput) return layer.Node.Kind == OvAnimationNodeKind.Effect;
            foreach (string triggerId in layer.TriggerNodeIds)
            {
                if (state.ActiveColorTriggerIds.Contains(triggerId)) return true;
            }
            return false;
        }

        private static void ApplyEffectStyle(TextState state, List<string> tokenIds, string groupId, EffectStyle style)
        {
            for (int t = 0; t < tokenIds.Count; t++)
            {
                string tokenId = tokenIds[t];
                if (!state.Poses.TryGetValue(tokenId, out OvTokenPose pose)) pose = OvTokenPose.Identity;
                if (style.Text)
                {
                    pose.HasTextColorOverride = true;
                    pose.TextColorR = style.TextColor.x;
                    pose.TextColorG = style.TextColor.y;
                    pose.TextColorB = style.TextColor.z;
                    pose.TextColorA = style.TextColor.w;
                }
                if (style.Outline)
                {
                    pose.HasOutlineColorOverride = true;
                    pose.OutlineColorR = style.OutlineColor.x;
                    pose.OutlineColorG = style.OutlineColor.y;
                    pose.OutlineColorB = style.OutlineColor.z;
                    pose.OutlineColorA = style.OutlineColor.w;
                    pose.OutlineColorGroupId = groupId;
                }
                if (style.Shadow)
                {
                    pose.HasShadowColorOverride = true;
                    pose.ShadowColorR = style.ShadowColor.x;
                    pose.ShadowColorG = style.ShadowColor.y;
                    pose.ShadowColorB = style.ShadowColor.z;
                    pose.ShadowColorA = style.ShadowColor.w;
                    pose.ShadowColorGroupId = groupId;
                }
                state.Poses[tokenId] = pose;
            }
        }

        private static void ApplyColorChangeStyle(TextState state, List<string> tokenIds, OvAnimationNode node)
        {
            Vector4 textColor = ToVector4(node.ColorChangeTextColor, new Vector4(1f, 1f, 1f, 1f));
            Vector4 outlineColor = ToVector4(node.ColorChangeOutlineColor, new Vector4(0f, 0f, 0f, 1f));
            Vector4 shadowColor = ToVector4(node.ColorChangeShadowColor, new Vector4(0f, 0f, 0f, 0.75f));
            for (int t = 0; t < tokenIds.Count; t++)
            {
                string tokenId = tokenIds[t];
                if (!state.Poses.TryGetValue(tokenId, out OvTokenPose pose)) pose = OvTokenPose.Identity;
                if (node.ColorChangeText)
                {
                    pose.HasTextColorOverride = true;
                    pose.TextColorR = textColor.x;
                    pose.TextColorG = textColor.y;
                    pose.TextColorB = textColor.z;
                    pose.TextColorA = textColor.w;
                }
                if (node.ColorChangeOutline)
                {
                    pose.HasOutlineColorOverride = true;
                    pose.OutlineColorR = outlineColor.x;
                    pose.OutlineColorG = outlineColor.y;
                    pose.OutlineColorB = outlineColor.z;
                    pose.OutlineColorA = outlineColor.w;
                    pose.OutlineColorGroupId = node.Id;
                }
                if (node.ColorChangeShadow)
                {
                    pose.HasShadowColorOverride = true;
                    pose.ShadowColorR = shadowColor.x;
                    pose.ShadowColorG = shadowColor.y;
                    pose.ShadowColorB = shadowColor.z;
                    pose.ShadowColorA = shadowColor.w;
                    pose.ShadowColorGroupId = node.Id;
                }
                state.Poses[tokenId] = pose;
            }
        }

        private static EffectStyle EvaluateEffectStyle(OvAnimationNode node, List<OvColorPoint> points, double value)
        {
            var result = new EffectStyle
            {
                Valid = !double.IsNaN(value) && !double.IsInfinity(value) && points != null && points.Count > 0,
                Text = node.EffectTextColor,
                Outline = node.EffectOutlineColor,
                Shadow = node.EffectShadowColor
            };
            if (!result.Valid || (!result.Text && !result.Outline && !result.Shadow))
            {
                result.Valid = false;
                return result;
            }

            OvColorPoint left = points[0];
            OvColorPoint right = points[points.Count - 1];
            float amount = 0f;
            if (value <= left.Value)
            {
                right = left;
            }
            else if (value >= right.Value)
            {
                left = right;
            }
            else
            {
                for (int i = 1; i < points.Count; i++)
                {
                    if (value < points[i].Value)
                    {
                        left = points[i - 1];
                        right = points[i];
                        break;
                    }
                }
                if (node.ColorMapMode == OvColorMapMode.Gradient)
                {
                    float span = right.Value - left.Value;
                    amount = Math.Abs(span) < 0.000001f ? 0f : Mathf.Clamp01(((float)value - left.Value) / span);
                }
                else
                {
                    right = left;
                }
            }

            result.TextColor = LerpColor(left.TextColor, right.TextColor, amount, new Vector4(1f, 1f, 1f, 1f));
            result.OutlineColor = LerpColor(left.OutlineColor, right.OutlineColor, amount, new Vector4(0f, 0f, 0f, 1f));
            result.ShadowColor = LerpColor(left.ShadowColor, right.ShadowColor, amount, new Vector4(0f, 0f, 0f, 1f));
            return result;
        }

        private static Vector4 LerpColor(float[] left, float[] right, float amount, Vector4 fallback)
        {
            Vector4 a = ToVector4(left, fallback);
            Vector4 b = ToVector4(right, fallback);
            return Vector4.Lerp(a, b, amount);
        }

        private static Vector4 ToVector4(float[] color, Vector4 fallback)
        {
            if (color == null || color.Length < 4) return fallback;
            return new Vector4(
                Mathf.Clamp01(color[0]),
                Mathf.Clamp01(color[1]),
                Mathf.Clamp01(color[2]),
                Mathf.Clamp01(color[3]));
        }

        private static bool EffectStyleEquals(EffectStyle left, EffectStyle right)
        {
            if (left.Valid != right.Valid || left.Text != right.Text
                || left.Outline != right.Outline || left.Shadow != right.Shadow) return false;
            if (!left.Valid) return true;
            return Approximately(left.TextColor, right.TextColor)
                && Approximately(left.OutlineColor, right.OutlineColor)
                && Approximately(left.ShadowColor, right.ShadowColor);
        }

        private static bool Approximately(Vector4 left, Vector4 right)
        {
            return Math.Abs(left.x - right.x) < 0.0005f
                && Math.Abs(left.y - right.y) < 0.0005f
                && Math.Abs(left.z - right.z) < 0.0005f
                && Math.Abs(left.w - right.w) < 0.0005f;
        }

        private static void ApplyTween(ref OvTokenPose pose, OvAnimationNode node, float progress)
        {
            float x = Mathf.Lerp(node.FromX, node.ToX, progress);
            float y = Mathf.Lerp(node.FromY, node.ToY, progress);
            switch (node.TweenProperty)
            {
                case OvTokenTweenProperty.Position:
                    pose.OffsetX += x;
                    pose.OffsetY += y;
                    break;
                case OvTokenTweenProperty.Scale:
                    pose.ScaleX *= x;
                    pose.ScaleY *= Math.Abs(y) < 0.0001f ? x : y;
                    break;
                case OvTokenTweenProperty.Rotation:
                    pose.Rotation += x;
                    break;
                case OvTokenTweenProperty.Opacity:
                    pose.Opacity *= Mathf.Clamp01(x);
                    break;
            }
        }

        private static void AddEdge(Dictionary<string, List<string>> map, string from, string to)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;
            if (!map.TryGetValue(from, out List<string> list))
            {
                list = new List<string>();
                map[from] = list;
            }
            if (!list.Contains(to)) list.Add(to);
        }
    }
}
