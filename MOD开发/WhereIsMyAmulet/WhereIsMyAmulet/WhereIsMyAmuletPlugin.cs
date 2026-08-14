using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WhereIsMyAmulet
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class WhereIsMyAmuletPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.wuyachiyu.WhereIsMyAmulet";
        public const string PluginName = "WhereIsMyAmulet";
        public const string PluginVersion = "1.0.0";

        private readonly Dictionary<int, AmuletLabel> _labels = new Dictionary<int, AmuletLabel>();
        private ManualLogSource _log;
        private Canvas _canvas;
        private TMP_FontAsset _font;
        private bool _loggedDiscovery;

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<KeyCode> _scanKey;
        private ConfigEntry<float> _maxDistance;
        private ConfigEntry<float> _fontSize;
        private ConfigEntry<bool> _showOffscreen;

        private void Awake()
        {
            _log = Logger;
            _enabled = Config.Bind("General", "Enabled", true, "Show labels for dropped amulet fragments.");
            _scanKey = Config.Bind("General", "ScanKey", KeyCode.C, "Press this key to scan for dropped amulet fragments.");
            _maxDistance = Config.Bind("Display", "MaxDistance", 500f, "Maximum distance in metres for a label. Set to 0 for unlimited.");
            _fontSize = Config.Bind("Display", "FontSize", 22f, "Distance label font size.");
            _showOffscreen = Config.Bind("Display", "ShowOffscreenDirection", true, "Show a direction marker on the screen edge for offscreen fragments.");
            CreateCanvas();
            _log.LogInfo(PluginName + " v" + PluginVersion + " loaded. Press " + _scanKey.Value + " to scan for dropped fragments.");
        }

        private void Update()
        {
            if (!_enabled.Value)
            {
                ClearLabels();
                return;
            }

            if (Input.GetKeyDown(_scanKey.Value))
            {
                ScanItems();
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
            _font = FindFont();
        }

        private void ScanItems()
        {
            if (_font == null)
            {
                _font = FindFont();
            }

            Item[] items = FindObjectsByType<Item>(FindObjectsSortMode.None);
            HashSet<int> seen = new HashSet<int>();
            foreach (Item item in items)
            {
                if (item == null || !item.gameObject.activeInHierarchy || item.itemState != ItemState.Ground)
                {
                    continue;
                }

                if (Matches(item))
                {
                    int itemId = item.gameObject.GetInstanceID();
                    seen.Add(itemId);
                    AddLabel(itemId, item.transform, "Amulet", () =>
                        item != null && item.gameObject.activeInHierarchy && item.itemState == ItemState.Ground);
                }

                Backpack backpack = item as Backpack;
                if (backpack != null && HasAmulet(backpack.data))
                {
                    int backpackId = backpack.gameObject.GetInstanceID();
                    seen.Add(backpackId);
                    AddLabel(backpackId, backpack.transform, "Backpack\nAmulet", () =>
                        backpack != null && backpack.gameObject.activeInHierarchy && backpack.itemState == ItemState.Ground && HasAmulet(backpack.data));
                }
            }

            foreach (int id in _labels.Keys.ToList())
            {
                if (!seen.Contains(id))
                {
                    RemoveLabel(id);
                }
            }
        }

        private static bool Matches(Item item)
        {
            return item.gameObject.name.IndexOf("amulet", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AddLabel(int id, Transform target, string title, Func<bool> isValid)
        {
            if (_labels.ContainsKey(id))
            {
                return;
            }

            _labels.Add(id, new AmuletLabel(_canvas.transform, target, title, isValid, _font, _fontSize));
            if (!_loggedDiscovery)
            {
                _log.LogInfo("Found amulet target: " + title);
                _loggedDiscovery = true;
            }
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
                if (slot != null && !slot.IsEmpty() && slot.prefab != null && Matches(slot.prefab))
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveLabel(int id)
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
        private readonly int _instanceId;
        private readonly Transform _target;
        private readonly Func<bool> _isValid;
        private readonly string _title;
        private readonly GameObject _root;
        private readonly CanvasGroup _group;
        private readonly List<TextMeshProUGUI> _shadowTexts = new List<TextMeshProUGUI>();
        private readonly TextMeshProUGUI _mainText;
        private readonly TextMeshProUGUI _arrow;

        public int InstanceId { get { return _instanceId; } }
        public bool IsValid { get { return _target != null && _isValid != null && _isValid(); } }

        public AmuletLabel(Transform canvas, Transform target, string title, Func<bool> isValid, TMP_FontAsset font, ConfigEntry<float> fontSize)
        {
            _instanceId = target == null ? 0 : target.gameObject.GetInstanceID();
            _target = target;
            _title = title;
            _isValid = isValid;
            _root = new GameObject("AmuletDistanceLabel");
            _root.transform.SetParent(canvas, false);
            _root.layer = 5;
            _group = _root.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            for (int i = 0; i < 8; i++)
            {
                TextMeshProUGUI shadow = CreateText("Shadow_" + i, font, fontSize.Value);
                shadow.color = new Color(0f, 0f, 0f, 0.9f);
                shadow.rectTransform.anchoredPosition = ShadowOffsets[i] * 1.5f;
                _shadowTexts.Add(shadow);
            }

            _mainText = CreateText("MainText", font, fontSize.Value);
            _mainText.color = new Color(0.875f, 0.855f, 0.761f, 1f);

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

        private TextMeshProUGUI CreateText(string name, TMP_FontAsset font, float size)
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
            text.rectTransform.sizeDelta = new Vector2(260f, 90f);
            text.rectTransform.anchoredPosition = Vector2.zero;
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
                SetText(string.Format("<b>{0}</b>\n<size=18>{1:F0}m</size>", _title, distance));
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
            SetText(string.Format("{0:F0}m", distance));
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

        private void SetText(string value)
        {
            _mainText.text = value;
            foreach (TextMeshProUGUI shadow in _shadowTexts)
            {
                shadow.text = value;
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
