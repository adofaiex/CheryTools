using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ImGuiNET;
using UnityEngine;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace CheryTools
{
    public static class RichTextCodeEditor
    {
        private sealed class EditorState
        {
            public int Cursor;
            public int SelectionAnchor = -1;
            public double LastBlinkTime;
            public bool CursorVisible = true;
            public int LastTextLength = -1;
            public bool Active;
            public bool DragSelecting;
            public KeyCode RepeatKey = KeyCode.None;
            public double NextRepeatTime;
        }

        private struct HighlightSegment
        {
            public int Start;
            public int Length;
            public uint Color;

            public HighlightSegment(int start, int length, uint color)
            {
                Start = start;
                Length = length;
                Color = color;
            }
        }

        private static readonly Dictionary<string, EditorState> States = new Dictionary<string, EditorState>();
        private static readonly Regex TagRegex = new Regex(@"\{[^{}\r\n]+\}", RegexOptions.Compiled);
        private static readonly Regex RichTagRegex = new Regex(@"</?(color|size)(=[^>]+)?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly HashSet<string> KnownTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "{fps}", "{kps}", "{tot}", "{combo}", "{combo:p}", "{music}", "{ttile}", "{atile}",
            "{level}", "{x}", "{xperfect:xpp}", "{xperfect:epp}", "{xperfect:lpp}", "{bpm}", "{tbpm}",
            "{cbpm}", "{cur}", "{maptime}", "{maptime:p}", "{musictime}", "{musictime:p}", "{datey}",
            "{datem}", "{dated}", "{wtime}", "{wtime12}", "{judge}", "{interval}", "{acc}", "{xacc}",
            "{progress}", "{te}", "{ve}", "{ep}", "{p}", "{lp}", "{vl}", "{tl}", "{fm}", "{fo}",
            "{miss}"
        };

        public static bool Draw(string id, ref string text, Vector2 size)
        {
            if (text == null)
            {
                text = string.Empty;
            }

            EditorState state = GetState(id);
            if (state.LastTextLength != text.Length)
            {
                state.Cursor = Clamp(state.Cursor, 0, text.Length);
                state.SelectionAnchor = state.SelectionAnchor >= 0 ? Clamp(state.SelectionAnchor, 0, text.Length) : -1;
                state.LastTextLength = text.Length;
            }

            bool changed = false;
            Vector2 actualSize = size;
            if (actualSize.X <= 0f)
            {
                actualSize.X = ImGui.GetContentRegionAvail().X;
            }
            if (actualSize.Y < 0f)
            {
                actualSize.Y = Math.Max(120f, ImGui.GetContentRegionAvail().Y);
            }
            else if (actualSize.Y <= 0f)
            {
                actualSize.Y = 120f;
            }

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8f, 6f));
            ImGui.BeginChild(id, actualSize, ImGuiChildFlags.Borders, ImGuiWindowFlags.HorizontalScrollbar);
            bool isHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup);

            Vector2 start = ImGui.GetCursorScreenPos();
            float fontSize = ImGui.GetFontSize();
            float lineHeight = fontSize + 4f;
            List<int> lineStarts = BuildLineStarts(text);
            float contentWidth = Math.Max(ImGui.GetContentRegionAvail().X, GetContentWidth(text, lineStarts) + 24f);
            float totalHeight = Math.Max(lineHeight, lineStarts.Count * lineHeight);
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            float scrollYBefore = ImGui.GetScrollY();

            ImGui.InvisibleButton(id + "_hit", new Vector2(contentWidth, totalHeight));
            bool clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
            bool mouseDown = ImGui.IsMouseDown(ImGuiMouseButton.Left);
            bool active = ImGui.IsItemActive() || state.Active;

            if (clicked)
            {
                ImGui.SetWindowFocus();
                state.Active = true;
                state.Cursor = HitTest(text, lineStarts, start, lineHeight, ImGui.GetMousePos());
                state.SelectionAnchor = state.Cursor;
                state.DragSelecting = true;
                ResetBlink(state);
            }
            else if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !isHovered)
            {
                state.Active = false;
                state.DragSelecting = false;
                ClearSelection(state);
            }

            if (state.DragSelecting)
            {
                if (mouseDown)
                {
                    state.Cursor = HitTest(text, lineStarts, start, lineHeight, ImGui.GetMousePos());
                    ResetBlink(state);
                }
                else
                {
                    if (!HasSelection(state))
                    {
                        ClearSelection(state);
                    }
                    state.DragSelecting = false;
                }
            }

            if (isHovered && ctrl)
            {
                ImGuiIOPtr io = ImGui.GetIO();
                if (Math.Abs(io.MouseWheel) > 0.001f)
                {
                    ImGui.SetScrollX(Math.Max(0f, ImGui.GetScrollX() - io.MouseWheel * 80f));
                    ImGui.SetScrollY(scrollYBefore);
                }
            }

            if (isHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.TextInput);
            }

            if (state.Active)
            {
                changed |= HandleInput(ref text, state);
            }
            if (changed)
            {
                lineStarts = BuildLineStarts(text);
                contentWidth = Math.Max(ImGui.GetContentRegionAvail().X, GetContentWidth(text, lineStarts) + 24f);
                totalHeight = Math.Max(lineHeight, lineStarts.Count * lineHeight);
            }

            DrawEditor(text, state, lineStarts, start, lineHeight, state.Active);

            ImGui.EndChild();
            ImGui.PopStyleVar();

            if (changed)
            {
                state.LastTextLength = text.Length;
            }

            return changed;
        }

        public static void SetCursorToEnd(string id, string text)
        {
            EditorState state = GetState(id);
            state.Cursor = text != null ? text.Length : 0;
            ClearSelection(state);
            ResetBlink(state);
        }

        public static void SetCursor(string id, string text, int cursor)
        {
            EditorState state = GetState(id);
            int length = text != null ? text.Length : 0;
            state.Cursor = Clamp(cursor, 0, length);
            ClearSelection(state);
            ResetBlink(state);
        }

        public static int GetCursor(string id, string text)
        {
            EditorState state = GetState(id);
            int length = text != null ? text.Length : 0;
            return Clamp(state.Cursor, 0, length);
        }

        private static EditorState GetState(string id)
        {
            if (!States.TryGetValue(id, out EditorState state))
            {
                state = new EditorState();
                States[id] = state;
            }
            return state;
        }

        private static bool HandleInput(ref string text, EditorState state)
        {
            bool changed = false;
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (ctrl && Input.GetKeyDown(KeyCode.A))
            {
                state.SelectionAnchor = 0;
                state.Cursor = text.Length;
                ResetBlink(state);
                return false;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.C))
            {
                string selected = GetSelectedText(text, state);
                if (!string.IsNullOrEmpty(selected))
                {
                    GUIUtility.systemCopyBuffer = selected;
                }
                return false;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.X))
            {
                string selected = GetSelectedText(text, state);
                if (!string.IsNullOrEmpty(selected))
                {
                    GUIUtility.systemCopyBuffer = selected;
                    DeleteSelection(ref text, state);
                    changed = true;
                }
                return changed;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.V))
            {
                string clip = GUIUtility.systemCopyBuffer ?? string.Empty;
                if (clip.Length > 0)
                {
                    InsertText(ref text, state, clip.Replace("\r\n", "\n").Replace('\r', '\n'));
                    changed = true;
                }
                return changed;
            }

            if (IsKeyPressedOrRepeated(state, KeyCode.LeftArrow))
            {
                MoveCursor(text, state, Math.Max(0, state.Cursor - 1), shift);
            }
            if (IsKeyPressedOrRepeated(state, KeyCode.RightArrow))
            {
                MoveCursor(text, state, Math.Min(text.Length, state.Cursor + 1), shift);
            }
            if (IsKeyPressedOrRepeated(state, KeyCode.Home))
            {
                MoveCursor(text, state, GetLineStart(text, state.Cursor), shift);
            }
            if (IsKeyPressedOrRepeated(state, KeyCode.End))
            {
                MoveCursor(text, state, GetLineEnd(text, state.Cursor), shift);
            }
            if (IsKeyPressedOrRepeated(state, KeyCode.UpArrow))
            {
                MoveCursor(text, state, MoveVertical(text, state.Cursor, -1), shift);
            }
            if (IsKeyPressedOrRepeated(state, KeyCode.DownArrow))
            {
                MoveCursor(text, state, MoveVertical(text, state.Cursor, 1), shift);
            }

            if (IsKeyPressedOrRepeated(state, KeyCode.Backspace))
            {
                if (HasSelection(state))
                {
                    DeleteSelection(ref text, state);
                    changed = true;
                }
                else if (state.Cursor > 0)
                {
                    text = text.Remove(state.Cursor - 1, 1);
                    state.Cursor--;
                    changed = true;
                }
                ResetBlink(state);
            }

            if (IsKeyPressedOrRepeated(state, KeyCode.Delete))
            {
                if (HasSelection(state))
                {
                    DeleteSelection(ref text, state);
                    changed = true;
                }
                else if (state.Cursor < text.Length)
                {
                    text = text.Remove(state.Cursor, 1);
                    changed = true;
                }
                ResetBlink(state);
            }

            if (!ctrl && !string.IsNullOrEmpty(Input.inputString))
            {
                foreach (char c in Input.inputString)
                {
                    if (c == '\b')
                    {
                        continue;
                    }
                    if (c == '\r' || c == '\n')
                    {
                        InsertText(ref text, state, "\n");
                        changed = true;
                        continue;
                    }
                    if (!char.IsControl(c))
                    {
                        InsertText(ref text, state, c.ToString());
                        changed = true;
                    }
                }
            }

            if (state.RepeatKey != KeyCode.None && !Input.GetKey(state.RepeatKey))
            {
                state.RepeatKey = KeyCode.None;
            }

            return changed;
        }

        private static bool IsKeyPressedOrRepeated(EditorState state, KeyCode key)
        {
            if (Input.GetKeyDown(key))
            {
                state.RepeatKey = key;
                state.NextRepeatTime = ImGui.GetTime() + 0.34;
                return true;
            }

            if (!Input.GetKey(key) || state.RepeatKey != key)
            {
                return false;
            }

            double now = ImGui.GetTime();
            if (now < state.NextRepeatTime)
            {
                return false;
            }

            state.NextRepeatTime = now + 0.035;
            return true;
        }

        private static void DrawEditor(string text, EditorState state, List<int> lineStarts, Vector2 start, float lineHeight, bool active)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2 padding = new Vector2(0f, 0f);
            float fontSize = ImGui.GetFontSize();
            float maxWidth = ImGui.GetContentRegionAvail().X;
            uint defaultColor = ImGui.GetColorU32(ImGuiCol.Text);
            uint gutterColor = ImGui.GetColorU32(new Vector4(0.42f, 0.48f, 0.56f, 1f));
            uint selectionColor = ImGui.GetColorU32(new Vector4(0.18f, 0.44f, 0.85f, 0.42f));
            uint cursorColor = ImGui.GetColorU32(new Vector4(0.95f, 0.97f, 1f, 1f));
            uint importColor = ImGui.GetColorU32(new Vector4(0.95f, 0.68f, 0.30f, 1f));
            uint commentColor = ImGui.GetColorU32(new Vector4(0.45f, 0.52f, 0.58f, 1f));
            uint knownTagColor = ImGui.GetColorU32(new Vector4(0.42f, 0.82f, 1f, 1f));
            uint unknownTagColor = ImGui.GetColorU32(new Vector4(1f, 0.42f, 0.42f, 1f));
            uint richTagColor = ImGui.GetColorU32(new Vector4(0.82f, 0.58f, 1f, 1f));

            int selStart = 0;
            int selEnd = 0;
            bool hasSelection = GetSelectionRange(state, out selStart, out selEnd);

            for (int line = 0; line < lineStarts.Count; line++)
            {
                int lineStart = lineStarts[line];
                int lineEnd = GetLineEndByIndex(text, lineStarts, line);
                string lineText = text.Substring(lineStart, lineEnd - lineStart);
                Vector2 pos = start + padding + new Vector2(0f, line * lineHeight);

                drawList.AddText(ImGui.GetFont(), fontSize, pos + new Vector2(0f, 0f), gutterColor, (line + 1).ToString());
                Vector2 textPos = pos + new Vector2(38f, 0f);

                if (hasSelection && selEnd > lineStart && selStart < lineEnd + 1)
                {
                    int localStart = Clamp(selStart - lineStart, 0, lineText.Length);
                    int localEnd = Clamp(selEnd - lineStart, 0, lineText.Length);
                    float x1 = textPos.X + CalcWidth(lineText, 0, localStart);
                    float x2 = textPos.X + CalcWidth(lineText, 0, localEnd);
                    drawList.AddRectFilled(new Vector2(x1, textPos.Y - 1f), new Vector2(Math.Max(x2, x1 + 2f), textPos.Y + lineHeight - 1f), selectionColor);
                }

                DrawHighlightedLine(drawList, lineText, lineStart, textPos, fontSize, defaultColor, importColor, commentColor, knownTagColor, unknownTagColor, richTagColor);
            }

            if (active)
            {
                double now = ImGui.GetTime();
                if (now - state.LastBlinkTime > 0.5)
                {
                    state.CursorVisible = !state.CursorVisible;
                    state.LastBlinkTime = now;
                }

                if (state.CursorVisible)
                {
                    CursorToLineColumn(text, lineStarts, state.Cursor, out int cursorLine, out int cursorColumn);
                    int lineStart = lineStarts[Math.Max(0, Math.Min(cursorLine, lineStarts.Count - 1))];
                    int lineEnd = GetLineEndByIndex(text, lineStarts, cursorLine);
                    string lineText = text.Substring(lineStart, lineEnd - lineStart);
                    float cursorX = start.X + 38f + CalcWidth(lineText, 0, Math.Min(cursorColumn, lineText.Length));
                    float cursorY = start.Y + cursorLine * lineHeight;
                    drawList.AddLine(new Vector2(cursorX, cursorY - 1f), new Vector2(cursorX, cursorY + lineHeight - 1f), cursorColor, 1.5f);
                }
            }
        }

        private static void DrawHighlightedLine(ImDrawListPtr drawList, string lineText, int lineGlobalStart, Vector2 pos, float fontSize, uint defaultColor, uint importColor, uint commentColor, uint knownTagColor, uint unknownTagColor, uint richTagColor)
        {
            List<HighlightSegment> segments = BuildHighlights(lineText, lineGlobalStart, defaultColor, importColor, commentColor, knownTagColor, unknownTagColor, richTagColor);
            float x = pos.X;
            foreach (HighlightSegment segment in segments)
            {
                if (segment.Length <= 0)
                {
                    continue;
                }

                string part = lineText.Substring(segment.Start, segment.Length);
                drawList.AddText(ImGui.GetFont(), fontSize, new Vector2(x, pos.Y), segment.Color, part);
                x += ImGui.GetFont().CalcTextSizeA(fontSize, float.MaxValue, 0f, part).X;
            }
        }

        private static List<HighlightSegment> BuildHighlights(string lineText, int lineGlobalStart, uint defaultColor, uint importColor, uint commentColor, uint knownTagColor, uint unknownTagColor, uint richTagColor)
        {
            var marks = new List<HighlightSegment>();
            string trimmed = lineText.TrimStart();
            if (trimmed.StartsWith("##", StringComparison.Ordinal))
            {
                marks.Add(new HighlightSegment(0, lineText.Length, commentColor));
                return marks;
            }
            if (trimmed.StartsWith("#import regex", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("#enable regex", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("#regex", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("#replace", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("#substitute", StringComparison.OrdinalIgnoreCase))
            {
                marks.Add(new HighlightSegment(0, lineText.Length, importColor));
                return marks;
            }

            foreach (Match match in RichTagRegex.Matches(lineText))
            {
                marks.Add(new HighlightSegment(match.Index, match.Length, richTagColor));
            }
            foreach (Match match in TagRegex.Matches(lineText))
            {
                string tag = match.Value;
                uint color = IsKnownTag(tag) ? knownTagColor : unknownTagColor;
                marks.Add(new HighlightSegment(match.Index, match.Length, color));
            }

            if (marks.Count == 0)
            {
                marks.Add(new HighlightSegment(0, lineText.Length, defaultColor));
                return marks;
            }

            marks.Sort((a, b) => a.Start.CompareTo(b.Start));
            var result = new List<HighlightSegment>();
            int cursor = 0;
            foreach (HighlightSegment mark in marks)
            {
                if (mark.Start < cursor)
                {
                    continue;
                }
                if (mark.Start > cursor)
                {
                    result.Add(new HighlightSegment(cursor, mark.Start - cursor, defaultColor));
                }
                result.Add(mark);
                cursor = mark.Start + mark.Length;
            }
            if (cursor < lineText.Length)
            {
                result.Add(new HighlightSegment(cursor, lineText.Length - cursor, defaultColor));
            }
            return result;
        }

        private static bool IsKnownTag(string tag)
        {
            if (KnownTags.Contains(tag))
            {
                return true;
            }

            int colon = tag.IndexOf(':');
            if (colon > 1 && tag.EndsWith("}", StringComparison.Ordinal))
            {
                string baseTag = tag.Substring(0, colon) + "}";
                return KnownTags.Contains(baseTag);
            }
            return false;
        }

        private static int HitTest(string text, List<int> lineStarts, Vector2 start, float lineHeight, Vector2 mouse)
        {
            int line = Clamp((int)Math.Floor((mouse.Y - start.Y) / Math.Max(1f, lineHeight)), 0, lineStarts.Count - 1);
            int lineStart = lineStarts[line];
            int lineEnd = GetLineEndByIndex(text, lineStarts, line);
            string lineText = text.Substring(lineStart, lineEnd - lineStart);
            float x = start.X + 38f;
            int column = 0;
            for (int i = 0; i < lineText.Length; i++)
            {
                float charWidth = ImGui.GetFont().CalcTextSizeA(ImGui.GetFontSize(), float.MaxValue, 0f, lineText[i].ToString()).X;
                if (mouse.X < x + charWidth * 0.5f)
                {
                    break;
                }
                x += charWidth;
                column++;
            }
            return lineStart + column;
        }

        private static void InsertText(ref string text, EditorState state, string insert)
        {
            DeleteSelection(ref text, state);
            state.Cursor = Clamp(state.Cursor, 0, text.Length);
            text = text.Insert(state.Cursor, insert);
            state.Cursor += insert.Length;
            ClearSelection(state);
            ResetBlink(state);
        }

        private static void DeleteSelection(ref string text, EditorState state)
        {
            if (!GetSelectionRange(state, out int start, out int end))
            {
                return;
            }

            start = Clamp(start, 0, text.Length);
            end = Clamp(end, 0, text.Length);
            if (end > start)
            {
                text = text.Remove(start, end - start);
            }
            state.Cursor = start;
            ClearSelection(state);
            ResetBlink(state);
        }

        private static bool HasSelection(EditorState state)
        {
            return state.SelectionAnchor >= 0 && state.SelectionAnchor != state.Cursor;
        }

        private static bool GetSelectionRange(EditorState state, out int start, out int end)
        {
            if (!HasSelection(state))
            {
                start = 0;
                end = 0;
                return false;
            }

            start = Math.Min(state.SelectionAnchor, state.Cursor);
            end = Math.Max(state.SelectionAnchor, state.Cursor);
            return true;
        }

        private static string GetSelectedText(string text, EditorState state)
        {
            if (!GetSelectionRange(state, out int start, out int end))
            {
                return string.Empty;
            }

            start = Clamp(start, 0, text.Length);
            end = Clamp(end, 0, text.Length);
            return end > start ? text.Substring(start, end - start) : string.Empty;
        }

        private static void MoveCursor(string text, EditorState state, int target, bool keepSelection)
        {
            target = Clamp(target, 0, text.Length);
            if (keepSelection)
            {
                if (state.SelectionAnchor < 0)
                {
                    state.SelectionAnchor = state.Cursor;
                }
            }
            else
            {
                ClearSelection(state);
            }

            state.Cursor = target;
            ResetBlink(state);
        }

        private static int MoveVertical(string text, int cursor, int direction)
        {
            List<int> lines = BuildLineStarts(text);
            CursorToLineColumn(text, lines, cursor, out int line, out int column);
            int targetLine = Clamp(line + direction, 0, lines.Count - 1);
            int targetStart = lines[targetLine];
            int targetEnd = GetLineEndByIndex(text, lines, targetLine);
            return targetStart + Math.Min(column, targetEnd - targetStart);
        }

        private static void CursorToLineColumn(string text, List<int> lines, int cursor, out int line, out int column)
        {
            cursor = Clamp(cursor, 0, text.Length);
            line = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                int next = (i + 1 < lines.Count) ? lines[i + 1] : text.Length + 1;
                if (cursor < next)
                {
                    line = i;
                    break;
                }
            }
            column = cursor - lines[line];
            int lineEnd = GetLineEndByIndex(text, lines, line);
            column = Clamp(column, 0, lineEnd - lines[line]);
        }

        private static int GetLineStart(string text, int cursor)
        {
            cursor = Clamp(cursor, 0, text.Length);
            int index = text.LastIndexOf('\n', Math.Max(0, cursor - 1));
            return index >= 0 ? index + 1 : 0;
        }

        private static int GetLineEnd(string text, int cursor)
        {
            cursor = Clamp(cursor, 0, text.Length);
            int index = text.IndexOf('\n', cursor);
            return index >= 0 ? index : text.Length;
        }

        private static List<int> BuildLineStarts(string text)
        {
            var starts = new List<int> { 0 };
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    starts.Add(i + 1);
                }
            }
            return starts;
        }

        private static int GetLineEndByIndex(string text, List<int> lineStarts, int line)
        {
            if (line < 0 || line >= lineStarts.Count)
            {
                return text.Length;
            }
            int nextStart = line + 1 < lineStarts.Count ? lineStarts[line + 1] : text.Length;
            int end = nextStart;
            if (end > lineStarts[line] && end <= text.Length && text[end - 1] == '\n')
            {
                end--;
            }
            return end;
        }

        private static float CalcWidth(string text, int start, int end)
        {
            start = Clamp(start, 0, text.Length);
            end = Clamp(end, start, text.Length);
            if (end <= start)
            {
                return 0f;
            }
            string part = text.Substring(start, end - start);
            return ImGui.GetFont().CalcTextSizeA(ImGui.GetFontSize(), float.MaxValue, 0f, part).X;
        }

        private static float GetContentWidth(string text, List<int> lineStarts)
        {
            float width = 64f;
            for (int i = 0; i < lineStarts.Count; i++)
            {
                int lineStart = lineStarts[i];
                int lineEnd = GetLineEndByIndex(text, lineStarts, i);
                width = Math.Max(width, 38f + CalcWidth(text, lineStart, lineEnd));
            }
            return width;
        }

        private static void ClearSelection(EditorState state)
        {
            state.SelectionAnchor = -1;
        }

        private static void ResetBlink(EditorState state)
        {
            state.LastBlinkTime = ImGui.GetTime();
            state.CursorVisible = true;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
