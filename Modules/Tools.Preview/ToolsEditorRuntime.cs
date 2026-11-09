using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ADOFAI;
using HarmonyLib;
using UnityEngine;

namespace CheryTools.Tools.Preview
{
    internal static class ToolsEditorRuntime
    {
        private static readonly LevelEventType[] BeatsAlteringLevelEvents =
        {
            LevelEventType.FreeRoam,
            LevelEventType.Pause
        };

        private static readonly LevelEventType[] SyncedLevelEvents =
        {
            LevelEventType.MoveTrack,
            LevelEventType.MoveCamera,
            LevelEventType.PositionTrack
        };

        private static scrFloor _displayFloor;
        private static string _originalText;
        private static bool _originalActive;
        private static int _refreshAfterFrame = -1;

        private static readonly Dictionary<char, int> PathAngles = new Dictionary<char, int>
        {
            { 'W', 195 }, { 'H', 210 }, { 'Q', 225 }, { 'G', 240 }, { 'q', 255 },
            { 'U', 270 }, { 'o', 285 }, { 'T', 300 }, { 'E', 315 }, { 'J', 330 },
            { 'p', 345 }, { 'R', 0 }, { 'A', 15 }, { 'M', 30 }, { 'C', 45 },
            { 'B', 60 }, { 'Y', 75 }, { 'D', 90 }, { 'V', 105 }, { 'F', 120 },
            { 'Z', 135 }, { 'N', 150 }, { 'x', 165 }, { 'L', 180 }
        };

        internal enum FloorMutationType
        {
            HorizontalFlip,
            VerticalFlip,
            BiDirectionFlip,
            Rotation
        }

        internal static void RequestRefreshNextFrame()
        {
            _refreshAfterFrame = Time.frameCount + 1;
        }

        internal static void Update(scnEditor editor)
        {
            if (_refreshAfterFrame >= 0 && Time.frameCount >= _refreshAfterFrame)
                _refreshAfterFrame = -1;

            ToolsEditorSettings settings = ToolsRuntime.Settings?.Editor;
            bool editorPlaying = false;
            try { editorPlaying = editor != null && editor.playMode; }
            catch { editorPlaying = EditorRuntimeCompatibility.IsPlayMode(editor); }
            if (!ToolsRuntime.IsActive || settings == null || editor == null || editorPlaying ||
                EditorRuntimeCompatibility.IsPlayMode(editor) || editor.SelectionIsEmpty())
            {
                Restore();
                return;
            }

            if (editor.showFloorNums)
            {
                Restore(false);
                return;
            }

            scrFloor floor = ResolveDisplayFloor(editor);
            if (floor == null || floor.editorNumText == null || floor.editorNumText.letterText == null)
            {
                Restore();
                return;
            }

            if (_displayFloor != floor)
            {
                Restore();
                _displayFloor = floor;
                _originalText = floor.editorNumText.letterText.text;
                _originalActive = floor.editorNumText.gameObject.activeSelf;
            }

            ADOBase.lm.CalculateFloorAngleLengths();
            double angle = 0d;
            int count = editor.selectedFloors.Count;
            int iterations = settings.UseTulttakBehavior && count > 1 ? count - 1 : count;
            for (int i = 0; i < iterations; i++)
            {
                scrFloor selected = editor.selectedFloors[i];
                if (selected == null || selected.seqID == editor.floors.Count - 1) continue;
                float speedFactor = i == 0 || selected.speed == 0f ? 1f : editor.selectedFloors[0].speed / selected.speed;
                angle += selected.angleLength * speedFactor * Mathf.Rad2Deg;

                if (editor.events != null)
                {
                    foreach (LevelEvent levelEvent in editor.events)
                    {
                        if (levelEvent == null || !levelEvent.active || levelEvent.floor != selected.seqID ||
                            !BeatsAlteringLevelEvents.Contains(levelEvent.eventType)) continue;
                        double extra = levelEvent.eventType == LevelEventType.Pause
                            ? levelEvent.GetFloat("duration") * 180d
                            : levelEvent.GetInt("duration") * 180d;
                        angle += extra * speedFactor;
                    }
                }
            }

            StringBuilder text = new StringBuilder();
            if (settings.ShowFloorAngle) text.Append("<color=#ff5252>").Append(angle.ToString("#.####")).Append("°</color>");
            if (settings.ShowFloorBeats)
            {
                AddLine(text);
                text.Append("<color=#52a9ff>").Append((angle / 180d).ToString("#.####")).Append("♩</color>");
            }
            if (settings.ShowFloorCount)
            {
                AddLine(text);
                text.Append("<color=#8a8a8a>").Append(count).Append("#</color>");
            }
            if (settings.ShowFloorDuration && editor.levelData != null && count > 0)
            {
                AddLine(text);
                double bpm = editor.levelData.bpm;
                double speed = editor.selectedFloors[0].speed;
                double seconds = speed == 0 || bpm == 0 ? 0 : angle / (speed * bpm * 3d);
                text.Append("<color=#ffffff>").Append(seconds.ToString("0.######")).Append("s</color>");
            }

            if (text.Length == 0)
            {
                Restore();
                return;
            }
            floor.editorNumText.gameObject.SetActive(true);
            floor.editorNumText.letterText.text = text.ToString();

            if (settings.FineTuneFloorRotations) ApplyFineRotation(editor);
        }

        internal static void Restore()
        {
            Restore(true);
        }

        private static void Restore(bool deactivate)
        {
            if (_displayFloor != null && _displayFloor.editorNumText != null && _displayFloor.editorNumText.letterText != null)
            {
                _displayFloor.editorNumText.letterText.text = _originalText ?? _displayFloor.seqID.ToString();
                if (deactivate) _displayFloor.editorNumText.gameObject.SetActive(_originalActive);
            }
            _displayFloor = null;
            _originalText = null;
            _refreshAfterFrame = -1;
        }

        private static scrFloor ResolveDisplayFloor(scnEditor editor)
        {
            if (_displayFloor != null && editor.selectedFloors.Contains(_displayFloor) && _displayFloor.enabled)
                return _displayFloor;

            for (int i = 0; i < editor.selectedFloors.Count; i++)
            {
                scrFloor floor = editor.selectedFloors[i];
                if (floor != null && floor.enabled) return floor;
            }

            Vector3 cameraPosition = scrCamera.instance != null ? scrCamera.instance.transform.position : Vector3.zero;
            scrFloor closest = null;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < editor.selectedFloors.Count; i++)
            {
                scrFloor floor = editor.selectedFloors[i];
                if (floor == null) continue;
                float distance = Vector3.Distance(floor.transform.position, cameraPosition);
                if (distance < closestDistance) { closestDistance = distance; closest = floor; }
            }
            return closest;
        }

        private static void ApplyFineRotation(scnEditor editor)
        {
            if (!(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) ||
                !(Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))) return;
            bool clockwise = Input.GetKeyDown(KeyCode.Period);
            bool counterClockwise = Input.GetKeyDown(KeyCode.Comma);
            if ((!clockwise && !counterClockwise) || editor.SelectionIsEmpty() || editor.levelData == null) return;

            int first = editor.selectedFloors[0].seqID - 1;
            int count = editor.selectedFloors.Count;
            if (first < 0) return;
            bool oldLevel = editor.levelData.isOldLevel;
            float direction = clockwise ? -1f : 1f;
            if (oldLevel)
            {
                if (string.IsNullOrEmpty(editor.levelData.pathData) || first + count > editor.levelData.pathData.Length) return;
                editor.levelData.pathData = editor.levelData.pathData.Remove(first, count);
                for (int i = 0; i < count; i++)
                {
                    scrFloor floor = editor.selectedFloors[i];
                    editor.levelData.pathData = editor.levelData.pathData.Insert(first + i,
                        RotatePath(floor.stringDirection, clockwise).ToString());
                }
            }
            else
            {
                if (editor.levelData.angleData == null || first + count > editor.levelData.angleData.Count) return;
                editor.levelData.angleData.RemoveRange(first, count);
                for (int i = 0; i < count; i++)
                {
                    scrFloor floor = editor.selectedFloors[i];
                    float angle = floor.floatDirection == 999f ? floor.floatDirection :
                        floor.floatDirection + direction * ToolsRuntime.Settings.Editor.FloorRotationStep;
                    editor.levelData.angleData.Insert(first + i, angle);
                }
            }

            editor.RemakePath();
            SyncTrackEvents(editor, FloorMutationType.Rotation, (clockwise ? -1f : 1f) *
                (oldLevel ? 15f : ToolsRuntime.Settings.Editor.FloorRotationStep) * Mathf.Deg2Rad);
            RequestRefreshNextFrame();
        }

        private static char RotatePath(char path, bool clockwise)
        {
            int angle;
            if (!PathAngles.TryGetValue(path, out angle)) return path;
            int target = (angle + (clockwise ? 15 : -15) + 360) % 360;
            foreach (KeyValuePair<char, int> pair in PathAngles)
                if (pair.Value == target) return pair.Key;
            return path;
        }

        private static void SyncTrackEvents(scnEditor editor, FloorMutationType mutation, float radians = 0f)
        {
            ToolsEditorSettings settings = ToolsRuntime.Settings?.Editor;
            if (settings == null || !settings.SyncLevelEventValues || editor.events == null) return;
            var floors = new HashSet<int>();
            for (int i = 0; i < editor.selectedFloors.Count; i++)
                if (editor.selectedFloors[i] != null) floors.Add(editor.selectedFloors[i].seqID);
            foreach (LevelEvent levelEvent in editor.events)
            {
                if (levelEvent == null || !levelEvent.active || !floors.Contains(levelEvent.floor) ||
                    !SyncedLevelEvents.Contains(levelEvent.eventType)) continue;
                string key = levelEvent.eventType == LevelEventType.MoveCamera ? "position" : "positionOffset";
                Vector2 value;
                try { value = (Vector2)levelEvent[key]; }
                catch { continue; }
                if (float.IsNaN(value.x)) value.x = 0f;
                if (float.IsNaN(value.y)) value.y = 0f;
                switch (mutation)
                {
                    case FloorMutationType.HorizontalFlip: value.x = -value.x; break;
                    case FloorMutationType.VerticalFlip: value.y = -value.y; break;
                    case FloorMutationType.BiDirectionFlip: value = -value; break;
                    case FloorMutationType.Rotation:
                        float x = value.x * Mathf.Cos(radians) - value.y * Mathf.Sin(radians);
                        value.y = value.x * Mathf.Sin(radians) + value.y * Mathf.Cos(radians);
                        value.x = x;
                        break;
                }
                levelEvent[key] = value;
            }
            editor.ApplyEventsToFloors();
        }

        internal static void SyncAfterMutation(scnEditor editor, FloorMutationType mutation, float radians = 0f)
        {
            if (editor == null || editor.SelectionIsEmpty()) return;
            SyncTrackEvents(editor, mutation, radians);
        }

        private static void AddLine(StringBuilder text)
        {
            if (text.Length > 0) text.Append('\n');
        }
    }

    [HarmonyPatch(typeof(scnEditor), "Update")]
    internal static class ToolsEditorUpdatePatch
    {
        private static void Postfix(scnEditor __instance)
        {
            ToolsEditorRuntime.Update(__instance);
        }
    }

    [HarmonyPatch(typeof(scnEditor), "OnSelectedFloorChange")]
    internal static class ToolsEditorSelectionPatch
    {
        private static void Postfix(scnEditor __instance) => ToolsEditorRuntime.Update(__instance);
    }

    [HarmonyPatch(typeof(scnEditor), "RemakePath")]
    internal static class ToolsEditorRemakePathPatch
    {
        private static void Postfix() => ToolsEditorRuntime.RequestRefreshNextFrame();
    }

    [HarmonyPatch(typeof(scnEditor), "FlipFloor")]
    [HarmonyPatch(typeof(scnEditor), "FlipSelection")]
    internal static class ToolsEditorFlipPatch
    {
        private static void Postfix(scnEditor __instance, bool horizontal)
        {
            ToolsEditorRuntime.SyncAfterMutation(__instance,
                horizontal ? ToolsEditorRuntime.FloorMutationType.HorizontalFlip :
                ToolsEditorRuntime.FloorMutationType.VerticalFlip);
            ToolsEditorRuntime.RequestRefreshNextFrame();
        }
    }

    [HarmonyPatch(typeof(scnEditor), "RotateFloor")]
    [HarmonyPatch(typeof(scnEditor), "RotateSelection")]
    internal static class ToolsEditorRotatePatch
    {
        private static void Postfix(scnEditor __instance, bool CW)
        {
            ToolsEditorRuntime.SyncAfterMutation(__instance, ToolsEditorRuntime.FloorMutationType.Rotation,
                Mathf.PI / 2f * (CW ? -1f : 1f));
            ToolsEditorRuntime.RequestRefreshNextFrame();
        }
    }

    [HarmonyPatch(typeof(scnEditor), "RotateFloor180")]
    [HarmonyPatch(typeof(scnEditor), "RotateSelection180")]
    internal static class ToolsEditorRotate180Patch
    {
        private static void Postfix(scnEditor __instance)
        {
            ToolsEditorRuntime.SyncAfterMutation(__instance, ToolsEditorRuntime.FloorMutationType.BiDirectionFlip);
            ToolsEditorRuntime.RequestRefreshNextFrame();
        }
    }
}
