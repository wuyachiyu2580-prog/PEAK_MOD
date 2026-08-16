using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WhereIsThing
{
    internal sealed class ThingLabel
    {
        private readonly string _key;
        private readonly Transform _target;
        private readonly Func<bool> _isValid;
        private readonly Func<string> _titleProvider;
        private readonly GameObject _root;
        private readonly CanvasGroup _group;
        private readonly List<TextMeshProUGUI> _shadowTexts = new List<TextMeshProUGUI>();
        private readonly TextMeshProUGUI _mainText;
        private readonly TextMeshProUGUI _arrow;
        private readonly float _fontSize;

        public ThingLabel(string key, Transform canvas, Transform target, Func<string> titleProvider, Func<bool> isValid, TMP_FontAsset font, float fontSize)
        {
            _key = key;
            _target = target;
            _titleProvider = titleProvider;
            _isValid = isValid;
            _fontSize = fontSize;

            _root = new GameObject("WhereIsThingLabel");
            _root.transform.SetParent(canvas, false);
            _root.layer = 5;
            _group = _root.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            for (int i = 0; i < 8; i++)
            {
                TextMeshProUGUI shadow = CreateText("Shadow_" + i, font, fontSize);
                shadow.color = new Color(0f, 0f, 0f, 0.9f);
                shadow.rectTransform.anchoredPosition = ShadowOffsets[i] * 1.5f;
                _shadowTexts.Add(shadow);
            }

            _mainText = CreateText("MainText", font, fontSize);
            _mainText.color = new Color(0.875f, 0.855f, 0.761f, 1f);

            GameObject arrowObject = new GameObject("OffscreenDirection");
            arrowObject.transform.SetParent(_root.transform, false);
            arrowObject.layer = 5;
            _arrow = arrowObject.AddComponent<TextMeshProUGUI>();
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

        public string Key { get { return _key; } }
        public bool IsValid { get { return _target != null && _isValid != null && _isValid(); } }

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
                SetText(string.Format("<b>{0}</b>\n<size=18>{1:F0}m</size>", _titleProvider(), distance));
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
            text.rectTransform.sizeDelta = new Vector2(320f, 96f);
            text.rectTransform.anchoredPosition = Vector2.zero;
            return text;
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
