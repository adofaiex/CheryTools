using System;
using CheryTools.Sonnet.Contracts;
using UnityEngine;

namespace CheryTools.Sonnet.Framework
{
    internal sealed class FrameworkVisualContext : IModuleVisualContext
    {
        public bool TryGetPanelBackground(out ModuleBackgroundInfo background)
        {
            return SonnetPanelBackground.TryGetModuleBackground(SonnetMain.Settings, out background);
        }

        public IntPtr RegisterTexture(Texture texture) => SonnetTextureRegistry.Register(texture);

        public void UnregisterTexture(Texture texture) => SonnetTextureRegistry.Unregister(texture);
    }
}
