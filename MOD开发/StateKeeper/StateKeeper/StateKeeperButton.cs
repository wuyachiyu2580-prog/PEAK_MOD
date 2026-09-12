using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace StateKeeper
{
    internal sealed class StateKeeperButton
    {
        internal readonly GameObject GameObject;
        internal readonly RectTransform RectTransform;
        internal readonly Button Button;
        internal readonly TextMeshProUGUI Text;

        internal bool IsAlive
        {
            get { return GameObject != null && RectTransform != null && Button != null; }
        }

        internal StateKeeperButton(GameObject gameObject)
        {
            GameObject = gameObject;
            RectTransform = gameObject.GetComponent<RectTransform>();
            Button = gameObject.GetComponent<Button>();
            Text = gameObject.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        internal static StateKeeperButton Create(GameObject template, string name, Transform parent)
        {
            GameObject buttonObject = Object.Instantiate(template, parent, false);
            buttonObject.name = name;
            LocalizedText localized = buttonObject.GetComponentInChildren<LocalizedText>(true);
            if (localized != null) Object.DestroyImmediate(localized);
            StateKeeperButton button = new StateKeeperButton(buttonObject);
            if (button.Button != null) button.Button.onClick = new Button.ButtonClickedEvent();
            return button;
        }

        internal void SetText(string text)
        {
            if (Text != null) Text.text = text;
        }

        internal void SetCompactText(string text)
        {
            if (Text == null) return;
            Text.text = text;
            Text.enableAutoSizing = true;
            Text.fontSizeMin = 18f;
            Text.fontSizeMax = 20f;
            Text.fontSize = 20f;
            Text.alignment = TextAlignmentOptions.Center;
            Text.textWrappingMode = TextWrappingModes.NoWrap;
            Text.overflowMode = TextOverflowModes.Ellipsis;
            Text.rectTransform.anchorMin = Vector2.zero;
            Text.rectTransform.anchorMax = Vector2.one;
            Text.rectTransform.offsetMin = new Vector2(16f, 0f);
            Text.rectTransform.offsetMax = new Vector2(-16f, 0f);
            Text.transform.localScale = Vector3.one;
        }

        internal void AddListener(UnityAction action)
        {
            if (Button != null) Button.onClick.AddListener(action);
        }

        internal void SetInteractable(bool value)
        {
            if (Button != null) Button.interactable = value;
        }
    }
}
