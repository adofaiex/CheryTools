using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CheryTools.ReadHelper.Preview
{
    public sealed class ReadHelperGraphic : MaskableGraphic
    {
        private readonly List<TimelineFloorNote> _activeNotes = new List<TimelineFloorNote>(128);
        private double _currentTime;
        private ReadHelperSettings _settings;

        public void UpdateRenderData(List<TimelineFloorNote> notes, double currentTime, ReadHelperSettings settings)
        {
            _activeNotes.Clear();
            if (notes != null) _activeNotes.AddRange(notes);
            _currentTime = currentTime;
            _settings = settings;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_settings == null || !_settings.Enabled) return;

            float trackHalfWidth = _settings.TrackWidth * 0.5f;
            float noteHeight = _settings.NoteHeight;
            float noteWidth = _settings.NoteWidth;
            float doubleTapWidth = _settings.DoubleTapWidth;
            float doubleTapBorder = _settings.DoubleTapBorderThickness;
            float lookAhead = Mathf.Max(0.1f, _settings.LookAheadSeconds);
            float fadePower = Mathf.Max(0.1f, _settings.FadePower);

            Color targetBoxColor = ToColor(_settings.TargetBoxColor);
            Color targetLineColor = ToColor(_settings.TargetLineColor);
            Color normalNoteColor = ToColor(_settings.NormalNoteColor);
            Color doubleTapFillColor = ToColor(_settings.DoubleTapFillColor);
            Color doubleTapBorderColor = ToColor(_settings.DoubleTapBorderColor);
            Color decelColor = ToColor(_settings.DecelColor);
            Color accelColor = ToColor(_settings.AccelColor);

            // 1. 绘制中央固定判定框与判定线 (x = 0)
            float boxHalfW = _settings.TargetBoxWidth * 0.5f;
            float boxHalfH = _settings.TargetBoxHeight * 0.5f;
            AddHollowRect(vh, -boxHalfW, -boxHalfH, boxHalfW, boxHalfH, 2f, targetBoxColor);

            float lineHalfW = _settings.TargetLineWidth * 0.5f;
            float lineHalfH = boxHalfH - 4f;
            AddQuad(vh, -lineHalfW, -lineHalfH, lineHalfW, lineHalfH, targetLineColor);

            // 2. 绘制流动音符
            for (int i = 0; i < _activeNotes.Count; i++)
            {
                TimelineFloorNote note = _activeNotes[i];
                double dt = note.HitTime - _currentTime;
                float u = (float)(dt / lookAhead);

                // 允许微小的穿过判定线停留感 (-0.05 ~ 1.0)
                if (u < -0.06f || u > 1.0f) continue;

                float clampedU = Mathf.Clamp01(Mathf.Max(0f, u));
                float alpha = Mathf.Pow(1f - clampedU, fadePower);
                if (alpha <= 0.001f) continue;

                if (note.IsDoubleTap)
                {
                    // 左侧轨道：从左往右流向中间 (x < 0)
                    float x = -clampedU * trackHalfWidth;
                    float halfW = doubleTapWidth * 0.5f;
                    float halfH = noteHeight * 0.5f;

                    Color border = doubleTapBorderColor;
                    border.a *= alpha;
                    Color fill = doubleTapFillColor;
                    fill.a *= alpha;

                    // 外圈粉色描边
                    AddHollowRect(vh, x - halfW, -halfH, x + halfW, halfH, doubleTapBorder, border);
                    // 内部纯白填充
                    AddQuad(vh, x - halfW + doubleTapBorder, -halfH + doubleTapBorder,
                               x + halfW - doubleTapBorder, halfH - doubleTapBorder, fill);
                }
                else
                {
                    // 右侧轨道：从右往左流向中间 (x > 0)
                    float x = clampedU * trackHalfWidth;
                    float w = note.IsSpeedChange ? (noteWidth + 1f) : noteWidth;
                    float halfW = w * 0.5f;
                    float halfH = noteHeight * 0.5f;

                    Color noteColor = normalNoteColor;
                    if (note.IsSpeedChange)
                    {
                        noteColor = note.IsDecel ? decelColor : accelColor;
                    }
                    noteColor.a *= alpha;

                    AddQuad(vh, x - halfW, -halfH, x + halfW, halfH, noteColor);
                }
            }
        }

        private static void AddQuad(VertexHelper vh, float xMin, float yMin, float xMax, float yMax, Color color)
        {
            if (color.a <= 0.001f) return;
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(xMin, yMin), color, Vector2.zero);
            vh.AddVert(new Vector3(xMin, yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(xMax, yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(xMax, yMin), color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddHollowRect(VertexHelper vh, float xMin, float yMin, float xMax, float yMax, float border, Color color)
        {
            if (color.a <= 0.001f || border <= 0.001f) return;
            // 顶
            AddQuad(vh, xMin, yMax - border, xMax, yMax, color);
            // 底
            AddQuad(vh, xMin, yMin, xMax, yMin + border, color);
            // 左
            AddQuad(vh, xMin, yMin + border, xMin + border, yMax - border, color);
            // 右
            AddQuad(vh, xMax - border, yMin + border, xMax, yMax - border, color);
        }

        private static Color ToColor(float[] c)
        {
            if (c == null || c.Length < 4) return Color.white;
            return new Color(c[0], c[1], c[2], c[3]);
        }
    }
}
