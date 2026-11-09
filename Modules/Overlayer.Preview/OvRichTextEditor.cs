using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ImGuiNET;
using UnityEngine;
using NumVector2 = System.Numerics.Vector2;
using NumVector4 = System.Numerics.Vector4;
using CheryTools.Sonnet.Shared;

namespace CheryTools.Overlayer.Preview
{
    internal static class OvRichTextEditor
    {
        internal sealed class EditorState
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
            public readonly List<string> UndoStack = new List<string>();
            public readonly List<string> RedoStack = new List<string>();
            public double LastKeystrokeTime;
            public string LastCommittedText;
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

        private const int MaxUndoSteps = 60;
        private static readonly Dictionary<string, EditorState> States = new Dictionary<string, EditorState>(StringComparer.Ordinal);
        private static readonly Regex TagRegex = new Regex(@"\{[^{}\r\n]+\}", RegexOptions.Compiled);
        private static readonly Regex RichTagRegex = new Regex(@"</?(color|size|b|i|u|s|alpha|align|font|material|line-height|cspace|voffset|mark|gradient)(=[^>]+)?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly HashSet<string> KnownTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "{fps}", "{minfps}", "{maxfps}",
            "{progress}", "{bpm}", "{tbpm}", "{cbpm}", "{x}",
            "{level}", "{maptime}", "{maptime:p}", "{maptimep}",
            "{musictime}", "{musictime:p}", "{musictimep}",
            "{cur}", "{judge}", "{interval}",
            "{datey}", "{datem}", "{dated}", "{wtime}", "{wtime12}",
            "{year}", "{month}", "{day}", "{worldtime}", "{worldtime12}",
            "{acc}", "{xacc}", "{timing}",
            "{ttile}", "{atile}",
            "{te}", "{ve}", "{ep}", "{ap}", "{-p}", "{xp}", "{+p}", "{lp}", "{vl}", "{tl}",
            "{fm}", "{fo}", "{miss}",
            "{allprefectcombo}", "{allperfectcombo}", "{xprefectcombo}", "{xperfectcombo}",
            "{score}", "{music}", "{artist}", "{title}",
            "{attempts}", "{checkpointused}", "{curcheckpoint}", "{totalcheckpoint}",
            "{totalplaytime}", "{cherysui}", "{cherysui:ct}",
            "{gameversion}", "{ctversion}", "{cherytoolsversion}"
        };

        public static bool Draw(string id, ref string text, NumVector2 size)
        {
            if (text == null) text = string.Empty;
            if (text.IndexOf('\r') >= 0)
            {
                text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            }

            EditorState state = GetState(id);
            if (state.LastTextLength != text.Length)
            {
                state.Cursor = Clamp(state.Cursor, 0, text.Length);
                state.SelectionAnchor = state.SelectionAnchor >= 0 ? Clamp(state.SelectionAnchor, 0, text.Length) : -1;
                state.LastTextLength = text.Length;
            }

            if (state.LastCommittedText == null)
            {
                state.LastCommittedText = text;
            }

            bool changed = false;
            NumVector2 actualSize = size;
            if (actualSize.X <= 0f) actualSize.X = ImGui.GetContentRegionAvail().X;
            if (actualSize.Y < 0f) actualSize.Y = Math.Max(120f, ImGui.GetContentRegionAvail().Y);
            else if (actualSize.Y <= 0f) actualSize.Y = 120f;

            // 半透明暗色玻璃风格，与 Sonnet 主体视觉保持一致
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new NumVector2(0f, 6f));
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 8f);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new NumVector4(0.008f, 0.013f, 0.021f, 0.78f));
            ImGui.PushStyleColor(ImGuiCol.Border, new NumVector4(1f, 1f, 1f, 0.09f));

            ImGui.BeginChild(id, actualSize, ImGuiChildFlags.Borders, ImGuiWindowFlags.HorizontalScrollbar);
            bool isHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup);

            NumVector2 start = ImGui.GetCursorScreenPos();
            float fontSize = ImGui.GetFontSize();
            float lineHeight = fontSize + 5f;
            List<int> lineStarts = BuildLineStarts(text);
            float gutterWidth = Math.Max(42f, ImGui.CalcTextSize((lineStarts.Count + 1).ToString()).X + 20f);
            float textOffset = gutterWidth + 12f;
            float contentWidth = Math.Max(ImGui.GetContentRegionAvail().X, GetContentWidth(text, lineStarts, textOffset) + 24f);
            float totalHeight = Math.Max(lineHeight, lineStarts.Count * lineHeight + 8f);
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            float scrollYBefore = ImGui.GetScrollY();

            ImGui.InvisibleButton(id + "_hit", new NumVector2(contentWidth, totalHeight));
            bool clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
            bool doubleClicked = ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);
            bool mouseDown = ImGui.IsMouseDown(ImGuiMouseButton.Left);

            if (doubleClicked && isHovered)
            {
                ImGui.SetWindowFocus();
                state.Active = true;
                state.DragSelecting = false;
                SelectWordOrTagUnderMouse(text, lineStarts, start, lineHeight, textOffset, ImGui.GetMousePos(), state);
                ResetBlink(state);
            }
            else if (clicked)
            {
                ImGui.SetWindowFocus();
                state.Active = true;
                state.Cursor = HitTest(text, lineStarts, start, lineHeight, textOffset, ImGui.GetMousePos());
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
                    state.Cursor = HitTest(text, lineStarts, start, lineHeight, textOffset, ImGui.GetMousePos());
                    ResetBlink(state);
                }
                else
                {
                    if (!HasSelection(state)) ClearSelection(state);
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
                gutterWidth = Math.Max(42f, ImGui.CalcTextSize((lineStarts.Count + 1).ToString()).X + 20f);
                textOffset = gutterWidth + 12f;
                contentWidth = Math.Max(ImGui.GetContentRegionAvail().X, GetContentWidth(text, lineStarts, textOffset) + 24f);
                totalHeight = Math.Max(lineHeight, lineStarts.Count * lineHeight + 8f);
            }

            if (state.Active)
            {
                CursorToLineColumn(text, lineStarts, state.Cursor, out int imeLine, out int imeColumn);
                int imeLineStart = lineStarts[Math.Max(0, Math.Min(imeLine, lineStarts.Count - 1))];
                int imeLineEnd = GetLineEndByIndex(text, lineStarts, imeLine);
                string imeLineText = text.Substring(imeLineStart, Math.Max(0, imeLineEnd - imeLineStart));
                float imeX = start.X + textOffset + GetColumnXOffset(imeLineText, Math.Min(imeColumn, imeLineText.Length));
                float imeY = start.Y + imeLine * lineHeight;
                UpdateImePosition(imeX, imeY, lineHeight);
            }

            DrawEditor(text, state, lineStarts, start, lineHeight, gutterWidth, textOffset, contentWidth, totalHeight, state.Active);

            ImGui.EndChild();
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(2);

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

        public static void InsertAtCursor(string id, ref string text, string insert, bool placeInsideTag = false)
        {
            if (text == null) text = string.Empty;
            if (string.IsNullOrEmpty(insert)) return;

            EditorState state = GetState(id);
            PushUndo(state, text);
            DeleteSelection(ref text, state);

            int pos = Clamp(state.Cursor, 0, text.Length);
            text = text.Insert(pos, insert);

            if (placeInsideTag)
            {
                int closeAngle = insert.IndexOf('>');
                if (closeAngle >= 0 && closeAngle < insert.Length - 1)
                {
                    state.Cursor = pos + closeAngle + 1;
                }
                else
                {
                    state.Cursor = pos + insert.Length;
                }
            }
            else
            {
                state.Cursor = pos + insert.Length;
            }

            ClearSelection(state);
            ResetBlink(state);
            state.LastTextLength = text.Length;
            state.LastCommittedText = text;
        }

        public static void Clear(string id, ref string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            EditorState state = GetState(id);
            PushUndo(state, text);
            text = string.Empty;
            state.Cursor = 0;
            ClearSelection(state);
            ResetBlink(state);
            state.LastTextLength = 0;
            state.LastCommittedText = text;
        }

        public static bool Undo(string id, ref string text)
        {
            EditorState state = GetState(id);
            return PerformUndo(ref text, state);
        }

        public static bool Redo(string id, ref string text)
        {
            EditorState state = GetState(id);
            return PerformRedo(ref text, state);
        }

        public static bool CanUndo(string id)
        {
            EditorState state = GetState(id);
            return state.UndoStack.Count > 0;
        }

        public static bool CanRedo(string id)
        {
            EditorState state = GetState(id);
            return state.RedoStack.Count > 0;
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

        private static void PushUndo(EditorState state, string text)
        {
            if (text == null) text = string.Empty;
            if (state.UndoStack.Count > 0 && state.UndoStack[state.UndoStack.Count - 1] == text) return;
            state.UndoStack.Add(text);
            if (state.UndoStack.Count > MaxUndoSteps)
            {
                state.UndoStack.RemoveAt(0);
            }
            state.RedoStack.Clear();
        }

        private static bool PerformUndo(ref string text, EditorState state)
        {
            if (state.UndoStack.Count == 0) return false;
            state.RedoStack.Add(text ?? string.Empty);
            text = state.UndoStack[state.UndoStack.Count - 1];
            state.UndoStack.RemoveAt(state.UndoStack.Count - 1);
            state.Cursor = Clamp(state.Cursor, 0, text.Length);
            ClearSelection(state);
            ResetBlink(state);
            state.LastTextLength = text.Length;
            state.LastCommittedText = text;
            return true;
        }

        private static bool PerformRedo(ref string text, EditorState state)
        {
            if (state.RedoStack.Count == 0) return false;
            state.UndoStack.Add(text ?? string.Empty);
            text = state.RedoStack[state.RedoStack.Count - 1];
            state.RedoStack.RemoveAt(state.RedoStack.Count - 1);
            state.Cursor = Clamp(state.Cursor, 0, text.Length);
            ClearSelection(state);
            ResetBlink(state);
            state.LastTextLength = text.Length;
            state.LastCommittedText = text;
            return true;
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
                if (!string.IsNullOrEmpty(selected)) GUIUtility.systemCopyBuffer = selected;
                return false;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.X))
            {
                string selected = GetSelectedText(text, state);
                if (!string.IsNullOrEmpty(selected))
                {
                    GUIUtility.systemCopyBuffer = selected;
                    PushUndo(state, text);
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
                    PushUndo(state, text);
                    InsertText(ref text, state, clip.Replace("\r\n", "\n").Replace('\r', '\n'));
                    changed = true;
                }
                return changed;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.Z))
            {
                if (shift)
                {
                    return PerformRedo(ref text, state);
                }
                return PerformUndo(ref text, state);
            }

            if (ctrl && Input.GetKeyDown(KeyCode.Y))
            {
                return PerformRedo(ref text, state);
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
                    PushUndo(state, text);
                    DeleteSelection(ref text, state);
                    changed = true;
                }
                else if (state.Cursor > 0)
                {
                    PushUndo(state, text);
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
                    PushUndo(state, text);
                    DeleteSelection(ref text, state);
                    changed = true;
                }
                else if (state.Cursor < text.Length)
                {
                    PushUndo(state, text);
                    text = text.Remove(state.Cursor, 1);
                    changed = true;
                }
                ResetBlink(state);
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                PushUndo(state, text);
                InsertText(ref text, state, "    ");
                changed = true;
            }

            if (!ctrl && !string.IsNullOrEmpty(Input.inputString))
            {
                double now = ImGui.GetTime();
                if (now - state.LastKeystrokeTime > 0.8)
                {
                    PushUndo(state, text);
                }
                state.LastKeystrokeTime = now;

                foreach (char c in Input.inputString)
                {
                    if (c == '\b') continue;
                    if (c == '\r' || c == '\n')
                    {
                        PushUndo(state, text);
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

        private static void DrawEditor(string text, EditorState state, List<int> lineStarts, NumVector2 start, float lineHeight,
            float gutterWidth, float textOffset, float contentWidth, float totalHeight, bool active)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            float fontSize = ImGui.GetFontSize();

            // 调色板：透气、现代的暗色半透玻璃层级
            uint defaultColor = ImGui.GetColorU32(SonnetPalette.Text);
            uint gutterBgColor = ImGui.GetColorU32(new NumVector4(0.005f, 0.008f, 0.014f, 0.50f));
            uint gutterNumColor = ImGui.GetColorU32(new NumVector4(0.42f, 0.48f, 0.58f, 0.80f));
            uint activeLineNumColor = ImGui.GetColorU32(SonnetPalette.Accent(1f));
            uint gutterSepColor = ImGui.GetColorU32(new NumVector4(1f, 1f, 1f, 0.09f));
            uint selectionColor = ImGui.GetColorU32(new NumVector4(0.18f, 0.46f, 0.88f, 0.45f));
            uint cursorColor = ImGui.GetColorU32(SonnetPalette.Text);
            uint currentLineBgColor = ImGui.GetColorU32(SonnetPalette.Accent(0.06f));
            uint knownTagColor = ImGui.GetColorU32(SonnetPalette.Accent(1f));
            uint unknownTagColor = ImGui.GetColorU32(new NumVector4(1f, 0.42f, 0.42f, 1f));
            uint richTagColor = ImGui.GetColorU32(new NumVector4(0.82f, 0.58f, 1f, 1f));

            int selStart = 0;
            int selEnd = 0;
            bool hasSelection = GetSelectionRange(state, out selStart, out selEnd);

            CursorToLineColumn(text, lineStarts, state.Cursor, out int activeCursorLine, out int activeCursorCol);

            // 1. 绘制行号栏底色
            drawList.AddRectFilled(start, start + new NumVector2(gutterWidth, totalHeight), gutterBgColor);

            // 2. 绘制行号与代码之间的垂直分割线
            float gutterDividerX = start.X + gutterWidth;
            drawList.AddLine(new NumVector2(gutterDividerX, start.Y),
                new NumVector2(gutterDividerX, start.Y + totalHeight),
                gutterSepColor, 1f);

            // 3. 逐行绘制
            for (int line = 0; line < lineStarts.Count; line++)
            {
                int lineStart = lineStarts[line];
                int lineEnd = GetLineEndByIndex(text, lineStarts, line);
                string lineText = text.Substring(lineStart, lineEnd - lineStart);
                NumVector2 pos = start + new NumVector2(0f, line * lineHeight);

                // 当前激活行高亮
                if (active && line == activeCursorLine)
                {
                    drawList.AddRectFilled(
                        new NumVector2(gutterDividerX + 1f, pos.Y),
                        new NumVector2(start.X + contentWidth, pos.Y + lineHeight),
                        currentLineBgColor);
                }

                // 行号
                string lineNumStr = (line + 1).ToString();
                float lineNumWidth = ImGui.CalcTextSize(lineNumStr).X;
                NumVector2 lineNumPos = new NumVector2(gutterDividerX - 10f - lineNumWidth, pos.Y + 2f);
                uint currentLineColor = (active && line == activeCursorLine) ? activeLineNumColor : gutterNumColor;
                drawList.AddText(ImGui.GetFont(), fontSize, lineNumPos, currentLineColor, lineNumStr);

                // 代码文本起始位置（与行号分割线保持良好呼吸间距）
                NumVector2 textPos = pos + new NumVector2(textOffset, 2f);

                // 选中区域背景
                if (hasSelection && selEnd > lineStart && selStart < lineEnd + 1)
                {
                    int localStart = Clamp(selStart - lineStart, 0, lineText.Length);
                    int localEnd = Clamp(selEnd - lineStart, 0, lineText.Length);
                    float x1 = textPos.X + GetColumnXOffset(lineText, localStart);
                    float x2 = textPos.X + GetColumnXOffset(lineText, localEnd);
                    drawList.AddRectFilled(new NumVector2(x1, textPos.Y - 1f), new NumVector2(Math.Max(x2, x1 + 2f), textPos.Y + lineHeight - 1f), selectionColor);
                }

                // 语法高亮代码绘制
                DrawHighlightedLine(drawList, lineText, textPos, fontSize, defaultColor, knownTagColor, unknownTagColor, richTagColor);
            }

            // 4. 光标渲染
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
                    float cursorX = start.X + textOffset + GetColumnXOffset(lineText, Math.Min(cursorColumn, lineText.Length));
                    float cursorY = start.Y + cursorLine * lineHeight + 2f;
                    drawList.AddLine(new NumVector2(cursorX, cursorY - 1f), new NumVector2(cursorX, cursorY + lineHeight - 1f), cursorColor, 1.8f);
                }
            }
        }

        private static void DrawHighlightedLine(ImDrawListPtr drawList, string lineText, NumVector2 pos, float fontSize,
            uint defaultColor, uint knownTagColor, uint unknownTagColor, uint richTagColor)
        {
            List<HighlightSegment> segments = BuildHighlights(lineText, defaultColor, knownTagColor, unknownTagColor, richTagColor);
            float x = pos.X;
            for (int i = 0; i < segments.Count; i++)
            {
                HighlightSegment segment = segments[i];
                if (segment.Length <= 0) continue;

                string part = lineText.Substring(segment.Start, segment.Length);
                drawList.AddText(ImGui.GetFont(), fontSize, new NumVector2(x, pos.Y), segment.Color, part);
                x += ImGui.CalcTextSize(part).X;
            }
        }

        private static List<HighlightSegment> BuildHighlights(string lineText, uint defaultColor,
            uint knownTagColor, uint unknownTagColor, uint richTagColor)
        {
            var marks = new List<HighlightSegment>();
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
            for (int i = 0; i < marks.Count; i++)
            {
                HighlightSegment mark = marks[i];
                if (mark.Start < cursor) continue;
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
            if (KnownTags.Contains(tag)) return true;

            int colon = tag.IndexOf(':');
            if (colon > 1 && tag.EndsWith("}", StringComparison.Ordinal))
            {
                string baseTag = tag.Substring(0, colon) + "}";
                return KnownTags.Contains(baseTag);
            }
            return false;
        }

        private static int HitTest(string text, List<int> lineStarts, NumVector2 start, float lineHeight, float textOffset, NumVector2 mouse)
        {
            if (string.IsNullOrEmpty(text) || lineStarts == null || lineStarts.Count == 0) return 0;
            int line = Clamp((int)Math.Floor((mouse.Y - start.Y) / Math.Max(1f, lineHeight)), 0, lineStarts.Count - 1);
            int lineStart = lineStarts[line];
            int lineEnd = GetLineEndByIndex(text, lineStarts, line);
            string lineText = text.Substring(lineStart, lineEnd - lineStart);
            float targetX = mouse.X - (start.X + textOffset);
            if (targetX <= 0f || lineText.Length == 0) return lineStart;

            List<HighlightSegment> segments = BuildHighlights(lineText, 0, 0, 0, 0);
            float currentX = 0f;
            for (int s = 0; s < segments.Count; s++)
            {
                HighlightSegment seg = segments[s];
                if (seg.Length <= 0) continue;

                string segText = lineText.Substring(seg.Start, seg.Length);
                float segWidth = ImGui.CalcTextSize(segText).X;

                if (targetX >= currentX + segWidth && s < segments.Count - 1)
                {
                    currentX += segWidth;
                    continue;
                }

                float prevCharX = currentX;
                for (int i = 1; i <= seg.Length; i++)
                {
                    float nextCharX = currentX + ImGui.CalcTextSize(segText.Substring(0, i)).X;
                    float midX = (prevCharX + nextCharX) * 0.5f;
                    if (targetX < midX)
                    {
                        return lineStart + seg.Start + (i - 1);
                    }
                    prevCharX = nextCharX;
                }

                if (s == segments.Count - 1 || targetX < currentX + segWidth)
                {
                    return lineStart + seg.Start + seg.Length;
                }
                currentX += segWidth;
            }

            return lineEnd;
        }

        private static float GetColumnXOffset(string lineText, int column)
        {
            if (string.IsNullOrEmpty(lineText) || column <= 0) return 0f;
            column = Clamp(column, 0, lineText.Length);
            List<HighlightSegment> segments = BuildHighlights(lineText, 0, 0, 0, 0);
            float offset = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                HighlightSegment seg = segments[i];
                if (seg.Length <= 0) continue;
                if (column >= seg.Start + seg.Length)
                {
                    string part = lineText.Substring(seg.Start, seg.Length);
                    offset += ImGui.CalcTextSize(part).X;
                }
                else if (column > seg.Start)
                {
                    string part = lineText.Substring(seg.Start, column - seg.Start);
                    offset += ImGui.CalcTextSize(part).X;
                    break;
                }
                else
                {
                    break;
                }
            }
            return offset;
        }

        private static void SelectWordOrTagUnderMouse(string text, List<int> lineStarts, NumVector2 start, float lineHeight, float textOffset, NumVector2 mouse, EditorState state)
        {
            int hit = HitTest(text, lineStarts, start, lineHeight, textOffset, mouse);
            hit = Clamp(hit, 0, text.Length);

            if (hit < text.Length && text[hit] == '{')
            {
                int close = text.IndexOf('}', hit);
                if (close >= 0 && text.IndexOf('\n', hit, close - hit) < 0)
                {
                    state.SelectionAnchor = hit;
                    state.Cursor = close + 1;
                    return;
                }
            }

            int lastOpenBrace = text.LastIndexOf('{', Math.Max(0, hit - 1));
            int nextCloseBrace = text.IndexOf('}', hit);
            if (lastOpenBrace >= 0 && nextCloseBrace > lastOpenBrace)
            {
                int lineBreak = text.IndexOf('\n', lastOpenBrace, nextCloseBrace - lastOpenBrace);
                if (lineBreak < 0)
                {
                    state.SelectionAnchor = lastOpenBrace;
                    state.Cursor = nextCloseBrace + 1;
                    return;
                }
            }

            if (hit < text.Length && text[hit] == '<')
            {
                int close = text.IndexOf('>', hit);
                if (close >= 0 && text.IndexOf('\n', hit, close - hit) < 0)
                {
                    state.SelectionAnchor = hit;
                    state.Cursor = close + 1;
                    return;
                }
            }

            int lastOpenAngle = text.LastIndexOf('<', Math.Max(0, hit - 1));
            int nextCloseAngle = text.IndexOf('>', hit);
            if (lastOpenAngle >= 0 && nextCloseAngle > lastOpenAngle)
            {
                int lineBreak = text.IndexOf('\n', lastOpenAngle, nextCloseAngle - lastOpenAngle);
                if (lineBreak < 0)
                {
                    state.SelectionAnchor = lastOpenAngle;
                    state.Cursor = nextCloseAngle + 1;
                    return;
                }
            }

            int startWord = hit;
            while (startWord > 0 && IsWordChar(text[startWord - 1])) startWord--;
            int endWord = hit;
            while (endWord < text.Length && IsWordChar(text[endWord])) endWord++;

            if (endWord > startWord)
            {
                state.SelectionAnchor = startWord;
                state.Cursor = endWord;
            }
            else
            {
                state.SelectionAnchor = hit;
                state.Cursor = hit;
            }
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '$' || c == '@';
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
            if (!GetSelectionRange(state, out int start, out int end)) return;

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
            if (!GetSelectionRange(state, out int start, out int end)) return string.Empty;

            start = Clamp(start, 0, text.Length);
            end = Clamp(end, 0, text.Length);
            return end > start ? text.Substring(start, end - start) : string.Empty;
        }

        private static void MoveCursor(string text, EditorState state, int target, bool keepSelection)
        {
            target = Clamp(target, 0, text.Length);
            if (keepSelection)
            {
                if (state.SelectionAnchor < 0) state.SelectionAnchor = state.Cursor;
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
            if (index >= 0)
            {
                if (index > 0 && text[index - 1] == '\r') return index - 1;
                return index;
            }
            return text.Length;
        }

        private static List<int> BuildLineStarts(string text)
        {
            var starts = new List<int> { 0 };
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') starts.Add(i + 1);
            }
            return starts;
        }

        private static int GetLineEndByIndex(string text, List<int> lineStarts, int line)
        {
            if (line < 0 || line >= lineStarts.Count) return text.Length;
            int nextStart = line + 1 < lineStarts.Count ? lineStarts[line + 1] : text.Length;
            int end = nextStart;
            if (end > lineStarts[line] && end <= text.Length && text[end - 1] == '\n') end--;
            if (end > lineStarts[line] && end <= text.Length && text[end - 1] == '\r') end--;
            return end;
        }

        private static float CalcWidth(string text, int start, int end)
        {
            start = Clamp(start, 0, text.Length);
            end = Clamp(end, start, text.Length);
            if (end <= start) return 0f;
            string part = text.Substring(start, end - start);
            return GetColumnXOffset(part, part.Length);
        }

        private static float GetContentWidth(string text, List<int> lineStarts, float textOffset)
        {
            float width = 64f;
            for (int i = 0; i < lineStarts.Count; i++)
            {
                int lineStart = lineStarts[i];
                int lineEnd = GetLineEndByIndex(text, lineStarts, i);
                string lineText = text.Substring(lineStart, lineEnd - lineStart);
                width = Math.Max(width, textOffset + GetColumnXOffset(lineText, lineText.Length));
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

        private static void UpdateImePosition(float x, float y, float lineHeight)
        {
            try
            {
                Input.imeCompositionMode = IMECompositionMode.On;
                float displayWidth = ImGui.GetIO().DisplaySize.X;
                float scale = displayWidth > 1f ? Screen.width / displayWidth : 1f;
                float screenX = Mathf.Clamp(x * scale, 0f, Math.Max(0f, Screen.width - 1f));
                float screenY = Mathf.Clamp((y + lineHeight) * scale, 0f, Math.Max(0f, Screen.height - 1f));
                Input.compositionCursorPos = new UnityEngine.Vector2(screenX, screenY);
            }
            catch
            {
            }
        }
    }
}
