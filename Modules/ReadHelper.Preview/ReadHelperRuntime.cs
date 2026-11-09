using System;
using System.IO;
using CheryTools.Sonnet.Contracts;
using UnityEngine;

namespace CheryTools.ReadHelper.Preview
{
    public static class ReadHelperRuntime
    {
        public static ModuleContext Context { get; private set; }
        public static ReadHelperSettings Settings { get; private set; }

        private static GameObject _hostObject;
        private static ReadHelperHost _host;
        private static string _settingsPath;

        public static void Initialize(ModuleContext context)
        {
            Context = context;
            _settingsPath = Path.Combine(context.ModuleDirectory, "ReadHelper.xml");
            Settings = ReadHelperSettings.Load(_settingsPath, context.Logger);

            if (context.Ugui?.Root != null)
            {
                _hostObject = new GameObject("CheryTools_ReadHelper_Host");
                _hostObject.transform.SetParent(context.Ugui.Root, false);
                _host = _hostObject.AddComponent<ReadHelperHost>();
                _host.Initialize(context.Ugui.Root, GetFrameworkDir(context.ModuleDirectory));
            }

            context.Logger?.Info("读谱辅助模块初始化完成");
        }

        private static string GetFrameworkDir(string moduleDir)
        {
            try
            {
                // moduleDir 通常是 Mods\CheryTools\Modules\ 或源码目录
                DirectoryInfo parent = Directory.GetParent(moduleDir);
                if (parent != null) return parent.FullName;
            }
            catch { }
            return string.Empty;
        }

        public static void SaveSettings()
        {
            if (Settings != null && !string.IsNullOrEmpty(_settingsPath))
            {
                Settings.Save(_settingsPath, Context?.Logger);
            }
        }

        public static void Shutdown()
        {
            SaveSettings();
            if (_hostObject != null)
            {
                UnityEngine.Object.Destroy(_hostObject);
                _hostObject = null;
                _host = null;
            }
            Context = null;
        }
    }
}
