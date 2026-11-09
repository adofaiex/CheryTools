using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheryTools.Sonnet.Framework
{
    internal static class SonnetTextureRegistry
    {
        private static readonly Dictionary<IntPtr, Texture> Textures = new Dictionary<IntPtr, Texture>();

        internal static IntPtr Register(Texture texture)
        {
            if (texture == null) return IntPtr.Zero;
            IntPtr id = (IntPtr)texture.GetInstanceID();
            Textures[id] = texture;
            return id;
        }

        internal static Texture Resolve(IntPtr id)
        {
            if (id == IntPtr.Zero) return null;
            Texture texture;
            return Textures.TryGetValue(id, out texture) && texture != null ? texture : null;
        }

        internal static void Unregister(Texture texture)
        {
            if (texture != null) Textures.Remove((IntPtr)texture.GetInstanceID());
        }

        internal static void Clear()
        {
            Textures.Clear();
        }
    }
}
