using System;
using System.Reflection;
using HarmonyLib;

namespace ADOFAI.EditorTweaks.ChartRendering.Features.ChartRendering
{
    [HarmonyPatch]
    internal static class ChartRenderJudgmentPatches
    {
        // game-assemblies-2026.09.06.2: ShowHitText(HitMargin, scrPlanet, float)
        // game-assemblies-2026.09.27+: ShowHitText(HitMargin, scrPlanet, float, int? msDiffNullable)
        private static MethodBase TargetMethod()
        {
            MethodInfo? withMsDiff = AccessTools.Method(
                typeof(scrHitTextManager),
                nameof(scrHitTextManager.ShowHitText),
                new[] { typeof(HitMargin), typeof(scrPlanet), typeof(float), typeof(int?) });
            if (withMsDiff != null)
            {
                return withMsDiff;
            }

            MethodInfo? legacy = AccessTools.Method(
                typeof(scrHitTextManager),
                nameof(scrHitTextManager.ShowHitText),
                new[] { typeof(HitMargin), typeof(scrPlanet), typeof(float) });
            if (legacy == null)
            {
                throw new MissingMethodException(
                    "scrHitTextManager.ShowHitText(HitMargin, scrPlanet, float[, int?]) was not found.");
            }

            return legacy;
        }

        private static bool Prefix()
        {
            return !ChartRenderSession.IsRendering || ChartRenderService.ShowHitJudgments;
        }
    }
}
