using ADOFAI.EditorTweaks.Api.Rendering;

namespace ADOFAI.EditorTweaks.ChartRendering.Features.ChartRendering
{
    internal sealed class ChartRenderResult
    {
        public bool Success;

        public string Message = string.Empty;

        public string OutputPath = string.Empty;

        public bool Canceled;

        public ChartRenderErrorCode ErrorCode;
    }
}
