using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

namespace PeakMapBrowser
{
    internal sealed class PeakMapWindow
    {
        private readonly MonoBehaviour _runner;
        private readonly ManualLogSource _log;
        private readonly PeakMapApiClient _api;
        private readonly int _pageSize;
        private readonly Dictionary<string, Texture2D> _thumbnails = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, MapDownloadInfo> _downloadInfoCache = new Dictionary<string, MapDownloadInfo>();

        private bool _visible;
        private bool _cursorCaptured;
        private bool _previousCursorVisible;
        private CursorLockMode _previousCursorLockMode;
        private GameObject _inputBlocker;
        private bool _stylesReady;
        private bool _loadingMaps;
        private bool _loadingVersions;
        private bool _downloading;
        private bool _uploading;
        private bool _liking;
        private bool _uploadOpen;
        private bool _loginOpen;
        private bool _accountOpen;
        private bool _communityDetailOpen;
        private bool _downloadConfirmOpen;
        private bool _loadingAccountMaps;
        private bool _savingAccountMap;
        private bool _deletingAccountMap;
        private bool _refreshingSession;
        private bool _loadedOnce;
        private int _topLayerOpenedFrame = -1;
        private float _nextSessionRefreshCheckTime;

        private Rect _windowRect;
        private Vector2 _mapScroll;
        private Vector2 _uploadSaveScroll;
        private Vector2 _detailScroll;
        private Vector2 _accountScroll;
        private Vector2 _accountDescriptionScroll;

        private List<MapEntry> _maps = new List<MapEntry>();
        private List<MapEntry> _accountMaps = new List<MapEntry>();
        private List<ModVersionEntry> _versions = new List<ModVersionEntry>();
        private PaginationInfo _pagination;
        private int _selectedMapIndex;
        private int _selectedAccountMapIndex = -1;
        private int _page = 1;
        private string _query = string.Empty;
        private string _sort = "newest";
        private string _versionFilter = string.Empty;
        private string _languageMode;
        private string _language;
        private string _languageSource;
        private string _languageRawValue;
        private string _toggleKeyLabel;
        private float _nextLanguageCheckTime;
        private string _status = string.Empty;
        private string _toast = string.Empty;
        private float _toastUntil;
        private float _downloadInfoCacheUntil;

        private string[] _localSaves = new string[0];
        private readonly List<int> _filteredLocalSaveIndexes = new List<int>();
        private int _selectedLocalSave;
        private string _localSaveFilter = string.Empty;
        private string _localSaveError = string.Empty;
        private float _nextLocalSaveScanTime;
        private int _selectedUploadVersion;
        private bool _uploadVersionDropdownOpen;
        private Vector2 _uploadVersionScroll;
        private bool _uploadSaveDropdownOpen;
        private string[] _localImages = new string[0];
        private readonly List<int> _filteredLocalImageIndexes = new List<int>();
        private int _selectedLocalImage = -1;
        private string _localImageFilter = string.Empty;
        private string _localImageError = string.Empty;
        private bool _uploadImageDropdownOpen;
        private Vector2 _uploadImageScroll;
        private bool _imagePickerOpen;
        private string[] _imageRootPaths = new string[0];
        private int _imagePickerRootIndex;
        private string _imagePickerDirectory = string.Empty;
        private string[] _imagePickerDirs = new string[0];
        private string[] _imagePickerFiles = new string[0];
        private string _imagePickerSelected = string.Empty;
        private string _imagePickerError = string.Empty;
        private Vector2 _imagePickerScroll;
        private string _uploadName = string.Empty;
        private string _uploadAuthor = string.Empty;
        private string _uploadDescription = string.Empty;
        private string _loginEmail = string.Empty;
        private string _loginPassword = string.Empty;
        private string _editName = string.Empty;
        private string _editAuthor = string.Empty;
        private string _editVersion = string.Empty;
        private string _editDescription = string.Empty;
        private bool _editReplaceJson;
        private bool _accountJsonDropdownOpen;
        private bool _accountVersionDropdownOpen;
        private Vector2 _accountVersionScroll;
        private bool _editReplaceImage;
        private bool _editRemoveImage;
        private string _deleteConfirmMapId = string.Empty;
        private string _downloadConfirmMapId = string.Empty;

        private GUIStyle _rootStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _panelStrongStyle;
        private GUIStyle _detailMetaStyle;
        private GUIStyle _detailDescriptionStyle;
        private GUIStyle _cardStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _primaryButtonStyle;
        private GUIStyle _iconButtonStyle;
        private GUIStyle _inputStyle;
        private GUIStyle _textAreaStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _h2Style;
        private GUIStyle _labelStyle;
        private GUIStyle _statLabelStyle;
        private GUIStyle _statValueStyle;
        private GUIStyle _cardStatsStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _cardDescStyle;
        private GUIStyle _detailTextStyle;
        private GUIStyle _detailTitleStyle;
        private GUIStyle _tinyStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _apiBadgeStyle;
        private GUIStyle _pagePillStyle;
        private GUIStyle _toastStyle;
        private GUIStyle _thumbStyle;
        private GUIStyle _sidebarButtonStyle;
        private GUIStyle _sidebarSelectedStyle;
        private GUIStyle _statBoxStyle;
        private GUIStyle _modalBackdropStyle;
        private GUIStyle _dangerStyle;
        private Texture2D _placeholderThumb;
        private Font _uiFont;

        public PeakMapWindow(MonoBehaviour runner, ManualLogSource log, string apiBaseUrl, string language, int pageSize, string toggleKeyLabel)
        {
            _runner = runner;
            _log = log;
            _toggleKeyLabel = string.IsNullOrWhiteSpace(toggleKeyLabel) ? "/" : toggleKeyLabel;
            _languageMode = language;
            PeakMapLanguageResult languageResult = PeakMapLanguage.ResolveDetailed(language, log);
            _language = languageResult.Language;
            _languageSource = languageResult.Source;
            _languageRawValue = languageResult.RawValue;
            _api = new PeakMapApiClient(runner, log, apiBaseUrl, _language);
            _pageSize = pageSize;
            _windowRect = new Rect(0f, 0f, 960f, 640f);
            _status = T("按 " + _toggleKeyLabel + " 打开或关闭地图库", "Press " + _toggleKeyLabel + " to open or close the map browser");
            _log.LogInfo("PEAK Map Browser language resolved: lang=" + _language + ", mode=" + languageResult.Mode + ", source=" + _languageSource + ", raw=" + _languageRawValue);
        }

        public void Toggle()
        {
            SetVisible(!_visible);
            if (_visible && !_loadedOnce)
            {
                RefreshAll();
            }
        }

        private void SetVisible(bool visible)
        {
            if (_visible == visible)
            {
                return;
            }

            _visible = visible;
            if (visible)
            {
                _log.LogInfo("Map browser opened.");
                CaptureCursor();
                EnsureInputBlocker();
            }
            else
            {
                _log.LogInfo("Map browser closed.");
                RestoreCursor();
                SetInputBlockerActive(false);
                _uploadOpen = false;
                _imagePickerOpen = false;
                _loginOpen = false;
                _accountOpen = false;
                _communityDetailOpen = false;
                _uploadVersionDropdownOpen = false;
                _uploadImageDropdownOpen = false;
                _uploadSaveDropdownOpen = false;
            }
        }

        private void CaptureCursor()
        {
            if (!_cursorCaptured)
            {
                _previousCursorVisible = Cursor.visible;
                _previousCursorLockMode = Cursor.lockState;
                _cursorCaptured = true;
            }

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void RestoreCursor()
        {
            if (!_cursorCaptured)
            {
                return;
            }

            Cursor.visible = _previousCursorVisible;
            Cursor.lockState = _previousCursorLockMode;
            _cursorCaptured = false;
        }

        private void EnsureInputBlocker()
        {
            if (_inputBlocker == null)
            {
                _inputBlocker = new GameObject("PeakMapBrowser_InputBlocker");
                UnityEngine.Object.DontDestroyOnLoad(_inputBlocker);

                Canvas canvas = _inputBlocker.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = short.MaxValue;

                _inputBlocker.AddComponent<GraphicRaycaster>();

                GameObject imageGo = new GameObject("Blocker");
                imageGo.transform.SetParent(_inputBlocker.transform, false);
                Image image = imageGo.AddComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0f);
                image.raycastTarget = true;
                RectTransform rt = image.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            SetInputBlockerActive(true);
        }

        private void SetInputBlockerActive(bool active)
        {
            if (_inputBlocker != null && _inputBlocker.activeSelf != active)
            {
                _inputBlocker.SetActive(active);
            }
        }

        public void Update()
        {
            RefreshLanguageIfNeeded();
            RefreshSessionIfNeeded();

            if (!_visible)
            {
                return;
            }

            CaptureCursor();
            EnsureInputBlocker();
            UnityEngine.Input.ResetInputAxes();
        }

        private void RefreshLanguageIfNeeded()
        {
            if (Time.unscaledTime < _nextLanguageCheckTime)
            {
                return;
            }

            _nextLanguageCheckTime = Time.unscaledTime + 2f;
            PeakMapLanguageResult nextResult = PeakMapLanguage.ResolveDetailed(_languageMode, _log);
            bool languageChanged = !string.Equals(nextResult.Language, _language, StringComparison.OrdinalIgnoreCase);
            bool sourceChanged = !string.Equals(nextResult.Source, _languageSource, StringComparison.Ordinal)
                || !string.Equals(nextResult.RawValue, _languageRawValue, StringComparison.Ordinal);

            if (!languageChanged && !sourceChanged)
            {
                return;
            }

            string oldLanguage = _language;
            string oldSource = _languageSource;
            string oldRaw = _languageRawValue;
            _language = nextResult.Language;
            _languageSource = nextResult.Source;
            _languageRawValue = nextResult.RawValue;

            if (languageChanged)
            {
                _api.SetLanguage(_language);
                _status = T("语言已切换为中文", "Language switched to English");
                ShowToast(_status);
            }

            _log.LogInfo("PEAK Map Browser language resolved: lang=" + _language
                + ", mode=" + nextResult.Mode
                + ", source=" + _languageSource
                + ", raw=" + _languageRawValue
                + " (previous lang=" + oldLanguage + ", source=" + oldSource + ", raw=" + oldRaw + ")");
        }

        public void Dispose()
        {
            SetVisible(false);
            if (_inputBlocker != null)
            {
                UnityEngine.Object.Destroy(_inputBlocker);
                _inputBlocker = null;
            }
        }

        public void Draw()
        {
            if (!_visible)
            {
                return;
            }

            Color previousColor = GUI.color;
            Color previousContentColor = GUI.contentColor;
            Color previousBackgroundColor = GUI.backgroundColor;
            bool previousEnabled = GUI.enabled;
            int previousDepth = GUI.depth;
            GUI.color = Color.white;
            GUI.contentColor = Color.white;
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            try
            {
                EnsureStyles();
                CenterWindow();

                GUI.depth = -100;
                DrawDimBackground();
                DrawSolidBackground(_windowRect, new Color(0.010f, 0.016f, 0.012f, 1f));
                GUI.Box(_windowRect, GUIContent.none, _rootStyle);
                bool modalLayerOpen = _communityDetailOpen || _downloadConfirmOpen;
                bool guiEnabledBeforeModal = GUI.enabled;
                if (modalLayerOpen)
                {
                    GUI.enabled = false;
                }

                if (!_uploadOpen && !_imagePickerOpen && !_loginOpen)
                {
                    DrawHeader();
                    DrawSidebar();
                    if (_accountOpen)
                    {
                        DrawAccountPage();
                    }
                    else
                    {
                        DrawContent();
                    }
                    DrawFooter();
                }
                GUI.enabled = guiEnabledBeforeModal;

                if (_communityDetailOpen && !_downloadConfirmOpen && !_imagePickerOpen && !_loginOpen && !_uploadOpen)
                {
                    DrawCommunityDetailModal();
                }

                if (_downloadConfirmOpen && !_imagePickerOpen && !_loginOpen && !_uploadOpen)
                {
                    DrawDownloadConfirmModal();
                }

                if (_uploadOpen && !_imagePickerOpen)
                {
                    DrawUploadModal();
                }
                if (_loginOpen && !_imagePickerOpen)
                {
                    DrawLoginModal();
                }
                if (_imagePickerOpen)
                {
                    DrawImagePickerModal();
                }
                DrawToast();

                ConsumeOverlayEvents();
            }
            finally
            {
                GUI.color = previousColor;
                GUI.contentColor = previousContentColor;
                GUI.backgroundColor = previousBackgroundColor;
                GUI.enabled = previousEnabled;
                GUI.depth = previousDepth;
            }
        }

        private void ConsumeOverlayEvents()
        {
            Event e = Event.current;
            if (e == null)
            {
                return;
            }

            if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.MouseDrag || e.type == EventType.ScrollWheel)
            {
                e.Use();
            }
        }

        private void BlockTopLayerInputThisFrame()
        {
            _topLayerOpenedFrame = Time.frameCount;
        }

        private bool IsTopLayerInputBlocked()
        {
            return _topLayerOpenedFrame == Time.frameCount;
        }

        private void CenterWindow()
        {
            float width = Mathf.Round(Mathf.Min(1160f, Screen.width - 24f));
            float height = Mathf.Round(Mathf.Min(720f, Screen.height - 24f));
            _windowRect = new Rect(Mathf.Round((Screen.width - width) * 0.5f), Mathf.Round((Screen.height - height) * 0.5f), width, height);
        }

        private void DrawDimBackground()
        {
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.42f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void DrawSolidBackground(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = old;
        }

        private void DrawHeader()
        {
            Rect header = new Rect(_windowRect.x, _windowRect.y, _windowRect.width, 64f);
            GUI.Box(header, GUIContent.none, _panelStrongStyle);

            Rect mark = new Rect(header.x + 22f, header.y + 17f, 30f, 30f);
            GUI.Box(mark, GUIContent.none, _badgeStyle);
            GUI.Label(new Rect(mark.x + 8f, mark.y - 2f, 22f, 28f), "/", _titleStyle);

            GUI.Label(new Rect(header.x + 62f, header.y + 11f, 190f, 16f), T("远征数据库", "EXPEDITION DATABASE"), _tinyStyle);
            GUI.Label(new Rect(header.x + 62f, header.y + 27f, 210f, 30f), T("PEAK 地图库", "PEAK Maps"), _titleStyle);

            float y = header.y + 17f;
            float closeW = 38f;
            float uploadW = 118f;
            float accountW = 112f;
            float refreshW = 38f;
            float gap = 8f;
            float x = header.xMax - closeW - accountW - uploadW - refreshW - gap * 4f - 22f;

            if (GUI.Button(new Rect(x, y, uploadW, 34f), T("上传地图", "Upload"), _primaryButtonStyle))
            {
                OpenUpload();
            }
            x += uploadW + gap;

            if (GUI.Button(new Rect(x, y, refreshW, 34f), "↻", _iconButtonStyle))
            {
                RefreshAll();
            }
            x += refreshW + gap;

            string accountLabel = _api.IsSignedIn ? ShortAccountName(_api.Session.DisplayName) : T("登录", "Sign in");
            if (GUI.Button(new Rect(x, y, accountW, 34f), accountLabel, _buttonStyle))
            {
                if (_api.IsSignedIn)
                {
                    OpenAccount();
                }
                else
                {
                    OpenLogin();
                }
            }
            x += accountW + gap;

            if (GUI.Button(new Rect(x, y, closeW, 34f), "×", _iconButtonStyle))
            {
                SetVisible(false);
            }

            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == "PeakMapSearch")
            {
                _page = 1;
                FetchMaps();
                e.Use();
            }
        }

        private void DrawSidebar()
        {
            Rect sidebar = new Rect(_windowRect.x, _windowRect.y + 64f, 230f, _windowRect.height - 92f);
            GUI.Box(sidebar, GUIContent.none, _panelStyle);

            GUI.Label(new Rect(sidebar.x + 24f, sidebar.y + 22f, 160f, 16f), T("导航", "NAVIGATION"), _tinyStyle);
            float y = sidebar.y + 58f;
            DrawNavButton(new Rect(sidebar.x + 22f, y, sidebar.width - 44f, 42f), T("⌂  主页", "⌂  Home"), !_accountOpen, () => _accountOpen = false);
            y += 50f;
            DrawNavButton(new Rect(sidebar.x + 22f, y, sidebar.width - 44f, 42f), T("▣  我的地图", "▣  My Maps"), _accountOpen, () =>
            {
                if (_api.IsSignedIn) OpenAccount();
                else OpenLogin();
            });
            y += 50f;
            DrawNavButton(new Rect(sidebar.x + 22f, y, sidebar.width - 44f, 42f), T("◎  社区地图", "◎  Database"), !_accountOpen, () => _accountOpen = false);
            y += 50f;
            DrawNavButton(new Rect(sidebar.x + 22f, y, sidebar.width - 44f, 42f), T("⚙  设置", "⚙  Settings"), false, OpenAccountWebsite);

            if (GUI.Button(new Rect(sidebar.x + 22f, sidebar.yMax - 84f, sidebar.width - 44f, 40f), T("上传地图", "Upload Map"), _primaryButtonStyle))
            {
                OpenUpload();
            }

            if (_api.IsSignedIn)
            {
                if (GUI.Button(new Rect(sidebar.x + 22f, sidebar.yMax - 38f, sidebar.width - 44f, 30f), T("退出登录", "Logout"), _buttonStyle))
                {
                    _accountOpen = false;
                    _api.SignOut((serverRevoked, error) =>
                    {
                        _status = string.IsNullOrEmpty(error)
                            ? T("已退出登录", "Signed out")
                            : T("已退出本地登录，但服务端撤销失败", "Signed out locally, but server revocation failed");
                        ShowToast(_status);
                        FetchMaps();
                    });
                }
            }
        }

        private void DrawNavButton(Rect rect, string label, bool selected, Action clicked)
        {
            if (GUI.Button(rect, label, selected ? _sidebarSelectedStyle : _sidebarButtonStyle))
            {
                clicked();
            }
        }

        private void DrawFooter()
        {
            Rect footer = new Rect(_windowRect.x, _windowRect.yMax - 28f, _windowRect.width, 28f);
            GUI.Box(footer, GUIContent.none, _panelStrongStyle);
            GUI.Label(new Rect(footer.x + 16f, footer.y + 7f, 360f, 16f), T("● 在线   © 2024 PEAK 地图库", "● ONLINE   © 2024 PEAK MAP DATABASE"), _tinyStyle);
            GUI.Label(new Rect(footer.xMax - 360f, footer.y + 7f, 340f, 16f), "API: peakmap.top", _mutedStyle);
        }

        private void DrawContent()
        {
            Rect content = new Rect(_windowRect.x + 248f, _windowRect.y + 84f, _windowRect.width - 270f, _windowRect.height - 128f);
            DrawMapList(content);
        }

        private void DrawMapList(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, _panelStyle);

            string total = _pagination != null ? _pagination.total.ToString() : (_maps != null ? _maps.Count.ToString() : "0");
            GUI.Label(new Rect(rect.x + 18f, rect.y + 16f, 260f, 16f), T("共 " + total + " 张地图", "TOTAL " + total + " MAPS FOUND"), _tinyStyle);
            GUI.Label(new Rect(rect.x + 18f, rect.y + 32f, 260f, 34f), T("社区地图", "Community Maps"), _titleStyle);
            GUI.Label(new Rect(rect.xMax - 142f, rect.y + 18f, 124f, 26f), "API: peakmap.top", _apiBadgeStyle);

            float toolbarY = rect.y + 72f;
            float gap = 8f;
            float searchW = Mathf.Max(180f, rect.width - 18f * 2f - 96f - 92f - 104f - gap * 3f);
            GUI.SetNextControlName("PeakMapSearch");
            string nextQuery = GUI.TextField(new Rect(rect.x + 18f, toolbarY, searchW, 34f), _query, _inputStyle);
            if (nextQuery != _query)
            {
                _query = nextQuery;
            }
            float x = rect.x + 18f + searchW + gap;
            if (GUI.Button(new Rect(x, toolbarY, 96f, 34f), ShortVersion(_versionFilter), _buttonStyle))
            {
                CycleVersionFilter();
            }
            x += 96f + gap;
            if (GUI.Button(new Rect(x, toolbarY, 92f, 34f), _sort == "downloads" ? T("下载量", "Popular") : T("最新", "Newest"), _buttonStyle))
            {
                _sort = _sort == "downloads" ? "newest" : "downloads";
                _page = 1;
                FetchMaps();
            }
            x += 92f + gap;
            if (GUI.Button(new Rect(x, toolbarY, 104f, 34f), T("搜索", "Search"), _primaryButtonStyle))
            {
                _page = 1;
                FetchMaps();
            }

            Rect cardsRect = new Rect(rect.x + 18f, rect.y + 118f, rect.width - 36f, rect.height - 174f);
            if (_loadingMaps)
            {
                GUI.Label(cardsRect, T("正在从 peakmap.top 获取地图列表...", "Fetching maps from peakmap.top..."), _labelStyle);
            }
            else if (_maps == null || _maps.Count == 0)
            {
                GUI.Label(cardsRect, string.IsNullOrEmpty(_query) ? T("暂无地图。", "No maps yet.") : T("没有找到匹配的地图。", "No matching maps found."), _labelStyle);
            }
            else
            {
                DrawCards(cardsRect);
            }

            DrawPagination(new Rect(rect.x + 18f, rect.yMax - 46f, rect.width - 36f, 34f));
        }

        private void DrawCards(Rect rect)
        {
            int columns = rect.width > 560f ? 2 : 1;
            float gap = 14f;
            float cardW = (rect.width - (columns - 1) * gap - 10f) / columns;
            float cardH = 290f;
            int rows = Mathf.CeilToInt(_maps.Count / (float)columns);
            Rect view = new Rect(0f, 0f, rect.width - 18f, rows * (cardH + gap));
            _mapScroll = GUI.BeginScrollView(rect, _mapScroll, view, false, true);

            for (int i = 0; i < _maps.Count; i++)
            {
                int col = i % columns;
                int row = i / columns;
                Rect card = new Rect(col * (cardW + gap), row * (cardH + gap), cardW, cardH);
                DrawCard(card, i, _maps[i]);
            }

            GUI.EndScrollView();
        }

        private void DrawCard(Rect rect, int index, MapEntry map)
        {
            bool selected = index == _selectedMapIndex;
            GUI.Box(rect, GUIContent.none, _cardStyle);
            if (selected)
            {
                DrawCardSelectionOutline(rect);
            }

            Rect inner = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
            Rect thumb = new Rect(inner.x, inner.y, inner.width, 130f);
            DrawThumbnail(thumb, map);
            GUI.Label(new Rect(thumb.x + 10f, thumb.y + 10f, 86f, 22f), ShortVersion(map.mod_version), _badgeStyle);
            if (map.revision > 1)
            {
                GUI.Label(new Rect(thumb.xMax - 78f, thumb.y + 10f, 66f, 22f), "v" + map.revision, _badgeStyle);
            }

            float textY = inner.y + 146f;
            GUI.Label(new Rect(inner.x + 14f, textY, inner.width - 28f, 30f), CleanUiText(Safe(map.name, T("未命名地图", "Untitled map"))), _h2Style);
            GUI.Label(new Rect(inner.x + 14f, textY + 34f, inner.width - 28f, 18f), "♙ " + CleanUiText(Safe(map.author, T("未知", "Unknown"))), _mutedStyle);
            float statsY = inner.yMax - 42f;
            float descriptionHeight = Mathf.Max(28f, statsY - (textY + 58f) - 6f);
            string cardDescription = CompactCardDescription(CleanUiText(Safe(map.description, T("没有描述", "No description"))), inner.width - 28f);
            GUI.Label(new Rect(inner.x + 14f, textY + 58f, inner.width - 28f, descriptionHeight), cardDescription, _cardDescStyle);

            string stats = T("下载 " + map.downloads + "    点赞 " + map.likes, "Downloads " + map.downloads + "    Likes " + map.likes);
            GUI.Label(new Rect(inner.x + 14f, statsY, inner.width - 104f, 26f), stats, _cardStatsStyle);
            Rect getRect = new Rect(inner.xMax - 82f, inner.yMax - 46f, 68f, 34f);
            if (GUI.Button(getRect, DownloadButtonLabel(map), _primaryButtonStyle))
            {
                SelectMap(index);
                DownloadSelected();
            }

            if (GUI.enabled && Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition) && !getRect.Contains(Event.current.mousePosition))
            {
                SelectMap(index);
                _communityDetailOpen = true;
                BlockTopLayerInputThisFrame();
                Event.current.Use();
            }
        }

        private void DrawCardSelectionOutline(Rect rect)
        {
            Color old = GUI.color;
            GUI.color = new Color(0.20f, 0.86f, 0.46f, 1f);
            const float thickness = 2f;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void DrawThumbnail(Rect rect, MapEntry map)
        {
            DrawThumbnail(rect, map, ScaleMode.ScaleAndCrop);
        }

        private void DrawThumbnail(Rect rect, MapEntry map, ScaleMode scaleMode)
        {
            Texture2D tex = GetThumbnail(map);
            GUI.DrawTexture(rect, tex != null ? tex : _placeholderThumb, scaleMode);
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.20f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0f, 0f, 0f, 0.50f);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 34f, rect.width, 34f), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private Texture2D GetThumbnail(MapEntry map)
        {
            string url = !string.IsNullOrEmpty(map.thumbnail_url) ? map.thumbnail_url : map.image_url;
            if (string.IsNullOrEmpty(url))
            {
                return _placeholderThumb;
            }

            Texture2D tex;
            if (_thumbnails.TryGetValue(url, out tex))
            {
                return tex != null ? tex : _placeholderThumb;
            }

            _thumbnails[url] = null;
            _api.DownloadTexture(url, loaded =>
            {
                if (loaded != null)
                {
                    _thumbnails[url] = loaded;
                }
            });
            return _placeholderThumb;
        }

        private void DrawDetail(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, _panelStyle);
            MapEntry map = SelectedMap;
            if (map == null)
            {
                GUI.Label(new Rect(rect.x + 16f, rect.y + 16f, rect.width - 32f, 40f), T("选择一张地图查看详情", "Select a map to view details"), _labelStyle);
                return;
            }

            Rect thumb = new Rect(rect.x + 14f, rect.y + 14f, rect.width - 28f, 150f);
            DrawThumbnail(thumb, map);
            GUI.Label(new Rect(thumb.x + 12f, thumb.yMax - 58f, thumb.width - 24f, 16f), T("当前地图", "SELECTED MAP"), _tinyStyle);
            GUI.Label(new Rect(thumb.x + 12f, thumb.yMax - 40f, thumb.width - 24f, 34f), CleanUiText(Safe(map.name, T("未命名地图", "Untitled map"))), _detailTitleStyle);
            float y = thumb.yMax + 16f;

            DrawDetailMeta(new Rect(rect.x + 14f, y, rect.width - 28f, 88f), map);
            y += 102f;

            GUI.Label(new Rect(rect.x + 16f, y, rect.width - 32f, 18f), T("描述", "DESCRIPTION"), _tinyStyle);
            y += 24f;

            float buttonY = rect.yMax - 48f;
            Rect descRect = new Rect(rect.x + 14f, y, rect.width - 28f, Mathf.Max(110f, buttonY - y - 12f));
            DrawScrollableDescription(descRect, FormatDetailDescription(CleanUiText(Safe(map.description, T("没有描述", "No description")))));

            float buttonGap = 8f;
            float buttonW = (rect.width - 32f - buttonGap * 2f) / 3f;
            GUI.enabled = !_downloading;
            if (GUI.Button(new Rect(rect.x + 16f, buttonY, buttonW, 36f), _downloading ? T("下载中", "Loading") : DownloadButtonLabel(map), _primaryButtonStyle))
            {
                DownloadSelected();
            }
            GUI.enabled = true;
            GUI.enabled = !_liking;
            if (GUI.Button(new Rect(rect.x + 16f + buttonW + buttonGap, buttonY, buttonW, 36f), map.liked_by_me ? T("已赞", "Liked") : T("点赞", "Like"), _buttonStyle))
            {
                ToggleLikeSelected();
            }
            GUI.enabled = true;
            if (GUI.Button(new Rect(rect.x + 16f + (buttonW + buttonGap) * 2f, buttonY, buttonW, 36f), T("刷新", "Refresh"), _buttonStyle))
            {
                RefreshAll();
            }
        }

        private void DrawCommunityDetailModal()
        {
            MapEntry map = SelectedMap;
            if (map == null)
            {
                _communityDetailOpen = false;
                return;
            }

            GUI.depth = -101;
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, _modalBackdropStyle);

            float modalWidth = Mathf.Min(820f, Screen.width - 32f);
            float modalHeight = Mathf.Min(680f, Screen.height - 26f);
            Rect modal = new Rect(Mathf.Round((Screen.width - modalWidth) * 0.5f),
                Mathf.Round((Screen.height - modalHeight) * 0.5f), modalWidth, modalHeight);
            GUI.Box(modal, GUIContent.none, _rootStyle);

            Rect thumb = new Rect(modal.x + 14f, modal.y + 14f, modal.width - 28f, 238f);
            DrawThumbnail(thumb, map, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(thumb.x + 14f, thumb.y + 12f, 118f, 24f), ShortVersion(map.mod_version), _badgeStyle);
            if (GUI.Button(new Rect(modal.xMax - 52f, modal.y + 18f, 34f, 34f), "×", _iconButtonStyle))
            {
                _communityDetailOpen = false;
            }

            float y = thumb.yMax + 16f;
            GUI.Label(new Rect(modal.x + 24f, y, modal.width - 48f, 38f),
                CleanUiText(Safe(map.name, T("未命名地图", "Untitled map"))), _titleStyle);
            y += 48f;

            float gap = 10f;
            float statW = (modal.width - 48f - gap * 3f) / 4f;
            DrawStatBox(new Rect(modal.x + 24f, y, statW, 64f), T("作者", "AUTHOR"), CleanUiText(Safe(map.author, T("未知", "Unknown"))));
            DrawStatBox(new Rect(modal.x + 24f + statW + gap, y, statW, 64f), T("MOD 版本", "MOD VERSION"), CleanUiText(Safe(map.mod_version, "-")));
            DrawStatBox(new Rect(modal.x + 24f + (statW + gap) * 2f, y, statW, 64f), T("下载次数", "DOWNLOADS"), map.downloads.ToString());
            DrawStatBox(new Rect(modal.x + 24f + (statW + gap) * 3f, y, statW, 64f), T("点赞", "LIKES"), map.likes.ToString());
            y += 78f;

            GUI.Label(new Rect(modal.x + 24f, y, modal.width - 48f, 20f),
                T("上传时间：", "Uploaded: ") + DateOnly(Safe(map.updated_at, map.created_at)), _mutedStyle);
            y += 28f;
            GUI.Label(new Rect(modal.x + 24f, y, modal.width - 48f, 20f), T("介绍", "DESCRIPTION"), _tinyStyle);
            y += 26f;

            float buttonY = modal.yMax - 58f;
            Rect descriptionRect = new Rect(modal.x + 24f, y, modal.width - 48f, Mathf.Max(80f, buttonY - y - 12f));
            DrawScrollableDescription(descriptionRect, FormatDetailDescription(CleanUiText(Safe(map.description, T("没有描述", "No description")))));

            bool previous = GUI.enabled;
            GUI.enabled = previous && !_downloading;
            if (GUI.Button(new Rect(modal.x + 24f, buttonY, 108f, 38f), _downloading ? T("下载中", "Loading") : DownloadButtonLabel(map), _primaryButtonStyle))
            {
                DownloadSelected();
            }
            GUI.enabled = previous && !_liking;
            if (GUI.Button(new Rect(modal.x + 144f, buttonY, 108f, 38f), map.liked_by_me ? T("已赞", "Liked") : T("点赞", "Like"), _buttonStyle))
            {
                ToggleLikeSelected();
            }
            GUI.enabled = previous;
            if (GUI.Button(new Rect(modal.xMax - 132f, buttonY, 108f, 38f), T("关闭", "Close"), _buttonStyle))
            {
                _communityDetailOpen = false;
            }
            GUI.enabled = previous;
        }

        private void DrawDetailMeta(Rect rect, MapEntry map)
        {
            float gap = 10f;
            float topW = (rect.width - gap) * 0.5f;
            DrawStatBox(new Rect(rect.x, rect.y, topW, 52f), T("作者", "AUTHOR"), CleanUiText(Safe(map.author, "Unknown")));
            DrawStatBox(new Rect(rect.x + topW + gap, rect.y, topW, 52f), T("版本", "VERSION"), CleanUiText(Safe(map.mod_version, "-")) + (map.revision > 1 ? " · v" + map.revision : ""));

            string stats = T("下载 ", "Downloads ") + map.downloads
                + "     " + T("点赞 ", "Likes ") + map.likes
                + "     " + DateOnly(Safe(map.updated_at, map.created_at));
            GUI.Label(new Rect(rect.x + 2f, rect.y + 62f, rect.width - 4f, 22f), stats, _mutedStyle);
        }

        private void DrawStatBox(Rect rect, string label, string value)
        {
            GUI.Box(rect, GUIContent.none, _statBoxStyle);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, 20f), label, _statLabelStyle);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 25f, rect.width - 20f, Mathf.Max(18f, rect.height - 29f)), value, _statValueStyle);
        }

        private void DrawScrollableDescription(Rect rect, string text)
        {
            GUI.Box(rect, GUIContent.none, _detailDescriptionStyle);
            string value = string.IsNullOrEmpty(text) ? T("没有描述", "No description") : text;
            Rect scrollRect = new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, rect.height - 24f);
            float textWidth = scrollRect.width - 18f;
            float textHeight = Mathf.Max(scrollRect.height, _detailTextStyle.CalcHeight(new GUIContent(value), textWidth) + 12f);
            Rect view = new Rect(0f, 0f, textWidth, textHeight + 10f);
            _detailScroll = GUI.BeginScrollView(scrollRect, _detailScroll, view, false, true);
            GUI.Label(new Rect(0f, 0f, textWidth, textHeight), value, _detailTextStyle);
            GUI.EndScrollView();
        }

        private void DrawPagination(Rect rect)
        {
            bool previousEnabled = GUI.enabled;
            GUI.enabled = _pagination != null && _pagination.has_prev && !_loadingMaps;
            if (GUI.Button(new Rect(rect.x, rect.y, 34f, 32f), "‹", _buttonStyle))
            {
                _page = Mathf.Max(1, _page - 1);
                FetchMaps();
            }
            GUI.enabled = previousEnabled;

            GUI.Label(new Rect(rect.x + 42f, rect.y, 54f, 32f), _page.ToString(), _pagePillStyle);

            GUI.enabled = _pagination != null && _pagination.has_next && !_loadingMaps;
            if (GUI.Button(new Rect(rect.x + 104f, rect.y, 34f, 32f), "›", _buttonStyle))
            {
                _page++;
                FetchMaps();
            }
            GUI.enabled = previousEnabled;

            string pageInfo = _pagination != null
                ? T("第 ", "Page ") + _pagination.page + " / " + Mathf.Max(1, _pagination.total_pages) + T(" 页", "")
                : T("第 ", "Page ") + _page + T(" 页", "");
            GUI.Label(new Rect(rect.xMax - 110f, rect.y + 7f, 110f, 22f), pageInfo, _mutedStyle);
        }

        private void DrawLoginModal()
        {
            GUI.depth = -101;
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, _modalBackdropStyle);

            Rect modal = new Rect(Mathf.Round((Screen.width - 460f) * 0.5f), Mathf.Round((Screen.height - 330f) * 0.5f), 460f, 330f);
            GUI.Box(modal, GUIContent.none, _rootStyle);
            GUI.Label(new Rect(modal.x + 18f, modal.y + 16f, 220f, 16f), T("PEAKMAP 账号", "PEAKMAP ACCOUNT"), _tinyStyle);
            GUI.Label(new Rect(modal.x + 18f, modal.y + 32f, 220f, 34f), T("登录账号", "Sign in"), _titleStyle);
            if (GUI.Button(new Rect(modal.xMax - 54f, modal.y + 22f, 36f, 36f), "×", _iconButtonStyle))
            {
                _loginOpen = false;
            }

            float x = modal.x + 24f;
            float y = modal.y + 90f;
            DrawFieldLabel(x, y, T("邮箱", "Email"));
            _loginEmail = GUI.TextField(new Rect(x, y + 20f, modal.width - 48f, 38f), _loginEmail, _inputStyle);
            y += 72f;
            DrawFieldLabel(x, y, T("密码", "Password"));
            _loginPassword = GUI.PasswordField(new Rect(x, y + 20f, modal.width - 48f, 38f), _loginPassword, '*', _inputStyle);
            y += 70f;
            GUI.Label(new Rect(x, y, modal.width - 48f, 24f), _status, _mutedStyle);

            bool previous = GUI.enabled;
            GUI.enabled = previous && !_refreshingSession;
            if (GUI.Button(new Rect(modal.xMax - 196f, modal.yMax - 58f, 82f, 38f), T("网页注册", "Account"), _buttonStyle))
            {
                OpenAccountWebsite();
            }
            if (GUI.Button(new Rect(modal.xMax - 104f, modal.yMax - 58f, 86f, 38f), T("登录", "Sign in"), _primaryButtonStyle))
            {
                SignInSelected();
            }
            GUI.enabled = previous;
        }

        private void DrawAccountPage()
        {
            Rect page = new Rect(_windowRect.x + 248f, _windowRect.y + 84f, _windowRect.width - 270f, _windowRect.height - 128f);
            GUI.Label(new Rect(page.x, page.y, 260f, 16f), T("我的地图 (" + _accountMaps.Count + ")", "ACTIVE REPOSITORIES (" + _accountMaps.Count + ")"), _tinyStyle);
            GUI.Label(new Rect(page.x, page.y + 18f, 260f, 34f), T("我的地图", "My Maps"), _titleStyle);
            GUI.Label(new Rect(page.x + 270f, page.y + 24f, 300f, 22f), T("登录账号：", "Logged in as ") + ShortAccountName(_api.Session.DisplayName), _mutedStyle);

            if (GUI.Button(new Rect(page.xMax - 246f, page.y + 8f, 72f, 34f), T("刷新", "Refresh"), _buttonStyle))
            {
                FetchAccountMaps();
            }
            if (GUI.Button(new Rect(page.xMax - 166f, page.y + 8f, 72f, 34f), T("退出", "Sign out"), _buttonStyle))
            {
                _accountOpen = false;
                _api.SignOut((serverRevoked, error) =>
                {
                    _status = string.IsNullOrEmpty(error)
                        ? T("已退出登录", "Signed out")
                        : T("已退出本地登录，但服务端撤销失败", "Signed out locally, but server revocation failed");
                    ShowToast(_status);
                    FetchMaps();
                });
            }
            if (GUI.Button(new Rect(page.xMax - 86f, page.y + 8f, 34f, 34f), "↗", _iconButtonStyle))
            {
                OpenAccountWebsite();
            }
            if (GUI.Button(new Rect(page.xMax - 44f, page.y + 8f, 34f, 34f), "×", _iconButtonStyle))
            {
                _accountOpen = false;
            }

            if (Time.unscaledTime >= _nextLocalSaveScanTime)
            {
                RefreshLocalSaves();
            }

            Rect listRect = new Rect(page.x, page.y + 66f, Mathf.Min(320f, page.width * 0.36f), page.height - 118f);
            Rect editRect = new Rect(listRect.xMax + 18f, listRect.y, page.xMax - listRect.xMax - 18f, listRect.height);
            GUI.Box(listRect, GUIContent.none, _panelStrongStyle);
            DrawAccountMapList(listRect);
            GUI.Box(editRect, GUIContent.none, _panelStrongStyle);
            DrawAccountEditor(editRect);

            GUI.Label(new Rect(page.x, page.yMax - 36f, page.width - 260f, 24f), _status, _mutedStyle);
            MapEntry selected = SelectedAccountMap;
            bool previous = GUI.enabled;
            GUI.enabled = previous && selected != null && !_savingAccountMap && !_deletingAccountMap
                && !_accountVersionDropdownOpen && !_accountJsonDropdownOpen;
            if (GUI.Button(new Rect(page.xMax - 214f, page.yMax - 44f, 72f, 38f), T("删除", "Delete"), _buttonStyle))
            {
                _deleteConfirmMapId = selected.id;
            }
            if (GUI.Button(new Rect(page.xMax - 132f, page.yMax - 44f, 132f, 38f), _savingAccountMap ? T("保存中", "Saving") : T("保存修改", "Save"), _primaryButtonStyle))
            {
                SaveAccountMap();
            }
            GUI.enabled = previous;

            if (selected != null && string.Equals(_deleteConfirmMapId, selected.id, StringComparison.Ordinal))
            {
                DrawDeleteConfirm(page, selected);
            }
        }

        private void DrawAccountMapList(Rect rect)
        {
            Rect inner = new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f);
            if (_loadingAccountMaps)
            {
                GUI.Label(inner, T("正在获取我的地图...", "Loading my maps..."), _mutedStyle);
                return;
            }
            if (_accountMaps.Count == 0)
            {
                GUI.Label(inner, T("当前账号还没有地图。登录后上传的地图会出现在这里。", "This account has no maps yet. Maps uploaded while signed in appear here."), _mutedStyle);
                return;
            }

            const float rowH = 54f;
            Rect view = new Rect(0f, 0f, inner.width - 18f, _accountMaps.Count * rowH);
            _accountScroll = GUI.BeginScrollView(inner, _accountScroll, view, false, true);
            for (int i = 0; i < _accountMaps.Count; i++)
            {
                MapEntry map = _accountMaps[i];
                GUIStyle style = i == _selectedAccountMapIndex ? _primaryButtonStyle : _buttonStyle;
                string label = CleanUiText(Safe(map.name, T("未命名地图", "Untitled map"))) + "\n" + ShortVersion(map.mod_version) + " · ♥ " + map.likes + " · ↓ " + map.downloads;
                if (GUI.Button(new Rect(0f, i * rowH, view.width, rowH - 4f), label, style))
                {
                    SelectAccountMap(i);
                }
            }
            GUI.EndScrollView();
        }

        private void DrawAccountEditor(Rect rect)
        {
            MapEntry map = SelectedAccountMap;
            if (map == null)
            {
                GUI.Label(new Rect(rect.x + 14f, rect.y + 14f, rect.width - 28f, 60f), T("选择一张自己的地图进行编辑。", "Select one of your maps to edit."), _mutedStyle);
                return;
            }

            float x = rect.x + 14f;
            float y = rect.y + 12f;
            float w = rect.width - 28f;
            DrawFieldLabel(x, y, T("地图名称", "Map name"));
            _editName = GUI.TextField(new Rect(x, y + 20f, w, 34f), _editName, _inputStyle);
            y += 60f;
            DrawFieldLabel(x, y, T("作者", "Author"));
            _editAuthor = GUI.TextField(new Rect(x, y + 20f, (w - 12f) * 0.5f, 34f), _editAuthor, _inputStyle);
            float versionX = x + (w + 12f) * 0.5f;
            float versionW = (w - 12f) * 0.5f;
            Rect versionButtonRect = new Rect(versionX, y + 20f, versionW, 34f);
            Rect versionDropdownRect = _accountVersionDropdownOpen
                ? new Rect(versionButtonRect.x, versionButtonRect.yMax + 4f, versionButtonRect.width,
                    Mathf.Min(150f, Mathf.Max(38f, _versions.Count * 30f + 10f)))
                : Rect.zero;
            if (_accountVersionDropdownOpen)
            {
                ConsumeAccountVersionDropdownOutsideClick(versionButtonRect, versionDropdownRect);
            }
            DrawFieldLabel(versionX, y, T("MOD 版本", "MOD version"));
            string editVersionLabel = string.IsNullOrEmpty(_editVersion)
                ? T("选择版本  ▾", "Select version  ▾")
                : _editVersion + "  ▾";
            if (GUI.Button(versionButtonRect, editVersionLabel, _buttonStyle))
            {
                _accountVersionDropdownOpen = !_accountVersionDropdownOpen;
                _accountJsonDropdownOpen = false;
                if (_accountVersionDropdownOpen)
                {
                    if (_versions.Count == 0)
                    {
                        FetchModVersions();
                    }
                    BlockTopLayerInputThisFrame();
                }
            }
            bool guiEnabledBeforeVersionDropdown = GUI.enabled;
            GUI.enabled = guiEnabledBeforeVersionDropdown && !_accountVersionDropdownOpen;
            y += 60f;
            DrawFieldLabel(x, y, T("描述", "Description"));
            Rect descriptionRect = new Rect(x, y + 20f, w, 112f);
            GUI.Box(descriptionRect, GUIContent.none, _detailMetaStyle);
            Rect descScrollRect = new Rect(descriptionRect.x + 8f, descriptionRect.y + 8f, descriptionRect.width - 16f, descriptionRect.height - 16f);
            float descTextWidth = descScrollRect.width - 18f;
            float descTextHeight = Mathf.Max(descScrollRect.height, _textAreaStyle.CalcHeight(new GUIContent(_editDescription + "\n "), descTextWidth) + 18f);
            Rect descView = new Rect(0f, 0f, descTextWidth, descTextHeight);
            _accountDescriptionScroll = GUI.BeginScrollView(descScrollRect, _accountDescriptionScroll, descView, false, true);
            _editDescription = GUI.TextArea(new Rect(0f, 0f, descTextWidth, descTextHeight), _editDescription, _textAreaStyle);
            GUI.EndScrollView();
            y += 140f;

            GUI.Label(new Rect(x, y, w, 18f), T("替换 JSON", "REPLACE JSON"), _tinyStyle);
            y += 22f;
            Rect jsonRow = new Rect(x, y, w, 42f);
            Rect jsonDropdownRect = _accountJsonDropdownOpen
                ? new Rect(jsonRow.x, jsonRow.yMax + 4f, jsonRow.width, Mathf.Min(150f, Mathf.Max(42f, _filteredLocalSaveIndexes.Count * 30f + 12f)))
                : Rect.zero;
            if (_accountJsonDropdownOpen)
            {
                ConsumeAccountJsonDropdownOutsideClick(jsonRow, jsonDropdownRect);
            }
            GUI.Box(jsonRow, GUIContent.none, _detailMetaStyle);
            string jsonLabel = _editReplaceJson && SelectedLocalSavePath != null ? MapSaveService.DisplayName(SelectedLocalSavePath) : T("不替换 JSON", "Keep current JSON");
            if (GUI.Button(new Rect(jsonRow.x + 8f, jsonRow.y + 6f, jsonRow.width - 78f, 30f), jsonLabel + "  ▾", _buttonStyle))
            {
                _accountJsonDropdownOpen = !_accountJsonDropdownOpen;
                if (_accountJsonDropdownOpen)
                {
                    RefreshLocalSaves(false);
                    BlockTopLayerInputThisFrame();
                }
            }
            if (GUI.Button(new Rect(jsonRow.xMax - 62f, jsonRow.y + 6f, 54f, 30f), T("清除", "Clear"), _buttonStyle))
            {
                _editReplaceJson = false;
                _accountJsonDropdownOpen = false;
            }
            if (_accountJsonDropdownOpen)
            {
                DrawAccountJsonDropdown(jsonDropdownRect);
            }
            y = (_accountJsonDropdownOpen ? jsonDropdownRect.yMax : jsonRow.yMax) + 12f;

            _editReplaceImage = GUI.Toggle(new Rect(x, y, w, 22f), _editReplaceImage, T("替换封面：", "Replace cover: ") + (SelectedLocalImagePath == null ? T("未选择", "none") : MapSaveService.DisplayName(SelectedLocalImagePath)));
            y += 24f;
            _editRemoveImage = GUI.Toggle(new Rect(x, y, w, 22f), _editRemoveImage, T("移除当前封面", "Remove current cover"));
            y += 28f;
            if (GUI.Button(new Rect(x, y, 86f, 32f), T("选封面", "Pick"), _buttonStyle))
            {
                OpenImagePicker();
            }
            if (GUI.Button(new Rect(x + 94f, y, 86f, 32f), T("自动匹配", "Auto"), _buttonStyle))
            {
                AutoSelectMatchingImage(true);
            }
            if (GUI.Button(new Rect(x + 188f, y, 86f, 32f), T("清除", "Clear"), _buttonStyle))
            {
                _selectedLocalImage = -1;
            }

            GUI.enabled = guiEnabledBeforeVersionDropdown;
            if (_accountVersionDropdownOpen)
            {
                DrawAccountVersionDropdown(versionDropdownRect);
            }
        }

        private void DrawDeleteConfirm(Rect modal, MapEntry map)
        {
            Rect confirm = new Rect(modal.x + 180f, modal.y + 210f, modal.width - 360f, 170f);
            GUI.Box(confirm, GUIContent.none, _rootStyle);
            GUI.Label(new Rect(confirm.x + 18f, confirm.y + 18f, confirm.width - 36f, 48f), T("确定删除这张地图？此操作不可恢复。", "Delete this map? This cannot be undone."), _dangerStyle);
            GUI.Label(new Rect(confirm.x + 18f, confirm.y + 70f, confirm.width - 36f, 28f), CleanUiText(Safe(map.name, "-")), _labelStyle);
            if (GUI.Button(new Rect(confirm.xMax - 188f, confirm.yMax - 52f, 78f, 34f), T("取消", "Cancel"), _buttonStyle))
            {
                _deleteConfirmMapId = string.Empty;
            }
            if (GUI.Button(new Rect(confirm.xMax - 100f, confirm.yMax - 52f, 82f, 34f), T("删除", "Delete"), _primaryButtonStyle))
            {
                DeleteAccountMap(map);
            }
        }

        private void DrawDownloadConfirmModal()
        {
            MapEntry map = FindMapById(_downloadConfirmMapId);
            if (map == null)
            {
                _downloadConfirmOpen = false;
                _downloadConfirmMapId = string.Empty;
                return;
            }

            MapDownloadInfo info = _api.GetDownloadInfo(map);
            if (info.Status != MapDownloadStatus.UpdateAvailable && info.Status != MapDownloadStatus.LocalModified)
            {
                _downloadConfirmOpen = false;
                _downloadConfirmMapId = string.Empty;
                return;
            }

            GUI.depth = -101;
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, _modalBackdropStyle);
            float width = Mathf.Min(560f, Screen.width - 32f);
            float height = 230f;
            Rect modal = new Rect(Mathf.Round((Screen.width - width) * 0.5f), Mathf.Round((Screen.height - height) * 0.5f), width, height);
            GUI.Box(modal, GUIContent.none, _rootStyle);
            GUI.Label(new Rect(modal.x + 22f, modal.y + 20f, modal.width - 44f, 32f),
                info.Status == MapDownloadStatus.LocalModified ? T("本地文件已修改", "Local file was modified") : T("发现地图新版本", "New map version available"), _titleStyle);
            GUI.Label(new Rect(modal.x + 22f, modal.y + 64f, modal.width - 44f, 34f),
                CleanUiText(Safe(map.name, T("未命名地图", "Untitled map"))), _labelStyle);
            string message = info.Status == MapDownloadStatus.LocalModified
                ? T("本地 JSON 与上次下载内容不同。继续更新会覆盖当前文件，并先备份旧文件。", "The local JSON differs from the downloaded copy. Updating will replace it after backing up the old file.")
                : T("服务器版本 v" + info.CurrentRevision + " 高于本地 v" + info.LocalRevision + "。继续更新会先备份旧文件。", "Server version v" + info.CurrentRevision + " is newer than local v" + info.LocalRevision + ". The old file will be backed up first.");
            GUI.Label(new Rect(modal.x + 22f, modal.y + 104f, modal.width - 44f, 52f), message, _mutedStyle);

            bool previous = GUI.enabled;
            GUI.enabled = previous && !_downloading;
            if (GUI.Button(new Rect(modal.xMax - 194f, modal.yMax - 52f, 82f, 34f), T("取消", "Cancel"), _buttonStyle))
            {
                _downloadConfirmOpen = false;
                _downloadConfirmMapId = string.Empty;
            }
            if (GUI.Button(new Rect(modal.xMax - 102f, modal.yMax - 52f, 84f, 34f), _downloading ? T("更新中", "Updating") : T("确认更新", "Update"), _primaryButtonStyle))
            {
                _downloadConfirmOpen = false;
                _downloadConfirmMapId = string.Empty;
                StartDownload(map, true);
            }
            GUI.enabled = previous;
        }

        private void ConsumeAccountJsonDropdownOutsideClick(Rect rowRect, Rect dropdownRect)
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.MouseDown)
            {
                return;
            }

            if (rowRect.Contains(e.mousePosition) || dropdownRect.Contains(e.mousePosition))
            {
                return;
            }

            _accountJsonDropdownOpen = false;
            GUI.FocusControl(null);
            e.Use();
        }

        private void DrawAccountJsonDropdown(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, _panelStrongStyle);
            Rect filterRect = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 30f);
            string nextFilter = GUI.TextField(filterRect, _localSaveFilter, _inputStyle);
            if (!string.Equals(nextFilter, _localSaveFilter, StringComparison.Ordinal))
            {
                _localSaveFilter = nextFilter;
                ApplyLocalSaveFilter(true);
            }

            Rect listRect = new Rect(rect.x + 8f, filterRect.yMax + 6f, rect.width - 16f, rect.height - 50f);
            if (_filteredLocalSaveIndexes.Count == 0)
            {
                GUI.Label(listRect, T("没有可用的 JSON。", "No JSON files available."), _mutedStyle);
                return;
            }

            const float rowH = 30f;
            Rect view = new Rect(0f, 0f, listRect.width - 18f, _filteredLocalSaveIndexes.Count * rowH);
            _uploadSaveScroll = GUI.BeginScrollView(listRect, _uploadSaveScroll, view, false, true);
            int first = Mathf.Max(0, Mathf.FloorToInt(_uploadSaveScroll.y / rowH) - 1);
            int last = Mathf.Min(_filteredLocalSaveIndexes.Count, first + Mathf.CeilToInt(listRect.height / rowH) + 2);
            for (int visibleIndex = first; visibleIndex < last; visibleIndex++)
            {
                int saveIndex = _filteredLocalSaveIndexes[visibleIndex];
                GUIStyle style = saveIndex == _selectedLocalSave ? _primaryButtonStyle : _buttonStyle;
                if (GUI.Button(new Rect(0f, visibleIndex * rowH, view.width, rowH - 3f),
                    MapSaveService.DisplayName(_localSaves[saveIndex]), style))
                {
                    _selectedLocalSave = saveIndex;
                    _editReplaceJson = true;
                    _accountJsonDropdownOpen = false;
                    _log.LogInfo("JSON selected from account editor: " + _localSaves[saveIndex]);
                }
            }
            GUI.EndScrollView();
        }

        private void ConsumeAccountVersionDropdownOutsideClick(Rect buttonRect, Rect dropdownRect)
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.MouseDown)
            {
                return;
            }

            if (buttonRect.Contains(e.mousePosition) || dropdownRect.Contains(e.mousePosition))
            {
                return;
            }

            _accountVersionDropdownOpen = false;
            GUI.FocusControl(null);
            e.Use();
        }

        private void DrawAccountVersionDropdown(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, _panelStrongStyle);
            if (_versions.Count == 0)
            {
                GUI.Label(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, 22f),
                    T("正在获取版本...", "Loading versions..."), _mutedStyle);
                return;
            }

            const float rowH = 30f;
            Rect scrollRect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
            Rect view = new Rect(0f, 0f, scrollRect.width - 18f, _versions.Count * rowH);
            _accountVersionScroll = GUI.BeginScrollView(scrollRect, _accountVersionScroll, view, false, true);
            int selectedIndex = _versions.FindIndex(v => string.Equals(v.version_name, _editVersion, StringComparison.Ordinal));
            int first = Mathf.Max(0, Mathf.FloorToInt(_accountVersionScroll.y / rowH) - 1);
            int last = Mathf.Min(_versions.Count, first + Mathf.CeilToInt(scrollRect.height / rowH) + 2);
            for (int i = first; i < last; i++)
            {
                GUIStyle style = i == selectedIndex ? _primaryButtonStyle : _buttonStyle;
                if (GUI.Button(new Rect(0f, i * rowH, view.width, rowH - 3f), _versions[i].version_name, style))
                {
                    _editVersion = _versions[i].version_name;
                    _accountVersionDropdownOpen = false;
                    _log.LogInfo("Account map version selected: " + _editVersion);
                }
            }
            GUI.EndScrollView();
        }

        private void DrawUploadModal()
        {
            GUI.depth = -101;
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, _modalBackdropStyle);

            float modalHeight = Mathf.Min(620f, Screen.height - 48f);
            Rect modal = new Rect(Mathf.Round((Screen.width - 680f) * 0.5f), Mathf.Round((Screen.height - modalHeight) * 0.5f), 680f, Mathf.Round(modalHeight));
            GUI.Box(modal, GUIContent.none, _rootStyle);

            GUI.Label(new Rect(modal.x + 18f, modal.y + 16f, 220f, 16f), T("提交本地存档", "SUBMIT LOCAL SAVE"), _tinyStyle);
            GUI.Label(new Rect(modal.x + 18f, modal.y + 32f, 220f, 34f), T("上传地图", "Upload Map"), _titleStyle);
            bool closeEnabledBeforeUpload = GUI.enabled;
            GUI.enabled = closeEnabledBeforeUpload && !IsTopLayerInputBlocked();
            if (GUI.Button(new Rect(modal.xMax - 54f, modal.y + 22f, 36f, 36f), "×", _iconButtonStyle))
            {
                _uploadOpen = false;
            }
            GUI.enabled = closeEnabledBeforeUpload;

            if (Time.unscaledTime >= _nextLocalSaveScanTime)
            {
                RefreshLocalSaves();
            }

            float y = modal.y + 82f;
            float left = modal.x + 18f;
            float fieldW = (modal.width - 54f) / 2f;
            Rect versionDropdownRect = Rect.zero;
            Rect imageDropdownRect = Rect.zero;
            Rect saveDropdownRect = Rect.zero;
            float versionY = y + 72f;
            Rect versionButtonRect = new Rect(left, versionY + 20f, fieldW, 38f);
            Rect imageButtonRect = new Rect(left + fieldW + 18f, versionY + 20f, fieldW - 184f, 38f);
            if (_uploadVersionDropdownOpen)
            {
                versionDropdownRect = new Rect(versionButtonRect.x, versionButtonRect.yMax + 4f, versionButtonRect.width, Mathf.Min(150f, Mathf.Max(38f, _versions.Count * 30f + 10f)));
            }
            if (_uploadImageDropdownOpen)
            {
                imageDropdownRect = new Rect(imageButtonRect.x, imageButtonRect.yMax + 4f, fieldW, 150f);
            }
            Rect saveButtonRect = new Rect(left, modal.y + 354f, modal.width - 36f, 38f);
            if (_uploadSaveDropdownOpen)
            {
                saveDropdownRect = new Rect(saveButtonRect.x, saveButtonRect.yMax + 4f, saveButtonRect.width, 184f);
            }

            bool dropdownOpen = _uploadVersionDropdownOpen || _uploadImageDropdownOpen || _uploadSaveDropdownOpen;
            bool topLayerInputBlocked = IsTopLayerInputBlocked();
            if (dropdownOpen)
            {
                Rect activeDropdown = _uploadVersionDropdownOpen ? versionDropdownRect
                    : (_uploadImageDropdownOpen ? imageDropdownRect : saveDropdownRect);
                ConsumeUploadDropdownOutsideClick(activeDropdown);
            }

            bool guiEnabledBeforeUpload = GUI.enabled;
            GUI.enabled = guiEnabledBeforeUpload && !dropdownOpen && !topLayerInputBlocked;
            DrawFieldLabel(left, y, T("地图名称", "Map name"));
            _uploadName = GUI.TextField(new Rect(left, y + 20f, fieldW, 38f), _uploadName, _inputStyle);
            DrawFieldLabel(left + fieldW + 18f, y, T("作者", "Author"));
            _uploadAuthor = GUI.TextField(new Rect(left + fieldW + 18f, y + 20f, fieldW, 38f), _uploadAuthor, _inputStyle);
            y += 72f;

            DrawFieldLabel(left, y, T("MOD 版本", "MOD version"));
            string versionText = _versions.Count > 0 ? _versions[Mathf.Clamp(_selectedUploadVersion, 0, _versions.Count - 1)].version_name : T("未获取到版本", "No versions loaded");
            GUI.enabled = guiEnabledBeforeUpload && !dropdownOpen && !topLayerInputBlocked && !_uploading && _versions.Count > 0;
            if (GUI.Button(versionButtonRect, versionText + "  ▾", _buttonStyle))
            {
                bool open = !_uploadVersionDropdownOpen;
                _uploadVersionDropdownOpen = open;
                _uploadImageDropdownOpen = false;
                if (open)
                {
                    BlockTopLayerInputThisFrame();
                }
            }
            GUI.enabled = guiEnabledBeforeUpload && !dropdownOpen && !topLayerInputBlocked;
            DrawFieldLabel(left + fieldW + 18f, y, T("封面图片（可选）", "Cover image (optional)"));
            string imageLabel = string.IsNullOrEmpty(SelectedLocalImagePath) ? T("不上传封面  ▾", "No cover  ▾") : MapSaveService.DisplayName(SelectedLocalImagePath) + "  ▾";
            if (GUI.Button(imageButtonRect, imageLabel, _buttonStyle))
            {
                bool open = !_uploadImageDropdownOpen;
                _uploadImageDropdownOpen = open;
                _uploadVersionDropdownOpen = false;
                if (open)
                {
                    BlockTopLayerInputThisFrame();
                }
            }
            if (GUI.Button(new Rect(imageButtonRect.xMax + 6f, imageButtonRect.y, 54f, 38f), T("选图", "Pick"), _buttonStyle))
            {
                _log.LogInfo("Image browser button clicked.");
                CloseUploadDropdowns();
                OpenImagePicker();
            }
            if (GUI.Button(new Rect(imageButtonRect.xMax + 66f, imageButtonRect.y, 54f, 38f), T("自动", "Auto"), _buttonStyle))
            {
                _log.LogInfo("Auto image match button clicked. Json: " + (SelectedLocalSavePath ?? "<none>"));
                CloseUploadDropdowns();
                AutoSelectMatchingImage(true);
            }
            if (GUI.Button(new Rect(imageButtonRect.xMax + 126f, imageButtonRect.y, 54f, 38f), T("清空", "Clear"), _buttonStyle))
            {
                _log.LogInfo("Upload cover image cleared.");
                CloseUploadDropdowns();
                _selectedLocalImage = -1;
            }
            y += 72f;

            DrawFieldLabel(left, y, T("描述", "Description"));
            _uploadDescription = GUI.TextArea(new Rect(left, y + 20f, modal.width - 36f, 72f), _uploadDescription, _inputStyle);
            y += 104f;

            DrawFieldLabel(left, modal.y + 324f, T("本地地图 JSON", "Local map JSON"));
            GUI.enabled = guiEnabledBeforeUpload && !topLayerInputBlocked && !_uploading;
            string saveLabel = string.IsNullOrEmpty(SelectedLocalSavePath)
                ? T("选择本地 JSON  ▾", "Select local JSON  ▾")
                : MapSaveService.DisplayName(SelectedLocalSavePath) + "  ▾";
            if (GUI.Button(saveButtonRect, saveLabel, _buttonStyle))
            {
                _uploadSaveDropdownOpen = !_uploadSaveDropdownOpen;
                _uploadVersionDropdownOpen = false;
                _uploadImageDropdownOpen = false;
                if (_uploadSaveDropdownOpen)
                {
                    RefreshLocalSaves(false);
                    BlockTopLayerInputThisFrame();
                }
            }
            GUI.enabled = guiEnabledBeforeUpload;

            string scanInfo = _localSaves.Length == 0
                ? T("未找到本地 JSON。目录：", "No local JSON found. Directory: ") + MapSaveService.SavePath
                : T("已扫描 ", "Found ") + _localSaves.Length + T(" 个 JSON，点击上方选择", " JSON files. Use the selector above");
            GUI.Label(new Rect(left, saveButtonRect.yMax + 6f, modal.width - 36f, 18f), scanInfo, _mutedStyle);

            GUI.Label(new Rect(left, modal.yMax - 66f, modal.width - 280f, 24f), _uploading ? T("上传中，请稍候...", "Uploading, please wait...") : _status, _mutedStyle);
            if (GUI.Button(new Rect(modal.xMax - 186f, modal.yMax - 58f, 76f, 38f), T("刷新", "Refresh"), _buttonStyle))
            {
                RefreshLocalSaves(true);
            }
            GUI.enabled = guiEnabledBeforeUpload && !dropdownOpen && !topLayerInputBlocked && !_uploading;
            if (GUI.Button(new Rect(modal.xMax - 100f, modal.yMax - 58f, 82f, 38f), _uploading ? T("上传中", "Uploading") : T("确认上传", "Upload"), _primaryButtonStyle))
            {
                UploadSelected();
            }
            GUI.enabled = guiEnabledBeforeUpload;

            if (_uploadVersionDropdownOpen)
            {
                GUI.enabled = guiEnabledBeforeUpload && !topLayerInputBlocked;
                DrawUploadVersionDropdown(versionDropdownRect);
            }
            if (_uploadImageDropdownOpen)
            {
                GUI.enabled = guiEnabledBeforeUpload && !topLayerInputBlocked;
                DrawUploadImageDropdown(imageDropdownRect);
            }
            if (_uploadSaveDropdownOpen)
            {
                GUI.enabled = guiEnabledBeforeUpload && !topLayerInputBlocked;
                DrawUploadSaveDropdown(saveDropdownRect);
            }
            GUI.enabled = guiEnabledBeforeUpload;
        }

        private void ConsumeUploadDropdownOutsideClick(Rect dropdownRect)
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.MouseDown)
            {
                return;
            }

            if (dropdownRect.Contains(e.mousePosition))
            {
                return;
            }

            CloseUploadDropdowns();
            GUI.FocusControl(null);
            e.Use();
        }

        private void CloseUploadDropdowns()
        {
            _uploadVersionDropdownOpen = false;
            _uploadImageDropdownOpen = false;
            _uploadSaveDropdownOpen = false;
        }

        private void DrawUploadSaveDropdown(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, _panelStrongStyle);

            Rect filterRect = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 30f);
            string nextFilter = GUI.TextField(filterRect, _localSaveFilter, _inputStyle);
            if (!string.Equals(nextFilter, _localSaveFilter, StringComparison.Ordinal))
            {
                _localSaveFilter = nextFilter;
                ApplyLocalSaveFilter(true);
            }

            Rect listRect = new Rect(rect.x + 8f, filterRect.yMax + 6f, rect.width - 16f, rect.height - 50f);
            if (!string.IsNullOrEmpty(_localSaveError))
            {
                GUI.Label(listRect, _localSaveError, _dangerStyle);
                return;
            }
            if (_localSaves.Length == 0)
            {
                GUI.Label(listRect, T("未找到本地 JSON。", "No local JSON files found."), _mutedStyle);
                return;
            }
            if (_filteredLocalSaveIndexes.Count == 0)
            {
                GUI.Label(listRect, T("没有匹配的 JSON。", "No matching JSON files."), _mutedStyle);
                return;
            }

            const float rowH = 30f;
            Rect view = new Rect(0f, 0f, listRect.width - 18f, _filteredLocalSaveIndexes.Count * rowH);
            _uploadSaveScroll = GUI.BeginScrollView(listRect, _uploadSaveScroll, view, false, true);
            int first = Mathf.Max(0, Mathf.FloorToInt(_uploadSaveScroll.y / rowH) - 1);
            int last = Mathf.Min(_filteredLocalSaveIndexes.Count, first + Mathf.CeilToInt(listRect.height / rowH) + 2);
            for (int visibleIndex = first; visibleIndex < last; visibleIndex++)
            {
                int saveIndex = _filteredLocalSaveIndexes[visibleIndex];
                GUIStyle style = saveIndex == _selectedLocalSave ? _primaryButtonStyle : _buttonStyle;
                if (GUI.Button(new Rect(0f, visibleIndex * rowH, view.width, rowH - 3f),
                    MapSaveService.DisplayName(_localSaves[saveIndex]), style))
                {
                    _selectedLocalSave = saveIndex;
                    if (string.IsNullOrWhiteSpace(_uploadName))
                    {
                        _uploadName = Path.GetFileNameWithoutExtension(_localSaves[saveIndex]);
                    }
                    AutoSelectMatchingImage(false);
                    _uploadSaveDropdownOpen = false;
                    _log.LogInfo("JSON selected from upload dropdown: " + _localSaves[saveIndex]);
                }
            }
            GUI.EndScrollView();
        }

        private void DrawUploadImageDropdown(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, _panelStrongStyle);

            Rect filterRect = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 30f);
            string nextFilter = GUI.TextField(filterRect, _localImageFilter, _inputStyle);
            if (!string.Equals(nextFilter, _localImageFilter, StringComparison.Ordinal))
            {
                _localImageFilter = nextFilter;
                ApplyLocalImageFilter(true);
            }

            Rect listRect = new Rect(rect.x + 8f, filterRect.yMax + 6f, rect.width - 16f, rect.height - 50f);
            if (!string.IsNullOrEmpty(_localImageError))
            {
                GUI.Label(listRect, _localImageError, _dangerStyle);
                return;
            }
            if (_localImages.Length == 0)
            {
                GUI.Label(listRect, T("未找到封面图片。\n可放入：", "No cover images found.\nYou can place them in: ") + MapSaveService.CoverPath, _mutedStyle);
                return;
            }
            if (_filteredLocalImageIndexes.Count == 0)
            {
                GUI.Label(listRect, T("没有匹配的图片。", "No matching images."), _mutedStyle);
                return;
            }

            const float rowH = 28f;
            Rect view = new Rect(0f, 0f, listRect.width - 18f, _filteredLocalImageIndexes.Count * rowH);
            _uploadImageScroll = GUI.BeginScrollView(listRect, _uploadImageScroll, view, false, true);
            int first = Mathf.Max(0, Mathf.FloorToInt(_uploadImageScroll.y / rowH) - 1);
            int last = Mathf.Min(_filteredLocalImageIndexes.Count, first + Mathf.CeilToInt(listRect.height / rowH) + 2);
            for (int visibleIndex = first; visibleIndex < last; visibleIndex++)
            {
                int imageIndex = _filteredLocalImageIndexes[visibleIndex];
                GUIStyle style = imageIndex == _selectedLocalImage ? _primaryButtonStyle : _buttonStyle;
                if (GUI.Button(new Rect(0f, visibleIndex * rowH, view.width, rowH - 2f), MapSaveService.DisplayName(_localImages[imageIndex]), style))
                {
                    _selectedLocalImage = imageIndex;
                    _uploadImageDropdownOpen = false;
                    _log.LogInfo("Cover image selected from quick list: " + _localImages[imageIndex]);
                }
            }
            GUI.EndScrollView();
        }

        private void DrawImagePickerModal()
        {
            GUI.depth = -102;
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, _modalBackdropStyle);
            bool guiEnabledBeforePicker = GUI.enabled;
            bool pickerInputEnabled = guiEnabledBeforePicker && !IsTopLayerInputBlocked();
            GUI.enabled = pickerInputEnabled;

            Rect modal = new Rect(Mathf.Round((Screen.width - 760f) * 0.5f), Mathf.Round((Screen.height - 540f) * 0.5f), 760f, 540f);
            GUI.Box(modal, GUIContent.none, _rootStyle);

            GUI.Label(new Rect(modal.x + 18f, modal.y + 16f, 240f, 16f), T("选择封面图片", "SAFE IMAGE PICKER"), _tinyStyle);
            GUI.Label(new Rect(modal.x + 18f, modal.y + 32f, 240f, 34f), T("选择封面图片", "Pick Cover"), _titleStyle);
            if (GUI.Button(new Rect(modal.xMax - 54f, modal.y + 22f, 36f, 36f), "×", _iconButtonStyle))
            {
                _imagePickerOpen = false;
            }

            Rect rootsRect = new Rect(modal.x + 18f, modal.y + 82f, 160f, modal.height - 148f);
            GUI.Box(rootsRect, GUIContent.none, _panelStrongStyle);
            GUI.Label(new Rect(rootsRect.x + 10f, rootsRect.y + 8f, rootsRect.width - 20f, 18f), T("白名单目录", "SAFE ROOTS"), _tinyStyle);
            for (int i = 0; i < _imageRootPaths.Length; i++)
            {
                GUIStyle style = i == _imagePickerRootIndex ? _primaryButtonStyle : _buttonStyle;
                if (GUI.Button(new Rect(rootsRect.x + 10f, rootsRect.y + 34f + i * 34f, rootsRect.width - 20f, 30f), MapSaveService.GetImageRootLabel(_imageRootPaths[i]), style))
                {
                    _imagePickerRootIndex = i;
                    _log.LogInfo("Image picker root selected: " + _imageRootPaths[i]);
                    SetImagePickerDirectory(_imageRootPaths[i]);
                }
            }

            Rect filesRect = new Rect(rootsRect.xMax + 12f, rootsRect.y, modal.width - 210f, rootsRect.height);
            GUI.Box(filesRect, GUIContent.none, _panelStrongStyle);

            GUI.Label(new Rect(filesRect.x + 12f, filesRect.y + 8f, filesRect.width - 130f, 20f), ShortPath(_imagePickerDirectory), _mutedStyle);
            if (GUI.Button(new Rect(filesRect.xMax - 86f, filesRect.y + 8f, 74f, 28f), T("上级", "Up"), _buttonStyle))
            {
                GoImagePickerParent();
            }

            if (!string.IsNullOrEmpty(_imagePickerError))
            {
                GUI.Label(new Rect(filesRect.x + 12f, filesRect.y + 46f, filesRect.width - 24f, 64f), _imagePickerError, _dangerStyle);
            }

            Rect listRect = new Rect(filesRect.x + 10f, filesRect.y + 46f, filesRect.width - 20f, filesRect.height - 58f);
            DrawImagePickerList(listRect);

            string selectedText = string.IsNullOrEmpty(_imagePickerSelected)
                ? T("未选择图片", "No image selected")
                : T("已选择：", "Selected: ") + MapSaveService.DisplayName(_imagePickerSelected);
            GUI.Label(new Rect(modal.x + 18f, modal.yMax - 58f, modal.width - 220f, 24f), selectedText, _mutedStyle);

            if (GUI.Button(new Rect(modal.xMax - 188f, modal.yMax - 62f, 78f, 38f), T("取消", "Cancel"), _buttonStyle))
            {
                _imagePickerOpen = false;
            }
            GUI.enabled = pickerInputEnabled && !string.IsNullOrEmpty(_imagePickerSelected);
            if (GUI.Button(new Rect(modal.xMax - 100f, modal.yMax - 62f, 82f, 38f), T("使用图片", "Use"), _primaryButtonStyle))
            {
                UsePickedImage();
            }
            GUI.enabled = guiEnabledBeforePicker;
        }

        private void DrawImagePickerList(Rect listRect)
        {
            const float rowH = 30f;
            int totalRows = _imagePickerDirs.Length + _imagePickerFiles.Length;
            Rect view = new Rect(0f, 0f, listRect.width - 18f, totalRows * rowH);
            _imagePickerScroll = GUI.BeginScrollView(listRect, _imagePickerScroll, view, false, true);

            int first = Mathf.Max(0, Mathf.FloorToInt(_imagePickerScroll.y / rowH) - 2);
            int visibleRows = Mathf.CeilToInt(listRect.height / rowH) + 4;
            int last = Mathf.Min(totalRows, first + visibleRows);
            for (int rowIndex = first; rowIndex < last; rowIndex++)
            {
                Rect row = new Rect(0f, rowIndex * rowH, view.width, rowH - 3f);
                if (rowIndex < _imagePickerDirs.Length)
                {
                    string dir = _imagePickerDirs[rowIndex];
                    if (GUI.Button(row, "▸ " + MapSaveService.DisplayName(dir), _buttonStyle))
                    {
                        _log.LogInfo("Image picker directory clicked: " + dir);
                        SetImagePickerDirectory(dir);
                    }
                }
                else
                {
                    int fileIndex = rowIndex - _imagePickerDirs.Length;
                    string file = _imagePickerFiles[fileIndex];
                    GUIStyle style = string.Equals(file, _imagePickerSelected, StringComparison.OrdinalIgnoreCase) ? _primaryButtonStyle : _buttonStyle;
                    if (GUI.Button(row, "□ " + MapSaveService.DisplayName(file), style))
                    {
                        _imagePickerSelected = file;
                        _log.LogInfo("Image picker file clicked: " + file);
                        UsePickedImage();
                    }
                }
            }

            GUI.EndScrollView();
        }

        private void OpenImagePicker()
        {
            _uploadImageDropdownOpen = false;
            _imageRootPaths = MapSaveService.GetImageRootPaths();
            _log.LogInfo("Opening image picker. Whitelisted root count: " + _imageRootPaths.Length);
            for (int i = 0; i < _imageRootPaths.Length; i++)
            {
                _log.LogInfo("Image picker root[" + i + "]: " + _imageRootPaths[i]);
            }
            if (_imageRootPaths.Length == 0)
            {
                _log.LogWarning("Image picker cannot open: no whitelisted image roots.");
                ShowToast(T("没有可用图片目录", "No image directories are available"));
                return;
            }

            _imagePickerRootIndex = Mathf.Clamp(_imagePickerRootIndex, 0, _imageRootPaths.Length - 1);
            _imagePickerSelected = SelectedLocalImagePath ?? string.Empty;
            SetImagePickerDirectory(!string.IsNullOrEmpty(_imagePickerSelected) ? Path.GetDirectoryName(_imagePickerSelected) : _imageRootPaths[_imagePickerRootIndex]);
            _imagePickerOpen = true;
            BlockTopLayerInputThisFrame();
            _log.LogInfo("Image picker opened. Directory: " + _imagePickerDirectory + ", preselected: " + (string.IsNullOrEmpty(_imagePickerSelected) ? "<none>" : _imagePickerSelected));
        }

        private void SetImagePickerDirectory(string directory)
        {
            string normalized;
            if (!MapSaveService.TryNormalizeWhitelistedDirectory(directory, out normalized))
            {
                _imagePickerError = T("目录不在白名单内。", "Directory is outside the whitelist.");
                _log.LogWarning("Image picker rejected directory: " + (directory ?? "<null>"));
                return;
            }

            _imagePickerDirectory = normalized;
            _imagePickerError = string.Empty;
            try
            {
                _imagePickerDirs = MapSaveService.GetChildDirectories(normalized);
                _imagePickerFiles = MapSaveService.GetImageFilesInDirectory(normalized);
                _log.LogInfo("Image picker loaded directory: " + normalized + " (dirs=" + _imagePickerDirs.Length + ", images=" + _imagePickerFiles.Length + ")");
            }
            catch (Exception ex)
            {
                _imagePickerDirs = new string[0];
                _imagePickerFiles = new string[0];
                _imagePickerError = T("读取目录失败：", "Failed to read directory: ") + ex.Message;
                _log.LogWarning("Image picker failed to read directory '" + normalized + "': " + ex.Message);
            }
            _imagePickerScroll = Vector2.zero;
        }

        private void GoImagePickerParent()
        {
            if (string.IsNullOrEmpty(_imagePickerDirectory))
            {
                return;
            }

            string parent = Path.GetDirectoryName(_imagePickerDirectory);
            string normalized;
            if (!string.IsNullOrEmpty(parent) && MapSaveService.TryNormalizeWhitelistedDirectory(parent, out normalized))
            {
                _log.LogInfo("Image picker parent directory selected: " + normalized);
                SetImagePickerDirectory(normalized);
            }
            else
            {
                _log.LogWarning("Image picker parent rejected or unavailable: " + (parent ?? "<null>"));
            }
        }

        private void UsePickedImage()
        {
            string normalized;
            if (!MapSaveService.TryNormalizeWhitelistedImage(_imagePickerSelected, out normalized))
            {
                _log.LogWarning("Image picker rejected image: " + (string.IsNullOrEmpty(_imagePickerSelected) ? "<none>" : _imagePickerSelected));
                ShowToast(T("图片不在白名单内", "Image is outside the whitelist"));
                return;
            }

            int index = Array.FindIndex(_localImages, p => string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                Array.Resize(ref _localImages, _localImages.Length + 1);
                _localImages[_localImages.Length - 1] = normalized;
                ApplyLocalImageFilter(false);
                index = _localImages.Length - 1;
            }

            _selectedLocalImage = index;
            if (_accountOpen)
            {
                _editReplaceImage = true;
                _editRemoveImage = false;
            }
            _imagePickerOpen = false;
            _uploadImageDropdownOpen = false;
            _log.LogInfo("Cover image applied: " + normalized);
            ShowToast(T("已选择封面：", "Cover selected: ") + MapSaveService.DisplayName(normalized));
        }

        private string ShortPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string label = path;
            if (label.Length > 74)
            {
                label = "..." + label.Substring(label.Length - 71);
            }
            return label;
        }

        private void DrawUploadVersionDropdown(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, _panelStrongStyle);
            if (_versions.Count == 0)
            {
                GUI.Label(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, 20f), T("暂无版本", "No versions"), _mutedStyle);
                return;
            }

            const float rowH = 30f;
            Rect scrollRect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
            Rect view = new Rect(0f, 0f, scrollRect.width - 18f, _versions.Count * rowH);
            _uploadVersionScroll = GUI.BeginScrollView(scrollRect, _uploadVersionScroll, view, false, true);
            int first = Mathf.Max(0, Mathf.FloorToInt(_uploadVersionScroll.y / rowH) - 1);
            int last = Mathf.Min(_versions.Count, first + Mathf.CeilToInt(scrollRect.height / rowH) + 2);
            for (int i = first; i < last; i++)
            {
                GUIStyle style = i == _selectedUploadVersion ? _primaryButtonStyle : _buttonStyle;
                if (GUI.Button(new Rect(0f, i * rowH, view.width, rowH - 3f), _versions[i].version_name, style))
                {
                    _selectedUploadVersion = i;
                    _uploadVersionDropdownOpen = false;
                }
            }
            GUI.EndScrollView();
        }

        private void DrawLocalSaveList(Rect savesRect)
        {
            const float rowH = 28f;
            Rect scrollRect = new Rect(savesRect.x + 8f, savesRect.y + 8f, savesRect.width - 16f, savesRect.height - 16f);
            Rect view = new Rect(0f, 0f, scrollRect.width - 18f, _filteredLocalSaveIndexes.Count * rowH);
            _uploadSaveScroll = GUI.BeginScrollView(scrollRect, _uploadSaveScroll, view, false, true);

            int first = Mathf.Max(0, Mathf.FloorToInt(_uploadSaveScroll.y / rowH) - 2);
            int visibleRows = Mathf.CeilToInt(scrollRect.height / rowH) + 4;
            int last = Mathf.Min(_filteredLocalSaveIndexes.Count, first + visibleRows);
            for (int visibleIndex = first; visibleIndex < last; visibleIndex++)
            {
                int saveIndex = _filteredLocalSaveIndexes[visibleIndex];
                Rect row = new Rect(0f, visibleIndex * rowH, view.width, rowH - 2f);
                GUIStyle style = saveIndex == _selectedLocalSave ? _primaryButtonStyle : _buttonStyle;
                string label = MapSaveService.DisplayName(_localSaves[saveIndex]);
                if (GUI.Button(row, label, style))
                {
                    _selectedLocalSave = saveIndex;
                    if (string.IsNullOrWhiteSpace(_uploadName))
                    {
                        _uploadName = Path.GetFileNameWithoutExtension(_localSaves[saveIndex]);
                    }
                    AutoSelectMatchingImage(false);
                }
            }

            GUI.EndScrollView();
        }

        private void DrawFieldLabel(float x, float y, string text)
        {
            GUI.Label(new Rect(x, y, 220f, 18f), text, _mutedStyle);
        }

        private void DrawToast()
        {
            if (string.IsNullOrEmpty(_toast) || Time.unscaledTime > _toastUntil)
            {
                return;
            }

            float width = Mathf.Min(340f, _windowRect.width - 80f);
            Rect rect = new Rect(_windowRect.x + (_windowRect.width - width) * 0.5f, _windowRect.y + 74f, width, 34f);
            GUI.Label(rect, "● " + _toast, _toastStyle);
        }

        private void RefreshAll()
        {
            _loadedOnce = true;
            FetchModVersions();
            FetchMaps();
            if (_api.IsSignedIn && _accountOpen)
            {
                FetchAccountMaps();
            }
        }

        private void RefreshSessionIfNeeded()
        {
            if (Time.unscaledTime < _nextSessionRefreshCheckTime || !_api.ShouldRefreshSession || _refreshingSession)
            {
                return;
            }

            _nextSessionRefreshCheckTime = Time.unscaledTime + 30f;
            _refreshingSession = true;
            _api.RefreshSession((ok, error) =>
            {
                _refreshingSession = false;
                if (!ok && !string.IsNullOrEmpty(error))
                {
                    _status = error;
                    ShowToast(error);
                    FetchMaps();
                }
            });
        }

        private void FetchMaps()
        {
            if (_loadingMaps)
            {
                return;
            }

            _loadingMaps = true;
            _status = T("正在获取地图列表...", "Fetching map list...");
            _api.FetchMaps(_page, _pageSize, _query, _sort, _versionFilter, (response, error) =>
            {
                _loadingMaps = false;
                if (!string.IsNullOrEmpty(error))
                {
                    _status = error;
                    ShowToast(error);
                    return;
                }

                _maps = response.data ?? new List<MapEntry>();
                _pagination = response.pagination;
                _selectedMapIndex = Mathf.Clamp(_selectedMapIndex, 0, Mathf.Max(0, _maps.Count - 1));
                _mapScroll = Vector2.zero;
                _status = T("地图列表已更新", "Map list updated");
            });
        }

        private void FetchAccountMaps()
        {
            if (_loadingAccountMaps || !_api.IsSignedIn)
            {
                return;
            }

            _loadingAccountMaps = true;
            _api.FetchAccountMaps((response, error) =>
            {
                _loadingAccountMaps = false;
                if (!string.IsNullOrEmpty(error))
                {
                    _status = error;
                    ShowToast(error);
                    return;
                }

                _accountMaps = response.data ?? new List<MapEntry>();
                _selectedAccountMapIndex = Mathf.Clamp(_selectedAccountMapIndex, 0, Mathf.Max(0, _accountMaps.Count - 1));
                if (_accountMaps.Count > 0)
                {
                    SelectAccountMap(_selectedAccountMapIndex);
                }
                _status = T("我的地图已更新", "My maps updated");
            });
        }

        private void FetchModVersions()
        {
            if (_loadingVersions)
            {
                return;
            }

            _loadingVersions = true;
            _api.FetchModVersions((response, error) =>
            {
                _loadingVersions = false;
                if (!string.IsNullOrEmpty(error))
                {
                    _status = error;
                    return;
                }

                _versions = response.data ?? new List<ModVersionEntry>();
                _selectedUploadVersion = Mathf.Clamp(_selectedUploadVersion, 0, Mathf.Max(0, _versions.Count - 1));
            });
        }

        private void DownloadSelected()
        {
            MapEntry map = SelectedMap;
            if (map == null || _downloading || _downloadConfirmOpen)
            {
                return;
            }

            MapDownloadInfo info = _api.GetDownloadInfo(map);
            if (info.Status == MapDownloadStatus.UpToDate)
            {
                _status = T("这张地图已经下载，且本地文件没有变化。", "This map is already downloaded and unchanged locally.");
                ShowToast(_status);
                return;
            }
            if (info.Status == MapDownloadStatus.UpdateAvailable || info.Status == MapDownloadStatus.LocalModified)
            {
                _downloadConfirmMapId = map.id;
                _downloadConfirmOpen = true;
                BlockTopLayerInputThisFrame();
                return;
            }

            StartDownload(map, false);
        }

        private void StartDownload(MapEntry map, bool allowOverwriteLocalChanges)
        {
            if (map == null || _downloading)
            {
                return;
            }

            _downloading = true;
            _status = T("正在下载 ", "Downloading ") + map.name + "...";
            ShowToast(T("正在下载地图...", "Downloading map..."));
            _api.DownloadMap(map, allowOverwriteLocalChanges, (savedPath, error) =>
            {
                _downloading = false;
                if (!string.IsNullOrEmpty(error))
                {
                    _status = error;
                    ShowToast(error);
                    return;
                }

                _status = T("已保存：", "Saved: ") + savedPath;
                _downloadInfoCache.Remove(map.id);
                _downloadInfoCacheUntil = 0f;
                ShowToast(T("下载完成", "Download complete"));
            });
        }

        private string DownloadButtonLabel(MapEntry map)
        {
            if (map == null)
            {
                return T("下载", "Get");
            }

            MapDownloadInfo info = GetDownloadInfoForDisplay(map);
            switch (info.Status)
            {
                case MapDownloadStatus.UpToDate:
                    return T("已下载", "Saved");
                case MapDownloadStatus.UpdateAvailable:
                    return T("有更新", "Update");
                case MapDownloadStatus.LocalModified:
                    return T("本地已改", "Changed");
                default:
                    return T("下载", "Get");
            }
        }

        private MapEntry FindMapById(string mapId)
        {
            if (string.IsNullOrEmpty(mapId))
            {
                return null;
            }

            for (int i = 0; i < _maps.Count; i++)
            {
                if (string.Equals(_maps[i].id, mapId, System.StringComparison.Ordinal))
                {
                    return _maps[i];
                }
            }

            return null;
        }

        private MapDownloadInfo GetDownloadInfoForDisplay(MapEntry map)
        {
            if (map == null || string.IsNullOrEmpty(map.id))
            {
                return _api.GetDownloadInfo(map);
            }

            if (Time.unscaledTime >= _downloadInfoCacheUntil)
            {
                _downloadInfoCache.Clear();
                _downloadInfoCacheUntil = Time.unscaledTime + 1.0f;
            }

            MapDownloadInfo info;
            if (!_downloadInfoCache.TryGetValue(map.id, out info))
            {
                info = _api.GetDownloadInfo(map);
                _downloadInfoCache[map.id] = info;
            }

            return info;
        }

        private void ToggleLikeSelected()
        {
            MapEntry map = SelectedMap;
            if (map == null || _liking)
            {
                return;
            }

            _liking = true;
            _api.ToggleLike(map, (response, error) =>
            {
                _liking = false;
                if (!string.IsNullOrEmpty(error))
                {
                    _status = error;
                    ShowToast(error);
                    return;
                }

                _status = response.liked ? T("已点赞", "Liked") : T("已取消点赞", "Unliked");
                ShowToast(_status);
            });
        }

        private void UploadSelected()
        {
            if (_uploading)
            {
                return;
            }
            string selectedPath = SelectedLocalSavePath;
            if (string.IsNullOrEmpty(selectedPath))
            {
                _log.LogWarning("Upload blocked: no local JSON selected.");
                ShowToast(T("请选择本地 JSON", "Please select a local JSON"));
                return;
            }

            string version = _versions.Count > 0 ? _versions[Mathf.Clamp(_selectedUploadVersion, 0, _versions.Count - 1)].version_name : string.Empty;
            string imagePath = SelectedLocalImagePath;
            _log.LogInfo("Upload started. Json: " + selectedPath + ", image: " + (string.IsNullOrEmpty(imagePath) ? "<none>" : imagePath) + ", version: " + (string.IsNullOrEmpty(version) ? "<none>" : version));
            _uploading = true;
            _status = T("正在上传地图...", "Uploading map...");
            _api.UploadMap(_uploadName, _uploadAuthor, version, _uploadDescription, selectedPath, imagePath, error =>
            {
                _uploading = false;
                if (!string.IsNullOrEmpty(error))
                {
                    _log.LogWarning("Upload failed. Json: " + selectedPath + ", image: " + (string.IsNullOrEmpty(imagePath) ? "<none>" : imagePath) + ", error: " + error);
                    _status = error;
                    ShowToast(T("上传失败", "Upload failed"));
                    return;
                }

                _log.LogInfo("Upload succeeded. Json: " + selectedPath + ", image: " + (string.IsNullOrEmpty(imagePath) ? "<none>" : imagePath));
                _status = T("上传成功", "Upload succeeded");
                ShowToast(T("上传成功", "Upload succeeded"));
                _uploadOpen = false;
                FetchMaps();
                if (_api.IsSignedIn)
                {
                    FetchAccountMaps();
                }
            });
        }

        private void SignInSelected()
        {
            if (_refreshingSession)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(_loginEmail) || string.IsNullOrEmpty(_loginPassword))
            {
                ShowToast(T("请输入邮箱和密码", "Enter email and password"));
                return;
            }

            _refreshingSession = true;
            _status = T("正在登录...", "Signing in...");
            _api.SignIn(_loginEmail, _loginPassword, (response, error) =>
            {
                _refreshingSession = false;
                if (!string.IsNullOrEmpty(error))
                {
                    _status = error;
                    ShowToast(error);
                    return;
                }

                _loginPassword = string.Empty;
                _loginOpen = false;
                _status = T("登录成功", "Signed in");
                ShowToast(_status);
                FetchMaps();
                OpenAccount();
            });
        }

        private void OpenLogin()
        {
            _loginOpen = true;
            _accountOpen = false;
            _uploadOpen = false;
            _imagePickerOpen = false;
            BlockTopLayerInputThisFrame();
        }

        private void OpenAccount()
        {
            if (!_api.IsSignedIn)
            {
                OpenLogin();
                return;
            }

            _accountOpen = true;
            _loginOpen = false;
            _uploadOpen = false;
            _imagePickerOpen = false;
            BlockTopLayerInputThisFrame();
            RefreshLocalSaves(true);
            FetchAccountMaps();
        }

        private void SelectAccountMap(int index)
        {
            // Each map reuses the same IMGUI editor controls; clear the previous text selection before loading new values.
            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;
            GUIUtility.hotControl = 0;

            _selectedAccountMapIndex = Mathf.Clamp(index, 0, Mathf.Max(0, _accountMaps.Count - 1));
            MapEntry map = SelectedAccountMap;
            if (map == null)
            {
                return;
            }

            _editName = map.name ?? string.Empty;
            _editAuthor = map.author ?? string.Empty;
            _editVersion = map.mod_version ?? string.Empty;
            _editDescription = map.description ?? string.Empty;
            _editReplaceJson = false;
            _accountJsonDropdownOpen = false;
            _accountVersionDropdownOpen = false;
            _accountVersionScroll = Vector2.zero;
            _editReplaceImage = false;
            _editRemoveImage = false;
            _deleteConfirmMapId = string.Empty;
        }

        private void SaveAccountMap()
        {
            MapEntry map = SelectedAccountMap;
            if (map == null || _savingAccountMap)
            {
                return;
            }

            string jsonPath = _editReplaceJson ? SelectedLocalSavePath : null;
            string imagePath = _editReplaceImage ? SelectedLocalImagePath : null;
            _savingAccountMap = true;
            _status = T("正在保存地图...", "Saving map...");
            _api.UpdateMap(map.id, _editName, _editAuthor, _editVersion, _editDescription, jsonPath, imagePath, _editRemoveImage, error =>
            {
                _savingAccountMap = false;
                if (!string.IsNullOrEmpty(error))
                {
                    _status = error;
                    ShowToast(error);
                    return;
                }

                _status = T("地图已保存", "Map saved");
                ShowToast(_status);
                FetchMaps();
                FetchAccountMaps();
            });
        }

        private void DeleteAccountMap(MapEntry map)
        {
            if (map == null || _deletingAccountMap)
            {
                return;
            }

            _deletingAccountMap = true;
            _status = T("正在删除地图...", "Deleting map...");
            _api.DeleteMap(map.id, error =>
            {
                _deletingAccountMap = false;
                _deleteConfirmMapId = string.Empty;
                if (!string.IsNullOrEmpty(error))
                {
                    _status = error;
                    ShowToast(error);
                    return;
                }

                _status = T("地图已删除", "Map deleted");
                ShowToast(_status);
                _selectedAccountMapIndex = 0;
                FetchMaps();
                FetchAccountMaps();
            });
        }

        private void OpenUpload()
        {
            _log.LogInfo("Upload dialog opened.");
            _uploadOpen = true;
            BlockTopLayerInputThisFrame();
            _uploadVersionDropdownOpen = false;
            _uploadImageDropdownOpen = false;
            _uploadSaveDropdownOpen = false;
            RefreshLocalSaves(true);
            _uploadAuthor = _api.IsSignedIn && !string.IsNullOrEmpty(_api.Session.DisplayName) ? _api.Session.DisplayName : GetSteamName();
            if (_versions.Count == 0)
            {
                FetchModVersions();
            }
            if (!string.IsNullOrEmpty(SelectedLocalSavePath) && string.IsNullOrWhiteSpace(_uploadName))
            {
                _uploadName = Path.GetFileNameWithoutExtension(SelectedLocalSavePath);
            }
            AutoSelectMatchingImage(false);
        }

        private void RefreshLocalSaves(bool resetScroll = false)
        {
            string selectedPath = SelectedLocalSavePath;
            try
            {
                _localSaves = MapSaveService.GetLocalJsonFiles();
                _localImages = MapSaveService.GetLocalImageFiles();
                _localSaveError = string.Empty;
                _localImageError = string.Empty;
                if (resetScroll)
                {
                    _log.LogInfo("Local map scan completed. SavePath: " + MapSaveService.SavePath + ", jsonCount=" + _localSaves.Length + ", imageCount=" + _localImages.Length);
                }
            }
            catch (Exception ex)
            {
                _localSaves = new string[0];
                _localImages = new string[0];
                _localSaveError = T("扫描 Map Saves 失败：", "Failed to scan Map Saves: ") + ex.Message;
                _localImageError = T("扫描封面图片失败：", "Failed to scan cover images: ") + ex.Message;
                _log.LogWarning("Local map/image scan failed. SavePath: " + MapSaveService.SavePath + ", error: " + ex.Message);
            }

            _nextLocalSaveScanTime = Time.unscaledTime + 3f;
            ApplyLocalSaveFilter(resetScroll);
            ApplyLocalImageFilter(resetScroll);

            if (!string.IsNullOrEmpty(selectedPath))
            {
                int existing = Array.FindIndex(_localSaves, p => string.Equals(p, selectedPath, StringComparison.OrdinalIgnoreCase));
                if (existing >= 0)
                {
                    _selectedLocalSave = existing;
                }
            }

            if ((_selectedLocalSave < 0 || _selectedLocalSave >= _localSaves.Length) && _filteredLocalSaveIndexes.Count > 0)
            {
                _selectedLocalSave = _filteredLocalSaveIndexes[0];
            }
            if (_selectedLocalImage >= _localImages.Length)
            {
                _selectedLocalImage = -1;
            }
        }

        private void ApplyLocalSaveFilter(bool resetScroll)
        {
            _filteredLocalSaveIndexes.Clear();
            string filter = (_localSaveFilter ?? string.Empty).Trim();
            for (int i = 0; i < _localSaves.Length; i++)
            {
                string name = MapSaveService.DisplayName(_localSaves[i]);
                if (string.IsNullOrEmpty(filter) || name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _filteredLocalSaveIndexes.Add(i);
                }
            }

            if (_filteredLocalSaveIndexes.Count > 0 && !_filteredLocalSaveIndexes.Contains(_selectedLocalSave))
            {
                _selectedLocalSave = _filteredLocalSaveIndexes[0];
            }

            if (resetScroll)
            {
                _uploadSaveScroll = Vector2.zero;
            }
        }

        private string SelectedLocalSavePath
        {
            get
            {
                return _selectedLocalSave >= 0 && _selectedLocalSave < _localSaves.Length ? _localSaves[_selectedLocalSave] : null;
            }
        }

        private void CycleSelectedLocalSave()
        {
            RefreshLocalSaves(false);
            if (_filteredLocalSaveIndexes.Count == 0)
            {
                ShowToast(T("未找到本地 JSON", "No local JSON found"));
                return;
            }

            int currentVisible = _filteredLocalSaveIndexes.IndexOf(_selectedLocalSave);
            int nextVisible = currentVisible < 0 ? 0 : (currentVisible + 1) % _filteredLocalSaveIndexes.Count;
            _selectedLocalSave = _filteredLocalSaveIndexes[nextVisible];
            ShowToast(T("已选择 JSON：", "JSON selected: ") + MapSaveService.DisplayName(SelectedLocalSavePath));
        }

        private void ApplyLocalImageFilter(bool resetScroll)
        {
            _filteredLocalImageIndexes.Clear();
            string filter = (_localImageFilter ?? string.Empty).Trim();
            for (int i = 0; i < _localImages.Length; i++)
            {
                string name = MapSaveService.DisplayName(_localImages[i]);
                if (string.IsNullOrEmpty(filter) || name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _filteredLocalImageIndexes.Add(i);
                }
            }

            if (resetScroll)
            {
                _uploadImageScroll = Vector2.zero;
            }
        }

        private string SelectedLocalImagePath
        {
            get
            {
                return _selectedLocalImage >= 0 && _selectedLocalImage < _localImages.Length ? _localImages[_selectedLocalImage] : null;
            }
        }

        private void AutoSelectMatchingImage(bool showToast)
        {
            string match = MapSaveService.FindMatchingImage(SelectedLocalSavePath, _localImages);
            if (string.IsNullOrEmpty(match))
            {
                _log.LogInfo("Auto image match not found for json: " + (SelectedLocalSavePath ?? "<none>"));
                if (showToast)
                {
                    ShowToast(T("未找到同名封面", "No matching cover found"));
                }
                return;
            }

            int index = Array.FindIndex(_localImages, p => string.Equals(p, match, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _selectedLocalImage = index;
                _log.LogInfo("Auto image match selected: " + match);
                if (showToast)
                {
                    ShowToast(T("已匹配封面：", "Matched cover: ") + MapSaveService.DisplayName(match));
                }
            }
        }

        private void CycleVersionFilter()
        {
            if (_versions.Count == 0)
            {
                FetchModVersions();
                return;
            }

            if (string.IsNullOrEmpty(_versionFilter))
            {
                _versionFilter = _versions[0].version_name;
            }
            else
            {
                int index = _versions.FindIndex(v => string.Equals(v.version_name, _versionFilter, StringComparison.Ordinal));
                index++;
                _versionFilter = index >= _versions.Count ? string.Empty : _versions[index].version_name;
            }

            _page = 1;
            FetchMaps();
        }

        private void SelectMap(int index)
        {
            _selectedMapIndex = Mathf.Clamp(index, 0, Mathf.Max(0, _maps.Count - 1));
        }

        private void ShowToast(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 3.6f;
        }

        private string T(string zh, string en)
        {
            return string.Equals(_language, "en", StringComparison.OrdinalIgnoreCase) ? en : zh;
        }

        private MapEntry SelectedMap
        {
            get
            {
                return _maps != null && _maps.Count > 0 && _selectedMapIndex >= 0 && _selectedMapIndex < _maps.Count
                    ? _maps[_selectedMapIndex]
                    : null;
            }
        }

        private MapEntry SelectedAccountMap
        {
            get
            {
                return _accountMaps != null && _accountMaps.Count > 0 && _selectedAccountMapIndex >= 0 && _selectedAccountMapIndex < _accountMaps.Count
                    ? _accountMaps[_selectedAccountMapIndex]
                    : null;
            }
        }

        private static string ShortAccountName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Account";
            }

            string clean = CleanUiText(value.Trim());
            return clean.Length <= 14 ? clean : clean.Substring(0, 13) + "...";
        }

        private void OpenAccountWebsite()
        {
            Application.OpenURL("https://peakmap.top/account");
        }

        private string GetSteamName()
        {
            try
            {
                string name = SteamFriends.GetPersonaName();
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning("Failed to read Steam persona name: " + ex.Message);
            }

            return "Steam Player";
        }

        private void EnsureStyles()
        {
            if (_stylesReady)
            {
                return;
            }

            try
            {
                _uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 16);
            }
            catch (Exception ex)
            {
                _log.LogWarning("Failed to create Chinese UI font: " + ex.Message);
            }

            _rootStyle = Box(new Color(0.020f, 0.032f, 0.026f, 0.995f), new Color(0.18f, 0.26f, 0.20f, 0.82f));
            _panelStyle = Box(new Color(0.014f, 0.025f, 0.019f, 0.985f), new Color(0.13f, 0.20f, 0.16f, 0.90f));
            _panelStrongStyle = Box(new Color(0.022f, 0.036f, 0.028f, 0.99f), new Color(0.16f, 0.24f, 0.18f, 0.90f));
            _detailMetaStyle = Box(new Color(0.030f, 0.048f, 0.038f, 0.94f), new Color(0.15f, 0.23f, 0.17f, 0.72f));
            _detailDescriptionStyle = Box(new Color(0.025f, 0.040f, 0.032f, 0.92f), new Color(0.14f, 0.22f, 0.17f, 0.65f));
            _cardStyle = Box(new Color(0.028f, 0.044f, 0.034f, 0.995f), new Color(0.15f, 0.23f, 0.17f, 0.95f));
            _buttonStyle = Button(new Color(0.044f, 0.060f, 0.050f, 0.995f), new Color(0.80f, 0.88f, 0.82f, 1f), false);
            _primaryButtonStyle = Button(new Color(0.18f, 0.78f, 0.42f, 1f), new Color(0.02f, 0.10f, 0.06f, 1f), true);
            _iconButtonStyle = Button(new Color(0.044f, 0.060f, 0.050f, 0.995f), new Color(0.86f, 0.94f, 0.86f, 1f), false);
            _sidebarButtonStyle = Button(new Color(0.018f, 0.030f, 0.024f, 0.995f), new Color(0.76f, 0.84f, 0.78f, 1f), false);
            _sidebarButtonStyle.alignment = TextAnchor.MiddleLeft;
            _sidebarButtonStyle.padding = new RectOffset(16, 8, 0, 0);
            _sidebarSelectedStyle = Button(new Color(0.12f, 0.20f, 0.14f, 0.995f), new Color(0.30f, 0.98f, 0.58f, 1f), false);
            _sidebarSelectedStyle.alignment = TextAnchor.MiddleLeft;
            _sidebarSelectedStyle.padding = new RectOffset(16, 8, 0, 0);
            _statBoxStyle = Box(new Color(0.040f, 0.060f, 0.048f, 0.98f), new Color(0.16f, 0.25f, 0.18f, 0.90f));
            _inputStyle = Input();
            _textAreaStyle = Input();
            _textAreaStyle.wordWrap = true;
            _textAreaStyle.alignment = TextAnchor.UpperLeft;
            _titleStyle = Label(24, FontStyle.Bold, Color.white);
            _h2Style = Label(18, FontStyle.Bold, Color.white);
            _h2Style.alignment = TextAnchor.UpperLeft;
            _labelStyle = Label(13, FontStyle.Bold, new Color(0.93f, 0.98f, 0.92f, 1f));
            _statLabelStyle = Label(11, FontStyle.Bold, new Color(0.98f, 0.64f, 0.24f, 1f));
            _statLabelStyle.wordWrap = false;
            _statLabelStyle.clipping = TextClipping.Overflow;
            _statValueStyle = Label(15, FontStyle.Bold, new Color(0.91f, 0.98f, 0.90f, 1f));
            _statValueStyle.wordWrap = false;
            _statValueStyle.clipping = TextClipping.Overflow;
            _cardStatsStyle = Label(13, FontStyle.Bold, new Color(0.86f, 0.96f, 0.86f, 1f));
            _cardStatsStyle.wordWrap = false;
            _cardStatsStyle.clipping = TextClipping.Clip;
            _mutedStyle = Label(12, FontStyle.Normal, new Color(0.72f, 0.78f, 0.72f, 1f));
            _cardDescStyle = Label(12, FontStyle.Normal, new Color(0.72f, 0.78f, 0.72f, 1f));
            _cardDescStyle.wordWrap = true;
            _detailTitleStyle = Label(20, FontStyle.Bold, Color.white);
            _detailTextStyle = Label(13, FontStyle.Normal, new Color(0.80f, 0.87f, 0.80f, 1f));
            _detailTextStyle.wordWrap = true;
            _detailTextStyle.padding = new RectOffset(0, 2, 1, 1);
            _tinyStyle = Label(10, FontStyle.Bold, new Color(0.95f, 0.67f, 0.32f, 1f));
            _dangerStyle = Label(12, FontStyle.Bold, new Color(0.95f, 0.45f, 0.38f, 1f));
            _badgeStyle = Box(new Color(0.035f, 0.115f, 0.075f, 0.98f), new Color(0.18f, 0.42f, 0.28f, 0.95f));
            _badgeStyle.alignment = TextAnchor.MiddleCenter;
            _badgeStyle.normal.textColor = new Color(0.70f, 0.95f, 0.76f, 1f);
            _badgeStyle.fontStyle = FontStyle.Bold;
            _badgeStyle.fontSize = 12;
            _badgeStyle.font = _uiFont;
            _apiBadgeStyle = Box(new Color(0.032f, 0.040f, 0.037f, 0.98f), new Color(0.14f, 0.18f, 0.16f, 0.95f));
            _apiBadgeStyle.alignment = TextAnchor.MiddleCenter;
            _apiBadgeStyle.normal.textColor = new Color(0.68f, 0.78f, 0.70f, 1f);
            _apiBadgeStyle.fontStyle = FontStyle.Bold;
            _apiBadgeStyle.fontSize = 12;
            _apiBadgeStyle.font = _uiFont;
            _pagePillStyle = Box(new Color(0.032f, 0.040f, 0.037f, 0.98f), new Color(0.16f, 0.20f, 0.18f, 0.95f));
            _pagePillStyle.alignment = TextAnchor.MiddleCenter;
            _pagePillStyle.normal.textColor = new Color(0.78f, 0.88f, 0.78f, 1f);
            _pagePillStyle.fontStyle = FontStyle.Bold;
            _pagePillStyle.fontSize = 13;
            _pagePillStyle.font = _uiFont;
            _toastStyle = Box(new Color(0.025f, 0.030f, 0.028f, 0.98f), new Color(0.18f, 0.42f, 0.28f, 0.90f));
            _toastStyle.normal.textColor = new Color(0.86f, 0.95f, 0.86f, 1f);
            _toastStyle.alignment = TextAnchor.MiddleCenter;
            _toastStyle.font = _uiFont;
            _thumbStyle = Box(new Color(0.055f, 0.070f, 0.060f, 1f), new Color(0.16f, 0.20f, 0.18f, 0.90f));
            _modalBackdropStyle = Box(new Color(0f, 0f, 0f, 0.78f), new Color(0f, 0f, 0f, 0f));
            _placeholderThumb = CreatePlaceholderThumb();
            _stylesReady = true;
        }

        private GUIStyle Box(Color bg, Color border)
        {
            Texture2D tex = MakeTex(8, 8, bg, border);
            return new GUIStyle(GUI.skin.box)
            {
                normal = { background = tex },
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(8, 8, 6, 6)
            };
        }

        private GUIStyle Button(Color bg, Color text, bool accent)
        {
            Color border = accent ? new Color(0.20f, 0.62f, 0.38f, 0.95f) : new Color(0.18f, 0.22f, 0.20f, 0.95f);
            Color hoverBg = accent ? new Color(0.18f, 0.58f, 0.37f, 1f) : new Color(0.058f, 0.066f, 0.062f, 0.995f);
            Color hoverBorder = accent ? new Color(0.24f, 0.70f, 0.44f, 0.95f) : new Color(0.25f, 0.30f, 0.27f, 0.95f);
            Texture2D normal = MakeTex(8, 8, bg, border);
            Texture2D hover = MakeTex(8, 8, hoverBg, hoverBorder);
            GUIStyle style = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                font = _uiFont,
                normal = { background = normal, textColor = text },
                hover = { background = hover, textColor = text },
                active = { background = hover, textColor = text },
                border = new RectOffset(1, 1, 1, 1)
            };
            return style;
        }

        private GUIStyle Input()
        {
            GUIStyle style = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 14,
                font = _uiFont,
                normal = { background = MakeTex(8, 8, new Color(0.010f, 0.012f, 0.012f, 0.995f), new Color(0.18f, 0.22f, 0.20f, 0.95f)), textColor = Color.white },
                focused = { background = MakeTex(8, 8, new Color(0.016f, 0.020f, 0.018f, 0.995f), new Color(0.24f, 0.46f, 0.34f, 0.95f)), textColor = Color.white },
                padding = new RectOffset(10, 10, 9, 8),
                border = new RectOffset(1, 1, 1, 1)
            };
            return style;
        }

        private GUIStyle Label(int size, FontStyle fontStyle, Color color)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = fontStyle,
                font = _uiFont,
                normal = { textColor = color },
                wordWrap = true,
                clipping = TextClipping.Clip
            };
        }

        private Texture2D MakeTex(int width, int height, Color bg, Color border)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool edge = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    pixels[y * width + x] = edge ? border : bg;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Texture2D CreatePlaceholderThumb()
        {
            int w = 256;
            int h = 144;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float contour = Mathf.Abs(Mathf.Sin((x * 0.05f) + (y * 0.035f)));
                    float route = Mathf.Abs(y - (h * 0.58f + Mathf.Sin(x * 0.035f) * 26f));
                    Color c = new Color(0.05f, 0.075f + contour * 0.05f, 0.06f, 1f);
                    if (route < 2.4f)
                    {
                        c = new Color(0.34f, 0.90f, 0.52f, 1f);
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string SingleLine(string value, int maxLength)
        {
            string result = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (result.Length <= maxLength)
            {
                return result;
            }

            return result.Substring(0, Mathf.Max(0, maxLength - 3)) + "...";
        }

        private static string CompactCardDescription(string value, float width)
        {
            string result = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (string.IsNullOrEmpty(result))
            {
                return result;
            }

            int charactersPerLine = Mathf.Max(24, Mathf.FloorToInt(width / 8f));
            int maxLength = Mathf.Max(48, charactersPerLine * 2);
            if (result.Length <= maxLength)
            {
                return result;
            }

            return result.Substring(0, Mathf.Max(0, maxLength - 3)).TrimEnd() + "...";
        }

        private static string CleanUiText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (char.IsHighSurrogate(ch))
                {
                    if (i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                    {
                        i++;
                    }
                    continue;
                }
                if (char.IsLowSurrogate(ch))
                {
                    continue;
                }
                if (char.IsControl(ch) && ch != '\n' && ch != '\r' && ch != '\t')
                {
                    continue;
                }
                if (IsEmojiLikeBmp(ch))
                {
                    continue;
                }

                sb.Append(ch);
            }

            return sb.ToString();
        }

        private static bool IsEmojiLikeBmp(char ch)
        {
            int c = ch;
            return (c >= 0x2600 && c <= 0x27BF)
                || (c >= 0xFE00 && c <= 0xFE0F)
                || (c >= 0x200D && c <= 0x200D)
                || (c >= 0x20E3 && c <= 0x20E3);
        }

        private static string FormatDetailDescription(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            string normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            System.Text.StringBuilder sb = new System.Text.StringBuilder(normalized.Length + 24);
            bool wroteLine = false;
            bool lastBlank = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                {
                    if (wroteLine && !lastBlank)
                    {
                        sb.AppendLine();
                        lastBlank = true;
                    }
                    continue;
                }

                if (wroteLine && !lastBlank && IsDescriptionKeyLine(line))
                {
                    sb.AppendLine();
                }

                sb.AppendLine(line);
                wroteLine = true;
                lastBlank = false;
            }

            return sb.ToString().TrimEnd();
        }

        private static bool IsDescriptionKeyLine(string line)
        {
            int colon = line.IndexOf(':');
            if (colon <= 0 || colon > 18)
            {
                return false;
            }

            string key = line.Substring(0, colon).Trim();
            return string.Equals(key, "Desc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "Description", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "Difficulty", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "Testers", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "Note", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "Player Count", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "Players", StringComparison.OrdinalIgnoreCase);
        }

        private string ShortVersion(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return T("全部", "All");
            }

            int paren = value.IndexOf('(');
            if (paren > 0)
            {
                string core = value.Substring(0, paren).Trim();
                if (!string.IsNullOrEmpty(core))
                {
                    return core.Length <= 10 ? core : core.Substring(0, 10);
                }
            }

            return value.Length <= 10 ? value : value.Substring(0, 10);
        }

        private static string DateOnly(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "-";
            }
            return value.Length >= 10 ? value.Substring(0, 10) : value;
        }
    }
}
