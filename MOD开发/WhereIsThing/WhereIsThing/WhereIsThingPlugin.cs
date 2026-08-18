using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

namespace WhereIsThing
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class WhereIsThingPlugin : BaseUnityPlugin, IInRoomCallbacks
    {
        public const string PluginGuid = "com.wuyachiyu.WhereIsThing";
        public const string PluginName = "WhereIsThing";
        public const string PluginVersion = "0.1.0";
        private const int PresetSchemaVersion = 4;
        private const int ShareProtocolVersion = 2;
        private const string ShareModePropertyKey = "WIT.ShareMode";
        private const string ShareProtocolPropertyKey = "WIT.Protocol";
        private const string ShareActorPropertyKey = "WIT.Actor";
        private const string ShareHashPropertyKey = "WIT.Hash";
        private const string SharePayloadPropertyKey = "WIT.Payload";

        private readonly Dictionary<string, ThingLabel> _labels = new Dictionary<string, ThingLabel>();
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
        private bool _displayActive;
        private float _hideAt;
        private float _nextRefresh;
        private bool _catalogLogged;
        private Harmony _harmony;
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

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<KeyCode> _scanKey;
        private ConfigEntry<KeyCode> _windowKey;
        private ConfigEntry<ThingScanMode> _scanMode;
        private ConfigEntry<float> _displayDuration;
        private ConfigEntry<float> _maxDistance;
        private ConfigEntry<float> _fontSize;
        private ConfigEntry<bool> _showOffscreen;
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
            _showOffscreen = Config.Bind("Display", "ShowOffscreenDirection", true, "Show a direction marker for offscreen items.");
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
                "Locations to scan: ground, held, backpack contents, or unopened luggage.");
            LoadSelection();
            LoadPresetState();
            _activeLocalPresetId = _activeLocalPresetIdConfig.Value ?? string.Empty;
            _selectedSharedPresetId = _selectedSharedPresetIdConfig.Value ?? string.Empty;
            _lastObservedShareMode = _shareModeConfig.Value;

            ModConfigLocalization.ApplyLocalizedDescriptions(GetConfigEntries());
            _harmony = new Harmony(PluginGuid + ".ModConfigLocalization");
            try
            {
                ModConfigLocalization.PatchDisplayNames(_harmony);
            }
            catch (Exception ex)
            {
                _log.LogWarning("ModConfig localization patch skipped: " + ex.Message);
            }
            LocalizedText.OnLangugageChanged += OnGameLanguageChanged;
            CreateCanvas();
            SceneManager.sceneLoaded += OnSceneLoaded;
            PhotonNetwork.AddCallbackTarget(this);
            _log.LogInfo(PluginName + " v" + PluginVersion + " loaded. Hold Alt and press " + _windowKey.Value + " to manage presets; press " + _scanKey.Value + " to scan.");
        }

        private void Start()
        {
            StartCoroutine(DeferredModConfigRefresh());
        }

        private void Update()
        {
            UpdateRoomState();

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
                _nextRefresh = Time.unscaledTime + 0.5f;
            }

            Camera camera = Camera.main;
            foreach (ThingLabel label in _labels.Values.ToList())
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
            PhotonNetwork.RemoveCallbackTarget(this);
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
            ModConfigLocalization.RefreshCache();
            _log.LogInfo("[ModConfig] Deferred localization refresh completed");
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
            StartCoroutine(DeferredModConfigRefresh());
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
            FontHelper.InvalidateCache();
            _font = null;
            _nextRefresh = Time.unscaledTime + 1f;
        }

        private void CreateCanvas()
        {
            GameObject canvasObject = new GameObject("WhereIsThingCanvas");
            DontDestroyOnLoad(canvasObject);
            canvasObject.layer = 5;
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32700;
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
                _log.LogInfo("No targets selected. Hold Alt and press " + _windowKey.Value + " to choose targets.");
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

            _font = _font ?? FontHelper.GetChineseCapable();
            if (_font == null)
            {
                return;
            }

            HashSet<string> seen = new HashSet<string>();
            Item[] items = FindObjectsByType<Item>(FindObjectsSortMode.None);
            foreach (Item item in items)
            {
                if (item == null || item.gameObject == null || !item.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (ShouldPreferMobSceneLabel(item))
                {
                    continue;
                }

                if ((_effectiveScopes & ThingLocationScope.Ground) != 0 && item.itemState == ItemState.Ground && _selectedIds.Contains(item.itemID))
                {
                    string key = "item:" + item.GetInstanceID();
                    seen.Add(key);
                    Item captured = item;
                    AddLabel(key, captured.transform, delegate { return ThingCatalog.GetDisplayName(captured, _nameLanguage.Value); },
                        delegate { return captured != null && captured.gameObject.activeInHierarchy && captured.itemState == ItemState.Ground && _selectedIds.Contains(captured.itemID); });
                }
                else if ((_effectiveScopes & ThingLocationScope.Held) != 0 && item.itemState == ItemState.Held && _selectedIds.Contains(item.itemID))
                {
                    string key = "held:" + item.GetInstanceID();
                    seen.Add(key);
                    Item captured = item;
                    AddLabel(key, captured.transform, delegate { return ThingCatalog.GetDisplayName(captured, _nameLanguage.Value); },
                        delegate { return captured != null && captured.gameObject.activeInHierarchy && captured.itemState == ItemState.Held && _selectedIds.Contains(captured.itemID); });
                }

                Backpack backpack = item as Backpack;
                if ((_effectiveScopes & ThingLocationScope.Backpack) == 0 || backpack == null || backpack.itemState != ItemState.Ground || !TryGetBackpackData(backpack.data, out BackpackData backpackData))
                {
                    continue;
                }

                foreach (ItemSlot slot in backpackData.itemSlots)
                {
                    if (slot == null || slot.IsEmpty() || slot.prefab == null || !_selectedIds.Contains(slot.prefab.itemID))
                    {
                        continue;
                    }

                    Item contentPrefab = slot.prefab;
                    string key = "backpack:" + backpack.GetInstanceID() + ":" + slot.itemSlotID;
                    seen.Add(key);
                    AddLabel(key, backpack.transform,
                        delegate { return ThingCatalog.GetDisplayName(contentPrefab, _nameLanguage.Value) + "\n" + ThingCatalog.GetContainerSuffix(_nameLanguage.Value); },
                        delegate { return backpack != null && backpack.gameObject.activeInHierarchy && backpack.itemState == ItemState.Ground && BackpackContains(backpack, contentPrefab.itemID); });
                }
            }

            if (_selectedLuggageTypes.Count > 0)
            {
                foreach (Luggage luggage in Luggage.ALL_LUGGAGE.ToList())
                {
                    if (luggage == null || !luggage.gameObject.activeInHierarchy || luggage.IsOpen)
                    {
                        continue;
                    }

                    ThingLuggageType luggageType = ThingCatalog.GetLuggageType(luggage);
                    if (!_selectedLuggageTypes.Contains(luggageType))
                    {
                        continue;
                    }

                    string key = "luggage:" + luggage.GetInstanceID();
                    seen.Add(key);
                    Luggage captured = luggage;
                    AddLabel(key, captured.transform,
                        delegate { return ThingCatalog.GetLuggageLabelName(captured, _nameLanguage.Value); },
                        delegate
                        {
                            return captured != null && captured.gameObject.activeInHierarchy && !captured.IsOpen &&
                                _selectedLuggageTypes.Contains(ThingCatalog.GetLuggageType(captured)) &&
                                _selectedLuggageTypes.Count > 0;
                        }, delegate { return captured.Center(); });
                }
            }

            RefreshSceneLabels(seen);

            foreach (string key in _labels.Keys.ToList())
            {
                if (!seen.Contains(key))
                {
                    RemoveLabel(key);
                }
            }
        }

        private void RefreshSceneLabels(HashSet<string> seen)
        {
            if (_selectedSceneTargetTypes.Count == 0)
            {
                return;
            }

            if (_selectedSceneTargetTypes.Contains(ThingSceneTargetType.MushroomZombie))
            {
                ZombieManager zombieManager = ZombieManager.Instance;
                if (zombieManager != null && zombieManager.zombies != null)
                {
                    foreach (MushroomZombie zombie in zombieManager.zombies.ToList())
                    {
                        if (zombie == null || !zombie.gameObject.activeInHierarchy || zombie.currentState == MushroomZombie.State.Dead)
                        {
                            continue;
                        }

                        MushroomZombie captured = zombie;
                        Character zombieCharacter = captured.GetComponent<Character>();
                        AddSceneLabel(seen, ThingSceneTargetType.MushroomZombie, captured,
                            delegate { return ThingCatalog.GetSceneTargetDisplayName(ThingSceneTargetType.MushroomZombie, _nameLanguage.Value); },
                            delegate
                            {
                                return captured != null && captured.gameObject.activeInHierarchy &&
                                    captured.currentState != MushroomZombie.State.Dead &&
                                    _selectedSceneTargetTypes.Contains(ThingSceneTargetType.MushroomZombie);
                            }, delegate
                            {
                                return zombieCharacter != null ? zombieCharacter.Center : captured.transform.position;
                            });
                    }
                }
            }

            RefreshMobLabels(seen);

            if (_selectedSceneTargetTypes.Contains(ThingSceneTargetType.TumbleWeed))
            {
                foreach (TumbleWeed tumbleWeed in FindObjectsByType<TumbleWeed>(FindObjectsSortMode.None))
                {
                    if (tumbleWeed == null || !tumbleWeed.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    TumbleWeed captured = tumbleWeed;
                    AddSceneLabel(seen, ThingSceneTargetType.TumbleWeed, captured,
                        delegate { return ThingCatalog.GetSceneTargetDisplayName(ThingSceneTargetType.TumbleWeed, _nameLanguage.Value); },
                        delegate
                        {
                            return captured != null && captured.gameObject.activeInHierarchy &&
                                _selectedSceneTargetTypes.Contains(ThingSceneTargetType.TumbleWeed);
                        });
                }
            }

            if (_selectedSceneTargetTypes.Contains(ThingSceneTargetType.GhostBall))
            {
                Peak.GhostBallSpawner ghostBallSpawner = Peak.GhostBallSpawner.Instance;
                Peak.GhostBall ghostBall = ghostBallSpawner == null ? null : ghostBallSpawner.currentGhostBall;
                if (ghostBall != null && ghostBall.gameObject.activeInHierarchy)
                {
                    Peak.GhostBall captured = ghostBall;
                    AddSceneLabel(seen, ThingSceneTargetType.GhostBall, captured,
                        delegate { return ThingCatalog.GetSceneTargetDisplayName(ThingSceneTargetType.GhostBall, _nameLanguage.Value); },
                        delegate
                        {
                            return captured != null && captured.gameObject.activeInHierarchy &&
                                _selectedSceneTargetTypes.Contains(ThingSceneTargetType.GhostBall);
                        });
                }
            }

            if (_selectedSceneTargetTypes.Contains(ThingSceneTargetType.GloomBellTower))
            {
                foreach (GhostFire ghostFire in Peak.GloomSafeZone.ALL_GLOOM_SAFE_ZONES.OfType<GhostFire>().ToList())
                {
                    if (ghostFire == null || !ghostFire.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    GhostFire captured = ghostFire;
                    AddSceneLabel(seen, ThingSceneTargetType.GloomBellTower, captured,
                        delegate { return ThingCatalog.GetGloomBellTowerLabelName(captured, _nameLanguage.Value); },
                        delegate
                        {
                            return captured != null && captured.gameObject.activeInHierarchy &&
                                _selectedSceneTargetTypes.Contains(ThingSceneTargetType.GloomBellTower);
                    });
                }
            }

            RefreshActiveSceneTargets<Spider>(seen, ThingSceneTargetType.Spider);
            RefreshActiveSceneTargets<BeeSwarm>(seen, ThingSceneTargetType.BeeSwarm);
            RefreshActiveSceneTargets<Scoutmaster>(seen, ThingSceneTargetType.Scoutmaster);
            RefreshActiveSceneTargets<Peak.SpikeTrap>(seen, ThingSceneTargetType.SpikeTrap);
            RefreshActiveSceneTargets<Antlion>(seen, ThingSceneTargetType.Antlion);
            RefreshActiveSceneTargets<VenusFlyTrap>(seen, ThingSceneTargetType.VenusFlyTrap);
            RefreshActiveSceneTargets<Tornado>(seen, ThingSceneTargetType.Tornado);
            RefreshActiveSceneTargets<OrbThatMakesYouSleepy>(seen, ThingSceneTargetType.NapberryHypnoOrb);
            RefreshActiveSceneTargets<ArrowShooter>(seen, ThingSceneTargetType.ArrowShooter);
            RefreshActiveSceneTargets<Peak.MovingSawBlade>(seen, ThingSceneTargetType.MovingSawBlade);
            RefreshActiveSceneTargets<Peak.SpikeRoller>(seen, ThingSceneTargetType.SpikeRoller);
            RefreshActiveSceneTargets<SwingingAxe>(seen, ThingSceneTargetType.SwingingAxe);
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

            foreach (Mob mob in mobManager.mobs.ToList())
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

        private void RefreshActiveSceneTargets<T>(HashSet<string> seen, ThingSceneTargetType sceneTargetType) where T : Component
        {
            if (!_selectedSceneTargetTypes.Contains(sceneTargetType))
            {
                return;
            }

            foreach (T target in FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (target == null || !target.gameObject.activeInHierarchy)
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
            if (_labels.ContainsKey(key))
            {
                return;
            }

            _labels.Add(key, new ThingLabel(key, _canvas.transform, target, titleProvider, isValid,
                positionProvider, _font, _fontSize.Value));
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
            _log.LogInfo("[Presets] Updated preset '" + preset.Name + "' (" + preset.Id + "): items=" + preset.SelectedItemIds.Count +
                ", luggage=" + preset.SelectedLuggageTypes.Count + ", scene=" + preset.SelectedSceneTargetTypes.Count);
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
            if (!_catalogLogged)
            {
                _catalogLogged = true;
                _log.LogInfo("Loaded " + _catalog.Count + " selectable targets from ItemDatabase and scene target definitions. Categories: " +
                    string.Join(", ", _catalog.Select(item => item.Category).Distinct().OrderBy(value => value).ToArray()));
            }
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
                    _log.LogInfo("[Presets] Schema v4 migration added the Ascent 8 fallback preset without replacing existing preset edits");
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
            _log.LogInfo("[Presets] Loaded local presets: count=" + _localPresets.Count + ", activeLocal='" + _activeLocalPresetId + "', selectedShared='" + _selectedSharedPresetId + "'");
        }

        private void EnsureDefaultLocalPresets()
        {
            UpsertBuiltInPreset(ThingPresetFactory.CreateAchievementPreset(), 0, false);
            UpsertBuiltInPreset(ThingPresetFactory.CreateSurvivalPreset(), 1, false);
            UpsertBuiltInPreset(ThingPresetFactory.CreateAscentEightPreset(), 2, false);
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
            ThingPresetFactory.ResolveBuiltInItemTargets(survival, _catalog);
            ThingPresetFactory.ResolveBuiltInItemTargets(ascentEight, _catalog);
            _builtInPresets.Add(achievement);
            _builtInPresets.Add(survival);
            _builtInPresets.Add(ascentEight);
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
                    else
                    {
                        _log.LogInfo(resolutionMessage);
                    }
                }
                else if ((string.Equals(preset.Id, ThingPresetFactory.SurvivalPresetId, StringComparison.Ordinal) ||
                    string.Equals(preset.Id, ThingPresetFactory.AscentEightPresetId, StringComparison.Ordinal)) &&
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
            _log.LogInfo("[Presets] Open picker: mode=" + (allowEditing ? "local-edit" : "client-select") + ", entries=" + presets.Count() +
                ", active='" + activePresetId + "', shareMode=" + _shareModeConfig.Value + ", source=" + (usingFallbackPresets ? "fallback" : "shared"));
            _pickerWindow.Open(presets, activePresetId, allowEditing, usingFallbackPresets, _shareModeConfig.Value,
                _locationScopes.Value, _scanMode.Value, _displayDuration.Value,
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
                AdjustLocalDisplayDuration);
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
            _log.LogInfo("[Presets] Selected local preset '" + preset.Name + "' (" + preset.Id + ")");
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
            _log.LogInfo("[Presets] Selected shared preset '" + presetId + "'");
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
            _log.LogInfo("[Presets] Open editor for preset '" + preset.Name + "' (" + preset.Id + ")");
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
            _log.LogInfo("[Presets] Created preset '" + preset.Name + "' (" + preset.Id + ")");
            OpenPresetPicker();
        }

        private void RenameLocalPreset(string presetId, string requestedName)
        {
            if (ThingPresetFactory.IsBuiltInPresetId(presetId))
            {
                _log.LogInfo("[Rename] Ignored rename request for built-in preset '" + presetId + "'");
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

            string previousName = preset.Name;
            preset.Name = normalizedName;
            SaveLocalPresets();
            MarkShareDirty(true);
            _log.LogInfo("[Rename] Renamed custom preset '" + previousName + "' -> '" + preset.Name + "' (" + preset.Id +
                "), published=" + preset.Published);
            OpenPresetPicker();
        }

        private void DeleteLocalPreset(string presetId)
        {
            if (ThingPresetFactory.IsBuiltInPresetId(presetId))
            {
                _log.LogInfo("[Presets] Delete ignored for built-in preset '" + presetId + "'");
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
            _log.LogInfo("[Presets] Deleted preset '" + preset.Name + "' (" + preset.Id + "), new active='" + _activeLocalPresetId + "'");
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
                _log.LogInfo("[Presets] Publish toggle ignored for built-in preset '" + preset.Name + "' (" + preset.Id + ")");
                OpenPresetPicker();
                return;
            }

            preset.Published = !preset.Published;
            SaveLocalPresets();
            MarkShareDirty(true);
            _log.LogInfo("[Presets] Toggled publish for '" + preset.Name + "' (" + preset.Id + ") -> " + preset.Published);
            OpenPresetPicker();
        }

        private void SetShareMode(ThingPresetShareMode shareMode)
        {
            _shareModeConfig.Value = shareMode;
            _lastObservedShareMode = shareMode;
            PersistConfig();
            MarkShareDirty(true);
            _log.LogInfo("[Presets] Share mode -> " + shareMode);
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
            _log.LogInfo("[Scan] Local scope '" + scope + "' -> " + enabled + ", effective=" + _locationScopes.Value);
            OpenPresetPicker();
        }

        private void CycleLocalScanMode()
        {
            _scanMode.Value = _scanMode.Value == ThingScanMode.Persistent ? ThingScanMode.Timed : ThingScanMode.Persistent;
            PersistConfig();
            RefreshEffectivePresetState(true);
            _log.LogInfo("[Scan] Local scan mode -> " + _scanMode.Value);
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
            _log.LogInfo("[Scan] Local display duration -> " + _displayDuration.Value.ToString("0.0", CultureInfo.InvariantCulture));
            OpenPresetPicker();
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
                _log.LogInfo("[Presets] Share mode changed externally -> " + _lastObservedShareMode);
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
                    _log.LogInfo("[Share] Left online room '" + _activeRoomName + "', clearing session presets");
                    _activeRoomName = string.Empty;
                    ClearSessionOverlay();
                    RefreshEffectivePresetState(true);
                }
                return;
            }

            string roomName = PhotonNetwork.CurrentRoom.Name ?? string.Empty;
            if (!string.Equals(roomName, _activeRoomName, StringComparison.Ordinal))
            {
                _log.LogInfo("[Share] Enter room '" + roomName + "' as " + (PhotonNetwork.IsMasterClient ? "master" : "client"));
                _activeRoomName = roomName;
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
                _log.LogInfo("[Share] Session preset grace expired; falling back from stale host data");
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
            _log.LogInfo("[Share] Published room state: mode=" + _shareModeConfig.Value + ", actor=" +
                (PhotonNetwork.LocalPlayer == null ? -1 : PhotonNetwork.LocalPlayer.ActorNumber) + ", payloadPresets=" +
                (_shareModeConfig.Value == ThingPresetShareMode.PublishedPresets ? _localPresets.Count(preset => preset.Published) : 0) +
                ", hash=" + hash);
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
                    _log.LogInfo("[Share] No room share metadata found; clearing session presets");
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
                    _log.LogInfo("[Share] Share payload invalid or stale for current master; clearing session presets");
                    ClearSessionOverlay();
                }
                return;
            }

            ThingPresetShareMode shareMode = (ThingPresetShareMode)shareModeValue;
            if (shareMode != ThingPresetShareMode.PublishedPresets || string.IsNullOrWhiteSpace(payload))
            {
                _log.LogInfo("[Share] Master is not sharing published presets (mode=" + shareMode + "); using fallback presets");
                ClearSessionOverlay();
                return;
            }

            List<ThingPresetDefinition> presets = ThingPresetCodec.DeserializePresets(payload);
            if (presets.Count == 0)
            {
                _log.LogInfo("[Share] Share payload parsed but contained no presets; using fallback presets");
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
            _log.LogInfo("[Share] Applied shared presets from actor=" + actorNumber + ", count=" + _sessionPresets.Count +
                ", selected='" + _selectedSharedPresetId + "'");
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
                _log.LogInfo("[Share] Room properties updated; refreshing shared presets");
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
            _log.LogInfo("[Share] Master switched -> actor=" + (newMasterClient == null ? -1 : newMasterClient.ActorNumber) +
                ", localIsMaster=" + PhotonNetwork.IsMasterClient);
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

        public void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer) { }
        public void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer) { }
        public void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, PhotonHashtable changedProps) { }

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

        private static bool BackpackContains(Backpack backpack, ushort itemId)
        {
            BackpackData data;
            if (!TryGetBackpackData(backpack == null ? null : backpack.data, out data))
            {
                return false;
            }
            return data.itemSlots.Any(slot => slot != null && !slot.IsEmpty() && slot.prefab != null && slot.prefab.itemID == itemId);
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
