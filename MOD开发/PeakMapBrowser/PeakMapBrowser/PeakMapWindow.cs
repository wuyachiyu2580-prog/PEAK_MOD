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
        private bool _uploadOpen;
        private bool _loadedOnce;
        private int _topLayerOpenedFrame = -1;

        private Rect _windowRect;
        private Vector2 _mapScroll;
        private Vector2 _uploadSaveScroll;
        private Vector2 _detailScroll;

        private List<MapEntry> _maps = new List<MapEntry>();
        private List<ModVersionEntry> _versions = new List<ModVersionEntry>();
        private PaginationInfo _pagination;
        private int _selectedMapIndex;
        private int _page = 1;
        private string _query = string.Empty;
        private string _sort = "newest";
        private string _versionFilter = string.Empty;
        private string _languageMode;
        private string _language;
        private float _nextLanguageCheckTime;
        private string _status = string.Empty;
        private string _toast = string.Empty;
        private float _toastUntil;

        private string[] _localSaves = new string[0];
        private readonly List<int> _filteredLocalSaveIndexes = new List<int>();
        private int _selectedLocalSave;
        private string _localSaveFilter = string.Empty;
        private string _localSaveError = string.Empty;
        private float _nextLocalSaveScanTime;
        private int _selectedUploadVersion;
        private bool _uploadVersionDropdownOpen;
        private Vector2 _uploadVersionScroll;
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

        private GUIStyle _rootStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _panelStrongStyle;
        private GUIStyle _detailMetaStyle;
        private GUIStyle _detailDescriptionStyle;
        private GUIStyle _cardStyle;
        private GUIStyle _cardSelectedStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _primaryButtonStyle;
        private GUIStyle _iconButtonStyle;
        private GUIStyle _inputStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _h2Style;
        private GUIStyle _labelStyle;
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
        private GUIStyle _modalBackdropStyle;
        private GUIStyle _dangerStyle;
        private Texture2D _placeholderThumb;
        private Font _uiFont;

        public PeakMapWindow(MonoBehaviour runner, ManualLogSource log, string apiBaseUrl, string language, int pageSize)
        {
            _runner = runner;
            _log = log;
            _languageMode = language;
            _language = PeakMapLanguage.Resolve(language, log);
            _api = new PeakMapApiClient(runner, log, apiBaseUrl, _language);
            _pageSize = pageSize;
            _windowRect = new Rect(0f, 0f, 960f, 640f);
            _status = T("按 F8 打开或关闭地图库", "Press F8 to open or close the map browser");
            _log.LogInfo("PEAK Map Browser language: " + _language + " (mode=" + PeakMapLanguage.NormalizeMode(_languageMode) + ")");
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
                _uploadVersionDropdownOpen = false;
                _uploadImageDropdownOpen = false;
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
            string next = PeakMapLanguage.Resolve(_languageMode, _log);
            if (string.Equals(next, _language, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _language = next;
            _api.SetLanguage(_language);
            _status = T("语言已切换为中文", "Language switched to English");
            ShowToast(_status);
            _log.LogInfo("PEAK Map Browser language changed to " + _language + ".");
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

            EnsureStyles();
            CenterWindow();

            GUI.depth = 10;
            DrawDimBackground();
            GUI.Box(_windowRect, GUIContent.none, _rootStyle);
            if (!_uploadOpen && !_imagePickerOpen)
            {
                DrawHeader();
                if (!_uploadOpen && !_imagePickerOpen)
                {
                    DrawContent();
                }
            }

            if (_uploadOpen && !_imagePickerOpen)
            {
                DrawUploadModal();
            }
            if (_imagePickerOpen)
            {
                DrawImagePickerModal();
            }
            DrawToast();

            ConsumeOverlayEvents();
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
            float width = Mathf.Round(Mathf.Min(960f, Screen.width - 48f));
            float height = Mathf.Round(Mathf.Min(640f, Screen.height - 48f));
            _windowRect = new Rect(Mathf.Round((Screen.width - width) * 0.5f), Mathf.Round((Screen.height - height) * 0.5f), width, height);
        }

        private void DrawDimBackground()
        {
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.42f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void DrawHeader()
        {
            Rect header = new Rect(_windowRect.x, _windowRect.y, _windowRect.width, 82f);
            GUI.Box(header, GUIContent.none, _panelStrongStyle);

            Rect mark = new Rect(header.x + 18f, header.y + 18f, 44f, 44f);
            GUI.Box(mark, GUIContent.none, _badgeStyle);
            GUI.Label(new Rect(mark.x + 11f, mark.y + 2f, 28f, 36f), "/", _titleStyle);

            GUI.Label(new Rect(header.x + 72f, header.y + 18f, 180f, 16f), "EXPEDITION DATABASE", _tinyStyle);
            GUI.Label(new Rect(header.x + 72f, header.y + 32f, 190f, 34f), T("PEAK 地图库", "PEAK Maps"), _titleStyle);

            float x = header.x + 276f;
            float y = header.y + 23f;
            float closeW = 38f;
            float uploadW = 118f;
            float refreshW = 38f;
            float sortW = 76f;
            float versionW = 96f;
            float gap = 8f;
            float searchW = Mathf.Max(170f, header.xMax - x - closeW - uploadW - refreshW - sortW - versionW - gap * 6f - 18f);

            GUI.SetNextControlName("PeakMapSearch");
            string nextQuery = GUI.TextField(new Rect(x, y, searchW, 38f), _query, _inputStyle);
            if (nextQuery != _query)
            {
                _query = nextQuery;
            }
            x += searchW + gap;

            if (GUI.Button(new Rect(x, y, versionW, 38f), ShortVersion(_versionFilter), _buttonStyle))
            {
                CycleVersionFilter();
            }
            x += versionW + gap;

            if (GUI.Button(new Rect(x, y, sortW, 38f), _sort == "downloads" ? T("下载量", "Popular") : T("最新", "Newest"), _buttonStyle))
            {
                _sort = _sort == "downloads" ? "newest" : "downloads";
                _page = 1;
                FetchMaps();
            }
            x += sortW + gap;

            if (GUI.Button(new Rect(x, y, refreshW, 38f), "↻", _iconButtonStyle))
            {
                RefreshAll();
            }
            x += refreshW + gap;

            if (GUI.Button(new Rect(x, y, uploadW, 38f), T("↑ 上传地图", "↑ Upload"), _primaryButtonStyle))
            {
                OpenUpload();
            }
            x += uploadW + gap;

            if (GUI.Button(new Rect(x, y, closeW, 38f), "×", _iconButtonStyle))
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

        private void DrawContent()
        {
            Rect content = new Rect(_windowRect.x + 14f, _windowRect.y + 96f, _windowRect.width - 28f, _windowRect.height - 110f);
            float detailW = Mathf.Min(336f, content.width * 0.38f);
            Rect listRect = new Rect(content.x, content.y, content.width - detailW - 14f, content.height);
            Rect detailRect = new Rect(listRect.xMax + 14f, content.y, detailW, content.height);

            DrawMapList(listRect);
            DrawDetail(detailRect);
        }

        private void DrawMapList(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, _panelStyle);

            string total = _pagination != null ? _pagination.total.ToString() : (_maps != null ? _maps.Count.ToString() : "0");
            GUI.Label(new Rect(rect.x + 14f, rect.y + 14f, rect.width - 160f, 22f),
                T("共 ", "") + total + T(" 张社区地图 · 当前筛选 ", " community maps · Filter ")
                    + (string.IsNullOrEmpty(_versionFilter) ? T("全部版本", "All versions") : _versionFilter),
                _mutedStyle);
            GUI.Label(new Rect(rect.xMax - 142f, rect.y + 10f, 128f, 26f), "API: peakmap.top", _apiBadgeStyle);

            Rect cardsRect = new Rect(rect.x + 14f, rect.y + 50f, rect.width - 28f, rect.height - 104f);
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

            DrawPagination(new Rect(rect.x + 14f, rect.yMax - 44f, rect.width - 28f, 34f));
        }

        private void DrawCards(Rect rect)
        {
            int columns = rect.width > 440f ? 2 : 1;
            float gap = 12f;
            float cardW = (rect.width - (columns - 1) * gap - 10f) / columns;
            float cardH = 270f;
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
            GUI.Box(rect, GUIContent.none, selected ? _cardSelectedStyle : _cardStyle);

            Rect inner = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
            Rect thumb = new Rect(inner.x, inner.y, inner.width, 108f);
            DrawThumbnail(thumb, map);
            GUI.Label(new Rect(thumb.x + 10f, thumb.y + 10f, 86f, 22f), ShortVersion(map.mod_version), _badgeStyle);

            float textY = inner.y + 124f;
            GUI.Label(new Rect(inner.x + 11f, textY, inner.width - 22f, 32f), CleanUiText(Safe(map.name, T("未命名地图", "Untitled map"))), _h2Style);
            GUI.Label(new Rect(inner.x + 11f, textY + 35f, inner.width - 22f, 19f), "○ " + CleanUiText(Safe(map.author, "Unknown")), _mutedStyle);
            GUI.Label(new Rect(inner.x + 11f, textY + 60f, inner.width - 22f, 42f), CleanUiText(Safe(map.description, T("没有描述", "No description"))), _cardDescStyle);

            GUI.Label(new Rect(inner.x + 11f, inner.yMax - 34f, 72f, 26f), "↓ " + map.downloads, _mutedStyle);
            if (GUI.Button(new Rect(inner.xMax - 74f, inner.yMax - 38f, 60f, 30f), T("下载", "Get"), _primaryButtonStyle))
            {
                SelectMap(index);
                DownloadSelected();
            }

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                SelectMap(index);
                Event.current.Use();
            }
        }

        private void DrawThumbnail(Rect rect, MapEntry map)
        {
            Texture2D tex = GetThumbnail(map);
            GUI.DrawTexture(rect, tex != null ? tex : _placeholderThumb, ScaleMode.ScaleAndCrop);
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

            Rect thumb = new Rect(rect.x + 14f, rect.y + 14f, 118f, 74f);
            DrawThumbnail(thumb, map);
            float titleX = thumb.xMax + 12f;
            GUI.Label(new Rect(titleX, rect.y + 16f, rect.xMax - titleX - 14f, 16f), "SELECTED MAP", _tinyStyle);
            GUI.Label(new Rect(titleX, rect.y + 34f, rect.xMax - titleX - 14f, 28f), CleanUiText(Safe(map.name, T("未命名地图", "Untitled map"))), _detailTitleStyle);
            GUI.Label(new Rect(titleX, rect.y + 64f, rect.xMax - titleX - 14f, 18f), T("下载 ", "Downloads ") + map.downloads + " · " + DateOnly(map.created_at), _mutedStyle);

            float y = thumb.yMax + 14f;

            DrawDetailMeta(new Rect(rect.x + 14f, y, rect.width - 28f, 58f), map);
            y += 68f;

            float buttonY = rect.yMax - 48f;
            Rect descRect = new Rect(rect.x + 14f, y, rect.width - 28f, Mathf.Max(110f, buttonY - y - 12f));
            DrawScrollableDescription(descRect, FormatDetailDescription(CleanUiText(Safe(map.description, T("没有描述", "No description")))));

            GUI.enabled = !_downloading;
            if (GUI.Button(new Rect(rect.x + 16f, buttonY, rect.width - 126f, 36f), _downloading ? T("下载中...", "Downloading...") : T("下载到本地", "Download"), _primaryButtonStyle))
            {
                DownloadSelected();
            }
            GUI.enabled = true;
            if (GUI.Button(new Rect(rect.xMax - 102f, buttonY, 86f, 36f), T("刷新", "Refresh"), _buttonStyle))
            {
                RefreshAll();
            }
        }

        private void DrawDetailMeta(Rect rect, MapEntry map)
        {
            GUI.Box(rect, GUIContent.none, _detailMetaStyle);
            float leftW = (rect.width - 20f) * 0.50f;
            float rightX = rect.x + leftW + 10f;
            GUI.Label(new Rect(rect.x + 10f, rect.y + 7f, leftW - 10f, 16f), T("作者", "AUTHOR"), _tinyStyle);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 27f, leftW - 10f, 20f), CleanUiText(Safe(map.author, "Unknown")), _labelStyle);
            GUI.Label(new Rect(rightX, rect.y + 7f, rect.xMax - rightX - 10f, 16f), T("版本", "VERSION"), _tinyStyle);
            GUI.Label(new Rect(rightX, rect.y + 27f, rect.xMax - rightX - 10f, 20f), CleanUiText(Safe(map.mod_version, "-")), _labelStyle);
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

        private void DrawUploadModal()
        {
            GUI.depth = 0;
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, _modalBackdropStyle);

            float modalHeight = Mathf.Min(620f, Screen.height - 48f);
            Rect modal = new Rect(Mathf.Round((Screen.width - 680f) * 0.5f), Mathf.Round((Screen.height - modalHeight) * 0.5f), 680f, Mathf.Round(modalHeight));
            GUI.Box(modal, GUIContent.none, _rootStyle);

            GUI.Label(new Rect(modal.x + 18f, modal.y + 16f, 220f, 16f), "SUBMIT LOCAL SAVE", _tinyStyle);
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
            float versionY = y + 72f;
            Rect versionButtonRect = new Rect(left, versionY + 20f, fieldW, 38f);
            Rect imageButtonRect = new Rect(left + fieldW + 18f, versionY + 20f, fieldW - 136f, 38f);
            if (_uploadVersionDropdownOpen)
            {
                versionDropdownRect = new Rect(versionButtonRect.x, versionButtonRect.yMax + 4f, versionButtonRect.width, Mathf.Min(150f, Mathf.Max(38f, _versions.Count * 30f + 10f)));
            }
            if (_uploadImageDropdownOpen)
            {
                imageDropdownRect = new Rect(imageButtonRect.x, imageButtonRect.yMax + 4f, fieldW, 150f);
            }

            bool dropdownOpen = _uploadVersionDropdownOpen || _uploadImageDropdownOpen;
            bool topLayerInputBlocked = IsTopLayerInputBlocked();
            if (dropdownOpen)
            {
                ConsumeUploadDropdownOutsideClick(_uploadVersionDropdownOpen ? versionDropdownRect : imageDropdownRect);
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
            if (GUI.Button(new Rect(imageButtonRect.xMax + 6f, imageButtonRect.y, 42f, 38f), T("浏览", "Pick"), _buttonStyle))
            {
                _log.LogInfo("Image browser button clicked.");
                CloseUploadDropdowns();
                OpenImagePicker();
            }
            if (GUI.Button(new Rect(imageButtonRect.xMax + 52f, imageButtonRect.y, 40f, 38f), T("自动", "Auto"), _buttonStyle))
            {
                _log.LogInfo("Auto image match button clicked. Json: " + (SelectedLocalSavePath ?? "<none>"));
                CloseUploadDropdowns();
                AutoSelectMatchingImage(true);
            }
            if (GUI.Button(new Rect(imageButtonRect.xMax + 96f, imageButtonRect.y, 36f, 38f), T("清除", "Clear"), _buttonStyle))
            {
                _log.LogInfo("Upload cover image cleared.");
                CloseUploadDropdowns();
                _selectedLocalImage = -1;
            }
            y += 72f;

            DrawFieldLabel(left, y, T("描述", "Description"));
            _uploadDescription = GUI.TextArea(new Rect(left, y + 20f, modal.width - 36f, 72f), _uploadDescription, _inputStyle);
            y += 104f;

            DrawFieldLabel(left, y, T("本地地图 JSON", "Local map JSON"));
            string nextFilter = GUI.TextField(new Rect(left + 110f, y - 4f, modal.width - 146f, 30f), _localSaveFilter, _inputStyle);
            if (!string.Equals(nextFilter, _localSaveFilter, StringComparison.Ordinal))
            {
                _localSaveFilter = nextFilter;
                ApplyLocalSaveFilter(true);
            }

            float footerTop = modal.yMax - 74f;
            Rect savesRect = new Rect(left, y + 34f, modal.width - 36f, Mathf.Max(76f, footerTop - y - 42f));
            GUI.Box(savesRect, GUIContent.none, _panelStrongStyle);
            if (!string.IsNullOrEmpty(_localSaveError))
            {
                GUI.Label(new Rect(savesRect.x + 14f, savesRect.y + 14f, savesRect.width - 28f, 42f), _localSaveError, _dangerStyle);
            }
            else if (_localSaves.Length == 0)
            {
                GUI.Label(new Rect(savesRect.x + 14f, savesRect.y + 14f, savesRect.width - 28f, 42f), T("未找到本地 JSON。目录已自动创建：\n", "No local JSON found. Directory was created:\n") + MapSaveService.SavePath, _mutedStyle);
            }
            else if (_filteredLocalSaveIndexes.Count == 0)
            {
                GUI.Label(new Rect(savesRect.x + 14f, savesRect.y + 14f, savesRect.width - 28f, 42f), T("没有匹配筛选条件的 JSON。", "No JSON matches the filter."), _mutedStyle);
            }
            else
            {
                DrawLocalSaveList(savesRect);
            }

            string scanInfo = T("扫描到 ", "Found ") + _localSaves.Length + T(" 个 JSON", " JSON files");
            if (!string.IsNullOrWhiteSpace(_localSaveFilter))
            {
                scanInfo += T(" · 匹配 ", " · Matched ") + _filteredLocalSaveIndexes.Count + T(" 个", "");
            }
            GUI.Label(new Rect(left, savesRect.yMax + 4f, modal.width - 36f, 18f), scanInfo, _mutedStyle);

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
            GUI.depth = -1;
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, _modalBackdropStyle);
            bool guiEnabledBeforePicker = GUI.enabled;
            bool pickerInputEnabled = guiEnabledBeforePicker && !IsTopLayerInputBlocked();
            GUI.enabled = pickerInputEnabled;

            Rect modal = new Rect(Mathf.Round((Screen.width - 760f) * 0.5f), Mathf.Round((Screen.height - 540f) * 0.5f), 760f, 540f);
            GUI.Box(modal, GUIContent.none, _rootStyle);

            GUI.Label(new Rect(modal.x + 18f, modal.y + 16f, 240f, 16f), "SAFE IMAGE PICKER", _tinyStyle);
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

            Rect rect = new Rect(_windowRect.xMax - 230f, _windowRect.yMax - 54f, 210f, 34f);
            GUI.Label(rect, "● " + _toast, _toastStyle);
        }

        private void RefreshAll()
        {
            _loadedOnce = true;
            FetchModVersions();
            FetchMaps();
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
            if (map == null || _downloading)
            {
                return;
            }

            _downloading = true;
            _status = T("正在下载 ", "Downloading ") + map.name + "...";
            ShowToast(T("正在下载地图...", "Downloading map..."));
            _api.DownloadMap(map, (savedPath, error) =>
            {
                _downloading = false;
                if (!string.IsNullOrEmpty(error))
                {
                    _status = error;
                    ShowToast(error);
                    return;
                }

                _status = T("已保存：", "Saved: ") + savedPath;
                ShowToast(T("下载完成", "Download complete"));
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
            });
        }

        private void OpenUpload()
        {
            _log.LogInfo("Upload dialog opened.");
            _uploadOpen = true;
            BlockTopLayerInputThisFrame();
            _uploadVersionDropdownOpen = false;
            _uploadImageDropdownOpen = false;
            RefreshLocalSaves(true);
            _uploadAuthor = GetSteamName();
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

            _rootStyle = Box(new Color(0.045f, 0.052f, 0.049f, 0.995f), new Color(0.22f, 0.27f, 0.24f, 0.80f));
            _panelStyle = Box(new Color(0.024f, 0.028f, 0.026f, 0.985f), new Color(0.16f, 0.19f, 0.17f, 0.90f));
            _panelStrongStyle = Box(new Color(0.035f, 0.040f, 0.038f, 0.99f), new Color(0.18f, 0.21f, 0.19f, 0.90f));
            _detailMetaStyle = Box(new Color(0.028f, 0.032f, 0.030f, 0.94f), new Color(0.12f, 0.15f, 0.14f, 0.72f));
            _detailDescriptionStyle = Box(new Color(0.032f, 0.036f, 0.034f, 0.92f), new Color(0.12f, 0.15f, 0.14f, 0.65f));
            _cardStyle = Box(new Color(0.045f, 0.052f, 0.049f, 0.995f), new Color(0.18f, 0.22f, 0.20f, 0.95f));
            _cardSelectedStyle = Box(new Color(0.048f, 0.058f, 0.053f, 0.995f), new Color(0.17f, 0.36f, 0.25f, 0.95f));
            _buttonStyle = Button(new Color(0.042f, 0.048f, 0.046f, 0.995f), new Color(0.80f, 0.86f, 0.80f, 1f), false);
            _primaryButtonStyle = Button(new Color(0.15f, 0.53f, 0.34f, 1f), Color.white, true);
            _iconButtonStyle = Button(new Color(0.042f, 0.048f, 0.046f, 0.995f), new Color(0.86f, 0.92f, 0.86f, 1f), false);
            _inputStyle = Input();
            _titleStyle = Label(24, FontStyle.Bold, Color.white);
            _h2Style = Label(18, FontStyle.Bold, Color.white);
            _h2Style.alignment = TextAnchor.UpperLeft;
            _labelStyle = Label(13, FontStyle.Bold, new Color(0.93f, 0.98f, 0.92f, 1f));
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
