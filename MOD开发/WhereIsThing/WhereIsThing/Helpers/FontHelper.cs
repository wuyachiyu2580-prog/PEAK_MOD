using TMPro;
using UnityEngine;
using System.Collections.Generic;
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
            return IsChineseCapable(font, new HashSet<TMP_FontAsset>());
        }

        public static TMP_FontAsset GetLabelFont(ThingLabelFont choice)
        {
            string requestedSource;
            TMP_FontAsset requested = GetFontForChoice(choice, out requestedSource);
            TMP_FontAsset selected = requested;

            if (selected == null)
            {
                selected = GetGameDefaultFont();
            }

            return selected;
        }

        private static bool IsChineseCapable(TMP_FontAsset font, HashSet<TMP_FontAsset> visited)
        {
            if (font == null || !visited.Add(font))
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
                if (fallback != null && !ReferenceEquals(fallback, font) && IsChineseCapable(fallback, visited))
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

        private static TMP_FontAsset GetFontForChoice(ThingLabelFont choice, out string source)
        {
            switch (choice)
            {
                case ThingLabelFont.TmpDefault:
                    source = "TmpDefault";
                    return TMP_Settings.defaultFontAsset;
                case ThingLabelFont.KoreanBinggrae:
                    source = "KoreanBinggrae";
                    return FindFontAsset("Korean Binggrae-Bold SDF");
                case ThingLabelFont.Crazk:
                    source = "Crazk";
                    return FindFontAsset("CRAZK___ SDF");
                case ThingLabelFont.Auto:
                    source = "Auto(GameDefault)";
                    return GetGameDefaultFont() ?? TMP_Settings.defaultFontAsset;
                default:
                    source = "GameDefault";
                    return GetGameDefaultFont();
            }
        }

        private static TMP_FontAsset GetGameDefaultFont()
        {
            return FontFallbackSwapper.instance == null ? null : FontFallbackSwapper.instance.mainBaseFont;
        }

        private static TMP_FontAsset FindFontAsset(string assetName)
        {
            IEnumerable<TMP_FontAsset> loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            IEnumerable<TMP_FontAsset> resourceFonts;
            try
            {
                resourceFonts = Resources.LoadAll<TMP_FontAsset>(string.Empty);
            }
            catch
            {
                resourceFonts = Enumerable.Empty<TMP_FontAsset>();
            }

            return loadedFonts.Concat(resourceFonts)
                .Where(font => font != null)
                .Distinct()
                .FirstOrDefault(font => string.Equals(NormalizeFontName(font.name), NormalizeFontName(assetName),
                    System.StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeFontName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            int cloneSuffix = name.IndexOf(" (Clone)", System.StringComparison.OrdinalIgnoreCase);
            return (cloneSuffix < 0 ? name : name.Substring(0, cloneSuffix)).Trim();
        }

        private static TMP_FontAsset[] GetCandidateFonts()
        {
            System.Collections.Generic.List<TMP_FontAsset> fonts = new System.Collections.Generic.List<TMP_FontAsset>();

            TMP_FontAsset gameDefault = GetGameDefaultFont();
            if (gameDefault != null)
            {
                fonts.Add(gameDefault);
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
