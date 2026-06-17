using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace CheryTools
{
    internal static class VideoTextureManager
    {
        private sealed class VideoEntry
        {
            public GameObject GameObject;
            public VideoPlayer Player;
            public RenderTexture Texture;
            public string RawPath;
            public string Path;
            public bool Loop;
            public int Width;
            public int Height;
            public int LastFrame;
            public string Owner;
            public bool ShouldPlay;
            public long LastObservedFrame = long.MinValue;
            public double LastObservedTime = -1d;
            public float LastHealthCheckTime = -100f;
            public float StallStartTime = -1f;
            public float LastPrepareRequestTime = -100f;
        }

        private const float HealthCheckInterval = 0.5f;
        private const float StallRestartDelay = 1.5f;
        private const float PrepareRetryInterval = 0.5f;
        private static readonly Dictionary<string, VideoEntry> _entries = new Dictionary<string, VideoEntry>();
        private static readonly Dictionary<string, int> _ownerFrameIds = new Dictionary<string, int>();
        private static GameObject _root;

        public static bool HasEntries
        {
            get { return _entries.Count > 0; }
        }

        public static void BeginFrame()
        {
            BeginFrame("Default");
        }

        public static void BeginFrame(string owner)
        {
            owner = NormalizeOwner(owner);
            _ownerFrameIds.TryGetValue(owner, out int frameId);
            _ownerFrameIds[owner] = frameId + 1;
        }

        public static Texture GetOrCreateVideoTexture(string id, string path, bool loop, int widthHint, int heightHint, bool shouldPlay)
        {
            return GetOrCreateVideoTexture("Default", id, path, loop, widthHint, heightHint, shouldPlay);
        }

        public static Texture GetOrCreateVideoTexture(string owner, string id, string path, bool loop, int widthHint, int heightHint, bool shouldPlay)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(path))
            {
                return null;
            }
            if (string.IsNullOrEmpty(owner))
            {
                owner = "Default";
            }
            owner = NormalizeOwner(owner);

            widthHint = Mathf.Clamp(widthHint, 16, 4096);
            heightHint = Mathf.Clamp(heightHint, 16, 4096);

            string key = owner + ":" + id;
            if (_entries.TryGetValue(key, out VideoEntry existing)
                && existing != null
                && string.Equals(existing.RawPath, path, StringComparison.Ordinal)
                && existing.Loop == loop
                && existing.Width == widthHint
                && existing.Height == heightHint
                && existing.Texture != null
                && existing.Player != null)
            {
                existing.LastFrame = GetOwnerFrame(owner);
                existing.ShouldPlay = shouldPlay;
                if (existing.Player.isLooping != loop)
                {
                    existing.Player.isLooping = loop;
                }
                ApplyPlaybackState(existing, shouldPlay);
                return existing.Texture;
            }

            string resolvedPath = CheryToolsAssets.ResolveAssetPath(path);
            if (!IsSupportedVideoPath(resolvedPath) || !File.Exists(resolvedPath))
            {
                return null;
            }

            VideoEntry entry = GetOrCreateEntry(key, owner, resolvedPath, loop, widthHint, heightHint);
            if (entry == null)
            {
                return null;
            }

            entry.RawPath = path;
            entry.LastFrame = GetOwnerFrame(owner);
            entry.ShouldPlay = shouldPlay;
            if (entry.Player != null)
            {
                if (entry.Player.isLooping != loop)
                {
                    entry.Player.isLooping = loop;
                }
                ApplyPlaybackState(entry, shouldPlay);
            }

            return entry.Texture;
        }

        public static void EndFrame()
        {
            EndFrame(null);
        }

        public static void EndFrame(string owner)
        {
            if (_entries.Count == 0) return;

            foreach (var pair in _entries)
            {
                VideoEntry entry = pair.Value;
                if (entry == null || entry.Player == null) continue;
                if (!string.IsNullOrEmpty(owner) && !string.Equals(entry.Owner, owner, StringComparison.Ordinal)) continue;
                ApplyPlaybackState(entry, entry.LastFrame == GetOwnerFrame(entry.Owner) && entry.ShouldPlay);
            }
        }

        public static void RefreshExpectedPlayback()
        {
            RefreshExpectedPlayback(null);
        }

        public static void RefreshExpectedPlayback(string owner)
        {
            if (_entries.Count == 0) return;

            foreach (var pair in _entries)
            {
                VideoEntry entry = pair.Value;
                if (entry == null || entry.Player == null) continue;
                if (!string.IsNullOrEmpty(owner) && !string.Equals(entry.Owner, owner, StringComparison.Ordinal)) continue;
                ApplyPlaybackState(entry, entry.LastFrame == GetOwnerFrame(entry.Owner) && entry.ShouldPlay);
            }
        }

        public static void PauseAll()
        {
            PauseAll(null);
        }

        public static void PauseAll(string owner)
        {
            if (_entries.Count == 0) return;

            foreach (var pair in _entries)
            {
                VideoEntry entry = pair.Value;
                if (!string.IsNullOrEmpty(owner) && !string.Equals(entry.Owner, owner, StringComparison.Ordinal)) continue;
                if (entry != null) entry.ShouldPlay = false;
                if (entry != null && entry.Player != null && entry.Player.isPlaying)
                {
                    entry.Player.Pause();
                }
            }
        }

        public static void Shutdown()
        {
            foreach (var pair in _entries)
            {
                DestroyEntry(pair.Value);
            }
            _entries.Clear();
            _ownerFrameIds.Clear();

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }
        }

        public static bool IsSupportedVideoPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return string.Equals(Path.GetExtension(path), ".mp4", StringComparison.OrdinalIgnoreCase);
        }

        private static VideoEntry GetOrCreateEntry(string id, string owner, string resolvedPath, bool loop, int widthHint, int heightHint)
        {
            if (_entries.TryGetValue(id, out VideoEntry existing) && existing != null)
            {
                if (string.Equals(existing.Path, resolvedPath, StringComparison.OrdinalIgnoreCase)
                    && existing.Loop == loop
                    && existing.Width == widthHint
                    && existing.Height == heightHint
                    && existing.Texture != null
                    && existing.Player != null)
                {
                    return existing;
                }

                DestroyEntry(existing);
                _entries.Remove(id);
            }

            try
            {
                EnsureRoot();
                if (_root == null) return null;

                GameObject go = new GameObject("CheryTools_Video_" + id);
                go.transform.SetParent(_root.transform, false);
                UnityEngine.Object.DontDestroyOnLoad(go);

                RenderTexture texture = new RenderTexture(widthHint, heightHint, 0, RenderTextureFormat.ARGB32);
                texture.name = "CheryToolsVideo_" + id;
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.Create();

                VideoPlayer player = go.AddComponent<VideoPlayer>();
                player.playOnAwake = false;
                player.waitForFirstFrame = true;
                player.skipOnDrop = true;
                player.isLooping = loop;
                player.renderMode = VideoRenderMode.RenderTexture;
                player.aspectRatio = VideoAspectRatio.Stretch;
                player.audioOutputMode = VideoAudioOutputMode.None;
                player.targetTexture = texture;
                player.url = resolvedPath;
                player.Prepare();

                VideoEntry entry = new VideoEntry
                {
                    GameObject = go,
                    Player = player,
                    Texture = texture,
                    RawPath = "",
                    Path = resolvedPath,
                    Loop = loop,
                    Width = widthHint,
                    Height = heightHint,
                    LastFrame = GetOwnerFrame(owner),
                    Owner = owner,
                    ShouldPlay = false
                };
                _entries[id] = entry;
                return entry;
            }
            catch (Exception ex)
            {
                if (Main.Logger != null)
                {
                    Main.Logger.Log("[CheryTools] Failed to create video texture: " + ex.Message);
                }
                return null;
            }
        }

        private static void RequestPrepare(VideoEntry entry, bool force = false)
        {
            if (entry == null || entry.Player == null) return;

            float now = Time.realtimeSinceStartup;
            if (!force && now - entry.LastPrepareRequestTime < PrepareRetryInterval) return;

            try
            {
                entry.Player.Prepare();
                entry.LastPrepareRequestTime = now;
            }
            catch (Exception ex)
            {
                if (Main.Logger != null)
                {
                    Main.Logger.Log("[CheryTools] Failed to prepare video: " + ex.Message);
                }
            }
        }

        private static void TryPlay(VideoEntry entry)
        {
            if (entry == null || entry.Player == null || entry.Player.isPlaying) return;

            try
            {
                if (!entry.Player.isPrepared)
                {
                    RequestPrepare(entry);
                    return;
                }

                entry.Player.Play();
                ResetPlaybackWatch(entry);
            }
            catch (Exception ex)
            {
                if (Main.Logger != null)
                {
                    Main.Logger.Log("[CheryTools] Failed to play video: " + ex.Message);
                }
            }
        }

        private static void ApplyPlaybackState(VideoEntry entry, bool shouldPlay)
        {
            if (entry == null || entry.Player == null) return;

            if (shouldPlay)
            {
                EnsurePlayback(entry);
            }
            else if (entry.Player.isPlaying)
            {
                entry.Player.Pause();
                ResetPlaybackWatch(entry);
            }
        }

        private static void EnsurePlayback(VideoEntry entry)
        {
            if (entry == null || entry.Player == null) return;

            if (!entry.Player.isPrepared)
            {
                RequestPrepare(entry);
                return;
            }

            if (!entry.Player.isPlaying)
            {
                TryPlay(entry);
                return;
            }

            CheckPlaybackProgress(entry);
        }

        private static void CheckPlaybackProgress(VideoEntry entry)
        {
            float now = Time.realtimeSinceStartup;
            if (now - entry.LastHealthCheckTime < HealthCheckInterval) return;
            entry.LastHealthCheckTime = now;

            long frame = -1;
            double videoTime = -1d;
            try
            {
                frame = entry.Player.frame;
                videoTime = entry.Player.time;
            }
            catch
            {
            }

            bool firstObservation = entry.LastObservedFrame == long.MinValue;
            bool frameAdvanced = frame >= 0 && frame != entry.LastObservedFrame;
            bool timeAdvanced = videoTime >= 0d && Math.Abs(videoTime - entry.LastObservedTime) > 0.01d;
            if (firstObservation || frameAdvanced || timeAdvanced)
            {
                entry.LastObservedFrame = frame;
                entry.LastObservedTime = videoTime;
                entry.StallStartTime = -1f;
                return;
            }

            if (entry.StallStartTime < 0f)
            {
                entry.StallStartTime = now;
                return;
            }

            if (now - entry.StallStartTime >= StallRestartDelay)
            {
                RestartStalledPlayback(entry);
            }
        }

        private static void RestartStalledPlayback(VideoEntry entry)
        {
            if (entry == null || entry.Player == null) return;

            try
            {
                entry.Player.Stop();
                entry.Player.targetTexture = entry.Texture;
                ResetPlaybackWatch(entry);
                RequestPrepare(entry, true);
            }
            catch (Exception ex)
            {
                if (Main.Logger != null)
                {
                    Main.Logger.Log("[CheryTools] Failed to restart stalled video: " + ex.Message);
                }
            }
        }

        private static void ResetPlaybackWatch(VideoEntry entry)
        {
            if (entry == null) return;
            entry.LastObservedFrame = long.MinValue;
            entry.LastObservedTime = -1d;
            entry.LastHealthCheckTime = Time.realtimeSinceStartup;
            entry.StallStartTime = -1f;
        }

        private static string NormalizeOwner(string owner)
        {
            return string.IsNullOrEmpty(owner) ? "Default" : owner;
        }

        private static int GetOwnerFrame(string owner)
        {
            owner = NormalizeOwner(owner);
            _ownerFrameIds.TryGetValue(owner, out int frameId);
            return frameId;
        }

        private static void EnsureRoot()
        {
            if (_root != null) return;

            _root = new GameObject("CheryTools_Video_Root");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            _root.hideFlags = HideFlags.HideAndDontSave;
        }

        private static void DestroyEntry(VideoEntry entry)
        {
            if (entry == null) return;

            try
            {
                if (entry.Player != null)
                {
                    entry.Player.Stop();
                    entry.Player.targetTexture = null;
                }
            }
            catch
            {
            }

            if (entry.Texture != null)
            {
                entry.Texture.Release();
                UnityEngine.Object.Destroy(entry.Texture);
            }
            if (entry.GameObject != null)
            {
                UnityEngine.Object.Destroy(entry.GameObject);
            }
        }
    }
}
