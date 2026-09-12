using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zorro.UI;

namespace StateKeeper
{
    internal static partial class StateKeeperUi
    {
        private static readonly List<GameObject> Rows = new List<GameObject>();
        private const float HistoryActionsWidth = 488f;
        private static Transform _menuParent;
        private static PauseMenuHandler _handler;
        private static PauseMenuMainPage _parentPage;
        private static StateKeeperPage _historyPage;
        private static StateKeeperDetailsPage _detailsPage;
        private static StateKeeperButton _openButton;
        private static TextMeshProUGUI _title;
        private static TextMeshProUGUI _summary;
        private static TextMeshProUGUI _empty;
        private static TextMeshProUGUI _recentTabText;
        private static TextMeshProUGUI _favoritesTabText;
        private static TextMeshProUGUI _historyBackText;
        private static TextMeshProUGUI _renameLabel;
        private static TextMeshProUGUI _confirmText;
        private static TextMeshProUGUI _cancelText;
        private static TextMeshProUGUI _detailsBackText;
        private static TextMeshProUGUI _detailsTitle;
        private static TextMeshProUGUI _detailsStatus;
        private static TextMeshProUGUI _playerFilterText;
        private static TextMeshProUGUI _segmentFilterText;
        private static TextMeshProUGUI _confidenceFilterText;
        private static TextMeshProUGUI _resourceFilterText;
        private static readonly List<TextMeshProUGUI> DetailsTabTexts = new List<TextMeshProUGUI>();
        private static StateKeeperButton _analysisAction;
        private static RunIndexEntry _selectedEntry;
        private static AnalysisResult _displayedAnalysis;
        private static int _detailsView;
        private static int _playerFilter = -1;
        private static int _segmentFilter = -1;
        private static int _confidenceFilter;
        private static int _resourceFilter;
        private static int _itemPage;
        private static bool _analysisRendered;
        private static readonly string[] ResourceFilters = { "", "uses", "fuel", "useRemaining", "cookedAmount", "used", "flareActive", "powerEnabled", "petterItemUses" };
        private static TMP_InputField _nameInput;
        private static GameObject _renamePanel;
        private static bool _favorites;
        private static string _editingRunId;
        private static TMP_FontAsset _font;
        private static bool _languageSubscribed;
        private static GameObject _dashboardContent;
        private static ScrollRect _dashboardScroll;

        internal static void Register()
        {
            if (_languageSubscribed) return;
            LocalizedText.OnLangugageChanged += RefreshLanguage;
            _languageSubscribed = true;
        }

        internal static void Dispose()
        {
            StateKeeperPlugin.Analysis?.Cancel(); _compareService?.Cancel();
            if (!_languageSubscribed) return;
            LocalizedText.OnLangugageChanged -= RefreshLanguage;
            _languageSubscribed = false;
        }

        internal static void BuildPauseMenu(PauseMenuMainPage parentPage)
        {
            if (parentPage == null) return;
            Transform parent = parentPage.transform;
            if (_menuParent == null || _menuParent != parent)
                Reset();
            _menuParent = parent;
            _parentPage = parentPage;
            _handler = parent.GetComponentInParent<PauseMenuHandler>();
            Register();
            if (_handler == null) return;
            if (_openButton == null || !_openButton.IsAlive)
            {
                GameObject template = parentPage.resumeButton == null ? null : parentPage.resumeButton.gameObject;
                if (template == null) return;
                _openButton = StateKeeperButton.Create(template, "StateKeeperOpenButton", parent);
                _openButton.AddListener(OpenHistory);
            }
            _openButton.GameObject.SetActive(true);
            _openButton.SetText(Text("STATE KEEPER", "状态分析"));
            PositionOpenButton(parentPage);
        }

        private static void PositionOpenButton(PauseMenuMainPage parentPage)
        {
            Button quitButton = GetMenuButton(parentPage, "m_quitButton");
            if (quitButton == null)
            {
                // Keep the entry usable on an unexpected menu hierarchy, but do not place it over the title.
                _openButton.RectTransform.anchorMin = new Vector2(0.5f, 0f);
                _openButton.RectTransform.anchorMax = new Vector2(0.5f, 0f);
                _openButton.RectTransform.pivot = new Vector2(0.5f, 0f);
                _openButton.RectTransform.anchoredPosition = new Vector2(0f, 90f);
                _openButton.RectTransform.sizeDelta = new Vector2(277f, _openButton.RectTransform.sizeDelta.y);
                return;
            }

            RectTransform quitRect = quitButton.GetComponent<RectTransform>();
            RectTransform entryRect = _openButton.RectTransform;
            entryRect.SetParent(quitRect.parent, false);
            entryRect.SetAsLastSibling();

            float buttonPitch = FindMenuButtonPitch(parentPage, quitRect);
            entryRect.anchorMin = quitRect.anchorMin;
            entryRect.anchorMax = quitRect.anchorMax;
            entryRect.pivot = quitRect.pivot;
            entryRect.sizeDelta = quitRect.sizeDelta;
            entryRect.localScale = quitRect.localScale;
            entryRect.localRotation = quitRect.localRotation;
            entryRect.anchoredPosition = quitRect.anchoredPosition + Vector2.down * buttonPitch;

            // The entry is physically below Leave Game, so controller navigation follows the same order.
            Navigation quitNavigation = quitButton.navigation;
            quitNavigation.selectOnDown = _openButton.Button;
            quitButton.navigation = quitNavigation;
            Navigation entryNavigation = _openButton.Button.navigation;
            entryNavigation.selectOnUp = quitButton;
            _openButton.Button.navigation = entryNavigation;
        }

        private static float FindMenuButtonPitch(PauseMenuMainPage page, RectTransform quitRect)
        {
            float result = float.MaxValue;
            foreach (FieldInfo field in typeof(PauseMenuMainPage).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (field.FieldType != typeof(Button)) continue;
                Button button = field.GetValue(page) as Button;
                RectTransform rect = button == null ? null : button.GetComponent<RectTransform>();
                if (rect == null || rect == quitRect || rect.parent != quitRect.parent) continue;
                float distance = Mathf.Abs(quitRect.anchoredPosition.y - rect.anchoredPosition.y);
                if (distance > 1f && distance < result) result = distance;
            }
            return result < float.MaxValue ? result : Mathf.Max(quitRect.rect.height, 54f) + 20f;
        }

        private static Button GetMenuButton(PauseMenuMainPage page, string fieldName)
        {
            FieldInfo field = typeof(PauseMenuMainPage).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(page) as Button;
        }

        private static void OpenHistory()
        {
            if (!EnsureHistoryPage()) return;
            _favorites = false;
            RefreshHistory();
            Transition(_historyPage);
        }

        private static bool EnsureHistoryPage()
        {
            if (_historyPage != null) return true;
            if (_parentPage == null || _handler == null) return false;
            EnsureFont();
            GameObject pageObject = new GameObject("StateKeeperHistoryPage", typeof(RectTransform));
            pageObject.transform.SetParent(_parentPage.transform.parent, false);
            RectTransform pageRect = pageObject.GetComponent<RectTransform>();
            Stretch(pageRect);
            _historyPage = pageObject.AddComponent<StateKeeperPage>();
            _historyPage.ParentPage = _parentPage;
            _historyPage.OnOpened = RefreshHistory;
            AddImage(pageObject.transform, "Background", new Color(0.02f, 0.02f, 0.02f, 0.94f), true);
            _title = CreateText(pageObject.transform, "Title", 46f, TextAlignmentOptions.Center);
            Anchor(_title.rectTransform, 0.5f, 1f, 0.5f, 1f, 0f, -62f, 900f, 82f);
            _summary = CreateText(pageObject.transform, "Summary", 24f, TextAlignmentOptions.Center);
            Anchor(_summary.rectTransform, 0.5f, 1f, 0.5f, 1f, 0f, -132f, 1200f, 42f);
            CreateTabButtons(pageObject.transform);
            CreateHistoryList(pageObject.transform);
            CreateRenamePanel(pageObject.transform);
            StateKeeperButton back = CreateButton(pageObject.transform, "Back", Text("BACK", "返回"));
            _historyBackText = back.Text;
            Anchor(back.RectTransform, 0.5f, 0f, 0.5f, 0f, 0f, 30f, 220f, 54f);
            back.AddListener(delegate { Transition(_parentPage); });
            _detailsPage = null;
            pageObject.SetActive(false);
            return true;
        }

        private static void CreateTabButtons(Transform parent)
        {
            StateKeeperButton recent = CreateButton(parent, "RecentTab", Text("RECENT", "最近记录"));
            _recentTabText = recent.Text;
            Anchor(recent.RectTransform, 0.5f, 1f, 0.5f, 1f, -190f, -180f, 180f, 48f);
            recent.AddListener(delegate { _favorites = false; RefreshHistory(); });
            StateKeeperButton favorites = CreateButton(parent, "FavoritesTab", Text("FAVORITES", "收藏记录"));
            _favoritesTabText = favorites.Text;
            Anchor(favorites.RectTransform, 0.5f, 1f, 0.5f, 1f, 190f, -180f, 180f, 48f);
            favorites.AddListener(delegate { _favorites = true; RefreshHistory(); });
        }

        private static void CreateHistoryList(Transform parent)
        {
            GameObject scrollObject = new GameObject("HistoryScroll", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
            scrollObject.transform.SetParent(parent, false);
            RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
            Stretch(scrollRect); scrollRect.offsetMin = new Vector2(75, 110); scrollRect.offsetMax = new Vector2(-75, -240);
            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(scrollObject.transform, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 0f);
            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 8f;
            contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            _empty = CreateText(parent, "Empty", 30f, TextAlignmentOptions.Center);
            Anchor(_empty.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0f, -15f, 1000f, 200f);
        }

        private static void CreateRenamePanel(Transform parent)
        {
            // Keep the modal in its page's canvas; a fixed child sorting order can put it behind the menu.
            _renamePanel = new GameObject("RenamePanel", typeof(RectTransform), typeof(Image));
            _renamePanel.transform.SetParent(parent, false);
            Stretch(_renamePanel.GetComponent<RectTransform>());
            _renamePanel.GetComponent<Image>().color = new Color(0, 0, 0, .85f);
            _renamePanel.GetComponent<Image>().raycastTarget = true;
            var frame = new GameObject("Dialog", typeof(RectTransform), typeof(Image)); frame.transform.SetParent(_renamePanel.transform, false);
            Anchor(frame.GetComponent<RectTransform>(), .5f, .5f, .5f, .5f, 0, 0, 620, 270);
            frame.GetComponent<Image>().color = new Color(.08f, .08f, .08f, 1);
            TextMeshProUGUI label = CreateText(frame.transform, "Label", 24f, TextAlignmentOptions.Left);
            _renameLabel = label;
            label.alignment = TextAlignmentOptions.Center;
            Anchor(label.rectTransform, .5f, 1f, .5f, 1f, 0f, -20f, 560f, 34f);
            label.text = Text("EXPEDITION NAME", "远征名称");
            _nameInput = CreateInput(frame.transform);
            Anchor(_nameInput.GetComponent<RectTransform>(), .5f, 1f, .5f, 1f, 0f, -76f, 560f, 46f);
            _renameMessage = CreateText(frame.transform, "Validation", 18, TextAlignmentOptions.Left);
            Anchor(_renameMessage.rectTransform, .5f, 1f, .5f, 1f, 0, -134, 560, 60);
            _nameInput.onValueChanged.AddListener(_ => UpdateRenameMessage(false));
            StateKeeperButton confirm = CreateButton(frame.transform, "Confirm", Text("CONFIRM", "确认"));
            confirm.SetCompactText(Text("CONFIRM", "确认"));
            _confirmText = confirm.Text;
            Anchor(confirm.RectTransform, .25f, 0f, .25f, 0f, 0f, 20f, 180f, 42f);
            confirm.AddListener(ConfirmRename);
            StateKeeperButton cancel = CreateButton(frame.transform, "Cancel", Text("CANCEL", "取消"));
            cancel.SetCompactText(Text("CANCEL", "取消"));
            _cancelText = cancel.Text;
            Anchor(cancel.RectTransform, .75f, 0f, .75f, 0f, 0f, 20f, 180f, 42f);
            cancel.AddListener(CancelRename);
            _nameInput.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = confirm.Button };
            confirm.Button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = _nameInput, selectOnRight = cancel.Button };
            cancel.Button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = _nameInput, selectOnLeft = confirm.Button };
            _renamePanel.SetActive(false);
        }

        private static TMP_InputField CreateInput(Transform parent)
        {
            GameObject inputObject = new GameObject("NameInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            inputObject.transform.SetParent(parent, false);
            inputObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);
            TextMeshProUGUI text = CreateText(inputObject.transform, "Text", 24f, TextAlignmentOptions.Left);
            Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(12, 4); text.rectTransform.offsetMax = new Vector2(-12, -4);
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow; text.richText = false;
            inputObject.AddComponent<RectMask2D>();
            TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
            input.textComponent = text;
            input.textViewport = inputObject.GetComponent<RectTransform>();
            input.characterLimit = 40;
            input.lineType = TMP_InputField.LineType.SingleLine;
            return input;
        }

        private static void RefreshHistory()
        {
            if (_historyPage == null || StateKeeperPlugin.Store == null) return;
            foreach (GameObject row in Rows) if (row != null) UnityEngine.Object.Destroy(row);
            Rows.Clear();
            IReadOnlyList<RunIndexEntry> entries = _favorites
                ? StateKeeperPlugin.Store.GetFavoriteEntries()
                : StateKeeperPlugin.Store.GetRecentEntries();
            if (_empty != null)
            {
                _empty.text = entries.Count == 0 ? Text("NO RECORDED EXPEDITIONS", "暂无记录") : string.Empty;
                _empty.gameObject.SetActive(entries.Count == 0);
            }
            if (_title != null) _title.text = Text("STATE KEEPER", "状态分析");
            if (_summary != null) _summary.text = _favorites ? Text("FAVORITE EXPEDITIONS", "收藏记录") : Text("RECENT EXPEDITIONS", "最近记录");
            foreach (RunIndexEntry entry in entries) CreateRow(entry);
            RefreshHistoryNavigation();
        }

        private static void CreateRow(RunIndexEntry entry)
        {
            GameObject rowObject = new GameObject("RunRow_" + entry.runId, typeof(RectTransform), typeof(Image), typeof(Button));
            rowObject.transform.SetParent(_historyPage.transform.Find("HistoryScroll/Content"), false);
            rowObject.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 112f);
            rowObject.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.96f);
            Button rowButton = rowObject.GetComponent<Button>();
            rowButton.onClick.AddListener(delegate { OpenDetails(entry); });
            TextMeshProUGUI text = CreateText(rowObject.transform, "Text", 22f, TextAlignmentOptions.Left);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(54, 8);
            text.rectTransform.offsetMax = new Vector2(-HistoryActionsWidth, -8);
            text.raycastTarget = false;
            text.richText = false;
            string name = string.IsNullOrWhiteSpace(entry.customName) ? Text("UNTITLED EXPEDITION", "未命名远征") : entry.customName;
            string outcome = entry.outcome == RunOutcome.Victory.ToString() ? Text("VICTORY", "胜利") :
                entry.outcome == RunOutcome.Defeat.ToString() ? Text("DEFEAT", "失败") : Text("ABORTED", "中止");
            string date = FormatDate(entry.startedUtc);
            string duration = TimeSpan.FromSeconds(Math.Max(0f, entry.durationSeconds)).ToString("hh\\:mm\\:ss", CultureInfo.InvariantCulture);
            text.text = name + "\n" + date + " | " + outcome + " | " + duration + " | " +
                Text("Players", "玩家") + ": " + entry.playerCount + " | " + Text("Data", "数据") + ": " +
                entry.sampleCount + "/" + entry.inventorySnapshotCount + "/" + entry.eventCount;
            StateKeeperButton favorite = CreateButton(rowObject.transform, "Favorite", entry.favorite ? Text("UNFAVORITE", "取消收藏") : Text("FAVORITE", "收藏"));
            favorite.SetCompactText(entry.favorite ? Text("UNFAVORITE", "取消收藏") : Text("FAVORITE", "收藏"));
            Anchor(favorite.RectTransform, 1f, 0.5f, 1f, 0.5f, -288f, 0f, 176f, 44f);
            favorite.AddListener(delegate { StateKeeperPlugin.Store.ToggleFavorite(entry.runId); RefreshHistory(); });
            StateKeeperButton rename = CreateButton(rowObject.transform, "Rename", Text("RENAME", "命名"));
            rename.SetCompactText(Text("RENAME", "命名"));
            Anchor(rename.RectTransform, 1f, 0.5f, 1f, 0.5f, -148f, 0f, 128f, 44f);
            rename.AddListener(delegate { BeginRename(entry); });
            StateKeeperButton delete = CreateButton(rowObject.transform, "Delete", Text("DELETE", "删除"));
            delete.SetCompactText(Text("DELETE", "删除"));
            Anchor(delete.RectTransform, 1f, 0.5f, 1f, 0.5f, -24f, 0f, 112f, 44f);
            delete.AddListener(delegate { StateKeeperPlugin.Store.DeleteRun(entry.runId); RefreshHistory(); });
            Rows.Add(rowObject);
            AddCompareSelection(rowObject.transform, entry);
        }

        private static void OpenDetails(RunIndexEntry entry)
        {
            if (!EnsureDetailsPage()) return;
            _selectedEntry = entry;
            _displayedAnalysis = null;
            _analysisRendered = false;
            _detailsView = 0;
            _playerFilter = -1;
            _segmentFilter = -1;
            _confidenceFilter = 0;
            _resourceFilter = 0;
            _itemPage = 0;
            _rangeStart = null; _rangeEnd = null; _evidenceEpoch = null; _focusedEvent = null; _itemQuery = ""; _timelinePage = 0;
            _searchTerms.Clear(); _searchInput?.SetTextWithoutNotify(""); _searchDirty = false; _relationIndex = -1;
            _detailsTitle.text = string.IsNullOrWhiteSpace(entry.customName) ? Text("UNTITLED EXPEDITION", "未命名远征") : entry.customName;
            ClearDashboard();
            _detailsStatus.text = Text("PREPARING ANALYSIS", "正在准备分析");
            if (StateKeeperPlugin.Analysis != null) StateKeeperPlugin.Analysis.Start(entry.runId);
            RefreshDetailsLabels();
            Transition(_detailsPage);
        }

        private static bool EnsureDetailsPage()
        {
            if (_detailsPage != null) return true;
            GameObject pageObject = new GameObject("StateKeeperDetailsPage", typeof(RectTransform));
            pageObject.transform.SetParent(_historyPage.transform.parent, false);
            Stretch(pageObject.GetComponent<RectTransform>());
            _detailsPage = pageObject.AddComponent<StateKeeperDetailsPage>();
            _detailsPage.ParentPage = _historyPage;
            _detailsPage.OnTick = PollAnalysis;
            _detailsPage.OnClosed = delegate
            {
                if (StateKeeperPlugin.Analysis != null && StateKeeperPlugin.Analysis.IsRunning) StateKeeperPlugin.Analysis.Cancel();
            };
            AddImage(pageObject.transform, "Background", new Color(0.02f, 0.02f, 0.02f, 0.96f), true);
            _detailsTitle = CreateText(pageObject.transform, "Title", 38f, TextAlignmentOptions.Center);
            Anchor(_detailsTitle.rectTransform, 0.5f, 1f, 0.5f, 1f, 0f, -30f, 1000f, 58f);
            CreateDetailsTabs(pageObject.transform);
            CreateDetailsFilters(pageObject.transform);
            CreateReportControls(pageObject.transform);
            _detailsStatus = CreateText(pageObject.transform, "Status", 20f, TextAlignmentOptions.Center);
            Anchor(_detailsStatus.rectTransform, 0.5f, 1f, 0.5f, 1f, -70f, -190f, 1000f, 34f);
            _analysisAction = CreateButton(pageObject.transform, "AnalysisAction", Text("CANCEL", "取消"));
            _analysisAction.SetCompactText(Text("CANCEL", "取消"));
            Anchor(_analysisAction.RectTransform, 0.5f, 1f, 0.5f, 1f, 530f, -190f, 120f, 36f);
            _analysisAction.AddListener(delegate
            {
                if (StateKeeperPlugin.Analysis == null || _selectedEntry == null) return;
                if (StateKeeperPlugin.Analysis.IsRunning) StateKeeperPlugin.Analysis.Cancel();
                else { _analysisRendered = false; _displayedAnalysis = null; StateKeeperPlugin.Analysis.Start(_selectedEntry.runId, true); }
            });
            CreateDashboard(pageObject.transform);
            StateKeeperButton back = CreateButton(pageObject.transform, "Back", Text("BACK", "返回"));
            _detailsBackText = back.Text;
            Anchor(back.RectTransform, 0.5f, 0f, 0.5f, 0f, 0f, 30f, 220f, 54f);
            back.AddListener(delegate { Transition(_historyPage); });
            pageObject.SetActive(false);
            return true;
        }

        private static void CreateDetailsTabs(Transform parent)
        {
            string[] english = { "OVERVIEW", "PLAYER REPORT", "TEAM RELATIONS", "ITEMS", "TIMELINE", "DATA QUALITY" };
            string[] chinese = { "概览", "玩家报告", "队伍关系", "物品", "事件时间线", "数据质量" };
            int[] positions = { 0, 2, 3, 4, 1, 5 };
            for (int i = 0; i < english.Length; i++)
            {
                int view = i;
                StateKeeperButton button = CreateButton(parent, "DetailsTab" + i, Text(english[i], chinese[i]));
                button.SetCompactText(Text(english[i], chinese[i]));
                Anchor(button.RectTransform, 0.5f, 1f, 0.5f, 1f, -500f + positions[i] * 200f, -92f, 185f, 44f);
                button.AddListener(delegate { _detailsView = view; _itemPage = 0; _focusedEvent = null; RenderDetails(); });
                DetailsTabTexts.Add(button.Text);
            }
        }

        private static void CreateDetailsFilters(Transform parent)
        {
            StateKeeperButton player = CreateButton(parent, "PlayerFilter", string.Empty);
            _playerFilterText = player.Text;
            Anchor(player.RectTransform, 0.5f, 1f, 0.5f, 1f, -345f, -145f, 210f, 40f);
            player.AddListener(CyclePlayerFilter);
            StateKeeperButton segment = CreateButton(parent, "SegmentFilter", string.Empty);
            _segmentFilterText = segment.Text;
            Anchor(segment.RectTransform, 0.5f, 1f, 0.5f, 1f, -115f, -145f, 210f, 40f);
            segment.AddListener(CycleSegmentFilter);
            StateKeeperButton confidence = CreateButton(parent, "ConfidenceFilter", string.Empty);
            _confidenceFilterText = confidence.Text;
            Anchor(confidence.RectTransform, 0.5f, 1f, 0.5f, 1f, 115f, -145f, 210f, 40f);
            confidence.AddListener(delegate { _confidenceFilter = (_confidenceFilter + 1) % 4; _itemPage = 0; RenderDetails(); });
            StateKeeperButton resource = CreateButton(parent, "ResourceFilter", string.Empty);
            _resourceFilterText = resource.Text;
            Anchor(resource.RectTransform, 0.5f, 1f, 0.5f, 1f, 345f, -145f, 210f, 40f);
            resource.AddListener(delegate { _resourceFilter = (_resourceFilter + 1) % ResourceFilters.Length; _itemPage = 0; RenderDetails(); });
        }

        private static void PollAnalysis()
        {
            PollReportInput();
            RunAnalysisService analysis = StateKeeperPlugin.Analysis;
            if (analysis == null || _selectedEntry == null) return;
            if (analysis.IsRunning)
            {
                int current = analysis.CurrentChunk;
                int total = analysis.TotalChunks;
                string progress = total > 0 ? " (" + Math.Min(current, total) + "/" + total + ", " + (100 * Math.Min(current, total) / total) + "%)" : string.Empty;
                _detailsStatus.text = Text("ANALYZING" + progress, "正在分析" + progress);
                _analysisAction.SetCompactText(Text("CANCEL", "取消"));
                _analysisAction.SetInteractable(true);
                return;
            }

            if (analysis.Error != null)
            {
                _detailsStatus.text = Text("ANALYSIS FAILED: " + analysis.Error.Message, "分析失败: " + analysis.Error.Message);
                _analysisAction.SetCompactText(Text("RETRY", "重试"));
                return;
            }
            if (string.Equals(analysis.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                _detailsStatus.text = Text("ANALYSIS CANCELLED", "分析已取消");
                _analysisAction.SetCompactText(Text("RETRY", "重试"));
                return;
            }

            AnalysisResult result = analysis.Result;
            if (result == null) return;
            _analysisAction.SetCompactText(Text("REANALYZE", "重新分析"));
            _detailsStatus.text = Text("ANALYSIS COMPLETE", "分析完成");
            if (!_analysisRendered || !ReferenceEquals(_displayedAnalysis, result))
            {
                _displayedAnalysis = result;
                _analysisRendered = true;
                RefreshDetailsLabels();
                RenderDetails();
            }
        }

        private static void RefreshDetailsLabels()
        {
            string[] english = { "OVERVIEW", "PLAYER REPORT", "TEAM RELATIONS", "ITEMS", "TIMELINE", "DATA QUALITY" };
            string[] chinese = { "概览", "玩家报告", "队伍关系", "物品", "事件时间线", "数据质量" };
            for (int i = 0; i < DetailsTabTexts.Count && i < english.Length; i++)
                if (DetailsTabTexts[i] != null) DetailsTabTexts[i].text = Text(english[i], chinese[i]);
            if (_playerFilterText != null)
            {
                AnalysisPlayer player = GetSelectedPlayer();
                _playerFilterText.text = player == null ? Text("PLAYER: ALL", "玩家: 全部") :
                    Text("PLAYER: " + DisplayPlayerName(player), "玩家: " + DisplayPlayerName(player));
            }
            if (_segmentFilterText != null)
            {
                AnalysisSegment segment = GetSelectedSegment();
                _segmentFilterText.text = segment == null ? Text("SEGMENT: WHOLE RUN", "山段: 整局") :
                    Text("SEGMENT: " + SegmentName(segment), "山段: " + SegmentName(segment));
            }
            if (_confidenceFilterText != null)
            {
                string[] en = { "ATTRIBUTION: ALL", "ATTRIBUTION: CERTAIN", "ATTRIBUTION: LIKELY", "ATTRIBUTION: AMBIGUOUS" };
                string[] zh = { "归因: 全部", "归因: 确定", "归因: 可能", "归因: 不明确" };
                _confidenceFilterText.text = Text(en[Mathf.Clamp(_confidenceFilter, 0, 3)], zh[Mathf.Clamp(_confidenceFilter, 0, 3)]);
            }
            if (_resourceFilterText != null)
            {
                string[] en = { "RESOURCE: ALL", "RESOURCE: USES", "RESOURCE: FUEL", "RESOURCE: REMAINING", "RESOURCE: COOKED", "RESOURCE: USED", "RESOURCE: FLARE", "RESOURCE: POWER", "RESOURCE: PETTER USES" };
                string[] zh = { "资源: 全部", "资源: 使用次数", "资源: 燃料", "资源: 剩余比例", "资源: 烹饪量", "资源: 已使用", "资源: 信号棒", "资源: 电源", "资源: 绳枪次数" };
                int index = Mathf.Clamp(_resourceFilter, 0, ResourceFilters.Length - 1);
                _resourceFilterText.text = Text(en[index], zh[index]);
            }
        }

        private static void RenderDetails()
        {
            if (_displayedAnalysis == null || _dashboardContent == null) return;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            RefreshDetailsLabels();
            RefreshReportControls();
            RenderDashboard();
            if (StateKeeperPlugin.IsDebugLogging && watch.Elapsed.TotalMilliseconds >= 16) StateKeeperPlugin.LogInfo("UI report rebuild: " + watch.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture) + "ms");
        }

        private static void CreateDashboard(Transform parent)
        {
            GameObject scrollObject = new GameObject("DashboardScroll", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
            scrollObject.transform.SetParent(parent, false);
            RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
            Stretch(scrollRect);
            scrollRect.offsetMin = new Vector2(90f, 110f);
            scrollRect.offsetMax = new Vector2(-90f, -315f);
            Image hitArea = scrollObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            _dashboardScroll = scrollObject.GetComponent<ScrollRect>();
            _dashboardScroll.horizontal = false;
            _dashboardScroll.vertical = true;
            _dashboardContent = new GameObject("DashboardContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            _dashboardContent.transform.SetParent(scrollObject.transform, false);
            RectTransform contentRect = _dashboardContent.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f); contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(.5f, 1f); contentRect.sizeDelta = new Vector2(0f, 0f);
            VerticalLayoutGroup layout = _dashboardContent.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 12, 24); layout.spacing = 16f;
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            _dashboardContent.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _dashboardScroll.content = contentRect;
            _dashboardScroll.viewport = scrollRect;
        }

        private static void ClearDashboard()
        {
            if (_dashboardContent == null) return;
            for (int i = _dashboardContent.transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = _dashboardContent.transform.GetChild(i).gameObject;
                child.SetActive(false);
                if (child.GetComponent<StateKeeperAxes>() != null && ChartRows.Count < 32)
                { child.transform.SetParent(_detailsPage.transform, false); ChartRows.Enqueue(child.GetComponent<RectTransform>()); }
                else UnityEngine.Object.Destroy(child);
            }
        }

        private static void RenderDashboard()
        {
            ClearDashboard();
            if (_displayedAnalysis == null) return;
            if (_focusedEvent != null) { RenderEventEvidence(); RefreshDashboardNavigation(); return; }
            StringBuilder body = new StringBuilder();
            AnalysisOverview o = _displayedAnalysis.overview;
            if (_detailsView == 0)
            {
                body.AppendLine(Text("RUN OVERVIEW", "整局概览"));
                body.AppendLine(Text("Outcome: ", "结果: ") + LocalizedOutcome(o.outcome));
                body.AppendLine(Text("Duration: ", "时长: ") + FormatDuration(o.durationSeconds) + "   " + Text("Players: ", "玩家: ") + o.playerCount);
                body.AppendLine(Text("Jumps: ", "跳跃: ") + o.jumpCount + "   " + Text("Deaths: ", "死亡: ") + o.deathCount + "   " + Text("Passed out: ", "昏迷: ") + o.passedOutCount);
                body.AppendLine(Text("Item observations: ", "物品观察: ") + o.itemObservationCount + "   " + Text("Valid distance samples: ", "有效距离采样: ") + o.validDistanceCount);
                foreach (AnalysisPlayer p in FilterPlayers()) body.AppendLine(LifecycleSummary(p));
                body.AppendLine(Text("Ascent: ", "难度：") + (_displayedAnalysis.hasAscentLevel ? _displayedAnalysis.ascentLevel.ToString() : Text("unknown", "未知")) +
                    Text(" | Progress points: ", " | 进度点：") + _displayedAnalysis.mountainSegments.Count);
                AppendAssistanceSummary(body);
                AppendQuality(body, _displayedAnalysis.quality);
                AddDashboardText(body.ToString(), 22f);
                AddReportHighlights();
            }
            else if (_detailsView == 1)
            {
                AddDashboardText(Text("STAMINA: green = regular | gold = extra | grey = capacity", "体力：绿 = 普通 | 金 = 额外 | 灰 = 普通容量"), 20f);
                foreach (AnalysisPlayer p in FilterPlayers()) { AddPlayerReport(p); AddPlayerChart(p); }
            }
            else if (_detailsView == 2)
            {
                RenderRelations();
            }
            else if (_detailsView == 3)
            {
                RenderItemMode();
            }
            else if (_detailsView == 4) RenderTimeline();
            else RenderQualityReport();
            RefreshDashboardNavigation();
        }

        private static void AddDashboardText(string value, float size)
        {
            TextMeshProUGUI text = CreateText(_dashboardContent.transform, "DashboardText", size, TextAlignmentOptions.Left);
            text.text = value; text.rectTransform.sizeDelta = new Vector2(0f, 0f);
            text.raycastTarget = false;
            text.gameObject.AddComponent<LayoutElement>().minHeight = size * 1.5f;
        }

        private static void AddPlayerChart(AnalysisPlayer player)
        {
            AddDashboardText(DisplayPlayerName(player), 20f);
            float start, end; GetTimeRange(out start, out end);
            List<AnalysisSeriesPoint> points = player.staminaSeries.Where(p => p.endTime >= start && p.time <= end && EpochMatches(p.epoch)).ToList();
            if (points.Count == 0) { AddDashboardText(Text("No samples in this range", "此范围没有采样"), 18f); return; }
            float ceiling = Mathf.Max(1f, points.Max(p => Mathf.Max(p.maxStamina, Mathf.Max(p.regularMaximum, p.extraMaximum))));
            StateKeeperChart chart = AddAxisChart(ceiling, start, end, "");
            List<StateKeeperChart.Series> series = new List<StateKeeperChart.Series>();
            StateKeeperChart.Series regular = new StateKeeperChart.Series { color = new Color(.25f, 1f, .35f) };
            StateKeeperChart.Series extra = new StateKeeperChart.Series { color = new Color(1f, .75f, .2f) };
            StateKeeperChart.Series capacity = new StateKeeperChart.Series { color = Color.gray };
            foreach (AnalysisSeriesPoint p in points)
            {
                float x = (p.endTime - start) / Mathf.Max(.01f, end - start);
                if (p.breakBefore || p.dead) foreach (var line in new[] { regular, extra, capacity }) line.points.Add(new Vector2(x, float.NaN));
                if (p.dead) continue;
                regular.points.Add(new Vector2(x, p.regularMinimum / ceiling)); regular.points.Add(new Vector2(x, p.regularMaximum / ceiling)); regular.points.Add(new Vector2(x, p.regularStamina / ceiling));
                extra.points.Add(new Vector2(x, p.extraMinimum / ceiling)); extra.points.Add(new Vector2(x, p.extraMaximum / ceiling)); extra.points.Add(new Vector2(x, p.extraStamina / ceiling)); capacity.points.Add(new Vector2(x, p.maxStamina / ceiling));
            }
            series.Add(regular); series.Add(extra); series.Add(capacity);
            chart.SetSeries(series, SelectedJumps(player).Select(t => (t - start) / Mathf.Max(.01f, end - start)));
            chart.OnSelected = ratio =>
            {
                float time = start + ratio * (end - start); var point = points.OrderBy(p => Math.Abs(p.endTime - time)).First();
                OpenEvent(new ReportEvent { evidence = point.evidence, playerIndex = player.playerIndex, time = time, endTime = time, kind = "TimeSelection", confidence = "Ambiguous" });
            };
            AddStatusSnapshot(points);
        }

        private static void AddDistanceChart(AnalysisDistancePair pair)
        {
            float start, end; GetTimeRange(out start, out end);
            List<AnalysisDistancePoint> points = pair.series.Where(p => p.endTime >= start && p.time <= end && EpochMatches(p.epoch)).ToList();
            if (points.Count == 0) return;
            AddDashboardText(PlayerName(pair.playerA) + " <-> " + PlayerName(pair.playerB), 20f);
            float ceiling = Mathf.Max(100f, points.Max(p => p.maximum));
            StateKeeperChart chart = AddAxisChart(ceiling, start, end, "m");
            StateKeeperChart.Series line = new StateKeeperChart.Series { color = new Color(.3f, .8f, 1f) };
            foreach (AnalysisDistancePoint p in points)
            {
                float x = (p.time - start) / Mathf.Max(.01f, end - start);
                if (p.breakBefore) line.points.Add(new Vector2(x, float.NaN));
                line.points.Add(new Vector2(x, p.minimum / ceiling));
                line.points.Add(new Vector2(x, p.maximum / ceiling));
                line.points.Add(new Vector2((p.endTime - start) / Mathf.Max(.01f, end - start), p.value / ceiling));
            }
            chart.SetSeries(new[] { line });
            chart.OnSelected = ratio =>
            {
                float time = start + ratio * (end - start); var point = points.OrderBy(p => Math.Abs(p.time - time)).First();
                OpenEvent(new ReportEvent { evidence = point.evidence, playerIndex = pair.playerA, otherPlayerIndex = pair.playerB, time = time, endTime = time, kind = "TimeSelection", confidence = "Ambiguous" });
            };
            var summary = new AnalysisDistancePair { intervals = pair.intervals, intervalEpochs = pair.intervalEpochs };
            RunAnalysisEngine.SummarizeDistance(summary, start, end, _evidenceEpoch);
            if (summary.validSeconds > 0)
                AddDashboardText(Text("Median / P90 / P99: ", "中位 / P90 / P99：") + summary.median.ToString("0.0") + " / " + summary.p90.ToString("0.0") + " / " + summary.p99.ToString("0.0") + "m\n" +
                    Text("Time >25 / >50 / >100m: ", "时长占比 >25 / >50 / >100米：") + Percent(summary.over25Ratio) + " / " + Percent(summary.over50Ratio) + " / " + Percent(summary.over100Ratio), 18f);
        }

        private static void CyclePlayerFilter()
        {
            if (_displayedAnalysis == null || _displayedAnalysis.players == null || _displayedAnalysis.players.Count == 0) return;
            _playerFilter++;
            if (_playerFilter >= _displayedAnalysis.players.Count) _playerFilter = -1;
            _itemPage = 0;
            RenderDetails();
        }

        private static void CycleSegmentFilter()
        {
            if (_displayedAnalysis == null || _displayedAnalysis.segments == null || _displayedAnalysis.segments.Count == 0)
            {
                _segmentFilter = -1;
                RefreshDetailsLabels();
                return;
            }
            do { _segmentFilter++; }
            while (_segmentFilter < _displayedAnalysis.segments.Count && (!_displayedAnalysis.segments[_segmentFilter].hasStartTime || !_displayedAnalysis.segments[_segmentFilter].hasEndTime));
            if (_segmentFilter >= _displayedAnalysis.segments.Count) _segmentFilter = -1;
            _rangeStart = _rangeEnd = null; _evidenceEpoch = null;
            _itemPage = 0;
            RenderDetails();
        }

        private static bool ItemMatches(AnalysisItemObservation item)
        {
            if (_playerFilter >= 0 && item.playerIndex != _displayedAnalysis.players[_playerFilter].playerIndex && item.targetPlayerIndex != _displayedAnalysis.players[_playerFilter].playerIndex && item.actorPlayerIndex != _displayedAnalysis.players[_playerFilter].playerIndex) return false;
            if (_confidenceFilter > 0 && !string.Equals(item.attribution, ConfidenceName(_confidenceFilter), StringComparison.OrdinalIgnoreCase)) return false;
            if (_resourceFilter > 0 && !string.Equals(item.resourceKey, ResourceFilters[_resourceFilter], StringComparison.OrdinalIgnoreCase)) return false;
            return InSelectedRange(item.time) && EpochMatches(item.epoch) && SearchItem(item);
        }

        private static IEnumerable<AnalysisPlayer> FilterPlayers()
        {
            AnalysisPlayer selected = GetSelectedPlayer();
            return selected == null ? _displayedAnalysis.players : new[] { selected };
        }

        private static AnalysisPlayer GetSelectedPlayer()
        {
            return _displayedAnalysis == null || _playerFilter < 0 || _playerFilter >= _displayedAnalysis.players.Count ? null : _displayedAnalysis.players[_playerFilter];
        }

        private static AnalysisSegment GetSelectedSegment()
        {
            return _displayedAnalysis == null || _segmentFilter < 0 || _segmentFilter >= _displayedAnalysis.segments.Count ? null : _displayedAnalysis.segments[_segmentFilter];
        }

        private static string DisplayPlayerName(AnalysisPlayer player)
        {
            return player == null ? Text("ALL", "全部") : (string.IsNullOrWhiteSpace(player.displayName) ? "P" + player.playerIndex : player.displayName);
        }

        private static string PlayerName(int index)
        {
            AnalysisPlayer player = _displayedAnalysis == null ? null : _displayedAnalysis.players.FirstOrDefault(p => p.playerIndex == index);
            return player == null ? (index < 0 ? Text("Unknown", "未知") : "P" + index.ToString(CultureInfo.InvariantCulture)) : DisplayPlayerName(player);
        }

        private static string SegmentName(AnalysisSegment segment)
        {
            return segment == null ? Text("WHOLE RUN", "整局") : segment.titleKey == "INITIAL" ? Text("INITIAL STAGE", "起始阶段") : Text(segment.titleKey ?? segment.displayName, segment.displayName ?? segment.titleKey);
        }

        private static void GetTimeRange(out float start, out float end)
        {
            AnalysisSegment segment = GetSelectedSegment();
            start = segment == null || !segment.hasStartTime ? 0f : segment.startTime;
            end = segment == null || !segment.hasEndTime ? _displayedAnalysis.overview.durationSeconds : segment.endTime;
            if (_rangeStart.HasValue) start = Math.Max(start, _rangeStart.Value);
            if (_rangeEnd.HasValue) end = Math.Min(end, _rangeEnd.Value);
            if (end < start) end = start;
        }

        private static bool InSelectedRange(float time)
        {
            float start, end;
            GetTimeRange(out start, out end);
            return time >= start && time <= end;
        }

        private static bool InSelectedRange(float startTime, float endTime)
        {
            float start, end;
            GetTimeRange(out start, out end);
            return endTime >= start && startTime <= end;
        }

        private static void AppendQuality(StringBuilder body, AnalysisQuality quality)
        {
            if (quality == null) return;
            body.AppendLine();
            body.AppendLine(Text("QUALITY", "数据质量"));
            body.AppendLine(Text("missing/corrupt chunks: ", "缺失/损坏区块: ") + quality.missingChunkCount + "/" + quality.corruptChunkCount + " | " + Text("time backwards: ", "时间倒退: ") + quality.timeBackwardsCount + " | " + Text("missing player frames: ", "缺失玩家帧: ") + quality.missingPlayerSampleCount);
            body.AppendLine(Text("invalid positions: ", "无效坐标: ") + quality.invalidPositionCount + " | " + Text("non-finite values: ", "非有限数值: ") + quality.nonFiniteValueCount + " | " + Text("excluded distances: ", "排除距离: ") + quality.excludedDistanceCount);
            foreach (string warning in (quality.warnings ?? new List<string>()).Take(8)) body.AppendLine("! " + warning);
        }

        private static string LocalizedOutcome(string outcome)
        {
            if (outcome == RunOutcome.Victory.ToString()) return Text("VICTORY", "胜利");
            if (outcome == RunOutcome.Defeat.ToString()) return Text("DEFEAT", "失败");
            if (outcome == RunOutcome.Aborted.ToString()) return Text("ABORTED", "中止");
            return Text("UNKNOWN", "未知");
        }

        private static string ConfidenceName(int index)
        {
            return index == 1 ? "Certain" : index == 2 ? "Likely" : "Ambiguous";
        }

        private static string ConfidenceText(string value)
        {
            if (value == "Certain") return Text("Certain", "确定");
            if (value == "Likely") return Text("Likely", "可能");
            if (value == "Ambiguous") return Text("Ambiguous", "不明确");
            return value ?? Text("Unknown", "未知");
        }

        private static string FormatDuration(float seconds)
        {
            return TimeSpan.FromSeconds(Math.Max(0f, seconds)).ToString(seconds >= 3600f ? "hh\\:mm\\:ss" : "mm\\:ss", CultureInfo.InvariantCulture);
        }

        private static string Percent(float value)
        {
            return (Math.Max(0f, Math.Min(1f, value)) * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        private static void BeginRename(RunIndexEntry entry)
        {
            if (entry == null || _historyPage == null) return;
            if (_renamePanel == null) CreateRenamePanel(_historyPage.transform);
            _editingRunId = entry.runId;
            _renameFocus = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            _renamePanel.transform.SetAsLastSibling();
            _nameInput.text = entry.customName ?? string.Empty;
            _renamePanel.SetActive(true);
            Canvas.ForceUpdateCanvases();
            UpdateRenameMessage(false);
            _modalDisabled.Clear();
            foreach (Selectable control in _historyPage.GetComponentsInChildren<Selectable>(false))
                if (!control.transform.IsChildOf(_renamePanel.transform) && control.interactable) { control.interactable = false; _modalDisabled.Add(control); }
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(_nameInput.gameObject);
            _nameInput.ActivateInputField();
            if (StateKeeperPlugin.IsDebugLogging) StateKeeperPlugin.LogInfo("Rename opened: active=" + _renamePanel.activeInHierarchy + ", bounds=" + _renamePanel.GetComponent<RectTransform>().rect);
        }

        private static void ConfirmRename()
        {
            string renamedId = _editingRunId;
            try
            {
                if (string.IsNullOrEmpty(_editingRunId) || !StateKeeperPlugin.Store.RenameRun(_editingRunId, _nameInput.text)) { UpdateRenameMessage(true); return; }
                RefreshRenamedEntry(_editingRunId);
            }
            catch { UpdateRenameMessage(true); return; }
            CancelRename();
            RefreshHistory();
            GameObject row = Rows.FirstOrDefault(r => r != null && r.name == "RunRow_" + renamedId);
            if (row != null) UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(row);
        }

        private static void CancelRename()
        {
            _editingRunId = null;
            if (_renamePanel != null) _renamePanel.SetActive(false);
            foreach (Selectable control in _modalDisabled) if (control != null) control.interactable = true;
            _modalDisabled.Clear();
            if (_renameFocus != null) UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(_renameFocus);
        }

        internal static bool DismissRename()
        {
            if (_renamePanel == null || !_renamePanel.activeSelf) return false;
            CancelRename(); return true;
        }

        private static StateKeeperButton CreateButton(Transform parent, string name, string text)
        {
            GameObject template = _parentPage == null || _parentPage.resumeButton == null ? null : _parentPage.resumeButton.gameObject;
            if (template != null)
            {
                StateKeeperButton result = StateKeeperButton.Create(template, "StateKeeper_" + name, parent);
                result.SetText(text);
                result.Button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
                return result;
            }
            GameObject objectValue = new GameObject("StateKeeper_" + name, typeof(RectTransform), typeof(Image), typeof(Button));
            objectValue.transform.SetParent(parent, false);
            objectValue.GetComponent<Image>().color = new Color(0.22f, 0.22f, 0.22f, 0.96f);
            StateKeeperButton fallback = CreateFallbackButton(objectValue, text);
            return fallback;
        }

        private static StateKeeperButton CreateFallbackButton(GameObject value, string text)
        {
            TextMeshProUGUI label = CreateText(value.transform, "Text", 20f, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            StateKeeperButton result = new StateKeeperButton(value);
            result.SetText(text);
            return result;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, float size, TextAlignmentOptions alignment)
        {
            GameObject value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            value.transform.SetParent(parent, false);
            TextMeshProUGUI text = value.GetComponent<TextMeshProUGUI>();
            EnsureFont();
            text.font = _font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.enableAutoSizing = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static void EnsureFont()
        {
            if (_font != null) return;
            if (GUIManager.instance != null && GUIManager.instance.heroDayText != null)
                _font = GUIManager.instance.heroDayText.font;
        }

        private static void AddImage(Transform parent, string name, Color color, bool first)
        {
            GameObject value = new GameObject(name, typeof(RectTransform), typeof(Image));
            value.transform.SetParent(parent, false);
            Image image = value.GetComponent<Image>();
            image.color = color;
            Stretch(value.GetComponent<RectTransform>());
            if (first) value.transform.SetAsFirstSibling();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rect, float minX, float minY, float maxX, float maxY, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.pivot = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Transition(UIPage page)
        {
            if (_handler != null && page != null)
                ((UIPageHandler)_handler).TransistionToPage(page, new SetActivePageTransistion());
        }

        private static string Text(string english, string chinese)
        {
            return (int)LocalizedText.CURRENT_LANGUAGE == 9 ? chinese : english;
        }

        private static string FormatDate(string value)
        {
            DateTime date;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date))
                return date.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            return value ?? string.Empty;
        }

        private static void RefreshLanguage()
        {
            if (_openButton != null) _openButton.SetText(Text("STATE KEEPER", "状态分析"));
            if (_recentTabText != null) _recentTabText.text = Text("RECENT", "最近记录");
            if (_favoritesTabText != null) _favoritesTabText.text = Text("FAVORITES", "收藏记录");
            if (_historyBackText != null) _historyBackText.text = Text("BACK", "返回");
            if (_renameLabel != null) _renameLabel.text = Text("EXPEDITION NAME", "远征名称");
            if (_confirmText != null) _confirmText.text = Text("CONFIRM", "确认");
            if (_cancelText != null) _cancelText.text = Text("CANCEL", "取消");
            if (_detailsBackText != null) _detailsBackText.text = Text("BACK", "返回");
            RefreshReportLanguage();
            if (_historyPage != null && _historyPage.gameObject.activeInHierarchy) RefreshHistory();
            if (_detailsPage != null && _detailsPage.gameObject.activeInHierarchy)
            {
                RefreshDetailsLabels();
                if (_displayedAnalysis != null) RenderDetails();
                else if (StateKeeperPlugin.Analysis != null && StateKeeperPlugin.Analysis.IsRunning)
                    _detailsStatus.text = Text("ANALYZING", "正在分析");
            }
        }

        private static void Reset()
        {
            if (_openButton != null && _openButton.IsAlive)
                UnityEngine.Object.Destroy(_openButton.GameObject);
            if (_historyPage != null) UnityEngine.Object.Destroy(_historyPage.gameObject);
            if (_detailsPage != null) UnityEngine.Object.Destroy(_detailsPage.gameObject);
            _historyPage = null;
            _detailsPage = null;
            _openButton = null;
            _dashboardContent = null;
            _dashboardScroll = null;
            _selectedEntry = null;
            _displayedAnalysis = null;
            _analysisRendered = false;
            DetailsTabTexts.Clear();
            Rows.Clear();
            ResetReports();
            ChartRows.Clear();
        }
    }
}
