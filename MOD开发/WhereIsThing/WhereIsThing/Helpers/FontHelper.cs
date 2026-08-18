using TMPro;
using UnityEngine;
using System.Linq;

namespace WhereIsThing
{
    internal static class FontHelper
    {
        private const string ChineseSample = "物品在哪简体中文急救箱绷带药用根茎灵药菇";
        private static TMP_FontAsset _cached;

        public static TMP_FontAsset GetChineseCapable()
        {
            if (IsChineseCapable(_cached))
            {
                return _cached;
            }

            try
            {
                TMP_FontAsset preferred = FindPreferredFont();
                if (preferred != null)
                {
                    _cached = preferred;
                    return preferred;
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

        public static bool IsChineseCapable(TMP_FontAsset font)
        {
            if (font == null)
            {
                return false;
            }

            try
            {
                if (font.HasCharacters(ChineseSample))
                {
                    return true;
                }
            }
            catch
            {
            }

            if (font.fallbackFontAssetTable == null)
            {
                return false;
            }

            foreach (TMP_FontAsset fallback in font.fallbackFontAssetTable)
            {
                if (fallback != null && !ReferenceEquals(fallback, font) && IsChineseCapable(fallback))
                {
                    return true;
                }
            }

            return false;
        }

        private static TMP_FontAsset FindPreferredFont()
        {
            foreach (TMP_FontAsset font in GetCandidateFonts())
            {
                if (IsChineseCapable(font))
                {
                    return font;
                }
            }

            foreach (TMP_FontAsset font in GetCandidateFonts())
            {
                if (font != null)
                {
                    return font;
                }
            }

            return null;
        }

        private static TMP_FontAsset[] GetCandidateFonts()
        {
            System.Collections.Generic.List<TMP_FontAsset> fonts = new System.Collections.Generic.List<TMP_FontAsset>();

            if (GUIManager.instance != null)
            {
                AscentUI ascent = GUIManager.instance.GetComponentInChildren<AscentUI>(true);
                if (ascent != null && ascent.text != null && ascent.text.font != null)
                {
                    fonts.Add(ascent.text.font);
                }

                if (GUIManager.instance.heroDayText != null && GUIManager.instance.heroDayText.font != null)
                {
                    fonts.Add(GUIManager.instance.heroDayText.font);
                }
            }

            TextMeshProUGUI anyText = Object.FindAnyObjectByType<TextMeshProUGUI>();
            if (anyText != null && anyText.font != null)
            {
                fonts.Add(anyText.font);
            }

            if (TMP_Settings.defaultFontAsset != null)
            {
                fonts.Add(TMP_Settings.defaultFontAsset);
            }

            foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (font != null)
                {
                    fonts.Add(font);
                }
            }

            return fonts.Distinct().ToArray();
        }
    }
}
