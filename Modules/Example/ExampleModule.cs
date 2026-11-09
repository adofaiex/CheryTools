using System.Collections.Generic;
using System.Numerics;
using CheryTools.Sonnet.Contracts;
using ImGuiNET;

namespace CheryTools.Sonnet.Example
{
    public sealed class ExampleModule : ICheryModule
    {
        private IModuleLogger _logger;
        private int _clicks;
        private bool _enabled = true;
        private static readonly IReadOnlyList<ModuleTab> _tabs = new List<ModuleTab>
        {
            new ModuleTab("feature1", "示例功能 1"),
            new ModuleTab("feature2", "示例功能 2"),
            new ModuleTab("feature3", "示例功能 3")
        };

        public ModuleMetadata Metadata { get; } = new ModuleMetadata(
            "example",
            "示例模块",
            "1.0.0",
            "工具",
            new Vector4(0.36f, 0.78f, 1f, 1f))
        {
            NavigationTitle = "配置列表"
        };

        public IReadOnlyList<ModuleTab> Tabs => _tabs;

        public void Initialize(ModuleContext context)
        {
            _logger = context.Logger;
            context.Ugui.CreateObject("ExampleRuntimeRoot");
            _logger.Info("示例模块已初始化");
        }

        public void Update()
        {
        }

        public void Draw(ModuleUiContext context)
        {
            ImGui.TextWrapped("这是一个独立 DLL 模块。当前页面由 Sonnet Framework 承载，模块只负责自己的内容。");
            ImGui.Separator();
            if (context.ActiveTabId == "feature1")
            {
                ImGui.TextColored(new Vector4(0.36f, 0.78f, 1f, 1f), "示例功能 1");
                ImGui.TextDisabled("用于验证基础模块页面注册。");
            }
            else if (context.ActiveTabId == "feature2")
            {
                ImGui.TextColored(new Vector4(0.48f, 0.90f, 0.68f, 1f), "示例功能 2");
                if (ImGui.Button("点击测试")) _clicks++;
                ImGui.SameLine();
                ImGui.TextUnformatted("点击次数：" + _clicks);
            }
            else
            {
                ImGui.TextColored(new Vector4(1f, 0.72f, 0.40f, 1f), "示例功能 3");
                if (ImGui.Checkbox("模块内部开关", ref _enabled)) _logger.Info("示例模块开关：" + _enabled);
                ImGui.TextDisabled("UGUI 根节点已由框架分配，并与模块生命周期绑定。");
            }
        }

        public void Shutdown()
        {
            _logger?.Info("示例模块已关闭");
        }
    }
}
