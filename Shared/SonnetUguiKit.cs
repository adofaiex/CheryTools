using System;
using UnityEngine;

namespace CheryTools.Sonnet.Shared
{
    // Shared source file linked into the KeyViewer and Overlayer projects (see SonnetWindowChrome for
    // how shared sources are compiled). Small UGUI and reflection helpers used by both runtime hosts.
    internal static class SonnetUguiKit
    {
        internal static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // A nested canvas confines layout and batching rebuilds to its own subtree, so an element that
        // changes every frame (rain bars, live text) does not force its siblings to be rebatched.
        internal static void IsolateCanvas(RectTransform rect)
        {
            Canvas canvas = rect.gameObject.AddComponent<Canvas>();
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        }

        internal static Color ToColor(float[] value)
        {
            return value != null && value.Length >= 4 ? new Color(value[0], value[1], value[2], value[3]) : Color.white;
        }

        internal static float Pixel(float value) => Mathf.Round(value);

        internal static Vector2 Pixel(Vector2 value) => new Vector2(Mathf.Round(value.x), Mathf.Round(value.y));

        // VideoPlayer lives in UnityEngine.VideoModule, which the modules do not reference directly,
        // so it is driven through reflection.
        internal static void SetProperty(Component target, string name, object value)
        {
            try { target?.GetType().GetProperty(name)?.SetValue(target, value, null); }
            catch { }
        }

        internal static void SetEnumProperty(Component target, string name, string value)
        {
            try
            {
                var property = target?.GetType().GetProperty(name);
                if (property != null) property.SetValue(target, Enum.Parse(property.PropertyType, value), null);
            }
            catch { }
        }

        internal static void Invoke(Component target, string method)
        {
            try { target?.GetType().GetMethod(method, Type.EmptyTypes)?.Invoke(target, null); }
            catch { }
        }
    }
}
