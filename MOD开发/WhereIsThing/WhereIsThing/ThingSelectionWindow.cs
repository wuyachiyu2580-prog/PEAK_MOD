using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WhereIsThing
{
    internal sealed class ThingSelectionWindow
    {
        private const float MinCellWidth = 220f;
        private const float CellHeight = 44f;
        private const float CellSpacing = 6f;

        private readonly Canvas _canvas;
        private readonly TMP_FontAsset _font;
        private readonly Action<HashSet<ushort>, HashSet<ThingLuggageType>, HashSet<ThingSceneTargetType>, ThingNameLanguage, ThingLocationScope> _apply;
        private readonly Action _cancel;
        private readonly GameObject _root;
        private readonly GameObject _cursorWindowObject;
        private readonly MenuWindow _cursorWindow;
        private readonly RectTransform _viewport;
        private readonly RectTransform _content;
        private readonly TextMeshProUGUI _languageButtonText;
        private readonly TextMeshProUGUI _categoryButtonText;
        private readonly TextMeshProUGUI _countText;
        private readonly TextMeshProUGUI _emptyText;
        private readonly TMP_InputField _searchInput;
        private readonly Toggle _selectedOnlyToggle;
        private readonly List<ThingTargetDefinition> _catalog = new List<ThingTargetDefinition>();
        private readonly List<ThingTargetDefinition> _visibleDefinitions = new List<ThingTargetDefinition>();
        private readonly Dictionary<ThingLocationScope, Toggle> _scopeToggles = new Dictionary<ThingLocationScope, Toggle>();
        private HashSet<ushort> _workingSelection = new HashSet<ushort>();
        private ThingNameLanguage _workingLanguage;
        private ThingLocationScope _workingScopes;
        private string _workingCategory = "All";
        private HashSet<ThingLuggageType> _workingLuggageTypes = new HashSet<ThingLuggageType>();
        private HashSet<ThingSceneTargetType> _workingSceneTargetTypes = new HashSet<ThingSceneTargetType>();
        private bool _selectedOnly;
        private bool _isOpen;
        private bool _isRebuilding;
        private float _lastListWidth = -1f;
        private bool _previousCursorVisible;
        private CursorLockMode _previousCursorLockState;

        public ThingSelectionWindow(Canvas canvas, TMP_FontAsset font,
            Action<HashSet<ushort>, HashSet<ThingLuggageType>, HashSet<ThingSceneTargetType>, ThingNameLanguage, ThingLocationScope> apply, Action cancel)
        {
            _canvas = canvas;
            _font = font;
            _apply = apply;
            _cancel = cancel;

            _root = CreateRect("WhereIsThingWindowOverlay", canvas.transform);
            Stretch(_root.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            Image overlay = _root.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.54f);
            overlay.raycastTarget = true;

            // GUIManager derives cursor state from MenuWindow.AllActiveWindows.
            _cursorWindowObject = CreateRect("WhereIsThingMenuWindow", _root.transform);
            _cursorWindowObject.SetActive(false);
            _cursorWindow = _cursorWindowObject.AddComponent<MenuWindow>();

            GameObject panel = CreateRect("ItemBrowserPanel", _root.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.08f, 0.06f);
            panelRect.anchorMax = new Vector2(0.92f, 0.94f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.075f, 0.08f, 0.075f, 0.98f);
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.7f, 0.66f, 0.52f, 0.75f);
            outline.effectDistance = new Vector2(2f, 2f);

            CreateText(panel.transform, "Title", "WhereIsThing / 物品与目标位置", 25f, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -50f), new Vector2(-32f, -12f));
            CreateText(panel.transform, "Subtitle", "Select targets to show / 选择要显示的目标", 14f, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -76f), new Vector2(-32f, -52f));

            _languageButtonText = CreateButton(panel.transform, "LanguageButton", new Vector2(32f, -96f), new Vector2(230f, 42f), string.Empty, CycleLanguage);
            _categoryButtonText = CreateButton(panel.transform, "CategoryButton", new Vector2(274f, -96f), new Vector2(230f, 42f), string.Empty, CycleCategory);
            _selectedOnlyToggle = CreateFilterToggle(panel.transform, "Selected only / 仅显示已选", new Vector2(516f, -96f), new Vector2(180f, 42f));
            _searchInput = CreateSearch(panel.transform);
            _countText = CreateText(panel.transform, "Count", string.Empty, 14f, TextAlignmentOptions.Right,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-250f, -76f), new Vector2(-32f, -52f));

            CreateText(panel.transform, "ScopeTitle", "Locations / 显示范围", 14f, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -151f), new Vector2(250f, -128f));
            CreateScopeToggle(panel.transform, "Ground / 地面", ThingLocationScope.Ground, 32f);
            CreateScopeToggle(panel.transform, "Held / 手持", ThingLocationScope.Held, 224f);
            CreateScopeToggle(panel.transform, "Backpack / 背包", ThingLocationScope.Backpack, 416f);

            GameObject viewportObject = CreateRect("Viewport", panel.transform);
            _viewport = viewportObject.GetComponent<RectTransform>();
            _viewport.anchorMin = new Vector2(0f, 0f);
            _viewport.anchorMax = new Vector2(1f, 1f);
            _viewport.offsetMin = new Vector2(32f, 78f);
            _viewport.offsetMax = new Vector2(-32f, -204f);
            Image viewportImage = viewportObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.18f);
            Mask mask = viewportObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject scrollObject = CreateRect("Scroll", viewportObject.transform);
            Stretch(scrollObject.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = _viewport;
            _content = CreateRect("Content", scrollObject.transform).GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0f, 0f);
            scroll.content = _content;
            VerticalLayoutGroup layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _emptyText = CreateText(panel.transform, "Empty", "No matching targets / 没有匹配目标", 17f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-180f, -10f), new Vector2(180f, 34f));
            _emptyText.color = new Color(0.72f, 0.72f, 0.68f, 1f);

            CreateActionButton(panel.transform, "SelectAll", "Select visible / 全选当前", new Vector2(32f, 24f), new Vector2(180f, 42f), SelectVisible);
            CreateActionButton(panel.transform, "Clear", "Clear visible / 清除当前", new Vector2(222f, 24f), new Vector2(180f, 42f), ClearVisible);
            CreateActionButton(panel.transform, "Close", "Cancel / 取消", new Vector2(-212f, 24f), new Vector2(180f, 42f), Close);
            CreateActionButton(panel.transform, "Apply", "Apply / 应用", new Vector2(-32f, 24f), new Vector2(180f, 42f), Apply);

            _searchInput.onValueChanged.AddListener(delegate { RebuildRows(); });
            _root.SetActive(false);
        }

        public bool IsOpen { get { return _isOpen; } }

        public void Open(IEnumerable<ThingTargetDefinition> catalog, IEnumerable<ushort> selected, IEnumerable<ThingLuggageType> selectedLuggageTypes,
            IEnumerable<ThingSceneTargetType> selectedSceneTargetTypes,
            ThingNameLanguage language, ThingLocationScope scopes)
        {
            if (_font == null)
            {
                return;
            }

            _catalog.Clear();
            _catalog.AddRange(catalog ?? Enumerable.Empty<ThingTargetDefinition>());
            _workingSelection = new HashSet<ushort>(selected ?? Enumerable.Empty<ushort>());
            _workingLuggageTypes = new HashSet<ThingLuggageType>(selectedLuggageTypes ?? Enumerable.Empty<ThingLuggageType>());
            _workingSceneTargetTypes = new HashSet<ThingSceneTargetType>(selectedSceneTargetTypes ?? Enumerable.Empty<ThingSceneTargetType>());
            _workingLanguage = language;
            // Luggage is controlled by the selected luggage types, not by a separate scope toggle.
            _workingScopes = scopes & ~ThingLocationScope.Luggage;
            if (_workingLuggageTypes.Count > 0)
            {
                _workingScopes |= ThingLocationScope.Luggage;
            }
            _workingCategory = "All";
            _selectedOnly = false;
            _searchInput.text = string.Empty;
            _isOpen = true;
            _lastListWidth = -1f;
            _isRebuilding = true;
            _selectedOnlyToggle.isOn = false;
            foreach (KeyValuePair<ThingLocationScope, Toggle> scopeToggle in _scopeToggles)
            {
                scopeToggle.Value.isOn = (_workingScopes & scopeToggle.Key) != 0;
            }
            _isRebuilding = false;
            _previousCursorVisible = Cursor.visible;
            _previousCursorLockState = Cursor.lockState;
            _root.SetActive(true);
            RegisterCursorWindow();
            MaintainCursor();
            Canvas.ForceUpdateCanvases();
            RebuildRows();
        }

        public void Tick()
        {
            if (!_isOpen)
            {
                return;
            }

            MaintainCursor();
            float width = _viewport.rect.width;
            if (width > 0f && Mathf.Abs(width - _lastListWidth) > 4f)
            {
                RebuildRows();
            }
        }

        public void MaintainCursor()
        {
            if (_isOpen)
            {
                RegisterCursorWindow();
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        public void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            CloseInternal(true);
        }

        public void Dispose()
        {
            if (_isOpen)
            {
                RestoreCursor();
                _isOpen = false;
            }
            UnregisterCursorWindow();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }
        }

        private void Apply()
        {
            if (_apply != null)
            {
                _apply(new HashSet<ushort>(_workingSelection), new HashSet<ThingLuggageType>(_workingLuggageTypes),
                    new HashSet<ThingSceneTargetType>(_workingSceneTargetTypes), _workingLanguage, _workingScopes);
            }
            CloseInternal(false);
        }

        private void CloseInternal(bool notifyCancel)
        {
            _isOpen = false;
            _root.SetActive(false);
            UnregisterCursorWindow();
            RestoreCursor();
            if (notifyCancel && _cancel != null)
            {
                _cancel();
            }
        }

        private void RestoreCursor()
        {
            Cursor.visible = _previousCursorVisible;
            Cursor.lockState = _previousCursorLockState;
        }

        private void RegisterCursorWindow()
        {
            if (_cursorWindow == null || MenuWindow.AllActiveWindows.Contains(_cursorWindow))
            {
                return;
            }

            MenuWindow.AllActiveWindows.Add(_cursorWindow);
            if (GUIManager.instance != null)
            {
                GUIManager.instance.UpdateWindowStatus();
            }
        }

        private void UnregisterCursorWindow()
        {
            if (_cursorWindow != null && MenuWindow.AllActiveWindows.Remove(_cursorWindow) && GUIManager.instance != null)
            {
                GUIManager.instance.UpdateWindowStatus();
            }
        }

        private void CycleLanguage()
        {
            _workingLanguage = (ThingNameLanguage)(((int)_workingLanguage + 1) % 3);
            RebuildRows();
        }

        private void CycleCategory()
        {
            List<string> categories = new List<string> { "All" };
            categories.AddRange(_catalog.Select(item => item.Category).Distinct().OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
            int index = categories.IndexOf(_workingCategory);
            _workingCategory = categories[(index + 1) % categories.Count];
            RebuildRows();
        }

        private void SelectVisible()
        {
            foreach (ThingTargetDefinition definition in _visibleDefinitions)
            {
                definition.SetSelected(_workingSelection, _workingLuggageTypes, _workingSceneTargetTypes, true);
            }
            RebuildRows();
        }

        private void ClearVisible()
        {
            foreach (ThingTargetDefinition definition in _visibleDefinitions)
            {
                definition.SetSelected(_workingSelection, _workingLuggageTypes, _workingSceneTargetTypes, false);
            }
            RebuildRows();
        }

        private void ToggleScope(ThingLocationScope scope, bool value)
        {
            if (_isRebuilding)
            {
                return;
            }
            if (value) _workingScopes |= scope;
            else _workingScopes &= ~scope;
        }

        private void RebuildRows()
        {
            if (!_isOpen || !_root.activeSelf)
            {
                return;
            }

            _isRebuilding = true;
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);
            }
            _visibleDefinitions.Clear();

            string search = _searchInput == null ? string.Empty : (_searchInput.text ?? string.Empty).Trim();
            List<ThingTargetDefinition> visible = _catalog
                .Where(item => _workingCategory == "All" || item.Category == _workingCategory)
                .Where(item => !_selectedOnly || item.IsSelected(_workingSelection, _workingLuggageTypes, _workingSceneTargetTypes))
                .Where(item => string.IsNullOrEmpty(search) || item.GetSearchText(_workingLanguage).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            foreach (IGrouping<string, ThingTargetDefinition> group in visible.GroupBy(item => item.Category))
            {
                List<ThingTargetDefinition> definitions = group
                    .OrderBy(item => item.GetDisplayName(_workingLanguage), StringComparer.OrdinalIgnoreCase)
                    .ToList();
                AddCategorySection(group.Key, definitions);
                _visibleDefinitions.AddRange(definitions);
            }

            _emptyText.gameObject.SetActive(visible.Count == 0);
            _languageButtonText.text = "Language: " + GetLanguageDisplay(_workingLanguage);
            _categoryButtonText.text = "Category: " + ThingCatalog.GetCategoryDisplay(_workingCategory, _workingLanguage);
            _countText.text = string.Format("{0} selected / 已选 {1}", CountSelected(), _catalog.Count);
            _lastListWidth = _viewport.rect.width;
            _isRebuilding = false;
        }

        private void AddCategorySection(string category, List<ThingTargetDefinition> definitions)
        {
            GameObject section = CreateRect("CategorySection_" + category, _content);
            VerticalLayoutGroup sectionLayout = section.AddComponent<VerticalLayoutGroup>();
            sectionLayout.spacing = 4f;
            sectionLayout.childControlWidth = true;
            sectionLayout.childControlHeight = true;
            sectionLayout.childForceExpandWidth = true;
            sectionLayout.childForceExpandHeight = false;
            ContentSizeFitter sectionFitter = section.AddComponent<ContentSizeFitter>();
            sectionFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sectionFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject header = CreateRect("Header", section.transform);
            LayoutElement headerLayout = header.AddComponent<LayoutElement>();
            headerLayout.minHeight = 28f;
            headerLayout.preferredHeight = 28f;
            Image headerImage = header.AddComponent<Image>();
            headerImage.color = new Color(0.2f, 0.19f, 0.15f, 0.9f);
            TextMeshProUGUI headerText = CreateText(header.transform, "Text", ThingCatalog.GetCategoryDisplay(category, _workingLanguage), 15f, TextAlignmentOptions.Left,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-8f, 0f));
            headerText.fontStyle = FontStyles.Bold;

            GameObject gridObject = CreateRect("Grid", section.transform);
            LayoutElement gridLayoutElement = gridObject.AddComponent<LayoutElement>();
            gridLayoutElement.flexibleWidth = 1f;
            GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.spacing = new Vector2(CellSpacing, CellSpacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;

            float width = Mathf.Max(320f, _viewport.rect.width - 16f);
            int columnCount = Mathf.Clamp(Mathf.FloorToInt((width + CellSpacing) / (MinCellWidth + CellSpacing)), 1, 6);
            float cellWidth = Mathf.Max(160f, (width - CellSpacing * (columnCount - 1)) / columnCount);
            grid.constraintCount = columnCount;
            grid.cellSize = new Vector2(cellWidth, CellHeight);
            int rowCount = Mathf.CeilToInt(definitions.Count / (float)columnCount);
            gridLayoutElement.minHeight = rowCount * CellHeight + Mathf.Max(0, rowCount - 1) * CellSpacing;
            gridLayoutElement.preferredHeight = gridLayoutElement.minHeight;

            foreach (ThingTargetDefinition definition in definitions)
            {
                AddItemCell(gridObject.transform, definition);
            }
        }

        private void AddItemCell(Transform parent, ThingTargetDefinition definition)
        {
            GameObject cell = CreateRect("ItemCell_" + definition.ItemId, parent);
            Image cellImage = cell.AddComponent<Image>();
            cellImage.color = new Color(0.12f, 0.13f, 0.12f, 0.9f);

            GameObject toggleObject = CreateRect("Toggle", cell.transform);
            RectTransform toggleRect = toggleObject.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0f, 0.5f);
            toggleRect.anchorMax = new Vector2(0f, 0.5f);
            toggleRect.sizeDelta = new Vector2(26f, 26f);
            toggleRect.anchoredPosition = new Vector2(18f, 0f);
            Image background = toggleObject.AddComponent<Image>();
            background.color = new Color(0.03f, 0.03f, 0.03f, 1f);
            GameObject checkObject = CreateRect("Checkmark", toggleObject.transform);
            Stretch(checkObject.GetComponent<RectTransform>(), 5f, 5f, 5f, 5f);
            Image check = checkObject.AddComponent<Image>();
            check.color = new Color(0.85f, 0.72f, 0.28f, 1f);
            Toggle toggle = toggleObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = check;
            toggle.isOn = definition.IsSelected(_workingSelection, _workingLuggageTypes, _workingSceneTargetTypes);
            toggle.onValueChanged.AddListener(delegate(bool value)
            {
                if (_isRebuilding) return;
                definition.SetSelected(_workingSelection, _workingLuggageTypes, _workingSceneTargetTypes, value);
                if (_selectedOnly)
                {
                    RebuildRows();
                    return;
                }
                _countText.text = string.Format("{0} selected / 已选 {1}", CountSelected(), _catalog.Count);
            });

            string label = definition.GetDisplayName(_workingLanguage) + definition.GetVariantSuffix(_workingLanguage);
            TextMeshProUGUI text = CreateText(cell.transform, "Name", label, 14f, TextAlignmentOptions.Left,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(56f, 2f), new Vector2(-8f, -2f));
            text.color = new Color(0.9f, 0.89f, 0.82f, 1f);
        }

        private int CountSelected()
        {
            return _catalog.Count(definition => definition.IsSelected(_workingSelection, _workingLuggageTypes, _workingSceneTargetTypes));
        }

        private TMP_InputField CreateSearch(Transform parent)
        {
            GameObject inputObject = CreateRect("Search", parent);
            RectTransform rect = inputObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(340f, 42f);
            rect.anchoredPosition = new Vector2(-202f, -117f);
            Image image = inputObject.AddComponent<Image>();
            image.color = new Color(0.03f, 0.035f, 0.03f, 1f);

            TextMeshProUGUI text = CreateText(inputObject.transform, "Text", string.Empty, 15f, TextAlignmentOptions.Left,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-12f, 0f));
            TextMeshProUGUI placeholder = CreateText(inputObject.transform, "Placeholder", "Search / 搜索", 15f, TextAlignmentOptions.Left,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-12f, 0f));
            placeholder.color = new Color(0.55f, 0.56f, 0.52f, 1f);
            TMP_InputField input = inputObject.AddComponent<TMP_InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 80;
            return input;
        }

        private Toggle CreateScopeToggle(Transform parent, string label, ThingLocationScope scope, float x)
        {
            GameObject toggleObject = CreateRect("Scope_" + scope, parent);
            RectTransform toggleRect = toggleObject.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0f, 1f);
            toggleRect.anchorMax = new Vector2(0f, 1f);
            toggleRect.sizeDelta = new Vector2(180f, 30f);
            toggleRect.anchoredPosition = new Vector2(x + 90f, -178f);
            Image background = toggleObject.AddComponent<Image>();
            background.color = new Color(0.03f, 0.03f, 0.03f, 1f);
            GameObject checkObject = CreateRect("Checkmark", toggleObject.transform);
            RectTransform checkRect = checkObject.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0f, 0.5f);
            checkRect.anchorMax = new Vector2(0f, 0.5f);
            checkRect.sizeDelta = new Vector2(18f, 18f);
            checkRect.anchoredPosition = new Vector2(6f, 0f);
            Image check = checkObject.AddComponent<Image>();
            check.color = new Color(0.85f, 0.72f, 0.28f, 1f);
            Toggle toggle = toggleObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = check;
            toggle.isOn = (_workingScopes & scope) != 0;
            toggle.onValueChanged.AddListener(delegate(bool value) { ToggleScope(scope, value); });
            _scopeToggles[scope] = toggle;
            CreateText(toggleObject.transform, "Label", label, 13f, TextAlignmentOptions.Left,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(32f, 0f), new Vector2(-4f, 0f));
            return toggle;
        }

        private Toggle CreateFilterToggle(Transform parent, string label, Vector2 position, Vector2 size)
        {
            GameObject toggleObject = CreateRect("SelectedOnly", parent);
            RectTransform toggleRect = toggleObject.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0f, 1f);
            toggleRect.anchorMax = new Vector2(0f, 1f);
            toggleRect.sizeDelta = size;
            toggleRect.anchoredPosition = position + new Vector2(size.x * 0.5f, -size.y * 0.5f);
            Image background = toggleObject.AddComponent<Image>();
            background.color = new Color(0.2f, 0.2f, 0.18f, 1f);

            GameObject checkObject = CreateRect("Checkmark", toggleObject.transform);
            RectTransform checkRect = checkObject.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0f, 0.5f);
            checkRect.anchorMax = new Vector2(0f, 0.5f);
            checkRect.sizeDelta = new Vector2(22f, 22f);
            checkRect.anchoredPosition = new Vector2(18f, 0f);
            Image check = checkObject.AddComponent<Image>();
            check.color = new Color(0.85f, 0.72f, 0.28f, 1f);

            Toggle toggle = toggleObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = check;
            toggle.isOn = false;
            toggle.onValueChanged.AddListener(delegate(bool value)
            {
                if (_isRebuilding)
                {
                    return;
                }
                _selectedOnly = value;
                RebuildRows();
            });
            CreateText(toggleObject.transform, "Label", label, 13f, TextAlignmentOptions.Left,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(38f, 0f), new Vector2(-6f, 0f));
            return toggle;
        }

        private TextMeshProUGUI CreateButton(Transform parent, string name, Vector2 position, Vector2 size, string text, UnityAction action)
        {
            GameObject buttonObject = CreateRect(name, parent);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position + new Vector2(size.x * 0.5f, -size.y * 0.5f);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.18f, 1f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            return CreateText(buttonObject.transform, "Text", text, 14f, TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        }

        private Button CreateActionButton(Transform parent, string name, string text, Vector2 position, Vector2 size, UnityAction action)
        {
            GameObject buttonObject = CreateRect(name, parent);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(position.x < 0f ? 1f : 0f, 0f);
            rect.anchorMax = new Vector2(position.x < 0f ? 1f : 0f, 0f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(position.x + (position.x < 0f ? -size.x * 0.5f : size.x * 0.5f), position.y + size.y * 0.5f);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.18f, 1f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            CreateText(buttonObject.transform, "Text", text, 14f, TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            return button;
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, string value, float size, TextAlignmentOptions alignment,
            Vector2 anchorMin, Vector2 pivot, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject textObject = CreateRect(name, parent);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.fontSize = Mathf.Clamp(size, 8f, 24f);
            text.alignment = alignment;
            text.text = value;
            text.color = new Color(0.9f, 0.88f, 0.8f, 1f);
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            GameObject value = new GameObject(name, typeof(RectTransform));
            value.transform.SetParent(parent, false);
            value.layer = 5;
            return value;
        }

        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static string GetLanguageDisplay(ThingNameLanguage language)
        {
            switch (language)
            {
                case ThingNameLanguage.English: return "English";
                case ThingNameLanguage.SimplifiedChinese: return "简体中文";
                default: return "Game / 游戏";
            }
        }
    }
}
