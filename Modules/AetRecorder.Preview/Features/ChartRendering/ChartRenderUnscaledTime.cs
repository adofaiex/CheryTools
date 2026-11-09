using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ADOFAI.EditorTweaks.ChartRendering.Features.ChartRendering
{
    // Renders set Time.captureFramerate, which steps Time.time by a fixed amount per frame, but
    // Time.unscaledTime keeps following the wall clock. Game visuals timed with it ran fast in
    // rendered videos whenever a frame took longer than 1/fps to render: the planet surface spin
    // (PlanetSprite), rainbow planets and the miss flash (PlanetRenderer), hitstop and camera
    // kickback, and text decoration wave/shake (LetterMovement). These methods read the clock below
    // instead. Outside a capture it returns Unity's own values, so normal play is unchanged.
    // Conductor, input and analytics timing are deliberately left on the real clock.
    internal static class ChartRenderUnscaledTime
    {
        private static int lastFrame = -1;
        private static double now;

        public static float UnscaledTime
        {
            get
            {
                float capture = Time.captureDeltaTime;
                if (capture <= 0f)
                {
                    lastFrame = -1;
                    return Time.unscaledTime;
                }

                int frame = Time.frameCount;
                if (lastFrame < 0)
                {
                    now = Time.unscaledTimeAsDouble;
                }
                else if (frame != lastFrame)
                {
                    now += (double)capture * (frame - lastFrame);
                }

                lastFrame = frame;
                return (float)now;
            }
        }

        public static float UnscaledDeltaTime
        {
            get
            {
                float capture = Time.captureDeltaTime;
                return capture > 0f ? capture : Time.unscaledDeltaTime;
            }
        }
    }

    [HarmonyPatch]
    internal static class ChartRenderUnscaledTimePatch
    {
        private static readonly string[] TargetTypes =
        {
            "PlanetSprite",
            "PlanetRenderer",
            "Hitstop",
            "Kickback",
            "LetterMovement"
        };

        private static readonly MethodInfo UnscaledTimeGetter =
            AccessTools.PropertyGetter(typeof(Time), nameof(Time.unscaledTime));

        private static readonly MethodInfo UnscaledDeltaTimeGetter =
            AccessTools.PropertyGetter(typeof(Time), nameof(Time.unscaledDeltaTime));

        private static readonly MethodInfo CaptureUnscaledTime =
            AccessTools.PropertyGetter(typeof(ChartRenderUnscaledTime), nameof(ChartRenderUnscaledTime.UnscaledTime));

        private static readonly MethodInfo CaptureUnscaledDeltaTime =
            AccessTools.PropertyGetter(typeof(ChartRenderUnscaledTime), nameof(ChartRenderUnscaledTime.UnscaledDeltaTime));

        private static IEnumerable<MethodBase> TargetMethods()
        {
            List<MethodBase> targets = new List<MethodBase>();
            foreach (string typeName in TargetTypes)
            {
                Type? type = AccessTools.TypeByName(typeName);
                if (type == null)
                {
                    continue;
                }

                foreach (MethodInfo method in AccessTools.GetDeclaredMethods(type))
                {
                    if (method.IsAbstract || method.ContainsGenericParameters || method.GetMethodBody() == null)
                    {
                        continue;
                    }

                    if (PatchProcessor.ReadMethodBody(method).Any(op =>
                            Equals(op.Value, UnscaledTimeGetter) || Equals(op.Value, UnscaledDeltaTimeGetter)))
                    {
                        targets.Add(method);
                    }
                }
            }

            if (targets.Count == 0)
            {
                throw new MissingMethodException("No game visuals using Time.unscaledTime were found.");
            }

            return targets;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(UnscaledTimeGetter))
                {
                    instruction.operand = CaptureUnscaledTime;
                }
                else if (instruction.Calls(UnscaledDeltaTimeGetter))
                {
                    instruction.operand = CaptureUnscaledDeltaTime;
                }

                yield return instruction;
            }
        }
    }
}
