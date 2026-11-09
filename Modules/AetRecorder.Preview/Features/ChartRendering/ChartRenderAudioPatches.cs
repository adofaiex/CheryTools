using HarmonyLib;
using UnityEngine;

namespace ADOFAI.EditorTweaks.ChartRendering.Features.ChartRendering
{
    [HarmonyPatch(typeof(scrSfx), nameof(scrSfx.PlaySfx), typeof(AudioClip), typeof(MixerGroup), typeof(float), typeof(float), typeof(float))]
    internal static class ChartRenderAudioPatches
    {
        private static bool Prefix(AudioClip clip, MixerGroup group, ref AudioClip __result)
        {
            if (!ChartRenderSession.IsRendering || group != MixerGroup.InterfaceParent)
            {
                return true;
            }

            __result = clip;
            return false;
        }
    }

    [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.PlayScheduled), typeof(double))]
    internal static class ChartRenderPlayScheduledPatch
    {
        private static bool Prefix(AudioSource __instance, double time)
        {
            if (ChartRenderSession.IsRendering && ADOBase.conductor != null)
            {
                if (__instance == ADOBase.conductor.song 
                    || __instance == ADOBase.conductor.song2 
                    || __instance == ADOBase.conductor.song3)
                {
                    ChartRenderDiagnostics.Log("Suppressed AudioSource.PlayScheduled on conductor.song during render (scheduledTime=" 
                        + time + "). Driven by ChartRenderSongSync instead.");
                    return false;
                }
            }

            return true;
        }
    }
}

