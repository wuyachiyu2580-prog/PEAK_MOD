using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Peak;
using Zorro.Core;

namespace WhereIsMyAmulet
{
    internal enum AmuletDisplayMode
    {
        Persistent,
        Timed
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class WhereIsMyAmuletPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.wuyachiyu.WhereIsMyAmulet";
        public const string PluginName = "WhereIsMyAmulet";
        public const string PluginVersion = "1.0.2";

        private readonly Dictionary<string, AmuletLabel> _labels = new Dictionary<string, AmuletLabel>();
        private readonly Dictionary<int, Item> _amuletDefinitions = new Dictionary<int, Item>();
        private ManualLogSource _log;
        private Canvas _canvas;
        private TMP_FontAsset _font;
        private Harmony _harmony;
        private bool _modConfigRefreshScheduled;
        private AmuletLabelFont _lastFontChoice;
        private float _lastFontSize;
        private float _hideAt = float.PositiveInfinity;

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<KeyCode> _scanKey;
        private ConfigEntry<AmuletDisplayMode> _scanMode;
        private ConfigEntry<float> _displayDuration;
        private ConfigEntry<float> _maxDistance;
        private ConfigEntry<float> _fontSize;
        private ConfigEntry<AmuletLabelFont> _fontChoice;
        private ConfigEntry<bool> _showStatueFragments;
        private ConfigEntry<bool> _showOffscreen;

        private void Awake()
        {
            _log = Logger;
            _enabled = Config.Bind("General", "Enabled", true, "Show labels for dropped amulet fragments.");
            _scanKey = Config.Bind("General", "ScanKey", KeyCode.C, "Press this key to scan for dropped amulet fragments.");
            _scanMode = Config.Bind("General", "ScanMode", AmuletDisplayMode.Persistent,
                "Keep labels visible or hide them automatically after a timed scan.");
            _displayDuration = Config.Bind("General", "DisplayDurationSeconds", 8f,
                "Timed display duration in seconds.");
            _maxDistance = Config.Bind("Display", "MaxDistance", 500f, "Maximum distance in metres for a label. Set to 0 for unlimited.");
            _fontSize = Config.Bind("Display", "FontSize", 22f, "Distance label font size.");
            _fontChoice = Config.Bind("Display", "LabelFont", AmuletLabelFont.GameDefault,
                "Font used by English location labels. Chinese text may display as tofu boxes.");
            _showStatueFragments = Config.Bind("Display", "ShowStatueFragments", true,
                "Show amulet fragments held by amulet statues.");
            _showOffscreen = Config.Bind("Display", "ShowOffscreenDirection", true, "Show a direction marker on the screen edge for offscreen fragments.");
            _lastFontChoice = _fontChoice.Value;
            _lastFontSize = _fontSize.Value;
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
            SceneManager.sceneLoaded += OnSceneLoaded;
            CreateCanvas();
            _log.LogInfo(PluginName + " v" + PluginVersion + " loaded. Press " + _scanKey.Value + " to scan for dropped fragments.");
        }

        private void Start()
        {
            ScheduleModConfigRefresh();
        }

        private void Update()
        {
            if (!_enabled.Value)
            {
                ClearLabels();
                return;
            }

            UpdateLabelStyleIfChanged();

            if (Input.GetKeyDown(_scanKey.Value))
            {
                ScanItems();
            }

            if (_scanMode.Value == AmuletDisplayMode.Timed && Time.unscaledTime >= _hideAt)
            {
                ClearLabels();
                return;
            }

            foreach (AmuletLabel label in _labels.Values.ToList())
            {
                if (label.IsValid)
                {
                    label.Update(Camera.main, _maxDistance.Value, _showOffscreen.Value);
                }
                else
                {
                    RemoveLabel(label.InstanceId);
                }
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
            ClearLabels();
            if (_canvas != null)
            {
                Destroy(_canvas.gameObject);
            }
        }

        private void CreateCanvas()
        {
            GameObject canvasObject = new GameObject("WhereIsMyAmuletCanvas");
            DontDestroyOnLoad(canvasObject);
            canvasObject.layer = 5;
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32700;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();
            _font = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ClearLabels();
            _amuletDefinitions.Clear();
            _font = null;
        }

        private void ScanItems()
        {
            if (!EnsureFont(true))
            {
                return;
            }

            RefreshAmuletDefinitions();
            Item[] items = FindObjectsByType<Item>(FindObjectsSortMode.None);
            HashSet<string> seen = new HashSet<string>();
            foreach (Item item in items)
            {
                if (item == null || !item.gameObject.activeInHierarchy || item.itemState != ItemState.Ground)
                {
                    continue;
                }

                int amuletIndex;
                if (TryGetAmuletIndex(item, out amuletIndex))
                {
                    string itemKey = "item:" + item.gameObject.GetInstanceID();
                    seen.Add(itemKey);
                    Item captured = item;
                    AddLabel(itemKey, captured.transform, () => GetItemName(captured), () =>
                        captured != null && captured.gameObject.activeInHierarchy && captured.itemState == ItemState.Ground && TryGetAmuletIndex(captured, out _));
                }

                Backpack backpack = item as Backpack;
                if (backpack != null && HasAmulet(backpack.data))
                {
                    string backpackKey = "backpack:" + backpack.gameObject.GetInstanceID();
                    seen.Add(backpackKey);
                    Backpack captured = backpack;
                    AddLabel(backpackKey, captured.transform, () => GetBackpackTitle(captured.data), () =>
                        captured != null && captured.gameObject.activeInHierarchy && captured.itemState == ItemState.Ground && HasAmulet(captured.data));
                }
            }

            if (_showStatueFragments.Value)
            {
                PropSpawner_AmuletStatues[] statues = Resources.FindObjectsOfTypeAll<PropSpawner_AmuletStatues>();
                HashSet<int> statueIds = new HashSet<int>();
                foreach (PropSpawner_AmuletStatues statue in statues)
                {
                    if (statue == null || !statue.gameObject.scene.IsValid() || !statue.gameObject.activeInHierarchy ||
                        !statueIds.Add(statue.gameObject.GetInstanceID()) || statue.transform.childCount == 0)
                    {
                        continue;
                    }

                    GameObject statueObject = statue.transform.GetChild(0).gameObject;
                    if (statueObject == null || !statueObject.activeInHierarchy)
                    {
                        continue;
                    }
                    FakeItem statueFragment = FindAmuletStatueFakeItem(statue, statueObject);
                    if (statueFragment == null)
                    {
                        continue;
                    }

                    string statueKey = "amulet-statue:" + statue.gameObject.GetInstanceID();
                    seen.Add(statueKey);
                    PropSpawner_AmuletStatues capturedStatue = statue;
                    GameObject capturedStatueObject = statueObject;
                    FakeItem capturedStatueFragment = statueFragment;
                    Transform target = capturedStatueFragment.transform;
                    Transform capturedTarget = target;
                    AddLabel(statueKey, target, () => GetAmuletStatueTitle(capturedStatue, capturedStatueObject), () =>
                        IsAmuletStatueValid(capturedStatue, capturedStatueObject, capturedTarget, capturedStatueFragment));
                }

                ScanScoutStatues(seen);
            }

            foreach (string id in _labels.Keys.ToList())
            {
                if (!seen.Contains(id))
                {
                    RemoveLabel(id);
                }
            }

            _hideAt = _scanMode.Value == AmuletDisplayMode.Timed
                ? Time.unscaledTime + Mathf.Max(0.5f, _displayDuration.Value)
                : float.PositiveInfinity;

        }

        private static Transform FindAmuletStatueTarget(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            Transform fallback = root;
            Queue<Transform> queue = new Queue<Transform>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                Transform current = queue.Dequeue();
                string name = current.gameObject.name.ToLowerInvariant();
                if (name.Contains("amulet") || name.Contains("gem") || name.Contains("fragment") || name.Contains("shard"))
                {
                    return current;
                }

                for (int i = 0; i < current.childCount; i++)
                {
                    queue.Enqueue(current.GetChild(i));
                }
            }

            return fallback;
        }

        private string GetAmuletStatueTitle(PropSpawner_AmuletStatues statue, GameObject statueObject)
        {
            int index;
            if (TryGetAmuletIndexFromStatueName(statueObject, out index))
            {
                return GetAmuletName(index);
            }

            if (statue != null && statue.props != null && statue.props.Length == 4)
            {
                int type = statue.statueIndex;
                if (type >= 0 && type < 4)
                {
                    return GetAmuletName(type);
                }
            }

            return IsChineseLanguage() ? "雕像碎片" : "Statue Fragment";
        }

        private static FakeItem FindAmuletStatueFakeItem(PropSpawner_AmuletStatues statue, GameObject statueObject)
        {
            if (statueObject == null)
            {
                return null;
            }

            int statueAmuletIndex;
            bool hasStatueAmuletIndex = TryGetAmuletIndexFromStatueName(statueObject, out statueAmuletIndex);
            if (!hasStatueAmuletIndex && statue != null && statue.props != null && statue.props.Length == 4 &&
                statue.statueIndex >= 0 && statue.statueIndex < 4)
            {
                statueAmuletIndex = statue.statueIndex;
                hasStatueAmuletIndex = true;
            }

            FakeItem fallback = null;
            FakeItem[] fakeItems = statueObject.GetComponentsInChildren<FakeItem>(true);
            foreach (FakeItem fakeItem in fakeItems)
            {
                if (fakeItem == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = fakeItem;
                }

                int fakeAmuletIndex;
                if (hasStatueAmuletIndex && fakeItem.realItemPrefab != null &&
                    TryGetAmuletIndex(fakeItem.realItemPrefab, out fakeAmuletIndex) &&
                    fakeAmuletIndex == statueAmuletIndex)
                {
                    return fakeItem;
                }
            }

            return fakeItems.Length == 1 ? fallback : null;
        }

        private static bool TryGetAmuletIndexFromStatueName(GameObject statueObject, out int index)
        {
            index = -1;
            if (statueObject == null)
            {
                return false;
            }

            string name = statueObject.name.ToLowerInvariant();
            if (name.Contains("doublejump") || name.Contains("double_jump") || name.Contains("superjump") || name.Contains("initiative"))
            {
                index = 0;
                return true;
            }
            if (name.Contains("infinitestam") || name.Contains("infinite_stam") || name.Contains("stamina") || name.Contains("ambition"))
            {
                index = 1;
                return true;
            }
            if (name.Contains("healing") || name.Contains("heal") || name.Contains("tenacity"))
            {
                index = 2;
                return true;
            }
            if (name.Contains("clone") || name.Contains("generosity"))
            {
                index = 3;
                return true;
            }

            return false;
        }

        private static bool IsAmuletStatueValid(PropSpawner_AmuletStatues statue, GameObject statueObject, Transform statueTarget, FakeItem statueFragment)
        {
            return statue != null && statue.gameObject.activeInHierarchy && statue.transform.childCount > 0 &&
                statueObject != null && statueObject.activeInHierarchy &&
                statue.transform.GetChild(0).gameObject == statueObject &&
                IsFakeStatueFragmentVisible(statueTarget, statueFragment);
        }

        private static bool IsFakeStatueFragmentVisible(Transform fragmentTarget, FakeItem fragmentItem)
        {
            return fragmentTarget != null && fragmentItem != null &&
                fragmentTarget.gameObject.activeInHierarchy && fragmentItem.gameObject.activeInHierarchy &&
                !fragmentItem.pickedUp;
        }

        private static bool IsStatueFragmentVisible(Transform fragmentTarget, Item fragmentItem)
        {
            if (fragmentTarget == null || !fragmentTarget.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (fragmentItem == null)
            {
                return true;
            }

            return fragmentItem.gameObject.activeInHierarchy && fragmentItem.itemState == ItemState.Ground;
        }

        private void ScanScoutStatues(HashSet<string> seen)
        {
            ScoutStatue[] statues = Resources.FindObjectsOfTypeAll<ScoutStatue>();
            HashSet<int> statueIds = new HashSet<int>();
            foreach (ScoutStatue statue in statues)
            {
                if (statue == null || !statue.gameObject.scene.IsValid() || !statue.gameObject.activeInHierarchy ||
                    !statueIds.Add(statue.gameObject.GetInstanceID()) || statue.amuletObjects == null)
                {
                    continue;
                }

                for (int index = 0; index < 4 && index < statue.amuletObjects.Length; index++)
                {
                    GameObject fragment = statue.amuletObjects[index];
                    if (fragment == null || !fragment.activeInHierarchy)
                    {
                        continue;
                    }

                    string statueKey = "statue:" + statue.gameObject.GetInstanceID() + ":" + index;
                    seen.Add(statueKey);
                    int capturedIndex = index;
                    ScoutStatue capturedStatue = statue;
                    GameObject capturedFragment = fragment;
                    Item capturedFragmentItem = fragment.GetComponentInChildren<Item>(true);
                    AddLabel(statueKey, capturedFragment.transform, () => GetAmuletName(capturedIndex), () =>
                        capturedStatue != null && capturedStatue.gameObject.activeInHierarchy &&
                        capturedStatue.amuletObjects != null && capturedIndex < capturedStatue.amuletObjects.Length &&
                        capturedStatue.amuletObjects[capturedIndex] == capturedFragment &&
                        IsStatueFragmentVisible(capturedFragment.transform, capturedFragmentItem));
                }
            }
        }

        private static bool TryGetAmuletIndex(Item item, out int index)
        {
            index = -1;
            if (item == null || !item.TryGetComponent<AmuletBase>(out AmuletBase amulet))
            {
                return false;
            }

            index = amulet.amuletIndex;
            return index >= 0 && index < 4;
        }

        private void AddLabel(string id, Transform target, Func<string> titleProvider, Func<bool> isValid)
        {
            if (_labels.ContainsKey(id))
            {
                return;
            }

            _labels.Add(id, new AmuletLabel(id, _canvas.transform, target, titleProvider, isValid, _font, _fontSize.Value));
        }

        private static bool HasAmulet(ItemInstanceData instanceData)
        {
            if (instanceData == null || !instanceData.TryGetDataEntry<BackpackData>(DataEntryKey.BackpackData, out BackpackData backpackData)
                || backpackData == null || backpackData.itemSlots == null)
            {
                return false;
            }

            foreach (ItemSlot slot in backpackData.itemSlots)
            {
                if (slot != null && !slot.IsEmpty() && slot.prefab != null && TryGetAmuletIndex(slot.prefab, out _))
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshAmuletDefinitions()
        {
            if (_amuletDefinitions.Count == 4)
            {
                return;
            }

            try
            {
                ItemDatabase database = SingletonAsset<ItemDatabase>.Instance;
                if (database == null || database.itemLookup == null)
                {
                    return;
                }

                foreach (Item item in database.itemLookup.Values)
                {
                    int index;
                    if (TryGetAmuletIndex(item, out index) && !_amuletDefinitions.ContainsKey(index))
                    {
                        _amuletDefinitions.Add(index, item);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogDebug("WhereIsMyAmulet: amulet definition lookup unavailable: " + ex.Message);
            }
        }

        private string GetItemName(Item item)
        {
            try
            {
                if (item != null)
                {
                    string name = item.GetName();
                    if (IsUsableLocalizedName(name))
                    {
                        return name;
                    }
                }
            }
            catch
            {
            }

            int index;
            return TryGetAmuletIndex(item, out index) ? GetAmuletName(index) : "Amulet";
        }

        private string GetAmuletName(int index)
        {
            Item definition;
            if (_amuletDefinitions.TryGetValue(index, out definition))
            {
                string name = GetItemNameFromDefinition(definition);
                if (IsUsableLocalizedName(name))
                {
                    return name;
                }
            }

            string localizationKey;
            switch (index)
            {
                case 0: localizationKey = "NAME_AMULET_DOUBLEJUMP"; break;
                case 1: localizationKey = "NAME_AMULET_INFINITESTAM"; break;
                case 2: localizationKey = "NAME_AMULET_HEALING"; break;
                case 3: localizationKey = "NAME_AMULET_CLONE"; break;
                default: return "Amulet";
            }

            try
            {
                string localized = LocalizedText.GetText(localizationKey);
                if (IsUsableLocalizedName(localized))
                {
                    return localized;
                }
            }
            catch
            {
            }

            switch (index)
            {
                case 0: return "Scout's Initiative";
                case 1: return "Scout's Ambition";
                case 2: return "Scout's Tenacity";
                case 3: return "Scout's Generosity";
                default: return "Amulet";
            }
        }

        private static string GetItemNameFromDefinition(Item definition)
        {
            try
            {
                return definition == null ? null : definition.GetName();
            }
            catch
            {
                return null;
            }
        }

        private string GetBackpackTitle(ItemInstanceData data)
        {
            List<string> names = new List<string>();
            BackpackData backpackData;
            if (data != null && data.TryGetDataEntry<BackpackData>(DataEntryKey.BackpackData, out backpackData) &&
                backpackData != null && backpackData.itemSlots != null)
            {
                foreach (ItemSlot slot in backpackData.itemSlots)
                {
                    int index;
                    if (slot == null || slot.IsEmpty() || slot.prefab == null || !TryGetAmuletIndex(slot.prefab, out index))
                    {
                        continue;
                    }

                    string name = GetItemName(slot.prefab);
                    if (!names.Contains(name))
                    {
                        names.Add(name);
                    }
                }
            }

            return string.Join("\n", names.ToArray()) + "\n" + (IsChineseLanguage() ? "背包内" : "In backpack");
        }

        private static bool IsUsableLocalizedName(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && !value.StartsWith("LOC: ", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsChineseLanguage()
        {
            return LocalizedText.CURRENT_LANGUAGE == LocalizedText.Language.SimplifiedChinese ||
                LocalizedText.CURRENT_LANGUAGE == LocalizedText.Language.TraditionalChinese;
        }

        private void RemoveLabel(string id)
        {
            AmuletLabel label;
            if (_labels.TryGetValue(id, out label))
            {
                label.Dispose();
                _labels.Remove(id);
            }
        }

        private void ClearLabels()
        {
            foreach (AmuletLabel label in _labels.Values)
            {
                label.Dispose();
            }
            _labels.Clear();
        }

        private IEnumerable<ConfigEntryBase> GetConfigEntries()
        {
            return new ConfigEntryBase[] { _enabled, _scanKey, _scanMode, _displayDuration, _maxDistance, _fontSize, _fontChoice,
                _showStatueFragments, _showOffscreen };
        }

        private void UpdateLabelStyleIfChanged()
        {
            if (_fontChoice.Value == _lastFontChoice && Mathf.Approximately(_fontSize.Value, _lastFontSize))
            {
                return;
            }

            _lastFontChoice = _fontChoice.Value;
            _lastFontSize = _fontSize.Value;
            _font = null;
            ApplyLabelStyleToExisting(true);
        }

        private bool EnsureFont(bool logResolution)
        {
            if (_font != null)
            {
                return true;
            }

            _font = FontHelper.GetLabelFont(_fontChoice.Value);
            if (_font == null)
            {
                if (logResolution)
                {
                    _log.LogWarning("No TMP font is currently available for amulet labels");
                }
                return false;
            }

            return true;
        }

        private void ApplyLabelStyleToExisting(bool logResolution)
        {
            if (!EnsureFont(logResolution))
            {
                return;
            }

            foreach (AmuletLabel label in _labels.Values)
            {
                label.ApplyStyle(_font, _fontSize.Value);
            }
        }

        private void OnGameLanguageChanged()
        {
            _amuletDefinitions.Clear();
            _font = null;
            ApplyLabelStyleToExisting(true);
            ModConfigLocalization.ApplyLocalizedDescriptions(GetConfigEntries());
            ModConfigLocalization.RefreshVisibleUi();
            ScheduleModConfigRefresh();
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

        private System.Collections.IEnumerator DeferredModConfigRefresh()
        {
            yield return null;
            ModConfigLocalization.ApplyLocalizedDescriptions(GetConfigEntries());
            ModConfigLocalization.RefreshVisibleUi();
            _modConfigRefreshScheduled = false;
        }

        private static TMP_FontAsset FindFont()
        {
            try
            {
                if (GUIManager.instance != null)
                {
                    AscentUI ascent = GUIManager.instance.GetComponentInChildren<AscentUI>(true);
                    if (ascent != null && ascent.text != null && ascent.text.font != null)
                    {
                        return ascent.text.font;
                    }

                    if (GUIManager.instance.heroDayText != null && GUIManager.instance.heroDayText.font != null)
                    {
                        return GUIManager.instance.heroDayText.font;
                    }
                }

                TextMeshProUGUI anyText = UnityEngine.Object.FindAnyObjectByType<TextMeshProUGUI>();
                if (anyText != null && anyText.font != null)
                {
                    return anyText.font;
                }

                if (TMP_Settings.defaultFontAsset != null)
                {
                    return TMP_Settings.defaultFontAsset;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("WhereIsMyAmulet: failed to find a game font: " + ex.Message);
            }

            return null;
        }
    }

    internal sealed class AmuletLabel
    {
        private readonly string _instanceId;
        private readonly Transform _target;
        private readonly Func<bool> _isValid;
        private readonly Func<string> _titleProvider;
        private readonly GameObject _root;
        private readonly CanvasGroup _group;
        private readonly List<TextMeshProUGUI> _titleShadowTexts = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> _distanceShadowTexts = new List<TextMeshProUGUI>();
        private readonly TextMeshProUGUI _titleText;
        private readonly TextMeshProUGUI _distanceText;
        private readonly TextMeshProUGUI _arrow;
        private float _fontSize;

        public string InstanceId { get { return _instanceId; } }
        public bool IsValid { get { return _target != null && _isValid != null && _isValid(); } }

        public AmuletLabel(string id, Transform canvas, Transform target, Func<string> titleProvider, Func<bool> isValid,
            TMP_FontAsset font, float fontSize)
        {
            _instanceId = id ?? string.Empty;
            _target = target;
            _titleProvider = titleProvider;
            _isValid = isValid;
            _fontSize = Mathf.Clamp(fontSize, 10f, 64f);
            _root = new GameObject("AmuletDistanceLabel");
            _root.transform.SetParent(canvas, false);
            _root.layer = 5;
            _group = _root.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            CreateShadowTexts("TitleShadow_", font, _fontSize, new Vector2(0f, 10f), _titleShadowTexts);
            _titleText = CreateText("TitleText", font, _fontSize, new Vector2(0f, 10f));
            _titleText.color = new Color(0.875f, 0.855f, 0.761f, 1f);

            CreateShadowTexts("DistanceShadow_", font, 18f, new Vector2(0f, -16f), _distanceShadowTexts);
            _distanceText = CreateText("DistanceText", font, 18f, new Vector2(0f, -16f));
            _distanceText.color = new Color(0.875f, 0.855f, 0.761f, 1f);

            GameObject arrowObject = new GameObject("OffscreenDirection");
            arrowObject.transform.SetParent(_root.transform, false);
            _arrow = arrowObject.AddComponent<TextMeshProUGUI>();
            arrowObject.layer = 5;
            _arrow.font = font;
            _arrow.text = "^";
            _arrow.alignment = TextAlignmentOptions.Center;
            _arrow.fontSize = 22f;
            _arrow.color = new Color(1f, 0.85f, 0.2f, 1f);
            _arrow.raycastTarget = false;
            _arrow.rectTransform.sizeDelta = new Vector2(18f, 18f);
            _arrow.rectTransform.anchoredPosition = new Vector2(0f, -28f);
            _arrow.enabled = false;
        }

        public void ApplyStyle(TMP_FontAsset font, float fontSize)
        {
            if (font == null)
            {
                return;
            }

            _fontSize = Mathf.Clamp(fontSize, 10f, 64f);
            ApplyFont(_titleText, font, _fontSize);
            foreach (TextMeshProUGUI shadow in _titleShadowTexts)
            {
                ApplyFont(shadow, font, _fontSize);
            }
            ApplyFont(_distanceText, font, 18f);
            foreach (TextMeshProUGUI shadow in _distanceShadowTexts)
            {
                ApplyFont(shadow, font, 18f);
            }
            _arrow.font = font;
        }

        private void CreateShadowTexts(string namePrefix, TMP_FontAsset font, float size, Vector2 position,
            List<TextMeshProUGUI> shadows)
        {
            for (int i = 0; i < ShadowOffsets.Length; i++)
            {
                TextMeshProUGUI shadow = CreateText(namePrefix + i, font, size, position + ShadowOffsets[i] * 1.5f);
                shadow.color = new Color(0f, 0f, 0f, 0.9f);
                shadows.Add(shadow);
            }
        }

        private static void ApplyFont(TextMeshProUGUI text, TMP_FontAsset font, float size)
        {
            text.font = font;
            text.fontSize = Mathf.Clamp(size, 10f, 64f);
        }

        private TextMeshProUGUI CreateText(string name, TMP_FontAsset font, float size, Vector2 position)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(_root.transform, false);
            textObject.layer = 5;
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = true;
            text.font = font;
            text.fontSize = Mathf.Clamp(size, 10f, 64f);
            text.rectTransform.sizeDelta = new Vector2(420f, 180f);
            text.rectTransform.anchoredPosition = position;
            return text;
        }

        public void Update(Camera camera, float maxDistance, bool showOffscreen)
        {
            if (camera == null)
            {
                SetVisible(false);
                return;
            }

            Vector3 worldPosition = _target.position + Vector3.up * 0.8f;
            Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
            float distance = Vector3.Distance(camera.transform.position, _target.position);
            bool withinDistance = maxDistance <= 0f || distance <= maxDistance;
            bool inFront = viewport.z > 0f;
            bool onScreen = inFront && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;

            if (onScreen && withinDistance)
            {
                _root.transform.position = camera.WorldToScreenPoint(worldPosition);
                string title = _titleProvider == null ? "Amulet" : _titleProvider();
                SetText(string.Format("<b>{0}</b>", title), string.Format("{0:F0}m", distance));
                _arrow.enabled = false;
                SetVisible(true);
                return;
            }

            if (!showOffscreen || !withinDistance)
            {
                SetVisible(false);
                return;
            }

            Vector2 direction = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            if (!inFront)
            {
                direction = -direction;
            }
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Vector2.up;
            }
            direction.Normalize();
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            float edge = Mathf.Min(Screen.width, Screen.height) * 0.42f;
            _root.transform.position = screenCenter + direction * edge;
            SetText(string.Empty, string.Format("{0:F0}m", distance));
            _arrow.enabled = true;
            _arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            SetVisible(true);
        }

        public void Dispose()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }
        }

        private void SetVisible(bool visible)
        {
            _group.alpha = visible ? 1f : 0f;
        }

        private void SetText(string title, string distance)
        {
            _titleText.text = title;
            foreach (TextMeshProUGUI shadow in _titleShadowTexts)
            {
                shadow.text = title;
            }
            _distanceText.text = distance;
            foreach (TextMeshProUGUI shadow in _distanceShadowTexts)
            {
                shadow.text = distance;
            }
        }

        private static readonly Vector2[] ShadowOffsets =
        {
            new Vector2(0f, 1f),
            new Vector2(0f, -1f),
            new Vector2(-1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-1f, -1f),
            new Vector2(1f, -1f)
        };
    }
}
