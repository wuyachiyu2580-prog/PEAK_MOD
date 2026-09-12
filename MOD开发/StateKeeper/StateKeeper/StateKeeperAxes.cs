using System;
using TMPro;
using UnityEngine;

namespace StateKeeper
{
    internal sealed class StateKeeperAxes : MonoBehaviour
    {
        internal TextMeshProUGUI[] labels;
        internal float start, end;
        private float _width = -1;
        internal void ResetRange(float from, float to) { start = from; end = to; _width = -1; }
        private void LateUpdate()
        {
            float width = ((RectTransform)transform).rect.width - 112;
            if (labels == null || Math.Abs(width - _width) < .5f) return;
            _width = width;
            int count = width >= 1100 ? 7 : width >= 650 ? 5 : 3;
            string format = end >= 3600 ? "hh\\:mm\\:ss" : "mm\\:ss";
            float labelWidth = 90;
            while (count > 3 && width / (count - 1) < labelWidth + 12) count -= 2;
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].gameObject.SetActive(i < count);
                if (i >= count) continue;
                float ratio = (float)i / (count - 1);
                labels[i].text = TimeSpan.FromSeconds(Math.Max(0, start + (end - start) * ratio)).ToString(format);
                RectTransform rect = labels[i].rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0, 0); rect.pivot = new Vector2(ratio, 0);
                rect.anchoredPosition = new Vector2(88 + width * ratio, 0); rect.sizeDelta = new Vector2(labelWidth, 26);
                labels[i].alignment = i == 0 ? TextAlignmentOptions.Left : i == count - 1 ? TextAlignmentOptions.Right : TextAlignmentOptions.Center;
            }
        }
    }
}
