using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WhereIsThing
{
    internal sealed class ThingPresetPickerWindow
    {
        private readonly Canvas _canvas;
        private readonly TMP_FontAsset _font;
        private readonly GameObject _root;
        private readonly GameObject _cursorWindowObject;
        private readonly MenuWindow _cursorWindow;
        private readonly RectTransform _panelRect;
        private readonly RectTransform _content;
        private readonly TextMeshProUGUI _titleText;
        private readonly TextMeshProUGUI _subtitleText;
        private readonly TextMeshProUGUI _shareModeText;
        private readonly Button _shareModeButton;
        private readonly Button _newButton;
        private readonly TextMeshProUGUI _settingsTitleText;
        private readonly TextMeshProUGUI _scanModeText;
        private readonly TextMeshProUGUI _durationText;
        private readonly Button _durationMinusButton;
        private readonly Button _durationPlusButton;
        private readonly Toggle _ownerNamesToggle;
        private readonly Dictionary<ThingLocationScope, Toggle> _scopeToggles = new Dictionary<ThingLocationScope, Toggle>();
        private readonly List<ThingPresetDefinition> _presets = new List<ThingPresetDefinition>();
        private GameObject _renameOverlay;
        private TMP_InputField _renameInput;
        private TextMeshProUGUI _renameTitleText;
        private TextMeshProUGUI _renameErrorText;
        private Func<ThingPresetDefinition, string> _summaryProvider;
        private Action<string> _usePreset;
        private Action<string> _editPreset;
        private Action<string, string> _renamePreset;
        private Action<string> _togglePublished;
        private Action<string> _deletePreset;
        private Action _createPreset;
        private Action<ThingPresetShareMode> _setShareMode;
        private Action<ThingLocationScope, bool> _setScope;
        private Action _cycleScanMode;
        private Action<int> _adjustDisplayDuration;
        private Action<bool> _setShowOwnerNames;
        private bool _usingFallbackPresets;
        private bool _allowEditing;
        private bool _isOpen;
        private bool _isRebuilding;
        private bool _previousCursorVisible;
        private CursorLockMode _previousCursorLockState;
        private ThingPresetShareMode _shareMode;
        private ThingLocationScope _scopes;
        private ThingScanMode _scanMode;
        private float _displayDuration;
        private bool _showOwnerNames;
        private string _activePresetId;
        private string _renamingPresetId;

        public ThingPresetPickerWindow(Canvas canvas, TMP_FontAsset font)
        {
            _canvas = canvas;
            _font = font;

            _root = CreateRect("WhereIsThingPresetPickerOverlay", canvas.transform);
            Stretch(_root.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            Image overlay = _root.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.54f);
            overlay.raycastTarget = true;

            _cursorWindowObject = CreateRect("WhereIsThingPresetMenuWindow", _root.transform);
            _cursorWindowObject.SetActive(false);
            _cursorWindow = _cursorWindowObject.AddComponent<MenuWindow>();

            GameObject panel = CreateRect("PresetPickerPanel", _root.transform);
            _panelRect = panel.GetComponent<RectTransform>();
            _panelRect.anchorMin = new Vector2(0.2f, 0.14f);
            _panelRect.anchorMax = new Vector2(0.8f, 0.86f);
            _panelRect.offsetMin = Vector2.zero;
            _panelRect.offsetMax = Vector2.zero;
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.075f, 0.08f, 0.075f, 0.98f);
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.7f, 0.66f, 0.52f, 0.75f);
            outline.effectDistance = new Vector2(2f, 2f);

            _titleText = CreateText(panel.transform, "Title", string.Empty, 22f, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -42f), new Vector2(-22f, -8f), false, true);
            _subtitleText = CreateText(panel.transform, "Subtitle", string.Empty, 12f, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -72f), new Vector2(-22f, -40f), true, true);
            _shareModeText = CreateButton(panel.transform, "ShareMode", new Vector2(22f, -88f), new Vector2(232f, 38f), string.Empty, CycleShareModeInternal);
            _shareModeButton = _shareModeText.GetComponentInParent<Button>();
            _newButton = CreateActionButton(panel.transform, "New", ThingUi.NewPreset(), new Vector2(264f, -88f), new Vector2(150f, 38f), CreatePresetInternal, false);
            CreateActionButton(panel.transform, "Close", ThingUi.Close(), new Vector2(-22f, 18f), new Vector2(140f, 38f), Close, true);

            GameObject viewportObject = CreateRect("Viewport", panel.transform);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.offsetMin = new Vector2(22f, 142f);
            viewport.offsetMax = new Vector2(-22f, -132f);
            Image viewportImage = viewportObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.18f);
            Mask mask = viewportObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject scrollObject = CreateRect("Scroll", viewportObject.transform);
            Stretch(scrollObject.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 72f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = viewport;
            _content = CreateRect("Content", scrollObject.transform).GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = Vector2.zero;
            scroll.content = _content;
            VerticalLayoutGroup layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(10, 10, 6, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _settingsTitleText = CreateText(panel.transform, "SettingsTitle", ThingUi.LocalScanSettings(), 13f, TextAlignmentOptions.Left,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(22f, 98f), new Vector2(-22f, 122f), false, true);
            _scanModeText = CreateButton(panel.transform, "ScanMode", new Vector2(22f, 52f), new Vector2(196f, 36f), string.Empty, CycleScanModeInternal);
            _durationMinusButton = CreateActionButton(panel.transform, "DurationMinus", ThingUi.TimeMinus(), new Vector2(228f, 52f), new Vector2(36f, 36f), delegate { AdjustDurationInternal(-1); }, false);
            _durationText = CreateText(panel.transform, "DurationText", string.Empty, 13f, TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(270f, 52f), new Vector2(350f, 88f), false, true);
            _durationPlusButton = CreateActionButton(panel.transform, "DurationPlus", ThingUi.TimePlus(), new Vector2(358f, 52f), new Vector2(36f, 36f), delegate { AdjustDurationInternal(1); }, false);

            CreateText(panel.transform, "ScopeTitle", ThingUi.ScopeTitle(), 13f, TextAlignmentOptions.Left,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(22f, 18f), new Vector2(128f, 44f), false, true);
            GameObject scopeOptions = CreateRect("ScopeOptions", panel.transform);
            RectTransform scopeOptionsRect = scopeOptions.GetComponent<RectTransform>();
            scopeOptionsRect.anchorMin = new Vector2(0f, 0f);
            scopeOptionsRect.anchorMax = new Vector2(1f, 0f);
            scopeOptionsRect.offsetMin = new Vector2(136f, 4f);
            scopeOptionsRect.offsetMax = new Vector2(-22f, 32f);
            CreateScopeToggle(scopeOptions.transform, ThingUi.Ground(), ThingLocationScope.Ground, 0f, 0.25f);
            CreateScopeToggle(scopeOptions.transform, ThingUi.Held(), ThingLocationScope.Held, 0.25f, 0.5f);
            CreateScopeToggle(scopeOptions.transform, ThingUi.Backpack(), ThingLocationScope.Backpack, 0.5f, 0.75f);
            CreateScopeToggle(scopeOptions.transform, ThingUi.Statue(), ThingLocationScope.Statue, 0.75f, 1f);
            _ownerNamesToggle = CreateBoolToggle(panel.transform, "OwnerNames", ThingUi.PlayerNames(),
                new Vector2(-97f, 110f), new Vector2(150f, 28f), SetOwnerNamesInternal);

            CreateRenameDialog();

            _root.SetActive(false);
        }

        public bool IsOpen { get { return _isOpen; } }

        public void Open(IEnumerable<ThingPresetDefinition> presets, string activePresetId, bool allowEditing, bool usingFallbackPresets, ThingPresetShareMode shareMode,
            ThingLocationScope scopes, ThingScanMode scanMode, float displayDurationSeconds, bool showOwnerNames,
            Func<ThingPresetDefinition, string> summaryProvider, Action<string> usePreset, Action<string> editPreset,
            Action<string, string> renamePreset, Action<string> togglePublished, Action<string> deletePreset, Action createPreset,
            Action<ThingPresetShareMode> setShareMode,
            Action<ThingLocationScope, bool> setScope, Action cycleScanMode, Action<int> adjustDisplayDuration,
            Action<bool> setShowOwnerNames)
        {
            _presets.Clear();
            _presets.AddRange((presets ?? Enumerable.Empty<ThingPresetDefinition>()).Where(preset => preset != null).Select(preset => preset.Clone()).ToList());
            _activePresetId = activePresetId ?? string.Empty;
            _allowEditing = allowEditing;
            _usingFallbackPresets = usingFallbackPresets;
            _shareMode = shareMode;
            _scopes = scopes;
            _scanMode = scanMode;
            _displayDuration = Mathf.Clamp(displayDurationSeconds, 2f, 60f);
            _showOwnerNames = showOwnerNames;
            _summaryProvider = summaryProvider;
            _usePreset = usePreset;
            _editPreset = editPreset;
            _renamePreset = renamePreset;
            _togglePublished = togglePublished;
            _deletePreset = deletePreset;
            _createPreset = createPreset;
            _setShareMode = setShareMode;
            _setScope = setScope;
            _cycleScanMode = cycleScanMode;
            _adjustDisplayDuration = adjustDisplayDuration;
            _setShowOwnerNames = setShowOwnerNames;
            _isOpen = true;
            _previousCursorVisible = Cursor.visible;
            _previousCursorLockState = Cursor.lockState;
            _root.SetActive(true);
            CloseRenameDialog();
            RegisterCursorWindow();
            MaintainCursor();
            RebuildRows();
            RebuildSettings();
        }

        public void Tick()
        {
            if (_isOpen)
            {
                MaintainCursor();
                if (_renameOverlay != null && _renameOverlay.activeSelf && Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseRenameDialog();
                }
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

            RestoreCursor();
            _isOpen = false;
            CloseRenameDialog();
            _root.SetActive(false);
            UnregisterCursorWindow();
        }

        public void Dispose()
        {
            if (_isOpen)
            {
                RestoreCursor();
            }
            _isOpen = false;
            UnregisterCursorWindow();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }
        }

        private void RebuildRows()
        {
            foreach (Transform child in _content)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }

            _panelRect.anchorMin = _allowEditing ? new Vector2(0.16f, 0.12f) : new Vector2(0.23f, 0.16f);
            _panelRect.anchorMax = _allowEditing ? new Vector2(0.84f, 0.88f) : new Vector2(0.77f, 0.84f);

            _titleText.text = ThingUi.PresetWindowTitle(_allowEditing);
            _subtitleText.text = ThingUi.PresetWindowSubtitle(_allowEditing, _usingFallbackPresets);
            _shareModeButton.gameObject.SetActive(_allowEditing);
            _newButton.gameObject.SetActive(_allowEditing);
            _shareModeText.text = ThingUi.ShareModeLabel(_shareMode);

            foreach (ThingPresetDefinition preset in _presets)
            {
                AddPresetRow(preset);
            }
            if (_presets.Count == 0)
            {
                CreateText(_content, "Empty", ThingUi.EmptyPresets(), 14f, TextAlignmentOptions.Center,
                    new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-12f, -36f), false, true);
            }
        }

        private void RebuildSettings()
        {
            _isRebuilding = true;
            _settingsTitleText.text = ThingUi.LocalScanSettings();
            _scanModeText.text = ThingUi.ScanModeLabel(_scanMode, _displayDuration);
            _durationText.text = string.Format("{0:0}s", _displayDuration);
            bool timed = _scanMode == ThingScanMode.Timed;
            _durationText.gameObject.SetActive(timed);
            _durationMinusButton.gameObject.SetActive(timed);
            _durationPlusButton.gameObject.SetActive(timed);
            _ownerNamesToggle.isOn = _showOwnerNames;

            foreach (KeyValuePair<ThingLocationScope, Toggle> entry in _scopeToggles)
            {
                entry.Value.isOn = (_scopes & entry.Key) != 0;
            }
            _isRebuilding = false;
        }

        private void AddPresetRow(ThingPresetDefinition preset)
        {
            GameObject row = CreateRect("Preset_" + preset.Id, _content);
            LayoutElement element = row.AddComponent<LayoutElement>();
            bool active = string.Equals(preset.Id, _activePresetId, StringComparison.Ordinal);
            bool builtIn = ThingPresetFactory.IsBuiltInPresetId(preset.Id);
            element.minHeight = _allowEditing ? 82f : 70f;
            element.preferredHeight = element.minHeight;
            Image background = row.AddComponent<Image>();
            background.color = active ? new Color(0.22f, 0.22f, 0.16f, 1f) : new Color(0.12f, 0.12f, 0.1f, 1f);

            int actionCount = 1 + (_allowEditing ? 1 : 0) + (_allowEditing && !builtIn ? 3 : 0);
            float actionWidth = 80f + (_allowEditing ? 60f : 0f) + (_allowEditing && !builtIn ? 188f : 0f) +
                Mathf.Max(0, actionCount - 1) * 4f;

            GameObject info = CreateRect("Info", row.transform);
            RectTransform infoRect = info.GetComponent<RectTransform>();
            infoRect.anchorMin = Vector2.zero;
            infoRect.anchorMax = Vector2.one;
            infoRect.offsetMin = new Vector2(24f, 8f);
            infoRect.offsetMax = new Vector2(-(actionWidth + 32f), -8f);
            CreateText(info.transform, "Name", (active ? "● " : string.Empty) + ThingPresetFactory.GetDisplayName(preset), 15f, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -28f), Vector2.zero, false, true);
            CreateText(info.transform, "Summary", _summaryProvider == null ? string.Empty : _summaryProvider(preset), 12f, TextAlignmentOptions.Left,
                Vector2.zero, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -30f), true, true);

            GameObject actions = CreateRect("Actions", row.transform);
            RectTransform actionsRect = actions.GetComponent<RectTransform>();
            actionsRect.anchorMin = new Vector2(1f, 0.5f);
            actionsRect.anchorMax = new Vector2(1f, 0.5f);
            actionsRect.pivot = new Vector2(1f, 0.5f);
            actionsRect.sizeDelta = new Vector2(actionWidth, 32f);
            actionsRect.anchoredPosition = new Vector2(-18f, 0f);
            HorizontalLayoutGroup actionLayout = actions.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 4f;
            actionLayout.childControlWidth = true;
            actionLayout.childControlHeight = true;
            actionLayout.childForceExpandWidth = false;
            actionLayout.childForceExpandHeight = false;
            actionLayout.childAlignment = TextAnchor.MiddleRight;
            CreateLayoutButton(actions.transform, "Use", ThingUi.Use(active), 80f, delegate
            {
                if (_usePreset != null)
                {
                    _usePreset(preset.Id);
                }
            });

            if (_allowEditing)
            {
                CreateLayoutButton(actions.transform, "Edit", ThingUi.Edit(), 60f, delegate
                {
                    if (_editPreset != null)
                    {
                        _editPreset(preset.Id);
                    }
                });

                if (!builtIn)
                {
                    CreateLayoutButton(actions.transform, "Rename", ThingUi.Rename(), 68f,
                        delegate { OpenRenameDialog(preset); });
                    CreateLayoutButton(actions.transform, "Publish", preset.Published ? ThingUi.Hide() : ThingUi.Publish(), 60f,
                        delegate { InvokeAndRefresh(_togglePublished, preset.Id); });
                    CreateLayoutButton(actions.transform, "Delete", ThingUi.Delete(), 60f,
                        delegate { InvokeAndRefresh(_deletePreset, preset.Id); });
                }
            }
        }

        private void CreateRenameDialog()
        {
            _renameOverlay = CreateRect("RenameOverlay", _root.transform);
            Stretch(_renameOverlay.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            Image overlayImage = _renameOverlay.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.68f);
            overlayImage.raycastTarget = true;

            GameObject panel = CreateRect("RenamePanel", _renameOverlay.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.32f, 0.34f);
            panelRect.anchorMax = new Vector2(0.68f, 0.66f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.075f, 0.08f, 0.075f, 1f);
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.7f, 0.66f, 0.52f, 0.75f);
            outline.effectDistance = new Vector2(2f, 2f);

            _renameTitleText = CreateText(panel.transform, "Title", ThingUi.RenamePresetTitle(), 20f, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -48f), new Vector2(-22f, -10f), false, true);

            GameObject inputObject = CreateRect("NameInput", panel.transform);
            RectTransform inputRect = inputObject.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0f, 1f);
            inputRect.anchorMax = new Vector2(1f, 1f);
            inputRect.pivot = new Vector2(0.5f, 1f);
            inputRect.sizeDelta = new Vector2(-44f, 42f);
            inputRect.anchoredPosition = new Vector2(0f, -62f);
            Image inputImage = inputObject.AddComponent<Image>();
            inputImage.color = new Color(0.03f, 0.035f, 0.03f, 1f);
            TextMeshProUGUI inputText = CreateText(inputObject.transform, "Text", string.Empty, 16f, TextAlignmentOptions.Left,
                Vector2.zero, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f), false, true);
            TextMeshProUGUI placeholder = CreateText(inputObject.transform, "Placeholder", ThingUi.PresetNamePlaceholder(), 16f, TextAlignmentOptions.Left,
                Vector2.zero, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f), false, true);
            placeholder.color = new Color(0.55f, 0.56f, 0.52f, 1f);
            _renameInput = inputObject.AddComponent<TMP_InputField>();
            _renameInput.textComponent = inputText;
            _renameInput.placeholder = placeholder;
            _renameInput.lineType = TMP_InputField.LineType.SingleLine;
            _renameInput.characterLimit = 24;
            _renameInput.onSubmit.AddListener(delegate { ConfirmRename(); });

            _renameErrorText = CreateText(panel.transform, "Error", string.Empty, 12f, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -130f), new Vector2(-22f, -106f), false, true);
            _renameErrorText.color = new Color(0.95f, 0.48f, 0.4f, 1f);

            CreateActionButton(panel.transform, "Cancel", ThingUi.Cancel(), new Vector2(-152f, 18f), new Vector2(120f, 36f), CloseRenameDialog, true);
            CreateActionButton(panel.transform, "Save", ThingUi.Save(), new Vector2(-22f, 18f), new Vector2(120f, 36f), ConfirmRename, true);
            _renameOverlay.SetActive(false);
        }

        private void OpenRenameDialog(ThingPresetDefinition preset)
        {
            if (!_allowEditing || preset == null || ThingPresetFactory.IsBuiltInPresetId(preset.Id) || _renamePreset == null)
            {
                return;
            }

            _renamingPresetId = preset.Id;
            _renameTitleText.text = ThingUi.RenamePresetTitle();
            _renameInput.text = preset.Name ?? string.Empty;
            _renameErrorText.text = string.Empty;
            _renameOverlay.SetActive(true);
            _renameOverlay.transform.SetAsLastSibling();
            _renameInput.ActivateInputField();
            _renameInput.MoveTextEnd(false);
        }

        private void ConfirmRename()
        {
            if (_renameOverlay == null || !_renameOverlay.activeSelf)
            {
                return;
            }

            string value = (_renameInput.text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                _renameErrorText.text = ThingUi.PresetNameRequired();
                _renameInput.ActivateInputField();
                return;
            }

            string presetId = _renamingPresetId;
            CloseRenameDialog();
            if (_renamePreset != null)
            {
                _renamePreset(presetId, value);
            }
        }

        private void CloseRenameDialog()
        {
            _renamingPresetId = string.Empty;
            if (_renameErrorText != null)
            {
                _renameErrorText.text = string.Empty;
            }
            if (_renameOverlay != null)
            {
                _renameOverlay.SetActive(false);
            }
        }

        private void InvokeAndRefresh(Action<string> action, string presetId)
        {
            if (action != null)
            {
                action(presetId);
            }
        }

        private void CreatePresetInternal()
        {
            if (_createPreset != null)
            {
                _createPreset();
            }
        }

        private void CycleShareModeInternal()
        {
            if (!_allowEditing || _setShareMode == null)
            {
                return;
            }

            ThingPresetShareMode next = _shareMode == ThingPresetShareMode.Off
                ? ThingPresetShareMode.BuiltInOnly
                : (_shareMode == ThingPresetShareMode.BuiltInOnly ? ThingPresetShareMode.PublishedPresets : ThingPresetShareMode.Off);
            _setShareMode(next);
        }

        private void CycleScanModeInternal()
        {
            if (_cycleScanMode != null)
            {
                _cycleScanMode();
            }
        }

        private void AdjustDurationInternal(int delta)
        {
            if (_adjustDisplayDuration != null)
            {
                _adjustDisplayDuration(delta);
            }
        }

        private void SetScopeInternal(ThingLocationScope scope, bool value)
        {
            if (_isRebuilding)
            {
                return;
            }

            if (_setScope != null)
            {
                _setScope(scope, value);
            }
        }

        private void SetOwnerNamesInternal(bool value)
        {
            if (_isRebuilding)
            {
                return;
            }

            if (_setShowOwnerNames != null)
            {
                _setShowOwnerNames(value);
            }
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

        private void RestoreCursor()
        {
            Cursor.visible = _previousCursorVisible;
            Cursor.lockState = _previousCursorLockState;
        }

        private Toggle CreateScopeToggle(Transform parent, string label, ThingLocationScope scope, float anchorMinX, float anchorMaxX)
        {
            GameObject toggleObject = CreateRect("Scope_" + scope, parent);
            RectTransform toggleRect = toggleObject.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(anchorMinX, 0f);
            toggleRect.anchorMax = new Vector2(anchorMaxX, 1f);
            toggleRect.sizeDelta = new Vector2(-8f, 0f);
            toggleRect.anchoredPosition = Vector2.zero;
            Image background = toggleObject.AddComponent<Image>();
            background.color = new Color(0.2f, 0.2f, 0.18f, 1f);

            GameObject checkObject = CreateRect("Checkmark", toggleObject.transform);
            RectTransform checkRect = checkObject.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0f, 0.5f);
            checkRect.anchorMax = new Vector2(0f, 0.5f);
            checkRect.sizeDelta = new Vector2(18f, 18f);
            checkRect.anchoredPosition = new Vector2(12f, 0f);
            Image check = checkObject.AddComponent<Image>();
            check.color = new Color(0.85f, 0.72f, 0.28f, 1f);

            Toggle toggle = toggleObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = check;
            toggle.onValueChanged.AddListener(delegate(bool value) { SetScopeInternal(scope, value); });
            CreateText(toggleObject.transform, "Label", label, 12f, TextAlignmentOptions.Left,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(30f, 0f), new Vector2(-4f, 0f), false, true);
            _scopeToggles[scope] = toggle;
            return toggle;
        }

        private Toggle CreateBoolToggle(Transform parent, string name, string label, Vector2 position, Vector2 size, Action<bool> onValueChanged)
        {
            GameObject toggleObject = CreateRect(name, parent);
            RectTransform toggleRect = toggleObject.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(1f, 0f);
            toggleRect.anchorMax = new Vector2(1f, 0f);
            toggleRect.sizeDelta = size;
            toggleRect.anchoredPosition = position;
            Image background = toggleObject.AddComponent<Image>();
            background.color = new Color(0.2f, 0.2f, 0.18f, 1f);

            GameObject checkObject = CreateRect("Checkmark", toggleObject.transform);
            RectTransform checkRect = checkObject.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0f, 0.5f);
            checkRect.anchorMax = new Vector2(0f, 0.5f);
            checkRect.sizeDelta = new Vector2(18f, 18f);
            checkRect.anchoredPosition = new Vector2(12f, 0f);
            Image check = checkObject.AddComponent<Image>();
            check.color = new Color(0.85f, 0.72f, 0.28f, 1f);

            Toggle toggle = toggleObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = check;
            if (onValueChanged != null)
            {
                toggle.onValueChanged.AddListener(delegate(bool value) { onValueChanged(value); });
            }
            CreateText(toggleObject.transform, "Label", label, 12f, TextAlignmentOptions.Left,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(30f, 0f), new Vector2(-4f, 0f), false, true);
            return toggle;
        }

        private TextMeshProUGUI CreateRowButton(Transform parent, string name, string label, float right, UnityAction action, float width)
        {
            return CreateButton(parent, name, new Vector2(right - width, -16f), new Vector2(width, 28f), label, action, true);
        }

        private TextMeshProUGUI CreateLayoutText(Transform parent, string name, string value, float size, float preferredHeight, bool wrap,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            GameObject textObject = CreateRect(name, parent);
            LayoutElement element = textObject.AddComponent<LayoutElement>();
            element.minHeight = preferredHeight;
            element.preferredHeight = preferredHeight;
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.fontSize = Mathf.Clamp(size, 8f, 24f);
            text.alignment = alignment;
            text.text = value;
            text.color = new Color(0.9f, 0.88f, 0.8f, 1f);
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(9f, size - 4f);
            text.fontSizeMax = Mathf.Clamp(size, 9f, 24f);
            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = wrap ? TextOverflowModes.Truncate : TextOverflowModes.Ellipsis;
            text.margin = new Vector4(2f, 0f, 2f, 0f);
            return text;
        }

        private TextMeshProUGUI CreateLayoutButton(Transform parent, string name, string label, float preferredWidth, UnityAction action)
        {
            GameObject buttonObject = CreateRect(name, parent);
            LayoutElement element = buttonObject.AddComponent<LayoutElement>();
            element.minWidth = preferredWidth;
            element.preferredWidth = preferredWidth;
            element.minHeight = 28f;
            element.preferredHeight = 28f;
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.18f, 1f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            return CreateText(buttonObject.transform, "Text", label, 11f, TextAlignmentOptions.Center,
                Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.one, Vector2.zero, Vector2.zero, false, true);
        }

        private TextMeshProUGUI CreateButton(Transform parent, string name, Vector2 position, Vector2 size, string text, UnityAction action, bool anchorRight = false)
        {
            GameObject buttonObject = CreateRect(name, parent);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            if (anchorRight)
            {
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.sizeDelta = size;
                rect.anchoredPosition = new Vector2(position.x + size.x, position.y - size.y * 0.5f);
            }
            else
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.sizeDelta = size;
                rect.anchoredPosition = position + new Vector2(size.x * 0.5f, -size.y * 0.5f);
            }
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.18f, 1f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            return CreateText(buttonObject.transform, "Text", text, 12f, TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, false, true);
        }

        private Button CreateActionButton(Transform parent, string name, string text, Vector2 position, Vector2 size, UnityAction action, bool anchorRight)
        {
            GameObject buttonObject = CreateRect(name, parent);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(anchorRight ? 1f : 0f, anchorRight ? 0f : 1f);
            rect.anchorMax = new Vector2(anchorRight ? 1f : 0f, anchorRight ? 0f : 1f);
            rect.sizeDelta = size;
            if (anchorRight)
            {
                rect.anchoredPosition = new Vector2(position.x - size.x * 0.5f, position.y + size.y * 0.5f);
            }
            else
            {
                rect.anchoredPosition = position + new Vector2(size.x * 0.5f, -size.y * 0.5f);
            }
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.18f, 1f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            CreateText(buttonObject.transform, "Text", text, 13f, TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, false, true);
            return button;
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, string value, float size, TextAlignmentOptions alignment,
            Vector2 anchorMin, Vector2 pivot, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, bool wrap, bool autoSize)
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
            text.enableAutoSizing = autoSize;
            if (autoSize)
            {
                text.fontSizeMin = Mathf.Max(9f, size - 4f);
                text.fontSizeMax = Mathf.Clamp(size, 9f, 24f);
            }
            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = wrap ? TextOverflowModes.Truncate : TextOverflowModes.Ellipsis;
            text.margin = new Vector4(4f, 0f, 4f, 0f);
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
    }
}
