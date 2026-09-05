using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace WhereIsThing
{
    internal sealed class ThingLabel
    {
        private readonly string _key;
        private readonly Transform _target;
        private readonly Func<Vector3> _positionProvider;
        private readonly Func<bool> _isValid;
        private readonly Func<string> _titleProvider;
        private readonly Func<string> _ownerProvider;
        private readonly GameObject _root;
        private readonly CanvasGroup _group;
        private readonly TextMeshProUGUI _mainTitle;
        private readonly TextMeshProUGUI _mainOwner;
        private readonly TextMeshProUGUI _mainDistance;
        private readonly TextMeshProUGUI _arrow;
        private float _fontSize;
        private string _cachedTitle = string.Empty;
        private string _cachedOwner = string.Empty;
        private string _lastTitle = string.Empty;
        private string _lastOwner = string.Empty;
        private string _lastDistance = string.Empty;
        private int _lastDistanceMeters;
        private bool _contentDirty = true;
        private bool _hasTextState;
        private bool _lastPlaceAboveTarget;
        private bool _visible;

        private const float LabelWidth = 420f;
        private const float OwnerFontScale = 0.78f;
        private const float DistanceFontScale = 0.78f;
        private const float RowGap = 1f;

        public ThingLabel(string key, Transform canvas, Transform target, Func<string> titleProvider, Func<bool> isValid,
            TMP_FontAsset font, Material titleMaterial, Material detailMaterial, float fontSize)
            : this(key, canvas, target, titleProvider, isValid, null, null, font, titleMaterial, detailMaterial, fontSize)
        {
        }

        public ThingLabel(string key, Transform canvas, Transform target, Func<string> titleProvider, Func<bool> isValid,
            Func<Vector3> positionProvider, TMP_FontAsset font, Material titleMaterial, Material detailMaterial, float fontSize)
            : this(key, canvas, target, titleProvider, isValid, positionProvider, null, font, titleMaterial, detailMaterial, fontSize)
        {
        }

        public ThingLabel(string key, Transform canvas, Transform target, Func<string> titleProvider, Func<bool> isValid,
            Func<Vector3> positionProvider, Func<string> ownerProvider, TMP_FontAsset font, Material titleMaterial,
            Material detailMaterial, float fontSize)
        {
            _key = key;
            _target = target;
            _positionProvider = positionProvider;
            _titleProvider = titleProvider;
            _ownerProvider = ownerProvider;
            _isValid = isValid;
            _fontSize = Mathf.Clamp(fontSize, 10f, 64f);

            _root = new GameObject("WhereIsThingLabel");
            _root.transform.SetParent(canvas, false);
            _root.layer = 5;
            _group = _root.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = 0f;

            _mainTitle = CreateText("MainTitle", font, titleMaterial, _fontSize);
            _mainOwner = CreateText("OwnerName", font, detailMaterial, _fontSize * OwnerFontScale);
            _mainDistance = CreateText("Distance", font, detailMaterial, _fontSize * DistanceFontScale);
            _mainTitle.color = new Color(0.875f, 0.855f, 0.761f, 1f);
            _mainOwner.color = new Color(0.847f, 0.788f, 0.584f, 1f);
            _mainDistance.color = new Color(0.784f, 0.745f, 0.600f, 1f);

            GameObject arrowObject = new GameObject("OffscreenDirection");
            arrowObject.transform.SetParent(_root.transform, false);
            arrowObject.layer = 5;
            _arrow = arrowObject.AddComponent<TextMeshProUGUI>();
            _arrow.font = font;
            _arrow.fontSharedMaterial = font.material;
            _arrow.UpdateMeshPadding();
            _arrow.text = "^";
            _arrow.alignment = TextAlignmentOptions.Center;
            _arrow.fontSize = 22f;
            _arrow.color = new Color(1f, 0.85f, 0.2f, 1f);
            _arrow.raycastTarget = false;
            _arrow.rectTransform.sizeDelta = new Vector2(18f, 18f);
            _arrow.rectTransform.anchoredPosition = new Vector2(0f, -28f);
            _arrow.enabled = false;
        }

        public string Key { get { return _key; } }
        public bool IsValid { get { return _target != null && _isValid != null && _isValid(); } }

        public void ApplyStyle(TMP_FontAsset font, Material titleMaterial, Material detailMaterial, float fontSize)
        {
            if (font == null)
            {
                return;
            }

            _fontSize = Mathf.Clamp(fontSize, 10f, 64f);
            _hasTextState = false;
            ApplyFont(_mainTitle, font, titleMaterial, _fontSize);
            ApplyFont(_mainOwner, font, detailMaterial, _fontSize * OwnerFontScale);
            ApplyFont(_mainDistance, font, detailMaterial, _fontSize * DistanceFontScale);
            _arrow.font = font;
            _arrow.fontSharedMaterial = font.material;
            _arrow.UpdateMeshPadding();
        }

        public void RefreshContent()
        {
            _cachedTitle = _titleProvider == null ? string.Empty : (_titleProvider() ?? string.Empty);
            _cachedOwner = _ownerProvider == null ? string.Empty : (_ownerProvider() ?? string.Empty).Trim();
            _contentDirty = false;
        }

        public void Update(Camera camera, float maxDistance, bool showOffscreen)
        {
            if (camera == null)
            {
                SetVisible(false);
                return;
            }

            Vector3 targetPosition = _positionProvider == null ? _target.position : _positionProvider();
            Vector3 worldPosition = targetPosition + Vector3.up * 0.8f;
            Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
            float distance = Vector3.Distance(camera.transform.position, targetPosition);
            bool withinDistance = maxDistance <= 0f || distance <= maxDistance;
            bool inFront = viewport.z > 0f;
            bool onScreen = inFront && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;

            if (onScreen && withinDistance)
            {
                _root.transform.position = camera.WorldToScreenPoint(worldPosition);
                EnsureContent();
                SetText(_cachedTitle, _cachedOwner, GetDistanceText(distance), true);
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
            SetText(string.Empty, string.Empty, GetDistanceText(distance), false);
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

        private TextMeshProUGUI CreateText(string name, TMP_FontAsset font, Material material, float size)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(_root.transform, false);
            textObject.layer = 5;
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.richText = false;
            text.font = font;
            text.fontSharedMaterial = material;
            text.UpdateMeshPadding();
            text.fontStyle = FontStyles.Normal;
            text.fontSize = Mathf.Clamp(size, 8f, 64f);
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = Mathf.Max(8f, text.fontSize * 0.65f);
            text.enableAutoSizing = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(LabelWidth, 24f);
            text.rectTransform.anchoredPosition = Vector2.zero;
            return text;
        }

        private static void ApplyFont(TextMeshProUGUI text, TMP_FontAsset font, Material material, float size)
        {
            text.font = font;
            text.fontSharedMaterial = material;
            text.UpdateMeshPadding();
            text.fontSize = Mathf.Clamp(size, 8f, 64f);
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = Mathf.Max(8f, text.fontSize * 0.65f);
        }

        private void SetVisible(bool visible)
        {
            if (_visible == visible)
            {
                return;
            }

            _visible = visible;
            _group.alpha = visible ? 1f : 0f;
        }

        private void SetText(string title, string owner, string distance, bool placeAboveTarget)
        {
            if (_hasTextState && string.Equals(_lastTitle, title, StringComparison.Ordinal) &&
                string.Equals(_lastOwner, owner, StringComparison.Ordinal) &&
                string.Equals(_lastDistance, distance, StringComparison.Ordinal) &&
                _lastPlaceAboveTarget == placeAboveTarget)
            {
                return;
            }

            _hasTextState = true;
            _lastTitle = title;
            _lastOwner = owner;
            _lastDistance = distance;
            _lastPlaceAboveTarget = placeAboveTarget;
            if (!placeAboveTarget)
            {
                SetTextPart(_mainTitle, string.Empty, false, 0f, 24f, new Vector2(0.5f, 0.5f), Vector2.zero);
                SetTextPart(_mainOwner, string.Empty, false, 0f, 24f, new Vector2(0.5f, 0.5f), Vector2.zero);
                SetTextPart(_mainDistance, distance, true, 0f, 24f, new Vector2(0.5f, 0.5f), Vector2.zero);
                return;
            }

            int titleLines = CountLines(title);
            float titleHeight = Mathf.Max(18f, titleLines * _fontSize * 1.15f);
            float ownerHeight = Mathf.Max(16f, _fontSize * OwnerFontScale * 1.15f);
            float distanceHeight = Mathf.Max(16f, _fontSize * DistanceFontScale * 1.15f);
            float distanceY = 0f;
            float ownerY = distanceHeight + RowGap;
            float titleY = string.IsNullOrEmpty(owner) ? ownerY : ownerY + ownerHeight + RowGap;

            SetTextPart(_mainTitle, title, true, titleY, titleHeight, new Vector2(0.5f, 0f), Vector2.zero);
            SetTextPart(_mainOwner, owner, !string.IsNullOrEmpty(owner), ownerY, ownerHeight, new Vector2(0.5f, 0f), Vector2.zero);
            SetTextPart(_mainDistance, distance, true, distanceY, distanceHeight, new Vector2(0.5f, 0f), Vector2.zero);
        }

        private void EnsureContent()
        {
            if (_contentDirty)
            {
                RefreshContent();
            }
        }

        private string GetDistanceText(float distance)
        {
            int distanceMeters = Mathf.RoundToInt(distance);
            if (distanceMeters == _lastDistanceMeters && !string.IsNullOrEmpty(_lastDistance))
            {
                return _lastDistance;
            }

            _lastDistanceMeters = distanceMeters;
            return distanceMeters.ToString(CultureInfo.InvariantCulture) + "m";
        }

        private static void SetTextPart(TextMeshProUGUI text, string value, bool enabled, float y, float height,
            Vector2 pivot, Vector2 position)
        {
            text.text = value;
            text.enabled = enabled;
            text.rectTransform.sizeDelta = new Vector2(LabelWidth, height);
            text.rectTransform.pivot = pivot;
            text.rectTransform.anchoredPosition = position + new Vector2(0f, y);
        }

        private static int CountLines(string value)
        {
            int lines = 1;
            if (!string.IsNullOrEmpty(value))
            {
                foreach (char character in value)
                {
                    if (character == '\n')
                    {
                        lines++;
                    }
                }
            }
            return lines;
        }

    }
}
