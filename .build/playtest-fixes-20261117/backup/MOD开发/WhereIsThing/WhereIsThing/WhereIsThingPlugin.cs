using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zorro.Core;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

namespace WhereIsThing
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.github.PEAKModding.PEAKLib.ModConfig", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed partial class WhereIsThingPlugin : BaseUnityPlugin, IInRoomCallbacks
    {
        public const string PluginGuid = "com.wuyachiyu.WhereIsThing";
        public const string PluginName = "WhereIsThing";
        public const string PluginVersion = "0.1.3";
        private const int PresetSchemaVersion = 6;
        private const int ShareProtocolVersion = 2;
        private const string ShareModePropertyKey = "WIT.ShareMode";
        private const string ShareProtocolPropertyKey = "WIT.Protocol";
        private const string ShareActorPropertyKey = "WIT.Actor";
        private const string ShareHashPropertyKey = "WIT.Hash";
        private const string SharePayloadPropertyKey = "WIT.Payload";

        private readonly Dictionary<string, ThingLabel> _labels = new Dictionary<string, ThingLabel>();
        private readonly List<ThingLabel> _labelUpdateBuffer = new List<ThingLabel>();
        private readonly List<string> _labelKeyRemovalBuffer = new List<string>();
        private readonly List<int> _networkRecordRemovalBuffer = new List<int>();
        private readonly Dictionary<int, string> _ownerNamesByActor = new Dictionary<int, string>();
        private readonly Dictionary<int, NetworkObjectRecord> _networkObjects = new Dictionary<int, NetworkObjectRecord>();
        private readonly Dictionary<string, Item> _itemDefinitionsByPrefabName =
            new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<PlacedPrefabSource>> _placedSourcesByPrefabName =
            new Dictionary<string, List<PlacedPrefabSource>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<ushort, List<PlacedPrefabSource>> _throwableSourcesByItemId =
            new Dictionary<ushort, List<PlacedPrefabSource>>();
        private readonly List<ThrownItemEvidence> _thrownItemEvidence = new List<ThrownItemEvidence>(32);
        private readonly List<NetworkObjectRecord> _classificationRetries = new List<NetworkObjectRecord>();
        private readonly List<PeakSequence> _peakSequences = new List<PeakSequence>();
        private static readonly FieldInfo RopeAttachedAnchorField = typeof(Rope).GetField("attachedToAnchor",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ItemLastThrownCharacterField = typeof(Item).GetField("lastThrownCharacter",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly List<ThingTargetDefinition> _catalog = new List<ThingTargetDefinition>();
        private readonly List<ThingPresetDefinition> _localPresets = new List<ThingPresetDefinition>();
        private readonly List<ThingPresetDefinition> _sessionPresets = new List<ThingPresetDefinition>();
        private readonly List<ThingPresetDefinition> _builtInPresets = new List<ThingPresetDefinition>();
        private readonly HashSet<ushort> _selectedIds = new HashSet<ushort>();
        private readonly HashSet<ThingLuggageType> _selectedLuggageTypes = new HashSet<ThingLuggageType>();
        private readonly HashSet<ThingSceneTargetType> _selectedSceneTargetTypes = new HashSet<ThingSceneTargetType>();
        private ManualLogSource _log;
        private Canvas _canvas;
        private ThingSelectionWindow _window;
        private ThingPresetPickerWindow _pickerWindow;
        private TMP_FontAsset _font;
        private TMP_FontAsset _labelFont;
        private Material _labelTitleMaterial;
        private Material _labelDetailMaterial;
        private Camera _mainCamera;
        private float _nextCameraCheck;
        private bool _displayActive;
        private float _hideAt;
        private float _nextRefresh;
        private Harmony _harmony;
        private bool _modConfigRefreshScheduled;
        private ThingLocationScope _effectiveScopes;
        private ThingScanMode _effectiveScanMode;
        private float _effectiveDisplayDuration;
        private readonly List<string> _activeLocalPresetIds = new List<string>();
        private string PrimaryLocalPresetId => _activeLocalPresetIds.Count == 0 ? string.Empty : _activeLocalPresetIds[0];
        private bool _labelPlacedFromItemSelection;
        private string _selectedSharedPresetId = string.Empty;
        private string _editingPresetId = string.Empty;
        private string _lastPublishedSharePayload = string.Empty;
        private string _lastPublishedShareHash = string.Empty;
        private float _lastPublishAt = -10f;
        private bool _shareDirty = true;
        private int _sessionSourceActor = -1;
        private bool _sessionValid;
        private float _sessionStaleUntil = -1f;
        private ThingPresetShareMode _lastObservedShareMode;
        private ThingLabelFont _lastObservedLabelFont;
        private float _lastObservedFontSize;
        private ThingNameLanguage _lastObservedNameLanguage;
        private bool _labelFontWarningLogged;
        private NonAllocDictionary<int, PhotonView>.ValueIterator _photonViewIterator;
        private bool _photonDiscoveryActive;
        private int _photonDiscoveryGeneration;
        private int _lastPhotonViewCount = -1;
        private float _nextNetworkDiscovery;
        private float _nextNetworkCleanup;
        private int _sceneDiscoveryStep = SceneDiscoveryStepCount;
        private float _nextSceneDiscovery;
        private int _sceneGeneration;
        private int _classificationRetryCursor;

        private const int NetworkObjectsPerFrame = 128;
        private const int ClassificationRetriesPerFrame = 32;
        private const double DiscoveryBudgetMilliseconds = 0.75;
        private const double ClassificationRetryBudgetMilliseconds = 0.25;
        private const float NetworkDiscoveryInterval = 0.5f;
        private const float FullValidationInterval = 5f;
        private const int SceneDiscoveryStepCount = 20;
        private static readonly float[] ClassificationRetryDelays = { 0.05f, 0.15f, 0.30f, 0.50f, 1f, 2f };

        private sealed class NetworkObjectRecord
        {
            public int LastSeenGeneration;
            public PhotonView View;
            public Item Item;
            public bool PreferPlacedItemLabel;
            public bool NeedsLateClassification;
            public float FirstSeenAt;
            public int NextRetryIndex;
            public readonly List<PlacedTargetRecord> PlacedTargets = new List<PlacedTargetRecord>();
            public readonly HashSet<string> LabelKeys = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> DesiredLabelKeys = new HashSet<string>(StringComparer.Ordinal);
            public readonly Dictionary<string, NetworkLabelBinding> LabelBindings = new Dictionary<string, NetworkLabelBinding>(StringComparer.Ordinal);
        }

        private sealed class NetworkLabelBinding
        {
            public Transform Target;
            public object Source;
            public Item Definition;
            public bool ShowOwner;
        }

        private sealed class PlacedTargetRecord
        {
            public string Key;
            public string FallbackName;
            public Component Target;
            public Item Definition;
            public PhotonView View;
            public PhotonView OwnerView;
            public int OwnerActorNumber;
            public ThingSceneTargetType? LegacySceneTargetType;
            public Rope Rope;
            public bool IsRope;
            public RopeAnchor RopeAnchor;
            public ClimbHandle PitonHandle;
            public Func<Vector3> PositionProvider;
            public bool ShowOwner = true;
        }

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<bool> _restrictClientPresets;
        private bool _lastObservedClientRestriction;
        private ConfigEntry<KeyCode> _scanKey;
        private ConfigEntry<KeyCode> _windowKey;
        private ConfigEntry<ThingScanMode> _scanMode;
        private ConfigEntry<float> _displayDuration;
        private ConfigEntry<float> _maxDistance;
        private ConfigEntry<ThingMapScope> _mapScope;
        private readonly ThingSegmentFilter _segmentFilter = new ThingSegmentFilter();
        private int _observedSegmentRevision;
        private ThingMapScope _observedMapScope;
        private ConfigEntry<float> _fontSize;
        private ConfigEntry<ThingLabelFont> _labelFontChoice;
        private ConfigEntry<bool> _showOffscreen;
        private ConfigEntry<bool> _showOwnerNames;
        private ConfigEntry<ThingNameLanguage> _nameLanguage;
        private ConfigEntry<ThingLocationScope> _locationScopes;
        private ConfigEntry<string> _selectedItemIds;
        private ConfigEntry<bool> _selectedLuggage;
        private ConfigEntry<string> _selectedLuggageTypesConfig;
        private ConfigEntry<string> _selectedSceneTargetTypesConfig;
        private ConfigEntry<int> _presetSchemaVersion;
        private ConfigEntry<int> _pickupIdentityVersion;
        private ConfigEntry<string> _localPresetsConfig;
        private ConfigEntry<string> _activeLocalPresetIdConfig;
        private ConfigEntry<string> _activeLocalPresetIdsConfig;
        private ConfigEntry<string> _selectedSharedPresetIdConfig;
        private ConfigEntry<ThingPresetShareMode> _shareModeConfig;

        private void Awake()
        {
            _log = Logger;
            _enabled = Config.Bind("General", "Enabled", true, "Enable WhereIsThing.");
            _scanKey = Config.Bind("General", "ScanKey", KeyCode.C, "Scan for selected items.");
            _windowKey = Config.Bind("General", "WindowKey", KeyCode.C, "Open the item selection window while holding Alt.");
            _scanMode = Config.Bind("General", "ScanMode", ThingScanMode.Persistent, "Persistent labels or labels that expire after a few seconds.");
            _displayDuration = Config.Bind("General", "DisplayDurationSeconds", 8f, "Timed display duration in seconds.");
            _nameLanguage = Config.Bind("Display", "NameLanguage", ThingNameLanguage.Game, "Game language, English, or Simplified Chinese.");
            _maxDistance = Config.Bind("Display", "MaxDistance", 500f, "Maximum distance in metres. Set to 0 for unlimited.");
            _mapScope = Config.Bind("Display", "MapScope", ThingMapScope.CurrentSegment,
                "Current segment including its entry camp, or the whole map. Unlimited distance respects this scope.");
            _fontSize = Config.Bind("Display", "FontSize", 22f, "Distance label font size.");
            _labelFontChoice = Config.Bind("Display", "LabelFont", ThingLabelFont.GameDefault,
                "Font used by location labels. Chinese names use an available Chinese-capable game font when needed.");
            _showOffscreen = Config.Bind("Display", "ShowOffscreenDirection", true, "Show a direction marker for offscreen items.");
            _showOwnerNames = Config.Bind("Display", "ShowOwnerNames", true, "Show the placer name after supported labels.");
            _presetSchemaVersion = Config.Bind("Presets", "PresetSchemaVersion", 0,
                new ConfigDescription("Preset schema version for WhereIsThing.", null, "Hidden"));
            _pickupIdentityVersion = Config.Bind("Presets", "PickupIdentityVersion", 0,
                new ConfigDescription("Pickup selection migration version.", null, "Hidden"));
            _localPresetsConfig = Config.Bind("Presets", "LocalPresets", string.Empty,
                new ConfigDescription("Serialized local presets managed by WhereIsThing.", null, "Hidden"));
            _activeLocalPresetIdConfig = Config.Bind("Presets", "ActiveLocalPresetId", string.Empty,
                new ConfigDescription("Currently active local preset ID.", null, "Hidden"));
            _activeLocalPresetIdsConfig = Config.Bind("Presets", "ActiveLocalPresetIds", string.Empty,
                new ConfigDescription("Currently enabled local preset IDs.", null, "Hidden"));
            _selectedSharedPresetIdConfig = Config.Bind("Presets", "SelectedSharedPresetId", string.Empty,
                new ConfigDescription("Currently selected shared preset ID.", null, "Hidden"));
            _shareModeConfig = Config.Bind("Presets", "ShareMode", ThingPresetShareMode.PublishedPresets, "Whether the host shares presets with clients.");
            _restrictClientPresets = Config.Bind("Presets", "RestrictClientPresets", true,
                "When you join as a client, use the host's shared presets (or fallback presets). Disable to use and edit your own local presets. This is a local preference, not a host-enforced rule.");
            _lastObservedClientRestriction = IsClientPresetRestricted();
            _selectedItemIds = Config.Bind("Selection", "SelectedItemIds", string.Empty,
                new ConfigDescription("Comma-separated item IDs selected in the window.", null, "Hidden"));
            _selectedLuggage = Config.Bind("Selection", "SelectedLuggage", false,
                new ConfigDescription("Track unopened luggage targets.", null, "Hidden"));
            _selectedLuggageTypesConfig = Config.Bind("Selection", "SelectedLuggageTypes", string.Empty,
                new ConfigDescription("Comma-separated luggage types selected in the window.", null, "Hidden"));
            _selectedSceneTargetTypesConfig = Config.Bind("Selection", "SelectedSceneTargetTypes", string.Empty,
                new ConfigDescription("Comma-separated scene target types selected in the window.", null, "Hidden"));
            _locationScopes = Config.Bind("Selection", "LocationScopes", ThingLocationScope.Ground | ThingLocationScope.Backpack | ThingLocationScope.Luggage,
                "Locations to scan: ground, held, backpack contents, unopened luggage, or selected amulet fragments held by statues.");
            LoadSelection();
            LoadPresetState();
            _selectedSharedPresetId = _selectedSharedPresetIdConfig.Value ?? string.Empty;
            _lastObservedShareMode = _shareModeConfig.Value;
            _lastObservedLabelFont = _labelFontChoice.Value;
            _lastObservedFontSize = _fontSize.Value;
            _lastObservedNameLanguage = _nameLanguage.Value;

            ModConfigLocalization.ApplyLocalizedDescriptions(GetConfigEntries());
            _harmony = new Harmony(PluginGuid + ".ModConfigLocalization");
            try
            {
                ModConfigLocalization.PatchDisplayNames(_harmony);
                _harmony.PatchAll();
            }
            catch (Exception ex)
            {
                _log.LogWarning("ModConfig localization patch skipped: " + ex.Message);
            }
            LocalizedText.OnLangugageChanged += OnGameLanguageChanged;
            CreateCanvas();
            SceneManager.sceneLoaded += OnSceneLoaded;
            PhotonNetwork.AddCallbackTarget(this);
            SubscribeToItemThrown();
            _log.LogInfo(PluginName + " v" + PluginVersion + " loaded. Hold Alt and press " + _windowKey.Value + " to manage presets; press " + _scanKey.Value + " to scan.");
        }

        private void Start()
        {
            ScheduleModConfigRefresh();
        }

        private void Update()
        {
            ObserveScanSettings();
            UpdateRoomState();
            UpdateLabelStyleIfChanged();

            bool menuInput = (_window != null && _window.IsOpen) || (_pickerWindow != null && _pickerWindow.IsOpen);
            if (Input.GetKeyDown(_windowKey.Value) && IsAltHeld())
            {
                ToggleWindow();
                menuInput = true;
            }
            else if (_window != null && _window.IsOpen)
            {
                _window.Tick();
                if (Input.GetKeyDown(KeyCode.Escape)) _window.Close();
            }
            else if (_pickerWindow != null && _pickerWindow.IsOpen)
            {
                _pickerWindow.Tick();
            }

            if (!_enabled.Value)
            {
                _displayActive = false;
                ClearLabels();
                return;
            }

            if (!menuInput && Input.GetKeyDown(_scanKey.Value) && !IsAltHeld())
            {
                BeginScan();
            }

            if (!_displayActive)
            {
                return;
            }

            if (_effectiveScanMode == ThingScanMode.Timed && Time.unscaledTime >= _hideAt)
            {
                _displayActive = false;
                ClearLabels();
                return;
            }

            if (Time.unscaledTime >= _nextRefresh)
            {
                RefreshLabels();
                _nextRefresh = Time.unscaledTime + NetworkDiscoveryInterval;
            }

            _segmentFilter.Refresh();
            if (_observedSegmentRevision != _segmentFilter.Revision || _observedMapScope != _mapScope.Value)
            {
                _observedSegmentRevision = _segmentFilter.Revision;
                _observedMapScope = _mapScope.Value;
                _nextRefresh = 0f;
                _nextSceneDiscovery = 0f;
                _sceneDiscoveryStep = 0;
            }
            ProcessPhotonDiscovery();
            ProcessSceneDiscovery();

            Camera camera = GetMainCamera();
            Vector3 cameraPosition = camera == null ? Vector3.zero : camera.transform.position;
            float maxDistance = _maxDistance.Value;
            _labelUpdateBuffer.Clear();
            _labelUpdateBuffer.AddRange(_labels.Values);
            foreach (ThingLabel label in _labelUpdateBuffer)
            {
                if (label.IsValid)
                {
                    if (!label.Update(camera, cameraPosition, maxDistance, _showOffscreen.Value)) RemoveLabel(label.Key);
                }
                else
                {
                    RemoveLabel(label.Key);
                }
            }
        }

        private void LateUpdate()
        {
            if (_window != null && _window.IsOpen)
            {
                _window.MaintainCursor();
            }
            if (_pickerWindow != null && _pickerWindow.IsOpen)
            {
                _pickerWindow.MaintainCursor();
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            LocalizedText.OnLangugageChanged -= OnGameLanguageChanged;
            ModConfigLocalization.Shutdown();
            PhotonNetwork.RemoveCallbackTarget(this);
            UnsubscribeFromItemThrown();
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }
            if (_window != null)
            {
                _window.Dispose();
            }
            if (_pickerWindow != null)
            {
                _pickerWindow.Dispose();
            }
            ClearLabels();
            ClearDiscoveryCaches();
            ReleaseLabelMaterial();
            if (_canvas != null)
            {
                Destroy(_canvas.gameObject);
            }
        }

        private IEnumerator DeferredModConfigRefresh()
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            yield return null;
            while (Time.realtimeSinceStartup < deadline && GUIManager.instance == null)
            {
                yield return null;
            }
            yield return null;
            ModConfigLocalization.ApplyLocalizedDescriptions(GetConfigEntries());
            ModConfigLocalization.RefreshVisibleUi();
            _modConfigRefreshScheduled = false;
        }

        private void ScheduleModConfigRefresh()
        {
            if (_modConfigRefreshScheduled)
            {
                return;
            }

            _modConfigRefreshScheduled = true;
            StartCoroutine(DeferredModConfigRefresh());
        }

        private void OnGameLanguageChanged()
        {
            FontHelper.InvalidateCache();
            _font = FontHelper.GetChineseCapable();
            _window?.RefreshLanguage(_font);
            _pickerWindow?.RefreshLanguage(_font);
            _labelFont = null;
            ReleaseLabelMaterial();
            _labelFontWarningLogged = false;
            ApplyLabelStyleToExisting(true);
            ScheduleModConfigRefresh();
        }

        private IEnumerable<ConfigEntryBase> GetConfigEntries()
        {
            return new ConfigEntryBase[]
            {
                _enabled,
                _scanKey,
                _windowKey,
                _scanMode,
                _displayDuration,
                _nameLanguage,
                _maxDistance,
                _mapScope,
                _fontSize,
                _labelFontChoice,
                _showOffscreen,
                _presetSchemaVersion,
                _pickupIdentityVersion,
                _localPresetsConfig,
                _activeLocalPresetIdConfig,
                _activeLocalPresetIdsConfig,
                _selectedSharedPresetIdConfig,
                _shareModeConfig,
                _restrictClientPresets,
                _selectedItemIds,
                _selectedLuggage,
                _selectedLuggageTypesConfig,
                _selectedSceneTargetTypesConfig,
                _locationScopes
            };
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _segmentFilter.Reset();
            if (_window != null)
            {
                _window.Close();
                _window.Dispose();
                _window = null;
            }
            if (_pickerWindow != null)
            {
                _pickerWindow.Close();
                _pickerWindow.Dispose();
                _pickerWindow = null;
            }
            ClearLabels();
            ClearDiscoveryCaches();
            _sceneGeneration++;
            _thrownItemEvidence.Clear();
            StartCoroutine(RefreshSceneEventSubscriptions());
            FontHelper.InvalidateCache();
            _font = null;
            _labelFont = null;
            ReleaseLabelMaterial();
            _mainCamera = null;
            _labelFontWarningLogged = false;
            _nextRefresh = Time.unscaledTime + 1f;
        }

        private void CreateCanvas()
        {
            GameObject canvasObject = new GameObject("WhereIsThingCanvas");
            DontDestroyOnLoad(canvasObject);
            canvasObject.layer = 5;
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Keep below TMP dropdowns (30000); TFA raises its windows above active canvases.
            _canvas.sortingOrder = 20000;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        private void ToggleWindow()
        {
            if (_window != null && _window.IsOpen)
            {
                _window.Close();
                return;
            }
            if (_pickerWindow != null && _pickerWindow.IsOpen)
            {
                _pickerWindow.Close();
                return;
            }

            if (!TryLoadCatalog())
            {
                _log.LogWarning("Item database is not ready yet; the selection window will be available after the game HUD loads.");
                return;
            }

            _font = _font ?? FontHelper.GetChineseCapable();
            if (_font == null)
            {
                _log.LogWarning("No game TMP font is available yet; cannot create the selection window.");
                return;
            }

            if (_pickerWindow == null)
            {
                _pickerWindow = new ThingPresetPickerWindow(_canvas, _font);
            }
            OpenPresetPicker();
        }

        private void BeginScan()
        {
            if (!TryLoadCatalog())
            {
                return;
            }

            RefreshEffectivePresetState(false, false);
            if (_selectedIds.Count == 0 && _selectedLuggageTypes.Count == 0 && _selectedSceneTargetTypes.Count == 0)
            {
                _displayActive = false;
                ClearLabels();
                return;
            }

            _displayActive = true;
            _hideAt = _effectiveScanMode == ThingScanMode.Timed
                ? Time.unscaledTime + Mathf.Max(0.5f, _effectiveDisplayDuration)
                : float.PositiveInfinity;
            _nextRefresh = 0f;
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _segmentFilter.Refresh();
            RefreshEffectivePresetState(false, false);
            if (_canvas == null || (_selectedIds.Count == 0 && _selectedLuggageTypes.Count == 0 && _selectedSceneTargetTypes.Count == 0))
            {
                ClearLabels();
                return;
            }

            if (!EnsureLabelFont(true))
            {
                return;
            }

            StartPhotonDiscoveryCycle();
            RefreshRegisteredTargets();
            if (_sceneDiscoveryStep >= SceneDiscoveryStepCount && Time.unscaledTime >= _nextSceneDiscovery)
            {
                _sceneDiscoveryStep = 0;
                _nextSceneDiscovery = Time.unscaledTime + FullValidationInterval;
            }

            foreach (ThingLabel label in _labels.Values)
            {
                label.RefreshContent();
            }
        }

        private string GetOwnerName(int actorNumber, PhotonView fallbackView = null)
        {
            if (_showOwnerNames == null || !_showOwnerNames.Value)
            {
                return string.Empty;
            }

            if (actorNumber <= 0)
            {
                return string.Empty;
            }

            string cachedName;
            if (_ownerNamesByActor.TryGetValue(actorNumber, out cachedName))
            {
                return cachedName;
            }

            Photon.Realtime.Player player = PhotonNetwork.CurrentRoom == null
                ? null
                : PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
            if (player == null && PhotonNetwork.CurrentRoom == null && fallbackView != null)
            {
                player = fallbackView.Owner;
            }

            string nickName = player == null ? null : player.NickName;
            string result = string.IsNullOrWhiteSpace(nickName) ? string.Empty : nickName.Trim();
            if (player != null)
            {
                _ownerNamesByActor[actorNumber] = result;
            }
            return result;
        }

        private static bool IsPlayerCreatedView(PhotonView view)
        {
            return view != null && view.gameObject != null && view.gameObject.activeInHierarchy &&
                !view.IsRoomView && (view.CreatorActorNr > 0 || (view.Owner != null && view.Owner.ActorNumber > 0));
        }

        private static RopeAnchor GetAttachedRopeAnchor(Rope rope)
        {
            return rope == null || RopeAttachedAnchorField == null
                ? null
                : RopeAttachedAnchorField.GetValue(rope) as RopeAnchor;
        }

        private static bool ShouldShowHeldItem(Item item)
        {
            RopeShooter ropeShooter = item == null ? null : item.GetComponent<RopeShooter>();
            return ropeShooter == null || ropeShooter.HasAmmo;
        }

        private static bool IsPitonActive(ClimbHandle handle)
        {
            return handle != null && handle.gameObject.activeInHierarchy &&
                handle.GetComponentsInChildren<Transform>(true).Any(child => child != handle.transform && child.gameObject.activeInHierarchy);
        }

        private static bool HasRunSetting(Component target, RunSettings.SETTINGTYPE setting)
        {
            DisableBasedOnRunSettings disable = target == null ? null : target.GetComponentInParent<DisableBasedOnRunSettings>();
            return disable != null && disable.disableIfSettingDisabled == setting;
        }

        private static bool HasComponentInHierarchy<T>(Component target) where T : Component
        {
            return target != null && target.GetComponentInChildren<T>(true) != null;
        }

        private static bool HasObjectNameInHierarchy(Component target, params string[] objectNames)
        {
            if (target == null || objectNames == null)
            {
                return false;
            }

            for (Transform current = target.transform; current != null; current = current.parent)
            {
                string currentName = NormalizeObjectName(current.gameObject.name);
                foreach (string objectName in objectNames)
                {
                    if (string.Equals(currentName, objectName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            foreach (Transform current in target.GetComponentsInChildren<Transform>(true))
            {
                string currentName = NormalizeObjectName(current.gameObject.name);
                foreach (string objectName in objectNames)
                {
                    if (string.Equals(currentName, objectName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static string NormalizeObjectName(string value)
        {
            string name = value ?? string.Empty;
            int cloneSuffix = name.IndexOf("(Clone)", StringComparison.OrdinalIgnoreCase);
            return cloneSuffix < 0 ? name.Trim() : name.Substring(0, cloneSuffix).Trim();
        }


        private static Vector3 GetMultipleGroundPointsPosition(Component target)
        {
            if (target == null)
            {
                return Vector3.zero;
            }

            Transform groundPoints = target.transform.Find("GroundPoints");
            if (groundPoints == null || groundPoints.childCount == 0)
            {
                return target.transform.position;
            }

            Vector3 total = Vector3.zero;
            int count = 0;
            for (int i = 0; i < groundPoints.childCount; i++)
            {
                Transform point = groundPoints.GetChild(i);
                if (point == null)
                {
                    continue;
                }

                total += point.position;
                count++;
            }

            return count == 0 ? target.transform.position : total / count;
        }


        private Camera GetMainCamera()
        {
            if (_mainCamera == null || !_mainCamera.isActiveAndEnabled || Time.unscaledTime >= _nextCameraCheck)
            {
                _mainCamera = Camera.main;
                _nextCameraCheck = Time.unscaledTime + 0.5f;
            }
            return _mainCamera;
        }

        private void RefreshMobLabels()
        {
            if (!_selectedSceneTargetTypes.Contains(ThingSceneTargetType.Beetle) &&
                !_selectedSceneTargetTypes.Contains(ThingSceneTargetType.Scorpion))
            {
                return;
            }

            MobManager mobManager = MobManager.instance;
            if (mobManager == null || mobManager.mobs == null)
            {
                return;
            }

            foreach (Mob mob in mobManager.mobs)
            {
                if (mob == null || !mob.gameObject.activeInHierarchy)
                {
                    continue;
                }

                ThingSceneTargetType sceneTargetType;
                if (!TryGetMobSceneTargetType(mob, out sceneTargetType))
                {
                    continue;
                }

                if (!_selectedSceneTargetTypes.Contains(sceneTargetType))
                {
                    continue;
                }

                MobItem mobItem = mob.GetComponent<MobItem>();
                if (mobItem != null && mobItem.itemState != ItemState.Ground)
                {
                    continue;
                }

                Mob captured = mob;
                ThingSceneTargetType capturedType = sceneTargetType;
                AddSceneLabel(capturedType, captured,
                    delegate { return ThingCatalog.GetSceneTargetDisplayName(capturedType, _nameLanguage.Value); },
                    delegate
                    {
                        return captured != null && captured.gameObject.activeInHierarchy &&
                            _selectedSceneTargetTypes.Contains(capturedType);
                    });
            }
        }

        private bool ShouldPreferMobSceneLabel(Item item)
        {
            if (item.itemState != ItemState.Ground || !(item is MobItem))
            {
                return false;
            }

            ThingSceneTargetType sceneTargetType;
            Mob mob = item.GetComponent<Mob>();
            return TryGetMobSceneTargetType(mob, out sceneTargetType) &&
                _selectedSceneTargetTypes.Contains(sceneTargetType);
        }


        private static bool TryGetMobSceneTargetType(Mob mob, out ThingSceneTargetType sceneTargetType)
        {
            if (mob is Beetle)
            {
                sceneTargetType = ThingSceneTargetType.Beetle;
                return true;
            }
            if (mob is Scorpion)
            {
                sceneTargetType = ThingSceneTargetType.Scorpion;
                return true;
            }

            sceneTargetType = default(ThingSceneTargetType);
            return false;
        }

        private void RefreshActiveSceneTargets<T>(ThingSceneTargetType sceneTargetType,
            Func<T, bool> additionalFilter = null) where T : Component
        {
            if (!_selectedSceneTargetTypes.Contains(sceneTargetType))
            {
                return;
            }

            foreach (T target in FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (target == null || !target.gameObject.activeInHierarchy || !IsWithinLabelRange(target.transform) ||
                    (additionalFilter != null && !additionalFilter(target)))
                {
                    continue;
                }

                T captured = target;
                float nextFilterCheck = 0f;
                bool matchesFilter = true;
                AddSceneLabel(sceneTargetType, captured,
                    delegate { return ThingCatalog.GetSceneTargetDisplayName(sceneTargetType, _nameLanguage.Value); },
                    delegate
                    {
                        if (captured == null || !captured.gameObject.activeInHierarchy ||
                            !_selectedSceneTargetTypes.Contains(sceneTargetType)) return false;
                        if (additionalFilter != null && Time.unscaledTime >= nextFilterCheck)
                        {
                            matchesFilter = additionalFilter(captured);
                            nextFilterCheck = Time.unscaledTime + 0.25f;
                        }
                        return matchesFilter;
                    });
            }
        }

        private void AddSceneLabel(ThingSceneTargetType sceneTargetType, Component target,
            Func<string> titleProvider, Func<bool> isValid, Func<Vector3> positionProvider = null)
        {
            if (target == null || target.gameObject == null)
            {
                return;
            }

            string key = "scene:" + sceneTargetType + ":" + target.GetInstanceID();
            AddLabel(key, target.transform, titleProvider, isValid, positionProvider);
        }

        private void AddLabel(string key, Transform target, Func<string> titleProvider, Func<bool> isValid)
        {
            AddLabel(key, target, titleProvider, isValid, null);
        }

        private void AddLabel(string key, Transform target, Func<string> titleProvider, Func<bool> isValid,
            Func<Vector3> positionProvider)
        {
            AddLabel(key, target, titleProvider, isValid, positionProvider, null);
        }

        private void AddLabel(string key, Transform target, Func<string> titleProvider, Func<bool> isValid,
            Func<Vector3> positionProvider, Func<string> ownerProvider)
        {
            if (_labels.ContainsKey(key))
            {
                return;
            }
            if (!IsWithinLabelRange(target, positionProvider) || !IsWithinMapScope(target, positionProvider)) return;

            float scopeCheckAt = 0f;
            int scopeRevision = -1;
            ThingMapScope previousScope = _mapScope.Value;
            bool withinScope = true;
            Func<bool> scopedValidity = delegate
            {
                if (isValid != null && !isValid()) return false;
                if (Time.unscaledTime >= scopeCheckAt || scopeRevision != _segmentFilter.Revision || previousScope != _mapScope.Value)
                {
                    withinScope = IsWithinMapScope(target, positionProvider);
                    scopeCheckAt = Time.unscaledTime + 0.25f;
                    scopeRevision = _segmentFilter.Revision;
                    previousScope = _mapScope.Value;
                }
                return withinScope;
            };
            _labels.Add(key, new ThingLabel(key, _canvas.transform, target, titleProvider, scopedValidity,
                positionProvider, ownerProvider, _labelFont, _labelTitleMaterial, _labelDetailMaterial, _fontSize.Value));
        }

        private bool IsWithinLabelRange(Transform target, Func<Vector3> positionProvider = null)
        {
            if (target == null && positionProvider == null) return false;
            float distance = _maxDistance.Value;
            Camera camera = GetMainCamera();
            if (distance <= 0f || camera == null) return true;
            Vector3 position = positionProvider == null ? target.position : positionProvider();
            return ThingLabelRange.Within(position, camera.transform.position, distance, 10f);
        }

        private bool IsWithinMapScope(Transform target, Func<Vector3> positionProvider)
        {
            return target != null && (_mapScope.Value == ThingMapScope.WholeMap ||
                _segmentFilter.Includes(target, positionProvider == null ? target.position : positionProvider()));
        }

        private void UpdateLabelStyleIfChanged()
        {
            if (_labelFontChoice.Value == _lastObservedLabelFont &&
                Mathf.Approximately(_fontSize.Value, _lastObservedFontSize) &&
                _nameLanguage.Value == _lastObservedNameLanguage)
            {
                return;
            }

            _lastObservedLabelFont = _labelFontChoice.Value;
            _lastObservedFontSize = _fontSize.Value;
            _lastObservedNameLanguage = _nameLanguage.Value;
            _labelFont = null;
            ReleaseLabelMaterial();
            _labelFontWarningLogged = false;
            ApplyLabelStyleToExisting(true);
            if (_pickerWindow != null && _pickerWindow.IsOpen) _pickerWindow.RefreshLanguage(_font);
        }

        private bool EnsureLabelFont(bool logResolution)
        {
            if (_labelFont != null)
            {
                EnsureLabelMaterial();
                return true;
            }

            _labelFont = FontHelper.GetLabelFont(_labelFontChoice.Value);
            if (ThingCatalog.UseChineseNames(_nameLanguage.Value) && !FontHelper.IsChineseCapable(_labelFont))
                _labelFont = FontHelper.GetChineseCapable() ?? _labelFont;
            if (_labelFont == null)
            {
                if (logResolution && !_labelFontWarningLogged)
                {
                    _log.LogWarning("[Display] No TMP font is currently available for location labels");
                    _labelFontWarningLogged = true;
                }
                return false;
            }

            _labelFontWarningLogged = false;
            EnsureLabelMaterial();
            return true;
        }

        private void EnsureLabelMaterial()
        {
            if ((_labelTitleMaterial != null && _labelDetailMaterial != null) ||
                _labelFont == null || _labelFont.material == null)
            {
                return;
            }

            ReleaseLabelMaterial();
            _labelTitleMaterial = CreateLabelMaterial(
                "WhereIsThing Label Title", 0.055f, 0.80f, 0.35f, 0.05f, 0.05f);
            _labelDetailMaterial = CreateLabelMaterial(
                "WhereIsThing Label Detail", 0.035f, 0.72f, 0.30f, 0f, 0.08f);
        }

        private Material CreateLabelMaterial(string name, float outlineWidth, float underlayAlpha,
            float underlayOffset, float underlayDilate, float underlaySoftness)
        {
            Material material = new Material(_labelFont.material)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (material.HasProperty("_OutlineColor") && material.HasProperty("_OutlineWidth"))
            {
                material.EnableKeyword("OUTLINE_ON");
                material.SetColor("_OutlineColor", new Color(0f, 0f, 0f, 0.88f));
                material.SetFloat("_OutlineWidth", outlineWidth);
            }
            if (material.HasProperty("_UnderlayColor") && material.HasProperty("_UnderlayOffsetX") &&
                material.HasProperty("_UnderlayOffsetY") && material.HasProperty("_UnderlayDilate") &&
                material.HasProperty("_UnderlaySoftness"))
            {
                material.EnableKeyword("UNDERLAY_ON");
                material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, underlayAlpha));
                material.SetFloat("_UnderlayOffsetX", underlayOffset);
                material.SetFloat("_UnderlayOffsetY", -underlayOffset);
                material.SetFloat("_UnderlayDilate", underlayDilate);
                material.SetFloat("_UnderlaySoftness", underlaySoftness);
            }
            return material;
        }

        private void ReleaseLabelMaterial()
        {
            if (_labelTitleMaterial != null)
            {
                Destroy(_labelTitleMaterial);
                _labelTitleMaterial = null;
            }
            if (_labelDetailMaterial != null)
            {
                Destroy(_labelDetailMaterial);
                _labelDetailMaterial = null;
            }
        }

        private void ApplyLabelStyleToExisting(bool logResolution)
        {
            if (!EnsureLabelFont(logResolution))
            {
                return;
            }

            foreach (ThingLabel label in _labels.Values)
            {
                label.ApplyStyle(_labelFont, _labelTitleMaterial, _labelDetailMaterial, _fontSize.Value);
                label.RefreshContent();
            }
        }

        private void ApplyWindowChanges(HashSet<ushort> selection, HashSet<ThingLuggageType> selectedLuggageTypes,
            HashSet<ThingSceneTargetType> selectedSceneTargetTypes, ThingNameLanguage language)
        {
            if (IsClientPresetRestricted() || string.IsNullOrEmpty(_editingPresetId)) return;
            ThingPresetDefinition preset = GetEditableLocalPreset(_editingPresetId);
            if (preset == null)
            {
                return;
            }

            preset.SelectedItemIds.Clear();
            preset.SelectedItemIds.UnionWith(selection ?? Enumerable.Empty<ushort>());
            preset.SelectedLuggageTypes.Clear();
            preset.SelectedLuggageTypes.UnionWith(selectedLuggageTypes ?? Enumerable.Empty<ThingLuggageType>());
            preset.SelectedSceneTargetTypes.Clear();
            preset.SelectedSceneTargetTypes.UnionWith(selectedSceneTargetTypes ?? Enumerable.Empty<ThingSceneTargetType>());
            _nameLanguage.Value = language;

            SaveLocalPresets();
            RefreshEffectivePresetState(true, true);
            MarkShareDirty(true);
        }

        private void LoadSelection()
        {
            _selectedIds.Clear();
            foreach (string value in (_selectedItemIds.Value ?? string.Empty).Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                ushort id;
                if (ushort.TryParse(value, out id))
                {
                    _selectedIds.Add(id);
                }
            }

            _selectedLuggageTypes.Clear();
            string configuredLuggageTypes = _selectedLuggageTypesConfig.Value ?? string.Empty;
            bool hasConfiguredLuggageTypes = !string.IsNullOrWhiteSpace(configuredLuggageTypes);
            foreach (string value in configuredLuggageTypes.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                ThingLuggageType luggageType;
                if (Enum.TryParse(value, true, out luggageType) && Enum.IsDefined(typeof(ThingLuggageType), luggageType))
                {
                    _selectedLuggageTypes.Add(luggageType);
                }
            }

            if (!hasConfiguredLuggageTypes && _selectedLuggage.Value)
            {
                foreach (ThingLuggageType luggageType in Enum.GetValues(typeof(ThingLuggageType)))
                {
                    _selectedLuggageTypes.Add(luggageType);
                }
                _selectedLuggageTypesConfig.Value = SerializeLuggageTypes(_selectedLuggageTypes);
            }

            // Keep old configs readable, while deriving luggage scanning from the selected types.
            ThingLocationScope scopes = _locationScopes.Value & ~ThingLocationScope.Luggage;
            if (_selectedLuggageTypes.Count > 0)
            {
                scopes |= ThingLocationScope.Luggage;
            }
            _locationScopes.Value = scopes;

            _selectedSceneTargetTypes.Clear();
            foreach (string value in (_selectedSceneTargetTypesConfig.Value ?? string.Empty).Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                ThingSceneTargetType sceneTargetType;
                if (Enum.TryParse(value, true, out sceneTargetType) && Enum.IsDefined(typeof(ThingSceneTargetType), sceneTargetType))
                {
                    _selectedSceneTargetTypes.Add(sceneTargetType);
                }
            }
        }

        private void LoadPresetState()
        {
            _localPresets.Clear();
            _localPresets.AddRange(ThingPresetCodec.DeserializePresets(_localPresetsConfig.Value));
            bool initializeFromLegacy = _localPresets.Count == 0;
            if (initializeFromLegacy)
            {
                _localPresets.Clear();
                _localPresets.Add(ThingPresetFactory.CreateAchievementPreset());
                _localPresets.Add(ThingPresetFactory.CreateSurvivalPreset());
                _localPresets.Add(ThingPresetFactory.CreateAscentEightPreset());
                _localPresets.Add(ThingPresetFactory.CreatePlayerPlacedPreset());
                if (_selectedIds.Count > 0 || _selectedLuggageTypes.Count > 0 || _selectedSceneTargetTypes.Count > 0)
                {
                    ThingPresetDefinition migrated = new ThingPresetDefinition("preset-migrated", ThingUi.MigratedPresetName())
                    {
                        Published = false
                    };
                    migrated.SelectedItemIds.UnionWith(_selectedIds);
                    migrated.SelectedLuggageTypes.UnionWith(_selectedLuggageTypes);
                    migrated.SelectedSceneTargetTypes.UnionWith(_selectedSceneTargetTypes);

                    _localPresets.Add(migrated);
                    _activeLocalPresetIdConfig.Value = migrated.Id;
                }
                else
                {
                    _activeLocalPresetIdConfig.Value = ThingPresetFactory.AchievementPresetId;
                }
            }
            else
            {
                if (_presetSchemaVersion.Value < 3)
                {
                    UpsertBuiltInPreset(ThingPresetFactory.CreateAchievementPreset(), 0, true);
                    UpsertBuiltInPreset(ThingPresetFactory.CreateSurvivalPreset(), 1, true);
                }
                if (_presetSchemaVersion.Value < 4)
                {
                    UpsertBuiltInPreset(ThingPresetFactory.CreateAscentEightPreset(), 2, false);
                }
                if (_presetSchemaVersion.Value < 6)
                {
                    UpsertBuiltInPreset(ThingPresetFactory.CreatePlayerPlacedPreset(), 3, false);
                }
            }

            EnsureDefaultLocalPresets();

            _activeLocalPresetIds.Clear();
            _activeLocalPresetIds.AddRange(ThingPresetSelection.Load(_activeLocalPresetIdsConfig.Value,
                _activeLocalPresetIdConfig.Value, _localPresets.Select(preset => preset.Id)));
            if (string.IsNullOrWhiteSpace(_selectedSharedPresetIdConfig.Value))
            {
                _selectedSharedPresetIdConfig.Value = ThingPresetFactory.AchievementPresetId;
            }

            _selectedSharedPresetId = _selectedSharedPresetIdConfig.Value ?? string.Empty;
            _presetSchemaVersion.Value = PresetSchemaVersion;
            SaveLocalPresets();
        }

        private void EnsureDefaultLocalPresets()
        {
            UpsertBuiltInPreset(ThingPresetFactory.CreateAchievementPreset(), 0, false);
            UpsertBuiltInPreset(ThingPresetFactory.CreateSurvivalPreset(), 1, false);
            UpsertBuiltInPreset(ThingPresetFactory.CreateAscentEightPreset(), 2, false);
            UpsertBuiltInPreset(ThingPresetFactory.CreatePlayerPlacedPreset(), 3, false);
        }

        private void UpsertBuiltInPreset(ThingPresetDefinition builtInPreset, int insertIndex, bool overwrite)
        {
            ThingPresetDefinition existing = _localPresets.FirstOrDefault(preset => string.Equals(preset.Id, builtInPreset.Id, StringComparison.Ordinal));
            if (existing == null)
            {
                _localPresets.Insert(Mathf.Min(insertIndex, _localPresets.Count), builtInPreset);
                return;
            }

            if (!overwrite)
            {
                return;
            }

            existing.Name = builtInPreset.Name;
            existing.Published = true;
            existing.SelectedItemIds.Clear();
            existing.SelectedItemIds.UnionWith(builtInPreset.SelectedItemIds);
            existing.SelectedLuggageTypes.Clear();
            existing.SelectedLuggageTypes.UnionWith(builtInPreset.SelectedLuggageTypes);
            existing.SelectedSceneTargetTypes.Clear();
            existing.SelectedSceneTargetTypes.UnionWith(builtInPreset.SelectedSceneTargetTypes);

        }

        private void MigratePlacedSceneTargetsToItems()
        {
            foreach (ThingPresetDefinition preset in _localPresets)
            {
                if (preset == null)
                {
                    continue;
                }

                MigratePlacedSceneTarget(preset, ThingSceneTargetType.CheckpointFlagPlaced, "Flag_Plantable_Checkpoint");
                MigratePlacedSceneTarget(preset, ThingSceneTargetType.BounceShroomPlaced, "BounceShroom");
                MigratePlacedSceneTarget(preset, ThingSceneTargetType.RopePlaced,
                    "RopeSpool", "Anti-Rope Spool", "RopeShooter", "RopeShooterAnti");
                MigratePlacedSceneTarget(preset, ThingSceneTargetType.PitonPlaced, "ClimbingSpike");
                MigratePlacedSceneTarget(preset, ThingSceneTargetType.MagicBeanVine, "MagicBean");
            }
        }

        private void MigratePlacedSceneTarget(ThingPresetDefinition preset, ThingSceneTargetType sceneTargetType,
            params string[] itemPrefabNames)
        {
            if (!preset.SelectedSceneTargetTypes.Remove(sceneTargetType))
            {
                return;
            }

            foreach (ThingTargetDefinition definition in _catalog)
            {
                if (definition == null || definition.IsLuggage || definition.IsSceneTarget ||
                    !definition.Prefabs.Any(prefab => prefab != null && prefab.gameObject != null &&
                        itemPrefabNames.Any(name => string.Equals(prefab.gameObject.name, name, StringComparison.OrdinalIgnoreCase))))
                {
                    continue;
                }

                preset.SelectedItemIds.UnionWith(definition.ItemIds);
            }
        }

        private void SaveLocalPresets()
        {
            _selectionDirty = true;
            MarkShareDirty(false);

            _localPresetsConfig.Value = ThingPresetCodec.SerializePresets(_localPresets);
            _presetSchemaVersion.Value = PresetSchemaVersion;
            _activeLocalPresetIdsConfig.Value = string.Join(",", _activeLocalPresetIds);
            _activeLocalPresetIdConfig.Value = PrimaryLocalPresetId;
            _selectedSharedPresetIdConfig.Value = _selectedSharedPresetId;
            ThingPresetDefinition localPreset = GetCurrentLocalPreset();
            if (localPreset != null)
            {
                _selectedItemIds.Value = string.Join(",", localPreset.SelectedItemIds.OrderBy(id => id).Select(id => id.ToString()).ToArray());
                _selectedLuggage.Value = localPreset.SelectedLuggageTypes.Count > 0;
                _selectedLuggageTypesConfig.Value = SerializeLuggageTypes(localPreset.SelectedLuggageTypes);
                _selectedSceneTargetTypesConfig.Value = SerializeSceneTargetTypes(localPreset.SelectedSceneTargetTypes);
            }
            PersistConfig();
        }

        private void OpenPresetPicker()
        {
            bool allowEditing = !IsClientPresetRestricted();
            IEnumerable<ThingPresetDefinition> presets = allowEditing ? _localPresets : GetSharedPresetSource();
            IEnumerable<string> activePresetIds = allowEditing ? (IEnumerable<string>)_activeLocalPresetIds : new[] { _selectedSharedPresetId };
            bool usingFallbackPresets = !allowEditing && !HasUsableSessionPresets();
            _pickerWindow.Open(presets, activePresetIds, allowEditing, usingFallbackPresets, _shareModeConfig.Value,
                _locationScopes.Value, _scanMode.Value, _displayDuration.Value, _maxDistance.Value, _showOwnerNames.Value,
                delegate(ThingPresetDefinition preset) { return ThingPresetFactory.BuildSummary(preset, _catalog, _nameLanguage.Value); },
                allowEditing ? (Action<string>)SelectLocalPreset : SelectSharedPreset,
                allowEditing ? (Action<string>)OpenSelectionEditor : null,
                allowEditing ? (Action<string, string>)RenameLocalPreset : null,
                allowEditing ? (Action<string>)TogglePresetPublished : null,
                allowEditing ? (Action<string>)DeleteLocalPreset : null,
                allowEditing ? (Action)CreatePreset : null,
                allowEditing ? (Action<ThingPresetShareMode>)SetShareMode : null,
                SetLocationScope,
                CycleLocalScanMode,
                AdjustLocalDisplayDuration,
                SetShowOwnerNames,
                SetMaxDistance, _mapScope.Value, SetMapScope);
        }

        private void SetMapScope(ThingMapScope scope)
        {
            _mapScope.Value = scope;
            PersistConfig();
            _nextRefresh = 0f;
            _nextSceneDiscovery = 0f;
            RefreshPickerSettings();
        }

        private void SelectLocalPreset(string presetId)
        {
            if (IsClientPresetRestricted()) return;
            ThingPresetDefinition preset = GetEditableLocalPreset(presetId);
            if (preset == null) return;
            if (!ThingPresetSelection.Toggle(_activeLocalPresetIds, preset.Id)) return;
            SaveLocalPresets();
            RefreshEffectivePresetState(true, true);
            OpenPresetPicker();
        }

        private void SelectSharedPreset(string presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId))
            {
                return;
            }
            _selectionDirty = true;
            _selectedSharedPresetId = presetId;
            _selectedSharedPresetIdConfig.Value = presetId;
            PersistConfig();
            RefreshEffectivePresetState(true, true);
            OpenPresetPicker();
        }

        private void OpenSelectionEditor(string presetId)
        {
            if (IsClientPresetRestricted()) return;
            ThingPresetDefinition preset = GetEditableLocalPreset(presetId);
            if (preset == null)
            {
                return;
            }

            _editingPresetId = preset.Id;
            SaveLocalPresets();
            if (_pickerWindow != null && _pickerWindow.IsOpen)
            {
                _pickerWindow.Close();
            }
            if (_window == null)
            {
                _window = new ThingSelectionWindow(_canvas, _font, ApplyWindowChanges, null);
            }
            _window.Open(_catalog, preset.SelectedItemIds, preset.SelectedLuggageTypes, preset.SelectedSceneTargetTypes, _nameLanguage.Value);
        }

        private void CreatePreset()
        {
            if (IsClientPresetRestricted()) return;
            int customIndex = _localPresets.Count(preset => !ThingPresetFactory.IsBuiltInPresetId(preset.Id)) + 1;
            ThingPresetDefinition preset = ThingPresetFactory.CreateCustomPreset(customIndex);
            _localPresets.Add(preset);
            _activeLocalPresetIds.Insert(0, preset.Id);
            SaveLocalPresets();
            RefreshEffectivePresetState(true, true);
            MarkShareDirty(true);
            OpenPresetPicker();
        }

        private void RenameLocalPreset(string presetId, string requestedName)
        {
            if (IsClientPresetRestricted()) return;
            if (ThingPresetFactory.IsBuiltInPresetId(presetId))
            {
                return;
            }

            ThingPresetDefinition preset = _localPresets.FirstOrDefault(value =>
                value != null && string.Equals(value.Id, presetId, StringComparison.Ordinal));
            if (preset == null)
            {
                _log.LogWarning("[Rename] Preset not found: '" + (presetId ?? string.Empty) + "'");
                return;
            }

            string normalizedName = new string((requestedName ?? string.Empty).Where(value => !char.IsControl(value)).ToArray()).Trim();
            if (normalizedName.Length > 24)
            {
                normalizedName = normalizedName.Substring(0, 24).Trim();
            }
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                _log.LogWarning("[Rename] Empty preset name rejected for '" + preset.Id + "'");
                OpenPresetPicker();
                return;
            }

            preset.Name = normalizedName;
            SaveLocalPresets();
            MarkShareDirty(true);
            OpenPresetPicker();
        }

        private void DeleteLocalPreset(string presetId)
        {
            if (IsClientPresetRestricted()) return;
            if (ThingPresetFactory.IsBuiltInPresetId(presetId))
            {
                return;
            }

            ThingPresetDefinition preset = _localPresets.FirstOrDefault(value => string.Equals(value.Id, presetId, StringComparison.Ordinal));
            if (preset == null || _localPresets.Count <= 1)
            {
                return;
            }

            _localPresets.Remove(preset);
            _activeLocalPresetIds.Remove(presetId);
            if (_activeLocalPresetIds.Count == 0) _activeLocalPresetIds.Add(_localPresets[0].Id);
            SaveLocalPresets();
            RefreshEffectivePresetState(true, true);
            MarkShareDirty(true);
            OpenPresetPicker();
        }

        private void TogglePresetPublished(string presetId)
        {
            if (IsClientPresetRestricted()) return;
            ThingPresetDefinition preset = GetEditableLocalPreset(presetId);
            if (preset == null)
            {
                return;
            }

            if (ThingPresetFactory.IsBuiltInPresetId(preset.Id))
            {
                preset.Published = true;
                SaveLocalPresets();
                OpenPresetPicker();
                return;
            }

            preset.Published = !preset.Published;
            SaveLocalPresets();
            MarkShareDirty(true);
            OpenPresetPicker();
        }

        private void SetShareMode(ThingPresetShareMode shareMode)
        {
            _shareModeConfig.Value = shareMode;
            _lastObservedShareMode = shareMode;
            PersistConfig();
            MarkShareDirty(true);
            OpenPresetPicker();
        }

        private void SetLocationScope(ThingLocationScope scope, bool enabled)
        {
            ThingLocationScope newScopes = _locationScopes.Value;
            if (enabled)
            {
                newScopes |= scope;
            }
            else
            {
                newScopes &= ~scope;
            }

            if (newScopes == _locationScopes.Value)
            {
                return;
            }

            _locationScopes.Value = newScopes;
            PersistConfig();
            RefreshEffectivePresetState(true, true);
            RefreshPickerSettings();
        }

        private void CycleLocalScanMode()
        {
            _scanMode.Value = _scanMode.Value == ThingScanMode.Persistent ? ThingScanMode.Timed : ThingScanMode.Persistent;
            PersistConfig();
            ObserveScanSettings();
        }

        private void AdjustLocalDisplayDuration(int step)
        {
            float next = Mathf.Clamp(_displayDuration.Value + step * 2f, 2f, 60f);
            if (Mathf.Approximately(next, _displayDuration.Value))
            {
                return;
            }

            _displayDuration.Value = next;
            PersistConfig();
            ObserveScanSettings();
        }

        private void SetShowOwnerNames(bool enabled)
        {
            if (_showOwnerNames != null && _showOwnerNames.Value == enabled)
            {
                return;
            }

            _showOwnerNames.Value = enabled;
            PersistConfig();
            RefreshLabels();
            RefreshPickerSettings();
        }

        private void SetMaxDistance(float distance)
        {
            float next = Mathf.Clamp(Mathf.Round(distance / 20f) * 20f, 0f, 500f);
            if (_maxDistance != null && Mathf.Approximately(_maxDistance.Value, next))
            {
                return;
            }

            _maxDistance.Value = next;
            PersistConfig();
            RefreshLabels();
            if (_pickerWindow != null && _pickerWindow.IsOpen)
            {
                RefreshPickerSettings();
            }
        }

        private void PersistConfig()
        {
            try
            {
                Config.Save();
            }
            catch (Exception ex)
            {
                _log.LogWarning("[Presets] Config.Save failed: " + ex.Message);
            }
        }

        public void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            if (newPlayer != null)
            {
                _ownerNamesByActor.Remove(newPlayer.ActorNumber);
            }
        }

        public void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            if (otherPlayer != null)
            {
                _ownerNamesByActor.Remove(otherPlayer.ActorNumber);
            }
        }

        public void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, PhotonHashtable changedProps)
        {
            if (targetPlayer != null)
            {
                _ownerNamesByActor.Remove(targetPlayer.ActorNumber);
            }
        }

        private static string SerializeLuggageTypes(IEnumerable<ThingLuggageType> luggageTypes)
        {
            return string.Join(",", Enum.GetValues(typeof(ThingLuggageType))
                .Cast<ThingLuggageType>()
                .Where(luggageType => luggageTypes != null && luggageTypes.Contains(luggageType))
                .Select(luggageType => luggageType.ToString())
                .ToArray());
        }

        private static string SerializeSceneTargetTypes(IEnumerable<ThingSceneTargetType> sceneTargetTypes)
        {
            return string.Join(",", Enum.GetValues(typeof(ThingSceneTargetType))
                .Cast<ThingSceneTargetType>()
                .Where(sceneTargetType => sceneTargetTypes != null && sceneTargetTypes.Contains(sceneTargetType))
                .Select(sceneTargetType => sceneTargetType.ToString())
                .ToArray());
        }

        private static bool TryGetBackpackData(ItemInstanceData data, out BackpackData backpackData)
        {
            backpackData = null;
            return data != null && data.TryGetDataEntry<BackpackData>(DataEntryKey.BackpackData, out backpackData) && backpackData != null && backpackData.itemSlots != null;
        }


        private void RemoveLabel(string key)
        {
            ThingLabel label;
            if (_labels.TryGetValue(key, out label))
            {
                label.Dispose();
                _labels.Remove(key);
            }
        }

        private void ClearLabels()
        {
            foreach (ThingLabel label in _labels.Values)
            {
                label.Dispose();
            }
            _labels.Clear();
        }

        private static bool IsAltHeld()
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }
    }
}
