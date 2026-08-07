using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace CheryTools
{
    /// <summary>
    /// Tracks per-level persistent playtime and values that belong to the current
    /// level session. This intentionally lives outside Settings.xml so importing a
    /// .cyt configuration cannot overwrite a player's accumulated statistics.
    /// </summary>
    internal sealed class OvLevelStatsTracker : MonoBehaviour
    {
        private const string StatsFileName = "OvLevelStats.json";
        private const float SaveIntervalSeconds = 30f;

        [Serializable]
        private sealed class StatsDocument
        {
            public Dictionary<string, double> LevelPlaytimeSeconds = new Dictionary<string, double>();
        }

        public static OvLevelStatsTracker Instance { get; private set; }

        private StatsDocument _document = new StatsDocument();
        private string _activeLevelKey = string.Empty;
        private string _activeLevelIdentity = string.Empty;
        private string _activeWorldIdentity = string.Empty;
        private int _activeControllerId;
        private double _currentLevelPlaytime;
        private int _currentAttempts;
        private float _nextAttemptsRefreshTime;
        private float _minFps = float.PositiveInfinity;
        private float _maxFps;
        private float _nextSaveTime;
        private bool _dirty;

        private readonly List<int> _checkpointSeqIds = new List<int>();
        private readonly HashSet<int> _passedCheckpointSeqIds = new HashSet<int>();
        private int _nextCheckpointIndex;
        private int _checkpointFloorCount = -1;
        private bool _checkpointScanReady;

        public static double CurrentLevelPlaytimeSeconds => Instance != null ? Instance._currentLevelPlaytime : 0.0;
        public static int CurrentCheckpointCount => Instance != null ? Instance._passedCheckpointSeqIds.Count : 0;
        public static int TotalCheckpointCount => Instance != null ? Instance._checkpointSeqIds.Count : 0;
        public static float CurrentMinFps => Instance != null && !float.IsPositiveInfinity(Instance._minFps) ? Instance._minFps : 0f;
        public static float CurrentMaxFps => Instance != null ? Instance._maxFps : 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            Load();
        }

        private void Update()
        {
            scrController controller = scrController.instance;
            if (controller == null || !controller.gameworld)
            {
                EndSession();
                return;
            }

            int controllerId = controller.GetInstanceID();
            string levelIdentity = GetCurrentLevelIdentity(controller);
            string worldIdentity = GetCurrentWorldIdentity();
            if (controllerId != _activeControllerId
                || !string.Equals(levelIdentity, _activeLevelIdentity, StringComparison.Ordinal)
                || !string.Equals(worldIdentity, _activeWorldIdentity, StringComparison.Ordinal))
            {
                BeginSession(controllerId, levelIdentity, worldIdentity, BuildCurrentLevelKey());
            }

            RefreshCheckpointMap(controller);
            UpdatePassedCheckpoints(controller.currentSeqID);

            if (Main.IsGamePlaying())
            {
                double delta = Time.unscaledDeltaTime;
                if (delta > 0.0 && !double.IsNaN(delta) && !double.IsInfinity(delta))
                {
                    _currentLevelPlaytime += delta;
                    _dirty = true;
                }
            }

            if (Time.unscaledTime >= _nextAttemptsRefreshTime)
            {
                _currentAttempts = ReadCurrentLevelAttempts();
                _nextAttemptsRefreshTime = Time.unscaledTime + 0.5f;
            }

            if (_dirty && Time.unscaledTime >= _nextSaveTime)
            {
                Save();
            }
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            EndSession();
            Save();
            Instance = null;
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        internal static void RecordFps(float fps)
        {
            OvLevelStatsTracker tracker = Instance;
            if (tracker == null || string.IsNullOrEmpty(tracker._activeLevelKey)) return;
            if (!Main.IsGamePlaying()) return;
            if (fps <= 0f || float.IsNaN(fps) || float.IsInfinity(fps)) return;

            if (fps < tracker._minFps) tracker._minFps = fps;
            if (fps > tracker._maxFps) tracker._maxFps = fps;
        }

        internal static int GetCurrentLevelAttempts()
        {
            return Instance != null ? Instance._currentAttempts : ReadCurrentLevelAttempts();
        }

        private static int ReadCurrentLevelAttempts()
        {
            try
            {
                if (ADOBase.isOfficialLevel && !string.IsNullOrEmpty(scrController.currentWorldString))
                {
                    if (WorldData.dict != null
                        && WorldData.dict.TryGetValue(scrController.currentWorldString, out WorldData world)
                        && world != null)
                    {
                        return Math.Max(0, Persistence.GetWorldAttempts(world.index));
                    }
                }

                scnGame customLevel = scnGame.instance;
                if (customLevel != null && customLevel.levelData != null
                    && !string.IsNullOrEmpty(customLevel.levelData.Hash))
                {
                    return Math.Max(0, Persistence.GetCustomWorldAttempts(customLevel.levelData.Hash));
                }
            }
            catch
            {
                // During scene transitions the game's static level references can be
                // temporarily incomplete. Tags should show 0 rather than log-spam.
            }

            return 0;
        }

        private void BeginSession(int controllerId, string levelIdentity, string worldIdentity, string levelKey)
        {
            EndSession();

            _activeControllerId = controllerId;
            _activeLevelIdentity = levelIdentity ?? string.Empty;
            _activeWorldIdentity = worldIdentity ?? string.Empty;
            _activeLevelKey = string.IsNullOrEmpty(levelKey) ? "unknown" : levelKey;
            if (!_document.LevelPlaytimeSeconds.TryGetValue(_activeLevelKey, out _currentLevelPlaytime)
                || _currentLevelPlaytime < 0.0
                || double.IsNaN(_currentLevelPlaytime)
                || double.IsInfinity(_currentLevelPlaytime))
            {
                _currentLevelPlaytime = 0.0;
            }

            _minFps = float.PositiveInfinity;
            _maxFps = 0f;
            _currentAttempts = ReadCurrentLevelAttempts();
            _nextAttemptsRefreshTime = Time.unscaledTime + 0.5f;
            _checkpointSeqIds.Clear();
            _passedCheckpointSeqIds.Clear();
            _nextCheckpointIndex = 0;
            _checkpointFloorCount = -1;
            _checkpointScanReady = false;
            _nextSaveTime = Time.unscaledTime + SaveIntervalSeconds;
        }

        private void EndSession()
        {
            if (string.IsNullOrEmpty(_activeLevelKey)) return;

            _document.LevelPlaytimeSeconds[_activeLevelKey] = Math.Max(0.0, _currentLevelPlaytime);
            _dirty = true;
            Save();

            _activeLevelKey = string.Empty;
            _activeLevelIdentity = string.Empty;
            _activeWorldIdentity = string.Empty;
            _activeControllerId = 0;
            _currentLevelPlaytime = 0.0;
            _currentAttempts = 0;
            _minFps = float.PositiveInfinity;
            _maxFps = 0f;
            _checkpointSeqIds.Clear();
            _passedCheckpointSeqIds.Clear();
            _nextCheckpointIndex = 0;
            _checkpointFloorCount = -1;
            _checkpointScanReady = false;
        }

        private void RefreshCheckpointMap(scrController controller)
        {
            if (scrLevelMaker.instance == null || scrLevelMaker.instance.listFloors == null) return;

            List<scrFloor> floors = scrLevelMaker.instance.listFloors;
            if (_checkpointScanReady && floors.Count == _checkpointFloorCount) return;
            if (!controller.setupComplete) return;

            _checkpointSeqIds.Clear();
            HashSet<int> unique = new HashSet<int>();
            for (int i = 0; i < floors.Count; i++)
            {
                scrFloor floor = floors[i];
                if (floor == null) continue;
                ffxCheckpoint checkpoint = floor.GetComponent<ffxCheckpoint>();
                if (checkpoint == null) continue;

                int seqId = floor.seqID + checkpoint.checkpointTileOffset;
                if (seqId >= 0 && unique.Add(seqId))
                {
                    _checkpointSeqIds.Add(seqId);
                }
            }

            _checkpointSeqIds.Sort();
            _nextCheckpointIndex = 0;
            _checkpointFloorCount = floors.Count;
            _checkpointScanReady = true;
        }

        private void UpdatePassedCheckpoints(int currentSeqId)
        {
            while (_nextCheckpointIndex < _checkpointSeqIds.Count)
            {
                int checkpointSeqId = _checkpointSeqIds[_nextCheckpointIndex];
                if (checkpointSeqId > currentSeqId) break;
                _passedCheckpointSeqIds.Add(checkpointSeqId);
                _nextCheckpointIndex++;
            }
        }

        private static string GetCurrentLevelIdentity(scrController controller)
        {
            try
            {
                scnGame customLevel = scnGame.instance;
                if (!ADOBase.isOfficialLevel && customLevel != null && customLevel.levelData != null
                    && !string.IsNullOrEmpty(customLevel.levelData.Hash))
                {
                    return customLevel.levelData.Hash;
                }
            }
            catch
            {
            }

            return controller != null ? controller.levelName ?? string.Empty : string.Empty;
        }

        private static string GetCurrentWorldIdentity()
        {
            try
            {
                return ADOBase.isOfficialLevel ? scrController.currentWorldString ?? string.Empty : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string BuildCurrentLevelKey()
        {
            try
            {
                if (ADOBase.isOfficialLevel)
                {
                    return "official:" + (scrController.currentWorldString ?? string.Empty)
                        + ":" + (scrController.instance != null ? scrController.instance.levelName ?? string.Empty : string.Empty);
                }

                scnGame customLevel = scnGame.instance;
                if (customLevel != null && customLevel.levelData != null
                    && !string.IsNullOrEmpty(customLevel.levelData.Hash))
                {
                    return "custom:" + customLevel.levelData.Hash;
                }

                if (customLevel != null && !string.IsNullOrEmpty(customLevel.levelPath))
                {
                    return "path:" + customLevel.levelPath;
                }
            }
            catch
            {
            }

            return "scene:" + ADOBase.sceneName;
        }

        private static string StatsPath
        {
            get
            {
                string root = Main.ModEntry != null && !string.IsNullOrEmpty(Main.ModEntry.Path)
                    ? Main.ModEntry.Path
                    : AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(root, StatsFileName);
            }
        }

        private void Load()
        {
            try
            {
                string path = StatsPath;
                if (!File.Exists(path)) return;

                StatsDocument loaded = JsonConvert.DeserializeObject<StatsDocument>(File.ReadAllText(path));
                if (loaded != null)
                {
                    _document = loaded;
                    if (_document.LevelPlaytimeSeconds == null)
                    {
                        _document.LevelPlaytimeSeconds = new Dictionary<string, double>();
                    }
                }
            }
            catch (Exception ex)
            {
                Main.Logger?.Log("[CheryTools] Failed to load OV level stats: " + ex.Message);
                _document = new StatsDocument();
            }
        }

        private void Save()
        {
            if (!_dirty) return;

            try
            {
                if (!string.IsNullOrEmpty(_activeLevelKey))
                {
                    _document.LevelPlaytimeSeconds[_activeLevelKey] = Math.Max(0.0, _currentLevelPlaytime);
                }
                File.WriteAllText(StatsPath, JsonConvert.SerializeObject(_document, Formatting.Indented));
                _dirty = false;
                _nextSaveTime = Time.unscaledTime + SaveIntervalSeconds;
            }
            catch (Exception ex)
            {
                _nextSaveTime = Time.unscaledTime + SaveIntervalSeconds;
                Main.Logger?.Log("[CheryTools] Failed to save OV level stats: " + ex.Message);
            }
        }
    }
}
