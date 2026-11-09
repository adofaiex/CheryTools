using System;
using System.Collections.Generic;
using ADOFAI.EditorTweaks.Api.Rendering;
using ADOFAI.EditorTweaks.ChartRendering.Features.ChartRendering;
using CheryTools.AetRecorder.Preview.UI;
using CheryTools.Sonnet.Contracts;
using UnityEngine;
using NumVector4 = System.Numerics.Vector4;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace CheryTools.AetRecorder.Preview
{
    public sealed class AetRecorderPreviewModule : ICheryModule
    {
        private static readonly IReadOnlyList<ModuleTab> ModuleTabs = new List<ModuleTab>
        {
            new ModuleTab("render", "录制控制", "Render"),
            new ModuleTab("settings", "画质与参数", "Settings"),
            new ModuleTab("diagnostics", "诊断与状态", "Diagnostics")
        };

        public ModuleMetadata Metadata { get; } = new ModuleMetadata(
            "aet.recorder",
            "AET Recorder",
            "1.0.0",
            "创作工具",
            new NumVector4(0.20f, 0.85f, 0.75f, 1f))
        {
            NavigationTitle = "谱面录制",
            NavigationTitleEn = "Chart Rendering"
        };

        public IReadOnlyList<ModuleTab> Tabs => ModuleTabs;

        public void Initialize(ModuleContext context)
        {
            AetRecorderRuntime.Initialize(context);
        }

        public void Update()
        {
            AetRecorderRuntime.Tick();

            if (ChartRenderService.IsActive && Input.GetKeyDown(KeyCode.Escape))
            {
                try
                {
                    ChartRenderApi.CurrentTask?.Cancel();
                    AetRecorderRuntime.Logger?.Info(L("[AetRecorder] 录制已被 Escape 键中止。", "[AetRecorder] Render canceled with Escape."));
                }
                catch
                {
                }
            }
        }

        public void Draw(ModuleUiContext context)
        {
            AetRecorderUi.Draw(context.ActiveTabId);
        }

        public void Shutdown()
        {
            AetRecorderRuntime.Shutdown();
        }
    }
}
