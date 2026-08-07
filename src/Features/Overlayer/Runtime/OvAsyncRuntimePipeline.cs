using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace CheryTools
{
    /// <summary>
    /// Values captured from Unity/ADOFAI on the main thread.  The worker must
    /// never dereference a Unity object; only these copied primitive values may
    /// cross the thread boundary.
    /// </summary>
    internal struct OvRuntimeMainSnapshot
    {
        public long FrameId;
        public bool Active;
        public bool ResetRuntimeBeforeProcessing;
        public bool TrackJudgements;
        public float TimelineTime;
        public float TimelineDeltaTime;
        public bool CalculateFps;
        public float FpsRefreshInterval;

        public bool AnyKeyDown;
        public KeyCode[] KeysDown;
        public KeyCode[] KeysUp;

        public bool AutoplayEnabled;
        public bool NoFailEnabled;
        public int JudgementMode;

        public int ControllerInstanceId;
        public int ControllerState;
        public OvRuntimeBeatEvent[] BeatEvents;

        public int TrackerInstanceId;
        public int TrackerGeneration;
        public int[] BootstrapJudgements;
        public long BootstrapJudgementSequence;
    }

    internal struct OvRuntimeBeatEvent
    {
        public int ConductorInstanceId;
        public int BeatNumber;
    }

    internal struct OvRuntimeJudgementEvent
    {
        public long Sequence;
        public int TrackerInstanceId;
        public int Judgement;
    }

    /// <summary>
    /// Pure-data result returned by the OV worker.  Results are queued rather
    /// than simply replacing the previous result so short-lived trigger events
    /// cannot disappear when the game thread and worker run at different rates.
    /// </summary>
    internal struct OvRuntimeComputedFrame
    {
        public long SourceFrameId;
        public bool Reset;
        public bool RenderStateChanged;

        public bool FpsUpdated;
        public float Fps;

        public int PureCombo;
        public int PerfectCombo;
        public bool ComboIncreased;
        public bool PureComboBroken;
        public bool PerfectComboBroken;
        public int[] Judgements;

        public bool AnyKeyDown;
        public KeyCode[] KeysDown;
        public KeyCode[] KeysUp;

        public bool BeatHappened;
        public int BeatNumber;
        public bool LevelStarted;
        public bool LevelEnded;

        public bool AutoplayEnabled;
        public bool NoFailEnabled;
        public int JudgementMode;
    }

    /// <summary>
    /// Single event-driven worker for the lightweight OV runtime state machine.
    /// Unity state is sampled by OverlayerManager; this class performs only
    /// primitive/collection calculations and therefore remains thread-safe.
    /// </summary>
    internal sealed class OvAsyncRuntimePipeline : IDisposable
    {
        private readonly object _snapshotGate = new object();
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly ConcurrentQueue<OvRuntimeJudgementEvent> _judgements
            = new ConcurrentQueue<OvRuntimeJudgementEvent>();
        private readonly ConcurrentQueue<OvRuntimeComputedFrame> _results
            = new ConcurrentQueue<OvRuntimeComputedFrame>();

        private Thread _worker;
        private OvRuntimeMainSnapshot _pendingSnapshot;
        private bool _hasPendingSnapshot;
        private volatile bool _running;
        private int _acceptJudgements;
        private long _judgementSequence;
        private string _pendingError;

        // Worker-owned state.  No Unity object may be added here.
        private int _trackerGeneration = int.MinValue;
        private int _pureCombo;
        private int _perfectCombo;
        private int _controllerInstanceId;
        private int _lastControllerState;
        private bool _controllerStateInitialized;
        private int _conductorInstanceId;
        private int _lastBeatNumber = int.MinValue;
        private float _lastFpsSampleTime = -1f;
        private bool _hasPublishedState;
        private bool _publishedAutoplayEnabled;
        private bool _publishedNoFailEnabled;
        private int _publishedJudgementMode;

        public long LastEnqueuedJudgementSequence
        {
            get { return Interlocked.Read(ref _judgementSequence); }
        }

        public bool HasPendingJudgements
        {
            get { return !_judgements.IsEmpty; }
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "CheryTools OV Runtime",
                Priority = System.Threading.ThreadPriority.BelowNormal
            };
            _worker.Start();
        }

        public void Publish(OvRuntimeMainSnapshot snapshot)
        {
            if (!_running) return;
            Interlocked.Exchange(ref _acceptJudgements,
                snapshot.Active && snapshot.TrackJudgements ? 1 : 0);

            lock (_snapshotGate)
            {
                if (_hasPendingSnapshot)
                {
                    // Keep transient input gathered by a snapshot which the
                    // worker has not consumed yet, while replacing stale state
                    // values with the newest main-thread sample.
                    snapshot.AnyKeyDown |= _pendingSnapshot.AnyKeyDown;
                    snapshot.KeysDown = MergeUnique(_pendingSnapshot.KeysDown, snapshot.KeysDown);
                    snapshot.KeysUp = MergeUnique(_pendingSnapshot.KeysUp, snapshot.KeysUp);
                    snapshot.BeatEvents = MergeBeatEvents(_pendingSnapshot.BeatEvents, snapshot.BeatEvents);
                    snapshot.ResetRuntimeBeforeProcessing |= _pendingSnapshot.ResetRuntimeBeforeProcessing
                        || !_pendingSnapshot.Active;

                    // A bootstrap marks an exact reset boundary and must not be
                    // lost if the latest frame no longer carries its payload.
                    if (snapshot.BootstrapJudgements == null
                        && _pendingSnapshot.TrackerGeneration == snapshot.TrackerGeneration
                        && _pendingSnapshot.BootstrapJudgements != null)
                    {
                        snapshot.TrackerGeneration = _pendingSnapshot.TrackerGeneration;
                        snapshot.TrackerInstanceId = _pendingSnapshot.TrackerInstanceId;
                        snapshot.BootstrapJudgements = _pendingSnapshot.BootstrapJudgements;
                        snapshot.BootstrapJudgementSequence = _pendingSnapshot.BootstrapJudgementSequence;
                    }
                }

                _pendingSnapshot = snapshot;
                _hasPendingSnapshot = true;
            }
            _wake.Set();
        }

        public void EnqueueJudgement(int trackerInstanceId, int judgement)
        {
            if (!_running || Volatile.Read(ref _acceptJudgements) == 0) return;
            long sequence = Interlocked.Increment(ref _judgementSequence);
            _judgements.Enqueue(new OvRuntimeJudgementEvent
            {
                Sequence = sequence,
                TrackerInstanceId = trackerInstanceId,
                Judgement = judgement
            });
            // Judgements are applied against a main-thread snapshot. Waking the
            // worker without one only causes an empty context switch; the manager
            // observes HasPendingJudgements and publishes on its next Update.
        }

        public bool TryDequeue(out OvRuntimeComputedFrame frame)
        {
            return _results.TryDequeue(out frame);
        }

        public string ConsumeError()
        {
            return Interlocked.Exchange(ref _pendingError, null);
        }

        public void Dispose()
        {
            if (!_running)
            {
                _wake.Dispose();
                return;
            }

            _running = false;
            Interlocked.Exchange(ref _acceptJudgements, 0);
            _wake.Set();
            try
            {
                if (_worker != null && _worker.IsAlive) _worker.Join(1000);
            }
            catch
            {
            }
            _worker = null;
            _wake.Dispose();
            ClearQueue(_judgements);
            ClearQueue(_results);
        }

        private void WorkerLoop()
        {
            while (_running)
            {
                try
                {
                    _wake.WaitOne();
                    if (!_running) break;

                    while (TryTakeSnapshot(out OvRuntimeMainSnapshot snapshot))
                    {
                        ProcessSnapshot(snapshot);
                    }
                }
                catch (Exception ex)
                {
                    // Do not log from the worker and do not flood UMM.  The
                    // manager consumes this slot and reports it once.
                    Interlocked.CompareExchange(ref _pendingError,
                        ex.GetType().Name + ": " + ex.Message, null);
                }
            }
        }

        private bool TryTakeSnapshot(out OvRuntimeMainSnapshot snapshot)
        {
            lock (_snapshotGate)
            {
                if (!_hasPendingSnapshot)
                {
                    snapshot = default(OvRuntimeMainSnapshot);
                    return false;
                }
                snapshot = _pendingSnapshot;
                _pendingSnapshot = default(OvRuntimeMainSnapshot);
                _hasPendingSnapshot = false;
                return true;
            }
        }

        private void ProcessSnapshot(OvRuntimeMainSnapshot snapshot)
        {
            if (!snapshot.Active)
            {
                ResetWorkerState();
                ClearQueue(_judgements);
                _results.Enqueue(new OvRuntimeComputedFrame
                {
                    SourceFrameId = snapshot.FrameId,
                    Reset = true,
                    JudgementMode = (int)OvJudgementMode.Normal
                });
                return;
            }

            if (snapshot.ResetRuntimeBeforeProcessing)
            {
                ResetWorkerState();
            }

            var frame = new OvRuntimeComputedFrame
            {
                SourceFrameId = snapshot.FrameId,
                AnyKeyDown = snapshot.AnyKeyDown,
                KeysDown = snapshot.KeysDown,
                KeysUp = snapshot.KeysUp,
                AutoplayEnabled = snapshot.AutoplayEnabled,
                NoFailEnabled = snapshot.NoFailEnabled,
                JudgementMode = snapshot.JudgementMode
            };

            ProcessFps(snapshot, ref frame);
            ProcessLevelState(snapshot, ref frame);
            ProcessBeatEvents(snapshot.BeatEvents, ref frame);
            ProcessJudgements(snapshot, ref frame);

            frame.PureCombo = _pureCombo;
            frame.PerfectCombo = _perfectCombo;
            bool stateChanged = !_hasPublishedState
                || frame.AutoplayEnabled != _publishedAutoplayEnabled
                || frame.NoFailEnabled != _publishedNoFailEnabled
                || frame.JudgementMode != _publishedJudgementMode;
            bool hasTransient = frame.AnyKeyDown
                || (frame.KeysDown != null && frame.KeysDown.Length > 0)
                || (frame.KeysUp != null && frame.KeysUp.Length > 0)
                || frame.ComboIncreased
                || frame.PureComboBroken
                || frame.PerfectComboBroken
                || frame.BeatHappened
                || frame.LevelStarted
                || frame.LevelEnded
                || (frame.Judgements != null && frame.Judgements.Length > 0);
            if (stateChanged || hasTransient || frame.FpsUpdated || frame.RenderStateChanged)
            {
                _results.Enqueue(frame);
                _hasPublishedState = true;
                _publishedAutoplayEnabled = frame.AutoplayEnabled;
                _publishedNoFailEnabled = frame.NoFailEnabled;
                _publishedJudgementMode = frame.JudgementMode;
            }
        }

        private void ProcessFps(OvRuntimeMainSnapshot snapshot, ref OvRuntimeComputedFrame frame)
        {
            if (!snapshot.CalculateFps) return;
            float interval = snapshot.FpsRefreshInterval;
            if (interval <= 0f || float.IsNaN(interval) || float.IsInfinity(interval)) interval = 0.25f;
            if (_lastFpsSampleTime >= 0f && snapshot.TimelineTime - _lastFpsSampleTime < interval) return;

            float delta = snapshot.TimelineDeltaTime;
            if (delta <= 0.000001f || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            frame.Fps = 1f / delta;
            frame.FpsUpdated = true;
            frame.RenderStateChanged = true;
            _lastFpsSampleTime = snapshot.TimelineTime;
        }

        private void ProcessLevelState(OvRuntimeMainSnapshot snapshot, ref OvRuntimeComputedFrame frame)
        {
            int controllerId = snapshot.ControllerInstanceId;
            int currentState = snapshot.ControllerState;
            if (controllerId != _controllerInstanceId)
            {
                if (_controllerInstanceId != 0
                    && _controllerStateInitialized
                    && !IsTerminalLevelState(_lastControllerState)
                    && controllerId == 0)
                {
                    frame.LevelEnded = true;
                }

                _controllerInstanceId = controllerId;
                _controllerStateInitialized = false;
                _lastControllerState = (int)States.None;
            }

            if (controllerId == 0) return;
            if (!_controllerStateInitialized)
            {
                _controllerStateInitialized = true;
                _lastControllerState = currentState;
                return;
            }
            if (currentState == _lastControllerState) return;

            if (currentState == (int)States.PlayerControl
                && _lastControllerState != (int)States.PlayerControl)
            {
                frame.LevelStarted = true;
                _lastBeatNumber = int.MinValue;
            }
            if (IsTerminalLevelState(currentState) && !IsTerminalLevelState(_lastControllerState))
            {
                frame.LevelEnded = true;
            }
            _lastControllerState = currentState;
        }

        private void ProcessBeatEvents(OvRuntimeBeatEvent[] beats, ref OvRuntimeComputedFrame frame)
        {
            if (beats == null) return;
            for (int i = 0; i < beats.Length; i++)
            {
                OvRuntimeBeatEvent beat = beats[i];
                if (beat.ConductorInstanceId != _conductorInstanceId)
                {
                    _conductorInstanceId = beat.ConductorInstanceId;
                    _lastBeatNumber = int.MinValue;
                }
                if (beat.BeatNumber == _lastBeatNumber) continue;
                _lastBeatNumber = beat.BeatNumber;
                frame.BeatHappened = true;
                frame.BeatNumber = beat.BeatNumber;
            }
        }

        private void ProcessJudgements(OvRuntimeMainSnapshot snapshot, ref OvRuntimeComputedFrame frame)
        {
            long bootstrapWatermark = long.MinValue;
            if (snapshot.TrackerGeneration != _trackerGeneration)
            {
                _trackerGeneration = snapshot.TrackerGeneration;
                _pureCombo = 0;
                _perfectCombo = 0;
                bootstrapWatermark = snapshot.BootstrapJudgementSequence;
                int[] bootstrap = snapshot.BootstrapJudgements;
                if (bootstrap != null)
                {
                    for (int i = 0; i < bootstrap.Length; i++)
                    {
                        ApplyJudgement(bootstrap[i], false, ref frame);
                    }
                }
                frame.RenderStateChanged = true;
            }

            if (!snapshot.TrackJudgements)
            {
                ClearQueue(_judgements);
                return;
            }

            List<int> emitted = null;
            while (_judgements.TryDequeue(out OvRuntimeJudgementEvent item))
            {
                if (item.TrackerInstanceId != snapshot.TrackerInstanceId) continue;
                if (item.Sequence <= bootstrapWatermark) continue;
                if (emitted == null) emitted = new List<int>(4);
                emitted.Add(item.Judgement);
                ApplyJudgement(item.Judgement, true, ref frame);
            }

            if (emitted == null) return;
            frame.Judgements = emitted.ToArray();
            frame.RenderStateChanged = true;
        }

        private void ApplyJudgement(int judgement, bool emitEvents, ref OvRuntimeComputedFrame frame)
        {
            int previousPure = _pureCombo;
            int previousPerfect = _perfectCombo;
            bool increased = false;
            HitMargin hit = (HitMargin)judgement;
            if (hit == HitMargin.Perfect || hit == HitMargin.Auto)
            {
                _pureCombo++;
                _perfectCombo++;
                increased = true;
            }
            else if (hit == HitMargin.EarlyPerfect || hit == HitMargin.LatePerfect)
            {
                _pureCombo = 0;
                _perfectCombo++;
                increased = true;
            }
            else
            {
                _pureCombo = 0;
                _perfectCombo = 0;
            }

            if (!emitEvents) return;
            if (increased) frame.ComboIncreased = true;
            if (previousPure > 0 && _pureCombo == 0) frame.PureComboBroken = true;
            if (previousPerfect > 0 && _perfectCombo == 0) frame.PerfectComboBroken = true;
        }

        private void ResetWorkerState()
        {
            _trackerGeneration = int.MinValue;
            _pureCombo = 0;
            _perfectCombo = 0;
            _controllerInstanceId = 0;
            _lastControllerState = (int)States.None;
            _controllerStateInitialized = false;
            _conductorInstanceId = 0;
            _lastBeatNumber = int.MinValue;
            _lastFpsSampleTime = -1f;
            _hasPublishedState = false;
            _publishedAutoplayEnabled = false;
            _publishedNoFailEnabled = false;
            _publishedJudgementMode = (int)OvJudgementMode.Normal;
        }

        private static bool IsTerminalLevelState(int state)
        {
            return state == (int)States.Won || state == (int)States.Fail || state == (int)States.Fail2;
        }

        private static T[] MergeUnique<T>(T[] first, T[] second)
        {
            if (first == null || first.Length == 0) return second;
            if (second == null || second.Length == 0) return first;
            var result = new List<T>(first.Length + second.Length);
            var seen = new HashSet<T>();
            for (int i = 0; i < first.Length; i++)
            {
                if (seen.Add(first[i])) result.Add(first[i]);
            }
            for (int i = 0; i < second.Length; i++)
            {
                if (seen.Add(second[i])) result.Add(second[i]);
            }
            return result.ToArray();
        }

        private static OvRuntimeBeatEvent[] MergeBeatEvents(OvRuntimeBeatEvent[] first, OvRuntimeBeatEvent[] second)
        {
            if (first == null || first.Length == 0) return second;
            if (second == null || second.Length == 0) return first;
            var result = new List<OvRuntimeBeatEvent>(first.Length + second.Length);
            for (int i = 0; i < first.Length; i++) AddBeatUnique(result, first[i]);
            for (int i = 0; i < second.Length; i++) AddBeatUnique(result, second[i]);
            return result.ToArray();
        }

        private static void AddBeatUnique(List<OvRuntimeBeatEvent> result, OvRuntimeBeatEvent item)
        {
            for (int i = 0; i < result.Count; i++)
            {
                if (result[i].ConductorInstanceId == item.ConductorInstanceId
                    && result[i].BeatNumber == item.BeatNumber) return;
            }
            result.Add(item);
        }

        private static void ClearQueue<T>(ConcurrentQueue<T> queue)
        {
            while (queue.TryDequeue(out _))
            {
            }
        }
    }
}
