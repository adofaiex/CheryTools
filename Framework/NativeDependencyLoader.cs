using System;
using System.IO;
using System.Runtime.InteropServices;
using CheryTools.Sonnet.Contracts;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.Sonnet.Framework
{
    internal static class NativeDependencyLoader
    {
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string fileName);

        private static IntPtr _cimgui;

        internal static void Load(string directory, IModuleLogger logger)
        {
            if (_cimgui != IntPtr.Zero) return;
            string path = Path.Combine(directory ?? string.Empty, "cimgui.dll");
            if (!File.Exists(path))
            {
                logger?.Warning(L("未找到 cimgui.dll：", "cimgui.dll not found: ") + path);
                return;
            }
            _cimgui = LoadLibrary(path);
            if (_cimgui == IntPtr.Zero)
                logger?.Error(L("加载 cimgui.dll 失败，Win32Error=", "Failed to load cimgui.dll, Win32Error=") + Marshal.GetLastWin32Error());
        }
    }
}
