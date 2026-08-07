using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace CheryTools
{
    public static class TextureManager
    {
        private static Dictionary<string, Texture2D> _loadedTextures = new Dictionary<string, Texture2D>();
        private static Dictionary<string, Texture2D> _scaledTextures = new Dictionary<string, Texture2D>();
        private static Dictionary<string, Texture2D> _planetSpriteTextures = new Dictionary<string, Texture2D>();
        private static Dictionary<string, Vector2Int> _imageSizes = new Dictionary<string, Vector2Int>();
        private static Dictionary<IntPtr, Texture2D> _ptrToTexture = new Dictionary<IntPtr, Texture2D>();
        private const int TextureSizeBucket = 64;
        private const int PlanetSpriteFrameCount = 11;

        // Negative cache: a missing or corrupt image would otherwise be re-probed from
        // disk (File.Exists / ReadAllBytes + decode) on every frame it stays configured.
        // Failures are remembered per resolved path and only retried after a cooldown,
        // so a file dropped in later is still picked up within a few seconds.
        private static readonly Dictionary<string, float> _failedLoadTimes = new Dictionary<string, float>();
        private const float FailedLoadRetrySeconds = 5f;
        private const int FailedLoadCacheCapacity = 256;

        // Scaled variants are bounded: an image animated across many size buckets would
        // otherwise accumulate one mipmapped copy per bucket for the whole session.
        // Entries untouched for the retention window are destroyed once the cache is
        // over capacity; entries in active use are never evicted.
        private static readonly Dictionary<string, int> _scaledLastAccessFrame = new Dictionary<string, int>();
        private static readonly List<string> _scaledEvictionBuffer = new List<string>();
        private const int MaxScaledTextures = 48;
        private const int ScaledTextureRetentionFrames = 600;

        private static bool IsLoadFailureCached(string resolvedPath)
        {
            if (!_failedLoadTimes.TryGetValue(resolvedPath, out float failedAt)) return false;
            if (Time.realtimeSinceStartup - failedAt < FailedLoadRetrySeconds) return true;
            _failedLoadTimes.Remove(resolvedPath);
            return false;
        }

        private static void RememberLoadFailure(string resolvedPath)
        {
            if (_failedLoadTimes.Count >= FailedLoadCacheCapacity && !_failedLoadTimes.ContainsKey(resolvedPath))
            {
                return;
            }
            _failedLoadTimes[resolvedPath] = Time.realtimeSinceStartup;
        }

        private static void TouchScaledTexture(string cacheKey)
        {
            _scaledLastAccessFrame[cacheKey] = Time.frameCount;
        }

        private static void EvictStaleScaledTextures()
        {
            if (_scaledTextures.Count <= MaxScaledTextures) return;

            int now = Time.frameCount;
            _scaledEvictionBuffer.Clear();
            foreach (var pair in _scaledTextures)
            {
                _scaledLastAccessFrame.TryGetValue(pair.Key, out int lastAccess);
                if (now - lastAccess > ScaledTextureRetentionFrames)
                {
                    _scaledEvictionBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < _scaledEvictionBuffer.Count; i++)
            {
                string key = _scaledEvictionBuffer[i];
                if (_scaledTextures.TryGetValue(key, out Texture2D tex) && tex != null)
                {
                    _ptrToTexture.Remove((IntPtr)tex.GetInstanceID());
                    UnityEngine.Object.Destroy(tex);
                }
                _scaledTextures.Remove(key);
                _scaledLastAccessFrame.Remove(key);
            }
        }

        public static IntPtr GetOrCreateTexture(string path)
        {
            Texture2D tex = GetOrCreateTexture2D(path);
            return tex != null ? (IntPtr)tex.GetInstanceID() : IntPtr.Zero;
        }

        public static Texture2D GetOrCreateTexture2D(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string resolvedPath = CheryToolsAssets.ResolveAssetPath(path);
            if (string.IsNullOrEmpty(resolvedPath)) return null;

            if (_loadedTextures.TryGetValue(resolvedPath, out Texture2D tex))
            {
                if (tex != null)
                    return tex;
            }

            if (IsLoadFailureCached(resolvedPath))
                return null;

            if (!File.Exists(resolvedPath))
            {
                RememberLoadFailure(resolvedPath);
                return null;
            }

            try
            {
                tex = LoadTextureFromFile(resolvedPath);
                if (tex == null)
                {
                    RememberLoadFailure(resolvedPath);
                    return null;
                }

                _failedLoadTimes.Remove(resolvedPath);
                _loadedTextures[resolvedPath] = tex;
                _imageSizes[resolvedPath] = new Vector2Int(tex.width, tex.height);

                RegisterTexture(tex);
                return tex;
            }
            catch (Exception e)
            {
                Main.ModEntry.Logger.Log($"[TextureManager] Failed to load image at {resolvedPath}: {e.Message}");
                RememberLoadFailure(resolvedPath);
                return null;
            }
        }

        public static Texture2D GetOrCreateTexture2D(string path, float displayWidth, float displayHeight)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string resolvedPath = CheryToolsAssets.ResolveAssetPath(path);
            if (string.IsNullOrEmpty(resolvedPath)) return null;

            if (!TryGetImageSize(path, out int originalWidth, out int originalHeight))
            {
                return GetOrCreateTexture2D(path);
            }

            float renderScale = Main.Settings != null ? Main.Settings.ImageRenderScale : 1.0f;
            if (float.IsNaN(renderScale) || float.IsInfinity(renderScale) || renderScale <= 0f)
            {
                renderScale = 1.0f;
            }
            renderScale = Mathf.Clamp(renderScale, 0.25f, 2.0f);

            int targetWidth = BucketTextureSize(Mathf.CeilToInt(Mathf.Abs(displayWidth) * renderScale));
            int targetHeight = BucketTextureSize(Mathf.CeilToInt(Mathf.Abs(displayHeight) * renderScale));
            targetWidth = Mathf.Clamp(targetWidth, 1, originalWidth);
            targetHeight = Mathf.Clamp(targetHeight, 1, originalHeight);

            if (targetWidth >= originalWidth && targetHeight >= originalHeight)
            {
                return GetOrCreateTexture2D(path);
            }

            string cacheKey = resolvedPath + "|" + targetWidth.ToString() + "x" + targetHeight.ToString();
            if (_scaledTextures.TryGetValue(cacheKey, out Texture2D cached) && cached != null)
            {
                TouchScaledTexture(cacheKey);
                return cached;
            }

            try
            {
                // Reuse the managed original texture instead of decoding the same file
                // again for every new display-size bucket.
                Texture2D source = GetOrCreateTexture2D(path);
                if (source == null) return null;

                _imageSizes[resolvedPath] = new Vector2Int(source.width, source.height);
                Texture2D scaled = ResizeTexture(source, targetWidth, targetHeight);
                if (scaled == null) return null;

                scaled.name = Path.GetFileNameWithoutExtension(resolvedPath) + "_" + targetWidth.ToString() + "x" + targetHeight.ToString();
                EvictStaleScaledTextures();
                _scaledTextures[cacheKey] = scaled;
                TouchScaledTexture(cacheKey);
                RegisterTexture(scaled);
                return scaled;
            }
            catch (Exception e)
            {
                Main.ModEntry.Logger.Log($"[TextureManager] Failed to create scaled image at {resolvedPath}: {e.Message}");
                return GetOrCreateTexture2D(path);
            }
        }

        public static Texture2D GetOrCreatePlanetSpriteTexture(string path, Texture referenceTexture)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string resolvedPath = CheryToolsAssets.ResolveAssetPath(path);
            if (string.IsNullOrEmpty(resolvedPath)) return null;

            Texture2D source = GetOrCreateTexture2D(path);
            if (source == null) return null;

            if (IsLikelyPlanetSpriteAtlas(source))
            {
                return source;
            }

            int frameWidth = source.width;
            int atlasHeight = source.height;
            if (referenceTexture != null && referenceTexture.width > 0 && referenceTexture.height > 0)
            {
                frameWidth = Mathf.Max(1, Mathf.RoundToInt(referenceTexture.width / (float)PlanetSpriteFrameCount));
                atlasHeight = Mathf.Max(1, referenceTexture.height);
            }

            int atlasWidth = Mathf.Max(1, frameWidth * PlanetSpriteFrameCount);
            string cacheKey = resolvedPath + "|planet|" + atlasWidth.ToString() + "x" + atlasHeight.ToString();
            if (_planetSpriteTextures.TryGetValue(cacheKey, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            try
            {
                Texture2D atlas = BuildRepeatedPlanetAtlas(source, frameWidth, atlasHeight);
                if (atlas == null) return source;

                atlas.name = Path.GetFileNameWithoutExtension(resolvedPath) + "_PlanetAtlas";
                atlas.filterMode = FilterMode.Bilinear;
                atlas.wrapMode = TextureWrapMode.Clamp;
                _planetSpriteTextures[cacheKey] = atlas;
                RegisterTexture(atlas);
                return atlas;
            }
            catch (Exception e)
            {
                Main.ModEntry.Logger.Log($"[TextureManager] Failed to create planet texture atlas at {resolvedPath}: {e.Message}");
                return source;
            }
        }

        public static bool TryGetImageSize(string path, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (string.IsNullOrEmpty(path)) return false;

            string resolvedPath = CheryToolsAssets.ResolveAssetPath(path);
            if (string.IsNullOrEmpty(resolvedPath)) return false;

            if (_imageSizes.TryGetValue(resolvedPath, out Vector2Int cachedSize))
            {
                width = cachedSize.x;
                height = cachedSize.y;
                return width > 0 && height > 0;
            }

            if (IsLoadFailureCached(resolvedPath)) return false;

            if (!File.Exists(resolvedPath))
            {
                RememberLoadFailure(resolvedPath);
                return false;
            }

            if (TryReadImageHeaderSize(resolvedPath, out width, out height))
            {
                _imageSizes[resolvedPath] = new Vector2Int(width, height);
                return true;
            }

            Texture2D tex = GetOrCreateTexture2D(path);
            if (tex == null) return false;
            width = tex.width;
            height = tex.height;
            _imageSizes[resolvedPath] = new Vector2Int(width, height);
            return true;
        }

        public static Texture2D GetTextureByPtr(IntPtr ptr)
        {
            if (_ptrToTexture.TryGetValue(ptr, out Texture2D tex))
                return tex;
            return null;
        }

        public static void ClearScaledTextures()
        {
            foreach (var tex in _scaledTextures.Values)
            {
                if (tex != null) UnityEngine.Object.Destroy(tex);
            }
            _scaledTextures.Clear();
            _scaledLastAccessFrame.Clear();
            RebuildPtrLookup();
        }
        
        public static void Clear()
        {
            var destroyed = new HashSet<Texture2D>();
            foreach (var tex in _loadedTextures.Values)
            {
                if (tex != null && destroyed.Add(tex)) UnityEngine.Object.Destroy(tex);
            }
            foreach (var tex in _scaledTextures.Values)
            {
                if (tex != null && destroyed.Add(tex)) UnityEngine.Object.Destroy(tex);
            }
            foreach (var tex in _planetSpriteTextures.Values)
            {
                if (tex != null && destroyed.Add(tex)) UnityEngine.Object.Destroy(tex);
            }
            _loadedTextures.Clear();
            _scaledTextures.Clear();
            _scaledLastAccessFrame.Clear();
            _planetSpriteTextures.Clear();
            _imageSizes.Clear();
            _ptrToTexture.Clear();
            _failedLoadTimes.Clear();
        }

        private static Func<Texture2D, byte[], bool> _loadImageDelegate;
        private static bool _loadImageResolved;

        private static Func<Texture2D, byte[], bool> ResolveLoadImage()
        {
            if (_loadImageResolved) return _loadImageDelegate;
            _loadImageResolved = true;

            var t = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            if (t == null) return null;

            var method = t.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
            if (method != null)
            {
                try
                {
                    _loadImageDelegate = (Func<Texture2D, byte[], bool>)Delegate.CreateDelegate(
                        typeof(Func<Texture2D, byte[], bool>), method);
                    return _loadImageDelegate;
                }
                catch { }
            }

            var method3 = t.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
            if (method3 != null)
            {
                try
                {
                    var full = (Func<Texture2D, byte[], bool, bool>)Delegate.CreateDelegate(
                        typeof(Func<Texture2D, byte[], bool, bool>), method3);
                    _loadImageDelegate = (tex, data) => full(tex, data, false);
                    return _loadImageDelegate;
                }
                catch { }
            }

            return null;
        }

        private static Texture2D LoadTextureFromFile(string resolvedPath)
        {
            Func<Texture2D, byte[], bool> loadImage = ResolveLoadImage();
            if (loadImage == null) return null;

            byte[] data = File.ReadAllBytes(resolvedPath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!loadImage(tex, data))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        private static Texture2D ResizeTexture(Texture2D source, int width, int height)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            try
            {
                rt.filterMode = FilterMode.Bilinear;
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, true);
                result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                result.Apply(true, true);
                result.filterMode = FilterMode.Bilinear;
                result.wrapMode = TextureWrapMode.Clamp;
                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static Texture2D BuildRepeatedPlanetAtlas(Texture2D source, int frameWidth, int height)
        {
            frameWidth = Mathf.Max(1, frameWidth);
            height = Mathf.Max(1, height);

            int atlasWidth = frameWidth * PlanetSpriteFrameCount;
            Texture2D atlas = new Texture2D(atlasWidth, height, TextureFormat.RGBA32, false);
            Color[] framePixels = new Color[frameWidth * height];

            float invW = frameWidth > 1 ? 1f / (frameWidth - 1) : 0f;
            float invH = height > 1 ? 1f / (height - 1) : 0f;
            for (int y = 0; y < height; y++)
            {
                float v = height > 1 ? y * invH : 0.5f;
                for (int x = 0; x < frameWidth; x++)
                {
                    float u = frameWidth > 1 ? x * invW : 0.5f;
                    framePixels[y * frameWidth + x] = source.GetPixelBilinear(u, v);
                }
            }

            for (int frame = 0; frame < PlanetSpriteFrameCount; frame++)
            {
                atlas.SetPixels(frame * frameWidth, 0, frameWidth, height, framePixels);
            }

            atlas.Apply(false, true);
            return atlas;
        }

        private static bool IsLikelyPlanetSpriteAtlas(Texture2D texture)
        {
            if (texture == null || texture.height <= 0) return false;

            float atlasAspect = texture.width / (float)texture.height;
            return Mathf.Abs(atlasAspect - PlanetSpriteFrameCount) <= 0.05f;
        }

        private static int BucketTextureSize(int size)
        {
            size = Mathf.Max(1, size);
            return Mathf.Max(TextureSizeBucket, Mathf.CeilToInt(size / (float)TextureSizeBucket) * TextureSizeBucket);
        }

        private static void RegisterTexture(Texture2D tex)
        {
            if (tex == null) return;
            _ptrToTexture[(IntPtr)tex.GetInstanceID()] = tex;
        }

        private static void RebuildPtrLookup()
        {
            _ptrToTexture.Clear();
            foreach (var tex in _loadedTextures.Values)
            {
                RegisterTexture(tex);
            }
            foreach (var tex in _scaledTextures.Values)
            {
                RegisterTexture(tex);
            }
            foreach (var tex in _planetSpriteTextures.Values)
            {
                RegisterTexture(tex);
            }
        }

        private static bool TryReadImageHeaderSize(string path, out int width, out int height)
        {
            width = 0;
            height = 0;
            byte[] header = new byte[32];
            using (FileStream fs = File.OpenRead(path))
            {
                int read = fs.Read(header, 0, header.Length);
                if (read >= 24
                    && header[0] == 0x89
                    && header[1] == 0x50
                    && header[2] == 0x4E
                    && header[3] == 0x47)
                {
                    width = ReadBigEndianInt(header, 16);
                    height = ReadBigEndianInt(header, 20);
                    return width > 0 && height > 0;
                }

                fs.Position = 0;
                if (read >= 2 && header[0] == 0xFF && header[1] == 0xD8)
                {
                    return TryReadJpegSize(fs, out width, out height);
                }
            }
            return false;
        }

        private static bool TryReadJpegSize(FileStream fs, out int width, out int height)
        {
            width = 0;
            height = 0;
            fs.Position = 2;
            while (fs.Position + 9 < fs.Length)
            {
                int markerPrefix = fs.ReadByte();
                if (markerPrefix != 0xFF) continue;

                int marker = fs.ReadByte();
                while (marker == 0xFF) marker = fs.ReadByte();
                if (marker < 0) return false;
                if (marker == 0xD9 || marker == 0xDA) return false;

                int length = ReadBigEndianShort(fs);
                if (length < 2 || fs.Position + length - 2 > fs.Length) return false;

                bool isStartOfFrame = marker >= 0xC0 && marker <= 0xC3;
                if (isStartOfFrame)
                {
                    fs.ReadByte();
                    height = ReadBigEndianShort(fs);
                    width = ReadBigEndianShort(fs);
                    return width > 0 && height > 0;
                }

                fs.Position += length - 2;
            }
            return false;
        }

        private static int ReadBigEndianInt(byte[] data, int offset)
        {
            return (data[offset] << 24)
                | (data[offset + 1] << 16)
                | (data[offset + 2] << 8)
                | data[offset + 3];
        }

        private static int ReadBigEndianShort(FileStream fs)
        {
            int hi = fs.ReadByte();
            int lo = fs.ReadByte();
            if (hi < 0 || lo < 0) return -1;
            return (hi << 8) | lo;
        }
    }
}
