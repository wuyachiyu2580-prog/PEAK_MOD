using TMPro;
using UnityEngine;

namespace WhereIsThing
{
    internal static class FontHelper
    {
        private static TMP_FontAsset _cached;

        public static TMP_FontAsset GetChineseCapable()
        {
            if (_cached != null)
            {
                return _cached;
            }

            try
            {
                if (GUIManager.instance != null)
                {
                    AscentUI ascent = GUIManager.instance.GetComponentInChildren<AscentUI>(true);
                    if (ascent != null && ascent.text != null && ascent.text.font != null)
                    {
                        _cached = ascent.text.font;
                        return _cached;
                    }

                    if (GUIManager.instance.heroDayText != null && GUIManager.instance.heroDayText.font != null)
                    {
                        _cached = GUIManager.instance.heroDayText.font;
                        return _cached;
                    }
                }

                TextMeshProUGUI anyText = Object.FindAnyObjectByType<TextMeshProUGUI>();
                if (anyText != null && anyText.font != null)
                {
                    _cached = anyText.font;
                    return _cached;
                }

                if (TMP_Settings.defaultFontAsset != null)
                {
                    _cached = TMP_Settings.defaultFontAsset;
                    return _cached;
                }
            }
            catch
            {
                // UI assets can be unavailable during the first loading frame.
            }

            return null;
        }

        public static void InvalidateCache()
        {
            _cached = null;
        }
    }
}
