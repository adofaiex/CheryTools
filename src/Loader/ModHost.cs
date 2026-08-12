using System;

namespace CheryTools
{
    public interface IModLogger
    {
        void Log(string message);
        void Warning(string message);
        void Error(string message);
    }

    public sealed class ModHostInfo
    {
        public string Id { get; }
        public string Version { get; }

        public ModHostInfo(string id, string version)
        {
            Id = string.IsNullOrWhiteSpace(id) ? BuildInfo.ModId : id;
            Version = string.IsNullOrWhiteSpace(version) ? BuildInfo.DisplayVersion : version;
        }
    }

    public interface IModHost
    {
        string LoaderName { get; }
        string Path { get; }
        IModLogger Logger { get; }
        ModHostInfo Info { get; }
    }

    public sealed class DelegateModLogger : IModLogger
    {
        private readonly Action<string> _log;
        private readonly Action<string> _warning;
        private readonly Action<string> _error;

        public DelegateModLogger(Action<string> log, Action<string> warning, Action<string> error)
        {
            _log = log;
            _warning = warning ?? log;
            _error = error ?? log;
        }

        public void Log(string message) => _log?.Invoke(message ?? string.Empty);
        public void Warning(string message) => _warning?.Invoke(message ?? string.Empty);
        public void Error(string message) => _error?.Invoke(message ?? string.Empty);
    }

    public sealed class BasicModHost : IModHost
    {
        public string LoaderName { get; }
        public string Path { get; }
        public IModLogger Logger { get; }
        public ModHostInfo Info { get; }

        public BasicModHost(string loaderName, string path, IModLogger logger, string id, string version)
        {
            LoaderName = string.IsNullOrWhiteSpace(loaderName) ? "Unknown" : loaderName;
            Path = path ?? string.Empty;
            Logger = logger;
            Info = new ModHostInfo(id, version);
        }
    }

    public static class BuildInfo
    {
        public const string ModId = "CheryTools";
        public const string DisplayName = "Chery Tools";
        public const string DisplayVersion = "26.4 Alpha";
        public const string AssemblyVersion = "26.4.0";
        public const string Author = "Chery";
    }
}
