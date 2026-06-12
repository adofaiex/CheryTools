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
        private static Dictionary<string, Vector2Int> _imageSizes = new Dictionary<string, Vector2Int>();
        private static Dictionary<IntPtr, Texture2D> _ptrToTexture = new Dictionary<IntPtr, Texture2D>();
        private const int TextureSizeBucket = 64;

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

            if (!File.Exists(resolvedPath))
                return null;

            try
            {
                tex = LoadTextureFromFile(resolvedPath);
                if (tex == null) return null;
                
                _loadedTextures[resolvedPath] = tex;
                _imageSizes[resolvedPath] = new Vector2Int(tex.width, tex.height);
                
                RegisterTexture(tex);
                return tex;
            }
            catch (Exception e)
            {
                Main.ModEntry.Logger.Log($"[TextureManager] Failed to load image at {resolvedPath}: {e.Message}");
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
                return cached;
            }

            try
            {
                Texture2D source = LoadTextureFromFile(resolvedPath);
                if (source == null) return null;

                _imageSizes[resolvedPath] = new Vector2Int(source.width, source.height);
                Texture2D scaled = ResizeTexture(source, targetWidth, targetHeight);
                UnityEngine.Object.Destroy(source);
                if (scaled == null) return null;

                scaled.name = Path.GetFileNameWithoutExtension(resolvedPath) + "_" + targetWidth.ToString() + "x" + targetHeight.ToString();
                _scaledTextures[cacheKey] = scaled;
                RegisterTexture(scaled);
                return scaled;
            }
            catch (Exception e)
            {
                Main.ModEntry.Logger.Log($"[TextureManager] Failed to create scaled image at {resolvedPath}: {e.Message}");
                return GetOrCreateTexture2D(path);
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

            if (!File.Exists(resolvedPath)) return false;

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
            _loadedTextures.Clear();
            _scaledTextures.Clear();
            _imageSizes.Clear();
            _ptrToTexture.Clear();
        }

        private static Texture2D LoadTextureFromFile(string resolvedPath)
        {
            byte[] data = File.ReadAllBytes(resolvedPath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            bool loaded = false;
            var t = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            if (t != null)
            {
                var method = t.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
                object result = null;
                if (method != null)
                {
                    result = method.Invoke(null, new object[] { tex, data });
                }
                else
                {
                    method = t.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
                    if (method != null)
                    {
                        result = method.Invoke(null, new object[] { tex, data, false });
                    }
                }

                if (method != null)
                {
                    loaded = !(result is bool) || (bool)result;
                }
            }
            if (!loaded)
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
