using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CheryTools.Sonnet.Contracts;
using UnityEngine;
using UnityEngine.UI;
using NumVector2 = System.Numerics.Vector2;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.Sonnet.Framework
{
    internal sealed class LoadedModule
    {
        internal ICheryModule Instance;
        internal ModuleMetadata Metadata;
        internal string AssemblyPath;
        internal FrameworkUguiContext Ugui;
        internal bool Failed;
    }

    internal sealed class FrameworkUguiContext : IUguiModuleContext
    {
        private readonly GameObject _root;
        public Canvas Canvas { get; }
        public RectTransform Root { get; }

        internal FrameworkUguiContext(Canvas canvas, Transform parent, string moduleId)
        {
            Canvas = canvas;
            _root = new GameObject("Sonnet_Module_" + moduleId, typeof(RectTransform));
            _root.transform.SetParent(parent, false);
            Root = _root.GetComponent<RectTransform>();
            Root.anchorMin = Vector2.zero;
            Root.anchorMax = Vector2.one;
            Root.offsetMin = Vector2.zero;
            Root.offsetMax = Vector2.zero;
            _root.SetActive(true);
        }

        public GameObject CreateObject(string name)
        {
            var child = new GameObject(string.IsNullOrWhiteSpace(name) ? "ModuleObject" : name, typeof(RectTransform));
            child.transform.SetParent(Root, false);
            return child;
        }

        public Text CreateText(string name, string text)
        {
            var label = CreateObject(name).AddComponent<Text>();
            label.text = text ?? string.Empty;
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = 20;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return label;
        }

        public void Clear()
        {
            for (int i = Root.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(Root.GetChild(i).gameObject);
        }

        internal void SetActive(bool active) => _root.SetActive(active);
        internal void Destroy() => UnityEngine.Object.Destroy(_root);
    }

    internal sealed class SonnetModuleLoader
    {
        private readonly List<LoadedModule> _modules = new List<LoadedModule>();
        private readonly List<LoadedModule> _displayModules = new List<LoadedModule>();
        private readonly string _modulesDirectory;
        private readonly IModuleLogger _logger;
        private readonly Canvas _canvas;
        private readonly Transform _uguiParent;

        internal IReadOnlyList<LoadedModule> Modules => _modules;
        internal IReadOnlyList<LoadedModule> DisplayModules => _displayModules;

        // Module runtime UI (KeyViewer, Overlayer, ...) lives on its own root canvas, just below
        // the ImGui canvas, so a recorder can redirect it into a render texture without also
        // capturing the ImGui panel.
        internal const string ModuleCanvasName = "CheryTools_Sonnet_ModuleCanvas";

        internal SonnetModuleLoader(string frameworkDirectory, Canvas imGuiCanvas, IModuleLogger logger)
        {
            _modulesDirectory = Path.Combine(frameworkDirectory, "Modules");
            _logger = logger;
            var canvasObject = new GameObject(ModuleCanvasName);
            UnityEngine.Object.DontDestroyOnLoad(canvasObject);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _normalSortingOrder = imGuiCanvas.sortingOrder - 2;
            _canvas.sortingOrder = _normalSortingOrder;
            canvasObject.AddComponent<GraphicRaycaster>();
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            var root = new GameObject("CheryTools_Sonnet_Modules", typeof(RectTransform));
            root.transform.SetParent(_canvas.transform, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            _uguiParent = root.transform;
        }

        internal void LoadAll()
        {
            Directory.CreateDirectory(_modulesDirectory);
            foreach (string path in Directory.GetFiles(_modulesDirectory, "*.dll", SearchOption.TopDirectoryOnly).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                string fileName = Path.GetFileName(path);
                if (string.Equals(fileName, "CheryTools.Sonnet.Contracts.dll", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, "CheryTools.Sonnet.Example.dll", StringComparison.OrdinalIgnoreCase)) continue;
                LoadAssembly(path);
            }
            RebuildDisplayOrder(null);
        }

        internal void ApplyDisplayOrder(IList<string> savedOrder)
        {
            RebuildDisplayOrder(savedOrder);
        }

        internal List<string> GetDisplayOrder()
        {
            return _displayModules.Select(module => module.Metadata.Id).ToList();
        }

        internal bool ReorderDisplay(string moduleId, int targetIndex)
        {
            if (string.IsNullOrEmpty(moduleId)) return false;
            int currentIndex = _displayModules.FindIndex(module =>
                string.Equals(module.Metadata.Id, moduleId, StringComparison.Ordinal));
            if (currentIndex < 0) return false;

            targetIndex = Math.Max(0, Math.Min(_displayModules.Count, targetIndex));
            LoadedModule moving = _displayModules[currentIndex];
            _displayModules.RemoveAt(currentIndex);
            if (targetIndex > currentIndex) targetIndex--;
            targetIndex = Math.Max(0, Math.Min(_displayModules.Count, targetIndex));
            _displayModules.Insert(targetIndex, moving);
            return currentIndex != targetIndex;
        }

        private void RebuildDisplayOrder(IList<string> savedOrder)
        {
            _displayModules.Clear();
            var used = new HashSet<string>(StringComparer.Ordinal);
            if (savedOrder != null)
            {
                for (int i = 0; i < savedOrder.Count; i++)
                {
                    string id = savedOrder[i];
                    if (string.IsNullOrEmpty(id) || !used.Add(id)) continue;
                    LoadedModule module = _modules.FirstOrDefault(item =>
                        string.Equals(item.Metadata.Id, id, StringComparison.Ordinal));
                    if (module != null) _displayModules.Add(module);
                }
            }
            for (int i = 0; i < _modules.Count; i++)
            {
                LoadedModule module = _modules[i];
                if (used.Add(module.Metadata.Id)) _displayModules.Add(module);
            }
        }

        private void LoadAssembly(string path)
        {
            try
            {
                Assembly assembly = Assembly.LoadFrom(path);
                Type[] types = GetLoadableTypes(assembly, path);
                foreach (Type type in types)
                {
                    if (type.IsAbstract || !typeof(ICheryModule).IsAssignableFrom(type)) continue;
                    var module = (ICheryModule)Activator.CreateInstance(type);
                    if (module.Metadata == null || _modules.Any(item => item.Metadata.Id == module.Metadata.Id))
                    {
                        _logger.Warning(L("跳过无效或重复模块：", "Skipped invalid or duplicate module: ") + path);
                        continue;
                    }
                    if (module.Metadata.ApiVersion != ModuleApi.CurrentVersion)
                    {
                        _logger.Warning(L("模块 API 版本不兼容：", "Incompatible module API version: ") + module.Metadata.DisplayName + L("，需要 ", ", requires ") + module.Metadata.ApiVersion + L("，框架提供 ", ", framework provides ") + ModuleApi.CurrentVersion);
                        continue;
                    }

                    var loaded = new LoadedModule
                    {
                        Instance = module,
                        Metadata = module.Metadata,
                        AssemblyPath = path,
                        Ugui = new FrameworkUguiContext(_canvas, _uguiParent, module.Metadata.Id)
                    };
                    try
                    {
                        module.Initialize(new ModuleContext(module.Metadata, Path.GetDirectoryName(path), _logger, loaded.Ugui,
                            new FrameworkVisualContext()));
                        _modules.Add(loaded);
                        _logger.Info(L("已加载模块：", "Loaded module: ") + module.Metadata.DisplayName + " " + module.Metadata.Version);
                    }
                    catch (Exception ex)
                    {
                        loaded.Ugui.Destroy();
                        _logger.Error(L("初始化模块失败 ", "Failed to initialize module ") + module.Metadata.Id + L("：", ": ") + ex.GetBaseException().Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(L("加载模块失败 ", "Failed to load module ") + Path.GetFileName(path) + L("：", ": ") + ex.GetBaseException().Message);
            }
        }

        private Type[] GetLoadableTypes(Assembly assembly, string path)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                _logger.Warning(L("模块程序集有部分类型无法加载，将继续扫描可用类型：", "Some types in the module assembly could not be loaded; scanning the rest: ") + Path.GetFileName(path));
                return ex.Types.Where(type => type != null).ToArray();
            }
        }

        // Below the level editor's own canvas (sorting order 1) while editing, so KeyViewer and
        // Overlayer never cover the editor panels; back on top as soon as the chart plays.
        private const int EditModeSortingOrder = 0;
        private const int EditorPollFrames = 8;
        private int _normalSortingOrder;
        private bool _editorResolved;
        private System.Reflection.FieldInfo _editorInstance;
        private System.Reflection.MethodInfo _editorPlayMode;

        private void UpdateCanvasOrder()
        {
            if (Time.frameCount % EditorPollFrames != 0) return;
            int target = IsEditingInLevelEditor() ? EditModeSortingOrder : _normalSortingOrder;
            if (_canvas.sortingOrder != target) _canvas.sortingOrder = target;
        }

        private bool IsEditingInLevelEditor()
        {
            if (!_editorResolved)
            {
                _editorResolved = true;
                Type editor = Type.GetType("scnEditor, Assembly-CSharp", false);
                _editorInstance = editor?.GetField("instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                _editorPlayMode = editor?.GetProperty("playMode")?.GetGetMethod();
            }
            if (_editorInstance == null || _editorPlayMode == null) return false;
            UnityEngine.Object instance = _editorInstance.GetValue(null) as UnityEngine.Object;
            return instance != null && !(bool)_editorPlayMode.Invoke(instance, null);
        }

        internal void Update()
        {
            UpdateCanvasOrder();
            for (int i = 0; i < _modules.Count; i++)
            {
                LoadedModule module = _modules[i];
                if (module.Failed) continue;
                try { module.Instance.Update(); }
                catch (Exception ex)
                {
                    module.Failed = true;
                    module.Ugui.SetActive(false);
                    _logger.Error(L("模块 Update 失败 ", "Module Update failed ") + module.Metadata.Id + L("：", ": ") + ex.GetBaseException().Message);
                }
            }
        }

        internal void DrawCurrent(string moduleId, string tabId, NumVector2 contentSize)
        {
            LoadedModule module = _modules.FirstOrDefault(item => item.Metadata.Id == moduleId);
            if (module == null || module.Failed) return;
            string activeTab = tabId;
            if (string.IsNullOrEmpty(activeTab) && module.Instance.Tabs != null && module.Instance.Tabs.Count > 0)
                activeTab = module.Instance.Tabs[0].Id;
            try
            {
                module.Instance.Draw(new ModuleUiContext(module.Metadata, activeTab, contentSize));
            }
            catch (Exception ex)
            {
                module.Failed = true;
                module.Ugui.SetActive(false);
                _logger.Error(L("模块 UI 失败 ", "Module UI failed ") + module.Metadata.Id + L("：", ": ") + ex.GetBaseException().Message);
            }
        }

        internal bool HasOpenOverlays
        {
            get
            {
                foreach (LoadedModule module in _modules)
                {
                    if (module.Failed) continue;
                    var overlay = module.Instance as ICheryModuleOverlay;
                    try { if (overlay != null && overlay.HasOpenOverlay) return true; }
                    catch (Exception ex)
                    {
                        module.Failed = true;
                        module.Ugui.SetActive(false);
                        _logger.Error(L("模块浮层状态读取失败 ", "Failed to read module overlay state ") + module.Metadata.Id + L("：", ": ") + ex.GetBaseException().Message);
                    }
                }
                return false;
            }
        }

        internal void DrawOverlays()
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                LoadedModule module = _modules[i];
                if (module.Failed) continue;
                var overlay = module.Instance as ICheryModuleOverlay;
                if (overlay == null) continue;
                try
                {
                    if (overlay.HasOpenOverlay) overlay.DrawOverlay();
                }
                catch (Exception ex)
                {
                    module.Failed = true;
                    module.Ugui.SetActive(false);
                    _logger.Error(L("模块浮层 UI 失败 ", "Module overlay UI failed ") + module.Metadata.Id + L("：", ": ") + ex.GetBaseException().Message);
                }
            }
        }

        internal LoadedModule Find(string id) => _modules.FirstOrDefault(item => item.Metadata.Id == id);

        internal void SetRuntimeActive(bool active)
        {
            foreach (LoadedModule module in _modules)
                module.Ugui.SetActive(active && !module.Failed);
        }

        internal void Shutdown()
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                LoadedModule module = _modules[i];
                try { module.Instance.Shutdown(); }
                catch (Exception ex) { _logger.Warning(L("模块关闭失败 ", "Module shutdown failed ") + module.Metadata.Id + L("：", ": ") + ex.GetBaseException().Message); }
                module.Ugui.Destroy();
            }
            _modules.Clear();
            if (_canvas != null) UnityEngine.Object.Destroy(_canvas.gameObject);
        }
    }
}
