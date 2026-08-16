using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WhereIsThing
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class WhereIsThingPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.wuyachiyu.WhereIsThing";
        public const string PluginName = "WhereIsThing";
        public const string PluginVersion = "0.1.0";

        private readonly Dictionary<string, ThingLabel> _labels = new Dictionary<string, ThingLabel>();
        private readonly List<ThingTargetDefinition> _catalog = new List<ThingTargetDefinition>();
        private readonly HashSet<ushort> _selectedIds = new HashSet<ushort>();
        private readonly HashSet<ThingLuggageType> _selectedLuggageTypes = new HashSet<ThingLuggageType>();
        private readonly HashSet<ThingSceneTargetType> _selectedSceneTargetTypes = new HashSet<ThingSceneTargetType>();
        private ManualLogSource _log;
        private Canvas _canvas;
        private ThingSelectionWindow _window;
        private TMP_FontAsset _font;
        private bool _displayActive;
        private float _hideAt;
        private float _nextRefresh;
        private bool _catalogLogged;
        private Harmony _harmony;

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
            _selectedItemIds = Config.Bind("Selection", "SelectedItemIds", string.Empty, "Comma-separated item IDs selected in the window.");
            _selectedLuggage = Config.Bind("Selection", "SelectedLuggage", false, "Track unopened luggage targets.");
            _selectedLuggageTypesConfig = Config.Bind("Selection", "SelectedLuggageTypes", string.Empty, "Comma-separated luggage types selected in the window.");
            _selectedSceneTargetTypesConfig = Config.Bind("Selection", "SelectedSceneTargetTypes", string.Empty, "Comma-separated scene target types selected in the window.");
            _locationScopes = Config.Bind("Selection", "LocationScopes", ThingLocationScope.Ground | ThingLocationScope.Backpack | ThingLocationScope.Luggage,
                "Locations to scan: ground, held, backpack contents, or unopened luggage.");
            LoadSelection();

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
            StartCoroutine(DeferredModConfigRefresh());

            CreateCanvas();
            SceneManager.sceneLoaded += OnSceneLoaded;
            _log.LogInfo(PluginName + " v" + PluginVersion + " loaded. Hold Alt and press " + _windowKey.Value + " to choose items; press " + _scanKey.Value + " to scan.");
        }

        private void Update()
        {
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

            if (_scanMode.Value == ThingScanMode.Timed && Time.unscaledTime >= _hideAt)
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
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            LocalizedText.OnLangugageChanged -= OnGameLanguageChanged;
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }
            if (_window != null)
            {
                _window.Dispose();
            }
            ClearLabels();
            if (_canvas != null)
            {
                Destroy(_canvas.gameObject);
            }
        }

        private IEnumerator DeferredModConfigRefresh()
        {
            yield return null;
            ModConfigLocalization.ApplyLocalizedDescriptions(GetConfigEntries());
            ModConfigLocalization.RefreshCache();
        }

        private void OnGameLanguageChanged()
        {
            ModConfigLocalization.ApplyLocalizedDescriptions(GetConfigEntries());
            ModConfigLocalization.RefreshCache();
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

            if (_window == null)
            {
                _window = new ThingSelectionWindow(_canvas, _font, ApplyWindowChanges, null);
            }
            _window.Open(_catalog, _selectedIds, _selectedLuggageTypes, _selectedSceneTargetTypes,
                _nameLanguage.Value, _locationScopes.Value);
        }

        private void BeginScan()
        {
            if (!TryLoadCatalog())
            {
                return;
            }

            if (_selectedIds.Count == 0 && _selectedLuggageTypes.Count == 0 && _selectedSceneTargetTypes.Count == 0)
            {
                _displayActive = false;
                ClearLabels();
                _log.LogInfo("No targets selected. Hold Alt and press " + _windowKey.Value + " to choose targets.");
                return;
            }

            _displayActive = true;
            _hideAt = _scanMode.Value == ThingScanMode.Timed
                ? Time.unscaledTime + Mathf.Max(0.5f, _displayDuration.Value)
                : float.PositiveInfinity;
            _nextRefresh = 0f;
            RefreshLabels();
        }

        private void RefreshLabels()
        {
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

                if ((_locationScopes.Value & ThingLocationScope.Ground) != 0 && item.itemState == ItemState.Ground && _selectedIds.Contains(item.itemID))
                {
                    string key = "item:" + item.GetInstanceID();
                    seen.Add(key);
                    Item captured = item;
                    AddLabel(key, captured.transform, delegate { return ThingCatalog.GetDisplayName(captured, _nameLanguage.Value); },
                        delegate { return captured != null && captured.gameObject.activeInHierarchy && captured.itemState == ItemState.Ground && _selectedIds.Contains(captured.itemID); });
                }
                else if ((_locationScopes.Value & ThingLocationScope.Held) != 0 && item.itemState == ItemState.Held && _selectedIds.Contains(item.itemID))
                {
                    string key = "held:" + item.GetInstanceID();
                    seen.Add(key);
                    Item captured = item;
                    AddLabel(key, captured.transform, delegate { return ThingCatalog.GetDisplayName(captured, _nameLanguage.Value); },
                        delegate { return captured != null && captured.gameObject.activeInHierarchy && captured.itemState == ItemState.Held && _selectedIds.Contains(captured.itemID); });
                }

                Backpack backpack = item as Backpack;
                if ((_locationScopes.Value & ThingLocationScope.Backpack) == 0 || backpack == null || backpack.itemState != ItemState.Ground || !TryGetBackpackData(backpack.data, out BackpackData backpackData))
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
            HashSet<ThingSceneTargetType> selectedSceneTargetTypes, ThingNameLanguage language, ThingLocationScope scopes)
        {
            _selectedIds.Clear();
            _selectedIds.UnionWith(selection);
            _selectedLuggageTypes.Clear();
            _selectedLuggageTypes.UnionWith(selectedLuggageTypes ?? new HashSet<ThingLuggageType>());
            _selectedSceneTargetTypes.Clear();
            _selectedSceneTargetTypes.UnionWith(selectedSceneTargetTypes ?? new HashSet<ThingSceneTargetType>());
            _selectedLuggage.Value = _selectedLuggageTypes.Count > 0;
            _nameLanguage.Value = language;
            scopes &= ~ThingLocationScope.Luggage;
            if (_selectedLuggageTypes.Count > 0)
            {
                scopes |= ThingLocationScope.Luggage;
            }
            _locationScopes.Value = scopes;
            _selectedItemIds.Value = string.Join(",", _selectedIds.OrderBy(id => id).Select(id => id.ToString()).ToArray());
            _selectedLuggageTypesConfig.Value = SerializeLuggageTypes(_selectedLuggageTypes);
            _selectedSceneTargetTypesConfig.Value = SerializeSceneTargetTypes(_selectedSceneTargetTypes);
            if (_displayActive)
            {
                RefreshLabels();
            }
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
