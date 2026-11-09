using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheryTools.ReadHelper.Preview
{
    public sealed class TimelineFloorNote
    {
        public int SeqId;
        public double HitTime;
        public bool IsDoubleTap;
        public bool IsSpeedChange;
        public bool IsDecel;
        public bool IsAccel;
        public float SpeedMultiplier;
        public float EffectiveBpm;
        public string LabelText;
    }

    public sealed class ReadHelperDataManager
    {
        private object _lastFloorsRef;
        private int _lastFloorCount = -1;
        private int _lastControllerId = -1;
        private float _lastAngleThreshold = -1f;
        private string _lastTimeSignature = string.Empty;

        private readonly List<TimelineFloorNote> _notes = new List<TimelineFloorNote>(1024);

        public IReadOnlyList<TimelineFloorNote> Notes => _notes;

        public bool HasNotes => _notes.Count > 0;

        public void CheckAndRefresh(ReadHelperSettings settings)
        {
            try
            {
                scrController controller = scrController.instance;
                int controllerId = controller != null ? controller.GetInstanceID() : 0;
                var floors = scrLevelMaker.instance?.listFloors;
                int floorCount = floors != null ? floors.Count : 0;

                bool changed = controllerId != _lastControllerId
                    || !ReferenceEquals(floors, _lastFloorsRef)
                    || floorCount != _lastFloorCount
                    || Math.Abs(settings.DoubleTapAngleThreshold - _lastAngleThreshold) > 0.01f
                    || !string.Equals(settings.TimeSignature, _lastTimeSignature, StringComparison.Ordinal);

                if (changed)
                {
                    _lastControllerId = controllerId;
                    _lastFloorsRef = floors;
                    _lastFloorCount = floorCount;
                    _lastAngleThreshold = settings.DoubleTapAngleThreshold;
                    _lastTimeSignature = settings.TimeSignature;

                    RebuildNotes(floors, settings);
                }
            }
            catch
            {
                // 忽略非关卡状态下的异常
            }
        }

        private void RebuildNotes(List<scrFloor> floors, ReadHelperSettings settings)
        {
            _notes.Clear();
            if (floors == null || floors.Count == 0) return;

            double baseBpm = 100.0;
            if (scrConductor.instance != null && scrConductor.instance.bpm > 0f)
                baseBpm = scrConductor.instance.bpm;
            else if (scnEditor.instance?.levelData != null && scnEditor.instance.levelData.bpm > 0)
                baseBpm = scnEditor.instance.levelData.bpm;
            else if (scrLevelMaker.instance != null && scrLevelMaker.instance.bpm_forUnityUseOnly > 0f)
                baseBpm = scrLevelMaker.instance.bpm_forUnityUseOnly;

            float prevSpeed = 1f;

            for (int i = 0; i < floors.Count; i++)
            {
                scrFloor floor = floors[i];
                if (floor == null) continue;

                // 连续方块夹角计算：angleLength 在 ADOFAI 内存中以弧度存储
                double angleDeg = floor.angleLength * Mathf.Rad2Deg;

                // 双押判定：如果与前一个方块的角度 < 用户配置阈值（默认 30°）
                // 且不是首个出生块（i > 0）
                bool isDoubleTap = i > 0 && angleDeg >= 0d && angleDeg < settings.DoubleTapAngleThreshold;

                float currentSpeed = floor.speed;
                bool isSpeedChange = false;
                bool isDecel = false;
                bool isAccel = false;
                float ratio = 1f;
                float effectiveBpm = (float)(baseBpm * currentSpeed);
                string labelText = string.Empty;

                if (i == 0 || i == 1)
                {
                    // 关卡首拍音符，显示初始拍号与 BPM
                    isSpeedChange = true;
                    isDecel = false;
                    isAccel = false;
                    ratio = 1f;
                    labelText = string.Format("{0} {1:0}BPM", settings.TimeSignature, effectiveBpm);
                    prevSpeed = currentSpeed;
                }
                else if (Mathf.Abs(currentSpeed - prevSpeed) > 0.001f)
                {
                    isSpeedChange = true;
                    isDecel = currentSpeed < prevSpeed;
                    isAccel = currentSpeed > prevSpeed;
                    ratio = prevSpeed > 0.0001f ? currentSpeed / prevSpeed : currentSpeed;
                    labelText = string.Format("{0} {1:0}BPM x{2:0.##}", settings.TimeSignature, effectiveBpm, ratio);
                    prevSpeed = currentSpeed;
                }

                var note = new TimelineFloorNote
                {
                    SeqId = floor.seqID,
                    HitTime = floor.entryTime,
                    IsDoubleTap = isDoubleTap,
                    IsSpeedChange = isSpeedChange,
                    IsDecel = isDecel,
                    IsAccel = isAccel,
                    SpeedMultiplier = ratio,
                    EffectiveBpm = effectiveBpm,
                    LabelText = labelText
                };

                _notes.Add(note);
            }
        }

        public void GetVisibleNotes(double currentTime, float lookAhead, List<TimelineFloorNote> visibleNotes)
        {
            visibleNotes.Clear();
            if (_notes.Count == 0) return;

            double minTime = currentTime - 0.06;
            double maxTime = currentTime + lookAhead;

            // 二分查找区间起点
            int low = 0;
            int high = _notes.Count - 1;
            int startIndex = 0;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (_notes[mid].HitTime >= minTime)
                {
                    startIndex = mid;
                    high = mid - 1;
                }
                else
                {
                    low = mid + 1;
                }
            }

            for (int i = startIndex; i < _notes.Count; i++)
            {
                TimelineFloorNote note = _notes[i];
                if (note.HitTime > maxTime) break;
                if (note.HitTime >= minTime)
                {
                    visibleNotes.Add(note);
                }
            }
        }
    }
}
