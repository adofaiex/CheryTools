using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using UnityEngine;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace CheryTools
{
    /// <summary>
    /// Connects Dear ImGui's text-input state to Unity's legacy IME support.
    /// Unity only enables IME automatically for its own text fields, so a custom
    /// ImGui backend has to opt in and provide the current caret position.
    /// </summary>
    internal static class ImGuiImeBridge
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PlatformSetImeDataDelegate(IntPtr context, IntPtr viewport, IntPtr data);

        [StructLayout(LayoutKind.Sequential)]
        private struct PlatformImeData
        {
            [MarshalAs(UnmanagedType.I1)]
            public bool WantVisible;
            public Vector2 InputPos;
            public float InputLineHeight;
        }

        private static PlatformSetImeDataDelegate _platformSetImeDataDelegate;
        private static bool _initialized;
        private static bool _platformWantsIme;
        private static bool _customWantsImeThisFrame;
        private static bool _customWantsImeLastFrame;
        private static bool _imeEnabled;
        private static bool _hasCaretPosition;
        private static Vector2 _caretPosition;
        private static float _caretLineHeight;

        public static void Initialize()
        {
            if (_initialized) return;

            _platformSetImeDataDelegate = OnPlatformSetImeData;
            ImGuiPlatformIOPtr platformIo = ImGui.GetPlatformIO();
            platformIo.Platform_SetImeDataFn = Marshal.GetFunctionPointerForDelegate(_platformSetImeDataDelegate);
            _initialized = true;
            SetImeEnabled(false);
        }

        public static void BeginFrame()
        {
            if (!_initialized) return;

            _customWantsImeThisFrame = false;
            _platformWantsIme = false;

            // Keep IME enabled while entering the next frame. ImGui updates
            // WantTextInput and the precise caret position later in this frame.
            bool wantsIme = ImGui.GetIO().WantTextInput || _customWantsImeLastFrame;
            SetImeEnabled(wantsIme);
        }

        public static void RequestCustomTextInput(Vector2 caretPosition, float lineHeight)
        {
            _customWantsImeThisFrame = true;
            UpdateCaretPosition(caretPosition, lineHeight);
        }

        public static void AddInputCharacters(ImGuiIOPtr io, string input)
        {
            if (string.IsNullOrEmpty(input)) return;

            for (int i = 0; i < input.Length; i++)
            {
                char current = input[i];
                if (current == '\b' || current == '\r' || current == '\n' || char.IsControl(current))
                    continue;

                int codePoint;
                if (char.IsHighSurrogate(current) && i + 1 < input.Length && char.IsLowSurrogate(input[i + 1]))
                {
                    codePoint = char.ConvertToUtf32(current, input[++i]);
                }
                else if (char.IsSurrogate(current))
                {
                    continue;
                }
                else
                {
                    codePoint = current;
                }

                io.AddInputCharacter((uint)codePoint);
            }
        }

        public static void DrawCompositionPreview()
        {
            if (!_imeEnabled || !_hasCaretPosition) return;

            string composition = Input.compositionString;
            if (string.IsNullOrEmpty(composition)) return;

            float fontSize = Math.Max(12f, ImGui.GetFontSize());
            ImFontPtr font = ImGuiController.ChineseDefaultUIFont;
            Vector2 textSize = font.CalcTextSizeA(fontSize, float.MaxValue, 0f, composition);
            Vector2 padding = new Vector2(5f, 3f);
            Vector2 textPosition = _caretPosition + new Vector2(1f, Math.Max(0f, _caretLineHeight - fontSize));
            Vector2 min = textPosition - padding;
            Vector2 max = textPosition + textSize + padding;
            ImDrawListPtr drawList = ImGui.GetForegroundDrawList();
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.04f, 0.05f, 0.07f, 0.96f)), 3f);
            drawList.AddText(font, fontSize, textPosition, ImGui.GetColorU32(new Vector4(0.95f, 0.97f, 1f, 1f)), composition);
            float underlineY = textPosition.Y + textSize.Y + 1f;
            drawList.AddLine(new Vector2(textPosition.X, underlineY), new Vector2(textPosition.X + textSize.X, underlineY),
                ImGui.GetColorU32(new Vector4(0.28f, 0.65f, 1f, 1f)), 1.5f);
        }

        public static void EndFrame()
        {
            if (!_initialized) return;

            bool wantsIme = ImGui.GetIO().WantTextInput || _platformWantsIme || _customWantsImeThisFrame;
            _customWantsImeLastFrame = _customWantsImeThisFrame;
            SetImeEnabled(wantsIme);
            if (wantsIme && _hasCaretPosition)
                ApplyCompositionCursorPosition();
        }

        public static void Suspend()
        {
            _platformWantsIme = false;
            _customWantsImeThisFrame = false;
            _customWantsImeLastFrame = false;
            _hasCaretPosition = false;
            SetImeEnabled(false);
        }

        public static void Shutdown()
        {
            if (!_initialized) return;

            Suspend();
            try
            {
                ImGuiPlatformIOPtr platformIo = ImGui.GetPlatformIO();
                platformIo.Platform_SetImeDataFn = IntPtr.Zero;
                platformIo.Platform_ImeUserData = IntPtr.Zero;
            }
            catch
            {
                // The ImGui context may already be tearing down.
            }
            _platformSetImeDataDelegate = null;
            _initialized = false;
        }

        private static void OnPlatformSetImeData(IntPtr context, IntPtr viewport, IntPtr data)
        {
            if (data == IntPtr.Zero) return;

            PlatformImeData imeData = (PlatformImeData)Marshal.PtrToStructure(data, typeof(PlatformImeData));
            _platformWantsIme = imeData.WantVisible;
            if (imeData.WantVisible)
                UpdateCaretPosition(imeData.InputPos, imeData.InputLineHeight);
        }

        private static void UpdateCaretPosition(Vector2 position, float lineHeight)
        {
            if (float.IsNaN(position.X) || float.IsInfinity(position.X)
                || float.IsNaN(position.Y) || float.IsInfinity(position.Y))
                return;

            _caretPosition = position;
            _caretLineHeight = float.IsNaN(lineHeight) || float.IsInfinity(lineHeight) ? 0f : Math.Max(0f, lineHeight);
            _hasCaretPosition = true;
        }

        private static void SetImeEnabled(bool enabled)
        {
            if (_imeEnabled == enabled) return;

            _imeEnabled = enabled;
            try
            {
                ApplyUnityImeMode(enabled);
            }
            catch (Exception)
            {
                // Allows the non-Unity regression harness to exercise the bridge.
            }
            if (enabled && _hasCaretPosition)
                ApplyCompositionCursorPosition();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ApplyUnityImeMode(bool enabled)
        {
            Input.imeCompositionMode = enabled ? IMECompositionMode.On : IMECompositionMode.Auto;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ApplyCompositionCursorPosition()
        {
            float scale = Math.Max(0.001f, ImGuiController.PanelScale);
            float x = Mathf.Clamp(_caretPosition.X * scale, 0f, Math.Max(0f, Screen.width - 1f));
            float y = Mathf.Clamp((_caretPosition.Y + _caretLineHeight) * scale, 0f, Math.Max(0f, Screen.height - 1f));
            Input.compositionCursorPos = new UnityEngine.Vector2(x, y);
        }
    }
}
