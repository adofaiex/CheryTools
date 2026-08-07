using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CheryTools
{
    /// <summary>
    /// Keeps editor-version details out of Harmony patches. Detection is performed
    /// once when the type is initialized; no reflection is used from a game Update.
    /// </summary>
    internal static class EditorRuntimeCompatibility
    {
        private static readonly Func<scnEditor, bool> PlayModeGetter = CreatePlayModeGetter();

        internal static bool IsPlayMode(scnEditor editor)
        {
            if (editor == null)
                return false;

            try
            {
                return PlayModeGetter != null
                    ? PlayModeGetter(editor)
                    : scrController.instance != null
                      && scrController.instance.gameworld
                      && !scrController.instance.paused;
            }
            catch
            {
                return scrController.instance != null
                    && scrController.instance.gameworld
                    && !scrController.instance.paused;
            }
        }

        internal static bool IsMouseWheelZoom(float requestedDelta)
        {
            Vector2 wheel = RDInput.mouseScrollDelta;
            return Mathf.Abs(wheel.y) > 0.05f
                && Mathf.Abs(requestedDelta - wheel.y) < 0.0001f;
        }

        private static Func<scnEditor, bool> CreatePlayModeGetter()
        {
            PropertyInfo property = AccessTools.Property(typeof(scnEditor), "playMode");
            MethodInfo getter = property != null ? property.GetGetMethod(true) : null;
            if (getter != null && getter.ReturnType == typeof(bool))
            {
                try
                {
                    return (Func<scnEditor, bool>)Delegate.CreateDelegate(
                        typeof(Func<scnEditor, bool>),
                        null,
                        getter);
                }
                catch
                {
                    // Fall through to the field adapter used by older/newer builds.
                }
            }

            FieldInfo field = AccessTools.Field(typeof(scnEditor), "playMode");
            if (field != null && field.FieldType == typeof(bool))
            {
                return editor => (bool)field.GetValue(editor);
            }

            return null;
        }
    }
}
