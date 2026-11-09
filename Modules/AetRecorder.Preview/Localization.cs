using System;
using System.Collections.Generic;
using UnityEngine;

namespace ADOFAI.EditorTweaks.ChartRendering
{
    public static class Localization
    {
        private static readonly Dictionary<string, (string en, string zh)> Dictionary = new Dictionary<string, (string en, string zh)>(StringComparer.Ordinal)
        {
            ["title"] = ("ADOFAI Editor Tweaks - Chart Rendering", "ADOFAI 编辑器优化 - 谱面渲染"),
            ["patchFeatureRenderInputGuard"] = ("Render input guard", "渲染输入保护"),
            ["patchFeatureChartRendering"] = ("Chart rendering", "谱面渲染"),
            ["chartRenderSelectedRangeWholeLevel"] = ("Whole level", "整首谱面"),
            ["chartRenderSelectedRangeActive"] = ("Selected range", "选中段落"),
            ["chartRendererGameViewSizeChanged"] = ("The game window size changed during rendering.", "渲染期间游戏窗口尺寸发生变化。"),
            ["chartRendererInvalidGameViewSize"] = ("The current game view size is invalid.", "当前游戏画面尺寸无效。"),
            ["chartRendererSelectedRangeEditorOnly"] = ("Selected range rendering is available in the editor only.", "选中段落渲染只能在编辑器中使用。"),
            ["chartRendererSelectedRangeMissing"] = ("Select at least two continuous tiles first.", "请先框选至少两个连续砖块。"),
            ["chartRendererMuxing"] = ("Muxing audio", "正在合成音频"),
            ["chartRendererRangeStartTimeout"] = ("Timed out while preparing the selected range.", "准备选中段落超时。"),
            ["chartRendererRendering"] = ("Rendering video", "正在渲染视频"),
            ["chartRendererWaitingRangeStart"] = ("Preparing selected range", "正在准备选中段落"),
            ["chartRendererDone"] = ("Render complete", "渲染完成"),
            ["chartRendererFailed"] = ("Render failed", "渲染失败"),
            ["chartRendererMissingFfmpeg"] = ("FFmpeg is missing.", "缺少 FFmpeg。"),
            ["chartRendererIncompatible"] = ("Chart rendering is unavailable for this game version.", "当前游戏版本不可用谱面渲染。")
        };

        public static string Text(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            bool isZh = IsChineseLanguage();
            if (Dictionary.TryGetValue(key, out var pair))
            {
                return isZh ? pair.zh : pair.en;
            }
            return key;
        }

        // Follows the CheryTools UI language chosen in the Framework (follow game / zh / en).
        private static bool IsChineseLanguage() => CheryTools.Sonnet.Contracts.SonnetLanguage.IsChinese;
    }
}
