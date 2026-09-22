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
        public const string PluginVersion = "0.1.2";
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
        private readonly HashSet<string> _scanSeenBuffer = new HashSet<string>(StringComparer.Ordinal);
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
        private bool _displayActive;
        private float _hideAt;
        private float _nextRefresh;
        private Harmony _harmony;
        private bool _modConfigRefreshScheduled;
        private ThingLocationScope _effectiveScopes;
        private ThingScanMode _effectiveScanMode;
        private float _effectiveDisplayDuration;
        private string _activeLocalPresetId = string.Empty;
        private string _selectedSharedPresetId = string.Empty;
        private string _editingPresetId = string.Empty;
        private string _activeRoomName = string.Empty;
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
        private const int SceneDiscoveryStepCount = 18;
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
        private ConfigEntry<KeyCode> _scanKey;
        private ConfigEntry<KeyCode> _windowKey;
        private ConfigEntry<ThingScanMode> _scanMode;
        private ConfigEntry<float> _displayDuration;
        private ConfigEntry<float> _maxDistance;
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
        private ConfigEntry<string> _localPresetsConfig;
        private ConfigEntry<string> _activeLocalPresetIdConfig;
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
            _fontSize = Config.Bind("Display", "FontSize", 22f, "Distance label font size.");
            _labelFontChoice = Config.Bind("Display", "LabelFont", ThingLabelFont.GameDefault,
                "Font used by English location labels. Chinese text may display as tofu boxes.");
            _showOffscreen = Config.Bind("Display", "ShowOffscreenDirection", true, "Show a direction marker for offscreen items.");
            _showOwnerNames = Config.Bind("Display", "ShowOwnerNames", true, "Show the placer name after supported labels.");
            _presetSchemaVersion = Config.Bind("Presets", "PresetSchemaVersion", 0,
                new ConfigDescription("Preset schema version for WhereIsThing.", null, "Hidden"));
            _localPresetsConfig = Config.Bind("Presets", "LocalPresets", string.Empty,
                new ConfigDescription("Serialized local presets managed by WhereIsThing.", null, "Hidden"));
            _activeLocalPresetIdConfig = Config.Bind("Presets", "ActiveLocalPresetId", string.Empty,
                new ConfigDescription("Currently active local preset ID.", null, "Hidden"));
            _selectedSharedPresetIdConfig = Config.Bind("Presets", "SelectedSharedPresetId", string.Empty,
                new ConfigDescription("Currently selected shared preset ID.", null, "Hidden"));
            _shareModeConfig = Config.Bind("Presets", "ShareMode", ThingPresetShareMode.PublishedPresets, "Whether the host shares presets with clients.");
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
            _activeLocalPresetId = _activeLocalPresetIdConfig.Value ?? string.Empty;
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
            UpdateRoomState();
            UpdateLabelStyleIfChanged();

            if (Input.GetKeyDown(_windowKey.Value) && IsAltHeld())
            {
                ToggleWindow();
                return;
            }

            if (_window != null && _window.IsOpen)
            {
                _window.Tick();
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    _window.Close();
                }
                return;
            }

            if (_pickerWindow != null && _pickerWindow.IsOpen)
            {
                _pickerWindow.Tick();
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    _pickerWindow.Close();
                }
                return;
            }

            if (!_enabled.Value)
            {
                _displayActive = false;
                ClearLabels();
                return;
            }

            if (Input.GetKeyDown(_scanKey.Value) && !IsAltHeld())
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

            ProcessPhotonDiscovery();
            ProcessSceneDiscovery();

            Camera camera = GetMainCamera();
            _labelUpdateBuffer.Clear();
            _labelUpdateBuffer.AddRange(_labels.Values);
            foreach (ThingLabel label in _labelUpdateBuffer)
            {
                if (label.IsValid)
                {
                    label.Update(camera, _maxDistance.Value, _showOffscreen.Value);
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
            if (_window != null)
            {
                _window.Dispose();
                _window = null;
            }
            if (_pickerWindow != null)
            {
                _pickerWindow.Dispose();
                _pickerWindow = null;
            }
            FontHelper.InvalidateCache();
            _font = null;
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
                _fontSize,
                _labelFontChoice,
                _showOffscreen,
                _presetSchemaVersion,
                _localPresetsConfig,
                _activeLocalPresetIdConfig,
                _selectedSharedPresetIdConfig,
                _shareModeConfig,
                _selectedItemIds,
                _selectedLuggage,
                _selectedLuggageTypesConfig,
                _selectedSceneTargetTypesConfig,
                _locationScopes
            };
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
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

            RefreshEffectivePresetState(false);
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
            RefreshEffectivePresetState(false);
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
            if (_mainCamera == null || !_mainCamera.gameObject.activeInHierarchy)
            {
                _mainCamera = Camera.main;
            }
            return _mainCamera;
        }

        private void RefreshMobLabels(HashSet<string> seen)
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
                AddSceneLabel(seen, capturedType, captured,
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

        private void RefreshActiveSceneTargets<T>(HashSet<string> seen, ThingSceneTargetType sceneTargetType,
            Func<T, bool> additionalFilter = null) where T : Component
        {
            if (!_selectedSceneTargetTypes.Contains(sceneTargetType))
            {
                return;
            }

            foreach (T target in FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (target == null || !target.gameObject.activeInHierarchy ||
                    (additionalFilter != null && !additionalFilter(target)))
                {
                    continue;
                }

                T captured = target;
                AddSceneLabel(seen, sceneTargetType, captured,
                    delegate { return ThingCatalog.GetSceneTargetDisplayName(sceneTargetType, _nameLanguage.Value); },
                    delegate
                    {
                        return captured != null && captured.gameObject.activeInHierarchy &&
                            _selectedSceneTargetTypes.Contains(sceneTargetType);
                    });
            }
        }

        private void AddSceneLabel(HashSet<string> seen, ThingSceneTargetType sceneTargetType, Component target,
            Func<string> titleProvider, Func<bool> isValid, Func<Vector3> positionProvider = null)
        {
            if (target == null || target.gameObject == null)
            {
                return;
            }

            string key = "scene:" + sceneTargetType + ":" + target.GetInstanceID();
            seen.Add(key);
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

            _labels.Add(key, new ThingLabel(key, _canvas.transform, target, titleProvider, isValid,
                positionProvider, ownerProvider, _labelFont, _labelTitleMaterial, _labelDetailMaterial, _fontSize.Value));
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
        }

        private bool EnsureLabelFont(bool logResolution)
        {
            if (_labelFont != null)
            {
                EnsureLabelMaterial();
                return true;
            }

            _labelFont = FontHelper.GetLabelFont(_labelFontChoice.Value);
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
            }
        }

        private void ApplyWindowChanges(HashSet<ushort> selection, HashSet<ThingLuggageType> selectedLuggageTypes,
            HashSet<ThingSceneTargetType> selectedSceneTargetTypes, ThingNameLanguage language)
        {
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
            preset.Normalize();
            SaveLocalPresets();
            RefreshEffectivePresetState(true);
            MarkShareDirty(true);
        }

        private bool TryLoadCatalog()
        {
            if (_catalog.Count > 0)
            {
                return true;
            }

            List<ThingTargetDefinition> loaded = ThingCatalog.Load();
            if (loaded.Count == 0)
            {
                return false;
            }

            _catalog.AddRange(loaded);
            BuildCatalogIndexes();
            MigratePlacedSceneTargetsToItems();
            BuildBuiltInPresetCache();
            ResolveBuiltInPresetTargets();
            SaveLocalPresets();
            RefreshEffectivePresetState(false);
            foreach (ThingTargetDefinition definition in _catalog)
            {
                if (definition.IsLuggage)
                {
                    continue;
                }

                bool selected = definition.ItemIds.Any(_selectedIds.Contains) ||
                    (definition.IsSceneTarget && _selectedSceneTargetTypes.Contains(definition.SceneTargetType));
                if (selected)
                {
                    definition.SetSelected(_selectedIds, _selectedLuggageTypes, _selectedSceneTargetTypes, true);
                }
            }
            _selectedItemIds.Value = string.Join(",", _selectedIds.OrderBy(id => id).Select(id => id.ToString()).ToArray());
            _selectedSceneTargetTypesConfig.Value = SerializeSceneTargetTypes(_selectedSceneTargetTypes);
            return true;
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
                    migrated.Normalize();
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
            NormalizeLocalPresets();

            if (!_localPresets.Any(preset => string.Equals(preset.Id, _activeLocalPresetIdConfig.Value, StringComparison.Ordinal)))
            {
                _activeLocalPresetIdConfig.Value = _localPresets[0].Id;
            }
            if (string.IsNullOrWhiteSpace(_selectedSharedPresetIdConfig.Value))
            {
                _selectedSharedPresetIdConfig.Value = ThingPresetFactory.AchievementPresetId;
            }

            _activeLocalPresetId = _activeLocalPresetIdConfig.Value ?? string.Empty;
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
            existing.Normalize();
        }

        private void NormalizeLocalPresets()
        {
            foreach (ThingPresetDefinition preset in _localPresets)
            {
                if (preset != null)
                {
                    preset.Normalize();
                }
            }
        }

        private void BuildBuiltInPresetCache()
        {
            _builtInPresets.Clear();
            ThingPresetDefinition achievement = ThingPresetFactory.CreateAchievementPreset();
            ThingPresetDefinition survival = ThingPresetFactory.CreateSurvivalPreset();
            ThingPresetDefinition ascentEight = ThingPresetFactory.CreateAscentEightPreset();
            ThingPresetDefinition playerPlaced = ThingPresetFactory.CreatePlayerPlacedPreset();
            ThingPresetFactory.ResolveBuiltInItemTargets(survival, _catalog);
            ThingPresetFactory.ResolveBuiltInItemTargets(ascentEight, _catalog);
            ThingPresetFactory.ResolveBuiltInItemTargets(playerPlaced, _catalog);
            _builtInPresets.Add(achievement);
            _builtInPresets.Add(survival);
            _builtInPresets.Add(ascentEight);
            _builtInPresets.Add(playerPlaced);
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

        private void ResolveBuiltInPresetTargets()
        {
            foreach (ThingPresetDefinition preset in _localPresets)
            {
                int added = ThingPresetFactory.ResolveBuiltInItemTargets(preset, _catalog);
                if (added > 0)
                {
                    List<string> resolvedNames = _catalog
                        .Where(definition => definition != null && !definition.IsLuggage && !definition.IsSceneTarget &&
                            definition.ItemIds.Any(preset.SelectedItemIds.Contains))
                        .Select(definition => definition.GetDisplayName(ThingNameLanguage.English))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    string resolutionMessage = "[Presets] Resolved built-in targets for '" + ThingPresetFactory.GetDisplayName(preset) +
                        "' (" + preset.Id + "): addedItemIds=" + added + ", targetGroups=" + resolvedNames.Count +
                        ", targets=[" + string.Join(", ", resolvedNames.ToArray()) + "]";
                    if (resolvedNames.Count < 4)
                    {
                        _log.LogWarning(resolutionMessage + "; expected 4 target groups");
                    }
                }
                else if ((string.Equals(preset.Id, ThingPresetFactory.SurvivalPresetId, StringComparison.Ordinal) ||
                    string.Equals(preset.Id, ThingPresetFactory.AscentEightPresetId, StringComparison.Ordinal) ||
                    string.Equals(preset.Id, ThingPresetFactory.PlayerPlacedPresetId, StringComparison.Ordinal)) &&
                    preset.SelectedItemIds.Count == 0)
                {
                    _log.LogWarning("[Presets] Built-in target resolution found no items for '" +
                        ThingPresetFactory.GetDisplayName(preset) + "' (" + preset.Id + ")");
                }
            }
        }

        private void SaveLocalPresets()
        {
            NormalizeLocalPresets();
            _localPresetsConfig.Value = ThingPresetCodec.SerializePresets(_localPresets);
            _presetSchemaVersion.Value = PresetSchemaVersion;
            _activeLocalPresetIdConfig.Value = _activeLocalPresetId;
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

        private ThingPresetDefinition GetEditableLocalPreset(string presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId))
            {
                presetId = _activeLocalPresetId;
            }
            ThingPresetDefinition preset = _localPresets.FirstOrDefault(value => string.Equals(value.Id, presetId, StringComparison.Ordinal));
            if (preset == null && _localPresets.Count > 0)
            {
                preset = _localPresets[0];
            }
            return preset;
        }

        private ThingPresetDefinition GetCurrentLocalPreset()
        {
            ThingPresetDefinition preset = GetEditableLocalPreset(_activeLocalPresetId);
            if (preset != null)
            {
                _activeLocalPresetId = preset.Id;
            }
            return preset;
        }

        private ThingPresetDefinition GetCurrentEffectivePreset()
        {
            if (IsClientPresetRestricted())
            {
                List<ThingPresetDefinition> shared = GetSharedPresetSource().ToList();
                if (shared.Count == 0)
                {
                    return null;
                }

                ThingPresetDefinition selected = shared.FirstOrDefault(preset => string.Equals(preset.Id, _selectedSharedPresetId, StringComparison.Ordinal));
                if (selected == null)
                {
                    selected = shared[0];
                    _selectedSharedPresetId = selected.Id;
                    _selectedSharedPresetIdConfig.Value = _selectedSharedPresetId;
                }
                return selected;
            }

            return GetCurrentLocalPreset();
        }

        private IEnumerable<ThingPresetDefinition> GetSharedPresetSource()
        {
            if (HasUsableSessionPresets())
            {
                return _sessionPresets;
            }
            return _builtInPresets;
        }

        private bool HasUsableSessionPresets()
        {
            if (_sessionValid && _sessionSourceActor == GetCurrentMasterActorNumber() && _sessionPresets.Count > 0)
            {
                return true;
            }
            return _sessionPresets.Count > 0 && _sessionStaleUntil > Time.unscaledTime;
        }

        private void RefreshEffectivePresetState(bool refreshLabels)
        {
            ThingPresetDefinition preset = GetCurrentEffectivePreset();
            _selectedIds.Clear();
            _selectedLuggageTypes.Clear();
            _selectedSceneTargetTypes.Clear();

            if (preset != null)
            {
                _selectedIds.UnionWith(preset.SelectedItemIds);
                _selectedLuggageTypes.UnionWith(preset.SelectedLuggageTypes);
                _selectedSceneTargetTypes.UnionWith(preset.SelectedSceneTargetTypes);
            }
            _effectiveScopes = _locationScopes.Value;
            _effectiveScanMode = _scanMode.Value;
            _effectiveDisplayDuration = Mathf.Clamp(_displayDuration.Value, 2f, 60f);

            _effectiveScopes &= ~ThingLocationScope.Luggage;
            if (_selectedLuggageTypes.Count > 0)
            {
                _effectiveScopes |= ThingLocationScope.Luggage;
            }

            if (refreshLabels && _displayActive)
            {
                _hideAt = _effectiveScanMode == ThingScanMode.Timed
                    ? Time.unscaledTime + Mathf.Max(0.5f, _effectiveDisplayDuration)
                    : float.PositiveInfinity;
                RefreshLabels();
            }
        }

        private void OpenPresetPicker()
        {
            bool allowEditing = !IsClientPresetRestricted();
            IEnumerable<ThingPresetDefinition> presets = allowEditing ? _localPresets : GetSharedPresetSource();
            string activePresetId = allowEditing ? _activeLocalPresetId : _selectedSharedPresetId;
            bool usingFallbackPresets = !allowEditing && !HasUsableSessionPresets();
            _pickerWindow.Open(presets, activePresetId, allowEditing, usingFallbackPresets, _shareModeConfig.Value,
                _locationScopes.Value, _scanMode.Value, _displayDuration.Value, _maxDistance.Value, _showOwnerNames.Value,
                delegate(ThingPresetDefinition preset) { return ThingPresetFactory.BuildSummary(preset, _catalog); },
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
                SetMaxDistance);
        }

        private void SelectLocalPreset(string presetId)
        {
            ThingPresetDefinition preset = GetEditableLocalPreset(presetId);
            if (preset == null)
            {
                return;
            }
            _activeLocalPresetId = preset.Id;
            SaveLocalPresets();
            RefreshEffectivePresetState(true);
            OpenPresetPicker();
        }

        private void SelectSharedPreset(string presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId))
            {
                return;
            }
            _selectedSharedPresetId = presetId;
            _selectedSharedPresetIdConfig.Value = presetId;
            PersistConfig();
            RefreshEffectivePresetState(true);
            OpenPresetPicker();
        }

        private void OpenSelectionEditor(string presetId)
        {
            ThingPresetDefinition preset = GetEditableLocalPreset(presetId);
            if (preset == null)
            {
                return;
            }

            _editingPresetId = preset.Id;
            _activeLocalPresetId = preset.Id;
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
            int customIndex = _localPresets.Count(preset => !ThingPresetFactory.IsBuiltInPresetId(preset.Id)) + 1;
            ThingPresetDefinition preset = ThingPresetFactory.CreateCustomPreset(customIndex);
            _localPresets.Add(preset);
            _activeLocalPresetId = preset.Id;
            SaveLocalPresets();
            RefreshEffectivePresetState(true);
            MarkShareDirty(true);
            OpenPresetPicker();
        }

        private void RenameLocalPreset(string presetId, string requestedName)
        {
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
            if (string.Equals(_activeLocalPresetId, presetId, StringComparison.Ordinal))
            {
                _activeLocalPresetId = _localPresets[0].Id;
            }
            SaveLocalPresets();
            RefreshEffectivePresetState(true);
            MarkShareDirty(true);
            OpenPresetPicker();
        }

        private void TogglePresetPublished(string presetId)
        {
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
            RefreshEffectivePresetState(true);
            OpenPresetPicker();
        }

        private void CycleLocalScanMode()
        {
            _scanMode.Value = _scanMode.Value == ThingScanMode.Persistent ? ThingScanMode.Timed : ThingScanMode.Persistent;
            PersistConfig();
            RefreshEffectivePresetState(true);
            OpenPresetPicker();
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
            RefreshEffectivePresetState(true);
            OpenPresetPicker();
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
                OpenPresetPicker();
            }
        }

        private bool IsClientPresetRestricted()
        {
            return PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode && !PhotonNetwork.IsMasterClient;
        }

        private void MarkShareDirty(bool immediate)
        {
            _shareDirty = true;
            if (immediate)
            {
                _lastPublishAt = -10f;
            }
        }

        private void UpdateRoomState()
        {
            if (_shareModeConfig.Value != _lastObservedShareMode)
            {
                _lastObservedShareMode = _shareModeConfig.Value;
                PersistConfig();
                MarkShareDirty(true);
                if (_pickerWindow != null && _pickerWindow.IsOpen)
                {
                    OpenPresetPicker();
                }
            }

            bool inOnlineRoom = PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode && PhotonNetwork.CurrentRoom != null;
            if (!inOnlineRoom)
            {
                if (!string.IsNullOrEmpty(_activeRoomName))
                {
                    _activeRoomName = string.Empty;
                    ClearSessionOverlay();
                    RefreshEffectivePresetState(true);
                }
                return;
            }

            string roomName = PhotonNetwork.CurrentRoom.Name ?? string.Empty;
            if (!string.Equals(roomName, _activeRoomName, StringComparison.Ordinal))
            {
                _activeRoomName = roomName;
                _ownerNamesByActor.Clear();
                if (PhotonNetwork.IsMasterClient)
                {
                    ClearSessionOverlay();
                    MarkShareDirty(true);
                }
                else
                {
                    ApplySharedStateFromRoomProperties();
                    RefreshEffectivePresetState(true);
                }
            }

            if (PhotonNetwork.IsMasterClient)
            {
                PublishShareStateIfNeeded();
            }
            else if (_sessionPresets.Count > 0 && !_sessionValid && _sessionStaleUntil > 0f && Time.unscaledTime >= _sessionStaleUntil)
            {
                ClearSessionOverlay();
                RefreshEffectivePresetState(true);
                if (_pickerWindow != null && _pickerWindow.IsOpen)
                {
                    OpenPresetPicker();
                }
            }
        }

        private void PublishShareStateIfNeeded()
        {
            if (!_shareDirty || Time.unscaledTime - _lastPublishAt < 0.25f || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            {
                return;
            }

            string payload = string.Empty;
            if (_shareModeConfig.Value == ThingPresetShareMode.PublishedPresets)
            {
                payload = ThingPresetCodec.SerializePresets(_localPresets.Where(preset => preset.Published || ThingPresetFactory.IsBuiltInPresetId(preset.Id)));
            }
            string hash = ComputePayloadHash(payload);
            if (string.Equals(payload, _lastPublishedSharePayload, StringComparison.Ordinal) &&
                string.Equals(hash, _lastPublishedShareHash, StringComparison.Ordinal))
            {
                _shareDirty = false;
                _lastPublishAt = Time.unscaledTime;
                return;
            }

            PhotonHashtable props = new PhotonHashtable
            {
                { ShareModePropertyKey, (int)_shareModeConfig.Value },
                { ShareProtocolPropertyKey, ShareProtocolVersion },
                { ShareActorPropertyKey, PhotonNetwork.LocalPlayer == null ? -1 : PhotonNetwork.LocalPlayer.ActorNumber },
                { ShareHashPropertyKey, hash },
                { SharePayloadPropertyKey, payload ?? string.Empty }
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            _lastPublishedSharePayload = payload;
            _lastPublishedShareHash = hash;
            _shareDirty = false;
            _lastPublishAt = Time.unscaledTime;
        }

        private void ApplySharedStateFromRoomProperties()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || PhotonNetwork.IsMasterClient)
            {
                return;
            }

            int shareModeValue;
            if (!TryGetRoomInt(ShareModePropertyKey, out shareModeValue))
            {
                if (_sessionStaleUntil <= 0f || Time.unscaledTime >= _sessionStaleUntil)
                {
                    ClearSessionOverlay();
                }
                return;
            }

            int actorNumber;
            int protocolVersion;
            string payload;
            if (!TryGetRoomInt(ShareActorPropertyKey, out actorNumber) ||
                !TryGetRoomInt(ShareProtocolPropertyKey, out protocolVersion) ||
                !TryGetRoomString(SharePayloadPropertyKey, out payload) ||
                actorNumber != GetCurrentMasterActorNumber() ||
                protocolVersion != ShareProtocolVersion)
            {
                if (_sessionStaleUntil <= 0f || Time.unscaledTime >= _sessionStaleUntil)
                {
                    ClearSessionOverlay();
                }
                return;
            }

            ThingPresetShareMode shareMode = (ThingPresetShareMode)shareModeValue;
            if (shareMode != ThingPresetShareMode.PublishedPresets || string.IsNullOrWhiteSpace(payload))
            {
                ClearSessionOverlay();
                return;
            }

            List<ThingPresetDefinition> presets = ThingPresetCodec.DeserializePresets(payload);
            if (presets.Count == 0)
            {
                ClearSessionOverlay();
                return;
            }

            _sessionPresets.Clear();
            _sessionPresets.AddRange(presets);
            _sessionSourceActor = actorNumber;
            _sessionValid = true;
            _sessionStaleUntil = -1f;
            if (!_sessionPresets.Any(preset => string.Equals(preset.Id, _selectedSharedPresetId, StringComparison.Ordinal)))
            {
                _selectedSharedPresetId = _sessionPresets[0].Id;
                _selectedSharedPresetIdConfig.Value = _selectedSharedPresetId;
            }
        }

        private void ClearSessionOverlay()
        {
            _sessionPresets.Clear();
            _sessionSourceActor = -1;
            _sessionValid = false;
            _sessionStaleUntil = -1f;
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

        private static string ComputePayloadHash(string payload)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char value in payload ?? string.Empty)
                {
                    hash ^= value;
                    hash *= 16777619;
                }
                return hash.ToString("X8", CultureInfo.InvariantCulture);
            }
        }

        private bool TryGetRoomInt(string key, out int value)
        {
            value = 0;
            if (PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.CustomProperties == null)
            {
                return false;
            }

            object raw;
            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out raw))
            {
                return false;
            }

            if (raw is int)
            {
                value = (int)raw;
                return true;
            }
            return false;
        }

        private bool TryGetRoomString(string key, out string value)
        {
            value = null;
            if (PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.CustomProperties == null)
            {
                return false;
            }

            object raw;
            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out raw))
            {
                return false;
            }

            value = raw as string;
            return value != null;
        }

        private int GetCurrentMasterActorNumber()
        {
            return PhotonNetwork.MasterClient == null ? -1 : PhotonNetwork.MasterClient.ActorNumber;
        }

        public void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged)
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                ApplySharedStateFromRoomProperties();
                RefreshEffectivePresetState(true);
                if (_pickerWindow != null && _pickerWindow.IsOpen)
                {
                    OpenPresetPicker();
                }
            }
        }

        public void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                ClearSessionOverlay();
                MarkShareDirty(true);
            }
            else
            {
                _sessionValid = false;
                _sessionStaleUntil = Time.unscaledTime + 3f;
                ApplySharedStateFromRoomProperties();
            }
            RefreshEffectivePresetState(true);
            if (_pickerWindow != null && _pickerWindow.IsOpen)
            {
                OpenPresetPicker();
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
