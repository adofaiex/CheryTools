using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CheryTools.ReadHelper.Preview
{
    [DefaultExecutionOrder(31500)]
    public sealed class ReadHelperHost : MonoBehaviour
    {
        private RectTransform _container;
        private ReadHelperGraphic _graphic;
        private readonly List<Text> _textPool = new List<Text>(16);
        private readonly ReadHelperDataManager _dataManager = new ReadHelperDataManager();
        private readonly List<TimelineFloorNote> _visibleNotes = new List<TimelineFloorNote>(128);

        private Font _font;

        public void Initialize(RectTransform parentRoot, string frameworkDir)
        {
            // 创建 HUD 主容器
            var containerObj = new GameObject("ReadHelper_Container", typeof(RectTransform));
            containerObj.transform.SetParent(parentRoot, false);
            _container = containerObj.GetComponent<RectTransform>();
            _container.anchorMin = new Vector2(0.5f, 0.5f);
            _container.anchorMax = new Vector2(0.5f, 0.5f);
            _container.pivot = new Vector2(0.5f, 0.5f);

            // 创建绘图组件
            var graphicObj = new GameObject("ReadHelper_Graphic", typeof(RectTransform), typeof(ReadHelperGraphic));
            graphicObj.transform.SetParent(_container, false);
            var graphicRt = graphicObj.GetComponent<RectTransform>();
            graphicRt.anchorMin = Vector2.zero;
            graphicRt.anchorMax = Vector2.one;
            graphicRt.offsetMin = Vector2.zero;
            graphicRt.offsetMax = Vector2.zero;
            _graphic = graphicObj.GetComponent<ReadHelperGraphic>();
            _graphic.raycastTarget = false;

            // 加载字体
            _font = ResolveFont(frameworkDir);
        }

        private static Font ResolveFont(string frameworkDir)
        {
            try
            {
                if (!string.IsNullOrEmpty(frameworkDir))
                {
                    string miSans = System.IO.Path.Combine(frameworkDir, "Resources", "MiSans-Bold.ttf");
                    if (System.IO.File.Exists(miSans))
                    {
                        var created = new Font(miSans);
                        if (created != null) return created;
                    }
                }
            }
            catch { }
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private Text GetOrCreateText(int index)
        {
            while (_textPool.Count <= index)
            {
                var textObj = new GameObject("Label_" + _textPool.Count, typeof(RectTransform), typeof(Text));
                textObj.transform.SetParent(_container, false);
                var rt = textObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0f); // 底部居中对齐，便于放在方块顶部
                rt.sizeDelta = new Vector2(240f, 40f);

                var txt = textObj.GetComponent<Text>();
                txt.font = _font;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.raycastTarget = false;
                _textPool.Add(txt);
            }
            return _textPool[index];
        }

        private void Update()
        {
            ReadHelperSettings settings = ReadHelperRuntime.Settings;
            if (settings == null || !settings.Enabled)
            {
                if (_container != null && _container.gameObject.activeSelf)
                    _container.gameObject.SetActive(false);
                return;
            }

            // 判断当前运行场景与游玩状态
            scrController controller = scrController.instance;
            bool inGame = controller != null && controller.gameworld;
            bool inEditor = ADOBase.isLevelEditor || scnEditor.instance != null;

            if (settings.ShowInGameOnly && !inGame && (!settings.ShowInEditor || !inEditor))
            {
                if (_container != null && _container.gameObject.activeSelf)
                    _container.gameObject.SetActive(false);
                return;
            }

            if (settings.HideWhenPaused && controller != null && controller.paused)
            {
                if (_container != null && _container.gameObject.activeSelf)
                    _container.gameObject.SetActive(false);
                return;
            }

            if (_container != null && !_container.gameObject.activeSelf)
                _container.gameObject.SetActive(true);

            // 更新容器位置与缩放
            _container.anchoredPosition = new Vector2(settings.PositionX, settings.PositionY);
            _container.localScale = Vector3.one * settings.Scale;

            // 检查关卡数据
            _dataManager.CheckAndRefresh(settings);

            // 读取音频时间
            double currentTime = 0d;
            if (scrConductor.instance != null)
            {
                currentTime = scrConductor.instance.songposition_minusi;
            }

            // 获取视野范围内的音符
            _dataManager.GetVisibleNotes(currentTime, settings.LookAheadSeconds, _visibleNotes);

            // 渲染网格线条与判定框
            _graphic.UpdateRenderData(_visibleNotes, currentTime, settings);

            // 渲染速度变化文字标签
            int textIndex = 0;
            if (settings.ShowSpeedLabels)
            {
                float trackHalfWidth = settings.TrackWidth * 0.5f;
                float noteHeight = settings.NoteHeight;
                float lookAhead = Mathf.Max(0.1f, settings.LookAheadSeconds);
                float fadePower = Mathf.Max(0.1f, settings.FadePower);

                Color decelColor = ToColor(settings.DecelColor);
                Color accelColor = ToColor(settings.AccelColor);

                for (int i = 0; i < _visibleNotes.Count; i++)
                {
                    TimelineFloorNote note = _visibleNotes[i];
                    if (!note.IsSpeedChange || string.IsNullOrEmpty(note.LabelText) || note.IsDoubleTap)
                        continue;

                    double dt = note.HitTime - currentTime;
                    float u = (float)(dt / lookAhead);
                    if (u < -0.05f || u > 1.0f) continue;

                    float clampedU = Mathf.Clamp01(Mathf.Max(0f, u));
                    float alpha = Mathf.Pow(1f - clampedU, fadePower);
                    if (alpha <= 0.01f) continue;

                    float x = clampedU * trackHalfWidth;
                    float y = noteHeight * 0.5f + settings.LabelOffsetY;

                    Text txt = GetOrCreateText(textIndex++);
                    txt.rectTransform.anchoredPosition = new Vector2(x, y);
                    txt.fontSize = settings.FontSize;
                    txt.text = note.LabelText;

                    Color c = note.IsDecel ? decelColor : accelColor;
                    c.a *= alpha;
                    txt.color = c;
                    txt.gameObject.SetActive(true);
                }
            }

            // 隐藏多余的标签
            for (int i = textIndex; i < _textPool.Count; i++)
            {
                if (_textPool[i].gameObject.activeSelf)
                    _textPool[i].gameObject.SetActive(false);
            }
        }

        private static Color ToColor(float[] c)
        {
            if (c == null || c.Length < 4) return Color.white;
            return new Color(c[0], c[1], c[2], c[3]);
        }

        private void OnDestroy()
        {
            _textPool.Clear();
            _visibleNotes.Clear();
        }
    }
}
