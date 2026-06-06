using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace CheryTools
{
    public static class TextureManager
    {
        private static Dictionary<string, Texture2D> _loadedTextures = new Dictionary<string, Texture2D>();
        private static Dictionary<IntPtr, Texture2D> _ptrToTexture = new Dictionary<IntPtr, Texture2D>();

        public static IntPtr GetOrCreateTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return IntPtr.Zero;

            if (_loadedTextures.TryGetValue(path, out Texture2D tex))
            {
                if (tex != null)
                    return (IntPtr)tex.GetInstanceID();
            }

            if (!File.Exists(path))
                return IntPtr.Zero;

            try
            {
                byte[] data = File.ReadAllBytes(path);
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                var t = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                if (t != null)
                {
                    var method = t.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
                    if (method != null) method.Invoke(null, new object[] { tex, data });
                }
                tex.filterMode = FilterMode.Bilinear; // Smooth scaling
                
                _loadedTextures[path] = tex;
                
                IntPtr ptr = (IntPtr)tex.GetInstanceID();
                _ptrToTexture[ptr] = tex;
                return ptr;
            }
            catch (Exception e)
            {
                Main.ModEntry.Logger.Log($"[TextureManager] Failed to load image at {path}: {e.Message}");
                return IntPtr.Zero;
            }
        }

        public static Texture2D GetTextureByPtr(IntPtr ptr)
        {
            if (_ptrToTexture.TryGetValue(ptr, out Texture2D tex))
                return tex;
            return null;
        }
        
        public static void Clear()
        {
            foreach (var tex in _loadedTextures.Values)
            {
                if (tex != null) UnityEngine.Object.Destroy(tex);
            }
            _loadedTextures.Clear();
            _ptrToTexture.Clear();
        }
    }
}
