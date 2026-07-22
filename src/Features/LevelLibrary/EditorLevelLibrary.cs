using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ADOFAI;
using ADOFAI.LevelEditor.Controls;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CheryTools
{
    [Serializable]
    public sealed class EditorLevelLibraryEntry
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string FilePath = string.Empty;
        public string LevelName = string.Empty;
        public string LevelAuthor = string.Empty;
        public string Artist = string.Empty;
        public string SongName = string.Empty;
        public string Notes = string.Empty;
        public string RatingCategory = string.Empty;
        public int RatingLevel = 1;
        public bool RatingIsJ;
        public bool Starred;
        public long AddedUtcTicks = DateTime.UtcNow.Ticks;
        public long LastOpenedUtcTicks;
    }

    internal static class EditorLevelLibraryStore
    {
        internal static void NormalizeEntries(List<EditorLevelLibraryEntry> entries)
        {
            if (entries == null) return;

            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                EditorLevelLibraryEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.FilePath))
                {
                    entries.RemoveAt(i);
                    continue;
                }

                entry.FilePath = NormalizePath(entry.FilePath);
                if (!paths.Add(entry.FilePath))
                {
                    entries.RemoveAt(i);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.Id)) entry.Id = Guid.NewGuid().ToString("N");
                if (string.IsNullOrWhiteSpace(entry.LevelName)) entry.LevelName = Path.GetFileNameWithoutExtension(entry.FilePath);
                if (entry.LevelAuthor == null) entry.LevelAuthor = string.Empty;
                if (entry.Artist == null) entry.Artist = string.Empty;
                if (entry.SongName == null) entry.SongName = string.Empty;
                if (entry.Notes == null) entry.Notes = string.Empty;
                entry.RatingCategory = NormalizeRatingCategory(entry.RatingCategory);
                entry.RatingLevel = Math.Max(1, Math.Min(20, entry.RatingLevel));
                if (entry.RatingCategory != "U") entry.RatingIsJ = false;
                if (entry.AddedUtcTicks <= 0) entry.AddedUtcTicks = DateTime.UtcNow.Ticks;
            }
        }

        internal static string NormalizeRatingCategory(string value)
        {
            value = (value ?? string.Empty).Trim().ToUpperInvariant();
            return value == "P" || value == "G" || value == "U" ? value : string.Empty;
        }

        internal static EditorLevelLibraryEntry Add(string path)
        {
            if (Main.Settings == null || string.IsNullOrWhiteSpace(path)) return null;
            path = NormalizePath(path);
            if (!File.Exists(path) || !string.Equals(Path.GetExtension(path), ".adofai", StringComparison.OrdinalIgnoreCase)) return null;

            EditorLevelLibraryEntry existing = Main.Settings.EditorLevelLibraryEntries
                .FirstOrDefault(x => x != null && string.Equals(NormalizePath(x.FilePath), path, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;

            EditorLevelLibraryEntry entry = new EditorLevelLibraryEntry
            {
                FilePath = path,
                LevelName = Path.GetFileNameWithoutExtension(path),
                AddedUtcTicks = DateTime.UtcNow.Ticks
            };
            ReadLevelMetadata(entry);
            Main.Settings.EditorLevelLibraryEntries.Add(entry);
            Save();
            return entry;
        }

        internal static void Remove(EditorLevelLibraryEntry entry)
        {
            if (entry == null || Main.Settings == null) return;
            Main.Settings.EditorLevelLibraryEntries.Remove(entry);
            Save();
        }

        internal static void Save()
        {
            if (Main.Settings == null || Main.ModEntry == null) return;
            NormalizeEntries(Main.Settings.EditorLevelLibraryEntries);
            Main.Settings.Save(Main.ModEntry);
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return path == null ? string.Empty : path.Trim();
            }
        }

        private static void ReadLevelMetadata(EditorLevelLibraryEntry entry)
        {
            try
            {
                string json = File.ReadAllText(entry.FilePath);
                JObject root = JObject.Parse(json);
                JObject settings = root["settings"] as JObject;
                if (settings == null) return;

                string artist = settings.Value<string>("artist");
                string song = settings.Value<string>("song");
                string author = settings.Value<string>("author");
                if (!string.IsNullOrWhiteSpace(author)) entry.LevelAuthor = author;
                if (!string.IsNullOrWhiteSpace(artist)) entry.Artist = artist;
                if (!string.IsNullOrWhiteSpace(song)) entry.SongName = song;
            }
            catch (Exception ex)
            {
                Main.Logger?.Log("[CheryTools] Failed to read level library metadata: " + ex.Message);
            }
        }
    }

    internal sealed class EditorLevelLibraryPanel : MonoBehaviour
    {
        private const float RowHeight = 40f;
        private static EditorLevelLibraryPanel _instance;

        private scnEditor _editor;
        private InspectorPanel _leftInspector;
        private InspectorPanel _rightInspector;
        private GameObject _leftRoot;
        private GameObject _rightRoot;
        private InspectorTab _customTab;
        private Button _customTabButton;
        private Image _customTabIcon;
        private TMP_InputField _searchField;
        private RectTransform _listContent;
        private TMP_Text _emptyText;
        private TMP_Text _statusText;
        private TMP_InputField _nameField;
        private TMP_InputField _authorField;
        private TMP_InputField _artistField;
        private TMP_InputField _songField;
        private TMP_InputField _notesField;
        private TMP_InputField _ratingCategoryField;
        private TMP_InputField _ratingLevelField;
        private TMP_InputField _ratingVariantField;
        private GameObject _ratingCategoryRoot;
        private GameObject _ratingLevelRoot;
        private GameObject _ratingVariantRoot;
        private Button _openButton;
        private EditorLevelLibraryEntry _selected;
        private bool _updatingRatingControls;
        private bool _libraryActive;
        private bool _rightButtonsCaptured;
        private bool _deleteButtonWasActive;
        private bool _disableButtonWasActive;
        private readonly List<GameObject> _rows = new List<GameObject>();
        private readonly List<EditorLevelLibraryEntry> _displayedEntries = new List<EditorLevelLibraryEntry>();

        internal static void Install(scnEditor editor)
        {
            if (editor == null || !Main.IsEnabled || Main.Settings == null || !Main.Settings.EnableEditorLevelLibrary) return;
            if (_instance != null) Destroy(_instance.gameObject);

            GameObject host = new GameObject("CheryTools_EditorLevelLibrary");
            host.transform.SetParent(editor.transform, false);
            _instance = host.AddComponent<EditorLevelLibraryPanel>();
            _instance.Initialize(editor);
        }

        internal static void RefreshFeatureState()
        {
            if (!Main.IsEnabled || Main.Settings == null || !Main.Settings.EnableEditorLevelLibrary)
            {
                if (_instance != null) Destroy(_instance.gameObject);
                return;
            }

            if (_instance != null) return;
            scnEditor editor = UnityEngine.Object.FindObjectOfType<scnEditor>();
            if (editor != null) Install(editor);
        }

        internal static void BeforeOfficialPanel(InspectorPanel panel)
        {
            if (_instance == null || panel == null) return;
            _instance.HandleOfficialPanel(panel);
        }

        private void Initialize(scnEditor editor)
        {
            _editor = editor;
            _leftInspector = editor.settingsPanel;
            _rightInspector = editor.levelEventsPanel;
            if (_leftInspector == null || _rightInspector == null || editor.propertyControlDecorationsList == null)
            {
                Destroy(gameObject);
                return;
            }

            CreateTab();
            CreateLeftPanel();
            CreateRightPanel();
            RefreshList();
        }

        private void OnDestroy()
        {
            if (_leftRoot != null) Destroy(_leftRoot);
            if (_rightRoot != null) Destroy(_rightRoot);
            if (_customTab != null) Destroy(_customTab.gameObject);
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (!Main.IsEnabled)
            {
                if (_leftRoot != null) _leftRoot.SetActive(false);
                if (_rightRoot != null) _rightRoot.SetActive(false);
            }
        }

        private void CreateTab()
        {
            InspectorTab decorationTab = _leftInspector.GetTabForEventType(LevelEventType.DecorationSettings);
            if (decorationTab == null) return;

            GameObject tabObject = Instantiate(decorationTab.gameObject, _leftInspector.tabs, false);
            tabObject.name = "CheryTools_LevelLibraryTab";
            _customTab = tabObject.GetComponent<InspectorTab>();
            _customTab.enabled = false;
            // Keep the component so the editor's tab enumeration remains valid,
            // but use a sentinel that can never collide with an official panel.
            _customTab.levelEventType = (LevelEventType)(-987654);
            _customTab.panel = _leftInspector;
            if (_customTab.cycleButtons != null) _customTab.cycleButtons.gameObject.SetActive(false);
            _customTabButton = _customTab.button;
            _customTabButton.onClick.RemoveAllListeners();
            _customTabButton.onClick.AddListener(ShowLibrary);
            _customTabButton.name = "CheryTools_LevelLibrary";
            _customTabIcon = _customTab.icon;
            _customTabIcon.sprite = _editor.fileIcon != null ? _editor.fileIcon.sprite : _editor.fileSpriteUp;

            float minY = 0f;
            bool found = false;
            foreach (Transform child in _leftInspector.tabs)
            {
                if (child == tabObject.transform) continue;
                RectTransform childRect = child as RectTransform;
                if (childRect == null) continue;
                if (!found || childRect.anchoredPosition.y < minY)
                {
                    minY = childRect.anchoredPosition.y;
                    found = true;
                }
            }
            RectTransform tabRect = tabObject.GetComponent<RectTransform>();
            tabRect.anchoredPosition = tabRect.anchoredPosition.WithY(minY - InspectorPanel.tabHeight);
            SetCustomTabSelected(false);
        }

        private void CreateLeftPanel()
        {
            PropertiesPanel decorationPanel = _leftInspector.panelsList.FirstOrDefault(x => x.levelEventType == LevelEventType.DecorationSettings);
            _leftRoot = CreatePanelRoot("CheryTools_LevelLibraryLeft", _leftInspector.panels, decorationPanel != null ? decorationPanel.GetComponent<RectTransform>() : null);

            TMP_InputField inputTemplate = _editor.propertyControlDecorationsList.searchField;
            _searchField = CloneInput(inputTemplate, _leftRoot.transform, "Search");
            SetRect(_searchField.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -57f), new Vector2(-18f, -17f));
            SetPlaceholder(_searchField, "搜索关卡....");
            _searchField.onValueChanged.AddListener(delegate { RefreshList(); });

            GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportObject.transform.SetParent(_leftRoot.transform, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            SetRect(viewport, Vector2.zero, Vector2.one, new Vector2(14f, 48f), new Vector2(-14f, -68f));
            viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            GameObject contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewport, false);
            _listContent = contentObject.GetComponent<RectTransform>();
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = new Vector2(1f, 1f);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.offsetMin = Vector2.zero;
            _listContent.offsetMax = Vector2.zero;

            ScrollRect scrollRect = _leftRoot.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = _listContent;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.inertia = true;
            scrollRect.scrollSensitivity = 34f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            _emptyText = CreateText("Empty", _leftRoot.transform, "尚未加入关卡。\n点击左下角的 + 添加 .adofai 文件。", 23f, TextAlignmentOptions.Center);
            SetRect(_emptyText.rectTransform, new Vector2(0f, 0.35f), new Vector2(1f, 0.65f), new Vector2(30f, 0f), new Vector2(-30f, 0f));
            _emptyText.color = new Color(1f, 1f, 1f, 0.55f);

            Button addButton = CreateTextButton("Add", _leftRoot.transform, "+", 22f, new Color(0f, 0f, 0f, 0f), Color.white);
            SetRect(addButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14f, 8f), new Vector2(52f, 44f));
            addButton.onClick.AddListener(AddLevel);
            _leftRoot.SetActive(false);
        }

        private void CreateRightPanel()
        {
            RectTransform sourceRect = _rightInspector.panelsList.Count > 0 ? _rightInspector.panelsList[0].GetComponent<RectTransform>() : null;
            _rightRoot = CreatePanelRoot("CheryTools_LevelLibraryRight", _rightInspector.panels, sourceRect);
            float top = -8f;
            _nameField = CreateLabeledInput("关卡名", "Name", top, false);
            top -= 76f;
            _authorField = CreateLabeledInput("谱面作者", "Author", top, false);
            top -= 76f;
            _artistField = CreateLabeledInput("曲师", "Artist", top, false);
            top -= 76f;
            _songField = CreateLabeledInput("曲名", "Song", top, false);
            top -= 76f;
            _notesField = CreateLabeledInput("备注", "Notes", top, true);

            _nameField.onEndEdit.AddListener(value => UpdateSelectedEntry(entry => entry.LevelName = CleanName(value, entry.FilePath)));
            _authorField.onEndEdit.AddListener(value => UpdateSelectedEntry(entry => entry.LevelAuthor = value ?? string.Empty));
            _artistField.onEndEdit.AddListener(value => UpdateSelectedEntry(entry => entry.Artist = value ?? string.Empty));
            _songField.onEndEdit.AddListener(value => UpdateSelectedEntry(entry => entry.SongName = value ?? string.Empty));
            _notesField.onEndEdit.AddListener(value => UpdateSelectedEntry(entry => entry.Notes = value ?? string.Empty));

            CreateRatingControls(-466f);

            Image separator = CreateImage("Separator", _rightRoot.transform, null);
            separator.preserveAspect = false;
            separator.color = new Color(1f, 1f, 1f, 0.45f);
            SetRect(separator.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -548f), new Vector2(-20f, -546f));

            _openButton = CreateTextButton("OpenLevel", _rightRoot.transform, "打开关卡", 19f, Color.white, Color.black);
            _openButton.image.sprite = GetSelectionBackgroundSprite();
            _openButton.image.type = Image.Type.Sliced;
            SetRect(_openButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -600f), new Vector2(-20f, -554f));
            _openButton.onClick.AddListener(OpenSelectedLevel);

            _statusText = CreateText("Status", _rightRoot.transform, string.Empty, 18f, TextAlignmentOptions.Center);
            SetRect(_statusText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -632f), new Vector2(-18f, -606f));
            _statusText.color = new Color(1f, 0.45f, 0.45f, 1f);
            _rightRoot.SetActive(false);
        }

        private void CreateRatingControls(float top)
        {
            TMP_Text label = CreateText("RatingLabel", _rightRoot.transform, "等级", 18f, TextAlignmentOptions.Left);
            SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, top - 24f), new Vector2(-20f, top));

            _ratingCategoryField = ClonePropertyInput(false, _rightRoot.transform, "RatingCategory");
            _ratingLevelField = ClonePropertyInput(false, _rightRoot.transform, "RatingLevel");
            _ratingVariantField = ClonePropertyInput(false, _rightRoot.transform, "RatingVariant");
            _ratingCategoryRoot = _ratingCategoryField.gameObject;
            _ratingLevelRoot = _ratingLevelField.gameObject;
            _ratingVariantRoot = _ratingVariantField.gameObject;
            SetPlaceholder(_ratingCategoryField, "P / G / U；留空为未设置");
            SetPlaceholder(_ratingLevelField, "1 - 20");
            SetPlaceholder(_ratingVariantField, "空白 / J");
            _ratingCategoryField.characterLimit = 8;
            _ratingLevelField.characterLimit = 2;
            _ratingLevelField.contentType = TMP_InputField.ContentType.IntegerNumber;
            _ratingVariantField.characterLimit = 1;

            _ratingCategoryField.onEndEdit.AddListener(value =>
            {
                if (_updatingRatingControls || _selected == null) return;
                _selected.RatingCategory = EditorLevelLibraryStore.NormalizeRatingCategory(value);
                if (_selected.RatingCategory != "U") _selected.RatingIsJ = false;
                SaveRatingAndRefresh();
            });
            _ratingLevelField.onEndEdit.AddListener(value =>
            {
                if (_updatingRatingControls || _selected == null) return;
                if (int.TryParse(value, out int level)) _selected.RatingLevel = Math.Max(1, Math.Min(20, level));
                SaveRatingAndRefresh();
            });
            _ratingVariantField.onEndEdit.AddListener(value =>
            {
                if (_updatingRatingControls || _selected == null) return;
                _selected.RatingIsJ = _selected.RatingCategory == "U"
                    && string.Equals((value ?? string.Empty).Trim(), "J", StringComparison.OrdinalIgnoreCase);
                SaveRatingAndRefresh();
            });

            UpdateRatingControlLayout(string.Empty, top);
        }

        private TMP_InputField CreateLabeledInput(string label, string name, float top, bool multiline)
        {
            TMP_Text labelText = CreateText(name + "Label", _rightRoot.transform, label, 18f, TextAlignmentOptions.Left);
            SetRect(labelText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, top - 24f), new Vector2(-20f, top));

            TMP_InputField input = ClonePropertyInput(multiline, _rightRoot.transform, name);
            float height = multiline ? 112f : 40f;
            SetRect(input.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, top - 30f - height), new Vector2(-20f, top - 30f));
            input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            if (multiline)
            {
                input.textComponent.enableWordWrapping = true;
                input.textComponent.alignment = TextAlignmentOptions.TopLeft;
            }
            return input;
        }

        private void UpdateRatingControlLayout(string category, float top = -466f)
        {
            category = EditorLevelLibraryStore.NormalizeRatingCategory(category);
            bool hasRating = !string.IsNullOrEmpty(category);
            bool isU = category == "U";
            if (_ratingCategoryRoot == null || _ratingLevelRoot == null || _ratingVariantRoot == null) return;
            _ratingLevelRoot.SetActive(hasRating);
            _ratingVariantRoot.SetActive(isU);

            float controlTop = top - 30f;
            float controlBottom = top - 70f;
            if (!hasRating)
            {
                SetRect(_ratingCategoryRoot.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, controlBottom), new Vector2(-20f, controlTop));
                return;
            }

            if (!isU)
            {
                SetRect(_ratingCategoryRoot.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(20f, controlBottom), new Vector2(-4f, controlTop));
                SetRect(_ratingLevelRoot.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(4f, controlBottom), new Vector2(-20f, controlTop));
                return;
            }

            SetRect(_ratingCategoryRoot.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0.3333f, 1f), new Vector2(20f, controlBottom), new Vector2(-4f, controlTop));
            SetRect(_ratingLevelRoot.GetComponent<RectTransform>(), new Vector2(0.3333f, 1f), new Vector2(0.6667f, 1f), new Vector2(4f, controlBottom), new Vector2(-4f, controlTop));
            SetRect(_ratingVariantRoot.GetComponent<RectTransform>(), new Vector2(0.6667f, 1f), new Vector2(1f, 1f), new Vector2(4f, controlBottom), new Vector2(-20f, controlTop));
        }

        private void SaveRatingAndRefresh()
        {
            if (_selected == null) return;
            EditorLevelLibraryStore.Save();
            UpdateRatingControls(_selected);
            RefreshList();
        }

        private void UpdateRatingControls(EditorLevelLibraryEntry entry)
        {
            if (entry == null) return;
            _updatingRatingControls = true;
            try
            {
                string category = EditorLevelLibraryStore.NormalizeRatingCategory(entry.RatingCategory);
                _ratingCategoryField.SetTextWithoutNotify(category);
                _ratingLevelField.SetTextWithoutNotify(Math.Max(1, Math.Min(20, entry.RatingLevel)).ToString());
                _ratingVariantField.SetTextWithoutNotify(entry.RatingIsJ ? "J" : string.Empty);
                UpdateRatingControlLayout(category);
            }
            finally
            {
                _updatingRatingControls = false;
            }
        }

        private void ShowLibrary()
        {
            _libraryActive = true;
            foreach (PropertiesPanel panel in _leftInspector.panelsList)
            {
                panel.gameObject.SetActive(false);
                if (panel.tabContainer != null) panel.tabContainer.gameObject.SetActive(false);
            }
            foreach (Transform tab in _leftInspector.tabs)
            {
                InspectorTab inspectorTab = tab.GetComponent<InspectorTab>();
                if (inspectorTab != null && inspectorTab != _customTab) inspectorTab.SetSelected(false);
            }
            _leftInspector.titleCanvas.SetActive(true);
            _leftInspector.title.text = "关卡列表";
            if (_leftInspector.messageCanvas != null) _leftInspector.messageCanvas.SetActive(false);
            _leftRoot.SetActive(true);
            _leftInspector.ShowInspector(true, forceAction: true);
            SetCustomTabSelected(true);
            RefreshList();
            if (_selected != null) ShowDetails(_selected);
        }

        private void HandleOfficialPanel(InspectorPanel panel)
        {
            if (panel == _leftInspector)
            {
                _libraryActive = false;
                if (_leftRoot != null) _leftRoot.SetActive(false);
                SetCustomTabSelected(false);
                HideRightPanel();
            }
            else if (panel == _rightInspector)
            {
                HideRightPanel();
            }
        }

        private void SetCustomTabSelected(bool selected)
        {
            if (_customTabButton == null || _customTabIcon == null) return;
            ColorBlock colors = _customTabButton.colors;
            colors.normalColor = Color.white.WithAlpha(selected ? 0.7f : 0.45f);
            _customTabButton.colors = colors;
            _customTabIcon.color = Color.white.WithAlpha(selected ? 1f : 0.6f);
            RectTransform rect = _customTab.GetComponent<RectTransform>();
            if (rect != null) rect.anchoredPosition = rect.anchoredPosition.WithX(selected ? 0f : 3f);
        }

        private void AddLevel()
        {
            string initialDirectory = Persistence.GetLastUsedFolder();
            string path = ModernFileDialog.ShowOpenFileDialog("加入关卡列表", "ADOFAI 关卡文件 (*.adofai)|*.adofai", initialDirectory);
            if (string.IsNullOrWhiteSpace(path)) return;

            EditorLevelLibraryEntry entry = EditorLevelLibraryStore.Add(path);
            if (entry == null)
            {
                SetStatus("无法读取所选关卡文件。");
                return;
            }
            Persistence.UpdateLastUsedFolder(entry.FilePath);
            _selected = entry;
            _searchField.SetTextWithoutNotify(string.Empty);
            RefreshList();
            ShowDetails(entry);
        }

        private void RefreshList()
        {
            if (_listContent == null || Main.Settings == null) return;
            foreach (GameObject row in _rows) Destroy(row);
            _rows.Clear();

            string query = _searchField == null ? string.Empty : (_searchField.text ?? string.Empty).Trim();
            List<EditorLevelLibraryEntry> entries = Main.Settings.EditorLevelLibraryEntries
                .Where(x => x != null)
                .Select((x, index) => new { Entry = x, Index = index, Score = SearchScore(x, query) })
                .Where(x => string.IsNullOrEmpty(query) || x.Score > 0)
                .OrderByDescending(x => string.IsNullOrEmpty(query) ? (x.Entry.Starred ? 1 : 0) : x.Score)
                .ThenByDescending(x => x.Entry.Starred)
                .ThenBy(x => x.Index)
                .Select(x => x.Entry)
                .ToList();

            _displayedEntries.Clear();
            _displayedEntries.AddRange(entries);
            for (int i = 0; i < entries.Count; i++) CreateRow(entries[i], i);
            _listContent.sizeDelta = new Vector2(0f, entries.Count * RowHeight);
            _emptyText.gameObject.SetActive(entries.Count == 0);
        }

        private void CreateRow(EditorLevelLibraryEntry entry, int index)
        {
            GameObject row = new GameObject("Level_" + entry.Id, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            row.transform.SetParent(_listContent, false);
            RectTransform rect = row.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -RowHeight * (index + 1));
            rect.offsetMax = new Vector2(0f, -RowHeight * index);
            Image background = row.GetComponent<Image>();
            background.sprite = GetSelectionBackgroundSprite();
            background.type = Image.Type.Sliced;
            bool selected = _selected == entry;
            background.color = selected ? Color.white : new Color(0f, 0f, 0f, 0f);

            Sprite ratingIcon = EditorLevelRatingIcons.Get(entry);
            Image icon = CreateImage("Icon", row.transform, ratingIcon != null ? ratingIcon : (_editor.fileIcon != null ? _editor.fileIcon.sprite : _editor.fileSpriteUp));
            SetRect(icon.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(8f, 8f), new Vector2(32f, -8f));
            icon.color = Color.white;

            TMP_Text name = CreateText("Name", row.transform, entry.LevelName, 17f, TextAlignmentOptions.Left);
            SetRect(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(36f, 0f), new Vector2(-70f, 0f));
            name.color = selected ? Color.black : Color.white;

            Button hit = CreateTransparentButton("Select", row.transform);
            SetRect(hit.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-70f, 0f));
            hit.onClick.AddListener(delegate { SelectEntry(entry); });
            EditorLevelLibraryRow clickHandler = hit.gameObject.AddComponent<EditorLevelLibraryRow>();
            clickHandler.Entry = entry;
            clickHandler.Background = background;
            clickHandler.Icon = icon;
            clickHandler.NameText = name;
            clickHandler.OnDoubleClick = delegate { BeginInlineRename(entry, row.transform, name); };

            Button star = CreateTextButton("Star", row.transform, entry.Starred ? "★" : "☆", 18f, new Color(0f, 0f, 0f, 0f), selected ? Color.black : Color.white);
            clickHandler.StarGraphic = star.GetComponentInChildren<TMP_Text>();
            clickHandler.RowCanvasGroup = row.GetComponent<CanvasGroup>();
            clickHandler.CanDrag = CanReorderRows;
            clickHandler.OnDragEnded = EndRowDrag;
            SetRect(star.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-66f, 3f), new Vector2(-38f, -3f));
            star.onClick.AddListener(delegate
            {
                entry.Starred = !entry.Starred;
                EditorLevelLibraryStore.Save();
                RefreshList();
            });

            Button remove = CreateImageButton("Remove", row.transform, GetDeleteIconSprite(), selected ? Color.black : Color.white);
            clickHandler.RemoveGraphic = remove.image;
            clickHandler.RowRect = rect;
            SetRect(remove.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-35f, 7f), new Vector2(-7f, -7f));
            remove.onClick.AddListener(delegate
            {
                if (_selected == entry)
                {
                    _selected = null;
                    HideRightPanel();
                }
                EditorLevelLibraryStore.Remove(entry);
                RefreshList();
            });

            _rows.Add(row);
        }

        private bool CanReorderRows()
        {
            return _searchField == null || string.IsNullOrWhiteSpace(_searchField.text);
        }

        private void EndRowDrag(EditorLevelLibraryEntry dragged, PointerEventData eventData)
        {
            if (dragged == null || eventData == null || !CanReorderRows() || _displayedEntries.Count < 2) return;

            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _listContent,
                    eventData.position,
                    eventData.pressEventCamera,
                    out localPoint)) return;

            float distanceFromTop = -localPoint.y;
            int slot;
            if (distanceFromTop <= 0f)
            {
                slot = 0;
            }
            else if (distanceFromTop >= _displayedEntries.Count * RowHeight)
            {
                slot = _displayedEntries.Count;
            }
            else
            {
                int rowIndex = Mathf.FloorToInt(distanceFromTop / RowHeight);
                float positionInRow = distanceFromTop - rowIndex * RowHeight;
                slot = rowIndex + (positionInRow >= RowHeight * 0.5f ? 1 : 0);
            }

            MoveEntryWithinStarGroup(dragged, slot);
        }

        private void MoveEntryWithinStarGroup(EditorLevelLibraryEntry dragged, int displaySlot)
        {
            List<EditorLevelLibraryEntry> settingsEntries = Main.Settings.EditorLevelLibraryEntries;
            List<EditorLevelLibraryEntry> group = _displayedEntries
                .Where(entry => entry.Starred == dragged.Starred)
                .ToList();
            int oldGroupIndex = group.IndexOf(dragged);
            if (oldGroupIndex < 0 || group.Count < 2) return;

            int groupStart = _displayedEntries.FindIndex(entry => entry.Starred == dragged.Starred);
            int groupSlot = Mathf.Clamp(displaySlot - groupStart, 0, group.Count);
            group.RemoveAt(oldGroupIndex);
            if (groupSlot > oldGroupIndex) groupSlot--;
            groupSlot = Mathf.Clamp(groupSlot, 0, group.Count);
            group.Insert(groupSlot, dragged);
            if (groupSlot == oldGroupIndex) return;

            List<int> settingsPositions = new List<int>();
            for (int i = 0; i < settingsEntries.Count; i++)
            {
                EditorLevelLibraryEntry entry = settingsEntries[i];
                if (entry != null && entry.Starred == dragged.Starred) settingsPositions.Add(i);
            }
            if (settingsPositions.Count != group.Count) return;

            for (int i = 0; i < settingsPositions.Count; i++)
            {
                settingsEntries[settingsPositions[i]] = group[i];
            }
            EditorLevelLibraryStore.Save();
            RefreshList();
        }

        private void BeginInlineRename(EditorLevelLibraryEntry entry, Transform row, TMP_Text label)
        {
            TMP_InputField rename = ClonePropertyInput(false, row, "Rename");
            SetRect(rename.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(36f, 3f), new Vector2(-70f, -3f));
            rename.SetTextWithoutNotify(entry.LevelName);
            label.gameObject.SetActive(false);
            rename.onEndEdit.AddListener(value =>
            {
                entry.LevelName = CleanName(value, entry.FilePath);
                EditorLevelLibraryStore.Save();
                Destroy(rename.gameObject);
                RefreshList();
                if (_selected == entry) PopulateDetails(entry);
            });
            rename.ActivateInputField();
            rename.Select();
        }

        private void SelectEntry(EditorLevelLibraryEntry entry)
        {
            if (_selected == entry)
            {
                return;
            }
            _selected = entry;
            foreach (GameObject row in _rows)
            {
                EditorLevelLibraryRow rowController = row.GetComponentInChildren<EditorLevelLibraryRow>();
                if (rowController != null) rowController.SetSelected(rowController.Entry == entry);
            }
            ShowDetails(entry);
        }

        private void ShowDetails(EditorLevelLibraryEntry entry)
        {
            if (entry == null || _rightRoot == null) return;
            foreach (PropertiesPanel panel in _rightInspector.panelsList)
            {
                panel.gameObject.SetActive(false);
                if (panel.tabContainer != null) panel.tabContainer.gameObject.SetActive(false);
            }
            foreach (Transform tab in _rightInspector.tabs) tab.gameObject.SetActive(false);

            if (!_rightButtonsCaptured)
            {
                _deleteButtonWasActive = _rightInspector.deleteEventButton != null && _rightInspector.deleteEventButton.gameObject.activeSelf;
                _disableButtonWasActive = _rightInspector.disableEventButton != null && _rightInspector.disableEventButton.gameObject.activeSelf;
                _rightButtonsCaptured = true;
            }
            if (_rightInspector.deleteEventButton != null) _rightInspector.deleteEventButton.gameObject.SetActive(false);
            if (_rightInspector.disableEventButton != null) _rightInspector.disableEventButton.gameObject.SetActive(false);
            _rightInspector.titleCanvas.SetActive(true);
            _rightInspector.title.text = "关卡信息";
            if (_rightInspector.messageCanvas != null) _rightInspector.messageCanvas.SetActive(false);
            _rightRoot.SetActive(true);
            _rightInspector.ShowInspector(true, forceAction: true);
            PopulateDetails(entry);
        }

        private void PopulateDetails(EditorLevelLibraryEntry entry)
        {
            if (entry == null) return;
            _nameField.SetTextWithoutNotify(entry.LevelName ?? string.Empty);
            _authorField.SetTextWithoutNotify(entry.LevelAuthor ?? string.Empty);
            _artistField.SetTextWithoutNotify(entry.Artist ?? string.Empty);
            _songField.SetTextWithoutNotify(entry.SongName ?? string.Empty);
            _notesField.SetTextWithoutNotify(entry.Notes ?? string.Empty);
            UpdateRatingControls(entry);
            bool exists = File.Exists(entry.FilePath);
            _openButton.interactable = exists;
            SetStatus(exists ? string.Empty : "原始 .adofai 文件不存在。请重新添加该关卡。");
        }

        private void HideRightPanel()
        {
            if (_rightRoot != null) _rightRoot.SetActive(false);
            if (_rightButtonsCaptured)
            {
                if (_rightInspector.deleteEventButton != null) _rightInspector.deleteEventButton.gameObject.SetActive(_deleteButtonWasActive);
                if (_rightInspector.disableEventButton != null) _rightInspector.disableEventButton.gameObject.SetActive(_disableButtonWasActive);
                _rightButtonsCaptured = false;
            }
        }

        private void UpdateSelectedEntry(Action<EditorLevelLibraryEntry> update)
        {
            if (_selected == null || update == null) return;
            update(_selected);
            EditorLevelLibraryStore.Save();
            RefreshList();
            PopulateDetails(_selected);
        }

        private void OpenSelectedLevel()
        {
            if (_selected == null) return;
            if (!File.Exists(_selected.FilePath))
            {
                SetStatus("原始 .adofai 文件不存在。请重新添加该关卡。");
                return;
            }

            _selected.LastOpenedUtcTicks = DateTime.UtcNow.Ticks;
            EditorLevelLibraryStore.Save();
            string path = _selected.FilePath;
            _editor.CheckUnsavedChanges(delegate { _editor.OpenLevel(path); });
        }

        private void SetStatus(string text)
        {
            if (_statusText != null) _statusText.text = text ?? string.Empty;
        }

        private static int SearchScore(EditorLevelLibraryEntry entry, string query)
        {
            if (string.IsNullOrEmpty(query)) return 1;
            int quality;
            if ((quality = MatchQuality(entry.LevelName, query)) > 0) return 5000 + quality;
            if ((quality = RatingMatchQuality(entry, query)) > 0) return 4500 + quality;
            if ((quality = MatchQuality(entry.LevelAuthor, query)) > 0) return 4000 + quality;
            if ((quality = MatchQuality(entry.Artist, query)) > 0) return 3000 + quality;
            if ((quality = MatchQuality(entry.SongName, query)) > 0) return 2000 + quality;
            if ((quality = MatchQuality(entry.Notes, query)) > 0) return 1000 + quality;
            return 0;
        }

        private static int RatingMatchQuality(EditorLevelLibraryEntry entry, string query)
        {
            string category = EditorLevelLibraryStore.NormalizeRatingCategory(entry.RatingCategory);
            string rating = string.IsNullOrEmpty(category)
                ? "未设置"
                : category + Math.Max(1, Math.Min(20, entry.RatingLevel)).ToString()
                    + (category == "U" && entry.RatingIsJ ? "J" : string.Empty);
            string compactQuery = new string((query ?? string.Empty)
                .Where(character => !char.IsWhiteSpace(character) && character != '-' && character != '_')
                .ToArray());
            return MatchQuality(rating, compactQuery);
        }

        private static int MatchQuality(string value, string query)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            if (string.Equals(value, query, StringComparison.CurrentCultureIgnoreCase)) return 300;
            if (value.StartsWith(query, StringComparison.CurrentCultureIgnoreCase)) return 200;
            return value.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 ? 100 : 0;
        }

        private static string CleanName(string value, string path)
        {
            return string.IsNullOrWhiteSpace(value) ? Path.GetFileNameWithoutExtension(path) : value.Trim();
        }

        private GameObject CreatePanelRoot(string name, Transform parent, RectTransform source)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            if (source != null)
            {
                rect.anchorMin = source.anchorMin;
                rect.anchorMax = source.anchorMax;
                rect.pivot = source.pivot;
                rect.anchoredPosition = source.anchoredPosition;
                rect.sizeDelta = source.sizeDelta;
                rect.offsetMin = source.offsetMin;
                rect.offsetMax = source.offsetMax;
            }
            else
            {
                SetRect(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
            return root;
        }

        private TMP_InputField CloneInput(TMP_InputField template, Transform parent, string name)
        {
            TMP_InputField input = Instantiate(template.gameObject, parent, false).GetComponent<TMP_InputField>();
            input.gameObject.name = name;
            input.onValueChanged = new TMP_InputField.OnChangeEvent();
            input.onEndEdit = new TMP_InputField.SubmitEvent();
            input.SetTextWithoutNotify(string.Empty);
            input.interactable = true;
            return input;
        }

        private TMP_InputField ClonePropertyInput(bool multiline, Transform parent, string name)
        {
            GameObject prefab = multiline ? ADOBase.gc.prefab_controlLongText : ADOBase.gc.prefab_controlText;
            GameObject controlObject = Instantiate(prefab, parent, false);
            PropertyControl control = controlObject.GetComponent<PropertyControl>();
            TMP_InputField sourceInput = multiline
                ? ((PropertyControl_LongText)control).inputField
                : ((PropertyControl_Text)control).inputField;

            bool inputIsRoot = sourceInput.gameObject == controlObject;
            if (!inputIsRoot) sourceInput.transform.SetParent(parent, false);
            sourceInput.gameObject.name = name;
            sourceInput.onValueChanged = new TMP_InputField.OnChangeEvent();
            sourceInput.onEndEdit = new TMP_InputField.SubmitEvent();
            sourceInput.SetTextWithoutNotify(string.Empty);
            sourceInput.interactable = true;
            if (inputIsRoot)
            {
                Destroy(control);
            }
            else
            {
                Destroy(controlObject);
            }
            return sourceInput;
        }

        private Sprite GetDeleteIconSprite()
        {
            Button source = _editor.propertyControlDecorationsList.removeButton;
            if (source == null) return null;
            foreach (Image image in source.GetComponentsInChildren<Image>(true))
            {
                if (image != null && image != source.image && image.sprite != null)
                {
                    return image.sprite;
                }
            }
            return source.image != null ? source.image.sprite : null;
        }

        private Sprite GetSelectionBackgroundSprite()
        {
            ListItemPool pool = _editor.propertyControlDecorationsList.listItemPool;
            if (pool == null || pool.itemPrefab == null) return null;
            ListItem item = pool.itemPrefab.GetComponent<ListItem>();
            if (item == null || item.selectionBackground == null) return null;
            Image image = item.selectionBackground.GetComponent<Image>();
            return image != null ? image.sprite : null;
        }

        private void SetPlaceholder(TMP_InputField input, string text)
        {
            TMP_Text placeholder = input.placeholder as TMP_Text;
            if (placeholder != null) placeholder.text = text;
        }

        private TMP_Text CreateText(string name, Transform parent, string text, float size, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TMP_Text tmp = go.GetComponent<TMP_Text>();
            tmp.text = text;
            tmp.font = _leftInspector.title.font;
            tmp.fontSize = size;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = true;
            return tmp;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private Button CreateTransparentButton(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.001f);
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private Button CreateTextButton(string name, Transform parent, string text, float textSize, Color background, Color foreground)
        {
            Button button = CreateTransparentButton(name, parent);
            button.image.color = background.a <= 0f ? new Color(0f, 0f, 0f, 0.001f) : background;
            TMP_Text label = CreateText("Label", button.transform, text, textSize, TextAlignmentOptions.Center);
            label.color = foreground;
            SetRect(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        private static Button CreateImageButton(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = color;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }

    internal sealed class EditorLevelLibraryRow : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        internal Action OnDoubleClick;
        internal Func<bool> CanDrag;
        internal Action<EditorLevelLibraryEntry, PointerEventData> OnDragEnded;
        internal EditorLevelLibraryEntry Entry;
        internal Image Background;
        internal Image Icon;
        internal TMP_Text NameText;
        internal Graphic StarGraphic;
        internal Graphic RemoveGraphic;
        internal CanvasGroup RowCanvasGroup;
        internal RectTransform RowRect;
        private bool _dragging;
        private int _originalSiblingIndex;
        private Vector2 _startAnchoredPosition;
        private Vector2 _pointerOffset;

        internal void SetSelected(bool selected)
        {
            Color color = selected ? Color.black : Color.white;
            if (Background != null) Background.color = selected ? Color.white : new Color(0f, 0f, 0f, 0f);
            if (Icon != null) Icon.color = Color.white;
            if (NameText != null) NameText.color = color;
            if (StarGraphic != null) StarGraphic.color = color;
            if (RemoveGraphic != null) RemoveGraphic.color = color;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
            {
                OnDoubleClick?.Invoke();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || (CanDrag != null && !CanDrag())) return;
            _dragging = true;
            if (RowCanvasGroup != null)
            {
                RowCanvasGroup.alpha = 0.65f;
                RowCanvasGroup.blocksRaycasts = false;
            }
            if (RowRect != null)
            {
                _originalSiblingIndex = RowRect.GetSiblingIndex();
                _startAnchoredPosition = RowRect.anchoredPosition;
                Vector2 pointerPosition;
                RectTransform parent = RowRect.parent as RectTransform;
                if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parent,
                        eventData.position,
                        eventData.pressEventCamera,
                        out pointerPosition))
                {
                    _pointerOffset = _startAnchoredPosition - pointerPosition;
                }
                else
                {
                    _pointerOffset = Vector2.zero;
                }
                RowRect.SetAsLastSibling();
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || RowRect == null) return;
            Vector2 pointerPosition;
            RectTransform parent = RowRect.parent as RectTransform;
            if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    eventData.position,
                    eventData.pressEventCamera,
                    out pointerPosition))
            {
                RowRect.anchoredPosition = pointerPosition + _pointerOffset;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;
            if (RowCanvasGroup != null)
            {
                RowCanvasGroup.alpha = 1f;
                RowCanvasGroup.blocksRaycasts = true;
            }
            if (RowRect != null)
            {
                RowRect.anchoredPosition = _startAnchoredPosition;
                RowRect.SetSiblingIndex(_originalSiblingIndex);
            }
            OnDragEnded?.Invoke(Entry, eventData);
        }
    }

    [HarmonyPatch(typeof(scnEditor), "Start")]
    internal static class scnEditor_Start_EditorLevelLibrary_Patch
    {
        private static void Postfix(scnEditor __instance)
        {
            EditorLevelLibraryPanel.Install(__instance);
        }
    }

    [HarmonyPatch(typeof(InspectorPanel), "ShowPanel")]
    internal static class InspectorPanel_ShowPanel_EditorLevelLibrary_Patch
    {
        private static void Prefix(InspectorPanel __instance)
        {
            EditorLevelLibraryPanel.BeforeOfficialPanel(__instance);
        }
    }
}
