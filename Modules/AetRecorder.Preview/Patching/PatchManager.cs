using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ADOFAI.EditorTweaks.ChartRendering.Features.ChartRendering;
using ADOFAI.EditorTweaks.ChartRendering.Features.RenderInputGuard;
using ADOFAI.EditorTweaks.ChartRendering.Features.VideoBackgroundSync;
using HarmonyLib;
using static CheryTools.Sonnet.Shared.SonnetText;

namespace ADOFAI.EditorTweaks.ChartRendering.Patching
{
    internal enum PatchFeature
    {
        VideoBackgroundSync,
        RenderInputGuard,
        ChartRendering,
        RenderVisualTime
    }

    internal enum PatchGroupState
    {
        Inactive,
        Active,
        Failed,
        Blocked
    }

    internal sealed class PatchGroupStatus
    {
        public PatchGroupStatus(PatchFeature feature, string name)
        {
            Feature = feature;
            Name = name;
        }

        public PatchFeature Feature { get; }

        public string Name { get; }

        public string DisplayNameKey => Name;

        public PatchGroupState State { get; internal set; }

        public int PatchCount { get; internal set; }

        public string FailedPatchName { get; internal set; } = string.Empty;

        public string Reason { get; internal set; } = string.Empty;

        public Exception? Exception { get; internal set; }

        public PatchFeature? BlockedBy { get; internal set; }
    }

    internal sealed class PatchGroupDefinition
    {
        public PatchGroupDefinition(
            PatchFeature feature,
            string id,
            string name,
            Type[] patchRoots,
            params PatchFeature[] dependencies)
        {
            Feature = feature;
            Id = id;
            Name = name;
            PatchRoots = patchRoots;
            Dependencies = dependencies;
        }

        public PatchFeature Feature { get; }

        public string Id { get; }

        public string Name { get; }

        public Type[] PatchRoots { get; }

        public PatchFeature[] Dependencies { get; }
    }

    internal static class PatchManager
    {
        private static readonly PatchGroupDefinition[] definitions =
        {
            new PatchGroupDefinition(
                PatchFeature.VideoBackgroundSync,
                "video-background-sync",
                "Video background synchronization",
                new[] { typeof(VideoBackgroundSyncPatches) }),
            new PatchGroupDefinition(
                PatchFeature.RenderInputGuard,
                "render-input-guard",
                "Render input guard",
                new[] { typeof(RenderInputGuardPatches) }),
            new PatchGroupDefinition(
                PatchFeature.ChartRendering,
                "chart-rendering",
                "Chart rendering",
                new[]
                {
                    typeof(ChartRenderConductorSongPositionPatch),
                    typeof(ChartRenderConductorSongPositionGetterPatch),
                    typeof(ChartRenderAudioPatches),
                    typeof(ChartRenderPlayScheduledPatch),
                    typeof(ChartRenderConductorUpdatePatch),
                    typeof(ChartRenderAsyncAnglePatch),
                    typeof(ChartRenderHitEndFloorPatch),
                    typeof(ChartRenderJudgmentPatches),
                    typeof(ChartRenderAudioBufferCheckPatch),
                    typeof(ChartRenderCustomFrameRateScreenPatch)
                },
                PatchFeature.RenderInputGuard),
            // Kept separate so a game update that moves these visuals only disables this fix,
            // not chart rendering itself.
            new PatchGroupDefinition(
                PatchFeature.RenderVisualTime,
                "render-visual-time",
                "Render visual time",
                new[] { typeof(ChartRenderUnscaledTimePatch) })
        };

        private static readonly Dictionary<PatchFeature, PatchGroupStatus> statuses =
            new Dictionary<PatchFeature, PatchGroupStatus>();

        private static readonly Dictionary<PatchFeature, Harmony> activeHarmonies =
            new Dictionary<PatchFeature, Harmony>();

        private static readonly Dictionary<PatchFeature, string> activeHarmonyIds =
            new Dictionary<PatchFeature, string>();

        public static IReadOnlyList<PatchGroupStatus> Statuses =>
            definitions.Select(GetOrCreateStatus).ToArray();

        public static bool HasRegistrationErrors { get; private set; }

        public static IReadOnlyList<PatchGroupStatus> ApplyAll(string modId)
        {
            ClearActivePatches();
            statuses.Clear();
            HasRegistrationErrors = false;

            Dictionary<PatchFeature, List<Type>> registrations =
                BuildRegistrations(out Dictionary<PatchFeature, string> invalidGroups);
            ValidateRegistrations(registrations, invalidGroups);

            foreach (PatchGroupDefinition definition in definitions)
            {
                PatchGroupStatus status = GetOrCreateStatus(definition);
                List<Type> patchTypes = registrations.TryGetValue(definition.Feature, out List<Type>? registeredTypes)
                    ? registeredTypes
                    : new List<Type>();
                status.PatchCount = patchTypes.Count;

                if (invalidGroups.TryGetValue(definition.Feature, out string? registrationError))
                {
                    status.State = PatchGroupState.Failed;
                    status.Reason = registrationError;
                    LogError($"[PatchManager] Group '{definition.Id}' was not applied: {registrationError}");
                    continue;
                }

                PatchFeature? unavailableDependency = FindUnavailableDependency(definition);
                if (unavailableDependency.HasValue)
                {
                    status.State = PatchGroupState.Blocked;
                    status.BlockedBy = unavailableDependency;
                    status.Reason = "Required patch group is unavailable.";
                    Main.Log($"[PatchManager] Group '{definition.Id}' blocked by '{unavailableDependency.Value}'.");
                    continue;
                }

                ApplyGroup(modId, definition, patchTypes, status);
            }

            int activeCount = statuses.Values.Count(status => status.State == PatchGroupState.Active);
            int failedCount = statuses.Values.Count(status => status.State == PatchGroupState.Failed);
            int blockedCount = statuses.Values.Count(status => status.State == PatchGroupState.Blocked);
            Main.Log($"[PatchManager] {activeCount}/{definitions.Length} groups active, {failedCount} failed, {blockedCount} blocked.");
            return Statuses;
        }

        public static void UnpatchAll()
        {
            ClearActivePatches();
            foreach (PatchGroupDefinition definition in definitions)
            {
                PatchGroupStatus status = GetOrCreateStatus(definition);
                status.State = PatchGroupState.Inactive;
                status.BlockedBy = null;
                status.FailedPatchName = string.Empty;
                status.Reason = string.Empty;
                status.Exception = null;
            }
        }

        public static bool IsAvailable(PatchFeature feature)
        {
            return statuses.TryGetValue(feature, out PatchGroupStatus? status)
                && status.State == PatchGroupState.Active;
        }

        public static string GetSummary()
        {
            int activeCount = statuses.Values.Count(item => item.State == PatchGroupState.Active);
            return activeCount + "/" + definitions.Length + L(" 渲染功能组可用", " render feature groups available");
        }

        private static void ApplyGroup(
            string modId,
            PatchGroupDefinition definition,
            List<Type> patchTypes,
            PatchGroupStatus status)
        {
            string harmonyId = modId + "." + definition.Id;
            Harmony harmony = new Harmony(harmonyId);
            Type? currentPatchType = null;

            try
            {
                foreach (Type patchType in patchTypes)
                {
                    currentPatchType = patchType;
                    harmony.CreateClassProcessor(patchType).Patch();
                }

                activeHarmonies[definition.Feature] = harmony;
                activeHarmonyIds[definition.Feature] = harmonyId;
                status.State = PatchGroupState.Active;
                Main.Log($"[PatchManager] Group '{definition.Id}' active ({patchTypes.Count} patches).");
            }
            catch (Exception exception)
            {
                try
                {
                    harmony.UnpatchAll(harmonyId);
                }
                catch (Exception rollbackException)
                {
                    LogError($"[PatchManager] Failed to roll back group '{definition.Id}': {rollbackException}");
                }

                Exception rootException = exception.GetBaseException();
                status.State = PatchGroupState.Failed;
                status.FailedPatchName = currentPatchType?.FullName ?? string.Empty;
                status.Reason = rootException.Message;
                status.Exception = exception;
                LogError(
                    $"[PatchManager] Group '{definition.Id}' failed at '{status.FailedPatchName}' and was rolled back.\n"
                    + exception);
            }
        }

        private static Dictionary<PatchFeature, List<Type>> BuildRegistrations(
            out Dictionary<PatchFeature, string> invalidGroups)
        {
            Dictionary<PatchFeature, List<Type>> registrations =
                new Dictionary<PatchFeature, List<Type>>();
            invalidGroups = new Dictionary<PatchFeature, string>();

            foreach (PatchGroupDefinition definition in definitions)
            {
                HashSet<Type> patchTypes = new HashSet<Type>();
                try
                {
                    foreach (Type root in definition.PatchRoots)
                    {
                        CollectPatchTypes(root, patchTypes);
                    }

                    registrations[definition.Feature] = patchTypes
                        .OrderBy(type => type.FullName, StringComparer.Ordinal)
                        .ToList();
                }
                catch (Exception exception)
                {
                    HasRegistrationErrors = true;
                    registrations[definition.Feature] = patchTypes.ToList();
                    invalidGroups[definition.Feature] = "Unable to inspect patch registrations: "
                        + exception.GetBaseException().Message;
                    LogError($"[PatchManager] Failed to inspect group '{definition.Id}': {exception}");
                }
            }

            return registrations;
        }

        private static void ValidateRegistrations(
            Dictionary<PatchFeature, List<Type>> registrations,
            Dictionary<PatchFeature, string> invalidGroups)
        {
            Dictionary<Type, List<PatchFeature>> owners = new Dictionary<Type, List<PatchFeature>>();
            foreach (KeyValuePair<PatchFeature, List<Type>> registration in registrations)
            {
                foreach (Type patchType in registration.Value)
                {
                    if (!owners.TryGetValue(patchType, out List<PatchFeature>? features))
                    {
                        features = new List<PatchFeature>();
                        owners[patchType] = features;
                    }

                    features.Add(registration.Key);
                }
            }

            foreach (KeyValuePair<Type, List<PatchFeature>> owner in owners.Where(item => item.Value.Count > 1))
            {
                HasRegistrationErrors = true;
                string message = "Patch type is registered more than once: "
                    + (owner.Key.FullName ?? owner.Key.Name);
                foreach (PatchFeature feature in owner.Value)
                {
                    invalidGroups[feature] = message;
                }

                LogError("[PatchManager] " + message);
            }

            int discoveredCount = 0;
            foreach (Type type in GetLoadableTypes(Assembly.GetExecutingAssembly()))
            {
                bool isPatchType;
                try
                {
                    isPatchType = IsPatchType(type);
                }
                catch (Exception exception)
                {
                    HasRegistrationErrors = true;
                    LogError($"[PatchManager] Unable to inspect Harmony metadata for '{type.FullName}': {exception}");
                    continue;
                }

                if (!isPatchType)
                {
                    continue;
                }

                discoveredCount++;
                if (!owners.ContainsKey(type))
                {
                    HasRegistrationErrors = true;
                    LogError($"[PatchManager] Unregistered Harmony patch type was skipped: {type.FullName}");
                }
            }

            Main.Log($"[PatchManager] Registration check: {discoveredCount} discovered, {owners.Count} registered.");
        }

        private static void CollectPatchTypes(Type type, HashSet<Type> patchTypes)
        {
            if (IsPatchType(type))
            {
                patchTypes.Add(type);
            }

            foreach (Type nestedType in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                CollectPatchTypes(nestedType, patchTypes);
            }
        }

        private static bool IsPatchType(Type type)
        {
            return type.IsDefined(typeof(HarmonyPatch), inherit: true);
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                HasRegistrationErrors = true;
                foreach (Exception? loaderException in exception.LoaderExceptions.Where(item => item != null))
                {
                    LogError("[PatchManager] Type loader error: " + loaderException);
                }

                return exception.Types.Where(type => type != null).Cast<Type>();
            }
        }

        private static PatchFeature? FindUnavailableDependency(PatchGroupDefinition definition)
        {
            foreach (PatchFeature dependency in definition.Dependencies)
            {
                if (!IsAvailable(dependency))
                {
                    return dependency;
                }
            }

            return null;
        }

        private static PatchGroupStatus GetOrCreateStatus(PatchGroupDefinition definition)
        {
            if (!statuses.TryGetValue(definition.Feature, out PatchGroupStatus? status))
            {
                status = new PatchGroupStatus(definition.Feature, definition.Name);
                statuses[definition.Feature] = status;
            }

            return status;
        }

        private static void ClearActivePatches()
        {
            foreach (KeyValuePair<PatchFeature, Harmony> activeHarmony in activeHarmonies)
            {
                if (!activeHarmonyIds.TryGetValue(activeHarmony.Key, out string? harmonyId))
                {
                    continue;
                }

                try
                {
                    activeHarmony.Value.UnpatchAll(harmonyId);
                }
                catch (Exception exception)
                {
                    LogError($"[PatchManager] Failed to unpatch group '{activeHarmony.Key}': {exception}");
                }
            }

            activeHarmonies.Clear();
            activeHarmonyIds.Clear();
        }

        private static void LogError(string message)
        {
            Main.LogError(message);
        }
    }
}
