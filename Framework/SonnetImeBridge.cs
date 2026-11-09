using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using UnityEngine;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;

namespace CheryTools.Sonnet.Framework
{
    /// <summary>Connects Dear ImGui text fields to Unity's legacy IME input.</summary>
    internal static class SonnetImeBridge
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PlatformSetImeDataDelegate(IntPtr context, IntPtr viewport, IntPtr data);

        [StructLayout(LayoutKind.Sequential)]
        private struct PlatformImeData
        {
            [MarshalAs(UnmanagedType.I1)] public bool WantVisible;
            public NumVector2 InputPos;
            public float InputLineHeight;
        }

        private static PlatformSetImeDataDelegate _callback;
        private static bool _initialized;
        private static bool _platformWantsIme;
        private static bool _imeEnabled;
        private static bool _hasCaret;
        private static NumVector2 _caret;
        private static float _lineHeight;

        internal static void Initialize()
        {
            if (_initialized) return;
            _callback = OnPlatformSetImeData;
            ImGuiPlatformIOPtr platform = ImGui.GetPlatformIO();
            platform.Platform_SetImeDataFn = Marshal.GetFunctionPointerForDelegate(_callback);
            _initialized = true;
            SetImeEnabled(false);
        }

        internal static void BeginFrame()
        {
            if (!_initialized) return;
            _platformWantsIme = false;
            SetImeEnabled(ImGui.GetIO().WantTextInput);
        }

        internal static void AddInputCharacters(ImGuiIOPtr io, string input)
        {
            if (string.IsNullOrEmpty(input)) return;
            for (int i = 0; i < input.Length; i++)
            {
                char current = input[i];
                if (char.IsControl(current)) continue;
                int codePoint;
                if (char.IsHighSurrogate(current) && i + 1 < input.Length && char.IsLowSurrogate(input[i + 1]))
                    codePoint = char.ConvertToUtf32(current, input[++i]);
                else if (char.IsSurrogate(current))
                    continue;
                else
                    codePoint = current;
                io.AddInputCharacter((uint)codePoint);
            }
        }

        internal static void DrawCompositionPreview()
        {
            if (!_imeEnabled || !_hasCaret) return;
            string composition = Input.compositionString;
            if (string.IsNullOrEmpty(composition)) return;
            float fontSize = Math.Max(12f, ImGui.GetFontSize());
            ImFontPtr font = ImGui.GetFont();
            NumVector2 size = font.CalcTextSizeA(fontSize, float.MaxValue, 0f, composition);
            NumVector2 textPos = _caret + new NumVector2(1f, Math.Max(0f, _lineHeight - fontSize));
            NumVector2 padding = new NumVector2(6f, 4f);
            ImDrawListPtr draw = ImGui.GetForegroundDrawList();
            draw.AddRectFilled(textPos - padding, textPos + size + padding,
                ImGui.GetColorU32(new NumVector4(0.015f, 0.022f, 0.034f, 0.97f)), 6f);
            draw.AddText(font, fontSize, textPos, ImGui.GetColorU32(new NumVector4(0.95f, 0.97f, 1f, 1f)), composition);
            float underline = textPos.Y + size.Y + 1f;
            draw.AddLine(new NumVector2(textPos.X, underline), new NumVector2(textPos.X + size.X, underline),
                ImGui.GetColorU32(new NumVector4(0.39f, 0.80f, 1f, 1f)), 1.5f);
        }

        internal static void EndFrame()
        {
            if (!_initialized) return;
            bool wantsIme = ImGui.GetIO().WantTextInput || _platformWantsIme;
            SetImeEnabled(wantsIme);
            if (wantsIme && _hasCaret) ApplyCursorPosition();
        }

        internal static void Suspend()
        {
            _platformWantsIme = false;
            _hasCaret = false;
            SetImeEnabled(false);
        }

        internal static void Shutdown()
        {
            if (!_initialized) return;
            Suspend();
            try
            {
                ImGuiPlatformIOPtr platform = ImGui.GetPlatformIO();
                platform.Platform_SetImeDataFn = IntPtr.Zero;
                platform.Platform_ImeUserData = IntPtr.Zero;
            }
            catch { }
            _callback = null;
            _initialized = false;
        }

        private static void OnPlatformSetImeData(IntPtr context, IntPtr viewport, IntPtr data)
        {
            if (data == IntPtr.Zero) return;
            PlatformImeData ime = (PlatformImeData)Marshal.PtrToStructure(data, typeof(PlatformImeData));
            _platformWantsIme = ime.WantVisible;
            if (!ime.WantVisible || float.IsNaN(ime.InputPos.X) || float.IsInfinity(ime.InputPos.X)
                || float.IsNaN(ime.InputPos.Y) || float.IsInfinity(ime.InputPos.Y)) return;
            _caret = ime.InputPos;
            _lineHeight = float.IsNaN(ime.InputLineHeight) || float.IsInfinity(ime.InputLineHeight)
                ? 0f : Math.Max(0f, ime.InputLineHeight);
            _hasCaret = true;
        }

        private static void SetImeEnabled(bool enabled)
        {
            if (_imeEnabled == enabled) return;
            _imeEnabled = enabled;
            try { ApplyImeMode(enabled); }
            catch { }
            if (enabled && _hasCaret) ApplyCursorPosition();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ApplyImeMode(bool enabled)
        {
            Input.imeCompositionMode = enabled ? IMECompositionMode.On : IMECompositionMode.Auto;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ApplyCursorPosition()
        {
            float scale = Math.Max(0.001f, SonnetImGuiController.PanelScale);
            float x = Mathf.Clamp(_caret.X * scale, 0f, Math.Max(0f, Screen.width - 1f));
            float y = Mathf.Clamp((_caret.Y + _lineHeight) * scale, 0f, Math.Max(0f, Screen.height - 1f));
            Input.compositionCursorPos = new UnityEngine.Vector2(x, y);
        }
    }
}
